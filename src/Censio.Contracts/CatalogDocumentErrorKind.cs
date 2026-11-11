namespace Censio.Contracts;

/// <summary>
/// The reason a catalog document was refused.
/// </summary>
public enum CatalogDocumentErrorKind
{
    #region Values

    /// <summary>
    /// The input is not a well-formed document of the expected structure.
    /// </summary>
    Malformed,

    /// <summary>
    /// The schema version is not the supported one.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// A value breaks a value rule.
    /// </summary>
    Invalid,

    /// <summary>
    /// The document, a string or a count exceeds its bound.
    /// </summary>
    TooLarge

    #endregion // Values
}