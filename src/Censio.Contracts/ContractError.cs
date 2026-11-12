namespace Censio.Contracts;

/// <summary>
/// The refusal of a contract message: a kind and a fixed text without message content.
/// </summary>
public sealed class ContractError
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractError" /> class.
    /// </summary>
    /// <param name="kind">The kind of the refusal</param>
    /// <param name="text">The fixed text of the refusal</param>
    /// <param name="version">The contract version found, or <c>null</c></param>
    /// <param name="catalogError">The catalog error, or <c>null</c></param>
    internal ContractError(ContractErrorKind kind, string text, int? version, CatalogDocumentError? catalogError)
    {
        Kind = kind;
        Text = text;
        Version = version;
        CatalogError = catalogError;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the kind of the refusal.
    /// </summary>
    public ContractErrorKind Kind { get; }

    /// <summary>
    /// Gets the fixed text of the refusal.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the contract version found, or <c>null</c> when none applies.
    /// </summary>
    public int? Version { get; }

    /// <summary>
    /// Gets the catalog error of a refused upload, or <c>null</c>.
    /// </summary>
    public CatalogDocumentError? CatalogError { get; }

    #endregion // Properties
}