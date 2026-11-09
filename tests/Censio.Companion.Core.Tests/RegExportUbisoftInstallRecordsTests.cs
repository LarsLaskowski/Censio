namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="RegExportUbisoftInstallRecords" />.
/// </summary>
[TestClass]
public sealed class RegExportUbisoftInstallRecordsTests
{
    #region Methods

    /// <summary>
    /// The Product Manager's export gives exactly its two records with their install folders verbatim.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RegExportUbisoftInstallRecordsReadsSample()
    {
        // Arrange
        var records = new RegExportUbisoftInstallRecords(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GameInstallation", "ubisoft-connect-installs.reg"));

        // Act
        var result = records.Read();

        // Assert
        Assert.AreSequenceEqual(new[]
                                {
                                    new UbisoftInstallRecord
                                    {
                                        Name = "4553",
                                        InstallDir = "E:/Anno 1800/"
                                    },
                                    new UbisoftInstallRecord
                                    {
                                        Name = "921",
                                        InstallDir = "E:/Anno 117 - Pax Romana/"
                                    }
                                },
                                result.ToArray(),
                                "The sample should give its two records in file order with the install folders as written");
    }

    #endregion // Methods
}