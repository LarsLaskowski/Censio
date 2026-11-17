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
    public static TrendView Extend(TrendView held, TrendView newest, TrendRange range)
    {
        var replacedFrom = newest.Points.Count > 0
                               ? newest.Points[0].Time
                               : DateTimeOffset.MaxValue;
        var kept = held.Points.Where(point => point.Time >= range.Start && point.Time < replacedFrom).ToList();
        var removed = held.Points.Count - kept.Count;
        var points = kept.Concat(newest.Points).ToList();

        if (points.Count > TrendRange.MaxPoints)
        {
            points.RemoveRange(0, points.Count - TrendRange.MaxPoints);
        }

        return new TrendView
               {
                   Points = points,
                   TotalCount = Math.Max(0, held.TotalCount - removed) + newest.TotalCount
               };
    }

    #endregion // Methods
}