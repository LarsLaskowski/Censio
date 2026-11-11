namespace Censio.GameData;

/// <summary>
/// Holds the content read from the catalog cache or the reason there is none.
/// </summary>
public sealed class GameCatalogCacheResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogCacheResult" /> class.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    private GameCatalogCacheResult(GameCatalogCacheResultKind kind, GameCatalog? catalog, GameIconSet? icons)
    {
        Kind = kind;
        Catalog = catalog;
        Icons = icons;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public GameCatalogCacheResultKind Kind { get; }

    /// <summary>
    /// Gets the catalog, or <c>null</c> unless the kind is <see cref="GameCatalogCacheResultKind.Loaded" />.
    /// </summary>
    public GameCatalog? Catalog { get; }

    /// <summary>
    /// Gets the icons, or <c>null</c> unless the kind is <see cref="GameCatalogCacheResultKind.Loaded" />.
    /// </summary>
    public GameIconSet? Icons { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of a loaded cache.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns>The result</returns>
    public static GameCatalogCacheResult FromContent(GameCatalog catalog, GameIconSet icons)
    {
        return new GameCatalogCacheResult(GameCatalogCacheResultKind.Loaded, catalog, icons);
    }

    /// <summary>
    /// Creates the result of a cache that was not loaded.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The result</returns>
    public static GameCatalogCacheResult FromKind(GameCatalogCacheResultKind kind)
    {
        if (kind == GameCatalogCacheResultKind.Loaded)
        {
            throw new ArgumentException("A loaded result needs a catalog and icons", nameof(kind));
        }

        return new GameCatalogCacheResult(kind, null, null);
    }

    #endregion // Methods
}