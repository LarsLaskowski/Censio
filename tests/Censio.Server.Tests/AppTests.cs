using Bunit;

using Censio.Server.Components;

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

    #endregion // Methods
}