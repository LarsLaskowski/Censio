using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeRecordingAnonymiser" />.
/// </summary>
[TestClass]
public sealed class PipeRecordingAnonymiserTests
{
    #region Constants

    /// <summary>
    /// A draw that goes upwards, with a magnitude of three quarters.
    /// </summary>
    private const double Up = 0.75;

    /// <summary>
    /// A draw that goes downwards, with a magnitude of one quarter.
    /// </summary>
    private const double Down = 0.25;

    /// <summary>
    /// The factor of an upwards draw of <see cref="Up" />: 1.01 + 0.19 × 0.75.
    /// </summary>
    private const double UpFactor = 1.1525;

    /// <summary>
    /// The factor of a downwards draw of <see cref="Down" />: 0.80 + 0.19 × 0.25.
    /// </summary>
    private const double DownFactor = 0.8475;

    /// <summary>
    /// The factor of the lower range for a magnitude of <see cref="Up" />: 0.80 + 0.19 × 0.75.
    /// </summary>
    private const double LowerUpFactor = 0.9425;

    /// <summary>
    /// The relative tolerance of a factor computed from float32 values.
    /// </summary>
    private const double FactorTolerance = 1e-6;

    /// <summary>
    /// The offset of the generation in an entry.
    /// </summary>
    private const int GenerationOffset = 4;

    /// <summary>
    /// The offset of the consumption in an entry.
    /// </summary>
    private const int ConsumptionOffset = 8;

    /// <summary>
    /// The offset of the delta in an entry.
    /// </summary>
    private const int DeltaOffset = 12;

    /// <summary>
    /// The offset of the perfect generation in an entry.
    /// </summary>
    private const int PerfectGenerationOffset = 16;

    /// <summary>
    /// The offset of the perfect consumption in an entry.
    /// </summary>
    private const int PerfectConsumptionOffset = 20;

    /// <summary>
    /// The offset of the income in an entry.
    /// </summary>
    private const int IncomeOffset = 32;

    /// <summary>
    /// The offset of the summed productivity in an entry.
    /// </summary>
    private const int SummedProductivityOffset = 40;

    /// <summary>
    /// The offset of the average productivity in an entry.
    /// </summary>
    private const int AverageProductivityOffset = 44;

    /// <summary>
    /// The length of the scalar fields of an entry, before its maps.
    /// </summary>
    private const int EntryScalarsLength = 48;

    /// <summary>
    /// The bits of the standard positive NaN.
    /// </summary>
    private const uint PositiveNaNBits = 0x7FC00000;

    /// <summary>
    /// The bits of the standard negative NaN (.NET's <see cref="float.NaN" />).
    /// </summary>
    private const uint NegativeNaNBits = 0xFFC00000;

    /// <summary>
    /// The bits of positive infinity.
    /// </summary>
    private const uint PositiveInfinityBits = 0x7F800000;

    /// <summary>
    /// The bits of negative infinity.
    /// </summary>
    private const uint NegativeInfinityBits = 0xFF800000;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the recordings.
    /// </summary>
    private static readonly DateTimeOffset _start = new DateTimeOffset(2026, 11, 1, 17, 9, 0, TimeSpan.Zero).AddTicks(1234567);

    /// <summary>
    /// The start a header gets when neither a frame nor the input's header gives one.
    /// </summary>
    private static readonly DateTimeOffset _defaultStarted = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The shift of a first draw of <see cref="Up" />: 30 days + 0.75 × 335 days = 281.25 days.
    /// </summary>
    private static readonly TimeSpan _upOffset = TimeSpan.FromTicks(TimeSpan.TicksPerDay * 1125 / 4);

    /// <summary>
    /// A map without pairs.
    /// </summary>
    private static readonly KeyValuePair<int, int>[] _noPairs = [];

    /// <summary>
    /// An empty map of amounts.
    /// </summary>
    private static readonly Dictionary<int, int> _noAmounts = [];

    /// <summary>
    /// A version 2 frame.
    /// </summary>
    private static readonly byte[] _version2 = [0, 2, 0, 0, 0];

