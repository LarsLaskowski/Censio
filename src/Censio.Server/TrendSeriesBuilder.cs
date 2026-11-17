using MudBlazor;

namespace Censio.Server;

/// <summary>
/// Builds the chart series of a trend view.
/// </summary>
public static class TrendSeriesBuilder
{
    #region Constants

    /// <summary>
    /// The greatest UTC offset in minutes, in either direction, that counts as an offset.
    /// </summary>
    private const int MaxOffsetMinutes = 840;

    #endregion // Constants

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
    /// <param name="axis">The time axis</param>
    /// <returns>The series</returns>
    public static IReadOnlyList<ChartSeries<double>> Build(TrendView view, IReadOnlyList<string> lineNames, int? offsetMinutes, ChartAxis axis)
    {
        if (axis == ChartAxis.GameTime)
        {
#pragma warning disable RH2003 // Skeleton: removed with the implementation
            throw new NotImplementedException();
#pragma warning restore RH2003
        }

        var offset = OffsetOf(offsetMinutes);

        return [.. lineNames.Select((name, line) => BuildLine(view, name, line, offset))];
    }

    /// <summary>
    /// Maps a game time in milliseconds to its place on the game-time axis.
    /// </summary>
    /// <param name="gameTime">The game time in milliseconds</param>
    /// <returns>The place, or <see langword="null"/> when it lies outside <see cref="DateTime"/></returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static DateTime? GameAxisTime(long gameTime)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether a line is interrupted between two points on the game-time axis.
    /// </summary>
    /// <param name="earlier">The earlier point</param>
    /// <param name="later">The later point</param>
    /// <returns><see langword="true"/> when the line breaks before the later point</returns>
    public static bool BreaksBeforeInGameTime(TrendPoint earlier, TrendPoint later)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gives the step between the time labels of a span.
    /// </summary>
    /// <param name="span">The span the chart shows</param>
    /// <returns>The smallest step of the step list that gives at most eight intervals</returns>
    public static TimeSpan LabelSpacing(TimeSpan span)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gives the format of the time labels of an axis and span.
    /// </summary>
    /// <param name="axis">The time axis</param>
    /// <param name="span">The span the chart shows</param>
    /// <returns>The <see cref="DateTime"/> format</returns>
    public static string TimeLabelFormat(ChartAxis axis, TimeSpan span)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Gives the clock time of an instant at the browser's offset, or in UTC without a usable offset: the time the chart's axis shows.
    /// </summary>
    /// <param name="time">The instant</param>
    /// <param name="offsetMinutes">The UTC offset of the browser in minutes, or <see langword="null"/> for UTC</param>
    /// <returns>The clock time</returns>
    public static DateTime LocalTime(DateTimeOffset time, int? offsetMinutes)
    {
        return time.ToOffset(OffsetOf(offsetMinutes)).DateTime;
    }

    /// <summary>
    /// Tells whether a line is interrupted between two points: the later one starts more than <see cref="GapAfter"/> after the end of the earlier one.
    /// </summary>
    /// <param name="earlier">The earlier point</param>
    /// <param name="later">The later point</param>
    /// <returns><see langword="true"/> when the line breaks before the later point</returns>
    public static bool BreaksBefore(TrendPoint earlier, TrendPoint later)
    {
        return later.Time - EndOf(earlier) > GapAfter;
    }

    /// <summary>
    /// Gives the offset the times are shown at.
    /// </summary>
    /// <param name="offsetMinutes">The UTC offset of the browser in minutes, or <see langword="null"/> for UTC</param>
    /// <returns>The offset</returns>
    private static TimeSpan OffsetOf(int? offsetMinutes)
    {
        return offsetMinutes is { } minutes && minutes is >= -MaxOffsetMinutes and <= MaxOffsetMinutes
                   ? TimeSpan.FromMinutes(minutes)
                   : TimeSpan.Zero;
    }

    /// <summary>
    /// Gives the end of a point: a tick ends at its time, an hourly point one hour after its start.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The end</returns>
    private static DateTimeOffset EndOf(TrendPoint point)
    {
        return point.IsHourly
                   ? point.Time.AddHours(1)
                   : point.Time;
    }

    /// <summary>
    /// Builds the series of one line.
    /// </summary>
    /// <param name="view">The view</param>
    /// <param name="name">The name of the line</param>
    /// <param name="line">The index of the line</param>
    /// <param name="offset">The offset the times are shown at</param>
    /// <returns>The series</returns>
    private static ChartSeries<double> BuildLine(TrendView view, string name, int line, TimeSpan offset)
    {
        var anchor = view.Points.Select(point => point.Values[line]).FirstOrDefault(value => value is not null) ?? 0d;
        var points = new List<(DateTime Time, double Value, bool HasValue)>();

        for (var index = 0; index < view.Points.Count; index++)
        {
            var point = view.Points[index];

            if (point.Values[line] is { } value)
            {
                points.Add((point.Time.ToOffset(offset).DateTime, value, true));

                if (index + 1 < view.Points.Count && BreaksBefore(point, view.Points[index + 1]))
                {
                    points.Add((EndOf(point).ToOffset(offset).DateTime, anchor, false));
                }
            }
            else
            {
                points.Add((point.Time.ToOffset(offset).DateTime, anchor, false));
            }
        }

        var data = new ChartData<double>([.. points.Select(point => (point.Time, point.Value))]);

        for (var index = 0; index < points.Count; index++)
        {
            data.Points[index].HasValue = points[index].HasValue;
        }

        return new ChartSeries<double>
               {
                   Name = name,
                   Data = data
               };
    }

    #endregion // Methods
}