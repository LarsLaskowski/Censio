namespace Censio.Server;

/// <summary>
/// A running server hosted in the Companion's process.
/// </summary>
internal sealed class LocalServerHost : ILocalServer
{
    #region Fields

    /// <summary>
    /// The completion source of <see cref="Stopped" />.
    /// </summary>
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The application.
    /// </summary>
    private readonly WebApplication _app;

    /// <summary>
    /// The parts the host releases after the stop.
    /// </summary>
    private readonly ServerParts _parts;

    /// <summary>
    /// The lock of the stop.
    /// </summary>
    private readonly Lock _stopLock = new();

    /// <summary>
    /// The stop in progress or done, or <c>null</c> before the first stop.
    /// </summary>
    private Task? _stopTask;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalServerHost" /> class.
    /// </summary>
    /// <param name="app">The started application.</param>
    /// <param name="parts">The parts the host releases after the stop.</param>
    /// <param name="port">The port.</param>
    /// <param name="token">The ingest token.</param>
    public LocalServerHost(WebApplication app, ServerParts parts, int port, string token)
    {
        _app = app;
        _parts = parts;
        Port = port;
        Token = token;
        app.Lifetime.ApplicationStopped.Register(() => _stopped.TrySetResult());
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Stops the application and then disposes the retention, the database and the application.
    /// </summary>
    /// <param name="cancellationToken">The token that abandons the wait for the stop.</param>
    /// <returns>A task that completes when everything is released.</returns>
    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _parts.Dispose();
            await _app.DisposeAsync().ConfigureAwait(false);
            _stopped.TrySetResult();
        }
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
        lock (_stopLock)
        {
            return _stopTask ??= StopCoreAsync(cancellationToken);
        }
    }

    #endregion // ILocalServer

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    #endregion // IAsyncDisposable
}