using Bunit;

using Censio.Server.Components;

using Microsoft.AspNetCore.Http;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="App" />.
/// </summary>
[TestClass]
public sealed class AppTests
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AppTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public AppTests(TestContext testContext)
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
    /// Without a request the document falls back to the English language and the light theme.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [TestCategory("AC9")]
    public async Task AppRenderWithoutHttpContextShowsEnglishDocumentInLightTheme()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var app = host.Context.Render<App>();

        // Assert
        Assert.AreEqual("en", app.Find("html").GetAttribute("lang"), "The document language should be English without a request");
        Assert.HasCount(1, app.FindAll("button[aria-label='Switch to the dark theme']"), "The frame should show the light theme's English theme switch without a request");
    }

    /// <summary>
    /// The document passes to the router whether the request's host may delete, by the host rule over the request's host
    /// (without its port) and the setting <c>AllowedHosts</c>.
    /// </summary>
    /// <param name="requestHost">The host of the request, with a port where given</param>
    /// <param name="allowedHosts">The setting <c>AllowedHosts</c>, or <see langword="null" /> when unset</param>
    /// <param name="expected">Whether the router should be told that the page may delete</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("127.0.0.1:8080", null, true)]
    [DataRow("[::1]:8080", "*", true)]
    [DataRow("localhost", "*", true)]
    [DataRow("nas:8080", "nas;127.0.0.1", true)]
    [DataRow("evil.example:8080", "*", false)]
    [DataRow("nas", null, false)]
    public async Task AppPassesDeletionAllowedOfRequestHost(string requestHost, string? allowedHosts, bool expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.Configuration["AllowedHosts"] = allowedHosts;

        var httpContext = new DefaultHttpContext();

        httpContext.Request.Host = new HostString(requestHost);

        // Act
        var app = host.Context.Render<App>(parameters => parameters.AddCascadingValue<HttpContext>(httpContext));

        // Assert
        Assert.AreEqual(expected, app.FindComponent<Routes>().Instance.DeletionAllowed, $"The router should be told whether the host '{requestHost}' may delete");
    }

    #endregion // Methods
}