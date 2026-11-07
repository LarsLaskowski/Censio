namespace Censio.Pipe;

/// <summary>
/// Reads the lines of a pipe recording one by one with the line rules of the format.
/// </summary>
public sealed class PipeRecordingReader
{
    #region Constants

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
    /// The recording to read.
    /// </summary>
    private readonly Stream _recording;

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
    /// The line that ended the reading, given again by every later read, or <see langword="null" /> while the reading goes on.
    /// </summary>
    private PipeRecordingLine? _final;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingReader"/> class.
    /// </summary>
    /// <param name="recording">The recording to read</param>
    public PipeRecordingReader(Stream recording)
    {
        _recording = recording;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the start time of the header once the header is read, or <see langword="null" /> when it is missing or unusable.
    /// </summary>
    public DateTimeOffset? Started { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Reads the next line; the first call reads and checks the header.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns>The frame line, the end of the recording or the problem that ends the reading</returns>
    public async ValueTask<PipeRecordingLine> ReadAsync(CancellationToken cancellationToken)
    {
        if (_final is not null)
        {
            return _final;
        }

        try
        {
            if (_lineNumber == 0 && await ReadHeaderAsync(cancellationToken).ConfigureAwait(false) is { } headerProblem)
            {
                return Fail(headerProblem, null);
            }

            return await ReadFrameLineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(PipeReplayProblemKind.ReadFailed, null);
        }
    }

    /// <summary>
    /// Reads the next line after the header and makes a line of it.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns>The frame line, the end of the recording or the problem</returns>
    private async ValueTask<PipeRecordingLine> ReadFrameLineAsync(CancellationToken cancellationToken)
    {
        var status = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

        if (status == PipeReplayLineStatus.End)
        {
            return End();
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
                       : End();
        }

        if (PipeRecordingFormat.TryParseFrameLine(_buffer.AsSpan(_lineStart, _lineLength), out var receiveTime, out var frame) == false)
        {
            return Fail(PipeReplayProblemKind.InvalidLine, _lineNumber);
        }

        return new PipeRecordingLine
               {
                   Kind = PipeRecordingLineKind.Frame,
                   LineNumber = _lineNumber,
                   ReceiveTime = receiveTime,
                   Frame = frame
               };
    }

    /// <summary>
    /// Reads and checks the header line and takes its start time.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns>The problem, or <see langword="null" /> when the header is valid</returns>
    private async ValueTask<PipeReplayProblemKind?> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var status = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

        _lineNumber++;

        if (status == PipeReplayLineStatus.TooLong)
        {
            return PipeReplayProblemKind.NotARecording;
        }

        var line = _buffer.AsSpan(_lineStart, status == PipeReplayLineStatus.End ? 0 : _lineLength);

        if (line.StartsWith(_byteOrderMark))
        {
            line = line[_byteOrderMark.Length..];
        }

        var problem = PipeRecordingFormat.CheckHeader(line);

        if (problem is null)
        {
            Started = PipeRecordingFormat.ReadStarted(line);
        }

        return problem;
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
            var availableLength = _end - _start;
            var index = _buffer.AsSpan(_start, availableLength)[scanned..].IndexOf((byte)'\n');

            if (index >= 0)
            {
                return CompleteLine(scanned + index);
            }

            scanned = availableLength;

            if (availableLength >= LineBufferLength)
            {
                return PipeReplayLineStatus.TooLong;
            }

            CompactBuffer();

            var read = await _recording.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return CompleteFinalLine();
            }

            _end += read;
        }
    }

    /// <summary>
    /// Completes the line that ends with a line feed at the given offset behind <see cref="_start" />.
    /// </summary>
    /// <param name="length">The number of bytes before the line feed</param>
    /// <returns>The status of the line</returns>
    private PipeReplayLineStatus CompleteLine(int length)
    {
        var hasCarriageReturn = length > 0 && _buffer![_start + length - 1] == (byte)'\r';

        _lineStart = _start;
        _lineLength = hasCarriageReturn ? length - 1 : length;
        _start += length + 1;

        return _lineLength > PipeRecordingFormat.MaxLineLength ? PipeReplayLineStatus.TooLong : PipeReplayLineStatus.Line;
    }

    /// <summary>
    /// Completes the last line of a recording that ends without a line feed.
    /// </summary>
    /// <returns>The status of the line</returns>
    private PipeReplayLineStatus CompleteFinalLine()
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

    /// <summary>
    /// Moves the unconsumed bytes to the start of the buffer.
    /// </summary>
    private void CompactBuffer()
    {
        if (_start > 0)
        {
            var length = _end - _start;

            _buffer.AsSpan(_start, length).CopyTo(_buffer);
            _start = 0;
            _end = length;
        }
    }

    /// <summary>
    /// Ends the reading at the end of the recording.
    /// </summary>
    /// <returns>The end line</returns>
    private PipeRecordingLine End()
    {
        _final = new PipeRecordingLine
                 {
                     Kind = PipeRecordingLineKind.End
                 };

        return _final;
    }

    /// <summary>
    /// Ends the reading with a problem.
    /// </summary>
    /// <param name="kind">The kind of the problem</param>
    /// <param name="lineNumber">The number of the line, if the problem belongs to one</param>
    /// <returns>The problem line</returns>
    private PipeRecordingLine Fail(PipeReplayProblemKind kind, long? lineNumber)
    {
        _final = new PipeRecordingLine
                 {
                     Kind = PipeRecordingLineKind.Problem,
                     Problem = new PipeReplayProblem
                               {
                                   Kind = kind,
                                   LineNumber = lineNumber
                               }
                 };

        return _final;
    }

    #endregion // Methods
}