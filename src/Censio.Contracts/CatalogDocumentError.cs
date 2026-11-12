using System.Globalization;

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

    #region Methods

    /// <summary>
    /// Creates the refusal of a malformed document.
    /// </summary>
    /// <returns>The error</returns>
    internal static CatalogDocumentError CreateMalformed()
    {
        return new CatalogDocumentError(CatalogDocumentErrorKind.Malformed, "Catalog document is malformed");
    }

    /// <summary>
    /// Creates the refusal of a schema version that is not supported.
    /// </summary>
    /// <param name="version">The schema version found</param>
    /// <returns>The error</returns>
    internal static CatalogDocumentError CreateUnsupportedVersion(int version)
    {
        return new CatalogDocumentError(CatalogDocumentErrorKind.UnsupportedVersion,
                                        string.Create(CultureInfo.InvariantCulture,
                                                      $"Catalog schema version {version} is not supported; supported: {CatalogDocument.CurrentSchemaVersion}"));
    }

    /// <summary>
    /// Creates the refusal of a document that breaks a value rule.
    /// </summary>
    /// <returns>The error</returns>
    internal static CatalogDocumentError CreateInvalid()
    {
        return new CatalogDocumentError(CatalogDocumentErrorKind.Invalid, "Catalog document breaks a rule of schema version 1");
    }

    /// <summary>
    /// Creates the refusal of a document that exceeds a size bound.
    /// </summary>
    /// <returns>The error</returns>
    internal static CatalogDocumentError CreateTooLarge()
    {
        return new CatalogDocumentError(CatalogDocumentErrorKind.TooLarge, "Catalog document exceeds a size bound");
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    /// Gets the kind of the refusal.
    /// </summary>
    public CatalogDocumentErrorKind Kind { get; }

    /// <summary>
    /// Gets the fixed text of the refusal.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the schema version found for an unsupported version, or <c>null</c> for every other kind.
    /// </summary>
    public int? Version { get; }

    #endregion // Properties
}