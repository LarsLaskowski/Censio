using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Censio.Server.Data;

/// <summary>
/// The opened server database.
/// </summary>
public sealed class ServerDatabase : IDisposable
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDatabase"/> class.
    /// </summary>
    /// <param name="databaseFile">The full path of the database file, or <see langword="null"/> for an in-memory database</param>
    /// <param name="keeper">The connection kept open for the lifetime of the database</param>
    internal ServerDatabase(string? databaseFile, SqliteConnection keeper)
    {
        DatabaseFile = databaseFile;
        Connection = keeper;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the full path of the database file, or <see langword="null"/> for an in-memory database.
    /// </summary>
    public string? DatabaseFile { get; }

    /// <summary>
    /// Gets the connection kept open for the lifetime of the database, the one every write runs on.
    /// </summary>
    internal SqliteConnection Connection { get; }

    /// <summary>
    /// Gets the gate that serializes the writes, and in memory the reads.
    /// </summary>
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the database, creating or updating it as needed.
    /// </summary>
    /// <param name="databaseFile">The full path of the database file, or <see langword="null"/> for an in-memory database</param>
    /// <param name="loggerFactory">The logger factory of the context</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of opening</returns>
    public static Task<ServerDatabaseOpenResult> OpenAsync(string? databaseFile, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        return OpenAsync(databaseFile,
                         loggerFactory,
                         static _ =>
                                {
                                },
                         cancellationToken);
    }

    /// <summary>
    /// Opens the database, creating or updating it as needed.
    /// </summary>
    /// <param name="databaseFile">The full path of the database file, or <see langword="null"/> for an in-memory database</param>
    /// <param name="loggerFactory">The logger factory of the context</param>
    /// <param name="configureContext">Further configuration of the context options</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of opening</returns>
    public static Task<ServerDatabaseOpenResult> OpenAsync(string? databaseFile, ILoggerFactory loggerFactory, Action<DbContextOptionsBuilder> configureContext, CancellationToken cancellationToken)
    {
        return ServerDatabaseOpener.OpenAsync(databaseFile, loggerFactory, configureContext, cancellationToken);
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        ServerDatabaseOpener.Release(Connection);
        Gate.Dispose();
    }

    #endregion // IDisposable
}