using System.Globalization;

using Censio.Contracts;
using Censio.Server.Data;
using Censio.Server.Data.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendReader" />, over the fixture replayed into the history and over a fake of the history's chart
/// reads.
/// </summary>
[TestClass]
public sealed class TrendReaderTests
{
    #region Constants

    /// <summary>
    /// The session of the region whose total is read.
    /// </summary>
    private const int RegionSession = 3245;

    /// <summary>
    /// The text of the warning a failed chart read logs.
    /// </summary>
    private const string FailedReadMessage = "A chart could not be read from the database.";

    /// <summary>
    /// The longest the test waits for a read to start or end.
    /// </summary>
    private const int WaitSeconds = 10;

    /// <summary>
    /// How long the test waits to see that a read that must wait has not started, in milliseconds.
    /// </summary>
    private const int NegativeWaitMilliseconds = 300;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The island of the fixture whose chart is read.
    /// </summary>
    private static readonly IslandKey _island = new(3245, 33, 1);

    /// <summary>
    /// The receive time of the latest tick of the tests that use a fake.
    /// </summary>
    private static readonly DateTimeOffset _latest = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Whether each point of the folded chart is an hourly point.
    /// </summary>
    private static readonly bool[] _hourlyFlags = [true, true, false, false, false, false, false, false, false, false];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TrendReaderTests(TestContext testContext)
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
    /// With the fixture replayed, each good of an island has one point per stored tick, at the tick's receive time, with the
    /// good's production, consumption and delta of that tick, and no value where the tick lacks it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task TrendReaderFixtureIslandGoodMatchesStoredTicks()
    {
        // Arrange
        using var fixture = await TrendFixture.CreateAsync(TestContext.CancellationToken);

        var goods = fixture.Ticks.SelectMany(tick => TrendFixture.FindIsland(tick, _island)?.Entries ?? []).Select(entry => entry.ProductGuid).Distinct().Order().ToList();
        var expected = new List<string>();
        var actual = new List<string>();

        // Act
        foreach (var good in goods)
        {
            var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, [_island], good, Request(fixture.LatestTick), TestContext.CancellationToken);

            actual.AddRange(DescribeRead(good, read));
            expected.Add($"{good} total {fixture.Ticks.Count}");
            expected.AddRange(fixture.Ticks.Select(tick => DescribeTick(good, tick, TrendFixture.FirstEntry(tick, _island, good))));
        }

