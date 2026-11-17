using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Reads the charts of the data pages from the history.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The semaphore is never waited on through its handle, so it holds nothing to dispose")]
public sealed class TrendReader
{
    #region Fields

    /// <summary>
    /// The history the charts are read from.
    /// </summary>
    private readonly IHistoryTrends _trends;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger<TrendReader> _logger;

    /// <summary>
    /// The gate that lets one chart read run at a time.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendReader"/> class.
    /// </summary>
    /// <param name="trends">The history the charts are read from</param>
    /// <param name="logger">The logger</param>
    public TrendReader(IHistoryTrends trends, ILogger<TrendReader> logger)
    {
        _trends = trends;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the chart of one good over a set of islands: the production, consumption and delta.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="islands">The islands to sum</param>
    /// <param name="productGuid">The product GUID of the good</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    public Task<TrendRead> ReadGoodAsync(Guid profileId, IReadOnlyList<IslandKey> islands, int productGuid, DateTimeOffset latestTick, CancellationToken cancellationToken)
    {
        var range = TrendRange.Before(latestTick);
        var query = new HistoryGoodTrendQuery
                    {
                        ProfileId = profileId,
                        Islands = [.. islands.Select(island => new HistoryIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex))],
                        ProductGuid = productGuid,
                        RangeStart = range.Start,
                        RangeEnd = range.End,
                        MaxPoints = TrendRange.MaxPoints
                    };

        return ReadAsync(async () =>
                         {
                             var trend = await _trends.ReadGoodTrendAsync(query, cancellationToken).ConfigureAwait(false);

                             return Convert(trend, point => point.Time, point => point.IsHourly, point => [point.Generation, point.Consumption, point.Delta]);
                         },
                         cancellationToken);
    }

    /// <summary>
    /// Reads the chart of the key figures of one island: the buildings, produced goods and goods in deficit.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    public Task<TrendRead> ReadFiguresAsync(Guid profileId, IslandKey island, DateTimeOffset latestTick, CancellationToken cancellationToken)
    {
        var range = TrendRange.Before(latestTick);
        var query = new HistoryFiguresTrendQuery
                    {
                        ProfileId = profileId,
                        Island = new HistoryIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex),
                        RangeStart = range.Start,
                        RangeEnd = range.End,
                        MaxPoints = TrendRange.MaxPoints
                    };

        return ReadAsync(async () =>
                         {
                             var trend = await _trends.ReadFiguresTrendAsync(query, cancellationToken).ConfigureAwait(false);

                             return Convert(trend, point => point.Time, point => point.IsHourly, point => [point.Buildings, point.ProducedGoods, point.GoodsInDeficit]);
                         },
                         cancellationToken);
    }

    /// <summary>
    /// Converts a trend of the history into a view.
    /// </summary>
    /// <typeparam name="TPoint">The type of a point of the history</typeparam>
    /// <param name="trend">The trend</param>
    /// <param name="time">Gives the time of a point</param>
    /// <param name="isHourly">Tells whether a point is hourly</param>
    /// <param name="values">Gives the values of a point in line order</param>
    /// <returns>The view</returns>
    private static TrendView Convert<TPoint>(HistoryTrend<TPoint> trend, Func<TPoint, DateTimeOffset> time, Func<TPoint, bool> isHourly, Func<TPoint, double?[]> values)
    {
        return new TrendView
               {
                   Points = [
                                .. trend.Points.Select(point => new TrendPoint
                                                                {
                                                                    Time = time(point),
                                                                    IsHourly = isHourly(point),
                                                                    Values = values(point)
                                                                })
                            ],
                   TotalCount = trend.TotalCount
               };
    }

    /// <summary>
    /// Runs one chart read at a time: a cancelled read rethrows, any other failure is logged without values and reported.
    /// </summary>
    /// <param name="read">The read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read</returns>
    private async Task<TrendRead> ReadAsync(Func<Task<TrendView>> read, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return new TrendRead
                   {
                       View = await read().ConfigureAwait(false),
                       IsFailed = false
                   };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            UiLog.ChartReadFailed(_logger, exception);

            return new TrendRead
                   {
                       View = null,
                       IsFailed = true
                   };
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion // Methods
}