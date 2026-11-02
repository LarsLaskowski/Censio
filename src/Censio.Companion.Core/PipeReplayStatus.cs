using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// The status of a replay of a recording.
/// </summary>
public readonly record struct PipeReplayStatus
{
    #region Properties

    /// <summary>
    /// Gets the state of the replayed connection.
    /// </summary>
    public required PipeConnectionState State { get; init; }

    /// <summary>
    /// Gets a value indicating whether the replay has finished.
    /// </summary>
    public bool IsFinished { get; init; }

    /// <summary>
    /// Gets the problem that ended the replay, if any.
    /// </summary>
    public PipeReplayProblem? Problem { get; init; }

    #endregion // Properties
}