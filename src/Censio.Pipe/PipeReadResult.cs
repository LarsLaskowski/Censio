namespace Censio.Pipe;

/// <summary>
/// The outcome of reading one message from the pipe stream.
/// </summary>
public sealed class PipeReadResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReadResult" /> class.
    /// </summary>
    /// <param name="kind">The outcome</param>
    /// <param name="message">The message</param>
    /// <param name="error">The error</param>
    private PipeReadResult(PipeReadResultKind kind, PipeMessage? message, PipeError? error)
    {
        Kind = kind;
        Message = message;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the end-of-stream result.
    /// </summary>
    public static PipeReadResult End { get; } = new(PipeReadResultKind.EndOfStream, null, null);

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public PipeReadResultKind Kind { get; }

    /// <summary>
    /// Gets the message of a <see cref="PipeReadResultKind.Message" /> result.
    /// </summary>
    public PipeMessage? Message { get; }

    /// <summary>
    /// Gets the error of a <see cref="PipeReadResultKind.Error" /> result.
    /// </summary>
    public PipeError? Error { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a result that holds a message.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The result</returns>
    public static PipeReadResult FromMessage(PipeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new PipeReadResult(PipeReadResultKind.Message, message, null);
    }

    /// <summary>
    /// Creates a result that holds an error.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static PipeReadResult FromError(PipeError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new PipeReadResult(PipeReadResultKind.Error, null, error);
    }

    #endregion // Methods
}