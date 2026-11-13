using MudBlazor;

namespace Censio.Server;

/// <summary>
/// Creates the MudBlazor theme of the web UI.
/// </summary>
public static class CensioTheme
{
    #region Constants

    /// <summary>
    /// The white of the light palette.
    /// </summary>
    private const string White = "#FFFFFF";

    /// <summary>
    /// The font family of body text and controls; system fonts only.
    /// </summary>
    private static readonly string[] _defaultFamily = ["Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    /// <summary>
    /// The font family of headings; system fonts only.
    /// </summary>
    private static readonly string[] _headingFamily = ["Georgia", "Times New Roman", "serif"];

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Creates the theme with the light and the dark palette.
    /// </summary>
    /// <returns>The theme.</returns>
    public static MudTheme Create()
    {
        return new MudTheme
               {
                   PaletteLight = new PaletteLight
                                  {
                                      Primary = "#8B1E1E",
                                      PrimaryContrastText = White,
                                      Secondary = "#7A5A12",
                                      SecondaryContrastText = White,
                                      AppbarBackground = "#6E1616",
                                      AppbarText = White,
                                      Background = "#FAF6EF",
                                      Surface = White,
                                      DrawerBackground = "#F2EADD",
                                      DrawerText = "#2B2420",
                                      DrawerIcon = "#5A4F45",
                                      TextPrimary = "#2B2420",
                                      TextSecondary = "#5A4F45",
                                      ActionDefault = "#5A4F45",
                                      LinesInputs = "#8A7D70"
                                  },
                   PaletteDark = new PaletteDark
                                 {
                                     Primary = "#E8907A",
                                     PrimaryContrastText = "#1C1310",
                                     Secondary = "#D9AE4A",
                                     SecondaryContrastText = "#1C1310",
                                     AppbarBackground = "#2E1515",
                                     AppbarText = "#F5EDE3",
                                     Background = "#1A1614",
                                     Surface = "#251F1C",
                                     DrawerBackground = "#211B18",
                                     DrawerText = "#EDE4D8",
                                     DrawerIcon = "#C2B5A6",
                                     TextPrimary = "#EDE4D8",
                                     TextSecondary = "#C2B5A6",
                                     ActionDefault = "#C2B5A6",
                                     LinesInputs = "#8F8274"
                                 },
                   Typography = new Typography
                                {
                                    Default = new DefaultTypography
                                              {
                                                  FontFamily = _defaultFamily
                                              },
                                    H1 = new H1Typography
                                         {
                                             FontFamily = _headingFamily
                                         },
                                    H2 = new H2Typography
                                         {
                                             FontFamily = _headingFamily
                                         },
                                    H3 = new H3Typography
                                         {
                                             FontFamily = _headingFamily
                                         },
                                    H4 = new H4Typography
                                         {
                                             FontFamily = _headingFamily
                                         },
                                    H5 = new H5Typography
                                         {
                                             FontFamily = _headingFamily
                                         },
                                    H6 = new H6Typography
                                         {
                                             FontFamily = _headingFamily
                                         }
                                }
               };
    }

    #endregion // Methods
}