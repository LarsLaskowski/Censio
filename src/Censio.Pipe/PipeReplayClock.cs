namespace Censio.Pipe;

/// <summary>
/// A time provider whose time is set by the replay from the recorded receive times.
/// </summary>
public sealed class PipeReplayClock : TimeProvider
{
    #region Fields

    /// <summary>
    /// The current time of the clock.
    /// </summary>
    private DateTimeOffset _now;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayClock"/> class.
    /// </summary>
    /// <param name="start">The time the clock starts at</param>
    public PipeReplayClock(DateTimeOffset start)
    {
        _now = start;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Sets the current time of the clock.
    /// </summary>
    /// <param name="now">The new time</param>
    public void SetUtcNow(DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods

    #region TimeProvider

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        throw new NotImplementedException();
    }

    #endregion // TimeProvider
}