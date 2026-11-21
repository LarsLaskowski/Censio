namespace Censio.Server;

/// <summary>
/// The result of starting a local server.
/// </summary>
public sealed class LocalServerStart
{
    #region Properties

    /// <summary>
    /// Gets the outcome of the start.
    /// </summary>
    public required LocalServerStartKind Kind { get; init; }

    /// <summary>
    /// Gets the running server, or <c>null</c> when the start did not succeed.
    /// </summary>
    public ILocalServer? Server { get; init; }

    /// <summary>
    /// Gets the exception that failed the start, or <c>null</c> when there is none.
    /// </summary>
    public Exception? Exception { get; init; }

    #endregion // Properties
}