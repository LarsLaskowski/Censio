namespace Censio.Server;

/// <summary>
/// Loads the sparklines of a page off its load loop and holds the read that belongs to the page's address.
/// </summary>
/// <typeparam name="TKey">The type of the key of a sparkline</typeparam>
public sealed class SparklineLoader<TKey> : IDisposable
    where TKey : notnull
{
    #region Fields

    /// <summary>
    /// The lock that guards the state of the loader.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// The address the held read, the failure and the running read belong to.
    /// </summary>
    private object? _address;

    /// <summary>
    /// The held read of the address.
    /// </summary>
    private SparklineRead<TKey>? _held;

    /// <summary>
    /// The as-of time the held read was made for.
    /// </summary>
    private DateTimeOffset? _heldAsOf;

    /// <summary>
    /// The as-of time of the read in flight.
    /// </summary>
    private DateTimeOffset? _inFlightAsOf;

    /// <summary>
    /// Whether the read of the address failed.
    /// </summary>
    private bool _isFailed;

    /// <summary>
    /// The request that waits for the read in flight, the newest one.
    /// </summary>
    private (DateTimeOffset AsOf, Func<SparklineRead<TKey>?, CancellationToken, Task<SparklineRead<TKey>>> Read, Func<Task> Changed)? _pending;

    /// <summary>
    /// The source of the cancellation of the reads of the address.
    /// </summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>
    /// The number of the current run, which changes with the address.
    /// </summary>
    private int _runId;

    /// <summary>
    /// Whether a run is going.
    /// </summary>
    private bool _isRunning;

    /// <summary>
    /// Whether the loader is disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Requests the read for an address, replacing a pending one.
    /// </summary>
    /// <param name="addressKey">The key of the address the read belongs to</param>
    /// <param name="asOf">The time of the latest tick the read is made for</param>
    /// <param name="read">Reads the sparklines, given the held read and a cancellation token</param>
    /// <param name="changed">Called when the held read or the failure changed</param>
    public void Request(object addressKey, DateTimeOffset asOf, Func<SparklineRead<TKey>?, CancellationToken, Task<SparklineRead<TKey>>> read, Func<Task> changed)
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (Equals(_address, addressKey))
            {
                if (_isFailed || _heldAsOf == asOf || _inFlightAsOf == asOf || _pending?.AsOf == asOf)
                {
                    return;
                }

                _pending = (asOf, read, changed);

                if (_isRunning)
                {
                    return;
                }
            }
            else
            {
                _cancellation?.Cancel();
                _cancellation?.Dispose();
                _cancellation = new CancellationTokenSource();
                _address = addressKey;
                _held = null;
                _heldAsOf = null;
                _inFlightAsOf = null;
                _isFailed = false;
                _pending = (asOf, read, changed);
                _runId++;
            }

            _isRunning = true;

            var runId = _runId;
            var token = _cancellation!.Token;

            _ = Task.Run(() => RunAsync(runId, token), CancellationToken.None);
        }
    }

    /// <summary>
    /// Gives the held read when it belongs to an address.
    /// </summary>
    /// <param name="addressKey">The key of the address</param>
    /// <returns>The held read, or <see langword="null"/> when none belongs to the address</returns>
    public SparklineRead<TKey>? HeldFor(object addressKey)
    {
        lock (_lock)
        {
            return Equals(_address, addressKey)
                       ? _held
                       : null;
        }
    }

    /// <summary>
    /// Tells whether the read of an address failed.
    /// </summary>
    /// <param name="addressKey">The key of the address</param>
    /// <returns><see langword="true"/> when the read of the address failed</returns>
    public bool IsFailedFor(object addressKey)
    {
        lock (_lock)
        {
            return Equals(_address, addressKey) && _isFailed;
        }
    }

    /// <summary>
    /// Runs a read, whatever it throws.
    /// </summary>
    /// <param name="read">The read</param>
    /// <param name="held">The held read</param>
    /// <param name="token">The cancellation token</param>
    /// <returns>The answer, none for a cancelled read, and whether the address failed</returns>
    private static async Task<(SparklineRead<TKey>? Answer, bool IsFailed)> ReadAsync(Func<SparklineRead<TKey>?, CancellationToken, Task<SparklineRead<TKey>>> read, SparklineRead<TKey>? held, CancellationToken token)
    {
        try
        {
            var answer = await read(held, token).ConfigureAwait(false);

            return (answer, answer.IsFailed);
        }
        catch (OperationCanceledException)
        {
            return (null, false);
        }
        catch (Exception)
        {
            return (null, true);
        }
    }

    /// <summary>
    /// Runs the pending requests of one address, the newest first, until none is left.
    /// </summary>
    /// <param name="runId">The number of the run</param>
    /// <param name="token">The cancellation token of the reads of the address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task RunAsync(int runId, CancellationToken token)
    {
        while (true)
        {
            Func<SparklineRead<TKey>?, CancellationToken, Task<SparklineRead<TKey>>> read;
            Func<Task> changed;
            DateTimeOffset asOf;
            SparklineRead<TKey>? held;

            lock (_lock)
            {
                if (IsCurrent(runId) == false)
                {
                    return;
                }

                if (_pending is not { } request)
                {
                    _isRunning = false;

                    return;
                }

                (asOf, read, changed) = request;
                held = _held;
                _pending = null;
                _inFlightAsOf = asOf;
            }

            var (answer, isFailed) = await ReadAsync(read, held, token).ConfigureAwait(false);

            lock (_lock)
            {
                if (IsCurrent(runId) == false)
                {
                    return;
                }

                _inFlightAsOf = null;

                if (isFailed)
                {
                    _held = null;
                    _heldAsOf = null;
                    _pending = null;
                    _isFailed = true;
                }
                else if (answer is not null)
                {
                    _held = answer;
                    _heldAsOf = asOf;
                }
            }

            if (isFailed == false && answer is null)
            {
                continue;
            }

            if (await NotifyAsync(changed, runId).ConfigureAwait(false) == false)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Tells the page that the held read or the failure changed, unless the run is over.
    /// </summary>
    /// <param name="changed">Called when the held read or the failure changed</param>
    /// <param name="runId">The number of the run</param>
    /// <returns><see langword="true"/> when the run goes on</returns>
    private async Task<bool> NotifyAsync(Func<Task> changed, int runId)
    {
        lock (_lock)
        {
            if (IsCurrent(runId) == false)
            {
                return false;
            }
        }

        try
        {
            await changed().ConfigureAwait(false);

            return true;
        }
        catch (Exception)
        {
            lock (_lock)
            {
                if (IsCurrent(runId))
                {
                    _isRunning = false;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Tells whether a run is the current one of a loader that is not disposed. The caller holds the lock.
    /// </summary>
    /// <param name="runId">The number of the run</param>
    /// <returns><see langword="true"/> when the run may store its answer</returns>
    private bool IsCurrent(int runId)
    {
        return _isDisposed == false && runId == _runId;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _pending = null;
            _cancellation?.Cancel();
            _cancellation?.Dispose();
        }
    }

    #endregion // IDisposable
}