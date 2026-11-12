namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestAccess" />.
/// </summary>
[TestClass]
public sealed class IngestAccessTests
{
    #region Constants

    /// <summary>
    /// The token the access of the token cases is created for.
    /// </summary>
    private const string Token = "secret-token";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The access of one token accepts exactly that token and requires a token.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void IngestAccessIsTokenAcceptedMatchingTokenReturnsTrue()
    {
        // Arrange
        var access = IngestAccess.CreateForToken(Token);

        // Act
        var accepted = access.IsTokenAccepted(Token);

        // Assert
        Assert.IsTrue(accepted, "The stored token should be accepted");
        Assert.IsTrue(access.IsTokenRequired, "The access of a token should require a token");
    }

    /// <summary>
    /// The access of one token refuses a token of another length, an empty token and none.
    /// </summary>
    /// <param name="presented">The presented token</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("secret-toke")]
    [DataRow("secret-token1")]
    [DataRow("secret-token ")]
    [DataRow("")]
    [DataRow(null)]
    public void IngestAccessIsTokenAcceptedOtherLengthReturnsFalse(string? presented)
    {
        // Arrange
        var access = IngestAccess.CreateForToken(Token);

        // Act
        var accepted = access.IsTokenAccepted(presented);

        // Assert
        Assert.IsFalse(accepted, $"A token of another length should be refused: '{presented}'");
    }

    /// <summary>
    /// The access that refuses all (no data directory) refuses every token, none included, and requires a token.
    /// </summary>
    /// <param name="presented">The presented token</param>
    [TestMethod]
    [TestCategory("AC3")]
    [TestCategory("AC8")]
    [DataRow(Token)]
    [DataRow("")]
    [DataRow(null)]
    public void IngestAccessIsTokenAcceptedRefusingAllRefusesEveryToken(string? presented)
    {
        // Arrange
        var access = IngestAccess.CreateRefusingAll();

        // Act
        var accepted = access.IsTokenAccepted(presented);

        // Assert
        Assert.IsFalse(accepted, $"The access that refuses all should refuse the token: '{presented}'");
        Assert.IsTrue(access.IsTokenRequired, "The access that refuses all should require a token");
    }

    /// <summary>
    /// The open access (token switched off) accepts every token, none included, and requires none.
    /// </summary>
    /// <param name="presented">The presented token</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(Token)]
    [DataRow("")]
    [DataRow(null)]
    public void IngestAccessIsTokenAcceptedOpenAcceptsAnyToken(string? presented)
    {
        // Arrange
        var access = IngestAccess.CreateOpen();

        // Act
        var accepted = access.IsTokenAccepted(presented);

        // Assert
        Assert.IsTrue(accepted, $"The open access should accept the token: '{presented}'");
        Assert.IsFalse(access.IsTokenRequired, "The open access should require no token");
    }

    #endregion // Methods
}