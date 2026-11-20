namespace Censio.Server;

/// <summary>
/// The trade routes of a profile as a page shows them.
/// </summary>
public sealed class TradeRoutesView
{
    #region Properties

    /// <summary>
    /// Gets the receive time of the trade routes.
    /// </summary>
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>
    /// Gets the routes.
    /// </summary>
    public required IReadOnlyList<TradeRouteView> Routes { get; init; }

    #endregion // Properties
}