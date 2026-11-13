using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Censio.Contracts;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Censio.Server.Data.Tests;

/// <summary>
/// Tests for <see cref="ServerStore" />, against real SQLite: a file in a temporary directory, or the in-memory database.
/// </summary>
[TestClass]
public sealed class ServerStoreTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// A profile name.
    /// </summary>
    private const string ProfileName = "Private profile name 7f3c";

    /// <summary>
    /// Another profile name.
    /// </summary>
    private const string OtherProfileName = "Renamed profile 2b81";

    /// <summary>
    /// An island name.
    /// </summary>
    private const string IslandName = "Private island name 9b2d";

    /// <summary>
    /// A product GUID no other value of the tests uses.
    /// </summary>
    private const int MarkedProductGuid = 192837465;

    /// <summary>
    /// A building count no other value of the tests uses.
    /// </summary>
    private const int MarkedBuildingCount = 918273;

    /// <summary>
    /// A float no other value of the tests uses.
    /// </summary>
    private const float MarkedFloat = 31415.9f;

    /// <summary>
    /// The text inside the catalog document of the tests.
    /// </summary>
    private const string CatalogMarker = "catalog-private-5e1a";

    /// <summary>
    /// The text inside the PNG bytes of the tests.
    /// </summary>
    private const string PngMarker = "png-private-c4d7";

    /// <summary>
    /// The text of the trigger's error that makes a write fail.
    /// </summary>
    private const string FailingWriteMessage = "test write failure";

    /// <summary>
    /// The statement that makes every insert into the map pairs fail, as a full disk would in the middle of a tick.
    /// </summary>
    private const string CreateFailingTriggerSql = "CREATE TRIGGER \"FailMapPairs\" BEFORE INSERT ON \"TickMapPairs\" BEGIN SELECT RAISE(ABORT, '" + FailingWriteMessage + "'); END;";

    /// <summary>
    /// The number of islands of a tick at the contract's bounds.
    /// </summary>
    private const int BoundIslands = 256;

    /// <summary>
    /// The number of entries per island of a tick at the contract's bounds (with three map pairs each, 524,288 entries
    /// and pairs in all).
    /// </summary>
    private const int BoundEntriesPerIsland = 512;

    /// <summary>
    /// The number of references of a catalog upload's missing-icons list at the contract's bound.
    /// </summary>
    private const int BoundIconReferences = 16384;

    /// <summary>
    /// The trigger that makes SQLite itself end the transaction of a tick write, before the store rolls it back.
    /// </summary>
    private const string CreateRollingBackTriggerSql = "CREATE TRIGGER \"RollBackMapPairs\" BEFORE INSERT ON \"TickMapPairs\" BEGIN SELECT RAISE(ROLLBACK, '" + FailingWriteMessage + "'); END;";

    /// <summary>
    /// The name of the SQL function that holds a tick write until the test has read.
    /// </summary>
    private const string PauseFunctionName = "censio_test_pause";

    /// <summary>
    /// The temporary trigger, on the store's own connection only, that holds a tick write at its first island row, when
    /// the tick, its islands, entries and map pairs are written but not committed.
    /// </summary>
    private const string CreatePauseTriggerSql = "CREATE TEMP TRIGGER \"PauseTickWrite\" AFTER INSERT ON main.\"Islands\" BEGIN SELECT " + PauseFunctionName + "(); END;";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The receive time the tests count their seconds from.
    /// </summary>
    private static readonly DateTimeOffset _baseReceiveTime = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The longest the test waits for a tick write to reach its hold and, once released, to finish.
    /// </summary>
    private static readonly TimeSpan _pauseBound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The longest a held tick write waits for the test's reads, a fail-safe only: reads that do not wait for the write
    /// release it at once, reads that wait for it see it only after this bound and fail the test.
    /// </summary>
    private static readonly TimeSpan _holdBound = TimeSpan.FromSeconds(5);

    /// <summary>
    /// What the reads see of a tick and its profile before the tick is stored.
    /// </summary>
    private static readonly string[] _stateBeforeWrite = ["tick <none>", "latest <none>", "islands 0", "profiles 0"];

    /// <summary>
    /// The islands <see cref="ServerStoreListIslandsGivesNameOfLatestReceiveTime" /> expects: key and latest name.
    /// </summary>
    private static readonly string[] _expectedLatestIslands = ["1/1/0 Name at three", "2/2/0 Second island"];

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    /// <summary>
    /// The provider that records what the database and the store log.
    /// </summary>
    private readonly RecordingLoggerProvider _loggerProvider = new();

    /// <summary>
    /// The logger factory handed to the database.
    /// </summary>
    private readonly LoggerFactory _loggerFactory;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStoreTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ServerStoreTests(TestContext testContext)
    {
        TestContext = testContext;
        _loggerFactory = new LoggerFactory([_loggerProvider]);
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
        CloseDatabases();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Without a database file the store keeps a tick in memory and reads it back with every value.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ServerStoreInMemoryStoresAndReadsTick()
    {
        // Arrange
        var store = await OpenInMemoryStoreAsync();
        var tick = CreateEveryValueTick(Guid.NewGuid());
        var stored = await store.StoreTickAsync(tick, TestContext.CancellationToken);

        // Act
        var readBack = await store.ReadTickAsync(tick.Id, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The tick should be stored in memory");
        Assert.AreSequenceEqual(DescribeTick(tick), DescribeTick(readBack), "The tick should read back with every value from memory");
    }

    /// <summary>
    /// Storing a profile, a tick, the catalog, an icon, every read and a failing write log no name and no stored value,
    /// with every log level on.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task ServerStoreLogsNoNameOrValue()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);

        // Act
        await RunEveryOperationAsync(store, databaseFile);

        // Assert
        var lines = _loggerProvider.Logger.Entries.Select(entry => $"{entry.Message} {entry.Exception}").ToList();

        Assert.IsNotEmpty(lines, "The operations should have logged at the trace level, or the check below would prove nothing");
        Assert.IsEmpty(FindLinesHolding(lines, ForbiddenLogTexts()), "No log line should hold a name or a stored value");
    }

    /// <summary>
    /// A stored profile is listed with its identity and name.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreStoreProfileListsIdentityAndName()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();

        // Act
        var result = await store.StoreProfileAsync(profileId, ProfileName, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The profile should be stored");
        Assert.AreSequenceEqual(new[] { DescribeProfile(profileId, ProfileName) }, DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), "The profile should be listed with its identity and name");
    }

    /// <summary>
    /// Storing a known identity with another name keeps one profile, which takes the new name.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreStoreKnownProfileWithNewNameKeepsOneProfile()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();

        await store.StoreProfileAsync(profileId, ProfileName, TestContext.CancellationToken);

        // Act
        var result = await store.StoreProfileAsync(profileId, OtherProfileName, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The new name should be stored");
        Assert.AreSequenceEqual(new[] { DescribeProfile(profileId, OtherProfileName) }, DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), "One profile with the new name should be listed");
    }

    /// <summary>
    /// A tick of a profile identity the store does not hold is stored and creates that profile without a name.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [TestCategory("AC8")]
    public async Task ServerStoreStoreTickOfUnknownProfileListsProfileWithoutName()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();
        var tick = CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1)));

        // Act
        var result = await store.StoreTickAsync(tick, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "A tick of an unknown profile should be stored");
        Assert.AreSequenceEqual(new[] { DescribeProfile(profileId, null) }, DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), "The profile should be created without a name");
        Assert.IsNotNull(await store.ReadTickAsync(tick.Id, TestContext.CancellationToken), "The tick should read back");
    }

    /// <summary>
    /// A profile created by a tick takes the name of its next hello.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ServerStoreStoreProfileNamesProfileCreatedByTick()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();

        await store.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        // Act
        var result = await store.StoreProfileAsync(profileId, ProfileName, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The name should be stored");
        Assert.AreSequenceEqual(new[] { DescribeProfile(profileId, ProfileName) }, DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), "The profile the tick created should take the name");
    }

    /// <summary>
    /// A stored tick, read back from the file through a new connection, has every value of the message equal, floats
    /// bit for bit with no value and negative zero kept.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStoreStoreTickReadsBackEveryValueFromNewConnection()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var tick = CreateEveryValueTick(Guid.NewGuid());
        var writer = await OpenStoreAsync(databaseFile);
        var stored = await writer.StoreTickAsync(tick, TestContext.CancellationToken);

        CloseDatabases();

        var reader = await OpenStoreAsync(databaseFile);

        // Act
        var readBack = await reader.ReadTickAsync(tick.Id, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The tick should be stored");
        Assert.AreSequenceEqual(DescribeTick(tick), DescribeTick(readBack), "The tick should read back with every value, floats bit for bit");
    }

    /// <summary>
    /// The islands of a profile are listed by key, each with the name of the tick with the latest receive time that
    /// carried it, not the name stored last; another profile's islands are not listed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStoreListIslandsGivesNameOfLatestReceiveTime()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();

        await StoreTicksAsync(store,
                              CreateTick(profileId, 2, CreateIsland(1, "Name at two", CreateEntry(1)), CreateIsland(2, "Second island", CreateEntry(1))),
                              CreateTick(profileId, 3, CreateIsland(1, "Name at three", CreateEntry(1))),
                              CreateTick(profileId, 1, CreateIsland(1, "Name at one, stored last", CreateEntry(1))),
                              CreateTick(Guid.NewGuid(), 4, CreateIsland(3, "Island of another profile", CreateEntry(1))));

        // Act
        var islands = await store.ListIslandsAsync(profileId, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(_expectedLatestIslands, DescribeIslands(islands), "Each island of the profile should be listed once with the name of its latest receive time");
    }

    /// <summary>
    /// A tick whose identity is stored is reported as already stored and changes nothing: not the tick, not the island
    /// names and not the profiles.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStoreStoreKnownTickIdReportsAlreadyStoredAndChangesNothing()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();
        var first = CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1)));
        var resend = CreateTick(first.Id, Guid.NewGuid(), 2, CreateIsland(1, "Name of the resend", CreateEntry(2)));

        await store.StoreTickAsync(first, TestContext.CancellationToken);

        // Act
        var result = await store.StoreTickAsync(resend, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.AlreadyStored, result.Outcome, "A known tick identity should be reported as already stored");
        Assert.AreSequenceEqual(DescribeTick(first), DescribeTick(await store.ReadTickAsync(first.Id, TestContext.CancellationToken)), "The stored tick should stay as it was");
        Assert.AreSequenceEqual(new[] { "1/1/0 " + IslandName }, DescribeIslands(await store.ListIslandsAsync(profileId, TestContext.CancellationToken)), "The island name should stay as it was");
        Assert.AreSequenceEqual(new[] { DescribeProfile(profileId, null) }, DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), "The resend's profile should not be created");
    }

    /// <summary>
    /// The latest tick of a profile is its own, also when a tick of another profile with a later receive time was
    /// stored later.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ServerStoreReadLatestTickIgnoresOtherProfile()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();
        var own = CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1)));

        await StoreTicksAsync(store, own, CreateTick(Guid.NewGuid(), 5, CreateIsland(1, IslandName, CreateEntry(1))));

        // Act
        var latest = await store.ReadLatestTickAsync(profileId, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(own.Id, latest?.Id, "The latest tick should be the profile's own");
    }

    /// <summary>
    /// Of two ticks of a profile with equal receive times the latest is the one stored last.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ServerStoreReadLatestTickWithEqualReceiveTimesGivesStoredLast()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();
        var storedLast = CreateTick(profileId, 3, CreateIsland(1, IslandName, CreateEntry(2)));

        await StoreTicksAsync(store, CreateTick(profileId, 3, CreateIsland(1, IslandName, CreateEntry(1))), storedLast);

        // Act
        var latest = await store.ReadLatestTickAsync(profileId, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(storedLast.Id, latest?.Id, "Of equal receive times the tick stored last should be the latest");
    }

    /// <summary>
    /// A tick stored later with an earlier receive time (a clock set back) is not the latest.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ServerStoreReadLatestTickIgnoresEarlierReceiveTimeStoredLater()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();
        var later = CreateTick(profileId, 5, CreateIsland(1, IslandName, CreateEntry(1)));

        await StoreTicksAsync(store, later, CreateTick(profileId, 3, CreateIsland(1, IslandName, CreateEntry(1))));

        // Act
        var latest = await store.ReadLatestTickAsync(profileId, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(later.Id, latest?.Id, "The tick with the latest receive time should be the latest, though stored earlier");
    }

    /// <summary>
    /// A stored catalog reads back byte for byte with its identity.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStoreStoreCatalogReadsBackBytesAndIdentity()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var upload = CreateCatalogUpload("first");
        var stored = await store.StoreCatalogAsync(upload, TestContext.CancellationToken);

        // Act
        var catalog = await store.ReadCatalogAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The catalog should be stored");
        Assert.IsNotNull(catalog, "The stored catalog should read back");
        Assert.AreEqual(upload.ComputeIdentity(), catalog.Identity, "The catalog identity should be the SHA-256 of the document");
        Assert.AreSequenceEqual(upload.Document.ToArray(), catalog.Document.ToArray(), "The document should read back byte for byte");
    }

    /// <summary>
    /// A later catalog replaces the stored one.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStoreStoreCatalogReplacesEarlierOne()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var later = CreateCatalogUpload("later");

        await store.StoreCatalogAsync(CreateCatalogUpload("earlier"), TestContext.CancellationToken);

        // Act
        var result = await store.StoreCatalogAsync(later, TestContext.CancellationToken);

        // Assert
        var catalog = await store.ReadCatalogAsync(TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The later catalog should be stored");
        Assert.IsNotNull(catalog, "The later catalog should read back");
        Assert.AreEqual(later.ComputeIdentity(), catalog.Identity, "The later catalog's identity should be read back");
        Assert.AreSequenceEqual(later.Document.ToArray(), catalog.Document.ToArray(), "The later document should replace the earlier one");
    }

    /// <summary>
    /// A stored icon reads back by its reference, and storing its reference again is reported as already stored and
    /// changes nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStoreStoreIconReadsBackAndSecondStoreChangesNothing()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var reference = IconReference(1);
        var first = CreateIcon(reference, "first");
        var firstResult = await store.StoreIconAsync(first, TestContext.CancellationToken);

        // Act
        var secondResult = await store.StoreIconAsync(CreateIcon(reference, "second"), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, firstResult.Outcome, "The icon should be stored");
        Assert.AreEqual(ServerStoreOutcome.AlreadyStored, secondResult.Outcome, "A stored reference should be reported as already stored");
        Assert.AreSequenceEqual(first.Png.ToArray(), await store.ReadIconAsync(reference, TestContext.CancellationToken) ?? [], "The icon should read back with the bytes stored first");
    }

    /// <summary>
    /// The missing-references query returns exactly the references not stored, distinct and ordinally sorted, also for
    /// as many references as a catalog upload names at most.
    /// </summary>
    /// <param name="referenceCount">The number of distinct references asked for</param>
    /// <param name="storedEvery">Every how many references one is stored</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(5, 2)]
    [DataRow(BoundIconReferences, 1000)]
    public async Task ServerStoreFindMissingIconsReturnsExactlyUnstored(int referenceCount, int storedEvery)
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var references = Enumerable.Range(0, referenceCount).Select(IconReference).ToList();
        var stored = references.Where((_, index) => index % storedEvery == 0).ToList();
        var asked = Enumerable.Reverse(references).Concat(references.Take(2)).ToList();

        await StoreIconsAsync(store, stored);

        // Act
        var missing = await store.FindMissingIconsAsync(asked, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(references.Except(stored).Order(StringComparer.Ordinal).ToList(), missing.ToList(), "Exactly the references not stored should be returned, distinct and ordinally sorted");
    }

    /// <summary>
    /// A write that fails in the middle of a tick stores nothing of it, not its profile either, and returns a failure.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task ServerStoreStoreTickFailingWriteStoresNothingAndReportsFailed()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var profileId = Guid.NewGuid();
        var tick = CreateEveryValueTick(profileId);

        Execute(databaseFile, command => command.CommandText = CreateFailingTriggerSql);

        // Act
        var result = await store.StoreTickAsync(tick, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Failed, result.Outcome, "A failing write should be reported as failed");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.IsNull(await store.ReadTickAsync(tick.Id, TestContext.CancellationToken), "Nothing of the tick should be stored");
        Assert.IsEmpty(await store.ListProfilesAsync(TestContext.CancellationToken), "The tick's profile should not be created");
        Assert.IsEmpty(await store.ListIslandsAsync(profileId, TestContext.CancellationToken), "No island of the tick should be listed");
        Assert.AreEqual("0 0 0 0", CountTickRows(databaseFile), "No row of the tick, its islands, entries or map pairs should remain");
    }

    /// <summary>
    /// A write whose transaction SQLite ends itself in the middle of a tick stores nothing of it and returns a failure.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task ServerStoreStoreTickRolledBackBySqliteStoresNothingAndReportsFailed()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var store = await OpenStoreAsync(databaseFile);
        var profileId = Guid.NewGuid();
        var tick = CreateEveryValueTick(profileId);

        Execute(databaseFile, command => command.CommandText = CreateRollingBackTriggerSql);

        // Act
        var result = await store.StoreTickAsync(tick, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Failed, result.Outcome, "A write SQLite rolled back should be reported as failed");
        Assert.IsInstanceOfType<SqliteException>(result.Failure, "The SQLite error behind the failure should be attached");
        Assert.IsNull(await store.ReadTickAsync(tick.Id, TestContext.CancellationToken), "Nothing of the tick should be stored");
        Assert.IsEmpty(await store.ListProfilesAsync(TestContext.CancellationToken), "The tick's profile should not be created");
        Assert.AreEqual("0 0 0 0", CountTickRows(databaseFile), "No row of the tick, its islands, entries or map pairs should remain");
        Assert.AreEqual(ServerStoreOutcome.Stored, (await store.StoreProfileAsync(profileId, ProfileName, TestContext.CancellationToken)).Outcome, "The next write should begin its own transaction and be stored");
    }

    /// <summary>
    /// Without a stored catalog, reading the catalog gives none.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStoreReadCatalogWithoutStoredCatalogReturnsNull()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());

        // Act
        var catalog = await store.ReadCatalogAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsNull(catalog, "No catalog should be read when none is stored");
    }

    /// <summary>
    /// Reads during a tick write, held after the tick, its islands, entries and map pairs are written but before it
    /// commits, see the state before the write: no part of the tick; after the write they see the whole tick. The write
    /// is held until the reads are done, so a read that waited for it instead would see it only after the hold's bound.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ServerStoreReadDuringTickWriteSeesStateBeforeOrAfterIt()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var profileId = Guid.NewGuid();
        var tick = CreateEveryValueTick(profileId);
        var writeHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var readsDone = new ManualResetEventSlim(false);

        HoldTickWrites(_databases[^1], writeHeld, readsDone);

        var write = Task.Run(() => store.StoreTickAsync(tick, TestContext.CancellationToken), TestContext.CancellationToken);

        await writeHeld.Task.WaitAsync(_pauseBound, TestContext.CancellationToken);

        // Act
        List<string> during;

        try
        {
            during = await DescribeStoredStateAsync(store, tick.Id, profileId);
        }
        finally
        {
            readsDone.Set();
        }

        var stored = await write.WaitAsync(_pauseBound, TestContext.CancellationToken);
        var after = await store.ReadTickAsync(tick.Id, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(_stateBeforeWrite, during, "A read during the write should see the state before it, no part of the tick");
        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The held write should be stored once released");
        Assert.AreSequenceEqual(DescribeTick(tick), DescribeTick(after), "A read after the write should see the whole tick");
    }

    /// <summary>
    /// A tick with an entry whose two maps are empty and an island without entries reads back with exactly those: empty
    /// maps and no entries, between islands and entries that have them.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStoreStoreTickWithEmptyMapsAndIslandWithoutEntriesReadsBack()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var emptyMapsEntry = new ContractTickEntry
                             {
                                 ProductGuid = 77,
                                 Generation = 1f,
                                 Consumption = null,
                                 Delta = 1f,
                                 PerfectGeneration = null,
                                 PerfectConsumption = null,
                                 BuildingCount = 1,
                                 Maintenance = 2,
                                 Income = null,
                                 Profit = 3,
                                 SummedProductivity = null,
                                 AverageProductivity = null,
                                 WorkforceAmounts = new Dictionary<int, int>(),
                                 BuildingAmounts = new Dictionary<int, int>()
                             };
        var tick = CreateTick(Guid.NewGuid(),
                              1,
                              CreateIsland(1, "With entries", CreateEntry(5), emptyMapsEntry),
                              CreateIsland(2, "Without entries"),
                              CreateIsland(3, "Last island", CreateEntry(6)));
        var stored = await store.StoreTickAsync(tick, TestContext.CancellationToken);

        // Act
        var readBack = await store.ReadTickAsync(tick.Id, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The tick should be stored");
        Assert.AreSequenceEqual(DescribeTick(tick), DescribeTick(readBack), "Empty maps and an island without entries should read back as they were sent");
    }

    /// <summary>
    /// A tick at the contract's bounds (256 islands, 524,288 entries and map pairs, island names of 765 bytes) is
    /// stored and read back with every value.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task ServerStoreStoreTickAtContractBoundsReadsBack()
    {
        // Arrange
        var store = await OpenStoreAsync(CreateDatabaseFile());
        var tick = CreateBoundTick(Guid.NewGuid());
        var stored = await store.StoreTickAsync(tick, TestContext.CancellationToken);

        // Act
        var readBack = await store.ReadTickAsync(tick.Id, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The tick at the bounds should be stored");
        Assert.AreSequenceEqual(DescribeTick(tick), DescribeTick(readBack), "The tick at the bounds should read back with every value");
    }

    /// <summary>
    /// Gives the receive time some seconds after the base receive time.
    /// </summary>
    /// <param name="seconds">The seconds</param>
    /// <returns>The receive time, in UTC</returns>
    private static DateTimeOffset ReceiveTime(long seconds)
    {
        return _baseReceiveTime.AddSeconds(seconds);
    }

    /// <summary>
    /// Creates a tick of a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds after the base receive time</param>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static ContractTick CreateTick(Guid profileId, long receiveSeconds, params ContractTickIsland[] islands)
    {
        return CreateTick(Guid.NewGuid(), profileId, receiveSeconds, islands);
    }

    /// <summary>
    /// Creates a tick.
    /// </summary>
    /// <param name="tickId">The tick identity</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds after the base receive time</param>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static ContractTick CreateTick(Guid tickId, Guid profileId, long receiveSeconds, params ContractTickIsland[] islands)
    {
        return new ContractTick
               {
                   Id = tickId,
                   ProfileId = profileId,
                   ReceiveTime = ReceiveTime(receiveSeconds),
                   GameTime = 3_600_000 + receiveSeconds,
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
    private static ContractTickIsland CreateIsland(int key, string name, params ContractTickEntry[] entries)
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
    /// Creates an entry with plain values and one pair in each map.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry CreateEntry(int productGuid)
    {
        return new ContractTickEntry
               {
                   ProductGuid = productGuid,
                   Generation = 1.5f,
                   Consumption = 2.5f,
                   Delta = -1f,
                   PerfectGeneration = 1.75f,
                   PerfectConsumption = 2.25f,
                   BuildingCount = 3,
                   Maintenance = 40,
                   Income = 12.5f,
                   Profit = -27,
                   SummedProductivity = 2.5f,
                   AverageProductivity = 0.8333333f,
                   WorkforceAmounts = new Dictionary<int, int>
                                      {
                                          [100] = 7
                                      },
                   BuildingAmounts = new Dictionary<int, int>
                                     {
                                         [200] = 3
                                     }
               };
    }

    /// <summary>
    /// Creates a tick that carries every kind of value: floats with no value, negative and positive zero, the smallest
    /// subnormal and the largest magnitudes, extreme integers, empty and filled maps, names beyond ASCII and entries in
    /// an order that is not that of their product GUIDs.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The tick</returns>
    private static ContractTick CreateEveryValueTick(Guid profileId)
    {
        var edgeEntry = new ContractTickEntry
                        {
                            ProductGuid = int.MaxValue,
                            Generation = null,
                            Consumption = -0f,
                            Delta = 0f,
                            PerfectGeneration = float.Epsilon,
                            PerfectConsumption = -float.MaxValue,
                            BuildingCount = int.MinValue,
                            Maintenance = int.MaxValue,
                            Income = float.MaxValue,
                            Profit = int.MinValue,
                            SummedProductivity = null,
                            AverageProductivity = -float.Epsilon,
                            WorkforceAmounts = new Dictionary<int, int>(),
                            BuildingAmounts = new Dictionary<int, int>
                                              {
                                                  [int.MinValue] = int.MinValue,
                                                  [-1] = 0,
                                                  [int.MaxValue] = int.MaxValue
                                              }
                        };
        var markedEntry = new ContractTickEntry
                          {
                              ProductGuid = MarkedProductGuid,
                              Generation = MarkedFloat,
                              Consumption = 0.1f,
                              Delta = null,
                              PerfectGeneration = null,
                              PerfectConsumption = null,
                              BuildingCount = MarkedBuildingCount,
                              Maintenance = 0,
                              Income = null,
                              Profit = 0,
                              SummedProductivity = -0f,
                              AverageProductivity = null,
                              WorkforceAmounts = new Dictionary<int, int>
                                                 {
                                                     [1] = 10,
                                                     [2] = 20,
                                                     [3] = 30
                                                 },
                              BuildingAmounts = new Dictionary<int, int>()
                          };

        return CreateTick(profileId,
                          7,
                          new ContractTickIsland
                          {
                              SessionGuid = -5,
                              IslandId = byte.MaxValue,
                              AreaIndex = byte.MaxValue,
                              SessionId = byte.MaxValue,
                              Name = IslandName + " é€中",
                              Entries = [edgeEntry, CreateEntry(-3), markedEntry]
                          },
                          CreateIsland(1, "Second island", CreateEntry(5)));
    }

    /// <summary>
    /// Creates a tick at the contract's bounds: 256 islands with names of 765 UTF-8 bytes, each with 512 entries of
    /// three map pairs, 524,288 entries and pairs in all.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The tick</returns>
    private static ContractTick CreateBoundTick(Guid profileId)
    {
        var islands = Enumerable.Range(0, BoundIslands)
                                .Select(island => new ContractTickIsland
                                                  {
                                                      SessionGuid = island,
                                                      IslandId = (byte)island,
                                                      AreaIndex = (byte)(island % 3),
                                                      SessionId = (byte)(island % 7),
                                                      Name = new string('€', 255),
                                                      Entries = Enumerable.Range(0, BoundEntriesPerIsland).Select(entry => CreateBoundEntry(island, entry)).ToList()
                                                  })
                                .ToList();

        return CreateTick(profileId, 1, [.. islands]);
    }

    /// <summary>
    /// Creates an entry of the tick at the contract's bounds, with values derived from its position.
    /// </summary>
    /// <param name="island">The island position</param>
    /// <param name="entry">The entry position</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry CreateBoundEntry(int island, int entry)
    {
        var value = (island * BoundEntriesPerIsland) + entry;

        return new ContractTickEntry
               {
                   ProductGuid = value,
                   Generation = value * 0.5f,
                   Consumption = entry % 2 == 0 ? null : -value,
                   Delta = value / 3f,
                   PerfectGeneration = value,
                   PerfectConsumption = -0f,
                   BuildingCount = entry,
                   Maintenance = island,
                   Income = value * 1.25f,
                   Profit = -value,
                   SummedProductivity = 1f / (value + 1),
                   AverageProductivity = null,
                   WorkforceAmounts = new Dictionary<int, int>
                                      {
                                          [entry] = value,
                                          [entry + 1000] = island
                                      },
                   BuildingAmounts = new Dictionary<int, int>
                                     {
                                         [value] = entry
                                     }
               };
    }

    /// <summary>
    /// Creates a catalog upload whose document holds a text.
    /// </summary>
    /// <param name="text">The text that tells the documents apart</param>
    /// <returns>The upload</returns>
    private static ContractCatalogUpload CreateCatalogUpload(string text)
    {
        return new ContractCatalogUpload
               {
                   Id = Guid.NewGuid(),
                   Document = Encoding.UTF8.GetBytes($"{{\"marker\":\"{CatalogMarker}\",\"text\":\"{text}\"}}")
               };
    }

    /// <summary>
    /// Creates an icon image whose bytes hold a text.
    /// </summary>
    /// <param name="reference">The reference key</param>
    /// <param name="text">The text that tells the images apart</param>
    /// <returns>The icon</returns>
    private static ContractIconImage CreateIcon(string reference, string text)
    {
        return new ContractIconImage
               {
                   Id = Guid.NewGuid(),
                   Reference = reference,
                   Png = Encoding.UTF8.GetBytes($"{PngMarker} {text}")
               };
    }

    /// <summary>
    /// Gives an icon reference key: 64 lowercase hex characters derived from a number.
    /// </summary>
    /// <param name="number">The number</param>
    /// <returns>The reference</returns>
    private static string IconReference(int number)
    {
        return Convert.ToHexStringLower(SHA256.HashData(BitConverter.GetBytes(number)));
    }

    /// <summary>
    /// Describes a tick as text, one line for the tick, one per island and one per entry, floats as their bit patterns
    /// and maps in ascending order.
    /// </summary>
    /// <param name="tick">The tick, or <see langword="null" /></param>
    /// <returns>The lines</returns>
    private static List<string> DescribeTick(ContractTick? tick)
    {
        if (tick is null)
        {
            return ["<no tick>"];
        }

        var head = $"tick {tick.Id} profile {tick.ProfileId} receive {tick.ReceiveTime.UtcTicks} game {tick.GameTime}";

        return tick.Islands.SelectMany(DescribeIsland).Prepend(head).ToList();
    }

    /// <summary>
    /// Describes an island and its entries as text.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The lines</returns>
    private static IEnumerable<string> DescribeIsland(ContractTickIsland island)
    {
        var head = $"island {island.SessionGuid}/{island.IslandId}/{island.AreaIndex} session {island.SessionId} name {island.Name}";

        return island.Entries.Select(DescribeEntry).Prepend(head);
    }

    /// <summary>
    /// Describes an entry as text.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The line</returns>
    private static string DescribeEntry(ContractTickEntry entry)
    {
        var floats = string.Join(' ',
                                 DescribeFloat(entry.Generation),
                                 DescribeFloat(entry.Consumption),
                                 DescribeFloat(entry.Delta),
                                 DescribeFloat(entry.PerfectGeneration),
                                 DescribeFloat(entry.PerfectConsumption),
                                 DescribeFloat(entry.Income),
                                 DescribeFloat(entry.SummedProductivity),
                                 DescribeFloat(entry.AverageProductivity));

        return $"entry {entry.ProductGuid} floats {floats} ints {entry.BuildingCount} {entry.Maintenance} {entry.Profit} workforce {DescribeMap(entry.WorkforceAmounts)} buildings {DescribeMap(entry.BuildingAmounts)}";
    }

    /// <summary>
    /// Describes a float by its bit pattern.
    /// </summary>
    /// <param name="value">The float, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string DescribeFloat(float? value)
    {
        return value is null
                   ? "null"
                   : BitConverter.SingleToInt32Bits(value.Value).ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Describes a map in ascending key order.
    /// </summary>
    /// <param name="map">The map</param>
    /// <returns>The text</returns>
    private static string DescribeMap(IReadOnlyDictionary<int, int> map)
    {
        return string.Join(',', map.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>
    /// Describes a profile as text.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The name, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string DescribeProfile(Guid profileId, string? name)
    {
        return $"{profileId} {name ?? "<no name>"}";
    }

    /// <summary>
    /// Describes listed profiles as text, sorted ordinally.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeProfiles(IReadOnlyList<StoredProfile> profiles)
    {
        return profiles.Select(profile => DescribeProfile(profile.Id, profile.Name)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Describes listed islands as text (key, then name), sorted ordinally.
    /// </summary>
    /// <param name="islands">The islands</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeIslands(IReadOnlyList<StoredIsland> islands)
    {
        return islands.Select(island => $"{island.SessionGuid}/{island.IslandId}/{island.AreaIndex} {island.Name}").Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Gives the texts no log line may hold: the names and the marked values of the tests, as text and as bit pattern.
    /// </summary>
    /// <returns>The texts</returns>
    private static List<string> ForbiddenLogTexts()
    {
        var markedBits = BitConverter.SingleToInt32Bits(MarkedFloat);
        var png = CreateIcon(IconReference(1), "first").Png.ToArray();

        return
        [
            ProfileName,
            IslandName,
            CatalogMarker,
            PngMarker,
            IconReference(1),
            MarkedProductGuid.ToString(CultureInfo.InvariantCulture),
            MarkedBuildingCount.ToString(CultureInfo.InvariantCulture),
            MarkedFloat.ToString(CultureInfo.InvariantCulture),
            markedBits.ToString(CultureInfo.InvariantCulture),
            Convert.ToHexString(png),
            Convert.ToBase64String(png)
        ];
    }

    /// <summary>
    /// Finds the lines that hold one of the given texts.
    /// </summary>
    /// <param name="lines">The lines</param>
    /// <param name="texts">The texts</param>
    /// <returns>The lines that hold a text</returns>
    private static List<string> FindLinesHolding(List<string> lines, List<string> texts)
    {
        return lines.Where(line => texts.Any(text => line.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
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
    /// Runs one command over a connection of its own.
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
    /// Counts the rows of the tick tables over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The counts of ticks, tick islands, entries and map pairs, separated by spaces</returns>
    private static string CountTickRows(string databaseFile)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT (SELECT count(*) FROM \"Ticks\") || ' ' || (SELECT count(*) FROM \"TickIslands\") || ' ' || (SELECT count(*) FROM \"TickEntries\") || ' ' || (SELECT count(*) FROM \"TickMapPairs\");";

        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Holds the first tick write over a database's own connection at its first island row until the reads are done.
    /// </summary>
    /// <param name="database">The database whose connection the store writes over</param>
    /// <param name="writeHeld">Completed when the write is held</param>
    /// <param name="readsDone">Set by the test when its reads are done</param>
    private static void HoldTickWrites(ServerDatabase database, TaskCompletionSource writeHeld, ManualResetEventSlim readsDone)
    {
        database.Connection.CreateFunction(PauseFunctionName,
                                           () =>
                                           {
                                               if (writeHeld.TrySetResult())
                                               {
                                                   readsDone.Wait(_holdBound);
                                               }

                                               return 0;
                                           });

        using var command = database.Connection.CreateCommand();

        command.CommandText = CreatePauseTriggerSql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates the path of a database file in a new temporary directory.
    /// </summary>
    /// <returns>The full path of the database file, not yet created</returns>
    private string CreateDatabaseFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-server-store-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        return Path.Combine(directory, DatabaseFileName);
    }

    /// <summary>
    /// Disposes the databases opened so far and closes their pooled connections.
    /// </summary>
    private void CloseDatabases()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _databases.Clear();
        SqliteConnection.ClearAllPools();
    }

    /// <summary>
    /// Opens a store on a database file.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <returns>The store</returns>
    private Task<ServerStore> OpenStoreAsync(string databaseFile)
    {
        return OpenStoreCoreAsync(databaseFile);
    }

    /// <summary>
    /// Opens a store on an in-memory database.
    /// </summary>
    /// <returns>The store</returns>
    private Task<ServerStore> OpenInMemoryStoreAsync()
    {
        return OpenStoreCoreAsync(null);
    }

    /// <summary>
    /// Opens a database and a store on it, and keeps the database for the cleanup.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null" /> for an in-memory database</param>
    /// <returns>The store</returns>
    private async Task<ServerStore> OpenStoreCoreAsync(string? databaseFile)
    {
        var result = await ServerDatabase.OpenAsync(databaseFile, _loggerFactory, TestContext.CancellationToken);
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

    /// <summary>
    /// Stores an icon under each reference.
    /// </summary>
    /// <param name="store">The store</param>
    /// <param name="references">The references</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task StoreIconsAsync(ServerStore store, List<string> references)
    {
        foreach (var reference in references)
        {
            await store.StoreIconAsync(CreateIcon(reference, reference), TestContext.CancellationToken);
        }
    }

    /// <summary>
    /// Describes what the store's reads see of a tick and its profile.
    /// </summary>
    /// <param name="store">The store</param>
    /// <param name="tickId">The tick identity</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The tick read by identity, the latest tick of the profile, and the numbers of its islands and of profiles</returns>
    private async Task<List<string>> DescribeStoredStateAsync(ServerStore store, Guid tickId, Guid profileId)
    {
        var cancellationToken = TestContext.CancellationToken;
        var tick = await store.ReadTickAsync(tickId, cancellationToken);
        var latest = await store.ReadLatestTickAsync(profileId, cancellationToken);
        var islands = await store.ListIslandsAsync(profileId, cancellationToken);
        var profiles = await store.ListProfilesAsync(cancellationToken);

        return
        [
            "tick " + (tick is null ? "<none>" : string.Join(" | ", DescribeTick(tick))),
            "latest " + (latest is null ? "<none>" : string.Join(" | ", DescribeTick(latest))),
            "islands " + islands.Count.ToString(CultureInfo.InvariantCulture),
            "profiles " + profiles.Count.ToString(CultureInfo.InvariantCulture)
        ];
    }

    /// <summary>
    /// Runs every operation of the store once: a profile, a tick, the catalog and an icon stored, every read, and a
    /// tick whose write fails.
    /// </summary>
    /// <param name="store">The store</param>
    /// <param name="databaseFile">The database file, which gets the trigger that makes the last write fail</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task RunEveryOperationAsync(ServerStore store, string databaseFile)
    {
        var profileId = Guid.NewGuid();
        var tick = CreateEveryValueTick(profileId);
        var cancellationToken = TestContext.CancellationToken;

        await store.StoreProfileAsync(profileId, ProfileName, cancellationToken);
        await store.StoreTickAsync(tick, cancellationToken);
        await store.StoreCatalogAsync(CreateCatalogUpload("first"), cancellationToken);
        await store.StoreIconAsync(CreateIcon(IconReference(1), "first"), cancellationToken);
        await store.ListProfilesAsync(cancellationToken);
        await store.ReadTickAsync(tick.Id, cancellationToken);
        await store.ReadLatestTickAsync(profileId, cancellationToken);
        await store.ListIslandsAsync(profileId, cancellationToken);
        await store.ReadCatalogAsync(cancellationToken);
        await store.ReadIconAsync(IconReference(1), cancellationToken);
        await store.FindMissingIconsAsync([IconReference(1), IconReference(2)], cancellationToken);

        Execute(databaseFile, command => command.CommandText = CreateFailingTriggerSql);

        await store.StoreTickAsync(CreateEveryValueTick(profileId), cancellationToken);
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _loggerFactory.Dispose();
        _loggerProvider.Dispose();
    }

    #endregion // IDisposable
}