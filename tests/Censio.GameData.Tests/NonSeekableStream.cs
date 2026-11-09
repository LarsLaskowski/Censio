namespace Censio.GameData.Tests;

/// <summary>
/// A read-only stream over another stream that cannot seek and does not know its length, so a test can show that a
/// reader works forward-only.
/// </summary>
public sealed class NonSeekableStream : Stream
{
    #region Fields

    /// <summary>
    /// The stream that holds the bytes.
    /// </summary>
    private readonly Stream _inner;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NonSeekableStream" /> class.
    /// </summary>
    /// <param name="inner">The stream that holds the bytes; it is disposed with this stream</param>
    public NonSeekableStream(Stream inner)
    {
        _inner = inner;
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
    public override long Length => throw new NotSupportedException("The stream does not know its length");

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException("The stream does not know its position");
        set => throw new NotSupportedException("The stream cannot seek");
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("The stream cannot seek");
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException("The stream is read-only");
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("The stream is read-only");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion // Stream
}