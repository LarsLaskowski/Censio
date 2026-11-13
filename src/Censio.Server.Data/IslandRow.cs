namespace Censio.Server.Data;

/// <summary>
/// One island of the <c>Islands</c> table, with its latest name.
/// </summary>
internal sealed class IslandRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the profile the island belongs to.
    /// </summary>
    public Guid ProfileId { get; set; }

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
    /// Gets or sets the latest name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the receive time of the tick the name came from, as UTC ticks.
    /// </summary>
    public long NameReceiveTicks { get; set; }

    /// <summary>
    /// Gets or sets the number of the tick the name came from.
    /// </summary>
    public long NameTickNumber { get; set; }

    #endregion // Properties
}