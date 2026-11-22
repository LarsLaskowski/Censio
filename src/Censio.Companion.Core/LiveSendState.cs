namespace Censio.Companion.Core;

/// <summary>
/// The state of live sending.
/// </summary>
public enum LiveSendState
{
    /// <summary>
    /// The connection to the server is being opened.
    /// </summary>
    Connecting,

    /// <summary>
    /// The server accepted the connection.
    /// </summary>
    Connected,

    /// <summary>
    /// The connection failed and is retried.
    /// </summary>
    Retrying,

    /// <summary>
    /// Live sending ended and is not retried.
    /// </summary>
    Stopped
}