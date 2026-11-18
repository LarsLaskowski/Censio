namespace Censio.Server;

/// <summary>
/// Builds the line of a sparkline from its samples.
/// </summary>
public static class SparklineBuilder
{
    #region Constants

    /// <summary>
    /// The width of a sparkline.
    /// </summary>
    public const int Width = 80;

    /// <summary>
    /// The height of a sparkline.
    /// </summary>
    public const int Height = 20;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Builds the sparkline of a series of samples.
    /// </summary>
    /// <param name="samples">The samples, oldest first</param>
    /// <param name="range">The range the sparkline spans</param>
    /// <param name="decimals">The decimals the values are rounded to for the direction</param>
    /// <returns>The sparkline, or <see langword="null"/> for fewer than two valued points</returns>
    public static SparklineView? Build(IReadOnlyList<SparklineSample> samples, TrendRange range, int decimals)
    {
#pragma warning disable RH2003 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods
}