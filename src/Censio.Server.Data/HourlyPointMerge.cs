using Microsoft.Data.Sqlite;

namespace Censio.Server.Data;

/// <summary>
/// Combines the hourly points of two profiles that hold the same hours into the target's, with set-wise statements over
/// the pairs of point numbers: no hourly row is read into memory.
/// </summary>
internal static class HourlyPointMerge
{
    #region Constants

    /// <summary>
    /// The name of the parameter of the source profile.
    /// </summary>
    private const string SourceParameter = "$source";

    /// <summary>
    /// The name of the parameter of the target profile.
    /// </summary>
    private const string TargetParameter = "$target";

    /// <summary>
    /// The statement that lists the point of each shared hour: the source's number and the target's.
    /// </summary>
    private const string CreatePairsSql = "CREATE TEMP TABLE \"MergePairs\" (\"SourcePoint\" INTEGER NOT NULL PRIMARY KEY, \"TargetPoint\" INTEGER NOT NULL);";

    /// <summary>
    /// The statement that fills the pairs of points.
    /// </summary>
    private const string FillPairsSql = "INSERT INTO \"MergePairs\" (\"SourcePoint\", \"TargetPoint\") "
                                        + "SELECT s.\"Number\", t.\"Number\" FROM \"HourlyPoints\" s JOIN \"HourlyPoints\" t ON t.\"HourTicks\" = s.\"HourTicks\" AND t.\"ProfileId\" = $target WHERE s.\"ProfileId\" = $source;";

    /// <summary>
    /// The statement that adds the ticks and widens the game-time range of the target's points.
    /// </summary>
    private const string CombinePointsSql = "UPDATE \"HourlyPoints\" AS t SET \"TickCount\" = t.\"TickCount\" + s.\"TickCount\", \"FirstGameTime\" = min(t.\"FirstGameTime\", s.\"FirstGameTime\"), \"LastGameTime\" = max(t.\"LastGameTime\", s.\"LastGameTime\") "
                                            + "FROM \"MergePairs\" m JOIN \"HourlyPoints\" s ON s.\"Number\" = m.\"SourcePoint\" WHERE t.\"Number\" = m.\"TargetPoint\";";

    /// <summary>
    /// The statement that gives a shared island the name of the later stamp.
    /// </summary>
    private const string RenameIslandsSql = "UPDATE \"HourlyIslands\" AS t SET \"Name\" = s.\"Name\", \"NameReceiveTicks\" = s.\"NameReceiveTicks\", \"NameTickNumber\" = s.\"NameTickNumber\" "
                                            + "FROM \"MergePairs\" m JOIN \"HourlyIslands\" s ON s.\"PointNumber\" = m.\"SourcePoint\" "
                                            + "WHERE t.\"PointNumber\" = m.\"TargetPoint\" AND t.\"SessionGuid\" = s.\"SessionGuid\" AND t.\"IslandId\" = s.\"IslandId\" AND t.\"AreaIndex\" = s.\"AreaIndex\" "
                                            + "AND (s.\"NameReceiveTicks\", s.\"NameTickNumber\") >= (t.\"NameReceiveTicks\", t.\"NameTickNumber\");";

    /// <summary>
    /// The statement that creates the pairs of the islands both points hold.
    /// </summary>
    private const string CreateIslandsSql = "CREATE TEMP TABLE \"MergeIslands\" (\"SourceIsland\" INTEGER NOT NULL PRIMARY KEY, \"TargetIsland\" INTEGER NOT NULL);";

    /// <summary>
    /// The statement that fills the pairs of islands.
    /// </summary>
    private const string FillIslandsSql = "INSERT INTO \"MergeIslands\" (\"SourceIsland\", \"TargetIsland\") "
                                          + "SELECT s.\"Id\", t.\"Id\" FROM \"MergePairs\" m JOIN \"HourlyIslands\" s ON s.\"PointNumber\" = m.\"SourcePoint\" "
                                          + "JOIN \"HourlyIslands\" t ON t.\"PointNumber\" = m.\"TargetPoint\" AND t.\"SessionGuid\" = s.\"SessionGuid\" AND t.\"IslandId\" = s.\"IslandId\" AND t.\"AreaIndex\" = s.\"AreaIndex\";";

    /// <summary>
    /// The statement that moves the islands the target's point lacks.
    /// </summary>
    private const string MoveIslandsSql = "UPDATE \"HourlyIslands\" SET \"PointNumber\" = m.\"TargetPoint\" FROM \"MergePairs\" m "
                                          + "WHERE \"HourlyIslands\".\"PointNumber\" = m.\"SourcePoint\" AND \"HourlyIslands\".\"Id\" NOT IN (SELECT \"SourceIsland\" FROM \"MergeIslands\");";

    /// <summary>
    /// The statement that creates the pairs of the entries both islands hold.
    /// </summary>
    private const string CreateEntriesSql = "CREATE TEMP TABLE \"MergeEntries\" (\"SourceEntry\" INTEGER NOT NULL PRIMARY KEY, \"TargetEntry\" INTEGER NOT NULL);";

    /// <summary>
    /// The statement that fills the pairs of entries.
    /// </summary>
    private const string FillEntriesSql = "INSERT INTO \"MergeEntries\" (\"SourceEntry\", \"TargetEntry\") "
                                          + "SELECT s.\"Id\", t.\"Id\" FROM \"MergeIslands\" i JOIN \"HourlyEntries\" s ON s.\"HourlyIslandId\" = i.\"SourceIsland\" "
                                          + "JOIN \"HourlyEntries\" t ON t.\"HourlyIslandId\" = i.\"TargetIsland\" AND t.\"ProductGuid\" = s.\"ProductGuid\";";

