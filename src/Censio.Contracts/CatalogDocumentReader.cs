using System.Text.Json;

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
        if (utf8Json.Length > MaxDocumentBytes)
        {
            return Refuse(CatalogDocumentError.CreateTooLarge());
        }

        var scanner = new CatalogDocumentScanner();
        var scanError = scanner.Scan(utf8Json);

        if (scanError is not null)
        {
            return Refuse(scanError);
        }

        if (scanner.Version != CatalogDocument.CurrentSchemaVersion)
        {
            return Refuse(CatalogDocumentError.CreateUnsupportedVersion(scanner.Version));
        }

        CatalogDocument? document;

        try
        {
            document = JsonSerializer.Deserialize(utf8Json, CatalogDocumentJsonContext.Default.CatalogDocument);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Refuse(CatalogDocumentError.CreateMalformed());
        }

        if (document is null || CatalogDocumentRules.ContainsNull(document))
        {
            return Refuse(CatalogDocumentError.CreateMalformed());
        }

        var ruleError = CatalogDocumentRules.Check(document);

        return ruleError is null ? new CatalogDocumentReadResult(document, null) : Refuse(ruleError);
    }

    /// <summary>
    /// Builds the result of a refused document.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    private static CatalogDocumentReadResult Refuse(CatalogDocumentError error)
    {
        return new CatalogDocumentReadResult(null, error);
    }

    #endregion // Methods
}