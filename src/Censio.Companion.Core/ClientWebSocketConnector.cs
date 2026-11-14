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