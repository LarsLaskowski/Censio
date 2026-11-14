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
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly TimeProvider _timeProvider;
#pragma warning restore S4487 // Skeleton: removed with the implementation

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
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // IReplayServerConnector
}