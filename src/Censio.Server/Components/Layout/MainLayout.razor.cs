using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Layout;

/// <summary>
/// The frame of every page of the web UI.
/// </summary>
public partial class MainLayout
{
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

    #endregion // Properties
}