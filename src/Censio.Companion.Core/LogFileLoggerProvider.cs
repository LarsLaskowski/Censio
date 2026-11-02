using System.Text;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Writes log entries to rotating files in a folder.
/// </summary>
public sealed class LogFileLoggerProvider : ILoggerProvider
{
    #region Constants

    /// <summary>
    /// The maximum size of a log file in bytes.
    /// </summary>
    public const long MaxFileBytes = 10485760;

    /// <summary>
    /// The maximum number of log files kept.
    /// </summary>
    public const int MaxFiles = 10;

    /// <summary>
    /// The maximum number of characters queued for writing.
    /// </summary>
    public const int MaxQueuedChars = 1048576;

    /// <summary>
    /// The highest number of a log file of a day.
    /// </summary>
    private const int MaxSequence = 9999;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The log folder.
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Whether the folder is a full path.
    /// </summary>
    private readonly bool _usable;

    /// <summary>
    /// The queue of the writer.
    /// </summary>
    private readonly Channel<LogQueueItem> _queue = Channel.CreateUnbounded<LogQueueItem>(new UnboundedChannelOptions
                                                                                          {
                                                                                              SingleReader = true
                                                                                          });

    /// <summary>
    /// Stops the writer.
    /// </summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>
    /// The task of the writer.
    /// </summary>
    private readonly Task _writer;

    /// <summary>
    /// The number of characters queued.
    /// </summary>
    private int _queuedChars;

    /// <summary>
    /// Whether the provider is disposed.
    /// </summary>
    private int _disposed;

    /// <summary>
    /// The open file, only used by the writer.
    /// </summary>
    private FileStream? _stream;

    /// <summary>
    /// The size of the open file in bytes, only used by the writer.
    /// </summary>
    private long _streamLength;

    /// <summary>
    /// The date of the open file, only used by the writer.
    /// </summary>
    private DateOnly _streamDate;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LogFileLoggerProvider" /> class.
    /// </summary>
    /// <param name="folder">The log folder</param>
    /// <param name="timeProvider">The time provider</param>
    public LogFileLoggerProvider(string folder, TimeProvider timeProvider)
    {
        _folder = folder;
        _timeProvider = timeProvider;
        _usable = Path.IsPathFullyQualified(folder);
        _writer = Task.Run(RunWriterAsync, CancellationToken.None);
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the time provider.
    /// </summary>
    internal TimeProvider TimeProvider => _timeProvider;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Waits until the queue is written or the timeout has passed.
    /// </summary>
    /// <param name="timeout">The timeout</param>
    /// <returns>The task</returns>
    public async Task FlushAsync(TimeSpan timeout)
    {
        var marker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (_queue.Writer.TryWrite(new LogQueueItem(default, string.Empty, marker)) == false)
        {
            return;
        }

        try
        {
            await marker.Task.WaitAsync(timeout, _timeProvider, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The writer is stalled; the caller goes on without the rest of the queue.
        }
    }

    /// <summary>
    /// Queues a line for writing, or drops it if the queue is full or the folder is not a full path.
    /// </summary>
    /// <param name="date">The local date of the line</param>
    /// <param name="line">The line</param>
    internal void Enqueue(DateOnly date, string line)
    {
        if (_usable == false)
        {
            return;
        }

        if (Interlocked.Add(ref _queuedChars, line.Length) > MaxQueuedChars
            || _queue.Writer.TryWrite(new LogQueueItem(date, line, null)) == false)
        {
            Interlocked.Add(ref _queuedChars, -line.Length);
        }
    }

    /// <summary>
    /// Handles the items of the queue until the provider is disposed.
    /// </summary>
    /// <returns>The task</returns>
    private async Task RunWriterAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                if (item.Marker is null)
                {
                    Write(item.Date, item.Line);
                    Interlocked.Add(ref _queuedChars, -item.Line.Length);
                }
                else
                {
                    item.Marker.TrySetResult();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The provider is disposed.
        }
        finally
        {
            CloseStream();

            while (_queue.Reader.TryRead(out var item))
            {
                item.Marker?.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Writes a line to the file of its date; an I/O failure drops the line.
    /// </summary>
    /// <param name="date">The local date of the line</param>
    /// <param name="line">The line</param>
    private void Write(DateOnly date, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);

        try
        {
            if (_stream is null || _streamDate != date || _streamLength + bytes.Length > MaxFileBytes)
            {
                OpenFile(date, bytes.Length);
            }

            if (_stream is not null)
            {
                _stream.Write(bytes);
                _stream.Flush();
                _streamLength += bytes.Length;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            CloseStream();
        }
    }

    /// <summary>
    /// Opens the file for a line, closing the open one, and prunes the folder after a new file; without a file
    /// that fits (number 9999 is full), no file is open afterwards.
    /// </summary>
    /// <param name="date">The local date of the line</param>
    /// <param name="length">The length of the line in bytes</param>
    private void OpenFile(DateOnly date, int length)
    {
        CloseStream();

        Directory.CreateDirectory(_folder);

        var highest = 0;

        foreach (var path in Directory.EnumerateFiles(_folder))
        {
            if (LogFileNames.TryParse(Path.GetFileName(path), out var fileDate, out var sequence) && fileDate == date && sequence > highest)
            {
                highest = sequence;
            }
        }

        var number = Math.Max(highest, 1);

        if (highest > 0 && new FileInfo(Path.Combine(_folder, LogFileNames.Format(date, highest))).Length + length > MaxFileBytes)
        {
            number = highest + 1;
        }

        if (number > MaxSequence)
        {
            return;
        }

        var name = LogFileNames.Format(date, number);
        var fullPath = Path.Combine(_folder, name);
        var isNew = File.Exists(fullPath) == false;

        _stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _streamLength = isNew ? 0 : new FileInfo(fullPath).Length;
        _streamDate = date;

        if (isNew)
        {
            Prune(name);
        }
    }

    /// <summary>
    /// Deletes all log files but the newest ones, never the open one; a failing delete is ignored.
    /// </summary>
    /// <param name="openName">The name of the open file</param>
    private void Prune(string openName)
    {
        var names = Directory.EnumerateFiles(_folder).Select(path => Path.GetFileName(path)).ToList();

        foreach (var name in LogFileNames.SelectForDeletion(names, MaxFiles))
        {
            if (string.Equals(name, openName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(Path.Combine(_folder, name));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The file stays; the next new file tries again.
            }
        }
    }

    /// <summary>
    /// Closes the open file.
    /// </summary>
    private void CloseStream()
    {
        var stream = _stream;

        _stream = null;

        try
        {
            stream?.Dispose();
        }
        catch (IOException)
        {
            // The file is closed as far as it can be.
        }
    }

    #endregion // Methods

    #region ILoggerProvider

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new LogFileLogger(this);
    }

    #endregion // ILoggerProvider

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        _queue.Writer.TryComplete();
        _writer.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        _stop.Dispose();
    }

    #endregion // IDisposable
}