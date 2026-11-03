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

    #endregion // Methods
}