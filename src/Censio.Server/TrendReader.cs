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
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    public Task<TrendRead> ReadGoodAsync(Guid profileId, IReadOnlyList<IslandKey> islands, int productGuid, TrendRequest request, CancellationToken cancellationToken)
    {
        var range = TrendRange.For(request.Choice.Range, request.LatestTick);
        var query = new HistoryGoodTrendQuery
                    {
                        ProfileId = profileId,
                        Islands = [.. islands.Select(island => new HistoryIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex))],
                        ProductGuid = productGuid,
                        RangeStart = ReadStart(request, range),
                        RangeEnd = range.End,
                        MaxPoints = TrendRange.MaxPoints,
                        Resolution = TrendRange.ResolutionOf(request.Choice.Range)
                    };

        return ReadAsync(async () =>
                         {
                             var trend = await _trends.ReadGoodTrendAsync(query, cancellationToken).ConfigureAwait(false);

                             return ReadOf(Merge(Convert(trend, point => point.Time, point => point.IsHourly, point => [point.Generation, point.Consumption, point.Delta], point => (point.GameTime, point.FirstGameTime, point.HasNoEntries)), request, range));
                         },
                         FailedRead,
                         cancellationToken);
    }

    /// <summary>
    /// Reads the chart of the key figures of one island: the buildings, produced goods and goods in deficit.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island</param>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    public Task<TrendRead> ReadFiguresAsync(Guid profileId, IslandKey island, TrendRequest request, CancellationToken cancellationToken)
    {
        var range = TrendRange.For(request.Choice.Range, request.LatestTick);
        var query = new HistoryFiguresTrendQuery
                    {
                        ProfileId = profileId,
                        Island = new HistoryIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex),
                        RangeStart = ReadStart(request, range),
                        RangeEnd = range.End,
                        MaxPoints = TrendRange.MaxPoints,
                        Resolution = TrendRange.ResolutionOf(request.Choice.Range)
                    };

        return ReadAsync(async () =>
                         {
                             var trend = await _trends.ReadFiguresTrendAsync(query, cancellationToken).ConfigureAwait(false);

                             return ReadOf(Merge(Convert(trend, point => point.Time, point => point.IsHourly, point => [point.Buildings, point.ProducedGoods, point.GoodsInDeficit], point => (point.GameTime, point.FirstGameTime, point.HasNoEntries)), request, range));
                         },
                         FailedRead,
                         cancellationToken);
    }

    /// <summary>
    /// Reads the sparklines of the deltas of several goods of one island.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island</param>
    /// <param name="productGuids">The product GUIDs of the goods</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="held">The read held for the address, or <see langword="null"/> for none</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    public Task<SparklineRead<int>> ReadGoodSparklinesAsync(Guid profileId, IslandKey island, IReadOnlyList<int> productGuids, DateTimeOffset latestTick, SparklineRead<int>? held, CancellationToken cancellationToken)
    {
        var historyIsland = new HistoryIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex);

        return ReadSparklinesAsync(productGuids,
                                   latestTick,
                                   held,
                                   2,
                                   async (products, start, end) =>
                                   {
                                       var query = new HistoryGoodsTrendQuery
                                                   {
                                                       ProfileId = profileId,
                                                       Island = historyIsland,
                                                       ProductGuids = products,
                                                       RangeStart = start,
                                                       RangeEnd = end,
                                                       MaxPoints = TrendRange.SparklineMaxPoints
                                                   };
                                       var trend = await _trends.ReadGoodsTrendAsync(query, cancellationToken).ConfigureAwait(false);

                                       return trend.Points.Select(point => (point.Time, point.IsHourly, (Func<int, double?>)(product => point.Deltas.TryGetValue(product, out var delta) ? delta : null))).ToList();
                                   },
                                   cancellationToken);
    }

    /// <summary>
    /// Reads the sparklines of the building counts of several islands.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="islands">The islands</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="held">The read held for the address, or <see langword="null"/> for none</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    public Task<SparklineRead<IslandKey>> ReadIslandSparklinesAsync(Guid profileId, IReadOnlyList<IslandKey> islands, DateTimeOffset latestTick, SparklineRead<IslandKey>? held, CancellationToken cancellationToken)
    {
        return ReadSparklinesAsync(islands,
                                   latestTick,
                                   held,
                                   0,
                                   async (keys, start, end) =>
                                   {
                                       var query = new HistoryIslandsTrendQuery
                                                   {
                                                       ProfileId = profileId,
                                                       Islands = [.. keys.Select(ToHistoryKey)],
                                                       RangeStart = start,
                                                       RangeEnd = end,
                                                       MaxPoints = TrendRange.SparklineMaxPoints
                                                   };
                                       var trend = await _trends.ReadIslandsTrendAsync(query, cancellationToken).ConfigureAwait(false);

                                       return trend.Points.Select(point => (point.Time, point.IsHourly, (Func<IslandKey, double?>)(island => point.Buildings.TryGetValue(ToHistoryKey(island), out var buildings) ? buildings : null))).ToList();
                                   },
                                   cancellationToken);
    }

    /// <summary>
    /// Gives the start a read asks for: the range start, or for a wide range with a held view the hour of the held view's last point.
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="range">The range</param>
    /// <returns>The start</returns>
    private static DateTimeOffset ReadStart(TrendRequest request, TrendRange range)
    {
        if (TrendRange.ResolutionOf(request.Choice.Range) != HistoryTrendResolution.Hours || request.Held is not { Points.Count: > 0 } held)
        {
            return range.Start;
        }

        var last = held.Points[^1].Time.UtcTicks;
        var hour = new DateTimeOffset(last - (last % TimeSpan.TicksPerHour), TimeSpan.Zero);

        return hour > range.Start ? hour : range.Start;
    }

    /// <summary>
    /// Merges the newly read view into the held one when only the newest hours were read.
    /// </summary>
    /// <param name="view">The view read</param>
    /// <param name="request">The request</param>
    /// <param name="range">The range</param>
    /// <returns>The view to show</returns>
    private static TrendView Merge(TrendView view, TrendRequest request, TrendRange range)
    {
        return TrendRange.ResolutionOf(request.Choice.Range) == HistoryTrendResolution.Hours && request.Held is { Points.Count: > 0 } held
                   ? TrendWindow.Extend(held, view, range)
                   : view;
    }

    /// <summary>
    /// Converts a trend of the history into a view.
    /// </summary>
    /// <typeparam name="TPoint">The type of a point of the history</typeparam>
    /// <param name="trend">The trend</param>
    /// <param name="time">Gives the time of a point</param>
    /// <param name="isHourly">Tells whether a point is hourly</param>
    /// <param name="values">Gives the values of a point in line order</param>
    /// <param name="gameTimes">Gives the game time, the first game time and whether no island of a point holds an entry</param>
    /// <returns>The view</returns>
    private static TrendView Convert<TPoint>(HistoryTrend<TPoint> trend,
                                             Func<TPoint, DateTimeOffset> time,
                                             Func<TPoint, bool> isHourly,
                                             Func<TPoint, double?[]> values,
                                             Func<TPoint, (long? GameTime, long? FirstGameTime, bool HasNoEntries)> gameTimes)
    {
        return new TrendView
               {
                   Points = [
                                .. trend.Points.Select(point => new TrendPoint
                                                                {
                                                                    Time = time(point),
                                                                    IsHourly = isHourly(point),
                                                                    Values = values(point),
                                                                    GameTime = gameTimes(point).GameTime,
                                                                    FirstGameTime = gameTimes(point).FirstGameTime,
                                                                    HasNoEntries = gameTimes(point).HasNoEntries
                                                                })
                            ],
                   TotalCount = trend.TotalCount
               };
    }

    /// <summary>
    /// Gives the read of a view.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The read</returns>
    private static TrendRead ReadOf(TrendView view)
    {
        return new TrendRead
               {
                   View = view,
                   IsFailed = false
               };
    }

    /// <summary>
    /// Gives the read of a chart that could not be read.
    /// </summary>
    /// <returns>The failed read</returns>
    private static TrendRead FailedRead()
    {
        return new TrendRead
               {
                   View = null,
                   IsFailed = true
               };
    }

    /// <summary>
    /// Gives the read of sparklines that could not be read.
    /// </summary>
    /// <typeparam name="TKey">The type of the key of a sparkline</typeparam>
    /// <returns>The failed read</returns>
    private static SparklineRead<TKey> FailedSparklines<TKey>()
        where TKey : notnull
    {
        return new SparklineRead<TKey>
               {
                   Lines = new Dictionary<TKey, SparklineView?>(),
                   Samples = new Dictionary<TKey, IReadOnlyList<SparklineSample>>(),
                   IsFailed = true
               };
    }

    /// <summary>
    /// Gives the held samples of the window a refresh keeps: those of a held read with the asked keys and points.
    /// </summary>
    /// <typeparam name="TKey">The type of the key of a sparkline</typeparam>
    /// <param name="held">The held read, or <see langword="null"/></param>
    /// <param name="keys">The asked keys</param>
    /// <param name="range">The window</param>
    /// <returns>The samples at or after the window start by key, or none when the whole window is to be read</returns>
    private static Dictionary<TKey, List<SparklineSample>> RefreshBase<TKey>(SparklineRead<TKey>? held, List<TKey> keys, TrendRange range)
        where TKey : notnull
    {
        if (held is not { IsFailed: false } || held.Samples.Count == 0 || held.Samples.Keys.ToHashSet().SetEquals(keys) == false || held.Samples.Values.All(samples => samples.Count == 0))
        {
            return [];
        }

        return held.Samples.ToDictionary(pair => pair.Key, pair => pair.Value.Where(sample => sample.Time >= range.Start).ToList());
    }

    /// <summary>
    /// Gives the key of the history of an island.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The key</returns>
    private static HistoryIslandKey ToHistoryKey(IslandKey island)
    {
        return new HistoryIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex);
    }

    /// <summary>
    /// Runs one chart read at a time: a cancelled read rethrows, any other failure is logged without values and reported.
    /// </summary>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="read">The read</param>
    /// <param name="failed">Gives the result of a failed read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result</returns>
    private async Task<TResult> ReadAsync<TResult>(Func<Task<TResult>> read, Func<TResult> failed, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            UiLog.ChartReadFailed(_logger, exception);

            return failed();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads the sparklines of a set of keys over the window of the latest tick, only the points after the held ones when
    /// the held read has the same keys.
    /// </summary>
    /// <typeparam name="TKey">The type of the key of a sparkline</typeparam>
    /// <param name="keys">The keys</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="held">The read held for the address, or <see langword="null"/> for none</param>
    /// <param name="decimals">The decimals the direction compares at</param>
    /// <param name="readPoints">Reads the points of the keys between a start and an end, each with its time, kind and the value of a key</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read, which is failed when the history could not be read</returns>
    private Task<SparklineRead<TKey>> ReadSparklinesAsync<TKey>(IReadOnlyList<TKey> keys,
                                                                DateTimeOffset latestTick,
                                                                SparklineRead<TKey>? held,
                                                                int decimals,
                                                                Func<List<TKey>, DateTimeOffset, DateTimeOffset, Task<List<(DateTimeOffset Time, bool IsHourly, Func<TKey, double?> Value)>>> readPoints,
                                                                CancellationToken cancellationToken)
        where TKey : notnull
    {
        var range = TrendRange.ForSparklines(latestTick);
        var asked = keys.Distinct().ToList();

        return ReadAsync(async () =>
                         {
                             var kept = RefreshBase(held, asked, range);
                             var start = kept.Count > 0
                                             ? new DateTimeOffset(Math.Min(Math.Max(range.Start.UtcTicks, kept.Values.Max(samples => samples.Count > 0 ? samples[^1].Time.UtcTicks : 0L) + 1), range.End.UtcTicks), TimeSpan.Zero)
                                             : range.Start;
                             var points = await readPoints(asked, start, range.End).ConfigureAwait(false);
                             var samples = asked.ToDictionary(key => key,
                                                              key => (IReadOnlyList<SparklineSample>)[
                                                                                                         .. kept.GetValueOrDefault(key, [])
                                                                                                                .Concat(points.Select(point => new SparklineSample(point.Time, point.IsHourly, point.Value(key))))
                                                                                                                .TakeLast(TrendRange.SparklineMaxPoints)
                                                                                                     ]);

                             return new SparklineRead<TKey>
                                    {
                                        Lines = samples.ToDictionary(pair => pair.Key, pair => SparklineBuilder.Build(pair.Value, range, decimals)),
                                        Samples = samples,
                                        IsFailed = false
                                    };
                         },
                         FailedSparklines<TKey>,
                         cancellationToken);
    }

    #endregion // Methods
}