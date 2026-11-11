using System.Security.Cryptography;

using Censio.Contracts;
using Censio.GameData;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogDocumentConverter" />.
/// </summary>
[TestClass]
public sealed class GameCatalogDocumentConverterTests
{
    #region Fields

    /// <summary>
    /// The names of the six fixture files in the order of the parameters of <c>GameCatalogReader.Read</c>.
    /// </summary>
    private static readonly string[] _fileNames = ["datasets.xml", "assets.xml", "texts_english.xml", "texts_german.xml", "properties.xml", "templates.xml"];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Every field of an entry is mapped: GUID, kind, names, region GUID, region types and the SHA-256 reference of the icon.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentMapsEveryField()
    {
        // Arrange
        var regionPng = new byte[] { 1, 2, 3, 4 };
        var goodPng = new byte[] { 9, 8, 7 };
        var catalog = CatalogOf(Entry(-10, GameCatalogKind.Region, "Wood", "Holz", 5, ["Coast", "Land"], "r.dds"),
                                Entry(20, GameCatalogKind.Good, "Plank", null, null, null, "g.dds"),
                                Entry(30, GameCatalogKind.Workforce, null, "Arbeiter", null, null, null),
                                Entry(40, GameCatalogKind.Building, string.Empty, string.Empty, null, [], null),
                                Entry(50, GameCatalogKind.Building, "Hut", "Huette", null, null, null));
        var icons = IconsOf((-10, Icon(regionPng)), (20, Icon(goodPng)));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, icons, "1.2.3.4");

        // Assert
        Assert.AreEqual(1, document.SchemaVersion, "The document should be of schema version 1");
        Assert.AreEqual("1.2.3.4", document.GameVersion, "The game version should be the build");
        Assert.HasCount(5, document.Entries, "Every entry should be converted");
        Assert.AreSequenceEqual(new[] { -10, 20, 30, 40, 50 }, document.Entries.Select(entry => entry.AssetGuid).ToArray(), "The entries should keep the catalog order");
        Assert.AreSequenceEqual(new[] { CatalogEntryKind.Region, CatalogEntryKind.Good, CatalogEntryKind.Workforce, CatalogEntryKind.Building, CatalogEntryKind.Building }, document.Entries.Select(entry => entry.Kind).ToArray(), "The kinds should be mapped");

        Assert.HasCount(2, document.Entries[0].Names, "A region with two names should have both");
        Assert.AreEqual("Wood", document.Entries[0].Names["en"], "The English name should be under en");
        Assert.AreEqual("Holz", document.Entries[0].Names["de"], "The German name should be under de");
        Assert.AreEqual(5, document.Entries[0].RegionGuid, "The region GUID should be mapped");

        string[] expectedTypes = ["Coast", "Land"];

        Assert.AreSequenceEqual(expectedTypes, document.Entries[0].RegionTypes!.ToArray(), "The region types should keep their order");
        Assert.AreEqual(Hash(regionPng), document.Entries[0].IconReference, "The icon reference should be the lowercase hex SHA-256 of the PNG");

        Assert.HasCount(1, document.Entries[1].Names, "A good with one name should have that one");
        Assert.AreEqual("Plank", document.Entries[1].Names["en"], "The English name should be mapped");
        Assert.IsNull(document.Entries[1].RegionGuid, "A good should have no region GUID");
        Assert.IsNull(document.Entries[1].RegionTypes, "Region types not stated should stay null");
        Assert.AreEqual(Hash(goodPng), document.Entries[1].IconReference, "The good should have its icon reference");

        Assert.HasCount(1, document.Entries[2].Names, "A name only in German should be the only one");
        Assert.AreEqual("Arbeiter", document.Entries[2].Names["de"], "The German name should be mapped");
        Assert.IsNull(document.Entries[2].IconReference, "An entry without an icon should have no reference");

