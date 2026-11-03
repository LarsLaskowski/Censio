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
        var snapshot = TraySnapshot.ForConnection(status, recording, _lastTick);

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
        var snapshot = TraySnapshot.ForReplay(status, _lastTick);

        // Assert
        Assert.AreEqual(expectedLook, snapshot.Look, "The look should follow the replayed state");
        Assert.AreEqual(expected, snapshot, "The snapshot should carry the replay status text, the last tick and a toggle that cannot be used");
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
        var snapshot = TraySnapshot.ForInvalidCommandLine();

        // Assert
        Assert.AreEqual(expected, snapshot, "The snapshot should show the problem look, the status text, no last tick and no recording");
    }

    #endregion // Methods
}