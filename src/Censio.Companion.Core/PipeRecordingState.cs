namespace Censio.Companion.Core;

/// <summary>
/// The state of the recording of the statistics pipe.
/// </summary>
public enum PipeRecordingState
{
    /// <summary>
    /// Nothing is recorded.
    /// </summary>
    Off,

    /// <summary>
    /// The pipe is recorded.
    /// </summary>
    Recording,

    /// <summary>
    /// The recording failed.
    /// </summary>
    Failed
}