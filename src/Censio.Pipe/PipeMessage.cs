namespace Censio.Pipe;

/// <summary>
/// The base class of all decoded pipe messages.
/// </summary>
public abstract class PipeMessage
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeMessage"/> class.
    /// </summary>
    protected PipeMessage()
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the message type.
    /// </summary>
    public abstract PipeMessageType MessageType { get; }

    #endregion // Properties
}