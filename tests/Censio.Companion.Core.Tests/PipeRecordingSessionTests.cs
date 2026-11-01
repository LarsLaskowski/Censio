using Censio.Pipe;
using Censio.Pipe.Tests;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeRecordingSession" />.
/// </summary>
[TestClass]
public sealed class PipeRecordingSessionTests
{
    #region Fields

    /// <summary>
    /// The receive time of the first frame of the tests.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingSessionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeRecordingSessionTests(TestContext testContext)
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
    /// A new session records, and it keeps recording while the lines are written.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStateIsRecordingUntilFailure()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);
        var changes = 0;

        session.StateChanged += (_, _) => changes++;

        var recorder = session.BeginConnection()!;

        // Act
        recorder.Record(Result(PipeTestFrames.Version(2)), _start);

        await sink.WaitForFlushedBytesAsync(Text(PipeTestFrames.Version(2), _start).Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Recording, session.State, "The session should record while the lines are written");
        Assert.AreEqual(0, changes, "No state change should be raised without a failure");
    }

    /// <summary>
    /// A failed write sets the failed state and raises the state change.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionWriteFailureSetsFailedAndRaisesStateChanged()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);
        var changed = ChangeOf(session);
        var recorder = session.BeginConnection()!;

        sink.FailWritesAfter(0, new IOException("Disk full"));

        // Act
        recorder.Record(Result(PipeTestFrames.Version(2)), _start);

        await changed.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "A failed write should set the failed state");
    }

    /// <summary>
    /// A file that cannot be created sets the failed state.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionOpenFailureSetsFailed()
    {
        // Arrange
        var session = new PipeRecordingSession(_ => throw new DirectoryNotFoundException("The folder is gone"));
        var changed = ChangeOf(session);
        var recorder = session.BeginConnection()!;

        // Act
        recorder.Record(Result(PipeTestFrames.Version(2)), _start);

        await changed.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "A file that cannot be created should set the failed state");
    }

    /// <summary>
    /// After a failure no connection is recorded any more.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionFailureStopsRecordingForRestOfRun()
    {
        // Arrange
        var session = new PipeRecordingSession(_ => throw new IOException("Disk full"));
        var changed = ChangeOf(session);

        session.BeginConnection()!.Record(Result(PipeTestFrames.Version(2)), _start);

        await changed.WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        var next = session.BeginConnection();

        // Assert
        Assert.IsNull(next, "A failed session should record no later connection");
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "The session should stay failed");
    }

    /// <summary>
    /// The state change is raised once even when several recorders fail.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionSeveralFailuresRaiseStateChangedOnce()
    {
        // Arrange
        var first = new ControlledWriteStream();
        var second = new ControlledWriteStream();
        var session = new PipeRecordingSession(time => time == _start ? first : second);
        var changes = 0;

        session.StateChanged += (_, _) => Interlocked.Increment(ref changes);

        var firstRecorder = session.BeginConnection()!;
        var secondRecorder = session.BeginConnection()!;
        var firstFailed = ChangeOf(firstRecorder);
        var secondFailed = ChangeOf(secondRecorder);

        first.FailWritesAfter(0, new IOException("Disk full"));
        second.FailWritesAfter(0, new IOException("Disk full"));

        // Act
        firstRecorder.Record(Result(PipeTestFrames.Version(2)), _start);
        secondRecorder.Record(Result(PipeTestFrames.Version(2)), _start.AddSeconds(1));

        await Task.WhenAll(firstFailed, secondFailed).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, changes, "The state change should be raised once");
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "The session should be failed");
    }

    /// <summary>
    /// Each connection gets a recorder of its own.
    /// </summary>
    [TestMethod]
    public void PipeRecordingSessionBeginConnectionReturnsRecorderPerConnection()
    {
        // Arrange
        var session = new PipeRecordingSession(_ => new ControlledWriteStream());

        // Act
        var first = session.BeginConnection();
        var second = session.BeginConnection();

        // Assert
        Assert.IsNotNull(first, "The first connection should get a recorder");
        Assert.IsNotNull(second, "The second connection should get a recorder");
        Assert.AreNotSame(first, second, "Each connection should get its own recorder");
    }

    /// <summary>
    /// Ending a connection writes its queued lines and closes its file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionEndConnectionClosesFile()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);
        var recorder = session.BeginConnection()!;

        recorder.Record(Result(PipeTestFrames.Version(2)), _start);
        recorder.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(1));

        // Act
        session.EndConnection(recorder);

        await sink.WaitForDisposeAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}{Text(PipeTestFrames.SessionEnd(), _start.AddSeconds(1))}", sink.Text, "The file should hold the header and both lines");
        Assert.AreEqual(PipeRecordingState.Recording, session.State, "Ending a connection should not change the state");
    }

    /// <summary>
    /// The stop gives up on a stalled disk after the timeout and leaves only complete lines.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStopReturnsWithinTimeoutOnStalledDisk()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);
        var recorder = session.BeginConnection()!;
        var firstLines = $"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}";

        recorder.Record(Result(PipeTestFrames.Version(2)), _start);

        await sink.WaitForFlushedBytesAsync(firstLines.Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        sink.HoldWrites();
        recorder.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(1));

        await sink.WaitForWritesAsync(3, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        await session.StopAsync(TimeSpan.FromMilliseconds(100)).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(firstLines, sink.Text, "The file should end with the last complete line written");

        sink.ReleaseWrites();
    }

    /// <summary>
    /// The stop writes the queued lines of every connection and closes every file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStopCompletesAllConnections()
    {
        // Arrange
        var first = new ControlledWriteStream();
        var second = new ControlledWriteStream();
        var session = new PipeRecordingSession(time => time == _start ? first : second);
        var firstRecorder = session.BeginConnection()!;
        var secondRecorder = session.BeginConnection()!;
        var secondStart = _start.AddSeconds(1);

        firstRecorder.Record(Result(PipeTestFrames.Version(2)), _start);
        firstRecorder.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(2));
        secondRecorder.Record(Result(PipeTestFrames.Version(2)), secondStart);
        secondRecorder.Record(Result(PipeTestFrames.SessionEnd()), secondStart.AddSeconds(2));

        // Act
        await session.StopAsync(_guard).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}{Text(PipeTestFrames.SessionEnd(), _start.AddSeconds(2))}", first.Text, "The first file should hold all its lines");
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(secondStart)}{Text(PipeTestFrames.Version(2), secondStart)}{Text(PipeTestFrames.SessionEnd(), secondStart.AddSeconds(2))}", second.Text, "The second file should hold all its lines");
        Assert.IsTrue(first.IsDisposed, "The first file should be closed");
        Assert.IsTrue(second.IsDisposed, "The second file should be closed");
    }

    /// <summary>
    /// Creates the read result of a frame the recorder records.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The result</returns>
    private static PipeReadResult Result(byte[] frame)
    {
        return PipeReadResult.FromMessage(new SessionEndMessage()).WithFrame(frame);
    }

    /// <summary>
    /// Creates the line a recorder writes for a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="time">The receive time</param>
    /// <returns>The line with its terminator</returns>
    private static string Text(byte[] frame, DateTimeOffset time)
    {
        return PipeRecordingFormat.FormatFrameLine(time, frame);
    }

    /// <summary>
    /// Creates a task that completes when the session raises its state change.
    /// </summary>
    /// <param name="session">The session</param>
    /// <returns>The task</returns>
    private static Task ChangeOf(PipeRecordingSession session)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        session.StateChanged += (_, _) => source.TrySetResult();

        return source.Task;
    }

    /// <summary>
    /// Creates a task that completes when the recorder reports its failure.
    /// </summary>
    /// <param name="recorder">The recorder</param>
    /// <returns>The task</returns>
    private static Task ChangeOf(PipeRecorder recorder)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        recorder.Failed += (_, _) => source.TrySetResult();

        return source.Task;
    }

    #endregion // Methods
}