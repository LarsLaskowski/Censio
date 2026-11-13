using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

namespace Censio.Server.Components.Layout;

/// <summary>
/// The frame of every page of the web UI.
/// </summary>
public partial class MainLayout
{
    #region Fields

    /// <summary>
    /// The MudBlazor theme.
    /// </summary>
    private readonly MudTheme _mudTheme = CensioTheme.Create();

    /// <summary>
    /// The theme shown.
    /// </summary>
    private UiTheme _theme;

    /// <summary>
    /// The language shown.
    /// </summary>
    private UiLanguage _language;

    /// <summary>
    /// The texts of the language shown.
    /// </summary>
    private UiTexts _texts = UiTexts.For(UiLanguage.English);

    /// <summary>
    /// A value that indicates whether the navigation is open.
    /// </summary>
    private bool _drawerOpen = true;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the theme the page starts with.
    /// </summary>
    [CascadingParameter]
    public UiTheme InitialTheme { get; set; }

    /// <summary>
    /// Gets or sets the language the page starts with.
    /// </summary>
    [CascadingParameter]
    public UiLanguage InitialLanguage { get; set; }

    /// <summary>
    /// Gets or sets the JavaScript runtime.
    /// </summary>
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    [Inject]
    private ILogger<MainLayout> Logger { get; set; } = null!;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Closes or opens the navigation.
    /// </summary>
    private void ToggleDrawer()
    {
        _drawerOpen = _drawerOpen == false;
    }

    /// <summary>
    /// Switches the theme and asks the browser to store it.
    /// </summary>
    /// <returns>A task that completes when the browser answered</returns>
    private async Task ToggleThemeAsync()
    {
        _theme = _theme == UiTheme.Dark ? UiTheme.Light : UiTheme.Dark;

        await StoreAsync("theme", "censio.storePreference", UiPreferences.ThemeCookieName, UiPreferences.StoredValue(_theme)).ConfigureAwait(true);
    }

    /// <summary>
    /// Switches the language and asks the browser to store it.
    /// </summary>
    /// <param name="language">The language chosen</param>
    /// <returns>A task that completes when the browser answered</returns>
    private async Task ChooseLanguageAsync(UiLanguage language)
    {
        _language = language;
        _texts = UiTexts.For(language);

        await StoreAsync("language", "censio.storePreference", UiPreferences.LanguageCookieName, UiPreferences.StoredValue(language)).ConfigureAwait(true);
        await StoreAsync("language", "censio.setDocumentLanguage", UiPreferences.StoredValue(language)).ConfigureAwait(true);
    }

    /// <summary>
    /// Calls a browser function; a failed call is logged by the kind of the preference only.
    /// </summary>
    /// <param name="preference">The kind of the preference</param>
    /// <param name="identifier">The browser function</param>
    /// <param name="arguments">The arguments of the function</param>
    /// <returns>A task that completes when the call ended</returns>
    private async Task StoreAsync(string preference, string identifier, params object[] arguments)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync(identifier, arguments).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or OperationCanceledException)
        {
            UiLog.PreferenceNotStored(Logger, preference);
        }
    }

    #endregion // Methods

    #region ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _theme = InitialTheme;
        _language = InitialLanguage;
        _texts = UiTexts.For(_language);
    }

    #endregion // ComponentBase
}