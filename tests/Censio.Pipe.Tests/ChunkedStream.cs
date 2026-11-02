namespace Censio.Pipe.Tests;

/// <summary>
/// A read-only stream over bytes that returns at most a fixed number of bytes per read, and can fail with an
/// <see cref="IOException" /> or cancel a <see cref="CancellationTokenSource" /> at a given offset.
/// </summary>
public sealed class ChunkedStream : Stream
{
    #region Fields

    /// <summary>
    /// The bytes to deliver.
    /// </summary>
    private readonly byte[] _data;

    /// <summary>
    /// The most bytes one read returns.
    /// </summary>
    private readonly int _chunkSize;

    /// <summary>
    /// The offset at which a read throws an <see cref="IOException" />.
    /// </summary>
    private readonly int? _ioErrorOffset;

    /// <summary>
    /// The offset at which a read cancels <see cref="_cancellationSource" />.
    /// </summary>
    private readonly int? _cancelOffset;

    /// <summary>
    /// The source cancelled at <see cref="_cancelOffset" />.
    /// </summary>
    private readonly CancellationTokenSource? _cancellationSource;

    /// <summary>
    /// The value of <see cref="CanRead" /> while the stream is not disposed.
    /// </summary>
    private readonly bool _readable;

    /// <summary>
    /// Whether the failing read throws an <see cref="UnauthorizedAccessException" /> instead of an <see cref="IOException" />.
    /// </summary>
    private readonly bool _accessDenied;

    /// <summary>
    /// Whether the stream is disposed.
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedStream" /> class.
    /// </summary>
    /// <param name="data">The bytes to deliver</param>
    /// <param name="chunkSize">The most bytes one read returns</param>
    /// <param name="ioErrorOffset">The offset at which a read throws an <see cref="IOException" />, if any</param>
    /// <param name="cancelOffset">The offset at which a read cancels <paramref name="cancellationSource" />, if any</param>
    /// <param name="cancellationSource">The source to cancel at <paramref name="cancelOffset" /></param>
    /// <param name="readable">The value of <see cref="CanRead" /></param>
    /// <param name="accessDenied">Whether the failing read throws an <see cref="UnauthorizedAccessException" /> instead</param>
    public ChunkedStream(byte[] data,
                         int chunkSize = int.MaxValue,
                         int? ioErrorOffset = null,
                         int? cancelOffset = null,
                         CancellationTokenSource? cancellationSource = null,
                         bool readable = true,
                         bool accessDenied = false)
    {
        _data = data;
        _chunkSize = chunkSize;
        _ioErrorOffset = ioErrorOffset;
        _cancelOffset = cancelOffset;
        _cancellationSource = cancellationSource;
        _readable = readable;
        _accessDenied = accessDenied;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of bytes delivered so far.
    /// </summary>
    public int Offset { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Delivers the next bytes, honouring the chunk size, the I/O error offset and the cancellation offset.
    /// </summary>
    /// <param name="destination">The buffer to fill</param>
    /// <param name="cancellationToken">The token of the read</param>
    /// <returns>The number of bytes delivered; 0 at the end of the data</returns>
    private int ReadCore(Span<byte> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cancelOffset == Offset)
        {
            _cancellationSource?.Cancel();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_ioErrorOffset == Offset)
        {
            throw _accessDenied ? new UnauthorizedAccessException("Simulated access denied") : new IOException("Simulated I/O error");
        }

        var count = Math.Min(Math.Min(destination.Length, _chunkSize), _data.Length - Offset);

        count = LimitBefore(count, _ioErrorOffset);
        count = LimitBefore(count, _cancelOffset);

        _data.AsSpan(Offset, count).CopyTo(destination);
        Offset += count;

        return count;
    }

    /// <summary>
    /// Shortens a read so that it stops at an offset instead of passing it.
    /// </summary>
    /// <param name="count">The planned number of bytes</param>
    /// <param name="stopOffset">The offset to stop at, if any</param>
    /// <returns>The number of bytes to deliver</returns>
    private int LimitBefore(int count, int? stopOffset)
    {
        if (stopOffset is > 0 && stopOffset > Offset)
        {
            return Math.Min(count, stopOffset.Value - Offset);
        }

        return count;
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => _readable && _disposed == false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

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
    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadCore(buffer.AsSpan(offset, count), CancellationToken.None);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return new ValueTask<int>(ReadCore(buffer.Span, cancellationToken));
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
    protected override void Dispose(bool disposing)
    {
        _disposed = true;

        base.Dispose(disposing);
    }

    #endregion // Stream
}