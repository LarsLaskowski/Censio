using System.Text.RegularExpressions;

using AngleSharp.Dom;

using Bunit;

using Censio.Contracts;
using Censio.Server.Components.Layout;
using Censio.Server.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the charts of one good of an island, <see cref="GoodTrend" />.
/// </summary>
[TestClass]
public sealed partial class GoodTrendTests
{
    #region Constants

    /// <summary>
    /// The session GUID of the shown island.
    /// </summary>
    private const int Session = 3245;

    /// <summary>
    /// The product GUID of the shown good.
    /// </summary>
    private const int Good = 1001;

    /// <summary>
    /// The selector of the drawn charts.
    /// </summary>
    private const string ChartSelector = "svg.mud-chart-line";

    /// <summary>
    /// The text the page shows for a profile that is not found.
    /// </summary>
    private const string NotFound = "Not found.";

    /// <summary>
    /// How long the test waits to see that nothing changes, in milliseconds.
    /// </summary>
    private const int NegativeWaitMilliseconds = 400;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The profile of the tests.
    /// </summary>
    private static readonly Guid _profile = new("11111111-1111-4111-8111-111111111111");

    /// <summary>
    /// Another profile.
    /// </summary>
    private static readonly Guid _otherProfile = new("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// The time of the last point the fake gives.
    /// </summary>
    private static readonly DateTimeOffset _pointEnd = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The hover points of two charts of two points in each of three lines.
    /// </summary>
    private static readonly int[] _twoSmallCharts = [6, 6];

    /// <summary>
    /// The hover points of two charts of three points in each of three lines.
    /// </summary>
    private static readonly int[] _twoLargerCharts = [9, 9];

    /// <summary>
    /// The English chart headings.
    /// </summary>
    private static readonly string[] _englishHeadings = ["This island", "All islands of the region"];

    /// <summary>
    /// The German chart headings.
    /// </summary>
    private static readonly string[] _germanHeadings = ["Diese Insel", "Alle Inseln der Region"];

    /// <summary>
    /// The German names of the lines of a good chart.
    /// </summary>
    private static readonly string[] _germanLegend = ["Produktion", "Verbrauch", "Delta"];

    /// <summary>
    /// The attributes that hold an address.
    /// </summary>
    private static readonly string[] _addressAttributes = ["src", "href", "xlink:href", "action", "srcset", "data"];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GoodTrendTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public GoodTrendTests(TestContext testContext)
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
    /// The page shows the good's name as heading and title, the island and region as links with the profile under it, the
    /// range line, and the headings and legends of both charts, in the language shown.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="goodName">The shown name of the good</param>
    /// <param name="regionName">The shown name of the region</param>
    /// <param name="range">The range line</param>
    /// <param name="headings">The chart headings, joined by <c>|</c></param>
    /// <param name="legend">The names of the lines, joined by <c>|</c></param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, "Wood", "Latium", "Last 24 hours up to the latest tick", "This island|All islands of the region", "Production|Consumption|Delta")]
    [DataRow(UiLanguage.German, "Holz", "Latium DE", "Letzte 24 Stunden bis zum letzten Tick", "Diese Insel|Alle Inseln der Region", "Produktion|Verbrauch|Delta")]
    public async Task GoodTrendShowsHeadingLinksAndChartHeadings(UiLanguage language, string goodName, string regionName, string range, string headings, string legend)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.GoodEntry(Good, "Wood", "Holz", null), PageTestHost.RegionEntry(Session, 900001, "Latium", "Latium DE"));
        await StoreProfileAsync(host);

        var head = host.Context.Render<HeadOutlet>();

