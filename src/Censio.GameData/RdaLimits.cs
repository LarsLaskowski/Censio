namespace Censio.GameData;

/// <summary>
/// Holds the bounds within which an RDA archive is read.
/// </summary>
public sealed class RdaLimits
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaLimits" /> class.
    /// </summary>
    /// <param name="maxBlocks">The most blocks an archive may chain</param>
    /// <param name="maxEntries">The most entries an archive may list</param>
    /// <param name="maxDirectoryBytes">The most bytes a block's directory may take, stored or decompressed</param>
    /// <param name="maxEntryBytes">The most bytes an entry's content may take, stored or decompressed</param>
    public RdaLimits(int maxBlocks, int maxEntries, int maxDirectoryBytes, int maxEntryBytes)
    {
        MaxBlocks = maxBlocks;
        MaxEntries = maxEntries;
        MaxDirectoryBytes = maxDirectoryBytes;
        MaxEntryBytes = maxEntryBytes;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the limits used when the caller names none.
    /// </summary>
    public static RdaLimits Default { get; } = new(1_048_576, 1_048_576, 64 * 1024 * 1024, 512 * 1024 * 1024);

    /// <summary>
    /// Gets the most blocks an archive may chain.
    /// </summary>
    public int MaxBlocks { get; }

    /// <summary>
    /// Gets the most entries an archive may list.
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    /// Gets the most bytes a block's directory may take, stored or decompressed.
    /// </summary>
    public int MaxDirectoryBytes { get; }

    /// <summary>
    /// Gets the most bytes an entry's content may take, stored or decompressed.
    /// </summary>
    public int MaxEntryBytes { get; }

    #endregion // Properties
}