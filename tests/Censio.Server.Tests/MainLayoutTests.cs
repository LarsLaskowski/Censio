using System.Globalization;

using Bunit;
using Bunit.TestDoubles;

using Censio.Server.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using MudBlazor;
using MudBlazor.Services;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="MainLayout" />.
/// </summary>
[TestClass]
public sealed class MainLayoutTests
{
    #region Constants

    /// <summary>
    /// The browser function that stores a preference.
    /// </summary>
    private const string StorePreference = "censio.storePreference";

    /// <summary>
    /// The browser function that sets the document's language.
    /// </summary>
    private const string SetDocumentLanguage = "censio.setDocumentLanguage";

    /// <summary>
    /// The accessible name of the English navigation button.
    /// </summary>
    private const string EnglishNavigationButton = "Open or close the navigation";

    /// <summary>
    /// The accessible name of the English theme switch in the light theme.
    /// </summary>
    private const string EnglishSwitchToDark = "Switch to the dark theme";

    /// <summary>
    /// The accessible name of the English theme switch in the dark theme.
    /// </summary>
    private const string EnglishSwitchToLight = "Switch to the light theme";

    /// <summary>
    /// The accessible name of the English language switch.
    /// </summary>
    private const string EnglishLanguageSwitch = "Language";

    /// <summary>
    /// The accessible name of the German language switch.
    /// </summary>
    private const string GermanLanguageSwitch = "Sprache";

    /// <summary>
    /// The class of the drawer element.
    /// </summary>
    private const string DrawerSelector = ".mud-drawer";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The theme switch turns the theme over without a navigation, and its accessible name follows the theme shown.
    /// </summary>
    /// <param name="startTheme">The theme the page starts with</param>
    /// <param name="language">The language shown</param>
    /// <param name="switchLabel">The accessible name of the theme switch before the click</param>
    /// <param name="expectedTheme">The theme expected after the click</param>
    /// <param name="expectedLabel">The accessible name of the theme switch after the click</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(UiTheme.Light, UiLanguage.English, EnglishSwitchToDark, UiTheme.Dark, EnglishSwitchToLight)]
    [DataRow(UiTheme.Dark, UiLanguage.English, EnglishSwitchToLight, UiTheme.Light, EnglishSwitchToDark)]
    [DataRow(UiTheme.Light, UiLanguage.German, "Zum dunklen Design wechseln", UiTheme.Dark, "Zum hellen Design wechseln")]
    [DataRow(UiTheme.Dark, UiLanguage.German, "Zum hellen Design wechseln", UiTheme.Light, "Zum dunklen Design wechseln")]
    public async Task MainLayoutThemeSwitchTogglesDarkModeWithoutReload(UiTheme startTheme, UiLanguage language, string switchLabel, UiTheme expectedTheme, string expectedLabel)
    {
        // Arrange
        await using var context = CreateContext();
        var layout = RenderLayout(context, startTheme, language);

        // Act
        await layout.Find(ByLabel(switchLabel)).ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains(PaletteBackground(expectedTheme), layout.Markup, "The theme switch should show the palette of the other theme");
        Assert.DoesNotContain(PaletteBackground(startTheme), layout.Markup, "The theme switch should leave the palette of the start theme");
        Assert.HasCount(1, layout.FindAll(ByLabel(expectedLabel)), $"The theme switch should be named '{expectedLabel}' after the click");
        Assert.IsEmpty(context.Services.GetRequiredService<BunitNavigationManager>().History, "The theme switch should not navigate or reload");
    }

