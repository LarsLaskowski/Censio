using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Censio.Server.Tests;

/// <summary>
/// A page stand-in that renders the chart choice it receives as a cascaded value and a button that hands a choice to the
/// cascaded callback.
/// </summary>
public sealed class CascadedChartChoiceProbe : ComponentBase
{
    #region Properties

    /// <summary>
    /// Gets or sets the cascaded chart choice.
    /// </summary>
    [CascadingParameter]
    public ChartChoice Choice { get; set; } = ChartChoice.Default;

    /// <summary>
    /// Gets or sets the cascaded callback that takes a new chart choice.
    /// </summary>
    [CascadingParameter(Name = PageTestHost.ChooseChartName)]
    public EventCallback<ChartChoice> ChooseChart { get; set; }

    /// <summary>
    /// Gets or sets the choice the button hands to the callback.
    /// </summary>
    [Parameter]
    public ChartChoice Target { get; set; } = new()
                                              {
                                                  Range = ChartRange.LastWeek,
                                                  Axis = ChartAxis.GameTime
                                              };

    #endregion // Properties

    #region ComponentBase

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "probe-choice");
        builder.AddContent(2, $"{Choice.Range} {Choice.Axis}");
        builder.CloseElement();
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "class", "probe-choose");
        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => ChooseChart.InvokeAsync(Target)));
        builder.AddContent(6, "choose");
        builder.CloseElement();
    }

    #endregion // ComponentBase
}