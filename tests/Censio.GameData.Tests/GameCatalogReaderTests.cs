using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using Censio.Pipe;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogReader" />.
/// </summary>
[TestClass]
public sealed class GameCatalogReaderTests
{
    #region Constants

    /// <summary>
    /// The name of the environment variable that names the folder of the six sample files.
    /// </summary>
    private const string SampleVariable = "CENSIO_CATALOG_SAMPLE";

    /// <summary>
    /// The name of the environment variable that names a real installation.
    /// </summary>
    private const string InstallationVariable = "CENSIO_ANNO_INSTALL";

    /// <summary>
    /// The offset of the GUIDs of the fixture.
    /// </summary>
    private const int Base = 2_000_000_000;

    /// <summary>
    /// A text that stands for file content and must never reach an error message.
    /// </summary>
    private const string Secret = "INVENTED-CONTENT-7QX";

    /// <summary>
    /// The fixed message of an unreadable archive.
    /// </summary>
    private const string ArchiveMessage = "The configuration archive could not be opened.";

    /// <summary>
    /// The fixed message of a missing file.
    /// </summary>
    private const string MissingMessage = "A required file is missing from the archive.";

    /// <summary>
    /// The fixed message of an unreadable file.
    /// </summary>
    private const string UnreadableMessage = "A required file could not be read.";

    /// <summary>
    /// The fixed message of a malformed file.
    /// </summary>
    private const string MalformedMessage = "A required file does not have the expected structure.";

    /// <summary>
    /// A <c>Product</c> property with the region type <c>Yultic</c>.
    /// </summary>
    private const string ProductYultic = "<Product><AssociatedRegion>Yultic</AssociatedRegion></Product>";

    /// <summary>
    /// A <c>Product</c> property with the region type <c>Xanthi</c>.
    /// </summary>
    private const string ProductXanthi = "<Product><AssociatedRegion>Xanthi</AssociatedRegion></Product>";

    /// <summary>
    /// A <c>Product</c> property with an empty list.
    /// </summary>
    private const string ProductEmpty = "<Product><AssociatedRegion /></Product>";

    /// <summary>
    /// A <c>Building</c> property with the region type <c>Yultic</c>.
    /// </summary>
    private const string BuildingYultic = "<Building><AssociatedRegions>Yultic</AssociatedRegions></Building>";

    /// <summary>
    /// A <c>Building</c> property with the region type <c>Xanthi</c>.
    /// </summary>
    private const string BuildingXanthi = "<Building><AssociatedRegions>Xanthi</AssociatedRegions></Building>";

    /// <summary>
    /// A <c>Building</c> property with an empty list.
    /// </summary>
    private const string BuildingEmpty = "<Building><AssociatedRegions /></Building>";

    /// <summary>
    /// A <c>Session</c> property that names region asset 5.
    /// </summary>
    private const string SessionRegion5 = "<Session><Region>5</Region></Session>";

    /// <summary>
    /// A <c>Session</c> property that names region asset 8.
    /// </summary>
    private const string SessionRegion8 = "<Session><Region>8</Region></Session>";

    /// <summary>
    /// A <c>Session</c> property that names region <c>0</c>.
    /// </summary>
    private const string SessionRegion0 = "<Session><Region>0</Region></Session>";

    /// <summary>
    /// A <c>Session</c> property with an empty region.
    /// </summary>
    private const string SessionRegionEmpty = "<Session><Region /></Session>";

    /// <summary>
    /// A <c>Session</c> property without a region.
    /// </summary>
    private const string SessionBare = "<Session />";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The names of the files of a catalog in the order of the parameters of <c>Read</c>.
    /// </summary>
    private static readonly string[] _fileNames = ["datasets.xml", "assets.xml", "texts_english.xml", "texts_german.xml", "properties.xml", "templates.xml"];

    /// <summary>
    /// The indexes of the files in the order of their passes: data sets, properties, templates, assets, English, German.
    /// </summary>
    private static readonly int[] _passOrder = [0, 4, 5, 1, 2, 3];

    /// <summary>
    /// The paths of the files of a catalog in the order of the parameters of <c>Read</c>.
    /// </summary>
    private static readonly string[] _filePaths = [GameCatalogReader.DataSetsPath, GameCatalogReader.AssetsPath, GameCatalogReader.EnglishTextsPath, GameCatalogReader.GermanTextsPath, GameCatalogReader.PropertiesPath, GameCatalogReader.TemplatesPath];

    /// <summary>
    /// The entries of the fixture: GUID, kind, English name, German name and problem.
    /// </summary>
    private static readonly (int Guid, GameCatalogKind Kind, string? English, string? German, GameCatalogProblem Problem)[] _fixtureEntries = [
                                                                                                                                                  (Base + 201, GameCatalogKind.Region, "Region 101", "Gebiet 101", GameCatalogProblem.None),
                                                                                                                                                  (Base + 202, GameCatalogKind.Region, "Region 104", "Gebiet 104", GameCatalogProblem.None),
                                                                                                                                                  (Base + 203, GameCatalogKind.Region, "Region 102", "Gebiet 102", GameCatalogProblem.None),
                                                                                                                                                  (Base + 204, GameCatalogKind.Region, "Region 103", "Gebiet 103", GameCatalogProblem.None),
                                                                                                                                                  (Base + 207, GameCatalogKind.Region, "Session 207", "Sitzung 207", GameCatalogProblem.None),
                                                                                                                                                  (Base + 301, GameCatalogKind.Good, "Good 301", "Ware 301", GameCatalogProblem.None),
                                                                                                                                                  (Base + 302, GameCatalogKind.Good, "Good 302", "Ware 302", GameCatalogProblem.None),
                                                                                                                                                  (Base + 303, GameCatalogKind.Good, "Good 303", "Ware 303", GameCatalogProblem.None),
                                                                                                                                                  (Base + 304, GameCatalogKind.Good, "Good 304", "Ware 304", GameCatalogProblem.None),
                                                                                                                                                  (Base + 305, GameCatalogKind.Good, "Good 305", null, GameCatalogProblem.Incomplete),
                                                                                                                                                  (Base + 306, GameCatalogKind.Good, "Good 306", "Ware 306", GameCatalogProblem.None),
                                                                                                                                                  (Base + 307, GameCatalogKind.Good, "Good 306", "Ware 306", GameCatalogProblem.None),
                                                                                                                                                  (Base + 308, GameCatalogKind.Good, null, "Ware 308", GameCatalogProblem.Incomplete),
                                                                                                                                                  (Base + 309, GameCatalogKind.Good, "Good 309", "Ware 309", GameCatalogProblem.None),
                                                                                                                                                  (Base + 311, GameCatalogKind.Good, null, null, GameCatalogProblem.Nameless),
                                                                                                                                                  (Base + 312, GameCatalogKind.Good, null, null, GameCatalogProblem.Nameless),
                                                                                                                                                  (Base + 313, GameCatalogKind.Good, null, null, GameCatalogProblem.Nameless),
                                                                                                                                                  (-(Base + 401), GameCatalogKind.Good, "Good 401", "Ware 401", GameCatalogProblem.None),
                                                                                                                                                  (Base + 351, GameCatalogKind.Workforce, "Worker 351", "Arbeiter 351", GameCatalogProblem.None),
                                                                                                                                                  (Base + 501, GameCatalogKind.Building, "Building 501", "Gebäude 501", GameCatalogProblem.None),
                                                                                                                                                  (Base + 502, GameCatalogKind.Building, "Building 502", "Gebäude 502", GameCatalogProblem.None),
                                                                                                                                                  (Base + 503, GameCatalogKind.Building, "Building 503", "Gebäude 503", GameCatalogProblem.None),
                                                                                                                                                  (Base + 504, GameCatalogKind.Building, "Building 504", "Gebäude 504", GameCatalogProblem.None),
                                                                                                                                                  (Base + 505, GameCatalogKind.Building, "Building 505", "Gebäude 505", GameCatalogProblem.None),
                                                                                                                                                  (Base + 506, GameCatalogKind.Building, "Building 506", "Gebäude 506", GameCatalogProblem.None)
                                                                                                                                              ];

    /// <summary>
    /// The folder of the installation of a synthetic test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-catalog-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public GameCatalogReaderTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the <c>maindata</c> folder of the installation of a synthetic test.
    /// </summary>
    private string MainData => Path.Combine(_root, "maindata");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Deletes the folder of a synthetic test.
    /// </summary>
    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    #endregion // Methods

    #region AC1

    /// <summary>
    /// With the six loose sample files, every non-zero GUID of the pipe fixture resolves to an entry of its kind with
    /// both names, and every region carries exactly one region type; the test writes counts, unresolved GUIDs, per region
    /// its GUID, its region asset GUID and its number of region types, the elapsed time and the peak working set only; it
    /// also builds the catalog with the assets' own values (empty property and template files), fails when an entry of that
    /// build is missing, and writes the difference as counts and GUIDs.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC4")]
    public async Task GameCatalogReaderSampleResolvesFixtureGuids()
    {
        // Arrange
        var folder = Environment.GetEnvironmentVariable(SampleVariable);

        if (string.IsNullOrEmpty(folder))
        {
            Assert.Inconclusive($"{SampleVariable} is not set: the catalog did not run on the sample files");
        }

        var guids = await ReadPipeGuidsAsync();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GameCatalogReader.Read(() => OpenSample(folder, _fileNames[0]),
                                            () => OpenSample(folder, _fileNames[1]),
                                            () => OpenSample(folder, _fileNames[2]),
                                            () => OpenSample(folder, _fileNames[3]),
                                            () => OpenSample(folder, _fileNames[4]),
                                            () => OpenSample(folder, _fileNames[5]));

        stopwatch.Stop();

        // Assert
        Assert.IsNull(result.Error, $"The sample should give a catalog, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "The sample should give a catalog");

        var ownValues = GameCatalogReader.Read(() => OpenSample(folder, _fileNames[0]),
                                               () => OpenSample(folder, _fileNames[1]),
                                               () => OpenSample(folder, _fileNames[2]),
                                               () => OpenSample(folder, _fileNames[3]),
                                               Open("<Properties />"),
                                               Open("<Templates />"));

        Assert.IsNotNull(ownValues.Catalog, $"The sample with the assets' own values should give a catalog, but failed with {ownValues.Error?.Kind}");
        WriteLayerReport(result.Catalog, ownValues.Catalog, guids);
        AssertPipeGuidsResolve(result.Catalog, guids);

        using var process = Process.GetCurrentProcess();

        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Catalog: {result.Catalog.Count} entries, {result.Catalog.InheritedAssetCount} inherited assets left out, {result.Catalog.DroppedRegionTypeCount} region types dropped"));
        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Elapsed: {stopwatch.ElapsedMilliseconds} ms, peak working set: {process.PeakWorkingSet64 / (1024 * 1024)} MiB"));
    }

    /// <summary>
    /// On a real installation, every non-zero GUID of the pipe fixture resolves through <c>Load</c> and every region carries
    /// exactly one region type; the test writes counts, unresolved GUIDs, per region its GUID, its region asset GUID and
    /// its number of region types, the copies found in other archives, the elapsed time and the peak working set only; it
    /// also builds the catalog with the assets' own values (empty property and template files), fails when an entry of that
    /// build is missing, and writes the difference as counts and GUIDs.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC4")]
    public async Task GameCatalogReaderInstallationResolvesFixtureGuids()
    {
        // Arrange
        var installation = Environment.GetEnvironmentVariable(InstallationVariable);

        if (string.IsNullOrEmpty(installation))
        {
            Assert.Inconclusive($"{InstallationVariable} is not set: the catalog did not run on a real installation");
        }

        var guids = await ReadPipeGuidsAsync();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GameCatalogReader.Load(installation, RdaLimits.Default);

        stopwatch.Stop();

        // Assert
        Assert.IsNull(result.Error, $"The installation should give a catalog, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "The installation should give a catalog");

        using var process = Process.GetCurrentProcess();

        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Catalog: {result.Catalog.Count} entries, {result.Catalog.InheritedAssetCount} inherited assets left out, {result.Catalog.DroppedRegionTypeCount} region types dropped"));
        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Elapsed: {stopwatch.ElapsedMilliseconds} ms, peak working set: {process.PeakWorkingSet64 / (1024 * 1024)} MiB"));
        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Copies in other archives: {result.Copies.Count}, archives that could not be checked: {result.UncheckedArchiveCount}"));

        foreach (var copy in result.Copies)
        {
            TestContext.WriteLine($"Copy: {copy.ArchiveName} also lists {copy.Path}");
        }

        var ownValues = ReadInstallationWithoutDefaults(installation);

        Assert.IsNotNull(ownValues.Catalog, $"The installation with the assets' own values should give a catalog, but failed with {ownValues.Error?.Kind}");
        WriteLayerReport(result.Catalog, ownValues.Catalog, guids);
        AssertPipeGuidsResolve(result.Catalog, guids);
    }

    #endregion // AC1

    #region AC2

    /// <summary>
    /// A session of the template <c>SessionModerate</c> is a region entry with its region asset and region type.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesSessionAsRegion()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 201);

        // Assert
        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.AreEqual(GameCatalogKind.Region, entry.Kind, "The session should be a region entry");
        Assert.AreEqual(Base + 101, entry.RegionGuid, "The entry should carry the GUID of its region asset");
        Assert.AreSequenceEqual(["Zorlan"], entry.RegionTypes, "The entry should carry the region type its region asset states");
    }

    /// <summary>
    /// A session of the template <c>SessionModerateRandom</c> is a region entry as well.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesRandomSessionAsRegion()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 202);

        // Assert
        Assert.IsNotNull(entry, "The random session should be an entry");
        Assert.AreEqual(GameCatalogKind.Region, entry.Kind, "The random session should be a region entry");
        Assert.AreEqual(Base + 104, entry.RegionGuid, "The entry should carry the GUID of its region asset");
        Assert.AreSequenceEqual(["Yultic"], entry.RegionTypes, "The entry should carry the region type its region asset states");
    }

