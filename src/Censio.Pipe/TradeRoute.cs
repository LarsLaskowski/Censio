namespace Censio.Pipe;

/// <summary>
/// One trade route.
/// </summary>
public sealed class TradeRoute
{
    #region Properties

    /// <summary>
    /// Gets the route ID.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the route name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the folder name.
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
    /// Gets the stations in frame order.
    /// </summary>
    public required IReadOnlyList<TradeRouteStation> Stations { get; init; }

    #endregion // Properties
}