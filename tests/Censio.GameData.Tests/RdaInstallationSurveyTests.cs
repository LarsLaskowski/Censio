using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="RdaInstallationSurvey" />.
/// </summary>
[TestClass]
public sealed class RdaInstallationSurveyTests
{
    #region Constants

    /// <summary>
    /// The name of the environment variable that names a real installation.
    /// </summary>
    private const string InstallationVariable = "CENSIO_ANNO_INSTALL";

    /// <summary>
    /// A text that stands for archive content and must never reach a line of the report.
    /// </summary>
    private const string Secret = "INVENTED-CONTENT-7QX";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The folder of the installation of a synthetic test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-rda-survey-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The folder a test made unreadable, restored before the cleanup.
    /// </summary>
    private string? _lockedFolder;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaInstallationSurveyTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public RdaInstallationSurveyTests(TestContext testContext)
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
        if (_lockedFolder is not null && Directory.Exists(_lockedFolder) && IsUnix())
        {
            SetUnixMode(_lockedFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    #region AC5

    /// <summary>
    /// The sample reads name the archive, the entry, the size and the recognized marker.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunReadsAssetAndIconWithMarkers()
    {
        // Arrange
        var asset = WithByteOrderMark("<Assets>" + Secret + "</Assets>");
        var icon = Sized("DDS ", 40);
        const string assetPath = "data/base/config/game/assets.xml";
        const string iconPath = "data/ui/4k/base/icon_content/workforce/icon_2d_scribe_0.dds";

        Place("config.rda", (assetPath, asset));
        Place("zz_patchfiles_200.rda", (iconPath, icon));

        // Act
        var report = Run();

        // Assert
        Assert.Contains($"Asset read: config.rda | {assetPath} | {asset.Length} | XML", report.Lines, "The asset sample should name archive, entry, size and the XML marker, also after a byte order mark and white space");
        Assert.Contains($"Icon read: zz_patchfiles_200.rda | {iconPath} | {icon.Length} | DDS", report.Lines, "The icon sample should name archive, entry, size and the DDS marker, also when no ui.rda exists");
        Assert.IsTrue(report.AssetRead, "The asset sample was read and recognized");
        Assert.IsTrue(report.IconRead, "The icon sample was read and recognized");
    }

    /// <summary>
    /// The smallest asset XML of <c>config.rda</c> and the smallest icon of <c>ui.rda</c> are read, not those of other archives.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunPrefersConfigAndUiArchives()
    {
        // Arrange
        Place("a_patch.rda", ("data/base/config/assets_tiny.xml", Sized("<t>", 8)), ("data/ui/icon_tiny.dds", Sized("DDS ", 8)));
        Place("config.rda", ("data/base/config/game/assets_big.xml", Sized("<b>", 60)), ("data/base/config/game/assets_small.xml", Sized("<s>", 30)), ("data/base/config/game/other.xml", Sized("<o>", 10)));
        Place("ui.rda", ("data/ui/icon_big.dds", Sized("DDS ", 50)), ("data/ui/icon_small.dds", Sized("DDS ", 20)));

        // Act
        var report = Run();

        // Assert
        Assert.Contains("Asset read: config.rda | data/base/config/game/assets_small.xml | 30 | XML", report.Lines, "The smallest asset XML of config.rda should be read, though a smaller XML without asset and a smaller one in another archive exist");
        Assert.Contains("Icon read: ui.rda | data/ui/icon_small.dds | 20 | DDS", report.Lines, "The smallest icon of ui.rda should be read, though a smaller one in another archive exists");
    }

    /// <summary>
    /// Without an asset XML the smallest XML of <c>config.rda</c> is read, also when it is UTF-16.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunFallsBackToAnyConfigXml()
    {
        // Arrange
        var small = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<x/>")).ToArray();

        Place("a.rda", ("data/base/config/assets.xml", Sized("<t>", 4)));
        Place("config.rda", ("data/base/config/gui/texts_english.xml", Sized("<e>", 90)), ("data/base/config/game/zz.xml", small));

        // Act
        var report = Run();

        // Assert
        Assert.Contains($"Asset read: config.rda | data/base/config/game/zz.xml | {small.Length} | XML", report.Lines, "The smallest XML of config.rda should be read when no entry has asset in its path, with the UTF-16 byte order mark skipped");
        Assert.Contains("Icon read: none found", report.Lines, "Without an icon the icon read should say so");
        Assert.IsTrue(report.AssetRead, "The fallback sample was recognized");
        Assert.IsFalse(report.IconRead, "No icon was read");
    }

    /// <summary>
    /// Every archive is listed with its relative path, its counts and in path order ignoring case.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunReportsPathInEveryArchive()
    {
        // Arrange
        var first = new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Sized("a", 3)), ("data/dup.txt", Sized("d", 3)), ("data/dup.txt", Sized("e", 3)))
                                           .AddBlock(RdaArchiveBuilder.Compressed, ("data/z.txt", Sized("z", 30)))
                                           .AddBlock(RdaArchiveBuilder.Encrypted, ("data/sealed.bin", Sized("s", 3)))
                                           .Build();

        File.WriteAllBytes(PlaceFolder("A.rda"), first.Bytes);
        Place("b/x.rda", ("data/b.txt", Sized("b", 3)));
        Place("C.rda", ("data/c.txt", Sized("c", 3)));

        // Act
        var report = Run();
        var archiveLines = report.Lines.Where(line => line.Contains(" | entries ", StringComparison.Ordinal)).ToArray();

        // Assert
        Assert.AreEqual("Archives: 3", report.Lines[0], "The first line should count the archives");
        Assert.AreSequenceEqual(["A.rda", Path.Combine("b", "x.rda"), "C.rda"], archiveLines.Select(line => line.Split(" | ")[0]).ToArray(), "The archives should be listed by relative path, ordinal and ignoring case");
        Assert.StartsWith("A.rda | entries 4 | flags ", archiveLines[0], "The line should hold the entry count, which excludes the encrypted block");
        Assert.Contains("Compressed=1", archiveLines[0], "The line should count the entries per block flag");
        Assert.EndsWith(" | encrypted blocks 1 | duplicate paths 1", archiveLines[0], "The line should end with the encrypted blocks and the paths found more than once");
        Assert.Contains(" | entries 1 | ", archiveLines[1], "Every archive should get its own counts");
    }

