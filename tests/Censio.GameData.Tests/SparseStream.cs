namespace Censio.GameData.Tests;

/// <summary>
/// A seekable, read-only stream of a given length that reads as zeros outside the regions written into it, so a
/// test can place data above 4 GiB without holding that much memory, and that can fail with an
/// <see cref="IOException" /> when a read covers a given offset.
/// </summary>
public sealed class SparseStream : Stream
{
    #region Fields

    /// <summary>
    /// The length of the stream.
    /// </summary>
    private readonly long _length;

    /// <summary>
    /// The regions written into the stream.
    /// </summary>
    private readonly List<KeyValuePair<long, byte[]>> _regions = [];

    /// <summary>
    /// The offset a read must not cover, or <c>-1</c> for none.
    /// </summary>
    private long _failOffset = -1;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SparseStream" /> class.
    /// </summary>
    /// <param name="length">The length of the stream in bytes</param>
    public SparseStream(long length)
    {
        _length = length;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Places bytes at an offset.
    /// </summary>
    /// <param name="offset">The offset of the first byte</param>
    /// <param name="data">The bytes</param>
    /// <returns>This stream</returns>
    public SparseStream WithRegion(long offset, byte[] data)
    {
        _regions.Add(new KeyValuePair<long, byte[]>(offset, data));

        return this;
    }

    /// <summary>
    /// Makes every read that covers an offset throw an <see cref="IOException" />.
    /// </summary>
    /// <param name="offset">The offset</param>
    /// <returns>This stream</returns>
    public SparseStream ThrowingAt(long offset)
    {
        _failOffset = offset;

        return this;
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _length;

    /// <inheritdoc />
    public override long Position { get; set; }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (Position < 0 || Position >= _length)
        {
            return 0;
        }

        var count = (int)Math.Min(buffer.Length, _length - Position);

        if (_failOffset >= Position && _failOffset < Position + count)
        {
            throw new IOException("Simulated read failure");
        }

        buffer[..count].Clear();

        foreach (var region in _regions)
        {
            var start = Math.Max(region.Key, Position);
            var end = Math.Min(region.Key + region.Value.Length, Position + count);

            if (start < end)
            {
                region.Value.AsSpan((int)(start - region.Key), (int)(end - start)).CopyTo(buffer[(int)(start - Position)..]);
            }
        }

        Position += count;

        return count;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
                   {
                       SeekOrigin.Begin => offset,
                       SeekOrigin.Current => Position + offset,
                       _ => _length + offset
                   };

        return Position;
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