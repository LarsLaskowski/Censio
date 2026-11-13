using System.Globalization;
using System.Text;

using Censio.Companion.Core;
using Censio.Contracts;
using Censio.Pipe;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="PageReader" />: over a <see cref="FakeProfileStateSource" /> for the one read at a time, and over a
/// real <see cref="ServerState" /> fed with the pipe fixture <c>long-session.jsonl</c> for the views of AC1.
/// </summary>
[TestClass]
public sealed class PageReaderTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The text of an island without values, in the descriptions.
    /// </summary>
    private const string NoValues = "<no values>";

    /// <summary>
    /// The region GUID the catalog of the merge test gives two of the fixture's sessions.
    /// </summary>
    private const int MergedRegionGuid = 900001;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The longest the test waits for a read.
    /// </summary>
    private static readonly TimeSpan _waitBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The profile the fixture's ticks are stored for.
    /// </summary>
    private static readonly Guid _fixtureProfile = new("7e57f1c7-0000-4000-8000-000000000001");

    /// <summary>
    /// The disposables of the running test.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PageReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PageReaderTests(TestContext testContext)
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
    /// Pages that read profiles not held at the same time cause one source read at a time: while the first read is in
    /// flight no other starts, and every page gets its view afterwards.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PageReaderNotHeldReadsOneAtATime()
    {
        // Arrange
        var source = CreateSource(true);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        source.Store(first, StoredRead(CreateState(first)));
        source.Store(second, StoredRead(CreateState(second)));

        var reader = new PageReader(source);
        var firstRead = reader.ReadAsync(first, null, TestContext.CancellationToken);

        await source.WaitForReadAsync(_waitBound, TestContext.CancellationToken);

        // Act
        var secondRead = reader.ReadAsync(second, null, TestContext.CancellationToken);
        var thirdRead = reader.ReadAsync(first, null, TestContext.CancellationToken);
        var readsWhileFirstInFlight = source.Reads.Count;

        source.ReleaseReads();

        var reads = await Task.WhenAll(firstRead, secondRead, thirdRead).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, readsWhileFirstInFlight, "No read should start while the first is in flight");
        Assert.AreEqual(1, source.MaxActive, "At most one read should be in flight at a time");
        Assert.AreSequenceEqual(new[] { first, second, first }, reads.Select(read => read.View?.ProfileId ?? Guid.Empty).ToList(), "Every page should get the view of its profile");
    }

    /// <summary>
    /// A profile held while a page checks it but evicted at the check is read inside the gate: its read waits for the read
    /// in flight.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PageReaderEvictedBetweenCheckAndReadReadsInsideGate()
    {
        // Arrange
        var source = CreateSource(true);
        var first = Guid.NewGuid();
        var evicted = Guid.NewGuid();

        source.Store(first, StoredRead(CreateState(first)));
        source.Hold(CreateState(evicted));
        source.Store(evicted, StoredRead(CreateState(evicted)));
        source.EvictOnCheck(evicted);

        var reader = new PageReader(source);
        var firstRead = reader.ReadAsync(first, null, TestContext.CancellationToken);

        await source.WaitForReadAsync(_waitBound, TestContext.CancellationToken);

        // Act
        var evictedRead = reader.ReadAsync(evicted, null, TestContext.CancellationToken);
        var readsWhileFirstInFlight = source.Reads.Count;

        source.ReleaseReads();

        var reads = await Task.WhenAll(firstRead, evictedRead).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, readsWhileFirstInFlight, "The evicted profile's read should wait for the read in flight");
        Assert.AreSequenceEqual(new[] { first, evicted }, source.Reads.ToList(), "The evicted profile should be read after the first");
        Assert.AreEqual(1, source.MaxActive, "At most one read should be in flight at a time");
        Assert.AreEqual(evicted, reads[1].View?.ProfileId, "The page should get the view of the evicted profile");
    }

    /// <summary>
    /// A held profile is built from its held state without a source read and without waiting for a read in flight.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PageReaderHeldProfileReadsWithoutWaiting()
    {
        // Arrange
        var source = CreateSource(true);
        var first = Guid.NewGuid();
        var held = Guid.NewGuid();

        source.Store(first, StoredRead(CreateState(first)));
        source.Hold(CreateState(held));

        var reader = new PageReader(source);
        var firstRead = reader.ReadAsync(first, null, TestContext.CancellationToken);

        await source.WaitForReadAsync(_waitBound, TestContext.CancellationToken);

        // Act
        var heldRead = await reader.ReadAsync(held, null, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        source.ReleaseReads();
        await firstRead.WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreEqual(held, heldRead.View?.ProfileId, "The held profile's view should be built while another read is in flight");
        Assert.AreSequenceEqual(new[] { first }, source.Reads.ToList(), "The held profile should cause no source read");
    }

    /// <summary>
    /// A page whose token is cancelled while it waits for the gate ends with the cancellation and causes no read, and the
    /// gate lets the next page read afterwards.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PageReaderCancelledWaiterLeavesWithoutRead()
    {
        // Arrange
        var source = CreateSource(true);
        var first = Guid.NewGuid();
        var waiting = Guid.NewGuid();
        var later = Guid.NewGuid();

        source.Store(first, StoredRead(CreateState(first)));
        source.Store(waiting, StoredRead(CreateState(waiting)));
        source.Store(later, StoredRead(CreateState(later)));

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var reader = new PageReader(source);
        var firstRead = reader.ReadAsync(first, null, TestContext.CancellationToken);

        await source.WaitForReadAsync(_waitBound, TestContext.CancellationToken);

        var waitingRead = reader.ReadAsync(waiting, null, cancellation.Token);

        // Act
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => waitingRead.WaitAsync(_waitBound, TestContext.CancellationToken), "The cancelled page should end with the cancellation");

        source.ReleaseReads();
        await firstRead.WaitAsync(_waitBound, TestContext.CancellationToken);

        var laterRead = await reader.ReadAsync(later, null, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreEqual(later, laterRead.View?.ProfileId, "A later page should read once the read in flight ended");
        Assert.AreSequenceEqual(new[] { first, later }, source.Reads.ToList(), "The cancelled page should cause no read");
    }

    /// <summary>
    /// A profile the source does not know gives no view and no failure.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task PageReaderUnknownProfileGivesNoView()
    {
        // Arrange
        var reader = new PageReader(CreateSource(false));

        // Act
        var read = await reader.ReadAsync(Guid.NewGuid(), null, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(read.View, "An unknown profile should give no view");
        Assert.IsFalse(read.IsFailed, "An unknown profile should not count as a failed read");
    }

    /// <summary>
    /// A failed source read gives a failure without a view.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task PageReaderFailedReadGivesFailure()
    {
        // Arrange
        var source = CreateSource(false);
        var profileId = Guid.NewGuid();

        source.Store(profileId,
                     new ProfileStateRead
                     {
                         State = null,
                         Failure = new SqliteException("test read failure", 1)
                     });

        var reader = new PageReader(source);

        // Act
        var read = await reader.ReadAsync(profileId, null, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(read.View, "A failed read should give no view");
        Assert.IsTrue(read.IsFailed, "A failed read should be reported as failed");
    }

    /// <summary>
    /// The fixture replayed without a catalog up to its last tick with data shows three regions, the session GUIDs 3245,
    /// 6627 and 149679 with 13, 9 and 2 islands.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PageReaderFixtureWithoutCatalogGivesThreeRegions()
    {
        // Arrange
        var ticks = await ReplayFixtureAsync();
        var state = await StoreTicksAsync(UpToLastTickWithData(ticks));
        var reader = new PageReader(state);

        // Act
        var read = await reader.ReadAsync(_fixtureProfile, null, TestContext.CancellationToken);

        // Assert
        string[] expected = ["3245 session [3245] islands 13", "6627 session [6627] islands 9", "149679 session [149679] islands 2"];

        Assert.AreSequenceEqual(expected, DescribeRegions(read.View), "Without a catalog each session should be a region with its islands");
    }

    /// <summary>
    /// Up to the fixture's last tick with data, each island is named by the latest tick that carries it, the renamed island
    /// (6627, 10, 1) included, and its detail holds exactly the goods of the last tick with data with their values, or no
    /// values when that tick lacks the island.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PageReaderFixtureGivesLatestNamesAndGoods()
    {
        // Arrange
        var ticks = UpToLastTickWithData(await ReplayFixtureAsync());
        var state = await StoreTicksAsync(ticks);
        var reader = new PageReader(state);
        var expected = ExpectedIslands(ticks);
        var renamedNames = ticks.SelectMany(tick => tick.Islands).Where(island => island is { SessionGuid: 6627, IslandId: 10, AreaIndex: 1 }).Select(island => island.Name).Distinct(StringComparer.Ordinal).Count();

        // Act
        var actual = await DescribeEveryIslandAsync(reader, ticks);

        // Assert
        Assert.AreEqual(2, renamedNames, "The island (6627, 10, 1) should carry two names in the fixture, or the test would prove less");
        Assert.HasCount(24, expected, "The fixture should have 24 islands, or the test would prove less");
        Assert.AreSequenceEqual(expected, actual, "Each island should show its latest name and the goods of the last tick with data");
    }

    /// <summary>
    /// After the fixture's final tick without data the 24 islands stay listed, and no island or region has values.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PageReaderFixtureFinalEmptyTickKeepsIslandsWithoutValues()
    {
        // Arrange
        var ticks = await ReplayFixtureAsync();
        var state = await StoreTicksAsync(ticks);
        var reader = new PageReader(state);
        var island = ticks.SelectMany(tick => tick.Islands).First(tick => tick.Entries.Count > 0);

        // Act
        var read = await reader.ReadAsync(_fixtureProfile, new IslandKey(island.SessionGuid, island.IslandId, island.AreaIndex), TestContext.CancellationToken);

        // Assert
        var view = read.View ?? throw new AssertFailedException("The fixture's profile should have a view");
        var islands = view.Regions.SelectMany(region => region.Islands).ToList();

        Assert.HasCount(24, islands, "The 24 islands should stay listed");
        Assert.IsEmpty(islands.Where(listed => listed.Figures is not null).ToList(), "No island should have values after the empty tick");
        Assert.IsEmpty(view.Regions.Where(region => region.Figures is not null).ToList(), "No region should have values after the empty tick");
        Assert.IsEmpty(view.Rows, "An island's detail should show no goods after the empty tick");
    }

    /// <summary>
    /// With a catalog whose region entries give the sessions 3245 and 6627 one region GUID, the two form one region with
    /// the sum of their islands, named by the entry of the smaller session; 149679 stays a region of its own.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    public async Task PageReaderFixtureCatalogMergesSessionsOfOneRegion()
    {
        // Arrange
        var ticks = await ReplayFixtureAsync();
        var state = await StoreTicksAsync(UpToLastTickWithData(ticks));
        var (upload, document) = CreateMergingCatalog();

        await state.StoreCatalogAsync(upload, document, TestContext.CancellationToken);

        var reader = new PageReader(state);

        // Act
        var read = await reader.ReadAsync(_fixtureProfile, null, TestContext.CancellationToken);

        // Assert
        string[] expected = ["149679 session [149679] islands 2", "900001 region [3245,6627] islands 22"];
        var merged = read.View?.Regions.SingleOrDefault(region => region.RegionGuid == MergedRegionGuid);

        Assert.AreSequenceEqual(expected, DescribeRegions(read.View), "The two sessions of one catalog region should form one region");
        Assert.AreEqual("Latium | Latium DE", merged is null ? "<no region>" : $"{merged.Name.English} | {merged.Name.German}", "The merged region should be named by the entry of its smaller session");
    }

    /// <summary>
    /// Creates a state of a profile with one island and one entry.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The state</returns>
    private static StoredProfileState CreateState(Guid profileId)
    {
        return new StoredProfileState
               {
                   Profile = new StoredProfile
                             {
                                 Id = profileId,
                                 Name = "Profile 3a9e"
                             },
                   LatestTick = new ContractTick
                                {
                                    Id = Guid.NewGuid(),
                                    ProfileId = profileId,
                                    ReceiveTime = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero),
                                    GameTime = 3_600_000,
                                    Islands = [
                                                  new ContractTickIsland
                                                  {
                                                      SessionGuid = 10,
                                                      IslandId = 1,
                                                      AreaIndex = 0,
                                                      SessionId = 1,
                                                      Name = "Island 1",
                                                      Entries = [
                                                                    new ContractTickEntry
                                                                    {
                                                                        ProductGuid = 1,
                                                                        Generation = 1f,
                                                                        Consumption = 1f,
                                                                        Delta = 0f,
                                                                        PerfectGeneration = 1f,
                                                                        PerfectConsumption = 1f,
                                                                        BuildingCount = 1,
                                                                        Maintenance = 0,
                                                                        Income = 0f,
                                                                        Profit = 0,
                                                                        SummedProductivity = 1f,
                                                                        AverageProductivity = 100f,
                                                                        WorkforceAmounts = new Dictionary<int, int>(),
                                                                        BuildingAmounts = new Dictionary<int, int>()
                                                                    }
                                                                ]
                                                  }
                                              ]
                                },
                   Islands = [
                                 new StoredIsland
                                 {
                                     SessionGuid = 10,
                                     IslandId = 1,
                                     AreaIndex = 0,
                                     Name = "Island 1"
                                 }
                             ]
               };
    }

    /// <summary>
    /// Gives the read result of a stored state.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The read result</returns>
    private static ProfileStateRead StoredRead(StoredProfileState state)
    {
        return new ProfileStateRead
               {
                   State = state,
                   Failure = null
               };
    }

    /// <summary>
    /// Gives the ticks up to and including the last tick with data.
    /// </summary>
    /// <param name="ticks">The ticks in replay order</param>
    /// <returns>The ticks</returns>
    private static List<ContractTick> UpToLastTickWithData(List<ContractTick> ticks)
    {
        var last = ticks.FindLastIndex(tick => tick.Islands.Any(island => island.Entries.Count > 0));

        return ticks[..(last + 1)];
    }

    /// <summary>
    /// Describes the expected detail of each island of the ticks, ordered by key: the name of the latest tick that carries
    /// the island, and the goods of the last tick with their values, or no values when it lacks the island.
    /// </summary>
    /// <param name="ticks">The ticks in storing order, the last one with data</param>
    /// <returns>The lines</returns>
    private static List<string> ExpectedIslands(List<ContractTick> ticks)
    {
        var last = ticks[^1];

        return [
                   .. ticks.SelectMany(tick => tick.Islands)
                           .GroupBy(island => new IslandKey(island.SessionGuid, island.IslandId, island.AreaIndex))
                           .OrderBy(group => group.Key.SessionGuid)
                           .ThenBy(group => group.Key.IslandId)
                           .ThenBy(group => group.Key.AreaIndex)
                           .Select(group => DescribeIsland(group.Key, group.Last().Name, ExpectedGoods(last, group.Key)))
               ];
    }

    /// <summary>
    /// Describes the goods of an island in a tick, ordered by product GUID, or no values when the tick lacks the island or
    /// it has no entries.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="key">The island key</param>
    /// <returns>The text</returns>
    private static string ExpectedGoods(ContractTick tick, IslandKey key)
    {
        var island = tick.Islands.FirstOrDefault(candidate => new IslandKey(candidate.SessionGuid, candidate.IslandId, candidate.AreaIndex) == key);

        if (island is null || island.Entries.Count == 0)
        {
            return NoValues;
        }

        return string.Join(';', island.Entries.OrderBy(entry => entry.ProductGuid).Select(entry => DescribeGood(entry.ProductGuid, entry.Generation, entry.Consumption, entry.Delta, entry.BuildingCount, entry.AverageProductivity)));
    }

    /// <summary>
    /// Describes the shown goods of a view, ordered by product GUID, or no values when it has none.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The text</returns>
    private static string ShownGoods(ProfileView view)
    {
        if (view.Rows.Count == 0)
        {
            return NoValues;
        }

        return string.Join(';', view.Rows.OrderBy(row => row.ProductGuid).Select(row => DescribeGood(row.ProductGuid, row.Production, row.Consumption, row.Delta, row.Buildings, row.Productivity)));
    }

    /// <summary>
    /// Describes a good with its values as their bit patterns.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="production">The production</param>
    /// <param name="consumption">The consumption</param>
    /// <param name="delta">The delta</param>
    /// <param name="buildings">The building count</param>
    /// <param name="productivity">The productivity</param>
    /// <returns>The text</returns>
    private static string DescribeGood(int productGuid, float? production, float? consumption, float? delta, int buildings, float? productivity)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{productGuid} {Bits(production)} {Bits(consumption)} {Bits(delta)} {buildings} {Bits(productivity)}");
    }

    /// <summary>
    /// Describes a float by its bit pattern.
    /// </summary>
    /// <param name="value">The float, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string Bits(float? value)
    {
        return value is null
                   ? "null"
                   : BitConverter.SingleToInt32Bits(value.Value).ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Describes an island of the detail.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="name">The name</param>
    /// <param name="goods">The goods</param>
    /// <returns>The text</returns>
    private static string DescribeIsland(IslandKey key, string name, string goods)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{key.SessionGuid}/{key.IslandId}/{key.AreaIndex} {name}: {goods}");
    }

    /// <summary>
    /// Describes the regions of a view, ordered by GUID: GUID, kind, sessions and island count.
    /// </summary>
    /// <param name="view">The view, or <see langword="null" /></param>
    /// <returns>The lines</returns>
    private static List<string> DescribeRegions(ProfileView? view)
    {
        if (view is null)
        {
            return ["<no view>"];
        }

        return [.. view.Regions.OrderBy(region => region.RegionGuid).Select(region => string.Create(CultureInfo.InvariantCulture, $"{region.RegionGuid} {(region.IsSession ? "session" : "region")} [{string.Join(',', region.SessionGuids)}] islands {region.Islands.Count}"))];
    }

    /// <summary>
    /// Creates a catalog whose region entries give the sessions 3245 and 6627 one region GUID.
    /// </summary>
    /// <returns>The upload and its document</returns>
    private static (ContractCatalogUpload Upload, CatalogDocument Document) CreateMergingCatalog()
    {
        var entries = string.Create(CultureInfo.InvariantCulture,
                                    $"{{\"guid\":6627,\"kind\":\"region\",\"names\":{{\"de\":\"Gallien\",\"en\":\"Gallia\"}},\"regionGuid\":{MergedRegionGuid},\"regionTypes\":null,\"icon\":null}},{{\"guid\":3245,\"kind\":\"region\",\"names\":{{\"de\":\"Latium DE\",\"en\":\"Latium\"}},\"regionGuid\":{MergedRegionGuid},\"regionTypes\":null,\"icon\":null}}");
        var bytes = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[{entries}]}}");
        var document = CatalogDocumentReader.Read(bytes).Document ?? throw new InvalidOperationException("The test catalog should be read");

        return (new ContractCatalogUpload
                {
                    Id = Guid.NewGuid(),
                    Document = bytes
                },
                document);
    }

    /// <summary>
    /// Creates a fake source, disposed after the test.
    /// </summary>
    /// <param name="holdReads">Whether every read waits until released</param>
    /// <returns>The source</returns>
    private FakeProfileStateSource CreateSource(bool holdReads)
    {
        var source = new FakeProfileStateSource(holdReads);

        _disposables.Add(source);

        return source;
    }

    /// <summary>
    /// Replays the fixture through the message reader and the tick assembler and converts each tick as the Companion does.
    /// </summary>
    /// <returns>The contract ticks in replay order</returns>
    private async Task<List<ContractTick>> ReplayFixtureAsync()
    {
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        using var replay = new PipeReplayStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", "long-session.jsonl")), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock);

        var ticks = new List<ContractTick>();
        var result = await assembler.ReadAsync(TestContext.CancellationToken);

        while (result.Kind != PipeTickResultKind.EndOfStream)
        {
            if (result.Tick is { } tick)
            {
                ticks.Add(ContractMessageConverter.ToTick(tick, _fixtureProfile));
            }

            result = await assembler.ReadAsync(TestContext.CancellationToken);
        }

        return ticks;
    }

    /// <summary>
    /// Stores ticks in order into a restored state over an in-memory database.
    /// </summary>
    /// <param name="ticks">The ticks</param>
    /// <returns>The state</returns>
    private async Task<ServerState> StoreTicksAsync(List<ContractTick> ticks)
    {
        var opened = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = opened.Database ?? throw new InvalidOperationException($"The database could not be opened: {opened.Kind}", opened.Failure);

        _disposables.Add(database);

        var state = new ServerState(new ServerStore(database), NullLogger.Instance);

        await state.RestoreAsync(TestContext.CancellationToken);

        foreach (var tick in ticks)
        {
            var stored = await state.StoreTickAsync(tick, TestContext.CancellationToken);

            if (stored.Outcome != ServerStoreOutcome.Stored)
            {
                throw new InvalidOperationException($"The fixture tick was not stored: {stored.Outcome}", stored.Failure);
            }
        }

        return state;
    }

    /// <summary>
    /// Reads the detail of each island of the ticks and describes its name and goods, ordered by key.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <param name="ticks">The ticks</param>
    /// <returns>The lines</returns>
    private async Task<List<string>> DescribeEveryIslandAsync(PageReader reader, List<ContractTick> ticks)
    {
        var keys = ticks.SelectMany(tick => tick.Islands)
                        .Select(island => new IslandKey(island.SessionGuid, island.IslandId, island.AreaIndex))
                        .Distinct()
                        .OrderBy(key => key.SessionGuid)
                        .ThenBy(key => key.IslandId)
                        .ThenBy(key => key.AreaIndex);
        var lines = new List<string>();

        foreach (var key in keys)
        {
            var view = (await reader.ReadAsync(_fixtureProfile, key, TestContext.CancellationToken)).View ?? throw new AssertFailedException("The fixture's profile should have a view");
            var island = view.Regions.SelectMany(region => region.Islands).Single(listed => listed.Key == key);

            lines.Add(DescribeIsland(key, island.Name, ShownGoods(view)));
        }

        return lines;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        SqliteConnection.ClearAllPools();
    }

    #endregion // IDisposable
}