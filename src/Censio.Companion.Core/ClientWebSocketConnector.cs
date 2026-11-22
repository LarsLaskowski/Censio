using System.Net.WebSockets;

namespace Censio.Companion.Core;

/// <summary>
/// Opens the connection of a replay to a server with a client web socket.
/// </summary>
public sealed class ClientWebSocketConnector : IReplayServerConnector
{
    #region Fields

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The interval of the keep-alive pings, or <see langword="null" /> for the default of the socket.
    /// </summary>
    private readonly TimeSpan? _keepAliveInterval;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The time the socket waits for the answer to a ping, or <see langword="null" /> for the default of the socket.
    /// </summary>
    private readonly TimeSpan? _keepAliveTimeout;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientWebSocketConnector" /> class.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    public ClientWebSocketConnector(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientWebSocketConnector" /> class.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="keepAliveInterval">The interval of the keep-alive pings</param>
    /// <param name="keepAliveTimeout">The time the socket waits for the answer to a ping</param>
    public ClientWebSocketConnector(TimeProvider timeProvider, TimeSpan keepAliveInterval, TimeSpan keepAliveTimeout)
    {
        _timeProvider = timeProvider;
        _keepAliveInterval = keepAliveInterval;
        _keepAliveTimeout = keepAliveTimeout;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the time the connection may take to open.
    /// </summary>
    public static TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(30);

    #endregion // Properties

    #region IReplayServerConnector

    /// <inheritdoc />
    public async Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();

        try
        {
            using var handler = new SocketsHttpHandler
                                {
                                    UseProxy = false,
                                    AllowAutoRedirect = false
                                };
            using var invoker = new HttpMessageInvoker(handler);
            using var timeout = new CancellationTokenSource(ConnectTimeout, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            await socket.ConnectAsync(address, invoker, linked.Token).ConfigureAwait(false);

            return socket;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || cancellationToken.IsCancellationRequested == false)
        {
            socket.Dispose();

            return null;
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();

            throw;
        }
    }

    #endregion // IReplayServerConnector
}