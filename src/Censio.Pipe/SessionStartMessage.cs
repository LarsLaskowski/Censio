namespace Censio.Pipe;

/// <summary>
/// The start of a game session.
/// </summary>
public sealed class SessionStartMessage : PipeMessage
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStartMessage" /> class.
    /// </summary>
    /// <param name="headline">The session headline</param>
    public SessionStartMessage(string headline)
    {
        Headline = headline;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the session headline.
    /// </summary>
    public string Headline { get; }

    #endregion // Properties

    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType => PipeMessageType.SessionStart;

    #endregion // PipeMessage
}