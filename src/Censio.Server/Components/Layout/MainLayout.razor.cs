using Censio.Server.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

using MudBlazor;

namespace Censio.Server.Components.Layout;

/// <summary>
/// The frame of every page of the web UI.
/// </summary>
public sealed partial class MainLayout : IDisposable
{
    #region Constants

    /// <summary>
    /// The most profiles the profile switch lists.
    /// </summary>
    private const int MaxListedProfiles = 100;

    #endregion // Constants

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

    /// <summary>
    /// The profiles the shown list was built from.
    /// </summary>
    private IReadOnlyList<StoredProfile> _profiles = [];

    /// <summary>
    /// The lines of the profile switch: the profile identity and its shown name.
    /// </summary>
    private IReadOnlyList<KeyValuePair<Guid, string>> _profileItems = [];

    /// <summary>
    /// The number of profiles the switch does not list.
    /// </summary>
    private int _moreProfiles;

    /// <summary>
    /// The address the Regions navigation entry leads to.
    /// </summary>
    private string _regionsLink = "/regions";

    /// <summary>
    /// A value that indicates whether the layout is disposed.
    /// </summary>
    private volatile bool _isDisposed;

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
    /// Gets or sets the navigation manager.
    /// </summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>
    /// Gets or sets the server state.
    /// </summary>
    [Inject]
    private ServerState State { get; set; } = null!;

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    [Inject]
    private ILogger<MainLayout> Logger { get; set; } = null!;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Builds the lines of the profile switch from the profiles of the server state when they changed or the language did.
    /// </summary>
    /// <param name="force"><see langword="true" /> to build them although the profiles are the same</param>
    private void RefreshProfiles(bool force)
    {
        var profiles = State.Profiles;

        if (force == false && ReferenceEquals(profiles, _profiles))
        {
            return;
        }

        _profiles = profiles;
        _profileItems = PageNames.Profiles(profiles, _language, MaxListedProfiles);
        _moreProfiles = Math.Max(0, profiles.Count - MaxListedProfiles);
    }

    /// <summary>
    /// Follows the profiles of the server state: it only hands the work to the layout's context.
    /// </summary>
    /// <param name="sender">The server state</param>
    /// <param name="arguments">The notice</param>
    private void OnStateChanged(object? sender, ServerStateChangedEventArgs arguments)
    {
        if (_isDisposed || arguments.Kind != ServerStateChangeKind.Profile)
        {
            return;
        }

        _ = InvokeAsync(() =>
                        {
                            if (_isDisposed)
                            {
                                return;
                            }

                            RefreshProfiles(false);
                            StateHasChanged();
                        });
    }

    /// <summary>
    /// Points the Regions navigation entry at the address now shown.
    /// </summary>
    private void RefreshRegionsLink()
    {
        _regionsLink = PageAddress.RegionsEntryLink(Navigation.ToBaseRelativePath(Navigation.Uri));
    }

    /// <summary>
    /// Follows the address: it only hands the work to the layout's context.
    /// </summary>
    /// <param name="sender">The navigation manager</param>
    /// <param name="arguments">The change</param>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs arguments)
    {
        if (_isDisposed)
        {
            return;
        }

        _ = InvokeAsync(() =>
                        {
                            if (_isDisposed)
                            {
                                return;
                            }

                            RefreshRegionsLink();
                            StateHasChanged();
                        });
    }

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

        RefreshProfiles(true);

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

        RefreshProfiles(true);
        RefreshRegionsLink();
        State.Changed += OnStateChanged;
        Navigation.LocationChanged += OnLocationChanged;
    }

    #endregion // ComponentBase

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _isDisposed = true;
        State.Changed -= OnStateChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }

    #endregion // IDisposable
}