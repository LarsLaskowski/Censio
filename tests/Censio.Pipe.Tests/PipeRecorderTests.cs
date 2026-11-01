using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeRecorder" />.
/// </summary>
[TestClass]
public sealed class PipeRecorderTests
{
    #region Fields

    /// <summary>
    /// The receive time of the first frame of most tests.
    /// </summary>
    private static readonly DateTimeOffset _start = new DateTimeOffset(2026, 11, 1, 17, 9, 0, TimeSpan.Zero).AddTicks(1234567);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecorderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeRecorderTests(TestContext testContext)
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
    /// Gets the exceptions a file can fail with, and whether the failing call is a flush.
    /// </summary>
    private static IEnumerable<object[]> WriteFailures
    {
        get
        {
            yield return [new IOException("The disk is full"), false];
            yield return [new IOException("The disk is full"), true];
            yield return [new UnauthorizedAccessException("Access is denied"), false];
            yield return [new DirectoryNotFoundException("The folder was deleted"), true];
        }
    }

    /// <summary>
    /// Gets the exceptions a file factory can fail with.
    /// </summary>
    private static IEnumerable<object[]> OpenFailures
    {
        get
        {
            yield return [new IOException("The folder cannot be created")];
            yield return [new UnauthorizedAccessException("Access is denied")];
            yield return [new DirectoryNotFoundException("The folder was deleted")];
            yield return [new InvalidOperationException("The folder is not a full path")];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The cost of an entry is the base64 length of the frame plus the overhead.
    /// </summary>
    /// <param name="frameLength">The frame length</param>
    /// <param name="expected">The expected cost</param>
    [TestMethod]
    [DataRow(0, 128L)]
    [DataRow(1, 132L)]
    [DataRow(2, 132L)]
    [DataRow(3, 132L)]
    [DataRow(4, 136L)]
    [DataRow(786432, 1048704L)]
    [DataRow(1048576, 1398232L)]
    public void PipeRecorderEntryCostIsBase64LengthPlusOverhead(int frameLength, long expected)
    {
        // Arrange

        // Act
        var cost = PipeRecorder.EntryCost(frameLength);

        // Assert
        Assert.AreEqual(expected, cost, "The cost should be 4 x ceil(length / 3) + 128");
    }

    /// <summary>
    /// Every frame is in the file exactly once and in order, after the header.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderRecordsEachFrameOnceInOrder()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        byte[][] frames = [[2], [3, 1, 2], [], [0xFB, 0xEF, 0xBE]];

        // Act
        recorder.Record(Result(frames[0]), _start);
        recorder.Record(Result(frames[1]), _start.AddSeconds(1));
        recorder.Record(Result(frames[2]), _start.AddSeconds(2));
        recorder.Record(Result(frames[3]), _start.AddSeconds(2));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        var expected = string.Concat(PipeRecordingFormat.FormatHeader(_start),
                                     PipeRecordingFormat.FormatFrameLine(_start, frames[0]),
                                     PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(1), frames[1]),
                                     PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(2), frames[2]),
                                     PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(2), frames[3]));

        Assert.AreEqual(expected, stream.Text, "The file should hold the header and each frame once, in order");
        Assert.IsFalse(recorder.HasFailed, "Nothing failed");
    }

    /// <summary>
    /// A result without a frame is not recorded and does not open the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderResultWithoutFrameIsNotRecorded()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var opened = new List<DateTimeOffset>();
        var recorder = new PipeRecorder(Open(stream, opened));

        // Act
        recorder.Record(PipeReadResult.End, _start.AddSeconds(-2));
        recorder.Record(PipeReadResult.FromMessage(new SessionEndMessage()), _start.AddSeconds(-1));

        var openedBeforeFrame = opened.Count;

        recorder.Record(Result(2), _start);
        recorder.Record(PipeReadResult.End, _start.AddSeconds(1));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(0, openedBeforeFrame, "No file should be opened for results without a frame");
        Assert.AreEqual(PipeRecordingFormat.FormatHeader(_start) + PipeRecordingFormat.FormatFrameLine(_start, [2]), stream.Text, "Only the result with a frame should be in the file");
    }

    /// <summary>
    /// Completing writes the queued frames, all as complete lines, and closes the file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderCompleteWritesQueuedFramesAsCompleteLines()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);

        stream.HoldWrites();
        recorder.Record(Result(2), _start);
        recorder.Record(Result(3, 4), _start.AddSeconds(1));
        recorder.Record(Result(5, 6, 7), _start.AddSeconds(2));

        await stream.WaitForWritesAsync(1, TestContext.CancellationToken);

        // Act
        var completion = recorder.CompleteAsync(TestContext.CancellationToken);
        var completedWhileHeld = completion.IsCompleted;

        stream.ReleaseWrites();

        await completion.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var lines = stream.Text.Split('\n');

        Assert.IsFalse(completedWhileHeld, "Completing should wait for the writer");
        Assert.HasCount(5, lines, "The file should hold the header and three frame lines, each ended by a line feed");
        Assert.AreEqual(string.Empty, lines[^1], "The file should end with a line feed");
        Assert.IsTrue(stream.IsDisposed, "The file should be closed");
    }

    /// <summary>
    /// Each line is flushed after it is written, so every flush ends at a line boundary.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderFlushesEachLine()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        var headerBytes = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatHeader(_start));
        var firstLine = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatFrameLine(_start, [2]));
        var secondLine = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(1), [3, 4]));
        var thirdLine = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(2), [5, 6, 7]));

        // Act
        recorder.Record(Result(2), _start);
        recorder.Record(Result(3, 4), _start.AddSeconds(1));
        recorder.Record(Result(5, 6, 7), _start.AddSeconds(2));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        var flushed = stream.FlushedLengths;

        Assert.Contains(headerBytes + firstLine, flushed, "The first frame line should be flushed");
        Assert.Contains(headerBytes + firstLine + secondLine, flushed, "The second frame line should be flushed");
        Assert.Contains(headerBytes + firstLine + secondLine + thirdLine, flushed, "The third frame line should be flushed");
        Assert.AreEqual(stream.Length, flushed[^1], "The last flush should cover everything written");
    }

    /// <summary>
    /// The lines already written free their queue space, so a long run is not limited to the queue size in total.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderWrittenLinesFreeQueueSpace()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        var failures = 0;
        var frame = new byte[786432];
        var result = Result(frame);
        var headerBytes = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatHeader(_start));
        var lineBytes = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatFrameLine(_start, frame));

        recorder.Failed += (_, _) => Interlocked.Increment(ref failures);
        RecordMany(recorder, 10, result, _start);

        await stream.WaitForFlushedBytesAsync(headerBytes + (10L * lineBytes), TestContext.CancellationToken);

        // Act
        RecordMany(recorder, 10, result, _start);

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsGreaterThan(PipeRecorder.MaxQueuedBytes, 20 * PipeRecorder.EntryCost(frame.Length), "The twenty frames should not fit the queue together");
        Assert.AreEqual(0, failures, "The recorder should not have failed");
        Assert.IsFalse(recorder.HasFailed, "The recorder should not have failed");
        Assert.AreEqual(headerBytes + (20L * lineBytes), stream.Length, "All twenty frames should be in the file");
    }

    /// <summary>
    /// A frame that does not fit the queue fails the recorder once, and the frames already queued are still written.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderQueueOverLimitFailsAndKeepsQueuedFrames()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        var failures = 0;
        var frame = new byte[786336];
        var headerBytes = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatHeader(_start));
        var lineBytes = Encoding.UTF8.GetByteCount(PipeRecordingFormat.FormatFrameLine(_start, frame));

        recorder.Failed += (_, _) => Interlocked.Increment(ref failures);
        stream.HoldWrites();
        RecordMany(recorder, 16, Result(frame), _start);

        await stream.WaitForWritesAsync(1, TestContext.CancellationToken);

        var failedAtLimit = recorder.HasFailed;

        // Act
        recorder.Record(Result(2), _start.AddSeconds(1));

        var failedOverLimit = recorder.HasFailed;
        var failuresOverLimit = failures;

        recorder.Record(Result(3), _start.AddSeconds(2));
        stream.ReleaseWrites();

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecorder.MaxQueuedBytes, 16 * PipeRecorder.EntryCost(frame.Length), "Sixteen frames should cost exactly the queue size");
        Assert.IsFalse(failedAtLimit, "A queue exactly at the limit should not fail");
        Assert.IsTrue(failedOverLimit, "The frame over the limit should fail the recorder at once");
        Assert.AreEqual(1, failuresOverLimit, "The failure should be raised once, on the call that detected it");
        Assert.AreEqual(1, failures, "No later call should raise the failure again");
        Assert.AreEqual(headerBytes + (16L * lineBytes), stream.Length, "The sixteen queued frames should be written, the later ones not");
    }

    /// <summary>
    /// Empty frames count their overhead, so the queue takes 131,072 of them and fails at the next one.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderEmptyFramesOnHeldStreamFailAtEntryLimit()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        var empty = Result();

        stream.HoldWrites();
        RecordMany(recorder, 131072, empty, _start);

        var failedAtLimit = recorder.HasFailed;

        // Act
        recorder.Record(empty, _start);

        var failedOverLimit = recorder.HasFailed;

        stream.ReleaseWrites();

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(failedAtLimit, "131,072 empty frames should fit the queue");
        Assert.IsTrue(failedOverLimit, "The 131,073rd empty frame should fail the recorder");
        Assert.AreEqual(131073, stream.ToArray().AsSpan().Count((byte)'\n'), "The header and the 131,072 queued frames should be written");
    }

    /// <summary>
    /// A stalled write does not make Record wait.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderRecordDoesNotWaitForStalledWrite()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);

        stream.HoldWrites();
        recorder.Record(Result(2), _start);

        await stream.WaitForWritesAsync(1, TestContext.CancellationToken);

        // Act
        var recording = Task.Run(() => RecordMany(recorder, 3, Result(3), _start.AddSeconds(1)), TestContext.CancellationToken);

        await recording.WaitAsync(_guard, TestContext.CancellationToken);
        stream.ReleaseWrites();

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(recording.IsCompletedSuccessfully, "Recording should return while the write is stalled");
        Assert.HasCount(5, stream.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries), "The header and all four frames should be written once the write is released");
    }

    /// <summary>
    /// A write or a flush that fails fails the recorder once, leaves only complete lines in the file and drops the later frames.
    /// </summary>
    /// <param name="exception">The exception of the failing call</param>
    /// <param name="onFlush">A value indicating whether the flush fails instead of the write</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DynamicData(nameof(WriteFailures))]
    public async Task PipeRecorderWriteFailureFailsAndKeepsCompleteLines(Exception exception, bool onFlush)
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        var failures = 0;
        var kept = string.Concat(PipeRecordingFormat.FormatHeader(_start),
                                 PipeRecordingFormat.FormatFrameLine(_start, [2]),
                                 PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(1), [3]));
        var dropped = PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(3), [5]);

        recorder.Failed += (_, _) => Interlocked.Increment(ref failures);
        FailAfter(stream, onFlush, Encoding.UTF8.GetByteCount(kept), exception);

        // Act
        recorder.Record(Result(2), _start);
        recorder.Record(Result(3), _start.AddSeconds(1));
        recorder.Record(Result(4), _start.AddSeconds(2));
        recorder.Record(Result(5), _start.AddSeconds(3));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        var text = stream.Text;

        Assert.IsTrue(recorder.HasFailed, "The recorder should have failed");
        Assert.AreEqual(1, failures, "The failure should be raised once");
        Assert.StartsWith(kept, text, "The lines before the failure should be in the file");
        Assert.EndsWith("\n", text, "The file should end with a complete line");
        Assert.DoesNotContain(dropped, text, "The frames after the failure should not be written");
        Assert.IsTrue(stream.IsDisposed, "The file should be closed after the failure");
    }

    /// <summary>
    /// A factory that fails fails the recorder once and is not called again.
    /// </summary>
    /// <param name="exception">The exception of the factory</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DynamicData(nameof(OpenFailures))]
    public async Task PipeRecorderOpenFailureFails(Exception exception)
    {
        // Arrange
        var failures = 0;
        var openCalls = 0;
        var recorder = new PipeRecorder(_ =>
                                        {
                                            openCalls++;

                                            throw exception;
                                        });

        recorder.Failed += (_, _) => Interlocked.Increment(ref failures);

        // Act
        recorder.Record(Result(2), _start);
        recorder.Record(Result(3), _start.AddSeconds(1));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(recorder.HasFailed, "The recorder should have failed");
        Assert.AreEqual(1, failures, "The failure should be raised once");
        Assert.AreEqual(1, openCalls, "The file should not be opened again");
    }

    /// <summary>
    /// A cancelled token stops the wait of Complete although the writer is stalled.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderCompleteCancelledStopsWaiting()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        stream.HoldWrites();
        recorder.Record(Result(2), _start);

        await stream.WaitForWritesAsync(1, TestContext.CancellationToken);

        var completion = recorder.CompleteAsync(cancellation.Token);

        // Act
        await cancellation.CancelAsync();

        var finished = await Task.WhenAny(completion, Task.Delay(_guard, TestContext.CancellationToken));

        // Assert
        Assert.AreSame(completion, finished, "Complete should stop waiting for the stalled writer");

        stream.ReleaseWrites();

        await recorder.DisposeAsync();
    }

    /// <summary>
    /// A handler of Failed that throws is ignored and the other handlers are still called.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderThrowingFailedHandlerIsIgnored()
    {
        // Arrange
        var secondHandlerCalls = 0;
        var recorder = new PipeRecorder(_ => throw new IOException("The folder cannot be created"));

        recorder.Failed += (_, _) => throw new InvalidOperationException("The handler failed");
        recorder.Failed += (_, _) => Interlocked.Increment(ref secondHandlerCalls);

        // Act
        recorder.Record(Result(2), _start);

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(recorder.HasFailed, "The recorder should have failed");
        Assert.AreEqual(1, secondHandlerCalls, "The handler after the throwing one should be called");
    }

    /// <summary>
    /// The file is opened at the first frame with the receive time of that frame, once.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderOpensFileAtFirstFrameWithItsTime()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var opened = new List<DateTimeOffset>();
        var recorder = new PipeRecorder(Open(stream, opened));

        // Act
        recorder.Record(PipeReadResult.End, _start.AddSeconds(-1));
        recorder.Record(Result(2), _start);
        recorder.Record(Result(3), _start.AddSeconds(5));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        Assert.HasCount(1, opened, "The file should be opened once");
        Assert.AreEqual(_start, opened[0], "The file should be opened with the receive time of the first frame");
    }

    /// <summary>
    /// The header with the receive time of the first frame comes before the first frame line.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeRecorderWritesHeaderBeforeFirstFrame()
    {
        // Arrange
        var stream = new ControlledWriteStream();
        var recorder = new PipeRecorder(_ => stream);

        // Act
        recorder.Record(Result(2), _start);

        await recorder.CompleteAsync(TestContext.CancellationToken);

        // Assert
        var expected = """{"format":"censio-pipe-recording","version":1,"started":"2026-11-01T17:09:00.1234567Z"}""" + "\n"
                       + """{"time":"2026-11-01T17:09:00.1234567Z","frame":"Ag=="}""" + "\n";

        Assert.AreEqual(expected, stream.Text, "The file should start with the header and then hold the frame line");
    }

    /// <summary>
    /// Creates the factory of a recorder that hands out the stream and notes the start times it was called with.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="opened">The list that receives the start times</param>
    /// <returns>The factory</returns>
    private static Func<DateTimeOffset, Stream> Open(ControlledWriteStream stream, List<DateTimeOffset> opened)
    {
        return time =>
               {
                   opened.Add(time);

                   return stream;
               };
    }

    /// <summary>
    /// Creates the result of a message read from a frame.
    /// </summary>
    /// <param name="frame">The frame bytes</param>
    /// <returns>The result</returns>
    private static PipeReadResult Result(params byte[] frame)
    {
        return PipeReadResult.FromMessage(new SessionEndMessage()).WithFrame(frame);
    }

    /// <summary>
    /// Records the same result several times.
    /// </summary>
    /// <param name="recorder">The recorder</param>
    /// <param name="count">The number of times</param>
    /// <param name="result">The result</param>
    /// <param name="time">The receive time</param>
    private static void RecordMany(PipeRecorder recorder, int count, PipeReadResult result, DateTimeOffset time)
    {
        for (var index = 0; index < count; index++)
        {
            recorder.Record(result, time);
        }
    }

    /// <summary>
    /// Makes a write or a flush of the stream fail once the content is over a length.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="onFlush">A value indicating whether the flush fails instead of the write</param>
    /// <param name="length">The length of the content that is still fine</param>
    /// <param name="exception">The exception</param>
    private static void FailAfter(ControlledWriteStream stream, bool onFlush, long length, Exception exception)
    {
        if (onFlush)
        {
            stream.FailFlushesAfter(length, exception);
        }
        else
        {
            stream.FailWritesAfter(length, exception);
        }
    }

    #endregion // Methods
}