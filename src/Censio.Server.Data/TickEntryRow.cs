namespace Censio.Server.Data;

/// <summary>
/// One product entry of a tick island, in the <c>TickEntries</c> table; a float is stored as its bit pattern.
/// </summary>
internal sealed class TickEntryRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number of the tick island.
    /// </summary>
    public long TickIslandId { get; set; }

    /// <summary>
    /// Gets or sets the position within the island.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the product GUID.
    /// </summary>
    public int ProductGuid { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the Generation value, or <see langword="null"/> for no value.
    /// </summary>
    public int? Generation { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the Consumption value, or <see langword="null"/> for no value.
    /// </summary>
    public int? Consumption { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the Delta value, or <see langword="null"/> for no value.
    /// </summary>
    public int? Delta { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the PerfectGeneration value, or <see langword="null"/> for no value.
    /// </summary>
    public int? PerfectGeneration { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the PerfectConsumption value, or <see langword="null"/> for no value.
    /// </summary>
    public int? PerfectConsumption { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the Income value, or <see langword="null"/> for no value.
    /// </summary>
    public int? Income { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the SummedProductivity value, or <see langword="null"/> for no value.
    /// </summary>
    public int? SummedProductivity { get; set; }

    /// <summary>
    /// Gets or sets the bit pattern of the AverageProductivity value, or <see langword="null"/> for no value.
    /// </summary>
    public int? AverageProductivity { get; set; }

    /// <summary>
    /// Gets or sets the building count.
    /// </summary>
    public int BuildingCount { get; set; }

    /// <summary>
    /// Gets or sets the maintenance.
    /// </summary>
    public int Maintenance { get; set; }

    /// <summary>
    /// Gets or sets the profit.
    /// </summary>
    public int Profit { get; set; }

    #endregion // Properties
}