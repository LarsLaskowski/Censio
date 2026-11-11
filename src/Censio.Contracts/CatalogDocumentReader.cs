namespace Censio.Contracts;

/// <summary>
/// Reads a catalog document from UTF-8 JSON, strictly and without throwing.
/// </summary>
public static class CatalogDocumentReader
{
    #region Constants

    /// <summary>
    /// The largest document in bytes.
    /// </summary>
    public const int MaxDocumentBytes = 16777216;

    /// <summary>
    /// The largest string in UTF-8 bytes, after unescaping.
    /// </summary>
    public const int MaxStringBytes = 65536;

    /// <summary>
    /// The largest number of entries.
    /// </summary>
    public const int MaxEntries = 16384;

    /// <summary>
    /// The largest number of names per entry.
    /// </summary>
    public const int MaxNamesPerEntry = 16;

    /// <summary>
    /// The largest number of region types per entry.
    /// </summary>
    public const int MaxRegionTypesPerEntry = 32;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads a catalog document.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes</param>
    /// <returns>The document, or the error that refused it</returns>
    public static CatalogDocumentReadResult Read(ReadOnlySpan<byte> utf8Json)
    {
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods
}