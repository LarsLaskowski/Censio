using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Folds, sweeps and reads the history of the server database.
/// </summary>
public sealed class HistoryStore : IHistoryTrends
{
    #region Constants

    /// <summary>
    /// The number of points a trend read takes in one part.
    /// </summary>
    public const int TrendPartSize = 240;

    /// <summary>
    /// The statement that selects the latest receive time of a profile.
    /// </summary>
    private const string LatestReceiveSql = "SELECT max(\"ReceiveTicks\") FROM \"Ticks\" WHERE \"ProfileId\" = $profile;";

    /// <summary>
    /// The statement that selects the oldest tick of a profile received before a time.
    /// </summary>
    private const string OldestTickSql = "SELECT \"Number\" FROM \"Ticks\" WHERE \"ProfileId\" = $profile AND \"ReceiveTicks\" < $limit ORDER BY \"ReceiveTicks\", \"Number\" LIMIT 1;";

    /// <summary>
    /// The statement that selects a tick of a deleted profile.
    /// </summary>
    private const string DeletedTickSql = "SELECT t.\"Number\" FROM \"DeletedProfiles\" d CROSS JOIN \"Ticks\" t ON t.\"ProfileId\" = d.\"TombstoneId\" LIMIT 1;";

    /// <summary>
    /// The statement that selects an hourly point of a deleted profile.
    /// </summary>
    private const string DeletedPointSql = "SELECT p.\"Number\" FROM \"DeletedProfiles\" d CROSS JOIN \"HourlyPoints\" p ON p.\"ProfileId\" = d.\"TombstoneId\" LIMIT 1;";

    /// <summary>
    /// The statement that selects a tombstone.
    /// </summary>
    private const string TombstoneSql = "SELECT \"TombstoneId\" FROM \"DeletedProfiles\" LIMIT 1;";

    /// <summary>
    /// The statement that selects the hourly points of a series with the island and good of the series.
    /// </summary>
    private const string HourlySeriesSql = "SELECT p.\"HourTicks\", p.\"TickCount\", p.\"FirstGameTime\", p.\"LastGameTime\", i.\"Name\", e.\"Id\", e.\"EntryCount\", "
                                           + "e.\"GenerationSum\", e.\"GenerationCount\", e.\"ConsumptionSum\", e.\"ConsumptionCount\", e.\"DeltaSum\", e.\"DeltaCount\", e.\"PerfectGenerationSum\", e.\"PerfectGenerationCount\", "
                                           + "e.\"PerfectConsumptionSum\", e.\"PerfectConsumptionCount\", e.\"IncomeSum\", e.\"IncomeCount\", e.\"SummedProductivitySum\", e.\"SummedProductivityCount\", e.\"AverageProductivitySum\", e.\"AverageProductivityCount\", "
                                           + "e.\"BuildingCountSum\", e.\"MaintenanceSum\", e.\"ProfitSum\" "
                                           + "FROM \"HourlyPoints\" p "
                                           + "LEFT JOIN \"HourlyIslands\" i ON i.\"PointNumber\" = p.\"Number\" AND i.\"SessionGuid\" = $session AND i.\"IslandId\" = $island AND i.\"AreaIndex\" = $area "
                                           + "LEFT JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = i.\"Id\" AND e.\"ProductGuid\" = $product "
                                           + "WHERE p.\"ProfileId\" = $profile AND p.\"HourTicks\" >= $start AND p.\"HourTicks\" < $end ORDER BY p.\"HourTicks\";";

    /// <summary>
    /// The statement that selects the ticks of a series with the island and the first entry of the good of the series.
    /// </summary>
    private const string TickSeriesSql = "SELECT t.\"Number\", t.\"ReceiveTicks\", t.\"GameTime\", i.\"Name\", e.\"Id\", "
                                         + "e.\"Generation\", e.\"Consumption\", e.\"Delta\", e.\"PerfectGeneration\", e.\"PerfectConsumption\", e.\"Income\", e.\"SummedProductivity\", e.\"AverageProductivity\", "
                                         + "e.\"BuildingCount\", e.\"Maintenance\", e.\"Profit\" "
                                         + "FROM \"Ticks\" t "
                                         + "LEFT JOIN \"TickIslands\" i ON i.\"TickNumber\" = t.\"Number\" AND i.\"SessionGuid\" = $session AND i.\"IslandId\" = $island AND i.\"AreaIndex\" = $area "
                                         + "LEFT JOIN \"TickEntries\" e ON e.\"Id\" = (SELECT x.\"Id\" FROM \"TickEntries\" x WHERE x.\"TickIslandId\" = i.\"Id\" AND x.\"ProductGuid\" = $product ORDER BY x.\"Position\" LIMIT 1) "
                                         + "WHERE t.\"ProfileId\" = $profile AND t.\"ReceiveTicks\" >= $start AND t.\"ReceiveTicks\" < $end ORDER BY t.\"ReceiveTicks\", t.\"Number\";";

