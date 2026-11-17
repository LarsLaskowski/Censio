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

    /// <summary>
    /// The most intervals the time axis is divided into.
    /// </summary>
    private const int MaxLabelIntervals = 8;

    /// <summary>
    /// The smallest game time in milliseconds that has a place on the game-time axis.
    /// </summary>
    private const long MinGameTime = -62_135_596_800_000;

    /// <summary>
    /// The largest game time in milliseconds that has a place on the game-time axis.
    /// </summary>
    private const long MaxGameTime = 253_402_300_799_999;

    /// <summary>
    /// The format of the labels of the real-time axis: digits and a literal colon.
    /// </summary>
    private const string TimeOnlyLabelFormat = "HH\\:mm";

    /// <summary>
    /// The format of the labels of the real-time axis over more than a day: month, day, hour and minute.
    /// </summary>
    private const string DateLabelFormat = "MM'-'dd HH\\:mm";

    /// <summary>
    /// The format that writes no label.
    /// </summary>
    private const string EmptyLabelFormat = "''";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The time after which a missing point interrupts a line.
    /// </summary>
    public static readonly TimeSpan GapAfter = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The distances between two labels of the time axis the chart may take, the smallest first.
    /// </summary>
    private static readonly TimeSpan[] _labelSpacings = [TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12), TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7), TimeSpan.FromDays(14), TimeSpan.FromDays(30), TimeSpan.FromDays(90), TimeSpan.FromDays(365)];

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
            var placed = view.Points.Where(IsOnGameAxis).OrderBy(point => point.GameTime).ToList();

            return [.. lineNames.Select((name, line) => BuildGameLine(placed, name, line))];
        }

        var offset = OffsetOf(offsetMinutes);

        return [.. lineNames.Select((name, line) => BuildLine(view, name, line, offset))];
    }

    /// <summary>
    /// Maps a game time in milliseconds to its place on the game-time axis.
    /// </summary>
    /// <param name="gameTime">The game time in milliseconds</param>
    /// <returns>The place, or <see langword="null"/> when it lies outside <see cref="DateTime"/></returns>
    public static DateTime? GameAxisTime(long gameTime)
    {
        return gameTime is >= MinGameTime and <= MaxGameTime
                   ? DateTime.UnixEpoch.AddTicks(gameTime * TimeSpan.TicksPerMillisecond)
                   : null;
    }

    /// <summary>
    /// Tells whether a point has a place on the game-time axis: it holds an entry and its game time lies inside <see cref="DateTime"/>.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns><see langword="true"/> when the point is drawn on the game-time axis</returns>
    public static bool IsOnGameAxis(TrendPoint point)
    {
        return point is { HasNoEntries: false, GameTime: { } gameTime } && GameAxisTime(gameTime) is not null;
    }

    /// <summary>
    /// Tells whether a line is interrupted between two points on the game-time axis: the places are compared, never the raw game times.
    /// </summary>
    /// <param name="earlier">The earlier point</param>
    /// <param name="later">The later point</param>
    /// <returns><see langword="true"/> when the line breaks before the later point</returns>
    public static bool BreaksBeforeInGameTime(TrendPoint earlier, TrendPoint later)
    {
        var end = earlier.GameTime is { } earlierTime ? GameAxisTime(earlierTime) : null;
        var start = (later.FirstGameTime is { } first ? GameAxisTime(first) : null) ?? (later.GameTime is { } own ? GameAxisTime(own) : null);

        return end is not { } from || start is not { } to || to - from > GapAfter;
    }

    /// <summary>
    /// Gives the step between the time labels of a span.
    /// </summary>
    /// <param name="span">The span the chart shows</param>
    /// <returns>The smallest step of the step list that gives at most eight intervals</returns>
    public static TimeSpan LabelSpacing(TimeSpan span)
    {
        return _labelSpacings.FirstOrDefault(spacing => span / spacing <= MaxLabelIntervals, _labelSpacings[^1]);
    }

    /// <summary>
    /// Gives the format of the time labels of an axis and span.
    /// </summary>
    /// <param name="axis">The time axis</param>
    /// <param name="span">The span the chart shows</param>
    /// <returns>The <see cref="DateTime"/> format</returns>
    public static string TimeLabelFormat(ChartAxis axis, TimeSpan span)
    {
        if (axis == ChartAxis.GameTime)
        {
            return EmptyLabelFormat;
        }

        return span > TimeSpan.FromDays(1)
                   ? DateLabelFormat
                   : TimeOnlyLabelFormat;
    }

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
    /// Builds the series of one line on the game-time axis from the points that have a place, in game-time order.
    /// </summary>
    /// <param name="placed">The points that have a place, in game-time order</param>
    /// <param name="name">The name of the line</param>
    /// <param name="line">The index of the line</param>
    /// <returns>The series</returns>
    private static ChartSeries<double> BuildGameLine(List<TrendPoint> placed, string name, int line)
    {
        var anchor = placed.Select(point => point.Values[line]).FirstOrDefault(value => value is not null) ?? 0d;
        var points = new List<(DateTime Time, double Value, bool HasValue)>();

        for (var index = 0; index < placed.Count; index++)
        {
            var point = placed[index];
            var place = GameAxisTime(point.GameTime!.Value)!.Value;

            if (point.Values[line] is not { } value)
            {
                points.Add((place, anchor, false));

                continue;
            }

            points.Add((place, value, true));

            if (index + 1 < placed.Count && BreaksBeforeInGameTime(point, placed[index + 1]))
            {
                points.Add((place.Ticks > DateTime.MaxValue.Ticks - GapAfter.Ticks ? place : place + GapAfter, anchor, false));
            }
        }

        return CreateSeries(name, points);
    }

    /// <summary>
    /// Creates the series of a line from its points.
    /// </summary>
    /// <param name="name">The name of the line</param>
    /// <param name="points">The places, values and whether each has a value</param>
    /// <returns>The series</returns>
    private static ChartSeries<double> CreateSeries(string name, List<(DateTime Time, double Value, bool HasValue)> points)
    {
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

        return CreateSeries(name, points);
    }

    #endregion // Methods
}