using System.Net.WebSockets;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// An open <see cref="WebSocket" /> whose every send and receive throws a given exception, for a failure no catch list names.
/// </summary>
public sealed class ThrowingWebSocket : WebSocket
{
    #region Fields

    /// <summary>
    /// The exception every send and receive throws.
    /// </summary>
    private readonly Exception _exception;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrowingWebSocket" /> class.
    /// </summary>
    /// <param name="exception">The exception every send and receive throws</param>
    public ThrowingWebSocket(Exception exception)
    {
        _exception = exception;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of sends and receives that were tried.
    /// </summary>
    public int Calls { get; private set; }

    #endregion // Properties

    #region WebSocket

    /// <inheritdoc />
    public override WebSocketCloseStatus? CloseStatus => null;

    /// <inheritdoc />
    public override string? CloseStatusDescription => null;

    /// <inheritdoc />
    public override WebSocketState State => WebSocketState.Open;

    /// <inheritdoc />
    public override string? SubProtocol => null;

    /// <inheritdoc />
    public override void Abort()
    {
    }

    /// <inheritdoc />
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
    }

    /// <inheritdoc />
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        Calls++;

        throw _exception;
    }

    /// <inheritdoc />
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        Calls++;

        throw _exception;
    }

    #endregion // WebSocket
}