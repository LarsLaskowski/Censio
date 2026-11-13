using MudBlazor;
using MudBlazor.Utilities;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="CensioTheme" />.
/// </summary>
[TestClass]
public sealed class CensioThemeTests
{
    #region Constants

    /// <summary>
    /// The WCAG 2.1 level AA contrast of text against its background.
    /// </summary>
    private const double TextContrast = 4.5;

    /// <summary>
    /// The WCAG 2.1 level AA contrast of a control boundary, an icon or a focus indicator against its surroundings.
    /// </summary>
    private const double LineContrast = 3.0;

    /// <summary>
    /// The highest value of a colour channel.
    /// </summary>
    private const double ChannelMaximum = 255.0;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Every text-background pair, control boundary, icon and focus indicator of both palettes meets the contrast level of
    /// WCAG 2.1 AA against the surface it lies on; both colours of a pair are opaque, so the ratio is the rendered one.
    /// </summary>
    /// <param name="theme">The theme whose palette is checked</param>
    /// <param name="foregroundName">The name of the foreground colour of the palette</param>
    /// <param name="backgroundName">The name of the background colour of the palette</param>
    /// <param name="minimum">The lowest contrast ratio allowed</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(UiTheme.Light, nameof(Palette.TextPrimary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.TextPrimary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.TextSecondary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.TextSecondary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.AppbarText), nameof(Palette.AppbarBackground), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.DrawerText), nameof(Palette.DrawerBackground), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Primary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Primary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Primary), nameof(Palette.DrawerBackground), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.PrimaryContrastText), nameof(Palette.Primary), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.SecondaryContrastText), nameof(Palette.Secondary), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Secondary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Secondary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.DrawerIcon), nameof(Palette.DrawerBackground), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.ActionDefault), nameof(Palette.Background), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.ActionDefault), nameof(Palette.Surface), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.LinesInputs), nameof(Palette.Background), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.LinesInputs), nameof(Palette.Surface), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.AppbarText), nameof(Palette.AppbarBackground), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Primary), nameof(Palette.DrawerBackground), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Primary), nameof(Palette.Background), LineContrast)]
    [DataRow(UiTheme.Light, nameof(Palette.Primary), nameof(Palette.Surface), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.TextPrimary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.TextPrimary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.TextSecondary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.TextSecondary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.AppbarText), nameof(Palette.AppbarBackground), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.DrawerText), nameof(Palette.DrawerBackground), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Primary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Primary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Primary), nameof(Palette.DrawerBackground), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.PrimaryContrastText), nameof(Palette.Primary), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.SecondaryContrastText), nameof(Palette.Secondary), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Secondary), nameof(Palette.Background), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Secondary), nameof(Palette.Surface), TextContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.DrawerIcon), nameof(Palette.DrawerBackground), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.ActionDefault), nameof(Palette.Background), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.ActionDefault), nameof(Palette.Surface), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.LinesInputs), nameof(Palette.Background), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.LinesInputs), nameof(Palette.Surface), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.AppbarText), nameof(Palette.AppbarBackground), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Primary), nameof(Palette.DrawerBackground), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Primary), nameof(Palette.Background), LineContrast)]
    [DataRow(UiTheme.Dark, nameof(Palette.Primary), nameof(Palette.Surface), LineContrast)]
    public void CensioThemeCreatePalettesMeetContrastLevels(UiTheme theme, string foregroundName, string backgroundName, double minimum)
    {
        // Act
        var palette = SelectPalette(CensioTheme.Create(), theme);

        // Assert
        var foreground = ReadColor(palette, foregroundName);
        var background = ReadColor(palette, backgroundName);

        Assert.AreEqual(byte.MaxValue, foreground.A, $"{foregroundName} of the {theme} palette should be opaque");
        Assert.AreEqual(byte.MaxValue, background.A, $"{backgroundName} of the {theme} palette should be opaque");
        Assert.IsGreaterThanOrEqualTo(minimum, ContrastRatio(foreground, background), $"{foregroundName} on {backgroundName} of the {theme} palette should reach {minimum}:1");
    }

    /// <summary>
    /// Selects the palette of a theme.
    /// </summary>
    /// <param name="mudTheme">The MudBlazor theme</param>
    /// <param name="theme">The theme</param>
    /// <returns>The palette</returns>
    private static Palette SelectPalette(MudTheme mudTheme, UiTheme theme)
    {
        return theme == UiTheme.Dark ? mudTheme.PaletteDark : mudTheme.PaletteLight;
    }

    /// <summary>
    /// Reads a colour of a palette by its property name.
    /// </summary>
    /// <param name="palette">The palette</param>
    /// <param name="colorName">The name of the colour property</param>
    /// <returns>The colour</returns>
    private static MudColor ReadColor(Palette palette, string colorName)
    {
        return (MudColor)typeof(Palette).GetProperty(colorName)!.GetValue(palette)!;
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