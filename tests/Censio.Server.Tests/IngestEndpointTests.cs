using System.Net;
using System.Net.WebSockets;

using Censio.Contracts;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestEndpoint" />, through the server as <see cref="Program" /> builds it where possible.
/// </summary>
[TestClass]
public sealed class IngestEndpointTests
{
    #region Fields

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The sockets of the running test, aborted afterwards.
    /// </summary>
    private readonly List<WebSocket> _sockets = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestEndpointTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public IngestEndpointTests(TestContext testContext)
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
    /// Aborts the sockets and deletes the temporary directories of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var socket in _sockets)
        {
            socket.Abort();
            socket.Dispose();
        }

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// The accept context sends a ping every 15 seconds, drops a peer after 35 seconds without its pong, and names no
    /// subprotocol.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestEndpointCreateAcceptContextSetsKeepAlive()
    {
        // Arrange
        var interval = TimeSpan.FromSeconds(15);
        var timeout = TimeSpan.FromSeconds(35);

        // Act
        var context = IngestEndpoint.CreateAcceptContext();

        // Assert
        Assert.AreEqual(interval, context.KeepAliveInterval, "A ping should be sent every 15 seconds");
        Assert.AreEqual(timeout, context.KeepAliveTimeout, "A missing pong should drop the peer after 35 seconds");
        Assert.IsNull(context.SubProtocol, "No subprotocol should be named");
        Assert.AreEqual(interval, IngestEndpoint.KeepAliveInterval, "The interval property should be 15 seconds");
        Assert.AreEqual(timeout, IngestEndpoint.KeepAliveTimeout, "The timeout property should be 35 seconds");
    }

