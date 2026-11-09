namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameInstallation" />.
/// </summary>
[TestClass]
public sealed class GameInstallationTests
{
    #region Fields

    /// <summary>
    /// The temporary folder of the test, which holds the installation.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-installation-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// A folder is an installation only when it holds both <c>maindata/config.rda</c> and
    /// <c>bin/win64/anno117.exe</c>; a trailing separator does not matter.
    /// </summary>
    /// <param name="hasConfig">Whether the folder holds the configuration archive</param>
    /// <param name="hasProgram">Whether the folder holds the program file</param>
    /// <param name="suffix">The text appended to the folder path</param>
    /// <param name="expected">Whether the folder is an installation</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(true, true, "", true, DisplayName = "Both files")]
    [DataRow(true, true, "/", true, DisplayName = "Both files, trailing separator")]
    [DataRow(true, false, "", false, DisplayName = "Only the configuration archive")]
    [DataRow(false, true, "", false, DisplayName = "Only the program file")]
    [DataRow(false, false, "", false, DisplayName = "None")]
    public void GameInstallationIsInstallationNeedsConfigAndProgramFile(bool hasConfig, bool hasProgram, string suffix, bool expected)
    {
        // Arrange
        var folder = Path.Combine(_root, "Anno 117");

        Directory.CreateDirectory(folder);
        CreateFileIf(hasConfig, folder, "maindata", "config.rda");
        CreateFileIf(hasProgram, folder, "bin", "win64", "anno117.exe");

        // Act
        var result = GameInstallation.IsInstallation(folder + suffix);

        // Assert
        Assert.AreEqual(expected, result, "A folder should be an installation exactly when it holds both files");
    }

    /// <summary>
    /// The two files of an installation must be files, not folders of that name.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameInstallationIsInstallationFoldersNamedLikeFilesAreNoInstallation()
    {
        // Arrange
        var folder = Path.Combine(_root, "Anno 117");

        Directory.CreateDirectory(Path.Combine(folder, "maindata", "config.rda"));
        Directory.CreateDirectory(Path.Combine(folder, "bin", "win64", "anno117.exe"));

        // Act
        var result = GameInstallation.IsInstallation(folder);

        // Assert
        Assert.IsFalse(result, "Folders named like the two files should not make an installation");
    }

    /// <summary>
    /// The program file lies in <c>bin/win64</c> below the installation.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameInstallationProgramFilePathIsBelowBinWin64()
    {
        // Arrange
        var folder = Path.Combine(_root, "Anno 117");

        // Act
        var path = GameInstallation.ProgramFilePath(folder);

        // Assert
        Assert.AreEqual(Path.Combine(folder, "bin", "win64", "anno117.exe"), path, "The program file should be bin/win64/anno117.exe below the installation");
    }

    /// <summary>
    /// Ubisoft Connect's default games folder lies below the 32-bit program files folder.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameInstallationDefaultUbisoftGamesFolderIsUnderProgramFiles()
    {
        // Arrange
        var programFiles = Path.Combine(_root, "Program Files (x86)");

        // Act
        var folder = GameInstallation.DefaultUbisoftGamesFolder(programFiles);

        // Assert
        Assert.AreEqual(Path.Combine(programFiles, "Ubisoft", "Ubisoft Game Launcher", "games"), folder, "The default games folder should be Ubisoft\\Ubisoft Game Launcher\\games below the program files folder");
    }

    /// <summary>
    /// A path whose first character is a separator and whose second is a separator or <c>?</c> is a network or device
    /// path, with <c>\</c> and <c>/</c> both counted as separators.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="expected">Whether the path is a network or device path</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(@"\\srv\share\", true)]
    [DataRow("//srv/share/", true)]
    [DataRow(@"\/srv/share/", true)]
    [DataRow(@"/\srv\share\", true)]
    [DataRow(@"\??\UNC\srv\share\", true)]
    [DataRow(@"\\?\C:\x", true)]
    [DataRow(@"\\.\pipe\x", true)]
    [DataRow("/?/x", true)]
    [DataRow(@"C:\x", false)]
    [DataRow("E:/Anno 117 - Pax Romana/", false)]
    [DataRow("/tmp/x", false)]
    [DataRow(@"\x", false)]
    [DataRow("", false)]
    public void GameInstallationIsNetworkOrDevicePathDetectsSeparatorPairs(string path, bool expected)
    {
        // Act
        var result = GameInstallation.IsNetworkOrDevicePath(path);

        // Assert
        Assert.AreEqual(expected, result, "A separator followed by a separator or a question mark should be a network or device path");
    }

    /// <summary>
    /// The build is the four numbers of the version, invariant; no version or all zero is unknown.
    /// </summary>
    /// <param name="version">The version text, or <see langword="null" /> for none</param>
    /// <param name="expected">The expected build, or <see langword="null" /> for unknown</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("26.29905.4.51797", "26.29905.4.51797", DisplayName = "Sample version")]
    [DataRow("0.0.0.1", "0.0.0.1", DisplayName = "Only the last number set")]
    [DataRow("1000.0.0.0", "1000.0.0.0", DisplayName = "No group separator")]
    [DataRow("0.0.0.0", null, DisplayName = "All zero")]
    [DataRow(null, null, DisplayName = "No version")]
    public void GameInstallationFormatBuildWritesFourNumbers(string? version, string? expected)
    {
        // Arrange
        var parsed = ParseVersion(version);

        // Act
        var build = GameInstallation.FormatBuild(parsed);

        // Assert
        Assert.AreEqual(expected, build, "The build should be the four numbers of the version, or unknown");
    }

    /// <summary>
    /// Parses a version text.
    /// </summary>
    /// <param name="version">The version text, or <see langword="null" /> for none</param>
    /// <returns>The version, or <see langword="null" /> for none</returns>
    private static Version? ParseVersion(string? version)
    {
        return version is null ? null : Version.Parse(version);
    }

    /// <summary>
    /// Creates an empty file below a folder when asked to.
    /// </summary>
    /// <param name="create">Whether to create the file</param>
    /// <param name="folder">The folder</param>
    /// <param name="segments">The path segments of the file below the folder</param>
    private static void CreateFileIf(bool create, string folder, params string[] segments)
    {
        if (create)
        {
            var path = Path.Combine([folder, .. segments]);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [1]);
        }
    }

    #endregion // Methods
}