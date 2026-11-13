using System.Net.WebSockets;
using System.Text;

using Censio.Contracts;
using Censio.Pipe.Tests;
using Censio.Server.Data;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestSession" />, each over a real WebSocket pair on an <see cref="InMemoryDuplexStream" /> with a
/// fake clock.
/// </summary>
[TestClass]
public sealed class IngestSessionTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The stored token.
    /// </summary>
    private const string Token = "MARKERTOKEN-secret";

    /// <summary>
    /// A wrong token of the same length that differs only in its last character.
    /// </summary>
    private const string LastCharacterChanged = "MARKERTOKEN-secreT";

    /// <summary>
    /// The version the server reports.
    /// </summary>
    private const string ServerVersion = "1.2.3";

    /// <summary>
    /// The identity of the first message.
    /// </summary>
    private const string Id1 = "11111111-2222-4333-8444-555555555551";

    /// <summary>
    /// The identity of the second message.
    /// </summary>
    private const string Id2 = "11111111-2222-4333-8444-555555555552";

    /// <summary>
    /// The identity of the third message.
    /// </summary>
    private const string Id3 = "11111111-2222-4333-8444-555555555553";

    /// <summary>
    /// The identity of the fourth message.
    /// </summary>
    private const string Id4 = "11111111-2222-4333-8444-555555555554";

    /// <summary>
    /// The identity of the fifth message.
    /// </summary>
    private const string Id5 = "11111111-2222-4333-8444-555555555555";

    /// <summary>
    /// The all-zero identity.
    /// </summary>
    private const string ZeroId = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// The first icon reference in ordinal order.
    /// </summary>
    private const string IconA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The second icon reference in ordinal order.
    /// </summary>
    private const string IconB = "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// A server-to-Companion message, which a Companion may not send.
    /// </summary>
    private const string HelloAcceptedNotation = "{'type':'helloAccepted','contractVersion':1,'serverVersion':'0.3.0','catalogIdentity':null}";

    /// <summary>
    /// The bound of the first message in bytes.
    /// </summary>
    private const int HelloBound = 32768;

    /// <summary>
    /// The bound of every later message in bytes.
    /// </summary>
    private const int MessageBound = 25165824;

    /// <summary>
    /// The bytes a read may take beyond a bound: frame headers and the socket's own read-ahead.
    /// </summary>
    private const int ReadSlack = 4096;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The versions a refused hello names as supported.
    /// </summary>
    private static readonly int[] _supportedVersions = [1];

    /// <summary>
    /// The time the session has for its hello.
    /// </summary>
    private static readonly TimeSpan _helloTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The time a close waits for the peer's close.
    /// </summary>
    private static readonly TimeSpan _closeWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The catalog documents the catalog reader refuses, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<byte[]>> _refusedCatalogs = new()
                                                                                {
                                                                                    ["catalog version 2"] = () => Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"gameVersion\":null,\"entries\":[]}"),
                                                                                    ["catalog malformed"] = () => Encoding.UTF8.GetBytes("not json"),
                                                                                    ["catalog invalid"] = () => Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"gameVersion\":\"0.0.0.0\",\"entries\":[]}"),
                                                                                    ["catalog too large"] = () => new byte[CatalogDocumentReader.MaxDocumentBytes + 1]
                                                                                };

    /// <summary>
    /// The messages after the hello whose <c>id</c> cannot be used or whose type the server does not take: the name of the
    /// case and the message in single-quote notation.
    /// </summary>
    private static readonly Dictionary<string, string> _unreadableMessages = new()
                                                                             {
                                                                                 ["malformed"] = "{'type':'tick','contractVersion':1",
                                                                                 ["version 2"] = IngestClient.Tick(Id1).Replace("'contractVersion':1", "'contractVersion':2", StringComparison.Ordinal),
                                                                                 ["invalid id"] = IngestClient.Tick(ZeroId),
                                                                                 ["helloAccepted"] = HelloAcceptedNotation,
                                                                                 ["acknowledgement with a valid id"] = $"{{'type':'acknowledgement','contractVersion':1,'id':'{Id1}','refusal':null,'refusedVersion':null,'missingIcons':[]}}",
                                                                                 ["second hello"] = IngestClient.Hello(Token)
                                                                             };

    /// <summary>
    /// The cases of a close the peer never answers: the name of the case, the messages the peer sends and the close status.
    /// </summary>
    private static readonly Dictionary<string, (string[] Messages, WebSocketCloseStatus Status)> _unansweredCloses = new()
                                                                                                                     {
                                                                                                                         ["after tokenRefused"] = ([IngestClient.Hello("MARKERTOKEN-wrong")], WebSocketCloseStatus.NormalClosure),
                                                                                                                         ["after a 1008 in the message phase"] = ([IngestClient.Hello(Token), HelloAcceptedNotation], WebSocketCloseStatus.PolicyViolation)
                                                                                                                     };

    /// <summary>
    /// The disposables of the running test.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

    /// <summary>
    /// The clock of the sessions.
    /// </summary>
    private readonly RecordingTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The logger of the sessions.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    /// <summary>
    /// The connection counters the sessions share.
    /// </summary>
    private readonly IngestConnections _connections = new();

    /// <summary>
    /// The token that signals the stop of the server.
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestSessionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public IngestSessionTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the names of the refused catalog cases with the refusal kind and the refused version.
    /// </summary>
    private static IEnumerable<object?[]> CatalogRefusalRows
    {
        get
        {
            yield return new object?[] { "catalog version 2", ContractAcknowledgementRefusal.CatalogUnsupportedVersion, 2 };
            yield return new object?[] { "catalog malformed", ContractAcknowledgementRefusal.CatalogMalformed, null };
            yield return new object?[] { "catalog invalid", ContractAcknowledgementRefusal.CatalogInvalid, null };
            yield return new object?[] { "catalog too large", ContractAcknowledgementRefusal.CatalogTooLarge, null };
        }
    }

    /// <summary>
    /// Gets the names of the unreadable message cases.
    /// </summary>
    private static IEnumerable<object[]> UnreadableRows => _unreadableMessages.Keys.Select(label => new object[] { label });

    /// <summary>
    /// Gets the names of the unanswered close cases.
    /// </summary>
    private static IEnumerable<object[]> UnansweredCloseRows => _unansweredCloses.Keys.Select(label => new object[] { label });

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A version-1 hello with the stored token is answered helloAccepted with the server's version and no catalog identity,
    /// and the connection is logged.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IngestSessionRunAsyncWithStoredTokenRepliesHelloAccepted()
    {
        // Arrange
        var (_, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(Token));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        var accepted = Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello should be accepted");

        Assert.AreEqual(ServerVersion, accepted.ServerVersion, "The reply should carry the server's version");
        Assert.IsNull(accepted.CatalogIdentity, "The reply should carry no catalog identity");
        Assert.Contains("Companion connected.", LogMessages(), "The connection should be logged");
        Assert.IsFalse(_connections.TryTakeCompanion(), "The Companion's place should be taken");
    }

    /// <summary>
    /// A hello with a wrong token, one differing only in its last character, or none is refused as tokenRefused with version
    /// 1 and supported versions [1], closed with 1000 and an empty reason, and warned about without the token.
    /// </summary>
    /// <param name="presented">The presented token</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("MARKERTOKEN-wrong")]
    [DataRow(LastCharacterChanged)]
    [DataRow(null)]
    public async Task IngestSessionRunAsyncWithWrongTokenRepliesTokenRefused(string? presented)
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(presented));

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.HasCount(1, messages, "Exactly one reply should be sent");
        AssertHelloRefused(messages[0], ContractHelloRefusal.TokenRefused, 1);
        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, closeStatus, "The connection should be closed with 1000");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.Contains((LogLevel.Warning, "Companion refused: token refused."), LogEntries(), "The refusal should be warned about");
        AssertNoMarkerInLog();
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should stay free");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
    }

    /// <summary>
    /// After a refused token no later message of the connection is read: a correct hello sent right after it is not
    /// accepted.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestSessionRunAsyncAfterTokenRefusedReadsNoFurtherMessage()
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.SendAsync(client, IngestClient.Hello("MARKERTOKEN-wrong"));

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(Token));

        var (messages, closeStatus, _) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.HasCount(1, messages, "Only the refusal should be sent");
        AssertHelloRefused(messages[0], ContractHelloRefusal.TokenRefused, 1);
        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, closeStatus, "The connection should be closed with 1000");
        Assert.DoesNotContain("Companion connected.", LogMessages(), "The second hello should not be read");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should stay free");
    }

    /// <summary>
    /// No log line holds the token, the profile name, the Companion version or an island name, whether the hello is
    /// accepted or refused.
    /// </summary>
    /// <param name="presented">The presented token</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(Token)]
    [DataRow("MARKERTOKEN-wrong")]
    public async Task IngestSessionRunAsyncLogsNoTokenOrMessageContent(string presented)
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.SendAsync(client, IngestClient.Hello(presented));
        await IngestClient.SendAsync(client, IngestClient.Tick(Id1));

        // Act
        await FinishAsync(client, run);

        // Assert
        Assert.IsNotEmpty(_logger.Entries, "The session should log its connection or refusal");
        AssertNoMarkerInLog();
    }

    /// <summary>
    /// A first message of contract version 2 is refused as unsupportedVersion with version 2 and supported versions [1],
    /// with the right or a wrong token.
    /// </summary>
    /// <param name="presented">The presented token</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(Token)]
    [DataRow("MARKERTOKEN-wrong")]
    public async Task IngestSessionRunAsyncWithVersionTwoHelloRepliesUnsupportedVersion(string presented)
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(presented, 2));

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.HasCount(1, messages, "Exactly one reply should be sent");
        AssertHelloRefused(messages[0], ContractHelloRefusal.UnsupportedVersion, 2);
        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, closeStatus, "The connection should be closed with 1000");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.Contains((LogLevel.Warning, "Companion refused: unsupported contract version 2."), LogEntries(), "The refusal should be warned about");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should stay free");
    }

    /// <summary>
    /// While a Companion is connected a second valid hello is refused as companionConnected, and the first connection keeps
    /// working.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncWhileCompanionConnectedRepliesCompanionConnected()
    {
        // Arrange
        var (_, firstClient, _) = await StartAcceptedSessionAsync();
        var (secondRun, secondClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(secondClient, IngestClient.Hello(Token));

        var (messages, closeStatus, _) = await IngestClient.ReceiveUntilCloseAsync(secondClient);

        await FinishAsync(secondClient, secondRun);
        await IngestClient.SendAsync(firstClient, IngestClient.Tick(Id1));

        var (firstReply, _, _) = await IngestClient.ReceiveAsync(firstClient);

        // Assert
        Assert.HasCount(1, messages, "Exactly one reply should be sent to the second Companion");
        AssertHelloRefused(messages[0], ContractHelloRefusal.CompanionConnected, 1);
        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, closeStatus, "The second connection should be closed with 1000");
        Assert.Contains((LogLevel.Warning, "Companion refused: another Companion is connected."), LogEntries(), "The refusal should be warned about");
        AssertAcknowledgement(firstReply, Id1, null, null, []);
    }

    /// <summary>
    /// After the first Companion closes its connection, the next hello is accepted.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncAfterFirstClosesAcceptsNextHello()
    {
        // Arrange
        var (firstRun, firstClient, _) = await StartAcceptedSessionAsync();

        await FinishAsync(firstClient, firstRun);

        var (_, secondClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(secondClient, IngestClient.Hello(Token));

        var (reply, _, _) = await IngestClient.ReceiveAsync(secondClient);

        // Assert
        Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The next Companion should be accepted");
    }

    /// <summary>
    /// A connection lost without a close ends the session without an exception and releases the Companion's place.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncAfterConnectionLostReleasesCompanion()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        // Act
        client.Abort();
        await IngestClient.GuardAsync(run);

        // Assert
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
        Assert.Contains("Companion disconnected; 0 messages accepted, 0 refused.", LogMessages(), "The end should be logged with the counts");
    }

    /// <summary>
    /// A Companion that sends nothing for 120 seconds after its hello stays connected: the session has no idle timer of its
    /// own.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncIdlePastSixtySecondsStaysConnected()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(120));
        await IngestClient.SendAsync(client, IngestClient.Tick(Id1));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        AssertAcknowledgement(reply, Id1, null, null, []);
        Assert.IsFalse(run.IsCompleted, "The session should still run");
        Assert.IsFalse(_connections.TryTakeCompanion(), "The Companion's place should stay taken");
    }

    /// <summary>
    /// A message that arrives in parts over more than 60 seconds is read and acknowledged.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncMessageInPartsOverSixtySecondsStaysConnected()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        var tick = IngestClient.Tick(Id1);

        await IngestClient.SendPartAsync(client, tick[..40], false);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(70));
        await IngestClient.SendPartAsync(client, tick[40..], true);

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        AssertAcknowledgement(reply, Id1, null, null, []);
        Assert.IsFalse(run.IsCompleted, "The session should still run");
    }

    /// <summary>
    /// An accepted tick, session start, session end, icon image and catalog upload are acknowledged in order with their id.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task IngestSessionRunAsyncAcceptedMessagesAcknowledgedInOrder()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        // Act
        await IngestClient.SendAsync(client, IngestClient.Tick(Id1));
        await IngestClient.SendAsync(client, IngestClient.SessionStart(Id2));
        await IngestClient.SendAsync(client, IngestClient.SessionEnd(Id3));
        await IngestClient.SendAsync(client, IngestClient.IconImage(Id4));
        await IngestClient.SendAsync(client, IngestClient.CatalogUpload(Id5, Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[]}")));

        var replies = await ReceiveMessagesAsync(client, 5);

        // Assert
        AssertAcknowledgement(replies[0], Id1, null, null, []);
        AssertAcknowledgement(replies[1], Id2, null, null, []);
        AssertAcknowledgement(replies[2], Id3, null, null, []);
        AssertAcknowledgement(replies[3], Id4, null, null, []);
        AssertAcknowledgement(replies[4], Id5, null, null, []);
    }

    /// <summary>
    /// An accepted catalog upload is acknowledged with its distinct icon references, sorted ordinal.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task IngestSessionRunAsyncCatalogUploadListsSortedDistinctIcons()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        var document = IngestClient.Json("{'schemaVersion':1,'gameVersion':null,'entries':["
                                         + $"{{'guid':1,'kind':'good','names':{{}},'regionGuid':null,'regionTypes':null,'icon':'{IconB}'}},"
                                         + $"{{'guid':2,'kind':'good','names':{{}},'regionGuid':null,'regionTypes':null,'icon':'{IconA}'}},"
                                         + $"{{'guid':3,'kind':'good','names':{{}},'regionGuid':null,'regionTypes':null,'icon':'{IconB}'}},"
                                         + "{'guid':4,'kind':'good','names':{},'regionGuid':null,'regionTypes':null,'icon':null}]}");

        // Act
        await IngestClient.SendAsync(client, IngestClient.CatalogUpload(Id1, Encoding.UTF8.GetBytes(document)));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        AssertAcknowledgement(reply, Id1, null, null, [IconA, IconB]);
    }

    /// <summary>
    /// A status message gets no reply: the next reply is the acknowledgement of the message after it.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task IngestSessionRunAsyncStatusGetsNoReply()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        // Act
        await IngestClient.SendAsync(client, IngestClient.Status());
        await IngestClient.SendAsync(client, IngestClient.Tick(Id1));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        AssertAcknowledgement(reply, Id1, null, null, []);
    }

    /// <summary>
    /// At the close the counts of accepted and refused messages are logged.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task IngestSessionRunAsyncOnCloseLogsCounts()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        await IngestClient.SendAsync(client, IngestClient.Tick(Id1));
        await IngestClient.SendAsync(client, IngestClient.Status());
        await IngestClient.SendAsync(client, InvalidTick(Id2));
        await ReceiveMessagesAsync(client, 2);

        // Act
        await FinishAsync(client, run);

        // Assert
        Assert.Contains("Companion disconnected; 2 messages accepted, 1 refused.", LogMessages(), "The end should be logged with the counts");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
    }

    /// <summary>
    /// A message refused as invalid with a valid id is acknowledged with the refusal, and the connection stays open.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task IngestSessionRunAsyncInvalidMessageWithIdAcknowledgesRefusal()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        // Act
        await IngestClient.SendAsync(client, InvalidTick(Id1));
        await IngestClient.SendAsync(client, IngestClient.Tick(Id2));

        var replies = await ReceiveMessagesAsync(client, 2);

        // Assert
        AssertAcknowledgement(replies[0], Id1, ContractAcknowledgementRefusal.Invalid, null, []);
        AssertAcknowledgement(replies[1], Id2, null, null, []);
    }

    /// <summary>
    /// A catalog upload whose catalog is refused is acknowledged with the catalog kind, the refused schema version for an
    /// unsupported version, and the connection stays open.
    /// </summary>
    /// <param name="label">The name of the refused catalog</param>
    /// <param name="refusal">The expected refusal</param>
    /// <param name="refusedVersion">The expected refused version</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DynamicData(nameof(CatalogRefusalRows))]
    public async Task IngestSessionRunAsyncCatalogRefusalAcknowledgesCatalogKind(string label, ContractAcknowledgementRefusal refusal, int? refusedVersion)
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        // Act
        await IngestClient.SendAsync(client, IngestClient.CatalogUpload(Id1, _refusedCatalogs[label]()));
        await IngestClient.SendAsync(client, IngestClient.Tick(Id2));

        var replies = await ReceiveMessagesAsync(client, 2);

        // Assert
        AssertAcknowledgement(replies[0], Id1, refusal, refusedVersion, []);
        AssertAcknowledgement(replies[1], Id2, null, null, []);
    }

    /// <summary>
    /// After the hello, a message whose id cannot be used, a server-to-Companion type or a second hello closes the
    /// connection with 1008 without a reply, and is counted as refused.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DynamicData(nameof(UnreadableRows))]
    public async Task IngestSessionRunAsyncUnreadableMessageClosesConnection(string label)
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        // Act
        await IngestClient.SendAsync(client, _unreadableMessages[label]);

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, $"No reply should be sent: {label}");
        Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, closeStatus, $"The connection should be closed with 1008: {label}");
        Assert.AreEqual(string.Empty, closeDescription, $"The close should carry an empty reason: {label}");
        Assert.Contains("Companion disconnected; 0 messages accepted, 1 refused.", LogMessages(), $"The closing message should be counted as refused: {label}");
        Assert.IsTrue(_connections.TryTakeCompanion(), $"The Companion's place should be released: {label}");
    }

    /// <summary>
    /// A binary frame, as the first message or after the hello, closes the connection with 1003 without a reply.
    /// </summary>
    /// <param name="hellos">The number of hellos sent before the binary frame (0 or 1)</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(0)]
    [DataRow(1)]
    public async Task IngestSessionRunAsyncBinaryFrameCloses1003(int hellos)
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        await SendRepeatedAsync(client, IngestClient.Hello(Token), hellos);

        // Act
        await client.SendAsync(new byte[] { 1, 2, 3 }, WebSocketMessageType.Binary, true, CancellationToken.None);

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.HasCount(hellos, messages, "Only the hello should be answered");
        Assert.AreEqual(WebSocketCloseStatus.InvalidMessageType, closeStatus, "The connection should be closed with 1003");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be free");
    }

    /// <summary>
    /// When the server stops, a connected Companion's connection is closed with 1001.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task IngestSessionRunAsyncOnStoppingCloses1001()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        // Act
        await _stopping.CancelAsync();

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "No reply should be sent");
        Assert.AreEqual(WebSocketCloseStatus.EndpointUnavailable, closeStatus, "The connection should be closed with 1001");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
    }

    /// <summary>
    /// A message that arrives after the server's stop closed the connection with 1001 is not answered: a hello while the
    /// hello phase is open, or a tick after an accepted hello.
    /// </summary>
    /// <param name="hellos">The number of hellos accepted before the stop (0 or 1)</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(0)]
    [DataRow(1)]
    public async Task IngestSessionRunAsyncMessageAfterStopIsNotAnswered(int hellos)
    {
        // Arrange
        var (run, client, _) = hellos == 0 ? StartSession(IngestAccess.CreateForToken(Token)) : await StartAcceptedSessionAsync();

        await _stopping.CancelAsync();

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        // Act
        await IngestClient.SendAsync(client, hellos == 0 ? IngestClient.Hello(Token) : IngestClient.Tick(Id1));
        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, $"No reply should be sent: {hellos} hellos");
        Assert.AreEqual(WebSocketCloseStatus.EndpointUnavailable, closeStatus, $"The connection should be closed with 1001: {hellos} hellos");
        Assert.AreEqual(string.Empty, closeDescription, $"The close should carry an empty reason: {hellos} hellos");
        Assert.AreEqual(hellos, LogMessages().Count(message => message == "Companion connected."), $"Only the hello before the stop should be logged as connected: {hellos} hellos");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), $"The pending place should be left: {hellos} hellos");
        Assert.IsTrue(_connections.TryTakeCompanion(), $"The Companion's place should be free: {hellos} hellos");
    }

    /// <summary>
    /// A reply the writer refuses (a <c>helloAccepted</c> with an empty server version) is never sent: the connection is
    /// aborted, both places are freed and the log holds no token or message content.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task IngestSessionRunAsyncUnwritableReplyAbortsWithoutReply()
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token), string.Empty);

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(Token));
        await IngestClient.GuardAsync(run);

        // Assert
        await Assert.ThrowsAsync<WebSocketException>(() => IngestClient.ReceiveAsync(client), "The connection should be aborted without a reply");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
        Assert.DoesNotContain(entry => entry.Level > LogLevel.Information, _logger.Entries.ToList(), "No warning or error should be logged");
        AssertNoMarkerInLog();
    }

    /// <summary>
    /// A peer that never answers the server's close and keeps sending frames is cut off once 2 seconds pass on the clock.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DynamicData(nameof(UnansweredCloseRows))]
    public async Task IngestSessionRunAsyncPeerNeverAnswersCloseEndsAfterCloseWait(string label)
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));
        var (sentMessages, expectedStatus) = _unansweredCloses[label];

        await SendAllAsync(client, sentMessages);

        var (_, closeStatus, _) = await IngestClient.ReceiveUntilCloseAsync(client);

        await IngestClient.SendSpacesAsync(client, 10, 3);
        await IngestClient.GuardAsync(_timeProvider.WaitForDelayAsync(_closeWait, TestContext.CancellationToken));

        // Act
        _timeProvider.Advance(_closeWait);
        await IngestClient.GuardAsync(run);

        // Assert
        Assert.AreEqual(expectedStatus, closeStatus, $"The connection should be closed with the status of its case: {label}");
        Assert.IsTrue(_connections.TryTakeCompanion(), $"The Companion's place should be free: {label}");
    }

    /// <summary>
    /// A first message over 32 KiB is closed with 1009 without waiting for its end and without reading past the bound.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestSessionRunAsyncOversizedHelloCloses1009()
    {
        // Arrange
        var (run, client, serverStream) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendSpacesAsync(client, HelloBound + 1, 1);

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        var bytesRead = serverStream.BytesRead;

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "No reply should be sent");
        Assert.AreEqual(WebSocketCloseStatus.MessageTooBig, closeStatus, "The connection should be closed with 1009");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.IsLessThanOrEqualTo(HelloBound + 1 + ReadSlack, bytesRead, "The server should not read past the bound");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
    }

    /// <summary>
    /// A later message over 25,165,824 bytes is closed with 1009 without waiting for its end and without reading past the
    /// bound.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestSessionRunAsyncOversizedMessageReadsNoFurther()
    {
        // Arrange
        var (run, client, serverStream) = await StartAcceptedSessionAsync();

        var before = serverStream.BytesRead;

        // Act
        await IngestClient.SendSpacesAsync(client, 1048576, MessageBound / 1048576);
        await IngestClient.SendSpacesAsync(client, 1, 1);

        var (messages, closeStatus, _) = await IngestClient.ReceiveUntilCloseAsync(client);

        var bytesRead = serverStream.BytesRead - before;

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "No reply should be sent");
        Assert.AreEqual(WebSocketCloseStatus.MessageTooBig, closeStatus, "The connection should be closed with 1009");
        Assert.IsLessThanOrEqualTo(MessageBound + 1 + ReadSlack, bytesRead, "The server should not read past the bound");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
    }

    /// <summary>
    /// A hello not sent within 10 seconds of the start closes the connection with 1008 without a reply or log line.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestSessionRunAsyncHelloAfterTenSecondsCloses1008()
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.GuardAsync(_timeProvider.WaitForDelayAsync(_helloTimeout, TestContext.CancellationToken));

        // Act
        _timeProvider.Advance(_helloTimeout);

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "No reply should be sent");
        Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, closeStatus, "The connection should be closed with 1008");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.IsEmpty(_logger.Entries, "The late hello should not be logged");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
    }

    /// <summary>
    /// A hello sent in parts that started in time but is not complete within 10 seconds of the start closes the connection
    /// with 1008.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestSessionRunAsyncTrickledHelloPastDeadlineCloses1008()
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        var hello = IngestClient.Hello(Token);

        await IngestClient.GuardAsync(_timeProvider.WaitForDelayAsync(_helloTimeout, TestContext.CancellationToken));
        await IngestClient.SendPartAsync(client, hello[..20], false);
        _timeProvider.Advance(TimeSpan.FromSeconds(9));
        await IngestClient.SendPartAsync(client, hello[20..40], false);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        var (messages, closeStatus, _) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "No reply should be sent");
        Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, closeStatus, "The connection should be closed with 1008");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
    }

    /// <summary>
    /// A hello that started in time but is completed only after the 10 seconds ran out and the 1008 close went out is not
    /// answered.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task IngestSessionRunAsyncHelloCompletedAfterDeadlineIsNotAnswered()
    {
        // Arrange
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        var hello = IngestClient.Hello(Token);

        await IngestClient.GuardAsync(_timeProvider.WaitForDelayAsync(_helloTimeout, TestContext.CancellationToken));
        await IngestClient.SendPartAsync(client, hello[..20], false);
        _timeProvider.Advance(_helloTimeout);

        // Act
        await IngestClient.SendPartAsync(client, hello[20..], true);

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "The late hello should not be answered");
        Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, closeStatus, "The connection should be closed with 1008");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.IsEmpty(_logger.Entries, "The late hello should not be logged");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should stay free");
    }

    /// <summary>
    /// After the hello, a message within the byte bound that the reader refuses as too large (an island name of 766 bytes)
    /// closes the connection with 1009 without a reply, and is counted as refused.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [TestCategory("AC11")]
    public async Task IngestSessionRunAsyncMessageOverReaderBoundCloses1009()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        var tick = IngestClient.Tick(Id1).Replace("'MARKERISLAND'", $"'{new string('a', 766)}'", StringComparison.Ordinal);

        // Act
        await IngestClient.SendAsync(client, tick);

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "No reply should be sent");
        Assert.AreEqual(WebSocketCloseStatus.MessageTooBig, closeStatus, "The connection should be closed with 1009");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.Contains("Companion disconnected; 0 messages accepted, 1 refused.", LogMessages(), "The message should be counted as refused");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
    }

    /// <summary>
    /// Builds a tick that breaks a value rule (a receive time without fraction) but has a valid id.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <returns>The tick in single-quote notation</returns>
    private static string InvalidTick(string id)
    {
        return IngestClient.Tick(id).Replace(IngestClient.Time, "2026-11-12T10:20:30Z", StringComparison.Ordinal);
    }

    /// <summary>
    /// Asserts that a reply is a refused hello of a reason with the received version and supported versions [1].
    /// </summary>
    /// <param name="message">The reply</param>
    /// <param name="reason">The expected reason</param>
    /// <param name="receivedVersion">The expected received version</param>
    private static void AssertHelloRefused(ContractMessage message, ContractHelloRefusal reason, int receivedVersion)
    {
        var refused = Assert.IsInstanceOfType<ContractHelloRefused>(message, "The reply should be a refused hello");

        Assert.AreEqual(reason, refused.Reason, "The refusal should carry its reason");
        Assert.AreEqual(receivedVersion, refused.ReceivedVersion, "The refusal should carry the received version");
        Assert.AreSequenceEqual(_supportedVersions, refused.SupportedVersions.ToArray(), "The refusal should name version 1 as the only supported one");
    }

    /// <summary>
    /// Asserts that a reply is an acknowledgement of an id with a refusal, a refused version and missing icons.
    /// </summary>
    /// <param name="message">The reply</param>
    /// <param name="id">The expected identity</param>
    /// <param name="refusal">The expected refusal</param>
    /// <param name="refusedVersion">The expected refused version</param>
    /// <param name="missingIcons">The expected missing icons</param>
    private static void AssertAcknowledgement(ContractMessage? message, string id, ContractAcknowledgementRefusal? refusal, int? refusedVersion, string[] missingIcons)
    {
        var acknowledgement = Assert.IsInstanceOfType<ContractAcknowledgement>(message, $"The reply should be an acknowledgement of {id}");

        Assert.AreEqual(Guid.Parse(id), acknowledgement.Id, $"The acknowledgement should name the message's id {id}");
        Assert.AreEqual(refusal, acknowledgement.Refusal, $"The acknowledgement should carry the refusal of {id}");
        Assert.AreEqual(refusedVersion, acknowledgement.RefusedVersion, $"The acknowledgement should carry the refused version of {id}");
        Assert.AreSequenceEqual(missingIcons, acknowledgement.MissingIcons.ToArray(), $"The acknowledgement should list the missing icons of {id}");
    }

    /// <summary>
    /// Sends the messages in order.
    /// </summary>
    /// <param name="client">The client socket</param>
    /// <param name="messages">The messages in single-quote notation</param>
    /// <returns>The task</returns>
    private static async Task SendAllAsync(WebSocket client, IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            await IngestClient.SendAsync(client, message).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a message a number of times.
    /// </summary>
    /// <param name="client">The client socket</param>
    /// <param name="message">The message in single-quote notation</param>
    /// <param name="count">The number of times</param>
    /// <returns>The task</returns>
    private static Task SendRepeatedAsync(WebSocket client, string message, int count)
    {
        return SendAllAsync(client, Enumerable.Repeat(message, count));
    }

    /// <summary>
    /// Receives a number of messages.
    /// </summary>
    /// <param name="client">The client socket</param>
    /// <param name="count">The number of messages</param>
    /// <returns>The messages</returns>
    private static async Task<List<ContractMessage?>> ReceiveMessagesAsync(WebSocket client, int count)
    {
        var messages = new List<ContractMessage?>();

        for (var index = 0; index < count; index++)
        {
            var (message, _, _) = await IngestClient.ReceiveAsync(client).ConfigureAwait(false);

            messages.Add(message);
        }

        return messages;
    }

    /// <summary>
    /// Closes the connection from the client and waits for the session to end.
    /// </summary>
    /// <param name="client">The client socket</param>
    /// <param name="run">The session's run</param>
    /// <returns>The task</returns>
    private static async Task FinishAsync(WebSocket client, Task run)
    {
        await IngestClient.CloseAsync(client).ConfigureAwait(false);
        await IngestClient.GuardAsync(run).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a session on a new connection, taking its pending place first as the endpoint does.
    /// </summary>
    /// <param name="access">The access of the session</param>
    /// <returns>The session's run, the client socket and the server's end of the connection</returns>
    private (Task Run, WebSocket Client, InMemoryDuplexStream ServerStream) StartSession(IngestAccess access)
    {
        return StartSession(access, ServerVersion);
    }

    /// <summary>
    /// Starts a session with a server version on a new connection, taking its pending place first as the endpoint does.
    /// </summary>
    /// <param name="access">The access of the session</param>
    /// <param name="serverVersion">The version of the server the session reports</param>
    /// <returns>The session's run, the client socket and the server's end of the connection</returns>
    private (Task Run, WebSocket Client, InMemoryDuplexStream ServerStream) StartSession(IngestAccess access, string serverVersion)
    {
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();

        var server = WebSocket.CreateFromStream(serverStream,
                                                new WebSocketCreationOptions
                                                {
                                                    IsServer = true,
                                                    KeepAliveInterval = TimeSpan.Zero
                                                });
        var client = WebSocket.CreateFromStream(clientStream,
                                                new WebSocketCreationOptions
                                                {
                                                    IsServer = false,
                                                    KeepAliveInterval = TimeSpan.Zero
                                                });

        _disposables.AddRange([client, server, clientStream, serverStream]);
        _connections.TryEnterPendingHello();

        var database = ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, _stopping.Token).GetAwaiter().GetResult().Database!;

        _disposables.Add(database);

        var session = new IngestSession(server, access, _connections, new ServerState(new ServerStore(database), _logger), _timeProvider, _logger, serverVersion);

        return (session.RunAsync(_stopping.Token), client, serverStream);
    }

    /// <summary>
    /// Starts a session and has its hello with the stored token accepted.
    /// </summary>
    /// <returns>The session's run, the client socket and the server's end of the connection</returns>
    private async Task<(Task Run, WebSocket Client, InMemoryDuplexStream ServerStream)> StartAcceptedSessionAsync()
    {
        var session = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.SendAsync(session.Client, IngestClient.Hello(Token)).ConfigureAwait(false);

        var (reply, _, _) = await IngestClient.ReceiveAsync(session.Client).ConfigureAwait(false);

        Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello with the stored token should be accepted");

        return session;
    }

    /// <summary>
    /// Counts the free pending places by taking them all.
    /// </summary>
    /// <returns>The number of free places</returns>
    private int CountFreePendingPlaces()
    {
        var count = 0;

        while (count <= IngestConnections.MaxPendingHellos && _connections.TryEnterPendingHello())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Returns the messages logged so far.
    /// </summary>
    /// <returns>The messages</returns>
    private List<string> LogMessages()
    {
        return [.. _logger.Entries.Select(entry => entry.Message)];
    }

    /// <summary>
    /// Returns the levels and messages logged so far.
    /// </summary>
    /// <returns>The levels and messages</returns>
    private List<(LogLevel Level, string Message)> LogEntries()
    {
        return [.. _logger.Entries.Select(entry => (entry.Level, entry.Message))];
    }

    /// <summary>
    /// Asserts that no log message or attached exception holds a marker of the token or the message content.
    /// </summary>
    private void AssertNoMarkerInLog()
    {
        Assert.DoesNotContain(text => text.Contains("MARKER", StringComparison.Ordinal), _logger.Entries.Select(entry => $"{entry.Message} {entry.Exception}").ToList(), "No log line should hold the token or message content");
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Cancel();

        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _stopping.Dispose();
    }

    #endregion // IDisposable
}