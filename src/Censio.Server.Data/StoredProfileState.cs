using Censio.Contracts;

namespace Censio.Server.Data;

/// <summary>
/// The stored state of a profile: its row, its latest tick and its islands.
/// </summary>
public sealed class StoredProfileState
{
    #region Properties

    /// <summary>
    /// Gets the profile.
    /// </summary>
    public required StoredProfile Profile { get; init; }

    /// <summary>
    /// Gets the latest tick, or <see langword="null"/> when none is stored.
    /// </summary>
    public required ContractTick? LatestTick { get; init; }

    /// <summary>
    /// Gets the islands of the profile.
    /// </summary>
    public required IReadOnlyList<StoredIsland> Islands { get; init; }

    #endregion // Properties
}