using System.Net.WebSockets;

using Censio.Contracts;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LiveSender" />.
/// </summary>
[TestClass]
public sealed class LiveSenderTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The token of the forward setting.
    /// </summary>
    private const string Token = "LiveToken-0123456789abcdefghijKLMNOPQR";

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private const string CompanionVersion = "1.2.3";

    /// <summary>
    /// The host of the server address.
    /// </summary>
    private const string ServerHost = "secret-host.example";

    /// <summary>
    /// The port of the local server.
    /// </summary>
    private const int LocalPort = 5117;

    /// <summary>
    /// The number of stations of each route of a large trade-routes message.
    /// </summary>
    private const int StationsPerRoute = 256;

    /// <summary>
    /// The number of routes of a large trade-routes message.
    /// </summary>
    private const int LargeRoutes = 150;

    /// <summary>
    /// The number of large trade-routes messages queued: one more than fit the byte bound, about 2 MB each.
    /// </summary>
    private const int LargeMessages = 9;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start time of the time provider.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address of the server.
    /// </summary>
    private static readonly Uri _address = new UriBuilder(Uri.UriSchemeWs, ServerHost, 8080, "/ingest", "?key=hidden").Uri;

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The delays before the first seven retries: 1, 2, 4, 8, 16, 30 and 30 seconds.
    /// </summary>
    private static readonly TimeSpan[] _sevenRetryDelays = [.. new[] { 1, 2, 4, 8, 16, 30, 30 }.Select(seconds => TimeSpan.FromSeconds(seconds))];

    /// <summary>
    /// The first route identities of the large messages kept within the byte bound.
    /// </summary>
    private static readonly int[] _keptRouteIds = [2, 3, 4, 5, 6, 7, 8, 9];

    /// <summary>
    /// The log line of two dropped messages.
    /// </summary>
    private static readonly string[] _droppedTwoLines = ["Live messages dropped: 2"];

    /// <summary>
    /// The log line of a refused token.
    /// </summary>
    private static readonly string[] _tokenRefusedLines = ["Live sending refused by the server: token refused; not retried"];

    /// <summary>
    /// The log line of an unsupported contract version.
    /// </summary>
    private static readonly string[] _versionRefusedLines = ["Live sending refused by the server: server does not support contract version 2; not retried"];

    /// <summary>
    /// The log lines of a lost connection followed by a connection.
    /// </summary>
    private static readonly string[] _lostThenConnectedLines = ["Live sending failed: server connection lost; retrying", "Live sending connected to the server"];

    /// <summary>
    /// The logger of the senders.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    /// <summary>
    /// The statuses the senders raised, in order.
    /// </summary>
    private readonly List<LiveSendStatus> _statuses = [];

    /// <summary>
    /// The tests waiting for statuses, each with the condition on the statuses that ends its wait.
    /// </summary>
    private readonly List<(Func<List<LiveSendStatus>, bool> IsReached, TaskCompletionSource Source)> _statusWaiters = [];

    /// <summary>
    /// The lock of the statuses and their waiters.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The source that stops the runs of the test.
    /// </summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>
    /// The runs started by the test.
    /// </summary>
    private readonly List<Task> _runs = [];

    /// <summary>
    /// The senders of the test, disposed afterwards.
    /// </summary>
    private readonly List<LiveSender> _senders = [];

    /// <summary>
    /// The scripted servers of the test, disposed afterwards.
    /// </summary>
    private readonly List<ScriptedIngestServer> _servers = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSenderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LiveSenderTests(TestContext testContext)
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
    /// Stops the runs, disposes the senders and the servers.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        await StopRunsAsync();

        foreach (var sender in _senders)
        {
            await sender.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        }

        foreach (var server in _servers)
        {
            await server.DisposeAsync();
        }
    }

    /// <summary>
    /// The hello carries the fixed profile identity, the profile name Live, the Companion's version and the token of the
    /// setting, or no token without one, and goes to the address of the setting (S1, E2).
    /// </summary>
    /// <param name="token">The token of the setting</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(Token, DisplayName = "With token")]
    [DataRow(null, DisplayName = "Without token")]
    public async Task LiveSenderRunSendsHelloWithFixedProfileAndToken(string? token)
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), token);

        // Act
        StartRun(sender);

        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var hello = Assert.IsInstanceOfType<ContractHello>(message, "The first message should be the hello");

        Assert.AreEqual(token, hello.Token, "The hello should carry the token of the setting");
        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should carry the fixed profile identity");
        Assert.AreEqual("Live", hello.ProfileName, "The hello should carry the profile name Live");
        Assert.AreEqual(CompanionVersion, hello.CompanionVersion, "The hello should carry the Companion's version");
        Assert.AreEqual(2, ContractReader.CurrentVersion, "The server double reads only contract version 2, so the hello it read was written in version 2");
        Assert.AreSequenceEqual([_address], connector.Addresses, "The connector should be asked once for the address of the setting");
    }

    /// <summary>
    /// The fixed profile is one identity built into the Companion, the same on every read and on every start, named Live
    /// (Q1, record 0102).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void LiveSenderProfileIdIsFixed()
    {
        // Arrange
        var expected = new Guid("b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94");

        // Act
        var first = LiveSender.ProfileId;
        var second = LiveSender.ProfileId;

        // Assert
        Assert.AreEqual(expected, first, "The profile identity should be the fixed identity of the plan");
        Assert.AreEqual(first, second, "Every read should give the same profile identity");
        Assert.AreEqual("Live", LiveSender.ProfileName, "The fixed profile should be named Live");
    }

    /// <summary>
    /// The run connects at once, before any pipe item or state, without waiting for the game or the clock (S1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveSenderRunConnectsBeforeAnyPipeItem()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        // Act
        StartRun(sender);

        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await WaitForStatusAsync(LiveSendState.Connected, null, 1);

        // Assert
        Assert.IsInstanceOfType<ContractHello>(message, "The hello should be sent without any pipe item");
        Assert.AreEqual(_start, timeProvider.GetUtcNow(), "The connection should not wait for the clock");
        Assert.AreEqual(1, connector.AttemptCount, "The run should connect once");
    }

    /// <summary>
    /// The status reads connecting before the hello is accepted and connected afterwards, without a problem (S1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveSenderStatusIsConnectingThenConnected()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);
        var before = sender.Status;

        // Act
        StartRun(sender);

        await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await WaitForStatusAsync(LiveSendState.Connected, null, 1);

        // Assert
        var statuses = Statuses();

        Assert.AreEqual(Status(LiveSendState.Connecting, null), before, "Before the run the status should read connecting");
        Assert.AreEqual(Status(LiveSendState.Connected, null), sender.Status, "After the accepted hello the status should read connected");
        Assert.AreEqual(Status(LiveSendState.Connected, null), statuses[^1], "The last raised status should be connected");
        Assert.IsEmpty(statuses.Where(status => status.State is not (LiveSendState.Connecting or LiveSendState.Connected)).ToList(), "Only connecting and connected should be raised");
    }

    /// <summary>
    /// After the accepted hello each tick, trade-routes item, session start and session end is sent in the order queued,
    /// each with its receive time, the fixed profile and a new identity, the next only after the acknowledgement of the
    /// one before (S2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderEnqueueSendsItemsInOrderOneInFlight()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);

        StartRun(sender);
        await AcceptAsync(server);

        // Act
        sender.Enqueue(TickItem(100, 1, 2));
        sender.Enqueue(TradeRoutesItem(1, 3));
        sender.Enqueue(Item(PipeTickResult.FromSessionStart("Capital"), 4));
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 5));

        var first = await ReceiveContentAsync(server);
        var second = ReceiveContentAsync(server);
        var isSecondBeforeAcknowledgement = second.IsCompleted;

        await server.AcknowledgeAsync(first, null, TestContext.CancellationToken);

        var messages = new List<ContractMessage?>
                       {
                           first,
                           await second.WaitAsync(_guard, TestContext.CancellationToken)
                       };

        messages.AddRange(await ReceiveAndAcknowledgeAsync(server, messages[1], 2));

        // Assert
        Assert.IsFalse(isSecondBeforeAcknowledgement, "The second message should wait for the acknowledgement of the first");

        var tick = Assert.IsInstanceOfType<ContractTick>(messages[0], "The first message should be the tick");
        var routes = Assert.IsInstanceOfType<ContractTradeRoutes>(messages[1], "The second message should be the trade routes");
        var start = Assert.IsInstanceOfType<ContractSessionStart>(messages[2], "The third message should be the session start");
        var end = Assert.IsInstanceOfType<ContractSessionEnd>(messages[3], "The fourth message should be the session end");

        Assert.AreEqual(_start.AddSeconds(2), tick.ReceiveTime, "The tick should carry its live receive time");
        Assert.AreEqual(_start.AddSeconds(3), routes.ReceiveTime, "The trade routes should carry their read stamp");
        Assert.AreEqual(_start.AddSeconds(4), start.ReceiveTime, "The session start should carry its read stamp");
        Assert.AreEqual(_start.AddSeconds(5), end.ReceiveTime, "The session end should carry its read stamp");
        Assert.AreSequenceEqual(new[] { LiveSender.ProfileId, LiveSender.ProfileId, LiveSender.ProfileId, LiveSender.ProfileId }, new[] { tick.ProfileId, routes.ProfileId, start.ProfileId, end.ProfileId }, "Every message should go to the fixed profile");
        Assert.HasCount(4, new[] { tick.Id, routes.Id, start.Id, end.Id }.Where(id => id != Guid.Empty).Distinct().ToList(), "Every message should have a new identity of its own");
    }

    /// <summary>
    /// A tick without islands has no message (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderEnqueueSkipsTickWithoutIslands()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);

        StartRun(sender);
        await AcceptAsync(server);

        // Act
        sender.Enqueue(TickItem(100, 0, 1));
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));

        var message = await ReceiveContentAsync(server);

        // Assert
        Assert.IsInstanceOfType<ContractSessionEnd>(message, "The tick without islands should not be sent");
    }

    /// <summary>
    /// The pipe state known before the connection is sent as a status after the accepted hello, before any item, and each
    /// change afterwards as a further status (S2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderNotifyStateSendsStatusAfterAcceptAndOnChange()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);

        sender.NotifyState(PipeConnectionState.Receiving);
        StartRun(sender);

        var hello = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var first = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        sender.NotifyState(PipeConnectionState.ProtocolMismatch);

        var second = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsInstanceOfType<ContractHello>(hello, "The hello should come first");
        Assert.AreEqual(ContractPipeState.Receiving, Assert.IsInstanceOfType<ContractStatus>(first, "A status should follow the accept").PipeState, "The first status should be the state known before the connection");
        Assert.AreEqual(ContractPipeState.ProtocolMismatch, Assert.IsInstanceOfType<ContractStatus>(second, "A change should be sent as a status").PipeState, "The second status should be the changed state");
    }

    /// <summary>
    /// A tick the contract writer refuses is not sent, is logged once, and the next item is sent (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderEnqueueWithWriterRefusalLogsAndSkips()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);

        StartRun(sender);
        await AcceptAsync(server);

        // Act
        sender.Enqueue(TickItem(100, ContractReader.MaxIslands + 1, 1));
        sender.Enqueue(TickItem(200, 1, 2));

        var message = await ReceiveContentAsync(server);

        await server.AcknowledgeAsync(message, null, TestContext.CancellationToken);
        await StopRunsAsync();

        // Assert
        var tick = Assert.IsInstanceOfType<ContractTick>(message, "The next tick should be sent");

        Assert.AreEqual(200L, tick.GameTime, "The refused tick should not be sent");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: "Live message not sent: the contract writer refused it" }, _logger.Entries.ToList(), "The writer refusal should be logged once as a warning");
    }

    /// <summary>
    /// A message the server acknowledges with a refusal is not sent again, its refusal kind is logged, and the next message
    /// follows (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderAcknowledgementWithRefusalIsNotResentAndLogged()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);

        StartRun(sender);
        await AcceptAsync(server);

        sender.Enqueue(TickItem(100, 1, 1));
        sender.Enqueue(TickItem(200, 1, 2));

        var first = await ReceiveContentAsync(server);

        // Act
        await server.AcknowledgeAsync(first, ContractAcknowledgementRefusal.Invalid, TestContext.CancellationToken);

        var second = await ReceiveContentAsync(server);

        await server.AcknowledgeAsync(second, null, TestContext.CancellationToken);
        await StopRunsAsync();

        // Assert
        Assert.AreEqual(100L, Assert.IsInstanceOfType<ContractTick>(first, "The first message should be the first tick").GameTime, "The first tick should be sent first");
        Assert.AreEqual(200L, Assert.IsInstanceOfType<ContractTick>(second, "The refused tick should not be sent again").GameTime, "The next tick should follow the refused one");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: "Server refused a live message: Invalid" }, _logger.Entries.ToList(), "The refusal should be logged once with its kind");
    }

    /// <summary>
    /// An unreachable server is retried after 1, 2, 4, 8, 16 and then 30 seconds, each failure raising retrying with its
    /// problem (S4, N4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderRunRetriesWithGrowingDelays()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, timeProvider, Token);

        EnqueueUnreachable(connector, _sevenRetryDelays.Length);

        // Act
        StartRun(sender);

        await AdvanceThroughTimersAsync(timeProvider, _sevenRetryDelays.Length);
        await connector.WaitForAttemptsAsync(_sevenRetryDelays.Length + 1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(_sevenRetryDelays, timeProvider.Delays.ToArray(), "The retries should wait 1, 2, 4, 8, 16, 30 and 30 seconds");
        Assert.AreSequenceEqual(Enumerable.Repeat(Status(LiveSendState.Retrying, ReplaySendProblemKind.ServerUnreachable), _sevenRetryDelays.Length).ToArray(),
                                Statuses().Where(status => status.State == LiveSendState.Retrying).ToArray(),
                                "Each failure should raise retrying with server unreachable");
    }

    /// <summary>
    /// A connection that stayed accepted for 60 seconds resets the delays, so its loss is retried after 1 second (N4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderRunResetsDelaysAfterSixtySecondsConnected()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, timeProvider, Token);

        EnqueueUnreachable(connector, 2);

        var server = AddServer(connector);

        StartRun(sender);

        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(2), 1);
        await AcceptAsync(server);
        await WaitForStatusAsync(LiveSendState.Connected, null, 1);

        timeProvider.Advance(LiveSender.SuccessfulConnectionTime);

        // Act
        server.Abort();

        await WaitForStatusAsync(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost, 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 2);
        await connector.WaitForAttemptsAsync(4, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(4, connector.AttemptCount, "The lost connection should be retried after 1 second");
        Assert.AreEqual(TimeSpan.FromSeconds(60), LiveSender.SuccessfulConnectionTime, "The delays should reset after 60 seconds connected");
    }

    /// <summary>
    /// A connection the server ends while a message is in flight shows connection lost, retrying, and the next attempt
    /// connects again (S3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderRunReconnectsAfterConnectionLost()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        StartRun(sender);
        await AcceptAsync(first);
        sender.Enqueue(TickItem(100, 1, 1));
        await ReceiveContentAsync(first);

        var second = AddServer(connector);

        // Act
        first.Abort();

        await WaitForStatusAsync(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost, 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AcceptAsync(second);
        await WaitForStatusAsync(LiveSendState.Connected, null, 2);

        // Assert
        Assert.AreEqual(Status(LiveSendState.Connected, null), sender.Status, "The sender should be connected again");
        Assert.AreEqual(2, connector.AttemptCount, "The sender should have connected twice");
    }

    /// <summary>
    /// A hello refused because another Companion is connected shows that, retrying, and the next attempt connects (E3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderRunRetriesWhileAnotherCompanionIsConnected()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var second = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        StartRun(sender);

        // Act
        await RefuseHelloAsync(first, ContractHelloRefusal.CompanionConnected);
        await WaitForStatusAsync(LiveSendState.Retrying, ReplaySendProblemKind.CompanionConnected, 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AcceptAsync(second);
        await WaitForStatusAsync(LiveSendState.Connected, null, 1);

        // Assert
        Assert.AreEqual(Status(LiveSendState.Connected, null), sender.Status, "The sender should connect once the place is free");
        Assert.AreEqual(2, connector.AttemptCount, "The refused hello should be retried once");
    }

    /// <summary>
    /// A connection that fails while no message is in flight, as a dead idle connection whose pending receive fails, shows
    /// connection lost, retrying, and the next attempt connects (E11).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderRunReconnectsAfterIdleConnectionFails()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var second = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        StartRun(sender);
        await AcceptAsync(first);

        // Act
        first.Abort();

        await WaitForStatusAsync(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost, 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AcceptAsync(second);
        await WaitForStatusAsync(LiveSendState.Connected, null, 2);

        // Assert
        Assert.AreEqual(Status(LiveSendState.Connected, null), sender.Status, "The sender should be connected again");
        Assert.AreEqual(2, connector.AttemptCount, "The idle connection's failure should be retried once");
    }

    /// <summary>
    /// The message in flight when the connection was lost is sent first after the reconnect, with its identity, and the
    /// messages waiting behind it follow in order (S3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LiveSenderRunResendsUnacknowledgedMessageFirstWithSameId()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        StartRun(sender);
        await AcceptAsync(first);
        sender.Enqueue(TickItem(100, 1, 1));
        sender.Enqueue(TickItem(200, 1, 2));

        var lost = await ReceiveContentAsync(first);
        var second = AddServer(connector);

        // Act
        first.Abort();

        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AcceptAsync(second);

        var resent = await ReceiveContentAsync(second);
        var next = await ReceiveAndAcknowledgeAsync(second, resent, 1);

        // Assert
        var lostTick = Assert.IsInstanceOfType<ContractTick>(lost, "The lost message should be the first tick");
        var resentTick = Assert.IsInstanceOfType<ContractTick>(resent, "The first message after the reconnect should be a tick");

        Assert.AreEqual(lostTick.Id, resentTick.Id, "The unacknowledged message should be sent again with its identity");
        Assert.AreEqual(100L, resentTick.GameTime, "The unacknowledged tick should be sent first");
        Assert.AreEqual(200L, Assert.IsInstanceOfType<ContractTick>(next.Single(), "The waiting tick should follow").GameTime, "The waiting tick should follow the resent one");
    }

    /// <summary>
    /// Beyond 64 waiting messages the oldest are dropped, and the newest 64 messages are sent in order: 63 ticks and the
    /// session end, the oldest 2 dropped (E5, N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LiveSenderEnqueueBeyondCountBoundDropsOldest()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, CreateTimeProvider(), Token);
        var expected = Enumerable.Range(3, LiveSender.MaxWaitingMessages - 1).Select(gameTime => (long)gameTime).ToArray();

        EnqueueTicks(sender, LiveSender.MaxWaitingMessages + 1);

        var server = AddServer(connector);

        // Act
        StartRun(sender);
        await AcceptAsync(server);

        var messages = await ReceiveAndAcknowledgeAsync(server, null, LiveSender.MaxWaitingMessages);

        // Assert
        Assert.AreEqual(64, LiveSender.MaxWaitingMessages, "The bound should be 64 messages");
        Assert.HasCount(LiveSender.MaxWaitingMessages, messages, "The newest 64 messages should be sent: 63 ticks and the session end, the oldest 2 dropped");
        Assert.AreSequenceEqual(expected, messages.Take(LiveSender.MaxWaitingMessages - 1).Select(message => Assert.IsInstanceOfType<ContractTick>(message, "Every message but the last should be a tick").GameTime).ToArray(), "The newest 64 messages should be sent in order: 63 ticks and the session end, the oldest 2 dropped");
        Assert.IsInstanceOfType<ContractSessionEnd>(messages[^1], "The item queued after the drop should follow the kept ticks");
    }

    /// <summary>
    /// Beyond 16 MiB of written JSON the oldest waiting messages are dropped, the newest kept (E5, N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LiveSenderEnqueueBeyondByteBoundDropsOldest()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, CreateTimeProvider(), Token);
        var size = ContractWriter.Write(ContractMessageConverter.ToTradeRoutes(CreateLargeTradeRoutes(1), LiveSender.ProfileId, _start)).Utf8Json.Length;

        EnqueueLargeTradeRoutes(sender, LargeMessages);

        var server = AddServer(connector);

        // Act
        StartRun(sender);
        await AcceptAsync(server);

        var messages = await ReceiveAndAcknowledgeAsync(server, null, LargeMessages - 1);

        sender.Enqueue(Item(PipeTickResult.SessionEnd, 9));

        var next = await ReceiveContentAsync(server);

        // Assert
        Assert.AreEqual(16777216, LiveSender.MaxWaitingBytes, "The bound should be 16 MiB");
        Assert.IsLessThanOrEqualTo((long)LiveSender.MaxWaitingBytes, (LargeMessages - 1L) * size, $"Eight large messages of {size} bytes should fit the bound");
        Assert.IsGreaterThan((long)LiveSender.MaxWaitingBytes, (long)LargeMessages * size, $"Nine large messages of {size} bytes should exceed the bound");
        Assert.AreSequenceEqual(_keptRouteIds, messages.Select(message => Assert.IsInstanceOfType<ContractTradeRoutes>(message, "Every message should be trade routes").Routes[0].Id).ToArray(), "The oldest message should be dropped and the newest eight sent, below the count bound");
        Assert.IsInstanceOfType<ContractSessionEnd>(next, "No further large message should follow");
    }

    /// <summary>
    /// The number of dropped messages is logged once, at the acknowledgement after the reconnect (E5, N6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LiveSenderRunLogsDroppedCountOnce()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, CreateTimeProvider(), Token);

        EnqueueTicks(sender, LiveSender.MaxWaitingMessages + 1);

        var server = AddServer(connector);

        StartRun(sender);
        await AcceptAsync(server);

        // Act
        await ReceiveAndAcknowledgeAsync(server, null, LiveSender.MaxWaitingMessages);
        await StopRunsAsync();

        // Assert
        Assert.AreSequenceEqual(_droppedTwoLines, DroppedLines(), "The dropped count should be logged once");
    }

    /// <summary>
    /// A dropped count that no acknowledgement logged yet is logged when the server refuses for good or the sender is
    /// disposed (E5).
    /// </summary>
    /// <param name="ending">How live sending ends: token refused or disposed</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("token refused")]
    [DataRow("disposed")]
    public async Task LiveSenderRunLogsUnloggedDroppedCountWhenStoppedOrDisposed(string ending)
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, timeProvider, Token);

        EnqueueTicks(sender, LiveSender.MaxWaitingMessages + 1);

        // Act
        await EndAsync(sender, connector, timeProvider, ending);

        // Assert
        Assert.AreSequenceEqual(_droppedTwoLines, DroppedLines(), $"The unlogged dropped count should be logged once when {ending}");
    }

    /// <summary>
    /// A refused token stops live sending: the status reads stopped with the problem, the refusal is logged and no attempt
    /// follows, also after 60 seconds (E6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderRunStopsOnTokenRefused()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        // Act
        var run = await RunUntilRefusedAsync(sender, server, timeProvider, ContractHelloRefusal.TokenRefused);

        // Assert
        Assert.IsTrue(run.IsCompletedSuccessfully, "The run should end after the refusal");
        Assert.AreEqual(Status(LiveSendState.Stopped, ReplaySendProblemKind.TokenRefused), sender.Status, "The status should read stopped with the token refused");
        Assert.AreEqual(1, connector.AttemptCount, "A refused token should not be retried");
        Assert.AreSequenceEqual(_tokenRefusedLines, Lines(), "The refusal should be logged once");
    }

    /// <summary>
    /// A server that does not support contract version 2 stops live sending: the status reads stopped with the problem,
    /// the refusal is logged and no attempt follows, also after 60 seconds (E7).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderRunStopsOnUnsupportedVersion()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, timeProvider, Token);

        // Act
        var run = await RunUntilRefusedAsync(sender, server, timeProvider, ContractHelloRefusal.UnsupportedVersion);

        // Assert
        Assert.IsTrue(run.IsCompletedSuccessfully, "The run should end after the refusal");
        Assert.AreEqual(Status(LiveSendState.Stopped, ReplaySendProblemKind.VersionNotSupported), sender.Status, "The status should read stopped with the version not supported");
        Assert.AreEqual(1, connector.AttemptCount, "An unsupported version should not be retried");
        Assert.AreSequenceEqual(_versionRefusedLines, Lines(), "The refusal should be logged once");
    }

    /// <summary>
    /// The sender for a server connects to the address of the forward setting as given.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task LiveSenderForServerConnectsToGivenAddress()
    {
        // Arrange
        var address = new Uri("wss://censio.example.org:8443/ingest?profile=1");
        var connector = new QueuedIngestConnector();
        var sender = LiveSender.ForServer(new ForwardServerSetting(address, Token), CompanionVersion, connector, CreateTimeProvider(), _logger);

        _senders.Add(sender);

        // Act
        StartRun(sender);

        await connector.WaitForAttemptsAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual([address], connector.Addresses, "The connector should be asked for the address of the setting");
    }

    /// <summary>
    /// In local mode the sender connects to ws://127.0.0.1:&lt;port&gt;/ingest with the token the process holds (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveSenderForLocalServerConnectsToLoopbackWithHeldToken()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateLocalSender(connector, CreateTimeProvider(), _ => Task.FromResult<string?>(Token));

        // Act
        StartRun(sender);

        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var hello = Assert.IsInstanceOfType<ContractHello>(message, "The first message should be the hello");

        Assert.AreEqual(Token, hello.Token, "The hello should carry the token of the local server");
        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should carry the fixed profile identity");
        Assert.AreSequenceEqual([new Uri("ws://127.0.0.1:5117/ingest")], connector.Addresses, "The sender should connect to the local server's loopback address");
    }

    /// <summary>
    /// In local mode a server that does not run (its wait gives no token) stops live sending without an attempt (E10).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveSenderForLocalServerStopsWhenServerNotRunning()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var sender = CreateLocalSender(connector, CreateTimeProvider(), _ => Task.FromResult<string?>(null));

        // Act
        var run = StartTrackedRun(sender);

        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LiveSendState.Stopped, sender.Status.State, "A local server that does not run should stop live sending");
        Assert.AreEqual(0, connector.AttemptCount, "No connection should be attempted");
    }

    /// <summary>
    /// Live data is sent in the normal mode and while recording, with a forward or a local setting, and never in a replay,
    /// with an invalid command line, without a setting or with an invalid one (S6).
    /// </summary>
    /// <param name="mode">The mode of the command line</param>
    /// <param name="kind">The kind of the setting</param>
    /// <param name="expected">Whether live data is sent</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Forward, true)]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Forward, true)]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Local, true)]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Local, true)]
    [DataRow(CompanionMode.Replay, LocalServerSettingKind.Forward, false)]
    [DataRow(CompanionMode.Replay, LocalServerSettingKind.Local, false)]
    [DataRow(CompanionMode.Invalid, LocalServerSettingKind.Forward, false)]
    [DataRow(CompanionMode.Invalid, LocalServerSettingKind.Local, false)]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Off, false)]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Off, false)]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Invalid, false)]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Invalid, false)]
    public void LiveSenderIsSendingPerModeAndSetting(CompanionMode mode, LocalServerSettingKind kind, bool expected)
    {
        // Arrange
        var setting = CreateSetting(kind);

        // Act
        var isSending = LiveSender.IsSending(mode, setting);

        // Assert
        Assert.AreEqual(expected, isSending, $"A {kind} setting with the mode {mode} should send live data: {expected}");
    }

    /// <summary>
    /// Disposing the sender ends its connection at once, also with a message in flight, and ends the run (E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task LiveSenderDisposeAsyncAbortsHeldConnection()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider(), Token);
        var run = StartTrackedRun(sender);

        await AcceptAsync(server);
        sender.Enqueue(TickItem(100, 1, 1));
        await ReceiveContentAsync(server);

        // Act
        await sender.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);

        var after = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await Task.WhenAny(run).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(after, "The connection should be ended by the dispose, with no further message");
        Assert.IsTrue(run.IsCompleted, "The run should end once the sender is disposed");
    }

    /// <summary>
    /// A run through every failure kind logs each change of reason once, with fixed texts only: never the token, the
    /// address or an exception (N5, N6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task LiveSenderLogNeverHoldsTokenAddressOrExceptionText()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, timeProvider, Token);
        var expected = new[]
                       {
                           "Live sending failed: server unreachable; retrying",
                           "Live sending failed: server does not answer; retrying",
                           "Live sending connected to the server",
                           "Live sending failed: server connection lost; retrying",
                           "Live sending failed: invalid server reply; retrying",
                           "Live sending failed: another Companion is connected; retrying",
                           "Live sending refused by the server: token refused; not retried"
                       };

        // Act
        await RunThroughEveryFailureAsync(sender, connector, timeProvider);

        // Assert
        var entries = _logger.Entries.ToList();

        Assert.AreSequenceEqual(expected, entries.Select(entry => entry.Message).ToArray(), "Each change of reason should be logged once with its fixed text");
        Assert.IsEmpty(entries.Where(entry => entry.Exception is not null).ToList(), "No entry should carry an exception");
        Assert.IsEmpty(entries.Where(entry => entry.Message.Contains(Token, StringComparison.Ordinal) || entry.Message.Contains(ServerHost, StringComparison.OrdinalIgnoreCase) || entry.Message.Contains("hidden", StringComparison.Ordinal) || entry.Message.Contains("Exception", StringComparison.Ordinal)).ToList(),
                       "No entry should hold the token, the address or an exception text");
    }

    /// <summary>
    /// A failure is logged when its reason differs from the last one logged, not at every retry (N6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task LiveSenderRunLogsFailureOncePerReason()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var sender = CreateSender(connector, timeProvider, Token);
        var expected = new[]
                       {
                           "Live sending failed: server unreachable; retrying",
                           "Live sending failed: another Companion is connected; retrying",
                           "Live sending failed: server unreachable; retrying",
                           "Live sending connected to the server"
                       };

        // Act
        await RunThroughRepeatedFailuresAsync(sender, connector, timeProvider);

        // Assert
        Assert.AreSequenceEqual(expected, Lines(), "A failure should be logged only when its reason changes");
    }

    /// <summary>
    /// A token reader that throws, with a host name in its text, takes the retry path as a lost connection without logging
    /// the exception, and the next attempt connects (N5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task LiveSenderRunWithThrowingTokenReaderLogsNoExceptionAndRetries()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateLocalSender(connector, timeProvider, CreateThrowingOnceTokenReader());
        var run = StartTrackedRun(sender);

        // Act
        await WaitForStatusAsync(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost, 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AcceptAsync(server);
        await WaitForStatusAsync(LiveSendState.Connected, null, 1);
        await StopRunsAsync();

        // Assert
        var entries = _logger.Entries.ToList();

        Assert.IsFalse(run.IsFaulted, "The exception of the token reader should not escape the run");
        Assert.AreSequenceEqual(_lostThenConnectedLines, entries.Select(entry => entry.Message).ToArray(), "The failure should be logged as a lost connection, then the connection");
        Assert.IsEmpty(entries.Where(entry => entry.Exception is not null || entry.Message.Contains(ServerHost, StringComparison.OrdinalIgnoreCase)).ToList(), "No entry should carry the exception or its text");
    }

    /// <summary>
    /// Creates a time provider that records its timers.
    /// </summary>
    /// <returns>The time provider</returns>
    private static RecordingTimeProvider CreateTimeProvider()
    {
        return new RecordingTimeProvider(_start);
    }

    /// <summary>
    /// Creates a status.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="problem">The problem</param>
    /// <returns>The status</returns>
    private static LiveSendStatus Status(LiveSendState state, ReplaySendProblemKind? problem)
    {
        return new LiveSendStatus
               {
                   State = state,
                   Problem = problem
               };
    }

    /// <summary>
    /// Creates a setting of a kind; a forward setting carries the address and the token of the tests.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The setting</returns>
    private static LocalServerSetting CreateSetting(LocalServerSettingKind kind)
    {
        return new LocalServerSetting
               {
                   Kind = kind,
                   Port = kind == LocalServerSettingKind.Local ? LocalPort : 0,
                   Problem = kind == LocalServerSettingKind.Invalid ? "serverAddress is missing" : null,
                   Forward = kind == LocalServerSettingKind.Forward ? new ForwardServerSetting(_address, Token) : null
               };
    }

    /// <summary>
    /// Creates an item of the pipe connection.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="readSeconds">The read stamp in seconds after the start</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem Item(PipeTickResult result, int readSeconds)
    {
        return new PipeConnectionItem
               {
                   Result = result,
                   ReadStamp = _start.AddSeconds(readSeconds)
               };
    }

    /// <summary>
    /// Creates the item of a tick whose receive time is one second before its read stamp.
    /// </summary>
    /// <param name="gameTime">The game time, which identifies the tick</param>
    /// <param name="islands">The number of islands</param>
    /// <param name="receiveSeconds">The receive time in seconds after the start</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem TickItem(long gameTime, int islands, int receiveSeconds)
    {
        var tick = new PipeTick
                   {
                       GameTime = gameTime,
                       ReceiveTime = _start.AddSeconds(receiveSeconds),
                       Islands = [.. Enumerable.Range(0, islands).Select(CreateIsland)],
                       CloseReason = PipeTickCloseReason.GameTimeChanged
                   };

        return Item(PipeTickResult.FromTick(tick), receiveSeconds + 1);
    }

    /// <summary>
    /// Creates the item of trade routes with one route without stations.
    /// </summary>
    /// <param name="routeId">The identity of the route, which identifies the message</param>
    /// <param name="readSeconds">The read stamp in seconds after the start</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem TradeRoutesItem(int routeId, int readSeconds)
    {
        var message = new TradeRoutesMessage
                      {
                          Routes = [CreateRoute(routeId, [])]
                      };

        return Item(PipeTickResult.FromTradeRoutes(message), readSeconds);
    }

    /// <summary>
    /// Creates the item of large trade routes.
    /// </summary>
    /// <param name="firstRouteId">The identity of the first route, which identifies the message</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem LargeTradeRoutesItem(int firstRouteId)
    {
        return Item(PipeTickResult.FromTradeRoutes(CreateLargeTradeRoutes(firstRouteId)), firstRouteId);
    }

    /// <summary>
    /// Creates trade routes of <see cref="LargeRoutes" /> routes of <see cref="StationsPerRoute" /> stations each.
    /// </summary>
    /// <param name="firstRouteId">The identity of the first route</param>
    /// <returns>The trade routes</returns>
    private static TradeRoutesMessage CreateLargeTradeRoutes(int firstRouteId)
    {
        var stations = Enumerable.Range(0, StationsPerRoute).Select(CreateStation).ToArray();

        return new TradeRoutesMessage
               {
                   Routes = [CreateRoute(firstRouteId, stations), .. Enumerable.Range(1, LargeRoutes - 1).Select(index => CreateRoute(1000 + index, stations))]
               };
    }

    /// <summary>
    /// Creates a trade route without problems or lost ships.
    /// </summary>
    /// <param name="id">The identity of the route</param>
    /// <param name="stations">The stations in route order</param>
    /// <returns>The route</returns>
    private static TradeRoute CreateRoute(int id, TradeRouteStation[] stations)
    {
        return new TradeRoute
               {
                   Id = id,
                   Name = "Via Appia",
                   Folder = "North",
                   MajorProblems = 0,
                   MinorProblems = 0,
                   Ships = 2,
                   LostShips = 0,
                   Stations = stations
               };
    }

    /// <summary>
    /// Creates a station without goods.
    /// </summary>
    /// <param name="index">The index of the station, which gives its island ID</param>
    /// <returns>The station</returns>
    private static TradeRouteStation CreateStation(int index)
    {
        return new TradeRouteStation
               {
                   Id = index,
                   SessionId = 1,
                   IslandId = (byte)(index % 256),
                   AreaIndex = 0,
                   SessionGuid = 180023,
                   Owner = 41,
                   ThrowOverboard = false,
                   WaitUntilFull = false,
                   WaitUntilEmpty = false,
                   Goods = []
               };
    }

    /// <summary>
    /// Creates an island without entries.
    /// </summary>
    /// <param name="index">The index of the island, which makes its key distinct</param>
    /// <returns>The island</returns>
    private static PipeTickIsland CreateIsland(int index)
    {
        return new PipeTickIsland
               {
                   Key = new PipeIslandKey(180023, (byte)(index % 256), (byte)(index / 256)),
                   SessionId = 1,
                   Name = "Isle Of Hidden Names",
                   Entries = []
               };
    }

    /// <summary>
    /// Queues ticks with the game times 1 to the count, followed by a session end.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="count">The number of ticks</param>
    private static void EnqueueTicks(LiveSender sender, int count)
    {
        for (var gameTime = 1; gameTime <= count; gameTime++)
        {
            sender.Enqueue(TickItem(gameTime, 1, 1));
        }

        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));
    }

    /// <summary>
    /// Queues large trade-routes messages whose first routes have the identities 1 to the count.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="count">The number of messages</param>
    private static void EnqueueLargeTradeRoutes(LiveSender sender, int count)
    {
        for (var routeId = 1; routeId <= count; routeId++)
        {
            sender.Enqueue(LargeTradeRoutesItem(routeId));
        }
    }

    /// <summary>
    /// Queues attempts of a server that cannot be reached.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="count">The number of attempts</param>
    private static void EnqueueUnreachable(QueuedIngestConnector connector, int count)
    {
        for (var index = 0; index < count; index++)
        {
            connector.Enqueue(null);
        }
    }

    /// <summary>
    /// Creates a token reader that throws an exception with a host name in its text at its first call and returns the
    /// token afterwards.
    /// </summary>
    /// <returns>The token reader</returns>
    private static Func<CancellationToken, Task<string?>> CreateThrowingOnceTokenReader()
    {
        var calls = 0;

        return _ => Interlocked.Increment(ref calls) == 1
                        ? throw new InvalidOperationException($"The name {ServerHost} could not be resolved")
                        : Task.FromResult<string?>(Token);
    }

    /// <summary>
    /// Creates a sender for the server of the tests and records its statuses.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="token">The token of the setting</param>
    /// <returns>The sender</returns>
    private LiveSender CreateSender(QueuedIngestConnector connector, TimeProvider timeProvider, string? token)
    {
        return Track(LiveSender.ForServer(new ForwardServerSetting(_address, token), CompanionVersion, connector, timeProvider, _logger));
    }

    /// <summary>
    /// Creates a sender for the local server of the tests and records its statuses.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="waitForToken">The wait for the token of the local server</param>
    /// <returns>The sender</returns>
    private LiveSender CreateLocalSender(QueuedIngestConnector connector, TimeProvider timeProvider, Func<CancellationToken, Task<string?>> waitForToken)
    {
        return Track(LiveSender.ForLocalServer(LocalPort, waitForToken, CompanionVersion, connector, timeProvider, _logger));
    }

    /// <summary>
    /// Records the statuses of a sender and disposes it after the test.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <returns>The sender</returns>
    private LiveSender Track(LiveSender sender)
    {
        sender.StatusChanged += OnStatusChanged;
        _senders.Add(sender);

        return sender;
    }

    /// <summary>
    /// Records a status and ends the waits it completes.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="status">The status</param>
    private void OnStatusChanged(object? sender, LiveSendStatus status)
    {
        lock (_lock)
        {
            _statuses.Add(status);

            foreach (var waiter in _statusWaiters.Where(waiter => waiter.IsReached(_statuses)).ToList())
            {
                waiter.Source.TrySetResult();
                _statusWaiters.Remove(waiter);
            }
        }
    }

    /// <summary>
    /// Returns the statuses raised so far.
    /// </summary>
    /// <returns>The statuses in order</returns>
    private List<LiveSendStatus> Statuses()
    {
        lock (_lock)
        {
            return [.. _statuses];
        }
    }

    /// <summary>
    /// Waits until a status was raised a number of times.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="problem">The problem</param>
    /// <param name="occurrence">The number of times</param>
    /// <returns>A task that completes when the status was raised as often</returns>
    private Task WaitForStatusAsync(LiveSendState state, ReplaySendProblemKind? problem, int occurrence)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = Status(state, problem);

        bool IsReached(List<LiveSendStatus> statuses)
        {
            return statuses.Count(status => status == expected) >= occurrence;
        }

        lock (_lock)
        {
            if (IsReached(_statuses))
            {
                source.SetResult();
            }
            else
            {
                _statusWaiters.Add((IsReached, source));
            }
        }

        return source.Task.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Starts a run that the test or its cleanup stops.
    /// </summary>
    /// <param name="sender">The sender</param>
    private void StartRun(LiveSender sender)
    {
        _runs.Add(sender.RunAsync(_stop.Token));
    }

    /// <summary>
    /// Starts a run that the test or its cleanup stops, and returns it.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <returns>The run</returns>
    private Task StartTrackedRun(LiveSender sender)
    {
        StartRun(sender);

        return _runs[^1];
    }

    /// <summary>
    /// Cancels the runs and waits until they ended.
    /// </summary>
    /// <returns>A task that completes when the runs ended</returns>
    private async Task StopRunsAsync()
    {
        await _stop.CancelAsync();

        foreach (var run in _runs)
        {
            try
            {
                await run.WaitAsync(_guard, TestContext.CancellationToken);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                // The run ends with the cancellation of the test.
            }
        }
    }

    /// <summary>
    /// Creates a scripted server whose socket the next attempt without one returns.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <returns>The server</returns>
    private ScriptedIngestServer AddServer(QueuedIngestConnector connector)
    {
        var server = ScriptedIngestServer.Create();

        _servers.Add(server);
        connector.Enqueue(server.ClientSocket);

        return server;
    }

    /// <summary>
    /// Accepts the hello of a server's connection.
    /// </summary>
    /// <param name="server">The server</param>
    /// <returns>A task that completes when the hello is accepted</returns>
    private async Task AcceptAsync(ScriptedIngestServer server)
    {
        var hello = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        Assert.IsInstanceOfType<ContractHello>(hello, "The first message should be the hello");
    }

    /// <summary>
    /// Refuses the hello of a server's connection.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="reason">The reason</param>
    /// <returns>A task that completes when the refusal is sent</returns>
    private async Task RefuseHelloAsync(ScriptedIngestServer server, ContractHelloRefusal reason)
    {
        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await server.SendAsync(new ContractHelloRefused
                               {
                                   Reason = reason,
                                   ReceivedVersion = 2,
                                   SupportedVersions = [2]
                               },
                               TestContext.CancellationToken);
    }

    /// <summary>
    /// Receives the next message that is not a status.
    /// </summary>
    /// <param name="server">The server</param>
    /// <returns>The message, or <c>null</c> when the connection ended</returns>
    private async Task<ContractMessage?> ReceiveContentAsync(ScriptedIngestServer server)
    {
        var message = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        while (message is ContractStatus)
        {
            message = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        return message;
    }

    /// <summary>
    /// Acknowledges a message, if any, then receives and acknowledges a number of further messages that are not statuses.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="pending">The received message still to acknowledge, or <c>null</c></param>
    /// <param name="count">The number of further messages</param>
    /// <returns>The further messages in order</returns>
    private async Task<List<ContractMessage?>> ReceiveAndAcknowledgeAsync(ScriptedIngestServer server, ContractMessage? pending, int count)
    {
        var messages = new List<ContractMessage?>();

        if (pending is not null)
        {
            await server.AcknowledgeAsync(pending, null, TestContext.CancellationToken);
        }

        for (var index = 0; index < count; index++)
        {
            var message = await ReceiveContentAsync(server);

            messages.Add(message);
            await server.AcknowledgeAsync(message, null, TestContext.CancellationToken);
        }

        return messages;
    }

    /// <summary>
    /// Waits until a timer of a due time was created a number of times, then advances the clock by that time.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="delay">The due time</param>
    /// <param name="occurrence">The number of timers of that due time to wait for</param>
    /// <returns>A task that completes when the clock was advanced</returns>
    private async Task AdvanceRetryAsync(RecordingTimeProvider timeProvider, TimeSpan delay, int occurrence)
    {
        while (timeProvider.Delays.Count(due => due == delay) < occurrence)
        {
            await timeProvider.WaitForTimersAsync(timeProvider.Delays.Count + 1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        timeProvider.Advance(delay);
    }

    /// <summary>
    /// Advances the clock through a number of timers, each by its due time, once it was created.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="count">The number of timers</param>
    /// <returns>A task that completes when the clock was advanced through the timers</returns>
    private async Task AdvanceThroughTimersAsync(RecordingTimeProvider timeProvider, int count)
    {
        for (var index = 0; index < count; index++)
        {
            await timeProvider.WaitForTimersAsync(index + 1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
            timeProvider.Advance(timeProvider.Delays[index]);
        }
    }

    /// <summary>
    /// Runs until the server refuses the hello for good, then advances the clock by 60 seconds.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="reason">The reason of the refusal</param>
    /// <returns>The run, ended</returns>
    private async Task<Task> RunUntilRefusedAsync(LiveSender sender, ScriptedIngestServer server, RecordingTimeProvider timeProvider, ContractHelloRefusal reason)
    {
        var run = StartTrackedRun(sender);

        await RefuseHelloAsync(server, reason);
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(60));

        return run;
    }

    /// <summary>
    /// Ends live sending after messages were dropped: by a refused token or by disposing the sender, and waits until the run
    /// ended.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider of the sender</param>
    /// <param name="ending">How live sending ends: token refused or disposed</param>
    /// <returns>A task that completes when the run ended</returns>
    private async Task EndAsync(LiveSender sender, QueuedIngestConnector connector, RecordingTimeProvider timeProvider, string ending)
    {
        if (ending == "token refused")
        {
            var server = AddServer(connector);

            await RunUntilRefusedAsync(sender, server, timeProvider, ContractHelloRefusal.TokenRefused);

            return;
        }

        StartRun(sender);

        await connector.WaitForAttemptsAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await sender.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        await StopRunsAsync();
    }

    /// <summary>
    /// Runs through every failure kind: unreachable, no answer, a lost connection after an accepted hello, an invalid reply,
    /// another Companion connected and finally a refused token, and waits until the run ended.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>A task that completes when the run ended</returns>
    private async Task RunThroughEveryFailureAsync(LiveSender sender, QueuedIngestConnector connector, RecordingTimeProvider timeProvider)
    {
        connector.Enqueue(null);

        var silent = AddServer(connector);
        var lost = AddServer(connector);
        var invalid = AddServer(connector);
        var busy = AddServer(connector);
        var refusing = AddServer(connector);
        var run = StartTrackedRun(sender);

        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await silent.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await AdvanceRetryAsync(timeProvider, IngestClient.ReplyTimeout, 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(2), 1);
        await AcceptAsync(lost);
        await WaitForStatusAsync(LiveSendState.Connected, null, 1);
        lost.Abort();
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(4), 1);
        await invalid.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await invalid.SendRawAsync("not a contract message"u8.ToArray(), WebSocketMessageType.Text, TestContext.CancellationToken);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(8), 1);
        await RefuseHelloAsync(busy, ContractHelloRefusal.CompanionConnected);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(16), 1);
        await RefuseHelloAsync(refusing, ContractHelloRefusal.TokenRefused);
        await run.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Runs through an unreachable server twice, another Companion connected twice, the unreachable server again and an
    /// accepted hello, then stops the run.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>A task that completes when the run ended</returns>
    private async Task RunThroughRepeatedFailuresAsync(LiveSender sender, QueuedIngestConnector connector, RecordingTimeProvider timeProvider)
    {
        EnqueueUnreachable(connector, 2);

        var firstBusy = AddServer(connector);
        var secondBusy = AddServer(connector);

        connector.Enqueue(null);

        var accepting = AddServer(connector);

        StartRun(sender);

        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(2), 1);
        await RefuseHelloAsync(firstBusy, ContractHelloRefusal.CompanionConnected);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(4), 1);
        await RefuseHelloAsync(secondBusy, ContractHelloRefusal.CompanionConnected);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(8), 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(16), 1);
        await AcceptAsync(accepting);
        await WaitForStatusAsync(LiveSendState.Connected, null, 1);
        await StopRunsAsync();
    }

    /// <summary>
    /// Returns the messages of the log entries so far.
    /// </summary>
    /// <returns>The messages in order</returns>
    private string[] Lines()
    {
        return [.. _logger.Entries.Select(entry => entry.Message)];
    }

    /// <summary>
    /// Returns the log lines of dropped messages so far.
    /// </summary>
    /// <returns>The lines in order</returns>
    private string[] DroppedLines()
    {
        return [.. Lines().Where(line => line.StartsWith("Live messages dropped", StringComparison.Ordinal))];
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Dispose();
    }

    #endregion // IDisposable
}