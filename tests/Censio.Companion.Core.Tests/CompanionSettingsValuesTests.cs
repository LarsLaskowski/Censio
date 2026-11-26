namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionSettingsValues" />.
/// </summary>
[TestClass]
public sealed class CompanionSettingsValuesTests
{
    #region Constants

    /// <summary>
    /// A valid token of 39 visible ASCII characters.
    /// </summary>
    private const string ServerToken = "ForwardToken-0123456789abcdefghijKLMNOP";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The text of the values is the name of their type alone and never holds the token (N5).
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void CompanionSettingsValuesToStringHoldsNoToken()
    {
        // Arrange
        var values = new CompanionSettingsValues
                     {
                         Mode = CompanionSettingsMode.Forward,
                         LocalPort = "5000",
                         ServerAddress = "ws://nas:8080/ingest",
                         ServerToken = ServerToken,
                         BufferHours = "24",
                         BufferMegabytes = "100",
                         GameFolder = string.Empty
                     };

        // Act
        var text = values.ToString() ?? string.Empty;

        // Assert
        Assert.AreEqual(typeof(CompanionSettingsValues).FullName, text, "The text of the values should be the name of their type alone");
        Assert.DoesNotContain(ServerToken, text, "The text of the values should never hold the token");
    }

    #endregion // Methods
}