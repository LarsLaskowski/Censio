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
    /// Every text the data pages, their banner and the profile switch add exists in English and German, each exactly the
    /// one of the spec and the plan's layout.
    /// </summary>
    /// <param name="textName">The name of the text property</param>
    /// <param name="english">The English text</param>
    /// <param name="german">The German text</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(nameof(UiTexts.RegionsEntry), "Regions", "Regionen")]
    [DataRow(nameof(UiTexts.MoreProfilesFormat), "{0} more profiles", "{0} weitere Profile")]
    [DataRow(nameof(UiTexts.ColumnRegion), "Region", "Region")]
    [DataRow(nameof(UiTexts.ColumnIslands), "Islands", "Inseln")]
    [DataRow(nameof(UiTexts.ColumnBuildings), "Buildings", "Gebäude")]
    [DataRow(nameof(UiTexts.ColumnProducedGoods), "Produced goods", "Produzierte Waren")]
    [DataRow(nameof(UiTexts.ColumnGoodsInDeficit), "Goods in deficit", "Waren im Defizit")]
    [DataRow(nameof(UiTexts.ColumnIsland), "Island", "Insel")]
    [DataRow(nameof(UiTexts.ColumnGood), "Good", "Ware")]
    [DataRow(nameof(UiTexts.ColumnProduction), "Production", "Produktion")]
    [DataRow(nameof(UiTexts.ColumnConsumption), "Consumption", "Verbrauch")]
    [DataRow(nameof(UiTexts.ColumnDelta), "Delta", "Delta")]
    [DataRow(nameof(UiTexts.ColumnProductivity), "Productivity", "Produktivität")]
    [DataRow(nameof(UiTexts.AllRegions), "All regions", "Alle Regionen")]
    [DataRow(nameof(UiTexts.NoData), "No data yet. The pages fill once the Companion sends data.", "Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.")]
    [DataRow(nameof(UiTexts.NoIslandValues), "No values for this island in the latest tick.", "Keine Werte für diese Insel im letzten Tick.")]
    [DataRow(nameof(UiTexts.NotFound), "Not found.", "Nicht gefunden.")]
    [DataRow(nameof(UiTexts.Loading), "Loading…", "Wird geladen…")]
    [DataRow(nameof(UiTexts.ReadFailed), "The data cannot be read right now. Reload the page to try again.", "Die Daten können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu versuchen.")]
    [DataRow(nameof(UiTexts.ShowingGoodsFormat), "Showing {0} of {1} goods.", "{0} von {1} Waren angezeigt.")]
    [DataRow(nameof(UiTexts.AsOfFormat), "As of {0}", "Stand: {0}")]
    [DataRow(nameof(UiTexts.CompanionNotConnected), "The Companion is not connected.", "Der Companion ist nicht verbunden.")]
    [DataRow(nameof(UiTexts.GameNotRunning), "The game is not running.", "Das Spiel läuft nicht.")]
    [DataRow(nameof(UiTexts.StatisticsUnreadable), "The Companion cannot read the game's statistics.", "Der Companion kann die Statistik des Spiels nicht lesen.")]
    public void UiTextsEveryPageTextHasBothLanguages(string textName, string english, string german)
    {
        // Act
        var texts = $"{ReadText(UiTexts.For(UiLanguage.English), textName)}|{ReadText(UiTexts.For(UiLanguage.German), textName)}";

        // Assert
        Assert.AreEqual($"{english}|{german}", texts, $"The text {textName} should exist in English and German as the spec gives it");
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