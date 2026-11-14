namespace Censio.Server;

/// <summary>
/// The texts of the web UI in one language.
/// </summary>
public sealed class UiTexts
{
    #region Fields

    /// <summary>
    /// The English texts.
    /// </summary>
    private static readonly UiTexts _english = new()
                                               {
                                                   NavigationButton = "Open or close the navigation",
                                                   HomeEntry = "Home",
                                                   RegionsEntry = "Regions",
                                                   ProfileSwitch = "Profile",
                                                   SwitchToDarkTheme = "Switch to the dark theme",
                                                   SwitchToLightTheme = "Switch to the light theme",
                                                   LanguageSwitch = "Language",
                                                   MoreProfilesFormat = "{0} more profiles",
                                                   UnnamedIsland = "Unnamed island",
                                                   UnnamedProfile = "Unnamed profile",
                                                   GoodFallbackFormat = "Good {0}",
                                                   ColumnRegion = "Region",
                                                   ColumnIslands = "Islands",
                                                   ColumnBuildings = "Buildings",
                                                   ColumnProducedGoods = "Produced goods",
                                                   ColumnGoodsInDeficit = "Goods in deficit",
                                                   ColumnIsland = "Island",
                                                   ColumnGood = "Good",
                                                   ColumnProduction = "Production",
                                                   ColumnConsumption = "Consumption",
                                                   ColumnDelta = "Delta",
                                                   ColumnProductivity = "Productivity",
                                                   AllRegions = "All regions",
                                                   NoData = "No data yet. The pages fill once the Companion sends data.",
                                                   NoIslandValues = "No values for this island in the latest tick.",
                                                   NotFound = "Not found.",
                                                   Loading = "Loading…",
                                                   ReadFailed = "The data cannot be read right now. Reload the page to try again.",
                                                   ShowingGoodsFormat = "Showing {0} of {1} goods.",
                                                   AsOfFormat = "As of {0}",
                                                   CompanionNotConnected = "The Companion is not connected.",
                                                   GameNotRunning = "The game is not running.",
                                                   StatisticsUnreadable = "The Companion cannot read the game's statistics."
                                               };

    /// <summary>
    /// The German texts.
    /// </summary>
    private static readonly UiTexts _german = new()
                                              {
                                                  NavigationButton = "Navigation öffnen oder schließen",
                                                  HomeEntry = "Startseite",
                                                  RegionsEntry = "Regionen",
                                                  ProfileSwitch = "Profil",
                                                  SwitchToDarkTheme = "Zum dunklen Design wechseln",
                                                  SwitchToLightTheme = "Zum hellen Design wechseln",
                                                  LanguageSwitch = "Sprache",
                                                  MoreProfilesFormat = "{0} weitere Profile",
                                                  UnnamedIsland = "Unbenannte Insel",
                                                  UnnamedProfile = "Unbenanntes Profil",
                                                  GoodFallbackFormat = "Ware {0}",
                                                  ColumnRegion = "Region",
                                                  ColumnIslands = "Inseln",
                                                  ColumnBuildings = "Gebäude",
                                                  ColumnProducedGoods = "Produzierte Waren",
                                                  ColumnGoodsInDeficit = "Waren im Defizit",
                                                  ColumnIsland = "Insel",
                                                  ColumnGood = "Ware",
                                                  ColumnProduction = "Produktion",
                                                  ColumnConsumption = "Verbrauch",
                                                  ColumnDelta = "Delta",
                                                  ColumnProductivity = "Produktivität",
                                                  AllRegions = "Alle Regionen",
                                                  NoData = "Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.",
                                                  NoIslandValues = "Keine Werte für diese Insel im letzten Tick.",
                                                  NotFound = "Nicht gefunden.",
                                                  Loading = "Wird geladen…",
                                                  ReadFailed = "Die Daten können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu versuchen.",
                                                  ShowingGoodsFormat = "{0} von {1} Waren angezeigt.",
                                                  AsOfFormat = "Stand: {0}",
                                                  CompanionNotConnected = "Der Companion ist nicht verbunden.",
                                                  GameNotRunning = "Das Spiel läuft nicht.",
                                                  StatisticsUnreadable = "Der Companion kann die Statistik des Spiels nicht lesen."
                                              };

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the name shown for an island without a name.
    /// </summary>
    public required string UnnamedIsland { get; init; }

