using System.Buffers.Binary;

namespace Censio.Pipe;

/// <summary>
/// Reads length-prefixed frames from one pipe stream and never reads past the current frame.
/// </summary>
public sealed class PipeFrameReader
{
    #region Constants

    /// <summary>
    /// The size of the length prefix in bytes.
    /// </summary>
    private const int PrefixLength = 4;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The stream to read from.
    /// </summary>
    private readonly Stream _stream;

    /// <summary>
    /// The length prefix buffer, reused for every frame.
    /// </summary>
    private readonly byte[] _prefix = new byte[PrefixLength];

    /// <summary>
    /// Indicates whether the reader has ended and returns only the end of the stream.
    /// </summary>
    private bool _ended;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeFrameReader" /> class.
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream" /> cannot be read.</exception>
    public PipeFrameReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanRead == false)
        {
            throw new ArgumentException("The stream cannot be read.", nameof(stream));
        }

        _stream = stream;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the next frame.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The frame, the error that ended the stream, or the end of the stream</returns>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public async ValueTask<PipeFrameResult> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (_ended)
        {
            return PipeFrameResult.End;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ended = true;

            throw;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return Fail(new PipeError
                        {
                            Kind = PipeErrorKind.ReadFailed
                        });
        }
    }

    /// <summary>
    /// Reads the prefix and the frame body.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The frame, the error, or the end of the stream</returns>
    private async ValueTask<PipeFrameResult> ReadCoreAsync(CancellationToken cancellationToken)
    {
        var prefixRead = await _stream.ReadAtLeastAsync(_prefix, PrefixLength, false, cancellationToken).ConfigureAwait(false);

        if (prefixRead == 0)
        {
            _ended = true;

            return PipeFrameResult.End;
        }

        if (prefixRead < PrefixLength)
        {
            return Fail(new PipeError
                        {
                            Kind = PipeErrorKind.TruncatedFrame
                        });
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(_prefix);

        if (length < 0 || length > PipeProtocol.MaxFrameLength)
        {
            return Fail(new PipeError
                        {
                            Kind = PipeErrorKind.OversizedFrame,
                            Length = length
                        });
        }

        var frame = new byte[length];
        var bodyRead = await _stream.ReadAtLeastAsync(frame, length, false, cancellationToken).ConfigureAwait(false);

        if (bodyRead < length)
        {
            return Fail(new PipeError
                        {
                            Kind = PipeErrorKind.TruncatedFrame,
                            Length = length
                        });
        }

        return PipeFrameResult.FromFrame(frame);
    }

    /// <summary>
    /// Ends the reader with an error.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The error result</returns>
    private PipeFrameResult Fail(PipeError error)
    {
        _ended = true;

        return PipeFrameResult.FromError(error);
    }

    #endregion // Methods
}