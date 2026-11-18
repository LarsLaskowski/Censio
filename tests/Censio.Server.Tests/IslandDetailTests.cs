using System.Globalization;
using System.Security.Cryptography;

using AngleSharp.Dom;

using Bunit;
using Bunit.TestDoubles;

using Censio.Contracts;
using Censio.Server.Components.Layout;
using Censio.Server.Components.Pages;
using Censio.Server.Data;
using Censio.Server.Data.Tests;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the goods table of an island, <see cref="IslandDetail" />.
/// </summary>
[TestClass]
public sealed class IslandDetailTests
{
    #region Constants

    /// <summary>
    /// The text of a value without a value.
    /// </summary>
    private const string Dash = "–";

    /// <summary>
    /// The session GUID of the shown island.
    /// </summary>
    private const int Session = 3245;

    /// <summary>
    /// The index of the delta cell in a row.
    /// </summary>
    private const int DeltaCell = 4;

    /// <summary>
    /// The index of the trend cell in a row.
    /// </summary>
    private const int TrendCell = 5;

    /// <summary>
    /// The decimals a delta's trend compares at.
    /// </summary>
    private const int DeltaDecimals = 2;

    /// <summary>
    /// The selector of the drawn sparklines.
    /// </summary>
    private const string SparklineSelector = "tbody svg.censio-sparkline";

