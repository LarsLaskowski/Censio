using Censio.Server;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// An <see cref="ILocalServer" /> with a port and a token, whose stop the test can hold and whose <see cref="Stopped" />
/// the test completes to model a server that stops on its own. A stop request is <see cref="StopAsync" /> or
/// <see cref="DisposeAsync" />, either of which completes <see cref="Stopped" /> once the stop is not held any more.
/// </summary>
public sealed class FakeLocalServer : ILocalServer
{
    #region Fields

    /// <summary>
    /// Completes when the server stopped.
    /// </summary>
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes when the first stop was requested.
    /// </summary>
    private readonly TaskCompletionSource _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes when a held stop may finish.
    /// </summary>
    private readonly TaskCompletionSource _stopRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The lock of the counts.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The number of stop requests.
    /// </summary>
    private int _stopRequests;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeLocalServer" /> class.
    /// </summary>
    /// <param name="port">The port</param>
    /// <param name="token">The token</param>
    /// <param name="isStopHeld">Whether a stop waits until <see cref="ReleaseStop" /> is called</param>
    public FakeLocalServer(int port, string token, bool isStopHeld)
    {
        Port = port;
        Token = token;

        if (isStopHeld == false)
        {
            _stopRelease.SetResult();
        }
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a task that completes when the first stop was requested.
    /// </summary>
    public Task StopRequested => _stopRequested.Task;

    /// <summary>
    /// Gets the number of stop requests, by <see cref="StopAsync" /> or <see cref="DisposeAsync" />.
    /// </summary>
    public int StopRequests
    {
        get
        {
            lock (_lock)
            {
                return _stopRequests;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Ends the server on its own, as a server whose host stopped without being asked.
    /// </summary>
    public void CompleteStopped()
    {
        _stopped.TrySetResult();
    }

    /// <summary>
    /// Lets a held stop finish.
    /// </summary>
    public void ReleaseStop()
    {
        _stopRelease.TrySetResult();
    }

    /// <summary>
    /// Records a stop request and stops once the stop is not held any more.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the wait for a held stop</param>
    /// <returns>A task that completes when the server stopped</returns>
    private async Task RequestStopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _stopRequests++;
        }

        _stopRequested.TrySetResult();

        await _stopRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        _stopped.TrySetResult();
    }

    #endregion // Methods

    #region ILocalServer

    /// <inheritdoc />
    public int Port { get; }

    /// <inheritdoc />
    public string Token { get; }

    /// <inheritdoc />
    public Task Stopped => _stopped.Task;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return RequestStopAsync(cancellationToken);
    }

    #endregion // ILocalServer

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await RequestStopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    #endregion // IAsyncDisposable
}