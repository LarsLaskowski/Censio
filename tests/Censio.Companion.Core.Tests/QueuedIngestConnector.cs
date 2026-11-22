using System.Net.WebSockets;
using System.Threading.Channels;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// An <see cref="IReplayServerConnector" /> that hands out a scripted sequence of sockets, or <c>null</c> for a server that
/// cannot be reached, one per connection attempt, and records the address of every attempt. An attempt for which the test
/// queued nothing yet waits until it does or the attempt is cancelled, as a connector that has not been answered.
/// </summary>
public sealed class QueuedIngestConnector : IReplayServerConnector
{
    #region Fields

    /// <summary>
    /// The sockets the next attempts return, in order.
    /// </summary>
    private readonly Channel<WebSocket?> _sockets = Channel.CreateUnbounded<WebSocket?>();

    /// <summary>
    /// The addresses of the attempts, in order.
    /// </summary>
    private readonly List<Uri> _addresses = [];

    /// <summary>
    /// The tests waiting for a number of attempts.
    /// </summary>
    private readonly List<(int Count, TaskCompletionSource Source)> _waiters = [];

    /// <summary>
    /// The lock of the addresses and the waiters.
    /// </summary>
    private readonly Lock _lock = new();

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the addresses of the attempts so far, in order.
    /// </summary>
    public IReadOnlyList<Uri> Addresses
    {
        get
        {
            lock (_lock)
            {
                return [.. _addresses];
            }
        }
    }

    /// <summary>
    /// Gets the number of attempts so far.
    /// </summary>
    public int AttemptCount
    {
        get
        {
            lock (_lock)
            {
                return _addresses.Count;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Queues the result of the next attempt that has none yet.
    /// </summary>
    /// <param name="socket">The socket the attempt returns, or <c>null</c> for a server that cannot be reached</param>
    public void Enqueue(WebSocket? socket)
    {
        _sockets.Writer.TryWrite(socket);
    }

    /// <summary>
    /// Waits until the given number of attempts was made.
    /// </summary>
    /// <param name="count">The number of attempts</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>A task that completes when the attempts were made</returns>
    public Task WaitForAttemptsAsync(int count, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            if (_addresses.Count >= count)
            {
                source.SetResult();
            }
            else
            {
                _waiters.Add((count, source));
            }
        }

        return source.Task.WaitAsync(cancellationToken);
    }

    #endregion // Methods

    #region IReplayServerConnector

    /// <inheritdoc />
    public async Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _addresses.Add(address);

            foreach (var waiter in _waiters.Where(waiter => waiter.Count <= _addresses.Count).ToList())
            {
                waiter.Source.TrySetResult();
                _waiters.Remove(waiter);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _sockets.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion // IReplayServerConnector
}