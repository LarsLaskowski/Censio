using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Censio.Contracts;
using Censio.GameData;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for the profiles of <see cref="LiveSender" />: the profile of each message, the hello of each connection, the
/// reconnect of a switch, the notices of a session start, and the deletions of profiles with their history.
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
    /// A raised notice is logged once with its kind, at Information and without a figure or profile name, before the
    /// subscriber of the notice is called (S12, N5).
    /// </summary>
    /// <param name="kind">The kind of the notice</param>
    /// <param name="expected">The expected log line</param>
    [TestMethod]
    [TestCategory("AC13")]
    [TestCategory("AC9")]
    [DataRow(ProfileNoticeKind.LastUsedWithOther, "Profile notice raised: LastUsedWithOther", DisplayName = "Last used with another profile")]
    [DataRow(ProfileNoticeKind.NewFigure, "Profile notice raised: NewFigure", DisplayName = "New figure")]
    public void LiveSenderProfileNoticeLoggedWithKindBeforeRaised(ProfileNoticeKind kind, string expected)
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var buffer = CreateRunningBuffer(timeProvider);
        var sender = Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, new QueuedIngestConnector(), timeProvider, _logger));
        var profiles = LoadProfiles();
        string[]? linesAtNotice = null;

        sender.UseProfiles(profiles);

        var figure = PrepareNotice(sender, profiles, kind);

        sender.ProfileNoticeRaised += (_, _) => linesAtNotice = [.. Lines()];

        // Act
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(figure), 3));

        // Assert
        var noticeLines = _logger.Entries.Where(entry => entry.Message.StartsWith("Profile notice", StringComparison.Ordinal)).Select(entry => (entry.Level, entry.Message)).ToArray();

        Assert.AreSequenceEqual([(kind, SecondName)], Notices().Select(notice => (notice.Kind, notice.ProfileName)).ToArray(), $"The session start should raise one notice naming the second profile: {kind}");
        Assert.IsNotNull(linesAtNotice, $"The subscriber should have been called: {kind}");
        Assert.AreSequenceEqual([expected], linesAtNotice.Where(line => line.StartsWith("Profile notice", StringComparison.Ordinal)).ToArray(), $"The line should be logged before the subscriber is called: {kind}");
        Assert.AreSequenceEqual([(LogLevel.Information, expected)], noticeLines, $"The notice should be logged once, at Information: {kind}");
        Assert.IsEmpty(Lines().Where(line => line.Contains("MARKER", StringComparison.Ordinal) || line.Contains("Cæsar", StringComparison.Ordinal)).ToList(), $"No log line should hold the figure or a profile name: {kind}");
    }

    /// <summary>
    /// Session starts that give no notice — a first figure, the same figure on the same profile again — raise nothing and
    /// log no notice line (S12, N5).
    /// </summary>
    [TestMethod]
    [TestCategory("AC13")]
    public void LiveSenderProfileNoNoticeLogsNothing()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var buffer = CreateRunningBuffer(timeProvider);
        var sender = Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, new QueuedIngestConnector(), timeProvider, _logger));
        var profiles = LoadProfiles();

        sender.UseProfiles(profiles);
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 1));
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));

        // Act
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 3));

        // Assert
        Assert.IsEmpty(Notices(), "No session start should raise a notice");
        Assert.IsEmpty(Lines().Where(line => line.StartsWith("Profile notice", StringComparison.Ordinal)).ToList(), "No notice line should be logged");
        Assert.IsNull(profiles.PendingNotice, "No notice should be pending");
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
    /// A deletion ticked before the run is sent as the first message after the accepted hello, naming the deleted profile
    /// under a message identity of its own (S3, AC5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LiveSenderProfileDeletionSentAfterHello()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var deleted = profiles.Create(SecondName);
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.Select(LiveSender.ProfileId);
        profiles.Delete(deleted.Id, true);
        sender.UseProfiles(profiles);
        sender.Enqueue(TickItem(300, 4));

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var first = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        var deletion = Assert.IsInstanceOfType<ContractProfileDeletion>(first, "The first message after the accepted hello should be the waiting deletion");

        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should name the active profile");
        Assert.AreEqual(deleted.Id, deletion.ProfileId, "The deletion should name the deleted profile");
        Assert.AreNotEqual(Guid.Empty, deletion.Id, "The deletion should carry a message identity");
        Assert.AreNotEqual(deleted.Id, deletion.Id, "The message identity of the deletion should not be the profile's identity");
    }

    /// <summary>
    /// On a connection whose hello names another profile than <c>Live</c>, the waiting deletions go first, oldest first and
    /// each after the acknowledgement of the one before, then the catalog, then the buffered message (S3, AC14).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC14")]
    public async Task LiveSenderProfileDeletionSentBeforeCatalogAndBuffer()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var older = profiles.Create("MARKER-Deleted Older");
        var newer = profiles.Create("MARKER-Deleted Newer");
        var active = profiles.Create(SecondName);
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.Delete(older.Id, true);
        profiles.Delete(newer.Id, true);
        sender.UseProfiles(profiles);
        Offer(sender, CatalogTestData.Create("A", 1));
        sender.Enqueue(TickItem(300, 4));

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var first = await ReceiveAndAcknowledgeAsync(server);
        var second = await ReceiveAndAcknowledgeAsync(server);
        var third = await ReceiveAndAcknowledgeAsync(server);
        var fourth = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(active.Id, hello.ProfileId, "The hello should name the active profile, not Live");
        Assert.AreEqual(older.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(first, "The first message after the hello should be a deletion").ProfileId, "The older deletion should go first");
        Assert.AreEqual(newer.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(second, "The second message should be a deletion").ProfileId, "The newer deletion should go second");
        Assert.IsInstanceOfType<ContractCatalogUpload>(third, "The catalog should go after the deletions");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(fourth, "The buffered tick should go after the catalog").GameTime, "The buffered tick should go last");
    }

    /// <summary>
    /// A deletion ticked while the connection waits for messages wakes it: the deletion is sent at once, and its accepted
    /// acknowledgement confirms it on the list, logged once at Information; the connection goes on (S3, AC7).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task LiveSenderProfileDeletionConfirmedOnAcknowledgement()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var deleted = profiles.Create(SecondName);
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.Select(LiveSender.ProfileId);
        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(server);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        // Act
        profiles.Delete(deleted.Id, true);

        var sent = await ReceiveAndAcknowledgeAsync(server);

        sender.Enqueue(TickItem(300, 4));

        var tick = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(sent, "The deletion ticked while connected should be sent at once").ProfileId, "The deletion should name the deleted profile");
        Assert.IsEmpty(profiles.WaitingDeletions, "The acknowledged deletion should not wait any more");
        Assert.IsTrue(profiles.IsDeleted(deleted.Id), "The confirmed identity should stay on the list");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(tick, "The tick after the deletion should be sent").GameTime, "The connection should go on with the tick");
        Assert.AreEqual(1, connector.AttemptCount, "The deletion should not reconnect");
        Assert.AreSequenceEqual([(LogLevel.Information, "Profile deletion confirmed by the server")], DeletionLines(), "The confirmation should be logged once, at Information");
    }

    /// <summary>
    /// A deletion ticked while a message is in flight goes after it; a refusal of the deletion confirms it on the list too,
    /// with the refusal logged once as a warning (S3, E6, AC7).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task LiveSenderProfileDeletionRefusedLogsAndConfirms()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var deleted = profiles.Create(SecondName);
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.Select(LiveSender.ProfileId);
        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(server);
        sender.Enqueue(TickItem(300, 4));

        var inFlight = await ReceiveContentAsync(server);

        // Act
        profiles.Delete(deleted.Id, true);
        await server.AcknowledgeAsync(inFlight, null, TestContext.CancellationToken);

        var sent = await ReceiveContentAsync(server);

        await server.AcknowledgeAsync(sent, ContractAcknowledgementRefusal.Invalid, TestContext.CancellationToken);
        sender.Enqueue(TickItem(400, 5));

        var next = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(inFlight, "The tick should be in flight at the deletion").GameTime, "The tick in flight should be the one enqueued");
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(sent, "The deletion should go after the message in flight").ProfileId, "The deletion should name the deleted profile");
        Assert.IsEmpty(profiles.WaitingDeletions, "The refused deletion should not wait any more");
        Assert.IsTrue(profiles.IsDeleted(deleted.Id), "The refused deletion should stay on the list, so the filter stays");
        Assert.AreSequenceEqual([(LogLevel.Warning, "Profile deletion refused by the server: Invalid")], DeletionLines(), "The refusal should be logged once as a warning, with no confirmation line");
        Assert.AreEqual(400L, Assert.IsInstanceOfType<ContractTick>(next, "The tick after the refusal should be sent").GameTime, "The connection should go on after the refusal");
    }

    /// <summary>
    /// A connection that ends before the deletion's acknowledgement keeps the deletion waiting; the next connection sends it
    /// again first, and its acknowledgement confirms it (E4, E5, AC7).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task LiveSenderProfileDeletionKeptWaitingWhenConnectionLost()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var deleted = profiles.Create(SecondName);
        var sender = CreateSender(connector, timeProvider);

        profiles.Select(LiveSender.ProfileId);
        profiles.Delete(deleted.Id, true);
        sender.UseProfiles(profiles);
        sender.Enqueue(TickItem(300, 4));
        StartRun(sender);
        await AcceptAsync(first);

        var lost = await ReceiveContentAsync(first);

        // Act
        first.Abort();

        await timeProvider.WaitForDelayAsync(LiveSender.RetryDelays[0], TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var waitingAfterLoss = profiles.WaitingDeletions.ToArray();

        timeProvider.Advance(LiveSender.RetryDelays[0]);
        await AcceptAsync(next);

        var resent = await ReceiveAndAcknowledgeAsync(next);
        var tick = await ReceiveAndAcknowledgeAsync(next);

        // Assert
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(lost, "The first connection should send the deletion").ProfileId, "The first deletion should name the deleted profile");
        Assert.AreSequenceEqual([deleted.Id], waitingAfterLoss, "The deletion without an acknowledgement should keep waiting");
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(resent, "The next connection should send the deletion again first").ProfileId, "The resent deletion should name the deleted profile");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(tick, "The buffered tick should follow the deletion").GameTime, "The buffered tick should be sent after the deletion");
        Assert.IsEmpty(profiles.WaitingDeletions, "The acknowledged deletion should not wait any more");
    }

    /// <summary>
    /// The buffered messages of a profile deleted with its history are never sent: after the deletion the connection
    /// goes on with the next message of another profile, without a reconnect, and each one counts as dropped (S2, E10,
    /// AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderProfileDeletionDropsWaitingMessages()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        sender.Enqueue(TickItem(200, 3));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(300, 4));
        profiles.Delete(deleted.Id, true);

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var deletion = await ReceiveAndAcknowledgeAsync(server);
        var next = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        var tick = Assert.IsInstanceOfType<ContractTick>(next, "The message after the deletion should be a tick");

        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should name the active profile");
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(deletion, "The deletion should be sent first").ProfileId, "The deletion should name the deleted profile");
        Assert.AreEqual(300L, tick.GameTime, "The ticks of the deleted profile should not be sent");
        Assert.AreEqual(LiveSender.ProfileId, tick.ProfileId, "The tick sent should be Live's");
        Assert.AreEqual(2, buffer.Status.Dropped, "Both ticks of the deleted profile should count as dropped");
        Assert.AreEqual(2, LoggedDropCount(), "The dropped ticks should be logged as dropped messages");
        Assert.AreEqual(1, connector.AttemptCount, "The dropped messages should not reconnect");
    }

    /// <summary>
    /// A confirmed identity still filters the buffer: its buffered message is dropped and no deletion is sent for it again
    /// (Deleted identities, AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderProfileDeletionDropsAfterConfirmation()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(300, 4));
        profiles.Delete(deleted.Id, true);
        profiles.ConfirmDeletion(deleted.Id);

        // Act
        StartRun(sender);
        await AcceptAsync(server);

        var next = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(next, "The first message should be the tick of Live, with no deletion sent again").GameTime, "The tick of the deleted profile should not be sent");
        Assert.AreEqual(1, buffer.Status.Dropped, "The tick of the confirmed profile should count as dropped");
    }

    /// <summary>
    /// The message in flight at a lost connection, of a profile deleted with its history before the reconnect, is dropped
    /// instead of being sent again (Deleted identities, AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderProfileDeletionDropsResentInFlightMessage()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        StartRun(sender);

        var firstHello = await AcceptAsync(first);

        sender.Enqueue(TickItem(100, 2));

        var inFlight = await ReceiveContentAsync(first);

        first.Abort();
        await timeProvider.WaitForDelayAsync(LiveSender.RetryDelays[0], TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(300, 4));

        // Act
        profiles.Delete(deleted.Id, true);
        timeProvider.Advance(LiveSender.RetryDelays[0]);

        var nextHello = await AcceptAsync(next);
        var deletion = await ReceiveAndAcknowledgeAsync(next);
        var after = await ReceiveAndAcknowledgeAsync(next);

        // Assert
        Assert.AreEqual(deleted.Id, firstHello.ProfileId, "The first hello should name the profile deleted later");
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractTick>(inFlight, "The tick should be in flight at the lost connection").ProfileId, "The tick in flight should be of the profile deleted later");
        Assert.AreEqual(LiveSender.ProfileId, nextHello.ProfileId, "The next hello should name the active profile");
        Assert.IsInstanceOfType<ContractProfileDeletion>(deletion, "The next connection should send the deletion first");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(after, "The message after the deletion should be a tick").GameTime, "The tick in flight of the deleted profile should not be sent again");
        Assert.AreEqual(1, buffer.Status.Dropped, "The tick in flight should count as dropped");
    }

    /// <summary>
    /// A dropped message counts toward the messages that waited at the hello: a catalog deferred by a connection that failed
    /// during its upload goes once the waiting messages are sent or dropped, before a message taken after the hello
    /// (Challenge note 2 of the plan, AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderProfileDeletionDropReleasesDeferredCatalog()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(200, 3));
        profiles.Delete(deleted.Id, true);
        profiles.ConfirmDeletion(deleted.Id);
        Offer(sender, CatalogTestData.Create("A", 1));
        StartRun(sender);
        await AcceptAsync(first);

        var failedUpload = await ReceiveContentAsync(first);

        first.Abort();
        await AdvanceRetryAsync(timeProvider, LiveSender.RetryDelays[0], 1);
        await AcceptAsync(next);

        // Act
        var waiting = await ReceiveContentAsync(next);

        sender.Enqueue(TickItem(300, 4));
        await next.AcknowledgeAsync(waiting, null, TestContext.CancellationToken);

        var afterWaiting = await ReceiveContentAsync(next);

        // Assert
        Assert.IsInstanceOfType<ContractCatalogUpload>(failedUpload, "The first connection should fail during the catalog");
        Assert.AreEqual(200L, Assert.IsInstanceOfType<ContractTick>(waiting, "The next connection should send the waiting tick of Live first").GameTime, "The waiting tick of the deleted profile should be dropped");
        Assert.IsInstanceOfType<ContractCatalogUpload>(afterWaiting, "The deferred catalog should go once the messages that waited at the hello were sent or dropped");
        Assert.AreEqual(1, buffer.Status.Dropped, "The tick of the deleted profile should count as dropped");
    }

    /// <summary>
    /// A profile deleted with its history after a waiting message of it began a switch to it is not named by the next hello,
    /// which names the active profile (AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderProfileDeletionHelloNeverNamesDeletedProfile()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider);
        var closeWait = TimeSpan.FromSeconds(2);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(300, 4));
        StartRun(sender);

        var firstHello = await AcceptAsync(first);
        var isClosing = await WaitForAsync(() => timeProvider.Delays.Contains(closeWait));

        // Act
        profiles.Delete(deleted.Id, true);

        var closed = await first.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var nextHello = await AcceptAsync(next);
        var deletion = await ReceiveAndAcknowledgeAsync(next);
        var tick = await ReceiveAndAcknowledgeAsync(next);

        // Assert
        Assert.AreEqual(LiveSender.ProfileId, firstHello.ProfileId, "The first hello should name the active profile");
        Assert.IsTrue(isClosing, "The waiting tick of the other profile should begin a switch to it");
        Assert.IsNull(closed, "The first connection should send nothing before its close");
        Assert.AreEqual(LiveSender.ProfileId, nextHello.ProfileId, "The next hello should name the active profile, never the deleted one");
        Assert.AreEqual(LiveSender.ProfileName, nextHello.ProfileName, "The next hello should carry the active profile's name");
        Assert.AreEqual(deleted.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(deletion, "The next connection should send the deletion first").ProfileId, "The deletion should name the deleted profile");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(tick, "The message after the deletion should be a tick").GameTime, "The tick of the deleted profile should not be sent");
    }

    /// <summary>
    /// A profile deleted with the history box cleared after a waiting message of it began a switch to it is not named by
    /// the next hello, which names the active profile; its waiting message is still sent on that connection (S1, E9, AC3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderProfileDeletionWithoutHistoryHelloNamesActiveProfile()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);
        var closeWait = TimeSpan.FromSeconds(2);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(300, 4));
        StartRun(sender);

        var firstHello = await AcceptAsync(first);
        var isClosing = await WaitForAsync(() => timeProvider.Delays.Contains(closeWait));

        // Act
        profiles.Delete(deleted.Id, false);

        var closed = await first.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var nextHello = await AcceptAsync(next);
        var waiting = await ReceiveAndAcknowledgeAsync(next);
        var tick = await ReceiveAndAcknowledgeAsync(next);

        // Assert
        var sent = Assert.IsInstanceOfType<ContractTick>(waiting, "The first message of the next connection should be the waiting tick, with no deletion sent");

        Assert.AreEqual(LiveSender.ProfileId, firstHello.ProfileId, "The first hello should name the active profile");
        Assert.IsTrue(isClosing, "The waiting tick of the other profile should begin a switch to it");
        Assert.IsNull(closed, "The first connection should send nothing before its close");
        Assert.AreEqual(LiveSender.ProfileId, nextHello.ProfileId, "The next hello should name the active profile, never the deleted one");
        Assert.AreEqual(LiveSender.ProfileName, nextHello.ProfileName, "The next hello should carry the active profile's name, not the deleted one's");
        Assert.AreEqual(100L, sent.GameTime, "The waiting tick of the deleted profile should be sent");
        Assert.AreEqual(deleted.Id, sent.ProfileId, "The waiting tick should keep the deleted profile's identity");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(tick, "The second message should be the tick of Live").GameTime, "The tick of Live should follow");
        Assert.AreEqual(2, connector.AttemptCount, "The waiting tick should go out on the connection of the next hello");
        Assert.AreEqual(0, buffer.Status.Dropped, "The waiting tick of the deleted profile should not be dropped");
    }

    /// <summary>
    /// A profile deleted with the history box cleared sends nothing for the deletion: its waiting messages go out on the
    /// current connection, before the messages of <c>Live</c>, and none is dropped (S1, E9, AC3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderProfileDeletionWithoutHistorySendsWaitingMessages()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);

        sender.UseProfiles(profiles);

        var deleted = profiles.Create(SecondName);

        sender.Enqueue(TickItem(100, 2));
        profiles.Select(LiveSender.ProfileId);
        sender.Enqueue(TickItem(300, 4));
        profiles.Delete(deleted.Id, false);

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var first = await ReceiveAndAcknowledgeAsync(server);
        var second = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        var waiting = Assert.IsInstanceOfType<ContractTick>(first, "The first message after the hello should be the waiting tick, with no deletion sent");

        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should name the active profile");
        Assert.AreEqual(100L, waiting.GameTime, "The waiting tick of the deleted profile should be sent first");
        Assert.AreEqual(deleted.Id, waiting.ProfileId, "The waiting tick should keep the deleted profile's identity");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(second, "The second message should be the tick of Live").GameTime, "The tick of Live should follow");
        Assert.IsFalse(profiles.IsDeleted(deleted.Id), "A deletion without history should not put the identity on the list");
        Assert.IsEmpty(profiles.WaitingDeletions, "A deletion without history should not wait to be sent");
        Assert.AreEqual(0, buffer.Status.Dropped, "No waiting message should be dropped");
        Assert.AreEqual(1, connector.AttemptCount, "The waiting messages should go out on the current connection");
    }

    /// <summary>
    /// A waiting deletion the contract writer refuses, the all-zero identity the profiles file accepts, is logged once and
    /// stays waiting; the next waiting deletion is still sent and confirmed, and the connection goes on.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task LiveSenderProfileDeletionNotWrittenKeepsWaitingAndSendsNext()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var valid = new Guid("5f0c2a9e-3d71-4b8e-a6c4-91e2d7b03f58");
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N"), "profiles.json");
        var live = LiveSender.ProfileId.ToString("D");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, $$"""{"version":2,"active":"{{live}}","profiles":[{"id":"{{live}}","name":"Live"}],"figures":[],"deleted":[{"id":"00000000-0000-0000-0000-000000000000","confirmed":false},{"id":"{{valid:D}}","confirmed":false}]}""", TestContext.CancellationToken);

        var profiles = CompanionProfiles.Load(path, _profilesLogger);
        var waitingAtLoad = profiles.WaitingDeletions.ToArray();
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);

        // Act
        StartRun(sender);
        await AcceptAsync(server);

        var sent = await ReceiveAndAcknowledgeAsync(server);

        sender.Enqueue(TickItem(300, 4));

        var tick = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreSequenceEqual([Guid.Empty, valid], waitingAtLoad, "The profiles file should load both waiting deletions, the all-zero one first");
        Assert.AreEqual(valid, Assert.IsInstanceOfType<ContractProfileDeletion>(sent, "The first message after the hello should be the valid deletion").ProfileId, "Only the valid deletion should be sent");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(tick, "The message after the deletion should be the tick").GameTime, "The connection should go on after the refused write");
        Assert.HasCount(1, Lines().Where(line => line == "Live message not sent: the contract writer refused it").ToList(), "The refused write should be logged once");
        Assert.AreSequenceEqual([Guid.Empty], profiles.WaitingDeletions.ToArray(), "The deletion the writer refused should keep waiting, the valid one should not");
        Assert.IsTrue(profiles.IsDeleted(valid), "The confirmed identity should stay on the list");
        Assert.AreSequenceEqual([(LogLevel.Information, "Profile deletion confirmed by the server")], DeletionLines(), "Only the valid deletion should be confirmed");
        Assert.AreEqual(1, connector.AttemptCount, "The refused write should not end the connection");
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
    /// Offers a ready catalog to a sender.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="data">The catalog data</param>
    private static void Offer(LiveSender sender, CatalogData data)
    {
        var status = new GameCatalogStatus
                     {
                         Kind = GameCatalogStatusKind.Ready,
                         EntryCount = 3
                     };

        sender.SetCatalog(status, data.Catalog, data.Icons, data.Build);
    }

    /// <summary>
    /// Takes the session of a figure on the created second profile, whose start and end give no notice, and gives the
    /// figure whose next session start gives the notice of a kind naming the second profile: the same figure after
    /// <c>Live</c> was selected, or a new figure on the second profile.
    /// </summary>
    /// <param name="sender">The sender, which uses the profiles</param>
    /// <param name="profiles">The profiles: <c>Live</c> alone</param>
    /// <param name="kind">The kind of the notice</param>
    /// <returns>The figure of the session start that gives the notice</returns>
    private static string PrepareNotice(LiveSender sender, CompanionProfiles profiles, ProfileNoticeKind kind)
    {
        profiles.Create(SecondName);
        sender.Enqueue(Item(PipeTickResult.FromSessionStart(Figure), 1));
        sender.Enqueue(Item(PipeTickResult.SessionEnd, 2));

        if (kind == ProfileNoticeKind.LastUsedWithOther)
        {
            profiles.Select(LiveSender.ProfileId);

            return Figure;
        }

        return "MARKER-Figure Other";
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
        return CreateSender(connector, timeProvider, CreateRunningBuffer(timeProvider));
    }

    /// <summary>
    /// Creates a sender for the server of the tests over a buffer and records its statuses and notices.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="buffer">The buffer, running</param>
    /// <returns>The sender</returns>
    private LiveSender CreateSender(QueuedIngestConnector connector, TimeProvider timeProvider, LiveBuffer buffer)
    {
        return Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, connector, timeProvider, _logger));
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
    /// Returns the senders' log entries about profile deletions so far.
    /// </summary>
    /// <returns>The level and message of each entry, in order</returns>
    private (LogLevel Level, string Message)[] DeletionLines()
    {
        return [.. _logger.Entries.Where(entry => entry.Message.StartsWith("Profile deletion", StringComparison.Ordinal)).Select(entry => (entry.Level, entry.Message))];
    }

    /// <summary>
    /// Returns the sum of the counts the senders logged as dropped messages so far.
    /// </summary>
    /// <returns>The number of dropped messages logged</returns>
    private int LoggedDropCount()
    {
        const string prefix = "Live messages dropped: ";

        return Lines().Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).Sum(line => int.Parse(line[prefix.Length..], CultureInfo.InvariantCulture));
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
    /// Receives the next message that is not a status, without acknowledging it.
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
    /// Receives the next message that is not a status and acknowledges it.
    /// </summary>
    /// <param name="server">The server</param>
    /// <returns>The message, or <c>null</c> when the connection ended</returns>
    private async Task<ContractMessage?> ReceiveAndAcknowledgeAsync(ScriptedIngestServer server)
    {
        var message = await ReceiveContentAsync(server);

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
        await timeProvider.WaitForDelaysAsync(delay, occurrence, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

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