namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="SingleInstanceGuard" />.
/// </summary>
[TestClass]
public sealed class SingleInstanceGuardTests
{
    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The wait of a restarted guard that the holder releases in time, short enough for the test's runtime.
    /// </summary>
    private static readonly TimeSpan _longWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The wait of a restarted guard that runs out while the name is held.
    /// </summary>
    private static readonly TimeSpan _shortWait = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The real time a waiting guard is watched for not having returned yet.
    /// </summary>
    private static readonly TimeSpan _pendingCheck = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The events that let the holding threads of the test end, set in the cleanup.
    /// </summary>
    private readonly List<ManualResetEventSlim> _releases = [];

    /// <summary>
    /// The threads of the test, joined in the cleanup.
    /// </summary>
    private readonly List<Thread> _threads = [];

    /// <summary>
    /// The mutexes whose owning thread ended without releasing them, disposed in the cleanup.
    /// </summary>
    private readonly List<Mutex> _abandoned = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleInstanceGuardTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public SingleInstanceGuardTests(TestContext testContext)
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
    /// Lets every holding thread end, joins the threads and disposes the abandoned mutexes.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var release in _releases)
        {
            release.Set();
        }

        foreach (var thread in _threads)
        {
            thread.Join(_longWait + _guard);
        }

        foreach (var release in _releases)
        {
            release.Dispose();
        }

