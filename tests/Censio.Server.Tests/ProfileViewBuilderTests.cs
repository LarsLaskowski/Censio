using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Censio.Contracts;
using Censio.Server.Data;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="ProfileViewBuilder" />, over hand-built profile states and catalogs.
/// </summary>
[TestClass]
public sealed class ProfileViewBuilderTests
{
    #region Constants

    /// <summary>
    /// The text of a figure that is not there, in the descriptions.
    /// </summary>
    private const string NoFigures = "<no figures>";

    /// <summary>
    /// An icon reference the catalog names and the store holds.
    /// </summary>
    private const string StoredIcon = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// An icon reference the catalog names and the store lacks.
    /// </summary>
    private const string MissingIcon = "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The receive time of the latest tick of the built states.
    /// </summary>
    private static readonly DateTimeOffset _receiveTime = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The identity of the profile of the built states.
    /// </summary>
    private static readonly Guid _profileId = new("5a1b2c3d-0000-4000-8000-000000000001");

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Islands are grouped by the region GUID of the catalog region entry of their session: sessions sharing a region GUID
    /// form one region, a session with its own region GUID another, and a session without an entry is a region of its own.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ProfileViewBuilderGroupsSessionsByRegionGuid()
    {
        // Arrange
        var catalog = CreateCatalog(RegionEntry(10, 500, "Alpha", "Alpha DE"), RegionEntry(20, 500, "Zeta", "Zeta DE"), RegionEntry(30, 600, "Gallia", "Gallien"));
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1", Entry(1, 1, 1f)), TickIsland(20, 1, "Island 2", Entry(1, 1, 1f)), TickIsland(30, 1, "Island 3", Entry(1, 1, 1f)), TickIsland(40, 1, "Island 4", Entry(1, 1, 1f))));

        // Act
        var view = ProfileViewBuilder.Build(state, catalog, null, null);

        // Assert
        string[] expected = ["40 session [40] islands 40/1/0", "500 region [10,20] islands 10/1/0,20/1/0", "600 region [30] islands 30/1/0"];

        Assert.AreSequenceEqual(expected, DescribeRegions(view), "The islands should be grouped by the catalog region of their session");
    }

    /// <summary>
    /// Without a catalog, and for a session the catalog has no region entry for, each session is a region of its own,
    /// named <c>Region &lt;session GUID&gt;</c> in both languages.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ProfileViewBuilderSessionWithoutEntryIsOwnRegion()
    {
        // Arrange
        var catalog = CreateCatalog(RegionEntry(10, 500, "Alpha", "Alpha DE"), """{"guid":40,"kind":"good","names":{"en":"Not a region"},"regionGuid":null,"regionTypes":null,"icon":null}""");
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1", Entry(1, 1, 1f)), TickIsland(40, 1, "Island 4", Entry(1, 1, 1f)), TickIsland(40, 2, "Island 5", Entry(1, 1, 1f))));

        // Act
        var view = ProfileViewBuilder.Build(state, catalog, null, null);

        // Assert
        string[] expected = ["40 session [40] islands 40/1/0,40/2/0", "500 region [10] islands 10/1/0"];
        var own = view.Regions.Single(region => region.RegionGuid == 40);

        Assert.AreSequenceEqual(expected, DescribeRegions(view), "The session without a region entry should be a region of its own");
        Assert.AreEqual("Region 40 | Region 40", DescribeName(own.Name), "The session's region should be named by its session GUID in both languages");
    }

    /// <summary>
    /// A region is named by the region entry of its smallest session GUID, in both languages.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ProfileViewBuilderNamesRegionBySmallestSession()
    {
        // Arrange
        var catalog = CreateCatalog(RegionEntry(20, 500, "Zeta", "Zeta DE"), RegionEntry(10, 500, "Alpha", "Alpha DE"));
        var state = CreateState(CreateTick(TickIsland(20, 1, "Island 2", Entry(1, 1, 1f)), TickIsland(10, 1, "Island 1", Entry(1, 1, 1f))));

        // Act
        var view = ProfileViewBuilder.Build(state, catalog, null, null);

        // Assert
        Assert.HasCount(1, view.Regions, "Both sessions should form one region");
        Assert.AreEqual("Alpha | Alpha DE", DescribeName(view.Regions[0].Name), "The region should take the names of its smallest session's entry");
    }

