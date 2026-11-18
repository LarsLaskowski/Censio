namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeProtocol" />.
/// </summary>
[TestClass]
public sealed class PipeProtocolTests
{
    #region Methods

    /// <summary>
    /// Protocol versions 2 and 3 are supported.
    /// </summary>
    /// <param name="version">The version</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(2)]
    [DataRow(3)]
    public void PipeProtocolIsSupportedReturnsTrueForSupportedVersions(int version)
    {
        // Act
        var supported = PipeProtocol.IsSupported(version);

        // Assert
        Assert.IsTrue(supported, $"Version {version} should be supported");
    }

    /// <summary>
    /// Every version other than 2 and 3 is not supported, the extremes of the int32 range included.
    /// </summary>
    /// <param name="version">The version</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void PipeProtocolIsSupportedReturnsFalseForOtherVersions(int version)
    {
        // Act
        var supported = PipeProtocol.IsSupported(version);

        // Assert
        Assert.IsFalse(supported, $"Version {version} should not be supported");
    }

    #endregion // Methods
}