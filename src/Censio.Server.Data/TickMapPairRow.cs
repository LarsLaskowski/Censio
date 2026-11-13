namespace Censio.Server.Data;

/// <summary>
/// One workforce or building amount of an entry, in the <c>TickMapPairs</c> table.
/// </summary>
internal sealed class TickMapPairRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number of the entry.
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
    /// Gets or sets the amount.
    /// </summary>
    public int Amount { get; set; }

    #endregion // Properties
}