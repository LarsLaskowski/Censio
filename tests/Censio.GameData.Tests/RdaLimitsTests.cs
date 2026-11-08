namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="RdaLimits" />.
/// </summary>
[TestClass]
public sealed class RdaLimitsTests
{
    #region Methods

    /// <summary>
    /// A value of zero or less in any position is refused.
    /// </summary>
    /// <param name="maxBlocks">The most blocks</param>
    /// <param name="maxEntries">The most entries</param>
    /// <param name="maxDirectoryBytes">The most directory bytes</param>
    /// <param name="maxEntryBytes">The most entry bytes</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0, 1, 1, 1)]
    [DataRow(-1, 1, 1, 1)]
    [DataRow(1, 0, 1, 1)]
    [DataRow(1, -1, 1, 1)]
    [DataRow(1, 1, 0, 1)]
    [DataRow(1, 1, -1, 1)]
    [DataRow(1, 1, 1, 0)]
    [DataRow(1, 1, 1, -1)]
    public void RdaLimitsConstructorNonPositiveValueThrows(int maxBlocks, int maxEntries, int maxDirectoryBytes, int maxEntryBytes)
    {
        // Arrange & Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RdaLimits(maxBlocks, maxEntries, maxDirectoryBytes, maxEntryBytes), "A limit of zero or less should be refused");
    }

    #endregion // Methods
}