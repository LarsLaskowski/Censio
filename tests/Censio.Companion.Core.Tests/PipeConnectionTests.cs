using System.Collections.Concurrent;

using Censio.Pipe;
using Censio.Pipe.Tests;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeConnection" />.
/// </summary>
[TestClass]
public sealed class PipeConnectionTests
{
    #region Fields

    /// <summary>
    /// The start of the fake clock.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The step in which the fake clock is moved, so every timer fires at its own due time.
    /// </summary>
    private static readonly TimeSpan _step = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The fake clock of the connection and the connector.
    /// </summary>
    private readonly FakeTimeProvider _clock = new(_start);

    /// <summary>
    /// The connector of the connection.
    /// </summary>
    private readonly ScriptedPipeConnector _connector;

    /// <summary>
    /// The statuses the connection raised, in order.
    /// </summary>
    private readonly ConcurrentQueue<PipeConnectionStatus> _events = [];

    /// <summary>
    /// The runs started by the test.
    /// </summary>
    private readonly List<Task> _runs = [];

    /// <summary>
    /// The source that stops the runs of the test.
    /// </summary>
    private readonly CancellationTokenSource _stop = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeConnectionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeConnectionTests(TestContext testContext)
    {
        TestContext = testContext;
        _connector = new ScriptedPipeConnector(_clock);
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
    /// Stops every run of the test and waits until it has ended.
    /// </summary>
    /// <returns>The task</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _stop.CancelAsync();
        await Task.WhenAll(_runs).WaitAsync(_guard, TestContext.CancellationToken);

        _stop.Dispose();
    }

