using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

using Censio.Companion.Core;
using Censio.Contracts;
using Censio.Pipe;
using Censio.Server.Data;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// The real-time limit of a replay of the fixture into the test server.
    /// </summary>
    private static readonly TimeSpan _replayBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The regions of the fixture, ordered by GUID, with their numbers of islands.
    /// </summary>
    private static readonly string[] _fixtureRegions = ["3245 islands 13", "6627 islands 9", "149679 islands 2"];

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

        var opened = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, TestContext.CancellationToken);

        using var database = opened.Database ?? throw new InvalidOperationException("The in-memory database should open");

        app.MapCensioIngest(IngestAccess.CreateOpen(), new ServerState(new ServerStore(database), NullLogger.Instance));
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
    /// A replay of the fixture sent with the stored token fills the pages of its profile: three regions with the session
    /// GUIDs 3245, 6627 and 149679 and 13, 9 and 2 islands, and after the final tick without data the 24 islands without
    /// values.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IngestEndpointReplaySentFixtureFillsPages()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var state = factory.Services.GetRequiredService<ServerState>();
        await using var sender = CreateReplaySender(factory, dataDirectory);

        // Act
        await CreateFixtureRunner(sender).RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);

        var read = await new PageReader(state).ReadAsync(sender.ProfileId, null, TestContext.CancellationToken);

        // Assert
        var view = read.View ?? throw new AssertFailedException("The replayed profile should have a view");
        var islands = view.Regions.SelectMany(region => region.Islands).ToList();

        Assert.AreSequenceEqual(_fixtureRegions, view.Regions.OrderBy(region => region.RegionGuid).Select(region => $"{region.RegionGuid} islands {region.Islands.Count}"), "The pages should show the fixture's three regions with their islands");
        Assert.HasCount(24, islands, "The 24 islands should be listed");
        Assert.IsEmpty(islands.Where(island => island.Figures is not null).ToList(), "After the final tick without data no island should have values");
        Assert.AreEqual(sender.ProfileName, state.Profiles.Single(profile => profile.Id == sender.ProfileId).Name, "The profile should carry the replay's name");
        Assert.StartsWith("Replay ", sender.ProfileName, "The profile name should be the replay's");
    }

    /// <summary>
    /// A replay of the fixture ends with the number of its ticks with at least one island sent and none refused.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IngestEndpointReplaySentFixtureCountsTicksWithIslands()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var expected = await CountFixtureTicksWithIslandsAsync();
        await using var sender = CreateReplaySender(factory, dataDirectory);
        var runner = CreateFixtureRunner(sender);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);

        // Assert
        Assert.IsGreaterThan(0, expected, "The fixture should have ticks with islands, or the test would prove less");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = expected,
                            Refused = 0
                        },
                        runner.Status.Sent,
                        "Every tick with islands should be sent and none refused");
        Assert.AreEqual($"Censio Companion: Replay finished: {expected} ticks sent, 0 refused", TrayMenu.FormatReplayToolTip(runner.Status), "The Companion should end with the counts");
    }

    /// <summary>
    /// The followers of the server state are told of the replay's profile and of each sent tick.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointReplaySentFixtureNotifiesFollowersPerTick()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var state = factory.Services.GetRequiredService<ServerState>();
        var ticks = await CountFixtureTicksWithIslandsAsync();
        await using var sender = CreateReplaySender(factory, dataDirectory);
        var (notices, told) = FollowProfile(state, sender.ProfileId, ticks + 1);

        // Act
        await CreateFixtureRunner(sender).RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);
        await told.WaitAsync(_replayBound, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ticks + 1, Volatile.Read(ref notices.Value), "The followers should be told of the profile and of each sent tick");
    }

    /// <summary>
    /// The Companion shows as connected with the replayed state while the ticks are sent, and as not connected after the end.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointReplaySentShowsCompanionConnectedThenNot()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var state = factory.Services.GetRequiredService<ServerState>();
        await using var sender = CreateReplaySender(factory, dataDirectory);
        var runner = CreateFixtureRunner(sender);
        var seen = new List<CompanionConnection>();

        runner.TickReceived += (_, _) => seen.Add(state.CompanionState);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);
        await WaitForCompanionDisconnectedAsync(state);

        // Assert
        Assert.IsNotEmpty(seen, "The replay should raise its ticks");
        Assert.DoesNotContain(connection => connection.IsConnected == false, seen, "The Companion should show as connected while the ticks are sent");
        Assert.AreEqual(ContractPipeState.Receiving, seen[^1].PipeState, "The Companion should show the replayed state");
        Assert.IsFalse(state.CompanionState.IsConnected, "After the end the Companion should show as not connected");
    }

    /// <summary>
    /// A second replay creates a second profile.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestEndpointSecondReplayCreatesSecondProfile()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var state = factory.Services.GetRequiredService<ServerState>();
        await using var first = CreateReplaySender(factory, dataDirectory);
        await using var second = CreateReplaySender(factory, dataDirectory);

        await CreateFixtureRunner(first).RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);
        await WaitForCompanionDisconnectedAsync(state);

        // Act
        await CreateFixtureRunner(second).RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);

        // Assert
        Assert.AreNotEqual(first.ProfileId, second.ProfileId, "Each replay should have its own profile identity");
        Assert.AreSequenceEqual(new[] { first.ProfileId, second.ProfileId }.Order(), state.Profiles.Select(profile => profile.Id).Order(), "The server should hold the two replays as two profiles");
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
    /// Creates a runner that replays the fixture at maximum speed into a sender.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <returns>The runner</returns>
    private static PipeReplayRunner CreateFixtureRunner(ReplaySender sender)
    {
        return new PipeReplayRunner(() => File.OpenRead(FixturePath()), PipeReplaySpeed.Maximum, TimeProvider.System, NullLogger.Instance, sender);
    }

    /// <summary>
    /// Creates a sender that connects to the test server's ingest endpoint with the stored token.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="dataDirectory">The data directory of the server</param>
    /// <returns>The sender</returns>
    private static ReplaySender CreateReplaySender(WebApplicationFactory<Program> factory, string dataDirectory)
    {
        return new ReplaySender(IngestUri(factory), Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName), "1.0.0", new TestServerReplayConnector(factory.Server), TimeProvider.System, NullLogger.Instance);
    }

    /// <summary>
    /// Returns the path of the pipe fixture.
    /// </summary>
    /// <returns>The path</returns>
    private static string FixturePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", "long-session.jsonl");
    }

    /// <summary>
    /// Follows the server state and counts the changes of a profile.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="expected">The number of changes after which the returned task completes</param>
    /// <returns>The count and the task that completes once the expected number was told</returns>
    private static (StrongBox<int> Notices, Task Told) FollowProfile(ServerState state, Guid profileId, int expected)
    {
        var notices = new StrongBox<int>();
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        state.Changed += (_, change) =>
                         {
                             if (change.Kind == ServerStateChangeKind.Profile && change.ProfileId == profileId && Interlocked.Increment(ref notices.Value) >= expected)
                             {
                                 told.TrySetResult();
                             }
                         };

        return (notices, told.Task);
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
    /// Replays the fixture through the message reader and the tick assembler and counts its ticks with at least one island.
    /// </summary>
    /// <returns>The number of ticks with islands</returns>
    private async Task<int> CountFixtureTicksWithIslandsAsync()
    {
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        using var replay = new PipeReplayStream(File.OpenRead(FixturePath()), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock);

        var count = 0;
        var result = await assembler.ReadAsync(TestContext.CancellationToken);

        while (result.Kind != PipeTickResultKind.EndOfStream)
        {
            count += result.Tick is { Islands.Count: > 0 } ? 1 : 0;
            result = await assembler.ReadAsync(TestContext.CancellationToken);
        }

        return count;
    }

    /// <summary>
    /// Waits until the server shows the Companion as not connected.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <returns>A task that completes when the Companion is not connected</returns>
    private async Task WaitForCompanionDisconnectedAsync(ServerState state)
    {
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        state.Changed += (_, _) =>
                         {
                             if (state.CompanionState.IsConnected == false)
                             {
                                 disconnected.TrySetResult();
                             }
                         };

        if (state.CompanionState.IsConnected == false)
        {
            disconnected.TrySetResult();
        }

        await disconnected.Task.WaitAsync(_replayBound, TestContext.CancellationToken);
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