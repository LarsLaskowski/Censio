using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameIconReader" />, with archives built in code from synthetic DDS files; no game icon is used.
/// </summary>
[TestClass]
public sealed class GameIconReaderTests
{
    #region Constants

    /// <summary>
    /// The name of the environment variable that names a real installation.
    /// </summary>
    private const string InstallationVariable = "CENSIO_ANNO_INSTALL";

    /// <summary>
    /// The archive name of the patch with number 300.
    /// </summary>
    private const string Patch300 = "zz_patchfiles_300.rda";

    /// <summary>
    /// The rest of the path of an icon that the tests name.
    /// </summary>
    private const string RestA = "goods/alpha";

    /// <summary>
    /// The rest of the path of a second icon that the tests name.
    /// </summary>
    private const string RestB = "goods/beta";

    /// <summary>
    /// The number of distinct paths of the test of the total bound.
    /// </summary>
    private const int TotalBoundPaths = 21;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The temporary installation folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-icons-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameIconReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public GameIconReaderTests(TestContext testContext)
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
    /// Gets the <c>maindata</c> folder of the temporary installation.
    /// </summary>
    private string MainData => Path.Combine(_root, "maindata");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Deletes the temporary installation.
    /// </summary>
    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    #region AC2

    /// <summary>
    /// The archive with the highest patch number wins over the others and over <c>ui.rda</c>, whatever the order of the files.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameIconReaderLoadHighestPatchNumberWins()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive("zz_patchfiles_100.rda", (Inner(RestA), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive(Patch300, (Inner(RestA), DdsFileBuilder.Build(12, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive("zz_patchfiles_200.rda", (Inner(RestA), DdsFileBuilder.Build(16, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var icons = LoadSet(CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        AssertSize(icons.Find(1), 12, 4);
    }

    /// <summary>
    /// A path no patch archive holds is read from <c>ui.rda</c>, and without any patch archive <c>ui.rda</c> is used.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameIconReaderLoadUiArchiveUsedWithoutPatchCopy()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)), (Inner(RestB), DdsFileBuilder.Build(8, 8, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive(Patch300, (Inner(RestA), DdsFileBuilder.Build(12, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA)), Entry(2, GameCatalogKind.Good, Asset(RestB)));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        AssertSize(icons.Find(1), 12, 4);
        AssertSize(icons.Find(2), 8, 8);
    }

    /// <summary>
    /// Patch numbers are compared as numbers, not as text: 10 comes after 9 and 2.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameIconReaderLoadPatchNumbersOrderedNumerically()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive("zz_patchfiles_9.rda", (Inner(RestA), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive("zz_patchfiles_10.rda", (Inner(RestA), DdsFileBuilder.Build(12, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive("zz_patchfiles_2.rda", (Inner(RestA), DdsFileBuilder.Build(16, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var icons = LoadSet(CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        AssertSize(icons.Find(1), 12, 4);
    }

    /// <summary>
    /// A patch archive in a folder below <c>maindata</c> counts, with a file name in any case.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameIconReaderLoadPatchArchiveInSubfolderCounts()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive(Path.Combine("sub", "ZZ_PatchFiles_007.RDA"), (Inner(RestA), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var icons = LoadSet(CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        AssertSize(icons.Find(1), 8, 4);
    }

    /// <summary>
    /// The patch number of a file name is read as a 32-bit decimal number, and anything else gives <see langword="null" />.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="expected">The number, or <see langword="null" /></param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("zz_patchfiles_300.rda", 300u)]
    [DataRow("ZZ_PatchFiles_7.RDA", 7u)]
    [DataRow("zz_patchfiles_007.rda", 7u)]
    [DataRow("zz_patchfiles_0.rda", 0u)]
    [DataRow("zz_patchfiles_4294967295.rda", 4294967295u)]
    [DataRow("zz_patchfiles_4294967296.rda", null)]
    [DataRow("zz_patchfiles_.rda", null)]
    [DataRow("zz_patchfiles_-1.rda", null)]
    [DataRow("zz_patchfiles_+1.rda", null)]
    [DataRow("zz_patchfiles_ 1.rda", null)]
    [DataRow("zz_patchfiles_1x.rda", null)]
    [DataRow("zz_patchfiles_\u0661.rda", null)]
    [DataRow("zz_patchfiles_\uFF11.rda", null)]
    [DataRow("zz_patchfiles_1.rdx", null)]
    [DataRow("zz_patchfiles_1", null)]
    [DataRow("xzz_patchfiles_1.rda", null)]
    [DataRow("ui.rda", null)]
    [DataRow("", null)]
    public void GameIconReaderPatchNumberOfRecognisesForms(string fileName, uint? expected)
    {
        // Arrange & Act
        var number = GameIconReader.PatchNumberOf(fileName);

        // Assert
        Assert.AreEqual(expected, number, $"The patch number of '{fileName}' should be as the form says");
    }

    #endregion // AC2

    #region AC3

    /// <summary>
    /// Without <c>ui.rda</c> no entry has an icon and the error names the archive.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadUiArchiveMissingFails()
    {
        // Arrange
        WriteArchive(Patch300, (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var result = GameIconReader.Load(_root, CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.IsNull(result.Icons, "No entry should have an icon");
        Assert.IsNotNull(result.Error, "The missing archive should be reported");
        Assert.AreEqual(GameIconErrorKind.ArchiveMissing, result.Error.Kind, "The kind should be ArchiveMissing");
        Assert.AreEqual(GameIconReader.UiArchiveName, result.Error.Archive, "The error should name the base archive");
    }

    /// <summary>
    /// An installation without a <c>maindata</c> folder is a missing base archive, not a failure of the listing.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadNoMainDataFolderFailsAsArchiveMissing()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        // Act
        var result = GameIconReader.Load(_root, CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.IsNull(result.Icons, "No entry should have an icon");
        Assert.AreEqual(GameIconErrorKind.ArchiveMissing, result.Error?.Kind, "The kind should be ArchiveMissing");
    }

    /// <summary>
    /// An icon archive that cannot be opened ends the step with no icon and names the archive.
    /// </summary>
    /// <param name="archive">The relative path of the broken archive</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(Patch300)]
    [DataRow("ui.rda")]
    public void GameIconReaderLoadPatchArchiveUnreadableFails(string archive)
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        Directory.CreateDirectory(MainData);
        File.WriteAllBytes(Path.Combine(MainData, archive), new byte[100]);

        // Act
        var result = GameIconReader.Load(_root, CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.IsNull(result.Icons, "No entry should have an icon");
        Assert.IsNotNull(result.Error, "The unreadable archive should be reported");
        Assert.AreEqual(GameIconErrorKind.ArchiveUnreadable, result.Error.Kind, "The kind should be ArchiveUnreadable");
        Assert.AreEqual(archive, result.Error.Archive, "The error should name the archive");
    }

    /// <summary>
    /// Two patch archives with the same number, <c>300</c> and <c>0300</c>, end the step with no icon.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadDuplicatePatchNumberFails()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive(Patch300, (Inner(RestA), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive("zz_patchfiles_0300.rda", (Inner(RestA), DdsFileBuilder.Build(12, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var result = GameIconReader.Load(_root, CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.IsNull(result.Icons, "No entry should have an icon");
        Assert.IsNotNull(result.Error, "The duplicate number should be reported");
        Assert.AreEqual(GameIconErrorKind.DuplicateArchiveNumber, result.Error.Kind, "The kind should be DuplicateArchiveNumber");
        Assert.AreEqual(Patch300, result.Error.Archive, "The error should name the second archive in the listing order");
    }

    /// <summary>
    /// A file named like a patch archive whose number is no number or exceeds 32 bits, and any other file, is no icon
    /// archive, even when it cannot be opened.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadOtherArchivesAreNoIconArchives()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        Directory.CreateDirectory(MainData);

        Array.ForEach(["zz_patchfiles_x.rda", "zz_patchfiles_4294967296.rda", "config.rda", "zz_patchfiles_1.txt"], name => File.WriteAllBytes(Path.Combine(MainData, name), new byte[100]));

        // Act
        var result = GameIconReader.Load(_root, CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.IsNull(result.Error, "A file that is no icon archive should be ignored");
        AssertSize(result.Icons?.Find(1), 4, 4);
    }

    /// <summary>
    /// Entries whose asset names no icon are counted as not named, and the others are unaffected.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadIconNotNamedCounted()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, null), Entry(2, GameCatalogKind.Region, null), Entry(3, GameCatalogKind.Good, Asset(RestA)));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        Assert.AreEqual(2, icons.Count(GameIconProblem.NotNamed), "Two entries should be counted as not named");
        Assert.AreEqual(GameIconProblem.NotNamed, icons.Check(1), "The good should be not named");
        Assert.AreEqual(GameIconProblem.NotNamed, icons.Check(2), "The region should be not named");
        Assert.IsNull(icons.Find(1), "A good without a named icon should have none");
        Assert.AreEqual(GameIconProblem.None, icons.Check(3), "The third entry should have its icon");
        Assert.AreEqual(1, icons.GoodsWithIcon, "One good should have an icon");
        Assert.AreEqual(0, icons.RegionsWithIcon, "No region should have an icon");
    }

    /// <summary>
    /// A path without the form of the rule and a path no archive holds are counted as missing.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadIconMissingCounted()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, "data/ui/other/x.png"),
                                Entry(2, GameCatalogKind.Good, "x.png"),
                                Entry(3, GameCatalogKind.Region, "data/ui/fhd/x.dds"),
                                Entry(4, GameCatalogKind.Good, "data/ui/fhd/.png"),
                                Entry(5, GameCatalogKind.Good, Asset("goods/absent")),
                                Entry(6, GameCatalogKind.Region, Asset(RestA)));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        Assert.AreEqual(5, icons.Count(GameIconProblem.Missing), "Five entries should be counted as missing");
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(1), "A path with another prefix should be missing");
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(3), "A path with another extension should be missing");
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(5), "A path no archive holds should be missing");
        Assert.AreEqual(GameIconProblem.None, icons.Check(6), "The region with a held path should have its icon");
        Assert.AreEqual(1, icons.RegionsWithIcon, "One region should have an icon");
    }

    /// <summary>
    /// An icon entry above the entry bound is unreadable and the other icons are unaffected.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadIconOverEntryBoundUnreadable()
    {
        // Arrange
        WriteBlockArchive("ui.rda", RdaArchiveBuilder.Compressed, (Inner(RestA), new byte[GameIconReader.MaxIconEntryBytes + 1]), (Inner(RestB), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA)), Entry(2, GameCatalogKind.Good, Asset(RestB)));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        Assert.AreEqual(GameIconProblem.Unreadable, icons.Check(1), "The entry above the bound should be unreadable");
        Assert.AreEqual(1, icons.Count(GameIconProblem.Unreadable), "One entry should be counted as unreadable");
        AssertSize(icons.Find(2), 4, 4);
    }

    /// <summary>
    /// A path one archive lists twice, also ignoring case, is unreadable, and the same path in two archives is not.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadIconListedTwiceUnreadable()
    {
        // Arrange
        var file = DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1);

        WriteArchive("ui.rda", (Inner("goods/Dup"), file), (Inner("goods/dup"), file), (Inner(RestB), file), (Inner(RestB), file), (Inner("goods/twin"), file));
        WriteArchive(Patch300, (Inner("goods/twin"), file));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, Asset("goods/dup")), Entry(2, GameCatalogKind.Good, Asset(RestB)), Entry(3, GameCatalogKind.Good, Asset("goods/twin")));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        Assert.AreEqual(GameIconProblem.Unreadable, icons.Check(1), "Two entries that differ in case only should be unreadable");
        Assert.AreEqual(GameIconProblem.Unreadable, icons.Check(2), "Two equal entries should be unreadable");
        Assert.AreEqual(GameIconProblem.None, icons.Check(3), "A path in two archives should have an icon");
    }

    /// <summary>
    /// A file that is no accepted DDS image is unreadable, also one wider than the bound.
    /// </summary>
    /// <param name="position">The position of the changed 32-bit field</param>
    /// <param name="value">The new value</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(DdsFileBuilder.MagicPosition, 0)]
    [DataRow(DdsFileBuilder.DxgiFormatPosition, 71)]
    [DataRow(DdsFileBuilder.ArraySizePosition, 2)]
    public void GameIconReaderLoadNotAcceptedDdsUnreadable(int position, int value)
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Patch(DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1), position, value)));

        // Act
        var icons = LoadSet(CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.AreEqual(GameIconProblem.Unreadable, icons.Check(1), "A file the DDS reader refuses should be unreadable");
        Assert.IsNull(icons.Find(1), "The entry should have no icon");
    }

    /// <summary>
    /// An image wider than the icon bound is unreadable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadSideAboveBoundUnreadable()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(GameIconReader.MaxIconSide + 4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var icons = LoadSet(CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA))));

        // Assert
        Assert.AreEqual(GameIconProblem.Unreadable, icons.Check(1), "A width above the bound should be unreadable");
    }

    /// <summary>
    /// An icon path with <c>..</c>, a rooted path or a drive letter is only compared with archive entry paths: an entry with
    /// the literal path is found, and a file the path would reach on disk is not used.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderLoadParentPathOnlyComparedWithEntries()
    {
        // Arrange
        var file = DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1);

        WriteArchive("ui.rda", ("data/ui/4k/../x_0.dds", file));
        Directory.CreateDirectory(Path.Combine(_root, "data", "ui"));
        File.WriteAllBytes(Path.Combine(_root, "data", "ui", "y_0.dds"), file);
        Directory.CreateDirectory(Path.Combine(MainData, "data", "ui"));
        File.WriteAllBytes(Path.Combine(MainData, "data", "ui", "y_0.dds"), file);

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, "data/ui/fhd/../x.png"),
                                Entry(2, GameCatalogKind.Good, "data/ui/fhd/../y.png"),
                                Entry(3, GameCatalogKind.Good, "/data/ui/fhd/x.png"),
                                Entry(4, GameCatalogKind.Good, "C:\\data\\ui\\fhd\\x.png"),
                                Entry(5, GameCatalogKind.Good, "C:/data/ui/fhd/x.png"));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        AssertSize(icons.Find(1), 4, 4);
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(2), "A path that matches no entry should be missing, whatever a file system would reach");
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(3), "A rooted path should match no entry");
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(4), "A path with a drive letter should match no entry");
        Assert.AreEqual(GameIconProblem.Missing, icons.Check(5), "A path with a drive letter and slashes should match no entry");
    }

    /// <summary>
    /// A path of at most the path bound characters is mapped and a longer one is not.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameIconReaderEntryPathOfEnforcesLengthBound()
    {
        // Arrange
        var atBound = $"data/ui/fhd/{new string('a', GameIconReader.MaxIconPathLength - "data/ui/fhd/.png".Length)}.png";
        var aboveBound = $"data/ui/fhd/{new string('a', GameIconReader.MaxIconPathLength - "data/ui/fhd/.png".Length + 1)}.png";

        // Act
        var mapped = GameIconReader.EntryPathOf(atBound);
        var refused = GameIconReader.EntryPathOf(aboveBound);

        // Assert
        Assert.AreEqual(GameIconReader.MaxIconPathLength, atBound.Length, "The path at the bound should be as long as the bound");
        Assert.IsNotNull(mapped, "A path of the bound's length should be mapped");
        Assert.IsNull(refused, "A path above the bound should not be mapped");
    }

    /// <summary>
    /// The PNGs of the icons together above the bound end the step with no icon, and the error names the archive being read.
    /// The PNGs hardly compress: this takes the time of encoding 21 images of 2,048 by 2,048 pixels.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    [Timeout(120000, CooperativeCancellation = true)]
    public void GameIconReaderLoadAboveTotalIconBytesBoundFails()
    {
        // Arrange
        var rests = Enumerable.Range(0, TotalBoundPaths).Select(index => string.Create(CultureInfo.InvariantCulture, $"noise/n{index}")).ToArray();

        WriteBytes("ui.rda", SharedArchive(rests.Select(Inner).ToArray(), DdsFileBuilder.BuildNoise(GameIconReader.MaxIconSide)));

        var catalog = CatalogOf(rests.Select((rest, index) => Entry(index + 1, GameCatalogKind.Good, Asset(rest))).ToArray());

        // Act
        var result = GameIconReader.Load(_root, catalog);

        // Assert
        Assert.IsNull(result.Icons, "No entry should have an icon");
        Assert.IsNotNull(result.Error, "The total bound should be reported");
        Assert.AreEqual(GameIconErrorKind.IconSetTooLarge, result.Error.Kind, "The kind should be IconSetTooLarge");
        Assert.AreEqual("ui.rda", result.Error.Archive, "The error should name the archive being read");
    }

    #endregion // AC3

    #region AC4

    /// <summary>
    /// On a real installation every good of the pipe fixture the catalog names has an icon, and no region entry that names
    /// a path of the rule is unreadable. The test writes GUIDs, kinds and counts, never a name, a path or an image.
    /// </summary>
    /// <returns>A task that represents the test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task GameIconReaderRealInstallationGivesEveryFixtureGoodAnIcon()
    {
        // Arrange
        var installation = Environment.GetEnvironmentVariable(InstallationVariable);

        if (string.IsNullOrEmpty(installation))
        {
            Assert.Inconclusive($"{InstallationVariable} is not set: the icons did not run on a real installation");
        }

        var guids = await GameCatalogReaderTests.ReadPipeGuidsAsync();
        var catalogResult = GameCatalogReader.Load(installation, RdaLimits.Default);

        Assert.IsNotNull(catalogResult.Catalog, $"The installation should give a catalog, but failed with {catalogResult.Error?.Kind}");

        var catalog = catalogResult.Catalog;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = GameIconReader.Load(installation, catalog);

        stopwatch.Stop();

        // Assert
        Assert.IsNull(result.Error, $"The installation should give icons, but failed with {result.Error?.Kind} ({result.Error?.Archive})");
        Assert.IsNotNull(result.Icons, "The installation should give icons");

        var icons = result.Icons;
        var goods = guids.Goods.Where(guid => catalog.Find(guid)?.Kind == GameCatalogKind.Good).Order().ToList();
        var without = goods.Where(guid => icons.Find(guid) is null).Select(guid => string.Create(CultureInfo.InvariantCulture, $"{guid} {icons.Check(guid)}")).ToList();
        var unreadableRegions = catalog.Entries.Where(entry => entry.Kind == GameCatalogKind.Region && GameIconReader.EntryPathOf(entry.IconFilename) is not null && icons.Check(entry.AssetGuid) == GameIconProblem.Unreadable)
                                               .Select(entry => entry.AssetGuid)
                                               .ToList();

        WriteReport(installation, catalog, icons, goods.Count, without.Count, stopwatch.ElapsedMilliseconds);

        Assert.IsEmpty(without, $"Every good of the fixture the catalog names should have an icon; without: {string.Join(", ", without)}");
        Assert.IsEmpty(unreadableRegions, $"No region entry that names a path of the rule should be unreadable; unreadable: {string.Join(", ", unreadableRegions)}");
    }

    #endregion // AC4

    #region AC6

    /// <summary>
    /// Loading icons changes nothing in the installation and writes no file to the working folder.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameIconReaderLoadWritesNothing()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));
        WriteArchive(Patch300, (Inner(RestB), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var before = Snapshot();
        var workingFolder = Environment.CurrentDirectory;
        var workingBefore = Directory.GetFileSystemEntries(workingFolder).Length;
        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA)), Entry(2, GameCatalogKind.Region, Asset(RestB)));

        // Act
        var result = GameIconReader.Load(_root, catalog);

        // Assert
        Assert.IsNotNull(result.Icons, $"The installation should give icons, but failed with {result.Error?.Kind}");
        Assert.AreEqual(2, result.Icons.GoodsWithIcon + result.Icons.RegionsWithIcon, "Both entries should have an icon");
        Assert.AreSequenceEqual(before, Snapshot(), "The installation should be unchanged");
        Assert.HasCount(workingBefore, Directory.GetFileSystemEntries(workingFolder), "Nothing should be written to the working folder");
    }

    #endregion // AC6

    #region AC7

    /// <summary>
    /// The asset path <c>data/ui/fhd/&lt;rest&gt;.png</c> maps to <c>data/ui/4k/&lt;rest&gt;_0.dds</c>, the prefix and the extension
    /// compared ignoring case, the rest kept as written, surrounding white space trimmed; any other form gives <see langword="null" />.
    /// </summary>
    /// <param name="iconFilename">The value of the asset</param>
    /// <param name="expected">The path in the archive, or <see langword="null" /></param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("data/ui/fhd/a/b.png", "data/ui/4k/a/b_0.dds")]
    [DataRow("DATA/UI/FHD/A/B.PNG", "data/ui/4k/A/B_0.dds")]
    [DataRow("Data/Ui/Fhd/Mixed/Case.Png", "data/ui/4k/Mixed/Case_0.dds")]
    [DataRow("  data/ui/fhd/a/b.png \t", "data/ui/4k/a/b_0.dds")]
    [DataRow("data/ui/fhd/a.png.png", "data/ui/4k/a.png_0.dds")]
    [DataRow("data/ui/fhd/../x.png", "data/ui/4k/../x_0.dds")]
    [DataRow("data/ui/other/a.png", null)]
    [DataRow("data/ui/4k/a_0.dds", null)]
    [DataRow("data/ui/fhd/a.dds", null)]
    [DataRow("data/ui/fhd/a", null)]
    [DataRow("data/ui/fhd/.png", null)]
    [DataRow("data/ui/fhd/", null)]
    [DataRow("data\\ui\\fhd\\a.png", null)]
    [DataRow("/data/ui/fhd/a.png", null)]
    [DataRow("xdata/ui/fhd/a.png", null)]
    [DataRow("", null)]
    [DataRow("   ", null)]
    [DataRow(null, null)]
    public void GameIconReaderEntryPathOfMapsFhdToFourK(string? iconFilename, string? expected)
    {
        // Arrange & Act
        var path = GameIconReader.EntryPathOf(iconFilename);

        // Assert
        Assert.AreEqual(expected, path, "The path should map as the rule says");
    }

    /// <summary>
    /// The lookup ignores case: an archive entry whose case differs from the asset value in every part is found, which a
    /// read with the mapped path would not.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameIconReaderLoadLookupIgnoresCase()
    {
        // Arrange
        WriteArchive("ui.rda", ("DATA/UI/4K/Goods/ALPHA_0.DDS", DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        // Act
        var icons = LoadSet(CatalogOf(Entry(1, GameCatalogKind.Good, "data/ui/fhd/goods/alpha.png"), Entry(2, GameCatalogKind.Good, "Data/UI/FHD/goods/Alpha.PNG")));

        // Assert
        AssertSize(icons.Find(1), 8, 4);
        AssertSize(icons.Find(2), 8, 4);
    }

    /// <summary>
    /// Entries that name the same path, also in another case, share one icon, and the PNG bytes count it once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameIconReaderLoadSharesIconBetweenEntries()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)), (Inner(RestB), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA)), Entry(2, GameCatalogKind.Good, Asset(RestA).ToUpperInvariant()), Entry(3, GameCatalogKind.Region, Asset(RestA)), Entry(4, GameCatalogKind.Good, Asset(RestB)));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        var first = icons.Find(1);

        Assert.IsNotNull(first, "The first entry should have an icon");
        Assert.AreSame(first, icons.Find(2), "A second good with the path should share the icon");
        Assert.AreSame(first, icons.Find(3), "A region with the path should share the icon");
        Assert.AreEqual(3, icons.GoodsWithIcon, "Three goods should have an icon");
        Assert.AreEqual(1, icons.RegionsWithIcon, "One region should have an icon");
        Assert.AreEqual(first.Png.Length + icons.Find(4)!.Png.Length, icons.PngBytes, "The PNG bytes should count each distinct icon once");
    }

    /// <summary>
    /// Workforce and building entries get no icon and count in neither total.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameIconReaderLoadSkipsWorkforceAndBuildings()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)), (Inner(RestB), DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = CatalogOf(Entry(1, GameCatalogKind.Good, Asset(RestA)), Entry(2, GameCatalogKind.Workforce, Asset(RestA)), Entry(3, GameCatalogKind.Building, Asset(RestB)));

        // Act
        var icons = LoadSet(catalog);

        // Assert
        Assert.IsNull(icons.Find(2), "A workforce entry should have no icon");
        Assert.IsNull(icons.Find(3), "A building entry should have no icon");
        Assert.AreEqual(1, icons.GoodsWithIcon, "Only the good should count");
        Assert.AreEqual(0, icons.RegionsWithIcon, "No region should count");
        Assert.AreEqual(icons.Find(1)!.Png.Length, icons.PngBytes, "Only the good's PNG should be held");
    }

    /// <summary>
    /// An ambiguous or unknown GUID has no icon, and an entry without names can have one.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void GameIconReaderLoadAmbiguousGuidHasNoIcon()
    {
        // Arrange
        WriteArchive("ui.rda", (Inner(RestA), DdsFileBuilder.Build(4, 4, DdsFileBuilder.DxgiBc7Srgb, 1)));

        var catalog = new GameCatalog([Entry(77, GameCatalogKind.Good, Asset(RestA)), Entry(78, GameCatalogKind.Good, Asset(RestA))], [77], 0, 0);

        // Act
        var icons = LoadSet(catalog);

        // Assert
        Assert.IsNull(icons.Find(77), "An ambiguous GUID should have no icon");
        Assert.AreEqual(GameIconProblem.NoEntry, icons.Check(77), "An ambiguous GUID should have no entry");
        Assert.IsNull(icons.Find(999), "An unknown GUID should have no icon");
        Assert.AreEqual(GameIconProblem.NoEntry, icons.Check(999), "An unknown GUID should have no entry");
        AssertSize(icons.Find(78), 4, 4);
    }

    #endregion // AC7

    /// <summary>
    /// Creates a catalog entry that has no names.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="kind">The kind</param>
    /// <param name="iconFilename">The value the asset names</param>
    /// <returns>The entry</returns>
    private static GameCatalogEntry Entry(int guid, GameCatalogKind kind, string? iconFilename)
    {
        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = kind,
                   IconFilename = iconFilename
               };
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
    /// Gets the value an asset names for a rest.
    /// </summary>
    /// <param name="rest">The rest of the path</param>
    /// <returns>The path in the form of the asset</returns>
    private static string Asset(string rest)
    {
        return $"data/ui/fhd/{rest}.png";
    }

    /// <summary>
    /// Gets the path of the entry in the archive for a rest.
    /// </summary>
    /// <param name="rest">The rest of the path</param>
    /// <returns>The path in the archive</returns>
    private static string Inner(string rest)
    {
        return $"data/ui/4k/{rest}_0.dds";
    }

    /// <summary>
    /// Checks the size and the PNG signature of an icon.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <param name="width">The expected width</param>
    /// <param name="height">The expected height</param>
    private static void AssertSize(GameIcon? icon, int width, int height)
    {
        Assert.IsNotNull(icon, "The entry should have an icon");
        Assert.AreEqual(width, icon.Width, "The icon should have the width of the winning file");
        Assert.AreEqual(height, icon.Height, "The icon should have the height of the winning file");
        Assert.AreEqual(width, BinaryPrimitives.ReadInt32BigEndian(icon.Png.Span.Slice(16, 4)), "The PNG header should hold the width");
        Assert.AreEqual(height, BinaryPrimitives.ReadInt32BigEndian(icon.Png.Span.Slice(20, 4)), "The PNG header should hold the height");
        Assert.AreEqual(0x89504E47u, BinaryPrimitives.ReadUInt32BigEndian(icon.Png.Span), "The icon should be a PNG");
    }

    /// <summary>
    /// Builds an archive that lists many paths over the data of one file.
    /// </summary>
    /// <param name="paths">The paths</param>
    /// <param name="content">The content every path reads</param>
    /// <returns>The archive</returns>
    private static byte[] SharedArchive(string[] paths, byte[] content)
    {
        var files = paths.Select((path, index) => (Path: path, Content: index == 0 ? content : new byte[1])).ToArray();
        var offset = 0UL;
        var stored = 0UL;
        var size = 0UL;
        var seen = 0;

        void Redirect(byte[] entry)
        {
            if (seen++ == 0)
            {
                offset = BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(RdaArchiveBuilder.EntryOffsetPosition));
                stored = BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(RdaArchiveBuilder.EntryStoredSizePosition));
                size = BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(RdaArchiveBuilder.EntrySizePosition));

                return;
            }

            RdaArchiveBuilder.WriteUInt64(entry, RdaArchiveBuilder.EntryOffsetPosition, offset);
            RdaArchiveBuilder.WriteUInt64(entry, RdaArchiveBuilder.EntryStoredSizePosition, stored);
            RdaArchiveBuilder.WriteUInt64(entry, RdaArchiveBuilder.EntrySizePosition, size);
        }

        return new RdaArchiveBuilder().AddBlockEditingEntries(0, Redirect, files).Build().Bytes;
    }

    /// <summary>
    /// Counts the icon paths of the catalog that more than one icon archive holds, and those whose entries differ in size.
    /// </summary>
    /// <param name="installation">The installation folder</param>
    /// <param name="catalog">The catalog</param>
    /// <returns>The number of paths held by more than one archive and the number of them whose entry sizes differ</returns>
    private static (int Shared, int Differing) CountCopies(string installation, GameCatalog catalog)
    {
        var mainData = Path.Combine(installation, "maindata");
        var wanted = catalog.Entries.Select(entry => GameIconReader.EntryPathOf(entry.IconFilename)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sizes = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
        var archives = Directory.EnumerateFiles(mainData, "*.rda", SearchOption.AllDirectories)
                                .Where(file => string.Equals(Path.GetRelativePath(mainData, file), GameIconReader.UiArchiveName, StringComparison.OrdinalIgnoreCase) || GameIconReader.PatchNumberOf(Path.GetFileName(file)) is not null);

        foreach (var file in archives)
        {
            var opened = RdaArchive.Open(file, RdaLimits.Default);

            Assert.IsNotNull(opened.Archive, "An icon archive should open");

            using var archive = opened.Archive;

            foreach (var group in archive.Entries.Where(entry => wanted.Contains(entry.Path)).GroupBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
            {
                sizes.TryAdd(group.Key, []);
                sizes[group.Key].Add(group.First().Size);
            }
        }

        var shared = sizes.Values.Where(list => list.Count > 1).ToList();

        return (shared.Count, shared.Count(list => list.Distinct().Count() > 1));
    }

    /// <summary>
    /// Writes an archive of uncompressed files below <c>maindata</c>.
    /// </summary>
    /// <param name="relativePath">The path below <c>maindata</c></param>
    /// <param name="files">The paths and contents of the files</param>
    private void WriteArchive(string relativePath, params (string Path, byte[] Content)[] files)
    {
        WriteBlockArchive(relativePath, 0, files);
    }

    /// <summary>
    /// Writes an archive of one block below <c>maindata</c>.
    /// </summary>
    /// <param name="relativePath">The path below <c>maindata</c></param>
    /// <param name="flags">The flags of the block</param>
    /// <param name="files">The paths and contents of the files</param>
    private void WriteBlockArchive(string relativePath, uint flags, params (string Path, byte[] Content)[] files)
    {
        WriteBytes(relativePath, new RdaArchiveBuilder().AddBlock(flags, files).Build().Bytes);
    }

    /// <summary>
    /// Writes a file below <c>maindata</c>.
    /// </summary>
    /// <param name="relativePath">The path below <c>maindata</c></param>
    /// <param name="bytes">The content</param>
    private void WriteBytes(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(MainData, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Loads the icons of the temporary installation, requiring that the step succeeds.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <returns>The icons</returns>
    private GameIconSet LoadSet(GameCatalog catalog)
    {
        var result = GameIconReader.Load(_root, catalog);

        Assert.IsNull(result.Error, $"The icon step should succeed, but failed with {result.Error?.Kind} ({result.Error?.Archive})");
        Assert.IsNotNull(result.Icons, "The icon step should give icons");

        return result.Icons;
    }

    /// <summary>
    /// Lists every file and folder of the temporary installation with its size and write time.
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

    /// <summary>
    /// Writes the report of the installation test: GUIDs, kinds and counts only.
    /// </summary>
    /// <param name="installation">The installation folder</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <param name="goodCount">The number of goods of the fixture the catalog names</param>
    /// <param name="withoutCount">How many of them have no icon</param>
    /// <param name="milliseconds">The duration of the icon step</param>
    private void WriteReport(string installation, GameCatalog catalog, GameIconSet icons, int goodCount, int withoutCount, long milliseconds)
    {
        using var process = Process.GetCurrentProcess();

        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Fixture goods named by the catalog: {goodCount}, without an icon: {withoutCount}"));
        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Entries with an icon: {icons.GoodsWithIcon} goods, {icons.RegionsWithIcon} regions; without: {icons.Count(GameIconProblem.NotNamed)} not named, {icons.Count(GameIconProblem.Missing)} missing, {icons.Count(GameIconProblem.Unreadable)} unreadable"));

        foreach (var region in catalog.Entries.Where(entry => entry.Kind == GameCatalogKind.Region).OrderBy(entry => entry.AssetGuid))
        {
            var icon = icons.Find(region.AssetGuid);

            TestContext.WriteLine(icon is null
                                      ? string.Create(CultureInfo.InvariantCulture, $"Region {region.AssetGuid}: {icons.Check(region.AssetGuid)}")
                                      : string.Create(CultureInfo.InvariantCulture, $"Region {region.AssetGuid}: icon {icon.Width}x{icon.Height}, {icon.Png.Length} PNG bytes"));
        }

        var largest = catalog.Entries.Select(entry => icons.Find(entry.AssetGuid)).OfType<GameIcon>().MaxBy(icon => icon.Png.Length);
        var (shared, differing) = CountCopies(installation, catalog);

        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Icon paths held by more than one archive: {shared}, of them with entries of different sizes: {differing}"));
        TestContext.WriteLine(largest is null ? "Largest icon: none" : string.Create(CultureInfo.InvariantCulture, $"Largest icon: {largest.Width}x{largest.Height}, {largest.Png.Length} PNG bytes"));
        TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"PNG bytes in total: {icons.PngBytes}, elapsed: {milliseconds} ms, peak working set: {process.PeakWorkingSet64 / (1024 * 1024)} MiB"));
    }

    #endregion // Methods
}