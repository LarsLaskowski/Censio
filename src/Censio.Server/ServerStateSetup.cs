namespace Censio.Server;

/// <summary>
/// Restores the server state at startup and reports the result in the log.
/// </summary>
public static partial class ServerStateSetup
{
    #region Methods

#pragma warning disable RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Restores the state from the database and reports the result.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="databaseFile">The database file, or <see langword="null"/> for an in-memory database</param>
    /// <param name="logger">The logger</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns><see langword="true"/> when the state is restored, <see langword="false"/> when the server cannot start</returns>
    public static Task<bool> TryRestoreAsync(ServerState state, string? databaseFile, ILogger logger, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Methods
}