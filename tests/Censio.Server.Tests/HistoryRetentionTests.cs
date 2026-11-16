using System.Globalization;
using System.Text;

using Censio.Contracts;
using Censio.Pipe.Tests;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="HistoryRetention" />, over a real <see cref="ServerState" />, <see cref="ServerStore" /> and
/// <see cref="HistoryStore" /> on a database file in a temporary directory, with the clock of a
/// <see cref="FakeTimeProvider" />.
/// </summary>
[TestClass]
public sealed class HistoryRetentionTests
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// The island name of the ticks of the kept profiles.
    /// </summary>
    private const string IslandName = "Harbour island";

    /// <summary>
    /// The profile name only the deleted profile holds.
    /// </summary>
    private const string DeletedProfileName = "Deleted profile 93ac";

    /// <summary>
    /// The island name only the deleted profile holds.
    /// </summary>
    private const string DeletedIslandName = "Deleted island 2f6b";

    /// <summary>
    /// The warning of a failed fold step (spec E6).
    /// </summary>
    private const string ReduceFailedLine = "The history could not be reduced; the next run tries again.";

    /// <summary>
    /// The line of a deletion whose rows are gone (spec N3).
    /// </summary>
    private const string DeletedLine = "The history of a profile was deleted.";

    /// <summary>
    /// The line of a finished clean-up after a deletion (spec N3).
    /// </summary>
    private const string ClearedLine = "The deleted history was cleared from the database file.";

    /// <summary>
    /// The warning of a clean-up that could not finish (spec E10).
    /// </summary>
    private const string ClearBlockedLine = "The deleted history could not yet be cleared from the database file; the next retention run tries again.";

    /// <summary>
    /// The warning of a failed deletion (spec N3).
    /// </summary>
    private const string DeleteFailedLine = "The history of a profile could not be deleted.";

    /// <summary>
    /// The text of an entry without an exception, in the descriptions.
    /// </summary>
    private const string NoException = "<none>";

    /// <summary>
    /// The statement that makes every fold step fail: inserting a folded tick's identity raises an error.
    /// </summary>
    private const string CreateFailingFoldTriggerSql = "CREATE TRIGGER \"FailFoldedTicks\" BEFORE INSERT ON \"FoldedTicks\" BEGIN SELECT RAISE(ABORT, 'test fold failure'); END;";

    /// <summary>
    /// The statement that removes the trigger of <see cref="CreateFailingFoldTriggerSql" />.
    /// </summary>
    private const string DropFailingFoldTriggerSql = "DROP TRIGGER \"FailFoldedTicks\";";

    /// <summary>
    /// The statement that makes every fold step after the first fail, as a full disk would.
    /// </summary>
    private const string CreateSecondFoldFailingTriggerSql = "CREATE TRIGGER \"FailFoldedTicks\" BEFORE INSERT ON \"FoldedTicks\" WHEN (SELECT count(*) FROM \"FoldedTicks\") >= 1 BEGIN SELECT RAISE(ABORT, 'test fold failure'); END;";

    /// <summary>
    /// The statement that makes every sweep step removing a tick of a deleted profile fail, and nothing else.
    /// </summary>
    private const string CreateFailingSweepTriggerSql = "CREATE TRIGGER \"FailSweep\" BEFORE DELETE ON \"Ticks\" WHEN OLD.\"ProfileId\" IN (SELECT \"TombstoneId\" FROM \"DeletedProfiles\") BEGIN SELECT RAISE(ABORT, 'test sweep failure'); END;";

    /// <summary>
    /// The statement that removes the trigger of <see cref="CreateFailingSweepTriggerSql" />.
    /// </summary>
    private const string DropFailingSweepTriggerSql = "DROP TRIGGER \"FailSweep\";";

    /// <summary>
    /// The statement that makes every deletion of a profile fail before its rows are gone.
    /// </summary>
    private const string CreateFailingDeleteTriggerSql = "CREATE TRIGGER \"FailProfileDelete\" BEFORE DELETE ON \"Profiles\" BEGIN SELECT RAISE(ABORT, 'test delete failure'); END;";

    /// <summary>
    /// The statements that record, for each folded tick, the number of deleted profiles not yet swept at that moment.
    /// </summary>
    private const string CreateFoldLogSql = "CREATE TABLE \"FoldLog\" (\"Pending\" INTEGER NOT NULL);"
                                            + " CREATE TRIGGER \"LogFold\" AFTER INSERT ON \"FoldedTicks\" BEGIN INSERT INTO \"FoldLog\" (\"Pending\") VALUES ((SELECT count(*) FROM \"DeletedProfiles\")); END;";

    /// <summary>
    /// The statement that reads <c>FoldLog</c>, in the order of the folds.
    /// </summary>
    private const string ReadFoldLogSql = "SELECT coalesce(group_concat(\"Pending\", ' '), '') FROM (SELECT \"Pending\" FROM \"FoldLog\" ORDER BY rowid);";

    /// <summary>
    /// The statement that counts the rows of the tick and history tables: ticks, folded tick identities, hourly points and
    /// deleted profiles not yet swept.
    /// </summary>
    private const string CountRowsSql = "SELECT (SELECT count(*) FROM \"Ticks\") || ' ' || (SELECT count(*) FROM \"FoldedTicks\") || ' ' || (SELECT count(*) FROM \"HourlyPoints\") || ' ' || (SELECT count(*) FROM \"DeletedProfiles\");";

    /// <summary>
    /// The statement a held read runs in its open transaction, so it keeps the snapshot of that moment.
    /// </summary>
    private const string HoldReadSql = "BEGIN; SELECT count(*) FROM \"Ticks\";";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start of the hour <c>H</c> the old ticks of the tests lie in.
    /// </summary>
    private static readonly DateTimeOffset _hour = new(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A clock time at which the hours <c>H</c>, <c>H + 1 h</c> and <c>H + 2 h</c> are older than full resolution and the
    /// hour of the latest ticks (<c>H + 6 h</c>) is not.
    /// </summary>
    private static readonly DateTimeOffset _foldTime = _hour + TimeSpan.FromHours(3) + HistoryStore.FullResolution;

    /// <summary>
    /// The longest the test waits for a run, a deletion or a held read of the clock.
    /// </summary>
    private static readonly TimeSpan _waitBound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The time the test gives a disposal to return while the run it stops is held; a disposal that waits for the run
    /// passes whatever its length.
    /// </summary>
    private static readonly TimeSpan _heldWindow = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The row counts of <see cref="CountRowsSql" /> with the one old tick of a profile whole or folded.
    /// </summary>
    private static readonly string[] _wholeOrFoldedRows = ["2 0 0 0", "1 1 1 0"];

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    /// <summary>
    /// The logger of the retention job.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryRetentionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public HistoryRetentionTests(TestContext testContext)
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
    /// Disposes the opened databases and deletes the temporary directories of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _databases.Clear();
        SqliteConnection.ClearAllPools();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A run that folded ticks of two profiles logs the reduced line once, with the distinct hours of each profile and
    /// the ticks it folded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task HistoryRetentionRunFoldedTicksLogsReducedLine()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await StoreTicksAsync(state,
                              Tick(firstId, _hour.AddMinutes(10), IslandName),
                              Tick(firstId, _hour.AddMinutes(20), IslandName),
                              Tick(firstId, _hour.AddMinutes(70), IslandName),
                              LatestTick(firstId),
                              Tick(secondId, _hour.AddMinutes(30), IslandName),
                              LatestTick(secondId));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        // Act
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        Assert.AreSequenceEqual(new[] { Describe(LogLevel.Information, ReducedLine(3, 4), null) }, DescribeEntries(), "The run should log the reduced line once, with three profile hours from four ticks");
        Assert.AreEqual("2 4 3 0", CountRows(databaseFile), "Only the two latest ticks should be left unfolded");
    }

    /// <summary>
    /// A failing fold step logs the warning of spec E6 with its exception once and ends the run, leaving every tick
    /// whole; the next run, with the failure gone, folds the ticks of both profiles and logs the reduced line.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task HistoryRetentionRunFoldFailureLogsWarningAndNextRunContinues()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await StoreTicksAsync(state,
                              Tick(firstId, _hour.AddMinutes(10), IslandName),
                              Tick(firstId, _hour.AddMinutes(70), IslandName),
                              LatestTick(firstId),
                              Tick(secondId, _hour.AddMinutes(30), IslandName),
                              LatestTick(secondId));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        Execute(databaseFile, CreateFailingFoldTriggerSql);
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        var rowsAfterFailure = CountRows(databaseFile);

        Execute(databaseFile, DropFailingFoldTriggerSql);

        // Act
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           Describe(LogLevel.Warning, ReduceFailedLine, typeof(SqliteException)),
                           Describe(LogLevel.Information, ReducedLine(3, 3), null)
                       };

        Assert.AreEqual("5 0 0 0", rowsAfterFailure, "The failed run should leave every tick whole");
        Assert.AreSequenceEqual(expected, DescribeEntries(), "The failed run should log one warning with its exception and end, and the next run should fold and log the reduced line");
        Assert.AreEqual("2 3 3 0", CountRows(databaseFile), "The next run should fold every old tick of both profiles");
    }

    /// <summary>
    /// A fold step that fails after an earlier step of the run folded logs the warning of spec E6, then the reduced line
    /// of the folded tick; the clean-up a deletion asked for is done right after the run's opening sweep, before the
    /// folds, so the failure does not hold it back, and the next run folds the rest without clearing again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task HistoryRetentionRunFoldFailureAfterFoldLogsWarningThenReducedLine()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var keptId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        await StoreTicksAsync(state,
                              Tick(keptId, _hour.AddMinutes(10), IslandName),
                              Tick(keptId, _hour.AddMinutes(70), IslandName),
                              LatestTick(keptId),
                              Tick(deletedId, _hour.AddMinutes(20), DeletedIslandName));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken);
        Execute(databaseFile, CreateSecondFoldFailingTriggerSql);
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        var afterFailedRun = DescribeEntries();
        var rowsAfterFailedRun = CountRows(databaseFile);

        Execute(databaseFile, DropFailingFoldTriggerSql);

        // Act
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var failedRun = new[]
                        {
                            Describe(LogLevel.Information, DeletedLine, null),
                            Describe(LogLevel.Information, ClearedLine, null),
                            Describe(LogLevel.Warning, ReduceFailedLine, typeof(SqliteException)),
                            Describe(LogLevel.Information, ReducedLine(1, 1), null)
                        };

        Assert.AreSequenceEqual(failedRun, afterFailedRun, "The failed run should clear after its sweep, then log the warning and the reduced line of its one fold");
        Assert.AreEqual("2 1 1 0", rowsAfterFailedRun, "The failed run should have folded one tick and swept the deleted profile");
        Assert.AreSequenceEqual(failedRun.Append(Describe(LogLevel.Information, ReducedLine(1, 1), null)).ToList(), DescribeEntries(), "The next run should fold the rest without clearing again");
        Assert.AreEqual("1 2 2 0", CountRows(databaseFile), "The next run should fold the remaining old tick");
    }

    /// <summary>
    /// A fold that keeps failing does not hold back the clean-up a deletion asked for: the run sweeps the deleted
    /// profile, clears and logs the cleared line before its failing fold, and the next run, failing again, does not
    /// clear again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task HistoryRetentionRunFailingFoldStillClearsDeletedHistory()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var keptId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        await state.StoreProfileAsync(deletedId, DeletedProfileName, TestContext.CancellationToken);
        await StoreTicksAsync(state,
                              Tick(keptId, _hour.AddMinutes(10), IslandName),
                              LatestTick(keptId),
                              Tick(deletedId, _hour.AddMinutes(20), DeletedIslandName));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken);
        Execute(databaseFile, CreateFailingFoldTriggerSql);
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Act
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           Describe(LogLevel.Information, DeletedLine, null),
                           Describe(LogLevel.Information, ClearedLine, null),
                           Describe(LogLevel.Warning, ReduceFailedLine, typeof(SqliteException)),
                           Describe(LogLevel.Warning, ReduceFailedLine, typeof(SqliteException))
                       };

        Assert.AreSequenceEqual(expected, DescribeEntries(), "The clean-up should be done after the sweep although every fold fails, and only once");
        Assert.AreEqual("2 0 0 0", CountRows(databaseFile), "The deleted profile should be swept and the kept ticks left whole");
        Assert.IsEmpty(FindNamesInFiles(databaseFile, DeletedProfileName, DeletedIslandName), "No name only the deleted profile held should be left in the database files");
    }

    /// <summary>
    /// A sweep step that fails at the start of a run logs the clean-up warning of spec E10 with its exception and ends
    /// the run before any fold; the next run, with the failure gone, sweeps, clears and folds.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [TestCategory("AC8")]
    public async Task HistoryRetentionRunSweepFailureLogsWarningAndEndsRun()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var keptId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        await StoreTicksAsync(state,
                              Tick(keptId, _hour.AddMinutes(10), IslandName),
                              LatestTick(keptId),
                              Tick(deletedId, _hour.AddMinutes(20), DeletedIslandName));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken);
        Execute(databaseFile, CreateFailingSweepTriggerSql);
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        var afterFailedRun = DescribeEntries();
        var rowsAfterFailedRun = CountRows(databaseFile);

        Execute(databaseFile, DropFailingSweepTriggerSql);

        // Act
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var failedRun = new[]
                        {
                            Describe(LogLevel.Information, DeletedLine, null),
                            Describe(LogLevel.Warning, ClearBlockedLine, typeof(SqliteException))
                        };

        Assert.AreSequenceEqual(failedRun, afterFailedRun, "The failed sweep should log the clean-up warning with its exception and end the run");
        Assert.AreEqual("3 0 0 1", rowsAfterFailedRun, "The failed run should fold nothing and leave the deleted profile unswept");
        Assert.AreSequenceEqual(failedRun.Concat([Describe(LogLevel.Information, ClearedLine, null), Describe(LogLevel.Information, ReducedLine(1, 1), null)]).ToList(), DescribeEntries(), "The next run should sweep, clear right after the sweep, then fold");
        Assert.AreEqual("1 1 1 0", CountRows(databaseFile), "The next run should remove the deleted profile's rows and fold the kept old tick");
    }

    /// <summary>
    /// A deletion asked while a run is under way whose sweep step then fails, before the run's next fold step, logs the
    /// clean-up warning of spec E10 with its exception and ends the run without folding.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [TestCategory("AC8")]
    public async Task HistoryRetentionDeleteProfileDuringFoldsSweepFailureEndsRun()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var keptId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        using var clock = new BlockingClockTimeProvider(_foldTime);

        await StoreTicksAsync(state,
                              Tick(keptId, _hour.AddMinutes(10), IslandName),
                              LatestTick(keptId),
                              Tick(deletedId, _hour.AddMinutes(20), DeletedIslandName));
        Execute(databaseFile, CreateFailingSweepTriggerSql);

        using var retention = new HistoryRetention(state, history, clock, _logger);

        var run = Task.Run(() => retention.RunAsync(TestContext.CancellationToken), TestContext.CancellationToken);

        await Task.WhenAny(clock.Reached, run).WaitAsync(_waitBound, TestContext.CancellationToken);
        await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Act
        clock.Release();

        await run.WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           Describe(LogLevel.Information, DeletedLine, null),
                           Describe(LogLevel.Warning, ClearBlockedLine, typeof(SqliteException))
                       };

        Assert.AreSequenceEqual(expected, DescribeEntries(), "The failed sweep during the folds should log the clean-up warning with its exception and no reduced line");
        Assert.AreEqual("3 0 0 1", CountRows(databaseFile), "The run should end before folding and leave the deleted profile unswept");
    }

    /// <summary>
    /// A started job runs at once, then waits one hour after the end of each run before the next one: the first run
    /// folds the hour old enough at the start, the second the hour that became old enough during the wait.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task HistoryRetentionStartRunsAfterStartAndEveryHour()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();
        var clock = new RecordingTimeProvider(_hour + TimeSpan.FromHours(2) + HistoryStore.FullResolution - TimeSpan.FromTicks(1));

        await StoreTicksAsync(state,
                              Tick(profileId, _hour.AddMinutes(10), IslandName),
                              Tick(profileId, _hour.AddMinutes(70), IslandName),
                              LatestTick(profileId));

        using var retention = new HistoryRetention(state, history, clock, _logger);

        // Act
        retention.Start(TestContext.CancellationToken);

        await clock.WaitForDelayAsync(HistoryRetention.RunInterval, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        var afterFirstRun = DescribeEntries();
        var rowsAfterFirstRun = CountRows(databaseFile);

        clock.Advance(HistoryRetention.RunInterval);

        await clock.WaitForTimersAsync(2, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var reduced = Describe(LogLevel.Information, ReducedLine(1, 1), null);

        Assert.AreSequenceEqual(new[] { reduced }, afterFirstRun, "The run at the start should fold the one hour old enough and log it");
        Assert.AreEqual("2 1 1 0", rowsAfterFirstRun, "The run at the start should fold only the hour old enough at the start");
        Assert.AreSequenceEqual(new[] { reduced, reduced }, DescribeEntries(), "The run an hour later should fold the hour that became old enough");
        Assert.AreEqual("1 2 2 0", CountRows(databaseFile), "After the second run only the latest tick should be unfolded");
        Assert.AreSequenceEqual(new[] { HistoryRetention.RunInterval, HistoryRetention.RunInterval }, clock.Delays, "After each run the job should wait one hour");
    }

    /// <summary>
    /// Disposing the job while a run is held at its read of the clock does not return before the run has ended, and the
    /// stopped run logs nothing and leaves every tick whole or folded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task HistoryRetentionStopDuringRunLogsNoWarningAndDisposeWaits()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();

        using var clock = new BlockingClockTimeProvider(_foldTime);

        await StoreTicksAsync(state,
                              Tick(profileId, _hour.AddMinutes(10), IslandName),
                              LatestTick(profileId));

        using var retention = new HistoryRetention(state, history, clock, _logger);

        retention.Start(TestContext.CancellationToken);

        await clock.Reached.WaitAsync(_waitBound, TestContext.CancellationToken);

        // Act
        var disposing = Task.Run(retention.Dispose, TestContext.CancellationToken);
        var first = await Task.WhenAny(disposing, Task.Delay(_heldWindow, TestContext.CancellationToken));

        clock.Release();

        await disposing.WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        Assert.AreNotSame(disposing, first, "The disposal should not return while the run is held");
        Assert.IsTrue(clock.HasReturned, "The disposal should return only after the held run went on and ended");
        Assert.IsEmpty(_logger.Entries, "A run cut off by the stop should log nothing");
        Assert.Contains(CountRows(databaseFile), _wholeOrFoldedRows, "Every tick should be whole or folded");
    }

    /// <summary>
    /// A deletion asked while a run is under way, after its sweep steps, is swept before the run's next fold step: no
    /// tick is folded while a deleted profile is left unswept, and the run still folds every old tick of the kept
    /// profile.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task HistoryRetentionDeleteProfileDuringFoldsSweepsBeforeNextFoldStep()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var keptId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        using var clock = new BlockingClockTimeProvider(_foldTime);

        await StoreTicksAsync(state,
                              Tick(keptId, _hour.AddMinutes(10), IslandName),
                              Tick(keptId, _hour.AddMinutes(70), IslandName),
                              Tick(keptId, _hour.AddMinutes(130), IslandName),
                              LatestTick(keptId),
                              Tick(deletedId, _hour.AddMinutes(20), DeletedIslandName),
                              LatestTick(deletedId));
        Execute(databaseFile, CreateFoldLogSql);

        using var retention = new HistoryRetention(state, history, clock, _logger);

        var run = Task.Run(() => retention.RunAsync(TestContext.CancellationToken), TestContext.CancellationToken);

        await Task.WhenAny(clock.Reached, run).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Act
        var outcome = await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        clock.Release();

        await run.WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Done, outcome, "The deletion should be done");
        Assert.AreEqual("0 0 0", ReadFoldLog(databaseFile), "Each of the three folds of the kept profile should find no deleted profile left to sweep");
        Assert.AreEqual("1 3 3 0", CountRows(databaseFile), "The deleted profile's rows should be gone and the kept profile's old ticks folded");
    }

    /// <summary>
    /// A clean-up that a held read blocks logs the warning of spec E10 without an exception; once the read has ended the
    /// next run clears the deleted history, logs the cleared line, and no name only the deleted profile held is left in
    /// the database files.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task HistoryRetentionRunBlockedClearLogsWarningAndNextRunClears()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var deletedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();

        await state.StoreProfileAsync(deletedId, DeletedProfileName, TestContext.CancellationToken);
        await StoreTicksAsync(state,
                              Tick(deletedId, _hour.AddHours(6), DeletedIslandName),
                              LatestTick(keptId));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        using (var reader = OpenRawConnection(databaseFile))
        {
            using var hold = reader.CreateCommand();

            hold.CommandText = HoldReadSql;
            await hold.ExecuteScalarAsync(TestContext.CancellationToken);

            await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken);
            await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);
        }

        // Act
        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           Describe(LogLevel.Information, DeletedLine, null),
                           Describe(LogLevel.Warning, ClearBlockedLine, null),
                           Describe(LogLevel.Information, ClearedLine, null)
                       };

        Assert.AreSequenceEqual(expected, DescribeEntries(), "The blocked clean-up should log the warning and the next run the cleared line");
        Assert.IsEmpty(FindNamesInFiles(databaseFile, DeletedProfileName, DeletedIslandName), "No name only the deleted profile held should be left in the database files");
    }

    /// <summary>
    /// A deletion and the run that clears it log the deleted and the cleared line, without a name, an identity or an
    /// exception.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task HistoryRetentionDeleteProfileLogsLinesWithoutName()
    {
        // Arrange
        var (state, history, _) = await OpenAsync();

        var deletedId = Guid.NewGuid();

        await state.StoreProfileAsync(deletedId, DeletedProfileName, TestContext.CancellationToken);
        await StoreTicksAsync(state, Tick(deletedId, _hour.AddHours(6), DeletedIslandName));

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        // Act
        var outcome = await retention.DeleteProfileAsync(deletedId, TestContext.CancellationToken);

        await retention.RunAsync(TestContext.CancellationToken).WaitAsync(_waitBound, TestContext.CancellationToken);

        // Assert
        var expected = new[]
                       {
                           Describe(LogLevel.Information, DeletedLine, null),
                           Describe(LogLevel.Information, ClearedLine, null)
                       };

        Assert.AreEqual(HistoryOutcome.Done, outcome, "The deletion should be done");
        Assert.AreSequenceEqual(expected, DescribeEntries(), "The deletion and its clean-up should log their lines, without a name");
        Assert.DoesNotContain(deletedId, state.Profiles.Select(profile => profile.Id).ToList(), "The deleted profile should no longer be listed");
    }

    /// <summary>
    /// A deletion that fails before its rows are gone logs the warning of spec N3 with its exception and no name, reports
    /// failed and leaves the profile listed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task HistoryRetentionDeleteProfileFailureLogsWarning()
    {
        // Arrange
        var (state, history, databaseFile) = await OpenAsync();

        var profileId = Guid.NewGuid();

        await state.StoreProfileAsync(profileId, DeletedProfileName, TestContext.CancellationToken);
        await StoreTicksAsync(state, Tick(profileId, _hour.AddHours(6), DeletedIslandName));
        Execute(databaseFile, CreateFailingDeleteTriggerSql);

        using var retention = new HistoryRetention(state, history, new FakeTimeProvider(_foldTime), _logger);

        // Act
        var outcome = await retention.DeleteProfileAsync(profileId, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(HistoryOutcome.Failed, outcome, "The failed deletion should be reported as failed");
        Assert.AreSequenceEqual(new[] { Describe(LogLevel.Warning, DeleteFailedLine, typeof(SqliteException)) }, DescribeEntries(), "The failed deletion should log the warning with its exception and no name");
        Assert.Contains(profileId, state.Profiles.Select(profile => profile.Id).ToList(), "The profile should stay listed");
    }

    /// <summary>
    /// Gives the reduced line of a run (spec N3).
    /// </summary>
    /// <param name="hours">The hours</param>
    /// <param name="ticks">The ticks</param>
    /// <returns>The line</returns>
    private static string ReducedLine(int hours, int ticks)
    {
        return $"History reduced to hourly averages: {hours.ToString(CultureInfo.InvariantCulture)} hours from {ticks.ToString(CultureInfo.InvariantCulture)} ticks.";
    }

    /// <summary>
    /// Describes a log entry as text: its level, its message and the type of its exception.
    /// </summary>
    /// <param name="level">The level</param>
    /// <param name="message">The message</param>
    /// <param name="exceptionType">The type of the exception, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string Describe(LogLevel level, string message, Type? exceptionType)
    {
        return $"{level} {message} {exceptionType?.Name ?? NoException}";
    }

    /// <summary>
    /// Creates a tick of a new identity with one island of one good.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveTime">The receive time</param>
    /// <param name="islandName">The island name</param>
    /// <returns>The tick</returns>
    private static ContractTick Tick(Guid profileId, DateTimeOffset receiveTime, string islandName)
    {
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = receiveTime,
                   GameTime = 3_600_000 + ((receiveTime - _hour).Ticks / TimeSpan.TicksPerSecond),
                   Islands = [
                                 new ContractTickIsland
                                 {
                                     SessionGuid = 1,
                                     IslandId = 1,
                                     AreaIndex = 0,
                                     SessionId = 1,
                                     Name = islandName,
                                     Entries = [
                                                   new ContractTickEntry
                                                   {
                                                       ProductGuid = 1010,
                                                       Generation = 1.5f,
                                                       Consumption = 2.5f,
                                                       Delta = -1f,
                                                       PerfectGeneration = 1.75f,
                                                       PerfectConsumption = 2.25f,
                                                       BuildingCount = 3,
                                                       Maintenance = 40,
                                                       Income = 12.5f,
                                                       Profit = -27,
                                                       SummedProductivity = 2.5f,
                                                       AverageProductivity = 0.75f,
                                                       WorkforceAmounts = new Dictionary<int, int>
                                                                          {
                                                                              [100] = 7
                                                                          },
                                                       BuildingAmounts = new Dictionary<int, int>
                                                                         {
                                                                             [200] = 3
                                                                         }
                                                   }
                                               ]
                                 }
                             ]
               };
    }

    /// <summary>
    /// Creates the latest tick of a profile, in the hour <c>H + 6 h</c>, which is never folded.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The tick</returns>
    private static ContractTick LatestTick(Guid profileId)
    {
        return Tick(profileId, _hour.AddHours(6).AddMinutes(30), IslandName);
    }

    /// <summary>
    /// Opens a connection of its own, without the pool, to a SQLite file.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The opened connection</returns>
    private static SqliteConnection OpenRawConnection(string databaseFile)
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };
        var connection = new SqliteConnection(builder.ToString());

        connection.Open();

        return connection;
    }

    /// <summary>
    /// Runs a constant statement of this class over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="sql">The statement</param>
    private static void Execute(string databaseFile, string sql)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only
        command.CommandText = sql;
