using System.Text.Json;

using Censio.Contracts;

using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Stores and reads the server's data in the server database.
/// </summary>
public sealed class ServerStore
{
    #region Fields

    /// <summary>
    /// The database
    /// </summary>
    private readonly ServerDatabase _database;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStore"/> class.
    /// </summary>
    /// <param name="database">The database</param>
    public ServerStore(ServerDatabase database)
    {
        _database = database;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Stores the name of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreProfileAsync(Guid profileId, string name, CancellationToken cancellationToken)
    {
        return WriteAsync(async connection =>
                          {
                              await ExecuteAsync(connection,
                                                 "INSERT INTO \"Profiles\" (\"Id\", \"Name\") VALUES ($id, $name) ON CONFLICT (\"Id\") DO UPDATE SET \"Name\" = excluded.\"Name\";",
                                                 cancellationToken,
                                                 ("$id", profileId),
                                                 ("$name", name)).ConfigureAwait(false);

                              return ServerStoreOutcome.Stored;
                          },
                          cancellationToken);
    }

    /// <summary>
    /// Lists the stored profiles.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The profiles</returns>
    public Task<IReadOnlyList<StoredProfile>> ListProfilesAsync(CancellationToken cancellationToken)
    {
        return ReadAsync<IReadOnlyList<StoredProfile>>(async connection =>
                                                       {
                                                           var profiles = new List<StoredProfile>();

                                                           using var command = CreateCommand(connection, "SELECT \"Id\", \"Name\" FROM \"Profiles\" ORDER BY \"Id\";");
                                                           using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                                                           while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                                           {
                                                               profiles.Add(new StoredProfile
                                                                            {
                                                                                Id = reader.GetGuid(0),
                                                                                Name = reader.IsDBNull(1) ? null : reader.GetString(1)
                                                                            });
                                                           }

                                                           return profiles;
                                                       },
                                                       cancellationToken);
    }

    /// <summary>
    /// Stores a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreTickAsync(ContractTick tick, CancellationToken cancellationToken)
    {
        return WriteAsync(async connection =>
                          {
                              using var exists = CreateCommand(connection, "SELECT 1 FROM \"Ticks\" WHERE \"Id\" = $id;", ("$id", tick.Id));

                              if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                              {
                                  return ServerStoreOutcome.AlreadyStored;
                              }

                              await ExecuteAsync(connection, "INSERT OR IGNORE INTO \"Profiles\" (\"Id\", \"Name\") VALUES ($id, NULL);", cancellationToken, ("$id", tick.ProfileId)).ConfigureAwait(false);

                              using var writer = new TickWriter(connection);

                              await writer.WriteAsync(tick, cancellationToken).ConfigureAwait(false);

                              return ServerStoreOutcome.Stored;
                          },
                          cancellationToken);
    }

    /// <summary>
    /// Reads a tick.
    /// </summary>
    /// <param name="tickId">The tick identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The tick, or <see langword="null"/> when unknown</returns>
    public Task<ContractTick?> ReadTickAsync(Guid tickId, CancellationToken cancellationToken)
    {
        return ReadAsync(connection => TickReader.ReadAsync(connection, TickReader.ByIdSql, tickId, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Reads the latest tick of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The tick, or <see langword="null"/> when none is stored</returns>
    public Task<ContractTick?> ReadLatestTickAsync(Guid profileId, CancellationToken cancellationToken)
    {
        return ReadAsync(connection => TickReader.ReadAsync(connection, TickReader.LatestSql, profileId, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Lists the islands of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The islands</returns>
    public Task<IReadOnlyList<StoredIsland>> ListIslandsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        return ReadAsync<IReadOnlyList<StoredIsland>>(async connection =>
                                                      {
                                                          var islands = new List<StoredIsland>();

                                                          using var command = CreateCommand(connection,
                                                                                            "SELECT \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"Name\" FROM \"Islands\" WHERE \"ProfileId\" = $profile ORDER BY \"SessionGuid\", \"IslandId\", \"AreaIndex\";",
                                                                                            ("$profile", profileId));
                                                          using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                                                          while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                                          {
                                                              islands.Add(new StoredIsland
                                                                          {
                                                                              SessionGuid = reader.GetInt32(0),
                                                                              IslandId = reader.GetByte(1),
                                                                              AreaIndex = reader.GetByte(2),
                                                                              Name = reader.GetString(3)
                                                                          });
                                                          }

                                                          return islands;
                                                      },
                                                      cancellationToken);
    }

    /// <summary>
    /// Stores the catalog, replacing an earlier one.
    /// </summary>
    /// <param name="upload">The catalog upload</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreCatalogAsync(ContractCatalogUpload upload, CancellationToken cancellationToken)
    {
        return WriteAsync(async connection =>
                          {
                              await ExecuteAsync(connection,
                                                 "INSERT INTO \"Catalog\" (\"Id\", \"Identity\", \"Document\") VALUES (1, $identity, $document) ON CONFLICT (\"Id\") DO UPDATE SET \"Identity\" = excluded.\"Identity\", \"Document\" = excluded.\"Document\";",
                                                 cancellationToken,
                                                 ("$identity", upload.ComputeIdentity()),
                                                 ("$document", upload.Document.ToArray())).ConfigureAwait(false);

                              return ServerStoreOutcome.Stored;
                          },
                          cancellationToken);
    }

    /// <summary>
    /// Reads the stored catalog.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The catalog, or <see langword="null"/> when none is stored</returns>
    public Task<StoredCatalog?> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        return ReadAsync(async connection =>
                         {
                             using var command = CreateCommand(connection, "SELECT \"Identity\", \"Document\" FROM \"Catalog\" WHERE \"Id\" = 1;");
                             using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                             if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == false)
                             {
                                 return null;
                             }

                             return new StoredCatalog
                                    {
                                        Identity = reader.GetString(0),
                                        Document = (byte[])reader["Document"]
                                    };
                         },
                         cancellationToken);
    }

    /// <summary>
    /// Stores an icon.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreIconAsync(ContractIconImage icon, CancellationToken cancellationToken)
    {
        return WriteAsync(async connection =>
                          {
                              var changed = await ExecuteAsync(connection,
                                                               "INSERT OR IGNORE INTO \"Icons\" (\"Reference\", \"Png\") VALUES ($reference, $png);",
                                                               cancellationToken,
                                                               ("$reference", icon.Reference),
                                                               ("$png", icon.Png.ToArray())).ConfigureAwait(false);

                              return changed == 0
                                         ? ServerStoreOutcome.AlreadyStored
                                         : ServerStoreOutcome.Stored;
                          },
                          cancellationToken);
    }

    /// <summary>
    /// Reads an icon.
    /// </summary>
    /// <param name="reference">The icon reference key</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The PNG bytes, or <see langword="null"/> when unknown</returns>
    public Task<byte[]?> ReadIconAsync(string reference, CancellationToken cancellationToken)
    {
        return ReadAsync(async connection =>
                         {
                             using var command = CreateCommand(connection, "SELECT \"Png\" FROM \"Icons\" WHERE \"Reference\" = $reference;", ("$reference", reference));

                             return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
                         },
                         cancellationToken);
    }

    /// <summary>
    /// Finds the references without a stored icon.
    /// </summary>
    /// <param name="references">The icon reference keys</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The missing references, distinct and ordinally sorted</returns>
    public Task<IReadOnlyList<string>> FindMissingIconsAsync(IReadOnlyCollection<string> references, CancellationToken cancellationToken)
    {
        return ReadAsync<IReadOnlyList<string>>(async connection =>
                                                {
                                                    var missing = new List<string>();

                                                    using var command = CreateCommand(connection,
                                                                                      "SELECT DISTINCT j.\"value\" FROM json_each($references) j WHERE NOT EXISTS (SELECT 1 FROM \"Icons\" i WHERE i.\"Reference\" = j.\"value\");",
                                                                                      ("$references", JsonSerializer.Serialize(references)));
                                                    using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                                                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                                    {
                                                        missing.Add(reader.GetString(0));
                                                    }

                                                    missing.Sort(StringComparer.Ordinal);

                                                    return missing;
                                                },
                                                cancellationToken);
    }

    /// <summary>
    /// Rolls back the open transaction, when SQLite has not ended it already.
    /// </summary>
    /// <param name="connection">The connection</param>
    private static void Rollback(SqliteConnection connection)
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
    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only, values travel as parameters
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
    private static async Task<int> ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        using var command = CreateCommand(connection, sql, parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a write in one immediate transaction, serialized by the gate of the database.
    /// </summary>
    /// <param name="write">The write, which returns its outcome</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result; a failure of SQLite rolls everything back</returns>
    private async Task<ServerStoreResult> WriteAsync(Func<SqliteConnection, Task<ServerStoreOutcome>> write, CancellationToken cancellationToken)
    {
        await _database.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var connection = _database.Connection;
            var begun = false;
            var committed = false;

            try
            {
                await ExecuteAsync(connection, "BEGIN IMMEDIATE;", cancellationToken).ConfigureAwait(false);

                begun = true;

                var outcome = await write(connection).ConfigureAwait(false);

                await ExecuteAsync(connection, "COMMIT;", cancellationToken).ConfigureAwait(false);

                committed = true;

                return new ServerStoreResult(outcome, null);
            }
            catch (SqliteException exception)
            {
                return new ServerStoreResult(ServerStoreOutcome.Failed, exception);
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
            _database.Gate.Release();
        }
    }

    /// <summary>
    /// Runs a read: in memory on the kept connection under the gate, from a file on a connection of its own.
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="read">The read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the read</returns>
    private async Task<T> ReadAsync<T>(Func<SqliteConnection, Task<T>> read, CancellationToken cancellationToken)
    {
        if (_database.DatabaseFile is null)
        {
            await _database.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await read(_database.Connection).ConfigureAwait(false);
            }
            finally
            {
                _database.Gate.Release();
            }
        }

        using var connection = new SqliteConnection(_database.Connection.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await read(connection).ConfigureAwait(false);
    }

    #endregion // Methods
}