using System.Globalization;
using System.Text.RegularExpressions;

using AngleSharp.Dom;

using Bunit;
using Bunit.JSInterop;

using Censio.Server.Components;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the trend chart of a data page, <see cref="TrendChart" />.
/// </summary>
[TestClass]
public sealed partial class TrendChartTests
{
    #region Constants

    /// <summary>
    /// The browser function that gives the UTC offset of an instant.
    /// </summary>
    private const string UtcOffsetMinutes = "censio.utcOffsetMinutes";

    /// <summary>
    /// The selector of the drawn chart.
    /// </summary>
    private const string ChartSelector = "svg.mud-chart-line";

    /// <summary>
    /// The selector of the hover points of the drawn chart.
    /// </summary>
    private const string PointSelector = "svg.mud-chart-line circle";

    /// <summary>
    /// The heading of the charts of the tests.
    /// </summary>
    private const string Heading = "This island";

    /// <summary>
    /// How long the test waits to see that nothing changes, in milliseconds.
    /// </summary>
    private const int NegativeWaitMilliseconds = 300;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The receive time of the latest tick of the first read.
    /// </summary>
    private static readonly DateTimeOffset _asOf = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The receive time of the latest tick of the second read.
    /// </summary>
    private static readonly DateTimeOffset _laterAsOf = new(2026, 11, 13, 12, 2, 0, TimeSpan.Zero);

    /// <summary>
    /// The time of the first point of the charts.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 13, 11, 0, 0, TimeSpan.Zero);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendChartTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TrendChartTests(TestContext testContext)
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
    /// A chart whose range holds more points than it shows names the newest 2,880 and the count under the chart, in the
    /// language shown; a chart that shows every point of its range has no such line.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="totalCount">The number of points the range holds</param>
    /// <param name="expectedPattern">The pattern of the line, or an empty text for none</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(UiLanguage.English, 3000, "Showing the newest 2,880 of 3[,.]?000 points\\.")]
    [DataRow(UiLanguage.German, 3000, "Die neuesten 2\\.880 von 3[,.]?000 Punkten angezeigt\\.")]
    [DataRow(UiLanguage.English, 10, "")]
    [DataRow(UiLanguage.German, 10, "")]
    public async Task TrendChartOverCapShowsNewestAndCapLine(UiLanguage language, int totalCount, string expectedPattern)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var chart = RenderChart(host, language, TrendKind.Good, CreateRead(TickView(10, totalCount)), _asOf, 1);

        // Assert
        await chart.WaitForAssertionAsync(() => Assert.HasCount(1, chart.FindAll(ChartSelector), "The chart should be drawn"), PageTestHost.WaitBound);

        var text = PageTestHost.Text(chart);

