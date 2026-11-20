namespace Censio.Contracts;

/// <summary>
/// A station of a trade route.
/// </summary>
public sealed class ContractTradeRouteStation
{
    #region Properties

    /// <summary>
    /// Gets the session GUID.
    /// </summary>
    public required int SessionGuid { get; init; }

    /// <summary>
    /// Gets the island id.
    /// </summary>
    public required byte IslandId { get; init; }

    /// <summary>
    /// Gets the area index.
    /// </summary>
    public required byte AreaIndex { get; init; }

    #endregion // Properties
}