namespace Censio.Server.Data;

/// <summary>
/// Folds, sweeps and reads the history of the server database.
/// </summary>
public sealed class HistoryStore
{
    #region Fields

    /// <summary>
    /// The time span ticks are kept at full resolution
    /// </summary>
    public static readonly TimeSpan FullResolution = TimeSpan.FromDays(7);

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The database
    /// </summary>
    private readonly ServerDatabase _database;
#pragma warning restore S4487

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

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Folds the oldest tick of a profile beyond full resolution into its hourly point.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="now">The current time</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the step</returns>
    public Task<HistoryResult> FoldNextTickAsync(Guid profileId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Removes one piece of deleted history.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the step</returns>
    public Task<HistoryResult> SweepNextAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Checkpoints and truncates the write-ahead log so no deleted data stays in it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the step</returns>
    public Task<HistoryResult> ClearDeletedAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Reads the history series of one good on one island.
    /// </summary>
    /// <param name="query">The query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The points in time order</returns>
    public Task<IReadOnlyList<HistoryPoint>> ReadSeriesAsync(HistorySeriesQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    #endregion // Methods
}