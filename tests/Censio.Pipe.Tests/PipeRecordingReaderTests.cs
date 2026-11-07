using System.Globalization;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeRecordingReader" />.
/// </summary>
[TestClass]
public sealed class PipeRecordingReaderTests
{
    #region Constants

    /// <summary>
    /// The time of the hand-written frame lines.
    /// </summary>
    private const string LineTime = "2026-11-01T17:09:04.0000000Z";

    /// <summary>
    /// The end of the padded frame line.
    /// </summary>
    private const string LineSuffix = "\"}";

    /// <summary>
    /// The most lines a test reads before it gives up, so that a reader that never ends cannot hang a test.
    /// </summary>
    private const int MaxReads = 100;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the recordings.
    /// </summary>
    private static readonly DateTimeOffset _start = new DateTimeOffset(2026, 11, 1, 17, 9, 0, TimeSpan.Zero).AddTicks(1234567);

    /// <summary>
    /// The first frame of the hand-written recordings: a version 2 frame.
    /// </summary>
    private static readonly byte[] _firstFrame = [0, 2, 0, 0, 0];

    /// <summary>
    /// The second frame of the hand-written recordings: a session-end frame.
    /// </summary>
    private static readonly byte[] _secondFrame = [2];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeRecordingReaderTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the files that hold the frames <see cref="_firstFrame" /> and <see cref="_secondFrame" /> in lines 2 and 3 in
    /// a form that is accepted.
    /// </summary>
    private static IEnumerable<object[]> AcceptedFiles
    {
        get
        {
            var plain = string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame));
            var crlf = plain.Replace("\n", "\r\n", StringComparison.Ordinal);

