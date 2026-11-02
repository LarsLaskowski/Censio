using Microsoft.Extensions.Time.Testing;

namespace Censio.Pipe.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider" /> that records the due time of every timer created on it, lets a test wait until a
/// number of timers or a timer of a given due time exists and advances through the waits a pending task starts. It
/// models no real component.
/// </summary>
public sealed class RecordingTimeProvider : FakeTimeProvider
{
    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The due times of the timers created so far.
    /// </summary>
    private readonly List<TimeSpan> _delays = [];

    /// <summary>
    /// The tests waiting for the timers, each with the condition on the due times that ends its wait.
    /// </summary>
    private readonly List<(Func<List<TimeSpan>, bool> IsReached, TaskCompletionSource Source)> _waiters = [];

    /// <summary>
    /// The lock of the delays and the waiters.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The number of timers <see cref="RunWaitsAsync" /> has advanced through.
    /// </summary>
    private int _handled;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingTimeProvider" /> class.
    /// </summary>
    /// <param name="start">The time the clock starts at</param>
    public RecordingTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the due times of the timers created so far, in the order of their creation.
    /// </summary>
    public IReadOnlyList<TimeSpan> Delays
    {
        get
        {
            lock (_lock)
            {
                return [.. _delays];
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Waits until the given number of timers were created.
    /// </summary>
    /// <param name="count">The number of timers</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the timers exist</returns>
    public Task WaitForTimersAsync(int count, CancellationToken cancellationToken)
    {
        return WaitAsync(delays => delays.Count >= count, cancellationToken);
    }

    /// <summary>
    /// Waits until a timer with the given due time was created.
    /// </summary>
    /// <param name="dueTime">The due time of the timer</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the timer exists</returns>
    public Task WaitForDelayAsync(TimeSpan dueTime, CancellationToken cancellationToken)
    {
        return WaitAsync(delays => delays.Contains(dueTime), cancellationToken);
    }

    /// <summary>
    /// Advances the clock by the due time of each timer the task creates, until the task is completed.
    /// </summary>
    /// <param name="task">The task that waits on this provider</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the given task is completed</returns>
    public async Task RunWaitsAsync(Task task, CancellationToken cancellationToken)
    {
        while (task.IsCompleted == false)
        {
            var next = WaitForTimersAsync(_handled + 1, cancellationToken);

            await Task.WhenAny(task, next).WaitAsync(_guard, cancellationToken).ConfigureAwait(false);

            if (task.IsCompleted)
            {
                break;
            }

            TimeSpan due;

            lock (_lock)
            {
                due = _delays[_handled];
            }

            _handled++;

            Advance(due);
        }
    }

    /// <summary>
    /// Waits until the due times of the created timers meet a condition.
    /// </summary>
    /// <param name="isReached">The condition on the due times</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the condition is met</returns>
    private Task WaitAsync(Func<List<TimeSpan>, bool> isReached, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            if (isReached(_delays))
            {
                source.SetResult();
            }
            else
            {
                _waiters.Add((isReached, source));
            }
        }

        return source.Task.WaitAsync(cancellationToken);
    }

    #endregion // Methods

    #region TimeProvider

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);

        lock (_lock)
        {
            _delays.Add(dueTime);

            foreach (var waiter in _waiters.Where(waiter => waiter.IsReached(_delays)).ToList())
            {
                waiter.Source.TrySetResult();
                _waiters.Remove(waiter);
            }
        }

        return timer;
    }

    #endregion // TimeProvider
}