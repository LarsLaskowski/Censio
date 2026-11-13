namespace Censio.Server;

/// <summary>
/// The texts of the web UI's frame in one language.
/// </summary>
public sealed class UiTexts
{
    #region Fields

    /// <summary>
    /// The English texts.
    /// </summary>
    private static readonly UiTexts _english = new("Open or close the navigation",
                                                   "Home",
                                                   "Profile",
                                                   "Switch to the dark theme",
                                                   "Switch to the light theme",
                                                   "Language");

    /// <summary>
    /// The German texts.
    /// </summary>
    private static readonly UiTexts _german = new("Navigation öffnen oder schließen",
                                                  "Startseite",
                                                  "Profil",
                                                  "Zum dunklen Design wechseln",
                                                  "Zum hellen Design wechseln",
                                                  "Sprache");

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="UiTexts" /> class.
    /// </summary>
    /// <param name="navigationButton">The label of the navigation button</param>
    /// <param name="homeEntry">The text of the start page's navigation entry</param>
    /// <param name="profileSwitch">The text of the profile switch</param>
    /// <param name="switchToDarkTheme">The label of the button that switches to the dark theme</param>
    /// <param name="switchToLightTheme">The label of the button that switches to the light theme</param>
    /// <param name="languageSwitch">The label of the language menu</param>
    private UiTexts(string navigationButton,
                    string homeEntry,
                    string profileSwitch,
                    string switchToDarkTheme,
                    string switchToLightTheme,
                    string languageSwitch)
    {
        NavigationButton = navigationButton;
        HomeEntry = homeEntry;
        ProfileSwitch = profileSwitch;
        SwitchToDarkTheme = switchToDarkTheme;
        SwitchToLightTheme = switchToLightTheme;
        LanguageSwitch = languageSwitch;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the label of the button that closes and opens the navigation.
    /// </summary>
    public string NavigationButton { get; }

    /// <summary>
    /// Gets the text of the navigation entry of the start page.
    /// </summary>
    public string HomeEntry { get; }

    /// <summary>
    /// Gets the text of the profile switch.
    /// </summary>
    public string ProfileSwitch { get; }

    /// <summary>
    /// Gets the label of the button that switches to the dark theme.
    /// </summary>
    public string SwitchToDarkTheme { get; }

    /// <summary>
    /// Gets the label of the button that switches to the light theme.
    /// </summary>
    public string SwitchToLightTheme { get; }

    /// <summary>
    /// Gets the label of the language menu.
    /// </summary>
    public string LanguageSwitch { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the texts of a language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The texts.</returns>
    public static UiTexts For(UiLanguage language)
    {
        return language == UiLanguage.German ? _german : _english;
    }

    /// <summary>
    /// Gets the name of a language in that language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The name.</returns>
    public static string LanguageName(UiLanguage language)
    {
        return language == UiLanguage.German ? "Deutsch" : "English";
    }

    #endregion // Methods
}