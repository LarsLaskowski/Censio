namespace Censio.GameData;

/// <summary>
/// Describes one asset of the game catalog.
/// </summary>
public sealed class GameCatalogEntry
{
    #region Properties

    /// <summary>
    /// Gets the asset GUID.
    /// </summary>
    public required int AssetGuid { get; init; }

    /// <summary>
    /// Gets the kind of the entry.
    /// </summary>
    public required GameCatalogKind Kind { get; init; }

    /// <summary>
    /// Gets the English name, or <c>null</c> when there is none.
    /// </summary>
    public string? EnglishName { get; init; }

    /// <summary>
    /// Gets the German name, or <c>null</c> when there is none.
    /// </summary>
    public string? GermanName { get; init; }

    /// <summary>
    /// Gets the GUID of the region asset a session names, or <c>null</c> when there is none.
    /// </summary>
    public int? RegionGuid { get; init; }

    /// <summary>
    /// Gets the region types, or <c>null</c> when the asset states none.
    /// </summary>
    public IReadOnlyList<string>? RegionTypes { get; init; }

    /// <summary>
    /// Gets the icon file name the asset states, or <c>null</c> when the asset states none.
    /// </summary>
    public string? IconFilename { get; init; }

    #endregion // Properties
}