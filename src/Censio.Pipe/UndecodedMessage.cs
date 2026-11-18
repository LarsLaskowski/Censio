namespace Censio.Pipe;

/// <summary>
/// A known message type whose payload this reader does not decode.
/// </summary>
public sealed class UndecodedMessage : PipeMessage
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UndecodedMessage" /> class.
    /// </summary>
    /// <param name="messageType">The type of the undecoded message</param>
    public UndecodedMessage(PipeMessageType messageType)
    {
        MessageType = messageType;
    }

    #endregion // Constructors

    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType { get; }

    #endregion // PipeMessage
}