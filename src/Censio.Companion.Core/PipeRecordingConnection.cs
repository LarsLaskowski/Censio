using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Records the reads of one connection for a <see cref="PipeRecordingSession" />.
/// </summary>
public sealed class PipeRecordingConnection
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingConnection" /> class.
    /// </summary>
    internal PipeRecordingConnection()
    {
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Records a read if the session is recording.
    /// </summary>
    /// <param name="read">The read</param>
    /// <param name="receiveTime">The time the read was received</param>
    public void Record(PipeReadResult read, DateTimeOffset receiveTime)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}