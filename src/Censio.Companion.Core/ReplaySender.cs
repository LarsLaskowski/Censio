using System.Globalization;
using System.Net.WebSockets;

using Censio.Contracts;
using Censio.Pipe;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Sends a replayed recording to a server over one connection.
/// </summary>
public sealed class ReplaySender : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The largest size of a reply of the server in bytes.
    /// </summary>
    public const int MaxReplyBytes = 65536;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The time the sender waits for the close of the server.
    /// </summary>
    private static readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The address of the server.
    /// </summary>
    private readonly Uri _address;

    /// <summary>
    /// The path of the token file, if any.
    /// </summary>
    private readonly string? _tokenFilePath;

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
    /// The lock of the state that the receive loop and the sends share.
    /// </summary>
    private readonly Lock _stateLock = new();

    /// <summary>
    /// The lock that lets one message at a time be written to the socket.
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>
    /// The cancellation of the receive loop and the status sends.
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>
    /// The token read from the token file, if any.
    /// </summary>
    private string? _token;

    /// <summary>
    /// The socket, once connected.
    /// </summary>
    private WebSocket? _socket;

    /// <summary>
    /// The receive loop.
    /// </summary>
    private Task _receiveLoop = Task.CompletedTask;

    /// <summary>
    /// The last status send; it never faults.
    /// </summary>
    private Task _statusSend = Task.CompletedTask;

    /// <summary>
    /// The last state that was passed on.
    /// </summary>
    private PipeConnectionState? _lastState;

    /// <summary>
    /// The identity of the message whose acknowledgement is awaited.
    /// </summary>
    private Guid _awaitedId;

    /// <summary>
    /// The acknowledgement that is awaited, if any.
    /// </summary>
    private TaskCompletionSource<ContractAcknowledgement?>? _awaited;

    /// <summary>
    /// The problem that ended the run, if any.
    /// </summary>
    private ReplaySendProblemKind? _failure;

    /// <summary>
    /// The number of ticks sent.
    /// </summary>
    private int _ticks;

    /// <summary>
    /// The number of messages refused.
    /// </summary>
    private int _refused;

    /// <summary>
    /// Whether the sender closes the connection.
    /// </summary>
    private bool _isClosing;

    /// <summary>
    /// Whether the sender is disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySender" /> class.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="tokenFilePath">The path of the token file, if any</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public ReplaySender(Uri address, string? tokenFilePath, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        _address = address;
        _tokenFilePath = tokenFilePath;
        _companionVersion = companionVersion;
        _connector = connector;
        _timeProvider = timeProvider;
        _logger = logger;

        ProfileId = Guid.NewGuid();
        ProfileName = "Replay " + timeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the time the sender waits for a reply of the server.
    /// </summary>
    public static TimeSpan ReplyTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the identity of the profile the replay is sent to.
    /// </summary>
    public Guid ProfileId { get; }

    /// <summary>
    /// Gets the name of the profile the replay is sent to.
    /// </summary>
    public string ProfileName { get; }

    /// <summary>
    /// Gets the counts of what was sent.
    /// </summary>
    public ReplaySendCounts Counts
    {
        get
        {
            lock (_stateLock)
            {
                return new ReplaySendCounts
                       {
                           Ticks = _ticks,
                           Refused = _refused
                       };
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Reads the token file, if one is named.
    /// </summary>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public ReplaySendProblemKind? Prepare()
    {
        if (_tokenFilePath is null)
        {
            return null;
        }

        var result = ReplayTokenFile.ReadFile(_tokenFilePath);

        if (result.Kind == ReplayTokenFileResultKind.Read)
        {
            _token = result.Token;

            return null;
        }

        var problem = result.Kind == ReplayTokenFileResultKind.CannotBeRead
                          ? ReplaySendProblemKind.TokenFileCannotBeRead
                          : ReplaySendProblemKind.TokenFileHoldsNoValidToken;

        CompanionLog.ReplaySendFailed(_logger, FormatReason(problem));

        return problem;
    }

    /// <summary>
    /// Connects to the server and sends the hello.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> ConnectServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbortSocket();

            throw;
        }
        catch (Exception)
        {
            return Fail(ReplaySendProblemKind.ConnectionLost);
        }
    }

    /// <summary>
    /// Passes a change of the connection state on to the server.
    /// </summary>
    /// <param name="state">The state</param>
    public void NotifyState(PipeConnectionState state)
    {
        Task previous;

        lock (_stateLock)
        {
            if (_socket is null || _failure is not null || _isClosing || _isDisposed || _lastState == state)
            {
                return;
            }

            _lastState = state;
            previous = _statusSend;
            _statusSend = Task.Run(() => SendStatusAsync(previous, state), CancellationToken.None);
        }
    }

    /// <summary>
    /// Sends a tick and waits for the acknowledgement.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendTickAsync(PipeTick tick, CancellationToken cancellationToken)
    {
        if (tick.Islands.Count == 0)
        {
            return null;
        }

        var message = ContractMessageConverter.ToTick(tick, ProfileId);

        return await SendMessageAsync(message, message.Id, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the start of a session and waits for the acknowledgement.
    /// </summary>
    /// <param name="receiveTime">The time the start was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendSessionStartAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        var message = ContractMessageConverter.ToSessionStart(ProfileId, receiveTime);

        return await SendMessageAsync(message, message.Id, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the end of a session and waits for the acknowledgement.
    /// </summary>
    /// <param name="receiveTime">The time the end was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendSessionEndAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        var message = ContractMessageConverter.ToSessionEnd(ProfileId, receiveTime);

        return await SendMessageAsync(message, message.Id, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> CloseAsync(CancellationToken cancellationToken)
    {
        WebSocket? socket;
        Task receiveLoop;

        lock (_stateLock)
        {
            socket = _socket;
            receiveLoop = _receiveLoop;
        }

        await _statusSend.ConfigureAwait(false);

        lock (_stateLock)
        {
            if (socket is null || _failure is not null)
            {
                return _failure;
            }

            _isClosing = true;
        }

        try
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
            await WaitForCloseAsync(receiveLoop, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbortSocket();

            throw;
        }
        catch (Exception)
        {
            return Fail(ReplaySendProblemKind.ConnectionLost);
        }

        var counts = Counts;

        CompanionLog.ReplaySendFinished(_logger, counts.Ticks, counts.Refused);

        return null;
    }

    /// <summary>
    /// Reads one message of the server, at most <see cref="MaxReplyBytes" /> bytes long; it never throws.
    /// </summary>
    /// <param name="socket">The socket</param>
    /// <param name="buffer">The buffer of <see cref="MaxReplyBytes" /> plus one byte</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The message or the problem</returns>
    private static async Task<(ContractMessage? Message, ReplaySendProblemKind? Problem)> ReadMessageAsync(WebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            var length = 0;
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return (null, ReplaySendProblemKind.ConnectionLost);
                }

                if (result.MessageType != WebSocketMessageType.Text || (result.EndOfMessage == false && length + result.Count >= MaxReplyBytes))
                {
                    return (null, ReplaySendProblemKind.InvalidReply);
                }

                length += result.Count;
            }
            while (result.EndOfMessage == false);

            if (length > MaxReplyBytes)
            {
                return (null, ReplaySendProblemKind.InvalidReply);
            }

            var read = ContractReader.Read(buffer.AsSpan(0, length));

            return read.Message is null
                       ? (null, ReplaySendProblemKind.InvalidReply)
                       : (read.Message, null);
        }
        catch (Exception)
        {
            return (null, ReplaySendProblemKind.ConnectionLost);
        }
    }

    /// <summary>
    /// Waits for the receive of an unfinished read to end, so that its exception is observed; the socket is aborted when
    /// the read does not end.
    /// </summary>
    /// <param name="receiving">The read</param>
    /// <param name="socket">The socket</param>
    /// <returns>A task that completes when the read ended</returns>
    private static async Task ObserveAsync(Task receiving, WebSocket socket)
    {
        if (receiving.IsCompleted == false)
        {
            socket.Abort();
        }

        try
        {
            await receiving.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The read ended with the abort of the socket; the problem is the one of the caller.
        }
    }

    /// <summary>
    /// Gives the reason of a problem as the tray shows it.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The reason</returns>
    private static string FormatReason(ReplaySendProblemKind problem)
    {
        return problem switch
               {
                   ReplaySendProblemKind.TokenFileCannotBeRead => "token file cannot be read",
                   ReplaySendProblemKind.TokenFileHoldsNoValidToken => "token file holds no valid token",
                   ReplaySendProblemKind.ServerUnreachable => "server unreachable",
                   ReplaySendProblemKind.TokenRefused => "token refused",
                   ReplaySendProblemKind.CompanionConnected => "another Companion is connected",
                   ReplaySendProblemKind.VersionNotSupported => "server does not support contract version 1",
                   ReplaySendProblemKind.NoAnswer => "server does not answer",
                   ReplaySendProblemKind.ConnectionLost => "server connection lost",
                   _ => "invalid server reply"
               };
    }

    /// <summary>
    /// Connects, sends the hello and waits for its answer.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    private async Task<ReplaySendProblemKind?> ConnectCoreAsync(CancellationToken cancellationToken)
    {
        var socket = await _connector.ConnectAsync(_address, cancellationToken).ConfigureAwait(false);

        if (socket is null)
        {
            return Fail(ReplaySendProblemKind.ServerUnreachable);
        }

        lock (_stateLock)
        {
            _socket = socket;
        }

        var hello = new ContractHello
                    {
                        Token = _token,
                        CompanionVersion = _companionVersion,
                        ProfileId = ProfileId,
                        ProfileName = ProfileName
                    };
        var written = ContractWriter.Write(hello);

        if (written.Error is not null)
        {
            return Fail(ReplaySendProblemKind.ConnectionLost);
        }

        await socket.SendAsync(written.Utf8Json, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);

        var receiving = ReadMessageAsync(socket, new byte[MaxReplyBytes + 1], _lifetime.Token);
        (ContractMessage? Message, ReplaySendProblemKind? Problem) outcome;

        try
        {
            outcome = await receiving.WaitAsync(ReplyTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return Fail(ReplaySendProblemKind.NoAnswer);
        }
        finally
        {
            await ObserveAsync(receiving, socket).ConfigureAwait(false);
        }

        return HandleHelloReply(socket, outcome);
    }

    /// <summary>
    /// Handles the first message of the server.
    /// </summary>
    /// <param name="socket">The socket</param>
    /// <param name="outcome">The outcome of reading the message</param>
    /// <returns>The problem, or <c>null</c> if the hello is accepted</returns>
    private ReplaySendProblemKind? HandleHelloReply(WebSocket socket, (ContractMessage? Message, ReplaySendProblemKind? Problem) outcome)
    {
        if (outcome.Problem is not null)
        {
            return Fail(outcome.Problem.Value);
        }

        switch (outcome.Message)
        {
            case ContractHelloAccepted:
                {
                    CompanionLog.ReplaySendConnected(_logger);
                    _receiveLoop = Task.Run(() => ReceiveLoopAsync(socket, _lifetime.Token), CancellationToken.None);

                    return null;
                }

            case ContractHelloRefused refused:
                {
                    return Fail(refused.Reason switch
                                {
                                    ContractHelloRefusal.TokenRefused => ReplaySendProblemKind.TokenRefused,
                                    ContractHelloRefusal.CompanionConnected => ReplaySendProblemKind.CompanionConnected,
                                    _ => ReplaySendProblemKind.VersionNotSupported
                                });
                }

            default:
                {
                    return Fail(ReplaySendProblemKind.InvalidReply);
                }
        }
    }

    /// <summary>
    /// Waits at most the close timeout for the server's close, then aborts the socket.
    /// </summary>
    /// <param name="receiveLoop">The receive loop</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the connection ended</returns>
    private async Task WaitForCloseAsync(Task receiveLoop, CancellationToken cancellationToken)
    {
        try
        {
            await receiveLoop.WaitAsync(_closeTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            AbortSocket();
        }
    }

    /// <summary>
    /// Sends a message and waits for its acknowledgement.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="isTick">Whether the message is a tick, which is counted when it is accepted</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    private async Task<ReplaySendProblemKind?> SendMessageAsync(ContractMessage message, Guid id, bool isTick, CancellationToken cancellationToken)
    {
        var written = ContractWriter.Write(message);

        if (written.Error is not null)
        {
            CountRefused();

            return null;
        }

        try
        {
            var acknowledgement = await SendAndAwaitAsync(written.Utf8Json, id, cancellationToken).ConfigureAwait(false);

            if (acknowledgement is null)
            {
                return GetFailure();
            }

            Count(acknowledgement.Refusal is not null, isTick);

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbortSocket();

            throw;
        }
        catch (TimeoutException)
        {
            return Fail(ReplaySendProblemKind.NoAnswer);
        }
        catch (Exception)
        {
            return Fail(ReplaySendProblemKind.ConnectionLost);
        }
        finally
        {
            lock (_stateLock)
            {
                _awaited = null;
            }
        }
    }

    /// <summary>
    /// Writes a message and waits for its acknowledgement.
    /// </summary>
    /// <param name="data">The bytes of the message</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The acknowledgement, or <c>null</c> if the run failed</returns>
    private async Task<ContractAcknowledgement?> SendAndAwaitAsync(ReadOnlyMemory<byte> data, Guid id, CancellationToken cancellationToken)
    {
        var awaited = new TaskCompletionSource<ContractAcknowledgement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        WebSocket socket;

        lock (_stateLock)
        {
            if (_socket is null || _failure is not null)
            {
                return null;
            }

            socket = _socket;
            _awaitedId = id;
            _awaited = awaited;
        }

        // A frame of the server that arrived before this send is read by the receive loop first.
        await Task.Yield();

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (GetFailure() is not null)
            {
                return null;
            }

            await socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }

        return await awaited.Task.WaitAsync(ReplyTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the result of a message.
    /// </summary>
    /// <param name="isRefused">Whether the server refused the message</param>
    /// <param name="isTick">Whether the message is a tick</param>
    private void Count(bool isRefused, bool isTick)
    {
        lock (_stateLock)
        {
            if (isRefused)
            {
                _refused++;
            }
            else if (isTick)
            {
                _ticks++;
            }
        }
    }

    /// <summary>
    /// Counts a message that was refused before it was sent.
    /// </summary>
    private void CountRefused()
    {
        Count(true, false);
    }

    /// <summary>
    /// Sends the status of a state after the earlier status sends; it never throws.
    /// </summary>
    /// <param name="previous">The earlier status send</param>
    /// <param name="state">The state</param>
    /// <returns>A task that completes when the status is sent or the run failed</returns>
    private async Task SendStatusAsync(Task previous, PipeConnectionState state)
    {
        await previous.ConfigureAwait(false);

        try
        {
            var written = ContractWriter.Write(ContractMessageConverter.ToStatus(state));
            WebSocket? socket;

            lock (_stateLock)
            {
                socket = _failure is null ? _socket : null;
            }

            if (socket is null || written.Error is not null)
            {
                return;
            }

            await _sendLock.WaitAsync(_lifetime.Token).ConfigureAwait(false);

            try
            {
                await socket.SendAsync(written.Utf8Json, WebSocketMessageType.Text, true, _lifetime.Token).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The sender is disposed.
        }
        catch (Exception)
        {
            Fail(ReplaySendProblemKind.ConnectionLost);
        }
    }

    /// <summary>
    /// Reads the messages of the server from the hello's acceptance to the end of the connection.
    /// </summary>
    /// <param name="socket">The socket</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the connection ended</returns>
    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxReplyBytes + 1];

        while (cancellationToken.IsCancellationRequested == false)
        {
            var outcome = await ReadMessageAsync(socket, buffer, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested || IsClosing())
            {
                return;
            }

            var problem = outcome.Problem ?? Deliver(outcome.Message);

            if (problem is not null)
            {
                Fail(problem.Value);

                return;
            }
        }
    }

    /// <summary>
    /// Hands an acknowledgement to the send that awaits it.
    /// </summary>
    /// <param name="message">The message of the server</param>
    /// <returns>The problem, or <c>null</c> if the message is the awaited acknowledgement</returns>
    private ReplaySendProblemKind? Deliver(ContractMessage? message)
    {
        lock (_stateLock)
        {
            if (message is not ContractAcknowledgement acknowledgement || _awaited is null || acknowledgement.Id != _awaitedId)
            {
                return ReplaySendProblemKind.InvalidReply;
            }

            var awaited = _awaited;

            _awaited = null;
            awaited.TrySetResult(acknowledgement);

            return null;
        }
    }

    /// <summary>
    /// Ends the run with a problem: the first problem is logged and the connection is aborted.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The problem the run ended with</returns>
    private ReplaySendProblemKind Fail(ReplaySendProblemKind problem)
    {
        WebSocket? socket;
        TaskCompletionSource<ContractAcknowledgement?>? awaited;
        bool isFirst;

        lock (_stateLock)
        {
            isFirst = _failure is null;
            _failure ??= problem;
            socket = _socket;
            awaited = _awaited;
            _awaited = null;
        }

        if (isFirst)
        {
            LogFailure(problem);
        }

        socket?.Abort();
        awaited?.TrySetResult(null);

        return GetFailure() ?? problem;
    }

    /// <summary>
    /// Logs a problem with its reason.
    /// </summary>
    /// <param name="problem">The problem</param>
    private void LogFailure(ReplaySendProblemKind problem)
    {
        if (problem is ReplaySendProblemKind.TokenRefused or ReplaySendProblemKind.CompanionConnected or ReplaySendProblemKind.VersionNotSupported)
        {
            CompanionLog.ReplaySendRefused(_logger, FormatReason(problem));
        }
        else
        {
            CompanionLog.ReplaySendFailed(_logger, FormatReason(problem));
        }
    }

    /// <summary>
    /// Gets the problem that ended the run.
    /// </summary>
    /// <returns>The problem, or <c>null</c> if the run did not fail</returns>
    private ReplaySendProblemKind? GetFailure()
    {
        lock (_stateLock)
        {
            return _failure;
        }
    }

    /// <summary>
    /// Checks whether the sender closes the connection.
    /// </summary>
    /// <returns><c>true</c> if the close was started</returns>
    private bool IsClosing()
    {
        lock (_stateLock)
        {
            return _isClosing;
        }
    }

    /// <summary>
    /// Aborts the socket, if there is one.
    /// </summary>
    private void AbortSocket()
    {
        WebSocket? socket;

        lock (_stateLock)
        {
            socket = _socket;
        }

        socket?.Abort();
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        WebSocket? socket;
        Task receiveLoop;

        lock (_stateLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            socket = _socket;
            receiveLoop = _receiveLoop;
        }

        await _lifetime.CancelAsync().ConfigureAwait(false);

        socket?.Abort();

        await _statusSend.ConfigureAwait(false);
        await receiveLoop.ConfigureAwait(false);

        socket?.Dispose();
        _lifetime.Dispose();
        _sendLock.Dispose();
    }

    #endregion // IAsyncDisposable
}