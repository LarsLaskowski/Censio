namespace Censio.Server.Data;

/// <summary>
/// The result of a write to the server store.
/// </summary>
public sealed class ServerStoreResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStoreResult"/> class.
    /// </summary>
    /// <param name="outcome">The outcome</param>
    /// <param name="failure">The exception behind a failure, or <see langword="null"/></param>
    public ServerStoreResult(ServerStoreOutcome outcome, Exception? failure)
    {
        Outcome = outcome;
        Failure = failure;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public ServerStoreOutcome Outcome { get; }

    /// <summary>
    /// Gets the exception behind a failure, or <see langword="null"/>.
    /// </summary>
    public Exception? Failure { get; }

    #endregion // Properties
}