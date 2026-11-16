using Censio.Contracts;

using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Folds one stored tick into its hourly point with prepared commands, one per row kind.
/// </summary>
internal sealed class HistoryFolder : IDisposable
{
    #region Constants

    /// <summary>
    /// The name of the parameter of a tick row number.
    /// </summary>
    private const string TickParameter = "$tick";

    /// <summary>
    /// The name of the parameter of an hourly point row number.
    /// </summary>
    private const string PointParameter = "$point";

    /// <summary>
    /// The number of float columns of an entry.
    /// </summary>
    private const int FloatCount = 8;

    /// <summary>
    /// The statement that inserts a tick into its hourly point or adds it to it.
    /// </summary>
    private const string PointSql = "INSERT INTO \"HourlyPoints\" (\"ProfileId\", \"HourTicks\", \"TickCount\", \"FirstGameTime\", \"LastGameTime\") VALUES ($profile, $hour, 1, $game, $game) "
                                    + "ON CONFLICT (\"ProfileId\", \"HourTicks\") DO UPDATE SET \"TickCount\" = \"TickCount\" + 1, \"FirstGameTime\" = min(\"FirstGameTime\", excluded.\"FirstGameTime\"), \"LastGameTime\" = max(\"LastGameTime\", excluded.\"LastGameTime\") RETURNING \"Number\";";

    /// <summary>
    /// The statement that inserts an island of an hourly point or takes the name when its receive time and number are not older.
    /// </summary>
    private const string IslandSql = "INSERT INTO \"HourlyIslands\" (\"PointNumber\", \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"Name\", \"NameReceiveTicks\", \"NameTickNumber\") VALUES ($point, $guid, $island, $area, $name, $receive, $tick) "
                                     + "ON CONFLICT (\"PointNumber\", \"SessionGuid\", \"IslandId\", \"AreaIndex\") DO UPDATE SET "
                                     + "\"Name\" = CASE WHEN (excluded.\"NameReceiveTicks\", excluded.\"NameTickNumber\") >= (\"HourlyIslands\".\"NameReceiveTicks\", \"HourlyIslands\".\"NameTickNumber\") THEN excluded.\"Name\" ELSE \"HourlyIslands\".\"Name\" END, "
                                     + "\"NameReceiveTicks\" = max(excluded.\"NameReceiveTicks\", \"HourlyIslands\".\"NameReceiveTicks\"), "
                                     + "\"NameTickNumber\" = CASE WHEN (excluded.\"NameReceiveTicks\", excluded.\"NameTickNumber\") >= (\"HourlyIslands\".\"NameReceiveTicks\", \"HourlyIslands\".\"NameTickNumber\") THEN excluded.\"NameTickNumber\" ELSE \"HourlyIslands\".\"NameTickNumber\" END RETURNING \"Id\";";

