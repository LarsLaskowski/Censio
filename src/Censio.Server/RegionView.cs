namespace Censio.Server;

/// <summary>
/// A region as a page shows it.
/// </summary>
public sealed class RegionView
{
    #region Properties

    /// <summary>
    /// Gets the region GUID.
    /// </summary>
    public required int RegionGuid { get; init; }

    /// <summary>
    /// Gets a value indicating whether the region is a session without a catalog region.
    /// </summary>
    public required bool IsSession { get; init; }

    /// <summary>
    /// Gets the GUIDs of the sessions of the region.
    /// </summary>
    public required IReadOnlyList<int> SessionGuids { get; init; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public required ShownName Name { get; init; }

    /// <summary>
    /// Gets the islands.
    /// </summary>
    public required IReadOnlyList<IslandView> Islands { get; init; }

    /// <summary>
    /// Gets the key figures, or <see langword="null"/> when no island has values.
    /// </summary>
    public required KeyFigures? Figures { get; init; }

    #endregion // Properties
}