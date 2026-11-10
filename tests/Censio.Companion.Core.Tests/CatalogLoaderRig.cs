using Censio.GameData;
using Censio.Server.Tests;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Holds a catalog loader with fakes for the catalog build, the icon build and the key reader, a real cache store in a temporary
/// folder, a fake clock and a recording logger, and records what the loader raises.
/// </summary>
internal sealed class CatalogLoaderRig : IDisposable
{
    #region Fields

    /// <summary>
    /// The temporary folder that holds the cache folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-loader-rig-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogLoaderRig" /> class.
    /// </summary>
    /// <param name="cacheFolderIsFile">Whether the cache folder is a file, so that the cache cannot be written</param>
    /// <param name="clock">The clock, or <c>null</c> for a fake clock that stands still</param>
    public CatalogLoaderRig(bool cacheFolderIsFile = false, FakeTimeProvider? clock = null)
    {
        Directory.CreateDirectory(_root);

        CacheFolder = Path.Combine(_root, "cache");

        if (cacheFolderIsFile)
        {
            File.WriteAllBytes(CacheFolder, [1]);
        }

        Clock = clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        Store = new GameCatalogCacheStore(CacheFolder, Clock);
        BuiltCatalog = Catalog(2);
        BuiltIcons = Icons();
        CurrentKey = Key();
        Loader = new GameCatalogLoader(_ => Build(), (_, _) => BuildIcons(), installation => ReadKey(installation), Store, Clock, Logger);

        Loader.StatusChanged += (_, status) => Statuses.Add(status);
        Loader.CatalogChanged += (_, catalog) => CatalogEvents.Add((catalog, Loader.Status.Kind));
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the cache folder.
    /// </summary>
    public string CacheFolder { get; }

    /// <summary>
    /// Gets the clock.
    /// </summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    public RecordingLogger Logger { get; } = new();

    /// <summary>
    /// Gets the cache store.
    /// </summary>
    public GameCatalogCacheStore Store { get; }

    /// <summary>
    /// Gets the loader.
    /// </summary>
    public GameCatalogLoader Loader { get; }

    /// <summary>
    /// Gets the statuses the loader raised.
    /// </summary>
    public List<GameCatalogStatus> Statuses { get; } = [];

    /// <summary>
    /// Gets the catalogs the loader published, each with the kind of the status at the moment of the event.
    /// </summary>
    public List<(GameCatalog? Catalog, GameCatalogStatusKind Status)> CatalogEvents { get; } = [];

    /// <summary>
    /// Gets or sets the catalog the build returns.
    /// </summary>
    public GameCatalog BuiltCatalog { get; set; }

    /// <summary>
    /// Gets or sets the icons the icon build returns.
    /// </summary>
    public GameIconSet BuiltIcons { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the catalog build fails.
    /// </summary>
    public bool CatalogFails { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the icon build fails.
    /// </summary>
    public bool IconsFail { get; set; }

    /// <summary>
    /// Gets or sets the key the key reader returns, or <c>null</c> when the installation cannot be read.
    /// </summary>
    public GameCatalogKey? CurrentKey { get; set; }

    /// <summary>
    /// Gets or sets the action that runs inside the catalog build.
    /// </summary>
    public Action? OnBuild { get; set; }

    /// <summary>
    /// Gets or sets the action that runs inside the icon build.
    /// </summary>
    public Action? OnIcons { get; set; }

    /// <summary>
    /// Gets the number of catalog builds.
    /// </summary>
    public int BuildCalls { get; private set; }

    /// <summary>
    /// Gets the number of icon builds.
    /// </summary>
    public int IconCalls { get; private set; }

    /// <summary>
    /// Gets the installations the key reader was asked about.
    /// </summary>
    public List<GameInstallationStatus> KeyReads { get; } = [];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the status of a found installation.
    /// </summary>
    /// <param name="build">The build</param>
    /// <returns>The status</returns>
    public static GameInstallationStatus Found(string build = "1")
    {
        return new GameInstallationStatus
               {
                   Kind = GameInstallationStatusKind.Found,
                   Source = GameInstallationSource.UbisoftConnect,
                   Folder = "C:\\Game",
                   Build = build
               };
    }

    /// <summary>
    /// Creates the key of the tests.
    /// </summary>
    /// <param name="build">The build</param>
    /// <param name="identity">The Companion identity</param>
    /// <returns>The key</returns>
    public static GameCatalogKey Key(string build = "1", string identity = "idA")
    {
        return new GameCatalogKey(identity,
                                  "C:\\Game",
                                  build,
                                  [
                                      new GameArchiveState
                                      {
                                          Path = "a.rda",
                                          Length = 5,
                                          LastWriteTimeUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
                                      }
                                  ]);
    }

    /// <summary>
    /// Creates a catalog of good entries.
    /// </summary>
    /// <param name="count">The number of entries</param>
    /// <returns>The catalog</returns>
    public static GameCatalog Catalog(int count)
    {
        var entries = Enumerable.Range(1, count).Select(guid => new GameCatalogEntry
                                                                {
                                                                    AssetGuid = guid,
                                                                    Kind = guid == 2 ? GameCatalogKind.Region : GameCatalogKind.Good,
                                                                    EnglishName = $"Name {guid}",
                                                                    GermanName = $"Name {guid}"
                                                                }).ToList();

        return new GameCatalog(entries, [], 0, 0);
    }

    /// <summary>
    /// Creates icons for the assets 1 and 2 that share one icon.
    /// </summary>
    /// <returns>The icons</returns>
    public static GameIconSet Icons()
    {
        var icon = new GameIcon(4, 4, PngWriter.Write(4, 4, new byte[64]));

        return new GameIconSet(new Dictionary<int, GameIcon>
                               {
                                   [1] = icon,
                                   [2] = icon
                               },
                               new Dictionary<int, GameIconProblem>(),
                               1,
                               1);
    }

    /// <summary>
    /// Writes a cache file for a key with the catalog and icons of the rig.
    /// </summary>
    /// <param name="key">The key</param>
    public void Prime(GameCatalogKey key)
    {
        var result = Store.Save(key, BuiltCatalog, BuiltIcons);

        Assert.IsTrue(result.IsWritten, "The cache of the test should be written");
    }

    /// <summary>
    /// Gets the level and message of every logged entry.
    /// </summary>
    /// <returns>The lines</returns>
    public List<string> Messages()
    {
        return [.. Logger.Entries.Select(entry => entry.Message)];
    }

    /// <summary>
    /// Builds the catalog.
    /// </summary>
    /// <returns>The result</returns>
    private GameCatalogResult Build()
    {
        BuildCalls++;
        OnBuild?.Invoke();

        return CatalogFails ? GameCatalogResult.FromError(new GameCatalogError(GameCatalogErrorKind.FileMissing, "x")) : GameCatalogResult.FromCatalog(BuiltCatalog, [], 0);
    }

    /// <summary>
    /// Builds the icons.
    /// </summary>
    /// <returns>The result</returns>
    private GameIconResult BuildIcons()
    {
        IconCalls++;
        OnIcons?.Invoke();

        return IconsFail ? GameIconResult.FromError(new GameIconError(GameIconErrorKind.ArchiveMissing, "ui.rda")) : GameIconResult.FromIcons(BuiltIcons);
    }

    /// <summary>
    /// Reads the key.
    /// </summary>
    /// <param name="installation">The installation</param>
    /// <returns>The key</returns>
    private GameCatalogKey? ReadKey(GameInstallationStatus installation)
    {
        KeyReads.Add(installation);

        return CurrentKey;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    #endregion // IDisposable
}