    /// <summary>
    /// The statement that inserts the first entry of a good of an hourly island or adds a tick's entry to it.
    /// </summary>
    private const string EntrySql = "INSERT INTO \"HourlyEntries\" (\"HourlyIslandId\", \"ProductGuid\", \"EntryCount\", "
                                    + "\"GenerationSum\", \"GenerationCount\", \"ConsumptionSum\", \"ConsumptionCount\", \"DeltaSum\", \"DeltaCount\", \"PerfectGenerationSum\", \"PerfectGenerationCount\", "
                                    + "\"PerfectConsumptionSum\", \"PerfectConsumptionCount\", \"IncomeSum\", \"IncomeCount\", \"SummedProductivitySum\", \"SummedProductivityCount\", \"AverageProductivitySum\", \"AverageProductivityCount\", "
                                    + "\"BuildingCountSum\", \"MaintenanceSum\", \"ProfitSum\") "
                                    + "VALUES ($island, $product, 1, $s0, $c0, $s1, $c1, $s2, $c2, $s3, $c3, $s4, $c4, $s5, $c5, $s6, $c6, $s7, $c7, $buildings, $maintenance, $profit) "
                                    + "ON CONFLICT (\"HourlyIslandId\", \"ProductGuid\") DO UPDATE SET \"EntryCount\" = \"EntryCount\" + 1, "
                                    + "\"GenerationSum\" = \"GenerationSum\" + excluded.\"GenerationSum\", \"GenerationCount\" = \"GenerationCount\" + excluded.\"GenerationCount\", "
                                    + "\"ConsumptionSum\" = \"ConsumptionSum\" + excluded.\"ConsumptionSum\", \"ConsumptionCount\" = \"ConsumptionCount\" + excluded.\"ConsumptionCount\", "
                                    + "\"DeltaSum\" = \"DeltaSum\" + excluded.\"DeltaSum\", \"DeltaCount\" = \"DeltaCount\" + excluded.\"DeltaCount\", "
                                    + "\"PerfectGenerationSum\" = \"PerfectGenerationSum\" + excluded.\"PerfectGenerationSum\", \"PerfectGenerationCount\" = \"PerfectGenerationCount\" + excluded.\"PerfectGenerationCount\", "
                                    + "\"PerfectConsumptionSum\" = \"PerfectConsumptionSum\" + excluded.\"PerfectConsumptionSum\", \"PerfectConsumptionCount\" = \"PerfectConsumptionCount\" + excluded.\"PerfectConsumptionCount\", "
                                    + "\"IncomeSum\" = \"IncomeSum\" + excluded.\"IncomeSum\", \"IncomeCount\" = \"IncomeCount\" + excluded.\"IncomeCount\", "
                                    + "\"SummedProductivitySum\" = \"SummedProductivitySum\" + excluded.\"SummedProductivitySum\", \"SummedProductivityCount\" = \"SummedProductivityCount\" + excluded.\"SummedProductivityCount\", "
                                    + "\"AverageProductivitySum\" = \"AverageProductivitySum\" + excluded.\"AverageProductivitySum\", \"AverageProductivityCount\" = \"AverageProductivityCount\" + excluded.\"AverageProductivityCount\", "
                                    + "\"BuildingCountSum\" = \"BuildingCountSum\" + excluded.\"BuildingCountSum\", \"MaintenanceSum\" = \"MaintenanceSum\" + excluded.\"MaintenanceSum\", \"ProfitSum\" = \"ProfitSum\" + excluded.\"ProfitSum\" "
                                    + "RETURNING \"Id\";";

    /// <summary>
    /// The statement that inserts a map pair of an hourly entry or adds an amount to it.
    /// </summary>
    private const string PairSql = "INSERT INTO \"HourlyMapPairs\" (\"EntryId\", \"Kind\", \"Guid\", \"AmountSum\") VALUES ($entry, $kind, $guid, $amount) "
                                   + "ON CONFLICT (\"EntryId\", \"Kind\", \"Guid\") DO UPDATE SET \"AmountSum\" = \"AmountSum\" + excluded.\"AmountSum\";";

    /// <summary>
    /// The statement that records the identity of a folded tick.
    /// </summary>
    private const string FoldedSql = "INSERT INTO \"FoldedTicks\" (\"Id\", \"PointNumber\") VALUES ($id, $point);";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The command that inserts or updates the hourly point.
    /// </summary>
    private readonly SqliteCommand _pointCommand;

    /// <summary>
    /// The command that inserts or updates an island of the point.
    /// </summary>
    private readonly SqliteCommand _islandCommand;

    /// <summary>
    /// The command that inserts or updates an entry.
    /// </summary>
    private readonly SqliteCommand _entryCommand;

    /// <summary>
    /// The command that inserts or updates a map pair.
    /// </summary>
    private readonly SqliteCommand _pairCommand;

    /// <summary>
    /// The command that records the identity of the folded tick.
    /// </summary>
    private readonly SqliteCommand _foldedCommand;

