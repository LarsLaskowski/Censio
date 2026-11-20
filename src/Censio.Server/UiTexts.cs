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
                                                   TradeRoutesEntry = string.Empty,
                                                   ColumnRoute = string.Empty,
                                                   ColumnFolder = string.Empty,
                                                   ColumnShips = string.Empty,
                                                   ColumnLostShips = string.Empty,
                                                   ColumnMajorProblems = string.Empty,
                                                   ColumnMinorProblems = string.Empty,
                                                   ColumnStations = string.Empty,
                                                   UnnamedRoute = string.Empty,
                                                   StationFallbackFormat = string.Empty,
                                                   NoTradeRoutes = string.Empty,
                                                   EmptyTradeRoutes = string.Empty,
                                                   TradeRoutesSummaryFormat = string.Empty,
                                                   TradeRoutesSummaryOneFormat = string.Empty,
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
                                                   ColumnTrend = "Trend",
                                                   TrendColumnTooltip = "Last 4 hours up to the latest tick",
                                                   TrendRising = "Rising",
                                                   TrendFalling = "Falling",
                                                   TrendStable = "Stable",
                                                   TrendsReadFailed = "The trends cannot be read right now. Reload the page to try again.",
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
                                                   StatisticsUnreadable = "The Companion cannot read the game's statistics.",
                                                   DeleteHistory = "Delete history",
                                                   DeleteHistoryTitle = "Delete history?",
                                                   DeleteHistoryTextFormat = "All data of the profile {0} is removed from the server: its regions, islands, goods and history. This cannot be undone.",
                                                   DeleteHistoryConfirm = "Delete",
                                                   DeleteHistoryCancel = "Cancel",
                                                   DeleteHistoryFailed = "The history could not be deleted.",
                                                   DeleteHistoryHostRefused = "Deleting needs this page opened by IP address or as localhost, or by a host name the setting AllowedHosts lists.",
                                                   ChartRangeFormat = "{0} up to the latest tick",
                                                   RangeLastHour = "Last hour",
                                                   RangeLastDay = "Last 24 hours",
                                                   RangeLastWeek = "Last 7 days",
                                                   RangeAll = "All",
                                                   RangeAllHistory = "All history",
                                                   AxisRealTime = "Real time",
                                                   AxisGameTime = "Game time",
                                                   ChartRangeLabel = "Period",
                                                   ChartAxisLabel = "Time axis",
                                                   GameTimeFormat = "Game time {0}",
                                                   ChartThisIsland = "This island",
                                                   ChartRegionTotal = "All islands of the region",
                                                   ChartOverview = "Overview",
                                                   ChartNoValues = "No values in this period.",
                                                   ChartCapFormat = "Showing the newest 2,880 of {0} points.",
                                                   HourlyAverage = "Hourly average"
                                               };

    /// <summary>
    /// The German texts.
    /// </summary>
    private static readonly UiTexts _german = new()
                                              {
                                                  NavigationButton = "Navigation öffnen oder schließen",
                                                  HomeEntry = "Startseite",
                                                  RegionsEntry = "Regionen",
                                                  TradeRoutesEntry = string.Empty,
                                                  ColumnRoute = string.Empty,
                                                  ColumnFolder = string.Empty,
                                                  ColumnShips = string.Empty,
                                                  ColumnLostShips = string.Empty,
                                                  ColumnMajorProblems = string.Empty,
                                                  ColumnMinorProblems = string.Empty,
                                                  ColumnStations = string.Empty,
                                                  UnnamedRoute = string.Empty,
                                                  StationFallbackFormat = string.Empty,
                                                  NoTradeRoutes = string.Empty,
                                                  EmptyTradeRoutes = string.Empty,
                                                  TradeRoutesSummaryFormat = string.Empty,
                                                  TradeRoutesSummaryOneFormat = string.Empty,
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
                                                  ColumnTrend = "Trend",
                                                  TrendColumnTooltip = "Letzte 4 Stunden bis zum letzten Tick",
                                                  TrendRising = "Steigend",
                                                  TrendFalling = "Fallend",
                                                  TrendStable = "Gleichbleibend",
                                                  TrendsReadFailed = "Die Trends können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu versuchen.",
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
                                                  StatisticsUnreadable = "Der Companion kann die Statistik des Spiels nicht lesen.",
                                                  DeleteHistory = "Verlauf löschen",
                                                  DeleteHistoryTitle = "Verlauf löschen?",
                                                  DeleteHistoryTextFormat = "Alle Daten des Profils {0} werden vom Server entfernt: seine Regionen, Inseln, Waren und sein Verlauf. Das lässt sich nicht rückgängig machen.",
                                                  DeleteHistoryConfirm = "Löschen",
                                                  DeleteHistoryCancel = "Abbrechen",
                                                  DeleteHistoryFailed = "Der Verlauf konnte nicht gelöscht werden.",
                                                  DeleteHistoryHostRefused = "Löschen setzt voraus, dass diese Seite über die IP-Adresse oder localhost geöffnet wurde, oder über einen Hostnamen, den die Einstellung AllowedHosts nennt.",
                                                  ChartRangeFormat = "{0} bis zum letzten Tick",
                                                  RangeLastHour = "Letzte Stunde",
                                                  RangeLastDay = "Letzte 24 Stunden",
                                                  RangeLastWeek = "Letzte 7 Tage",
                                                  RangeAll = "Gesamt",
                                                  RangeAllHistory = "Gesamter Verlauf",
                                                  AxisRealTime = "Echtzeit",
                                                  AxisGameTime = "Spielzeit",
                                                  ChartRangeLabel = "Zeitraum",
                                                  ChartAxisLabel = "Zeitachse",
                                                  GameTimeFormat = "Spielzeit {0}",
                                                  ChartThisIsland = "Diese Insel",
                                                  ChartRegionTotal = "Alle Inseln der Region",
                                                  ChartOverview = "Übersicht",
                                                  ChartNoValues = "Keine Werte in diesem Zeitraum.",
                                                  ChartCapFormat = "Die neuesten 2.880 von {0} Punkten angezeigt.",
                                                  HourlyAverage = "Stundenmittel"
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
    /// Gets the text of the navigation entry of the trade routes, also their heading.
    /// </summary>
    public required string TradeRoutesEntry { get; init; }

    /// <summary>
    /// Gets the heading of the route column.
    /// </summary>
    public required string ColumnRoute { get; init; }

    /// <summary>
    /// Gets the heading of the folder column.
    /// </summary>
    public required string ColumnFolder { get; init; }

    /// <summary>
    /// Gets the heading of the ships column.
    /// </summary>
    public required string ColumnShips { get; init; }

    /// <summary>
    /// Gets the heading of the lost-ships column.
    /// </summary>
    public required string ColumnLostShips { get; init; }

    /// <summary>
    /// Gets the heading of the major-problems column.
    /// </summary>
    public required string ColumnMajorProblems { get; init; }

    /// <summary>
    /// Gets the heading of the minor-problems column.
    /// </summary>
    public required string ColumnMinorProblems { get; init; }

    /// <summary>
    /// Gets the heading of the stations column.
    /// </summary>
    public required string ColumnStations { get; init; }

    /// <summary>
    /// Gets the shown name of a route without a name.
    /// </summary>
    public required string UnnamedRoute { get; init; }

    /// <summary>
    /// Gets the format of the shown name of a station no island list entry names.
    /// </summary>
    public required string StationFallbackFormat { get; init; }

    /// <summary>
    /// Gets the text when no trade routes were received yet.
    /// </summary>
    public required string NoTradeRoutes { get; init; }

    /// <summary>
    /// Gets the text when the latest trade routes hold no route.
    /// </summary>
    public required string EmptyTradeRoutes { get; init; }

    /// <summary>
    /// Gets the format of the summary of the trade routes.
    /// </summary>
    public required string TradeRoutesSummaryFormat { get; init; }

    /// <summary>
    /// Gets the format of the summary of one trade route.
    /// </summary>
    public required string TradeRoutesSummaryOneFormat { get; init; }

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
    /// Gets the heading of the trend column.
    /// </summary>
    public required string ColumnTrend { get; init; }

    /// <summary>
    /// Gets the tooltip of the trend column heading.
    /// </summary>
    public required string TrendColumnTooltip { get; init; }

    /// <summary>
    /// Gets the text of a rising trend.
    /// </summary>
    public required string TrendRising { get; init; }

    /// <summary>
    /// Gets the text of a falling trend.
    /// </summary>
    public required string TrendFalling { get; init; }

    /// <summary>
    /// Gets the text of a stable trend.
    /// </summary>
    public required string TrendStable { get; init; }

    /// <summary>
    /// Gets the text shown when the trends cannot be read.
    /// </summary>
    public required string TrendsReadFailed { get; init; }

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

    /// <summary>
    /// Gets the label of the button that deletes a profile's history.
    /// </summary>
    public required string DeleteHistory { get; init; }

    /// <summary>
    /// Gets the title of the deletion dialog.
    /// </summary>
    public required string DeleteHistoryTitle { get; init; }

    /// <summary>
    /// Gets the text of the deletion dialog; the profile name is argument 0.
    /// </summary>
    public required string DeleteHistoryTextFormat { get; init; }

    /// <summary>
    /// Gets the label of the button that confirms the deletion.
    /// </summary>
    public required string DeleteHistoryConfirm { get; init; }

    /// <summary>
    /// Gets the label of the button that cancels the deletion.
    /// </summary>
    public required string DeleteHistoryCancel { get; init; }

    /// <summary>
    /// Gets the text shown when the deletion failed.
    /// </summary>
    public required string DeleteHistoryFailed { get; init; }

    /// <summary>
    /// Gets the text shown when the page was opened by a host that may not delete.
    /// </summary>
    public required string DeleteHistoryHostRefused { get; init; }

    /// <summary>
    /// Gets the chart text ChartRangeFormat.
    /// </summary>
    public required string ChartRangeFormat { get; init; }

    /// <summary>
    /// Gets the chart text RangeLastHour.
    /// </summary>
    public required string RangeLastHour { get; init; }

    /// <summary>
    /// Gets the chart text RangeLastDay.
    /// </summary>
    public required string RangeLastDay { get; init; }

    /// <summary>
    /// Gets the chart text RangeLastWeek.
    /// </summary>
    public required string RangeLastWeek { get; init; }

    /// <summary>
    /// Gets the chart text RangeAll.
    /// </summary>
    public required string RangeAll { get; init; }

    /// <summary>
    /// Gets the chart text RangeAllHistory.
    /// </summary>
    public required string RangeAllHistory { get; init; }

    /// <summary>
    /// Gets the chart text AxisRealTime.
    /// </summary>
    public required string AxisRealTime { get; init; }

    /// <summary>
    /// Gets the chart text AxisGameTime.
    /// </summary>
    public required string AxisGameTime { get; init; }

    /// <summary>
    /// Gets the chart text ChartRangeLabel.
    /// </summary>
    public required string ChartRangeLabel { get; init; }

    /// <summary>
    /// Gets the chart text ChartAxisLabel.
    /// </summary>
    public required string ChartAxisLabel { get; init; }

    /// <summary>
    /// Gets the chart text GameTimeFormat.
    /// </summary>
    public required string GameTimeFormat { get; init; }

    /// <summary>
    /// Gets the chart text ChartThisIsland.
    /// </summary>
    public required string ChartThisIsland { get; init; }

    /// <summary>
    /// Gets the chart text ChartRegionTotal.
    /// </summary>
    public required string ChartRegionTotal { get; init; }

    /// <summary>
    /// Gets the chart text ChartOverview.
    /// </summary>
    public required string ChartOverview { get; init; }

    /// <summary>
    /// Gets the chart text ChartNoValues.
    /// </summary>
    public required string ChartNoValues { get; init; }

    /// <summary>
    /// Gets the chart text ChartCapFormat.
    /// </summary>
    public required string ChartCapFormat { get; init; }

    /// <summary>
    /// Gets the chart text HourlyAverage.
    /// </summary>
    public required string HourlyAverage { get; init; }

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