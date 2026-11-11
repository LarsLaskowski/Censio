using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// One entry of the catalog document.
/// </summary>
public sealed class CatalogDocumentEntry
{
    #region Properties

    /// <summary>
    /// Gets the asset GUID of the entry.
    /// </summary>
    [JsonPropertyName("guid")]
    public required int AssetGuid { get; init; }

    /// <summary>
    /// Gets the kind of the entry.
    /// </summary>
    [JsonPropertyName("kind")]
    public required CatalogEntryKind Kind { get; init; }

    /// <summary>
    /// Gets the names of the entry by language tag.
    /// </summary>
    [JsonPropertyName("names")]
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>
    /// Gets the GUID of the region the entry belongs to, or <c>null</c>.
    /// </summary>
    [JsonPropertyName("regionGuid")]
    public required int? RegionGuid { get; init; }

    /// <summary>
    /// Gets the region types of the entry, or <c>null</c>.
    /// </summary>
    [JsonPropertyName("regionTypes")]
    public required IReadOnlyList<string>? RegionTypes { get; init; }

    /// <summary>
    /// Gets the reference of the entry's icon (lowercase SHA-256 hex), or <c>null</c>.
    /// </summary>
    [JsonPropertyName("icon")]
    public required string? IconReference { get; init; }

    #endregion // Properties
}