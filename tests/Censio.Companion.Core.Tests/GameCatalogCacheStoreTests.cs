using Censio.GameData;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogCacheStore" />, with a cache folder in a temporary folder.
/// </summary>
[TestClass]
public sealed class GameCatalogCacheStoreTests
{
    #region Constants

    /// <summary>
    /// A temporary file name that the store deletes when it is old.
    /// </summary>
    private const string TemporaryName = "catalog-0123456789abcdef0123456789abcdef.tmp";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The temporary folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-store-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The clock of the store.
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the cache folder.
    /// </summary>
    private string Folder => Path.Combine(_root, "cache");

    #endregion // Properties

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
    /// A folder without a cache file, or no folder, gives a missing cache.
    /// </summary>
    /// <param name="createFolder">Whether the folder exists</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(false)]
    [DataRow(true)]
    public void GameCatalogCacheStoreLoadMissingReturnsMissing(bool createFolder)
    {
        // Arrange
        if (createFolder)
        {
            Directory.CreateDirectory(Folder);
        }

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        var result = store.Load(CatalogLoaderRig.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Missing, result.Kind, "Without a cache file the cache should be missing");
    }

    /// <summary>
    /// A folder that is no full path is never read: a load gives a missing cache.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreLoadNotFullPathReturnsMissing()
    {
        // Arrange
        var store = new GameCatalogCacheStore(Path.Combine("relative", "cache"), _clock);

        // Act
        var result = store.Load(CatalogLoaderRig.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Missing, result.Kind, "A folder that is no full path should give a missing cache");
    }

    /// <summary>
    /// A saved cache is loaded again with the same catalog and icons.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogCacheStoreSaveThenLoadRoundTrips()
    {
        // Arrange
        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        var saved = store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());
        var result = store.Load(CatalogLoaderRig.Key());

