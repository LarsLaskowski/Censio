using System.Globalization;
using System.Text;

namespace Censio.Server;

/// <summary>
/// Builds the line of a sparkline from its samples.
/// </summary>
public static class SparklineBuilder
{
    #region Constants

    /// <summary>
    /// The width of a sparkline.
    /// </summary>
    public const int Width = 80;

    /// <summary>
    /// The height of a sparkline.
    /// </summary>
    public const int Height = 20;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Builds the sparkline of a series of samples.
    /// </summary>
    /// <param name="samples">The samples, oldest first</param>
    /// <param name="range">The range the sparkline spans</param>
    /// <param name="decimals">The decimals the values are rounded to for the direction</param>
    /// <returns>The sparkline, or <see langword="null"/> for fewer than two valued points</returns>
    public static SparklineView? Build(IReadOnlyList<SparklineSample> samples, TrendRange range, int decimals)
    {
        var valued = samples.Where(sample => sample.Value.HasValue).ToList();

        if (valued.Count < 2)
        {
            return null;
        }

        var minimum = valued.Min(sample => sample.Value!.Value);
        var maximum = valued.Max(sample => sample.Value!.Value);
        var span = (range.End - range.Start).Ticks;
        var path = new StringBuilder();
        var previous = default(SparklineSample?);
        var isAlone = false;

        foreach (var sample in samples)
        {
            if (sample.Value is not { } value)
            {
                CloseRun(path, ref isAlone);
                previous = null;

                continue;
            }

            var isJoined = previous is { } earlier && TrendSeriesBuilder.BreaksBefore(earlier.Time, earlier.IsHourly, sample.Time) == false;

            if (isJoined == false)
            {
                CloseRun(path, ref isAlone);
            }

            path.Append(isJoined ? 'L' : 'M')
                .Append(Coordinate(span > 0 ? (sample.Time - range.Start).Ticks * (double)Width / span : 0d, Width))
                .Append(' ')
                .Append(Coordinate(YOf(value, minimum, maximum), Height));

            isAlone = isJoined == false;
            previous = sample;
        }

        CloseRun(path, ref isAlone);

        return new SparklineView
               {
                   Path = path.ToString(),
                   Direction = DirectionOf(valued[0].Value!.Value, valued[^1].Value!.Value, decimals)
               };
    }

    /// <summary>
    /// Gives the vertical position of a value: the minimum at the bottom, the maximum at the top, all equal in the middle.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="minimum">The smallest value</param>
    /// <param name="maximum">The largest value</param>
    /// <returns>The position</returns>
    private static double YOf(double value, double minimum, double maximum)
    {
        return maximum > minimum
                   ? Height - 2 - ((value - minimum) / (maximum - minimum) * (Height - 4))
                   : Height / 2d;
    }

    /// <summary>
    /// Compares the rounded oldest and newest value.
    /// </summary>
    /// <param name="oldest">The oldest value</param>
    /// <param name="newest">The newest value</param>
    /// <param name="decimals">The decimals to round to</param>
    /// <returns>The direction</returns>
    private static TrendDirection DirectionOf(double oldest, double newest, int decimals)
    {
        var first = Math.Round(oldest, decimals, MidpointRounding.AwayFromZero);
        var last = Math.Round(newest, decimals, MidpointRounding.AwayFromZero);

        if (last > first)
        {
            return TrendDirection.Rising;
        }

        return last < first
                   ? TrendDirection.Falling
                   : TrendDirection.Stable;
    }

    /// <summary>
    /// Ends a run of the path: a point alone gets a zero-length line, so its round cap draws a dot.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="isAlone">Whether the run holds one point only, reset by the call</param>
    private static void CloseRun(StringBuilder path, ref bool isAlone)
    {
        if (isAlone)
        {
            path.Append("h0");
        }

        isAlone = false;
    }

    /// <summary>
    /// Writes a coordinate with at most one decimal, without culture or exponent.
    /// </summary>
    /// <param name="value">The coordinate</param>
    /// <param name="maximum">The largest coordinate</param>
    /// <returns>The text</returns>
    private static string Coordinate(double value, double maximum)
    {
        return (Math.Round(Math.Clamp(value, 0d, maximum), 1, MidpointRounding.AwayFromZero) + 0d).ToString("0.#", CultureInfo.InvariantCulture);
    }

    #endregion // Methods
}