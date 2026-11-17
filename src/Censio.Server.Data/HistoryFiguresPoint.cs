namespace Censio.Server.Data;

/// <summary>
/// A point of the trend of the figures of one island.
/// </summary>
public sealed class HistoryFiguresPoint
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
    /// Gets the number of buildings, or <see langword="null"/> when the point has none.
    /// </summary>
    public required long? Buildings { get; init; }

    /// <summary>
    /// Gets the number of produced goods, or <see langword="null"/> when the point has none.
    /// </summary>
    public required int? ProducedGoods { get; init; }

    /// <summary>
    /// Gets the number of goods in deficit, or <see langword="null"/> when the point has none.
    /// </summary>
    public required int? GoodsInDeficit { get; init; }

    #endregion // Properties
}