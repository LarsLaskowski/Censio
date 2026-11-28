using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Folds old history, clears deleted data and runs the deletion of a profile's history.
/// </summary>
public sealed partial class HistoryRetention : IDisposable
{
    #region Fields

    /// <summary>
    /// The time between the end of a run and the start of the next one
    /// </summary>
    public static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// The state
    /// </summary>
    private readonly ServerState _state;

    /// <summary>
    /// The history store
    /// </summary>
    private readonly HistoryStore _store;

    /// <summary>
    /// The time provider
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The source that is completed when a deletion asks for a run; replaced at the start of each run.
    /// </summary>
    private TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The source of the cancellation that ends the loop, set by <see cref="Start"/>.
    /// </summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>
    /// The loop, set by <see cref="Start"/>.
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// Whether a deletion asked for its rows to be swept, 1 or 0.
    /// </summary>
    private int _sweepPending;

    /// <summary>
    /// Whether the clean-up of the database file is still to be done, 1 or 0.
    /// </summary>
    private int _cleanUpRequested;

    /// <summary>
    /// Whether the job was disposed, 1 or 0.
    /// </summary>
    private int _disposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryRetention"/> class.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="store">The history store</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public HistoryRetention(ServerState state, HistoryStore store, TimeProvider timeProvider, ILogger logger)
    {
        _state = state;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Starts the retention loop: a run at once, then one an hour after each run ends and one whenever a deletion asks.
    /// </summary>
    /// <param name="stoppingToken">The token that ends the loop</param>
    public void Start(CancellationToken stoppingToken)
    {
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var token = _cancellation.Token;

        _loop = Task.Run(() => LoopAsync(token), CancellationToken.None);
    }

    /// <summary>
    /// Runs one retention run: sweeps deleted data, folds old ticks and clears the log.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The task</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _sweepPending, 0);

        if (await SweepAsync(cancellationToken).ConfigureAwait(false) == false)
        {
            return;
        }

        var clearFailed = await ClearRequestedAsync(cancellationToken).ConfigureAwait(false) == false;
        var now = _timeProvider.GetUtcNow();
        var hours = new HashSet<(Guid ProfileId, DateTimeOffset Hour)>();

        cancellationToken.ThrowIfCancellationRequested();

        var (ticks, completed) = await FoldAsync(now, hours, cancellationToken).ConfigureAwait(false);

        if (ticks > 0)
        {
            LogReduced(_logger, hours.Count, ticks);
        }

        if (completed == false)
        {
            return;
        }

        if (clearFailed == false)
        {
            await ClearRequestedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes the history of a profile and asks for a run that sweeps and clears it.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The outcome</returns>
    public async Task<HistoryOutcome> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var result = await _state.DeleteProfileAsync(profileId, cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case HistoryOutcome.Done:
                {
                    LogDeleted(_logger);
                    Volatile.Write(ref _sweepPending, 1);
                    Volatile.Write(ref _cleanUpRequested, 1);
                    Volatile.Read(ref _asked).TrySetResult();
                }
                break;

            case HistoryOutcome.Failed:
                {
                    LogDeleteFailed(_logger, result.Failure!);
                }
                break;
        }

        return result.Outcome;
    }

    // Skeleton: removed with the implementation
#pragma warning disable S1172, S2325, RH2003
    /// <summary>
    /// Merges the history of a profile into another profile's.
    /// </summary>
    /// <param name="profileId">The identity of the profile whose history is moved (the source)</param>
    /// <param name="targetProfileId">The identity of the profile that receives the history</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The outcome</returns>
    public Task<HistoryOutcome> MergeProfileAsync(Guid profileId, Guid targetProfileId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    /// <summary>
    /// Logs the result of a run that folded ticks.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="hours">The number of hours</param>
    /// <param name="ticks">The number of ticks</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "History reduced to hourly averages: {Hours} hours from {Ticks} ticks.")]
    private static partial void LogReduced(ILogger logger, int hours, int ticks);

    /// <summary>
    /// Logs a failed fold step.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The history could not be reduced; the next run tries again.")]
    private static partial void LogReduceFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Logs a deletion whose rows are gone.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The history of a profile was deleted.")]
    private static partial void LogDeleted(ILogger logger);

    /// <summary>
    /// Logs a failed deletion.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The history of a profile could not be deleted.")]
    private static partial void LogDeleteFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Logs a finished clean-up.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The deleted history was cleared from the database file.")]
    private static partial void LogCleared(ILogger logger);

    /// <summary>
    /// Logs a clean-up that could not finish.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure, or <see langword="null"/> when a read blocks the clean-up</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The deleted history could not yet be cleared from the database file; the next retention run tries again.")]
    private static partial void LogClearIncomplete(ILogger logger, Exception? exception);

    /// <summary>
    /// Runs runs until the token is cancelled.
    /// </summary>
    /// <param name="token">The token that ends the loop</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            while (token.IsCancellationRequested == false)
            {
                var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                Volatile.Write(ref _asked, asked);

                await RunAsync(token).ConfigureAwait(false);

                using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);

                await Task.WhenAny(Task.Delay(RunInterval, _timeProvider, waiting.Token), asked.Task).ConfigureAwait(false);
                await waiting.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The stop ends the loop; the step it cut off rolled back and nothing is logged
        }
    }

