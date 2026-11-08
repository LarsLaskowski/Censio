namespace Censio.GameData;

/// <summary>
/// Names the flags of a block of an RDA archive.
/// </summary>
[Flags]
public enum RdaBlockFlags
{
    /// <summary>
    /// The block has no flag.
    /// </summary>
    None = 0,

    /// <summary>
    /// The block's data is compressed with zlib.
    /// </summary>
    Compressed = 1,

    /// <summary>
    /// The block's data is encrypted.
    /// </summary>
    Encrypted = 2,

    /// <summary>
    /// The block's data is memory-resident: stored and decompressed as one unit.
    /// </summary>
    MemoryResident = 4,

    /// <summary>
    /// The block is deleted.
    /// </summary>
    Deleted = 8
}