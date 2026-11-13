using System.Runtime.Versioning;
using System.Text;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Censio.Server.Data.Tests;

/// <summary>
/// Tests for <see cref="ServerDatabase" />, against real SQLite files in a temporary directory.
/// </summary>
[TestClass]
public sealed class ServerDatabaseTests : IDisposable
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// The name of the profile a test stores before the start, to check that the start keeps it.
    /// </summary>
    private const string KeptProfileName = "Kept profile";

    /// <summary>
    /// The identity of the profile a test stores before the start, as EF Core stores a GUID.
    /// </summary>
    private const string KeptProfileId = "6F9619FF-8B86-D011-B42D-00C04FC964FF";

    /// <summary>
    /// An applied migration identifier no build of this version knows.
    /// </summary>
    private const string NewerMigrationId = "29991231000000_FromNewerVersion";

    /// <summary>
    /// The statement EF Core creates its lock table with in SQLite.
    /// </summary>
    private const string CreateLockTableSql = "CREATE TABLE \"__EFMigrationsLock\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK___EFMigrationsLock\" PRIMARY KEY, \"Timestamp\" TEXT NOT NULL);";

    /// <summary>
    /// The statement EF Core creates its history table with in SQLite.
    /// </summary>
    private const string CreateHistoryTableSql = "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";

    /// <summary>
    /// The statement that leaves a lock row behind, as an EF Core update stopped while it held its lock does.
    /// </summary>
    private const string InsertLockRowSql = "INSERT INTO \"__EFMigrationsLock\" (\"Id\", \"Timestamp\") VALUES (1, '2026-11-13 12:00:00+00:00');";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The longest an open may take, also on a file a stopped update left its lock row in.
    /// </summary>
    private static readonly TimeSpan _openBound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The tables of the current schema, sorted ordinally.
    /// </summary>
    private static readonly string[] _currentTables = ["Catalog", "Icons", "Islands", "Profiles", "TickEntries", "TickIslands", "TickMapPairs", "Ticks"];

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The folders the running test made read-only, made writable again before they are deleted.
    /// </summary>
    private readonly List<string> _readOnlyFolders = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    /// <summary>
    /// The provider that records what the database logs.
    /// </summary>
    private readonly RecordingLoggerProvider _loggerProvider = new();

    /// <summary>
    /// The logger factory handed to the database.
    /// </summary>
    private readonly LoggerFactory _loggerFactory;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDatabaseTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ServerDatabaseTests(TestContext testContext)
    {
        TestContext = testContext;
        _loggerFactory = new LoggerFactory([_loggerProvider]);
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

        SqliteConnection.ClearAllPools();
        MakeFoldersWritable(_readOnlyFolders);

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A start without a database file creates it with every migration of the build applied and the tables of the
    /// current schema.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ServerDatabaseOpenMissingFileCreatesCurrentSchema()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Created, result.Kind, "A missing file should be reported as created");
        Assert.AreEqual(databaseFile, result.DatabaseFile, "The result should name the database file");
        Assert.IsNotNull(result.Database, "A created database should be handed out");
        Assert.AreEqual(databaseFile, result.Database.DatabaseFile, "The database should name its file");
        Assert.IsNull(result.Failure, "A created database carries no failure");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.ToList(), ReadHistory(databaseFile), "Every migration of the build should be recorded as applied");
        Assert.AreSequenceEqual(_currentTables, ReadUserTables(databaseFile), "The file should hold the tables of the current schema");
    }

    /// <summary>
    /// A start on a database at the current schema opens it, keeps its data and applies nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task ServerDatabaseOpenCurrentSchemaKeepsDataAndReportsOpened()
    {
        // Arrange
        var databaseFile = await CreateCurrentSchemaDatabaseAsync();
        var schemaBefore = ReadSchema(databaseFile);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Opened, result.Kind, "A database at the current schema should be reported as opened");
        Assert.IsNotNull(result.Database, "An opened database should be handed out");
        Assert.AreSequenceEqual(schemaBefore, ReadSchema(databaseFile), "Opening should not change the schema");
        Assert.AreSequenceEqual(new[] { KeptProfileName }, ReadProfileNames(databaseFile), "The stored profile should be kept");
    }

    /// <summary>
    /// A start on a database of an earlier schema holding data applies the later step, records it and keeps the data.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task ServerDatabaseOpenEarlierSchemaWithDataUpdatesAndKeepsData()
    {
        // Arrange
        var databaseFile = await CreateCurrentSchemaDatabaseAsync();

        // Act
        var result = await OpenWithTestMigrationsAsync(databaseFile, typeof(AddedTableTestMigration));

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Updated, result.Kind, "A database of an earlier schema should be reported as updated");
        Assert.IsNotNull(result.Database, "An updated database should be handed out");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.Append(AddedTableTestMigration.Id).ToList(), ReadHistory(databaseFile), "Each version applied should be recorded");
        Assert.Contains(AddedTableTestMigration.TableName, ReadUserTables(databaseFile), "The later step should have been applied");
        Assert.AreSequenceEqual(new[] { KeptProfileName }, ReadProfileNames(databaseFile), "The stored profile should be kept across the update");
    }

    /// <summary>
    /// A start on an empty file brings it to the current schema and reports it as updated.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task ServerDatabaseOpenEmptyFileReportsUpdated()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        WriteBytes(databaseFile, []);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Updated, result.Kind, "An empty file should be reported as updated");
        Assert.IsNotNull(result.Database, "An updated database should be handed out");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.ToList(), ReadHistory(databaseFile), "Every migration of the build should be recorded as applied");
        Assert.AreSequenceEqual(_currentTables, ReadUserTables(databaseFile), "The file should hold the tables of the current schema");
    }

    /// <summary>
    /// A file holding only EF Core's two bookkeeping tables, a lock row and no applied migration (as EF Core's own update
    /// or its tools leave it, never the server) is updated as a fresh file within the bound, not left waiting for the
    /// lock.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenBookkeepingTablesOnlyFileUpdatesWithinBound()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        Execute(databaseFile, command => command.CommandText = CreateLockTableSql + CreateHistoryTableSql + InsertLockRowSql);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Updated, result.Kind, "A file with only the bookkeeping tables should be updated within the bound");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.ToList(), ReadHistory(databaseFile), "Every migration of the build should be recorded as applied");
        Assert.AreSequenceEqual(_currentTables, ReadUserTables(databaseFile), "The file should hold the tables of the current schema");
    }

    /// <summary>
    /// A file holding EF Core's two bookkeeping tables and anything else (here a view) is not taken for a fresh file:
    /// it is refused as not a Censio database and left unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenBookkeepingTablesWithViewReportsNotADatabaseAndKeepsFile()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        Execute(databaseFile, command => command.CommandText = CreateLockTableSql + CreateHistoryTableSql + "CREATE VIEW \"ExtraView\" AS SELECT 1 AS \"Value\";");

        var bytesBefore = await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotADatabase, result.Kind, "Bookkeeping tables beside a view should be refused as not a Censio database");
        Assert.IsNull(result.Database, "A refused file should not be handed out");
        Assert.AreSequenceEqual(bytesBefore, await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken), "A refused file should be left unchanged");
    }

    /// <summary>
    /// A database file whose folder path runs through a regular file cannot be created.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenFolderBelowFileReportsNotWritable()
    {
        // Arrange
        var blocker = Path.Combine(CreateTemporaryDirectory(), "blocker");
        var blockerBytes = Encoding.UTF8.GetBytes("a file where the database folder should be");

        WriteBytes(blocker, blockerBytes);

        var databaseFile = Path.Combine(blocker, DatabaseFileName);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotWritable, result.Kind, "A folder that cannot be created should be reported as not writable");
        Assert.AreEqual(databaseFile, result.DatabaseFile, "The result should name the database file");
        Assert.IsNull(result.Database, "No database should be handed out");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.AreSequenceEqual(blockerBytes, await File.ReadAllBytesAsync(blocker, TestContext.CancellationToken), "The file in the way should be left unchanged");
    }

    /// <summary>
    /// A database at the current schema in a read-only file is refused as not writable, though every read would pass,
    /// and stays unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [OSCondition(OperatingSystems.Linux)]
    [SupportedOSPlatform("linux")]
    public async Task ServerDatabaseOpenReadOnlyFileReportsNotWritable()
    {
        // Arrange
        SkipWhenPrivileged();

        var databaseFile = await CreateCurrentSchemaDatabaseAsync();

        File.SetUnixFileMode(databaseFile, UnixFileMode.UserRead);

        var bytesBefore = await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotWritable, result.Kind, "A read-only file should be reported as not writable");
        Assert.IsNull(result.Database, "No database should be handed out");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.AreSequenceEqual(bytesBefore, await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken), "The read-only file should be left unchanged");
    }

    /// <summary>
    /// An empty database file in a read-only folder is refused as not writable, and the folder stays as it was.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [OSCondition(OperatingSystems.Linux)]
    [SupportedOSPlatform("linux")]
    public async Task ServerDatabaseOpenFileInReadOnlyFolderReportsNotWritable()
    {
        // Arrange
        SkipWhenPrivileged();

        var folder = CreateTemporaryDirectory();
        var databaseFile = Path.Combine(folder, DatabaseFileName);

        WriteBytes(databaseFile, []);
        MakeFolderReadOnly(folder);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotWritable, result.Kind, "A file in a read-only folder should be reported as not writable");
        Assert.IsNull(result.Database, "No database should be handed out");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.AreEqual(0L, new FileInfo(databaseFile).Length, "The empty file should stay empty");
        Assert.AreSequenceEqual(new[] { databaseFile }, Directory.GetFiles(folder), "No other file should appear in the folder");
    }

    /// <summary>
    /// A text file is refused as not a Censio database and left unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenTextFileReportsNotADatabaseAndKeepsFile()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);
        var text = Encoding.UTF8.GetBytes("This is a plain text file and not a database.\nIt has a few lines,\nso that it is longer than the database header of one hundred bytes.\n");

        WriteBytes(databaseFile, text);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotADatabase, result.Kind, "A text file should be refused as not a Censio database");
        Assert.IsNull(result.Database, "A refused file should not be handed out");
        Assert.IsInstanceOfType<SqliteException>(result.Failure, "The SQLite error behind the refusal should be attached");
        Assert.AreSequenceEqual(text, await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken), "A refused file should be left unchanged");
    }

    /// <summary>
    /// A SQLite database whose header is damaged is refused as not a Censio database and left unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenDamagedHeaderReportsNotADatabaseAndKeepsFile()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        Execute(databaseFile, command => command.CommandText = "CREATE TABLE \"Damaged\" (\"Value\" INTEGER);");

        var damaged = await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken);

        damaged[0] = (byte)'X';
        WriteBytes(databaseFile, damaged);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotADatabase, result.Kind, "A damaged header should be refused as not a Censio database");
        Assert.IsNull(result.Database, "A refused file should not be handed out");
        Assert.IsInstanceOfType<SqliteException>(result.Failure, "The SQLite error behind the refusal should be attached");
        Assert.AreSequenceEqual(damaged, await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken), "A refused file should be left unchanged");
    }

    /// <summary>
    /// A SQLite database Censio did not write (tables, but no record of Censio's schema versions) is refused as not a
    /// Censio database and left unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenForeignSqliteFileReportsNotADatabaseAndKeepsFile()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        Execute(databaseFile, command => command.CommandText = "CREATE TABLE \"Foreign\" (\"Value\" INTEGER); INSERT INTO \"Foreign\" (\"Value\") VALUES (42);");

        var bytesBefore = await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NotADatabase, result.Kind, "A foreign SQLite file should be refused as not a Censio database");
        Assert.IsNull(result.Database, "A refused file should not be handed out");
        Assert.AreSequenceEqual(bytesBefore, await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken), "A refused file should be left unchanged");
    }

    /// <summary>
    /// A database that records a schema version this build does not know is refused as written by a newer version and
    /// left unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenUnknownAppliedMigrationReportsNewerSchemaAndKeepsFile()
    {
        // Arrange
        var databaseFile = await CreateCurrentSchemaDatabaseAsync();

        Execute(databaseFile, command => command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('" + NewerMigrationId + "', '99.0.0');");

        var bytesBefore = await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.NewerSchema, result.Kind, "An unknown applied migration should be reported as a newer schema");
        Assert.IsNull(result.Database, "A refused file should not be handed out");
        Assert.AreSequenceEqual(bytesBefore, await File.ReadAllBytesAsync(databaseFile, TestContext.CancellationToken), "A refused file should be left unchanged");
    }

    /// <summary>
    /// A two-step update whose second step fails leaves the schema, the recorded versions and the data of before the
    /// start, also the first step that had succeeded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenFailingSecondStepReportsUpdateFailedAndKeepsFile()
    {
        // Arrange
        var databaseFile = await CreateCurrentSchemaDatabaseAsync();
        var schemaBefore = ReadSchema(databaseFile);

        // Act
        var result = await OpenWithTestMigrationsAsync(databaseFile, typeof(AddedTableTestMigration), typeof(FailingTestMigration));

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.UpdateFailed, result.Kind, "A failing step should be reported as a failed update");
        Assert.IsNull(result.Database, "No database should be handed out after a failed update");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.AreSequenceEqual(schemaBefore, ReadSchema(databaseFile), "The schema of before the start should be kept, without the first step's table");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.ToList(), ReadHistory(databaseFile), "No step of the failed update should be recorded");
        Assert.AreSequenceEqual(new[] { KeptProfileName }, ReadProfileNames(databaseFile), "The stored profile should be kept");
    }

    /// <summary>
    /// An update with a step that EF Core would run outside the migration transaction is refused before anything runs.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenSuppressedTransactionReportsUpdateFailedAndKeepsFile()
    {
        // Arrange
        var databaseFile = await CreateCurrentSchemaDatabaseAsync();
        var schemaBefore = ReadSchema(databaseFile);

        // Act
        var result = await OpenWithTestMigrationsAsync(databaseFile, typeof(SuppressedTransactionTestMigration));

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.UpdateFailed, result.Kind, "A step outside the transaction should be refused as a failed update");
        Assert.IsNull(result.Database, "No database should be handed out after a refused update");
        Assert.AreSequenceEqual(schemaBefore, ReadSchema(databaseFile), "Nothing of the refused step should have run");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.ToList(), ReadHistory(databaseFile), "No step of the refused update should be recorded");
        Assert.AreSequenceEqual(new[] { KeptProfileName }, ReadProfileNames(databaseFile), "The stored profile should be kept");
    }

    /// <summary>
    /// After a failed first update of a new file, the next start brings the file to the current schema.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenAfterUpdateFailedOpensCurrentSchema()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        await OpenWithTestMigrationsAsync(databaseFile, typeof(FailingTestMigration));

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Updated, result.Kind, "The file a failed first update left should be updated by the next start");
        Assert.IsNotNull(result.Database, "The updated database should be handed out");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.ToList(), ReadHistory(databaseFile), "Every migration of the build should be recorded as applied");
        Assert.AreSequenceEqual(_currentTables, ReadUserTables(databaseFile), "The file should hold the tables of the current schema");
    }

    /// <summary>
    /// A new file whose first update fails in one of its steps is reported as a failed update and holds no table,
    /// index or other schema entry afterwards, not even EF Core's bookkeeping tables.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenNewFileWithFailingStepReportsUpdateFailedAndLeavesNoTable()
    {
        // Arrange
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);

        // Act
        var result = await OpenWithTestMigrationsAsync(databaseFile, typeof(FailingTestMigration));

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.UpdateFailed, result.Kind, "A failing step of the first update should be reported as a failed update");
        Assert.AreEqual(databaseFile, result.DatabaseFile, "The result should name the database file");
        Assert.IsNull(result.Database, "No database should be handed out after a failed update");
        Assert.IsNotNull(result.Failure, "The exception behind the failure should be attached");
        Assert.IsEmpty(ReadSchema(databaseFile), "The failed first update should leave no schema entry in the new file");
    }

    /// <summary>
    /// A database of an earlier schema holding EF Core's lock table with a stale lock row (left by EF Core's own update
    /// or its tools, never by the server) is updated within the bound: the start never waits on that lock.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ServerDatabaseOpenEarlierSchemaWithLockRowUpdatesWithinBound()
    {
        // Arrange
        var databaseFile = await CreateCurrentSchemaDatabaseAsync();

        Execute(databaseFile, command => command.CommandText = CreateLockTableSql + InsertLockRowSql);

        // Act
        var result = await OpenWithTestMigrationsAsync(databaseFile, typeof(AddedTableTestMigration));

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Updated, result.Kind, "A database with a stale lock row should be updated within the bound");
        Assert.AreSequenceEqual(ExtendedMigrationsAssembly.RealMigrationIds.Append(AddedTableTestMigration.Id).ToList(), ReadHistory(databaseFile), "Each version applied should be recorded");
        Assert.AreSequenceEqual(new[] { KeptProfileName }, ReadProfileNames(databaseFile), "The stored profile should be kept across the update");
    }

    /// <summary>
    /// On Linux the database file, the files SQLite keeps beside it and the folder the start creates are owner-only.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [OSCondition(OperatingSystems.Linux)]
    [SupportedOSPlatform("linux")]
    public async Task ServerDatabaseOpenNewFileIsOwnerOnly()
    {
        // Arrange
        var folder = Path.Combine(CreateTemporaryDirectory(), "created");
        var databaseFile = Path.Combine(folder, DatabaseFileName);

        // Act
        var result = await OpenAsync(databaseFile);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Created, result.Kind, "A missing file should be reported as created");
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(folder), "The created folder should be accessible by its owner only");
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(databaseFile), "The database file should be readable and writable by its owner only");
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(databaseFile + "-wal"), "The write-ahead log beside the open database should be readable and writable by its owner only");
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(databaseFile + "-shm"), "The shared-memory file beside the open database should be readable and writable by its owner only");
    }

    /// <summary>
    /// Ends the test as inconclusive in a process that ignores file permissions.
    /// </summary>
    private static void SkipWhenPrivileged()
    {
        if (Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("The process ignores file permissions (root): the read-only case did not run");
        }
    }

    /// <summary>
    /// Makes folders writable for their owner again, so that they can be deleted.
    /// </summary>
    /// <param name="folders">The folders</param>
    private static void MakeFoldersWritable(List<string> folders)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var folder in folders.Where(Directory.Exists))
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// Writes the bytes of a file.
    /// </summary>
    /// <param name="path">The file</param>
    /// <param name="bytes">The bytes</param>
    private static void WriteBytes(string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
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
                          Mode = SqliteOpenMode.ReadWriteCreate,
                          Pooling = false
                      };
        var connection = new SqliteConnection(builder.ToString());

        connection.Open();

        return connection;
    }

    /// <summary>
    /// Runs one command over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="setCommandText">Sets the command's text (one or more statements)</param>
    private static void Execute(string databaseFile, Action<SqliteCommand> setCommandText)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        setCommandText(command);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Reads the first column of every row of a query over a connection of its own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="setCommandText">Sets the query's text</param>
    /// <returns>The values as text, in the order of the query</returns>
    private static List<string> ReadColumn(string databaseFile, Action<SqliteCommand> setCommandText)
    {
        using var connection = OpenRawConnection(databaseFile);
        using var command = connection.CreateCommand();

        setCommandText(command);

        using var reader = command.ExecuteReader();
        var values = new List<string>();

        while (reader.Read())
        {
            values.Add(reader.IsDBNull(0) ? "<null>" : reader.GetString(0));
        }

        return values;
    }

    /// <summary>
    /// Reads every schema entry of a file: type, name, table and statement.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>One line per entry, ordered by type and name</returns>
    private static List<string> ReadSchema(string databaseFile)
    {
        return ReadColumn(databaseFile, command => command.CommandText = "SELECT \"type\" || ' ' || \"name\" || ' ' || \"tbl_name\" || ' ' || ifnull(\"sql\", '') FROM \"sqlite_master\" ORDER BY \"type\", \"name\";");
    }

    /// <summary>
    /// Reads the applied migrations a file records.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The migration identifiers, sorted ordinally</returns>
    private static List<string> ReadHistory(string databaseFile)
    {
        return ReadColumn(databaseFile, command => command.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" COLLATE BINARY;");
    }

    /// <summary>
    /// Reads the tables of a file other than SQLite's and EF Core's own.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The table names, sorted ordinally</returns>
    private static List<string> ReadUserTables(string databaseFile)
    {
        return ReadColumn(databaseFile, command => command.CommandText = "SELECT \"name\" FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" NOT LIKE 'sqlite\\_%' ESCAPE '\\' AND \"name\" NOT LIKE '\\_\\_EF%' ESCAPE '\\' ORDER BY \"name\" COLLATE BINARY;");
    }

    /// <summary>
    /// Reads the names of the stored profiles.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <returns>The names, sorted ordinally</returns>
    private static List<string> ReadProfileNames(string databaseFile)
    {
        return ReadColumn(databaseFile, command => command.CommandText = "SELECT \"Name\" FROM \"Profiles\" ORDER BY \"Name\" COLLATE BINARY;");
    }

    /// <summary>
    /// Creates a temporary directory for the test, deleted afterwards.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-server-database-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        return directory;
    }

    /// <summary>
    /// Makes a folder read-only for its owner until the test ends.
    /// </summary>
    /// <param name="folder">The folder</param>
    [SupportedOSPlatform("linux")]
    private void MakeFolderReadOnly(string folder)
    {
        File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        _readOnlyFolders.Add(folder);
    }

    /// <summary>
    /// Creates a database at the current schema of the build holding one profile, closed again.
    /// </summary>
    /// <returns>The full path of the database file</returns>
    private async Task<string> CreateCurrentSchemaDatabaseAsync()
    {
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), DatabaseFileName);
        var result = await OpenAsync(databaseFile);

        result.Database?.Dispose();
        SqliteConnection.ClearAllPools();

        Execute(databaseFile, command => command.CommandText = "INSERT INTO \"Profiles\" (\"Id\", \"Name\") VALUES ('" + KeptProfileId + "', '" + KeptProfileName + "');");

        return databaseFile;
    }

    /// <summary>
    /// Opens a database with the real migrations, within <see cref="_openBound" />.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <returns>The result of opening</returns>
    private Task<ServerDatabaseOpenResult> OpenAsync(string databaseFile)
    {
        return OpenWithinBoundAsync(token => ServerDatabase.OpenAsync(databaseFile, _loggerFactory, token));
    }

    /// <summary>
    /// Opens a database with the real migrations plus test migrations, within <see cref="_openBound" />.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    /// <param name="testMigrations">The test migration classes</param>
    /// <returns>The result of opening</returns>
    private Task<ServerDatabaseOpenResult> OpenWithTestMigrationsAsync(string databaseFile, params Type[] testMigrations)
    {
        return OpenWithinBoundAsync(token => ServerDatabase.OpenAsync(databaseFile, _loggerFactory, builder => ExtendedMigrationsAssembly.Configure(builder, testMigrations), token));
    }

    /// <summary>
    /// Runs an open, cancelled and abandoned after <see cref="_openBound" />, and keeps its database for the cleanup.
    /// </summary>
    /// <param name="open">The open, given the cancellation token</param>
    /// <returns>The result of opening</returns>
    private async Task<ServerDatabaseOpenResult> OpenWithinBoundAsync(Func<CancellationToken, Task<ServerDatabaseOpenResult>> open)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        deadline.CancelAfter(_openBound);

        var result = await open(deadline.Token).WaitAsync(_openBound, TestContext.CancellationToken);

        if (result.Database is not null)
        {
            _databases.Add(result.Database);
        }

        return result;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _loggerFactory.Dispose();
        _loggerProvider.Dispose();
    }

    #endregion // IDisposable
}