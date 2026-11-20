namespace Censio.Server.Data;

/// <summary>
/// One trade route of the <c>TradeRoutes</c> table.
/// </summary>
internal sealed class TradeRouteRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the profile the route belongs to.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the position in the message.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the id of the route.
    /// </summary>
    public int RouteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the route.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the folder of the route.
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of major problems.
    /// </summary>
    public int MajorProblems { get; set; }

    /// <summary>
    /// Gets or sets the number of minor problems.
    /// </summary>
    public int MinorProblems { get; set; }

    /// <summary>
    /// Gets or sets the number of ships.
    /// </summary>
    public int Ships { get; set; }

    /// <summary>
    /// Gets or sets the number of lost ships.
    /// </summary>
    public int LostShips { get; set; }

    #endregion // Properties
}