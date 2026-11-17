namespace Censio.Server.Data;

/// <summary>
/// The slice of one UTC hour that a part of an hourly trend read covers.
/// </summary>
/// <param name="HourTicks">The UTC ticks of the start of the hour.</param>
/// <param name="Ticks">The number of ticks of the hour the part reads.</param>
/// <param name="WithHourly">Whether the part reads the hour's hourly point.</param>
internal readonly record struct TrendHourSlice(long HourTicks, int Ticks, bool WithHourly);