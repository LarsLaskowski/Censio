namespace Censio.Contracts;

/// <summary>
/// The refusal of a catalog document: a kind and a fixed text without document content.
/// </summary>
public sealed class CatalogDocumentError
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogDocumentError" /> class.
    /// </summary>
    /// <param name="kind">The kind of the refusal</param>
    /// <param name="text">The fixed text of the refusal</param>
    internal CatalogDocumentError(CatalogDocumentErrorKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the kind of the refusal.
    /// </summary>
    public CatalogDocumentErrorKind Kind { get; }

    /// <summary>
    /// Gets the fixed text of the refusal.
    /// </summary>
    public string Text { get; }

    #endregion // Properties
}