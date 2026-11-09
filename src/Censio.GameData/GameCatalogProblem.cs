namespace Censio.GameData;

/// <summary>
/// Names why a GUID cannot be named from the catalog.
/// </summary>
public enum GameCatalogProblem
{
    /// <summary>
    /// The GUID is named.
    /// </summary>
    None,

    /// <summary>
    /// The catalog holds no entry for the GUID.
    /// </summary>
    Unknown,

    /// <summary>
    /// The entry lacks a fact the GUID's role needs.
    /// </summary>
    Incomplete,

    /// <summary>
    /// The entry has no name in either language.
    /// </summary>
    Nameless,

    /// <summary>
    /// The GUID is defined or named more than once.
    /// </summary>
    Ambiguous
}