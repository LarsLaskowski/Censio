using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;

using Censio.Contracts;
using Censio.GameData;
using Censio.Pipe;
using Censio.Pipe.Tests;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for the catalog upload of <see cref="LiveSender" />.
/// </summary>
[TestClass]
public sealed class LiveSenderCatalogTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The token of the forward setting.
    /// </summary>
    private const string Token = "CatalogToken-0123456789abcdefghijKLMNOPQR";

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private const string CompanionVersion = "1.2.3";

    /// <summary>
    /// The host of the server address, which must never reach the log.
    /// </summary>
    private const string ServerHost = "catalog-host.example";

    /// <summary>
    /// The port of the local server.
    /// </summary>
    private const int LocalPort = 5117;

    /// <summary>
    /// The number of ten-millisecond polls of a condition before it counts as not reached.
    /// </summary>
    private const int MaxPolls = 500;

    /// <summary>
    /// The text every log line of a sent catalog starts with.
    /// </summary>
    private const string SentPrefix = "Catalog sent to the server";

    /// <summary>
    /// The log line of a catalog the server already holds.
    /// </summary>
    private const string AlreadyLine = "Catalog already on the server";

    /// <summary>
    /// The log line of a catalog the writer refused.
    /// </summary>
    private const string NotWrittenLine = "Catalog not sent: the contract writer refused it";

    /// <summary>
    /// The log line of a catalog given up after failed connections.
    /// </summary>
    private const string AbandonedLine = "Catalog not sent: the connection failed 3 times during its upload";

    /// <summary>
    /// The log line of a lost connection after a reply did not come.
    /// </summary>
    private const string NoAnswerLine = "Live sending failed: server does not answer; retrying";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start time of the time provider.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address of the server.
    /// </summary>
    private static readonly Uri _address = new UriBuilder(Uri.UriSchemeWs, ServerHost, 8080, "/ingest", "?key=hidden").Uri;

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// An identity of a catalog the Companion does not hold.
    /// </summary>
    private static readonly string _otherIdentity = new('a', 64);

    /// <summary>
    /// The logger of the senders.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    /// <summary>
    /// The logger of the buffers, kept apart from the senders' log.
    /// </summary>
    private readonly ConcurrentRecordingLogger _bufferLogger = new();

    /// <summary>
    /// The time provider of the senders, which records its timers.
    /// </summary>
    private readonly RecordingTimeProvider _timeProvider = new(_start);

    /// <summary>
    /// The connector of the senders.
    /// </summary>
    private readonly QueuedIngestConnector _connector = new();

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
    /// The loops that serve the scripted servers.
    /// </summary>
    private readonly List<Task> _serving = [];

    /// <summary>
    /// The buffers of the test, disposed after the senders.
    /// </summary>
    private readonly List<LiveBuffer> _buffers = [];

    /// <summary>
    /// The runs of the buffers, ended by their dispose.
    /// </summary>
    private readonly List<Task> _bufferRuns = [];

    /// <summary>
    /// The short names of the identities and icon references of the test catalogs, as the labels show them.
    /// </summary>
    private readonly Dictionary<string, string> _names = [];

    /// <summary>
    /// The temporary folder that holds the buffer folders of the test, deleted afterwards.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-catalog-sender-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSenderCatalogTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LiveSenderCatalogTests(TestContext testContext)
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
    /// Stops the runs, disposes the senders, the buffers and the servers, and deletes the buffer folders.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
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

        await Task.WhenAll(_serving).WaitAsync(_guard, TestContext.CancellationToken);

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// A ready catalog is uploaded right after the first accepted hello, as the catalog writer's bytes of the converted
    /// catalog, then one icon per listed reference with the PNG whose SHA-256 is the reference, and then the waiting
    /// messages (S1, N1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LiveSenderUploadsCatalogBeforeWaitingMessages()
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var served = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));
        sender.Enqueue(TickItem(2));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 6);

        // Assert
        var upload = served.Received.OfType<ContractCatalogUpload>().ToList();
        var icons = served.Received.OfType<ContractIconImage>().ToList();

        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "icon A2", "tick 1", "tick 2"), Labels(served.Received), "The catalog and its icons should come before the waiting messages");
        Assert.HasCount(1, upload, "Exactly one catalog should be uploaded");
        Assert.AreSequenceEqual(CatalogTestData.WriteDocument(catalogA.Catalog, catalogA.Icons, catalogA.Build), upload[0].Document.ToArray(), "The upload should carry the bytes of the document the writer gives for the converted catalog");
        Assert.AreSequenceEqual(catalogA.References, icons.Select(icon => icon.Reference).ToArray(), "One icon per listed reference should follow");
        Assert.AreSequenceEqual(catalogA.Pngs[0], icons[0].Png.ToArray(), "The first icon should carry its PNG");
        Assert.AreSequenceEqual(catalogA.Pngs[1], icons[1].Png.ToArray(), "The second icon should carry its PNG");
        Assert.AreSequenceEqual(catalogA.References, icons.Select(icon => Convert.ToHexStringLower(SHA256.HashData(icon.Png.Span))).ToArray(), "The SHA-256 of each PNG should be its reference");
        Assert.AreEqual(catalogA.Identity, upload[0].ComputeIdentity(), "The identity of the upload should be the Companion's catalog identity");
    }

    /// <summary>
    /// Only the icons the acknowledgement lists are sent, in the order it lists them, not the order of the catalog (S1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LiveSenderSendsMissingIconsInListedOrder()
    {
        // Arrange
        var catalogA = AddCatalog("A", 3);
        var served = AddServer(null, Accepting(catalogA.References[0], catalogA.References[2]));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 5);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "icon A3", "tick 1"), Labels(served.Received), "Only the listed icons should be sent, in the listed order");
    }

    /// <summary>
    /// A server whose catalog is another one, as from another installation, gets the catalog replaced (E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LiveSenderUploadsWhenServerNamesAnotherIdentity()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var served = AddServer(_otherIdentity, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 4);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "tick 1"), Labels(served.Received), "A catalog the server does not hold by its identity should be uploaded");
    }

    /// <summary>
    /// Live sending to the local server uploads the catalog as the forward mode does (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LiveSenderUploadsToLocalServer()
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var served = AddServer(null, Accepting(catalogA.References));
        var sender = CreateLocalSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 5);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "icon A2", "tick 1"), Labels(served.Received), "The local server should get the catalog and its icons before the waiting messages");
        Assert.AreSequenceEqual(new[] { new Uri($"ws://127.0.0.1:{LocalPort}/ingest") }, _connector.Addresses.ToArray(), "The sender should connect to the local server");
    }

    /// <summary>
    /// A server that names the identity of the Companion's catalog gets no catalog and no icon, and the Companion logs that
    /// the catalog is already there, once (S2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveSenderSkipsUploadWhenServerNamesIdentity()
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var served = AddServer(catalogA.Identity, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(served.Received), "Neither a catalog nor an icon should be sent");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Information, Message: AlreadyLine }, _logger.Entries.ToList(), "The skip should be logged once");
    }

    /// <summary>
    /// A catalog that becomes ready while the connection is open is uploaded after the message in flight, before the next
    /// waiting message (S3, S4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderUploadsCatalogReadyAfterHelloAfterMessageInFlight()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var served = AddServer(null, Holding(message => message is ContractTick { GameTime: 1 }, catalogA.References));
        var sender = CreateSender();

        sender.Enqueue(TickItem(1));
        sender.Enqueue(TickItem(2));
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Act
        Offer(sender, catalogA);
        await SettleAsync();

        var beforeAcknowledgement = Labels(served.Received);

        await served.Server.AcknowledgeAsync(served.Received.OfType<ContractTick>().Single(), null, TestContext.CancellationToken);
        await WaitForMessagesAsync(served.Received, 5);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), beforeAcknowledgement, "The catalog should wait for the acknowledgement of the message in flight");
        Assert.AreSequenceEqual(Expected("hello", "tick 1", "catalog A", "icon A1", "tick 2"), Labels(served.Received), "The catalog should follow the message in flight and come before the next waiting message");
    }

    /// <summary>
    /// A catalog that becomes ready again with the identity of the one the server holds is not uploaded and logs the skip once,
    /// however often it is offered (S4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderSkipsReextractedCatalogWithSameIdentity()
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var again = AddCatalog("A", 2);
        var third = AddCatalog("A", 2);
        var served = AddServer(catalogA.Identity, Accepting(catalogA.References));
        var sender = CreateSender();

        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 1);

        // Act
        Offer(sender, again);
        await WaitUntilAsync(() => Lines().Contains(AlreadyLine), "The skip should be logged");
        Offer(sender, third);
        sender.Enqueue(TickItem(1));
        await WaitForMessagesAsync(served.Received, 2);

        // Assert
        Assert.AreEqual(catalogA.Identity, again.Identity, "Both catalogs should have the same identity");
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(served.Received), "No catalog and no icon should be sent");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Information, Message: AlreadyLine }, _logger.Entries.ToList(), "The skip should be logged once");
    }

    /// <summary>
    /// A newer catalog stops the icons of an older one: its remaining icons are not sent, no line is logged for it, and the
    /// newer one follows the icon in flight (E6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveSenderNewerCatalogStopsIconsOfOlder()
    {
        // Arrange
        var catalogA = AddCatalog("A", 3);
        var catalogB = AddCatalog("B", 1);
        var served = AddServer(null,
                               (_, message) => message switch
                                               {
                                                   ContractCatalogUpload upload => Reply(message, null, upload.ComputeIdentity() == catalogA.Identity ? catalogA.References : catalogB.References),
                                                   ContractIconImage icon when icon.Reference == catalogA.References[0] => null,
                                                   _ => Ack(message)
                                               });
        var sender = CreateSender();

        Offer(sender, catalogA);
        StartRun(sender);
        await WaitForMessagesAsync(served.Received, 3);

        // Act
        Offer(sender, catalogB);
        await SettleAsync();
        await served.Server.AcknowledgeAsync(served.Received.OfType<ContractIconImage>().Single(), null, TestContext.CancellationToken);
        await WaitForMessagesAsync(served.Received, 5);
        await WaitUntilAsync(() => Lines().Any(line => line.StartsWith(SentPrefix, StringComparison.Ordinal)), "The newer catalog should be logged as sent");

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "catalog B", "icon B1"), Labels(served.Received), "The remaining icons of the older catalog should not be sent, and the newer catalog should follow");
        Assert.ContainsSingle(entry => entry.Message.StartsWith(SentPrefix, StringComparison.Ordinal), _logger.Entries.ToList(), "Only the newer catalog should be logged as sent");
    }

    /// <summary>
    /// A status that is not final offers no catalog: nothing is uploaded while the loader still reads, checks or extracts
    /// (S3).
    /// </summary>
    /// <param name="kind">The kind of the status</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(GameCatalogStatusKind.Reading)]
    [DataRow(GameCatalogStatusKind.Checking)]
    [DataRow(GameCatalogStatusKind.ExtractingNames)]
    [DataRow(GameCatalogStatusKind.ExtractingIcons)]
    public async Task LiveSenderIgnoresCatalogBeforeItsFinalStatus(GameCatalogStatusKind kind)
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var served = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        sender.SetCatalog(StatusOf(kind), catalogA.Catalog, null, catalogA.Build);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(served.Received), "No catalog should be uploaded before the final status");
        Assert.IsEmpty(CatalogLines(), "Nothing about a catalog should be logged");
    }

    /// <summary>
    /// A connection lost while the icons are sent leads to the upload again at the next accepted hello that names no catalog
    /// and to exactly the icons its acknowledgement lists (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderResendsCatalogAndListedIconsAfterLostConnection()
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var first = AddServer(null, Aborting(message => message is ContractIconImage icon && icon.Reference == catalogA.References[1], catalogA.References));
        var second = AddServer(null, Accepting(catalogA.References[1]));
        var sender = CreateSender();

        Offer(sender, catalogA);

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(first.Received, 4);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
        await WaitForMessagesAsync(second.Received, 3);
        await WaitUntilAsync(() => Lines().Contains("Catalog sent to the server: 1 icons sent, 0 refused"), "The completed upload should be logged");

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "icon A2"), Labels(first.Received), "The first connection should end while the second icon is sent");
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A2"), Labels(second.Received), "The next connection should upload again and send only the icon the acknowledgement lists");
        Assert.ContainsSingle(line => line.StartsWith(SentPrefix, StringComparison.Ordinal), Lines(), "Only the completed upload should be logged as sent");
    }

    /// <summary>
    /// A catalog that is not acknowledged within 30 seconds ends the connection as not answering, and the next connection
    /// uploads it again (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveSenderRetriesCatalogAfterAcknowledgementTimeout()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var first = AddServer(null, Holding(message => message is ContractCatalogUpload, catalogA.References));
        var second = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        StartRun(sender);
        await WaitForMessagesAsync(first.Received, 2);

        // Act
        await AdvanceUntilAsync(IngestClient.ReplyTimeout, () => Lines().Contains(NoAnswerLine));
        await AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => _connector.AttemptCount >= 2);
        await WaitForMessagesAsync(second.Received, 3);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(first.Received), "The first connection should not get further than the catalog");
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1"), Labels(second.Received), "The catalog should be uploaded again after the reconnect");
    }

    /// <summary>
    /// Without a game and after a failed extraction there is no catalog: nothing is sent and nothing is logged (E1).
    /// </summary>
    /// <param name="kind">The kind of the status</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameCatalogStatusKind.NoGame)]
    [DataRow(GameCatalogStatusKind.Failed)]
    public async Task LiveSenderSendsNoCatalogWithoutGame(GameCatalogStatusKind kind)
    {
        // Arrange
        var served = AddServer(null, Accepting());
        var sender = CreateSender();

        sender.SetCatalog(StatusOf(kind), null, null, null);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(served.Received), "No catalog should be sent");
        Assert.IsEmpty(CatalogLines(), "Nothing about a catalog should be logged");
    }

    /// <summary>
    /// A catalog that was ready is forgotten when the game is gone or its extraction failed: nothing is uploaded (E1).
    /// </summary>
    /// <param name="kind">The kind of the status that clears the catalog</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(GameCatalogStatusKind.NoGame)]
    [DataRow(GameCatalogStatusKind.Failed)]
    public async Task LiveSenderClearsCatalogAfterFailedExtraction(GameCatalogStatusKind kind)
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var served = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.SetCatalog(StatusOf(kind), null, null, null);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(served.Received), "A cleared catalog should not be uploaded");
        Assert.IsEmpty(CatalogLines(), "Nothing about a catalog should be logged");
    }

    /// <summary>
    /// A catalog the server refuses sends no icon, logs the refusal kind as a warning, is not uploaded again after the
    /// reconnect, and the waiting messages are sent (E2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderDoesNotUploadRefusedCatalogAgain()
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);
        var first = AddServer(null,
                              (server, message) => message switch
                                                   {
                                                       ContractCatalogUpload => Reply(message, ContractAcknowledgementRefusal.CatalogInvalid, []),
                                                       ContractTick { GameTime: 2 } => Abort(server),
                                                       _ => Ack(message)
                                                   });
        var second = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));
        sender.Enqueue(TickItem(2));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(first.Received, 4);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
        await WaitForMessagesAsync(second.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "tick 1", "tick 2"), Labels(first.Received), "A refused catalog should send no icon, and the waiting messages should follow");
        Assert.AreSequenceEqual(Expected("hello", "tick 2"), Labels(second.Received), "A refused catalog should not be uploaded again after the reconnect");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: "Server refused the catalog: CatalogInvalid" }, _logger.Entries.ToList(), "The refusal should be logged once with its kind");
    }

    /// <summary>
    /// A refused icon is counted in the line of the sent catalog and the next icon follows; no line is logged per icon (E3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderCountsRefusedIcon()
    {
        // Arrange
        var catalogA = AddCatalog("A", 3);
        var served = AddServer(null,
                               (_, message) => message switch
                                               {
                                                   ContractCatalogUpload => Reply(message, null, catalogA.References),
                                                   ContractIconImage icon when icon.Reference == catalogA.References[1] => Reply(message, ContractAcknowledgementRefusal.Invalid, []),
                                                   _ => Ack(message)
                                               });
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 6);

        // Assert
        var sentLines = Lines().Where(line => line.StartsWith(SentPrefix, StringComparison.Ordinal)).ToList();

        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "icon A2", "icon A3", "tick 1"), Labels(served.Received), "The icon after a refused one should follow");
        Assert.HasCount(1, sentLines, "The upload should be logged once");
        Assert.AreEqual("Catalog sent to the server: 2 icons sent, 1 refused", sentLines[0], "The line should count the accepted and the refused icons");
        Assert.IsEmpty(Lines().Where(line => line.StartsWith("Server refused", StringComparison.Ordinal)).ToList(), "No line should be logged per icon");
    }

    /// <summary>
    /// A catalog the contract writer refuses sends nothing, logs a warning once, and the waiting messages are sent (E4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task LiveSenderLogsUnwritableCatalogAndSendsNothing()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var served = AddServer(_otherIdentity, Accepting(catalogA.References));
        var sender = CreateSender();

        Assert.IsNotNull(CatalogDocumentWriter.Write(GameCatalogDocumentConverter.ToDocument(catalogA.Catalog, catalogA.Icons, "not a version")).Error, "The document of the test should be refused by the writer");

        sender.SetCatalog(StatusOf(GameCatalogStatusKind.Ready), catalogA.Catalog, catalogA.Icons, "not a version");
        sender.Enqueue(TickItem(1));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(served.Received), "Nothing of the catalog should be sent");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: NotWrittenLine }, _logger.Entries.ToList(), "The refusal of the writer should be logged once as a warning");
    }

    /// <summary>
    /// Catalog and icon messages never reach the buffer: while the catalog is in flight the buffer holds only the waiting
    /// messages and their files, and afterwards nothing (E5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveSenderNeverBuffersCatalogMessages()
    {
        // Arrange
        var catalogA = AddCatalog("A", 3);
        var folder = NewBufferFolder();
        var buffer = CreateRunningBuffer(folder);
        var served = AddServer(null, Holding(message => message is ContractCatalogUpload, catalogA.References));
        var sender = CreateSender(buffer);

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));
        sender.Enqueue(TickItem(2));

        await WaitUntilAsync(() => MessageFileCount(folder) == 2, "The waiting messages should be written to the buffer folder");

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        var duringUpload = (buffer.Status.Waiting, MessageFileCount(folder));

        await served.Server.AcknowledgeAsync(served.Received.OfType<ContractCatalogUpload>().Single(), null, catalogA.References, TestContext.CancellationToken);
        await WaitForMessagesAsync(served.Received, 7);
        await WaitUntilAsync(() => buffer.Status.Waiting == 0 && MessageFileCount(folder) == 0, "The buffer should be empty once the messages are acknowledged");

        // Assert
        Assert.AreEqual((2, 2), duringUpload, "Only the two waiting messages should be buffered, in memory and in files, while the catalog is in flight");
        Assert.AreSequenceEqual(Expected("hello", "catalog A", "icon A1", "icon A2", "icon A3", "tick 1", "tick 2"), Labels(served.Received), "The catalog, its icons and the messages should be sent in order");
    }

    /// <summary>
    /// Disposing the sender while the catalog is in flight ends the run (E10).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveSenderDisposeDuringUploadEndsRun()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var served = AddServer(null, Holding(message => message is ContractCatalogUpload, catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);

        var run = StartTrackedRun(sender);

        await WaitForMessagesAsync(served.Received, 2);

        // Act
        await sender.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(TaskStatus.RanToCompletion, run.Status, "The run should end with the dispose");
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(served.Received), "Nothing more than the catalog should have been sent");
    }

    /// <summary>
    /// The five catalog log lines have exactly their text and level and hold no name, reference, identity, path or host
    /// (N4, N7).
    /// </summary>
    /// <param name="scenario">The scenario that writes the line</param>
    /// <param name="level">The level of the line</param>
    /// <param name="message">The text of the line</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("sent", LogLevel.Information, "Catalog sent to the server: 2 icons sent, 0 refused", DisplayName = "Catalog sent")]
    [DataRow("already", LogLevel.Information, AlreadyLine, DisplayName = "Catalog already on the server")]
    [DataRow("refused", LogLevel.Warning, "Server refused the catalog: CatalogInvalid", DisplayName = "Server refused the catalog")]
    [DataRow("unwritable", LogLevel.Warning, NotWrittenLine, DisplayName = "Writer refused the catalog")]
    [DataRow("abandoned", LogLevel.Warning, AbandonedLine, DisplayName = "Catalog given up")]
    public async Task LiveSenderCatalogLogLinesHaveTextAndLevel(string scenario, LogLevel level, string message)
    {
        // Arrange
        var catalogA = AddCatalog("A", 2);

        // Act
        await RunLogScenarioAsync(scenario, catalogA);

        // Assert
        var entries = _logger.Entries.ToList();
        var secrets = CatalogTestData.NamesOf("A", 2).Concat(catalogA.References).Append(catalogA.Identity).Append(_root).Append(ServerHost).ToList();

        Assert.ContainsSingle(entry => entry.Level == level && entry.Message == message, entries, "The line should be written once with its text and level");
        Assert.IsEmpty(entries.Where(entry => secrets.Exists(secret => entry.Message.Contains(secret, StringComparison.OrdinalIgnoreCase))).ToList(), "No line should hold a name, reference, identity, path or host");
    }

    /// <summary>
    /// A server that never takes the catalog does not hold back the live messages: they are acknowledged on the connections
    /// after the failed ones, waiting messages and later ones, and after the third failed connection the catalog is given up
    /// and logged once (E11).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task LiveSenderSendsLiveMessagesWhileCatalogKeepsFailing()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var first = AddServer(null, Aborting(IsCatalog, catalogA.References));
        var second = AddServer(null, Aborting(IsCatalog, catalogA.References));
        var third = AddServer(null, Aborting(IsCatalog, catalogA.References));
        var fourth = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));
        sender.Enqueue(TickItem(2));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(first.Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
        await WaitForMessagesAsync(second.Received, 4);
        sender.Enqueue(TickItem(3));
        await AdvanceRetryAsync(TimeSpan.FromSeconds(2), 1);
        await WaitForMessagesAsync(third.Received, 3);
        sender.Enqueue(TickItem(4));
        await WaitUntilAsync(() => Lines().Contains(AbandonedLine), "The catalog should be given up after the third failed connection");
        await AdvanceRetryAsync(TimeSpan.FromSeconds(4), 1);
        await WaitForMessagesAsync(fourth.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(first.Received), "The catalog should come first at the first hello");
        Assert.AreSequenceEqual(Expected("hello", "tick 1", "tick 2", "catalog A"), Labels(second.Received), "After a failed upload the waiting messages should come first");
        Assert.AreSequenceEqual(Expected("hello", "tick 3", "catalog A"), Labels(third.Received), "A message taken later should be sent before the deferred catalog");
        Assert.AreSequenceEqual(Expected("hello", "tick 4"), Labels(fourth.Received), "The given up catalog should not be sent again");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: AbandonedLine }, _logger.Entries.ToList(), "The give up should be logged once");
    }

    /// <summary>
    /// After the third connection that failed during the upload the catalog is not sent again in this run, and the give up is
    /// logged once (E11).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task LiveSenderAbandonsCatalogAfterThreeFailedConnections()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var failing = new[] { AddServer(null, Aborting(IsCatalog, catalogA.References)), AddServer(null, Aborting(IsCatalog, catalogA.References)), AddServer(null, Aborting(IsCatalog, catalogA.References)) };
        var last = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(failing[0].Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
        await WaitForMessagesAsync(failing[1].Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(2), 1);
        await WaitForMessagesAsync(failing[2].Received, 2);
        await WaitUntilAsync(() => Lines().Contains(AbandonedLine), "The catalog should be given up after the third failed connection");
        sender.Enqueue(TickItem(1));
        await AdvanceRetryAsync(TimeSpan.FromSeconds(4), 1);
        await WaitForMessagesAsync(last.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(failing[0].Received), "The first connection should get the hello and the catalog only");
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(failing[1].Received), "The second connection should get the hello and the catalog only");
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(failing[2].Received), "The third connection should get the hello and the catalog only");
        Assert.AreSequenceEqual(Expected("hello", "tick 1"), Labels(last.Received), "The fourth connection should send no catalog");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: AbandonedLine }, _logger.Entries.ToList(), "The give up should be logged once");
    }

    /// <summary>
    /// A completed upload starts the count of failed connections again: after one failure, one completed upload and a server
    /// that names no catalog again, three more failures are needed to give the catalog up (E11).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task LiveSenderResetsFailuresAfterCompletedUpload()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var servers = new[]
                      {
                          AddServer(null, Aborting(IsCatalog, catalogA.References)),
                          AddServer(null, Accepting(catalogA.References)),
                          AddServer(null, Aborting(IsCatalog, catalogA.References)),
                          AddServer(null, Aborting(IsCatalog, catalogA.References)),
                          AddServer(null, Aborting(IsCatalog, catalogA.References))
                      };
        var sender = CreateSender();

        Offer(sender, catalogA);

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(servers[0].Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
        await WaitUntilAsync(() => Lines().Contains("Catalog sent to the server: 1 icons sent, 0 refused"), "The upload of the second connection should complete");
        servers[1].Server.Abort();
        await AdvanceRetryAsync(TimeSpan.FromSeconds(2), 1);
        await WaitForMessagesAsync(servers[2].Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(4), 1);
        await WaitForMessagesAsync(servers[3].Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(8), 1);
        await WaitForMessagesAsync(servers[4].Received, 2);
        await WaitUntilAsync(() => Lines().Contains(AbandonedLine), "The catalog should be given up after three failures since the completed upload");

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(servers[4].Received), "The fifth connection should still upload the catalog, as the count started again");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: AbandonedLine }, _logger.Entries.ToList(), "The give up should be logged once");
    }

    /// <summary>
    /// A connection that fails while the waiting messages are sent, before the deferred catalog's turn, neither counts as a
    /// failure of the catalog nor resets the count: the catalog is given up after the fourth connection (E11).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task LiveSenderKeepsFailureCountWhenConnectionFailsBeforeDeferredCatalog()
    {
        // Arrange
        var catalogA = AddCatalog("A", 1);
        var first = AddServer(null, Aborting(IsCatalog, catalogA.References));
        var second = AddServer(null, Aborting(message => message is ContractTick { GameTime: 2 }, catalogA.References));
        var third = AddServer(null, Aborting(IsCatalog, catalogA.References));
        var fourth = AddServer(null, Aborting(IsCatalog, catalogA.References));
        var fifth = AddServer(null, Accepting(catalogA.References));
        var sender = CreateSender();

        Offer(sender, catalogA);
        sender.Enqueue(TickItem(1));
        sender.Enqueue(TickItem(2));

        // Act
        StartRun(sender);

        await WaitForMessagesAsync(first.Received, 2);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
        await WaitForMessagesAsync(second.Received, 3);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(2), 1);
        await WaitForMessagesAsync(third.Received, 3);
        await AdvanceRetryAsync(TimeSpan.FromSeconds(4), 1);
        await WaitForMessagesAsync(fourth.Received, 2);
        await WaitUntilAsync(() => Lines().Contains(AbandonedLine), "The catalog should be given up after the fourth connection");
        sender.Enqueue(TickItem(3));
        await AdvanceRetryAsync(TimeSpan.FromSeconds(8), 1);
        await WaitForMessagesAsync(fifth.Received, 2);

        // Assert
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(first.Received), "The first connection should fail during the catalog");
        Assert.AreSequenceEqual(Expected("hello", "tick 1", "tick 2"), Labels(second.Received), "The second connection should fail during the waiting messages");
        Assert.AreSequenceEqual(Expected("hello", "tick 2", "catalog A"), Labels(third.Received), "The third connection should send the waiting message first and fail during the catalog");
        Assert.AreSequenceEqual(Expected("hello", "catalog A"), Labels(fourth.Received), "The fourth connection should fail during the catalog, the third such failure");
        Assert.AreSequenceEqual(Expected("hello", "tick 3"), Labels(fifth.Received), "The given up catalog should not be sent again");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: AbandonedLine }, _logger.Entries.ToList(), "The give up should be logged once");
    }

    /// <summary>
    /// Offers a ready catalog to a sender.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="data">The catalog data</param>
    private static void Offer(LiveSender sender, CatalogData data)
    {
        sender.SetCatalog(StatusOf(GameCatalogStatusKind.Ready), data.Catalog, data.Icons, data.Build);
    }

    /// <summary>
    /// Gives the labels a test expects.
    /// </summary>
    /// <param name="labels">The labels</param>
    /// <returns>The labels</returns>
    private static string[] Expected(params string[] labels)
    {
        return labels;
    }

    /// <summary>
    /// Creates the status of a kind as the loader publishes it.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The status</returns>
    private static GameCatalogStatus StatusOf(GameCatalogStatusKind kind)
    {
        return new GameCatalogStatus
               {
                   Kind = kind,
                   EntryCount = 3
               };
    }

    /// <summary>
    /// Creates a handler that accepts every message and lists the given icons as missing in the acknowledgement of a catalog.
    /// </summary>
    /// <param name="missing">The references the acknowledgement of a catalog lists</param>
    /// <returns>The handler</returns>
    private static Func<ScriptedIngestServer, ContractMessage, ContractAcknowledgement?> Accepting(params string[] missing)
    {
        return (_, message) => message is ContractCatalogUpload ? Reply(message, null, missing) : Ack(message);
    }

    /// <summary>
    /// Creates a handler that accepts every message but does not acknowledge those that match.
    /// </summary>
    /// <param name="isHeld">The predicate of the messages that stay unacknowledged</param>
    /// <param name="missing">The references the acknowledgement of a catalog lists</param>
    /// <returns>The handler</returns>
    private static Func<ScriptedIngestServer, ContractMessage, ContractAcknowledgement?> Holding(Func<ContractMessage, bool> isHeld, params string[] missing)
    {
        return (_, message) => isHeld(message) ? null : Accepting(missing)(null!, message);
    }

    /// <summary>
    /// Creates a handler that accepts every message but ends the connection without a close frame at those that match.
    /// </summary>
    /// <param name="isAbortPoint">The predicate of the messages at which the connection ends</param>
    /// <param name="missing">The references the acknowledgement of a catalog lists</param>
    /// <returns>The handler</returns>
    private static Func<ScriptedIngestServer, ContractMessage, ContractAcknowledgement?> Aborting(Func<ContractMessage, bool> isAbortPoint, params string[] missing)
    {
        return (server, message) => isAbortPoint(message) ? Abort(server) : Accepting(missing)(server, message);
    }

    /// <summary>
    /// Tells whether a message is a catalog upload.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns><see langword="true" /> for a catalog upload</returns>
    private static bool IsCatalog(ContractMessage message)
    {
        return message is ContractCatalogUpload;
    }

    /// <summary>
    /// Ends the connection without a close frame and acknowledges nothing.
    /// </summary>
    /// <param name="server">The server</param>
    /// <returns>No acknowledgement</returns>
    private static ContractAcknowledgement? Abort(ScriptedIngestServer server)
    {
        server.Abort();

        return null;
    }

    /// <summary>
    /// Acknowledges a message as accepted.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The acknowledgement</returns>
    private static ContractAcknowledgement Ack(ContractMessage message)
    {
        return ScriptedIngestServer.CreateAcknowledgement(ScriptedIngestServer.IdOf(message), null);
    }

    /// <summary>
    /// Acknowledges a message with a refusal or a list of missing icons.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="refusal">The refusal, or <see langword="null" /></param>
    /// <param name="missing">The references of the missing icons</param>
    /// <returns>The acknowledgement</returns>
    private static ContractAcknowledgement Reply(ContractMessage message, ContractAcknowledgementRefusal? refusal, IReadOnlyList<string> missing)
    {
        return ScriptedIngestServer.CreateAcknowledgement(ScriptedIngestServer.IdOf(message), refusal, missing);
    }

    /// <summary>
    /// Creates the item of a tick of one island.
    /// </summary>
    /// <param name="gameTime">The game time, which identifies the tick</param>
    /// <returns>The item</returns>
    private static PipeConnectionItem TickItem(long gameTime)
    {
        var tick = new PipeTick
                   {
                       GameTime = gameTime,
                       ReceiveTime = _start.AddSeconds(gameTime),
                       Islands = [
                                     new PipeTickIsland
                                     {
                                         Key = new PipeIslandKey(180023, 0, 0),
                                         SessionId = 1,
                                         Name = "Isle Of Hidden Names",
                                         Entries = []
                                     }
                                 ],
                       CloseReason = PipeTickCloseReason.GameTimeChanged
                   };

        return new PipeConnectionItem
               {
                   Result = PipeTickResult.FromTick(tick),
                   ReadStamp = _start.AddSeconds(gameTime + 1)
               };
    }

    /// <summary>
    /// Counts the message files of a buffer folder.
    /// </summary>
    /// <param name="folder">The buffer folder</param>
    /// <returns>The number of message files</returns>
    private static int MessageFileCount(string folder)
    {
        return Directory.Exists(folder)
                   ? Directory.EnumerateFiles(folder, "*.msg").Count()
                   : 0;
    }

    /// <summary>
    /// Creates the catalog of a name and remembers the short names of its identity and icons for the labels.
    /// </summary>
    /// <param name="name">The name of the catalog</param>
    /// <param name="iconCount">The number of icons</param>
    /// <returns>The catalog data</returns>
    private CatalogData AddCatalog(string name, int iconCount)
    {
        var data = CatalogTestData.Create(name, iconCount);

        _names[data.Identity] = name;

        foreach (var (reference, index) in data.References.Select((reference, index) => (reference, index)))
        {
            _names[reference] = name + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        return data;
    }

    /// <summary>
    /// Gives the label of a message as the tests compare it: the hello, a catalog or an icon by its short name, a tick by
    /// its game time.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The label</returns>
    private string Label(ContractMessage message)
    {
        return message switch
               {
                   ContractHello => "hello",
                   ContractCatalogUpload upload => "catalog " + _names.GetValueOrDefault(upload.ComputeIdentity(), "unknown"),
                   ContractIconImage icon => "icon " + _names.GetValueOrDefault(icon.Reference, "unknown"),
                   ContractTick tick => "tick " + tick.GameTime.ToString(CultureInfo.InvariantCulture),
                   _ => message.GetType().Name
               };
    }

    /// <summary>
    /// Gives the labels of the received messages.
    /// </summary>
    /// <param name="received">The messages</param>
    /// <returns>The labels in order</returns>
    private string[] Labels(ConcurrentQueue<ContractMessage> received)
    {
        return [.. received.Select(Label)];
    }

    /// <summary>
    /// Returns the messages of the log entries so far.
    /// </summary>
    /// <returns>The messages in order</returns>
    private List<string> Lines()
    {
        return [.. _logger.Entries.Select(entry => entry.Message)];
    }

    /// <summary>
    /// Returns the log lines about the catalog so far.
    /// </summary>
    /// <returns>The lines in order</returns>
    private List<string> CatalogLines()
    {
        return [.. Lines().Where(line => line.StartsWith("Catalog", StringComparison.Ordinal) || line.StartsWith("Server refused the catalog", StringComparison.Ordinal))];
    }

    /// <summary>
    /// Creates a sender for the server of the tests over a new buffer.
    /// </summary>
    /// <returns>The sender</returns>
    private LiveSender CreateSender()
    {
        return CreateSender(CreateRunningBuffer(NewBufferFolder()));
    }

    /// <summary>
    /// Creates a sender for the server of the tests over a buffer.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <returns>The sender</returns>
    private LiveSender CreateSender(LiveBuffer buffer)
    {
        return Track(LiveSender.ForServer(new ForwardServerSetting(_address, Token), buffer, CompanionVersion, _connector, _timeProvider, _logger));
    }

    /// <summary>
    /// Creates a sender for the local server of the tests over a new buffer.
    /// </summary>
    /// <returns>The sender</returns>
    private LiveSender CreateLocalSender()
    {
        return Track(LiveSender.ForLocalServer(LocalPort, _ => Task.FromResult<string?>(Token), CreateRunningBuffer(NewBufferFolder()), CompanionVersion, _connector, _timeProvider, _logger));
    }

    /// <summary>
    /// Disposes a sender after the test.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <returns>The sender</returns>
    private LiveSender Track(LiveSender sender)
    {
        _senders.Add(sender);

        return sender;
    }

    /// <summary>
    /// Gets a new buffer folder below the test's temporary folder, not created yet.
    /// </summary>
    /// <returns>The path</returns>
    private string NewBufferFolder()
    {
        return Path.Combine(_root, Guid.NewGuid().ToString("N"), "Buffer");
    }

    /// <summary>
    /// Creates a buffer with the default limits in a folder and starts its run; both end after the test.
    /// </summary>
    /// <param name="folder">The buffer folder</param>
    /// <returns>The buffer</returns>
    private LiveBuffer CreateRunningBuffer(string folder)
    {
        var buffer = new LiveBuffer(folder, LiveBufferLimits.Defaults, _timeProvider, _bufferLogger);

        _buffers.Add(buffer);
        _bufferRuns.Add(buffer.RunAsync(TestContext.CancellationToken));

        return buffer;
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
    /// Creates a scripted server whose socket the next connection attempt gets and serves it: it accepts the hello naming an
    /// identity and answers every message as the handler says.
    /// </summary>
    /// <param name="identity">The identity of the catalog the server names in the accepted hello, or <see langword="null" /></param>
    /// <param name="handler">The handler that gives the acknowledgement of a message, or <see langword="null" /> for none</param>
    /// <returns>The server and the messages it received, the hello first</returns>
    private (ScriptedIngestServer Server, ConcurrentQueue<ContractMessage> Received) AddServer(string? identity, Func<ScriptedIngestServer, ContractMessage, ContractAcknowledgement?> handler)
    {
        var server = ScriptedIngestServer.Create();
        var received = new ConcurrentQueue<ContractMessage>();

        _servers.Add(server);
        _connector.Enqueue(server.ClientSocket);
        _serving.Add(ServeAsync(server, identity, handler, received));

        return (server, received);
    }

    /// <summary>
    /// Serves a scripted server until its connection ends.
    /// </summary>
    /// <param name="server">The server</param>
    /// <param name="identity">The identity of the catalog the accepted hello names</param>
    /// <param name="handler">The handler that gives the acknowledgement of a message</param>
    /// <param name="received">The messages received</param>
    /// <returns>A task that completes when the connection ended</returns>
    private async Task ServeAsync(ScriptedIngestServer server, string? identity, Func<ScriptedIngestServer, ContractMessage, ContractAcknowledgement?> handler, ConcurrentQueue<ContractMessage> received)
    {
        try
        {
            var hello = await server.AcceptHelloAsync(identity, TestContext.CancellationToken).ConfigureAwait(false);

            if (hello is null)
            {
                return;
            }

            received.Enqueue(hello);

            var message = await server.ReceiveAsync(TestContext.CancellationToken).ConfigureAwait(false);

            while (message is not null)
            {
                received.Enqueue(message);

                if (handler(server, message) is { } acknowledgement)
                {
                    await server.SendAsync(acknowledgement, TestContext.CancellationToken).ConfigureAwait(false);
                }

                message = await server.ReceiveAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // The connection ended with the abort of the server, the sender or the test.
        }
    }

    /// <summary>
    /// Polls a condition, ten milliseconds apart, until it holds or the polls run out, and asserts that it holds.
    /// </summary>
    /// <param name="condition">The condition</param>
    /// <param name="description">What the condition says, as the assert message</param>
    /// <returns>A task that completes when the condition holds</returns>
    private async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var polls = 0;

        while (condition() == false && polls < MaxPolls)
        {
            await Task.Delay(10, TestContext.CancellationToken);

            polls++;
        }

        Assert.IsTrue(condition(), description);
    }

    /// <summary>
    /// Waits until a server received a number of messages.
    /// </summary>
    /// <param name="received">The messages the server received</param>
    /// <param name="count">The number of messages</param>
    /// <returns>A task that completes when the messages arrived</returns>
    private Task WaitForMessagesAsync(ConcurrentQueue<ContractMessage> received, int count)
    {
        return WaitUntilAsync(() => received.Count >= count, $"The server should receive {count} messages, but received: {string.Join(", ", Labels(received))}");
    }

    /// <summary>
    /// Gives the sender a moment to act on what the test did, before the test asserts that it did not act.
    /// </summary>
    /// <returns>A task that completes after the moment</returns>
    private Task SettleAsync()
    {
        return Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.CancellationToken);
    }

    /// <summary>
    /// Waits until a timer of a due time was created a number of times, then advances the clock by that time.
    /// </summary>
    /// <param name="delay">The due time</param>
    /// <param name="occurrence">The number of timers of that due time to wait for</param>
    /// <returns>A task that completes when the clock was advanced</returns>
    private async Task AdvanceRetryAsync(TimeSpan delay, int occurrence)
    {
        while (_timeProvider.Delays.Count(due => due == delay) < occurrence)
        {
            await _timeProvider.WaitForTimersAsync(_timeProvider.Delays.Count + 1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        }

        _timeProvider.Advance(delay);
    }

    /// <summary>
    /// Advances the clock by a step, again and again, until a condition holds.
    /// </summary>
    /// <param name="step">The step</param>
    /// <param name="condition">The condition</param>
    /// <returns>A task that completes when the condition holds</returns>
    private async Task AdvanceUntilAsync(TimeSpan step, Func<bool> condition)
    {
        var steps = 0;

        while (condition() == false && steps < MaxPolls)
        {
            _timeProvider.Advance(step);
            await Task.Delay(10, TestContext.CancellationToken);

            steps++;
        }

        Assert.IsTrue(condition(), "The advancing clock should bring about the condition");
    }

    /// <summary>
    /// Runs the scenario that writes one of the five catalog log lines.
    /// </summary>
    /// <param name="scenario">The scenario</param>
    /// <param name="catalog">The catalog of the test</param>
    /// <returns>A task that completes when the line is written</returns>
    private async Task RunLogScenarioAsync(string scenario, CatalogData catalog)
    {
        var sender = CreateSender();

        switch (scenario)
        {
            case "sent":
                {
                    var served = AddServer(null, Accepting(catalog.References));

                    Offer(sender, catalog);
                    StartRun(sender);
                    await WaitForMessagesAsync(served.Received, 3);
                    await WaitUntilAsync(() => CatalogLines().Count > 0, "The upload should be logged");
                }

                break;

            case "already":
                {
                    var served = AddServer(catalog.Identity, Accepting(catalog.References));

                    Offer(sender, catalog);
                    StartRun(sender);
                    await WaitUntilAsync(() => CatalogLines().Count > 0, "The skip should be logged");
                    await WaitForMessagesAsync(served.Received, 1);
                }

                break;

            case "refused":
                {
                    var served = AddServer(null, (_, message) => message is ContractCatalogUpload ? Reply(message, ContractAcknowledgementRefusal.CatalogInvalid, []) : Ack(message));

                    Offer(sender, catalog);
                    StartRun(sender);
                    await WaitForMessagesAsync(served.Received, 2);
                    await WaitUntilAsync(() => CatalogLines().Count > 0, "The refusal should be logged");
                }

                break;

            case "unwritable":
                {
                    var served = AddServer(_otherIdentity, Accepting(catalog.References));

                    sender.SetCatalog(StatusOf(GameCatalogStatusKind.Ready), catalog.Catalog, catalog.Icons, "not a version");
                    StartRun(sender);
                    await WaitForMessagesAsync(served.Received, 1);
                    await WaitUntilAsync(() => CatalogLines().Count > 0, "The refusal of the writer should be logged");
                }

                break;

            default:
                {
                    var failing = new[] { AddServer(null, Aborting(IsCatalog)), AddServer(null, Aborting(IsCatalog)), AddServer(null, Aborting(IsCatalog)) };

                    Offer(sender, catalog);
                    StartRun(sender);
                    await WaitForMessagesAsync(failing[0].Received, 2);
                    await AdvanceRetryAsync(TimeSpan.FromSeconds(1), 1);
                    await WaitForMessagesAsync(failing[1].Received, 2);
                    await AdvanceRetryAsync(TimeSpan.FromSeconds(2), 1);
                    await WaitForMessagesAsync(failing[2].Received, 2);
                    await WaitUntilAsync(() => CatalogLines().Count > 0, "The give up should be logged");
                }

                break;
        }
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