#pragma warning restore CA2100

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Runs a constant query of this class over a connection of its own and gives its text.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="sql">The query</param>
    /// <returns>The text of the query's first value</returns>
    private static string Query(string databaseFile, string sql)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only
        command.CommandText = sql;
#pragma warning restore CA2100

        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Counts the rows of <see cref="CountRowsSql" />.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The counts, separated by spaces</returns>
    private static string CountRows(string databaseFile)
    {
        return Query(databaseFile, CountRowsSql);
    }

    /// <summary>
    /// Reads the numbers of deleted profiles left unswept at each fold, from <c>FoldLog</c>.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The numbers in the order of the folds, separated by spaces</returns>
    private static string ReadFoldLog(string databaseFile)
    {
        return Query(databaseFile, ReadFoldLogSql);
    }

    /// <summary>
    /// Reads the bytes of a file that SQLite holds open, or none when it does not exist.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The bytes</returns>
    private static byte[] ReadSharedBytes(string path)
    {
        if (File.Exists(path) == false)
        {
            return [];
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();

        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>
    /// Finds the names whose UTF-8 bytes occur in the database file or its <c>-wal</c> or <c>-shm</c> file.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <param name="names">The names</param>
    /// <returns>The names found, in the given order</returns>
    private static List<string> FindNamesInFiles(string databaseFile, params string[] names)
    {
        var files = new[] { databaseFile, databaseFile + "-wal", databaseFile + "-shm" }.Select(ReadSharedBytes).ToList();

        return names.Where(name => files.Any(bytes => bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(name)) >= 0)).ToList();
    }

    /// <summary>
    /// Describes the entries the job logged so far, in their order.
    /// </summary>
    /// <returns>The lines</returns>
    private List<string> DescribeEntries()
    {
        return _logger.Entries.Select(entry => Describe(entry.Level, entry.Message, entry.Exception?.GetType())).ToList();
    }

    /// <summary>
    /// Stores ticks in order through the state and fails on a tick not stored.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="ticks">The ticks</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task StoreTicksAsync(ServerState state, params ContractTick[] ticks)
    {
        foreach (var tick in ticks)
        {
            var result = await state.StoreTickAsync(tick, TestContext.CancellationToken);

            if (result.Outcome != ServerStoreOutcome.Stored)
            {
                throw new InvalidOperationException($"The test tick was not stored: {result.Outcome}", result.Failure);
            }
        }
    }

    /// <summary>
    /// Opens a database file in a new temporary directory, a restored state over a store on it and a history store, as
    /// the server does at start; the state logs to a logger of its own, so the job's logger holds only the job's lines.
    /// </summary>
    /// <returns>The state, the history store and the database file</returns>
    private async Task<(ServerState State, HistoryStore History, string DatabaseFile)> OpenAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-history-retention-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        var databaseFile = Path.Combine(directory, DatabaseFileName);
        var result = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        var state = new ServerState(new ServerStore(database), new ConcurrentRecordingLogger());

        await state.RestoreAsync(TestContext.CancellationToken);

        return (state, new HistoryStore(database), databaseFile);
    }

    #endregion // Methods
}