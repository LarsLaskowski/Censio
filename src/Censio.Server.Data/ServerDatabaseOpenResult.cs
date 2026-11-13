namespace Censio.Server.Data;

/// <summary>
/// The result of opening the server database.
/// </summary>
public sealed class ServerDatabaseOpenResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDatabaseOpenResult"/> class.
    /// </summary>
    /// <param name="kind">The result kind</param>
    /// <param name="databaseFile">The full path of the database file, or <see langword="null"/> for none</param>
    /// <param name="database">The opened database, or <see langword="null"/> after a failure</param>
    /// <param name="failure">The exception behind a failure, or <see langword="null"/></param>
    public ServerDatabaseOpenResult(ServerDatabaseOpenKind kind, string? databaseFile, ServerDatabase? database, Exception? failure)
    {
        Kind = kind;
        DatabaseFile = databaseFile;
        Database = database;
        Failure = failure;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the result kind.
    /// </summary>
    public ServerDatabaseOpenKind Kind { get; }

    /// <summary>
    /// Gets the full path of the database file, or <see langword="null"/> for none.
    /// </summary>
    public string? DatabaseFile { get; }

    /// <summary>
    /// Gets the opened database, or <see langword="null"/> after a failure.
    /// </summary>
    public ServerDatabase? Database { get; }

    /// <summary>
    /// Gets the exception behind a failure, or <see langword="null"/>.
    /// </summary>
    public Exception? Failure { get; }

    #endregion // Properties
}