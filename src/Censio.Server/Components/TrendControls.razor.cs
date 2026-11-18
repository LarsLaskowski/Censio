using System.Globalization;

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

    /// <summary>
    /// Gets the range line: the range, up to the latest tick.
    /// </summary>
    private string RangeLine => string.Format(CultureInfo.InvariantCulture,
                                              Texts.ChartRangeFormat,
                                              Choice.Range switch
                                              {
                                                  ChartRange.LastHour => Texts.RangeLastHour,
                                                  ChartRange.LastWeek => Texts.RangeLastWeek,
                                                  ChartRange.All => Texts.RangeAllHistory,
                                                  _ => Texts.RangeLastDay
                                              });

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Hands a new range, with the axis unchanged, to the callback.
    /// </summary>
    /// <param name="range">The range chosen</param>
    /// <returns>A task that completes when the callback is done</returns>
    private Task ChooseRangeAsync(ChartRange range)
    {
        return ChooseChart.InvokeAsync(Choice with
                                              {
                                                  Range = range
                                              });
    }

    /// <summary>
    /// Hands a new axis, with the range unchanged, to the callback.
    /// </summary>
    /// <param name="axis">The axis chosen</param>
    /// <returns>A task that completes when the callback is done</returns>
    private Task ChooseAxisAsync(ChartAxis axis)
    {
        return ChooseChart.InvokeAsync(Choice with
                                              {
                                                  Axis = axis
                                              });
    }

    #endregion // Methods
}