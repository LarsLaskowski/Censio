using System.ComponentModel;

using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="BrowserOpener" />.
/// </summary>
[TestClass]
public sealed class BrowserOpenerTests
{
    #region Fields

    /// <summary>
    /// The address of the local server.
    /// </summary>
    private static readonly Uri _address = new("http://localhost:5117/");

    /// <summary>
    /// The addresses the launch action was called with.
    /// </summary>
    private readonly List<Uri> _launched = [];

    /// <summary>
    /// The logger the opener writes to.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// The address is launched once as given, and nothing is logged (S2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void BrowserOpenerOpenLaunchesAddress()
    {
        // Arrange
        var opener = new BrowserOpener(_launched.Add, _logger);

        // Act
        opener.Open(_address);

        // Assert
        Assert.AreSequenceEqual(new[] { _address }, _launched, "The address should be launched once as given");
        Assert.IsEmpty(_logger.Entries, "Nothing should be logged when the browser opens");
    }

    /// <summary>
    /// A launch that throws logs a warning with the exception and does not throw (E6).
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void BrowserOpenerOpenWhenLaunchFailsLogsWarning()
    {
        // Arrange
        var failure = new Win32Exception(2, "The system cannot find the file specified");
        var opener = new BrowserOpener(_ => throw failure, _logger);

        // Act
        opener.Open(_address);

        // Assert
        Assert.HasCount(1, _logger.Entries, "One entry should be logged");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual("Could not open the browser", _logger.Entries[0].Message, "The entry should say that the browser could not be opened");
        Assert.AreSame(failure, _logger.Entries[0].Exception, "The entry should carry the exception of the launch");
    }

    #endregion // Methods
}