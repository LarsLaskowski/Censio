using System.Globalization;

using Censio.GameData;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogLoader" />.
/// </summary>
[TestClass]
public sealed class GameCatalogLoaderTests
{
    #region Constants

    /// <summary>
    /// A text that stands for content of a file and must never reach the log.
    /// </summary>
    private const string Secret = "INVENTED-CONTENT-7QX";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The logger of the loader.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogLoaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public GameCatalogLoaderTests(TestContext testContext)
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
    /// Without an installation, the catalog has no game and nothing is built.
    /// </summary>
    /// <param name="kind">The kind of the installation status</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameInstallationStatusKind.Searching)]
    [DataRow(GameInstallationStatusKind.NotFound)]
    public void GameCatalogLoaderRunGivesNoGameWithoutInstallation(GameInstallationStatusKind kind)
    {
        // Arrange
        var builds = 0;
        var loader = new GameCatalogLoader(_ =>
                                           {
                                               builds++;

                                               return GameCatalogResult.FromError(new GameCatalogError(GameCatalogErrorKind.FileMissing, "x"));
                                           },
                                           NoIcons,
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);

        // Act
        var status = loader.Run(new GameInstallationStatus
                                {
                                    Kind = kind
                                });

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.NoGame, status.Kind, "Without an installation the catalog should have no game");
        Assert.AreEqual(status, loader.Status, "The loader should publish the status it returns");
        Assert.IsNull(loader.Catalog, "No catalog should be published");
        Assert.AreEqual(0, builds, "Nothing should be built");
    }

    /// <summary>
    /// A settings error is no installation: every kind but <c>Found</c> gives no game, though the status may carry a
    /// folder.
    /// </summary>
    /// <param name="kind">The kind of the installation status</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotFound)]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotFullPath)]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotInstallation)]
    [DataRow(GameInstallationStatusKind.SettingsFileInvalid)]
    public void GameCatalogLoaderRunGivesNoGameForSettingsError(GameInstallationStatusKind kind)
    {
        // Arrange
        var builds = 0;
        var loader = new GameCatalogLoader(_ =>
                                           {
                                               builds++;

                                               return GameCatalogResult.FromError(new GameCatalogError(GameCatalogErrorKind.FileMissing, "x"));
                                           },
                                           NoIcons,
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);

        // Act
        var status = loader.Run(new GameInstallationStatus
                                {
                                    Kind = kind,
                                    Folder = "C:\\Settings\\Game"
                                });

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.NoGame, status.Kind, "A settings error should give no game");
        Assert.AreEqual(GameCatalogStatusKind.NoGame, loader.Status.Kind, "The loader should publish no game");
        Assert.AreEqual(0, builds, "Nothing should be built");
    }

    /// <summary>
    /// A found installation is built from its folder: the status is ready with the entry count, the catalog is
    /// published before the event, and the log holds the counts, the copies and the unchecked archives.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogLoaderRunGivesReadyWithEntryCount()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(1), Entry(2), Entry(3)], [], 1, 2);
        var folders = new List<string>();
        var loader = new GameCatalogLoader(folder =>
                                           {
                                               folders.Add(folder);

                                               return GameCatalogResult.FromCatalog(catalog, [new GameCatalogCopy("patch1.rda", "data/a.xml")], 4);
                                           },
                                           NoIcons,
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);
        var events = new List<(GameCatalogStatus Status, GameCatalogStatus Published, GameCatalog? Catalog)>();

        loader.StatusChanged += (_, status) => events.Add((status, loader.Status, loader.Catalog));

        // Act
        var status = loader.Run(Found("C:\\Game"));

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.Ready, status.Kind, "A built catalog should be ready");
        Assert.AreEqual(3, status.EntryCount, "The status should carry the entry count");
        Assert.AreEqual(status, loader.Status, "The loader should publish the status it returns");
        Assert.AreSame(catalog, loader.Catalog, "The loader should publish the catalog");
        Assert.AreSequenceEqual(["C:\\Game"], folders, "The catalog should be built from the folder of the installation");
        Assert.IsNotEmpty(events, "The status change should be raised");
        Assert.AreEqual(status, events[^1].Status, "The last event should carry the ready status");
        Assert.AreEqual(status, events[^1].Published, "The status should be published before the event");
        Assert.AreSame(catalog, events[^1].Catalog, "The catalog should be published before the event");
        Assert.Contains((LogLevel.Information, "Catalog built: 3 entries; 1 inherited assets left out; 2 unknown region types dropped"), Lines(), "The log should hold the counts");
        Assert.Contains((LogLevel.Warning, "Catalog: patch1.rda also lists data/a.xml; the copy in config.rda is used"), Lines(), "The log should name the other archive and the path");
        Assert.Contains((LogLevel.Warning, "Catalog: 4 other archives could not be checked for copies"), Lines(), "The log should count the archives that could not be checked");
    }

    /// <summary>
    /// With no archive unchecked, the log holds no line about them.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogLoaderRunLogsNoUncheckedArchivesWhenAllChecked()
    {
        // Arrange
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(new GameCatalog([Entry(1)], [], 0, 0), [], 0), NoIcons, _ => null, NewStore(), new FakeTimeProvider(), _logger);

        // Act
        loader.Run(Found("C:\\Game"));

        // Assert
        Assert.DoesNotContain(line => line.Message.Contains("could not be checked", StringComparison.Ordinal), Lines(), "No line about unchecked archives should be logged");
    }

    /// <summary>
    /// While the catalog is built the status says that the names are extracted and no catalog is published; the build runs in the caller's
    /// background task.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task GameCatalogLoaderStatusIsReadingWhileBuilding()
    {
        // Arrange
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var catalog = new GameCatalog([Entry(1)], [], 0, 0);
        var loader = new GameCatalogLoader(_ =>
                                           {
                                               entered.Set();
                                               release.Wait(_guard, TestContext.CancellationToken);

                                               return GameCatalogResult.FromCatalog(catalog, [], 0);
                                           },
                                           NoIcons,
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);

        // Act
        var run = Task.Run(() => loader.Run(Found("C:\\Game")), TestContext.CancellationToken);

        Assert.IsTrue(entered.Wait(_guard, TestContext.CancellationToken), "The build should start");

        var during = loader.Status;
        var catalogDuring = loader.Catalog;

        release.Set();

        var status = await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.ExtractingNames, during.Kind, "The status should say that the names are extracted while the catalog is built");
        Assert.IsNull(catalogDuring, "No catalog should be published while it is built");
        Assert.AreEqual(GameCatalogStatusKind.Ready, status.Kind, "The status should be ready afterwards");
        Assert.AreEqual(GameCatalogStatusKind.Ready, loader.Status.Kind, "The loader should publish the ready status");
    }

    /// <summary>
    /// A failed build gives the failed status, no catalog, and a log line with the failure kind and no content.
    /// </summary>
    /// <param name="kind">The failure kind</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(GameCatalogErrorKind.ArchiveUnreadable)]
    [DataRow(GameCatalogErrorKind.FileMissing)]
    [DataRow(GameCatalogErrorKind.FileUnreadable)]
    [DataRow(GameCatalogErrorKind.MalformedFile)]
    public void GameCatalogLoaderRunGivesFailedAndLogsKindOnly(GameCatalogErrorKind kind)
    {
        // Arrange
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromError(new GameCatalogError(kind, Secret)), NoIcons, _ => null, NewStore(), new FakeTimeProvider(), _logger);

        // Act
        var status = loader.Run(Found("C:\\Game"));

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.Failed, status.Kind, "A failed build should give the failed status");
        Assert.AreEqual(status, loader.Status, "The loader should publish the status it returns");
        Assert.IsNull(loader.Catalog, "No catalog should be used after a failure");
        Assert.Contains((LogLevel.Warning, $"Catalog failed: {kind}"), Lines(), "The log should name the failure kind");
        Assert.DoesNotContain(entry => entry.Message.Contains(Secret, StringComparison.Ordinal), _logger.Entries, "The log should hold no content of the message");
    }

    /// <summary>
    /// After the catalog is ready the icon step runs with the folder and the catalog, its icons are published before the
    /// event, and the log holds the counts only.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogLoaderRunLogsIconCounts()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(1)], [], 0, 0);
        var icons = new GameIconSet(new Dictionary<int, GameIcon>
                                    {
                                        [1] = new(1, 1, new byte[] { 1 })
                                    },
                                    new Dictionary<int, GameIconProblem>
                                    {
                                        [2] = GameIconProblem.NotNamed,
                                        [3] = GameIconProblem.NotNamed,
                                        [4] = GameIconProblem.Missing,
                                        [5] = GameIconProblem.Unreadable
                                    },
                                    3,
                                    2);
        var calls = new List<(string Folder, GameCatalog Catalog)>();
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(catalog, [], 0),
                                           (folder, built) =>
                                           {
                                               calls.Add((folder, built));

                                               return GameIconResult.FromIcons(icons);
                                           },
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);
        var events = new List<(object? Sender, GameIconSet? Published)>();

        loader.IconsChanged += (sender, _) => events.Add((sender, loader.Icons));

        // Act
        loader.Run(Found("C:\\Game"));

        // Assert
        Assert.AreSequenceEqual(["C:\\Game"], calls.Select(call => call.Folder), "The icons should be built from the folder of the installation");
        Assert.AreSame(catalog, calls[0].Catalog, "The icons should be built for the catalog");
        Assert.AreSame(icons, loader.Icons, "The loader should publish the icons");
        Assert.HasCount(1, events, "The change of the icons should be raised once");
        Assert.AreSame(loader, events[0].Sender, "The event should come from the loader");
        Assert.AreSame(icons, events[0].Published, "The icons should be published before the event");
        Assert.Contains((LogLevel.Information, "Catalog icons: 3 goods and 2 regions with an icon; without one: 2 not named, 1 missing, 1 unreadable"), Lines(), "The log should hold the counts");
    }

    /// <summary>
    /// A failed icon step logs a warning with the kind and the archive, keeps the catalog and its log lines, ends in the
    /// status that says the icons failed, and publishes no icons.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogLoaderRunLogsIconFailureAndKeepsNames()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(1)], [], 0, 0);
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(catalog, [], 0), (_, _) => GameIconResult.FromError(new GameIconError(GameIconErrorKind.DuplicateArchiveNumber, "zz_patchfiles_300.rda")), _ => null, NewStore(), new FakeTimeProvider(), _logger);

        // Act
        var status = loader.Run(Found("C:\\Game"));

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.IconsFailed, status.Kind, "The status should say that the icons failed");
        Assert.AreEqual(1, status.EntryCount, "The entry count should be unchanged");
        Assert.AreEqual(status, loader.Status, "The loader should keep the status it returns");
        Assert.AreSame(catalog, loader.Catalog, "The catalog should be kept");
        Assert.IsNull(loader.Icons, "No icons should be published");
        Assert.Contains((LogLevel.Warning, "Catalog icons failed: DuplicateArchiveNumber (zz_patchfiles_300.rda)"), Lines(), "The log should name the kind and the archive");
        Assert.Contains((LogLevel.Information, "Catalog built: 1 entries; 0 inherited assets left out; 0 unknown region types dropped"), Lines(), "The line about the catalog should be unchanged");
        Assert.DoesNotContain(line => line.Message.StartsWith("Catalog icons:", StringComparison.Ordinal), Lines(), "No line of counts should be logged");
    }

    /// <summary>
    /// A run after icons were published drops them and raises the change of the icons again, so that no icon set outlives
    /// its catalog.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogLoaderSecondRunClearsPublishedIcons()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(1)], [], 0, 0);
        var icons = new GameIconSet(new Dictionary<int, GameIcon>(), new Dictionary<int, GameIconProblem>(), 0, 0);
        var builds = 0;
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(catalog, [], 0),
                                           (_, _) => ++builds == 1
                                                         ? GameIconResult.FromIcons(icons)
                                                         : GameIconResult.FromError(new GameIconError(GameIconErrorKind.ArchiveUnreadable, "ui.rda")),
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);
        var published = new List<GameIconSet?>();

        loader.IconsChanged += (_, _) => published.Add(loader.Icons);

        // Act
        loader.Run(Found("C:\\Game"));
        loader.Run(Found("C:\\Game"));

        // Assert
        Assert.HasCount(2, published, "The icons should be announced when published and when cleared");
        Assert.AreSame(icons, published[0], "The first event should carry the icons");
        Assert.IsNull(published[1], "The second event should come with the icons cleared");
        Assert.IsNull(loader.Icons, "No icons of the earlier run should remain");
    }

    /// <summary>
    /// The names are published, with their event, before the icon step starts, and the icons follow with their own event.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogLoaderRunPublishesNamesBeforeIcons()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(1)], [], 0, 0);
        var icons = new GameIconSet(new Dictionary<int, GameIcon>(), new Dictionary<int, GameIconProblem>(), 0, 0);
        var steps = new List<string>();
        GameCatalogLoader? loader = null;

        loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(catalog, [], 0),
                                       (_, _) =>
                                       {
                                           steps.Add($"build {loader!.Status.Kind} {ReferenceEquals(loader.Catalog, catalog)} {loader.Icons is null}");

                                           return GameIconResult.FromIcons(icons);
                                       },
                                       _ => null,
                                       NewStore(),
                                       new FakeTimeProvider(),
                                       _logger);
        loader.StatusChanged += (_, status) => steps.Add($"status {status.Kind} {loader.Icons is null}");
        loader.IconsChanged += (_, _) => steps.Add("icons");

        // Act
        loader.Run(Found("C:\\Game"));

        // Assert
        var namesPublished = steps.IndexOf("status ExtractingIcons True");
        var iconStep = steps.IndexOf("build ExtractingIcons True True");
        var iconsPublished = steps.IndexOf("icons");

        Assert.IsGreaterThanOrEqualTo(0, namesPublished, "The names should be published with the status that says the icons are extracted");
        Assert.IsGreaterThan(namesPublished, iconStep, "The icon step should start after the names and their event, with the catalog published and no icons");
        Assert.IsGreaterThan(iconStep, iconsPublished, "The icons should be published last");
    }

    /// <summary>
    /// Without a game, or when the catalog fails, the icon step does not run and nothing is published.
    /// </summary>
    /// <param name="kind">The kind of the installation status</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(GameInstallationStatusKind.NotFound)]
    [DataRow(GameInstallationStatusKind.Found)]
    public void GameCatalogLoaderRunWithoutGameBuildsNoIcons(GameInstallationStatusKind kind)
    {
        // Arrange
        var iconBuilds = 0;
        var raised = 0;
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromError(new GameCatalogError(GameCatalogErrorKind.FileMissing, "x")),
                                           (_, _) =>
                                           {
                                               iconBuilds++;

                                               return GameIconResult.FromError(new GameIconError(GameIconErrorKind.ArchiveMissing, "ui.rda"));
                                           },
                                           _ => null,
                                           NewStore(),
                                           new FakeTimeProvider(),
                                           _logger);

        loader.IconsChanged += (_, _) => raised++;

        // Act
        loader.Run(new GameInstallationStatus
                   {
                       Kind = kind,
                       Folder = "C:\\Game"
                   });

        // Assert
        Assert.AreEqual(0, iconBuilds, "No icons should be built");
        Assert.AreEqual(0, raised, "No change of the icons should be raised");
        Assert.IsNull(loader.Icons, "No icons should be published");
        Assert.DoesNotContain(line => line.Message.StartsWith("Catalog icons", StringComparison.Ordinal), Lines(), "No line about icons should be logged");
    }

    #region Cache and checks

    /// <summary>
    /// A cache of the same game state and identity is published without building anything: names, icons, the status with the
    /// entry count and one event for the catalog.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogLoaderRunWithMatchingCacheBuildsNothing()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Prime(CatalogLoaderRig.Key());

        // Act
        var status = rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreEqual(0, rig.BuildCalls, "No catalog should be built");
        Assert.AreEqual(0, rig.IconCalls, "No icons should be built");
        Assert.AreEqual(GameCatalogStatusKind.Ready, status.Kind, "The loaded catalog should be ready");
        Assert.AreEqual(2, status.EntryCount, "The status should carry the entry count of the cache");
        Assert.AreEqual(2, rig.Loader.Catalog!.Count, "The names of the cache should be published");
        Assert.AreSequenceEqual(rig.BuiltIcons.Find(1)!.Png.ToArray(), rig.Loader.Icons!.Find(1)!.Png.ToArray(), "The icons of the cache should be published");
        Assert.HasCount(1, rig.CatalogEvents, "The catalog should be announced once");
        Assert.AreSame(rig.Loader.Catalog, rig.CatalogEvents[0].Catalog, "The announced catalog should be the published one");
    }

    /// <summary>
    /// A cache load logs the load line and no other line.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogLoaderRunWithMatchingCacheLogsLoadLineOnly()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Prime(CatalogLoaderRig.Key());

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreSequenceEqual(["Catalog loaded from cache in 0.0 s: 2 entries, 1 goods and 1 regions with an icon"], rig.Messages(), "Only the load line should be logged");
        Assert.AreEqual(LogLevel.Information, rig.Logger.Entries[0].Level, "The load line should be an information");
    }

    /// <summary>
    /// The load line holds the duration of the load with one decimal and a decimal point, read from the injected clock.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameCatalogLoaderRunLogsCacheLoadDuration()
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        using var rig = new CatalogLoaderRig(clock: new SteppingTimeProvider(TimeSpan.FromSeconds(2.5)));

        rig.Prime(CatalogLoaderRig.Key());

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreSequenceEqual(["Catalog loaded from cache in 2.5 s: 2 entries, 1 goods and 1 regions with an icon"], rig.Messages(), "The duration should be read from the clock and formatted with a decimal point");
    }

    /// <summary>
    /// Without a cache, with a cache of another game state and with a cache of another Companion identity, the catalog is
    /// extracted, the reason is logged and the cache is replaced by one for the key read.
    /// </summary>
    /// <param name="reason">The reason</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("missing")]
    [DataRow("game changed")]
    [DataRow("Companion changed")]
    public void GameCatalogLoaderRunWithChangedGameExtractsAndLogsReason(string reason)
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        if (reason == "game changed")
        {
            rig.Prime(CatalogLoaderRig.Key("0"));
        }
        else if (reason == "Companion changed")
        {
            rig.Prime(CatalogLoaderRig.Key("1", "idOld"));
        }

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        var entries = rig.Logger.Entries.Select(entry => (entry.Level, entry.Message)).ToList();

        Assert.AreEqual(1, rig.BuildCalls, "The catalog should be extracted");
        Assert.AreEqual(1, rig.IconCalls, "The icons should be extracted");
        Assert.Contains((LogLevel.Information, $"Catalog cache not used: {reason}"), entries, "The reason should be logged");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, rig.Store.Load(CatalogLoaderRig.Key()).Kind, "The cache should be replaced by one for the key read");
    }

    /// <summary>
    /// A key that cannot be read is logged like a changed game and the catalog is extracted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogLoaderRunWithUnreadableKeyExtracts()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Prime(CatalogLoaderRig.Key());
        rig.CurrentKey = null;

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreEqual(1, rig.BuildCalls, "The catalog should be extracted");
        Assert.Contains((LogLevel.Information, "Catalog cache not used: game changed"), rig.Logger.Entries.Select(entry => (entry.Level, entry.Message)).ToList(), "The cache should count as not used because the game changed");
        Assert.AreSame(rig.BuiltCatalog, rig.Loader.Catalog, "The extracted catalog should be published");
    }

    /// <summary>
    /// The cache is stored with the key read before the extraction, so a change during the extraction shows at the next
    /// check.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogLoaderRunStoresKeyTakenBeforeExtraction()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.OnBuild = () => rig.CurrentKey = CatalogLoaderRig.Key("2");

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, rig.Store.Load(CatalogLoaderRig.Key("1")).Kind, "The cache should hold the key taken before the extraction");
        Assert.AreEqual(GameCatalogCacheResultKind.GameChanged, rig.Store.Load(CatalogLoaderRig.Key("2")).Kind, "The cache should not hold the state the files had after the extraction");
    }

    /// <summary>
    /// After a change during the extraction the next check extracts again.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task GameCatalogLoaderConnectedAfterChangeDuringExtractionExtractsAgain()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.OnBuild = () => rig.CurrentKey = CatalogLoaderRig.Key("2");
        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.OnBuild = null;

        // Act
        await Check(rig, "2");

        // Assert
        Assert.AreEqual(2, rig.BuildCalls, "The next check should extract the state that changed during the extraction");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, rig.Store.Load(CatalogLoaderRig.Key("2")).Kind, "The cache should be replaced");
    }

    /// <summary>
    /// An invalid cache logs a warning, is not used in part and is replaced by an extraction.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogLoaderRunInvalidCacheWarnsAndExtracts()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        Directory.CreateDirectory(rig.CacheFolder);
        File.WriteAllBytes(Path.Combine(rig.CacheFolder, GameCatalogCacheStore.FileName), [1, 2, 3, 4, 5]);

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.Contains((LogLevel.Warning, "Catalog cache not used: invalid"), rig.Logger.Entries.Select(entry => (entry.Level, entry.Message)).ToList(), "The invalid cache should be logged as a warning");
        Assert.AreEqual(1, rig.BuildCalls, "The catalog should be extracted");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, rig.Store.Load(CatalogLoaderRig.Key()).Kind, "The invalid cache should be replaced");
    }

    /// <summary>
    /// The statuses of a start follow the texts of the menu: checking, extracting names, extracting icons with the count, and
    /// the end; a loaded cache goes from checking to ready.
    /// </summary>
    /// <param name="row">The row</param>
    /// <param name="expected">The statuses with their entry count, where there is one</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("noCache", "Checking,ExtractingNames,ExtractingIcons:2,Ready:2")]
    [DataRow("cache", "Checking,Ready:2")]
    [DataRow("iconsFailed", "Checking,ExtractingNames,ExtractingIcons:2,IconsFailed:2")]
    [DataRow("notCached", "Checking,ExtractingNames,ExtractingIcons:2,NotCached:2")]
    [DataRow("catalogFailed", "Checking,ExtractingNames,Failed")]
    public void GameCatalogLoaderRunReportsStatusSequence(string row, string expected)
    {
        // Arrange
        using var rig = new CatalogLoaderRig(row == "notCached");

        rig.IconsFail = row == "iconsFailed";
        rig.CatalogFails = row == "catalogFailed";

        if (row == "cache")
        {
            rig.Prime(CatalogLoaderRig.Key());
        }

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        var sequence = string.Join(',', rig.Statuses.Select(status => status.EntryCount > 0 ? $"{status.Kind}:{status.EntryCount}" : status.Kind.ToString()));

        Assert.AreEqual(expected, sequence, "The statuses should follow the texts of the menu");
    }

    /// <summary>
    /// A failed catalog is not cached and no cache of an old state is used.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogLoaderRunCatalogFailedCachesNothing()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.CatalogFails = true;
        rig.Prime(CatalogLoaderRig.Key("0"));

        // Act
        var status = rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.Failed, status.Kind, "The catalog should have failed");
        Assert.IsNull(rig.Loader.Catalog, "No catalog of an old state should be used");
        Assert.IsNull(rig.Loader.Icons, "No icons of an old state should be used");
        Assert.AreEqual(GameCatalogCacheResultKind.GameChanged, rig.Store.Load(CatalogLoaderRig.Key()).Kind, "Nothing of this state should be cached");
    }

    /// <summary>
    /// A catalog without icons is not cached.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogLoaderRunIconsFailedCachesNothing()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.IconsFail = true;

        // Act
        var status = rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.IconsFailed, status.Kind, "The icons should have failed");
        Assert.AreSame(rig.BuiltCatalog, rig.Loader.Catalog, "The names should be kept");
        Assert.IsFalse(File.Exists(Path.Combine(rig.CacheFolder, GameCatalogCacheStore.FileName)), "No cache file should be written");
    }

    /// <summary>
    /// A cache that cannot be written leaves names and icons published, shows that it is not cached and logs a warning with
    /// the exception.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogLoaderRunCacheNotWritableKeepsCatalog()
    {
        // Arrange
        using var rig = new CatalogLoaderRig(true);

        // Act
        var status = rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        var warning = rig.Logger.Entries.Single(entry => entry.Message == "Catalog cache not written");

        Assert.AreEqual(GameCatalogStatusKind.NotCached, status.Kind, "The status should say that the catalog is not cached");
        Assert.AreEqual(2, status.EntryCount, "The status should carry the entry count");
        Assert.AreSame(rig.BuiltCatalog, rig.Loader.Catalog, "The names should be used");
        Assert.AreSame(rig.BuiltIcons, rig.Loader.Icons, "The icons should be used");
        Assert.AreEqual(LogLevel.Warning, warning.Level, "The failure should be logged as a warning");
        Assert.IsNotNull(warning.Exception, "The warning should carry the exception");
    }

    /// <summary>
    /// Without a game the cache is neither used nor deleted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogLoaderRunNoGameLeavesCacheFile()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Prime(CatalogLoaderRig.Key());

        var file = Path.Combine(rig.CacheFolder, GameCatalogCacheStore.FileName);
        var before = File.ReadAllBytes(file);

        // Act
        var status = rig.Loader.Run(new GameInstallationStatus
                                    {
                                        Kind = GameInstallationStatusKind.NotFound
                                    });

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.NoGame, status.Kind, "Without a game the status should say so");
        Assert.AreSequenceEqual(before, File.ReadAllBytes(file), "The cache file should be left as it is");
        Assert.IsNull(rig.Loader.Catalog, "The cache should not be used");
    }

    /// <summary>
    /// An extraction logs its durations after the icon line, with one decimal and a decimal point, read from the injected clock,
    /// also when the icons failed.
    /// </summary>
    /// <param name="iconsFail">Whether the icons fail</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false)]
    [DataRow(true)]
    public void GameCatalogLoaderRunLogsExtractionDurations(bool iconsFail)
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

        using var rig = new CatalogLoaderRig(clock: clock);

        rig.IconsFail = iconsFail;
        rig.OnBuild = () => clock.Advance(TimeSpan.FromSeconds(1.5));
        rig.OnIcons = () => clock.Advance(TimeSpan.FromSeconds(3.5));

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        var messages = rig.Messages();
        var line = messages.IndexOf("Catalog extracted in 5.0 s (names 1.5 s, icons 3.5 s)");
        var iconLine = messages.FindIndex(message => message.StartsWith("Catalog icons", StringComparison.Ordinal));

        Assert.IsGreaterThanOrEqualTo(0, line, "The durations should be logged with one decimal and a decimal point");
        Assert.IsGreaterThan(iconLine, line, "The duration line should follow the icon line");
        Assert.AreEqual(LogLevel.Information, rig.Logger.Entries[line].Level, "The duration line should be an information");
    }

    /// <summary>
    /// A connection check after a success, after each failed result and after a cache load changes, extracts and logs nothing
    /// when the game state is the same.
    /// </summary>
    /// <param name="row">The row</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("success")]
    [DataRow("catalogFailed")]
    [DataRow("iconsFailed")]
    [DataRow("notCached")]
    [DataRow("cacheLoad")]
    public async Task GameCatalogLoaderConnectedUnchangedDoesNothing(string row)
    {
        // Arrange
        using var rig = new CatalogLoaderRig(row == "notCached");

        rig.CatalogFails = row == "catalogFailed";
        rig.IconsFail = row == "iconsFailed";

        if (row == "cacheLoad")
        {
            rig.Prime(CatalogLoaderRig.Key());
        }

        rig.Loader.Run(CatalogLoaderRig.Found());

        var builds = rig.BuildCalls;
        var statuses = rig.Statuses.Count;
        var messages = rig.Logger.Entries.Count;
        var catalog = rig.Loader.Catalog;
        var icons = rig.Loader.Icons;
        var status = rig.Loader.Status;
        var events = rig.CatalogEvents.Count;

        // Act
        await Check(rig, "1");

        // Assert
        Assert.AreEqual(builds, rig.BuildCalls, "Nothing should be extracted again");
        Assert.HasCount(statuses, rig.Statuses, "No status should be raised");
        Assert.HasCount(messages, rig.Logger.Entries, "Nothing should be logged");
        Assert.HasCount(events, rig.CatalogEvents, "No catalog should be announced");
        Assert.AreSame(catalog, rig.Loader.Catalog, "The catalog should stay");
        Assert.AreSame(icons, rig.Loader.Icons, "The icons should stay");
        Assert.AreEqual(status, rig.Loader.Status, "The status should stay");
    }

    /// <summary>
    /// After a failed result the loader extracts again when the game state changed.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedChangedAfterFailedResultExtracts()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.CatalogFails = true;
        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.CatalogFails = false;
        rig.CurrentKey = CatalogLoaderRig.Key("2");

        // Act
        await Check(rig, "2");

        // Assert
        Assert.AreEqual(2, rig.BuildCalls, "The changed game should be extracted");
        Assert.AreEqual(GameCatalogStatusKind.Ready, rig.Loader.Status.Kind, "The extraction should end ready");
        Assert.AreSame(rig.BuiltCatalog, rig.Loader.Catalog, "The new catalog should be published");
    }

    /// <summary>
    /// A changed game is extracted while the previous names and icons stay until the new names are published, the icons go
    /// until the new ones are built, and the cache is replaced.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedChangedKeepsNamesUntilPublished()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();
        using var namesEntered = new ManualResetEventSlim();
        using var namesRelease = new ManualResetEventSlim();
        using var iconsEntered = new ManualResetEventSlim();
        using var iconsRelease = new ManualResetEventSlim();

        rig.Loader.Run(CatalogLoaderRig.Found());

        var previousCatalog = rig.Loader.Catalog;
        var previousIcons = rig.Loader.Icons;
        var newCatalog = CatalogLoaderRig.Catalog(3);
        var newIcons = CatalogLoaderRig.Icons();

        rig.BuiltCatalog = newCatalog;
        rig.BuiltIcons = newIcons;
        rig.CurrentKey = CatalogLoaderRig.Key("2");
        rig.OnBuild = () =>
                      {
                          namesEntered.Set();
                          namesRelease.Wait(_guard, TestContext.CancellationToken);
                      };
        rig.OnIcons = () =>
                      {
                          iconsEntered.Set();
                          iconsRelease.Wait(_guard, TestContext.CancellationToken);
                      };

        // Act
        var check = rig.Loader.OnConnectionStatusChangedAsync(Connected(), () => CatalogLoaderRig.Found("2"));

        Assert.IsTrue(namesEntered.Wait(_guard, TestContext.CancellationToken), "The names should be extracted");

        var duringNames = (rig.Loader.Status.Kind, rig.Loader.Catalog, rig.Loader.Icons);

        namesRelease.Set();

        Assert.IsTrue(iconsEntered.Wait(_guard, TestContext.CancellationToken), "The icons should be extracted");

        var duringIcons = (rig.Loader.Status, rig.Loader.Catalog, rig.Loader.Icons);

        iconsRelease.Set();
        await check.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.ExtractingNames, duringNames.Kind, "The status should say that the names are extracted");
        Assert.AreSame(previousCatalog, duringNames.Catalog, "The previous names should stay while the new ones are extracted");
        Assert.AreSame(previousIcons, duringNames.Icons, "The previous icons should stay while the new names are extracted");
        Assert.AreEqual(GameCatalogStatusKind.ExtractingIcons, duringIcons.Status.Kind, "The status should say that the icons are extracted");
        Assert.AreEqual(3, duringIcons.Status.EntryCount, "The status should carry the new entry count");
        Assert.AreSame(newCatalog, duringIcons.Catalog, "The new names should replace the previous ones when ready");
        Assert.IsNull(duringIcons.Icons, "The previous icons should go until the new ones are built");
        Assert.AreSame(newIcons, rig.Loader.Icons, "The new icons should be published");
        Assert.AreEqual(GameCatalogStatusKind.Ready, rig.Loader.Status.Kind, "The extraction should end ready");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, rig.Store.Load(CatalogLoaderRig.Key("2")).Kind, "The cache should be replaced");
    }

    /// <summary>
    /// The check reads the build again through the given function and the key reader gets the status it returned.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedChangedRefreshesBuild()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.CurrentKey = CatalogLoaderRig.Key("2");

        var refreshed = 0;

        // Act
        await rig.Loader.OnConnectionStatusChangedAsync(Connected(),
                                                        () =>
                                                        {
                                                            refreshed++;

                                                            return CatalogLoaderRig.Found("2");
                                                        }).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, refreshed, "The build should be read once");
        Assert.AreEqual("2", rig.KeyReads[^1].Build, "The key should be read for the status that holds the new build");
    }

    /// <summary>
    /// A later check that cannot read the key keeps the catalog, the icons and the status, extracts nothing and logs a warning.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedUnreadableKeepsCatalogAndWarns()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(CatalogLoaderRig.Found());

        var catalog = rig.Loader.Catalog;
        var icons = rig.Loader.Icons;
        var status = rig.Loader.Status;
        var statuses = rig.Statuses.Count;
        var before = rig.Logger.Entries.Count;

        rig.CurrentKey = null;

        // Act
        await Check(rig, "2");

        // Assert
        Assert.AreEqual(1, rig.BuildCalls, "Nothing should be extracted");
        Assert.AreSame(catalog, rig.Loader.Catalog, "The current catalog should be kept");
        Assert.AreSame(icons, rig.Loader.Icons, "The current icons should be kept");
        Assert.AreEqual(status, rig.Loader.Status, "The status should be kept");
        Assert.HasCount(statuses, rig.Statuses, "No status should be raised");
        Assert.AreSequenceEqual([(LogLevel.Warning, "Catalog check failed; the current catalog is kept")], rig.Logger.Entries.Skip(before).Select(entry => (entry.Level, entry.Message)).ToList(), "A warning should be the only new line");
    }

    /// <summary>
    /// A changed installation without a folder is not extracted and changes nothing.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedRefreshedWithoutFolderDoesNothing()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.CurrentKey = CatalogLoaderRig.Key("2");

        var statuses = rig.Statuses.Count;
        var messages = rig.Logger.Entries.Count;

        // Act
        await rig.Loader.OnConnectionStatusChangedAsync(Connected(),
                                                        () => new GameInstallationStatus
                                                              {
                                                                  Kind = GameInstallationStatusKind.Found,
                                                                  Build = "2"
                                                              }).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, rig.BuildCalls, "Nothing should be extracted");
        Assert.HasCount(statuses, rig.Statuses, "No status should be raised");
        Assert.HasCount(messages, rig.Logger.Entries, "Nothing should be logged");
    }

    /// <summary>
    /// A check that throws logs the failed check, keeps the catalog and the icons, and leaves the loader free for the next
    /// check, which extracts a changed game.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedCheckThrowsLogsWarningAndCanCheckAgain()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(CatalogLoaderRig.Found());

        var catalog = rig.Loader.Catalog;
        var icons = rig.Loader.Icons;
        var before = rig.Logger.Entries.Count;

        // Act
        await rig.Loader.OnConnectionStatusChangedAsync(Connected(), () => throw new IOException("The build cannot be read")).WaitAsync(_guard, TestContext.CancellationToken);

        var afterFailure = rig.Logger.Entries.Skip(before).Select(entry => (entry.Level, entry.Message)).ToList();
        var keptCatalog = rig.Loader.Catalog;
        var keptIcons = rig.Loader.Icons;

        rig.CurrentKey = CatalogLoaderRig.Key("2");

        await Check(rig, "2");

        // Assert
        Assert.AreSequenceEqual([(LogLevel.Warning, "Catalog check failed; the current catalog is kept")], afterFailure, "A warning should be the only new line");
        Assert.AreSame(catalog, keptCatalog, "The current catalog should be kept");
        Assert.AreSame(icons, keptIcons, "The current icons should be kept");
        Assert.AreEqual(2, rig.BuildCalls, "The next check should extract the changed game");
    }

    /// <summary>
    /// A connection during a running check starts no second check: the call returns at once and the extraction runs once.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedDuringCheckIsMerged()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.CurrentKey = CatalogLoaderRig.Key("2");
        rig.OnBuild = () =>
                      {
                          entered.Set();
                          release.Wait(_guard, TestContext.CancellationToken);
                      };

        var refreshed = 0;
        var first = rig.Loader.OnConnectionStatusChangedAsync(Connected(), () => CatalogLoaderRig.Found("2"));

        Assert.IsTrue(entered.Wait(_guard, TestContext.CancellationToken), "The check should extract");

        // Act
        var second = rig.Loader.OnConnectionStatusChangedAsync(Connected(),
                                                               () =>
                                                               {
                                                                   refreshed++;

                                                                   return CatalogLoaderRig.Found("2");
                                                               });

        var isSecondCompleted = second.IsCompleted;

        release.Set();
        await first.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(isSecondCompleted, "The merged connection should return at once");
        Assert.AreEqual(0, refreshed, "The merged connection should read nothing");
        Assert.AreEqual(2, rig.BuildCalls, "The catalog should be extracted once more");
    }

    /// <summary>
    /// A connection before the start run, and one during it, starts no check.
    /// </summary>
    /// <param name="duringRun">Whether the connection comes while the start run builds</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GameCatalogLoaderConnectedBeforeStartRunEndsIsIgnored(bool duringRun)
    {
        // Arrange
        using var rig = new CatalogLoaderRig();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        rig.OnBuild = () =>
                      {
                          entered.Set();
                          release.Wait(_guard, TestContext.CancellationToken);
                      };

        var run = duringRun ? Task.Run(() => rig.Loader.Run(CatalogLoaderRig.Found()), TestContext.CancellationToken) : null;

        if (run is not null)
        {
            Assert.IsTrue(entered.Wait(_guard, TestContext.CancellationToken), "The start run should build");
        }

        var refreshed = 0;

        // Act
        var check = rig.Loader.OnConnectionStatusChangedAsync(Connected(),
                                                              () =>
                                                              {
                                                                  refreshed++;

                                                                  return CatalogLoaderRig.Found("2");
                                                              });

        var isCompleted = check.IsCompleted;

        release.Set();

        if (run is not null)
        {
            await run.WaitAsync(_guard, TestContext.CancellationToken);
        }

        // Assert
        Assert.IsTrue(isCompleted, "The connection should return at once");
        Assert.AreEqual(0, refreshed, "No check should read the build");
        Assert.AreEqual(duringRun ? 1 : 0, rig.BuildCalls, "Only the start run should build");
    }

    /// <summary>
    /// Every other state of the pipe starts no check.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(PipeConnectionState.WaitingForGame)]
    [DataRow(PipeConnectionState.Receiving)]
    [DataRow(PipeConnectionState.PipeBusy)]
    [DataRow(PipeConnectionState.ProtocolMismatch)]
    [DataRow(PipeConnectionState.Error)]
    public async Task GameCatalogLoaderOtherStatesDoNotCheck(PipeConnectionState state)
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.CurrentKey = CatalogLoaderRig.Key("2");

        var refreshed = 0;

        // Act
        await rig.Loader.OnConnectionStatusChangedAsync(new PipeConnectionStatus
                                                        {
                                                            State = state
                                                        },
                                                        () =>
                                                        {
                                                            refreshed++;

                                                            return CatalogLoaderRig.Found("2");
                                                        }).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(0, refreshed, "No build should be read");
        Assert.AreEqual(1, rig.BuildCalls, "Nothing should be extracted");
    }

    /// <summary>
    /// Without a found game a connection starts no check.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderConnectedNoGameDoesNothing()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(new GameInstallationStatus
                       {
                           Kind = GameInstallationStatusKind.NotFound
                       });

        var refreshed = 0;

        // Act
        await rig.Loader.OnConnectionStatusChangedAsync(Connected(),
                                                        () =>
                                                        {
                                                            refreshed++;

                                                            return CatalogLoaderRig.Found("2");
                                                        }).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(0, refreshed, "No build should be read");
        Assert.AreEqual(0, rig.BuildCalls, "Nothing should be extracted");
        Assert.AreEqual(GameCatalogStatusKind.NoGame, rig.Loader.Status.Kind, "The status should stay without a game");
    }

    /// <summary>
    /// A catalog is announced once when its names are published, whatever the icons and the cache do later.
    /// </summary>
    /// <param name="row">The row</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("ready")]
    [DataRow("iconsFailed")]
    [DataRow("notCached")]
    public void GameCatalogLoaderCatalogChangedRaisedOnPublish(string row)
    {
        // Arrange
        using var rig = new CatalogLoaderRig(row == "notCached");

        rig.IconsFail = row == "iconsFailed";

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.HasCount(1, rig.CatalogEvents, "The catalog should be announced once");
        Assert.AreSame(rig.BuiltCatalog, rig.CatalogEvents[0].Catalog, "The announced catalog should be the published one");
        Assert.AreEqual(GameCatalogStatusKind.ExtractingIcons, rig.CatalogEvents[0].Status, "The catalog should be announced when the names are published");
    }

    /// <summary>
    /// A failed extraction of a changed game removes the previous names and icons and announces that there is no catalog.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task GameCatalogLoaderCatalogChangedRaisedWithoutCatalogAfterFailedExtraction()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Loader.Run(CatalogLoaderRig.Found());
        rig.CatalogFails = true;
        rig.CurrentKey = CatalogLoaderRig.Key("2");

        // Act
        await Check(rig, "2");

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.Failed, rig.Loader.Status.Kind, "The extraction should have failed");
        Assert.IsNull(rig.Loader.Catalog, "The previous names should be removed");
        Assert.IsNull(rig.Loader.Icons, "The previous icons should be removed");
        Assert.HasCount(2, rig.CatalogEvents, "The failure should be announced");
        Assert.IsNull(rig.CatalogEvents[1].Catalog, "The failure should be announced with no catalog");
    }

    /// <summary>
    /// A cache load announces the loaded catalog once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogLoaderCatalogChangedRaisedOnCacheLoad()
    {
        // Arrange
        using var rig = new CatalogLoaderRig();

        rig.Prime(CatalogLoaderRig.Key());

        // Act
        rig.Loader.Run(CatalogLoaderRig.Found());

        // Assert
        Assert.HasCount(1, rig.CatalogEvents, "The loaded catalog should be announced once");
        Assert.AreSame(rig.Loader.Catalog, rig.CatalogEvents[0].Catalog, "The announced catalog should be the published one");
    }

    #endregion // Cache and checks

    /// <summary>
    /// Creates the status of a connected pipe.
    /// </summary>
    /// <returns>The status</returns>
    private static PipeConnectionStatus Connected()
    {
        return new PipeConnectionStatus
               {
                   State = PipeConnectionState.Connected
               };
    }

    /// <summary>
    /// Creates a cache store in a folder that does not exist.
    /// </summary>
    /// <returns>The store</returns>
    private static GameCatalogCacheStore NewStore()
    {
        return new GameCatalogCacheStore(Path.Combine(Path.GetTempPath(), "censio-loader-tests", Guid.NewGuid().ToString("N")), new FakeTimeProvider());
    }

    /// <summary>
    /// Builds the icons of a catalog as an empty set.
    /// </summary>
    /// <param name="folder">The installation folder</param>
    /// <param name="catalog">The catalog</param>
    /// <returns>The icons</returns>
    private static GameIconResult NoIcons(string folder, GameCatalog catalog)
    {
        return GameIconResult.FromIcons(new GameIconSet(new Dictionary<int, GameIcon>(), new Dictionary<int, GameIconProblem>(), 0, 0));
    }

    /// <summary>
    /// Runs a check on a new connection, for a game whose build is read as given, and waits for it.
    /// </summary>
    /// <param name="rig">The rig</param>
    /// <param name="build">The build the refresh returns</param>
    /// <returns>The task</returns>
    private async Task Check(CatalogLoaderRig rig, string build)
    {
        await rig.Loader.OnConnectionStatusChangedAsync(Connected(), () => CatalogLoaderRig.Found(build)).WaitAsync(_guard, TestContext.CancellationToken);
    }

    #endregion // Methods

    #region Helpers

    /// <summary>
    /// Creates the status of a found installation.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns>The status</returns>
    private static GameInstallationStatus Found(string folder)
    {
        return new GameInstallationStatus
               {
                   Kind = GameInstallationStatusKind.Found,
                   Source = GameInstallationSource.UbisoftConnect,
                   Folder = folder,
                   Build = "1"
               };
    }

    /// <summary>
    /// Creates a good entry.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <returns>The entry</returns>
    private static GameCatalogEntry Entry(int guid)
    {
        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = GameCatalogKind.Good,
                   EnglishName = "Good 1",
                   GermanName = "Ware 1"
               };
    }

    /// <summary>
    /// Gets the level and message of every logged entry.
    /// </summary>
    /// <returns>The lines</returns>
    private List<(LogLevel Level, string Message)> Lines()
    {
        return [.. _logger.Entries.Select(entry => (entry.Level, entry.Message))];
    }

    #endregion // Helpers
}