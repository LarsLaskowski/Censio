namespace Censio.Server;

/// <summary>
/// The time range a trend chart shows.
/// </summary>
public sealed class TrendRange
{
    #region Constants

    /// <summary>
    /// The most points a chart read returns.
    /// </summary>
    public const int MaxPoints = 2880;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the start of the range, inclusive.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Gets the end of the range, exclusive.
    /// </summary>
    public required DateTimeOffset End { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gives the range of the day up to the latest tick.
    /// </summary>
    /// <param name="latestTick">The receive time of the latest tick</param>
    /// <returns>The range</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static TrendRange Before(DateTimeOffset latestTick)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Methods
}