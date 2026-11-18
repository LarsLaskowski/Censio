using System.Collections.Concurrent;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="SparklineLoader{TKey}" />, with read delegates whose answers the test gives.
/// </summary>
[TestClass]
public sealed class SparklineLoaderTests
{
    #region Constants

    /// <summary>
    /// How long the test waits to see that nothing more happens, in milliseconds.
    /// </summary>
    private const int NegativeWaitMilliseconds = 300;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The address of the tests' first page.
    /// </summary>
    private static readonly object _address = (Guid.Parse("8c0b8e43-5d2a-4f55-9f0f-1d2d3c4b5a69"), 1);

    /// <summary>
    /// The address of another island of the same profile.
    /// </summary>
    private static readonly object _otherAddress = (Guid.Parse("8c0b8e43-5d2a-4f55-9f0f-1d2d3c4b5a69"), 2);

    /// <summary>
    /// The receive time of the latest tick of the first request.
    /// </summary>
    private static readonly DateTimeOffset _asOf = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The reads the loader started: the label of the request, the held read it was given and its cancellation token.
    /// </summary>
    private readonly ConcurrentQueue<(string Label, SparklineRead<int>? Held, CancellationToken Token)> _calls = new();

    /// <summary>
    /// The number of times the loader reported a change.
    /// </summary>
    private int _changedCount;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SparklineLoaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public SparklineLoaderTests(TestContext testContext)
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
    /// Requests made while a read runs mark one more read: once the running read answers, exactly one read follows, for the
    /// newest request, and it is given the answered read as the held one; its answer is then held.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SparklineLoaderRequestDuringReadReadsOnceMoreWithNewest()
    {
        // Arrange
        using var loader = new SparklineLoader<int>();

        var first = Answer();
        var second = Answer();
        var third = Answer();
        var firstRead = ReadOf();
        var thirdRead = ReadOf();

        // Act
        loader.Request(_address, _asOf, Read("first", first), Changed);

        var firstStarted = await WaitUntilAsync(() => _calls.Count == 1);

        loader.Request(_address, _asOf.AddMinutes(2), Read("second", second), Changed);
        loader.Request(_address, _asOf.AddMinutes(4), Read("third", third), Changed);
        first.SetResult(firstRead);

        var thirdStarted = await WaitUntilAsync(() => _calls.Count == 2);

        third.SetResult(thirdRead);

        var thirdHeld = await WaitUntilAsync(() => ReferenceEquals(loader.HeldFor(_address), thirdRead));

        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        // Assert
        var calls = _calls.ToList();

        Assert.IsTrue(firstStarted, "The first request should start a read");
        Assert.IsTrue(thirdStarted, "One more read should start once the first one answered");
        Assert.IsTrue(thirdHeld, "The answer of the newest request should be held");
        Assert.AreSequenceEqual(ExpectedLabels("first", "third"), calls.Select(call => call.Label), "Only the newest of the requests made during the read should be read");
        Assert.AreSame(firstRead, calls[1].Held, "The next read should be given the held read of the address for the refresh");
    }

    /// <summary>
    /// A request for the address and as-of time of the read in flight or of the held read reads nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SparklineLoaderSameAsOfReadsNothing()
    {
        // Arrange
        using var loader = new SparklineLoader<int>();

        var first = Answer();
        var firstRead = ReadOf();

        // Act
        loader.Request(_address, _asOf, Read("first", first), Changed);

        var started = await WaitUntilAsync(() => _calls.Count == 1);

        loader.Request(_address, _asOf, Read("during", Answer()), Changed);
        first.SetResult(firstRead);

        var held = await WaitUntilAsync(() => ReferenceEquals(loader.HeldFor(_address), firstRead));

        loader.Request(_address, _asOf, Read("after", Answer()), Changed);

        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(started, "The first request should start a read");
        Assert.IsTrue(held, "The answer should be held");
        Assert.AreSequenceEqual(ExpectedLabels("first"), _calls.Select(call => call.Label), "A request for the same address and as-of time should read nothing, in flight or held");
        Assert.AreSame(firstRead, loader.HeldFor(_address), "The held read should stay");
    }

