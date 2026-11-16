using MudBlazor;

namespace Censio.Server.Components;

/// <summary>
/// A trend chart that is drawn: the read, the offset its times were shown at and its series.
/// </summary>
internal sealed class DrawnTrend
{
    #region Properties

    /// <summary>
    /// Gets the read.
    /// </summary>
    public required TrendRead Read { get; init; }

    /// <summary>
    /// Gets the number of the read.
    /// </summary>
    public required int ReadNumber { get; init; }

    /// <summary>
    /// Gets the UTC offset in minutes the times are shown at, or <see langword="null"/> for UTC.
    /// </summary>
    public required int? OffsetMinutes { get; init; }

    /// <summary>
    /// Gets the points by the clock time they are drawn at.
    /// </summary>
    public required Dictionary<DateTime, TrendPoint> PointsByTime { get; init; }

    /// <summary>
    /// Gets the earliest clock time drawn.
    /// </summary>
    public required DateTime First { get; init; }

    /// <summary>
    /// Gets the latest clock time drawn.
    /// </summary>
    public required DateTime Last { get; init; }

    /// <summary>
    /// Gets the series.
    /// </summary>
    public required List<ChartSeries<double>> Series { get; init; }

    /// <summary>
    /// Gets the texts the names of the series are taken from.
    /// </summary>
    public required UiTexts SeriesTexts { get; init; }

    #endregion // Properties
}