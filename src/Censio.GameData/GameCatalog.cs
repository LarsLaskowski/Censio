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
#pragma warning disable S1144, S4487 // Skeleton: removed with the implementation
    private readonly IReadOnlyCollection<GameCatalogEntry> _entries;

    /// <summary>
    /// The GUIDs defined or named more than once
    /// </summary>
    private readonly IReadOnlyCollection<int> _ambiguousGuids;
#pragma warning restore S1144, S4487

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
        _entries = entries;
        _ambiguousGuids = ambiguousGuids;
        InheritedAssetCount = inheritedAssetCount;
        DroppedRegionTypeCount = droppedRegionTypeCount;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public int Count => throw new NotImplementedException();
#pragma warning restore S2325

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
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public GameCatalogEntry? Find(int assetGuid)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks whether an asset can be named.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The problem, or <see cref="GameCatalogProblem.None" /></returns>
    public GameCatalogProblem Check(int assetGuid)
    {
        throw new NotImplementedException();
    }

#pragma warning restore S2325

    #endregion // Methods
}