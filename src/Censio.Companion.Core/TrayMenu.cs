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
    /// The longest text the tray icon can show as a tooltip.
    /// </summary>
    public const int MaxToolTipLength = 127;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Formats the text of the version entry.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="version">The product version</param>
    /// <returns>The text of the version entry</returns>
    public static string FormatVersion(CompanionTexts texts, string version)
    {
        return "Version " + version;
    }

    /// <summary>
    /// Formats the tooltip of the tray icon for a connection state.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="state">The connection state</param>
    /// <returns>The tooltip text</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state" /> is not a defined value</exception>
    public static string FormatToolTip(CompanionTexts texts, PipeConnectionState state)
    {
        var text = state switch
                   {
                       PipeConnectionState.WaitingForGame => texts.StateWaitingForGame,
                       PipeConnectionState.Connected => texts.StateConnected,
                       PipeConnectionState.Receiving => texts.StateReceiving,
                       PipeConnectionState.PipeBusy => texts.StatePipeBusy,
                       PipeConnectionState.ProtocolMismatch => texts.StateProtocolMismatch,
                       PipeConnectionState.Error => texts.StateError,
                       _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The connection state is not defined")
                   };

        return $"{texts.ToolTip}: {text}";
    }

    /// <summary>
    /// Formats the tooltip of the tray icon for a connection state and a recording state.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <returns>The tooltip text</returns>
    /// <exception cref="ArgumentOutOfRangeException">A state is not a defined value</exception>
    public static string FormatToolTip(CompanionTexts texts, PipeConnectionState state, PipeRecordingState recording)
    {
        var suffix = recording switch
                     {
                         PipeRecordingState.Off => string.Empty,
                         PipeRecordingState.Recording => texts.RecordingSuffix,
                         PipeRecordingState.Failed => texts.RecordingFailedSuffix,
                         _ => throw new ArgumentOutOfRangeException(nameof(recording), recording, "The recording state is not defined")
                     };

        return FormatToolTip(texts, state) + suffix;
    }

    /// <summary>
    /// Formats the tooltip of the tray icon for a replay status.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The replay status</param>
    /// <returns>The tooltip text</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state or the problem kind is not a defined value</exception>
    public static string FormatReplayToolTip(CompanionTexts texts, PipeReplayStatus status)
    {
        if (status.SendProblem is { } sendProblem)
        {
            var sendReason = FormatReplaySendReason(texts, sendProblem) ?? throw new ArgumentOutOfRangeException(nameof(status), sendProblem, "The send problem kind is not defined");

            return $"{texts.ToolTip}: {string.Format(CultureInfo.InvariantCulture, texts.ReplayErrorFormat, sendReason)}";
        }

        if (status.Problem is { } problem)
        {
            var reason = FormatReplayReason(texts, problem) ?? throw new ArgumentOutOfRangeException(nameof(status), problem.Kind, "The problem kind is not defined");

            return $"{texts.ToolTip}: {string.Format(CultureInfo.InvariantCulture, texts.ReplayErrorFormat, reason)}";
        }

        if (status.IsConnecting)
        {
            return $"{texts.ToolTip}: {texts.ReplayConnecting}";
        }

        if (status is { IsFinished: true, Sent: { } sent })
        {
            return $"{texts.ToolTip}: {string.Format(CultureInfo.InvariantCulture, sent.Ticks == 1 ? texts.ReplayFinishedOneTickFormat : texts.ReplayFinishedTicksFormat, sent.Ticks, sent.Refused)}";
        }

        if (status.IsFinished)
        {
            return $"{texts.ToolTip}: {texts.ReplayFinished}";
        }

        var text = status.State switch
                   {
                       PipeConnectionState.Connected => texts.StateConnected,
                       PipeConnectionState.Receiving => texts.StateReceiving,
                       PipeConnectionState.ProtocolMismatch => texts.StateProtocolMismatch,
                       _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, "The replay state is not defined")
                   };

        return $"{texts.ToolTip}: {string.Format(CultureInfo.InvariantCulture, texts.ReplayStateFormat, text)}";
    }

    /// <summary>
    /// Formats the status text for a connection state and a recording state.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <returns>The status text</returns>
    public static string FormatStatusText(CompanionTexts texts, PipeConnectionState state, PipeRecordingState recording)
    {
        return WithoutToolTipPrefix(texts, FormatToolTip(texts, state, recording));
    }

    /// <summary>
    /// Formats the status text for a replay status.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The replay status</param>
    /// <returns>The status text</returns>
    public static string FormatReplayStatusText(CompanionTexts texts, PipeReplayStatus status)
    {
        return WithoutToolTipPrefix(texts, FormatReplayToolTip(texts, status));
    }

    /// <summary>
    /// Formats the text of the last tick.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="tick">The summary of the last tick, or <c>null</c> if none was received</param>
    /// <param name="clock">The clock</param>
    /// <returns>The text</returns>
    public static string FormatLastTick(CompanionTexts texts, PipeTickSummary? tick, TimeProvider clock)
    {
        if (tick is not { } summary)
        {
            return texts.LastTickNone;
        }

        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone);
        var count = summary.IslandCount == 1
                        ? texts.OneIsland
                        : string.Format(CultureInfo.InvariantCulture, texts.IslandsFormat, summary.IslandCount);
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

        return string.Format(CultureInfo.InvariantCulture, texts.LastTickFormat, text, count);
    }

    /// <summary>
    /// Formats the tooltip of a snapshot.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="snapshot">The snapshot</param>
    /// <param name="clock">The clock</param>
    /// <returns>The tooltip text</returns>
    public static string FormatSnapshotToolTip(CompanionTexts texts, TraySnapshot snapshot, TimeProvider clock)
    {
        var text = $"{texts.ToolTip}: {snapshot.StatusText}";

        return snapshot.LastTick is null
                   ? text
                   : $"{text}\n{FormatLastTick(texts, snapshot.LastTick, clock)}";
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
    /// <param name="texts">The texts</param>
    /// <param name="state">What the menu is built from</param>
    /// <returns>The entries in display order</returns>
    public static IReadOnlyList<TrayMenuEntry> Create(CompanionTexts texts, TrayMenuState state)
    {
        var version = state.Version;
        var snapshot = state.Snapshot;
        var game = state.Game;
        var catalog = state.Catalog;
        var server = state.Server;
        var clock = state.Clock;
        var profileName = state.ProfileName;
        var entries = new List<TrayMenuEntry>
                      {
                          new(TrayMenuEntryKind.Status, $"Status: {snapshot.StatusText}", false),
                          new(TrayMenuEntryKind.LastTick, FormatLastTick(texts, snapshot.LastTick, clock), false),
                          new(TrayMenuEntryKind.Game, FormatGame(texts, game), false),
                          new(TrayMenuEntryKind.Catalog, FormatCatalog(texts, catalog), false)
                      };

        if (server?.Local is { } local)
        {
            entries.Add(new TrayMenuEntry(TrayMenuEntryKind.LocalServer, FormatServerProfile(texts, FormatLocalServer(texts, local), profileName), false));
        }
        else if (server?.Live is { } live)
        {
            entries.Add(new TrayMenuEntry(TrayMenuEntryKind.ServerConnection, FormatServerProfile(texts, FormatLiveSend(texts, live), profileName), false));
        }

        if (server?.Buffer is { } buffer)
        {
            entries.Add(new TrayMenuEntry(TrayMenuEntryKind.LiveBuffer, FormatLiveBuffer(texts, buffer), false));
        }

        entries.Add(new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false));

        if (server?.Local is { } running)
        {
            entries.Add(new TrayMenuEntry(TrayMenuEntryKind.OpenCensio, texts.OpenCensioText, running.State == LocalServerState.Running));
        }

        if (state.Notice is { } notice)
        {
            entries.Add(new TrayMenuEntry(TrayMenuEntryKind.ProfileNotice, texts.FormatNotice(notice), true));
        }

        entries.AddRange([
                             new TrayMenuEntry(TrayMenuEntryKind.OpenSettings, texts.OpenSettingsText, true),
                             new TrayMenuEntry(TrayMenuEntryKind.OpenDiagnostics, texts.OpenDiagnosticsText, true),
                             new TrayMenuEntry(TrayMenuEntryKind.ToggleRecording, snapshot.IsRecording ? texts.StopRecordingText : texts.StartRecordingText, snapshot.CanRecord),
                             new TrayMenuEntry(TrayMenuEntryKind.OpenRecordingsFolder, texts.OpenRecordingsFolderText, true),
                             new TrayMenuEntry(TrayMenuEntryKind.OpenLogFolder, texts.OpenLogFolderText, true),
                             new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
                             new TrayMenuEntry(TrayMenuEntryKind.Version, FormatVersion(texts, version), false),
                             new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
                             new TrayMenuEntry(TrayMenuEntryKind.Exit, texts.ExitText, true)
                         ]);

        return entries;
    }

    /// <summary>
    /// Appends the name of the active profile to the text of the server line.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="server">The text of the server line</param>
    /// <param name="profileName">The name of the active profile, or <see langword="null" /> for none</param>
    /// <returns>The text of the server line, with the profile name if there is one</returns>
    public static string FormatServerProfile(CompanionTexts texts, string server, string? profileName)
    {
        return profileName is null
                   ? server
                   : string.Format(CultureInfo.InvariantCulture, texts.ServerProfileFormat, server, profileName);
    }

    /// <summary>
    /// Formats the text of the local server entry.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The status of the local server</param>
    /// <returns>The text</returns>
    public static string FormatLocalServer(CompanionTexts texts, LocalServerStatus status)
    {
        return status.State switch
               {
                   LocalServerState.Starting => texts.LocalServerStarting,
                   LocalServerState.Running => string.Format(CultureInfo.InvariantCulture, texts.LocalServerRunningFormat, status.Port),
                   LocalServerState.PortInUse => string.Format(CultureInfo.InvariantCulture, texts.LocalServerPortInUseFormat, status.Port),
                   LocalServerState.Failed => texts.LocalServerFailed,
                   _ => texts.LocalServerSettingsInvalid
               };
    }

    /// <summary>
    /// Formats the text of the live buffer entry.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The status of the live buffer</param>
    /// <returns>The text</returns>
    public static string FormatLiveBuffer(CompanionTexts texts, LiveBufferStatus status)
    {
        var text = string.Format(CultureInfo.InvariantCulture, texts.BufferFormat, status.Waiting);

        if (status.Dropped > 0)
        {
            text += string.Format(CultureInfo.InvariantCulture, texts.BufferDroppedFormat, status.Dropped);
        }

        return status.IsMemoryOnly ? text + texts.BufferMemoryOnly : text;
    }

    /// <summary>
    /// Formats the text of the server connection entry of forward mode.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The status of live sending</param>
    /// <returns>The text</returns>
    public static string FormatLiveSend(CompanionTexts texts, LiveSendStatus status)
    {
        return status.State switch
               {
                   LiveSendState.Connecting => texts.LiveSendConnecting,
                   LiveSendState.Connected => texts.LiveSendConnected,
                   LiveSendState.Retrying => status.Problem switch
                                             {
                                                 ReplaySendProblemKind.ServerUnreachable => texts.LiveSendUnreachableRetrying,
                                                 ReplaySendProblemKind.NoAnswer => texts.LiveSendNoAnswerRetrying,
                                                 ReplaySendProblemKind.ConnectionLost => texts.LiveSendConnectionLostRetrying,
                                                 ReplaySendProblemKind.InvalidReply => texts.LiveSendInvalidReplyRetrying,
                                                 ReplaySendProblemKind.CompanionConnected => texts.LiveSendCompanionConnectedRetrying,
                                                 _ => texts.LiveSendNotConnected
                                             },
                   LiveSendState.Stopped => status.Problem switch
                                            {
                                                ReplaySendProblemKind.TokenRefused => texts.LiveSendTokenRefused,
                                                ReplaySendProblemKind.VersionNotSupported => texts.LiveSendVersionNotSupported,
                                                _ => texts.LiveSendNotConnected
                                            },
                   _ => texts.LiveSendNotConnected
               };
    }

    /// <summary>
    /// Formats the text of the catalog entry.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The status of the game catalog</param>
    /// <returns>The text</returns>
    public static string FormatCatalog(CompanionTexts texts, GameCatalogStatus status)
    {
        return status.Kind switch
               {
                   GameCatalogStatusKind.Reading => texts.CatalogReading,
                   GameCatalogStatusKind.Ready => string.Format(CultureInfo.InvariantCulture, texts.CatalogReadyFormat, status.EntryCount),
                   GameCatalogStatusKind.NoGame => texts.CatalogNoGame,
                   GameCatalogStatusKind.Failed => texts.CatalogFailed,
                   GameCatalogStatusKind.Checking => texts.CatalogChecking,
                   GameCatalogStatusKind.ExtractingNames => texts.CatalogExtractingNames,
                   GameCatalogStatusKind.ExtractingIcons => string.Format(CultureInfo.InvariantCulture, texts.CatalogExtractingIconsFormat, status.EntryCount),
                   GameCatalogStatusKind.IconsFailed => string.Format(CultureInfo.InvariantCulture, texts.CatalogIconsFailedFormat, status.EntryCount),
                   GameCatalogStatusKind.NotCached => string.Format(CultureInfo.InvariantCulture, texts.CatalogNotCachedFormat, status.EntryCount),
                   _ => throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "The status kind is not defined")
               };
    }

    /// <summary>
    /// Formats the text of the game entry.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The status of the game installation search</param>
    /// <returns>The text</returns>
    public static string FormatGame(CompanionTexts texts, GameInstallationStatus status)
    {
        var source = status.Source == GameInstallationSource.UbisoftConnect ? texts.GameSourceUbisoftConnect : texts.GameSourceSettingsFile;

        return status.Kind switch
               {
                   GameInstallationStatusKind.Searching => texts.GameSearching,
                   GameInstallationStatusKind.Found => string.Format(CultureInfo.InvariantCulture, texts.GameFoundFormat, status.Build ?? texts.GameBuildUnknown, source),
                   GameInstallationStatusKind.NotFound => texts.GameNotFound,
                   GameInstallationStatusKind.SettingsFolderNotFound => texts.GameSettingsFolderNotFound,
                   GameInstallationStatusKind.SettingsFolderNotFullPath => texts.GameSettingsFolderNotFullPath,
                   GameInstallationStatusKind.SettingsFolderNotInstallation => texts.GameSettingsFolderNotInstallation,
                   GameInstallationStatusKind.SettingsFileInvalid => texts.GameSettingsFileInvalid,
                   _ => throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "The status kind is not defined")
               };
    }

    /// <summary>
    /// Formats the reason of a replay problem.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="problem">The problem</param>
    /// <returns>The reason, or <see langword="null" /> if the problem kind is not a defined value</returns>
    public static string? FormatReplayReason(CompanionTexts texts, PipeReplayProblem problem)
    {
        return problem.Kind switch
               {
                   PipeReplayProblemKind.FileNotFound => texts.ReplayReasonFileNotFound,
                   PipeReplayProblemKind.NotARecording => texts.ReplayReasonNotARecording,
                   PipeReplayProblemKind.UnsupportedVersion => texts.ReplayReasonUnsupportedVersion,
                   PipeReplayProblemKind.InvalidLine => string.Format(CultureInfo.InvariantCulture, texts.ReplayReasonInvalidLineFormat, problem.LineNumber).TrimEnd(),
                   PipeReplayProblemKind.ReadFailed => texts.ReplayReasonReadFailed,
                   _ => null
               };
    }

    /// <summary>
    /// Formats the reason of a problem of sending a replay to a server.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="kind">The problem kind</param>
    /// <returns>The reason, or <see langword="null" /> if the problem kind is not a defined value</returns>
    public static string? FormatReplaySendReason(CompanionTexts texts, ReplaySendProblemKind kind)
    {
        return kind switch
               {
                   ReplaySendProblemKind.TokenFileCannotBeRead => texts.ReplaySendReasonTokenFileCannotBeRead,
                   ReplaySendProblemKind.TokenFileHoldsNoValidToken => texts.ReplaySendReasonTokenFileHoldsNoValidToken,
                   ReplaySendProblemKind.ServerUnreachable => texts.ReplaySendReasonServerUnreachable,
                   ReplaySendProblemKind.TokenRefused => texts.ReplaySendReasonTokenRefused,
                   ReplaySendProblemKind.CompanionConnected => texts.ReplaySendReasonCompanionConnected,
                   ReplaySendProblemKind.VersionNotSupported => texts.ReplaySendReasonVersionNotSupported,
                   ReplaySendProblemKind.NoAnswer => texts.ReplaySendReasonNoAnswer,
                   ReplaySendProblemKind.ConnectionLost => texts.ReplaySendReasonConnectionLost,
                   ReplaySendProblemKind.InvalidReply => texts.ReplaySendReasonInvalidReply,
                   ReplaySendProblemKind.LocalServerNotRunning => texts.ReplaySendReasonLocalServerNotRunning,
                   _ => null
               };
    }

    /// <summary>
    /// Removes the application name and the colon from the start of a tooltip text.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="toolTip">The tooltip text</param>
    /// <returns>The status text</returns>
    private static string WithoutToolTipPrefix(CompanionTexts texts, string toolTip)
    {
        return toolTip[(texts.ToolTip.Length + 2)..];
    }

    #endregion // Methods
}