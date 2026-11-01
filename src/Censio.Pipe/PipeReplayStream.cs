namespace Censio.Pipe;

/// <summary>
/// A read-only stream that replays a pipe recording as the frames of the statistics pipe.
/// </summary>
public sealed class PipeReplayStream : Stream
{
    #region Fields

    /// <summary>
    /// The recording to replay.
    /// </summary>
    private readonly Stream _recording;

    /// <summary>
    /// The speed to replay at.
    /// </summary>
    private readonly PipeReplaySpeed _speed;

    /// <summary>
    /// The clock set to the recorded receive times.
    /// </summary>
    private readonly PipeReplayClock _clock;

    /// <summary>
    /// The time provider the pauses are waited on.
    /// </summary>
    private readonly TimeProvider _waitProvider;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayStream"/> class.
    /// </summary>
    /// <param name="recording">The recording to replay</param>
    /// <param name="speed">The speed to replay at</param>
    /// <param name="clock">The clock to set to the recorded receive times</param>
    /// <param name="waitProvider">The time provider to wait on</param>
    public PipeReplayStream(Stream recording, PipeReplaySpeed speed, PipeReplayClock clock, TimeProvider waitProvider)
    {
        _recording = recording;
        _speed = speed;
        _clock = clock;
        _waitProvider = waitProvider;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the problem that ended the replay, if any.
    /// </summary>
    public PipeReplayProblem? Problem { get; }

    #endregion // Properties

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => throw new NotImplementedException();

    /// <inheritdoc />
    public override bool CanSeek => throw new NotImplementedException();

    /// <inheritdoc />
    public override bool CanWrite => throw new NotImplementedException();

    /// <inheritdoc />
    public override long Length => throw new NotImplementedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    #endregion // Stream
}