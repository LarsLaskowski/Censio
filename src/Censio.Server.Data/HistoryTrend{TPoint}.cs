namespace Censio.Server.Data;

/// <summary>
/// The points of a trend read and the number of points the range holds.
/// </summary>
/// <typeparam name="TPoint">The type of a point</typeparam>
public sealed class HistoryTrend<TPoint>
{
    #region Properties

    /// <summary>
    /// Gets the points in time order.
    /// </summary>
    public required IReadOnlyList<TPoint> Points { get; init; }

    /// <summary>
    /// Gets the number of points the range holds before the bound.
    /// </summary>
    public required int TotalCount { get; init; }

    #endregion // Properties
}