namespace Censio.GameData;

/// <summary>
/// Names the reasons the icons could not be loaded.
/// </summary>
public enum GameIconErrorKind
{
    /// <summary>
    /// The base UI archive does not exist.
    /// </summary>
    ArchiveMissing,

    /// <summary>
    /// A UI archive could not be read.
    /// </summary>
    ArchiveUnreadable,

    /// <summary>
    /// The installation folder's archives could not be listed.
    /// </summary>
    ListingFailed,

    /// <summary>
    /// Two UI archives carry the same patch number.
    /// </summary>
    DuplicateArchiveNumber,

    /// <summary>
    /// The icons together exceed the size limit.
    /// </summary>
    IconSetTooLarge
}