namespace Censio.Companion.Core;

/// <summary>
/// The status of the game catalog.
/// </summary>
public sealed record GameCatalogStatus
{
    #region Properties

    /// <summary>
    /// Gets the status while the catalog is read.
    /// </summary>
    public static GameCatalogStatus Reading { get; } = new()
                                                       {
                                                           Kind = GameCatalogStatusKind.Reading
                                                       };

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public required GameCatalogStatusKind Kind { get; init; }

    /// <summary>
    /// Gets the number of entries of a built catalog.
    /// </summary>
    public int EntryCount { get; init; }

    #endregion // Properties
}