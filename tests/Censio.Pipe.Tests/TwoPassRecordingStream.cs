namespace Censio.Pipe.Tests;

/// <summary>
/// A seekable read-only stream that delivers one content until it is set back to its start and another content after
/// that, and can throw an <see cref="IOException" /> at an offset of either pass: a recording file that changes or
/// fails between the two passes of the anonymiser.
/// </summary>
public sealed class TwoPassRecordingStream : Stream
{
    #region Fields

    /// <summary>
    /// The content of the first pass.
    /// </summary>
    private readonly byte[] _firstPass;

    /// <summary>
    /// The content of every later pass.
    /// </summary>
    private readonly byte[] _laterPass;

    /// <summary>
    /// The offset at which a read throws, if any.
    /// </summary>
    private readonly int? _ioErrorOffset;

    /// <summary>
    /// The one-based pass in which a read throws at <see cref="_ioErrorOffset" />.
    /// </summary>
    private readonly int _ioErrorPass;

    /// <summary>
    /// The position in the current content.
    /// </summary>
    private long _position;

    /// <summary>
    /// Whether the current pass has delivered a read.
    /// </summary>
    private bool _readInPass;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoPassRecordingStream" /> class.
    /// </summary>
    /// <param name="firstPass">The content of the first pass</param>
    /// <param name="laterPass">The content after the stream is set back to its start</param>
    /// <param name="ioErrorOffset">The offset at which a read throws an <see cref="IOException" />, if any</param>
    /// <param name="ioErrorPass">The one-based pass in which the read throws</param>
    public TwoPassRecordingStream(byte[] firstPass, byte[] laterPass, int? ioErrorOffset = null, int ioErrorPass = 1)
    {
        _firstPass = firstPass;
        _laterPass = laterPass;
        _ioErrorOffset = ioErrorOffset;
        _ioErrorPass = ioErrorPass;
        Pass = 1;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the one-based number of the current pass; it grows each time the stream is set back to its start after a read.
    /// </summary>
    public int Pass { get; private set; }

    /// <summary>
    /// Gets the content of the current pass.
    /// </summary>
    private byte[] Content => Pass == 1 ? _firstPass : _laterPass;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Moves to a position; a move to the start after a read begins the next pass.
    /// </summary>
    /// <param name="position">The new position</param>
    private void MoveTo(long position)
    {
        if (position == 0 && _readInPass)
        {
            Pass++;
            _readInPass = false;
        }

        _position = position;
    }

    /// <summary>
    /// Delivers the next bytes of the current pass, honouring the I/O error offset.
    /// </summary>
    /// <param name="destination">The buffer to fill</param>
    /// <returns>The number of bytes delivered; 0 at the end of the content</returns>
    private int ReadCore(Span<byte> destination)
    {
        _readInPass = true;

        if (Pass == _ioErrorPass && _ioErrorOffset is { } errorOffset)
        {
            if (_position >= errorOffset)
            {
                throw new IOException("Simulated I/O error");
            }

            destination = destination[..(int)Math.Min(destination.Length, errorOffset - _position)];
        }

        var count = (int)Math.Max(0, Math.Min(destination.Length, Content.Length - _position));

        Content.AsSpan((int)_position, count).CopyTo(destination);
        _position += count;

        return count;
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
    public override long Length => Content.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => MoveTo(value);
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
    public override int Read(Span<byte> buffer)
    {
        return ReadCore(buffer);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(ReadCore(buffer.Span));
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
                     {
                         SeekOrigin.Begin => offset,
                         SeekOrigin.Current => _position + offset,
                         _ => Content.Length + offset
                     };

        MoveTo(target);

        return _position;
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