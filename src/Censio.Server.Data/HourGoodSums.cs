namespace Censio.Server.Data;

/// <summary>
/// The entry count, building count sum and delta sum of one good of an island over the ticks of one hour.
/// </summary>
internal sealed class HourGoodSums
{
    #region Properties

    /// <summary>
    /// Gets or sets the number of entries added.
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the building counts.
    /// </summary>
    public long BuildingSum { get; set; }

    /// <summary>
    /// Gets or sets the sum of the deltas.
    /// </summary>
    public double DeltaSum { get; set; }

    /// <summary>
    /// Gets or sets the number of entries that carried a delta.
    /// </summary>
    public int DeltaCount { get; set; }

    #endregion // Properties
}