using System.Collections.Concurrent;
using System.Text;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IconReader" />: one icon read at a time, over a read function that holds the first read open.
/// </summary>
[TestClass]
public sealed class IconReaderTests
{
    #region Constants

    /// <summary>
    /// The reference of the read that is held open.
    /// </summary>
    private const string FirstReference = "first";

    /// <summary>
    /// The reference of the read that waits for the first.
    /// </summary>
    private const string SecondReference = "second";

    /// <summary>
    /// The reference of a read after the first ended.
    /// </summary>
    private const string LaterReference = "later";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The longest the test waits for a read.
    /// </summary>
    private static readonly TimeSpan _waitBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The references the read function was called with, in call order.
    /// </summary>
    private readonly ConcurrentQueue<string> _references = new();

    /// <summary>
    /// Completed when the read function is called for the first reference.
    /// </summary>
    private readonly TaskCompletionSource _firstCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed to let the held read of the first reference end.
    /// </summary>
    private readonly TaskCompletionSource _releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IconReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public IconReaderTests(TestContext testContext)
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
    /// A second read started while the first is in flight does not reach the read function until the first completes, and
    /// both readers get the bytes of their reference afterwards.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IconReaderSecondReadWaitsForFirst()
    {
        // Arrange
        var reader = new IconReader(ReadIconAsync);
        var firstRead = reader.ReadAsync(FirstReference, TestContext.CancellationToken);

        await _firstCalled.Task.WaitAsync(_waitBound, TestContext.CancellationToken);

        // Act
        var secondRead = reader.ReadAsync(SecondReference, TestContext.CancellationToken);
        var callsWhileFirstInFlight = _references.Count;
        var secondCompletedWhileFirstInFlight = secondRead.IsCompleted;

        _releaseFirst.SetResult();

        var icons = await Task.WhenAll(firstRead, secondRead).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, callsWhileFirstInFlight, "The read function should be called once while the first read is in flight");
        Assert.IsFalse(secondCompletedWhileFirstInFlight, "The second read should wait while the first read is in flight");
        Assert.AreSequenceEqual(new[] { FirstReference, SecondReference }, _references.ToList(), "The read function should be called twice, the second read after the first");
        Assert.AreSequenceEqual(Encoding.UTF8.GetBytes(FirstReference), icons[0], "The first reader should get the bytes of its reference");
        Assert.AreSequenceEqual(Encoding.UTF8.GetBytes(SecondReference), icons[1], "The second reader should get the bytes of its reference");
    }

    /// <summary>
    /// A caller whose token is cancelled while it waits for the read in flight ends with the cancellation and causes no
    /// call of the read function, and a later read runs once the first ended.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IconReaderCancelledWaiterLeavesWithoutRead()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var reader = new IconReader(ReadIconAsync);
        var firstRead = reader.ReadAsync(FirstReference, TestContext.CancellationToken);

        await _firstCalled.Task.WaitAsync(_waitBound, TestContext.CancellationToken);

        var waitingRead = reader.ReadAsync(SecondReference, cancellation.Token);

        // Act
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => waitingRead.WaitAsync(_waitBound, TestContext.CancellationToken), "The cancelled caller should end with the cancellation");

        _releaseFirst.SetResult();
        await firstRead.WaitAsync(_waitBound, TestContext.CancellationToken);

        var laterIcon = await reader.ReadAsync(LaterReference, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        Assert.AreSequenceEqual(Encoding.UTF8.GetBytes(LaterReference), laterIcon, "A later read should run once the read in flight ended");
        Assert.AreSequenceEqual(new[] { FirstReference, LaterReference }, _references.ToList(), "The cancelled caller should cause no call of the read function");
    }

    /// <summary>
    /// The fake read function: records the reference, holds the read of <see cref="FirstReference" /> open until
    /// <see cref="_releaseFirst" /> completes, and returns the UTF-8 bytes of the reference.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The bytes of the reference</returns>
    private async Task<byte[]?> ReadIconAsync(string reference, CancellationToken cancellationToken)
    {
        _references.Enqueue(reference);

        if (reference == FirstReference)
        {
            _firstCalled.SetResult();

            await _releaseFirst.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Encoding.UTF8.GetBytes(reference);
    }

    #endregion // Methods
}