    /// <summary>
    /// Per archive, the entries are counted per extension and per first two path segments, the larger count first.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunReportsExtensionAndFolderCounts()
    {
        // Arrange
        Place("config.rda",
              ("data/base/config/gui/texts_english.xml", Sized("<a>", 5)),
              ("data/base/config/game/datasets.xml", Sized("<b>", 5)),
              ("data/base/provinces/celtic/province.xml", Sized("<c>", 5)),
              ("data/base/readme.txt", Sized("t", 5)),
              ("data/ui/icon_a.dds", Sized("DDS ", 5)),
              ("data/ui/icon_b.dds", Sized("DDS ", 5)));

        // Act
        var report = Run();
        var index = IndexOfArchiveLine(report, "config.rda");

        // Assert
        Assert.AreEqual("  extensions: .xml=3, .dds=2, .txt=1", report.Lines[index + 1], "The extensions should be counted, the larger count first");
        Assert.AreEqual("  folders: data/base=4, data/ui=2", report.Lines[index + 2], "The first two path segments should be counted, the larger count first");
    }

    /// <summary>
    /// The XML entries whose path names a data kind are listed with their size and every archive that holds them.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunListsMatchingEntriesWithEveryArchive()
    {
        // Arrange
        Place("config.rda",
              ("data/base/config/game/datasets.xml", Sized("<d>", 12)),
              ("data/base/provinces/celtic/province_a.xml", Sized("<p>", 13)),
              ("data/base/config/gui/texts_english.xml", Sized("<t>", 14)),
              ("data/base/config/game/guids.xml", Sized("<g>", 15)),
              ("data/base/config/regions.xml", Sized("<r>", 16)),
              ("data/base/config/session_x.xml", Sized("<s>", 17)),
              ("data/base/config/game/assets.xml", Sized("<a>", 18)),
              ("data/zzz/ASSETS_last.xml", Sized("<l>", 19)),
              ("data/base/config/other.xml", Sized("<o>", 20)),
              ("data/base/config/assets_list.txt", Sized("a", 21)));
        Place("zz_patchfiles_200.rda", ("data/base/config/game/datasets.xml", Sized("<d>", 12)));

        // Act
        var report = Run();
        var section = Section(report, "Matching entries:");

        // Assert
        Assert.AreSequenceEqual([
                                    "data/base/config/game/assets.xml | 18 | config.rda",
                                    "data/base/config/game/datasets.xml | 12 | config.rda, zz_patchfiles_200.rda",
                                    "data/base/config/game/guids.xml | 15 | config.rda",
                                    "data/base/config/gui/texts_english.xml | 14 | config.rda",
                                    "data/base/config/regions.xml | 16 | config.rda",
                                    "data/base/config/session_x.xml | 17 | config.rda",
                                    "data/base/provinces/celtic/province_a.xml | 13 | config.rda",
                                    "data/zzz/ASSETS_last.xml | 19 | config.rda"
                                ],
                                section,
                                "Only XML entries with asset, text, guid, region, province, session or dataset in the path should be listed, sorted by path, with every archive that holds them");
    }

