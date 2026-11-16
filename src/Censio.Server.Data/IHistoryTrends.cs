namespace Censio.Server.Data;

/// <summary>
/// Reads the points of the trend charts from the history.
/// </summary>
public interface IHistoryTrends
{
    #region Methods

    /// <summary>
    /// Reads the points of one good summed over a set of islands.
    /// </summary>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The trend of the good</returns>
    Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the figure points of one island.
    /// </summary>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The trend of the figures</returns>
    Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken);

    #endregion // Methods
}