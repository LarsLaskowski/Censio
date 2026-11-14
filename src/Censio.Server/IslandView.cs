namespace Censio.Server;

/// <summary>
/// An island as a page shows it.
/// </summary>
public sealed class IslandView
{
    #region Properties

    /// <summary>
    /// Gets the key.
    /// </summary>
    public required IslandKey Key { get; init; }

    /// <summary>
    /// Gets the shown name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the key figures, or <see langword="null"/> when the island has no values.
    /// </summary>
    public required KeyFigures? Figures { get; init; }

    #endregion // Properties
}