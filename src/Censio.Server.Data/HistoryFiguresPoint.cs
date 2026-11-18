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

    /// <summary>
    /// Gets the game time of the point in milliseconds, or <see langword="null"/> when the point has none.
    /// </summary>
    public long? GameTime { get; init; }

    /// <summary>
    /// Gets the first game time in milliseconds of the ticks the point stands for, or <see langword="null"/> when it has none.
    /// </summary>
    public long? FirstGameTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether no island of the point holds an entry.
    /// </summary>
    public bool HasNoEntries { get; init; }

    #endregion // Properties
}