using Censio.Contracts;

using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Reads a stored tick back into its contract form.
/// </summary>
internal static class TickReader
{
    #region Constants

    /// <summary>
    /// The statement that selects a tick by its identity.
    /// </summary>
    public const string ByIdSql = "SELECT \"Number\", \"Id\", \"ProfileId\", \"ReceiveTicks\", \"GameTime\" FROM \"Ticks\" WHERE \"Id\" = $value;";

    /// <summary>
    /// The statement that selects the latest tick of a profile: latest receive time, then the one stored last.
    /// </summary>
    public const string LatestSql = "SELECT \"Number\", \"Id\", \"ProfileId\", \"ReceiveTicks\", \"GameTime\" FROM \"Ticks\" WHERE \"ProfileId\" = $value ORDER BY \"ReceiveTicks\" DESC, \"Number\" DESC LIMIT 1;";

    /// <summary>
    /// The statement that selects the islands of a tick.
    /// </summary>
    private const string IslandsSql = "SELECT \"Id\", \"SessionGuid\", \"IslandId\", \"AreaIndex\", \"SessionId\", \"Name\" FROM \"TickIslands\" WHERE \"TickNumber\" = $tick ORDER BY \"Position\";";

    /// <summary>
    /// The statement that selects the entries of a tick in stored order.
    /// </summary>
    private const string EntriesSql = "SELECT e.\"Id\", e.\"TickIslandId\", e.\"ProductGuid\", e.\"Generation\", e.\"Consumption\", e.\"Delta\", e.\"PerfectGeneration\", e.\"PerfectConsumption\", e.\"Income\", e.\"SummedProductivity\", e.\"AverageProductivity\", e.\"BuildingCount\", e.\"Maintenance\", e.\"Profit\" "
                                      + "FROM \"TickEntries\" e JOIN \"TickIslands\" i ON i.\"Id\" = e.\"TickIslandId\" WHERE i.\"TickNumber\" = $tick ORDER BY i.\"Position\", e.\"Position\";";

    /// <summary>
    /// The statement that selects the map pairs of a tick.
    /// </summary>
    private const string PairsSql = "SELECT p.\"EntryId\", p.\"Kind\", p.\"Guid\", p.\"Amount\" FROM \"TickMapPairs\" p JOIN \"TickEntries\" e ON e.\"Id\" = p.\"EntryId\" JOIN \"TickIslands\" i ON i.\"Id\" = e.\"TickIslandId\" WHERE i.\"TickNumber\" = $tick ORDER BY p.\"Kind\", p.\"Guid\";";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads one tick, in one read transaction so that a write in between is not seen half.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="sql">The statement, <see cref="ByIdSql"/> or <see cref="LatestSql"/></param>
    /// <param name="value">The value of its parameter</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The tick, or <see langword="null"/> when none matches</returns>
    public static async Task<ContractTick?> ReadAsync(SqliteConnection connection, string sql, Guid value, CancellationToken cancellationToken)
    {
        using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        long number;
        Guid id;
        Guid profileId;
        long receiveTicks;
        long gameTime;

        using (var command = CreateCommand(connection, sql, "$value", value))
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == false)
            {
                return null;
            }

