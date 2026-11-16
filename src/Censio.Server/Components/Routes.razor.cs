using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components;

/// <summary>
/// The router of the web UI.
/// </summary>
public partial class Routes
{
    #region Properties

    /// <summary>
    /// Gets or sets the theme the page starts with.
    /// </summary>
    [Parameter]
    public UiTheme Theme { get; set; }

    /// <summary>
    /// Gets or sets the language the page starts with.
    /// </summary>
    [Parameter]
    public UiLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the page may delete a profile's history.
    /// </summary>
    [Parameter]
    public bool DeletionAllowed { get; set; }

    #endregion // Properties
}