    /// <summary>
    /// The statement that moves the entries the target's island lacks.
    /// </summary>
    private const string MoveEntriesSql = "UPDATE \"HourlyEntries\" SET \"HourlyIslandId\" = i.\"TargetIsland\" FROM \"MergeIslands\" i "
                                          + "WHERE \"HourlyEntries\".\"HourlyIslandId\" = i.\"SourceIsland\" AND \"HourlyEntries\".\"Id\" NOT IN (SELECT \"SourceEntry\" FROM \"MergeEntries\");";

    /// <summary>
    /// The statement that adds the amounts of the pairs both entries hold.
    /// </summary>
    private const string CombinePairsSql = "UPDATE \"HourlyMapPairs\" AS t SET \"AmountSum\" = t.\"AmountSum\" + s.\"AmountSum\" "
                                           + "FROM \"MergeEntries\" m JOIN \"HourlyMapPairs\" s ON s.\"EntryId\" = m.\"SourceEntry\" "
                                           + "WHERE t.\"EntryId\" = m.\"TargetEntry\" AND t.\"Kind\" = s.\"Kind\" AND t.\"Guid\" = s.\"Guid\";";

    /// <summary>
    /// The statement that moves the pairs the target's entry lacks.
    /// </summary>
    private const string MovePairsSql = "UPDATE \"HourlyMapPairs\" SET \"EntryId\" = m.\"TargetEntry\" FROM \"MergeEntries\" m "
                                        + "WHERE \"HourlyMapPairs\".\"EntryId\" = m.\"SourceEntry\" AND NOT EXISTS (SELECT 1 FROM \"HourlyMapPairs\" x WHERE x.\"EntryId\" = m.\"TargetEntry\" AND x.\"Kind\" = \"HourlyMapPairs\".\"Kind\" AND x.\"Guid\" = \"HourlyMapPairs\".\"Guid\");";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The statements that remove what is left of the source's shared points, pairs first.
    /// </summary>
    private static readonly string[] _removeSql = [
                                                      "DELETE FROM \"HourlyMapPairs\" WHERE \"EntryId\" IN (SELECT \"SourceEntry\" FROM \"MergeEntries\");",
                                                      "DELETE FROM \"HourlyEntries\" WHERE \"Id\" IN (SELECT \"SourceEntry\" FROM \"MergeEntries\");",
                                                      "DELETE FROM \"HourlyIslands\" WHERE \"Id\" IN (SELECT \"SourceIsland\" FROM \"MergeIslands\");",
                                                      "UPDATE \"FoldedTicks\" SET \"PointNumber\" = m.\"TargetPoint\" FROM \"MergePairs\" m WHERE \"FoldedTicks\".\"PointNumber\" = m.\"SourcePoint\";",
                                                      "DELETE FROM \"HourlyPoints\" WHERE \"Number\" IN (SELECT \"SourcePoint\" FROM \"MergePairs\");",
                                                      "DROP TABLE \"MergeEntries\";",
                                                      "DROP TABLE \"MergeIslands\";",
                                                      "DROP TABLE \"MergePairs\";"
                                                  ];

    /// <summary>
    /// The names of the columns of an entry that add up, the count of entries included.
    /// </summary>
    private static readonly string[] _entryColumns = [
                                                         "EntryCount",
                                                         "GenerationSum",
                                                         "GenerationCount",
                                                         "ConsumptionSum",
                                                         "ConsumptionCount",
                                                         "DeltaSum",
                                                         "DeltaCount",
                                                         "PerfectGenerationSum",
                                                         "PerfectGenerationCount",
                                                         "PerfectConsumptionSum",
                                                         "PerfectConsumptionCount",
                                                         "IncomeSum",
                                                         "IncomeCount",
                                                         "SummedProductivitySum",
                                                         "SummedProductivityCount",
                                                         "AverageProductivitySum",
                                                         "AverageProductivityCount",
                                                         "BuildingCountSum",
                                                         "MaintenanceSum",
                                                         "ProfitSum"
                                                     ];

    /// <summary>
    /// The statement that adds the sums and counts of the entries both islands hold.
    /// </summary>
    private static readonly string _combineEntriesSql = $"UPDATE \"HourlyEntries\" AS t SET {string.Join(", ", _entryColumns.Select(column => $"\"{column}\" = t.\"{column}\" + s.\"{column}\""))} FROM \"MergeEntries\" m JOIN \"HourlyEntries\" s ON s.\"Id\" = m.\"SourceEntry\" WHERE t.\"Id\" = m.\"TargetEntry\";";

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Combines the source's hourly points of the hours the target holds as well into the target's points: ticks, game-time
    /// range, island names by the later stamp, entries, sums and map amounts, and the folded tick identities.
    /// </summary>
    /// <param name="connection">The opened connection, inside a write transaction</param>
    /// <param name="sourceProfileId">The identity of the profile whose points are combined and removed</param>
    /// <param name="targetProfileId">The identity of the profile whose points receive them</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task CombineAsync(SqliteConnection connection, Guid sourceProfileId, Guid targetProfileId, CancellationToken cancellationToken)
    {
        await StoreAccess.ExecuteAsync(connection, CreatePairsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, FillPairsSql, cancellationToken, (SourceParameter, sourceProfileId), (TargetParameter, targetProfileId)).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, CombinePointsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, RenameIslandsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, CreateIslandsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, FillIslandsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, MoveIslandsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, CreateEntriesSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, FillEntriesSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, MoveEntriesSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, _combineEntriesSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, CombinePairsSql, cancellationToken).ConfigureAwait(false);
        await StoreAccess.ExecuteAsync(connection, MovePairsSql, cancellationToken).ConfigureAwait(false);

        foreach (var sql in _removeSql)
        {
            await StoreAccess.ExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion // Methods
}