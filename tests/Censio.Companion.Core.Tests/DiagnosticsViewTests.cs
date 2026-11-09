using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Censio.GameData;
using Censio.Pipe;
using Censio.Pipe.Tests;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="DiagnosticsView" />.
/// </summary>
[TestClass]
public sealed class DiagnosticsViewTests
{
    #region Fields

    /// <summary>
    /// The receive time of the first frame of the recordings, and the start of the live clock.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// The receive time of the ticks built directly: 17:09:04 in the local time zone of <see cref="CreateClock" />.
    /// </summary>
    private static readonly DateTimeOffset _received = new(2026, 11, 1, 17, 9, 4, TimeSpan.Zero);

    /// <summary>
    /// The current time of the clock of the views, on the date of <see cref="_received" />.
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The indexes of the float columns: generation, consumption, delta, perfect generation, perfect consumption, income
    /// and both productivities.
    /// </summary>
    private static readonly int[] _floatColumns = [6, 7, 8, 9, 10, 13, 15, 16];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsViewTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public DiagnosticsViewTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The table has the 17 columns of the window, left to right.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewColumnsInOrder()
    {
        // Arrange
        var expected = new[]
                       {
                           "Region",
                           "Island",
                           "Area",
                           "Session ID",
                           "Name",
                           "Good",
                           "Generation",
                           "Consumption",
                           "Delta",
                           "Perfect generation",
                           "Perfect consumption",
                           "Buildings",
                           "Maintenance",
                           "Income",
                           "Profit",
                           "Productivity (sum)",
                           "Productivity (avg)"
                       };

        // Act
        var columns = DiagnosticsView.Columns;

        // Assert
        Assert.AreSequenceEqual(expected, columns.ToArray(), "The columns should be the ones of the window, in order");
    }

    /// <summary>
    /// With a tick the header holds the status, the last tick as the menu shows it and the game time in decimal.
    /// </summary>
    /// <param name="gameTime">The game time of the tick</param>
    /// <param name="expected">The expected game-time line</param>
    [TestMethod]
    [DataRow(123456789012L, "Game time: 123456789012")]
    [DataRow(-5L, "Game time: -5")]
    public void DiagnosticsViewCreateHeaderShowsStatusLastTickAndGameTime(long gameTime, string expected)
    {
        // Arrange
        var tick = CreateTick(gameTime,
                              CreateIsland(new PipeIslandKey(1, 1, 0), 4, "Ostia", CreateEntry(10)),
                              CreateIsland(new PipeIslandKey(2, 1, 0), 4, "Capua", CreateEntry(20)));

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Assert
        Assert.AreSequenceEqual(new[] { "Status: Receiving", "Last tick: 17:09:04, 2 islands", expected }, view.HeaderLines.ToArray(), "The header should hold the status, the last tick and the game time");
        Assert.AreSame(tick, view.Tick, "The view should show the given tick");
    }

    /// <summary>
    /// A tick whose islands have no entries appends the no-data marker to the game time.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewCreateNoDataYetAppendsMarker()
    {
        // Arrange
        var tick = CreateTick(100,
                              CreateIsland(new PipeIslandKey(1, 1, 0), 4, "Ostia"),
                              CreateIsland(new PipeIslandKey(2, 1, 0), 4, "Capua"));

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Assert
        string[] expected = ["Status: Receiving", "Last tick: 17:09:04, 2 islands", "Game time: 100 (no data yet)"];

        Assert.AreSequenceEqual(expected, view.HeaderLines.ToArray(), "The game time should carry the no-data marker");
    }

    /// <summary>
    /// The rows follow the islands in tick order and the goods of each island in frame order, with the region and island
    /// columns on every row and each value in its column.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewRowsFollowIslandAndEntryOrder()
    {
        // Arrange
        var tick = CreateTick(100,
                              CreateIsland(new PipeIslandKey(-77, 2, 3), 4, "Ostia", CreateEntry(30), CreateEntry(10)),
                              CreateIsland(new PipeIslandKey(5, 1, 0), 6, "Capua", CreateEntry(20)));
        var expected = new[]
                       {
                           "-77|2|3|4|Ostia|30|1.5|2.25|-0.75|3.5|4.125|6|70|8.5|-9|1.25|0.625",
                           "-77|2|3|4|Ostia|10|1.5|2.25|-0.75|3.5|4.125|6|70|8.5|-9|1.25|0.625",
                           "5|1|0|6|Capua|20|1.5|2.25|-0.75|3.5|4.125|6|70|8.5|-9|1.25|0.625"
                       };

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Assert
        Assert.HasCount(3, view, "The view should have one row per good");
        Assert.AreSequenceEqual(expected, RowTexts(view), "The rows should follow island and entry order, each value in its column");

        int[] indexes = [0, 1, 2];

        Assert.AreSequenceEqual(indexes, Enumerable.Range(0, view.Count).Select(index => view[index].Index).ToArray(), "Each row should carry its index");
    }

