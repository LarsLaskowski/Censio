namespace Censio.GameData;

/// <summary>
/// Holds the facts of one defining asset that the catalog uses.
/// </summary>
internal sealed class GameCatalogAsset
{
    #region Properties

    /// <summary>
    /// Gets or sets the asset GUID, or <c>null</c> when the asset states none.
    /// </summary>
    internal int? Guid { get; set; }

    /// <summary>
    /// Gets or sets the template, or <c>null</c> when the asset has none.
    /// </summary>
    internal string? Template { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the asset names a base asset.
    /// </summary>
    internal bool HasBaseAsset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the asset has a building section.
    /// </summary>
    internal bool HasBuildingSection { get; set; }

    /// <summary>
    /// Gets or sets the text identifier, or <c>null</c> when the asset has none.
    /// </summary>
    internal long? TextId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the asset is a workforce product.
    /// </summary>
    internal bool IsWorkforce { get; set; }

    /// <summary>
    /// Gets or sets the associated regions of a product, or <c>null</c> when none are stated.
    /// </summary>
    internal string? ProductRegions { get; set; }

    /// <summary>
    /// Gets or sets the associated regions of a building, or <c>null</c> when none are stated.
    /// </summary>
    internal string? BuildingRegions { get; set; }

    /// <summary>
    /// Gets or sets the GUID of the region asset a session names, <c>0</c> when it states an empty element, or <c>null</c> when it states none.
    /// </summary>
    internal int? SessionRegion { get; set; }

    /// <summary>
    /// Gets or sets the region type a region asset states, or <c>null</c> when it states none.
    /// </summary>
    internal string? RegionId { get; set; }

    /// <summary>
    /// Gets or sets the icon file name, or <c>null</c> when the asset states none.
    /// </summary>
    internal string? IconFilename { get; set; }

    /// <summary>
    /// Gets or sets the kind of the product or building the asset is, or <c>null</c> when it is neither.
    /// </summary>
    internal GameCatalogKind? Kind { get; set; }

    #endregion // Properties
}