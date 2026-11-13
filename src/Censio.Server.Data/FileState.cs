namespace Censio.Server.Data;

/// <summary>
/// What an existing database file turned out to be.
/// </summary>
internal enum FileState
{
    /// <summary>
    /// Empty, or holding only EF Core's bookkeeping without an applied migration.
    /// </summary>
    Fresh,

    /// <summary>
    /// A database of this application.
    /// </summary>
    Existing,

    /// <summary>
    /// Not a database of this application.
    /// </summary>
    Foreign,

    /// <summary>
    /// Written by a newer version.
    /// </summary>
    Newer
}