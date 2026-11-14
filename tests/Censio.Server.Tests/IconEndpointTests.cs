using System.Net;
using System.Security.Cryptography;
using System.Text;

using Censio.Contracts;
using Censio.GameData;
using Censio.Server.Data;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IconEndpoint" />, over a test server that maps only the icon route on a state over an in-memory
/// database.
/// </summary>
[TestClass]
public sealed class IconEndpointTests : IAsyncDisposable
{
    #region Fields

    /// <summary>
    /// The disposables of the running test.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

    /// <summary>
    /// The started applications of the running test.
    /// </summary>
    private readonly List<WebApplication> _applications = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IconEndpointTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public IconEndpointTests(TestContext testContext)
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
    /// A stored PNG icon is served with its bytes, as a PNG, not to be sniffed, cached for a year as immutable, and under a
    /// content security policy that allows nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IconEndpointServesStoredPng()
    {
        // Arrange
        var (state, server) = await StartAsync();

        var png = PngWriter.Write(2, 2, new byte[16]);
        var reference = await StoreIconAsync(state, png);

        using var client = server.CreateClient();

        // Act
        using var response = await client.GetAsync(IconUri(reference), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "The stored PNG should be served");
        Assert.AreSequenceEqual(png, await response.Content.ReadAsByteArrayAsync(TestContext.CancellationToken), "The body should be the stored bytes");
        Assert.AreEqual("image/png", response.Content.Headers.NonValidated["Content-Type"].ToString(), "The icon should be served as a PNG");
        Assert.AreEqual("nosniff", response.Headers.NonValidated["X-Content-Type-Options"].ToString(), "The browser should not sniff the type");
        Assert.AreEqual("public, max-age=31536000, immutable", response.Headers.NonValidated["Cache-Control"].ToString(), "The icon, named by its content hash, should be cached as immutable");
        Assert.AreEqual("default-src 'none'; sandbox", response.Headers.NonValidated["Content-Security-Policy"].ToString(), "The icon should be served under a policy that allows nothing");
    }

    /// <summary>
    /// A stored icon that is no PNG is answered 404 with an empty body.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IconEndpointRefusesNonPng()
    {
        // Arrange
        var (state, server) = await StartAsync();

        var reference = await StoreIconAsync(state, Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"));

        using var client = server.CreateClient();

        // Act
        using var response = await client.GetAsync(IconUri(reference), TestContext.CancellationToken);

        // Assert
        await AssertEmptyNotFoundAsync(response, "A stored icon that is no PNG");
    }

    /// <summary>
    /// A reference that is not 64 lowercase hex characters is answered 404 with an empty body, even when it names a stored
    /// icon apart from its form.
    /// </summary>
    /// <param name="form">
    /// The form of the reference: a stored reference shortened, lengthened, in uppercase, or with a
    /// character that is no hex digit at position 64
    /// </param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("63 characters")]
    [DataRow("65 characters")]
    [DataRow("64 uppercase hex")]
    [DataRow("no hex digit at position 64")]
    public async Task IconEndpointRefusesMalformedReference(string form)
    {
        // Arrange
        var (state, server) = await StartAsync();

        var reference = await StoreIconAsync(state, PngWriter.Write(1, 1, new byte[4]));

        using var client = server.CreateClient();

        // Act
        using var response = await client.GetAsync(IconUri(Malform(reference, form)), TestContext.CancellationToken);

        // Assert
        await AssertEmptyNotFoundAsync(response, $"A reference of {form}");
    }

    /// <summary>
    /// A well-formed reference of no stored icon is answered 404 with an empty body.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IconEndpointUnknownReferenceIsNotFound()
    {
        // Arrange
        var (_, server) = await StartAsync();

        using var client = server.CreateClient();

        // Act
        using var response = await client.GetAsync(IconUri(Reference(PngWriter.Write(1, 1, new byte[4]))), TestContext.CancellationToken);

        // Assert
        await AssertEmptyNotFoundAsync(response, "An unknown reference");
    }

    /// <summary>
    /// A request aborted before its read is not served: the route passes the request's abort to the read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IconEndpointAbortedRequestCancelsRead()
    {
        // Arrange
        var (state, server) = await StartAsync();

        var reference = await StoreIconAsync(state, PngWriter.Write(1, 1, new byte[4]));

        using var aborted = new CancellationTokenSource();

        await aborted.CancelAsync();

        // Act
        var outcome = await SendAbortedAsync(server, reference, aborted.Token);

        // Assert
        Assert.AreEqual("not served", outcome, "An aborted request should not be served the icon");
    }

    /// <summary>
    /// Gives the address of an icon.
    /// </summary>
    /// <param name="reference">The reference</param>
    /// <returns>The relative address</returns>
    private static Uri IconUri(string reference)
    {
        return new Uri("/icons/" + reference, UriKind.Relative);
    }

    /// <summary>
    /// Gives the reference of PNG bytes: their SHA-256 in lowercase hex.
    /// </summary>
    /// <param name="png">The bytes</param>
    /// <returns>The reference</returns>
    private static string Reference(byte[] png)
    {
        return Convert.ToHexStringLower(SHA256.HashData(png));
    }

    /// <summary>
    /// Gives a stored reference in a malformed form.
    /// </summary>
    /// <param name="reference">The stored reference</param>
    /// <param name="form">The form</param>
    /// <returns>The malformed reference</returns>
    private static string Malform(string reference, string form)
    {
        return form switch
               {
                   "63 characters" => reference[..63],
                   "65 characters" => reference + "0",
                   "64 uppercase hex" => reference.ToUpperInvariant(),
                   _ => reference[..63] + "g"
               };
    }

    /// <summary>
    /// Asserts that a response is a 404 with an empty body.
    /// </summary>
    /// <param name="response">The response</param>
    /// <param name="what">What was requested, for the messages</param>
    /// <returns>A task that represents the asynchronous assertion</returns>
    private async Task AssertEmptyNotFoundAsync(HttpResponseMessage response, string what)
    {
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"{what} should be answered 404");
        Assert.IsEmpty(await response.Content.ReadAsByteArrayAsync(TestContext.CancellationToken), $"{what} should get an empty body");
    }

