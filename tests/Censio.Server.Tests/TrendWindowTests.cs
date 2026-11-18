using System.Globalization;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendWindow" />.
/// </summary>
[TestClass]
public sealed class TrendWindowTests
{
    #region Fields

    /// <summary>
    /// The hour the views of the tests end at: 2026-11-13 12:00 UTC.
    /// </summary>
    private static readonly DateTimeOffset _end = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    #endregion // Fields

    #region Methods

    /// <summary>
    /// The newest hours replace the held hours from the first of them on, the older held hours stay, and the total is the
    /// held total minus the replaced points plus the new total.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void TrendWindowExtendReplacesNewestHourAndKeepsOlder()
    {
        // Arrange
        var held = View(4, [(_end.AddHours(-3), 1), (_end.AddHours(-2), 2), (_end.AddHours(-1), 3)]);
        var newest = View(2, [(_end.AddHours(-1), 5), (_end, 6)]);
        var range = Range(_end.AddDays(-7));

        // Act
        var extended = TrendWindow.Extend(held, newest, range);

        // Assert
        var expected = new[] { "-3 1", "-2 2", "-1 5", "0 6" };

        Assert.AreSequenceEqual(expected, Describe(extended), "The last held hour should be replaced by the new one and the older hours kept");
        Assert.AreEqual(5, extended.TotalCount, "The total should be the held total of 4, minus the replaced hour, plus the new total of 2");
    }

    /// <summary>
    /// Hours before the start of the range leave the view and the total.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void TrendWindowExtendDropsHoursBeforeRangeStart()
    {
        // Arrange
        var held = View(4, [(_end.AddHours(-3), 1), (_end.AddHours(-2), 2), (_end.AddHours(-1), 3), (_end, 4)]);
        var newest = View(2, [(_end, 6), (_end.AddHours(1), 7)]);
        var range = Range(_end.AddHours(-2));

        // Act
        var extended = TrendWindow.Extend(held, newest, range);

        // Assert
        var expected = new[] { "-2 2", "-1 3", "0 6", "1 7" };

        Assert.AreSequenceEqual(expected, Describe(extended), "The hour before the range should be dropped and the last held hour replaced");
        Assert.AreEqual(4, extended.TotalCount, "The total should be 4 minus the replaced and the dropped hour plus 2");
    }

    /// <summary>
    /// A view over the cap of 2,880 points keeps the newest and the total stays: the oldest points leave only the view.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void TrendWindowExtendOverCapKeepsNewestAndTotal()
    {
        // Arrange
        var first = _end.AddHours(-2879);
        var held = View(3000, Enumerable.Range(0, TrendRange.MaxPoints).Select(index => (first.AddHours(index), (double)index)).ToList());
        var newest = View(2, [(_end, 5000), (_end.AddHours(1), 5001)]);
        var range = Range(DateTimeOffset.MinValue);

        // Act
        var extended = TrendWindow.Extend(held, newest, range);

        // Assert
        Assert.HasCount(TrendRange.MaxPoints, extended.Points, "The view should hold the newest 2,880 points");
        Assert.AreEqual(first.AddHours(1).UtcDateTime, extended.Points[0].Time.UtcDateTime, "The oldest point should have left");
        Assert.AreEqual(_end.AddHours(1).UtcDateTime, extended.Points[^1].Time.UtcDateTime, "The newest point should be the last new hour");
        Assert.AreEqual((double?)5000d, extended.Points[^2].Values[0], "The last held hour should be replaced");
        Assert.AreEqual(3001, extended.TotalCount, "The total should be 3,000 minus the replaced hour plus 2, the cap leaving it unchanged");
    }

    /// <summary>
    /// Creates a view of hourly points with one value each.
    /// </summary>
    /// <param name="total">The total</param>
    /// <param name="points">The times and values</param>
    /// <returns>The view</returns>
    private static TrendView View(int total, IReadOnlyList<(DateTimeOffset Time, double Value)> points)
    {
        return new TrendView
               {
                   Points = [
                                .. points.Select(point => new TrendPoint
                                                          {
                                                              Time = point.Time,
                                                              IsHourly = true,
                                                              Values = [point.Value]
                                                          })
                            ],
                   TotalCount = total
               };
    }

    /// <summary>
    /// Creates a range that ends after the hours of the tests.
    /// </summary>
    /// <param name="start">The start</param>
    /// <returns>The range</returns>
    private static TrendRange Range(DateTimeOffset start)
    {
        return new TrendRange
               {
                   Start = start,
                   End = _end.AddHours(2)
               };
    }

    /// <summary>
    /// Describes the points of a view: the hours from the end of the tests and the first value.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The texts</returns>
    private static IEnumerable<string> Describe(TrendView view)
    {
        return view.Points.Select(point => $"{(point.Time - _end).TotalHours.ToString(CultureInfo.InvariantCulture)} {point.Values[0]?.ToString(CultureInfo.InvariantCulture)}");
    }

    #endregion // Methods
}