    /// <summary>
    /// Folds the old ticks of every profile, step by step, sweeping a deletion asked meanwhile before the next step.
    /// </summary>
    /// <param name="now">The current time</param>
    /// <param name="hours">The hours folded into, to be filled</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The number of folded ticks, and whether the folding ended without a failed step</returns>
    private async Task<(int Ticks, bool Completed)> FoldAsync(DateTimeOffset now, HashSet<(Guid ProfileId, DateTimeOffset Hour)> hours, CancellationToken cancellationToken)
    {
        var ticks = 0;

        foreach (var profileId in _state.Profiles.Select(profile => profile.Id).ToList())
        {
            var result = new HistoryResult(HistoryOutcome.Done, null, null);

            while (result.Outcome == HistoryOutcome.Done)
            {
                if (Interlocked.Exchange(ref _sweepPending, 0) == 1 && await SweepAsync(cancellationToken).ConfigureAwait(false) == false)
                {
                    return (ticks, false);
                }

                result = await _store.FoldNextTickAsync(profileId, now, cancellationToken).ConfigureAwait(false);

                if (result.Outcome == HistoryOutcome.Done)
                {
                    hours.Add((profileId, result.Hour!.Value));
                    ticks++;
                }
            }

            if (result.Outcome == HistoryOutcome.Failed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogReduceFailed(_logger, result.Failure!);

                return (ticks, false);
            }
        }

        return (ticks, true);
    }

    /// <summary>
    /// Runs sweep steps until nothing is left to sweep.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns><see langword="true"/> when nothing is left, <see langword="false"/> when a step failed</returns>
    private async Task<bool> SweepAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await _store.SweepNextAsync(cancellationToken).ConfigureAwait(false);

            switch (result.Outcome)
            {
                case HistoryOutcome.Done:
                    {
                        Volatile.Write(ref _cleanUpRequested, 1);
                    }
                    break;

                case HistoryOutcome.Failed:
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        LogClearIncomplete(_logger, result.Failure);

                        return false;
                    }
                default:
                    {
                        return true;
                    }
            }
        }
    }

    /// <summary>
    /// Clears the deleted history from the database file when a clean-up is requested, and keeps the request when that cannot finish.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns><see langword="false"/> when the attempt did not finish, otherwise <see langword="true"/></returns>
    private async Task<bool> ClearRequestedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _cleanUpRequested, 0) == 0)
        {
            return true;
        }

        var result = await _store.ClearDeletedAsync(cancellationToken).ConfigureAwait(false);

        if (result.Outcome == HistoryOutcome.Done)
        {
            LogCleared(_logger);

            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (result.Outcome == HistoryOutcome.NothingToDo)
        {
            // A deletion left rows that no sweep has removed yet: keep the request, log nothing and ask for a run that sweeps
            Volatile.Write(ref _cleanUpRequested, 1);
            Volatile.Read(ref _asked).TrySetResult();

            return false;
        }

        Volatile.Write(ref _cleanUpRequested, 1);
        LogClearIncomplete(_logger, result.Failure);

        return false;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _cancellation?.Cancel();
        _loop?.Wait(CancellationToken.None);
        _cancellation?.Dispose();
    }

    #endregion // IDisposable
}