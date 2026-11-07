namespace Censio.Pipe;

/// <summary>
/// The kind of a line read from a pipe recording.
/// </summary>
public enum PipeRecordingLineKind
{
    #region Values

    /// <summary>
    /// A frame line.
    /// </summary>
    Frame,

    /// <summary>
    /// The end of the recording.
    /// </summary>
    End,

    /// <summary>
    /// A problem that ends the reading.
    /// </summary>
    Problem

    #endregion // Values
}