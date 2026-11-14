using System.Globalization;

using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The names the pages show.
/// </summary>
public static class PageNames
{
    #region Constants

    /// <summary>
    /// The number of characters of a profile identity that tell equal names apart.
    /// </summary>
    private const int IdentityCharacters = 8;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Gets the name of a good.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/></param>
    /// <returns>The name</returns>
    public static ShownName Good(int productGuid, PageCatalog? catalog)
    {
        var entry = catalog?.Find(productGuid);

        return Pick(entry,
                    string.Format(CultureInfo.InvariantCulture, UiTexts.For(UiLanguage.English).GoodFallbackFormat, productGuid),
                    string.Format(CultureInfo.InvariantCulture, UiTexts.For(UiLanguage.German).GoodFallbackFormat, productGuid));
    }

    /// <summary>
    /// Gets the name of a region.
    /// </summary>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="isSession">Whether the region is a session without a catalog region</param>
    /// <param name="smallestSession">The smallest session GUID of the region</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/></param>
    /// <returns>The name</returns>
    public static ShownName Region(int regionGuid, bool isSession, int smallestSession, PageCatalog? catalog)
    {
        var fallback = string.Create(CultureInfo.InvariantCulture, $"Region {(isSession ? smallestSession : regionGuid)}");

        return Pick(isSession ? null : catalog?.Find(smallestSession), fallback, fallback);
    }

    /// <summary>
    /// Gets the shown form of an island name.
    /// </summary>
    /// <param name="name">The stored name</param>
    /// <param name="language">The language</param>
    /// <returns>The shown name</returns>
    public static string Island(string name, UiLanguage language)
    {
        if (string.IsNullOrEmpty(name) == false)
        {
            return name;
        }

        return UiTexts.For(language).UnnamedIsland;
    }

    /// <summary>
    /// Gets the profiles to list, with their shown names.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="language">The language</param>
    /// <param name="limit">The maximum number of entries</param>
    /// <returns>The identities with their shown names</returns>
    public static IReadOnlyList<KeyValuePair<Guid, string>> Profiles(IReadOnlyList<StoredProfile> profiles, UiLanguage language, int limit)
    {
        var unnamed = UiTexts.For(language).UnnamedProfile;
        var named = profiles.Select(profile => (profile.Id, Name: string.IsNullOrEmpty(profile.Name) ? unnamed : profile.Name)).ToList();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in named.Select(profile => profile.Name))
        {
            counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        return [
                   .. named.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(profile => profile.Id.ToString(), StringComparer.Ordinal)
                           .Take(limit)
                           .Select(profile => new KeyValuePair<Guid, string>(profile.Id, counts[profile.Name] > 1 ? $"{profile.Name} ({profile.Id.ToString()[..IdentityCharacters]})" : profile.Name))
               ];
    }

    /// <summary>
    /// Orders the regions for a language.
    /// </summary>
    /// <param name="regions">The regions</param>
    /// <param name="language">The language</param>
    /// <returns>The ordered regions</returns>
    public static IReadOnlyList<RegionView> OrderRegions(IReadOnlyList<RegionView> regions, UiLanguage language)
    {
        return [.. regions.OrderBy(region => region.Name.For(language), StringComparer.OrdinalIgnoreCase).ThenBy(region => region.RegionGuid)];
    }

    /// <summary>
    /// Gets the text of a region in a list of regions.
    /// </summary>
    /// <param name="region">The region</param>
    /// <param name="regions">All regions</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
    public static string RegionText(RegionView region, IReadOnlyList<RegionView> regions, UiLanguage language)
    {
        var name = region.Name.For(language);
        var equal = regions.Count(other => string.Equals(other.Name.For(language), name, StringComparison.OrdinalIgnoreCase));

        return equal > 1
                   ? string.Create(CultureInfo.InvariantCulture, $"{name} ({region.RegionGuid})")
                   : name;
    }

    /// <summary>
    /// Takes the names of a catalog entry, the other language's for a language the entry lacks, or the fallbacks.
    /// </summary>
    /// <param name="entry">The entry, or <see langword="null"/></param>
    /// <param name="englishFallback">The English name when the entry names nothing</param>
    /// <param name="germanFallback">The German name when the entry names nothing</param>
    /// <returns>The name</returns>
    private static ShownName Pick(PageCatalogEntry? entry, string englishFallback, string germanFallback)
    {
        var english = string.IsNullOrEmpty(entry?.English) ? null : entry.English;
        var german = string.IsNullOrEmpty(entry?.German) ? null : entry.German;

        if (english is null && german is null)
        {
            return new ShownName
                   {
                       English = englishFallback,
                       German = germanFallback
                   };
        }

        return new ShownName
               {
                   English = english ?? german!,
                   German = german ?? english!
               };
    }

    #endregion // Methods
}