using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

using Censio.Contracts;
using Censio.Pipe.Tests;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;
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
    /// The identity of the sixth message.
    /// </summary>
    private const string Id6 = "11111111-2222-4333-8444-555555555556";

    /// <summary>
    /// A profile identity other than the one of <see cref="IngestClient.Hello" />.
    /// </summary>
    private const string OtherProfile = "bbbbbbbb-cccc-4ddd-8eee-ffffffffffff";

    /// <summary>
    /// The profile name of <see cref="IngestClient.Hello" />.
    /// </summary>
    private const string HelloProfileName = "MARKERPROFILE";

    /// <summary>
    /// The error line of a message that cannot be stored (spec E3).
    /// </summary>
    private const string StoreFailureLine = "A message could not be stored; the connection is closed.";

    /// <summary>
    /// The trigger that makes every insert of a tick fail.
    /// </summary>
    private const string FailTicksSql = "CREATE TRIGGER \"FailTicks\" BEFORE INSERT ON \"Ticks\" BEGIN SELECT RAISE(ABORT, 'test write failure'); END;";

    /// <summary>
    /// The trigger that makes every insert of a profile fail.
    /// </summary>
    private const string FailProfilesSql = "CREATE TRIGGER \"FailProfiles\" BEFORE INSERT ON \"Profiles\" BEGIN SELECT RAISE(ABORT, 'test write failure'); END;";

    /// <summary>
    /// The statement that removes the trigger of <see cref="FailProfilesSql" />.
    /// </summary>
    private const string DropFailProfilesSql = "DROP TRIGGER \"FailProfiles\";";

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
    /// The time the test gives a held write lock to let an acknowledgement through that a store-first session never sends;
    /// a correct session passes whatever its length.
    /// </summary>
    private static readonly TimeSpan _lockedWindow = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The longest the test waits for a follower.
    /// </summary>
    private static readonly TimeSpan _followerBound = TimeSpan.FromSeconds(10);

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

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The logger of the server state, apart from the sessions' logger.
    /// </summary>
    private readonly ConcurrentRecordingLogger _stateLogger = new();

    /// <summary>
    /// The database file of the running test, or <see langword="null" /> while the state is in memory.
    /// </summary>
    private string? _databaseFile;

    /// <summary>
    /// The store under the server state of the sessions.
    /// </summary>
    private ServerStore _store = null!;

    /// <summary>
    /// The server state every session of the test stores through.
    /// </summary>
    private ServerState _state = null!;

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
    /// Opens the in-memory database and the restored state the sessions of the test share.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    [TestInitialize]
    public Task InitializeAsync()
    {
        return OpenStateAsync(null);
    }

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
    /// A tick is acknowledged only after it is stored: while another connection holds the write lock no acknowledgement
    /// arrives, and once the lock is released the acknowledgement arrives and the tick reads back.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestSessionRunAsyncAcknowledgesTickOnlyWhenStored()
    {
        // Arrange
        await UseDatabaseFileAsync();

        var (_, client, _) = await StartAcceptedSessionAsync();

        Task<(ContractMessage? Message, WebSocketCloseStatus? CloseStatus, string? CloseDescription)> receive;
        Task first;

        // Act
        using (HoldWriteLock())
        {
            await IngestClient.SendAsync(client, IngestClient.Tick(Id1));
            receive = IngestClient.ReceiveAsync(client);
            first = await Task.WhenAny(receive, Task.Delay(_lockedWindow, TestContext.CancellationToken));
        }

        var (reply, _, _) = await receive;

        // Assert
        Assert.AreNotSame(receive, first, "No acknowledgement should arrive while the tick cannot be stored");
        AssertAcknowledgement(reply, Id1, null, null, []);
        Assert.IsNotNull(await _store.ReadTickAsync(Guid.Parse(Id1), TestContext.CancellationToken), "The acknowledged tick should be stored");
    }

    /// <summary>
    /// A resent tick (same id), icon (same reference) and catalog (same document) are acknowledged without a refusal, the
    /// catalog with the reference still without an icon, and no follower is told of them.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncResentMessagesAcknowledgedWithoutChange()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();
        var (stored, storedIcon) = IconMessage(Id2, "png stored");
        var (missing, _) = IconMessage(Id6, "png missing");
        var (_, resentIcon) = IconMessage(Id4, "png stored");

        var document = CatalogJson(stored, missing);

        await SendAllAsync(client, [IngestClient.Tick(Id1), storedIcon, IngestClient.CatalogUpload(Id3, document)]);
        await ReceiveMessagesAsync(client, 3);

        var (notices, told) = Follow(1);

        // Act
        await SendAllAsync(client, [IngestClient.Tick(Id1), resentIcon, IngestClient.CatalogUpload(Id5, document), TickOfOtherProfile(Id6)]);

        var replies = await ReceiveMessagesAsync(client, 4);

        // Assert
        await told.WaitAsync(_followerBound, TestContext.CancellationToken);

        AssertAcknowledgement(replies[0], Id1, null, null, []);
        AssertAcknowledgement(replies[1], Id4, null, null, []);
        AssertAcknowledgement(replies[2], Id5, null, null, [missing]);
        AssertAcknowledgement(replies[3], Id6, null, null, []);
        Assert.AreSequenceEqual(new[] { $"Profile {OtherProfile}" }, notices.ToList(), "No follower should be told of the resends, only of the later tick");
    }

    /// <summary>
    /// Session events and status messages store nothing: the hello's profile stays listed as it was and gets no tick, no
    /// follower is told of a profile change for them, and the status tells the followers only of the Companion's new pipe
    /// state.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestSessionRunAsyncSessionEventsAndStatusStoreNothing()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();
        var (notices, told) = Follow(2);

        // Act
        await SendAllAsync(client, [IngestClient.SessionStart(Id1), IngestClient.Status(), IngestClient.SessionEnd(Id2), TickOfOtherProfile(Id3)]);
        await ReceiveMessagesAsync(client, 3);

        // Assert
        await told.WaitAsync(_followerBound, TestContext.CancellationToken);

        var helloProfile = Guid.Parse(IngestClient.Profile);

        Assert.AreSequenceEqual(new[] { $"{helloProfile} {HelloProfileName}" }, DescribeProfiles(await _store.ListProfilesAsync(TestContext.CancellationToken), helloProfile), "The hello's profile should stay listed as the hello stored it");
        Assert.IsNull(await _store.ReadLatestTickAsync(helloProfile, TestContext.CancellationToken), "The session events should store no tick");
        Assert.AreSequenceEqual(new[] { Guid.Parse(OtherProfile) }, (await _store.ListLatestProfilesAsync(10, TestContext.CancellationToken)).ToList(), "Only the later tick's profile should have a tick");
        Assert.AreSequenceEqual(new[] { "Companion <no profile>", $"Profile {OtherProfile}" }, notices.Order(StringComparer.Ordinal).ToList(), "The followers should be told only of the status's pipe state and of the later tick, not of a profile change for the session events or the status");
    }

    /// <summary>
    /// A tick refused invalid and a catalog upload refused catalogInvalid are acknowledged with their refusal and store
    /// nothing: no tick, no catalog, no change of the complete catalog and no follower told.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IngestSessionRunAsyncRefusedMessagesStoreNothing()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        var completeBefore = _state.CompleteCatalogIdentity;
        var (notices, told) = Follow(1);

        // Act
        await SendAllAsync(client, [InvalidTick(Id1), IngestClient.CatalogUpload(Id2, _refusedCatalogs["catalog invalid"]()), TickOfOtherProfile(Id3)]);

        var replies = await ReceiveMessagesAsync(client, 3);

        // Assert
        await told.WaitAsync(_followerBound, TestContext.CancellationToken);

        AssertAcknowledgement(replies[0], Id1, ContractAcknowledgementRefusal.Invalid, null, []);
        AssertAcknowledgement(replies[1], Id2, ContractAcknowledgementRefusal.CatalogInvalid, null, []);
        AssertAcknowledgement(replies[2], Id3, null, null, []);
        Assert.IsNull(await _store.ReadTickAsync(Guid.Parse(Id1), TestContext.CancellationToken), "The refused tick should not be stored");
        Assert.IsNull(await _store.ReadCatalogAsync(TestContext.CancellationToken), "The refused catalog should not be stored");
        Assert.AreEqual(completeBefore, _state.CompleteCatalogIdentity, "The complete catalog should not change");
        Assert.AreSequenceEqual(new[] { $"Profile {OtherProfile}" }, notices.ToList(), "No follower should be told of the refused messages, only of the later tick");
    }

    /// <summary>
    /// An accepted hello stores its profile with its name before helloAccepted is sent.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IngestSessionRunAsyncHelloStoresProfileBeforeHelloAccepted()
    {
        // Arrange
        var (_, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        var expected = new[] { $"{IngestClient.Profile} {HelloProfileName}" };

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(Token));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        var stored = DescribeProfiles(await _store.ListProfilesAsync(TestContext.CancellationToken), null);

        // Assert
        Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello should be accepted");
        Assert.AreSequenceEqual(expected, stored, "The profile should be stored with its name when helloAccepted arrives");
        Assert.AreSequenceEqual(expected, DescribeProfiles(_state.Profiles, null), "The state should list the profile with its name");
    }

    /// <summary>
    /// An accepted hello with a known profile identity and another name renames the profile.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IngestSessionRunAsyncHelloWithKnownProfileRenamesIt()
    {
        // Arrange
        await _state.StoreProfileAsync(Guid.Parse(IngestClient.Profile), "Rome", TestContext.CancellationToken);

        var (_, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(client, HelloAs(IngestClient.Profile, "Roma"));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello should be accepted");
        Assert.AreSequenceEqual(new[] { $"{IngestClient.Profile} Roma" }, DescribeProfiles(await _store.ListProfilesAsync(TestContext.CancellationToken), null), "The known profile should take the hello's name");
    }

    /// <summary>
    /// A hello refused companionConnected, with a valid token, the connected profile's identity and another name, renames
    /// nothing.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IngestSessionRunAsyncCompanionConnectedHelloRenamesNothing()
    {
        // Arrange
        await StartAcceptedSessionAsync();

        var (secondRun, secondClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(secondClient, HelloAs(IngestClient.Profile, "Another name"));

        var (messages, _, _) = await IngestClient.ReceiveUntilCloseAsync(secondClient);

        await FinishAsync(secondClient, secondRun);

        // Assert
        AssertHelloRefused(messages[0], ContractHelloRefusal.CompanionConnected, 1);
        Assert.AreSequenceEqual(new[] { $"{IngestClient.Profile} {HelloProfileName}" }, DescribeProfiles(await _store.ListProfilesAsync(TestContext.CancellationToken), null), "The refused hello should not rename the connected profile");
    }

    /// <summary>
    /// A hello refused tokenRefused and a hello refused unsupportedVersion, the latter with the stored token, store no
    /// profile.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IngestSessionRunAsyncRefusedHelloStoresNothing()
    {
        // Arrange
        var (tokenRun, tokenClient, _) = StartSession(IngestAccess.CreateForToken(Token));
        var (versionRun, versionClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(tokenClient, IngestClient.Hello(LastCharacterChanged));

        var (tokenMessages, _, _) = await IngestClient.ReceiveUntilCloseAsync(tokenClient);

        await FinishAsync(tokenClient, tokenRun);
        await IngestClient.SendAsync(versionClient, IngestClient.Hello(Token, 2));

        var (versionMessages, _, _) = await IngestClient.ReceiveUntilCloseAsync(versionClient);

        await FinishAsync(versionClient, versionRun);

        // Assert
        AssertHelloRefused(tokenMessages[0], ContractHelloRefusal.TokenRefused, 1);
        AssertHelloRefused(versionMessages[0], ContractHelloRefusal.UnsupportedVersion, 2);
        Assert.IsEmpty(await _store.ListProfilesAsync(TestContext.CancellationToken), "No refused hello should store a profile");
        Assert.IsEmpty(_state.Profiles, "No refused hello should add a profile to the state");
    }

    /// <summary>
    /// helloAccepted names the stored catalog only once every icon it refers to is stored: not before a catalog, not
    /// after a catalog with one of its two icons on the next connection, whose re-upload lists the missing reference, and
    /// on the connection after both icons are stored.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncHelloNamesCatalogOnlyWhenAllIconsStored()
    {
        // Arrange
        var (firstReference, firstIcon) = IconMessage(Id2, "png first");
        var (secondReference, secondIcon) = IconMessage(Id4, "png second");

        var document = CatalogJson(firstReference, secondReference);
        var (beforeCatalog, firstReplies) = await RunAcceptedSessionAsync([IngestClient.CatalogUpload(Id1, document), firstIcon]);
        var (afterOneIcon, secondReplies) = await RunAcceptedSessionAsync([IngestClient.CatalogUpload(Id3, document), secondIcon]);
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(Token));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsNull(beforeCatalog.CatalogIdentity, "No catalog should be named before one is stored");
        AssertAcknowledgement(firstReplies[0], Id1, null, null, new[] { firstReference, secondReference }.Order(StringComparer.Ordinal).ToArray());
        Assert.IsNull(afterOneIcon.CatalogIdentity, "No catalog should be named while one of its icons is missing");
        AssertAcknowledgement(secondReplies[0], Id3, null, null, [secondReference]);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(document)), Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello should be accepted").CatalogIdentity, "The catalog should be named once all its icons are stored");
    }

    /// <summary>
    /// A catalog upload's acknowledgement lists only the references without a stored icon, distinct and ordinally sorted.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IngestSessionRunAsyncCatalogUploadListsOnlyIconsNotStored()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();
        var (stored, storedIcon) = IconMessage(Id1, "png stored");

        await IngestClient.SendAsync(client, storedIcon);
        await IngestClient.ReceiveAsync(client);

        // Act
        await IngestClient.SendAsync(client, IngestClient.CatalogUpload(Id2, CatalogJson(IconB, stored, IconA, IconB)));

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        AssertAcknowledgement(reply, Id2, null, null, [IconA, IconB]);
    }

    /// <summary>
    /// A tick whose storing fails is not acknowledged: the connection is closed with 1011 and an empty reason, the error is
    /// logged with its exception, the tick counts as refused and the Companion's place is freed.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestSessionRunAsyncTickStoreFailureCloses1011WithoutAcknowledgement()
    {
        // Arrange
        await UseDatabaseFileAsync();

        var (run, client, _) = await StartAcceptedSessionAsync();

        ExecuteOnFile(FailTicksSql);

        // Act
        await IngestClient.SendAsync(client, IngestClient.Tick(Id1));

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(client);

        await FinishAsync(client, run);

        // Assert
        Assert.IsEmpty(messages, "The tick should not be acknowledged");
        Assert.AreEqual(WebSocketCloseStatus.InternalServerError, closeStatus, "The connection should be closed with 1011");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        AssertOneStoreFailureLogged();
        Assert.Contains("Companion disconnected; 0 messages accepted, 1 refused.", LogMessages(), "The tick should be counted as refused");
        Assert.IsNull(await _store.ReadTickAsync(Guid.Parse(Id1), TestContext.CancellationToken), "The tick should not be stored");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
    }

    /// <summary>
    /// A hello whose profile cannot be stored gets no reply, is closed with 1011, logs only the error and frees the place
    /// once: a second Companion is accepted while the failed connection waits for its close, and after the failed session
    /// ends a third is still refused companionConnected.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestSessionRunAsyncHelloProfileStoreFailureCloses1011AndFreesPlace()
    {
        // Arrange
        await UseDatabaseFileAsync();
        ExecuteOnFile(FailProfilesSql);

        var (failedRun, failedClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.SendAsync(failedClient, IngestClient.Hello(Token));

        var (messages, closeStatus, closeDescription) = await IngestClient.ReceiveUntilCloseAsync(failedClient);

        ExecuteOnFile(DropFailProfilesSql);

        var (_, secondClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.SendAsync(secondClient, IngestClient.Hello(Token));

        var (secondReply, _, _) = await IngestClient.ReceiveAsync(secondClient);

        await FinishAsync(failedClient, failedRun);

        var (thirdRun, thirdClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(thirdClient, IngestClient.Hello(Token));

        var (thirdMessages, _, _) = await IngestClient.ReceiveUntilCloseAsync(thirdClient);

        await FinishAsync(thirdClient, thirdRun);

        // Assert
        Assert.IsEmpty(messages, "The hello whose profile cannot be stored should get no reply");
        Assert.AreEqual(WebSocketCloseStatus.InternalServerError, closeStatus, "The connection should be closed with 1011");
        Assert.AreEqual(string.Empty, closeDescription, "The close should carry an empty reason");
        Assert.IsInstanceOfType<ContractHelloAccepted>(secondReply, "A second Companion should be accepted while the failed connection waits for its close");
        AssertHelloRefused(thirdMessages[0], ContractHelloRefusal.CompanionConnected, 1);
        AssertOneStoreFailureLogged();
        Assert.ContainsSingle(message => message == "Companion connected.", LogMessages(), "Only the second Companion should be logged as connected");
        Assert.DoesNotContain(message => message.StartsWith("Companion disconnected", StringComparison.Ordinal), LogMessages(), "The failed hello should log no disconnect line");
    }

    /// <summary>
    /// A hello whose profile store ends with an exception other than a SQLite failure (the database is disposed, as at a
    /// stop) gets no reply, and the session's end frees the Companion's place and its pending place without logging a
    /// connected or a disconnect line.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestSessionRunAsyncHelloStoreExceptionFreesPlace()
    {
        // Arrange
        _disposables.OfType<ServerDatabase>().Single().Dispose();

        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(client, IngestClient.Hello(Token));
        await IngestClient.GuardAsync(run);

        // Assert
        await Assert.ThrowsAsync<WebSocketException>(() => IngestClient.ReceiveAsync(client), "The connection should end without a reply");
        Assert.IsTrue(_connections.TryTakeCompanion(), "The Companion's place should be released");
        Assert.AreEqual(IngestConnections.MaxPendingHellos, CountFreePendingPlaces(), "The pending place should be left");
        Assert.DoesNotContain("Companion connected.", LogMessages(), "The hello should not be logged as connected");
        Assert.DoesNotContain(message => message.StartsWith("Companion disconnected", StringComparison.Ordinal), LogMessages(), "The hello should log no disconnect line");
    }

    /// <summary>
    /// A follower that blocks delays no acknowledgement: the tick is acknowledged while its follower is still blocked.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task IngestSessionRunAsyncBlockingFollowerDelaysNoAcknowledgement()
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var release = new ManualResetEventSlim(false);

        _state.Changed += (_, _) =>
                          {
                              entered.TrySetResult();
                              release.Wait(_followerBound, TestContext.CancellationToken);
                          };

        // Act
        ContractMessage? reply;

        try
        {
            await IngestClient.SendAsync(client, IngestClient.Tick(Id1));

            (reply, _, _) = await IngestClient.ReceiveAsync(client);

            await entered.Task.WaitAsync(_followerBound, TestContext.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        // Assert
        AssertAcknowledgement(reply, Id1, null, null, []);
    }

    /// <summary>
    /// An accepted hello sets the Companion connected without a pipe state, and a status sets the pipe state it reports.
    /// </summary>
    /// <param name="wireName">The pipe state as the status names it</param>
    /// <param name="pipeState">The expected pipe state</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("waitingForGame", ContractPipeState.WaitingForGame)]
    [DataRow("connected", ContractPipeState.Connected)]
    [DataRow("receiving", ContractPipeState.Receiving)]
    [DataRow("pipeBusy", ContractPipeState.PipeBusy)]
    [DataRow("protocolMismatch", ContractPipeState.ProtocolMismatch)]
    [DataRow("error", ContractPipeState.Error)]
    public async Task IngestSessionStatusSetsCompanionState(string wireName, ContractPipeState pipeState)
    {
        // Arrange
        var (_, client, _) = await StartAcceptedSessionAsync();

        var afterHello = DescribeCompanion(_state.CompanionState);

        // Act
        await SendAllAsync(client, [StatusOf(wireName), IngestClient.Tick(Id1)]);

        var (reply, _, _) = await IngestClient.ReceiveAsync(client);

        // Assert
        AssertAcknowledgement(reply, Id1, null, null, []);
        Assert.AreEqual(DescribeCompanion(true, null), afterHello, "After the hello the Companion should be connected without a pipe state");
        Assert.AreEqual(DescribeCompanion(true, pipeState), DescribeCompanion(_state.CompanionState), "The status should set the Companion's pipe state");
    }

    /// <summary>
    /// When the connected Companion's session ends, the Companion state is not connected and without a pipe state before its
    /// place is free: a next Companion that takes the place at once finds the state of the first one cleared.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task IngestSessionDisconnectClearsCompanionBeforePlaceIsFree()
    {
        // Arrange
        var (run, client, _) = await StartAcceptedSessionAsync();

        await SendAllAsync(client, [StatusOf("receiving"), IngestClient.Tick(Id1)]);
        await IngestClient.ReceiveAsync(client);

        var connectedBefore = DescribeCompanion(_state.CompanionState);
        var atFreePlace = WatchFreePlace();

        // Act
        await FinishAsync(client, run);

        // Assert
        Assert.AreEqual(DescribeCompanion(true, ContractPipeState.Receiving), connectedBefore, "The Companion should be connected with its pipe state before its session ends, or the test would prove less");
        Assert.AreEqual(DescribeCompanion(false, null), await atFreePlace.WaitAsync(_followerBound, TestContext.CancellationToken), "The state should be cleared when the place becomes free");
        Assert.AreEqual(DescribeCompanion(false, null), DescribeCompanion(_state.CompanionState), "The state should stay cleared after the session ended");
    }

    /// <summary>
    /// A hello refused companionConnected leaves the connected Companion's state as it is when its session ends.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task IngestSessionRunAsyncRefusedHelloLeavesCompanionState()
    {
        // Arrange
        var (_, firstClient, _) = await StartAcceptedSessionAsync();

        await SendAllAsync(firstClient, [StatusOf("waitingForGame"), IngestClient.Tick(Id1)]);
        await IngestClient.ReceiveAsync(firstClient);

        var (secondRun, secondClient, _) = StartSession(IngestAccess.CreateForToken(Token));

        // Act
        await IngestClient.SendAsync(secondClient, IngestClient.Hello(Token));

        var (messages, _, _) = await IngestClient.ReceiveUntilCloseAsync(secondClient);

        await FinishAsync(secondClient, secondRun);

        // Assert
        AssertHelloRefused(messages[0], ContractHelloRefusal.CompanionConnected, 1);
        Assert.AreEqual(DescribeCompanion(true, ContractPipeState.WaitingForGame), DescribeCompanion(_state.CompanionState), "The refused session should leave the connected Companion's state");
    }

    /// <summary>
    /// Builds a status message with a pipe state.
    /// </summary>
    /// <param name="wireName">The pipe state as the contract names it</param>
    /// <returns>The status message in single-quote notation</returns>
    private static string StatusOf(string wireName)
    {
        return IngestClient.Status().Replace("'receiving'", $"'{wireName}'", StringComparison.Ordinal);
    }

    /// <summary>
    /// Describes a Companion state as text.
    /// </summary>
    /// <param name="isConnected">Whether a Companion is connected</param>
    /// <param name="pipeState">The pipe state, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string DescribeCompanion(bool isConnected, ContractPipeState? pipeState)
    {
        return $"connected {isConnected} pipe {pipeState?.ToString() ?? "<none>"}";
    }

    /// <summary>
    /// Describes a Companion state as text.
    /// </summary>
    /// <param name="companion">The Companion state</param>
    /// <returns>The text</returns>
    private static string DescribeCompanion(CompanionConnection companion)
    {
        return DescribeCompanion(companion.IsConnected, companion.PipeState);
    }

    /// <summary>
    /// Builds a hello with the stored token for a profile identity and name.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <returns>The hello in single-quote notation</returns>
    private static string HelloAs(string profileId, string name)
    {
        return IngestClient.Hello(Token)
                           .Replace(IngestClient.Profile, profileId, StringComparison.Ordinal)
                           .Replace($"'{HelloProfileName}'", $"'{name}'", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a tick of <see cref="OtherProfile" />.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <returns>The tick in single-quote notation</returns>
    private static string TickOfOtherProfile(string id)
    {
        return IngestClient.Tick(id).Replace(IngestClient.Profile, OtherProfile, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds an icon image whose PNG bytes hold a text, with the reference the contract requires (their SHA-256).
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <param name="content">The text of the PNG bytes</param>
    /// <returns>The reference and the icon image in single-quote notation</returns>
    private static (string Reference, string Notation) IconMessage(string id, string content)
    {
        var png = Encoding.UTF8.GetBytes(content);
        var reference = Convert.ToHexStringLower(SHA256.HashData(png));

        return (reference, $"{{'type':'iconImage','contractVersion':1,'id':'{id}','reference':'{reference}','png':'{Convert.ToBase64String(png)}'}}");
    }

    /// <summary>
    /// Builds a catalog document with one entry per icon reference.
    /// </summary>
    /// <param name="icons">The icon references</param>
    /// <returns>The bytes of the document</returns>
    private static byte[] CatalogJson(params string[] icons)
    {
        var entries = icons.Select((icon, index) => $"{{'guid':{index + 1},'kind':'good','names':{{}},'regionGuid':null,'regionTypes':null,'icon':'{icon}'}}");

        return Encoding.UTF8.GetBytes(IngestClient.Json($"{{'schemaVersion':1,'gameVersion':null,'entries':[{string.Join(',', entries)}]}}"));
    }

    /// <summary>
    /// Describes listed profiles as text, in the order given.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="only">The one profile to describe, or <see langword="null" /> for all</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeProfiles(IReadOnlyList<StoredProfile> profiles, Guid? only)
    {
        return [.. profiles.Where(profile => only is null || profile.Id == only).Select(profile => $"{profile.Id} {profile.Name ?? "<no name>"}")];
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

        var session = new IngestSession(server, access, _connections, _state, _timeProvider, _logger, serverVersion);

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
    /// Takes the Companion's place on a thread of its own the moment it becomes free, as a next Companion would, and
    /// describes the Companion state seen right then.
    /// </summary>
    /// <returns>The task that completes with the description once the place was taken</returns>
    private Task<string> WatchFreePlace()
    {
        var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        _disposables.Add(guard);
        guard.CancelAfter(_followerBound);

        var token = guard.Token;

        return Task.Factory.StartNew(() =>
                                     {
                                         while (_connections.TryTakeCompanion() == false)
                                         {
                                             token.ThrowIfCancellationRequested();
                                         }

                                         return DescribeCompanion(_state.CompanionState);
                                     },
                                     token,
                                     TaskCreationOptions.LongRunning,
                                     TaskScheduler.Default);
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

    /// <summary>
    /// Asserts that the session logged exactly one error: the line of a message that cannot be stored, with the SQLite
    /// exception.
    /// </summary>
    private void AssertOneStoreFailureLogged()
    {
        var errors = _logger.Entries.Where(entry => entry.Level == LogLevel.Error).ToList();

        Assert.AreSequenceEqual(new[] { StoreFailureLine }, errors.Select(entry => entry.Message).ToList(), "The failed store should be logged once as an error");
        Assert.IsInstanceOfType<SqliteException>(errors[0].Exception, "The error should carry the SQLite exception");
    }

    /// <summary>
    /// Adds a follower of the state that records each notice it is told.
    /// </summary>
    /// <param name="expected">The number of notices after which the returned task completes</param>
    /// <returns>The notices and the task that completes once the expected number was told</returns>
    private (ConcurrentQueue<string> Notices, Task Told) Follow(int expected)
    {
        var notices = new ConcurrentQueue<string>();
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _state.Changed += (_, change) =>
                          {
                              notices.Enqueue($"{change.Kind} {change.ProfileId?.ToString() ?? "<no profile>"}");

                              if (notices.Count >= expected)
                              {
                                  told.TrySetResult();
                              }
                          };

        return (notices, told.Task);
    }

    /// <summary>
    /// Runs a whole accepted session: the hello, the messages with their acknowledgements, and the close.
    /// </summary>
    /// <param name="messages">The messages after the hello, each acknowledged</param>
    /// <returns>The helloAccepted and the acknowledgements</returns>
    private async Task<(ContractHelloAccepted Accepted, List<ContractMessage?> Replies)> RunAcceptedSessionAsync(string[] messages)
    {
        var (run, client, _) = StartSession(IngestAccess.CreateForToken(Token));

        await IngestClient.SendAsync(client, IngestClient.Hello(Token)).ConfigureAwait(false);

        var (reply, _, _) = await IngestClient.ReceiveAsync(client).ConfigureAwait(false);

        await SendAllAsync(client, messages).ConfigureAwait(false);

        var replies = await ReceiveMessagesAsync(client, messages.Length).ConfigureAwait(false);

        await FinishAsync(client, run).ConfigureAwait(false);

        return (Assert.IsInstanceOfType<ContractHelloAccepted>(reply, "The hello should be accepted"), replies);
    }

    /// <summary>
    /// Opens a database (a file, or in memory), a store and a restored state that the sessions started afterwards share.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null" /> for in memory</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task OpenStateAsync(string? databaseFile)
    {
        var opened = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken).ConfigureAwait(false);
        var database = opened.Database ?? throw new InvalidOperationException($"The database could not be opened: {opened.Kind}", opened.Failure);

        _disposables.Add(database);
        _databaseFile = databaseFile;
        _store = new ServerStore(database);
        _state = new ServerState(_store, _stateLogger);

        await _state.RestoreAsync(TestContext.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces the in-memory state by one over a database file in a new temporary directory, for a test that needs a
    /// second connection to the file.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    private Task UseDatabaseFileAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-ingest-session-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        return OpenStateAsync(Path.Combine(directory, "censio.db"));
    }

    /// <summary>
    /// Opens a connection of its own, without the pool, to the database file of the test.
    /// </summary>
    /// <returns>The opened connection</returns>
    private SqliteConnection OpenRawConnection()
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = _databaseFile ?? throw new InvalidOperationException("The test should use a database file"),
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };
        var connection = new SqliteConnection(builder.ToString());

        connection.Open();

        return connection;
    }

    /// <summary>
    /// Runs a statement over a connection of its own to the database file of the test.
    /// </summary>
    /// <param name="sql">The constant statement</param>
    private void ExecuteOnFile(string sql)
    {
        using var connection = OpenRawConnection();
        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only
        command.CommandText = sql;
#pragma warning restore CA2100

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Takes the write lock of the database file over a connection of its own, until the returned connection is disposed.
    /// </summary>
    /// <returns>The connection that holds the lock</returns>
    private SqliteConnection HoldWriteLock()
    {
        var connection = OpenRawConnection();

        using var command = connection.CreateCommand();

        command.CommandText = "BEGIN IMMEDIATE;";
        command.ExecuteNonQuery();

        return connection;
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

        SqliteConnection.ClearAllPools();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }

        _stopping.Dispose();
    }

    #endregion // IDisposable
}