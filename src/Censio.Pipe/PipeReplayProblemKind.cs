namespace Censio.Pipe;

/// <summary>
/// The kind of problem found in a pipe recording.
/// </summary>
public enum PipeReplayProblemKind
{
    #region Values

    /// <summary>
    /// The recording file does not exist.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// The file is not a pipe recording.
    /// </summary>
    NotARecording,

    /// <summary>
    /// The recording has a format version this build does not support.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// A line of the recording is invalid.
    /// </summary>
    InvalidLine,

    /// <summary>
    /// Reading the recording failed.
    /// </summary>
    ReadFailed

    #endregion // Values
}