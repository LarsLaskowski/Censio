using Censio.Contracts;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="ServerStateSetup" />, over a real database: a file in a temporary directory, or in memory.
/// </summary>
[TestClass]
public sealed class ServerStateSetupTests
{
    #region Constants

    /// <summary>
    /// The file name of the database in the temporary directory.
    /// </summary>
    private const string DatabaseFileName = "censio.db";

    /// <summary>
    /// A profile name.
    /// </summary>
    private const string ProfileName = "Profile name 7f3c";

    /// <summary>
    /// An island name.
    /// </summary>
    private const string IslandName = "Island name 9b2d";

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

    /// <summary>
    /// The logger of the setup and the state.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStateSetupTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ServerStateSetupTests(TestContext testContext)
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
        CloseDatabases();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Restoring a file with three stored profiles, one without a tick, logs the number of profiles and nothing else,
    /// and reports success.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ServerStateSetupTryRestoreAsyncLogsProfileCount()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var writer = await OpenStoreAsync(databaseFile);

        await writer.StoreProfileAsync(Guid.NewGuid(), ProfileName, TestContext.CancellationToken);
        await writer.StoreTickAsync(CreateTick(Guid.NewGuid()), TestContext.CancellationToken);
        await writer.StoreTickAsync(CreateTick(Guid.NewGuid()), TestContext.CancellationToken);
        CloseDatabases();

        var state = new ServerState(await OpenStoreAsync(databaseFile), _logger);

        // Act
        var restored = await ServerStateSetup.TryRestoreAsync(state, databaseFile, _logger, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(restored, "The restore should succeed");
        Assert.AreSequenceEqual(new[] { (LogLevel.Information, "Profiles restored from the database: 3.", (Exception?)null) }, _logger.Entries.ToList(), "The restore should log the number of stored profiles and nothing else");
    }

    /// <summary>
    /// Restoring an in-memory database logs no profile and reports success.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ServerStateSetupTryRestoreAsyncInMemoryLogsZero()
    {
        // Arrange
        var state = new ServerState(await OpenStoreAsync(null), _logger);

        // Act
        var restored = await ServerStateSetup.TryRestoreAsync(state, null, _logger, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(restored, "The restore should succeed");
        Assert.AreSequenceEqual(new[] { (LogLevel.Information, "Profiles restored from the database: 0.", (Exception?)null) }, _logger.Entries.ToList(), "The restore should log that no profile was restored");
    }

    /// <summary>
    /// A database whose tick table was renamed opens, but its state cannot be read: the restore logs the critical line
    /// with the file and the exception, and reports failure.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ServerStateSetupTryRestoreAsyncUnreadableStateLogsCriticalAndReturnsFalse()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();
        var writer = await OpenStoreAsync(databaseFile);

        await writer.StoreTickAsync(CreateTick(Guid.NewGuid()), TestContext.CancellationToken);
        CloseDatabases();
        Execute(databaseFile, "ALTER TABLE \"Ticks\" RENAME TO \"TicksMoved\";");

        var state = new ServerState(await OpenStoreAsync(databaseFile), _logger);

        // Act
        var restored = await ServerStateSetup.TryRestoreAsync(state, databaseFile, _logger, TestContext.CancellationToken);

        // Assert
        var entries = _logger.Entries;

        Assert.IsFalse(restored, "The restore should fail");
        Assert.AreSequenceEqual(new[] { (LogLevel.Critical, $"The current state cannot be read from the database '{databaseFile}'; the server stops.") }, entries.Select(entry => (entry.Level, entry.Message)).ToList(), "The restore should log the critical line with the file and nothing else");
        Assert.IsInstanceOfType<SqliteException>(entries[0].Exception, "The critical line should carry the exception");
    }

    /// <summary>
    /// Creates a tick with one island.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The tick</returns>
    private static ContractTick CreateTick(Guid profileId)
    {
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero),
                   GameTime = 1,
                   Islands = [
                                 new ContractTickIsland
                                 {
                                     SessionGuid = 1,
                                     IslandId = 1,
                                     AreaIndex = 0,
                                     SessionId = 1,
                                     Name = IslandName,
                                     Entries = []
                                 }
                             ]
               };
    }

    /// <summary>
    /// Runs a statement over a connection of its own, without the pool.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="sql">The statement</param>
    private static void Execute(string databaseFile, string sql)
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };

        using var connection = new SqliteConnection(builder.ToString());

        connection.Open();

        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only
        command.CommandText = sql;
#pragma warning restore CA2100

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates the path of a database file in a new temporary directory.
    /// </summary>
    /// <returns>The full path of the database file, not yet created</returns>
    private string CreateDatabaseFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-server-state-setup-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        return Path.Combine(directory, DatabaseFileName);
    }

    /// <summary>
    /// Disposes the databases opened so far and closes their pooled connections.
    /// </summary>
    private void CloseDatabases()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _databases.Clear();
        SqliteConnection.ClearAllPools();
    }

    /// <summary>
    /// Opens a database and a store on it, and keeps the database for the cleanup.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null" /> for an in-memory database</param>
    /// <returns>The store</returns>
    private async Task<ServerStore> OpenStoreAsync(string? databaseFile)
    {
        var result = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);
        var database = result.Database ?? throw new InvalidOperationException($"The database could not be opened: {result.Kind}", result.Failure);

        _databases.Add(database);

        return new ServerStore(database);
    }

    #endregion // Methods
}