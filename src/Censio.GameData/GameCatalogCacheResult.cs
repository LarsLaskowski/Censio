namespace Censio.GameData;

/// <summary>
/// Holds the content read from the catalog cache or the reason there is none.
/// </summary>
public sealed class GameCatalogCacheResult
{
    #region Constructors

    // Skeleton: removed with the implementation
#pragma warning disable S1144, S1172
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
#pragma warning restore S1144, S1172

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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the result of a cache that was not loaded.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The result</returns>
    public static GameCatalogCacheResult FromKind(GameCatalogCacheResultKind kind)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}