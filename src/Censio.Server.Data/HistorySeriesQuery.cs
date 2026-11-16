namespace Censio.Server.Data;

/// <summary>
/// A query for the history series of one good on one island.
/// </summary>
public sealed class HistorySeriesQuery
{
    #region Properties

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the session GUID of the island.
    /// </summary>
    public required int SessionGuid { get; init; }

    /// <summary>
    /// Gets the island ID.
    /// </summary>
    public required byte IslandId { get; init; }

    /// <summary>
    /// Gets the area index of the island.
    /// </summary>
    public required byte AreaIndex { get; init; }

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

    #endregion // Properties
}