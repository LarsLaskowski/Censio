using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The state of a profile held in memory with its place in the order of the held profiles.
/// </summary>
internal sealed class HeldProfileState
{
    #region Properties

    /// <summary>
    /// Gets the state of the profile.
    /// </summary>
    public required StoredProfileState State { get; init; }

    /// <summary>
    /// Gets the receive time of the latest tick, in UTC ticks: the first part of the order key.
    /// </summary>
    public required long ReceiveTicks { get; init; }

    /// <summary>
    /// Gets the storing sequence: the second part of the order key, higher for what was stored later.
    /// </summary>
    public required long Sequence { get; init; }

    #endregion // Properties
}