    /// <summary>
    /// Sends a request for an icon whose abort token is already cancelled, and describes whether the icon was served.
    /// </summary>
    /// <param name="server">The test server</param>
    /// <param name="reference">The reference</param>
    /// <param name="abortToken">The cancelled abort token of the request</param>
    /// <returns><c>not served</c>, or the status and body length of a served response</returns>
    private async Task<string> SendAbortedAsync(TestServer server, string reference, CancellationToken abortToken)
    {
        try
        {
            var context = await server.SendAsync(context =>
                                                 {
                                                     context.Request.Method = HttpMethods.Get;
                                                     context.Request.Path = "/icons/" + reference;
                                                     context.RequestAborted = abortToken;
                                                 },
                                                 TestContext.CancellationToken);

            using var body = new MemoryStream();

            await context.Response.Body.CopyToAsync(body, TestContext.CancellationToken);

            return context.Response.StatusCode != StatusCodes.Status200OK && body.Length == 0
                       ? "not served"
                       : $"served {context.Response.StatusCode} with {body.Length} bytes";
        }
        catch (OperationCanceledException)
        {
            return "not served";
        }
    }

    /// <summary>
    /// Stores an icon of bytes under their reference through the state.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="png">The bytes</param>
    /// <returns>The reference</returns>
    private async Task<string> StoreIconAsync(ServerState state, byte[] png)
    {
        var reference = Reference(png);
        var stored = await state.StoreIconAsync(new ContractIconImage
                                                {
                                                    Id = Guid.NewGuid(),
                                                    Reference = reference,
                                                    Png = png
                                                },
                                                TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Stored, stored.Outcome, "The test icon should be stored");

        return reference;
    }

    /// <summary>
    /// Starts a test server that maps the icon route on a restored state over an in-memory database.
    /// </summary>
    /// <returns>The state and the test server</returns>
    private async Task<(ServerState State, TestServer Server)> StartAsync()
    {
        var opened = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = opened.Database ?? throw new InvalidOperationException("The in-memory database should open");

        _disposables.Add(database);

        var state = new ServerState(new ServerStore(database), NullLogger.Instance);

        await state.RestoreAsync(TestContext.CancellationToken);

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        var app = builder.Build();

        _applications.Add(app);
        app.MapCensioIcons(state);
        await app.StartAsync(TestContext.CancellationToken);

        return (state, app.GetTestServer());
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var app in _applications)
        {
            await app.DisposeAsync();
        }

        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        SqliteConnection.ClearAllPools();
    }

    #endregion // IAsyncDisposable
}