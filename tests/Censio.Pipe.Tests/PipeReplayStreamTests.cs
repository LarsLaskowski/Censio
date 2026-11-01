using System.Globalization;
using System.Text;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeReplayStream" />.
/// </summary>
[TestClass]
public sealed class PipeReplayStreamTests
{
    #region Constants

    /// <summary>
    /// The most bytes one read of a live run returns.
    /// </summary>
    private const int ChunkSize = 8;

    /// <summary>
    /// The one-megabyte frame size limit of the pipe and of a recording.
    /// </summary>
    private const int MaxFrameBytes = 1048576;

    /// <summary>
    /// The time of the hand-written frame lines.
    /// </summary>
    private const string LineTime = "2026-11-01T17:09:04.0000000Z";

    /// <summary>
    /// The end of the padded frame line.
    /// </summary>
    private const string LineSuffix = "\"}";

    /// <summary>
    /// The base64 text of a session-end frame.
    /// </summary>
    private const string LineFrame = "Ag==";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the recordings and of the clocks of most tests.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The time the clock of a live run moves by at every read of its stream.
    /// </summary>
    private static readonly TimeSpan _step = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The first frame of the hand-written recordings: a version 2 frame.
    /// </summary>
    private static readonly byte[] _firstFrame = [0, 2, 0, 0, 0];

    /// <summary>
    /// The second frame of the hand-written recordings: a session-end frame.
    /// </summary>
    private static readonly byte[] _secondFrame = [2];

    /// <summary>
    /// An empty frame.
    /// </summary>
    private static readonly byte[] _emptyFrame = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayStreamTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeReplayStreamTests(TestContext testContext)
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
    /// Gets the lines that are invalid as the fourth line of a recording of two valid frame lines, each with the bytes
    /// that follow it.
    /// </summary>
    private static IEnumerable<object[]> InvalidLines
    {
        get
        {
            var deepNesting = string.Concat(new string('[', 65), new string(']', 65));

            yield return ["truncated JSON", Tail("{\"time\":")];
            yield return ["not JSON", Tail("not json")];
            yield return ["only spaces", Tail("   ")];
            yield return ["array", Tail("[1,2]")];
            yield return ["string", Tail("\"text\"")];
            yield return ["number", Tail("5")];
            yield return ["null", Tail("null")];
            yield return ["empty object", Tail("{}")];
            yield return ["header as frame line", Tail($"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":1}}")];
            yield return ["content after the object", Tail($"{{\"time\":\"{LineTime}\",\"frame\":\"{LineFrame}\"}} {{}}")];
            yield return ["text after the object", Tail($"{{\"time\":\"{LineTime}\",\"frame\":\"{LineFrame}\"}}x")];
            yield return ["base64 of the wrong length", Tail($"{{\"time\":\"{LineTime}\",\"frame\":\"A\"}}")];
            yield return ["base64 with other characters", Tail($"{{\"time\":\"{LineTime}\",\"frame\":\"@@@@\"}}")];
            yield return ["missing time", Tail($"{{\"frame\":\"{LineFrame}\"}}")];
            yield return ["missing frame", Tail($"{{\"time\":\"{LineTime}\"}}")];
            yield return ["time twice", Tail($"{{\"time\":\"{LineTime}\",\"time\":\"{LineTime}\",\"frame\":\"{LineFrame}\"}}")];
            yield return ["frame twice", Tail($"{{\"time\":\"{LineTime}\",\"frame\":\"{LineFrame}\",\"frame\":\"{LineFrame}\"}}")];
            yield return ["empty time", Tail(FrameLineJson("\"\"", $"\"{LineFrame}\""))];
            yield return ["time that is no time", Tail(FrameLineJson("\"yesterday\"", $"\"{LineFrame}\""))];
            yield return ["latest time", Tail(FrameLineJson("\"9999-12-31T23:59:59.9999999Z\"", $"\"{LineFrame}\""))];
            yield return ["time less than 30 seconds before the latest", Tail(FrameLineJson("\"9999-12-31T23:59:30.0000001Z\"", $"\"{LineFrame}\""))];
            yield return ["time before year 1 in UTC", Tail(FrameLineJson("\"0001-01-01T00:00:00+01:00\"", $"\"{LineFrame}\""))];
            yield return ["time as number", Tail(FrameLineJson("5", $"\"{LineFrame}\""))];
            yield return ["time as null", Tail(FrameLineJson("null", $"\"{LineFrame}\""))];
            yield return ["time as true", Tail(FrameLineJson("true", $"\"{LineFrame}\""))];
            yield return ["time as object", Tail(FrameLineJson("{}", $"\"{LineFrame}\""))];
            yield return ["time as array", Tail(FrameLineJson("[]", $"\"{LineFrame}\""))];
            yield return ["frame as number", Tail(FrameLineJson($"\"{LineTime}\"", "5"))];
            yield return ["frame as null", Tail(FrameLineJson($"\"{LineTime}\"", "null"))];
            yield return ["frame as true", Tail(FrameLineJson($"\"{LineTime}\"", "true"))];
            yield return ["frame as object", Tail(FrameLineJson($"\"{LineTime}\"", "{}"))];
            yield return ["frame as array", Tail(FrameLineJson($"\"{LineTime}\"", "[]"))];
            yield return ["raw 0xFF in time", Concat(Utf8("{\"time\":\""), [0xFF], Utf8($"\",\"frame\":\"{LineFrame}\"}}\n"))];
            yield return ["raw 0xFF in frame", Concat(Utf8($"{{\"time\":\"{LineTime}\",\"frame\":\""), [0xFF], Utf8("\"}\n"))];
            yield return ["unknown field nested 65 deep", Tail($"{{\"time\":\"{LineTime}\",\"frame\":\"{LineFrame}\",\"x\":{deepNesting}}}")];
            yield return ["frame one byte over 1 MiB", Tail(FrameLineJson($"\"{LineTime}\"", $"\"{Convert.ToBase64String(new byte[MaxFrameBytes + 1])}\""))];
            yield return ["1398104 characters without padding", Tail(FrameLineJson($"\"{LineTime}\"", $"\"{Convert.ToBase64String(new byte[MaxFrameBytes + 2])}\""))];
            yield return ["frame far over 1 MiB", Tail(FrameLineJson($"\"{LineTime}\"", $"\"{Convert.ToBase64String(new byte[MaxFrameBytes + 5])}\""))];
            yield return ["cut last line without terminator", Utf8($"{{\"time\":\"{LineTime}\",\"fra")];
        }
    }

    /// <summary>
    /// Gets the files that hold the frames <see cref="_firstFrame" /> and <see cref="_secondFrame" /> in a form that is
    /// accepted.
    /// </summary>
    private static IEnumerable<object[]> AcceptedFiles
    {
        get
        {
            var plain = string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame));
            var crlf = plain.Replace("\n", "\r\n", StringComparison.Ordinal);
            var allowedNesting = string.Concat(new string('[', 63), new string(']', 63));
            var latest = new DateTimeOffset(9999, 12, 31, 23, 59, 29, TimeSpan.Zero).AddTicks(9999999);

