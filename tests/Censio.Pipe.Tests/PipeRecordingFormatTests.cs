using System.Globalization;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeRecordingFormat" />.
/// </summary>
[TestClass]
public sealed class PipeRecordingFormatTests
{
    #region Constants

    /// <summary>
    /// The header every valid header test starts from.
    /// </summary>
    private const string ValidHeader = """{"format":"censio-pipe-recording","version":1,"started":"2026-11-01T17:09:00.1234567Z"}""";

    /// <summary>
    /// The most bytes a frame may have.
    /// </summary>
    private const int MaxFrameBytes = 1048576;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the header lines that are not a recording, with the reason.
    /// </summary>
    private static IEnumerable<object[]> LargeNotARecordingHeaders
    {
        get
        {
            yield return ["an unknown field nested 65 deep", $$"""{"format":"censio-pipe-recording","version":1,"x":{{new string('[', 65)}}{{new string(']', 65)}}}"""];
        }
    }

    /// <summary>
    /// Gets the frame lines that are invalid and built at run time, with the reason.
    /// </summary>
    private static IEnumerable<object[]> LargeInvalidFrameLines
    {
        get
        {
            yield return ["a frame of 1 MiB plus one byte", FrameLine("2026-11-01T17:09:00Z", Convert.ToBase64String(new byte[MaxFrameBytes + 1]))];
            yield return ["a frame of 1,398,104 characters without padding (1 MiB plus two bytes)", FrameLine("2026-11-01T17:09:00Z", Convert.ToBase64String(new byte[MaxFrameBytes + 2]))];
            yield return ["a frame string over 1,398,104 characters", FrameLine("2026-11-01T17:09:00Z", new string('A', 1398108))];
            yield return ["an unknown field nested 65 deep", $$"""{"time":"2026-11-01T17:09:00Z","frame":"AA==","x":{{new string('[', 65)}}{{new string(']', 65)}}}"""];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The header line holds the format, the version and the start time in UTC, and ends with a line feed.
    /// </summary>
    /// <param name="offsetHours">The offset of the start time handed in</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(-5)]
    public void PipeRecordingFormatHeaderHasFormatVersionAndStarted(int offsetHours)
    {
        // Arrange
        var started = new DateTimeOffset(2026, 11, 1, 17, 9, 0, TimeSpan.Zero).AddTicks(1234567).ToOffset(TimeSpan.FromHours(offsetHours));

        // Act
        var header = PipeRecordingFormat.FormatHeader(started);

        // Assert
        Assert.AreEqual(ValidHeader + "\n", header, "The header should be one compact JSON line with the start in UTC and a line feed");
    }

    /// <summary>
    /// A frame line holds the receive time with seven fractional digits in UTC and the plain base64 of the frame.
    /// </summary>
    /// <param name="time">The receive time handed in</param>
    /// <param name="base64">The base64 of the frame handed in</param>
    /// <param name="expected">The expected line</param>
    [TestMethod]
    [DataRow("2026-11-01T17:09:00.1234567Z", "Ag==", """{"time":"2026-11-01T17:09:00.1234567Z","frame":"Ag=="}""" + "\n")]
    [DataRow("2026-11-01T17:09:00Z", "", """{"time":"2026-11-01T17:09:00.0000000Z","frame":""}""" + "\n")]
    [DataRow("2026-11-01T19:09:00.5+02:00", "++++////", """{"time":"2026-11-01T17:09:00.5000000Z","frame":"++++////"}""" + "\n")]
    public void PipeRecordingFormatFrameLineHasSevenDigitUtcTimeAndBase64(string time, string base64, string expected)
    {
        // Arrange
        var receiveTime = DateTimeOffset.Parse(time, CultureInfo.InvariantCulture);
        var frame = Convert.FromBase64String(base64);

        // Act
        var line = PipeRecordingFormat.FormatFrameLine(receiveTime, frame);

        // Assert
        Assert.AreEqual(expected, line, "The line should be one compact JSON line with time and unescaped base64 and a line feed");
    }

    /// <summary>
    /// The file name holds the start cut to the second in UTC, and a suffix from sequence 2 on.
    /// </summary>
    /// <param name="started">The start time handed in</param>
    /// <param name="sequence">The sequence number</param>
    /// <param name="expected">The expected file name</param>
    [TestMethod]
    [DataRow("2026-11-01T17:09:00.9999999Z", 1, "recording-20261101-170900Z.jsonl")]
    [DataRow("2026-11-01T17:09:00.9999999Z", 2, "recording-20261101-170900Z-2.jsonl")]
    [DataRow("2026-11-01T17:09:00Z", 10, "recording-20261101-170900Z-10.jsonl")]
    [DataRow("2026-11-01T01:30:05+02:00", 1, "recording-20261031-233005Z.jsonl")]
    public void PipeRecordingFormatFileNameUsesStartSecondAndSuffix(string started, int sequence, string expected)
    {
        // Arrange
        var time = DateTimeOffset.Parse(started, CultureInfo.InvariantCulture);

        // Act
        var name = PipeRecordingFormat.FormatFileName(time, sequence);

        // Assert
        Assert.AreEqual(expected, name, "The name should follow the start second and the sequence");
    }

    /// <summary>
    /// The longest frame still gives a line within the maximum line length.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFormatLargestFrameLineFitsMaxLineLength()
    {
        // Arrange
        var frame = new byte[MaxFrameBytes];

        // Act
        var line = PipeRecordingFormat.FormatFrameLine(DateTimeOffset.MaxValue, frame);

        // Assert
        Assert.IsLessThanOrEqualTo(PipeRecordingFormat.MaxLineLength, Encoding.UTF8.GetByteCount(line) - 1, "The longest frame line without its line feed should fit the maximum line length");
        Assert.IsGreaterThan(1398104, Encoding.UTF8.GetByteCount(line), "The line should hold the 1,398,104 characters of the base64");
    }

    /// <summary>
    /// A valid header is accepted whatever its field order, the fields it ignores and whether it has a start.
    /// </summary>
    /// <param name="header">The header line</param>
    [TestMethod]
    [DataRow(ValidHeader)]
    [DataRow("""{"started":"2026-11-01T17:09:00.1234567Z","version":1,"format":"censio-pipe-recording"}""")]
    [DataRow("""{"version":1,"format":"censio-pipe-recording"}""")]
    [DataRow("""{"x":{"a":[1,2,{"b":null}]},"format":"censio-pipe-recording","y":"z","version":1,"started":5}""")]
    public void PipeRecordingFormatHeaderCheckAcceptsValidHeaders(string header)
    {
        // Arrange
        var line = Encoding.UTF8.GetBytes(header);

        // Act
        var problem = PipeRecordingFormat.CheckHeader(line);

        // Assert
        Assert.IsNull(problem, "A valid header should have no problem");
    }

    /// <summary>
    /// A header without both fields, of the wrong format or with a field of the wrong type is not a recording.
    /// </summary>
    /// <param name="header">The header line</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("\"censio-pipe-recording\"")]
    [DataRow("{}")]
    [DataRow("""{"format":"other","version":1}""")]
    [DataRow("""{"format":"censio-pipe-recording"}""")]
    [DataRow("""{"version":1}""")]
    [DataRow("""{"format":1,"version":1}""")]
    [DataRow("""{"format":null,"version":1}""")]
    [DataRow("""{"format":{},"version":1}""")]
    [DataRow("""{"format":["censio-pipe-recording"],"version":1}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":"1"}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":null}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":1.5}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":true}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":99999999999}""")]
    [DataRow("""{"format":"censio-pipe-recording","format":"censio-pipe-recording","version":1}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":1,"version":1}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":1} x""")]
    [DataRow("""{"format":"censio-pipe-recording","version":1""")]
    public void PipeRecordingFormatHeaderCheckRefusesNonRecordings(string header)
    {
        // Arrange
        var line = Encoding.UTF8.GetBytes(header);

        // Act
        var problem = PipeRecordingFormat.CheckHeader(line);

        // Assert
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem, "The header should not be accepted as a recording");
    }

    /// <summary>
    /// A header with a field nested too deep is not a recording.
    /// </summary>
    /// <param name="reason">What is wrong with the header</param>
    /// <param name="header">The header line</param>
    [TestMethod]
    [DynamicData(nameof(LargeNotARecordingHeaders))]
    public void PipeRecordingFormatHeaderCheckRefusesTooDeepHeader(string reason, string header)
    {
        // Arrange
        var line = Encoding.UTF8.GetBytes(header);

        // Act
        var problem = PipeRecordingFormat.CheckHeader(line);

        // Assert
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem, $"A header with {reason} should not be accepted as a recording");
    }

