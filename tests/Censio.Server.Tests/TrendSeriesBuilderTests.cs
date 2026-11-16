using Censio.Contracts;

using MudBlazor;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendSeriesBuilder" />.
/// </summary>
[TestClass]
public sealed class TrendSeriesBuilderTests
{
    #region Constants

    /// <summary>
    /// The product GUID of the good whose fixture chart is checked.
    /// </summary>
    private const int FixtureGood = 2077;

    /// <summary>
    /// The index of the last tick before the 18-minute pause of the fixture.
    /// </summary>
    private const int BeforePause = 8;

    /// <summary>
    /// The index of the last tick before the session change of the fixture.
    /// </summary>
    private const int BeforeSessionChange = 31;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The island of the fixture whose chart is checked.
    /// </summary>
    private static readonly IslandKey _fixtureIsland = new(3245, 33, 1);

    /// <summary>
    /// The names of the three lines of a test chart.
    /// </summary>
    private static readonly string[] _lineNames = ["Production", "Consumption", "Delta"];

    /// <summary>
    /// The first hour of the hourly points of the tests.
    /// </summary>
    private static readonly DateTimeOffset _firstHour = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendSeriesBuilderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TrendSeriesBuilderTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// With the fixture as stored, before any fold, a line of a good on an island is interrupted at the 18-minute pause and
    /// at the ticks without a value, joined across the 9-minute session change and between every other two ticks, and a
    /// point without a value is never drawn to zero.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task TrendSeriesBuilderFixturePauseBreaksSessionChangeJoins()
    {
        // Arrange
        using var fixture = await TrendFixture.CreateAsync(TestContext.CancellationToken);

        var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, [_fixtureIsland], FixtureGood, fixture.LatestTick, TestContext.CancellationToken);
        var view = read.View ?? throw new AssertFailedException("The fixture's chart should be read");

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0);

        // Assert
        var production = series[0].Data.Points;
        var describedPairs = Enumerable.Range(1, view.Points.Count - 3).Select(index => Describe(view, production, index)).ToList();
        var valuedMinimum = production.Where(point => point.HasValue).Min(point => point.Y);
        var valuedMaximum = production.Where(point => point.HasValue).Max(point => point.Y);
        var expectedPairs = Enumerable.Range(1, view.Points.Count - 3).Select(index => index == BeforePause ? "interrupted" : "joined").ToList();

        Assert.HasCount(_lineNames.Length, series, "A chart should have a series for each line");
        Assert.HasCount(33, view.Points, "The fixture should hold 33 ticks");
        Assert.IsTrue(TrendSeriesBuilder.BreaksBefore(view.Points[BeforePause], view.Points[BeforePause + 1]), "The 18-minute pause should break the line");
        Assert.IsFalse(TrendSeriesBuilder.BreaksBefore(view.Points[BeforeSessionChange], view.Points[BeforeSessionChange + 1]), "The 9-minute session change should not break the line");
        Assert.AreSequenceEqual(expectedPairs, describedPairs, "The line should be interrupted at the pause only and joined between every other two ticks that carry the value");
        Assert.DoesNotContain(point => point.HasValue && point.Y == 0, production, "No value should be drawn as zero");
        Assert.DoesNotContain(point => point.HasValue == false && (point.Y < valuedMinimum || point.Y > valuedMaximum), production, "A point without a value should not stretch the value axis");
        Assert.IsNull(view.Points[0].Values[0], "The first tick carries no entries, so it should have no value");
        Assert.IsNull(view.Points[^1].Values[0], "The last tick carries no entries, so it should have no value");
        Assert.IsFalse(production[0].HasValue, "The line should start with the first tick's missing value");
        Assert.IsFalse(production[^1].HasValue, "The line should end with the last tick's missing value");
    }

    /// <summary>
    /// Hourly points join across consecutive hours and an hourly point joins the tick that follows it within 15 minutes of its
    /// end; a missing hour, and a gap of more than 15 minutes between ticks, break the line.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void TrendSeriesBuilderHourlyPointsJoinAndMissingHourBreaks()
    {
        // Arrange
        var points = new[]
                     {
                         Hourly(0, 1),
                         Hourly(1, 2),
                         Hourly(3, 3),
                         Hourly(4, 4),
                         Tick(_firstHour.AddHours(5).AddMinutes(10), 5),
                         Tick(_firstHour.AddHours(5).AddMinutes(25), 6),
                         Tick(_firstHour.AddHours(5).AddMinutes(40).AddSeconds(1), 7)
                     };
        var view = new TrendView
                   {
                       Points = points,
                       TotalCount = points.Length
                   };

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0);

        // Assert
        string[] expected = ["joined", "interrupted", "joined", "joined", "joined", "interrupted"];

        Assert.AreSequenceEqual(expected, Enumerable.Range(0, points.Length - 1).Select(index => Pair(view, series[0].Data.Points, index)).ToList(), "The line should be interrupted at the missing hour and after a gap of more than 15 minutes only");
        Assert.AreSequenceEqual(expected, Enumerable.Range(0, points.Length - 1).Select(index => TrendSeriesBuilder.BreaksBefore(points[index], points[index + 1]) ? "interrupted" : "joined").ToList(), "The break rule should say the same: a later start more than 15 minutes after the earlier end breaks, an hourly point ending one hour after its start");
    }

    /// <summary>
    /// The time of every point is the time at the browser's UTC offset, or in UTC without an offset.
    /// </summary>
    /// <param name="offsetMinutes">The offset in minutes, or <see langword="null" /> for none</param>
    /// <param name="expectedShiftMinutes">The expected shift of the time from UTC in minutes</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(120, 120)]
    [DataRow(-300, -300)]
    [DataRow(0, 0)]
    [DataRow(null, 0)]
    public void TrendSeriesBuilderUsesOffsetOrUtc(int? offsetMinutes, int expectedShiftMinutes)
    {
        // Arrange
        var first = new DateTimeOffset(2026, 11, 13, 23, 30, 0, TimeSpan.Zero);
        var points = new[] { Tick(first, 1), Tick(first.AddMinutes(2), 2) };
        var view = new TrendView
                   {
                       Points = points,
                       TotalCount = points.Length
                   };

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, offsetMinutes);

        // Assert
        var expected = points.Select(point => point.Time.UtcDateTime.AddMinutes(expectedShiftMinutes)).ToList();

        foreach (var line in series)
        {
            Assert.AreSequenceEqual(expected, line.Data.Points.Where(point => point.HasValue).Select(point => (DateTime)point.X!).ToList(), $"The points of {line.Name} should lie at the browser's offset, or in UTC");
        }
    }

    /// <summary>
    /// Describes how a line runs between a point and the next one of the view.
    /// </summary>
    /// <param name="view">The view</param>
    /// <param name="chartPoints">The chart points of a line</param>
    /// <param name="index">The index of the earlier point</param>
    /// <returns><c>joined</c>, <c>interrupted</c>, or <c>novalue</c> when one of the two has no value</returns>
    private static string Describe(TrendView view, IReadOnlyList<ChartPoint<double>> chartPoints, int index)
    {
        return view.Points[index].Values[0] is null || view.Points[index + 1].Values[0] is null
                   ? "novalue"
                   : Pair(view, chartPoints, index);
    }

    /// <summary>
    /// Tells whether a line is joined between a point of the view and the next one.
    /// </summary>
    /// <param name="view">The view</param>
    /// <param name="chartPoints">The chart points of a line</param>
    /// <param name="index">The index of the earlier point</param>
    /// <returns><c>joined</c> when every chart point between the two has a value, otherwise <c>interrupted</c></returns>
    private static string Pair(TrendView view, IReadOnlyList<ChartPoint<double>> chartPoints, int index)
    {
        var earlier = IndexOfValued(chartPoints, view.Points[index].Time);
        var later = IndexOfValued(chartPoints, view.Points[index + 1].Time);

        return chartPoints.Skip(earlier + 1).Take(later - earlier - 1).All(point => point.HasValue)
                   ? "joined"
                   : "interrupted";
    }

    /// <summary>
    /// Finds the chart point with a value at a time.
    /// </summary>
    /// <param name="chartPoints">The chart points of a line</param>
    /// <param name="time">The time of the point</param>
    /// <returns>The index of the chart point</returns>
    private static int IndexOfValued(IReadOnlyList<ChartPoint<double>> chartPoints, DateTimeOffset time)
    {
        for (var index = 0; index < chartPoints.Count; index++)
        {
            if (chartPoints[index].HasValue && chartPoints[index].X is DateTime pointTime && pointTime == time.UtcDateTime)
            {
                return index;
            }
        }

        throw new AssertFailedException($"The line should hold a point with a value at {time:O}");
    }

    /// <summary>
    /// Creates an hourly point of the hour a number of hours after the first hour, with the same value on every line.
    /// </summary>
    /// <param name="hours">The number of hours after the first hour</param>
    /// <param name="value">The value</param>
    /// <returns>The point</returns>
    private static TrendPoint Hourly(int hours, double value)
    {
        return new TrendPoint
               {
                   Time = _firstHour.AddHours(hours),
                   IsHourly = true,
                   Values = [value, value, value]
               };
    }

    /// <summary>
    /// Creates a tick point with the same value on every line.
    /// </summary>
    /// <param name="time">The time</param>
    /// <param name="value">The value</param>
    /// <returns>The point</returns>
    private static TrendPoint Tick(DateTimeOffset time, double value)
    {
        return new TrendPoint
               {
                   Time = time,
                   IsHourly = false,
                   Values = [value, value, value]
               };
    }

    #endregion // Methods
}