namespace Censio.Server.Data;

/// <summary>
/// A point of the trend of the building counts of several islands.
/// </summary>
public sealed class HistoryIslandsPoint
{
    #region Properties

    /// <summary>
    /// Gets the time of the point.
    /// </summary>
    public required DateTimeOffset Time { get; init; }

    /// <summary>
    /// Gets a value indicating whether the point is an hourly mean.
    /// </summary>
    public required bool IsHourly { get; init; }

    /// <summary>
    /// Gets the number of buildings per island, holding only the islands that have entries at the point.
    /// </summary>
    public required IReadOnlyDictionary<HistoryIslandKey, long> Buildings { get; init; }

    #endregion // Properties
}