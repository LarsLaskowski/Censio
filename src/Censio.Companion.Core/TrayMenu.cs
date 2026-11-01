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
            var reason = problem.Kind switch
                         {
                             PipeReplayProblemKind.FileNotFound => "file not found",
                             PipeReplayProblemKind.NotARecording => "not a recording",
                             PipeReplayProblemKind.UnsupportedVersion => "unsupported version",
                             PipeReplayProblemKind.InvalidLine => string.Create(CultureInfo.InvariantCulture, $"invalid line {problem.LineNumber}").TrimEnd(),
                             PipeReplayProblemKind.ReadFailed => "read failed",
                             _ => throw new ArgumentOutOfRangeException(nameof(status), problem.Kind, "The problem kind is not defined")
                         };

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
    /// Creates the entries of the tray menu.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <returns>The entries in display order</returns>
    public static IReadOnlyList<TrayMenuEntry> Create(string version)
    {
        return
        [
            new TrayMenuEntry(TrayMenuEntryKind.Version, FormatVersion(version), false),
            new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
            new TrayMenuEntry(TrayMenuEntryKind.Exit, ExitText, true)
        ];
    }

    #endregion // Methods
}