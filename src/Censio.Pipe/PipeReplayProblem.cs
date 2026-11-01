namespace Censio.Pipe;

/// <summary>
/// A problem found while replaying a pipe recording.
/// </summary>
public sealed class PipeReplayProblem
{
    #region Properties

    /// <summary>
    /// Gets the kind of the problem.
    /// </summary>
    public required PipeReplayProblemKind Kind { get; init; }

    /// <summary>
    /// Gets the one-based number of the line the problem was found in, if it belongs to a line.
    /// </summary>
    public long? LineNumber { get; init; }

    #endregion // Properties
}