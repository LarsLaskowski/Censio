namespace Censio.Server.Data;

/// <summary>
/// The result kind of opening the server database.
/// </summary>
public enum ServerDatabaseOpenKind
{
    /// <summary>
    /// A new database file was created with the current schema.
    /// </summary>
    Created,

    /// <summary>
    /// An existing database file with the current schema was opened.
    /// </summary>
    Opened,

    /// <summary>
    /// An existing database file was updated to the current schema.
    /// </summary>
    Updated,

    /// <summary>
    /// No file was given; an in-memory database was opened.
    /// </summary>
    InMemory,

    /// <summary>
    /// The file or its folder cannot be written.
    /// </summary>
    NotWritable,

    /// <summary>
    /// The file is not a database of this application.
    /// </summary>
    NotADatabase,

    /// <summary>
    /// The file was written by a newer version.
    /// </summary>
    NewerSchema,

    /// <summary>
    /// Updating the schema failed.
    /// </summary>
    UpdateFailed
}