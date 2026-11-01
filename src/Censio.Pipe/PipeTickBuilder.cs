namespace Censio.Pipe;

/// <summary>
/// Groups read results into ticks and session events; pure, without a clock or I/O.
/// </summary>
public sealed class PipeTickBuilder
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeTickBuilder" /> class.
    /// </summary>
    public PipeTickBuilder()
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the time at which the open tick closes as quiet, or <see langword="null" /> without an open tick.
    /// </summary>
    public DateTimeOffset? QuietDeadline => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds a read result and returns the items it produces, in order.
    /// </summary>
    /// <param name="result">The read result</param>
    /// <param name="receiveTime">The time the result was received</param>
    /// <returns>The produced items</returns>
    public IReadOnlyList<PipeTickResult> Add(PipeReadResult result, DateTimeOffset receiveTime)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Closes the open tick when the quiet period has passed.
    /// </summary>
    /// <param name="now">The current time</param>
    /// <returns>The closed tick, or <see langword="null" /> when nothing closed</returns>
    public PipeTickResult? CloseIfQuiet(DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}