using System.Globalization;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendRange" />.
/// </summary>
[TestClass]
public sealed class TrendRangeTests
{
    #region Methods

    /// <summary>
    /// The range starts at the start of the UTC hour that holds the time 24 hours before the latest tick, so that hour is
    /// in and the hour before it is not, and ends right after the latest tick, which the range includes.
    /// </summary>
    /// <param name="latestText">The receive time of the latest tick</param>
    /// <param name="expectedStartText">The start of the range</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("2026-06-11T00:21:36.5503617+00:00", "2026-06-10T00:00:00+00:00")]
    [DataRow("2026-06-11T10:00:00+00:00", "2026-06-10T10:00:00+00:00")]
    [DataRow("2026-06-11T10:59:59.9999999+00:00", "2026-06-10T10:00:00+00:00")]
    [DataRow("2026-06-11T10:00:00.0000001+00:00", "2026-06-10T10:00:00+00:00")]
    [DataRow("2026-06-11T09:59:59.9999999+00:00", "2026-06-10T09:00:00+00:00")]
    [DataRow("2026-06-11T12:30:00+02:00", "2026-06-10T10:00:00+00:00")]
    public void TrendRangeBeforeStartsAtHourOf24HoursBack(string latestText, string expectedStartText)
    {
        // Arrange
        var latest = DateTimeOffset.Parse(latestText, CultureInfo.InvariantCulture);
        var expectedStart = DateTimeOffset.Parse(expectedStartText, CultureInfo.InvariantCulture);

        // Act
        var range = TrendRange.Before(latest);

        // Assert
        Assert.AreEqual(expectedStart.UtcDateTime, range.Start.UtcDateTime, "The range should start at the UTC hour that holds the time 24 hours before the latest tick");
        Assert.IsLessThanOrEqualTo(latest.AddHours(-24), range.Start, "The start hour should not begin after the time 24 hours back");
        Assert.IsGreaterThan(latest.AddHours(-24), range.Start.AddHours(1), "The start hour should hold the time 24 hours back");
        Assert.AreEqual(latest.AddTicks(1).UtcDateTime, range.End.UtcDateTime, "The range should end, exclusive, one tick of time after the latest tick");
    }

    #endregion // Methods
}