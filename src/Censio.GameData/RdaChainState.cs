namespace Censio.GameData;

/// <summary>
/// The state of a walk over the block chain.
/// </summary>
internal sealed class RdaChainState
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaChainState" /> class.
    /// </summary>
    /// <param name="length">The length of the archive in bytes</param>
    public RdaChainState(long length)
    {
        Length = length;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the length of the archive in bytes.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the entries listed so far.
    /// </summary>
    public List<RdaEntry> Entries { get; } = [];

    /// <summary>
    /// Gets the position of the directory of each block read so far; -1 where the directory was not read.
    /// </summary>
    public List<long> DirectoryOffsets { get; } = [];

    /// <summary>
    /// Gets or sets the number of encrypted blocks met.
    /// </summary>
    public int EncryptedBlocks { get; set; }

    /// <summary>
    /// Gets or sets the stored directory bytes accepted so far.
    /// </summary>
    public long StoredDirectoryBytes { get; set; }

    #endregion // Properties
}