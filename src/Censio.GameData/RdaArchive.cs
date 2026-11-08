namespace Censio.GameData;

/// <summary>
/// Reads the entries of an RDA archive of format version 2.2 within fixed limits.
/// </summary>
public sealed class RdaArchive : IDisposable
{
    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The stream the archive is read from.
    /// </summary>
    private readonly Stream _stream;

    /// <summary>
    /// The limits the archive is read within.
    /// </summary>
    private readonly RdaLimits _limits;

#pragma warning restore S4487 // Skeleton: removed with the implementation

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaArchive" /> class.
    /// </summary>
    /// <param name="stream">The stream the archive is read from</param>
    /// <param name="limits">The limits the archive is read within</param>
    /// <param name="entries">The listed entries</param>
    /// <param name="encryptedBlockCount">The number of encrypted blocks</param>
    internal RdaArchive(Stream stream, RdaLimits limits, IReadOnlyList<RdaEntry> entries, int encryptedBlockCount)
    {
        _stream = stream;
        _limits = limits;
        Entries = entries;
        EncryptedBlockCount = encryptedBlockCount;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the entries in archive order, deleted and encrypted blocks excluded.
    /// </summary>
    public IReadOnlyList<RdaEntry> Entries { get; }

    /// <summary>
    /// Gets the number of encrypted blocks that were skipped.
    /// </summary>
    public int EncryptedBlockCount { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the archive file for reading, shared with other readers and writers.
    /// </summary>
    /// <param name="path">The path of the archive file</param>
    /// <param name="limits">The limits to read within</param>
    /// <returns>The archive or the reason it could not be opened</returns>
    public static RdaOpenResult Open(string path, RdaLimits limits)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Opens an archive from a stream, which the archive takes ownership of.
    /// </summary>
    /// <param name="stream">The readable, seekable stream</param>
    /// <param name="limits">The limits to read within</param>
    /// <returns>The archive or the reason it could not be opened</returns>
    public static RdaOpenResult Open(Stream stream, RdaLimits limits)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the decompressed content of the entry of a path.
    /// </summary>
    /// <param name="path">The path inside the archive</param>
    /// <returns>The content or the reason it could not be read</returns>
    public RdaReadResult Read(string path)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion // IDisposable
}