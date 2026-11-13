using Censio.Server.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="DatabaseSetup" />, against real SQLite files in a temporary directory.
/// </summary>
[TestClass]
public sealed class DatabaseSetupTests
{
    #region Constants

    /// <summary>
    /// The warning of a start without a data directory and without a database file (S5).
    /// </summary>
    private const string InMemoryText = "No database file without a data directory (Censio:DataDirectory) or Censio:Database:File; data is kept in memory and lost when the server stops.";

    /// <summary>
    /// The database file the log line tests name, as in the image.
    /// </summary>
    private const string ReportedFile = "/config/censio.db";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The databases the running test opened, disposed afterwards.
    /// </summary>
    private readonly List<ServerDatabase> _databases = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseSetupTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public DatabaseSetupTests(TestContext testContext)
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

        SqliteConnection.ClearAllPools();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A start with a data directory and no database file creates <c>censio.db</c> there, logs the line of S1 and hands
    /// out the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task DatabaseSetupTryOpenWithDataDirectoryCreatesDatabaseAndLogsCreated()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = Path.Combine(dataDirectory, DatabaseSetup.DatabaseFileName);
        var logger = new RecordingLogger();

        // Act
        var database = await TryOpenAsync(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNotNull(database, "The created database should be handed out");
        Assert.AreEqual(databaseFile, database.DatabaseFile, "The database should be censio.db in the data directory");
        Assert.IsTrue(File.Exists(databaseFile), "censio.db should be created in the data directory");
        AssertSingleEntry(logger, LogLevel.Information, $"The database '{databaseFile}' was created.");
    }

    /// <summary>
    /// With <c>Censio:Database:File</c> set to an absolute path that file is the database, with or without a data
    /// directory, and no <c>censio.db</c> is created in the data directory.
    /// </summary>
    /// <param name="withDataDirectory">Whether a data directory is configured too</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DatabaseSetupTryOpenWithDatabaseFileUsesItWithoutDataDirectory(bool withDataDirectory)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = Path.Combine(CreateTemporaryDirectory(), "configured.db");
        var logger = new RecordingLogger();

        // Act
        var database = await TryOpenAsync(CreateConfiguration(withDataDirectory ? dataDirectory : null, databaseFile), logger);

