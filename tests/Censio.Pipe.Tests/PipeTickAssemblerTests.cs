using System.Globalization;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeTickAssembler" />.
/// </summary>
[TestClass]
public sealed class PipeTickAssemblerTests
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

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the test context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The quiet period closes the open tick while the next read is pending.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerQuietPeriodClosesTickWhileReadPending()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();

        source.Enqueue(Frame(1, "Ostia", 100));

        using var assembler = new PipeTickAssembler(source, clock);

        var request = StartRead(assembler, TestContext.CancellationToken);

        clock.Advance(PipeTickLimits.QuietPeriod - TimeSpan.FromMilliseconds(1));

        var completedEarly = request.IsCompleted;

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(1));

        var result = await request.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(completedEarly, "Nothing should be delivered 1 ms before the quiet period ends");

        var tick = AssertTick(result, PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(1, tick.Islands, "The tick should hold the island");
        Assert.AreEqual(_start, tick.ReceiveTime, "The tick should carry the receive time of its frame");
        Assert.AreEqual(2, source.ReadCount, "The source should have been read for the frame and once more");
        Assert.IsFalse(source.TokenCancelled, "The pending read should not be cancelled");
    }

    /// <summary>
    /// A frame that arrives before the quiet period ends joins the tick, and nothing is delivered before the new deadline.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerBeforeQuietPeriodDeliversNothing()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        var early = PipeTickLimits.QuietPeriod - TimeSpan.FromMilliseconds(1);

        source.Enqueue(Frame(1, "Ostia", 100));
        source.Enqueue(Frame(2, "Neapolis", 100));

        var held = source.EnqueueHeld();

        using var assembler = new PipeTickAssembler(source, clock);

        var request = StartRead(assembler, TestContext.CancellationToken);

        clock.Advance(early);

        var completedEarly = request.IsCompleted;

        // Act
        held.Release(Frame(3, "Capua", 100));

        clock.Advance(PipeTickLimits.QuietPeriod);

        var result = await request.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(completedEarly, "Nothing should be delivered 1 ms before the quiet period ends");

        var tick = AssertTick(result, PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(3, tick.Islands, "The frame that arrived before the deadline should be in the tick");
        Assert.AreEqual(_start + early, tick.ReceiveTime, "The tick should carry the receive time of the late frame");
    }

    /// <summary>
    /// A read that was pending while the quiet period closed the tick is delivered on a later request, read once and not cancelled.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerPendingReadDeliveredAfterQuietClose()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();

        source.Enqueue(Frame(1, "Ostia", 100));

        var held = source.EnqueueHeld();

        using var assembler = new PipeTickAssembler(source, clock);

        var first = StartRead(assembler, TestContext.CancellationToken);

        clock.Advance(PipeTickLimits.QuietPeriod);

        var quiet = await first.WaitAsync(_guard, TestContext.CancellationToken);
        var readsAfterQuietClose = source.ReadCount;

        held.Release(Frame(2, "Neapolis", 100));

        var second = StartRead(assembler, TestContext.CancellationToken);

        // Act
        clock.Advance(PipeTickLimits.QuietPeriod);

        var result = await second.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.HasCount(1, AssertTick(quiet, PipeTickCloseReason.QuietPeriod).Islands, "The first tick should hold the first frame only");
        Assert.AreEqual(2, readsAfterQuietClose, "The pending read should not have been replaced");

        var tick = AssertTick(result, PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(1, tick.Islands, "The second tick should hold the frame of the pending read");
        Assert.AreEqual("Neapolis", tick.Islands[0].Name, "The second tick should hold the frame of the pending read");
        Assert.AreEqual(_start + PipeTickLimits.QuietPeriod, tick.ReceiveTime, "The frame should be stamped when its read completed");
        Assert.AreEqual(3, source.ReadCount, "The frame should have been read once and one more read started");
        Assert.IsFalse(source.TokenCancelled, "The pending read should never be cancelled");
    }

    /// <summary>
    /// A read that completed and a clock past the deadline give the quiet-period tick and then the tick of the frame.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerCompletedReadPastDeadlineGivesQuietTickThenFrame()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();

        source.Enqueue(Frame(1, "Ostia", 100));

        var held = source.EnqueueHeld();

        using var assembler = new PipeTickAssembler(source, clock);

        var first = StartRead(assembler, TestContext.CancellationToken);

        clock.Advance(PipeTickLimits.QuietPeriod);

        var quiet = await first.WaitAsync(_guard, TestContext.CancellationToken);

        held.Release(Frame(2, "Neapolis", 100));

        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var second = await StartRead(assembler, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual("Ostia", AssertTick(quiet, PipeTickCloseReason.QuietPeriod).Islands[0].Name, "The first tick should hold the first frame");

        var tick = AssertTick(second, PipeTickCloseReason.QuietPeriod);

        Assert.AreEqual("Neapolis", tick.Islands[0].Name, "The second tick should hold the frame that completed");
        Assert.AreEqual(_start + PipeTickLimits.QuietPeriod, tick.ReceiveTime, "The frame should carry the time its read completed");
    }

    /// <summary>
    /// The end of the stream delivers the open tick, then the end, and every later request returns the end.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerAfterEndReturnsEnd()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();

        source.Enqueue(Frame(1, "Ostia", 100));
        source.Enqueue(PipeReadResult.End);

        using var assembler = new PipeTickAssembler(source, clock);

        // Act
        var tick = await StartRead(assembler, TestContext.CancellationToken);
        var end = await StartRead(assembler, TestContext.CancellationToken);
        var again = await StartRead(assembler, TestContext.CancellationToken);
        var third = await StartRead(assembler, TestContext.CancellationToken);

        // Assert
        AssertTick(tick, PipeTickCloseReason.StreamEnded);
        Assert.AreEqual(PipeTickResultKind.EndOfStream, end.Kind, "The end should follow the tick");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, again.Kind, "A later request should return the end");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, third.Kind, "Every later request should return the end");
        Assert.AreEqual(2, source.ReadCount, "The ended stream should not be read again");
    }

    /// <summary>
    /// A cancelled request throws, cancels the read, discards the open tick and ends the stream.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerCancelledReadThrowsAndEndsStream()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        source.Enqueue(Frame(1, "Ostia", 100));

        using var assembler = new PipeTickAssembler(source, clock);

        var request = StartRead(assembler, cancellation.Token);

        // Act
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => request, "The cancelled request should throw the cancellation");

        var next = await StartRead(assembler, TestContext.CancellationToken);

        Assert.IsTrue(source.TokenCancelled, "The source should have seen its token cancelled");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, next.Kind, "The next request should return the end and not the discarded tick");
        Assert.AreEqual(2, source.ReadCount, "The ended stream should not be read again");
    }

    /// <summary>
    /// A token that is already cancelled throws although items are queued.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerPreCancelledTokenThrowsWithQueuedItems()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        using var cancellation = new CancellationTokenSource();

        source.Enqueue(Frame(1, "Ostia", 100));
        source.Enqueue(PipeReadResult.End);

        using var assembler = new PipeTickAssembler(source, clock);

        var tick = await StartRead(assembler, TestContext.CancellationToken);

        await cancellation.CancelAsync();

        // Act
        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => StartRead(assembler, cancellation.Token), "A cancelled token should throw although the end is queued");

        var next = await StartRead(assembler, TestContext.CancellationToken);

        // Assert
        AssertTick(tick, PipeTickCloseReason.StreamEnded);
        Assert.IsNotNull(thrown, "The cancellation should be thrown");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, next.Kind, "The next request should return the end");
    }

    /// <summary>
    /// A request after the dispose throws an object-disposed exception.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerReadAfterDisposeThrowsObjectDisposed()
    {
        // Arrange
        var assembler = new PipeTickAssembler(new ScriptedPipeMessageSource(), new FakeTimeProvider(_start));

        assembler.Dispose();

        // Act and Assert
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => StartRead(assembler, TestContext.CancellationToken), "A request after the dispose should throw");
    }

    /// <summary>
    /// The dispose cancels the pending read.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerDisposeCancelsPendingRead()
    {
        // Arrange
        var source = new ScriptedPipeMessageSource();
        var assembler = new PipeTickAssembler(source, new FakeTimeProvider(_start));

        source.Enqueue(Frame(1, "Ostia", 100));

        var request = StartRead(assembler, TestContext.CancellationToken);
        var observed = request.ContinueWith(static finished => finished.Exception, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        // Act
        assembler.Dispose();

        // Assert
        Assert.IsTrue(source.TokenCancelled, "The dispose should cancel the token of the pending read");

        await observed.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Completing the reads at once or releasing held reads later gives the same results.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerSynchronousAndHeldReadsGiveSameResults()
    {
        // Arrange
        var script = new (PipeReadResult Result, int Seconds)[]
                     {
                         (Frame(1, "Ostia", 100), 0),
                         (Frame(2, "Neapolis", 100), 1),
                         (PipeReadResult.FromMessage(new VersionMessage(2)), 2),
                         (Frame(3, "Capua", 200), 3),
                         (PipeReadResult.End, 4)
                     };
        var readsPerRequest = new[] { 3, 1, 1, 0 };

        // Act
        var synchronous = await RunSynchronouslyAsync(script, readsPerRequest.Length);
        var held = await RunHeldAsync(script, readsPerRequest);

        // Assert
        Assert.HasCount(readsPerRequest.Length, synchronous, "Every request should give a result");
        Assert.AreSequenceEqual(synchronous, held, "Held reads should give the same results as synchronous reads");
        Assert.AreEqual("Version", synchronous[0], "The version should come first");
        Assert.StartsWith("Tick GameTimeChanged 100 ", synchronous[1], "The game time change should close the first tick");
        Assert.StartsWith("Tick StreamEnded 200 ", synchronous[2], "The end should close the second tick");
        Assert.AreEqual("EndOfStream", synchronous[3], "The end should come last");
    }

    /// <summary>
    /// The read observer gets each read result with the stamp the assembler read for it, the end included.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerReadObserverGetsEachResultWithItsStamp()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        var frame = Frame(1, "Ostia", 100);
        var version = PipeReadResult.FromMessage(new VersionMessage(2));
        var observed = new List<(PipeReadResult Result, DateTimeOffset Stamp)>();

        source.Enqueue(frame, () => clock.SetUtcNow(_start.AddSeconds(1)));
        source.Enqueue(version, () => clock.SetUtcNow(_start.AddSeconds(2)));
        source.Enqueue(PipeReadResult.End, () => clock.SetUtcNow(_start.AddSeconds(3)));

        using var assembler = new PipeTickAssembler(source, clock, (result, stamp) => observed.Add((result, stamp)));

        // Act
        var results = await DrainAsync(assembler);

        // Assert
        Assert.AreEqual(PipeTickResultKind.EndOfStream, results[^1].Kind, "The stream should end");
        Assert.HasCount(3, observed, "The observer should see every read result once");
        Assert.AreSame(frame, observed[0].Result, "The observer should see the first result itself");
        Assert.AreSame(version, observed[1].Result, "The observer should see the second result itself");
        Assert.AreSame(PipeReadResult.End, observed[2].Result, "The observer should see the end");
        Assert.AreEqual(_start.AddSeconds(1), observed[0].Stamp, "The first stamp should be the clock after the first read");
        Assert.AreEqual(_start.AddSeconds(2), observed[1].Stamp, "The second stamp should be the clock after the second read");
        Assert.AreEqual(_start.AddSeconds(3), observed[2].Stamp, "The third stamp should be the clock after the third read");
    }

    /// <summary>
    /// A read observer that throws is ignored and the assembler delivers the same items.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerThrowingReadObserverKeepsReading()
    {
        // Arrange
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        var calls = 0;

        source.Enqueue(Frame(1, "Ostia", 100));
        source.Enqueue(PipeReadResult.End);

        void Observe(PipeReadResult result, DateTimeOffset stamp)
        {
            calls++;

            throw new InvalidOperationException("The observer failed");
        }

        using var assembler = new PipeTickAssembler(source, clock, Observe);

        // Act
        var results = await DrainAsync(assembler);

        // Assert
        Assert.AreEqual(2, calls, "The observer should have been called for each read result");
        Assert.HasCount(2, results, "The tick and the end should be delivered");

        var tick = AssertTick(results[0], PipeTickCloseReason.StreamEnded);

        Assert.HasCount(1, tick.Islands, "The tick should hold the island");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, results[1].Kind, "The end should follow");
    }

    /// <summary>
    /// A clock set back far over the delay limit after an island makes the assembler wait in steps instead of throwing.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeTickAssemblerClockBackOverDelayLimitWaitsInSteps()
    {
        // Arrange
        var clock = new BackwardsFakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();

        source.Enqueue(Frame(1, "Ostia", 100));

        var held = source.EnqueueHeld();

        using var assembler = new PipeTickAssembler(source, clock);

        var skippedRequest = StartRead(assembler, TestContext.CancellationToken);

        clock.MoveBack(TimeSpan.FromDays(60));
        held.Release(PipeReadResult.FromError(new PipeError
                                              {
                                                  Kind = PipeErrorKind.EmptyFrame
                                              }));

        await skippedRequest.WaitAsync(_guard, TestContext.CancellationToken);

        var request = StartRead(assembler, TestContext.CancellationToken);
        var faultedAfterSkippedFrame = request.IsFaulted;

        clock.Advance(TimeSpan.FromDays(1));
        clock.Advance(TimeSpan.FromDays(1));

        var endedEarly = request.IsCompleted;

        // Act
        clock.Advance(TimeSpan.FromDays(58) + PipeTickLimits.QuietPeriod);

        await Task.WhenAny(request.WaitAsync(_guard, TestContext.CancellationToken));

        // Assert
        Assert.IsFalse(faultedAfterSkippedFrame, "A deadline 60 days ahead of the clock should not make the read throw");
        Assert.IsFalse(endedEarly, "Two days of waiting should not close the tick or end the read");
        Assert.IsFalse(request.IsFaulted, "The wait should end without an exception");

        var tick = AssertTick(request.Result, PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(1, tick.Islands, "The tick should hold the island");
        Assert.AreEqual(_start, tick.ReceiveTime, "The tick should carry the receive time of its frame");
    }

    /// <summary>
    /// Summarizes a result by its kind, close reason, game time, receive time and island keys.
    /// </summary>
    /// <param name="result">The result</param>
    /// <returns>The summary</returns>
    private static string Summarize(PipeTickResult result)
    {
        if (result.Tick is null)
        {
            return result.Kind == PipeTickResultKind.Version ? "Version" : result.Kind.ToString();
        }

        var keys = string.Join(",", result.Tick.Islands.Select(island => island.Key.SessionGuid.ToString(CultureInfo.InvariantCulture)));

        return string.Create(CultureInfo.InvariantCulture, $"Tick {result.Tick.CloseReason} {result.Tick.GameTime} {result.Tick.ReceiveTime:O} [{keys}]");
    }

    /// <summary>
    /// Creates an island frame result without entries.
    /// </summary>
    /// <param name="sessionGuid">The session GUID, which tells the islands apart</param>
    /// <param name="name">The area name</param>
    /// <param name="timestamp">The timestamp</param>
    /// <returns>The read result</returns>
    private static PipeReadResult Frame(int sessionGuid, string name, long timestamp)
    {
        return PipeReadResult.FromMessage(new AreaProductionStatisticsMessage
                                          {
                                              SessionId = 7,
                                              IslandId = 1,
                                              AreaIndex = 0,
                                              SessionGuid = sessionGuid,
                                              AreaName = name,
                                              Timestamp = timestamp,
                                              Entries = []
                                          });
    }

    /// <summary>
    /// Asserts that a result is a tick with the given close reason.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="reason">The expected close reason</param>
    /// <returns>The tick</returns>
    private static PipeTick AssertTick(PipeTickResult result, PipeTickCloseReason reason)
    {
        Assert.AreEqual(PipeTickResultKind.Tick, result.Kind, "The result should be a tick");
        Assert.IsNotNull(result.Tick, "The result should carry the tick");
        Assert.AreEqual(reason, result.Tick.CloseReason, "The tick should carry the expected close reason");

        return result.Tick;
    }

    /// <summary>
    /// Requests the next result of the assembler.
    /// </summary>
    /// <param name="assembler">The assembler</param>
    /// <param name="cancellationToken">The token of the request</param>
    /// <returns>The task of the request</returns>
    private static Task<PipeTickResult> StartRead(PipeTickAssembler assembler, CancellationToken cancellationToken)
    {
        return assembler.ReadAsync(cancellationToken).AsTask();
    }

    /// <summary>
    /// Reads the assembler until the end of the stream.
    /// </summary>
    /// <param name="assembler">The assembler</param>
    /// <returns>All results, the end last</returns>
    private async Task<List<PipeTickResult>> DrainAsync(PipeTickAssembler assembler)
    {
        var results = new List<PipeTickResult>();

        for (var count = 0; count < 100; count++)
        {
            var result = await assembler.ReadAsync(TestContext.CancellationToken);

            results.Add(result);

            if (result.Kind == PipeTickResultKind.EndOfStream)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Runs the script with reads that complete at once, each setting the clock to its time.
    /// </summary>
    /// <param name="script">The results with their seconds after the start</param>
    /// <param name="requests">The number of requests</param>
    /// <returns>The summaries of the results</returns>
    private async Task<List<string>> RunSynchronouslyAsync((PipeReadResult Result, int Seconds)[] script, int requests)
    {
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();

        foreach (var (result, seconds) in script)
        {
            source.Enqueue(result, () => clock.SetUtcNow(_start.AddSeconds(seconds)));
        }

        using var assembler = new PipeTickAssembler(source, clock);

        var summaries = new List<string>();

        for (var index = 0; index < requests; index++)
        {
            var result = await StartRead(assembler, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

            summaries.Add(Summarize(result));
        }

        return summaries;
    }

    /// <summary>
    /// Runs the script with held reads that are released one by one at their times.
    /// </summary>
    /// <param name="script">The results with their seconds after the start</param>
    /// <param name="readsPerRequest">The number of reads each request consumes</param>
    /// <returns>The summaries of the results</returns>
    private async Task<List<string>> RunHeldAsync((PipeReadResult Result, int Seconds)[] script, int[] readsPerRequest)
    {
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        var reads = script.Select(_ => source.EnqueueHeld()).ToList();

        using var assembler = new PipeTickAssembler(source, clock);

        var summaries = new List<string>();
        var next = 0;

        foreach (var count in readsPerRequest)
        {
            var request = StartRead(assembler, TestContext.CancellationToken);

            for (var read = 0; read < count; read++)
            {
                await source.WaitForReadsAsync(next + 1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

                clock.SetUtcNow(_start.AddSeconds(script[next].Seconds));
                reads[next].Release(script[next].Result);

                next++;
            }

            summaries.Add(Summarize(await request.WaitAsync(_guard, TestContext.CancellationToken)));
        }

        return summaries;
    }

    #endregion // Methods
}