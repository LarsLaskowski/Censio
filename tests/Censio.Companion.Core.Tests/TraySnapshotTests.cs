using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="TraySnapshot" />.
/// </summary>
[TestClass]
public sealed class TraySnapshotTests
{
    #region Fields

    /// <summary>
    /// The summary of the last tick the snapshots carry.
    /// </summary>
    private static readonly PipeTickSummary _lastTick = new()
                                                        {
                                                            ReceiveTime = new DateTimeOffset(2026, 11, 2, 17, 26, 3, TimeSpan.Zero),
                                                            IslandCount = 3
                                                        };

    /// <summary>
    /// The notice pending in the tests of the look with a notice.
    /// </summary>
    private static readonly ProfileNotice _notice = new()
                                                    {
                                                        Kind = ProfileNoticeKind.NewFigure,
                                                        ProfileName = "MARKER-Profile"
                                                    };

    #endregion // Fields

    #region Methods

    /// <summary>
    /// The snapshot of a live connection shows the look of its state, the status text with the recording suffix, the last
    /// tick, and a toggle that can always be used.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <param name="expectedLook">The expected look</param>
    /// <param name="expectedStatusText">The expected status text</param>
    /// <param name="expectedIsRecording">Whether the snapshot should show a running recording</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Off, TrayIconLook.Waiting, "Waiting for game", false)]
    [DataRow(PipeConnectionState.Connected, PipeRecordingState.Recording, TrayIconLook.Waiting, "Connected (recording)", true)]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Off, TrayIconLook.Receiving, "Receiving", false)]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Recording, TrayIconLook.Receiving, "Receiving (recording)", true)]
    [DataRow(PipeConnectionState.PipeBusy, PipeRecordingState.Failed, TrayIconLook.Waiting, "Pipe busy (recording failed)", false)]
    [DataRow(PipeConnectionState.ProtocolMismatch, PipeRecordingState.Recording, TrayIconLook.Problem, "Protocol mismatch (recording)", true)]
    [DataRow(PipeConnectionState.Error, PipeRecordingState.Failed, TrayIconLook.Problem, "Error (recording failed)", false)]
    public void TraySnapshotForConnectionMapsLook(PipeConnectionState state, PipeRecordingState recording, TrayIconLook expectedLook, string expectedStatusText, bool expectedIsRecording)
    {
        // Arrange
        var status = new PipeConnectionStatus
                     {
                         State = state
                     };
        var expected = new TraySnapshot
                       {
                           StatusText = expectedStatusText,
                           Look = expectedLook,
                           LastTick = _lastTick,
                           CanRecord = true,
                           IsRecording = expectedIsRecording
                       };

        // Act
        var snapshot = TraySnapshot.ForConnection(CompanionTexts.English, status, recording, _lastTick);

        // Assert
        Assert.AreEqual(expectedLook, snapshot.Look, "The look should follow the connection state");
        Assert.AreEqual(expected, snapshot, "The snapshot should carry the status text, the last tick, an enabled toggle and the recording flag");
    }

    /// <summary>
    /// The snapshot of a replay shows the look of the replayed state, waiting when it finished and problem when it ended with a
    /// problem; its toggle cannot be used.
    /// </summary>
    /// <param name="state">The state of the replayed connection</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <param name="problemKind">The kind of the problem that ended the replay, if any</param>
    /// <param name="expectedLook">The expected look</param>
    /// <param name="expectedStatusText">The expected status text</param>
    [TestMethod]
    [DataRow(PipeConnectionState.Connected, false, null, TrayIconLook.Waiting, "Replay: Connected")]
    [DataRow(PipeConnectionState.Receiving, false, null, TrayIconLook.Receiving, "Replay: Receiving")]
    [DataRow(PipeConnectionState.ProtocolMismatch, false, null, TrayIconLook.Problem, "Replay: Protocol mismatch")]
    [DataRow(PipeConnectionState.Receiving, true, null, TrayIconLook.Waiting, "Replay finished")]
    [DataRow(PipeConnectionState.ProtocolMismatch, true, null, TrayIconLook.Waiting, "Replay finished")]
    [DataRow(PipeConnectionState.Receiving, true, PipeReplayProblemKind.ReadFailed, TrayIconLook.Problem, "Replay error: read failed")]
    [DataRow(PipeConnectionState.Connected, false, PipeReplayProblemKind.FileNotFound, TrayIconLook.Problem, "Replay error: file not found")]
    public void TraySnapshotForReplayMapsLook(PipeConnectionState state, bool isFinished, PipeReplayProblemKind? problemKind, TrayIconLook expectedLook, string expectedStatusText)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = state,
                         IsFinished = isFinished,
                         Problem = problemKind is { } kind
                                       ? new PipeReplayProblem
                                         {
                                             Kind = kind
                                         }
                                       : null
                     };
        var expected = new TraySnapshot
                       {
                           StatusText = expectedStatusText,
                           Look = expectedLook,
                           LastTick = _lastTick,
                           CanRecord = false,
                           IsRecording = false
                       };

        // Act
        var snapshot = TraySnapshot.ForReplay(CompanionTexts.English, status, _lastTick);

        // Assert
        Assert.AreEqual(expectedLook, snapshot.Look, "The look should follow the replayed state");
        Assert.AreEqual(expected, snapshot, "The snapshot should carry the replay status text, the last tick and a toggle that cannot be used");
    }

    /// <summary>
    /// The snapshot of a replay sent to a server shows the problem look for a send problem and the waiting look while it
    /// connects and when it finished with its counts.
    /// </summary>
    /// <param name="isConnecting">Whether the replay connects</param>
    /// <param name="sendProblem">The send problem, if any</param>
    /// <param name="isSent">Whether the replay finished with counts</param>
    /// <param name="expectedLook">The expected look</param>
    /// <param name="expectedStatusText">The expected status text</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false, ReplaySendProblemKind.ServerUnreachable, false, TrayIconLook.Problem, "Replay error: server unreachable")]
    [DataRow(true, null, false, TrayIconLook.Waiting, "Replay: Connecting to server")]
    [DataRow(false, null, true, TrayIconLook.Waiting, "Replay finished: 31 ticks sent, 1 refused")]
    public void TraySnapshotForReplaySendMapsLook(bool isConnecting, ReplaySendProblemKind? sendProblem, bool isSent, TrayIconLook expectedLook, string expectedStatusText)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Connected,
                         IsConnecting = isConnecting,
                         IsFinished = sendProblem is not null || isSent,
                         SendProblem = sendProblem,
                         Sent = isSent
                                    ? new ReplaySendCounts
                                      {
                                          Ticks = 31,
                                          Refused = 1
                                      }
                                    : null
                     };

        // Act
        var snapshot = TraySnapshot.ForReplay(CompanionTexts.English, status, _lastTick);

        // Assert
        Assert.AreEqual(expectedLook, snapshot.Look, "The look should follow the sending");
        Assert.AreEqual(expectedStatusText, snapshot.StatusText, "The status text should be the one of the tooltip");
    }

    /// <summary>
    /// The snapshot of an invalid command line shows the problem look, no last tick and a toggle that cannot be used.
    /// </summary>
    [TestMethod]
    public void TraySnapshotForInvalidCommandLineIsProblem()
    {
        // Arrange
        var expected = new TraySnapshot
                       {
                           StatusText = "Invalid command line",
                           Look = TrayIconLook.Problem,
                           LastTick = null,
                           CanRecord = false,
                           IsRecording = false
                       };

        // Act
        var snapshot = TraySnapshot.ForInvalidCommandLine(CompanionTexts.English);

        // Assert
        Assert.AreEqual(expected, snapshot, "The snapshot should show the problem look, the status text, no last tick and no recording");
    }

    /// <summary>
    /// The snapshot of a live connection takes its status text, with the recording suffix, from the texts it is given, so
    /// a language change shows at once in the next snapshot (S9).
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void TraySnapshotForConnectionUsesGivenTexts()
    {
        // Arrange
        var status = new PipeConnectionStatus
                     {
                         State = PipeConnectionState.Receiving
                     };
        var english = TraySnapshot.ForConnection(CompanionTexts.English, status, PipeRecordingState.Recording, _lastTick);
        var expectedText = TrayMenu.FormatStatusText(CompanionTexts.German, PipeConnectionState.Receiving, PipeRecordingState.Recording);

        // Act
        var snapshot = TraySnapshot.ForConnection(CompanionTexts.German, status, PipeRecordingState.Recording, _lastTick);

        // Assert
        Assert.AreEqual(expectedText, snapshot.StatusText, "The status text should be the one of the given German texts");
        Assert.AreNotEqual(english.StatusText, snapshot.StatusText, "The German status text should differ from the English one");
        Assert.AreEqual(english.Look, snapshot.Look, "The language should not change the look");
        Assert.AreEqual(english.IsRecording, snapshot.IsRecording, "The language should not change the recording flag");
    }

    /// <summary>
    /// While a notice is pending the look of a waiting or receiving state, a finished replay included, becomes the notice
    /// look; the snapshot keeps the look of its state (S13).
    /// </summary>
    /// <param name="scenario">The scenario of the snapshot</param>
    /// <param name="expectedStateLook">The look of the state</param>
    [TestMethod]
    [TestCategory("AC14")]
    [DataRow("waiting", TrayIconLook.Waiting, DisplayName = "Waiting")]
    [DataRow("receiving", TrayIconLook.Receiving, DisplayName = "Receiving")]
    [DataRow("replay finished", TrayIconLook.Waiting, DisplayName = "Replay finished")]
    public void TraySnapshotLookWithNoticeShowsNotice(string scenario, TrayIconLook expectedStateLook)
    {
        // Arrange
        var snapshot = SnapshotOf(scenario);

        // Act
        var look = snapshot.LookWith(_notice);

        // Assert
        Assert.AreEqual(TrayIconLook.Notice, look, $"A pending notice should show the notice look: {scenario}");
        Assert.AreEqual(expectedStateLook, snapshot.Look, $"The snapshot should keep the look of its state: {scenario}");
    }

    /// <summary>
    /// While a notice is pending a problem keeps the problem look: of the connection, of a replay and of an invalid
    /// command line (S13).
    /// </summary>
    /// <param name="scenario">The scenario of the snapshot</param>
    [TestMethod]
    [TestCategory("AC14")]
    [DataRow("protocol mismatch", DisplayName = "Protocol mismatch")]
    [DataRow("error", DisplayName = "Error")]
    [DataRow("replay problem", DisplayName = "Replay problem")]
    [DataRow("invalid command line", DisplayName = "Invalid command line")]
    public void TraySnapshotLookWithNoticeKeepsProblem(string scenario)
    {
        // Arrange
        var snapshot = SnapshotOf(scenario);

        // Act
        var look = snapshot.LookWith(_notice);

        // Assert
        Assert.AreEqual(TrayIconLook.Problem, snapshot.Look, $"The snapshot should show a problem: {scenario}");
        Assert.AreEqual(TrayIconLook.Problem, look, $"A problem should win over a pending notice: {scenario}");
    }

    /// <summary>
    /// Without a pending notice each of the three looks of a state stays (S13).
    /// </summary>
    /// <param name="scenario">The scenario of the snapshot</param>
    /// <param name="expected">The look of the state</param>
    [TestMethod]
    [TestCategory("AC14")]
    [DataRow("waiting", TrayIconLook.Waiting, DisplayName = "Waiting")]
    [DataRow("receiving", TrayIconLook.Receiving, DisplayName = "Receiving")]
    [DataRow("error", TrayIconLook.Problem, DisplayName = "Problem")]
    public void TraySnapshotLookWithoutNoticeKeepsLook(string scenario, TrayIconLook expected)
    {
        // Arrange
        var snapshot = SnapshotOf(scenario);

        // Act
        var look = snapshot.LookWith(null);

        // Assert
        Assert.AreEqual(expected, look, $"Without a notice the look of the state should stay: {scenario}");
    }

    /// <summary>
    /// Creates the snapshot of a scenario: a live connection waiting, receiving, with a protocol mismatch or an error, a
    /// finished replay, a replay with a problem or an invalid command line.
    /// </summary>
    /// <param name="scenario">The scenario</param>
    /// <returns>The snapshot</returns>
    private static TraySnapshot SnapshotOf(string scenario)
    {
        return scenario switch
               {
                   "receiving" => Connection(PipeConnectionState.Receiving),
                   "protocol mismatch" => Connection(PipeConnectionState.ProtocolMismatch),
                   "error" => Connection(PipeConnectionState.Error),
                   "replay finished" => TraySnapshot.ForReplay(CompanionTexts.English,
                                                               new PipeReplayStatus
                                                               {
                                                                   State = PipeConnectionState.Receiving,
                                                                   IsFinished = true
                                                               },
                                                               _lastTick),
                   "replay problem" => TraySnapshot.ForReplay(CompanionTexts.English,
                                                              new PipeReplayStatus
                                                              {
                                                                  State = PipeConnectionState.Receiving,
                                                                  Problem = new PipeReplayProblem
                                                                            {
                                                                                Kind = PipeReplayProblemKind.ReadFailed
                                                                            }
                                                              },
                                                              _lastTick),
                   "invalid command line" => TraySnapshot.ForInvalidCommandLine(CompanionTexts.English),
                   _ => Connection(PipeConnectionState.WaitingForGame)
               };
    }

    /// <summary>
    /// Creates the snapshot of a live connection in a state, without a recording.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The snapshot</returns>
    private static TraySnapshot Connection(PipeConnectionState state)
    {
        return TraySnapshot.ForConnection(CompanionTexts.English,
                                          new PipeConnectionStatus
                                          {
                                              State = state
                                          },
                                          PipeRecordingState.Off,
                                          _lastTick);
    }

    #endregion // Methods
}