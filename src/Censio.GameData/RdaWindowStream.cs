namespace Censio.GameData;

/// <summary>
/// A forward-only, read-only view of a fixed range of another seekable stream, so that a decompressor cannot read
/// past the stored range it was given.
/// </summary>
internal sealed class RdaWindowStream : Stream
{
    #region Fields

    /// <summary>
    /// The stream the window lies in.
    /// </summary>
    private readonly Stream _inner;

    /// <summary>
    /// The position of the first byte of the window.
    /// </summary>
    private readonly long _start;

    /// <summary>
    /// The length of the window in bytes.
    /// </summary>
    private readonly long _length;

    /// <summary>
    /// The number of bytes read from the window so far.
    /// </summary>
    private long _consumed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaWindowStream" /> class.
    /// </summary>
    /// <param name="inner">The stream the window lies in</param>
    /// <param name="start">The position of the first byte of the window</param>
    /// <param name="length">The length of the window in bytes</param>
    public RdaWindowStream(Stream inner, long start, long length)
    {
        _inner = inner;
        _start = start;
        _length = length;
    }

    #endregion // Constructors

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
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var wanted = (int)Math.Min(buffer.Length, _length - _consumed);

        if (wanted <= 0)
        {
            return 0;
        }

        _inner.Position = _start + _consumed;

        var read = _inner.Read(buffer[..wanted]);

        _consumed += read;

        return read;
    }

    /// <inheritdoc />
    public override void Flush()
    {
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