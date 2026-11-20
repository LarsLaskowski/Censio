using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using Censio.Companion.Core;
using Censio.Contracts;
using Censio.Pipe;
using Censio.Server.Data;
using Censio.Server.Data.Tests;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestEndpoint" />, through the server as <see cref="Program" /> builds it where possible.
/// </summary>
[TestClass]
public sealed class IngestEndpointTests
{
    #region Constants

    /// <summary>
    /// The logger category of the ingest log lines.
    /// </summary>
    private const string IngestLogCategory = "Censio.Server.Ingest";

    /// <summary>
    /// The line logged when a host is blocked.
    /// </summary>
    private const string BlockStartLine = "A host was refused 10 times within 60 seconds; its connections are refused for 60 seconds.";

    /// <summary>
    /// The setting that enables ASP.NET Core's forwarded headers (ASPNETCORE_FORWARDEDHEADERS_ENABLED).
    /// </summary>
    private const string ForwardedHeadersEnabledKey = "FORWARDEDHEADERS_ENABLED";

    /// <summary>
    /// The X-Forwarded-For header.
    /// </summary>
    private const string ForwardedForHeader = "X-Forwarded-For";

    /// <summary>
    /// The file name of the long-session fixture.
    /// </summary>
    private const string LongSessionFixture = "long-session.jsonl";

    /// <summary>
    /// The file name of the protocol 3 fixture with decoded trade routes.
    /// </summary>
    private const string TradeRoutesFixture = "protocol-3-trade-routes.jsonl";

