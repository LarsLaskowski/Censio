namespace Censio.Companion.Core;

/// <summary>
/// The status of live sending.
/// </summary>
public readonly record struct LiveSendStatus
{
    #region Properties

    /// <summary>
    /// Gets the state.
    /// </summary>
    public required LiveSendState State { get; init; }

    /// <summary>
    /// Gets the problem of the last attempt, or <see langword="null" /> when there is none.
    /// </summary>
    public ReplaySendProblemKind? Problem { get; init; }

    #endregion // Properties
}