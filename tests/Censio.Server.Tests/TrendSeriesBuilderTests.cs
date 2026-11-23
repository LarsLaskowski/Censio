using System.Globalization;

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
    [TestCategory("AC7")]
    public async Task TrendSeriesBuilderFixturePauseBreaksSessionChangeJoins()
    {
        // Arrange
        using var fixture = await TrendFixture.CreateAsync(TestContext.CancellationToken);

        var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId,
                                                      [_fixtureIsland],
                                                      FixtureGood,
                                                      new TrendRequest
                                                      {
                                                          LatestTick = fixture.LatestTick,
                                                          Choice = ChartChoice.Default,
                                                          Held = null
                                                      },
                                                      TestContext.CancellationToken);
        var view = read.View ?? throw new AssertFailedException("The fixture's chart should be read");

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.RealTime);

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
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.RealTime);

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
        var series = TrendSeriesBuilder.Build(view, _lineNames, offsetMinutes, ChartAxis.RealTime);

        // Assert
        var expected = points.Select(point => point.Time.UtcDateTime.AddMinutes(expectedShiftMinutes)).ToList();

        foreach (var line in series)
        {
            Assert.AreSequenceEqual(expected, line.Data.Points.Where(point => point.HasValue).Select(point => (DateTime)point.X!).ToList(), $"The points of {line.Name} should lie at the browser's offset, or in UTC");
        }
    }

    /// <summary>
    /// On the game-time axis the points are drawn in game-time order at the game time, the Unix epoch plus its milliseconds,
    /// whatever their receive times; points of one game time keep their order.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void TrendSeriesBuilderGameAxisPlacesPointsInGameOrder()
    {
        // Arrange
        var view = GameView(GameTick(3_000, 1), GameTick(1_000, 2), GameTick(2_000, 3), GameTick(2_000, 4));

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.GameTime);

        // Assert
        var expected = new[] { $"{Stamp(1_000)} 2 True", $"{Stamp(2_000)} 3 True", $"{Stamp(2_000)} 4 True", $"{Stamp(3_000)} 1 True" };

        Assert.AreSequenceEqual(expected, series[0].Data.Points.Select(DescribeChartPoint), "The points should lie at their game time in game-time order, equal game times in their order");
    }

    /// <summary>
    /// On the game-time axis a tick in which no island holds an entry and a point whose game time lies outside the dates
    /// are left out, so they never stretch the axis.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void TrendSeriesBuilderGameAxisLeavesOutNoEntriesTick()
    {
        // Arrange
        var noEntries = new TrendPoint
                        {
                            Time = _firstHour.AddMinutes(2),
                            IsHourly = false,
                            Values = [null, null, null],
                            GameTime = 19_000,
                            HasNoEntries = true
                        };
        var view = GameView(GameTick(1_750_000_000, 1), noEntries, GameTick(1_750_060_000, 2), GameTick(long.MaxValue, 3));

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.GameTime);

        // Assert
        var expected = new[] { $"{Stamp(1_750_000_000)} 1 True", $"{Stamp(1_750_060_000)} 2 True" };

        Assert.AreSequenceEqual(expected, series[0].Data.Points.Select(DescribeChartPoint), "Neither the tick without entries nor the point beyond the dates should be drawn");
    }

    /// <summary>
    /// On the game-time axis a pause of receive time leaves no break, and a line breaks only when more than 15 minutes of
    /// game time pass between two points.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void TrendSeriesBuilderGameAxisJoinsPauseAndBreaksAfter15GameMinutes()
    {
        // Arrange
        var first = GameTick(0, 1, _firstHour);
        var afterPause = GameTick(120_000, 2, _firstHour.AddMinutes(18));
        var afterFifteen = GameTick(1_020_000, 3, _firstHour.AddMinutes(20));
        var afterMore = GameTick(1_920_001, 4, _firstHour.AddMinutes(21));
        var view = GameView(first, afterPause, afterFifteen, afterMore);

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.GameTime);

        // Assert
        string[] expected = ["joined", "joined", "interrupted"];

        Assert.AreSequenceEqual(expected, new[] { (0L, 120_000L), (120_000L, 1_020_000L), (1_020_000L, 1_920_001L) }.Select(pair => GamePair(series[0].Data.Points, pair.Item1, pair.Item2)).ToList(), "An 18-minute real pause and exactly 15 game minutes should join, 15 game minutes and 1 ms should break");
    }

    /// <summary>
    /// An hourly mean sits at its largest game time, and its line joins the next point unless more than 15 minutes of game
    /// time pass between the largest game time and the next point's smallest.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void TrendSeriesBuilderGameAxisHourlyFromLargestToNextSmallest()
    {
        // Arrange
        var hourly = GameHourly(1_000_000, 5_000_000, 1);
        var tick = GameTick(5_800_000, 2);
        var nextHourly = GameHourly(6_800_000, 9_000_000, 3);
        var view = GameView(hourly, tick, nextHourly);

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.GameTime);

        // Assert
        string[] expected = ["joined", "interrupted"];

        Assert.AreSequenceEqual(expected, new[] { (5_000_000L, 5_800_000L), (5_800_000L, 9_000_000L) }.Select(pair => GamePair(series[0].Data.Points, pair.Item1, pair.Item2)).ToList(), "The hour should join the tick 800 s after its largest game time, and the next hour should break 1,000 s after the tick");
    }

    /// <summary>
    /// On the game-time axis a point without a value, other than a tick without entries, stays a point without a value
    /// that breaks the line.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void TrendSeriesBuilderGameAxisKeepsNoValuePointAsBreak()
    {
        // Arrange
        var noValue = new TrendPoint
                      {
                          Time = _firstHour.AddMinutes(2),
                          IsHourly = false,
                          Values = [null, null, null],
                          GameTime = 2_000
                      };
        var view = GameView(GameTick(1_000, 1), noValue, GameTick(3_000, 3));

        // Act
        var series = TrendSeriesBuilder.Build(view, _lineNames, 0, ChartAxis.GameTime);

        // Assert
        var expected = new[] { $"{Stamp(1_000)} 1 True", $"{Stamp(2_000)} {{none}} False", $"{Stamp(3_000)} 3 True" };

        Assert.AreSequenceEqual(expected, series[0].Data.Points.Select(DescribeChartPoint), "The point without a value should stay between its neighbours and break the line");
    }

    /// <summary>
    /// A game time lies on the axis from the smallest to the largest date, and outside it has no place.
    /// </summary>
    /// <param name="gameTime">The game time in milliseconds</param>
    /// <param name="expected">The expected place, or <c>null</c></param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(0L, "1970-01-01T00:00:00.000")]
    [DataRow(1_750_000_000L, "1970-01-21T06:06:40.000")]
    [DataRow(-62_135_596_800_000L, "0001-01-01T00:00:00.000")]
    [DataRow(-62_135_596_800_001L, "null")]
    [DataRow(253_402_300_799_999L, "9999-12-31T23:59:59.999")]
    [DataRow(253_402_300_800_000L, "null")]
    [DataRow(long.MaxValue, "null")]
    [DataRow(long.MinValue, "null")]
    public void TrendSeriesBuilderGameAxisTimeMapsInRangeOnly(long gameTime, string expected)
    {
        // Act
        var place = TrendSeriesBuilder.GameAxisTime(gameTime);

        // Assert
        Assert.AreEqual(expected, place is { } time ? time.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture) : "null", $"The game time {gameTime} should have its place on the axis, or none outside the dates");
    }

    /// <summary>
    /// A line breaks before a point when its smallest game time, or its own when that has no place, lies more than 15
    /// minutes of game time after the game time of the earlier point.
    /// </summary>
    /// <param name="earlierGameTime">The game time of the earlier point</param>
    /// <param name="laterFirstGameTime">The first game time of the later point, or <c>null</c></param>
    /// <param name="laterGameTime">The game time of the later point</param>
    /// <param name="expected">Whether the line breaks</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(1_000_000L, null, 1_900_000L, false)]
    [DataRow(1_000_000L, null, 1_900_001L, true)]
    [DataRow(1_000_000L, 1_900_000L, 9_000_000L, false)]
    [DataRow(1_000_000L, 1_900_001L, 9_000_000L, true)]
    [DataRow(2_000_000L, null, 1_000_000L, false)]
    [DataRow(1_000_000L, long.MinValue, 3_000_000L, true)]
    [DataRow(1_000_000L, long.MaxValue, 3_000_000L, true)]
    public void TrendSeriesBuilderBreaksBeforeInGameTimeComparesMappedTimes(long earlierGameTime, long? laterFirstGameTime, long laterGameTime, bool expected)
    {
        // Arrange
        var earlier = GameTick(earlierGameTime, 1);
        var later = new TrendPoint
                    {
                        Time = _firstHour.AddMinutes(1),
                        IsHourly = laterFirstGameTime is not null,
                        Values = [2, 2, 2],
                        GameTime = laterGameTime,
                        FirstGameTime = laterFirstGameTime
                    };

        // Act
        var breaks = TrendSeriesBuilder.BreaksBeforeInGameTime(earlier, later);

        // Assert
        Assert.AreEqual(expected, breaks, $"The gap from {earlierGameTime} to {laterFirstGameTime ?? laterGameTime} ms should decide the break");
    }

    /// <summary>
    /// A first game time at the ends of the number range does not wrap the comparison into a break: the later point's own
    /// game time decides.
    /// </summary>
    /// <param name="laterFirstGameTime">The first game time of the later point</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(long.MinValue)]
    [DataRow(long.MaxValue)]
    public void TrendSeriesBuilderBreaksBeforeInGameTimeWithExtremeFirstGameTimeDoesNotWrap(long laterFirstGameTime)
    {
        // Arrange
        var earlier = GameTick(1_000_000, 1);
        var later = new TrendPoint
                    {
                        Time = _firstHour.AddMinutes(1),
                        IsHourly = true,
                        Values = [2, 2, 2],
                        GameTime = 1_500_000,
                        FirstGameTime = laterFirstGameTime
                    };

        // Act
        var breaks = TrendSeriesBuilder.BreaksBeforeInGameTime(earlier, later);

        // Assert
        Assert.IsFalse(breaks, "A first game time without a place should fall back to the point's game time, 500 s after the earlier point");
    }

    /// <summary>
    /// The spacing of the time labels is the smallest step of the list that gives at most eight intervals over the span, and
    /// 365 days beyond the list.
    /// </summary>
    /// <param name="spanMinutes">The span in minutes</param>
    /// <param name="expectedMinutes">The expected step in minutes</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(0, 10)]
    [DataRow(60, 10)]
    [DataRow(80, 10)]
    [DataRow(81, 15)]
    [DataRow(240, 30)]
    [DataRow(480, 60)]
    [DataRow(1_500, 360)]
    [DataRow(5_760, 720)]
    [DataRow(10_140, 1_440)]
    [DataRow(172_800, 43_200)]
    [DataRow(576_000, 129_600)]
    [DataRow(7_200_000, 525_600)]
    public void TrendSeriesBuilderLabelSpacingGivesSmallestStepOfAtMostEightIntervals(int spanMinutes, int expectedMinutes)
    {
        // Act
        var spacing = TrendSeriesBuilder.LabelSpacing(TimeSpan.FromMinutes(spanMinutes));

        // Assert
        Assert.AreEqual(TimeSpan.FromMinutes(expectedMinutes), spacing, $"A span of {spanMinutes} minutes should be divided by {expectedMinutes} minutes");
    }

    /// <summary>
    /// The real-time labels are hour and minute, with the date when the span is more than a day; the game-time axis has
    /// an empty label format.
    /// </summary>
    /// <param name="axis">The axis</param>
    /// <param name="spanMinutes">The span in minutes</param>
    /// <param name="expected">The expected format</param>
    /// <param name="expectedLabel">The label of 2026-11-13 09:05 in that format</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(ChartAxis.RealTime, 60, "HH\\:mm", "09:05")]
    [DataRow(ChartAxis.RealTime, 1_440, "HH\\:mm", "09:05")]
    [DataRow(ChartAxis.RealTime, 1_441, "MM'-'dd HH\\:mm", "11-13 09:05")]
    [DataRow(ChartAxis.RealTime, 10_140, "MM'-'dd HH\\:mm", "11-13 09:05")]
    [DataRow(ChartAxis.GameTime, 60, "''", "")]
    [DataRow(ChartAxis.GameTime, 10_140, "''", "")]
    public void TrendSeriesBuilderTimeLabelFormatDependsOnAxisAndSpan(ChartAxis axis, int spanMinutes, string expected, string expectedLabel)
    {
        // Act
        var format = TrendSeriesBuilder.TimeLabelFormat(axis, TimeSpan.FromMinutes(spanMinutes));

        // Assert
        Assert.AreEqual(expected, format, $"The {axis} labels over {spanMinutes} minutes should use the format {expected}");
        Assert.AreEqual(expectedLabel, new DateTime(2026, 11, 13, 9, 5, 0, DateTimeKind.Utc).ToString(format, CultureInfo.InvariantCulture), "The format should write the label");
    }

    /// <summary>
    /// Creates a view of points.
    /// </summary>
    /// <param name="points">The points</param>
    /// <returns>The view</returns>
    private static TrendView GameView(params TrendPoint[] points)
    {
        return new TrendView
               {
                   Points = points,
                   TotalCount = points.Length
               };
    }

    /// <summary>
    /// Creates a tick with a game time and the same value on every line.
    /// </summary>
    /// <param name="gameTime">The game time</param>
    /// <param name="value">The value</param>
    /// <param name="time">The receive time, or <see langword="null" /> for one after the first hour by the game time</param>
    /// <returns>The point</returns>
    private static TrendPoint GameTick(long gameTime, double value, DateTimeOffset? time = null)
    {
        return new TrendPoint
               {
                   Time = time ?? _firstHour.AddSeconds(gameTime % 1_000_000),
                   IsHourly = false,
                   Values = [value, value, value],
                   GameTime = gameTime
               };
    }

    /// <summary>
    /// Creates an hourly mean with its smallest and largest game time and the same value on every line.
    /// </summary>
    /// <param name="firstGameTime">The smallest game time</param>
    /// <param name="gameTime">The largest game time</param>
    /// <param name="value">The value</param>
    /// <returns>The point</returns>
    private static TrendPoint GameHourly(long firstGameTime, long gameTime, double value)
    {
        return new TrendPoint
               {
                   Time = _firstHour.AddSeconds(gameTime % 1_000_000),
                   IsHourly = true,
                   Values = [value, value, value],
                   GameTime = gameTime,
                   FirstGameTime = firstGameTime
               };
    }

    /// <summary>
    /// Gives the place of a game time as text.
    /// </summary>
    /// <param name="gameTime">The game time in milliseconds</param>
    /// <returns>The text</returns>
    private static string Stamp(long gameTime)
    {
        return DateTime.UnixEpoch.AddMilliseconds(gameTime).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Describes a chart point: its place, its value, and whether it has one.
    /// </summary>
    /// <param name="point">The chart point</param>
    /// <returns>The text</returns>
    private static string DescribeChartPoint(ChartPoint<double> point)
    {
        return $"{((DateTime)point.X!).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture)} {(point.HasValue ? point.Y.ToString("R", CultureInfo.InvariantCulture) : "{none}")} {point.HasValue}";
    }

    /// <summary>
    /// Tells whether a line is joined between the chart points with values at two game times.
    /// </summary>
    /// <param name="chartPoints">The chart points of a line</param>
    /// <param name="earlierGameTime">The game time of the earlier point</param>
    /// <param name="laterGameTime">The game time of the later point</param>
    /// <returns><c>joined</c> when every chart point between the two has a value, otherwise <c>interrupted</c></returns>
    private static string GamePair(IReadOnlyList<ChartPoint<double>> chartPoints, long earlierGameTime, long laterGameTime)
    {
        var earlier = IndexOfValuedAt(chartPoints, DateTime.UnixEpoch.AddMilliseconds(earlierGameTime));
        var later = IndexOfValuedAt(chartPoints, DateTime.UnixEpoch.AddMilliseconds(laterGameTime));

        return chartPoints.Skip(earlier + 1).Take(later - earlier - 1).All(point => point.HasValue)
                   ? "joined"
                   : "interrupted";
    }

    /// <summary>
    /// Finds the first chart point with a value at a place.
    /// </summary>
    /// <param name="chartPoints">The chart points of a line</param>
    /// <param name="place">The place</param>
    /// <returns>The index of the chart point</returns>
    private static int IndexOfValuedAt(IReadOnlyList<ChartPoint<double>> chartPoints, DateTime place)
    {
        for (var index = 0; index < chartPoints.Count; index++)
        {
            if (chartPoints[index].HasValue && chartPoints[index].X is DateTime pointTime && pointTime == place)
            {
                return index;
            }
        }

        throw new AssertFailedException($"The line should hold a point with a value at {place:O}");
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