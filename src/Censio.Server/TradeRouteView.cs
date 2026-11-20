namespace Censio.Server;

/// <summary>
/// A trade route as a page shows it.
/// </summary>
public sealed class TradeRouteView
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
    /// Gets the stops in route order.
    /// </summary>
    public required IReadOnlyList<TradeRouteStop> Stations { get; init; }

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325, CA1065
    /// <summary>
    /// Gets a value indicating whether a problem count or the lost ships are above 0.
    /// </summary>
    public bool HasProblem => throw new NotImplementedException();
#pragma warning restore RH2003, S2325, CA1065

    #endregion // Properties
}