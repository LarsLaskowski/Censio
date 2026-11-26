namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionProfile" />.
/// </summary>
[TestClass]
public sealed class CompanionProfileTests
{
    #region Constants

    /// <summary>
    /// The name of the profile, a marker no text of it may hold.
    /// </summary>
    private const string ProfileName = "MARKER-Profile";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The text of a profile is the name of its type alone and never holds the profile's name (N5).
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void CompanionProfileToStringHoldsNoName()
    {
        // Arrange
        var profile = new CompanionProfile
                      {
                          Id = new Guid("0f8c2d3e-1111-4a2b-8c3d-00000000a002"),
                          Name = ProfileName
                      };

        // Act
        var text = profile.ToString() ?? string.Empty;

        // Assert
        Assert.AreEqual(typeof(CompanionProfile).FullName, text, "The text of the profile should be the name of its type alone");
        Assert.DoesNotContain(ProfileName, text, "The text of the profile should never hold its name");
    }

    #endregion // Methods
}