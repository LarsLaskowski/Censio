using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider" /> whose timers never run on their own: advancing the clock leaves them due, and a test
/// runs a timer's callback by hand, also after the timer was disposed. It models a timer callback the thread pool runs
/// late, or one already started when its timer is disposed.
/// </summary>
public sealed class HeldTimerTimeProvider : FakeTimeProvider
{
    #region Fields

    /// <summary>
    /// The lock of the callbacks.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The callbacks of the timers created so far, each with its state, in the order of their creation.
    /// </summary>
    private readonly List<(TimerCallback Callback, object? State)> _callbacks = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HeldTimerTimeProvider" /> class.
    /// </summary>
    /// <param name="start">The time the clock starts at</param>
    public HeldTimerTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of timers created so far.
    /// </summary>
    public int TimerCount
    {
        get
        {
            lock (_lock)
            {
                return _callbacks.Count;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Runs the callback of a timer, whether or not it is due or disposed.
    /// </summary>
    /// <param name="index">The index of the timer in the order of creation</param>
    public void RunTimer(int index)
    {
        (TimerCallback Callback, object? State) timer;

        lock (_lock)
        {
            timer = _callbacks[index];
        }

        timer.Callback(timer.State);
    }

    #endregion // Methods

    #region TimeProvider

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_lock)
        {
            _callbacks.Add((callback, state));
        }

        return base.CreateTimer(static _ =>
                                       {
                                       },
                                null,
                                Timeout.InfiniteTimeSpan,
                                Timeout.InfiniteTimeSpan);
    }

    #endregion // TimeProvider
}