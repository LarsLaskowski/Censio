using Microsoft.Data.Sqlite;

namespace Censio.Server;

/// <summary>
/// Restores the server state at startup and reports the result in the log.
/// </summary>
public static partial class ServerStateSetup
{
    #region Methods

    /// <summary>
    /// Restores the state from the database and reports the result.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="databaseFile">The database file, or <see langword="null"/> for an in-memory database</param>
    /// <param name="logger">The logger</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns><see langword="true"/> when the state is restored, <see langword="false"/> when the server cannot start</returns>
    public static async Task<bool> TryRestoreAsync(ServerState state, string? databaseFile, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var count = await state.RestoreAsync(cancellationToken).ConfigureAwait(false);

            LogRestored(logger, count);

            return true;
        }
        catch (SqliteException exception)
        {
            LogRestoreFailed(logger, exception, databaseFile);

            return false;
        }
    }

    /// <summary>
    /// Logs the restored profiles.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="count">The number of profiles</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Profiles restored from the database: {Count}.")]
    private static partial void LogRestored(ILogger logger, int count);

    /// <summary>
    /// Logs a state that cannot be read.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The current state cannot be read from the database '{DatabaseFile}'; the server stops.")]
    private static partial void LogRestoreFailed(ILogger logger, Exception exception, string? databaseFile);

    #endregion // Methods
}