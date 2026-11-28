using Censio.Contracts;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for the merges of profiles in <see cref="LiveSender" />: how a waiting merge is sent and confirmed, and how the
/// buffered messages of a merged profile are sent under its target.
/// </summary>
[TestClass]
public sealed class LiveSenderMergeTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The token of the server.
    /// </summary>
    private const string Token = "LiveToken-0123456789abcdefghijKLMNOPQR";

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private const string CompanionVersion = "1.2.3";

    /// <summary>
    /// The name of the profile whose history is merged.
    /// </summary>
    private const string SourceName = "MARKER-Source";

    /// <summary>
    /// The name of the profile that receives the history.
    /// </summary>
    private const string TargetName = "MARKER-Target";

    /// <summary>
    /// The line of a confirmed merge.
    /// </summary>
    private const string ConfirmedLine = "Profile merge confirmed by the server";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start time of the tests.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address of the server.
    /// </summary>
    private static readonly Uri _address = new("ws://merge-host.example:8080/ingest");

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
    /// The statuses the senders raised.
    /// </summary>
    private readonly List<LiveSendStatus> _statuses = [];

    /// <summary>
    /// The waits for a status.
    /// </summary>
    private readonly List<(Func<List<LiveSendStatus>, bool> IsReached, TaskCompletionSource Source)> _statusWaiters = [];

    /// <summary>
    /// The lock of the recorded statuses.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The source that stops the runs.
    /// </summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>
    /// The runs of the test.
    /// </summary>
    private readonly List<Task> _runs = [];

    /// <summary>
    /// The senders of the test.
    /// </summary>
    private readonly List<LiveSender> _senders = [];

    /// <summary>
    /// The servers of the test.
    /// </summary>
    private readonly List<ScriptedIngestServer> _servers = [];

    /// <summary>
    /// The buffers of the test.
    /// </summary>
    private readonly List<LiveBuffer> _buffers = [];

    /// <summary>
    /// The runs of the buffers.
    /// </summary>
    private readonly List<Task> _bufferRuns = [];

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-merge-sender-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSenderMergeTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LiveSenderMergeTests(TestContext testContext)
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
    /// Stops the runs and disposes what the test created.
    /// </summary>
    /// <returns>A task that completes when the test is cleaned up</returns>
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
    /// A waiting merge is the first message after the accepted hello, before the catalog and the buffered messages: it
    /// names the merged profile and its target under a message identity of its own, and its acknowledgement confirms it,
    /// logged once at Information (spec AC3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderMergeSentAfterHelloBeforeCatalogAndBuffer()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var source = profiles.Create(SourceName);
        var target = profiles.Create(TargetName);
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.Select(LiveSender.ProfileId);
        profiles.Merge(source.Id, target.Id);
        sender.UseProfiles(profiles);
        sender.SetCatalog(ReadyStatus(), CatalogTestData.Create("A", 1).Catalog, CatalogTestData.Create("A", 1).Icons, CatalogTestData.Build);
        sender.Enqueue(TickItem(300, 4));

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var first = await ReceiveAndAcknowledgeAsync(server);
        var second = await ReceiveAndAcknowledgeAsync(server);
        var third = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        var merge = Assert.IsInstanceOfType<ContractProfileMerge>(first, "The first message after the accepted hello should be the waiting merge");

        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should name the active profile");
        Assert.AreEqual(source.Id, merge.ProfileId, "The merge should name the merged profile");
        Assert.AreEqual(target.Id, merge.TargetProfileId, "The merge should name its target");
        Assert.AreNotEqual(Guid.Empty, merge.Id, "The merge should carry a message identity");
        Assert.AreNotEqual(source.Id, merge.Id, "The message identity of the merge should not be the profile's identity");
        Assert.IsInstanceOfType<ContractCatalogUpload>(second, "The catalog should go after the merge");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(third, "The buffered tick should go after the catalog").GameTime, "The buffered tick should go last");
        Assert.IsEmpty(profiles.WaitingMerges, "The acknowledged merge should not wait any more");
        Assert.AreSequenceEqual([(LogLevel.Information, ConfirmedLine)], MergeLines(), "The confirmation should be logged once, at Information");
    }

    /// <summary>
    /// A refusal of a merge confirms it too, with the refusal logged once as a warning and no confirmation line, and the
    /// connection goes on (spec AC3, E6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderMergeRefusedConfirmsAndWarns()
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var profiles = LoadProfiles();
        var source = profiles.Create(SourceName);
        var target = profiles.Create(TargetName);
        var sender = CreateSender(connector, CreateTimeProvider());

        profiles.Select(LiveSender.ProfileId);
        sender.UseProfiles(profiles);
        StartRun(sender);
        await AcceptAsync(server);
        await WaitForStatusAsync(LiveSendState.Connected, 1);

        // Act
        profiles.Merge(source.Id, target.Id);

        var sent = await ReceiveContentAsync(server);

        await server.AcknowledgeAsync(sent, ContractAcknowledgementRefusal.Invalid, TestContext.CancellationToken);
        sender.Enqueue(TickItem(400, 5));

        var next = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(source.Id, Assert.IsInstanceOfType<ContractProfileMerge>(sent, "The merge ticked while connected should be sent at once").ProfileId, "The merge should name the merged profile");
        Assert.IsEmpty(profiles.WaitingMerges, "The refused merge should not wait any more");
        Assert.AreEqual(target.Id, profiles.ResolveProfile(source.Id), "The refused merge should stay, so the buffered messages still go to the target");
        Assert.AreSequenceEqual([(LogLevel.Warning, "Profile merge refused by the server: Invalid")], MergeLines(), "The refusal should be logged once as a warning, with no confirmation line");
        Assert.AreEqual(400L, Assert.IsInstanceOfType<ContractTick>(next, "The tick after the refusal should be sent").GameTime, "The connection should go on after the refusal");
    }

    /// <summary>
    /// A connection that ends before the merge's acknowledgement keeps the merge waiting; the next connection sends it
    /// again first, and its acknowledgement confirms it (spec AC3, E6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderMergeLostConnectionKeepsWaitingAndResends()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var first = AddServer(connector);
        var next = AddServer(connector);
        var profiles = LoadProfiles();
        var source = profiles.Create(SourceName);
        var target = profiles.Create(TargetName);
        var sender = CreateSender(connector, timeProvider);

        profiles.Select(LiveSender.ProfileId);
        profiles.Merge(source.Id, target.Id);
        sender.UseProfiles(profiles);
        sender.Enqueue(TickItem(300, 4));
        StartRun(sender);
        await AcceptAsync(first);

        var lost = await ReceiveContentAsync(first);

        // Act
        first.Abort();

        await timeProvider.WaitForDelayAsync(LiveSender.RetryDelays[0], TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var waitingAfterLoss = profiles.WaitingMerges.ToArray();

        timeProvider.Advance(LiveSender.RetryDelays[0]);
        await AcceptAsync(next);

        var resent = await ReceiveAndAcknowledgeAsync(next);
        var tick = await ReceiveAndAcknowledgeAsync(next);

        // Assert
        Assert.AreEqual(source.Id, Assert.IsInstanceOfType<ContractProfileMerge>(lost, "The first connection should send the merge").ProfileId, "The first merge should name the merged profile");
        Assert.AreSequenceEqual([source.Id], waitingAfterLoss, "The merge without an acknowledgement should keep waiting");
        Assert.AreEqual(target.Id, Assert.IsInstanceOfType<ContractProfileMerge>(resent, "The next connection should send the merge again first").TargetProfileId, "The resent merge should name its target");
        Assert.AreEqual(300L, Assert.IsInstanceOfType<ContractTick>(tick, "The buffered tick should follow the merge").GameTime, "The buffered tick should be sent after the merge");
        Assert.IsEmpty(profiles.WaitingMerges, "The acknowledged merge should not wait any more");
    }

    /// <summary>
    /// Merges and deletions are sent in the order they were made, also when the profiles were written and loaded again and
    /// a new sender sends them (spec AC3, N2).
    /// </summary>
    /// <param name="scenario">The order: mergeThenDelete (merge A into B, then delete B) or deleteThenMerge (delete C, then merge A into B)</param>
    /// <param name="isReloaded">Whether the profiles are written and loaded again before the sender starts</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("mergeThenDelete", false, DisplayName = "Merge A into B, then delete B")]
    [DataRow("deleteThenMerge", false, DisplayName = "Delete C, then merge A into B")]
    [DataRow("mergeThenDelete", true, DisplayName = "Merge A into B, then delete B, after a reload")]
    [DataRow("deleteThenMerge", true, DisplayName = "Delete C, then merge A into B, after a reload")]
    public async Task LiveSenderMergeAndDeletionSentInOrderMade(string scenario, bool isReloaded)
    {
        // Arrange
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N"), "profiles.json");
        var profiles = CompanionProfiles.Load(path, _profilesLogger);
        var sourceId = profiles.Create("MARKER-A").Id;
        var targetId = profiles.Create("MARKER-B").Id;
        var otherId = profiles.Create("MARKER-C").Id;

        profiles.Select(LiveSender.ProfileId);

        string[] expected;

        if (scenario == "mergeThenDelete")
        {
            profiles.Merge(sourceId, targetId);
            profiles.Delete(targetId, true);

            expected = [$"merge {sourceId}>{targetId}", $"delete {targetId}"];
        }
        else
        {
            profiles.Delete(otherId, true);
            profiles.Merge(sourceId, targetId);

            expected = [$"delete {otherId}", $"merge {sourceId}>{targetId}"];
        }

        if (isReloaded)
        {
            await WriteAndStopAsync(profiles);

            profiles = CompanionProfiles.Load(path, _profilesLogger);
        }

        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);

        // Act
        StartRun(sender);

        await AcceptAsync(server);

        var first = await ReceiveAndAcknowledgeAsync(server);
        var second = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreSequenceEqual(expected, new[] { Describe(first), Describe(second) }, $"The removals should be sent in the order they were made: {scenario}, reloaded {isReloaded}");
        await WaitUntilAsync(() => profiles.WaitingRemovals.Count == 0, "Both removals should be confirmed");
    }

    /// <summary>
    /// A buffered message of a merged profile is sent under the target with its message identity and every other value,
    /// after the merge, on a connection whose hello names the target (spec AC4).
    /// </summary>
    /// <param name="kind">The kind of the message: tick, tradeRoutes, sessionStart or sessionEnd</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("tick", DisplayName = "Tick")]
    [DataRow("tradeRoutes", DisplayName = "Trade routes")]
    [DataRow("sessionStart", DisplayName = "Session start")]
    [DataRow("sessionEnd", DisplayName = "Session end")]
    public async Task LiveSenderMergeBufferedMessageSentUnderTarget(string kind)
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);
        var source = profiles.Create(SourceName);
        var message = CreateMessage(kind, source.Id);

        buffer.Add(IdOf(message), ContractWriter.Write(message).Utf8Json);

        var target = profiles.Create(TargetName);

        profiles.Merge(source.Id, target.Id);
        sender.UseProfiles(profiles);

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var merge = await ReceiveAndAcknowledgeAsync(server);
        var sent = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(target.Id, hello.ProfileId, "The hello should name the active profile, the target");
        Assert.IsInstanceOfType<ContractProfileMerge>(merge, "The merge should go first");
        Assert.IsNotNull(sent, $"The buffered message should be sent: {kind}");
        Assert.AreEqual(message.GetType(), sent.GetType(), $"The kind of the message should stay: {kind}");
        Assert.AreEqual(IdOf(message), IdOf(sent), $"The message should keep its identity: {kind}");
        Assert.AreEqual(target.Id, ProfileOf(sent), $"The message should be sent under the target: {kind}");
        Assert.AreEqual(1, connector.AttemptCount, $"The message should need no reconnect: {kind}");
    }

    /// <summary>
    /// A buffered message of a profile merged into a profile that was merged into a third is sent under the last target
    /// (spec AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderMergeChainResolvesToLastTarget()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);
        var source = profiles.Create(SourceName);
        var tick = ContractMessageConverter.ToTick(CreateTick(100, 2), source.Id);

        buffer.Add(tick.Id, ContractWriter.Write(tick).Utf8Json);

        var middle = profiles.Create("MARKER-Middle");
        var last = profiles.Create(TargetName);

        profiles.Merge(source.Id, middle.Id);
        profiles.Merge(middle.Id, last.Id);
        sender.UseProfiles(profiles);

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var first = await ReceiveAndAcknowledgeAsync(server);
        var second = await ReceiveAndAcknowledgeAsync(server);
        var sent = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(last.Id, hello.ProfileId, "The hello should name the active profile, the last target");
        Assert.AreEqual(source.Id, Assert.IsInstanceOfType<ContractProfileMerge>(first, "The first merge should go first").ProfileId, "The merges should be sent in the order they were made");
        Assert.AreEqual(middle.Id, Assert.IsInstanceOfType<ContractProfileMerge>(second, "The second merge should go second").ProfileId, "The second merge should name the middle profile");

        var resent = Assert.IsInstanceOfType<ContractTick>(sent, "The buffered tick should be sent");

        Assert.AreEqual(tick.Id, resent.Id, "The tick should keep its identity");
        Assert.AreEqual(last.Id, resent.ProfileId, "The tick should be sent under the last target");
    }

    /// <summary>
    /// A buffered message of a merged profile whose target was deleted with its history afterwards is dropped, as the
    /// messages of a deleted profile are, and the connection goes on with the next message (spec AC4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderMergeTargetDeletedDropsBufferedMessage()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        var connector = new QueuedIngestConnector();
        var server = AddServer(connector);
        var buffer = CreateRunningBuffer(timeProvider);
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, timeProvider, buffer);
        var source = profiles.Create(SourceName);
        var tick = ContractMessageConverter.ToTick(CreateTick(100, 2), source.Id);

        buffer.Add(tick.Id, ContractWriter.Write(tick).Utf8Json);

        var target = profiles.Create(TargetName);

        profiles.Select(LiveSender.ProfileId);
        profiles.Merge(source.Id, target.Id);
        profiles.Delete(target.Id, true);
        sender.UseProfiles(profiles);
        sender.Enqueue(TickItem(300, 4));

        // Act
        StartRun(sender);

        var hello = await AcceptAsync(server);
        var merge = await ReceiveAndAcknowledgeAsync(server);
        var deletion = await ReceiveAndAcknowledgeAsync(server);
        var next = await ReceiveAndAcknowledgeAsync(server);

        // Assert
        Assert.AreEqual(LiveSender.ProfileId, hello.ProfileId, "The hello should name the active profile");
        Assert.IsInstanceOfType<ContractProfileMerge>(merge, "The merge should go first");
        Assert.AreEqual(target.Id, Assert.IsInstanceOfType<ContractProfileDeletion>(deletion, "The deletion should go second").ProfileId, "The deletion should name the deleted target");

        var sent = Assert.IsInstanceOfType<ContractTick>(next, "The message after the removals should be a tick");

        Assert.AreEqual(300L, sent.GameTime, "The tick of the merged profile should not be sent");
        Assert.AreEqual(LiveSender.ProfileId, sent.ProfileId, "The tick sent should be Live's");
        Assert.AreEqual(1, buffer.Status.Dropped, "The tick of the merged profile should count as dropped");
        Assert.AreEqual(1, connector.AttemptCount, "The dropped message should not reconnect");
    }

    /// <summary>
    /// A merge of the profile the connection was opened for keeps the connection until its next message: that message,
    /// of the target or of the active profile, ends it as a switch of profile does, and the new hello names that message's
    /// profile (spec AC4, E8).
    /// </summary>
    /// <param name="next">The profile of the next message: target or active</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("target", DisplayName = "Next message of the target")]
    [DataRow("active", DisplayName = "Next message of the active profile")]
    public async Task LiveSenderMergeOfConnectionProfileReconnectsAsSwitch(string next)
    {
        // Arrange
        var connector = new QueuedIngestConnector();
        var servers = new[] { AddServer(connector), AddServer(connector), AddServer(connector) };
        var profiles = LoadProfiles();
        var sender = CreateSender(connector, CreateTimeProvider());

        sender.UseProfiles(profiles);

        var source = profiles.Create(SourceName);

        sender.Enqueue(TickItem(100, 2));

        var target = profiles.Create(TargetName);

        profiles.Select(LiveSender.ProfileId);
        StartRun(sender);

        await AcceptAsync(servers[0]);
        await servers[0].ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var sourceHello = await AcceptAsync(servers[1]);
        var firstTick = await ReceiveAndAcknowledgeAsync(servers[1]);

        // Act
        profiles.Merge(source.Id, target.Id);

        var merge = await ReceiveAndAcknowledgeAsync(servers[1]);
        var nextProfile = next == "target" ? target.Id : LiveSender.ProfileId;

        profiles.Select(nextProfile);
        sender.Enqueue(TickItem(200, 3));

        var closed = await servers[1].ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var nextHello = await AcceptAsync(servers[2]);
        var nextTick = await ReceiveAndAcknowledgeAsync(servers[2]);

        // Assert
        Assert.AreEqual(source.Id, sourceHello.ProfileId, "The second connection should be opened for the profile of the buffered tick");
        Assert.AreEqual(source.Id, Assert.IsInstanceOfType<ContractTick>(firstTick, "The buffered tick should be sent").ProfileId, "The buffered tick should keep its profile");
        Assert.AreEqual(source.Id, Assert.IsInstanceOfType<ContractProfileMerge>(merge, "The merge should be sent on the connection of the merged profile").ProfileId, "The merge should name the merged profile");
        Assert.IsNull(closed, "The connection of the merged profile should send nothing more before its close");
        Assert.AreEqual(nextProfile, nextHello.ProfileId, $"The hello after the merge should name the profile of the next message: {next}");
        Assert.AreEqual(nextProfile, Assert.IsInstanceOfType<ContractTick>(nextTick, "The next tick should be sent").ProfileId, $"The next tick should carry its profile: {next}");
        Assert.AreEqual(3, connector.AttemptCount, $"The merge should make one new connection: {next}");
    }

    /// <summary>
    /// Creates a message of a kind for a profile.
    /// </summary>
    /// <param name="kind">The kind: tick, tradeRoutes, sessionStart or sessionEnd</param>
    /// <param name="profileId">The profile</param>
    /// <returns>The message</returns>
    private static ContractMessage CreateMessage(string kind, Guid profileId)
    {
        var time = _start.AddSeconds(2);

        return kind switch
               {
                   "tick" => ContractMessageConverter.ToTick(CreateTick(100, 2), profileId),
                   "tradeRoutes" => ContractMessageConverter.ToTradeRoutes(new TradeRoutesMessage
                                                                           {
                                                                               Routes = []
                                                                           },
                                                                           profileId,
                                                                           time),
                   "sessionStart" => ContractMessageConverter.ToSessionStart(profileId, time),
                   _ => ContractMessageConverter.ToSessionEnd(profileId, time)
               };
    }

    /// <summary>
    /// Gives the message identity of a message.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The identity</returns>
    private static Guid IdOf(ContractMessage message)
    {
        return ScriptedIngestServer.IdOf(message);
    }

    /// <summary>
    /// Gives the profile of a tick, trade routes, session start or session end.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The profile</returns>
    private static Guid ProfileOf(ContractMessage message)
    {
        return message switch
               {
                   ContractTick tick => tick.ProfileId,
                   ContractTradeRoutes routes => routes.ProfileId,
                   ContractSessionStart start => start.ProfileId,
                   ContractSessionEnd end => end.ProfileId,
                   _ => Guid.Empty
               };
    }

    /// <summary>
    /// Describes a removal the sender sent.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The description</returns>
    private static string Describe(ContractMessage? message)
    {
        return message switch
               {
                   ContractProfileMerge merge => $"merge {merge.ProfileId}>{merge.TargetProfileId}",
                   ContractProfileDeletion deletion => $"delete {deletion.ProfileId}",
                   _ => "other"
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
        return new PipeConnectionItem
               {
                   Result = PipeTickResult.FromTick(CreateTick(gameTime, receiveSeconds)),
                   ReadStamp = _start.AddSeconds(receiveSeconds + 1)
               };
    }

    /// <summary>
    /// Creates the status of a ready catalog.
    /// </summary>
    /// <returns>The status</returns>
    private static GameCatalogStatus ReadyStatus()
    {
        return new GameCatalogStatus
               {
                   Kind = GameCatalogStatusKind.Ready,
                   EntryCount = 3
               };
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
    /// Returns the senders' log entries about profile merges so far.
    /// </summary>
    /// <returns>The level and message of each entry, in order</returns>
    private (LogLevel Level, string Message)[] MergeLines()
    {
        return [.. _logger.Entries.Where(entry => entry.Message.StartsWith("Profile merge", StringComparison.Ordinal)).Select(entry => (entry.Level, entry.Message))];
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
    /// Lets the writer of the profiles write their state and end.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <returns>A task that completes when the writer ended</returns>
    private async Task WriteAndStopAsync(CompanionProfiles profiles)
    {
        using var stop = new CancellationTokenSource();

        var run = profiles.RunAsync(stop.Token);

        await stop.CancelAsync();

        try
        {
            await run.WaitAsync(_guard, TestContext.CancellationToken);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // The writer may end with the cancellation.
        }
    }

    /// <summary>
    /// Creates a sender over a running buffer and records its statuses.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The sender</returns>
    private LiveSender CreateSender(QueuedIngestConnector connector, TimeProvider timeProvider)
    {
        return CreateSender(connector, timeProvider, CreateRunningBuffer(timeProvider));
    }

    /// <summary>
    /// Creates a sender over a buffer and records its statuses.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="buffer">The buffer, running</param>
    /// <returns>The sender</returns>
    private LiveSender CreateSender(QueuedIngestConnector connector, TimeProvider timeProvider, LiveBuffer buffer)
    {
        var sender = LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, connector, timeProvider, _logger);

        sender.StatusChanged += OnStatusChanged;
        _senders.Add(sender);

        return sender;
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
    /// Polls a condition, ten milliseconds apart, until it holds or the real-time guard runs out, and asserts that it holds.
    /// </summary>
    /// <param name="condition">The condition</param>
    /// <param name="description">What the condition says, as the assert message</param>
    /// <returns>A task that completes when the condition holds</returns>
    private async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var polls = 0;

        while (condition() == false && polls < (int)(_guard.TotalMilliseconds / 10))
        {
            await Task.Delay(10, TestContext.CancellationToken);

            polls++;
        }

        Assert.IsTrue(condition(), description);
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

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Dispose();
    }

    #endregion // IDisposable
}