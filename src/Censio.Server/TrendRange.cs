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
        var latest = latestTick.UtcTicks;
        var start = 0L;

        if (range != ChartRange.All)
        {
            var back = Math.Max(0L,
                                latest - (range switch
                                          {
                                              ChartRange.LastHour => TimeSpan.TicksPerHour,
                                              ChartRange.LastDay => TimeSpan.TicksPerDay,
                                              _ => 7 * TimeSpan.TicksPerDay
                                          }));

            start = back - (back % TimeSpan.TicksPerHour);
        }

        return new TrendRange
               {
                   Start = new DateTimeOffset(start, TimeSpan.Zero),
                   End = new DateTimeOffset(Math.Min(latest + 1, DateTimeOffset.MaxValue.UtcTicks), TimeSpan.Zero)
               };
    }

    /// <summary>
    /// Gives the range of the sparklines up to the latest tick.
    /// </summary>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <returns>The range</returns>
    public static TrendRange ForSparklines(DateTimeOffset latestTick)
    {
#pragma warning disable RH2003 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
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

    #endregion // Methods
}