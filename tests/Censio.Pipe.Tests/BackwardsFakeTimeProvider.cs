using Microsoft.Extensions.Time.Testing;

namespace Censio.Pipe.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider" /> whose reading can be set back, as a system clock set back by the user: the
/// timers keep counting forward with <see cref="FakeTimeProvider.Advance" />, only the time they read moves. It
/// models the system clock set back; the timers measured: <c>Task.Delay</c> with this provider fires after the
/// advanced duration whatever the reading.
/// </summary>
public sealed class BackwardsFakeTimeProvider : FakeTimeProvider
{
    #region Fields

    /// <summary>
    /// The lock of the shift.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The amount the reading is shifted by.
    /// </summary>
    private TimeSpan _shift;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BackwardsFakeTimeProvider" /> class.
    /// </summary>
    /// <param name="start">The time the clock starts at</param>
    public BackwardsFakeTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Sets the reading of the clock back; the timers are not affected.
    /// </summary>
    /// <param name="span">The amount to set it back by</param>
    public void MoveBack(TimeSpan span)
    {
        lock (_lock)
        {
            _shift -= span;
        }
    }

    #endregion // Methods

    #region TimeProvider

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return base.GetUtcNow() + _shift;
        }
    }

    #endregion // TimeProvider
}