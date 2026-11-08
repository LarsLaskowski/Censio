namespace Censio.Pipe;

/// <summary>
/// The outcome of anonymising a recording.
/// </summary>
public enum PipeAnonymiseResultKind
{
    #region Values

    /// <summary>
    /// The recording was anonymised.
    /// </summary>
    Anonymised,

    /// <summary>
    /// The recording is refused for a problem a replay reports too.
    /// </summary>
    RecordingProblem,

    /// <summary>
    /// A frame cannot be anonymised.
    /// </summary>
    FrameNotAnonymisable

    #endregion // Values
}