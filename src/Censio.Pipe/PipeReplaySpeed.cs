using System.Globalization;

namespace Censio.Pipe;

/// <summary>
/// The speed a pipe recording is replayed at.
/// </summary>
public readonly record struct PipeReplaySpeed
{
    #region Constants

    /// <summary>
    /// The slowest factor.
    /// </summary>
    private const double MinFactor = 0.1;

    /// <summary>
    /// The fastest factor.
    /// </summary>
    private const double MaxFactor = 1000;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The divisor a pause is scaled by, parsed from the same text as the factor, or <c>null</c> at maximum speed.
    /// </summary>
    private readonly decimal? _divisor;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplaySpeed"/> struct.
    /// </summary>
    /// <param name="factor">The speed factor, or <c>null</c> for maximum speed</param>
    /// <param name="divisor">The divisor a pause is scaled by, or <c>null</c> for maximum speed</param>
    private PipeReplaySpeed(double? factor, decimal? divisor)
    {
        Factor = factor;
        _divisor = divisor;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the replay at the recorded speed.
    /// </summary>
    public static PipeReplaySpeed RealTime => new(1d, 1m);

    /// <summary>
    /// Gets the replay without waiting.
    /// </summary>
    public static PipeReplaySpeed Maximum => new(null, null);

    /// <summary>
    /// Gets the speed factor, or <c>null</c> at maximum speed.
    /// </summary>
    public double? Factor { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Parses a speed from text.
    /// </summary>
    /// <param name="text">The text to parse</param>
    /// <param name="speed">The parsed speed</param>
    /// <returns><c>true</c> if the text is a valid speed</returns>
    public static bool TryParse(string? text, out PipeReplaySpeed speed)
    {
        speed = default;

        if (string.Equals(text, "max", StringComparison.OrdinalIgnoreCase))
        {
            speed = Maximum;

            return true;
        }

        if (double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var factor) == false
            || (factor >= MinFactor && factor <= MaxFactor) == false
            || decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var divisor) == false
            || divisor <= 0m)
        {
            return false;
        }

        speed = new PipeReplaySpeed(factor, divisor);

        return true;
    }

    /// <summary>
    /// Scales a pause of the recording to the time to wait at this speed.
    /// </summary>
    /// <param name="recordedPause">The recorded pause</param>
    /// <returns>The time to wait</returns>
    public TimeSpan Scale(TimeSpan recordedPause)
    {
        if (_divisor is not { } divisor || recordedPause <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var scaled = Math.Round(recordedPause.Ticks / divisor);

        return scaled >= long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)scaled);
    }

    #endregion // Methods
}