        // Assert
        Assert.IsTrue(saved.IsWritten, "The cache should be written");
        Assert.IsNull(saved.Failure, "A written cache should have no failure");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "The saved cache should be loaded");
        Assert.AreEqual(2, result.Catalog!.Count, "The catalog should be loaded");
        Assert.AreEqual(1, result.Icons!.GoodsWithIcon, "The icons should be loaded");
    }

    /// <summary>
    /// A cache of another key is reported as such by the store.
    /// </summary>
    /// <param name="row">The row</param>
    /// <param name="expected">The expected kind</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("game", GameCatalogCacheResultKind.GameChanged)]
    [DataRow("companion", GameCatalogCacheResultKind.CompanionChanged)]
    public void GameCatalogCacheStoreLoadOtherKeyReturnsKind(string row, GameCatalogCacheResultKind expected)
    {
        // Arrange
        var store = new GameCatalogCacheStore(Folder, _clock);
        var other = row == "game" ? CatalogLoaderRig.Key("2") : CatalogLoaderRig.Key("1", "idB");

        store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Act
        var result = store.Load(other);

        // Assert
        Assert.AreEqual(expected, result.Kind, "A cache of another key should be reported by the kind of the difference");
    }

    /// <summary>
    /// A file that is no cache is invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreLoadDamagedFileReturnsInvalid()
    {
        // Arrange
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(Path.Combine(Folder, GameCatalogCacheStore.FileName), [1, 2, 3]);

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        var result = store.Load(CatalogLoaderRig.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Invalid, result.Kind, "A damaged file should be invalid");
    }

    /// <summary>
    /// A file above 320 MiB is refused as invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreLoadFileAboveMaximumReturnsInvalid()
    {
        // Arrange
        Directory.CreateDirectory(Folder);

        using (var stream = new FileStream(Path.Combine(Folder, GameCatalogCacheStore.FileName), FileMode.Create))
        {
            stream.SetLength(GameCatalogCacheFile.MaxFileBytes + 1);
        }

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        var result = store.Load(CatalogLoaderRig.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Invalid, result.Kind, "A file above the maximum should be invalid");
    }

    /// <summary>
    /// A save into a folder that is a file fails with the exception and does not throw.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogCacheStoreSaveFolderIsFileReturnsFailure()
    {
        // Arrange
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Folder, [1]);

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        var result = store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.IsFalse(result.IsWritten, "The cache should not be written");
        Assert.IsNotNull(result.Failure, "The failure should carry the exception");
    }

    /// <summary>
    /// A folder that is no full path is never written to: the save fails and creates nothing.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void GameCatalogCacheStoreSaveNotFullPathReturnsFailure()
    {
        // Arrange
        var relative = Path.Combine("censio-relative-" + Guid.NewGuid().ToString("N"), "cache");
        var store = new GameCatalogCacheStore(relative, _clock);

        // Act
        var result = store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.IsFalse(result.IsWritten, "The cache should not be written");
        Assert.IsFalse(Directory.Exists(relative), "No folder should be created relative to the current directory");
    }

    /// <summary>
    /// A save leaves no temporary file in the folder.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreSaveLeavesNoTemporaryFile()
    {
        // Arrange
        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.AreSequenceEqual([GameCatalogCacheStore.FileName], Directory.GetFileSystemEntries(Folder).Select(Path.GetFileName).ToList(), "Only the cache file should remain");
    }

    /// <summary>
    /// Two saves leave one complete file, the second one's.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreSaveTwiceKeepsOneCompleteFile()
    {
        // Arrange
        var store = new GameCatalogCacheStore(Folder, _clock);

        store.Save(CatalogLoaderRig.Key("1"), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Act
        var second = store.Save(CatalogLoaderRig.Key("2"), CatalogLoaderRig.Catalog(3), CatalogLoaderRig.Icons());

        // Assert
        var result = store.Load(CatalogLoaderRig.Key("2"));

        Assert.IsTrue(second.IsWritten, "The second save should be written");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "The second cache should be loaded");
        Assert.AreEqual(3, result.Catalog!.Count, "The second catalog should be loaded");
        Assert.AreEqual(GameCatalogCacheResultKind.GameChanged, store.Load(CatalogLoaderRig.Key("1")).Kind, "The first cache should be gone");
        Assert.HasCount(1, Directory.GetFileSystemEntries(Folder), "Only one file should remain");
    }

    /// <summary>
    /// A save deletes top-level files that are exactly a temporary name and older than 24 hours, and no other file.
    /// </summary>
    /// <param name="name">The name of the file</param>
    /// <param name="ageHours">The age of the file in hours</param>
    /// <param name="isDeleted">Whether the file is deleted</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(TemporaryName, 25, true, DisplayName = "Old temporary file")]
    [DataRow(TemporaryName, 23, false, DisplayName = "Fresh file of a concurrent writer")]
    [DataRow("catalog-0123456789abcdef0123456789abcdef.tmpx", 100, false, DisplayName = "Longer extension")]
    [DataRow("catalog-0123456789ABCDEF0123456789ABCDEF.tmp", 100, false, DisplayName = "Uppercase hex")]
    [DataRow("catalog-0123456789abcdef0123456789abcde.tmp", 100, false, DisplayName = "31 hex digits")]
    [DataRow("catalog-0123456789abcdef0123456789abcdef0.tmp", 100, false, DisplayName = "33 hex digits")]
    [DataRow("catalog-0123456789abcdef0123456789abcdeg.tmp", 100, false, DisplayName = "Not hex")]
    [DataRow("Catalog-0123456789abcdef0123456789abcdef.tmp", 100, false, DisplayName = "Other case of the prefix")]
    [DataRow("catalog-0123456789abcdef0123456789abcdef.TMP", 100, false, DisplayName = "Other case of the extension")]
    [DataRow("xcatalog-0123456789abcdef0123456789abcdef.tmp", 100, false, DisplayName = "Text before")]
    [DataRow("catalog-0123456789abcdef0123456789abcdef.tmp.old", 100, false, DisplayName = "Text after")]
    [DataRow("other.tmp", 100, false, DisplayName = "Other temporary file")]
    [DataRow("notes.txt", 100, false, DisplayName = "Other file")]
    public void GameCatalogCacheStoreSavePrunesOnlyOldTemporaryFiles(string name, int ageHours, bool isDeleted)
    {
        // Arrange
        Directory.CreateDirectory(Folder);

        var path = Path.Combine(Folder, name);

        File.WriteAllBytes(path, [1]);
        File.SetLastWriteTimeUtc(path, _clock.GetUtcNow().UtcDateTime.AddHours(-ageHours));

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        var result = store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.IsTrue(result.IsWritten, "The cache should be written");
        Assert.AreEqual(isDeleted == false, File.Exists(path), isDeleted ? "The old temporary file should be deleted" : "The file should be kept");
    }

    /// <summary>
    /// A folder named like an old temporary file is kept, with its content.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreSaveKeepsFolderOfTemporaryName()
    {
        // Arrange
        var folder = Path.Combine(Folder, TemporaryName);

        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "inner.bin"), [1]);
        Directory.SetLastWriteTimeUtc(folder, _clock.GetUtcNow().UtcDateTime.AddHours(-100));

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.IsTrue(File.Exists(Path.Combine(folder, "inner.bin")), "A folder of a temporary name should be kept with its content");
    }

    /// <summary>
    /// A file of a temporary name in a subfolder is kept.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreSaveKeepsTemporaryFileInSubfolder()
    {
        // Arrange
        var inner = Path.Combine(Folder, "sub", TemporaryName);

        Directory.CreateDirectory(Path.GetDirectoryName(inner)!);
        File.WriteAllBytes(inner, [1]);
        File.SetLastWriteTimeUtc(inner, _clock.GetUtcNow().UtcDateTime.AddHours(-100));

        var store = new GameCatalogCacheStore(Folder, _clock);

        // Act
        store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.IsTrue(File.Exists(inner), "Only top-level files should be pruned");
    }

    /// <summary>
    /// A symbolic link of a temporary name that is more than 24 hours old is deleted as a link and the file it points to stays.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheStoreSavePrunesLinkAsLink()
    {
        // Arrange
        Directory.CreateDirectory(Folder);

        var target = Path.Combine(_root, "target.bin");
        var link = Path.Combine(Folder, TemporaryName);

        File.WriteAllBytes(target, [1]);

        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive("The run cannot create a symbolic link: " + exception.Message);
        }

        var clock = new FakeTimeProvider(new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new GameCatalogCacheStore(Folder, clock);

        // Act
        store.Save(CatalogLoaderRig.Key(), CatalogLoaderRig.Catalog(2), CatalogLoaderRig.Icons());

        // Assert
        Assert.IsNull(new FileInfo(link).LinkTarget, "The link should be deleted");
        Assert.IsTrue(File.Exists(target), "The file the link pointed to should stay");
    }

    #endregion // Methods
}