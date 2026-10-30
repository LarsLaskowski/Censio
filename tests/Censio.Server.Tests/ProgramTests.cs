using System.Text.RegularExpressions;

using Censio.Contracts;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the HTTP surface of <see cref="Program" />.
/// </summary>
[TestClass]
public sealed class ProgramTests
{
    #region Fields

    /// <summary>
    /// The pattern of the Blazor script element on the start page.
    /// </summary>
    private static readonly Regex _blazorScript = new("<script[^>]*\\ssrc=\"([^\"]*_framework/blazor\\.web(\\.[A-Za-z0-9]+)?\\.js)\"", RegexOptions.None, TimeSpan.FromSeconds(5));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgramTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ProgramTests(TestContext testContext)
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
    /// The start page carries the title, the heading and the version.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramGetRootReturnsStartPageWithTitleHeadingAndVersion()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        var expectedVersion = ProductVersion.Read(typeof(Program).Assembly);

        // Act
        using var response = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The start page should answer 200");
        Assert.IsTrue(Regex.IsMatch(body, "<title[^>]*>Censio</title>", RegexOptions.None, TimeSpan.FromSeconds(5)), "The title element should hold the text Censio");
        Assert.IsTrue(Regex.IsMatch(body, "<h1[^>]*>Censio</h1>", RegexOptions.None, TimeSpan.FromSeconds(5)), "The h1 element should hold the text Censio");
        Assert.Contains("Version " + expectedVersion, body, "The start page should show the version of the server assembly");
    }

    /// <summary>
    /// The script the start page references is served.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramGetRootReferencesServedBlazorScript()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        using var page = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await page.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var match = _blazorScript.Match(body);

        // Act
        using var script = await client.GetAsync(match.Groups[1].Value, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(match.Success, "The start page should reference _framework/blazor.web.js, fingerprinted or not");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, script.StatusCode, "The referenced Blazor script should answer 200");
    }

    /// <summary>
    /// The start page contains the marker of an interactive server component.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramGetRootRendersInteractiveServerMarker()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.Contains("<!--Blazor:{\"type\":\"server\"", body, "The page should contain the prerender marker of the interactive server render mode");
    }

    /// <summary>
    /// The health endpoint answers Healthy as plain text.
    /// </summary>
    /// <param name="environment">The hosting environment</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [DataRow("Production")]
    [DataRow("Development")]
    public async Task ProgramGetHealthReturnsHealthyPlainText(string environment)
    {
        // Arrange
        using var factory = new CensioServerFactory(environment);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The health endpoint should answer 200");
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType, "The health endpoint should answer text/plain");
        Assert.AreEqual("Healthy", body, "The health endpoint should answer exactly Healthy");
    }

    /// <summary>
    /// A request over HTTP is not redirected to HTTPS, even when an HTTPS port is known.
    /// </summary>
    /// <param name="path">The requested path</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [DataRow("/")]
    [DataRow("/health")]
    public async Task ProgramGetOverHttpWithHttpsPortDoesNotRedirect(string path)
    {
        // Arrange
        var settings = new Dictionary<string, string?>
                       {
                           ["https_port"] = "443"
                       };
        using var factory = new CensioServerFactory("Production", settings);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                                                {
                                                    AllowAutoRedirect = false,
                                                    BaseAddress = new Uri("http://censio.example")
                                                });

        // Act
        using var response = await client.GetAsync(path, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The request over HTTP should be answered, not redirected");
        Assert.IsNull(response.Headers.Location, "The answer should carry no Location header");
    }

    /// <summary>
    /// A request over HTTPS gets no HSTS header.
    /// </summary>
    /// <param name="path">The requested path</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [DataRow("/")]
    [DataRow("/health")]
    public async Task ProgramGetOverHttpsSendsNoHstsHeader(string path)
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                                                {
                                                    AllowAutoRedirect = false,
                                                    BaseAddress = new Uri("https://censio.example")
                                                });

        // Act
        using var response = await client.GetAsync(path, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The request over HTTPS should be answered");
        Assert.IsFalse(response.Headers.Contains("Strict-Transport-Security"), "The answer should carry no Strict-Transport-Security header");
    }

    /// <summary>
    /// An unknown path answers 404 with an empty body.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramGetUnknownPathReturnsNotFound()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/does-not-exist", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode, "An unknown path should answer 404");
        Assert.AreEqual(string.Empty, body, "The 404 answer should have an empty body");
    }

    /// <summary>
    /// The committed settings files set no listen address.
    /// </summary>
    /// <param name="environment">The hosting environment</param>
    [TestMethod]
    [DataRow("Production")]
    [DataRow("Development")]
    public void ProgramProductionConfigurationSetsNoListenAddress(string environment)
    {
        // Arrange
        // Only the JSON sources are read (the committed appsettings*.json), never environment variables, which machines set.
        // That the server listens on loopback by default is not proven here but by starting the built server.
        using var factory = new CensioServerFactory(environment);
        var root = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();
        var forbidden = new[] { "urls", "http_ports", "https_ports" };

        // Act
        var jsonProviders = root.Providers.OfType<JsonConfigurationProvider>().ToList();
        var keys = jsonProviders.SelectMany(provider => provider.GetChildKeys([], null)).ToList();
        var endpoints = jsonProviders.Where(provider => provider.GetChildKeys([], "Kestrel").Contains("Endpoints", StringComparer.OrdinalIgnoreCase)).ToList();

        // Assert
        Assert.IsNotEmpty(jsonProviders, "The committed settings files should be loaded as JSON sources");

        foreach (var key in forbidden)
        {
            Assert.DoesNotContain(keys.Select(item => item.ToLowerInvariant()).ToList(), key.ToLowerInvariant(), $"The committed settings should not set {key}");
        }

        Assert.IsEmpty(endpoints, "The committed settings should not set Kestrel:Endpoints");
    }

    #endregion // Methods
}