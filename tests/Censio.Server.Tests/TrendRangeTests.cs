using System.Globalization;

using Censio.Server.Data;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendRange" />.
/// </summary>
[TestClass]
public sealed class TrendRangeTests
{
    #region Methods

    /// <summary>
    /// A range starts at the start of the UTC hour that holds the latest tick's receive time minus 1 hour, 24 hours or
    /// 7 days, so that hour is in and the hour before it is not, and ends right after the latest tick, which the range
    /// includes; no receive time throws, the start and end are clamped at the ends of the time scale.
    /// </summary>
    /// <param name="range">The range</param>
    /// <param name="latestText">The receive time of the latest tick</param>
    /// <param name="expectedStartText">The start of the range</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(ChartRange.LastHour, "2026-06-11T00:21:36.5503617+00:00", "2026-06-10T23:00:00+00:00")]
    [DataRow(ChartRange.LastHour, "2026-06-11T10:00:00+00:00", "2026-06-11T09:00:00+00:00")]
    [DataRow(ChartRange.LastHour, "2026-06-11T10:59:59.9999999+00:00", "2026-06-11T09:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-06-11T00:21:36.5503617+00:00", "2026-06-10T00:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-06-11T10:00:00+00:00", "2026-06-10T10:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-06-11T10:59:59.9999999+00:00", "2026-06-10T10:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-06-11T10:00:00.0000001+00:00", "2026-06-10T10:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-06-11T09:59:59.9999999+00:00", "2026-06-10T09:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "2026-06-11T12:30:00+02:00", "2026-06-10T10:00:00+00:00")]
    [DataRow(ChartRange.LastWeek, "2026-06-11T00:21:36.5503617+00:00", "2026-06-04T00:00:00+00:00")]
    [DataRow(ChartRange.LastWeek, "2026-06-11T10:59:59.9999999+00:00", "2026-06-04T10:00:00+00:00")]
    [DataRow(ChartRange.LastHour, "0001-01-01T00:00:00+00:00", "0001-01-01T00:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "0001-01-01T00:00:00+00:00", "0001-01-01T00:00:00+00:00")]
    [DataRow(ChartRange.LastWeek, "0001-01-01T00:00:00+00:00", "0001-01-01T00:00:00+00:00")]
    [DataRow(ChartRange.LastHour, "9999-12-31T23:59:59.9999999+00:00", "9999-12-31T22:00:00+00:00")]
    [DataRow(ChartRange.LastDay, "9999-12-31T23:59:59.9999999+00:00", "9999-12-30T23:00:00+00:00")]
    [DataRow(ChartRange.LastWeek, "9999-12-31T23:59:59.9999999+00:00", "9999-12-24T23:00:00+00:00")]
    public void TrendRangeForStartsAtHourOfRangeBack(ChartRange range, string latestText, string expectedStartText)
    {
        // Arrange
        var latest = DateTimeOffset.Parse(latestText, CultureInfo.InvariantCulture);
        var expectedStart = DateTimeOffset.Parse(expectedStartText, CultureInfo.InvariantCulture);
        var expectedEnd = latest.UtcTicks == DateTimeOffset.MaxValue.UtcTicks ? DateTimeOffset.MaxValue : latest.AddTicks(1);

        // Act
        var result = TrendRange.For(range, latest);

        // Assert
        Assert.AreEqual(expectedStart.UtcDateTime, result.Start.UtcDateTime, $"The range {range} should start at the UTC hour that holds its time back from the latest tick");
        Assert.AreEqual(expectedEnd.UtcDateTime, result.End.UtcDateTime, "The range should end, exclusive, one tick of time after the latest tick, at most at the end of the time scale");
    }

    /// <summary>
    /// The range of all history starts at the beginning of the time scale and ends right after the latest tick.
    /// </summary>
    /// <param name="latestText">The receive time of the latest tick</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("2026-06-11T10:00:00+00:00")]
    [DataRow("0001-01-01T00:00:00+00:00")]
    public void TrendRangeForAllStartsAtBeginning(string latestText)
    {
        // Arrange
        var latest = DateTimeOffset.Parse(latestText, CultureInfo.InvariantCulture);

        // Act
        var result = TrendRange.For(ChartRange.All, latest);

        // Assert
        Assert.AreEqual(DateTimeOffset.MinValue.UtcDateTime, result.Start.UtcDateTime, "All history should start at the beginning of the time scale");
        Assert.AreEqual(latest.AddTicks(1).UtcDateTime, result.End.UtcDateTime, "All history should end, exclusive, one tick of time after the latest tick");
    }

    /// <summary>
    /// The hour and the day are read tick by tick, the week and all history as hourly means.
    /// </summary>
    /// <param name="range">The range</param>
    /// <param name="expected">The resolution</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(ChartRange.LastHour, HistoryTrendResolution.Points)]
    [DataRow(ChartRange.LastDay, HistoryTrendResolution.Points)]
    [DataRow(ChartRange.LastWeek, HistoryTrendResolution.Hours)]
    [DataRow(ChartRange.All, HistoryTrendResolution.Hours)]
    public void TrendRangeResolutionOfGivesHoursForWideRanges(ChartRange range, HistoryTrendResolution expected)
    {
        // Act
        var resolution = TrendRange.ResolutionOf(range);

        // Assert
        Assert.AreEqual(expected, resolution, $"The range {range} should be read at its resolution");
    }

    #endregion // Methods
}