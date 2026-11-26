namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="ProfileNotice" />.
/// </summary>
[TestClass]
public sealed class ProfileNoticeTests
{
    #region Constants

    /// <summary>
    /// The name of the profile of the notice, a marker no text of it may hold.
    /// </summary>
    private const string ProfileName = "MARKER-Profile";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The text of a notice is the name of its type alone and never holds the profile's name (N5).
    /// </summary>
    /// <param name="kind">The kind of the notice</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(ProfileNoticeKind.LastUsedWithOther)]
    [DataRow(ProfileNoticeKind.NewFigure)]
    public void ProfileNoticeToStringHoldsNoName(ProfileNoticeKind kind)
    {
        // Arrange
        var notice = new ProfileNotice
                     {
                         Kind = kind,
                         ProfileName = ProfileName
                     };

        // Act
        var text = notice.ToString() ?? string.Empty;

        // Assert
        Assert.AreEqual(typeof(ProfileNotice).FullName, text, "The text of the notice should be the name of its type alone");
        Assert.DoesNotContain(ProfileName, text, "The text of the notice should never hold the profile's name");
    }

    #endregion // Methods
}