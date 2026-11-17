namespace Censio.Server;

/// <summary>
/// The period of receive time a trend chart shows.
/// </summary>
public enum ChartRange
{
    /// <summary>
    /// The last hour up to the latest tick.
    /// </summary>
    LastHour,

    /// <summary>
    /// The last 24 hours up to the latest tick.
    /// </summary>
    LastDay,

    /// <summary>
    /// The last 7 days up to the latest tick.
    /// </summary>
    LastWeek,

    /// <summary>
    /// The whole history up to the latest tick.
    /// </summary>
    All
}