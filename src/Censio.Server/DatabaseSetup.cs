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

#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Opens the database named by the configuration and reports the result.
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <param name="loggerFactory">The logger factory of the database</param>
    /// <param name="logger">The logger</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The opened database, or <see langword="null"/> when the server cannot start</returns>
    public static Task<ServerDatabase?> TryOpenAsync(IConfiguration configuration, ILoggerFactory loggerFactory, ILogger logger, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes the log line of an open result.
    /// </summary>
    /// <param name="result">The open result</param>
    /// <param name="logger">The logger</param>
    /// <returns>The opened database, or <see langword="null"/> when the server cannot start</returns>
    public static ServerDatabase? ReportOpenResult(ServerDatabaseOpenResult result, ILogger logger)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}