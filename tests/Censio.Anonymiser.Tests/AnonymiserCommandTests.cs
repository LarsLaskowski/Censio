using System.Diagnostics;
using System.Text;

using Censio.Pipe;
using Censio.Pipe.Tests;

namespace Censio.Anonymiser.Tests;

/// <summary>
/// Tests for <see cref="AnonymiserCommand" />.
/// </summary>
[TestClass]
public sealed class AnonymiserCommandTests
{
    #region Constants

    /// <summary>
    /// The file name of the input recording.
    /// </summary>
    private const string InputName = "recording.jsonl";

    /// <summary>
    /// The file name of the output.
    /// </summary>
    private const string OutputName = "anonymised.jsonl";

    /// <summary>
    /// The usage line.
    /// </summary>
    private const string Usage = "Usage: Censio.Anonymiser <recording> <output>";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the recordings.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a blocking command from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A version 2 frame.
    /// </summary>
    private static readonly byte[] _version2 = [0, 2, 0, 0, 0];

    /// <summary>
    /// A session-end frame.
    /// </summary>
    private static readonly byte[] _sessionEnd = [2];

    /// <summary>
    /// The folder of the files of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-anonymiser-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymiserCommandTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public AnonymiserCommandTests(TestContext testContext)
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
    /// Gets the recordings with a frame the anonymiser cannot decode, with the number of that frame's line.
    /// </summary>
    private static IEnumerable<object[]> UndecodableRecordings
    {
        get
        {
            var island = Island();

            yield return ["unknown type", RecordingOf(_version2, [9, 1, 2]), 3L];
            yield return ["malformed island", RecordingOf(_version2, island[..^1]), 3L];
            yield return ["island before Version", RecordingOf(island, _version2), 2L];
            yield return ["session end before Version", RecordingOf(_sessionEnd, _version2), 2L];
            yield return ["island after Version 3", RecordingOf(_version2, [0, 3, 0, 0, 0], island), 4L];
        }
    }

