using System.Collections.Immutable;

namespace Censio.Server;

/// <summary>
/// What the state knows of the stored catalog: its identity and the references of its icons not stored yet.
/// </summary>
public sealed class CatalogHold
{
    #region Properties

    /// <summary>
    /// Gets the hold of a state without a usable catalog.
    /// </summary>
    public static CatalogHold None { get; } = new()
                                              {
                                                  Identity = null,
                                                  MissingIcons = null,
                                                  Lookup = null
                                              };

    /// <summary>
    /// Gets the identity of the catalog, or <see langword="null"/> when none is stored or it is not usable.
    /// </summary>
    public required string? Identity { get; init; }

    /// <summary>
    /// Gets the references of the icons not stored yet, or <see langword="null"/> when the catalog is not usable.
    /// </summary>
    public required ImmutableHashSet<string>? MissingIcons { get; init; }

    /// <summary>
    /// Gets the lookup over the catalog document, or <see langword="null"/> when the catalog is not usable.
    /// </summary>
    public required PageCatalog? Lookup { get; init; }

    /// <summary>
    /// Gets a value indicating whether the catalog is usable.
    /// </summary>
    public bool IsUsable => Identity is not null && MissingIcons is not null;

    #endregion // Properties
}