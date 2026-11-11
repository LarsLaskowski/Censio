namespace Censio.Contracts;

/// <summary>
/// The result of writing a catalog document: the bytes or an error, never both.
/// </summary>
public sealed class CatalogDocumentWriteResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogDocumentWriteResult" /> class.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON written, empty on an error</param>
    /// <param name="error">The error, or <c>null</c></param>
    internal CatalogDocumentWriteResult(ReadOnlyMemory<byte> utf8Json, CatalogDocumentError? error)
    {
        Utf8Json = utf8Json;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the UTF-8 JSON written, empty when the document was refused.
    /// </summary>
    public ReadOnlyMemory<byte> Utf8Json { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the document was written.
    /// </summary>
    public CatalogDocumentError? Error { get; }

    #endregion // Properties
}