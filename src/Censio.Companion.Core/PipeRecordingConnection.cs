using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Records the reads of one connection for a <see cref="PipeRecordingSession" />.
/// </summary>
public sealed class PipeRecordingConnection
{
    #region Fields

    /// <summary>
    /// The session.
    /// </summary>
    private readonly PipeRecordingSession _session;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingConnection" /> class.
    /// </summary>
    /// <param name="session">The session</param>
    internal PipeRecordingConnection(PipeRecordingSession session)
    {
        _session = session;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets or sets the active recorder; only used under the lock of the session.
    /// </summary>
    internal PipeRecorder? Recorder { get; set; }

    /// <summary>
    /// Gets or sets the last read of a version frame; only used under the lock of the session.
    /// </summary>
    internal PipeReadResult? LastVersion { get; set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Records a read if the session is recording.
    /// </summary>
    /// <param name="read">The read</param>
    /// <param name="receiveTime">The time the read was received</param>
    public void Record(PipeReadResult read, DateTimeOffset receiveTime)
    {
        _session.Record(this, read, receiveTime);
    }

    #endregion // Methods
}