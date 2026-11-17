namespace Censio.Server;

/// <summary>
/// The time axis a trend chart is drawn on.
/// </summary>
public enum ChartAxis
{
    /// <summary>
    /// The receive time of the points.
    /// </summary>
    RealTime,

    /// <summary>
    /// The game time of the points.
    /// </summary>
    GameTime
}