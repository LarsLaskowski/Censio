using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

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
    /// A started session records, and it keeps recording while the lines are written.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStateIsRecordingUntilFailure()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);
        var changes = 0;

        session.Start();
        session.StateChanged += (_, _) => changes++;

        var connection = session.BeginConnection();

        // Act
        connection.Record(Result(PipeTestFrames.Version(2)), _start);

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

        session.Start();

        var changed = ChangeOf(session);
        var connection = session.BeginConnection();

        sink.FailWritesAfter(0, new IOException("Disk full"));

        // Act
        connection.Record(Result(PipeTestFrames.Version(2)), _start);

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

        session.Start();

        var changed = ChangeOf(session);
        var connection = session.BeginConnection();

        // Act
        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await changed.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "A file that cannot be created should set the failed state");
    }

    /// <summary>
    /// After a failure a connection records nothing until the recording is started again.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionFailureRecordsNothingUntilStart()
    {
        // Arrange
        var opened = 0;
        var session = new PipeRecordingSession(_ =>
                                               {
                                                   Interlocked.Increment(ref opened);

                                                   throw new IOException("Disk full");
                                               });

        session.Start();

        var changed = ChangeOf(session);

        session.BeginConnection().Record(Result(PipeTestFrames.Version(2)), _start);

        await changed.WaitAsync(_guard, TestContext.CancellationToken);

        var next = session.BeginConnection();

        // Act
        next.Record(Result(PipeTestFrames.Version(2)), _start.AddSeconds(1));

        await session.StopAsync(_guard).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, Volatile.Read(ref opened), "A failed session should open no file for a later connection");
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

        session.Start();
        session.StateChanged += (_, _) => Interlocked.Increment(ref changes);

        var firstConnection = session.BeginConnection();
        var secondConnection = session.BeginConnection();

        first.FailWritesAfter(0, new IOException("Disk full"));
        second.FailWritesAfter(0, new IOException("Disk full"));

        // Act
        firstConnection.Record(Result(PipeTestFrames.Version(2)), _start);
        secondConnection.Record(Result(PipeTestFrames.Version(2)), _start.AddSeconds(1));

        await Task.WhenAll(first.WaitForDisposeAsync(TestContext.CancellationToken), second.WaitForDisposeAsync(TestContext.CancellationToken)).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, Volatile.Read(ref changes), "The state change should be raised once");
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "The session should be failed");
    }

    /// <summary>
    /// A subscriber that throws neither stops the other subscribers nor the failed state.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionThrowingStateChangedHandlerIsIgnored()
    {
        // Arrange
        var session = new PipeRecordingSession(_ => throw new IOException("Disk full"));

        session.Start();
        session.StateChanged += (_, _) => throw new InvalidOperationException("The subscriber failed");

        var changed = ChangeOf(session);

        // Act
        session.BeginConnection().Record(Result(PipeTestFrames.Version(2)), _start);

        await changed.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Failed, session.State, "The failed state should be set although a subscriber threw");
    }

    /// <summary>
    /// Each connection gets a recording of its own.
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
        Assert.IsNotNull(first, "The first connection should get a recording");
        Assert.IsNotNull(second, "The second connection should get a recording");
        Assert.AreNotSame(first, second, "Each connection should get its own recording");
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

        session.Start();

        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);
        connection.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(1));

        // Act
        session.EndConnection(connection);

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

        session.Start();

        var connection = session.BeginConnection();
        var firstLines = $"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}";

        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await sink.WaitForFlushedBytesAsync(firstLines.Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        sink.HoldWrites();
        connection.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(1));

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

        session.Start();

        var firstConnection = session.BeginConnection();
        var secondConnection = session.BeginConnection();
        var secondStart = _start.AddSeconds(1);

        firstConnection.Record(Result(PipeTestFrames.Version(2)), _start);
        firstConnection.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(2));
        secondConnection.Record(Result(PipeTestFrames.Version(2)), secondStart);
        secondConnection.Record(Result(PipeTestFrames.SessionEnd()), secondStart.AddSeconds(2));

        // Act
        await session.StopAsync(_guard).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}{Text(PipeTestFrames.SessionEnd(), _start.AddSeconds(2))}", first.Text, "The first file should hold all its lines");
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(secondStart)}{Text(PipeTestFrames.Version(2), secondStart)}{Text(PipeTestFrames.SessionEnd(), secondStart.AddSeconds(2))}", second.Text, "The second file should hold all its lines");
        Assert.IsTrue(first.IsDisposed, "The first file should be closed");
        Assert.IsTrue(second.IsDisposed, "The second file should be closed");
    }

    /// <summary>
    /// A new session is off and records nothing.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStartsOff()
    {
        // Arrange
        var opened = 0;
        var session = new PipeRecordingSession(_ =>
                                               {
                                                   Interlocked.Increment(ref opened);

                                                   return new ControlledWriteStream();
                                               });
        var connection = session.BeginConnection();

        // Act
        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await session.StopAsync(_guard).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Off, session.State, "A new session should be off");
        Assert.AreEqual(0, Volatile.Read(ref opened), "A session that is off should create no file");
    }

    /// <summary>
    /// A start while off records the next frame of an open connection.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStartWhileOffRecords()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);
        var connection = session.BeginConnection();
        var expected = $"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}";

        // Act
        session.Start();
        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await sink.WaitForFlushedBytesAsync(expected.Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Recording, session.State, "A started session should record");
        Assert.AreEqual(expected, sink.Text, "The file should hold the header and the frame");
    }

    /// <summary>
    /// A stop writes the queued lines of the active recorder, closes its file and turns the session off.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStopEndsRecorderAndClosesFile()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var session = new PipeRecordingSession(_ => sink);

        session.Start();

        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);
        connection.Record(Result(PipeTestFrames.SessionEnd()), _start.AddSeconds(1));

        // Act
        session.Stop();

        await sink.WaitForDisposeAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Off, session.State, "A stopped session should be off");
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}{Text(PipeTestFrames.SessionEnd(), _start.AddSeconds(1))}", sink.Text, "The queued lines should be written before the file is closed");
    }

    /// <summary>
    /// A start after a stop writes a new file, named and headed by its first frame.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStartAfterStopWritesNewFile()
    {
        // Arrange
        var first = new ControlledWriteStream();
        var second = new ControlledWriteStream();
        var restart = _start.AddSeconds(5);
        var session = new PipeRecordingSession(time => time == _start ? first : second);

        session.Start();

        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);
        session.Stop();

        await first.WaitForDisposeAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var expected = $"{PipeRecordingFormat.FormatHeader(restart)}{Text(PipeTestFrames.Version(2), restart)}";

        // Act
        session.Start();
        connection.Record(Result(PipeTestFrames.Version(2)), restart);

        await second.WaitForFlushedBytesAsync(expected.Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}", first.Text, "The first file should hold only the frame of the first recording");
        Assert.AreEqual(expected, second.Text, "The second recording should write a new file from its first frame");
    }

    /// <summary>
    /// A start without an open connection creates no file; the file is created at the first frame of the next
    /// connection, with that frame's receive time.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStartWithoutConnectionCreatesFileAtFirstFrame()
    {
        // Arrange
        var opened = new List<DateTimeOffset>();
        var sink = new ControlledWriteStream();
        var frameTime = _start.AddMinutes(3);
        var session = new PipeRecordingSession(time =>
                                               {
                                                   lock (opened)
                                                   {
                                                       opened.Add(time);
                                                   }

                                                   return sink;
                                               });
        var expected = $"{PipeRecordingFormat.FormatHeader(frameTime)}{Text(PipeTestFrames.Version(2), frameTime)}";

        session.Start();

        var connection = session.BeginConnection();

        // Act
        connection.Record(Result(PipeTestFrames.Version(2)), frameTime);

        await sink.WaitForFlushedBytesAsync(expected.Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        DateTimeOffset[] times;

        lock (opened)
        {
            times = [.. opened];
        }

        Assert.AreSequenceEqual(new[] { frameTime }, times, "One file should be created, at the receive time of the first frame");
        Assert.AreEqual(expected, sink.Text, "The file should be headed by the first frame's receive time");
    }

    /// <summary>
    /// A start after a failure records again into a new file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStartAfterFailureRecordsAgain()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var retry = _start.AddSeconds(10);
        var session = new PipeRecordingSession(time => time == _start ? throw new IOException("Disk full") : sink);

        session.Start();

        var failed = ChangeOf(session);
        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await failed.WaitAsync(_guard, TestContext.CancellationToken);

        var expected = $"{PipeRecordingFormat.FormatHeader(retry)}{Text(PipeTestFrames.Version(2), retry)}";

        // Act
        session.Start();
        connection.Record(Result(PipeTestFrames.Version(2)), retry);

        await sink.WaitForFlushedBytesAsync(expected.Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Recording, session.State, "A start after a failure should record again");
        Assert.AreEqual(expected, sink.Text, "The new recording should write a new file");
    }

    /// <summary>
    /// A stopped recorder that fails while it writes its remaining frames changes neither the state of a new
    /// recording nor raises a state change; the failure is only logged.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStoppedRecorderFailureKeepsState()
    {
        // Arrange
        var first = new ControlledWriteStream();
        var second = new ControlledWriteStream();
        var logger = new RecordingLogger();
        var session = new PipeRecordingSession(time => time == _start ? first : second, logger);
        var changes = 0;

        first.HoldWrites();
        session.Start();

        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await first.WaitForWritesAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        session.Stop();
        session.Start();
        session.StateChanged += (_, _) => Interlocked.Increment(ref changes);
        first.FailWritesAfter(0, new IOException("Disk full"));

        // Act
        first.ReleaseWrites();

        await first.WaitForDisposeAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingState.Recording, session.State, "The failure of a stopped recorder should not change the state");
        Assert.AreEqual(0, Volatile.Read(ref changes), "The failure of a stopped recorder should raise no state change");
        Assert.AreEqual((LogLevel.Warning, "A stopped recording failed while writing its remaining frames"), (logger.Entries[^1].Level, logger.Entries[^1].Message), "The failure should be logged as a warning");
    }

    /// <summary>
    /// A toggle starts an off session and stops a recording one.
    /// </summary>
    /// <param name="toggles">The number of toggles</param>
    /// <param name="expected">The expected state</param>
    [TestMethod]
    [DataRow(1, PipeRecordingState.Recording)]
    [DataRow(2, PipeRecordingState.Off)]
    [DataRow(3, PipeRecordingState.Recording)]
    public void PipeRecordingSessionToggleSwitches(int toggles, PipeRecordingState expected)
    {
        // Arrange
        var session = new PipeRecordingSession(_ => new ControlledWriteStream());

        // Act
        Toggle(session, toggles);

        // Assert
        Assert.AreEqual(expected, session.State, $"{toggles} toggles should leave the session {expected}");
    }

    /// <summary>
    /// A toggle of a failed session starts it again.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionToggleAfterFailureRecords()
    {
        // Arrange
        var session = new PipeRecordingSession(_ => throw new IOException("Disk full"));

        session.Start();

        var failed = ChangeOf(session);

        session.BeginConnection().Record(Result(PipeTestFrames.Version(2)), _start);

        await failed.WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        session.Toggle();

        // Assert
        Assert.AreEqual(PipeRecordingState.Recording, session.State, "A toggle of a failed session should start it");
    }

    /// <summary>
    /// Every change between off and recording raises the state change; a call that changes nothing raises none.
    /// </summary>
    [TestMethod]
    public void PipeRecordingSessionStateChangedOnEveryChange()
    {
        // Arrange
        var session = new PipeRecordingSession(_ => new ControlledWriteStream());
        var states = new List<PipeRecordingState>();

        session.StateChanged += (_, _) => states.Add(session.State);

        // Act
        session.Stop();
        session.Start();
        session.Start();
        session.Stop();
        session.Toggle();

        // Assert
        PipeRecordingState[] expected = [PipeRecordingState.Recording, PipeRecordingState.Off, PipeRecordingState.Recording];

        Assert.AreSequenceEqual(expected, states.ToArray(), "Each change, and only a change, should raise the state change");
    }

    /// <summary>
    /// The stop at exit writes the queued lines of the active recorder and of a recorder stopped before that is
    /// still writing, and waits for both.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStopAsyncCompletesActiveAndStoppedRecorders()
    {
        // Arrange
        var first = new ControlledWriteStream();
        var second = new ControlledWriteStream();
        var restart = _start.AddSeconds(5);
        var session = new PipeRecordingSession(time => time == _start ? first : second);

        first.HoldWrites();
        session.Start();

        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await first.WaitForWritesAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        session.Stop();
        session.Start();
        connection.Record(Result(PipeTestFrames.Version(2)), restart);
        connection.Record(Result(PipeTestFrames.SessionEnd()), restart.AddSeconds(1));

        // Act
        var stopping = session.StopAsync(_guard);
        var completedWhileHeld = stopping.IsCompleted;

        first.ReleaseWrites();

        await stopping.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(completedWhileHeld, "The stop should wait for the stopped recorder");
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(_start)}{Text(PipeTestFrames.Version(2), _start)}", first.Text, "The stopped recording should hold all its lines");
        Assert.AreEqual($"{PipeRecordingFormat.FormatHeader(restart)}{Text(PipeTestFrames.Version(2), restart)}{Text(PipeTestFrames.SessionEnd(), restart.AddSeconds(1))}", second.Text, "The active recording should hold all its lines");
        Assert.IsTrue(first.IsDisposed, "The stopped recording's file should be closed");
        Assert.IsTrue(second.IsDisposed, "The active recording's file should be closed");
    }

    /// <summary>
    /// The stop at exit gives up after the timeout while a stopped recorder's disk stalls.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionStopAsyncGivesUpAfterTimeout()
    {
        // Arrange
        var first = new ControlledWriteStream();
        var second = new ControlledWriteStream();
        var restart = _start.AddSeconds(5);
        var session = new PipeRecordingSession(time => time == _start ? first : second);

        first.HoldWrites();
        session.Start();

        var connection = session.BeginConnection();

        connection.Record(Result(PipeTestFrames.Version(2)), _start);

        await first.WaitForWritesAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        session.Stop();
        session.Start();
        connection.Record(Result(PipeTestFrames.Version(2)), restart);

        await second.WaitForFlushedBytesAsync($"{PipeRecordingFormat.FormatHeader(restart)}{Text(PipeTestFrames.Version(2), restart)}".Length, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        await session.StopAsync(TimeSpan.FromMilliseconds(100)).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(first.IsDisposed, "The stalled recording should still be writing when the stop gives up");
        Assert.IsTrue(second.IsDisposed, "The active recording's file should be closed");

        first.ReleaseWrites();
    }

    /// <summary>
    /// The start, the stop and the failure of a recording are logged.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecordingSessionLogsStartStopAndFailure()
    {
        // Arrange
        var sink = new ControlledWriteStream();
        var logger = new RecordingLogger();
        var session = new PipeRecordingSession(_ => sink, logger);

        sink.FailWritesAfter(0, new IOException("Disk full"));

        // Act
        session.Start();
        session.Stop();
        session.Start();
        session.BeginConnection().Record(Result(PipeTestFrames.Version(2)), _start);

        await sink.WaitForDisposeAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        (LogLevel, string)[] expected = [
                                            (LogLevel.Information, "Recording started"),
                                            (LogLevel.Information, "Recording stopped"),
                                            (LogLevel.Information, "Recording started"),
                                            (LogLevel.Warning, "Recording failed"),
                                        ];

        Assert.AreSequenceEqual(expected, logger.Entries.Select(entry => (entry.Level, entry.Message)).ToArray(), "Start, stop and failure should be logged in order");
    }

    /// <summary>
    /// Toggles the session a number of times.
    /// </summary>
    /// <param name="session">The session</param>
    /// <param name="count">The number of toggles</param>
    private static void Toggle(PipeRecordingSession session, int count)
    {
        for (var index = 0; index < count; index++)
        {
            session.Toggle();
        }
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

    #endregion // Methods
}