    /// <summary>
    /// The selector of the drawn charts.
    /// </summary>
    private const string ChartSelector = "svg.mud-chart-line";

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
    /// The receive time of the tick at second 0, the latest tick of most tests.
    /// </summary>
    private static readonly DateTimeOffset _latestTick = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IslandDetailTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public IslandDetailTests(TestContext testContext)
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
    /// The detail shows the island's name as heading and per good the icon, name, production, consumption, delta, trend,
    /// buildings and productivity, in this order and formatted for the shown language; a good with fewer than two points
    /// in the history shows a dash as its trend.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expectedHeaders">The header texts, joined by <c>|</c></param>
    /// <param name="expectedRow">The cell texts of the row, joined by <c>|</c></param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(UiLanguage.English, "|Good|Production|Consumption|Delta|Trend|Buildings|Productivity", "|Good 1001|1,234.50|2.50|+1.25|–|3|87%")]
    [DataRow(UiLanguage.German, "|Ware|Produktion|Verbrauch|Delta|Trend|Gebäude|Produktivität", "|Ware 1001|1.234,50|2,50|+1,25|–|3|87 %")]
    public async Task IslandDetailShowsColumnsInOrder(UiLanguage language, string expectedHeaders, string expectedRow)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1234.5f, 2.5f, 1.25f, 3, 87.4f));

        // Act
        var page = RenderIsland(host, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(expectedRow), PageTestHost.Describe(page), "The row should show the entry's values formatted for the language"), PageTestHost.WaitBound);

        Assert.AreEqual("Isle A", page.Find("h1").TextContent.Trim(), "The heading should name the island");
        Assert.AreEqual(expectedHeaders, string.Join('|', PageTestHost.Headers(page)), "The table should have the detail's columns in order");
    }

    /// <summary>
    /// A value the tick holds as no value shows a dash.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task IslandDetailNullValueShowsDash()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, null, null, null, 0, null));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect($"|Good 1001|{Dash}|{Dash}|{Dash}|{Dash}|0|{Dash}"), PageTestHost.Describe(page), "Values without a value should show a dash"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// The delta carries its sign and the palette colour of its sign; zero takes the text colour.
    /// </summary>
    /// <param name="delta">The delta of the entry</param>
    /// <param name="expectedText">The shown delta</param>
    /// <param name="expectedColour">The colour of the delta</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(-0.5f, "-0.50", "error")]
    [DataRow(1.25f, "+1.25", "success")]
    [DataRow(0f, "0.00", "none")]
    public async Task IslandDetailDeltaHasSignAndColour(float delta, string expectedText, string expectedColour)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, delta, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        var cell = page.FindAll("tbody tr td")[DeltaCell];

        Assert.AreEqual(expectedText, cell.TextContent.Trim(), "The delta should carry its sign");
        Assert.AreEqual(expectedColour, PageTestHost.Colour(cell), "The delta should take the colour of its sign");
    }

    /// <summary>
    /// A delta that rounds to zero shows <c>0.00</c> in the text colour.
    /// </summary>
    /// <param name="delta">The delta of the entry</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(-0.004f)]
    [DataRow(0.004f)]
    public async Task IslandDetailTinyDeltaIsNeutralAndNoDeficit(float delta)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, delta, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        var cell = page.FindAll("tbody tr td")[DeltaCell];

        Assert.AreEqual("0.00", cell.TextContent.Trim(), "A delta that rounds to zero should show 0.00");
        Assert.AreEqual("none", PageTestHost.Colour(cell), "A delta that rounds to zero should take the text colour");
    }

    /// <summary>
    /// An island with more than 500 entries shows the first 500 entries of the tick ordered by name, and the line with the
    /// count.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task IslandDetailShowsFirst500EntriesByName()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", [.. Enumerable.Range(1, 501).Select(guid => PageTestHost.Entry(guid, 1f, 1f, 0f, 1, 100f))]);

        var expected = Enumerable.Range(1, 500).Select(guid => string.Create(CultureInfo.InvariantCulture, $"Good {guid}")).Order(StringComparer.OrdinalIgnoreCase).ToList();

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count > 0, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(expected, PageTestHost.Rows(page).Select(cells => cells[1]).ToList(), "The first 500 entries of the tick should be shown ordered by name");
        Assert.Contains("Showing 500 of 501 goods.", PageTestHost.Text(page), "The page should say how many goods it shows");
    }

    /// <summary>
    /// The island without values in the latest tick shows the no-values text instead of the table.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IslandDetailIslandWithoutValuesShowsNoValuesText()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 10));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("No values for this island in the latest tick.", PageTestHost.Text(page), "An island without values should show the no-values text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "An island without values should show no table");
        Assert.AreEqual("Isle A", page.Find("h1").TextContent.Trim(), "The heading should still name the island");
    }

    /// <summary>
    /// The island's overview chart is drawn from the read of its key figures.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task IslandDetailOverviewReadDrawsChart()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(ChartSelector), "The overview chart should be drawn"), PageTestHost.WaitBound);
        Assert.HasCount(1, fake.FiguresQueries, "The overview should be read once");
    }

    /// <summary>
    /// After the overview read failed, the chart shows the reload text, the goods table stays, and a notice of the server
    /// state reads no overview again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IslandDetailOverviewFailedReadShowsReloadTextAndReadsNoMore()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       Failure = new InvalidOperationException("The history is gone")
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        var page = RenderIsland(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.Contains(UiTexts.For(UiLanguage.English).ReadFailed, PageTestHost.Text(page), "The chart should show the reload text"), PageTestHost.WaitBound);

        var callsAfterFailure = fake.FiguresQueries.Count;

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 2f, 1f, 0f, 1, 100f))));
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        // Assert
        Assert.HasCount(callsAfterFailure, fake.FiguresQueries, "A notice should not read the overview again after a read failed");
        Assert.IsEmpty(page.FindAll(ChartSelector), "No chart should be drawn after a failed read");
        Assert.HasCount(1, page.FindAll("table"), "The goods table should still be shown");
    }

    /// <summary>
    /// An overview read that ends after the page moved to another island is dropped: the chart of the new island is shown once.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task IslandDetailOverviewReadOfIslandLeftBehindIsDropped()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f)), PageTestHost.Island(Session, 2, "Isle B", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f))));

        fake.Hold();

        var page = RenderIsland(host, UiLanguage.English);

        await fake.WaitForFiguresCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        page.Render(parameters => parameters.Add(component => component.Island, "2"));

        // Act
        fake.Release();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(ChartSelector), "Only the chart of the other island should be drawn"), PageTestHost.WaitBound);
        Assert.AreEqual("Isle B", page.Find("h1").TextContent.Trim(), "The page should show the other island");
    }

    /// <summary>
    /// A new tick of the profile reads the overview once more.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task IslandDetailTickReadsOverviewAgain()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(2)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        var page = RenderIsland(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(ChartSelector), "The overview chart should be drawn"), PageTestHost.WaitBound);

        fake.FiguresTrend = CreateFiguresTrend(4);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 2f, 1f, 0f, 1, 100f))));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, fake.FiguresQueries, "The notice should read the overview once more"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A good shows its icon from the server only when the catalog names one whose PNG is stored; otherwise its icon cell
    /// stays empty.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IslandDetailIconOnlyWhenStored()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.GoodEntry(1, "Wood", "Holz", IconReference(1)), PageTestHost.GoodEntry(2, "Clay", "Lehm", IconReference(2)));
        await host.StoreIconAsync(IconReference(1));
        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1, 1f, 1f, 0f, 1, 100f), PageTestHost.Entry(2, 1f, 1f, 0f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 2, PageTestHost.WaitBound);

        var rows = page.FindAll("tbody tr");

        Assert.AreSequenceEqual(PageTestHost.Expect("|Clay", "|Wood"), PageTestHost.Rows(page).Select(cells => string.Join('|', cells.Take(2))).ToList(), "The goods should be named from the catalog and ordered by name");
        Assert.IsEmpty(rows[0].QuerySelectorAll("img"), "A good whose icon is not stored should show no icon");
        Assert.AreEqual($"/icons/{IconReference(1)}", rows[1].QuerySelector("td img")?.GetAttribute("src"), "A good whose icon is stored should show it from the server");
        Assert.AreEqual(string.Empty, rows[1].QuerySelector("td img")?.GetAttribute("alt"), "The icon should carry an empty alternative text");
    }

    /// <summary>
    /// A language switch in the frame changes the shown good names at once, without a navigation.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IslandDetailLanguageSwitchChangesNames()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.GoodEntry(1, "Wood", "Holz", null));
        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1, 1f, 1f, 0f, 1, 100f));

        var layout = host.Context.Render<MainLayout>(parameters => parameters.AddCascadingValue(UiTheme.Light)
                                                                             .AddCascadingValue(UiLanguage.English)
                                                                             .Add(frame => frame.Body, RenderIslandBody));

        await layout.WaitForAssertionAsync(() => Assert.AreEqual("Wood", GoodNames(layout).FirstOrDefault(), "The good should be named in English first"), PageTestHost.WaitBound);

        // Act
        await layout.Find("button[aria-label='Language']").ClickAsync(new MouseEventArgs());
        await layout.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == "Deutsch").ClickAsync(new MouseEventArgs());

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Holz"), GoodNames(layout), "The good should be named in German after the switch"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// An island name with markup shows its characters in the heading, not markup.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task IslandDetailNameWithMarkupShowsCharacters()
    {
        // Arrange
        const string name = "<b>Bold</b> & <script>alert(1)</script>";

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, name, PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual(name, page.Find("h1").TextContent.Trim(), "The heading should show the island name's characters");
        Assert.IsEmpty(page.FindAll("h1 b, h1 script"), "The heading should not render the island name as markup");
    }

    /// <summary>
    /// An island the profile's view lacks, or a malformed island address, shows the not-found text under the regions
    /// heading and title, without a table and without the as-of line.
    /// </summary>
    /// <param name="session">The session segment of the address</param>
    /// <param name="island">The island segment of the address</param>
    /// <param name="area">The area segment of the address</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("3245", "9", "0")]
    [DataRow("9999", "1", "0")]
    [DataRow("3245", "1", "1")]
    [DataRow("x7q", "1", "0")]
    [DataRow("3245", "256", "0")]
    [DataRow("3245", "1", "-1")]
    public async Task IslandDetailUnknownOrMalformedIslandShowsNotFound(string session, string island, string area)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        var head = host.Context.Render<HeadOutlet>();

        // Act
        var page = RenderIsland(host, session, island, area);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("Not found.", PageTestHost.Text(page), "An unknown or malformed island should show the not-found text"), PageTestHost.WaitBound);

        Assert.AreEqual("Regions", page.Find("h1").TextContent.Trim(), "The heading should be the regions heading");
        Assert.AreEqual("Censio – Regions", head.Find("title").TextContent, "The page title should be the regions title");
        Assert.IsEmpty(page.FindAll("table"), "An unknown or malformed island should show no table");
        Assert.DoesNotContain("As of", PageTestHost.Text(page), "A page that is not found should show no as-of line");
    }

    /// <summary>
    /// The detail of a profile that only said hello shows the no-data text under the regions heading in the shown language.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expectedHeading">The regions heading of the language</param>
    /// <param name="expectedText">The no-data text of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(UiLanguage.English, "Regions", "No data yet. The pages fill once the Companion sends data.")]
    [DataRow(UiLanguage.German, "Regionen", "Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.")]
    public async Task IslandDetailProfileWithoutTickShowsNoDataText(UiLanguage language, string expectedHeading, string expectedText)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreProfileAsync(_profile, "Hello only");

        // Act
        var page = RenderIsland(host, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains(expectedText, PageTestHost.Text(page), "A profile without a tick should show the no-data text"), PageTestHost.WaitBound);

        Assert.AreEqual(expectedHeading, page.Find("h1").TextContent.Trim(), "The heading should be the regions heading while no island is shown");
        Assert.IsEmpty(page.FindAll("table"), "A profile without a tick should show no table");
    }

    /// <summary>
    /// A new island in the address of the open page shows that island's goods under its name.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IslandDetailAddressOfOtherIslandShowsItsGoods()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     0,
                                                     PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f)),
                                                     PageTestHost.Island(Session, 2, "Isle B", PageTestHost.Entry(1002, 2f, 1f, 1f, 4, 100f))));

        var page = RenderIsland(host, UiLanguage.English);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        page.Render(parameters => parameters.Add(detail => detail.Island, "2"));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual("Isle B", page.Find("h1").TextContent.Trim(), "The heading should name the island of the new address"), PageTestHost.WaitBound);
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect($"|Good 1002|2.00|1.00|+1.00|{Dash}|4|100%"), PageTestHost.Describe(page), "The page should show the goods of the island of the new address"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// An icon stored while the detail is open appears in the good's icon cell without a reload.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IslandDetailStoredIconAppearsLive()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.GoodEntry(1, "Wood", "Holz", IconReference(1)));
        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1, 1f, 1f, 0f, 1, 100f));

        var page = RenderIsland(host, UiLanguage.English);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.IsEmpty(page.FindAll("tbody img"), "The good should show no icon before its PNG is stored");

        // Act
        await host.StoreIconAsync(IconReference(1));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual($"/icons/{IconReference(1)}", page.Find("tbody td img").GetAttribute("src"), "The stored icon should appear without a reload"), PageTestHost.WaitBound);
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "The update should not navigate or reload");
    }

    /// <summary>
    /// Every good name in the goods table links to the good's page on the island.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IslandDetailGoodNameLinksToGoodPage()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f), PageTestHost.Entry(-7, 2f, 1f, 0f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 2, PageTestHost.WaitBound);

        var links = page.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td")[1].QuerySelector("a")).ToList();
        var island = new IslandKey(Session, 1, 0);

        Assert.AreSequenceEqual(PageTestHost.Expect("Good -7", "Good 1001"), links.Select(link => link?.TextContent.Trim() ?? string.Empty).ToList(), "Each good name should be a link, ordered by name");
        Assert.AreSequenceEqual(PageTestHost.Expect(PageAddress.GoodLink(_profile, island, -7), PageAddress.GoodLink(_profile, island, 1001)), links.Select(link => link?.GetAttribute("href") ?? string.Empty).ToList(), "Each link should lead to the good's page on the island");
    }

    /// <summary>
    /// Choosing another range in the controls under the overview heading reads the overview again with that range and
    /// shows the new range line.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task IslandDetailRangeChangeRereadsOverview()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        var layout = RenderIslandInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.HasCount(1, layout.FindAll(ChartSelector), "The overview chart should be drawn"), PageTestHost.WaitBound);

        // Act
        await PageTestHost.ChooseAsync(layout, "Period", "Last hour");

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.HasCount(2, fake.FiguresQueries, "The overview should be read again"), PageTestHost.WaitBound);
        await layout.WaitForAssertionAsync(() => Assert.HasCount(1, layout.FindAll(ChartSelector), "The overview chart should be drawn again"), PageTestHost.WaitBound);

        Assert.AreEqual("2026-11-13T11:00:00.0000000Z", fake.FiguresQueries[1].RangeStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), "The read should start an hour before the latest tick");
        Assert.AreEqual(HistoryTrendResolution.Points, fake.FiguresQueries[1].Resolution, "The hour should be read tick by tick");
        Assert.Contains("Last hour up to the latest tick", layout.FindAll("p").Select(paragraph => paragraph.TextContent.Trim()).ToList(), "The range line should name the new range");
    }

    /// <summary>
    /// Choosing the game-time axis redraws the overview from the read it holds: nothing is read again and the time axis
    /// carries no label.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task IslandDetailAxisChangeRedrawsWithoutRead()
    {
        // Arrange
        var trend = CreateFiguresTrend(3);
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = new HistoryTrend<HistoryFiguresPoint>
                                      {
                                          Points = [
                                                       .. trend.Points.Select((point, index) => new HistoryFiguresPoint
                                                                                                {
                                                                                                    Time = point.Time,
                                                                                                    IsHourly = false,
                                                                                                    Buildings = point.Buildings,
                                                                                                    ProducedGoods = point.ProducedGoods,
                                                                                                    GoodsInDeficit = point.GoodsInDeficit,
                                                                                                    GameTime = 1_750_000_000 + (index * 120_000)
                                                                                                })
                                                   ],
                                          TotalCount = 3
                                      }
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        var layout = RenderIslandInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.HasCount(1, layout.FindAll(ChartSelector), "The overview chart should be drawn"), PageTestHost.WaitBound);

        var callsBefore = fake.FiguresQueries.Count;

        // Act
        await PageTestHost.ChooseAsync(layout, "Time axis", "Game time");

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.IsTrue(layout.FindAll(".mud-charts-xaxis > *").All(label => label.TextContent.Length == 0), "The game-time axis should carry no time label"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.HasCount(callsBefore, fake.FiguresQueries, "An axis change should read nothing");
        Assert.HasCount(1, layout.FindAll(ChartSelector), "The overview chart should stay drawn");
    }

    /// <summary>
    /// Each shown good gets the sparkline of its delta at each point of the window of the latest tick and the arrow of its
    /// direction; the one read asks for the shown goods of the island over the fixed four hours, at most 240 points.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IslandDetailTrendColumnDrawsEachShownGood()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3],
                                                         [1002] = [3, 2, 1]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f), PageTestHost.Entry(1002, 1f, 1f, 1f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, page.FindAll(SparklineSelector), "Each shown good should get a sparkline"), PageTestHost.WaitBound);

        var query = fake.GoodsQueries[0];

        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3), ExpectedPath(3, 2, 1)), Paths(page), "Each good's sparkline should be its delta at each point of the window");
        Assert.AreSequenceEqual(PageTestHost.Expect("Rising", "Falling"), ArrowNames(page), "Each good's arrow should follow its delta");
        Assert.AreEqual("Last 4 hours up to the latest tick", page.FindAll("thead th")[TrendCell].GetAttribute("title"), "The trend heading should name the window as its tooltip");
        Assert.HasCount(1, fake.GoodsQueries, "The trends of the table should be one read");
        Assert.AreEqual(_profile, query.ProfileId, "The read should be of the page's profile");
        Assert.AreEqual(new HistoryIslandKey(Session, 1, 0), query.Island, "The read should be of the page's island");
        Assert.AreSequenceEqual(Goods(1001, 1002), query.ProductGuids.Order().ToList(), "The read should ask for the shown goods");
        Assert.AreEqual("2026-11-13T08:00:00.0000000Z", query.RangeStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), "The window should start at the hour four hours before the latest tick");
        Assert.AreEqual(_latestTick.AddTicks(1), query.RangeEnd, "The window should end just after the latest tick");
        Assert.AreEqual(TrendRange.SparklineMaxPoints, query.MaxPoints, "The read should ask for the newest 240 points at most");
    }

    /// <summary>
    /// A good the island carries twice gets its sparkline in the row of its first entry; the other row shows a dash, and the
    /// read asks for the good once.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IslandDetailSecondRowOfRepeatedGoodShowsDash()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f), PageTestHost.Entry(1001, 5f, 1f, -1f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(SparklineSelector), "Only one row of the good should get a sparkline"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(PageTestHost.Expect("|Good 1001|1.00|1.00|+3.00|↑|1|100%", $"|Good 1001|5.00|1.00|-1.00|{Dash}|1|100%"), PageTestHost.Describe(page), "The row of the first entry should get the trend and the other row a dash");
        Assert.AreSequenceEqual(Goods(1001), fake.GoodsQueries[0].ProductGuids.ToList(), "The read should ask for the good once");
    }

    /// <summary>
    /// An island with more than 500 entries reads the trends of the 500 shown goods only.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task IslandDetailReadsTrendsOfShownRowsOnly()
    {
        // Arrange
        var fake = new FakeHistoryTrends();

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", [.. Enumerable.Range(1, 501).Select(guid => PageTestHost.Entry(guid, 1f, 1f, 0f, 1, 100f))]);

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await fake.WaitForRowTrendCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        Assert.AreSequenceEqual(Enumerable.Range(1, 500).ToList(), fake.GoodsQueries[0].ProductGuids.Order().ToList(), "The read should ask for the shown goods only");
        Assert.HasCount(500, page.FindAll("tbody tr"), "The table should show 500 goods");
    }

    /// <summary>
    /// The trend cells stay empty while the trends are not read yet, and on a prerendered page, which reads none.
    /// </summary>
    /// <param name="rendererName">The name of the renderer</param>
    /// <param name="isInteractive">Whether the renderer is interactive</param>
    /// <param name="expectedReads">The trend reads expected</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("Server", true, 1)]
    [DataRow("Static", false, 0)]
    public async Task IslandDetailTrendCellsEmptyBeforeReadAndWhenPrerendered(string rendererName, bool isInteractive, int expectedReads)
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        fake.HoldRowTrends();
        host.Context.SetRendererInfo(new RendererInfo(rendererName, isInteractive));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);
        await fake.WaitForRowTrendCallsAsync(expectedReads, PageTestHost.WaitBound, TestContext.CancellationToken);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual("Trend", PageTestHost.Headers(page)[TrendCell], "The table should have the trend column");
        Assert.AreSequenceEqual(PageTestHost.Expect(string.Empty), TrendTexts(page), "The trend cell should be empty before the trends are read");
        Assert.IsEmpty(page.FindAll(SparklineSelector), "No sparkline should be drawn before the trends are read");
        Assert.AreEqual(expectedReads, fake.RowTrendCallCount, "Only an interactive page should read the trends");
    }

    /// <summary>
    /// A failed trend read shows a dash in every trend cell and the line of the failure under the table, logs the charts'
    /// warning once, and leaves the overview chart drawn.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [TestCategory("AC7")]
    public async Task IslandDetailFailedTrendReadShowsDashAndLine()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsFailure = new InvalidOperationException("The history is gone")
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake, trendLogger: factory.CreateLogger<TrendReader>());

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f), PageTestHost.Entry(1002, 1f, 1f, 1f, 1, 100f));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(Dash, Dash), TrendTexts(page), "Every trend cell should show a dash after a failed read"), PageTestHost.WaitBound);

        Assert.Contains("The trends cannot be read right now. Reload the page to try again.", page.FindAll("p").Select(paragraph => paragraph.TextContent.Trim()).ToList(), "The line of the failure should show under the table");
        Assert.HasCount(1, provider.Logger.Entries, "The failed read should log once");
        Assert.AreEqual("A chart could not be read from the database.", provider.Logger.Entries[0].Message, "The failed read should log the charts' warning");
        Assert.HasCount(1, page.FindAll(ChartSelector), "The overview chart should still be drawn");
    }

    /// <summary>
    /// A trend read cancelled by an address change shows and logs nothing: the read of the new island starts, the page
    /// shows no failure and the log stays empty.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IslandDetailCancelledTrendReadShowsAndLogsNothing()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3],
                                                         [1002] = [3, 2, 1]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake, trendLogger: factory.CreateLogger<TrendReader>());

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     0,
                                                     PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f)),
                                                     PageTestHost.Island(Session, 2, "Isle B", PageTestHost.Entry(1002, 1f, 1f, 0f, 1, 100f))));

        fake.HoldRowTrends();

        var page = RenderIsland(host, UiLanguage.English);

        await fake.WaitForRowTrendCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        page.Render(parameters => parameters.Add(component => component.Island, "2"));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual(1, fake.CancelledCount, "The address change should cancel the trend read of the earlier island"), PageTestHost.WaitBound);
        await fake.WaitForRowTrendCallsAsync(2, PageTestHost.WaitBound, TestContext.CancellationToken);
        await page.WaitForAssertionAsync(() => Assert.AreEqual("Isle B", page.Find("h1").TextContent.Trim(), "The page should show the other island"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(Goods(1002), fake.GoodsQueries[1].ProductGuids.ToList(), "The new read should ask for the goods of the new island");
        Assert.AreSequenceEqual(PageTestHost.Expect(string.Empty), TrendTexts(page), "The cancelled read should show nothing in the new island's trend cell");
        Assert.DoesNotContain("The trends cannot be read right now. Reload the page to try again.", PageTestHost.Text(page), "A cancelled read should show no failure");
        Assert.IsEmpty(provider.Logger.Entries, "A cancelled read should log nothing");
    }

    /// <summary>
    /// A stored tick of the page's profile reads the trends again, from the held read, and the arrow follows the new point
    /// without a reload.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IslandDetailStoredTickRereadsTrends()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        var page = RenderIsland(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Rising"), ArrowNames(page), "The arrow should rise first"), PageTestHost.WaitBound);

        fake.GoodsTrend = new HistoryTrend<HistoryGoodsPoint>
                          {
                              Points = [
                                           new HistoryGoodsPoint
                                           {
                                               Time = _latestTick.AddMinutes(2),
                                               IsHourly = false,
                                               Deltas = new Dictionary<int, double>
                                                        {
                                                            [1001] = -5
                                                        }
                                           }
                                       ],
                              TotalCount = 1
                          };

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 1f, 1f, -5f, 1, 100f))));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Falling"), ArrowNames(page), "The arrow should follow the new point without a reload"), PageTestHost.WaitBound);

        Assert.HasCount(2, fake.GoodsQueries, "The stored tick should read the trends once more");
        Assert.AreEqual(_latestTick.AddMinutes(2).AddTicks(1), fake.GoodsQueries[1].RangeEnd, "The read should end just after the new latest tick");
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "The update should not navigate or reload");
    }

    /// <summary>
    /// A range choice after the trends were read reads no trends, and the shown sparklines stay.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IslandDetailRangeChoiceReadsNoTrends()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        var layout = RenderIslandInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.HasCount(1, layout.FindAll(SparklineSelector), "The sparkline should be drawn"), PageTestHost.WaitBound);

        // Act
        await PageTestHost.ChooseAsync(layout, "Period", "Last hour");

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.HasCount(2, fake.FiguresQueries, "The overview should be read again"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(1, fake.RowTrendCallCount, "A range choice should read no trends");
        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(layout), "The shown sparkline should stay");
    }

    /// <summary>
    /// A range choice while the trend read runs neither cancels nor repeats it: its answer is shown.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IslandDetailRangeChoiceDuringTrendReadKeepsIt()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        fake.HoldRowTrends();

        var layout = RenderIslandInFrame(host);

        await fake.WaitForRowTrendCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        await PageTestHost.ChooseAsync(layout, "Period", "Last hour");

        // Assert
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(0, fake.CancelledCount, "A range choice should not cancel the running trend read");

        fake.ReleaseRowTrends();

        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(layout), "The answer of the running read should be shown"), PageTestHost.WaitBound);
        await layout.WaitForAssertionAsync(() => Assert.HasCount(2, fake.FiguresQueries, "The overview should be read again"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(1, fake.RowTrendCallCount, "A range choice should not repeat the trend read");
    }

    /// <summary>
    /// A language switch in the frame redraws the arrow's name and the heading's tooltip in the new language without a read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IslandDetailLanguageSwitchRedrawsTrendsWithoutRead()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        var layout = RenderIslandInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Rising"), ArrowNames(layout), "The arrow should be named in English first"), PageTestHost.WaitBound);

        // Act
        await layout.Find("button[aria-label='Language']").ClickAsync(new MouseEventArgs());
        await layout.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == "Deutsch").ClickAsync(new MouseEventArgs());

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Steigend"), ArrowNames(layout), "The arrow should be named in German after the switch"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual("Letzte 4 Stunden bis zum letzten Tick", layout.FindAll("thead th")[TrendCell].GetAttribute("title"), "The heading's tooltip should be German after the switch");
        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(layout), "The sparkline should stay drawn");
        Assert.AreEqual(1, fake.RowTrendCallCount, "A language switch should read no trends");
    }

    /// <summary>
    /// The goods table and the overview chart show while the trend read is held, with empty trend cells.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IslandDetailTableShowsWhileTrendReadHeld()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3)
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        fake.HoldRowTrends();

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await fake.WaitForRowTrendCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("|Good 1001|1.00|1.00|0.00||1|100%"), PageTestHost.Describe(page), "The figures should show while the trend read is held"), PageTestHost.WaitBound);

        Assert.HasCount(1, page.FindAll(ChartSelector), "The overview chart should show while the trend read is held");
    }

    /// <summary>
    /// A stored tick updates the figures of the goods table while the trend read is held.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IslandDetailStoredTickUpdatesFiguresWhileTrendReadHeld()
    {
        // Arrange
        var fake = new FakeHistoryTrends();

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 0f, 1, 100f));

        fake.HoldRowTrends();

        var page = RenderIsland(host, UiLanguage.English);

        await fake.WaitForRowTrendCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 2f, 1f, 1f, 1, 100f))));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("|Good 1001|2.00|1.00|+1.00||1|100%"), PageTestHost.Describe(page), "The figures of the new tick should show while the trend read is held"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// The trends are read after the overview chart's read, and still read when that read failed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task IslandDetailTrendsReadAfterOverviewAndDespiteItsFailure()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresFailure = new InvalidOperationException("The history is gone"),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        fake.Hold();

        var page = RenderIsland(host, UiLanguage.English);

        await fake.WaitForCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        var firstReads = $"{fake.FiguresQueries.Count}|{fake.RowTrendCallCount}";

        // Act
        fake.Release();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(page), "The trends should be drawn despite the overview's failure"), PageTestHost.WaitBound);

        Assert.AreEqual("1|0", firstReads, "The overview should be read first, before the trends");
        Assert.Contains(UiTexts.For(UiLanguage.English).ReadFailed, PageTestHost.Text(page), "The overview should show its reload text");
        Assert.AreEqual(1, fake.RowTrendCallCount, "The trends should be read once");
    }

    /// <summary>
    /// An island page that shows the no-values text instead of the goods table has no trend cells and reads no trends.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task IslandDetailWithoutValuesReadsNoTrends()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 10));

        // Act
        var page = RenderIsland(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(ChartSelector), "The overview chart should be drawn"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.Contains("No values for this island in the latest tick.", PageTestHost.Text(page), "The page should show the no-values text");
        Assert.IsEmpty(TrendCells(page), "A page without values should have no trend cells");
        Assert.AreEqual(0, fake.RowTrendCallCount, "A page without values should read no trends");
    }

    /// <summary>
    /// An overview read that ends normally after the page moved to another island is dropped: it reads no trends of the
    /// island left behind.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task IslandDetailOverviewReadEndingAfterAddressChangeReadsNoTrends()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     }),
                       IsIgnoringCancellation = true
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     -10,
                                                     PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f)),
                                                     PageTestHost.Island(Session, 2, "Isle B", PageTestHost.Entry(1002, 1f, 1f, 3f, 1, 100f))),
                                   PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f))));

        fake.Hold();

        var page = RenderIsland(host, UiLanguage.English);

        await fake.WaitForFiguresCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);

        page.Render(parameters => parameters.Add(component => component.Island, "2"));

        // Act
        fake.Release();

        // Assert
        await fake.WaitForFiguresCallsAsync(2, PageTestHost.WaitBound, TestContext.CancellationToken);
        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(ChartSelector), "The chart of the other island should be drawn"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual("Isle B", page.Find("h1").TextContent.Trim(), "The page should show the other island");
        Assert.HasCount(2, fake.FiguresQueries, "Each island should read its overview once");
        Assert.AreEqual(0, fake.CancelledCount, "The overview read of the island left behind should end normally");
        Assert.AreEqual(0, fake.RowTrendCallCount, "The overview read of the island left behind should read no trends");
    }

    /// <summary>
    /// An axis choice after the trends were read reads no trends, and the shown sparklines stay.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task IslandDetailAxisChoiceReadsNoTrends()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       FiguresTrend = CreateFiguresTrend(3),
                       GoodsTrend = CreateGoodsTrend(new Dictionary<int, double[]>
                                                     {
                                                         [1001] = [1, 2, 3]
                                                     })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1f, 1f, 3f, 1, 100f));

        var layout = RenderIslandInFrame(host);

        await layout.WaitForAssertionAsync(() => Assert.HasCount(1, layout.FindAll(SparklineSelector), "The sparkline should be drawn"), PageTestHost.WaitBound);

        // Act
        await PageTestHost.ChooseAsync(layout, "Time axis", "Game time");

        // Assert
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(1, fake.RowTrendCallCount, "An axis choice should read no trends");
        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(layout), "The shown sparkline should stay");
    }

    /// <summary>
    /// Stores a tick with the shown island (session 3245, island 1, area 0).
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="name">The island name</param>
    /// <param name="entries">The entries</param>
    /// <returns>A task that completes when the tick is stored</returns>
    private static Task StoreIslandAsync(PageTestHost host, string name, params ContractTickEntry[] entries)
    {
        return host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, name, entries)));
    }

    /// <summary>
    /// Creates a figures trend of points two minutes apart.
    /// </summary>
    /// <param name="count">The number of points</param>
    /// <returns>The trend</returns>
    private static HistoryTrend<HistoryFiguresPoint> CreateFiguresTrend(int count)
    {
        var end = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

        return new HistoryTrend<HistoryFiguresPoint>
               {
                   Points = [
                                .. Enumerable.Range(0, count).Select(index => new HistoryFiguresPoint
                                                                              {
                                                                                  Time = end.AddMinutes(2 * (index - count + 1)),
                                                                                  IsHourly = false,
                                                                                  Buildings = 10 + index,
                                                                                  ProducedGoods = 5 + index,
                                                                                  GoodsInDeficit = index
                                                                              })
                            ],
                   TotalCount = count
               };
    }

    /// <summary>
    /// Creates a goods trend of points two minutes apart that end at the latest tick, each with the delta of every good at
    /// its index.
    /// </summary>
    /// <param name="deltas">The deltas of each good, one per point</param>
    /// <returns>The trend</returns>
    private static HistoryTrend<HistoryGoodsPoint> CreateGoodsTrend(Dictionary<int, double[]> deltas)
    {
        var count = deltas.Values.Max(values => values.Length);

        return new HistoryTrend<HistoryGoodsPoint>
               {
                   Points = [
                                .. Enumerable.Range(0, count).Select(index => new HistoryGoodsPoint
                                                                              {
                                                                                  Time = PointTime(index, count),
                                                                                  IsHourly = false,
                                                                                  Deltas = deltas.ToDictionary(pair => pair.Key, pair => pair.Value[index])
                                                                              })
                            ],
                   TotalCount = count
               };
    }

    /// <summary>
    /// Gives a list of expected product GUIDs, so that a test passes no constant array.
    /// </summary>
    /// <param name="productGuids">The product GUIDs</param>
    /// <returns>The list</returns>
    private static List<int> Goods(params int[] productGuids)
    {
        return [.. productGuids];
    }

    /// <summary>
    /// Gives the time of a point of a trend whose points lie two minutes apart and end at the latest tick.
    /// </summary>
    /// <param name="index">The index of the point</param>
    /// <param name="count">The number of points</param>
    /// <returns>The time</returns>
    private static DateTimeOffset PointTime(int index, int count)
    {
        return _latestTick.AddMinutes(2 * (index - count + 1));
    }

    /// <summary>
    /// Gives the path of the sparkline of deltas at the points of <see cref="CreateGoodsTrend" /> over the window of the
    /// latest tick.
    /// </summary>
    /// <param name="deltas">The deltas, one per point</param>
    /// <returns>The path, or an empty text when the deltas give none</returns>
    private static string ExpectedPath(params double[] deltas)
    {
        var samples = deltas.Select((delta, index) => new SparklineSample(PointTime(index, deltas.Length), false, delta)).ToList();

        return SparklineBuilder.Build(samples, TrendRange.ForSparklines(_latestTick), DeltaDecimals)?.Path ?? string.Empty;
    }

    /// <summary>
    /// Gives the trend cell of each body row.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <returns>The cells</returns>
    private static List<IElement> TrendCells<TComponent>(IRenderedComponent<TComponent> component)
        where TComponent : IComponent
    {
        return [.. component.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td")[TrendCell])];
    }

    /// <summary>
    /// Gives the trimmed text of each trend cell.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <returns>The texts</returns>
    private static List<string> TrendTexts<TComponent>(IRenderedComponent<TComponent> component)
        where TComponent : IComponent
    {
        return [.. TrendCells(component).Select(cell => cell.TextContent.Trim())];
    }

    /// <summary>
    /// Gives the path of the sparkline of each trend cell, an empty text for a cell without one.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <returns>The paths</returns>
    private static List<string> Paths<TComponent>(IRenderedComponent<TComponent> component)
        where TComponent : IComponent
    {
        return [.. TrendCells(component).Select(cell => cell.QuerySelector("svg path")?.GetAttribute("d") ?? string.Empty)];
    }

    /// <summary>
    /// Gives the accessible name of the arrow of each trend cell, an empty text for a cell without one.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <returns>The names</returns>
    private static List<string> ArrowNames<TComponent>(IRenderedComponent<TComponent> component)
        where TComponent : IComponent
    {
        return [.. TrendCells(component).Select(cell => cell.QuerySelector("span[role='img']")?.GetAttribute("aria-label") ?? string.Empty)];
    }

    /// <summary>
    /// Renders the detail of the shown island.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<IslandDetail> RenderIsland(PageTestHost host, UiLanguage language)
    {
        return host.Render<IslandDetail>(language,
                                         parameters => parameters.Add(page => page.Profile, _profile.ToString())
                                                                 .Add(page => page.Session, "3245")
                                                                 .Add(page => page.Island, "1")
                                                                 .Add(page => page.Area, "0"));
    }

    /// <summary>
    /// Renders the detail of an address of the profile in English.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="session">The session segment of the address</param>
    /// <param name="island">The island segment of the address</param>
    /// <param name="area">The area segment of the address</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<IslandDetail> RenderIsland(PageTestHost host, string session, string island, string area)
    {
        return host.Render<IslandDetail>(UiLanguage.English,
                                         parameters => parameters.Add(page => page.Profile, _profile.ToString())
                                                                 .Add(page => page.Session, session)
                                                                 .Add(page => page.Island, island)
                                                                 .Add(page => page.Area, area));
    }

    /// <summary>
    /// Renders the detail of the shown island as the body of the frame, which starts in the light theme and English.
    /// </summary>
    /// <param name="host">The host</param>
    /// <returns>The rendered frame</returns>
    private static IRenderedComponent<MainLayout> RenderIslandInFrame(PageTestHost host)
    {
        return host.Context.Render<MainLayout>(parameters => parameters.AddCascadingValue(UiTheme.Light)
                                                                       .AddCascadingValue(UiLanguage.English)
                                                                       .Add(frame => frame.Body, RenderIslandBody));
    }

    /// <summary>
    /// Renders the detail of the shown island as the body of the frame.
    /// </summary>
    /// <param name="builder">The render tree builder</param>
    private static void RenderIslandBody(RenderTreeBuilder builder)
    {
        builder.OpenComponent<IslandDetail>(0);
        builder.AddComponentParameter(1, nameof(IslandDetail.Profile), _profile.ToString());
        builder.AddComponentParameter(2, nameof(IslandDetail.Session), "3245");
        builder.AddComponentParameter(3, nameof(IslandDetail.Island), "1");
        builder.AddComponentParameter(4, nameof(IslandDetail.Area), "0");
        builder.CloseComponent();
    }

    /// <summary>
    /// Gives the shown good names of the goods table inside the frame.
    /// </summary>
    /// <param name="layout">The rendered frame</param>
    /// <returns>The names</returns>
    private static List<string> GoodNames(IRenderedComponent<MainLayout> layout)
    {
        return [.. PageTestHost.Rows(layout).Select(cells => cells[1])];
    }

    /// <summary>
    /// Gives an icon reference: 64 lowercase hex characters derived from a number.
    /// </summary>
    /// <param name="number">The number</param>
    /// <returns>The reference</returns>
    private static string IconReference(int number)
    {
        return Convert.ToHexStringLower(SHA256.HashData(BitConverter.GetBytes(number)));
    }

    #endregion // Methods
}