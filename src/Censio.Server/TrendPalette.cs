using MudBlazor;
using MudBlazor.Utilities;

namespace Censio.Server;

/// <summary>
/// The line colours of the trend charts.
/// </summary>
public static class TrendPalette
{
    #region Fields

    /// <summary>
    /// The theme the colours are taken from.
    /// </summary>
    private static readonly MudTheme _theme = CensioTheme.Create();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Gives the line colours of a chart in a theme: the values of the theme's palette, which reach 4.5:1 on the surface.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="theme">The theme</param>
    /// <returns>The colours</returns>
    public static IReadOnlyList<string> For(TrendKind kind, UiTheme theme)
    {
        var palette = PaletteOf(theme);

        return kind == TrendKind.Good
                   ? [Hex(palette.Success), Hex(palette.Error), Hex(palette.Secondary)]
                   : [Hex(palette.Secondary), Hex(palette.Success), Hex(palette.Error)];
    }

    /// <summary>
    /// Gives the palette of a theme.
    /// </summary>
    /// <param name="theme">The theme</param>
    /// <returns>The palette</returns>
    private static Palette PaletteOf(UiTheme theme)
    {
        return theme == UiTheme.Dark
                   ? _theme.PaletteDark
                   : _theme.PaletteLight;
    }

    /// <summary>
    /// Gives a colour as hexadecimal text.
    /// </summary>
    /// <param name="color">The colour</param>
    /// <returns>The text</returns>
    private static string Hex(MudColor color)
    {
        return color.ToString(MudColorOutputFormats.Hex);
    }

    #endregion // Methods
}