    /// <summary>
    /// The theme switch asks the browser to store the theme now shown.
    /// </summary>
    /// <param name="startTheme">The theme the page starts with</param>
    /// <param name="switchLabel">The accessible name of the theme switch</param>
    /// <param name="expectedValue">The stored value</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(UiTheme.Light, EnglishSwitchToDark, "dark")]
    [DataRow(UiTheme.Dark, EnglishSwitchToLight, "light")]
    public async Task MainLayoutThemeSwitchStoresChoice(UiTheme startTheme, string switchLabel, string expectedValue)
    {
        // Arrange
        await using var context = CreateContext();
        var layout = RenderLayout(context, startTheme, UiLanguage.English);

        // Act
        await layout.Find(ByLabel(switchLabel)).ClickAsync(new MouseEventArgs());

        // Assert
        var invocations = context.JSInterop.Invocations[StorePreference];

        Assert.HasCount(1, invocations, "The theme switch should store the choice once");
        Assert.AreSequenceEqual(new object[] { UiPreferences.ThemeCookieName, expectedValue }, invocations[0].Arguments.ToArray(), "The theme switch should store the theme cookie with the theme shown");
    }

    /// <summary>
    /// When the browser cannot store the theme, the page keeps the new theme and logs only the kind of the choice.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task MainLayoutThemeSwitchWhenStoreFailsKeepsThemeAndLogsKindOnly()
    {
        // Arrange
        await using var context = CreateContext();
        var loggerProvider = new Censio.Server.Data.Tests.RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Trace));

        context.Services.AddSingleton(loggerFactory);
        context.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        context.JSInterop.SetupVoid(StorePreference, _ => true).SetException(new JSException("The browser refused the cookie dark"));

        var layout = RenderLayout(context, UiTheme.Light, UiLanguage.English);

        // Act
        await layout.Find(ByLabel(EnglishSwitchToDark)).ClickAsync(new MouseEventArgs());

        // Assert
        var entries = loggerProvider.Logger.Entries;
        var choiceEntries = entries.Where(entry => entry.Message == "The browser did not store the theme choice.").ToList();

        Assert.Contains(PaletteBackground(UiTheme.Dark), layout.Markup, "The page should keep the dark theme although it was not stored");
        Assert.HasCount(1, choiceEntries, "The failed store should be logged exactly once");
        Assert.AreEqual(LogLevel.Debug, choiceEntries[0].Level, "The failed store should be logged at Debug");
        Assert.IsNull(choiceEntries[0].Exception, "The log entry should not carry the exception");
        Assert.IsEmpty(entries.Where(entry => entry.Message.Contains("dark", StringComparison.Ordinal)), "No log entry should hold the chosen value");
    }

    /// <summary>
    /// The language switch changes every text of the frame and the document's language without a navigation.
    /// </summary>
    /// <param name="startLanguage">The language the page starts with</param>
    /// <param name="menuLabel">The accessible name of the language switch before the choice</param>
    /// <param name="option">The option chosen</param>
    /// <param name="expectedLanguage">The language expected after the choice</param>
    /// <param name="expectedTag">The document language expected after the choice</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, EnglishLanguageSwitch, "Deutsch", UiLanguage.German, "de")]
    [DataRow(UiLanguage.German, GermanLanguageSwitch, "English", UiLanguage.English, "en")]
    public async Task MainLayoutLanguageSwitchChangesTextsAndDocumentLanguage(UiLanguage startLanguage, string menuLabel, string option, UiLanguage expectedLanguage, string expectedTag)
    {
        // Arrange
        await using var context = CreateContext();
        var layout = RenderLayout(context, UiTheme.Light, startLanguage);
        var expected = SpecTexts(expectedLanguage);

        // Act
        await ChooseLanguageAsync(layout, menuLabel, option);

        // Assert
        var documentLanguage = context.JSInterop.Invocations[SetDocumentLanguage];

        Assert.HasCount(1, layout.FindAll(ByLabel(expected.NavigationButton)), $"The navigation button should be named '{expected.NavigationButton}'");
        Assert.AreEqual(expected.HomeEntry, layout.Find(".mud-nav-link").TextContent.Trim(), "The start page's entry should be in the chosen language");
        Assert.AreEqual(expected.ProfileSwitch, layout.Find("button[disabled]").TextContent.Trim(), "The profile switch should be in the chosen language");
        Assert.HasCount(1, layout.FindAll(ByLabel(expected.SwitchToDarkTheme)), $"The theme switch should be named '{expected.SwitchToDarkTheme}'");
        Assert.HasCount(1, layout.FindAll(ByLabel(expected.LanguageSwitch)), $"The language switch should be named '{expected.LanguageSwitch}'");
        Assert.HasCount(1, documentLanguage, "The document language should be set once");
        Assert.AreSequenceEqual(new object[] { expectedTag }, documentLanguage[0].Arguments.ToArray(), "The document language should name the chosen language");
        Assert.IsEmpty(context.Services.GetRequiredService<BunitNavigationManager>().History, "The language switch should not navigate or reload");
    }

    /// <summary>
    /// The language switch asks the browser to store the language chosen.
    /// </summary>
    /// <param name="startLanguage">The language the page starts with</param>
    /// <param name="menuLabel">The accessible name of the language switch</param>
    /// <param name="option">The option chosen</param>
    /// <param name="expectedValue">The stored value</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, EnglishLanguageSwitch, "Deutsch", "de")]
    [DataRow(UiLanguage.German, GermanLanguageSwitch, "English", "en")]
    public async Task MainLayoutLanguageSwitchStoresChoice(UiLanguage startLanguage, string menuLabel, string option, string expectedValue)
    {
        // Arrange
        await using var context = CreateContext();
        var layout = RenderLayout(context, UiTheme.Light, startLanguage);

        // Act
        await ChooseLanguageAsync(layout, menuLabel, option);

        // Assert
        var invocations = context.JSInterop.Invocations[StorePreference];

        Assert.HasCount(1, invocations, "The language switch should store the choice once");
        Assert.AreSequenceEqual(new object[] { UiPreferences.LanguageCookieName, expectedValue }, invocations[0].Arguments.ToArray(), "The language switch should store the language cookie with the language chosen");
    }

    /// <summary>
    /// The page inside the frame receives the language and the texts shown, not the language the page started with.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task MainLayoutLanguageSwitchCascadesCurrentLanguageToPage()
    {
        // Arrange
        await using var context = CreateContext();
        var layout = RenderLayout(context, UiTheme.Light, UiLanguage.English, RenderProbe);

        // Act
        await ChooseLanguageAsync(layout, EnglishLanguageSwitch, "Deutsch");

        // Assert
        Assert.AreEqual(nameof(UiLanguage.German), layout.Find(".probe-language").TextContent, "The page should receive the language shown");
        Assert.AreEqual("Startseite", layout.Find(".probe-home-entry").TextContent, "The page should receive the texts of the language shown");
    }

    /// <summary>
    /// The profile switch is disabled and shows only its label, without any profile service registered.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expectedLabel">The label of the spec's table</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(UiLanguage.English, "Profile")]
    [DataRow(UiLanguage.German, "Profil")]
    public async Task MainLayoutProfileSwitchIsDisabledWithLabelOnly(UiLanguage language, string expectedLabel)
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var layout = RenderLayout(context, UiTheme.Light, language);

        // Assert
        var disabledButtons = layout.FindAll("button[disabled]");

        Assert.HasCount(1, disabledButtons, "The profile switch should be the one disabled button");
        Assert.AreEqual(expectedLabel, disabledButtons[0].TextContent.Trim(), "The profile switch should show only its label");
    }

    /// <summary>
    /// The navigation starts open; the navigation button closes it and opens it again.
    /// </summary>
    /// <param name="clicks">The number of clicks on the navigation button</param>
    /// <param name="expectedState">The class of the drawer's state after the clicks</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(1, "mud-drawer--closed")]
    [DataRow(2, "mud-drawer--open")]
    public async Task MainLayoutNavigationButtonClosesAndOpensDrawer(int clicks, string expectedState)
    {
        // Arrange
        await using var context = CreateContext();
        var layout = RenderLayout(context, UiTheme.Light, UiLanguage.English);

        // Act
        await ClickNavigationButtonAsync(layout, clicks);

        // Assert
        Assert.Contains(expectedState, layout.Find(DrawerSelector).ClassList, $"The drawer should carry {expectedState} after {clicks} click(s)");
    }

    /// <summary>
    /// Creates a bUnit context with MudBlazor's services and loose JavaScript interop.
    /// </summary>
    /// <returns>The context</returns>
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();

        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        return context;
    }

    /// <summary>
    /// Renders the layout with the cascaded start values and a page body.
    /// </summary>
    /// <param name="context">The bUnit context</param>
    /// <param name="theme">The theme the page starts with</param>
    /// <param name="language">The language the page starts with</param>
    /// <param name="body">The page body, or <c>null</c> for a plain text</param>
    /// <returns>The rendered layout</returns>
    private static IRenderedComponent<MainLayout> RenderLayout(BunitContext context, UiTheme theme, UiLanguage language, RenderFragment? body = null)
    {
        return context.Render<MainLayout>(parameters => parameters.AddCascadingValue(theme)
                                                                  .AddCascadingValue(language)
                                                                  .Add(layout => layout.Body, body ?? RenderPlainBody));
    }

    /// <summary>
    /// Renders a plain page body.
    /// </summary>
    /// <param name="builder">The render tree builder</param>
    private static void RenderPlainBody(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.AddContent(0, "Page");
    }

    /// <summary>
    /// Renders the probe that shows the cascaded language as the page body.
    /// </summary>
    /// <param name="builder">The render tree builder</param>
    private static void RenderProbe(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadedLanguageProbe>(0);
        builder.CloseComponent();
    }

    /// <summary>
    /// Opens the language switch and chooses an option.
    /// </summary>
    /// <param name="layout">The rendered layout</param>
    /// <param name="menuLabel">The accessible name of the language switch</param>
    /// <param name="option">The option chosen</param>
    /// <returns>A task that completes when the option is chosen</returns>
    private static async Task ChooseLanguageAsync(IRenderedComponent<MainLayout> layout, string menuLabel, string option)
    {
        await layout.Find(ByLabel(menuLabel)).ClickAsync(new MouseEventArgs());
        await layout.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == option).ClickAsync(new MouseEventArgs());
    }

    /// <summary>
    /// Clicks the navigation button a number of times.
    /// </summary>
    /// <param name="layout">The rendered layout</param>
    /// <param name="clicks">The number of clicks</param>
    /// <returns>A task that completes after the last click</returns>
    private static async Task ClickNavigationButtonAsync(IRenderedComponent<MainLayout> layout, int clicks)
    {
        for (var click = 0; click < clicks; click++)
        {
            await layout.Find(ByLabel(EnglishNavigationButton)).ClickAsync(new MouseEventArgs());
        }
    }

    /// <summary>
    /// Gets the declaration of the background variable the theme provider writes for a theme's palette.
    /// </summary>
    /// <param name="theme">The theme</param>
    /// <returns>The declaration</returns>
    private static string PaletteBackground(UiTheme theme)
    {
        var mudTheme = CensioTheme.Create();
        var color = theme == UiTheme.Dark ? mudTheme.PaletteDark.Background : mudTheme.PaletteLight.Background;

        return string.Create(CultureInfo.InvariantCulture, $"--mud-palette-background: rgba({color.R},{color.G},{color.B},1)");
    }

    /// <summary>
    /// Gets the selector of a button by its accessible name.
    /// </summary>
    /// <param name="label">The accessible name</param>
    /// <returns>The selector</returns>
    private static string ByLabel(string label)
    {
        return $"button[aria-label='{label}']";
    }

    /// <summary>
    /// Gets the texts of the spec's table for a language.
    /// </summary>
    /// <param name="language">The language</param>
    /// <returns>
    /// The navigation button, start page entry, profile switch, theme switch in the light theme and language
    /// switch texts
    /// </returns>
    private static (string NavigationButton, string HomeEntry, string ProfileSwitch, string SwitchToDarkTheme, string LanguageSwitch) SpecTexts(UiLanguage language)
    {
        return language == UiLanguage.German
                   ? ("Navigation öffnen oder schließen", "Startseite", "Profil", "Zum dunklen Design wechseln", GermanLanguageSwitch)
                   : (EnglishNavigationButton, "Home", "Profile", EnglishSwitchToDark, EnglishLanguageSwitch);
    }

    #endregion // Methods
}