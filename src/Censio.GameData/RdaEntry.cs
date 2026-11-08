namespace Censio.GameData;

/// <summary>
/// Describes one file listed in the directory of an RDA archive.
/// </summary>
public sealed class RdaEntry
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaEntry" /> class.
    /// </summary>
    /// <param name="path">The path inside the archive</param>
    /// <param name="size">The decompressed size in bytes</param>
    /// <param name="flags">The flags of the entry's block</param>
    /// <param name="offset">The offset of the entry's data in the archive</param>
    /// <param name="storedSize">The stored size in bytes</param>
    /// <param name="blockIndex">The index of the entry's block in the chain</param>
    internal RdaEntry(string path, long size, RdaBlockFlags flags, ulong offset, ulong storedSize, int blockIndex)
    {
        Path = path;
        Size = size;
        Flags = flags;
        Offset = offset;
        StoredSize = storedSize;
        BlockIndex = blockIndex;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the path inside the archive; an untrusted string.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the decompressed size in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets the flags of the entry's block.
    /// </summary>
    public RdaBlockFlags Flags { get; }

    /// <summary>
    /// Gets the offset of the entry's data in the archive.
    /// </summary>
    internal ulong Offset { get; }

    /// <summary>
    /// Gets the stored size in bytes.
    /// </summary>
    internal ulong StoredSize { get; }

    /// <summary>
    /// Gets the index of the entry's block in the chain.
    /// </summary>
    internal int BlockIndex { get; }

    #endregion // Properties
}