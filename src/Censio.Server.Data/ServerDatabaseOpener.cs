using System.Data;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace Censio.Server.Data;

/// <summary>
/// Opens the server database: creates, classifies, probes, updates and secures it.
/// </summary>
internal static class ServerDatabaseOpener
{
    #region Constants

    /// <summary>
    /// The SQLite result code of a file that is not a database.
    /// </summary>
    private const int NotADatabaseCode = 26;

    /// <summary>
    /// The SQLite result code of a corrupt database image.
    /// </summary>
    private const int CorruptCode = 11;

    /// <summary>
    /// The name of EF Core's lock table.
    /// </summary>
    private const string LockTable = "__EFMigrationsLock";

    /// <summary>
    /// The name of EF Core's history table.
    /// </summary>
    private const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// The statement that lists the entries of the schema that are not SQLite's own.
    /// </summary>
    private const string UserEntriesSql = "SELECT \"name\" FROM \"sqlite_master\" WHERE \"name\" NOT LIKE 'sqlite\\_%' ESCAPE '\\' ORDER BY \"name\" COLLATE BINARY;";

    /// <summary>
    /// The statement that lists the applied migrations.
    /// </summary>
    private const string AppliedMigrationsSql = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" COLLATE BINARY;";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Opens the database.
    /// </summary>
    /// <param name="databaseFile">The full path of the database file, or <see langword="null"/> for an in-memory database</param>
    /// <param name="loggerFactory">The logger factory of the context</param>
    /// <param name="configureContext">Further configuration of the context options</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of opening</returns>
    public static async Task<ServerDatabaseOpenResult> OpenAsync(string? databaseFile, ILoggerFactory loggerFactory, Action<DbContextOptionsBuilder> configureContext, CancellationToken cancellationToken)
    {
        var created = false;

        if (databaseFile is not null)
        {
            try
            {
                created = PrepareFile(databaseFile);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new ServerDatabaseOpenResult(ServerDatabaseOpenKind.NotWritable, databaseFile, null, exception);
            }
        }

        var connectionString = CreateConnectionString(databaseFile);
        var optionsBuilder = new DbContextOptionsBuilder<CensioDbContext>().UseSqlite(connectionString)
                                                                           .UseLoggerFactory(loggerFactory);

        configureContext(optionsBuilder);

        var options = optionsBuilder.Options;
        var keeper = new SqliteConnection(connectionString);
        var succeeded = false;

        try
        {
            var result = await OpenCoreAsync(databaseFile, created, keeper, options, cancellationToken).ConfigureAwait(false);

            succeeded = result.Database is not null;

            return result;
        }
        finally
        {
            if (succeeded == false)
            {
                Release(keeper);
            }
        }
    }

    /// <summary>
    /// Closes a connection and the pool behind it.
    /// </summary>
    /// <param name="connection">The connection</param>
    public static void Release(SqliteConnection connection)
    {
        SqliteConnection.ClearPool(connection);
        connection.Dispose();
    }

    /// <summary>
    /// Creates the folder and the empty file when they are missing, owner-only on Linux.
    /// </summary>
    /// <param name="databaseFile">The full path of the database file</param>
    /// <returns><see langword="true"/> when the file was created</returns>
    private static bool PrepareFile(string databaseFile)
    {
        var folder = Path.GetDirectoryName(databaseFile);

        if (string.IsNullOrEmpty(folder) == false && Directory.Exists(folder) == false)
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(folder);
            }
            else
            {
                Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        if (File.Exists(databaseFile))
        {
            return false;
        }

        var fileOptions = new FileStreamOptions
                          {
                              Mode = FileMode.CreateNew,
                              Access = FileAccess.Write
                          };

        if (OperatingSystem.IsWindows())
        {
            new FileStream(databaseFile, fileOptions).Dispose();

            return true;
        }

        fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        new FileStream(databaseFile, fileOptions).Dispose();

        return true;
    }

