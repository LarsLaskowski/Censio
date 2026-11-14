using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Censio.Contracts;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeReplayRunner" />.
/// </summary>
[TestClass]
public sealed class PipeReplayRunnerTests
{
    #region Fields

    /// <summary>
    /// The receive time of the first frame of the recordings.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The messages of the replay sent in order, statuses left out.
    /// </summary>
    private static readonly string[] _replayInOrder = ["Hello", "SessionStart", "Tick 2", "Tick 1", "SessionEnd"];

    /// <summary>
    /// The replayed states sent as statuses.
    /// </summary>
    private static readonly ContractPipeState[] _replayedStates = [ContractPipeState.Connected, ContractPipeState.Receiving];

    /// <summary>
    /// The session events sent when a session end repeats.
    /// </summary>
    private static readonly string[] _sessionEventsOnce = ["SessionStart", "SessionEnd"];

    /// <summary>
    /// The statuses the runner raised, in order.
    /// </summary>
    private readonly ConcurrentQueue<PipeReplayStatus> _events = [];

    /// <summary>
    /// The temporary directories of the test.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayRunnerTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeReplayRunnerTests(TestContext testContext)
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
    /// Deletes the temporary directories.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Skipped frames keep the replay connected until a frame changes the state.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerConnectedUntilFirstStatus()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var recording = PipeTestFrames.Recording(_start, (_start, []), (_start.AddSeconds(10), PipeTestFrames.Version(2)));
        var runner = CreateRunner(recording, PipeReplaySpeed.RealTime, waits);
        var run = runner.RunAsync(TestContext.CancellationToken);

        await waits.WaitForTimersAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var beforeVersion = runner.Status;
        var eventsBeforeVersion = _events.Count;

