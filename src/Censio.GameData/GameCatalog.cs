using System.Collections.ObjectModel;

namespace Censio.GameData;

/// <summary>
/// Holds the assets of the game catalog.
/// </summary>
public sealed class GameCatalog
{
    #region Fields

    /// <summary>
    /// The entries
    /// </summary>
    private readonly ReadOnlyCollection<GameCatalogEntry> _entries;

    /// <summary>
    /// The entries by asset GUID
    /// </summary>
    private readonly Dictionary<int, GameCatalogEntry> _byGuid = [];

    /// <summary>
    /// The GUIDs defined or named more than once
    /// </summary>
    private readonly HashSet<int> _ambiguousGuids;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalog" /> class.
    /// </summary>
    /// <param name="entries">The entries</param>
    /// <param name="ambiguousGuids">The GUIDs defined or named more than once</param>
    /// <param name="inheritedAssetCount">The number of inherited assets left out</param>
    /// <param name="droppedRegionTypeCount">The number of unknown region types dropped</param>
    public GameCatalog(IReadOnlyCollection<GameCatalogEntry> entries, IReadOnlyCollection<int> ambiguousGuids, int inheritedAssetCount, int droppedRegionTypeCount)
    {
        _entries = entries.ToList().AsReadOnly();
        _ambiguousGuids = [.. ambiguousGuids];

        foreach (var entry in entries)
        {
            _byGuid.TryAdd(entry.AssetGuid, entry);
        }

        InheritedAssetCount = inheritedAssetCount;
        DroppedRegionTypeCount = droppedRegionTypeCount;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets the entries the catalog kept.
    /// </summary>
    public IReadOnlyCollection<GameCatalogEntry> Entries => _entries;

    /// <summary>
    /// Gets the GUIDs defined or named more than once.
    /// </summary>
    public IReadOnlyCollection<int> AmbiguousGuids => _ambiguousGuids;

    /// <summary>
    /// Gets the number of inherited assets left out.
    /// </summary>
    public int InheritedAssetCount { get; }

    /// <summary>
    /// Gets the number of unknown region types dropped.
    /// </summary>
    public int DroppedRegionTypeCount { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Finds the entry of an asset.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The entry, or <c>null</c> when there is none or the GUID is ambiguous</returns>
    public GameCatalogEntry? Find(int assetGuid)
    {
        return _ambiguousGuids.Contains(assetGuid) ? null : _byGuid.GetValueOrDefault(assetGuid);
    }

    /// <summary>
    /// Checks whether an asset can be named.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The problem, or <see cref="GameCatalogProblem.None" /></returns>
    public GameCatalogProblem Check(int assetGuid)
    {
        if (_ambiguousGuids.Contains(assetGuid))
        {
            return GameCatalogProblem.Ambiguous;
        }

        if (_byGuid.TryGetValue(assetGuid, out var entry) == false)
        {
            return GameCatalogProblem.Unknown;
        }

        var names = (string.IsNullOrEmpty(entry.EnglishName) ? 0 : 1) + (string.IsNullOrEmpty(entry.GermanName) ? 0 : 1);

        return names switch
               {
                   2 => GameCatalogProblem.None,
                   1 => GameCatalogProblem.Incomplete,
                   _ => GameCatalogProblem.Nameless
               };
    }

    #endregion // Methods
}