namespace Censio.Companion.Core;

/// <summary>
/// The outcome kind of one attempt to connect to the statistics pipe.
/// </summary>
public enum PipeConnectResultKind
{
    /// <summary>
    /// The pipe is connected and a stream is available.
    /// </summary>
    Connected,

    /// <summary>
    /// The pipe does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The pipe exists but another reader holds it.
    /// </summary>
    Busy,

    /// <summary>
    /// The attempt failed for another reason.
    /// </summary>
    Failed
}