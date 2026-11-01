namespace Censio.Companion.Core;

/// <summary>
/// The state of the connection to the statistics pipe.
/// </summary>
public enum PipeConnectionState
{
    /// <summary>
    /// The pipe does not exist yet, the game is not running.
    /// </summary>
    WaitingForGame,

    /// <summary>
    /// The pipe is open, no data has arrived yet.
    /// </summary>
    Connected,

    /// <summary>
    /// Data is arriving from the pipe.
    /// </summary>
    Receiving,

    /// <summary>
    /// Another reader holds the pipe.
    /// </summary>
    PipeBusy,

    /// <summary>
    /// The pipe sent a protocol version this application does not know.
    /// </summary>
    ProtocolMismatch,

    /// <summary>
    /// The pipe failed for another reason.
    /// </summary>
    Error
}