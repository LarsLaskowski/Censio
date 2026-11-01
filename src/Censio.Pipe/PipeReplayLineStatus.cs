namespace Censio.Pipe;

/// <summary>
/// The result of reading a line of a recording.
/// </summary>
internal enum PipeReplayLineStatus
{
    /// <summary>
    /// A line was read.
    /// </summary>
    Line,

    /// <summary>
    /// The recording has no more bytes.
    /// </summary>
    End,

    /// <summary>
    /// The line is longer than the longest line.
    /// </summary>
    TooLong
}