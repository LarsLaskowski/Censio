namespace Censio.Pipe;

/// <summary>
/// The protocol version announcement.
/// </summary>
public sealed class VersionMessage : PipeMessage
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionMessage" /> class.
    /// </summary>
    /// <param name="version">The announced protocol version</param>
    public VersionMessage(int version)
    {
        Version = version;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the announced protocol version.
    /// </summary>
    public int Version { get; }

    #endregion // Properties

    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType => PipeMessageType.Version;

    #endregion // PipeMessage
}