    /// <summary>
    /// A session-end frame.
    /// </summary>
    private static readonly byte[] _sessionEnd = [2];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingAnonymiserTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeRecordingAnonymiserTests(TestContext testContext)
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
    /// Gets the contents of a recording's second pass that differ from its first pass, with a description.
    /// </summary>
    private static IEnumerable<object[]> ChangedSecondPasses
    {
        get
        {
            yield return ["another name", RecordingOf(_version2, SessionStart("Mallory"), StandardIsland(1, "Ostia", 100))];
            yield return ["another island name", RecordingOf(_version2, SessionStart("Alice"), StandardIsland(1, "Roma", 100))];
            yield return ["one frame more", RecordingOf(_version2, SessionStart("Alice"), StandardIsland(1, "Ostia", 100), _sessionEnd)];
            yield return ["one frame less", RecordingOf(_version2, SessionStart("Alice"))];
            yield return ["a frame that cannot be anonymised", RecordingOf(_version2, SessionStart("Alice"), [9, 1, 2])];
            yield return ["no recording", Encoding.UTF8.GetBytes("garbage\n")];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The replay of an anonymised recording gives the items of the original's replay — versions, session events,
    /// ticks with their island keys, game times and close reasons — with every receive time earlier by the offset.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC4")]
    public async Task PipeRecordingAnonymiserReplayGivesSameItemsAsOriginal()
    {
        // Arrange
        var recording = RecordingOf(ReplayScenario());
        var original = await ReplayAsync(recording);

        // Act
        var (result, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        var replayed = await ReplayAsync(output);

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "The recording should be anonymised");
        Assert.AreEqual(8L, result.FrameCount, "Every frame should be counted");
        Assert.HasCount(2, original.Where(item => item.Tick is not null).ToList(), "The original should replay as two ticks");
        Assert.AreSequenceEqual(original.Select(item => DescribeShape(item, _upOffset)).ToList(),
                                replayed.Select(item => DescribeShape(item, TimeSpan.Zero)).ToList(),
                                "The anonymised replay should give the original's items, receive times earlier by the offset");
    }

    /// <summary>
    /// The output has the input's line count and, frame by frame, its type, the version and session-end frames, and of
    /// an island frame its key, game time, entries, product GUIDs, building counts, building map and workforce GUIDs.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeRecordingAnonymiserKeepsLinesAndStructure()
    {
        // Arrange
        var frames = ReplayScenario();
        var recording = RecordingOf(frames);

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.HasCount(frames.Length + 1, OutputLines(output), "The output should have the input's line count");
        Assert.AreSequenceEqual(frames.Select(DescribeStructure).ToList(),
                                OutputFrames(output).Select(line => DescribeStructure(line.Frame)).ToList(),
                                "Every frame should keep its type and structure");
    }

    /// <summary>
    /// Every map pair is written in wire order, a repeated GUID included: workforce amounts change, building pairs are
    /// copied.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC3")]
    public async Task PipeRecordingAnonymiserRepeatedMapGuidKeepsEveryPair()
    {
        // Arrange
        var island = Island(1, "Ostia", 100, EntryBytes(StandardEntry(10), [new(7, 10), new(7, 20), new(8, 30)], [new(5, 1), new(5, 2)]));

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        var entry = WalkIsland(OutputFrames(output)[1].Frame).Entries[0];
        string[] expectedWorkforce = ["7=12", "7=23", "8=35"];
        string[] expectedBuildings = ["5=1", "5=2"];

        Assert.AreSequenceEqual(expectedWorkforce, entry.Workforce.Select(DescribePair).ToArray(), "Every workforce pair should stay in wire order with its amount changed by the factor of 1.1525");
        Assert.AreSequenceEqual(expectedBuildings, entry.Buildings.Select(DescribePair).ToArray(), "Every building pair should be copied in wire order");
    }

    /// <summary>
    /// Bytes after the last field of a frame are dropped, for each of the four frame types.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeRecordingAnonymiserTrailingBytesAreDropped()
    {
        // Arrange
        byte[] version = [.. _version2, 0xAA, 0xBB];
        byte[] start = [.. SessionStart("Alice"), 0xAA];
        byte[] end = [.. _sessionEnd, 0xAA, 0xBB];
        byte[] island = [.. StandardIsland(1, "Ostia", 100), 0xAA, 0xBB];

        // Act
        var (result, output) = await AnonymiseAsync(RecordingOf(version, start, end, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        var frames = OutputFrames(output);

        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "Frames with trailing bytes should be anonymised");
        Assert.AreSequenceEqual(_version2, frames[0].Frame, "The version frame should end after its version");
        Assert.AreSequenceEqual(SessionStart("Player 1"), frames[1].Frame, "The session start should be the type byte, the length byte and the invented name");
        Assert.AreSequenceEqual(_sessionEnd, frames[2].Frame, "The session end should be its type byte alone");
        Assert.AreEqual(frames[3].Frame.Length, WalkIsland(frames[3].Frame).End, "The island frame should end after its last field");
    }

    /// <summary>
    /// An empty frame holds nothing and is copied, before and after the version.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeRecordingAnonymiserEmptyFrameIsCopied()
    {
        // Arrange
        var recording = RecordingOf([], _version2, [], _sessionEnd);

        // Act
        var (result, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "A recording with empty frames should be anonymised");
        Assert.AreEqual(4L, result.FrameCount, "Every frame should be counted");
        Assert.AreSequenceEqual(new[] { string.Empty, "AAIAAAA=", string.Empty, "Ag==" }, OutputFrames(output).Select(line => Convert.ToBase64String(line.Frame)).ToArray(), "The empty frames should be copied in their lines");
    }

    /// <summary>
    /// A header-only recording gives a header-only output whose start is the input's start shifted, or the default start
    /// shifted when the input's is missing or unusable.
    /// </summary>
    /// <param name="startedJson">The JSON of the started field, or an empty text for none</param>
    /// <param name="expectedStarted">The start before the shift</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(",\"started\":\"2026-11-01T17:09:00.1234567Z\"", "2026-11-01T17:09:00.1234567Z")]
    [DataRow(",\"started\":\"x\"", "2000-01-01T00:00:00Z")]
    [DataRow("", "2000-01-01T00:00:00Z")]
    public async Task PipeRecordingAnonymiserHeaderOnlyGivesHeaderOnly(string startedJson, string expectedStarted)
    {
        // Arrange
        var recording = Encoding.UTF8.GetBytes($"{{\"format\":\"{PipeRecordingFormat.FormatName}\",\"version\":1{startedJson}}}\n");
        var expected = PipeRecordingFormat.FormatHeader(DateTimeOffset.Parse(expectedStarted, CultureInfo.InvariantCulture) - _upOffset);

        // Act
        var (result, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "A header-only recording should be anonymised");
        Assert.AreEqual(0L, result.FrameCount, "No frame should be counted");
        Assert.AreEqual(expected, Encoding.UTF8.GetString(output), "The output should be the header alone with the shifted start");
    }

    /// <summary>
    /// Every output line is formatted anew: unknown fields, escapes, offsets, the byte order mark and carriage returns of
    /// the input do not reach the output, and the header's start is the first frame's shifted time.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeRecordingAnonymiserOutputLinesAreFormattedAnew()
    {
        // Arrange
        var text = string.Concat("""{"format":"censio-pipe-recording","note":"a\"b\\c\/d","version":1,"started":"2026-11-01T19:09:00.1234567+02:00"}""",
                                 "\r\n",
                                 """{"x":{"y":["\/",1.5]},"time":"2026-11-01T19:09:01.1234567+02:00","frame":"AAIAAAA=","z":"\n"}""",
                                 "\r\n",
                                 """{"time":"2026-11-01T17:09:02.1234567Z","frame":"Ag==","w":null}""",
                                 "\r\n");
        byte[] recording = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];
        var firstTime = _start.AddSeconds(1) - _upOffset;
        var expected = string.Concat(PipeRecordingFormat.FormatHeader(firstTime),
                                     PipeRecordingFormat.FormatFrameLine(firstTime, _version2),
                                     PipeRecordingFormat.FormatFrameLine(_start.AddSeconds(2) - _upOffset, _sessionEnd));

        // Act
        var (result, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.Contains("a\\\"b\\\\c\\/d", text, "The input should hold its JSON escapes");
        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "The recording should be anonymised");
        Assert.AreEqual(expected, Encoding.UTF8.GetString(output), "Every output line should be made by the format methods alone");
        Assert.AreEqual((byte)'{', output[0], "The output should start without a byte order mark");
    }

    /// <summary>
    /// One original name always gets the same invented name in a file, whichever island carries it.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task PipeRecordingAnonymiserSameNameGetsSameInventedName()
    {
        // Arrange
        var recording = RecordingOf(_version2,
                                    SessionStart("Alice"),
                                    StandardIsland(1, "Ostia", 100),
                                    StandardIsland(2, "Ostia", 100),
                                    StandardIsland(1, "Ostia", 200),
                                    _sessionEnd,
                                    SessionStart("Alice"));

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        string[] expected = ["-", "Player 1", "Island 1", "Island 1", "Island 1", "-", "Player 1"];

        Assert.AreSequenceEqual(expected, OutputFrames(output).Select(line => NameOf(line.Frame)).ToArray(), "One original name should get one invented name");
    }

    /// <summary>
    /// Two names get two invented names, numbered in the order of first appearance, so a rename and a change of figure
    /// stay visible.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task PipeRecordingAnonymiserTwoNamesGetTwoInventedNames()
    {
        // Arrange
        var recording = RecordingOf(_version2,
                                    SessionStart("Alice"),
                                    StandardIsland(1, "Ostia", 100),
                                    StandardIsland(1, "Ostia Nova", 200),
                                    _sessionEnd,
                                    SessionStart("Bob"),
                                    StandardIsland(1, "Ostia Nova", 300),
                                    StandardIsland(2, "Ostia", 300));

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        string[] expected = ["-", "Player 1", "Island 1", "Island 2", "-", "Player 2", "Island 2", "Island 1"];

        Assert.AreSequenceEqual(expected, OutputFrames(output).Select(line => NameOf(line.Frame)).ToArray(), "Two names should get two invented names in the order of first appearance");
    }

    /// <summary>
    /// A number whose invented name equals an original name of the file, ignoring case, is skipped.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task PipeRecordingAnonymiserOriginalEqualToInventedNameIsSkipped()
    {
        // Arrange
        var recording = RecordingOf(_version2,
                                    SessionStart("PLAYER 1"),
                                    StandardIsland(1, "Island 1", 100),
                                    StandardIsland(2, "island 2", 100),
                                    StandardIsland(3, "Ostia", 100));

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        string[] expected = ["-", "Player 2", "Island 3", "Island 4", "Island 5"];

        Assert.AreSequenceEqual(expected, OutputFrames(output).Select(line => NameOf(line.Frame)).ToArray(), "No invented name should equal an original name of the file, ignoring case");
    }

    /// <summary>
    /// An empty name stays empty and takes no number.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task PipeRecordingAnonymiserEmptyNameStaysEmpty()
    {
        // Arrange
        var recording = RecordingOf(_version2,
                                    SessionStart(string.Empty),
                                    StandardIsland(1, string.Empty, 100),
                                    StandardIsland(2, "Ostia", 100),
                                    SessionStart("Bob"));

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        string[] expected = ["-", string.Empty, string.Empty, "Island 1", "Player 1"];

        Assert.AreSequenceEqual(expected, OutputFrames(output).Select(line => NameOf(line.Frame)).ToArray(), "An empty name should stay empty");
    }

    /// <summary>
    /// The multiplied amounts of an entry share one factor in the bounds; each entry draws its own.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task PipeRecordingAnonymiserFloatAmountsShareOneEntryFactor()
    {
        // Arrange
        var first = StandardEntry(10);
        var second = StandardEntry(11);
        var island = Island(1, "Ostia", 100, EntryBytes(first, _noPairs, _noPairs), EntryBytes(second, _noPairs, _noPairs));

        // Draws: the offset; the first entry's factor (side, magnitude) and maintenance (side, magnitude); then the
        // second entry's, all downwards.
        var random = new ScriptedPipeAnonymiserRandom(Down, 0.5, Up, Up, Up, Up);

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), random);

        // Assert
        var entries = ParseIsland(OutputFrames(output)[1].Frame).Entries;
        var firstRatios = MultipliedRatios(first, entries[0]);
        var secondRatios = MultipliedRatios(second, entries[1]);

        Assert.AreEqual(UpFactor, firstRatios.Min(), UpFactor * FactorTolerance, "Every multiplied amount of the first entry should share the upwards factor");
        Assert.AreEqual(UpFactor, firstRatios.Max(), UpFactor * FactorTolerance, "Every multiplied amount of the first entry should share the upwards factor");
        Assert.AreEqual(DownFactor, secondRatios.Min(), DownFactor * FactorTolerance, "Every multiplied amount of the second entry should share the downwards factor");
        Assert.AreEqual(DownFactor, secondRatios.Max(), DownFactor * FactorTolerance, "Every multiplied amount of the second entry should share the downwards factor");
    }

