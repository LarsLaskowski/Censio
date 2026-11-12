using System.Buffers;
using System.IO.Pipelines;

namespace Censio.Server.Tests;

/// <summary>
/// One end of an in-memory connection of two pipes, with the back pressure of a pipe, that counts the bytes read from it.
/// Disposing an end ends the other end's reads (end of stream) and fails its writes, as a closed socket does. It models
/// a socket stream for <see cref="System.Net.WebSockets.WebSocket.CreateFromStream(Stream, System.Net.WebSockets.WebSocketCreationOptions)" />,
/// which uses only the asynchronous members.
/// </summary>
public sealed class InMemoryDuplexStream : Stream
{
    #region Fields

    /// <summary>
    /// The pipe this end reads from.
    /// </summary>
    private readonly PipeReader _input;

    /// <summary>
    /// The pipe this end writes to.
    /// </summary>
    private readonly PipeWriter _output;

    /// <summary>
    /// The lock of the read and dispose state.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The number of bytes read from this end.
    /// </summary>
    private long _bytesRead;

    /// <summary>
    /// Whether a read is in progress.
    /// </summary>
    private bool _isReading;

    /// <summary>
    /// Whether this end is disposed.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// Whether the reader of the input pipe is completed.
    /// </summary>
    private bool _isInputCompleted;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDuplexStream" /> class.
    /// </summary>
    /// <param name="input">The pipe this end reads from</param>
    /// <param name="output">The pipe this end writes to</param>
    public InMemoryDuplexStream(PipeReader input, PipeWriter output)
    {
        _input = input;
        _output = output;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of bytes read from this end.
    /// </summary>
    public long BytesRead => Interlocked.Read(ref _bytesRead);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the two connected ends.
    /// </summary>
    /// <returns>The server end and the client end</returns>
    public static (InMemoryDuplexStream Server, InMemoryDuplexStream Client) CreatePair()
    {
        var toServer = new System.IO.Pipelines.Pipe();
        var toClient = new System.IO.Pipelines.Pipe();

        return (new InMemoryDuplexStream(toServer.Reader, toClient.Writer), new InMemoryDuplexStream(toClient.Reader, toServer.Writer));
    }

    /// <summary>
    /// Completes the reader of the input pipe once, which fails the other end's later writes.
    /// </summary>
    private void CompleteInput()
    {
        if (_isInputCompleted == false)
        {
            _isInputCompleted = true;
            _input.Complete();
        }
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => _isDisposed == false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => _isDisposed == false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return 0;
            }

            _isReading = true;
        }

        try
        {
            var result = await _input.ReadAsync(cancellationToken).ConfigureAwait(false);
            var data = result.Buffer;
            var count = result.IsCanceled ? 0 : (int)Math.Min(data.Length, buffer.Length);

            data.Slice(0, count).CopyTo(buffer.Span);
            _input.AdvanceTo(data.GetPosition(count));
            Interlocked.Add(ref _bytesRead, count);

            return count;
        }
        finally
        {
            lock (_lock)
            {
                _isReading = false;

                if (_isDisposed)
                {
                    CompleteInput();
                }
            }
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await _output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (result.IsCompleted)
        {
            throw new IOException("The other end of the connection is closed.");
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;

                if (_isReading)
                {
                    _input.CancelPendingRead();
                }
                else
                {
                    CompleteInput();
                }
            }

            _output.Complete();
        }

        base.Dispose(disposing);
    }

    #endregion // Stream
}