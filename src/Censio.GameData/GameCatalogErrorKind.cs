namespace Censio.GameData;

/// <summary>
/// Names the reasons the catalog could not be built.
/// </summary>
public enum GameCatalogErrorKind
{
    /// <summary>
    /// The configuration archive could not be opened.
    /// </summary>
    ArchiveUnreadable,

    /// <summary>
    /// A required file is missing from the archive.
    /// </summary>
    FileMissing,

    /// <summary>
    /// A required file could not be read.
    /// </summary>
    FileUnreadable,

    /// <summary>
    /// A required file does not have the expected structure.
    /// </summary>
    MalformedFile
}