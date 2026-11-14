using System.Net.WebSockets;

namespace Censio.Companion.Core;

/// <summary>
/// Opens the connection of a replay to a server.
/// </summary>
public interface IReplayServerConnector
{
    #region Methods

    /// <summary>
    /// Connects to a server.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The open web socket, or <c>null</c> if the connection could not be opened</returns>
    Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken);

    #endregion // Methods
}