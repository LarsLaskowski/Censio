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

    /// <summary>
    /// The document passes the chart choice of the request's cookies to the router; an absent or unknown cookie value gives
    /// the default of its part.
    /// </summary>
    /// <param name="storedRange">The cookie of the range, or <see langword="null" /> for none</param>
    /// <param name="storedAxis">The cookie of the axis, or <see langword="null" /> for none</param>
    /// <param name="expectedRange">The range the router should be told</param>
    /// <param name="expectedAxis">The axis the router should be told</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("week", "game", ChartRange.LastWeek, ChartAxis.GameTime)]
    [DataRow("hour", "real", ChartRange.LastHour, ChartAxis.RealTime)]
    [DataRow("all", null, ChartRange.All, ChartAxis.RealTime)]
    [DataRow(null, "game", ChartRange.LastDay, ChartAxis.GameTime)]
    [DataRow("Week", "GAME", ChartRange.LastDay, ChartAxis.RealTime)]
    [DataRow("%3Cb%3E", "x", ChartRange.LastDay, ChartAxis.RealTime)]
    [DataRow(null, null, ChartRange.LastDay, ChartAxis.RealTime)]
    public async Task AppPassesStoredChartChoiceOfRequest(string? storedRange, string? storedAxis, ChartRange expectedRange, ChartAxis expectedAxis)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        var httpContext = new DefaultHttpContext();
        var cookies = new[] { (UiPreferences.ChartRangeCookieName, storedRange), (UiPreferences.ChartAxisCookieName, storedAxis) }.Where(cookie => cookie.Item2 is not null).Select(cookie => $"{cookie.Item1}={cookie.Item2}");

        httpContext.Request.Headers.Cookie = string.Join("; ", cookies);

        // Act
        var app = host.Context.Render<App>(parameters => parameters.AddCascadingValue<HttpContext>(httpContext));

        // Assert
        var expected = new ChartChoice
                       {
                           Range = expectedRange,
                           Axis = expectedAxis
                       };

        Assert.AreEqual(expected, app.FindComponent<Routes>().Instance.ChartChoice, "The router should be told the stored choice, or the default of each part that is absent or unknown");
    }

    #endregion // Methods
}