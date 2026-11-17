namespace Censio.Server;

/// <summary>
/// Moves the hourly view of a wide range on by its newest hours.
/// </summary>
public static class TrendWindow
{
    #region Methods

    /// <summary>
    /// Replaces the newest hours of a held view by the newly read ones, drops the hours before the range and keeps the newest points within the cap.
    /// </summary>
    /// <param name="held">The held view</param>
    /// <param name="newest">The view of the newest hours</param>
    /// <param name="range">The range</param>
    /// <returns>The extended view</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static TrendView Extend(TrendView held, TrendView newest, TrendRange range)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}