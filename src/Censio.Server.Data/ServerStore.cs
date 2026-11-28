using System.Text.Json;

using Censio.Contracts;

using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Stores and reads the server's data in the server database.
/// </summary>
public sealed class ServerStore
{
    #region Constants

    /// <summary>
    /// The name of the profile parameter of the trade-route statements.
    /// </summary>
    private const string ProfileParameter = "$profile";

    /// <summary>
    /// The name of the source profile parameter of the merge statements.
    /// </summary>
    private const string SourceParameter = "$source";

    /// <summary>
    /// The name of the target profile parameter of the merge statements.
    /// </summary>
    private const string TargetParameter = "$target";

    #endregion // Constants

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
                              await StoreAccess.ExecuteAsync(connection,
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

                                                           using var command = StoreAccess.CreateCommand(connection, "SELECT \"Id\", \"Name\" FROM \"Profiles\" ORDER BY \"Id\";");
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
                              using var exists = StoreAccess.CreateCommand(connection, "SELECT 1 WHERE EXISTS (SELECT 1 FROM \"Ticks\" WHERE \"Id\" = $id) OR EXISTS (SELECT 1 FROM \"FoldedTicks\" WHERE \"Id\" = $id);", ("$id", tick.Id));

                              if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                              {
                                  return ServerStoreOutcome.AlreadyStored;
                              }

                              await StoreAccess.ExecuteAsync(connection, "INSERT OR IGNORE INTO \"Profiles\" (\"Id\", \"Name\") VALUES ($id, NULL);", cancellationToken, ("$id", tick.ProfileId)).ConfigureAwait(false);

                              using var writer = new TickWriter(connection);

                              await writer.WriteAsync(tick, cancellationToken).ConfigureAwait(false);

                              return ServerStoreOutcome.Stored;
                          },
                          cancellationToken);
    }

    /// <summary>
    /// Deletes a profile with its history: its profile row and islands at once, its ticks and hourly points are hidden
    /// by a tombstone and removed by the sweep steps of the history store.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the deletion</returns>
    public Task<HistoryResult> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        return StoreAccess.WriteAsync(_database,
                                      async connection =>
                                      {
                                          using var exists = StoreAccess.CreateCommand(connection, "SELECT 1 FROM \"Profiles\" WHERE \"Id\" = $id;", ("$id", profileId));

                                          if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                                          {
                                              return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
                                          }

                                          var tombstone = Guid.NewGuid();

                                          await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"Profiles\" WHERE \"Id\" = $id;", cancellationToken, ("$id", profileId)).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"Islands\" WHERE \"ProfileId\" = $id;", cancellationToken, ("$id", profileId)).ConfigureAwait(false);
                                          await DeleteTradeRoutesAsync(connection, profileId, cancellationToken).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "INSERT INTO \"DeletedProfiles\" (\"TombstoneId\") VALUES ($tombstone);", cancellationToken, ("$tombstone", tombstone)).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "UPDATE \"Ticks\" SET \"ProfileId\" = $tombstone WHERE \"ProfileId\" = $id;", cancellationToken, ("$tombstone", tombstone), ("$id", profileId)).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "UPDATE \"HourlyPoints\" SET \"ProfileId\" = $tombstone WHERE \"ProfileId\" = $id;", cancellationToken, ("$tombstone", tombstone), ("$id", profileId)).ConfigureAwait(false);

                                          return new HistoryResult(HistoryOutcome.Done, null, null);
                                      },
                                      exception => new HistoryResult(HistoryOutcome.Failed, exception, null),
                                      cancellationToken);
    }

    /// <summary>
    /// Merges the history of a profile into another profile's in one transaction: islands, ticks, hourly points, trade routes
    /// and the profile row of the source move to the target, which is created without a name when it is not stored.
    /// </summary>
    /// <param name="profileId">The identity of the profile whose history is moved (the source)</param>
    /// <param name="targetProfileId">The identity of the profile that receives the history</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the merge</returns>
    public Task<HistoryResult> MergeProfileAsync(Guid profileId, Guid targetProfileId, CancellationToken cancellationToken)
    {
        return StoreAccess.WriteAsync(_database,
                                      async connection =>
                                      {
                                          using var exists = StoreAccess.CreateCommand(connection, "SELECT 1 FROM \"Profiles\" WHERE \"Id\" = $id;", ("$id", profileId));

                                          if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                                          {
                                              return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
                                          }

                                          var parameters = new (string Name, object Value)[] { (SourceParameter, profileId), (TargetParameter, targetProfileId) };

                                          await StoreAccess.ExecuteAsync(connection, "INSERT OR IGNORE INTO \"Profiles\" (\"Id\", \"Name\") VALUES ($target, NULL);", cancellationToken, (TargetParameter, targetProfileId)).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection,
                                                                         "INSERT INTO \"Islands\" (\"ProfileId\", \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"Name\", \"NameReceiveTicks\", \"NameTickNumber\") "
                                                                         + "SELECT $target, \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"Name\", \"NameReceiveTicks\", \"NameTickNumber\" FROM \"Islands\" WHERE \"ProfileId\" = $source "
                                                                         + "ON CONFLICT (\"ProfileId\", \"SessionGuid\", \"IslandId\", \"AreaIndex\") DO UPDATE SET \"Name\" = excluded.\"Name\", \"NameReceiveTicks\" = excluded.\"NameReceiveTicks\", \"NameTickNumber\" = excluded.\"NameTickNumber\" "
                                                                         + "WHERE (excluded.\"NameReceiveTicks\", excluded.\"NameTickNumber\") >= (\"Islands\".\"NameReceiveTicks\", \"Islands\".\"NameTickNumber\");",
                                                                         cancellationToken,
                                                                         parameters).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"Islands\" WHERE \"ProfileId\" = $source;", cancellationToken, (SourceParameter, profileId)).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "UPDATE \"Ticks\" SET \"ProfileId\" = $target WHERE \"ProfileId\" = $source;", cancellationToken, parameters).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection,
                                                                         "UPDATE \"HourlyPoints\" SET \"ProfileId\" = $target WHERE \"ProfileId\" = $source AND \"HourTicks\" NOT IN (SELECT \"HourTicks\" FROM \"HourlyPoints\" WHERE \"ProfileId\" = $target);",
                                                                         cancellationToken,
                                                                         parameters).ConfigureAwait(false);
                                          await HourlyPointMerge.CombineAsync(connection, profileId, targetProfileId, cancellationToken).ConfigureAwait(false);
                                          await MergeTradeRoutesAsync(connection, profileId, targetProfileId, cancellationToken).ConfigureAwait(false);
                                          await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"Profiles\" WHERE \"Id\" = $source;", cancellationToken, (SourceParameter, profileId)).ConfigureAwait(false);

                                          return new HistoryResult(HistoryOutcome.Done, null, null);
                                      },
                                      exception => new HistoryResult(HistoryOutcome.Failed, exception, null),
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
        return ReadAsync(connection => ReadIslandsAsync(connection, profileId, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Reads the profile row, its latest tick and its islands in one read transaction.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The state, or <see langword="null"/> when the profile is unknown</returns>
    public Task<StoredProfileState?> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)
    {
        return ReadAsync(async connection =>
                         {
#pragma warning disable S6966 // The async overload has no deferred form and begins an immediate (write) transaction
                             using var transaction = connection.BeginTransaction(true);
#pragma warning restore S6966

                             StoredProfile profile;

                             using (var command = StoreAccess.CreateCommand(connection, "SELECT \"Id\", \"Name\" FROM \"Profiles\" WHERE \"Id\" = $id;", ("$id", profileId)))
                             {
                                 using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                                 if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == false)
                                 {
                                     return null;
                                 }

                                 profile = new StoredProfile
                                           {
                                               Id = reader.GetGuid(0),
                                               Name = reader.IsDBNull(1) ? null : reader.GetString(1)
                                           };
                             }

                             var latestTick = await TickReader.ReadInTransactionAsync(connection, TickReader.LatestSql, profileId, cancellationToken).ConfigureAwait(false);
                             var islands = await ReadIslandsAsync(connection, profileId, cancellationToken).ConfigureAwait(false);
                             var latestTradeRoutes = await ReadTradeRoutesAsync(connection, profileId, cancellationToken).ConfigureAwait(false);

                             return new StoredProfileState
                                    {
                                        Profile = profile,
                                        LatestTick = latestTick,
                                        Islands = islands,
                                        LatestTradeRoutes = latestTradeRoutes
                                    };
                         },
                         cancellationToken);
    }

    /// <summary>
    /// Lists the profiles with ticks, the one with the newest latest tick first.
    /// </summary>
    /// <param name="count">The maximum number of profiles</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The profile identities</returns>
    public Task<IReadOnlyList<Guid>> ListLatestProfilesAsync(int count, CancellationToken cancellationToken)
    {
        return ReadAsync<IReadOnlyList<Guid>>(async connection =>
                                              {
                                                  var profiles = new List<Guid>();

                                                  using var command = StoreAccess.CreateCommand(connection,
                                                                                                "SELECT p.\"Id\" FROM \"Profiles\" p JOIN \"Ticks\" t ON t.\"Number\" = (SELECT \"Number\" FROM \"Ticks\" WHERE \"ProfileId\" = p.\"Id\" ORDER BY \"ReceiveTicks\" DESC, \"Number\" DESC LIMIT 1) ORDER BY t.\"ReceiveTicks\" DESC, t.\"Number\" DESC LIMIT $count;",
                                                                                                ("$count", count));
                                                  using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                                                  while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                                  {
                                                      profiles.Add(reader.GetGuid(0));
                                                  }

                                                  return profiles;
                                              },
                                              cancellationToken);
    }

    /// <summary>
    /// Stores the trade routes of a profile, replacing the earlier ones: a message with the stored identity or an earlier
    /// receive time than the stored set writes nothing.
    /// </summary>
    /// <param name="routes">The trade routes</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreTradeRoutesAsync(ContractTradeRoutes routes, CancellationToken cancellationToken)
    {
        return WriteAsync(async connection =>
                          {
                              using (var stored = StoreAccess.CreateCommand(connection,
                                                                            "SELECT 1 FROM \"TradeRouteSets\" WHERE \"ProfileId\" = $profile AND (\"MessageId\" = $message OR \"ReceiveTicks\" > $receive);",
                                                                            (ProfileParameter, routes.ProfileId),
                                                                            ("$message", routes.Id),
                                                                            ("$receive", routes.ReceiveTime.UtcTicks)))
                              {
                                  if (await stored.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                                  {
                                      return ServerStoreOutcome.AlreadyStored;
                                  }
                              }

                              await StoreAccess.ExecuteAsync(connection, "INSERT OR IGNORE INTO \"Profiles\" (\"Id\", \"Name\") VALUES ($id, NULL);", cancellationToken, ("$id", routes.ProfileId)).ConfigureAwait(false);
                              await DeleteTradeRoutesAsync(connection, routes.ProfileId, cancellationToken).ConfigureAwait(false);
                              await StoreAccess.ExecuteAsync(connection,
                                                             "INSERT INTO \"TradeRouteSets\" (\"ProfileId\", \"MessageId\", \"ReceiveTicks\") VALUES ($profile, $message, $receive);",
                                                             cancellationToken,
                                                             (ProfileParameter, routes.ProfileId),
                                                             ("$message", routes.Id),
                                                             ("$receive", routes.ReceiveTime.UtcTicks)).ConfigureAwait(false);

                              await WriteTradeRoutesAsync(connection, routes, cancellationToken).ConfigureAwait(false);

                              return ServerStoreOutcome.Stored;
                          },
                          cancellationToken);
    }

    /// <summary>
    /// Reads the latest trade routes of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The trade routes, or <see langword="null"/> when none are stored</returns>
    public Task<ContractTradeRoutes?> ReadTradeRoutesAsync(Guid profileId, CancellationToken cancellationToken)
    {
        return ReadAsync(async connection =>
                         {
#pragma warning disable S6966 // The async overload has no deferred form and begins an immediate (write) transaction
                             using var transaction = connection.BeginTransaction(true);
#pragma warning restore S6966

                             return await ReadTradeRoutesAsync(connection, profileId, cancellationToken).ConfigureAwait(false);
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
                              await StoreAccess.ExecuteAsync(connection,
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
                             using var command = StoreAccess.CreateCommand(connection, "SELECT \"Identity\", \"Document\" FROM \"Catalog\" WHERE \"Id\" = 1;");
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
                              var changed = await StoreAccess.ExecuteAsync(connection,
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
                             using var command = StoreAccess.CreateCommand(connection, "SELECT \"Png\" FROM \"Icons\" WHERE \"Reference\" = $reference;", ("$reference", reference));

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

                                                    using var command = StoreAccess.CreateCommand(connection,
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
    /// Deletes the stored trade routes of a profile: its stations, routes and set.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task DeleteTradeRoutesAsync(SqliteConnection connection, Guid profileId, CancellationToken cancellationToken)
    {
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"TradeRouteStations\" WHERE \"TradeRouteId\" IN (SELECT \"Id\" FROM \"TradeRoutes\" WHERE \"ProfileId\" = $id);", cancellationToken, ("$id", profileId)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"TradeRoutes\" WHERE \"ProfileId\" = $id;", cancellationToken, ("$id", profileId)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"TradeRouteSets\" WHERE \"ProfileId\" = $id;", cancellationToken, ("$id", profileId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves the trade routes of the source to the target when the target has none or the source's set was received later,
    /// and drops the source's set otherwise.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    /// <param name="sourceProfileId">The identity of the source profile</param>
    /// <param name="targetProfileId">The identity of the target profile</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task MergeTradeRoutesAsync(SqliteConnection connection, Guid sourceProfileId, Guid targetProfileId, CancellationToken cancellationToken)
    {
        using var keep = StoreAccess.CreateCommand(connection,
                                                   "SELECT EXISTS (SELECT 1 FROM \"TradeRouteSets\" s WHERE s.\"ProfileId\" = $source AND NOT EXISTS (SELECT 1 FROM \"TradeRouteSets\" t WHERE t.\"ProfileId\" = $target AND t.\"ReceiveTicks\" >= s.\"ReceiveTicks\"));",
                                                   (SourceParameter, sourceProfileId),
                                                   (TargetParameter, targetProfileId));
        var isSourceKept = (long)(await keep.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! != 0;

        if (isSourceKept == false)
        {
            await DeleteTradeRoutesAsync(connection, sourceProfileId, cancellationToken).ConfigureAwait(false);

            return;
        }

        await DeleteTradeRoutesAsync(connection, targetProfileId, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "UPDATE \"TradeRouteSets\" SET \"ProfileId\" = $target WHERE \"ProfileId\" = $source;", cancellationToken, (SourceParameter, sourceProfileId), (TargetParameter, targetProfileId)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "UPDATE \"TradeRoutes\" SET \"ProfileId\" = $target WHERE \"ProfileId\" = $source;", cancellationToken, (SourceParameter, sourceProfileId), (TargetParameter, targetProfileId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the routes and stations of a set in order.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    /// <param name="routes">The trade routes</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task WriteTradeRoutesAsync(SqliteConnection connection, ContractTradeRoutes routes, CancellationToken cancellationToken)
    {
        using var routeCommand = TickWriter.Create(connection,
                                                   "INSERT INTO \"TradeRoutes\" (\"ProfileId\", \"Position\", \"RouteId\", \"Name\", \"Folder\", \"MajorProblems\", \"MinorProblems\", \"Ships\", \"LostShips\") VALUES ($profile, $position, $route, $name, $folder, $major, $minor, $ships, $lost) RETURNING \"Id\";",
                                                   (ProfileParameter, SqliteType.Text),
                                                   ("$position", SqliteType.Integer),
                                                   ("$route", SqliteType.Integer),
                                                   ("$name", SqliteType.Text),
                                                   ("$folder", SqliteType.Text),
                                                   ("$major", SqliteType.Integer),
                                                   ("$minor", SqliteType.Integer),
                                                   ("$ships", SqliteType.Integer),
                                                   ("$lost", SqliteType.Integer));
        using var stationCommand = TickWriter.Create(connection,
                                                     "INSERT INTO \"TradeRouteStations\" (\"TradeRouteId\", \"Position\", \"SessionGuid\", \"IslandId\", \"AreaIndex\") VALUES ($route, $position, $guid, $island, $area);",
                                                     ("$route", SqliteType.Integer),
                                                     ("$position", SqliteType.Integer),
                                                     ("$guid", SqliteType.Integer),
                                                     ("$island", SqliteType.Integer),
                                                     ("$area", SqliteType.Integer));

        await routeCommand.PrepareAsync(cancellationToken).ConfigureAwait(false);
        await stationCommand.PrepareAsync(cancellationToken).ConfigureAwait(false);

        for (var position = 0; position < routes.Routes.Count; position++)
        {
            var route = routes.Routes[position];

            routeCommand.Parameters[0].Value = routes.ProfileId;
            routeCommand.Parameters[1].Value = position;
            routeCommand.Parameters[2].Value = route.Id;
            routeCommand.Parameters[3].Value = route.Name;
            routeCommand.Parameters[4].Value = route.Folder;
            routeCommand.Parameters[5].Value = route.MajorProblems;
            routeCommand.Parameters[6].Value = route.MinorProblems;
            routeCommand.Parameters[7].Value = route.Ships;
            routeCommand.Parameters[8].Value = route.LostShips;

            var routeRow = (long)(await routeCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

            for (var index = 0; index < route.Stations.Count; index++)
            {
                var station = route.Stations[index];

                stationCommand.Parameters[0].Value = routeRow;
                stationCommand.Parameters[1].Value = index;
                stationCommand.Parameters[2].Value = station.SessionGuid;
                stationCommand.Parameters[3].Value = station.IslandId;
                stationCommand.Parameters[4].Value = station.AreaIndex;

                await stationCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Reads the trade routes of a profile on an opened connection.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The trade routes, or <see langword="null"/> when none are stored</returns>
    private static async Task<ContractTradeRoutes?> ReadTradeRoutesAsync(SqliteConnection connection, Guid profileId, CancellationToken cancellationToken)
    {
        Guid messageId;
        long receiveTicks;

        using (var command = StoreAccess.CreateCommand(connection, "SELECT \"MessageId\", \"ReceiveTicks\" FROM \"TradeRouteSets\" WHERE \"ProfileId\" = $profile;", (ProfileParameter, profileId)))
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == false)
            {
                return null;
            }

            messageId = reader.GetGuid(0);
            receiveTicks = reader.GetInt64(1);
        }

        var stations = new Dictionary<long, List<ContractTradeRouteStation>>();

        using (var command = StoreAccess.CreateCommand(connection,
                                                       "SELECT s.\"TradeRouteId\", s.\"SessionGuid\", s.\"IslandId\", s.\"AreaIndex\" FROM \"TradeRouteStations\" s JOIN \"TradeRoutes\" r ON r.\"Id\" = s.\"TradeRouteId\" WHERE r.\"ProfileId\" = $profile ORDER BY r.\"Position\", s.\"Position\";",
                                                       (ProfileParameter, profileId)))
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var routeRow = reader.GetInt64(0);

                if (stations.TryGetValue(routeRow, out var list) == false)
                {
                    list = [];
                    stations.Add(routeRow, list);
                }

                list.Add(new ContractTradeRouteStation
                         {
                             SessionGuid = reader.GetInt32(1),
                             IslandId = reader.GetByte(2),
                             AreaIndex = reader.GetByte(3)
                         });
            }
        }

        var routes = new List<ContractTradeRoute>();

        using (var command = StoreAccess.CreateCommand(connection,
                                                       "SELECT \"Id\", \"RouteId\", \"Name\", \"Folder\", \"MajorProblems\", \"MinorProblems\", \"Ships\", \"LostShips\" FROM \"TradeRoutes\" WHERE \"ProfileId\" = $profile ORDER BY \"Position\";",
                                                       (ProfileParameter, profileId)))
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                routes.Add(new ContractTradeRoute
                           {
                               Id = reader.GetInt32(1),
                               Name = reader.GetString(2),
                               Folder = reader.GetString(3),
                               MajorProblems = reader.GetInt32(4),
                               MinorProblems = reader.GetInt32(5),
                               Ships = reader.GetInt32(6),
                               LostShips = reader.GetInt32(7),
                               Stations = stations.TryGetValue(reader.GetInt64(0), out var list) ? list : []
                           });
            }
        }

        return new ContractTradeRoutes
               {
                   Id = messageId,
                   ProfileId = profileId,
                   ReceiveTime = new DateTimeOffset(receiveTicks, TimeSpan.Zero),
                   Routes = routes
               };
    }

    /// <summary>
    /// Reads the islands of a profile on an opened connection.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The islands</returns>
    private static async Task<IReadOnlyList<StoredIsland>> ReadIslandsAsync(SqliteConnection connection, Guid profileId, CancellationToken cancellationToken)
    {
        var islands = new List<StoredIsland>();

        using var command = StoreAccess.CreateCommand(connection,
                                                      "SELECT \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"Name\" FROM \"Islands\" WHERE \"ProfileId\" = $profile ORDER BY \"SessionGuid\", \"IslandId\", \"AreaIndex\";",
                                                      (ProfileParameter, profileId));
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
    }

    /// <summary>
    /// Runs a write in one immediate transaction, serialized by the gate of the database.
    /// </summary>
    /// <param name="write">The write, which returns its outcome</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result; a failure of SQLite rolls everything back</returns>
    private Task<ServerStoreResult> WriteAsync(Func<SqliteConnection, Task<ServerStoreOutcome>> write, CancellationToken cancellationToken)
    {
        return StoreAccess.WriteAsync(_database,
                                      async connection => new ServerStoreResult(await write(connection).ConfigureAwait(false), null),
                                      exception => new ServerStoreResult(ServerStoreOutcome.Failed, exception),
                                      cancellationToken);
    }

    /// <summary>
    /// Runs a read: in memory on the kept connection under the gate, from a file on a connection of its own.
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="read">The read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the read</returns>
    private Task<T> ReadAsync<T>(Func<SqliteConnection, Task<T>> read, CancellationToken cancellationToken)
    {
        return StoreAccess.ReadAsync(_database, read, cancellationToken);
    }

    #endregion // Methods
}