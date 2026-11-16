using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Runs writes and reads of the stores on the server database.
/// </summary>
internal static class StoreAccess
{
    #region Methods

    /// <summary>
    /// Rolls back the open transaction, when SQLite has not ended it already.
    /// </summary>
    /// <param name="connection">The connection</param>
    public static void Rollback(SqliteConnection connection)
    {
        try
        {
            using var command = CreateCommand(connection, "ROLLBACK;");

            command.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // SQLite ended the transaction itself, so nothing is left to roll back
        }
    }

    /// <summary>
    /// Creates a command with parameters.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="parameters">The parameters</param>
    /// <returns>The command</returns>
    public static SqliteCommand CreateCommand(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of the stores only, values travel as parameters
        command.CommandText = sql;
#pragma warning restore CA2100

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }

    /// <summary>
    /// Runs one statement.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <param name="parameters">The parameters</param>
    /// <returns>The number of changed rows</returns>
    public static async Task<int> ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        using var command = CreateCommand(connection, sql, parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a write in one immediate transaction, serialized by the gate of the database.
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="database">The database</param>
    /// <param name="write">The write, which returns its result</param>
    /// <param name="failed">Creates the result of a failure of SQLite</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the write; a failure of SQLite rolls everything back</returns>
    public static async Task<T> WriteAsync<T>(ServerDatabase database, Func<SqliteConnection, Task<T>> write, Func<SqliteException, T> failed, CancellationToken cancellationToken)
    {
        await database.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var connection = database.Connection;
            var begun = false;
            var committed = false;

            try
            {
                await ExecuteAsync(connection, "BEGIN IMMEDIATE;", cancellationToken).ConfigureAwait(false);

                begun = true;

                var result = await write(connection).ConfigureAwait(false);

                await ExecuteAsync(connection, "COMMIT;", cancellationToken).ConfigureAwait(false);

                committed = true;

                return result;
            }
            catch (SqliteException exception)
            {
                return failed(exception);
            }
            finally
            {
                if (begun && committed == false)
                {
                    Rollback(connection);
                }
            }
        }
        finally
        {
            database.Gate.Release();
        }
    }

    /// <summary>
    /// Runs a read: in memory on the kept connection under the gate, from a file on a connection of its own.
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="database">The database</param>
    /// <param name="read">The read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the read</returns>
    public static async Task<T> ReadAsync<T>(ServerDatabase database, Func<SqliteConnection, Task<T>> read, CancellationToken cancellationToken)
    {
        if (database.DatabaseFile is null)
        {
            await database.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await read(database.Connection).ConfigureAwait(false);
            }
            finally
            {
                database.Gate.Release();
            }
        }

        using var connection = new SqliteConnection(database.Connection.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await read(connection).ConfigureAwait(false);
    }

    #endregion // Methods
}