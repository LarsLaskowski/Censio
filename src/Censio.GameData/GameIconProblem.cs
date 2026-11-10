namespace Censio.GameData;

/// <summary>
/// Names why an asset has no icon.
/// </summary>
public enum GameIconProblem
{
    /// <summary>
    /// There is no problem.
    /// </summary>
    None,

    /// <summary>
    /// The catalog has no entry for the asset.
    /// </summary>
    NoEntry,

    /// <summary>
    /// The asset names no icon file.
    /// </summary>
    NotNamed,

    /// <summary>
    /// No archive holds the named icon.
    /// </summary>
    Missing,

    /// <summary>
    /// The named icon could not be read or decoded.
    /// </summary>
    Unreadable
}