using System.Net.WebSockets;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// A <see cref="WebSocket" /> that passes everything to another socket until its sends are made to fail, as a connection
/// whose writes break while its reads still work.
/// </summary>
public sealed class SendFailingWebSocket : WebSocket
{
    #region Fields

    /// <summary>
    /// The socket everything is passed to.
    /// </summary>
    private readonly WebSocket _inner;

    /// <summary>
    /// Completed when the first send failed.
    /// </summary>
    private readonly TaskCompletionSource _failedSend = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when a send is held.
    /// </summary>
    private readonly TaskCompletionSource _heldSend = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when the held sends are released.
    /// </summary>
    private readonly TaskCompletionSource _releasedSends = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Whether sends fail.
    /// </summary>
    private volatile bool _isFailingSends;

    /// <summary>
    /// Whether a failing send is held until it is released or cancelled.
    /// </summary>
    private volatile bool _isHoldingSends;

    /// <summary>
    /// The number of sends that failed or were held.
    /// </summary>
    private int _failedSendCount;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SendFailingWebSocket" /> class.
    /// </summary>
    /// <param name="inner">The socket everything is passed to</param>
    public SendFailingWebSocket(WebSocket inner)
    {
        _inner = inner;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a task that completes when the first send failed.
    /// </summary>
    public Task FailedSend => _failedSend.Task;

    /// <summary>
    /// Gets a task that completes when the first send was held.
    /// </summary>
    public Task HeldSend => _heldSend.Task;

    /// <summary>
    /// Gets the number of message sends that failed or were held.
    /// </summary>
    public int FailedSendCount => Volatile.Read(ref _failedSendCount);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Makes every following send, the close frame included, fail with an <see cref="IOException" />.
    /// </summary>
    public void FailSends()
    {
        _isFailingSends = true;
    }

    /// <summary>
    /// Makes every following message send wait until <see cref="ReleaseSends" /> and then fail with an
    /// <see cref="IOException" />, or end with the cancellation of its token, as a write that hangs and then breaks.
    /// </summary>
    public void HoldSends()
    {
        _isHoldingSends = true;
        _isFailingSends = true;
    }

    /// <summary>
    /// Lets the held sends fail.
    /// </summary>
    public void ReleaseSends()
    {
        _releasedSends.TrySetResult();
    }

    /// <summary>
    /// Holds a send until it is released or its token is cancelled, then fails it.
    /// </summary>
    /// <param name="cancellationToken">The token of the send</param>
    /// <returns>A task that fails when the send is released or cancelled</returns>
    /// <exception cref="IOException">The send was released</exception>
    private async ValueTask HoldAsync(CancellationToken cancellationToken)
    {
        _heldSend.TrySetResult();

        await _releasedSends.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        throw new IOException("The connection's writes failed.");
    }

    #endregion // Methods

    #region WebSocket

    /// <inheritdoc />
    public override WebSocketCloseStatus? CloseStatus => _inner.CloseStatus;

    /// <inheritdoc />
    public override string? CloseStatusDescription => _inner.CloseStatusDescription;

    /// <inheritdoc />
    public override WebSocketState State => _inner.State;

    /// <inheritdoc />
    public override string? SubProtocol => _inner.SubProtocol;

    /// <inheritdoc />
    public override void Abort()
    {
        _inner.Abort();
    }

    /// <inheritdoc />
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        return _inner.CloseAsync(closeStatus, statusDescription, cancellationToken);
    }

    /// <inheritdoc />
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        if (_isFailingSends)
        {
            _failedSend.TrySetResult();

            return Task.FromException(new IOException("The connection's writes failed."));
        }

        return _inner.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _inner.Dispose();
    }

    /// <inheritdoc />
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        return _inner.ReceiveAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _inner.ReceiveAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        return SendAsync(buffer.AsMemory(), messageType, endOfMessage, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        if (_isFailingSends)
        {
            Interlocked.Increment(ref _failedSendCount);
        }

        if (_isHoldingSends)
        {
            return HoldAsync(cancellationToken);
        }

        if (_isFailingSends)
        {
            _failedSend.TrySetResult();

            return ValueTask.FromException(new IOException("The connection's writes failed."));
        }

        return _inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
    }

    #endregion // WebSocket
}