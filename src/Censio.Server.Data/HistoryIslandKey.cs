namespace Censio.Server.Data;

/// <summary>
/// The key of an island in the history.
/// </summary>
/// <param name="SessionGuid">The session GUID of the island</param>
/// <param name="IslandId">The island ID</param>
/// <param name="AreaIndex">The area index of the island</param>
public readonly record struct HistoryIslandKey(int SessionGuid, byte IslandId, byte AreaIndex);