using System.Globalization;

namespace Censio.Contracts;

/// <summary>
/// The refusal of a contract message: a kind and a fixed text without message content.
/// </summary>
public sealed class ContractError
{
    #region Constants

    /// <summary>
    /// The fixed text of a malformed message.
    /// </summary>
    internal const string MalformedText = "Contract message is malformed";

    /// <summary>
    /// The fixed text of a message that breaks a value rule.
    /// </summary>
    internal const string InvalidText = "Contract message breaks a rule of contract version 1";

    /// <summary>
    /// The fixed text of a message that exceeds a size bound.
    /// </summary>
    internal const string TooLargeText = "Contract message exceeds a size bound";

    #endregion // Constants

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

    #region Methods

    /// <summary>
    /// Gets the fixed text of a contract version that is not supported.
    /// </summary>
    /// <param name="version">The version found</param>
    /// <returns>The text</returns>
    internal static string GetUnsupportedVersionText(int version)
    {
        return string.Create(CultureInfo.InvariantCulture, $"Contract version {version} is not supported; supported: {ContractReader.CurrentVersion}");
    }

    /// <summary>
    /// Creates the refusal of a malformed message.
    /// </summary>
    /// <returns>The error</returns>
    internal static ContractError CreateMalformed()
    {
        return new ContractError(ContractErrorKind.Malformed, MalformedText, null, null);
    }

    /// <summary>
    /// Creates the refusal of a message of a contract version that is not supported.
    /// </summary>
    /// <param name="version">The version found</param>
    /// <returns>The error</returns>
    internal static ContractError CreateUnsupportedVersion(int version)
    {
        return new ContractError(ContractErrorKind.UnsupportedVersion, GetUnsupportedVersionText(version), version, null);
    }

    /// <summary>
    /// Creates the refusal of a message that breaks a value rule.
    /// </summary>
    /// <returns>The error</returns>
    internal static ContractError CreateInvalid()
    {
        return new ContractError(ContractErrorKind.Invalid, InvalidText, null, null);
    }

    /// <summary>
    /// Creates the refusal of a message that exceeds a size bound.
    /// </summary>
    /// <returns>The error</returns>
    internal static ContractError CreateTooLarge()
    {
        return new ContractError(ContractErrorKind.TooLarge, TooLargeText, null, null);
    }

    /// <summary>
    /// Creates the refusal of an upload whose catalog document the catalog reader refused.
    /// </summary>
    /// <param name="catalogError">The catalog error</param>
    /// <returns>The error</returns>
    internal static ContractError CreateCatalogRefused(CatalogDocumentError catalogError)
    {
        return new ContractError(ContractErrorKind.CatalogRefused, catalogError.Text, null, catalogError);
    }

    /// <summary>
    /// Creates the refusal of a kind that has no version and no catalog error.
    /// </summary>
    /// <param name="kind">The kind: malformed, invalid or too large</param>
    /// <returns>The error</returns>
    internal static ContractError Create(ContractErrorKind kind)
    {
        return kind switch
               {
                   ContractErrorKind.Malformed => CreateMalformed(),
                   ContractErrorKind.TooLarge => CreateTooLarge(),
                   _ => CreateInvalid()
               };
    }

    #endregion // Methods

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