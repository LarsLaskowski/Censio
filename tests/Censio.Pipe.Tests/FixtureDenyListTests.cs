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
    /// A non-empty route or folder name that is not an invented name of its kind fails and names the route.
    /// </summary>
    /// <param name="kind">The kind of the name: route or folder</param>
    /// <param name="name">The name</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("route", "Via Appia")]
    [DataRow("route", "Folder 1")]
    [DataRow("route", "Route 01")]
    [DataRow("route", "route 1")]
    [DataRow("folder", "North")]
    [DataRow("folder", "Route 1")]
    [DataRow("folder", "Folder 0")]
    public async Task FixtureDenyListNonInventedRouteOrFolderNameFails(string kind, string name)
    {
        // Arrange
        var route = kind == "route" ? TradeRouteOf(name, string.Empty) : TradeRouteOf(string.Empty, name);

        WriteFixture(RecordingOf(_version2, TradeRoutesFrame(TradeRouteOf("Route 1", "Folder 1"), route)));

        // Act
        var report = await FixtureDenyList.Empty.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { $"a.jsonl: line 3: route 2: {kind} name is not an invented name" }, report.Findings.ToArray(), "The name should not pass as invented");
        Assert.AreEqual(3L, report.NameCount, "The three non-empty route and folder names should be checked");
    }

    /// <summary>
    /// A non-zero problem, ship, lost-ship count or goods amount equal to the original's at the same line, route, station
    /// and good fails, by field name only; zero values and a negative goods amount (kept by design) are not compared.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task FixtureDenyListUnchangedTradeRouteAmountFails()
    {
        // Arrange
        var frames = RecordingOf(_version2, TradeRoutesFrame(AmountsRoute("Route 1", "Folder 1"), TradeRouteOf("Route 2", "Folder 1")));
        var original = WriteOriginal(frames);

        WriteFixture(frames);

        var list = FixtureDenyList.Parse($"original {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        string[] expected = [
                                "a.jsonl: line 3: route 1: MajorProblems equals the original",
                                "a.jsonl: line 3: route 1: MinorProblems equals the original",
                                "a.jsonl: line 3: route 1: Ships equals the original",
                                "a.jsonl: line 3: route 1: LostShips equals the original",
                                "a.jsonl: line 3: route 1: station 1: good 1: Amount equals the original"
                            ];

        Assert.AreSequenceEqual(expected, report.Findings.ToArray(), "Every unchanged non-zero amount should fail, by field name only");
        Assert.AreEqual(5L, report.ComparedValueCount, "The zero and negative values should not be compared");
    }

    /// <summary>
    /// A recording with trade routes and item offers anonymised by the anonymiser passes the check against its original,
    /// with its route and folder names and its amounts checked.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task FixtureDenyListAnonymisedTradeRoutesPass()
    {
        // Arrange
        var offers = new PipeFrameBuilder().Byte(5).ItemOffers([
                                                                   new ItemOffer
                                                                   {
                                                                       TraderId = 8,
                                                                       ItemGuids = [1000, 1001]
                                                                   }
                                                               ]).Build();
        var original = WriteOriginal(RecordingOf(_version2, SessionStart("Alice"), TradeRoutesFrame(AmountsRoute("Via Appia", "North")), offers));

        await AnonymiseAsync(original, Path.Combine(FixturesFolder, FixtureName));

        var list = FixtureDenyList.Parse($"deny Alice\ndeny via appia\ndeny North\noriginal {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckAsync(FixturesFolder, TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(report.Findings, report.ToString());
        Assert.AreEqual(3L, report.NameCount, "The figure, route and folder names should be checked");
        Assert.AreEqual(5L, report.ComparedValueCount, "The four counts and the positive goods amount should be compared");
    }

    /// <summary>
    /// With the local deny list, every trade-route and item-offer frame of the original of
    /// <c>protocol-3-trade-routes.jsonl</c> decodes, ends exactly at the frame's end and holds no U+FFFD in a name; without
    /// the variable the test is inconclusive, not passed.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task FixtureDenyListOriginalTradeFramesDecodeToTheirEnd()
    {
        // Arrange
        var list = await FixtureDenyList.LoadAsync(RequireDenyListPath(), TestContext.CancellationToken);

        // Act
        var report = await list.CheckOriginalTradeFramesAsync("protocol-3-trade-routes.jsonl", TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(report.Findings, report.ToString());
        Assert.IsGreaterThan(0L, report.TradeFrameCount, "The original should hold trade-route or item-offer frames");
    }

    /// <summary>
    /// A trade-route or item-offer frame of an original with bytes after its last field is reported by its line only.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task FixtureDenyListOriginalTradeFrameWithTrailingBytesIsReported()
    {
        // Arrange
        var routes = TradeRoutesFrame(TradeRouteOf("Via Appia", "North"));
        var offers = new PipeFrameBuilder().Byte(5).ItemOffers([
                                                                   new ItemOffer
                                                                   {
                                                                       TraderId = 8,
                                                                       ItemGuids = [1000]
                                                                   }
                                                               ]).Build();
        var original = WriteOriginal(RecordingOf(_version2, [.. routes, 0xAA, 0xBB], [.. offers, 0x00], routes, offers));
        var list = FixtureDenyList.Parse($"original {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckOriginalTradeFramesAsync(FixtureName, TestContext.CancellationToken);

        // Assert
        string[] expected = ["a.jsonl: original line 3: frame has bytes after its last field", "a.jsonl: original line 4: frame has bytes after its last field"];

        Assert.AreSequenceEqual(expected, report.Findings.ToArray(), "Both frames with trailing bytes should be reported by line");
        Assert.AreEqual(4L, report.TradeFrameCount, "Every trade-route and item-offer frame should be checked");
    }

    /// <summary>
    /// A route or folder name of an original that holds U+FFFD (an invalid UTF-8 byte) is reported by line, route and
    /// field only.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task FixtureDenyListOriginalTradeFrameWithReplacementCharacterIsReported()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(4)
                                          .Int32Value(2)
                                          .Route(TradeRouteOf("Via Appia", "North"))
                                          .Int32Value(9)
                                          .Byte(2)
                                          .Raw(0x41, 0xFF)
                                          .Byte(1)
                                          .Raw(0xC3)
                                          .Raw(new byte[20])
                                          .Build();
        var original = WriteOriginal(RecordingOf(_version2, frame));
        var list = FixtureDenyList.Parse($"original {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckOriginalTradeFramesAsync(FixtureName, TestContext.CancellationToken);

        // Assert
        string[] expected = ["a.jsonl: original line 3: route 2: route name holds U+FFFD", "a.jsonl: original line 3: route 2: folder name holds U+FFFD"];

        Assert.AreSequenceEqual(expected, report.Findings.ToArray(), "Both names with a replacement character should be reported by route and field");
    }

    /// <summary>
    /// A trade-route or item-offer frame of an original that does not decode is reported by its line; frames of other
    /// types are not checked.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task FixtureDenyListOriginalTradeFrameThatDoesNotDecodeIsReported()
    {
        // Arrange
        var original = WriteOriginal(RecordingOf(_version2, [4, 0xFF, 0xFF, 0xFF, 0xFF], [3, 1], [5, 2, 0, 0, 0]));
        var list = FixtureDenyList.Parse($"original {FixtureName} {original}\n", _root);

        // Act
        var report = await list.CheckOriginalTradeFramesAsync(FixtureName, TestContext.CancellationToken);

        // Assert
        string[] expected = ["a.jsonl: original line 3: frame does not decode", "a.jsonl: original line 5: frame does not decode"];

        Assert.AreSequenceEqual(expected, report.Findings.ToArray(), "Both malformed frames should be reported by line");
        Assert.AreEqual(2L, report.TradeFrameCount, "Only the trade-route and item-offer frames should be checked");
    }

    /// <summary>
    /// The original check of a fixture without an original line, or with a missing original, names the fixture only.
    /// </summary>
    /// <param name="listed">Whether the deny list names an original that does not exist</param>
    /// <param name="expected">The expected finding</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC12")]
    [DataRow(false, "a.jsonl: no original in the deny list")]
    [DataRow(true, "a.jsonl: the original is missing or unreadable")]
    public async Task FixtureDenyListOriginalTradeFramesWithoutOriginalFails(bool listed, string expected)
    {
        // Arrange
        var text = listed ? $"original {FixtureName} originals/missing.jsonl\n" : "deny Alice\n";
        var list = FixtureDenyList.Parse(text, _root);

        // Act
        var report = await list.CheckOriginalTradeFramesAsync(FixtureName, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { expected }, report.Findings.ToArray(), "The fixture should be named without its original");
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
        return new PipeFrameBuilder().Byte(1).StringValue(headline).Build();
    }

    /// <summary>
    /// Creates an island frame with one entry.
    /// </summary>
    /// <param name="name">The island's name</param>
    /// <param name="entry">The bytes of the entry</param>
    /// <returns>The frame</returns>
    private static byte[] Island(string name, byte[] entry)
    {
        return new PipeFrameBuilder().Byte(3).Byte(7).Byte(1).Byte(0).Int32Value(1).StringValue(name).Int64Value(100).Int32Value(1).Raw(entry).Build();
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
        return new PipeFrameBuilder().Int32Value(10).Raw(new byte[44]).Int32Value(0).Int32Value(0).Build();
    }

    /// <summary>
    /// Creates a trade route without counts and stations.
    /// </summary>
    /// <param name="name">The route name</param>
    /// <param name="folder">The folder name</param>
    /// <returns>The route</returns>
    private static TradeRoute TradeRouteOf(string name, string folder)
    {
        return new TradeRoute
               {
                   Id = 7,
                   Name = name,
                   Folder = folder,
                   MajorProblems = 0,
                   MinorProblems = 0,
                   Ships = 0,
                   LostShips = 0,
                   Stations = []
               };
    }

    /// <summary>
    /// Creates a trade route with non-zero counts (lost ships at most ships) and one station of three goods: a positive,
    /// a negative and a zero amount.
    /// </summary>
    /// <param name="name">The route name</param>
    /// <param name="folder">The folder name</param>
    /// <returns>The route</returns>
    private static TradeRoute AmountsRoute(string name, string folder)
    {
        TradeRouteGood[] goods = [
                                     new TradeRouteGood
                                     {
                                         ProductGuid = 1010,
                                         Amount = 30,
                                         Loading = true
                                     },
                                     new TradeRouteGood
                                     {
                                         ProductGuid = 1011,
                                         Amount = -4,
                                         Loading = false
                                     },
                                     new TradeRouteGood
                                     {
                                         ProductGuid = 1012,
                                         Amount = 0,
                                         Loading = true
                                     }
                                 ];
        var station = new TradeRouteStation
                      {
                          Id = 11,
                          SessionId = 1,
                          IslandId = 2,
                          AreaIndex = 0,
                          SessionGuid = 180023,
                          Owner = 7,
                          ThrowOverboard = false,
                          WaitUntilFull = true,
                          WaitUntilEmpty = false,
                          Goods = goods
                      };

        return new TradeRoute
               {
                   Id = 7,
                   Name = name,
                   Folder = folder,
                   MajorProblems = 2,
                   MinorProblems = 9,
                   Ships = 12,
                   LostShips = 3,
                   Stations = [station]
               };
    }

    /// <summary>
    /// Creates a trade-route frame.
    /// </summary>
    /// <param name="routes">The routes</param>
    /// <returns>The frame</returns>
    private static byte[] TradeRoutesFrame(params TradeRoute[] routes)
    {
        return new PipeFrameBuilder().Byte(4).TradeRoutes(routes).Build();
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