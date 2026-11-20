using Bunit;
using Bunit.TestDoubles;

using Censio.Companion.Core;
using Censio.Contracts;
using Censio.Pipe;
using Censio.Server.Components;
using Censio.Server.Components.Pages;
using Censio.Server.Data;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the trade-routes page <see cref="TradeRoutes" />.
/// </summary>
[TestClass]
public sealed class TradeRoutesTests
{
    #region Constants

    /// <summary>
    /// The text of an empty folder and of a route without stations.
    /// </summary>
    private const string Dash = "–";

    /// <summary>
    /// The session GUID of the listed island of the tests.
    /// </summary>
    private const int FirstSession = 3245;

    /// <summary>
    /// The session GUID of an island no island list names.
    /// </summary>
    private const int UnlistedSession = 9999;

    /// <summary>
    /// The name of the listed island of the tests.
    /// </summary>
    private const string FirstIslandName = "Isle A";

    /// <summary>
    /// The English no-data text of the data pages.
    /// </summary>
    private const string NoDataText = "No data yet. The pages fill once the Companion sends data.";

    /// <summary>
    /// The separator between two stations of a route (plan, *Approach*).
    /// </summary>
    private const string Arrow = " → ";

    /// <summary>
    /// The name of the protocol-3 fixture with decoded trade routes.
    /// </summary>
    private const string TradeRoutesFixture = "protocol-3-trade-routes.jsonl";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The receive time at second 0, as <see cref="PageTestHost.BaseReceiveTimeText" /> gives it.
    /// </summary>
    private static readonly DateTimeOffset _baseReceiveTime = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The first profile of the tests.
    /// </summary>
    private static readonly Guid _firstProfile = new("11111111-1111-4111-8111-111111111111");

    /// <summary>
    /// The second profile of the tests.
    /// </summary>
    private static readonly Guid _secondProfile = new("22222222-2222-4222-8222-222222222222");

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TradeRoutesTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TradeRoutesTests(TestContext testContext)
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
    /// A replay of the protocol-3 fixture makes the profile's page, headed and titled <c>Trade routes</c>, list the 57
    /// routes of the last trade-routes frame (the repeated name as two rows) with name, folder, ships, lost ships, major
    /// and minor problems and the stations' island names in route order, under the as-of time of that frame, with the
    /// Companion banner and without <c>Delete history</c>.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task TradeRoutesFixtureReplayListsLastFrameRoutes()
    {
        // Arrange
        var messages = await ReplayFixtureAsync();
        var last = messages.OfType<ContractTradeRoutes>().Last();
        var lastTick = messages.OfType<ContractTick>().Last();
        var expected = ExpectedFixtureRows(messages, last);

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreAsync(host, messages);

        Assert.HasCount(57, last.Routes, "The last trade-routes frame should hold 57 routes, or the test would prove less");
        Assert.HasCount(56, last.Routes.Select(route => route.Name).Distinct(StringComparer.Ordinal).ToList(), "The last frame should repeat one route name, or the test would prove less");
        Assert.AreNotEqual(lastTick.ReceiveTime, last.ReceiveTime, "The last tick and the last trade routes should differ in time, or the test would prove less");

        // Act
        var page = RenderPicked(host, _firstProfile, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 57, PageTestHost.WaitBound);

        Assert.AreEqual("Trade routes", page.Find("h1").TextContent.Trim(), "The heading should name the trade routes");
        Assert.AreEqual("Censio – Trade routes", TabTitle(host, page), "The tab title should name the trade routes");
        Assert.AreSequenceEqual(PageTestHost.Expect("Route", "Folder", "Ships", "Lost ships", "Major problems", "Minor problems", "Stations"), PageTestHost.Headers(page), "The table should have the columns of the plan in order");
        Assert.AreSequenceEqual(expected, Describe(page).Order(StringComparer.Ordinal).ToList(), "Every route of the last frame should be one row with its values and its stations' island names in route order");
        Assert.AreEqual(last.ReceiveTime, page.FindComponent<DataStatus>().Instance.AsOf, "The as-of time should be the receive time of the last trade routes, not the latest tick's");
        Assert.AreEqual("The Companion is not connected.", page.Find(".mud-alert").TextContent.Trim(), "The page should show the Companion banner");
        Assert.IsEmpty(page.FindAll("button").Where(button => button.TextContent.Trim() == "Delete history").ToList(), "The page should offer no Delete history button");
    }

