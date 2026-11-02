using System.Text;
using System.Threading.Channels;

namespace Censio.Pipe;

/// <summary>
/// Writes the frames of the pipe to a recording file on a background writer without ever waiting.
/// </summary>
public class PipeRecorder : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The most bytes that may be queued for the writer.
    /// </summary>
    public const long MaxQueuedBytes = 16777216;

    /// <summary>
    /// The bytes counted for each queued entry on top of its encoded frame.
    /// </summary>
    public const int EntryOverhead = 128;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The factory that opens the recording file for a start time.
    /// </summary>
    private readonly Func<DateTimeOffset, Stream> _openFile;

    /// <summary>
    /// The queue between the callers of <see cref="Record" /> and the writer.
    /// </summary>
    private readonly Channel<(ReadOnlyMemory<byte> Frame, DateTimeOffset Time, long Cost)> _queue = Channel.CreateUnbounded<(ReadOnlyMemory<byte> Frame, DateTimeOffset Time, long Cost)>(new UnboundedChannelOptions
                                                                                                                                                                                          {
                                                                                                                                                                                              SingleReader = true
                                                                                                                                                                                          });

    /// <summary>
    /// The lock that starts the writer once.
    /// </summary>
    private readonly object _writerLock = new();

    /// <summary>
    /// The bytes counted for the entries not yet flushed.
    /// </summary>
    private long _queuedBytes;

    /// <summary>
    /// Whether the recorder has failed (0 or 1).
    /// </summary>
    private int _failed;

    /// <summary>
    /// The task of the writer, or <see langword="null" /> until the first frame.
    /// </summary>
    private Task? _writer;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecorder" /> class.
    /// </summary>
    /// <param name="openFile">The factory that opens the recording file for a start time</param>
    public PipeRecorder(Func<DateTimeOffset, Stream> openFile)
    {
        _openFile = openFile;
    }

    #endregion // Constructors

    #region Events

    /// <summary>
    /// Raised once when the recorder has failed.
    /// </summary>
    public event EventHandler? Failed;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the recorder has failed.
    /// </summary>
    public bool HasFailed => Volatile.Read(ref _failed) == 1;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Calculates the queue cost of a frame.
    /// </summary>
    /// <param name="frameLength">The frame length in bytes</param>
    /// <returns>The cost in bytes</returns>
    public static long EntryCost(int frameLength)
    {
        return (4L * (((long)frameLength + 2) / 3)) + EntryOverhead;
    }

    /// <summary>
    /// Queues the frame of a read result for writing.
    /// </summary>
    /// <param name="result">The read result</param>
    /// <param name="receiveTime">The time the result was received</param>
    public void Record(PipeReadResult result, DateTimeOffset receiveTime)
    {
        if (result.Frame is not { } frame || HasFailed)
        {
            return;
        }

        var cost = EntryCost(frame.Length);

        if (Interlocked.Add(ref _queuedBytes, cost) > MaxQueuedBytes)
        {
            Interlocked.Add(ref _queuedBytes, -cost);
            _queue.Writer.TryComplete();
            Fail();

            return;
        }

        StartWriter();

        if (_queue.Writer.TryWrite((frame, receiveTime, cost)) == false)
        {
            Interlocked.Add(ref _queuedBytes, -cost);
        }
    }

    /// <summary>
    /// Closes the queue and waits for the writer.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the wait</param>
    /// <returns>The task</returns>
    public Task CompleteAsync(CancellationToken cancellationToken)
    {
        Task? writer;

        _queue.Writer.TryComplete();

        lock (_writerLock)
        {
            writer = _writer;
        }

        return writer is null ? Task.CompletedTask : writer.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Writes a line as one write and flushes it.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="line">The line with its terminator</param>
    /// <returns>The task</returns>
    private static async Task WriteLineAsync(Stream stream, string line)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(line)).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the writer when it is not running yet.
    /// </summary>
    private void StartWriter()
    {
        lock (_writerLock)
        {
            _writer ??= Task.Run(WriteAsync);
        }
    }

    /// <summary>
    /// Marks the recorder as failed and raises <see cref="Failed" /> once.
    /// </summary>
    private void Fail()
    {
        if (Interlocked.Exchange(ref _failed, 1) == 1)
        {
            return;
        }

        var handlers = Failed;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // A handler must never stop the recorder
            }
        }
    }

    /// <summary>
    /// Writes the queued entries until the queue is closed and empty, or until a write fails.
    /// </summary>
    /// <returns>The task</returns>
    private async Task WriteAsync()
    {
        Stream? stream = null;

        try
        {
            while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var entry))
                {
                    if (stream is null)
                    {
                        stream = _openFile(entry.Time);

                        await WriteLineAsync(stream, PipeRecordingFormat.FormatHeader(entry.Time)).ConfigureAwait(false);
                    }

                    await WriteLineAsync(stream, PipeRecordingFormat.FormatFrameLine(entry.Time, entry.Frame.Span)).ConfigureAwait(false);
                    Interlocked.Add(ref _queuedBytes, -entry.Cost);
                }
            }
        }
        catch (Exception)
        {
            _queue.Writer.TryComplete();
            Fail();
        }
        finally
        {
            await CloseAsync(stream).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the stream; a failure to close fails the recorder.
    /// </summary>
    /// <param name="stream">The stream, or <see langword="null" /> when none was opened</param>
    /// <returns>The task</returns>
    private async Task CloseAsync(Stream? stream)
    {
        if (stream is null)
        {
            return;
        }

        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            Fail();
        }
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CompleteAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    #endregion // IAsyncDisposable
}