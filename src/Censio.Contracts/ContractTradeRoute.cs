namespace Censio.Contracts;

/// <summary>
/// A trade route.
/// </summary>
public sealed class ContractTradeRoute
{
    #region Properties

    /// <summary>
    /// Gets the id of the route.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the name of the route.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the folder of the route.
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    /// Gets the number of major problems.
    /// </summary>
    public required int MajorProblems { get; init; }

    /// <summary>
    /// Gets the number of minor problems.
    /// </summary>
    public required int MinorProblems { get; init; }

    /// <summary>
    /// Gets the number of ships.
    /// </summary>
    public required int Ships { get; init; }

    /// <summary>
    /// Gets the number of lost ships.
    /// </summary>
    public required int LostShips { get; init; }

    /// <summary>
    /// Gets the stations in route order.
    /// </summary>
    public required IReadOnlyList<ContractTradeRouteStation> Stations { get; init; }

    #endregion // Properties
}