using System.Globalization;
using System.Text;

using Censio.Contracts;
using Censio.Server.Data;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="PageNames" /> and the <see cref="ShownName" /> it gives.
/// </summary>
[TestClass]
public sealed class PageNamesTests
{
    #region Constants

    /// <summary>
    /// The shown name of a profile without a name in English (spec E5).
    /// </summary>
    private const string UnnamedProfileEnglish = "Unnamed profile";

    /// <summary>
    /// A profile name two profiles share.
    /// </summary>
    private const string SharedProfileName = "Rome";

    /// <summary>
    /// A region name two regions share.
    /// </summary>
    private const string SharedRegionName = "Latium";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// A good or region takes the catalog name of the shown language, else the other language's, else the fallback of the
    /// spec: <c>Good &lt;GUID&gt;</c> / <c>Ware &lt;GUID&gt;</c> for a good, <c>Region &lt;session GUID&gt;</c> for a
    /// session without a catalog region.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void PageNamesTakesShownLanguageThenOtherThenFallback()
    {
        // Arrange
        var catalog = CreateCatalog("""{"guid":1,"kind":"good","names":{"de":"Brot","en":"Bread"},"regionGuid":null,"regionTypes":null,"icon":null}""",
                                    """{"guid":2,"kind":"good","names":{"en":"Fish"},"regionGuid":null,"regionTypes":null,"icon":null}""",
                                    """{"guid":3,"kind":"good","names":{"de":"Wein"},"regionGuid":null,"regionTypes":null,"icon":null}""",
                                    """{"guid":4,"kind":"good","names":{},"regionGuid":null,"regionTypes":null,"icon":null}""",
                                    """{"guid":10,"kind":"region","names":{"de":"Gallien"},"regionGuid":500,"regionTypes":null,"icon":null}""");

        // Act
        string[] names = [
                             DescribeName(PageNames.Good(1, catalog)),
                             DescribeName(PageNames.Good(2, catalog)),
                             DescribeName(PageNames.Good(3, catalog)),
                             DescribeName(PageNames.Good(4, catalog)),
                             DescribeName(PageNames.Good(-7, catalog)),
                             DescribeName(PageNames.Good(1, null)),
                             DescribeName(PageNames.Region(500, false, 10, catalog)),
                             DescribeName(PageNames.Region(40, true, 40, catalog)),
                             DescribeName(PageNames.Region(40, true, 40, null))
                         ];

        // Assert
        string[] expected = [
                                "Bread | Brot",
                                "Fish | Fish",
                                "Wein | Wein",
                                "Good 4 | Ware 4",
                                "Good -7 | Ware -7",
                                "Good 1 | Ware 1",
                                "Gallien | Gallien",
                                "Region 40 | Region 40",
                                "Region 40 | Region 40"
                            ];

        Assert.AreSequenceEqual(expected, names, "Each name should come from the shown language, else the other language, else the fallback");
    }

    /// <summary>
    /// Regions are ordered by their name in the shown language, ordinally and ignoring case, ties by GUID.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected GUIDs in order</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(UiLanguage.English, "2,9,5,7")]
    [DataRow(UiLanguage.German, "5,7,2,9")]
    public void PageNamesOrdersByNameIgnoringCaseThenGuid(UiLanguage language, string expected)
    {
        // Arrange
        RegionView[] regions = [
                                   CreateRegion(5, "beta", "Alpha"),
                                   CreateRegion(9, "Alpha", "zulu"),
                                   CreateRegion(7, "Äpfel", "alpha"),
                                   CreateRegion(2, "alpha", "Yankee")
                               ];

        // Act
        var ordered = PageNames.OrderRegions(regions, language);

        // Assert
        Assert.AreEqual(expected, string.Join(',', ordered.Select(region => region.RegionGuid)), "The regions should be ordered by name, ordinally ignoring case, ties by GUID");
    }

    /// <summary>
    /// Regions whose shown names are equal get their GUID appended in parentheses; a name only one region shows stays as it
    /// is, in each language on its own.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected texts of the three regions</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(UiLanguage.English, "Latium (5)|Latium (9)|Gallia")]
    [DataRow(UiLanguage.German, "Latium|Latium Nova|Gallien")]
    public void PageNamesEqualRegionNamesGetGuid(UiLanguage language, string expected)
    {
        // Arrange
        RegionView[] regions = [
                                   CreateRegion(5, SharedRegionName, SharedRegionName),
                                   CreateRegion(9, SharedRegionName, "Latium Nova"),
                                   CreateRegion(3, "Gallia", "Gallien")
                               ];

        // Act
        var texts = regions.Select(region => PageNames.RegionText(region, regions, language)).ToList();

        // Assert
        Assert.AreEqual(expected, string.Join('|', texts), "Equal shown names should get the GUID appended, other names stay as they are");
    }

