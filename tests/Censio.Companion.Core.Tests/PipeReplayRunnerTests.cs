using System.Collections.Concurrent;
using System.Text;

using Censio.Pipe;
using Censio.Pipe.Tests;

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
    /// The statuses the runner raised, in order.
    /// </summary>
    private readonly ConcurrentQueue<PipeReplayStatus> _events = [];

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