    /// <summary>
    /// A request to the endpoint that is no WebSocket upgrade is answered 400 with an empty body, for every method.
    /// </summary>
    /// <param name="method">The HTTP method</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    [DataRow("GET")]
    [DataRow("POST")]
    public async Task IngestEndpointPlainRequestReturns400(string method)
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), IngestEndpoint.Path);

        // Act
        using var response = await client.SendAsync(request, TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, $"A plain {method} should answer 400");
        Assert.AreEqual(string.Empty, body, $"The 400 answer to {method} should have an empty body");
    }

    /// <summary>
    /// A WebSocket upgrade that carries an Origin header (a browser) is answered 403.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestEndpointUpgradeWithOriginReturns403()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        var client = factory.Server.CreateWebSocketClient();

        client.ConfigureRequest = request => request.Headers.Origin = "http://browser.example";

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.ConnectAsync(IngestUri(factory), TestContext.CancellationToken), "The upgrade should be refused");

        // Assert
        Assert.AreEqual("Incomplete handshake, status code: 403", exception.Message, "The upgrade with an Origin header should answer 403");
    }

    /// <summary>
    /// While 16 connections wait for their hello, a 17th upgrade is answered 503.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestEndpointSeventeenthPendingConnectionReturns503()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider())));
        var client = factory.Server.CreateWebSocketClient();

        await ConnectPendingAsync(client, IngestUri(factory), IngestConnections.MaxPendingHellos);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.ConnectAsync(IngestUri(factory), TestContext.CancellationToken), "The 17th upgrade should be refused");

        // Assert
        Assert.HasCount(16, _sockets, "Sixteen connections should wait for their hello");
        Assert.AreEqual("Incomplete handshake, status code: 503", exception.Message, "The 17th pending connection should answer 503");
    }

    /// <summary>
    /// An accept that fails leaves the pending place it took.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestEndpointAcceptFailureLeavesPendingPlace()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Services.AddCensioIngest();

        await using var app = builder.Build();
        var feature = new FailingWebSocketFeature();

        app.UseWebSockets();
        app.Use((context, next) =>
                {
                    context.Features.Set<IHttpWebSocketFeature>(feature);

                    return next(context);
                });
        app.MapCensioIngest(IngestAccess.CreateOpen());
        await app.StartAsync(TestContext.CancellationToken);

        using var client = app.GetTestClient();

        // Act
        await SendIgnoringFailureAsync(client, IngestEndpoint.Path, TestContext.CancellationToken);

        // Assert
        var connections = app.Services.GetRequiredService<IngestConnections>();

        Assert.AreEqual(1, feature.AcceptCount, "The endpoint should have tried to accept");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(connections), "The failed accept should leave its pending place");
    }

    /// <summary>
    /// A Companion connecting to the server's endpoint with the stored token gets helloAccepted with the server's product
    /// version.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointConnectWithStoredTokenReceivesHelloAccepted()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var socket = await ConnectAsync(factory.Server.CreateWebSocketClient(), IngestUri(factory));
        var token = await File.ReadAllTextAsync(Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName), TestContext.CancellationToken);

        // Act
        await IngestClient.SendAsync(socket, IngestClient.Hello(token));

        var (reply, _, _) = await IngestClient.ReceiveAsync(socket);

        // Assert
        var accepted = Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello with the stored token should be accepted");

        Assert.AreEqual(ProductVersion.Read(typeof(Program).Assembly), accepted.ServerVersion, "The reply should carry the server's product version");
        Assert.IsNull(accepted.CatalogIdentity, "The reply should carry no catalog identity");
    }

    /// <summary>
    /// After a connection closed for an unreadable first message, the server still answers /health and accepts the next
    /// Companion.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task IngestEndpointAfterRefusedMessageHealthStillAnswers()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var webSocketClient = factory.Server.CreateWebSocketClient();
        var refused = await ConnectAsync(webSocketClient, IngestUri(factory));
        var token = await File.ReadAllTextAsync(Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName), TestContext.CancellationToken);

        await IngestClient.SendAsync(refused, "{'type':'hello'");

        var (_, closeStatus, _) = await IngestClient.ReceiveUntilCloseAsync(refused);

        await IngestClient.CloseAsync(refused);

        using var httpClient = factory.CreateClient();

        // Act
        using var health = await httpClient.GetAsync("/health", TestContext.CancellationToken);
        var next = await ConnectAsync(webSocketClient, IngestUri(factory));

        await IngestClient.SendAsync(next, IngestClient.Hello(token));

        var (reply, _, _) = await IngestClient.ReceiveAsync(next);

        // Assert
        Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, closeStatus, "The unreadable first message should be closed with 1008");
        Assert.AreEqual(HttpStatusCode.OK, health.StatusCode, "The health endpoint should still answer 200");
        Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The next Companion should be accepted");
    }

    /// <summary>
    /// Returns the WebSocket address of the endpoint on the test server.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <returns>The address</returns>
    private static Uri IngestUri(WebApplicationFactory<Program> factory)
    {
        return new UriBuilder(factory.Server.BaseAddress)
               {
                   Scheme = "ws",
                   Path = IngestEndpoint.Path
               }.Uri;
    }

    /// <summary>
    /// Creates the settings that point the server to a data directory.
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <returns>The settings</returns>
    private static Dictionary<string, string?> CreateDataDirectorySettings(string dataDirectory)
    {
        return new Dictionary<string, string?>
               {
                   [DataProtectionSetup.DataDirectoryKey] = dataDirectory
               };
    }

    /// <summary>
    /// Sends a GET request and ignores a failure of the server, which the test host rethrows.
    /// </summary>
    /// <param name="client">The client</param>
    /// <param name="path">The path</param>
    /// <param name="cancellationToken">The token that ends the request</param>
    /// <returns>The task</returns>
    private static async Task SendIgnoringFailureAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // The test host rethrows the failed accept; the test asserts the pending place instead.
        }
    }

    /// <summary>
    /// Counts the free pending places by taking them all.
    /// </summary>
    /// <param name="connections">The connections</param>
    /// <returns>The number of free places</returns>
    private static int CountFreePendingPlaces(IngestConnections connections)
    {
        var count = 0;

        while (count <= IngestConnections.MaxPendingHellos && connections.TryEnterPendingHello())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Opens connections that send no hello.
    /// </summary>
    /// <param name="client">The WebSocket client of the test server</param>
    /// <param name="uri">The address of the endpoint</param>
    /// <param name="count">The number of connections</param>
    /// <returns>The task</returns>
    private async Task ConnectPendingAsync(WebSocketClient client, Uri uri, int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ConnectAsync(client, uri).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a connection that is aborted after the test.
    /// </summary>
    /// <param name="client">The WebSocket client of the test server</param>
    /// <param name="uri">The address of the endpoint</param>
    /// <returns>The socket</returns>
    private async Task<WebSocket> ConnectAsync(WebSocketClient client, Uri uri)
    {
        var socket = await client.ConnectAsync(uri, TestContext.CancellationToken).ConfigureAwait(false);

        _sockets.Add(socket);

        return socket;
    }

    /// <summary>
    /// Creates an empty temporary directory that is deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);

        return path;
    }

    #endregion // Methods
}