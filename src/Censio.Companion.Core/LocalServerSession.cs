using System.Globalization;

using Censio.Server;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Hosts the local server and tracks its status.
/// </summary>
public sealed class LocalServerSession : IAsyncDisposable
{
    #region Fields

    /// <summary>
    /// The setting.
    /// </summary>
    private readonly LocalServerSetting _setting;

    /// <summary>
    /// The delegate that starts the server.
    /// </summary>
    private readonly Func<int, CancellationToken, Task<LocalServerStart>> _start;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The lock of the status and the flags.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The cancellation of the run, requested by a stop.
    /// </summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>
    /// The token that is cancelled by a stop.
    /// </summary>
    private readonly CancellationToken _stopToken;

    /// <summary>
    /// The completion source of the token of the server.
    /// </summary>
    private readonly TaskCompletionSource<string?> _token = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The completion source that completes when the run ended.
    /// </summary>
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The status.
    /// </summary>
    private LocalServerStatus _status;

    /// <summary>
    /// Whether a run began.
    /// </summary>
    private bool _isRunStarted;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalServerSession" /> class.
    /// </summary>
    /// <param name="setting">The setting</param>
    /// <param name="start">The delegate that starts the server on a port</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public LocalServerSession(LocalServerSetting setting, Func<int, CancellationToken, Task<LocalServerStart>> start, TimeProvider timeProvider, ILogger logger)
    {
        _setting = setting;
        _start = start;
        _timeProvider = timeProvider;
        _logger = logger;
        _stopToken = _stop.Token;
        _status = new LocalServerStatus
                  {
                      State = LocalServerState.Starting,
                      Port = setting.Port
                  };
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<LocalServerStatus>? StatusChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the status.
    /// </summary>
    public LocalServerStatus Status
    {
        get
        {
            lock (_lock)
            {
                return _status;
            }
        }
    }

    /// <summary>
    /// Gets the address of the server.
    /// </summary>
    public Uri Address => new(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{_setting.Port}/"));

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Determines whether the Companion hosts a local server.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="setting">The setting</param>
    /// <returns><see langword="true" /> when a server is hosted</returns>
    public static bool IsHosted(CompanionMode mode, LocalServerSetting setting)
    {
        return setting.Kind is LocalServerSettingKind.Local or LocalServerSettingKind.Invalid && mode != CompanionMode.Invalid;
    }

    /// <summary>
    /// Starts the server and runs until it ends.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>A task that completes when the run ends</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _isRunStarted = true;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopToken);

            await RunCoreAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _token.TrySetResult(null);
            _ended.TrySetResult();
        }
    }

    /// <summary>
    /// Waits for the token of the running server.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the wait</param>
    /// <returns>The token, or <see langword="null" /> when the server does not run</returns>
    public Task<string?> WaitForTokenAsync(CancellationToken cancellationToken)
    {
        return _token.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Waits for the token of the running server for live sending.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the wait</param>
    /// <returns>The token, or <see langword="null" /> when the server does not run</returns>
    public async Task<string?> WaitForLiveTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _token.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        return _ended.Task.IsCompleted ? null : token;
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <param name="timeout">The longest time to wait</param>
    /// <returns>A task that completes when the server has stopped or the time is over</returns>
    public async Task StopAsync(TimeSpan timeout)
    {
        bool isRunStarted;

        lock (_lock)
        {
            isRunStarted = _isRunStarted;
        }

        using var delayCancellation = new CancellationTokenSource();

        var delay = Task.Delay(timeout, _timeProvider, delayCancellation.Token);

        await _stop.CancelAsync().ConfigureAwait(false);

        if (isRunStarted)
        {
            await Task.WhenAny(_ended.Task, delay).ConfigureAwait(false);
        }

        await delayCancellation.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the server of a start that finishes after the run was cancelled.
    /// </summary>
    /// <param name="startTask">The task of the start</param>
    /// <returns>A task that completes when the server has stopped</returns>
    private static async Task StopLateServerAsync(Task<LocalServerStart> startTask)
    {
        await Task.WhenAny(startTask).ConfigureAwait(false);

        if (startTask.IsCompletedSuccessfully)
        {
            var start = await startTask.ConfigureAwait(false);

            if (start.Server is not null)
            {
                await start.Server.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Starts the server, waits until it ends or the run is cancelled and stops it then.
    /// </summary>
    /// <param name="runToken">The token that ends the run</param>
    /// <returns>A task that completes when the run ended</returns>
    private async Task RunCoreAsync(CancellationToken runToken)
    {
        if (_setting.Kind != LocalServerSettingKind.Local)
        {
            if (_setting.Kind == LocalServerSettingKind.Invalid)
            {
                CompanionLog.ServerSettingsInvalid(_logger, _setting.Problem ?? string.Empty);
                SetStatus(LocalServerState.SettingsInvalid);
            }

            return;
        }

        if (runToken.IsCancellationRequested)
        {
            return;
        }

        var startTask = StartAsync(runToken);
        LocalServerStart start;

        try
        {
            start = await startTask.WaitAsync(runToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            await StopLateServerAsync(startTask).ConfigureAwait(false);

            return;
        }
        catch (Exception exception)
        {
            CompanionLog.LocalServerFailed(_logger, exception);
            SetStatus(LocalServerState.Failed);

            return;
        }

        if (start.Kind != LocalServerStartKind.Running || start.Server is null)
        {
            ReportFailedStart(start);

            return;
        }

        await RunServerAsync(start.Server, runToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls the start delegate, turning a synchronous throw into a faulted task.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the start</param>
    /// <returns>The task of the start</returns>
    private Task<LocalServerStart> StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            return _start(_setting.Port, cancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException<LocalServerStart>(exception);
        }
    }

    /// <summary>
    /// Shows and logs a start that did not run a server.
    /// </summary>
    /// <param name="start">The result of the start</param>
    private void ReportFailedStart(LocalServerStart start)
    {
        if (start.Kind == LocalServerStartKind.PortInUse)
        {
            CompanionLog.LocalServerPortInUse(_logger, _setting.Port);
            SetStatus(LocalServerState.PortInUse);

            return;
        }

        if (start.Exception is not null)
        {
            CompanionLog.LocalServerFailed(_logger, start.Exception);
        }

        SetStatus(LocalServerState.Failed);
    }

    /// <summary>
    /// Shows the running server and waits until it stops on its own or the run is cancelled, which stops it.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="runToken">The token that ends the run</param>
    /// <returns>A task that completes when the server has stopped</returns>
    private async Task RunServerAsync(ILocalServer server, CancellationToken runToken)
    {
        if (runToken.IsCancellationRequested)
        {
            await server.StopAsync(CancellationToken.None).ConfigureAwait(false);

            return;
        }

        CompanionLog.LocalServerRunning(_logger, _setting.Port);
        _token.TrySetResult(server.Token);
        SetStatus(LocalServerState.Running);

        try
        {
            await server.Stopped.WaitAsync(runToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            await server.StopAsync(CancellationToken.None).ConfigureAwait(false);

            return;
        }

        CompanionLog.LocalServerStoppedUnexpectedly(_logger);
        SetStatus(LocalServerState.Failed);
    }

    /// <summary>
    /// Sets the state of the status and raises <see cref="StatusChanged" />.
    /// </summary>
    /// <param name="state">The state</param>
    private void SetStatus(LocalServerState state)
    {
        LocalServerStatus status;

        lock (_lock)
        {
            status = _status with
                             {
                                 State = state
                             };
            _status = status;
        }

        StatusChanged?.Invoke(this, status);
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _stop.Dispose();

        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}