    /// <summary>
    /// An island shows its name, and an empty name the text of the spec in the shown language.
    /// </summary>
    /// <param name="name">The stored name</param>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected shown name</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("", UiLanguage.English, "Unnamed island")]
    [DataRow("", UiLanguage.German, "Unbenannte Insel")]
    [DataRow("Island 3", UiLanguage.German, "Island 3")]
    public void PageNamesIslandEmptyNameShowsUnnamed(string name, UiLanguage language, string expected)
    {
        // Act
        var shown = PageNames.Island(name, language);

        // Assert
        Assert.AreEqual(expected, shown, "An island should show its name, or the unnamed text when it has none");
    }

    /// <summary>
    /// The profiles are listed by shown name; a profile without a name shows the text of the spec, and profiles with equal
    /// shown names get the first eight characters of their identity appended in parentheses.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="unnamed">The expected shown name of the profile without a name</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(UiLanguage.English, UnnamedProfileEnglish)]
    [DataRow(UiLanguage.German, "Unbenanntes Profil")]
    public void PageNamesProfilesNamesUnnamedAndTellsEqualNamesApart(UiLanguage language, string unnamed)
    {
        // Arrange
        StoredProfile[] profiles = [
                                       CreateProfile("33333333-0000-4000-8000-000000000003", null),
                                       CreateProfile("22222222-0000-4000-8000-000000000002", SharedProfileName),
                                       CreateProfile("44444444-0000-4000-8000-000000000004", "Athens"),
                                       CreateProfile("11111111-0000-4000-8000-000000000001", SharedProfileName)
                                   ];

        // Act
        var listed = PageNames.Profiles(profiles, language, 100);

        // Assert
        string[] expected = [
                                "44444444-0000-4000-8000-000000000004 Athens",
                                "11111111-0000-4000-8000-000000000001 Rome (11111111)",
                                "22222222-0000-4000-8000-000000000002 Rome (22222222)",
                                $"33333333-0000-4000-8000-000000000003 {unnamed}"
                            ];

        Assert.AreSequenceEqual(expected, listed.Select(pair => $"{pair.Key} {pair.Value}").ToList(), "The profiles should be listed by shown name, equal names told apart by their identity");
    }

    /// <summary>
    /// At most the limit of profiles is listed, the first in the order of their shown names.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void PageNamesProfilesListsAtMostLimit()
    {
        // Arrange
        StoredProfile[] profiles = [
                                       CreateProfile("33333333-0000-4000-8000-000000000003", "Carthage"),
                                       CreateProfile("11111111-0000-4000-8000-000000000001", "Athens"),
                                       CreateProfile("22222222-0000-4000-8000-000000000002", "Babylon")
                                   ];

        // Act
        var listed = PageNames.Profiles(profiles, UiLanguage.English, 2);

        // Assert
        string[] expected = ["Athens", "Babylon"];

        Assert.AreSequenceEqual(expected, listed.Select(pair => pair.Value).ToList(), "Only the first profiles up to the limit should be listed");
    }

    /// <summary>
    /// The fallback names of an unnamed island, an unnamed profile and a good the catalog cannot name are the texts of
    /// <see cref="UiTexts" /> of the shown language.
    /// </summary>
    /// <param name="language">The language</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(UiLanguage.English)]
    [DataRow(UiLanguage.German)]
    public void PageNamesFallbacksComeFromUiTexts(UiLanguage language)
    {
        // Arrange
        var texts = UiTexts.For(language);

        // Act
        var names = $"{PageNames.Island(string.Empty, language)}|{PageNames.Profiles([CreateProfile("11111111-0000-4000-8000-000000000001", null)], language, 1)[0].Value}|{PageNames.Good(1001, null).For(language)}";

        // Assert
        Assert.AreEqual($"{texts.UnnamedIsland}|{texts.UnnamedProfile}|{string.Format(CultureInfo.InvariantCulture, texts.GoodFallbackFormat, 1001)}", names, $"The fallback names in {language} should be the texts of UiTexts");
    }

