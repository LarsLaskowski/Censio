using System.Globalization;
using System.Reflection;

using Censio.Contracts;
using Censio.Pipe;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="TrayMenu" />.
/// </summary>
[TestClass]
public sealed class TrayMenuTests
{
    #region Fields

    /// <summary>
    /// The current time of the clock: 2026-11-02 18:26:03 in the local time zone of <see cref="CreateClock(int)" /> at one hour.
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 17, 26, 3, TimeSpan.Zero);

    /// <summary>
    /// A tick of today's local date (2026-11-02 00:30:00 at one hour) whose UTC date is the day before.
    /// </summary>
    private static readonly PipeTickSummary _todayTick = new()
                                                         {
                                                             ReceiveTime = new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.Zero),
                                                             IslandCount = 3
                                                         };

    /// <summary>
    /// The culture the test started with.
    /// </summary>
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Restores the culture of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        CultureInfo.CurrentCulture = _culture;
    }

    /// <summary>
    /// The menu lists status, last tick, game, separator, settings, open diagnostics, toggle, both folder entries,
    /// separator, start with Windows, separator, version, separator and exit, in this order, with the status and last-tick
    /// entries greyed out and no entry checked while the autostart is off.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void TrayMenuCreateListsEntriesInOrder()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var expectedKinds = new[]
                            {
                                TrayMenuEntryKind.Status,
                                TrayMenuEntryKind.LastTick,
                                TrayMenuEntryKind.Game,
                                TrayMenuEntryKind.Catalog,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.OpenSettings,
                                TrayMenuEntryKind.OpenDiagnostics,
                                TrayMenuEntryKind.ToggleRecording,
                                TrayMenuEntryKind.OpenRecordingsFolder,
                                TrayMenuEntryKind.OpenLogFolder,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.ToggleAutostart,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Version,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Exit
                            };
        var expectedTexts = new[]
                            {
                                "Status: Receiving",
                                "Last tick: 00:30:00, 3 islands",
                                "Game: searching",
                                "Catalog: reading",
                                string.Empty,
                                "Settings…",
                                "Open diagnostics",
                                "Start recording",
                                "Open recordings folder",
                                "Open log folder",
                                string.Empty,
                                "Start with Windows",
                                string.Empty,
                                "Version 1.2.3",
                                string.Empty,
                                "Exit"
                            };
        var expectedEnabled = new[] { false, false, false, false, false, true, true, true, true, true, false, true, false, false, false, true };
        var expectedChecked = new bool[16];

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        Assert.AreSequenceEqual(expectedKinds, entries.Select(entry => entry.Kind).ToArray(), "The entries should be in the order of the menu");
        Assert.AreSequenceEqual(expectedTexts, entries.Select(entry => entry.Text).ToArray(), "The entries should carry the status, the last tick, the game, the catalog, settings, open diagnostics, the toggle, the folders, the version and Exit");
        Assert.AreSequenceEqual(expectedEnabled, entries.Select(entry => entry.IsEnabled).ToArray(), "Status, last tick, game, catalog, version and separators should be greyed out, the rest enabled");
        Assert.AreSequenceEqual(expectedChecked, entries.Select(entry => entry.IsChecked).ToArray(), "No entry should be checked while the autostart is off");
    }

    /// <summary>
    /// The start-with-Windows entry reads Start with Windows and is enabled live, while recording, in a replay and with an
    /// invalid command line.
    /// </summary>
    /// <param name="mode">The mode of the Companion: live, recording, replay or invalid</param>
    [TestMethod]
    [DataRow("live")]
    [DataRow("recording")]
    [DataRow("replay")]
    [DataRow("invalid")]
    public void TrayMenuCreateAutostartEnabledInEveryMode(string mode)
    {
        // Arrange
        var snapshot = CreateSnapshotForMode(mode);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var autostart = entries.SingleOrDefault(entry => entry.Kind == TrayMenuEntryKind.ToggleAutostart);

        Assert.IsNotNull(autostart, "The menu should hold the start-with-Windows entry in every mode");
        Assert.AreEqual("Start with Windows", autostart.Text, "The entry should read Start with Windows");
        Assert.AreEqual("Start with Windows", TrayMenu.StartWithWindowsText, "The constant should read Start with Windows");
        Assert.IsTrue(autostart.IsEnabled, "The entry should be enabled in every mode");
    }

    /// <summary>
    /// The start-with-Windows entry carries a check mark when the autostart is on, and no other entry does.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateAutostartCheckedWhenEnabled()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = true,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var checkedKinds = entries.Where(entry => entry.IsChecked).Select(entry => entry.Kind).ToArray();

        Assert.AreSequenceEqual(new[] { TrayMenuEntryKind.ToggleAutostart }, checkedKinds, "Only the start-with-Windows entry should be checked when the autostart is on");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.ToggleAutostart).IsEnabled, "The checked entry should stay enabled");
    }

    /// <summary>
    /// The start-with-Windows entry carries no check mark when the autostart is off.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateAutostartUncheckedWhenDisabled()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var autostart = entries.SingleOrDefault(entry => entry.Kind == TrayMenuEntryKind.ToggleAutostart);

        Assert.IsNotNull(autostart, "The menu should hold the start-with-Windows entry");
        Assert.IsFalse(autostart.IsChecked, "The entry should not be checked when the autostart is off");
        Assert.IsTrue(autostart.IsEnabled, "The unchecked entry should stay enabled");
    }

    /// <summary>
    /// The open-diagnostics entry is enabled and second after the first separator, below the settings entry, live, while
    /// recording, in a replay and with an invalid command line.
    /// </summary>
    /// <param name="mode">The mode of the Companion: live, recording, replay or invalid</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("live")]
    [DataRow("recording")]
    [DataRow("replay")]
    [DataRow("invalid")]
    public void TrayMenuCreateOpenDiagnosticsEnabledInEveryMode(string mode)
    {
        // Arrange
        var snapshot = CreateSnapshotForMode(mode);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var diagnostics = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenDiagnostics);

        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Separator) + 2, kinds.IndexOf(TrayMenuEntryKind.OpenDiagnostics), "The entry should be second after the first separator, below the settings entry");
        Assert.AreEqual("Open diagnostics", diagnostics.Text, "The entry should read Open diagnostics");
        Assert.IsTrue(diagnostics.IsEnabled, "The entry should be enabled in every mode");
    }

    /// <summary>
    /// The entry Settings… is enabled, unchecked and directly above Open diagnostics live, while recording, in a replay,
    /// with an invalid command line, in local mode and in forward mode (S1).
    /// </summary>
    /// <param name="mode">The mode of the Companion: live, recording, replay or invalid</param>
    /// <param name="serverMode">The server mode: none, local or forward</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("live", "none")]
    [DataRow("recording", "none")]
    [DataRow("replay", "none")]
    [DataRow("invalid", "none")]
    [DataRow("live", "local")]
    [DataRow("live", "forward")]
    public void TrayMenuCreateHasSettingsAboveOpenDiagnosticsInEveryMode(string mode, string serverMode)
    {
        // Arrange
        var snapshot = CreateSnapshotForMode(mode);
        var server = CreateServerStatusOrNone(serverMode);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = server,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var settings = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenSettings);

        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.OpenDiagnostics) - 1, kinds.IndexOf(TrayMenuEntryKind.OpenSettings), $"Settings… should be directly above Open diagnostics: {mode}, {serverMode}");
        Assert.AreEqual("Settings…", settings.Text, $"The entry should read Settings…: {mode}, {serverMode}");
        Assert.IsTrue(settings.IsEnabled, $"The entry should be enabled in every mode: {mode}, {serverMode}");
        Assert.IsFalse(settings.IsChecked, $"The entry should not be checked: {mode}, {serverMode}");
    }

    /// <summary>
    /// With the German texts every entry of a full menu, from the status to Exit, reads in German, in the same order and
    /// state as in English; only the version entry reads the same in both languages (S9).
    /// </summary>
    /// <param name="serverMode">The server mode: local or forward</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("local")]
    [DataRow("forward")]
    public void TrayMenuCreateGermanShowsGermanEntries(string serverMode)
    {
        // Arrange
        var english = TrayMenu.Create(CompanionTexts.English, CreateFullState(CompanionTexts.English, serverMode));
        var germanState = CreateFullState(CompanionTexts.German, serverMode);

        // Act
        var german = TrayMenu.Create(CompanionTexts.German, germanState);

        // Assert
        Assert.AreSequenceEqual(english.Select(entry => entry.Kind).ToArray(), german.Select(entry => entry.Kind).ToArray(), $"The language should not change the entries or their order: {serverMode}");
        Assert.AreSequenceEqual(english.Select(entry => entry.IsEnabled).ToArray(), german.Select(entry => entry.IsEnabled).ToArray(), $"The language should not change which entries are enabled: {serverMode}");
        Assert.AreSequenceEqual(english.Select(entry => entry.IsChecked).ToArray(), german.Select(entry => entry.IsChecked).ToArray(), $"The language should not change which entries are checked: {serverMode}");
        Assert.Contains(TrayMenuEntryKind.OpenSettings, german.Select(entry => entry.Kind).ToList(), $"The German menu should hold the settings entry: {serverMode}");
        Assert.IsEmpty(SameTexts(english, german), $"Every German entry but the version should differ from the English one: {serverMode}");
    }

    /// <summary>
    /// While a recording runs the toggle reads Stop recording and can be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateRecordingShowsStop()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving (recording)", canRecord: true, isRecording: true);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var toggle = entries.Single(entry => entry.Kind == TrayMenuEntryKind.ToggleRecording);

        Assert.AreEqual("Stop recording", toggle.Text, "The toggle should read Stop recording while recording");
        Assert.IsTrue(toggle.IsEnabled, "The toggle should be enabled while recording");
    }

    /// <summary>
    /// In a replay the toggle reads Start recording and is greyed out; folder entries and exit stay enabled.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateReplayGreysToggle()
    {
        // Arrange
        var snapshot = CreateSnapshot("Replay: Receiving", canRecord: false, isRecording: false);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var toggle = entries.Single(entry => entry.Kind == TrayMenuEntryKind.ToggleRecording);

        Assert.AreEqual("Start recording", toggle.Text, "The toggle should read Start recording in a replay");
        Assert.IsFalse(toggle.IsEnabled, "The toggle should be greyed out in a replay");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenRecordingsFolder).IsEnabled, "The recordings folder entry should be enabled in a replay");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenLogFolder).IsEnabled, "The log folder entry should be enabled in a replay");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Exit).IsEnabled, "The exit entry should be enabled in a replay");
    }

    /// <summary>
    /// With an invalid command line the menu shows the status, no last tick and a greyed toggle; folder entries and exit stay
    /// enabled.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateInvalidGreysToggle()
    {
        // Arrange
        var snapshot = TraySnapshot.ForInvalidCommandLine(CompanionTexts.English);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var toggle = entries.Single(entry => entry.Kind == TrayMenuEntryKind.ToggleRecording);

        Assert.AreEqual("Status: Invalid command line", entries.Single(entry => entry.Kind == TrayMenuEntryKind.Status).Text, "The status entry should name the invalid command line");
        Assert.AreEqual("Last tick: none", entries.Single(entry => entry.Kind == TrayMenuEntryKind.LastTick).Text, "The last-tick entry should show none");
        Assert.AreEqual("Start recording", toggle.Text, "The toggle should read Start recording with an invalid command line");
        Assert.IsFalse(toggle.IsEnabled, "The toggle should be greyed out with an invalid command line");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenRecordingsFolder).IsEnabled, "The recordings folder entry should be enabled with an invalid command line");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenLogFolder).IsEnabled, "The log folder entry should be enabled with an invalid command line");
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Exit).IsEnabled, "The exit entry should be enabled with an invalid command line");
    }

    /// <summary>
    /// The status entry shows the status text of the snapshot and is greyed out.
    /// </summary>
    /// <param name="statusText">The status text of the snapshot</param>
    /// <param name="expected">The expected text of the entry</param>
    [TestMethod]
    [DataRow("Waiting for game", "Status: Waiting for game")]
    [DataRow("Receiving (recording failed)", "Status: Receiving (recording failed)")]
    [DataRow("Replay error: invalid line 7", "Status: Replay error: invalid line 7")]
    public void TrayMenuCreateStatusEntryShowsStatusText(string statusText, string expected)
    {
        // Arrange
        var snapshot = CreateSnapshot(statusText, canRecord: true, isRecording: false);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var status = entries.Single(entry => entry.Kind == TrayMenuEntryKind.Status);

        Assert.AreEqual(expected, status.Text, "The status entry should read Status: and the status text");
        Assert.IsFalse(status.IsEnabled, "The status entry should be greyed out");
    }

    /// <summary>
    /// The version entry cannot be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateVersionEntryIsDisabled()
    {
        // Arrange
        const string version = "1.2.3";

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = version,
                                          Snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false),
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        Assert.IsFalse(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Version).IsEnabled, "The version entry should be greyed out");
    }

    /// <summary>
    /// The exit entry can be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateExitEntryIsEnabled()
    {
        // Arrange
        const string version = "1.2.3";

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = version,
                                          Snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false),
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Exit).IsEnabled, "The exit entry should be enabled");
    }

    /// <summary>
    /// The version entry shows the version of the Core assembly.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateVersionEntryShowsVersion()
    {
        // Arrange
        var version = ProductVersion.Read(typeof(TrayMenu).Assembly);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = version,
                                          Snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false),
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        Assert.AreEqual("Version " + version, entries.Single(entry => entry.Kind == TrayMenuEntryKind.Version).Text, "The version entry should read Version followed by the product version");
    }

    /// <summary>
    /// A tick of today's local date shows its local time only.
    /// </summary>
    /// <param name="receiveTime">The receive time of the tick, in UTC</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [DataRow("2026-11-01T23:30:00Z", "Last tick: 00:30:00, 3 islands")]
    [DataRow("2026-11-02T17:26:03Z", "Last tick: 18:26:03, 3 islands")]
    public void TrayMenuFormatLastTickTodayShowsTimeOnly(string receiveTime, string expected)
    {
        // Arrange
        var tick = CreateTick(receiveTime, 3);

        CultureInfo.CurrentCulture = CreateOddCulture();

        // Act
        var text = TrayMenu.FormatLastTick(CompanionTexts.English, tick, CreateClock(1));

        // Assert
        Assert.AreEqual(expected, text, "A tick of today should show the local time as HH:mm:ss under any culture");
    }

    /// <summary>
    /// A tick of another local date shows its local date and time.
    /// </summary>
    /// <param name="receiveTime">The receive time of the tick, in UTC</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [DataRow("2026-11-01T22:59:59Z", "Last tick: 2026-11-01 23:59:59, 3 islands")]
    [DataRow("2026-11-02T23:00:00Z", "Last tick: 2026-11-03 00:00:00, 3 islands")]
    [DataRow("2025-11-02T10:00:00Z", "Last tick: 2025-11-02 11:00:00, 3 islands")]
    public void TrayMenuFormatLastTickOtherDayShowsDate(string receiveTime, string expected)
    {
        // Arrange
        var tick = CreateTick(receiveTime, 3);

        CultureInfo.CurrentCulture = CreateOddCulture();

        // Act
        var text = TrayMenu.FormatLastTick(CompanionTexts.English, tick, CreateClock(1));

        // Assert
        Assert.AreEqual(expected, text, "A tick of another day should show the local time as yyyy-MM-dd HH:mm:ss under any culture");
    }

    /// <summary>
    /// A tick less than one day after the earliest representable time shows in UTC with a Z, from one day on in local time.
    /// </summary>
    /// <param name="secondsAfterMinValue">The seconds of the receive time after <see cref="DateTimeOffset.MinValue" /></param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [DataRow(0L, "Last tick: 0001-01-01 00:00:00Z, 2 islands")]
    [DataRow(86399L, "Last tick: 0001-01-01 23:59:59Z, 2 islands")]
    [DataRow(86400L, "Last tick: 0001-01-01 23:00:00, 2 islands")]
    public void TrayMenuFormatLastTickNearMinValueShowsUtc(long secondsAfterMinValue, string expected)
    {
        // Arrange
        var tick = new PipeTickSummary
                   {
                       ReceiveTime = DateTimeOffset.MinValue.AddSeconds(secondsAfterMinValue),
                       IslandCount = 2
                   };

        // Act
        var text = TrayMenu.FormatLastTick(CompanionTexts.English, tick, CreateClock(-1));

        // Assert
        Assert.AreEqual(expected, text, "A time within one day after the earliest time should show in UTC, a later one in local time");
    }

    /// <summary>
    /// A tick less than one day before the latest representable time shows in UTC with a Z, from one day on in local time.
    /// </summary>
    /// <param name="secondsBeforeMaxValue">The seconds of the receive time before <see cref="DateTimeOffset.MaxValue" /></param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [DataRow(0L, "Last tick: 9999-12-31 23:59:59Z, 2 islands")]
    [DataRow(86399L, "Last tick: 9999-12-31 00:00:00Z, 2 islands")]
    [DataRow(86400L, "Last tick: 9999-12-31 00:59:59, 2 islands")]
    public void TrayMenuFormatLastTickNearMaxValueShowsUtc(long secondsBeforeMaxValue, string expected)
    {
        // Arrange
        var tick = new PipeTickSummary
                   {
                       ReceiveTime = DateTimeOffset.MaxValue.AddSeconds(-secondsBeforeMaxValue),
                       IslandCount = 2
                   };

        // Act
        var text = TrayMenu.FormatLastTick(CompanionTexts.English, tick, CreateClock(1));

        // Assert
        Assert.AreEqual(expected, text, "A time within one day before the latest time should show in UTC, an earlier one in local time");
    }

    /// <summary>
    /// One island is written in the singular, every other count in the plural.
    /// </summary>
    /// <param name="islandCount">The number of islands of the tick</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [DataRow(1, "Last tick: 00:30:00, 1 island")]
    [DataRow(0, "Last tick: 00:30:00, 0 islands")]
    [DataRow(2, "Last tick: 00:30:00, 2 islands")]
    [DataRow(256, "Last tick: 00:30:00, 256 islands")]
    public void TrayMenuFormatLastTickOneIslandSingular(int islandCount, string expected)
    {
        // Arrange
        var tick = _todayTick with
                              {
                                  IslandCount = islandCount
                              };

        // Act
        var text = TrayMenu.FormatLastTick(CompanionTexts.English, tick, CreateClock(1));

        // Assert
        Assert.AreEqual(expected, text, "The island count should read 1 island in the singular and n islands otherwise");
    }

    /// <summary>
    /// Before the first tick the text reads none.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatLastTickNoneBeforeFirstTick()
    {
        // Arrange
        PipeTickSummary? tick = null;

        // Act
        var text = TrayMenu.FormatLastTick(CompanionTexts.English, tick, CreateClock(1));

        // Assert
        Assert.AreEqual("Last tick: none", text, "Without a tick the text should read Last tick: none");
    }

    /// <summary>
    /// Without a last tick the tooltip has one line: the application name and the status text.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipWithoutTickHasOneLine()
    {
        // Arrange
        var snapshot = new TraySnapshot
                       {
                           StatusText = "Receiving",
                           Look = TrayIconLook.Receiving,
                           LastTick = null
                       };

        // Act
        var toolTip = TrayMenu.FormatSnapshotToolTip(CompanionTexts.English, snapshot, CreateClock(1));

        // Assert
        Assert.AreEqual("Censio Companion: Receiving", toolTip, "Without a tick the tooltip should be the one status line");
    }

    /// <summary>
    /// With a last tick the tooltip has a second line with the last tick.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipWithTickHasTwoLines()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving (recording)", canRecord: true, isRecording: true);

        // Act
        var toolTip = TrayMenu.FormatSnapshotToolTip(CompanionTexts.English, snapshot, CreateClock(1));

        // Assert
        Assert.AreEqual("Censio Companion: Receiving (recording)\nLast tick: 00:30:00, 3 islands", toolTip, "With a tick the tooltip should hold the status line, a line feed and the last-tick line");
    }

    /// <summary>
    /// The longest tooltip, a replay error at the largest line number with a tick at the earliest time and 256 islands, fits
    /// the limit of the tray icon.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipLongestFitsLimit()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = PipeReplayProblemKind.InvalidLine,
                                       LineNumber = long.MaxValue
                                   }
                     };
        var tick = new PipeTickSummary
                   {
                       ReceiveTime = DateTimeOffset.MinValue,
                       IslandCount = 256
                   };
        var snapshot = TraySnapshot.ForReplay(CompanionTexts.English, status, tick);

        // Act
        var toolTip = TrayMenu.FormatSnapshotToolTip(CompanionTexts.English, snapshot, CreateClock(-1));

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: invalid line 9223372036854775807\nLast tick: 0001-01-01 00:00:00Z, 256 islands", toolTip, "The longest tooltip should hold the replay error and the last tick in UTC");
        Assert.IsLessThanOrEqualTo(TrayMenu.MaxToolTipLength, toolTip.Length, "The longest tooltip should fit the limit of the tray icon");
    }

    /// <summary>
    /// The time until the next local midnight is measured in the local time zone; at midnight the next one is a day away.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC</param>
    /// <param name="offsetHours">The offset of the local time zone, in hours</param>
    /// <param name="expectedSeconds">The expected time, in seconds</param>
    [TestMethod]
    [DataRow("2026-11-02T17:26:03Z", 1, 20037)]
    [DataRow("2026-11-02T23:00:00Z", 1, 86400)]
    [DataRow("2026-11-02T02:00:00Z", -3, 3600)]
    public void TrayMenuUntilNextLocalMidnightNormalDay(string utcNow, int offsetHours, int expectedSeconds)
    {
        // Arrange
        var clock = CreateClock(offsetHours, DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));

        // Act
        var delay = TrayMenu.UntilNextLocalMidnight(clock);

        // Assert
        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), delay, "The delay should end at the next local midnight");
    }

    /// <summary>
    /// When the clocks jump forward at midnight, the delay ends one hour later, at the first valid local time.
    /// </summary>
    [TestMethod]
    public void TrayMenuUntilNextLocalMidnightSkipsInvalidMidnight()
    {
        // Arrange
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                                                                    new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
                                                                    TimeSpan.FromHours(1),
                                                                    TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), 11, 3),
                                                                    TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), 12, 20));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Censio Midnight Jump", TimeSpan.FromHours(-3), "Censio Midnight Jump", "Censio Standard", "Censio Daylight", [rule]);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 22, 0, 0, TimeSpan.Zero));

        clock.SetLocalTimeZone(zone);

        // Act
        var delay = TrayMenu.UntilNextLocalMidnight(clock);

        // Assert
        Assert.AreEqual(TimeSpan.FromHours(5), delay, "The delay should end at 01:00 local time when midnight does not exist");
    }

    /// <summary>
    /// Just before midnight the delay is at least one second.
    /// </summary>
    /// <param name="ticksBeforeMidnight">The ticks of the current time before the local midnight</param>
    /// <param name="expectedTicks">The expected delay, in ticks</param>
    [TestMethod]
    [DataRow(1L, 10_000_000L)]
    [DataRow(5_000_000L, 10_000_000L)]
    [DataRow(10_000_000L, 10_000_000L)]
    [DataRow(15_000_000L, 15_000_000L)]
    public void TrayMenuUntilNextLocalMidnightAtLeastOneSecond(long ticksBeforeMidnight, long expectedTicks)
    {
        // Arrange
        var localMidnightUtc = new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero);
        var clock = CreateClock(1, localMidnightUtc.AddTicks(-ticksBeforeMidnight));

        // Act
        var delay = TrayMenu.UntilNextLocalMidnight(clock);

        // Assert
        Assert.AreEqual(TimeSpan.FromTicks(expectedTicks), delay, "The delay should be the time to midnight, but at least one second");
    }

    /// <summary>
    /// The tooltip names the application.
    /// </summary>
    [TestMethod]
    public void TrayMenuToolTipIsCensioCompanion()
    {
        // Arrange
        const string expected = "Censio Companion";

        // Act
        var toolTip = TrayMenu.ToolTip;

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip of the tray icon should be Censio Companion");
    }

    /// <summary>
    /// The tooltip shows the application name and the text of the state.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame, "Censio Companion: Waiting for game")]
    [DataRow(PipeConnectionState.Connected, "Censio Companion: Connected")]
    [DataRow(PipeConnectionState.Receiving, "Censio Companion: Receiving")]
    [DataRow(PipeConnectionState.PipeBusy, "Censio Companion: Pipe busy")]
    [DataRow(PipeConnectionState.ProtocolMismatch, "Censio Companion: Protocol mismatch")]
    [DataRow(PipeConnectionState.Error, "Censio Companion: Error")]
    public void TrayMenuFormatToolTipReturnsTextPerState(PipeConnectionState state, string expected)
    {
        // Arrange
        // (state and expected come from the data row)

        // Act
        var toolTip = TrayMenu.FormatToolTip(CompanionTexts.English, state);

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should be the application name, a colon and the state text");
    }

    /// <summary>
    /// An undefined state is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipUndefinedStateThrows()
    {
        // Arrange
        var state = (PipeConnectionState)99;

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TrayMenu.FormatToolTip(CompanionTexts.English, state), "An undefined state should be rejected");

        // Assert
        Assert.AreEqual("state", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// The tooltip appends the recording suffix to the state text of the connection.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Off, "Censio Companion: Waiting for game")]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Off, "Censio Companion: Receiving")]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Recording, "Censio Companion: Waiting for game (recording)")]
    [DataRow(PipeConnectionState.Connected, PipeRecordingState.Recording, "Censio Companion: Connected (recording)")]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Recording, "Censio Companion: Receiving (recording)")]
    [DataRow(PipeConnectionState.PipeBusy, PipeRecordingState.Recording, "Censio Companion: Pipe busy (recording)")]
    [DataRow(PipeConnectionState.ProtocolMismatch, PipeRecordingState.Recording, "Censio Companion: Protocol mismatch (recording)")]
    [DataRow(PipeConnectionState.Error, PipeRecordingState.Recording, "Censio Companion: Error (recording)")]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Failed, "Censio Companion: Waiting for game (recording failed)")]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Failed, "Censio Companion: Receiving (recording failed)")]
    [DataRow(PipeConnectionState.Error, PipeRecordingState.Failed, "Censio Companion: Error (recording failed)")]
    public void TrayMenuFormatToolTipWithRecordingStateAppendsSuffix(PipeConnectionState state, PipeRecordingState recording, string expected)
    {
        // Arrange
        // (state, recording and expected come from the data row)

        // Act
        var toolTip = TrayMenu.FormatToolTip(CompanionTexts.English, state, recording);

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should carry the suffix of the recording state");
    }

    /// <summary>
    /// The replay tooltip shows the state of the replayed connection until the replay finished.
    /// </summary>
    /// <param name="state">The state of the replayed connection</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.Connected, false, "Censio Companion: Replay: Connected")]
    [DataRow(PipeConnectionState.Receiving, false, "Censio Companion: Replay: Receiving")]
    [DataRow(PipeConnectionState.ProtocolMismatch, false, "Censio Companion: Replay: Protocol mismatch")]
    [DataRow(PipeConnectionState.Connected, true, "Censio Companion: Replay finished")]
    [DataRow(PipeConnectionState.Receiving, true, "Censio Companion: Replay finished")]
    [DataRow(PipeConnectionState.ProtocolMismatch, true, "Censio Companion: Replay finished")]
    public void TrayMenuFormatReplayToolTipReturnsTextPerStatus(PipeConnectionState state, bool isFinished, string expected)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = state,
                         IsFinished = isFinished
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, toolTip, "The replay tooltip should name the state, or the end of the replay");
    }

    /// <summary>
    /// The replay tooltip names the problem that ended the replay, which wins over the end of the replay.
    /// </summary>
    /// <param name="kind">The kind of the problem</param>
    /// <param name="lineNumber">The line number of the problem, if any</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeReplayProblemKind.FileNotFound, null, true, "Censio Companion: Replay error: file not found")]
    [DataRow(PipeReplayProblemKind.NotARecording, null, true, "Censio Companion: Replay error: not a recording")]
    [DataRow(PipeReplayProblemKind.UnsupportedVersion, null, true, "Censio Companion: Replay error: unsupported version")]
    [DataRow(PipeReplayProblemKind.ReadFailed, null, true, "Censio Companion: Replay error: read failed")]
    [DataRow(PipeReplayProblemKind.InvalidLine, 7L, true, "Censio Companion: Replay error: invalid line 7")]
    [DataRow(PipeReplayProblemKind.InvalidLine, 1234567L, true, "Censio Companion: Replay error: invalid line 1234567")]
    [DataRow(PipeReplayProblemKind.InvalidLine, long.MaxValue, true, "Censio Companion: Replay error: invalid line 9223372036854775807")]
    [DataRow(PipeReplayProblemKind.FileNotFound, null, false, "Censio Companion: Replay error: file not found")]
    public void TrayMenuFormatReplayToolTipProblemReturnsErrorText(PipeReplayProblemKind kind, long? lineNumber, bool isFinished, string expected)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = isFinished,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = kind,
                                       LineNumber = lineNumber
                                   }
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, toolTip, "The replay tooltip should name the reason of the problem");
    }

    /// <summary>
    /// The line number in the tooltip has no digit grouping under any culture.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatReplayToolTipInvalidLineIgnoresCulture()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = PipeReplayProblemKind.InvalidLine,
                                       LineNumber = 1234567
                                   }
                     };

        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: invalid line 1234567", toolTip, "The line number should be written without grouping");
    }

    /// <summary>
    /// A replay that ended with a problem of sending names its reason, and the reason is the text of its kind.
    /// </summary>
    /// <param name="kind">The kind of the send problem</param>
    /// <param name="reason">The expected reason</param>
    [TestMethod]
    [TestCategory("AC5")]
    [TestCategory("AC7")]
    [DataRow(ReplaySendProblemKind.TokenFileCannotBeRead, "token file cannot be read")]
    [DataRow(ReplaySendProblemKind.TokenFileHoldsNoValidToken, "token file holds no valid token")]
    [DataRow(ReplaySendProblemKind.ServerUnreachable, "server unreachable")]
    [DataRow(ReplaySendProblemKind.TokenRefused, "token refused")]
    [DataRow(ReplaySendProblemKind.CompanionConnected, "another Companion is connected")]
    [DataRow(ReplaySendProblemKind.VersionNotSupported, "server does not support contract version 2")]
    [DataRow(ReplaySendProblemKind.NoAnswer, "server does not answer")]
    [DataRow(ReplaySendProblemKind.ConnectionLost, "server connection lost")]
    [DataRow(ReplaySendProblemKind.InvalidReply, "invalid server reply")]
    public void TrayMenuReplaySendProblemGivesReasonText(ReplaySendProblemKind kind, string reason)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         SendProblem = kind
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: " + reason, toolTip, $"The tooltip should name the reason of {kind}");
        Assert.AreEqual(reason, TrayMenu.FormatReplaySendReason(CompanionTexts.English, kind), $"The reason of {kind} should be its text");
    }

    /// <summary>
    /// A replay a server refused for its version names contract version 2, the only version this build sends, in the
    /// tooltip and in the reason.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void TrayMenuVersionNotSupportedNamesVersionTwo()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         SendProblem = ReplaySendProblemKind.VersionNotSupported
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: server does not support contract version 2", toolTip, "The tooltip should name contract version 2");
        Assert.AreEqual("server does not support contract version 2", TrayMenu.FormatReplaySendReason(CompanionTexts.English, ReplaySendProblemKind.VersionNotSupported), "The reason should name contract version 2");
    }

    /// <summary>
    /// A send problem wins over a problem of the recording, the end of the replay and the counts.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void TrayMenuReplaySendProblemWinsOverProblem()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = PipeReplayProblemKind.InvalidLine,
                                       LineNumber = 7
                                   },
                         SendProblem = ReplaySendProblemKind.ConnectionLost,
                         Sent = new ReplaySendCounts
                                {
                                    Ticks = 3,
                                    Refused = 0
                                }
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: server connection lost", toolTip, "The send problem should be shown first");
    }

    /// <summary>
    /// A replay that connects to the server says so.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void TrayMenuReplayConnectingGivesConnectingText()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Connected,
                         IsConnecting = true
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay: Connecting to server", toolTip, "The tooltip should say that the replay connects to the server");
    }

    /// <summary>
    /// A replay sent to the server ends with the counts of sent ticks and refused messages, one tick in the singular.
    /// </summary>
    /// <param name="ticks">The number of sent ticks</param>
    /// <param name="refused">The number of refused messages</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC8")]
    [DataRow(0, 0, "Censio Companion: Replay finished: 0 ticks sent, 0 refused")]
    [DataRow(1, 0, "Censio Companion: Replay finished: 1 tick sent, 0 refused")]
    [DataRow(31, 1, "Censio Companion: Replay finished: 31 ticks sent, 1 refused")]
    [DataRow(2, 3, "Censio Companion: Replay finished: 2 ticks sent, 3 refused")]
    public void TrayMenuReplaySendFinishedGivesCounts(int ticks, int refused, string expected)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         Sent = new ReplaySendCounts
                                {
                                    Ticks = ticks,
                                    Refused = refused
                                }
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should name the sent ticks and the refused messages");
    }

    /// <summary>
    /// The tooltip of an invalid command line has the exact text.
    /// </summary>
    [TestMethod]
    public void TrayMenuInvalidCommandLineToolTipHasExactText()
    {
        // Arrange
        const string expected = "Censio Companion: Invalid command line";

        // Act
        var toolTip = TrayMenu.InvalidCommandLineToolTip;

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should name the application and the invalid command line");
    }

    /// <summary>
    /// An undefined recording state is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipUndefinedRecordingStateThrows()
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TrayMenu.FormatToolTip(CompanionTexts.English, PipeConnectionState.Connected, (PipeRecordingState)99), "An undefined recording state should be rejected");

        // Assert
        Assert.AreEqual("recording", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// An undefined problem kind is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatReplayToolTipUndefinedProblemKindThrows()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Connected,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = (PipeReplayProblemKind)99
                                   }
                     };

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TrayMenu.FormatReplayToolTip(CompanionTexts.English, status), "An undefined problem kind should be rejected");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// An undefined send problem kind has no reason text and is rejected by the tooltip.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void TrayMenuFormatReplayToolTipUndefinedSendProblemKindThrows()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Connected,
                         IsFinished = true,
                         SendProblem = (ReplaySendProblemKind)99
                     };

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TrayMenu.FormatReplayToolTip(CompanionTexts.English, status), "An undefined send problem kind should be rejected");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the parameter");
        Assert.IsNull(TrayMenu.FormatReplaySendReason(CompanionTexts.English, (ReplaySendProblemKind)99), "An undefined send problem kind should have no reason text");
    }

    /// <summary>
    /// A state a replay cannot be in is rejected.
    /// </summary>
    /// <param name="state">The state</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame)]
    [DataRow(PipeConnectionState.Error)]
    [DataRow((PipeConnectionState)99)]
    public void TrayMenuFormatReplayToolTipUnreachableStateThrows(PipeConnectionState state)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = state
                     };

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TrayMenu.FormatReplayToolTip(CompanionTexts.English, status), "A state a replay cannot reach should be rejected");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// The Core assembly carries the build version of the solution.
    /// </summary>
    [TestMethod]
    public void TrayMenuCoreAssemblyCarriesBuildVersion()
    {
        // Arrange
        var assembly = typeof(TrayMenu).Assembly;
        var expected = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                               .Single(attribute => attribute.Key == "CensioBuildVersion")
                               .Value;

        // Act
        var actual = ProductVersion.Read(assembly);

        // Assert
        Assert.AreEqual(expected, actual, "The Core assembly should carry the build version as its informational version");
    }

    /// <summary>
    /// The game entry lies directly below the last-tick entry and above the first separator, greyed out like it, and shows
    /// the text of each status, never the folder.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="source">The source of a found installation</param>
    /// <param name="build">The build of a found installation</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC3")]
    [TestCategory("AC4")]
    [TestCategory("AC5")]
    [TestCategory("AC6")]
    [DataRow(GameInstallationStatusKind.Searching, null, null, "Game: searching", DisplayName = "Searching")]
    [DataRow(GameInstallationStatusKind.Found, GameInstallationSource.UbisoftConnect, "26.29905.4.51797", "Game: build 26.29905.4.51797 (Ubisoft Connect)", DisplayName = "Found through Ubisoft Connect")]
    [DataRow(GameInstallationStatusKind.Found, GameInstallationSource.SettingsFile, null, "Game: build unknown (settings file)", DisplayName = "Found through the settings file, build unknown")]
    [DataRow(GameInstallationStatusKind.NotFound, null, null, "Game: not found", DisplayName = "Not found")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotFound, null, null, "Game: folder in settings not found", DisplayName = "Settings folder not found")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotFullPath, null, null, "Game: folder in settings is not a full path", DisplayName = "Settings folder not a full path")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotInstallation, null, null, "Game: folder in settings is not an Anno 117 installation", DisplayName = "Settings folder not an installation")]
    [DataRow(GameInstallationStatusKind.SettingsFileInvalid, null, null, "Game: settings file invalid", DisplayName = "Settings file invalid")]
    public void TrayMenuCreateGameLineBelowLastTick(GameInstallationStatusKind kind, GameInstallationSource? source, string? build, string expected)
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var game = CreateGameStatus(kind, source, build);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = game,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var index = kinds.IndexOf(TrayMenuEntryKind.Game);

        Assert.ContainsSingle(entryKind => entryKind == TrayMenuEntryKind.Game, kinds, "The menu should hold exactly one game entry");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.LastTick) + 1, index, "The game entry should be directly below the last-tick entry");
        Assert.AreEqual(index + 1, kinds.IndexOf(TrayMenuEntryKind.Catalog), "The catalog entry should be directly below the game entry");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Separator) - 2, index, "The game entry should be directly above the catalog entry and the first separator");
        Assert.AreEqual(expected, entries[index].Text, "The game entry should show the text of the status");
        Assert.IsFalse(entries[index].IsEnabled, "The game entry should be greyed out like the last-tick entry");
        Assert.IsFalse(entries[index].IsChecked, "The game entry should not be checked");
    }

    /// <summary>
    /// The catalog text follows the state of the build: reading, the entry count (invariant, no grouping, also for one
    /// and none), no game, failed; the count is shown only when ready.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="count">The entry count</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameCatalogStatusKind.Reading, 0, "Catalog: reading", DisplayName = "Reading")]
    [DataRow(GameCatalogStatusKind.Reading, 7, "Catalog: reading", DisplayName = "Reading ignores the count")]
    [DataRow(GameCatalogStatusKind.Ready, 0, "Catalog: 0 entries", DisplayName = "Ready, none")]
    [DataRow(GameCatalogStatusKind.Ready, 1, "Catalog: 1 entries", DisplayName = "Ready, one")]
    [DataRow(GameCatalogStatusKind.Ready, 1234567, "Catalog: 1234567 entries", DisplayName = "Ready, no grouping")]
    [DataRow(GameCatalogStatusKind.NoGame, 5, "Catalog: no game", DisplayName = "No game")]
    [DataRow(GameCatalogStatusKind.Failed, 5, "Catalog: failed", DisplayName = "Failed")]
    public void TrayMenuFormatCatalogGivesTextPerState(GameCatalogStatusKind kind, int count, string expected)
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        var status = new GameCatalogStatus
                     {
                         Kind = kind,
                         EntryCount = count
                     };

        // Act
        var text = TrayMenu.FormatCatalog(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, "The catalog text should follow the state of the build, whatever the culture");
    }

    /// <summary>
    /// The new catalog texts: checking, extracting names, extracting icons with the count, and the count with the icons failed or
    /// the cache not written.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="count">The entry count</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(GameCatalogStatusKind.Checking, 0, "Catalog: checking", DisplayName = "Checking")]
    [DataRow(GameCatalogStatusKind.Checking, 7, "Catalog: checking", DisplayName = "Checking ignores the count")]
    [DataRow(GameCatalogStatusKind.ExtractingNames, 0, "Catalog: extracting names", DisplayName = "Extracting names")]
    [DataRow(GameCatalogStatusKind.ExtractingNames, 7, "Catalog: extracting names", DisplayName = "Extracting names ignores the count")]
    [DataRow(GameCatalogStatusKind.ExtractingIcons, 412, "Catalog: extracting icons, 412 entries", DisplayName = "Extracting icons")]
    [DataRow(GameCatalogStatusKind.ExtractingIcons, 1234567, "Catalog: extracting icons, 1234567 entries", DisplayName = "Extracting icons, no grouping")]
    [DataRow(GameCatalogStatusKind.IconsFailed, 412, "Catalog: 412 entries, icons failed", DisplayName = "Icons failed")]
    [DataRow(GameCatalogStatusKind.NotCached, 412, "Catalog: 412 entries, not cached", DisplayName = "Not cached")]
    [DataRow(GameCatalogStatusKind.NotCached, 1234567, "Catalog: 1234567 entries, not cached", DisplayName = "Not cached, no grouping")]
    public void TrayMenuFormatCatalogNewStates(GameCatalogStatusKind kind, int count, string expected)
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        var status = new GameCatalogStatus
                     {
                         Kind = kind,
                         EntryCount = count
                     };

        // Act
        var text = TrayMenu.FormatCatalog(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, "The catalog text should be the one of the state, whatever the culture");
    }

    /// <summary>
    /// A status kind that is not defined is rejected with an exception that names the parameter.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void TrayMenuFormatCatalogRejectsUndefinedKind()
    {
        // Arrange
        var status = new GameCatalogStatus
                     {
                         Kind = (GameCatalogStatusKind)99
                     };

        // Act
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TrayMenu.FormatCatalog(CompanionTexts.English, status), "An undefined kind should throw");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the status");
    }

    /// <summary>
    /// The catalog entry is greyed out, unchecked and directly below the game entry, with the text of the status.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="count">The entry count</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameCatalogStatusKind.Reading, 0, "Catalog: reading", DisplayName = "Reading")]
    [DataRow(GameCatalogStatusKind.Ready, 12, "Catalog: 12 entries", DisplayName = "Ready")]
    [DataRow(GameCatalogStatusKind.NoGame, 0, "Catalog: no game", DisplayName = "No game")]
    [DataRow(GameCatalogStatusKind.Failed, 0, "Catalog: failed", DisplayName = "Failed")]
    public void TrayMenuCreatePlacesCatalogBelowGame(GameCatalogStatusKind kind, int count, string expected)
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var catalog = new GameCatalogStatus
                      {
                          Kind = kind,
                          EntryCount = count
                      };

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = catalog,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var index = kinds.IndexOf(TrayMenuEntryKind.Catalog);

        Assert.ContainsSingle(entryKind => entryKind == TrayMenuEntryKind.Catalog, kinds, "The menu should hold exactly one catalog entry");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Game) + 1, index, "The catalog entry should be directly below the game entry");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Separator) - 1, index, "The catalog entry should be directly above the first separator");
        Assert.AreEqual(expected, entries[index].Text, "The catalog entry should show the text of the status");
        Assert.IsFalse(entries[index].IsEnabled, "The catalog entry should be greyed out");
        Assert.IsFalse(entries[index].IsChecked, "The catalog entry should not be checked");
    }

    /// <summary>
    /// The server line reads the text of each state, with the port where the state names one (S1, S3, E1, E2, E4).
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="port">The port</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(LocalServerState.Starting, 5000, "Server: starting")]
    [DataRow(LocalServerState.Starting, 5117, "Server: starting")]
    [DataRow(LocalServerState.Running, 5000, "Server: running at http://localhost:5000/")]
    [DataRow(LocalServerState.Running, 5117, "Server: running at http://localhost:5117/")]
    [DataRow(LocalServerState.PortInUse, 5000, "Server: port 5000 in use")]
    [DataRow(LocalServerState.PortInUse, 5117, "Server: port 5117 in use")]
    [DataRow(LocalServerState.Failed, 5000, "Server: failed")]
    [DataRow(LocalServerState.Failed, 5117, "Server: failed")]
    [DataRow(LocalServerState.SettingsInvalid, 5000, "Server: settings invalid")]
    [DataRow(LocalServerState.SettingsInvalid, 5117, "Server: settings invalid")]
    public void TrayMenuFormatLocalServerReturnsTextPerState(LocalServerState state, int port, string expected)
    {
        // Arrange
        var status = CreateLocalServerStatus(state, port);

        // Act
        var text = TrayMenu.FormatLocalServer(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, $"The server line of {state} at port {port} should be its text");
    }

    /// <summary>
    /// In local mode the greyed, unchecked server line is directly below the catalog entry and directly above the first
    /// separator, with the text of the status (S1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void TrayMenuCreateInLocalModeShowsServerLineBelowCatalog()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var localServer = CreateLocalServerStatus(LocalServerState.Running, 5117);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = new TrayServerStatus
                                                   {
                                                       Local = localServer
                                                   },
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var index = kinds.IndexOf(TrayMenuEntryKind.LocalServer);

        Assert.ContainsSingle(entryKind => entryKind == TrayMenuEntryKind.LocalServer, kinds, "The menu should hold exactly one server line");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Catalog) + 1, index, "The server line should be directly below the catalog entry");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Separator) - 1, index, "The server line should be directly above the first separator");
        Assert.AreEqual("Server: running at http://localhost:5117/", entries[index].Text, "The server line should show the text of the status");
        Assert.IsFalse(entries[index].IsEnabled, "The server line should be greyed out");
        Assert.IsFalse(entries[index].IsChecked, "The server line should not be checked");
    }

    /// <summary>
    /// Without local mode the menu shows neither the server line nor Open Censio, in every mode of the Companion (S7).
    /// </summary>
    /// <param name="mode">The mode of the Companion: live, recording, replay or invalid</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("live")]
    [DataRow("recording")]
    [DataRow("replay")]
    [DataRow("invalid")]
    public void TrayMenuCreateWithoutLocalModeShowsNoServerEntries(string mode)
    {
        // Arrange
        var snapshot = CreateSnapshotForMode(mode);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = null,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();

        Assert.DoesNotContain(TrayMenuEntryKind.LocalServer, kinds, $"Without local mode the menu should hold no server line: {mode}");
        Assert.DoesNotContain(TrayMenuEntryKind.OpenCensio, kinds, $"Without local mode the menu should hold no Open Censio: {mode}");
        Assert.IsEmpty(entries.Where(entry => entry.Text.StartsWith("Server:", StringComparison.Ordinal) || entry.Text == TrayMenu.OpenCensioText).ToList(), $"Without local mode no entry should carry a server text: {mode}");
    }

    /// <summary>
    /// In local mode the menu lists the server line below the catalog and Open Censio directly after the first separator,
    /// above Settings… and Open diagnostics, the rest of the menu in today's order (S1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC3")]
    public void TrayMenuCreateInLocalModeShowsOpenCensioAboveDiagnostics()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var localServer = CreateLocalServerStatus(LocalServerState.Running, 5000);
        var expectedKinds = new[]
                            {
                                TrayMenuEntryKind.Status,
                                TrayMenuEntryKind.LastTick,
                                TrayMenuEntryKind.Game,
                                TrayMenuEntryKind.Catalog,
                                TrayMenuEntryKind.LocalServer,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.OpenCensio,
                                TrayMenuEntryKind.OpenSettings,
                                TrayMenuEntryKind.OpenDiagnostics,
                                TrayMenuEntryKind.ToggleRecording,
                                TrayMenuEntryKind.OpenRecordingsFolder,
                                TrayMenuEntryKind.OpenLogFolder,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.ToggleAutostart,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Version,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Exit
                            };

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = new TrayServerStatus
                                                   {
                                                       Local = localServer
                                                   },
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var openCensio = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenCensio);

        Assert.AreSequenceEqual(expectedKinds, entries.Select(entry => entry.Kind).ToArray(), "Open Censio should be directly above Settings…, Settings… directly above Open diagnostics and the server line below the catalog");
        Assert.AreEqual("Open Censio", openCensio.Text, "The entry should read Open Censio");
        Assert.AreEqual(TrayMenu.OpenCensioText, openCensio.Text, "The entry should carry the text of the constant");
        Assert.IsFalse(openCensio.IsChecked, "Open Censio should not be checked");
    }

    /// <summary>
    /// Open Censio is enabled only while the server runs (S1, E1, E4).
    /// </summary>
    /// <param name="state">The state of the server</param>
    /// <param name="expected">Whether Open Censio is enabled</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(LocalServerState.Starting, false)]
    [DataRow(LocalServerState.Running, true)]
    [DataRow(LocalServerState.PortInUse, false)]
    [DataRow(LocalServerState.Failed, false)]
    [DataRow(LocalServerState.SettingsInvalid, false)]
    public void TrayMenuCreateEnablesOpenCensioOnlyWhileRunning(LocalServerState state, bool expected)
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var localServer = CreateLocalServerStatus(state, 5000);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = new TrayServerStatus
                                                   {
                                                       Local = localServer
                                                   },
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var openCensio = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenCensio);

        Assert.AreEqual(expected, openCensio.IsEnabled, $"Open Censio should be enabled only while the server runs, not in {state}");
    }

    /// <summary>
    /// An invalid server setting, forward mode's included, keeps the server line reading settings invalid and the greyed
    /// Open Censio of local mode (S6, feature 40 challenge 1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void TrayMenuCreateWithInvalidSettingKeepsGreyedOpenCensio()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var server = new TrayServerStatus
                     {
                         Local = CreateLocalServerStatus(LocalServerState.SettingsInvalid, 5000)
                     };

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = server,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var serverLine = entries.Single(entry => entry.Kind == TrayMenuEntryKind.LocalServer);
        var openCensio = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenCensio);

        Assert.AreEqual("Server: settings invalid", serverLine.Text, "The server line should read settings invalid");
        Assert.IsFalse(openCensio.IsEnabled, "Open Censio should stay greyed out with invalid settings");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.OpenSettings) - 1, kinds.IndexOf(TrayMenuEntryKind.OpenCensio), "Open Censio should stay directly above Settings…");
        Assert.DoesNotContain(TrayMenuEntryKind.ServerConnection, kinds, "Invalid settings should show no forward-mode server line");
    }

    /// <summary>
    /// The server line of forward mode reads the text of each state and problem: connecting (whatever problem the
    /// status still carries), connected, the five
    /// retryable failures, the two final refusals, and not connected for any other status (S1, E3, E4, E6, E7).
    /// </summary>
    /// <param name="state">The state of live sending</param>
    /// <param name="problem">The problem, or <see langword="null" /> when there is none</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(LiveSendState.Connecting, null, "Server: connecting")]
    [DataRow(LiveSendState.Connecting, ReplaySendProblemKind.ConnectionLost, "Server: connecting")]
    [DataRow(LiveSendState.Connected, null, "Server: connected")]
    [DataRow((LiveSendState)99, null, "Server: not connected")]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.ServerUnreachable, "Server: unreachable, retrying")]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.NoAnswer, "Server: not answering, retrying")]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost, "Server: connection lost, retrying")]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.InvalidReply, "Server: invalid reply, retrying")]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.CompanionConnected, "Server: another Companion is connected, retrying")]
    [DataRow(LiveSendState.Stopped, ReplaySendProblemKind.TokenRefused, "Server: token refused")]
    [DataRow(LiveSendState.Stopped, ReplaySendProblemKind.VersionNotSupported, "Server: does not support contract version 2")]
    [DataRow(LiveSendState.Stopped, ReplaySendProblemKind.LocalServerNotRunning, "Server: not connected")]
    [DataRow(LiveSendState.Stopped, null, "Server: not connected")]
    [DataRow(LiveSendState.Retrying, null, "Server: not connected")]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.TokenFileHoldsNoValidToken, "Server: not connected")]
    public void TrayMenuFormatLiveSendFormatsEveryState(LiveSendState state, ReplaySendProblemKind? problem, string expected)
    {
        // Arrange
        var status = new LiveSendStatus
                     {
                         State = state,
                         Problem = problem
                     };

        // Act
        var text = TrayMenu.FormatLiveSend(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, $"The server line of {state} with {problem} should be its text");
    }

    /// <summary>
    /// In forward mode the greyed, unchecked server line is directly below the catalog entry with the text of the live
    /// status, and the menu holds neither the local server line nor Open Censio (S1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    public void TrayMenuCreateWithLiveSendShowsServerLine()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var server = new TrayServerStatus
                     {
                         Live = new LiveSendStatus
                                {
                                    State = LiveSendState.Connected
                                }
                     };
        var expectedKinds = new[]
                            {
                                TrayMenuEntryKind.Status,
                                TrayMenuEntryKind.LastTick,
                                TrayMenuEntryKind.Game,
                                TrayMenuEntryKind.Catalog,
                                TrayMenuEntryKind.ServerConnection,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.OpenSettings,
                                TrayMenuEntryKind.OpenDiagnostics,
                                TrayMenuEntryKind.ToggleRecording,
                                TrayMenuEntryKind.OpenRecordingsFolder,
                                TrayMenuEntryKind.OpenLogFolder,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.ToggleAutostart,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Version,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Exit
                            };

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = server,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var serverLine = entries.Single(entry => entry.Kind == TrayMenuEntryKind.ServerConnection);

        Assert.AreSequenceEqual(expectedKinds, entries.Select(entry => entry.Kind).ToArray(), "The server line should be below the catalog, without the local server line or Open Censio");
        Assert.AreEqual("Server: connected", serverLine.Text, "The server line should show the text of the live status");
        Assert.IsFalse(serverLine.IsEnabled, "The server line should be greyed out");
        Assert.IsFalse(serverLine.IsChecked, "The server line should not be checked");
    }

    /// <summary>
    /// In local mode, whatever the state of live sending into the hosted server, the menu keeps the local server line with
    /// its text and the enabled Open Censio, and shows no line of forward mode (S5, E10).
    /// </summary>
    /// <param name="state">The state of live sending</param>
    /// <param name="problem">The problem, or <see langword="null" /> when there is none</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(LiveSendState.Connecting, null)]
    [DataRow(LiveSendState.Connected, null)]
    [DataRow(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost)]
    [DataRow(LiveSendState.Stopped, ReplaySendProblemKind.LocalServerNotRunning)]
    public void TrayMenuCreateInLocalModeKeepsLocalServerLine(LiveSendState state, ReplaySendProblemKind? problem)
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var server = new TrayServerStatus
                     {
                         Local = CreateLocalServerStatus(LocalServerState.Running, 5117),
                         Live = new LiveSendStatus
                                {
                                    State = state,
                                    Problem = problem
                                }
                     };

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = server,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var serverLine = entries.Single(entry => entry.Kind == TrayMenuEntryKind.LocalServer);
        var openCensio = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenCensio);

        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Catalog) + 1, kinds.IndexOf(TrayMenuEntryKind.LocalServer), "The local server line should stay directly below the catalog entry");
        Assert.AreEqual("Server: running at http://localhost:5117/", serverLine.Text, $"The local server line should keep its text while live sending is {state} with {problem}");
        Assert.IsTrue(openCensio.IsEnabled, "Open Censio should stay enabled while the hosted server runs");
        Assert.DoesNotContain(TrayMenuEntryKind.ServerConnection, kinds, "Local mode should show no server line of forward mode");
        Assert.ContainsSingle(entry => entry.Text.StartsWith("Server:", StringComparison.Ordinal), entries, "The menu should hold exactly one server text");
    }

    /// <summary>
    /// The buffer line reads the waiting count, with the dropped count once messages were dropped and the note on memory
    /// only after a failed write, in digits without separators (S1, S3, E2).
    /// </summary>
    /// <param name="waiting">The number of waiting messages</param>
    /// <param name="dropped">The number of dropped messages</param>
    /// <param name="isMemoryOnly">Whether the buffer keeps messages in memory only</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(0, 0, false, "Buffer: 0 waiting", DisplayName = "Empty")]
    [DataRow(1234, 0, false, "Buffer: 1234 waiting", DisplayName = "Waiting")]
    [DataRow(0, 3, false, "Buffer: 0 waiting, 3 dropped", DisplayName = "Dropped")]
    [DataRow(5, 1, false, "Buffer: 5 waiting, 1 dropped", DisplayName = "Waiting and dropped")]
    [DataRow(2, 0, true, "Buffer: 2 waiting, not saved to disk", DisplayName = "Memory only")]
    [DataRow(1234, 3, true, "Buffer: 1234 waiting, 3 dropped, not saved to disk", DisplayName = "Dropped and memory only")]
    public void TrayMenuFormatLiveBufferTexts(int waiting, int dropped, bool isMemoryOnly, string expected)
    {
        // Arrange
        var status = new LiveBufferStatus
                     {
                         Waiting = waiting,
                         Dropped = dropped,
                         IsMemoryOnly = isMemoryOnly
                     };

        // Act
        var text = TrayMenu.FormatLiveBuffer(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, $"The buffer line of {waiting} waiting, {dropped} dropped, memory only {isMemoryOnly} should be its text");
    }

    /// <summary>
    /// Where live sending runs, in forward and in local mode, one greyed, unchecked buffer line follows directly below the
    /// server line with the text of the buffer's status (S1, S6).
    /// </summary>
    /// <param name="mode">The mode: forward or local</param>
    /// <param name="serverKind">The kind of the server line of the mode</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("forward", TrayMenuEntryKind.ServerConnection)]
    [DataRow("local", TrayMenuEntryKind.LocalServer)]
    public void TrayMenuCreateShowsBufferLineBelowServerLine(string mode, TrayMenuEntryKind serverKind)
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var server = CreateServerStatus(mode) with
                                              {
                                                  Buffer = new LiveBufferStatus
                                                           {
                                                               Waiting = 3,
                                                               Dropped = 1,
                                                               IsMemoryOnly = false
                                                           }
                                              };

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = server,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var bufferLine = entries.Single(entry => entry.Kind == TrayMenuEntryKind.LiveBuffer);

        Assert.AreEqual(kinds.IndexOf(serverKind) + 1, kinds.IndexOf(TrayMenuEntryKind.LiveBuffer), $"The buffer line should be directly below the server line in {mode} mode");
        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Separator) - 1, kinds.IndexOf(TrayMenuEntryKind.LiveBuffer), $"The buffer line should be directly above the first separator in {mode} mode");
        Assert.AreEqual("Buffer: 3 waiting, 1 dropped", bufferLine.Text, $"The buffer line should show the text of the buffer's status in {mode} mode");
        Assert.IsFalse(bufferLine.IsEnabled, "The buffer line should be greyed out");
        Assert.IsFalse(bufferLine.IsChecked, "The buffer line should not be checked");
        Assert.ContainsSingle(entry => entry.Text.StartsWith("Buffer:", StringComparison.Ordinal), entries, "The menu should hold exactly one buffer text");
    }

    /// <summary>
    /// Without a buffer status, that is without live sending (no server setting) or with a server status that carries no
    /// buffer, the menu shows no buffer line (S8).
    /// </summary>
    /// <param name="mode">The mode: none, forward or local</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("none")]
    [DataRow("forward")]
    [DataRow("local")]
    public void TrayMenuCreateWithoutBufferStatusShowsNoBufferLine(string mode)
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        TrayServerStatus? server = mode == "none" ? null : CreateServerStatus(mode);

        // Act
        var entries = TrayMenu.Create(CompanionTexts.English,
                                      new TrayMenuState
                                      {
                                          Version = "1.2.3",
                                          Snapshot = snapshot,
                                          Game = GameInstallationStatus.Searching,
                                          Catalog = GameCatalogStatus.Reading,
                                          Server = server,
                                          IsAutostartEnabled = false,
                                          Clock = CreateClock(1)
                                      });

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();

        Assert.DoesNotContain(TrayMenuEntryKind.LiveBuffer, kinds, $"The menu should hold no buffer entry without a buffer status in mode {mode}");
        Assert.DoesNotContain(entry => entry.Text.StartsWith("Buffer:", StringComparison.Ordinal), entries, $"The menu should hold no buffer text without a buffer status in mode {mode}");
    }

    /// <summary>
    /// A replay that ends because the local server does not run names the reason in the tooltip and the reason text (E10).
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void TrayMenuFormatReplaySendReasonLocalServerNotRunningReturnsText()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         SendProblem = ReplaySendProblemKind.LocalServerNotRunning
                     };

        // Act
        var reason = TrayMenu.FormatReplaySendReason(CompanionTexts.English, ReplaySendProblemKind.LocalServerNotRunning);

        // Assert
        Assert.AreEqual("local server not running", reason, "The reason should read local server not running");
        Assert.AreEqual("Censio Companion: Replay error: local server not running", TrayMenu.FormatReplayToolTip(CompanionTexts.English, status), "The tooltip should name the reason");
    }

    /// <summary>
    /// A found installation shows its build and its source.
    /// </summary>
    /// <param name="source">The source</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    [DataRow(GameInstallationSource.UbisoftConnect, "Game: build 26.29905.4.51797 (Ubisoft Connect)", DisplayName = "Ubisoft Connect")]
    [DataRow(GameInstallationSource.SettingsFile, "Game: build 26.29905.4.51797 (settings file)", DisplayName = "Settings file")]
    public void TrayMenuFormatGameFoundShowsBuildAndSource(GameInstallationSource source, string expected)
    {
        // Arrange
        var status = CreateGameStatus(GameInstallationStatusKind.Found, source, "26.29905.4.51797");

        // Act
        var text = TrayMenu.FormatGame(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, "A found installation should show its build and source, not its folder");
    }

    /// <summary>
    /// A wrong game folder or settings file shows the fixed text of its case.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotFound, "Game: folder in settings not found", DisplayName = "E1")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotFullPath, "Game: folder in settings is not a full path", DisplayName = "E2")]
    [DataRow(GameInstallationStatusKind.SettingsFolderNotInstallation, "Game: folder in settings is not an Anno 117 installation", DisplayName = "E3")]
    [DataRow(GameInstallationStatusKind.SettingsFileInvalid, "Game: settings file invalid", DisplayName = "E4")]
    public void TrayMenuFormatGameProblemShowsText(GameInstallationStatusKind kind, string expected)
    {
        // Arrange
        var status = CreateGameStatus(kind, null, null);

        // Act
        var text = TrayMenu.FormatGame(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, "The problem should show the fixed text of its case");
    }

    /// <summary>
    /// Nothing found shows not found.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void TrayMenuFormatGameNotFoundShowsText()
    {
        // Arrange
        var status = CreateGameStatus(GameInstallationStatusKind.NotFound, null, null);

        // Act
        var text = TrayMenu.FormatGame(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual("Game: not found", text, "Nothing found should show not found");
    }

    /// <summary>
    /// A found installation without a build shows the build as unknown with its source.
    /// </summary>
    /// <param name="source">The source</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameInstallationSource.UbisoftConnect, "Game: build unknown (Ubisoft Connect)", DisplayName = "Ubisoft Connect")]
    [DataRow(GameInstallationSource.SettingsFile, "Game: build unknown (settings file)", DisplayName = "Settings file")]
    public void TrayMenuFormatGameUnknownBuildShowsText(GameInstallationSource source, string expected)
    {
        // Arrange
        var status = CreateGameStatus(GameInstallationStatusKind.Found, source, null);

        // Act
        var text = TrayMenu.FormatGame(CompanionTexts.English, status);

        // Assert
        Assert.AreEqual(expected, text, "A found installation without a build should show the build as unknown");
    }

    /// <summary>
    /// A search that has not ended shows searching.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void TrayMenuFormatGameSearchingShowsText()
    {
        // Act
        var text = TrayMenu.FormatGame(CompanionTexts.English, GameInstallationStatus.Searching);

        // Assert
        Assert.AreEqual("Game: searching", text, "A running search should show searching");
    }

    /// <summary>
    /// Creates a game installation status; a found one carries a folder, which the menu must not show.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="source">The source of a found installation</param>
    /// <param name="build">The build of a found installation</param>
    /// <returns>The status</returns>
    private static GameInstallationStatus CreateGameStatus(GameInstallationStatusKind kind, GameInstallationSource? source, string? build)
    {
        return new GameInstallationStatus
               {
                   Kind = kind,
                   Source = source,
                   Folder = source is null ? null : "E:/Anno 117 - Pax Romana/",
                   Build = build
               };
    }

    /// <summary>
    /// Creates a clock at <see cref="_now" /> in a local time zone with a fixed offset.
    /// </summary>
    /// <param name="offsetHours">The offset of the local time zone, in hours</param>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock(int offsetHours)
    {
        return CreateClock(offsetHours, _now);
    }

    /// <summary>
    /// Creates a clock at a time in a local time zone with a fixed offset.
    /// </summary>
    /// <param name="offsetHours">The offset of the local time zone, in hours</param>
    /// <param name="utcNow">The current time of the clock</param>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock(int offsetHours, DateTimeOffset utcNow)
    {
        var clock = new FakeTimeProvider(utcNow);

        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Censio Test", TimeSpan.FromHours(offsetHours), "Censio Test", "Censio Test"));

        return clock;
    }

    /// <summary>
    /// Creates the summary of a tick.
    /// </summary>
    /// <param name="receiveTime">The receive time, in UTC, as an ISO 8601 text</param>
    /// <param name="islandCount">The number of islands</param>
    /// <returns>The summary</returns>
    private static PipeTickSummary CreateTick(string receiveTime, int islandCount)
    {
        return new PipeTickSummary
               {
                   ReceiveTime = DateTimeOffset.Parse(receiveTime, CultureInfo.InvariantCulture),
                   IslandCount = islandCount
               };
    }

    /// <summary>
    /// Creates a snapshot with <see cref="_todayTick" /> as its last tick.
    /// </summary>
    /// <param name="statusText">The status text</param>
    /// <param name="canRecord">Whether the recording can be started or stopped</param>
    /// <param name="isRecording">Whether the recording is running</param>
    /// <returns>The snapshot</returns>
    private static TraySnapshot CreateSnapshot(string statusText, bool canRecord, bool isRecording)
    {
        return new TraySnapshot
               {
                   StatusText = statusText,
                   Look = TrayIconLook.Receiving,
                   LastTick = _todayTick,
                   CanRecord = canRecord,
                   IsRecording = isRecording
               };
    }

    /// <summary>
    /// Creates the snapshot of a mode of the Companion.
    /// </summary>
    /// <param name="mode">The mode: live, recording, replay or invalid</param>
    /// <returns>The snapshot</returns>
    private static TraySnapshot CreateSnapshotForMode(string mode)
    {
        return mode switch
               {
                   "live" => TraySnapshot.ForConnection(CompanionTexts.English,
                                                        new PipeConnectionStatus
                                                        {
                                                            State = PipeConnectionState.Receiving
                                                        },
                                                        PipeRecordingState.Off,
                                                        _todayTick),
                   "recording" => TraySnapshot.ForConnection(CompanionTexts.English,
                                                             new PipeConnectionStatus
                                                             {
                                                                 State = PipeConnectionState.Receiving
                                                             },
                                                             PipeRecordingState.Recording,
                                                             _todayTick),
                   "replay" => TraySnapshot.ForReplay(CompanionTexts.English,
                                                      new PipeReplayStatus
                                                      {
                                                          State = PipeConnectionState.Receiving
                                                      },
                                                      _todayTick),
                   _ => TraySnapshot.ForInvalidCommandLine(CompanionTexts.English)
               };
    }

    /// <summary>
    /// Creates a status of the local server.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="port">The port</param>
    /// <returns>The status</returns>
    private static LocalServerStatus CreateLocalServerStatus(LocalServerState state, int port)
    {
        return new LocalServerStatus
               {
                   State = state,
                   Port = port
               };
    }

    /// <summary>
    /// Creates the server status of a mode with live sending: connected in forward mode, a running hosted server in local
    /// mode.
    /// </summary>
    /// <param name="mode">The mode: forward or local</param>
    /// <returns>The server status</returns>
    private static TrayServerStatus CreateServerStatus(string mode)
    {
        var live = new LiveSendStatus
                   {
                       State = LiveSendState.Connected
                   };

        return mode == "local"
                   ? new TrayServerStatus
                     {
                         Local = CreateLocalServerStatus(LocalServerState.Running, 5117),
                         Live = live
                     }
                   : new TrayServerStatus
                     {
                         Live = live
                     };
    }

    /// <summary>
    /// Creates the server status of a server mode, or none.
    /// </summary>
    /// <param name="mode">The server mode: none, local or forward</param>
    /// <returns>The server status, or <see langword="null" /> for none</returns>
    private static TrayServerStatus? CreateServerStatusOrNone(string mode)
    {
        return mode == "none" ? null : CreateServerStatus(mode);
    }

    /// <summary>
    /// Creates the state of a menu that holds every entry of a server mode: a recording live connection with a tick, a
    /// found game, a ready catalog, the server line, a buffer line with dropped messages kept in memory only, and the
    /// autostart on.
    /// </summary>
    /// <param name="texts">The texts of the snapshot</param>
    /// <param name="mode">The server mode: local or forward</param>
    /// <returns>The state</returns>
    private static TrayMenuState CreateFullState(CompanionTexts texts, string mode)
    {
        var status = new PipeConnectionStatus
                     {
                         State = PipeConnectionState.Receiving
                     };
        var buffer = new LiveBufferStatus
                     {
                         Waiting = 3,
                         Dropped = 2,
                         IsMemoryOnly = true
                     };

        return new TrayMenuState
               {
                   Version = "1.2.3",
                   Snapshot = TraySnapshot.ForConnection(texts, status, PipeRecordingState.Recording, _todayTick),
                   Game = CreateGameStatus(GameInstallationStatusKind.Found, GameInstallationSource.UbisoftConnect, "12345"),
                   Catalog = new GameCatalogStatus
                             {
                                 Kind = GameCatalogStatusKind.Ready,
                                 EntryCount = 2
                             },
                   Server = CreateServerStatus(mode) with
                                                     {
                                                         Buffer = buffer
                                                     },
                   IsAutostartEnabled = true,
                   Clock = CreateClock(1)
               };
    }

    /// <summary>
    /// Gets the texts of the entries that read the same in two menus of the same entries, separators and the version
    /// entry left out.
    /// </summary>
    /// <param name="english">The entries of the English menu</param>
    /// <param name="german">The entries of the German menu</param>
    /// <returns>The English texts that the German menu repeats</returns>
    private static List<string> SameTexts(IReadOnlyList<TrayMenuEntry> english, IReadOnlyList<TrayMenuEntry> german)
    {
        return english.Zip(german)
                      .Where(pair => pair.First.Kind is not (TrayMenuEntryKind.Separator or TrayMenuEntryKind.Version))
                      .Where(pair => pair.First.Text == pair.Second.Text)
                      .Select(pair => pair.First.Text)
                      .ToList();
    }

    /// <summary>
    /// Creates a culture whose date and time separators differ from the invariant culture's.
    /// </summary>
    /// <returns>The culture</returns>
    private static CultureInfo CreateOddCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();

        culture.DateTimeFormat.DateSeparator = "/";
        culture.DateTimeFormat.TimeSeparator = ".";

        return culture;
    }

    #endregion // Methods
}