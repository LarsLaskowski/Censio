using System.Text.RegularExpressions;

using Censio.Contracts;

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

    #endregion // Methods
}