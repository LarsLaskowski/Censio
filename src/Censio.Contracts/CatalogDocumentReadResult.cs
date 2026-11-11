namespace Censio.Contracts;

/// <summary>
/// The result of reading a catalog document: the document or an error, never both.
/// </summary>
public sealed class CatalogDocumentReadResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogDocumentReadResult" /> class.
    /// </summary>
    /// <param name="document">The document read, or <c>null</c></param>
    /// <param name="error">The error, or <c>null</c></param>
    internal CatalogDocumentReadResult(CatalogDocument? document, CatalogDocumentError? error)
    {
        Document = document;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the document read, or <c>null</c> when it was refused.
    /// </summary>
    public CatalogDocument? Document { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the document was read.
    /// </summary>
    public CatalogDocumentError? Error { get; }

    #endregion // Properties
}