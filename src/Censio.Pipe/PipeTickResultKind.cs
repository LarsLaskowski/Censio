namespace Censio.Pipe;

/// <summary>
/// The kind of a <see cref="PipeTickResult" />.
/// </summary>
public enum PipeTickResultKind
{
    /// <summary>
    /// A closed tick.
    /// </summary>
    Tick,

    /// <summary>
    /// A session start with its headline.
    /// </summary>
    SessionStart,

    /// <summary>
    /// A session end.
    /// </summary>
    SessionEnd,

    /// <summary>
    /// A session end that no island frame or session start preceded.
    /// </summary>
    RepeatedSessionEnd,

    /// <summary>
    /// A version message.
    /// </summary>
    Version,

    /// <summary>
    /// A reader error.
    /// </summary>
    Error,

    /// <summary>
    /// An island frame that exceeded a tick limit.
    /// </summary>
    TickLimit,

    /// <summary>
    /// The end of the stream.
    /// </summary>
    EndOfStream
}