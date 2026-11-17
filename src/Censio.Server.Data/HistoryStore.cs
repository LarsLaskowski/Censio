using System.Text.Json;

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

    /// <summary>
    /// The name of the profile parameter of the statements.
    /// </summary>
    private const string ProfileParameter = "$profile";

    /// <summary>
    /// The expression that makes one number of an island key of a tick entry's island.
    /// </summary>
    private const string TickIslandKeySql = "(ti.\"SessionGuid\" * 65536 + ti.\"IslandId\" * 256 + ti.\"AreaIndex\")";

    /// <summary>
    /// The expression that makes one number of an island key of an hourly island.
    /// </summary>
    private const string HourlyIslandKeySql = "(hi.\"SessionGuid\" * 65536 + hi.\"IslandId\" * 256 + hi.\"AreaIndex\")";

    /// <summary>
    /// The statement that selects the newest hourly points of a range with their times.
    /// </summary>
    private const string HourlyTimesSql = "SELECT \"HourTicks\", \"Number\" FROM \"HourlyPoints\" WHERE \"ProfileId\" = $profile AND \"HourTicks\" >= $start AND \"HourTicks\" < $end ORDER BY \"HourTicks\" DESC, \"Number\" DESC LIMIT $max;";

    /// <summary>
    /// The statement that selects the newest ticks of a range with their times.
    /// </summary>
    private const string TickTimesSql = "SELECT \"ReceiveTicks\", \"Number\" FROM \"Ticks\" WHERE \"ProfileId\" = $profile AND \"ReceiveTicks\" >= $start AND \"ReceiveTicks\" < $end ORDER BY \"ReceiveTicks\" DESC, \"Number\" DESC LIMIT $max;";

    /// <summary>
    /// The statement that counts the hourly points of a range.
    /// </summary>
    private const string HourlyCountSql = "SELECT count(*) FROM \"HourlyPoints\" WHERE \"ProfileId\" = $profile AND \"HourTicks\" >= $start AND \"HourTicks\" < $end;";

    /// <summary>
    /// The statement that counts the ticks of a range.
    /// </summary>
    private const string TickCountSql = "SELECT count(*) FROM \"Ticks\" WHERE \"ProfileId\" = $profile AND \"ReceiveTicks\" >= $start AND \"ReceiveTicks\" < $end;";

    /// <summary>
    /// The statement that selects the first entry of a good on each island of a set for some ticks.
    /// </summary>
    private const string TickGoodTrendSql = "SELECT t.\"Number\", e.\"Generation\", e.\"Consumption\", e.\"Delta\" "
                                            + "FROM \"Ticks\" t "
                                            + "JOIN \"TickIslands\" ti ON ti.\"TickNumber\" = t.\"Number\" "
                                            + "JOIN \"TickEntries\" e ON e.\"Id\" = (SELECT x.\"Id\" FROM \"TickEntries\" x WHERE x.\"TickIslandId\" = ti.\"Id\" AND x.\"ProductGuid\" = $product ORDER BY x.\"Position\" LIMIT 1) "
                                            + "WHERE t.\"ProfileId\" = $profile AND t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) AND " + TickIslandKeySql + " IN (SELECT \"value\" FROM json_each($islands)) "
                                            + "ORDER BY t.\"Number\", ti.\"SessionGuid\", ti.\"IslandId\", ti.\"AreaIndex\";";

    /// <summary>
    /// The statement that selects the entry of a good on each island of a set for some hourly points.
    /// </summary>
    private const string HourlyGoodTrendSql = "SELECT p.\"Number\", e.\"GenerationSum\", e.\"GenerationCount\", e.\"ConsumptionSum\", e.\"ConsumptionCount\", e.\"DeltaSum\", e.\"DeltaCount\" "
                                              + "FROM \"HourlyPoints\" p "
                                              + "JOIN \"HourlyIslands\" hi ON hi.\"PointNumber\" = p.\"Number\" "
                                              + "JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = hi.\"Id\" AND e.\"ProductGuid\" = $product "
                                              + "WHERE p.\"ProfileId\" = $profile AND p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) AND " + HourlyIslandKeySql + " IN (SELECT \"value\" FROM json_each($islands)) "
                                              + "ORDER BY p.\"Number\", hi.\"SessionGuid\", hi.\"IslandId\", hi.\"AreaIndex\";";

    /// <summary>
    /// The statement that selects the entries of an island for some ticks.
    /// </summary>
    private const string TickFiguresTrendSql = "SELECT t.\"Number\", e.\"BuildingCount\", e.\"Delta\" "
                                               + "FROM \"Ticks\" t "
                                               + "JOIN \"TickIslands\" ti ON ti.\"TickNumber\" = t.\"Number\" AND ti.\"SessionGuid\" = $session AND ti.\"IslandId\" = $island AND ti.\"AreaIndex\" = $area "
                                               + "JOIN \"TickEntries\" e ON e.\"TickIslandId\" = ti.\"Id\" "
                                               + "WHERE t.\"ProfileId\" = $profile AND t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers));";

    /// <summary>
    /// The statement that selects the entries of an island for some hourly points.
    /// </summary>
    private const string HourlyFiguresTrendSql = "SELECT p.\"Number\", e.\"EntryCount\", e.\"BuildingCountSum\", e.\"DeltaSum\", e.\"DeltaCount\" "
                                                 + "FROM \"HourlyPoints\" p "
                                                 + "JOIN \"HourlyIslands\" hi ON hi.\"PointNumber\" = p.\"Number\" AND hi.\"SessionGuid\" = $session AND hi.\"IslandId\" = $island AND hi.\"AreaIndex\" = $area "
                                                 + "JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = hi.\"Id\" "
                                                 + "WHERE p.\"ProfileId\" = $profile AND p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers));";

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

                                          using var latestCommand = StoreAccess.CreateCommand(connection, LatestReceiveSql, (ProfileParameter, profileId));

                                          if (await latestCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not long latest)
                                          {
                                              return new HistoryResult(HistoryOutcome.NothingToDo, null, null);
                                          }

                                          var limit = Math.Min(cutoff, latest - (latest % TimeSpan.TicksPerHour));

                                          using var oldestCommand = StoreAccess.CreateCommand(connection, OldestTickSql, (ProfileParameter, profileId), ("$limit", limit));

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
        await _database.TrendGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await ClearDeletedGatedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _database.TrendGate.Release();
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
    /// Cuts the hours of a range, oldest first, into parts of at most the part size rows.
    /// </summary>
    /// <param name="hours">The hours with their tick counts and whether an hourly point exists.</param>
    /// <param name="partSize">The maximum number of rows per part.</param>
    /// <returns>The parts, each a list of hour slices.</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    internal static List<List<TrendHourSlice>> PlanHourParts(IReadOnlyList<(long HourTicks, int Ticks, bool WithHourly)> hours, int partSize)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

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
                                         (ProfileParameter, query.ProfileId),
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

    /// <summary>
    /// Reads the times of the newest points of a range and the number of all points of it.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <param name="maxPoints">The maximum number of points</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The times of the newest points, oldest first, the hourly point first on equal times, and the number of all points</returns>
    private static async Task<(List<TrendTime> Times, int Total)> ReadTimesAsync(SqliteConnection connection, Guid profileId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, int maxPoints, CancellationToken cancellationToken)
    {
        var times = new List<TrendTime>();
        var total = 0;

        foreach (var (isHourly, timesSql, countSql) in new[] { (true, HourlyTimesSql, HourlyCountSql), (false, TickTimesSql, TickCountSql) })
        {
            using var countCommand = CreateRangeCommand(connection, countSql, profileId, rangeStart, rangeEnd);

            total += Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);

            using var command = CreateRangeCommand(connection, timesSql, profileId, rangeStart, rangeEnd, ("$max", maxPoints));
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                times.Add(new TrendTime(isHourly, reader.GetInt64(0), reader.GetInt64(1)));
            }
        }

        times.Sort(static (left, right) =>
                          {
                              var byTime = left.Ticks.CompareTo(right.Ticks);

                              if (byTime != 0)
                              {
                                  return byTime;
                              }

                              var byKind = right.IsHourly.CompareTo(left.IsHourly);

                              return byKind != 0 ? byKind : left.Number.CompareTo(right.Number);
                          });

        if (times.Count > maxPoints)
        {
            times.RemoveRange(0, times.Count - maxPoints);
        }

        return (times, total);
    }

    /// <summary>
    /// Creates the command of a statement over the range of a profile.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <param name="parameters">Further parameters</param>
    /// <returns>The command</returns>
    private static SqliteCommand CreateRangeCommand(SqliteConnection connection, string sql, Guid profileId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, params (string Name, object Value)[] parameters)
    {
        return StoreAccess.CreateCommand(connection,
                                         sql,
                                         [(ProfileParameter, profileId), ("$start", rangeStart.UtcTicks), ("$end", rangeEnd.UtcTicks), .. parameters]);
    }

    /// <summary>
    /// Serializes the row numbers of the points of a part, of one kind, as the one JSON parameter of a statement.
    /// </summary>
    /// <param name="part">The points of the part</param>
    /// <param name="isHourly">Whether the numbers of the hourly points are wanted</param>
    /// <returns>The JSON array</returns>
    private static string SerializeNumbers(List<TrendTime> part, bool isHourly)
    {
        return JsonSerializer.Serialize(part.Where(time => time.IsHourly == isHourly).Select(time => time.Number).ToArray());
    }

    /// <summary>
    /// Serializes a set of islands as the one JSON parameter of a statement, each island as one number.
    /// </summary>
    /// <param name="islands">The islands</param>
    /// <returns>The JSON array</returns>
    private static string SerializeIslands(IReadOnlyList<HistoryIslandKey> islands)
    {
        return JsonSerializer.Serialize(islands.Select(island => ((long)island.SessionGuid * 65536) + (island.IslandId * 256L) + island.AreaIndex).ToArray());
    }

    /// <summary>
    /// Reads a float stored as its bit pattern.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <param name="ordinal">The column</param>
    /// <returns>The float widened to a double, or <see langword="null"/></returns>
    private static double? ReadFloat(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
                   ? null
                   : BitConverter.Int32BitsToSingle(reader.GetInt32(ordinal));
    }

    /// <summary>
    /// Adds a value to a sum, which has none until the first value.
    /// </summary>
    /// <param name="sum">The sum</param>
    /// <param name="value">The value, or <see langword="null"/> for none</param>
    /// <returns>The new sum</returns>
    private static double? Add(double? sum, double? value)
    {
        return value is null
                   ? sum
                   : (sum ?? 0d) + value.Value;
    }

    /// <summary>
    /// Reads the generation, consumption and delta of the points of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="part">The points of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points, in the order of the part</returns>
    private static async Task<List<HistoryGoodPoint>> ReadGoodPartAsync(SqliteConnection connection, HistoryGoodTrendQuery query, List<TrendTime> part, CancellationToken cancellationToken)
    {
        var islands = SerializeIslands(query.Islands);
        var ticks = new Dictionary<long, (double? Generation, double? Consumption, double? Delta)>();
        var hourly = new Dictionary<long, (double? Generation, double? Consumption, double? Delta)>();

        using (var command = StoreAccess.CreateCommand(connection, TickGoodTrendSql, (ProfileParameter, query.ProfileId), ("$product", query.ProductGuid), ("$numbers", SerializeNumbers(part, false)), ("$islands", islands)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var sums = ticks.GetValueOrDefault(reader.GetInt64(0));

                    ticks[reader.GetInt64(0)] = (Add(sums.Generation, ReadFloat(reader, 1)), Add(sums.Consumption, ReadFloat(reader, 2)), Add(sums.Delta, ReadFloat(reader, 3)));
                }
            }

        using (var command = StoreAccess.CreateCommand(connection, HourlyGoodTrendSql, (ProfileParameter, query.ProfileId), ("$product", query.ProductGuid), ("$numbers", SerializeNumbers(part, true)), ("$islands", islands)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var sums = hourly.GetValueOrDefault(reader.GetInt64(0));

                    hourly[reader.GetInt64(0)] = (Add(sums.Generation, Mean(reader, 1)), Add(sums.Consumption, Mean(reader, 3)), Add(sums.Delta, Mean(reader, 5)));
                }
            }

        return [
                   .. part.Select(time =>
                                  {
                                      var sums = (time.IsHourly ? hourly : ticks).GetValueOrDefault(time.Number);

                                      return new HistoryGoodPoint
                                             {
                                                 Time = new DateTimeOffset(time.Ticks, TimeSpan.Zero),
                                                 IsHourly = time.IsHourly,
                                                 Generation = sums.Generation,
                                                 Consumption = sums.Consumption,
                                                 Delta = sums.Delta
                                             };
                                  })
               ];
    }

    /// <summary>
    /// Reads the mean of an hourly value from its sum and its count.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <param name="ordinal">The column of the sum, followed by the column of the count</param>
    /// <returns>The mean, or <see langword="null"/> when no tick carried the value</returns>
    private static double? Mean(SqliteDataReader reader, int ordinal)
    {
        var count = reader.GetInt32(ordinal + 1);

        return count == 0
                   ? null
                   : reader.GetDouble(ordinal) / count;
    }

    /// <summary>
    /// Rounds a delta to two decimals as the pages do and tells whether it is a deficit.
    /// </summary>
    /// <param name="delta">The delta</param>
    /// <returns><see langword="true"/> when the rounded delta is below 0</returns>
    private static bool IsDeficit(double delta)
    {
        return Math.Round(delta, 2, MidpointRounding.AwayFromZero) < 0;
    }

    /// <summary>
    /// Reads the key figures of an island for the points of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="part">The points of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points, in the order of the part</returns>
    private static async Task<List<HistoryFiguresPoint>> ReadFiguresPartAsync(SqliteConnection connection, HistoryFiguresTrendQuery query, List<TrendTime> part, CancellationToken cancellationToken)
    {
        var ticks = await ReadTickFiguresAsync(connection, query, part, cancellationToken).ConfigureAwait(false);
        var hourly = await ReadHourlyFiguresAsync(connection, query, part, cancellationToken).ConfigureAwait(false);

        return [
                   .. part.Select(time =>
                                  {
                                      var found = (time.IsHourly ? hourly : ticks).TryGetValue(time.Number, out var sums);

                                      return new HistoryFiguresPoint
                                             {
                                                 Time = new DateTimeOffset(time.Ticks, TimeSpan.Zero),
                                                 IsHourly = time.IsHourly,
                                                 Buildings = found ? (long)Math.Round(sums.Buildings, MidpointRounding.AwayFromZero) : null,
                                                 ProducedGoods = found ? sums.Produced : null,
                                                 GoodsInDeficit = found ? sums.Deficit : null
                                             };
                                  })
               ];
    }

    /// <summary>
    /// Reads the key figures of an island of the ticks of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="part">The points of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The buildings, produced goods and goods in deficit by tick row number; a tick without entries of the island is missing</returns>
    private static async Task<Dictionary<long, (double Buildings, int Produced, int Deficit)>> ReadTickFiguresAsync(SqliteConnection connection, HistoryFiguresTrendQuery query, List<TrendTime> part, CancellationToken cancellationToken)
    {
        var ticks = new Dictionary<long, (double Buildings, int Produced, int Deficit)>();

        using var command = CreateFiguresCommand(connection, TickFiguresTrendSql, query, SerializeNumbers(part, false));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var buildingCount = reader.GetInt32(1);
            var delta = ReadFloat(reader, 2);
            var sums = ticks.GetValueOrDefault(reader.GetInt64(0));

            ticks[reader.GetInt64(0)] = (sums.Buildings + buildingCount, sums.Produced + (buildingCount > 0 ? 1 : 0), sums.Deficit + (delta is { } value && IsDeficit(value) ? 1 : 0));
        }

        return ticks;
    }

    /// <summary>
    /// Reads the key figures of an island of the hourly points of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="part">The points of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The sum of the mean building counts, the produced goods and the goods in deficit by hourly point row number; a point without entries of the island is missing</returns>
    private static async Task<Dictionary<long, (double Buildings, int Produced, int Deficit)>> ReadHourlyFiguresAsync(SqliteConnection connection, HistoryFiguresTrendQuery query, List<TrendTime> part, CancellationToken cancellationToken)
    {
        var hourly = new Dictionary<long, (double Buildings, int Produced, int Deficit)>();

        using var command = CreateFiguresCommand(connection, HourlyFiguresTrendSql, query, SerializeNumbers(part, true));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var buildingSum = reader.GetInt64(2);
            var deltaCount = reader.GetInt32(4);
            var sums = hourly.GetValueOrDefault(reader.GetInt64(0));

            hourly[reader.GetInt64(0)] = (sums.Buildings + (buildingSum / (double)reader.GetInt32(1)),
                                          sums.Produced + (buildingSum > 0 ? 1 : 0),
                                          sums.Deficit + (deltaCount > 0 && IsDeficit(reader.GetDouble(3) / deltaCount) ? 1 : 0));
        }

        return hourly;
    }

    /// <summary>
    /// Creates the command of a figures statement.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="query">The query</param>
    /// <param name="numbers">The JSON array of the row numbers</param>
    /// <returns>The command</returns>
    private static SqliteCommand CreateFiguresCommand(SqliteConnection connection, string sql, HistoryFiguresTrendQuery query, string numbers)
    {
        return StoreAccess.CreateCommand(connection,
                                         sql,
                                         (ProfileParameter, query.ProfileId),
                                         ("$session", query.Island.SessionGuid),
                                         ("$island", (int)query.Island.IslandId),
                                         ("$area", (int)query.Island.AreaIndex),
                                         ("$numbers", numbers));
    }

    /// <summary>
    /// Checkpoints and truncates the write-ahead log while the trend gate is held.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the clean-up</returns>
    private async Task<HistoryResult> ClearDeletedGatedAsync(CancellationToken cancellationToken)
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
    /// Runs one transaction of a trend read: under the trend gate, so the clean-up never overlaps it, on a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="read">The read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the read</returns>
    private async Task<T> ReadTrendTransactionAsync<T>(Func<SqliteConnection, Task<T>> read, CancellationToken cancellationToken)
    {
        await _database.TrendGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await StoreAccess.ReadAsync(_database,
                                               async connection =>
                                               {
#pragma warning disable S6966 // The async overload has no deferred form and begins an immediate (write) transaction
                                                   using var transaction = connection.BeginTransaction(true);
#pragma warning restore S6966

                                                   return await read(connection).ConfigureAwait(false);
                                               },
                                               cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _database.TrendGate.Release();
        }
    }

    /// <summary>
    /// Reads a trend: the times of the newest points and their count in one transaction, then the values in parts.
    /// </summary>
    /// <typeparam name="TPoint">The type of a point</typeparam>
    /// <param name="profileId">The profile identity</param>
    /// <param name="rangeStart">The range start, inclusive</param>
    /// <param name="rangeEnd">The range end, exclusive</param>
    /// <param name="maxPoints">The maximum number of points</param>
    /// <param name="readPart">Reads the points of one part, in the order of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The newest points in time order and the number of all points of the range</returns>
    private async Task<HistoryTrend<TPoint>> ReadTrendAsync<TPoint>(Guid profileId,
                                                                    DateTimeOffset rangeStart,
                                                                    DateTimeOffset rangeEnd,
                                                                    int maxPoints,
                                                                    Func<SqliteConnection, List<TrendTime>, Task<List<TPoint>>> readPart,
                                                                    CancellationToken cancellationToken)
    {
        var (times, total) = await ReadTrendTransactionAsync(connection => ReadTimesAsync(connection, profileId, rangeStart, rangeEnd, Math.Max(0, maxPoints), cancellationToken), cancellationToken).ConfigureAwait(false);

        var points = new List<TPoint>(times.Count);

        for (var offset = 0; offset < times.Count; offset += TrendPartSize)
        {
            var part = times.GetRange(offset, Math.Min(TrendPartSize, times.Count - offset));

            points.AddRange(await ReadTrendTransactionAsync(connection => readPart(connection, part), cancellationToken).ConfigureAwait(false));
        }

        return new HistoryTrend<TPoint>
               {
                   Points = points,
                   TotalCount = total
               };
    }

    #endregion // Methods

    #region IHistoryTrends

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken)
    {
        return ReadTrendAsync(query.ProfileId,
                              query.RangeStart,
                              query.RangeEnd,
                              query.MaxPoints,
                              (connection, part) => ReadGoodPartAsync(connection, query, part, cancellationToken),
                              cancellationToken);
    }

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken)
    {
        return ReadTrendAsync(query.ProfileId,
                              query.RangeStart,
                              query.RangeEnd,
                              query.MaxPoints,
                              (connection, part) => ReadFiguresPartAsync(connection, query, part, cancellationToken),
                              cancellationToken);
    }

    #endregion // IHistoryTrends
}