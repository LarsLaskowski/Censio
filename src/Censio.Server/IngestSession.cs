using System.Net.WebSockets;

using Censio.Contracts;
using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// One WebSocket connection of the ingest endpoint, from the hello to the close.
/// </summary>
public sealed partial class IngestSession : IDisposable
{
    #region Constants

    /// <summary>
    /// The maximum size of the hello message in bytes.
    /// </summary>
    public const int MaxHelloBytes = 32768;

    /// <summary>
    /// The size of the buffer that frames are discarded into after a close.
    /// </summary>
    private const int DiscardBytes = 1024;

    /// <summary>
    /// The state of the hello phase while it is open.
    /// </summary>
    private const int PhaseOpen = 0;

    /// <summary>
    /// The state of the hello phase after the hello was decided.
    /// </summary>
    private const int PhaseDecided = 1;

    /// <summary>
    /// The state of the hello phase after its time ran out.
    /// </summary>
    private const int PhaseTimedOut = 2;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The only contract version the server supports.
    /// </summary>
    private static readonly int[] _supportedVersions = [ContractReader.CurrentVersion];

    /// <summary>
    /// The WebSocket of the connection.
    /// </summary>
    private readonly WebSocket _webSocket;

    /// <summary>
    /// The access that checks the token.
    /// </summary>
    private readonly IngestAccess _access;

    /// <summary>
    /// The connection counters.
    /// </summary>
    private readonly IngestConnections _connections;

    /// <summary>
    /// The server state the messages are stored through.
    /// </summary>
    private readonly ServerState _state;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The version of the server.
    /// </summary>
    private readonly string _serverVersion;

    /// <summary>
    /// The lock that serializes everything the session sends.
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>
    /// The state of the hello phase.
    /// </summary>
    private int _helloPhase;

    /// <summary>
    /// A value indicating whether a close was decided (0 no, 1 yes).
    /// </summary>
    private int _closing;

    /// <summary>
    /// A value indicating whether the pending place was left (0 no, 1 yes).
    /// </summary>
    private int _pendingLeft;

    /// <summary>
    /// A value indicating whether the session holds the place of the Companion (0 no, 1 yes).
    /// </summary>
    private int _placeTaken;

    /// <summary>
    /// A value indicating whether the hello was accepted.
    /// </summary>
    private bool _helloAccepted;

    /// <summary>
    /// The token that signals the stop of the server, passed to every store call.
    /// </summary>
    private CancellationToken _stoppingToken;

    /// <summary>
    /// The number of accepted messages.
    /// </summary>
    private int _acceptedCount;

    /// <summary>
    /// The number of refused messages.
    /// </summary>
    private int _refusedCount;

    /// <summary>
    /// The timer that ends the hello phase.
    /// </summary>
    private ITimer? _helloTimer;

    /// <summary>
    /// The timer that aborts the socket when the peer does not answer a close.
    /// </summary>
    private ITimer? _closeWaitTimer;

    /// <summary>
    /// The close the hello timer started.
    /// </summary>
    private Task _timeoutClose = Task.CompletedTask;

