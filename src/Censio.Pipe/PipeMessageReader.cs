namespace Censio.Pipe;

/// <summary>
/// Reads typed messages from one pipe stream and applies the protocol version gate.
/// </summary>
public sealed class PipeMessageReader
{
    #region Fields

    /// <summary>
    /// The stream to read from.
    /// </summary>
    private readonly Stream _stream;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeMessageReader" /> class.
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    public PipeMessageReader(Stream stream)
    {
        _stream = stream;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the next message.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The message, the error of the frame, or the end of the stream</returns>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public ValueTask<PipeReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        _ = _stream;

        throw new NotImplementedException();
    }

    #endregion // Methods
}