    /// <summary>
    /// A request for another address cancels the read in flight, drops the held read of the earlier address and reads
    /// the new address without a held read; its answer is held for the new address only.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SparklineLoaderAddressChangeCancelsAndDropsHeld()
    {
        // Arrange
        using var loader = new SparklineLoader<int>();

        var firstRead = ReadOf();
        var otherRead = ReadOf();
        var other = Answer();

        loader.Request(_address, _asOf, Read("first", Answered(firstRead)), Changed);

        var held = await WaitUntilAsync(() => ReferenceEquals(loader.HeldFor(_address), firstRead));

        // Act
        loader.Request(_address, _asOf.AddMinutes(2), Read("refresh", Answer()), Changed);

        var refreshStarted = await WaitUntilAsync(() => _calls.Count == 2);

        loader.Request(_otherAddress, _asOf.AddMinutes(2), Read("other", other), Changed);

        var otherStarted = await WaitUntilAsync(() => _calls.Count == 3);
        var heldWhileOtherRuns = loader.HeldFor(_address);

        other.SetResult(otherRead);

        var otherHeld = await WaitUntilAsync(() => ReferenceEquals(loader.HeldFor(_otherAddress), otherRead));

        // Assert
        var calls = _calls.ToList();

        Assert.IsTrue(held, "The first answer should be held");
        Assert.IsTrue(refreshStarted, "The refresh of the first address should start");
        Assert.IsTrue(otherStarted, "The other address should be read");
        Assert.IsTrue(calls[1].Token.IsCancellationRequested, "The read in flight for the earlier address should be cancelled");
        Assert.IsNull(heldWhileOtherRuns, "The held read of the earlier address should be dropped");
        Assert.IsNull(calls[2].Held, "The read of the other address should be given no held read");
        Assert.IsTrue(otherHeld, "The answer for the other address should be held");
        Assert.IsNull(loader.HeldFor(_address), "No read should be held for the earlier address");
    }

    /// <summary>
    /// A cancelled read stores nothing and marks no failure, so the next request of the address reads again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SparklineLoaderCancelledReadKeepsNothing()
    {
        // Arrange
        using var loader = new SparklineLoader<int>();

        var laterRead = ReadOf();

        // Act
        loader.Request(_address, _asOf, Read("cancelled", Cancelled()), Changed);

        var started = await WaitUntilAsync(() => _calls.Count == 1);

        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        var heldAfterCancel = loader.HeldFor(_address);
        var failedAfterCancel = loader.IsFailedFor(_address);

        loader.Request(_address, _asOf.AddMinutes(2), Read("later", Answered(laterRead)), Changed);

        var laterHeld = await WaitUntilAsync(() => ReferenceEquals(loader.HeldFor(_address), laterRead));

        // Assert
        Assert.IsTrue(started, "The request should start a read");
        Assert.IsNull(heldAfterCancel, "A cancelled read should store nothing");
        Assert.IsFalse(failedAfterCancel, "A cancelled read should mark no failure");
        Assert.IsTrue(laterHeld, "The next request should read again and hold its answer");
        Assert.IsNull(_calls.ToList()[1].Held, "The next read should be given no held read");
    }

    /// <summary>
    /// A read that fails, by a failed answer or by an exception of the read delegate outside the reader, marks the address
    /// failed and reports the change, lets nothing escape, and keeps the address from being read again until another
    /// address is requested, which drops the failure.
    /// </summary>
    /// <param name="throws">Whether the read delegate throws instead of answering a failed read</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SparklineLoaderFailedReadMarksAddressFailedAndReadsNoMore(bool throws)
    {
        // Arrange
        using var loader = new SparklineLoader<int>();

        var failing = throws ? Throwing("failing") : Read("failing", Answered(FailedRead()));

        // Act
        loader.Request(_address, _asOf, failing, Changed);

        var failed = await WaitUntilAsync(() => loader.IsFailedFor(_address));
        var changedAfterFailure = Volatile.Read(ref _changedCount);

        loader.Request(_address, _asOf.AddMinutes(2), Read("retry", Answered(ReadOf())), Changed);

        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        var callsBeforeOther = _calls.Count;

        loader.Request(_otherAddress, _asOf.AddMinutes(2), Read("other", Answered(ReadOf())), Changed);

        var otherRead = await WaitUntilAsync(() => loader.HeldFor(_otherAddress) is not null);

        // Assert
        Assert.IsTrue(failed, "The address should be marked failed");
        Assert.IsGreaterThanOrEqualTo(1, changedAfterFailure, "The failure should be reported as a change, so the page shows it");
        Assert.IsNull(loader.HeldFor(_address), "A failed read should hold nothing");
        Assert.AreEqual(1, callsBeforeOther, "A failed address should not be read again");
        Assert.IsTrue(otherRead, "The loader should go on reading another address");
        Assert.IsFalse(loader.IsFailedFor(_address), "Another address should drop the failure");
    }

    /// <summary>
    /// Disposing the loader cancels the read in flight, and an answer that comes after the disposal reports no change.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task SparklineLoaderDisposedCancelsReadAndSkipsChanged()
    {
        // Arrange
        var loader = new SparklineLoader<int>();
        var answer = Answer();

        loader.Request(_address, _asOf, ReadIgnoringToken("held", answer), Changed);

        var started = await WaitUntilAsync(() => _calls.Count == 1);

        // Act
        loader.Dispose();
        answer.SetResult(ReadOf());

        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(started, "The request should start a read");
        Assert.IsTrue(_calls.ToList()[0].Token.IsCancellationRequested, "Disposing the loader should cancel the read in flight");
        Assert.AreEqual(0, Volatile.Read(ref _changedCount), "An answer after the disposal should report no change");
    }

