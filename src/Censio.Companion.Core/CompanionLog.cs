using Censio.GameData;
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

    /// <summary>
    /// Logs that the autostart was turned on.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Autostart enabled")]
    public static partial void AutostartEnabled(ILogger logger);

    /// <summary>
    /// Logs that the autostart was turned off.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Autostart disabled")]
    public static partial void AutostartDisabled(ILogger logger);

    /// <summary>
    /// Logs that the autostart setting could not be read.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception, or <see langword="null" /> if there is none</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the autostart setting")]
    public static partial void AutostartReadFailed(ILogger logger, Exception? exception);

    /// <summary>
    /// Logs that the autostart setting could not be changed.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception, or <see langword="null" /> if there is none</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not change the autostart setting")]
    public static partial void AutostartChangeFailed(ILogger logger, Exception? exception);

    /// <summary>
    /// Logs a found game installation.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="source">The source</param>
    /// <param name="build">The build</param>
    /// <param name="folder">The folder</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Game installation found through {Source}, build {Build}, folder {Folder}")]
    public static partial void GameInstallationFound(ILogger logger, string source, string build, string folder);

    /// <summary>
    /// Logs that no game installation was found.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception, if any</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Game installation not found; checked the Ubisoft Connect records and its default games folder")]
    public static partial void GameInstallationNotFound(ILogger logger, Exception? exception);

    /// <summary>
    /// Logs a folder of the settings file that does not exist.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="folder">The folder</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The game folder {Folder} in the settings file does not exist")]
    public static partial void GameSettingsFolderMissing(ILogger logger, string folder);

    /// <summary>
    /// Logs a folder of the settings file that is not a full path.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="folder">The folder</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The game folder {Folder} in the settings file is not a full path")]
    public static partial void GameSettingsFolderNotFullPath(ILogger logger, string folder);

    /// <summary>
    /// Logs a folder of the settings file that is not a game installation.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="folder">The folder</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The game folder {Folder} in the settings file is not an Anno 117 installation; expected the folder that holds maindata\\config.rda and bin\\win64\\anno117.exe")]
    public static partial void GameSettingsFolderNotInstallation(ILogger logger, string folder);

    /// <summary>
    /// Logs a settings file that cannot be used.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="file">The file</param>
    /// <param name="problem">The problem</param>
    /// <param name="exception">The exception, if any</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file {File} is invalid: {Problem}")]
    public static partial void GameSettingsFileInvalid(ILogger logger, string file, string problem, Exception? exception);

    /// <summary>
    /// Logs the built catalog.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="entryCount">The number of entries</param>
    /// <param name="inheritedAssetCount">The number of inherited assets left out</param>
    /// <param name="droppedRegionTypeCount">The number of unknown region types dropped</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog built: {EntryCount} entries; {InheritedAssetCount} inherited assets left out; {DroppedRegionTypeCount} unknown region types dropped")]
    public static partial void CatalogBuilt(ILogger logger, int entryCount, int inheritedAssetCount, int droppedRegionTypeCount);

    /// <summary>
    /// Logs that the catalog could not be built.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="kind">The kind of the error</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog failed: {Kind}")]
    public static partial void CatalogFailed(ILogger logger, GameCatalogErrorKind kind);

    /// <summary>
    /// Logs a copy of a catalog file in another archive.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="archive">The name of the archive</param>
    /// <param name="path">The path of the copy</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog: {Archive} also lists {Path}; the copy in config.rda is used")]
    public static partial void CatalogCopyFound(ILogger logger, string archive, string path);

    /// <summary>
    /// Logs the archives that could not be checked for copies.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="count">The number of archives</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog: {Count} other archives could not be checked for copies")]
    public static partial void CatalogArchivesUnchecked(ILogger logger, int count);

    /// <summary>
    /// Logs the built catalog icons.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="goodCount">The number of goods with an icon</param>
    /// <param name="regionCount">The number of regions with an icon</param>
    /// <param name="notNamedCount">The number of assets that name no icon</param>
    /// <param name="missingCount">The number of assets whose icon is missing</param>
    /// <param name="unreadableCount">The number of assets whose icon is unreadable</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog icons: {GoodCount} goods and {RegionCount} regions with an icon; without one: {NotNamedCount} not named, {MissingCount} missing, {UnreadableCount} unreadable")]
    public static partial void CatalogIconsBuilt(ILogger logger, int goodCount, int regionCount, int notNamedCount, int missingCount, int unreadableCount);

    /// <summary>
    /// Logs that the catalog icons could not be loaded.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="kind">The kind of the error</param>
    /// <param name="archive">The name of the archive concerned</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog icons failed: {Kind} ({Archive})")]
    public static partial void CatalogIconsFailed(ILogger logger, GameIconErrorKind kind, string archive);

    /// <summary>
    /// Logs a GUID the catalog cannot name.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="role">The role of the GUID</param>
    /// <param name="assetGuid">The asset GUID</param>
    /// <param name="problem">The problem</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog cannot name {Role} {AssetGuid}: {Problem}")]
    public static partial void CatalogCannotName(ILogger logger, GameCatalogKind role, int assetGuid, GameCatalogProblem problem);

    /// <summary>
    /// Logs that no further GUIDs are reported.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="maxReports">The most GUIDs logged</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog: more than {MaxReports} GUIDs cannot be named; further ones are not logged for this catalog")]
    public static partial void CatalogReportsCapped(ILogger logger, int maxReports);

    /// <summary>
    /// Logs why the catalog cache was not used.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="reason">The reason</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog cache not used: {Reason}")]
    public static partial void CatalogCacheNotUsed(ILogger logger, string reason);

    /// <summary>
    /// Logs that the catalog cache is invalid.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog cache not used: invalid")]
    public static partial void CatalogCacheInvalid(ILogger logger);

    /// <summary>
    /// Logs that the catalog cache could not be written.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception, if any</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog cache not written")]
    public static partial void CatalogCacheNotWritten(ILogger logger, Exception? exception);

    /// <summary>
    /// Logs that the check for a changed game failed.
    /// </summary>
    /// <param name="logger">The logger</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog check failed; the current catalog is kept")]
    public static partial void CatalogCheckFailed(ILogger logger);

    /// <summary>
    /// Logs the durations of an extraction.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="seconds">The total seconds</param>
    /// <param name="nameSeconds">The seconds of the names</param>
    /// <param name="iconSeconds">The seconds of the icons</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog extracted in {Seconds} s (names {NameSeconds} s, icons {IconSeconds} s)")]
    public static partial void CatalogExtracted(ILogger logger, string seconds, string nameSeconds, string iconSeconds);

    /// <summary>
    /// Logs a catalog loaded from the cache.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="seconds">The seconds</param>
    /// <param name="count">The number of entries</param>
    /// <param name="goodCount">The number of goods with an icon</param>
    /// <param name="regionCount">The number of regions with an icon</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog loaded from cache in {Seconds} s: {Count} entries, {GoodCount} goods and {RegionCount} regions with an icon")]
    public static partial void CatalogLoadedFromCache(ILogger logger, string seconds, int count, int goodCount, int regionCount);

    #endregion // Methods
}