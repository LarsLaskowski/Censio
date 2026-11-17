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
#pragma warning disable S1172 // Skeleton: removed with the implementation
    public static TrendRange For(ChartRange range, DateTimeOffset latestTick)
    {
        var back = latestTick.UtcTicks - TimeSpan.TicksPerDay;
        var start = back - (back % TimeSpan.TicksPerHour);

        return new TrendRange
               {
                   Start = new DateTimeOffset(start, TimeSpan.Zero),
                   End = new DateTimeOffset(latestTick.UtcTicks + 1, TimeSpan.Zero)
               };
    }

    /// <summary>
    /// Gives the resolution a range is read at.
    /// </summary>
    /// <param name="range">The range of the choice</param>
    /// <returns>The resolution</returns>
    public static HistoryTrendResolution ResolutionOf(ChartRange range)
    {
        return HistoryTrendResolution.Points;
    }
#pragma warning restore S1172

    #endregion // Methods
}