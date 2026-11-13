namespace Censio.Server;

/// <summary>
/// The texts of the web UI's frame in one language.
/// </summary>
public sealed class UiTexts
{
    #region Properties

#pragma warning disable S2325
    // Skeleton: removed with the implementation

    /// <summary>
    /// Gets the label of the button that closes and opens the navigation.
    /// </summary>
    public string NavigationButton => Missing();

    /// <summary>
    /// Gets the text of the navigation entry of the start page.
    /// </summary>
    public string HomeEntry => Missing();

    /// <summary>
    /// Gets the text of the profile switch.
    /// </summary>
    public string ProfileSwitch => Missing();

    /// <summary>
    /// Gets the label of the button that switches to the dark theme.
    /// </summary>
    public string SwitchToDarkTheme => Missing();

    /// <summary>
    /// Gets the label of the button that switches to the light theme.
    /// </summary>
    public string SwitchToLightTheme => Missing();

    /// <summary>
    /// Gets the label of the language menu.
    /// </summary>
    public string LanguageSwitch => Missing();

#pragma warning restore S2325

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the texts of a language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The texts.</returns>
    public static UiTexts For(UiLanguage language)
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Gets the name of a language in that language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The name.</returns>
    public static string LanguageName(UiLanguage language)
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Stands in for a text until the implementation exists.
    /// </summary>
    /// <returns>Never returns.</returns>
    private static string Missing()
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods
}