        if (expectedPattern.Length > 0)
        {
            Assert.IsTrue(Regex.IsMatch(text, expectedPattern), $"The chart should name the cap and the count in {language}, but shows '{text}'");
        }
        else
        {
            Assert.DoesNotContain("newest", text, StringComparison.OrdinalIgnoreCase, "A chart that shows all its points should have no cap line");
            Assert.DoesNotContain("neuesten", text, StringComparison.OrdinalIgnoreCase, "A chart that shows all its points should have no cap line");
        }
    }

    /// <summary>
    /// A new read of the chart keeps the drawn chart while the browser's answer about the new instant is outstanding, then
    /// draws the new read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task TrendChartNewReadKeepsChartUntilOffsetAnswers()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        SetupOffsetFor(host, _asOf).SetResult(60);

        var pending = SetupOffsetFor(host, _laterAsOf);
        var chart = RenderChart(host, UiLanguage.English, TrendKind.Good, CreateRead(TickView(3, 3)), _asOf, 1);

        await chart.WaitForAssertionAsync(() => Assert.HasCount(9, chart.FindAll(PointSelector), "The first read should be drawn with three lines of three points"), PageTestHost.WaitBound);

        // Act
        chart.Render(parameters => parameters.Add(component => component.Read, CreateRead(TickView(4, 4))).Add(component => component.AsOf, _laterAsOf).Add(component => component.ReadNumber, 2));
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        var pointsWhileAsking = chart.FindAll(PointSelector).Count;
        var textWhileAsking = PageTestHost.Text(chart);

        pending.SetResult(120);

        // Assert
        Assert.AreEqual(9, pointsWhileAsking, "The chart should keep showing the first read until the browser answers");
        Assert.DoesNotContain("Loading", textWhileAsking, "The chart should not fall back to the loading text while it asks");
        await chart.WaitForAssertionAsync(() => Assert.HasCount(12, chart.FindAll(PointSelector), "The new read should be drawn once the browser answered"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// Hovering a point shows the time at the browser's offset, or in UTC without an answer, the mark of an hourly point and
    /// each line's value formatted for the language.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="language">The language shown</param>
    /// <param name="offsetMinutes">The offset the browser gives, or <see langword="null" /> when it gives none</param>
    /// <param name="pointIndex">The index of the hovered point</param>
    /// <param name="expectedTexts">The texts the tooltip holds, joined by <c>|</c></param>
    /// <param name="unexpectedText">A text the tooltip does not hold, or an empty text</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(TrendKind.Good, UiLanguage.English, 60, 1, "2026-11-13 12:02|Production: 1,234.50|Consumption: 2.50|Delta: +1.25", "Hourly average")]
    [DataRow(TrendKind.Good, UiLanguage.German, 60, 1, "2026-11-13 12:02|Produktion: 1.234,50|Verbrauch: 2,50|Delta: +1,25", "Stundenmittel")]
    [DataRow(TrendKind.Good, UiLanguage.English, 60, 0, "2026-11-13 11:00|Hourly average|Production: 10.50|Consumption: 20.25|Delta: -3.00", "")]
    [DataRow(TrendKind.Good, UiLanguage.German, 60, 0, "2026-11-13 11:00|Stundenmittel|Produktion: 10,50|Verbrauch: 20,25|Delta: -3,00", "")]
    [DataRow(TrendKind.Good, UiLanguage.English, null, 1, "2026-11-13 11:02 UTC|Production: 1,234.50", "")]
    [DataRow(TrendKind.Figures, UiLanguage.English, 60, 1, "2026-11-13 12:02|Buildings: 3|Produced goods: 2|Goods in deficit: 1", "Hourly average")]
    [DataRow(TrendKind.Figures, UiLanguage.German, 60, 1, "2026-11-13 12:02|Gebäude: 3|Produzierte Waren: 2|Waren im Defizit: 1", "Stundenmittel")]
    public async Task TrendChartTooltipShowsTimeHourlyMarkAndValues(TrendKind kind, UiLanguage language, int? offsetMinutes, int pointIndex, string expectedTexts, string unexpectedText)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        if (offsetMinutes is { } offset)
        {
            host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetResult(offset);
        }
        else
        {
            host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetException(new JSException("no offset"));
        }

        var chart = RenderChart(host, language, kind, CreateRead(TooltipView(kind)), _asOf, 1);

        await chart.WaitForAssertionAsync(() => Assert.HasCount(9, chart.FindAll(PointSelector), "The chart should be drawn with three lines of three points"), PageTestHost.WaitBound);

        // Act
        await chart.FindAll(PointSelector)[pointIndex].TriggerEventAsync("onmouseover", new MouseEventArgs());

        // Assert
        var tooltip = chart.Find(".mud-charts-tooltip").TextContent;

        foreach (var expected in expectedTexts.Split('|'))
        {
            Assert.Contains(expected, tooltip, $"The tooltip should hold '{expected}', but holds '{tooltip}'");
        }

        if (unexpectedText.Length > 0)
        {
            Assert.DoesNotContain(unexpectedText, tooltip, $"The tooltip should not hold '{unexpectedText}'");
        }
    }

    /// <summary>
    /// A chart without a receive time asks the browser for no offset and shows its times in UTC.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task TrendChartWithoutAsOfShowsUtcTimes()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetResult(60);

        var chart = RenderChart(host, UiLanguage.English, TrendKind.Good, CreateRead(TooltipView(TrendKind.Good)), null, 1);

        await chart.WaitForAssertionAsync(() => Assert.HasCount(9, chart.FindAll(PointSelector), "The chart should be drawn"), PageTestHost.WaitBound);

        // Act
        await chart.FindAll(PointSelector)[1].TriggerEventAsync("onmouseover", new MouseEventArgs());

        // Assert
        Assert.Contains("2026-11-13 11:02 UTC", chart.Find(".mud-charts-tooltip").TextContent, "The time should be shown in UTC when there is no receive time to ask the offset at");
    }

    /// <summary>
    /// A chart of one point shows the tooltip of that point.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task TrendChartSinglePointShowsTooltip()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetResult(60);

        var chart = RenderChart(host, UiLanguage.English, TrendKind.Good, CreateRead(TickView(1, 1)), _asOf, 1);

        await chart.WaitForAssertionAsync(() => Assert.IsNotEmpty(chart.FindAll(PointSelector), "The chart should be drawn"), PageTestHost.WaitBound);

        // Act
        await chart.FindAll(PointSelector)[0].TriggerEventAsync("onmouseover", new MouseEventArgs());

        // Assert
        Assert.Contains("Production: 1.50", chart.Find(".mud-charts-tooltip").TextContent, "The tooltip of the only point should show its values");
    }

    /// <summary>
    /// The chart's lines and legend carry the palette of the shown theme, in line order, and the chart sits in the element
    /// whose style animates the lines.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="theme">The theme shown</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(TrendKind.Good, UiTheme.Light)]
    [DataRow(TrendKind.Good, UiTheme.Dark)]
    [DataRow(TrendKind.Figures, UiTheme.Light)]
    [DataRow(TrendKind.Figures, UiTheme.Dark)]
    public async Task TrendChartCarriesThemePaletteAndAnimationClass(TrendKind kind, UiTheme theme)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.Theme = theme;

        // Act
        var chart = RenderChart(host, UiLanguage.English, kind, CreateRead(TickView(3, 3)), _asOf, 1);

        // Assert
        await chart.WaitForAssertionAsync(() => Assert.HasCount(1, chart.FindAll(ChartSelector), "The chart should be drawn"), PageTestHost.WaitBound);

        var expected = TrendPalette.For(kind, theme).ToList();

        Assert.AreSequenceEqual(expected, chart.FindAll("path.mud-chart-line").Select(line => line.GetAttribute("stroke")!).ToList(), $"The lines should take the {theme} palette in line order");
        Assert.AreSequenceEqual(expected, chart.FindAll(".mud-chart-legend-marker").Select(marker => MarkerColor().Match(marker.GetAttribute("style") ?? string.Empty).Groups[1].Value.Trim()).ToList(), $"The legend should take the {theme} palette in line order");
        Assert.HasCount(1, chart.FindAll(".censio-chart svg.mud-chart-line"), "The chart should sit in the element that animates its lines");
    }

    /// <summary>
    /// A heading with markup is shown as text, not as markup.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task TrendChartMarkupInNamesIsText()
    {
        // Arrange
        const string heading = "<img src=x onerror=alert(1)> & <b>bold</b><script>alert(2)</script>";

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var chart = RenderChart(host, UiLanguage.English, TrendKind.Good, CreateRead(TickView(3, 3)), _asOf, 1, heading);

        // Assert
        await chart.WaitForAssertionAsync(() => Assert.HasCount(1, chart.FindAll(ChartSelector), "The chart should be drawn"), PageTestHost.WaitBound);

        Assert.AreEqual(heading, chart.Find("h2").TextContent.Trim(), "The heading should show its characters");
        Assert.IsEmpty(chart.FindAll("img, b, script, [onerror]"), "The chart should hold no element a name could bring in");
    }

    /// <summary>
    /// The labels of the axes hold only a number or an hour and minute: nothing else reaches the chart's raw markup.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="language">The language shown</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    [DataRow(TrendKind.Good, UiLanguage.English)]
    [DataRow(TrendKind.Good, UiLanguage.German)]
    [DataRow(TrendKind.Figures, UiLanguage.English)]
    [DataRow(TrendKind.Figures, UiLanguage.German)]
    public async Task TrendChartAxisLabelsHoldOnlyNumberAndTimeText(TrendKind kind, UiLanguage language)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        var chart = RenderChart(host, language, kind, CreateRead(TooltipView(kind)), _asOf, 1, "<img src=x onerror=alert(1)>");

        // Act
        await chart.WaitForAssertionAsync(() => Assert.HasCount(1, chart.FindAll(ChartSelector), "The chart should be drawn"), PageTestHost.WaitBound);

        // Assert
        var valueLabels = chart.FindAll(".mud-charts-yaxis > *");
        var timeLabels = chart.FindAll(".mud-charts-xaxis > *");

        Assert.IsNotEmpty(valueLabels, "The value axis should have labels");
        Assert.IsNotEmpty(timeLabels, "The time axis should have labels");
        Assert.IsTrue(valueLabels.All(label => label.LocalName == "text" && NumberText().IsMatch(label.TextContent)), $"Each value label should be a number: {string.Join(" ", valueLabels.Select(label => label.OuterHtml))}");
        Assert.IsTrue(timeLabels.All(label => label.LocalName == "text" && TimeText().IsMatch(label.TextContent)), $"Each time label should be an hour and minute: {string.Join(" ", timeLabels.Select(label => label.OuterHtml))}");
    }

    /// <summary>
    /// Sets up the answer of the browser for the offset at an instant; the answer is outstanding until it is given.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="instant">The instant the browser is asked about</param>
    /// <returns>The handler of the answer</returns>
    private static JSRuntimeInvocationHandler<int> SetupOffsetFor(PageTestHost host, DateTimeOffset instant)
    {
        var asked = instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        return host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, invocation => invocation.Arguments.Count == 1 && string.Equals(invocation.Arguments[0]?.ToString(), asked, StringComparison.Ordinal));
    }

    /// <summary>
    /// Creates a successful read of a view.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The read</returns>
    private static TrendRead CreateRead(TrendView view)
    {
        return new TrendRead
               {
                   View = view,
                   IsFailed = false
               };
    }

    /// <summary>
    /// Creates a view of ticks two minutes apart, with values that differ by point and line.
    /// </summary>
    /// <param name="count">The number of points</param>
    /// <param name="totalCount">The number of points the range holds</param>
    /// <returns>The view</returns>
    private static TrendView TickView(int count, int totalCount)
    {
        return new TrendView
               {
                   Points = [
                                .. Enumerable.Range(0, count).Select(index => new TrendPoint
                                                                              {
                                                                                  Time = _start.AddMinutes(2 * index),
                                                                                  IsHourly = false,
                                                                                  Values = [index + 1.5, index + 2.5, index - 1.0]
                                                                              })
                            ],
                   TotalCount = totalCount
               };
    }

    /// <summary>
    /// Creates a view of an hourly point and two ticks that follow it without a gap, with values of different size.
    /// </summary>
    /// <param name="kind">The kind of the chart</param>
    /// <returns>The view</returns>
    private static TrendView TooltipView(TrendKind kind)
    {
        double[][] good = [[10.5, 20.25, -3], [1234.5, 2.5, 1.25], [1.5, 1.5, 0]];
        double[][] figures = [[10, 5, 2], [3, 2, 1], [4, 3, 0]];
        var values = kind == TrendKind.Good ? good : figures;

        return new TrendView
               {
                   Points = [
                                new TrendPoint
                                {
                                    Time = _start.AddHours(-1),
                                    IsHourly = true,
                                    Values = [.. values[0].Select(value => (double?)value)]
                                },
                                new TrendPoint
                                {
                                    Time = _start.AddMinutes(2),
                                    IsHourly = false,
                                    Values = [.. values[1].Select(value => (double?)value)]
                                },
                                new TrendPoint
                                {
                                    Time = _start.AddMinutes(4),
                                    IsHourly = false,
                                    Values = [.. values[2].Select(value => (double?)value)]
                                }
                            ],
                   TotalCount = 3
               };
    }

    /// <summary>
    /// Renders a chart.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <param name="kind">The kind of the chart</param>
    /// <param name="read">The read the chart shows</param>
    /// <param name="asOf">The receive time of the latest tick</param>
    /// <param name="readNumber">The number of the read</param>
    /// <param name="heading">The heading of the chart</param>
    /// <returns>The rendered chart</returns>
    private static IRenderedComponent<TrendChart> RenderChart(PageTestHost host, UiLanguage language, TrendKind kind, TrendRead? read, DateTimeOffset? asOf, int readNumber, string heading = Heading)
    {
        return host.Render<TrendChart>(language,
                                       parameters => parameters.Add(component => component.Heading, heading)
                                                               .Add(component => component.Kind, kind)
                                                               .Add(component => component.Read, read)
                                                               .Add(component => component.AsOf, asOf)
                                                               .Add(component => component.ReadNumber, readNumber));
    }

    /// <summary>
    /// Gets the expression of the background color of a legend marker.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("background-color:\\s*([^;]+)")]
    private static partial Regex MarkerColor();

    /// <summary>
    /// Gets the expression of a value label: a signed number.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("^[0-9.,+\\-–]+$")]
    private static partial Regex NumberText();

    /// <summary>
    /// Gets the expression of a time label: an hour and a minute.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("^[0-9]{2}:[0-9]{2}$")]
    private static partial Regex TimeText();

    #endregion // Methods
}