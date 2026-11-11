using Censio.GameData;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogKeyReader" />, with installations built in a temporary folder.
/// </summary>
[TestClass]
public sealed class GameCatalogKeyReaderTests
{
    #region Fields

    /// <summary>
    /// The temporary folder that holds the installation.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-keyreader-" + Guid.NewGuid().ToString("N"));

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
    /// The key of a found installation holds the identity, the folder as found, the build of the status and the archives of
    /// <c>maindata</c> with their size and time.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyReaderReadFoundInstallationReturnsKey()
    {
        // Arrange
        var time = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        CreateInstallation(true);
        WriteArchive("config.rda", 3, time);
        WriteArchive(Path.Combine("sub", "patch.rda"), 8, time.AddDays(1));

        // Act
        var key = GameCatalogKeyReader.Read(Found("26.1.2.3"), "identity-1");

        // Assert
        Assert.IsNotNull(key, "A readable installation should give a key");
        Assert.AreEqual("identity-1", key.Identity, "The key should hold the identity");
        Assert.AreEqual(_root, key.Folder, "The key should hold the folder");
        Assert.AreEqual("26.1.2.3", key.Build, "The key should hold the build of the status");

        var config = key.Archives.Single(archive => archive.Path == "config.rda");

        Assert.HasCount(2, key.Archives, "The key should hold every archive");
        Assert.AreEqual(3L, config.Length, "The key should hold the size");
        Assert.AreEqual(time, config.LastWriteTimeUtc, "The key should hold the time");
    }

    /// <summary>
    /// A listing of <c>maindata</c> that cannot be read gives no key.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyReaderReadUnreadableListingReturnsNull()
    {
        // Arrange
        CreateInstallation(true);
        Directory.Delete(Path.Combine(_root, "maindata"), true);

        // Act
        var key = GameCatalogKeyReader.Read(Found("1"), "i");

        // Assert
        Assert.IsNull(key, "A missing maindata folder should give no key");
    }

    /// <summary>
    /// A program file that does not exist gives no key.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyReaderReadMissingProgramFileReturnsNull()
    {
        // Arrange
        CreateInstallation(false);
        WriteArchive("config.rda", 3, DateTime.UtcNow);

        // Act
        var key = GameCatalogKeyReader.Read(Found("1"), "i");

        // Assert
        Assert.IsNull(key, "A missing program file should give no key");
    }

    /// <summary>
    /// A program file without a version gives a key with an unknown build: the installation is readable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyReaderReadNoVersionGivesUnknown()
    {
        // Arrange
        CreateInstallation(true);
        WriteArchive("config.rda", 3, DateTime.UtcNow);

        // Act
        var key = GameCatalogKeyReader.Read(Found(null), "i");

        // Assert
        Assert.IsNotNull(key, "An unknown build should still give a key");
        Assert.IsNull(key.Build, "The build should be unknown");
    }

    /// <summary>
    /// A status without a found installation gives no key.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyReaderReadNotFoundStatusReturnsNull()
    {
        // Arrange
        var status = new GameInstallationStatus
                     {
                         Kind = GameInstallationStatusKind.NotFound
                     };

        // Act
        var key = GameCatalogKeyReader.Read(status, "i");

        // Assert
        Assert.IsNull(key, "A status without an installation should give no key");
    }

    /// <summary>
    /// Creates the installation folder, with or without the program file.
    /// </summary>
    /// <param name="withProgram">Whether the program file exists</param>
    private void CreateInstallation(bool withProgram)
    {
        Directory.CreateDirectory(Path.Combine(_root, "maindata"));

        if (withProgram)
        {
            var program = GameInstallation.ProgramFilePath(_root);

            Directory.CreateDirectory(Path.GetDirectoryName(program)!);
            File.WriteAllBytes(program, [1]);
        }
    }

    /// <summary>
    /// Writes an archive below <c>maindata</c>.
    /// </summary>
    /// <param name="relative">The path below <c>maindata</c></param>
    /// <param name="length">The length</param>
    /// <param name="time">The last write time</param>
    private void WriteArchive(string relative, int length, DateTime time)
    {
        var path = Path.Combine(_root, "maindata", relative);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, time);
    }

    /// <summary>
    /// Creates the status of the installation.
    /// </summary>
    /// <param name="build">The build</param>
    /// <returns>The status</returns>
    private GameInstallationStatus Found(string? build)
    {
        return new GameInstallationStatus
               {
                   Kind = GameInstallationStatusKind.Found,
                   Source = GameInstallationSource.UbisoftConnect,
                   Folder = _root,
                   Build = build
               };
    }

    #endregion // Methods
}