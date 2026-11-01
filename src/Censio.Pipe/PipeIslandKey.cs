namespace Censio.Pipe;

/// <summary>
/// Identifies an island frame within a tick by session GUID, island ID and area index.
/// </summary>
public readonly record struct PipeIslandKey
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeIslandKey" /> struct.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="islandId">The island ID</param>
    /// <param name="areaIndex">The area index</param>
    public PipeIslandKey(int sessionGuid, byte islandId, byte areaIndex)
    {
        SessionGuid = sessionGuid;
        IslandId = islandId;
        AreaIndex = areaIndex;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the session GUID.
    /// </summary>
    public int SessionGuid { get; }

    /// <summary>
    /// Gets the island ID.
    /// </summary>
    public byte IslandId { get; }

    /// <summary>
    /// Gets the area index.
    /// </summary>
    public byte AreaIndex { get; }

    #endregion // Properties
}