    /// <summary>
    /// Trade routes are ordered by major problems, minor problems and lost ships, each descending (a negative count below
    /// 0), then by shown name ordinally ignoring case (the unnamed-route text of the language for an empty name), folder
    /// and ID.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected IDs in order</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(UiLanguage.English, "2,5,4,3,11,1,7,8,6,10,9,12")]
    [DataRow(UiLanguage.German, "2,5,4,3,11,1,7,8,6,9,10,12")]
    public void PageNamesOrderTradeRoutesByProblemsThenNameFolderAndId(UiLanguage language, string expected)
    {
        // Arrange
        TradeRouteView[] routes = [
                                      CreateTradeRoute(1, "alpha", "F", "0/0/0"),
                                      CreateTradeRoute(12, "aaa", "F", "-1/0/0"),
                                      CreateTradeRoute(3, "zeta", "F", "1/5/0"),
                                      CreateTradeRoute(6, "Beta", "F", "0/0/0"),
                                      CreateTradeRoute(8, "beta", "E", "0/0/0"),
                                      CreateTradeRoute(10, "Uncle", "F", "0/0/0"),
                                      CreateTradeRoute(4, "zeta", "F", "1/5/3"),
                                      CreateTradeRoute(9, string.Empty, "F", "0/0/0"),
                                      CreateTradeRoute(2, "zeta", "F", "2/0/0"),
                                      CreateTradeRoute(7, "beta", "E", "0/0/0"),
                                      CreateTradeRoute(11, "zeta", "F", "0/0/1"),
                                      CreateTradeRoute(5, "zeta", "F", "1/7/0")
                                  ];

        // Act
        var ordered = PageNames.OrderTradeRoutes(routes, language);

        // Assert
        Assert.AreEqual(expected, string.Join(',', ordered.Select(route => route.Id)), "The routes should be ordered by their counts descending, then by shown name ignoring case, folder and ID");
    }

    /// <summary>
    /// The summary line counts the routes and those with a major or minor problem or a lost ship above 0, in the one-route
    /// form for one route and with the thousands separator of the language.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="counts">The major problems, minor problems and lost ships of each route, separated by commas</param>
    /// <param name="repeat">The number of times the routes are repeated</param>
    /// <param name="expected">The summary line of the spec</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(UiLanguage.English, "0/0/0,1/0/0,0/1/0,0/0/1,-1/0/0,0/-2/-3", 1, "6 trade routes, 3 with problems or lost ships.")]
    [DataRow(UiLanguage.German, "0/0/0,1/0/0,0/1/0,0/0/1,-1/0/0,0/-2/-3", 1, "6 Handelsrouten, 3 mit Problemen oder verlorenen Schiffen.")]
    [DataRow(UiLanguage.English, "0/0/0", 1, "1 trade route, 0 with problems or lost ships.")]
    [DataRow(UiLanguage.German, "0/0/0", 1, "1 Handelsroute, 0 mit Problemen oder verlorenen Schiffen.")]
    [DataRow(UiLanguage.English, "2/0/0", 1, "1 trade route, 1 with problems or lost ships.")]
    [DataRow(UiLanguage.German, "0/0/4", 1, "1 Handelsroute, 1 mit Problemen oder verlorenen Schiffen.")]
    [DataRow(UiLanguage.English, "0/0/1", 1000, "1,000 trade routes, 1,000 with problems or lost ships.")]
    [DataRow(UiLanguage.German, "0/0/1", 1000, "1.000 Handelsrouten, 1.000 mit Problemen oder verlorenen Schiffen.")]
    public void PageNamesTradeRoutesSummaryCountsRoutesWithProblems(UiLanguage language, string counts, int repeat, string expected)
    {
        // Arrange
        var routes = CreateTradeRoutes(counts, repeat);

        // Act
        var summary = PageNames.TradeRoutesSummary(routes, language);

        // Assert
        Assert.AreEqual(expected, summary, "The summary line should be the one of the spec");
    }

