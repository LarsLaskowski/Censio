using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Reads what the notification area icon shows.
/// </summary>
internal sealed class TrayIconSources
{
    #region Properties

    /// <summary>
    /// Gets the reader of the current snapshot.
    /// </summary>
    public required Func<TraySnapshot> Snapshot { get; init; }

    /// <summary>
    /// Gets the reader of the status of the game installation search.
    /// </summary>
    public required Func<GameInstallationStatus> Game { get; init; }

    /// <summary>
    /// Gets the reader of the status of the game catalog.
    /// </summary>
    public required Func<GameCatalogStatus> Catalog { get; init; }

    /// <summary>
    /// Gets the reader of the status of the server, <c>null</c> when none is hosted or forwarded to.
    /// </summary>
    public required Func<TrayServerStatus?> Server { get; init; }

    /// <summary>
    /// Gets the reader of the texts in the current language.
    /// </summary>
    public required Func<CompanionTexts> Texts { get; init; }

    #endregion // Properties
}