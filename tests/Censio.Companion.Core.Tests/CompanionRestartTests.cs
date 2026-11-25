using System.ComponentModel;

using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionRestart" />.
/// </summary>
[TestClass]
public sealed class CompanionRestartTests
{
    #region Constants

    /// <summary>
    /// The start of the line a restart that did not start logs.
    /// </summary>
    private const string NotStartedLine = "Restart failed: the Companion could not be started";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The full path of the Companion's executable, as <see cref="Environment.ProcessPath" /> gives it.
    /// </summary>
    private static readonly string _exePath = Path.Combine(Path.GetTempPath(), "Censio", "Censio.Companion.exe");

    /// <summary>
    /// The paths and arguments the launch delegate was called with, in order.
    /// </summary>
    private readonly List<(string Path, string Argument)> _launches = [];

    /// <summary>
    /// The logger of the restart.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// A restart starts the Companion's own executable with the switch --restarted, once, and logs nothing (S3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void CompanionRestartStartsOwnExeWithRestartedSwitch()
    {
        // Arrange
        var restart = new CompanionRestart(Record, _exePath, _logger);

        // Act
        var started = restart.TryStart();

        // Assert
        Assert.IsTrue(started, "A launch that does not fail should count as started");
        Assert.AreSequenceEqual([(_exePath, "--restarted")], _launches.ToArray(), "The restart should launch the own executable once with --restarted");
        Assert.IsEmpty(_logger.Entries, "A started restart should log nothing");
    }

    /// <summary>
    /// A launch that fails with one of the exceptions of a process start returns false and logs the exception's type
    /// without the exception, so the old Companion keeps running (E15).
    /// </summary>
    /// <param name="exceptionType">The name of the exception's type</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(nameof(Win32Exception))]
    [DataRow(nameof(IOException))]
    [DataRow(nameof(InvalidOperationException))]
    [DataRow(nameof(UnauthorizedAccessException))]
    public void CompanionRestartLaunchFailureReturnsFalseAndLogs(string exceptionType)
    {
        // Arrange
        var failure = CreateFailure(exceptionType);
        var restart = new CompanionRestart((_, _) => throw failure, _exePath, _logger);

        // Act
        var started = restart.TryStart();

        // Assert
        Assert.IsFalse(started, $"A launch failing with {exceptionType} should not count as started");
        Assert.AreSequenceEqual([(LogLevel.Warning, $"{NotStartedLine}: {exceptionType}", (Exception?)null)], _logger.Entries.ToArray(), "The failed launch should be logged once as a warning with the exception's type and without the exception");
    }

    /// <summary>
    /// An executable path that is unknown or not a full path launches nothing, returns false and logs that the restart
    /// did not start with the type ArgumentException (E15).
    /// </summary>
    /// <param name="exePath">The path of the executable</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Censio.Companion.exe")]
    [DataRow("Censio/Censio.Companion.exe")]
    public void CompanionRestartWithoutFullExePathLaunchesNothing(string? exePath)
    {
        // Arrange
        var restart = new CompanionRestart(Record, exePath, _logger);

        // Act
        var started = restart.TryStart();

        // Assert
        Assert.IsFalse(started, $"The path '{exePath}' should not count as started");
        Assert.IsEmpty(_launches, $"The path '{exePath}' should launch nothing");
        Assert.AreSequenceEqual([(LogLevel.Warning, $"{NotStartedLine}: {nameof(ArgumentException)}", (Exception?)null)], _logger.Entries.ToArray(), "The restart that did not start should be logged once as a warning naming ArgumentException, without an exception");
    }

    /// <summary>
    /// Creates an exception of a process start.
    /// </summary>
    /// <param name="exceptionType">The name of the exception's type</param>
    /// <returns>The exception</returns>
    private static Exception CreateFailure(string exceptionType)
    {
        return exceptionType switch
               {
                   nameof(Win32Exception) => new Win32Exception(2),
                   nameof(IOException) => new IOException("The file is in use"),
                   nameof(InvalidOperationException) => new InvalidOperationException("No file name"),
                   _ => new UnauthorizedAccessException("Access is denied")
               };
    }

    /// <summary>
    /// The launch delegate of the restarts: records the path and the argument.
    /// </summary>
    /// <param name="path">The path of the executable</param>
    /// <param name="argument">The argument</param>
    private void Record(string path, string argument)
    {
        _launches.Add((path, argument));
    }

    #endregion // Methods
}