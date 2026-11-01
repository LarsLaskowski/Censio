namespace Censio.Pipe;

/// <summary>
/// The speed a pipe recording is replayed at.
/// </summary>
public readonly record struct PipeReplaySpeed
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplaySpeed"/> struct.
    /// </summary>
    /// <param name="factor">The speed factor, or <c>null</c> for maximum speed</param>
    private PipeReplaySpeed(double? factor)
    {
        Factor = factor;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the replay at the recorded speed.
    /// </summary>
    public static PipeReplaySpeed RealTime => throw new NotImplementedException();

    /// <summary>
    /// Gets the replay without waiting.
    /// </summary>
    public static PipeReplaySpeed Maximum => throw new NotImplementedException();

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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Scales a pause of the recording to the time to wait at this speed.
    /// </summary>
    /// <param name="recordedPause">The recorded pause</param>
    /// <returns>The time to wait</returns>
    public TimeSpan Scale(TimeSpan recordedPause)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}