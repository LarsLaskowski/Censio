using Bunit;

using Censio.Server.Components;

using MudBlazor.Services;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="App" />.
/// </summary>
[TestClass]
public sealed class AppTests
{
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
        await using var context = new BunitContext();

        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        // Act
        var app = context.Render<App>();

        // Assert
        Assert.AreEqual("en", app.Find("html").GetAttribute("lang"), "The document language should be English without a request");
        Assert.HasCount(1, app.FindAll("button[aria-label='Switch to the dark theme']"), "The frame should show the light theme's English theme switch without a request");
    }

    #endregion // Methods
}