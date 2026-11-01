namespace Censio.Pipe.Tests;

/// <summary>
/// A read-only <see cref="Stream" /> over bytes that returns at most a fixed number of bytes per read and runs an
/// action at the start of every read, for example to move a fake clock while one frame arrives in chunks. It models no
/// real component.
/// </summary>
public sealed class ChunkedClockStream : Stream
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
    /// The action run at the start of every read.
    /// </summary>
    private readonly Action _onRead;

    /// <summary>
    /// The number of bytes delivered so far.
    /// </summary>
    private int _offset;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedClockStream" /> class.
    /// </summary>
    /// <param name="data">The bytes to deliver</param>
    /// <param name="chunkSize">The most bytes one read returns</param>
    /// <param name="onRead">The action run at the start of every read, before it returns bytes</param>
    public ChunkedClockStream(byte[] data, int chunkSize, Action onRead)
    {
        _data = data;
        _chunkSize = chunkSize;
        _onRead = onRead;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of reads that returned at least one byte.
    /// </summary>
    public int DeliveredReadCount { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Runs the read action and delivers the next bytes.
    /// </summary>
    /// <param name="destination">The buffer to fill</param>
    /// <returns>The number of bytes delivered; 0 at the end of the data</returns>
    private int ReadCore(Span<byte> destination)
    {
        _onRead();

        var count = Math.Min(Math.Min(destination.Length, _chunkSize), _data.Length - _offset);

        _data.AsSpan(_offset, count).CopyTo(destination);
        _offset += count;

        if (count > 0)
        {
            DeliveredReadCount++;
        }

        return count;
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => true;

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
        return ReadCore(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return new ValueTask<int>(ReadCore(buffer.Span));
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

    #endregion // Stream
}