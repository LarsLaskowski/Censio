using System.IO.Pipes;
using System.Security.Principal;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeEndpoint" />.
/// </summary>
[TestClass]
public sealed class PipeEndpointTests
{
    #region Methods

    /// <summary>
    /// The pipe is opened by the name anno117 on the local machine, which is its full path.
    /// </summary>
    [TestMethod]
    public void PipeEndpointNameIsAnno117OnLocalServer()
    {
        // Arrange
        var expectedPath = @"\\.\pipe\anno117";

        // Act
        var serverName = PipeEndpoint.ServerName;
        var pipeName = PipeEndpoint.PipeName;
        var listedPath = PipeEndpoint.PipePath;

        // Assert
        Assert.AreEqual(".", serverName, "The pipe should be opened on the local machine");
        Assert.AreEqual("anno117", pipeName, "The pipe should be opened by the name anno117");
        Assert.AreEqual(expectedPath, $@"\\{serverName}\pipe\{pipeName}", "Server and pipe name should form the path of the game's pipe");
        Assert.AreEqual(expectedPath, listedPath, "The listed path should be the path of the game's pipe");
    }

    /// <summary>
    /// The pipe is opened for reading only, with overlapped reads.
    /// </summary>
    [TestMethod]
    public void PipeEndpointOpensReadOnlyAsynchronous()
    {
        // Arrange
        var direction = PipeEndpoint.Direction;

        // Act
        var options = PipeEndpoint.Options;

        // Assert
        Assert.AreEqual(PipeDirection.In, direction, "The pipe should be opened with read access only");
        Assert.AreEqual(PipeOptions.Asynchronous, options, "The pipe should be opened for asynchronous reads and nothing else");
    }

    /// <summary>
    /// The pipe's server may identify the user but not act as the user.
    /// </summary>
    [TestMethod]
    public void PipeEndpointImpersonationLevelIsIdentification()
    {
        // Arrange
        var notImpersonation = TokenImpersonationLevel.Impersonation;

        // Act
        var level = PipeEndpoint.ImpersonationLevel;

        // Assert
        Assert.AreEqual(TokenImpersonationLevel.Identification, level, "The pipe's server should only be able to identify the user");
        Assert.AreNotEqual(notImpersonation, level, "The pipe's server should not be able to act as the user");
    }

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