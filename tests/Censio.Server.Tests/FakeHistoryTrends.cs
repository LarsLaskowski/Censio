using Censio.Server.Data;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="IHistoryTrends" /> of trends set by the test: it records each query, counts the reads in flight and the
/// most that ran at once, can hold every read until released, and can fail every read.
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
    /// Completed when the held reads may go on; replaced while reads are held.
    /// </summary>
    private TaskCompletionSource _release = CreateCompleted();

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
    /// Gets or sets the exception every read throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? Failure { get; set; }

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
    /// Gets the number of reads started, good and figures reads together.
    /// </summary>
    public int CallCount
    {
        get
        {
            lock (_lock)
            {
                return _goodQueries.Count + _figuresQueries.Count;
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
    /// Waits until a number of reads have started.
    /// </summary>
    /// <param name="count">The number of reads</param>
    /// <param name="timeout">The longest to wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the reads have started</returns>
    public async Task WaitForCallsAsync(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow() + timeout;

        while (CallCount < count)
        {
            if (TimeProvider.System.GetUtcNow() > deadline)
            {
                throw new TimeoutException($"Only {CallCount} of {count} reads started");
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
    /// Runs one read: counts it, waits while it is held, and throws the failure.
    /// </summary>
    /// <param name="record">Records the query of the read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the read may return its trend</returns>
    private async Task RunAsync(Action record, CancellationToken cancellationToken)
    {
        TaskCompletionSource release;

        lock (_lock)
        {
            record();

            _active++;
            _maxActive = Math.Max(_maxActive, _active);
            release = _release;
        }

        try
        {
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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

        if (Failure is { } failure)
        {
            throw failure;
        }
    }

    #endregion // Methods

    #region IHistoryTrends

    /// <inheritdoc />
    public async Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken)
    {
        await RunAsync(() => _goodQueries.Add(query), cancellationToken).ConfigureAwait(false);

        return GoodTrend;
    }

    /// <inheritdoc />
    public async Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken)
    {
        await RunAsync(() => _figuresQueries.Add(query), cancellationToken).ConfigureAwait(false);

        return FiguresTrend;
    }

    #endregion // IHistoryTrends
}