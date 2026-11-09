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
    Failed
}