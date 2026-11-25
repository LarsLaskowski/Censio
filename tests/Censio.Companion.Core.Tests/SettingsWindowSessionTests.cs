using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="SettingsWindowSession" />.
/// </summary>
[TestClass]
public sealed class SettingsWindowSessionTests
{
    #region Constants

    /// <summary>
    /// A valid server address.
    /// </summary>
    private const string ServerAddress = "ws://nas:8080/ingest";

    /// <summary>
    /// A valid token of 37 visible ASCII characters, a marker no log line may hold.
    /// </summary>
    private const string ServerToken = "MARKER-Token-0123456789abcdefghijKLMN";

    /// <summary>
    /// The name of a new profile, a marker no log line may hold.
    /// </summary>
    private const string ProfileName = "MARKER-Profile";

    /// <summary>
    /// A settings file in local mode on port 6000.
    /// </summary>
    private const string LocalSettings = """{"mode":"local","localPort":6000}""";

    /// <summary>
    /// The settings file written outside the window after it opened.
    /// </summary>
    private const string OutsideSettings = """{"mode":"local","localPort":7000}""";

    /// <summary>
    /// The launch of a restart in <see cref="_events" />.
    /// </summary>
    private const string LaunchEvent = "launch";

    /// <summary>
    /// The exit for a restart in <see cref="_events" />.
    /// </summary>
    private const string ExitEvent = "exit";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-settings-window-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The actions posted to the window's thread and not yet run.
    /// </summary>
    private readonly Channel<Action> _posted = Channel.CreateUnbounded<Action>();

    /// <summary>
    /// The windows the session created, in order.
    /// </summary>
    private readonly ConcurrentQueue<FakeSettingsWindow> _windows = [];

    /// <summary>
    /// The launches and exits of a restart, in order.
    /// </summary>
    private readonly ConcurrentQueue<string> _events = [];

    /// <summary>
    /// The paths and arguments of the launches, in order.
    /// </summary>
    private readonly ConcurrentQueue<(string Path, string Argument)> _launches = [];

    /// <summary>
    /// The languages applied, in order.
    /// </summary>
    private readonly ConcurrentQueue<CompanionLanguage> _appliedLanguages = [];

    /// <summary>
    /// The store of the autostart entry.
    /// </summary>
    private readonly FakeStartupEntryStore _store = new();

    /// <summary>
    /// The logger of the session and of everything it uses.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    /// <summary>
    /// The completion that lets a held folder check return; completed while checks are not held.
    /// </summary>
    private volatile TaskCompletionSource _folderGate = CreateCompleted();

    /// <summary>
    /// Completed with the first folder check that started after <see cref="HoldFolderChecks" />.
    /// </summary>
    private volatile TaskCompletionSource _folderCheckStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The number of folder checks waiting for <see cref="_folderGate" />.
    /// </summary>
    private int _waitingFolderChecks;

    /// <summary>
    /// The texts the session reads.
    /// </summary>
    private CompanionTexts _texts = CompanionTexts.English;

    /// <summary>
    /// The run state the session reads.
    /// </summary>
    private SettingsRunState _runState;

    /// <summary>
    /// The exception the launch of a restart throws, or <see langword="null" /> to launch.
    /// </summary>
    private Exception? _launchException;

    /// <summary>
    /// The profiles of the session.
    /// </summary>
    private CompanionProfiles _profiles = null!;

    /// <summary>
    /// The start with Windows of the session.
    /// </summary>
    private CompanionAutostart _autostart = null!;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindowSessionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public SettingsWindowSessionTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the path of the settings file in the temporary folder.
    /// </summary>
    private string SettingsPath => Path.Combine(_root, "settings.json");

    /// <summary>
    /// Gets the path of the temporary file of a save.
    /// </summary>
    private string TmpPath => SettingsPath + ".tmp";

    /// <summary>
    /// Gets the path of the profiles file in the temporary folder.
    /// </summary>
    private string ProfilesPath => Path.Combine(_root, "profiles.json");

    /// <summary>
    /// Gets the full path of the Companion's executable.
    /// </summary>
    private string ExePath => Path.Combine(_root, "Censio.Companion.exe");

    /// <summary>
    /// Gets a game folder the checks of the test accept.
    /// </summary>
    private string GameFolder => Path.Combine(_root, "Anno 117");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the temporary folder, the profiles and the start with Windows.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        Directory.CreateDirectory(_root);

