namespace Censio.Pipe;

/// <summary>
/// Reads length-prefixed frames from one pipe stream and never reads past the current frame.
/// </summary>
public sealed class PipeFrameReader
{
    #region Fields

    /// <summary>
    /// The stream to read from.
    /// </summary>
    private readonly Stream _stream;

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
    public ValueTask<PipeFrameResult> ReadFrameAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}