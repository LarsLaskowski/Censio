namespace Censio.Server.Data;

/// <summary>
/// One summed workforce or building amount of a folded hour's entry, in the <c>HourlyMapPairs</c> table.
/// </summary>
internal sealed class HourlyMapPairRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number of the hourly entry.
    /// </summary>
    public long EntryId { get; set; }

    /// <summary>
    /// Gets or sets the kind, 0 for workforce and 1 for building.
    /// </summary>
    public byte Kind { get; set; }

    /// <summary>
    /// Gets or sets the GUID.
    /// </summary>
    public int Guid { get; set; }

    /// <summary>
    /// Gets or sets the sum of the amounts.
    /// </summary>
    public long AmountSum { get; set; }

    #endregion // Properties
}