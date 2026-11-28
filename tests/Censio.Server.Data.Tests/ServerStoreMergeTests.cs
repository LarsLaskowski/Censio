using System.Globalization;

using Censio.Contracts;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Censio.Server.Data.Tests;

/// <summary>
/// Tests for <see cref="ServerStore.MergeProfileAsync" />, against real SQLite: a file in a temporary directory.
/// </summary>
[TestClass]
public sealed class ServerStoreMergeTests
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// The name of the profile whose history is merged (the source).
    /// </summary>
    private const string SourceName = "Source profile 41ac";

    /// <summary>
    /// The name of the profile that receives the history (the target).
    /// </summary>
    private const string TargetName = "Target profile 9e07";

    /// <summary>
    /// The text of the trigger's error that makes a merge fail.
    /// </summary>
    private const string FailingMessage = "test merge failure";

    /// <summary>
    /// The statement that makes the removal of the source's profile row, the last step of a merge, fail.
    /// </summary>
    private const string CreateFailingDeleteTriggerSql = "CREATE TRIGGER \"FailProfileDelete\" BEFORE DELETE ON \"Profiles\" BEGIN SELECT RAISE(ABORT, '" + FailingMessage + "'); END;";

    /// <summary>
    /// The statement that lists every row a merge may touch, ordered, as one text.
    /// </summary>
    private const string SnapshotSql = "SELECT coalesce(group_concat(\"x\", '|'), '') FROM (SELECT 'P ' || \"Id\" || ' ' || coalesce(\"Name\", '-') AS \"x\" FROM \"Profiles\""
                                       + " UNION ALL SELECT 'T ' || \"Number\" || ' ' || \"Id\" || ' ' || \"ProfileId\" FROM \"Ticks\""
                                       + " UNION ALL SELECT 'I ' || \"ProfileId\" || ' ' || \"SessionGuid\" || ' ' || \"IslandId\" || ' ' || \"AreaIndex\" || ' ' || \"Name\" || ' ' || \"NameReceiveTicks\" FROM \"Islands\""
                                       + " UNION ALL SELECT 'H ' || \"Number\" || ' ' || \"ProfileId\" || ' ' || \"HourTicks\" || ' ' || \"TickCount\" FROM \"HourlyPoints\""
                                       + " UNION ALL SELECT 'F ' || \"Id\" || ' ' || \"PointNumber\" FROM \"FoldedTicks\""
                                       + " UNION ALL SELECT 'R ' || \"ProfileId\" || ' ' || \"MessageId\" FROM \"TradeRouteSets\" ORDER BY \"x\");";

    /// <summary>
    /// The statement that counts the hourly rows of one profile, in the order points, islands, entries, pairs, folded
    /// ticks.
    /// </summary>
    private const string CountHourlySql = "SELECT (SELECT count(*) FROM \"HourlyPoints\" WHERE \"ProfileId\" = $profile)"
                                          + " || ' ' || (SELECT count(*) FROM \"HourlyIslands\" i JOIN \"HourlyPoints\" p ON p.\"Number\" = i.\"PointNumber\" WHERE p.\"ProfileId\" = $profile)"
                                          + " || ' ' || (SELECT count(*) FROM \"HourlyEntries\" e JOIN \"HourlyIslands\" i ON i.\"Id\" = e.\"HourlyIslandId\" JOIN \"HourlyPoints\" p ON p.\"Number\" = i.\"PointNumber\" WHERE p.\"ProfileId\" = $profile)"
                                          + " || ' ' || (SELECT count(*) FROM \"HourlyMapPairs\" m JOIN \"HourlyEntries\" e ON e.\"Id\" = m.\"EntryId\" JOIN \"HourlyIslands\" i ON i.\"Id\" = e.\"HourlyIslandId\" JOIN \"HourlyPoints\" p ON p.\"Number\" = i.\"PointNumber\" WHERE p.\"ProfileId\" = $profile)"
                                          + " || ' ' || (SELECT count(*) FROM \"FoldedTicks\" f JOIN \"HourlyPoints\" p ON p.\"Number\" = f.\"PointNumber\" WHERE p.\"ProfileId\" = $profile);";

    /// <summary>
    /// The statement that lists the identities of the ticks in their storing order.
    /// </summary>
    private const string TickOrderSql = "SELECT group_concat(\"Id\") FROM (SELECT \"Id\" FROM \"Ticks\" ORDER BY \"Number\");";

    /// <summary>
    /// The statement that counts the ticks of a profile.
    /// </summary>
    private const string CountTicksSql = "SELECT count(*) FROM \"Ticks\" WHERE \"ProfileId\" = $profile;";

    /// <summary>
    /// The statement that counts the rows of the trade-route tables.
    /// </summary>
    private const string CountTradeRoutesSql = "SELECT (SELECT count(*) FROM \"TradeRouteSets\") || ' ' || (SELECT count(*) FROM \"TradeRoutes\") || ' ' || (SELECT count(*) FROM \"TradeRouteStations\");";

    /// <summary>
    /// The most fold steps a helper runs for one profile.
    /// </summary>
    private const int MaxSteps = 100;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The first hour of the ticks of the tests.
    /// </summary>
    private static readonly DateTimeOffset _hour = new(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The islands of the first test after the merge.
    /// </summary>
    private static readonly string[] _expectedMergedIslands = ["1/1/0 TgtOne", "2/2/0 SrcTwoLate", "3/3/0 SrcThree"];

    /// <summary>
    /// The island of the merge into an unstored target.
    /// </summary>
    private static readonly string[] _expectedMovedIslands = ["1/1/0 SrcOne"];

    /// <summary>
    /// The numbers of hourly points of the source, the target and the reference before the merge.
    /// </summary>
    private static readonly string[] _expectedPointCounts = ["2", "1", "2"];

    /// <summary>
    /// The island keys and goods the combine test compares.
    /// </summary>
    private static readonly (int Island, int Product)[] _seriesKeys = [(1, 1), (1, 2), (1, 3), (2, 1), (3, 1)];

    /// <summary>
    /// The time the ticks of the first hours are folded at: more than a week after them and after their profile's latest
    /// tick's hour.
    /// </summary>
    private static readonly DateTimeOffset _foldTime = _hour.AddDays(7).AddHours(3);

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStoreMergeTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ServerStoreMergeTests(TestContext testContext)
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
    /// A merge moves the source's ticks to the target in their storing order, merges the profiles' islands by the later
    /// name stamp, keeps the target's name and removes the source's profile and islands (spec S2, E10).
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreMergeMovesTicksIslandsAndRemovesSource()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;
        var targetEarly = Tick(target, 5, 5, Island(2, "TgtTwoEarly", Entry(1, 1f, 1)));
        var sourceEarly = Tick(source, 10, 10, Island(1, "SrcOneEarly", Entry(1, 1f, 1)), Island(3, "SrcThree", Entry(1, 1f, 1)));
        var targetLate = Tick(target, 20, 20, Island(1, "TgtOne", Entry(1, 1f, 1)));
        var sourceLate = Tick(source, 30, 30, Island(2, "SrcTwoLate", Entry(1, 1f, 1)));

        await store.StoreProfileAsync(source, SourceName, cancellationToken);
        await store.StoreProfileAsync(target, TargetName, cancellationToken);
        await StoreTicksAsync(store, targetEarly, sourceEarly, targetLate, sourceLate);

        var orderBefore = ReadScalar(databaseFile, command => command.CommandText = TickOrderSql);

        // Act
        var result = await store.MergeProfileAsync(source, target, cancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The merge of a stored profile should be done");
        Assert.IsNull(result.Failure, "A merge that is done should report no failure");
        Assert.AreSequenceEqual(new[] { $"{target} {TargetName}" }, (await store.ListProfilesAsync(cancellationToken)).Select(profile => $"{profile.Id} {profile.Name}").ToList(), "Only the target should be listed, with its own name");
        Assert.AreSequenceEqual(_expectedMergedIslands, DescribeIslands(await store.ListIslandsAsync(target, cancellationToken)), "Each island should be named by the later stamp of its two profiles");
        Assert.IsEmpty(await store.ListIslandsAsync(source, cancellationToken), "The source should hold no island any more");
        Assert.AreEqual("0", ReadScalar(databaseFile, command => SetTickCount(command, source)), "No tick should be left under the source");
        Assert.AreEqual(orderBefore, ReadScalar(databaseFile, command => command.CommandText = TickOrderSql), "The ticks should keep their storing order");
        Assert.AreEqual(sourceLate.Id, (await store.ReadLatestTickAsync(target, cancellationToken))?.Id, "The latest tick of the target should be the source's newer one");

        foreach (var tick in (ContractTick[])[targetEarly, sourceEarly, targetLate, sourceLate])
        {
            var read = await store.ReadTickAsync(tick.Id, cancellationToken);

            Assert.AreEqual(target, read?.ProfileId, $"Every tick should be read under the target: {tick.Id}");
            Assert.AreEqual(tick.ReceiveTime, read?.ReceiveTime, $"A moved tick should keep its receive time: {tick.Id}");
            Assert.AreSequenceEqual(tick.Islands.Select(island => island.Name).ToList(), read?.Islands.Select(island => island.Name).ToList() ?? [], $"A moved tick should keep its islands: {tick.Id}");
        }
    }

    /// <summary>
    /// A merge of profiles that fold the same hour makes one hourly point of that hour, one of the hours only the source
    /// holds becomes the target's, and every series equals the one of a profile that received all the ticks: counts and
    /// sums added, the game-time range the union, islands, entries and pairs combined, island names by the later stamp;
    /// the folded tick identities of the source stay known (spec E10).
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreMergeCombinesHourlyPointsOfSameHour()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var history = new HistoryStore(_databases[^1]);
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var reference = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;
        var (sourceTicks, targetTicks) = CreateCombineTicks(source, target);
        var (referenceSourceTicks, referenceTargetTicks) = CreateCombineTicks(reference, reference);

        await store.StoreProfileAsync(source, SourceName, cancellationToken);
        await store.StoreProfileAsync(target, TargetName, cancellationToken);
        await StoreTicksAsync(store, [.. sourceTicks, .. targetTicks, .. referenceSourceTicks, .. referenceTargetTicks]);
        await FoldAsync(history, source);
        await FoldAsync(history, target);
        await FoldAsync(history, reference);

        Assert.AreSequenceEqual(_expectedPointCounts, new[] { source, target, reference }.Select(profile => CountHourly(databaseFile, profile).Split(' ')[0]).ToList(), "The folds should have made the hourly points the merge combines, or the checks below prove nothing");

        // Act
        var result = await store.MergeProfileAsync(source, target, cancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The merge should be done");
        Assert.AreEqual(CountHourly(databaseFile, reference), CountHourly(databaseFile, target), "The target should hold the hourly rows of a profile that received all the ticks");
        Assert.AreEqual("0 0 0 0 0", CountHourly(databaseFile, source), "The source should hold no hourly row any more");

        foreach (var (island, product) in _seriesKeys)
        {
            var expected = DescribeSeries(await history.ReadSeriesAsync(Query(reference, island, product), cancellationToken));
            var actual = DescribeSeries(await history.ReadSeriesAsync(Query(target, island, product), cancellationToken));

            Assert.IsNotEmpty(expected, $"The reference series should hold points, or the comparison proves nothing: island {island}, good {product}");
            Assert.AreSequenceEqual(expected, actual, $"The merged series should equal the reference's: island {island}, good {product}");
        }

        var shared = (await history.ReadSeriesAsync(Query(target, 1, 1), cancellationToken)).Single(point => point.IsHourly && point.Time == _hour);

        Assert.AreEqual(3, shared.TickCount, "The shared hour should count the ticks of both profiles");
        Assert.AreEqual(1000, shared.FirstGameTime, "The shared hour should start at the smallest game time");
        Assert.AreEqual(1500, shared.LastGameTime, "The shared hour should end at the largest game time");
        Assert.AreEqual("SrcOneLate", shared.IslandName, "The shared hour's island should be named by the later stamp");
        Assert.IsEmpty(await history.ReadSeriesAsync(Query(source, 1, 1), cancellationToken), "No series should be read for the source");

        foreach (var tick in sourceTicks.Take(3))
        {
            var resent = await store.StoreTickAsync(CopyOf(tick, target), cancellationToken);

            Assert.AreEqual(ServerStoreOutcome.AlreadyStored, resent.Outcome, $"A folded tick of the source should stay known: {tick.Id}");
        }
    }

    /// <summary>
    /// A merge keeps the set of trade routes with the later receive time (the target's when they are equal), the only
    /// one when one profile has none, and removes the other.
    /// </summary>
    /// <param name="sourceSeconds">The receive time of the source's set in seconds, or -1 for none</param>
    /// <param name="targetSeconds">The receive time of the target's set in seconds, or -1 for none</param>
    /// <param name="expectedRoute">The name of the route of the set that stays</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(20, 10, "Route of the source")]
    [DataRow(10, 20, "Route of the target")]
    [DataRow(15, 15, "Route of the target")]
    [DataRow(20, -1, "Route of the source")]
    [DataRow(-1, 20, "Route of the target")]
    public async Task ServerStoreMergeKeepsLaterTradeRouteSet(int sourceSeconds, int targetSeconds, string expectedRoute)
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;

        await store.StoreProfileAsync(source, SourceName, cancellationToken);
        await store.StoreProfileAsync(target, TargetName, cancellationToken);

        if (sourceSeconds >= 0)
        {
            await store.StoreTradeRoutesAsync(TradeRoutes(source, sourceSeconds, "Route of the source"), cancellationToken);
        }

        if (targetSeconds >= 0)
        {
            await store.StoreTradeRoutesAsync(TradeRoutes(target, targetSeconds, "Route of the target"), cancellationToken);
        }

        // Act
        var result = await store.MergeProfileAsync(source, target, cancellationToken);

        // Assert
        var kept = await store.ReadTradeRoutesAsync(target, cancellationToken);

        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The merge should be done");
        Assert.IsNotNull(kept, "The target should hold a set of trade routes");
        Assert.AreEqual(target, kept.ProfileId, "The set should belong to the target");
        Assert.AreSequenceEqual(new[] { expectedRoute }, kept.Routes.Select(route => route.Name).ToList(), "The set with the later receive time should stay");
        Assert.AreEqual(ReceiveTime(Math.Max(sourceSeconds, targetSeconds)), kept.ReceiveTime, "The set should keep its receive time");
        Assert.IsNull(await store.ReadTradeRoutesAsync(source, cancellationToken), "The source should hold no set of trade routes");
        Assert.AreEqual("1 1 1", CountTradeRouteRows(databaseFile), "Exactly one set with its route and station should be stored");
    }

    /// <summary>
    /// A merge into a target the store does not hold creates the target without a name and moves the history to it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreMergeIntoUnstoredTargetCreatesUnnamed()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;
        var tick = Tick(source, 10, 10, Island(1, "SrcOne", Entry(1, 1f, 1)));
        var routes = TradeRoutes(source, 10, "Route of the source");

        await store.StoreProfileAsync(source, SourceName, cancellationToken);
        await store.StoreTickAsync(tick, cancellationToken);
        await store.StoreTradeRoutesAsync(routes, cancellationToken);

        // Act
        var result = await store.MergeProfileAsync(source, target, cancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, result.Outcome, "The merge should be done");
        Assert.AreSequenceEqual(new[] { $"{target} <no name>" }, (await store.ListProfilesAsync(cancellationToken)).Select(profile => $"{profile.Id} {profile.Name ?? "<no name>"}").ToList(), "Only the target should be listed, without a name");
        Assert.AreEqual(target, (await store.ReadTickAsync(tick.Id, cancellationToken))?.ProfileId, "The tick should be read under the target");
        Assert.AreSequenceEqual(_expectedMovedIslands, DescribeIslands(await store.ListIslandsAsync(target, cancellationToken)), "The island should be listed under the target");
        Assert.AreEqual(target, (await store.ReadTradeRoutesAsync(target, cancellationToken))?.ProfileId, "The set of trade routes should belong to the target");
    }

    /// <summary>
    /// A merge of a source the store does not hold reports nothing to do and changes nothing, whether the target is
    /// stored or not.
    /// </summary>
    /// <param name="isTargetStored">Whether the target is stored</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ServerStoreMergeOfUnstoredSourceChangesNothing(bool isTargetStored)
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var target = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;

        if (isTargetStored)
        {
            await store.StoreProfileAsync(target, TargetName, cancellationToken);
            await store.StoreTickAsync(Tick(target, 10, 10, Island(1, "TgtOne", Entry(1, 1f, 1))), cancellationToken);
        }

        var before = ReadScalar(databaseFile, command => command.CommandText = SnapshotSql);

        // Act
        var result = await store.MergeProfileAsync(Guid.NewGuid(), target, cancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.NothingToDo, result.Outcome, "The merge of an unknown source should report nothing to do");
        Assert.AreEqual(before, ReadScalar(databaseFile, command => command.CommandText = SnapshotSql), $"No row should change, and no target should be created: target stored {isTargetStored}");
    }

    /// <summary>
    /// A merge that fails at its last step changes nothing: every row, the profiles, ticks, islands, hourly points,
    /// folded tick identities and trade routes of both profiles, reads as before, and the failure is reported with its
    /// exception.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreMergeFailureChangesNothing()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var history = new HistoryStore(_databases[^1]);
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;
        var (sourceTicks, targetTicks) = CreateCombineTicks(source, target);

        await store.StoreProfileAsync(source, SourceName, cancellationToken);
        await store.StoreProfileAsync(target, TargetName, cancellationToken);
        await StoreTicksAsync(store, [.. sourceTicks, .. targetTicks]);
        await store.StoreTradeRoutesAsync(TradeRoutes(source, 20, "Route of the source"), cancellationToken);
        await store.StoreTradeRoutesAsync(TradeRoutes(target, 10, "Route of the target"), cancellationToken);
        await FoldAsync(history, source);
        await FoldAsync(history, target);
        Execute(databaseFile, command => command.CommandText = CreateFailingDeleteTriggerSql);

        var before = ReadScalar(databaseFile, command => command.CommandText = SnapshotSql);

        // Act
        var result = await store.MergeProfileAsync(source, target, cancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Failed, result.Outcome, "A failing merge should be reported as failed");
        Assert.IsInstanceOfType<SqliteException>(result.Failure, "The SQLite exception behind the failure should be attached");
        Assert.IsNotEmpty(before, "The snapshot should hold rows, or the comparison proves nothing");
        Assert.AreEqual(before, ReadScalar(databaseFile, command => command.CommandText = SnapshotSql), "Every row should read as before the merge");
        Assert.AreEqual(source, (await store.ReadTickAsync(sourceTicks[^1].Id, cancellationToken))?.ProfileId, "The source's tick should still be read under the source");
    }

    /// <summary>
    /// A tick sent again after a merge is reported as already stored, wherever it is sent: under the target or under the
    /// merged identity, which is not recreated by it; a new tick under the merged identity recreates it without a name
    /// (spec E13).
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="isUnderSource">Whether the tick is sent under the merged identity</param>
    /// <param name="isNewTick">Whether the tick is a new one</param>
    /// <param name="expected">The expected outcome</param>
    /// <param name="isSourceListed">Whether the merged identity is listed afterwards</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("resent under the target", false, false, ServerStoreOutcome.AlreadyStored, false)]
    [DataRow("resent under the merged identity", true, false, ServerStoreOutcome.AlreadyStored, false)]
    [DataRow("new under the merged identity", true, true, ServerStoreOutcome.Stored, true)]
    public async Task ServerStoreMergeResentTickReportedAsStored(string label, bool isUnderSource, bool isNewTick, ServerStoreOutcome expected, bool isSourceListed)
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var cancellationToken = TestContext.CancellationToken;
        var tick = Tick(source, 10, 10, Island(1, "SrcOne", Entry(1, 1f, 1)));

        await store.StoreProfileAsync(source, SourceName, cancellationToken);
        await store.StoreProfileAsync(target, TargetName, cancellationToken);
        await store.StoreTickAsync(tick, cancellationToken);
        await store.MergeProfileAsync(source, target, cancellationToken);

        var resentUnder = isUnderSource ? source : target;
        var sent = isNewTick ? Tick(source, 40, 40, Island(2, "SrcTwo", Entry(1, 1f, 1))) : CopyOf(tick, resentUnder);

        // Act
        var result = await store.StoreTickAsync(sent, cancellationToken);

        // Assert
        var profiles = (await store.ListProfilesAsync(cancellationToken)).Select(profile => $"{profile.Id} {profile.Name ?? "<no name>"}").ToList();

        Assert.AreEqual(expected, result.Outcome, $"The tick should be reported as expected: {label}");
        Assert.AreEqual(isSourceListed, profiles.Contains($"{source} <no name>"), $"The merged identity should be listed, without a name, only for a new tick: {label}");
        Assert.Contains($"{target} {TargetName}", profiles, $"The target should stay listed with its name: {label}");
    }

    /// <summary>
    /// Creates the ticks of the combine scenario: the source's three ticks to fold (two in the first hour, one three
    /// hours before it) and one later one, and the target's one to fold (in the first hour) and one later one.
    /// </summary>
    /// <param name="source">The profile of the source's ticks</param>
    /// <param name="target">The profile of the target's ticks</param>
    /// <returns>The source's ticks, the folded ones first, and the target's</returns>
    private static (List<ContractTick> Source, List<ContractTick> Target) CreateCombineTicks(Guid source, Guid target)
    {
        List<ContractTick> sourceTicks = [
                                             Tick(source, 10, 1000, Island(1, "SrcOne", Entry(1, 2f, 4, Map(100, 2), Map(200, 1)), Entry(2, 6f, 1)), Island(2, "SrcTwo", Entry(1, 5f, 2))),
                                             Tick(source, 50, 1500, Island(1, "SrcOneLate", Entry(1, 4f, 3, Map(100, 3, 101, 5), Map(200, 4)))),
                                             Tick(source, -(3 * 3600) + 5, 700, Island(3, "SrcThree", Entry(1, 7f, 2, Map(100, 1), Map(200, 1)))),
                                             Tick(source, (2 * 3600) + 1, 9000, Island(1, "SrcLatest", Entry(1, 1f, 1)))
                                         ];
        List<ContractTick> targetTicks = [
                                             Tick(target, 30, 1200, Island(1, "TgtOne", Entry(1, 8f, 5, Map(100, 1), Map(200, 2, 201, 1)), Entry(3, 1f, 1))),
                                             Tick(target, (2 * 3600) + 2, 9100, Island(1, "TgtLatest", Entry(1, 1f, 1)))
                                         ];

        return (sourceTicks, targetTicks);
    }

    /// <summary>
    /// Creates the series query of an island and a good over the hours of the tests.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island key number</param>
    /// <param name="product">The product GUID</param>
    /// <returns>The query</returns>
    private static HistorySeriesQuery Query(Guid profileId, int island, int product)
    {
        return new HistorySeriesQuery
               {
                   ProfileId = profileId,
                   SessionGuid = island,
                   IslandId = (byte)island,
                   AreaIndex = 0,
                   ProductGuid = product,
                   RangeStart = _hour.AddHours(-5),
                   RangeEnd = _hour.AddHours(5)
               };
    }

    /// <summary>
    /// Describes the points of a series as text, with every value.
    /// </summary>
    /// <param name="points">The points</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeSeries(IReadOnlyList<HistoryPoint> points)
    {
        return points.Select(point => string.Join(" ",
                                                  point.Time.UtcTicks,
                                                  point.IsHourly,
                                                  point.TickCount,
                                                  point.FirstGameTime,
                                                  point.LastGameTime,
                                                  point.IslandName,
                                                  Format(point.Generation),
                                                  Format(point.Consumption),
                                                  Format(point.Delta),
                                                  Format(point.PerfectGeneration),
                                                  Format(point.PerfectConsumption),
                                                  Format(point.Income),
                                                  Format(point.SummedProductivity),
                                                  Format(point.AverageProductivity),
                                                  Format(point.BuildingCount),
                                                  Format(point.Maintenance),
                                                  Format(point.Profit),
                                                  string.Join(",", point.WorkforceAmounts.Select(amount => $"{amount.ItemGuid}={Format(amount.Amount)}")),
                                                  string.Join(",", point.BuildingAmounts.Select(amount => $"{amount.ItemGuid}={Format(amount.Amount)}")))).ToList();
    }

    /// <summary>
    /// Formats a value, round-trip, or a dash for none.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string Format(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture) ?? "-";
    }

    /// <summary>
    /// Creates a copy of a tick with a new profile and the same identity.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="profileId">The profile identity of the copy</param>
    /// <returns>The copy</returns>
    private static ContractTick CopyOf(ContractTick tick, Guid profileId)
    {
        return new ContractTick
               {
                   Id = tick.Id,
                   ProfileId = profileId,
                   ReceiveTime = tick.ReceiveTime,
                   GameTime = tick.GameTime,
                   Islands = tick.Islands
               };
    }

    /// <summary>
    /// Gives the receive time some seconds after the first hour.
    /// </summary>
    /// <param name="seconds">The seconds</param>
    /// <returns>The receive time, in UTC</returns>
    private static DateTimeOffset ReceiveTime(long seconds)
    {
        return _hour.AddSeconds(seconds);
    }

    /// <summary>
    /// Creates a tick of a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the first hour</param>
    /// <param name="gameTime">The game time</param>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static ContractTick Tick(Guid profileId, long seconds, long gameTime, params ContractTickIsland[] islands)
    {
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = ReceiveTime(seconds),
                   GameTime = gameTime,
                   Islands = islands
               };
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
    /// Creates an entry whose every value derives from the generation and the building count.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="generation">The generation</param>
    /// <param name="buildings">The building count</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry(int productGuid, float generation, int buildings)
    {
        return Entry(productGuid, generation, buildings, Map(100, 1), Map(200, 1));
    }

    /// <summary>
    /// Creates an entry whose every value derives from the generation and the building count.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="generation">The generation</param>
    /// <param name="buildings">The building count</param>
    /// <param name="workforce">The workforce amounts</param>
    /// <param name="buildingAmounts">The building amounts</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry(int productGuid, float generation, int buildings, Dictionary<int, int> workforce, Dictionary<int, int> buildingAmounts)
    {
        return new ContractTickEntry
               {
                   ProductGuid = productGuid,
                   Generation = generation,
                   Consumption = generation + 0.5f,
                   Delta = generation - 1f,
                   PerfectGeneration = generation * 2f,
                   PerfectConsumption = generation * 3f,
                   BuildingCount = buildings,
                   Maintenance = buildings * 10,
                   Income = generation * 0.25f,
                   Profit = buildings * -3,
                   SummedProductivity = generation + 1f,
                   AverageProductivity = generation / 4f,
                   WorkforceAmounts = workforce,
                   BuildingAmounts = buildingAmounts
               };
    }

    /// <summary>
    /// Creates a map of pairs.
    /// </summary>
    /// <param name="pairs">The keys and values in turn</param>
    /// <returns>The map</returns>
    private static Dictionary<int, int> Map(params int[] pairs)
    {
        var map = new Dictionary<int, int>();

        for (var index = 0; index < pairs.Length; index += 2)
        {
            map[pairs[index]] = pairs[index + 1];
        }

        return map;
    }

    /// <summary>
    /// Creates a set of one route with one station.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="seconds">The receive time in seconds after the first hour</param>
    /// <param name="name">The route name</param>
    /// <returns>The set</returns>
    private static ContractTradeRoutes TradeRoutes(Guid profileId, long seconds, string name)
    {
        return new ContractTradeRoutes
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = ReceiveTime(seconds),
                   Routes = [
                                new ContractTradeRoute
                                {
                                    Id = 1,
                                    Name = name,
                                    Folder = string.Empty,
                                    MajorProblems = 0,
                                    MinorProblems = 0,
                                    Ships = 1,
                                    LostShips = 0,
                                    Stations = [
                                                   new ContractTradeRouteStation
                                                   {
                                                       SessionGuid = 1,
                                                       IslandId = 1,
                                                       AreaIndex = 0
                                                   }
                                               ]
                                }
                            ]
               };
    }

    /// <summary>
    /// Describes islands as text in their stored order.
    /// </summary>
    /// <param name="islands">The islands</param>
    /// <returns>The lines, ordered by their key</returns>
    private static List<string> DescribeIslands(IReadOnlyList<StoredIsland> islands)
    {
        return islands.OrderBy(island => island.SessionGuid).ThenBy(island => island.IslandId).Select(island => $"{island.SessionGuid}/{island.IslandId}/{island.AreaIndex} {island.Name}").ToList();
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
    /// Runs one statement over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="setCommandText">Sets the command's text</param>
    private static void Execute(string databaseFile, Action<SqliteCommand> setCommandText)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        setCommandText(command);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Reads the first value of a query over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="setCommandText">Sets the command's text and parameters</param>
    /// <returns>The value as text</returns>
    private static string ReadScalar(string databaseFile, Action<SqliteCommand> setCommandText)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        setCommandText(command);

        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Sets the statement that counts the ticks of a profile.
    /// </summary>
    /// <param name="command">The command</param>
    /// <param name="profileId">The profile identity</param>
    private static void SetTickCount(SqliteCommand command, Guid profileId)
    {
        command.CommandText = CountTicksSql;
        command.Parameters.AddWithValue("$profile", profileId);
    }

    /// <summary>
    /// Counts the hourly rows of a profile.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The counts of points, islands, entries, map pairs and folded tick identities, separated by spaces</returns>
    private static string CountHourly(string databaseFile, Guid profileId)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        command.CommandText = CountHourlySql;
        command.Parameters.AddWithValue("$profile", profileId);

        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Counts the rows of the trade-route tables.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The counts of sets, routes and stations, separated by spaces</returns>
    private static string CountTradeRouteRows(string databaseFile)
    {
        return ReadScalar(databaseFile, command => command.CommandText = CountTradeRoutesSql);
    }

    /// <summary>
    /// Folds every tick of a profile that is old enough.
    /// </summary>
    /// <param name="history">The history store</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task FoldAsync(HistoryStore history, Guid profileId)
    {
        for (var step = 0; step < MaxSteps; step++)
        {
            var result = await history.FoldNextTickAsync(profileId, _foldTime, TestContext.CancellationToken);

            if (result.Outcome != HistoryOutcome.Done)
            {
                return;
            }
        }

        throw new InvalidOperationException("The fold did not end");
    }

    /// <summary>
    /// Creates the path of a database file in a new temporary directory.
    /// </summary>
    /// <returns>The full path of the database file, not yet created</returns>
    private string CreateDatabaseFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-server-merge-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        return Path.Combine(directory, DatabaseFileName);
    }

    /// <summary>
    /// Opens a database and a store on a database file, and keeps the database for the cleanup.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <returns>The store</returns>
    private async Task<ServerStore> OpenStoreAsync(string databaseFile)
    {
        var result = await ServerDatabase.OpenAsync(databaseFile, new LoggerFactory(), TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        return new ServerStore(database);
    }

    /// <summary>
    /// Stores ticks in order.
    /// </summary>
    /// <param name="store">The store</param>
    /// <param name="ticks">The ticks</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task StoreTicksAsync(ServerStore store, params ContractTick[] ticks)
    {
        foreach (var tick in ticks)
        {
            await store.StoreTickAsync(tick, TestContext.CancellationToken);
        }
    }

    #endregion // Methods
}