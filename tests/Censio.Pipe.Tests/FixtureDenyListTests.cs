using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="FixtureDenyList" /> and the check of the committed fixtures.
/// </summary>
[TestClass]
public sealed class FixtureDenyListTests
{
    #region Constants

    /// <summary>
    /// The file name of the fixture of the synthetic tests.
    /// </summary>
    private const string FixtureName = "a.jsonl";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the recordings.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// A version 2 frame.
    /// </summary>
    private static readonly byte[] _version2 = [0, 2, 0, 0, 0];

    /// <summary>
    /// The folder of the files of a synthetic test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-deny-list-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FixtureDenyListTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public FixtureDenyListTests(TestContext testContext)
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
    /// Gets the folder of the fixtures of a synthetic test.
    /// </summary>
    private string FixturesFolder => Path.Combine(_root, "Fixtures");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Deletes the folder of a synthetic test.
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
    /// Every non-empty name of every committed fixture is an invented name; this needs no deny list.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task FixtureDenyListFixturesHaveInventedNames()
    {
        // Arrange
        var list = FixtureDenyList.Empty;

        // Act
        var report = await list.CheckAsync(FixtureDenyList.FixturesFolder, TestContext.CancellationToken);

        // Assert
        Assert.IsGreaterThan(0, report.FixtureCount, "The fixtures should be copied next to the tests");
        Assert.IsGreaterThan(0L, report.NameCount, "The fixtures should hold names");
        Assert.IsEmpty(report.Findings, report.ToString());
    }

