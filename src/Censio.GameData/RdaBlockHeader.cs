namespace Censio.GameData;

/// <summary>
/// The values of one block header that the checks and the reading of its directory need.
/// </summary>
internal sealed class RdaBlockHeader
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaBlockHeader" /> class.
    /// </summary>
    /// <param name="index">The index of the block</param>
    /// <param name="headerOffset">The offset of the block header</param>
    /// <param name="flags">The flags of the block</param>
    /// <param name="count">The file count of the block</param>
    /// <param name="stored">The stored size of the directory</param>
    /// <param name="declared">The declared decompressed size of the directory</param>
    public RdaBlockHeader(int index, long headerOffset, uint flags, uint count, long stored, long declared)
    {
        Index = index;
        HeaderOffset = headerOffset;
        Flags = flags;
        Count = count;
        Stored = stored;
        Declared = declared;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the index of the block.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the offset of the block header.
    /// </summary>
    public long HeaderOffset { get; }

    /// <summary>
    /// Gets the flags of the block.
    /// </summary>
    public uint Flags { get; }

    /// <summary>
    /// Gets the file count of the block.
    /// </summary>
    public uint Count { get; }

    /// <summary>
    /// Gets the stored size of the directory.
    /// </summary>
    public long Stored { get; }

    /// <summary>
    /// Gets the declared decompressed size of the directory.
    /// </summary>
    public long Declared { get; }

    /// <summary>
    /// Gets the position of the directory.
    /// </summary>
    public long DirectoryOffset => HeaderOffset - Stored;

    #endregion // Properties
}