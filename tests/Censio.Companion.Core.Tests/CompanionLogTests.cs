using Censio.Pipe;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionLog" />.
/// </summary>
[TestClass]
public sealed class CompanionLogTests
{
    #region Methods

    /// <summary>
    /// Every log method writes its level and its exact text with the values filled in, and passes on its exception.
    /// </summary>
    [TestMethod]
    public void CompanionLogTemplates()
    {
        // Arrange
        var logger = new RecordingLogger();
        var folderFailure = new IOException("The folder is gone");
        var taskFailure = new InvalidOperationException("The task failed");

        // Act
        CompanionLog.Started(logger, "0.1.0", CompanionMode.Record);
        CompanionLog.ConnectionStateChanged(logger, PipeConnectionState.Error, PipeErrorKind.ReadFailed, 109, null);
        CompanionLog.RecordingStarted(logger);
        CompanionLog.RecordingStopped(logger);
        CompanionLog.RecordingFileCreated(logger, "recording-20261101-170900Z.jsonl");
        CompanionLog.RecordingFailed(logger);
        CompanionLog.StoppedRecordingFailed(logger);
        CompanionLog.ReplayStarted(logger, "recording-20261101-170900Z.jsonl");
        CompanionLog.ReplayFinished(logger);
        CompanionLog.ReplayProblem(logger, "invalid line 3");
        CompanionLog.FolderOpenFailed(logger, "log", folderFailure);
        CompanionLog.BackgroundTaskFailed(logger, taskFailure);
        CompanionLog.Exiting(logger);

        // Assert
        (LogLevel, string, Exception?)[] expected = [
                                                        (LogLevel.Information, "Censio Companion 0.1.0 started in mode Record", null),
                                                        (LogLevel.Information, "Connection state Error, error kind ReadFailed, Windows error code 109, received version (null)", null),
                                                        (LogLevel.Information, "Recording started", null),
                                                        (LogLevel.Information, "Recording stopped", null),
                                                        (LogLevel.Information, "Recording file recording-20261101-170900Z.jsonl created", null),
                                                        (LogLevel.Warning, "Recording failed", null),
                                                        (LogLevel.Warning, "A stopped recording failed while writing its remaining frames", null),
                                                        (LogLevel.Information, "Replay of recording-20261101-170900Z.jsonl started", null),
                                                        (LogLevel.Information, "Replay finished", null),
                                                        (LogLevel.Warning, "Replay error: invalid line 3", null),
                                                        (LogLevel.Warning, "Could not open the log folder", folderFailure),
                                                        (LogLevel.Warning, "A background task failed", taskFailure),
                                                        (LogLevel.Information, "Exit", null),
                                                    ];

        Assert.AreSequenceEqual(expected, logger.Entries.Select(entry => (entry.Level, entry.Message, entry.Exception)).ToArray(), "Each method should log its level, its text and its exception");
    }

    #endregion // Methods
}