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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the status text for a replay status.
    /// </summary>
    /// <param name="status">The replay status</param>
    /// <returns>The status text</returns>
    public static string FormatReplayStatusText(PipeReplayStatus status)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the text of the last tick.
    /// </summary>
    /// <param name="tick">The summary of the last tick, or <c>null</c> if none was received</param>
    /// <param name="clock">The clock</param>
    /// <returns>The text</returns>
    public static string FormatLastTick(PipeTickSummary? tick, TimeProvider clock)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the tooltip of a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    /// <param name="clock">The clock</param>
    /// <returns>The tooltip text</returns>
    public static string FormatSnapshotToolTip(TraySnapshot snapshot, TimeProvider clock)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the time until the next local midnight.
    /// </summary>
    /// <param name="clock">The clock</param>
    /// <returns>The time, at least one second</returns>
    public static TimeSpan UntilNextLocalMidnight(TimeProvider clock)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the entries of the tray menu.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <param name="snapshot">The snapshot</param>
    /// <param name="clock">The clock</param>
    /// <returns>The entries in display order</returns>
    public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, TimeProvider clock)
    {
        throw new NotImplementedException();
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

    #endregion // Methods
}