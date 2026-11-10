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
    /// Builds the icons of a catalog, or <c>null</c> when none are built.
    /// </summary>
    private readonly Func<string, GameCatalog, GameIconResult> _buildIcons;

    // Skeleton: removed with the implementation
#pragma warning disable S1144, S4487
    /// <summary>
    /// Reads the key of an installation, or <c>null</c> when it cannot be read.
    /// </summary>
    private readonly Func<GameInstallationStatus, GameCatalogKey?> _readKey;

    /// <summary>
    /// The catalog cache.
    /// </summary>
    private readonly GameCatalogCacheStore _cache;

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly TimeProvider _clock;
#pragma warning restore S1144, S4487

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

    /// <summary>
    /// The icons, or <c>null</c> if none are built.
    /// </summary>
    private volatile GameIconSet? _icons;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogLoader" /> class.
    /// </summary>
    /// <param name="build">Builds the catalog of an installation folder</param>
    /// <param name="buildIcons">Builds the icons of a catalog</param>
    /// <param name="readKey">Reads the key of an installation, or <c>null</c> when it cannot be read</param>
    /// <param name="cache">The catalog cache</param>
    /// <param name="clock">The clock</param>
    /// <param name="logger">The logger</param>
    public GameCatalogLoader(Func<string, GameCatalogResult> build,
                             Func<string, GameCatalog, GameIconResult> buildIcons,
                             Func<GameInstallationStatus, GameCatalogKey?> readKey,
                             GameCatalogCacheStore cache,
                             TimeProvider clock,
                             ILogger logger)
    {
        _build = build;
        _buildIcons = buildIcons;
        _readKey = readKey;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changed.
    /// </summary>
    public event EventHandler<GameCatalogStatus>? StatusChanged;

    /// <summary>
    /// Occurs when the icons changed.
    /// </summary>
    public event EventHandler? IconsChanged;

    /// <summary>
    /// Occurs when a catalog instance, or none after a failure, was published.
    /// </summary>
#pragma warning disable CS0067 // Skeleton: removed with the implementation
    public event EventHandler<GameCatalog?>? CatalogChanged;
#pragma warning restore CS0067

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

    /// <summary>
    /// Gets the icons, or <c>null</c> if none are built.
    /// </summary>
    public GameIconSet? Icons => _icons;

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

        var ready = Publish(new GameCatalogStatus
                            {
                                Kind = GameCatalogStatusKind.Ready,
                                EntryCount = catalog.Count
                            },
                            catalog);

        BuildIcons(folder, catalog);

        return ready;
    }

    /// <summary>
    /// Checks the game for a change when the pipe connected, and extracts the catalog again when it changed.
    /// </summary>
    /// <param name="status">The status of the pipe connection</param>
    /// <param name="refreshInstallation">Reads the build of the installation again</param>
    /// <returns>The task of the check; completed at once when no check is needed</returns>
    public Task OnConnectionStatusChangedAsync(PipeConnectionStatus status, Func<GameInstallationStatus> refreshInstallation)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Builds the icons of a published catalog, publishes them and raises the event.
    /// </summary>
    /// <param name="folder">The folder of the installation</param>
    /// <param name="catalog">The catalog</param>
    private void BuildIcons(string folder, GameCatalog catalog)
    {
        var result = _buildIcons(folder, catalog);

        if (result.Icons is not { } icons)
        {
            var error = result.Error;

            CompanionLog.CatalogIconsFailed(_logger, error?.Kind ?? GameIconErrorKind.ArchiveUnreadable, error?.Archive ?? string.Empty);

            return;
        }

        var notNamed = icons.Count(GameIconProblem.NotNamed);
        var missing = icons.Count(GameIconProblem.Missing);
        var unreadable = icons.Count(GameIconProblem.Unreadable);

        CompanionLog.CatalogIconsBuilt(_logger, icons.GoodsWithIcon, icons.RegionsWithIcon, notNamed, missing, unreadable);
        _icons = icons;
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Publishes the catalog and the status, then raises the event.
    /// </summary>
    /// <param name="status">The status</param>
    /// <param name="catalog">The catalog, or <c>null</c> if none</param>
    /// <returns>The status</returns>
    private GameCatalogStatus Publish(GameCatalogStatus status, GameCatalog? catalog)
    {
        var hadIcons = _icons is not null;

        _icons = null;
        _catalog = catalog;
        _status = status;
        StatusChanged?.Invoke(this, status);

        if (hadIcons)
        {
            IconsChanged?.Invoke(this, EventArgs.Empty);
        }

        return status;
    }

    #endregion // Methods
}