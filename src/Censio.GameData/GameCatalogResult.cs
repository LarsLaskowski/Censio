namespace Censio.GameData;

/// <summary>
/// Holds the built catalog or the reason it could not be built.
/// </summary>
public sealed class GameCatalogResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogResult" /> class.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="error">The error</param>
    /// <param name="copies">The copies in other archives</param>
    /// <param name="uncheckedArchiveCount">The number of archives that could not be checked for copies</param>
    private GameCatalogResult(GameCatalog? catalog, GameCatalogError? error, IReadOnlyList<GameCatalogCopy> copies, int uncheckedArchiveCount)
    {
        Catalog = catalog;
        Error = error;
        Copies = copies;
        UncheckedArchiveCount = uncheckedArchiveCount;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the catalog, or <c>null</c> on an error.
    /// </summary>
    public GameCatalog? Catalog { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the catalog was built.
    /// </summary>
    public GameCatalogError? Error { get; }

    /// <summary>
    /// Gets the copies of catalog files in other archives.
    /// </summary>
    public IReadOnlyList<GameCatalogCopy> Copies { get; }

    /// <summary>
    /// Gets the number of archives that could not be checked for copies.
    /// </summary>
    public int UncheckedArchiveCount { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of a built catalog.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="copies">The copies in other archives</param>
    /// <param name="uncheckedArchiveCount">The number of archives that could not be checked for copies</param>
    /// <returns>The result</returns>
    public static GameCatalogResult FromCatalog(GameCatalog catalog, IReadOnlyList<GameCatalogCopy> copies, int uncheckedArchiveCount)
    {
        return new GameCatalogResult(catalog, null, copies, uncheckedArchiveCount);
    }

    /// <summary>
    /// Creates the result of a failed build.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static GameCatalogResult FromError(GameCatalogError error)
    {
        return new GameCatalogResult(null, error, [], 0);
    }

    #endregion // Methods
}