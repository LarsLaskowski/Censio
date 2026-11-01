namespace Censio.Pipe;

/// <summary>
/// Why a tick was closed.
/// </summary>
public enum PipeTickCloseReason
{
    /// <summary>
    /// An island frame carried another game time.
    /// </summary>
    GameTimeChanged,

    /// <summary>
    /// No island frame arrived within the quiet period.
    /// </summary>
    QuietPeriod,

    /// <summary>
    /// An island frame repeated an island key of the open tick.
    /// </summary>
    IslandRepeated,

    /// <summary>
    /// A session start or session end arrived.
    /// </summary>
    SessionEvent,

    /// <summary>
    /// The stream ended or ended with an error.
    /// </summary>
    StreamEnded
}