    /// <summary>
    /// The pipe message type of a trade-routes frame (its first byte).
    /// </summary>
    private const byte TradeRoutesFrameType = 4;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// Four client addresses, each a host of its own.
    /// </summary>
    private static readonly IPAddress[] _fourHosts = [IPAddress.Parse("198.51.100.1"), IPAddress.Parse("198.51.100.2"), IPAddress.Parse("198.51.100.3"), IPAddress.Parse("198.51.100.4")];

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
    /// While 16 connections of 4 hosts wait for their hello, a 17th upgrade of a fifth host is answered 503.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    [TestCategory("AC1")]
    public async Task IngestEndpointSeventeenthPendingConnectionReturns503()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider());

        await ConnectPendingFromHostsAsync(factory, _fourHosts, IngestHosts.MaxPendingHellosPerHost);

        var client = CreateClientFrom(factory, IPAddress.Parse("198.51.100.5"));

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.ConnectAsync(IngestUri(factory), TestContext.CancellationToken), "The 17th upgrade should be refused");

        // Assert
        Assert.HasCount(16, _sockets, "Sixteen connections of four hosts should wait for their hello");
        Assert.AreEqual("Incomplete handshake, status code: 503", exception.Message, "The 17th pending connection should answer 503");
    }

    /// <summary>
    /// An accept that fails leaves the pending place and the host's place it took, and is not counted as a failed hello.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    [TestCategory("AC4")]
    public async Task IngestEndpointAcceptFailureLeavesPendingPlace()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
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
        var hosts = app.Services.GetRequiredService<IngestHosts>();

        FailHellos(hosts, null, IngestHosts.MaxFailedHellos - 1);

        // Act
        await SendIgnoringFailureAsync(client, IngestEndpoint.Path, TestContext.CancellationToken);

        // Assert
        var connections = app.Services.GetRequiredService<IngestConnections>();

        Assert.AreEqual(1, feature.AcceptCount, "The endpoint should have tried to accept");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(connections), "The failed accept should leave its pending place");
        Assert.AreEqual(IngestHosts.MaxPendingHellosPerHost, CountFreeHostPlaces(hosts, null), "The failed accept should leave the host's places free and not block it as a tenth failed hello");
    }

    /// <summary>
    /// A host with 4 connections waiting gets 429 with an empty body on a fifth upgrade, while another host's upgrade is
    /// accepted.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IngestEndpointFifthPendingConnectionOfOneHostReturns429()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider());
        var full = IPAddress.Parse("198.51.100.1");

        await ConnectPendingFromHostsAsync(factory, [full], IngestHosts.MaxPendingHellosPerHost);

        // Act
        var (status, body) = await SendRequestAsync(factory, Upgrade(full));

        // Assert
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, status, "The fifth upgrade of the host should answer 429");
        Assert.AreEqual(string.Empty, body, "The 429 answer should have an empty body");
        Assert.AreEqual(WebSocketState.Open, (await ConnectFromAsync(factory, IPAddress.Parse("198.51.100.2"))).State, "Another host's upgrade should be accepted");
    }

    /// <summary>
    /// While 16 connections wait, a host with 4 of them gets 429, not 503: the host's bound is decided first.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointFullHostWhileSixteenPendingReturns429()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider());

        await ConnectPendingFromHostsAsync(factory, _fourHosts, IngestHosts.MaxPendingHellosPerHost);

        // Act
        var (status, body) = await SendRequestAsync(factory, Upgrade(_fourHosts[0]));

        // Assert
        Assert.HasCount(16, _sockets, "Sixteen connections should wait for their hello");
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, status, "The full host should answer 429 before the total of 16 is checked");
        Assert.AreEqual(string.Empty, body, "The 429 answer should have an empty body");
    }

    /// <summary>
    /// The refusals are decided in the order not an upgrade 400, Origin 403, blocked host 429, full host 429, 16 waiting
    /// 503, each with an empty body, and none writes an ingest log line.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointRefusedUpgradesWriteNoLogLine()
    {
        // Arrange
        var loggerProvider = new RecordingLoggerProvider();
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider(), builder => builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider).AddFilter<RecordingLoggerProvider>((category, _) => category == IngestLogCategory)));
        var blocked = IPAddress.Parse("203.0.113.9");
        var fresh = IPAddress.Parse("203.0.113.10");

        await ConnectPendingFromHostsAsync(factory, _fourHosts, IngestHosts.MaxPendingHellosPerHost);
        FailHellos(factory.Services.GetRequiredService<IngestHosts>(), blocked, IngestHosts.MaxFailedHellos);

        var linesBefore = loggerProvider.Logger.Entries.Count;

        // Act
        var answers = new List<(int? Status, string Body)>
                      {
                          await SendRequestAsync(factory, Plain(blocked)),
                          await SendRequestAsync(factory, WithOrigin(Upgrade(blocked))),
                          await SendRequestAsync(factory, Upgrade(blocked)),
                          await SendRequestAsync(factory, Upgrade(_fourHosts[0])),
                          await SendRequestAsync(factory, Upgrade(fresh))
                      };

        // Assert
        Assert.AreSequenceEqual(new int?[] { 400, 403, 429, 429, 503 }, answers.Select(answer => answer.Status).ToList(), "The refusals should be plain 400, Origin 403, blocked host 429, full host 429 and 16 waiting 503");
        Assert.IsEmpty(answers.Where(answer => answer.Body.Length > 0).ToList(), "Every refusal should have an empty body");
        Assert.HasCount(linesBefore, loggerProvider.Logger.Entries, "No refusal should write an ingest log line");
    }

    /// <summary>
    /// A plain request of a blocked host is answered 400, not 429: the request is checked before the host.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointBlockedHostPlainRequestReturns400()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider());
        var blocked = IPAddress.Parse("203.0.113.9");

        FailHellos(factory.Services.GetRequiredService<IngestHosts>(), blocked, IngestHosts.MaxFailedHellos);

        // Act
        var (status, body) = await SendRequestAsync(factory, Plain(blocked));

        // Assert
        Assert.AreEqual(StatusCodes.Status400BadRequest, status, "The blocked host's plain request should answer 400");
        Assert.AreEqual(string.Empty, body, "The 400 answer should have an empty body");
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, (await SendRequestAsync(factory, Upgrade(blocked))).Status, "The host should be blocked");
    }

    /// <summary>
    /// An upgrade with an Origin header of a blocked host is answered 403, not 429: the Origin is checked before the host.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestEndpointBlockedHostWithOriginReturns403()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider());
        var blocked = IPAddress.Parse("203.0.113.9");

        FailHellos(factory.Services.GetRequiredService<IngestHosts>(), blocked, IngestHosts.MaxFailedHellos);

        // Act
        var (status, body) = await SendRequestAsync(factory, WithOrigin(Upgrade(blocked)));

        // Assert
        Assert.AreEqual(StatusCodes.Status403Forbidden, status, "The blocked host's upgrade with an Origin header should answer 403");
        Assert.AreEqual(string.Empty, body, "The 403 answer should have an empty body");
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, (await SendRequestAsync(factory, Upgrade(blocked))).Status, "The host should be blocked");
    }

    /// <summary>
    /// The 10th hello of a host refused for its token blocks the host: its next upgrade gets 429 with an empty body, and
    /// the start of the block is logged.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestEndpointTenthTokenRefusedHelloBlocksHost()
    {
        // Arrange
        var loggerProvider = new RecordingLoggerProvider();
        using var baseFactory = new CensioServerFactory("Production", CreateDataDirectorySettings(CreateTemporaryDirectory()));
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider(), builder => builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider).AddFilter<RecordingLoggerProvider>((category, _) => category == IngestLogCategory)));
        var host = IPAddress.Parse("198.51.100.1");

        await RefuseTokenHellosAsync(factory, host, IngestHosts.MaxFailedHellos, true);

        // Act
        var (status, body) = await SendRequestAsync(factory, Upgrade(host));

        // Assert
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, status, "The blocked host's upgrade should answer 429");
        Assert.AreEqual(string.Empty, body, "The 429 answer should have an empty body");
        Assert.ContainsSingle(entry => entry.Message == BlockStartLine, loggerProvider.Logger.Entries.ToList(), "The start of the block should be logged once");
    }

    /// <summary>
    /// Hellos refused for their token count when they are decided, while their connections still wait for the peer's
    /// close: the 10th blocks the host.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestEndpointRefusedHellosInCloseWaitBlockHost()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production", CreateDataDirectorySettings(CreateTemporaryDirectory()));
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider());
        var host = IPAddress.Parse("198.51.100.1");

        await RefuseTokenHellosAsync(factory, host, IngestHosts.MaxFailedHellos, false);

        // Act
        var (status, _) = await SendRequestAsync(factory, Upgrade(host));

        // Assert
        Assert.HasCount(IngestHosts.MaxFailedHellos, _sockets, "Ten refused connections should wait in their close wait");
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, status, "The host should be blocked by hellos refused while their connections wait for the close");
    }

    /// <summary>
    /// With forwarded headers enabled, upgrades carrying X-Forwarded-For count for their TCP peer: a fifth from one peer
    /// gets 429 whatever address the header names, and a peer whose header names the full host is accepted.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task IngestEndpointForwardedForWithForwardedHeadersEnabledCountsForPeer()
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider(), builder => builder.UseSetting(ForwardedHeadersEnabledKey, "true"));
        var peer = IPAddress.Parse("192.0.2.1");

        await ConnectPendingWithHeaderAsync(factory, peer, ForwardedForHeader, "203.0.113.{0}", IngestHosts.MaxPendingHellosPerHost);

        // Act
        var (status, _) = await SendRequestAsync(factory, WithHeader(Upgrade(peer), ForwardedForHeader, "203.0.113.5"));

        // Assert
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, status, "The peer's fifth upgrade should answer 429 whatever X-Forwarded-For names");
        Assert.AreEqual(WebSocketState.Open, (await ConnectFromAsync(factory, IPAddress.Parse("192.0.2.2"), ForwardedForHeader, peer.ToString())).State, "Another peer naming the full host in X-Forwarded-For should be accepted");
    }

    /// <summary>
    /// Upgrades carrying X-Forwarded-For or Forwarded count for their TCP peer, with forwarded headers unset or enabled.
    /// </summary>
    /// <param name="header">The forwarded header</param>
    /// <param name="format">The format of the header's value, with the index of the connection</param>
    /// <param name="enabled">The value of the forwarded-headers setting, or <c>null</c> when unset</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(ForwardedForHeader, "203.0.113.{0}", null)]
    [DataRow("Forwarded", "for=203.0.113.{0}", null)]
    [DataRow("Forwarded", "for=203.0.113.{0}", "true")]
    public async Task IngestEndpointForwardedHeadersCountForPeer(string header, string format, string? enabled)
    {
        // Arrange
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = WithFakeClock(baseFactory, new FakeTimeProvider(), builder => builder.UseSetting(ForwardedHeadersEnabledKey, enabled));
        var peer = IPAddress.Parse("192.0.2.1");

        await ConnectPendingWithHeaderAsync(factory, peer, header, format, IngestHosts.MaxPendingHellosPerHost);

        // Act
        var (status, _) = await SendRequestAsync(factory, WithHeader(Upgrade(peer), header, string.Format(CultureInfo.InvariantCulture, format, 5)));

        // Assert
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, status, $"The peer's fifth upgrade should answer 429 whatever {header} names, setting {enabled ?? "unset"}");
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
    /// A replay of the trade-routes fixture sent with the stored token leaves the 57 routes of the file's last trade-routes
    /// frame as the profile's latest trade routes, in order with every value (the repeated name as two routes), under that
    /// frame's recorded stamp.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IngestEndpointReplaySentTradeRoutesFixtureStoresLastRoutes()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var state = factory.Services.GetRequiredService<ServerState>();
        await using var sender = CreateReplaySender(factory, dataDirectory);
        var expected = await ReadLastFixtureTradeRoutesAsync(TradeRoutesFixture, sender.ProfileId);

        // Act
        await CreateFixtureRunner(sender, TradeRoutesFixture).RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);

        var read = await state.ReadProfileStateAsync(sender.ProfileId, TestContext.CancellationToken);

        // Assert
        var stored = read.State?.LatestTradeRoutes;

        Assert.HasCount(57, expected?.Routes ?? [], "The fixture's last trade-routes frame should hold 57 routes, or the comparison below would prove less");
        Assert.HasCount(56, expected!.Routes.Select(route => route.Name).Distinct(StringComparer.Ordinal).ToList(), "One route name of the frame should be repeated, or the comparison below would prove less");
        Assert.AreSequenceEqual(DescribeRoutes(expected), DescribeRoutes(stored), "The profile's latest trade routes should be the last frame's routes, in order with every value");
        Assert.AreEqual(ReadLastTradeRoutesStamp(TradeRoutesFixture), stored?.ReceiveTime, "The latest trade routes should carry the last frame's recorded stamp");
    }

    /// <summary>
    /// A replay of the trade-routes fixture counts only its ticks with islands as sent, none refused: the acknowledged
    /// trade-routes messages count in neither number.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestEndpointReplaySentTradeRoutesFixtureCountsOnlyTicks()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        var expected = await CountFixtureTicksWithIslandsAsync(TradeRoutesFixture);
        await using var sender = CreateReplaySender(factory, dataDirectory);
        var runner = CreateFixtureRunner(sender, TradeRoutesFixture);

        // Act
        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);

        // Assert
        Assert.IsGreaterThan(0, expected, "The fixture should have ticks with islands, or the test would prove less");
        Assert.AreEqual($"Censio Companion: Replay finished: {expected} ticks sent, 0 refused", TrayMenu.FormatReplayToolTip(runner.Status), "Only the ticks should be counted as sent, and the acknowledged trade routes not as refused");
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
    /// Creates a runner that replays a fixture at maximum speed into a sender.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="fileName">The file name of the fixture</param>
    /// <returns>The runner</returns>
    private static PipeReplayRunner CreateFixtureRunner(ReplaySender sender, string fileName = LongSessionFixture)
    {
        return new PipeReplayRunner(() => File.OpenRead(FixturePath(fileName)), PipeReplaySpeed.Maximum, TimeProvider.System, NullLogger.Instance, sender);
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
    /// Returns the path of a linked pipe fixture.
    /// </summary>
    /// <param name="fileName">The file name of the fixture</param>
    /// <returns>The path</returns>
    private static string FixturePath(string fileName = LongSessionFixture)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", fileName);
    }

    /// <summary>
    /// Reads the recorded stamp of the last trade-routes frame of a fixture from its lines.
    /// </summary>
    /// <param name="fileName">The file name of the fixture</param>
    /// <returns>The stamp</returns>
    private static DateTimeOffset ReadLastTradeRoutesStamp(string fileName)
    {
        var last = File.ReadLines(FixturePath(fileName))
                       .Skip(1)
                       .Select(line => JsonNode.Parse(line)!)
                       .Last(line => Convert.FromBase64String(line["frame"]!.GetValue<string>())[0] == TradeRoutesFrameType);

        return last["time"]!.GetValue<DateTimeOffset>();
    }

    /// <summary>
    /// Describes the routes of a set of trade routes as text, one line per route in order with its stations in order.
    /// </summary>
    /// <param name="routes">The set, or <see langword="null" /></param>
    /// <returns>The lines</returns>
    private static List<string> DescribeRoutes(ContractTradeRoutes? routes)
    {
        if (routes is null)
        {
            return ["<no trade routes>"];
        }

        return [.. routes.Routes.Select(route => $"{route.Id} {route.Name} {route.Folder} {route.MajorProblems} {route.MinorProblems} {route.Ships} {route.LostShips} [{string.Join(" ", route.Stations.Select(station => $"{station.SessionGuid}/{station.IslandId}/{station.AreaIndex}"))}]")];
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
    /// Counts the free places of a host by taking them all.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address of the host</param>
    /// <returns>The number of free places; 0 when the host is blocked</returns>
    private static int CountFreeHostPlaces(IngestHosts hosts, IPAddress? address)
    {
        var count = 0;

        while (count <= IngestHosts.MaxPendingHellosPerHost && hosts.TryEnterPendingHello(address) is not null)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Records failed hellos of a host directly on the hosts, each on a place it got.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address of the host</param>
    /// <param name="count">The number of failed hellos</param>
    private static void FailHellos(IngestHosts hosts, IPAddress? address, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var host = hosts.TryEnterPendingHello(address);

            Assert.IsNotNull(host, "Each failed hello of the arrangement should get a place");
            hosts.RecordFailedHello(host);
        }
    }

    /// <summary>
    /// Builds the server with a fake clock and further configuration.
    /// </summary>
    /// <param name="baseFactory">The server factory</param>
    /// <param name="clock">The clock of the server</param>
    /// <param name="configure">The further configuration, or <c>null</c> for none</param>
    /// <returns>The configured factory</returns>
    private static WebApplicationFactory<Program> WithFakeClock(CensioServerFactory baseFactory, FakeTimeProvider clock, Action<IWebHostBuilder>? configure = null)
    {
        return baseFactory.WithWebHostBuilder(builder =>
                                              {
                                                  builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock));
                                                  configure?.Invoke(builder);
                                              });
    }

    /// <summary>
    /// Builds a plain request to the endpoint from a client address.
    /// </summary>
    /// <param name="address">The client address</param>
    /// <returns>The configuration of the request</returns>
    private static Action<HttpContext> Plain(IPAddress address)
    {
        return context =>
               {
                   context.Request.Path = IngestEndpoint.Path;
                   context.Connection.RemoteIpAddress = address;
               };
    }

    /// <summary>
    /// Builds a WebSocket upgrade to the endpoint from a client address, whose accept fails if it is reached.
    /// </summary>
    /// <param name="address">The client address</param>
    /// <returns>The configuration of the request</returns>
    private static Action<HttpContext> Upgrade(IPAddress address)
    {
        var plain = Plain(address);

        return context =>
               {
                   plain(context);
                   context.Features.Set<IHttpWebSocketFeature>(new FailingWebSocketFeature());
               };
    }

    /// <summary>
    /// Adds an Origin header to a request.
    /// </summary>
    /// <param name="request">The configuration of the request</param>
    /// <returns>The configuration with the header</returns>
    private static Action<HttpContext> WithOrigin(Action<HttpContext> request)
    {
        return WithHeader(request, "Origin", "http://browser.example");
    }

    /// <summary>
    /// Adds a header to a request.
    /// </summary>
    /// <param name="request">The configuration of the request</param>
    /// <param name="name">The name of the header</param>
    /// <param name="value">The value of the header</param>
    /// <returns>The configuration with the header</returns>
    private static Action<HttpContext> WithHeader(Action<HttpContext> request, string name, string value)
    {
        return context =>
               {
                   request(context);
                   context.Request.Headers[name] = value;
               };
    }

    /// <summary>
    /// Creates a WebSocket client whose connections come from a client address, with an optional header.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="address">The client address</param>
    /// <param name="header">The name of a header to send, or <c>null</c> for none</param>
    /// <param name="value">The value of the header</param>
    /// <returns>The client</returns>
    private static WebSocketClient CreateClientFrom(WebApplicationFactory<Program> factory, IPAddress address, string? header = null, string? value = null)
    {
        var client = factory.Server.CreateWebSocketClient();

        client.ConfigureRequest = request =>
                                  {
                                      request.HttpContext.Connection.RemoteIpAddress = address;

                                      if (header is not null)
                                      {
                                          request.Headers[header] = value;
                                      }
                                  };

        return client;
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
    /// Opens a connection from a client address, with an optional header, that is aborted after the test.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="address">The client address</param>
    /// <param name="header">The name of a header to send, or <c>null</c> for none</param>
    /// <param name="value">The value of the header</param>
    /// <returns>The socket</returns>
    private Task<WebSocket> ConnectFromAsync(WebApplicationFactory<Program> factory, IPAddress address, string? header = null, string? value = null)
    {
        return ConnectAsync(CreateClientFrom(factory, address, header, value), IngestUri(factory));
    }

    /// <summary>
    /// Opens connections that send no hello from each of a number of hosts.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="hosts">The client addresses</param>
    /// <param name="perHost">The number of connections of each host</param>
    /// <returns>The task</returns>
    private async Task ConnectPendingFromHostsAsync(WebApplicationFactory<Program> factory, IPAddress[] hosts, int perHost)
    {
        foreach (var host in hosts)
        {
            await ConnectPendingAsync(CreateClientFrom(factory, host), IngestUri(factory), perHost).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens connections that send no hello from one peer, each with a header naming another address.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="peer">The TCP peer's address</param>
    /// <param name="header">The name of the header</param>
    /// <param name="format">The format of the header's value, with the index of the connection</param>
    /// <param name="count">The number of connections</param>
    /// <returns>The task</returns>
    private async Task ConnectPendingWithHeaderAsync(WebApplicationFactory<Program> factory, IPAddress peer, string header, string format, int count)
    {
        for (var index = 1; index <= count; index++)
        {
            await ConnectFromAsync(factory, peer, header, string.Format(CultureInfo.InvariantCulture, format, index)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens connections from a host that each send a hello with a wrong token and read its refusal and the server's close.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="host">The client address</param>
    /// <param name="count">The number of connections</param>
    /// <param name="answerClose">Whether each connection answers the server's close, or leaves it in its close wait</param>
    /// <returns>The task</returns>
    private async Task RefuseTokenHellosAsync(WebApplicationFactory<Program> factory, IPAddress host, int count, bool answerClose)
    {
        for (var index = 0; index < count; index++)
        {
            var socket = await ConnectFromAsync(factory, host).ConfigureAwait(false);

            await IngestClient.SendAsync(socket, IngestClient.Hello("MARKERTOKEN-wrong")).ConfigureAwait(false);

            var (messages, _, _) = await IngestClient.ReceiveUntilCloseAsync(socket).ConfigureAwait(false);

            Assert.IsInstanceOfType<ContractHelloRefused>(messages.Single(), "Each hello of the arrangement should be refused");

            if (answerClose)
            {
                await IngestClient.CloseAsync(socket).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sends a request through the test server and reads its status and body.
    /// </summary>
    /// <param name="factory">The server factory</param>
    /// <param name="request">The configuration of the request</param>
    /// <returns>The status and body, or a <c>null</c> status when the request reached the accept</returns>
    private async Task<(int? Status, string Body)> SendRequestAsync(WebApplicationFactory<Program> factory, Action<HttpContext> request)
    {
        try
        {
            var context = await factory.Server.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

            using var reader = new StreamReader(context.Response.Body);

            return (context.Response.StatusCode, await reader.ReadToEndAsync(TestContext.CancellationToken).ConfigureAwait(false));
        }
        catch (WebSocketException)
        {
            // The test host rethrows the failed accept of a request that was let through.
            return (null, string.Empty);
        }
    }

    /// <summary>
    /// Replays a fixture through the message reader and the tick assembler and counts its ticks with at least one island.
    /// </summary>
    /// <param name="fileName">The file name of the fixture</param>
    /// <returns>The number of ticks with islands</returns>
    private async Task<int> CountFixtureTicksWithIslandsAsync(string fileName = LongSessionFixture)
    {
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        using var replay = new PipeReplayStream(File.OpenRead(FixturePath(fileName)), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
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
    /// Replays a fixture through the message reader and the tick assembler and gives its last trade-routes message as the
    /// Companion converts it.
    /// </summary>
    /// <param name="fileName">The file name of the fixture</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The converted message, or <see langword="null" /> when the fixture has none</returns>
    private async Task<ContractTradeRoutes?> ReadLastFixtureTradeRoutesAsync(string fileName, Guid profileId)
    {
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        using var replay = new PipeReplayStream(File.OpenRead(FixturePath(fileName)), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock);

        TradeRoutesMessage? last = null;
        var result = await assembler.ReadAsync(TestContext.CancellationToken);

        while (result.Kind != PipeTickResultKind.EndOfStream)
        {
            last = result.TradeRoutes ?? last;
            result = await assembler.ReadAsync(TestContext.CancellationToken);
        }

        return last is null
                   ? null
                   : ContractMessageConverter.ToTradeRoutes(last, profileId, DateTimeOffset.UnixEpoch);
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