    /// <summary>
    /// A format name with a byte that is no UTF-8 is not a recording and throws nothing.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFormatHeaderCheckRefusesInvalidUtf8InFormat()
    {
        // Arrange
        byte[] line = [.. "{\"format\":\"censio-pipe-recording"u8, 0xFF, .. "\",\"version\":1}"u8];

        // Act
        var problem = PipeRecordingFormat.CheckHeader(line);

        // Assert
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem, "A format name with a raw 0xFF byte should not be accepted");
    }

    /// <summary>
    /// A version other than 1 is unsupported.
    /// </summary>
    /// <param name="header">The header line</param>
    [TestMethod]
    [DataRow("""{"format":"censio-pipe-recording","version":2}""")]
    [DataRow("""{"version":2,"format":"censio-pipe-recording","started":"2026-11-01T17:09:00Z"}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":0}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":-1}""")]
    [DataRow("""{"format":"censio-pipe-recording","version":2147483647}""")]
    public void PipeRecordingFormatHeaderCheckOtherVersionIsUnsupported(string header)
    {
        // Arrange
        var line = Encoding.UTF8.GetBytes(header);

        // Act
        var problem = PipeRecordingFormat.CheckHeader(line);

        // Assert
        Assert.AreEqual(PipeReplayProblemKind.UnsupportedVersion, problem, "A version other than 1 should be unsupported");
    }

    /// <summary>
    /// A frame line is accepted whatever its field order, the fields it ignores and the offset of its time; the time
    /// comes back in UTC.
    /// </summary>
    /// <param name="line">The frame line</param>
    /// <param name="expectedTime">The expected receive time</param>
    /// <param name="expectedBase64">The base64 of the expected frame</param>
    [TestMethod]
    [DataRow("""{"time":"2026-11-01T17:09:00.1234567Z","frame":"AAEC"}""", "2026-11-01T17:09:00.1234567Z", "AAEC")]
    [DataRow("""{"frame":"AAEC","time":"2026-11-01T17:09:00.1234567Z"}""", "2026-11-01T17:09:00.1234567Z", "AAEC")]
    [DataRow("""{"x":{"a":[1,{"b":null}]},"time":"2026-11-01T17:09:00.1234567Z","y":true,"frame":"++++////"}""", "2026-11-01T17:09:00.1234567Z", "++++////")]
    [DataRow("""{"time":"2026-11-01T19:09:00.1234567+02:00","frame":"Ag=="}""", "2026-11-01T17:09:00.1234567Z", "Ag==")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"AA=="}""", "2026-11-01T17:09:00Z", "AA==")]
    [DataRow("""{"time":"2026-11-01T17:09:00.1234567Z","frame":""}""", "2026-11-01T17:09:00.1234567Z", "")]
    [DataRow("""{"time":"0001-01-01T00:00:00Z","frame":"AA=="}""", "0001-01-01T00:00:00Z", "AA==")]
    [DataRow("""{"time":"9999-12-31T23:59:29.9999999Z","frame":"AA=="}""", "9999-12-31T23:59:29.9999999Z", "AA==")]
    public void PipeRecordingFormatTryParseFrameLineAcceptsValidForms(string line, string expectedTime, string expectedBase64)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(line);

        // Act
        var parsed = PipeRecordingFormat.TryParseFrameLine(bytes, out var receiveTime, out var frame);

        // Assert
        Assert.IsTrue(parsed, "The line should be accepted");
        Assert.AreEqual(DateTimeOffset.Parse(expectedTime, CultureInfo.InvariantCulture), receiveTime, "The receive time should be the recorded instant");
        Assert.AreEqual(TimeSpan.Zero, receiveTime.Offset, "The receive time should be in UTC");
        Assert.AreSequenceEqual(Convert.FromBase64String(expectedBase64), frame, "The frame should be the decoded base64");
    }

    /// <summary>
    /// A frame of exactly 1 MiB is accepted.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFormatTryParseFrameLineAcceptsFrameOfOneMebibyte()
    {
        // Arrange
        var expected = new byte[MaxFrameBytes];

        expected[0] = 3;
        expected[^1] = 9;

        var line = Encoding.UTF8.GetBytes(FrameLine("2026-11-01T17:09:00Z", Convert.ToBase64String(expected)));

        // Act
        var parsed = PipeRecordingFormat.TryParseFrameLine(line, out _, out var frame);

        // Assert
        Assert.IsTrue(parsed, "A frame of 1,048,576 bytes should be accepted");
        Assert.AreSequenceEqual(expected, frame, "The frame should be decoded whole");
    }

    /// <summary>
    /// A line that is not a frame line of the format is refused.
    /// </summary>
    /// <param name="line">The frame line</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("\"x\"")]
    [DataRow("{}")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z"}""")]
    [DataRow("""{"frame":"AA=="}""")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"A"}""")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"!!!!"}""")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"AA==","frame":"AA=="}""")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","time":"2026-11-01T17:09:00Z","frame":"AA=="}""")]
    [DataRow("""{"time":"yesterday","frame":"AA=="}""")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"AA=="} x""")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"AA=="}{"time":"2026-11-01T17:09:00Z","frame":"AA=="}""")]
    [DataRow("""{"time":"2026-11-01T17:09:00.1234567Z","frame":"AAE""")]
    [DataRow("""{"time":"9999-12-31T23:59:59.9999999Z","frame":"AA=="}""")]
    [DataRow("""{"time":"9999-12-31T23:59:30.0000000Z","frame":"AA=="}""")]
    [DataRow("""{"time":"0001-01-01T00:00:00+01:00","frame":"AA=="}""")]
    public void PipeRecordingFormatTryParseFrameLineRefusesInvalidLines(string line)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(line);

        // Act
        var parsed = PipeRecordingFormat.TryParseFrameLine(bytes, out _, out _);

        // Assert
        Assert.IsFalse(parsed, "The line should be refused");
    }

    /// <summary>
    /// A field of the wrong JSON type is refused without an exception, whichever of time and frame it is.
    /// </summary>
    /// <param name="value">The JSON value of the wrong type</param>
    [TestMethod]
    [DataRow("1")]
    [DataRow("1.5")]
    [DataRow("null")]
    [DataRow("true")]
    [DataRow("{}")]
    [DataRow("""{"a":1}""")]
    [DataRow("[]")]
    [DataRow("""["AA=="]""")]
    public void PipeRecordingFormatTryParseFrameLineRefusesFieldsOfWrongType(string value)
    {
        // Arrange
        var timeWrong = Encoding.UTF8.GetBytes($$"""{"time":{{value}},"frame":"AA=="}""");
        var frameWrong = Encoding.UTF8.GetBytes($$"""{"time":"2026-11-01T17:09:00Z","frame":{{value}}}""");

        // Act
        var timeParsed = PipeRecordingFormat.TryParseFrameLine(timeWrong, out _, out _);
        var frameParsed = PipeRecordingFormat.TryParseFrameLine(frameWrong, out _, out _);

        // Assert
        Assert.IsFalse(timeParsed, "A time of the wrong JSON type should be refused");
        Assert.IsFalse(frameParsed, "A frame of the wrong JSON type should be refused");
    }

    /// <summary>
    /// A time or a frame with a byte that is no UTF-8 is refused without an exception.
    /// </summary>
    /// <param name="before">The text before the raw byte</param>
    /// <param name="after">The text after the raw byte</param>
    [TestMethod]
    [DataRow("""{"time":"2026-11-01T17:09:00Z""", "\",\"frame\":\"AA==\"}")]
    [DataRow("""{"time":"2026-11-01T17:09:00Z","frame":"AA""", """=="}""")]
    public void PipeRecordingFormatTryParseFrameLineRefusesInvalidUtf8(string before, string after)
    {
        // Arrange
        byte[] line = [.. Encoding.UTF8.GetBytes(before), 0xFF, .. Encoding.UTF8.GetBytes(after)];

        // Act
        var parsed = PipeRecordingFormat.TryParseFrameLine(line, out _, out _);

        // Assert
        Assert.IsFalse(parsed, "A line with a raw 0xFF byte inside a string should be refused");
    }

    /// <summary>
    /// A frame over the limits and a line nested too deep are refused before they are decoded or skipped.
    /// </summary>
    /// <param name="reason">What is wrong with the line</param>
    /// <param name="line">The frame line</param>
    [TestMethod]
    [DynamicData(nameof(LargeInvalidFrameLines))]
    public void PipeRecordingFormatTryParseFrameLineRefusesLargeInvalidLines(string reason, string line)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(line);

        // Act
        var parsed = PipeRecordingFormat.TryParseFrameLine(bytes, out _, out _);

        // Assert
        Assert.IsFalse(parsed, $"A line with {reason} should be refused");
    }

    /// <summary>
    /// A formatted frame line, read back without its line feed, gives the same time and frame.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFormatFrameLineRoundTripsThroughParse()
    {
        // Arrange
        var time = new DateTimeOffset(2026, 11, 1, 17, 9, 0, TimeSpan.Zero).AddTicks(7654321);
        var frame = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var line = PipeRecordingFormat.FormatFrameLine(time, frame);

        // Act
        var parsed = PipeRecordingFormat.TryParseFrameLine(Encoding.UTF8.GetBytes(line.TrimEnd('\n')), out var receiveTime, out var parsedFrame);

        // Assert
        Assert.IsTrue(parsed, "The formatted line should be accepted");
        Assert.AreEqual(time, receiveTime, "The receive time should survive to the 100 ns");
        Assert.AreSequenceEqual(frame, parsedFrame, "The frame should survive");
    }

    /// <summary>
    /// Creates a frame line.
    /// </summary>
    /// <param name="time">The time text</param>
    /// <param name="base64">The base64 text</param>
    /// <returns>The line without a line feed</returns>
    private static string FrameLine(string time, string base64)
    {
        return $$"""{"time":"{{time}}","frame":"{{base64}}"}""";
    }

    #endregion // Methods
}