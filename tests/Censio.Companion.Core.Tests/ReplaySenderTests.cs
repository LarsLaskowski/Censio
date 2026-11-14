using System.Net.WebSockets;
using System.Text;

using Censio.Contracts;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="ReplaySender" />.
/// </summary>
[TestClass]
public sealed class ReplaySenderTests
{
    #region Constants

    /// <summary>
    /// The token of the token file.
    /// </summary>
    private const string Token = "ReplayToken-0123456789abcdefghijKLMNOP";

    /// <summary>
    /// The name of the island of the ticks.
    /// </summary>
    private const string IslandName = "Isle Of Hidden Names";

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private const string CompanionVersion = "1.2.3";

    /// <summary>
    /// The host of the server address.
    /// </summary>
    private const string ServerHost = "secret-host.example";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start time of the time provider, in UTC.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 14, 12, 34, 56, 789, TimeSpan.Zero);

    /// <summary>
    /// The local time zone of the time provider, two hours ahead of UTC.
    /// </summary>
    private static readonly TimeZoneInfo _localZone = TimeZoneInfo.CreateCustomTimeZone("Censio/ReplaySenderTests", TimeSpan.FromHours(2), "Replay test time", "Replay test time");

    /// <summary>
    /// The address of the server.
    /// </summary>
    private static readonly Uri _address = new UriBuilder(Uri.UriSchemeWs, ServerHost, 8080, "/ingest", "?key=hidden").Uri;

    /// <summary>
    /// The lines of a connection and its end with one tick sent and one refused.
    /// </summary>
    private static readonly string[] _connectedAndEndLines = ["Replay connected to the server", "Replay sent 1 ticks, 1 refused"];

    /// <summary>
    /// The lines of a connection that is lost.
    /// </summary>
    private static readonly string[] _connectedAndLostLines = ["Replay connected to the server", "Replay sending failed: server connection lost"];

    /// <summary>
    /// The levels of the lines of a connection and its end.
    /// </summary>
    private static readonly LogLevel[] _connectedAndEndLevels = [LogLevel.Information, LogLevel.Information];

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The temporary directories of the test.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySenderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ReplaySenderTests(TestContext testContext)
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
    /// Deletes the temporary directories.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// The hello carries a new profile identity, the name of the local start time, the Companion's version and the token of
    /// the file, and the connector is asked for the address as given.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task ReplaySenderHelloCarriesNewProfileNameAndToken()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var connector = new DuplexReplayConnector(server.ClientSocket);
        var timeProvider = CreateTimeProvider();
        var tokenFile = WriteTokenFile(Token + "\r\n");
        await using var sender = CreateSender(connector, timeProvider, new RecordingLogger(), tokenFile);
        await using var other = CreateSender(new DuplexReplayConnector(null), timeProvider, new RecordingLogger(), tokenFile);

        // Act
        var prepared = sender.Prepare();
        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);
        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var connected = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(prepared, "The token file should be read");
        Assert.IsNull(connected, "The accepted hello should connect");

        var hello = Assert.IsInstanceOfType<ContractHello>(message, "The first message should be the hello");

        Assert.AreEqual(Token, hello.Token, "The hello should carry the token of the file");
        Assert.AreEqual("Replay 2026-11-14 14:34:56", hello.ProfileName, "The profile name should be the local start time");
        Assert.AreEqual(hello.ProfileName, sender.ProfileName, "The sender should name the profile it sent");
        Assert.AreEqual(sender.ProfileId, hello.ProfileId, "The hello should carry the sender's profile identity");
        Assert.AreNotEqual(Guid.Empty, hello.ProfileId, "The profile identity should not be empty");
        Assert.AreNotEqual(other.ProfileId, sender.ProfileId, "Each run should have a new profile identity");
        Assert.AreEqual(CompanionVersion, hello.CompanionVersion, "The hello should carry the Companion's version");
        Assert.AreSequenceEqual([_address], connector.Addresses, "The connector should be asked once for the address as given");
    }

    /// <summary>
    /// Without a token file the hello carries no token.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task ReplaySenderWithoutTokenFileSendsNoToken()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        // Act
        var prepared = sender.Prepare();
        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);
        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var connected = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(prepared, "Without a token file there should be nothing to read");
        Assert.IsNull(connected, "The accepted hello should connect");

        var hello = Assert.IsInstanceOfType<ContractHello>(message, "The first message should be the hello");

        Assert.IsNull(hello.Token, "Without a token file the hello should carry no token");
    }

    /// <summary>
    /// A connector that gives no socket is server unreachable.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ReplaySenderUnreachableServerGivesServerUnreachable()
    {
        // Arrange
        var connector = new DuplexReplayConnector(null);
        await using var sender = CreateSender(connector, CreateTimeProvider(), new RecordingLogger(), null);

        // Act
        var problem = await sender.ConnectServerAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.ServerUnreachable, problem, "No socket should be server unreachable");
        Assert.HasCount(1, connector.Addresses, "The connector should be asked once");
    }

    /// <summary>
    /// A refused hello gives the problem of its reason.
    /// </summary>
    /// <param name="reason">The reason of the refusal</param>
    /// <param name="expected">The expected problem</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(ContractHelloRefusal.TokenRefused, ReplaySendProblemKind.TokenRefused)]
    [DataRow(ContractHelloRefusal.CompanionConnected, ReplaySendProblemKind.CompanionConnected)]
    [DataRow(ContractHelloRefusal.UnsupportedVersion, ReplaySendProblemKind.VersionNotSupported)]
    public async Task ReplaySenderHelloRefusedGivesReason(ContractHelloRefusal reason, ReplaySendProblemKind expected)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        // Act
        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await server.SendAsync(CreateHelloRefused(reason), TestContext.CancellationToken);

        var problem = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, problem, $"The refusal {reason} should give its problem");
    }

    /// <summary>
    /// A hello without a reply within the reply timeout is no answer, not before, and the connection is closed.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ReplaySenderNoHelloReplyGivesNoAnswer()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var timeProvider = CreateTimeProvider();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), timeProvider, new RecordingLogger(), null);

        // Act
        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var isCompletedBefore = await ExpireReplyTimeoutAsync(timeProvider, connecting, 1);
        var problem = await connecting.WaitAsync(_guard, TestContext.CancellationToken);
        var afterwards = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(isCompletedBefore, "The sender should still wait before the reply timeout");
        Assert.AreEqual(ReplaySendProblemKind.NoAnswer, problem, "No reply within the timeout should be no answer");
        Assert.IsNull(afterwards, "The connection should be closed");
        Assert.AreEqual(TimeSpan.FromSeconds(30), ReplaySender.ReplyTimeout, "The reply timeout should be 30 seconds");
    }

    /// <summary>
    /// A tick without an acknowledgement within the reply timeout is no answer, not before, and the connection is closed.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ReplaySenderNoAcknowledgementGivesNoAnswer()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var timeProvider = CreateTimeProvider();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), timeProvider, new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        var replyTimers = CountReplyTimers(timeProvider);

        // Act
        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var isCompletedBefore = await ExpireReplyTimeoutAsync(timeProvider, sending, replyTimers + 1);
        var problem = await sending.WaitAsync(_guard, TestContext.CancellationToken);
        var afterwards = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(isCompletedBefore, "The sender should still wait before the reply timeout");
        Assert.AreEqual(ReplaySendProblemKind.NoAnswer, problem, "No acknowledgement within the timeout should be no answer");
        Assert.IsNull(afterwards, "The connection should be closed");
    }

    /// <summary>
    /// A connection that ends after the hello, by a close frame or without one, before a send or while its acknowledgement
    /// is awaited, is connection lost.
    /// </summary>
    /// <param name="isCloseFrame">Whether the server sends a close frame; otherwise the connection just ends</param>
    /// <param name="isWhileAwaited">Whether the connection ends while an acknowledgement is awaited</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    public async Task ReplaySenderCloseAfterHelloGivesConnectionLost(bool isCloseFrame, bool isWhileAwaited)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var problem = await SendTickThroughBrokenConnectionAsync(sender, server, isCloseFrame, isWhileAwaited);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.ConnectionLost, problem, "A connection that ends after the hello should be connection lost");
        Assert.AreEqual(0, sender.Counts.Ticks, "No tick should be counted as sent");
    }

    /// <summary>
    /// A reply that is not JSON, a binary frame, a message of a type the server does not send at that point, an
    /// acknowledgement of another message and one while nothing is awaited are an invalid reply, and the connection is
    /// closed.
    /// </summary>
    /// <param name="reply">The reply of the server</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("notJson")]
    [DataRow("binary")]
    [DataRow("helloAccepted")]
    [DataRow("otherId")]
    [DataRow("unawaited")]
    public async Task ReplaySenderInvalidReplyGivesInvalidServerReply(string reply)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var problem = await SendTickWithReplyAsync(sender, server, reply);
        var afterwards = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.InvalidReply, problem, $"The reply {reply} should be an invalid reply");
        Assert.IsNull(afterwards, $"After the reply {reply} the connection should be closed");
    }

    /// <summary>
    /// A message acknowledged with a refusal is counted as refused, an accepted session event is not counted, and the
    /// replay goes on.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ReplaySenderRefusedAcknowledgementCountsRefused()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var refusedTick = await SendAcknowledgedAsync(sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken), server, ContractAcknowledgementRefusal.Invalid);
        var refusedStart = await SendAcknowledgedAsync(sender.SendSessionStartAsync(_start, TestContext.CancellationToken), server, ContractAcknowledgementRefusal.Malformed);
        var acceptedEnd = await SendAcknowledgedAsync(sender.SendSessionEndAsync(_start, TestContext.CancellationToken), server, null);
        var acceptedTick = await SendAcknowledgedAsync(sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken), server, null);

        // Assert
        Assert.IsNull(refusedTick, "A refused tick should not stop the replay");
        Assert.IsNull(refusedStart, "A refused session start should not stop the replay");
        Assert.IsNull(acceptedEnd, "An accepted session end should give no problem");
        Assert.IsNull(acceptedTick, "An accepted tick should give no problem");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 1,
                            Refused = 2
                        },
                        sender.Counts,
                        "One tick should be sent and the two refused messages counted");
    }

    /// <summary>
    /// A tick the contract writer refuses is counted as refused without being sent, and the replay goes on.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ReplaySenderWriterRefusalCountsRefusedAndGoesOn()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var refused = await sender.SendTickAsync(CreateTick(ContractReader.MaxIslands + 1), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);
        var next = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await server.AcknowledgeAsync(next, null, TestContext.CancellationToken);

        var sent = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(refused, "A tick the writer refuses should not stop the replay");
        Assert.IsNull(sent, "The next tick should be sent");

        var tick = Assert.IsInstanceOfType<ContractTick>(next, "The next message should be the next tick");

        Assert.HasCount(1, tick.Islands, "The refused tick should not have been sent");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 1,
                            Refused = 1
                        },
                        sender.Counts,
                        "The refused tick and the sent tick should be counted");
    }

    /// <summary>
    /// A send waits for its acknowledgement before it returns, so the next message is sent only after it.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderSendsNextMessageOnlyAfterAcknowledgement()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var firstSending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);
        var first = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var isCompletedBeforeAcknowledgement = firstSending.IsCompleted;

        await server.AcknowledgeAsync(first, null, TestContext.CancellationToken);

        var firstProblem = await firstSending.WaitAsync(_guard, TestContext.CancellationToken);
        var secondSending = sender.SendSessionEndAsync(_start, TestContext.CancellationToken);
        var second = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await server.AcknowledgeAsync(second, null, TestContext.CancellationToken);

        var secondProblem = await secondSending.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(isCompletedBeforeAcknowledgement, "The send should wait for its acknowledgement");
        Assert.IsInstanceOfType<ContractTick>(first, "The first message should be the tick");
        Assert.IsInstanceOfType<ContractSessionEnd>(second, "The second message should be the session end");
        Assert.IsNull(firstProblem, "The acknowledged tick should give no problem");
        Assert.IsNull(secondProblem, "The acknowledged session end should give no problem");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 1,
                            Refused = 0
                        },
                        sender.Counts,
                        "One tick should be counted as sent");
    }

    /// <summary>
    /// Each change of the state is sent as a status without waiting for a reply, also while an acknowledgement is awaited,
    /// and a state that did not change is not sent again.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderStateChangeSendsStatusWithoutWaiting()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        sender.NotifyState(PipeConnectionState.Connected);
        sender.NotifyState(PipeConnectionState.Connected);
        sender.NotifyState(PipeConnectionState.Receiving);

        var first = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var second = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);
        var tick = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        sender.NotifyState(PipeConnectionState.ProtocolMismatch);

        var third = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await server.AcknowledgeAsync(tick, null, TestContext.CancellationToken);

        var problem = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ContractPipeState.Connected, Assert.IsInstanceOfType<ContractStatus>(first, "The first message should be a status").PipeState, "The first status should be connected");
        Assert.AreEqual(ContractPipeState.Receiving, Assert.IsInstanceOfType<ContractStatus>(second, "The repeated state should not be sent again").PipeState, "The second status should be receiving");
        Assert.IsInstanceOfType<ContractTick>(tick, "The tick should follow the statuses");
        Assert.AreEqual(ContractPipeState.ProtocolMismatch, Assert.IsInstanceOfType<ContractStatus>(third, "A status should be sent while the acknowledgement is awaited").PipeState, "The third status should be the protocol mismatch");
        Assert.IsNull(problem, "The tick should be acknowledged");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 1,
                            Refused = 0
                        },
                        sender.Counts,
                        "Statuses should not be counted");
    }

    /// <summary>
    /// A reply over the bound is an invalid reply, and the sender reads no further than the bound and its frame header.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderReplyOverBoundReadsNoFurther()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        var bytesBefore = server.ClientBytesRead;

        // Act
        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);
        var tick = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var oversized = SendIgnoringFailureAsync(server, CreatePaddedAcknowledgement(tick, 200000));
        var problem = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        await sender.DisposeAsync();
        await oversized.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.InvalidReply, problem, "A reply over 65,536 bytes should be an invalid reply, even a valid acknowledgement");
        Assert.AreEqual(65536, ReplaySender.MaxReplyBytes, "The bound should be 65,536 bytes");
        Assert.IsLessThanOrEqualTo(ReplaySender.MaxReplyBytes + 1L + 14L, server.ClientBytesRead - bytesBefore, "The sender should read no more than the bound, one byte and a frame header");
    }

    /// <summary>
    /// The log has the connection and the end with its counts.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task ReplaySenderLogsConnectionAndEnd()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var logger = new RecordingLogger();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), logger, null);

        await ConnectAcceptedAsync(sender, server);
        await SendAcknowledgedAsync(sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken), server, null);
        await SendAcknowledgedAsync(sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken), server, ContractAcknowledgementRefusal.TooLarge);

        // Act
        var serverEnd = server.ReceiveAsync(TestContext.CancellationToken);
        var problem = await sender.CloseAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var closeFrame = await serverEnd.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(problem, "The close should give no problem");
        Assert.IsNull(closeFrame, "The server should receive the close");
        Assert.AreSequenceEqual(_connectedAndEndLines, logger.Entries.Select(entry => entry.Message), "The log should have the connection and the end with its counts");
        Assert.AreSequenceEqual(_connectedAndEndLevels, logger.Entries.Select(entry => entry.Level), "Both lines should be information");
    }

    /// <summary>
    /// Every failure logs its reason, and no log line holds the token, the address or a name, nor an exception.
    /// </summary>
    /// <param name="failure">The failure</param>
    /// <param name="expectedLine">The line the failure logs</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    [DataRow("unreachable", "Replay sending failed: server unreachable")]
    [DataRow("tokenRefused", "Replay refused by the server: token refused")]
    [DataRow("companionConnected", "Replay refused by the server: another Companion is connected")]
    [DataRow("unsupportedVersion", "Replay refused by the server: server does not support contract version 1")]
    [DataRow("noAnswer", "Replay sending failed: server does not answer")]
    [DataRow("connectionLost", "Replay sending failed: server connection lost")]
    [DataRow("invalidReply", "Replay sending failed: invalid server reply")]
    public async Task ReplaySenderLogsNeitherTokenNorAddressNorNames(string failure, string expectedLine)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var logger = new RecordingLogger();
        var timeProvider = CreateTimeProvider();
        var connector = new DuplexReplayConnector(failure == "unreachable" ? null : server.ClientSocket);
        await using var sender = CreateSender(connector, timeProvider, logger, WriteTokenFile(Token));

        sender.Prepare();

        // Act
        var problem = await RunFailureAsync(sender, server, timeProvider, failure);

        // Assert
        Assert.IsNotNull(problem, $"The failure {failure} should give a problem");
        Assert.Contains(expectedLine, logger.Entries.Select(entry => entry.Message), $"The failure {failure} should log its reason");
        Assert.DoesNotContain(entry => entry.Message.Contains(Token, StringComparison.Ordinal), logger.Entries, "No log line should hold the token");
        Assert.DoesNotContain(entry => entry.Message.Contains(ServerHost, StringComparison.OrdinalIgnoreCase), logger.Entries, "No log line should hold the address");
        Assert.DoesNotContain(entry => entry.Message.Contains(IslandName, StringComparison.Ordinal), logger.Entries, "No log line should hold a name");
        Assert.DoesNotContain(entry => entry.Exception is not null, logger.Entries, "No log line should carry an exception, whose text can hold the address");
    }

    /// <summary>
    /// An exception of a type no catch list names, whose message holds the address, is connection lost; it leaves the
    /// sender neither as an exception nor in a log line.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task ReplaySenderUnlistedExceptionGivesConnectionLostWithoutAddress()
    {
        // Arrange
        var socket = new ThrowingWebSocket(new KeyNotFoundException("No route to " + _address));
        var logger = new RecordingLogger();
        await using var sender = CreateSender(new DuplexReplayConnector(socket), CreateTimeProvider(), logger, null);

        // Act
        var problem = await sender.ConnectServerAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.ConnectionLost, problem, "An unlisted exception should be connection lost");
        Assert.IsGreaterThan(0, socket.Calls, "The socket should have been used");
        Assert.Contains("Replay sending failed: server connection lost", logger.Entries.Select(entry => entry.Message), "The failure should be logged with its reason");
        Assert.DoesNotContain(entry => entry.Message.Contains(ServerHost, StringComparison.OrdinalIgnoreCase), logger.Entries, "No log line should hold the address of the exception's message");
        Assert.DoesNotContain(entry => entry.Exception is not null, logger.Entries, "No log line should carry the exception");
    }

    /// <summary>
    /// Between messages a receive is pending, so a frame of the server is read at once: an acknowledgement nothing awaits
    /// closes the connection without any further call of the sender.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task ReplaySenderFrameBetweenMessagesIsReadAtOnce()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        await server.SendAsync(ScriptedIngestServer.CreateAcknowledgement(Guid.NewGuid(), null), TestContext.CancellationToken);

        var afterwards = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(afterwards, "The frame should be read at once and the connection closed");
    }

    /// <summary>
    /// A token file that cannot be read or holds no valid token gives its problem and logs its reason.
    /// </summary>
    /// <param name="content">The content of the file; <c>null</c> for a file that does not exist</param>
    /// <param name="expected">The expected problem</param>
    /// <param name="expectedLine">The expected log line</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(null, ReplaySendProblemKind.TokenFileCannotBeRead, "Replay sending failed: token file cannot be read")]
    [DataRow("too-short-token", ReplaySendProblemKind.TokenFileHoldsNoValidToken, "Replay sending failed: token file holds no valid token")]
    public async Task ReplaySenderTokenFileProblemGivesItsKind(string? content, ReplaySendProblemKind expected, string expectedLine)
    {
        // Arrange
        var logger = new RecordingLogger();
        var connector = new DuplexReplayConnector(null);
        await using var sender = CreateSender(connector, CreateTimeProvider(), logger, CreateTokenFilePath(content));

        // Act
        var problem = sender.Prepare();

        // Assert
        Assert.AreEqual(expected, problem, "The token file should give its problem");
        Assert.AreSequenceEqual([expectedLine], logger.Entries.Select(entry => entry.Message), "The problem should be logged with its reason");
        Assert.IsEmpty(connector.Addresses, "No connection should be opened");
    }

    /// <summary>
    /// A tick without islands is neither sent nor counted, and the next tick is sent.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ReplaySenderTickWithoutIslandsIsNotSent()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var empty = await sender.SendTickAsync(CreateTick(0), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var next = await SendAcknowledgedAsync(sender.SendTickAsync(CreateTick(2), TestContext.CancellationToken), server, null);

        // Assert
        Assert.IsNull(empty, "A tick without islands should give no problem");
        Assert.IsNull(next, "The next tick should be acknowledged");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 1,
                            Refused = 0
                        },
                        sender.Counts,
                        "Only the tick with islands should be counted");
    }

    /// <summary>
    /// A hello whose reply is a close frame is connection lost; one whose reply is not a readable hello reply is an invalid
    /// reply.
    /// </summary>
    /// <param name="reply">The reply of the server</param>
    /// <param name="expected">The expected problem</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("close", ReplaySendProblemKind.ConnectionLost)]
    [DataRow("notJson", ReplaySendProblemKind.InvalidReply)]
    [DataRow("binary", ReplaySendProblemKind.InvalidReply)]
    [DataRow("acknowledgement", ReplaySendProblemKind.InvalidReply)]
    public async Task ReplaySenderHelloReplyProblemGivesItsKind(string reply, ReplaySendProblemKind expected)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        // Act
        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await ReplyToHelloAsync(server, reply);

        var problem = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, problem, $"The hello reply {reply} should give its problem");
        Assert.AreEqual(WebSocketState.Aborted, server.ClientSocket.State, $"After the hello reply {reply} the connection should be closed");
    }

    /// <summary>
    /// A hello the contract writer refuses is not sent and ends the connection as lost.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task ReplaySenderHelloWriterRefusalGivesConnectionLost()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = new ReplaySender(_address, null, new string('9', ContractReader.MaxStringBytes + 1), new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger());

        // Act
        var problem = await sender.ConnectServerAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var received = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.ConnectionLost, problem, "A hello the writer refuses should be connection lost");
        Assert.IsNull(received, "The server should receive no hello, only the end of the connection");
    }

    /// <summary>
    /// A reply of exactly 65,536 bytes is read as an acknowledgement.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderReplyOfBoundIsRead()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var problem = await SendTickWithAcknowledgementOfSizeAsync(sender, server, ReplaySender.MaxReplyBytes);

        // Assert
        Assert.IsNull(problem, "A reply of 65,536 bytes should be read");
        Assert.AreEqual(new ReplaySendCounts
                        {
                            Ticks = 1,
                            Refused = 0
                        },
                        sender.Counts,
                        "The acknowledged tick should be counted");
    }

    /// <summary>
    /// A reply of one byte more than 65,536 bytes is an invalid reply, also as a single frame.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderReplyOneOverBoundIsInvalid()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var problem = await SendTickWithAcknowledgementOfSizeAsync(sender, server, ReplaySender.MaxReplyBytes + 1);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.InvalidReply, problem, "A reply of 65,537 bytes should be an invalid reply");
        Assert.AreEqual(0, sender.Counts.Ticks, "The tick should not be counted as sent");
    }

    /// <summary>
    /// The caller's cancellation while connecting, sending or closing is rethrown and ends the connection.
    /// </summary>
    /// <param name="step">The step that is cancelled</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow("connect")]
    [DataRow("send")]
    [DataRow("close")]
    public async Task ReplaySenderCallerCancellationIsRethrown(string step)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), CreateTimeProvider(), new RecordingLogger(), null);
        using var cancellation = new CancellationTokenSource();

        // Act
        var running = await StartCancelledStepAsync(sender, server, step, cancellation);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => running.WaitAsync(_guard, TestContext.CancellationToken), $"The cancelled {step} should rethrow the cancellation");
        Assert.AreEqual(WebSocketState.Aborted, server.ClientSocket.State, $"The cancelled {step} should end the connection");
    }

    /// <summary>
    /// A close the server does not answer is aborted after two seconds of the time provider, not before, and ends without
    /// a problem.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ReplaySenderCloseWithoutServerCloseAbortsAfterTwoSeconds()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var timeProvider = CreateTimeProvider();
        var logger = new RecordingLogger();
        await using var sender = CreateSender(new DuplexReplayConnector(server.ClientSocket), timeProvider, logger, null);

        await ConnectAcceptedAsync(sender, server);

        // Act
        var closing = sender.CloseAsync(TestContext.CancellationToken);

        await timeProvider.WaitForDelayAsync(TimeSpan.FromSeconds(2), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));

        var isCompletedBefore = closing.IsCompleted;

        timeProvider.Advance(TimeSpan.FromTicks(1));

        var problem = await closing.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(isCompletedBefore, "The close should wait for the server's close up to two seconds");
        Assert.IsNull(problem, "A close the server does not answer should give no problem");
        Assert.AreEqual(WebSocketState.Aborted, server.ClientSocket.State, "The connection should be aborted after two seconds");
        Assert.Contains("Replay sent 0 ticks, 0 refused", logger.Entries.Select(entry => entry.Message), "The end should be logged");
    }

    /// <summary>
    /// A close after a failure gives that failure and logs no end.
    /// </summary>
    /// <param name="failure">The failure</param>
    /// <param name="expected">The expected problem</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("unreachable", ReplaySendProblemKind.ServerUnreachable)]
    [DataRow("tokenRefused", ReplaySendProblemKind.TokenRefused)]
    [DataRow("invalidReply", ReplaySendProblemKind.InvalidReply)]
    public async Task ReplaySenderCloseAfterFailureGivesTheFailure(string failure, ReplaySendProblemKind expected)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var logger = new RecordingLogger();
        var timeProvider = CreateTimeProvider();
        await using var sender = CreateSender(new DuplexReplayConnector(failure == "unreachable" ? null : server.ClientSocket), timeProvider, logger, null);

        await RunFailureAsync(sender, server, timeProvider, failure);

        // Act
        var problem = await sender.CloseAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, problem, $"The close after {failure} should give that failure");
        Assert.DoesNotContain(entry => entry.Message.StartsWith("Replay sent", StringComparison.Ordinal), logger.Entries, "A failed run should log no end");
    }

    /// <summary>
    /// A status whose send fails ends the run as connection lost at the next awaited step.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderStatusSendFailureGivesConnectionLost()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var socket = new SendFailingWebSocket(server.ClientSocket);
        var logger = new RecordingLogger();
        await using var sender = CreateSender(new DuplexReplayConnector(socket), CreateTimeProvider(), logger, null);

        await ConnectAcceptedAsync(sender, server);
        socket.FailSends();

        // Act
        sender.NotifyState(PipeConnectionState.Connected);

        await socket.FailedSend.WaitAsync(_guard, TestContext.CancellationToken);

        var problem = await sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.ConnectionLost, problem, "A failed status send should end the run as connection lost");
        Assert.ContainsSingle(entry => entry.Message == "Replay sending failed: server connection lost", logger.Entries, "The failure should be logged once");
    }

    /// <summary>
    /// A tick or a close frame whose write fails ends the run as connection lost.
    /// </summary>
    /// <param name="step">The step whose write fails: tick or close</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("tick")]
    [DataRow("close")]
    public async Task ReplaySenderWriteFailureGivesConnectionLost(string step)
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var socket = new SendFailingWebSocket(server.ClientSocket);
        var logger = new RecordingLogger();
        await using var sender = CreateSender(new DuplexReplayConnector(socket), CreateTimeProvider(), logger, null);

        await ConnectAcceptedAsync(sender, server);
        socket.FailSends();

        // Act
        var problem = await RunStepAsync(sender, step).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ReplaySendProblemKind.ConnectionLost, problem, $"A failed write of the {step} should be connection lost");
        Assert.AreSequenceEqual(_connectedAndLostLines, logger.Entries.Select(entry => entry.Message), "The failure should be logged, and no end");
    }

    /// <summary>
    /// The caller's cancellation before the connection is opened is rethrown.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task ReplaySenderCancelledBeforeConnectionIsRethrown()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var connector = new DuplexReplayConnector(server.ClientSocket);
        await using var sender = CreateSender(connector, CreateTimeProvider(), new RecordingLogger(), null);
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        // Act
        var connecting = sender.ConnectServerAsync(cancellation.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => connecting.WaitAsync(_guard, TestContext.CancellationToken), "The cancellation should be rethrown");
        Assert.HasCount(1, connector.Addresses, "The connector should have been asked");
        Assert.AreEqual(WebSocketState.Open, server.ClientSocket.State, "A socket the connector did not give should be left alone");
    }

    /// <summary>
    /// Creates a time provider that records its timers, at the start time in the local zone of the tests.
    /// </summary>
    /// <returns>The time provider</returns>
    private static RecordingTimeProvider CreateTimeProvider()
    {
        var timeProvider = new RecordingTimeProvider(_start);

        timeProvider.SetLocalTimeZone(_localZone);

        return timeProvider;
    }

    /// <summary>
    /// Creates a sender to the address of the tests.
    /// </summary>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <param name="tokenFilePath">The path of the token file, if any</param>
    /// <returns>The sender</returns>
    private static ReplaySender CreateSender(IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger, string? tokenFilePath)
    {
        return new ReplaySender(_address, tokenFilePath, CompanionVersion, connector, timeProvider, logger);
    }

    /// <summary>
    /// Creates a refused hello.
    /// </summary>
    /// <param name="reason">The reason</param>
    /// <returns>The refused hello</returns>
    private static ContractHelloRefused CreateHelloRefused(ContractHelloRefusal reason)
    {
        return new ContractHelloRefused
               {
                   Reason = reason,
                   ReceivedVersion = 1,
                   SupportedVersions = [2]
               };
    }

    /// <summary>
    /// Creates a tick of islands that have distinct keys and the name of the tests.
    /// </summary>
    /// <param name="islands">The number of islands</param>
    /// <returns>The tick</returns>
    private static PipeTick CreateTick(int islands)
    {
        return new PipeTick
               {
                   GameTime = 123456,
                   ReceiveTime = _start,
                   Islands = [.. Enumerable.Range(0, islands).Select(CreateIsland)],
                   CloseReason = PipeTickCloseReason.GameTimeChanged
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
                   Name = IslandName,
                   Entries = []
               };
    }

    /// <summary>
    /// Creates the acknowledgement of a message, preceded by white space to a size over the bound.
    /// </summary>
    /// <param name="message">The acknowledged message</param>
    /// <param name="padding">The number of spaces before the acknowledgement</param>
    /// <returns>The bytes of the reply</returns>
    private static byte[] CreatePaddedAcknowledgement(ContractMessage? message, int padding)
    {
        var acknowledgement = ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(ScriptedIngestServer.IdOf(message), null));

        return [.. Encoding.UTF8.GetBytes(new string(' ', padding)), .. acknowledgement];
    }

    /// <summary>
    /// Counts the reply timers the sender created so far.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The number of timers of the reply timeout</returns>
    private static int CountReplyTimers(RecordingTimeProvider timeProvider)
    {
        return timeProvider.Delays.Count(delay => delay == ReplaySender.ReplyTimeout);
    }

    /// <summary>
    /// Writes a token file into a new temporary directory.
    /// </summary>
    /// <param name="content">The content</param>
    /// <returns>The path of the file</returns>
    private string WriteTokenFile(string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-replay-sender-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        var path = Path.Combine(directory, "token.txt");

        File.WriteAllText(path, content, new UTF8Encoding(false));

        return path;
    }

    /// <summary>
    /// Returns the path of a token file in a new temporary directory, written with a content or not written at all.
    /// </summary>
    /// <param name="content">The content; <c>null</c> for a file that does not exist</param>
    /// <returns>The path of the file</returns>
    private string CreateTokenFilePath(string? content)
    {
        if (content is not null)
        {
            return WriteTokenFile(content);
        }

        var directory = Path.Combine(Path.GetTempPath(), "censio-replay-sender-" + Guid.NewGuid().ToString("N"));

        return Path.Combine(directory, "missing-token.txt");
    }

    /// <summary>
    /// Answers the hello with a reply that is not a hello reply, or with a close frame.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="reply">The reply: close, notJson, binary or acknowledgement</param>
    /// <returns>A task that completes when the reply is sent</returns>
    private async Task ReplyToHelloAsync(ScriptedIngestServer server, string reply)
    {
        var acknowledgement = ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(Guid.NewGuid(), null));

        switch (reply)
        {
            case "close":
                await server.CloseOutputAsync(TestContext.CancellationToken);
                break;

            case "notJson":
                await server.SendRawAsync(Encoding.UTF8.GetBytes("not a contract message"), WebSocketMessageType.Text, TestContext.CancellationToken);
                break;

            case "binary":
                await server.SendRawAsync(acknowledgement, WebSocketMessageType.Binary, TestContext.CancellationToken);
                break;

            default:
                await server.SendRawAsync(acknowledgement, WebSocketMessageType.Text, TestContext.CancellationToken);
                break;
        }
    }

    /// <summary>
    /// Sends a tick and answers it with its acknowledgement padded with leading white space to a total size.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="size">The size of the reply in bytes</param>
    /// <returns>The problem of the send</returns>
    private async Task<ReplaySendProblemKind?> SendTickWithAcknowledgementOfSizeAsync(ReplaySender sender, ScriptedIngestServer server, int size)
    {
        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);
        var tick = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var acknowledgementSize = ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(ScriptedIngestServer.IdOf(tick), null)).Length;
        var replying = SendIgnoringFailureAsync(server, CreatePaddedAcknowledgement(tick, size - acknowledgementSize));
        var problem = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        await replying.WaitAsync(_guard, TestContext.CancellationToken);

        return problem;
    }

    /// <summary>
    /// Starts a step of the sender with a token that is cancelled while the step waits for the server.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="step">The step: connect, send or close</param>
    /// <param name="cancellation">The source of the token</param>
    /// <returns>The task of the cancelled step</returns>
    private async Task<Task> StartCancelledStepAsync(ReplaySender sender, ScriptedIngestServer server, string step, CancellationTokenSource cancellation)
    {
        if (step == "connect")
        {
            var connecting = sender.ConnectServerAsync(cancellation.Token);

            await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
            await cancellation.CancelAsync();

            return connecting;
        }

        await ConnectAcceptedAsync(sender, server);

        if (step == "send")
        {
            var sending = sender.SendTickAsync(CreateTick(1), cancellation.Token);

            await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
            await cancellation.CancelAsync();

            return sending;
        }

        var closing = sender.CloseAsync(cancellation.Token);

        await cancellation.CancelAsync();

        return closing;
    }

    /// <summary>
    /// Runs a step of a connected sender: a tick send or the close.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="step">The step: tick or close</param>
    /// <returns>The problem of the step</returns>
    private Task<ReplaySendProblemKind?> RunStepAsync(ReplaySender sender, string step)
    {
        return step == "tick"
                   ? sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken)
                   : sender.CloseAsync(TestContext.CancellationToken);
    }

    /// <summary>
    /// Connects a sender to the scripted server, which accepts the hello.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <returns>A task that completes when the sender is connected</returns>
    private async Task ConnectAcceptedAsync(ReplaySender sender, ScriptedIngestServer server)
    {
        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);

        await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var problem = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        Assert.IsNull(problem, "The accepted hello should connect");
    }

    /// <summary>
    /// Waits until the sender started its next reply timer, then lets the time run to one tick before the timeout and
    /// to the timeout.
    /// </summary>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="waiting">The task that waits for the reply</param>
    /// <param name="replyTimers">The number of reply timers the sender has started when it waits</param>
    /// <returns>Whether the task was completed one tick before the timeout</returns>
    private async Task<bool> ExpireReplyTimeoutAsync(RecordingTimeProvider timeProvider, Task waiting, int replyTimers)
    {
        while (CountReplyTimers(timeProvider) < replyTimers)
        {
            await timeProvider.WaitForTimersAsync(timeProvider.Delays.Count + 1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        timeProvider.Advance(ReplaySender.ReplyTimeout - TimeSpan.FromTicks(1));

        var isCompleted = waiting.IsCompleted;

        timeProvider.Advance(TimeSpan.FromTicks(1));

        return isCompleted;
    }

    /// <summary>
    /// Waits until the server received the message of a send, acknowledges it and returns the send's result.
    /// </summary>
    /// <param name="sending">The send</param>
    /// <param name="server">The server</param>
    /// <param name="refusal">The refusal, or <c>null</c> for an accepted message</param>
    /// <returns>The problem of the send</returns>
    private async Task<ReplaySendProblemKind?> SendAcknowledgedAsync(Task<ReplaySendProblemKind?> sending, ScriptedIngestServer server, ContractAcknowledgementRefusal? refusal)
    {
        var message = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        await server.AcknowledgeAsync(message, refusal, TestContext.CancellationToken);

        return await sending.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Sends a tick over a connection the server ends, before the send or while its acknowledgement is awaited.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="isCloseFrame">Whether the server sends a close frame; otherwise the connection just ends</param>
    /// <param name="isWhileAwaited">Whether the connection ends while the acknowledgement is awaited</param>
    /// <returns>The problem of the send</returns>
    private async Task<ReplaySendProblemKind?> SendTickThroughBrokenConnectionAsync(ReplaySender sender, ScriptedIngestServer server, bool isCloseFrame, bool isWhileAwaited)
    {
        if (isWhileAwaited == false)
        {
            await BreakConnectionAsync(server, isCloseFrame);

            return await sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await BreakConnectionAsync(server, isCloseFrame);

        return await sending.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Ends the connection from the server's side.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="isCloseFrame">Whether the server sends a close frame; otherwise the connection just ends</param>
    /// <returns>A task that completes when the connection is ended</returns>
    private async Task BreakConnectionAsync(ScriptedIngestServer server, bool isCloseFrame)
    {
        if (isCloseFrame)
        {
            await server.CloseOutputAsync(TestContext.CancellationToken);
        }
        else
        {
            server.Abort();
        }
    }

    /// <summary>
    /// Sends a tick and answers it with a reply that is not valid at that point.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="reply">The reply: notJson, binary, helloAccepted, otherId or unawaited (an acknowledgement before the tick)</param>
    /// <returns>The problem of the send</returns>
    private async Task<ReplaySendProblemKind?> SendTickWithReplyAsync(ReplaySender sender, ScriptedIngestServer server, string reply)
    {
        if (reply == "unawaited")
        {
            await server.SendAsync(ScriptedIngestServer.CreateAcknowledgement(Guid.NewGuid(), null), TestContext.CancellationToken);

            // The sender reads the acknowledgement at once and closes, before the tick is sent.
            await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

            return await sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);
        var tick = await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var acknowledgement = ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(ScriptedIngestServer.IdOf(tick), null));

        var (data, messageType) = reply switch
                                  {
                                      "notJson" => (Encoding.UTF8.GetBytes("not a contract message"), WebSocketMessageType.Text),
                                      "binary" => (acknowledgement, WebSocketMessageType.Binary),
                                      "helloAccepted" => (ScriptedIngestServer.Write(new ContractHelloAccepted
                                                                                     {
                                                                                         ServerVersion = ScriptedIngestServer.ServerVersion,
                                                                                         CatalogIdentity = null
                                                                                     }), WebSocketMessageType.Text),
                                      _ => (ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(Guid.NewGuid(), null)), WebSocketMessageType.Text)
                                  };

        await server.SendRawAsync(data, messageType, TestContext.CancellationToken);

        return await sending.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Runs a sender, whose token file is read, into a failure, sending a tick with a named island first where the failure
    /// comes after the hello.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="failure">The failure</param>
    /// <returns>The problem</returns>
    private async Task<ReplaySendProblemKind?> RunFailureAsync(ReplaySender sender, ScriptedIngestServer server, RecordingTimeProvider timeProvider, string failure)
    {
        if (failure == "unreachable")
        {
            return await sender.ConnectServerAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        if (Enum.TryParse<ContractHelloRefusal>(failure, true, out var reason))
        {
            var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);

            await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
            await server.SendAsync(CreateHelloRefused(reason), TestContext.CancellationToken);

            return await connecting.WaitAsync(_guard, TestContext.CancellationToken);
        }

        await ConnectAcceptedAsync(sender, server);

        var replyTimers = CountReplyTimers(timeProvider);

        return failure switch
               {
                   "noAnswer" => await SendTickWithoutAnswerAsync(sender, server, timeProvider, replyTimers + 1),
                   "connectionLost" => await SendTickThroughBrokenConnectionAsync(sender, server, false, true),
                   _ => await SendTickWithReplyAsync(sender, server, "otherId")
               };
    }

    /// <summary>
    /// Sends a tick the server does not acknowledge and lets the reply timeout expire.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="server">The server</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="replyTimers">The number of reply timers the sender has started when it waits</param>
    /// <returns>The problem of the send</returns>
    private async Task<ReplaySendProblemKind?> SendTickWithoutAnswerAsync(ReplaySender sender, ScriptedIngestServer server, RecordingTimeProvider timeProvider, int replyTimers)
    {
        var sending = sender.SendTickAsync(CreateTick(1), TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await ExpireReplyTimeoutAsync(timeProvider, sending, replyTimers);

        return await sending.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Sends a reply that the sender may stop reading, ignoring the failure of the ended connection.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="data">The reply</param>
    /// <returns>A task that completes when the reply is sent or the connection ended</returns>
    private async Task SendIgnoringFailureAsync(ScriptedIngestServer server, byte[] data)
    {
        try
        {
            await server.SendRawAsync(data, WebSocketMessageType.Text, TestContext.CancellationToken);
        }
        catch (Exception exception) when (exception is WebSocketException or IOException or OperationCanceledException)
        {
            // The sender ended the connection while the reply was sent.
        }
    }

    #endregion // Methods
}