        // Assert
        Assert.IsGreaterThan(50, goods.Count, "The island should carry many goods, or the test would prove less");
        Assert.AreSequenceEqual(expected, actual, "Each good should have one point per stored tick, with that tick's production, consumption and delta");
    }

    /// <summary>
    /// With the fixture replayed, each point of a region's total of a good is the sum, in island key order, over the
    /// region's islands that carry the value, and no value where none does.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task TrendReaderFixtureRegionTotalIsSumOfCarryingIslands()
    {
        // Arrange
        using var fixture = await TrendFixture.CreateAsync(TestContext.CancellationToken);

        var islands = fixture.Ticks[1].Islands.Where(island => island.SessionGuid == RegionSession).Select(TrendFixture.KeyOf).OrderBy(key => key.SessionGuid).ThenBy(key => key.IslandId).ThenBy(key => key.AreaIndex).ToList();
        var good = FindPartlyCarriedGood(fixture.Ticks[1], islands);

        // Act
        var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, islands, good, Request(fixture.LatestTick), TestContext.CancellationToken);

        // Assert
        var expected = new List<string>
                       {
                           $"{good} total {fixture.Ticks.Count}"
                       };

        expected.AddRange(fixture.Ticks.Select(tick => DescribeSum(good, tick, islands)));

        Assert.AreSequenceEqual(expected, DescribeRead(good, read), "Each point should be the sum of the islands that carry the value, in island key order");
    }

    /// <summary>
    /// With the fixture replayed, each point of an island's overview equals the key figures computed for that tick, and has
    /// no value where the tick lacks the island or the island has no entries.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task TrendReaderFixtureFiguresMatchKeyFiguresPerTick()
    {
        // Arrange
        using var fixture = await TrendFixture.CreateAsync(TestContext.CancellationToken);

        var islands = fixture.Ticks[1].Islands.Select(TrendFixture.KeyOf).OrderBy(key => key.SessionGuid).ThenBy(key => key.IslandId).ThenBy(key => key.AreaIndex).ToList();
        var expected = new List<string>();
        var actual = new List<string>();

        // Act
        foreach (var island in islands)
        {
            var read = await fixture.Reader.ReadFiguresAsync(TrendFixture.ProfileId, island, Request(fixture.LatestTick), TestContext.CancellationToken);

            actual.AddRange(DescribeFigures(island, read));
            expected.Add($"{island} total {fixture.Ticks.Count}");
            expected.AddRange(fixture.Ticks.Select(tick => DescribeKeyFigures(island, tick)));
        }

        // Assert
        Assert.HasCount(24, islands, "The fixture should have 24 islands, or the test would prove less");
        Assert.AreSequenceEqual(expected, actual, "Each overview point should be the key figures of its tick");
    }

    /// <summary>
    /// With the fixture replayed and a retention run on a clock past 7 days, the island's chart holds the hourly points of
    /// 22:00 and 23:00 UTC with the means of their ticks, then the ticks from 00:00, all joined.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task TrendReaderFixtureAfterRetentionShowsJoinedHourlyMeans()
    {
        // Arrange
        using var fixture = await TrendFixture.CreateAsync(TestContext.CancellationToken);

        const int good = 2077;

        await FoldAllAsync(fixture, new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero));

        // Act
        var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, [_island], good, Request(fixture.LatestTick), TestContext.CancellationToken);

        // Assert
        var view = read.View ?? throw new AssertFailedException("The chart should be read");
        var points = view.Points;

        Assert.HasCount(10, points, "The chart should hold two hourly points and the eight ticks of the latest hour");
        Assert.AreEqual(10, view.TotalCount, "The range should hold the same ten points");
        Assert.AreSequenceEqual(_hourlyFlags, points.Select(point => point.IsHourly).ToList(), "The two hours before the latest hour should be hourly points");
        Assert.AreEqual(new DateTimeOffset(2026, 6, 10, 22, 0, 0, TimeSpan.Zero).UtcDateTime, points[0].Time.UtcDateTime, "The first hourly point should be the hour 22:00 UTC");
        Assert.AreEqual(new DateTimeOffset(2026, 6, 10, 23, 0, 0, TimeSpan.Zero).UtcDateTime, points[1].Time.UtcDateTime, "The second hourly point should be the hour 23:00 UTC");
        Assert.AreEqual(Mean(fixture.Ticks.Take(4), good), points[0].Values[0]!.Value, 1e-6, "The first hourly point should be the mean over the ticks of 22:00 that carry the value");
        Assert.AreEqual(Mean(fixture.Ticks.Skip(4).Take(21), good), points[1].Values[0]!.Value, 1e-6, "The second hourly point should be the mean over the ticks of 23:00, the pause included");
        Assert.AreSequenceEqual(fixture.Ticks.Skip(25).Select(tick => tick.ReceiveTime.UtcDateTime).ToList(), points.Skip(2).Select(point => point.Time.UtcDateTime).ToList(), "The ticks from 00:00 should follow at their receive times");
        Assert.DoesNotContain(true, Enumerable.Range(0, points.Count - 1).Select(index => TrendSeriesBuilder.BreaksBefore(points[index], points[index + 1])).ToList(), "The hourly points and the ticks should be joined");
    }

    /// <summary>
    /// The chart reads of one reader run one at a time: a second read starts only when the first has ended, and each read
    /// asks for the range of the latest tick and at most 2,880 points.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task TrendReaderReadsOneAtATime()
    {
        // Arrange
        var fake = new FakeHistoryTrends();
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var profileId = Guid.NewGuid();
        var token = TestContext.CancellationToken;
        var wait = TimeSpan.FromSeconds(WaitSeconds);

        fake.Hold();

        // Act
        var first = reader.ReadGoodAsync(profileId, [new IslandKey(3245, 1, 0), new IslandKey(3245, 2, 1)], 2077, Request(_latest), token);

        await fake.WaitForCallsAsync(1, wait, token);

        var second = reader.ReadFiguresAsync(profileId, new IslandKey(3245, 1, 0), Request(_latest), token);

        await Task.Delay(NegativeWaitMilliseconds, token);

        var startedWhileFirstHeld = fake.CallCount;

        fake.Release();
        await Task.WhenAll(first, second).WaitAsync(wait, token);

        // Assert
        var range = TrendRange.For(ChartRange.LastDay, _latest);

        Assert.AreEqual(1, startedWhileFirstHeld, "The second read should not start while the first one runs");
        Assert.AreEqual(2, fake.CallCount, "Both reads should run in the end");
        Assert.AreEqual(1, fake.MaxConcurrentCalls, "No two chart reads should run at once");
        Assert.AreEqual(TrendRange.MaxPoints, fake.GoodQueries[0].MaxPoints, "A read should ask for at most 2,880 points");
        Assert.AreEqual(range.Start.UtcDateTime, fake.GoodQueries[0].RangeStart.UtcDateTime, "A read should start at the range of the latest tick");
        Assert.AreEqual(range.End.UtcDateTime, fake.GoodQueries[0].RangeEnd.UtcDateTime, "A read should end at the range of the latest tick");
        Assert.AreEqual(profileId, fake.GoodQueries[0].ProfileId, "A read should ask for the profile");
        Assert.AreEqual(2077, fake.GoodQueries[0].ProductGuid, "A read should ask for the good");
        Assert.AreSequenceEqual(new[] { new HistoryIslandKey(3245, 1, 0), new HistoryIslandKey(3245, 2, 1) }, fake.GoodQueries[0].Islands.ToList(), "A read should ask for the islands given");
        Assert.AreEqual(TrendRange.MaxPoints, fake.FiguresQueries[0].MaxPoints, "A figures read should ask for at most 2,880 points");
        Assert.AreEqual(new HistoryIslandKey(3245, 1, 0), fake.FiguresQueries[0].Island, "A figures read should ask for the island given");
    }

    /// <summary>
    /// A read cancelled with its page ends by cancellation, waiting or running, shows and logs nothing, and leaves the
    /// reader free for the next read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task TrendReaderDisposedPageCancelsRead()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        var fake = new FakeHistoryTrends();
        var reader = new TrendReader(fake, factory.CreateLogger<TrendReader>());
        var token = TestContext.CancellationToken;
        var wait = TimeSpan.FromSeconds(WaitSeconds);

        fake.Hold();

        using var runningPage = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var waitingPage = CancellationTokenSource.CreateLinkedTokenSource(token);

        var running = reader.ReadGoodAsync(Guid.NewGuid(), [new IslandKey(1, 1, 0)], 1, Request(_latest), runningPage.Token);

        await fake.WaitForCallsAsync(1, wait, token);

        var waiting = reader.ReadFiguresAsync(Guid.NewGuid(), new IslandKey(1, 1, 0), Request(_latest), waitingPage.Token);

        // Act
        await waitingPage.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting, "A waiting read should end by cancellation");
        await runningPage.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => running, "A running read should end by cancellation");

        fake.Release();

        var next = await reader.ReadFiguresAsync(Guid.NewGuid(), new IslandKey(1, 1, 0), Request(_latest), token).WaitAsync(wait, token);

        // Assert
        Assert.AreEqual(1, fake.CancelledCount, "The running read should have been cancelled in the history");
        Assert.AreEqual(2, fake.CallCount, "The waiting read should never have reached the history");
        Assert.IsFalse(next.IsFailed, "The reader should read again after the cancellations");
        Assert.IsEmpty(provider.Logger.Entries, "A cancelled read should log nothing");
    }

    /// <summary>
    /// A failed read gives a failed result without a view and logs one warning with the exception and no value, name or
    /// address.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task TrendReaderFailedReadLogsWithoutValues()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        var failure = new InvalidOperationException("Island Secret 7f3a at 3245/33/1");
        var fake = new FakeHistoryTrends
                   {
                       Failure = failure
                   };
        var reader = new TrendReader(fake, factory.CreateLogger<TrendReader>());

        // Act
        var read = await reader.ReadGoodAsync(Guid.NewGuid(), [new IslandKey(3245, 33, 1)], 2077, Request(_latest), TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(read.IsFailed, "A failed read should be reported as failed");
        Assert.IsNull(read.View, "A failed read should give no view");
        Assert.HasCount(1, provider.Logger.Entries, "A failed read should log once");
        Assert.AreEqual(LogLevel.Warning, provider.Logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual(FailedReadMessage, provider.Logger.Entries[0].Message, "The entry should carry the text and no argument");
        Assert.AreSame(failure, provider.Logger.Entries[0].Exception, "The entry should carry the exception");
    }

    /// <summary>
    /// A good read asks the history for the range of the choice up to the latest tick and for the resolution of the range:
    /// ticks for an hour and a day, hourly means for a week and for all.
    /// </summary>
    /// <param name="range">The range of the choice</param>
    /// <param name="expectedStartText">The start the read asks for</param>
    /// <param name="expectedResolution">The resolution the read asks for</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(ChartRange.LastHour, "2026-11-13T11:00:00+00:00", HistoryTrendResolution.Points)]
    [DataRow(ChartRange.LastDay, "2026-11-12T12:00:00+00:00", HistoryTrendResolution.Points)]
    [DataRow(ChartRange.LastWeek, "2026-11-06T12:00:00+00:00", HistoryTrendResolution.Hours)]
    [DataRow(ChartRange.All, "0001-01-01T00:00:00+00:00", HistoryTrendResolution.Hours)]
    public async Task TrendReaderReadGoodPassesRangeAndResolutionOfChoice(ChartRange range, string expectedStartText, HistoryTrendResolution expectedResolution)
    {
        // Arrange
        var fake = new FakeHistoryTrends();
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);

        // Act
        await reader.ReadGoodAsync(Guid.NewGuid(), [_island], 2077, Request(_latest, range, null), TestContext.CancellationToken);

        // Assert
        var query = fake.GoodQueries[0];

        Assert.AreEqual(DateTimeOffset.Parse(expectedStartText, CultureInfo.InvariantCulture).UtcDateTime, query.RangeStart.UtcDateTime, $"The read of {range} should start at its range start");
        Assert.AreEqual(_latest.AddTicks(1).UtcDateTime, query.RangeEnd.UtcDateTime, "The read should end right after the latest tick");
        Assert.AreEqual(expectedResolution, query.Resolution, $"The read of {range} should ask for its resolution");
    }

    /// <summary>
    /// A figures read asks the history for the range of the choice up to the latest tick and for the resolution of the
    /// range.
    /// </summary>
    /// <param name="range">The range of the choice</param>
    /// <param name="expectedStartText">The start the read asks for</param>
    /// <param name="expectedResolution">The resolution the read asks for</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(ChartRange.LastHour, "2026-11-13T11:00:00+00:00", HistoryTrendResolution.Points)]
    [DataRow(ChartRange.LastDay, "2026-11-12T12:00:00+00:00", HistoryTrendResolution.Points)]
    [DataRow(ChartRange.LastWeek, "2026-11-06T12:00:00+00:00", HistoryTrendResolution.Hours)]
    [DataRow(ChartRange.All, "0001-01-01T00:00:00+00:00", HistoryTrendResolution.Hours)]
    public async Task TrendReaderReadFiguresPassesRangeAndResolutionOfChoice(ChartRange range, string expectedStartText, HistoryTrendResolution expectedResolution)
    {
        // Arrange
        var fake = new FakeHistoryTrends();
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);

        // Act
        await reader.ReadFiguresAsync(Guid.NewGuid(), _island, Request(_latest, range, null), TestContext.CancellationToken);

        // Assert
        var query = fake.FiguresQueries[0];

        Assert.AreEqual(DateTimeOffset.Parse(expectedStartText, CultureInfo.InvariantCulture).UtcDateTime, query.RangeStart.UtcDateTime, $"The read of {range} should start at its range start");
        Assert.AreEqual(_latest.AddTicks(1).UtcDateTime, query.RangeEnd.UtcDateTime, "The read should end right after the latest tick");
        Assert.AreEqual(expectedResolution, query.Resolution, $"The read of {range} should ask for its resolution");
    }

    /// <summary>
    /// A wide range with a held view reads only from the hour of the held view's last point and merges the newest hours
    /// into the held ones: the last held hour is replaced, the older ones stay, the total follows.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task TrendReaderWideRangeWithHeldViewReadsFromNewestHour()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = new HistoryTrend<HistoryGoodPoint>
                                   {
                                       Points = [HourPoint(_latest.AddHours(-1), 5), HourPoint(_latest, 6)],
                                       TotalCount = 2
                                   }
                   };
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var held = new TrendView
                   {
                       Points = [HeldPoint(_latest.AddHours(-3), 1), HeldPoint(_latest.AddHours(-2), 2), HeldPoint(_latest.AddHours(-1), 3)],
                       TotalCount = 3
                   };

        // Act
        var read = await reader.ReadGoodAsync(Guid.NewGuid(), [_island], 2077, Request(_latest, ChartRange.LastWeek, held), TestContext.CancellationToken);

        // Assert
        var view = read.View ?? throw new AssertFailedException("The chart should be read");

        Assert.AreEqual(_latest.AddHours(-1).UtcDateTime, fake.GoodQueries[0].RangeStart.UtcDateTime, "The read should start at the hour of the held view's last point");
        Assert.AreEqual(HistoryTrendResolution.Hours, fake.GoodQueries[0].Resolution, "The read should be hourly");

        var expected = new[] { $"{Time(_latest.AddHours(-3))} 1", $"{Time(_latest.AddHours(-2))} 2", $"{Time(_latest.AddHours(-1))} 5", $"{Time(_latest)} 6" };

        Assert.AreSequenceEqual(expected, view.Points.Select(point => $"{Time(point.Time)} {Value(point.Values[0])}"), "The newest hours should replace the last held hour and follow the older held ones");
        Assert.AreEqual(4, view.TotalCount, "The total should be the held total minus the replaced hour plus the new total");
    }

    /// <summary>
    /// A wide range with a held view without a point reads the whole range.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task TrendReaderWideRangeWithEmptyHeldViewReadsWholeRange()
    {
        // Arrange
        var fake = new FakeHistoryTrends();
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var held = new TrendView
                   {
                       Points = [],
                       TotalCount = 0
                   };

        // Act
        await reader.ReadGoodAsync(Guid.NewGuid(), [_island], 2077, Request(_latest, ChartRange.LastWeek, held), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(_latest.AddDays(-7).UtcDateTime, fake.GoodQueries[0].RangeStart.UtcDateTime, "A held view without a point should leave the whole week to read");
    }

    /// <summary>
    /// A narrow range reads its whole range and shows what the history gives, whatever view the page holds.
    /// </summary>
    /// <param name="range">The narrow range</param>
    /// <param name="expectedStartText">The start the read asks for</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(ChartRange.LastHour, "2026-11-13T11:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-11-12T12:00:00+00:00")]
    public async Task TrendReaderNarrowRangeIgnoresHeldView(ChartRange range, string expectedStartText)
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = new HistoryTrend<HistoryFiguresPoint>
                                      {
                                          Points = [
                                                       new HistoryFiguresPoint
                                                       {
                                                           Time = _latest,
                                                           IsHourly = false,
                                                           Buildings = 7,
                                                           ProducedGoods = 1,
                                                           GoodsInDeficit = 0
                                                       }
                                                   ],
                                          TotalCount = 1
                                      }
                   };
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var held = new TrendView
                   {
                       Points = [HeldPoint(_latest.AddMinutes(-30), 1)],
                       TotalCount = 1
                   };

        // Act
        var read = await reader.ReadFiguresAsync(Guid.NewGuid(), _island, Request(_latest, range, held), TestContext.CancellationToken);

        // Assert
        var view = read.View ?? throw new AssertFailedException("The chart should be read");

        Assert.AreEqual(DateTimeOffset.Parse(expectedStartText, CultureInfo.InvariantCulture).UtcDateTime, fake.FiguresQueries[0].RangeStart.UtcDateTime, "The read should start at the start of its range");
        Assert.HasCount(1, view.Points, "The view should hold the points the history gave and none of the held view");
        Assert.AreEqual(1, view.TotalCount, "The total should be the history's");
    }

    /// <summary>
    /// A goods sparkline read is one read for all shown goods of the island over the window of the latest tick, at most
    /// 240 points; it gives every asked good one sample per point, a point without its delta a sample without a value, and
    /// the line the builder draws of those samples with two decimals, or none.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task TrendReaderReadGoodSparklinesAsksForTheWindowAndShownGoods()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = GoodsTrend(GoodsPoint(_latest.AddMinutes(-4), (1010, 1), (1011, 2)), GoodsPoint(_latest.AddMinutes(-2), (1010, 2)), GoodsPoint(_latest, (1010, 3), (1011, 1)))
                   };
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var profileId = Guid.NewGuid();

        // Act
        var read = await reader.ReadGoodSparklinesAsync(profileId, _island, [1010, 1011, 1012], _latest, null, TestContext.CancellationToken);

        // Assert
        var range = TrendRange.ForSparklines(_latest);
        var expected = new Dictionary<int, List<SparklineSample>>
                       {
                           [1010] = Samples((_latest.AddMinutes(-4), 1), (_latest.AddMinutes(-2), 2), (_latest, 3)),
                           [1011] = Samples((_latest.AddMinutes(-4), 2), (_latest.AddMinutes(-2), null), (_latest, 1)),
                           [1012] = Samples((_latest.AddMinutes(-4), null), (_latest.AddMinutes(-2), null), (_latest, null))
                       };

        Assert.HasCount(1, fake.GoodsQueries, "All goods should be read in one read");

        var query = fake.GoodsQueries[0];

        Assert.AreEqual(profileId, query.ProfileId, "The read should ask for the profile");
        Assert.AreEqual(new HistoryIslandKey(3245, 33, 1), query.Island, "The read should ask for the island");
        Assert.AreSequenceEqual(expected.Keys.Order().ToList(), query.ProductGuids.ToList(), "The read should ask for the shown goods");
        Assert.AreEqual(range.Start.UtcDateTime, query.RangeStart.UtcDateTime, "The read should start at the start of the sparklines' window");
        Assert.AreEqual(range.End.UtcDateTime, query.RangeEnd.UtcDateTime, "The read should end right after the latest tick");
        Assert.AreEqual(TrendRange.SparklineMaxPoints, query.MaxPoints, "The read should ask for at most 240 points");
        Assert.IsFalse(read.IsFailed, "The read should not be failed");
        Assert.AreSequenceEqual(DescribeSamples(expected), DescribeSamples(read.Samples), "Every asked good should get one sample per point, without a value where the point lacks its delta");
        Assert.AreSequenceEqual(DescribeLines(expected, range, 2), DescribeLines(read.Lines), "Every asked good should get the line the builder draws of its samples with two decimals, or none");
    }

    /// <summary>
    /// A failed sparkline read gives a failed result and logs the charts' one warning with the exception and no value,
    /// name or address.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task TrendReaderSparklineReadFailureLogsChartWarningOnce()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        var failure = new InvalidOperationException("Island Secret 7f3a at 3245/33/1");
        var fake = new FakeHistoryTrends
                   {
                       GoodsFailure = failure
                   };
        var reader = new TrendReader(fake, factory.CreateLogger<TrendReader>());

        // Act
        var read = await reader.ReadGoodSparklinesAsync(Guid.NewGuid(), _island, [2077], _latest, null, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(read.IsFailed, "A failed read should be reported as failed");
        Assert.HasCount(1, provider.Logger.Entries, "A failed read should log once");
        Assert.AreEqual(LogLevel.Warning, provider.Logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual(FailedReadMessage, provider.Logger.Entries[0].Message, "The entry should be the charts' warning and carry no argument");
        Assert.AreSame(failure, provider.Logger.Entries[0].Exception, "The entry should carry the exception");
    }

    /// <summary>
    /// A sparkline read cancelled while it runs ends by cancellation, logs nothing, and leaves the reader free for the next
    /// read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task TrendReaderSparklineReadCancelledLogsNothing()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        var fake = new FakeHistoryTrends();
        var reader = new TrendReader(fake, factory.CreateLogger<TrendReader>());
        var token = TestContext.CancellationToken;
        var wait = TimeSpan.FromSeconds(WaitSeconds);

        fake.HoldRowTrends();

        using var page = CancellationTokenSource.CreateLinkedTokenSource(token);

        var running = reader.ReadIslandSparklinesAsync(Guid.NewGuid(), [_island], _latest, null, page.Token);

        await fake.WaitForRowTrendCallsAsync(1, wait, token);

        // Act
        await page.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => running, "A running read should end by cancellation");

        fake.ReleaseRowTrends();

        var next = await reader.ReadIslandSparklinesAsync(Guid.NewGuid(), [_island], _latest, null, token).WaitAsync(wait, token);

        // Assert
        Assert.AreEqual(1, fake.CancelledCount, "The running read should have been cancelled in the history");
        Assert.IsFalse(next.IsFailed, "The reader should read again after the cancellation");
        Assert.IsEmpty(provider.Logger.Entries, "A cancelled read should log nothing");
    }

    /// <summary>
    /// A refresh with a held read of the same goods reads only from just after the held newest point up to the new
    /// latest tick, appends the new samples to the held ones and draws the line over the new window.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task TrendReaderSparklineRefreshReadsOnlyAfterNewestHeldPoint()
    {
        // Arrange
        var later = _latest.AddMinutes(2);
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = GoodsTrend(GoodsPoint(later, (1010, 4)))
                   };
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var held = HeldRead(1010, Samples((_latest.AddMinutes(-4), 1), (_latest.AddMinutes(-2), 2), (_latest, 3)));

        // Act
        var read = await reader.ReadGoodSparklinesAsync(Guid.NewGuid(), _island, [1010], later, held, TestContext.CancellationToken);

        // Assert
        var range = TrendRange.ForSparklines(later);
        var expected = new Dictionary<int, List<SparklineSample>>
                       {
                           [1010] = Samples((_latest.AddMinutes(-4), 1), (_latest.AddMinutes(-2), 2), (_latest, 3), (later, 4))
                       };

        Assert.AreEqual(_latest.AddTicks(1).UtcDateTime, fake.GoodsQueries[0].RangeStart.UtcDateTime, "The refresh should read from just after the held newest point");
        Assert.AreEqual(range.End.UtcDateTime, fake.GoodsQueries[0].RangeEnd.UtcDateTime, "The refresh should read up to the new latest tick");
        Assert.AreSequenceEqual(DescribeSamples(expected), DescribeSamples(read.Samples), "The new samples should follow the held ones");
        Assert.AreSequenceEqual(DescribeLines(expected, range, 2), DescribeLines(read.Lines), "The line should be drawn over the new window from all samples");
    }

    /// <summary>
    /// A refresh drops the held samples before the new window's start, keeps the one at it, and of the held and new
    /// samples keeps the newest 240.
    /// </summary>
    /// <param name="heldInWindow">The number of held samples from the new window's start on, 30 seconds apart</param>
    /// <param name="newCount">The number of new samples, two minutes apart up to the new latest tick</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(2, 2)]
    [DataRow(240, 2)]
    public async Task TrendReaderSparklineRefreshDropsPointsBeforeWindowAndKeepsNewest240(int heldInWindow, int newCount)
    {
        // Arrange
        var later = _latest.AddHours(1);
        var range = TrendRange.ForSparklines(later);
        var inWindow = Enumerable.Range(0, heldInWindow).Select(index => new SparklineSample(range.Start.AddSeconds(index * 30), false, index + 1)).ToList();
        var newPoints = Enumerable.Range(0, newCount).Select(index => GoodsPoint(later.AddMinutes(2 * (index - newCount + 1)), (1010, 1000 + index))).ToList();
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = GoodsTrend([.. newPoints])
                   };
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var held = HeldRead(1010, [new SparklineSample(range.Start.AddMinutes(-10), false, 0), .. inWindow]);

        // Act
        var read = await reader.ReadGoodSparklinesAsync(Guid.NewGuid(), _island, [1010], later, held, TestContext.CancellationToken);

        // Assert
        var kept = inWindow.Concat(newPoints.Select(point => new SparklineSample(point.Time, false, point.Deltas[1010]))).ToList();
        var expected = new Dictionary<int, List<SparklineSample>>
                       {
                           [1010] = [.. kept.Skip(Math.Max(0, kept.Count - TrendRange.SparklineMaxPoints))]
                       };

        Assert.AreEqual(inWindow[^1].Time.AddTicks(1).UtcDateTime, fake.GoodsQueries[0].RangeStart.UtcDateTime, "The refresh should read from just after the held newest point");
        Assert.AreSequenceEqual(DescribeSamples(expected), DescribeSamples(read.Samples), "The samples before the window should be dropped and the newest 240 kept");
        Assert.AreSequenceEqual(DescribeLines(expected, range, 2), DescribeLines(read.Lines), "The line should be drawn from the kept samples");
    }

    /// <summary>
    /// A refresh whose islands differ from the held read's reads the whole window and keeps none of the held samples; an
    /// island read draws its lines with no decimals.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task TrendReaderSparklineChangedKeySetReadsWhole()
    {
        // Arrange
        var first = new IslandKey(3245, 1, 0);
        var second = new IslandKey(3245, 2, 0);
        var later = _latest.AddMinutes(2);
        var fake = new FakeHistoryTrends
                   {
                       IslandsTrend = new HistoryTrend<HistoryIslandsPoint>
                                      {
                                          Points = [IslandsPoint(_latest, (first, 6)), IslandsPoint(later, (first, 7), (second, 3))],
                                          TotalCount = 2
                                      }
                   };
        var reader = new TrendReader(fake, NullLogger<TrendReader>.Instance);
        var held = HeldRead(first, Samples((_latest.AddMinutes(-2), 5), (_latest, 6)));

        // Act
        var read = await reader.ReadIslandSparklinesAsync(Guid.NewGuid(), [first, second], later, held, TestContext.CancellationToken);

        // Assert
        var range = TrendRange.ForSparklines(later);
        var expected = new Dictionary<IslandKey, List<SparklineSample>>
                       {
                           [first] = Samples((_latest, 6), (later, 7)),
                           [second] = Samples((_latest, null), (later, 3))
                       };
        var query = fake.IslandsQueries[0];

        Assert.AreEqual(range.Start.UtcDateTime, query.RangeStart.UtcDateTime, "A changed set of islands should read the whole window");
        Assert.AreEqual(range.End.UtcDateTime, query.RangeEnd.UtcDateTime, "The read should end right after the latest tick");
        Assert.AreEqual(TrendRange.SparklineMaxPoints, query.MaxPoints, "The read should ask for at most 240 points");
        Assert.AreSequenceEqual(new[] { new HistoryIslandKey(3245, 1, 0), new HistoryIslandKey(3245, 2, 0) }, query.Islands.ToList(), "The read should ask for the islands given");
        Assert.AreSequenceEqual(DescribeSamples(expected), DescribeSamples(read.Samples), "The samples should be the read's only, without the held ones");
        Assert.AreSequenceEqual(DescribeLines(expected, range, 0), DescribeLines(read.Lines), "Every island should get the line the builder draws of its samples with no decimals");
    }

    /// <summary>
    /// Finds a good the first tick's islands carry on some of them, but not on all.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="islands">The islands of the region</param>
    /// <returns>The product GUID</returns>
    private static int FindPartlyCarriedGood(ContractTick tick, List<IslandKey> islands)
    {
        var good = tick.Islands.SelectMany(island => island.Entries.Select(entry => entry.ProductGuid))
                               .Distinct()
                               .Where(candidate => candidate != 0)
                               .Order()
                               .FirstOrDefault(candidate => islands.Count(island => TrendFixture.FirstEntry(tick, island, candidate) is not null) is > 1 and var carriers && carriers < islands.Count);

        Assert.AreNotEqual(0, good, "A good carried by some islands of the region should exist");

        return good;
    }

    /// <summary>
    /// Computes the mean production of a good on the island over the ticks that carry it.
    /// </summary>
    /// <param name="ticks">The ticks</param>
    /// <param name="good">The product GUID</param>
    /// <returns>The mean</returns>
    private static double Mean(IEnumerable<ContractTick> ticks, int good)
    {
        return ticks.Select(tick => TrendFixture.FirstEntry(tick, _island, good)?.Generation).OfType<float>().Select(value => (double)value).Average();
    }

    /// <summary>
    /// Describes the points of a read of a good: the total, then each point.
    /// </summary>
    /// <param name="good">The product GUID</param>
    /// <param name="read">The read</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeRead(int good, TrendRead read)
    {
        var view = read.View ?? throw new AssertFailedException($"The chart of the good {good} should be read");
        var lines = new List<string>
                    {
                        $"{good} total {view.TotalCount}"
                    };

        lines.AddRange(view.Points.Select(point => $"{good} {Time(point.Time)} {(point.IsHourly ? "hourly" : "tick")} {Value(point.Values[0])}/{Value(point.Values[1])}/{Value(point.Values[2])}"));

        return lines;
    }

    /// <summary>
    /// Describes the points of a read of an island's figures: the total, then each point.
    /// </summary>
    /// <param name="island">The island</param>
    /// <param name="read">The read</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeFigures(IslandKey island, TrendRead read)
    {
        var view = read.View ?? throw new AssertFailedException($"The overview of {island} should be read");
        var lines = new List<string>
                    {
                        $"{island} total {view.TotalCount}"
                    };

        lines.AddRange(view.Points.Select(point => $"{island} {Time(point.Time)} {(point.IsHourly ? "hourly" : "tick")} {Value(point.Values[0])}/{Value(point.Values[1])}/{Value(point.Values[2])}"));

        return lines;
    }

    /// <summary>
    /// Describes what a read of a good should give for a tick.
    /// </summary>
    /// <param name="good">The product GUID</param>
    /// <param name="tick">The tick</param>
    /// <param name="entry">The first entry of the good on the island in the tick, or <see langword="null" /></param>
    /// <returns>The line</returns>
    private static string DescribeTick(int good, ContractTick tick, ContractTickEntry? entry)
    {
        return $"{good} {Time(tick.ReceiveTime)} tick {Value(entry?.Generation)}/{Value(entry?.Consumption)}/{Value(entry?.Delta)}";
    }

    /// <summary>
    /// Describes what the total of a good over islands should give for a tick: the sum of each value over the islands that
    /// carry it, in the order given.
    /// </summary>
    /// <param name="good">The product GUID</param>
    /// <param name="tick">The tick</param>
    /// <param name="islands">The islands, in key order</param>
    /// <returns>The line</returns>
    private static string DescribeSum(int good, ContractTick tick, IReadOnlyList<IslandKey> islands)
    {
        var entries = islands.Select(island => TrendFixture.FirstEntry(tick, island, good)).OfType<ContractTickEntry>().ToList();

        return $"{good} {Time(tick.ReceiveTime)} tick {Value(Sum(entries.Select(entry => entry.Generation)))}/{Value(Sum(entries.Select(entry => entry.Consumption)))}/{Value(Sum(entries.Select(entry => entry.Delta)))}";
    }

    /// <summary>
    /// Describes what the overview of an island should give for a tick.
    /// </summary>
    /// <param name="island">The island</param>
    /// <param name="tick">The tick</param>
    /// <returns>The line</returns>
    private static string DescribeKeyFigures(IslandKey island, ContractTick tick)
    {
        var entries = TrendFixture.FindIsland(tick, island)?.Entries ?? [];
        var figures = entries.Count == 0
                          ? "null/null/null"
                          : $"{entries.Sum(entry => (long)entry.BuildingCount)}/{entries.Count(entry => entry.BuildingCount > 0)}/{entries.Count(entry => entry.Delta is { } delta && Math.Round((double)delta, 2, MidpointRounding.AwayFromZero) < 0)}";

        return $"{island} {Time(tick.ReceiveTime)} tick {figures}";
    }

    /// <summary>
    /// Sums values in the order given; the sum of no value is no value.
    /// </summary>
    /// <param name="values">The values</param>
    /// <returns>The sum, or <see langword="null" /></returns>
    private static double? Sum(IEnumerable<float?> values)
    {
        double? total = null;

        foreach (var value in values.OfType<float>())
        {
            total = (total ?? 0) + value;
        }

        return total;
    }

    /// <summary>
    /// Creates a request of the default choice without a held view.
    /// </summary>
    /// <param name="latest">The receive time of the latest tick</param>
    /// <returns>The request</returns>
    private static TrendRequest Request(DateTimeOffset latest)
    {
        return Request(latest, ChartChoice.Default.Range, null);
    }

    /// <summary>
    /// Creates a request on the real-time axis.
    /// </summary>
    /// <param name="latest">The receive time of the latest tick</param>
    /// <param name="range">The range</param>
    /// <param name="held">The held view, or <see langword="null" /></param>
    /// <returns>The request</returns>
    private static TrendRequest Request(DateTimeOffset latest, ChartRange range, TrendView? held)
    {
        return new TrendRequest
               {
                   LatestTick = latest,
                   Choice = new ChartChoice
                            {
                                Range = range,
                                Axis = ChartAxis.RealTime
                            },
                   Held = held
               };
    }

    /// <summary>
    /// Creates an hourly point of the history with a generation.
    /// </summary>
    /// <param name="time">The start of the hour</param>
    /// <param name="generation">The generation</param>
    /// <returns>The point</returns>
    private static HistoryGoodPoint HourPoint(DateTimeOffset time, double generation)
    {
        return new HistoryGoodPoint
               {
                   Time = time,
                   IsHourly = true,
                   Generation = generation,
                   Consumption = 0,
                   Delta = 0
               };
    }

    /// <summary>
    /// Creates an hourly point of a held view with a first value.
    /// </summary>
    /// <param name="time">The start of the hour</param>
    /// <param name="value">The first value</param>
    /// <returns>The point</returns>
    private static TrendPoint HeldPoint(DateTimeOffset time, double value)
    {
        return new TrendPoint
               {
                   Time = time,
                   IsHourly = true,
                   Values = [value, 0, 0]
               };
    }

    /// <summary>
    /// Creates a goods trend of points.
    /// </summary>
    /// <param name="points">The points, oldest first</param>
    /// <returns>The trend</returns>
    private static HistoryTrend<HistoryGoodsPoint> GoodsTrend(params HistoryGoodsPoint[] points)
    {
        return new HistoryTrend<HistoryGoodsPoint>
               {
                   Points = points,
                   TotalCount = points.Length
               };
    }

    /// <summary>
    /// Creates a tick point of a goods trend.
    /// </summary>
    /// <param name="time">The time</param>
    /// <param name="deltas">The product GUIDs and deltas of the goods with a value</param>
    /// <returns>The point</returns>
    private static HistoryGoodsPoint GoodsPoint(DateTimeOffset time, params (int ProductGuid, double Delta)[] deltas)
    {
        return new HistoryGoodsPoint
               {
                   Time = time,
                   IsHourly = false,
                   Deltas = deltas.ToDictionary(delta => delta.ProductGuid, delta => delta.Delta)
               };
    }

    /// <summary>
    /// Creates a tick point of an islands trend.
    /// </summary>
    /// <param name="time">The time</param>
    /// <param name="buildings">The islands and building counts of the islands with entries</param>
    /// <returns>The point</returns>
    private static HistoryIslandsPoint IslandsPoint(DateTimeOffset time, params (IslandKey Island, long Buildings)[] buildings)
    {
        return new HistoryIslandsPoint
               {
                   Time = time,
                   IsHourly = false,
                   Buildings = buildings.ToDictionary(island => new HistoryIslandKey(island.Island.SessionGuid, island.Island.IslandId, island.Island.AreaIndex), island => island.Buildings)
               };
    }

    /// <summary>
    /// Creates samples of tick points.
    /// </summary>
    /// <param name="values">The times and values</param>
    /// <returns>The samples</returns>
    private static List<SparklineSample> Samples(params (DateTimeOffset Time, double? Value)[] values)
    {
        return [.. values.Select(value => new SparklineSample(value.Time, false, value.Value))];
    }

    /// <summary>
    /// Creates a held read of one key.
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <param name="key">The key</param>
    /// <param name="samples">The samples of the key</param>
    /// <returns>The read</returns>
    private static SparklineRead<TKey> HeldRead<TKey>(TKey key, List<SparklineSample> samples)
        where TKey : notnull
    {
        return new SparklineRead<TKey>
               {
                   Lines = new Dictionary<TKey, SparklineView?>
                           {
                               [key] = new SparklineView
                                       {
                                           Path = "M0 10L80 10",
                                           Direction = TrendDirection.Stable
                                       }
                           },
                   Samples = new Dictionary<TKey, IReadOnlyList<SparklineSample>>
                             {
                                 [key] = samples
                             },
                   IsFailed = false
               };
    }

    /// <summary>
    /// Describes the samples of each key, keys in the order of their text.
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <typeparam name="TSamples">The type of the samples of a key</typeparam>
    /// <param name="samples">The samples per key</param>
    /// <returns>One line per key</returns>
    private static List<string> DescribeSamples<TKey, TSamples>(IReadOnlyDictionary<TKey, TSamples> samples)
        where TKey : notnull
        where TSamples : IEnumerable<SparklineSample>
    {
        return [
                   .. samples.Select(pair => $"{KeyText(pair.Key)}: {string.Join(' ', pair.Value.Select(sample => $"{Time(sample.Time)}/{(sample.IsHourly ? "hourly" : "tick")}/{Value(sample.Value)}"))}")
                             .Order(StringComparer.Ordinal)
               ];
    }

    /// <summary>
    /// Describes the line the builder draws of the samples of each key, keys in the order of their text.
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <param name="samples">The samples per key</param>
    /// <param name="range">The range of the lines</param>
    /// <param name="decimals">The decimals of the direction</param>
    /// <returns>One line per key</returns>
    private static List<string> DescribeLines<TKey>(Dictionary<TKey, List<SparklineSample>> samples, TrendRange range, int decimals)
        where TKey : notnull
    {
        return DescribeLines(samples.ToDictionary(pair => pair.Key, pair => SparklineBuilder.Build(pair.Value, range, decimals)));
    }

    /// <summary>
    /// Describes the line of each key, keys in the order of their text.
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <param name="lines">The line per key</param>
    /// <returns>One line per key</returns>
    private static List<string> DescribeLines<TKey>(IReadOnlyDictionary<TKey, SparklineView?> lines)
        where TKey : notnull
    {
        return [
                   .. lines.Select(pair => $"{KeyText(pair.Key)}: {(pair.Value is { } line ? $"{line.Direction} {line.Path}" : "none")}")
                           .Order(StringComparer.Ordinal)
               ];
    }

    /// <summary>
    /// Gives the text of a key.
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <param name="key">The key</param>
    /// <returns>The text</returns>
    private static string KeyText<TKey>(TKey key)
        where TKey : notnull
    {
        return Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Formats a time in UTC, round-trip.
    /// </summary>
    /// <param name="time">The time</param>
    /// <returns>The text</returns>
    private static string Time(DateTimeOffset time)
    {
        return time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a value, round-trip, or <c>null</c> for none.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string Value(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";
    }

    /// <summary>
    /// Formats a float value as the double it is widened to, or <c>null</c> for none.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string Value(float? value)
    {
        return Value(value is { } single ? (double?)single : null);
    }

    /// <summary>
    /// Runs fold steps of the fixture's profile at a time until one reports anything but done.
    /// </summary>
    /// <param name="fixture">The fixture</param>
    /// <param name="now">The clock time of the steps</param>
    /// <returns>A task that completes when nothing is left to fold</returns>
    private async Task FoldAllAsync(TrendFixture fixture, DateTimeOffset now)
    {
        var steps = 0;

        while (steps < 1000 && (await fixture.History.FoldNextTickAsync(TrendFixture.ProfileId, now, TestContext.CancellationToken)).Outcome == HistoryOutcome.Done)
        {
            steps++;
        }

        Assert.AreEqual(25, steps, "The ticks before 00:00 UTC should be folded, one per step");
    }

    #endregion // Methods
}