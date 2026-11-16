namespace Censio.Server;

/// <summary>
/// The points of a trend chart and the number of points its range holds.
/// </summary>
public sealed class TrendView
{
    #region Properties

    /// <summary>
    /// Gets the points in time order.
    /// </summary>
    public required IReadOnlyList<TrendPoint> Points { get; init; }

    /// <summary>
    /// Gets the number of points the range holds before the bound.
    /// </summary>
    public required int TotalCount { get; init; }

    #endregion // Properties
}