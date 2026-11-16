namespace Censio.Server.Data;

/// <summary>
/// One island of a folded hour, in the <c>HourlyIslands</c> table.
/// </summary>
internal sealed class HourlyIslandRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number of the hourly point.
    /// </summary>
    public long PointNumber { get; set; }

    /// <summary>
    /// Gets or sets the session GUID.
    /// </summary>
    public int SessionGuid { get; set; }

    /// <summary>
    /// Gets or sets the island ID.
    /// </summary>
    public byte IslandId { get; set; }

    /// <summary>
    /// Gets or sets the area index.
    /// </summary>
    public byte AreaIndex { get; set; }

    /// <summary>
    /// Gets or sets the island name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the receive time of the tick the name was taken from.
    /// </summary>
    public long NameReceiveTicks { get; set; }

    /// <summary>
    /// Gets or sets the row number of the tick the name was taken from.
    /// </summary>
    public long NameTickNumber { get; set; }

    #endregion // Properties
}