using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Reads the charts of the data pages from the history.
/// </summary>
public sealed class TrendReader
{
    #region Fields

    /// <summary>
    /// The history trend reads
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly IHistoryTrends _trends;
#pragma warning restore S4487

    /// <summary>
    /// The logger
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly ILogger<TrendReader> _logger;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendReader"/> class.
    /// </summary>
    /// <param name="trends">The history trend reads</param>
    /// <param name="logger">The logger</param>
    public TrendReader(IHistoryTrends trends, ILogger<TrendReader> logger)
    {
        _trends = trends;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the chart of a good summed over a set of islands.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="islands">The islands to sum</param>
    /// <param name="productGuid">The product GUID of the good</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the read</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<TrendRead> ReadGoodAsync(Guid profileId, IReadOnlyList<IslandKey> islands, int productGuid, DateTimeOffset latestTick, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325

    /// <summary>
    /// Reads the chart of the key figures of an island.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island</param>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the read</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<TrendRead> ReadFiguresAsync(Guid profileId, IslandKey island, DateTimeOffset latestTick, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325

    #endregion // Methods
}