    /// <summary>
    /// The key figures of an island are its buildings, its entries with buildings and its entries in deficit; an island the
    /// latest tick lacks has none; a region sums the figures of its islands with values, and every listed island counts.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ProfileViewBuilderSumsFiguresOverIslandsWithValues()
    {
        // Arrange
        var tick = CreateTick(TickIsland(10, 1, "Island 1", Entry(1, 2, -1f), Entry(2, 0, 1f), Entry(3, 3, null), Entry(4, 0, -2f)),
                              TickIsland(10, 2, "Island 2", Entry(1, 1, -0.5f)));
        var state = CreateState(tick, ListedIsland(10, 3, "Island 3"));

        // Act
        var view = ProfileViewBuilder.Build(state, null, null, null);

        // Assert
        string[] expected = ["10/1/0 Island 1 buildings 5 produced 2 deficit 2", "10/2/0 Island 2 buildings 1 produced 1 deficit 1", $"10/3/0 Island 3 {NoFigures}"];

        Assert.AreSequenceEqual(expected, DescribeIslands(view.Regions.Single()), "Each island should carry its own figures, the island the tick lacks none");
        Assert.AreEqual("buildings 6 produced 3 deficit 3", DescribeFigures(view.Regions.Single().Figures), "The region should sum the figures of its islands with values");
    }

    /// <summary>
    /// A delta that rounds to zero is no deficit: the count follows the value rounded to two decimals.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    [TestCategory("AC4")]
    public void ProfileViewBuilderTinyNegativeDeltaIsNoDeficit()
    {
        // Arrange
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1", Entry(1, 1, -0.004f), Entry(2, 1, 0.004f), Entry(3, 1, -0.006f))));

        // Act
        var view = ProfileViewBuilder.Build(state, null, null, null);

        // Assert
        Assert.AreEqual("buildings 3 produced 3 deficit 1", DescribeFigures(view.Regions.Single().Islands.Single().Figures), "Only the delta that rounds below zero should count as a deficit");
    }

    /// <summary>
    /// After the empty tick the game sends after a load, the islands stay listed without figures, and so does their
    /// region; an island the tick carries without entries has no figures either.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ProfileViewBuilderRegionWithoutValuesHasNoFigures()
    {
        // Arrange
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1")), ListedIsland(10, 2, "Island 2"));

        // Act
        var view = ProfileViewBuilder.Build(state, null, null, null);

        // Assert
        var region = view.Regions.Single();

        Assert.AreSequenceEqual(new[] { $"10/1/0 Island 1 {NoFigures}", $"10/2/0 Island 2 {NoFigures}" }, DescribeIslands(region), "Both islands should stay listed without figures");
        Assert.AreEqual(NoFigures, DescribeFigures(region.Figures), "A region without islands with values should have no figures");
    }

    /// <summary>
    /// The rows of the shown island carry each entry's values, its good's catalog names, and its icon only when the
    /// catalog names one that is stored; the view carries the profile and the receive time of the latest tick.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    [TestCategory("AC5")]
    public void ProfileViewBuilderRowsCarryValuesNamesAndStoredIcons()
    {
        // Arrange
        var catalog = CreateCatalog(GoodEntry(1, "Bread", "Brot", StoredIcon), GoodEntry(2, "Fish", "Fisch", MissingIcon));
        var island = new IslandKey(10, 1, 0);
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1", Entry(1, 2, -1.5f), Entry(2, 0, 0.25f), Entry(3, 4, null)), TickIsland(10, 2, "Island 2", Entry(9, 1, 1f))));

        // Act
        var view = ProfileViewBuilder.Build(state,
                                            catalog,
                                            new HashSet<string>(StringComparer.Ordinal)
                                            {
                                                MissingIcon
                                            },
                                            island);

        // Assert
        string[] expected = [
                                $"1 Bread | Brot icon {StoredIcon} production 1.5 consumption 2.5 delta -1.5 buildings 2 productivity 87.4",
                                "2 Fish | Fisch icon <none> production 1.5 consumption 2.5 delta 0.25 buildings 0 productivity 87.4",
                                "3 Good 3 | Ware 3 icon <none> production 1.5 consumption 2.5 delta null buildings 4 productivity 87.4"
                            ];

        Assert.AreEqual($"{_profileId} Profile 7f3c {_receiveTime:O} island {island}", $"{view.ProfileId} {view.ProfileName} {view.AsOf:O} island {view.Island}", "The view should carry the profile, its latest receive time and the shown island");
        Assert.AreSequenceEqual(expected, DescribeRows(view), "The rows should be the shown island's entries with their values, names and stored icons");
        Assert.AreEqual(3, view.RowCount, "The row count should be the island's number of entries");
    }

