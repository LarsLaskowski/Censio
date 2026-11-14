using System.Net.WebSockets;

using Censio.Companion.Core;

using Microsoft.AspNetCore.TestHost;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="IReplayServerConnector" /> that connects to the server of a test host through its WebSocket client, so a
/// replay is sent into the real ingest endpoint in memory.
/// </summary>
public sealed class TestServerReplayConnector : IReplayServerConnector
{
    #region Fields

    /// <summary>
    /// The WebSocket client of the test server.
    /// </summary>
    private readonly WebSocketClient _client;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TestServerReplayConnector" /> class.
    /// </summary>
    /// <param name="server">The test server</param>
    public TestServerReplayConnector(TestServer server)
    {
        _client = server.CreateWebSocketClient();
    }

    #endregion // Constructors

    #region IReplayServerConnector

    /// <inheritdoc />
    public async Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.ConnectAsync(address, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    #endregion // IReplayServerConnector
}