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
    /// The start of the statements over the ticks.
    /// </summary>
    private const string FromTicksSql = "FROM \"Ticks\" t ";

    /// <summary>
    /// The start of the statements over the hourly points.
    /// </summary>
    private const string FromHourlyPointsSql = "FROM \"HourlyPoints\" p ";

    /// <summary>
    /// The condition tail that restricts an island key to the set of the islands parameter.
    /// </summary>
    private const string IslandsInSql = " IN (SELECT \"value\" FROM json_each($islands)) ";

    /// <summary>
    /// The name of the parameter of the islands of the statements.
    /// </summary>
    private const string IslandsParameter = "$islands";

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
                                           + FromHourlyPointsSql
                                           + "LEFT JOIN \"HourlyIslands\" i ON i.\"PointNumber\" = p.\"Number\" AND i.\"SessionGuid\" = $session AND i.\"IslandId\" = $island AND i.\"AreaIndex\" = $area "
                                           + "LEFT JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = i.\"Id\" AND e.\"ProductGuid\" = $product "
                                           + "WHERE p.\"ProfileId\" = $profile AND p.\"HourTicks\" >= $start AND p.\"HourTicks\" < $end ORDER BY p.\"HourTicks\";";

    /// <summary>
    /// The statement that selects the ticks of a series with the island and the first entry of the good of the series.
    /// </summary>
    private const string TickSeriesSql = "SELECT t.\"Number\", t.\"ReceiveTicks\", t.\"GameTime\", i.\"Name\", e.\"Id\", "
                                         + "e.\"Generation\", e.\"Consumption\", e.\"Delta\", e.\"PerfectGeneration\", e.\"PerfectConsumption\", e.\"Income\", e.\"SummedProductivity\", e.\"AverageProductivity\", "
                                         + "e.\"BuildingCount\", e.\"Maintenance\", e.\"Profit\" "
                                         + FromTicksSql
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
    /// The name of the product parameter of the statements.
    /// </summary>
    private const string ProductParameter = "$product";

    /// <summary>
    /// The name of the parameter of the row numbers of the statements.
    /// </summary>
    private const string NumbersParameter = "$numbers";

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
    private const string TickGoodTrendSql = "SELECT t.\"Number\", e.\"Generation\", e.\"Consumption\", e.\"Delta\" " + TickGoodFromSql + ";";

    /// <summary>
    /// The statement that selects the first entry of a good on each island of a set for some ticks, with the island key.
    /// </summary>
    private const string TickGoodHourSql = "SELECT t.\"Number\", e.\"Generation\", e.\"Consumption\", e.\"Delta\", " + TickIslandKeySql + " " + TickGoodFromSql + ";";

    /// <summary>
    /// The statement that selects the entry of a good on each island of a set for some hourly points.
    /// </summary>
    private const string HourlyGoodTrendSql = "SELECT p.\"Number\", e.\"GenerationSum\", e.\"GenerationCount\", e.\"ConsumptionSum\", e.\"ConsumptionCount\", e.\"DeltaSum\", e.\"DeltaCount\" " + HourlyGoodFromSql + ";";

    /// <summary>
    /// The statement that selects the entry of a good on each island of a set for some hourly points, with the island key.
    /// </summary>
    private const string HourlyGoodHourSql = "SELECT p.\"Number\", e.\"GenerationSum\", e.\"GenerationCount\", e.\"ConsumptionSum\", e.\"ConsumptionCount\", e.\"DeltaSum\", e.\"DeltaCount\", " + HourlyIslandKeySql + " " + HourlyGoodFromSql + ";";

    /// <summary>
    /// The statement that selects the entries of an island for some ticks.
    /// </summary>
    private const string TickFiguresTrendSql = "SELECT t.\"Number\", e.\"BuildingCount\", e.\"Delta\" " + TickFiguresFromSql + ";";

    /// <summary>
    /// The statement that selects the entries of an island for some ticks with their good, in the order of the entries.
    /// </summary>
    private const string TickFiguresHourSql = "SELECT t.\"Number\", e.\"BuildingCount\", e.\"Delta\", e.\"ProductGuid\" " + TickFiguresFromSql + " ORDER BY t.\"Number\", e.\"Position\";";

    /// <summary>
    /// The statement that selects the entries of an island for some hourly points.
    /// </summary>
    private const string HourlyFiguresTrendSql = "SELECT p.\"Number\", e.\"EntryCount\", e.\"BuildingCountSum\", e.\"DeltaSum\", e.\"DeltaCount\" " + HourlyFiguresFromSql + ";";

    /// <summary>
    /// The statement that selects the entries of an island for some hourly points with their good.
    /// </summary>
    private const string HourlyFiguresHourSql = "SELECT p.\"Number\", e.\"EntryCount\", e.\"BuildingCountSum\", e.\"DeltaSum\", e.\"DeltaCount\", e.\"ProductGuid\" " + HourlyFiguresFromSql + ";";

    /// <summary>
    /// The statement that selects the entries of some goods of an island for some ticks, in the order of the entries.
    /// </summary>
    private const string TickGoodsTrendSql = "SELECT t.\"Number\", e.\"ProductGuid\", e.\"Delta\" "
                                             + FromTicksSql
                                             + "JOIN \"TickIslands\" ti ON ti.\"TickNumber\" = t.\"Number\" AND ti.\"SessionGuid\" = $session AND ti.\"IslandId\" = $island AND ti.\"AreaIndex\" = $area "
                                             + "JOIN \"TickEntries\" e ON e.\"TickIslandId\" = ti.\"Id\" AND e.\"ProductGuid\" IN (SELECT \"value\" FROM json_each($products)) "
                                             + "WHERE t.\"ProfileId\" = $profile AND t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) "
                                             + "ORDER BY t.\"Number\", e.\"Position\";";

    /// <summary>
    /// The statement that selects the entries of some goods of an island for some hourly points.
    /// </summary>
    private const string HourlyGoodsTrendSql = "SELECT p.\"Number\", e.\"ProductGuid\", e.\"DeltaSum\", e.\"DeltaCount\" "
                                               + FromHourlyPointsSql
                                               + "JOIN \"HourlyIslands\" hi ON hi.\"PointNumber\" = p.\"Number\" AND hi.\"SessionGuid\" = $session AND hi.\"IslandId\" = $island AND hi.\"AreaIndex\" = $area "
                                               + "JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = hi.\"Id\" AND e.\"ProductGuid\" IN (SELECT \"value\" FROM json_each($products)) "
                                               + "WHERE p.\"ProfileId\" = $profile AND p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers));";

    /// <summary>
    /// The statement that selects the sum of the building counts of each island of a set for some ticks.
    /// </summary>
    private const string TickIslandsTrendSql = "SELECT t.\"Number\", " + TickIslandKeySql + ", sum(e.\"BuildingCount\") "
                                               + FromTicksSql
                                               + "JOIN \"TickIslands\" ti ON ti.\"TickNumber\" = t.\"Number\" "
                                               + "JOIN \"TickEntries\" e ON e.\"TickIslandId\" = ti.\"Id\" "
                                               + "WHERE t.\"ProfileId\" = $profile AND t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) AND " + TickIslandKeySql + IslandsInSql
                                               + "GROUP BY t.\"Number\", ti.\"Id\";";

    /// <summary>
    /// The statement that selects the sum of the mean building counts of each island of a set for some hourly points.
    /// </summary>
    private const string HourlyIslandsTrendSql = "SELECT p.\"Number\", " + HourlyIslandKeySql + ", sum(e.\"BuildingCountSum\" / CAST(e.\"EntryCount\" AS REAL)) "
                                                 + FromHourlyPointsSql
                                                 + "JOIN \"HourlyIslands\" hi ON hi.\"PointNumber\" = p.\"Number\" "
                                                 + "JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = hi.\"Id\" "
                                                 + "WHERE p.\"ProfileId\" = $profile AND p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) AND " + HourlyIslandKeySql + IslandsInSql
                                                 + "GROUP BY p.\"Number\", hi.\"Id\";";

    /// <summary>
    /// The expression that tells whether an island of a tick holds an entry.
    /// </summary>
    private const string TickHasEntrySql = "EXISTS (SELECT 1 FROM \"TickIslands\" xi JOIN \"TickEntries\" xe ON xe.\"TickIslandId\" = xi.\"Id\" WHERE xi.\"TickNumber\" = t.\"Number\")";

    /// <summary>
    /// The expression that tells whether an island of an hourly point holds an entry.
    /// </summary>
    private const string HourlyHasEntrySql = "EXISTS (SELECT 1 FROM \"HourlyIslands\" xi JOIN \"HourlyEntries\" xe ON xe.\"HourlyIslandId\" = xi.\"Id\" WHERE xi.\"PointNumber\" = p.\"Number\")";

    /// <summary>
    /// The statement that selects the game time of some ticks and whether an island of each holds an entry.
    /// </summary>
    private const string TickGameTimesSql = "SELECT t.\"Number\", t.\"GameTime\", " + TickHasEntrySql + " FROM \"Ticks\" t WHERE t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers));";

    /// <summary>
    /// The statement that selects the game times of some hourly points and whether an island of each holds an entry.
    /// </summary>
    private const string HourlyGameTimesSql = "SELECT p.\"Number\", p.\"FirstGameTime\", p.\"LastGameTime\", " + HourlyHasEntrySql + " FROM \"HourlyPoints\" p WHERE p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers));";

    /// <summary>
    /// The statement that selects the newest hours of a range that hold an hourly point.
    /// </summary>
    private const string HourlyHoursSql = "SELECT \"HourTicks\" FROM \"HourlyPoints\" WHERE \"ProfileId\" = $profile AND \"HourTicks\" >= $start AND \"HourTicks\" < $end ORDER BY \"HourTicks\" DESC LIMIT $max;";

    /// <summary>
    /// The statement that selects the newest hours of a range that hold a tick, with the number of ticks of each.
    /// </summary>
    private const string TickHoursSql = "SELECT (\"ReceiveTicks\" / $hour) * $hour AS \"Hour\", count(*) FROM \"Ticks\" WHERE \"ProfileId\" = $profile AND \"ReceiveTicks\" >= $start AND \"ReceiveTicks\" < $end GROUP BY \"Hour\" ORDER BY \"Hour\" DESC LIMIT $max;";

    /// <summary>
    /// The statement that counts the hours of a range that hold a tick or an hourly point.
    /// </summary>
    private const string HourCountSql = "SELECT count(*) FROM (SELECT \"HourTicks\" AS \"Hour\" FROM \"HourlyPoints\" WHERE \"ProfileId\" = $profile AND \"HourTicks\" >= $start AND \"HourTicks\" < $end "
                                        + "UNION SELECT (\"ReceiveTicks\" / $hour) * $hour FROM \"Ticks\" WHERE \"ProfileId\" = $profile AND \"ReceiveTicks\" >= $start AND \"ReceiveTicks\" < $end);";

    /// <summary>
    /// The statement that selects the next ticks of a range of receive times, after a tick, with their game time and whether an island holds an entry.
    /// </summary>
    private const string TickSliceSql = "SELECT t.\"Number\", t.\"ReceiveTicks\", t.\"GameTime\", " + TickHasEntrySql + " FROM \"Ticks\" t "
                                        + "WHERE t.\"ProfileId\" = $profile AND t.\"ReceiveTicks\" >= $from AND t.\"ReceiveTicks\" < $to AND (t.\"ReceiveTicks\", t.\"Number\") > ($lastReceive, $lastNumber) "
                                        + "ORDER BY t.\"ReceiveTicks\", t.\"Number\" LIMIT $limit;";

    /// <summary>
    /// The statement that selects the hourly points of some hours with their game times and whether an island holds an entry.
    /// </summary>
    private const string HourlyHeadsSql = "SELECT p.\"Number\", p.\"HourTicks\", p.\"FirstGameTime\", p.\"LastGameTime\", " + HourlyHasEntrySql + " FROM \"HourlyPoints\" p WHERE p.\"ProfileId\" = $profile AND p.\"HourTicks\" IN (SELECT \"value\" FROM json_each($hours));";

    /// <summary>
    /// The statement that selects the entry of a good on each island of a set for some ticks, without the columns.
    /// </summary>
    private const string TickGoodFromSql = FromTicksSql
                                           + "JOIN \"TickIslands\" ti ON ti.\"TickNumber\" = t.\"Number\" "
                                           + "JOIN \"TickEntries\" e ON e.\"Id\" = (SELECT x.\"Id\" FROM \"TickEntries\" x WHERE x.\"TickIslandId\" = ti.\"Id\" AND x.\"ProductGuid\" = $product ORDER BY x.\"Position\" LIMIT 1) "
                                           + "WHERE t.\"ProfileId\" = $profile AND t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) AND " + TickIslandKeySql + IslandsInSql
                                           + "ORDER BY t.\"Number\", ti.\"SessionGuid\", ti.\"IslandId\", ti.\"AreaIndex\"";

    /// <summary>
    /// The statement that selects the entry of a good on each island of a set for some hourly points, without the columns.
    /// </summary>
    private const string HourlyGoodFromSql = FromHourlyPointsSql
                                             + "JOIN \"HourlyIslands\" hi ON hi.\"PointNumber\" = p.\"Number\" "
                                             + "JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = hi.\"Id\" AND e.\"ProductGuid\" = $product "
                                             + "WHERE p.\"ProfileId\" = $profile AND p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers)) AND " + HourlyIslandKeySql + IslandsInSql
                                             + "ORDER BY p.\"Number\", hi.\"SessionGuid\", hi.\"IslandId\", hi.\"AreaIndex\"";

    /// <summary>
    /// The statement that selects the entries of an island for some ticks, without the columns.
    /// </summary>
    private const string TickFiguresFromSql = FromTicksSql
                                              + "JOIN \"TickIslands\" ti ON ti.\"TickNumber\" = t.\"Number\" AND ti.\"SessionGuid\" = $session AND ti.\"IslandId\" = $island AND ti.\"AreaIndex\" = $area "
                                              + "JOIN \"TickEntries\" e ON e.\"TickIslandId\" = ti.\"Id\" "
                                              + "WHERE t.\"ProfileId\" = $profile AND t.\"Number\" IN (SELECT \"value\" FROM json_each($numbers))";

    /// <summary>
    /// The statement that selects the entries of an island for some hourly points, without the columns.
    /// </summary>
    private const string HourlyFiguresFromSql = FromHourlyPointsSql
                                                + "JOIN \"HourlyIslands\" hi ON hi.\"PointNumber\" = p.\"Number\" AND hi.\"SessionGuid\" = $session AND hi.\"IslandId\" = $island AND hi.\"AreaIndex\" = $area "
                                                + "JOIN \"HourlyEntries\" e ON e.\"HourlyIslandId\" = hi.\"Id\" "
                                                + "WHERE p.\"ProfileId\" = $profile AND p.\"Number\" IN (SELECT \"value\" FROM json_each($numbers))";

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
    /// Cuts the points and islands of an islands trend read into parts of at most the part size island points.
    /// </summary>
    /// <param name="pointCount">The number of points.</param>
    /// <param name="islandCount">The number of islands.</param>
    /// <param name="partSize">The maximum number of island points per part.</param>
    /// <returns>The parts, each a block of points and islands.</returns>
    internal static List<TrendIslandSlice> PlanIslandParts(int pointCount, int islandCount, int partSize)
    {
        var parts = new List<TrendIslandSlice>();

        if (pointCount < 1 || islandCount < 1 || partSize < 1)
        {
            return parts;
        }

        for (var islandStart = 0; islandStart < islandCount; islandStart += partSize)
        {
            var islands = Math.Min(partSize, islandCount - islandStart);
            var points = Math.Max(1, partSize / islands);

            for (var pointStart = 0; pointStart < pointCount; pointStart += points)
            {
                parts.Add(new TrendIslandSlice(pointStart, Math.Min(points, pointCount - pointStart), islandStart, islands));
            }
        }

        return parts;
    }

    /// <summary>
    /// Cuts the hours of a range, oldest first, into parts of at most the part size rows.
    /// </summary>
    /// <param name="hours">The hours with their tick counts and whether an hourly point exists.</param>
    /// <param name="partSize">The maximum number of rows per part.</param>
    /// <returns>The parts, each a list of hour slices.</returns>
    internal static List<List<TrendHourSlice>> PlanHourParts(IReadOnlyList<(long HourTicks, int Ticks, bool WithHourly)> hours, int partSize)
    {
        var size = Math.Max(1, partSize);
        var parts = new List<List<TrendHourSlice>>();
        var part = new List<TrendHourSlice>();
        var room = size;

        foreach (var (hourTicks, ticks, withHourly) in hours)
        {
            var remaining = ticks;
            var hourly = withHourly;

            while (remaining > 0 || hourly)
            {
                var taken = Math.Min(remaining, room - (hourly ? 1 : 0));

                part.Add(new TrendHourSlice(hourTicks, taken, hourly));

                room -= taken + (hourly ? 1 : 0);
                remaining -= taken;
                hourly = false;

                if (room == 0)
                {
                    parts.Add(part);
                    part = [];
                    room = size;
                }
            }
        }

        if (part.Count > 0)
        {
            parts.Add(part);
        }

        return parts;
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
                                         (ProfileParameter, query.ProfileId),
                                         ("$session", query.SessionGuid),
                                         ("$island", (int)query.IslandId),
                                         ("$area", (int)query.AreaIndex),
                                         (ProductParameter, query.ProductGuid),
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
        var gameTimes = await ReadGameTimesAsync(connection, part, cancellationToken).ConfigureAwait(false);
        var ticks = new Dictionary<long, (double? Generation, double? Consumption, double? Delta)>();
        var hourly = new Dictionary<long, (double? Generation, double? Consumption, double? Delta)>();

        using (var command = StoreAccess.CreateCommand(connection, TickGoodTrendSql, (ProfileParameter, query.ProfileId), (ProductParameter, query.ProductGuid), (NumbersParameter, SerializeNumbers(part, false)), (IslandsParameter, islands)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var sums = ticks.GetValueOrDefault(reader.GetInt64(0));

                    ticks[reader.GetInt64(0)] = (Add(sums.Generation, ReadFloat(reader, 1)), Add(sums.Consumption, ReadFloat(reader, 2)), Add(sums.Delta, ReadFloat(reader, 3)));
                }
            }

        using (var command = StoreAccess.CreateCommand(connection, HourlyGoodTrendSql, (ProfileParameter, query.ProfileId), (ProductParameter, query.ProductGuid), (NumbersParameter, SerializeNumbers(part, true)), (IslandsParameter, islands)))
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
                                      var found = gameTimes.TryGetValue((time.IsHourly, time.Number), out var head);

                                      return new HistoryGoodPoint
                                             {
                                                 Time = new DateTimeOffset(time.Ticks, TimeSpan.Zero),
                                                 IsHourly = time.IsHourly,
                                                 Generation = sums.Generation,
                                                 Consumption = sums.Consumption,
                                                 Delta = sums.Delta,
                                                 GameTime = found ? head.Last : null,
                                                 FirstGameTime = found ? head.First : null,
                                                 HasNoEntries = found && head.HasEntries == false
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
        var gameTimes = await ReadGameTimesAsync(connection, part, cancellationToken).ConfigureAwait(false);

        return [
                   .. part.Select(time =>
                                  {
                                      var found = (time.IsHourly ? hourly : ticks).TryGetValue(time.Number, out var sums);
                                      var headFound = gameTimes.TryGetValue((time.IsHourly, time.Number), out var head);

                                      return new HistoryFiguresPoint
                                             {
                                                 Time = new DateTimeOffset(time.Ticks, TimeSpan.Zero),
                                                 IsHourly = time.IsHourly,
                                                 Buildings = found ? (long)Math.Round(sums.Buildings, MidpointRounding.AwayFromZero) : null,
                                                 ProducedGoods = found ? sums.Produced : null,
                                                 GoodsInDeficit = found ? sums.Deficit : null,
                                                 GameTime = headFound ? head.Last : null,
                                                 FirstGameTime = headFound ? head.First : null,
                                                 HasNoEntries = headFound && head.HasEntries == false
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
                                         (NumbersParameter, numbers));
    }

    /// <summary>
    /// Reads the game times of the points of a part and whether an island of each holds an entry.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="part">The points of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The smallest and largest game time and the entry flag by kind and row number; a tick has the same game time twice</returns>
    private static async Task<Dictionary<(bool IsHourly, long Number), (long First, long Last, bool HasEntries)>> ReadGameTimesAsync(SqliteConnection connection, List<TrendTime> part, CancellationToken cancellationToken)
    {
        var heads = new Dictionary<(bool IsHourly, long Number), (long First, long Last, bool HasEntries)>();

        foreach (var (isHourly, sql) in new[] { (false, TickGameTimesSql), (true, HourlyGameTimesSql) })
        {
            using var command = StoreAccess.CreateCommand(connection, sql, (NumbersParameter, SerializeNumbers(part, isHourly)));
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var first = reader.GetInt64(1);

                heads[(isHourly, reader.GetInt64(0))] = isHourly
                                                            ? (first, reader.GetInt64(2), reader.GetInt64(3) != 0)
                                                            : (first, first, reader.GetInt64(2) != 0);
            }
        }

        return heads;
    }

    /// <summary>
    /// Reads the newest hours of a range that hold a tick or an hourly point, and the number of all such hours.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="range">The range start, inclusive, and end, exclusive</param>
    /// <param name="maxPoints">The maximum number of hours</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The newest hours, oldest first, with their tick counts and whether an hourly point exists, and the number of all hours</returns>
    private static async Task<(List<(long HourTicks, int Ticks, bool WithHourly)> Hours, int Total)> ReadHourTimesAsync(SqliteConnection connection, Guid profileId, (DateTimeOffset Start, DateTimeOffset End) range, int maxPoints, CancellationToken cancellationToken)
    {
        var found = new Dictionary<long, (int Ticks, bool WithHourly)>();

        using var countCommand = CreateRangeCommand(connection, HourCountSql, profileId, range.Start, range.End, ("$hour", TimeSpan.TicksPerHour));

        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);

        using (var command = CreateRangeCommand(connection, HourlyHoursSql, profileId, range.Start, range.End, ("$max", maxPoints)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    found[reader.GetInt64(0)] = (0, true);
                }
            }

        using (var command = CreateRangeCommand(connection, TickHoursSql, profileId, range.Start, range.End, ("$hour", TimeSpan.TicksPerHour), ("$max", maxPoints)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var hour = reader.GetInt64(0);

                    found[hour] = (reader.GetInt32(1), found.TryGetValue(hour, out var existing) && existing.WithHourly);
                }
            }

        return ([.. found.OrderByDescending(pair => pair.Key).Take(maxPoints).OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Ticks, pair.Value.WithHourly))], total);
    }

    /// <summary>
    /// Reads the hourly points of the hours of a part, with their game times and whether an island holds an entry.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="part">The slices of the part</param>
    /// <param name="totalsByHour">The totals of the hours of the part</param>
    /// <param name="owners">Receives the totals of each hourly point by its row number</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task ReadHourlyHeadsAsync(SqliteConnection connection, Guid profileId, List<TrendHourSlice> part, Dictionary<long, HourTotals> totalsByHour, Dictionary<long, HourTotals> owners, CancellationToken cancellationToken)
    {
        var hours = JsonSerializer.Serialize(part.Where(slice => slice.WithHourly).Select(slice => slice.HourTicks).ToArray());

        using var command = StoreAccess.CreateCommand(connection, HourlyHeadsSql, (ProfileParameter, profileId), ("$hours", hours));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var totals = totalsByHour[reader.GetInt64(1)];

            owners[reader.GetInt64(0)] = totals;
            totals.AddGameTimes(reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4) != 0);
        }
    }

    /// <summary>
    /// Reads the next ticks of an hour after the last one read, with their game times and whether an island holds an entry.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="range">The range start, inclusive, and end, exclusive</param>
    /// <param name="slice">The slice of the hour to read</param>
    /// <param name="totals">The totals of the hour, which take the game times and the position of the last tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The row numbers of the ticks read</returns>
    private static async Task<List<long>> ReadTickSliceAsync(SqliteConnection connection, Guid profileId, (DateTimeOffset Start, DateTimeOffset End) range, TrendHourSlice slice, HourTotals totals, CancellationToken cancellationToken)
    {
        var numbers = new List<long>(slice.Ticks);
        var from = Math.Max(slice.HourTicks, range.Start.UtcTicks);
        var to = Math.Min(slice.HourTicks + TimeSpan.TicksPerHour, range.End.UtcTicks);

        using var command = StoreAccess.CreateCommand(connection,
                                                      TickSliceSql,
                                                      (ProfileParameter, profileId),
                                                      ("$from", from),
                                                      ("$to", to),
                                                      ("$lastReceive", totals.LastReceiveTicks ?? from),
                                                      ("$lastNumber", totals.LastReceiveTicks is null ? -1L : totals.LastNumber),
                                                      ("$limit", slice.Ticks));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var gameTime = reader.GetInt64(2);

            numbers.Add(reader.GetInt64(0));
            totals.LastNumber = reader.GetInt64(0);
            totals.LastReceiveTicks = reader.GetInt64(1);
            totals.AddGameTimes(gameTime, gameTime, reader.GetInt64(3) != 0);
        }

        return numbers;
    }

    /// <summary>
    /// Reads one part of an hourly trend: the hourly points and ticks of its slices, their game times and their values.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="range">The range start, inclusive, and end, exclusive</param>
    /// <param name="part">The slices of the part</param>
    /// <param name="carry">The totals of the last hour of the part before, which this part may continue</param>
    /// <param name="accumulate">Adds the values of the hourly points and the ticks, given the totals by row number, to the totals</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The totals of the hours of the part, oldest first</returns>
    private static async Task<List<HourTotals>> ReadHourPartAsync(SqliteConnection connection,
                                                                  Guid profileId,
                                                                  (DateTimeOffset Start, DateTimeOffset End) range,
                                                                  List<TrendHourSlice> part,
                                                                  HourTotals? carry,
                                                                  Func<SqliteConnection, Dictionary<long, HourTotals>, Dictionary<long, HourTotals>, CancellationToken, Task> accumulate,
                                                                  CancellationToken cancellationToken)
    {
        var totalsByHour = new Dictionary<long, HourTotals>();
        var order = new List<HourTotals>();

        foreach (var hourTicks in part.Select(slice => slice.HourTicks).Distinct())
        {
            var totals = carry is not null && carry.HourTicks == hourTicks
                             ? carry
                             : new HourTotals(hourTicks);

            totalsByHour[hourTicks] = totals;
            order.Add(totals);
        }

        var pointOwners = new Dictionary<long, HourTotals>();
        var tickOwners = new Dictionary<long, HourTotals>();

        if (part.Any(slice => slice.WithHourly))
        {
            await ReadHourlyHeadsAsync(connection, profileId, part, totalsByHour, pointOwners, cancellationToken).ConfigureAwait(false);
        }

        foreach (var slice in part.Where(slice => slice.Ticks > 0))
        {
            var totals = totalsByHour[slice.HourTicks];

            foreach (var number in await ReadTickSliceAsync(connection, profileId, range, slice, totals, cancellationToken).ConfigureAwait(false))
            {
                tickOwners[number] = totals;
            }
        }

        await accumulate(connection, pointOwners, tickOwners, cancellationToken).ConfigureAwait(false);

        return order;
    }

    /// <summary>
    /// Adds the generation, consumption and delta of a good on the islands to the hours of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="pointOwners">The totals by row number of the hourly points of the part</param>
    /// <param name="tickOwners">The totals by row number of the ticks of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task AccumulateGoodAsync(SqliteConnection connection, HistoryGoodTrendQuery query, Dictionary<long, HourTotals> pointOwners, Dictionary<long, HourTotals> tickOwners, CancellationToken cancellationToken)
    {
        var islands = SerializeIslands(query.Islands);

        foreach (var (isHourly, sql, owners) in new[] { (true, HourlyGoodHourSql, pointOwners), (false, TickGoodHourSql, tickOwners) })
        {
            if (owners.Count == 0)
            {
                continue;
            }

            using var command = StoreAccess.CreateCommand(connection, sql, (ProfileParameter, query.ProfileId), (ProductParameter, query.ProductGuid), (NumbersParameter, JsonSerializer.Serialize(owners.Keys.ToArray())), (IslandsParameter, islands));
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var totals = owners[reader.GetInt64(0)];
                var islandKey = reader.GetInt64(isHourly ? 7 : 4);

                if (totals.Islands.TryGetValue(islandKey, out var sums) == false)
                {
                    sums = new HourIslandSums();
                    totals.Islands[islandKey] = sums;
                }

                for (var index = 0; index < 3; index++)
                {
                    if (isHourly)
                    {
                        sums.Add(index, reader.GetDouble(1 + (2 * index)), reader.GetInt32(2 + (2 * index)));
                    }
                    else if (ReadFloat(reader, 1 + index) is { } value)
                    {
                        sums.Add(index, value, 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds the entries of an island, the first of each good, to the hours of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="pointOwners">The totals by row number of the hourly points of the part</param>
    /// <param name="tickOwners">The totals by row number of the ticks of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private static async Task AccumulateFiguresAsync(SqliteConnection connection, HistoryFiguresTrendQuery query, Dictionary<long, HourTotals> pointOwners, Dictionary<long, HourTotals> tickOwners, CancellationToken cancellationToken)
    {
        foreach (var (isHourly, sql, owners) in new[] { (true, HourlyFiguresHourSql, pointOwners), (false, TickFiguresHourSql, tickOwners) })
        {
            if (owners.Count == 0)
            {
                continue;
            }

            using var command = CreateFiguresCommand(connection, sql, query, JsonSerializer.Serialize(owners.Keys.ToArray()));
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var seen = new HashSet<(long Number, int Product)>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var number = reader.GetInt64(0);
                var product = reader.GetInt32(isHourly ? 5 : 3);

                if (isHourly == false && seen.Add((number, product)) == false)
                {
                    continue;
                }

                var totals = owners[number];

                if (totals.Goods.TryGetValue(product, out var sums) == false)
                {
                    sums = new HourGoodSums();
                    totals.Goods[product] = sums;
                }

                if (isHourly)
                {
                    sums.EntryCount += reader.GetInt32(1);
                    sums.BuildingSum += reader.GetInt64(2);
                    sums.DeltaSum += reader.GetDouble(3);
                    sums.DeltaCount += reader.GetInt32(4);
                }
                else
                {
                    sums.EntryCount++;
                    sums.BuildingSum += reader.GetInt32(1);

                    if (ReadFloat(reader, 2) is { } delta)
                    {
                        sums.DeltaSum += delta;
                        sums.DeltaCount++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Makes the point of an hour of a good's trend, as the hourly point after the fold would read.
    /// </summary>
    /// <param name="totals">The totals of the hour</param>
    /// <returns>The point</returns>
    private static HistoryGoodPoint CreateGoodHour(HourTotals totals)
    {
        double? generation = null;
        double? consumption = null;
        double? delta = null;

        foreach (var sums in totals.Islands.Values)
        {
            generation = Add(generation, sums.Mean(0));
            consumption = Add(consumption, sums.Mean(1));
            delta = Add(delta, sums.Mean(2));
        }

        return new HistoryGoodPoint
               {
                   Time = new DateTimeOffset(totals.HourTicks, TimeSpan.Zero),
                   IsHourly = true,
                   Generation = generation,
                   Consumption = consumption,
                   Delta = delta,
                   GameTime = totals.LastGameTime,
                   FirstGameTime = totals.FirstGameTime,
                   HasNoEntries = totals.HasEntries == false
               };
    }

    /// <summary>
    /// Makes the point of an hour of an island's figures, as the hourly point after the fold would read.
    /// </summary>
    /// <param name="totals">The totals of the hour</param>
    /// <returns>The point</returns>
    private static HistoryFiguresPoint CreateFiguresHour(HourTotals totals)
    {
        var found = totals.Goods.Count > 0;
        var buildings = 0d;
        var produced = 0;
        var deficit = 0;

        foreach (var sums in totals.Goods.Values)
        {
            buildings += sums.BuildingSum / (double)sums.EntryCount;
            produced += sums.BuildingSum > 0 ? 1 : 0;
            deficit += sums.DeltaCount > 0 && IsDeficit(sums.DeltaSum / sums.DeltaCount) ? 1 : 0;
        }

        return new HistoryFiguresPoint
               {
                   Time = new DateTimeOffset(totals.HourTicks, TimeSpan.Zero),
                   IsHourly = true,
                   Buildings = found ? (long)Math.Round(buildings, MidpointRounding.AwayFromZero) : null,
                   ProducedGoods = found ? produced : null,
                   GoodsInDeficit = found ? deficit : null,
                   GameTime = totals.LastGameTime,
                   FirstGameTime = totals.FirstGameTime,
                   HasNoEntries = totals.HasEntries == false
               };
    }

    /// <summary>
    /// Reads the deltas of some goods of an island for the points of a part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="query">The query</param>
    /// <param name="part">The points of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points, in the order of the part, each with the deltas of the goods that have one</returns>
    private static async Task<List<HistoryGoodsPoint>> ReadGoodsPartAsync(SqliteConnection connection, HistoryGoodsTrendQuery query, List<TrendTime> part, CancellationToken cancellationToken)
    {
        var products = JsonSerializer.Serialize(query.ProductGuids);
        var ticks = new Dictionary<long, Dictionary<int, double>>();
        var seen = new HashSet<(long Number, int ProductGuid)>();
        var hourly = new Dictionary<long, Dictionary<int, double>>();

        using (var command = CreateGoodsCommand(connection, TickGoodsTrendSql, query, products, SerializeNumbers(part, false)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var number = reader.GetInt64(0);
                    var productGuid = reader.GetInt32(1);

                    if (seen.Add((number, productGuid)) && ReadFloat(reader, 2) is { } delta)
                    {
                        GetOrAddDeltas(ticks, number)[productGuid] = delta;
                    }
                }
            }

        using (var command = CreateGoodsCommand(connection, HourlyGoodsTrendSql, query, products, SerializeNumbers(part, true)))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (Mean(reader, 2) is { } delta)
                    {
                        GetOrAddDeltas(hourly, reader.GetInt64(0))[reader.GetInt32(1)] = delta;
                    }
                }
            }

        return [
                   .. part.Select(time => new HistoryGoodsPoint
                                          {
                                              Time = new DateTimeOffset(time.Ticks, TimeSpan.Zero),
                                              IsHourly = time.IsHourly,
                                              Deltas = (time.IsHourly ? hourly : ticks).GetValueOrDefault(time.Number) ?? []
                                          })
               ];
    }

    /// <summary>
    /// Gets the deltas of a point, adding an empty set for a point that has none yet.
    /// </summary>
    /// <param name="deltas">The deltas by row number</param>
    /// <param name="number">The row number</param>
    /// <returns>The deltas of the point</returns>
    private static Dictionary<int, double> GetOrAddDeltas(Dictionary<long, Dictionary<int, double>> deltas, long number)
    {
        if (deltas.TryGetValue(number, out var found) == false)
        {
            found = [];
            deltas[number] = found;
        }

        return found;
    }

    /// <summary>
    /// Creates the command of a goods statement.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="query">The query</param>
    /// <param name="products">The JSON array of the product GUIDs</param>
    /// <param name="numbers">The JSON array of the row numbers</param>
    /// <returns>The command</returns>
    private static SqliteCommand CreateGoodsCommand(SqliteConnection connection, string sql, HistoryGoodsTrendQuery query, string products, string numbers)
    {
        return StoreAccess.CreateCommand(connection,
                                         sql,
                                         (ProfileParameter, query.ProfileId),
                                         ("$session", query.Island.SessionGuid),
                                         ("$island", (int)query.Island.IslandId),
                                         ("$area", (int)query.Island.AreaIndex),
                                         ("$products", products),
                                         (NumbersParameter, numbers));
    }

    /// <summary>
    /// Reads the buildings of the islands of a part for the points of the part.
    /// </summary>
    /// <param name="connection">The opened connection inside a read transaction</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="points">The points of the part</param>
    /// <param name="islands">The islands of the part</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The buildings of each island with entries, by kind and row number of the point</returns>
    private static async Task<Dictionary<(bool IsHourly, long Number), Dictionary<HistoryIslandKey, long>>> ReadIslandsPartAsync(SqliteConnection connection,
                                                                                                                                 Guid profileId,
                                                                                                                                 List<TrendTime> points,
                                                                                                                                 List<HistoryIslandKey> islands,
                                                                                                                                 CancellationToken cancellationToken)
    {
        var keys = islands.ToDictionary(island => ((long)island.SessionGuid * 65536) + (island.IslandId * 256L) + island.AreaIndex);
        var serialized = SerializeIslands(islands);
        var sums = new Dictionary<(bool IsHourly, long Number), Dictionary<HistoryIslandKey, double>>();

        foreach (var (isHourly, sql) in new[] { (false, TickIslandsTrendSql), (true, HourlyIslandsTrendSql) })
        {
            var numbers = SerializeNumbers(points, isHourly);

            if (numbers == "[]")
            {
                continue;
            }

            using var command = StoreAccess.CreateCommand(connection, sql, (ProfileParameter, profileId), (NumbersParameter, numbers), (IslandsParameter, serialized));
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var at = (isHourly, reader.GetInt64(0));

                if (sums.TryGetValue(at, out var found) == false)
                {
                    found = [];
                    sums[at] = found;
                }

                found[keys[reader.GetInt64(1)]] = reader.GetDouble(2);
            }
        }

        return sums.ToDictionary(pair => pair.Key, pair => pair.Value.ToDictionary(island => island.Key, island => (long)Math.Round(island.Value, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// Reads a trend by hour: the hours with their tick counts in one transaction, then each part of at most the part size rows in a transaction of its own.
    /// </summary>
    /// <typeparam name="TPoint">The type of a point</typeparam>
    /// <param name="profileId">The profile identity</param>
    /// <param name="range">The range start, inclusive, and end, exclusive</param>
    /// <param name="maxPoints">The maximum number of hours</param>
    /// <param name="accumulate">Adds the values of the hourly points and the ticks of a part, given the totals by row number, to the totals</param>
    /// <param name="createPoint">Makes the point of an hour</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The newest hours in time order and the number of all hours of the range</returns>
    private async Task<HistoryTrend<TPoint>> ReadHoursAsync<TPoint>(Guid profileId,
                                                                    (DateTimeOffset Start, DateTimeOffset End) range,
                                                                    int maxPoints,
                                                                    Func<SqliteConnection, Dictionary<long, HourTotals>, Dictionary<long, HourTotals>, CancellationToken, Task> accumulate,
                                                                    Func<HourTotals, TPoint> createPoint,
                                                                    CancellationToken cancellationToken)
    {
        var (hours, total) = await ReadTrendTransactionAsync(connection => ReadHourTimesAsync(connection, profileId, range, Math.Max(0, maxPoints), cancellationToken), cancellationToken).ConfigureAwait(false);

        var points = new List<TPoint>(hours.Count);
        HourTotals? carry = null;

        foreach (var part in PlanHourParts(hours, TrendPartSize))
        {
            var before = carry;
            var touched = await ReadTrendTransactionAsync(connection => ReadHourPartAsync(connection, profileId, range, part, before, accumulate, cancellationToken), cancellationToken).ConfigureAwait(false);

            if (before is not null && ReferenceEquals(before, touched[0]) == false)
            {
                points.Add(createPoint(before));
            }

            points.AddRange(touched.Take(touched.Count - 1).Select(createPoint));
            carry = touched[^1];
        }

        if (carry is not null)
        {
            points.Add(createPoint(carry));
        }

        return new HistoryTrend<TPoint>
               {
                   Points = points,
                   TotalCount = total
               };
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

    /// <summary>
    /// Reads the buildings of a set of islands over the newest points of a range, in parts of at most the part size island points.
    /// </summary>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The newest points in time order and the number of all points of the range</returns>
    private async Task<HistoryTrend<HistoryIslandsPoint>> ReadIslandsAsync(HistoryIslandsTrendQuery query, CancellationToken cancellationToken)
    {
        var (times, total) = await ReadTrendTransactionAsync(connection => ReadTimesAsync(connection, query.ProfileId, query.RangeStart, query.RangeEnd, Math.Max(0, query.MaxPoints), cancellationToken), cancellationToken).ConfigureAwait(false);

        var buildings = times.Select(_ => new Dictionary<HistoryIslandKey, long>()).ToList();

        foreach (var slice in PlanIslandParts(times.Count, query.Islands.Count, TrendPartSize))
        {
            var points = times.GetRange(slice.PointStart, slice.PointCount);
            var islands = query.Islands.Skip(slice.IslandStart).Take(slice.IslandCount).ToList();
            var found = await ReadTrendTransactionAsync(connection => ReadIslandsPartAsync(connection, query.ProfileId, points, islands, cancellationToken), cancellationToken).ConfigureAwait(false);

            for (var index = 0; index < points.Count; index++)
            {
                if (found.TryGetValue((points[index].IsHourly, points[index].Number), out var island))
                {
                    foreach (var (key, count) in island)
                    {
                        buildings[slice.PointStart + index][key] = count;
                    }
                }
            }
        }

        return new HistoryTrend<HistoryIslandsPoint>
               {
                   Points = [
                                .. times.Select((time, index) => new HistoryIslandsPoint
                                                                 {
                                                                     Time = new DateTimeOffset(time.Ticks, TimeSpan.Zero),
                                                                     IsHourly = time.IsHourly,
                                                                     Buildings = buildings[index]
                                                                 })
                            ],
                   TotalCount = total
               };
    }

    #endregion // Methods

    #region IHistoryTrends

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken)
    {
        if (query.Resolution == HistoryTrendResolution.Hours)
        {
            return ReadHoursAsync(query.ProfileId,
                                  (query.RangeStart, query.RangeEnd),
                                  query.MaxPoints,
                                  (connection, pointOwners, tickOwners, token) => AccumulateGoodAsync(connection, query, pointOwners, tickOwners, token),
                                  CreateGoodHour,
                                  cancellationToken);
        }

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
        if (query.Resolution == HistoryTrendResolution.Hours)
        {
            return ReadHoursAsync(query.ProfileId,
                                  (query.RangeStart, query.RangeEnd),
                                  query.MaxPoints,
                                  (connection, pointOwners, tickOwners, token) => AccumulateFiguresAsync(connection, query, pointOwners, tickOwners, token),
                                  CreateFiguresHour,
                                  cancellationToken);
        }

        return ReadTrendAsync(query.ProfileId,
                              query.RangeStart,
                              query.RangeEnd,
                              query.MaxPoints,
                              (connection, part) => ReadFiguresPartAsync(connection, query, part, cancellationToken),
                              cancellationToken);
    }

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryGoodsPoint>> ReadGoodsTrendAsync(HistoryGoodsTrendQuery query, CancellationToken cancellationToken)
    {
        return ReadTrendAsync(query.ProfileId,
                              query.RangeStart,
                              query.RangeEnd,
                              query.MaxPoints,
                              (connection, part) => ReadGoodsPartAsync(connection, query, part, cancellationToken),
                              cancellationToken);
    }

    /// <inheritdoc/>
    public Task<HistoryTrend<HistoryIslandsPoint>> ReadIslandsTrendAsync(HistoryIslandsTrendQuery query, CancellationToken cancellationToken)
    {
        return ReadIslandsAsync(query, cancellationToken);
    }

    #endregion // IHistoryTrends
}