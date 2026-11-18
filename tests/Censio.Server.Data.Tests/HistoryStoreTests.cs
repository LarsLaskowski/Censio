using System.Globalization;
using System.Text;

using Censio.Contracts;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Data.Tests;

/// <summary>
/// Tests for <see cref="HistoryStore" />, against real SQLite in a file of a temporary directory.
/// </summary>
[TestClass]
public sealed class HistoryStoreTests
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// The island name of the ticks.
    /// </summary>
    private const string IslandName = "Harbour island";

    /// <summary>
    /// The key number of the island the series are read for.
    /// </summary>
    private const int IslandKey = 1;

    /// <summary>
    /// The key number of another island.
    /// </summary>
    private const int OtherIslandKey = 2;

    /// <summary>
    /// The product GUID of the good the series are read for.
    /// </summary>
    private const int ProductGuid = 1010;

    /// <summary>
    /// The product GUID of a third good.
    /// </summary>
    private const int ThirdProductGuid = 1012;

    /// <summary>
    /// The product GUID of another good.
    /// </summary>
    private const int OtherProductGuid = 1011;

    /// <summary>
    /// The seconds of a minute.
    /// </summary>
    private const long Minute = 60;

    /// <summary>
    /// The seconds of an hour.
    /// </summary>
    private const long Hour = 3600;

    /// <summary>
    /// The most steps a helper runs until a store reports nothing to do.
    /// </summary>
    private const int MaxSteps = 10_000;

    /// <summary>
    /// The text of the trigger's error that makes a fold step fail.
    /// </summary>
    private const string FailingFoldMessage = "test fold failure";

    /// <summary>
    /// The statement that makes every insert into the folded tick identities fail, as a full disk would in a fold step.
    /// </summary>
    private const string CreateFailingFoldTriggerSql = "CREATE TRIGGER \"FailFoldedTicks\" BEFORE INSERT ON \"FoldedTicks\" BEGIN SELECT RAISE(ABORT, '" + FailingFoldMessage + "'); END;";

    /// <summary>
    /// The statement that makes every delete of a tick fail, after the tick's islands, entries and pairs were removed
    /// in the same sweep step.
    /// </summary>
    private const string CreateFailingSweepTriggerSql = "CREATE TRIGGER \"FailTickDelete\" BEFORE DELETE ON \"Ticks\" BEGIN SELECT RAISE(ABORT, '" + FailingFoldMessage + "'); END;";

    /// <summary>
    /// The statement that counts the rows of every table a profile's ticks, hourly points and identity live in, in the
    /// order Ticks, TickIslands, TickEntries, TickMapPairs, HourlyPoints, HourlyIslands, HourlyEntries, HourlyMapPairs,
    /// FoldedTicks, DeletedProfiles, Profiles, Islands.
    /// </summary>
    private const string CountRowsSql = "SELECT (SELECT count(*) FROM \"Ticks\") || ' ' || (SELECT count(*) FROM \"TickIslands\") || ' ' || (SELECT count(*) FROM \"TickEntries\") || ' ' || (SELECT count(*) FROM \"TickMapPairs\")"
                                        + " || ' ' || (SELECT count(*) FROM \"HourlyPoints\") || ' ' || (SELECT count(*) FROM \"HourlyIslands\") || ' ' || (SELECT count(*) FROM \"HourlyEntries\") || ' ' || (SELECT count(*) FROM \"HourlyMapPairs\")"
                                        + " || ' ' || (SELECT count(*) FROM \"FoldedTicks\") || ' ' || (SELECT count(*) FROM \"DeletedProfiles\") || ' ' || (SELECT count(*) FROM \"Profiles\") || ' ' || (SELECT count(*) FROM \"Islands\");";

    /// <summary>
    /// The statement a held read runs in its open transaction, so it keeps the snapshot of that moment.
    /// </summary>
    private const string HoldReadSql = "BEGIN; SELECT count(*) FROM \"Ticks\";";

    /// <summary>
    /// The first name of the profile that is deleted.
    /// </summary>
    private const string DeletedProfileName = "Deleted profile 4e1f";

    /// <summary>
    /// The later name of the profile that is deleted.
    /// </summary>
    private const string DeletedProfileRename = "Deleted profile renamed 8a2c";

    /// <summary>
    /// The first island name of the profile that is deleted.
    /// </summary>
    private const string DeletedIslandName = "Deleted island 51b7";

    /// <summary>
    /// The later island name of the profile that is deleted.
    /// </summary>
    private const string DeletedIslandRename = "Deleted island renamed 0c93";

    /// <summary>
    /// The name of the profile that is kept.
    /// </summary>
    private const string KeptProfileName = "Kept profile 7d20";

    /// <summary>
    /// The island name of the profile that is kept.
    /// </summary>
    private const string KeptIslandName = "Kept island 3f6e";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The UTC hour most ticks of the tests are received in: <c>H</c>.
    /// </summary>
    private static readonly DateTimeOffset _hour = new(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The first clock time at which the hour <c>H</c> lies fully 7 days back: <c>H + 1 h + 7 d</c>.
    /// </summary>
    private static readonly DateTimeOffset _hourFoldable = _hour + TimeSpan.FromHours(1) + TimeSpan.FromDays(7);

    /// <summary>
    /// The longest the test waits for queued steps to finish once the gate is released.
    /// </summary>
    private static readonly TimeSpan _stepBound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The longest the test waits to see that a task that should wait has not completed.
    /// </summary>
    private static readonly TimeSpan _waitBound = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// The plain floats of an entry, in the order of <see cref="HistoryPoint" />: Generation, Consumption, Delta,
    /// PerfectGeneration, PerfectConsumption, Income, SummedProductivity, AverageProductivity.
    /// </summary>
    private static readonly float?[] _plainFloats = [1.5f, 2.5f, -1f, 1.75f, 2.25f, 12.5f, 2.5f, 0.75f];

    /// <summary>
    /// The plain integers of an entry: BuildingCount, Maintenance, Profit.
    /// </summary>
    private static readonly int[] _plainIntegers = [3, 40, -27];

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    /// <summary>
    /// The server's clock.
    /// </summary>
    private readonly FakeTimeProvider _clock = new(_hourFoldable);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryStoreTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public HistoryStoreTests(TestContext testContext)
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
    /// Disposes the opened databases and deletes the temporary directories of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _databases.Clear();
        SqliteConnection.ClearAllPools();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// At the clock <c>H + 1 h + 7 d</c>, when the end of the hour <c>H</c> lies exactly 7 days back, a fold step folds
    /// the tick of <c>H</c> into its hourly point: the tick is gone and the series gives the hourly point.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task HistoryStoreFoldNextTickHourOlderThanSevenDaysFoldsIntoHourlyPoint()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var entry = PlainEntry(ProductGuid);
        var old = Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, entry));
        var latest = LatestTick(profileId);

        await StoreAsync(store, old, latest);
        _clock.SetUtcNow(_hour + TimeSpan.FromHours(1) + HistoryStore.FullResolution);

        // Act
        var result = await history.FoldNextTickAsync(profileId, _clock.GetUtcNow(), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The tick of an hour ending exactly 7 days back should be folded");
        Assert.AreEqual(_hour, result.Hour, "The step should name the hour it folded into");
        Assert.IsNull(await store.ReadTickAsync(old.Id, TestContext.CancellationToken), "The folded tick should no longer be stored as a tick");
        Assert.AreSequenceEqual(new[] { HourlyHead(_hour, 1, 1_000, 1_000, IslandName) + EntryBody(entry), TickLine(latest, IslandName, latest.Islands[0].Entries[0]) },
                                await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(3)),
                                "The series should give the hourly point of the folded hour and the younger tick");
    }

    /// <summary>
    /// One tick before <c>H + 1 h + 7 d</c>, when the end of the hour <c>H</c> lies less than 7 days back, a fold step
    /// has nothing to do and the tick stays.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task HistoryStoreFoldNextTickHourEndingWithinSevenDaysKeepsTick()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var old = Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, PlainEntry(ProductGuid)));
        var latest = LatestTick(profileId);

        await StoreAsync(store, old, latest);
        _clock.AdjustTime(_hourFoldable - TimeSpan.FromTicks(1));

        // Act
        var result = await history.FoldNextTickAsync(profileId, _clock.GetUtcNow(), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.NothingToDo, result.Outcome, "No tick of an hour ending less than 7 days back should be folded");
        Assert.IsNotNull(await store.ReadTickAsync(old.Id, TestContext.CancellationToken), "The tick should stay stored");
        Assert.AreSequenceEqual(new[] { TickLine(old, IslandName, old.Islands[0].Entries[0]), TickLine(latest, IslandName, latest.Islands[0].Entries[0]) },
                                await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(3)),
                                "The series should give both ticks unchanged");
    }

    /// <summary>
    /// The hour of a profile's latest tick is never folded, however old, also when another profile has a later tick.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task HistoryStoreFoldNextTickLatestHourOfProfileKeepsTicks()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var first = Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, PlainEntry(ProductGuid)));
        var second = Tick(profileId, 20 * Minute, 2_000, Island(IslandKey, IslandName, PlainEntry(ProductGuid)));

        await StoreAsync(store, first, second, LatestTick(Guid.NewGuid()));
        _clock.SetUtcNow(_hour.AddDays(30));

        // Act
        var result = await history.FoldNextTickAsync(profileId, _clock.GetUtcNow(), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.NothingToDo, result.Outcome, "No tick of the hour of the profile's latest tick should be folded");
        Assert.AreSequenceEqual(new[] { TickLine(first, IslandName, first.Islands[0].Entries[0]), TickLine(second, IslandName, second.Islands[0].Entries[0]) },
                                await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1)),
                                "Both ticks of the latest hour should stay");
    }

    /// <summary>
    /// Folding the three ticks of an hour, each with two islands, writes one hourly point, one row per island, one per
    /// island and good and one per map GUID of the hour, and one identity per folded tick; the younger tick stays.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task HistoryStoreFoldNextTickWritesOneRowPerIslandGoodAndMapGuid()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         TwoIslandTick(profileId, 10 * Minute, Map(100, 1)),
                         TwoIslandTick(profileId, 20 * Minute, Map(100, 2, 101, 3)),
                         TwoIslandTick(profileId, 30 * Minute, Map(102, 4)),
                         LatestTick(profileId));

        // Act
        var folded = await FoldAllAsync(history, profileId);

        // Assert
        Assert.AreEqual(3, folded, "The three ticks of the old hour should be folded, one per step");
        Assert.AreEqual("1 1 1 2 1 2 3 10 3 0 1 2", CountRows(databaseFile), "The hour should hold one point, two islands, three goods, ten map GUIDs and three identities; the younger tick should stay");
    }

    /// <summary>
    /// An hourly point gives each mean as a fractional number over the ticks that carried the value: floats with a
    /// tick without value, and integers whose means are not whole.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadSeriesHourlyPointGivesMeansOverCarryingTicks()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 100, Island(IslandKey, IslandName, Entry(ProductGuid, [1f, 3f, null, 0.25f, 1f, 10f, 0.5f, 1f], [1, 10, -1], Map(100, 6), Map(200, 2)))),
                         Tick(profileId, 20 * Minute, 200, Island(IslandKey, IslandName, Entry(ProductGuid, [2f, 4f, null, 0.25f, 1f, 20f, 1f, 1f], [2, 20, -2], Map(100, 6), Map(200, 2)))),
                         Tick(profileId, 30 * Minute, 300, Island(IslandKey, IslandName, Entry(ProductGuid, [null, 5f, 0.5f, 0.25f, 4f, 30f, null, 1f], [2, 31, -4], Map(100, 6), Map(200, 2)))),
                         LatestTick(profileId));
        await FoldAllAsync(history, profileId);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        var expected = HourlyHead(_hour, 3, 100, 300, IslandName) + Body([1.5, 4, 0.5, 0.25, 2, 20, 0.75, 1, 5d / 3, 61d / 3, -7d / 3], [(100, 6)], [(200, 2)]);

        Assert.AreSequenceEqual(new[] { expected }, series, "Each mean should be the sum over the number of ticks that carried the value, integers unrounded");
    }

    /// <summary>
    /// A value no tick of the hour carried has no mean in the hourly point, never 0.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadSeriesValueNoTickCarriedHasNoMean()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        float?[] floats = [null, 3f, 1f, 1f, 1f, null, 1f, 1f];

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 100, Island(IslandKey, IslandName, Entry(ProductGuid, floats, [1, 1, 1], Map(100, 1), Map(200, 1)))),
                         Tick(profileId, 20 * Minute, 200, Island(IslandKey, IslandName, Entry(ProductGuid, floats, [1, 1, 1], Map(100, 1), Map(200, 1)))),
                         LatestTick(profileId));
        await FoldAllAsync(history, profileId);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        var expected = HourlyHead(_hour, 2, 100, 200, IslandName) + Body([null, 3, 1, 1, 1, null, 1, 1, 1, 1, 1], [(100, 1)], [(200, 1)]);

        Assert.AreSequenceEqual(new[] { expected }, series, "Generation and Income, carried by no tick, should have no mean");
    }

    /// <summary>
    /// A workforce or building GUID absent from a carried entry counts as 0 in the mean of its amounts, which is taken
    /// over the ticks that carried the entry, not over a tick without the good.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadSeriesAbsentMapGuidCountsAsZero()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 100, Island(IslandKey, IslandName, Entry(ProductGuid, _plainFloats, _plainIntegers, Map(100, 6), Map(200, 2)))),
                         Tick(profileId, 20 * Minute, 200, Island(IslandKey, IslandName, Entry(ProductGuid, _plainFloats, _plainIntegers, Map(100, 3, 101, 9), Map(200, 4)))),
                         Tick(profileId, 30 * Minute, 300, Island(IslandKey, IslandName, Entry(ProductGuid, _plainFloats, _plainIntegers, Map(), Map(200, 6, 201, 3)))),
                         Tick(profileId, 40 * Minute, 400, Island(IslandKey, IslandName, PlainEntry(OtherProductGuid))),
                         LatestTick(profileId));
        await FoldAllAsync(history, profileId);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        var expected = HourlyHead(_hour, 4, 100, 400, IslandName) + Body(PlainValues(), [(100, 3), (101, 3)], [(200, 4), (201, 1)]);

        Assert.AreSequenceEqual(new[] { expected }, series, "An absent GUID should count as 0 over the three ticks that carried the entry");
    }

    /// <summary>
    /// An hourly point carries the hour's start, the smallest and largest game time of its ticks, their number and the
    /// island name of the hour's latest tick by receive time, though that tick was stored first.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadSeriesHourlyPointCarriesHourGameTimesCountAndLatestName()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 50 * Minute, 6_000, Island(IslandKey, "Latest name of the hour", PlainEntry(ProductGuid))),
                         Tick(profileId, 10 * Minute, 7_000, Island(IslandKey, "First name of the hour", PlainEntry(ProductGuid))),
                         Tick(profileId, 30 * Minute, 5_000, Island(IslandKey, "Middle name of the hour", PlainEntry(ProductGuid))),
                         LatestTick(profileId));
        await FoldAllAsync(history, profileId);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        var expected = HourlyHead(_hour, 3, 5_000, 7_000, "Latest name of the hour") + EntryBody(PlainEntry(ProductGuid));

        Assert.AreSequenceEqual(new[] { expected }, series, "The point should carry the hour start, the game time range, the tick count and the latest name");
    }

    /// <summary>
    /// A tick arriving for an already folded hour is folded into its hourly point at the next run: counts, integer sums
    /// and game times equal those of folding all ticks at once, and a float sum with cancelling terms differs from that
    /// at most by the rounding of the order of additions.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreFoldNextTickLateTickEqualsFoldingAllAtOnce()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var early = new[]
                    {
                        Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, Entry(ProductGuid, [float.MaxValue, 1f, 2f, 0.5f, 1f, 4f, 0.25f, 1f], [1, 7, -3], Map(100, 2), Map(200, 1)))),
                        Tick(profileId, 20 * Minute, 2_000, Island(IslandKey, IslandName, Entry(ProductGuid, [1.5f, 2f, null, 0.5f, 3f, 8f, 0.25f, 2f], [2, 9, -5], Map(100, 4, 101, 1), Map(200, 3)))),
                        Tick(profileId, 40 * Minute, 4_000, Island(IslandKey, "Latest name", Entry(ProductGuid, [-float.MaxValue, 3f, 4f, 0.5f, null, 16f, 0.75f, 3f], [4, 11, -7], Map(100, 6), Map()))),
                        LatestTick(profileId)
                    };
        var late = Tick(profileId, 5 * Minute, 500, Island(IslandKey, "Name of the late tick", Entry(ProductGuid, [0.5f, 4f, 8f, 0.5f, 5f, 32f, 0.5f, 4f], [8, 13, -9], Map(102, 5), Map(201, 2))));
        var (lateStore, lateHistory, _) = await OpenAsync();
        var (allStore, allHistory, _) = await OpenAsync();

        await StoreAsync(lateStore, early);
        await FoldAllAsync(lateHistory, profileId);
        await lateStore.StoreTickAsync(late, TestContext.CancellationToken);
        await StoreAsync(allStore, [.. early, late]);
        await FoldAllAsync(allHistory, profileId);

        // Act
        var folded = await FoldAllAsync(lateHistory, profileId);

        // Assert
        var lateSeries = await lateHistory.ReadSeriesAsync(Query(profileId, _hour, _hour.AddHours(1)), TestContext.CancellationToken);
        var allSeries = await allHistory.ReadSeriesAsync(Query(profileId, _hour, _hour.AddHours(1)), TestContext.CancellationToken);

        // n × 2^-52 × Σ|x| over the four Generation terms, divided by their count of 4 for the mean
        var sumBound = 4 * Math.Pow(2, -52) * ((2 * (double)float.MaxValue) + 1.5 + 0.5);
        var meanBound = sumBound / 4;

        Assert.AreEqual(1, folded, "The next run should fold the late tick");
        Assert.HasCount(1, allSeries, "Folding all at once should give one hourly point, or the comparison below would prove nothing");
        Assert.AreSequenceEqual(allSeries.Select(DescribeWithoutGeneration).ToList(), lateSeries.Select(DescribeWithoutGeneration).ToList(), "Counts, integer sums, game times, the name and the exact float sums should equal folding all at once");
        Assert.AreEqual(allSeries[0].Generation ?? double.NaN, lateSeries[0].Generation ?? double.NaN, meanBound, "The cancelling float sum should differ at most by the rounding of the order of additions");
    }

    /// <summary>
    /// A tick whose identity was folded is reported as already stored and changes nothing: not the hourly point and not
    /// the profiles.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreStoreTickFoldedIdentityReportsAlreadyStored()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var entry = PlainEntry(ProductGuid);
        var folded = Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, entry));
        var resend = new ContractTick
                     {
                         Id = folded.Id,
                         ProfileId = Guid.NewGuid(),
                         ReceiveTime = _hour.AddMinutes(20),
                         GameTime = 2_000,
                         Islands = [Island(IslandKey, "Name of the resend", PlainEntry(OtherProductGuid))]
                     };

        await StoreAsync(store, folded, LatestTick(profileId));
        await FoldAllAsync(history, profileId);

        // Act
        var result = await store.StoreTickAsync(resend, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.AlreadyStored, result.Outcome, "A folded tick identity should be reported as already stored");
        Assert.AreSequenceEqual(new[] { HourlyHead(_hour, 1, 1_000, 1_000, IslandName) + EntryBody(entry) }, await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1)), "The hourly point should stay as it was");
        Assert.AreSequenceEqual(new[] { profileId }, (await store.ListProfilesAsync(TestContext.CancellationToken)).Select(profile => profile.Id).ToList(), "The resend's profile should not be created");
    }

    /// <summary>
    /// Unfolded ticks give one point each, oldest first and, of equal receive times, in storing order, each with its
    /// receive time, game time and every value of the good; another profile's tick is not a point.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task HistoryStoreReadSeriesTicksGiveOnePointPerTickOldestFirst()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var at30 = Tick(profileId, 30 * Minute, 30, Island(IslandKey, IslandName, Entry(ProductGuid, [3f, 1f, 1f, 1f, 1f, 1f, 1f, 1f], [3, 1, 1], Map(100, 3), Map(200, 3))));
        var at20 = Tick(profileId, 20 * Minute, 21, Island(IslandKey, IslandName, Entry(ProductGuid, [2f, 1f, 1f, 1f, 1f, 1f, 1f, 1f], [2, 1, 1], Map(100, 2), Map(200, 2))));
        var at10 = Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, Entry(ProductGuid, [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f], [1, 1, 1], Map(100, 1), Map(200, 1))));
        var at20Later = Tick(profileId, 20 * Minute, 22, Island(IslandKey, IslandName, PlainEntry(ProductGuid)));

        await StoreAsync(store, at30, at20, at10, Tick(Guid.NewGuid(), 15 * Minute, 15, Island(IslandKey, IslandName, PlainEntry(ProductGuid))), at20Later);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        var expected = new[] { at10, at20, at20Later, at30 }.Select(tick => TickLine(tick, IslandName, tick.Islands[0].Entries[0])).ToList();

        Assert.AreSequenceEqual(expected, series, "Each tick should be one point, oldest first, equal times in storing order");
    }

    /// <summary>
    /// A tick without the good on the island, or without the island, is a point without values (and without a name for
    /// a missing island), never zeros; a missing float is no value.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task HistoryStoreReadSeriesMissingIslandOrGoodGivesPointWithoutValues()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var withGood = Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, PlainEntry(ProductGuid)));
        var withoutGood = Tick(profileId, 20 * Minute, 20, Island(IslandKey, IslandName, PlainEntry(OtherProductGuid)));
        var withoutIsland = Tick(profileId, 30 * Minute, 30, Island(OtherIslandKey, "Other island", PlainEntry(ProductGuid)));
        var withoutFloat = Tick(profileId, 40 * Minute, 40, Island(IslandKey, IslandName, Entry(ProductGuid, [null, 2.5f, -1f, 1.75f, 2.25f, 12.5f, 2.5f, 0.75f], _plainIntegers, Map(100, 7), Map(200, 3))));

        await StoreAsync(store, withGood, withoutGood, withoutIsland, withoutFloat);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        var expected = new[]
                       {
                           TickLine(withGood, IslandName, withGood.Islands[0].Entries[0]),
                           TickLine(withoutGood, IslandName, null),
                           TickLine(withoutIsland, null, null),
                           TickLine(withoutFloat, IslandName, withoutFloat.Islands[0].Entries[0])
                       };

        Assert.AreSequenceEqual(expected, series, "A missing island, good or value should be a point without it, never a zero");
    }

    /// <summary>
    /// A range spanning folded and unfolded hours gives the hourly points of the folded hours and the ticks of the rest
    /// in one time order, and no point for an hour without ticks.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task HistoryStoreReadSeriesSpanningFoldedHoursGivesPointsInTimeOrder()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var unfolded = new[] { PlainTick(profileId, Hour + (30 * Minute), 50), PlainTick(profileId, (2 * Hour) + (10 * Minute), 60), PlainTick(profileId, (2 * Hour) + (20 * Minute), 70) };

        await StoreAsync(store, PlainTick(profileId, -Hour + (10 * Minute), 10), PlainTick(profileId, -Hour + (20 * Minute), 20), PlainTick(profileId, 10 * Minute, 30), PlainTick(profileId, 20 * Minute, 40));
        await StoreAsync(store, unfolded);
        await FoldAllAsync(history, profileId);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour.AddHours(-2), _hour.AddHours(3));

        // Assert
        var body = EntryBody(PlainEntry(ProductGuid));
        var expected = new List<string>
                       {
                           HourlyHead(_hour.AddHours(-1), 2, 10, 20, IslandName) + body,
                           HourlyHead(_hour, 2, 30, 40, IslandName) + body
                       };

        expected.AddRange(unfolded.Select(tick => TickLine(tick, IslandName, tick.Islands[0].Entries[0])));

        Assert.AreSequenceEqual(expected, series, "The folded hours should be hourly points, the rest ticks, in one time order");
    }

    /// <summary>
    /// A tick arriving for an already folded hour is returned beside the hourly point until it is folded, after the
    /// hourly point on an equal time; the folded ticks are not returned beside it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task HistoryStoreReadSeriesLateTickReturnedBesideHourlyPointUntilFolded()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var latest = LatestTick(profileId);
        var late = PlainTick(profileId, 0, 5);

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), PlainTick(profileId, 20 * Minute, 20), latest);
        await FoldAllAsync(history, profileId);
        await store.StoreTickAsync(late, TestContext.CancellationToken);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(3));

        // Assert
        var expected = new[]
                       {
                           HourlyHead(_hour, 2, 10, 20, IslandName) + EntryBody(PlainEntry(ProductGuid)),
                           TickLine(late, IslandName, late.Islands[0].Entries[0]),
                           TickLine(latest, IslandName, latest.Islands[0].Entries[0])
                       };

        Assert.AreSequenceEqual(expected, series, "The late tick should be returned beside the hourly point, the folded ticks not");
    }

    /// <summary>
    /// A range includes its start and excludes its end, for hourly points (by their hour start) and ticks alike.
    /// </summary>
    /// <param name="startSeconds">The range start in seconds after the hour <c>H</c></param>
    /// <param name="endSeconds">The range end in seconds after the hour <c>H</c></param>
    /// <param name="expected">The expected points, kind and time</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0L, Hour, new[] { "hourly 2026-06-10T10:00:00.0000000Z" })]
    [DataRow(3 * Hour, (3 * Hour) + 20, new[] { "tick 2026-06-10T13:00:00.0000000Z", "tick 2026-06-10T13:00:10.0000000Z" })]
    public async Task HistoryStoreReadSeriesRangeEndIsExclusive(long startSeconds, long endSeconds, string[] expected)
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         PlainTick(profileId, -Hour + (10 * Minute), 1),
                         PlainTick(profileId, 10 * Minute, 2),
                         PlainTick(profileId, Hour + (10 * Minute), 3),
                         PlainTick(profileId, (3 * Hour) - 1, 4),
                         PlainTick(profileId, 3 * Hour, 5),
                         PlainTick(profileId, (3 * Hour) + 10, 6),
                         PlainTick(profileId, (3 * Hour) + 20, 7));
        _clock.SetUtcNow(_hour.AddHours(2) + HistoryStore.FullResolution);
        await FoldAllAsync(history, profileId);

        // Act
        var series = await history.ReadSeriesAsync(Query(profileId, _hour.AddSeconds(startSeconds), _hour.AddSeconds(endSeconds)), TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(expected, series.Select(point => $"{(point.IsHourly ? "hourly" : "tick")} {FormatTime(point.Time)}").ToList(), "The range should include its start and exclude its end");
    }

    /// <summary>
    /// A tick that arrives for an hour before an already folded hour is returned before that hourly point, so the
    /// series stays in time order.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task HistoryStoreReadSeriesLateTickOfEarlierHourComesBeforeHourlyPoint()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var latest = LatestTick(profileId);
        var late = PlainTick(profileId, -Hour + (10 * Minute), 5);

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), PlainTick(profileId, 20 * Minute, 20), latest);
        await FoldAllAsync(history, profileId);
        await store.StoreTickAsync(late, TestContext.CancellationToken);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour.AddHours(-1), _hour.AddHours(3));

        // Assert
        var expected = new[]
                       {
                           TickLine(late, IslandName, late.Islands[0].Entries[0]),
                           HourlyHead(_hour, 2, 10, 20, IslandName) + EntryBody(PlainEntry(ProductGuid)),
                           TickLine(latest, IslandName, latest.Islands[0].Entries[0])
                       };

        Assert.AreSequenceEqual(expected, series, "The late tick of the earlier hour should come before the hourly point");
    }

    /// <summary>
    /// An hourly point of an hour whose ticks lacked the island, or held the island without the good, is a point
    /// without values (and without a name for the missing island), never zeros.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task HistoryStoreReadSeriesHourlyPointWithoutIslandOrGoodHasNoValues()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, -Hour + (10 * Minute), 10, Island(OtherIslandKey, "Other island", PlainEntry(ProductGuid))),
                         Tick(profileId, -Hour + (20 * Minute), 20, Island(OtherIslandKey, "Other island", PlainEntry(ProductGuid))),
                         Tick(profileId, 10 * Minute, 30, Island(IslandKey, IslandName, PlainEntry(OtherProductGuid))),
                         Tick(profileId, 20 * Minute, 40, Island(IslandKey, IslandName, PlainEntry(OtherProductGuid))),
                         LatestTick(profileId));
        await FoldAllAsync(history, profileId);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour.AddHours(-1), _hour.AddHours(1));

        // Assert
        var expected = new[]
                       {
                           Head(true, _hour.AddHours(-1), 2, 10, 20, null) + EntryBody(null),
                           HourlyHead(_hour, 2, 30, 40, IslandName) + EntryBody(null)
                       };

        Assert.AreSequenceEqual(expected, series, "An hourly point without the island or the good should have no values, never zeros");
    }

    /// <summary>
    /// A fold step for a profile without ticks reports nothing to do and leaves another profile's foldable ticks alone.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task HistoryStoreFoldNextTickProfileWithoutTicksReportsNothingToDo()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var otherId = Guid.NewGuid();

        await StoreAsync(store, PlainTick(otherId, 10 * Minute, 10), LatestTick(otherId));

        // Act
        var result = await history.FoldNextTickAsync(Guid.NewGuid(), _clock.GetUtcNow(), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.NothingToDo, result.Outcome, "A profile without ticks should have nothing to fold");
        Assert.IsNull(result.Failure, "Nothing to do should carry no exception");
        Assert.IsNull(result.Hour, "Nothing to do should name no folded hour");
        Assert.AreEqual("2 2 2 4 0 0 0 0 0 0 1 1", CountRows(databaseFile), "The other profile's ticks should stay unfolded");
    }

    /// <summary>
    /// A fold step that fails changes nothing: the tick stays whole and no hourly row is written.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task HistoryStoreFoldNextTickFailureLeavesTickWhole()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var old = PlainTick(profileId, 10 * Minute, 10);

        await StoreAsync(store, old, LatestTick(profileId));
        CreateFailingFoldTrigger(databaseFile);

        // Act
        var result = await history.FoldNextTickAsync(profileId, _clock.GetUtcNow(), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Failed, result.Outcome, "A failing fold step should be reported as failed");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.AreEqual("2 2 2 4 0 0 0 0 0 0 1 1", CountRows(databaseFile), "The tick should stay whole and no hourly row or identity should be written");
        Assert.AreSequenceEqual(new[] { TickLine(old, IslandName, old.Islands[0].Entries[0]) }, await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1)), "The tick should still read as a tick");
    }

    /// <summary>
    /// Sweep steps after a deletion remove every row of the deleted profile: its ticks with their islands, entries and
    /// pairs, its hourly points with theirs, its tick identities and the tombstone; another profile's rows stay.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task HistoryStoreSweepNextRemovesEveryRowOfDeletedProfile()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), PlainTick(profileId, 20 * Minute, 20), LatestTick(profileId), LatestTick(Guid.NewGuid()));
        await FoldAllAsync(history, profileId);
        await store.DeleteProfileAsync(profileId, TestContext.CancellationToken);

        // Act
        var steps = await SweepAllAsync(history);

        // Assert
        Assert.IsGreaterThan(0, steps, "At least one sweep step should have removed rows");
        Assert.AreEqual(HistoryOutcome.NothingToDo, (await history.SweepNextAsync(TestContext.CancellationToken)).Outcome, "Nothing should be left to sweep");
        Assert.AreEqual("1 1 1 2 0 0 0 0 0 0 1 1", CountRows(databaseFile), "Only the other profile's tick, island and profile should remain");
    }

    /// <summary>
    /// A profile that a tick creates again after its deletion keeps that tick through the sweep, and its series holds
    /// only the new tick.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task HistoryStoreSweepNextKeepsRowsOfRecreatedProfile()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var recreating = PlainTick(profileId, 3 * Hour, 99);

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), LatestTick(profileId));
        await FoldAllAsync(history, profileId);
        await store.DeleteProfileAsync(profileId, TestContext.CancellationToken);
        await store.StoreTickAsync(recreating, TestContext.CancellationToken);

        // Act
        await SweepAllAsync(history);

        // Assert
        Assert.AreEqual("1 1 1 2 0 0 0 0 0 0 1 1", CountRows(databaseFile), "Only the new tick, its island and the recreated profile should remain");
        Assert.AreEqual(recreating.Id, (await store.ReadLatestTickAsync(profileId, TestContext.CancellationToken))?.Id, "The new tick should be the profile's latest");
        Assert.AreSequenceEqual(new[] { TickLine(recreating, IslandName, recreating.Islands[0].Entries[0]) }, await ReadSeriesAsync(history, profileId, _hour.AddDays(-1), _hour.AddDays(1)), "The series should hold only the new tick");
    }

    /// <summary>
    /// A sweep step that fails removes nothing: the deleted profile's tick rows, already removed children included,
    /// stay as they were, and the failure is reported with its exception.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task HistoryStoreSweepNextFailureRemovesNothing()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), LatestTick(profileId));
        await store.DeleteProfileAsync(profileId, TestContext.CancellationToken);
        CreateFailingSweepTrigger(databaseFile);

        // Act
        var result = await history.SweepNextAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Failed, result.Outcome, "A failing sweep step should be reported as failed");
        Assert.IsInstanceOfType<SqliteException>(result.Failure, "The SQLite exception behind the failure should be attached");
        Assert.AreEqual("2 2 2 4 0 0 0 0 0 1 0 0", CountRows(databaseFile), "Every tick row of the deleted profile and its tombstone should remain");
    }

    /// <summary>
    /// The clean-up of an in-memory database, which has no file and no write-ahead log, reports done.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task HistoryStoreClearDeletedInMemoryDatabaseReportsDone()
    {
        // Arrange
        var result = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        var history = new HistoryStore(database);

        // Act
        var cleared = await history.ClearDeletedAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, cleared.Outcome, "The clean-up of an in-memory database should be done");
        Assert.IsNull(cleared.Failure, "The clean-up of an in-memory database should carry no exception");
    }

    /// <summary>
    /// A clean-up while a deleted profile's rows are not all swept yet reports nothing to do, without an exception and
    /// without a checkpoint, in a file and in an in-memory database alike.
    /// </summary>
    /// <param name="inMemory">Whether the database is in memory</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HistoryStoreClearDeletedUnsweptRowsReportsNothingToDo(bool inMemory)
    {
        // Arrange
        var (store, history) = await OpenStoresAsync(inMemory);

        var profileId = Guid.NewGuid();

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), LatestTick(profileId));
        await store.DeleteProfileAsync(profileId, TestContext.CancellationToken);

        // Act
        var result = await history.ClearDeletedAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.NothingToDo, result.Outcome, "A clean-up before the sweep removed every row of the deleted profile should report nothing to do");
        Assert.IsNull(result.Failure, "Nothing to do should carry no exception");
        Assert.AreEqual(1, _databases[^1].Gate.CurrentCount, "The write gate should be released");
    }

    /// <summary>
    /// A checkpoint SQLite refuses with an error, here because the kept connection is inside a read transaction,
    /// reports failed with the exception and releases the write gate. The kept connection's timeout is lowered to one
    /// second, so SQLite's retries of the locked checkpoint end quickly.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task HistoryStoreClearDeletedCheckpointErrorReportsFailedAndReleasesGate()
    {
        // Arrange
        var (_, history, _) = await OpenAsync();

        var database = _databases[^1];

        database.Connection.DefaultTimeout = 1;

        using var hold = database.Connection.CreateCommand();

        hold.CommandText = HoldReadSql;
        await hold.ExecuteScalarAsync(TestContext.CancellationToken);

        // Act
        HistoryResult result;

        try
        {
            result = await history.ClearDeletedAsync(TestContext.CancellationToken);
        }
        finally
        {
            using var rollback = database.Connection.CreateCommand();

            rollback.CommandText = "ROLLBACK;";
            await rollback.ExecuteNonQueryAsync(TestContext.CancellationToken);
        }

        // Assert
        Assert.AreEqual(HistoryOutcome.Failed, result.Outcome, "A checkpoint SQLite refuses should be reported as failed");
        Assert.IsInstanceOfType<SqliteException>(result.Failure, "The SQLite exception behind the failure should be attached");
        Assert.AreEqual(1, database.Gate.CurrentCount, "The write gate should be released after the failure");
    }

    /// <summary>
    /// Once the clean-up after a deletion is done, no profile or island name only the deleted profile held, renamed
    /// ones included, occurs in the bytes of the database file or its <c>-wal</c> and <c>-shm</c> files, while the
    /// kept profile's names still do.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task HistoryStoreClearDeletedLeavesNoNameInFiles()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;

        await store.StoreProfileAsync(profileId, DeletedProfileName, cancellationToken);
        await store.StoreProfileAsync(profileId, DeletedProfileRename, cancellationToken);
        await store.StoreProfileAsync(keptId, KeptProfileName, cancellationToken);
        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 10, Island(IslandKey, DeletedIslandName, PlainEntry(ProductGuid))),
                         Tick(profileId, 20 * Minute, 20, Island(IslandKey, DeletedIslandRename, PlainEntry(ProductGuid))),
                         Tick(profileId, 2 * Hour, 30, Island(IslandKey, DeletedIslandRename, PlainEntry(ProductGuid))),
                         Tick(keptId, 2 * Hour, 40, Island(IslandKey, KeptIslandName, PlainEntry(ProductGuid))));
        await FoldAllAsync(history, profileId);
        await store.DeleteProfileAsync(profileId, cancellationToken);
        await SweepAllAsync(history);

        // Act
        var result = await history.ClearDeletedAsync(cancellationToken);

        // Assert
        var found = FindNamesInFiles(databaseFile, DeletedProfileName, DeletedProfileRename, DeletedIslandName, DeletedIslandRename, KeptProfileName, KeptIslandName);

        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The clean-up should be done");
        Assert.AreSequenceEqual(new[] { KeptProfileName, KeptIslandName }, found, "Only the kept profile's names should occur in the database files");
    }

    /// <summary>
    /// A clean-up while a read holds a snapshot from before the deletion reports blocked, without an exception.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task HistoryStoreClearDeletedHeldReadReportsBlocked()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), LatestTick(profileId));

        using var reader = OpenRawConnection(databaseFile);
        using var hold = reader.CreateCommand();

        hold.CommandText = HoldReadSql;
        await hold.ExecuteScalarAsync(TestContext.CancellationToken);

        await store.DeleteProfileAsync(profileId, TestContext.CancellationToken);
        await SweepAllAsync(history);

        // Act
        var result = await history.ClearDeletedAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Blocked, result.Outcome, "A clean-up a held read blocks should report blocked");
        Assert.IsNull(result.Failure, "A blocked clean-up should carry no exception");
    }

    /// <summary>
    /// A tick stored while fold steps wait for the write gate runs before the next fold step: with the gate held, a
    /// fold step, a store of a tick older than the remaining one and a second fold step are queued; once released, the
    /// second step folds the stored tick, so the order was fold, store, fold.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task HistoryStoreFoldNextTickWaitingStoreRunsBetweenSteps()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var gate = _databases[^1].Gate;
        var profileId = Guid.NewGuid();
        var remaining = PlainTick(profileId, 20 * Minute, 20);
        var stored = PlainTick(profileId, 5 * Minute, 5);
        var now = _clock.GetUtcNow();
        var cancellationToken = TestContext.CancellationToken;

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), remaining, LatestTick(profileId));
        await gate.WaitAsync(cancellationToken);

        // Act
        Task<HistoryResult> firstFold;
        Task<ServerStoreResult> storing;
        Task<HistoryResult> secondFold;

        try
        {
            firstFold = history.FoldNextTickAsync(profileId, now, cancellationToken);
            storing = store.StoreTickAsync(stored, cancellationToken);
            secondFold = history.FoldNextTickAsync(profileId, now, cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        await Task.WhenAll(firstFold, storing, secondFold).WaitAsync(_stepBound, cancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, (await firstFold).Outcome, "The first fold step should fold");
        Assert.AreEqual(ServerStoreOutcome.Stored, (await storing).Outcome, "The waiting tick should be stored");
        Assert.AreEqual(HistoryOutcome.Done, (await secondFold).Outcome, "The second fold step should fold");
        Assert.IsNull(await store.ReadTickAsync(stored.Id, TestContext.CancellationToken), "The second step should have folded the stored tick, so the store ran before it");
        Assert.IsNotNull(await store.ReadTickAsync(remaining.Id, TestContext.CancellationToken), "The tick younger than the stored one should remain unfolded");
    }

    /// <summary>
    /// A tick with two entries of one good on one island is read by its first entry, the second entry's values and map
    /// GUIDs absent.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task HistoryStoreReadSeriesRepeatedGoodUsesFirstEntry()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var first = UniformEntry(ProductGuid, 1f, 2, Map(100, 4), Map(200, 2));
        var tick = RepeatedGoodTick(profileId, 10 * Minute, first, UniformEntry(ProductGuid, 9f, 50, Map(101, 7), Map(201, 5)));

        await StoreAsync(store, tick);

        // Act
        var series = await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1));

        // Assert
        Assert.AreSequenceEqual(new[] { TickLine(tick, IslandName, first) }, series, "The tick should be read by the first entry of the good");
    }

    /// <summary>
    /// Ticks with two entries of one good on one island are folded by their first entries, the second entries' values
    /// and map GUIDs absent from the hourly point.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task HistoryStoreFoldNextTickRepeatedGoodFoldsFirstEntry()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         RepeatedGoodTick(profileId, 10 * Minute, UniformEntry(ProductGuid, 1f, 2, Map(100, 4), Map(200, 2)), UniformEntry(ProductGuid, 9f, 50, Map(101, 7), Map(201, 5))),
                         RepeatedGoodTick(profileId, 20 * Minute, UniformEntry(ProductGuid, 3f, 4, Map(100, 8), Map(200, 6)), UniformEntry(ProductGuid, 11f, 70, Map(101, 9), Map(201, 3))),
                         LatestTick(profileId));

        // Act
        await FoldAllAsync(history, profileId);

        // Assert
        var expected = HourlyHead(_hour, 2, 10 * Minute, 20 * Minute, IslandName) + Body([2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3], [(100, 6)], [(200, 4)]);

        Assert.AreSequenceEqual(new[] { expected }, await ReadSeriesAsync(history, profileId, _hour, _hour.AddHours(1)), "The hourly point should hold the means of the first entries only");
    }

    /// <summary>
    /// A point of a good's trend has no value where no island of the set carries the good, for a tick and for an hourly
    /// point alike; where several islands carry it, the values are summed over them.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreGoodTrendPointWithoutCarrierHasNoValue()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, TrendEntry(OtherProductGuid, 9f, 9f, 9f, 1)), Island(OtherIslandKey, "Second island", TrendEntry(OtherProductGuid, 9f, 9f, 9f, 1))),
                         Tick(profileId, Hour + (10 * Minute), 100, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 1.5f, 2.5f, -1f, 1)), Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 2.25f, 0.5f, 0.25f, 1))),
                         Tick(profileId, Hour + (20 * Minute), 200, Island(IslandKey, IslandName, TrendEntry(OtherProductGuid, 9f, 9f, 9f, 1)), Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 4f, 1f, 3f, 1))),
                         Tick(profileId, Hour + (30 * Minute), 300, Island(IslandKey, IslandName, TrendEntry(OtherProductGuid, 9f, 9f, 9f, 1)), Island(OtherIslandKey, "Second island", TrendEntry(OtherProductGuid, 9f, 9f, 9f, 1))));
        await FoldAllAsync(history, profileId);

        var query = GoodQuery(profileId, [TrendIsland(IslandKey), TrendIsland(OtherIslandKey)], _hour, _hour.AddHours(2), 100);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour)} null null null",
                           $"tick {FormatTime(_hour.AddSeconds(Hour + (10 * Minute)))} 3.75 3 -0.75",
                           $"tick {FormatTime(_hour.AddSeconds(Hour + (20 * Minute)))} 4 1 3",
                           $"tick {FormatTime(_hour.AddSeconds(Hour + (30 * Minute)))} null null null"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeGoodPoint), "The islands that carry the good should be summed and a point no island carries should have no value");
        Assert.AreEqual(4, trend.TotalCount, "The range should hold the hourly point and the three ticks");
    }

    /// <summary>
    /// The figures of an hourly point follow the hourly rules: the buildings are the sum of the mean building counts
    /// rounded away from zero, the produced goods those with a mean above 0, the goods in deficit those with a rounded
    /// mean delta below 0 and at least one delta; a tick without the island or without entries has no value.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreFiguresTrendHourlyPointFollowsHourlyRules()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -1f, 1), TrendEntry(OtherProductGuid, 0f, 0f, 0.5f, 1), TrendEntry(ThirdProductGuid, 0f, 0f, null, 0))),
                         Tick(profileId, 20 * Minute, 20, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -1f, 2), TrendEntry(OtherProductGuid, 0f, 0f, -0.5f, 1), TrendEntry(ThirdProductGuid, 0f, 0f, null, 0))),
                         Tick(profileId, Hour + (10 * Minute), 30, Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 0f, 0f, 0f, 1))),
                         Tick(profileId, Hour + (20 * Minute), 40, Island(IslandKey, IslandName)));
        await FoldAllAsync(history, profileId);

        var query = FiguresQuery(profileId, TrendIsland(IslandKey), _hour, _hour.AddHours(2), 100);

        // Act
        var trend = await history.ReadFiguresTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour)} 3 2 1",
                           $"tick {FormatTime(_hour.AddSeconds(Hour + (10 * Minute)))} null null null",
                           $"tick {FormatTime(_hour.AddSeconds(Hour + (20 * Minute)))} null null null"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeFiguresPoint), "The hourly point should round the buildings' sum of means up from 2.5, count the goods with a mean above 0 and the goods with a mean delta below 0");
    }

    /// <summary>
    /// The deficit of a figures point rounds the delta to two decimals first: -0.004 is no deficit, -0.006 is, for a
    /// tick and for an hourly point.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreFiguresTrendDeficitRoundsDelta()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -0.004f, 1), TrendEntry(OtherProductGuid, 0f, 0f, -0.006f, 1), TrendEntry(ThirdProductGuid, 0f, 0f, 0.006f, 1))),
                         Tick(profileId, 20 * Minute, 20, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -0.004f, 1), TrendEntry(OtherProductGuid, 0f, 0f, -0.006f, 1), TrendEntry(ThirdProductGuid, 0f, 0f, 0.006f, 1))),
                         Tick(profileId, Hour + (10 * Minute), 30, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -0.004f, 1), TrendEntry(OtherProductGuid, 0f, 0f, -0.006f, 1), TrendEntry(ThirdProductGuid, 0f, 0f, 0.006f, 1))));
        await FoldAllAsync(history, profileId);

        var query = FiguresQuery(profileId, TrendIsland(IslandKey), _hour, _hour.AddHours(2), 100);

        // Act
        var trend = await history.ReadFiguresTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour)} 3 3 1",
                           $"tick {FormatTime(_hour.AddSeconds(Hour + (10 * Minute)))} 3 3 1"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeFiguresPoint), "Only the good with a delta that rounds below 0 should count as in deficit");
    }

    /// <summary>
    /// A trend read holds exactly the points of its range: the start is inclusive, the end exclusive, for ticks and for
    /// hourly points, and the hourly point of the hour before the start is not read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task HistoryStoreGoodTrendReadsRangeOnly()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         GoodTick(profileId, 10 * Minute, 1),
                         GoodTick(profileId, Hour + (10 * Minute), 2),
                         GoodTick(profileId, (2 * Hour) + (10 * Minute), 3),
                         GoodTick(profileId, (2 * Hour) + (20 * Minute), 5),
                         GoodTick(profileId, (3 * Hour) + (5 * Minute), 6),
                         GoodTick(profileId, (3 * Hour) + (10 * Minute), 7),
                         GoodTick(profileId, (3 * Hour) + (20 * Minute), 8));
        _clock.SetUtcNow(_hour.AddDays(30));
        await FoldAllAsync(history, profileId);

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour.AddHours(1), _hour.AddSeconds((3 * Hour) + (10 * Minute)), 100);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour.AddHours(1))} 2 1 0",
                           $"hourly {FormatTime(_hour.AddHours(2))} 4 1 0",
                           $"tick {FormatTime(_hour.AddSeconds((3 * Hour) + (5 * Minute)))} 6 1 0"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeGoodPoint), "The range should hold the hourly points of its hours and the ticks from its start up to, not including, its end");
        Assert.AreEqual(3, trend.TotalCount, "The count should be the points of the range only");
    }

    /// <summary>
    /// A read with a maximum returns the newest points of the range in time order and the count of all points of the
    /// range.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task HistoryStoreGoodTrendReadsNewestMaxPointsAndCount()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         GoodTick(profileId, 10 * Minute, 1),
                         GoodTick(profileId, Hour + Minute, 11),
                         GoodTick(profileId, Hour + (2 * Minute), 12),
                         GoodTick(profileId, Hour + (3 * Minute), 13),
                         GoodTick(profileId, Hour + (4 * Minute), 14),
                         GoodTick(profileId, Hour + (5 * Minute), 15));
        await FoldAllAsync(history, profileId);

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour, _hour.AddHours(2), 4);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new double?[] { 12, 13, 14, 15 }, trend.Points.Select(point => point.Generation), "The newest points should be returned, oldest first");
        Assert.AreEqual(6, trend.TotalCount, "The count should hold the hourly point and the five ticks of the range");
    }

    /// <summary>
    /// A range of more points than one part holds is read in several parts and gives every point once, in time order.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task HistoryStoreTrendPartsGiveSamePointsAsOneRead()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var count = (2 * HistoryStore.TrendPartSize) + 30;
        var maxPoints = count - 5;

        await StoreAsync(store, Enumerable.Range(1, count).Select(index => GoodTick(profileId, index * 5, index)).ToArray());

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour, _hour.AddHours(2), maxPoints);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = Enumerable.Range(count - maxPoints + 1, maxPoints).Select(index => (double?)index);

        Assert.AreSequenceEqual(expected, trend.Points.Select(point => point.Generation), "The points of all parts should follow each other in time order without a gap or a repeat");
        Assert.AreEqual(count, trend.TotalCount, "The count should hold every point of the range");
    }

    /// <summary>
    /// A trend part asked for while the trend gate is held waits for it, then reads.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC16")]
    public async Task HistoryStoreTrendPartWaitsForTrendGate()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var gate = _databases[^1].TrendGate;
        var profileId = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;

        await StoreAsync(store, GoodTick(profileId, 10 * Minute, 1));

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour, _hour.AddHours(2), 100);

        await gate.WaitAsync(cancellationToken);

        // Act
        Task<HistoryTrend<HistoryGoodPoint>> read;
        bool completedWhileHeld;

        try
        {
            read = history.ReadGoodTrendAsync(query, cancellationToken);
            completedWhileHeld = await CompletesWithinWaitAsync(read);
        }
        finally
        {
            gate.Release();
        }

        var trend = await read.WaitAsync(_stepBound, cancellationToken);

        // Assert
        Assert.IsFalse(completedWhileHeld, "The read should wait while the trend gate is held");
        Assert.AreSequenceEqual(new double?[] { 1 }, trend.Points.Select(point => point.Generation), "The read should give its point once the gate is free");
    }

    /// <summary>
    /// A clean-up of a swept profile waits while the trend gate is held, as it is during a trend part, and truncates the
    /// write-ahead log to 0 bytes once the gate is free.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC16")]
    public async Task HistoryStoreClearDeletedWaitsForTrendPartThenTruncates()
    {
        // Arrange
        var (store, history, databaseFile) = await OpenAsync();

        var gate = _databases[^1].TrendGate;
        var profileId = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;

        await StoreAsync(store, PlainTick(profileId, 10 * Minute, 10), LatestTick(profileId));
        await store.DeleteProfileAsync(profileId, cancellationToken);
        await SweepAllAsync(history);
        await gate.WaitAsync(cancellationToken);

        // Act
        Task<HistoryResult> clearing;
        bool completedWhileHeld;

        try
        {
            clearing = history.ClearDeletedAsync(cancellationToken);
            completedWhileHeld = await CompletesWithinWaitAsync(clearing);
        }
        finally
        {
            gate.Release();
        }

        var result = await clearing.WaitAsync(_stepBound, cancellationToken);

        // Assert
        var walFile = databaseFile + "-wal";

        Assert.IsFalse(completedWhileHeld, "The clean-up should wait while the trend gate is held");
        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The clean-up should be done once the gate is free");
        Assert.AreEqual(0L, File.Exists(walFile) ? new FileInfo(walFile).Length : 0L, "The write-ahead log should be truncated to 0 bytes");
    }

    /// <summary>
    /// An hourly read gives one point per UTC hour that holds a tick, folded or not, with each value the mean over the
    /// hour's ticks that carried it, the folded point's ticks included; for several islands the sum of the islands'
    /// means. A tick without an island or without a value adds nothing to the mean, and an hour without a tick is no point.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadGoodTrendByHoursMeansTicksAndFoldedPoints()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 1.5f, 1f, 0.5f, 1))),
                         Tick(profileId, 20 * Minute, 20, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 2.5f, 3f, null, 1))),
                         Tick(profileId, Hour + (10 * Minute), 100, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 2f, 1f, null, 1)), Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 10f, 3f, 0.5f, 1))),
                         Tick(profileId, Hour + (20 * Minute), 200, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 4f, 3f, 1f, 1))),
                         Tick(profileId, (3 * Hour) + (5 * Minute), 300, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 7f, 1f, 0f, 1))));

        var folded = await FoldAllAsync(history, profileId);

        await StoreAsync(store, Tick(profileId, 40 * Minute, 30, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 5f, 2f, -1f, 1))));

        var query = GoodQuery(profileId, [TrendIsland(IslandKey), TrendIsland(OtherIslandKey)], _hour, _hour.AddHours(4), 100, HistoryTrendResolution.Hours);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour)} 3 2 -0.25",
                           $"hourly {FormatTime(_hour.AddHours(1))} 13 5 1.5",
                           $"hourly {FormatTime(_hour.AddHours(3))} 7 1 0"
                       };

        Assert.AreEqual(2, folded, "The two ticks of the first hour should have been folded before the late tick arrived");
        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeGoodPoint), "Each hour should give the mean of its ticks and of the folded point per island, summed over the islands, and an hour without a tick no point");
        Assert.AreEqual(3, trend.TotalCount, "The count should be the hours that hold a tick");
    }

    /// <summary>
    /// The hourly point read from the ticks of an hour equals the hourly point the fold makes of the same ticks, in
    /// its values and its game times, and the hourly read after the fold gives it again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadGoodTrendByHoursEqualsFoldedHour()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 1.25f, 1f, 0.5f, 1)), Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 4f, 2f, null, 1))),
                         Tick(profileId, 20 * Minute, 2_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 2.5f, 3f, -1.5f, 1))),
                         Tick(profileId, 30 * Minute, 3_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 3.75f, 2f, null, 1)), Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 8f, 4f, 0.25f, 1))),
                         LatestTick(profileId));

        var points = GoodQuery(profileId, [TrendIsland(IslandKey), TrendIsland(OtherIslandKey)], _hour, _hour.AddHours(1), 100);
        var hours = GoodQuery(profileId, [TrendIsland(IslandKey), TrendIsland(OtherIslandKey)], _hour, _hour.AddHours(1), 100, HistoryTrendResolution.Hours);

        // Act
        var before = await history.ReadGoodTrendAsync(hours, TestContext.CancellationToken);
        var folded = await FoldAllAsync(history, profileId);
        var afterPoints = await history.ReadGoodTrendAsync(points, TestContext.CancellationToken);
        var afterHours = await history.ReadGoodTrendAsync(hours, TestContext.CancellationToken);

        // Assert
        var expected = before.Points.Select(DescribeGoodWithGameTimes).ToList();

        Assert.AreEqual(3, folded, "The three ticks of the hour should have been folded");
        Assert.HasCount(1, expected, "The hourly read should give one point for the hour");
        Assert.AreSequenceEqual(expected, afterPoints.Points.Select(DescribeGoodWithGameTimes), "The hourly point of the fold should read as the hourly read of the ticks");
        Assert.AreSequenceEqual(expected, afterHours.Points.Select(DescribeGoodWithGameTimes), "The hourly read should give the same point after the fold");
    }

    /// <summary>
    /// The hourly figures of an island follow the fold: only the first entry of a good counts, the buildings are the sum of
    /// the goods' mean building counts rounded away from zero, and an hour whose ticks lack the island has no value.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadFiguresTrendByHoursUsesFirstEntryPerGood()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 10, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -1f, 2), TrendEntry(OtherProductGuid, 0f, 0f, 0.5f, 1), TrendEntry(ProductGuid, 0f, 0f, -5f, 100))),
                         Tick(profileId, 20 * Minute, 20, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, -1f, 4), TrendEntry(OtherProductGuid, 0f, 0f, 0.5f, 2), TrendEntry(ProductGuid, 0f, 0f, -5f, 100))),
                         Tick(profileId, 30 * Minute, 30, Island(IslandKey, IslandName)),
                         Tick(profileId, Hour + (10 * Minute), 40, Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 0f, 0f, 0f, 1))));

        var query = FiguresQuery(profileId, TrendIsland(IslandKey), _hour, _hour.AddHours(2), 100, HistoryTrendResolution.Hours);

        // Act
        var trend = await history.ReadFiguresTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour)} 5 2 1",
                           $"hourly {FormatTime(_hour.AddHours(1))} null null null"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeFiguresPoint), "The hour should count the first entry of each good, round the buildings' sum of means up from 4.5 and give an hour without the island no value");
        Assert.AreEqual(2, trend.TotalCount, "The count should be the hours that hold a tick");
    }

    /// <summary>
    /// An hour with more ticks than one part holds is read in several parts and still gives one point with the mean and
    /// the game times of all its ticks, followed by the next hour.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task HistoryStoreReadTrendByHoursSplitsLongHourAcrossParts()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var count = HistoryStore.TrendPartSize + 20;

        await StoreAsync(store, Enumerable.Range(1, count).Select(index => GoodTick(profileId, index * 10, index)).ToArray());
        await StoreAsync(store, GoodTick(profileId, Hour + Minute, 1_000), GoodTick(profileId, Hour + (2 * Minute), 2_000), GoodTick(profileId, Hour + (3 * Minute), 3_000));

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour, _hour.AddHours(2), 100, HistoryTrendResolution.Hours);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           $"hourly {FormatTime(_hour)} 130.5 1 0",
                           $"hourly {FormatTime(_hour.AddHours(1))} 2000 1 0"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeGoodPoint), "The ticks of the long hour should all count once in its mean, whatever part they were read in");
        Assert.AreSequenceEqual(new[] { $"hourly 10 {count * 10} False", $"hourly {Hour + Minute} {Hour + (3 * Minute)} False" }, trend.Points.Select(DescribeGoodGameTimes), "The hours should carry the smallest and largest game time of all their ticks");
        Assert.AreEqual(2, trend.TotalCount, "The count should be the hours that hold a tick");
    }

    /// <summary>
    /// The parts of an hourly read hold at most the part size in rows, an hourly point and a tick counting one each: every
    /// tick and every hourly point is read once, the hours stay oldest first, and no part or slice is empty.
    /// </summary>
    /// <param name="partSize">The part size</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(10)]
    [DataRow(HistoryStore.TrendPartSize)]
    public void HistoryStorePlanHourPartsHoldsAtMostPartSizeRows(int partSize)
    {
        // Arrange
        var hours = PlannedHours();

        // Act
        var parts = HistoryStore.PlanHourParts(hours, partSize);

        // Assert
        var slices = parts.SelectMany(part => part).ToList();

        Assert.IsGreaterThanOrEqualTo(1, parts.Count, "The hours should give at least one part");
        Assert.IsGreaterThanOrEqualTo(1, parts.Min(part => part.Count), "No part should be empty");
        Assert.IsGreaterThanOrEqualTo(1, slices.Min(RowsOf), "No slice should be empty");
        Assert.IsLessThanOrEqualTo(partSize, parts.Max(part => part.Sum(RowsOf)), "No part should hold more rows than the part size");
        Assert.AreSequenceEqual(hours.Select(hour => $"{hour.HourTicks} {hour.Ticks} {(hour.WithHourly ? 1 : 0)}"), slices.GroupBy(slice => slice.HourTicks).Select(group => $"{group.Key} {group.Sum(slice => slice.Ticks)} {group.Count(slice => slice.WithHourly)}"), "Every tick and every hourly point should be read exactly once, the hours in their order");
        Assert.AreSequenceEqual(slices.Select(slice => slice.HourTicks).Order(), slices.Select(slice => slice.HourTicks), "The slices of an hour should follow each other, oldest hour first");
    }

    /// <summary>
    /// A point of a trend read carries its game times and tells whether no island of it holds an entry: the tick its
    /// game time, a folded hour its smallest and largest, whatever the ticks held.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreReadTrendGivesGameTimesAndNoEntriesFlag()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         NoEntryTick(profileId, 10 * Minute, 19_000),
                         Tick(profileId, 20 * Minute, 1_750_000_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 1f, 1f, 0f, 1))),
                         Tick(profileId, 30 * Minute, 1_750_060_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 3f, 1f, 0f, 1))),
                         NoEntryTick(profileId, Hour + (10 * Minute), 19_001),
                         Tick(profileId, (3 * Hour) + (5 * Minute), 1_750_120_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 4f, 1f, 0f, 1))),
                         Tick(profileId, (3 * Hour) + (15 * Minute), 1_750_130_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 5f, 1f, 0f, 1))),
                         NoEntryTick(profileId, (3 * Hour) + (20 * Minute), 19_002));
        _clock.SetUtcNow(_hour.AddDays(30));
        await FoldAllAsync(history, profileId);

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour, _hour.AddHours(4), 100);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           "hourly 19000 1750060000 False",
                           "hourly 19001 19001 True",
                           "tick 1750120000 False",
                           "tick 1750130000 False",
                           "tick 19002 True"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Select(DescribeGoodGameTimes), "Each point should carry its game times and the flag of having no entry in any island");
    }

    /// <summary>
    /// An hourly read of a good gives an hour the largest and smallest game time over its ticks that hold an entry, the
    /// folded point and the unfolded ticks alike, and flags an hour without any entry.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreReadTrendByHoursGivesGameTimesOfTicksWithEntries()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 1f, 1f, 0f, 1))),
                         Tick(profileId, 20 * Minute, 2_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 2f, 1f, 0f, 1))),
                         Tick(profileId, (3 * Hour) + (5 * Minute), 9_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 9f, 1f, 0f, 1))));
        await FoldAllAsync(history, profileId);
        await StoreAsync(store,
                         Tick(profileId, 40 * Minute, 3_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 3f, 1f, 0f, 1))),
                         NoEntryTick(profileId, 50 * Minute, 500),
                         NoEntryTick(profileId, Hour + (10 * Minute), 19_000),
                         NoEntryTick(profileId, Hour + (20 * Minute), 19_500),
                         Tick(profileId, (2 * Hour) + (10 * Minute), 7_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 4f, 1f, 0f, 1))),
                         Tick(profileId, (2 * Hour) + (20 * Minute), 6_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 5f, 1f, 0f, 1))));

        var query = GoodQuery(profileId, [TrendIsland(IslandKey)], _hour, _hour.AddHours(4), 100, HistoryTrendResolution.Hours);

        // Act
        var trend = await history.ReadGoodTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           "hourly 1000 3000 False",
                           "hourly 6000 7000 False",
                           "hourly 9000 9000 False"
                       };

        Assert.AreSequenceEqual(expected, trend.Points.Where(point => point.HasNoEntries == false).Select(DescribeGoodGameTimes), "An hour should carry the smallest and largest game time of its ticks with an entry, the folded point included and a tick without entries left out");

        var expectedFlags = new[] { false, true, false, false };

        Assert.AreSequenceEqual(expectedFlags, trend.Points.Select(point => point.HasNoEntries), "Only the hour whose ticks hold no entry should be flagged");
    }

    /// <summary>
    /// An hourly read of the figures of an island gives an hour the game times of its ticks that hold an entry on any
    /// island, and flags an hour whose ticks hold no entry on any island.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task HistoryStoreReadFiguresTrendByHoursGivesGameTimesAndNoEntriesFlag()
    {
        // Arrange
        var (store, history, _) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await StoreAsync(store,
                         Tick(profileId, 10 * Minute, 1_000, Island(IslandKey, IslandName, TrendEntry(ProductGuid, 0f, 0f, 0f, 1))),
                         Tick(profileId, 20 * Minute, 3_000, Island(IslandKey, IslandName), Island(OtherIslandKey, "Second island", TrendEntry(ProductGuid, 0f, 0f, 0f, 1))),
                         NoEntryTick(profileId, 30 * Minute, 19_000),
                         NoEntryTick(profileId, Hour + (10 * Minute), 19_001));

        var query = FiguresQuery(profileId, TrendIsland(IslandKey), _hour, _hour.AddHours(2), 100, HistoryTrendResolution.Hours);

        // Act
        var trend = await history.ReadFiguresTrendAsync(query, TestContext.CancellationToken);

        // Assert
        var expected = new[] { "hourly 1000 3000 False" };
        var expectedFlags = new[] { false, true };

        Assert.AreSequenceEqual(expected, trend.Points.Where(point => point.HasNoEntries == false).Select(DescribeFiguresGameTimes), "The hour should carry the game times of its ticks with an entry on any island");
        Assert.AreSequenceEqual(expectedFlags, trend.Points.Select(point => point.HasNoEntries), "Only the hour whose ticks hold no entry on any island should be flagged");
    }

    /// <summary>
    /// Creates an entry for a trend: the generation, consumption, delta and building count, everything else zero.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="generation">The generation</param>
    /// <param name="consumption">The consumption</param>
    /// <param name="delta">The delta, or <see langword="null" /></param>
    /// <param name="buildingCount">The building count</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry TrendEntry(int productGuid, float generation, float consumption, float? delta, int buildingCount)
    {
        return Entry(productGuid, [generation, consumption, delta, 0f, 0f, 0f, 0f, 0f], [buildingCount, 0, 0], Map(), Map());
    }

    /// <summary>
    /// Creates a tick of the island and good of the tests with a generation, consumption 1 and delta 0.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the hour <c>H</c></param>
    /// <param name="generation">The generation</param>
    /// <returns>The tick</returns>
    private static ContractTick GoodTick(Guid profileId, long seconds, float generation)
    {
        return Tick(profileId, seconds, seconds, Island(IslandKey, IslandName, TrendEntry(ProductGuid, generation, 1f, 0f, 1)));
    }

    /// <summary>
    /// Creates the key of an island made by <see cref="Island" /> from one number.
    /// </summary>
    /// <param name="key">The number of the key</param>
    /// <returns>The key</returns>
    private static HistoryIslandKey TrendIsland(int key)
    {
        return new HistoryIslandKey(key, (byte)key, 0);
    }

    /// <summary>
    /// Creates the trend query of the good of the tests.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="islands">The islands to sum</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <param name="maxPoints">The maximum number of points</param>
    /// <param name="resolution">The resolution of the points</param>
    /// <returns>The query</returns>
    private static HistoryGoodTrendQuery GoodQuery(Guid profileId, IReadOnlyList<HistoryIslandKey> islands, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, int maxPoints, HistoryTrendResolution resolution = HistoryTrendResolution.Points)
    {
        return new HistoryGoodTrendQuery
               {
                   ProfileId = profileId,
                   Islands = islands,
                   ProductGuid = ProductGuid,
                   RangeStart = rangeStart,
                   RangeEnd = rangeEnd,
                   MaxPoints = maxPoints,
                   Resolution = resolution
               };
    }

    /// <summary>
    /// Creates the figures query of an island.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <param name="maxPoints">The maximum number of points</param>
    /// <param name="resolution">The resolution of the points</param>
    /// <returns>The query</returns>
    private static HistoryFiguresTrendQuery FiguresQuery(Guid profileId, HistoryIslandKey island, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, int maxPoints, HistoryTrendResolution resolution = HistoryTrendResolution.Points)
    {
        return new HistoryFiguresTrendQuery
               {
                   ProfileId = profileId,
                   Island = island,
                   RangeStart = rangeStart,
                   RangeEnd = rangeEnd,
                   MaxPoints = maxPoints,
                   Resolution = resolution
               };
    }

    /// <summary>
    /// Creates a tick whose island holds no entry, as the game sends after a load.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the hour <c>H</c></param>
    /// <param name="gameTime">The game time</param>
    /// <returns>The tick</returns>
    private static ContractTick NoEntryTick(Guid profileId, long seconds, long gameTime)
    {
        return Tick(profileId, seconds, gameTime, Island(IslandKey, IslandName));
    }

    /// <summary>
    /// Describes a point of a good's trend with its game times: kind, then the smallest and largest game time of an
    /// hourly point or the game time of a tick, and the flag of having no entries.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribeGoodGameTimes(HistoryGoodPoint point)
    {
        return DescribeGameTimes(point.IsHourly, point.FirstGameTime, point.GameTime, point.HasNoEntries);
    }

    /// <summary>
    /// Describes a point of the figures with its game times, as <see cref="DescribeGoodGameTimes" /> does.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribeFiguresGameTimes(HistoryFiguresPoint point)
    {
        return DescribeGameTimes(point.IsHourly, point.FirstGameTime, point.GameTime, point.HasNoEntries);
    }

    /// <summary>
    /// Describes the game times of a point.
    /// </summary>
    /// <param name="isHourly">Whether the point is an hour</param>
    /// <param name="firstGameTime">The first game time</param>
    /// <param name="gameTime">The game time</param>
    /// <param name="hasNoEntries">Whether the point has no entries</param>
    /// <returns>The text</returns>
    private static string DescribeGameTimes(bool isHourly, long? firstGameTime, long? gameTime, bool hasNoEntries)
    {
        var game = gameTime?.ToString(CultureInfo.InvariantCulture) ?? "null";
        var first = firstGameTime?.ToString(CultureInfo.InvariantCulture) ?? "null";

        return isHourly
                   ? $"hourly {first} {game} {hasNoEntries}"
                   : $"tick {game} {hasNoEntries}";
    }

    /// <summary>
    /// Describes a point of a good's trend with its values and game times.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribeGoodWithGameTimes(HistoryGoodPoint point)
    {
        return $"{DescribeGoodPoint(point)} {DescribeGoodGameTimes(point)}";
    }

    /// <summary>
    /// Counts the rows of a slice of an hourly read: its ticks and its hourly point.
    /// </summary>
    /// <param name="slice">The slice</param>
    /// <returns>The number of rows</returns>
    private static int RowsOf(TrendHourSlice slice)
    {
        return slice.Ticks + (slice.WithHourly ? 1 : 0);
    }

    /// <summary>
    /// Creates the hours of a plan: with ticks and an hourly point, with many ticks, with an hourly point only, with
    /// more rows than a small part holds, and with one tick.
    /// </summary>
    /// <returns>The hours, oldest first</returns>
    private static List<(long HourTicks, int Ticks, bool WithHourly)> PlannedHours()
    {
        return [
                   (_hour.UtcTicks, 3, true),
                   (_hour.AddHours(1).UtcTicks, 25, false),
                   (_hour.AddHours(2).UtcTicks, 0, true),
                   (_hour.AddHours(3).UtcTicks, 10, true),
                   (_hour.AddHours(4).UtcTicks, 1, false),
                   (_hour.AddHours(5).UtcTicks, 600, true)
               ];
    }

    /// <summary>
    /// Describes a point of a good's trend: kind, time, generation, consumption and delta.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribeGoodPoint(HistoryGoodPoint point)
    {
        return $"{(point.IsHourly ? "hourly" : "tick")} {FormatTime(point.Time)} {FormatValue(point.Generation)} {FormatValue(point.Consumption)} {FormatValue(point.Delta)}";
    }

    /// <summary>
    /// Describes a point of the figures: kind, time, buildings, produced goods and goods in deficit.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribeFiguresPoint(HistoryFiguresPoint point)
    {
        return $"{(point.IsHourly ? "hourly" : "tick")} {FormatTime(point.Time)} {point.Buildings?.ToString(CultureInfo.InvariantCulture) ?? "null"} {point.ProducedGoods?.ToString(CultureInfo.InvariantCulture) ?? "null"} {point.GoodsInDeficit?.ToString(CultureInfo.InvariantCulture) ?? "null"}";
    }

    /// <summary>
    /// Builds a map from alternating keys and values.
    /// </summary>
    /// <param name="pairs">The keys and values, alternating</param>
    /// <returns>The map</returns>
    private static Dictionary<int, int> Map(params int[] pairs)
    {
        return Enumerable.Range(0, pairs.Length / 2).ToDictionary(index => pairs[2 * index], index => pairs[(2 * index) + 1]);
    }

    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="floats">The eight floats in the order of <see cref="_plainFloats" /></param>
    /// <param name="integers">BuildingCount, Maintenance and Profit</param>
    /// <param name="workforce">The workforce amounts</param>
    /// <param name="buildings">The building amounts</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry(int productGuid, float?[] floats, int[] integers, Dictionary<int, int> workforce, Dictionary<int, int> buildings)
    {
        return new ContractTickEntry
               {
                   ProductGuid = productGuid,
                   Generation = floats[0],
                   Consumption = floats[1],
                   Delta = floats[2],
                   PerfectGeneration = floats[3],
                   PerfectConsumption = floats[4],
                   Income = floats[5],
                   SummedProductivity = floats[6],
                   AverageProductivity = floats[7],
                   BuildingCount = integers[0],
                   Maintenance = integers[1],
                   Profit = integers[2],
                   WorkforceAmounts = workforce,
                   BuildingAmounts = buildings
               };
    }

    /// <summary>
    /// Creates an entry with the plain values and one pair in each map.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry PlainEntry(int productGuid)
    {
        return Entry(productGuid, _plainFloats, _plainIntegers, Map(100, 7), Map(200, 3));
    }

    /// <summary>
    /// Creates an entry whose floats all have one value and whose integers all have another.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="floatValue">The value of every float</param>
    /// <param name="integerValue">The value of every integer</param>
    /// <param name="workforce">The workforce amounts</param>
    /// <param name="buildings">The building amounts</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry UniformEntry(int productGuid, float floatValue, int integerValue, Dictionary<int, int> workforce, Dictionary<int, int> buildings)
    {
        return Entry(productGuid, Enumerable.Repeat<float?>(floatValue, 8).ToArray(), [integerValue, integerValue, integerValue], workforce, buildings);
    }

    /// <summary>
    /// Creates an island whose key is derived from one number (session GUID and island ID that number, area index 0).
    /// </summary>
    /// <param name="key">The number of the key</param>
    /// <param name="name">The island name</param>
    /// <param name="entries">The entries</param>
    /// <returns>The island</returns>
    private static ContractTickIsland Island(int key, string name, params ContractTickEntry[] entries)
    {
        return new ContractTickIsland
               {
                   SessionGuid = key,
                   IslandId = (byte)key,
                   AreaIndex = 0,
                   SessionId = 1,
                   Name = name,
                   Entries = entries
               };
    }

    /// <summary>
    /// Creates a tick of a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the hour <c>H</c></param>
    /// <param name="gameTime">The game time</param>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static ContractTick Tick(Guid profileId, long seconds, long gameTime, params ContractTickIsland[] islands)
    {
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = _hour.AddSeconds(seconds),
                   GameTime = gameTime,
                   Islands = islands
               };
    }

    /// <summary>
    /// Creates a tick with the plain entry of the good on the island.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the hour <c>H</c></param>
    /// <param name="gameTime">The game time</param>
    /// <returns>The tick</returns>
    private static ContractTick PlainTick(Guid profileId, long seconds, long gameTime)
    {
        return Tick(profileId, seconds, gameTime, Island(IslandKey, IslandName, PlainEntry(ProductGuid)));
    }

    /// <summary>
    /// Creates a profile's tick two hours after <c>H</c>, so <c>H</c> is not the hour of its latest tick.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The tick</returns>
    private static ContractTick LatestTick(Guid profileId)
    {
        return PlainTick(profileId, 2 * Hour, 9_000);
    }

    /// <summary>
    /// Creates a tick of two islands: the first with two goods, the second with one, all with the given workforce.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the hour <c>H</c></param>
    /// <param name="workforce">The workforce amounts of every entry</param>
    /// <returns>The tick</returns>
    private static ContractTick TwoIslandTick(Guid profileId, long seconds, Dictionary<int, int> workforce)
    {
        return Tick(profileId,
                    seconds,
                    seconds,
                    Island(IslandKey,
                           IslandName,
                           Entry(ProductGuid, _plainFloats, _plainIntegers, workforce, Map(200, 1)),
                           Entry(OtherProductGuid, _plainFloats, _plainIntegers, workforce, Map(200, 1))),
                    Island(OtherIslandKey, "Second island", Entry(ProductGuid, _plainFloats, _plainIntegers, Map(100, 1), Map(200, 1))));
    }

    /// <summary>
    /// Creates a tick whose island holds the good twice, with another good between.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the hour <c>H</c></param>
    /// <param name="first">The first entry of the good</param>
    /// <param name="second">The second entry of the good</param>
    /// <returns>The tick</returns>
    private static ContractTick RepeatedGoodTick(Guid profileId, long seconds, ContractTickEntry first, ContractTickEntry second)
    {
        return Tick(profileId, seconds, seconds, Island(IslandKey, IslandName, first, PlainEntry(OtherProductGuid), second));
    }

    /// <summary>
    /// Creates the series query of the island and good of the tests.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <returns>The query</returns>
    private static HistorySeriesQuery Query(Guid profileId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        return new HistorySeriesQuery
               {
                   ProfileId = profileId,
                   SessionGuid = IslandKey,
                   IslandId = IslandKey,
                   AreaIndex = 0,
                   ProductGuid = ProductGuid,
                   RangeStart = rangeStart,
                   RangeEnd = rangeEnd
               };
    }

    /// <summary>
    /// Formats a time in UTC, round-trip.
    /// </summary>
    /// <param name="time">The time</param>
    /// <returns>The text</returns>
    private static string FormatTime(DateTimeOffset time)
    {
        return time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a value, round-trip, or <c>null</c> for none.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string FormatValue(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";
    }

    /// <summary>
    /// Describes the head of a point: kind, time, tick count, game times and island name.
    /// </summary>
    /// <param name="isHourly">Whether the point is a folded hour</param>
    /// <param name="time">The time</param>
    /// <param name="tickCount">The number of ticks</param>
    /// <param name="firstGameTime">The smallest game time</param>
    /// <param name="lastGameTime">The largest game time</param>
    /// <param name="islandName">The island name, or <see langword="null" /></param>
    /// <returns>The text, ending in a space</returns>
    private static string Head(bool isHourly, DateTimeOffset time, int tickCount, long firstGameTime, long lastGameTime, string? islandName)
    {
        return $"{(isHourly ? "hourly" : "tick")} {FormatTime(time)} n {tickCount} game {firstGameTime}..{lastGameTime} name {islandName ?? "<none>"} ";
    }

    /// <summary>
    /// Describes the head of an hourly point.
    /// </summary>
    /// <param name="hour">The hour start</param>
    /// <param name="tickCount">The number of ticks</param>
    /// <param name="firstGameTime">The smallest game time</param>
    /// <param name="lastGameTime">The largest game time</param>
    /// <param name="islandName">The island name</param>
    /// <returns>The text, ending in a space</returns>
    private static string HourlyHead(DateTimeOffset hour, int tickCount, long firstGameTime, long lastGameTime, string islandName)
    {
        return Head(true, hour, tickCount, firstGameTime, lastGameTime, islandName);
    }

    /// <summary>
    /// Describes the values and amounts of a point, amounts in ascending GUID order.
    /// </summary>
    /// <param name="values">The eleven values in the order of <see cref="HistoryPoint" /></param>
    /// <param name="workforce">The workforce amounts</param>
    /// <param name="buildings">The building amounts</param>
    /// <returns>The text</returns>
    private static string Body(double?[] values, IEnumerable<(int ItemGuid, double Amount)> workforce, IEnumerable<(int ItemGuid, double Amount)> buildings)
    {
        return $"values {string.Join(' ', values.Select(FormatValue))} workforce {DescribeAmounts(workforce)} buildings {DescribeAmounts(buildings)}";
    }

    /// <summary>
    /// Describes amounts in ascending GUID order.
    /// </summary>
    /// <param name="amounts">The amounts</param>
    /// <returns>The text</returns>
    private static string DescribeAmounts(IEnumerable<(int ItemGuid, double Amount)> amounts)
    {
        return string.Join(',', amounts.OrderBy(amount => amount.ItemGuid).Select(amount => $"{amount.ItemGuid}={FormatValue(amount.Amount)}"));
    }

    /// <summary>
    /// Gives the plain values of an entry as the values of a point.
    /// </summary>
    /// <returns>The eleven values</returns>
    private static double?[] PlainValues()
    {
        return EntryValues(PlainEntry(ProductGuid));
    }

    /// <summary>
    /// Gives the values of an entry as the values of a point: floats widened, integers as fractional numbers.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The eleven values</returns>
    private static double?[] EntryValues(ContractTickEntry entry)
    {
        return
        [
            entry.Generation,
            entry.Consumption,
            entry.Delta,
            entry.PerfectGeneration,
            entry.PerfectConsumption,
            entry.Income,
            entry.SummedProductivity,
            entry.AverageProductivity,
            entry.BuildingCount,
            entry.Maintenance,
            entry.Profit
        ];
    }

    /// <summary>
    /// Describes the values and amounts of an entry, or of none.
    /// </summary>
    /// <param name="entry">The entry, or <see langword="null" /> for a point without the good</param>
    /// <returns>The text</returns>
    private static string EntryBody(ContractTickEntry? entry)
    {
        return entry is null
                   ? Body(new double?[11], [], [])
                   : Body(EntryValues(entry), entry.WorkforceAmounts.Select(pair => (pair.Key, (double)pair.Value)), entry.BuildingAmounts.Select(pair => (pair.Key, (double)pair.Value)));
    }

    /// <summary>
    /// Describes the point a tick should give.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="islandName">The island name, or <see langword="null" /> for a tick without the island</param>
    /// <param name="entry">The entry of the good, or <see langword="null" /> for none</param>
    /// <returns>The text</returns>
    private static string TickLine(ContractTick tick, string? islandName, ContractTickEntry? entry)
    {
        return Head(false, tick.ReceiveTime, 1, tick.GameTime, tick.GameTime, islandName) + EntryBody(entry);
    }

    /// <summary>
    /// Gives the values of a point.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The eleven values</returns>
    private static double?[] PointValues(HistoryPoint point)
    {
        return
        [
            point.Generation,
            point.Consumption,
            point.Delta,
            point.PerfectGeneration,
            point.PerfectConsumption,
            point.Income,
            point.SummedProductivity,
            point.AverageProductivity,
            point.BuildingCount,
            point.Maintenance,
            point.Profit
        ];
    }

    /// <summary>
    /// Describes a point.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribePoint(HistoryPoint point)
    {
        return DescribeWithValues(point, PointValues(point));
    }

    /// <summary>
    /// Describes a point with the given values.
    /// </summary>
    /// <param name="point">The point</param>
    /// <param name="values">The values to describe in place of the point's</param>
    /// <returns>The text</returns>
    private static string DescribeWithValues(HistoryPoint point, double?[] values)
    {
        return Head(point.IsHourly, point.Time, point.TickCount, point.FirstGameTime, point.LastGameTime, point.IslandName)
               + Body(values, point.WorkforceAmounts.Select(amount => (amount.ItemGuid, amount.Amount)), point.BuildingAmounts.Select(amount => (amount.ItemGuid, amount.Amount)));
    }

    /// <summary>
    /// Describes a point without its Generation value.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The text</returns>
    private static string DescribeWithoutGeneration(HistoryPoint point)
    {
        var values = PointValues(point);

        values[0] = null;

        return DescribeWithValues(point, values);
    }

    /// <summary>
    /// Opens a connection of its own, without the pool, to a SQLite file.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The opened connection</returns>
    private static SqliteConnection OpenRawConnection(string databaseFile)
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };
        var connection = new SqliteConnection(builder.ToString());

        connection.Open();

        return connection;
    }

    /// <summary>
    /// Creates the trigger of <see cref="CreateFailingFoldTriggerSql" /> over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    private static void CreateFailingFoldTrigger(string databaseFile)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        command.CommandText = CreateFailingFoldTriggerSql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates the trigger of <see cref="CreateFailingSweepTriggerSql" /> over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    private static void CreateFailingSweepTrigger(string databaseFile)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        command.CommandText = CreateFailingSweepTriggerSql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Counts the rows of the tables of <see cref="CountRowsSql" /> over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The counts, separated by spaces</returns>
    private static string CountRows(string databaseFile)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        command.CommandText = CountRowsSql;

        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Reads the bytes of a file that SQLite holds open, or none when it does not exist.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The bytes</returns>
    private static byte[] ReadSharedBytes(string path)
    {
        if (File.Exists(path) == false)
        {
            return [];
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();

        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>
    /// Finds the names whose UTF-8 bytes occur in the database file or its <c>-wal</c> or <c>-shm</c> file.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <param name="names">The names</param>
    /// <returns>The names found, in the given order</returns>
    private static List<string> FindNamesInFiles(string databaseFile, params string[] names)
    {
        var files = new[] { databaseFile, databaseFile + "-wal", databaseFile + "-shm" }.Select(ReadSharedBytes).ToList();

        return names.Where(name => files.Any(bytes => bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(name)) >= 0)).ToList();
    }

    /// <summary>
    /// Opens a store and a history store on a new database file.
    /// </summary>
    /// <returns>The store, the history store and the database file</returns>
    private async Task<(ServerStore Store, HistoryStore History, string DatabaseFile)> OpenAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-history-store-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        var databaseFile = Path.Combine(directory, DatabaseFileName);
        var result = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        return (new ServerStore(database), new HistoryStore(database), databaseFile);
    }

    /// <summary>
    /// Opens a store and a history store on a new database file or an in-memory database.
    /// </summary>
    /// <param name="inMemory">Whether the database is in memory</param>
    /// <returns>The store and the history store</returns>
    private async Task<(ServerStore Store, HistoryStore History)> OpenStoresAsync(bool inMemory)
    {
        if (inMemory == false)
        {
            var (fileStore, fileHistory, _) = await OpenAsync();

            return (fileStore, fileHistory);
        }

        var result = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        return (new ServerStore(database), new HistoryStore(database));
    }

    /// <summary>
    /// Stores ticks in order.
    /// </summary>
    /// <param name="store">The store</param>
    /// <param name="ticks">The ticks</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task StoreAsync(ServerStore store, params ContractTick[] ticks)
    {
        foreach (var tick in ticks)
        {
            await store.StoreTickAsync(tick, TestContext.CancellationToken);
        }
    }

    /// <summary>
    /// Runs fold steps of a profile at the clock's time until one reports anything but done.
    /// </summary>
    /// <param name="history">The history store</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The number of steps that folded a tick</returns>
    private async Task<int> FoldAllAsync(HistoryStore history, Guid profileId)
    {
        var folded = 0;

        while (folded < MaxSteps && (await history.FoldNextTickAsync(profileId, _clock.GetUtcNow(), TestContext.CancellationToken)).Outcome == HistoryOutcome.Done)
        {
            folded++;
        }

        return folded;
    }

    /// <summary>
    /// Runs sweep steps until one reports anything but done.
    /// </summary>
    /// <param name="history">The history store</param>
    /// <returns>The number of steps that removed rows</returns>
    private async Task<int> SweepAllAsync(HistoryStore history)
    {
        var steps = 0;

        while (steps < MaxSteps && (await history.SweepNextAsync(TestContext.CancellationToken)).Outcome == HistoryOutcome.Done)
        {
            steps++;
        }

        return steps;
    }

    /// <summary>
    /// Waits a short time for a task and tells whether it completed.
    /// </summary>
    /// <param name="task">The task</param>
    /// <returns><see langword="true" /> when the task completed within the wait</returns>
    private async Task<bool> CompletesWithinWaitAsync(Task task)
    {
        try
        {
            await task.WaitAsync(_waitBound, TestContext.CancellationToken);

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the series of the island and good of the tests and describes its points.
    /// </summary>
    /// <param name="history">The history store</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <returns>The points described</returns>
    private async Task<List<string>> ReadSeriesAsync(HistoryStore history, Guid profileId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        var series = await history.ReadSeriesAsync(Query(profileId, rangeStart, rangeEnd), TestContext.CancellationToken);

        return series.Select(DescribePoint).ToList();
    }

    #endregion // Methods
}