    /// <summary>
    /// An island without entries has one row with its key columns, the no-entries text in the good column and empty value
    /// columns.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewIslandWithoutEntriesHasNoEntriesRow()
    {
        // Arrange
        var tick = CreateTick(100,
                              CreateIsland(new PipeIslandKey(1, 1, 0), 4, "Ostia", CreateEntry(10)),
                              CreateIsland(new PipeIslandKey(8, 9, 1), 4, "Neu Ostia"),
                              CreateIsland(new PipeIslandKey(2, 1, 0), 4, "Capua", CreateEntry(20)));
        string[] expected = ["8", "9", "1", "4", "Neu Ostia", "(no entries)", .. Enumerable.Repeat(string.Empty, 11)];

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Assert
        Assert.HasCount(3, view, "The island without entries should have one row");
        Assert.AreSequenceEqual(expected, view[1].Cells.ToArray(), "The row should hold the key columns, (no entries) and empty values");
        Assert.AreEqual("(no entries)", DiagnosticsView.NoEntriesText, "The text of the good column should be (no entries)");
    }

    /// <summary>
    /// Float cells are the shortest invariant text that reads back as the same value, negative zero, NaN and the
    /// infinities included.
    /// </summary>
    /// <param name="bits">The bits of the float</param>
    /// <param name="isNegative">Whether the sign bit of the float is set</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [DataRow(0x3DCCCCCD, false, "0.1")]
    [DataRow(unchecked((int)0xC0200000), true, "-2.5")]
    [DataRow(0x7E967699, false, "1E+38")]
    [DataRow(unchecked((int)0x80000000), true, "-0")]
    [DataRow(unchecked((int)0xFFC00000), true, "NaN")]
    [DataRow(0x7F800000, false, "Infinity")]
    [DataRow(unchecked((int)0xFF800000), true, "-Infinity")]
    public void DiagnosticsViewFloatCellsUseShortestInvariantText(int bits, bool isNegative, string expected)
    {
        // Arrange
        var input = BitConverter.Int32BitsToSingle(bits);
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(1, 1, 0), 4, "Ostia", CreateEntry(10, input, 1, 2, 3)));
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Act
        var row = view[0];

        // Assert
        Assert.AreEqual(isNegative, float.IsNegative(input), "The input should keep its sign bit");
        Assert.AreSequenceEqual(Enumerable.Repeat(expected, _floatColumns.Length).ToArray(), _floatColumns.Select(column => row.Cells[column]).ToArray(), "Every float column should show the shortest invariant text");
    }

    /// <summary>
    /// Integer cells show negative values in decimal with a leading minus.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewIntegerCellsShowNegativeValues()
    {
        // Arrange
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(-77, 1, 0), 4, "Ostia", CreateEntry(-123, 1f, -1, -2000, int.MinValue)));
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Act
        var row = view[0];

        // Assert
        string[] expected = ["-77", "-123", "-1", "-2000", "-2147483648"];

        Assert.AreSequenceEqual(expected, new[] { row.Cells[0], row.Cells[5], row.Cells[11], row.Cells[12], row.Cells[14] }, "Region, good, buildings, maintenance and profit should show their negative values");
    }

    /// <summary>
    /// Under a German culture the numbers keep the decimal point and have no digit grouping.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewNumbersIgnoreGermanCulture()
    {
        // Arrange
        var tick = CreateTick(1234567, CreateIsland(new PipeIslandKey(1234567, 1, 0), 4, "Ostia", CreateEntry(1234567, -2.5f, 1234567, 1234567, 1234567)));
        var clock = CreateClock();
        var german = CultureInfo.GetCultureInfo("de-DE");
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        string germanFloat;
        IReadOnlyList<string> header;
        IReadOnlyList<string> cells;

        try
        {
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;
            germanFloat = (-2.5f).ToString(CultureInfo.CurrentCulture);

            // Act
            var view = DiagnosticsView.Create("Receiving", tick, clock);

            header = view.HeaderLines;
            cells = view[0].Cells;
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        // Assert
        Assert.AreEqual("-2,5", germanFloat, "The German culture should use a decimal comma, or the test proves nothing");
        Assert.AreEqual("Game time: 1234567", header[2], "The game time should have no digit grouping");

        string[] expected = ["1234567", "1234567", "-2.5", "1234567", "1234567", "-2.5", "1234567"];

        Assert.AreSequenceEqual(expected, new[] { cells[0], cells[5], cells[6], cells[11], cells[12], cells[13], cells[14] }, "The cells should use the decimal point and no digit grouping");
    }

    /// <summary>
    /// A replayed recording ends with the last tick's receive time, game time, GUIDs and values in the header and the
    /// table, at maximum and at a finite speed.
    /// </summary>
    /// <param name="speedText">The speed as the command line gives it</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow("max")]
    [DataRow("1")]
    public async Task DiagnosticsViewReplayedRecordingShowsFrameValues(string speedText)
    {
        // Arrange
        var parsed = PipeReplaySpeed.TryParse(speedText, out var speed);
        var runner = await ReplayAsync(speed);

        // Act
        var view = DiagnosticsView.Create("Replay finished", runner.LatestTick, CreateClock());

        // Assert
        string[] expected = ["Last tick: 17:09:04, 3 islands", "Game time: 500"];

        Assert.IsTrue(parsed, "The speed should be valid");
        Assert.AreSequenceEqual(expected, view.HeaderLines.Skip(1).ToArray(), "The header should carry the receive time and the game time of the last tick");
        Assert.AreSequenceEqual(ExpectedRows(), RowTexts(view), "The table should hold the GUIDs and values of the last tick's frames");
    }

    /// <summary>
    /// The same frames fed as a live connection give the same header lines, the status aside, and the same table as their
    /// replay.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task DiagnosticsViewLiveConnectionEqualsReplay()
    {
        // Arrange
        var live = await RunLiveAsync();
        var replay = await ReplayAsync(PipeReplaySpeed.Maximum);
        var clock = CreateClock();

        // Act
        var liveView = DiagnosticsView.Create("Receiving", live, clock);
        var replayView = DiagnosticsView.Create("Replay finished", replay.LatestTick, clock);

        // Assert
        Assert.AreSequenceEqual(replayView.HeaderLines.Skip(1).ToArray(), liveView.HeaderLines.Skip(1).ToArray(), "The live header should equal the replayed one, the status aside");
        Assert.AreSequenceEqual(RowTexts(replayView), RowTexts(liveView), "The live table should equal the replayed one");
        Assert.AreSequenceEqual(ExpectedRows(), RowTexts(liveView), "The live table should hold the values of the frames");
    }

    /// <summary>
    /// Without a tick the header holds the status and no last tick, and the table is empty.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewCreateWithoutTickShowsNoneAndNoRows()
    {
        // Arrange
        var clock = CreateClock();

        // Act
        var view = DiagnosticsView.Create("Receiving", null, clock);

        // Assert
        string[] expected = ["Status: Receiving", "Last tick: none"];

        Assert.AreSequenceEqual(expected, view.HeaderLines.ToArray(), "The header should hold the status and Last tick: none only");
        Assert.IsEmpty(view, "The table should be empty");
        Assert.IsNull(view.Tick, "The view should show no tick");
    }

    /// <summary>
    /// With an invalid command line the header names it, shows no last tick, and the table is empty.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewCreateInvalidCommandLineShowsStatus()
    {
        // Arrange
        var statusText = TraySnapshot.ForInvalidCommandLine().StatusText;

        // Act
        var view = DiagnosticsView.Create(statusText, null, CreateClock());

        // Assert
        string[] expected = ["Status: Invalid command line", "Last tick: none"];

        Assert.AreSequenceEqual(expected, view.HeaderLines.ToArray(), "The header should name the invalid command line and show no last tick");
        Assert.IsEmpty(view, "The table should be empty");
    }

    /// <summary>
    /// A control character, a format character, a line or paragraph separator or a lone surrogate in a name is shown as
    /// one question mark.
    /// </summary>
    /// <param name="codeUnit">The UTF-16 code unit put into the name</param>
    [TestMethod]
    [DataRow(0x0007)]
    [DataRow(0x202E)]
    [DataRow(0x200B)]
    [DataRow(0x2028)]
    [DataRow(0x2029)]
    [DataRow(0xD800)]
    public void DiagnosticsViewNameReplacesControlAndFormatCharacters(int codeUnit)
    {
        // Arrange
        var name = string.Concat("A", new string((char)codeUnit, 1), "B");
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(1, 1, 0), 4, name, CreateEntry(10)));
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Act
        var cell = view[0].Cells[4];

        // Assert
        Assert.AreEqual(codeUnit, (int)name[1], "The input should hold the code unit of the case");
        Assert.AreEqual("A?B", cell, "The character should be shown as one question mark");
    }

    /// <summary>
    /// A name of the longest pipe string, 255 bytes with spaces at both ends, is shown whole.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewNameOfMaximumLengthIsNotCut()
    {
        // Arrange
        var name = $" {new string('x', 253)} ";
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(1, 1, 0), 4, name, CreateEntry(10)));
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Act
        var cell = view[0].Cells[4];

        // Assert
        Assert.AreEqual(255, Encoding.UTF8.GetByteCount(name), "The input should be the longest pipe string");
        Assert.AreEqual(name, cell, "The name should be shown whole");
    }

    /// <summary>
    /// An empty name, a name of spaces only and a name with spaces are shown as sent.
    /// </summary>
    /// <param name="name">The name</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(" Neu Ostia ")]
    public void DiagnosticsViewNameKeepsSpaces(string name)
    {
        // Arrange
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(1, 1, 0), 4, name, CreateEntry(10)));
        var view = DiagnosticsView.Create("Receiving", tick, CreateClock());

        // Act
        var cell = view[0].Cells[4];

        // Assert
        Assert.AreEqual(name, cell, "The name should be shown as sent");
    }

    /// <summary>
    /// A tick at the pipe's limits gives its row count and any one row without making the text of the other rows.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewCreateAtTickLimitsMakesNoRowText()
    {
        // Arrange
        var tick = CreateTickAtLimits();
        var clock = CreateClock();
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, clock);

        // Assert
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        string[] first = ["1000", "0", "4242"];
        string[] secondIsland = ["1001", "1", "4242"];
        string[] last = ["1255", "255", "4242"];

        Assert.IsLessThan(1024L * 1024L, allocated, "Creating the view should not make the text of its rows");
        Assert.HasCount(524288, view, "The view should have one row per entry");
        Assert.AreSequenceEqual(first, KeyAndGood(view[0]), "The first row should be the first good of the first island");
        Assert.AreSequenceEqual(secondIsland, KeyAndGood(view[2048]), "Row 2,048 should be the first good of the second island");
        Assert.AreSequenceEqual(last, KeyAndGood(view[524287]), "The last row should be the last good of the last island");
    }

    /// <summary>
    /// Finding a row of a tick at the pipe's limits reads its index instead of scanning the rows.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewIndexOfDoesNotScan()
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTickAtLimits(), CreateClock());
        var row = view[524287];
        IList list = view;
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var index = list.IndexOf(row);

        // Assert
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(524287, index, "The row should be found at its index");
        Assert.IsLessThan(64L * 1024L, allocated, "Finding the row should not make the text of the other rows");
    }

    /// <summary>
    /// As an <see cref="IList" /> the view is read-only, of fixed size and not synchronized, with itself as its root.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewListIsReadOnlyFixedSize()
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());

        // Act
        IList list = view;

        // Assert
        Assert.IsTrue(list.IsReadOnly, "The view should be read-only");
        Assert.IsTrue(list.IsFixedSize, "The view should be of fixed size");
        Assert.IsFalse(list.IsSynchronized, "The view should not be synchronized");
        Assert.AreSame(view, list.SyncRoot, "The view should be its own synchronization root");
    }

    /// <summary>
    /// Every member that would change the list throws <see cref="NotSupportedException" /> and leaves the rows as they
    /// are.
    /// </summary>
    /// <param name="member">The name of the mutating member</param>
    [TestMethod]
    [DataRow("Add")]
    [DataRow("Clear")]
    [DataRow("Insert")]
    [DataRow("Remove")]
    [DataRow("RemoveAt")]
    [DataRow("Indexer")]
    public void DiagnosticsViewListMutatorThrowsNotSupported(string member)
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());
        IList list = view;
        var row = view[0];
        Action mutate = member switch
                        {
                            "Add" => () => list.Add(row),
                            "Clear" => list.Clear,
                            "Insert" => () => list.Insert(0, row),
                            "Remove" => () => list.Remove(row),
                            "RemoveAt" => () => list.RemoveAt(0),
                            _ => () => list[0] = row
                        };

        // Act
        var exception = Assert.ThrowsExactly<NotSupportedException>(mutate, $"{member} should not be supported");

        // Assert
        Assert.AreEqual("The view is read-only", exception.Message, "The exception should say that the view is read-only");
        Assert.HasCount(3, view, "The rows should be left as they are");
    }

    /// <summary>
    /// The untyped indexer returns the row of the typed one, with the same index and cells.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewListIndexerReturnsRow()
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());
        IList list = view;

        // Act
        var item = list[2];

        // Assert
        var row = item as DiagnosticsRow;

        Assert.IsNotNull(row, "The item should be a row");
        Assert.AreEqual(2, row.Index, "The row should carry its index");
        Assert.AreSequenceEqual(view[2].Cells.ToArray(), row.Cells.ToArray(), "The row should have the cells of the typed indexer");
    }

    /// <summary>
    /// An index outside the rows throws <see cref="ArgumentOutOfRangeException" />.
    /// </summary>
    /// <param name="index">The index</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    public void DiagnosticsViewIndexerOutsideRowsThrows(int index)
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());

        // Act
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => view[index], "An index outside the rows should throw");

        // Assert
        Assert.AreEqual("index", exception.ParamName, "The exception should name the index");
    }

    /// <summary>
    /// A row of the view is found at its index and contained; a row whose index is outside the rows, a row whose cells
    /// differ from the ones at its index, null and an object that is no row are not.
    /// </summary>
    /// <param name="index">The index the row carries</param>
    /// <param name="goodCell">The good cell of the row, or null for the cells the view has at that index</param>
    /// <param name="expected">The expected index</param>
    [TestMethod]
    [DataRow(1, null, 1)]
    [DataRow(2, null, 2)]
    [DataRow(1, "30", -1)]
    [DataRow(-1, null, -1)]
    [DataRow(3, null, -1)]
    public void DiagnosticsViewListIndexOfAndContainsCompareCells(int index, string? goodCell, int expected)
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());
        IList list = view;
        var cells = view[Math.Clamp(index, 0, view.Count - 1)].Cells.ToArray();

        if (goodCell is not null)
        {
            cells[5] = goodCell;
        }

        var row = new DiagnosticsRow(index, cells);

        // Act
        var found = list.IndexOf(row);

        // Assert
        Assert.AreEqual(expected, found, "The row should be found only at its own index with the cells of that index");
        Assert.AreEqual(expected >= 0, list.Contains(row), "Contains should agree with IndexOf");
    }

    /// <summary>
    /// Null and an object that is no row are neither found nor contained.
    /// </summary>
    /// <param name="useNull">Whether the value is null instead of a string</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DiagnosticsViewListIndexOfNonRowReturnsMinusOne(bool useNull)
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());
        IList list = view;
        var value = useNull ? null : "30";

        // Act
        var found = list.IndexOf(value);
        var contained = list.Contains(value);

        // Assert
        Assert.AreEqual(-1, found, "A value that is no row should not be found");
        Assert.IsFalse(contained, "A value that is no row should not be contained");
    }

    /// <summary>
    /// Enumerating the view, typed and untyped, yields its rows in order with their indexes and cells.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewEnumerationYieldsRowsInOrder()
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());
        IEnumerable untyped = view;

        // Act
        var typedRows = view.ToArray();
        var untypedRows = new List<DiagnosticsRow>();
        var enumerator = untyped.GetEnumerator();

        while (enumerator.MoveNext())
        {
            untypedRows.Add((DiagnosticsRow)enumerator.Current);
        }

        // Assert
        string[] expected = ["30", "10", "20"];
        int[] indexes = [0, 1, 2];

        Assert.AreSequenceEqual(indexes, typedRows.Select(row => row.Index).ToArray(), "The typed enumeration should yield the rows in order");
        Assert.AreSequenceEqual(expected, typedRows.Select(row => row.Cells[5]).ToArray(), "The typed enumeration should yield the cells of each row");
        Assert.AreSequenceEqual(indexes, untypedRows.Select(row => row.Index).ToArray(), "The untyped enumeration should yield the rows in order");
        Assert.AreSequenceEqual(expected, untypedRows.Select(row => row.Cells[5]).ToArray(), "The untyped enumeration should yield the cells of each row");
    }

    /// <summary>
    /// Copying the view into an array writes its rows in order from the given index on.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewCopyToWritesRowsFromIndex()
    {
        // Arrange
        var view = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());
        ICollection collection = view;
        var array = new object?[5];

        // Act
        collection.CopyTo(array, 1);

        // Assert
        string[] expected = ["30", "10", "20"];

        Assert.IsNull(array[0], "The slot before the index should stay empty");
        Assert.IsNull(array[4], "The slot after the rows should stay empty");
        Assert.AreSequenceEqual(expected, array.Skip(1).Take(3).Cast<DiagnosticsRow>().Select(row => row.Cells[5]).ToArray(), "The rows should be copied in order from the index on");
    }

    /// <summary>
    /// Copying the view into no array throws <see cref="ArgumentNullException" />.
    /// </summary>
    [TestMethod]
    public void DiagnosticsViewCopyToNullArrayThrows()
    {
        // Arrange
        ICollection collection = DiagnosticsView.Create("Receiving", CreateTwoIslandTick(), CreateClock());

        // Act
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => collection.CopyTo(null!, 0), "Copying into no array should throw");

        // Assert
        Assert.AreEqual("array", exception.ParamName, "The exception should name the array");
    }

    /// <summary>
    /// With a catalog, the region and good cells of a known GUID show both names and the GUID.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void DiagnosticsViewCreateShowsBothNamesWithGuid()
    {
        // Arrange
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(100, 1, 0), 4, "Ostia", CreateEntry(200)));
        var catalog = CreateCatalog(Named(100, GameCatalogKind.Region, "Region 1", "Gebiet 1"), Named(200, GameCatalogKind.Good, "Good 1", "Ware 1"));

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, catalog, CreateClock());

        // Assert
        Assert.AreEqual("Region 1 / Gebiet 1 (100)", view[0].Cells[0], "The region cell should show both names and the GUID");
        Assert.AreEqual("Good 1 / Ware 1 (200)", view[0].Cells[5], "The good cell should show both names and the GUID");
        Assert.AreEqual("1", view[0].Cells[1], "The other cells should be unchanged");
    }

    /// <summary>
    /// An entry with one name shows the name it has and the GUID.
    /// </summary>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <param name="expected">The expected cell</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("Good 2", null, "Good 2 (201)", DisplayName = "English only")]
    [DataRow(null, "Ware 2", "Ware 2 (201)", DisplayName = "German only")]
    [DataRow("Good 2", "", "Good 2 (201)", DisplayName = "German empty")]
    public void DiagnosticsViewCreateShowsAvailableNameOfIncompleteEntry(string? english, string? german, string expected)
    {
        // Arrange
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(100, 1, 0), 4, "Ostia", CreateEntry(201)));
        var catalog = CreateCatalog(Named(201, GameCatalogKind.Good, english, german));

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, catalog, CreateClock());

        // Assert
        Assert.AreEqual(expected, view[0].Cells[5], "The cell should show the name the entry has and the GUID");
    }

    /// <summary>
    /// A GUID the catalog does not know, a nameless entry and an ambiguous GUID stay numbers.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void DiagnosticsViewCreateShowsNumberOfUnknownNamelessAndAmbiguous()
    {
        // Arrange
        var tick = CreateTick(100,
                              CreateIsland(new PipeIslandKey(300, 1, 0), 4, "Ostia", CreateEntry(301), CreateEntry(302), CreateEntry(303)),
                              CreateIsland(new PipeIslandKey(100, 2, 0), 4, "Capua", CreateEntry(200)));
        var catalog = new GameCatalog([Named(302, GameCatalogKind.Good, null, null), Named(100, GameCatalogKind.Region, "Region 1", "Gebiet 1"), Named(200, GameCatalogKind.Good, "Good 1", "Ware 1")], [303], 0, 0);

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, catalog, CreateClock());

        // Assert
        Assert.AreEqual("300", view[0].Cells[0], "An unknown region should stay a number");
        Assert.AreEqual("301", view[0].Cells[5], "An unknown good should stay a number");
        Assert.AreEqual("302", view[1].Cells[5], "A nameless good should stay a number");
        Assert.AreEqual("303", view[2].Cells[5], "An ambiguous good should stay a number");
        Assert.AreEqual("Good 1 / Ware 1 (200)", view[3].Cells[5], "A known good in the same tick should be named");
    }

    /// <summary>
    /// GUID 0 is never looked up: it stays <c>0</c> even if the catalog holds an entry for it, while a known GUID of the
    /// same tick is named.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void DiagnosticsViewCreateShowsZeroAsNumber()
    {
        // Arrange
        var tick = CreateTick(100,
                              CreateIsland(new PipeIslandKey(0, 1, 0), 4, "Ostia", CreateEntry(0), CreateEntry(200)));
        var catalog = CreateCatalog(Named(0, GameCatalogKind.Good, "Good 0", "Ware 0"), Named(200, GameCatalogKind.Good, "Good 1", "Ware 1"));

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, catalog, CreateClock());

        // Assert
        Assert.AreEqual("0", view[0].Cells[0], "Region 0 should stay 0");
        Assert.AreEqual("0", view[0].Cells[5], "Good 0 should stay 0");
        Assert.AreEqual("Good 1 / Ware 1 (200)", view[1].Cells[5], "A known good of the same tick should be named");
    }

    /// <summary>
    /// Names follow the rule of the log: control, format and separator characters become <c>?</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void DiagnosticsViewCreateSanitizesNames()
    {
        // Arrange
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(100, 1, 0), 4, "Ostia", CreateEntry(200)));
        var catalog = CreateCatalog(Named(200, GameCatalogKind.Good, "Good\u0007\n1", "Ware\u202E2\u2028"));

        // Act
        var view = DiagnosticsView.Create("Receiving", tick, catalog, CreateClock());

        // Assert
        Assert.AreEqual("Good??1 / Ware?2? (200)", view[0].Cells[5], "Control, format and separator characters should be replaced");
    }

    /// <summary>
    /// A name over <see cref="DiagnosticsView.MaxCatalogNameLength" /> characters is cut and ends in an ellipsis, one at
    /// the limit is kept.
    /// </summary>
    /// <param name="length">The length of the English name</param>
    /// <param name="isCut">Whether the name is cut</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(4096, false)]
    [DataRow(4097, true)]
    [DataRow(100000, true)]
    public void DiagnosticsViewCreateCutsLongName(int length, bool isCut)
    {
        // Arrange
        var tick = CreateTick(100, CreateIsland(new PipeIslandKey(100, 1, 0), 4, "Ostia", CreateEntry(200)));
        var catalog = CreateCatalog(Named(200, GameCatalogKind.Good, new string('x', length), "Ware 1"));
        const string tail = " / Ware 1 (200)";

        // Act
        var cell = DiagnosticsView.Create("Receiving", tick, catalog, CreateClock())[0].Cells[5];

        // Assert
        Assert.IsTrue(cell.EndsWith(tail, StringComparison.Ordinal), "The cell should end with the German name and the GUID");

        var name = cell[..^tail.Length];

        Assert.AreEqual(isCut, name.EndsWith('\u2026'), "A name over the limit should end in an ellipsis, one at the limit should not");
        Assert.IsLessThanOrEqualTo(DiagnosticsView.MaxCatalogNameLength + 1, name.Length, "The name should be cut to the limit");

        if (isCut == false)
        {
            Assert.AreEqual(length, name.Length, "A name at the limit should be kept whole");
        }
    }

    /// <summary>
    /// Creates a catalog of entries.
    /// </summary>
    /// <param name="entries">The entries</param>
    /// <returns>The catalog</returns>
    private static GameCatalog CreateCatalog(params GameCatalogEntry[] entries)
    {
        return new GameCatalog(entries, [], 0, 0);
    }

    /// <summary>
    /// Creates a catalog entry.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="kind">The kind</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <returns>The entry</returns>
    private static GameCatalogEntry Named(int guid, GameCatalogKind kind, string? english, string? german)
    {
        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = kind,
                   EnglishName = english,
                   GermanName = german
               };
    }

    /// <summary>
    /// Creates a tick of two islands with the goods 30 and 10, and 20: three rows.
    /// </summary>
    /// <returns>The tick</returns>
    private static PipeTick CreateTwoIslandTick()
    {
        return CreateTick(100,
                          CreateIsland(new PipeIslandKey(-77, 2, 3), 4, "Ostia", CreateEntry(30), CreateEntry(10)),
                          CreateIsland(new PipeIslandKey(5, 1, 0), 6, "Capua", CreateEntry(20)));
    }

    /// <summary>
    /// Creates a clock at <see cref="_now" /> in a local time zone without offset.
    /// </summary>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock()
    {
        var clock = new FakeTimeProvider(_now);

        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Censio Test", TimeSpan.Zero, "Censio Test", "Censio Test"));

        return clock;
    }

    /// <summary>
    /// Creates a production entry whose values differ from column to column.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <returns>The entry</returns>
    private static ProductionEntry CreateEntry(int productGuid)
    {
        return new ProductionEntry
               {
                   ProductGuid = productGuid,
                   Generation = 1.5f,
                   Consumption = 2.25f,
                   Delta = -0.75f,
                   PerfectGeneration = 3.5f,
                   PerfectConsumption = 4.125f,
                   BuildingCount = 6,
                   Maintenance = 70,
                   Income = 8.5f,
                   Profit = -9,
                   SummedProductivity = 1.25f,
                   AverageProductivity = 0.625f,
                   WorkforceAmounts = new Dictionary<int, int>(),
                   BuildingAmounts = new Dictionary<int, int>()
               };
    }

    /// <summary>
    /// Creates a production entry with one value in every float column.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="value">The value of every float column</param>
    /// <param name="buildingCount">The building count</param>
    /// <param name="maintenance">The maintenance</param>
    /// <param name="profit">The profit</param>
    /// <returns>The entry</returns>
    private static ProductionEntry CreateEntry(int productGuid, float value, int buildingCount, int maintenance, int profit)
    {
        return new ProductionEntry
               {
                   ProductGuid = productGuid,
                   Generation = value,
                   Consumption = value,
                   Delta = value,
                   PerfectGeneration = value,
                   PerfectConsumption = value,
                   BuildingCount = buildingCount,
                   Maintenance = maintenance,
                   Income = value,
                   Profit = profit,
                   SummedProductivity = value,
                   AverageProductivity = value,
                   WorkforceAmounts = new Dictionary<int, int>(),
                   BuildingAmounts = new Dictionary<int, int>()
               };
    }

    /// <summary>
    /// Creates an island of a tick.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="name">The area name</param>
    /// <param name="entries">The entries, in frame order</param>
    /// <returns>The island</returns>
    private static PipeTickIsland CreateIsland(PipeIslandKey key, byte sessionId, string name, params ProductionEntry[] entries)
    {
        return new PipeTickIsland
               {
                   Key = key,
                   SessionId = sessionId,
                   Name = name,
                   Entries = entries
               };
    }

    /// <summary>
    /// Creates a tick received at <see cref="_received" />.
    /// </summary>
    /// <param name="gameTime">The game time</param>
    /// <param name="islands">The islands, in frame order</param>
    /// <returns>The tick</returns>
    private static PipeTick CreateTick(long gameTime, params PipeTickIsland[] islands)
    {
        return new PipeTick
               {
                   GameTime = gameTime,
                   ReceiveTime = _received,
                   Islands = islands,
                   CloseReason = PipeTickCloseReason.GameTimeChanged
               };
    }

    /// <summary>
    /// Creates a tick at the pipe's limits cheaply: 256 islands, each with 2,048 references to one shared entry.
    /// </summary>
    /// <returns>The tick</returns>
    private static PipeTick CreateTickAtLimits()
    {
        var entries = Enumerable.Repeat(CreateEntry(4242), PipeTickLimits.MaxEntriesAndPairs / PipeTickLimits.MaxIslands).ToArray();
        var islands = Enumerable.Range(0, PipeTickLimits.MaxIslands)
                                .Select(index => CreateIsland(new PipeIslandKey(1000 + index, (byte)index, 0), 1, "Island", entries))
                                .ToArray();

        return CreateTick(100, islands);
    }

    /// <summary>
    /// Gets the region, island and good cells of a row.
    /// </summary>
    /// <param name="row">The row</param>
    /// <returns>The cells</returns>
    private static string[] KeyAndGood(DiagnosticsRow row)
    {
        return [row.Cells[0], row.Cells[1], row.Cells[5]];
    }

    /// <summary>
    /// Gets the cells of every row of a view, joined by a bar per row.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The rows</returns>
    private static string[] RowTexts(DiagnosticsView view)
    {
        return [.. Enumerable.Range(0, view.Count).Select(index => string.Join("|", view[index].Cells))];
    }

    /// <summary>
    /// Gets the frames of the recordings and of the live run, one second apart from <see cref="_start" />: the version, a
    /// tick of one island at game time 400, and a tick of three islands at game time 500 that the end of the stream closes.
    /// </summary>
    /// <returns>The frames without their length prefix</returns>
    private static byte[][] Frames()
    {
        var negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        var first = new ProductionEntry
                    {
                        ProductGuid = 1010,
                        Generation = 0.1f,
                        Consumption = -2.5f,
                        Delta = 1E+38f,
                        PerfectGeneration = 0f,
                        PerfectConsumption = negativeZero,
                        BuildingCount = 12,
                        Maintenance = -300,
                        Income = 4.75f,
                        Profit = int.MinValue,
                        SummedProductivity = float.NaN,
                        AverageProductivity = float.PositiveInfinity,
                        WorkforceAmounts = new Dictionary<int, int>
                                           {
                                               [1] = 2
                                           },
                        BuildingAmounts = new Dictionary<int, int>
                                          {
                                              [3] = 4
                                          }
                    };
        var second = new ProductionEntry
                     {
                         ProductGuid = 2020,
                         Generation = 7f,
                         Consumption = 0.5f,
                         Delta = 6.5f,
                         PerfectGeneration = 8f,
                         PerfectConsumption = 1.5f,
                         BuildingCount = 3,
                         Maintenance = 40,
                         Income = -1.25f,
                         Profit = 99,
                         SummedProductivity = 2.5f,
                         AverageProductivity = float.NegativeInfinity,
                         WorkforceAmounts = new Dictionary<int, int>(),
                         BuildingAmounts = new Dictionary<int, int>()
                     };
        var third = new ProductionEntry
                    {
                        ProductGuid = 3030,
                        Generation = 1f,
                        Consumption = 2f,
                        Delta = 3f,
                        PerfectGeneration = 4f,
                        PerfectConsumption = 5f,
                        BuildingCount = 6,
                        Maintenance = 7,
                        Income = 8f,
                        Profit = 9,
                        SummedProductivity = 10f,
                        AverageProductivity = 11f,
                        WorkforceAmounts = new Dictionary<int, int>
                                           {
                                               [5] = 6
                                           },
                        BuildingAmounts = new Dictionary<int, int>()
                    };

        return [
                   PipeTestFrames.Version(2),
                   PipeTestFrames.Island(1, 400),
                   PipeTestFrames.Island(new PipeIslandKey(-77, 1, 0), 7, "Ostia", 500, first, second),
                   PipeTestFrames.Island(new PipeIslandKey(int.MaxValue, 2, 1), 7, "Neu Ostia", 500),
                   PipeTestFrames.Island(new PipeIslandKey(5, 3, 0), 8, "Capua", 500, third)
               ];
    }

    /// <summary>
    /// Gets the rows the last tick of <see cref="Frames" /> gives, written out from the values of its frames.
    /// </summary>
    /// <returns>The rows, cells joined by a bar</returns>
    private static string[] ExpectedRows()
    {
        return [
                   "-77|1|0|7|Ostia|1010|0.1|-2.5|1E+38|0|-0|12|-300|4.75|-2147483648|NaN|Infinity",
                   "-77|1|0|7|Ostia|2020|7|0.5|6.5|8|1.5|3|40|-1.25|99|2.5|-Infinity",
                   string.Join("|", ["2147483647", "2", "1", "7", "Neu Ostia", "(no entries)", .. Enumerable.Repeat(string.Empty, 11)]),
                   "5|3|0|8|Capua|3030|1|2|3|4|5|6|7|8|9|10|11"
               ];
    }

    /// <summary>
    /// Replays a recording of <see cref="Frames" /> to its end.
    /// </summary>
    /// <param name="speed">The speed</param>
    /// <returns>The runner after the replay</returns>
    private async Task<PipeReplayRunner> ReplayAsync(PipeReplaySpeed speed)
    {
        var frames = Frames();
        var recording = PipeTestFrames.Recording(_start, [.. frames.Select((frame, index) => (_start.AddSeconds(index), frame))]);
        var waits = new RecordingTimeProvider(_start);
        var runner = new PipeReplayRunner(() => new MemoryStream(recording), speed, waits);
        var run = runner.RunAsync(TestContext.CancellationToken);

        await waits.RunWaitsAsync(run, TestContext.CancellationToken);
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        return runner;
    }

    /// <summary>
    /// Feeds <see cref="Frames" /> to a live connection one second apart from <see cref="_start" />, ends the stream so
    /// the last tick closes, and stops the connection.
    /// </summary>
    /// <returns>The latest tick of the connection</returns>
    private async Task<PipeTick?> RunLiveAsync()
    {
        var clock = new RecordingTimeProvider(_start);
        var connector = new ScriptedPipeConnector(clock);
        var stream = new ControlledPipeStream();
        var connection = new PipeConnection(connector, clock);
        var ticks = new ConcurrentQueue<PipeTick>();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = Frames();

        connector.Enqueue(PipeConnectResult.FromStream(stream));
        connection.TickReceived += (_, tick) =>
                                   {
                                       ticks.Enqueue(tick);

                                       if (ticks.Count == 2)
                                       {
                                           closed.TrySetResult();
                                       }
                                   };

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var run = connection.RunAsync(stop.Token);

        try
        {
            await stream.WaitForPendingReadsAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

            for (var index = 0; index < frames.Length; index++)
            {
                clock.SetUtcNow(_start.AddSeconds(index));
                stream.Feed(PipeTestFrames.Wire(frames[index]));

                await stream.WaitForPendingReadsAsync(index + 2, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
            }

            stream.End();

            await closed.Task.WaitAsync(_guard, TestContext.CancellationToken);
        }
        finally
        {
            await stop.CancelAsync();
            await run.WaitAsync(_guard, TestContext.CancellationToken);
        }

        return connection.LatestTick;
    }

    #endregion // Methods
}