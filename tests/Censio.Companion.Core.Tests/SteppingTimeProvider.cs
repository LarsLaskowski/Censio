using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// A fake time provider that moves forward by a fixed step at every request for a timestamp, so that the duration between two
/// requests is the step.
/// </summary>
internal sealed class SteppingTimeProvider : FakeTimeProvider
{
    #region Fields

    /// <summary>
    /// The step.
    /// </summary>
    private readonly TimeSpan _step;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SteppingTimeProvider" /> class.
    /// </summary>
    /// <param name="step">The step</param>
    public SteppingTimeProvider(TimeSpan step)
        : base(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))
    {
        _step = step;
    }

    #endregion // Constructors

    #region TimeProvider

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        Advance(_step);

        return base.GetTimestamp();
    }

    #endregion // TimeProvider
}