    /// <summary>
    /// Gets the name shown for a profile without a name.
    /// </summary>
    public required string UnnamedProfile { get; init; }

    /// <summary>
    /// Gets the format of the name of a good the catalog does not name, with the product GUID as its argument.
    /// </summary>
    public required string GoodFallbackFormat { get; init; }

    /// <summary>
    /// Gets the label of the button that closes and opens the navigation.
    /// </summary>
    public required string NavigationButton { get; init; }

    /// <summary>
    /// Gets the text of the navigation entry of the start page.
    /// </summary>
    public required string HomeEntry { get; init; }

    /// <summary>
    /// Gets the text of the navigation entry of the region overview, also its heading.
    /// </summary>
    public required string RegionsEntry { get; init; }

    /// <summary>
    /// Gets the text of the profile switch.
    /// </summary>
    public required string ProfileSwitch { get; init; }

    /// <summary>
    /// Gets the label of the button that switches to the dark theme.
    /// </summary>
    public required string SwitchToDarkTheme { get; init; }

    /// <summary>
    /// Gets the label of the button that switches to the light theme.
    /// </summary>
    public required string SwitchToLightTheme { get; init; }

    /// <summary>
    /// Gets the label of the language menu.
    /// </summary>
    public required string LanguageSwitch { get; init; }

    /// <summary>
    /// Gets the format of the line that counts the profiles the switch does not list; the count is argument 0.
    /// </summary>
    public required string MoreProfilesFormat { get; init; }

    /// <summary>
    /// Gets the heading of the region column.
    /// </summary>
    public required string ColumnRegion { get; init; }

    /// <summary>
    /// Gets the heading of the island count column.
    /// </summary>
    public required string ColumnIslands { get; init; }

    /// <summary>
    /// Gets the heading of the buildings column.
    /// </summary>
    public required string ColumnBuildings { get; init; }

    /// <summary>
    /// Gets the heading of the produced goods column.
    /// </summary>
    public required string ColumnProducedGoods { get; init; }

    /// <summary>
    /// Gets the heading of the goods in deficit column.
    /// </summary>
    public required string ColumnGoodsInDeficit { get; init; }

    /// <summary>
    /// Gets the heading of the island column.
    /// </summary>
    public required string ColumnIsland { get; init; }

    /// <summary>
    /// Gets the heading of the good column.
    /// </summary>
    public required string ColumnGood { get; init; }

    /// <summary>
    /// Gets the heading of the production column.
    /// </summary>
    public required string ColumnProduction { get; init; }

    /// <summary>
    /// Gets the heading of the consumption column.
    /// </summary>
    public required string ColumnConsumption { get; init; }

    /// <summary>
    /// Gets the heading of the delta column.
    /// </summary>
    public required string ColumnDelta { get; init; }

    /// <summary>
    /// Gets the heading of the productivity column.
    /// </summary>
    public required string ColumnProductivity { get; init; }

    /// <summary>
    /// Gets the text of the link to the region overview.
    /// </summary>
    public required string AllRegions { get; init; }

    /// <summary>
    /// Gets the text of a page without data.
    /// </summary>
    public required string NoData { get; init; }

    /// <summary>
    /// Gets the text of an island detail without values.
    /// </summary>
    public required string NoIslandValues { get; init; }

    /// <summary>
    /// Gets the text of a page whose address names nothing known.
    /// </summary>
    public required string NotFound { get; init; }

    /// <summary>
    /// Gets the text of a page that waits for its data.
    /// </summary>
    public required string Loading { get; init; }

    /// <summary>
    /// Gets the text of a page whose read failed.
    /// </summary>
    public required string ReadFailed { get; init; }

    /// <summary>
    /// Gets the format of the line under a cut goods table; the shown count is argument 0, the total argument 1.
    /// </summary>
    public required string ShowingGoodsFormat { get; init; }

    /// <summary>
    /// Gets the format of the as-of line; the time is argument 0.
    /// </summary>
    public required string AsOfFormat { get; init; }

    /// <summary>
    /// Gets the banner while no Companion is connected.
    /// </summary>
    public required string CompanionNotConnected { get; init; }

    /// <summary>
    /// Gets the banner while the Companion waits for the game.
    /// </summary>
    public required string GameNotRunning { get; init; }

    /// <summary>
    /// Gets the banner while the Companion cannot read the statistics.
    /// </summary>
    public required string StatisticsUnreadable { get; init; }

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