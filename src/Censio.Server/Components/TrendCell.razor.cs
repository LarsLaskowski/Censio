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

    /// <summary>
    /// Gets the colour of the line in the theme shown.
    /// </summary>
    private string Stroke => TrendPalette.Sparkline(Theme);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gives the sign of a direction.
    /// </summary>
    /// <param name="direction">The direction</param>
    /// <returns>The arrow</returns>
    private static string Arrow(TrendDirection direction)
    {
        return direction switch
               {
                   TrendDirection.Rising => "↑",
                   TrendDirection.Falling => "↓",
                   _ => "→"
               };
    }

    /// <summary>
    /// Gives the name of a direction in the language shown.
    /// </summary>
    /// <param name="direction">The direction</param>
    /// <returns>The name</returns>
    private string ArrowName(TrendDirection direction)
    {
        return direction switch
               {
                   TrendDirection.Rising => Texts.TrendRising,
                   TrendDirection.Falling => Texts.TrendFalling,
                   _ => Texts.TrendStable
               };
    }

    #endregion // Methods
}