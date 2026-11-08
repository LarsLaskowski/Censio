using System.Text;

namespace Censio.Pipe;

/// <summary>
/// Rewrites a pipe recording so that it holds no names of the player and no exact amounts.
/// </summary>
public sealed class PipeRecordingAnonymiser
{
    #region Constants

    /// <summary>
    /// The smallest shift of the receive times in days.
    /// </summary>
    public const int MinShiftDays = 30;

    /// <summary>
    /// The largest additional shift of the receive times in days.
    /// </summary>
    public const int MaxShiftDays = 365;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start a header gets when neither a frame nor the input's header gives one.
    /// </summary>
    private static readonly DateTimeOffset _defaultStarted = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The random source.
    /// </summary>
    private readonly IPipeAnonymiserRandom _random;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingAnonymiser"/> class.
    /// </summary>
    /// <param name="random">The random source</param>
    public PipeRecordingAnonymiser(IPipeAnonymiserRandom random)
    {
        _random = random;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Anonymises a recording.
    /// </summary>
    /// <param name="recording">The recording to read; it must support seeking</param>
    /// <param name="output">The stream the anonymised recording is written to</param>
    /// <param name="cancellationToken">The token that cancels the work</param>
    /// <returns>The result</returns>
    public async Task<PipeAnonymiseResult> AnonymiseAsync(Stream recording, Stream output, CancellationToken cancellationToken)
    {
        if (recording.CanSeek == false)
        {
            return ReadFailed();
        }

        var offset = DrawOffset();
        var names = new PipeAnonymousNames();
        var frames = new PipeFrameAnonymiser(names, _random);
        var collected = await CollectAsync(recording, offset, frames, cancellationToken).ConfigureAwait(false);

        if (collected.Failure is not null)
        {
            return collected.Failure;
        }

        names.Complete();
        frames.Reset();

        if (TryRewind(recording) == false)
        {
            return ReadFailed();
        }

        var started = collected.FirstTime ?? collected.Started ?? _defaultStarted;

        await WriteAsync(output, PipeRecordingFormat.FormatHeader(started - offset), cancellationToken).ConfigureAwait(false);

        return await RewriteAsync(recording, output, offset, frames, collected.Count, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the recording once: checks every line and collects the names.
    /// </summary>
    /// <param name="recording">The recording</param>
    /// <param name="offset">The shift of the receive times</param>
    /// <param name="frames">The frame anonymiser</param>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns>The failure, if any, the number of frames, the first receive time and the start of the header</returns>
    private static async Task<(PipeAnonymiseResult? Failure, long Count, DateTimeOffset? FirstTime, DateTimeOffset? Started)> CollectAsync(Stream recording, TimeSpan offset, PipeFrameAnonymiser frames, CancellationToken cancellationToken)
    {
        var reader = new PipeRecordingReader(recording);
        var count = 0L;
        DateTimeOffset? firstTime = null;

        while (true)
        {
            var line = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (line.Kind == PipeRecordingLineKind.Problem)
            {
                return (new PipeAnonymiseResult
                        {
                            Kind = PipeAnonymiseResultKind.RecordingProblem,
                            Problem = line.Problem
                        }, count, firstTime, null);
            }

            if (line.Kind == PipeRecordingLineKind.End)
            {
                return (null, count, firstTime, reader.Started);
            }

            if (line.ReceiveTime.UtcTicks < offset.Ticks)
            {
                return (InvalidLine(line.LineNumber), count, firstTime, null);
            }

            if (frames.TryCollect(line.Frame) == false)
            {
                return (new PipeAnonymiseResult
                        {
                            Kind = PipeAnonymiseResultKind.FrameNotAnonymisable,
                            LineNumber = line.LineNumber
                        }, count, firstTime, null);
            }

            count++;
            firstTime ??= line.ReceiveTime;
        }
    }

    /// <summary>
    /// Reads the recording again and writes every frame line anew.
    /// </summary>
    /// <param name="recording">The recording, rewound</param>
    /// <param name="output">The output</param>
    /// <param name="offset">The shift of the receive times</param>
    /// <param name="frames">The frame anonymiser</param>
    /// <param name="expectedCount">The number of frames the first pass read</param>
    /// <param name="cancellationToken">The token that cancels the work</param>
    /// <returns>The result</returns>
    private static async Task<PipeAnonymiseResult> RewriteAsync(Stream recording, Stream output, TimeSpan offset, PipeFrameAnonymiser frames, long expectedCount, CancellationToken cancellationToken)
    {
        var reader = new PipeRecordingReader(recording);

        for (var index = 0L; index < expectedCount; index++)
        {
            var line = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var rewritten = line.Kind == PipeRecordingLineKind.Frame && line.ReceiveTime.UtcTicks >= offset.Ticks ? frames.Anonymise(line.Frame) : null;

            if (rewritten is null)
            {
                return ReadFailed();
            }

            await WriteAsync(output, PipeRecordingFormat.FormatFrameLine(line.ReceiveTime - offset, rewritten), cancellationToken).ConfigureAwait(false);
        }

        var last = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        return last.Kind == PipeRecordingLineKind.End
                   ? new PipeAnonymiseResult
                     {
                         Kind = PipeAnonymiseResultKind.Anonymised,
                         FrameCount = expectedCount
                     }
                   : ReadFailed();
    }

    /// <summary>
    /// Moves a recording to its start.
    /// </summary>
    /// <param name="recording">The recording</param>
    /// <returns><see langword="true" /> when the recording is at its start</returns>
    private static bool TryRewind(Stream recording)
    {
        try
        {
            recording.Seek(0, SeekOrigin.Begin);

            return true;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes a text to the output as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="output">The output</param>
    /// <param name="text">The text</param>
    /// <param name="cancellationToken">The token that cancels the write</param>
    /// <returns>The task</returns>
    private static ValueTask WriteAsync(Stream output, string text, CancellationToken cancellationToken)
    {
        return output.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
    }

    /// <summary>
    /// Creates the result of a recording that could not be read, or that changed between the passes.
    /// </summary>
    /// <returns>The result</returns>
    private static PipeAnonymiseResult ReadFailed()
    {
        return new PipeAnonymiseResult
               {
                   Kind = PipeAnonymiseResultKind.RecordingProblem,
                   Problem = new PipeReplayProblem
                             {
                                 Kind = PipeReplayProblemKind.ReadFailed
                             }
               };
    }

    /// <summary>
    /// Creates the result of a line whose shifted time would pass the earliest time.
    /// </summary>
    /// <param name="lineNumber">The number of the line</param>
    /// <returns>The result</returns>
    private static PipeAnonymiseResult InvalidLine(long lineNumber)
    {
        return new PipeAnonymiseResult
               {
                   Kind = PipeAnonymiseResultKind.RecordingProblem,
                   Problem = new PipeReplayProblem
                             {
                                 Kind = PipeReplayProblemKind.InvalidLine,
                                 LineNumber = lineNumber
                             }
               };
    }

    /// <summary>
    /// Draws the shift of the receive times: at least 30 days, under 365 days and never a whole number of days.
    /// </summary>
    /// <returns>The shift</returns>
    private TimeSpan DrawOffset()
    {
        const long spanTicks = (MaxShiftDays - MinShiftDays) * TimeSpan.TicksPerDay;

        var extra = Math.Min((long)(_random.NextDouble() * spanTicks), spanTicks - 1);
        var ticks = (MinShiftDays * TimeSpan.TicksPerDay) + extra;

        if (ticks % TimeSpan.TicksPerDay == 0)
        {
            ticks++;
        }

        return TimeSpan.FromTicks(ticks);
    }

    #endregion // Methods
}