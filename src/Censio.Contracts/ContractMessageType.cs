namespace Censio.Contracts;

/// <summary>
/// The type of a contract message.
/// </summary>
public enum ContractMessageType
{
    #region Values

    /// <summary>
    /// The hello of the Companion.
    /// </summary>
    Hello,

    /// <summary>
    /// The server accepts the hello.
    /// </summary>
    HelloAccepted,

    /// <summary>
    /// The server refuses the hello.
    /// </summary>
    HelloRefused,

    /// <summary>
    /// The upload of the game catalog.
    /// </summary>
    CatalogUpload,

    /// <summary>
    /// The image of a game icon.
    /// </summary>
    IconImage,

    /// <summary>
    /// A closed tick of the pipe.
    /// </summary>
    Tick,

    /// <summary>
    /// The start of a game session.
    /// </summary>
    SessionStart,

    /// <summary>
    /// The end of a game session.
    /// </summary>
    SessionEnd,

    /// <summary>
    /// The state of the pipe connection.
    /// </summary>
    Status,

    /// <summary>
    /// The server's answer to a message.
    /// </summary>
    Acknowledgement

    #endregion // Values
}