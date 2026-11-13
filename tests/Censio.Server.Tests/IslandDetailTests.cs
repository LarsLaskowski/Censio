using System.Globalization;
using System.Security.Cryptography;

using Bunit;
using Bunit.TestDoubles;

using Censio.Contracts;
using Censio.Server.Components.Layout;
using Censio.Server.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

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

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The profile of the tests.
    /// </summary>
    private static readonly Guid _profile = new("11111111-1111-4111-8111-111111111111");

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
    /// The detail shows the island's name as heading and per good the icon, name, production, consumption, delta,
    /// buildings and productivity, in this order and formatted for the shown language.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expectedHeaders">The header texts, joined by <c>|</c></param>
    /// <param name="expectedRow">The cell texts of the row, joined by <c>|</c></param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(UiLanguage.English, "|Good|Production|Consumption|Delta|Buildings|Productivity", "|Good 1001|1,234.50|2.50|+1.25|3|87%")]
    [DataRow(UiLanguage.German, "|Ware|Produktion|Verbrauch|Delta|Gebäude|Produktivität", "|Ware 1001|1.234,50|2,50|+1,25|3|87 %")]
    public async Task IslandDetailShowsColumnsInOrder(UiLanguage language, string expectedHeaders, string expectedRow)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreIslandAsync(host, "Isle A", PageTestHost.Entry(1001, 1234.5f, 2.5f, 1.25f, 3, 87.4f));

        // Act
        var page = RenderIsland(host, language);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual("Isle A", page.Find("h1").TextContent.Trim(), "The heading should name the island");
        Assert.AreEqual(expectedHeaders, string.Join('|', PageTestHost.Headers(page)), "The table should have the detail's columns in order");
        Assert.AreSequenceEqual(PageTestHost.Expect(expectedRow), PageTestHost.Describe(page), "The row should show the entry's values formatted for the language");
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
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(PageTestHost.Expect($"|Good 1001|{Dash}|{Dash}|{Dash}|0|{Dash}"), PageTestHost.Describe(page), "Values without a value should show a dash");
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

        Assert.AreSequenceEqual(PageTestHost.Expect("|Good 1002|2.00|1.00|+1.00|4|100%"), PageTestHost.Describe(page), "The page should show the goods of the island of the new address");
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