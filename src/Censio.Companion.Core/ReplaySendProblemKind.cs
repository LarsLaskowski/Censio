namespace Censio.Companion.Core;

/// <summary>
/// The kind of problem that stops sending a replay to a server.
/// </summary>
public enum ReplaySendProblemKind
{
    /// <summary>
    /// The token file cannot be read.
    /// </summary>
    TokenFileCannotBeRead,

    /// <summary>
    /// The token file holds no valid token.
    /// </summary>
    TokenFileHoldsNoValidToken,

    /// <summary>
    /// The server cannot be reached.
    /// </summary>
    ServerUnreachable,

    /// <summary>
    /// The server refused the token.
    /// </summary>
    TokenRefused,

    /// <summary>
    /// Another Companion is connected to the server.
    /// </summary>
    CompanionConnected,

    /// <summary>
    /// The server does not support the contract version.
    /// </summary>
    VersionNotSupported,

    /// <summary>
    /// The server did not answer in time.
    /// </summary>
    NoAnswer,

    /// <summary>
    /// The connection to the server was lost.
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// The server sent a reply that is not valid.
    /// </summary>
    InvalidReply
}