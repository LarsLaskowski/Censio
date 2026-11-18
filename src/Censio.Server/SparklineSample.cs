namespace Censio.Server;

/// <summary>
/// One value of a sparkline at a time.
/// </summary>
/// <param name="Time">The time of the value.</param>
/// <param name="IsHourly">Whether the value is an hourly mean.</param>
/// <param name="Value">The value, or <see langword="null"/> when the point has none.</param>
public readonly record struct SparklineSample(DateTimeOffset Time, bool IsHourly, double? Value);