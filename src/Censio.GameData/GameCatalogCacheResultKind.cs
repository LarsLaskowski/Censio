namespace Censio.GameData;

/// <summary>
/// Names the outcome of reading the catalog cache.
/// </summary>
public enum GameCatalogCacheResultKind
{
    /// <summary>
    /// The cache matched and holds the catalog and icons.
    /// </summary>
    Loaded,

    /// <summary>
    /// There is no cache.
    /// </summary>
    Missing,

    /// <summary>
    /// The cache was written by another Companion build.
    /// </summary>
    CompanionChanged,

    /// <summary>
    /// The cache was written for another game state.
    /// </summary>
    GameChanged,

    /// <summary>
    /// The cache is damaged or not in the expected format.
    /// </summary>
    Invalid
}