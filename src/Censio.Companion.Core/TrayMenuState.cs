namespace Censio.Companion.Core;

/// <summary>
/// What the tray menu is built from.
/// </summary>
public sealed record TrayMenuState
{
    #region Properties

    /// <summary>
    /// Gets the product version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the snapshot.
    /// </summary>
    public required TraySnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets the status of the game installation search.
    /// </summary>
    public required GameInstallationStatus Game { get; init; }

    /// <summary>
    /// Gets the status of the game catalog.
    /// </summary>
    public required GameCatalogStatus Catalog { get; init; }

    /// <summary>
    /// Gets the status of the server, or <see langword="null" /> when none is hosted or forwarded to.
    /// </summary>
    public TrayServerStatus? Server { get; init; }

    /// <summary>
    /// Gets the clock.
    /// </summary>
    public required TimeProvider Clock { get; init; }

    #endregion // Properties
}