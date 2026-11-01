namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="TrayStatusUpdater" />.
/// </summary>
[TestClass]
public sealed class TrayStatusUpdaterTests
{
    #region Fields

    /// <summary>
    /// The callbacks posted to the tray and not yet run.
    /// </summary>
    private readonly Queue<Action> _posted = [];

    /// <summary>
    /// The statuses the tray showed, in order.
    /// </summary>
    private readonly List<PipeConnectionStatus> _shown = [];

    /// <summary>
    /// The status the connection reports.
    /// </summary>
    private PipeConnectionStatus _current = Mismatch(0);

    /// <summary>
    /// The updater a show callback announces a change to.
    /// </summary>
    private TrayStatusUpdater? _updater;

    /// <summary>
    /// The number of posts made.
    /// </summary>
    private int _postCount;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Many notifications before the post runs give one post, and it shows the latest status.
    /// </summary>
    [TestMethod]
    public void TrayStatusUpdaterManyChangesWhilePendingPostOnce()
    {
        // Arrange
        var updater = CreateUpdater(_shown.Add);

        // Act
        NotifyChanges(updater, 1000);
        RunPosted();

        // Assert
        Assert.AreEqual(1, _postCount, "1,000 notifications before the post runs should give one post");
        Assert.AreSequenceEqual(new[] { Mismatch(1000) }, _shown, "The one post should show the latest status");
    }

    /// <summary>
    /// A notification after the post has run posts again.
    /// </summary>
    [TestMethod]
    public void TrayStatusUpdaterNotifyAfterPostRanPostsAgain()
    {
        // Arrange
        var updater = CreateUpdater(_shown.Add);

        updater.Notify();
        RunPosted();

        _current = Mismatch(1);

        // Act
        updater.Notify();

        // Assert
        Assert.AreEqual(2, _postCount, "A notification after the post ran should post again");
        Assert.HasCount(1, _posted, "The second post should be waiting");
    }

    /// <summary>
    /// A change announced while the status is shown posts again and the second post shows the new status.
    /// </summary>
    [TestMethod]
    public void TrayStatusUpdaterChangeDuringShowPostsAgain()
    {
        // Arrange
        _updater = CreateUpdater(ShowAndChangeOnFirstShow);

        // Act
        _updater.Notify();
        RunPosted();

        // Assert
        Assert.AreEqual(2, _postCount, "A change during the show should post again");
        Assert.AreSequenceEqual(new[] { Mismatch(0), Mismatch(1) }, _shown, "The second post should show the new status");
    }

    /// <summary>
    /// Creates a status that differs by its received version.
    /// </summary>
    /// <param name="version">The received version</param>
    /// <returns>The status</returns>
    private static PipeConnectionStatus Mismatch(int version)
    {
        return new PipeConnectionStatus
               {
                   State = PipeConnectionState.ProtocolMismatch,
                   ReceivedVersion = version
               };
    }

    /// <summary>
    /// Creates the updater on the fields of the test.
    /// </summary>
    /// <param name="show">Shows a status</param>
    /// <returns>The updater</returns>
    private TrayStatusUpdater CreateUpdater(Action<PipeConnectionStatus> show)
    {
        return new TrayStatusUpdater(() => _current,
                                     action =>
                                     {
                                         _postCount++;
                                         _posted.Enqueue(action);
                                     },
                                     show);
    }

    /// <summary>
    /// Changes the status and notifies, the given number of times, without running a post.
    /// </summary>
    /// <param name="updater">The updater</param>
    /// <param name="count">The number of changes</param>
    private void NotifyChanges(TrayStatusUpdater updater, int count)
    {
        for (var version = 1; version <= count; version++)
        {
            _current = Mismatch(version);
            updater.Notify();
        }
    }

    /// <summary>
    /// Shows a status; on the first show the status changes and the change is announced, as a connection on another
    /// thread would do while the tray shows the status.
    /// </summary>
    /// <param name="status">The status to show</param>
    private void ShowAndChangeOnFirstShow(PipeConnectionStatus status)
    {
        _shown.Add(status);

        if (_shown.Count == 1)
        {
            _current = Mismatch(1);
            _updater?.Notify();
        }
    }

    /// <summary>
    /// Runs the posted callbacks in order, including those posted while they run.
    /// </summary>
    private void RunPosted()
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }

    #endregion // Methods
}