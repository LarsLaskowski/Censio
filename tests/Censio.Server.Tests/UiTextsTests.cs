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
    [DataRow(nameof(UiTexts.UnnamedIsland), "Unnamed island", "Unbenannte Insel")]
    [DataRow(nameof(UiTexts.UnnamedProfile), "Unnamed profile", "Unbenanntes Profil")]
    [DataRow(nameof(UiTexts.GoodFallbackFormat), "Good {0}", "Ware {0}")]
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
    /// Every text of the deletion exists in English and German, each exactly the one of spec AC9 and E9.
    /// </summary>
    /// <param name="textName">The name of the text property</param>
    /// <param name="english">The English text</param>
    /// <param name="german">The German text</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(nameof(UiTexts.DeleteHistory), "Delete history", "Verlauf löschen")]
    [DataRow(nameof(UiTexts.DeleteHistoryTitle), "Delete history?", "Verlauf löschen?")]
    [DataRow(nameof(UiTexts.DeleteHistoryTextFormat), "All data of the profile {0} is removed from the server: its regions, islands, goods and history. This cannot be undone.", "Alle Daten des Profils {0} werden vom Server entfernt: seine Regionen, Inseln, Waren und sein Verlauf. Das lässt sich nicht rückgängig machen.")]
    [DataRow(nameof(UiTexts.DeleteHistoryConfirm), "Delete", "Löschen")]
    [DataRow(nameof(UiTexts.DeleteHistoryCancel), "Cancel", "Abbrechen")]
    [DataRow(nameof(UiTexts.DeleteHistoryFailed), "The history could not be deleted.", "Der Verlauf konnte nicht gelöscht werden.")]
    [DataRow(nameof(UiTexts.DeleteHistoryHostRefused), "Deleting needs this page opened by IP address or as localhost, or by a host name the setting AllowedHosts lists.", "Löschen setzt voraus, dass diese Seite über die IP-Adresse oder localhost geöffnet wurde, oder über einen Hostnamen, den die Einstellung AllowedHosts nennt.")]
    public void UiTextsForDeletionTextsMatchSpec(string textName, string english, string german)
    {
        // Act
        var texts = $"{ReadText(UiTexts.For(UiLanguage.English), textName)}|{ReadText(UiTexts.For(UiLanguage.German), textName)}";

        // Assert
        Assert.AreEqual($"{english}|{german}", texts, $"The text {textName} should exist in English and German as spec AC9 and E9 give it");
    }

    /// <summary>
    /// Every text of the trend charts exists in English and German, each exactly the one of the spec.
    /// </summary>
    /// <param name="textName">The name of the text property</param>
    /// <param name="english">The English text</param>
    /// <param name="german">The German text</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(nameof(UiTexts.ChartRange), "Last 24 hours up to the latest tick", "Letzte 24 Stunden bis zum letzten Tick")]
    [DataRow(nameof(UiTexts.ChartThisIsland), "This island", "Diese Insel")]
    [DataRow(nameof(UiTexts.ChartRegionTotal), "All islands of the region", "Alle Inseln der Region")]
    [DataRow(nameof(UiTexts.ChartOverview), "Overview", "Übersicht")]
    [DataRow(nameof(UiTexts.ChartNoValues), "No values in this period.", "Keine Werte in diesem Zeitraum.")]
    [DataRow(nameof(UiTexts.ChartCapFormat), "Showing the newest 2,880 of {0} points.", "Die neuesten 2.880 von {0} Punkten angezeigt.")]
    [DataRow(nameof(UiTexts.HourlyAverage), "Hourly average", "Stundenmittel")]
    public void UiTextsChartTextsInBothLanguages(string textName, string english, string german)
    {
        // Act
        var shownInEnglish = ReadText(UiTexts.For(UiLanguage.English), textName);
        var shownInGerman = ReadText(UiTexts.For(UiLanguage.German), textName);

        // Assert
        Assert.AreEqual(english, shownInEnglish, $"The chart text {textName} should read as the spec gives it in English");
        Assert.AreEqual(german, shownInGerman, $"The chart text {textName} should read as the spec gives it in German");
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