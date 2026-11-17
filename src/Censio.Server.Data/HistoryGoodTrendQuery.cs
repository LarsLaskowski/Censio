namespace Censio.Server.Data;

/// <summary>
/// A query for the trend of one good summed over a set of islands.
/// </summary>
public sealed class HistoryGoodTrendQuery
{
    #region Properties

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the islands to sum.
    /// </summary>
    public required IReadOnlyList<HistoryIslandKey> Islands { get; init; }

    /// <summary>
    /// Gets the product GUID of the good.
    /// </summary>
    public required int ProductGuid { get; init; }

    /// <summary>
    /// Gets the start of the range, inclusive.
    /// </summary>
    public required DateTimeOffset RangeStart { get; init; }

    /// <summary>
    /// Gets the end of the range, exclusive.
    /// </summary>
    public required DateTimeOffset RangeEnd { get; init; }

    /// <summary>
    /// Gets the maximum number of points to return.
    /// </summary>
    public required int MaxPoints { get; init; }

    #endregion // Properties
}