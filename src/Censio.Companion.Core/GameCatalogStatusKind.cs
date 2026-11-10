namespace Censio.Companion.Core;

/// <summary>
/// The kind of the status of the game catalog.
/// </summary>
public enum GameCatalogStatusKind
{
    /// <summary>
    /// The catalog is being read, or the game search still runs.
    /// </summary>
    Reading,

    /// <summary>
    /// The catalog is built.
    /// </summary>
    Ready,

    /// <summary>
    /// There is no game installation.
    /// </summary>
    NoGame,

    /// <summary>
    /// The catalog could not be built.
    /// </summary>
    Failed,

    /// <summary>
    /// The game is checked for a change, or the cache is looked up.
    /// </summary>
    Checking,

    /// <summary>
    /// The names are being extracted from the game files.
    /// </summary>
    ExtractingNames,

    /// <summary>
    /// The names are known and the icons are being extracted.
    /// </summary>
    ExtractingIcons,

    /// <summary>
    /// The names are known, but the icons could not be built.
    /// </summary>
    IconsFailed,

    /// <summary>
    /// The catalog is built, but could not be written to the cache.
    /// </summary>
    NotCached
}