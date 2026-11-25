using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// What the tray shows at one moment.
/// </summary>
public sealed record TraySnapshot
{
    #region Properties

    /// <summary>
    /// Gets the status text.
    /// </summary>
    public required string StatusText { get; init; }

    /// <summary>
    /// Gets the look of the tray icon.
    /// </summary>
    public required TrayIconLook Look { get; init; }

    /// <summary>
    /// Gets the summary of the last tick, or <c>null</c> if none was received.
    /// </summary>
    public PipeTickSummary? LastTick { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recording can be started or stopped.
    /// </summary>
    public bool CanRecord { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recording is running.
    /// </summary>
    public bool IsRecording { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the snapshot of a live connection.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The connection status</param>
    /// <param name="recording">The recording state</param>
    /// <param name="lastTick">The last tick</param>
    /// <returns>The snapshot</returns>
    public static TraySnapshot ForConnection(CompanionTexts texts, PipeConnectionStatus status, PipeRecordingState recording, PipeTickSummary? lastTick)
    {
        return new TraySnapshot
               {
                   StatusText = TrayMenu.FormatStatusText(texts, status.State, recording),
                   Look = LookOf(status.State),
                   LastTick = lastTick,
                   CanRecord = true,
                   IsRecording = recording == PipeRecordingState.Recording
               };
    }

    /// <summary>
    /// Creates the snapshot of a replay.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="status">The replay status</param>
    /// <param name="lastTick">The last tick</param>
    /// <returns>The snapshot</returns>
    public static TraySnapshot ForReplay(CompanionTexts texts, PipeReplayStatus status, PipeTickSummary? lastTick)
    {
        TrayIconLook look;

        if (status.Problem is not null || status.SendProblem is not null)
        {
            look = TrayIconLook.Problem;
        }
        else if (status.IsFinished || status.IsConnecting)
        {
            look = TrayIconLook.Waiting;
        }
        else
        {
            look = LookOf(status.State);
        }

        return new TraySnapshot
               {
                   StatusText = TrayMenu.FormatReplayStatusText(texts, status),
                   Look = look,
                   LastTick = lastTick,
                   CanRecord = false,
                   IsRecording = false
               };
    }

    /// <summary>
    /// Creates the snapshot of an invalid command line.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <returns>The snapshot</returns>
    public static TraySnapshot ForInvalidCommandLine(CompanionTexts texts)
    {
        return new TraySnapshot
               {
                   StatusText = texts.InvalidCommandLineStatusText,
                   Look = TrayIconLook.Problem,
                   CanRecord = false,
                   IsRecording = false
               };
    }

    /// <summary>
    /// Gets the look of the tray icon while a notice may be pending: a problem stays, else a pending notice shows.
    /// </summary>
    /// <param name="notice">The pending notice, or <c>null</c> if none is pending</param>
    /// <returns>The look</returns>
    public TrayIconLook LookWith(ProfileNotice? notice)
    {
        return notice is null || Look == TrayIconLook.Problem ? Look : TrayIconLook.Notice;
    }

    /// <summary>
    /// Gets the look of a connection state.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The look</returns>
    private static TrayIconLook LookOf(PipeConnectionState state)
    {
        return state switch
               {
                   PipeConnectionState.Receiving => TrayIconLook.Receiving,
                   PipeConnectionState.ProtocolMismatch or PipeConnectionState.Error => TrayIconLook.Problem,
                   _ => TrayIconLook.Waiting
               };
    }

    #endregion // Methods
}