using System.Globalization;

using Censio.GameData;
using Censio.Pipe;

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

    /// <summary>
    /// The key of the last extraction or cache load, or <c>null</c> when it could not be read.
    /// </summary>
    private volatile GameCatalogKey? _lastKey;

    /// <summary>
    /// The last installation that was found, or <c>null</c> when there is none.
    /// </summary>
    private volatile GameInstallationStatus? _lastInstallation;

    /// <summary>
    /// Whether a start run or a check is running: 1 from construction until the start run ends, and during each later check.
    /// </summary>
    private int _isBusy = 1;

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
    public event EventHandler<GameCatalog?>? CatalogChanged;

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
    /// Loads the catalog from the cache, or builds it, for an installation status.
    /// </summary>
    /// <param name="installation">The status of the game installation search</param>
    /// <returns>The resulting status</returns>
    public GameCatalogStatus Run(GameInstallationStatus installation)
    {
        try
        {
            return Start(installation);
        }
        finally
        {
            Volatile.Write(ref _isBusy, 0);
        }
    }

    /// <summary>
    /// Checks the game for a change when the pipe connected, and extracts the catalog again when it changed.
    /// </summary>
    /// <param name="status">The status of the pipe connection</param>
    /// <param name="refreshInstallation">Reads the build of the installation again</param>
    /// <returns>The task of the check; completed at once when no check is needed</returns>
    public Task OnConnectionStatusChangedAsync(PipeConnectionStatus status, Func<GameInstallationStatus> refreshInstallation)
    {
        if (status.State != PipeConnectionState.Connected || _lastInstallation is null || Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() => Check(refreshInstallation));
    }

    /// <summary>
    /// Names the reason a cache is not used.
    /// </summary>
    /// <param name="kind">The kind of the cache result</param>
    /// <returns>The reason, empty for a cache that is invalid</returns>
    private static string Describe(GameCatalogCacheResultKind kind)
    {
        return kind switch
               {
                   GameCatalogCacheResultKind.Missing => "missing",
                   GameCatalogCacheResultKind.CompanionChanged => "Companion changed",
                   GameCatalogCacheResultKind.GameChanged => "game changed",
                   _ => string.Empty
               };
    }

    /// <summary>
    /// Formats a duration in seconds with one decimal.
    /// </summary>
    /// <param name="duration">The duration</param>
    /// <returns>The text</returns>
    private static string FormatSeconds(TimeSpan duration)
    {
        return duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Runs the start: no game, a cache load or an extraction.
    /// </summary>
    /// <param name="installation">The status of the game installation search</param>
    /// <returns>The resulting status</returns>
    private GameCatalogStatus Start(GameInstallationStatus installation)
    {
        if (installation.Kind != GameInstallationStatusKind.Found || installation.Folder is not { } folder)
        {
            _lastInstallation = null;

            return Publish(new GameCatalogStatus
                           {
                               Kind = GameCatalogStatusKind.NoGame
                           },
                           null,
                           true);
        }

        _lastInstallation = installation;

        var started = _clock.GetTimestamp();

        Publish(new GameCatalogStatus
                {
                    Kind = GameCatalogStatusKind.Checking
                },
                _catalog,
                false);

        var key = _readKey(installation);

        _lastKey = key;

        var reason = "game changed";

        if (key is not null)
        {
            var cached = _cache.Load(key);

            if (cached.Kind == GameCatalogCacheResultKind.Loaded)
            {
                return PublishCached(cached, started);
            }

            reason = Describe(cached.Kind);
        }

        if (reason.Length == 0)
        {
            CompanionLog.CatalogCacheInvalid(_logger);
        }
        else
        {
            CompanionLog.CatalogCacheNotUsed(_logger, reason);
        }

        return Extract(folder, key);
    }

    /// <summary>
    /// Checks a later connection for a change of the game.
    /// </summary>
    /// <param name="refreshInstallation">Reads the build of the installation again</param>
    private void Check(Func<GameInstallationStatus> refreshInstallation)
    {
        try
        {
            var installation = refreshInstallation();
            var key = _readKey(installation);

            if (key is null)
            {
                CompanionLog.CatalogCheckFailed(_logger);

                return;
            }

            if (_lastKey is { } last && last.IsSameGame(key))
            {
                return;
            }

            if (installation.Folder is not { } folder)
            {
                return;
            }

            _lastInstallation = installation;
            _lastKey = key;
            Extract(folder, key);
        }
        catch (Exception)
        {
            CompanionLog.CatalogCheckFailed(_logger);
        }
        finally
        {
            Volatile.Write(ref _isBusy, 0);
        }
    }

    /// <summary>
    /// Publishes the content of the cache.
    /// </summary>
    /// <param name="cached">The result of the cache</param>
    /// <param name="started">The timestamp the start began at</param>
    /// <returns>The resulting status</returns>
    private GameCatalogStatus PublishCached(GameCatalogCacheResult cached, long started)
    {
        var catalog = cached.Catalog!;
        var icons = cached.Icons!;
        var status = new GameCatalogStatus
                     {
                         Kind = GameCatalogStatusKind.Ready,
                         EntryCount = catalog.Count
                     };

        _catalog = catalog;
        CatalogChanged?.Invoke(this, catalog);
        _icons = icons;
        IconsChanged?.Invoke(this, EventArgs.Empty);
        _status = status;
        StatusChanged?.Invoke(this, status);

        var seconds = FormatSeconds(_clock.GetElapsedTime(started));

        CompanionLog.CatalogLoadedFromCache(_logger, seconds, catalog.Count, icons.GoodsWithIcon, icons.RegionsWithIcon);

        return status;
    }

    /// <summary>
    /// Extracts the catalog and the icons from the game files and caches them.
    /// </summary>
    /// <param name="folder">The folder of the installation</param>
    /// <param name="key">The key read before the extraction, or <c>null</c> when it could not be read</param>
    /// <returns>The resulting status</returns>
    private GameCatalogStatus Extract(string folder, GameCatalogKey? key)
    {
        Publish(new GameCatalogStatus
                {
                    Kind = GameCatalogStatusKind.ExtractingNames
                },
                _catalog,
                false);

        var started = _clock.GetTimestamp();
        var result = _build(folder);
        var namesDone = _clock.GetTimestamp();

        if (result.Catalog is not { } catalog)
        {
            CompanionLog.CatalogFailed(_logger, result.Error?.Kind ?? GameCatalogErrorKind.MalformedFile);

            return Publish(new GameCatalogStatus
                           {
                               Kind = GameCatalogStatusKind.Failed
                           },
                           null,
                           true);
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

        Publish(new GameCatalogStatus
                {
                    Kind = GameCatalogStatusKind.ExtractingIcons,
                    EntryCount = catalog.Count
                },
                catalog,
                true);

        var icons = BuildIcons(folder, catalog);
        var iconsDone = _clock.GetTimestamp();

        var total = FormatSeconds(_clock.GetElapsedTime(started, iconsDone));
        var nameSeconds = FormatSeconds(_clock.GetElapsedTime(started, namesDone));
        var iconSeconds = FormatSeconds(_clock.GetElapsedTime(namesDone, iconsDone));

        CompanionLog.CatalogExtracted(_logger, total, nameSeconds, iconSeconds);

        if (icons is null)
        {
            return Publish(new GameCatalogStatus
                           {
                               Kind = GameCatalogStatusKind.IconsFailed,
                               EntryCount = catalog.Count
                           },
                           catalog,
                           false);
        }

        return Save(key, catalog, icons);
    }

    /// <summary>
    /// Writes the cache, if there is a key, and publishes the final status.
    /// </summary>
    /// <param name="key">The key, or <c>null</c> when it could not be read</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns>The final status</returns>
    private GameCatalogStatus Save(GameCatalogKey? key, GameCatalog catalog, GameIconSet icons)
    {
        var kind = GameCatalogStatusKind.Ready;

        if (key is not null)
        {
            var written = _cache.Save(key, catalog, icons);

            if (written.IsWritten == false)
            {
                CompanionLog.CatalogCacheNotWritten(_logger, written.Failure);
                kind = GameCatalogStatusKind.NotCached;
            }
        }

        return Publish(new GameCatalogStatus
                       {
                           Kind = kind,
                           EntryCount = catalog.Count
                       },
                       catalog,
                       false);
    }

    /// <summary>
    /// Builds the icons of a published catalog, publishes them and raises the event.
    /// </summary>
    /// <param name="folder">The folder of the installation</param>
    /// <param name="catalog">The catalog</param>
    /// <returns>The icons, or <c>null</c> when they could not be built</returns>
    private GameIconSet? BuildIcons(string folder, GameCatalog catalog)
    {
        var result = _buildIcons(folder, catalog);

        if (result.Icons is not { } icons)
        {
            var error = result.Error;

            CompanionLog.CatalogIconsFailed(_logger, error?.Kind ?? GameIconErrorKind.ArchiveUnreadable, error?.Archive ?? string.Empty);

            return null;
        }

        var notNamed = icons.Count(GameIconProblem.NotNamed);
        var missing = icons.Count(GameIconProblem.Missing);
        var unreadable = icons.Count(GameIconProblem.Unreadable);

        CompanionLog.CatalogIconsBuilt(_logger, icons.GoodsWithIcon, icons.RegionsWithIcon, notNamed, missing, unreadable);
        _icons = icons;
        IconsChanged?.Invoke(this, EventArgs.Empty);

        return icons;
    }

    /// <summary>
    /// Publishes the status and the catalog, then raises the events.
    /// </summary>
    /// <param name="status">The status</param>
    /// <param name="catalog">The catalog, or <c>null</c> if none</param>
    /// <param name="clearIcons">Whether the published icons go</param>
    /// <returns>The status</returns>
    private GameCatalogStatus Publish(GameCatalogStatus status, GameCatalog? catalog, bool clearIcons)
    {
        var previous = _catalog;
        var hadIcons = clearIcons && _icons is not null;

        if (clearIcons)
        {
            _icons = null;
        }

        _catalog = catalog;
        _status = status;
        StatusChanged?.Invoke(this, status);

        if (hadIcons)
        {
            IconsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (ReferenceEquals(previous, catalog) == false)
        {
            CatalogChanged?.Invoke(this, catalog);
        }

        return status;
    }

    #endregion // Methods
}