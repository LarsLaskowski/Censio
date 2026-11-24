using System.Net.WebSockets;

using Censio.Contracts;

namespace Censio.Companion.Core;

/// <summary>
/// One connection to an ingest server: connect, hello, one message in flight, status sends and close. It logs nothing.
/// </summary>
public sealed class IngestClient : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The largest size of a reply of the server in bytes.
    /// </summary>
    public const int MaxReplyBytes = 65536;

    /// <summary>
    /// The largest size of the acknowledgement of a catalog upload in bytes.
    /// </summary>
    public const int MaxCatalogReplyBytes = 1179648;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The time the client waits for the close of the server.
    /// </summary>
    private static readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The connector.
    /// </summary>
    private readonly IReplayServerConnector _connector;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

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
    /// The problem that ended the connection, if any.
    /// </summary>
    private ReplaySendProblemKind? _failure;

    /// <summary>
    /// Whether the client closes the connection.
    /// </summary>
    private bool _isClosing;

    /// <summary>
    /// Whether the client is disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestClient" /> class.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    public IngestClient(IReplayServerConnector connector, TimeProvider timeProvider)
    {
        _connector = connector;
        _timeProvider = timeProvider;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Raised once, synchronously, at the first failure of the connection.
    /// </summary>
    public event EventHandler<ReplaySendProblemKind>? Failed;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the time the client waits for a reply of the server.
    /// </summary>
    public static TimeSpan ReplyTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the problem that ended the connection, or <see langword="null" /> when it did not fail.
    /// </summary>
    public ReplaySendProblemKind? Failure => GetFailure();

    /// <summary>
    /// Gets the identity of the catalog the server holds, as its hello reply named it, or <see langword="null" />.
    /// </summary>
    public string? ServerCatalogIdentity { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Connects to the server and sends the hello.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="hello">The hello</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if the server accepted the hello</returns>
    public async Task<ReplaySendProblemKind?> OpenAsync(Uri address, ContractHello hello, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenCoreAsync(address, hello, cancellationToken).ConfigureAwait(false);
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
    /// Sends a catalog upload and waits for its acknowledgement, which may be larger than other replies.
    /// </summary>
    /// <param name="utf8Json">The bytes of the message</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The acknowledgement, or <c>null</c> if the connection failed, see <see cref="Failure" /></returns>
#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation
    public Task<ContractAcknowledgement?> SendCatalogUploadAsync(ReadOnlyMemory<byte> utf8Json, Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    /// <summary>
    /// Sends a message and waits for its acknowledgement.
    /// </summary>
    /// <param name="utf8Json">The bytes of the message</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The acknowledgement, or <c>null</c> if the connection failed, see <see cref="Failure" /></returns>
    public async Task<ContractAcknowledgement?> SendAsync(ReadOnlyMemory<byte> utf8Json, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await SendCoreAsync(utf8Json, id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbortSocket();

            throw;
        }
        catch (TimeoutException)
        {
            Fail(ReplaySendProblemKind.NoAnswer);

            return null;
        }
        catch (Exception)
        {
            Fail(ReplaySendProblemKind.ConnectionLost);

            return null;
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

        return null;
    }

    /// <summary>
    /// Ends the run with a problem: the first problem is raised and the connection is aborted.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The problem the run ended with</returns>
    internal ReplaySendProblemKind Fail(ReplaySendProblemKind problem)
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
            Failed?.Invoke(this, problem);
        }

        socket?.Abort();
        awaited?.TrySetResult(null);

        return GetFailure() ?? problem;
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
    /// Connects, sends the hello and waits for its answer.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="hello">The hello</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    private async Task<ReplaySendProblemKind?> OpenCoreAsync(Uri address, ContractHello hello, CancellationToken cancellationToken)
    {
        var socket = await _connector.ConnectAsync(address, cancellationToken).ConfigureAwait(false);

        if (socket is null)
        {
            return Fail(ReplaySendProblemKind.ServerUnreachable);
        }

        lock (_stateLock)
        {
            _socket = socket;
        }

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
    /// Writes a message and waits for its acknowledgement.
    /// </summary>
    /// <param name="data">The bytes of the message</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The acknowledgement, or <c>null</c> if the run failed</returns>
    private async Task<ContractAcknowledgement?> SendCoreAsync(ReadOnlyMemory<byte> data, Guid id, CancellationToken cancellationToken)
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

        // Taking the send lock and writing the message are bounded by the reply timeout, like the acknowledgement.
        using (var writeTimeout = new CancellationTokenSource(ReplyTimeout, _timeProvider))
            using (var writeLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeTimeout.Token))
            {
                try
                {
                    await _sendLock.WaitAsync(writeLinked.Token).ConfigureAwait(false);

                    try
                    {
                        if (GetFailure() is not null)
                        {
                            return null;
                        }

                        await socket.SendAsync(data, WebSocketMessageType.Text, true, writeLinked.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _sendLock.Release();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false && writeTimeout.IsCancellationRequested)
                {
                    throw new TimeoutException("The server did not take the message in time.");
                }
            }

        return await awaited.Task.WaitAsync(ReplyTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
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
            // The client is disposed.
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