namespace Censio.Server.Data;

/// <summary>
/// The time and row number of one point of a trend.
/// </summary>
/// <param name="IsHourly">Whether the point is an hourly point</param>
/// <param name="Ticks">The UTC ticks of its time</param>
/// <param name="Number">The row number of the tick or hourly point</param>
internal readonly record struct TrendTime(bool IsHourly, long Ticks, long Number);