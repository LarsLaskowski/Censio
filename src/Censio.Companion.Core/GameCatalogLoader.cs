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
    private readonly Func<string, GameCatalogResult> _build;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The status.
    /// </summary>
    private volatile GameCatalogStatus _status = GameCatalogStatus.Reading;

    /// <summary>
    /// The catalog, or <c>null</c> if none is built.
    /// </summary>
    private volatile GameCatalog? _catalog;

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
    public event EventHandler<GameCatalogStatus>? StatusChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the status.
    /// </summary>
    public GameCatalogStatus Status => _status;

    /// <summary>
    /// Gets the catalog, or <c>null</c> if none is built.
    /// </summary>
    public GameCatalog? Catalog => _catalog;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Builds the catalog for an installation status.
    /// </summary>
    /// <param name="installation">The status of the game installation search</param>
    /// <returns>The resulting status</returns>
    public GameCatalogStatus Run(GameInstallationStatus installation)
    {
        if (installation.Kind != GameInstallationStatusKind.Found || installation.Folder is not { } folder)
        {
            return Publish(new GameCatalogStatus
                           {
                               Kind = GameCatalogStatusKind.NoGame
                           },
                           null);
        }

        var result = _build(folder);

        if (result.Catalog is not { } catalog)
        {
            CompanionLog.CatalogFailed(_logger, result.Error?.Kind ?? GameCatalogErrorKind.MalformedFile);

            return Publish(new GameCatalogStatus
                           {
                               Kind = GameCatalogStatusKind.Failed
                           },
                           null);
        }

        CompanionLog.CatalogBuilt(_logger, catalog.Count, catalog.InheritedAssetCount, catalog.DroppedRegionTypeCount);

        foreach (var copy in result.Copies)
        {
            CompanionLog.CatalogCopyFound(_logger, copy.ArchiveName, copy.Path);
        }

        if (result.UncheckedArchiveCount > 0)
        {
            CompanionLog.CatalogArchivesUnchecked(_logger, result.UncheckedArchiveCount);
        }

        return Publish(new GameCatalogStatus
                       {
                           Kind = GameCatalogStatusKind.Ready,
                           EntryCount = catalog.Count
                       },
                       catalog);
    }

    /// <summary>
    /// Publishes the catalog and the status, then raises the event.
    /// </summary>
    /// <param name="status">The status</param>
    /// <param name="catalog">The catalog, or <c>null</c> if none</param>
    /// <returns>The status</returns>
    private GameCatalogStatus Publish(GameCatalogStatus status, GameCatalog? catalog)
    {
        _catalog = catalog;
        _status = status;
        StatusChanged?.Invoke(this, status);

        return status;
    }

    #endregion // Methods
}