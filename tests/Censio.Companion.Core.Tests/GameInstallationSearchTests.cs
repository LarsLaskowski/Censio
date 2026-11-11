using System.Globalization;
using System.Text;
using System.Text.Json;

using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameInstallationSearch" />.
/// </summary>
[TestClass]
public sealed class GameInstallationSearchTests
{
    #region Constants

    /// <summary>
    /// The text of the installation's files, which must never reach the log.
    /// </summary>
    private const string GameContent = "ANNO-GAME-CONTENT-7F3A";

    /// <summary>
    /// The build of <see cref="_sampleVersion" />.
    /// </summary>
    private const string SampleBuild = "26.29905.4.51797";

    /// <summary>
    /// The marker in a test text that stands for the temporary folder.
    /// </summary>
    private const string RootMarker = "<root>";

    /// <summary>
    /// The marker in a test text that stands for the temporary folder relative to the current directory.
    /// </summary>
    private const string RelativeRootMarker = "<rel>";

    /// <summary>
    /// The warning when nothing is found.
    /// </summary>
    private const string NotFoundMessage = "Game installation not found; checked the Ubisoft Connect records and its default games folder";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The file version of the sample's program file.
    /// </summary>
    private static readonly Version _sampleVersion = new(26, 29905, 4, 51797);

