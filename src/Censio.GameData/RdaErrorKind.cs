namespace Censio.GameData;

/// <summary>
/// Names the reasons an RDA archive or one of its entries could not be read.
/// </summary>
public enum RdaErrorKind
{
    /// <summary>
    /// The archive is not of the supported format version.
    /// </summary>
    UnsupportedArchive,

    /// <summary>
    /// The archive's structure contradicts itself or exceeds a limit.
    /// </summary>
    CorruptArchive,

    /// <summary>
    /// The entry's location, sizes or data contradict themselves or exceed a limit.
    /// </summary>
    CorruptEntry,

    /// <summary>
    /// The entry's block kind cannot be read.
    /// </summary>
    UnsupportedEntry,

    /// <summary>
    /// The archive holds no entry of that path.
    /// </summary>
    NotFound,

    /// <summary>
    /// The archive holds several entries of that path.
    /// </summary>
    DuplicateEntry,

    /// <summary>
    /// The file or stream could not be read.
    /// </summary>
    Unreadable
}