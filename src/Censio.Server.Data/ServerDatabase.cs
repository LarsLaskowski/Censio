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
    internal ServerDatabase(string? databaseFile)
    {
        DatabaseFile = databaseFile;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the full path of the database file, or <see langword="null"/> for an in-memory database.
    /// </summary>
    public string? DatabaseFile { get; }

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
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static Task<ServerDatabaseOpenResult> OpenAsync(string? databaseFile, ILoggerFactory loggerFactory, Action<DbContextOptionsBuilder> configureContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003 // Skeleton: removed with the implementation

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        // Skeleton: nothing is opened yet, so nothing is released
    }

    #endregion // IDisposable
}