    /// <summary>
    /// Gives the labels of the reads a test expects, in their order.
    /// </summary>
    /// <param name="labels">The labels</param>
    /// <returns>The labels</returns>
    private static List<string> ExpectedLabels(params string[] labels)
    {
        return [.. labels];
    }

    /// <summary>
    /// Creates a read with no keys.
    /// </summary>
    /// <returns>The read</returns>
    private static SparklineRead<int> ReadOf()
    {
        return new SparklineRead<int>
               {
                   Lines = new Dictionary<int, SparklineView?>(),
                   Samples = new Dictionary<int, IReadOnlyList<SparklineSample>>(),
                   IsFailed = false
               };
    }

    /// <summary>
    /// Creates a failed read.
    /// </summary>
    /// <returns>The read</returns>
    private static SparklineRead<int> FailedRead()
    {
        return new SparklineRead<int>
               {
                   Lines = new Dictionary<int, SparklineView?>(),
                   Samples = new Dictionary<int, IReadOnlyList<SparklineSample>>(),
                   IsFailed = true
               };
    }

    /// <summary>
    /// Creates the source of an answer the test gives later.
    /// </summary>
    /// <returns>The source</returns>
    private static TaskCompletionSource<SparklineRead<int>> Answer()
    {
        return new TaskCompletionSource<SparklineRead<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Creates the source of an answer given at once.
    /// </summary>
    /// <param name="read">The answer</param>
    /// <returns>The source</returns>
    private static TaskCompletionSource<SparklineRead<int>> Answered(SparklineRead<int> read)
    {
        var source = Answer();

        source.SetResult(read);

        return source;
    }

    /// <summary>
    /// Creates the source of an answer that ends by cancellation, as a reader's read does when its page is cancelled.
    /// </summary>
    /// <returns>The source</returns>
    private static TaskCompletionSource<SparklineRead<int>> Cancelled()
    {
        var source = Answer();

        source.SetCanceled();

        return source;
    }

    /// <summary>
    /// Reports a change of the loader, as the page's re-render does.
    /// </summary>
    /// <returns>A completed task</returns>
    private Task Changed()
    {
        Interlocked.Increment(ref _changedCount);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a read delegate that records its call and answers what the source gives, or ends when its token is cancelled.
    /// </summary>
    /// <param name="label">The label of the request</param>
    /// <param name="answer">The source of the answer</param>
    /// <returns>The delegate</returns>
    private Func<SparklineRead<int>?, CancellationToken, Task<SparklineRead<int>>> Read(string label, TaskCompletionSource<SparklineRead<int>> answer)
    {
        return (held, token) =>
               {
                   _calls.Enqueue((label, held, token));

                   return answer.Task.WaitAsync(token);
               };
    }

    /// <summary>
    /// Creates a read delegate that records its call and answers what the source gives, whatever its token says.
    /// </summary>
    /// <param name="label">The label of the request</param>
    /// <param name="answer">The source of the answer</param>
    /// <returns>The delegate</returns>
    private Func<SparklineRead<int>?, CancellationToken, Task<SparklineRead<int>>> ReadIgnoringToken(string label, TaskCompletionSource<SparklineRead<int>> answer)
    {
        return (held, token) =>
               {
                   _calls.Enqueue((label, held, token));

                   return answer.Task;
               };
    }

    /// <summary>
    /// Creates a read delegate that records its call and throws before it gives a task, as a page's delegate could while
    /// building its keys.
    /// </summary>
    /// <param name="label">The label of the request</param>
    /// <returns>The delegate</returns>
    private Func<SparklineRead<int>?, CancellationToken, Task<SparklineRead<int>>> Throwing(string label)
    {
        return (held, token) =>
               {
                   _calls.Enqueue((label, held, token));

                   throw new InvalidOperationException("The keys of the page could not be built");
               };
    }

    /// <summary>
    /// Waits until a condition holds, at most <see cref="PageTestHost.WaitBound" />.
    /// </summary>
    /// <param name="condition">The condition</param>
    /// <returns><see langword="true" /> when the condition held in time</returns>
    private async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        var deadline = TimeProvider.System.GetUtcNow() + PageTestHost.WaitBound;

        while (condition() == false)
        {
            if (TimeProvider.System.GetUtcNow() > deadline)
            {
                return false;
            }

            await Task.Delay(10, TestContext.CancellationToken);
        }

        return true;
    }

    #endregion // Methods
}