using Censio.Server.Data;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="IHistoryTrends" /> of trends set by the test: it records each query, counts the reads in flight and the
/// most that ran at once, can hold every read or only the row trend reads (goods and islands) until released, can let a
/// held read ignore its cancellation, and can fail every read or one kind of read.
/// </summary>
public sealed class FakeHistoryTrends : IHistoryTrends
{
    #region Fields

    /// <summary>
    /// The guard of the counters and the recorded queries.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The queries of the good reads, in the order they started.
    /// </summary>
    private readonly List<HistoryGoodTrendQuery> _goodQueries = [];

    /// <summary>
    /// The queries of the figures reads, in the order they started.
    /// </summary>
    private readonly List<HistoryFiguresTrendQuery> _figuresQueries = [];

    /// <summary>
    /// The queries of the goods reads, in the order they started.
    /// </summary>
    private readonly List<HistoryGoodsTrendQuery> _goodsQueries = [];

    /// <summary>
    /// The queries of the islands reads, in the order they started.
    /// </summary>
    private readonly List<HistoryIslandsTrendQuery> _islandsQueries = [];

    /// <summary>
    /// Completed when the held reads may go on; replaced while reads are held.
    /// </summary>
    private TaskCompletionSource _release = CreateCompleted();

    /// <summary>
    /// Completed when the held row trend reads (goods and islands) may go on; replaced while they are held.
    /// </summary>
    private TaskCompletionSource _rowTrendRelease = CreateCompleted();

    /// <summary>
    /// The number of reads in flight.
    /// </summary>
    private int _active;

    /// <summary>
    /// The most reads in flight at one time.
    /// </summary>
    private int _maxActive;

    /// <summary>
    /// The number of reads that ended by cancellation.
    /// </summary>
    private int _cancelled;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the trend every good read returns.
    /// </summary>
    public HistoryTrend<HistoryGoodPoint> GoodTrend { get; set; } = new()
                                                                    {
                                                                        Points = [],
                                                                        TotalCount = 0
                                                                    };

    /// <summary>
    /// Gets or sets the trend every figures read returns.
    /// </summary>
    public HistoryTrend<HistoryFiguresPoint> FiguresTrend { get; set; } = new()
                                                                          {
                                                                              Points = [],
                                                                              TotalCount = 0
                                                                          };

    /// <summary>
    /// Gets or sets the trend every goods read returns.
    /// </summary>
    public HistoryTrend<HistoryGoodsPoint> GoodsTrend { get; set; } = new()
                                                                      {
                                                                          Points = [],
                                                                          TotalCount = 0
                                                                      };

    /// <summary>
    /// Gets or sets the trend every islands read returns.
    /// </summary>
    public HistoryTrend<HistoryIslandsPoint> IslandsTrend { get; set; } = new()
                                                                          {
                                                                              Points = [],
                                                                              TotalCount = 0
                                                                          };

    /// <summary>
    /// Gets or sets the exception every read throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Gets or sets the exception the good reads throw in place of <see cref="Failure" />, or <see langword="null" /> to
    /// follow it.
    /// </summary>
    public Exception? GoodFailure { get; set; }

    /// <summary>
    /// Gets or sets the exception the figures reads throw in place of <see cref="Failure" />, or <see langword="null" /> to
    /// follow it.
    /// </summary>
    public Exception? FiguresFailure { get; set; }

    /// <summary>
    /// Gets or sets the exception the goods reads throw in place of <see cref="Failure" />, or <see langword="null" /> to
    /// follow it.
    /// </summary>
    public Exception? GoodsFailure { get; set; }

