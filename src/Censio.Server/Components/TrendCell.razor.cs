using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components;

/// <summary>
/// The cell of a table that shows a sparkline with its trend arrow.
/// </summary>
public sealed partial class TrendCell
{
    #region Properties

    /// <summary>
    /// Gets or sets the sparkline, or <see langword="null"/> when there is none.
    /// </summary>
    [Parameter]
    public SparklineView? Line { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sparklines were read.
    /// </summary>
    [Parameter]
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the theme shown.
    /// </summary>
    [CascadingParameter]
    public UiTheme Theme { get; set; }

    /// <summary>
    /// Gets or sets the texts of the language shown.
    /// </summary>
    [CascadingParameter]
    public UiTexts Texts { get; set; } = UiTexts.For(UiLanguage.English);

    #endregion // Properties
}