    /// <summary>
    /// On a message with counts above 0, the routes with problems or lost ships come first, the summary line counts them,
    /// and each count above 0 of lost ships, major and minor problems shows its number in the error colour; ships, a 0
    /// and a negative count show no colour.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="summary">The summary line of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(UiLanguage.English, "4 trade routes, 3 with problems or lost ships.")]
    [DataRow(UiLanguage.German, "4 Handelsrouten, 3 mit Problemen oder verlorenen Schiffen.")]
    public async Task TradeRoutesCountsAboveZeroShowErrorColour(UiLanguage language, string summary)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0));
        await StoreRoutesAsync(host,
                               Message(_firstProfile,
                                       10,
                                       Route(1, "Calm", "F", (0, 0, 5, -1), Station(FirstSession, 1)),
                                       Route(2, "Lost", "F", (0, 0, 2, 1), Station(FirstSession, 1)),
                                       Route(3, "Minor", "F", (0, 3, 1, 0), Station(FirstSession, 1)),
                                       Route(4, "Major", "F", (2, 0, 1, 0), Station(FirstSession, 1))));

        // Act
        var page = RenderPicked(host, _firstProfile, language);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 4, PageTestHost.WaitBound);

        string[] expected = [
                                "Major|F|1|0|2|0|Isle A",
                                "Minor|F|1|0|0|3|Isle A",
                                "Lost|F|2|1|0|0|Isle A",
                                "Calm|F|5|-1|0|0|Isle A"
                            ];
        string[] colours = [
                               "none|none|error|none",
                               "none|none|none|error",
                               "none|error|none|none",
                               "none|none|none|none"
                           ];

        Assert.AreSequenceEqual(expected, Describe(page), "The routes with problems or lost ships should come first, each count shown as its number");
        Assert.AreSequenceEqual(colours, CountColours(page), "Each count above 0 of lost ships, major or minor problems should show in the error colour, nothing else");
        Assert.Contains(summary, PageTestHost.Text(page), "The page should show the summary line of the spec");
    }

    /// <summary>
    /// The page without a profile in its address shows the trade routes of the profile with the newest tick and follows a
    /// newer one.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task TradeRoutesNewestAddressFollowsNewestProfile()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0));
        await StoreRoutesAsync(host, Message(_firstProfile, 5, Route(1, "First route", "F", (0, 0, 1, 0), Station(FirstSession, 1))));
        await StoreRoutesAsync(host, Message(_secondProfile, 6, Route(1, "Second route", "F", (0, 0, 1, 0), Station(FirstSession, 1))));

        var page = RenderUnpicked(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("First route"), RouteNames(page), "The page should show the routes of the only profile with a tick"), PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(FirstSessionTick(_secondProfile, 10));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Second route"), RouteNames(page), "The page should follow the profile with the newest tick"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// An unknown or malformed profile in the address shows the not-found text, without a table and without echoing the
    /// address.
    /// </summary>
    /// <param name="segment">The repeated part of the address segment</param>
    /// <param name="repeat">The number of repetitions</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("3f2504e0-4f89-41d3-9a0c-0305e82c3301", 1)]
    [DataRow("x", 1)]
    [DataRow("a", 2000)]
    [DataRow("%3Cb%3E", 1)]
    public async Task TradeRoutesUnknownOrMalformedProfileShowsNotFound(string segment, int repeat)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0));
        await StoreRoutesAsync(host, Message(_firstProfile, 5, Route(1, "First route", "F", (0, 0, 1, 0), Station(FirstSession, 1))));

        var address = string.Concat(Enumerable.Repeat(segment, repeat));

        // Act
        var page = host.Render<TradeRoutes>(UiLanguage.English, parameters => parameters.Add(tradeRoutes => tradeRoutes.Profile, address));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("Not found.", PageTestHost.Text(page), "An unknown or malformed address should show the not-found text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "An unknown or malformed address should show no table");
        Assert.DoesNotContain(address, PageTestHost.Text(page), "The page should not echo the address");
    }

    /// <summary>
    /// A newer trade-routes message of the shown profile replaces the shown routes without a navigation.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task TradeRoutesNewerMessageUpdatesOpenPage()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0));
        await StoreRoutesAsync(host, Message(_firstProfile, 10, Route(1, "Old route", "F", (0, 0, 1, 0), Station(FirstSession, 1))));

        var page = RenderPicked(host, _firstProfile, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Old route"), RouteNames(page), "The page should show the stored routes"), PageTestHost.WaitBound);

        // Act
        await StoreRoutesAsync(host, Message(_firstProfile, 20, Route(2, "New route", "F", (0, 0, 1, 0), Station(FirstSession, 1)), Route(3, "Other route", "F", (0, 0, 1, 0))));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("New route", "Other route"), RouteNames(page), "The page should show the routes of the newer message"), PageTestHost.WaitBound);
        Assert.AreEqual(_baseReceiveTime.AddSeconds(20), page.FindComponent<DataStatus>().Instance.AsOf, "The as-of time should follow the newer message");
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "The update should not navigate or reload");
    }

    /// <summary>
    /// A profile that has never received trade routes shows the spec's text for none, and one whose latest message holds
    /// no route the text for an empty set, without a table.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="routesStored">Whether an empty trade-routes message is stored</param>
    /// <param name="expected">The text of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, false, "No trade routes received yet.")]
    [DataRow(UiLanguage.German, false, "Noch keine Handelsrouten empfangen.")]
    [DataRow(UiLanguage.English, true, "No trade routes in the latest data.")]
    [DataRow(UiLanguage.German, true, "Keine Handelsrouten in den letzten Daten.")]
    public async Task TradeRoutesWithoutOrEmptyTradeRoutesShowsSpecTexts(UiLanguage language, bool routesStored, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0));
        await StoreEmptyRoutesAsync(host, routesStored);

        // Act
        var page = RenderPicked(host, _firstProfile, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains(expected, PageTestHost.Text(page), "The page should show the spec's text instead of a table"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "The page should show no table");
    }

    /// <summary>
    /// A route, folder and island name that look like markup show their characters in the table, not markup.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task TradeRoutesMarkupNameShownAsText()
    {
        // Arrange
        const string routeName = "<b>Ostia</b> & <i>co</i>";
        const string folderName = "<i>Folder</i>";
        const string islandName = "<b>Isle</b>";

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_firstProfile, 0, PageTestHost.Island(FirstSession, 1, islandName, PageTestHost.Entry(1, 1f, 1f, 0.5f, 3, 100f))));
        await StoreRoutesAsync(host, Message(_firstProfile, 5, Route(1, routeName, folderName, (0, 0, 1, 0), Station(FirstSession, 1))));

        // Act
        var page = RenderPicked(host, _firstProfile, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(new[] { $"{routeName}|{folderName}|1|0|0|0|{islandName}" }, Describe(page), "The names should show their characters");
        Assert.IsEmpty(page.FindAll("tbody b, tbody i"), "No name should be rendered as markup");
    }

    /// <summary>
    /// The page without a profile in its address shows the no-data text of the data pages while no profile has a tick,
    /// even when trade routes are stored.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expected">The text of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, NoDataText)]
    [DataRow(UiLanguage.German, "Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.")]
    public async Task TradeRoutesNewestWithoutProfileShowsNoData(UiLanguage language, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreRoutesAsync(host, Message(_firstProfile, 5, Route(1, "First route", "F", (0, 0, 1, 0), Station(FirstSession, 1))));

        // Act
        var page = RenderUnpicked(host, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains(expected, PageTestHost.Text(page), "The page should show the no-data text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "The page should show no table without a profile with a tick");
    }

    /// <summary>
    /// A message with a repeated route ID, lost ships above ships, a negative count, a station no island list names and a
    /// route without stations is shown row for row as received: nothing merged, every count as it came, the fallback name
    /// for the unnamed island, the unnamed-route text, a dash for the empty folder and for the route without stations.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="first">The expected first row</param>
    /// <param name="second">The expected second row</param>
    /// <param name="third">The expected third row</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    [TestCategory("AC5")]
    [DataRow(UiLanguage.English, "Ostia|F|1|3|0|0|Isle A → Island 9999/2/0", "Ostia|F|2|0|0|0|–", "Unnamed route|–|0|0|-1|0|Isle A")]
    [DataRow(UiLanguage.German, "Ostia|F|1|3|0|0|Isle A → Insel 9999/2/0", "Ostia|F|2|0|0|0|–", "Unbenannte Route|–|0|0|-1|0|Isle A")]
    public async Task TradeRoutesRepeatedIdAndRouteWithoutStationsShownAsReceived(UiLanguage language, string first, string second, string third)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0));
        await StoreRoutesAsync(host,
                               Message(_firstProfile,
                                       5,
                                       Route(6, string.Empty, string.Empty, (-1, 0, 0, 0), Station(FirstSession, 1)),
                                       Route(5, "Ostia", "F", (0, 0, 2, 0)),
                                       Route(5, "Ostia", "F", (0, 0, 1, 3), Station(FirstSession, 1), Station(UnlistedSession, 2))));

        // Act
        var page = RenderPicked(host, _firstProfile, language);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 3, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(PageTestHost.Expect(first, second, third), Describe(page), "Every route should be one row as received, the repeated ID included");
        Assert.AreEqual(Dash, Rows(page)[1][6], "The route without stations should show a dash in its stations cell");
    }

    /// <summary>
    /// Adds the contract message of an item of the assembler, if it is a tick or a set of trade routes.
    /// </summary>
    /// <param name="messages">The messages</param>
    /// <param name="result">The item</param>
    /// <param name="stamp">The stamp of the last read</param>
    private static void AddConverted(List<ContractMessage> messages, PipeTickResult result, DateTimeOffset stamp)
    {
        if (result.Tick is { } tick)
        {
            messages.Add(ContractMessageConverter.ToTick(tick, _firstProfile));
        }

        if (result.TradeRoutes is { } routes)
        {
            messages.Add(ContractMessageConverter.ToTradeRoutes(routes, _firstProfile, stamp));
        }
    }

    /// <summary>
    /// Describes the rows a page should show for the last trade routes of the fixture, ordered ordinally: each route's
    /// name, folder, counts and its stations named by the island names of the ticks.
    /// </summary>
    /// <param name="messages">The messages of the replay</param>
    /// <param name="last">The last trade routes</param>
    /// <returns>The rows</returns>
    private static List<string> ExpectedFixtureRows(List<ContractMessage> messages, ContractTradeRoutes last)
    {
        var names = new Dictionary<(int, byte, byte), string>();

        foreach (var island in messages.OfType<ContractTick>().SelectMany(tick => tick.Islands))
        {
            names[(island.SessionGuid, island.IslandId, island.AreaIndex)] = island.Name;
        }

        return [
                   .. last.Routes.Select(route => $"{route.Name}|{(route.Folder.Length == 0 ? Dash : route.Folder)}|{route.Ships}|{route.LostShips}|{route.MajorProblems}|{route.MinorProblems}|{string.Join(Arrow, route.Stations.Select(station => names[(station.SessionGuid, station.IslandId, station.AreaIndex)]))}")
                                 .Select(Normalize)
                                 .Order(StringComparer.Ordinal)
               ];
    }

    /// <summary>
    /// Creates a set of trade routes.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds after the base time</param>
    /// <param name="routes">The routes</param>
    /// <returns>The set</returns>
    private static ContractTradeRoutes Message(Guid profileId, long receiveSeconds, params ContractTradeRoute[] routes)
    {
        return new ContractTradeRoutes
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = _baseReceiveTime.AddSeconds(receiveSeconds),
                   Routes = routes
               };
    }

    /// <summary>
    /// Creates a route.
    /// </summary>
    /// <param name="id">The route ID</param>
    /// <param name="name">The name</param>
    /// <param name="folder">The folder</param>
    /// <param name="counts">The major problems, minor problems, ships and lost ships</param>
    /// <param name="stations">The stations</param>
    /// <returns>The route</returns>
    private static ContractTradeRoute Route(int id, string name, string folder, (int Major, int Minor, int Ships, int Lost) counts, params ContractTradeRouteStation[] stations)
    {
        return new ContractTradeRoute
               {
                   Id = id,
                   Name = name,
                   Folder = folder,
                   MajorProblems = counts.Major,
                   MinorProblems = counts.Minor,
                   Ships = counts.Ships,
                   LostShips = counts.Lost,
                   Stations = stations
               };
    }

    /// <summary>
    /// Creates a station in area 0.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="islandId">The island ID</param>
    /// <returns>The station</returns>
    private static ContractTradeRouteStation Station(int sessionGuid, byte islandId)
    {
        return new ContractTradeRouteStation
               {
                   SessionGuid = sessionGuid,
                   IslandId = islandId,
                   AreaIndex = 0
               };
    }

    /// <summary>
    /// Creates a tick with the listed island of the tests.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds</param>
    /// <returns>The tick</returns>
    private static ContractTick FirstSessionTick(Guid profileId, long receiveSeconds)
    {
        return PageTestHost.Tick(profileId, receiveSeconds, PageTestHost.Island(FirstSession, 1, FirstIslandName, PageTestHost.Entry(1, 1f, 1f, 0.5f, 3, 100f)));
    }

    /// <summary>
    /// Renders the page of a profile.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="profileId">The profile</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<TradeRoutes> RenderPicked(PageTestHost host, Guid profileId, UiLanguage language)
    {
        return host.Render<TradeRoutes>(language, parameters => parameters.Add(tradeRoutes => tradeRoutes.Profile, profileId.ToString()));
    }

    /// <summary>
    /// Renders the page without a profile in its address.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<TradeRoutes> RenderUnpicked(PageTestHost host, UiLanguage language)
    {
        return host.Render<TradeRoutes>(language,
                                        _ =>
                                        {
                                        });
    }

    /// <summary>
    /// Gives the text of the page's tab title.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="page">The rendered page</param>
    /// <returns>The title</returns>
    private static string TabTitle(PageTestHost host, IRenderedComponent<TradeRoutes> page)
    {
        var content = page.FindComponent<PageTitle>().Instance.ChildContent ?? throw new AssertFailedException("The page title should have content");

        return PageTestHost.Text(host.Context.Render(content)).Trim();
    }

    /// <summary>
    /// Gives the cell texts of each body row, each with its runs of white space made one space.
    /// </summary>
    /// <param name="page">The rendered page</param>
    /// <returns>The rows</returns>
    private static List<List<string>> Rows(IRenderedComponent<TradeRoutes> page)
    {
        return [.. page.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td").Select(cell => Normalize(cell.TextContent)).ToList())];
    }

    /// <summary>
    /// Describes each body row as its cell texts joined by <c>|</c>.
    /// </summary>
    /// <param name="page">The rendered page</param>
    /// <returns>The rows</returns>
    private static List<string> Describe(IRenderedComponent<TradeRoutes> page)
    {
        return [.. Rows(page).Select(cells => string.Join('|', cells))];
    }

    /// <summary>
    /// Gives the shown route names in row order.
    /// </summary>
    /// <param name="page">The rendered page</param>
    /// <returns>The names</returns>
    private static List<string> RouteNames(IRenderedComponent<TradeRoutes> page)
    {
        return [.. Rows(page).Select(cells => cells[0])];
    }

    /// <summary>
    /// Describes the colours of the ships, lost ships, major and minor problems cells of each body row.
    /// </summary>
    /// <param name="page">The rendered page</param>
    /// <returns>The colours joined by <c>|</c>, one line per row</returns>
    private static List<string> CountColours(IRenderedComponent<TradeRoutes> page)
    {
        return [.. page.FindAll("tbody tr").Select(row => string.Join('|', row.QuerySelectorAll("td").Skip(2).Take(4).Select(PageTestHost.Colour)))];
    }

    /// <summary>
    /// Makes every run of white space of a text one space and trims it.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The normalized text</returns>
    private static string Normalize(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Replays the fixture through the message reader and the tick assembler and converts each tick and each set of trade
    /// routes as the Companion does, the trade routes with the stamp of their read.
    /// </summary>
    /// <returns>The contract messages in replay order</returns>
    private async Task<List<ContractMessage>> ReplayFixtureAsync()
    {
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);
        var stamp = DateTimeOffset.UnixEpoch;

        using var replay = new PipeReplayStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", TradeRoutesFixture)), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock, (_, readStamp) => stamp = readStamp);

        var messages = new List<ContractMessage>();
        var result = await assembler.ReadAsync(TestContext.CancellationToken);

        while (result.Kind != PipeTickResultKind.EndOfStream)
        {
            AddConverted(messages, result, stamp);

            result = await assembler.ReadAsync(TestContext.CancellationToken);
        }

        return messages;
    }

    /// <summary>
    /// Stores ticks and trade routes in order.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="messages">The messages</param>
    /// <returns>A task that completes when every message is stored</returns>
    private async Task StoreAsync(PageTestHost host, List<ContractMessage> messages)
    {
        foreach (var message in messages)
        {
            await (message is ContractTick tick
                       ? host.StoreTicksAsync(tick)
                       : StoreRoutesAsync(host, (ContractTradeRoutes)message));
        }
    }

    /// <summary>
    /// Stores a set of trade routes and fails when it was not stored.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="routes">The set</param>
    /// <returns>A task that completes when the set is stored</returns>
    private async Task StoreRoutesAsync(PageTestHost host, ContractTradeRoutes routes)
    {
        var result = await host.State.StoreTradeRoutesAsync(routes, TestContext.CancellationToken);

        Assert.AreEqual(ServerStoreOutcome.Stored, result.Outcome, "The test trade routes should be stored");
    }

    /// <summary>
    /// Stores a set without routes of the first profile when asked to.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="store">Whether to store it</param>
    /// <returns>A task that completes when the set is stored, or at once</returns>
    private Task StoreEmptyRoutesAsync(PageTestHost host, bool store)
    {
        return store
                   ? StoreRoutesAsync(host, Message(_firstProfile, 5))
                   : Task.CompletedTask;
    }

    #endregion // Methods
}