    /// <summary>
    /// Gets or sets the exception the islands reads throw in place of <see cref="Failure" />, or <see langword="null" /> to
    /// follow it.
    /// </summary>
    public Exception? IslandsFailure { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether a held read ignores its cancellation and returns its trend once released,
    /// like a store read that completes after its caller gave up.
    /// </summary>
    public bool IsIgnoringCancellation { get; set; }

    /// <summary>
    /// Gets the queries of the good reads, in the order they started.
    /// </summary>
    public IReadOnlyList<HistoryGoodTrendQuery> GoodQueries
    {
        get
        {
            lock (_lock)
            {
                return [.. _goodQueries];
            }
        }
    }

    /// <summary>
    /// Gets the queries of the figures reads, in the order they started.
    /// </summary>
    public IReadOnlyList<HistoryFiguresTrendQuery> FiguresQueries
    {
        get
        {
            lock (_lock)
            {
                return [.. _figuresQueries];
            }
        }
    }

    /// <summary>
    /// Gets the queries of the goods reads, in the order they started.
    /// </summary>
    public IReadOnlyList<HistoryGoodsTrendQuery> GoodsQueries
    {
        get
        {
            lock (_lock)
            {
                return [.. _goodsQueries];
            }
        }
    }

    /// <summary>
    /// Gets the queries of the islands reads, in the order they started.
    /// </summary>
    public IReadOnlyList<HistoryIslandsTrendQuery> IslandsQueries
    {
        get
        {
            lock (_lock)
            {
                return [.. _islandsQueries];
            }
        }
    }

    /// <summary>
    /// Gets the number of reads started, good, figures, goods and islands reads together.
    /// </summary>
    public int CallCount
    {
        get
        {
            lock (_lock)
            {
                return _goodQueries.Count + _figuresQueries.Count + _goodsQueries.Count + _islandsQueries.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of row trend reads started, goods and islands reads together.
    /// </summary>
    public int RowTrendCallCount
    {
        get
        {
            lock (_lock)
            {
                return _goodsQueries.Count + _islandsQueries.Count;
            }
        }
    }

    /// <summary>
    /// Gets the most reads that were in flight at one time.
    /// </summary>
    public int MaxConcurrentCalls
    {
        get
        {
            lock (_lock)
            {
                return _maxActive;
            }
        }
    }

    /// <summary>
    /// Gets the number of reads that ended by cancellation.
    /// </summary>
    public int CancelledCount
    {
        get
        {
            lock (_lock)
            {
                return _cancelled;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a good trend of points two minutes apart that end at a time, each with the values of its number.
    /// </summary>
    /// <param name="end">The time of the last point</param>
    /// <param name="count">The number of points</param>
    /// <returns>The trend</returns>
    public static HistoryTrend<HistoryGoodPoint> CreateGoodTrend(DateTimeOffset end, int count)
    {
        return new HistoryTrend<HistoryGoodPoint>
               {
                   Points = [
                                .. Enumerable.Range(0, count).Select(index => new HistoryGoodPoint
                                                                              {
                                                                                  Time = end.AddMinutes(2 * (index - count + 1)),
                                                                                  IsHourly = false,
                                                                                  Generation = index + 1.5,
                                                                                  Consumption = index + 0.5,
                                                                                  Delta = index - 1
                                                                              })
                            ],
                   TotalCount = count
               };
    }

    /// <summary>
    /// Makes every read wait until <see cref="Release" /> is called.
    /// </summary>
    public void Hold()
    {
        lock (_lock)
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Lets the held reads go on, and the later ones run at once.
    /// </summary>
    public void Release()
    {
        TaskCompletionSource release;

        lock (_lock)
        {
            release = _release;
        }

        release.TrySetResult();
    }

    /// <summary>
    /// Makes every row trend read (goods and islands) wait until <see cref="ReleaseRowTrends" /> is called; the good and
    /// figures reads run on.
    /// </summary>
    public void HoldRowTrends()
    {
        lock (_lock)
        {
            _rowTrendRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Lets the held row trend reads go on, and the later ones run at once.
    /// </summary>
    public void ReleaseRowTrends()
    {
        TaskCompletionSource release;

        lock (_lock)
        {
            release = _rowTrendRelease;
        }

        release.TrySetResult();
    }

    /// <summary>
    /// Waits until a number of reads have started.
    /// </summary>
    /// <param name="count">The number of reads</param>
    /// <param name="timeout">The longest to wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the reads have started</returns>
    public Task WaitForCallsAsync(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return WaitForCountAsync(() => CallCount, count, timeout, cancellationToken);
    }

    /// <summary>
    /// Waits until a number of figures reads have started.
    /// </summary>
    /// <param name="count">The number of figures reads</param>
    /// <param name="timeout">The longest to wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the reads have started</returns>
    public Task WaitForFiguresCallsAsync(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return WaitForCountAsync(() => FiguresQueries.Count, count, timeout, cancellationToken);
    }

    /// <summary>
    /// Waits until a number of row trend reads (goods and islands) have started.
    /// </summary>
    /// <param name="count">The number of row trend reads</param>
    /// <param name="timeout">The longest to wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the reads have started</returns>
    public Task WaitForRowTrendCallsAsync(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return WaitForCountAsync(() => RowTrendCallCount, count, timeout, cancellationToken);
    }

    /// <summary>
    /// Waits until a number of reads have ended by cancellation, also when no render follows the cancellation.
    /// </summary>
    /// <param name="count">The number of cancelled reads</param>
    /// <param name="timeout">The longest to wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the reads have been cancelled</returns>
    public Task WaitForCancelledAsync(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return WaitForCountAsync(() => CancelledCount, count, timeout, cancellationToken);
    }

    /// <summary>
    /// Waits until a count of reads reaches a number.
    /// </summary>
    /// <param name="current">Gives the current count</param>
    /// <param name="count">The number of reads</param>
    /// <param name="timeout">The longest to wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the count is reached</returns>
    private static async Task WaitForCountAsync(Func<int> current, int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow() + timeout;

        while (current() < count)
        {
            if (TimeProvider.System.GetUtcNow() > deadline)
            {
                throw new TimeoutException($"Only {current()} of {count} reads counted");
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a task that has completed.
    /// </summary>
    /// <returns>The source of the task</returns>
    private static TaskCompletionSource CreateCompleted()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SetResult();

        return source;
    }

    /// <summary>
    /// Runs one read: counts it, waits while it is held, and throws its failure or the failure of every read.
    /// </summary>
    /// <param name="record">Records the query of the read</param>
    /// <param name="isRowTrend">Whether the read is a row trend read, which <see cref="HoldRowTrends" /> holds too</param>
    /// <param name="readFailure">Gives the failure of this kind of read, or <see langword="null" /> to follow <see cref="Failure" /></param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the read may return its trend</returns>
    private async Task RunAsync(Action record, bool isRowTrend, Func<Exception?> readFailure, CancellationToken cancellationToken)
    {
        TaskCompletionSource release;
        TaskCompletionSource rowTrendRelease;

        lock (_lock)
        {
            record();

            _active++;
            _maxActive = Math.Max(_maxActive, _active);
            release = _release;
            rowTrendRelease = isRowTrend ? _rowTrendRelease : CreateCompleted();
        }

        try
        {
            await Task.WhenAll(release.Task, rowTrendRelease.Task).WaitAsync(IsIgnoringCancellation ? CancellationToken.None : cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                _cancelled++;
            }

            throw;
        }
        finally
        {
            lock (_lock)
            {
                _active--;
            }
        }

        if ((readFailure() ?? Failure) is { } failure)
        {
            throw failure;
        }
    }

    #endregion // Methods

    #region IHistoryTrends

    /// <inheritdoc />
    public async Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken)
    {
        await RunAsync(() => _goodQueries.Add(query), false, () => GoodFailure, cancellationToken).ConfigureAwait(false);

        return GoodTrend;
    }

    /// <inheritdoc />
    public async Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken)
    {
        await RunAsync(() => _figuresQueries.Add(query), false, () => FiguresFailure, cancellationToken).ConfigureAwait(false);

        return FiguresTrend;
    }

    /// <inheritdoc />
    public async Task<HistoryTrend<HistoryGoodsPoint>> ReadGoodsTrendAsync(HistoryGoodsTrendQuery query, CancellationToken cancellationToken)
    {
        await RunAsync(() => _goodsQueries.Add(query), true, () => GoodsFailure, cancellationToken).ConfigureAwait(false);

        return GoodsTrend;
    }

    /// <inheritdoc />
    public async Task<HistoryTrend<HistoryIslandsPoint>> ReadIslandsTrendAsync(HistoryIslandsTrendQuery query, CancellationToken cancellationToken)
    {
        await RunAsync(() => _islandsQueries.Add(query), true, () => IslandsFailure, cancellationToken).ConfigureAwait(false);

        return IslandsTrend;
    }

    #endregion // IHistoryTrends
}