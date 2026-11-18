using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The time range a trend chart shows.
/// </summary>
public sealed class TrendRange
{
    #region Constants

    /// <summary>
    /// The most points a chart read returns.
    /// </summary>
    public const int MaxPoints = 2880;

    /// <summary>
    /// The most points a sparkline read returns.
    /// </summary>
    public const int SparklineMaxPoints = 240;

    /// <summary>
    /// The hours a sparkline spans.
    /// </summary>
    public const int SparklineHours = 4;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the start of the range, inclusive.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Gets the end of the range, exclusive.
    /// </summary>
    public required DateTimeOffset End { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gives the range of a choice up to the latest tick.
    /// </summary>
    /// <param name="range">The range of the choice</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <returns>The range</returns>
    public static TrendRange For(ChartRange range, DateTimeOffset latestTick)
    {
        return range == ChartRange.All
                   ? Create(latestTick.UtcTicks, null)
                   : Create(latestTick.UtcTicks,
                            range switch
                            {
                                ChartRange.LastHour => TimeSpan.TicksPerHour,
                                ChartRange.LastDay => TimeSpan.TicksPerDay,
                                _ => 7 * TimeSpan.TicksPerDay
                            });
    }

    /// <summary>
    /// Gives the range of the sparklines up to the latest tick.
    /// </summary>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <returns>The range</returns>
    public static TrendRange ForSparklines(DateTimeOffset latestTick)
    {
        return Create(latestTick.UtcTicks, SparklineHours * TimeSpan.TicksPerHour);
    }

    /// <summary>
    /// Gives the resolution a range is read at.
    /// </summary>
    /// <param name="range">The range of the choice</param>
    /// <returns>The resolution</returns>
    public static HistoryTrendResolution ResolutionOf(ChartRange range)
    {
        return range is ChartRange.LastWeek or ChartRange.All
                   ? HistoryTrendResolution.Hours
                   : HistoryTrendResolution.Points;
    }

    /// <summary>
    /// Creates the range that ends right after a tick and starts at the hour that holds the time a span before it.
    /// </summary>
    /// <param name="latest">The receive time of the latest tick in ticks</param>
    /// <param name="span">The ticks to go back, or <see langword="null"/> for the beginning of the time scale</param>
    /// <returns>The range</returns>
    private static TrendRange Create(long latest, long? span)
    {
        var start = 0L;

        if (span is { } back)
        {
            var from = Math.Max(0L, latest - back);

            start = from - (from % TimeSpan.TicksPerHour);
        }

        return new TrendRange
               {
                   Start = new DateTimeOffset(start, TimeSpan.Zero),
                   End = new DateTimeOffset(Math.Min(latest + 1, DateTimeOffset.MaxValue.UtcTicks), TimeSpan.Zero)
               };
    }

    #endregion // Methods
}