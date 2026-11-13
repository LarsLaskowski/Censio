using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Opens the server database from the configuration and reports the result in the log.
/// </summary>
public static partial class DatabaseSetup
{
    #region Constants

    /// <summary>
    /// The configuration key that names the database file.
    /// </summary>
    public const string DatabaseFileKey = "Censio:Database:File";

    /// <summary>
    /// The name of the database file in the data directory.
    /// </summary>
    public const string DatabaseFileName = "censio.db";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Opens the database named by the configuration and reports the result.
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <param name="loggerFactory">The logger factory of the database</param>
    /// <param name="logger">The logger</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The opened database, or <see langword="null"/> when the server cannot start</returns>
    public static async Task<ServerDatabase?> TryOpenAsync(IConfiguration configuration, ILoggerFactory loggerFactory, ILogger logger, CancellationToken cancellationToken)
    {
        var configuredFile = configuration[DatabaseFileKey];
        string? databaseFile;

        if (configuredFile is not null)
        {
            if (Path.IsPathFullyQualified(configuredFile) == false)
            {
                LogRelativeDatabaseFile(logger, configuredFile);

                return null;
            }

            databaseFile = Path.GetFullPath(configuredFile);
        }
        else
        {
            var dataDirectory = configuration[DataProtectionSetup.DataDirectoryKey];

            databaseFile = dataDirectory is null
                               ? null
                               : Path.Combine(Path.GetFullPath(dataDirectory), DatabaseFileName);
        }

        var result = await ServerDatabase.OpenAsync(databaseFile, loggerFactory, cancellationToken).ConfigureAwait(false);

        return ReportOpenResult(result, logger);
    }

    /// <summary>
    /// Writes the log line of an open result.
    /// </summary>
    /// <param name="result">The open result</param>
    /// <param name="logger">The logger</param>
    /// <returns>The opened database, or <see langword="null"/> when the server cannot start</returns>
    public static ServerDatabase? ReportOpenResult(ServerDatabaseOpenResult result, ILogger logger)
    {
        var file = result.DatabaseFile;

        switch (result.Kind)
        {
            case ServerDatabaseOpenKind.Created:
                LogCreated(logger, file);
                break;

            case ServerDatabaseOpenKind.Opened:
                LogOpened(logger, file);
                break;

            case ServerDatabaseOpenKind.Updated:
                LogUpdated(logger, file);
                break;

            case ServerDatabaseOpenKind.InMemory:
                LogInMemory(logger);
                break;

            case ServerDatabaseOpenKind.NotWritable:
                LogNotWritable(logger, result.Failure, file);
                break;

            case ServerDatabaseOpenKind.NotADatabase:
                LogNotADatabase(logger, result.Failure, file);
                break;

            case ServerDatabaseOpenKind.NewerSchema:
                LogNewerSchema(logger, file);
                break;

            default:
                LogUpdateFailed(logger, result.Failure, file);
                break;
        }

        return result.Database;
    }

    /// <summary>
    /// Logs a database file setting that is not an absolute path.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="value">The configured value</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The database file (Censio:Database:File) must be an absolute path, not '{Value}'; the server stops.")]
    private static partial void LogRelativeDatabaseFile(ILogger logger, string value);

    /// <summary>
    /// Logs a created database.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The database '{DatabaseFile}' was created.")]
    private static partial void LogCreated(ILogger logger, string? databaseFile);

    /// <summary>
    /// Logs an opened database.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The database '{DatabaseFile}' was opened.")]
    private static partial void LogOpened(ILogger logger, string? databaseFile);

    /// <summary>
    /// Logs an updated database.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The database '{DatabaseFile}' was updated to the current schema.")]
    private static partial void LogUpdated(ILogger logger, string? databaseFile);

    /// <summary>
    /// Logs a database kept in memory.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "No database file without a data directory (Censio:DataDirectory) or Censio:Database:File; data is kept in memory and lost when the server stops.")]
    private static partial void LogInMemory(ILogger logger);

    /// <summary>
    /// Logs a database that cannot be opened or written.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The database '{DatabaseFile}' cannot be opened or written; the server stops. Make its folder writable for the user the server runs as.")]
    private static partial void LogNotWritable(ILogger logger, Exception? exception, string? databaseFile);

    /// <summary>
    /// Logs a file that is not a Censio database.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The file '{DatabaseFile}' is not a readable Censio database; the server stops.")]
    private static partial void LogNotADatabase(ILogger logger, Exception? exception, string? databaseFile);

    /// <summary>
    /// Logs a database written by a newer version.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The database '{DatabaseFile}' was written by a newer Censio version; the server stops. Start that version or a newer one.")]
    private static partial void LogNewerSchema(ILogger logger, string? databaseFile);

    /// <summary>
    /// Logs a failed update of the schema.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    /// <param name="databaseFile">The database file</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The database '{DatabaseFile}' cannot be updated to the current schema; the server stops.")]
    private static partial void LogUpdateFailed(ILogger logger, Exception? exception, string? databaseFile);

    #endregion // Methods
}