namespace Censio.Server.Data;

/// <summary>
/// The resolution of the points of a trend read.
/// </summary>
public enum HistoryTrendResolution
{
    /// <summary>
    /// One point per tick or folded hour as stored.
    /// </summary>
    Points,

    /// <summary>
    /// One mean point per UTC hour.
    /// </summary>
    Hours
}