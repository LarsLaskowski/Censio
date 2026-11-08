namespace Censio.GameData.Tests;

/// <summary>
/// A built archive with the positions of its parts.
/// </summary>
public sealed class RdaBuiltArchive
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaBuiltArchive" /> class.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <param name="blocks">The blocks</param>
    public RdaBuiltArchive(byte[] bytes, IReadOnlyList<RdaBuiltBlock> blocks)
    {
        Bytes = bytes;
        Blocks = blocks;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the bytes of the archive.
    /// </summary>
    public byte[] Bytes { get; }

    /// <summary>
    /// Gets the positions of the blocks.
    /// </summary>
    public IReadOnlyList<RdaBuiltBlock> Blocks { get; }

    #endregion // Properties
}