namespace Censio.Pipe;

/// <summary>
/// A line read from a pipe recording.
/// </summary>
public sealed class PipeRecordingLine
{
    #region Properties

    /// <summary>
    /// Gets the kind of the line.
    /// </summary>
    public required PipeRecordingLineKind Kind { get; init; }

    /// <summary>
    /// Gets the one-based number of the line in the recording.
    /// </summary>
    public long LineNumber { get; init; }

    /// <summary>
    /// Gets the receive time of the frame in UTC.
    /// </summary>
    public DateTimeOffset ReceiveTime { get; init; }

    /// <summary>
    /// Gets the decoded frame.
    /// </summary>
    public byte[] Frame { get; init; } = [];

    /// <summary>
    /// Gets the problem that ends the reading, if any.
    /// </summary>
    public PipeReplayProblem? Problem { get; init; }

    #endregion // Properties
}