    /// <summary>
    /// Before the first attempt the connection waits for the game, without a cause.
    /// </summary>
    [TestMethod]
    public void PipeConnectionStatusBeforeRunIsWaitingForGame()
    {
        // Arrange
        var connection = CreateConnection();

        // Act
        var status = connection.Status;

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.WaitingForGame), status, "Before the first attempt the connection should wait for the game");
    }

    /// <summary>
    /// The first attempt runs at once.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionRunFirstAttemptWithoutDelay()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.NotFound);

        var connection = CreateConnection();

        // Act
        StartRun(connection);

        await WaitForAttemptsAsync(1);

        // Assert
        Assert.AreSequenceEqual(Seconds(0), AttemptOffsets(), "The first attempt should run without the clock being advanced");
    }

    /// <summary>
    /// While the pipe does not exist, the attempts follow the retry delays 1, 2, 4, 8, 10 and 10 seconds.
    /// </summary>
    [TestMethod]
    public void PipeConnectionPipeNotFoundRetriesWithGrowingDelays()
    {
        // Arrange
        EnqueueResults(PipeConnectResult.NotFound, 7);

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(35));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1, 3, 7, 15, 25, 35), AttemptOffsets(), "The attempts should follow the retry delays 1, 2, 4, 8, 10, 10 s");
    }

    /// <summary>
    /// While the pipe does not exist, no event is raised.
    /// </summary>
    [TestMethod]
    public void PipeConnectionPipeNotFoundRaisesNoEvent()
    {
        // Arrange
        EnqueueResults(PipeConnectResult.NotFound, 7);

        var connection = CreateConnection();

        StartRun(connection);

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(35));

        // Assert
        Assert.AreEqual(7, _connector.AttemptCount, "Seven attempts should have been made");
        Assert.IsEmpty(_events, "A missing pipe should raise no event");
        Assert.AreEqual(Status(PipeConnectionState.WaitingForGame), connection.Status, "The connection should still wait for the game");
    }

    /// <summary>
    /// An attempt that opens the pipe raises connected.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionConnectedRaisesConnected()
    {
        // Arrange
        var stream = EnqueueStream();
        var connection = CreateConnection();

        // Act
        StartRun(connection);

        await WaitForPendingReadsAsync(stream, 1);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected) }, _events.ToArray(), "An opened pipe should raise connected");
    }

    /// <summary>
    /// The first version 2 message raises receiving.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionVersionTwoRaisesReceiving()
    {
        // Arrange
        var stream = await StartConnectedAsync();

        // Act
        stream.Feed(VersionFrame(2));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving) },
                                _events.ToArray(),
                                "Version 2 should raise receiving after connected");
    }

    /// <summary>
    /// A stream that ends at a frame boundary raises waiting for game.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionEndAtFrameBoundaryRaisesWaitingForGame()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2));

        // Act
        stream.End();

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving), Status(PipeConnectionState.WaitingForGame) },
                                _events.ToArray(),
                                "The end of the stream at a frame boundary should raise waiting for game");
    }

    /// <summary>
    /// A stream that ends inside a frame raises an error with the truncated-frame kind.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionEndInsideFrameRaisesErrorTruncatedFrame()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2), PartialFrame());

        // Act
        stream.End();

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving), ErrorStatus(PipeErrorKind.TruncatedFrame) },
                                _events.ToArray(),
                                "The end of the stream inside a frame should raise an error with the truncated-frame kind");
    }

    /// <summary>
    /// After a connection ended, the next attempt follows after the retry delay.
    /// </summary>
    [TestMethod]
    public void PipeConnectionEndedConnectionRetriesAfterDelay()
    {
        // Arrange
        var stream = EnqueueStream(VersionFrame(2));

        stream.End();

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(2));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1), AttemptOffsets(), "The next attempt should follow one second after the connection ended");
    }

    /// <summary>
    /// A pipe that appears again after the connection ended is connected.
    /// </summary>
    [TestMethod]
    public void PipeConnectionReappearingPipeIsConnected()
    {
        // Arrange
        var first = EnqueueStream(VersionFrame(2));

        first.End();

        _connector.Enqueue(PipeConnectResult.NotFound);

        var second = EnqueueStream();

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(3));

        // Assert
        Assert.AreSequenceEqual(new[]
                                {
                                    Status(PipeConnectionState.Connected),
                                    Status(PipeConnectionState.Receiving),
                                    Status(PipeConnectionState.WaitingForGame),
                                    Status(PipeConnectionState.Connected)
                                },
                                _events.ToArray(),
                                "The reappearing pipe should be connected again");
        Assert.AreEqual(1, second.PendingReadCount, "The new connection should read the reappeared pipe");
    }

    /// <summary>
    /// Session and island messages keep receiving and cause no new attempt.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionSessionAndIslandMessagesKeepReceiving()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2));

        // Act
        stream.Feed(Concat(SessionStartFrame(), SessionEndFrame(), IslandFrame(), IslandFrame()));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving) },
                                _events.ToArray(),
                                "Session and island messages should raise no event after receiving");
        Assert.AreEqual(1, _connector.AttemptCount, "Session and island messages should cause no new attempt");
        Assert.IsFalse(stream.IsDisposed, "The connection should stay open");
    }

    /// <summary>
    /// An attempt on a pipe without a free instance raises pipe busy.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionBusyRaisesPipeBusy()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.Busy);

        var connection = CreateConnection();

        // Act
        StartRun(connection);

        await WaitForAttemptsAsync(1);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.PipeBusy) }, _events.ToArray(), "A busy pipe should raise pipe busy");
        Assert.AreEqual(Status(PipeConnectionState.PipeBusy), connection.Status, "The status should be pipe busy");
    }

    /// <summary>
    /// A busy pipe is retried with the retry delay.
    /// </summary>
    [TestMethod]
    public void PipeConnectionBusyRetriesWithDelay()
    {
        // Arrange
        EnqueueResults(PipeConnectResult.Busy, 2);

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(3));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1, 3), AttemptOffsets(), "A busy pipe should be retried after 1 and 2 seconds");
    }

    /// <summary>
    /// A busy pipe that becomes free is connected.
    /// </summary>
    [TestMethod]
    public void PipeConnectionBusyThenConnectedRaisesConnected()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.Busy);

        EnqueueStream();
        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(1));

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.PipeBusy), Status(PipeConnectionState.Connected) },
                                _events.ToArray(),
                                "The freed pipe should raise connected after pipe busy");
    }

    /// <summary>
    /// A version other than 2 raises a protocol mismatch with the received version.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionWrongVersionRaisesProtocolMismatchWithVersion()
    {
        // Arrange
        var stream = await StartConnectedAsync();

        // Act
        stream.Feed(VersionFrame(3));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), MismatchStatus(3) },
                                _events.ToArray(),
                                "Version 3 should raise a protocol mismatch carrying version 3");
    }

    /// <summary>
    /// A frame before any version raises a protocol mismatch without a version.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionFrameBeforeVersionRaisesProtocolMismatchWithoutVersion()
    {
        // Arrange
        var stream = await StartConnectedAsync();

        // Act
        stream.Feed(SessionEndFrame());

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), MismatchStatus(null) },
                                _events.ToArray(),
                                "A frame before the version should raise a protocol mismatch without a version");
    }

    /// <summary>
    /// The mismatch errors of the frames after a mismatch raise no new event.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionRepeatedMismatchRaisesNoEvent()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(3));

        // Act
        stream.Feed(Concat(SessionEndFrame(), SessionStartFrame(), IslandFrame()));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), MismatchStatus(3) },
                                _events.ToArray(),
                                "Further mismatch errors should raise no new event");
    }

    /// <summary>
    /// A protocol mismatch keeps the connection open and reading.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionMismatchKeepsReading()
    {
        // Arrange
        var stream = await StartConnectedAsync();

        // Act
        stream.Feed(Concat(VersionFrame(3), SessionEndFrame(), SessionEndFrame()));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreEqual(0, stream.UnreadByteCount, "The frames after the mismatch should be read");
        Assert.IsFalse(stream.IsDisposed, "The connection should stay open");
        Assert.AreEqual(1, _connector.AttemptCount, "A mismatch should cause no new attempt");
    }

    /// <summary>
    /// A version 2 after a mismatch raises receiving.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionVersionTwoAfterMismatchRaisesReceiving()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(3));

        // Act
        stream.Feed(VersionFrame(2));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), MismatchStatus(3), Status(PipeConnectionState.Receiving) },
                                _events.ToArray(),
                                "Version 2 after the mismatch should raise receiving");
    }

    /// <summary>
    /// An empty, unknown or malformed frame does not change the state.
    /// </summary>
    /// <param name="frame">The skipped frame with its length prefix</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0 }, DisplayName = "Empty frame")]
    [DataRow(new byte[] { 1, 0, 0, 0, 4 }, DisplayName = "Unknown type")]
    [DataRow(new byte[] { 2, 0, 0, 0, 1, 50 }, DisplayName = "Malformed message")]
    public async Task PipeConnectionSkippedFrameKeepsState(byte[] frame)
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2));

        // Act
        stream.Feed(Concat(frame, SessionEndFrame()));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving) },
                                _events.ToArray(),
                                "A skipped frame should not change the state");
        Assert.IsFalse(stream.IsDisposed, "A skipped frame should keep the connection open");
    }

    /// <summary>
    /// A truncated frame, an oversized frame or a failed read raises an error with that kind.
    /// </summary>
    /// <param name="kind">The error that ends the stream</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(PipeErrorKind.TruncatedFrame)]
    [DataRow(PipeErrorKind.OversizedFrame)]
    [DataRow(PipeErrorKind.ReadFailed)]
    public async Task PipeConnectionStreamEndingErrorRaisesError(PipeErrorKind kind)
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2));

        // Act
        EndWithError(stream, kind);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving), ErrorStatus(kind) },
                                _events.ToArray(),
                                "The error that ends the stream should raise an error with its kind");
    }

    /// <summary>
    /// An error that ends the stream closes the pipe.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionStreamEndingErrorDisposesStream()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2));

        // Act
        stream.Feed(PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));

        // Assert
        Assert.IsTrue(stream.IsDisposed, "An oversized frame should close the pipe");
    }

    /// <summary>
    /// The result of the next attempt replaces the error of an ended stream.
    /// </summary>
    [TestMethod]
    public void PipeConnectionErrorReplacedByNextAttempt()
    {
        // Arrange
        EnqueueStream(VersionFrame(2), PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));

        _connector.Enqueue(PipeConnectResult.NotFound);

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(1));

        // Assert
        Assert.AreSequenceEqual(new[]
                                {
                                    Status(PipeConnectionState.Connected),
                                    Status(PipeConnectionState.Receiving),
                                    ErrorStatus(PipeErrorKind.OversizedFrame),
                                    Status(PipeConnectionState.WaitingForGame)
                                },
                                _events.ToArray(),
                                "The next attempt's result should replace the error");
    }

    /// <summary>
    /// A read exception the frame reader does not handle ends the connection as read failed and is followed by an
    /// attempt after the retry delay.
    /// </summary>
    /// <param name="exceptionType">The type of the exception the read throws</param>
    [TestMethod]
    [DataRow(typeof(UnauthorizedAccessException))]
    [DataRow(typeof(OperationCanceledException))]
    public void PipeConnectionUnexpectedReadExceptionRaisesReadFailed(Type exceptionType)
    {
        // Arrange
        var stream = EnqueueStream(VersionFrame(2));

        stream.Fail((Exception)Activator.CreateInstance(exceptionType)!);

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(1));

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving), ErrorStatus(PipeErrorKind.ReadFailed) },
                                _events.ToArray(),
                                "An unexpected read exception should raise an error with the read-failed kind");
        Assert.IsTrue(stream.IsDisposed, "The failed connection should be closed");
        Assert.AreSequenceEqual(Seconds(0, 1), AttemptOffsets(), "A new attempt should follow after the retry delay");
    }

    /// <summary>
    /// An attempt that fails for another reason raises an error with the Windows error code.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionFailedAttemptRaisesErrorWithCode()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.FromFailure(new UnauthorizedAccessException(), true));

        var connection = CreateConnection();

        // Act
        StartRun(connection);

        await WaitForAttemptsAsync(1);

        // Assert
        Assert.AreSequenceEqual(new[] { CodeStatus(5) }, _events.ToArray(), "Access denied should raise an error with code 5");
    }

    /// <summary>
    /// The result of the next attempt replaces the error of a failed attempt.
    /// </summary>
    [TestMethod]
    public void PipeConnectionFailedAttemptReplacedByNextAttempt()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.FromFailure(new UnauthorizedAccessException(), true));
        _connector.Enqueue(PipeConnectResult.NotFound);

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(1));

        // Assert
        Assert.AreSequenceEqual(new[] { CodeStatus(5), Status(PipeConnectionState.WaitingForGame) },
                                _events.ToArray(),
                                "The next attempt's result should replace the error");
    }

    /// <summary>
    /// An exception of the connector raises an error without a code.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionConnectorExceptionRaisesErrorWithoutCode()
    {
        // Arrange
        _connector.EnqueueException(new InvalidOperationException("The connector failed."));

        var connection = CreateConnection();

        // Act
        StartRun(connection);

        await WaitForAttemptsAsync(1);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Error) }, _events.ToArray(), "A connector exception should raise an error without code");
        Assert.AreEqual(Status(PipeConnectionState.Error), connection.Status, "The status should be an error without code");
    }

    /// <summary>
    /// Stopping during an attempt ends the run without the clock being advanced and raises no event.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionCancelDuringAttemptCompletes()
    {
        // Arrange
        _connector.EnqueueHeld();

        StartRun(CreateConnection());

        var run = _runs[0];

        await WaitForAttemptsAsync(1);

        // Act
        await _stop.CancelAsync();
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(TaskStatus.RanToCompletion, run.Status, "The stop should end the run normally");
        Assert.IsEmpty(_events, "No event should follow the stop");
    }

    /// <summary>
    /// Stopping during a retry delay ends the run without the clock being advanced and raises no event.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionCancelDuringDelayCompletes()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.Busy);

        StartRun(CreateConnection());

        var run = _runs[0];

        await WaitForAttemptsAsync(1);

        // Act
        await _stop.CancelAsync();
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(TaskStatus.RanToCompletion, run.Status, "The stop should end the run normally");
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.PipeBusy) }, _events.ToArray(), "No event should follow the stop");
        Assert.AreEqual(1, _connector.AttemptCount, "No attempt should follow the stop");
    }

    /// <summary>
    /// Stopping during a read ends the run without the clock being advanced and raises no event.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionCancelDuringReadCompletes()
    {
        // Arrange
        await StartConnectedAsync(VersionFrame(2));

        var run = _runs[0];

        // Act
        await _stop.CancelAsync();
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(TaskStatus.RanToCompletion, run.Status, "The stop should end the run normally");
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving) },
                                _events.ToArray(),
                                "No event should follow the stop");
    }

    /// <summary>
    /// Stopping during a read closes the pipe.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionCancelDuringReadDisposesStream()
    {
        // Arrange
        var stream = await StartConnectedAsync(VersionFrame(2));
        var run = _runs[0];

        // Act
        await _stop.CancelAsync();
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(stream.IsDisposed, "The stop should close the pipe");
    }

    /// <summary>
    /// A second run while one runs throws.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionRunWhileRunningThrows()
    {
        // Arrange
        var connection = CreateConnection();

        StartRun(connection);

        await WaitForAttemptsAsync(1);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.RunAsync(_stop.Token), "A second run while one runs should throw");
    }

    /// <summary>
    /// The events follow the changes of the state in order, with their values and causes.
    /// </summary>
    [TestMethod]
    public void PipeConnectionRaisesEventsInOrder()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.NotFound);

        var stream = EnqueueStream(VersionFrame(2));

        stream.End();

        var connection = CreateConnection();

        StartRun(connection);

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(1));

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving), Status(PipeConnectionState.WaitingForGame) },
                                _events.ToArray(),
                                "Waiting for game should be followed by connected, receiving and waiting for game, once each");
        Assert.AreEqual(Status(PipeConnectionState.WaitingForGame), connection.Status, "The status should be the last event");
    }

    /// <summary>
    /// A change of the cause alone raises an event; the same cause again does not.
    /// </summary>
    [TestMethod]
    public void PipeConnectionCauseChangeRaisesEvent()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.FromFailure(new UnauthorizedAccessException(), true));
        _connector.Enqueue(PipeConnectResult.FromFailure(new UnauthorizedAccessException(), true));
        _connector.Enqueue(PipeConnectResult.FromFailure(new IOException("Sharing violation", unchecked((int)0x80070020)), true));

        var stream = EnqueueStream(VersionFrame(2));

        stream.Fail(new IOException("Broken pipe"));

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(7));

        // Assert
        Assert.AreSequenceEqual(new[]
                                {
                                    CodeStatus(5),
                                    CodeStatus(32),
                                    Status(PipeConnectionState.Connected),
                                    Status(PipeConnectionState.Receiving),
                                    ErrorStatus(PipeErrorKind.ReadFailed)
                                },
                                _events.ToArray(),
                                "Each change of the cause should raise one event, a repeated cause none");
    }

    /// <summary>
    /// A handler that throws does not stop the connection.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionThrowingHandlerDoesNotStopConnection()
    {
        // Arrange
        var stream = EnqueueStream();
        var connection = new PipeConnection(_connector, _clock);

        connection.StatusChanged += ThrowingHandler;

        StartRun(connection);

        var run = _runs[0];

        await WaitForPendingReadsAsync(stream, 1);

        // Act
        stream.Feed(VersionFrame(2));

        await WaitForPendingReadsAsync(stream, 2);

        // Assert
        Assert.AreEqual(Status(PipeConnectionState.Receiving), connection.Status, "The connection should go on to receiving");
        Assert.IsFalse(run.IsCompleted, "The run should go on");
    }

    /// <summary>
    /// A handler that throws does not keep the other handlers from their events.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionThrowingHandlerOtherHandlersReceive()
    {
        // Arrange
        var stream = EnqueueStream(VersionFrame(2));
        var connection = new PipeConnection(_connector, _clock);

        connection.StatusChanged += ThrowingHandler;
        connection.StatusChanged += (_, status) => _events.Enqueue(status);

        // Act
        StartRun(connection);

        await WaitForPendingReadsAsync(stream, 1);

        // Assert
        Assert.AreSequenceEqual(new[] { Status(PipeConnectionState.Connected), Status(PipeConnectionState.Receiving) },
                                _events.ToArray(),
                                "The handler after the throwing one should receive every event");
    }

    /// <summary>
    /// The connection never writes to the pipe.
    /// </summary>
    [TestMethod]
    public void PipeConnectionNeverWritesToStream()
    {
        // Arrange
        var stream = EnqueueStream(VersionFrame(2), SessionStartFrame(), IslandFrame(), SessionEndFrame(), VersionFrame(3), SessionEndFrame());

        stream.End();

        // Act
        StartRun(CreateConnection());

        // Assert
        Assert.IsTrue(stream.IsDisposed, "The whole connection should have run");
        Assert.AreEqual(0, stream.WriteCallCount, "The connection should never write to the pipe");
    }

    /// <summary>
    /// Frames queued at once are all read without the clock being advanced.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeConnectionReadsAllFramesWithoutClockAdvance()
    {
        // Arrange
        var stream = EnqueueStream([VersionFrame(2), .. Enumerable.Repeat(SessionEndFrame(), 19)]);

        // Act
        StartRun(CreateConnection());

        await WaitForPendingReadsAsync(stream, 1);

        // Assert
        Assert.AreEqual(0, stream.UnreadByteCount, "All twenty frames should be read");
        Assert.AreEqual(1, stream.PendingReadCount, "The connection should wait for the next frame");
        Assert.AreEqual(_start, _clock.GetUtcNow(), "The clock should not have moved");
    }

    /// <summary>
    /// A missing pipe raises no pipe busy and the next attempt follows after one second, without a wait of its own.
    /// </summary>
    [TestMethod]
    public void PipeConnectionNotFoundRaisesNoBusyAndNoWait()
    {
        // Arrange
        EnqueueResults(PipeConnectResult.NotFound, 2);

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(1));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1), AttemptOffsets(), "The next attempt should be due one second after the missing pipe, not three");
        Assert.IsEmpty(_events, "A missing pipe should never raise pipe busy");
    }

    /// <summary>
    /// A connection that reaches receiving and fails at once does not reset the retry delay.
    /// </summary>
    [TestMethod]
    public void PipeConnectionShortReceivingDoesNotResetDelay()
    {
        // Arrange
        EnqueueStream(VersionFrame(2), PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));
        EnqueueStream(VersionFrame(2), PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));
        EnqueueStream(VersionFrame(2), PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(7));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1, 3, 7), AttemptOffsets(), "Short connections should keep the delays growing: 1, 2, 4 s");
    }

    /// <summary>
    /// A connection that was in receiving for 30 seconds resets the retry delay; 1 ms less does not.
    /// </summary>
    /// <param name="receivingMilliseconds">The time in receiving before the stream ends</param>
    /// <param name="expectedDelayMilliseconds">The expected delay before the next attempt</param>
    [TestMethod]
    [DataRow(29999, 4000)]
    [DataRow(30000, 1000)]
    public void PipeConnectionReceivingThirtySecondsResetsDelay(int receivingMilliseconds, int expectedDelayMilliseconds)
    {
        // Arrange
        var receiving = TimeSpan.FromMilliseconds(receivingMilliseconds);

        EnqueueResults(PipeConnectResult.NotFound, 2);

        var stream = EnqueueStream(VersionFrame(2));

        StartRun(CreateConnection());
        AdvanceInSteps(TimeSpan.FromSeconds(3));

        _clock.Advance(receiving);
        stream.End();

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(5));

        // Assert
        TimeSpan[] expected = [.. Seconds(0, 1, 3), TimeSpan.FromSeconds(3) + receiving + TimeSpan.FromMilliseconds(expectedDelayMilliseconds)];

        Assert.AreSequenceEqual(expected, AttemptOffsets(), "Only 30 seconds in receiving should reset the delay to one second");
    }

    /// <summary>
    /// Two stretches in receiving interrupted by a mismatch do not add up to the 30 seconds that reset the delay.
    /// </summary>
    [TestMethod]
    public void PipeConnectionInterruptedReceivingDoesNotResetDelay()
    {
        // Arrange
        EnqueueResults(PipeConnectResult.NotFound, 2);

        var stream = EnqueueStream(VersionFrame(2));

        StartRun(CreateConnection());
        AdvanceInSteps(TimeSpan.FromSeconds(3));

        _clock.Advance(TimeSpan.FromSeconds(20));
        stream.Feed(VersionFrame(3));
        stream.Feed(VersionFrame(2));
        _clock.Advance(TimeSpan.FromSeconds(20));
        stream.End();

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(5));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1, 3, 47), AttemptOffsets(), "An interrupted stretch in receiving should not reset the delay of 4 s");
    }

    /// <summary>
    /// One retry counter spans not found, busy and failed attempts.
    /// </summary>
    [TestMethod]
    public void PipeConnectionOneCounterSpansFailureKinds()
    {
        // Arrange
        _connector.Enqueue(PipeConnectResult.NotFound);
        _connector.Enqueue(PipeConnectResult.Busy);
        _connector.Enqueue(PipeConnectResult.FromFailure(new UnauthorizedAccessException(), true));

        StartRun(CreateConnection());

        // Act
        AdvanceInSteps(TimeSpan.FromSeconds(7));

        // Assert
        Assert.AreSequenceEqual(Seconds(0, 1, 3, 7), AttemptOffsets(), "The delays should grow across failure kinds: 1, 2, 4 s");
    }

    /// <summary>
    /// Creates the given number of seconds as offsets.
    /// </summary>
    /// <param name="seconds">The seconds</param>
    /// <returns>The offsets</returns>
    private static TimeSpan[] Seconds(params int[] seconds)
    {
        return [.. seconds.Select(value => TimeSpan.FromSeconds(value))];
    }

    /// <summary>
    /// Creates a status without a cause.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The status</returns>
    private static PipeConnectionStatus Status(PipeConnectionState state)
    {
        return new PipeConnectionStatus
               {
                   State = state
               };
    }

    /// <summary>
    /// Creates an error status with a pipe error kind.
    /// </summary>
    /// <param name="kind">The error kind</param>
    /// <returns>The status</returns>
    private static PipeConnectionStatus ErrorStatus(PipeErrorKind kind)
    {
        return new PipeConnectionStatus
               {
                   State = PipeConnectionState.Error,
                   ErrorKind = kind
               };
    }

    /// <summary>
    /// Creates an error status with a Windows error code.
    /// </summary>
    /// <param name="code">The error code</param>
    /// <returns>The status</returns>
    private static PipeConnectionStatus CodeStatus(int code)
    {
        return new PipeConnectionStatus
               {
                   State = PipeConnectionState.Error,
                   ErrorCode = code
               };
    }

    /// <summary>
    /// Creates a protocol mismatch status.
    /// </summary>
    /// <param name="version">The received version, if any</param>
    /// <returns>The status</returns>
    private static PipeConnectionStatus MismatchStatus(int? version)
    {
        return new PipeConnectionStatus
               {
                   State = PipeConnectionState.ProtocolMismatch,
                   ReceivedVersion = version
               };
    }

    /// <summary>
    /// A status handler that always throws.
    /// </summary>
    /// <param name="sender">The connection</param>
    /// <param name="status">The status</param>
    private static void ThrowingHandler(object? sender, PipeConnectionStatus status)
    {
        throw new InvalidOperationException("The handler failed.");
    }

    /// <summary>
    /// Ends a stream with the given error.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="kind">The error kind</param>
    private static void EndWithError(ControlledPipeStream stream, PipeErrorKind kind)
    {
        switch (kind)
        {
            case PipeErrorKind.TruncatedFrame:
                {
                    stream.Feed(PartialFrame());
                    stream.End();
                }
                break;

            case PipeErrorKind.OversizedFrame:
                {
                    stream.Feed(PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));
                }
                break;

            default:
                {
                    stream.Fail(new IOException("Broken pipe"));
                }
                break;
        }
    }

    /// <summary>
    /// Joins frames into one byte sequence.
    /// </summary>
    /// <param name="frames">The frames</param>
    /// <returns>The bytes</returns>
    private static byte[] Concat(params byte[][] frames)
    {
        return [.. frames.SelectMany(frame => frame)];
    }

    /// <summary>
    /// Creates a version frame with its length prefix.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The bytes</returns>
    private static byte[] VersionFrame(int version)
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.Version).Int32(version).BuildWithPrefix();
    }

    /// <summary>
    /// Creates a session-start frame with its length prefix.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] SessionStartFrame()
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.SessionStart).String("Head").BuildWithPrefix();
    }

    /// <summary>
    /// Creates a session-end frame with its length prefix.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] SessionEndFrame()
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.SessionEnd).BuildWithPrefix();
    }

    /// <summary>
    /// Creates an island frame (area production statistics) with its length prefix.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] IslandFrame()
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.AreaProductionStatistics)
                                     .Byte(1)
                                     .Byte(2)
                                     .Byte(0)
                                     .Int32(-77)
                                     .String("Area")
                                     .Int64(123456789012L)
                                     .Int32(1)
                                     .Entry(10, [new(1, 2)], [new(3, 4)])
                                     .BuildWithPrefix();
    }

    /// <summary>
    /// Creates the start of a frame: a length prefix of 8 and two of its bytes.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] PartialFrame()
    {
        return [.. PipeFrameBuilder.RawLength(8), (byte)PipeMessageType.SessionEnd, 0];
    }

    /// <summary>
    /// Creates the connection under test with a handler that records its events.
    /// </summary>
    /// <returns>The connection</returns>
    private PipeConnection CreateConnection()
    {
        var connection = new PipeConnection(_connector, _clock);

        connection.StatusChanged += (_, status) => _events.Enqueue(status);

        return connection;
    }

    /// <summary>
    /// Starts a run that the cleanup of the test stops.
    /// </summary>
    /// <param name="connection">The connection</param>
    private void StartRun(PipeConnection connection)
    {
        _runs.Add(connection.RunAsync(_stop.Token));
    }

    /// <summary>
    /// Queues the same attempt result a number of times.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="count">The number of attempts</param>
    private void EnqueueResults(PipeConnectResult result, int count)
    {
        for (var index = 0; index < count; index++)
        {
            _connector.Enqueue(result);
        }
    }

    /// <summary>
    /// Queues an attempt that opens a stream already fed with the given frames.
    /// </summary>
    /// <param name="frames">The frames, each with its length prefix</param>
    /// <returns>The stream</returns>
    private ControlledPipeStream EnqueueStream(params byte[][] frames)
    {
        var stream = new ControlledPipeStream();

        stream.Feed(Concat(frames));

        _connector.Enqueue(PipeConnectResult.FromStream(stream));

        return stream;
    }

    /// <summary>
    /// Starts a run whose first attempt opens a stream fed with the given frames, and waits until it reads.
    /// </summary>
    /// <param name="frames">The frames, each with its length prefix</param>
    /// <returns>The stream</returns>
    private async Task<ControlledPipeStream> StartConnectedAsync(params byte[][] frames)
    {
        var stream = EnqueueStream(frames);

        StartRun(CreateConnection());

        await WaitForPendingReadsAsync(stream, 1);

        return stream;
    }

    /// <summary>
    /// Moves the clock forward in small steps, so every timer fires at its due time.
    /// </summary>
    /// <param name="total">The time to move</param>
    private void AdvanceInSteps(TimeSpan total)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < total; elapsed += _step)
        {
            _clock.Advance(_step);
        }
    }

    /// <summary>
    /// Gets the times of the attempts as offsets from the start of the clock.
    /// </summary>
    /// <returns>The offsets</returns>
    private TimeSpan[] AttemptOffsets()
    {
        return [.. _connector.AttemptTimes.Select(time => time - _start)];
    }

    /// <summary>
    /// Waits until the connector was asked for the given number of attempts.
    /// </summary>
    /// <param name="count">The number of attempts</param>
    /// <returns>The task</returns>
    private Task WaitForAttemptsAsync(int count)
    {
        return _connector.WaitForAttemptsAsync(count, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Waits until the given number of reads of the stream wait for bytes.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="count">The number of pending reads</param>
    /// <returns>The task</returns>
    private Task WaitForPendingReadsAsync(ControlledPipeStream stream, int count)
    {
        return stream.WaitForPendingReadsAsync(count, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
    }

    #endregion // Methods
}