    /// <summary>
    /// An empty route name shows the unnamed-route text, an empty folder a dash, a station no island list names
    /// <c>Island &lt;session GUID&gt;/&lt;island ID&gt;/&lt;area index&gt;</c> and an island with an empty name the
    /// unnamed-island text, in the shown language; other names stay as they are.
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected shown names</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, "Unnamed route|Ostia|–|F1|Island 3245/1/2|Unnamed island|Isle A")]
    [DataRow(UiLanguage.German, "Unbenannte Route|Ostia|–|F1|Insel 3245/1/2|Unbenannte Insel|Isle A")]
    public void PageNamesTradeRouteStationAndFolderFallbacks(UiLanguage language, string expected)
    {
        // Arrange
        var island = new IslandKey(3245, 1, 2);

        // Act
        string[] names = [
                             PageNames.TradeRoute(string.Empty, language),
                             PageNames.TradeRoute("Ostia", language),
                             PageNames.Folder(string.Empty),
                             PageNames.Folder("F1"),
                             PageNames.Station(CreateStop(island, null), language),
                             PageNames.Station(CreateStop(island, string.Empty), language),
                             PageNames.Station(CreateStop(island, "Isle A"), language)
                         ];

        // Assert
        Assert.AreEqual(expected, string.Join('|', names), "Each shown name should be the name or the spec's fallback");
    }

    /// <summary>
    /// Creates a trade route with one ship and no stations.
    /// </summary>
    /// <param name="id">The route ID</param>
    /// <param name="name">The name</param>
    /// <param name="folder">The folder</param>
    /// <param name="counts">The major problems, minor problems and lost ships, separated by slashes</param>
    /// <returns>The route</returns>
    private static TradeRouteView CreateTradeRoute(int id, string name, string folder, string counts)
    {
        var values = counts.Split('/').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToList();

        return new TradeRouteView
               {
                   Id = id,
                   Name = name,
                   Folder = folder,
                   MajorProblems = values[0],
                   MinorProblems = values[1],
                   Ships = 1,
                   LostShips = values[2],
                   Stations = []
               };
    }

    /// <summary>
    /// Creates trade routes from their counts, repeated a number of times.
    /// </summary>
    /// <param name="counts">The major problems, minor problems and lost ships of each route, separated by commas</param>
    /// <param name="repeat">The number of times the routes are repeated</param>
    /// <returns>The routes</returns>
    private static List<TradeRouteView> CreateTradeRoutes(string counts, int repeat)
    {
        return [.. Enumerable.Repeat(counts.Split(','), repeat).SelectMany(route => route).Select((route, index) => CreateTradeRoute(index, "Route", "Folder", route))];
    }

    /// <summary>
    /// Creates a stop of a trade route.
    /// </summary>
    /// <param name="island">The island</param>
    /// <param name="name">The island name, or <see langword="null" /> for none</param>
    /// <returns>The stop</returns>
    private static TradeRouteStop CreateStop(IslandKey island, string? name)
    {
        return new TradeRouteStop
               {
                   Island = island,
                   Name = name
               };
    }

    /// <summary>
    /// Reads a catalog document of entries into the lookup of the pages.
    /// </summary>
    /// <param name="entries">The entries as JSON</param>
    /// <returns>The lookup</returns>
    private static PageCatalog CreateCatalog(params string[] entries)
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[{string.Join(',', entries)}]}}");
        var document = CatalogDocumentReader.Read(bytes).Document ?? throw new InvalidOperationException("The test catalog should be read");

        return PageCatalog.From(document);
    }

    /// <summary>
    /// Describes a shown name as its English and its German form.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The text</returns>
    private static string DescribeName(ShownName name)
    {
        return $"{name.For(UiLanguage.English)} | {name.For(UiLanguage.German)}";
    }

    /// <summary>
    /// Creates a region of one session without islands.
    /// </summary>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <returns>The region</returns>
    private static RegionView CreateRegion(int regionGuid, string english, string german)
    {
        return new RegionView
               {
                   RegionGuid = regionGuid,
                   IsSession = false,
                   SessionGuids = [regionGuid],
                   Name = new ShownName
                          {
                              English = english,
                              German = german
                          },
                   Islands = [],
                   Figures = null
               };
    }

    /// <summary>
    /// Creates a stored profile.
    /// </summary>
    /// <param name="id">The identity</param>
    /// <param name="name">The name, or <see langword="null" /></param>
    /// <returns>The profile</returns>
    private static StoredProfile CreateProfile(string id, string? name)
    {
        return new StoredProfile
               {
                   Id = Guid.Parse(id, CultureInfo.InvariantCulture),
                   Name = name
               };
    }

    #endregion // Methods
}