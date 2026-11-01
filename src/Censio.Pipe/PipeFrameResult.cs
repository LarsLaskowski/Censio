namespace Censio.Pipe;

/// <summary>
/// The outcome of reading one frame from the pipe stream.
/// </summary>
public sealed class PipeFrameResult
{
    #region Fields

    /// <summary>
    /// The shared end-of-stream result.
    /// </summary>
    private static readonly PipeFrameResult _endResult = new(PipeFrameResultKind.EndOfStream, ReadOnlyMemory<byte>.Empty, null);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeFrameResult" /> class.
    /// </summary>
    /// <param name="kind">The outcome</param>
    /// <param name="frame">The frame bytes</param>
    /// <param name="error">The error</param>
    private PipeFrameResult(PipeFrameResultKind kind, ReadOnlyMemory<byte> frame, PipeError? error)
    {
        Kind = kind;
        Frame = frame;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the end-of-stream result.
    /// </summary>
    public static PipeFrameResult End => _endResult;

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public PipeFrameResultKind Kind { get; }

    /// <summary>
    /// Gets the frame bytes (type byte and payload) of a <see cref="PipeFrameResultKind.Frame" /> result.
    /// </summary>
    public ReadOnlyMemory<byte> Frame { get; }

    /// <summary>
    /// Gets the error of a <see cref="PipeFrameResultKind.Error" /> result.
    /// </summary>
    public PipeError? Error { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a result that holds a frame.
    /// </summary>
    /// <param name="frame">The frame bytes (type byte and payload)</param>
    /// <returns>The result</returns>
    public static PipeFrameResult FromFrame(ReadOnlyMemory<byte> frame)
    {
        return new PipeFrameResult(PipeFrameResultKind.Frame, frame, null);
    }

    /// <summary>
    /// Creates a result that holds an error.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static PipeFrameResult FromError(PipeError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new PipeFrameResult(PipeFrameResultKind.Error, ReadOnlyMemory<byte>.Empty, error);
    }

    #endregion // Methods
}