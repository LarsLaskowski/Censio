using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components;

/// <summary>
/// The range picker, the time axis switch and the range line of the trend charts.
/// </summary>
public sealed partial class TrendControls
{
    #region Properties

    /// <summary>
    /// Gets or sets the current chart choice.
    /// </summary>
    [CascadingParameter]
    public ChartChoice Choice { get; set; } = ChartChoice.Default;

    /// <summary>
    /// Gets or sets the callback that takes a new chart choice.
    /// </summary>
    [CascadingParameter(Name = "ChooseChart")]
    public EventCallback<ChartChoice> ChooseChart { get; set; }

    /// <summary>
    /// Gets or sets the texts of the language shown.
    /// </summary>
    [CascadingParameter]
    public UiTexts Texts { get; set; } = UiTexts.For(UiLanguage.English);

    #endregion // Properties
}