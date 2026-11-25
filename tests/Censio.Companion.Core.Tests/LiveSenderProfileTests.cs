using System.Security.Cryptography;
using System.Text;

using Censio.Contracts;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for the profiles of <see cref="LiveSender" />: the profile of each message, the hello of each connection, the
/// reconnect of a switch and the notices of a session start.
/// </summary>
[TestClass]
public sealed class LiveSenderProfileTests : IDisposable
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
    /// The port of the local server.
    /// </summary>
    private const int LocalPort = 5117;

    /// <summary>
    /// The name of the profile created in the tests, a marker no log line may hold.
    /// </summary>
    private const string SecondName = "MARKER-Second";

    /// <summary>
    /// The name of the figure of the tests, a marker no log line may hold.
    /// </summary>
    private const string Figure = "MARKER-Figure Cæsar";

    /// <summary>
    /// The beginning of the log line of a failed attempt.
    /// </summary>
    private const string FailedLinePrefix = "Live sending failed";

    /// <summary>
    /// The number of ten-millisecond polls of a condition before it counts as not reached.
    /// </summary>
    private const int MaxPolls = 500;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start time of the time provider.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address of the server.
    /// </summary>
    private static readonly Uri _address = new("ws://profiles-host.example:8080/ingest");

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The logger of the senders.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    /// <summary>
    /// The logger of the profiles.
    /// </summary>
    private readonly ConcurrentRecordingLogger _profilesLogger = new();

    /// <summary>
    /// The logger of the buffers.
    /// </summary>
    private readonly ConcurrentRecordingLogger _bufferLogger = new();

    /// <summary>
    /// The statuses the senders raised, in order.
    /// </summary>
    private readonly List<LiveSendStatus> _statuses = [];

    /// <summary>
    /// The notices the senders raised, in order.
    /// </summary>
    private readonly List<ProfileNotice> _notices = [];

    /// <summary>
    /// The tests waiting for statuses, each with the condition on the statuses that ends its wait.
    /// </summary>
    private readonly List<(Func<List<LiveSendStatus>, bool> IsReached, TaskCompletionSource Source)> _statusWaiters = [];

    /// <summary>
    /// The lock of the statuses, the notices and the waiters.
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

    /// <summary>
    /// The buffers of the test, disposed after the senders.
    /// </summary>
    private readonly List<LiveBuffer> _buffers = [];

    /// <summary>
    /// The runs of the buffers, ended by their dispose.
    /// </summary>
    private readonly List<Task> _bufferRuns = [];

    /// <summary>
    /// The temporary folder of the test, deleted afterwards.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-profile-sender-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSenderProfileTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LiveSenderProfileTests(TestContext testContext)
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
    /// Stops the runs, disposes the senders, the buffers and the servers, and deletes the temporary folder.
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

        foreach (var buffer in _buffers)
        {
            await buffer.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        }

        await Task.WhenAny(Task.WhenAll(_bufferRuns)).WaitAsync(_guard, TestContext.CancellationToken);

        foreach (var server in _servers)
        {
            await server.DisposeAsync();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// The hello names the active profile and every message taken carries its identity (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileNewMessagesCarryActiveProfile()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var second = profiles.Create(SecondName);
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);
        StartRun(sender);

        // Act
        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        sender.Enqueue(TickItem(100, 2));
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 3));

        var tick = await ReceiveAndAcknowledgeAsync(server);
        var start = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        var hello = Assert.IsInstanceOfType<ContractHello>(message, "The first message should be the hello");

        Assert.AreEqual(second.Id, hello.ProfileId, "The hello should name the active profile's identity");
        Assert.AreEqual(SecondName, hello.ProfileName, "The hello should name the active profile's name");
        Assert.AreEqual(second.Id, Assert.IsInstanceOfType<ContractTick>(tick, "The tick should be sent").ProfileId, "The tick should carry the active profile");
        Assert.AreEqual(second.Id, Assert.IsInstanceOfType<ContractSessionStart>(start, "The session start should be sent").ProfileId, "The session start should carry the active profile");
    }

    /// <summary>
    /// Selecting another profile reconnects with a hello naming it, and the messages taken afterwards carry it (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileSwitchReconnectsWithHelloOfNewProfile()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);
        StartRun(sender);

        var firstHello = await AcceptAsync(first);

        sender.Enqueue(TickItem(100, 2));

        var firstTick = await ReceiveAndAcknowledgeAsync(first);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        // Act
        var second = profiles.Create(SecondName);
        var nextHello = await AcceptAsync(next);

        sender.Enqueue(TickItem(200, 3));

        var nextTick = await ReceiveAndAcknowledgeAsync(next);

        // Assert
        Assert.AreEqual(LiveSender.ProfileId, firstHello.ProfileId, "The first hello should name Live");
        Assert.AreEqual(LiveSender.ProfileId, Assert.IsInstanceOfType<ContractTick>(firstTick, "The first tick should be sent").ProfileId, "The first tick should carry Live");
        Assert.AreEqual(second.Id, nextHello.ProfileId, "The hello after the switch should name the new profile's identity");
        Assert.AreEqual(SecondName, nextHello.ProfileName, "The hello after the switch should name the new profile's name");
        Assert.AreEqual(second.Id, Assert.IsInstanceOfType<ContractTick>(nextTick, "The second tick should be sent").ProfileId, "The tick after the switch should carry the new profile");
        Assert.AreEqual(2, connector.AttemptCount, "The switch should make one new connection");
    }

    /// <summary>
    /// The switch ends the old connection with the close handshake before the new connection is made (S6, fact 19).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileSwitchClosesOldConnectionBeforeNewHello()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(first);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        var closing = first.ReceiveAsync(TestContext.CancellationToken);

        // Act
        profiles.Create(SecondName);

        await connector.WaitForAttemptsAsync(2, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var isClosedAtNewAttempt = first.IsClosedByClient;
        var afterClose = await closing.WaitAsync(_guard, TestContext.CancellationToken);
        var nextHello = await AcceptAsync(next);

        // Assert
        Assert.IsTrue(isClosedAtNewAttempt, "The old connection should be closed with a close frame before the new connection");
        Assert.IsNull(afterClose, "The old connection should send nothing more before its close");
        Assert.AreEqual(SecondName, nextHello.ProfileName, "The new connection's hello should name the new profile");
    }

    /// <summary>
    /// A message taken for a profile while offline, after which another profile was selected, is sent after a reconnect
    /// whose hello names its profile; the message after it reconnects to its own profile again (S6, Challenge 1 of the
    /// spec).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileBufferedMessageOfOtherProfileReconnectsWithItsName()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var servers = new[] { AddServer(connector), AddServer(connector), AddServer(connector) };
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);

        var offline = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(200, 3));

        // Act
        StartRun(sender);

        var firstHello = await AcceptAsync(servers[0]);
        var firstContent = await servers[0].ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var secondHello = await AcceptAsync(servers[1]);
        var offlineTick = await ReceiveAndAcknowledgeAsync(servers[1]);
        var secondContent = await servers[1].ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var thirdHello = await AcceptAsync(servers[2]);
        var liveTick = await ReceiveAndAcknowledgeAsync(servers[2]);

        // Assert
        Assert.AreSequenceEqual([LiveSender.ProfileName, SecondName, LiveSender.ProfileName], new[] { firstHello.ProfileName, secondHello.ProfileName, thirdHello.ProfileName }, "Each hello should name the profile of the messages its connection sends");
        Assert.AreSequenceEqual([LiveSender.ProfileId, offline.Id, LiveSender.ProfileId], new[] { firstHello.ProfileId, secondHello.ProfileId, thirdHello.ProfileId }, "Each hello should carry the identity of that profile");
        Assert.IsNull(firstContent, "The connection of the active profile should send nothing before the waiting message of the other");

        var waitingTick = Assert.IsInstanceOfType<ContractTick>(offlineTick, "The waiting tick should be sent");

        Assert.AreEqual(100L, waitingTick.GameTime, "The tick taken offline should be sent first");
        Assert.AreEqual(offline.Id, waitingTick.ProfileId, "The tick taken offline should keep its profile");
        Assert.IsNull(secondContent, "The connection of the offline profile should send no message of another profile");
        Assert.AreEqual(LiveSender.ProfileId, Assert.IsInstanceOfType<ContractTick>(liveTick, "The later tick should be sent").ProfileId, "The later tick should keep its profile");
        Assert.IsEmpty(Statuses().Where(status => status.State == LiveSendState.Retrying).ToList(), "No reconnect of a profile should count as a failure");
    }

    /// <summary>
    /// A waiting message of a profile the profiles do not know is sent on the current connection, without a reconnect (G3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileUnknownProfileSentOnCurrentConnection()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var unknown = new Guid("4e3d2c1b-0a99-4b88-8c77-000000000c04");
        var tick = ContractMessageConverter.ToTick(CreateTick(100, 2), unknown);

        buffer.Add(tick.Id, ContractWriter.Write(tick).Utf8Json);

        var sender = Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, connector, timeProvider, _logger));

        sender.UseProfiles(LoadProfiles());

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var sent = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(LiveSender.ProfileName, hello.ProfileName, "The hello should name the active profile");
        Assert.AreEqual(unknown, Assert.IsInstanceOfType<ContractTick>(sent, "The tick should be sent").ProfileId, "The tick should keep its unknown profile");
        Assert.AreEqual(1, connector.AttemptCount, "An unknown profile should not reconnect");
    }

    /// <summary>
    /// A switch reconnects at once, without a retry delay, a failure line or a retrying status: connecting, then connected
    /// (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileSwitchReconnectHasNoDelayAndLogsNoFailure()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider);

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(first);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        // Act
        profiles.Create(SecondName);

        await AcceptAsync(next);
        await WaitForStatusAsync(LiveSendState.Connected, 2);

        // Assert
        var afterFirst = Statuses().SkipWhile(status => status.State != LiveSendState.Connected).Skip(1).Select(status => status.State).ToArray();

        Assert.AreSequenceEqual([LiveSendState.Connecting, LiveSendState.Connected], afterFirst, "The switch should show connecting, then connected");
        Assert.IsEmpty(Lines().Where(line => line.StartsWith(FailedLinePrefix, StringComparison.Ordinal)).ToList(), "The switch should log no failure");
        Assert.DoesNotContain(LiveSender.RetryDelays[0], timeProvider.Delays, "The switch should wait no retry delay");
        Assert.AreEqual(_start, timeProvider.GetUtcNow(), "The switch should not wait for the clock");
    }

    /// <summary>
    /// A switch leaves the step of the retry delays as it was: after two failures and a switch, the next failure waits the
    /// third delay of 4 seconds, neither the first nor the fourth (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileSwitchLeavesDelayStepUnchanged()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();

        connector.Enqueue(null);
        connector.Enqueue(null);

        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider);

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(1), 1);
        await AdvanceRetryAsync(timeProvider, TimeSpan.FromSeconds(2), 1);
        await AcceptAsync(first);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        profiles.Create(SecondName);

        await AcceptAsync(next);
        await WaitForStatusAsync(LiveSendState.Connected, 2);

        // Act
        next.Abort();

        var isWaiting = await WaitForAsync(() => StepDelays(timeProvider).Length >= 2);

        // Assert
        Assert.IsTrue(isWaiting, "The failure after the switch should wait a retry delay");
        Assert.AreSequenceEqual([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)], StepDelays(timeProvider), "The failure after the switch should wait the third delay, neither the first nor the fourth");
    }

    /// <summary>
    /// A hello after the switch refused because the old session is still connected is tried again after
    /// <see cref="LiveSender.SwitchRetryDelay" />, as part of the switch: no failure line, no retrying status, no retry delay
    /// (S6, fact 20).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileSwitchRefusedCompanionConnectedRetriedWithoutFailure()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var firstRefusing = AddServer(connector);
        var secondRefusing = AddServer(connector);
        var accepting = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider);

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(first);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        // Act
        var second = profiles.Create(SecondName);

        await RefuseHelloAsync(firstRefusing, ContractHelloRefusal.CompanionConnected);
        await AdvanceRetryAsync(timeProvider, LiveSender.SwitchRetryDelay, 1);
        await RefuseHelloAsync(secondRefusing, ContractHelloRefusal.CompanionConnected);
        await AdvanceRetryAsync(timeProvider, LiveSender.SwitchRetryDelay, 2);

        var hello = await AcceptAsync(accepting);

        await WaitForStatusAsync(LiveSendState.Connected, 2);

        // Assert
        Assert.AreEqual(second.Id, hello.ProfileId, "The accepted hello should name the new profile");
        Assert.AreEqual(4, connector.AttemptCount, "Each refused hello should be tried again once");
        Assert.IsEmpty(Lines().Where(line => line.StartsWith(FailedLinePrefix, StringComparison.Ordinal)).ToList(), "A refusal within the switch should log no failure");
        Assert.IsEmpty(Statuses().Where(status => status.State == LiveSendState.Retrying).ToList(), "A refusal within the switch should show no retrying status");
        Assert.DoesNotContain(LiveSender.RetryDelays[0], timeProvider.Delays, "A refusal within the switch should wait no retry delay");
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), LiveSender.SwitchRetryDelay, "The switch should try again after 250 milliseconds");
    }

    /// <summary>
    /// A refusal because another Companion is connected that comes later than <see cref="LiveSender.SwitchRefusalTime" />
    /// after the switch is handled as a failure: retrying status, failure line and the first retry delay (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileSwitchRefusalPastRefusalTimeCountsAsFailure()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var refusing = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider);

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(first);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        profiles.Create(SecondName);

        await refusing.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        timeProvider.Advance(LiveSender.SwitchRefusalTime + TimeSpan.FromSeconds(1));

        await refusing.SendAsync(new ContractHelloRefused
                                 {
                                     Reason = ContractHelloRefusal.CompanionConnected,
                                     ReceivedVersion = 2,
                                     SupportedVersions = [2]
                                 },
                                 TestContext.CancellationToken);
        await WaitForStatusAsync(LiveSendState.Retrying, 1);
        await timeProvider.WaitForDelayAsync(LiveSender.RetryDelays[0], TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(TimeSpan.FromSeconds(5), LiveSender.SwitchRefusalTime, "The switch should last 5 seconds");
        Assert.AreEqual(ReplaySendProblemKind.CompanionConnected, sender.Status.Problem, "The status should name the other Companion");
        Assert.Contains("Live sending failed: another Companion is connected; retrying", Lines(), "The late refusal should log the failure");
    }

    /// <summary>
    /// In local mode a switch reconnects as well, with the token of the local server (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileLocalServerSwitchReconnects()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = Track(LiveSender.ForLocalServer(LocalPort, _ => Task.FromResult<string?>(Token), CreateRunningBuffer(timeProvider), CompanionVersion, connector, timeProvider, _logger));

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(first);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        // Act
        var second = profiles.Create(SecondName);
        var hello = await AcceptAsync(next);

        // Assert
        Assert.AreEqual(second.Id, hello.ProfileId, "The local hello after the switch should name the new profile");
        Assert.AreEqual(SecondName, hello.ProfileName, "The local hello after the switch should carry the new name");
        Assert.AreEqual(Token, hello.Token, "The local hello should carry the local server's token");
        Assert.AreSequenceEqual([new Uri("ws://127.0.0.1:5117/ingest"), new Uri("ws://127.0.0.1:5117/ingest")], connector.Addresses, "Both connections should go to the local server");
    }

    /// <summary>
    /// The session start of a figure last used with another profile raises the notice while it is taken, and the session
    /// start and the tick after it are taken without waiting (S7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void LiveSenderProfileSessionStartRaisesNoticeWithoutHoldingTicks()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var buffer = CreateRunningBuffer(timeProvider);
        var sender = Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, new QueuedIngestConnector(), timeProvider, _logger));
        var profiles = LoadProfiles();

        sender.UseProfiles(profiles);
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 1));
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));
        profiles.Create(SecondName);

        // Act
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 3));

        var noticesAtReturn = Notices();

        sender.Enqueue(TickItem(100, 4));

        // Assert
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, LiveSender.ProfileName)], noticesAtReturn.Select(notice => (notice.Kind, notice.ProfileName)).ToArray(), "The second session start should raise the notice naming Live before it returns");
        Assert.AreEqual(4, buffer.Status.Waiting, "Every message should be taken without waiting");
    }

    /// <summary>
    /// Neither the figure, its key nor a profile name is in any log line of the sender or the profiles (N2, N5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task LiveSenderProfileFigureNameNeverLogged()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, CreateTimeProvider());
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Figure)));

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(first);

        // Act
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 1));
        await ReceiveAndAcknowledgeAsync(first);
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));
        await ReceiveAndAcknowledgeAsync(first);

        _ = first.ReceiveAsync(TestContext.CancellationToken);

        profiles.Create(SecondName);
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 3));
        await AcceptAsync(next);
        await ReceiveAndAcknowledgeAsync(next);
        await StopRunsAsync();

        // Assert
        var lines = Lines().Concat(_profilesLogger.Entries.Select(entry => entry.Message)).ToList();

        Assert.HasCount(1, Notices(), "The figure's session start on the new profile should raise a notice");
        Assert.IsEmpty(lines.Where(line => line.Contains("MARKER", StringComparison.Ordinal) || line.Contains("Cæsar", StringComparison.Ordinal) || line.Contains(key, StringComparison.OrdinalIgnoreCase)).ToList(), "No log line should hold the figure, its key or a profile name");
    }

    /// <summary>
    /// Trade routes taken carry the active profile and are sent on its connection (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileTradeRoutesCarryActiveProfile()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var second = profiles.Create(SecondName);
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(server);

        // Act
        sender.Enqueue(TradeRoutesItem(2));

        var sent = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(second.Id, Assert.IsInstanceOfType<ContractTradeRoutes>(sent, "The trade routes should be sent").ProfileId, "The trade routes should carry the active profile");
        Assert.AreEqual(1, connector.AttemptCount, "Trade routes of the connection's profile should not reconnect");
    }

    /// <summary>
    /// A waiting entry the contract cannot read names no profile and is sent on the current connection, without a
    /// reconnect; the connection goes on with the next message (G3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileUnreadableEntrySentOnCurrentConnection()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var unreadable = new Guid("5f4e3d2c-1b0a-4c99-8d88-000000000d05");

        buffer.Add(unreadable, Encoding.UTF8.GetBytes("""{"type":"no-such-message"}"""));

        var sender = Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, connector, timeProvider, _logger));

        sender.UseProfiles(LoadProfiles());

        // Act
        StartRun(sender);
        await AcceptAsync(server);

        var received = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await server.SendAsync(ScriptedIngestServer.CreateAcknowledgement(unreadable, null), TestContext.CancellationToken);
        sender.Enqueue(TickItem(100, 2));

        var tick = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.IsNull(received, "The unreadable entry should reach the server as a message the contract cannot read");
        Assert.AreEqual(LiveSender.ProfileId, Assert.IsInstanceOfType<ContractTick>(tick, "The next tick should be sent on the same connection").ProfileId, "The next tick should carry the active profile");
        Assert.AreEqual(1, connector.AttemptCount, "An entry without a profile should not reconnect");
    }

    /// <summary>
    /// Changes of the active profile that end on the profile the connection names keep the connection: the messages after
    /// them are sent on it, without a reconnect (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileChangeBackToConnectionProfileKeepsConnection()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.ActiveChanged += (_, _) => SelectLiveAgain(profiles);
        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(server);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        // Act
        profiles.Create(SecondName);
        sender.Enqueue(TickItem(100, 2));

        var first = await ReceiveAndAcknowledgeAsync(server);

        sender.Enqueue(TickItem(200, 3));

        var second = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(LiveSender.ProfileId, profiles.Active.Id, "Live should be active again");
        Assert.AreEqual(LiveSender.ProfileId, Assert.IsInstanceOfType<ContractTick>(first, "The first tick should be sent").ProfileId, "The first tick should carry Live");
        Assert.AreEqual(LiveSender.ProfileId, Assert.IsInstanceOfType<ContractTick>(second, "The second tick should be sent").ProfileId, "The second tick should carry Live");
        Assert.AreEqual(1, connector.AttemptCount, "Changes back to the connection's profile should not reconnect");
    }

    /// <summary>
    /// A notice whose subscriber throws, or that has no subscriber, does not stop the messages from being taken (S7).
    /// </summary>
    /// <param name="hasThrowingSubscriber">Whether a subscriber that throws is attached; otherwise none is</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(true, DisplayName = "Subscriber throws")]
    [DataRow(false, DisplayName = "No subscriber")]
    public void LiveSenderProfileNoticeWithoutWorkingSubscriberKeepsTaking(bool hasThrowingSubscriber)
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var buffer = CreateRunningBuffer(timeProvider);
        var sender = Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, new QueuedIngestConnector(), timeProvider, _logger));
        var profiles = LoadProfiles();
        var calls = 0;

        sender.ProfileNoticeRaised -= OnProfileNoticeRaised;

        if (hasThrowingSubscriber)
        {
            sender.ProfileNoticeRaised += (_, _) =>
                                          {
                                              calls++;

                                              throw new InvalidOperationException("The subscriber failed.");
                                          };
        }

        sender.UseProfiles(profiles);
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 1));
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));
        profiles.Create(SecondName);

        // Act
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 3));
        sender.Enqueue(TickItem(100, 4));

        // Assert
        Assert.AreEqual(hasThrowingSubscriber ? 1 : 0, calls, "The notice should reach the subscriber once");
        Assert.AreEqual(4, buffer.Status.Waiting, "Every message should be taken");
    }

    /// <summary>
    /// A run cancelled while the close handshake of a switch waits for the server ends without a new connection and
    /// without a failure line (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderProfileCancelDuringSwitchCloseEndsRun()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider);
        var closeWait = TimeSpan.FromSeconds(2);

        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(server);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        var closeWaitsBefore = timeProvider.Delays.Count(delay => delay == closeWait);

        profiles.Create(SecondName);

        var isClosing = await WaitForAsync(() => timeProvider.Delays.Count(delay => delay == closeWait) > closeWaitsBefore);

        // Act
        await StopRunsAsync();

        // Assert
        Assert.AreEqual(0, closeWaitsBefore, "No close should wait before the switch");
        Assert.IsTrue(isClosing, "The switch should wait for the close handshake");
        Assert.AreEqual(1, connector.AttemptCount, "The cancelled run should make no new connection");
        Assert.IsEmpty(Lines().Where(line => line.StartsWith(FailedLinePrefix, StringComparison.Ordinal)).ToList(), "The cancelled switch should log no failure");
    }

    /// <summary>
    /// Selects <c>Live</c> again whenever another profile is active.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    private static void SelectLiveAgain(CompanionProfiles profiles)
    {
        if (profiles.Active.Id != LiveSender.ProfileId)
        {
            profiles.Select(LiveSender.ProfileId);
        }
    }

    /// <summary>
    /// Creates the item of trade routes with one route.
    /// </summary>
    /// <param name="readSeconds">The read stamp in seconds after the start</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem TradeRoutesItem(int readSeconds)
    {
        var message = new TradeRoutesMessage
                      {
                          Routes = [
                                       new TradeRoute
                                       {
                                           Id = 1,
                                           Name = "Via Appia",
                                           Folder = "North",
                                           MajorProblems = 0,
                                           MinorProblems = 0,
                                           Ships = 2,
                                           LostShips = 0,
                                           Stations = []
                                       }
                                   ]
                      };

        return Item(PipeTickResult.FromTradeRoutes(message), readSeconds);
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
    /// Returns the timers of the first, third and fourth retry delay, the ones that tell the step after the switch apart;
    /// the second delay of 2 seconds is left out, as the close of a connection waits as long.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The due times in the order of their timers</returns>
    private static TimeSpan[] StepDelays(RecordingTimeProvider timeProvider)
    {
        TimeSpan[] steps = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

        return [.. timeProvider.Delays.Where(steps.Contains)];
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
    /// Creates a tick of one island.
    /// </summary>
    /// <param name="gameTime">The game time, which identifies the tick</param>
    /// <param name="receiveSeconds">The receive time in seconds after the start</param>
    /// <returns>The tick</returns>
    private static PipeTick CreateTick(long gameTime, int receiveSeconds)
    {
        return new PipeTick
               {
                   GameTime = gameTime,
                   ReceiveTime = _start.AddSeconds(receiveSeconds),
                   Islands = [
                                 new PipeTickIsland
                                 {
                                     Key = new PipeIslandKey(180023, 1, 0),
                                     SessionId = 1,
                                     Name = "Isle Of Hidden Names",
                                     Entries = []
                                 }
                             ],
                   CloseReason = PipeTickCloseReason.GameTimeChanged
               };
    }

    /// <summary>
    /// Creates the item of a tick of one island whose read stamp is one second after its receive time.
    /// </summary>
    /// <param name="gameTime">The game time, which identifies the tick</param>
    /// <param name="receiveSeconds">The receive time in seconds after the start</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem TickItem(long gameTime, int receiveSeconds)
    {
        return Item(PipeTickResult.FromTick(CreateTick(gameTime, receiveSeconds)), receiveSeconds + 1);
    }

    /// <summary>
    /// Loads the profiles of a file in the temporary folder that does not exist yet.
    /// </summary>
    /// <returns>The profiles: <c>Live</c> alone</returns>
    private CompanionProfiles LoadProfiles()
    {
        return CompanionProfiles.Load(Path.Combine(_root, Guid.NewGuid().ToString("N"), "profiles.json"), _profilesLogger);
    }

    /// <summary>
    /// Creates a sender for the server of the tests over a running buffer and records its statuses and notices.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The sender</returns>
    private LiveSender CreateSender(QueuedIngestConnector connector, TimeProvider timeProvider)
    {
        return Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), CreateRunningBuffer(timeProvider), CompanionVersion, connector, timeProvider, _logger));
    }

    /// <summary>
    /// Creates a buffer with the default limits in a new folder and starts its run; both end after the test.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The buffer</returns>
    private LiveBuffer CreateRunningBuffer(TimeProvider timeProvider)
    {
        var buffer = new LiveBuffer(Path.Combine(_root, Guid.NewGuid().ToString("N"), "Buffer"), LiveBufferLimits.Defaults, timeProvider, _bufferLogger);

        _buffers.Add(buffer);
        _bufferRuns.Add(buffer.RunAsync(TestContext.CancellationToken));

        return buffer;
    }

    /// <summary>
    /// Records the statuses and notices of a sender and disposes it after the test.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <returns>The sender</returns>
    private LiveSender Track(LiveSender sender)
    {
        sender.StatusChanged += OnStatusChanged;
        sender.ProfileNoticeRaised += OnProfileNoticeRaised;
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
    /// Records a notice.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="notice">The notice</param>
    private void OnProfileNoticeRaised(object? sender, ProfileNotice notice)
    {
        lock (_lock)
        {
            _notices.Add(notice);
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
    /// Returns the notices raised so far.
    /// </summary>
    /// <returns>The notices in order</returns>
    private List<ProfileNotice> Notices()
    {
        lock (_lock)
        {
            return [.. _notices];
        }
    }

    /// <summary>
    /// Returns the messages of the senders' log entries so far.
    /// </summary>
    /// <returns>The messages in order</returns>
    private List<string> Lines()
    {
        return [.. _logger.Entries.Select(entry => entry.Message)];
    }

    /// <summary>
    /// Waits until a state was raised a number of times.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="occurrence">The number of times</param>
    /// <returns>A task that completes when the state was raised as often</returns>
    private Task WaitForStatusAsync(LiveSendState state, int occurrence)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bool IsReached(List<LiveSendStatus> statuses)
        {
            return statuses.Count(status => status.State == state) >= occurrence;
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
    /// Polls a condition, ten milliseconds apart, until it holds or the polls run out.
    /// </summary>
    /// <param name="condition">The condition</param>
    /// <returns>Whether the condition holds</returns>
    private async Task<bool> WaitForAsync(Func<bool> condition)
    {
        var polls = 0;

        while (condition() == false && polls < MaxPolls)
        {
            await Task.Delay(10, TestContext.CancellationToken);

            polls++;
        }

        return condition();
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
    /// <returns>The hello</returns>
    private async Task<ContractHello> AcceptAsync(ScriptedIngestServer server)
    {
        var hello = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        return Assert.IsInstanceOfType<ContractHello>(hello, "The first message should be the hello");
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
    /// Receives the next message that is not a status and acknowledges it.
    /// </summary>
    /// <param name="server">The server</param>
    /// <returns>The message, or <c>null</c> when the connection ended</returns>
    private async Task<ContractMessage?> ReceiveAndAcknowledgeAsync(ScriptedIngestServer server)
    {
        var message = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        while (message is ContractStatus)
        {
            message = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        if (message is not null)
        {
            await server.AcknowledgeAsync(message, null, TestContext.CancellationToken);
        }

        return message;
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

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Dispose();
    }

    #endregion // IDisposable
}