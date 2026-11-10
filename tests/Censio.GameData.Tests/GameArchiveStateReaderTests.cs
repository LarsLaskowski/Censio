using System.Runtime.Versioning;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameArchiveStateReader" />, with installation folders built in a temporary folder.
/// </summary>
[TestClass]
public sealed class GameArchiveStateReaderTests
{
    #region Fields

    /// <summary>
    /// The temporary installation folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-archives-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the <c>maindata</c> folder of the temporary installation.
    /// </summary>
    private string MainData => Path.Combine(_root, "maindata");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Restores the permissions of the folders and deletes the temporary installation.
    /// </summary>
    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root) == false)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.Delete(_root, true);

            return;
        }

        RestorePermissions();
        Directory.Delete(_root, true);
    }

    /// <summary>
    /// The archives below <c>maindata</c>, also in a subfolder, are listed with their length and last write time in UTC, and
    /// files that are not archives are left out.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadListsSizeAndTime()
    {
        // Arrange
        var time = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        WriteFile("ui.rda", 5, time);
        WriteFile(Path.Combine("sub", "patch.rda"), 9, time.AddDays(1));
        WriteFile("notes.txt", 3, time);

        // Act
        var archives = GameArchiveStateReader.Read(_root);

        // Assert
        var paths = archives.Select(archive => archive.Path.Replace('\\', '/')).Order(StringComparer.Ordinal).ToList();
        var ui = archives.Single(archive => archive.Path == "ui.rda");
        var patch = archives.Single(archive => archive.Path.Replace('\\', '/') == "sub/patch.rda");

        List<string> expectedPaths = ["sub/patch.rda", "ui.rda"];

        Assert.AreSequenceEqual(expectedPaths, paths, "Only the archives, relative to maindata, should be listed");
        Assert.AreEqual(5L, ui.Length, "The length of the archive should be listed");
        Assert.AreEqual(time, ui.LastWriteTimeUtc, "The last write time should be listed");
        Assert.AreEqual(DateTimeKind.Utc, ui.LastWriteTimeUtc.Kind, "The time should be UTC");
        Assert.AreEqual(9L, patch.Length, "The length of an archive in a subfolder should be listed");
        Assert.AreEqual(time.AddDays(1), patch.LastWriteTimeUtc, "The time of an archive in a subfolder should be listed");
    }

    /// <summary>
    /// The order does not depend on the order the files were created in.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadOrderIsStable()
    {
        // Arrange
        var time = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        WriteFile("zz.rda", 1, time);
        WriteFile("aa.rda", 1, time);
        WriteFile(Path.Combine("mm", "bb.rda"), 1, time);

        var first = GameArchiveStateReader.Read(_root).Select(archive => archive.Path).ToList();

        // Act
        var second = GameArchiveStateReader.Read(_root).Select(archive => archive.Path).ToList();

        // Assert
        Assert.AreSequenceEqual(first, second, "Two listings of the same folder should be in the same order");
        Assert.AreSequenceEqual(first.Order(StringComparer.OrdinalIgnoreCase).ToList(), first, "The order should be by path");
    }

    /// <summary>
    /// A <c>maindata</c> folder without archives is an empty list, not an error.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadEmptyMainDataReturnsEmpty()
    {
        // Arrange
        Directory.CreateDirectory(MainData);

        // Act
        var archives = GameArchiveStateReader.Read(_root);

        // Assert
        Assert.IsEmpty(archives, "A maindata folder without archives should give an empty list");
    }

    /// <summary>
    /// A link to a folder below <c>maindata</c> is not followed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadSkipsLinkedFolder()
    {
        // Arrange
        var time = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var outside = Path.Combine(_root, "outside");

        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "linked.rda"), [1, 2, 3]);
        WriteFile("real.rda", 1, time);

        try
        {
            Directory.CreateSymbolicLink(Path.Combine(MainData, "link"), outside);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive("The run cannot create a symbolic link: " + exception.Message);
        }

        // Act
        var archives = GameArchiveStateReader.Read(_root);

        // Assert
        List<string> expectedPaths = ["real.rda"];

        Assert.AreSequenceEqual(expectedPaths, archives.Select(archive => archive.Path).ToList(), "A linked folder should not be followed");
    }

    /// <summary>
    /// A path that is a file and no installation folder cannot be listed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadMissingFileThrows()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        var notAFolder = Path.Combine(_root, "program.exe");

        File.WriteAllBytes(notAFolder, [1]);

        // Act and Assert
        Assert.Throws<IOException>(() => GameArchiveStateReader.Read(notAFolder), "A file in place of the installation folder should throw");
    }

    /// <summary>
    /// An installation folder without <c>maindata</c> cannot be listed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadMissingMainDataThrows()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        // Act and Assert
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => GameArchiveStateReader.Read(_root), "A missing maindata folder should throw");
    }

    /// <summary>
    /// An installation folder that does not exist cannot be listed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameArchiveStateReaderReadMissingFolderThrows()
    {
        // Arrange
        var missing = Path.Combine(_root, "missing");

        // Act and Assert
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => GameArchiveStateReader.Read(missing), "A missing installation folder should throw");
    }

    /// <summary>
    /// A subfolder that cannot be read makes the listing fail instead of leaving its archives out.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void GameArchiveStateReaderReadInaccessibleFolderThrows()
    {
        // Arrange
        var time = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var locked = Path.Combine(MainData, "locked");

        WriteFile(Path.Combine("locked", "hidden.rda"), 1, time);
        File.SetUnixFileMode(locked, UnixFileMode.None);

        if (CanList(locked))
        {
            Assert.Inconclusive("The run can read a folder without permission, as root does");
        }

        // Act and Assert
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => GameArchiveStateReader.Read(_root), "An inaccessible folder should throw");
    }

    /// <summary>
    /// Checks whether a folder can be listed.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns><c>true</c> when it can</returns>
    private static bool CanList(string folder)
    {
        try
        {
            _ = Directory.GetFileSystemEntries(folder);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gives the owner access to every folder again.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private void RestorePermissions()
    {
        foreach (var folder in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// Writes a file below <c>maindata</c>.
    /// </summary>
    /// <param name="relative">The path below <c>maindata</c></param>
    /// <param name="length">The length</param>
    /// <param name="time">The last write time</param>
    private void WriteFile(string relative, int length, DateTime time)
    {
        var path = Path.Combine(MainData, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, time);
    }

    #endregion // Methods
}