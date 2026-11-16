namespace Censio.Server.Data;

/// <summary>
/// A query for the trend of the figures of one island.
/// </summary>
public sealed class HistoryFiguresTrendQuery
{
    #region Properties

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the island.
    /// </summary>
    public required HistoryIslandKey Island { get; init; }

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