    /// <summary>
    /// Delta, average productivity and profit are derived from the perturbed values; with a building count of 0 the
    /// average is 0; with a non-finite generation or summed productivity the delta or average is multiplied instead.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task PipeRecordingAnonymiserDerivedValuesFollowRelations()
    {
        // Arrange
        var noBuildings = new ProductionEntry
                          {
                              ProductGuid = 11,
                              Generation = 1000f,
                              Consumption = 800f,
                              Delta = 200f,
                              PerfectGeneration = 1200f,
                              PerfectConsumption = 900f,
                              BuildingCount = 0,
                              Maintenance = 100,
                              Income = 500f,
                              Profit = 400,
                              SummedProductivity = 300f,
                              AverageProductivity = 75f,
                              WorkforceAmounts = _noAmounts,
                              BuildingAmounts = _noAmounts
                          };
        var infinite = new ProductionEntry
                       {
                           ProductGuid = 12,
                           Generation = float.PositiveInfinity,
                           Consumption = 800f,
                           Delta = 7f,
                           PerfectGeneration = 1200f,
                           PerfectConsumption = 900f,
                           BuildingCount = 2,
                           Maintenance = 100,
                           Income = 500f,
                           Profit = 400,
                           SummedProductivity = float.PositiveInfinity,
                           AverageProductivity = 50f,
                           WorkforceAmounts = _noAmounts,
                           BuildingAmounts = _noAmounts
                       };
        var island = Island(1,
                            "Ostia",
                            100,
                            EntryBytes(StandardEntry(10), _noPairs, _noPairs),
                            EntryBytes(noBuildings, _noPairs, _noPairs),
                            EntryBytes(infinite, _noPairs, _noPairs));

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        var entries = ParseIsland(OutputFrames(output)[1].Frame).Entries;
        var standard = entries[0];

        Assert.AreEqual(standard.Generation - standard.Consumption, standard.Delta, 1e-3f, "The delta should be the perturbed generation minus the perturbed consumption");
        Assert.AreEqual((float)(100.0 * standard.SummedProductivity / 4), standard.AverageProductivity, 1e-3f, "The average should be 100 times the perturbed summed productivity over the building count");
        Assert.AreEqual(115, standard.Maintenance, "The maintenance of 100 should change by the factor of 1.1525, rounded");
        Assert.AreEqual(461, standard.Profit, "The profit should be 400 plus the change of income minus maintenance (461.25 - 400), rounded");
        Assert.IsLessThanOrEqualTo(1.0, Math.Abs(standard.Profit - (standard.Income - standard.Maintenance)), "The profit should stay within 1 of income minus maintenance");
        Assert.AreEqual(0f, entries[1].AverageProductivity, "The average should be 0 with a building count of 0");
        Assert.AreEqual(float.PositiveInfinity, entries[2].Generation, "An infinite generation should stay infinite");
        Assert.AreEqual((float)(7 * UpFactor), entries[2].Delta, 1e-5f, "The delta should be multiplied when the generation is not finite");
        Assert.AreEqual((float)(50 * UpFactor), entries[2].AverageProductivity, 1e-4f, "The average should be multiplied when the perturbed summed productivity is not finite");
    }

