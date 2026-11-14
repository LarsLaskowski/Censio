using System.Net.WebSockets;

using Censio.Pipe.Tests;

using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="ClientWebSocketConnector" />.
/// </summary>
[TestClass]
public sealed class ClientWebSocketConnectorTests
{
    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientWebSocketConnectorTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ClientWebSocketConnectorTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// An address that answers with HTTP instead of an upgrade gives no socket.
    /// </summary>
    /// <param name="path">The path that answers with HTTP</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(LoopbackWebSocketHost.MissingPath)]
    [DataRow(LoopbackWebSocketHost.PagePath)]
    public async Task ClientWebSocketConnectorHttpAnswerGivesNoSocket(string path)
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());

        // Act
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(path, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(socket, $"The HTTP answer of {path} should give no socket");
    }

    /// <summary>
    /// A redirect is not followed and gives no socket.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorRedirectGivesNoSocket()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());

        // Act
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.RedirectPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(socket, "A redirect should give no socket");
        Assert.IsEmpty(host.EndpointRequests, "The redirect's target should not be requested");
    }

    /// <summary>
    /// A certificate the operating system does not trust gives no socket.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorUntrustedCertificateGivesNoSocket()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());

        // Act
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.IngestPath, true), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(socket, "A self-signed certificate should give no socket");
        Assert.IsEmpty(host.EndpointRequests, "No request should cross the untrusted connection");
    }

    /// <summary>
    /// The upgrade carries no Origin header and no subprotocol, and gives an open socket.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorSendsNoOrigin()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());

        // Act
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.IngestPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(socket, "The WebSocket endpoint should give a socket");
        Assert.AreEqual(WebSocketState.Open, socket.State, "The socket should be open");

        var request = Assert.ContainsSingle(host.EndpointRequests, "The endpoint should receive one upgrade");

        Assert.DoesNotContain(key => string.Equals(key, HeaderNames.Origin, StringComparison.OrdinalIgnoreCase), request.Keys, "The upgrade should carry no Origin header");
        Assert.DoesNotContain(key => string.Equals(key, HeaderNames.SecWebSocketProtocol, StringComparison.OrdinalIgnoreCase), request.Keys, "The upgrade should ask for no subprotocol");
    }

    /// <summary>
    /// A configured system proxy, also one that does not bypass loopback addresses, is not used.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorBypassesSystemProxy()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());
        var systemProxy = HttpClient.DefaultProxy;
        WebSocket? socket;

        HttpClient.DefaultProxy = new LoopbackWebProxy(host.ProxyAddress);

        // Act
        try
        {
            socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.IngestPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }
        finally
        {
            HttpClient.DefaultProxy = systemProxy;
        }

        using var connected = socket;

        // Assert
        Assert.IsNotNull(connected, "The connection should go to the server directly");
        Assert.AreEqual(0, host.ProxyConnections, "The system proxy should receive no connection");
        Assert.HasCount(1, host.EndpointRequests, "The endpoint should receive the upgrade itself");
    }

    /// <summary>
    /// An upgrade without an answer gives no socket after the connect timeout of the time provider, not before.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorNoUpgradeWithinTimeoutGivesNoSocket()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var timeProvider = new RecordingTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var connector = new ClientWebSocketConnector(timeProvider);

        // Act
        var connecting = connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.HeldPath, false), TestContext.CancellationToken);

        await host.HeldRequestReceived.WaitAsync(_guard, TestContext.CancellationToken);
        await timeProvider.WaitForDelayAsync(ClientWebSocketConnector.ConnectTimeout, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        timeProvider.Advance(ClientWebSocketConnector.ConnectTimeout - TimeSpan.FromTicks(1));

        var isCompletedBefore = connecting.IsCompleted;

        timeProvider.Advance(TimeSpan.FromTicks(1));

        using var socket = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(isCompletedBefore, "The connection should still wait before the timeout");
        Assert.IsNull(socket, "An upgrade without an answer should give no socket");
        Assert.AreEqual(TimeSpan.FromSeconds(30), ClientWebSocketConnector.ConnectTimeout, "The connect timeout should be 30 seconds");
    }

    #endregion // Methods
}