        // Act
        await waits.RunWaitsAsync(run, TestContext.CancellationToken);
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.Connected, false), beforeVersion, "A skipped frame should leave the replay connected");
        Assert.AreEqual(0, eventsBeforeVersion, "A skipped frame should raise no event");
        Assert.AreEqual(PipeConnectionState.Receiving, _events.First().State, "The version frame should be the first change");
        Assert.AreEqual(Status(PipeConnectionState.Receiving, true), runner.Status, "The replay should end receiving and finished");
    }

    /// <summary>
    /// A first frame that is not a version shows a protocol mismatch, as live.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerFrameBeforeVersionShowsProtocolMismatch()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Island(1, 100)));
        var runner = CreateRunner(recording);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeConnectionState.ProtocolMismatch, _events.First().State, "A frame before the version should show a protocol mismatch at once");
        Assert.AreEqual(Status(PipeConnectionState.ProtocolMismatch, true), runner.Status, "The replay should end in the protocol mismatch");
    }

    /// <summary>
    /// The version 2 shows receiving.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerReceivingAfterVersion2()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)), (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)));
        var runner = CreateRunner(recording);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.Receiving, false), _events.First(), "The version 2 should show receiving");
        Assert.AreEqual(Status(PipeConnectionState.Receiving, true), runner.Status, "The replay should end receiving and finished");
    }

    /// <summary>
    /// Another version shows a protocol mismatch.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerProtocolMismatchAfterOtherVersion()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(3)), (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)));
        var runner = CreateRunner(recording);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.ProtocolMismatch, false), _events.First(), "The version 3 should show a protocol mismatch");
        Assert.AreEqual(Status(PipeConnectionState.ProtocolMismatch, true), runner.Status, "The replay should end in the protocol mismatch");
    }

    /// <summary>
    /// After the last frame the replay is finished, and the change is raised once.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerFinishedAfterLastFrame()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var runner = CreateRunner(recording);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Receiving, false), Status(PipeConnectionState.Receiving, true) }, _events.ToArray(), "The runner should raise receiving, then finished");
        Assert.IsNull(runner.Status.Problem, "A complete recording should end without a problem");
    }

    /// <summary>
    /// A recording with a header only is finished at once.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerHeaderOnlyRecordingIsFinishedAtOnce()
    {
        // Arrange
        var runner = CreateRunner(PipeTestFrames.Recording(_start));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.Connected, true), runner.Status, "A header-only recording should be connected and finished");
        Assert.IsNull(runner.Status.Problem, "A header-only recording is no problem");
    }

    /// <summary>
    /// A problem of the file ends the replay after the frames before it.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerProblemShownAfterEarlierFrames()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var runner = CreateRunner([.. recording, .. Encoding.UTF8.GetBytes("not json\n")]);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeConnectionState.Receiving, _events.First().State, "The frame before the problem should have been replayed");
        Assert.IsTrue(runner.Status.IsFinished, "A problem should end the replay");
        Assert.IsNotNull(runner.Status.Problem, "The problem should be on the status");
        Assert.AreEqual(PipeReplayProblemKind.InvalidLine, runner.Status.Problem.Kind, "The line should be invalid");
        Assert.AreEqual(3L, runner.Status.Problem.LineNumber, "The problem should name the line after the header and the frame");
    }

    /// <summary>
    /// A file that is no recording, or of another version, ends the replay with its problem.
    /// </summary>
    /// <param name="text">The content of the file</param>
    /// <param name="kind">The expected problem</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("hello\n", PipeReplayProblemKind.NotARecording)]
    [DataRow("", PipeReplayProblemKind.NotARecording)]
    [DataRow("{\"format\":\"censio-pipe-recording\",\"version\":2}\n", PipeReplayProblemKind.UnsupportedVersion)]
    public async Task PipeReplayRunnerFileProblemIsShown(string text, PipeReplayProblemKind kind)
    {
        // Arrange
        var runner = CreateRunner(Encoding.UTF8.GetBytes(text));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(runner.Status.IsFinished, "A file problem should end the replay");
        Assert.IsNotNull(runner.Status.Problem, "The problem should be on the status");
        Assert.AreEqual(kind, runner.Status.Problem.Kind, "The problem should be the one of the file");
    }

    /// <summary>
    /// Stopping the replay while it waits ends it without a status change.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerCancelledEndsWithoutEvent()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)), (_start.AddSeconds(10), PipeTestFrames.SessionEnd()));
        var runner = CreateRunner(recording, PipeReplaySpeed.RealTime, waits);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var run = runner.RunAsync(stop.Token);

        await waits.WaitForTimersAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var eventsBeforeStop = _events.Count;

        // Act
        await stop.CancelAsync();
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, eventsBeforeStop, "Only the version frame should have changed the status");
        Assert.HasCount(1, _events, "The stop should raise no event");
        Assert.IsFalse(runner.Status.IsFinished, "A stopped replay is not finished");
        Assert.IsNull(runner.Status.Problem, "A stopped replay is no problem");
    }

    /// <summary>
    /// A file that does not exist is a problem of the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerMissingFileIsFileNotFound()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"censio-missing-{Guid.NewGuid():N}.jsonl");
        var runner = CreateRunner(() => File.OpenRead(path));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        AssertFileNotFound(runner);
    }

    /// <summary>
    /// A file the user may not read is a problem of the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerDeniedFileIsFileNotFound()
    {
        // Arrange
        var runner = CreateRunner(() => throw new UnauthorizedAccessException("Access denied"));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        AssertFileNotFound(runner);
    }

    /// <summary>
    /// A path .NET refuses is a problem of the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerRefusedPathIsFileNotFound()
    {
        // Arrange
        var runner = CreateRunner(() => throw new ArgumentException("The path is not valid"));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        AssertFileNotFound(runner);
    }

    /// <summary>
    /// A folder that does not exist is a problem of the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerMissingFolderIsFileNotFound()
    {
        // Arrange
        var runner = CreateRunner(() => throw new DirectoryNotFoundException("The folder does not exist"));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        AssertFileNotFound(runner);
    }

    /// <summary>
    /// A recording that cannot be opened because the stream type is not supported is a problem of the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerNotSupportedOpenIsFileNotFound()
    {
        // Arrange
        var runner = CreateRunner(() => throw new NotSupportedException("The path format is not supported"));

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        AssertFileNotFound(runner);
    }

    /// <summary>
    /// A stream that cannot be read is not reported as a missing file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerNotSupportedReadIsNotFileNotFound()
    {
        // Arrange
        var stream = new ControlledPipeStream();

        stream.Fail(new NotSupportedException("The stream cannot be read"));

        var runner = CreateRunner(() => stream);

        // Act
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => runner.RunAsync(TestContext.CancellationToken), "A read failure of the stream should not be turned into file not found");

        // Assert
        Assert.IsFalse(runner.Status.IsFinished, "The replay should not report a problem of the file");
        Assert.IsNull(runner.Status.Problem, "No problem should be set");
    }

    /// <summary>
    /// A subscriber that throws does not stop the replay or the other subscribers.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerThrowingStatusChangedHandlerIsIgnored()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var runner = new PipeReplayRunner(() => new MemoryStream(recording), PipeReplaySpeed.Maximum, TimeProvider.System);

        runner.StatusChanged += (_, _) => throw new InvalidOperationException("The subscriber failed");
        runner.StatusChanged += (_, status) => _events.Enqueue(status);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.Receiving, true), runner.Status, "The replay should finish although a subscriber threw");
        Assert.HasCount(2, _events, "The other subscriber should get the receiving and the finished status");
    }

    /// <summary>
    /// A replay without a subscriber runs to its end.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerWithoutSubscriberFinishes()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var runner = new PipeReplayRunner(() => new MemoryStream(recording), PipeReplaySpeed.Maximum, TimeProvider.System);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.Receiving, true), runner.Status, "The replay should finish without a subscriber");
    }

    /// <summary>
    /// A replayed tick sets the last tick with its recorded receive time and raises the tick event.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerTickSetsLastTickWithRecordedTime()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(2, 100)),
                                                 (_start.AddSeconds(3), PipeTestFrames.SessionEnd()));
        var runner = CreateRunner(recording);
        var ticks = new ConcurrentQueue<PipeTick>();

        runner.TickReceived += (_, tick) => ticks.Enqueue(tick);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(new PipeTickSummary
                        {
                            ReceiveTime = _start.AddSeconds(2),
                            IslandCount = 2
                        },
                        runner.LastTick,
                        "The last tick should carry the recorded receive time of the latest tick");
        Assert.HasCount(1, ticks, "One tick should be raised");
        Assert.AreEqual(100L, ticks.Single().GameTime, "The raised tick should be the replayed one");
    }

    /// <summary>
    /// The replay keeps the latest closed tick whole: the last tick it raised, with its islands and entries.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerLatestTickHoldsLastTickWhole()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(2, 200)),
                                                 (_start.AddSeconds(3), PipeTestFrames.Island(3, 200)));
        var runner = CreateRunner(recording);
        var ticks = new ConcurrentQueue<PipeTick>();

        runner.TickReceived += (_, tick) => ticks.Enqueue(tick);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var latest = runner.LatestTick;
        int[] goods = [12, 13];

        Assert.HasCount(2, ticks, "Two ticks should be raised");
        Assert.AreSame(ticks.Last(), latest, "The latest tick should be the last closed tick itself");
        Assert.AreSequenceEqual(goods, latest!.Islands.Select(island => island.Entries.Single().ProductGuid).ToArray(), "The latest tick should hold the entries of its islands");
    }

    /// <summary>
    /// After a replay error the latest closed tick stays.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerLatestTickStaysAfterReplayError()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(1, 200)));
        var runner = CreateRunner([.. recording, .. Encoding.UTF8.GetBytes("not json\n")]);
        var ticks = new ConcurrentQueue<PipeTick>();

        runner.TickReceived += (_, tick) => ticks.Enqueue(tick);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(runner.Status.Problem, "The replay should end with its error");
        Assert.IsNotEmpty(ticks, "A tick should close before the error");
        Assert.AreSame(ticks.Last(), runner.LatestTick, "The latest tick should stay after the replay error");
    }

    /// <summary>
    /// A tick subscriber that throws neither stops the replay nor the other tick subscribers.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayRunnerThrowingTickHandlerIsIgnored()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(1, 200)),
                                                 (_start.AddSeconds(3), PipeTestFrames.SessionEnd()));
        var runner = CreateRunner(recording);
        var ticks = new ConcurrentQueue<PipeTick>();

        runner.TickReceived += (_, _) => throw new InvalidOperationException("The subscriber failed");
        runner.TickReceived += (_, tick) => ticks.Enqueue(tick);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        long[] expected = [100L, 200L];

        Assert.AreSequenceEqual(expected, ticks.Select(tick => tick.GameTime).ToArray(), "The other subscriber should get every tick");
        Assert.AreEqual(Status(PipeConnectionState.Receiving, true), runner.Status, "The replay should finish although a tick subscriber threw");
    }

    /// <summary>
    /// The end of a replay is logged, as finished or with its problem.
    /// </summary>
    /// <param name="tail">The text after the recorded frame</param>
    /// <param name="level">The expected level</param>
    /// <param name="message">The expected message</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("", LogLevel.Information, "Replay finished")]
    [DataRow("not json\n", LogLevel.Warning, "Replay error: invalid line 3")]
    public async Task PipeReplayRunnerLogsFinishAndProblem(string tail, LogLevel level, string message)
    {
        // Arrange
        var logger = new RecordingLogger();
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var runner = new PipeReplayRunner(() => new MemoryStream([.. recording, .. Encoding.UTF8.GetBytes(tail)]), PipeReplaySpeed.Maximum, TimeProvider.System, logger);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.HasCount(1, logger.Entries.Where(entry => entry.Message.StartsWith("Replay", StringComparison.Ordinal)), "The end of the replay should be logged once");
        Assert.AreEqual((level, message), (logger.Entries[^1].Level, logger.Entries[^1].Message), "The end of the replay should be logged last");
    }

    /// <summary>
    /// With a sender the replay reads the token file, checks the header, connects and only then replays: the hello comes
    /// first, the session events and ticks follow in replay order, the replayed states go as statuses, the recording is
    /// opened twice and the connection is closed with the counts on the finished status.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC9")]
    public async Task PipeReplayRunnerWithSenderSendsReplayInOrder()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.SessionStart("Player 1")),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(2, 100)),
                                                 (_start.AddSeconds(3), PipeTestFrames.Island(1, 200)),
                                                 (_start.AddSeconds(4), PipeTestFrames.SessionEnd()));
        await using var server = ScriptedIngestServer.Create();
        var connector = new DuplexReplayConnector(server.ClientSocket);
        var opens = new ConcurrentQueue<int>();
        await using var sender = CreateSender(connector, null);
        var runner = CreateSendingRunner(() => OpenCounted(recording, opens), sender);
        var serving = server.AcknowledgeAllAsync(false, TestContext.CancellationToken);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var messages = await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(_replayInOrder, messages.Where(message => message is not ContractStatus).Select(Describe), "The hello should come first, then the replay in its order");
        Assert.AreSequenceEqual(_replayedStates, messages.OfType<ContractStatus>().Select(status => status.PipeState), "The replayed states should be sent as statuses");
        Assert.IsTrue(server.IsClosedByClient, "The connection should be closed with a close frame");
        Assert.HasCount(2, opens, "The recording should be opened for the header check and for the replay");
        Assert.IsTrue(_events.First().IsConnecting, "The replay should show that it connects first");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 2,
                            Refused = 0
                        },
                        runner.Status.Sent,
                        "The finished status should carry the counts");
        Assert.IsTrue(runner.Status.IsFinished, "The replay should be finished");
        Assert.IsNull(runner.Status.SendProblem, "The replay should end without a send problem");
        Assert.IsNull(runner.Status.Problem, "The replay should end without a problem");
    }

    /// <summary>
    /// A token file that cannot be read or holds no valid token ends the replay with its problem before the recording is
    /// opened and before any connection.
    /// </summary>
    /// <param name="content">The content of the token file; <c>null</c> for a file that does not exist</param>
    /// <param name="expected">The expected problem</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(null, ReplaySendProblemKind.TokenFileCannotBeRead)]
    [DataRow("too-short-token", ReplaySendProblemKind.TokenFileHoldsNoValidToken)]
    public async Task PipeReplayRunnerTokenFileProblemConnectsNever(string? content, ReplaySendProblemKind expected)
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var connector = new DuplexReplayConnector(null);
        var opens = new ConcurrentQueue<int>();
        await using var sender = CreateSender(connector, CreateTokenFilePath(content));
        var runner = CreateSendingRunner(() => OpenCounted(recording, opens), sender);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, runner.Status.SendProblem, "The token file should end the replay with its problem");
        Assert.IsTrue(runner.Status.IsFinished, "The token file problem should end the replay");
        Assert.IsEmpty(opens, "The recording should not be opened");
        Assert.IsEmpty(connector.Addresses, "No connection should be opened");
    }

    /// <summary>
    /// A failure of the server ends the replay with its send problem: a refused hello before the replay, a connection lost
    /// at the first tick.
    /// </summary>
    /// <param name="isRefused">Whether the hello is refused; otherwise the connection ends at the first tick</param>
    /// <param name="expected">The expected problem</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(true, ReplaySendProblemKind.TokenRefused)]
    [DataRow(false, ReplaySendProblemKind.ConnectionLost)]
    public async Task PipeReplayRunnerServerFailureEndsWithSendProblem(bool isRefused, ReplaySendProblemKind expected)
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(1, 200)),
                                                 (_start.AddSeconds(3), PipeTestFrames.Island(1, 300)));
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), null);
        var runner = CreateSendingRunner(() => new MemoryStream(recording), sender);
        var serving = ServeFailingAsync(server, isRefused);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, runner.Status.SendProblem, "The server's failure should end the replay with its problem");
        Assert.IsTrue(runner.Status.IsFinished, "The send problem should end the replay");
        Assert.IsNull(runner.Status.Sent, "A failed replay should carry no counts");
    }

    /// <summary>
    /// A session end that repeats the one before carries no new information and is not sent.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task PipeReplayRunnerRepeatedSessionEndIsNotSent()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.SessionEnd()),
                                                 (_start.AddSeconds(2), PipeTestFrames.SessionStart("Player 1")),
                                                 (_start.AddSeconds(3), PipeTestFrames.SessionEnd()),
                                                 (_start.AddSeconds(4), PipeTestFrames.SessionEnd()));
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), null);
        var runner = CreateSendingRunner(() => new MemoryStream(recording), sender);
        var serving = server.AcknowledgeAllAsync(false, TestContext.CancellationToken);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var messages = await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(_sessionEventsOnce, messages.Where(message => message is ContractSessionStart or ContractSessionEnd).Select(Describe), "Only the session start and the first session end after it should be sent");
    }

    /// <summary>
    /// A session event carries the recorded read stamp of its frame as its receive time.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task PipeReplayRunnerSessionEventCarriesRecordedStamp()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(5), PipeTestFrames.SessionStart("Player 1")),
                                                 (_start.AddSeconds(20), PipeTestFrames.SessionEnd()));
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), null);
        var runner = CreateSendingRunner(() => new MemoryStream(recording), sender);
        var serving = server.AcknowledgeAllAsync(false, TestContext.CancellationToken);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var messages = await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(_start.AddSeconds(5), Assert.ContainsSingle(messages.OfType<ContractSessionStart>(), "One session start should be sent").ReceiveTime, "The session start should carry its frame's recorded stamp");
        Assert.AreEqual(_start.AddSeconds(20), Assert.ContainsSingle(messages.OfType<ContractSessionEnd>(), "One session end should be sent").ReceiveTime, "The session end should carry its frame's recorded stamp");
    }

    /// <summary>
    /// An invalid line after sent ticks ends the replay with today's problem after the ticks before it were sent, and the
    /// connection is closed.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PipeReplayRunnerInvalidLineAfterTicksClosesConnection()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(1), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(2), PipeTestFrames.Island(1, 200)));
        byte[] withInvalidLine = [.. recording, .. Encoding.UTF8.GetBytes("not json\n")];
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), null);
        var runner = CreateSendingRunner(() => new MemoryStream(withInvalidLine), sender);
        var serving = server.AcknowledgeAllAsync(false, TestContext.CancellationToken);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var messages = await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeReplayProblemKind.InvalidLine, runner.Status.Problem?.Kind, "The invalid line should end the replay with today's problem");
        Assert.AreEqual(5L, runner.Status.Problem?.LineNumber, "The problem should name the invalid line");
        Assert.IsNull(runner.Status.SendProblem, "The invalid line is no send problem");
        Assert.IsNotEmpty(messages.OfType<ContractTick>().ToList(), "The ticks before the invalid line should be sent");
        Assert.IsTrue(server.IsClosedByClient, "The connection should be closed with a close frame");
    }

    /// <summary>
    /// Stopping the replay ends the connection at once and raises no further status.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PipeReplayRunnerCancelledAbortsConnection()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var recording = PipeTestFrames.Recording(_start,
                                                 (_start, PipeTestFrames.Version(2)),
                                                 (_start.AddSeconds(10), PipeTestFrames.Island(1, 100)),
                                                 (_start.AddSeconds(100), PipeTestFrames.SessionEnd()));
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), null);
        var runner = new PipeReplayRunner(() => new MemoryStream(recording), PipeReplaySpeed.RealTime, waits, new RecordingLogger(), sender);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = server.AcknowledgeAllAsync(false, TestContext.CancellationToken);

        runner.StatusChanged += (_, status) => _events.Enqueue(status);

        var run = runner.RunAsync(stop.Token);

        await waits.WaitForTimersAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        await stop.CancelAsync();
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        var messages = await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsInstanceOfType<ContractHello>(messages.FirstOrDefault(), "The replay should have been connected");
        Assert.IsFalse(runner.Status.IsFinished, "A stopped replay is not finished");
        Assert.IsNull(runner.Status.SendProblem, "A stopped replay has no send problem");
        Assert.IsEmpty(_events.Where(status => status.IsFinished).ToList(), "The stop should raise no finished status");
    }

    /// <summary>
    /// A recording that opens for the header check but not a second time for the replay ends with file not found after the
    /// connection was closed with a close frame.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task PipeReplayRunnerSecondOpenFailureClosesConnection()
    {
        // Arrange
        var recording = PipeTestFrames.Recording(_start, (_start, PipeTestFrames.Version(2)));
        var opens = new ConcurrentQueue<int>();
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), null);
        var runner = CreateSendingRunner(() => OpenOnce(recording, opens), sender);
        var serving = server.AcknowledgeAllAsync(false, TestContext.CancellationToken);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var messages = await serving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeReplayProblemKind.FileNotFound, runner.Status.Problem?.Kind, "The failed second open should end the replay with file not found");
        Assert.IsInstanceOfType<ContractHello>(messages.FirstOrDefault(), "The connection should have been opened after the header check");
        Assert.IsTrue(server.IsClosedByClient, "The connection should be closed with a close frame");
        Assert.HasCount(2, opens, "The recording should have been opened twice");
    }

    /// <summary>
    /// Describes a message sent to the server: its type, and a tick's number of islands.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The description</returns>
    private static string Describe(ContractMessage message)
    {
        return message switch
               {
                   ContractTick tick => string.Create(CultureInfo.InvariantCulture, $"Tick {tick.Islands.Count}"),
                   _ => message.Type.ToString()
               };
    }

    /// <summary>
    /// Opens a recording and counts the opening.
    /// </summary>
    /// <param name="recording">The bytes of the recording</param>
    /// <param name="opens">The openings</param>
    /// <returns>The stream</returns>
    private static MemoryStream OpenCounted(byte[] recording, ConcurrentQueue<int> opens)
    {
        opens.Enqueue(opens.Count + 1);

        return new MemoryStream(recording);
    }

    /// <summary>
    /// Opens a recording the first time and fails every later time, as a file deleted after the header check.
    /// </summary>
    /// <param name="recording">The bytes of the recording</param>
    /// <param name="opens">The openings</param>
    /// <returns>The stream</returns>
    /// <exception cref="FileNotFoundException">The recording was opened before</exception>
    private static MemoryStream OpenOnce(byte[] recording, ConcurrentQueue<int> opens)
    {
        var isFirst = opens.IsEmpty;

        opens.Enqueue(opens.Count + 1);

        return isFirst ? new MemoryStream(recording) : throw new FileNotFoundException("The recording is gone.");
    }

    /// <summary>
    /// Creates a sender to a test address, whose timers never run.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="tokenFilePath">The path of the token file, if any</param>
    /// <returns>The sender</returns>
    private static ReplaySender CreateSender(IReplayServerConnector connector, string? tokenFilePath)
    {
        return new ReplaySender(new UriBuilder(Uri.UriSchemeWs, "127.0.0.1", 5000, "/ingest").Uri, tokenFilePath, "1.2.3", connector, new FakeTimeProvider(_start), new RecordingLogger());
    }

    /// <summary>
    /// Creates a status without a problem.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <returns>The status</returns>
    private static PipeReplayStatus Status(PipeConnectionState state, bool isFinished)
    {
        return new PipeReplayStatus
               {
                   State = state,
                   IsFinished = isFinished
               };
    }

    /// <summary>
    /// Creates a runner at maximum speed that sends the replay, with a handler that records its events.
    /// </summary>
    /// <param name="open">Opens the recording</param>
    /// <param name="sender">The sender</param>
    /// <returns>The runner</returns>
    private PipeReplayRunner CreateSendingRunner(Func<Stream> open, ReplaySender sender)
    {
        var runner = new PipeReplayRunner(open, PipeReplaySpeed.Maximum, TimeProvider.System, new RecordingLogger(), sender);

        runner.StatusChanged += (_, status) => _events.Enqueue(status);

        return runner;
    }

    /// <summary>
    /// Returns the path of a token file in a new temporary directory, written with a content or not written at all.
    /// </summary>
    /// <param name="content">The content; <c>null</c> for a file that does not exist</param>
    /// <returns>The path of the file</returns>
    private string CreateTokenFilePath(string? content)
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-replay-runner-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "token.txt");

        if (content is not null)
        {
            Directory.CreateDirectory(directory);
            _temporaryDirectories.Add(directory);
            File.WriteAllText(path, content);
        }

        return path;
    }

    /// <summary>
    /// Serves a sender with a failure: a refused hello, or a connection that ends at the first tick.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="isRefused">Whether the hello is refused</param>
    /// <returns>A task that completes when the failure was served</returns>
    private async Task ServeFailingAsync(ScriptedIngestServer server, bool isRefused)
    {
        if (isRefused == false)
        {
            await server.AcknowledgeAllAsync(true, TestContext.CancellationToken);

            return;
        }

        await server.ReceiveAsync(TestContext.CancellationToken);
        await server.SendAsync(new ContractHelloRefused
                               {
                                   Reason = ContractHelloRefusal.TokenRefused,
                                   ReceivedVersion = 1,
                                   SupportedVersions = [1]
                               },
                               TestContext.CancellationToken);
    }

    /// <summary>
    /// Asserts that the replay ended with the problem file not found.
    /// </summary>
    /// <param name="runner">The runner</param>
    private void AssertFileNotFound(PipeReplayRunner runner)
    {
        Assert.IsTrue(runner.Status.IsFinished, "A file that cannot be opened should end the replay");
        Assert.IsNotNull(runner.Status.Problem, "The problem should be on the status");
        Assert.AreEqual(PipeReplayProblemKind.FileNotFound, runner.Status.Problem.Kind, "The problem should be file not found");
        Assert.HasCount(1, _events, "The problem should be raised once");
    }

    /// <summary>
    /// Creates a runner at maximum speed over a recording, with a handler that records its events.
    /// </summary>
    /// <param name="recording">The bytes of the recording</param>
    /// <returns>The runner</returns>
    private PipeReplayRunner CreateRunner(byte[] recording)
    {
        return CreateRunner(recording, PipeReplaySpeed.Maximum, TimeProvider.System);
    }

    /// <summary>
    /// Creates a runner over a recording, with a handler that records its events.
    /// </summary>
    /// <param name="recording">The bytes of the recording</param>
    /// <param name="speed">The speed</param>
    /// <param name="waits">The clock of the waits</param>
    /// <returns>The runner</returns>
    private PipeReplayRunner CreateRunner(byte[] recording, PipeReplaySpeed speed, TimeProvider waits)
    {
        var runner = new PipeReplayRunner(() => new MemoryStream(recording), speed, waits);

        runner.StatusChanged += (_, status) => _events.Enqueue(status);

        return runner;
    }

    /// <summary>
    /// Creates a runner at maximum speed whose recording is opened by the given function.
    /// </summary>
    /// <param name="open">Opens the recording</param>
    /// <returns>The runner</returns>
    private PipeReplayRunner CreateRunner(Func<Stream> open)
    {
        var runner = new PipeReplayRunner(open, PipeReplaySpeed.Maximum, TimeProvider.System);

        runner.StatusChanged += (_, status) => _events.Enqueue(status);

        return runner;
    }

    #endregion // Methods
}