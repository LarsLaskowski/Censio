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
    /// Whether sends fail.
    /// </summary>
    private volatile bool _isFailingSends;

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

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Makes every following send, the close frame included, fail with an <see cref="IOException" />.
    /// </summary>
    public void FailSends()
    {
        _isFailingSends = true;
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
            _failedSend.TrySetResult();

            return ValueTask.FromException(new IOException("The connection's writes failed."));
        }

        return _inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
    }

    #endregion // WebSocket
}