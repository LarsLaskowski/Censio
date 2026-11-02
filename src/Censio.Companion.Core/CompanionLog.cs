using Censio.Pipe;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// The log messages of the Companion.
/// </summary>
public static partial class CompanionLog
{
    #region Methods

    /// <summary>
    /// Logs the start of the Companion.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="version">The version</param>
    /// <param name="mode">The mode</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Censio Companion {Version} started in mode {Mode}")]
    public static partial void Started(ILogger logger, string version, CompanionMode mode);

    /// <summary>
    /// Logs a change of the connection state.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="state">The state</param>
    /// <param name="errorKind">The kind of the error</param>
    /// <param name="errorCode">The Windows error code</param>
    /// <param name="receivedVersion">The received version</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Connection state {State}, error kind {ErrorKind}, Windows error code {ErrorCode}, received version {ReceivedVersion}")]
    public static partial void ConnectionStateChanged(ILogger logger, PipeConnectionState state, PipeErrorKind? errorKind, int? errorCode, int? receivedVersion);

    /// <summary>
    /// Logs the start of a recording.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording started")]
    public static partial void RecordingStarted(ILogger logger);

    /// <summary>
    /// Logs the stop of a recording.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording stopped")]
    public static partial void RecordingStopped(ILogger logger);

    /// <summary>
    /// Logs the creation of a recording file.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="fileName">The file name</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording file {FileName} created")]
    public static partial void RecordingFileCreated(ILogger logger, string fileName);

    /// <summary>
    /// Logs the failure of a recording.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Recording failed")]
    public static partial void RecordingFailed(ILogger logger);

    /// <summary>
    /// Logs the failure of a stopped recording.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A stopped recording failed while writing its remaining frames")]
    public static partial void StoppedRecordingFailed(ILogger logger);

    /// <summary>
    /// Logs the start of a replay.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="fileName">The file name</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Replay of {FileName} started")]
    public static partial void ReplayStarted(ILogger logger, string fileName);

    /// <summary>
    /// Logs the end of a replay.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Replay finished")]
    public static partial void ReplayFinished(ILogger logger);

    /// <summary>
    /// Logs the problem of a replay.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="reason">The reason</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Replay error: {Reason}")]
    public static partial void ReplayProblem(ILogger logger, string reason);

    /// <summary>
    /// Logs that a folder could not be opened.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="folder">The name of the folder</param>
    /// <param name="exception">The exception, or <see langword="null" /> if there is none</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not open the {Folder} folder")]
    public static partial void FolderOpenFailed(ILogger logger, string folder, Exception? exception);

    /// <summary>
    /// Logs a failed background task.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A background task failed")]
    public static partial void BackgroundTaskFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Logs the exit of the Companion.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Exit")]
    public static partial void Exiting(ILogger logger);

    #endregion // Methods
}