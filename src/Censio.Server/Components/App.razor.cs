using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components;

/// <summary>
/// The static document of the web UI.
/// </summary>
public partial class App
{
    #region Fields

    /// <summary>
    /// The theme read from the request.
    /// </summary>
    private UiTheme _theme;

    /// <summary>
    /// The language read from the request.
    /// </summary>
    private UiLanguage _language;

    /// <summary>
    /// Whether the host of the request may delete a profile's history.
    /// </summary>
    private bool _deletionAllowed;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the HTTP context of the request.
    /// </summary>
    [CascadingParameter]
    public HttpContext? HttpContext { get; set; }

    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    [Inject]
    public IConfiguration Configuration { get; set; } = null!;

    #endregion // Properties

    #region ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        var request = HttpContext?.Request;

        _theme = UiPreferences.ReadTheme(request?.Cookies[UiPreferences.ThemeCookieName]);
        _language = UiPreferences.ReadLanguage(request?.Cookies[UiPreferences.LanguageCookieName], request?.Headers.AcceptLanguage.ToString());
        _deletionAllowed = request is not null && DeletionHostRule.IsTrusted(request.Host.Host, Configuration["AllowedHosts"]);
    }

    #endregion // ComponentBase
}