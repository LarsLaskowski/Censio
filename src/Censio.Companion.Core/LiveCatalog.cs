using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// The catalog a live sender offers to the server: its identity and the messages written from it on demand.
/// </summary>
public sealed class LiveCatalog
{
    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The catalog.
    /// </summary>
    private readonly GameCatalog _catalog;

    /// <summary>
    /// The icons, or <c>null</c> when there are none.
    /// </summary>
    private readonly GameIconSet? _icons;

    /// <summary>
    /// The game build, or <c>null</c> when unknown.
    /// </summary>
    private readonly string? _build;

#pragma warning restore S4487

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCatalog" /> class.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c> when there are none</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    private LiveCatalog(GameCatalog catalog, GameIconSet? icons, string? build)
    {
        _catalog = catalog;
        _icons = icons;
        _build = build;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the identity of the catalog document, or <c>null</c> when the contract writer refused it.
    /// </summary>
    public string? Identity { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the catalog state and computes its identity.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c> when there are none</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    /// <returns>The catalog state</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static LiveCatalog Create(GameCatalog catalog, GameIconSet? icons, string? build)
    {
        _ = new LiveCatalog(catalog, icons, build);

        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    /// <summary>
    /// Writes the catalog upload message.
    /// </summary>
    /// <returns>The written message, or <c>null</c> when the contract writer refused it</returns>
#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation
    public LiveCatalogMessage? WriteUpload()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes the icon message of one icon reference.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <returns>The written message, or <c>null</c> for an unknown reference or a refusal of the contract writer</returns>
    public LiveCatalogMessage? WriteIcon(string reference)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    #endregion // Methods
}