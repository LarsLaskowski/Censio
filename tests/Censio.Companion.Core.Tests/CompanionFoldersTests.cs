namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionFolders" />.
/// </summary>
[TestClass]
public sealed class CompanionFoldersTests
{
    #region Methods

    /// <summary>
    /// The log folder is Censio\Logs below the local application data.
    /// </summary>
    [TestMethod]
    public void CompanionFoldersLogsIsUnderCensio()
    {
        // Arrange
        var data = Path.Combine(Path.GetTempPath(), "Local");

        // Act
        var folder = CompanionFolders.Logs(data);

        // Assert
        Assert.AreEqual(Path.Combine(data, "Censio", "Logs"), folder, "The log folder should be Censio\\Logs below the local application data");
    }

    /// <summary>
    /// The cache folder is Censio\Cache below the local application data.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionFoldersCacheIsUnderCensio()
    {
        // Arrange
        var data = Path.Combine(Path.GetTempPath(), "Local");

        // Act
        var folder = CompanionFolders.Cache(data);

        // Assert
        Assert.AreEqual(Path.Combine(data, "Censio", "Cache"), folder, "The cache folder should be Censio\\Cache below the local application data");
    }

    /// <summary>
    /// The settings file is Censio\settings.json below the local application data.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CompanionFoldersSettingsIsUnderCensio()
    {
        // Arrange
        var data = Path.Combine(Path.GetTempPath(), "Local");

        // Act
        var file = CompanionFolders.Settings(data);

        // Assert
        Assert.AreEqual(Path.Combine(data, "Censio", "settings.json"), file, "The settings file should be Censio\\settings.json below the local application data");
    }

    #endregion // Methods
}