    /// <summary>
    /// Gets the recordings the replay refuses too, with the reason it gives.
    /// </summary>
    private static IEnumerable<object[]> RefusedRecordings
    {
        get
        {
            yield return ["no recording", Encoding.UTF8.GetBytes("hello\n"), "not a recording"];
            yield return ["version 2 of the format", Encoding.UTF8.GetBytes($"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":2}}\n"), "unsupported version"];
            yield return ["invalid third line", Encoding.UTF8.GetBytes(string.Concat(PipeRecordingFormat.FormatHeader(_start), PipeRecordingFormat.FormatFrameLine(_start, _version2), "not json\n")), "invalid line 3"];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Deletes the folder of the test.
    /// </summary>
    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// A successful run writes the output and prints only the frame count and the output as typed; no offset or factor.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task AnonymiserCommandSuccessPrintsOnlyCountAndOutput()
    {
        // Arrange
        var input = WriteInput(RecordingOf(_version2, SessionStart("Alice"), Island()));
        var output = Path.Combine(_root, ".", OutputName);

        // Act
        var run = await RunAsync([input, output], TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(AnonymiserCommand.SuccessCode, run.Code, "A successful run should exit with 0");
        Assert.AreEqual($"Anonymised 3 frames to {output}{Environment.NewLine}", run.Output, "Only the frame count and the output as typed should be printed");
        Assert.AreEqual(string.Empty, run.Error, "Nothing should be printed on standard error");
        Assert.AreSequenceEqual(new[] { OutputName, InputName }, FolderEntries(), "Only the output should be added, no temporary file left");
        Assert.StartsWith($"{{\"format\":\"{PipeRecordingFormat.FormatName}\"", await File.ReadAllTextAsync(output, TestContext.CancellationToken), "The output should be a recording");
    }

    /// <summary>
    /// A standard output that fails after the output is in place does not turn the run into an error: the run succeeds,
    /// the complete output stays, nothing is printed on standard error and no temporary file is left.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task AnonymiserCommandFailingStandardOutputStillSucceeds()
    {
        // Arrange
        var input = WriteInput(RecordingOf(_version2, SessionStart("Alice"), Island()));
        var output = Path.Combine(_root, OutputName);
        var standardOutput = new FailingTextWriter();

        // Act
        var run = await RunWithOutputAsync([input, output], standardOutput);

        // Assert
        var lines = (await File.ReadAllTextAsync(output, TestContext.CancellationToken)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.AreEqual(AnonymiserCommand.SuccessCode, run.Code, "A failing standard output should not fail the run");
        Assert.AreEqual(string.Empty, run.Error, "Nothing should be printed on standard error");
        Assert.AreEqual(1, standardOutput.WriteCount, "The success line should have been tried once");
        Assert.AreSequenceEqual(new[] { OutputName, InputName }, FolderEntries(), "The output should stay and no temporary file should be left");
        Assert.HasCount(4, lines, "The output should hold the header and the three frame lines");
        Assert.IsNull(PipeRecordingFormat.CheckHeader(Encoding.UTF8.GetBytes(lines[0])), "The output should start with a valid header");
        Assert.AreEqual(3, lines.Skip(1).Count(line => PipeRecordingFormat.TryParseFrameLine(Encoding.UTF8.GetBytes(line), out _, out _)), "Every frame line of the output should be valid");
    }

    /// <summary>
    /// The input is never changed by a run.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task AnonymiserCommandLeavesInputUnchanged()
    {
        // Arrange
        var content = RecordingOf(_version2, SessionStart("Alice"), Island());
        var input = WriteInput(content);
        var written = File.GetLastWriteTimeUtc(input);

        // Act
        var run = await RunAsync([input, Path.Combine(_root, OutputName)], TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(AnonymiserCommand.SuccessCode, run.Code, "The run should succeed");
        Assert.AreSequenceEqual(content, await File.ReadAllBytesAsync(input, TestContext.CancellationToken), "The input's bytes should be unchanged");
        Assert.AreEqual(written, File.GetLastWriteTimeUtc(input), "The input should not be written");
    }

    /// <summary>
    /// A missing input prints the replay's reason.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task AnonymiserCommandMissingInputPrintsFileNotFound()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        // Act
        var run = await RunAsync([Path.Combine(_root, InputName), Path.Combine(_root, OutputName)], TestContext.CancellationToken);

        // Assert
        AssertError(run, "file not found");
        Assert.IsEmpty(FolderEntries(), "No file should be written");
    }

    /// <summary>
    /// A recording the replay refuses prints the replay's reason and leaves no output.
    /// </summary>
    /// <param name="description">What is wrong with the recording</param>
    /// <param name="content">The recording</param>
    /// <param name="reason">The expected reason</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(RefusedRecordings))]
    public async Task AnonymiserCommandRefusedRecordingPrintsReplayReason(string description, byte[] content, string reason)
    {
        // Arrange
        var input = WriteInput(content);

        // Act
        var run = await RunAsync([input, Path.Combine(_root, OutputName)], TestContext.CancellationToken);

        // Assert
        AssertError(run, reason, description);
        Assert.AreSequenceEqual(new[] { InputName }, FolderEntries(), $"No output and no temporary file should be left ({description})");
    }

    /// <summary>
    /// A frame the anonymiser cannot decode prints its line and leaves no output.
    /// </summary>
    /// <param name="description">What the frame is</param>
    /// <param name="content">The recording</param>
    /// <param name="line">The number of the frame's line</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(UndecodableRecordings))]
    public async Task AnonymiserCommandUndecodableFramePrintsLine(string description, byte[] content, long line)
    {
        // Arrange
        var input = WriteInput(content);

        // Act
        var run = await RunAsync([input, Path.Combine(_root, OutputName)], TestContext.CancellationToken);

        // Assert
        AssertError(run, $"frame cannot be anonymised in line {line}", description);
        Assert.AreSequenceEqual(new[] { InputName }, FolderEntries(), $"No output and no temporary file should be left ({description})");
    }

    /// <summary>
    /// An output that exists, as a file or a folder, is never overwritten.
    /// </summary>
    /// <param name="asFolder">Whether the output exists as a folder</param>
    /// <param name="expected">What the output should still be: the file's text, or "folder"</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(false, "keep")]
    [DataRow(true, "folder")]
    public async Task AnonymiserCommandExistingOutputIsNotOverwritten(bool asFolder, string expected)
    {
        // Arrange
        var input = WriteInput(RecordingOf(_version2, _sessionEnd));
        var output = Path.Combine(_root, OutputName);

        WriteExisting(output, asFolder);

        // Act
        var run = await RunAsync([input, output], TestContext.CancellationToken);

        // Assert
        AssertError(run, "output file exists");
        Assert.AreSequenceEqual(new[] { OutputName, InputName }, FolderEntries(), "No temporary file should be left");
        Assert.AreEqual(expected, DescribeExisting(output), "The existing output should be unchanged");
    }

    /// <summary>
    /// An output that is the input, however it is spelt, is refused and the input stays unchanged.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task AnonymiserCommandOutputIsInputIsRefused()
    {
        // Arrange
        var content = RecordingOf(_version2, _sessionEnd);
        var input = WriteInput(content);

        // Act
        var run = await RunAsync([input, Path.Combine(_root, ".", InputName)], TestContext.CancellationToken);

        // Assert
        AssertError(run, "output is the input");
        Assert.AreSequenceEqual(content, await File.ReadAllBytesAsync(input, TestContext.CancellationToken), "The input should be unchanged");
        Assert.AreSequenceEqual(new[] { InputName }, FolderEntries(), "No temporary file should be left");
    }

    /// <summary>
    /// An output in a folder that does not exist prints a write failure.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task AnonymiserCommandMissingOutputFolderPrintsWriteFailed()
    {
        // Arrange
        var input = WriteInput(RecordingOf(_version2, _sessionEnd));

        // Act
        var run = await RunAsync([input, Path.Combine(_root, "missing", OutputName)], TestContext.CancellationToken);

        // Assert
        AssertError(run, "write failed");
        Assert.AreSequenceEqual(new[] { InputName }, FolderEntries(), "No folder and no file should be created");
    }

    /// <summary>
    /// An input that cannot seek, a FIFO whose writer end is open, prints a read failure before any output exists.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [OSCondition(OperatingSystems.Linux)]
    public async Task AnonymiserCommandNonSeekableInputPrintsReadFailed()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        var input = Path.Combine(_root, InputName);

        await MakeFifoAsync(input);

        // Opening read and write does not block on Linux (fifo(7)), so the command's open finds a writer.
        await using var writer = new FileStream(input, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        await writer.WriteAsync(RecordingOf(_version2, _sessionEnd), TestContext.CancellationToken);
        await writer.FlushAsync(TestContext.CancellationToken);

        // Act
        var run = await RunAsync([input, Path.Combine(_root, OutputName)], TestContext.CancellationToken);

        // Assert
        AssertError(run, "read failed");
        Assert.AreSequenceEqual(new[] { InputName }, FolderEntries(), "No output and no temporary file should exist");
    }

    /// <summary>
    /// Not exactly two arguments print the usage and exit with the usage code.
    /// </summary>
    /// <param name="count">The number of arguments</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public async Task AnonymiserCommandWrongArgumentCountPrintsUsage(int count)
    {
        // Arrange
        var arguments = Enumerable.Range(1, count).Select(index => Path.Combine(_root, $"file{index}.jsonl")).ToArray();

        // Act
        var run = await RunAsync(arguments, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(AnonymiserCommand.UsageCode, run.Code, "A wrong argument count should exit with 2");
        Assert.AreEqual(Usage + Environment.NewLine, run.Error, "The usage should be printed on standard error");
        Assert.AreEqual(string.Empty, run.Output, "Nothing should be printed on standard output");
        Assert.IsFalse(Directory.Exists(_root), "No file should be written");
    }

    /// <summary>
    /// Not exactly two arguments print the usage and exit with the usage code even when the run is already cancelled.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task AnonymiserCommandWrongArgumentCountWithCancelledTokenPrintsUsage()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        await cancellation.CancelAsync();

        // Act
        var run = await RunAsync([Path.Combine(_root, "file1.jsonl")], cancellation.Token);

        // Assert
        Assert.AreEqual(AnonymiserCommand.UsageCode, run.Code, "A wrong argument count should exit with 2 although the run is cancelled");
        Assert.AreEqual(Usage + Environment.NewLine, run.Error, "The usage should be printed on standard error although the run is cancelled");
        Assert.AreEqual(string.Empty, run.Output, "Nothing should be printed on standard output");
    }

    /// <summary>
    /// A cancelled run prints that it was cancelled and leaves no output and no temporary file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task AnonymiserCommandCancelledLeavesNoOutput()
    {
        // Arrange
        var input = WriteInput(RecordingOf(_version2, SessionStart("Alice"), Island()));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        await cancellation.CancelAsync();

        // Act
        var run = await RunAsync([input, Path.Combine(_root, OutputName)], cancellation.Token);

        // Assert
        AssertError(run, "cancelled");
        Assert.AreSequenceEqual(new[] { InputName }, FolderEntries(), "No output and no temporary file should be left");
    }

    /// <summary>
    /// Creates a session-start frame.
    /// </summary>
    /// <param name="headline">The figure's name</param>
    /// <returns>The frame</returns>
    private static byte[] SessionStart(string headline)
    {
        return new PipeFrameBuilder().Byte(1).StringValue(headline).Build();
    }

    /// <summary>
    /// Creates an island frame with one entry.
    /// </summary>
    /// <returns>The frame</returns>
    private static byte[] Island()
    {
        return new PipeFrameBuilder().Byte(3)
                                     .Byte(7)
                                     .Byte(1)
                                     .Byte(0)
                                     .Int32Value(1)
                                     .StringValue("Ostia")
                                     .Int64Value(100)
                                     .Int32Value(1)
                                     .Entry(10, [new(1, 20)], [new(3, 4)])
                                     .Build();
    }

    /// <summary>
    /// Creates a recording of frames one second apart from the start on.
    /// </summary>
    /// <param name="frames">The frames</param>
    /// <returns>The file bytes</returns>
    private static byte[] RecordingOf(params byte[][] frames)
    {
        var lines = frames.Select((frame, index) => PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(index), frame));

        return Encoding.UTF8.GetBytes(string.Concat(PipeRecordingFormat.FormatHeader(_start), string.Concat(lines)));
    }

    /// <summary>
    /// Asserts that a run failed with one error line and printed nothing on standard output.
    /// </summary>
    /// <param name="run">The run</param>
    /// <param name="reason">The expected reason</param>
    /// <param name="description">What the case is, if more than one case shares the test</param>
    private static void AssertError((int Code, string Output, string Error) run, string reason, string description = "")
    {
        Assert.AreEqual(AnonymiserCommand.ErrorCode, run.Code, $"The run should exit with 1 {description}");
        Assert.AreEqual($"Error: {reason}{Environment.NewLine}", run.Error, $"The reason should be printed on standard error {description}");
        Assert.AreEqual(string.Empty, run.Output, $"Nothing should be printed on standard output {description}");
    }

    /// <summary>
    /// Describes an existing output: "folder" for a folder, else the file's text.
    /// </summary>
    /// <param name="path">The path of the output</param>
    /// <returns>The description</returns>
    private static string DescribeExisting(string path)
    {
        return Directory.Exists(path) ? "folder" : File.ReadAllText(path);
    }

    /// <summary>
    /// Creates the folder of the test and writes the input recording into it.
    /// </summary>
    /// <param name="content">The recording</param>
    /// <returns>The full path of the input</returns>
    private string WriteInput(byte[] content)
    {
        Directory.CreateDirectory(_root);

        var path = Path.Combine(_root, InputName);

        File.WriteAllBytes(path, content);

        return path;
    }

    /// <summary>
    /// Creates an existing output, as a folder or as a file holding "keep".
    /// </summary>
    /// <param name="path">The path of the output</param>
    /// <param name="asFolder">Whether to create a folder</param>
    private void WriteExisting(string path, bool asFolder)
    {
        Directory.CreateDirectory(_root);

        if (asFolder)
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            File.WriteAllText(path, "keep");
        }
    }

    /// <summary>
    /// Lists the names in the folder of the test, sorted.
    /// </summary>
    /// <returns>The names</returns>
    private string[] FolderEntries()
    {
        return Directory.GetFileSystemEntries(_root).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Creates a FIFO with <c>mkfifo</c>.
    /// </summary>
    /// <param name="path">The path of the FIFO</param>
    /// <returns>The task</returns>
    private async Task MakeFifoAsync(string path)
    {
        var start = new ProcessStartInfo("mkfifo")
                    {
                        UseShellExecute = false
                    };

        start.ArgumentList.Add(path);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("mkfifo did not start");

        await process.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        Assert.AreEqual(0, process.ExitCode, "mkfifo should create the FIFO");
    }

    /// <summary>
    /// Runs the command on a worker thread, so that a blocking open or read cannot hang the test.
    /// </summary>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token of the run</param>
    /// <returns>The exit code and what was printed on standard output and standard error</returns>
    private async Task<(int Code, string Output, string Error)> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var command = new AnonymiserCommand(output, error, new ScriptedPipeAnonymiserRandom(0.75));
        var code = await Task.Run(() => command.RunAsync(arguments, cancellationToken), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        return (code, output.ToString(), error.ToString());
    }

    /// <summary>
    /// Runs the command with a given standard output, on a worker thread.
    /// </summary>
    /// <param name="arguments">The arguments</param>
    /// <param name="output">The standard output</param>
    /// <returns>The exit code and what was printed on standard error</returns>
    private async Task<(int Code, string Error)> RunWithOutputAsync(string[] arguments, TextWriter output)
    {
        using var error = new StringWriter();

        var command = new AnonymiserCommand(output, error, new ScriptedPipeAnonymiserRandom(0.75));
        var code = await Task.Run(() => command.RunAsync(arguments, TestContext.CancellationToken), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        return (code, error.ToString());
    }

    #endregion // Methods
}