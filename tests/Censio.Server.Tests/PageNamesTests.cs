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