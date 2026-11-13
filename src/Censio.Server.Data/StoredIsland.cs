namespace Censio.Server.Data;

/// <summary>
/// A stored island of a profile, with its latest name.
/// </summary>
public sealed class StoredIsland
{
    #region Properties

    /// <summary>
    /// Gets the session GUID.
    /// </summary>
    public required int SessionGuid { get; init; }

    /// <summary>
    /// Gets the island ID.
    /// </summary>
    public required byte IslandId { get; init; }

    /// <summary>
    /// Gets the area index.
    /// </summary>
    public required byte AreaIndex { get; init; }

    /// <summary>
    /// Gets the latest island name.
    /// </summary>
    public required string Name { get; init; }

    #endregion // Properties
}