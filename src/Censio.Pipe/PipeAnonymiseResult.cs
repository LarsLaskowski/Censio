namespace Censio.Pipe;

/// <summary>
/// The result of anonymising a recording.
/// </summary>
public sealed class PipeAnonymiseResult
{
    #region Properties

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public required PipeAnonymiseResultKind Kind { get; init; }

    /// <summary>
    /// Gets the number of frames written.
    /// </summary>
    public long FrameCount { get; init; }

    /// <summary>
    /// Gets the one-based number of the line the result belongs to, if any.
    /// </summary>
    public long? LineNumber { get; init; }

    /// <summary>
    /// Gets the recording problem, if any.
    /// </summary>
    public PipeReplayProblem? Problem { get; init; }

    #endregion // Properties
}