            yield return ["line feeds", Utf8(plain)];
            yield return ["carriage return and line feed", Utf8(crlf)];
            yield return ["mixed terminators", Utf8(string.Concat(Header(), Line(1, _firstFrame).Replace("\n", "\r\n", StringComparison.Ordinal), Line(2, _secondFrame)))];
            yield return ["no final line feed", Utf8(plain.TrimEnd('\n'))];
            yield return ["no final carriage return and line feed", Utf8(crlf.TrimEnd('\r', '\n'))];
            yield return ["empty last line after line feed", Utf8(plain + "\n")];
            yield return ["empty last line after carriage return and line feed", Utf8(crlf + "\r\n")];
            yield return ["byte order mark", Concat(Encoding.UTF8.GetPreamble(), Utf8(plain))];
            yield return ["byte order mark and carriage return and line feed", Concat(Encoding.UTF8.GetPreamble(), Utf8(crlf))];
            yield return [
                             "unknown fields",
                             Utf8(string.Concat($"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":1,\"started\":5,\"extra\":{{\"a\":[1,{{\"b\":null}}]}}}}\n",
                                                $"{{\"extra\":[true,false,null,\"x\",1.5],\"time\":\"{FormatTime(1)}\",\"frame\":\"{Convert.ToBase64String(_firstFrame)}\",\"more\":{{}}}}\n",
                                                Line(2, _secondFrame)))
                         ];
            yield return ["header without started", Utf8(string.Concat($"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":1}}\n", Line(1, _firstFrame), Line(2, _secondFrame)))];
            yield return [
                             "any field order",
                             Utf8(string.Concat($"{{\"version\":1,\"started\":\"x\",\"format\":\"{PipeRecordingFormat.FormatName}\"}}\n",
                                                $"{{\"frame\":\"{Convert.ToBase64String(_firstFrame)}\",\"time\":\"{FormatTime(1)}\"}}\n",
                                                Line(2, _secondFrame)))
                         ];
            yield return [
                             "spaces and tabs between tokens",
                             Utf8(string.Concat($"{{ \"format\" : \"{PipeRecordingFormat.FormatName}\" ,\t\"version\" : 1 }}\n",
                                                $"{{ \"time\" :\t\"{FormatTime(1)}\" , \"frame\" : \"{Convert.ToBase64String(_firstFrame)}\" }}\n",
                                                Line(2, _secondFrame)))
                         ];
            yield return [
                             "unknown field nested 64 deep",
                             Utf8(string.Concat(Header(),
                                                $"{{\"time\":\"{FormatTime(1)}\",\"frame\":\"{Convert.ToBase64String(_firstFrame)}\",\"x\":{allowedNesting}}}\n",
                                                Line(2, _secondFrame)))
                         ];
            yield return [
                             "time with offset",
                             Utf8(string.Concat(Header(),
                                                $"{{\"time\":\"2026-11-01T19:09:01.0000000+02:00\",\"frame\":\"{Convert.ToBase64String(_firstFrame)}\"}}\n",
                                                Line(2, _secondFrame)))
                         ];
            yield return ["latest accepted time", Utf8(string.Concat(Header(), PipeRecordingFormat.FormatFrameLine(latest, _firstFrame), PipeRecordingFormat.FormatFrameLine(latest, _secondFrame)))];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A recorded run replayed at maximum speed gives the same ticks, session events, versions and skipped frames in the
    /// same order, without waiting.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamRecordedRunReplaysSameItemsAtMaximumSpeed()
    {
        // Arrange
        var live = await RunLiveAsync(Wire(Scenario()));

        // Act
        var replay = await RunReplayAsync(live.Recording, "max");

        // Assert
        Assert.HasCount(12, live.Items, "The live run should give the 12 items of the scenario");
        Assert.AreSequenceEqual(live.Items.Select(item => Describe(item, true)).ToList(),
                                replay.Items.Select(item => Describe(item, true)).ToList(),
                                "The replay should give the items of the live run, receive times and close reasons included");
        Assert.IsEmpty(replay.Delays, "Maximum speed should not wait");
    }

    /// <summary>
    /// A recorded run replayed at a factor gives the same items and waits the recorded pauses divided by the factor.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamRecordedRunReplaysSameItemsAtFactorSpeed()
    {
        // Arrange
        var live = await RunLiveAsync(Wire(Scenario()));
        var times = RecordedTimes(live.Recording);
        var expectedWait = TimeSpan.FromTicks((times[^1] - times[0]).Ticks / 10);

        // Act
        var replay = await RunReplayAsync(live.Recording, "10");

        // Assert
        Assert.AreSequenceEqual(live.Items.Select(item => Describe(item, true)).ToList(),
                                replay.Items.Select(item => Describe(item, true)).ToList(),
                                "The replay should give the items of the live run, whatever the speed");
        Assert.AreEqual(expectedWait, TimeSpan.FromTicks(replay.Delays.Sum(delay => delay.Ticks)), "The replay should wait the recorded pauses divided by 10");
    }

    /// <summary>
    /// The clock moves while one frame arrives in chunks: the recorded time, the live receive time and the replayed
    /// receive time are the one reading taken when the frame was whole.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamClockMovedInsideChunkedFrameKeepsReceiveTime()
    {
        // Arrange
        var live = await RunLiveAsync(Wire(VersionFrame(2), IslandFrame(1, 100)));
        var expected = _start + (_step * live.DeliveredReads);

        // Act
        var replay = await RunReplayAsync(live.Recording, "max");

        // Assert
        Assert.HasCount(3, live.Items, "The live run should give the version, the tick and the end");
        Assert.IsNotNull(live.Items[1].Tick, "The live run should give the tick second");
        Assert.AreEqual(expected, live.Items[1].Tick!.ReceiveTime, "The live tick should carry the time at which its frame was whole");
        Assert.AreEqual(expected, RecordedTimes(live.Recording)[1], "The recording should hold the time of the live tick");
        Assert.HasCount(3, replay.Items, "The replay should give the version, the tick and the end");
        Assert.IsNotNull(replay.Items[1].Tick, "The replay should give the tick second");
        Assert.AreEqual(expected, replay.Items[1].Tick!.ReceiveTime, "The replayed tick should carry the recorded time unchanged");
    }

    /// <summary>
    /// A tick the live quiet period closed while a read was pending closes as the quiet period in the replay too, when a
    /// later frame follows.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamQuietPeriodCloseReplaysAsQuietPeriod()
    {
        // Arrange
        var live = await RunQuietLiveAsync(true);

        // Act
        var replay = await RunReplayAsync(live.Recording, "max");

        // Assert
        Assert.HasCount(4, live.Items, "The live run should give the version, two ticks and the end");
        Assert.AreEqual(PipeTickCloseReason.QuietPeriod, live.Items[1].Tick!.CloseReason, "The live quiet period should close the first tick");
        Assert.AreSequenceEqual(live.Items.Select(item => Describe(item, true)).ToList(),
                                replay.Items.Select(item => Describe(item, true)).ToList(),
                                "The replay should give the items of the live run");
    }

    /// <summary>
    /// A last tick the live quiet period closed is closed as stream ended in the replay: the one accepted difference.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamLastTickClosedByQuietPeriodReplaysAsStreamEnded()
    {
        // Arrange
        var live = await RunQuietLiveAsync(false);

        // Act
        var replay = await RunReplayAsync(live.Recording, "max");

        // Assert
        Assert.HasCount(3, live.Items, "The live run should give the version, the tick and the end");
        Assert.HasCount(3, replay.Items, "The replay should give the version, the tick and the end");
        Assert.AreEqual(PipeTickCloseReason.QuietPeriod, live.Items[1].Tick!.CloseReason, "The live quiet period should close the last tick");
        Assert.AreEqual(PipeTickCloseReason.StreamEnded, replay.Items[1].Tick!.CloseReason, "The replay should close the last tick as stream ended");
        Assert.AreSequenceEqual(live.Items.Select(item => Describe(item, false)).ToList(),
                                replay.Items.Select(item => Describe(item, false)).ToList(),
                                "The replay should give the items of the live run but for the close reason of the last tick");
    }

    /// <summary>
    /// Session events and version messages replay in the order of the live run.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamSessionEventsAndVersionsReplayInOrder()
    {
        // Arrange
        var wire = Wire(VersionFrame(2),
                        SessionStartFrame("Capital"),
                        IslandFrame(1, 100),
                        SessionEndFrame(),
                        SessionEndFrame(),
                        SessionStartFrame("Again"),
                        IslandFrame(2, 200),
                        SessionEndFrame());
        var live = await RunLiveAsync(wire);

        // Act
        var replay = await RunReplayAsync(live.Recording, "max");

        // Assert
        string[] expectedKinds = [
                                     "Version",
                                     "SessionStart",
                                     "Tick",
                                     "SessionEnd",
                                     "RepeatedSessionEnd",
                                     "SessionStart",
                                     "Tick",
                                     "SessionEnd",
                                     "EndOfStream"
                                 ];

        Assert.AreSequenceEqual(expectedKinds, replay.Items.Select(item => item.Kind.ToString()).ToArray(), "The replay should give the events in the order they were sent");
        Assert.AreSequenceEqual(live.Items.Select(item => Describe(item, true)).ToList(),
                                replay.Items.Select(item => Describe(item, true)).ToList(),
                                "The replay should give the items of the live run, headlines and versions included");
    }

    /// <summary>
    /// A frame the reader skips live is skipped in the replay, and the replay goes on.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamSkippedFramesReplayAsSkipped()
    {
        // Arrange
        byte[][] frames = [
                              VersionFrame(2),
                              [],
                              [9, 1, 2],
                              [3, 7],
                              SessionStartFrame("Capital"),
                              [4],
                              SessionEndFrame()
                          ];
        var expected = await ReadResultsAsync(new PipeMessageReader(new MemoryStream(Wire(frames))));
        using var replay = NewReplay(RecordingOf(frames));

        // Act
        var results = await ReadResultsAsync(new PipeMessageReader(replay));

        // Assert
        Assert.HasCount(8, results, "The replay should give a result for each frame and the end");
        Assert.AreSequenceEqual(expected, results, "The replay should skip the frames the live reader skips");
        Assert.IsNull(replay.Problem, "Skipped frames are no problem of the file");
    }

    /// <summary>
    /// Frames before the version message, and after another version, give the protocol mismatch of a live read.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamFrameBeforeVersionGivesProtocolMismatch()
    {
        // Arrange
        byte[][] frames = [
                              IslandFrame(1, 100),
                              VersionFrame(3),
                              IslandFrame(1, 100),
                              VersionFrame(2),
                              IslandFrame(1, 100)
                          ];
        var expected = await ReadResultsAsync(new PipeMessageReader(new MemoryStream(Wire(frames))));
        using var replay = NewReplay(RecordingOf(frames));

        // Act
        var results = await ReadResultsAsync(new PipeMessageReader(replay));

        // Assert
        Assert.HasCount(6, results, "The replay should give a result for each frame and the end");
        Assert.StartsWith("Error ProtocolMismatch", results[0], "A frame before the version should give a protocol mismatch");
        Assert.AreSequenceEqual(expected, results, "The replay should apply the version gate as the live reader");
    }

    /// <summary>
    /// The island limit of a tick applies to replayed frames.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamTickLimitAppliesToReplayedFrames()
    {
        // Arrange
        var frames = new[] { VersionFrame(2) }.Concat(Enumerable.Range(1, PipeTickLimits.MaxIslands + 1).Select(guid => IslandFrame(guid, 100, false))).ToArray();
        var recording = Recording(frames.Select((frame, index) => (_start.AddMilliseconds(index), frame)).ToArray());
        var replay = await RunReplayAsync(recording, "max");

        // Act
        var items = replay.Items;

        // Assert
        Assert.HasCount(4, items, "The replay should give the version, the limit, the tick and the end");
        Assert.AreEqual(PipeTickResultKind.TickLimit, items[1].Kind, "The island over the limit should give a tick limit");
        Assert.AreEqual(PipeTickLimits.MaxIslands + 1, items[1].IslandKey!.Value.SessionGuid, "The limit should name the island over it");
        Assert.HasCount(PipeTickLimits.MaxIslands, items[2].Tick!.Islands, "The tick should hold the islands up to the limit");
        Assert.AreEqual(PipeTickCloseReason.StreamEnded, items[2].Tick!.CloseReason, "The tick should close when the replay ends");
    }

    /// <summary>
    /// A file without bytes is not a recording.
    /// </summary>
    /// <param name="content">The content of the file</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("")]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public async Task PipeReplayStreamEmptyFileIsNotARecording(string content)
    {
        // Arrange
        using var recording = new MemoryStream(Utf8(content));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, "No frame should be replayed");
        Assert.IsNotNull(problem, "The replay should end with a problem");
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem.Kind, "A file without a header is not a recording");
    }

    /// <summary>
    /// A first line that is not the header of this format is not a recording.
    /// </summary>
    /// <param name="firstLine">The first line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("hello")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"format\":\"other\",\"version\":1}")]
    [DataRow("{\"format\":\"Censio-Pipe-Recording\",\"version\":1}")]
    [DataRow("{\"format\":\"censio-pipe-recording\",\"version\":1}{}")]
    [DataRow("{\"time\":\"2026-11-01T17:09:00.0000000Z\",\"frame\":\"Ag==\"}")]
    public async Task PipeReplayStreamOtherFormatIsNotARecording(string firstLine)
    {
        // Arrange
        using var recording = new MemoryStream(Utf8($"{firstLine}\n{Line(1, _firstFrame)}"));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, "No frame should be replayed");
        Assert.IsNotNull(problem, "The replay should end with a problem");
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem.Kind, "A first line that is not the header makes the file no recording");
    }

    /// <summary>
    /// A header without the format name or the version is not a recording.
    /// </summary>
    /// <param name="header">The header</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("{\"format\":\"censio-pipe-recording\"}")]
    [DataRow("{\"version\":1}")]
    [DataRow("{\"format\":\"censio-pipe-recording\",\"started\":\"2026-11-01T17:09:00.0000000Z\"}")]
    [DataRow("{\"format\":\"censio-pipe-recording\",\"format\":\"censio-pipe-recording\",\"version\":1}")]
    [DataRow("{\"format\":\"censio-pipe-recording\",\"version\":1,\"version\":1}")]
    public async Task PipeReplayStreamHeaderWithoutVersionIsNotARecording(string header)
    {
        // Arrange
        using var recording = new MemoryStream(Utf8($"{header}\n{Line(1, _firstFrame)}"));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, "No frame should be replayed");
        Assert.IsNotNull(problem, "The replay should end with a problem");
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem.Kind, "A header that lacks a field, or has one twice, makes the file no recording");
    }

    /// <summary>
    /// A header field of another JSON type, or an invalid one, is not a recording and never throws.
    /// </summary>
    /// <param name="description">What the header holds</param>
    /// <param name="header">The header bytes</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("format as number", "{\"format\":5,\"version\":1}")]
    [DataRow("format as null", "{\"format\":null,\"version\":1}")]
    [DataRow("format as object", "{\"format\":{},\"version\":1}")]
    [DataRow("format as array", "{\"format\":[\"censio-pipe-recording\"],\"version\":1}")]
    [DataRow("version as text", "{\"format\":\"censio-pipe-recording\",\"version\":\"1\"}")]
    [DataRow("version as null", "{\"format\":\"censio-pipe-recording\",\"version\":null}")]
    [DataRow("version as fraction", "{\"format\":\"censio-pipe-recording\",\"version\":1.5}")]
    [DataRow("version as true", "{\"format\":\"censio-pipe-recording\",\"version\":true}")]
    [DataRow("version as object", "{\"format\":\"censio-pipe-recording\",\"version\":{}}")]
    [DataRow("version over Int32", "{\"format\":\"censio-pipe-recording\",\"version\":3000000000}")]
    public async Task PipeReplayStreamHeaderFieldOfWrongTypeIsNotARecording(string description, string header)
    {
        // Arrange
        using var recording = new MemoryStream(Utf8($"{header}\n{Line(1, _firstFrame)}"));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, $"No frame should be replayed ({description})");
        Assert.IsNotNull(problem, $"The replay should end with a problem ({description})");
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem.Kind, $"A header field of the wrong type makes the file no recording ({description})");
    }

    /// <summary>
    /// A header whose format name holds a raw byte that is no UTF-8 is not a recording and never throws.
    /// </summary>
    /// <param name="before">The header text before the raw byte</param>
    /// <param name="after">The header text after the raw byte</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("{\"format\":\"", "\",\"version\":1}")]
    public async Task PipeReplayStreamHeaderWithRawByteIsNotARecording(string before, string after)
    {
        // Arrange
        using var recording = new MemoryStream([.. Utf8(before), 0xFF, .. Utf8(after + "\n"), .. Utf8(Line(1, _firstFrame))]);

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, "No frame should be replayed");
        Assert.IsNotNull(problem, "The replay should end with a problem");
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem.Kind, "A header that is no valid UTF-8 JSON makes the file no recording");
    }

    /// <summary>
    /// A header with another integer version is unsupported.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(99)]
    [DataRow(-1)]
    public async Task PipeReplayStreamOtherVersionIsUnsupported(int version)
    {
        // Arrange
        var header = string.Create(CultureInfo.InvariantCulture, $"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":{version}}}\n");
        using var recording = new MemoryStream(Utf8(header + Line(1, _firstFrame)));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, "No frame should be replayed");
        Assert.IsNotNull(problem, "The replay should end with a problem");
        Assert.AreEqual(PipeReplayProblemKind.UnsupportedVersion, problem.Kind, "Another version should be unsupported");
    }

    /// <summary>
    /// An invalid line ends the replay after the frames before it, with its line number.
    /// </summary>
    /// <param name="description">What the line holds</param>
    /// <param name="tail">The bytes from the invalid fourth line on</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DynamicData(nameof(InvalidLines))]
    public async Task PipeReplayStreamInvalidLineEndsAfterEarlierFrames(string description, byte[] tail)
    {
        // Arrange
        using var recording = new MemoryStream([.. Utf8(string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame))), .. tail]);

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.AreSequenceEqual(new[] { Convert.ToBase64String(_firstFrame), Convert.ToBase64String(_secondFrame) }, ToBase64(frames), $"The frames before the invalid line should be replayed ({description})");
        AssertInvalidLine(problem, 4, description);
    }

    /// <summary>
    /// A line over the longest line is invalid at once: the replay does not read it whole.
    /// </summary>
    /// <param name="lineLength">The length of the line in bytes</param>
    /// <param name="terminated">Whether a line feed ends the line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(PipeRecordingFormat.MaxLineLength + 1, true)]
    [DataRow(PipeRecordingFormat.MaxLineLength * 5, false)]
    public async Task PipeReplayStreamLineOverMaxLengthIsInvalidWithoutReadingItWhole(int lineLength, bool terminated)
    {
        // Arrange
        var head = Utf8(string.Concat(Header(), Line(1, _firstFrame), Line(2, _secondFrame)));
        var text = PaddedLine(string.Empty, lineLength) + (terminated ? "\n" : string.Empty);
        using var recording = new ChunkedStream([.. head, .. Utf8(text)], 4096);

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.HasCount(2, frames, "The frames before the long line should be replayed");
        AssertInvalidLine(problem, 4, "line over the maximum length");
        Assert.IsLessThan(2 * PipeRecordingFormat.MaxLineLength, recording.Offset, "The replay should stop reading soon after the longest line");
    }

    /// <summary>
    /// A line of the longest length, holding a frame of one mebibyte, is accepted, whatever ends it.
    /// </summary>
    /// <param name="terminator">The line terminator</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("")]
    public async Task PipeReplayStreamLineAtMaxLengthIsAccepted(string terminator)
    {
        // Arrange
        var big = new byte[MaxFrameBytes];

        big[0] = 9;
        big[^1] = 7;

        var line = PaddedLine(Convert.ToBase64String(big), PipeRecordingFormat.MaxLineLength);
        using var recording = new MemoryStream(Utf8(string.Concat(Header(), Line(1, _firstFrame), line, terminator)));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.AreSequenceEqual(new[] { Convert.ToBase64String(_firstFrame), Convert.ToBase64String(big) }, ToBase64(frames), "A frame of one mebibyte in a line of the longest length should be replayed");
        Assert.IsNull(problem, "A line of the longest length is no problem");
    }

    /// <summary>
    /// The line forms of the format are accepted.
    /// </summary>
    /// <param name="description">The form</param>
    /// <param name="file">The file</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DynamicData(nameof(AcceptedFiles))]
    public async Task PipeReplayStreamAcceptedLineForms(string description, byte[] file)
    {
        // Arrange
        using var recording = new MemoryStream(file);

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.AreSequenceEqual(new[] { Convert.ToBase64String(_firstFrame), Convert.ToBase64String(_secondFrame) }, ToBase64(frames), $"Both frames should be replayed ({description})");
        Assert.IsNull(problem, $"The form should be no problem ({description})");
    }

    /// <summary>
    /// An empty line anywhere but as the one last line is invalid.
    /// </summary>
    /// <param name="content">The file</param>
    /// <param name="expectedFrames">The number of frames before the empty line</param>
    /// <param name="expectedLine">The number of the empty line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("<H>\n<1>\n\n<2>\n", 1, 3)]
    [DataRow("<H>\r\n<1>\r\n\r\n<2>\r\n", 1, 3)]
    [DataRow("<H>\n\n<1>\n<2>\n", 0, 2)]
    [DataRow("<H>\r\n\r\n<1>\r\n<2>\r\n", 0, 2)]
    [DataRow("<H>\n<1>\n<2>\n\n\n", 2, 4)]
    [DataRow("<H>\n<1>\n<2>\n\n<1>\n", 2, 4)]
    public async Task PipeReplayStreamEmptyLineInsideIsInvalid(string content, int expectedFrames, long expectedLine)
    {
        // Arrange
        var file = content.Replace("<H>", Header().TrimEnd('\n'), StringComparison.Ordinal)
                          .Replace("<1>", Line(1, _firstFrame).TrimEnd('\n'), StringComparison.Ordinal)
                          .Replace("<2>", Line(2, _secondFrame).TrimEnd('\n'), StringComparison.Ordinal);
        using var recording = new MemoryStream(Utf8(file));

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.HasCount(expectedFrames, frames, "The frames before the empty line should be replayed");
        AssertInvalidLine(problem, expectedLine, "empty line");
    }

    /// <summary>
    /// A recording that cannot be read on ends with a read failure, after the frames read whole before it.
    /// </summary>
    /// <param name="wholeLines">The number of whole lines before the failing read</param>
    /// <param name="expectedFrames">The number of frames replayed</param>
    /// <param name="accessDenied">Whether the failing read throws an access error instead of an I/O error</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(0, 0, false)]
    [DataRow(1, 0, false)]
    [DataRow(2, 1, false)]
    [DataRow(3, 2, false)]
    [DataRow(0, 0, true)]
    [DataRow(1, 0, true)]
    [DataRow(2, 1, true)]
    [DataRow(3, 2, true)]
    public async Task PipeReplayStreamReadFailureIsReadFailed(int wholeLines, int expectedFrames, bool accessDenied)
    {
        // Arrange
        string[] lines = [Header(), Line(1, _firstFrame), Line(2, _secondFrame), Line(3, _firstFrame)];
        var failAt = lines.Take(wholeLines).Sum(line => Utf8(line).Length) + 5;
        using var recording = new ChunkedStream(Utf8(string.Concat(lines)), 7, failAt, accessDenied: accessDenied);

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.HasCount(expectedFrames, frames, "The frames read whole before the failure should be replayed");
        Assert.IsNotNull(problem, "The replay should end with a problem");
        Assert.AreEqual(PipeReplayProblemKind.ReadFailed, problem.Kind, "A recording that cannot be read on should give a read failure");
    }

    /// <summary>
    /// A recording of the header alone ends at once, without a problem and without waiting.
    /// </summary>
    /// <param name="content">The file</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("<H>\n")]
    [DataRow("<H>")]
    [DataRow("<H>\r\n")]
    public async Task PipeReplayStreamHeaderOnlyEndsAtOnce(string content)
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        using var recording = new MemoryStream(Utf8(content.Replace("<H>", Header().TrimEnd('\n'), StringComparison.Ordinal)));
        using var stream = new PipeReplayStream(recording, PipeReplaySpeed.RealTime, new PipeReplayClock(_start), waits);

        // Act
        var read = await stream.ReadAsync(new byte[4], TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(0, read, "A recording without frames should end at once");
        Assert.IsNull(stream.Problem, "A header-only recording is no problem");
        Assert.IsEmpty(waits.Delays, "Nothing should be waited for");
    }

    /// <summary>
    /// At real speed the replay waits the recorded pause before the second frame, and sets the clock after the wait.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamRealSpeedWaitsRecordedPause()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start.AddDays(-1));
        using var stream = NewReplay(Recording((_start, _secondFrame), (_start.AddSeconds(5), _secondFrame)), PipeReplaySpeed.RealTime, clock, waits);
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        var delaysAfterFirst = waits.Delays.Count;
        var clockAfterFirst = clock.GetUtcNow();

        // Act
        var second = reader.ReadFrameAsync(TestContext.CancellationToken).AsTask();

        await waits.WaitForTimersAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var clockWhileWaiting = clock.GetUtcNow();

        waits.Advance(TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));

        var completedEarly = second.IsCompleted;

        waits.Advance(TimeSpan.FromMilliseconds(1));

        var result = await second.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(0, delaysAfterFirst, "The first frame should not be waited for");
        Assert.AreEqual(_start, clockAfterFirst, "The clock should be set to the time of the first frame");
        Assert.AreEqual(TimeSpan.FromSeconds(5), waits.Delays[0], "The second frame should be waited for by the recorded pause");
        Assert.AreEqual(_start, clockWhileWaiting, "The clock should keep the time of the first frame during the wait");
        Assert.IsFalse(completedEarly, "The frame should not be served before the pause is over");
        Assert.AreEqual(PipeFrameResultKind.Frame, result.Kind, "The frame should be served when the pause is over");
        Assert.AreEqual(_start.AddSeconds(5), clock.GetUtcNow(), "The clock should be set to the time of the second frame");
    }

    /// <summary>
    /// At a factor the replay waits the recorded pause divided by it.
    /// </summary>
    /// <param name="speedText">The speed</param>
    /// <param name="expectedMilliseconds">The expected wait for a pause of five seconds</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("10", 500)]
    [DataRow("0.5", 10000)]
    [DataRow("1000", 5)]
    public async Task PipeReplayStreamFactorDividesPause(string speedText, long expectedMilliseconds)
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start);
        var expected = TimeSpan.FromMilliseconds(expectedMilliseconds);
        using var stream = NewReplay(Recording((_start, _secondFrame), (_start.AddSeconds(5), _secondFrame)), Speed(speedText), clock, waits);
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Act
        var second = reader.ReadFrameAsync(TestContext.CancellationToken).AsTask();

        await waits.WaitForTimersAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        waits.Advance(expected - TimeSpan.FromMilliseconds(1));

        var completedEarly = second.IsCompleted;

        waits.Advance(TimeSpan.FromMilliseconds(1));

        await second.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, waits.Delays[0], "The pause should be divided by the factor");
        Assert.IsFalse(completedEarly, "The frame should not be served before the scaled pause is over");
        Assert.AreEqual(_start.AddSeconds(5), clock.GetUtcNow(), "The frame should be stamped with its recorded time at every speed");
    }

    /// <summary>
    /// At maximum speed the replay does not wait, whatever the gaps.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamMaximumSpeedDoesNotWait()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start);
        DateTimeOffset[] times = [_start, _start.AddDays(1), _start.AddDays(40), _start.AddDays(2)];
        using var stream = NewReplay(Recording(times.Select(time => (time, _secondFrame)).ToArray()), PipeReplaySpeed.Maximum, clock, waits);

        // Act
        var stamped = await ReadClockTimesAsync(stream, clock, waits);

        // Assert
        Assert.AreSequenceEqual(times, stamped, "Every frame should be stamped with its recorded time");
        Assert.IsEmpty(waits.Delays, "Maximum speed should not wait");
    }

    /// <summary>
    /// A time that goes backwards or repeats is not waited for and is stamped unchanged; a later forward pause is waited
    /// for as usual.
    /// </summary>
    /// <param name="speedText">The speed</param>
    /// <param name="expectedWaitSeconds">The expected total wait in seconds</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("1", 12)]
    [DataRow("max", 0)]
    public async Task PipeReplayStreamBackwardOrRepeatedTimeDoesNotWaitAndKeepsTime(string speedText, long expectedWaitSeconds)
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start.AddDays(10));
        DateTimeOffset[] times = [_start, _start, _start.AddSeconds(-10), _start.AddSeconds(-10), _start.AddSeconds(2)];
        using var stream = NewReplay(Recording(times.Select(time => (time, _secondFrame)).ToArray()), Speed(speedText), clock, waits);

        // Act
        var stamped = await ReadClockTimesAsync(stream, clock, waits);

        // Assert
        Assert.AreSequenceEqual(times, stamped, "Every frame should be stamped with its recorded time, backwards or repeated");
        Assert.AreEqual(TimeSpan.FromSeconds(expectedWaitSeconds), TimeSpan.FromTicks(waits.Delays.Sum(delay => delay.Ticks)), "Only the forward pause should be waited for");
    }

    /// <summary>
    /// A cancelled token ends the wait with an <see cref="OperationCanceledException" />.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamCancelledWaitThrowsCancellation()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        using var stream = NewReplay(Recording((_start, _secondFrame), (_start.AddSeconds(5), _secondFrame)), PipeReplaySpeed.RealTime, new PipeReplayClock(_start), waits);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        var second = reader.ReadFrameAsync(cancellation.Token).AsTask();

        await waits.WaitForTimersAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => second, "A cancelled wait should end with a cancellation");
    }

    /// <summary>
    /// A gap over the longest delay is waited for in steps of at most a day, not refused.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamGapOverDelayLimitIsWaitedInSteps()
    {
        // Arrange
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start);
        using var stream = NewReplay(Recording((_start, _secondFrame), (_start.AddDays(60), _secondFrame)), PipeReplaySpeed.RealTime, clock, waits);
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        var second = reader.ReadFrameAsync(TestContext.CancellationToken).AsTask();

        // Act
        await waits.RunWaitsAsync(second, TestContext.CancellationToken);

        var result = await second;

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Frame, result.Kind, "The frame should be served after the gap");
        Assert.IsLessThanOrEqualTo(TimeSpan.FromDays(1), waits.Delays.Max(), "No single wait should be longer than a day");
        Assert.AreEqual(TimeSpan.FromDays(60), TimeSpan.FromTicks(waits.Delays.Sum(delay => delay.Ticks)), "The steps should add up to the gap");
        Assert.AreEqual(_start.AddDays(60), clock.GetUtcNow(), "The clock should be set to the time after the gap");
    }

    /// <summary>
    /// A frame recorded 60 days before an island, followed one second later by the next frame, replays through the
    /// assembler with the items of the recorded times and no exception.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamBackwardTimeOverDelayLimitAfterIslandDoesNotThrow()
    {
        // Arrange
        var islandTime = _start.AddSeconds(1);
        var backTime = islandTime.AddDays(-60);
        var recording = Recording((_start, VersionFrame(2)), (islandTime, IslandFrame(1, 100)), (backTime, _emptyFrame), (backTime.AddSeconds(1), IslandFrame(2, 200)));

        // Act
        var replay = await RunReplayAsync(recording, "1");

        // Assert
        Assert.HasCount(5, replay.Items, "The replay should give the version, the skipped frame, two ticks and the end");
        Assert.AreEqual(PipeTickResultKind.Version, replay.Items[0].Kind, "The version should come first");
        Assert.AreEqual(PipeTickResultKind.Error, replay.Items[1].Kind, "The skipped frame should give an error");
        Assert.AreEqual(PipeTickCloseReason.GameTimeChanged, replay.Items[2].Tick!.CloseReason, "The first island should close when the game time changes");
        Assert.AreEqual(islandTime, replay.Items[2].Tick!.ReceiveTime, "The first tick should carry the recorded time of its island");
        Assert.AreEqual(PipeTickCloseReason.StreamEnded, replay.Items[3].Tick!.CloseReason, "The last island should close when the replay ends");
        Assert.AreEqual(backTime.AddSeconds(1), replay.Items[3].Tick!.ReceiveTime, "The second tick should carry the recorded time of its island");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, replay.Items[4].Kind, "The end should come last");
    }

    /// <summary>
    /// A header line over the longest line makes the file no recording, whatever ends it.
    /// </summary>
    /// <param name="terminated">Whether a line feed ends the line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PipeReplayStreamHeaderOverMaxLengthIsNotARecording(bool terminated)
    {
        // Arrange
        var text = PaddedLine(string.Empty, PipeRecordingFormat.MaxLineLength + 1) + (terminated ? "\n" : string.Empty);
        using var recording = new ChunkedStream(Utf8(text), 4096);

        // Act
        var (frames, problem) = await ReplayFramesAsync(recording);

        // Assert
        Assert.IsEmpty(frames, "No frame follows a header that is too long");
        Assert.IsNotNull(problem, "A header over the maximum line length should end the replay with a problem");
        Assert.AreEqual(PipeReplayProblemKind.NotARecording, problem.Kind, "A header that is too long makes the file no recording");
        Assert.IsNull(problem.LineNumber, "The problem of the header names no line");
    }

    /// <summary>
    /// The replay stream is a forward-only, read-only stream: everything else is not supported.
    /// </summary>
    [TestMethod]
    public void PipeReplayStreamUnsupportedMembersThrowNotSupported()
    {
        // Arrange
        using var stream = NewReplay(Utf8(Header()));
        var buffer = new byte[4];

        // Act
        // Assert
        Assert.IsTrue(stream.CanRead, "The stream should be readable");
        Assert.IsFalse(stream.CanSeek, "The stream should not seek");
        Assert.IsFalse(stream.CanWrite, "The stream should not be writable");
        Assert.Throws<NotSupportedException>(() => _ = stream.Length, "Length should not be supported");
        Assert.Throws<NotSupportedException>(() => _ = stream.Position, "Reading the position should not be supported");
        Assert.Throws<NotSupportedException>(() => stream.Position = 0, "Setting the position should not be supported");
        Assert.Throws<NotSupportedException>(() => stream.Read(buffer, 0, buffer.Length), "A synchronous read should not be supported");
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin), "Seek should not be supported");
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0), "SetLength should not be supported");
        Assert.Throws<NotSupportedException>(() => stream.Write(buffer, 0, buffer.Length), "Write should not be supported");
        stream.Flush();
    }

    /// <summary>
    /// A read into an empty buffer returns zero and consumes nothing of the replay.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task PipeReplayStreamReadIntoEmptyBufferReturnsZeroAndKeepsTheFrames()
    {
        // Arrange
        using var stream = NewReplay(Utf8(string.Concat(Header(), Line(1, _firstFrame))));

        // Act
        var empty = await stream.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken);
        var buffer = new byte[16];
        var read = await stream.ReadAsync(buffer, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(0, empty, "A read into an empty buffer should return zero");
        Assert.AreEqual(4 + _firstFrame.Length, read, "The frame should still be served after the empty read");
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
    /// Creates the JSON of a frame line with raw field values.
    /// </summary>
    /// <param name="time">The JSON of the time</param>
    /// <param name="frame">The JSON of the frame</param>
    /// <returns>The JSON object</returns>
    private static string FrameLineJson(string time, string frame)
    {
        return $"{{\"time\":{time},\"frame\":{frame}}}";
    }

    /// <summary>
    /// Creates the bytes from an invalid fourth line on: the line, a line feed and a valid frame line.
    /// </summary>
    /// <param name="line">The invalid line</param>
    /// <returns>The bytes</returns>
    private static byte[] Tail(string line)
    {
        return Utf8(string.Concat(line, "\n", Line(5, _secondFrame)));
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
    /// Creates a recording from lines with their receive times.
    /// </summary>
    /// <param name="lines">The lines</param>
    /// <returns>The file bytes</returns>
    private static byte[] Recording(params (DateTimeOffset Time, byte[] Frame)[] lines)
    {
        var header = PipeRecordingFormat.FormatHeader(lines.Length > 0 ? lines[0].Time : _start);

        return Utf8(string.Concat(header, string.Concat(lines.Select(line => PipeRecordingFormat.FormatFrameLine(line.Time, line.Frame)))));
    }

    /// <summary>
    /// Creates a recording of frames one second apart, from the start on.
    /// </summary>
    /// <param name="frames">The frames</param>
    /// <returns>The file bytes</returns>
    private static byte[] RecordingOf(byte[][] frames)
    {
        return Recording(frames.Select((frame, index) => (_start.AddSeconds(index), frame)).ToArray());
    }

    /// <summary>
    /// Creates the bytes of frames with their length prefixes.
    /// </summary>
    /// <param name="frames">The frames</param>
    /// <returns>The bytes</returns>
    private static byte[] Wire(params byte[][] frames)
    {
        return [.. frames.SelectMany(PipeFrameBuilder.WithPrefix)];
    }

    /// <summary>
    /// Creates a version frame.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The frame</returns>
    private static byte[] VersionFrame(int version)
    {
        return new PipeFrameBuilder().Byte(0).Int32(version).Build();
    }

    /// <summary>
    /// Creates a session-start frame.
    /// </summary>
    /// <param name="headline">The headline</param>
    /// <returns>The frame</returns>
    private static byte[] SessionStartFrame(string headline)
    {
        return new PipeFrameBuilder().Byte(1).String(headline).Build();
    }

    /// <summary>
    /// Creates a session-end frame.
    /// </summary>
    /// <returns>The frame</returns>
    private static byte[] SessionEndFrame()
    {
        return new PipeFrameBuilder().Byte(2).Build();
    }

    /// <summary>
    /// Creates an island frame.
    /// </summary>
    /// <param name="sessionGuid">The session GUID, which tells the islands apart</param>
    /// <param name="timestamp">The game time</param>
    /// <param name="withEntry">Whether the island holds a production entry</param>
    /// <returns>The frame</returns>
    private static byte[] IslandFrame(int sessionGuid, long timestamp, bool withEntry = true)
    {
        var builder = new PipeFrameBuilder().Byte(3).Byte(7).Byte(1).Byte(0).Int32(sessionGuid).String("Ostia").Int64(timestamp);

        return withEntry
                   ? builder.Int32(1).Entry(sessionGuid + 10, [new(1, 2)], [new(3, 4)]).Build()
                   : builder.Int32(0).Build();
    }

    /// <summary>
    /// Creates the frames of the main scenario: skipped frames, repeated islands, session events and a game time change.
    /// </summary>
    /// <returns>The frames</returns>
    private static byte[][] Scenario()
    {
        return
        [
            VersionFrame(2),
            SessionStartFrame("Capital"),
            IslandFrame(1, 100),
            IslandFrame(2, 100),
            [],
            IslandFrame(3, 200),
            [9, 1, 2],
            IslandFrame(3, 200),
            SessionEndFrame(),
            SessionEndFrame(),
            SessionStartFrame("Again"),
            IslandFrame(1, 300)
        ];
    }

    /// <summary>
    /// Creates a read result as the message reader gives it for a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The result</returns>
    private static PipeReadResult Read(byte[] frame)
    {
        return PipeMessageParser.Parse(frame).WithFrame(frame);
    }

    /// <summary>
    /// Describes a pipeline result with everything the replay must reproduce.
    /// </summary>
    /// <param name="item">The result</param>
    /// <param name="includeCloseReason">Whether the close reason of a tick is part of the description</param>
    /// <returns>The description</returns>
    private static string Describe(PipeTickResult item, bool includeCloseReason)
    {
        if (item.Tick is null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{item.Kind} headline={item.Headline} version={item.Version} error={item.Error} key={item.IslandKey}");
        }

        var reason = includeCloseReason ? item.Tick.CloseReason.ToString() : "-";
        var islands = string.Join(";", item.Tick.Islands.Select(DescribeIsland));

        return string.Create(CultureInfo.InvariantCulture, $"Tick {reason} game={item.Tick.GameTime} received={item.Tick.ReceiveTime:O} [{islands}]");
    }

    /// <summary>
    /// Describes an island of a tick with its data.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The description</returns>
    private static string DescribeIsland(PipeTickIsland island)
    {
        var entries = string.Join(",", island.Entries.Select(DescribeEntry));

        return string.Create(CultureInfo.InvariantCulture, $"{island.Key.SessionGuid}/{island.Key.IslandId}/{island.Key.AreaIndex} session={island.SessionId} {island.Name} entries=[{entries}]");
    }

    /// <summary>
    /// Describes a production entry with all its values.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The description</returns>
    private static string DescribeEntry(ProductionEntry entry)
    {
        var workforce = string.Join("+", entry.WorkforceAmounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
        var buildings = string.Join("+", entry.BuildingAmounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));

        return string.Create(CultureInfo.InvariantCulture,
                             $"{entry.ProductGuid}:{entry.Generation:R}:{entry.Consumption:R}:{entry.Delta:R}:{entry.PerfectGeneration:R}:{entry.PerfectConsumption:R}:{entry.BuildingCount}:{entry.Maintenance}:{entry.Income:R}:{entry.Profit}:{entry.SummedProductivity:R}:{entry.AverageProductivity:R}:w[{workforce}]:b[{buildings}]");
    }

    /// <summary>
    /// Describes a read result.
    /// </summary>
    /// <param name="result">The result</param>
    /// <returns>The description</returns>
    private static string DescribeRead(PipeReadResult result)
    {
        return result.Kind switch
               {
                   PipeReadResultKind.Message => DescribeMessage(result.Message!),
                   PipeReadResultKind.Error => $"Error {result.Error}",
                   _ => "End"
               };
    }

    /// <summary>
    /// Describes a message with its fields.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The description</returns>
    private static string DescribeMessage(PipeMessage message)
    {
        return message switch
               {
                   VersionMessage version => $"Version {version.Version}",
                   SessionStartMessage start => $"SessionStart {start.Headline}",
                   AreaProductionStatisticsMessage area => $"Area {area.SessionGuid} {area.AreaName} {area.Timestamp} {area.Entries.Count}",
                   _ => message.MessageType.ToString()
               };
    }

    /// <summary>
    /// Encodes frames as base64 texts, which compare by value.
    /// </summary>
    /// <param name="frames">The frames</param>
    /// <returns>The texts</returns>
    private static List<string> ToBase64(IEnumerable<byte[]> frames)
    {
        return frames.Select(Convert.ToBase64String).ToList();
    }

    /// <summary>
    /// Reads the receive times of the frame lines of a recording.
    /// </summary>
    /// <param name="recording">The recording bytes</param>
    /// <returns>The times in file order</returns>
    private static List<DateTimeOffset> RecordedTimes(byte[] recording)
    {
        return Encoding.UTF8.GetString(recording)
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Skip(1)
                            .Select(line => PipeRecordingFormat.TryParseFrameLine(Utf8(line), out var time, out _) ? time : throw new InvalidOperationException("The recording holds an invalid line"))
                            .ToList();
    }

    /// <summary>
    /// Asserts that a replay ended with an invalid line.
    /// </summary>
    /// <param name="problem">The problem of the replay</param>
    /// <param name="line">The expected line number</param>
    /// <param name="description">What the line holds</param>
    private static void AssertInvalidLine(PipeReplayProblem? problem, long line, string description)
    {
        Assert.IsNotNull(problem, $"The replay should end with a problem ({description})");
        Assert.AreEqual(PipeReplayProblemKind.InvalidLine, problem.Kind, $"The line should be invalid ({description})");
        Assert.AreEqual(line, problem.LineNumber, $"The problem should name the line ({description})");
    }

    /// <summary>
    /// Parses a speed that must be valid.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The speed</returns>
    private static PipeReplaySpeed Speed(string text)
    {
        Assert.IsTrue(PipeReplaySpeed.TryParse(text, out var speed), "The speed should be valid");

        return speed;
    }

    /// <summary>
    /// Creates a replay stream at maximum speed over a recording.
    /// </summary>
    /// <param name="recording">The recording bytes</param>
    /// <returns>The stream</returns>
    private static PipeReplayStream NewReplay(byte[] recording)
    {
        return NewReplay(recording, PipeReplaySpeed.Maximum, new PipeReplayClock(_start), TimeProvider.System);
    }

    /// <summary>
    /// Creates a replay stream over a recording.
    /// </summary>
    /// <param name="recording">The recording bytes</param>
    /// <param name="speed">The speed</param>
    /// <param name="clock">The replay clock</param>
    /// <param name="waitProvider">The time provider the pauses are waited on</param>
    /// <returns>The stream</returns>
    private static PipeReplayStream NewReplay(byte[] recording, PipeReplaySpeed speed, PipeReplayClock clock, TimeProvider waitProvider)
    {
        return new PipeReplayStream(new MemoryStream(recording), speed, clock, waitProvider);
    }

    /// <summary>
    /// Replays a recording at maximum speed and reads its frames to the end.
    /// </summary>
    /// <param name="recording">The recording</param>
    /// <returns>The frames and the problem the replay ended with, if any</returns>
    private async Task<(List<byte[]> Frames, PipeReplayProblem? Problem)> ReplayFramesAsync(Stream recording)
    {
        using var stream = new PipeReplayStream(recording, PipeReplaySpeed.Maximum, new PipeReplayClock(_start), TimeProvider.System);

        var reader = new PipeFrameReader(stream);
        var frames = new List<byte[]>();
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        while (result.Kind == PipeFrameResultKind.Frame && frames.Count < 10000)
        {
            frames.Add(result.Frame.ToArray());

            result = await reader.ReadFrameAsync(TestContext.CancellationToken);
        }

        Assert.AreEqual(PipeFrameResultKind.EndOfStream, result.Kind, "The replay should end at a frame boundary");

        return (frames, stream.Problem);
    }

    /// <summary>
    /// Reads one frame, advancing through the waits the read starts.
    /// </summary>
    /// <param name="reader">The frame reader</param>
    /// <param name="waits">The time provider the stream waits on</param>
    /// <returns>The result of the read</returns>
    private async Task<PipeFrameResult> ReadFrameWithWaitsAsync(PipeFrameReader reader, RecordingTimeProvider waits)
    {
        var read = StartFrameRead(reader);

        await waits.RunWaitsAsync(read, TestContext.CancellationToken);

        return await read;
    }

    /// <summary>
    /// Starts reading one frame.
    /// </summary>
    /// <param name="reader">The frame reader</param>
    /// <returns>The task of the read</returns>
    private Task<PipeFrameResult> StartFrameRead(PipeFrameReader reader)
    {
        return reader.ReadFrameAsync(TestContext.CancellationToken).AsTask();
    }

    /// <summary>
    /// Reads the frames of a replay stream one by one and takes the replay clock after each.
    /// </summary>
    /// <param name="stream">The replay stream</param>
    /// <param name="clock">The replay clock of the stream</param>
    /// <param name="waits">The time provider the stream waits on</param>
    /// <returns>The clock reading after each frame</returns>
    private async Task<List<DateTimeOffset>> ReadClockTimesAsync(PipeReplayStream stream, PipeReplayClock clock, RecordingTimeProvider waits)
    {
        var reader = new PipeFrameReader(stream);
        var times = new List<DateTimeOffset>();
        var result = await ReadFrameWithWaitsAsync(reader, waits);

        while (result.Kind == PipeFrameResultKind.Frame && times.Count < 1000)
        {
            times.Add(clock.GetUtcNow());

            result = await ReadFrameWithWaitsAsync(reader, waits);
        }

        return times;
    }

    /// <summary>
    /// Reads a message source to its end.
    /// </summary>
    /// <param name="source">The source</param>
    /// <returns>The descriptions of the results, the end last</returns>
    private async Task<List<string>> ReadResultsAsync(PipeMessageReader source)
    {
        var results = new List<string>();
        var result = await source.ReadAsync(TestContext.CancellationToken);

        results.Add(DescribeRead(result));

        while (result.Kind != PipeReadResultKind.EndOfStream && results.Count < 1000)
        {
            result = await source.ReadAsync(TestContext.CancellationToken);

            results.Add(DescribeRead(result));
        }

        return results;
    }

    /// <summary>
    /// Reads a tick assembler to its end.
    /// </summary>
    /// <param name="assembler">The assembler</param>
    /// <returns>All results, the end last</returns>
    private async Task<List<PipeTickResult>> DrainAsync(PipeTickAssembler assembler)
    {
        var results = new List<PipeTickResult>();
        var result = await assembler.ReadAsync(TestContext.CancellationToken);

        results.Add(result);

        while (result.Kind != PipeTickResultKind.EndOfStream && results.Count < 1000)
        {
            result = await assembler.ReadAsync(TestContext.CancellationToken);

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Runs the frames through the tick assembler on a fake clock that moves at every read of the stream, and records
    /// them through the read observer.
    /// </summary>
    /// <param name="wire">The bytes of the stream</param>
    /// <returns>The recording, the items and the number of reads that delivered bytes</returns>
    private async Task<(byte[] Recording, List<PipeTickResult> Items, int DeliveredReads)> RunLiveAsync(byte[] wire)
    {
        var clock = new FakeTimeProvider(_start);
        var sink = new ControlledWriteStream();

        await using var recorder = new PipeRecorder(_ => sink);

        using var stream = new ChunkedClockStream(wire, ChunkSize, () => clock.Advance(_step));
        using var assembler = new PipeTickAssembler(new PipeMessageReader(stream), clock, recorder.Record);

        var items = await DrainAsync(assembler);

        await recorder.CompleteAsync(TestContext.CancellationToken);

        return (sink.ToArray(), items, stream.DeliveredReadCount);
    }

    /// <summary>
    /// Runs a version, an island and a quiet period through the tick assembler, with the read pending while the quiet
    /// period ends, and records the frames through the read observer.
    /// </summary>
    /// <param name="laterIslandFollows">Whether a second island arrives after the quiet period, before the end</param>
    /// <returns>The recording and the items</returns>
    private async Task<(byte[] Recording, List<PipeTickResult> Items)> RunQuietLiveAsync(bool laterIslandFollows)
    {
        var clock = new FakeTimeProvider(_start);
        var source = new ScriptedPipeMessageSource();
        var sink = new ControlledWriteStream();

        await using var recorder = new PipeRecorder(_ => sink);

        source.Enqueue(Read(VersionFrame(2)), () => clock.SetUtcNow(_start.AddSeconds(1)));
        source.Enqueue(Read(IslandFrame(1, 100)), () => clock.SetUtcNow(_start.AddSeconds(2)));

        var held = source.EnqueueHeld();

        source.Enqueue(PipeReadResult.End, () => clock.SetUtcNow(_start.AddSeconds(41)));

        using var assembler = new PipeTickAssembler(source, clock, recorder.Record);

        var items = new List<PipeTickResult>
                    {
                        await assembler.ReadAsync(TestContext.CancellationToken)
                    };
        var quietRequest = assembler.ReadAsync(TestContext.CancellationToken).AsTask();

        clock.Advance(PipeTickLimits.QuietPeriod);

        items.Add(await quietRequest.WaitAsync(_guard, TestContext.CancellationToken));

        if (laterIslandFollows)
        {
            clock.SetUtcNow(_start.AddSeconds(40));
            held.Release(Read(IslandFrame(2, 200)));
        }
        else
        {
            held.Release(PipeReadResult.End);
        }

        items.AddRange(await DrainAsync(assembler));

        await recorder.CompleteAsync(TestContext.CancellationToken);

        return (sink.ToArray(), items);
    }

    /// <summary>
    /// Replays a recording through the message reader and the tick assembler on the replay clock.
    /// </summary>
    /// <param name="recording">The recording</param>
    /// <param name="speedText">The speed</param>
    /// <returns>The items and the waits the replay started</returns>
    private async Task<(List<PipeTickResult> Items, IReadOnlyList<TimeSpan> Delays)> RunReplayAsync(byte[] recording, string speedText)
    {
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start);

        using var stream = NewReplay(recording, Speed(speedText), clock, waits);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(stream), clock);

        var drain = DrainAsync(assembler);

        await waits.RunWaitsAsync(drain, TestContext.CancellationToken);

        return (await drain, waits.Delays);
    }

    #endregion // Methods
}