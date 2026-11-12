namespace Censio.Contracts;

/// <summary>
/// The reason the server refused a message.
/// </summary>
public enum ContractAcknowledgementRefusal
{
    #region Values

    /// <summary>
    /// The message is malformed.
    /// </summary>
    Malformed,

    /// <summary>
    /// The contract version is not supported.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// The message breaks a value rule.
    /// </summary>
    Invalid,

    /// <summary>
    /// The message exceeds a size bound.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The catalog document is malformed.
    /// </summary>
    CatalogMalformed,

    /// <summary>
    /// The catalog schema version is not supported.
    /// </summary>
    CatalogUnsupportedVersion,

    /// <summary>
    /// The catalog document breaks a value rule.
    /// </summary>
    CatalogInvalid,

    /// <summary>
    /// The catalog document exceeds a size bound.
    /// </summary>
    CatalogTooLarge

    #endregion // Values
}