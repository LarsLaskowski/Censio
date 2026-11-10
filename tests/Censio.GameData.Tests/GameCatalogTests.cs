using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameCatalog" />.
/// </summary>
[TestClass]
public sealed class GameCatalogTests
{
    #region Methods

    /// <summary>
    /// Every case of a GUID gives its problem: none for two names, incomplete for one, nameless for none, ambiguous for
    /// an ambiguous GUID (also one that has an entry), unknown for a GUID the catalog does not hold.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogCheckGivesProblemPerCase()
    {
        // Arrange
        var catalog = new GameCatalog([
                                          Entry(10, "Good 10", "Ware 10"),
                                          Entry(11, "Good 11", null),
                                          Entry(12, null, "Ware 12"),
                                          Entry(13, null, null),
                                          Entry(14, string.Empty, string.Empty),
                                          Entry(15, "Good 15", string.Empty),
                                          Entry(16, "Good 16", "Ware 16")
                                      ],
                                      [16, 17],
                                      0,
                                      0);

        // Act
        var problems = new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, -5 }.Select(guid => catalog.Check(guid)).ToArray();

        // Assert
        Assert.AreSequenceEqual([
                                    GameCatalogProblem.None,
                                    GameCatalogProblem.Incomplete,
                                    GameCatalogProblem.Incomplete,
                                    GameCatalogProblem.Nameless,
                                    GameCatalogProblem.Nameless,
                                    GameCatalogProblem.Incomplete,
                                    GameCatalogProblem.Ambiguous,
                                    GameCatalogProblem.Ambiguous,
                                    GameCatalogProblem.Unknown,
                                    GameCatalogProblem.Unknown
                                ],
                                problems,
                                "Each GUID should give the problem of its case");
    }

    /// <summary>
    /// The count is the number of entries and a GUID finds its entry, a GUID the catalog does not hold finds none.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogCountAndFindGiveEntries()
    {
        // Arrange
        var first = Entry(10, "Good 10", "Ware 10");
        var second = Entry(-11, "Good 11", "Ware 11");
        var catalog = new GameCatalog([first, second], [], 3, 2);

        // Assert
        Assert.AreEqual(2, catalog.Count, "The count should be the number of entries");
        Assert.AreSame(first, catalog.Find(10), "A GUID should find its entry");
        Assert.AreSame(second, catalog.Find(-11), "A negative GUID should find its entry");
        Assert.IsNull(catalog.Find(12), "A GUID the catalog does not hold should find no entry");
        Assert.AreEqual(3, catalog.InheritedAssetCount, "The inherited asset count should be kept");
        Assert.AreEqual(2, catalog.DroppedRegionTypeCount, "The dropped region type count should be kept");
    }

    /// <summary>
    /// The entries of a catalog read from files hold no GUID that is ambiguous, one defined twice among them.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void GameCatalogEntriesHoldsEntriesWithoutAmbiguousGuids()
    {
        // Arrange
        const string dataSets = "<DataSets><Group><Name>Global</Name><DataSet><Name>Region</Name><Id>1</Id><Items><Item><Name>Zorlan</Name><Id>1</Id></Item></Items></DataSet></Group></DataSets>";
        const string assets = "<AssetList><Groups><Group><Assets>"
                              + "<Asset><Template>Product</Template><Values><Standard><GUID>10</GUID></Standard><Text><OasisId>1000</OasisId></Text><Product /></Values></Asset>"
                              + "<Asset><Template>Product</Template><Values><Standard><GUID>10</GUID></Standard><Text><OasisId>1000</OasisId></Text><Product /></Values></Asset>"
                              + "<Asset><Template>Product</Template><Values><Standard><GUID>11</GUID></Standard><Text><OasisId>1000</OasisId></Text><Product /></Values></Asset>"
                              + "</Assets></Group></Groups></AssetList>";
        const string texts = "<TextExport><Texts><Text><LineId>1000</LineId><Text>Name</Text></Text></Texts></TextExport>";
        const string properties = "<Properties><Groups /></Properties>";
        const string templates = "<Templates><Group><Name>Session</Name></Group></Templates>";

        // Act
        var result = GameCatalogReader.Read(Open(dataSets), Open(assets), Open(texts), Open(texts), Open(properties), Open(templates));

        // Assert
        Assert.IsNull(result.Error, $"The files should give a catalog, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "The files should give a catalog");
        Assert.AreEqual(GameCatalogProblem.Ambiguous, result.Catalog.Check(10), "The GUID defined twice should be ambiguous");
        Assert.AreSequenceEqual([11], result.Catalog.Entries.Select(entry => entry.AssetGuid).ToArray(), "The entries should hold only the GUID that is not ambiguous");
    }

    #endregion // Methods

    #region Helpers

    /// <summary>
    /// Creates a good entry.
    /// </summary>
    /// <param name="guid">The asset GUID</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <returns>The entry</returns>
    private static GameCatalogEntry Entry(int guid, string? english, string? german)
    {
        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = GameCatalogKind.Good,
                   EnglishName = english,
                   GermanName = german
               };
    }

    /// <summary>
    /// Creates an opener of a file with the given content.
    /// </summary>
    /// <param name="content">The content</param>
    /// <returns>The opener</returns>
    private static Func<GameCatalogSource> Open(string content)
    {
        return () => GameCatalogSource.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    #endregion // Helpers
}