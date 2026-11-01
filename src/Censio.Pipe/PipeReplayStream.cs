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

    /// <summary>
    /// The size of the line buffer: the longest line and a carriage return and line feed.
    /// </summary>
    private const int LineBufferLength = PipeRecordingFormat.MaxLineLength + 2;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The byte order mark of UTF-8.
    /// </summary>
    private static readonly byte[] _byteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// The longest single wait.
    /// </summary>
    private static readonly TimeSpan _maxWaitStep = TimeSpan.FromDays(1);

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

    /// <summary>
    /// The buffer of the bytes read from the recording, allocated with the first read.
    /// </summary>
    private byte[]? _buffer;

    /// <summary>
    /// The offset in <see cref="_buffer" /> of the first byte not yet consumed.
    /// </summary>
    private int _start;

    /// <summary>
    /// The offset in <see cref="_buffer" /> behind the last byte read.
    /// </summary>
    private int _end;

    /// <summary>
    /// The offset in <see cref="_buffer" /> of the line last read.
    /// </summary>
    private int _lineStart;

    /// <summary>
    /// The length in bytes of the line last read, its terminator excluded.
    /// </summary>
    private int _lineLength;

    /// <summary>
    /// The number of lines read so far.
    /// </summary>
    private long _lineNumber;

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
        try
        {
            if (_lineNumber == 0 && await ReadHeaderAsync(cancellationToken).ConfigureAwait(false) == false)
            {
                return false;
            }

            var status = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (status == PipeReplayLineStatus.End)
            {
                return EndReplay();
            }

            _lineNumber++;

            if (status == PipeReplayLineStatus.TooLong)
            {
                return Fail(PipeReplayProblemKind.InvalidLine, _lineNumber);
            }

            if (_lineLength == 0)
            {
                return await HasMoreBytesAsync(cancellationToken).ConfigureAwait(false)
                           ? Fail(PipeReplayProblemKind.InvalidLine, _lineNumber)
                           : EndReplay();
            }

            if (PipeRecordingFormat.TryParseFrameLine(_buffer.AsSpan(_lineStart, _lineLength), out var receiveTime, out var frame) == false)
            {
                return Fail(PipeReplayProblemKind.InvalidLine, _lineNumber);
            }

            await WaitAsync(receiveTime, cancellationToken).ConfigureAwait(false);

            _clock.SetUtcNow(receiveTime);
            _previousTime = receiveTime;
            _pending = new byte[PrefixLength + frame.Length];
            _pendingOffset = 0;
            BinaryPrimitives.WriteInt32LittleEndian(_pending, frame.Length);
            frame.CopyTo(_pending, PrefixLength);

            return true;
        }
        catch (IOException)
        {
            return Fail(PipeReplayProblemKind.ReadFailed, null);
        }
    }

    /// <summary>
    /// Reads and checks the header line.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns><see langword="true" /> when the header is valid</returns>
    private async ValueTask<bool> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var status = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

        _lineNumber++;

        if (status == PipeReplayLineStatus.TooLong)
        {
            return Fail(PipeReplayProblemKind.NotARecording, null);
        }

        var line = _buffer.AsSpan(_lineStart, status == PipeReplayLineStatus.End ? 0 : _lineLength);

        if (line.StartsWith(_byteOrderMark))
        {
            line = line[_byteOrderMark.Length..];
        }

        var problem = PipeRecordingFormat.CheckHeader(line);

        return problem is null || Fail(problem.Value, null);
    }

    /// <summary>
    /// Checks whether the recording holds a byte that is not consumed yet.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns><see langword="true" /> when at least one byte follows</returns>
    private async ValueTask<bool> HasMoreBytesAsync(CancellationToken cancellationToken)
    {
        if (_start < _end)
        {
            return true;
        }

        _start = 0;
        _end = 0;

        var read = await _recording.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

        _end = read;

        return read > 0;
    }

    /// <summary>
    /// Reads the next line into the buffer without reading past its end more than the buffer holds.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns>The status of the read; the line is at <see cref="_lineStart" /> and <see cref="_lineLength" /></returns>
    private async ValueTask<PipeReplayLineStatus> ReadLineAsync(CancellationToken cancellationToken)
    {
        _buffer ??= new byte[LineBufferLength];
        _lineStart = _start;
        _lineLength = 0;

        var scanned = 0;

        while (true)
        {
            var available = _buffer.AsSpan(_start, _end - _start);
            var index = available[scanned..].IndexOf((byte)'\n');

            if (index >= 0)
            {
                var length = scanned + index;

                _lineStart = _start;
                _lineLength = length > 0 && available[length - 1] == (byte)'\r' ? length - 1 : length;
                _start += length + 1;

                return _lineLength > PipeRecordingFormat.MaxLineLength ? PipeReplayLineStatus.TooLong : PipeReplayLineStatus.Line;
            }

            scanned = available.Length;

            if (available.Length >= LineBufferLength)
            {
                return PipeReplayLineStatus.TooLong;
            }

            if (_start > 0)
            {
                available.CopyTo(_buffer);
                _start = 0;
                _end = available.Length;
            }

            var read = await _recording.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                _lineStart = _start;
                _lineLength = _end - _start;
                _start = _end;

                if (_lineLength == 0)
                {
                    return PipeReplayLineStatus.End;
                }

                return _lineLength > PipeRecordingFormat.MaxLineLength ? PipeReplayLineStatus.TooLong : PipeReplayLineStatus.Line;
            }

            _end += read;
        }
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

    /// <summary>
    /// Ends the replay with a problem.
    /// </summary>
    /// <param name="kind">The kind of the problem</param>
    /// <param name="lineNumber">The number of the line, if the problem belongs to one</param>
    /// <returns><see langword="false" />, as no frame is pending</returns>
    private bool Fail(PipeReplayProblemKind kind, long? lineNumber)
    {
        Problem = new PipeReplayProblem
                  {
                      Kind = kind,
                      LineNumber = lineNumber
                  };

        return EndReplay();
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