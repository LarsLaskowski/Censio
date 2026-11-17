using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Censio.Server.Components;

/// <summary>
/// The tooltip of a trend chart: the lines of text of a hovered point in a box beside the point, written through the
/// render tree so no text is ever markup.
/// </summary>
public sealed class TrendTooltip : ComponentBase
{
    #region Constants

    /// <summary>
    /// The height of a line of text.
    /// </summary>
    private const double LineHeight = 16;

    /// <summary>
    /// The space between the box and its text.
    /// </summary>
    private const double Padding = 6;

    /// <summary>
    /// The distance from the top of a line to its baseline.
    /// </summary>
    private const double TextBaseline = 11;

    /// <summary>
    /// The width of one character of the text, estimated.
    /// </summary>
    private const double CharacterWidth = 7;

    /// <summary>
    /// The space between the box and the hovered point.
    /// </summary>
    private const double Gap = 10;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets or sets the lines of text.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the colour of the line the point lies on.
    /// </summary>
    [Parameter]
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value that indicates whether the box ends at the point, as the point lies in the right half of the chart.
    /// </summary>
    [Parameter]
    public bool AlignRight { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the box lies below the point, as there is no room above it.
    /// </summary>
    [Parameter]
    public bool Below { get; set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Formats a coordinate for an attribute.
    /// </summary>
    /// <param name="value">The coordinate</param>
    /// <returns>The text</returns>
    private static string S(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    #endregion // Methods

    #region ComponentBase

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Lines.Count == 0)
        {
            return;
        }

        var width = (Lines.Max(line => line.Length) * CharacterWidth) + (2 * Padding);
        var height = (Lines.Count * LineHeight) + Padding;
        var left = AlignRight ? Gap - width : -Gap;
        var top = Below ? Gap + Padding : -height - Gap;

        builder.OpenElement(0, "g");
        builder.AddAttribute(1, "class", "censio-chart-tooltip");
        builder.AddAttribute(2, "style", "pointer-events: none;");
        builder.OpenElement(3, "rect");
        builder.AddAttribute(4, "x", S(left));
        builder.AddAttribute(5, "y", S(top));
        builder.AddAttribute(6, "width", S(width));
        builder.AddAttribute(7, "height", S(height));
        builder.AddAttribute(8, "rx", "4");
        builder.AddAttribute(9, "ry", "4");
        builder.AddAttribute(10, "stroke", Color);
        builder.AddAttribute(11, "stroke-width", "1");
        builder.AddAttribute(12, "style", "fill: var(--mud-palette-surface);");
        builder.CloseElement();

        for (var index = 0; index < Lines.Count; index++)
        {
            builder.OpenElement(13, "text");
            builder.AddAttribute(14, "x", S(left + Padding));
            builder.AddAttribute(15, "y", S(top + (Padding / 2) + (index * LineHeight) + TextBaseline));
            builder.AddAttribute(16, "font-size", "12px");
            builder.AddAttribute(17, "style", "fill: var(--mud-palette-text-primary);");
            builder.AddContent(18, Lines[index]);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    #endregion // ComponentBase
}