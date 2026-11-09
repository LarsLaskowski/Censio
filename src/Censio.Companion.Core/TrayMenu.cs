using System.Globalization;

using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Builds the entries of the tray menu.
/// </summary>
public static class TrayMenu
{
    #region Constants

    /// <summary>
    /// The tooltip of the tray icon.
    /// </summary>
    public const string ToolTip = "Censio Companion";

    /// <summary>
    /// The text of the exit entry.
    /// </summary>
    public const string ExitText = "Exit";

    /// <summary>
    /// The tooltip of the tray icon when the command line is invalid.
    /// </summary>
    public const string InvalidCommandLineToolTip = "Censio Companion: Invalid command line";

    /// <summary>
    /// The status text when the command line is invalid.
    /// </summary>
    public const string InvalidCommandLineStatusText = "Invalid command line";

    /// <summary>
    /// The longest text the tray icon can show as a tooltip.
    /// </summary>
    public const int MaxToolTipLength = 127;

    /// <summary>
    /// The text of the entry that starts the recording.
    /// </summary>
    public const string StartRecordingText = "Start recording";

    /// <summary>
    /// The text of the entry that stops the recording.
    /// </summary>
    public const string StopRecordingText = "Stop recording";

    /// <summary>
    /// The text of the entry that opens the recordings folder.
    /// </summary>
    public const string OpenRecordingsFolderText = "Open recordings folder";

    /// <summary>
    /// The text of the entry that opens the log folder.
    /// </summary>
    public const string OpenLogFolderText = "Open log folder";

    /// <summary>
    /// The text of the entry that opens the diagnostics window.
    /// </summary>
    public const string OpenDiagnosticsText = "Open diagnostics";

    /// <summary>
    /// The text of the entry that turns the start with Windows on or off.
    /// </summary>
    public const string StartWithWindowsText = "Start with Windows";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Formats the text of the version entry.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <returns>The text of the version entry</returns>
    public static string FormatVersion(string version)
    {
        return "Version " + version;
    }

    /// <summary>
    /// Formats the tooltip of the tray icon for a connection state.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <returns>The tooltip text</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state" /> is not a defined value</exception>
    public static string FormatToolTip(PipeConnectionState state)
    {
        var text = state switch
                   {
                       PipeConnectionState.WaitingForGame => "Waiting for game",
                       PipeConnectionState.Connected => "Connected",
                       PipeConnectionState.Receiving => "Receiving",
                       PipeConnectionState.PipeBusy => "Pipe busy",
                       PipeConnectionState.ProtocolMismatch => "Protocol mismatch",
                       PipeConnectionState.Error => "Error",
                       _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The connection state is not defined")
                   };

        return $"{ToolTip}: {text}";
    }

    /// <summary>
    /// Formats the tooltip of the tray icon for a connection state and a recording state.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <returns>The tooltip text</returns>
    /// <exception cref="ArgumentOutOfRangeException">A state is not a defined value</exception>
    public static string FormatToolTip(PipeConnectionState state, PipeRecordingState recording)
    {
        var suffix = recording switch
                     {
                         PipeRecordingState.Off => string.Empty,
                         PipeRecordingState.Recording => " (recording)",
                         PipeRecordingState.Failed => " (recording failed)",
                         _ => throw new ArgumentOutOfRangeException(nameof(recording), recording, "The recording state is not defined")
                     };

        return FormatToolTip(state) + suffix;
    }

    /// <summary>
    /// Formats the tooltip of the tray icon for a replay status.
    /// </summary>
    /// <param name="status">The replay status</param>
    /// <returns>The tooltip text</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state or the problem kind is not a defined value</exception>
    public static string FormatReplayToolTip(PipeReplayStatus status)
    {
        if (status.Problem is { } problem)
        {
            var reason = FormatReplayReason(problem) ?? throw new ArgumentOutOfRangeException(nameof(status), problem.Kind, "The problem kind is not defined");

            return $"{ToolTip}: Replay error: {reason}";
        }

        if (status.IsFinished)
        {
            return $"{ToolTip}: Replay finished";
        }

        var text = status.State switch
                   {
                       PipeConnectionState.Connected => "Connected",
                       PipeConnectionState.Receiving => "Receiving",
                       PipeConnectionState.ProtocolMismatch => "Protocol mismatch",
                       _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, "The replay state is not defined")
                   };

