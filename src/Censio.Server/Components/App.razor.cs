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

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the HTTP context of the request.
    /// </summary>
    [CascadingParameter]
    public HttpContext? HttpContext { get; set; }

    #endregion // Properties

    #region ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        var request = HttpContext?.Request;

        _theme = UiPreferences.ReadTheme(request?.Cookies[UiPreferences.ThemeCookieName]);
        _language = UiPreferences.ReadLanguage(request?.Cookies[UiPreferences.LanguageCookieName], request?.Headers.AcceptLanguage.ToString());
    }

    #endregion // ComponentBase
}