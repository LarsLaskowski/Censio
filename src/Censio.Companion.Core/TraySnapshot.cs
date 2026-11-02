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
    /// <param name="status">The connection status</param>
    /// <param name="recording">The recording state</param>
    /// <param name="lastTick">The last tick</param>
    /// <returns>The snapshot</returns>
    public static TraySnapshot ForConnection(PipeConnectionStatus status, PipeRecordingState recording, PipeTickSummary? lastTick)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the snapshot of a replay.
    /// </summary>
    /// <param name="status">The replay status</param>
    /// <param name="lastTick">The last tick</param>
    /// <returns>The snapshot</returns>
    public static TraySnapshot ForReplay(PipeReplayStatus status, PipeTickSummary? lastTick)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the snapshot of an invalid command line.
    /// </summary>
    /// <returns>The snapshot</returns>
    public static TraySnapshot ForInvalidCommandLine()
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}