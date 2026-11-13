namespace Censio.Server.Data;

/// <summary>
/// One island of a tick, in the <c>TickIslands</c> table.
/// </summary>
internal sealed class TickIslandRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number of the tick.
    /// </summary>
    public long TickNumber { get; set; }

    /// <summary>
    /// Gets or sets the position within the tick.
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

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public byte SessionId { get; set; }

    /// <summary>
    /// Gets or sets the island name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    #endregion // Properties
}