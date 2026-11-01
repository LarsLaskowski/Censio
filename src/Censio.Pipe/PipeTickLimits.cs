namespace Censio.Pipe;

/// <summary>
/// The limits and timing of tick assembly.
/// </summary>
public static class PipeTickLimits
{
    #region Constants

    /// <summary>
    /// The maximum number of islands in one tick.
    /// </summary>
    public const int MaxIslands = 256;

    /// <summary>
    /// The maximum number of entries and map pairs in one tick, counted after decoding.
    /// </summary>
    public const int MaxEntriesAndPairs = 524288;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the time without an island frame after which an open tick is closed.
    /// </summary>
    public static TimeSpan QuietPeriod { get; } = TimeSpan.FromSeconds(30);

    #endregion // Properties
}