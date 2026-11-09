using Censio.GameData;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Builds the game catalog and keeps it with its status.
/// </summary>
public sealed class GameCatalogLoader
{
    #region Fields

    /// <summary>
    /// Builds the catalog of an installation folder.
    /// </summary>
#pragma warning disable S1144, S4487 // Skeleton: removed with the implementation
    private readonly Func<string, GameCatalogResult> _build;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
#pragma warning restore S1144, S4487

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogLoader" /> class.
    /// </summary>
    /// <param name="build">Builds the catalog of an installation folder</param>
    /// <param name="logger">The logger</param>
    public GameCatalogLoader(Func<string, GameCatalogResult> build, ILogger logger)
    {
        _build = build;
        _logger = logger;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changed.
    /// </summary>
#pragma warning disable CS0067, S3264 // Skeleton: removed with the implementation
    public event EventHandler<GameCatalogStatus>? StatusChanged;
#pragma warning restore CS0067, S3264

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the status.
    /// </summary>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public GameCatalogStatus Status => throw new NotImplementedException();

    /// <summary>
    /// Gets the catalog, or <c>null</c> if none is built.
    /// </summary>
    public GameCatalog? Catalog => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Builds the catalog for an installation status.
    /// </summary>
    /// <param name="installation">The status of the game installation search</param>
    /// <returns>The resulting status</returns>
    public GameCatalogStatus Run(GameInstallationStatus installation)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325

    #endregion // Methods
}