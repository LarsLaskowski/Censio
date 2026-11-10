using Censio.GameData;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

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
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(new GameCatalog([Entry(1)], [], 0, 0), [], 0), _logger);

        // Act
        loader.Run(Found("C:\\Game"));

        // Assert
        Assert.DoesNotContain(line => line.Message.Contains("could not be checked", StringComparison.Ordinal), Lines(), "No line about unchecked archives should be logged");
    }

    /// <summary>
    /// While the catalog is built the status is reading and no catalog is published; the build runs in the caller's
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
                                           _logger);

        // Act
        var run = Task.Run(() => loader.Run(Found("C:\\Game")), TestContext.CancellationToken);

        Assert.IsTrue(entered.Wait(_guard, TestContext.CancellationToken), "The build should start");

        var during = loader.Status;
        var catalogDuring = loader.Catalog;

        release.Set();

        var status = await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.Reading, during.Kind, "The status should be reading while the catalog is built");
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
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromError(new GameCatalogError(kind, Secret)), _logger);

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
    /// A failed icon step logs a warning with the kind and the archive, keeps the catalog, its status and its log lines, and
    /// publishes no icons.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogLoaderRunLogsIconFailureAndKeepsNames()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(1)], [], 0, 0);
        var loader = new GameCatalogLoader(_ => GameCatalogResult.FromCatalog(catalog, [], 0),
                                           (_, _) => GameIconResult.FromError(new GameIconError(GameIconErrorKind.DuplicateArchiveNumber, "zz_patchfiles_300.rda")),
                                           _logger);

        // Act
        var status = loader.Run(Found("C:\\Game"));

        // Assert
        Assert.AreEqual(GameCatalogStatusKind.Ready, status.Kind, "The catalog should stay ready");
        Assert.AreEqual(1, status.EntryCount, "The entry count should be unchanged");
        Assert.AreEqual(status, loader.Status, "The loader should keep the status it returns");
        Assert.AreSame(catalog, loader.Catalog, "The catalog should be kept");
        Assert.IsNull(loader.Icons, "No icons should be published");
        Assert.Contains((LogLevel.Warning, "Catalog icons failed: DuplicateArchiveNumber (zz_patchfiles_300.rda)"), Lines(), "The log should name the kind and the archive");
        Assert.Contains((LogLevel.Information, "Catalog built: 1 entries; 0 inherited assets left out; 0 unknown region types dropped"), Lines(), "The line about the catalog should be unchanged");
        Assert.DoesNotContain(line => line.Message.StartsWith("Catalog icons:", StringComparison.Ordinal), Lines(), "No line of counts should be logged");
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
                                       _logger);
        loader.StatusChanged += (_, status) => steps.Add($"status {status.Kind} {loader.Icons is null}");
        loader.IconsChanged += (_, _) => steps.Add("icons");

        // Act
        loader.Run(Found("C:\\Game"));

        // Assert
        Assert.AreSequenceEqual(["status Ready True", "build Ready True True", "icons"], steps, "The names and their event should come before the icon step, and the icons last");
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