        // Act
        var page = RenderGood(host, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, page.FindAll(ChartSelector), "Both charts should be drawn"), PageTestHost.WaitBound);

        var line = page.FindAll("p").Single(paragraph => paragraph.QuerySelector("a") is not null);

        Assert.AreEqual(goodName, page.Find("h1").TextContent.Trim(), "The heading should be the good's shown name");
        Assert.AreEqual($"Censio – {goodName}", head.Find("title").TextContent, "The page title should name the good");
        Assert.AreEqual($"Isle A · {regionName} · Test profile", WhitespaceRun().Replace(line.TextContent, " ").Trim(), "The line should name the island, the region and the profile");
        Assert.AreSequenceEqual(new[] { PageAddress.IslandLink(_profile, new IslandKey(Session, 1, 0)), PageAddress.RegionLink(_profile, Session) }, line.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList(), "The island should link to its detail and the region to its islands");
        Assert.Contains(range, page.FindAll("p").Select(paragraph => paragraph.TextContent.Trim()).ToList(), $"The page should show the range line '{range}'");
        Assert.AreSequenceEqual(headings.Split('|'), page.FindAll("h2").Select(heading => heading.TextContent.Trim()).ToList(), "The page should show the heading of each chart");
        Assert.AreSequenceEqual(legend.Split('|'), page.FindAll(ChartSelector)[0].ParentElement!.QuerySelectorAll(".mud-chart-legend-item").Select(item => item.TextContent.Trim()).ToList(), "The chart should name its three lines in the language shown");
    }

    /// <summary>
    /// A malformed or unknown profile, island or good shows the not-found text without charts or as-of line, and reads no
    /// chart.
    /// </summary>
    /// <param name="profile">The profile segment, <c>own</c> for the stored profile</param>
    /// <param name="session">The session segment</param>
    /// <param name="island">The island segment</param>
    /// <param name="area">The area segment</param>
    /// <param name="good">The good segment</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("own", "3245", "1", "0", "x7q")]
    [DataRow("own", "3245", "1", "0", "12345678901")]
    [DataRow("own", "3245", "1", "0", "")]
    [DataRow("own", "3245", "9", "0", "1001")]
    [DataRow("own", "9999", "1", "0", "1001")]
    [DataRow("own", "3245", "1", "-1", "1001")]
    [DataRow("own", "x7q", "1", "0", "1001")]
    [DataRow("not-a-profile", "3245", "1", "0", "1001")]
    [DataRow("33333333-3333-4333-8333-333333333333", "3245", "1", "0", "1001")]
    public async Task GoodTrendMalformedOrUnknownAddressShowsNotFound(string profile, string session, string island, string area, string good)
    {
        // Arrange
        var fake = new FakeHistoryTrends();

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        // Act
        var page = host.Render<GoodTrend>(UiLanguage.English,
                                          parameters => parameters.Add(component => component.Profile, profile == "own" ? _profile.ToString() : profile)
                                                                  .Add(component => component.Session, session)
                                                                  .Add(component => component.Island, island)
                                                                  .Add(component => component.Area, area)
                                                                  .Add(component => component.Good, good));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains(NotFound, PageTestHost.Text(page), "An unknown or malformed address should show the not-found text"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.IsEmpty(page.FindAll("h2"), "A page that is not found should show no chart heading");
        Assert.IsEmpty(page.FindAll(ChartSelector), "A page that is not found should show no chart");
        Assert.DoesNotContain("As of", PageTestHost.Text(page), "A page that is not found should show no as-of line");
        Assert.AreEqual(0, fake.CallCount, "A page that is not found should read no chart");
    }

    /// <summary>
    /// The charts show the loading text while their reads run, the no-values text for a range without a value, the reload
    /// text for a failed read, and a profile without a tick shows the no-data text and no chart, in both languages.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="state">The state: <c>Loading</c>, <c>NoValues</c>, <c>Failed</c> or <c>NoData</c></param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(UiLanguage.English, "Loading")]
    [DataRow(UiLanguage.German, "Loading")]
    [DataRow(UiLanguage.English, "NoValues")]
    [DataRow(UiLanguage.German, "NoValues")]
    [DataRow(UiLanguage.English, "Failed")]
    [DataRow(UiLanguage.German, "Failed")]
    [DataRow(UiLanguage.English, "NoData")]
    [DataRow(UiLanguage.German, "NoData")]
    public async Task GoodTrendStatesShowTexts(UiLanguage language, string state)
    {
        // Arrange
        var fake = new FakeHistoryTrends();
        var texts = UiTexts.For(language);

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        var (expectedText, expectedCount) = await ArrangeStateAsync(host, fake, texts, state);

        // Act
        var page = RenderGood(host, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual(expectedCount, CountOf(PageTestHost.Text(page), expectedText), $"The page should show '{expectedText}' {expectedCount} time(s) in the state {state}"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll(ChartSelector), $"No chart should be drawn in the state {state}");
    }

    /// <summary>
    /// After a chart read failed, a notice of the server state reads no chart again and the charts keep the reload text.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task GoodTrendNoticeAfterFailedReadReadsNoMore()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       Failure = new InvalidOperationException("The history is gone")
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        var page = RenderGood(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreEqual(2, CountOf(PageTestHost.Text(page), UiTexts.For(UiLanguage.English).ReadFailed), "Both charts should show the reload text"), PageTestHost.WaitBound);

        var callsAfterFailure = fake.CallCount;

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, IslandA(), IslandB()));
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(callsAfterFailure, fake.CallCount, "A notice should not read a chart again after a read failed");
        Assert.AreEqual(2, CountOf(PageTestHost.Text(page), UiTexts.For(UiLanguage.English).ReadFailed), "The charts should keep the reload text");
    }

    /// <summary>
    /// A tick of the page's profile stored while the page is open reads and redraws every chart without a reload.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendTickOfProfileRedrawsCharts()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        var page = RenderGood(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_twoSmallCharts, PointCounts(page), "Both charts should show three lines of two points"), PageTestHost.WaitBound);

        fake.GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd.AddMinutes(2), 3);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, IslandA(), IslandB()));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_twoLargerCharts, PointCounts(page), "Both charts should show the new point"), PageTestHost.WaitBound);
        Assert.AreEqual(4, fake.CallCount, "Each chart should have been read once more");
    }

    /// <summary>
    /// A tick of another profile changes nothing on the page and reads no chart.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendTickOfOtherProfileChangesNothing()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        var page = RenderGood(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_twoSmallCharts, PointCounts(page), "Both charts should show three lines of two points"), PageTestHost.WaitBound);

        var markup = page.Markup;

        // Act
        await host.StoreProfileAsync(_otherProfile, "Other profile");
        await host.StoreTicksAsync(PageTestHost.Tick(_otherProfile, 300, IslandA(), IslandB()));
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(2, fake.CallCount, "No chart should be read for a tick of another profile");
        Assert.AreEqual(markup, page.Markup, "The page should not change");
    }

    /// <summary>
    /// A notice that arrives while the charts are being read causes exactly one more read of each chart.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendNoticeDuringReadReadsOnceMore()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        fake.Hold();

        var page = RenderGood(host, UiLanguage.English);

        await fake.WaitForCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, IslandA(), IslandB()));
        fake.Release();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual(4, fake.CallCount, "Each chart should be read once more after the notice"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(4, fake.CallCount, "The notice should cause one more read of each chart, not more");
        Assert.AreSequenceEqual(_twoSmallCharts, PointCounts(page), "Both charts should be drawn");
    }

    /// <summary>
    /// A theme switch in the frame recolours the charts at once, without a read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendThemeSwitchRedrawsWithoutRead()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        var layout = RenderGoodInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(TrendPalette.For(TrendKind.Good, UiTheme.Light), LineColours(layout, 0), "The chart should start in the light palette"), PageTestHost.WaitBound);

        var callsBefore = fake.CallCount;

        // Act
        await layout.Find("button[aria-label='Switch to the dark theme']").ClickAsync(new MouseEventArgs());

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(TrendPalette.For(TrendKind.Good, UiTheme.Dark), LineColours(layout, 0), "The first chart should take the dark palette"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(TrendPalette.For(TrendKind.Good, UiTheme.Dark), LineColours(layout, 1), "The second chart should take the dark palette");
        Assert.AreEqual(callsBefore, fake.CallCount, "A theme switch should read no chart");
    }

    /// <summary>
    /// A language switch in the frame retranslates the headings and the legends of the charts at once, without a read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendLanguageSwitchRedrawsWithoutRead()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        var layout = RenderGoodInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_englishHeadings, layout.FindAll("h2").Select(heading => heading.TextContent.Trim()).ToList(), "The charts should start in English"), PageTestHost.WaitBound);

        var callsBefore = fake.CallCount;

        // Act
        await layout.Find("button[aria-label='Language']").ClickAsync(new MouseEventArgs());
        await layout.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == "Deutsch").ClickAsync(new MouseEventArgs());

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_germanHeadings, layout.FindAll("h2").Select(heading => heading.TextContent.Trim()).ToList(), "The headings should be German after the switch"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(_germanLegend, layout.FindAll(ChartSelector)[0].ParentElement!.QuerySelectorAll(".mud-chart-legend-item").Select(item => item.TextContent.Trim()).ToList(), "The legend should be German after the switch");
        Assert.AreEqual(callsBefore, fake.CallCount, "A language switch should read no chart");
    }

    /// <summary>
    /// A new good in the address of the open page shows the loading text in both charts until the reads of that good end,
    /// not the charts of the good before.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendOtherGoodShowsLoadingUntilItsRead()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        var page = RenderGood(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_twoSmallCharts, PointCounts(page), "Both charts should be drawn"), PageTestHost.WaitBound);

        fake.Hold();

        // Act
        page.Render(parameters => parameters.Add(component => component.Good, "1002"));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual(2, CountOf(PageTestHost.Text(page), UiTexts.For(UiLanguage.English).Loading), "Both charts should show the loading text while the other good is read"), PageTestHost.WaitBound);
        await fake.WaitForCallsAsync(3, PageTestHost.WaitBound, TestContext.CancellationToken);

        Assert.IsEmpty(page.FindAll(ChartSelector), "The charts of the good before should be gone");

        fake.Release();

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_twoSmallCharts, PointCounts(page), "The charts of the other good should be drawn once read"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A read that ends after the page moved to another good is dropped: only the charts of the new good are shown.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task GoodTrendReadOfGoodLeftBehindIsDropped()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        fake.Hold();

        var page = RenderGood(host, UiLanguage.English);

        await fake.WaitForCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        page.Render(parameters => parameters.Add(component => component.Good, "1002"));

        // Act
        fake.Release();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(_twoSmallCharts, PointCounts(page), "Only the charts of the other good should be drawn"), PageTestHost.WaitBound);
        Assert.AreEqual(1002, fake.GoodQueries[^1].ProductGuid, "The last read should be the one of the other good");
    }

    /// <summary>
    /// Names that hold markup or script are shown as text on the page.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task GoodTrendMarkupInNamesIsText()
    {
        // Arrange
        const string goodName = "<img src=x onerror=alert(1)>";
        const string regionName = "<b>Region</b>";
        const string islandName = "<script>alert(2)</script>Isle";
        const string profileName = "<i onclick=alert(3)>Profile</i>";

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.GoodEntry(Good, goodName, goodName, null), PageTestHost.RegionEntry(Session, 900001, regionName, regionName));
        await host.StoreProfileAsync(_profile, profileName);
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, islandName, Entry()), PageTestHost.Island(Session, 2, "Isle B", Entry())));

        // Act
        var page = RenderGood(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, page.FindAll(ChartSelector), "Both charts should be drawn"), PageTestHost.WaitBound);

        var line = page.FindAll("p").Single(paragraph => paragraph.QuerySelector("a") is not null);

        Assert.AreEqual(goodName, page.Find("h1").TextContent.Trim(), "The heading should show the good's characters");
        Assert.AreEqual($"{islandName} · {regionName} · {profileName}", WhitespaceRun().Replace(line.TextContent, " ").Trim(), "The line should show the names' characters");
        Assert.IsEmpty(page.FindAll("img, b, i, script, [onerror], [onclick]"), "The page should hold no element a name could bring in");
    }

    /// <summary>
    /// The page and its charts load nothing from another site: every address in them leads to the server's own paths or a
    /// fragment, and the page holds no script, style sheet or frame of its own.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task GoodTrendLoadsResourcesFromServerOnly()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreProfileAsync(host);

        // Act
        var page = RenderGood(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, page.FindAll(ChartSelector), "Both charts should be drawn"), PageTestHost.WaitBound);

        var addresses = page.FindAll("[src], [href], [xlink\\:href], [action], [srcset], [data]").SelectMany(element => _addressAttributes.Select(name => element.GetAttribute(name))).OfType<string>().ToList();
        var styles = page.FindAll("[style]").Select(element => element.GetAttribute("style")!).Where(style => style.Contains("url(", StringComparison.OrdinalIgnoreCase) || style.Contains("@import", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.IsNotEmpty(addresses, "The page should link to the island and the region, or the test would prove less");
        Assert.IsEmpty(addresses.Where(address => address.StartsWith('/') == false || address.StartsWith("//", StringComparison.Ordinal)).ToList(), "Every address should lead to the server's own paths");
        Assert.IsEmpty(styles, "No style should load a resource");
        Assert.IsEmpty(page.FindAll("script, link, iframe, object, embed, style, image, use"), "The page should hold no script, style sheet, frame or image of its own");
    }

    /// <summary>
    /// A prerendered page reads no chart: its charts show the loading text until the page is interactive.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC13")]
    public async Task GoodTrendPrerenderReadsNoChart()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodTrend = FakeHistoryTrends.CreateGoodTrend(_pointEnd, 2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreProfileAsync(host);

        host.Context.SetRendererInfo(new RendererInfo("Static", false));

        // Act
        var page = RenderGood(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.IsNotEmpty(page.FindAll("p a"), "The page should show its links once its view is read"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(0, fake.CallCount, "A prerendered page should read no chart");
        Assert.AreEqual(2, CountOf(PageTestHost.Text(page), UiTexts.For(UiLanguage.English).Loading), "Both charts should show the loading text");
        Assert.IsEmpty(page.FindAll(ChartSelector), "A prerendered page should draw no chart");
    }

    /// <summary>
    /// Renders the page of the shown good.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<GoodTrend> RenderGood(PageTestHost host, UiLanguage language)
    {
        return host.Render<GoodTrend>(language,
                                      parameters => parameters.Add(component => component.Profile, _profile.ToString())
                                                              .Add(component => component.Session, Session.ToString())
                                                              .Add(component => component.Island, "1")
                                                              .Add(component => component.Area, "0")
                                                              .Add(component => component.Good, Good.ToString()));
    }

    /// <summary>
    /// Renders the page of the shown good as the body of the frame, which starts in the light theme and English.
    /// </summary>
    /// <param name="host">The host</param>
    /// <returns>The rendered frame</returns>
    private static IRenderedComponent<MainLayout> RenderGoodInFrame(PageTestHost host)
    {
        return host.Context.Render<MainLayout>(parameters => parameters.AddCascadingValue(UiTheme.Light)
                                                                       .AddCascadingValue(UiLanguage.English)
                                                                       .Add(frame => frame.Body, RenderGoodBody));
    }

    /// <summary>
    /// Renders the page of the shown good.
    /// </summary>
    /// <param name="builder">The render tree builder</param>
    private static void RenderGoodBody(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GoodTrend>(0);
        builder.AddComponentParameter(1, nameof(GoodTrend.Profile), _profile.ToString());
        builder.AddComponentParameter(2, nameof(GoodTrend.Session), Session.ToString());
        builder.AddComponentParameter(3, nameof(GoodTrend.Island), "1");
        builder.AddComponentParameter(4, nameof(GoodTrend.Area), "0");
        builder.AddComponentParameter(5, nameof(GoodTrend.Good), Good.ToString());
        builder.CloseComponent();
    }

    /// <summary>
    /// Creates the entry of the shown good.
    /// </summary>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry()
    {
        return PageTestHost.Entry(Good, 1.5f, 2.5f, -1f, 3, 90f);
    }

    /// <summary>
    /// Creates the shown island.
    /// </summary>
    /// <returns>The island</returns>
    private static ContractTickIsland IslandA()
    {
        return PageTestHost.Island(Session, 1, "Isle A", Entry());
    }

    /// <summary>
    /// Creates the other island of the region.
    /// </summary>
    /// <returns>The island</returns>
    private static ContractTickIsland IslandB()
    {
        return PageTestHost.Island(Session, 2, "Isle B", Entry());
    }

    /// <summary>
    /// Counts the hover points of each drawn chart.
    /// </summary>
    /// <param name="component">The rendered page or frame</param>
    /// <returns>The counts, one per chart</returns>
    private static List<int> PointCounts(IRenderedComponent<GoodTrend> component)
    {
        return [.. component.FindAll(ChartSelector).Select(chart => chart.QuerySelectorAll("circle").Length)];
    }

    /// <summary>
    /// Gives the colours of the lines of a drawn chart in line order.
    /// </summary>
    /// <param name="layout">The rendered frame</param>
    /// <param name="chartIndex">The index of the chart</param>
    /// <returns>The colours</returns>
    private static List<string> LineColours(IRenderedComponent<MainLayout> layout, int chartIndex)
    {
        var charts = layout.FindAll(ChartSelector);

        return chartIndex < charts.Count
                   ? [.. charts[chartIndex].QuerySelectorAll("path.mud-chart-line").Select(line => line.GetAttribute("stroke") ?? string.Empty)]
                   : [];
    }

    /// <summary>
    /// Counts how often a text occurs in another one.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="needle">The text to count</param>
    /// <returns>The number of occurrences</returns>
    private static int CountOf(string text, string needle)
    {
        return Regex.Count(text, Regex.Escape(needle));
    }

    /// <summary>
    /// Stores the profile with the shown island and another island of its region in one tick.
    /// </summary>
    /// <param name="host">The host</param>
    /// <returns>A task that completes when the profile is stored</returns>
    private static async Task StoreProfileAsync(PageTestHost host)
    {
        await host.StoreProfileAsync(_profile, "Test profile");
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, IslandA(), IslandB()));
    }

    /// <summary>
    /// Brings the host into a state and gives the text the charts show in it and how often.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="fake">The fake chart reads</param>
    /// <param name="texts">The texts of the language shown</param>
    /// <param name="state">The state</param>
    /// <returns>The text to expect and its number of occurrences</returns>
    private static async Task<(string Text, int Count)> ArrangeStateAsync(PageTestHost host, FakeHistoryTrends fake, UiTexts texts, string state)
    {
        if (state == "NoData")
        {
            await host.StoreProfileAsync(_profile, "Hello only");

            return (texts.NoData, 1);
        }

        await StoreProfileAsync(host);

        switch (state)
        {
            case "Loading":
                {
                    fake.Hold();

                    return (texts.Loading, 2);
                }

            case "Failed":
                {
                    fake.Failure = new InvalidOperationException("The history is gone");

                    return (texts.ReadFailed, 2);
                }

            default:
                {
                    return (texts.ChartNoValues, 2);
                }
        }
    }

    /// <summary>
    /// Gets the expression of a run of whitespace.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRun();

    #endregion // Methods
}