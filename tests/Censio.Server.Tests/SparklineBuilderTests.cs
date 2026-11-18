using System.Globalization;
using System.Text.RegularExpressions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="SparklineBuilder" />.
/// </summary>
[TestClass]
public sealed partial class SparklineBuilderTests
{
    #region Constants

    /// <summary>
    /// The most characters the markup of one trend cell may take at 240 points.
    /// </summary>
    internal const int MarkupLimit = 4000;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the range of the tests: 4 hours before <see cref="_end" />.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The end of the range of the tests, so a minute is a third of a unit of width.
    /// </summary>
    private static readonly DateTimeOffset _end = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The values of the points between the oldest and the newest of <see cref="MaxPointSamples" />, which give a y
    /// coordinate with a decimal.
    /// </summary>
    private static readonly double[] _decimalValues = [1, 2, 4, 5, 7, 8, 10, 11];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// The direction compares the oldest and the newest valued point, each rounded to the decimals of its value with the
    /// midpoint away from zero: newer higher rises, lower falls, equal is stable, whatever lies between them.
    /// </summary>
    /// <param name="oldest">The oldest value</param>
    /// <param name="middle">The value between</param>
    /// <param name="newest">The newest value</param>
    /// <param name="decimals">The decimals the values are rounded to</param>
    /// <param name="expected">The direction</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(1.0, 0.5, 2.0, 2, TrendDirection.Rising)]
    [DataRow(2.0, 3.0, 1.0, 2, TrendDirection.Falling)]
    [DataRow(1.5, 9.0, 1.5, 2, TrendDirection.Stable)]
    [DataRow(-0.004, 1.0, 0.004, 2, TrendDirection.Stable)]
    [DataRow(-0.006, 1.0, 0.004, 2, TrendDirection.Rising)]
    [DataRow(3.4, 1.0, 2.6, 0, TrendDirection.Stable)]
    [DataRow(2.5, 1.0, 3.4, 0, TrendDirection.Stable)]
    [DataRow(2.4, 1.0, 2.5, 0, TrendDirection.Rising)]
    public void SparklineBuilderDirectionComparesRoundedOldestAndNewest(double oldest, double middle, double newest, int decimals, TrendDirection expected)
    {
        // Arrange
        var samples = new[] { Sample(0, oldest), Sample(30, middle), Sample(60, newest) };

        // Act
        var line = SparklineBuilder.Build(samples, Range(), decimals);

        // Assert
        Assert.IsNotNull(line, "Three valued points should give a line");
        Assert.AreEqual(expected, line.Direction, $"The values {oldest} and {newest} rounded to {decimals} decimals should give {expected}");
    }

    /// <summary>
    /// The direction skips the points without a value at either end and compares the oldest and the newest valued ones.
    /// </summary>
    /// <param name="oldest">The oldest valued value</param>
    /// <param name="newest">The newest valued value</param>
    /// <param name="expected">The direction</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(1.0, 2.0, TrendDirection.Rising)]
    [DataRow(3.0, 1.0, TrendDirection.Falling)]
    public void SparklineBuilderDirectionSkipsPointsWithoutValue(double oldest, double newest, TrendDirection expected)
    {
        // Arrange
        var samples = new[] { Sample(0, null), Sample(3, oldest), Sample(6, null), Sample(9, newest), Sample(12, null) };

        // Act
        var line = SparklineBuilder.Build(samples, Range(), 2);

        // Assert
        Assert.IsNotNull(line, "Two valued points should give a line");
        Assert.AreEqual(expected, line.Direction, "The direction should compare the oldest and newest points that have a value");
    }

    /// <summary>
    /// A series with fewer than two points that have a value gives no line.
    /// </summary>
    /// <param name="valued">The number of valued points</param>
    /// <param name="empty">The number of points without a value</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0, 0)]
    [DataRow(0, 3)]
    [DataRow(1, 0)]
    [DataRow(1, 4)]
    public void SparklineBuilderFewerThanTwoValuedPointsGivesNone(int valued, int empty)
    {
        // Arrange
        var samples = Enumerable.Range(0, valued)
                                .Select(index => Sample(index * 3, 5))
                                .Concat(Enumerable.Range(valued, empty).Select(index => Sample(index * 3, null)))
                                .ToList();

        // Act
        var line = SparklineBuilder.Build(samples, Range(), 2);

        // Assert
        Assert.IsNull(line, $"{valued} valued and {empty} empty points should give no line");
    }