            number = reader.GetInt64(0);
            id = reader.GetGuid(1);
            profileId = reader.GetGuid(2);
            receiveTicks = reader.GetInt64(3);
            gameTime = reader.GetInt64(4);
        }

        var pairs = await ReadPairsAsync(connection, number, cancellationToken).ConfigureAwait(false);
        var entries = await ReadEntriesAsync(connection, number, pairs, cancellationToken).ConfigureAwait(false);
        var islands = await ReadIslandsAsync(connection, number, entries, cancellationToken).ConfigureAwait(false);

        return new ContractTick
               {
                   Id = id,
                   ProfileId = profileId,
                   ReceiveTime = new DateTimeOffset(receiveTicks, TimeSpan.Zero),
                   GameTime = gameTime,
                   Islands = islands
               };
    }

    /// <summary>
    /// Creates a command with one parameter.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="sql">The constant statement</param>
    /// <param name="name">The parameter name</param>
    /// <param name="value">The parameter value</param>
    /// <returns>The command</returns>
    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, string name, object value)
    {
        var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only, values travel as parameters
        command.CommandText = sql;
#pragma warning restore CA2100

        command.Parameters.AddWithValue(name, value);

        return command;
    }

    /// <summary>
    /// Reads the map pairs of a tick, by entry row.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="number">The tick row number</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The workforce and building maps of the entries that have pairs</returns>
    private static async Task<Dictionary<long, (Dictionary<int, int> Workforce, Dictionary<int, int> Buildings)>> ReadPairsAsync(SqliteConnection connection, long number, CancellationToken cancellationToken)
    {
        var pairs = new Dictionary<long, (Dictionary<int, int> Workforce, Dictionary<int, int> Buildings)>();

        using var command = CreateCommand(connection, PairsSql, "$tick", number);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entryRow = reader.GetInt64(0);

            if (pairs.TryGetValue(entryRow, out var maps) == false)
            {
                maps = ([], []);
                pairs[entryRow] = maps;
            }

            var target = reader.GetByte(1) == TickWriter.WorkforceKind
                             ? maps.Workforce
                             : maps.Buildings;

            target[reader.GetInt32(2)] = reader.GetInt32(3);
        }

        return pairs;
    }

    /// <summary>
    /// Reads the entries of a tick, by island row.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="number">The tick row number</param>
    /// <param name="pairs">The map pairs by entry row</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The entries of each island, in stored order</returns>
    private static async Task<Dictionary<long, List<ContractTickEntry>>> ReadEntriesAsync(SqliteConnection connection, long number, Dictionary<long, (Dictionary<int, int> Workforce, Dictionary<int, int> Buildings)> pairs, CancellationToken cancellationToken)
    {
        var entries = new Dictionary<long, List<ContractTickEntry>>();

        using var command = CreateCommand(connection, EntriesSql, "$tick", number);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var islandRow = reader.GetInt64(1);

            if (entries.TryGetValue(islandRow, out var list) == false)
            {
                list = [];
                entries[islandRow] = list;
            }

            var maps = pairs.GetValueOrDefault(reader.GetInt64(0));

            list.Add(new ContractTickEntry
                     {
                         ProductGuid = reader.GetInt32(2),
                         Generation = ReadFloat(reader, 3),
                         Consumption = ReadFloat(reader, 4),
                         Delta = ReadFloat(reader, 5),
                         PerfectGeneration = ReadFloat(reader, 6),
                         PerfectConsumption = ReadFloat(reader, 7),
                         Income = ReadFloat(reader, 8),
                         SummedProductivity = ReadFloat(reader, 9),
                         AverageProductivity = ReadFloat(reader, 10),
                         BuildingCount = reader.GetInt32(11),
                         Maintenance = reader.GetInt32(12),
                         Profit = reader.GetInt32(13),
                         WorkforceAmounts = maps.Workforce ?? [],
                         BuildingAmounts = maps.Buildings ?? []
                     });
        }

        return entries;
    }

    /// <summary>
    /// Reads the islands of a tick.
    /// </summary>
    /// <param name="connection">The opened connection</param>
    /// <param name="number">The tick row number</param>
    /// <param name="entries">The entries by island row</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The islands in stored order</returns>
    private static async Task<List<ContractTickIsland>> ReadIslandsAsync(SqliteConnection connection, long number, Dictionary<long, List<ContractTickEntry>> entries, CancellationToken cancellationToken)
    {
        var islands = new List<ContractTickIsland>();

        using var command = CreateCommand(connection, IslandsSql, "$tick", number);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            islands.Add(new ContractTickIsland
                        {
                            SessionGuid = reader.GetInt32(1),
                            IslandId = reader.GetByte(2),
                            AreaIndex = reader.GetByte(3),
                            SessionId = reader.GetByte(4),
                            Name = reader.GetString(5),
                            Entries = entries.GetValueOrDefault(reader.GetInt64(0)) ?? []
                        });
        }

        return islands;
    }

    /// <summary>
    /// Reads a stored float pattern.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <param name="ordinal">The column</param>
    /// <returns>The float, or <see langword="null"/></returns>
    private static float? ReadFloat(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
                   ? null
                   : BitConverter.Int32BitsToSingle(reader.GetInt32(ordinal));
    }

    #endregion // Methods
}