using Censio.Contracts;

using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Writes the rows of one tick with prepared commands, one per row kind.
/// </summary>
internal sealed class TickWriter : IDisposable
{
    #region Constants

    /// <summary>
    /// The pair kind of a workforce amount.
    /// </summary>
    public const byte WorkforceKind = 0;

    /// <summary>
    /// The pair kind of a building amount.
    /// </summary>
    public const byte BuildingKind = 1;

    /// <summary>
    /// The number of float columns of an entry.
    /// </summary>
    private const int FloatCount = 8;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The command that inserts the tick.
    /// </summary>
    private readonly SqliteCommand _tickCommand;

    /// <summary>
    /// The command that inserts an island of the tick.
    /// </summary>
    private readonly SqliteCommand _islandCommand;

    /// <summary>
    /// The command that inserts an entry.
    /// </summary>
    private readonly SqliteCommand _entryCommand;

    /// <summary>
    /// The command that inserts a map pair.
    /// </summary>
    private readonly SqliteCommand _pairCommand;

    /// <summary>
    /// The command that inserts or updates the latest name of an island.
    /// </summary>
    private readonly SqliteCommand _nameCommand;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TickWriter"/> class.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    public TickWriter(SqliteConnection connection)
    {
        _tickCommand = Create(connection,
                              "INSERT INTO \"Ticks\" (\"Id\", \"ProfileId\", \"ReceiveTicks\", \"GameTime\") VALUES ($id, $profile, $receive, $game) RETURNING \"Number\";",
                              ("$id", SqliteType.Text),
                              ("$profile", SqliteType.Text),
                              ("$receive", SqliteType.Integer),
                              ("$game", SqliteType.Integer));
        _islandCommand = Create(connection,
                                "INSERT INTO \"TickIslands\" (\"TickNumber\", \"Position\", \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"SessionId\", \"Name\") VALUES ($tick, $position, $guid, $island, $area, $session, $name) RETURNING \"Id\";",
                                ("$tick", SqliteType.Integer),
                                ("$position", SqliteType.Integer),
                                ("$guid", SqliteType.Integer),
                                ("$island", SqliteType.Integer),
                                ("$area", SqliteType.Integer),
                                ("$session", SqliteType.Integer),
                                ("$name", SqliteType.Text));
        _entryCommand = Create(connection,
                               "INSERT INTO \"TickEntries\" (\"TickIslandId\", \"Position\", \"ProductGuid\", \"Generation\", \"Consumption\", \"Delta\", \"PerfectGeneration\", \"PerfectConsumption\", \"Income\", \"SummedProductivity\", \"AverageProductivity\", \"BuildingCount\", \"Maintenance\", \"Profit\") VALUES ($island, $position, $product, $f0, $f1, $f2, $f3, $f4, $f5, $f6, $f7, $buildings, $maintenance, $profit) RETURNING \"Id\";",
                               ("$island", SqliteType.Integer),
                               ("$position", SqliteType.Integer),
                               ("$product", SqliteType.Integer),
                               ("$f0", SqliteType.Integer),
                               ("$f1", SqliteType.Integer),
                               ("$f2", SqliteType.Integer),
                               ("$f3", SqliteType.Integer),
                               ("$f4", SqliteType.Integer),
                               ("$f5", SqliteType.Integer),
                               ("$f6", SqliteType.Integer),
                               ("$f7", SqliteType.Integer),
                               ("$buildings", SqliteType.Integer),
                               ("$maintenance", SqliteType.Integer),
                               ("$profit", SqliteType.Integer));
        _pairCommand = Create(connection,
                              "INSERT INTO \"TickMapPairs\" (\"EntryId\", \"Kind\", \"Guid\", \"Amount\") VALUES ($entry, $kind, $guid, $amount);",
                              ("$entry", SqliteType.Integer),
                              ("$kind", SqliteType.Integer),
                              ("$guid", SqliteType.Integer),
                              ("$amount", SqliteType.Integer));
        _nameCommand = Create(connection,
                              "INSERT INTO \"Islands\" (\"ProfileId\", \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"Name\", \"NameReceiveTicks\", \"NameTickNumber\") VALUES ($profile, $guid, $island, $area, $name, $receive, $tick) "
                              + "ON CONFLICT (\"ProfileId\", \"SessionGuid\", \"IslandId\", \"AreaIndex\") DO UPDATE SET \"Name\" = excluded.\"Name\", \"NameReceiveTicks\" = excluded.\"NameReceiveTicks\", \"NameTickNumber\" = excluded.\"NameTickNumber\" "
                              + "WHERE (excluded.\"NameReceiveTicks\", excluded.\"NameTickNumber\") >= (\"Islands\".\"NameReceiveTicks\", \"Islands\".\"NameTickNumber\");",
                              ("$profile", SqliteType.Text),
                              ("$guid", SqliteType.Integer),
                              ("$island", SqliteType.Integer),
                              ("$area", SqliteType.Integer),
                              ("$name", SqliteType.Text),
                              ("$receive", SqliteType.Integer),
                              ("$tick", SqliteType.Integer));

        foreach (var command in (SqliteCommand[])[_tickCommand, _islandCommand, _entryCommand, _pairCommand, _nameCommand])
        {
            command.Prepare();
        }
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Writes the tick, its islands, entries and pairs, and the latest island names.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task WriteAsync(ContractTick tick, CancellationToken cancellationToken)
    {
        _tickCommand.Parameters[0].Value = tick.Id;
        _tickCommand.Parameters[1].Value = tick.ProfileId;
        _tickCommand.Parameters[2].Value = tick.ReceiveTime.UtcTicks;
        _tickCommand.Parameters[3].Value = tick.GameTime;

        var number = (long)(await _tickCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        for (var position = 0; position < tick.Islands.Count; position++)
        {
            var island = tick.Islands[position];

            await WriteIslandAsync(tick, number, position, island, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the eight floats of an entry as their 32-bit patterns.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The bit patterns, <see cref="DBNull"/> for a missing value</returns>
    private static object[] ReadFloats(ContractTickEntry entry)
    {
        return
        [
            ToBits(entry.Generation),
            ToBits(entry.Consumption),
            ToBits(entry.Delta),
            ToBits(entry.PerfectGeneration),
            ToBits(entry.PerfectConsumption),
            ToBits(entry.Income),
            ToBits(entry.SummedProductivity),
            ToBits(entry.AverageProductivity)
        ];
    }

    /// <summary>
    /// Converts a float to its stored form.
    /// </summary>
    /// <param name="value">The value, or <see langword="null"/></param>
    /// <returns>The 32-bit pattern, or <see cref="DBNull"/></returns>
    private static object ToBits(float? value)
    {
        return value is null
                   ? DBNull.Value
                   : BitConverter.SingleToInt32Bits(value.Value);
    }

    /// <summary>
    /// Creates a prepared command.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The statement</param>
    /// <param name="parameters">The parameters, in the order they are set</param>
    /// <returns>The prepared command</returns>
    private static SqliteCommand Create(SqliteConnection connection, string sql, params (string Name, SqliteType Type)[] parameters)
    {
        var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only, values travel as parameters
        command.CommandText = sql;
#pragma warning restore CA2100

        foreach (var (name, type) in parameters)
        {
            command.Parameters.Add(name, type);
        }

        return command;
    }

    /// <summary>
    /// Writes one island with its entries and the island's latest name.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="number">The row number of the tick</param>
    /// <param name="position">The position of the island in the tick</param>
    /// <param name="island">The island</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task WriteIslandAsync(ContractTick tick, long number, int position, ContractTickIsland island, CancellationToken cancellationToken)
    {
        _islandCommand.Parameters[0].Value = number;
        _islandCommand.Parameters[1].Value = position;
        _islandCommand.Parameters[2].Value = island.SessionGuid;
        _islandCommand.Parameters[3].Value = (int)island.IslandId;
        _islandCommand.Parameters[4].Value = (int)island.AreaIndex;
        _islandCommand.Parameters[5].Value = (int)island.SessionId;
        _islandCommand.Parameters[6].Value = island.Name;

        var islandRow = (long)(await _islandCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        for (var entryPosition = 0; entryPosition < island.Entries.Count; entryPosition++)
        {
            await WriteEntryAsync(islandRow, entryPosition, island.Entries[entryPosition], cancellationToken).ConfigureAwait(false);
        }

        _nameCommand.Parameters[0].Value = tick.ProfileId;
        _nameCommand.Parameters[1].Value = island.SessionGuid;
        _nameCommand.Parameters[2].Value = (int)island.IslandId;
        _nameCommand.Parameters[3].Value = (int)island.AreaIndex;
        _nameCommand.Parameters[4].Value = island.Name;
        _nameCommand.Parameters[5].Value = tick.ReceiveTime.UtcTicks;
        _nameCommand.Parameters[6].Value = number;

        await _nameCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one entry with its map pairs.
    /// </summary>
    /// <param name="islandRow">The row number of the island</param>
    /// <param name="position">The position of the entry in the island</param>
    /// <param name="entry">The entry</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task WriteEntryAsync(long islandRow, int position, ContractTickEntry entry, CancellationToken cancellationToken)
    {
        var parameters = _entryCommand.Parameters;

        parameters[0].Value = islandRow;
        parameters[1].Value = position;
        parameters[2].Value = entry.ProductGuid;

        var floats = ReadFloats(entry);

        for (var index = 0; index < FloatCount; index++)
        {
            parameters[3 + index].Value = floats[index];
        }

        parameters[11].Value = entry.BuildingCount;
        parameters[12].Value = entry.Maintenance;
        parameters[13].Value = entry.Profit;

        var entryRow = (long)(await _entryCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        await WritePairsAsync(entryRow, WorkforceKind, entry.WorkforceAmounts, cancellationToken).ConfigureAwait(false);
        await WritePairsAsync(entryRow, BuildingKind, entry.BuildingAmounts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the pairs of one map.
    /// </summary>
    /// <param name="entryRow">The row number of the entry</param>
    /// <param name="kind">The pair kind</param>
    /// <param name="map">The map</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task WritePairsAsync(long entryRow, byte kind, IReadOnlyDictionary<int, int> map, CancellationToken cancellationToken)
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
        _tickCommand.Dispose();
        _islandCommand.Dispose();
        _entryCommand.Dispose();
        _pairCommand.Dispose();
        _nameCommand.Dispose();
    }

    #endregion // IDisposable
}