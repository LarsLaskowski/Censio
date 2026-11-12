using System.Net.WebSockets;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="IHttpWebSocketFeature" /> that marks every request as a WebSocket upgrade and fails every accept, as a
/// connection lost during the handshake does; it counts the accepts.
/// </summary>
public sealed class FailingWebSocketFeature : IHttpWebSocketFeature
{
    #region Fields

    /// <summary>
    /// The number of accepts so far.
    /// </summary>
    private int _acceptCount;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the number of accepts so far.
    /// </summary>
    public int AcceptCount => Volatile.Read(ref _acceptCount);

    #endregion // Properties

    #region IHttpWebSocketFeature

    /// <inheritdoc />
    public bool IsWebSocketRequest => true;

    /// <inheritdoc />
    public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
    {
        Interlocked.Increment(ref _acceptCount);

        return Task.FromException<WebSocket>(new WebSocketException(WebSocketError.ConnectionClosedPrematurely));
    }

    #endregion // IHttpWebSocketFeature
}