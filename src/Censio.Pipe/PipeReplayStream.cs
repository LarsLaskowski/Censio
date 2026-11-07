using System.Buffers.Binary;

namespace Censio.Pipe;

/// <summary>
/// A read-only stream that replays a pipe recording as the frames of the statistics pipe.
/// </summary>
public sealed class PipeReplayStream : Stream
{
    #region Constants

    /// <summary>
    /// The size of the length prefix of a frame in bytes.
    /// </summary>
    private const int PrefixLength = 4;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The longest single wait.
    /// </summary>
    private static readonly TimeSpan _maxWaitStep = TimeSpan.FromDays(1);

    /// <summary>
    /// The recording to replay.
    /// </summary>
    private readonly Stream _recording;

    /// <summary>
    /// The reader of the lines of the recording.
    /// </summary>
    private readonly PipeRecordingReader _reader;

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

    /// <summary>
    /// The receive time of the frame served last, if any.
    /// </summary>
    private DateTimeOffset? _previousTime;

    /// <summary>
    /// The length prefix and the bytes of the current frame, or <see langword="null" /> between frames.
    /// </summary>
    private byte[]? _pending;

    /// <summary>
    /// The offset in <see cref="_pending" /> of the first byte not yet served.
    /// </summary>
    private int _pendingOffset;

    /// <summary>
    /// Indicates whether the replay has ended.
    /// </summary>
    private bool _finished;

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
        _reader = new PipeRecordingReader(recording);
        _speed = speed;
        _clock = clock;
        _waitProvider = waitProvider;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the problem that ended the replay, if any.
    /// </summary>
    public PipeReplayProblem? Problem { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Loads the next frame of the recording, waits for its pause and sets the clock to its time.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the wait and the reads</param>
    /// <returns><see langword="true" /> when a frame is pending; <see langword="false" /> when the replay has ended</returns>
    private async ValueTask<bool> LoadFrameAsync(CancellationToken cancellationToken)
    {
        var line = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (line.Kind == PipeRecordingLineKind.End)
        {
            return EndReplay();
        }

        if (line.Kind == PipeRecordingLineKind.Problem)
        {
            Problem = line.Problem;

            return EndReplay();
        }

        await WaitAsync(line.ReceiveTime, cancellationToken).ConfigureAwait(false);

        _clock.SetUtcNow(line.ReceiveTime);
        _previousTime = line.ReceiveTime;
        _pending = new byte[PrefixLength + line.Frame.Length];
        _pendingOffset = 0;
        BinaryPrimitives.WriteInt32LittleEndian(_pending, line.Frame.Length);
        line.Frame.CopyTo(_pending, PrefixLength);

        return true;
    }

    /// <summary>
    /// Waits the scaled pause between the previous frame and a frame, in steps of at most one day.
    /// </summary>
    /// <param name="receiveTime">The receive time of the frame</param>
    /// <param name="cancellationToken">The token that cancels the wait</param>
    /// <returns>The task</returns>
    private async ValueTask WaitAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        if (_previousTime is not { } previous)
        {
            return;
        }

        var remaining = _speed.Scale(receiveTime - previous);

        while (remaining > TimeSpan.Zero)
        {
            var step = remaining > _maxWaitStep ? _maxWaitStep : remaining;

            await Task.Delay(step, _waitProvider, cancellationToken).ConfigureAwait(false);

            remaining -= step;
        }
    }

    /// <summary>
    /// Ends the replay without a problem.
    /// </summary>
    /// <returns><see langword="false" />, as no frame is pending</returns>
    private bool EndReplay()
    {
        _finished = true;

        return false;
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        if (_pending is null && (_finished || await LoadFrameAsync(cancellationToken).ConfigureAwait(false) == false))
        {
            return 0;
        }

        var count = Math.Min(buffer.Length, _pending!.Length - _pendingOffset);

        _pending.AsMemory(_pendingOffset, count).CopyTo(buffer);
        _pendingOffset += count;

        if (_pendingOffset == _pending.Length)
        {
            _pending = null;
        }

        return count;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recording.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion // Stream
}