    /// <summary>
    /// The statement that selects the map pairs of an hourly entry.
    /// </summary>
    private const string HourlyPairsSql = "SELECT \"Kind\", \"Guid\", \"AmountSum\" FROM \"HourlyMapPairs\" WHERE \"EntryId\" = $entry ORDER BY \"Kind\", \"Guid\";";

    /// <summary>
    /// The statement that selects the map pairs of a tick entry.
    /// </summary>
    private const string TickPairsSql = "SELECT \"Kind\", \"Guid\", \"Amount\" FROM \"TickMapPairs\" WHERE \"EntryId\" = $entry ORDER BY \"Kind\", \"Guid\";";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The time span ticks are kept at full resolution
    /// </summary>
    public static readonly TimeSpan FullResolution = TimeSpan.FromDays(7);

    /// <summary>
    /// The database
    /// </summary>
    private readonly ServerDatabase _database;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryStore"/> class.
    /// </summary>
    /// <param name="database">The database</param>
    public HistoryStore(ServerDatabase database)
    {
        _database = database;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Folds the oldest tick of a profile beyond full resolution into its hourly point.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="now">The current time</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the step</returns>
    public Task<HistoryResult> FoldNextTickAsync(Guid profileId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return StoreAccess.WriteAsync(_database,
                                      async connection =>
                                      {
                                          var cutoffTicks = (now - FullResolution).UtcTicks;
                                          var cutoff = cutoffTicks - (cutoffTicks % TimeSpan.TicksPerHour);

                                          using var latestCommand = StoreAccess.CreateCommand(connection, LatestReceiveSql, ("$profile", profileId));

                                          if (await latestCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not long latest)
                                          {
                                              return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
                                          }

                                          var limit = Math.Min(cutoff, latest - (latest % TimeSpan.TicksPerHour));

                                          using var oldestCommand = StoreAccess.CreateCommand(connection, OldestTickSql, ("$profile", profileId), ("$limit", limit));

                                          if (await oldestCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not long number)
                                          {
                                              return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
                                          }

                                          var tick = await TickReader.ReadInTransactionAsync(connection, TickReader.ByNumberSql, number, cancellationToken).ConfigureAwait(false);

                                          using var folder = new HistoryFolder(connection);

                                          var hour = await folder.FoldAsync(tick!, number, cancellationToken).ConfigureAwait(false);

                                          return new HistoryResult(HistoryOutcome.Done, null, hour);
                                      },
                                      exception => new HistoryResult(HistoryOutcome.Failed, exception, null),
                                      cancellationToken);
    }

    /// <summary>
    /// Removes one piece of deleted history.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the step</returns>
    public Task<HistoryResult> SweepNextAsync(CancellationToken cancellationToken)
    {
        return StoreAccess.WriteAsync(_database,
                                      async connection =>
                                      {
                                          using var tickCommand = StoreAccess.CreateCommand(connection, DeletedTickSql);

                                          if (await tickCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long tickNumber)
                                          {
                                              await HistoryFolder.RemoveTickAsync(connection, tickNumber, cancellationToken).ConfigureAwait(false);

                                              return new HistoryResult(HistoryOutcome.Done, null, null);
                                          }

                                          using var pointCommand = StoreAccess.CreateCommand(connection, DeletedPointSql);

                                          if (await pointCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long pointNumber)
                                          {
                                              await HistoryFolder.RemovePointAsync(connection, pointNumber, cancellationToken).ConfigureAwait(false);

                                              return new HistoryResult(HistoryOutcome.Done, null, null);
                                          }

                                          using var tombstoneCommand = StoreAccess.CreateCommand(connection, TombstoneSql);

                                          if (await tombstoneCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not string tombstone)
                                          {
                                              return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
                                          }

                                          await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"DeletedProfiles\" WHERE \"TombstoneId\" = $tombstone;", cancellationToken, ("$tombstone", tombstone)).ConfigureAwait(false);

                                          return new HistoryResult(HistoryOutcome.Done, null, null);
                                      },
                                      exception => new HistoryResult(HistoryOutcome.Failed, exception, null),
                                      cancellationToken);
    }

