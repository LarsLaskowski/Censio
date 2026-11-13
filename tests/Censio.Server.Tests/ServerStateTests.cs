using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Censio.Contracts;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="ServerState" />, over a real <see cref="ServerStore" />: a file in a temporary directory, or the
/// in-memory database.
/// </summary>
[TestClass]
public sealed class ServerStateTests
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// A profile name.
    /// </summary>
    private const string ProfileName = "Profile name 7f3c";

    /// <summary>
    /// Another profile name.
    /// </summary>
    private const string OtherProfileName = "Renamed profile 2b81";

    /// <summary>
    /// An island name.
    /// </summary>
    private const string IslandName = "Island name 9b2d";

    /// <summary>
    /// The error line of a profile state that cannot be read (spec E13).
    /// </summary>
    private const string ReadFailureLine = "The current state of a profile cannot be read from the database.";

    /// <summary>
    /// The warning line of a follower that throws (spec E10).
    /// </summary>
    private const string FollowerFailureLine = "A follower of the current state failed.";

    /// <summary>
    /// The statement that renames the tick table, so that every read of a tick fails.
    /// </summary>
    private const string RenameTicksSql = "ALTER TABLE \"Ticks\" RENAME TO \"TicksMoved\";";

    /// <summary>
    /// The statement that undoes <see cref="RenameTicksSql" />.
    /// </summary>
    private const string RestoreTicksSql = "ALTER TABLE \"TicksMoved\" RENAME TO \"Ticks\";";

    /// <summary>
    /// The statement that renames the icon table, so that every read of the missing icons fails.
    /// </summary>
    private const string RenameIconsSql = "ALTER TABLE \"Icons\" RENAME TO \"IconsMoved\";";

    /// <summary>
    /// The temporary view, on the pooled read connection only, that shadows the island table and fails every read of
    /// it at run time, while the store's own write connection still writes islands.
    /// </summary>
    private const string BreakIslandReadsSql = "CREATE TEMP VIEW \"Islands\" AS SELECT * FROM main.\"Islands\" WHERE json('not json') IS NOT NULL;";

    /// <summary>
    /// The statement that removes the view of <see cref="BreakIslandReadsSql" />.
    /// </summary>
    private const string RepairIslandReadsSql = "DROP VIEW temp.\"Islands\";";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The receive time the tests count their seconds from.
    /// </summary>
    private static readonly DateTimeOffset _baseReceiveTime = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The longest the test waits for a follower, a log entry or a store call.
    /// </summary>
    private static readonly TimeSpan _waitBound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The islands <see cref="ServerStateStoreTickAsyncTickWithoutEntriesOrSomeIslandsBecomesState" /> expects.
    /// </summary>
    private static readonly string[] _expectedPartialIslands = ["1/1/0 Loaded island", "2/2/0 Lacked island"];

    /// <summary>
    /// The islands <see cref="ServerStateStoreTickAsyncOlderTickIslandNamesEqualStore" /> expects the store to list.
    /// </summary>
    private static readonly string[] _expectedOlderTickIslands = ["1/1/0 Name of the latest tick", "2/2/0 Island only of an older tick"];

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    /// <summary>
    /// The logger of the state.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStateTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ServerStateTests(TestContext testContext)
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
        CloseDatabases();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A state restored from the reopened file equals the state before: the profiles in the same order with their names,
    /// each profile's latest tick bit for bit, its islands with an island only an older tick named, and the complete
    /// catalog; a tick of one profile did not change the other.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    public async Task ServerStateRestoreAsyncAfterReopenEqualsStateBeforeStop()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var (_, before) = await OpenRestoredAsync(databaseFile);

        var named = Guid.NewGuid();
        var created = Guid.NewGuid();
        var (upload, document) = CreateCatalog(1, IconReference(1));

        await before.StoreProfileAsync(named, ProfileName, TestContext.CancellationToken);
        await StoreTicksAsync(before,
                              CreateEveryValueTick(named),
                              CreateTick(named, 3, CreateIsland(7, "Island only of an older tick", CreateEntry(1))),
                              CreateEveryValueTick(created));
        await before.StoreCatalogAsync(upload, document, TestContext.CancellationToken);
        await before.StoreIconAsync(CreateIcon(IconReference(1)), TestContext.CancellationToken);

        var expected = await DescribeWholeStateAsync(before);

        CloseDatabases();

        var (_, after) = await OpenAsync(databaseFile);

        // Act
        var count = await after.RestoreAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(2, count, "Both stored profiles should be restored");
        Assert.AreEqual(upload.ComputeIdentity(), before.CompleteCatalogIdentity, "The catalog should be complete before the stop, or the comparison below would prove less");
        Assert.AreSequenceEqual(expected, await DescribeWholeStateAsync(after), "The restored state should equal the state before the stop");
    }

    /// <summary>
    /// A tick of one profile changes neither the current state nor the island list of another.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task ServerStateStoreTickAsyncOfOtherProfileLeavesStateUnchanged()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        await StoreTicksAsync(state,
                              CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))),
                              CreateTick(otherId, 1, CreateIsland(2, "Island of the other profile", CreateEntry(1))));

        var expected = DescribeState(await ReadStateAsync(state, profileId));

        // Act
        await state.StoreTickAsync(CreateTick(otherId, 2, CreateIsland(1, "Renamed by the other profile", CreateEntry(2)), CreateIsland(3, "New island of the other profile", CreateEntry(3))), TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(expected, DescribeState(await ReadStateAsync(state, profileId)), "A tick of another profile should change neither the state nor the islands of this one");
    }

    /// <summary>
    /// A tick that arrives after a newer one is stored, and the state stays the newer tick.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ServerStateStoreTickAsyncOlderTickKeepsNewerState()
    {
        // Arrange
        var (store, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();
        var newer = CreateTick(profileId, 5, CreateIsland(1, IslandName, CreateEntry(1)));
        var older = CreateTick(profileId, 3, CreateIsland(1, IslandName, CreateEntry(2)));

        await state.StoreTickAsync(newer, TestContext.CancellationToken);

        // Act
        var result = await state.StoreTickAsync(older, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The older tick should be stored");
        Assert.IsNotNull(await store.ReadTickAsync(older.Id, TestContext.CancellationToken), "The older tick should be in the store");
        Assert.AreSequenceEqual(DescribeTick(newer), DescribeTick((await ReadStateAsync(state, profileId))?.LatestTick), "The state should stay the newer tick");
    }

    /// <summary>
    /// Of two ticks with equal receive times the one stored later is the state.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ServerStateStoreTickAsyncEqualReceiveTimeStoredLaterWins()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();
        var storedLater = CreateTick(profileId, 3, CreateIsland(1, IslandName, CreateEntry(2)));

        await state.StoreTickAsync(CreateTick(profileId, 3, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        // Act
        await state.StoreTickAsync(storedLater, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(DescribeTick(storedLater), DescribeTick((await ReadStateAsync(state, profileId))?.LatestTick), "Of equal receive times the tick stored later should be the state");
    }

    /// <summary>
    /// A later tick whose one island has no entries and that lacks another island becomes the state; the island it lacks
    /// keeps its listing and name.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ServerStateStoreTickAsyncTickWithoutEntriesOrSomeIslandsBecomesState()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();
        var partial = CreateTick(profileId, 2, CreateIsland(1, "Loaded island"));

        await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1)), CreateIsland(2, "Lacked island", CreateEntry(2))), TestContext.CancellationToken);

        // Act
        await state.StoreTickAsync(partial, TestContext.CancellationToken);

        // Assert
        var read = await ReadStateAsync(state, profileId);

        Assert.AreSequenceEqual(DescribeTick(partial), DescribeTick(read?.LatestTick), "The tick without entries and with only some islands should be the state");
        Assert.AreSequenceEqual(_expectedPartialIslands, DescribeIslands(read?.Islands ?? []), "The lacked island should keep its listing and name");
    }

    /// <summary>
    /// After an older tick that names an island the latest tick lacks and renames one it carries, the held islands equal
    /// the stored ones: each with the name of its latest receive time.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ServerStateStoreTickAsyncOlderTickIslandNamesEqualStore()
    {
        // Arrange
        var (store, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();

        await state.StoreTickAsync(CreateTick(profileId, 5, CreateIsland(1, "Name of the latest tick", CreateEntry(1))), TestContext.CancellationToken);

        // Act
        await state.StoreTickAsync(CreateTick(profileId, 3, CreateIsland(1, "Name of an older tick", CreateEntry(1)), CreateIsland(2, "Island only of an older tick", CreateEntry(2))), TestContext.CancellationToken);

        // Assert
        var stored = await store.ReadProfileStateAsync(profileId, TestContext.CancellationToken);

        Assert.AreSequenceEqual(_expectedOlderTickIslands, DescribeIslands(stored?.Islands ?? []), "The store should list both islands, or the comparison below would prove less");
        Assert.AreSequenceEqual(DescribeIslands(stored?.Islands ?? []), DescribeIslands((await ReadStateAsync(state, profileId))?.Islands ?? []), "The held islands should equal the stored ones");
    }

    /// <summary>
    /// A tick of an identity no hello named creates the profile without a name, listed in identity order with the others.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerStateStoreTickAsyncOfUnknownProfileCreatesNamelessProfile()
    {
        // Arrange
        var (store, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();

        await state.StoreProfileAsync(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), ProfileName, TestContext.CancellationToken);
        await state.StoreProfileAsync(Guid.Empty, OtherProfileName, TestContext.CancellationToken);

        // Act
        await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        // Assert
        Assert.Contains(DescribeProfile(profileId, null), DescribeProfiles(state.Profiles), "The profile of the tick should be listed without a name");
        Assert.AreSequenceEqual(DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), DescribeProfiles(state.Profiles), "The profiles should be listed as the store lists them, in identity order");
    }

    /// <summary>
    /// A hello's name for a held profile created by a tick names it in the list and in its state, keeps the identity
    /// order, and tells the followers that the profile changed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [TestCategory("AC8")]
    public async Task ServerStateStoreProfileAsyncNamesHeldProfileAndTellsFollowers()
    {
        // Arrange
        var (store, state) = await OpenRestoredAsync(CreateDatabaseFile());

        var profileId = Guid.NewGuid();

        await state.StoreProfileAsync(Guid.Empty, OtherProfileName, TestContext.CancellationToken);
        await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        var (notices, told) = Follow(state, 1);

        // Act
        var result = await state.StoreProfileAsync(profileId, ProfileName, TestContext.CancellationToken);

        // Assert
        await told.WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The name should be stored");
        Assert.AreSequenceEqual(DescribeProfiles(await store.ListProfilesAsync(TestContext.CancellationToken)), DescribeProfiles(state.Profiles), "The named profile should be listed as the store lists it");
        Assert.AreEqual(DescribeProfile(profileId, ProfileName), DescribeState(await ReadStateAsync(state, profileId))[0], "The held state should carry the name");
        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Profile, profileId) }, notices.ToList(), "The followers should be told that the profile changed");
    }

    /// <summary>
    /// A resent catalog writes nothing, tells no follower, and lists the references still without a stored icon.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task ServerStateStoreCatalogAsyncResentCatalogListsStillMissingIcons()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(CreateDatabaseFile());
        var (upload, document) = CreateCatalog(1, IconReference(2), IconReference(1));

        var resend = new ContractCatalogUpload
                     {
                         Id = Guid.NewGuid(),
                         Document = upload.Document
                     };
        var first = await state.StoreCatalogAsync(upload, document, TestContext.CancellationToken);

        await state.StoreIconAsync(CreateIcon(IconReference(1)), TestContext.CancellationToken);

        var (notices, told) = Follow(state, 1);

        var otherProfile = Guid.NewGuid();

        // Act
        var result = await state.StoreCatalogAsync(resend, document, TestContext.CancellationToken);

        // Assert
        await state.StoreTickAsync(CreateTick(otherProfile, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);
        await told.WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { IconReference(1), IconReference(2) }.Order(StringComparer.Ordinal).ToList(), first.MissingIcons.ToList(), "The first upload should list both references, ordinally sorted");
        Assert.AreEqual(ServerStoreOutcome.AlreadyStored, result.Result.Outcome, "The resent catalog should write nothing");
        Assert.AreSequenceEqual(new[] { IconReference(2) }, result.MissingIcons.ToList(), "The resent catalog should list the reference still without a stored icon");
        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Profile, otherProfile) }, notices.ToList(), "No follower should be told of the resend, only of the later tick");
    }

    /// <summary>
    /// A catalog uploaded after another replaces it, whatever its age.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task ServerStateStoreCatalogAsyncOlderCatalogReplacesNewer()
    {
        // Arrange
        var (store, state) = await OpenRestoredAsync(CreateDatabaseFile());
        var (newerUpload, newerDocument) = CreateCatalog(1);
        var (olderUpload, olderDocument) = CreateCatalog(100);

        await state.StoreCatalogAsync(newerUpload, newerDocument, TestContext.CancellationToken);

        // Act
        var result = await state.StoreCatalogAsync(olderUpload, olderDocument, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Result.Outcome, "The older catalog should be stored");
        Assert.AreEqual(olderUpload.ComputeIdentity(), state.CompleteCatalogIdentity, "The catalog uploaded last should be the complete catalog");
        Assert.AreEqual(olderUpload.ComputeIdentity(), (await store.ReadCatalogAsync(TestContext.CancellationToken))?.Identity, "The catalog uploaded last should be stored");
    }

    /// <summary>
    /// A catalog stored while its missing icons cannot be read leaves no usable catalog, returns the failure and tells
    /// the followers that the catalog changed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task ServerStateStoreCatalogAsyncMissingIconReadFailureMarksCatalogUnusable()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var (_, state) = await OpenRestoredAsync(databaseFile);
        var (firstUpload, firstDocument) = CreateCatalog(1);
        var (upload, document) = CreateCatalog(100);

        await state.StoreCatalogAsync(firstUpload, firstDocument, TestContext.CancellationToken);
        Execute(databaseFile, RenameIconsSql);

        var (notices, told) = Follow(state, 1);

        // Act
        var result = await state.StoreCatalogAsync(upload, document, TestContext.CancellationToken);

        // Assert
        await told.WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Failed, result.Result.Outcome, "A catalog whose missing icons cannot be read should fail");
        Assert.IsInstanceOfType<SqliteException>(result.Result.Failure, "The read's exception should be attached");
        Assert.IsNull(state.CompleteCatalogIdentity, "Neither catalog should be named as complete");
        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Catalog, null) }, notices.ToList(), "The followers should be told that the catalog changed");
    }

    /// <summary>
    /// A stored catalog with an icon not stored is restored without a complete catalog.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task ServerStateRestoreAsyncWithIconsMissingNamesNoCatalog()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var (_, before) = await OpenRestoredAsync(databaseFile);
        var (upload, document) = CreateCatalog(1, IconReference(1), IconReference(2));

        await before.StoreCatalogAsync(upload, document, TestContext.CancellationToken);
        await before.StoreIconAsync(CreateIcon(IconReference(1)), TestContext.CancellationToken);
        CloseDatabases();

        var (_, after) = await OpenAsync(databaseFile);

        // Act
        await after.RestoreAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsNull(after.CompleteCatalogIdentity, "A catalog with an icon not stored should not be named");
    }

    /// <summary>
    /// A stored tick tells the followers that its profile changed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStateStoreTickAsyncTellsFollowersProfileChanged()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(null);

        var profileId = Guid.NewGuid();
        var (notices, told) = Follow(state, 1);

        // Act
        await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        // Assert
        await told.WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Profile, profileId) }, notices.ToList(), "The followers should be told that the tick's profile changed");
    }

    /// <summary>
    /// A stored icon tells the followers that the catalog changed, and completes the catalog it was missing from.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [TestCategory("AC8")]
    public async Task ServerStateStoreIconAsyncTellsFollowersCatalogChanged()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(null);
        var (upload, document) = CreateCatalog(1, IconReference(1));

        await state.StoreCatalogAsync(upload, document, TestContext.CancellationToken);

        var incomplete = state.CompleteCatalogIdentity;
        var (notices, told) = Follow(state, 1);

        // Act
        await state.StoreIconAsync(CreateIcon(IconReference(1)), TestContext.CancellationToken);

        // Assert
        await told.WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.IsNull(incomplete, "The catalog should not be named while its icon is missing");
        Assert.AreEqual(upload.ComputeIdentity(), state.CompleteCatalogIdentity, "The catalog should be named once its icon is stored");
        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Catalog, null) }, notices.ToList(), "The followers should be told that the catalog changed");
    }

    /// <summary>
    /// A follower that throws is logged as a warning with its exception, and the other followers are told.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStateChangedThrowingFollowerLogsWarningAndOthersAreTold()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(null);

        var profileId = Guid.NewGuid();
        var failure = new InvalidOperationException("follower failure");

        state.Changed += (_, _) => throw failure;

        var (notices, told) = Follow(state, 1);

        // Act
        var result = await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        // Assert
        await told.WaitAsync(_waitBound, TestContext.CancellationToken);

        var warning = await _logger.WaitForEntryAsync(LogLevel.Warning).WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The tick should be stored");
        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Profile, profileId) }, notices.ToList(), "The other follower should be told");
        Assert.AreEqual(FollowerFailureLine, warning.Message, "The warning should name the failed follower");
        Assert.AreSame(failure, warning.Exception, "The warning should carry the follower's exception");
    }

    /// <summary>
    /// A follower that blocks delays neither the store call nor another follower.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStateChangedBlockingFollowerDelaysNoOther()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(null);

        var profileId = Guid.NewGuid();

        using var release = new ManualResetEventSlim(false);

        state.Changed += (_, _) => release.Wait(_waitBound, TestContext.CancellationToken);

        var (notices, told) = Follow(state, 1);

        // Act
        ServerStoreResult result;

        try
        {
            result = await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

            await told.WaitAsync(_waitBound, TestContext.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        // Assert
        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The store call should end while a follower blocks");
        Assert.AreSequenceEqual(new[] { DescribeNotice(ServerStateChangeKind.Profile, profileId) }, notices.ToList(), "The other follower should be told while a follower blocks");
    }

    /// <summary>
    /// A state read before a later tick stays as it was read: the later tick replaces the state whole, never in place.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ServerStateReadProfileStateAsyncEarlierStateUnchangedByLaterTick()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(null);

        var profileId = Guid.NewGuid();
        var later = CreateTick(profileId, 2, CreateIsland(1, "Renamed island", CreateEntry(2)), CreateIsland(2, "New island", CreateEntry(3)));

        await state.StoreTickAsync(CreateTick(profileId, 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        var earlier = await ReadStateAsync(state, profileId);
        var expected = DescribeState(earlier);

        // Act
        await state.StoreTickAsync(later, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(expected, DescribeState(earlier), "The state read before should stay as it was read");
        Assert.AreSequenceEqual(DescribeTick(later), DescribeTick((await ReadStateAsync(state, profileId))?.LatestTick), "A read after the tick should see it");
    }

    /// <summary>
    /// Restore holds the profiles whose latest ticks are the newest, by receive time and then storing order; an older tick
    /// stored last does not move its profile.
    /// </summary>
    /// <param name="secondSeconds">The receive time of the second profile's latest tick, equal to the third's in a tie</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(20)]
    [DataRow(30)]
    public async Task ServerStateRestoreAsyncHoldsProfilesWithNewestTicks(int secondSeconds)
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var profiles = await StoreThreeProfilesAsync(databaseFile, secondSeconds);

        CloseDatabases();

        var (_, state) = await OpenAsync(databaseFile);

        // Act
        await state.RestoreAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { profiles[1], profiles[2] }.Order().ToList(), state.HeldProfiles.Order().ToList(), "The second and third profile should be held");
    }

    /// <summary>
    /// After restore, a tick of the unheld profile not older than the oldest held state takes the place of the oldest
    /// held profile, of equal receive times the one restored as older; a rename of a held profile does not move it.
    /// </summary>
    /// <param name="secondSeconds">The receive time of the second profile's latest tick, equal to the third's in a tie</param>
    /// <param name="newSeconds">The receive time of the new tick of the first profile</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(20, 40)]
    [DataRow(30, 30)]
    public async Task ServerStateStoreTickAsyncNewerTickOfUnheldProfileTakesOldestPlace(int secondSeconds, int newSeconds)
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var profiles = await StoreThreeProfilesAsync(databaseFile, secondSeconds);
        var tick = CreateTick(profiles[0], newSeconds, CreateIsland(1, IslandName, CreateEntry(9)));

        CloseDatabases();

        var (_, state) = await OpenRestoredAsync(databaseFile);

        await state.StoreProfileAsync(profiles[1], OtherProfileName, TestContext.CancellationToken);

        // Act
        await state.StoreTickAsync(tick, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { profiles[0], profiles[2] }.Order().ToList(), state.HeldProfiles.Order().ToList(), "The first profile should take the place of the oldest held one");
        Assert.AreSequenceEqual(DescribeTick(tick), DescribeTick((await ReadStateAsync(state, profiles[0]))?.LatestTick), "The new tick should be the first profile's state");
    }

    /// <summary>
    /// With two profiles held, a tick of an unheld profile older than the oldest held state leaves the held profiles as
    /// they are, and the profile's state still reads as its stored latest.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStateStoreTickAsyncOlderTickOfUnheldProfileHoldsNothing()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var profiles = await StoreThreeProfilesAsync(databaseFile, 20);

        CloseDatabases();

        var (store, state) = await OpenRestoredAsync(databaseFile);

        // Act
        await state.StoreTickAsync(CreateTick(profiles[0], 15, CreateIsland(1, IslandName, CreateEntry(9))), TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { profiles[1], profiles[2] }.Order().ToList(), state.HeldProfiles.Order().ToList(), "The held profiles should stay");
        Assert.AreSequenceEqual(DescribeState(await store.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken)), DescribeState(await ReadStateAsync(state, profiles[0])), "The unheld profile's state should equal its stored one");
    }

    /// <summary>
    /// A profile whose newest tick was stored while its islands could not be read enters the held profiles at its next
    /// stored tick with its stored latest state, even when that next tick is older.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStateStoreTickAsyncAfterIslandReadFailureHoldsStoredLatest()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var (store, state) = await OpenRestoredAsync(databaseFile);

        var heldId = Guid.NewGuid();
        var markedId = Guid.NewGuid();
        var newest = CreateTick(markedId, 30, CreateIsland(1, "Island of the newest tick", CreateEntry(1)));

        await state.StoreTickAsync(CreateTick(heldId, 20, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);
        ExecutePooled(databaseFile, BreakIslandReadsSql);

        var failed = await state.StoreTickAsync(newest, TestContext.CancellationToken);

        ExecutePooled(databaseFile, RepairIslandReadsSql);

        var heldAfterFailure = state.HeldProfiles.ToList();

        // Act
        await state.StoreTickAsync(CreateTick(markedId, 10, CreateIsland(2, "Island of an older tick", CreateEntry(2))), TestContext.CancellationToken);

        // Assert
        var stored = await store.ReadProfileStateAsync(markedId, TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Stored, failed.Outcome, "The tick whose islands could not be read should still be stored");
        Assert.AreSequenceEqual(new[] { (LogLevel.Error, ReadFailureLine, true) }, _logger.Entries.Select(entry => (entry.Level, entry.Message, entry.Exception is SqliteException)).ToList(), "The failed island read should be logged once as an error with its exception");
        Assert.AreSequenceEqual(new[] { heldId }, heldAfterFailure, "The held profiles should stay after the failed read");
        Assert.AreSequenceEqual(new[] { heldId, markedId }.Order().ToList(), state.HeldProfiles.Order().ToList(), "The marked profile should be held after its next tick");
        Assert.AreSequenceEqual(DescribeTick(newest), DescribeTick(stored?.LatestTick), "The stored latest tick should be the newest one, or the comparison below would prove less");
        Assert.AreSequenceEqual(DescribeState(stored), DescribeState(await ReadStateAsync(state, markedId)), "The held state should be the stored latest state with its islands");
    }

    /// <summary>
    /// The state of a profile not held equals its stored state, read from the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStateReadProfileStateAsyncOfUnheldProfileEqualsStore()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var profiles = await StoreThreeProfilesAsync(databaseFile, 20);

        CloseDatabases();

        var (store, state) = await OpenRestoredAsync(databaseFile);

        // Act
        var read = await state.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken);

        // Assert
        Assert.DoesNotContain(profiles[0], state.HeldProfiles, "The first profile should not be held, or the read would not reach the database");
        Assert.IsNull(read.Failure, "The read should not fail");
        Assert.AreSequenceEqual(DescribeState(await store.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken)), DescribeState(read.State), "The state should equal the stored one");
    }

    /// <summary>
    /// A failed read of a profile not held gives the failure and no state, and logs the error with the exception.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStateReadProfileStateAsyncFailureLogsErrorAndReturnsFailure()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var profiles = await StoreThreeProfilesAsync(databaseFile, 20);

        CloseDatabases();

        var (_, state) = await OpenRestoredAsync(databaseFile);

        Execute(databaseFile, RenameTicksSql);

        // Act
        var read = await state.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken);

        // Assert
        Assert.IsNull(read.State, "A failed read should give no state");
        Assert.IsInstanceOfType<SqliteException>(read.Failure, "A failed read should give the exception");
        Assert.AreSequenceEqual(new[] { (LogLevel.Error, ReadFailureLine, (Exception?)read.Failure) }, _logger.Entries.ToList(), "The failed read should be logged once as an error with its exception");
    }

    /// <summary>
    /// After a failed read the next read of the same profile reads the database again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStateReadProfileStateAsyncAfterFailureReadsAgain()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var profiles = await StoreThreeProfilesAsync(databaseFile, 20);

        CloseDatabases();

        var (store, state) = await OpenRestoredAsync(databaseFile);

        Execute(databaseFile, RenameTicksSql);

        var failed = await state.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken);

        Execute(databaseFile, RestoreTicksSql);

        // Act
        var read = await state.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(failed.Failure, "The read under the renamed table should fail, or the read again would prove nothing");
        Assert.IsNull(read.Failure, "The next read should not fail");
        Assert.AreSequenceEqual(DescribeState(await store.ReadProfileStateAsync(profiles[0], TestContext.CancellationToken)), DescribeState(read.State), "The next read should give the stored state");
    }

    /// <summary>
    /// The state of a profile that is not stored is none, without a failure.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ServerStateReadProfileStateAsyncOfUnknownProfileReturnsNoState()
    {
        // Arrange
        var (_, state) = await OpenRestoredAsync(null);

        await state.StoreTickAsync(CreateTick(Guid.NewGuid(), 1, CreateIsland(1, IslandName, CreateEntry(1))), TestContext.CancellationToken);

        // Act
        var read = await state.ReadProfileStateAsync(Guid.NewGuid(), TestContext.CancellationToken);

        // Assert
        Assert.IsNull(read.State, "An unknown profile should have no state");
        Assert.IsNull(read.Failure, "An unknown profile should give no failure");
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
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
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
    /// subnormal and the largest magnitudes, extreme integers, empty and filled maps and names beyond ASCII.
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

        return CreateTick(profileId,
                          7,
                          new ContractTickIsland
                          {
                              SessionGuid = -5,
                              IslandId = byte.MaxValue,
                              AreaIndex = byte.MaxValue,
                              SessionId = byte.MaxValue,
                              Name = IslandName + " é€中",
                              Entries = [edgeEntry, CreateEntry(-3)]
                          },
                          CreateIsland(1, "Second island", CreateEntry(5)));
    }

    /// <summary>
    /// Creates a catalog upload and its read document: one entry per icon reference, or one entry without an icon.
    /// </summary>
    /// <param name="firstGuid">The asset GUID of the first entry, which tells catalogs apart</param>
    /// <param name="icons">The icon references</param>
    /// <returns>The upload and its document</returns>
    private static (ContractCatalogUpload Upload, CatalogDocument Document) CreateCatalog(int firstGuid, params string[] icons)
    {
        List<string> entries = icons.Length == 0
                                   ? [CatalogEntryJson(firstGuid, "null")]
                                   : [.. icons.Select((icon, index) => CatalogEntryJson(firstGuid + index, $"\"{icon}\""))];
        var bytes = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[{string.Join(',', entries)}]}}");
        var document = CatalogDocumentReader.Read(bytes).Document ?? throw new InvalidOperationException("The test catalog should be read");

        return (new ContractCatalogUpload
                {
                    Id = Guid.NewGuid(),
                    Document = bytes
                },
                document);
    }

    /// <summary>
    /// Gives the JSON of a catalog entry.
    /// </summary>
    /// <param name="guid">The asset GUID</param>
    /// <param name="icon">The JSON of the icon reference</param>
    /// <returns>The JSON</returns>
    private static string CatalogEntryJson(int guid, string icon)
    {
        return $"{{\"guid\":{guid.ToString(CultureInfo.InvariantCulture)},\"kind\":\"good\",\"names\":{{}},\"regionGuid\":null,\"regionTypes\":null,\"icon\":{icon}}}";
    }

    /// <summary>
    /// Creates an icon image.
    /// </summary>
    /// <param name="reference">The reference key</param>
    /// <returns>The icon</returns>
    private static ContractIconImage CreateIcon(string reference)
    {
        return new ContractIconImage
               {
                   Id = Guid.NewGuid(),
                   Reference = reference,
                   Png = Encoding.UTF8.GetBytes("png " + reference)
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
    /// Describes a state as text: the profile, the latest tick and the islands in the order given.
    /// </summary>
    /// <param name="state">The state, or <see langword="null" /></param>
    /// <returns>The lines</returns>
    private static List<string> DescribeState(StoredProfileState? state)
    {
        if (state is null)
        {
            return ["<no state>"];
        }

        return [DescribeProfile(state.Profile.Id, state.Profile.Name), .. DescribeTick(state.LatestTick), .. DescribeIslands(state.Islands).Select(island => "listed " + island)];
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
    /// Describes an island of a tick and its entries as text.
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
        return $"profile {profileId} {name ?? "<no name>"}";
    }

    /// <summary>
    /// Describes listed profiles as text, in the order given.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeProfiles(IReadOnlyList<StoredProfile> profiles)
    {
        return profiles.Select(profile => DescribeProfile(profile.Id, profile.Name)).ToList();
    }

    /// <summary>
    /// Describes listed islands as text (key, then name), in the order given.
    /// </summary>
    /// <param name="islands">The islands</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeIslands(IReadOnlyList<StoredIsland> islands)
    {
        return islands.Select(island => $"{island.SessionGuid}/{island.IslandId}/{island.AreaIndex} {island.Name}").ToList();
    }

    /// <summary>
    /// Describes a notice of a follower as text.
    /// </summary>
    /// <param name="kind">The kind of the change</param>
    /// <param name="profileId">The profile identity, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string DescribeNotice(ServerStateChangeKind kind, Guid? profileId)
    {
        return $"{kind} {profileId?.ToString() ?? "<no profile>"}";
    }

    /// <summary>
    /// Adds a follower that records each notice it is told.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="expected">The number of notices after which the returned task completes</param>
    /// <returns>The notices and the task that completes once the expected number was told</returns>
    private static (ConcurrentQueue<string> Notices, Task Told) Follow(ServerState state, int expected)
    {
        var notices = new ConcurrentQueue<string>();
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        state.Changed += (_, change) =>
                         {
                             notices.Enqueue(DescribeNotice(change.Kind, change.ProfileId));

                             if (notices.Count >= expected)
                             {
                                 told.TrySetResult();
                             }
                         };

        return (notices, told.Task);
    }

    /// <summary>
    /// Opens a connection of its own, without the pool, to a SQLite file and runs a statement.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="sql">The statement</param>
    private static void Execute(string databaseFile, string sql)
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };

        ExecuteOn(builder.ToString(), sql);
    }

    /// <summary>
    /// Runs a statement over a pooled connection with the connection string the store's reads of a file use, so that the
    /// store's next read takes that connection with what the statement left on it.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="sql">The statement</param>
    private static void ExecutePooled(string databaseFile, string sql)
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite
                      };

        ExecuteOn(builder.ToString(), sql);
    }

    /// <summary>
    /// Runs a statement over a connection.
    /// </summary>
    /// <param name="connectionString">The connection string</param>
    /// <param name="sql">The statement</param>
    private static void ExecuteOn(string connectionString, string sql)
    {
        using var connection = new SqliteConnection(connectionString);

        connection.Open();

        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only
        command.CommandText = sql;
#pragma warning restore CA2100

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Reads the state of a profile and fails on a read failure.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The profile state, or <see langword="null" /></returns>
    private async Task<StoredProfileState?> ReadStateAsync(ServerState state, Guid profileId)
    {
        var read = await state.ReadProfileStateAsync(profileId, TestContext.CancellationToken);

        return read.Failure is null
                   ? read.State
                   : throw new InvalidOperationException("The state could not be read", read.Failure);
    }

    /// <summary>
    /// Describes the whole state: the profiles in their order with each one's state, the held profiles and the complete
    /// catalog.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <returns>The lines</returns>
    private async Task<List<string>> DescribeWholeStateAsync(ServerState state)
    {
        var lines = new List<string>();

        foreach (var profile in state.Profiles)
        {
            lines.AddRange(DescribeState(await ReadStateAsync(state, profile.Id)));
        }

        lines.AddRange(state.HeldProfiles.Order().Select(profileId => $"held {profileId}"));
        lines.Add("catalog " + (state.CompleteCatalogIdentity ?? "<none>"));

        return lines;
    }

    /// <summary>
    /// Stores three profiles over a new state on a database file: the first with its latest tick at 10 s and an older
    /// tick stored last, the second at the given time, the third at 30 s.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <param name="secondSeconds">The receive time of the second profile's latest tick</param>
    /// <returns>The profile identities</returns>
    private async Task<Guid[]> StoreThreeProfilesAsync(string databaseFile, int secondSeconds)
    {
        var (_, state) = await OpenRestoredAsync(databaseFile);

        Guid[] profiles = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        await StoreTicksAsync(state,
                              CreateTick(profiles[0], 10, CreateIsland(1, IslandName, CreateEntry(1))),
                              CreateTick(profiles[1], secondSeconds, CreateIsland(1, IslandName, CreateEntry(2))),
                              CreateTick(profiles[2], 30, CreateIsland(1, IslandName, CreateEntry(3))),
                              CreateTick(profiles[0], 5, CreateIsland(2, "Island of an older tick", CreateEntry(4))));

        return profiles;
    }

    /// <summary>
    /// Stores ticks in order and fails on a tick not stored.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="ticks">The ticks</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task StoreTicksAsync(ServerState state, params ContractTick[] ticks)
    {
        foreach (var tick in ticks)
        {
            var result = await state.StoreTickAsync(tick, TestContext.CancellationToken);

            if (result.Outcome != ServerStoreOutcome.Stored)
            {
                throw new InvalidOperationException($"The test tick was not stored: {result.Outcome}", result.Failure);
            }
        }
    }

    /// <summary>
    /// Creates the path of a database file in a new temporary directory.
    /// </summary>
    /// <returns>The full path of the database file, not yet created</returns>
    private string CreateDatabaseFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-server-state-tests-" + Guid.NewGuid().ToString("N"));

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
    /// Opens a database, a store on it and a state over the store, not restored, and keeps the database for the cleanup.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null" /> for an in-memory database</param>
    /// <returns>The store and the state</returns>
    private async Task<(ServerStore Store, ServerState State)> OpenAsync(string? databaseFile)
    {
        var result = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        var store = new ServerStore(database);

        return (store, new ServerState(store, _logger));
    }

    /// <summary>
    /// Opens a database, a store and a state, and restores the state, as the server does at start.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null" /> for an in-memory database</param>
    /// <returns>The store and the restored state</returns>
    private async Task<(ServerStore Store, ServerState State)> OpenRestoredAsync(string? databaseFile)
    {
        var (store, state) = await OpenAsync(databaseFile);

        await state.RestoreAsync(TestContext.CancellationToken);

        return (store, state);
    }

    #endregion // Methods
}