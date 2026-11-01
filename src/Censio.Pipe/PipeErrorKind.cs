namespace Censio.Pipe;

/// <summary>
/// Names the reasons a frame or a message could not be read.
/// </summary>
public enum PipeErrorKind
{
    /// <summary>
    /// The stream ended inside a length prefix or a frame.
    /// </summary>
    TruncatedFrame,

    /// <summary>
    /// The announced frame length is negative or above the limit.
    /// </summary>
    OversizedFrame,

    /// <summary>
    /// The frame holds no bytes, not even a type byte.
    /// </summary>
    EmptyFrame,

    /// <summary>
    /// The type byte names no known message type.
    /// </summary>
    UnknownType,

    /// <summary>
    /// The payload does not fit the layout of its message type.
    /// </summary>
    MalformedMessage,

    /// <summary>
    /// The pipe does not speak the supported protocol version.
    /// </summary>
    ProtocolMismatch,

    /// <summary>
    /// The stream failed while reading.
    /// </summary>
    ReadFailed
}