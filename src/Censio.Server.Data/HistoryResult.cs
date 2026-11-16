namespace Censio.Server.Data;

/// <summary>
/// The result of a history step.
/// </summary>
public sealed class HistoryResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryResult"/> class.
    /// </summary>
    /// <param name="outcome">The outcome</param>
    /// <param name="failure">The exception behind a failure, or <see langword="null"/></param>
    /// <param name="hour">The hour the step worked on, or <see langword="null"/></param>
    public HistoryResult(HistoryOutcome outcome, Exception? failure, DateTimeOffset? hour)
    {
        Outcome = outcome;
        Failure = failure;
        Hour = hour;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public HistoryOutcome Outcome { get; }

    /// <summary>
    /// Gets the exception behind a failure, or <see langword="null"/>.
    /// </summary>
    public Exception? Failure { get; }

    /// <summary>
    /// Gets the hour the step worked on, or <see langword="null"/>.
    /// </summary>
    public DateTimeOffset? Hour { get; }

    #endregion // Properties
}