        _profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        _autostart = new CompanionAutostart(_store, ExePath, _logger);
    }

    /// <summary>
    /// Lets held folder checks return and deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        _folderGate.TrySetResult();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// <c>Settings…</c> opens one window with the file's values, the defaults for the missing keys, the profiles and the
    /// autostart state, in the current language and without warnings (S1).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task SettingsWindowSessionOpenShowsFileValuesAndDefaults()
    {
        // Arrange
        WriteSettings(LocalSettings);
        _store.SetCommand(CompanionAutostart.EntryName, CompanionAutostart.FormatCommand(ExePath));

        var session = CreateSession();

        // Act
        var window = await OpenAsync(session);

        // Assert
        var view = window.Views.Single();

        Assert.IsTrue(session.IsOpen, "The session should have its window open");
        Assert.HasCount(1, _windows, "One window should be created");
        Assert.AreSame(CompanionTexts.English, view.Texts, "The view should carry the current texts");
        Assert.AreEqual(CompanionSettingsMode.Local, view.Values.Mode, "The mode should be the file's");
        Assert.AreEqual("6000", view.Values.LocalPort, "The port should be the file's");
        Assert.AreEqual(string.Empty, view.Values.ServerAddress, "A missing address should be empty");
        Assert.AreEqual(string.Empty, view.Values.ServerToken, "A missing token should be empty");
        Assert.AreEqual("24", view.Values.BufferHours, "Missing hours should be the default 24");
        Assert.AreEqual("100", view.Values.BufferMegabytes, "Missing megabytes should be the default 100");
        Assert.AreEqual(string.Empty, view.Values.GameFolder, "A missing game folder should be empty, found automatically");
        Assert.IsNull(view.FileProblem, "A readable file should have no file problem");
        Assert.IsEmpty(view.Problems, "Valid keys should have no problems");
        Assert.AreSequenceEqual([CompanionProfile.Live.Id], view.ProfileList.Select(profile => profile.Id).ToArray(), "The profiles should be Live alone");
        Assert.AreEqual(CompanionProfile.Live.Id, view.ActiveProfile, "Live should be the active profile");
        Assert.IsFalse(view.IsProfilesFileInvalid, "A missing profiles file should be valid");
        Assert.IsTrue(view.IsAutostartEnabled, "The view should show the autostart entry of the store");
        Assert.IsEmpty(view.Warnings, "Nothing should be warned about while nothing runs");
    }

    /// <summary>
    /// An unparseable settings file and an unusable profiles file are shown with the defaults, the reason of the file and the
    /// profiles warning (E7, E16).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task SettingsWindowSessionOpenShowsFileAndProfilesProblems()
    {
        // Arrange
        WriteSettings("not json");
        await File.WriteAllTextAsync(ProfilesPath, "not json", TestContext.CancellationToken);

        _profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        var session = CreateSession();

        // Act
        var window = await OpenAsync(session);

        // Assert
        var view = window.Views.Single();

        Assert.AreEqual("not valid JSON", view.FileProblem, "The view should carry the reason the log gives");
        Assert.AreEqual(CompanionSettingsMode.Off, view.Values.Mode, "An unparseable file should show the default mode");
        Assert.AreEqual("5000", view.Values.LocalPort, "An unparseable file should show the default port");
        Assert.IsTrue(view.IsProfilesFileInvalid, "An unusable profiles file should be shown");
        Assert.AreSequenceEqual([CompanionProfile.Live.Id], view.ProfileList.Select(profile => profile.Id).ToArray(), "An unusable profiles file should give Live alone");
    }

    /// <summary>
    /// A second <c>Settings…</c> while the window is open brings it to the front and opens no other (S1).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task SettingsWindowSessionSecondOpenBringsWindowToFront()
    {
        // Arrange
        var session = CreateSession();
        var window = await OpenAsync(session);

        // Act
        session.Open();
        await RunPostedUntilAsync(() => window.BringToFrontCount > 0);

        // Assert
        Assert.HasCount(1, _windows, "No second window should be created");
        Assert.AreEqual(1, window.BringToFrontCount, "The open window should be brought to the front once");
    }

    /// <summary>
    /// Closing the window, as <c>Cancel</c> does, writes and changes nothing and ends the session's window (E13).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task SettingsWindowSessionCloseWritesNothing()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var old = await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken);
        var session = CreateSession();
        var window = await OpenAsync(session);

        // Act
        window.Close();
        RunPosted();

        // Assert
        Assert.IsFalse(session.IsOpen, "The session should have no window after it closed");
        Assert.AreSequenceEqual(old, await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken), "Closing should leave the file as it was");
        Assert.IsFalse(File.Exists(TmpPath), "Closing should write no temporary file");
        Assert.IsFalse(File.Exists(ProfilesPath), "Closing should write no profiles file");
        Assert.IsEmpty(window.Outcomes, "Closing should show no outcome");
        Assert.IsEmpty(_store.Writes, "Closing should not change the autostart");
        Assert.IsEmpty(_store.Removals, "Closing should not change the autostart");
        Assert.IsEmpty(_events, "Closing should not restart");
    }

    /// <summary>
    /// Opening checks the file's game folder off the caller: <c>Open</c> returns while the check is held, and the view is shown
    /// only once the check is done and its result was posted back (N3).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task SettingsWindowSessionOpenChecksFolderOffTheCaller()
    {
        // Arrange
        WriteSettings(new JsonObject
                      {
                          ["gameFolder"] = GameFolder
                      }.ToJsonString());

        var session = CreateSession();

        HoldFolderChecks();

        // Act
        session.Open();

        // Assert
        await _folderCheckStarted.Task.WaitAsync(_guard, TestContext.CancellationToken);

        Assert.AreEqual(1, Volatile.Read(ref _waitingFolderChecks), "The folder check should still be held after Open returned");
        Assert.AreEqual(0, CountViews(), "No view should be shown before the folder check is done");

        _folderGate.TrySetResult();
        await RunPostedUntilAsync(() => CountViews() > 0);

        Assert.AreEqual(GameFolder, _windows.Single().Views.Single().Values.GameFolder, "The view should show the file's game folder once checked");
    }

    /// <summary>
    /// <c>Save</c> checks the game folder off the caller: the request returns while the check is held, and the outcome is shown
    /// only once the check is done and its result was posted back (N3, Security N1).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task SettingsWindowSessionSaveChecksFolderOffTheCaller()
    {
        // Arrange
        WriteSettings(new JsonObject
                      {
                          ["gameFolder"] = GameFolder
                      }.ToJsonString());

        var session = CreateSession();
        var window = await OpenAsync(session);
        var input = CreateInput(window.Views.Single(), window.Views.Single().Values);

        HoldFolderChecks();

        // Act
        window.RequestSave(input);

        // Assert
        await _folderCheckStarted.Task.WaitAsync(_guard, TestContext.CancellationToken);

        Assert.AreEqual(1, Volatile.Read(ref _waitingFolderChecks), "The folder check should still be held after the request returned");
        Assert.IsEmpty(window.Outcomes, "No outcome should be shown before the folder check is done");

        _folderGate.TrySetResult();
        await RunPostedUntilAsync(() => window.Outcomes.Count > 0);

        Assert.AreEqual(SettingsSaveKind.Saved, window.Outcomes.Single().Kind, "The save should succeed once the folder is checked");
    }

    /// <summary>
    /// A second <c>Save</c> while one runs is ignored; a later one is handled again.
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task SettingsWindowSessionSecondSaveIgnoredWhileOneRuns()
    {
        // Arrange
        WriteSettings(new JsonObject
                      {
                          ["gameFolder"] = GameFolder
                      }.ToJsonString());

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var first = CreateInput(view, view.Values);
        var second = CreateInput(view, Change(view.Values, (SettingsField.LocalPort, "0")), SettingsField.LocalPort);
        var third = CreateInput(view, Change(view.Values, (SettingsField.BufferHours, "0")), SettingsField.BufferHours);

        HoldFolderChecks();

        // Act
        window.RequestSave(first);
        await _folderCheckStarted.Task.WaitAsync(_guard, TestContext.CancellationToken);
        window.RequestSave(second);
        _folderGate.TrySetResult();
        await RunPostedUntilAsync(() => window.Outcomes.Count > 0);
        window.RequestSave(third);
        await RunPostedUntilAsync(() => window.Outcomes.Any(outcome => outcome.Problems.Any(problem => problem.Field == SettingsField.BufferHours)));

        // Assert
        var outcomes = window.Outcomes;

        Assert.AreSequenceEqual([SettingsSaveKind.Saved, SettingsSaveKind.Refused], outcomes.Select(outcome => outcome.Kind).ToArray(), "The second request should be ignored, the third handled");
        Assert.AreSequenceEqual([CreateProblem(SettingsField.BufferHours, SettingsProblemKind.HoursRange)], outcomes[1].Problems.ToArray(), "The refused outcome should be the third request's");
    }

    /// <summary>
    /// A save in which the player did not touch the language writes no <c>language</c> key, even when the window fills the
    /// field with the language it shows (Challenge 4).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task SettingsWindowSessionSaveUntouchedLanguageWritesNone()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, Change(view.Values, CompanionLanguage.English, (SettingsField.BufferHours, "48")), SettingsField.BufferHours);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Restarting, outcome.Kind, "The changed buffer limit should be saved");
        Assert.IsFalse(ReadSavedProperty(CompanionSettings.LanguageKey).HasValue, "An untouched language should leave the key absent");
        Assert.AreEqual(48, ReadSavedProperty(CompanionSettings.BufferHoursKey)?.GetInt32(), "The edited field should be saved");
    }

    /// <summary>
    /// An invalid or duplicated key of the file blocks <c>Save</c> until its field is edited, also a key of another mode than
    /// the selected one (E8, Challenge 5).
    /// </summary>
    /// <param name="file">The settings file</param>
    /// <param name="field">The field of the invalid key</param>
    /// <param name="text">The text saved in that field</param>
    /// <param name="edited">The field the player edited</param>
    /// <param name="expected">The expected outcome</param>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("""{"mode":"local","localPort":"abc"}""", SettingsField.LocalPort, "", SettingsField.BufferHours, SettingsSaveKind.Refused, DisplayName = "Port of the selected mode, not edited")]
    [DataRow("""{"mode":"local","localPort":"abc"}""", SettingsField.LocalPort, "5000", SettingsField.LocalPort, SettingsSaveKind.Saved, DisplayName = "Port of the selected mode, edited")]
    [DataRow("""{"mode":"local","localPort":1,"localPort":2}""", SettingsField.LocalPort, "", SettingsField.BufferHours, SettingsSaveKind.Refused, DisplayName = "Port written twice, not edited")]
    [DataRow("""{"mode":"local","serverToken":"short"}""", SettingsField.ServerToken, "", SettingsField.BufferHours, SettingsSaveKind.Refused, DisplayName = "Token in local mode, not edited")]
    [DataRow("""{"mode":"local","serverToken":"short"}""", SettingsField.ServerToken, "", SettingsField.ServerToken, SettingsSaveKind.Saved, DisplayName = "Token in local mode, edited")]
    public async Task SettingsWindowSessionSaveBlockedUntilFileProblemFieldEdited(string file, SettingsField field, string text, SettingsField edited, SettingsSaveKind expected)
    {
        // Arrange
        WriteSettings(file);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, Change(view.Values, (field, text)), edited);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(expected, outcome.Kind, "A file problem should block the save until its field is edited");
    }

    /// <summary>
    /// A value with a message writes nothing, restarts nothing and changes no profile or autostart; the outcome names the
    /// problem (E1).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task SettingsWindowSessionSaveWritesNothingWhileProblemShown()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var old = await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken);
        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = Change(view.Values, (SettingsField.LocalPort, "0")),
                        SelectedProfile = CompanionProfile.Live.Id,
                        NewProfileName = ProfileName,
                        IsAutostartEnabled = true,
                        EditedFields = [SettingsField.LocalPort, SettingsField.ProfileName]
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Refused, outcome.Kind, "An invalid port should refuse the save");
        Assert.AreSequenceEqual([CreateProblem(SettingsField.LocalPort, SettingsProblemKind.PortRange)], outcome.Problems.ToArray(), "The outcome should name the port's problem");
        Assert.AreSequenceEqual(old, await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken), "A refused save should leave the file as it was");
        Assert.HasCount(1, _profiles.Profiles, "A refused save should create no profile");
        Assert.IsEmpty(_store.Writes, "A refused save should not change the autostart");
        Assert.IsEmpty(_events, "A refused save should not restart");
    }

    /// <summary>
    /// A new profile name another profile has, ignoring case, refuses the save with E6's problem and writes nothing.
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task SettingsWindowSessionSaveRefusesExistingProfileName()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var old = await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken);
        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = view.Values,
                        SelectedProfile = CompanionProfile.Live.Id,
                        NewProfileName = "live",
                        IsAutostartEnabled = view.IsAutostartEnabled,
                        EditedFields = [SettingsField.ProfileName]
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Refused, outcome.Kind, "An existing name should refuse the save");
        Assert.AreSequenceEqual([CreateProblem(SettingsField.ProfileName, SettingsProblemKind.NameExists)], outcome.Problems.ToArray(), "The outcome should name the profile name's problem");
        Assert.AreSequenceEqual(old, await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken), "A refused save should leave the file as it was");
        Assert.HasCount(1, _profiles.Profiles, "A refused save should create no profile");
    }

    /// <summary>
    /// A saved change of a connection setting starts the Companion's own exe with <c>--restarted</c>, then exits it (S3).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SettingsWindowSessionSaveConnectionChangeRestarts()
    {
        // Arrange
        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, Change(view.Values, CompanionSettingsMode.Forward, (SettingsField.ServerAddress, ServerAddress)), SettingsField.Mode, SettingsField.ServerAddress);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Restarting, outcome.Kind, "A changed mode should restart");
        Assert.AreSequenceEqual([(ExePath, CompanionCommandLine.RestartedSwitch)], _launches.ToArray(), "The own exe should be started once with the restart switch");
        Assert.AreSequenceEqual([LaunchEvent, ExitEvent], _events.ToArray(), "The Companion should exit after the new one was started");
        Assert.AreEqual(CompanionSettingsMode.Forward, CompanionSettingsDocument.Load(SettingsPath).Values.Mode, "The file should hold the saved mode");
    }

    /// <summary>
    /// A save that changes no connection or game setting restarts nothing (S3).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SettingsWindowSessionSaveWithoutConnectionChangeDoesNotRestart()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, view.Values);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Saved, outcome.Kind, "An unchanged connection should be saved without a restart");
        Assert.IsEmpty(_events, "Nothing should be launched or exited");
        Assert.AreEqual("6000", CompanionSettingsDocument.Load(SettingsPath).Values.LocalPort, "The file should hold the saved values");
    }

    /// <summary>
    /// The restart decision compares with the values the Companion started with, not with the file the window read: a file
    /// changed by hand after the start and saved unchanged restarts (Challenge 3).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SettingsWindowSessionSaveComparesWithStartupValues()
    {
        // Arrange
        WriteSettings("""{"mode":"local","localPort":5000}""");

        var startup = CompanionSettingsDocument.Load(SettingsPath).Values;

        WriteSettings(LocalSettings);

        var session = CreateSession(startup);
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, view.Values);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Restarting, outcome.Kind, "A port that differs from the one the Companion started with should restart");
        Assert.AreSequenceEqual([LaunchEvent, ExitEvent], _events.ToArray(), "The Companion should restart");
    }

    /// <summary>
    /// The window warns about a running replay, a running recording and a memory-only buffer, one line each in that order
    /// (E12).
    /// </summary>
    /// <param name="isReplay">Whether a replay runs</param>
    /// <param name="isRecording">Whether a recording runs</param>
    /// <param name="isBufferMemoryOnly">Whether the buffer is kept in memory only</param>
    /// <param name="expected">The warnings, joined by <c>|</c></param>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(false, false, false, "", DisplayName = "Nothing runs")]
    [DataRow(true, false, false, "Saving ends the running replay.", DisplayName = "Replay")]
    [DataRow(false, true, false, "Saving ends the running recording.", DisplayName = "Recording")]
    [DataRow(false, false, true, "Messages that could not be saved to disk are lost.", DisplayName = "Memory-only buffer")]
    [DataRow(true, true, true, "Saving ends the running replay.|Saving ends the running recording.|Messages that could not be saved to disk are lost.", DisplayName = "All")]
    public async Task SettingsWindowSessionWarnsAboutReplayRecordingAndMemoryBuffer(bool isReplay, bool isRecording, bool isBufferMemoryOnly, string expected)
    {
        // Arrange
        _runState = new SettingsRunState
                    {
                        IsReplay = isReplay,
                        IsRecording = isRecording,
                        IsBufferMemoryOnly = isBufferMemoryOnly
                    };

        var session = CreateSession();

        // Act
        var window = await OpenAsync(session);

        // Assert
        Assert.AreEqual(expected, string.Join('|', window.Views.Single().Warnings), "The warnings should follow the run state");
    }

    /// <summary>
    /// An exe that cannot be started leaves the Companion running with E15's outcome; the file holds the saved values.
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SettingsWindowSessionRestartNotStartedKeepsRunningWithMessage()
    {
        // Arrange
        _launchException = new IOException("The exe is gone");

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, Change(view.Values, CompanionSettingsMode.Forward, (SettingsField.ServerAddress, ServerAddress)), SettingsField.Mode, SettingsField.ServerAddress);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.RestartFailed, outcome.Kind, "A failed launch should be reported");
        Assert.AreSequenceEqual([LaunchEvent], _events.ToArray(), "The Companion should not exit after a failed launch");
        Assert.AreEqual(CompanionSettingsMode.Forward, CompanionSettingsDocument.Load(SettingsPath).Values.Mode, "The file should hold the saved values");
        Assert.Contains(entry => entry.Level == LogLevel.Warning && entry.Message == "Restart failed: the Companion could not be started: IOException", _logger.Entries, "The failed launch should be logged with the exception type");
    }

    /// <summary>
    /// A changed language is saved and applied at once, without a restart (S9).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task SettingsWindowSessionSaveLanguageAppliesAtOnceWithoutRestart()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, Change(view.Values, CompanionLanguage.German), SettingsField.Language);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Saved, outcome.Kind, "A language change should be saved without a restart");
        Assert.AreSequenceEqual([CompanionLanguage.German], _appliedLanguages.ToArray(), "The language should be applied once");
        Assert.IsEmpty(_events, "A language change should not restart");
        Assert.AreEqual(CompanionLanguage.German, CompanionSettings.ReadLanguage(SettingsPath), "The file should hold the language");
    }

    /// <summary>
    /// After the language changed, the open window is shown again with the texts of the current language (S9).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task SettingsWindowSessionRefreshTextsShowsViewInCurrentLanguage()
    {
        // Arrange
        var session = CreateSession();
        var window = await OpenAsync(session);

        _texts = CompanionTexts.German;

        // Act
        session.RefreshTexts();
        await RunPostedUntilAsync(() => window.Views.Count > 1);

        // Assert
        Assert.HasCount(2, window.Views, "The window should be shown once more");
        Assert.AreSame(CompanionTexts.German, window.Views[1].Texts, "The view should carry the texts of the current language");
    }

    /// <summary>
    /// The autostart is toggled only when the box differs from the entry read before the save (S4).
    /// </summary>
    /// <param name="isEntryEnabled">Whether the entry is the Companion's own before the save</param>
    /// <param name="isBoxTicked">Whether the box is ticked</param>
    /// <param name="writes">The expected number of writes of the entry</param>
    /// <param name="removals">The expected number of removals of the entry</param>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(false, false, 0, 0, DisplayName = "Off, box cleared")]
    [DataRow(true, true, 0, 0, DisplayName = "On, box ticked")]
    [DataRow(false, true, 1, 0, DisplayName = "Off, box ticked")]
    [DataRow(true, false, 0, 1, DisplayName = "On, box cleared")]
    public async Task SettingsWindowSessionAutostartToggledOnlyWhenBoxDiffers(bool isEntryEnabled, bool isBoxTicked, int writes, int removals)
    {
        // Arrange
        WriteSettings(LocalSettings);
        SetAutostartEntry(isEntryEnabled);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = view.Values,
                        SelectedProfile = view.ActiveProfile,
                        IsAutostartEnabled = isBoxTicked,
                        EditedFields = []
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Saved, outcome.Kind, "The save should succeed");
        Assert.IsFalse(outcome.IsAutostartFailed, "A working store should report no autostart failure");
        Assert.HasCount(writes, _store.Writes, "The entry should be written only when the box was ticked over an absent entry");
        Assert.HasCount(removals, _store.Removals, "The entry should be removed only when the box was cleared over the own entry");
        Assert.AreEqual(isBoxTicked, _store.GetCommand(CompanionAutostart.EntryName) is not null, "The entry should follow the box");
    }

    /// <summary>
    /// An autostart that cannot be written is reported while the other values are saved (E14).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task SettingsWindowSessionAutostartFailureReported()
    {
        // Arrange
        WriteSettings(LocalSettings);
        _store.WriteException = new UnauthorizedAccessException("The registry refused the write");

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = Change(view.Values, CompanionLanguage.German),
                        SelectedProfile = view.ActiveProfile,
                        IsAutostartEnabled = true,
                        EditedFields = [SettingsField.Language]
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Saved, outcome.Kind, "The other values should be saved");
        Assert.IsTrue(outcome.IsAutostartFailed, "The failed autostart write should be reported");
        Assert.HasCount(1, _store.Writes, "The entry should have been tried once");
        Assert.AreEqual(CompanionLanguage.German, CompanionSettings.ReadLanguage(SettingsPath), "The file should hold the other values");
    }

    /// <summary>
    /// A new profile name creates the profile and selects it (S5).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SettingsWindowSessionSaveCreatesProfileAndSelectsIt()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = view.Values,
                        SelectedProfile = CompanionProfile.Live.Id,
                        NewProfileName = ProfileName,
                        IsAutostartEnabled = view.IsAutostartEnabled,
                        EditedFields = [SettingsField.ProfileName]
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Saved, outcome.Kind, "Creating a profile should not restart");
        Assert.AreSequenceEqual(["Live", ProfileName], _profiles.Profiles.Select(profile => profile.Name).ToArray(), "The new profile should follow Live");
        Assert.AreEqual(ProfileName, _profiles.Active.Name, "The new profile should be active");
    }

    /// <summary>
    /// Selecting another profile and saving makes it active (S6).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SettingsWindowSessionSaveSelectsProfile()
    {
        // Arrange
        WriteSettings(LocalSettings);
        _ = _profiles.Create(ProfileName);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = view.Values,
                        SelectedProfile = CompanionProfile.Live.Id,
                        IsAutostartEnabled = view.IsAutostartEnabled,
                        EditedFields = []
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.Saved, outcome.Kind, "Selecting a profile should not restart");
        Assert.AreEqual(CompanionProfile.Live.Id, _profiles.Active.Id, "The selected profile should be active");
        Assert.HasCount(2, _profiles.Profiles, "Selecting should create no profile");
    }

    /// <summary>
    /// A file that cannot be written is reported with E9's outcome and logged with the exception type; nothing restarts and no
    /// profile is created.
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task SettingsWindowSessionSaveWriteFailureChangesNothingElse()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var old = await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken);
        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = Change(view.Values, CompanionSettingsMode.Forward, (SettingsField.ServerAddress, ServerAddress)),
                        SelectedProfile = CompanionProfile.Live.Id,
                        NewProfileName = ProfileName,
                        IsAutostartEnabled = true,
                        EditedFields = [SettingsField.Mode, SettingsField.ServerAddress, SettingsField.ProfileName]
                    };

        Directory.CreateDirectory(TmpPath);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.WriteFailed, outcome.Kind, "A failed write should be reported");
        Assert.AreSequenceEqual(old, await File.ReadAllBytesAsync(SettingsPath, TestContext.CancellationToken), "The old file should stay whole");
        Assert.IsEmpty(_events, "A failed write should not restart");
        Assert.HasCount(1, _profiles.Profiles, "A failed write should create no profile");
        Assert.IsEmpty(_store.Writes, "A failed write should not change the autostart");
        Assert.Contains(entry => entry.Level == LogLevel.Warning && entry.Message == "Could not save the settings: UnauthorizedAccessException", _logger.Entries, "The failure should be logged with the exception type");
    }

    /// <summary>
    /// A file changed after the window opened is reported with E10's outcome and not written.
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task SettingsWindowSessionSaveChangedOutsideWritesNothing()
    {
        // Arrange
        WriteSettings(LocalSettings);

        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = CreateInput(view, Change(view.Values, CompanionSettingsMode.Forward, (SettingsField.ServerAddress, ServerAddress)), SettingsField.Mode, SettingsField.ServerAddress);

        WriteSettings(OutsideSettings);

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        Assert.AreEqual(SettingsSaveKind.ChangedOutside, outcome.Kind, "An outside change should be reported");
        Assert.AreEqual(OutsideSettings, await File.ReadAllTextAsync(SettingsPath, TestContext.CancellationToken), "The outside change should stay");
        Assert.IsEmpty(_events, "An outside change should not restart");
    }

    /// <summary>
    /// A save with a token and a new profile logs <c>Settings saved</c> and neither the token nor the profile name (N2, N5).
    /// </summary>
    /// <returns>A task that completes with the test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task SettingsWindowSessionSaveLogsNoTokenOrName()
    {
        // Arrange
        var session = CreateSession();
        var window = await OpenAsync(session);
        var view = window.Views.Single();
        var input = new SettingsInput
                    {
                        Values = Change(view.Values, CompanionSettingsMode.Forward, (SettingsField.ServerAddress, ServerAddress), (SettingsField.ServerToken, ServerToken)),
                        SelectedProfile = CompanionProfile.Live.Id,
                        NewProfileName = ProfileName,
                        IsAutostartEnabled = view.IsAutostartEnabled,
                        EditedFields = [SettingsField.Mode, SettingsField.ServerAddress, SettingsField.ServerToken, SettingsField.ProfileName]
                    };

        // Act
        var outcome = await SaveAsync(window, input);

        // Assert
        var messages = _logger.Entries.Select(entry => entry.Message).ToArray();

        Assert.AreEqual(SettingsSaveKind.Restarting, outcome.Kind, "The save should succeed and restart");
        Assert.Contains(entry => entry.Level == LogLevel.Information && entry.Message == "Settings saved", _logger.Entries, "The save should be logged");
        Assert.DoesNotContain(message => message.Contains(ServerToken, StringComparison.Ordinal), messages, "No log line should hold the token");
        Assert.DoesNotContain(message => message.Contains(ProfileName, StringComparison.Ordinal), messages, "No log line should hold the profile name");
    }

    /// <summary>
    /// Creates a completed completion.
    /// </summary>
    /// <returns>The completion</returns>
    private static TaskCompletionSource CreateCompleted()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        completion.SetResult();

        return completion;
    }

    /// <summary>
    /// Creates a problem.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="kind">The kind</param>
    /// <returns>The problem</returns>
    private static SettingsProblem CreateProblem(SettingsField field, SettingsProblemKind kind)
    {
        return new SettingsProblem
               {
                   Field = field,
                   Kind = kind
               };
    }

    /// <summary>
    /// Creates a copy of values with some field texts replaced.
    /// </summary>
    /// <param name="source">The values</param>
    /// <param name="texts">The replaced field texts</param>
    /// <returns>The copy</returns>
    private static CompanionSettingsValues Change(CompanionSettingsValues source, params (SettingsField Field, string Text)[] texts)
    {
        return Change(source, source.Mode, source.Language, texts);
    }

    /// <summary>
    /// Creates a copy of values with another mode and some field texts replaced.
    /// </summary>
    /// <param name="source">The values</param>
    /// <param name="mode">The mode</param>
    /// <param name="texts">The replaced field texts</param>
    /// <returns>The copy</returns>
    private static CompanionSettingsValues Change(CompanionSettingsValues source, CompanionSettingsMode mode, params (SettingsField Field, string Text)[] texts)
    {
        return Change(source, mode, source.Language, texts);
    }

    /// <summary>
    /// Creates a copy of values with another language and some field texts replaced.
    /// </summary>
    /// <param name="source">The values</param>
    /// <param name="language">The language</param>
    /// <param name="texts">The replaced field texts</param>
    /// <returns>The copy</returns>
    private static CompanionSettingsValues Change(CompanionSettingsValues source, CompanionLanguage language, params (SettingsField Field, string Text)[] texts)
    {
        return Change(source, source.Mode, language, texts);
    }

    /// <summary>
    /// Creates a copy of values with another mode and language and some field texts replaced.
    /// </summary>
    /// <param name="source">The values</param>
    /// <param name="mode">The mode</param>
    /// <param name="language">The language</param>
    /// <param name="texts">The replaced field texts</param>
    /// <returns>The copy</returns>
    private static CompanionSettingsValues Change(CompanionSettingsValues source, CompanionSettingsMode mode, CompanionLanguage? language, (SettingsField Field, string Text)[] texts)
    {
        var map = texts.ToDictionary(text => text.Field, text => text.Text);

        return new CompanionSettingsValues
               {
                   Mode = mode,
                   LocalPort = map.GetValueOrDefault(SettingsField.LocalPort, source.LocalPort),
                   ServerAddress = map.GetValueOrDefault(SettingsField.ServerAddress, source.ServerAddress),
                   ServerToken = map.GetValueOrDefault(SettingsField.ServerToken, source.ServerToken),
                   BufferHours = map.GetValueOrDefault(SettingsField.BufferHours, source.BufferHours),
                   BufferMegabytes = map.GetValueOrDefault(SettingsField.BufferMegabytes, source.BufferMegabytes),
                   GameFolder = map.GetValueOrDefault(SettingsField.GameFolder, source.GameFolder),
                   Language = language
               };
    }

    /// <summary>
    /// Creates the input of a save of values with the view's profile and autostart state.
    /// </summary>
    /// <param name="view">The view shown</param>
    /// <param name="values">The values</param>
    /// <param name="edited">The fields the player edited</param>
    /// <returns>The input</returns>
    private static SettingsInput CreateInput(SettingsView view, CompanionSettingsValues values, params SettingsField[] edited)
    {
        return new SettingsInput
               {
                   Values = values,
                   SelectedProfile = view.ActiveProfile,
                   IsAutostartEnabled = view.IsAutostartEnabled,
                   EditedFields = edited
               };
    }

    /// <summary>
    /// Creates the session under test, whose windows are kept in <see cref="_windows" />.
    /// </summary>
    /// <param name="startupValues">The values the Companion started with, or <see langword="null" /> for the file's</param>
    /// <returns>The session</returns>
    private SettingsWindowSession CreateSession(CompanionSettingsValues? startupValues = null)
    {
        var context = new SettingsWindowContext
                      {
                          SettingsPath = SettingsPath,
                          StartupValues = startupValues ?? CompanionSettingsDocument.Load(SettingsPath).Values,
                          Profiles = _profiles,
                          Autostart = _autostart,
                          Restart = new CompanionRestart(Launch, ExePath, _logger),
                          Texts = () => _texts,
                          ApplyLanguage = _appliedLanguages.Enqueue,
                          RunState = () => _runState,
                          Exit = () => _events.Enqueue(ExitEvent),
                          DirectoryExists = CheckFolder,
                          IsInstallation = _ => true,
                          Logger = _logger
                      };

        return new SettingsWindowSession(context, action => _posted.Writer.TryWrite(action), CreateWindow);
    }

    /// <summary>
    /// Creates a window and keeps it in <see cref="_windows" />.
    /// </summary>
    /// <returns>The window</returns>
    private FakeSettingsWindow CreateWindow()
    {
        var window = new FakeSettingsWindow();

        _windows.Enqueue(window);

        return window;
    }

    /// <summary>
    /// Opens the window and runs the posted actions until it shows its first view.
    /// </summary>
    /// <param name="session">The session</param>
    /// <returns>The window</returns>
    private async Task<FakeSettingsWindow> OpenAsync(SettingsWindowSession session)
    {
        session.Open();

        await RunPostedUntilAsync(() => CountViews() > 0);

        return _windows.Single();
    }

    /// <summary>
    /// Asks the window to save and runs the posted actions until it shows the outcome, then those already posted.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="input">What the user entered</param>
    /// <returns>The outcome</returns>
    private async Task<SettingsSaveOutcome> SaveAsync(FakeSettingsWindow window, SettingsInput input)
    {
        var before = window.Outcomes.Count;

        window.RequestSave(input);

        await RunPostedUntilAsync(() => window.Outcomes.Count > before);

        RunPosted();

        return window.Outcomes[^1];
    }

    /// <summary>
    /// Runs the posted actions in order, waiting for more, until a condition holds; fails after <see cref="_guard" />.
    /// </summary>
    /// <param name="isDone">The condition</param>
    /// <returns>A task that completes when the condition holds</returns>
    private async Task RunPostedUntilAsync(Func<bool> isDone)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        timeout.CancelAfter(_guard);

        var isTimedOut = false;

        try
        {
            while (isDone() == false)
            {
                var action = await _posted.Reader.ReadAsync(timeout.Token);

                action();
            }
        }
        catch (OperationCanceledException) when (TestContext.CancellationToken.IsCancellationRequested == false)
        {
            isTimedOut = true;
        }

        Assert.IsFalse(isTimedOut, "The session did not reach the expected state within the guard time");
    }

    /// <summary>
    /// Runs the actions posted so far, including those they post.
    /// </summary>
    private void RunPosted()
    {
        while (_posted.Reader.TryRead(out var action))
        {
            action();
        }
    }

    /// <summary>
    /// Counts the views shown in every window.
    /// </summary>
    /// <returns>The number of views</returns>
    private int CountViews()
    {
        return _windows.Sum(window => window.Views.Count);
    }

    /// <summary>
    /// Makes the next folder checks wait until <see cref="_folderGate" /> completes.
    /// </summary>
    private void HoldFolderChecks()
    {
        _folderCheckStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _folderGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Checks a folder for the session: reports the start, waits while checks are held and accepts the folder.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns>Always <see langword="true" /></returns>
    private bool CheckFolder(string folder)
    {
        var gate = _folderGate;

        Interlocked.Increment(ref _waitingFolderChecks);
        _folderCheckStarted.TrySetResult();

        try
        {
            _ = gate.Task.Wait(_guard, TestContext.CancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _waitingFolderChecks);
        }

        return folder.Length > 0;
    }

    /// <summary>
    /// Records a launch of a restart and throws <see cref="_launchException" /> when set.
    /// </summary>
    /// <param name="path">The path of the executable</param>
    /// <param name="argument">The argument</param>
    private void Launch(string path, string argument)
    {
        _launches.Enqueue((path, argument));
        _events.Enqueue(LaunchEvent);

        if (_launchException is not null)
        {
            throw _launchException;
        }
    }

    /// <summary>
    /// Writes the Companion's own autostart entry to the store, or deletes it.
    /// </summary>
    /// <param name="isEnabled">Whether the entry is the Companion's own</param>
    private void SetAutostartEntry(bool isEnabled)
    {
        _store.SetCommand(CompanionAutostart.EntryName, isEnabled ? CompanionAutostart.FormatCommand(ExePath) : null);
    }

    /// <summary>
    /// Writes the settings file.
    /// </summary>
    /// <param name="text">The text of the file</param>
    private void WriteSettings(string text)
    {
        File.WriteAllText(SettingsPath, text);
    }

    /// <summary>
    /// Reads a property of the saved settings file.
    /// </summary>
    /// <param name="key">The key, matched exactly</param>
    /// <returns>A copy of the value, or <see langword="null" /> when the key is absent</returns>
    private JsonElement? ReadSavedProperty(string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(SettingsPath));

        return document.RootElement.TryGetProperty(key, out var value) ? value.Clone() : null;
    }

    #endregion // Methods
}