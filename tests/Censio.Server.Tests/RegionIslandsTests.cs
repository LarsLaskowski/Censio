using Bunit;

using Censio.Contracts;
using Censio.Server.Components.Pages;

using Microsoft.AspNetCore.Components.Web;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the island list of a region, <see cref="RegionIslands" />.
/// </summary>
[TestClass]
public sealed class RegionIslandsTests
{
    #region Constants

    /// <summary>
    /// The text of a figure without a value.
    /// </summary>
    private const string Dash = "–";

    /// <summary>
    /// The session GUID of the shown region.
    /// </summary>
    private const int Session = 3245;

    /// <summary>
    /// The session GUID of another region.
    /// </summary>
    private const int OtherSession = 6627;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The profile of the tests.
    /// </summary>
    private static readonly Guid _profile = new("11111111-1111-4111-8111-111111111111");

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RegionIslandsTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public RegionIslandsTests(TestContext testContext)
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
    /// The island list shows the region's islands ordered by name with their three figures, each island a link to its
    /// detail, and only the islands of that region.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task RegionIslandsShowsIslandsWithFiguresAndLinks()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     0,
                                                     PageTestHost.Island(Session, 2, "beta", PageTestHost.Entry(1, 1f, 2f, -1f, 3, 50f), PageTestHost.Entry(2, 2f, 0f, 2f, 0, 50f)),
                                                     PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f)),
                                                     PageTestHost.Island(OtherSession, 1, "Other", PageTestHost.Entry(1, 1f, 1f, 0.5f, 7, 100f))));

        // Act
        var page = RenderSession(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count > 0, PageTestHost.WaitBound);

        Assert.AreEqual("Region 3245", page.Find("h1").TextContent.Trim(), "The heading should name the region");
        Assert.AreSequenceEqual(PageTestHost.Expect("Island", "Buildings", "Produced goods", "Goods in deficit"), PageTestHost.Headers(page), "The table should have the island list's columns in order");
        Assert.AreSequenceEqual(PageTestHost.Expect("Alpha|1|1|0", "beta|3|1|1"), PageTestHost.Describe(page), "The region's islands should be listed by name with their figures");
        Assert.AreSequenceEqual(PageTestHost.Expect($"/profiles/{_profile}/islands/3245/1/0", $"/profiles/{_profile}/islands/3245/2/0"), PageTestHost.RowLinks(page), "Each island should link to its detail");
    }

    /// <summary>
    /// An island the latest tick lacks stays listed with a dash for each figure.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task RegionIslandsIslandWithoutValuesShowsDash()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     0,
                                                     PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f)),
                                                     PageTestHost.Island(Session, 2, "beta", PageTestHost.Entry(1, 1f, 2f, -1f, 3, 50f))),
                                   PageTestHost.Tick(_profile, 10, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 2, 100f))));

        // Act
        var page = RenderSession(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 2, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(PageTestHost.Expect("Alpha|2|1|0", $"beta|{Dash}|{Dash}|{Dash}"), PageTestHost.Describe(page), "An island without values should stay listed with dashes");
    }

    /// <summary>
    /// An island with an empty name is listed under the unnamed text of the shown language.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expected">The text of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(UiLanguage.English, "Unnamed island")]
    [DataRow(UiLanguage.German, "Unbenannte Insel")]
    public async Task RegionIslandsEmptyIslandNameShowsUnnamed(UiLanguage language, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, string.Empty, PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f))));

        // Act
        var page = RenderSession(host, language);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual(expected, PageTestHost.Rows(page)[0][0], "An island without a name should show the unnamed text");
    }

    /// <summary>
    /// A region name with markup shows its characters in the heading and the page title, not markup.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task RegionIslandsRegionNameWithMarkupShowsCharacters()
    {
        // Arrange
        const string name = "<b>Latium</b> & <i>co</i>";

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.RegionEntry(Session, 900001, name, name));
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f))));

        var head = host.Context.Render<HeadOutlet>();

        // Act
        var page = RenderSession(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual(name, page.Find("h1").TextContent.Trim(), "The heading should show the region name's characters");
        Assert.IsEmpty(page.FindAll("h1 b, h1 i"), "The heading should not render the region name as markup");
        Assert.AreEqual($"Censio – {name}", head.Find("title").TextContent, "The page title should show the region name's characters");
        Assert.IsEmpty(head.FindAll("title b, title i"), "The page title should not render the region name as markup");
    }

    /// <summary>
    /// Renders the island list of the shown region.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<RegionIslands> RenderSession(PageTestHost host, UiLanguage language)
    {
        return host.Render<RegionIslands>(language,
                                          parameters => parameters.Add(page => page.Profile, _profile.ToString())
                                                                  .Add(page => page.Session, "3245"));
    }

    #endregion // Methods
}