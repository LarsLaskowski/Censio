using MudBlazor;

namespace Censio.Server;

/// <summary>
/// Builds the chart series of a trend view.
/// </summary>
public static class TrendSeriesBuilder
{
    #region Fields

    /// <summary>
    /// The time after which a missing point interrupts a line.
    /// </summary>
    public static readonly TimeSpan GapAfter = TimeSpan.FromMinutes(15);

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Builds the series of a view.
    /// </summary>
    /// <param name="view">The view</param>
    /// <param name="lineNames">The names of the lines</param>
    /// <param name="offsetMinutes">The UTC offset of the browser in minutes, or <see langword="null"/> for UTC</param>
    /// <returns>The series</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static IReadOnlyList<ChartSeries<double>> Build(TrendView view, IReadOnlyList<string> lineNames, int? offsetMinutes)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    /// <summary>
    /// Tells whether a line is interrupted between two points.
    /// </summary>
    /// <param name="earlier">The earlier point</param>
    /// <param name="later">The later point</param>
    /// <returns><see langword="true"/> when the line breaks before the later point</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static bool BreaksBefore(TrendPoint earlier, TrendPoint later)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Methods
}