            yield return ["line feeds", Utf8(plain)];
            yield return ["carriage return and line feed", Utf8(crlf)];
            yield return ["mixed terminators", Utf8(string.Concat(Header(), Line(1, _firstFrame).Replace("\n", "\r\n", StringComparison.Ordinal), Line(2, _secondFrame)))];
            yield return ["no final line feed", Utf8(plain.TrimEnd('\n'))];
            yield return ["empty last line after line feed", Utf8(plain + "\n")];
            yield return ["empty last line after carriage return and line feed", Utf8(crlf + "\r\n")];
            yield return ["byte order mark", Concat(Encoding.UTF8.GetPreamble(), Utf8(plain))];
            yield return [
                             "unknown fields and any field order",
                             Utf8(string.Concat($"{{\"version\":1,\"x\":[1,{{\"a\":null}}],\"format\":\"{PipeRecordingFormat.FormatName}\"}}\n",
                                                $"{{\"frame\":\"{Convert.ToBase64String(_firstFrame)}\",\"y\":true,\"time\":\"{FormatTime(1)}\"}}\n",
                                                Line(2, _secondFrame)))
                         ];
        }
    }

    /// <summary>
    /// Gets the files whose header is refused, with the expected problem.
    /// </summary>
    private static IEnumerable<object[]> RefusedHeaders
    {
        get
        {
            var frames = string.Concat(Line(1, _firstFrame), Line(2, _secondFrame));

            yield return ["empty file", Array.Empty<byte>(), PipeReplayProblemKind.NotARecording];
            yield return ["empty first line", Utf8("\n" + frames), PipeReplayProblemKind.NotARecording];
            yield return ["frame line as first line", Utf8(frames), PipeReplayProblemKind.NotARecording];
            yield return ["other format", Utf8("{\"format\":\"other\",\"version\":1}\n" + frames), PipeReplayProblemKind.NotARecording];
            yield return ["version 2", Utf8($"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":2}}\n" + frames), PipeReplayProblemKind.UnsupportedVersion];
        }
    }

    /// <summary>
    /// Gets the files with an invalid line after two valid frame lines, with the number of the invalid line.
    /// </summary>
    private static IEnumerable<object[]> InvalidLines
    {
        get
        {
            var head = string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame));
            var tail = Line(5, _secondFrame);

            yield return ["not JSON", Utf8(string.Concat(head, "not json\n", tail)), 4L];
            yield return ["missing time", Utf8(string.Concat(head, $"{{\"frame\":\"{Convert.ToBase64String(_secondFrame)}\"}}\n", tail)), 4L];
            yield return ["invalid base64", Utf8(string.Concat(head, $"{{\"time\":\"{LineTime}\",\"frame\":\"A\"}}\n", tail)), 4L];
            yield return ["empty line inside", Utf8(string.Concat(head, "\n", tail)), 4L];
            yield return ["two empty lines at the end", Utf8(string.Concat(head, "\n\n")), 4L];
            yield return ["cut last line without terminator", Utf8(string.Concat(head, $"{{\"time\":\"{LineTime}\",\"fra")), 4L];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Each frame line is read with its line number (the header is line 1), its receive time in UTC and its decoded
    /// frame, and the end of the recording follows the last one.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeRecordingReaderFramesComeWithLineNumberTimeAndFrameThenEnd()
    {
        // Arrange
        var reader = new PipeRecordingReader(new MemoryStream(Utf8(string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame)))));

        // Act
        var lines = await ReadAllAsync(reader);

        // Assert
        string[] expected = [
                                $"Frame 2 {FormatTime(1)} {Convert.ToBase64String(_firstFrame)} no problem",
                                $"Frame 3 {FormatTime(2)} {Convert.ToBase64String(_secondFrame)} no problem",
                                "End no problem"
                            ];

        Assert.AreSequenceEqual(expected, lines.Select(Describe).ToArray(), "The reader should give both frame lines with their numbers, times and frames, then the end");
        Assert.AreEqual(TimeSpan.Zero, lines[0].ReceiveTime.Offset, "The receive time should be in UTC");
    }

    /// <summary>
    /// A frame line whose time has an offset is read with its time in UTC.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeRecordingReaderTimeWithOffsetIsReadInUtc()
    {
        // Arrange
        var line = $"{{\"time\":\"2026-11-01T19:09:01.5000000+02:00\",\"frame\":\"{Convert.ToBase64String(_firstFrame)}\"}}\n";
        var reader = new PipeRecordingReader(new MemoryStream(Utf8(Header() + line)));

        // Act
        var first = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingLineKind.Frame, first.Kind, "The line should be a frame line");
        Assert.AreEqual(new DateTimeOffset(2026, 11, 1, 17, 9, 1, 500, TimeSpan.Zero), first.ReceiveTime, "The receive time should be the recorded instant");
        Assert.AreEqual(TimeSpan.Zero, first.ReceiveTime.Offset, "The receive time should be in UTC");
    }

    /// <summary>
    /// The line forms of the format are accepted: both frames come in lines 2 and 3 and the recording then ends.
    /// </summary>
    /// <param name="description">The form</param>
    /// <param name="file">The file</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DynamicData(nameof(AcceptedFiles))]
    public async Task PipeRecordingReaderAcceptedLineFormsGiveFramesThenEnd(string description, byte[] file)
    {
        // Arrange
        var reader = new PipeRecordingReader(new MemoryStream(file));

        // Act
        var lines = await ReadAllAsync(reader);

        // Assert
        string[] expected = [
                                $"Frame 2 {FormatTime(1)} {Convert.ToBase64String(_firstFrame)} no problem",
                                $"Frame 3 {FormatTime(2)} {Convert.ToBase64String(_secondFrame)} no problem",
                                "End no problem"
                            ];

        Assert.AreSequenceEqual(expected, lines.Select(Describe).ToArray(), $"Both frames and then the end should be read ({description})");
    }

    /// <summary>
    /// A recording of the header alone ends at once, without a problem.
    /// </summary>
    /// <param name="content">The file</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("<H>\n")]
    [DataRow("<H>")]
    [DataRow("<H>\r\n")]
    public async Task PipeRecordingReaderHeaderOnlyGivesEnd(string content)
    {
        // Arrange
        var reader = new PipeRecordingReader(new MemoryStream(Utf8(content.Replace("<H>", Header().TrimEnd('\n'), StringComparison.Ordinal))));

        // Act
        var first = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingLineKind.End, first.Kind, "A header-only recording should end at once");
        Assert.IsNull(first.Problem, "A header-only recording is no problem");
        Assert.AreEqual(_start, reader.Started, "The start of the header should be known once the header is read");
    }

    /// <summary>
    /// The start of the header is known after the first read, in UTC; a missing or unusable start is
    /// <see langword="null" />, and the recording is read all the same.
    /// </summary>
    /// <param name="startedJson">The JSON of the started field, or an empty text for none</param>
    /// <param name="expected">The expected start, or <see langword="null" /></param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(",\"started\":\"2026-11-01T17:09:00.1234567Z\"", "2026-11-01T17:09:00.1234567Z")]
    [DataRow(",\"started\":\"2026-11-01T19:09:00.1234567+02:00\"", "2026-11-01T17:09:00.1234567Z")]
    [DataRow("", null)]
    [DataRow(",\"started\":\"x\"", null)]
    [DataRow(",\"started\":5", null)]
    [DataRow(",\"started\":null", null)]
    public async Task PipeRecordingReaderStartedIsHeaderStartInUtc(string startedJson, string? expected)
    {
        // Arrange
        var header = $"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":1{startedJson}}}\n";
        var reader = new PipeRecordingReader(new MemoryStream(Utf8(header + Line(1, _firstFrame))));
        DateTimeOffset? expectedStart = expected is null ? null : DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture);

        // Act
        var first = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingLineKind.Frame, first.Kind, "The recording should be read whatever its start");
        Assert.AreEqual(expectedStart, reader.Started, "The start should be the header's start, or null when missing or unusable");
        Assert.AreEqual(TimeSpan.Zero, reader.Started?.Offset ?? TimeSpan.Zero, "The start should be in UTC");
    }

    /// <summary>
    /// A header that is refused gives its problem at the first read, with no line number, and no frame.
    /// </summary>
    /// <param name="description">What is wrong with the file</param>
    /// <param name="file">The file</param>
    /// <param name="expected">The expected problem</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(RefusedHeaders))]
    public async Task PipeRecordingReaderRefusedHeaderGivesProblemWithoutLineNumber(string description, byte[] file, PipeReplayProblemKind expected)
    {
        // Arrange
        var reader = new PipeRecordingReader(new MemoryStream(file));

        // Act
        var first = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeRecordingLineKind.Problem, first.Kind, $"The first read should give a problem ({description})");
        Assert.IsNotNull(first.Problem, $"The problem should be set ({description})");
        Assert.AreEqual(expected, first.Problem.Kind, $"The header should be refused with its problem ({description})");
        Assert.IsNull(first.Problem.LineNumber, $"The problem of the header names no line ({description})");
    }

    /// <summary>
    /// An invalid line gives an invalid-line problem with its number, after the frames before it.
    /// </summary>
    /// <param name="description">What is wrong with the line</param>
    /// <param name="file">The file</param>
    /// <param name="expectedLine">The number of the invalid line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(InvalidLines))]
    public async Task PipeRecordingReaderInvalidLineGivesProblemWithItsNumber(string description, byte[] file, long expectedLine)
    {
        // Arrange
        var reader = new PipeRecordingReader(new MemoryStream(file));

        // Act
        var lines = await ReadAllAsync(reader);

        // Assert
        string[] expected = [
                                $"Frame 2 {FormatTime(1)} {Convert.ToBase64String(_firstFrame)} no problem",
                                $"Frame 3 {FormatTime(2)} {Convert.ToBase64String(_secondFrame)} no problem",
                                string.Create(CultureInfo.InvariantCulture, $"Problem InvalidLine {expectedLine}")
                            ];

        Assert.AreSequenceEqual(expected, lines.Select(Describe).ToArray(), $"The frames before the invalid line and then its problem should be read ({description})");
    }

    /// <summary>
    /// A line over the longest line is invalid at once: the reader holds one line in memory and does not read the long
    /// line whole.
    /// </summary>
    /// <param name="lineLength">The length of the line in bytes</param>
    /// <param name="terminated">Whether a line feed ends the line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(PipeRecordingFormat.MaxLineLength + 1, true)]
    [DataRow(PipeRecordingFormat.MaxLineLength * 5, false)]
    public async Task PipeRecordingReaderLineOverMaxLengthIsInvalidWithoutReadingItWhole(int lineLength, bool terminated)
    {
        // Arrange
        var head = Utf8(string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame)));
        var text = PaddedLine(string.Empty, lineLength) + (terminated ? "\n" : string.Empty);
        var recording = new ChunkedStream([.. head, .. Utf8(text)], 4096);
        var reader = new PipeRecordingReader(recording);

        // Act
        var lines = await ReadAllAsync(reader);

        // Assert
        Assert.HasCount(3, lines, "The reader should give both frames and the problem");
        Assert.AreEqual("Problem InvalidLine 4", Describe(lines[2]), "The long line should be invalid with its number");
        Assert.IsLessThan(2 * PipeRecordingFormat.MaxLineLength, recording.Offset, "The reader should stop reading soon after the longest line");
    }

    /// <summary>
    /// A header line over the longest line makes the file no recording, whatever ends it, without a line number.
    /// </summary>
    /// <param name="terminated">Whether a line feed ends the line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PipeRecordingReaderHeaderOverMaxLengthIsNotARecording(bool terminated)
    {
        // Arrange
        var text = PaddedLine(string.Empty, PipeRecordingFormat.MaxLineLength + 1) + (terminated ? "\n" : string.Empty);
        var reader = new PipeRecordingReader(new ChunkedStream(Utf8(text + Line(1, _firstFrame)), 4096));

        // Act
        var first = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual("Problem NotARecording none", Describe(first), "A header that is too long makes the file no recording");
    }

    /// <summary>
    /// A recording that cannot be read on gives a read failure with no line number, after the frames read whole before
    /// it.
    /// </summary>
    /// <param name="wholeLines">The number of whole lines before the failing read</param>
    /// <param name="expectedFrames">The number of frames read</param>
    /// <param name="accessDenied">Whether the failing read throws an access error instead of an I/O error</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(0, 0, false)]
    [DataRow(1, 0, false)]
    [DataRow(2, 1, false)]
    [DataRow(3, 2, false)]
    [DataRow(0, 0, true)]
    [DataRow(3, 2, true)]
    public async Task PipeRecordingReaderReadFailureIsReadFailed(int wholeLines, int expectedFrames, bool accessDenied)
    {
        // Arrange
        string[] content = [Header(), Line(1, _firstFrame), Line(2, _secondFrame), Line(3, _firstFrame)];
        var failAt = content.Take(wholeLines).Sum(line => Utf8(line).Length) + 5;
        var reader = new PipeRecordingReader(new ChunkedStream(Utf8(string.Concat(content)), 7, failAt, accessDenied: accessDenied));

        // Act
        var lines = await ReadAllAsync(reader);

        // Assert
        Assert.HasCount(expectedFrames + 1, lines, "The frames read whole before the failure and the problem should be read");
        Assert.AreEqual("Problem ReadFailed none", Describe(lines[^1]), "A recording that cannot be read on should give a read failure without a line number");
    }

    /// <summary>
    /// Creates the UTF-8 bytes of a text.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The bytes</returns>
    private static byte[] Utf8(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>
    /// Joins byte arrays.
    /// </summary>
    /// <param name="parts">The arrays</param>
    /// <returns>The bytes of all arrays</returns>
    private static byte[] Concat(params byte[][] parts)
    {
        return [.. parts.SelectMany(part => part)];
    }

    /// <summary>
    /// Formats a time of a hand-written line, the given seconds after the start.
    /// </summary>
    /// <param name="seconds">The seconds after the start</param>
    /// <returns>The time text</returns>
    private static string FormatTime(int seconds)
    {
        return _start.AddSeconds(seconds).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Creates the header line of the recordings.
    /// </summary>
    /// <returns>The header with its line feed</returns>
    private static string Header()
    {
        return PipeRecordingFormat.FormatHeader(_start);
    }

    /// <summary>
    /// Creates a frame line the given seconds after the start.
    /// </summary>
    /// <param name="seconds">The seconds after the start</param>
    /// <param name="frame">The frame</param>
    /// <returns>The line with its line feed</returns>
    private static string Line(int seconds, byte[] frame)
    {
        return PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(seconds), frame);
    }

    /// <summary>
    /// Creates a frame line of an exact length in bytes, padded with an unknown field.
    /// </summary>
    /// <param name="frameBase64">The base64 text of the frame</param>
    /// <param name="length">The length of the line in bytes, the terminator excluded</param>
    /// <returns>The line without terminator</returns>
    private static string PaddedLine(string frameBase64, int length)
    {
        var head = $"{{\"time\":\"{LineTime}\",\"frame\":\"{frameBase64}\",\"pad\":\"";

        return string.Concat(head, new string('x', length - head.Length - LineSuffix.Length), LineSuffix);
    }

    /// <summary>
    /// Describes a line read: a frame line with its number, time, frame and problem; another line with its kind and
    /// problem.
    /// </summary>
    /// <param name="line">The line</param>
    /// <returns>The description</returns>
    private static string Describe(PipeRecordingLine line)
    {
        var problem = line.Problem is null
                          ? "no problem"
                          : string.Create(CultureInfo.InvariantCulture, $"{line.Problem.Kind} {line.Problem.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "none"}");

        return line.Kind == PipeRecordingLineKind.Frame
                   ? string.Create(CultureInfo.InvariantCulture, $"Frame {line.LineNumber} {line.ReceiveTime.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'} {Convert.ToBase64String(line.Frame)} {problem}")
                   : $"{line.Kind} {problem}";
    }

    /// <summary>
    /// Reads the lines of a recording up to and including the first line that is not a frame line.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The lines read</returns>
    private async Task<List<PipeRecordingLine>> ReadAllAsync(PipeRecordingReader reader)
    {
        var lines = new List<PipeRecordingLine>();

        for (var count = 0; count < MaxReads; count++)
        {
            var line = await reader.ReadAsync(TestContext.CancellationToken);

            lines.Add(line);

            if (line.Kind != PipeRecordingLineKind.Frame)
            {
                break;
            }
        }

        return lines;
    }

    #endregion // Methods
}