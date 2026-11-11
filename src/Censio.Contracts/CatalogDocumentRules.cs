namespace Censio.Contracts;

/// <summary>
/// The value rules of the catalog document, shared by reader and writer.
/// </summary>
internal static class CatalogDocumentRules
{
    #region Methods

    /// <summary>
    /// Checks the value rules of a document.
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <returns>The first violation, or <c>null</c> when the document is valid</returns>
    internal static CatalogDocumentError? Check(CatalogDocument document)
    {
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods
}