        // Assert
        Assert.IsNotNull(database, "The configured database should be handed out");
        Assert.AreEqual(databaseFile, database.DatabaseFile, "The database should be the configured file");
        Assert.IsTrue(File.Exists(databaseFile), "The configured file should be created");
        Assert.IsEmpty(Directory.GetFileSystemEntries(dataDirectory), "No database should be created in the data directory");
        AssertSingleEntry(logger, LogLevel.Information, $"The database '{databaseFile}' was created.");
    }

    /// <summary>
    /// A configured database file whose folder is missing gets that folder created.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task DatabaseSetupTryOpenWithDatabaseFileCreatesMissingFolder()
    {
        // Arrange
        var folder = Path.Combine(CreateTemporaryDirectory(), "missing", "folder");
        var databaseFile = Path.Combine(folder, "censio.db");
        var logger = new RecordingLogger();

        // Act
        var database = await TryOpenAsync(CreateConfiguration(null, databaseFile), logger);

        // Assert
        Assert.IsNotNull(database, "The configured database should be handed out");
        Assert.IsTrue(Directory.Exists(folder), "The missing folder should be created");
        Assert.IsTrue(File.Exists(databaseFile), "The configured file should be created");
        AssertSingleEntry(logger, LogLevel.Information, $"The database '{databaseFile}' was created.");
    }

    /// <summary>
    /// A database file setting that is not an absolute path, the empty string included, stops the start with the line of
    /// E1 and creates no file.
    /// </summary>
    /// <param name="value">The value of the setting</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("relative/censio.db")]
    [DataRow("")]
    public async Task DatabaseSetupTryOpenWithRelativeDatabaseFileLogsCriticalAndReturnsNull(string value)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var logger = new RecordingLogger();

        // Act
        var database = await TryOpenAsync(CreateConfiguration(dataDirectory, value), logger);

        // Assert
        Assert.IsNull(database, "The start should stop");
        Assert.IsEmpty(Directory.GetFileSystemEntries(dataDirectory), "No database should be created in the data directory");
        Assert.IsFalse(Directory.Exists(Path.GetFullPath("relative")), "No file should be created relative to the working directory");
        AssertSingleEntry(logger, LogLevel.Critical, $"The database file (Censio:Database:File) must be an absolute path, not '{value}'; the server stops.");
    }

    /// <summary>
    /// Without a data directory and without the setting the start logs the warning of S5, goes on with a database in
    /// memory and writes no database file.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task DatabaseSetupTryOpenWithoutDataDirectoryLogsWarningAndWritesNoFile()
    {
        // Arrange
        var logger = new RecordingLogger();
        var workingDirectoryBefore = Directory.GetFileSystemEntries(Environment.CurrentDirectory).Order(StringComparer.Ordinal).ToList();

        // Act
        var database = await TryOpenAsync(CreateConfiguration(null, null), logger);

        // Assert
        Assert.IsNotNull(database, "The start should go on with a database in memory");
        Assert.IsNull(database.DatabaseFile, "The database should have no file");
        Assert.AreSequenceEqual(workingDirectoryBefore, Directory.GetFileSystemEntries(Environment.CurrentDirectory).Order(StringComparer.Ordinal).ToList(), "No file should be written to the working directory");
        AssertSingleEntry(logger, LogLevel.Warning, InMemoryText);
    }

    /// <summary>
    /// Each successful open is reported by its one line (S1, S2, S3, S5) and hands out the database.
    /// </summary>
    /// <param name="kind">The kind of the open result</param>
    /// <param name="level">The expected level</param>
    /// <param name="message">The expected line</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    [TestCategory("AC4")]
    [DataRow(ServerDatabaseOpenKind.Created, LogLevel.Information, "The database '/config/censio.db' was created.")]
    [DataRow(ServerDatabaseOpenKind.Opened, LogLevel.Information, "The database '/config/censio.db' was opened.")]
    [DataRow(ServerDatabaseOpenKind.Updated, LogLevel.Information, "The database '/config/censio.db' was updated to the current schema.")]
    [DataRow(ServerDatabaseOpenKind.InMemory, LogLevel.Warning, InMemoryText)]
    public async Task DatabaseSetupReportOpenResultLogsLinePerSuccessKind(ServerDatabaseOpenKind kind, LogLevel level, string message)
    {
        // Arrange
        var database = await OpenInMemoryDatabaseAsync();
        var databaseFile = kind == ServerDatabaseOpenKind.InMemory ? null : ReportedFile;
        var logger = new RecordingLogger();

        // Act
        var reported = DatabaseSetup.ReportOpenResult(new ServerDatabaseOpenResult(kind, databaseFile, database, null), logger);

        // Assert
        Assert.AreSame(database, reported, "The opened database should be handed out");
        AssertSingleEntry(logger, level, message);
        Assert.IsNull(logger.Entries[0].Exception, "A successful open carries no exception");
    }

    /// <summary>
    /// Each failed open is reported by its one critical line (E2 to E5) with the exception behind it, and stops the
    /// start.
    /// </summary>
    /// <param name="kind">The kind of the open result</param>
    /// <param name="withFailure">Whether the result carries an exception</param>
    /// <param name="message">The expected line</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(ServerDatabaseOpenKind.NotWritable, true, "The database '/config/censio.db' cannot be opened or written; the server stops. Make its folder writable for the user the server runs as.")]
    [DataRow(ServerDatabaseOpenKind.NotADatabase, true, "The file '/config/censio.db' is not a readable Censio database; the server stops.")]
    [DataRow(ServerDatabaseOpenKind.NewerSchema, false, "The database '/config/censio.db' was written by a newer Censio version; the server stops. Start that version or a newer one.")]
    [DataRow(ServerDatabaseOpenKind.UpdateFailed, true, "The database '/config/censio.db' cannot be updated to the current schema; the server stops.")]
    public void DatabaseSetupReportOpenResultLogsCriticalLinePerFailureKind(ServerDatabaseOpenKind kind, bool withFailure, string message)
    {
        // Arrange
        var failure = withFailure ? new IOException("test failure") : null;
        var logger = new RecordingLogger();

        // Act
        var reported = DatabaseSetup.ReportOpenResult(new ServerDatabaseOpenResult(kind, ReportedFile, null, failure), logger);

        // Assert
        Assert.IsNull(reported, "A failed open should stop the start");
        AssertSingleEntry(logger, LogLevel.Critical, message);
        Assert.AreSame(failure, logger.Entries[0].Exception, "The exception behind the failure should be attached to the line");
    }

    /// <summary>
    /// Creates a configuration with the data directory and the database file setting.
    /// </summary>
    /// <param name="dataDirectory">The data directory, or <c>null</c> for none</param>
    /// <param name="databaseFile">The value of the database file setting, or <c>null</c> for unset</param>
    /// <returns>The configuration</returns>
    private static IConfiguration CreateConfiguration(string? dataDirectory, string? databaseFile)
    {
        var settings = new Dictionary<string, string?>();

        if (dataDirectory is not null)
        {
            settings[DataProtectionSetup.DataDirectoryKey] = dataDirectory;
        }

        if (databaseFile is not null)
        {
            settings[DatabaseSetup.DatabaseFileKey] = databaseFile;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    /// <summary>
    /// Asserts that exactly one entry was logged, at the level and with the text given.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="level">The expected level</param>
    /// <param name="message">The expected text</param>
    private static void AssertSingleEntry(RecordingLogger logger, LogLevel level, string message)
    {
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(level, logger.Entries[0].Level, "The entry should have the level of its line");
        Assert.AreEqual(message, logger.Entries[0].Message, "The entry should carry the exact text of its line");
    }

    /// <summary>
    /// Opens the database the configuration names, and keeps it for the cleanup.
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <param name="logger">The logger</param>
    /// <returns>The database, or <see langword="null" /> when the start stops</returns>
    private async Task<ServerDatabase?> TryOpenAsync(IConfiguration configuration, RecordingLogger logger)
    {
        var database = await DatabaseSetup.TryOpenAsync(configuration, NullLoggerFactory.Instance, logger, TestContext.CancellationToken);

        if (database is not null)
        {
            _databases.Add(database);
        }

        return database;
    }

    /// <summary>
    /// Opens a database in memory, and keeps it for the cleanup.
    /// </summary>
    /// <returns>The database</returns>
    private async Task<ServerDatabase> OpenInMemoryDatabaseAsync()
    {
        var result = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        return database;
    }

    /// <summary>
    /// Creates an empty temporary directory that is deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "censio-database-setup-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);

        return path;
    }

    #endregion // Methods
}