    /// <summary>
    /// The folders of DDS entries with icon in the path are listed with the count and the archives, 2D and 3D alike.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunListsIconFolders()
    {
        // Arrange
        const string folder = "data/ui/4k/base/icon_content/";

        Place("ui.rda",
              ($"{folder}workforce/icon_2d_scribe_0.dds", Sized("DDS ", 5)),
              ($"{folder}workforce/icon_3d_scribe_0.dds", Sized("DDS ", 5)),
              ($"{folder}production_goods/icon_2d_bread_0.dds", Sized("DDS ", 5)),
              ($"{folder}workforce/icon_meta.xml", Sized("<m>", 5)),
              ("data/ui/background.dds", Sized("DDS ", 5)));

        // Act
        var report = Run();

        // Assert
        string[] expected = [$"{folder}production_goods | 1 | ui.rda", $"{folder}workforce | 2 | ui.rda"];

        Assert.AreSequenceEqual(expected,
                                Section(report, "Icon folders:"),
                                "Only the folders of DDS entries with icon in the path should be listed, with their count and archives");
    }

    /// <summary>
    /// The language archives list every entry that is not audio or video.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunListsLanguageArchiveEntriesWithoutMedia()
    {
        // Arrange
        Place("de_de0.rda",
              ("data/base/config/gui/texts_german.xml", Sized("<g>", 9)),
              ("data/sounds/voice_01.wem", Sized("w", 3)),
              ("data/sounds/bank.bnk", Sized("b", 3)),
              ("data/video/intro.webm", Sized("v", 3)),
              ("data/base/config/gui/fonts.txt", Sized("f", 7)));
        Place("en_us0.rda", ("data/base/config/gui/texts_english.xml", Sized("<e>", 11)), ("data/sounds/voice_01.ogg", Sized("o", 3)));
        Place("fr_fr0.rda", ("data/base/config/gui/texts_french.xml", Sized("<f>", 13)));

        // Act
        var report = Run();
        var section = Section(report, "Language archive entries:").Order(StringComparer.Ordinal).ToArray();

        // Assert
        Assert.AreSequenceEqual([
                                    "de_de0.rda | data/base/config/gui/fonts.txt | 7",
                                    "de_de0.rda | data/base/config/gui/texts_german.xml | 9",
                                    "en_us0.rda | data/base/config/gui/texts_english.xml | 11"
                                ],
                                section,
                                "Only the entries of de_de0.rda and en_us0.rda that are not audio or video should be listed");
    }

    /// <summary>
    /// An archive that cannot be opened gets a line with the kind and the message, and the survey goes on.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunReportsArchiveErrorWithMessageAndContinues()
    {
        // Arrange
        Directory.CreateDirectory(MainData);
        File.WriteAllBytes(Path.Combine(MainData, "bad.rda"), new byte[1000]);
        Place("good.rda", ("data/a.txt", Sized("a", 3)));

        // Act
        var report = Run();
        var errorLine = report.Lines.Single(line => line.StartsWith("bad.rda | ", StringComparison.Ordinal));
        var message = errorLine["bad.rda | error UnsupportedArchive: ".Length..];

        // Assert
        Assert.StartsWith("bad.rda | error UnsupportedArchive: ", errorLine, "The line should name the archive, the kind and the message");
        Assert.IsNotEmpty(message.Trim(), "The line should carry a message");
        Assert.DoesNotContain(_root, errorLine, "The message should not name the machine's path");
        Assert.Contains(line => line.StartsWith("good.rda | entries 1 | ", StringComparison.Ordinal), report.Lines, "The archive after the failed one should still be listed");
        Assert.AreEqual("Archives: 2", report.Lines[0], "Both archives should be counted");
    }

