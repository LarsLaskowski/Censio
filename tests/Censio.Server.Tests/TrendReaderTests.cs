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
            var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, [_island], good, fixture.LatestTick, TestContext.CancellationToken);

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
        var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, islands, good, fixture.LatestTick, TestContext.CancellationToken);

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
            var read = await fixture.Reader.ReadFiguresAsync(TrendFixture.ProfileId, island, fixture.LatestTick, TestContext.CancellationToken);

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
        var read = await fixture.Reader.ReadGoodAsync(TrendFixture.ProfileId, [_island], good, fixture.LatestTick, TestContext.CancellationToken);

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
        var first = reader.ReadGoodAsync(profileId, [new IslandKey(3245, 1, 0), new IslandKey(3245, 2, 1)], 2077, _latest, token);

        await fake.WaitForCallsAsync(1, wait, token);

        var second = reader.ReadFiguresAsync(profileId, new IslandKey(3245, 1, 0), _latest, token);

        await Task.Delay(NegativeWaitMilliseconds, token);

        var startedWhileFirstHeld = fake.CallCount;

        fake.Release();
        await Task.WhenAll(first, second).WaitAsync(wait, token);

        // Assert
        var range = TrendRange.Before(_latest);

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

        var running = reader.ReadGoodAsync(Guid.NewGuid(), [new IslandKey(1, 1, 0)], 1, _latest, runningPage.Token);

        await fake.WaitForCallsAsync(1, wait, token);

        var waiting = reader.ReadFiguresAsync(Guid.NewGuid(), new IslandKey(1, 1, 0), _latest, waitingPage.Token);

        // Act
        await waitingPage.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting, "A waiting read should end by cancellation");
        await runningPage.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => running, "A running read should end by cancellation");

        fake.Release();

        var next = await reader.ReadFiguresAsync(Guid.NewGuid(), new IslandKey(1, 1, 0), _latest, token).WaitAsync(wait, token);

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
        var read = await reader.ReadGoodAsync(Guid.NewGuid(), [new IslandKey(3245, 33, 1)], 2077, _latest, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(read.IsFailed, "A failed read should be reported as failed");
        Assert.IsNull(read.View, "A failed read should give no view");
        Assert.HasCount(1, provider.Logger.Entries, "A failed read should log once");
        Assert.AreEqual(LogLevel.Warning, provider.Logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual(FailedReadMessage, provider.Logger.Entries[0].Message, "The entry should carry the text and no argument");
        Assert.AreSame(failure, provider.Logger.Entries[0].Exception, "The entry should carry the exception");
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