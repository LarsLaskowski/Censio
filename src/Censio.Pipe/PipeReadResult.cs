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
    /// <param name="frame">The whole frame the result came from, or <see langword="null" /></param>
    private PipeReadResult(PipeReadResultKind kind, PipeMessage? message, PipeError? error, ReadOnlyMemory<byte>? frame = null)
    {
        Kind = kind;
        Message = message;
        Error = error;
        Frame = frame;
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

    /// <summary>
    /// Gets the whole frame (type byte and payload) the result came from, or <see langword="null" /> when there is none.
    /// </summary>
    public ReadOnlyMemory<byte>? Frame { get; }

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

    /// <summary>
    /// Creates a copy of the result that carries the frame it came from.
    /// </summary>
    /// <param name="frame">The whole frame</param>
    /// <returns>The result with the frame</returns>
    public PipeReadResult WithFrame(ReadOnlyMemory<byte> frame)
    {
        return new PipeReadResult(Kind, Message, Error, frame);
    }

    #endregion // Methods
}