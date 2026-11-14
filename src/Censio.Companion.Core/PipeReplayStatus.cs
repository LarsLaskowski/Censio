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

    /// <summary>
    /// Gets a value indicating whether the replay is connecting to the server.
    /// </summary>
    public bool IsConnecting { get; init; }

    /// <summary>
    /// Gets the problem of sending the replay to the server that ended the replay, if any.
    /// </summary>
    public ReplaySendProblemKind? SendProblem { get; init; }

    /// <summary>
    /// Gets the counts of what the replay sent to the server, if it was sent.
    /// </summary>
    public ReplaySendCounts? Sent { get; init; }

    #endregion // Properties
}