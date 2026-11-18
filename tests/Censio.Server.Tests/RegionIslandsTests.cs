using System.Globalization;

using AngleSharp.Dom;

using Bunit;

using Censio.Contracts;
using Censio.Server.Components.Layout;
using Censio.Server.Components.Pages;
using Censio.Server.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

    /// <summary>
    /// The index of the trend cell in a row.
    /// </summary>
    private const int TrendCell = 2;

    /// <summary>
    /// The decimals a buildings trend compares at.
    /// </summary>
    private const int BuildingsDecimals = 0;

    /// <summary>
    /// The selector of the drawn sparklines.
    /// </summary>
    private const string SparklineSelector = "tbody svg.censio-sparkline";

    /// <summary>
    /// How long the test waits to see that nothing changes, in milliseconds.
    /// </summary>
    private const int NegativeWaitMilliseconds = 400;

    /// <summary>
    /// The line under the table after a failed trend read.
    /// </summary>
    private const string TrendsReadFailedText = "The trends cannot be read right now. Reload the page to try again.";

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
    /// The receive time of the tick at second 0, the latest tick of most tests.
    /// </summary>
    private static readonly DateTimeOffset _latestTick = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The history key of the island <c>Alpha</c> of the shown region.
    /// </summary>
    private static readonly HistoryIslandKey _alpha = new(Session, 1, 0);

    /// <summary>
    /// The history key of the island <c>beta</c> of the shown region.
    /// </summary>
    private static readonly HistoryIslandKey _beta = new(Session, 2, 0);

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
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect($"Alpha|1|{Dash}|1|0", $"beta|3|{Dash}|1|1"), PageTestHost.Describe(page), "The region's islands should be listed by name with their figures"), PageTestHost.WaitBound);

        Assert.AreEqual("Region 3245", page.Find("h1").TextContent.Trim(), "The heading should name the region");
        Assert.AreSequenceEqual(PageTestHost.Expect("Island", "Buildings", "Trend", "Produced goods", "Goods in deficit"), PageTestHost.Headers(page), "The table should have the island list's columns in order");
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
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Alpha|2|↑|1|0", $"beta|{Dash}|{Dash}|{Dash}|{Dash}"), PageTestHost.Describe(page), "An island without values should stay listed with dashes"), PageTestHost.WaitBound);
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
    /// A session the profile's view has no region for, or a malformed session, shows the not-found text under the
    /// regions heading, without a table, without the as-of line and without echoing the address.
    /// </summary>
    /// <param name="session">The session segment of the address</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("9999")]
    [DataRow("x7q")]
    public async Task RegionIslandsUnknownOrMalformedSessionShowsNotFound(string session)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f))));

        var head = host.Context.Render<HeadOutlet>();

        // Act
        var page = host.Render<RegionIslands>(UiLanguage.English,
                                              parameters => parameters.Add(islands => islands.Profile, _profile.ToString())
                                                                      .Add(islands => islands.Session, session));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("Not found.", PageTestHost.Text(page), "An unknown or malformed session should show the not-found text"), PageTestHost.WaitBound);

        Assert.AreEqual("Regions", page.Find("h1").TextContent.Trim(), "The heading should be the regions heading");
        Assert.AreEqual("Censio – Regions", head.Find("title").TextContent, "The page title should be the regions title");
        Assert.IsEmpty(page.FindAll("table"), "An unknown or malformed session should show no table");
        Assert.DoesNotContain("As of", PageTestHost.Text(page), "A page that is not found should show no as-of line");
        Assert.DoesNotContain(session, PageTestHost.Text(page), "The page should not echo the address");
    }

    /// <summary>
    /// A tick of another profile does not make the island list read again; a later catalog notice does.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionIslandsTickOfOtherProfileReadsNothing()
    {
        // Arrange
        using var source = new FakeProfileStateSource(false);

        source.Store(_profile,
                     new ProfileStateRead
                     {
                         State = CreateState(),
                         Failure = null
                     });

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var page = RenderSession(host, UiLanguage.English);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_otherProfile, 10, PageTestHost.Island(OtherSession, 1, "Other", PageTestHost.Entry(1, 1f, 1f, 0.5f, 7, 100f))));
        await host.StoreCatalogAsync(PageTestHost.RegionEntry(Session, 900001, "Latium", "Latium"));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, source.Reads, "The catalog notice should cause one more read"), PageTestHost.WaitBound);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect($"Alpha|1|{Dash}|1|0"), PageTestHost.Describe(page), "The shown data should stay unchanged"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(new[] { _profile, _profile }, source.Reads.ToList(), "Only the first read and the catalog's should read, never the other profile's tick");
    }

    /// <summary>
    /// Each island of the region gets the sparkline of its buildings at each point of the window of the latest tick and the
    /// arrow of its direction; the one read asks for the region's islands over the fixed four hours, at most 240 points.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task RegionIslandsTrendColumnDrawsEachIsland()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       IslandsTrend = CreateIslandsTrend(new Dictionary<HistoryIslandKey, long[]>
                                                         {
                                                             [_alpha] = [1, 2, 3],
                                                             [_beta] = [5, 5, 4]
                                                         })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     0,
                                                     PageTestHost.Island(Session, 2, "beta", PageTestHost.Entry(1, 1f, 2f, -1f, 4, 50f)),
                                                     PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 3, 100f)),
                                                     PageTestHost.Island(OtherSession, 1, "Other", PageTestHost.Entry(1, 1f, 1f, 0.5f, 7, 100f))));

        // Act
        var page = RenderSession(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, page.FindAll(SparklineSelector), "Each island of the region should get a sparkline"), PageTestHost.WaitBound);

        var query = fake.IslandsQueries[0];

        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3), ExpectedPath(5, 5, 4)), Paths(page), "Each island's sparkline should be its buildings at each point of the window");
        Assert.AreSequenceEqual(PageTestHost.Expect("Rising", "Falling"), ArrowNames(page), "Each island's arrow should follow its buildings");
        Assert.AreEqual("Last 4 hours up to the latest tick", page.FindAll("thead th")[TrendCell].GetAttribute("title"), "The trend heading should name the window as its tooltip");
        Assert.HasCount(1, fake.IslandsQueries, "The trends of the island list should be one read");
        Assert.AreEqual(_profile, query.ProfileId, "The read should be of the page's profile");
        Assert.AreSequenceEqual(PageTestHost.Expect("3245/1/0", "3245/2/0"), Describe(query.Islands), "The read should ask for the region's islands only");
        Assert.AreEqual("2026-11-13T08:00:00.0000000Z", query.RangeStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), "The window should start at the hour four hours before the latest tick");
        Assert.AreEqual(_latestTick.AddTicks(1), query.RangeEnd, "The window should end just after the latest tick");
        Assert.AreEqual(TrendRange.SparklineMaxPoints, query.MaxPoints, "The read should ask for the newest 240 points at most");
    }

    /// <summary>
    /// An island the latest tick lacks keeps its dashes for the figures and still gets its sparkline from the history.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task RegionIslandsTrendShownForIslandLatestTickLacks()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       IslandsTrend = CreateIslandsTrend(new Dictionary<HistoryIslandKey, long[]>
                                                         {
                                                             [_beta] = [3, 2, 1]
                                                         })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     -10,
                                                     PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f)),
                                                     PageTestHost.Island(Session, 2, "beta", PageTestHost.Entry(1, 1f, 2f, -1f, 3, 50f))),
                                   PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 2, 100f))));

        // Act
        var page = RenderSession(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(SparklineSelector), "The island the latest tick lacks should get a sparkline"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(PageTestHost.Expect($"Alpha|2|{Dash}|1|0", $"beta|{Dash}|↓|{Dash}|{Dash}"), PageTestHost.Describe(page), "The island the latest tick lacks should keep its dashes and get its trend");
        Assert.AreSequenceEqual(PageTestHost.Expect(string.Empty, ExpectedPath(3, 2, 1)), Paths(page), "The sparkline should be the island's buildings from the history");
        Assert.AreSequenceEqual(PageTestHost.Expect("3245/1/0", "3245/2/0"), Describe(fake.IslandsQueries[0].Islands), "The read should ask for the island the latest tick lacks too");
    }

    /// <summary>
    /// A failed trend read shows a dash in every trend cell and the line of the failure under the table.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task RegionIslandsFailedTrendReadShowsDashAndLine()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       IslandsFailure = new InvalidOperationException("The history is gone")
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile,
                                                     0,
                                                     PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f)),
                                                     PageTestHost.Island(Session, 2, "beta", PageTestHost.Entry(1, 1f, 2f, -1f, 3, 50f))));

        // Act
        var page = RenderSession(host, UiLanguage.English);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(Dash, Dash), TrendTexts(page), "Every trend cell should show a dash after a failed read"), PageTestHost.WaitBound);

        Assert.Contains(TrendsReadFailedText, page.FindAll("p").Select(paragraph => paragraph.TextContent.Trim()).ToList(), "The line of the failure should show under the table");
        Assert.AreSequenceEqual(PageTestHost.Expect($"Alpha|1|{Dash}|1|0", $"beta|3|{Dash}|1|1"), PageTestHost.Describe(page), "The figures should still show");
    }

    /// <summary>
    /// A tick of another profile reads no trends and leaves the shown sparkline.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionIslandsTickOfOtherProfileReadsNoTrends()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       IslandsTrend = CreateIslandsTrend(new Dictionary<HistoryIslandKey, long[]>
                                                         {
                                                             [_alpha] = [1, 2, 3]
                                                         })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 3, 100f))));

        var page = RenderSession(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.HasCount(1, page.FindAll(SparklineSelector), "The sparkline should be drawn"), PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_otherProfile, 120, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 7, 100f))));

        // Assert
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreEqual(1, fake.RowTrendCallCount, "A tick of another profile should read no trends");
        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(page), "The shown sparkline should stay");
    }

    /// <summary>
    /// A theme switch in the frame redraws the sparklines in the new theme's colour without a read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionIslandsThemeSwitchRedrawsTrendsWithoutRead()
    {
        // Arrange
        var fake = new FakeHistoryTrends
                   {
                       IslandsTrend = CreateIslandsTrend(new Dictionary<HistoryIslandKey, long[]>
                                                         {
                                                             [_alpha] = [1, 2, 3]
                                                         })
                   };

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 3, 100f))));

        var layout = host.Context.Render<MainLayout>(parameters => parameters.AddCascadingValue(UiTheme.Light)
                                                                             .AddCascadingValue(UiLanguage.English)
                                                                             .Add(frame => frame.Body, RenderSessionBody));

        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(TrendPalette.Sparkline(UiTheme.Light)), Strokes(layout), "The sparkline should take the light theme's colour first"), PageTestHost.WaitBound);

        // Act
        await layout.Find("button[aria-label='Switch to the dark theme']").ClickAsync(new MouseEventArgs());

        // Assert
        await layout.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(TrendPalette.Sparkline(UiTheme.Dark)), Strokes(layout), "The sparkline should take the dark theme's colour after the switch"), PageTestHost.WaitBound);
        await Task.Delay(NegativeWaitMilliseconds, TestContext.CancellationToken);

        Assert.AreSequenceEqual(PageTestHost.Expect(ExpectedPath(1, 2, 3)), Paths(layout), "The sparkline should stay drawn");
        Assert.AreEqual(1, fake.RowTrendCallCount, "A theme switch should read no trends");
    }

    /// <summary>
    /// A stored tick updates the figures of the island list while the trend read is held.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task RegionIslandsStoredTickUpdatesFiguresWhileTrendReadHeld()
    {
        // Arrange
        var fake = new FakeHistoryTrends();

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, trends: fake);

        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f))));

        fake.HoldRowTrends();

        var page = RenderSession(host, UiLanguage.English);

        await fake.WaitForRowTrendCallsAsync(1, PageTestHost.WaitBound, TestContext.CancellationToken);
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Alpha|1||1|0"), PageTestHost.Describe(page), "The figures should show while the trend read is held"), PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(PageTestHost.Tick(_profile, 120, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 2, 100f))));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Alpha|2||1|0"), PageTestHost.Describe(page), "The figures of the new tick should show while the trend read is held"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// Creates an islands trend of points two minutes apart that end at the latest tick, each with the buildings of every
    /// island at its index.
    /// </summary>
    /// <param name="buildings">The buildings of each island, one per point</param>
    /// <returns>The trend</returns>
    private static HistoryTrend<HistoryIslandsPoint> CreateIslandsTrend(Dictionary<HistoryIslandKey, long[]> buildings)
    {
        var count = buildings.Values.Max(values => values.Length);

        return new HistoryTrend<HistoryIslandsPoint>
               {
                   Points = [
                                .. Enumerable.Range(0, count).Select(index => new HistoryIslandsPoint
                                                                              {
                                                                                  Time = PointTime(index, count),
                                                                                  IsHourly = false,
                                                                                  Buildings = buildings.ToDictionary(pair => pair.Key, pair => pair.Value[index])
                                                                              })
                            ],
                   TotalCount = count
               };
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
    /// Gives the path of the sparkline of buildings at the points of <see cref="CreateIslandsTrend" /> over the window of
    /// the latest tick.
    /// </summary>
    /// <param name="buildings">The buildings, one per point</param>
    /// <returns>The path, or an empty text when the buildings give none</returns>
    private static string ExpectedPath(params long[] buildings)
    {
        var samples = buildings.Select((count, index) => new SparklineSample(PointTime(index, buildings.Length), false, count)).ToList();

        return SparklineBuilder.Build(samples, TrendRange.ForSparklines(_latestTick), BuildingsDecimals)?.Path ?? string.Empty;
    }

    /// <summary>
    /// Describes island keys as <c>session/island/area</c>, ordered.
    /// </summary>
    /// <param name="islands">The island keys</param>
    /// <returns>The descriptions</returns>
    private static List<string> Describe(IEnumerable<HistoryIslandKey> islands)
    {
        return [.. islands.Select(island => string.Create(CultureInfo.InvariantCulture, $"{island.SessionGuid}/{island.IslandId}/{island.AreaIndex}")).Order(StringComparer.Ordinal)];
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
    /// Gives the stroke colour of the sparkline of each trend cell, an empty text for a cell without one.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <returns>The colours</returns>
    private static List<string> Strokes<TComponent>(IRenderedComponent<TComponent> component)
        where TComponent : IComponent
    {
        return [.. TrendCells(component).Select(cell => cell.QuerySelector("svg path")?.GetAttribute("stroke") ?? string.Empty)];
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
    /// Renders the island list of the shown region as the body of the frame.
    /// </summary>
    /// <param name="builder">The render tree builder</param>
    private static void RenderSessionBody(RenderTreeBuilder builder)
    {
        builder.OpenComponent<RegionIslands>(0);
        builder.AddComponentParameter(1, nameof(RegionIslands.Profile), _profile.ToString());
        builder.AddComponentParameter(2, nameof(RegionIslands.Session), "3245");
        builder.CloseComponent();
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

    /// <summary>
    /// Creates a state of the profile with one island <c>Alpha</c> in the shown region.
    /// </summary>
    /// <returns>The state</returns>
    private static StoredProfileState CreateState()
    {
        return new StoredProfileState
               {
                   Profile = new StoredProfile
                             {
                                 Id = _profile,
                                 Name = "First"
                             },
                   LatestTick = PageTestHost.Tick(_profile, 0, PageTestHost.Island(Session, 1, "Alpha", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f))),
                   Islands = [
                                 new StoredIsland
                                 {
                                     SessionGuid = Session,
                                     IslandId = 1,
                                     AreaIndex = 0,
                                     Name = "Alpha"
                                 }
                             ]
               };
    }

    #endregion // Methods
}