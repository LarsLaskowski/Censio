using System.Globalization;

using Censio.Contracts;
using Censio.Pipe;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Sends the live data of the pipe connection to a server and reconnects when the connection fails.
/// </summary>
public sealed class LiveSender : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The name of the profile live data goes to.
    /// </summary>
    public const string ProfileName = "Live";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The server the sender sends to.
    /// </summary>
    private readonly LiveSendTarget _target;

#pragma warning disable S4487
    // Skeleton: removed with the implementation
    /// <summary>
    /// The buffer of the waiting messages.
    /// </summary>
    private readonly LiveBuffer _buffer;
#pragma warning restore S4487

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private readonly string _companionVersion;

    /// <summary>
    /// The connector.
    /// </summary>
    private readonly IReplayServerConnector _connector;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The lock of the queue, the status and the connection.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The waiting messages, the oldest first; the first one is kept until it is acknowledged.
    /// </summary>
    private readonly List<(Guid Id, ReadOnlyMemory<byte> Utf8Json)> _queue = [];

    /// <summary>
    /// The signal that tells the send loop that a message was queued.
    /// </summary>
    private readonly SemaphoreSlim _queued = new(0);

    /// <summary>
    /// The cancellation of the run, requested by the dispose.
    /// </summary>
    private readonly CancellationTokenSource _dispose = new();

    /// <summary>
    /// The size of the waiting messages in bytes.
    /// </summary>
    private long _queuedBytes;

    /// <summary>
    /// Whether the first waiting message is being sent.
    /// </summary>
    private bool _isSending;

    /// <summary>
    /// The number of dropped messages that no log line holds yet.
    /// </summary>
    private int _unloggedDropped;

    /// <summary>
    /// The state of the pipe connection last passed on, or <see langword="null" /> when none was.
    /// </summary>
    private PipeConnectionState? _state;

    /// <summary>
    /// The delegate that passes a state on to the accepted connection, or <see langword="null" /> when there is none.
    /// </summary>
    private Action<PipeConnectionState>? _notify;

    /// <summary>
    /// The state of the status.
    /// </summary>
    private LiveSendState _statusState = LiveSendState.Connecting;

    /// <summary>
    /// The problem of the status.
    /// </summary>
    private ReplaySendProblemKind? _statusProblem;

    /// <summary>
    /// The task of the run, or <see langword="null" /> when none began.
    /// </summary>
    private Task? _run;

    /// <summary>
    /// Whether the sender is disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSender" /> class.
    /// </summary>
    /// <param name="target">The server the sender sends to</param>
    /// <param name="buffer">The buffer of the waiting messages</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    private LiveSender(LiveSendTarget target, LiveBuffer buffer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        _target = target;
        _buffer = buffer;
        _companionVersion = companionVersion;
        _connector = connector;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<LiveSendStatus>? StatusChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the identity of the profile live data goes to.
    /// </summary>
    public static Guid ProfileId { get; } = new("b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94");

    /// <summary>
    /// Gets the delays before the attempts to reconnect.
    /// </summary>
    public static IReadOnlyList<TimeSpan> RetryDelays { get; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30)];

    /// <summary>
    /// Gets the time a connection must stay accepted to reset the delays.
    /// </summary>
    public static TimeSpan SuccessfulConnectionTime { get; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the interval of the keep-alive pings.
    /// </summary>
    public static TimeSpan KeepAliveInterval { get; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets the time the connection waits for the answer to a ping.
    /// </summary>
    public static TimeSpan KeepAliveTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public LiveSendStatus Status
    {
        get
        {
            lock (_lock)
            {
                return new LiveSendStatus
                       {
                           State = _statusState,
                           Problem = _statusProblem
                       };
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Determines whether the Companion sends live data.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="setting">The setting</param>
    /// <returns><see langword="true" /> when live data is sent</returns>
    public static bool IsSending(CompanionMode mode, LocalServerSetting setting)
    {
        return mode is CompanionMode.Normal or CompanionMode.Record && setting.Kind is LocalServerSettingKind.Forward or LocalServerSettingKind.Local;
    }

    /// <summary>
    /// Creates a sender that sends to the server of the forward setting.
    /// </summary>
    /// <param name="setting">The forward setting</param>
    /// <param name="buffer">The buffer of the waiting messages</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <returns>The sender</returns>
    public static LiveSender ForServer(ForwardServerSetting setting, LiveBuffer buffer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        return new LiveSender(new LiveSendTarget
                              {
                                  Address = setting.Address,
                                  ReadToken = _ => Task.FromResult(setting.Token),
                                  IsLocal = false
                              },
                              buffer,
                              companionVersion,
                              connector,
                              timeProvider,
                              logger);
    }

    /// <summary>
    /// Creates a sender that sends to the local server.
    /// </summary>
    /// <param name="port">The port of the local server</param>
    /// <param name="waitForToken">The delegate that waits for the token of the local server</param>
    /// <param name="buffer">The buffer of the waiting messages</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <returns>The sender</returns>
    public static LiveSender ForLocalServer(int port, Func<CancellationToken, Task<string?>> waitForToken, LiveBuffer buffer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        return new LiveSender(new LiveSendTarget
                              {
                                  Address = new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{port}/ingest")),
                                  ReadToken = waitForToken,
                                  IsLocal = true
                              },
                              buffer,
                              companionVersion,
                              connector,
                              timeProvider,
                              logger);
    }

    /// <summary>
    /// Queues an item of the pipe connection for sending; it never waits.
    /// </summary>
    /// <param name="item">The item</param>
    public void Enqueue(PipeConnectionItem item)
    {
        if (Convert(item) is not var (id, message))
        {
            return;
        }

        var written = ContractWriter.Write(message);

        if (written.Error is not null)
        {
            CompanionLog.LiveMessageNotWritten(_logger);

            return;
        }

        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _queue.Add((id, written.Utf8Json));
            _queuedBytes += written.Utf8Json.Length;

            DropOldest();

            if (_queued.CurrentCount == 0)
            {
                _queued.Release();
            }
        }
    }

    /// <summary>
    /// Passes a change of the pipe connection state on to the server.
    /// </summary>
    /// <param name="state">The state</param>
    public void NotifyState(PipeConnectionState state)
    {
        lock (_lock)
        {
            _state = state;
            _notify?.Invoke(state);
        }
    }

    /// <summary>
    /// Connects, sends and reconnects until the token is cancelled or the server refuses for good.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>A task that completes when the run ends</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        var run = RunCoreAsync(cancellationToken);

        lock (_lock)
        {
            _run = run;
        }

        return run;
    }

    /// <summary>
    /// Converts an item of the pipe connection into a message.
    /// </summary>
    /// <param name="item">The item</param>
    /// <returns>The message, or <see langword="null" /> when the item has none</returns>
    private static (Guid Id, ContractMessage Message)? Convert(PipeConnectionItem item)
    {
        var result = item.Result;

        switch (result.Kind)
        {
            case PipeTickResultKind.Tick when result.Tick is { Islands.Count: > 0 } tick:
                {
                    var message = ContractMessageConverter.ToTick(tick, ProfileId);

                    return (message.Id, message);
                }

            case PipeTickResultKind.TradeRoutes when result.TradeRoutes is { } routes:
                {
                    var message = ContractMessageConverter.ToTradeRoutes(routes, ProfileId, item.ReadStamp);

                    return (message.Id, message);
                }

            case PipeTickResultKind.SessionStart:
                {
                    var message = ContractMessageConverter.ToSessionStart(ProfileId, item.ReadStamp);

                    return (message.Id, message);
                }

            case PipeTickResultKind.SessionEnd:
                {
                    var message = ContractMessageConverter.ToSessionEnd(ProfileId, item.ReadStamp);

                    return (message.Id, message);
                }

            default:
                {
                    return null;
                }
        }
    }

    /// <summary>
    /// Gives the reason of a problem as the tray shows it.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The reason</returns>
    private static string Reason(ReplaySendProblemKind problem)
    {
        return TrayMenu.FormatReplaySendReason(problem) ?? problem.ToString();
    }

    /// <summary>
    /// Removes the oldest waiting messages that are not being sent while the bounds are exceeded; the newest stays.
    /// The caller holds the lock.
    /// </summary>
    private void DropOldest()
    {
        var first = _isSending ? 1 : 0;

        while ((_queue.Count > LiveBuffer.MaxMemoryMessages || _queuedBytes > LiveBuffer.MaxMemoryBytes) && _queue.Count - first >= 2)
        {
            _queuedBytes -= _queue[first].Utf8Json.Length;
            _queue.RemoveAt(first);
            _unloggedDropped++;
        }
    }

    /// <summary>
    /// Logs the number of dropped messages that no log line holds yet.
    /// </summary>
    private void LogDropped()
    {
        int count;

        lock (_lock)
        {
            count = _unloggedDropped;
            _unloggedDropped = 0;
        }

        if (count > 0)
        {
            CompanionLog.LiveMessagesDropped(_logger, count);
        }
    }

    /// <summary>
    /// Sets the status and raises <see cref="StatusChanged" />.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="problem">The problem</param>
    private void SetStatus(LiveSendState state, ReplaySendProblemKind? problem)
    {
        var status = new LiveSendStatus
                     {
                         State = state,
                         Problem = problem
                     };

        lock (_lock)
        {
            _statusState = state;
            _statusProblem = problem;
        }

        var handlers = StatusChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<LiveSendStatus>>())
        {
            try
            {
                handler(this, status);
            }
            catch (Exception)
            {
                // A failing subscriber must not stop live sending
            }
        }
    }

    /// <summary>
    /// Runs the attempts until the token is cancelled or the server refuses for good.
    /// </summary>
    /// <param name="cancellationToken">The token of the caller</param>
    /// <returns>A task that completes when the run ends</returns>
    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _dispose.Token);

        try
        {
            await RetryAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            // The sender is disposed.
        }
        finally
        {
            LogDropped();
        }
    }

    /// <summary>
    /// Attempts a connection again and again with the delays of <see cref="RetryDelays" />.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>A task that completes when the server refuses for good</returns>
    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        var attempts = 0;
        ReplaySendProblemKind? lastLogged = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await AttemptAsync(cancellationToken).ConfigureAwait(false);
            var problem = outcome.Problem;

            if (outcome.IsAccepted)
            {
                lastLogged = null;
            }

            if (outcome.Duration >= SuccessfulConnectionTime)
            {
                attempts = 0;
            }

            if (problem is ReplaySendProblemKind.TokenRefused or ReplaySendProblemKind.VersionNotSupported)
            {
                SetStatus(LiveSendState.Stopped, problem);
                CompanionLog.LiveSendRefused(_logger, Reason(problem.Value));
                LogDropped();

                return;
            }

            if (problem is ReplaySendProblemKind.LocalServerNotRunning)
            {
                SetStatus(LiveSendState.Stopped, problem);
                LogDropped();

                return;
            }

            SetStatus(LiveSendState.Retrying, problem);

            if (problem != lastLogged)
            {
                CompanionLog.LiveSendFailed(_logger, Reason(problem!.Value));
                lastLogged = problem;
            }

            var delay = RetryDelays[Math.Min(attempts, RetryDelays.Count - 1)];

            attempts++;

            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Makes one connection: reads the token, connects and sends until the connection fails.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The outcome</returns>
    private async Task<(ReplaySendProblemKind? Problem, bool IsAccepted, TimeSpan Duration)> AttemptAsync(CancellationToken cancellationToken)
    {
        IngestClient? client = null;
        var accepted = default(DateTimeOffset?);

        try
        {
            string? token = null;

            if (_target.ReadToken is not null)
            {
                token = await _target.ReadToken(cancellationToken).ConfigureAwait(false);

                if (token is null && _target.IsLocal)
                {
                    return (ReplaySendProblemKind.LocalServerNotRunning, false, TimeSpan.Zero);
                }
            }

            client = new IngestClient(_connector, _timeProvider);

            var failed = new TaskCompletionSource<ReplaySendProblemKind>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.Failed += (_, problem) => failed.TrySetResult(problem);

            var hello = new ContractHello
                        {
                            Token = token,
                            CompanionVersion = _companionVersion,
                            ProfileId = ProfileId,
                            ProfileName = ProfileName
                        };
            var refusal = await client.OpenAsync(_target.Address, hello, cancellationToken).ConfigureAwait(false);

            if (refusal is not null)
            {
                return (refusal, false, TimeSpan.Zero);
            }

            accepted = _timeProvider.GetUtcNow();

            lock (_lock)
            {
                _notify = client.NotifyState;

                if (_state is { } known)
                {
                    client.NotifyState(known);
                }
            }

            SetStatus(LiveSendState.Connected, null);
            CompanionLog.LiveSendConnected(_logger);

            var problem = await SendLoopAsync(client, failed, cancellationToken).ConfigureAwait(false);

            return (problem, true, _timeProvider.GetUtcNow() - accepted.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return accepted is { } since
                       ? (ReplaySendProblemKind.ConnectionLost, true, _timeProvider.GetUtcNow() - since)
                       : (ReplaySendProblemKind.ConnectionLost, false, TimeSpan.Zero);
        }
        finally
        {
            lock (_lock)
            {
                _notify = null;
                _isSending = false;
            }

            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sends the waiting messages one at a time until the connection fails.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="failed">The completion that the failure of the client sets</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection</returns>
    private async Task<ReplaySendProblemKind> SendLoopAsync(IngestClient client, TaskCompletionSource<ReplaySendProblemKind> failed, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (failed.Task.IsCompleted)
            {
                return failed.Task.Result;
            }

            (Guid Id, ReadOnlyMemory<byte> Utf8Json)? next = null;

            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    next = _queue[0];
                    _isSending = true;
                }
            }

            if (next is null)
            {
                await WaitForMessageAsync(failed, cancellationToken).ConfigureAwait(false);

                continue;
            }

            var (id, utf8Json) = next.Value;

            var acknowledgement = await client.SendAsync(utf8Json, id, cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                _isSending = false;

                if (acknowledgement is not null && _queue.Count > 0 && _queue[0].Id == id)
                {
                    _queuedBytes -= _queue[0].Utf8Json.Length;
                    _queue.RemoveAt(0);
                }
            }

            if (acknowledgement is null)
            {
                return client.Failure ?? ReplaySendProblemKind.ConnectionLost;
            }

            if (acknowledgement.Refusal is { } refusal)
            {
                CompanionLog.LiveMessageRefused(_logger, refusal);
            }

            LogDropped();
        }
    }

    /// <summary>
    /// Waits until a message is queued or the connection failed.
    /// </summary>
    /// <param name="failed">The completion that the failure of the client sets</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>A task that completes then</returns>
    private async Task WaitForMessageAsync(TaskCompletionSource<ReplaySendProblemKind> failed, CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var queued = _queued.WaitAsync(waitCancellation.Token);

        await Task.WhenAny(queued, failed.Task).ConfigureAwait(false);
        await waitCancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await queued.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            // The wait ended because the connection failed.
        }
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task? run;

        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            run = _run;
        }

        await _dispose.CancelAsync().ConfigureAwait(false);

        if (run is not null)
        {
            try
            {
                await run.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The run ended with the cancellation.
            }
        }

        LogDropped();
        _dispose.Dispose();
        _queued.Dispose();
    }

    #endregion // IAsyncDisposable
}