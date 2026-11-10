namespace Censio.GameData;

/// <summary>
/// Holds what the pass over the asset file collected.
/// </summary>
internal sealed class GameCatalogAssets
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogAssets" /> class.
    /// </summary>
    /// <param name="templates">The facts of the defining templates by name</param>
    /// <param name="defaults">The property defaults</param>
    internal GameCatalogAssets(Dictionary<string, GameCatalogValues> templates, GameCatalogValues defaults)
    {
        Templates = templates;
        Defaults = defaults;
    }

    #endregion // Constructors

    #region Fields

    /// <summary>
    /// The GUIDs seen so far
    /// </summary>
    private readonly HashSet<int> _seen = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the facts of the defining templates by name.
    /// </summary>
    internal Dictionary<string, GameCatalogValues> Templates { get; }

    /// <summary>
    /// Gets the property defaults.
    /// </summary>
    internal GameCatalogValues Defaults { get; }

    /// <summary>
    /// Gets the assets that may become entries.
    /// </summary>
    internal List<GameCatalogAsset> Candidates { get; } = [];

    /// <summary>
    /// Gets the region assets by GUID.
    /// </summary>
    internal Dictionary<int, GameCatalogAsset> Regions { get; } = [];

    /// <summary>
    /// Gets the GUIDs defined more than once.
    /// </summary>
    internal HashSet<int> AmbiguousGuids { get; } = [];

    /// <summary>
    /// Gets or sets the number of inherited assets left out.
    /// </summary>
    internal int InheritedCount { get; set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds a defining asset.
    /// </summary>
    /// <param name="asset">The asset</param>
    internal void Add(GameCatalogAsset asset)
    {
        if (asset.Guid is not { } guid)
        {
            return;
        }

        if (_seen.Add(guid) == false)
        {
            AmbiguousGuids.Add(guid);
        }

        if (asset.Template is null && asset.HasBaseAsset)
        {
            InheritedCount++;

            return;
        }

        Resolve(asset);

        if (string.Equals(asset.Template, "Region", StringComparison.Ordinal))
        {
            Regions.TryAdd(guid, asset);

            return;
        }

        asset.Kind = KindOf(asset);

        if (asset.SessionRegion is not null and not 0 || asset.Kind is not null)
        {
            Candidates.Add(asset);
        }
    }

    /// <summary>
    /// Gets the kind of a product or building.
    /// </summary>
    /// <param name="asset">The asset</param>
    /// <returns>The kind, or <c>null</c> when the asset is neither</returns>
    private static GameCatalogKind? KindOf(GameCatalogAsset asset)
    {
        if (string.Equals(asset.Template, "Product", StringComparison.Ordinal))
        {
            return asset.IsWorkforce ? GameCatalogKind.Workforce : GameCatalogKind.Good;
        }

        return asset.HasBuildingSection ? GameCatalogKind.Building : null;
    }

    /// <summary>
    /// Fills the facts an asset does not state from its template, else from the property defaults.
    /// </summary>
    /// <param name="asset">The asset</param>
    private void Resolve(GameCatalogAsset asset)
    {
        if (asset.Template is null || Templates.TryGetValue(asset.Template, out var template) == false)
        {
            return;
        }

        if (template.HasProduct)
        {
            asset.ProductRegions ??= template.ProductRegions ?? Defaults.ProductRegions;
        }

        if (template.HasBuilding)
        {
            asset.HasBuildingSection = true;
            asset.BuildingRegions ??= template.BuildingRegions ?? Defaults.BuildingRegions;
        }

        if (template.HasSession)
        {
            asset.SessionRegion ??= template.SessionRegion ?? Defaults.SessionRegion;
        }

        if (template.HasRegion)
        {
            asset.RegionId ??= template.RegionId ?? Defaults.RegionId;
        }
    }

    #endregion // Methods
}