namespace Censio.GameData.Tests;

/// <summary>
/// The positions of the parts of a built block.
/// </summary>
public sealed class RdaBuiltBlock
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaBuiltBlock" /> class.
    /// </summary>
    /// <param name="headerOffset">The position of the block header</param>
    /// <param name="directoryOffset">The position of the directory</param>
    /// <param name="dataOffsets">The absolute position of each entry's data</param>
    public RdaBuiltBlock(long headerOffset, long directoryOffset, IReadOnlyList<long> dataOffsets)
    {
        HeaderOffset = headerOffset;
        DirectoryOffset = directoryOffset;
        DataOffsets = dataOffsets;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the position of the block header.
    /// </summary>
    public long HeaderOffset { get; }

    /// <summary>
    /// Gets the position of the directory; the two data sizes of a memory-resident block lie right before it.
    /// </summary>
    public long DirectoryOffset { get; }

    /// <summary>
    /// Gets the absolute position of each entry's stored data; 0 for a deleted block.
    /// </summary>
    public IReadOnlyList<long> DataOffsets { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the position of a directory entry.
    /// </summary>
    /// <param name="index">The index of the entry in the block</param>
    /// <returns>The position</returns>
    public long EntryPosition(int index)
    {
        return DirectoryOffset + (index * (long)RdaArchiveBuilder.EntrySize);
    }

    #endregion // Methods
}