    /// <summary>
    /// The close the stop of the server started.
    /// </summary>
    private Task _stoppingClose = Task.CompletedTask;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestSession" /> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket of the connection</param>
    /// <param name="access">The access that checks the token</param>
    /// <param name="connections">The connection counters</param>
    /// <param name="state">The server state the messages are stored through</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <param name="serverVersion">The version of the server</param>
    public IngestSession(WebSocket webSocket, IngestAccess access, IngestConnections connections, ServerState state, TimeProvider timeProvider, ILogger logger, string serverVersion)
    {
        _webSocket = webSocket;
        _access = access;
        _connections = connections;
        _state = state;
        _timeProvider = timeProvider;
        _logger = logger;
        _serverVersion = serverVersion;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the time the Companion has to send its hello (10 seconds).
    /// </summary>
    public static TimeSpan HelloTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the time a close waits for the reply of the peer (2 seconds).
    /// </summary>
    public static TimeSpan CloseWait => TimeSpan.FromSeconds(2);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Runs the session until the connection ends.
    /// </summary>
    /// <param name="stoppingToken">The token that signals the stop of the server</param>
    /// <returns>The task</returns>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        _helloTimer = _timeProvider.CreateTimer(static state => ((IngestSession)state!).OnHelloTimeout(), this, HelloTimeout, Timeout.InfiniteTimeSpan);

        var registration = stoppingToken.Register(static state => ((IngestSession)state!).OnStopping(), this);

        try
        {
            var buffer = new byte[MaxHelloBytes + 1];

            if (await RunHelloPhaseAsync(buffer).ConfigureAwait(false))
            {
                await RunMessagePhaseAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The connection is gone; the session ends without a log line of its own.
            _ = exception;
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
            await EndAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the id of a message that is acknowledged.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The id, or <c>null</c> when the message is of no acknowledged type</returns>
    private static Guid? GetId(ContractMessage message)
    {
        return message switch
               {
                   ContractTick tick => tick.Id,
                   ContractSessionStart start => start.Id,
                   ContractSessionEnd end => end.Id,
                   ContractIconImage icon => icon.Id,
                   ContractCatalogUpload upload => upload.Id,
                   _ => null
               };
    }

    /// <summary>
    /// Maps the error of a refused message to the refusal of its acknowledgement.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The refusal</returns>
    private static ContractAcknowledgementRefusal MapRefusal(ContractError error)
    {
        if (error.Kind != ContractErrorKind.CatalogRefused)
        {
            return ContractAcknowledgementRefusal.Invalid;
        }

        return error.CatalogError!.Kind switch
               {
                   CatalogDocumentErrorKind.Malformed => ContractAcknowledgementRefusal.CatalogMalformed,
                   CatalogDocumentErrorKind.UnsupportedVersion => ContractAcknowledgementRefusal.CatalogUnsupportedVersion,
                   CatalogDocumentErrorKind.TooLarge => ContractAcknowledgementRefusal.CatalogTooLarge,
                   _ => ContractAcknowledgementRefusal.CatalogInvalid
               };
    }

    /// <summary>
    /// Gets the close status for a received frame that is no text.
    /// </summary>
    /// <param name="messageType">The type of the frame</param>
    /// <returns>The status to close with, or <c>null</c> when the frame is the close of the peer</returns>
    private static WebSocketCloseStatus? GetNonTextStatus(WebSocketMessageType messageType)
    {
        return messageType == WebSocketMessageType.Close ? null : WebSocketCloseStatus.InvalidMessageType;
    }

    /// <summary>
    /// Logs that a Companion is connected.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Companion connected.")]
    private static partial void LogCompanionAccepted(ILogger logger);

    /// <summary>
    /// Logs the refusal of a hello because another Companion is connected.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Companion refused: another Companion is connected.")]
    private static partial void LogCompanionConnected(ILogger logger);

    /// <summary>
    /// Logs the refusal of a hello because of its token.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Companion refused: token refused.")]
    private static partial void LogTokenRefused(ILogger logger);

    /// <summary>
    /// Logs the refusal of a hello because of its contract version.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="version">The contract version of the first message.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Companion refused: unsupported contract version {Version}.")]
    private static partial void LogUnsupportedVersion(ILogger logger, int version);

    /// <summary>
    /// Logs the end of a Companion's connection.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="accepted">The number of accepted messages.</param>
    /// <param name="refused">The number of refused messages.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Companion disconnected; {Accepted} messages accepted, {Refused} refused.")]
    private static partial void LogCompanionDisconnected(ILogger logger, int accepted, int refused);

    /// <summary>
    /// Logs a message that could not be stored.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception behind the failure.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "A message could not be stored; the connection is closed.")]
    private static partial void LogStoreFailed(ILogger logger, Exception? exception);

    /// <summary>
    /// Reads the hello and answers it.
    /// </summary>
    /// <param name="buffer">The receive buffer</param>
    /// <returns><c>true</c> when the hello was accepted</returns>
    private async Task<bool> RunHelloPhaseAsync(byte[] buffer)
    {
        var (isMessage, length, closeStatus) = await ReadMessageAsync(buffer, MaxHelloBytes).ConfigureAwait(false);

        if (Interlocked.CompareExchange(ref _helloPhase, PhaseDecided, PhaseOpen) != PhaseOpen)
        {
            if (isMessage || closeStatus is not null)
            {
                await DiscardUntilClosedAsync(buffer).ConfigureAwait(false);
            }

            return false;
        }

        _helloTimer?.Dispose();
        LeavePendingHello();

        if (isMessage && Volatile.Read(ref _closing) != 0)
        {
            await DiscardUntilClosedAsync(buffer).ConfigureAwait(false);

            return false;
        }

        if (isMessage == false)
        {
            await CloseAndDiscardAsync(closeStatus, buffer).ConfigureAwait(false);

            return false;
        }

        var read = ContractReader.Read(buffer.AsSpan(0, length));

        if (read.Error is { Kind: ContractErrorKind.UnsupportedVersion, Version: { } version })
        {
            LogUnsupportedVersion(_logger, version);
            await RefuseHelloAsync(ContractHelloRefusal.UnsupportedVersion, version, buffer).ConfigureAwait(false);

            return false;
        }

        if (read.Message is not ContractHello hello)
        {
            await CloseAndDiscardAsync(WebSocketCloseStatus.PolicyViolation, buffer).ConfigureAwait(false);

            return false;
        }

        return await DecideHelloAsync(hello, buffer).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks the token and the place of the Companion and answers the hello.
    /// </summary>
    /// <param name="hello">The hello</param>
    /// <param name="buffer">The receive buffer</param>
    /// <returns><c>true</c> when the hello was accepted</returns>
    private async Task<bool> DecideHelloAsync(ContractHello hello, byte[] buffer)
    {
        if (_access.IsTokenAccepted(hello.Token) == false)
        {
            LogTokenRefused(_logger);
            await RefuseHelloAsync(ContractHelloRefusal.TokenRefused, ContractReader.CurrentVersion, buffer).ConfigureAwait(false);

            return false;
        }

        if (_connections.TryTakeCompanion() == false)
        {
            LogCompanionConnected(_logger);
            await RefuseHelloAsync(ContractHelloRefusal.CompanionConnected, ContractReader.CurrentVersion, buffer).ConfigureAwait(false);

            return false;
        }

        Volatile.Write(ref _placeTaken, 1);

        var stored = await _state.StoreProfileAsync(hello.ProfileId, hello.ProfileName, _stoppingToken).ConfigureAwait(false);

        if (stored.Outcome == ServerStoreOutcome.Failed)
        {
            ReleasePlace();
            LogStoreFailed(_logger, stored.Failure);
            await CloseAndDiscardAsync(WebSocketCloseStatus.InternalServerError, buffer).ConfigureAwait(false);

            return false;
        }

        _helloAccepted = true;
        LogCompanionAccepted(_logger);
        _state.SetCompanionConnected();

        return await SendAsync(new ContractHelloAccepted
                               {
                                   ServerVersion = _serverVersion,
                                   CatalogIdentity = _state.CompleteCatalogIdentity
                               }).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a hello with a refusal and closes the connection.
    /// </summary>
    /// <param name="reason">The reason</param>
    /// <param name="receivedVersion">The contract version of the first message</param>
    /// <param name="buffer">The receive buffer</param>
    /// <returns>A task that completes when the connection is closed</returns>
    private async Task RefuseHelloAsync(ContractHelloRefusal reason, int receivedVersion, byte[] buffer)
    {
        await SendAsync(new ContractHelloRefused
                        {
                            Reason = reason,
                            ReceivedVersion = receivedVersion,
                            SupportedVersions = _supportedVersions
                        }).ConfigureAwait(false);
        await CloseAndDiscardAsync(WebSocketCloseStatus.NormalClosure, buffer).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and answers the messages of the accepted Companion until the connection ends.
    /// </summary>
    /// <returns>A task that completes when the connection ends</returns>
    private async Task RunMessagePhaseAsync()
    {
        var buffer = new byte[ContractReader.MaxMessageBytes + 1];

        while (true)
        {
            var (isMessage, length, closeStatus) = await ReadMessageAsync(buffer, ContractReader.MaxMessageBytes).ConfigureAwait(false);

            if (isMessage == false)
            {
                if (closeStatus is not null)
                {
                    _refusedCount++;
                }

                await CloseAndDiscardAsync(closeStatus, buffer).ConfigureAwait(false);

                return;
            }

            if (Volatile.Read(ref _closing) != 0)
            {
                await DiscardUntilClosedAsync(buffer).ConfigureAwait(false);

                return;
            }

            var status = await HandleMessageAsync(buffer, length).ConfigureAwait(false);

            if (status is not null)
            {
                _refusedCount++;
                await CloseAndDiscardAsync(status, buffer).ConfigureAwait(false);

                return;
            }
        }
    }

    /// <summary>
    /// Decides what a message of the accepted Companion is answered with.
    /// </summary>
    /// <param name="buffer">The buffer that holds the message</param>
    /// <param name="length">The length of the message</param>
    /// <returns>The close status when the connection must be closed; otherwise, <c>null</c></returns>
    private async Task<WebSocketCloseStatus?> HandleMessageAsync(byte[] buffer, int length)
    {
        var read = ContractReader.Read(buffer.AsSpan(0, length));

        if (read.Message is { } message)
        {
            if (message is ContractStatus status)
            {
                _state.SetCompanionPipeState(status.PipeState);
                _acceptedCount++;

                return null;
            }

            if (GetId(message) is not { } id)
            {
                return WebSocketCloseStatus.PolicyViolation;
            }

            var (stored, missingIcons) = await StoreMessageAsync(message, read.CatalogDocument).ConfigureAwait(false);

            if (stored == false)
            {
                return WebSocketCloseStatus.InternalServerError;
            }

            _acceptedCount++;
            await AcknowledgeAsync(id, null, null, missingIcons).ConfigureAwait(false);

            return null;
        }

        var error = read.Error!;

        if (error.Kind == ContractErrorKind.TooLarge)
        {
            return WebSocketCloseStatus.MessageTooBig;
        }

        if (read.RefusedId is { } refusedId && error.Kind is ContractErrorKind.Invalid or ContractErrorKind.CatalogRefused)
        {
            _refusedCount++;
            await AcknowledgeAsync(refusedId, MapRefusal(error), error.CatalogError?.Version, []).ConfigureAwait(false);

            return null;
        }

        return WebSocketCloseStatus.PolicyViolation;
    }

    /// <summary>
    /// Stores an accepted message through the state; session events store nothing.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="catalogDocument">The document of a catalog upload</param>
    /// <returns>Whether the message is stored or needs no storing, and the references of the icons not stored yet</returns>
    private async Task<(bool Stored, IReadOnlyList<string> MissingIcons)> StoreMessageAsync(ContractMessage message, CatalogDocument? catalogDocument)
    {
        ServerStoreResult result;
        IReadOnlyList<string> missingIcons = [];

        switch (message)
        {
            case ContractTick tick:
                {
                    result = await _state.StoreTickAsync(tick, _stoppingToken).ConfigureAwait(false);
                }
                break;

            case ContractIconImage icon:
                {
                    result = await _state.StoreIconAsync(icon, _stoppingToken).ConfigureAwait(false);
                }
                break;

            case ContractCatalogUpload upload when catalogDocument is not null:
                {
                    var catalog = await _state.StoreCatalogAsync(upload, catalogDocument, _stoppingToken).ConfigureAwait(false);

                    result = catalog.Result;
                    missingIcons = catalog.MissingIcons;
                }
                break;

            default:
                {
                    return (true, missingIcons);
                }
        }

        if (result.Outcome != ServerStoreOutcome.Failed)
        {
            return (true, missingIcons);
        }

        LogStoreFailed(_logger, result.Failure);

        return (false, missingIcons);
    }

    /// <summary>
    /// Sends the acknowledgement of a message.
    /// </summary>
    /// <param name="id">The id of the message</param>
    /// <param name="refusal">The refusal, or <c>null</c> when the message was accepted</param>
    /// <param name="refusedVersion">The refused version of a catalog document</param>
    /// <param name="missingIcons">The missing icons</param>
    /// <returns>A task that completes when the acknowledgement is sent</returns>
    private async Task AcknowledgeAsync(Guid id, ContractAcknowledgementRefusal? refusal, int? refusedVersion, IReadOnlyList<string> missingIcons)
    {
        await SendAsync(new ContractAcknowledgement
                        {
                            Id = id,
                            Refusal = refusal,
                            RefusedVersion = refusedVersion,
                            MissingIcons = missingIcons
                        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one message into the buffer, never more than the bound plus one byte.
    /// </summary>
    /// <param name="buffer">The buffer, at least the bound plus one byte long</param>
    /// <param name="bound">The maximum size of the message</param>
    /// <returns>Whether a message was read and its length; otherwise the status to close with, or <c>null</c> when the peer closed</returns>
    private async Task<(bool IsMessage, int Length, WebSocketCloseStatus? CloseStatus)> ReadMessageAsync(byte[] buffer, int bound)
    {
        var length = 0;

        while (true)
        {
            var received = await _webSocket.ReceiveAsync(buffer.AsMemory(length, bound + 1 - length), CancellationToken.None).ConfigureAwait(false);

            if (received.MessageType != WebSocketMessageType.Text)
            {
                return (false, 0, GetNonTextStatus(received.MessageType));
            }

            length += received.Count;

            if (length > bound)
            {
                return (false, 0, WebSocketCloseStatus.MessageTooBig);
            }

            if (received.EndOfMessage)
            {
                return (true, length, null);
            }
        }
    }

    /// <summary>
    /// Receives one frame into the start of the buffer and drops it.
    /// </summary>
    /// <param name="buffer">The receive buffer</param>
    /// <returns><c>true</c> when the frame is the close of the peer</returns>
    private async Task<bool> DiscardFrameAsync(byte[] buffer)
    {
        var result = await _webSocket.ReceiveAsync(buffer.AsMemory(0, DiscardBytes), CancellationToken.None).ConfigureAwait(false);

        return result.MessageType == WebSocketMessageType.Close;
    }

    /// <summary>
    /// Discards what the peer sends until it closes.
    /// </summary>
    /// <param name="buffer">The receive buffer</param>
    /// <returns>A task that completes when the peer has closed</returns>
    private async Task DiscardUntilClosedAsync(byte[] buffer)
    {
        var closed = false;

        while (closed == false)
        {
            closed = await DiscardFrameAsync(buffer).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the connection and, when the peer has not closed yet, waits for its close.
    /// </summary>
    /// <param name="closeStatus">The status to close with, or <c>null</c> when the peer has closed</param>
    /// <param name="buffer">The receive buffer</param>
    /// <returns>A task that completes when the connection is closed</returns>
    private async Task CloseAndDiscardAsync(WebSocketCloseStatus? closeStatus, byte[] buffer)
    {
        if (closeStatus is null)
        {
            await CloseAsync(WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);

            return;
        }

        await CloseAsync(closeStatus.Value).ConfigureAwait(false);
        await DiscardUntilClosedAsync(buffer).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes a message and sends it, unless a close was decided.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns><c>true</c> when the message was sent</returns>
    private async Task<bool> SendAsync(ContractMessage message)
    {
        var written = ContractWriter.Write(message);

        if (written.Error is not null)
        {
            _webSocket.Abort();

            return false;
        }

        await _sendLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            if (Volatile.Read(ref _closing) != 0)
            {
                return false;
            }

            await _webSocket.SendAsync(written.Utf8Json, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Sends a close frame, once, and starts the wait for the close of the peer.
    /// </summary>
    /// <param name="status">The close status</param>
    /// <returns>A task that completes when the close frame is sent</returns>
    private async Task CloseAsync(WebSocketCloseStatus status)
    {
        if (Interlocked.CompareExchange(ref _closing, 1, 0) != 0)
        {
            return;
        }

        _closeWaitTimer = _timeProvider.CreateTimer(static state => ((IngestSession)state!).OnCloseWaitElapsed(), this, CloseWait, Timeout.InfiniteTimeSpan);

        try
        {
            await _sendLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                await _webSocket.CloseOutputAsync(status, string.Empty, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The connection is gone; the receive ends the session.
            _ = exception;
        }
    }

    /// <summary>
    /// Ends the hello phase when its time has run out.
    /// </summary>
    private void OnHelloTimeout()
    {
        if (Interlocked.CompareExchange(ref _helloPhase, PhaseTimedOut, PhaseOpen) != PhaseOpen)
        {
            return;
        }

        LeavePendingHello();
        _timeoutClose = CloseAsync(WebSocketCloseStatus.PolicyViolation);
    }

    /// <summary>
    /// Closes the connection because the server stops.
    /// </summary>
    private void OnStopping()
    {
        _stoppingClose = CloseAsync(WebSocketCloseStatus.EndpointUnavailable);
    }

    /// <summary>
    /// Aborts the socket because the peer did not answer the close in time.
    /// </summary>
    private void OnCloseWaitElapsed()
    {
        _webSocket.Abort();
    }

    /// <summary>
    /// Leaves the pending place, once.
    /// </summary>
    private void LeavePendingHello()
    {
        if (Interlocked.Exchange(ref _pendingLeft, 1) == 0)
        {
            _connections.LeavePendingHello();
        }
    }

    /// <summary>
    /// Releases the place of the Companion, at most once per session.
    /// </summary>
    private void ReleasePlace()
    {
        if (Interlocked.Exchange(ref _placeTaken, 0) == 1)
        {
            if (_helloAccepted)
            {
                _state.SetCompanionDisconnected();
            }

            _connections.ReleaseCompanion();
        }
    }

    /// <summary>
    /// Releases everything the session holds and logs its end.
    /// </summary>
    /// <returns>A task that completes when the session has ended</returns>
    private async Task EndAsync()
    {
        _helloTimer?.Dispose();
        LeavePendingHello();
        _webSocket.Abort();

        await _timeoutClose.ConfigureAwait(false);
        await _stoppingClose.ConfigureAwait(false);

        _closeWaitTimer?.Dispose();

        ReleasePlace();

        if (_helloAccepted)
        {
            LogCompanionDisconnected(_logger, _acceptedCount, _refusedCount);
        }
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _helloTimer?.Dispose();
        _closeWaitTimer?.Dispose();
        _sendLock.Dispose();
    }

    #endregion // IDisposable
}