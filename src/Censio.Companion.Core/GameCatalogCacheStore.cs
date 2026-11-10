using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// Loads and saves the catalog cache file in the cache folder.
/// </summary>
public sealed class GameCatalogCacheStore
{
    #region Constants

    /// <summary>
    /// The name of the cache file.
    /// </summary>
    public const string FileName = "catalog.bin";

    #endregion // Constants

    #region Fields

    // Skeleton: removed with the implementation
#pragma warning disable S1144, S4487
    /// <summary>
    /// The cache folder.
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly TimeProvider _clock;
#pragma warning restore S1144, S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogCacheStore" /> class.
    /// </summary>
    /// <param name="folder">The cache folder</param>
    /// <param name="clock">The clock</param>
    public GameCatalogCacheStore(string folder, TimeProvider clock)
    {
        _folder = folder;
        _clock = clock;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Loads the cache.
    /// </summary>
    /// <param name="expected">The key the cache must belong to</param>
    /// <returns>The content, or the reason there is none</returns>
    public GameCatalogCacheResult Load(GameCatalogKey expected)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Saves the cache.
    /// </summary>
    /// <param name="key">The key the content belongs to</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns>The outcome</returns>
    public GameCatalogCacheWriteResult Save(GameCatalogKey key, GameCatalog catalog, GameIconSet icons)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}