    /// <summary>
    /// The temporary folder of the test, which holds the installations, the settings file and the default games folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-search-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The logger the search writes to.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    /// <summary>
    /// The file version reader of the search.
    /// </summary>
    private readonly FakeFileVersionReader _versions = new();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// An installation recorded by Ubisoft Connect, written with a trailing separator, is found with its build, and the
    /// information line names the source, the build and the folder as written.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameInstallationSearchUbisoftRecordReturnsFound()
    {
        // Arrange
        var installDir = CreateInstallation("Anno 117 - Pax Romana") + "/";
        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", installDir)), MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The recorded installation should be found");
        Assert.AreEqual(GameInstallationSource.UbisoftConnect, result.Source, "The source should be Ubisoft Connect");
        Assert.AreEqual(installDir, result.Folder, "The folder should be the record's folder as written");
        Assert.AreEqual(SampleBuild, result.Build, "The build should be the program file's version");
        Assert.AreEqual(result, search.Status, "The status should be the result once the search ended");
        Assert.HasCount(1, _logger.Entries, "The search should log one line");
        Assert.AreEqual(LogLevel.Information, _logger.Entries[0].Level, "A found installation should be logged as information");
        Assert.AreEqual($"Game installation found through Ubisoft Connect, build {SampleBuild}, folder {installDir}", _logger.Entries[0].Message, "The line should name the source, the build and the folder");
    }

    /// <summary>
    /// Of several records the first valid one by ascending number is taken; names that are no number come last.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameInstallationSearchSeveralRecordsTakesFirstValidByNumber()
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords(Record("abc", CreateInstallation("abc")),
                                                    Record("10", CreateInstallation("ten")),
                                                    Record("2", CreateFolderWithoutInstallation("two")),
                                                    Record("9", CreateInstallation("nine")));
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "A valid record should be found");
        Assert.AreEqual(Path.Combine(_root, "nine"), result.Folder, "Record 9 should be taken: 2 is invalid, 9 comes before 10 by number and abc comes last");
    }

    /// <summary>
    /// Without a valid record the first installation in the default games folder, by name ignoring case, is found with
    /// the source Ubisoft Connect.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameInstallationSearchNoValidRecordFindsDefaultGamesFolder()
    {
        // Arrange
        var games = Path.Combine(_root, "games");

        CreateFolderWithoutInstallation(Path.Combine("games", "Aaa"));
        CreateInstallation(Path.Combine("games", "Beta"));
        CreateInstallation(Path.Combine("games", "alpha"));

        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", Path.Combine(_root, "uninstalled") + "/")), games);

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The installation in the default games folder should be found");
        Assert.AreEqual(GameInstallationSource.UbisoftConnect, result.Source, "The default games folder should count as Ubisoft Connect");
        Assert.AreEqual(Path.Combine(games, "alpha"), result.Folder, "The first installation by name ignoring case should be taken");
        Assert.AreEqual(SampleBuild, result.Build, "The build should be the program file's version");
    }

    /// <summary>
    /// No settings file, an empty or blank game folder, or no game folder key run the automatic search.
    /// </summary>
    /// <param name="settings">The settings text, or <see langword="null" /> for no file</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(null, DisplayName = "No file")]
    [DataRow("""{"gameFolder": ""}""", DisplayName = "Empty")]
    [DataRow("""{"gameFolder": "  "}""", DisplayName = "Blank")]
    [DataRow("""{"theme": "dark"}""", DisplayName = "No key")]
    public void GameInstallationSearchNoGameFolderRunsSearch(string? settings)
    {
        // Arrange
        var installDir = CreateInstallation("anno") + "/";
        var records = new FakeUbisoftInstallRecords(Record("921", installDir));
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(SettingsFile(settings));

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The automatic search should find the installation");
        Assert.AreEqual(GameInstallationSource.UbisoftConnect, result.Source, "The source should be Ubisoft Connect");
        Assert.AreEqual(installDir, result.Folder, "The recorded folder should be found");
        Assert.AreEqual(1, records.ReadCount, "The records should be read once");
    }

    /// <summary>
    /// A game folder in the settings file that names an installation is used without reading the records.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameInstallationSearchSettingsFolderSkipsRecords()
    {
        // Arrange
        var folder = CreateInstallation("Steam Anno");
        var records = new FakeUbisoftInstallRecords(Record("921", CreateInstallation("ubisoft") + "/"));
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(SettingsFileNaming(folder));

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The folder of the settings file should be found");
        Assert.AreEqual(GameInstallationSource.SettingsFile, result.Source, "The source should be the settings file");
        Assert.AreEqual(folder, result.Folder, "The folder should be the one of the settings file");
        Assert.AreEqual(SampleBuild, result.Build, "The build should be the program file's version");
        Assert.AreEqual(0, records.ReadCount, "The records should not be read");
        Assert.AreEqual($"Game installation found through settings file, build {SampleBuild}, folder {folder}", _logger.Entries[0].Message, "The line should name the settings file as the source");
    }

    /// <summary>
    /// A wrong game folder in the settings file gives the kind of its case and no automatic search.
    /// </summary>
    /// <param name="gameFolder">The game folder as written, with the markers of <see cref="Resolve" /></param>
    /// <param name="expected">The expected kind</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("<root>/missing", GameInstallationStatusKind.SettingsFolderNotFound, DisplayName = "Missing folder")]
    [DataRow("<rel>/anno", GameInstallationStatusKind.SettingsFolderNotFullPath, DisplayName = "Relative path to an installation")]
    [DataRow("C:", GameInstallationStatusKind.SettingsFolderNotFullPath, DisplayName = "Drive only")]
    [DataRow(@"%USERPROFILE%\x", GameInstallationStatusKind.SettingsFolderNotFullPath, DisplayName = "Starts with a variable")]
    [DataRow("<root>/half", GameInstallationStatusKind.SettingsFolderNotInstallation, DisplayName = "Folder without the two files")]
    public void GameInstallationSearchSettingsFolderProblemReturnsKind(string gameFolder, GameInstallationStatusKind expected)
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords(Record("921", CreateInstallation("anno") + "/"));

        CreateFolderWithoutInstallation("half");
        CreateInstallation(Path.Combine("games", "anno"));

        var search = CreateSearch(records, Path.Combine(_root, "games"));

        // Act
        var result = search.Run(SettingsFileNaming(Resolve(gameFolder)));

        // Assert
        Assert.AreEqual(expected, result.Kind, "The wrong game folder should give the kind of its case");
        Assert.AreEqual(0, records.ReadCount, "No automatic search should run");
        Assert.IsNull(result.Folder, "No folder should be found");
    }

    /// <summary>
    /// A wrong game folder in the settings file logs one warning naming the cause and the folder as written.
    /// </summary>
    /// <param name="gameFolder">The game folder as written, with the markers of <see cref="Resolve" /></param>
    /// <param name="template">The expected warning, with <c>{0}</c> for the folder</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("<root>/missing", "The game folder {0} in the settings file does not exist", DisplayName = "Missing folder")]
    [DataRow("C:", "The game folder {0} in the settings file is not a full path", DisplayName = "Not a full path")]
    [DataRow("<root>/half", @"The game folder {0} in the settings file is not an Anno 117 installation; expected the folder that holds maindata\config.rda and bin\win64\anno117.exe", DisplayName = "No installation")]
    public void GameInstallationSearchSettingsProblemLogsWarningAndReadsNoRecord(string gameFolder, string template)
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords(Record("921", CreateInstallation("anno") + "/"));

        CreateFolderWithoutInstallation("half");

        var folder = Resolve(gameFolder);
        var expected = string.Format(CultureInfo.InvariantCulture, template, folder);
        var search = CreateSearch(records, MissingFolder());

        // Act
        search.Run(SettingsFileNaming(folder));

        // Assert
        Assert.HasCount(1, _logger.Entries, "The search should log exactly one line");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "A wrong game folder should be logged as a warning");
        Assert.AreEqual(expected, _logger.Entries[0].Message, "The warning should name the cause and the folder as written");
        Assert.AreEqual(0, records.ReadCount, "No record should be read");
    }

    /// <summary>
    /// An invalid settings file gives its kind, logs a warning naming the file and the problem, and runs no automatic
    /// search.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameInstallationSearchInvalidSettingsFileReturnsInvalid()
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords(Record("921", CreateInstallation("anno") + "/"));
        var settings = SettingsFile("""{"gameFolder": 5}""");
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(settings);

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.SettingsFileInvalid, result.Kind, "An invalid settings file should give its kind");
        Assert.AreEqual(0, records.ReadCount, "No automatic search should run");
        Assert.HasCount(1, _logger.Entries, "The search should log exactly one line");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "An invalid settings file should be logged as a warning");
        Assert.AreEqual($"The settings file {settings} is invalid: gameFolder is not a text", _logger.Entries[0].Message, "The warning should name the file and the problem");
    }

    /// <summary>
    /// Records that cannot be read count as none, and the default games folder is searched.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void GameInstallationSearchRecordsThrowSkipsToDefaultFolder()
    {
        // Arrange
        var games = Path.Combine(_root, "games");

        CreateInstallation(Path.Combine("games", "Anno 117 - Pax Romana"));

        var records = new FakeUbisoftInstallRecords(Record("921", CreateInstallation("anno") + "/"))
                      {
                          Exception = new IOException("The records cannot be read")
                      };
        var search = CreateSearch(records, games);

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The default games folder should be searched");
        Assert.AreEqual(GameInstallationSource.UbisoftConnect, result.Source, "The default games folder should count as Ubisoft Connect");
        Assert.AreEqual(Path.Combine(games, "Anno 117 - Pax Romana"), result.Folder, "The installation of the default games folder should be found");
    }

    /// <summary>
    /// A record without a usable installation is skipped and the next record is taken; a folder written as a network
    /// path is skipped although it names an installation.
    /// </summary>
    /// <param name="installDir">The install folder of the first record, with the markers of <see cref="Resolve" /></param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(null, DisplayName = "InstallDir missing")]
    [DataRow("<rel>/first/", DisplayName = "Relative path to an installation")]
    [DataRow("<root>/missing/", DisplayName = "Missing folder")]
    [DataRow("<root>/half/", DisplayName = "No installation")]
    [DataRow("/<root>/first/", DisplayName = "Starts with two separators")]
    public void GameInstallationSearchUnusableRecordIsSkipped(string? installDir)
    {
        // Arrange
        CreateInstallation("first");
        CreateFolderWithoutInstallation("half");

        var second = CreateInstallation("second") + "/";
        var records = new FakeUbisoftInstallRecords(Record("1", ResolveOrNull(installDir)), Record("2", second));
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The next record should be found");
        Assert.AreEqual(second, result.Folder, "The unusable record should be skipped");
    }

    /// <summary>
    /// The Product Manager's records, each pointed at a temporary folder by name, are taken by number: 921 before 4553.
    /// </summary>
    /// <param name="installed">The record names whose folder holds an installation, comma-separated</param>
    /// <param name="expected">The record name whose folder is found</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("4553,921", "921", DisplayName = "Both installations")]
    [DataRow("4553", "4553", DisplayName = "Only 4553")]
    public void GameInstallationSearchSampleRecordsTakeNumericFirst(string installed, string expected)
    {
        // Arrange
        CreateInstallations(installed);

        var sample = new RegExportUbisoftInstallRecords(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GameInstallation", "ubisoft-connect-installs.reg")).Read();
        var records = new FakeUbisoftInstallRecords(sample.Select(record => record with
                                                                                   {
                                                                                       InstallDir = Path.Combine(_root, record.Name) + "/"
                                                                                   }).ToArray());
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "An installation of the sample's records should be found");
        Assert.AreEqual(Path.Combine(_root, expected) + "/", result.Folder, "The first valid record by number should be taken");
    }

    /// <summary>
    /// When nothing is found the result is not found, and one warning names the places checked, not their folders.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void GameInstallationSearchNothingFoundReturnsNotFoundAndLogsWarning()
    {
        // Arrange
        CreateFolderWithoutInstallation(Path.Combine("games", "empty"));

        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", Path.Combine(_root, "missing") + "/")), Path.Combine(_root, "games"));

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.NotFound, result.Kind, "Nothing should be found");
        Assert.IsNull(result.Folder, "No folder should be named");
        Assert.AreEqual(GameInstallationStatusKind.NotFound, search.Status.Kind, "The status should be not found");
        Assert.HasCount(1, _logger.Entries, "The search should log exactly one line");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "Nothing found should be logged as a warning");
        Assert.AreEqual(NotFoundMessage, _logger.Entries[0].Message, "The warning should name the places checked");
    }

    /// <summary>
    /// A default games folder that cannot be listed, or that is not a full path, counts as holding nothing.
    /// </summary>
    /// <param name="defaultFolder">The default games folder, with the markers of <see cref="Resolve" /></param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("<root>/games-file", DisplayName = "A file, not a folder")]
    [DataRow("<rel>/games", DisplayName = "Relative path to a folder with an installation")]
    public void GameInstallationSearchDefaultFolderUnreadableReturnsNotFound(string defaultFolder)
    {
        // Arrange
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "games-file"), GameContent);
        CreateInstallation(Path.Combine("games", "anno"));

        var search = CreateSearch(new FakeUbisoftInstallRecords(), Resolve(defaultFolder));

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.NotFound, result.Kind, "A default games folder that cannot be used should give not found");
    }

    /// <summary>
    /// The build follows the program file's version: the same version gives the same text, another version another.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameInstallationSearchBuildIsFileVersion()
    {
        // Arrange
        var installDir = CreateInstallation("anno") + "/";
        var first = CreateSearchWithVersion(installDir, _sampleVersion);
        var second = CreateSearchWithVersion(installDir, _sampleVersion);
        var updated = CreateSearchWithVersion(installDir, new Version(26, 29906, 0, 1));
        var settings = MissingSettings();

        // Act
        string?[] builds = [first.Run(settings).Build, second.Run(settings).Build, updated.Run(settings).Build];

        // Assert
        Assert.AreEqual(SampleBuild, builds[0], "The build should be the four numbers of the file version");
        Assert.AreEqual(builds[0], builds[1], "The same file version should give the same build at every start");
        Assert.AreEqual("26.29906.0.1", builds[2], "Another file version should give its own build");
        Assert.AreNotEqual(builds[0], builds[2], "Another file version should give another build");
    }

    /// <summary>
    /// A program file without a usable version gives an unknown build, and the installation is still used.
    /// </summary>
    /// <param name="version">The version the reader returns, or <see langword="null" /> for none</param>
    /// <param name="throws">Whether the reader throws</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(null, false, DisplayName = "No version")]
    [DataRow("0.0.0.0", false, DisplayName = "All zero")]
    [DataRow("26.29905.4.51797", true, DisplayName = "Reader throws")]
    public void GameInstallationSearchUnusableVersionIsUnknown(string? version, bool throws)
    {
        // Arrange
        var installDir = CreateInstallation("anno") + "/";

        _versions.Set(ProgramFile(installDir), ParseVersion(version));
        _versions.Exception = ExceptionIf(throws);

        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", installDir)), MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The installation should still be used");
        Assert.AreEqual(installDir, result.Folder, "The installation should still be named");
        Assert.IsNull(result.Build, "The build should be unknown");
        Assert.AreEqual($"Game installation found through Ubisoft Connect, build unknown, folder {installDir}", _logger.Entries[0].Message, "The line should name the build as unknown");
    }

    /// <summary>
    /// Reading the build again gives the status of the found installation with the build of the program file now, keeps the
    /// source and the folder, and becomes the status of the search.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameInstallationSearchRefreshBuildRereadsVersion()
    {
        // Arrange
        var installDir = CreateInstallation("anno") + "/";
        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", installDir)), MissingFolder());
        var found = search.Run(MissingSettings());

        _versions.Set(ProgramFile(installDir), new Version(26, 29906, 0, 1));

        // Act
        var refreshed = search.RefreshBuild();

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, refreshed.Kind, "The installation should still be found");
        Assert.AreEqual("26.29906.0.1", refreshed.Build, "The build should be the one read now");
        Assert.AreEqual(found.Folder, refreshed.Folder, "The folder should be kept");
        Assert.AreEqual(found.Source, refreshed.Source, "The source should be kept");
        Assert.AreEqual(refreshed, search.Status, "The status of the search should hold the build read now");
    }

    /// <summary>
    /// A program file that now has no version gives an unknown build.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameInstallationSearchRefreshBuildWithoutVersionIsUnknown()
    {
        // Arrange
        var installDir = CreateInstallation("anno") + "/";
        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", installDir)), MissingFolder());

        search.Run(MissingSettings());
        _versions.Set(ProgramFile(installDir), null);

        // Act
        var refreshed = search.RefreshBuild();

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, refreshed.Kind, "The installation should still be found");
        Assert.IsNull(refreshed.Build, "The build should be unknown");
    }

    /// <summary>
    /// Without a found installation, before the search ended and after a search that found nothing, the status stays and
    /// nothing is read.
    /// </summary>
    /// <param name="runSearch">Whether the search ran before</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(false)]
    [DataRow(true)]
    public void GameInstallationSearchRefreshBuildNotFoundKeepsStatus(bool runSearch)
    {
        // Arrange
        var search = CreateSearch(new FakeUbisoftInstallRecords(), MissingFolder());

        if (runSearch)
        {
            search.Run(MissingSettings());
        }

        var before = search.Status;

        // Act
        var refreshed = search.RefreshBuild();

        // Assert
        Assert.AreEqual(before, refreshed, "The status should be returned as it was");
        Assert.AreEqual(before, search.Status, "The status should stay");
        Assert.IsEmpty(_versions.Reads, "No version should be read");
    }

    /// <summary>
    /// A search writes nothing: the files and folders it reads keep their listing, lengths, contents and write times.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameInstallationSearchRunWritesNothing()
    {
        // Arrange
        var settings = SettingsFile("""{"theme": "dark"}""");
        var records = new FakeUbisoftInstallRecords(Record("921", CreateFolderWithoutInstallation("half") + "/"));

        CreateInstallation(Path.Combine("games", "anno"));

        var search = CreateSearch(records, Path.Combine(_root, "games"));
        var before = Snapshot();

        // Act
        var result = search.Run(settings);

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.Found, result.Kind, "The search should have gone through every step");
        Assert.AreSequenceEqual(before, Snapshot(), "No file or folder should be created, changed or removed");
    }

    /// <summary>
    /// An unexpected exception still ends the search in a final status.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameInstallationSearchUnexpectedExceptionEndsNotFound()
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords
                      {
                          Exception = new NotSupportedException("Unexpected")
                      };
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.NotFound, result.Kind, "The search should end in not found");
        Assert.AreEqual(GameInstallationStatusKind.NotFound, search.Status.Kind, "The status should never stay searching");
    }

    /// <summary>
    /// An exception that escapes every step, here from a damaged record without a name, ends the search in not found
    /// and is logged with the not-found warning.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameInstallationSearchEscapingExceptionIsLoggedAndEndsNotFound()
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords(Record(null!, CreateInstallation("anno") + "/"));
        var search = CreateSearch(records, MissingFolder());

        // Act
        var result = search.Run(MissingSettings());

        // Assert
        Assert.AreEqual(GameInstallationStatusKind.NotFound, result.Kind, "The search should end in not found");
        Assert.AreEqual(GameInstallationStatusKind.NotFound, search.Status.Kind, "The status should never stay searching");
        Assert.HasCount(1, _logger.Entries, "The search should log exactly one line");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "The escaping exception should be logged as a warning");
        Assert.AreEqual(NotFoundMessage, _logger.Entries[0].Message, "The escaping exception should be logged with the not-found warning");
        Assert.IsInstanceOfType<NullReferenceException>(_logger.Entries[0].Exception, "The warning should carry the escaping exception");
    }

    /// <summary>
    /// Before it runs, the search reports that it is searching.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameInstallationSearchStatusIsSearchingBeforeRun()
    {
        // Arrange
        var records = new FakeUbisoftInstallRecords(Record("921", CreateInstallation("anno") + "/"));

        // Act
        var search = CreateSearch(records, MissingFolder());

        // Assert
        Assert.AreEqual(GameInstallationStatus.Searching, search.Status, "The status should be searching until the search ends");
        Assert.AreEqual(GameInstallationStatusKind.Searching, search.Status.Kind, "The kind should be searching");
        Assert.AreEqual(0, records.ReadCount, "Nothing should be read before the search runs");
    }

    /// <summary>
    /// A control character in the folder is replaced when the line is written to the log.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameInstallationSearchFoundLogLineIsSanitized()
    {
        // Arrange
        var bell = ((char)7).ToString();
        var installDir = CreateInstallation($"Anno{bell}117") + "/";
        var search = CreateSearch(new FakeUbisoftInstallRecords(Record("921", installDir)), MissingFolder());

        // Act
        search.Run(MissingSettings());

        // Assert
        Assert.HasCount(1, _logger.Entries, "The search should log one line");

        var line = LogLine.Format(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), _logger.Entries[0].Level, _logger.Entries[0].Message, _logger.Entries[0].Exception);

        Assert.Contains("Anno?117", line, "The control character should be replaced by a question mark");
        Assert.DoesNotContain(bell, line, "The control character should not reach the log");
    }

    /// <summary>
    /// The log holds no content of the installation's files.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameInstallationSearchLogHoldsNoFileContent()
    {
        // Arrange
        var folder = CreateInstallation("anno");
        var search = CreateSearch(new FakeUbisoftInstallRecords(), MissingFolder());

        // Act
        search.Run(SettingsFileNaming(folder));

        // Assert
        Assert.HasCount(1, _logger.Entries, "The search should log one line");
        Assert.DoesNotContain(GameContent, _logger.Entries[0].Message, "No content of the game files should be logged");
    }

    /// <summary>
    /// Builds an install record.
    /// </summary>
    /// <param name="name">The record name</param>
    /// <param name="installDir">The install folder</param>
    /// <returns>The record</returns>
    private static UbisoftInstallRecord Record(string name, string? installDir)
    {
        return new UbisoftInstallRecord
               {
                   Name = name,
                   InstallDir = installDir
               };
    }

    /// <summary>
    /// Gets the path of the program file below an installation.
    /// </summary>
    /// <param name="folder">The installation folder</param>
    /// <returns>The path</returns>
    private static string ProgramFile(string folder)
    {
        return Path.Combine(folder, "bin", "win64", "anno117.exe");
    }

    /// <summary>
    /// Parses a version text.
    /// </summary>
    /// <param name="version">The version text, or <see langword="null" /> for none</param>
    /// <returns>The version, or <see langword="null" /> for none</returns>
    private static Version? ParseVersion(string? version)
    {
        return version is null ? null : Version.Parse(version);
    }

    /// <summary>
    /// Gets an exception for a reader that throws.
    /// </summary>
    /// <param name="throws">Whether the reader throws</param>
    /// <returns>The exception, or <see langword="null" /></returns>
    private static IOException? ExceptionIf(bool throws)
    {
        return throws ? new IOException("The version cannot be read") : null;
    }

    /// <summary>
    /// Describes a file or folder by path, and for a file by length, content and write time, for a folder by write time.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The description</returns>
    private static string Describe(string path)
    {
        return File.Exists(path)
                   ? $"{path}|{new FileInfo(path).Length}|{Convert.ToHexString(File.ReadAllBytes(path))}|{File.GetLastWriteTimeUtc(path):O}"
                   : $"{path}/|{Directory.GetLastWriteTimeUtc(path):O}";
    }

    /// <summary>
    /// Creates a search over the test's version reader and logger.
    /// </summary>
    /// <param name="records">The install records</param>
    /// <param name="defaultGamesFolder">The default games folder</param>
    /// <returns>The search</returns>
    private GameInstallationSearch CreateSearch(IUbisoftInstallRecords records, string defaultGamesFolder)
    {
        return new GameInstallationSearch(records, _versions, defaultGamesFolder, _logger);
    }

    /// <summary>
    /// Creates a search with its own version reader that finds one recorded installation of the given version.
    /// </summary>
    /// <param name="installDir">The installation folder</param>
    /// <param name="version">The file version of its program file</param>
    /// <returns>The search</returns>
    private GameInstallationSearch CreateSearchWithVersion(string installDir, Version version)
    {
        var versions = new FakeFileVersionReader();

        versions.Set(ProgramFile(installDir), version);

        return new GameInstallationSearch(new FakeUbisoftInstallRecords(Record("921", installDir)), versions, MissingFolder(), _logger);
    }

    /// <summary>
    /// Creates an installation below the temporary folder, its files holding <see cref="GameContent" />, with the
    /// sample's version for its program file.
    /// </summary>
    /// <param name="relative">The folder below the temporary folder</param>
    /// <returns>The full path of the installation, without a trailing separator</returns>
    private string CreateInstallation(string relative)
    {
        var folder = Path.Combine(_root, relative);
        var config = Path.Combine(folder, "maindata", "config.rda");
        var program = ProgramFile(folder);

        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        Directory.CreateDirectory(Path.GetDirectoryName(program)!);
        File.WriteAllText(config, GameContent);
        File.WriteAllText(program, GameContent);

        _versions.Set(program, _sampleVersion);

        return folder;
    }

    /// <summary>
    /// Creates installations below the temporary folder.
    /// </summary>
    /// <param name="names">The folder names, comma-separated</param>
    private void CreateInstallations(string names)
    {
        foreach (var name in names.Split(','))
        {
            CreateInstallation(name);
        }
    }

    /// <summary>
    /// Creates a folder below the temporary folder that holds only the configuration archive.
    /// </summary>
    /// <param name="relative">The folder below the temporary folder</param>
    /// <returns>The full path of the folder</returns>
    private string CreateFolderWithoutInstallation(string relative)
    {
        var folder = Path.Combine(_root, relative);

        Directory.CreateDirectory(Path.Combine(folder, "maindata"));
        File.WriteAllText(Path.Combine(folder, "maindata", "config.rda"), GameContent);

        return folder;
    }

    /// <summary>
    /// Gets a folder below the temporary folder that does not exist.
    /// </summary>
    /// <returns>The path</returns>
    private string MissingFolder()
    {
        return Path.Combine(_root, "no-such-folder");
    }

    /// <summary>
    /// Gets a settings file path that does not exist.
    /// </summary>
    /// <returns>The path</returns>
    private string MissingSettings()
    {
        return Path.Combine(_root, "no-settings", "settings.json");
    }

    /// <summary>
    /// Writes a settings file, or gets a missing one.
    /// </summary>
    /// <param name="text">The settings text, or <see langword="null" /> for no file</param>
    /// <returns>The path of the settings file</returns>
    private string SettingsFile(string? text)
    {
        if (text is null)
        {
            return MissingSettings();
        }

        var path = Path.Combine(_root, "settings", "settings.json");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));

        return path;
    }

    /// <summary>
    /// Writes a settings file whose <c>gameFolder</c> is the given text.
    /// </summary>
    /// <param name="gameFolder">The game folder as written</param>
    /// <returns>The path of the settings file</returns>
    private string SettingsFileNaming(string gameFolder)
    {
        return SettingsFile($$"""{"gameFolder": {{JsonSerializer.Serialize(gameFolder)}}}""");
    }

    /// <summary>
    /// Replaces <see cref="RootMarker" /> by the temporary folder and <see cref="RelativeRootMarker" /> by the
    /// temporary folder relative to the current directory.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The resolved text</returns>
    private string Resolve(string text)
    {
        return text.Replace(RelativeRootMarker, Path.GetRelativePath(Environment.CurrentDirectory, _root), StringComparison.Ordinal)
                   .Replace(RootMarker, _root, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves a text that may be missing.
    /// </summary>
    /// <param name="text">The text, or <see langword="null" /></param>
    /// <returns>The resolved text, or <see langword="null" /></returns>
    private string? ResolveOrNull(string? text)
    {
        return text is null ? null : Resolve(text);
    }

    /// <summary>
    /// Describes every file and folder below the temporary folder, in ordinal order.
    /// </summary>
    /// <returns>The descriptions</returns>
    private string[] Snapshot()
    {
        return Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories)
                        .Order(StringComparer.Ordinal)
                        .Select(Describe)
                        .ToArray();
    }

    #endregion // Methods
}