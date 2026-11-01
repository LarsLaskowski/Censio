namespace Censio.Companion.Core;

/// <summary>
/// The mode the Companion runs in, chosen by its command line.
/// </summary>
public enum CompanionMode
{
    /// <summary>
    /// Reads the pipe without recording.
    /// </summary>
    Normal,

    /// <summary>
    /// Reads the pipe and records it.
    /// </summary>
    Record,

    /// <summary>
    /// Replays a recording instead of reading the pipe.
    /// </summary>
    Replay,

    /// <summary>
    /// The command line is invalid.
    /// </summary>
    Invalid
}