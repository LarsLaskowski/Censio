namespace Censio.Pipe;

/// <summary>
/// One station of a trade route.
/// </summary>
public sealed class TradeRouteStation
{
    #region Properties

    /// <summary>
    /// Gets the station ID.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the session ID.
    /// </summary>
    public required byte SessionId { get; init; }

    /// <summary>
    /// Gets the island ID.
    /// </summary>
    public required byte IslandId { get; init; }

    /// <summary>
    /// Gets the area index.
    /// </summary>
    public required byte AreaIndex { get; init; }

    /// <summary>
    /// Gets the session GUID.
    /// </summary>
    public required int SessionGuid { get; init; }

    /// <summary>
    /// Gets the owner.
    /// </summary>
    public required int Owner { get; init; }

    /// <summary>
    /// Gets a value indicating whether goods are thrown overboard.
    /// </summary>
    public required bool ThrowOverboard { get; init; }

    /// <summary>
    /// Gets a value indicating whether the ship waits until full.
    /// </summary>
    public required bool WaitUntilFull { get; init; }

    /// <summary>
    /// Gets a value indicating whether the ship waits until empty.
    /// </summary>
    public required bool WaitUntilEmpty { get; init; }

    /// <summary>
    /// Gets the goods in frame order.
    /// </summary>
    public required IReadOnlyList<TradeRouteGood> Goods { get; init; }

    #endregion // Properties
}