    /// <summary>
    /// With the local deny list, every committed fixture passes the check; without the variable the test is
    /// inconclusive, not passed.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task FixtureDenyListFixturesAgainstLocalDenyList()
    {
        // Arrange
        var list = await FixtureDenyList.LoadAsync(RequireDenyListPath(), TestContext.CancellationToken);

        // Act
        var report = await list.CheckAsync(FixtureDenyList.FixturesFolder, TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(report.Findings, report.ToString());
        Assert.IsGreaterThan(0L, report.ComparedValueCount, "The fixtures should be compared with their originals");
    }

    /// <summary>
    /// A recording anonymised by the anonymiser passes the check against its original.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task FixtureDenyListAnonymisedRecordingPasses()
    {
        // Arrange
        var original = WriteOriginal(RecordingOf(_version2, SessionStart("Alice"), Island("Ostia", ValuesEntry())));

        await AnonymiseAsync(original, Path.Combine(FixturesFolder, FixtureName));

        var list = FixtureDenyList.Parse($"deny Alice\ndeny ostia\noriginal {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(report.Findings, report.ToString());
        Assert.AreEqual(2L, report.NameCount, "Both names should be checked");
        Assert.AreEqual(11L, report.ComparedValueCount, "The ten non-zero scalars and the workforce amount should be compared");
    }

    /// <summary>
    /// An invented name equal to a deny entry, whatever its case, fails.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task FixtureDenyListDeniedNameFails()
    {
        // Arrange
        var original = WriteOriginal(RecordingOf(_version2, SessionStart("Alice"), Island("Ostia", ZeroEntry())));

        WriteFixture(RecordingOf(_version2, SessionStart("Player 1"), Island("Island 1", ZeroEntry())));

        var list = FixtureDenyList.Parse($"deny PLAYER 1\ndeny island 1\noriginal {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        string[] expected = ["a.jsonl: line 3: figure name is denied", "a.jsonl: line 4: island name is denied"];

        Assert.AreSequenceEqual(expected, report.Findings.ToArray(), "Both names should be denied, ignoring case");
    }

    /// <summary>
    /// A non-empty name that is not an invented name of its kind fails.
    /// </summary>
    /// <param name="kind">The kind of the name: figure or island</param>
    /// <param name="name">The name</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("island", "Ostia")]
    [DataRow("island", "Island 0")]
    [DataRow("island", "Island 01")]
    [DataRow("island", "island 1")]
    [DataRow("island", "Island 1 ")]
    [DataRow("island", "Player 1")]
    [DataRow("figure", "Island 1")]
    [DataRow("figure", "Player")]
    [DataRow("figure", "Player 1a")]
    public async Task FixtureDenyListNonInventedNameFails(string kind, string name)
    {
        // Arrange
        WriteFixture(RecordingOf(_version2, NamedFrame(kind, name)));

        // Act
        var report = await FixtureDenyList.Empty.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { $"a.jsonl: line 3: {kind} name is not an invented name" }, report.Findings.ToArray(), "The name should not pass as invented");
    }

    /// <summary>
    /// A non-zero value equal to the value at the same line, entry and field of the original fails, for every economic
    /// field; zero values are not compared.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task FixtureDenyListUnchangedAmountFails()
    {
        // Arrange
        var original = WriteOriginal(RecordingOf(_version2, SessionStart("Alice"), Island("Ostia", ValuesEntry())));

        WriteFixture(RecordingOf(_version2, SessionStart("Player 1"), Island("Island 1", ValuesEntry())));

        var list = FixtureDenyList.Parse($"original {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        string[] fields = ["Generation", "Consumption", "Delta", "PerfectGeneration", "PerfectConsumption", "Maintenance", "Income", "Profit", "SummedProductivity", "AverageProductivity", "WorkforceAmounts"];

        Assert.AreSequenceEqual(fields.Select(field => $"a.jsonl: line 4: entry 1: {field} equals the original").ToArray(), report.Findings.ToArray(), "Every unchanged non-zero value should fail, by field name only");
        Assert.AreEqual(11L, report.ComparedValueCount, "The zero workforce amount should not be compared");
    }

    /// <summary>
    /// A fixture whose original is missing or no recording fails and is named.
    /// </summary>
    /// <param name="exists">Whether the original exists as a file that is no recording</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FixtureDenyListMissingOriginalFails(bool exists)
    {
        // Arrange
        var original = Path.Combine(_root, "originals", "missing.jsonl");

        WriteFixture(RecordingOf(_version2, SessionStart("Player 1")));
        WriteFile(original, exists ? Encoding.UTF8.GetBytes("hello\n") : null);

        var list = FixtureDenyList.Parse($"original {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        string[] expected = ["a.jsonl: the original is missing or unreadable"];

        Assert.AreSequenceEqual(expected, report.Findings.ToArray(), "The fixture with the missing or unreadable original should be named");
    }

    /// <summary>
    /// The report holds counts, fixture names, line numbers and field names only: no denied or original name, no value,
    /// no original path and no text of a deny-list line.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task FixtureDenyListReportHoldsNoDeniedValue()
    {
        // Arrange
        var frames = RecordingOf(_version2, Island("Ostia", ValuesEntry()));

        WriteFile(Path.Combine(_root, "originals", "Ostia-recording.jsonl"), frames);
        WriteFixture(frames);

        var list = FixtureDenyList.Parse($"deny ostia\nsecret Ostia\noriginal {FixtureName} originals/Ostia-recording.jsonl\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        string[] fields = ["Generation", "Consumption", "Delta", "PerfectGeneration", "PerfectConsumption", "Maintenance", "Income", "Profit", "SummedProductivity", "AverageProductivity", "WorkforceAmounts"];
        string[] head = [
                            "1 fixtures, 1 names, 11 values compared, 14 findings",
                            "deny list line 2: not understood",
                            "a.jsonl: line 3: island name is not an invented name",
                            "a.jsonl: line 3: island name is denied"
                        ];
        var expected = string.Join("\n", head.Concat(fields.Select(field => $"a.jsonl: line 3: entry 1: {field} equals the original")));

        Assert.AreEqual(expected, report.ToString(), "The report should hold counts, fixture names, line numbers and field names only");
    }

    /// <summary>
    /// Gives the path of the local deny list, or reports the test inconclusive when the variable is not set.
    /// </summary>
    /// <returns>The path</returns>
    private static string RequireDenyListPath()
    {
        var path = Environment.GetEnvironmentVariable(FixtureDenyList.VariableName);

        if (string.IsNullOrEmpty(path))
        {
            Assert.Inconclusive($"{FixtureDenyList.VariableName} is not set: the fixtures were not checked against the local deny list");
        }

        return path ?? string.Empty;
    }

    /// <summary>
    /// Creates a session-start frame.
    /// </summary>
    /// <param name="headline">The figure's name</param>
    /// <returns>The frame</returns>
    private static byte[] SessionStart(string headline)
    {
        return new PipeFrameBuilder().Byte(1).String(headline).Build();
    }

    /// <summary>
    /// Creates an island frame with one entry.
    /// </summary>
    /// <param name="name">The island's name</param>
    /// <param name="entry">The bytes of the entry</param>
    /// <returns>The frame</returns>
    private static byte[] Island(string name, byte[] entry)
    {
        return new PipeFrameBuilder().Byte(3).Byte(7).Byte(1).Byte(0).Int32(1).String(name).Int64(100).Int32(1).Raw(entry).Build();
    }

    /// <summary>
    /// Creates a frame that carries a name of a kind.
    /// </summary>
    /// <param name="kind">The kind: figure for a session start, else an island</param>
    /// <param name="name">The name</param>
    /// <returns>The frame</returns>
    private static byte[] NamedFrame(string kind, string name)
    {
        return kind == "figure" ? SessionStart(name) : Island(name, ZeroEntry());
    }

    /// <summary>
    /// Creates an entry of non-zero values, a workforce amount and a zero workforce amount.
    /// </summary>
    /// <returns>The bytes of the entry</returns>
    private static byte[] ValuesEntry()
    {
        return new PipeFrameBuilder().Entry(10, [new(1, 20), new(2, 0)], [new(3, 4)]).Build();
    }

    /// <summary>
    /// Creates an entry whose every value is zero.
    /// </summary>
    /// <returns>The bytes of the entry</returns>
    private static byte[] ZeroEntry()
    {
        return new PipeFrameBuilder().Int32(10).Raw(new byte[44]).Int32(0).Int32(0).Build();
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
    /// Writes a file, creating its folder, or only the folder when there is no content.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="content">The content, or <see langword="null" /> for no file</param>
    private static void WriteFile(string path, byte[]? content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (content is not null)
        {
            File.WriteAllBytes(path, content);
        }
    }

    /// <summary>
    /// Writes an original recording.
    /// </summary>
    /// <param name="content">The recording</param>
    /// <returns>The full path</returns>
    private string WriteOriginal(byte[] content)
    {
        var path = Path.Combine(_root, "originals", "original.jsonl");

        WriteFile(path, content);

        return path;
    }

    /// <summary>
    /// Writes the fixture of a synthetic test.
    /// </summary>
    /// <param name="content">The recording</param>
    private void WriteFixture(byte[] content)
    {
        WriteFile(Path.Combine(FixturesFolder, FixtureName), content);
    }

    /// <summary>
    /// Anonymises an original into the fixture.
    /// </summary>
    /// <param name="original">The original's path</param>
    /// <param name="fixture">The fixture's path</param>
    /// <returns>The task</returns>
    private async Task AnonymiseAsync(string original, string fixture)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fixture)!);

        await using var input = File.OpenRead(original);
        await using var output = File.Create(fixture);

        var result = await new PipeRecordingAnonymiser(new ScriptedPipeAnonymiserRandom(0.75)).AnonymiseAsync(input, output, TestContext.CancellationToken);

        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "The original should be anonymised");
    }

    #endregion // Methods
}