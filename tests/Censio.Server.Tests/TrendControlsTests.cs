using Bunit;

using Censio.Server.Components;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendControls" />.
/// </summary>
[TestClass]
public sealed class TrendControlsTests
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendControlsTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TrendControlsTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The controls offer the four ranges and the two axes in the language shown, in one row with the range group first,
    /// mark the current choice, and write the range line under the row.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="range">The current range</param>
    /// <param name="axis">The current axis</param>
    /// <param name="expectedLine">The range line</param>
    /// <param name="expectedRange">The text of the selected range</param>
    /// <param name="expectedAxis">The text of the selected axis</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(UiLanguage.English, ChartRange.LastHour, ChartAxis.RealTime, "Last hour up to the latest tick", "Last hour", "Real time")]
    [DataRow(UiLanguage.English, ChartRange.LastDay, ChartAxis.GameTime, "Last 24 hours up to the latest tick", "Last 24 hours", "Game time")]
    [DataRow(UiLanguage.English, ChartRange.LastWeek, ChartAxis.RealTime, "Last 7 days up to the latest tick", "Last 7 days", "Real time")]
    [DataRow(UiLanguage.English, ChartRange.All, ChartAxis.GameTime, "All history up to the latest tick", "All", "Game time")]
    [DataRow(UiLanguage.German, ChartRange.LastHour, ChartAxis.GameTime, "Letzte Stunde bis zum letzten Tick", "Letzte Stunde", "Spielzeit")]
    [DataRow(UiLanguage.German, ChartRange.LastDay, ChartAxis.RealTime, "Letzte 24 Stunden bis zum letzten Tick", "Letzte 24 Stunden", "Echtzeit")]
    [DataRow(UiLanguage.German, ChartRange.LastWeek, ChartAxis.GameTime, "Letzte 7 Tage bis zum letzten Tick", "Letzte 7 Tage", "Spielzeit")]
    [DataRow(UiLanguage.German, ChartRange.All, ChartAxis.RealTime, "Gesamter Verlauf bis zum letzten Tick", "Gesamt", "Echtzeit")]
    public async Task TrendControlsShowRangesAxesAndRangeLine(UiLanguage language, ChartRange range, ChartAxis axis, string expectedLine, string expectedRange, string expectedAxis)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.ChartChoice = new ChartChoice
                           {
                               Range = range,
                               Axis = axis
                           };

        // Act
        var controls = host.Render<TrendControls>(language,
                                                  _ =>
                                                  {
                                                  });

        // Assert
        var german = language == UiLanguage.German;
        var rangeGroup = german ? "Zeitraum" : "Period";
        var axisGroup = german ? "Zeitachse" : "Time axis";
        var rangeItems = german ? "Letzte Stunde|Letzte 24 Stunden|Letzte 7 Tage|Gesamt" : "Last hour|Last 24 hours|Last 7 days|All";
        var axisItems = german ? "Echtzeit|Spielzeit" : "Real time|Game time";

        Assert.AreEqual(rangeItems, string.Join('|', PageTestHost.Items(controls, rangeGroup)), "The range group should offer the four ranges");
        Assert.AreEqual(axisItems, string.Join('|', PageTestHost.Items(controls, axisGroup)), "The axis group should offer the two axes");
        Assert.AreEqual(expectedRange, string.Join('|', PageTestHost.Selected(controls, rangeGroup)), "Only the current range should be selected");
        Assert.AreEqual(expectedAxis, string.Join('|', PageTestHost.Selected(controls, axisGroup)), "Only the current axis should be selected");
        Assert.AreEqual(expectedLine, controls.Find("p").TextContent.Trim(), "The range line should name the range");
        Assert.HasCount(1, controls.FindAll("div.censio-chart-controls [role='group']").Where(group => group.GetAttribute("aria-label") == rangeGroup).ToList(), "The range group should sit in the controls row");
        Assert.AreEqual(rangeGroup, controls.FindAll("div.censio-chart-controls [role='group']")[0].GetAttribute("aria-label"), "The range group should come first in the row");
        Assert.AreEqual(axisGroup, controls.FindAll("div.censio-chart-controls [role='group']")[1].GetAttribute("aria-label"), "The axis group should follow the range group");
    }

    /// <summary>
    /// Choosing another range or axis hands the new choice, with the other part unchanged, to the cascaded callback.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="group">The accessible name of the group</param>
    /// <param name="item">The item clicked</param>
    /// <param name="expectedRange">The range handed over</param>
    /// <param name="expectedAxis">The axis handed over</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(UiLanguage.English, "Period", "Last hour", ChartRange.LastHour, ChartAxis.RealTime)]
    [DataRow(UiLanguage.English, "Period", "Last 7 days", ChartRange.LastWeek, ChartAxis.RealTime)]
    [DataRow(UiLanguage.English, "Period", "All", ChartRange.All, ChartAxis.RealTime)]
    [DataRow(UiLanguage.English, "Time axis", "Game time", ChartRange.LastDay, ChartAxis.GameTime)]
    [DataRow(UiLanguage.German, "Zeitraum", "Gesamt", ChartRange.All, ChartAxis.RealTime)]
    [DataRow(UiLanguage.German, "Zeitachse", "Spielzeit", ChartRange.LastDay, ChartAxis.GameTime)]
    public async Task TrendControlsClickInvokesChooseChartWithChoice(UiLanguage language, string group, string item, ChartRange expectedRange, ChartAxis expectedAxis)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        var controls = host.Render<TrendControls>(language,
                                                  _ =>
                                                  {
                                                  });

        // Act
        await PageTestHost.ChooseAsync(controls, group, item);

        // Assert
        var expected = new ChartChoice
                       {
                           Range = expectedRange,
                           Axis = expectedAxis
                       };

        Assert.HasCount(1, host.Chosen, $"Clicking '{item}' should hand one choice to the callback");
        Assert.AreEqual(expected, host.Chosen[0], $"Clicking '{item}' should change only its own part of the choice");
    }

    #endregion // Methods
}