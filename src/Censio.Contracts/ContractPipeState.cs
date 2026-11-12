namespace Censio.Contracts;

/// <summary>
/// The state of the Companion's pipe connection as sent to the server.
/// </summary>
public enum ContractPipeState
{
    #region Values

    /// <summary>
    /// Waiting for the game.
    /// </summary>
    WaitingForGame,

    /// <summary>
    /// Connected to the pipe.
    /// </summary>
    Connected,

    /// <summary>
    /// Receiving data.
    /// </summary>
    Receiving,

    /// <summary>
    /// The pipe is busy.
    /// </summary>
    PipeBusy,

    /// <summary>
    /// The pipe protocol does not match.
    /// </summary>
    ProtocolMismatch,

    /// <summary>
    /// The pipe failed.
    /// </summary>
    Error

    #endregion // Values
}