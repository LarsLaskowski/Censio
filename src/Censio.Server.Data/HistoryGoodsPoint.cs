namespace Censio.Server.Data;

/// <summary>
/// A point of the trend of several goods of one island.
/// </summary>
public sealed class HistoryGoodsPoint
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
    /// Gets the delta per product GUID, holding only the goods that have a value at the point.
    /// </summary>
    public required IReadOnlyDictionary<int, double> Deltas { get; init; }

    #endregion // Properties
}