namespace Censio.Pipe;

/// <summary>
/// The kind of a <see cref="PipeReadResult" />.
/// </summary>
public enum PipeReadResultKind
{
    /// <summary>
    /// A decoded message.
    /// </summary>
    Message,

    /// <summary>
    /// A protocol error.
    /// </summary>
    Error,

    /// <summary>
    /// The end of the stream.
    /// </summary>
    EndOfStream
}