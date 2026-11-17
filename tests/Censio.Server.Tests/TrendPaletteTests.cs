using MudBlazor;
using MudBlazor.Utilities;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendPalette" />.
/// </summary>
[TestClass]
public sealed class TrendPaletteTests
{
    #region Constants

    /// <summary>
    /// The WCAG 2.1 level AA contrast of a graphical object against its surroundings.
    /// </summary>
    private const double LineContrast = 3.0;

    /// <summary>
    /// The least difference in hue, in degrees, between two lines of a chart.
    /// </summary>
    private const double MinimumHueDifference = 30.0;

    /// <summary>
    /// The number of lines of each chart.
    /// </summary>
    private const int LineCount = 3;

    /// <summary>
    /// The highest value of a colour channel.
    /// </summary>
    private const double ChannelMaximum = 255.0;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Every line colour of a chart reaches the contrast of 3:1 against the surface of the theme it is drawn on, and is
    /// opaque.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="theme">The theme</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(TrendKind.Good, UiTheme.Light)]
    [DataRow(TrendKind.Good, UiTheme.Dark)]
    [DataRow(TrendKind.Figures, UiTheme.Light)]
    [DataRow(TrendKind.Figures, UiTheme.Dark)]
    public void TrendPaletteLinesReachThreeToOneOnSurface(TrendKind kind, UiTheme theme)
    {
        // Arrange
        var mudTheme = CensioTheme.Create();
        var surface = theme == UiTheme.Dark ? mudTheme.PaletteDark.Surface : mudTheme.PaletteLight.Surface;

        // Act
        var lines = TrendPalette.For(kind, theme).Select(text => new MudColor(text)).ToList();

        // Assert
        Assert.HasCount(LineCount, lines, "A chart should have a colour for each of its three lines");

        foreach (var line in lines)
        {
            Assert.AreEqual(byte.MaxValue, line.A, "A line colour should be opaque, so the ratio is the rendered one");
            Assert.IsGreaterThanOrEqualTo(LineContrast, ContrastRatio(line, surface), $"The line colour {line.ToString(MudColorOutputFormats.Hex)} of the {kind} chart should reach 3:1 on the surface of the {theme} theme");
        }
    }

    /// <summary>
    /// The lines of one chart differ in hue by 30 degrees or more, pairwise, in both themes.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="theme">The theme</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(TrendKind.Good, UiTheme.Light)]
    [DataRow(TrendKind.Good, UiTheme.Dark)]
    [DataRow(TrendKind.Figures, UiTheme.Light)]
    [DataRow(TrendKind.Figures, UiTheme.Dark)]
    public void TrendPaletteLinesOfAChartDifferInHue(TrendKind kind, UiTheme theme)
    {
        // Act
        var hues = TrendPalette.For(kind, theme).Select(text => new MudColor(text).H).ToList();

        // Assert
        Assert.HasCount(LineCount, hues, "A chart should have a colour for each of its three lines");

        foreach (var (first, second) in Pairs(hues))
        {
            var difference = Math.Abs(first - second);

            Assert.IsGreaterThanOrEqualTo(MinimumHueDifference, Math.Min(difference, 360 - difference), $"Two lines of the {kind} chart in the {theme} theme should differ in hue by 30 degrees or more, not {first:0.#} and {second:0.#}");
        }
    }

    /// <summary>
    /// Gives every pair of two different values of a list.
    /// </summary>
    /// <param name="values">The values</param>
    /// <returns>The pairs</returns>
    private static IEnumerable<(double First, double Second)> Pairs(IReadOnlyList<double> values)
    {
        return values.SelectMany((first, index) => values.Skip(index + 1).Select(second => (first, second)));
    }

    /// <summary>
    /// Computes the WCAG 2.1 contrast ratio of two colours.
    /// </summary>
    /// <param name="first">The first colour</param>
    /// <param name="second">The second colour</param>
    /// <returns>The ratio of the lighter to the darker colour's relative luminance, each plus 0.05</returns>
    private static double ContrastRatio(MudColor first, MudColor second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);

        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    /// <summary>
    /// Computes the WCAG 2.1 relative luminance of a colour.
    /// </summary>
    /// <param name="color">The colour</param>
    /// <returns>The relative luminance between 0 and 1</returns>
    private static double RelativeLuminance(MudColor color)
    {
        return (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));
    }

    /// <summary>
    /// Converts an sRGB colour channel into its linear value.
    /// </summary>
    /// <param name="channel">The channel value from 0 to 255</param>
    /// <returns>The linear value between 0 and 1</returns>
    private static double Linearize(byte channel)
    {
        var value = channel / ChannelMaximum;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    #endregion // Methods
}