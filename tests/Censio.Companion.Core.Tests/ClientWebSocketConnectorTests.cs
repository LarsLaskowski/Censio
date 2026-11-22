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

    /// <summary>
    /// The message the echo test sends.
    /// </summary>
    private static readonly byte[] _message = "replay message"u8.ToArray();

    /// <summary>
    /// The real-time limit of the keep-alive test, which waits on the socket's own real-clock timers.
    /// </summary>
    private static readonly TimeSpan _hangGuard = TimeSpan.FromSeconds(30);

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
    /// The rule holds for the replay's connector and for the live sender's connector with keep-alive (feature 40).
    /// </summary>
    /// <param name="isKeepAlive">Whether the connector is built with the keep-alive of the live sender</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false, DisplayName = "Without keep-alive")]
    [DataRow(true, DisplayName = "With keep-alive")]
    public async Task ClientWebSocketConnectorRedirectGivesNoSocket(bool isKeepAlive)
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = CreateConnector(isKeepAlive);

        // Act
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.RedirectPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(socket, "A redirect should give no socket");
        Assert.IsEmpty(host.EndpointRequests, "The redirect's target should not be requested");
    }

    /// <summary>
    /// A certificate the operating system does not trust gives no socket.
    /// The rule holds for the replay's connector and for the live sender's connector with keep-alive (feature 40).
    /// </summary>
    /// <param name="isKeepAlive">Whether the connector is built with the keep-alive of the live sender</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false, DisplayName = "Without keep-alive")]
    [DataRow(true, DisplayName = "With keep-alive")]
    public async Task ClientWebSocketConnectorUntrustedCertificateGivesNoSocket(bool isKeepAlive)
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = CreateConnector(isKeepAlive);

        // Act
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.IngestPath, true), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(socket, "A self-signed certificate should give no socket");
        Assert.IsEmpty(host.EndpointRequests, "No request should cross the untrusted connection");
    }

    /// <summary>
    /// The upgrade carries no Origin header and no subprotocol, and gives an open socket.
    /// The rule holds for the replay's connector and for the live sender's connector with keep-alive (feature 40).
    /// </summary>
    /// <param name="isKeepAlive">Whether the connector is built with the keep-alive of the live sender</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false, DisplayName = "Without keep-alive")]
    [DataRow(true, DisplayName = "With keep-alive")]
    public async Task ClientWebSocketConnectorSendsNoOrigin(bool isKeepAlive)
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = CreateConnector(isKeepAlive);

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
    /// The rule holds for the replay's connector and for the live sender's connector with keep-alive (feature 40).
    /// </summary>
    /// <param name="isKeepAlive">Whether the connector is built with the keep-alive of the live sender</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false, DisplayName = "Without keep-alive")]
    [DataRow(true, DisplayName = "With keep-alive")]
    public async Task ClientWebSocketConnectorBypassesSystemProxy(bool isKeepAlive)
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = CreateConnector(isKeepAlive);
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
    /// A connection whose server accepts the upgrade and then never reads, so no ping is answered, is aborted by the
    /// keep-alive: the pending receive fails and the socket is aborted. <see cref="ClientWebSocket" /> takes no
    /// <see cref="TimeProvider" />, so this test runs on the real clock, the documented exception of the unit-test rules;
    /// it asserts only the outcome, never a duration, within a 30-second hang guard (fact 7 measured 2.3 seconds for an
    /// interval and a timeout of 1 second each).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorConnectWithKeepAliveAbortsSilentConnection()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.SilentPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        Assert.IsNotNull(socket, "The silent endpoint should accept the upgrade");

        // Act
        var receiving = socket.ReceiveAsync(new byte[16].AsMemory(), TestContext.CancellationToken).AsTask();
        var first = await Task.WhenAny(receiving, Task.Delay(_hangGuard, TestContext.CancellationToken));

        // Assert
        Assert.AreSame(receiving, first, "The keep-alive should end the pending receive of a connection whose pings get no answer");
        Assert.IsFalse(receiving.IsCompletedSuccessfully, "The pending receive should fail, not return a message");
        Assert.AreEqual(WebSocketState.Aborted, socket.State, "The socket should be aborted");
    }

    /// <summary>
    /// A connection whose server answers the pings stays open while a receive is pending and no message is sent, for
    /// longer than the keep-alive interval and timeout together (1 and 5 seconds, observed 7 seconds), and still carries a
    /// message afterwards. <see cref="ClientWebSocket" /> takes no <see cref="TimeProvider" />, so this test waits on the
    /// real clock, the documented exception of the unit-test rules; it asserts only the outcome, never a duration.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorConnectWithKeepAliveKeepsAnsweringConnection()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.IngestPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        Assert.IsNotNull(socket, "The endpoint should accept the upgrade");

        var buffer = new byte[64];
        var receiving = socket.ReceiveAsync(buffer.AsMemory(), TestContext.CancellationToken).AsTask();

        // Act
        await Task.Delay(TimeSpan.FromSeconds(7), TestContext.CancellationToken);

        var isReceivingAfterIdle = receiving.IsCompleted == false;
        var stateAfterIdle = socket.State;

        await socket.SendAsync(_message, WebSocketMessageType.Binary, true, TestContext.CancellationToken).AsTask().WaitAsync(_guard, TestContext.CancellationToken);

        var echo = await receiving.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(isReceivingAfterIdle, "The pending receive should still wait after the idle time");
        Assert.AreEqual(WebSocketState.Open, stateAfterIdle, "A connection whose pings are answered should stay open");
        Assert.AreSequenceEqual(_message, buffer[..echo.Count], "The connection should still carry a message after the idle time");
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

    /// <summary>
    /// The socket of a connection stays usable after the connect returned: a message goes to the server and its echo comes
    /// back, and the connection closes normally.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ClientWebSocketConnectorConnectedSocketCarriesMessages()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());
        using var socket = await connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.IngestPath, false), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var buffer = new byte[64];

        Assert.IsNotNull(socket, "The WebSocket endpoint should give a socket");

        // Act
        await socket.SendAsync(_message.AsMemory(), WebSocketMessageType.Text, true, TestContext.CancellationToken);

        var echo = await socket.ReceiveAsync(buffer.AsMemory(), TestContext.CancellationToken);

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(_message, buffer.Take(echo.Count), "The echo of the message should come back over the connection");
        Assert.AreEqual(WebSocketState.Closed, socket.State, "The connection should close normally");
    }

    /// <summary>
    /// The caller's cancellation of a connection that waits for its upgrade is rethrown.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ClientWebSocketConnectorCallerCancellationIsRethrown()
    {
        // Arrange
        await using var host = await LoopbackWebSocketHost.StartAsync(TestContext.CancellationToken);
        var connector = new ClientWebSocketConnector(new FakeTimeProvider());
        using var cancellation = new CancellationTokenSource();

        // Act
        var connecting = connector.ConnectAsync(host.WebSocketAddress(LoopbackWebSocketHost.HeldPath, false), cancellation.Token);

        await host.HeldRequestReceived.WaitAsync(_guard, TestContext.CancellationToken);
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => connecting.WaitAsync(_guard, TestContext.CancellationToken), "The caller's cancellation should be rethrown");
    }

    /// <summary>
    /// Creates the connector of a test: the replay's, or the live sender's with its keep-alive.
    /// </summary>
    /// <param name="isKeepAlive">Whether the connector is built with the keep-alive of the live sender</param>
    /// <returns>The connector</returns>
    private static ClientWebSocketConnector CreateConnector(bool isKeepAlive)
    {
        return isKeepAlive
                   ? new ClientWebSocketConnector(new FakeTimeProvider(), LiveSender.KeepAliveInterval, LiveSender.KeepAliveTimeout)
                   : new ClientWebSocketConnector(new FakeTimeProvider());
    }

    #endregion // Methods
}