        Assert.IsEmpty(document.Entries[3].Names, "Empty names should be left out");
        Assert.IsNotNull(document.Entries[3].RegionTypes, "No region types should stay an empty list");
        Assert.IsEmpty(document.Entries[3].RegionTypes!, "No region types should stay an empty list");
        Assert.IsNull(document.Entries[4].IconReference, "An entry the icon set does not hold should have no reference");
    }

    /// <summary>
    /// The region types are copied, not shared with the catalog.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentCopiesRegionTypes()
    {
        // Arrange
        var types = new List<string>
                    {
                        "Coast"
                    };
        var catalog = CatalogOf(Entry(1, GameCatalogKind.Region, "A", "B", 5, types, null));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, null, null);

        types.Add("Land");

        // Assert
        string[] expectedType = ["Coast"];

        Assert.AreSequenceEqual(expectedType, document.Entries[0].RegionTypes!.ToArray(), "A later change of the catalog's list should not reach the document");
    }

    /// <summary>
    /// Entries that share an image get one reference, entries of another image another one.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentSharesReferenceOfSharedImage()
    {
        // Arrange
        var shared = Icon([5, 5, 5]);
        var other = Icon([6, 6, 6]);
        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, "A", "A", null, null, "x.dds"),
                                Entry(2, GameCatalogKind.Good, "B", "B", null, null, "x.dds"),
                                Entry(3, GameCatalogKind.Good, "C", "C", null, null, "y.dds"));
        var icons = IconsOf((1, shared), (2, shared), (3, other));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, icons, null);

        // Assert
        Assert.IsNotNull(document.Entries[0].IconReference, "The first entry should have a reference");
        Assert.AreEqual(document.Entries[0].IconReference, document.Entries[1].IconReference, "Entries sharing an image should share the reference");
        Assert.AreNotEqual(document.Entries[0].IconReference, document.Entries[2].IconReference, "An entry of another image should have another reference");
    }

    /// <summary>
    /// Without an icon set no entry has a reference.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentWithoutIconSetHasNoReferences()
    {
        // Arrange
        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, "A", "A", null, null, "x.dds"), Entry(2, GameCatalogKind.Region, "B", "B", 3, ["T"], "y.dds"));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, null, "1.0.0.0");

        // Assert
        Assert.HasCount(2, document.Entries, "The entries should be the same without icons");
        Assert.IsTrue(document.Entries.All(entry => entry.IconReference is null), "No entry should have an icon reference");
    }

    /// <summary>
    /// An entry of GUID 0 is left out.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentLeavesOutGuidZero()
    {
        // Arrange
        var catalog = CatalogOf(Entry(0, GameCatalogKind.Good, "Zero", "Null", null, null, null), Entry(7, GameCatalogKind.Good, "Seven", "Sieben", null, null, null));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, null, null);

        // Assert
        int[] expectedSeven = [7];

        Assert.AreSequenceEqual(expectedSeven, document.Entries.Select(entry => entry.AssetGuid).ToArray(), "Only the entry of GUID 7 should be converted");
    }

    /// <summary>
    /// Of two entries of one GUID the first is kept, at its place.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentKeepsFirstOfRepeatedGuid()
    {
        // Arrange
        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, "One", "Eins", null, null, null),
                                Entry(5, GameCatalogKind.Good, "First", "Erster", null, null, null),
                                Entry(2, GameCatalogKind.Good, "Two", "Zwei", null, null, null),
                                Entry(5, GameCatalogKind.Workforce, "Second", "Zweiter", null, null, null));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, null, null);

        // Assert
        int[] expectedOrder = [1, 5, 2];

        Assert.AreSequenceEqual(expectedOrder, document.Entries.Select(entry => entry.AssetGuid).ToArray(), "The repeated GUID should appear once, at the first place");
        Assert.AreEqual("First", document.Entries[1].Names["en"], "The first entry of the GUID should be kept");
        Assert.AreEqual(CatalogEntryKind.Good, document.Entries[1].Kind, "The kind of the first entry should be kept");
    }

    /// <summary>
    /// An entry of an ambiguous GUID is left out, although the catalog holds it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentLeavesOutAmbiguousGuid()
    {
        // Arrange
        var catalog = new GameCatalog([Entry(77, GameCatalogKind.Good, "Missing", "Fehlt", null, null, null), Entry(78, GameCatalogKind.Good, "Present", "Da", null, null, null)], [77], 0, 0);

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, null, null);

        // Assert
        int[] expectedUnambiguous = [78];

        Assert.AreSequenceEqual(expectedUnambiguous, document.Entries.Select(entry => entry.AssetGuid).ToArray(), "Only the entry of the unambiguous GUID should be converted");
    }

    /// <summary>
    /// An unknown build gives a game version of null.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentWritesUnknownBuildAsNull()
    {
        // Arrange
        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, "A", "A", null, null, null));

        // Act
        var document = GameCatalogDocumentConverter.ToDocument(catalog, null, null);

        // Assert
        Assert.IsNull(document.GameVersion, "An unknown build should give no game version");
        Assert.AreEqual(1, document.SchemaVersion, "The schema version should be 1");
    }

    /// <summary>
    /// The result of a conversion is accepted by the writer, also for an empty catalog and without names.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentIsAcceptedByWriter()
    {
        // Arrange
        var catalog = CatalogOf(Entry(0, GameCatalogKind.Good, "Zero", "Null", null, null, null),
                                Entry(-3, GameCatalogKind.Region, "R", "R", -9, ["T"], "r.dds"),
                                Entry(4, GameCatalogKind.Good, null, null, null, null, "g.dds"),
                                Entry(4, GameCatalogKind.Good, "Again", "Nochmal", null, null, null),
                                Entry(5, GameCatalogKind.Building, "B", null, null, [], null));
        var icons = IconsOf((-3, Icon([1, 1])), (4, Icon([2, 2])));
        var document = GameCatalogDocumentConverter.ToDocument(catalog, icons, "65535.0.0.1");

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        Assert.IsNull(result.Error, "The writer should accept the converted document");
        Assert.IsNull(CatalogDocumentReader.Read(result.Utf8Json.Span).Error, "The reader should accept what the writer wrote");
    }

    /// <summary>
    /// The catalog built from the six fixture files, with icons of invented pixels for its goods and regions, converts, writes and reads back valid and equal, every entry included.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogDocumentConverterFixtureCatalogRoundTripsValid()
    {
        // Arrange
        var loaded = GameCatalogReader.Read(Opener(0), Opener(1), Opener(2), Opener(3), Opener(4), Opener(5));

        Assert.IsNull(loaded.Error, "The fixture catalog should be built");

        var catalog = loaded.Catalog!;
        var icons = IconsOf(catalog.Entries.Where(entry => entry.Kind is GameCatalogKind.Good or GameCatalogKind.Region)
                                           .Select(entry => (entry.AssetGuid, Icon([(byte)(entry.AssetGuid & 0xFF), (byte)((entry.AssetGuid >> 8) & 0xFF), 42, 7])))
                                           .ToArray());
        var document = GameCatalogDocumentConverter.ToDocument(catalog, icons, "1.2.3.4");

        // Act
        var written = CatalogDocumentWriter.Write(document);
        var read = CatalogDocumentReader.Read(written.Utf8Json.Span);
        var again = CatalogDocumentWriter.Write(read.Document!);

        // Assert
        Assert.IsNull(written.Error, "The writer should accept the converted fixture catalog");
        Assert.IsNull(read.Error, "The reader should accept what the writer wrote");
        Assert.HasCount(25, catalog.Entries, "The fixture catalog should hold 25 entries");
        Assert.HasCount(catalog.Entries.Count, document.Entries, "Every entry of the fixture catalog should be converted");
        Assert.HasCount(catalog.Entries.Count, read.Document!.Entries, "Every entry should be read back");
        Assert.AreEqual(icons.Icons.Count, document.Entries.Count(entry => entry.IconReference is not null), "Every goods and region entry should have a reference");
        Assert.AreSequenceEqual(written.Utf8Json.ToArray(), again.Utf8Json.ToArray(), "Writing the document read back should give the same bytes");
        AssertSame(document, read.Document);
    }

    /// <summary>
    /// An entry of an undefined kind is not converted silently but refused with an argument exception.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogDocumentConverterToDocumentThrowsForUndefinedKind()
    {
        // Arrange
        var catalog = CatalogOf(Entry(1, (GameCatalogKind)99, "A", "A", null, null, null));

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => GameCatalogDocumentConverter.ToDocument(catalog, null, null), "An undefined kind should be refused");

        // Assert
        Assert.AreEqual("kind", exception.ParamName, "The exception should name the kind parameter");
    }

    /// <summary>
    /// Opens a file of the linked fixture.
    /// </summary>
    /// <param name="index">The index of the file</param>
    /// <returns>The opener</returns>
    private static Func<GameCatalogSource> Opener(int index)
    {
        return () => GameCatalogSource.FromStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GameCatalog", _fileNames[index])));
    }

    /// <summary>
    /// Creates a catalog without ambiguous GUIDs.
    /// </summary>
    /// <param name="entries">The entries</param>
    /// <returns>The catalog</returns>
    private static GameCatalog CatalogOf(params GameCatalogEntry[] entries)
    {
        return new GameCatalog(entries, [], 0, 0);
    }

    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="kind">The kind</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="regionTypes">The region types</param>
    /// <param name="iconFilename">The icon file name</param>
    /// <returns>The entry</returns>
    private static GameCatalogEntry Entry(int guid, GameCatalogKind kind, string? english, string? german, int? regionGuid, IReadOnlyList<string>? regionTypes, string? iconFilename)
    {
        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = kind,
                   EnglishName = english,
                   GermanName = german,
                   RegionGuid = regionGuid,
                   RegionTypes = regionTypes,
                   IconFilename = iconFilename
               };
    }

    /// <summary>
    /// Creates an icon of invented PNG bytes.
    /// </summary>
    /// <param name="png">The bytes</param>
    /// <returns>The icon</returns>
    private static GameIcon Icon(byte[] png)
    {
        return new GameIcon(4, 4, png);
    }

    /// <summary>
    /// Creates an icon set that holds icons for GUIDs.
    /// </summary>
    /// <param name="icons">The GUIDs with their icons</param>
    /// <returns>The set</returns>
    private static GameIconSet IconsOf(params (int Guid, GameIcon Icon)[] icons)
    {
        return new GameIconSet(icons.ToDictionary(pair => pair.Guid, pair => pair.Icon), new Dictionary<int, GameIconProblem>(), icons.Length, 0);
    }

    /// <summary>
    /// Computes the reference of PNG bytes: SHA-256 in lowercase hex.
    /// </summary>
    /// <param name="png">The bytes</param>
    /// <returns>The reference</returns>
    private static string Hash(byte[] png)
    {
        return Convert.ToHexStringLower(SHA256.HashData(png));
    }

    /// <summary>
    /// Asserts that two documents are equal in every field and order.
    /// </summary>
    /// <param name="expected">The expected document</param>
    /// <param name="actual">The actual document</param>
    private static void AssertSame(CatalogDocument expected, CatalogDocument actual)
    {
        Assert.AreEqual(expected.SchemaVersion, actual.SchemaVersion, "The schema version should be equal");
        Assert.AreEqual(expected.GameVersion, actual.GameVersion, "The game version should be equal");

        for (var index = 0; index < expected.Entries.Count; index++)
        {
            var want = expected.Entries[index];
            var got = actual.Entries[index];

            Assert.AreEqual(want.AssetGuid, got.AssetGuid, $"The GUID of entry {index} should be equal");
            Assert.AreEqual(want.Kind, got.Kind, $"The kind of entry {index} should be equal");
            Assert.AreEqual(want.RegionGuid, got.RegionGuid, $"The region GUID of entry {index} should be equal");
            Assert.AreEqual(want.IconReference, got.IconReference, $"The icon of entry {index} should be equal");
            Assert.HasCount(want.Names.Count, got.Names, $"The number of names of entry {index} should be equal");
            Assert.AreEqual(want.RegionTypes is null, got.RegionTypes is null, $"Region types not stated should stay so for entry {index}");

            foreach (var pair in want.Names)
            {
                Assert.AreEqual(pair.Value, got.Names[pair.Key], $"The name in {pair.Key} of entry {index} should be equal");
            }

            if (want.RegionTypes is not null)
            {
                Assert.AreSequenceEqual(want.RegionTypes.ToArray(), got.RegionTypes!.ToArray(), $"The region types of entry {index} should be equal");
            }
        }
    }

    #endregion // Methods
}