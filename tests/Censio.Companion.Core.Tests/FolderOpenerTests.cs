using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="FolderOpener" />.
/// </summary>
[TestClass]
public sealed class FolderOpenerTests
{
    #region Fields

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-folder-opener-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// A relative folder name no other test uses.
    /// </summary>
    private readonly string _relative = "censio-folder-opener-relative-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// The folders the launch action was called with.
    /// </summary>
    private readonly List<string> _launched = [];

    /// <summary>
    /// The logger the opener writes to.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Deletes the temporary folders.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }

        if (Directory.Exists(Path.GetFullPath(_relative)))
        {
            Directory.Delete(Path.GetFullPath(_relative), true);
        }
    }

    /// <summary>
    /// A missing folder is created and then launched, and nothing is logged.
    /// </summary>
    [TestMethod]
    public void FolderOpenerOpenCreatesMissingFolderAndLaunches()
    {
        // Arrange
        var folder = Path.Combine(_root, "Censio", "Recordings");
        var opener = new FolderOpener(_launched.Add, _logger);

        // Act
        opener.Open(folder, "recordings");

        // Assert
        Assert.IsTrue(Directory.Exists(folder), "The missing folder should be created");
        Assert.AreSequenceEqual(new[] { folder }, _launched, "The folder should be launched once");
        Assert.IsEmpty(_logger.Entries, "Nothing should be logged when the folder opens");
    }

    /// <summary>
    /// A launch that throws logs a warning with the exception and does not throw.
    /// </summary>
    [TestMethod]
    public void FolderOpenerOpenLaunchFailureLogsWarning()
    {
        // Arrange
        var folder = Path.Combine(_root, "Censio", "Recordings");
        var failure = new InvalidOperationException("explorer.exe could not be started");
        var opener = new FolderOpener(_ => throw failure, _logger);

        // Act
        opener.Open(folder, "recordings");

        // Assert
        Assert.HasCount(1, _logger.Entries, "One entry should be logged");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual("Could not open the recordings folder", _logger.Entries[0].Message, "The entry should name the folder");
        Assert.AreSame(failure, _logger.Entries[0].Exception, "The entry should carry the exception of the launch");
    }

    /// <summary>
    /// A folder that cannot be created logs a warning with the exception and is not launched.
    /// </summary>
    [TestMethod]
    public void FolderOpenerOpenCreateFailureLogsWarningWithoutLaunch()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        var blocking = Path.Combine(_root, "Censio");

        File.WriteAllText(blocking, "a file where the folder should be");

        var opener = new FolderOpener(_launched.Add, _logger);

        // Act
        opener.Open(Path.Combine(blocking, "Logs"), "log");

        // Assert
        Assert.IsEmpty(_launched, "A folder that could not be created should not be launched");
        Assert.HasCount(1, _logger.Entries, "One entry should be logged");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual("Could not open the log folder", _logger.Entries[0].Message, "The entry should name the folder");
        Assert.IsInstanceOfType<IOException>(_logger.Entries[0].Exception, "The entry should carry the exception of the creation");
    }

    /// <summary>
    /// A folder that is not a full path logs a warning without an exception, and nothing is created or launched.
    /// </summary>
    [TestMethod]
    public void FolderOpenerOpenRelativeFolderLogsWarningWithoutLaunch()
    {
        // Arrange
        var opener = new FolderOpener(_launched.Add, _logger);

        // Act
        opener.Open(_relative, "log");

        // Assert
        Assert.IsFalse(Directory.Exists(Path.GetFullPath(_relative)), "Nothing should be created below the working directory");
        Assert.IsEmpty(_launched, "A relative folder should not be launched");
        Assert.HasCount(1, _logger.Entries, "One entry should be logged");
        Assert.AreEqual(LogLevel.Warning, _logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual("Could not open the log folder", _logger.Entries[0].Message, "The entry should name the folder");
        Assert.IsNull(_logger.Entries[0].Exception, "The entry for a relative folder should carry no exception");
    }

    #endregion // Methods
}