    /// <summary>
    /// Of an island with more than 500 entries the first 500 in tick order are kept, and the count names all entries; a
    /// cut after ordering by name would keep others.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void ProfileViewBuilderKeepsFirst500EntriesInTickOrder()
    {
        // Arrange
        var entries = Enumerable.Range(1, 502).Select(guid => Entry(guid, 1, 1f)).ToArray();
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1", entries)));

        // Act
        var view = ProfileViewBuilder.Build(state, null, null, new IslandKey(10, 1, 0));

        // Assert
        Assert.AreSequenceEqual(Enumerable.Range(1, ProfileView.MaxRows).ToList(), view.Rows.Select(row => row.ProductGuid).Order().ToList(), "The rows should be the first 500 entries in tick order");
        Assert.AreEqual(502, view.RowCount, "The row count should name every entry of the island");
    }

    /// <summary>
    /// The built view holds no reference to the profile state or its tick: both are collected once only the view is kept.
    /// </summary>
    [TestMethod]
    [TestCategory("AC10")]
    public void ProfileViewBuilderViewHoldsNoState()
    {
        // Arrange
        var catalog = CreateCatalog(GoodEntry(1, "Bread", "Brot", StoredIcon));

        // Act
        var (view, stateReference, tickReference) = BuildAndForget(catalog);

        CollectGarbage();

        // Assert
        Assert.HasCount(1, view.Rows, "The view should keep its row, or the test would prove less");
        Assert.IsFalse(stateReference.IsAlive, "The view should hold no reference to the profile state");
        Assert.IsFalse(tickReference.IsAlive, "The view should hold no reference to the latest tick");
    }

    /// <summary>
    /// Collects every unreachable object, so that a weak reference to one is cleared.
    /// </summary>
#pragma warning disable S1215 // The test checks that the view leaves the state collectable, which only a collection shows
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
#pragma warning restore S1215

    /// <summary>
    /// Builds a view of a state that only weak references reach afterwards.
    /// </summary>
    /// <param name="catalog">The catalog lookup</param>
    /// <returns>The view and weak references to the state and its latest tick</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ProfileView View, WeakReference State, WeakReference Tick) BuildAndForget(PageCatalog catalog)
    {
        var state = CreateState(CreateTick(TickIsland(10, 1, "Island 1", Entry(1, 1, -1f))));
        var view = ProfileViewBuilder.Build(state, catalog, new HashSet<string>(StringComparer.Ordinal), new IslandKey(10, 1, 0));

        return (view, new WeakReference(state), new WeakReference(state.LatestTick));
    }

