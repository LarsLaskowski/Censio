namespace Censio.Server;

/// <summary>
/// The request for a trend read: the latest tick, the choice and the view the page holds.
/// </summary>
public sealed class TrendRequest
{
    #region Properties

    /// <summary>
    /// Gets the receive time of the latest tick.
    /// </summary>
    public required DateTimeOffset LatestTick { get; init; }

    /// <summary>
    /// Gets the choice of range and axis.
    /// </summary>
    public required ChartChoice Choice { get; init; }

    /// <summary>
    /// Gets the view the page holds, or <see langword="null"/> when it holds none.
    /// </summary>
    public required TrendView? Held { get; init; }

    #endregion // Properties
}