    /// <summary>
    /// The line maps time to x over the range and the valued minimum to y 18, the maximum to 2; it starts anew after a
    /// point without a value and where the later point starts more than 15 minutes after the end of the earlier one (an
    /// hourly point ending an hour after its start, 15 minutes being no break), and a point alone between breaks is a dot.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void SparklineBuilderBreaksAtMissingValueAndAfterFifteenMinutes()
    {
        // Arrange
        var samples = new[]
                      {
                          new SparklineSample(_start, true, 0),
                          Sample(72, 4),
                          Sample(81, 8),
                          Sample(96, null),
                          Sample(120, 2),
                          Sample(129, 6),
                          Sample(150, 1),
                          Sample(168, 4),
                          Sample(171, 0),
                          Sample(186, 8)
                      };

        // Act
        var line = SparklineBuilder.Build(samples, Range(), 2);

        // Assert
        var expected = new[] { "M 0 18", "L 24 10", "L 27 2", "M 40 14", "L 43 6", "M 50 16", "h 0", "M 56 10", "L 57 18", "L 62 2" };

        Assert.IsNotNull(line, "The series should give a line");
        Assert.AreSequenceEqual(expected, Commands(line.Path), $"The path '{line.Path}' should move after each break, draw a dot for a point alone and join the others");
    }

    /// <summary>
    /// A line of 240 points whose x coordinates all need a decimal, built under a culture with a decimal comma, has one
    /// coordinate pair per point, each number with at most one decimal and a decimal point, and stays within the markup
    /// limit of a trend cell.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void SparklineBuilderPathOfMaxPointsStaysWithinMarkupLimit()
    {
        // Arrange
        var samples = MaxPointSamples();
        var culture = CultureInfo.CurrentCulture;

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        // Act
        SparklineView? line;

        try
        {
            line = SparklineBuilder.Build(samples, Range(), 2);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        // Assert
        Assert.IsNotNull(line, "240 valued points should give a line");

        var points = Commands(line.Path).Where(command => command.StartsWith('M') || command.StartsWith('L')).ToList();

        Assert.HasCount(240, points, "The path should hold one point per sample");
        Assert.AreEqual(0, points.Count(command => command.Split(' ').Length != 3), $"Every point should have exactly two coordinates, written without a culture: {line.Path[..Math.Min(80, line.Path.Length)]}");
        Assert.IsTrue(CoordinatesOnly().IsMatch(line.Path), "The path should hold only move and line commands and numbers with at most one decimal");
        Assert.IsLessThanOrEqualTo(MarkupLimit, line.Path.Length, "The path of 240 points should stay within the markup limit of a trend cell");
    }

    /// <summary>
    /// Creates 240 valued points one minute apart, each 20 seconds after a full minute, so every x coordinate needs a
    /// decimal, and between the oldest (lowest) and newest (highest) values whose y coordinate needs one too.
    /// </summary>
    /// <returns>The samples, oldest first</returns>
    internal static List<SparklineSample> MaxPointSamples()
    {
        return [
                   .. Enumerable.Range(0, 240)
                                .Select(index => new SparklineSample(_start.AddSeconds((index * 60) + 20),
                                                                     false,
                                                                     index switch
                                                                     {
                                                                         0 => 0,
                                                                         239 => 12,
                                                                         _ => _decimalValues[index % _decimalValues.Length]
                                                                     }))
               ];
    }

    /// <summary>
    /// Gives the range of the tests: 4 hours from 08:00 UTC, so a minute is a third of a unit of width.
    /// </summary>
    /// <returns>The range</returns>
    internal static TrendRange Range()
    {
        return new TrendRange
               {
                   Start = _start,
                   End = _end
               };
    }

    /// <summary>
    /// Creates a point that is no hourly mean.
    /// </summary>
    /// <param name="minutes">The minutes after the range start</param>
    /// <param name="value">The value, or <see langword="null" /></param>
    /// <returns>The sample</returns>
    private static SparklineSample Sample(int minutes, double? value)
    {
        return new SparklineSample(_start.AddMinutes(minutes), false, value);
    }

    /// <summary>
    /// Splits path data into its commands, each the letter and its numbers separated by single spaces, the numbers as
    /// written with a decimal point and without trailing zeros.
    /// </summary>
    /// <param name="path">The path data</param>
    /// <returns>The commands</returns>
    private static List<string> Commands(string path)
    {
        var commands = new List<List<string>>();

        foreach (var token in PathToken().Matches(path).Select(match => match.Value))
        {
            if (char.IsLetter(token[0]))
            {
                commands.Add([token]);
            }
            else if (commands.Count > 0)
            {
                commands[^1].Add(double.Parse(token, CultureInfo.InvariantCulture).ToString("0.#", CultureInfo.InvariantCulture));
            }
        }

        return [.. commands.Select(command => string.Join(' ', command))];
    }

    /// <summary>
    /// Gets the expression of a token of path data: a command letter or a number.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("[A-Za-z]|-?[0-9]+(?:\\.[0-9]+)?")]
    private static partial Regex PathToken();

    /// <summary>
    /// Gets the expression of path data of move, line and dot commands and numbers with at most one decimal.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("^(?:[MLh]|-?[0-9]+(?:\\.[0-9])?|[ ,])+$")]
    private static partial Regex CoordinatesOnly();

    #endregion // Methods
}