namespace Censio.Server;

/// <summary>
/// The choice of range and time axis for the trend charts.
/// </summary>
public sealed record ChartChoice
{
    #region Properties

    /// <summary>
    /// Gets the default choice: the last day on the real-time axis.
    /// </summary>
    public static ChartChoice Default { get; } = new()
                                                 {
                                                     Range = ChartRange.LastDay,
                                                     Axis = ChartAxis.RealTime
                                                 };

    /// <summary>
    /// Gets the range.
    /// </summary>
    public required ChartRange Range { get; init; }

    /// <summary>
    /// Gets the time axis.
    /// </summary>
    public required ChartAxis Axis { get; init; }

    #endregion // Properties
}