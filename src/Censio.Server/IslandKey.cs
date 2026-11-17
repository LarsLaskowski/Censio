namespace Censio.Server;

/// <summary>
/// The key of an island: its session, island and area.
/// </summary>
/// <param name="SessionGuid">The session GUID</param>
/// <param name="IslandId">The island identity</param>
/// <param name="AreaIndex">The area index</param>
public readonly record struct IslandKey(int SessionGuid, byte IslandId, byte AreaIndex);