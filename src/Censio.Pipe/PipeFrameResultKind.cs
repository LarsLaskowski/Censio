namespace Censio.Pipe;

/// <summary>
/// Names the outcomes of reading one frame.
/// </summary>
public enum PipeFrameResultKind
{
    /// <summary>
    /// A complete frame was read.
    /// </summary>
    Frame,

    /// <summary>
    /// The frame could not be read; the error says why.
    /// </summary>
    Error,

    /// <summary>
    /// The stream ended at a frame boundary or was already ended.
    /// </summary>
    EndOfStream
}