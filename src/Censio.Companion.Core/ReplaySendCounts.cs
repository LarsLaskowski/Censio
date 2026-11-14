namespace Censio.Companion.Core;

/// <summary>
/// The counts of what a replay sent to a server.
/// </summary>
public readonly record struct ReplaySendCounts
{
    #region Properties

    /// <summary>
    /// Gets the number of ticks the server accepted.
    /// </summary>
    public required int Ticks { get; init; }

    /// <summary>
    /// Gets the number of messages the server or the writer refused.
    /// </summary>
    public required int Refused { get; init; }

    #endregion // Properties
}