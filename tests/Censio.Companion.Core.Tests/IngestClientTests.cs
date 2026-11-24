using System.Globalization;
using System.Net.WebSockets;

using Censio.Contracts;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="IngestClient" />.
/// </summary>
[TestClass]
public sealed class IngestClientTests
{
    #region Fields

    /// <summary>
    /// The start time of the time provider.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address of the server.
    /// </summary>
    private static readonly Uri _address = new("ws://catalog-client.example:8080/ingest");

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The clients of the test, disposed afterwards.
    /// </summary>
    private readonly List<IngestClient> _clients = [];

    /// <summary>
    /// The scripted servers of the test, disposed afterwards.
    /// </summary>
    private readonly List<ScriptedIngestServer> _servers = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestClientTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public IngestClientTests(TestContext testContext)
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
    /// Disposes the clients and the servers of the test.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        }

        foreach (var server in _servers)
        {
            await server.DisposeAsync();
        }
    }

    /// <summary>
    /// The acknowledgement of a catalog upload with the largest list of missing icons the contract allows, over one
    /// megabyte, is read and handed to the caller (E7, N3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestClientReadsCatalogAcknowledgementWithMaximalMissingIcons()
    {
        // Arrange
        var (client, server) = await OpenAsync(null);

        var id = Guid.NewGuid();
        var references = Enumerable.Range(0, ContractReader.MaxMissingIcons).Select(index => index.ToString("x64", CultureInfo.InvariantCulture)).ToArray();
        var reply = ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(id, null, references));

        Assert.IsGreaterThan(IngestClient.MaxReplyBytes, reply.Length, "The acknowledgement should be larger than any other reply may be");
        Assert.IsLessThanOrEqualTo(IngestClient.MaxCatalogReplyBytes, reply.Length, "The acknowledgement should fit the bound of a catalog acknowledgement");

        // Act
        var sending = client.SendCatalogUploadAsync(CreateUpload(id), id, TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await server.SendRawAsync(reply, WebSocketMessageType.Text, TestContext.CancellationToken);

        var acknowledgement = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(acknowledgement, "The acknowledgement should be handed to the caller");
        Assert.AreEqual(id, acknowledgement.Id, "The acknowledgement should be that of the upload");
        Assert.HasCount(ContractReader.MaxMissingIcons, acknowledgement.MissingIcons, "Every missing icon should be read");
        Assert.AreSequenceEqual(references, acknowledgement.MissingIcons.ToArray(), "The missing icons should keep their order");
        Assert.IsNull(client.Failure, "The connection should not fail");
    }

    /// <summary>
    /// An acknowledgement of an icon that is larger than 65,536 bytes ends the connection as an invalid reply, also after an
    /// acknowledged catalog upload on the same connection: the larger bound ends with the wait for its acknowledgement
    /// (N3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestClientRefusesLargeIconReplyAfterCatalogUpload()
    {
        // Arrange
        var (client, server) = await OpenAsync(null);

        var uploadId = Guid.NewGuid();
        var data = CatalogTestData.Create("A", 1);
        var icon = new ContractIconImage
                   {
                       Id = Guid.NewGuid(),
                       Reference = data.References[0],
                       Png = data.Pngs[0]
                   };
        var iconBytes = ContractWriter.Write(icon).Utf8Json;
        var reply = PadTo(ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(icon.Id, null)), IngestClient.MaxReplyBytes + 1);

        Assert.IsInstanceOfType<ContractAcknowledgement>(ContractReader.Read(reply).Message, "Apart from its size the reply should be a valid acknowledgement");

        var uploading = client.SendCatalogUploadAsync(CreateUpload(uploadId), uploadId, TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await server.SendAsync(ScriptedIngestServer.CreateAcknowledgement(uploadId, null, [data.References[0]]), TestContext.CancellationToken);

        var uploaded = await uploading.WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        var sending = client.SendAsync(iconBytes.ToArray(), icon.Id, TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var replying = server.SendRawIgnoringFailureAsync(reply, WebSocketMessageType.Text, TestContext.CancellationToken);
        var acknowledgement = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        await replying.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(uploaded, "The catalog upload should be acknowledged");
        Assert.IsNull(acknowledgement, "A reply of 65,537 bytes to an icon should not be handed to the caller");
        Assert.AreEqual(ReplaySendProblemKind.InvalidReply, client.Failure, "The connection should end as an invalid reply");
    }

    /// <summary>
    /// An acknowledgement of a catalog upload larger than the catalog bound ends the connection as an invalid reply (N3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestClientRefusesCatalogReplyAboveCatalogBound()
    {
        // Arrange
        var (client, server) = await OpenAsync(null);

        var id = Guid.NewGuid();
        var references = Enumerable.Range(0, ContractReader.MaxMissingIcons).Select(index => index.ToString("x64", CultureInfo.InvariantCulture)).ToArray();
        var reply = PadTo(ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(id, null, references)), IngestClient.MaxCatalogReplyBytes + 1);

        Assert.IsInstanceOfType<ContractAcknowledgement>(ContractReader.Read(reply).Message, "Apart from its size the reply should be a valid acknowledgement");

        // Act
        var sending = client.SendCatalogUploadAsync(CreateUpload(id), id, TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var replying = server.SendRawIgnoringFailureAsync(reply, WebSocketMessageType.Text, TestContext.CancellationToken);
        var acknowledgement = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        await replying.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(acknowledgement, "A reply above the catalog bound should not be handed to the caller");
        Assert.AreEqual(ReplaySendProblemKind.InvalidReply, client.Failure, "The connection should end as an invalid reply");
    }

    /// <summary>
    /// An acknowledgement larger than 65,536 bytes to a message other than a catalog upload ends the connection as an invalid
    /// reply, with no catalog upload on the connection (N3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IngestClientRefusesLargeReplyToOtherMessage()
    {
        // Arrange
        var (client, server) = await OpenAsync(null);

        var data = CatalogTestData.Create("A", 1);
        var icon = new ContractIconImage
                   {
                       Id = Guid.NewGuid(),
                       Reference = data.References[0],
                       Png = data.Pngs[0]
                   };
        var reply = PadTo(ScriptedIngestServer.Write(ScriptedIngestServer.CreateAcknowledgement(icon.Id, null)), IngestClient.MaxReplyBytes + 1);

        Assert.IsInstanceOfType<ContractAcknowledgement>(ContractReader.Read(reply).Message, "Apart from its size the reply should be a valid acknowledgement");

        // Act
        var sending = client.SendAsync(ContractWriter.Write(icon).Utf8Json.ToArray(), icon.Id, TestContext.CancellationToken);

        await server.ReceiveAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var replying = server.SendRawIgnoringFailureAsync(reply, WebSocketMessageType.Text, TestContext.CancellationToken);
        var acknowledgement = await sending.WaitAsync(_guard, TestContext.CancellationToken);

        await replying.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(acknowledgement, "A reply of 65,537 bytes to a message other than a catalog upload should not be handed to the caller");
        Assert.AreEqual(ReplaySendProblemKind.InvalidReply, client.Failure, "The connection should end as an invalid reply");
    }

    /// <summary>
    /// The identity of the catalog the server names in its accepted hello is kept for the caller, <c>null</c> when it names
    /// none (S1, S2).
    /// </summary>
    /// <param name="identity">The identity the server names</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(null, DisplayName = "No identity")]
    [DataRow("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", DisplayName = "An identity")]
    public async Task IngestClientKeepsCatalogIdentityOfHello(string? identity)
    {
        // Arrange and act
        var (client, _) = await OpenAsync(identity);

        // Assert
        Assert.AreEqual(identity, client.ServerCatalogIdentity, "The client should keep the identity the hello reply named");
    }

    /// <summary>
    /// Writes the catalog upload message of a small catalog.
    /// </summary>
    /// <param name="id">The id of the message</param>
    /// <returns>The bytes of the message</returns>
    private static byte[] CreateUpload(Guid id)
    {
        var data = CatalogTestData.Create("A", 1);
        var upload = new ContractCatalogUpload
                     {
                         Id = id,
                         Document = CatalogTestData.WriteDocument(data.Catalog, data.Icons, data.Build)
                     };

        return ContractWriter.Write(upload).Utf8Json.ToArray();
    }

    /// <summary>
    /// Pads the JSON of a message with white space after its opening brace to a length.
    /// </summary>
    /// <param name="json">The JSON of the message</param>
    /// <param name="length">The length of the padded message</param>
    /// <returns>The padded message, which reads as the message</returns>
    private static byte[] PadTo(byte[] json, int length)
    {
        var padding = length - json.Length;
        var padded = new byte[length];

        padded[0] = json[0];
        Array.Fill(padded, (byte)' ', 1, padding);
        Array.Copy(json, 1, padded, 1 + padding, json.Length - 1);

        return padded;
    }

    /// <summary>
    /// Opens a client against a scripted server that accepts the hello.
    /// </summary>
    /// <param name="identity">The identity of the catalog the server names, or <c>null</c></param>
    /// <returns>The client and the server</returns>
    private async Task<(IngestClient Client, ScriptedIngestServer Server)> OpenAsync(string? identity)
    {
        var connector = new QueuedIngestConnector();
        var server = ScriptedIngestServer.Create();
        var client = new IngestClient(connector, new FakeTimeProvider(_start));
        var hello = new ContractHello
                    {
                        Token = null,
                        CompanionVersion = "1.2.3",
                        ProfileId = LiveSender.ProfileId,
                        ProfileName = LiveSender.ProfileName
                    };

        _servers.Add(server);
        _clients.Add(client);
        connector.Enqueue(server.ClientSocket);

        var opening = client.OpenAsync(_address, hello, TestContext.CancellationToken);

        await server.AcceptHelloAsync(identity, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var problem = await opening.WaitAsync(_guard, TestContext.CancellationToken);

        Assert.IsNull(problem, "The hello should be accepted");

        return (client, server);
    }

    #endregion // Methods
}