    /// <summary>
    /// A session that names region <c>0</c> or an asset that is no region asset is not an entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadSkipsSessionWithoutRegionAsset()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Assert
        Assert.IsNull(catalog.Find(Base + 205), "A session that names region 0 should not be an entry");
        Assert.IsNull(catalog.Find(Base + 206), "A session that names an asset that is no region asset should not be an entry");
        Assert.AreEqual(GameCatalogProblem.Unknown, catalog.Check(Base + 205), "A session that names region 0 should be unknown");
        Assert.AreEqual(GameCatalogProblem.Unknown, catalog.Check(Base + 206), "A session that names no region asset should be unknown");
    }

    /// <summary>
    /// A good carries every region type of its association list, in the order of the list.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesGoodWithEveryRegionType()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var many = catalog.Find(Base + 302);
        var one = catalog.Find(Base + 301);

        // Assert
        Assert.IsNotNull(many, "The good with three region types should be an entry");
        Assert.AreEqual(GameCatalogKind.Good, many.Kind, "A product should be a good");
        Assert.AreSequenceEqual(["Zorlan", "Yultic", "Xanthi"], many.RegionTypes, "The good should carry all three region types");
        Assert.IsNotNull(one, "The good with one region type should be an entry, also with IsWorkforce 0");
        Assert.AreEqual(GameCatalogKind.Good, one.Kind, "A product with IsWorkforce 0 should be a good");
        Assert.AreSequenceEqual(["Zorlan"], one.RegionTypes, "The good should carry its one region type");
    }

    /// <summary>
    /// A product with <c>IsWorkforce</c> 1 is a workforce entry with its region types.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesWorkforce()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 351);

        // Assert
        Assert.IsNotNull(entry, "The workforce should be an entry");
        Assert.AreEqual(GameCatalogKind.Workforce, entry.Kind, "A product with IsWorkforce 1 should be a workforce");
        Assert.AreSequenceEqual(["Yultic"], entry.RegionTypes, "The workforce should carry its region type");
    }

    /// <summary>
    /// A building carries the region types of its <c>AssociatedRegions</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesBuildingRegions()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 501);

        // Assert
        Assert.IsNotNull(entry, "The building should be an entry");
        Assert.AreEqual(GameCatalogKind.Building, entry.Kind, "An asset with a Building section should be a building");
        Assert.AreSequenceEqual(["Zorlan", "Xanthi"], entry.RegionTypes, "The building should carry the region types of its list");
    }

    /// <summary>
    /// A missing region list is "not stated" (null), for a good and for a building.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadKeepsMissingRegionListUnstated()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Assert
        Assert.IsNull(catalog.Find(Base + 303)?.RegionTypes, "A good without AssociatedRegion should have no region types stated");
        Assert.IsNull(catalog.Find(Base + 502)?.RegionTypes, "A building without AssociatedRegions should have no region types stated");
        Assert.IsNotNull(catalog.Find(Base + 303), "The good without a list should be an entry");
        Assert.IsNotNull(catalog.Find(Base + 502), "The building without a list should be an entry");
    }

    /// <summary>
    /// An explicitly empty region list (<c>&lt;AssociatedRegions /&gt;</c>) is "none": an empty list, not null.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadKeepsEmptyRegionListAsNone()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 503);

        // Assert
        Assert.IsNotNull(entry, "The building with an empty list should be an entry");
        Assert.IsNotNull(entry.RegionTypes, "An explicitly empty list should not read as not stated");
        Assert.IsEmpty(entry.RegionTypes, "An explicitly empty list should hold no region type");
    }

    /// <summary>
    /// <c>ProductionRegions</c> says where a good is produced and is not read: a good that has only that list states no
    /// region types, one that has both carries the association list alone.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadIgnoresProductionRegions()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Assert
        Assert.IsNull(catalog.Find(Base + 303)?.RegionTypes, "A good with only ProductionRegions should have no region types stated");
        Assert.DoesNotContain("Xanthi", catalog.Find(Base + 301)?.RegionTypes ?? [], "A good should not carry the region types of its ProductionRegions");
    }

    /// <summary>
    /// A region type name that the <c>Region</c> data set does not hold is dropped from the entry and counted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadDropsUnknownRegionType()
    {
        // Arrange
        var assets = Assets(Good(10, 1000, "Zorlan;Vexan;Vexan"));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Good 10")), Texts((1000, "Ware 10")));

        // Assert
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(10)?.RegionTypes, "The unknown region type should be dropped from the entry");
        Assert.AreEqual(2, catalog.DroppedRegionTypeCount, "Each dropped token should be counted");
    }

    /// <summary>
    /// A <c>RegionID</c> that the <c>Region</c> data set does not hold is dropped and counted: the region entry has an
    /// empty list of region types.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadDropsUnknownRegionId()
    {
        // Arrange
        var assets = Assets(RegionAsset(5, 1000, "Vexan"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Region 5")), Texts((1000, "Gebiet 5")));

        // Assert
        var entry = catalog.Find(6);

        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.IsNotNull(entry.RegionTypes, "A dropped RegionID should leave an empty list, not no list");
        Assert.IsEmpty(entry.RegionTypes, "The unknown RegionID should be dropped from the entry");
        Assert.AreEqual(1, catalog.DroppedRegionTypeCount, "The dropped RegionID should be counted");
    }

    /// <summary>
    /// The region data set is the data set whose own name is <c>Region</c>: another data set that has an item named
    /// <c>Region</c> and other items is not it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadFindsRegionDataSetByOwnName()
    {
        // Arrange
        var dataSets = $"<DataSets><Group><Name>Global</Name>{DataSet("Other", "Region", "Vexan")}{DataSet("Region", "Zorlan")}</Group></DataSets>";
        var assets = Assets(Good(10, 1000, "Region;Vexan;Zorlan"));

        // Act
        var catalog = ReadCatalog(dataSets, assets, Texts((1000, "Good 10")), Texts((1000, "Ware 10")));

        // Assert
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(10)?.RegionTypes, "Only the items of the data set named Region should be region types");
        Assert.AreEqual(2, catalog.DroppedRegionTypeCount, "The names of the other data set should be dropped and counted");
    }

    /// <summary>
    /// A negative GUID is read as an asset GUID.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesNegativeGuid()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(-(Base + 401));

        // Assert
        Assert.IsNotNull(entry, "The negative GUID should be an entry");
        Assert.AreEqual(GameCatalogKind.Good, entry.Kind, "The negative GUID should be a good");
        Assert.AreEqual("Good 401", entry.EnglishName, "The negative GUID should have its English name");
        Assert.AreEqual("Ware 401", entry.GermanName, "The negative GUID should have its German name");
    }

    /// <summary>
    /// The fixture gives exactly its entries with their kinds, names and problems, and no other entry, and the counts
    /// of its inherited asset and dropped region types.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadResolvesFixtureEntries()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Assert
        Assert.AreEqual(_fixtureEntries.Length, catalog.Count, "The catalog should hold the entries of the fixture and no other");

        foreach (var expected in _fixtureEntries)
        {
            var entry = catalog.Find(expected.Guid);

            Assert.IsNotNull(entry, $"GUID {expected.Guid} should be an entry");
            Assert.AreEqual(expected.Kind, entry.Kind, $"GUID {expected.Guid} should have its kind");
            Assert.AreEqual(expected.English ?? string.Empty, entry.EnglishName ?? string.Empty, $"GUID {expected.Guid} should have its English name");
            Assert.AreEqual(expected.German ?? string.Empty, entry.GermanName ?? string.Empty, $"GUID {expected.Guid} should have its German name");
            Assert.AreEqual(expected.Problem, catalog.Check(expected.Guid), $"GUID {expected.Guid} should give its problem");
        }

        Assert.AreEqual(1, catalog.InheritedAssetCount, "The fixture's inherited asset should be counted");
        Assert.AreEqual(2, catalog.DroppedRegionTypeCount, "The fixture's unknown type token and unknown RegionID should be counted");
    }

    /// <summary>
    /// No GUID of the fixture occurs in the pipe fixture, and no region type name of it is one of the game's.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task GameCatalogReaderFixtureGuidsAreNotInPipeFixture()
    {
        // Arrange
        var pipe = await ReadPipeGuidsAsync();
        var pipeGuids = pipe.Regions.Concat(pipe.Goods).Concat(pipe.Workforce).Concat(pipe.Buildings).ToHashSet();
        var fixtureGuids = new HashSet<int>();

        foreach (var value in CollectValues(FixtureBytes(_fileNames[1]), ["GUID", "BaseAssetGUID", "Region"]))
        {
            if (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var guid))
            {
                fixtureGuids.Add(guid);
            }
        }

        var regionTypes = CollectValues(FixtureBytes(_fileNames[0]), ["Name"]);

        // Assert
        Assert.IsGreaterThan(20, fixtureGuids.Count, "The fixture should hold GUIDs");
        Assert.IsNotEmpty(pipeGuids, "The pipe fixture should hold GUIDs");
        Assert.AreEqual(0, fixtureGuids.Where(guid => guid != 0).Count(pipeGuids.Contains), "No GUID of the fixture should occur in the pipe fixture");
        Assert.IsEmpty(regionTypes.Intersect(["Meta", "Roman", "Celtic", "Egyptian"]), "No region type name of the fixture should be one of the game's");
    }

    /// <summary>
    /// A GUID is read with its surrounding whitespace trimmed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadTrimsGuidWhitespace()
    {
        // Arrange
        var asset = "<Asset><Template>Product</Template><Values><Standard><GUID>\n  10 </GUID></Standard><Text><OasisId> 1000 </OasisId></Text><Product /></Values></Asset>";

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), Assets(asset), "<TextExport><Texts><Text><LineId> 1000\n</LineId><Text>Good 10</Text></Text></Texts></TextExport>", Texts((1000, "Ware 10")));

        // Assert
        Assert.AreEqual("Good 10", catalog.Find(10)?.EnglishName, "A GUID and identifiers with surrounding whitespace should be read");
        Assert.AreEqual("Ware 10", catalog.Find(10)?.GermanName, "The text identifier of the asset should be trimmed too");
    }

    /// <summary>
    /// A session whose <c>Region</c> element is empty names no region and is no entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadSkipsSessionWithEmptyRegion()
    {
        // Arrange
        var session = "<Asset><Template>SessionModerate</Template><Values><Standard><GUID>6</GUID></Standard><Text><OasisId>1000</OasisId></Text><Session><Region /></Session></Values></Asset>";
        var assets = Assets(RegionAsset(5, 1000, "Zorlan"), session, Good(10, 1000, null));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Good 10")), Texts((1000, "Ware 10")));

        // Assert
        Assert.IsNull(catalog.Find(6), "A session with an empty Region element should not be an entry");
        Assert.AreEqual(1, catalog.Count, "Only the good should be an entry");
        Assert.AreEqual(0, catalog.DroppedRegionTypeCount, "Nothing should be dropped");
    }

    /// <summary>
    /// A region asset that several sessions use counts a dropped <c>RegionID</c> once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadCountsDroppedRegionIdOncePerRegionAsset()
    {
        // Arrange
        var assets = Assets(RegionAsset(5, 1000, "Vexan"), Session(6, "SessionModerate", 5, 1000), Session(7, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Region 5")), Texts((1000, "Gebiet 5")));

        // Assert
        Assert.AreEqual(2, catalog.Count, "Both sessions should be entries");
        Assert.AreEqual(1, catalog.DroppedRegionTypeCount, "The dropped RegionID should be counted once per region asset");
    }

    #endregion // AC2

    #region AC3

    /// <summary>
    /// A GUID defined by two assets is ambiguous.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadMarksDuplicateGuidAmbiguous()
    {
        // Arrange
        var assets = Assets(Good(10, 1000, null), Good(10, 1001, null), Good(11, 1000, null));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Good 10"), (1001, "Good 11")), Texts((1000, "Ware 10"), (1001, "Ware 11")));

        // Assert
        Assert.AreEqual(GameCatalogProblem.Ambiguous, catalog.Check(10), "A GUID defined twice should be ambiguous");
        Assert.AreEqual(GameCatalogProblem.None, catalog.Check(11), "A GUID defined once should be unaffected");
        Assert.AreEqual(1, catalog.Count, "The ambiguous GUID should not be counted as an entry");
    }

    /// <summary>
    /// A text identifier with two different texts in one language makes its assets ambiguous.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadMarksDifferingTextAmbiguous()
    {
        // Arrange
        var assets = Assets(Good(10, 1000, null), Good(11, 1001, null));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Good 10"), (1000, "Good 99"), (1001, "Good 11")), Texts((1000, "Ware 10"), (1001, "Ware 11")));

        // Assert
        Assert.AreEqual(GameCatalogProblem.Ambiguous, catalog.Check(10), "A text identifier with two different texts should make the GUID ambiguous");
        Assert.AreEqual(GameCatalogProblem.None, catalog.Check(11), "A GUID with an unambiguous text should be unaffected");
        Assert.AreEqual(1, catalog.Count, "The ambiguous GUID should not be counted as an entry");
    }

    /// <summary>
    /// A text identifier repeated with the same text is no ambiguity.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadAcceptsRepeatedSameText()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 309);

        // Assert
        Assert.AreEqual(GameCatalogProblem.None, catalog.Check(Base + 309), "A text identifier repeated with the same text should not be ambiguous");
        Assert.AreEqual("Good 309", entry?.EnglishName, "The repeated text should be the name");
    }

    /// <summary>
    /// A text identifier shared by several assets is no ambiguity: two goods, and a session with its region asset.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadAcceptsSharedTextId()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Assert
        Assert.AreEqual(GameCatalogProblem.None, catalog.Check(Base + 306), "The first good of a shared text identifier should resolve");
        Assert.AreEqual(GameCatalogProblem.None, catalog.Check(Base + 307), "The second good of a shared text identifier should resolve");
        Assert.AreEqual(catalog.Find(Base + 306)?.EnglishName, catalog.Find(Base + 307)?.EnglishName, "Both goods should have the shared name");
        Assert.AreEqual("Gebiet 101", catalog.Find(Base + 201)?.GermanName, "A session should resolve with the text it shares with its region asset");
    }

    /// <summary>
    /// An asset without a text identifier, one whose texts are both empty, one whose identifier is in neither file
    /// are nameless, and one whose text is missing from one language is incomplete.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadTreatsMissingLineIdAsNoName()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Assert
        Assert.AreEqual(GameCatalogProblem.Nameless, catalog.Check(Base + 311), "An asset without a text identifier should be nameless");
        Assert.AreEqual(GameCatalogProblem.Nameless, catalog.Check(Base + 312), "An asset whose texts are both empty should be nameless");
        Assert.AreEqual(GameCatalogProblem.Nameless, catalog.Check(Base + 313), "An asset whose text identifier is in neither file should be nameless");
        Assert.AreEqual(GameCatalogProblem.Incomplete, catalog.Check(Base + 305), "An asset whose text identifier is missing from the German file should be incomplete");
        Assert.AreEqual(GameCatalogProblem.Incomplete, catalog.Check(Base + 308), "An asset whose English text is empty should be incomplete");
        Assert.IsTrue(string.IsNullOrEmpty(catalog.Find(Base + 305)?.GermanName), "The missing German name should be absent");
        Assert.AreEqual("Ware 308", catalog.Find(Base + 308)?.GermanName, "The German name should be kept");
        Assert.IsNotNull(catalog.Find(Base + 311), "A nameless asset should still be an entry");
    }

    /// <summary>
    /// An asset that inherits from a base asset (no template of its own) is left out and counted, even with a
    /// building section and a text identifier; an asset with a template and a base asset is not left out.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadLeavesInheritedAssetOut()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();
        var withTemplate = "<Asset><BaseAssetGUID>9</BaseAssetGUID><Template>Product</Template><Values><Standard><GUID>10</GUID></Standard><Text><OasisId>1000</OasisId></Text><Product /></Values></Asset>";

        // Act
        var other = ReadCatalog(DataSets("Zorlan"), Assets(withTemplate), Texts((1000, "Good 10")), Texts((1000, "Ware 10")));

        // Assert
        Assert.IsNull(catalog.Find(Base + 601), "The inherited asset should not be an entry");
        Assert.AreEqual(GameCatalogProblem.Unknown, catalog.Check(Base + 601), "The inherited asset should be unknown");
        Assert.AreEqual(1, catalog.InheritedAssetCount, "The inherited asset should be counted");
        Assert.IsNotNull(other.Find(10), "An asset with a template is no inherited asset");
        Assert.AreEqual(0, other.InheritedAssetCount, "An asset with a template should not be counted as inherited");
    }

    /// <summary>
    /// A GUID defined twice by region assets is ambiguous.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadMarksDuplicateRegionAssetAmbiguous()
    {
        // Arrange
        var assets = Assets(RegionAsset(5, 1000, "Zorlan"), RegionAsset(5, 1001, "Zorlan"), RegionAsset(8, 1000, "Zorlan"));

        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), assets, Texts((1000, "Region 5")), Texts((1000, "Gebiet 5")));

        // Assert
        Assert.AreEqual(GameCatalogProblem.Ambiguous, catalog.Check(5), "A region asset GUID defined twice should be ambiguous");
        Assert.AreNotEqual(GameCatalogProblem.Ambiguous, catalog.Check(8), "A region asset GUID defined once should not be ambiguous");
    }

    /// <summary>
    /// Text is read with entities unescaped.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadUnescapesEntitiesInText()
    {
        // Act
        var catalog = ReadCatalog(DataSets("Zorlan"), Assets(Good(10, 1000, null)), Texts((1000, "Good &lt;10&gt; &amp; Co")), Texts((1000, "Ware &quot;10&quot;")));

        // Assert
        Assert.AreEqual("Good <10> & Co", catalog.Find(10)?.EnglishName, "The entities of the English text should be unescaped");
        Assert.AreEqual("Ware \"10\"", catalog.Find(10)?.GermanName, "The entities of the German text should be unescaped");
    }

    /// <summary>
    /// Empty elements, an asset without a GUID and unrelated content do not change what the files give.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadIgnoresEmptyAndUnrelatedElements()
    {
        // Arrange
        var dataSets = $"<DataSets><Group><Name>Global</Name><DataSet />{DataSet("Region", "Zorlan")}</Group></DataSets>";
        var assets = Assets("<Asset />", "<Asset><Template>Product</Template><Values><Product /></Values></Asset>", Good(10, 1000, "Zorlan"));
        var english = "<TextExport><Texts><Text /><Text><Note><A /></Note>stray<Text>Good 10</Text><LineId>1000</LineId></Text></Texts></TextExport>";

        // Act
        var catalog = ReadCatalog(dataSets, assets, english, Texts((1000, "Ware 10")));

        // Assert
        Assert.AreEqual(1, catalog.Count, "Only the good should be an entry");
        Assert.AreEqual("Good 10", catalog.Find(10)?.EnglishName, "A text that comes before its LineId should be read");
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(10)?.RegionTypes, "The region type should be kept");
    }

    #endregion // AC3

    #region AC6

    /// <summary>
    /// An installation without <c>config.rda</c>, and one without a <c>maindata</c> folder, gives the archive error.
    /// </summary>
    /// <param name="createMainData">Whether the <c>maindata</c> folder exists</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(true)]
    [DataRow(false)]
    public void GameCatalogReaderLoadFailsWithoutConfigArchive(bool createMainData)
    {
        // Arrange
        Directory.CreateDirectory(createMainData ? MainData : _root);

        // Act
        var result = GameCatalogReader.Load(_root, RdaLimits.Default);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.ArchiveUnreadable, result.Error?.Kind, "A missing config.rda should be an unreadable archive");
        Assert.AreEqual(ArchiveMessage, result.Error?.Message, "The message should be the fixed text of an unreadable archive");
    }

    /// <summary>
    /// A <c>config.rda</c> that does not list one of the six files gives a missing-file error.
    /// </summary>
    /// <param name="missing">The index of the file the archive lacks</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderLoadFailsForMissingFile(int missing)
    {
        // Arrange
        WriteConfigArchive(missing);

        // Act
        var result = GameCatalogReader.Load(_root, RdaLimits.Default);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.FileMissing, result.Error?.Kind, "A file the archive does not list should be missing");
        Assert.AreEqual(MissingMessage, result.Error?.Message, "The message should be the fixed text of a missing file");
    }

    /// <summary>
    /// A file above the per-entry bound of the limits gives an unreadable-file error.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderLoadFailsAboveEntryBound()
    {
        // Arrange
        WriteConfigArchive(-1);

        var bound = FixtureBytes(_fileNames[0]).Length + 1;
        var limits = new RdaLimits(RdaLimits.Default.MaxBlocks, RdaLimits.Default.MaxEntries, RdaLimits.Default.MaxDirectoryBytes, bound);

        // Act
        var result = GameCatalogReader.Load(_root, limits);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.FileUnreadable, result.Error?.Kind, "A file above the bound should be unreadable");
        Assert.AreEqual(UnreadableMessage, result.Error?.Message, "The message should be the fixed text of an unreadable file");
    }

    /// <summary>
    /// A malformed document gives the malformed-file kind with a fixed message that holds none of the content.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadFailsForMalformedXml()
    {
        // Act
        var first = ReadWithFile(1, $"<AssetList><Groups>{Secret}<Group></AssetList>");
        var second = ReadWithFile(1, $"<AssetList><Groups><Group><Assets><Asset>{Secret}</Assets></Group></Groups></AssetList>");
        var wrongRoot = ReadWithFile(1, $"<{Secret} />");

        // Assert
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, first.Error?.Kind, "A document that is not well-formed should be a malformed file");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, second.Error?.Kind, "A second malformed document should be a malformed file");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, wrongRoot.Error?.Kind, "A wrong root element should be a malformed file");
        Assert.IsFalse(string.IsNullOrEmpty(first.Error?.Message), "The error should carry a message");
        Assert.AreEqual(first.Error?.Message, second.Error?.Message, "The message should be one fixed text, whatever the document");
        Assert.AreEqual(first.Error?.Message, wrongRoot.Error?.Message, "The message of a wrong root should be the same fixed text");
        Assert.AreEqual(MalformedMessage, first.Error?.Message, "The message should be the fixed text of a malformed file");
        Assert.IsFalse(first.Error?.Message.Contains(Secret, StringComparison.Ordinal) ?? true, "The message should hold no content of the document");
    }

    /// <summary>
    /// A document type declaration in any of the six files gives the malformed-file kind.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadFailsForDocumentTypeDeclaration(int file)
    {
        // Arrange
        var root = new[] { "DataSets", "AssetList", "TextExport", "TextExport", "Properties", "Templates" }[file];
        var content = $"<!DOCTYPE {root} [<!ENTITY x \"{Secret}\">]><{root} />";

        // Act
        var result = ReadWithFile(file, content);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A document type declaration should be a malformed file");
        Assert.IsFalse(result.Error?.Message.Contains(Secret, StringComparison.Ordinal) ?? true, "The message should hold no content of the document");
    }

    /// <summary>
    /// A GUID or text identifier that is not an integer of its type gives the malformed-file kind.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="value">The value</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(1, "abc")]
    [DataRow(1, "99999999999")]
    [DataRow(1, "1.5")]
    [DataRow(1, "")]
    [DataRow(2, "abc")]
    [DataRow(3, "99999999999999999999")]
    public void GameCatalogReaderReadFailsForNonIntegerGuid(int file, string value)
    {
        // Arrange
        var content = file == 1
                          ? Assets($"<Asset><Template>Product</Template><Values><Standard><GUID>{value}</GUID></Standard><Product /></Values></Asset>")
                          : $"<TextExport><Texts><Text><LineId>{value}</LineId><Text>Good 1</Text></Text></Texts></TextExport>";

        // Act
        var result = ReadWithFile(file, content);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A value that is not an integer of its type should be a malformed file");
    }

    /// <summary>
    /// A data set file without a <c>Region</c> data set gives the malformed-file kind.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadFailsWithoutRegionDataSet()
    {
        // Act
        var result = ReadWithFile(0, $"<DataSets><Group><Name>Global</Name>{DataSet("Other", "Region", "Zorlan")}</Group></DataSets>");

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A file without a Region data set should be a malformed file");
    }

    /// <summary>
    /// A data set file with two <c>Region</c> data sets gives the malformed-file kind.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadFailsForTwoRegionDataSets()
    {
        // Act
        var result = ReadWithFile(0, $"<DataSets><Group><Name>Global</Name>{DataSet("Region", "Zorlan")}{DataSet("Region", "Yultic")}</Group></DataSets>");

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A file with two Region data sets should be a malformed file");
    }

    /// <summary>
    /// An asset nested deeper than <see cref="GameCatalogReader.MaxAssetDepth" /> gives the malformed-file kind.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadFailsForTooDeepAsset()
    {
        // Arrange
        var depth = GameCatalogReader.MaxAssetDepth + 10;
        var nested = string.Concat(Enumerable.Repeat("<N>", depth)) + string.Concat(Enumerable.Repeat("</N>", depth));
        var asset = $"<Asset><Template>Product</Template><Values><Standard><GUID>10</GUID></Standard>{nested}</Values></Asset>";

        // Act
        var result = ReadWithFile(1, Assets(asset));

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "An asset deeper than the bound should be a malformed file");
    }

    /// <summary>
    /// An asset with more than <see cref="GameCatalogReader.MaxAssetElements" /> elements gives the malformed-file kind.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadFailsForTooLargeAsset()
    {
        // Arrange
        var wide = string.Concat(Enumerable.Repeat("<A />", GameCatalogReader.MaxAssetElements + 10));
        var asset = $"<Asset><Template>Product</Template><Values><Standard><GUID>10</GUID></Standard>{wide}</Values></Asset>";

        // Act
        var result = ReadWithFile(1, Assets(asset));

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "An asset with more elements than the bound should be a malformed file");
    }

    /// <summary>
    /// The six files are read forward-only: streams that cannot seek and do not know their length give the same
    /// catalog as seekable ones.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadAcceptsNonSeekableStreams()
    {
        // Act
        var result = GameCatalogReader.Read(() => GameCatalogSource.FromStream(new NonSeekableStream(new MemoryStream(FixtureBytes(_fileNames[0])))),
                                            () => GameCatalogSource.FromStream(new NonSeekableStream(new MemoryStream(FixtureBytes(_fileNames[1])))),
                                            () => GameCatalogSource.FromStream(new NonSeekableStream(new MemoryStream(FixtureBytes(_fileNames[2])))),
                                            () => GameCatalogSource.FromStream(new NonSeekableStream(new MemoryStream(FixtureBytes(_fileNames[3])))),
                                            () => GameCatalogSource.FromStream(new NonSeekableStream(new MemoryStream(FixtureBytes(_fileNames[4])))),
                                            () => GameCatalogSource.FromStream(new NonSeekableStream(new MemoryStream(FixtureBytes(_fileNames[5])))));

        // Assert
        Assert.IsNull(result.Error, $"Non-seekable streams should be read, but failed with {result.Error?.Kind}");
        Assert.AreEqual(_fixtureEntries.Length, result.Catalog?.Count, "The catalog from non-seekable streams should hold the entries of the fixture");
        Assert.AreEqual("Ware 301", result.Catalog?.Find(Base + 301)?.GermanName, "The catalog from non-seekable streams should hold the names");
    }

    /// <summary>
    /// Each file is opened when its pass starts, in the order data sets, properties, templates, assets, English, German, and
    /// its stream is disposed before the next opener is called.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    [TestCategory("AC6")]
    public void GameCatalogReaderReadOpensOneFileAtATime()
    {
        // Arrange
        var log = new List<string>();

        Func<GameCatalogSource> Opener(int index)
        {
            return () =>
                   {
                       log.Add($"open {_fileNames[index]}");

                       return GameCatalogSource.FromStream(new TrackingStream(FixtureBytes(_fileNames[index]), $"close {_fileNames[index]}", log));
                   };
        }

        // Act
        var result = GameCatalogReader.Read(Opener(0), Opener(1), Opener(2), Opener(3), Opener(4), Opener(5));

        // Assert
        Assert.IsNotNull(result.Catalog, "The fixture should give a catalog");
        Assert.AreSequenceEqual([
                                    "open datasets.xml",
                                    "close datasets.xml",
                                    "open properties.xml",
                                    "close properties.xml",
                                    "open templates.xml",
                                    "close templates.xml",
                                    "open assets.xml",
                                    "close assets.xml",
                                    "open texts_english.xml",
                                    "close texts_english.xml",
                                    "open texts_german.xml",
                                    "close texts_german.xml"
                                ],
                                log,
                                "Each stream should be disposed before the next opener is called");
    }

    /// <summary>
    /// A source with an error kind ends the read with that kind and its fixed message; earlier streams are disposed
    /// and no later opener is called.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="kind">The error kind of the source</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0, GameCatalogErrorKind.FileMissing)]
    [DataRow(1, GameCatalogErrorKind.FileMissing)]
    [DataRow(2, GameCatalogErrorKind.FileMissing)]
    [DataRow(3, GameCatalogErrorKind.FileMissing)]
    [DataRow(1, GameCatalogErrorKind.MalformedFile)]
    [DataRow(3, GameCatalogErrorKind.MalformedFile)]
    [DataRow(0, GameCatalogErrorKind.FileUnreadable)]
    [DataRow(2, GameCatalogErrorKind.FileUnreadable)]
    [DataRow(0, GameCatalogErrorKind.ArchiveUnreadable)]
    [DataRow(3, GameCatalogErrorKind.ArchiveUnreadable)]
    [DataRow(4, GameCatalogErrorKind.FileMissing)]
    [DataRow(5, GameCatalogErrorKind.FileMissing)]
    [DataRow(4, GameCatalogErrorKind.MalformedFile)]
    [DataRow(5, GameCatalogErrorKind.MalformedFile)]
    [DataRow(4, GameCatalogErrorKind.FileUnreadable)]
    [DataRow(5, GameCatalogErrorKind.ArchiveUnreadable)]
    public void GameCatalogReaderReadEndsWithKindOfSourceError(int file, GameCatalogErrorKind kind)
    {
        // Arrange
        var log = new List<string>();

        // Act
        var result = ReadLogged(log, index => index == file ? GameCatalogSource.FromError(kind) : null);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(kind, result.Error?.Kind, "The read should end with the kind of the source");
        Assert.AreEqual(MessageOf(kind), result.Error?.Message, "The message should be the fixed text of the kind");
        Assert.AreSequenceEqual(ExpectedLog(file, false), log, "Earlier streams should be disposed and no later opener called");
    }

    /// <summary>
    /// An opener that throws gives the unreadable-file kind with its fixed message and no content of the exception;
    /// no later opener is called.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadGivesFileUnreadableWhenOpenerThrows(int file)
    {
        // Arrange
        var log = new List<string>();

        // Act
        var result = ReadLogged(log, index => index == file ? throw new InvalidOperationException(Secret) : null);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.FileUnreadable, result.Error?.Kind, "An opener that throws should give an unreadable file");
        Assert.AreEqual(UnreadableMessage, result.Error?.Message, "The message should be the fixed text and hold no content of the exception");
        Assert.AreSequenceEqual(ExpectedLog(file, false), log, "Earlier streams should be disposed and no later opener called");
    }

    /// <summary>
    /// A pass that fails on malformed content disposes its stream and no later opener is called.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadDisposesStreamOfFailedPass(int file)
    {
        // Arrange
        var log = new List<string>();

        // Act
        var result = ReadLogged(log, index => index == file ? GameCatalogSource.FromStream(new TrackingStream(Encoding.UTF8.GetBytes("<Wrong />"), $"close {_fileNames[index]}", log)) : null);

        // Assert
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A wrong root should be a malformed file");
        Assert.AreEqual(MalformedMessage, result.Error?.Message, "The message should be the fixed text");
        Assert.AreSequenceEqual(ExpectedLog(file, true), log, "The stream of the failed pass should be disposed and no later opener called");
    }

    /// <summary>
    /// A defining part with exactly <see cref="GameCatalogReader.MaxAssetElements" /> elements is read.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadAcceptsPartAtElementBound(int file)
    {
        // Arrange
        var filler = string.Concat(Enumerable.Repeat("<A />", GameCatalogReader.MaxAssetElements - BaseElements(file)));

        // Act
        var result = ReadWithFile(file, BoundedContent(file, filler));

        // Assert
        Assert.IsNull(result.Error, $"A part at the element bound should be read, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "A catalog should be built");
    }

    /// <summary>
    /// A defining part with one element more than <see cref="GameCatalogReader.MaxAssetElements" /> is malformed.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadFailsOneAboveElementBound(int file)
    {
        // Arrange
        var filler = string.Concat(Enumerable.Repeat("<A />", GameCatalogReader.MaxAssetElements - BaseElements(file) + 1));

        // Act
        var result = ReadWithFile(file, BoundedContent(file, filler));

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A part one element above the bound should be malformed");
    }

    /// <summary>
    /// A defining part nested exactly <see cref="GameCatalogReader.MaxAssetDepth" /> deep is read.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadAcceptsPartAtDepthBound(int file)
    {
        // Act
        var result = ReadWithFile(file, BoundedContent(file, Nested(GameCatalogReader.MaxAssetDepth)));

        // Assert
        Assert.IsNull(result.Error, $"A part at the depth bound should be read, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "A catalog should be built");
    }

    /// <summary>
    /// A defining part nested one level deeper than <see cref="GameCatalogReader.MaxAssetDepth" /> is malformed.
    /// </summary>
    /// <param name="file">The index of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void GameCatalogReaderReadFailsOneAboveDepthBound(int file)
    {
        // Act
        var result = ReadWithFile(file, BoundedContent(file, Nested(GameCatalogReader.MaxAssetDepth + 1)));

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A part one level above the bound should be malformed");
    }

    /// <summary>
    /// Elements outside the defining parts may nest to the depth bound; deeper is malformed.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="extra">The levels above the bound</param>
    /// <param name="malformed">Whether the file is malformed</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0, 0, false)]
    [DataRow(0, 1, true)]
    [DataRow(1, 0, false)]
    [DataRow(1, 1, true)]
    [DataRow(2, 0, false)]
    [DataRow(2, 1, true)]
    [DataRow(3, 0, false)]
    [DataRow(3, 1, true)]
    [DataRow(4, 0, false)]
    [DataRow(4, 1, true)]
    [DataRow(5, 0, false)]
    [DataRow(5, 1, true)]
    public void GameCatalogReaderReadBoundsDepthOutsideDefinitions(int file, int extra, bool malformed)
    {
        // Arrange
        var chain = Nested(GameCatalogReader.MaxAssetDepth + extra);
        var content = file switch
                      {
                          0 => $"<DataSets>{chain}<Group><Name>Global</Name>{DataSet("Region", "Zorlan")}</Group></DataSets>",
                          1 => $"<AssetList>{chain}</AssetList>",
                          2 or 3 => $"<TextExport>{chain}</TextExport>",
                          4 => $"<Properties>{chain}</Properties>",
                          _ => $"<Templates>{chain}</Templates>"
                      };

        // Act
        var result = ReadWithFile(file, content);

        // Assert
        Assert.AreEqual(malformed, result.Error?.Kind == GameCatalogErrorKind.MalformedFile, "Only nesting above the bound should be malformed");
        Assert.AreEqual(malformed, result.Catalog is null, "A catalog should be built exactly when the file is not malformed");
    }

    /// <summary>
    /// An archive that cannot be opened is reported as unchecked and does not stop the catalog.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderLoadCountsUnreadableOtherArchive()
    {
        // Arrange
        WriteConfigArchive(-1);
        File.WriteAllBytes(Path.Combine(MainData, "patch2.rda"), Encoding.UTF8.GetBytes("not an archive"));

        // Act
        var result = GameCatalogReader.Load(_root, RdaLimits.Default);

        // Assert
        Assert.IsNotNull(result.Catalog, $"The installation should give a catalog, but failed with {result.Error?.Kind}");
        Assert.AreEqual(1, result.UncheckedArchiveCount, "The archive that could not be opened should be counted");
        Assert.IsEmpty(result.Copies, "No copy should be reported");
    }

    /// <summary>
    /// An installation path that cannot name a file gives the archive error with its fixed message and no path.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogReaderLoadGivesArchiveErrorForInvalidPath()
    {
        // Act
        var result = GameCatalogReader.Load($"{_root}\0{Secret}", RdaLimits.Default);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.ArchiveUnreadable, result.Error?.Kind, "An invalid path should be an unreadable archive");
        Assert.AreEqual(ArchiveMessage, result.Error?.Message, "The message should be the fixed text and hold no path");
    }

    #endregion // AC6

    #region AC7

    /// <summary>
    /// Loading a catalog changes nothing in the installation: no file is created, written or removed. The archive
    /// copies of another archive are reported.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameCatalogReaderLoadWritesNoFile()
    {
        // Arrange
        WriteConfigArchive(-1);
        File.WriteAllBytes(Path.Combine(MainData, "patch1.rda"), new RdaArchiveBuilder().AddBlock(0, (GameCatalogReader.AssetsPath, RdaArchiveBuilder.Content("<AssetList />"))).Build().Bytes);

        var before = Snapshot();
        var workingFolder = Environment.CurrentDirectory;
        var workingBefore = Directory.GetFiles(workingFolder).Length;

        // Act
        var result = GameCatalogReader.Load(_root, RdaLimits.Default);

        // Assert
        Assert.IsNotNull(result.Catalog, $"The installation should give a catalog, but failed with {result.Error?.Kind}");
        Assert.HasCount(1, result.Copies, "The copy in the other archive should be reported");
        Assert.Contains("patch1", result.Copies[0].ArchiveName, StringComparison.OrdinalIgnoreCase, "The copy should name the other archive");
        Assert.AreEqual(GameCatalogReader.AssetsPath, result.Copies[0].Path, "The copy should name the path");
        Assert.AreSequenceEqual(before, Snapshot(), "The installation should be unchanged");
        Assert.HasCount(workingBefore, Directory.GetFiles(workingFolder), "No file should be written to the working folder");
    }

    /// <summary>
    /// With the sample files at hand, no name, text, icon path, GUID or text identifier of the fixture occurs in them;
    /// the test writes counts only.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameCatalogReaderFixtureHoldsNoSampleText()
    {
        // Arrange
        var folder = Environment.GetEnvironmentVariable(SampleVariable);

        if (string.IsNullOrEmpty(folder))
        {
            Assert.Inconclusive($"{SampleVariable} is not set: the fixture was not compared with the sample files");
        }

        string[] assetNames = ["GUID", "Name", "IconFilename", "OasisId", "RegionID"];
        string[] dataSetNames = ["Name"];
        string[] textNames = ["LineId", "Text"];
        string[] defaultNames = ["RegionID", "AssociatedRegions", "AssociatedRegion", "Region"];
        string[][] names = [dataSetNames, assetNames, textNames, textNames, defaultNames, defaultNames];
        var shared = 0;

        // Act
        for (var index = 0; index < _fileNames.Length; index++)
        {
            using var sample = File.OpenRead(Path.Combine(folder, _fileNames[index]));

            var sampleValues = CollectValues(sample, names[index]);
            var fixtureValues = CollectValues(FixtureBytes(_fileNames[index]), names[index]);

            if (index == 0)
            {
                fixtureValues.ExceptWith(["Global", "Region"]);
            }

            if (index >= 4)
            {
                fixtureValues.ExceptWith(["0"]);
            }

            var count = fixtureValues.Count(sampleValues.Contains);

            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{_fileNames[index]}: {fixtureValues.Count} fixture values, {sampleValues.Count} sample values, {count} in both"));
            shared += count;
        }

        // Assert
        Assert.AreEqual(0, shared, "No value of the fixture should occur in the sample files");
    }

    /// <summary>
    /// Every text, name and path in the fixture is invented: it has the shape of the invented names, and the
    /// region type names are the invented ones.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameCatalogReaderFixtureTextsAreInvented()
    {
        // Arrange
        var assets = FixtureBytes(_fileNames[1]);
        var texts = new[] { FixtureBytes(_fileNames[2]), FixtureBytes(_fileNames[3]) };

        // Act
        var names = CollectValues(assets, ["Name"]);
        var icons = CollectValues(assets, ["IconFilename"]);
        var regionIds = CollectValues(assets, ["RegionID", "AssociatedRegion", "AssociatedRegions", "RegionType"]).SelectMany(value => value.Split(';')).ToHashSet();
        string[] regionNames = ["RegionID", "AssociatedRegions", "AssociatedRegion"];
        var defaultRegionIds = CollectValues(FixtureBytes(_fileNames[4]), regionNames).Concat(CollectValues(FixtureBytes(_fileNames[5]), regionNames)).SelectMany(value => value.Split(';')).ToHashSet();
        var templateTexts = CollectValues(FixtureBytes(_fileNames[5]), ["Description"]).Concat(CollectValues(FixtureBytes(_fileNames[4]), ["Name"]).Where(value => value.StartsWith("Property ", StringComparison.Ordinal))).ToList();
        var textValues = texts.SelectMany(bytes => CollectValues(bytes, ["Text"])).ToList();
        var dataSetNames = CollectValues(FixtureBytes(_fileNames[0]), ["Name"]);

        // Assert
        Assert.IsTrue(names.Count > 10 && icons.Count > 10 && textValues.Count > 10, "The fixture should hold names, icons and texts");
        Assert.AreEqual(0, names.Count(name => IsInvented(name, @"^(Region|Session|Good|Building|Collectable) \d+$") == false), "Every asset name should be an invented name");
        Assert.AreEqual(0, icons.Count(icon => IsInvented(icon, @"^data/fixture/icon_\d+\.png$") == false), "Every icon path should be an invented path");
        Assert.AreEqual(0, textValues.Count(text => IsInvented(text, @"^(Region|Gebiet|Session|Sitzung|Good|Ware|Worker|Arbeiter|Building|Gebäude|Unused|Ungenutzt) \d+$") == false), "Every text should be an invented text");
        Assert.IsEmpty(regionIds.Except(["Zorlan", "Yultic", "Xanthi", "Qwertz", "Vexan"]), "Every region type name should be an invented name");
        Assert.IsNotEmpty(defaultRegionIds, "The property and template files should state region types");
        Assert.IsEmpty(defaultRegionIds.Except(["Zorlan", "Yultic", "Xanthi", "Qwertz", "Vexan"]), "Every region type name of the property and template files should be an invented name");
        Assert.IsNotEmpty(templateTexts, "The property and template files should hold descriptions or property names");
        Assert.AreEqual(0, templateTexts.Count(text => IsInvented(text, @"^(Template|Property) \d+$") == false), "Every description and property name should be an invented text");
        Assert.IsEmpty(dataSetNames.Except(["Global", "FixtureKinds", "Region", "Zorlan", "Yultic", "Xanthi", "Vexan"]), "Every data set name should be an invented name");
    }

    #endregion // AC7

    #region Region type defaults

    /// <summary>
    /// A region asset without a <c>RegionID</c> takes the default of the <c>Region</c> property: the fixture session of
    /// region asset 102 carries the default type.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadResolvesRegionTypeFromPropertyDefault()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 203);

        // Assert
        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.AreEqual(Base + 102, entry.RegionGuid, "The entry should carry the GUID of its region asset");
        Assert.AreSequenceEqual(["Xanthi"], entry.RegionTypes, "A region asset without a RegionID should take the default of the Region property");
    }

    /// <summary>
    /// The <c>RegionID</c> a template states wins over the property default.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadPrefersTemplateRegionIdOverDefault()
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan", "Yultic"), assets, Properties(RegionDefault("Zorlan")), Templates(Template("Region", RegionDefault("Yultic"))));

        // Assert
        Assert.AreSequenceEqual(["Yultic"], catalog.Find(6)?.RegionTypes, "The RegionID of the template should win over the default");
    }

    /// <summary>
    /// The <c>RegionID</c> a region asset states wins over its template and the property default.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadPrefersAssetRegionIdOverTemplate()
    {
        // Arrange
        var assets = Assets(RegionAsset(5, 1000, "Qwertz"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan", "Yultic", "Qwertz"), assets, Properties(RegionDefault("Zorlan")), Templates(Template("Region", RegionDefault("Yultic"))));

        // Assert
        Assert.AreSequenceEqual(["Qwertz"], catalog.Find(6)?.RegionTypes, "The RegionID of the asset should win over the template and the default");
    }

    /// <summary>
    /// An empty <c>RegionID</c> in the template or in the default is a stated value: it is dropped and counted, and the
    /// session has an empty list of region types, not the default and not "not stated".
    /// </summary>
    /// <param name="templateRegion">The content of the template's <c>Properties</c> element</param>
    /// <param name="defaultRegion">The content of the <c>DefaultValues</c> element</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("<Region><RegionID /></Region>", "<Region><RegionID>Zorlan</RegionID></Region>")]
    [DataRow("<Region><Kontor>1</Kontor></Region>", "<Region><RegionID /></Region>")]
    public void GameCatalogReaderReadCountsEmptyRegionIdAsStated(string templateRegion, string defaultRegion)
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, Properties(defaultRegion), Templates(Template("Region", templateRegion)));

        // Assert
        var entry = catalog.Find(6);

        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.IsNotNull(entry.RegionTypes, "An empty RegionID is stated, so the list should not be null");
        Assert.IsEmpty(entry.RegionTypes, "The empty RegionID should win over the default and leave an empty list");
        Assert.AreEqual(1, catalog.DroppedRegionTypeCount, "The empty RegionID should be counted as dropped");
    }

    /// <summary>
    /// A region asset whose asset, template and default state no <c>RegionID</c> stays "not stated" (null).
    /// </summary>
    /// <param name="defaultValues">The content of the <c>DefaultValues</c> element</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("")]
    [DataRow("<Region />")]
    [DataRow("<Region><Kontor>1</Kontor></Region>")]
    public void GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutAnyRegionId(string defaultValues)
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, Properties(defaultValues), Templates(Template("Region", "<Region><Kontor>1</Kontor></Region>")));

        // Assert
        var entry = catalog.Find(6);

        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.IsNull(entry.RegionTypes, "Without a RegionID at any level the region type should stay not stated, not an empty list");
        Assert.AreEqual(0, catalog.DroppedRegionTypeCount, "Nothing should be dropped");
    }

    /// <summary>
    /// Empty <c>DefaultValues</c> and <c>DefaultContainerValues</c> elements state no default.
    /// </summary>
    /// <param name="properties">The property file</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("<Properties><Groups><Group><DefaultValues /><Name>Session</Name></Group></Groups></Properties>")]
    [DataRow("<Properties><Groups><Group><DefaultContainerValues /><Name>Session</Name></Group></Groups></Properties>")]
    [DataRow("<Properties><Groups><Group><DefaultValues /><DefaultContainerValues /></Group></Groups></Properties>")]
    public void GameCatalogReaderReadLeavesRegionTypeUnstatedForEmptyDefaultElements(string properties)
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, properties, Templates(Template("Region", "<Region><Kontor>1</Kontor></Region>")));

        // Assert
        var entry = catalog.Find(6);

        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.IsNull(entry.RegionTypes, "An empty default element should state no region type");
    }

    /// <summary>
    /// A text node and an empty part next to the <c>Region</c> default in <c>DefaultValues</c> change nothing.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadSkipsTextAndEmptyPartsInDefaultValues()
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));
        var properties = "<Properties><Groups><Group><DefaultValues>text<Other /><Region><RegionID>Zorlan</RegionID></Region></DefaultValues><Name>Session</Name></Group></Groups></Properties>";

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, properties, Templates(Template("Region", "<Region />")));

        // Assert
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(6)?.RegionTypes, "The Region default should still be read next to a text node and an empty part");
    }

    /// <summary>
    /// An empty <c>Template</c> element is ignored and does not hide the templates next to it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadIgnoresEmptyTemplateElement()
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan", "Yultic"), assets, Properties(RegionDefault("Zorlan")), Templates("<Template />", Template("Region", RegionDefault("Yultic"))));

        // Assert
        Assert.AreSequenceEqual(["Yultic"], catalog.Find(6)?.RegionTypes, "The template after an empty Template element should still be read");
    }

    /// <summary>
    /// A region asset whose template has no <c>Region</c> property, or is not listed, takes no default.
    /// </summary>
    /// <param name="listed">Whether the template is listed without a <c>Region</c> property</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(true)]
    [DataRow(false)]
    public void GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutRegionProperty(bool listed)
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));
        var templates = listed ? Templates(Template("Region", "<Standard />")) : Templates(Template("Other", RegionDefault("Zorlan")));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, Properties(RegionDefault("Zorlan")), templates);

        // Assert
        var entry = catalog.Find(6);

        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.IsNull(entry.RegionTypes, "A template without a Region property should take no default");
    }

    /// <summary>
    /// A default <c>RegionID</c> that the <c>Region</c> data set does not hold leaves an empty list and is counted once
    /// per region asset.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadDropsUnknownDefaultRegionId()
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), RegionAssetWithoutRegionId(8, 1000, "Region"), Session(6, "SessionModerate", 5, 1000), Session(7, "SessionModerate", 5, 1000), Session(9, "SessionModerate", 8, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, Properties(RegionDefault("Vexan")), Templates(Template("Region", "<Region />")));

        // Assert
        var entry = catalog.Find(6);

        Assert.AreEqual(3, catalog.Count, "All three sessions should be entries");
        Assert.IsNotNull(entry, "The session should be an entry");
        Assert.IsNotNull(entry.RegionTypes, "A dropped default should leave an empty list, not no list");
        Assert.IsEmpty(entry.RegionTypes, "The unknown default should be dropped from the entry");
        Assert.AreEqual(2, catalog.DroppedRegionTypeCount, "The dropped default should be counted once per region asset");
    }

    /// <summary>
    /// The default region type comes from the data, whatever its name.
    /// </summary>
    /// <param name="name">The invented name</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("Kalmar")]
    [DataRow("Dunlin")]
    public void GameCatalogReaderReadTakesDefaultRegionIdFromData(string name)
    {
        // Arrange
        var assets = Assets(RegionAssetWithoutRegionId(5, 1000, "Region"), Session(6, "SessionModerate", 5, 1000));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan", name), assets, Properties(RegionDefault(name)), Templates(Template("Region", "<Region />")));

        // Assert
        Assert.AreSequenceEqual([name], catalog.Find(6)?.RegionTypes, "The region type should be the name the data states");
    }

    /// <summary>
    /// A <c>DefaultContainerValues</c> region, building and session, a <c>Region</c> field of another property, a
    /// <c>Region</c>, <c>Building</c> and <c>Session</c> property nested in another property's default and a template
    /// nested in another template's property are not read: the fixture still resolves region asset 102 to its default, the
    /// other region assets keep their own types, and the buildings and the session take the defaults of their groups.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderReadIgnoresContainerDefaultsAndNestedTemplates()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();
        var unknown = catalog.Find(Base + 204);

        // Assert
        Assert.AreSequenceEqual(["Xanthi"], catalog.Find(Base + 203)?.RegionTypes, "Only the DefaultValues region of a group should give the default");
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(Base + 201)?.RegionTypes, "A region asset with its own RegionID should keep it");
        Assert.AreSequenceEqual(["Yultic"], catalog.Find(Base + 202)?.RegionTypes, "A second region asset with its own RegionID should keep it");
        Assert.IsNotNull(unknown?.RegionTypes, "The region asset with an unknown RegionID should keep its empty list");
        Assert.IsEmpty(unknown.RegionTypes, "The region asset with an unknown RegionID should stay dropped");
        Assert.AreSequenceEqual(["Xanthi", "Zorlan"], catalog.Find(Base + 505)?.RegionTypes, "Only the DefaultValues building list of a group should give the default");
        Assert.AreSequenceEqual(["Yultic", "Zorlan"], catalog.Find(Base + 504)?.RegionTypes, "A building list in a container default or a nested template should not be read");
        Assert.IsNull(catalog.Find(Base + 208), "Only the DefaultValues session region of a group should give the default");
        Assert.AreEqual(2, catalog.DroppedRegionTypeCount, "The decoys should drop nothing more");
    }

    /// <summary>
    /// A second <c>DefaultValues/Region</c> is a malformed file.
    /// </summary>
    /// <param name="content">The property file</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("<Properties><Groups><Group><DefaultValues><Region /><Region /></DefaultValues></Group></Groups></Properties>")]
    [DataRow("<Properties><Groups><Group><DefaultValues><Region /></DefaultValues></Group><Group><DefaultValues><Region /></DefaultValues></Group></Groups></Properties>")]
    public void GameCatalogReaderReadFailsForSecondRegionDefault(string content)
    {
        // Act
        var result = ReadWithFile(4, content);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A second Region default should be a malformed file");
        Assert.AreEqual(MalformedMessage, result.Error?.Message, "The message should be the fixed text of a malformed file");
    }

    /// <summary>
    /// A second defining template of the same name is a malformed file, whichever of the two has a <c>Region</c>
    /// property.
    /// </summary>
    /// <param name="firstHasRegion">Whether the first template has a <c>Region</c> property</param>
    /// <param name="secondHasRegion">Whether the second template has a <c>Region</c> property</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void GameCatalogReaderReadFailsForSecondRegionTemplate(bool firstHasRegion, bool secondHasRegion)
    {
        // Arrange
        var first = Template("Region", firstHasRegion ? "<Region />" : "<Standard />");
        var second = Template("Region", secondHasRegion ? "<Region />" : "<Standard />");

        // Act
        var result = ReadWithFile(5, Templates(first, second));

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A second defining template of a name should be a malformed file");
        Assert.AreEqual(MalformedMessage, result.Error?.Message, "The message should be the fixed text of a malformed file");
    }

    /// <summary>
    /// A property file with the root of the template file, and the reverse, is a malformed file.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="root">The wrong root element</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(4, "Templates")]
    [DataRow(5, "Properties")]
    public void GameCatalogReaderReadFailsForWrongRootOfDefaultFile(int file, string root)
    {
        // Act
        var result = ReadWithFile(file, $"<{root}><Groups /></{root}>");

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A wrong root element should be a malformed file");
        Assert.AreEqual(MalformedMessage, result.Error?.Message, "The message should be the fixed text of a malformed file");
    }

    /// <summary>
    /// A copy of the template file in another archive is reported.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderLoadReportsCopyOfTemplates()
    {
        // Arrange
        WriteConfigArchive(-1);
        File.WriteAllBytes(Path.Combine(MainData, "patch1.rda"), new RdaArchiveBuilder().AddBlock(0, (GameCatalogReader.TemplatesPath, RdaArchiveBuilder.Content("<Templates />"))).Build().Bytes);

        // Act
        var result = GameCatalogReader.Load(_root, RdaLimits.Default);

        // Assert
        Assert.IsNotNull(result.Catalog, $"The installation should give a catalog, but failed with {result.Error?.Kind}");
        Assert.HasCount(1, result.Copies, "The copy in the other archive should be reported");
        Assert.AreEqual(GameCatalogReader.TemplatesPath, result.Copies[0].Path, "The copy should name the path of the template file");
    }

    /// <summary>
    /// A copy of the property file in another archive is reported.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReaderLoadReportsCopyOfProperties()
    {
        // Arrange
        WriteConfigArchive(-1);
        File.WriteAllBytes(Path.Combine(MainData, "patch1.rda"), new RdaArchiveBuilder().AddBlock(0, (GameCatalogReader.PropertiesPath, RdaArchiveBuilder.Content("<Properties />"))).Build().Bytes);

        // Act
        var result = GameCatalogReader.Load(_root, RdaLimits.Default);

        // Assert
        Assert.IsNotNull(result.Catalog, $"The installation should give a catalog, but failed with {result.Error?.Kind}");
        Assert.HasCount(1, result.Copies, "The copy in the other archive should be reported");
        Assert.AreEqual(GameCatalogReader.PropertiesPath, result.Copies[0].Path, "The copy should name the path of the property file");
    }

    #endregion // Region type defaults

    #region Template and property defaults

    /// <summary>
    /// An asset without a <c>Building</c> section of its own whose template has a <c>Building</c> property in the template
    /// file is a building, also when that property is empty.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogReaderReadMakesBuildingOfTemplateWithBuildingProperty()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var listed = catalog.Find(Base + 504);
        var bare = catalog.Find(Base + 505);

        // Assert
        Assert.IsNotNull(listed, "An asset whose template has a Building property with a list should be an entry");
        Assert.AreEqual(GameCatalogKind.Building, listed.Kind, "An asset whose template has a Building property should be a building");
        Assert.IsNotNull(bare, "An asset whose template has an empty Building property should be an entry");
        Assert.AreEqual(GameCatalogKind.Building, bare.Kind, "An asset whose template has an empty Building property should be a building");
    }

    /// <summary>
    /// The order of the kind rules is unchanged: a template <c>Product</c> with a <c>Building</c> property still gives a good,
    /// and a template with a <c>Building</c> property and a <c>Session/Region</c> that names a region asset gives a region.
    /// </summary>
    /// <param name="template">The name of the template of the asset</param>
    /// <param name="properties">The content of the <c>Properties</c> element of the template</param>
    /// <param name="expected">The kind of the entry</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("Product", "<Building />", GameCatalogKind.Good)]
    [DataRow("KindMix", "<Building /><Session><Region>5</Region></Session>", GameCatalogKind.Region)]
    public void GameCatalogReaderReadKeepsKindOrderWithTemplateProperties(string template, string properties, GameCatalogKind expected)
    {
        // Arrange
        var assets = Assets(RegionAsset(5, 1000, "Zorlan"), TemplatedAsset(10, template, string.Empty));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, Properties(string.Empty), Templates(Template(template, properties)));

        // Assert
        Assert.AreEqual(expected, catalog.Find(10)?.Kind, "The kind rules should run in their order on the resolved facts");
    }

    /// <summary>
    /// An asset without a <c>Building</c> section of its own is no building when its template is listed without a
    /// <c>Building</c> property or not listed, also when the property default states a building list.
    /// </summary>
    /// <param name="templates">The template file</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("<Templates><Group><Name>Session</Name><Template><Name>Plain</Name><Properties><Standard /></Properties></Template></Group></Templates>")]
    [DataRow("<Templates><Group><Name>Session</Name></Group></Templates>")]
    public void GameCatalogReaderReadMakesNoBuildingWithoutBuildingProperty(string templates)
    {
        // Arrange
        var assets = Assets(TemplatedAsset(10, "Plain", string.Empty));
        var properties = Properties(BuildingYultic);

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan", "Yultic"), assets, properties, templates);

        // Assert
        Assert.IsNull(catalog.Find(10), "An asset with neither a Building section nor a Building template property should be no entry");
        Assert.AreEqual(0, catalog.Count, "No entry should be made");
    }

    /// <summary>
    /// A building with a list of its own keeps it over the list of its template.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadPrefersOwnBuildingRegionsOverTemplate()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 506);

        // Assert
        Assert.IsNotNull(entry, "The building should be an entry");
        Assert.AreSequenceEqual(["Xanthi"], entry.RegionTypes, "The building should keep its own list over the list of its template");
    }

    /// <summary>
    /// A property default is taken only for an asset whose template is listed with that property: a template listed
    /// without it, or not listed, gives only the asset's own value, which is not stated.
    /// </summary>
    /// <param name="fact">The kind of the asset</param>
    /// <param name="template">The <c>Properties</c> of the template, or <c>null</c> for a template that is not listed</param>
    /// <param name="defaults">The <c>DefaultValues</c></param>
    /// <param name="expected">The expected observation</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("Product", "<Standard />", ProductYultic, "Good unstated")]
    [DataRow("Product", null, ProductYultic, "Good unstated")]
    [DataRow("Workforce", "<Standard />", ProductYultic, "Workforce unstated")]
    [DataRow("Workforce", null, ProductYultic, "Workforce unstated")]
    [DataRow("Building", "<Standard />", BuildingYultic, "Building unstated")]
    [DataRow("Building", null, BuildingYultic, "Building unstated")]
    [DataRow("Session", "<Standard />", SessionRegion8, "none")]
    [DataRow("Session", null, SessionRegion8, "none")]
    public void GameCatalogReaderReadTakesNoDefaultWithoutProperty(string fact, string? template, string defaults, string expected)
    {
        // Act
        var observed = ObserveLayers(fact, string.Empty, template, defaults);

        // Assert
        Assert.AreEqual(expected, observed, "The default should not be taken for a template without the property");
    }

    /// <summary>
    /// A building without a list of its own takes the list of the <c>Building</c> property of its template.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadTakesBuildingRegionsFromTemplate()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 504);

        // Assert
        Assert.IsNotNull(entry, "The building should be an entry");
        Assert.AreSequenceEqual(["Yultic", "Zorlan"], entry.RegionTypes, "The building should carry the list of its template");
    }

    /// <summary>
    /// A building whose template has the <c>Building</c> property without a list takes the list of the property default.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadTakesBuildingRegionsFromPropertyDefault()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var entry = catalog.Find(Base + 505);

        // Assert
        Assert.IsNotNull(entry, "The building should be an entry");
        Assert.AreSequenceEqual(["Xanthi", "Zorlan"], entry.RegionTypes, "The building should carry the list of the property default");
    }

    /// <summary>
    /// A session without a <c>Session/Region</c> of its own takes the region of its template; a session whose template has
    /// the <c>Session</c> property without a region takes the default <c>0</c> and is no entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadTakesSessionRegionOfFixtureFromTemplateAndDefault()
    {
        // Arrange
        var catalog = ReadFixtureCatalog();

        // Act
        var fromTemplate = catalog.Find(Base + 207);
        var fromDefault = catalog.Find(Base + 208);

        // Assert
        Assert.IsNotNull(fromTemplate, "The session without a region of its own should be an entry");
        Assert.AreEqual(GameCatalogKind.Region, fromTemplate.Kind, "The session should be a region entry");
        Assert.AreEqual(Base + 101, fromTemplate.RegionGuid, "The session should carry the region asset its template names");
        Assert.AreSequenceEqual(["Zorlan"], fromTemplate.RegionTypes, "The session should carry the region type of that region asset");
        Assert.IsNull(fromDefault, "A session that takes the default 0 should not be an entry");
    }

    /// <summary>
    /// <c>Product/AssociatedRegion</c> of a good and of a workforce is the asset's own value, else the template's, else the
    /// default's.
    /// </summary>
    /// <param name="fact">The kind of the asset</param>
    /// <param name="own">The own part of the asset, or <c>null</c> for none</param>
    /// <param name="template">The <c>Properties</c> of the template, or <c>null</c> for a template that is not listed</param>
    /// <param name="defaults">The <c>DefaultValues</c></param>
    /// <param name="expected">The expected observation</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("Product", "<AssociatedRegion>Zorlan</AssociatedRegion>", ProductYultic, ProductXanthi, "Good [Zorlan]")]
    [DataRow("Product", null, ProductYultic, ProductXanthi, "Good [Yultic]")]
    [DataRow("Product", null, "<Product />", ProductXanthi, "Good [Xanthi]")]
    [DataRow("Workforce", "<AssociatedRegion>Zorlan</AssociatedRegion>", ProductYultic, ProductXanthi, "Workforce [Zorlan]")]
    [DataRow("Workforce", null, ProductYultic, ProductXanthi, "Workforce [Yultic]")]
    [DataRow("Workforce", null, "<Product />", ProductXanthi, "Workforce [Xanthi]")]
    public void GameCatalogReaderReadLayersProductRegions(string fact, string? own, string? template, string defaults, string expected)
    {
        // Act
        var observed = ObserveLayers(fact, own, template, defaults);

        // Assert
        Assert.AreEqual(expected, observed, "The list should come from the first level that states it");
    }

    /// <summary>
    /// <c>Session/Region</c> is the asset's own value, else the template's, else the default's; the resolved value decides
    /// the region entry and <c>0</c> never makes one, also against a region asset of GUID <c>0</c>.
    /// </summary>
    /// <param name="own">The own part of the asset, or <c>null</c> for none</param>
    /// <param name="template">The <c>Properties</c> of the template, or <c>null</c> for a template that is not listed</param>
    /// <param name="defaults">The <c>DefaultValues</c></param>
    /// <param name="expected">The expected observation</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("<Region>8</Region>", SessionRegion5, SessionRegion5, "Region 8")]
    [DataRow(null, SessionRegion5, SessionRegion8, "Region 5")]
    [DataRow(null, SessionBare, SessionRegion8, "Region 8")]
    [DataRow(null, SessionRegion0, SessionRegion8, "none")]
    [DataRow(null, SessionBare, SessionRegion0, "none")]
    [DataRow("<Region>0</Region>", SessionRegion5, SessionRegion5, "none")]
    public void GameCatalogReaderReadLayersSessionRegion(string? own, string? template, string defaults, string expected)
    {
        // Act
        var observed = ObserveLayers("Session", own, template, defaults);

        // Assert
        Assert.AreEqual(expected, observed, "The region should come from the first level that states it, and 0 should make no entry");
    }

    /// <summary>
    /// A present empty element is stated at every level: an empty list means none, an empty <c>Session/Region</c> no region,
    /// and a lower level is not asked.
    /// </summary>
    /// <param name="fact">The kind of the asset</param>
    /// <param name="own">The own part of the asset, or <c>null</c> for none</param>
    /// <param name="template">The <c>Properties</c> of the template</param>
    /// <param name="defaults">The <c>DefaultValues</c></param>
    /// <param name="expected">The expected observation</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("Product", null, ProductEmpty, ProductXanthi, "Good []")]
    [DataRow("Product", null, "<Product />", ProductEmpty, "Good []")]
    [DataRow("Product", "<AssociatedRegion />", ProductYultic, ProductXanthi, "Good []")]
    [DataRow("Workforce", null, ProductEmpty, ProductXanthi, "Workforce []")]
    [DataRow("Workforce", null, "<Product />", ProductEmpty, "Workforce []")]
    [DataRow("Building", null, BuildingEmpty, BuildingXanthi, "Building []")]
    [DataRow("Building", null, "<Building />", BuildingEmpty, "Building []")]
    [DataRow("Building", "<AssociatedRegions />", BuildingYultic, BuildingXanthi, "Building []")]
    [DataRow("Session", null, SessionRegionEmpty, SessionRegion8, "none")]
    [DataRow("Session", null, SessionBare, SessionRegionEmpty, "none")]
    public void GameCatalogReaderReadCountsEmptyLayerValueAsStated(string fact, string? own, string template, string defaults, string expected)
    {
        // Act
        var observed = ObserveLayers(fact, own, template, defaults);

        // Assert
        Assert.AreEqual(expected, observed, "The empty value should win over the levels below it");
    }

    /// <summary>
    /// An unknown name of a template list or of a default list is dropped and counted once for the list, however many
    /// buildings use it.
    /// </summary>
    /// <param name="template">The <c>Properties</c> of the template</param>
    /// <param name="defaults">The <c>DefaultValues</c></param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("<Building><AssociatedRegions>Zorlan;Vexan</AssociatedRegions></Building>", "")]
    [DataRow("<Building />", "<Building><AssociatedRegions>Zorlan;Vexan</AssociatedRegions></Building>")]
    public void GameCatalogReaderReadCountsDroppedDefaultRegionTypes(string template, string defaults)
    {
        // Arrange
        var assets = Assets(TemplatedAsset(11, "Bld", string.Empty), TemplatedAsset(12, "Bld", string.Empty));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan"), assets, Properties(defaults), Templates(Template("Bld", template)));

        // Assert
        Assert.AreEqual(2, catalog.Count, "Both buildings should be entries");
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(11)?.RegionTypes, "The first building should keep the known name");
        Assert.AreSequenceEqual(["Zorlan"], catalog.Find(12)?.RegionTypes, "The second building should keep the known name");
        Assert.AreEqual(1, catalog.DroppedRegionTypeCount, "The unknown name should be counted once for its source list");
    }

    /// <summary>
    /// The buildings that take one template list share one list, and those that take the default list share another; the
    /// shared list cannot be changed through a cast.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogReaderReadSharesLayeredRegionList()
    {
        // Arrange
        var assets = Assets(TemplatedAsset(11, "Bld", string.Empty), TemplatedAsset(12, "Bld", string.Empty), TemplatedAsset(13, "Bare", string.Empty), TemplatedAsset(14, "Bare", string.Empty));
        var templates = Templates(Template("Bld", "<Building><AssociatedRegions>Zorlan;Yultic</AssociatedRegions></Building>"), Template("Bare", "<Building />"));

        // Act
        var catalog = ReadCatalogWithDefaults(DataSets("Zorlan", "Yultic", "Xanthi"), assets, Properties("<Building><AssociatedRegions>Xanthi</AssociatedRegions></Building>"), templates);

        // Assert
        var entries = catalog.Entries.ToDictionary(entry => entry.AssetGuid);

        Assert.HasCount(4, entries, "All four buildings should be entries");
        Assert.AreSequenceEqual(["Zorlan", "Yultic"], entries[11].RegionTypes, "The buildings of the template should carry its list");
        Assert.AreSequenceEqual(["Xanthi"], entries[13].RegionTypes, "The buildings of the default should carry its list");
        Assert.AreSame(entries[11].RegionTypes, entries[12].RegionTypes, "The buildings that take one template list should share it");
        Assert.AreSame(entries[13].RegionTypes, entries[14].RegionTypes, "The buildings that take the default list should share it");
        Assert.IsNotInstanceOfType<List<string>>(entries[11].RegionTypes, "A shared list should not be a list that a cast can change");
        Assert.IsNotInstanceOfType<List<string>>(entries[13].RegionTypes, "A shared default list should not be a list that a cast can change");
    }

    /// <summary>
    /// A second <c>DefaultValues</c> part <c>Building</c>, <c>Product</c> or <c>Session</c> is a malformed file, in one
    /// group and across two groups.
    /// </summary>
    /// <param name="property">The name of the part</param>
    /// <param name="sameGroup">Whether both parts are in one group</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("Building", true)]
    [DataRow("Building", false)]
    [DataRow("Product", true)]
    [DataRow("Product", false)]
    [DataRow("Session", true)]
    [DataRow("Session", false)]
    public void GameCatalogReaderReadFailsForSecondCatalogPropertyDefault(string property, bool sameGroup)
    {
        // Arrange
        var content = sameGroup
                          ? $"<Properties><Groups><Group><DefaultValues><{property} /><{property} /></DefaultValues></Group></Groups></Properties>"
                          : $"<Properties><Groups><Group><DefaultValues><{property} /></DefaultValues></Group><Group><DefaultValues><{property} /></DefaultValues></Group></Groups></Properties>";

        // Act
        var result = ReadWithFile(4, content);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A second default of the property should be a malformed file");
        Assert.AreEqual(MalformedMessage, result.Error?.Message, "The message should be the fixed text of a malformed file");
    }

    /// <summary>
    /// A <c>Session/Region</c> of a template or a default that is neither empty nor a signed 32-bit integer is a malformed file.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="value">The value</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(5, "abc")]
    [DataRow(4, "abc")]
    [DataRow(5, "99999999999")]
    [DataRow(4, "99999999999")]
    public void GameCatalogReaderReadFailsForNonIntegerSessionRegionLayer(int file, string value)
    {
        // Arrange
        var session = $"<Session><Region>{value}</Region></Session>";
        var content = file == 4 ? Properties(session) : Templates(Template("SessionModerate", session));

        // Act
        var result = ReadWithFile(file, content);

        // Assert
        Assert.IsNull(result.Catalog, "No catalog should be used");
        Assert.AreEqual(GameCatalogErrorKind.MalformedFile, result.Error?.Kind, "A Session/Region that is no integer should be a malformed file");
        Assert.AreEqual(MalformedMessage, result.Error?.Message, "The message should be the fixed text of a malformed file");
    }

    #endregion // Template and property defaults

    #region Helpers

    /// <summary>
    /// Gets the folder of the fixture.
    /// </summary>
    /// <returns>The folder</returns>
    private static string FixtureFolder()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "GameCatalog");
    }

    /// <summary>
    /// Reads a file of the fixture.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <returns>The bytes</returns>
    private static byte[] FixtureBytes(string name)
    {
        return File.ReadAllBytes(Path.Combine(FixtureFolder(), name));
    }

    /// <summary>
    /// Reads a file of the sample as a source.
    /// </summary>
    /// <param name="folder">The folder of the sample</param>
    /// <param name="name">The file name</param>
    /// <returns>The source</returns>
    private static GameCatalogSource OpenSample(string folder, string name)
    {
        return GameCatalogSource.FromStream(File.OpenRead(Path.Combine(folder, name)));
    }

    /// <summary>
    /// Creates a source over the UTF-8 bytes of a text.
    /// </summary>
    /// <param name="content">The text</param>
    /// <returns>The opener</returns>
    private static Func<GameCatalogSource> Open(string content)
    {
        return () => GameCatalogSource.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    /// <summary>
    /// Reads the catalog from texts, requiring that it is built.
    /// </summary>
    /// <param name="dataSets">The data set file</param>
    /// <param name="assets">The asset file</param>
    /// <param name="english">The English text file</param>
    /// <param name="german">The German text file</param>
    /// <returns>The catalog</returns>
    private static GameCatalog ReadCatalog(string dataSets, string assets, string english, string german)
    {
        var result = GameCatalogReader.Read(Open(dataSets), Open(assets), Open(english), Open(german), FixtureOpener(4), FixtureOpener(5));

        Assert.IsNull(result.Error, $"The files should give a catalog, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "The files should give a catalog");

        return result.Catalog;
    }

    /// <summary>
    /// Reads the catalog from texts with given property and template files, requiring that it is built.
    /// </summary>
    /// <param name="dataSets">The data set file</param>
    /// <param name="assets">The asset file</param>
    /// <param name="properties">The property file</param>
    /// <param name="templates">The template file</param>
    /// <returns>The catalog</returns>
    private static GameCatalog ReadCatalogWithDefaults(string dataSets, string assets, string properties, string templates)
    {
        var result = GameCatalogReader.Read(Open(dataSets), Open(assets), Open(Texts((1000, "Region 5"))), Open(Texts((1000, "Gebiet 5"))), Open(properties), Open(templates));

        Assert.IsNull(result.Error, $"The files should give a catalog, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "The files should give a catalog");

        return result.Catalog;
    }

    /// <summary>
    /// Reads the catalog of the fixture, requiring that it is built.
    /// </summary>
    /// <returns>The catalog</returns>
    private static GameCatalog ReadFixtureCatalog()
    {
        var result = GameCatalogReader.Read(() => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[0]))),
                                            () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[1]))),
                                            () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[2]))),
                                            () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[3]))),
                                            () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[4]))),
                                            () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[5]))));

        Assert.IsNull(result.Error, $"The fixture should give a catalog, but failed with {result.Error?.Kind}");
        Assert.IsNotNull(result.Catalog, "The fixture should give a catalog");

        return result.Catalog;
    }

    /// <summary>
    /// Opens a fixture file.
    /// </summary>
    /// <param name="index">The index of the file</param>
    /// <returns>The opener</returns>
    private static Func<GameCatalogSource> FixtureOpener(int index)
    {
        return () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[index])));
    }

    /// <summary>
    /// Reads the fixture with one file replaced.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="content">The content of the file</param>
    /// <returns>The result</returns>
    private static GameCatalogResult ReadWithFile(int file, string content)
    {
        Func<GameCatalogSource> Opener(int index)
        {
            return index == file ? Open(content) : () => GameCatalogSource.FromStream(new MemoryStream(FixtureBytes(_fileNames[index])));
        }

        return GameCatalogReader.Read(Opener(0), Opener(1), Opener(2), Opener(3), Opener(4), Opener(5));
    }

    /// <summary>
    /// Creates a data set file with a <c>Region</c> data set.
    /// </summary>
    /// <param name="regionTypes">The region types</param>
    /// <returns>The file</returns>
    private static string DataSets(params string[] regionTypes)
    {
        return $"<DataSets><Group><Name>Global</Name>{DataSet("Region", regionTypes)}</Group></DataSets>";
    }

    /// <summary>
    /// Creates a data set.
    /// </summary>
    /// <param name="name">The name of the data set</param>
    /// <param name="items">The names of its items</param>
    /// <returns>The data set</returns>
    private static string DataSet(string name, params string[] items)
    {
        var list = string.Concat(items.Select(item => $"<Item><Name>{item}</Name><Id>1</Id></Item>"));

        return $"<DataSet><Name>{name}</Name><Id>1</Id><Items>{list}</Items></DataSet>";
    }

    /// <summary>
    /// Creates an asset file.
    /// </summary>
    /// <param name="assets">The assets</param>
    /// <returns>The file</returns>
    private static string Assets(params string[] assets)
    {
        return $"<AssetList><Groups><Group><Assets>{string.Concat(assets)}</Assets></Group></Groups></AssetList>";
    }

    /// <summary>
    /// Creates a good.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="textId">The text identifier</param>
    /// <param name="regions">The association list, or null for none</param>
    /// <returns>The asset</returns>
    private static string Good(int guid, long textId, string? regions)
    {
        var list = regions is null ? string.Empty : $"<AssociatedRegion>{regions}</AssociatedRegion>";

        return string.Create(CultureInfo.InvariantCulture, $"<Asset><Template>Product</Template><Values><Standard><GUID>{guid}</GUID><Name>Good {guid}</Name></Standard><Text><OasisId>{textId}</OasisId></Text><Product>{list}</Product></Values></Asset>");
    }

    /// <summary>
    /// Creates a region asset.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="textId">The text identifier</param>
    /// <param name="regionId">The region type</param>
    /// <returns>The asset</returns>
    private static string RegionAsset(int guid, long textId, string regionId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"<Asset><Template>Region</Template><Values><Standard><GUID>{guid}</GUID></Standard><Text><OasisId>{textId}</OasisId></Text><Region><RegionID>{regionId}</RegionID></Region></Values></Asset>");
    }

    /// <summary>
    /// Creates a region asset that states no <c>RegionID</c>.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="textId">The text identifier</param>
    /// <param name="template">The template</param>
    /// <returns>The asset</returns>
    private static string RegionAssetWithoutRegionId(int guid, long textId, string template)
    {
        return string.Create(CultureInfo.InvariantCulture, $"<Asset><Template>{template}</Template><Values><Standard><GUID>{guid}</GUID></Standard><Text><OasisId>{textId}</OasisId></Text><Region><Kontor>1</Kontor></Region></Values></Asset>");
    }

    /// <summary>
    /// Creates a property defaults file with one group.
    /// </summary>
    /// <param name="defaultValues">The content of the <c>DefaultValues</c> element</param>
    /// <returns>The file</returns>
    private static string Properties(string defaultValues)
    {
        return $"<Properties><Groups><Group><DefaultValues>{defaultValues}</DefaultValues><Name>Session</Name></Group></Groups></Properties>";
    }

    /// <summary>
    /// Creates the <c>Region</c> property default.
    /// </summary>
    /// <param name="regionId">The region type</param>
    /// <returns>The property</returns>
    private static string RegionDefault(string regionId)
    {
        return $"<Region><RegionID>{regionId}</RegionID></Region>";
    }

    /// <summary>
    /// Creates a templates file with one group.
    /// </summary>
    /// <param name="templates">The templates</param>
    /// <returns>The file</returns>
    private static string Templates(params string[] templates)
    {
        return $"<Templates><Group><Name>Session</Name>{string.Concat(templates)}</Group></Templates>";
    }

    /// <summary>
    /// Creates a defining template.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="properties">The content of its <c>Properties</c> element</param>
    /// <returns>The template</returns>
    private static string Template(string name, string properties)
    {
        return $"<Template><Name>{name}</Name><Properties>{properties}</Properties></Template>";
    }

    /// <summary>
    /// Creates a session.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="template">The template</param>
    /// <param name="region">The GUID of the region asset</param>
    /// <param name="textId">The text identifier</param>
    /// <returns>The asset</returns>
    private static string Session(int guid, string template, int region, long textId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"<Asset><Template>{template}</Template><Values><Standard><GUID>{guid}</GUID></Standard><Text><OasisId>{textId}</OasisId></Text><Session><Region>{region}</Region></Session></Values></Asset>");
    }

    /// <summary>
    /// Creates an asset of a template with given sections and the text identifier <c>1000</c>.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="template">The template</param>
    /// <param name="sections">The sections of its values</param>
    /// <returns>The asset</returns>
    private static string TemplatedAsset(int guid, string template, string sections)
    {
        return string.Create(CultureInfo.InvariantCulture, $"<Asset><Template>{template}</Template><Values><Standard><GUID>{guid}</GUID></Standard><Text><OasisId>1000</OasisId></Text>{sections}</Values></Asset>");
    }

    /// <summary>
    /// Reads a catalog with one asset (GUID 10) of a kind and region assets 0, 5 and 8, and describes the asset.
    /// </summary>
    /// <param name="fact">The kind of the asset: <c>Product</c>, <c>Workforce</c>, <c>Building</c> or <c>Session</c></param>
    /// <param name="own">The content of the asset's own section, or <c>null</c> for no section (a good always has one)</param>
    /// <param name="templateProperties">The content of the <c>Properties</c> of the asset's template, or <c>null</c> for a template that is not listed</param>
    /// <param name="defaults">The content of the <c>DefaultValues</c></param>
    /// <returns>The kind with the region types, the kind with the region asset of a region entry, or <c>none</c> for no entry</returns>
    private static string ObserveLayers(string fact, string? own, string? templateProperties, string defaults)
    {
        var (template, sections) = fact switch
                                   {
                                       "Product" => ("Product", $"<Product>{own}</Product>"),
                                       "Workforce" => ("Product", $"<Product><IsWorkforce>1</IsWorkforce>{own}</Product>"),
                                       "Building" => ("Bld", own is null ? string.Empty : $"<Building>{own}</Building>"),
                                       _ => ("SessionModerate", own is null ? string.Empty : $"<Session>{own}</Session>")
                                   };

        var assets = Assets(RegionAsset(0, 1000, "Zorlan"), RegionAsset(5, 1000, "Zorlan"), RegionAsset(8, 1000, "Yultic"), TemplatedAsset(10, template, sections));
        var templates = templateProperties is null ? Templates() : Templates(Template(template, templateProperties));
        var entry = ReadCatalogWithDefaults(DataSets("Zorlan", "Yultic", "Xanthi"), assets, Properties(defaults), templates).Find(10);

        if (entry is null)
        {
            return "none";
        }

        if (entry.Kind == GameCatalogKind.Region)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Region {entry.RegionGuid}");
        }

        var types = entry.RegionTypes is null ? "unstated" : $"[{string.Join(';', entry.RegionTypes)}]";

        return $"{entry.Kind} {types}";
    }

    /// <summary>
    /// Creates a text file.
    /// </summary>
    /// <param name="texts">The text identifiers and texts</param>
    /// <returns>The file</returns>
    private static string Texts(params (long Id, string Text)[] texts)
    {
        var list = string.Concat(texts.Select(text => string.Create(CultureInfo.InvariantCulture, $"<Text><LineId>{text.Id}</LineId><Text>{text.Text}</Text></Text>")));

        return $"<TextExport><Texts>{list}</Texts></TextExport>";
    }

    /// <summary>
    /// Creates an XML reader over a stream as the tests read the fixture.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <returns>The reader</returns>
    private static XmlReader CreateXmlReader(Stream stream)
    {
        return XmlReader.Create(stream,
                                new XmlReaderSettings
                                {
                                    DtdProcessing = DtdProcessing.Prohibit,
                                    XmlResolver = null,
                                    IgnoreWhitespace = true,
                                    IgnoreComments = true
                                });
    }

    /// <summary>
    /// Collects the text values of the elements with given names.
    /// </summary>
    /// <param name="bytes">The XML</param>
    /// <param name="names">The element names</param>
    /// <returns>The values</returns>
    private static HashSet<string> CollectValues(byte[] bytes, string[] names)
    {
        using var stream = new MemoryStream(bytes);

        return CollectValues(stream, names);
    }

    /// <summary>
    /// Collects the text values of the elements with given names.
    /// </summary>
    /// <param name="stream">The XML</param>
    /// <param name="names">The element names</param>
    /// <returns>The values</returns>
    private static HashSet<string> CollectValues(Stream stream, string[] names)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        string? pending = null;

        using var reader = CreateXmlReader(stream);

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    {
                        pending = reader.IsEmptyElement ? null : reader.LocalName;
                    }
                    break;

                case XmlNodeType.Text:
                    {
                        if (pending is not null && names.Contains(pending))
                        {
                            values.Add(reader.Value);
                        }

                        pending = null;
                    }
                    break;

                default:
                    {
                        pending = null;
                    }
                    break;
            }
        }

        return values;
    }

    /// <summary>
    /// Decodes the pipe fixture and collects the GUIDs of its ticks by role, without GUID <c>0</c>.
    /// </summary>
    /// <returns>The region, good, workforce and building GUIDs</returns>
    private static async Task<(HashSet<int> Regions, HashSet<int> Goods, HashSet<int> Workforce, HashSet<int> Buildings)> ReadPipeGuidsAsync()
    {
        var regions = new HashSet<int>();
        var goods = new HashSet<int>();
        var workforce = new HashSet<int>();
        var buildings = new HashSet<int>();
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        using var replay = new PipeReplayStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", "long-session.jsonl")), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock);

        PipeTickResult result;

        do
        {
            result = await assembler.ReadAsync(CancellationToken.None);

            foreach (var island in result.Tick?.Islands ?? [])
            {
                regions.Add(island.Key.SessionGuid);

                foreach (var entry in island.Entries)
                {
                    goods.Add(entry.ProductGuid);
                    workforce.UnionWith(entry.WorkforceAmounts.Keys);
                    buildings.UnionWith(entry.BuildingAmounts.Keys);
                }
            }
        }
        while (result.Kind != PipeTickResultKind.EndOfStream);

        regions.Remove(0);
        goods.Remove(0);
        workforce.Remove(0);
        buildings.Remove(0);

        return (regions, goods, workforce, buildings);
    }

    /// <summary>
    /// Checks that a value has the shape of an invented value.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="pattern">The pattern</param>
    /// <returns><c>true</c> if the value matches</returns>
    private static bool IsInvented(string value, string pattern)
    {
        return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Gets the fixed message of an error kind.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The message</returns>
    private static string MessageOf(GameCatalogErrorKind kind)
    {
        return kind switch
               {
                   GameCatalogErrorKind.ArchiveUnreadable => ArchiveMessage,
                   GameCatalogErrorKind.FileMissing => MissingMessage,
                   GameCatalogErrorKind.FileUnreadable => UnreadableMessage,
                   _ => MalformedMessage
               };
    }

    /// <summary>
    /// Reads the fixture with openers that log, each file replaced by a source if the function gives one.
    /// </summary>
    /// <param name="log">The log</param>
    /// <param name="replace">Gives the source of a file, or <c>null</c> for the fixture file</param>
    /// <returns>The result</returns>
    private static GameCatalogResult ReadLogged(List<string> log, Func<int, GameCatalogSource?> replace)
    {
        Func<GameCatalogSource> Opener(int index)
        {
            return () =>
                   {
                       log.Add($"open {_fileNames[index]}");

                       return replace(index) ?? GameCatalogSource.FromStream(new TrackingStream(FixtureBytes(_fileNames[index]), $"close {_fileNames[index]}", log));
                   };
        }

        return GameCatalogReader.Read(Opener(0), Opener(1), Opener(2), Opener(3), Opener(4), Opener(5));
    }

    /// <summary>
    /// Gets the log of a read that ends in a file: the files of the earlier passes opened and closed, then the opening of the file.
    /// </summary>
    /// <param name="file">The index of the file where the read ends</param>
    /// <param name="closed">Whether the stream of that file is closed too</param>
    /// <returns>The log</returns>
    private static List<string> ExpectedLog(int file, bool closed)
    {
        var log = new List<string>();

        foreach (var index in _passOrder.TakeWhile(index => index != file))
        {
            log.Add($"open {_fileNames[index]}");
            log.Add($"close {_fileNames[index]}");
        }

        log.Add($"open {_fileNames[file]}");

        if (closed)
        {
            log.Add($"close {_fileNames[file]}");
        }

        return log;
    }

    /// <summary>
    /// Creates a file with a defining part that holds a filler.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <param name="filler">The filler, a child of the defining element</param>
    /// <returns>The file</returns>
    private static string BoundedContent(int file, string filler)
    {
        return file switch
               {
                   0 => $"<DataSets><Group><Name>Global</Name><DataSet><Name>Region</Name><Items><Item><Name>Zorlan</Name></Item></Items>{filler}</DataSet></Group></DataSets>",
                   1 => $"<AssetList><Groups><Group><Assets><Asset><Template>Product</Template><Values><Standard><GUID>10</GUID></Standard></Values>{filler}</Asset></Assets></Group></Groups></AssetList>",
                   2 or 3 => $"<TextExport><Texts><Text><LineId>1000</LineId><Text>Good 10</Text>{filler}</Text></Texts></TextExport>",
                   4 => $"<Properties><Groups><Group><DefaultValues><Region>{filler}</Region></DefaultValues><Name>Session</Name></Group></Groups></Properties>",
                   _ => $"<Templates><Group><Template><Name>Plain</Name><Properties />{filler}</Template></Group></Templates>"
               };
    }

    /// <summary>
    /// Gets the number of elements of <see cref="BoundedContent" /> without its filler, the defining element included.
    /// </summary>
    /// <param name="file">The index of the file</param>
    /// <returns>The number of elements</returns>
    private static int BaseElements(int file)
    {
        return file switch
               {
                   < 2 => 5,
                   4 => 1,
                   _ => 3
               };
    }

    /// <summary>
    /// Creates nested elements.
    /// </summary>
    /// <param name="depth">The depth</param>
    /// <returns>The elements</returns>
    private static string Nested(int depth)
    {
        return string.Concat(Enumerable.Repeat("<N>", depth)) + string.Concat(Enumerable.Repeat("</N>", depth));
    }

    /// <summary>
    /// Reads the catalog of an installation from <c>config.rda</c> with empty property and template files, so that every
    /// value is the asset's own.
    /// </summary>
    /// <param name="installation">The installation folder</param>
    /// <returns>The result</returns>
    private static GameCatalogResult ReadInstallationWithoutDefaults(string installation)
    {
        var opened = RdaArchive.Open(Path.Combine(installation, "maindata", "config.rda"), RdaLimits.Default);

        Assert.IsNotNull(opened.Archive, "The configuration archive should open");

        using var archive = opened.Archive;

        Func<GameCatalogSource> Entry(string path)
        {
            return () =>
                   {
                       var read = archive.Read(path);

                       return read.Error is null ? GameCatalogSource.FromStream(new MemoryStream(read.Content.ToArray(), false)) : GameCatalogSource.FromError(GameCatalogErrorKind.FileUnreadable);
                   };
        }

        return GameCatalogReader.Read(Entry(GameCatalogReader.DataSetsPath), Entry(GameCatalogReader.AssetsPath), Entry(GameCatalogReader.EnglishTextsPath), Entry(GameCatalogReader.GermanTextsPath), Open("<Properties />"), Open("<Templates />"));
    }

    /// <summary>
    /// Describes the region list of an entry for the report.
    /// </summary>
    /// <param name="entry">The entry, or <c>null</c> for none</param>
    /// <returns>The state: no entry, not stated, none, or a list</returns>
    private static string ListState(GameCatalogEntry? entry)
    {
        return entry?.RegionTypes switch
               {
                   _ when entry is null => "no entry",
                   null => "not stated",
                   { Count: 0 } => "none",
                   _ => "list"
               };
    }

    /// <summary>
    /// Describes how the region list of an entry differs between the own-values build and the layered build.
    /// </summary>
    /// <param name="own">The entry of the own-values build</param>
    /// <param name="layered">The entry of the layered build</param>
    /// <returns>The transition, or <c>null</c> when the list is the same</returns>
    private static string? ListTransition(GameCatalogEntry? own, GameCatalogEntry? layered)
    {
        var from = ListState(own);
        var to = ListState(layered);

        if (from != to)
        {
            return $"{from} -> {to}";
        }

        return from == "list" && own!.RegionTypes!.SequenceEqual(layered!.RegionTypes!) == false ? "list changed" : null;
    }

    /// <summary>
    /// Describes the number of region types of an entry for the report.
    /// </summary>
    /// <param name="entry">The entry, or <c>null</c> for none</param>
    /// <returns>The kind and the number of region types</returns>
    private static string Describe(GameCatalogEntry? entry)
    {
        if (entry is null)
        {
            return "no entry";
        }

        var types = entry.RegionTypes is null ? "region types not stated" : string.Create(CultureInfo.InvariantCulture, $"{entry.RegionTypes.Count} region types");

        return $"{entry.Kind}, {types}";
    }

    /// <summary>
    /// Checks that every GUID resolves to an entry of its kind with a non-empty English and German name and that every
    /// region carries exactly one region type, and writes counts, unresolved GUIDs and per region its GUID, its region
    /// asset GUID and its number of region types, never a name.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="guids">The GUIDs by role</param>
    private void AssertPipeGuidsResolve(GameCatalog catalog, (HashSet<int> Regions, HashSet<int> Goods, HashSet<int> Workforce, HashSet<int> Buildings) guids)
    {
        var problems = new List<string>();

        foreach (var (role, kind, set) in new[] { ("Region", GameCatalogKind.Region, guids.Regions), ("Good", GameCatalogKind.Good, guids.Goods), ("Workforce", GameCatalogKind.Workforce, guids.Workforce), ("Building", GameCatalogKind.Building, guids.Buildings) })
        {
            var unresolved = 0;

            foreach (var guid in set.Order())
            {
                var entry = catalog.Find(guid);
                string? problem = null;

                if (entry is null)
                {
                    problem = $"{role} {guid}: unresolved ({catalog.Check(guid)})";
                }
                else if (entry.Kind != kind)
                {
                    problem = $"{role} {guid}: kind {entry.Kind}";
                }
                else if (string.IsNullOrEmpty(entry.EnglishName) || string.IsNullOrEmpty(entry.GermanName))
                {
                    problem = $"{role} {guid}: name missing ({catalog.Check(guid)})";
                }
                else if (kind == GameCatalogKind.Region)
                {
                    var count = entry.RegionTypes?.Count ?? 0;

                    TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Region {guid}: region asset {entry.RegionGuid}, {count} region types"));

                    if (count != 1)
                    {
                        problem = $"{role} {guid}: region asset {entry.RegionGuid} has {count} region types";
                    }
                }

                if (problem is not null)
                {
                    unresolved++;
                    problems.Add(problem);
                }
            }

            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{role}: {set.Count} GUIDs, {unresolved} unresolved"));
        }

        foreach (var problem in problems)
        {
            TestContext.WriteLine(problem);
        }

        Assert.IsEmpty(problems, $"Every GUID should resolve to an entry of its kind with both names: {string.Join("; ", problems)}");
    }

    /// <summary>
    /// Writes the difference between the catalog built with the assets' own values and the layered catalog as counts and
    /// GUIDs, never a name, and fails when an entry of the own-values build is missing from the layered one.
    /// </summary>
    /// <param name="layered">The catalog with the template and property defaults</param>
    /// <param name="ownValues">The catalog with the assets' own values</param>
    /// <param name="guids">The GUIDs of the pipe fixture by role</param>
    private void WriteLayerReport(GameCatalog layered, GameCatalog ownValues, (HashSet<int> Regions, HashSet<int> Goods, HashSet<int> Workforce, HashSet<int> Buildings) guids)
    {
        var layeredByGuid = layered.Entries.ToDictionary(entry => entry.AssetGuid);
        var ownByGuid = ownValues.Entries.ToDictionary(entry => entry.AssetGuid);
        var missing = ownByGuid.Keys.Where(guid => layeredByGuid.ContainsKey(guid) == false).Order().ToList();

        foreach (var kind in Enum.GetValues<GameCatalogKind>())
        {
            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Layers {kind}: {ownValues.Entries.Count(entry => entry.Kind == kind)} entries with own values, {layered.Entries.Count(entry => entry.Kind == kind)} with the defaults"));
        }

        var kindChanges = layeredByGuid.Select(pair => (From: ownByGuid.TryGetValue(pair.Key, out var before) ? before.Kind.ToString() : "none", To: pair.Value.Kind.ToString()))
                                       .Concat(missing.Select(guid => (From: ownByGuid[guid].Kind.ToString(), To: "none")))
                                       .Where(change => change.From != change.To)
                                       .GroupBy(change => change)
                                       .OrderBy(group => group.Key.From, StringComparer.Ordinal)
                                       .ThenBy(group => group.Key.To, StringComparer.Ordinal);

        foreach (var group in kindChanges)
        {
            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Layers kind change {group.Key.From} -> {group.Key.To}: {group.Count()} entries"));
        }

        var listChanges = layeredByGuid.Where(pair => ownByGuid.TryGetValue(pair.Key, out var before) && before.Kind == pair.Value.Kind)
                                       .Select(pair => (pair.Value.Kind, Transition: ListTransition(ownByGuid[pair.Key], pair.Value)))
                                       .Where(change => change.Transition is not null)
                                       .GroupBy(change => change)
                                       .OrderBy(group => group.Key.Kind)
                                       .ThenBy(group => group.Key.Transition, StringComparer.Ordinal);

        foreach (var group in listChanges)
        {
            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Layers region list of {group.Key.Kind}, {group.Key.Transition}: {group.Count()} entries"));
        }

        foreach (var (role, set) in new[] { ("Region", guids.Regions), ("Good", guids.Goods), ("Workforce", guids.Workforce), ("Building", guids.Buildings) })
        {
            foreach (var guid in set.Order())
            {
                var before = ownByGuid.GetValueOrDefault(guid);
                var after = layeredByGuid.GetValueOrDefault(guid);

                if (before?.Kind != after?.Kind || ListTransition(before, after) is not null)
                {
                    TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Layers changed {role} {guid}: {Describe(before)} -> {Describe(after)}"));
                }
            }
        }

        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Layers: {missing.Count} entries of the own-values build are missing from the layered build"));

        foreach (var guid in missing)
        {
            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Layers missing {guid}: {Describe(ownByGuid[guid])}"));
        }

        Assert.IsEmpty(missing, "Every entry of the own-values build should be an entry of the layered build");
    }

    /// <summary>
    /// Writes <c>maindata/config.rda</c> with the fixture files.
    /// </summary>
    /// <param name="omit">The index of a file to leave out, or <c>-1</c> for none</param>
    private void WriteConfigArchive(int omit)
    {
        Directory.CreateDirectory(MainData);

        var files = _filePaths.Select((path, index) => (Path: path, Index: index))
                              .Where(file => file.Index != omit)
                              .Select(file => (file.Path, FixtureBytes(_fileNames[file.Index])))
                              .ToArray();

        File.WriteAllBytes(Path.Combine(MainData, "config.rda"), new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.Compressed, files).Build().Bytes);
    }

    /// <summary>
    /// Lists every file and folder of the installation with its size and write time.
    /// </summary>
    /// <returns>The lines</returns>
    private List<string> Snapshot()
    {
        return Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories)
                        .Order(StringComparer.Ordinal)
                        .Select(path => File.Exists(path)
                                            ? string.Create(CultureInfo.InvariantCulture, $"{path} {new FileInfo(path).Length} {File.GetLastWriteTimeUtc(path).Ticks}")
                                            : path)
                        .ToList();
    }

    #endregion // Helpers
}