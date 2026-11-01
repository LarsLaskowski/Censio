namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeReplayClock" />.
/// </summary>
[TestClass]
public sealed class PipeReplayClockTests
{
    #region Fields

    /// <summary>
    /// The start of the clock of most tests.
    /// </summary>
    private static readonly DateTimeOffset _start = new DateTimeOffset(2026, 11, 1, 17, 9, 0, TimeSpan.Zero).AddTicks(1234567);

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the test context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The clock reads its start, then the last time set, also when that time is earlier than the one before.
    /// </summary>
    [TestMethod]
    public void PipeReplayClockGetUtcNowReturnsLastSetTimeAlsoBackwards()
    {
        // Arrange
        var clock = new PipeReplayClock(_start);
        var atStart = clock.GetUtcNow();

        clock.SetUtcNow(_start.AddDays(3));

        var afterForward = clock.GetUtcNow();

        clock.SetUtcNow(_start.AddDays(-60));

        var afterBackward = clock.GetUtcNow();

        // Act
        clock.SetUtcNow(_start.AddDays(-60));

        var afterRepeat = clock.GetUtcNow();

        // Assert
        Assert.AreEqual(_start, atStart, "The clock should read its start before a time is set");
        Assert.AreEqual(_start.AddDays(3), afterForward, "The clock should read the time set");
        Assert.AreEqual(_start.AddDays(-60), afterBackward, "The clock should follow a time set earlier than the one before");
        Assert.AreEqual(_start.AddDays(-60), afterRepeat, "The clock should keep a time set again");
    }

    /// <summary>
    /// A time set with an offset reads as the same instant.
    /// </summary>
    [TestMethod]
    public void PipeReplayClockGetUtcNowReadsInstantOfTimeWithOffset()
    {
        // Arrange
        var clock = new PipeReplayClock(_start);
        var time = new DateTimeOffset(2026, 11, 1, 19, 0, 0, TimeSpan.FromHours(2));

        // Act
        clock.SetUtcNow(time);

        // Assert
        Assert.AreEqual(time.UtcDateTime, clock.GetUtcNow().UtcDateTime, "The clock should read the instant it was set to");
    }

    /// <summary>
    /// A timer of the clock never fires, also when the clock moves past its due time, and it accepts a change and a
    /// disposal.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayClockTimerNeverFiresAndAcceptsChangeAndDispose()
    {
        // Arrange
        var clock = new PipeReplayClock(_start);
        var fired = 0;
        var timer = clock.CreateTimer(_ => Interlocked.Increment(ref fired), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        var delay = Task.Delay(TimeSpan.FromHours(1), clock, TestContext.CancellationToken);

        // Act
        clock.SetUtcNow(_start.AddHours(2));
        timer.Change(TimeSpan.Zero, TimeSpan.Zero);
        clock.SetUtcNow(_start.AddHours(3));
        await timer.DisposeAsync();

        // Assert
        Assert.AreEqual(0, Volatile.Read(ref fired), "The callback of the timer should never run");
        Assert.IsFalse(delay.IsCompleted, "A delay on the clock should not end when the clock moves past it");
    }

    #endregion // Methods
}