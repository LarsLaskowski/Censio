namespace Censio.Server;

/// <summary>
/// A server hosted in the Companion's process.
/// </summary>
public interface ILocalServer : IAsyncDisposable
{
    #region Properties

    /// <summary>
    /// Gets the port the server listens on.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Gets the ingest token of the server.
    /// </summary>
    string Token { get; }

    /// <summary>
    /// Gets a task that completes when the server's lifetime ends.
    /// </summary>
    Task Stopped { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Stops the server and releases what it holds.
    /// </summary>
    /// <param name="cancellationToken">The token that abandons the wait for the stop.</param>
    /// <returns>A task that completes when the server has stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken);

    #endregion // Methods
}