    /// <summary>
    /// Reads a catalog document of entries into the lookup of the pages.
    /// </summary>
    /// <param name="entries">The entries as JSON</param>
    /// <returns>The lookup</returns>
    private static PageCatalog CreateCatalog(params string[] entries)
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[{string.Join(',', entries)}]}}");
        var document = CatalogDocumentReader.Read(bytes).Document ?? throw new InvalidOperationException("The test catalog should be read");

        return PageCatalog.From(document);
    }

    /// <summary>
    /// Gives the JSON of a catalog region entry.
    /// </summary>
    /// <param name="sessionGuid">The session GUID of the entry</param>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <returns>The JSON</returns>
    private static string RegionEntry(int sessionGuid, int regionGuid, string english, string german)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{{\"guid\":{sessionGuid},\"kind\":\"region\",\"names\":{{\"de\":\"{german}\",\"en\":\"{english}\"}},\"regionGuid\":{regionGuid},\"regionTypes\":null,\"icon\":null}}");
    }

    /// <summary>
    /// Gives the JSON of a catalog good entry.
    /// </summary>
    /// <param name="guid">The product GUID</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <param name="icon">The icon reference</param>
    /// <returns>The JSON</returns>
    private static string GoodEntry(int guid, string english, string german, string icon)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{{\"guid\":{guid},\"kind\":\"good\",\"names\":{{\"de\":\"{german}\",\"en\":\"{english}\"}},\"regionGuid\":null,\"regionTypes\":null,\"icon\":\"{icon}\"}}");
    }

    /// <summary>
    /// Creates a state of the test profile from its latest tick: the tick's islands are listed, and further listed islands
    /// the tick lacks.
    /// </summary>
    /// <param name="tick">The latest tick</param>
    /// <param name="others">The listed islands the tick lacks</param>
    /// <returns>The state</returns>
    private static StoredProfileState CreateState(ContractTick tick, params StoredIsland[] others)
    {
        var listed = tick.Islands.Select(island => ListedIsland(island.SessionGuid, island.IslandId, island.Name)).Concat(others).ToList();

        return new StoredProfileState
               {
                   Profile = new StoredProfile
                             {
                                 Id = _profileId,
                                 Name = "Profile 7f3c"
                             },
                   LatestTick = tick,
                   Islands = listed
               };
    }

    /// <summary>
    /// Creates a listed island.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="islandId">The island ID</param>
    /// <param name="name">The name</param>
    /// <returns>The island</returns>
    private static StoredIsland ListedIsland(int sessionGuid, byte islandId, string name)
    {
        return new StoredIsland
               {
                   SessionGuid = sessionGuid,
                   IslandId = islandId,
                   AreaIndex = 0,
                   Name = name
               };
    }

    /// <summary>
    /// Creates a tick of the test profile.
    /// </summary>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static ContractTick CreateTick(params ContractTickIsland[] islands)
    {
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = _profileId,
                   ReceiveTime = _receiveTime,
                   GameTime = 3_600_000,
                   Islands = islands
               };
    }

    /// <summary>
    /// Creates an island of a tick with area index 0.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="islandId">The island ID</param>
    /// <param name="name">The name</param>
    /// <param name="entries">The entries</param>
    /// <returns>The island</returns>
    private static ContractTickIsland TickIsland(int sessionGuid, byte islandId, string name, params ContractTickEntry[] entries)
    {
        return new ContractTickIsland
               {
                   SessionGuid = sessionGuid,
                   IslandId = islandId,
                   AreaIndex = 0,
                   SessionId = 1,
                   Name = name,
                   Entries = entries
               };
    }

    /// <summary>
    /// Creates an entry with a building count and a delta; production 1.5, consumption 2.5, productivity 87.4.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="buildingCount">The building count</param>
    /// <param name="delta">The delta, or <see langword="null" /></param>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry(int productGuid, int buildingCount, float? delta)
    {
        return new ContractTickEntry
               {
                   ProductGuid = productGuid,
                   Generation = 1.5f,
                   Consumption = 2.5f,
                   Delta = delta,
                   PerfectGeneration = 1.75f,
                   PerfectConsumption = 2.25f,
                   BuildingCount = buildingCount,
                   Maintenance = 40,
                   Income = 12.5f,
                   Profit = -27,
                   SummedProductivity = 2.5f,
                   AverageProductivity = 87.4f,
                   WorkforceAmounts = new Dictionary<int, int>(),
                   BuildingAmounts = new Dictionary<int, int>()
               };
    }

    /// <summary>
    /// Describes the regions of a view, ordered by GUID: GUID, kind, sessions and island keys.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeRegions(ProfileView view)
    {
        return [.. view.Regions.OrderBy(region => region.RegionGuid).Select(region => $"{region.RegionGuid} {(region.IsSession ? "session" : "region")} [{string.Join(',', region.SessionGuids)}] islands {string.Join(',', region.Islands.Select(island => DescribeKey(island.Key)).Order(StringComparer.Ordinal))}")];
    }

    /// <summary>
    /// Describes the islands of a region, ordered by key: key, name and figures.
    /// </summary>
    /// <param name="region">The region</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeIslands(RegionView region)
    {
        return [.. region.Islands.OrderBy(island => island.Key.SessionGuid).ThenBy(island => island.Key.IslandId).ThenBy(island => island.Key.AreaIndex).Select(island => $"{DescribeKey(island.Key)} {island.Name} {DescribeFigures(island.Figures)}")];
    }

    /// <summary>
    /// Describes key figures.
    /// </summary>
    /// <param name="figures">The figures, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string DescribeFigures(KeyFigures? figures)
    {
        return figures is null
                   ? NoFigures
                   : $"buildings {figures.Buildings} produced {figures.ProducedGoods} deficit {figures.GoodsInDeficit}";
    }

    /// <summary>
    /// Describes the rows of a view, ordered by product GUID.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The lines</returns>
    private static List<string> DescribeRows(ProfileView view)
    {
        return [.. view.Rows.OrderBy(row => row.ProductGuid).Select(row => $"{row.ProductGuid} {DescribeName(row.Name)} icon {row.IconReference ?? "<none>"} production {DescribeValue(row.Production)} consumption {DescribeValue(row.Consumption)} delta {DescribeValue(row.Delta)} buildings {row.Buildings} productivity {DescribeValue(row.Productivity)}")];
    }

    /// <summary>
    /// Describes a value of a row in its shortest round-trip form.
    /// </summary>
    /// <param name="value">The value, or <see langword="null" /></param>
    /// <returns>The text</returns>
    private static string DescribeValue(float? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    }

    /// <summary>
    /// Describes a shown name as its English and its German form.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The text</returns>
    private static string DescribeName(ShownName name)
    {
        return $"{name.English} | {name.German}";
    }

    /// <summary>
    /// Describes an island key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The text</returns>
    private static string DescribeKey(IslandKey key)
    {
        return $"{key.SessionGuid}/{key.IslandId}/{key.AreaIndex}";
    }

    #endregion // Methods
}