    /// <summary>
    /// Checkpoints and truncates the write-ahead log so no deleted data stays in it, when no deleted profile is left to sweep.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>
    /// <see cref="HistoryOutcome.NothingToDo"/> when a tombstone is left, so the sweep is not finished and nothing was cleared;
    /// otherwise <see cref="HistoryOutcome.Done"/> (also for the in-memory database, which has no file),
    /// <see cref="HistoryOutcome.Blocked"/> when a read blocks the truncation, or <see cref="HistoryOutcome.Failed"/>
    /// </returns>
    public async Task<HistoryResult> ClearDeletedAsync(CancellationToken cancellationToken)
    {
        await _database.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var tombstoneCommand = StoreAccess.CreateCommand(_database.Connection, TombstoneSql);

            if (await tombstoneCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
            }

            if (_database.DatabaseFile is null)
            {
                return new HistoryResult(HistoryOutcome.Done, null, null);
            }

            using var command = StoreAccess.CreateCommand(_database.Connection, "PRAGMA wal_checkpoint(TRUNCATE);");
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            return new HistoryResult(reader.GetInt32(0) == 0 ? HistoryOutcome.Done : HistoryOutcome.Blocked, null, null);
        }
        catch (SqliteException exception)
        {
            return new HistoryResult(HistoryOutcome.Failed, exception, null);
        }
        finally
        {
            _database.Gate.Release();
        }
    }

    /// <summary>
    /// Reads the history series of one good on one island.
    /// </summary>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points in time order</returns>
    public Task<IReadOnlyList<HistoryPoint>> ReadSeriesAsync(HistorySeriesQuery query, CancellationToken cancellationToken)
    {
        return StoreAccess.ReadAsync<IReadOnlyList<HistoryPoint>>(_database,
                                                                  async connection =>
                                                                  {
#pragma warning disable S6966 // The async overload has no deferred form and begins an immediate (write) transaction
                                                                      using var transaction = connection.BeginTransaction(true);
#pragma warning restore S6966

                                                                      var hourly = await ReadHourlyPointsAsync(connection, query, cancellationToken).ConfigureAwait(false);
                                                                      var ticks = await ReadTickPointsAsync(connection, query, cancellationToken).ConfigureAwait(false);

                                                                      return Merge(hourly, ticks);
                                                                  },
                                                                  cancellationToken);
    }

    /// <summary>
    /// Merges the hourly points and the tick points, both in time order, the hourly point first on equal times.
    /// </summary>
    /// <param name="hourly">The hourly points</param>
    /// <param name="ticks">The tick points</param>
    /// <returns>All points in time order</returns>
    private static List<HistoryPoint> Merge(List<HistoryPoint> hourly, List<HistoryPoint> ticks)
    {
        var merged = new List<HistoryPoint>(hourly.Count + ticks.Count);
        var hourlyIndex = 0;
        var tickIndex = 0;

        while (hourlyIndex < hourly.Count && tickIndex < ticks.Count)
        {
            if (hourly[hourlyIndex].Time <= ticks[tickIndex].Time)
            {
                merged.Add(hourly[hourlyIndex++]);
            }
            else
            {
                merged.Add(ticks[tickIndex++]);
            }
        }

        merged.AddRange(hourly.Skip(hourlyIndex));
        merged.AddRange(ticks.Skip(tickIndex));

        return merged;
    }

    /// <summary>
    /// Creates the command of a series statement with the parameters of the query.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="query">The query</param>
    /// <returns>The command</returns>
    private static SqliteCommand CreateSeriesCommand(SqliteConnection connection, string sql, HistorySeriesQuery query)
    {
        return StoreAccess.CreateCommand(connection,
                                         sql,
                                         ("$profile", query.ProfileId),
                                         ("$session", query.SessionGuid),
                                         ("$island", (int)query.IslandId),
                                         ("$area", (int)query.AreaIndex),
                                         ("$product", query.ProductGuid),
                                         ("$start", query.RangeStart.UtcTicks),
                                         ("$end", query.RangeEnd.UtcTicks));
    }

    /// <summary>
    /// Reads the hourly points of a series.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points, oldest first</returns>
    private static async Task<List<HistoryPoint>> ReadHourlyPointsAsync(SqliteConnection connection, HistorySeriesQuery query, CancellationToken cancellationToken)
    {
        var points = new List<HistoryPoint>();

        using var command = CreateSeriesCommand(connection, HourlySeriesSql, query);
        using var pairsCommand = StoreAccess.CreateCommand(connection, HourlyPairsSql, ("$entry", 0L));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(5))
            {
                points.Add(CreatePoint((new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero), true, reader.GetInt32(1), reader.GetInt64(2), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4)), new double?[11], ([], [])));

                continue;
            }

            var entryCount = reader.GetInt32(6);
            var values = new double?[11];

            for (var index = 0; index < 8; index++)
            {
                var count = reader.GetInt32(8 + (2 * index));

                values[index] = count == 0
                                    ? null
                                    : reader.GetDouble(7 + (2 * index)) / count;
            }

            values[8] = reader.GetInt64(23) / (double)entryCount;
            values[9] = reader.GetInt64(24) / (double)entryCount;
            values[10] = reader.GetInt64(25) / (double)entryCount;

            var amounts = await ReadAmountsAsync(pairsCommand, reader.GetInt64(5), entryCount, cancellationToken).ConfigureAwait(false);

            points.Add(CreatePoint((new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero), true, reader.GetInt32(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4)), values, amounts));
        }

        return points;
    }

    /// <summary>
    /// Reads the tick points of a series.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points, oldest first</returns>
    private static async Task<List<HistoryPoint>> ReadTickPointsAsync(SqliteConnection connection, HistorySeriesQuery query, CancellationToken cancellationToken)
    {
        var points = new List<HistoryPoint>();

        using var command = CreateSeriesCommand(connection, TickSeriesSql, query);
        using var pairsCommand = StoreAccess.CreateCommand(connection, TickPairsSql, ("$entry", 0L));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var time = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
            var gameTime = reader.GetInt64(2);
            var name = reader.IsDBNull(3) ? null : reader.GetString(3);

            if (reader.IsDBNull(4))
            {
                points.Add(CreatePoint((time, false, 1, gameTime, gameTime, name), new double?[11], ([], [])));

                continue;
            }

            var values = new double?[11];

            for (var index = 0; index < 8; index++)
            {
                values[index] = reader.IsDBNull(5 + index)
                                    ? null
                                    : BitConverter.Int32BitsToSingle(reader.GetInt32(5 + index));
            }

            values[8] = reader.GetInt32(13);
            values[9] = reader.GetInt32(14);
            values[10] = reader.GetInt32(15);

            var amounts = await ReadAmountsAsync(pairsCommand, reader.GetInt64(4), 1, cancellationToken).ConfigureAwait(false);

            points.Add(CreatePoint((time, false, 1, gameTime, gameTime, name), values, amounts));
        }

        return points;
    }

    /// <summary>
    /// Reads the workforce and building amounts of an entry, each divided by a number of ticks.
    /// </summary>
    /// <param name="command">The command that selects the pairs of an entry</param>
    /// <param name="entryRow">The row number of the entry</param>
    /// <param name="divisor">The number the sums are divided by</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The workforce and the building amounts, by GUID</returns>
    private static async Task<(List<HistoryAmount> Workforce, List<HistoryAmount> Buildings)> ReadAmountsAsync(SqliteCommand command, long entryRow, int divisor, CancellationToken cancellationToken)
    {
        var workforce = new List<HistoryAmount>();
        var buildings = new List<HistoryAmount>();

        command.Parameters[0].Value = entryRow;

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var amount = new HistoryAmount
                         {
                             ItemGuid = reader.GetInt32(1),
                             Amount = reader.GetInt64(2) / (double)divisor
                         };

            if (reader.GetByte(0) == TickWriter.WorkforceKind)
            {
                workforce.Add(amount);
            }
            else
            {
                buildings.Add(amount);
            }
        }

        return (workforce, buildings);
    }

    /// <summary>
    /// Creates a point.
    /// </summary>
    /// <param name="head">The time, whether the point is hourly, the number of ticks, the smallest and the largest game time and the island name or <see langword="null"/></param>
    /// <param name="values">The eleven values in the order of the point's properties from <c>Generation</c> to <c>Profit</c></param>
    /// <param name="amounts">The workforce and the building amounts</param>
    /// <returns>The point</returns>
    private static HistoryPoint CreatePoint((DateTimeOffset Time, bool IsHourly, int TickCount, long FirstGameTime, long LastGameTime, string? IslandName) head, double?[] values, (List<HistoryAmount> Workforce, List<HistoryAmount> Buildings) amounts)
    {
        return new HistoryPoint
               {
                   Time = head.Time,
                   IsHourly = head.IsHourly,
                   TickCount = head.TickCount,
                   FirstGameTime = head.FirstGameTime,
                   LastGameTime = head.LastGameTime,
                   IslandName = head.IslandName,
                   Generation = values[0],
                   Consumption = values[1],
                   Delta = values[2],
                   PerfectGeneration = values[3],
                   PerfectConsumption = values[4],
                   Income = values[5],
                   SummedProductivity = values[6],
                   AverageProductivity = values[7],
                   BuildingCount = values[8],
                   Maintenance = values[9],
                   Profit = values[10],
                   WorkforceAmounts = amounts.Workforce,
                   BuildingAmounts = amounts.Buildings
               };
    }

    #endregion // Methods

    #region IHistoryTrends

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken)
    {
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken)
    {
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // IHistoryTrends
}