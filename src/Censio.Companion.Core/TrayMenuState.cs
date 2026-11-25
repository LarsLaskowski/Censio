namespace Censio.Companion.Core;

/// <summary>
/// What the tray menu is built from.
/// </summary>
public sealed class TrayMenuState
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

    /// <summary>
    /// Gets the name of the active profile, or <see langword="null" /> when the server line shows none.
    /// </summary>
    public string? ProfileName { get; init; }

    /// <summary>
    /// Gets the pending notice about the active profile, or <see langword="null" /> if none.
    /// </summary>
    public ProfileNotice? Notice { get; init; }

    #endregion // Properties
}