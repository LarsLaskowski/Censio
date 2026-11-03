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
    /// The menu lists status, last tick, separator, open diagnostics, toggle, both folder entries, separator, version,
    /// separator and exit, in this order, with the status and last-tick entries greyed out.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateListsEntriesInOrder()
    {
        // Arrange
        var snapshot = CreateSnapshot("Receiving", canRecord: true, isRecording: false);
        var expectedKinds = new[]
                            {
                                TrayMenuEntryKind.Status,
                                TrayMenuEntryKind.LastTick,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.OpenDiagnostics,
                                TrayMenuEntryKind.ToggleRecording,
                                TrayMenuEntryKind.OpenRecordingsFolder,
                                TrayMenuEntryKind.OpenLogFolder,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Version,
                                TrayMenuEntryKind.Separator,
                                TrayMenuEntryKind.Exit
                            };
        var expectedTexts = new[]
                            {
                                "Status: Receiving",
                                "Last tick: 00:30:00, 3 islands",
                                string.Empty,
                                "Open diagnostics",
                                "Start recording",
                                "Open recordings folder",
                                "Open log folder",
                                string.Empty,
                                "Version 1.2.3",
                                string.Empty,
                                "Exit"
                            };
        var expectedEnabled = new[] { false, false, false, true, true, true, true, false, false, false, true };

        // Act
        var entries = TrayMenu.Create("1.2.3", snapshot, CreateClock(1));

        // Assert
        Assert.AreSequenceEqual(expectedKinds, entries.Select(entry => entry.Kind).ToArray(), "The entries should be in the order of the menu");
        Assert.AreSequenceEqual(expectedTexts, entries.Select(entry => entry.Text).ToArray(), "The entries should carry the status, the last tick, open diagnostics, the toggle, the folders, the version and Exit");
        Assert.AreSequenceEqual(expectedEnabled, entries.Select(entry => entry.IsEnabled).ToArray(), "Status, last tick, version and separators should be greyed out, the rest enabled");
    }

    /// <summary>
    /// The open-diagnostics entry is enabled and directly after the first separator live, while recording, in a replay and
    /// with an invalid command line.
    /// </summary>
    /// <param name="mode">The mode of the Companion: live, recording, replay or invalid</param>
    [TestMethod]
    [DataRow("live")]
    [DataRow("recording")]
    [DataRow("replay")]
    [DataRow("invalid")]
    public void TrayMenuCreateOpenDiagnosticsEnabledInEveryMode(string mode)
    {
        // Arrange
        var snapshot = CreateSnapshotForMode(mode);

        // Act
        var entries = TrayMenu.Create("1.2.3", snapshot, CreateClock(1));

        // Assert
        var kinds = entries.Select(entry => entry.Kind).ToList();
        var diagnostics = entries.Single(entry => entry.Kind == TrayMenuEntryKind.OpenDiagnostics);

        Assert.AreEqual(kinds.IndexOf(TrayMenuEntryKind.Separator) + 1, kinds.IndexOf(TrayMenuEntryKind.OpenDiagnostics), "The entry should be directly after the first separator");
        Assert.AreEqual("Open diagnostics", diagnostics.Text, "The entry should read Open diagnostics");
        Assert.IsTrue(diagnostics.IsEnabled, "The entry should be enabled in every mode");
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
        var entries = TrayMenu.Create("1.2.3", snapshot, CreateClock(1));

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
        var entries = TrayMenu.Create("1.2.3", snapshot, CreateClock(1));

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
        var snapshot = TraySnapshot.ForInvalidCommandLine();

        // Act
        var entries = TrayMenu.Create("1.2.3", snapshot, CreateClock(1));

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
        var entries = TrayMenu.Create("1.2.3", snapshot, CreateClock(1));

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
        var entries = TrayMenu.Create(version, CreateSnapshot("Receiving", canRecord: true, isRecording: false), CreateClock(1));

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
        var entries = TrayMenu.Create(version, CreateSnapshot("Receiving", canRecord: true, isRecording: false), CreateClock(1));

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
        var entries = TrayMenu.Create(version, CreateSnapshot("Receiving", canRecord: true, isRecording: false), CreateClock(1));

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
        var text = TrayMenu.FormatLastTick(tick, CreateClock(1));

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
        var text = TrayMenu.FormatLastTick(tick, CreateClock(1));

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
        var text = TrayMenu.FormatLastTick(tick, CreateClock(-1));

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
        var text = TrayMenu.FormatLastTick(tick, CreateClock(1));

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
        var text = TrayMenu.FormatLastTick(tick, CreateClock(1));

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
        var text = TrayMenu.FormatLastTick(tick, CreateClock(1));

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
        var toolTip = TrayMenu.FormatSnapshotToolTip(snapshot, CreateClock(1));

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
        var toolTip = TrayMenu.FormatSnapshotToolTip(snapshot, CreateClock(1));

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
        var snapshot = TraySnapshot.ForReplay(status, tick);

        // Act
        var toolTip = TrayMenu.FormatSnapshotToolTip(snapshot, CreateClock(-1));

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
        var toolTip = TrayMenu.FormatToolTip(state);

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
        var call = () => TrayMenu.FormatToolTip(state);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined state should be rejected");

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
        var toolTip = TrayMenu.FormatToolTip(state, recording);

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
        var toolTip = TrayMenu.FormatReplayToolTip(status);

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
        var toolTip = TrayMenu.FormatReplayToolTip(status);

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
        var toolTip = TrayMenu.FormatReplayToolTip(status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: invalid line 1234567", toolTip, "The line number should be written without grouping");
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
        // Arrange
        var call = () => TrayMenu.FormatToolTip(PipeConnectionState.Connected, (PipeRecordingState)99);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined recording state should be rejected");

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
        var call = () => TrayMenu.FormatReplayToolTip(status);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined problem kind should be rejected");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the parameter");
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
        var call = () => TrayMenu.FormatReplayToolTip(status);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "A state a replay cannot reach should be rejected");

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
                   "live" => TraySnapshot.ForConnection(new PipeConnectionStatus
                                                        {
                                                            State = PipeConnectionState.Receiving
                                                        },
                                                        PipeRecordingState.Off,
                                                        _todayTick),
                   "recording" => TraySnapshot.ForConnection(new PipeConnectionStatus
                                                             {
                                                                 State = PipeConnectionState.Receiving
                                                             },
                                                             PipeRecordingState.Recording,
                                                             _todayTick),
                   "replay" => TraySnapshot.ForReplay(new PipeReplayStatus
                                                      {
                                                          State = PipeConnectionState.Receiving
                                                      },
                                                      _todayTick),
                   _ => TraySnapshot.ForInvalidCommandLine()
               };
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