    /// <summary>
    /// The connection
    /// </summary>
    private readonly SqliteConnection _connection;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryFolder"/> class.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    public HistoryFolder(SqliteConnection connection)
    {
        _connection = connection;
        _pointCommand = TickWriter.Create(connection,
                                          PointSql,
                                          ("$profile", SqliteType.Text),
                                          ("$hour", SqliteType.Integer),
                                          ("$game", SqliteType.Integer));
        _islandCommand = TickWriter.Create(connection,
                                           IslandSql,
                                           (PointParameter, SqliteType.Integer),
                                           ("$guid", SqliteType.Integer),
                                           ("$island", SqliteType.Integer),
                                           ("$area", SqliteType.Integer),
                                           ("$name", SqliteType.Text),
                                           ("$receive", SqliteType.Integer),
                                           (TickParameter, SqliteType.Integer));
        _entryCommand = TickWriter.Create(connection,
                                          EntrySql,
                                          ("$island", SqliteType.Integer),
                                          ("$product", SqliteType.Integer),
                                          ("$s0", SqliteType.Real),
                                          ("$c0", SqliteType.Integer),
                                          ("$s1", SqliteType.Real),
                                          ("$c1", SqliteType.Integer),
                                          ("$s2", SqliteType.Real),
                                          ("$c2", SqliteType.Integer),
                                          ("$s3", SqliteType.Real),
                                          ("$c3", SqliteType.Integer),
                                          ("$s4", SqliteType.Real),
                                          ("$c4", SqliteType.Integer),
                                          ("$s5", SqliteType.Real),
                                          ("$c5", SqliteType.Integer),
                                          ("$s6", SqliteType.Real),
                                          ("$c6", SqliteType.Integer),
                                          ("$s7", SqliteType.Real),
                                          ("$c7", SqliteType.Integer),
                                          ("$buildings", SqliteType.Integer),
                                          ("$maintenance", SqliteType.Integer),
                                          ("$profit", SqliteType.Integer));
        _pairCommand = TickWriter.Create(connection,
                                         PairSql,
                                         ("$entry", SqliteType.Integer),
                                         ("$kind", SqliteType.Integer),
                                         ("$guid", SqliteType.Integer),
                                         ("$amount", SqliteType.Integer));
        _foldedCommand = TickWriter.Create(connection,
                                           FoldedSql,
                                           ("$id", SqliteType.Text),
                                           (PointParameter, SqliteType.Integer));

        foreach (var command in (SqliteCommand[])[_pointCommand, _islandCommand, _entryCommand, _pairCommand, _foldedCommand])
        {
            command.Prepare();
        }
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Removes a tick row with its islands, entries and map pairs.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    /// <param name="number">The row number of the tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task RemoveTickAsync(SqliteConnection connection, long number, CancellationToken cancellationToken)
    {
        await StoreAccess.ExecuteAsync(connection,
                                       "DELETE FROM \"TickMapPairs\" WHERE \"EntryId\" IN (SELECT e.\"Id\" FROM \"TickEntries\" e JOIN \"TickIslands\" i ON i.\"Id\" = e.\"TickIslandId\" WHERE i.\"TickNumber\" = $tick);",
                                       cancellationToken,
                                       (TickParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection,
                                       "DELETE FROM \"TickEntries\" WHERE \"TickIslandId\" IN (SELECT \"Id\" FROM \"TickIslands\" WHERE \"TickNumber\" = $tick);",
                                       cancellationToken,
                                       (TickParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"TickIslands\" WHERE \"TickNumber\" = $tick;", cancellationToken, (TickParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"Ticks\" WHERE \"Number\" = $tick;", cancellationToken, (TickParameter, number)).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes an hourly point with its islands, entries, map pairs and tick identities.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    /// <param name="number">The row number of the hourly point</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task RemovePointAsync(SqliteConnection connection, long number, CancellationToken cancellationToken)
    {
        await StoreAccess.ExecuteAsync(connection,
                                       "DELETE FROM \"HourlyMapPairs\" WHERE \"EntryId\" IN (SELECT e.\"Id\" FROM \"HourlyEntries\" e JOIN \"HourlyIslands\" i ON i.\"Id\" = e.\"HourlyIslandId\" WHERE i.\"PointNumber\" = $point);",
                                       cancellationToken,
                                       (PointParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection,
                                       "DELETE FROM \"HourlyEntries\" WHERE \"HourlyIslandId\" IN (SELECT \"Id\" FROM \"HourlyIslands\" WHERE \"PointNumber\" = $point);",
                                       cancellationToken,
                                       (PointParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"HourlyIslands\" WHERE \"PointNumber\" = $point;", cancellationToken, (PointParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"FoldedTicks\" WHERE \"PointNumber\" = $point;", cancellationToken, (PointParameter, number)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, "DELETE FROM \"HourlyPoints\" WHERE \"Number\" = $point;", cancellationToken, (PointParameter, number)).ConfigureAwait(false);
    }

    /// <summary>
    /// Folds a tick into the hourly point of its hour, records its identity and removes its rows.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="number">The row number of the tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The start of the hour the tick was folded into</returns>
    public async Task<DateTimeOffset> FoldAsync(ContractTick tick, long number, CancellationToken cancellationToken)
    {
        var receiveTicks = tick.ReceiveTime.UtcTicks;
        var hourTicks = receiveTicks - (receiveTicks % TimeSpan.TicksPerHour);

        _pointCommand.Parameters[0].Value = tick.ProfileId;
        _pointCommand.Parameters[1].Value = hourTicks;
        _pointCommand.Parameters[2].Value = tick.GameTime;

        var pointNumber = (long)(await _pointCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        foreach (var island in tick.Islands)
        {
            await FoldIslandAsync(pointNumber, receiveTicks, number, island, cancellationToken).ConfigureAwait(false);
        }

        _foldedCommand.Parameters[0].Value = tick.Id;
        _foldedCommand.Parameters[1].Value = pointNumber;

        await _foldedCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await RemoveTickAsync(_connection, number, cancellationToken).ConfigureAwait(false);

        return new DateTimeOffset(hourTicks, TimeSpan.Zero);
    }

    /// <summary>
    /// Folds one island of a tick, its first entry of each good, and those entries' map pairs.
    /// </summary>
    /// <param name="pointNumber">The row number of the hourly point</param>
    /// <param name="receiveTicks">The receive time of the tick in ticks</param>
    /// <param name="tickNumber">The row number of the tick</param>
    /// <param name="island">The island</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task FoldIslandAsync(long pointNumber, long receiveTicks, long tickNumber, ContractTickIsland island, CancellationToken cancellationToken)
    {
        _islandCommand.Parameters[0].Value = pointNumber;
        _islandCommand.Parameters[1].Value = island.SessionGuid;
        _islandCommand.Parameters[2].Value = (int)island.IslandId;
        _islandCommand.Parameters[3].Value = (int)island.AreaIndex;
        _islandCommand.Parameters[4].Value = island.Name;
        _islandCommand.Parameters[5].Value = receiveTicks;
        _islandCommand.Parameters[6].Value = tickNumber;

        var islandRow = (long)(await _islandCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        foreach (var entry in island.Entries.DistinctBy(entry => entry.ProductGuid))
        {
            await FoldEntryAsync(islandRow, entry, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Folds one entry with its map pairs.
    /// </summary>
    /// <param name="islandRow">The row number of the hourly island</param>
    /// <param name="entry">The entry</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task FoldEntryAsync(long islandRow, ContractTickEntry entry, CancellationToken cancellationToken)
    {
        var parameters = _entryCommand.Parameters;

        parameters[0].Value = islandRow;
        parameters[1].Value = entry.ProductGuid;

        float?[] floats = [entry.Generation, entry.Consumption, entry.Delta, entry.PerfectGeneration, entry.PerfectConsumption, entry.Income, entry.SummedProductivity, entry.AverageProductivity];

        for (var index = 0; index < FloatCount; index++)
        {
            var value = floats[index];

            parameters[2 + (2 * index)].Value = value is null
                                                    ? 0d
                                                    : (double)value.Value;
            parameters[3 + (2 * index)].Value = value is null
                                                    ? 0
                                                    : 1;
        }

        parameters[18].Value = entry.BuildingCount;
        parameters[19].Value = entry.Maintenance;
        parameters[20].Value = entry.Profit;

        var entryRow = (long)(await _entryCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        await FoldPairsAsync(entryRow, TickWriter.WorkforceKind, entry.WorkforceAmounts, cancellationToken).ConfigureAwait(false);
        await FoldPairsAsync(entryRow, TickWriter.BuildingKind, entry.BuildingAmounts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Folds the pairs of one map.
    /// </summary>
    /// <param name="entryRow">The row number of the hourly entry</param>
    /// <param name="kind">The pair kind</param>
    /// <param name="map">The map</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task FoldPairsAsync(long entryRow, byte kind, IReadOnlyDictionary<int, int> map, CancellationToken cancellationToken)
    {
        foreach (var pair in map)
        {
            _pairCommand.Parameters[0].Value = entryRow;
            _pairCommand.Parameters[1].Value = (int)kind;
            _pairCommand.Parameters[2].Value = pair.Key;
            _pairCommand.Parameters[3].Value = pair.Value;

            await _pairCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        _pointCommand.Dispose();
        _islandCommand.Dispose();
        _entryCommand.Dispose();
        _pairCommand.Dispose();
        _foldedCommand.Dispose();
    }

    #endregion // IDisposable
}