    /// <summary>
    /// A folder that is a link is not followed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunSkipsLinkedFolder()
    {
        // Arrange
        var outside = Path.Combine(_root, "outside");

        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "hidden.rda"), new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Sized("a", 3))).Build().Bytes);
        Place("real.rda", ("data/b.txt", Sized("b", 3)));
        CreateLinkOrSkip(Path.Combine(MainData, "linked"), outside);

        // Act
        var report = Run();

        // Assert
        Assert.AreEqual("Archives: 1", report.Lines[0], "Only the archive outside the linked folder should be counted");
        Assert.DoesNotContain(line => line.Contains("hidden.rda", StringComparison.Ordinal), report.Lines, "The archive behind the link should not be listed");
    }

    /// <summary>
    /// A folder the process may not read is ignored, not an error.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunIgnoresInaccessibleFolder()
    {
        // Arrange
        MakeLockedFolderOrSkip();
        Place("real.rda", ("data/b.txt", Sized("b", 3)));

        // Act
        var report = Run();

        // Assert
        Assert.AreEqual("Archives: 1", report.Lines[0], "The inaccessible folder should be skipped and the readable archive counted");
    }

    /// <summary>
    /// No line holds entry content.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunReportsNoContent()
    {
        // Arrange
        Place("config.rda", ("data/base/config/game/assets.xml", WithByteOrderMark("<Assets>" + Secret + "</Assets>")));
        Place("ui.rda", ("data/ui/icon_a.dds", Sized("DDS " + Secret, 60)));
        Place("de_de0.rda", ("data/base/config/gui/texts_german.xml", Sized("<g>" + Secret, 60)), ("data/readme.txt", Sized(Secret, 60)));

        // Act
        var report = Run();

        // Assert
        Assert.IsGreaterThan(6, report.Lines.Count, "The report should have lines to check");
        Assert.DoesNotContain(line => line.Contains(Secret, StringComparison.Ordinal), report.Lines, "No line should hold entry content");
        Assert.DoesNotContain(line => line.Contains('<', StringComparison.Ordinal), report.Lines, "No line should hold markup of an entry");
    }

    /// <summary>
    /// Without archives the report says so and nothing is read.
    /// </summary>
    /// <param name="createMainData">Whether the <c>maindata</c> folder exists</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(true)]
    [DataRow(false)]
    public void RdaInstallationSurveyRunWithoutArchivesReportsNone(bool createMainData)
    {
        // Arrange
        Directory.CreateDirectory(createMainData ? MainData : _root);

        // Act
        var report = Run();

        // Assert
        Assert.AreSequenceEqual(["Archives: none under maindata"], report.Lines, "The report should say that there are no archives and say nothing more");
        Assert.IsFalse(report.AssetRead, "Nothing was read");
        Assert.IsFalse(report.IconRead, "Nothing was read");
    }

    /// <summary>
    /// Content that does not start like the expected format is reported as an unknown marker and not as read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRunWrongMarkerIsNotRead()
    {
        // Arrange
        Place("config.rda", ("data/base/config/game/assets.xml", Sized("plain text", 30)));
        Place("ui.rda", ("data/ui/icon_a.dds", Sized("NOT A DDS", 20)));

        // Act
        var report = Run();

        // Assert
        Assert.Contains("Asset read: config.rda | data/base/config/game/assets.xml | 30 | unknown marker", report.Lines, "The asset sample should report an unknown marker");
        Assert.Contains("Icon read: ui.rda | data/ui/icon_a.dds | 20 | unknown marker", report.Lines, "The icon sample should report an unknown marker");
        Assert.IsFalse(report.AssetRead, "An unrecognized asset sample is not a read");
        Assert.IsFalse(report.IconRead, "An unrecognized icon sample is not a read");
    }

    /// <summary>
    /// On a real installation, one asset file and one icon are read; without the variable the test is inconclusive.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaInstallationSurveyRealInstallationReadsAssetAndIcon()
    {
        // Arrange
        var installation = Environment.GetEnvironmentVariable(InstallationVariable);

        if (string.IsNullOrEmpty(installation))
        {
            Assert.Inconclusive($"{InstallationVariable} is not set: the survey did not run on a real installation");
        }

        // Act
        var report = RdaInstallationSurvey.Run(installation, RdaLimits.Default);

        foreach (var line in report.Lines)
        {
            TestContext.WriteLine(line);
        }

        // Assert
        Assert.IsTrue(report.AssetRead, "One asset file should be read and recognized as XML");
        Assert.IsTrue(report.IconRead, "One icon should be read and recognized as DDS");
    }

    #endregion // AC5

    #region Helpers

    /// <summary>
    /// Creates bytes of an exact length that start with a text.
    /// </summary>
    /// <param name="prefix">The start</param>
    /// <param name="length">The length in bytes</param>
    /// <returns>The bytes</returns>
    private static byte[] Sized(string prefix, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(prefix.PadRight(length, 'x'));

        return bytes;
    }

    /// <summary>
    /// Creates XML content that starts with a UTF-8 byte order mark and white space.
    /// </summary>
    /// <param name="xml">The markup</param>
    /// <returns>The bytes</returns>
    private static byte[] WithByteOrderMark(string xml)
    {
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("\r\n  " + xml)).ToArray();
    }

    /// <summary>
    /// Finds the line of an archive.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="archive">The relative path of the archive</param>
    /// <returns>The index of the line</returns>
    private static int IndexOfArchiveLine(RdaSurveyReport report, string archive)
    {
        var index = report.Lines.ToList().FindIndex(line => line.StartsWith(archive + " | entries ", StringComparison.Ordinal));

        Assert.IsGreaterThanOrEqualTo(0, index, "The archive should have a line");

        return index;
    }

    /// <summary>
    /// Gets the lines of a section, trimmed, up to the next section.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="header">The header line of the section</param>
    /// <returns>The lines of the section</returns>
    private static string[] Section(RdaSurveyReport report, string header)
    {
        string[] headers = ["Archives:", "Matching entries:", "Icon folders:", "Language archive entries:", "Asset read:", "Icon read:"];
        var start = report.Lines.ToList().FindIndex(line => line == header);

        Assert.IsGreaterThanOrEqualTo(0, start, $"The report should hold the header {header}");

        return report.Lines.Skip(start + 1)
                           .TakeWhile(line => headers.Any(other => line.StartsWith(other, StringComparison.Ordinal)) == false)
                           .Select(line => line.Trim())
                           .ToArray();
    }

    /// <summary>
    /// Gets a value indicating whether Unix file modes exist.
    /// </summary>
    /// <returns><c>true</c> on Linux and macOS</returns>
    private static bool IsUnix()
    {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    }

    /// <summary>
    /// Sets the Unix mode of a folder.
    /// </summary>
    /// <param name="path">The folder</param>
    /// <param name="mode">The mode</param>
    private static void SetUnixMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    /// <summary>
    /// Creates a link to a folder, or ends the test as inconclusive where links cannot be created.
    /// </summary>
    /// <param name="link">The path of the link</param>
    /// <param name="target">The folder it points to</param>
    private static void CreateLinkOrSkip(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);

        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"A symbolic link cannot be created here ({exception.GetType().Name}): the link test did not run");
        }
    }

    /// <summary>
    /// Runs the survey on the synthetic installation.
    /// </summary>
    /// <returns>The report</returns>
    private RdaSurveyReport Run()
    {
        return RdaInstallationSurvey.Run(_root, RdaLimits.Default);
    }

    /// <summary>
    /// Gets the path of an archive file below <c>maindata</c> and creates its folder.
    /// </summary>
    /// <param name="relativePath">The path relative to <c>maindata</c></param>
    /// <returns>The full path</returns>
    private string PlaceFolder(string relativePath)
    {
        var path = Path.Combine(MainData, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        return path;
    }

    /// <summary>
    /// Writes an archive of one stored block below <c>maindata</c>.
    /// </summary>
    /// <param name="relativePath">The path relative to <c>maindata</c></param>
    /// <param name="files">The paths and contents of the files</param>
    private void Place(string relativePath, params (string Path, byte[] Content)[] files)
    {
        File.WriteAllBytes(PlaceFolder(relativePath), new RdaArchiveBuilder().AddBlock(0, files).Build().Bytes);
    }

    /// <summary>
    /// Creates a folder below <c>maindata</c> that the process cannot read, or ends the test as inconclusive where that
    /// cannot be shown (Windows, or a process that ignores permissions).
    /// </summary>
    private void MakeLockedFolderOrSkip()
    {
        if (IsUnix() == false || Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("A folder cannot be made unreadable for this process: the inaccessible-folder test did not run");
        }

        var locked = Path.Combine(MainData, "locked");

        Directory.CreateDirectory(locked);
        File.WriteAllBytes(Path.Combine(locked, "secret.rda"), new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Sized("a", 3))).Build().Bytes);
        SetUnixMode(locked, UnixFileMode.None);
        _lockedFolder = locked;
    }

    #endregion // Helpers

    #endregion // Methods
}