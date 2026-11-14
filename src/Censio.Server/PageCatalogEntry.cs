using Censio.Contracts;

namespace Censio.Server;

/// <summary>
/// An entry of the catalog as the data pages need it: a name and an icon for an asset.
/// </summary>
public sealed class PageCatalogEntry
{
    #region Properties

    /// <summary>
    /// Gets the kind of the entry.
    /// </summary>
    public required CatalogEntryKind Kind { get; init; }

    /// <summary>
    /// Gets the English name, or <see langword="null"/> when the catalog has none.
    /// </summary>
    public required string? English { get; init; }

    /// <summary>
    /// Gets the German name, or <see langword="null"/> when the catalog has none.
    /// </summary>
    public required string? German { get; init; }

    /// <summary>
    /// Gets the GUID of the region asset of a session entry, or <see langword="null"/> when there is none.
    /// </summary>
    public required int? RegionGuid { get; init; }

    /// <summary>
    /// Gets the reference of the entry's icon, or <see langword="null"/> when there is none.
    /// </summary>
    public required string? IconReference { get; init; }

    #endregion // Properties
}