namespace Censio.Pipe;

/// <summary>
/// The end of a game session.
/// </summary>
public sealed class SessionEndMessage : PipeMessage
{
    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType => PipeMessageType.SessionEnd;

    #endregion // PipeMessage
}