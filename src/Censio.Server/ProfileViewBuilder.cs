using Censio.Contracts;
using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Builds the view of a profile from its state.
/// </summary>
public static class ProfileViewBuilder
{
    #region Methods

    /// <summary>
    /// Builds the view of a profile.
    /// </summary>
    /// <param name="state">The profile state</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/> when none is usable</param>
    /// <param name="missingIcons">The references of the icons not stored, or <see langword="null"/> when unknown</param>
    /// <param name="island">The shown island, or <see langword="null"/> for none</param>
    /// <returns>The view</returns>
    public static ProfileView Build(StoredProfileState state, PageCatalog? catalog, IReadOnlySet<string>? missingIcons, IslandKey? island)
    {
        var tick = state.LatestTick;
        var tickIslands = new Dictionary<IslandKey, ContractTickIsland>();
        var names = new Dictionary<IslandKey, string>();

        foreach (var stored in state.Islands)
        {
            names[new IslandKey(stored.SessionGuid, stored.IslandId, stored.AreaIndex)] = stored.Name;
        }

        foreach (var tickIsland in tick?.Islands ?? [])
        {
            var key = new IslandKey(tickIsland.SessionGuid, tickIsland.IslandId, tickIsland.AreaIndex);

            tickIslands[key] = tickIsland;
            names.TryAdd(key, tickIsland.Name);
        }

        var groups = new Dictionary<(bool IsSession, int Guid), List<IslandView>>();

        foreach (var (key, name) in names)
        {
            var group = (IsSession: true, Guid: key.SessionGuid);

            if (catalog?.Find(key.SessionGuid) is { Kind: CatalogEntryKind.Region, RegionGuid: { } regionGuid })
            {
                group = (false, regionGuid);
            }

            if (groups.TryGetValue(group, out var islands) == false)
            {
                islands = [];
                groups[group] = islands;
            }

            islands.Add(new IslandView
                        {
                            Key = key,
                            Name = name,
                            Figures = tickIslands.TryGetValue(key, out var found) ? Figure(found) : null
                        });
        }

        var regions = groups.Select(pair => CreateRegion(pair.Key.IsSession, pair.Key.Guid, pair.Value, catalog)).OrderBy(region => region.SessionGuids[0]).ToList();
        var entries = island is { } shown && tickIslands.TryGetValue(shown, out var shownIsland)
                          ? shownIsland.Entries
                          : [];

        return new ProfileView
               {
                   ProfileId = state.Profile.Id,
                   ProfileName = state.Profile.Name,
                   AsOf = tick?.ReceiveTime,
                   Regions = regions,
                   Island = island,
                   Rows = [.. entries.Take(ProfileView.MaxRows).Select(entry => CreateRow(entry, catalog, missingIcons))],
                   RowCount = entries.Count
               };
    }

    /// <summary>
    /// Creates the view of a region.
    /// </summary>
    /// <param name="isSession">Whether the region is a session without a catalog region</param>
    /// <param name="regionGuid">The region GUID, or the session GUID of a session without a catalog region</param>
    /// <param name="islands">The islands of the region</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/></param>
    /// <returns>The region</returns>
    private static RegionView CreateRegion(bool isSession, int regionGuid, List<IslandView> islands, PageCatalog? catalog)
    {
        var sessions = islands.Select(island => island.Key.SessionGuid).Distinct().Order().ToList();
        var withFigures = islands.Select(island => island.Figures).OfType<KeyFigures>().ToList();

        return new RegionView
               {
                   RegionGuid = regionGuid,
                   IsSession = isSession,
                   SessionGuids = sessions,
                   Name = PageNames.Region(regionGuid, isSession, sessions[0], catalog),
                   Islands = [.. islands.OrderBy(island => island.Key.SessionGuid).ThenBy(island => island.Key.IslandId).ThenBy(island => island.Key.AreaIndex)],
                   Figures = withFigures.Count == 0
                                 ? null
                                 : new KeyFigures
                                   {
                                       Buildings = withFigures.Sum(figures => figures.Buildings),
                                       ProducedGoods = withFigures.Sum(figures => figures.ProducedGoods),
                                       GoodsInDeficit = withFigures.Sum(figures => figures.GoodsInDeficit)
                                   }
               };
    }

    /// <summary>
    /// Derives the key figures of an island of the latest tick.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The figures, or <see langword="null"/> when the island has no entries</returns>
    private static KeyFigures? Figure(ContractTickIsland island)
    {
        if (island.Entries.Count == 0)
        {
            return null;
        }

        return new KeyFigures
               {
                   Buildings = island.Entries.Sum(entry => entry.BuildingCount),
                   ProducedGoods = island.Entries.Count(entry => entry.BuildingCount > 0),
                   GoodsInDeficit = island.Entries.Count(entry => entry.Delta is { } delta && PageFormat.Round(delta) < 0)
               };
    }

    /// <summary>
    /// Creates the row of an entry.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/></param>
    /// <param name="missingIcons">The references of the icons not stored yet, or <see langword="null"/></param>
    /// <returns>The row</returns>
    private static GoodRow CreateRow(ContractTickEntry entry, PageCatalog? catalog, IReadOnlySet<string>? missingIcons)
    {
        var icon = catalog?.Find(entry.ProductGuid)?.IconReference;

        return new GoodRow
               {
                   ProductGuid = entry.ProductGuid,
                   Name = PageNames.Good(entry.ProductGuid, catalog),
                   IconReference = icon is not null && missingIcons?.Contains(icon) != true ? icon : null,
                   Production = entry.Generation,
                   Consumption = entry.Consumption,
                   Delta = entry.Delta,
                   Productivity = entry.AverageProductivity,
                   Buildings = entry.BuildingCount
               };
    }

    #endregion // Methods
}