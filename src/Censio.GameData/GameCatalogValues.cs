namespace Censio.GameData;

/// <summary>
/// Holds the facts a template or the property defaults state for the catalog.
/// </summary>
internal sealed class GameCatalogValues
{
    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether a <c>Product</c> property is present.
    /// </summary>
    internal bool HasProduct { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <c>Building</c> property is present.
    /// </summary>
    internal bool HasBuilding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <c>Session</c> property is present.
    /// </summary>
    internal bool HasSession { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <c>Region</c> property is present.
    /// </summary>
    internal bool HasRegion { get; set; }

    /// <summary>
    /// Gets or sets the <c>Product/AssociatedRegion</c> list, or <c>null</c> when not stated.
    /// </summary>
    internal string? ProductRegions { get; set; }

    /// <summary>
    /// Gets or sets the <c>Building/AssociatedRegions</c> list, or <c>null</c> when not stated.
    /// </summary>
    internal string? BuildingRegions { get; set; }

    /// <summary>
    /// Gets or sets the <c>Session/Region</c> GUID, or <c>null</c> when not stated.
    /// </summary>
    internal int? SessionRegion { get; set; }

    /// <summary>
    /// Gets or sets the <c>Region/RegionID</c>, or <c>null</c> when not stated.
    /// </summary>
    internal string? RegionId { get; set; }

    #endregion // Properties
}