using System.Collections.ObjectModel;

namespace Censio.GameData;

/// <summary>
/// Builds the catalog from what the passes collected.
/// </summary>
internal static class GameCatalogBuilder
{
    #region Methods

    /// <summary>
    /// Builds the catalog.
    /// </summary>
    /// <param name="regionTypes">The names of the region types</param>
    /// <param name="assets">The collected assets</param>
    /// <param name="english">The English texts</param>
    /// <param name="german">The German texts</param>
    /// <returns>The catalog</returns>
    internal static GameCatalog Build(HashSet<string> regionTypes, GameCatalogAssets assets, GameCatalogTexts english, GameCatalogTexts german)
    {
        var dropped = 0;
        var regionLists = new Dictionary<int, IReadOnlyList<string>?>();
        var lists = new Dictionary<string, IReadOnlyList<string>>(ReferenceEqualityComparer.Instance);

        foreach (var (guid, region) in assets.Regions)
        {
            regionLists[guid] = RegionIdList(region.RegionId, regionTypes, ref dropped);
        }

        var entries = new Dictionary<int, GameCatalogEntry>();
        var ambiguous = new HashSet<int>(assets.AmbiguousGuids);

        foreach (var candidate in assets.Candidates)
        {
            var guid = candidate.Guid.GetValueOrDefault();

            if (entries.ContainsKey(guid))
            {
                continue;
            }

            var entry = CreateEntry(candidate, regionTypes, regionLists, assets.Regions, lists, (english.NameOf(candidate.TextId), german.NameOf(candidate.TextId)), ref dropped);

            if (entry is null)
            {
                continue;
            }

            entries.Add(guid, entry);

            if (candidate.TextId is { } textId && (english.Ambiguous.Contains(textId) || german.Ambiguous.Contains(textId)))
            {
                ambiguous.Add(guid);
            }
        }

        return new GameCatalog(entries.Values.Where(entry => ambiguous.Contains(entry.AssetGuid) == false).ToList(), ambiguous, assets.InheritedCount, dropped);
    }

    /// <summary>
    /// Gets the text identifiers the candidates use.
    /// </summary>
    /// <param name="assets">The collected assets</param>
    /// <returns>The text identifiers</returns>
    internal static HashSet<long> UsedTextIds(GameCatalogAssets assets)
    {
        return assets.Candidates.Where(candidate => candidate.TextId is not null).Select(candidate => candidate.TextId.GetValueOrDefault()).ToHashSet();
    }

    /// <summary>
    /// Creates the entry of a candidate.
    /// </summary>
    /// <param name="candidate">The candidate</param>
    /// <param name="regionTypes">The names of the region types</param>
    /// <param name="regionLists">The region types of the region assets</param>
    /// <param name="regions">The region assets by GUID</param>
    /// <param name="lists">The split lists by source string, compared by reference</param>
    /// <param name="names">The English and the German name of the candidate</param>
    /// <param name="dropped">The number of dropped region types</param>
    /// <returns>The entry, or <c>null</c> when the candidate is none</returns>
    private static GameCatalogEntry? CreateEntry(GameCatalogAsset candidate,
                                                 HashSet<string> regionTypes,
                                                 Dictionary<int, IReadOnlyList<string>?> regionLists,
                                                 Dictionary<int, GameCatalogAsset> regions,
                                                 Dictionary<string, IReadOnlyList<string>> lists,
                                                 (string? English, string? German) names,
                                                 ref int dropped)
    {
        var guid = candidate.Guid.GetValueOrDefault();

        if (candidate.SessionRegion is { } regionGuid && regionGuid != 0 && regionLists.TryGetValue(regionGuid, out var types))
        {
            return new GameCatalogEntry
                   {
                       AssetGuid = guid,
                       Kind = GameCatalogKind.Region,
                       EnglishName = names.English,
                       GermanName = names.German,
                       RegionGuid = regionGuid,
                       RegionTypes = types,
                       IconFilename = candidate.IconFilename ?? regions.GetValueOrDefault(regionGuid)?.IconFilename
                   };
        }

        if (candidate.Kind is not { } kind)
        {
            return null;
        }

        var list = kind == GameCatalogKind.Building ? candidate.BuildingRegions : candidate.ProductRegions;

        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = kind,
                   EnglishName = names.English,
                   GermanName = names.German,
                   RegionTypes = RegionList(list, regionTypes, lists, ref dropped),
                   IconFilename = candidate.IconFilename
               };
    }

    /// <summary>
    /// Gets the region types of a region asset.
    /// </summary>
    /// <param name="regionId">The stated region type, or <c>null</c></param>
    /// <param name="regionTypes">The names of the region types</param>
    /// <param name="dropped">The number of dropped region types</param>
    /// <returns>The list, or <c>null</c> when none is stated</returns>
    private static IReadOnlyList<string>? RegionIdList(string? regionId, HashSet<string> regionTypes, ref int dropped)
    {
        if (regionId is null)
        {
            return null;
        }

        if (regionTypes.Contains(regionId.Trim()))
        {
            return [regionId.Trim()];
        }

        dropped++;

        return [];
    }

    /// <summary>
    /// Splits an association list into its known region types, once per source string.
    /// </summary>
    /// <param name="list">The list, or <c>null</c> when none is stated</param>
    /// <param name="regionTypes">The names of the region types</param>
    /// <param name="lists">The split lists by source string, compared by reference</param>
    /// <param name="dropped">The number of dropped region types</param>
    /// <returns>The read-only region types, or <c>null</c> when none are stated</returns>
    private static IReadOnlyList<string>? RegionList(string? list, HashSet<string> regionTypes, Dictionary<string, IReadOnlyList<string>> lists, ref int dropped)
    {
        if (list is null)
        {
            return null;
        }

        if (lists.TryGetValue(list, out var known))
        {
            return known;
        }

        var result = new List<string>();

        foreach (var token in list.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (regionTypes.Contains(token))
            {
                result.Add(token);
            }
            else
            {
                dropped++;
            }
        }

        var shared = new ReadOnlyCollection<string>(result);

        lists.Add(list, shared);

        return shared;
    }

    #endregion // Methods
}