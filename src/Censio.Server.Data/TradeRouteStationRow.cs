namespace Censio.Server.Data;

/// <summary>
/// One station of the <c>TradeRouteStations</c> table.
/// </summary>
internal sealed class TradeRouteStationRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the row number of the route.
    /// </summary>
    public long TradeRouteId { get; set; }

    /// <summary>
    /// Gets or sets the position in the route.
    /// </summary>
    public int Position { get; set; }

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

    #endregion // Properties
}