        return $"{ToolTip}: Replay: {text}";
    }

    /// <summary>
    /// Formats the status text for a connection state and a recording state.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <returns>The status text</returns>
    public static string FormatStatusText(PipeConnectionState state, PipeRecordingState recording)
    {
        return WithoutToolTipPrefix(FormatToolTip(state, recording));
    }

    /// <summary>
    /// Formats the status text for a replay status.
    /// </summary>
    /// <param name="status">The replay status</param>
    /// <returns>The status text</returns>
    public static string FormatReplayStatusText(PipeReplayStatus status)
    {
        return WithoutToolTipPrefix(FormatReplayToolTip(status));
    }

    /// <summary>
    /// Formats the text of the last tick.
    /// </summary>
    /// <param name="tick">The summary of the last tick, or <c>null</c> if none was received</param>
    /// <param name="clock">The clock</param>
    /// <returns>The text</returns>
    public static string FormatLastTick(PipeTickSummary? tick, TimeProvider clock)
    {
        if (tick is not { } summary)
        {
            return "Last tick: none";
        }

        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone);
        var count = summary.IslandCount == 1
                        ? "1 island"
                        : string.Create(CultureInfo.InvariantCulture, $"{summary.IslandCount} islands");
        var time = summary.ReceiveTime;
        string text;

        if (time.UtcTicks - DateTimeOffset.MinValue.UtcTicks < TimeSpan.TicksPerDay || DateTimeOffset.MaxValue.UtcTicks - time.UtcTicks < TimeSpan.TicksPerDay)
        {
            text = time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }
        else
        {
            var local = TimeZoneInfo.ConvertTime(time, clock.LocalTimeZone);

            text = local.Date == now.Date
                       ? local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                       : local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return $"Last tick: {text}, {count}";
    }

    /// <summary>
    /// Formats the tooltip of a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    /// <param name="clock">The clock</param>
    /// <returns>The tooltip text</returns>
    public static string FormatSnapshotToolTip(TraySnapshot snapshot, TimeProvider clock)
    {
        var text = $"{ToolTip}: {snapshot.StatusText}";

        return snapshot.LastTick is null
                   ? text
                   : $"{text}\n{FormatLastTick(snapshot.LastTick, clock)}";
    }

    /// <summary>
    /// Gets the time until the next local midnight.
    /// </summary>
    /// <param name="clock">The clock</param>
    /// <returns>The time, at least one second</returns>
    public static TimeSpan UntilNextLocalMidnight(TimeProvider clock)
    {
        var zone = clock.LocalTimeZone;
        var utcNow = clock.GetUtcNow();
        var target = TimeZoneInfo.ConvertTime(utcNow, zone).Date.AddDays(1);

        if (zone.IsInvalidTime(target))
        {
            target = target.AddHours(1);
        }

        var delay = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(target, zone), TimeSpan.Zero) - utcNow;

        return delay < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay;
    }

    /// <summary>
    /// Creates the entries of the tray menu.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <param name="snapshot">The snapshot</param>
    /// <param name="game">The status of the game installation search</param>
    /// <param name="catalog">The status of the game catalog</param>
    /// <param name="isAutostartEnabled">Whether the Companion starts with Windows</param>
    /// <param name="clock">The clock</param>
    /// <returns>The entries in display order</returns>
#pragma warning disable S1172 // Skeleton: removed with the implementation
    public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, GameInstallationStatus game, GameCatalogStatus catalog, bool isAutostartEnabled, TimeProvider clock)
    {
        return [
                   new TrayMenuEntry(TrayMenuEntryKind.Status, $"Status: {snapshot.StatusText}", false),
                   new TrayMenuEntry(TrayMenuEntryKind.LastTick, FormatLastTick(snapshot.LastTick, clock), false),
                   new TrayMenuEntry(TrayMenuEntryKind.Game, FormatGame(game), false),
                   new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
                   new TrayMenuEntry(TrayMenuEntryKind.OpenDiagnostics, OpenDiagnosticsText, true),
                   new TrayMenuEntry(TrayMenuEntryKind.ToggleRecording, snapshot.IsRecording ? StopRecordingText : StartRecordingText, snapshot.CanRecord),
                   new TrayMenuEntry(TrayMenuEntryKind.OpenRecordingsFolder, OpenRecordingsFolderText, true),
                   new TrayMenuEntry(TrayMenuEntryKind.OpenLogFolder, OpenLogFolderText, true),
                   new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
                   new TrayMenuEntry(TrayMenuEntryKind.ToggleAutostart, StartWithWindowsText, true, isAutostartEnabled),
                   new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
                   new TrayMenuEntry(TrayMenuEntryKind.Version, FormatVersion(version), false),
                   new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
                   new TrayMenuEntry(TrayMenuEntryKind.Exit, ExitText, true)
               ];
    }
#pragma warning restore S1172

    /// <summary>
    /// Formats the text of the catalog entry.
    /// </summary>
    /// <param name="status">The status of the game catalog</param>
    /// <returns>The text</returns>
    public static string FormatCatalog(GameCatalogStatus status)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the text of the game entry.
    /// </summary>
    /// <param name="status">The status of the game installation search</param>
    /// <returns>The text</returns>
    public static string FormatGame(GameInstallationStatus status)
    {
        var source = status.Source == GameInstallationSource.UbisoftConnect ? "Ubisoft Connect" : "settings file";

        return status.Kind switch
               {
                   GameInstallationStatusKind.Searching => "Game: searching",
                   GameInstallationStatusKind.Found => $"Game: build {status.Build ?? "unknown"} ({source})",
                   GameInstallationStatusKind.NotFound => "Game: not found",
                   GameInstallationStatusKind.SettingsFolderNotFound => "Game: folder in settings not found",
                   GameInstallationStatusKind.SettingsFolderNotFullPath => "Game: folder in settings is not a full path",
                   GameInstallationStatusKind.SettingsFolderNotInstallation => "Game: folder in settings is not an Anno 117 installation",
                   GameInstallationStatusKind.SettingsFileInvalid => "Game: settings file invalid",
                   _ => throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "The status kind is not defined")
               };
    }

    /// <summary>
    /// Formats the reason of a replay problem.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The reason, or <see langword="null" /> if the problem kind is not a defined value</returns>
    public static string? FormatReplayReason(PipeReplayProblem problem)
    {
        return problem.Kind switch
               {
                   PipeReplayProblemKind.FileNotFound => "file not found",
                   PipeReplayProblemKind.NotARecording => "not a recording",
                   PipeReplayProblemKind.UnsupportedVersion => "unsupported version",
                   PipeReplayProblemKind.InvalidLine => string.Create(CultureInfo.InvariantCulture, $"invalid line {problem.LineNumber}").TrimEnd(),
                   PipeReplayProblemKind.ReadFailed => "read failed",
                   _ => null
               };
    }

    /// <summary>
    /// Removes the application name and the colon from the start of a tooltip text.
    /// </summary>
    /// <param name="toolTip">The tooltip text</param>
    /// <returns>The status text</returns>
    private static string WithoutToolTipPrefix(string toolTip)
    {
        return toolTip[(ToolTip.Length + 2)..];
    }

    #endregion // Methods
}