    /// <summary>
    /// Creates the connection string of the database.
    /// </summary>
    /// <param name="databaseFile">The full path of the database file, or <see langword="null"/> for an in-memory database</param>
    /// <returns>The connection string</returns>
    private static string CreateConnectionString(string? databaseFile)
    {
        var builder = new SqliteConnectionStringBuilder();

        if (databaseFile is null)
        {
            builder.DataSource = "censio-" + Guid.NewGuid().ToString("N");
            builder.Mode = SqliteOpenMode.Memory;
            builder.Cache = SqliteCacheMode.Shared;
        }
        else
        {
            builder.DataSource = databaseFile;
            builder.Mode = SqliteOpenMode.ReadWrite;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Runs the opening steps over the kept connection.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null"/> for in memory</param>
    /// <param name="created">Whether the file was created by this start</param>
    /// <param name="keeper">The connection kept for the lifetime of the database</param>
    /// <param name="options">The context options</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of opening</returns>
    private static async Task<ServerDatabaseOpenResult> OpenCoreAsync(string? databaseFile, bool created, SqliteConnection keeper, DbContextOptions<CensioDbContext> options, CancellationToken cancellationToken)
    {
        using var context = new CensioDbContext(options);

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var applied = new List<string>();
        var state = FileState.Fresh;

        if (databaseFile is not null)
        {
            try
            {
                await keeper.OpenAsync(cancellationToken).ConfigureAwait(false);

                (state, applied) = await ClassifyAsync(keeper, migrationsAssembly, cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException exception)
            {
                return Failed(MapSqliteError(exception), databaseFile, exception);
            }

            switch (state)
            {
                case FileState.Foreign:
                    return Failed(ServerDatabaseOpenKind.NotADatabase, databaseFile, null);
                case FileState.Newer:
                    return Failed(ServerDatabaseOpenKind.NewerSchema, databaseFile, null);
            }

            var probeFailure = await ProbeWriteAsync(keeper, cancellationToken).ConfigureAwait(false);

            if (probeFailure is not null)
            {
                return Failed(ServerDatabaseOpenKind.NotWritable, databaseFile, probeFailure);
            }
        }
        else
        {
            await keeper.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var pending = migrationsAssembly.Migrations.Keys.Where(id => applied.Contains(id, StringComparer.Ordinal) == false).ToList();

        if (pending.Count > 0)
        {
            var updateFailure = await UpdateAsync(context, keeper, migrationsAssembly, pending, applied, cancellationToken).ConfigureAwait(false);

            if (updateFailure is not null)
            {
                return Failed(ServerDatabaseOpenKind.UpdateFailed, databaseFile, updateFailure);
            }
        }

        if (databaseFile is not null)
        {
            var failure = await EnableWriteAheadLogAsync(keeper, cancellationToken).ConfigureAwait(false);

            if (failure is not null)
            {
                return Failed(ServerDatabaseOpenKind.NotWritable, databaseFile, failure);
            }
        }

        var kind = ResolveKind(databaseFile, created, pending.Count);

        return new ServerDatabaseOpenResult(kind, databaseFile, new ServerDatabase(databaseFile, keeper, options), null);
    }

    /// <summary>
    /// Maps a failure of reading the file to a result kind.
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <returns>The result kind</returns>
    private static ServerDatabaseOpenKind MapSqliteError(SqliteException exception)
    {
        return exception.SqliteErrorCode is NotADatabaseCode or CorruptCode
                   ? ServerDatabaseOpenKind.NotADatabase
                   : ServerDatabaseOpenKind.NotWritable;
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="kind">The result kind</param>
    /// <param name="databaseFile">The database file</param>
    /// <param name="failure">The exception behind the failure, or <see langword="null"/></param>
    /// <returns>The result</returns>
    private static ServerDatabaseOpenResult Failed(ServerDatabaseOpenKind kind, string? databaseFile, Exception? failure)
    {
        return new ServerDatabaseOpenResult(kind, databaseFile, null, failure);
    }

    /// <summary>
    /// Resolves the success kind.
    /// </summary>
    /// <param name="databaseFile">The database file, or <see langword="null"/> for in memory</param>
    /// <param name="created">Whether the file was created by this start</param>
    /// <param name="pendingCount">The number of migrations applied by this start</param>
    /// <returns>The result kind</returns>
    private static ServerDatabaseOpenKind ResolveKind(string? databaseFile, bool created, int pendingCount)
    {
        if (databaseFile is null)
        {
            return ServerDatabaseOpenKind.InMemory;
        }

        if (created)
        {
            return ServerDatabaseOpenKind.Created;
        }

        return pendingCount > 0
                   ? ServerDatabaseOpenKind.Updated
                   : ServerDatabaseOpenKind.Opened;
    }

    /// <summary>
    /// Classifies an existing file, read-only.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="migrationsAssembly">The migrations of this build</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The state and the applied migrations</returns>
    private static async Task<(FileState State, List<string> Applied)> ClassifyAsync(SqliteConnection connection, IMigrationsAssembly migrationsAssembly, CancellationToken cancellationToken)
    {
        var entries = await ReadColumnAsync(connection, UserEntriesSql, cancellationToken).ConfigureAwait(false);
        var hasHistory = entries.Contains(HistoryTable, StringComparer.Ordinal);
        var applied = hasHistory
                          ? await ReadColumnAsync(connection, AppliedMigrationsSql, cancellationToken).ConfigureAwait(false)
                          : [];

        if (entries.All(entry => entry is LockTable or HistoryTable) && applied.Count == 0)
        {
            return (FileState.Fresh, applied);
        }

        var known = migrationsAssembly.Migrations.Keys;

        if (applied.Count == 0 || string.Equals(applied[0], known.First(), StringComparison.Ordinal) == false)
        {
            return (FileState.Foreign, applied);
        }

        return applied.Exists(id => known.Contains(id, StringComparer.Ordinal) == false)
                   ? (FileState.Newer, applied)
                   : (FileState.Existing, applied);
    }

    /// <summary>
    /// Reads the first column of a query as text.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="sql">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The values</returns>
    private static async Task<List<string>> ReadColumnAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Only the constants of this class and the script EF Core generates reach here, no input
        command.CommandText = sql;
#pragma warning restore CA2100

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var values = new List<string>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    /// <summary>
    /// Checks that the file can be written, by a table created and rolled back.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The exception behind a failure, or <see langword="null"/></returns>
    private static async Task<Exception?> ProbeWriteAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var begun = false;

        try
        {
            await ExecuteAsync(connection, "BEGIN IMMEDIATE;", cancellationToken).ConfigureAwait(false);

            begun = true;

            await ExecuteAsync(connection, "CREATE TABLE \"__CensioWriteProbe\" (\"Id\" INTEGER);", cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (SqliteException exception)
        {
            return exception;
        }
        finally
        {
            if (begun)
            {
                await ExecuteAsync(connection, "ROLLBACK;", CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sets the write-ahead log, which survives a power loss with the default synchronization.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The exception behind a failure, or <see langword="null"/></returns>
    private static async Task<Exception?> EnableWriteAheadLogAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);

            // The side files appear with the first access in the new mode, so the kept connection makes it now
            await ReadColumnAsync(connection, UserEntriesSql, cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (SqliteException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Runs one statement.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="sql">The statement</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Only the constants of this class and the script EF Core generates reach here, no input
        command.CommandText = sql;
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the pending migrations as one script in one transaction.
    /// </summary>
    /// <param name="context">The context, used for its services only</param>
    /// <param name="connection">The opened connection</param>
    /// <param name="migrationsAssembly">The migrations of this build</param>
    /// <param name="pending">The pending migration identifiers</param>
    /// <param name="applied">The applied migration identifiers</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The exception behind a failure, or <see langword="null"/></returns>
    private static async Task<Exception?> UpdateAsync(CensioDbContext context, SqliteConnection connection, IMigrationsAssembly migrationsAssembly, List<string> pending, List<string> applied, CancellationToken cancellationToken)
    {
        try
        {
            var suppressed = FindSuppressedTransaction(context, migrationsAssembly, pending);

            if (suppressed is not null)
            {
                return new InvalidOperationException($"The migration {suppressed} runs a step outside of a transaction");
            }

            var script = context.GetService<IMigrator>().GenerateScript(applied.Count == 0 ? null : applied[^1], null, MigrationsSqlGenerationOptions.NoTransactions);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();

            command.Transaction = (SqliteTransaction)transaction;
#pragma warning disable CA2100 // The script is generated by EF Core from the migrations of this build
            command.CommandText = script;
#pragma warning restore CA2100

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return exception;
        }
    }

    /// <summary>
    /// Finds a pending migration that has a step EF Core runs outside of a transaction.
    /// </summary>
    /// <param name="context">The context, used for its services only</param>
    /// <param name="migrationsAssembly">The migrations of this build</param>
    /// <param name="pending">The pending migration identifiers</param>
    /// <returns>The identifier of the first such migration, or <see langword="null"/></returns>
    private static string? FindSuppressedTransaction(CensioDbContext context, IMigrationsAssembly migrationsAssembly, List<string> pending)
    {
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var providerName = context.Database.ProviderName;

        foreach (var id in pending)
        {
            var migration = migrationsAssembly.CreateMigration(migrationsAssembly.Migrations[id], providerName!);
            var model = migration.TargetModel;

            if (model is IMutableModel mutable)
            {
                model = mutable.FinalizeModel();
            }

            var commands = generator.Generate(migration.UpOperations, initializer.Initialize(model), MigrationsSqlGenerationOptions.Default);

            if (commands.Any(command => command.TransactionSuppressed))
            {
                return id;
            }
        }

        return null;
    }

    #endregion // Methods
}