        foreach (var mutex in _abandoned)
        {
            mutex.Dispose();
        }
    }

    /// <summary>
    /// The first guard on a name acquires it.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardFirstTryAcquireReturnsTrue()
    {
        // Arrange
        using var guard = new SingleInstanceGuard(CreateName());

        // Act
        var acquired = guard.TryAcquire();

        // Assert
        Assert.IsTrue(acquired, "The first guard on a name should acquire it");
    }

    /// <summary>
    /// A second guard on the same name does not acquire it.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardSecondGuardWithSameNameReturnsFalse()
    {
        // Arrange
        var name = CreateName();
        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);
        var firstAcquired = first.TryAcquire();

        // Act
        var secondAcquired = second.TryAcquire();

        // Assert
        Assert.IsTrue(firstAcquired, "The first guard should acquire the name");
        Assert.IsFalse(secondAcquired, "The second guard on the same name should not acquire it");
    }

    /// <summary>
    /// After every guard on a name is disposed, the name can be acquired again.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardAfterDisposeNameCanBeAcquiredAgain()
    {
        // Arrange
        var name = CreateName();
        var first = new SingleInstanceGuard(name);
        var second = new SingleInstanceGuard(name);
        var firstAcquired = first.TryAcquire();
        var secondAcquired = second.TryAcquire();

        second.Dispose();
        first.Dispose();

        using var third = new SingleInstanceGuard(name);

        // Act
        var thirdAcquired = third.TryAcquire();

        // Assert
        Assert.IsTrue(firstAcquired, "The first guard should acquire the name");
        Assert.IsFalse(secondAcquired, "The second guard should not acquire the name while the first holds it");
        Assert.IsTrue(thirdAcquired, "The name should be free again once every earlier guard is disposed");
    }

    /// <summary>
    /// Disposing a guard that did not acquire the name does not release the owner.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardDisposeOfNonOwnerKeepsNameHeld()
    {
        // Arrange
        var name = CreateName();
        using var first = new SingleInstanceGuard(name);
        var firstAcquired = first.TryAcquire();
        var second = new SingleInstanceGuard(name);
        var secondAcquired = second.TryAcquire();

        // Act
        second.Dispose();

        using var third = new SingleInstanceGuard(name);
        var thirdAcquired = third.TryAcquire();

        // Assert
        Assert.IsTrue(firstAcquired, "The first guard should acquire the name");
        Assert.IsFalse(secondAcquired, "The second guard should not acquire the name");
        Assert.IsFalse(thirdAcquired, "The name should stay held by the first guard after the second one is disposed");
    }

    /// <summary>
    /// The default name is session-local.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardDefaultNameIsSessionLocal()
    {
        // Arrange
        const string expectedPrefix = "Local\\";

        // Act
        var name = SingleInstanceGuard.DefaultName;

        // Assert
        Assert.Contains("\\", name, "The default name should contain the backslash character");
        Assert.StartsWith(expectedPrefix, name, StringComparison.Ordinal, "The default name should start with Local followed by a backslash");
    }

    /// <summary>
    /// A restarted guard waits while the previous Companion holds the name and acquires it once that one releases it, so
    /// the new Companion comes up while the old one shuts down (S3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SingleInstanceGuardWaitAcquiresAfterHolderReleases()
    {
        // Arrange
        var name = CreateName();
        var holderRelease = CreateRelease();
        var holderAcquired = await StartHolder(name, holderRelease).WaitAsync(_guard, TestContext.CancellationToken);
        var waiter = StartWaiter(name, _longWait);
        var isPendingWhileHeld = await IsPendingAsync(waiter);

        // Act
        holderRelease.Set();

        var acquired = await waiter.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(holderAcquired, "The previous Companion should hold the name");
        Assert.IsTrue(isPendingWhileHeld, "The restarted guard should wait while the previous Companion holds the name");
        Assert.IsTrue(acquired, "The restarted guard should acquire the name once the previous Companion released it");
    }

    /// <summary>
    /// A restarted guard whose wait runs out while the name is still held does not acquire it (E15).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SingleInstanceGuardWaitTimesOutWhileHeld()
    {
        // Arrange
        var name = CreateName();
        var holderAcquired = await StartHolder(name, CreateRelease()).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        var acquired = await StartWaiter(name, _shortWait).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(holderAcquired, "The previous Companion should hold the name");
        Assert.IsFalse(acquired, "The restarted guard should not acquire a name that is still held when its wait runs out");
    }

    /// <summary>
    /// A name abandoned by a holder that ended without releasing it counts as acquired, as a previous Companion that was
    /// killed during the restart leaves it (S3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SingleInstanceGuardWaitAbandonedNameCountsAsAcquired()
    {
        // Arrange
        var name = CreateName();

        Abandon(name);

        // Act
        var acquired = await StartWaiter(name, _longWait).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(acquired, "A name abandoned by its holder should count as acquired");
    }

    /// <summary>
    /// A Companion started by hand, without the restart's wait, still fails at once while a restarted one waits for the
    /// previous one (S3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task SingleInstanceGuardTryAcquireWithoutWaitStillFailsAtOnce()
    {
        // Arrange
        var name = CreateName();
        var holderAcquired = await StartHolder(name, CreateRelease()).WaitAsync(_guard, TestContext.CancellationToken);
        var waiter = StartWaiter(name, _longWait);
        var isPendingBefore = await IsPendingAsync(waiter);

        using var handStarted = new SingleInstanceGuard(name);

        // Act
        var acquired = handStarted.TryAcquire();

        // Assert
        Assert.IsTrue(holderAcquired, "The previous Companion should hold the name");
        Assert.IsTrue(isPendingBefore, "The restarted guard should wait while the previous Companion holds the name");
        Assert.IsFalse(acquired, "A guard without a wait should fail at once while the name is held");
        Assert.IsFalse(waiter.IsCompleted, "The restarted guard should still wait after the hand-started one failed");
    }

    /// <summary>
    /// Acquires the name on the current thread and keeps the guard until the release is set, so the owning thread also
    /// releases the mutex.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="acquire">The acquisition</param>
    /// <param name="release">The event that ends the hold</param>
    /// <param name="acquired">The completion source of the result</param>
    private static void Hold(string name, Func<SingleInstanceGuard, bool> acquire, ManualResetEventSlim release, TaskCompletionSource<bool> acquired)
    {
        using var guard = new SingleInstanceGuard(name);

        try
        {
            acquired.SetResult(acquire(guard));
        }
        catch (Exception exception)
        {
            acquired.SetException(exception);

            return;
        }

        release.Wait(_longWait + _guard);
    }

    /// <summary>
    /// Creates a mutex name that no other test or run uses.
    /// </summary>
    /// <returns>A unique session-local mutex name</returns>
    private static string CreateName()
    {
        return "Local\\Censio.Tests." + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Starts a thread that acquires the name without a wait and holds it until the release is set.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="release">The event that ends the hold</param>
    /// <returns>A task that completes with the result of the acquisition</returns>
    private Task<bool> StartHolder(string name, ManualResetEventSlim release)
    {
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        StartThread(() => Hold(name, guard => guard.TryAcquire(), release, acquired));

        return acquired.Task;
    }

    /// <summary>
    /// Starts a thread that waits for the name as a restarted Companion does and holds it until the cleanup.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="wait">The longest time to wait</param>
    /// <returns>A task that completes with the result of the wait, or faults with the exception of the guard</returns>
    private Task<bool> StartWaiter(string name, TimeSpan wait)
    {
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = CreateRelease();

        StartThread(() => Hold(name, guard => guard.TryAcquire(wait), release, acquired));

        return acquired.Task;
    }

    /// <summary>
    /// Takes the name on a thread that ends without releasing it.
    /// </summary>
    /// <param name="name">The name</param>
    private void Abandon(string name)
    {
        Mutex? mutex = null;
        var thread = new Thread(() => mutex = new Mutex(true, name, out _));

        thread.Start();
        thread.Join(_guard);

        _abandoned.Add(mutex!);
    }

    /// <summary>
    /// Watches a waiting guard for a short real time and tells whether it is still waiting.
    /// </summary>
    /// <param name="waiter">The task of the waiting guard</param>
    /// <returns><see langword="true" /> when the guard did not return within the watched time</returns>
    private async Task<bool> IsPendingAsync(Task<bool> waiter)
    {
        var finished = await Task.WhenAny(waiter, Task.Delay(_pendingCheck, TestContext.CancellationToken));

        return finished != waiter;
    }

    /// <summary>
    /// Creates an event that ends a hold, set at the latest in the cleanup.
    /// </summary>
    /// <returns>The event</returns>
    private ManualResetEventSlim CreateRelease()
    {
        var release = new ManualResetEventSlim();

        _releases.Add(release);

        return release;
    }

    /// <summary>
    /// Starts a background thread of the test.
    /// </summary>
    /// <param name="body">The body of the thread</param>
    private void StartThread(Action body)
    {
        var thread = new Thread(() => body())
                     {
                         IsBackground = true
                     };

        _threads.Add(thread);
        thread.Start();
    }

    #endregion // Methods
}