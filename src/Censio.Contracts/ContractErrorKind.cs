namespace Censio.Contracts;

/// <summary>
/// The reason a contract message was refused.
/// </summary>
public enum ContractErrorKind
{
    #region Values

    /// <summary>
    /// The input is not a well-formed message of the expected structure.
    /// </summary>
    Malformed,

    /// <summary>
    /// The contract version is not the supported one.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// A value breaks a value rule.
    /// </summary>
    Invalid,

    /// <summary>
    /// The message, a string or a count exceeds its bound.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The catalog document of an upload was refused.
    /// </summary>
    CatalogRefused

    #endregion // Values
}