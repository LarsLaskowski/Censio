using System.Globalization;

using Censio.Contracts;
using Censio.GameData;
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

    /// <summary>
    /// The number of connections that may fail during the upload of a catalog before it is given up.
    /// </summary>
    public const int MaxCatalogAttempts = 3;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The server the sender sends to.
    /// </summary>
    private readonly LiveSendTarget _target;

    /// <summary>
    /// The buffer of the waiting messages.
    /// </summary>
    private readonly LiveBuffer _buffer;

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
    /// The lock of the status and the connection.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The cancellation of the run, requested by the dispose.
    /// </summary>
    private readonly CancellationTokenSource _dispose = new();

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

    /// <summary>
    /// The identity of the catalog the server holds on the current connection; only the run reads and writes it.
    /// </summary>
    private string? _serverIdentity;

    /// <summary>
    /// The catalog the current connection needs no more step for; only the run reads and writes it.
    /// </summary>
    private LiveCatalog? _settledCatalog;

    /// <summary>
    /// The catalog that is offered to the server, or <see langword="null" /> when there is none.
    /// </summary>
    private LiveCatalog? _catalog;

    /// <summary>
    /// Whether the offered catalog is not uploaded again in this run.
    /// </summary>
    private bool _isCatalogClosed;

    /// <summary>
    /// Whether the offered catalog waits for the messages that wait at the next hello, after a connection failed during its upload.
    /// </summary>
    private bool _isCatalogDeferred;

    /// <summary>
    /// The number of connections that failed during the upload of the offered catalog.
    /// </summary>
    private int _catalogFailures;

    /// <summary>
    /// The completion that the next change of the offered catalog sets.
    /// </summary>
    private TaskCompletionSource _catalogChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The profiles the sender sends under, or <see langword="null" /> when it sends under <c>Live</c>.
    /// </summary>
    private CompanionProfiles? _profiles;

    /// <summary>
    /// The number of changes of the active profile.
    /// </summary>
    private int _activeVersion;

    /// <summary>
    /// The completion that the next change of the active profile sets.
    /// </summary>
    private TaskCompletionSource _activeChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The number of renames of a profile.
    /// </summary>
    private int _renameVersion;

    /// <summary>
    /// The completion that the next rename of a profile sets.
    /// </summary>
    private TaskCompletionSource _renamed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The completion that the next change of the deleted identities sets.
    /// </summary>
    private TaskCompletionSource _deletionsChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The profile the next hello names instead of the active one, or <see langword="null" /> when it names the active one; only the run reads and writes it.
    /// </summary>
    private CompanionProfile? _helloProfile;

    /// <summary>
    /// The profile the hello of the current connection named; only the run reads and writes it.
    /// </summary>
    private Guid _connectionProfile = ProfileId;

    /// <summary>
    /// The number of changes of the active profile the current connection has seen; only the run reads and writes it.
    /// </summary>
    private int _seenActiveVersion;

    /// <summary>
    /// The number of renames the current connection has seen; only the run reads and writes it.
    /// </summary>
    private int _seenRenameVersion;

    /// <summary>
    /// The name of the profile the hello of the current connection sent; only the run reads and writes it.
    /// </summary>
    private string _connectionName = string.Empty;

    /// <summary>
    /// Whether the attempt ends with a profile switch; only the run reads and writes it.
    /// </summary>
    private bool _isSwitching;

    /// <summary>
    /// The time of the last profile switch, until a connection is accepted; only the run reads and writes it.
    /// </summary>
    private DateTimeOffset? _switchedAt;

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

    /// <summary>
    /// Occurs when a notice about the active profile is to be shown.
    /// </summary>
    public event EventHandler<ProfileNotice>? ProfileNoticeRaised;

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
    /// Gets the time after a profile switch in which a refusal because another Companion is connected is not counted.
    /// </summary>
    public static TimeSpan SwitchRefusalTime { get; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the delay before the hello after a profile switch.
    /// </summary>
    public static TimeSpan SwitchRetryDelay { get; } = TimeSpan.FromMilliseconds(250);

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
    /// Makes the sender send under the active profile of the profiles.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    public void UseProfiles(CompanionProfiles profiles)
    {
        lock (_lock)
        {
            _profiles = profiles;
        }

        profiles.ActiveChanged += (_, _) => OnActiveChanged();
        profiles.DeletionsChanged += (_, _) => OnDeletionsChanged();
        profiles.Renamed += (_, _) => OnRenamed();
    }

    /// <summary>
    /// Offers the state of the game catalog; a ready catalog is uploaded on the next connection.
    /// </summary>
    /// <param name="status">The status of the catalog loader</param>
    /// <param name="catalog">The published catalog, or <c>null</c> if none is built</param>
    /// <param name="icons">The published icons, or <c>null</c> if none are built</param>
    /// <param name="build">The build of the published catalog, or <c>null</c> when unknown</param>
    public void SetCatalog(GameCatalogStatus status, GameCatalog? catalog, GameIconSet? icons, string? build)
    {
        switch (status.Kind)
        {
            case GameCatalogStatusKind.Ready or GameCatalogStatusKind.NotCached or GameCatalogStatusKind.IconsFailed when catalog is not null:
                {
                    OfferCatalog(LiveCatalog.Create(catalog, icons, build));
                }

                break;

            case GameCatalogStatusKind.NoGame or GameCatalogStatusKind.Failed:
                {
                    OfferCatalog(null);
                }

                break;
        }
    }

    /// <summary>
    /// Puts an item of the pipe connection into the buffer for sending; it never waits.
    /// </summary>
    /// <param name="item">The item</param>
    public void Enqueue(PipeConnectionItem item)
    {
        var profiles = GetProfiles();
        var notice = ObserveSession(profiles, item.Result);

        if (Convert(item, profiles?.Active.Id ?? ProfileId) is not var (id, message))
        {
            RaiseNotice(notice);

            return;
        }

        var written = ContractWriter.Write(message);

        if (written.Error is not null)
        {
            CompanionLog.LiveMessageNotWritten(_logger);
            RaiseNotice(notice);

            return;
        }

        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _buffer.Add(id, written.Utf8Json);
        }

        RaiseNotice(notice);
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
    /// <param name="profileId">The identity of the profile the message belongs to</param>
    /// <returns>The message, or <see langword="null" /> when the item has none</returns>
    private static (Guid Id, ContractMessage Message)? Convert(PipeConnectionItem item, Guid profileId)
    {
        var result = item.Result;

        switch (result.Kind)
        {
            case PipeTickResultKind.Tick when result.Tick is { Islands.Count: > 0 } tick:
                {
                    var message = ContractMessageConverter.ToTick(tick, profileId);

                    return (message.Id, message);
                }

            case PipeTickResultKind.TradeRoutes when result.TradeRoutes is { } routes:
                {
                    var message = ContractMessageConverter.ToTradeRoutes(routes, profileId, item.ReadStamp);

                    return (message.Id, message);
                }

            case PipeTickResultKind.SessionStart:
                {
                    var message = ContractMessageConverter.ToSessionStart(profileId, item.ReadStamp);

                    return (message.Id, message);
                }

            case PipeTickResultKind.SessionEnd:
                {
                    var message = ContractMessageConverter.ToSessionEnd(profileId, item.ReadStamp);

                    return (message.Id, message);
                }

            default:
                {
                    return null;
                }
        }
    }

    /// <summary>
    /// Finds the oldest waiting removal when it was not tried yet; a tried one that still waits could not be written and blocks the later ones on this connection.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="tried">The identities tried on this connection</param>
    /// <returns>The removal, or <see langword="null" /> when none is left or the oldest was tried</returns>
    private static ProfileRemoval? FirstUntried(CompanionProfiles profiles, HashSet<Guid> tried)
    {
        var waiting = profiles.WaitingRemovals;

        return waiting.Count > 0 && tried.Contains(waiting[0].Id) == false ? waiting[0] : null;
    }

    /// <summary>
    /// Reads the identity of the profile a message belongs to.
    /// </summary>
    /// <param name="utf8Json">The message as the buffer holds it</param>
    /// <returns>The identity, or <see langword="null" /> when the message cannot be read or has none</returns>
    private static Guid? ProfileOf(ReadOnlyMemory<byte> utf8Json)
    {
        return ContractReader.Read(utf8Json.Span).Message switch
               {
                   ContractTick tick => tick.ProfileId,
                   ContractTradeRoutes routes => routes.ProfileId,
                   ContractSessionStart start => start.ProfileId,
                   ContractSessionEnd end => end.ProfileId,
                   _ => null
               };
    }

    /// <summary>
    /// Observes the session of an item and gives the notice its start raises.
    /// </summary>
    /// <param name="profiles">The profiles, or <see langword="null" /> when the sender sends under <c>Live</c></param>
    /// <param name="result">The result of the item</param>
    /// <returns>The notice, or <see langword="null" /> when there is none</returns>
    private static ProfileNotice? ObserveSession(CompanionProfiles? profiles, PipeTickResult result)
    {
        if (profiles is null)
        {
            return null;
        }

        if (result.Kind == PipeTickResultKind.SessionStart && result.Headline is { } figure)
        {
            return profiles.ObserveSessionStart(figure);
        }

        if (result.Kind == PipeTickResultKind.SessionEnd)
        {
            profiles.ObserveSessionEnd();
        }

        return null;
    }

    /// <summary>
    /// Gives the reason of a problem as the tray shows it.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The reason</returns>
    private static string Reason(ReplaySendProblemKind problem)
    {
        return TrayMenu.FormatReplaySendReason(CompanionTexts.English, problem) ?? problem.ToString();
    }

    /// <summary>
    /// Closes the connection of a profile switch with the close handshake; a failure of the close is not a problem.
    /// </summary>
    /// <param name="client">The client</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>A task that completes when the connection is closed</returns>
    private static async Task CloseAsync(IngestClient client, CancellationToken cancellationToken)
    {
        try
        {
            await client.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The connection is disposed next whether or not the close handshake completed.
        }
    }

    /// <summary>
    /// Gets the profiles the sender sends under.
    /// </summary>
    /// <returns>The profiles, or <see langword="null" /> when the sender sends under <c>Live</c></returns>
    private CompanionProfiles? GetProfiles()
    {
        lock (_lock)
        {
            return _profiles;
        }
    }

    /// <summary>
    /// Raises <see cref="ProfileNoticeRaised" />.
    /// </summary>
    /// <param name="notice">The notice, or <see langword="null" /> for none</param>
    private void RaiseNotice(ProfileNotice? notice)
    {
        if (notice is null)
        {
            return;
        }

        CompanionLog.ProfileNoticeRaised(_logger, notice.Kind);

        try
        {
            ProfileNoticeRaised?.Invoke(this, notice);
        }
        catch (Exception)
        {
            // A failing subscriber must not stop live sending
        }
    }

    /// <summary>
    /// Notes a change of the active profile and wakes the send loop.
    /// </summary>
    private void OnActiveChanged()
    {
        TaskCompletionSource changed;

        lock (_lock)
        {
            _activeVersion++;
            changed = _activeChanged;
            _activeChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.TrySetResult();
    }

    /// <summary>
    /// Notes a rename of a profile and wakes the send loop.
    /// </summary>
    private void OnRenamed()
    {
        TaskCompletionSource renamed;

        lock (_lock)
        {
            _renameVersion++;
            renamed = _renamed;
            _renamed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        renamed.TrySetResult();
    }

    /// <summary>
    /// Notes a change of the deleted identities and wakes the send loop.
    /// </summary>
    private void OnDeletionsChanged()
    {
        TaskCompletionSource changed;

        lock (_lock)
        {
            changed = _deletionsChanged;
            _deletionsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.TrySetResult();
    }

    /// <summary>
    /// Reads the completion of the next change of the deleted identities.
    /// </summary>
    /// <returns>The completion</returns>
    private Task GetDeletionsView()
    {
        lock (_lock)
        {
            return _deletionsChanged.Task;
        }
    }

    /// <summary>
    /// Reads the number of changes of the active profile and the completion of the next one.
    /// </summary>
    /// <returns>The number and the completion</returns>
    private (int Version, Task Changed) GetActiveView()
    {
        lock (_lock)
        {
            return (_activeVersion, _activeChanged.Task);
        }
    }

    /// <summary>
    /// Reads the number of renames and the completion of the next one.
    /// </summary>
    /// <returns>The number and the completion</returns>
    private (int Version, Task Changed) GetRenameView()
    {
        lock (_lock)
        {
            return (_renameVersion, _renamed.Task);
        }
    }

    /// <summary>
    /// Replaces the offered catalog, unless the new one has the identity of the current one, and wakes the send loop.
    /// </summary>
    /// <param name="created">The new catalog, or <see langword="null" /> to clear it</param>
    private void OfferCatalog(LiveCatalog? created)
    {
        TaskCompletionSource changed;

        lock (_lock)
        {
            if (_isDisposed || (created?.Identity is not null && _catalog?.Identity == created.Identity))
            {
                return;
            }

            _catalog = created;
            _isCatalogClosed = created is not null && created.Identity is null;
            _isCatalogDeferred = false;
            _catalogFailures = 0;
            changed = _catalogChanged;
            _catalogChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        if (created is not null && created.Identity is null)
        {
            CompanionLog.CatalogUploadNotWritten(_logger);
        }

        changed.TrySetResult();
    }

    /// <summary>
    /// Reads the catalog that is due for an upload and the completion of its next change.
    /// </summary>
    /// <returns>The catalog, or <see langword="null" /> when there is none or it is closed; whether it is deferred; the completion</returns>
    private (LiveCatalog? Catalog, bool IsDeferred, Task Changed) GetCatalogView()
    {
        lock (_lock)
        {
            return (_isCatalogClosed ? null : _catalog, _isCatalogDeferred, _catalogChanged.Task);
        }
    }

    /// <summary>
    /// Checks whether the catalog is the one offered now.
    /// </summary>
    /// <param name="live">The catalog</param>
    /// <returns><see langword="true" /> when it is</returns>
    private bool IsCurrentCatalog(LiveCatalog live)
    {
        lock (_lock)
        {
            return ReferenceEquals(_catalog, live);
        }
    }

    /// <summary>
    /// Closes a catalog so that it is not uploaded again in this run, when it is the one offered now.
    /// </summary>
    /// <param name="live">The catalog</param>
    private void CloseCatalog(LiveCatalog live)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_catalog, live))
            {
                _isCatalogClosed = true;
            }
        }
    }

    /// <summary>
    /// Counts a connection that failed during the upload of a catalog and defers the catalog; the third one closes it.
    /// </summary>
    /// <param name="live">The catalog</param>
    private void CountCatalogFailure(LiveCatalog live)
    {
        var isAbandoned = false;

        lock (_lock)
        {
            if (ReferenceEquals(_catalog, live))
            {
                _catalogFailures++;
                _isCatalogDeferred = true;
                isAbandoned = _catalogFailures >= MaxCatalogAttempts;
                _isCatalogClosed |= isAbandoned;
            }
        }

        if (isAbandoned)
        {
            CompanionLog.CatalogUploadAbandoned(_logger, MaxCatalogAttempts);
        }
    }

    /// <summary>
    /// Ends the count of failures and the deferral of a catalog whose upload completed, when it is the one offered now.
    /// </summary>
    /// <param name="live">The catalog</param>
    private void CompleteCatalog(LiveCatalog live)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_catalog, live))
            {
                _catalogFailures = 0;
                _isCatalogDeferred = false;
            }
        }
    }

    /// <summary>
    /// Logs the number of dropped messages that no log line holds yet.
    /// </summary>
    private void LogDropped()
    {
        var count = _buffer.TakeUnloggedDropped();

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

            if (await TryContinueSwitchAsync(outcome.IsSwitch, problem, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            _helloProfile = null;

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
    /// Handles an attempt that ended with a profile switch or with a refusal that is part of one.
    /// </summary>
    /// <param name="isSwitch">Whether the attempt ended with a profile switch</param>
    /// <param name="problem">The problem that ended the attempt</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns><see langword="true" /> when the attempt is to be repeated at once, without a delay step</returns>
    private async Task<bool> TryContinueSwitchAsync(bool isSwitch, ReplaySendProblemKind? problem, CancellationToken cancellationToken)
    {
        if (isSwitch)
        {
            _switchedAt = _timeProvider.GetUtcNow();
            SetStatus(LiveSendState.Connecting, null);

            return true;
        }

        if (problem is not ReplaySendProblemKind.CompanionConnected || _switchedAt is not { } switchedAt || _timeProvider.GetUtcNow() - switchedAt > SwitchRefusalTime)
        {
            return false;
        }

        SetStatus(LiveSendState.Connecting, null);

        await Task.Delay(SwitchRetryDelay, _timeProvider, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Makes one connection: reads the token, connects and sends until the connection fails.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The outcome</returns>
    private async Task<(ReplaySendProblemKind? Problem, bool IsAccepted, TimeSpan Duration, bool IsSwitch)> AttemptAsync(CancellationToken cancellationToken)
    {
        IngestClient? client = null;
        var accepted = default(DateTimeOffset?);

        _isSwitching = false;

        try
        {
            string? token = null;

            if (_target.ReadToken is not null)
            {
                token = await _target.ReadToken(cancellationToken).ConfigureAwait(false);

                if (token is null && _target.IsLocal)
                {
                    return (ReplaySendProblemKind.LocalServerNotRunning, false, TimeSpan.Zero, false);
                }
            }

            client = new IngestClient(_connector, _timeProvider);

            var failed = new TaskCompletionSource<ReplaySendProblemKind>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.Failed += (_, problem) => failed.TrySetResult(problem);

            var refusal = await client.OpenAsync(_target.Address, CreateHello(token), cancellationToken).ConfigureAwait(false);

            if (refusal is not null)
            {
                return (refusal, false, TimeSpan.Zero, false);
            }

            accepted = _timeProvider.GetUtcNow();
            _switchedAt = null;

            AttachStateNotification(client);
            SetStatus(LiveSendState.Connected, null);
            CompanionLog.LiveSendConnected(_logger);

            var problem = await SendLoopAsync(client, failed, cancellationToken).ConfigureAwait(false);

            return (problem, true, _timeProvider.GetUtcNow() - accepted.Value, _isSwitching);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return accepted is { } since
                       ? (ReplaySendProblemKind.ConnectionLost, true, _timeProvider.GetUtcNow() - since, false)
                       : (ReplaySendProblemKind.ConnectionLost, false, TimeSpan.Zero, false);
        }
        finally
        {
            lock (_lock)
            {
                _notify = null;
            }

            _buffer.ReleaseInFlight();

            if (client is not null)
            {
                if (_isSwitching)
                {
                    await CloseAsync(client, cancellationToken).ConfigureAwait(false);
                }

                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates the hello of a connection for the profile that <see cref="ChooseHelloProfile" /> chooses.
    /// </summary>
    /// <param name="token">The token, or <see langword="null" /> when the target needs none</param>
    /// <returns>The hello</returns>
    private ContractHello CreateHello(string? token)
    {
        var profile = ChooseHelloProfile();

        return new ContractHello
               {
                   Token = token,
                   CompanionVersion = _companionVersion,
                   ProfileId = profile.Id,
                   ProfileName = profile.Name
               };
    }

    /// <summary>
    /// Lets the client notify the known state of the game and sends that state at once.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    private void AttachStateNotification(IngestClient client)
    {
        lock (_lock)
        {
            _notify = client.NotifyState;

            if (_state is { } known)
            {
                client.NotifyState(known);
            }
        }
    }

    /// <summary>
    /// Chooses the profile the hello names and notes the changes of the active profile and the renames the connection starts with.
    /// </summary>
    /// <returns>The profile</returns>
    private CompanionProfile ChooseHelloProfile()
    {
        CompanionProfiles? profiles;

        lock (_lock)
        {
            profiles = _profiles;
            _seenActiveVersion = _activeVersion;
            _seenRenameVersion = _renameVersion;
        }

        CompanionProfile profile;

        if (profiles is null)
        {
            profile = CompanionProfile.Live;
        }
        else if (_helloProfile is { } named && profiles.IsDeleted(named.Id) == false && profiles.NameOf(named.Id) is { } name)
        {
            profile = new CompanionProfile
                      {
                          Id = named.Id,
                          Name = name
                      };
            _helloProfile = profile;
        }
        else
        {
            _helloProfile = null;
            profile = profiles.Active;
        }

        _connectionProfile = profile.Id;
        _connectionName = profile.Name;

        return profile;
    }

    /// <summary>
    /// Decides whether a change of the active profile or a rename ends the connection.
    /// </summary>
    /// <param name="activeVersion">The number of changes of the active profile</param>
    /// <param name="renameVersion">The number of renames</param>
    /// <returns><see langword="true" /> when the connection names another profile than the active one, or the connection's profile has another name</returns>
    private bool IsSwitchDue(int activeVersion, int renameVersion)
    {
        if (_profiles is not { } profiles)
        {
            return false;
        }

        var isActiveMoved = activeVersion != _seenActiveVersion;
        var isRenameMoved = renameVersion != _seenRenameVersion;

        _seenActiveVersion = activeVersion;
        _seenRenameVersion = renameVersion;

        if (isActiveMoved && profiles.Active.Id != _connectionProfile)
        {
            _helloProfile = null;
            _isSwitching = true;

            return true;
        }

        if (isRenameMoved && profiles.NameOf(_connectionProfile) is { } name && string.Equals(name, _connectionName, StringComparison.Ordinal) == false)
        {
            _helloProfile = profiles.Active.Id == _connectionProfile
                                ? null
                                : new CompanionProfile
                                  {
                                      Id = _connectionProfile,
                                      Name = name
                                  };
            _isSwitching = true;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Uploads the catalog when it is due and sends the waiting messages one at a time until the connection fails.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="failed">The completion that the failure of the client sets</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection</returns>
    private async Task<ReplaySendProblemKind> SendLoopAsync(IngestClient client, TaskCompletionSource<ReplaySendProblemKind> failed, CancellationToken cancellationToken)
    {
        var waitingAtHello = _buffer.Status.Waiting;
        var acknowledged = 0;
        var tried = new HashSet<Guid>();

        _serverIdentity = client.ServerCatalogIdentity;
        _settledCatalog = null;

        while (true)
        {
            var turn = await StartTurnAsync(client, failed, tried, cancellationToken).ConfigureAwait(false);

            if (turn.Problem is { } turnProblem)
            {
                return turnProblem;
            }

            var view = GetCatalogView();
            var step = await UploadDueCatalogAsync(client, view, acknowledged >= waitingAtHello, cancellationToken).ConfigureAwait(false);

            if (step.Problem is { } problem)
            {
                return problem;
            }

            if (step.IsUploaded)
            {
                continue;
            }

            var next = await NextOrFailureAsync(failed, view.Changed, turn.ActiveChanged, turn.RenameChanged, turn.DeletionsChanged, cancellationToken).ConfigureAwait(false);

            if (next is null)
            {
                continue;
            }

            var resolved = ResolveBuffered(next);

            if (resolved.IsDropped)
            {
                acknowledged++;

                continue;
            }

            if (IsOfOtherProfile(resolved.Profile))
            {
                return ReplaySendProblemKind.ConnectionLost;
            }

            if (await SendBufferedAsync(client, next.Id, resolved.Utf8Json, cancellationToken).ConfigureAwait(false) is { } sendProblem)
            {
                return sendProblem;
            }

            acknowledged++;
        }
    }

    /// <summary>
    /// Starts a turn of the send loop: checks the failure of the connection and a due switch, then sends the waiting removals.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="failed">The completion that the failure of the client sets</param>
    /// <param name="tried">The deletions tried on this connection</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection, and the completions of the next change of the active profile, of the next rename and of the deleted identities</returns>
    private async Task<(ReplaySendProblemKind? Problem, Task ActiveChanged, Task RenameChanged, Task DeletionsChanged)> StartTurnAsync(IngestClient client, TaskCompletionSource<ReplaySendProblemKind> failed, HashSet<Guid> tried, CancellationToken cancellationToken)
    {
        var (activeVersion, activeChanged) = GetActiveView();
        var (renameVersion, renameChanged) = GetRenameView();

        var deletionsChanged = GetDeletionsView();

        if (failed.Task.IsCompleted)
        {
            return (failed.Task.Result, activeChanged, renameChanged, deletionsChanged);
        }

        if (IsSwitchDue(activeVersion, renameVersion))
        {
            return (ReplaySendProblemKind.ConnectionLost, activeChanged, renameChanged, deletionsChanged);
        }

        return (await SendRemovalsAsync(client, tried, cancellationToken).ConfigureAwait(false), activeChanged, renameChanged, deletionsChanged);
    }

    /// <summary>
    /// Follows the merges of the profile of a buffered message: drops the message when the profile was deleted or the merges do not end,
    /// writes it again under the profile that holds the history now when that is another one.
    /// </summary>
    /// <param name="entry">The message</param>
    /// <returns>Whether the message was dropped, the profile it belongs to now, and the message to send</returns>
    private (bool IsDropped, Guid? Profile, ReadOnlyMemory<byte> Utf8Json) ResolveBuffered(LiveBufferEntry entry)
    {
        var profile = ProfileOf(entry.Utf8Json);

        if (_profiles is not { } profiles || profile is not { } id)
        {
            return (false, profile, entry.Utf8Json);
        }

        var resolved = profiles.ResolveProfile(id);

        if (resolved is not { } target || profiles.IsDeleted(target))
        {
            return (DropInFlight(entry), null, default);
        }

        if (target == id)
        {
            return (false, profile, entry.Utf8Json);
        }

        var message = ContractReader.Read(entry.Utf8Json.Span).Message;
        var moved = message is null ? null : ContractMessageConverter.WithProfile(message, target);
        var written = moved is null ? null : ContractWriter.Write(moved);

        if (written is null || written.Error is not null)
        {
            CompanionLog.LiveMessageNotWritten(_logger);

            return (DropInFlight(entry), null, default);
        }

        return (false, target, written.Utf8Json);
    }

    /// <summary>
    /// Drops the message in flight and logs the dropped messages.
    /// </summary>
    /// <param name="entry">The message</param>
    /// <returns><see langword="true" /></returns>
    private bool DropInFlight(LiveBufferEntry entry)
    {
        _buffer.DropInFlight(entry.Id);
        LogDropped();

        return true;
    }

    /// <summary>
    /// Checks whether a message belongs to a known profile other than the one the connection names; if so, releases it and
    /// prepares the switch to its profile.
    /// </summary>
    /// <param name="profile">The profile of the message, or <see langword="null" /> when it names none</param>
    /// <returns><see langword="true" /> when the connection is to be switched</returns>
    private bool IsOfOtherProfile(Guid? profile)
    {
        if (_profiles is not { } profiles || profile is not { } id || id == _connectionProfile || profiles.NameOf(id) is not { } name)
        {
            return false;
        }

        _buffer.ReleaseInFlight();

        _helloProfile = new CompanionProfile
                        {
                            Id = id,
                            Name = name
                        };
        _isSwitching = true;

        return true;
    }

    /// <summary>
    /// Skips the catalog when the server holds it, or uploads it when it is due.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="view">The catalog that is offered</param>
    /// <param name="isDeferralOver">Whether the messages that waited at the hello are acknowledged</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection, and whether an upload was made</returns>
    private async Task<(ReplaySendProblemKind? Problem, bool IsUploaded)> UploadDueCatalogAsync(IngestClient client, (LiveCatalog? Catalog, bool IsDeferred, Task Changed) view, bool isDeferralOver, CancellationToken cancellationToken)
    {
        if (view.Catalog is not { } live || ReferenceEquals(live, _settledCatalog))
        {
            return (null, false);
        }

        if (live.Identity == _serverIdentity)
        {
            CompanionLog.CatalogAlreadyOnServer(_logger);
            _settledCatalog = live;

            return (null, false);
        }

        if (view.IsDeferred && isDeferralOver == false && _buffer.Status.Waiting > 0)
        {
            return (null, false);
        }

        var upload = await UploadCatalogAsync(client, live, cancellationToken).ConfigureAwait(false);

        if (upload.Identity is not null)
        {
            _serverIdentity = upload.Identity;
            _settledCatalog = live;
        }

        return (upload.Problem, true);
    }

    /// <summary>
    /// Sends the waiting removals of profile histories (deletions and merges) not yet tried on this connection, one at a time in
    /// queue order; a removal that cannot be written ends the sending of removals on this connection.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="tried">The identities tried on this connection</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection, or <see langword="null" /> when none</returns>
    private async Task<ReplaySendProblemKind?> SendRemovalsAsync(IngestClient client, HashSet<Guid> tried, CancellationToken cancellationToken)
    {
        if (_profiles is not { } profiles)
        {
            return null;
        }

        while (FirstUntried(profiles, tried) is { } removal)
        {
            tried.Add(removal.Id);

            var id = Guid.NewGuid();
            ContractMessage message = removal.Target is { } target
                                          ? new ContractProfileMerge
                                            {
                                                Id = id,
                                                ProfileId = removal.Id,
                                                TargetProfileId = target
                                            }
                                          : new ContractProfileDeletion
                                            {
                                                Id = id,
                                                ProfileId = removal.Id
                                            };
            var written = ContractWriter.Write(message);

            if (written.Error is not null)
            {
                CompanionLog.LiveMessageNotWritten(_logger);

                return null;
            }

            var acknowledgement = await client.SendAsync(written.Utf8Json, id, cancellationToken).ConfigureAwait(false);

            if (acknowledgement is null)
            {
                return client.Failure ?? ReplaySendProblemKind.ConnectionLost;
            }

            ConfirmRemoval(profiles, removal, acknowledgement.Refusal);
        }

        return null;
    }

    /// <summary>
    /// Confirms an acknowledged removal and logs its outcome.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="removal">The removal</param>
    /// <param name="refusal">The refusal of the acknowledgement, or <see langword="null" /> when the server accepted</param>
    private void ConfirmRemoval(CompanionProfiles profiles, ProfileRemoval removal, ContractAcknowledgementRefusal? refusal)
    {
        var isMerge = removal.Target is not null;

        if (isMerge)
        {
            profiles.ConfirmMerge(removal.Id);
        }
        else
        {
            profiles.ConfirmDeletion(removal.Id);
        }

        if (refusal is { } refused)
        {
            if (isMerge)
            {
                CompanionLog.ProfileMergeRefused(_logger, refused);
            }
            else
            {
                CompanionLog.ProfileDeletionRefused(_logger, refused);
            }
        }
        else if (isMerge)
        {
            CompanionLog.ProfileMergeConfirmed(_logger);
        }
        else
        {
            CompanionLog.ProfileDeletionConfirmed(_logger);
        }
    }

    /// <summary>
    /// Sends a message of the buffer and waits for its acknowledgement.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="utf8Json">The message to send, under the profile it belongs to now</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection, or <see langword="null" /> when the message was acknowledged</returns>
    private async Task<ReplaySendProblemKind?> SendBufferedAsync(IngestClient client, Guid id, ReadOnlyMemory<byte> utf8Json, CancellationToken cancellationToken)
    {
        var acknowledgement = await client.SendAsync(utf8Json, id, cancellationToken).ConfigureAwait(false);

        if (acknowledgement is null)
        {
            return client.Failure ?? ReplaySendProblemKind.ConnectionLost;
        }

        _buffer.Acknowledge(id);

        if (acknowledgement.Refusal is { } refusal)
        {
            CompanionLog.LiveMessageRefused(_logger, refusal);
        }

        LogDropped();

        return null;
    }

    /// <summary>
    /// Uploads a catalog and the icons its acknowledgement lists, one message at a time.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="live">The catalog</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The problem that ended the connection, or the identity the server holds now, or neither when the upload was refused</returns>
    private async Task<(ReplaySendProblemKind? Problem, string? Identity)> UploadCatalogAsync(IngestClient client, LiveCatalog live, CancellationToken cancellationToken)
    {
        var sent = await SendCatalogAsync(client, live, cancellationToken).ConfigureAwait(false);

        if (sent.IsWritten == false)
        {
            return (null, null);
        }

        if (sent.Acknowledgement is not { } acknowledgement)
        {
            return (CatalogFailure(client, live), null);
        }

        if (acknowledgement.Refusal is { } refusal)
        {
            CompanionLog.CatalogRefusedByServer(_logger, refusal);
            CloseCatalog(live);

            return (null, null);
        }

        var accepted = 0;
        var refused = 0;

        foreach (var reference in acknowledgement.MissingIcons)
        {
            if (IsCurrentCatalog(live) == false)
            {
                return (null, live.Identity);
            }

            var icon = live.WriteIcon(reference);

            if (icon is null)
            {
                refused++;

                continue;
            }

            var iconAcknowledgement = await client.SendAsync(icon.Value.Utf8Json, icon.Value.Id, cancellationToken).ConfigureAwait(false);

            if (iconAcknowledgement is null)
            {
                return (CatalogFailure(client, live), null);
            }

            if (iconAcknowledgement.Refusal is null)
            {
                accepted++;
            }
            else
            {
                refused++;
            }
        }

        CompanionLog.CatalogSentToServer(_logger, accepted, refused);
        CompleteCatalog(live);

        return (null, live.Identity);
    }

    /// <summary>
    /// Writes the catalog upload and sends it, so that nothing of the written message outlives its acknowledgement.
    /// </summary>
    /// <param name="client">The client of the accepted connection</param>
    /// <param name="live">The catalog</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>Whether the upload was written, and its acknowledgement, which is <see langword="null" /> when the connection failed</returns>
    private async Task<(bool IsWritten, ContractAcknowledgement? Acknowledgement)> SendCatalogAsync(IngestClient client, LiveCatalog live, CancellationToken cancellationToken)
    {
        var upload = live.WriteUpload();

        if (upload is null)
        {
            CloseCatalog(live);
            CompanionLog.CatalogUploadNotWritten(_logger);

            return (false, null);
        }

        return (true, await client.SendCatalogUploadAsync(upload.Value.Utf8Json, upload.Value.Id, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Counts the failure of a connection during the upload of a catalog and gives its problem.
    /// </summary>
    /// <param name="client">The client of the connection</param>
    /// <param name="live">The catalog</param>
    /// <returns>The problem</returns>
    private ReplaySendProblemKind CatalogFailure(IngestClient client, LiveCatalog live)
    {
        CountCatalogFailure(live);

        return client.Failure ?? ReplaySendProblemKind.ConnectionLost;
    }

    /// <summary>
    /// Waits for the next message of the buffer, the failure of the connection or a change of the offered catalog.
    /// </summary>
    /// <param name="failed">The completion that the failure of the client sets</param>
    /// <param name="changed">The completion that the next change of the offered catalog sets</param>
    /// <param name="activeChanged">The completion that the next change of the active profile sets</param>
    /// <param name="renameChanged">The completion that the next rename of a profile sets</param>
    /// <param name="deletionsChanged">The completion that the next change of the deleted identities sets</param>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The message, or <see langword="null" /> when the connection failed or the catalog or the active profile changed first</returns>
    private async Task<LiveBufferEntry?> NextOrFailureAsync(TaskCompletionSource<ReplaySendProblemKind> failed, Task changed, Task activeChanged, Task renameChanged, Task deletionsChanged, CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var next = _buffer.NextAsync(waitCancellation.Token);

        await Task.WhenAny(next, failed.Task, changed, activeChanged, renameChanged, deletionsChanged).ConfigureAwait(false);

        if (next.IsCompletedSuccessfully)
        {
            return next.Result;
        }

        await waitCancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await next.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            // The wait ended because the connection failed or the catalog changed.
        }

        _buffer.ReleaseInFlight();

        return null;
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
    }

    #endregion // IAsyncDisposable
}