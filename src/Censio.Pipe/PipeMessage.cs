namespace Censio.Pipe;

/// <summary>
/// The base class of all decoded pipe messages.
/// </summary>
public abstract class PipeMessage
{
    #region Properties

    /// <summary>
    /// Gets the message type.
    /// </summary>
    public abstract PipeMessageType MessageType { get; }

    #endregion // Properties
}