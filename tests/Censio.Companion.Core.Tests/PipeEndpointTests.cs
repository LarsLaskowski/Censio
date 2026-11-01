namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeEndpoint" />.
/// </summary>
[TestClass]
public sealed class PipeEndpointTests
{
    #region Methods

    /// <summary>
    /// A connect attempt waits at most two seconds.
    /// </summary>
    [TestMethod]
    public void PipeEndpointAttemptTimeoutIsTwoSeconds()
    {
        // Arrange
        var expected = TimeSpan.FromSeconds(2);

        // Act
        var timeout = PipeEndpoint.AttemptTimeout;

        // Assert
        Assert.AreEqual(expected, timeout, "A connect attempt should wait two seconds at most");
    }

    /// <summary>
    /// Only a whole entry equal to the pipe path counts, ignoring case.
    /// </summary>
    /// <param name="entries">The entries, separated by a semicolon</param>
    /// <param name="expected">The expected result</param>
    [TestMethod]
    [DataRow(@"\\.\pipe\anno117", true)]
    [DataRow(@"\\.\PIPE\ANNO117", true)]
    [DataRow(@"\\.\pipe\other;\\.\pipe\anno117", true)]
    [DataRow(@"\\.\pipe\x\anno117", false)]
    [DataRow(@"\\.\pipe\anno1170", false)]
    [DataRow("", false)]
    public void PipeEndpointIsListedMatchesWholePath(string entries, bool expected)
    {
        // Arrange
        var list = entries.Length == 0 ? [] : entries.Split(';');

        // Act
        var actual = PipeEndpoint.IsListed(list);

        // Assert
        Assert.AreEqual(expected, actual, "Only a whole entry equal to the pipe path should count");
    }

    #endregion // Methods
}