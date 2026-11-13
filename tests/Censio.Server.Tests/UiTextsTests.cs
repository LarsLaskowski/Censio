namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="UiTexts" />.
/// </summary>
[TestClass]
public sealed class UiTextsTests
{
    #region Methods

    /// <summary>
    /// Every text of the frame is exactly the one of the spec's table, in both languages.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="textName">The name of the text property</param>
    /// <param name="expected">The text of the spec's table</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(UiLanguage.English, nameof(UiTexts.NavigationButton), "Open or close the navigation")]
    [DataRow(UiLanguage.German, nameof(UiTexts.NavigationButton), "Navigation öffnen oder schließen")]
    [DataRow(UiLanguage.English, nameof(UiTexts.HomeEntry), "Home")]
    [DataRow(UiLanguage.German, nameof(UiTexts.HomeEntry), "Startseite")]
    [DataRow(UiLanguage.English, nameof(UiTexts.ProfileSwitch), "Profile")]
    [DataRow(UiLanguage.German, nameof(UiTexts.ProfileSwitch), "Profil")]
    [DataRow(UiLanguage.English, nameof(UiTexts.SwitchToDarkTheme), "Switch to the dark theme")]
    [DataRow(UiLanguage.German, nameof(UiTexts.SwitchToDarkTheme), "Zum dunklen Design wechseln")]
    [DataRow(UiLanguage.English, nameof(UiTexts.SwitchToLightTheme), "Switch to the light theme")]
    [DataRow(UiLanguage.German, nameof(UiTexts.SwitchToLightTheme), "Zum hellen Design wechseln")]
    [DataRow(UiLanguage.English, nameof(UiTexts.LanguageSwitch), "Language")]
    [DataRow(UiLanguage.German, nameof(UiTexts.LanguageSwitch), "Sprache")]
    public void UiTextsForLanguageReturnsSpecTexts(UiLanguage language, string textName, string expected)
    {
        // Act
        var texts = UiTexts.For(language);

        // Assert
        Assert.AreEqual(expected, ReadText(texts, textName), $"The text {textName} in {language} should be the one of the spec");
    }

    /// <summary>
    /// The language options carry the name of each language in that language, whatever language is shown.
    /// </summary>
    /// <param name="language">The language of the option</param>
    /// <param name="expected">The name of the spec's table</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(UiLanguage.English, "English")]
    [DataRow(UiLanguage.German, "Deutsch")]
    public void UiTextsLanguageNameReturnsSpecName(UiLanguage language, string expected)
    {
        // Act
        var name = UiTexts.LanguageName(language);

        // Assert
        Assert.AreEqual(expected, name, $"The option of {language} should read {expected}");
    }

    /// <summary>
    /// Reads a text property by its name.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="textName">The name of the text property</param>
    /// <returns>The text</returns>
    private static string? ReadText(UiTexts texts, string textName)
    {
        return (string?)typeof(UiTexts).GetProperty(textName)?.GetValue(texts);
    }

    #endregion // Methods
}