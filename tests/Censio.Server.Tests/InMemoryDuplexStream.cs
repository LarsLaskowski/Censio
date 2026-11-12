namespace Censio.Server.Tests;

/// <summary>
/// A duplex stream over two pipes that counts the bytes read.
/// </summary>
public sealed class InMemoryDuplexStream : Stream
{
    #region Properties

    /// <summary>
    /// Gets the number of bytes read from this end.
    /// </summary>
    public long BytesRead => Length;

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S1172
    /// <summary>
    /// Creates the two connected ends.
    /// </summary>
    /// <returns>The server end and the client end</returns>
    public static (InMemoryDuplexStream Server, InMemoryDuplexStream Client) CreatePair()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S1172

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => 0;

    // Skeleton: removed with the implementation
#pragma warning disable RH2003
    /// <inheritdoc />
    public override long Position
    {
        get => 0;
        set => throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Stream
}