namespace Censio.Server;

/// <summary>
/// A stop of a trade route as a page shows it.
/// </summary>
public sealed class TradeRouteStop
{
    #region Properties

    /// <summary>
    /// Gets the island of the stop.
    /// </summary>
    public required IslandKey Island { get; init; }

    /// <summary>
    /// Gets the name of the island, or <see langword="null"/> when no island list entry names it.
    /// </summary>
    public required string? Name { get; init; }

    #endregion // Properties
}