    /// <summary>
    /// Maintenance and workforce amounts below 5 change by exactly 1 in magnitude and keep their sign; 1 always becomes 2.
    /// </summary>
    /// <param name="original">The original amount</param>
    /// <param name="draw">The draw of every side and magnitude</param>
    /// <param name="expected">The expected amount</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(1, Up, 2)]
    [DataRow(1, Down, 2)]
    [DataRow(2, Up, 3)]
    [DataRow(2, Down, 1)]
    [DataRow(4, Up, 5)]
    [DataRow(4, Down, 3)]
    [DataRow(-3, Up, -4)]
    [DataRow(-3, Down, -2)]
    public async Task PipeRecordingAnonymiserSmallIntegersChangeByOne(int original, double draw, int expected)
    {
        // Arrange
        var island = Island(1, "Ostia", 100, EntryBytes(Entry(10, 1000f, original, 500f, 400), [new(1, original)], _noPairs));

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(draw));

        // Assert
        var frame = OutputFrames(output)[1].Frame;

        Assert.AreEqual(expected, ParseIsland(frame).Entries[0].Maintenance, "The maintenance should change by 1 in magnitude");
        Assert.AreEqual(expected, WalkIsland(frame).Entries[0].Workforce[0].Value, "The workforce amount should change by 1 in magnitude");
    }

    /// <summary>
    /// Maintenance and workforce amounts from 5 on change by 1 % to 20 %, rounded, at least 1 and with their sign kept;
    /// downwards where upwards would leave the 32-bit range.
    /// </summary>
    /// <param name="original">The original amount</param>
    /// <param name="draw">The draw of every side and magnitude</param>
    /// <param name="expectedMin">The smallest expected amount</param>
    /// <param name="expectedMax">The largest expected amount</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(5, Up, 6, 6)]
    [DataRow(5, Down, 4, 4)]
    [DataRow(1000, 0.5, 1104, 1106)]
    [DataRow(1000, Down, 942, 943)]
    [DataRow(int.MaxValue, Up, 1819992390, 1819992391)]
    [DataRow(int.MinValue, Up, -1819992392, -1819992391)]
    public async Task PipeRecordingAnonymiserLargeIntegersChangeWithinBounds(int original, double draw, int expectedMin, int expectedMax)
    {
        // Arrange
        var island = Island(1, "Ostia", 100, EntryBytes(Entry(10, 1000f, original, 500f, 400), [new(1, original)], _noPairs));

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(draw));

        // Assert
        var frame = OutputFrames(output)[1].Frame;

        Assert.IsInRange(expectedMin, expectedMax, ParseIsland(frame).Entries[0].Maintenance, "The maintenance should change within the bounds of the draw");
        Assert.IsInRange(expectedMin, expectedMax, WalkIsland(frame).Entries[0].Workforce[0].Value, "The workforce amount should change within the bounds of the draw");
    }

    /// <summary>
    /// Zero stays zero and infinities stay; a NaN of any payload becomes the standard NaN with its sign, in a multiplied
    /// field, the delta and the average productivity.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task PipeRecordingAnonymiserZeroNaNAndInfinityAreKept()
    {
        // Arrange
        var positivePayloads = new PipeFrameBuilder().Int32(10)
                                                     .Raw(Bits(0x7FC00001))
                                                     .Float32(50f)
                                                     .Raw(Bits(0xFFE00123))
                                                     .Float32(float.PositiveInfinity)
                                                     .Float32(float.NegativeInfinity)
                                                     .Int32(2)
                                                     .Int32(0)
                                                     .Float32(0f)
                                                     .Int32(0)
                                                     .Float32(0f)
                                                     .Raw(Bits(0x7FFFFFFF))
                                                     .Map([new(1, 0)])
                                                     .Map(_noPairs)
                                                     .Build();
        var negativePayloads = new PipeFrameBuilder().Int32(11)
                                                     .Raw(Bits(0xFFC12345))
                                                     .Float32(0f)
                                                     .Raw(Bits(0x7FC00777))
                                                     .Float32(0f)
                                                     .Float32(0f)
                                                     .Int32(1)
                                                     .Int32(7)
                                                     .Float32(float.PositiveInfinity)
                                                     .Int32(3)
                                                     .Float32(5f)
                                                     .Raw(Bits(0xFFC0ABCD))
                                                     .Map(_noPairs)
                                                     .Map(_noPairs)
                                                     .Build();
        var island = Island(1, "Ostia", 100, positivePayloads, negativePayloads);

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        var frame = OutputFrames(output)[1].Frame;
        var wire = WalkIsland(frame);
        var first = wire.Entries[0];
        var second = wire.Entries[1];
        var parsed = ParseIsland(frame).Entries[0];

        Assert.AreEqual(0x7FC00001u, FloatBits(island, WalkIsland(island).Entries[0].Offset + GenerationOffset), "The input should hold the NaN payload");
        Assert.AreEqual(PositiveNaNBits, FloatBits(frame, first.Offset + GenerationOffset), "A positive NaN generation should become the standard positive NaN");
        Assert.AreEqual(NegativeNaNBits, FloatBits(frame, first.Offset + DeltaOffset), "A negative NaN delta should become the standard negative NaN");
        Assert.AreEqual(PositiveNaNBits, FloatBits(frame, first.Offset + AverageProductivityOffset), "A positive NaN average should become the standard positive NaN");
        Assert.AreEqual(PositiveInfinityBits, FloatBits(frame, first.Offset + PerfectGenerationOffset), "Positive infinity should stay");
        Assert.AreEqual(NegativeInfinityBits, FloatBits(frame, first.Offset + PerfectConsumptionOffset), "Negative infinity should stay");
        Assert.AreEqual(0u, FloatBits(frame, first.Offset + IncomeOffset), "A zero income should stay zero");
        Assert.AreEqual(0u, FloatBits(frame, first.Offset + SummedProductivityOffset), "A zero summed productivity should stay zero");
        Assert.AreEqual(0, parsed.Maintenance, "A zero maintenance should stay zero");
        Assert.AreEqual(0, parsed.Profit, "A zero profit with zero income and maintenance should stay zero");
        Assert.AreEqual(0, first.Workforce[0].Value, "A zero workforce amount should stay zero");
        Assert.AreEqual(NegativeNaNBits, FloatBits(frame, second.Offset + GenerationOffset), "A negative NaN generation should become the standard negative NaN");
        Assert.AreEqual(0u, FloatBits(frame, second.Offset + ConsumptionOffset), "A zero consumption should stay zero");
        Assert.AreEqual(PositiveNaNBits, FloatBits(frame, second.Offset + DeltaOffset), "A positive NaN delta should become the standard positive NaN");
        Assert.AreEqual(NegativeNaNBits, FloatBits(frame, second.Offset + AverageProductivityOffset), "A negative NaN average should become the standard negative NaN");
        Assert.AreEqual(PositiveInfinityBits, FloatBits(frame, second.Offset + IncomeOffset), "An infinite income should stay infinite");
    }

    /// <summary>
    /// A non-zero profit whose rounded change is 0 moves 1 towards the new income minus maintenance, or away from it at
    /// the edge of the 32-bit range.
    /// </summary>
    /// <param name="income">The income</param>
    /// <param name="profit">The profit</param>
    /// <param name="expected">The expected profit</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(0f, 5, 4)]
    [DataRow(0f, -5, -4)]
    [DataRow(0f, 1, 0)]
    [DataRow(0.1f, -3, -2)]
    [DataRow(3e9f, int.MaxValue, int.MaxValue - 1)]
    [DataRow(-3e9f, int.MinValue, int.MinValue + 1)]
    public async Task PipeRecordingAnonymiserProfitWhoseChangeRoundsToZeroMovesOne(float income, int profit, int expected)
    {
        // Arrange
        var island = Island(1, "Ostia", 100, EntryBytes(Entry(10, 1000f, 0, income, profit), _noPairs, _noPairs));

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(expected, ParseIsland(OutputFrames(output)[1].Frame).Entries[0].Profit, "The profit should move by 1 as the rule says");
    }

    /// <summary>
    /// The amounts of a workforce GUID that repeats are drawn so that their sum stays in the 32-bit range, and every pair
    /// stays in wire order.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task PipeRecordingAnonymiserRepeatedWorkforceGuidSumStaysInRange()
    {
        // Arrange
        var island = Island(1, "Ostia", 100, EntryBytes(StandardEntry(10), [new(7, 2000000000), new(7, 100000000)], _noPairs));

        // Act
        var (result, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        var frame = OutputFrames(output)[1].Frame;
        var pairs = WalkIsland(frame).Entries[0].Workforce;
        int[] expectedGuids = [7, 7];

        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "The recording should be anonymised");
        Assert.AreEqual(PipeReadResultKind.Message, PipeMessageParser.Parse(frame).Kind, "The anonymised frame should parse, its merged sum within the 32-bit range");
        Assert.AreSequenceEqual(expectedGuids, pairs.Select(pair => pair.Key).ToArray(), "Both pairs should stay in wire order");
        Assert.IsLessThanOrEqualTo((long)int.MaxValue, pairs.Sum(pair => (long)pair.Value), "The sum of the GUID's amounts should stay in the 32-bit range");
        Assert.AreNotEqual(2000000000, pairs[0].Value, "The first amount should change");
        Assert.AreNotEqual(100000000, pairs[1].Value, "The second amount should change");
        Assert.IsGreaterThan(0, pairs.Min(pair => pair.Value), "Both amounts should keep their sign");
    }

    /// <summary>
    /// When a multiplied value would become infinite, the entry's factor is drawn from the lower range.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task PipeRecordingAnonymiserOverflowingFactorUsesLowerRange()
    {
        // Arrange
        var entry = Entry(10, 3e38f, 100, 500f, 400);
        var island = Island(1, "Ostia", 100, EntryBytes(entry, _noPairs, _noPairs));

        // Act
        var (_, output) = await AnonymiseAsync(RecordingOf(_version2, island), new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        var anonymised = ParseIsland(OutputFrames(output)[1].Frame).Entries[0];
        var ratios = MultipliedRatios(entry, anonymised);

        Assert.IsTrue(float.IsFinite(anonymised.Generation), "The generation should stay finite");
        Assert.AreEqual(LowerUpFactor, ratios.Min(), LowerUpFactor * FactorTolerance, "Every multiplied amount should share the factor of the lower range");
        Assert.AreEqual(LowerUpFactor, ratios.Max(), LowerUpFactor * FactorTolerance, "Every multiplied amount should share the factor of the lower range");
    }

    /// <summary>
    /// Every receive time and the header's start move earlier by one offset, so every pause is kept to the 100 ns.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task PipeRecordingAnonymiserReceiveTimesShiftByOneOffset()
    {
        // Arrange
        DateTimeOffset[] times = [_start, _start.AddSeconds(1.5), _start.AddSeconds(40).AddTicks(1), _start.AddSeconds(-10)];
        var recording = Recording([(_start, _version2), .. times.Select(time => (time, _sessionEnd))]);
        var expected = string.Concat(PipeRecordingFormat.FormatHeader(_start - _upOffset),
                                     PipeRecordingFormat.FormatFrameLine(_start - _upOffset, _version2),
                                     string.Concat(times.Select(time => PipeRecordingFormat.FormatFrameLine(time - _upOffset, _sessionEnd))));

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(expected, Encoding.UTF8.GetString(output), "Every time should move earlier by 281.25 days");
    }

    /// <summary>
    /// The offset lies between 30 and 365 days and is never a whole number of days.
    /// </summary>
    /// <param name="draw">The draw of the offset</param>
    /// <param name="expectedMinTicks">The smallest expected offset in ticks</param>
    /// <param name="expectedMaxTicks">The largest expected offset in ticks</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0.0, (30 * TimeSpan.TicksPerDay) + 1, (30 * TimeSpan.TicksPerDay) + 1)]
    [DataRow(0.5, 395 * TimeSpan.TicksPerDay / 2, 395 * TimeSpan.TicksPerDay / 2)]
    [DataRow(0.9999999999999999, 364 * TimeSpan.TicksPerDay, (365 * TimeSpan.TicksPerDay) - 1)]
    public async Task PipeRecordingAnonymiserOffsetBoundsAreNotWholeDays(double draw, long expectedMinTicks, long expectedMaxTicks)
    {
        // Arrange
        var recording = Recording((_start, _version2), (_start.AddSeconds(1), _sessionEnd));

        // Act
        var (_, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(draw));

        // Assert
        var offset = _start - OutputFrames(output)[0].Time;

        Assert.IsInRange(expectedMinTicks, expectedMaxTicks, offset.Ticks, "The offset should follow the draw within 30 to 365 days");
        Assert.AreNotEqual(0L, offset.Ticks % TimeSpan.TicksPerDay, "The offset should not be a whole number of days");
    }

    /// <summary>
    /// A session end before the first version cannot be anonymised: only a version frame is decoded until a version 2.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task PipeRecordingAnonymiserSessionEndBeforeVersionIsNotAnonymisable()
    {
        // Arrange
        var recording = RecordingOf(_sessionEnd, _version2);

        // Act
        var (result, _) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.FrameNotAnonymisable, result.Kind, "A session end before the version should be refused");
        Assert.AreEqual(2L, result.LineNumber, "The refusal should name the line of the session end");
    }

    /// <summary>
    /// Two runs with the system random source draw different offsets and factors.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task PipeRecordingAnonymiserTwoRunsWithSystemRandomDiffer()
    {
        // Arrange
        var recording = RecordingOf(_version2, StandardIsland(1, "Ostia", 100));

        // Act
        var (_, firstOutput) = await AnonymiseAsync(recording, new PipeAnonymiserRandom());
        var (_, secondOutput) = await AnonymiseAsync(recording, new PipeAnonymiserRandom());

        // Assert
        var first = OutputFrames(firstOutput);
        var second = OutputFrames(secondOutput);

        Assert.AreNotEqual(first[0].Time, second[0].Time, "The two runs should draw different offsets");
        Assert.AreNotEqual(ParseIsland(first[1].Frame).Entries[0].Generation, ParseIsland(second[1].Frame).Entries[0].Generation, "The two runs should draw different factors");
    }

    /// <summary>
    /// A receive time the offset would move before the earliest time is an invalid line.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [TestCategory("AC5")]
    public async Task PipeRecordingAnonymiserShiftBeforeEarliestTimeIsInvalidLine()
    {
        // Arrange
        var recording = Recording((_start, _version2), (DateTimeOffset.MinValue.AddDays(10), _sessionEnd));

        // Act
        var (result, _) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(0.0));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.RecordingProblem, result.Kind, "The recording should be refused");
        Assert.IsNotNull(result.Problem, "The problem should be set");
        Assert.AreEqual(PipeReplayProblemKind.InvalidLine, result.Problem.Kind, "The line should be invalid");
        Assert.AreEqual(3L, result.Problem.LineNumber, "The problem should name the line of the frame");
    }

    /// <summary>
    /// A recording that fails to be read, in either pass, is a read failure.
    /// </summary>
    /// <param name="pass">The pass in which the read fails</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(1)]
    [DataRow(2)]
    public async Task PipeRecordingAnonymiserReadErrorIsReadFailed(int pass)
    {
        // Arrange
        var content = RecordingOf(ReplayScenario());
        var recording = new TwoPassRecordingStream(content, content, content.Length / 2, pass);

        // Act
        var (result, _) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.RecordingProblem, result.Kind, "The recording should be refused");
        Assert.AreEqual(PipeReplayProblemKind.ReadFailed, result.Problem?.Kind, "A failing read should be a read failure");
    }

    /// <summary>
    /// A recording that differs between the two passes is a read failure.
    /// </summary>
    /// <param name="description">How the second pass differs</param>
    /// <param name="secondPass">The content of the second pass</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(ChangedSecondPasses))]
    public async Task PipeRecordingAnonymiserFileChangedBetweenPassesIsReadFailed(string description, byte[] secondPass)
    {
        // Arrange
        var recording = new TwoPassRecordingStream(RecordingOf(_version2, SessionStart("Alice"), StandardIsland(1, "Ostia", 100)), secondPass);

        // Act
        var (result, _) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(2, recording.Pass, $"The recording should be read twice ({description})");
        Assert.AreEqual(PipeAnonymiseResultKind.RecordingProblem, result.Kind, $"The recording should be refused ({description})");
        Assert.AreEqual(PipeReplayProblemKind.ReadFailed, result.Problem?.Kind, $"A recording that changed should be a read failure ({description})");
    }

    /// <summary>
    /// A recording stream that cannot seek is a read failure.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task PipeRecordingAnonymiserNonSeekableRecordingIsReadFailed()
    {
        // Arrange
        var recording = new ChunkedStream(RecordingOf(_version2, _sessionEnd), 7);

        // Act
        var (result, _) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.RecordingProblem, result.Kind, "The recording should be refused");
        Assert.AreEqual(PipeReplayProblemKind.ReadFailed, result.Problem?.Kind, "A recording that cannot seek should be a read failure");
    }

    /// <summary>
    /// A header start so near the earliest time that the shift could pass it is not used: the default start is shifted
    /// instead.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task PipeRecordingAnonymiserStartedNearEarliestTimeUsesDefault()
    {
        // Arrange
        var recording = Encoding.UTF8.GetBytes(PipeRecordingFormat.FormatHeader(DateTimeOffset.MinValue.AddDays(100)));

        // Act
        var (result, output) = await AnonymiseAsync(recording, new ScriptedPipeAnonymiserRandom(Up));

        // Assert
        Assert.AreEqual(PipeAnonymiseResultKind.Anonymised, result.Kind, "The recording should be anonymised");
        Assert.AreEqual(PipeRecordingFormat.FormatHeader(_defaultStarted - _upOffset), Encoding.UTF8.GetString(output), "The default start should be shifted instead");
    }

    /// <summary>
    /// Creates the frames of the replay scenario: a version, a repeated session end, a session start, two ticks of two
    /// islands, a rename, the last tick closed by the stream end.
    /// </summary>
    /// <returns>The frames</returns>
    private static byte[][] ReplayScenario()
    {
        return
        [
            _version2,
            _sessionEnd,
            _sessionEnd,
            SessionStart("Alice"),
            StandardIsland(1, "Ostia", 100),
            StandardIsland(2, "Roma", 100),
            StandardIsland(1, "Ostia Nova", 200),
            StandardIsland(2, "Roma", 200)
        ];
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
    /// Creates an island frame.
    /// </summary>
    /// <param name="sessionGuid">The session GUID, which tells the islands apart</param>
    /// <param name="name">The island's name</param>
    /// <param name="gameTime">The game time</param>
    /// <param name="entries">The bytes of the entries</param>
    /// <returns>The frame</returns>
    private static byte[] Island(int sessionGuid, string name, long gameTime, params byte[][] entries)
    {
        return new PipeFrameBuilder().Byte(3)
                                     .Byte(7)
                                     .Byte(1)
                                     .Byte(0)
                                     .Int32(sessionGuid)
                                     .String(name)
                                     .Int64(gameTime)
                                     .Int32(entries.Length)
                                     .Raw([.. entries.SelectMany(entry => entry)])
                                     .Build();
    }

    /// <summary>
    /// Creates an island frame with one standard entry, a repeated workforce GUID and a building pair.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="name">The island's name</param>
    /// <param name="gameTime">The game time</param>
    /// <returns>The frame</returns>
    private static byte[] StandardIsland(int sessionGuid, string name, long gameTime)
    {
        return Island(sessionGuid, name, gameTime, EntryBytes(StandardEntry(sessionGuid + 10), [new(1, 20), new(1, 30)], [new(3, 4)]));
    }

    /// <summary>
    /// Creates an entry of non-zero, distinct values.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <returns>The entry</returns>
    private static ProductionEntry StandardEntry(int productGuid)
    {
        return Entry(productGuid, 1000f, 100, 500f, 400);
    }

    /// <summary>
    /// Creates an entry with the given generation, maintenance, income and profit and fixed other values.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="generation">The generation</param>
    /// <param name="maintenance">The maintenance</param>
    /// <param name="income">The income</param>
    /// <param name="profit">The profit</param>
    /// <returns>The entry</returns>
    private static ProductionEntry Entry(int productGuid, float generation, int maintenance, float income, int profit)
    {
        return new ProductionEntry
               {
                   ProductGuid = productGuid,
                   Generation = generation,
                   Consumption = 800f,
                   Delta = 200f,
                   PerfectGeneration = 1200f,
                   PerfectConsumption = 900f,
                   BuildingCount = 4,
                   Maintenance = maintenance,
                   Income = income,
                   Profit = profit,
                   SummedProductivity = 300f,
                   AverageProductivity = 75f,
                   WorkforceAmounts = _noAmounts,
                   BuildingAmounts = _noAmounts
               };
    }

    /// <summary>
    /// Writes the bytes of an entry with the given wire pairs of its maps.
    /// </summary>
    /// <param name="entry">The scalar values</param>
    /// <param name="workforce">The workforce pairs</param>
    /// <param name="buildings">The building pairs</param>
    /// <returns>The bytes</returns>
    private static byte[] EntryBytes(ProductionEntry entry, KeyValuePair<int, int>[] workforce, KeyValuePair<int, int>[] buildings)
    {
        return new PipeFrameBuilder().Int32(entry.ProductGuid)
                                     .Float32(entry.Generation)
                                     .Float32(entry.Consumption)
                                     .Float32(entry.Delta)
                                     .Float32(entry.PerfectGeneration)
                                     .Float32(entry.PerfectConsumption)
                                     .Int32(entry.BuildingCount)
                                     .Int32(entry.Maintenance)
                                     .Float32(entry.Income)
                                     .Int32(entry.Profit)
                                     .Float32(entry.SummedProductivity)
                                     .Float32(entry.AverageProductivity)
                                     .Map(workforce)
                                     .Map(buildings)
                                     .Build();
    }

    /// <summary>
    /// Creates the little-endian bytes of a float's bits.
    /// </summary>
    /// <param name="bits">The bits</param>
    /// <returns>The bytes</returns>
    private static byte[] Bits(uint bits)
    {
        var bytes = new byte[sizeof(uint)];

        BinaryPrimitives.WriteUInt32LittleEndian(bytes, bits);

        return bytes;
    }

    /// <summary>
    /// Reads the bits of a float in a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="offset">The offset of the float</param>
    /// <returns>The bits</returns>
    private static uint FloatBits(byte[] frame, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(offset));
    }

    /// <summary>
    /// Creates a recording from frames with their receive times; the header's start is the first time.
    /// </summary>
    /// <param name="lines">The frames with their receive times</param>
    /// <returns>The file bytes</returns>
    private static byte[] Recording(params (DateTimeOffset Time, byte[] Frame)[] lines)
    {
        var header = PipeRecordingFormat.FormatHeader(lines.Length > 0 ? lines[0].Time : _start);

        return Encoding.UTF8.GetBytes(string.Concat(header, string.Concat(lines.Select(line => PipeRecordingFormat.FormatFrameLine(line.Time, line.Frame)))));
    }

    /// <summary>
    /// Creates a recording of frames one second apart from the start on.
    /// </summary>
    /// <param name="frames">The frames</param>
    /// <returns>The file bytes</returns>
    private static byte[] RecordingOf(params byte[][] frames)
    {
        return Recording(frames.Select((frame, index) => (_start.AddSeconds(index), frame)).ToArray());
    }

    /// <summary>
    /// Splits an output into its lines without their line feeds.
    /// </summary>
    /// <param name="output">The output bytes</param>
    /// <returns>The lines</returns>
    private static List<string> OutputLines(byte[] output)
    {
        return Encoding.UTF8.GetString(output).Split('\n').SkipLast(1).ToList();
    }

    /// <summary>
    /// Reads the frame lines of an output.
    /// </summary>
    /// <param name="output">The output bytes</param>
    /// <returns>The receive times and frames in file order</returns>
    private static List<(DateTimeOffset Time, byte[] Frame)> OutputFrames(byte[] output)
    {
        return OutputLines(output).Skip(1)
                                  .Select(line => PipeRecordingFormat.TryParseFrameLine(Encoding.UTF8.GetBytes(line), out var time, out var frame)
                                                      ? (time, frame)
                                                      : throw new InvalidOperationException("The output holds an invalid frame line"))
                                  .ToList();
    }

    /// <summary>
    /// Parses an island frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The message</returns>
    private static AreaProductionStatisticsMessage ParseIsland(byte[] frame)
    {
        return PipeMessageParser.Parse(frame).Message as AreaProductionStatisticsMessage ?? throw new InvalidOperationException("The frame is no island frame");
    }

    /// <summary>
    /// Walks an island frame field by field and returns its wire layout.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The position behind the last field and, per entry, its offset and its map pairs in wire order</returns>
    private static (int End, List<(int Offset, List<KeyValuePair<int, int>> Workforce, List<KeyValuePair<int, int>> Buildings)> Entries) WalkIsland(byte[] frame)
    {
        var position = 9 + frame[8] + sizeof(long);
        var count = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(position));
        var entries = new List<(int Offset, List<KeyValuePair<int, int>> Workforce, List<KeyValuePair<int, int>> Buildings)>();

        position += sizeof(int);

        for (var index = 0; index < count; index++)
        {
            var offset = position;

            position += EntryScalarsLength;

            var workforce = ReadPairs(frame, ref position);
            var buildings = ReadPairs(frame, ref position);

            entries.Add((offset, workforce, buildings));
        }

        return (position, entries);
    }

    /// <summary>
    /// Reads the pairs of a map in wire order.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The position of the map's count; behind the map afterwards</param>
    /// <returns>The pairs</returns>
    private static List<KeyValuePair<int, int>> ReadPairs(byte[] frame, ref int position)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(position));
        var pairs = new List<KeyValuePair<int, int>>();

        position += sizeof(int);

        for (var index = 0; index < count; index++)
        {
            pairs.Add(new KeyValuePair<int, int>(BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(position)), BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(position + sizeof(int)))));
            position += 2 * sizeof(int);
        }

        return pairs;
    }

    /// <summary>
    /// Describes a map pair.
    /// </summary>
    /// <param name="pair">The pair</param>
    /// <returns>The description</returns>
    private static string DescribePair(KeyValuePair<int, int> pair)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{pair.Key}={pair.Value}");
    }

    /// <summary>
    /// Gives the name a frame carries, or "-" for a frame without a name.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The name</returns>
    private static string NameOf(byte[] frame)
    {
        return PipeMessageParser.Parse(frame).Message switch
               {
                   SessionStartMessage start => start.Headline,
                   AreaProductionStatisticsMessage area => area.AreaName,
                   _ => "-"
               };
    }

    /// <summary>
    /// Gives the ratios of the multiplied amounts of an anonymised entry to their originals.
    /// </summary>
    /// <param name="original">The original entry</param>
    /// <param name="anonymised">The anonymised entry</param>
    /// <returns>The ratios of generation, consumption, perfect generation, perfect consumption, income and summed productivity</returns>
    private static double[] MultipliedRatios(ProductionEntry original, ProductionEntry anonymised)
    {
        return
        [
            (double)anonymised.Generation / original.Generation,
            (double)anonymised.Consumption / original.Consumption,
            (double)anonymised.PerfectGeneration / original.PerfectGeneration,
            (double)anonymised.PerfectConsumption / original.PerfectConsumption,
            (double)anonymised.Income / original.Income,
            (double)anonymised.SummedProductivity / original.SummedProductivity
        ];
    }

    /// <summary>
    /// Describes what of a frame the anonymiser keeps: a version or session end whole, of an island frame its key, game
    /// time, entries, product GUIDs, building counts, building pairs and workforce GUIDs.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The description</returns>
    private static string DescribeStructure(byte[] frame)
    {
        var result = PipeMessageParser.Parse(frame);

        return result.Message switch
               {
                   AreaProductionStatisticsMessage area => DescribeIslandStructure(area, frame),
                   SessionStartMessage => "SessionStart",
                   null => $"Error {result.Error}",
                   _ => Convert.ToBase64String(frame)
               };
    }

    /// <summary>
    /// Describes what of an island frame the anonymiser keeps.
    /// </summary>
    /// <param name="area">The parsed frame</param>
    /// <param name="frame">The frame bytes</param>
    /// <returns>The description</returns>
    private static string DescribeIslandStructure(AreaProductionStatisticsMessage area, byte[] frame)
    {
        var wire = WalkIsland(frame).Entries;
        var entries = area.Entries.Select((entry, index) => string.Create(CultureInfo.InvariantCulture,
                                                                          $"{entry.ProductGuid} bc={entry.BuildingCount} w=[{string.Join(",", wire[index].Workforce.Select(pair => pair.Key))}] b=[{string.Join(",", wire[index].Buildings.Select(DescribePair))}]"));

        return string.Create(CultureInfo.InvariantCulture, $"Island {area.SessionId}/{area.IslandId}/{area.AreaIndex}/{area.SessionGuid} gt={area.Timestamp} [{string.Join(";", entries)}]");
    }

    /// <summary>
    /// Describes a replayed item without names and economic values, with its receive time moved earlier by a shift.
    /// </summary>
    /// <param name="item">The item</param>
    /// <param name="shift">The shift to subtract from a tick's receive time</param>
    /// <returns>The description</returns>
    private static string DescribeShape(PipeTickResult item, TimeSpan shift)
    {
        if (item.Tick is null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{item.Kind} version={item.Version} error={item.Error} key={item.IslandKey}");
        }

        var islands = string.Join(";", item.Tick.Islands.Select(DescribeIslandShape));

        return string.Create(CultureInfo.InvariantCulture, $"Tick {item.Tick.CloseReason} game={item.Tick.GameTime} received={(item.Tick.ReceiveTime - shift).UtcTicks} [{islands}]");
    }

    /// <summary>
    /// Describes a replayed island without its name and economic values.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The description</returns>
    private static string DescribeIslandShape(PipeTickIsland island)
    {
        var entries = island.Entries.Select(entry => string.Create(CultureInfo.InvariantCulture,
                                                                   $"{entry.ProductGuid} bc={entry.BuildingCount} w=[{string.Join(",", entry.WorkforceAmounts.Keys.Order())}] b=[{string.Join(",", entry.BuildingAmounts.OrderBy(pair => pair.Key).Select(DescribePair))}]"));

        return string.Create(CultureInfo.InvariantCulture, $"{island.Key} session={island.SessionId} [{string.Join(",", entries)}]");
    }

    /// <summary>
    /// Anonymises a recording held in memory.
    /// </summary>
    /// <param name="recording">The recording bytes</param>
    /// <param name="random">The random source</param>
    /// <returns>The result and the output bytes</returns>
    private Task<(PipeAnonymiseResult Result, byte[] Output)> AnonymiseAsync(byte[] recording, IPipeAnonymiserRandom random)
    {
        return AnonymiseAsync(new MemoryStream(recording), random);
    }

    /// <summary>
    /// Anonymises a recording stream into memory.
    /// </summary>
    /// <param name="recording">The recording stream</param>
    /// <param name="random">The random source</param>
    /// <returns>The result and the output bytes</returns>
    private async Task<(PipeAnonymiseResult Result, byte[] Output)> AnonymiseAsync(Stream recording, IPipeAnonymiserRandom random)
    {
        using var output = new MemoryStream();

        var result = await new PipeRecordingAnonymiser(random).AnonymiseAsync(recording, output, TestContext.CancellationToken);

        return (result, output.ToArray());
    }

    /// <summary>
    /// Replays a recording through the message reader and the tick assembler at maximum speed.
    /// </summary>
    /// <param name="recording">The recording bytes</param>
    /// <returns>All items, the end last</returns>
    private async Task<List<PipeTickResult>> ReplayAsync(byte[] recording)
    {
        var waits = new RecordingTimeProvider(_start);
        var clock = new PipeReplayClock(_start);

        using var stream = new PipeReplayStream(new MemoryStream(recording), PipeReplaySpeed.Maximum, clock, waits);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(stream), clock);

        var drain = DrainAsync(assembler);

        await waits.RunWaitsAsync(drain, TestContext.CancellationToken);

        return await drain;
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

    #endregion // Methods
}