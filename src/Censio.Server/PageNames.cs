using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The names the pages show.
/// </summary>
public static class PageNames
{
    #region Methods

    /// <summary>
    /// Gets the name of a good.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/></param>
    /// <returns>The name</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static ShownName Good(int productGuid, PageCatalog? catalog)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Gets the name of a region.
    /// </summary>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="isSession">Whether the region is a session without a catalog region</param>
    /// <param name="smallestSession">The smallest session GUID of the region</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/></param>
    /// <returns>The name</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static ShownName Region(int regionGuid, bool isSession, int smallestSession, PageCatalog? catalog)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Gets the shown form of an island name.
    /// </summary>
    /// <param name="name">The stored name</param>
    /// <param name="language">The language</param>
    /// <returns>The shown name</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string Island(string name, UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Gets the profiles to list, with their shown names.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="language">The language</param>
    /// <param name="limit">The maximum number of entries</param>
    /// <returns>The identities with their shown names</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static IReadOnlyList<KeyValuePair<Guid, string>> Profiles(IReadOnlyList<StoredProfile> profiles, UiLanguage language, int limit)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Orders the regions for a language.
    /// </summary>
    /// <param name="regions">The regions</param>
    /// <param name="language">The language</param>
    /// <returns>The ordered regions</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static IReadOnlyList<RegionView> OrderRegions(IReadOnlyList<RegionView> regions, UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Gets the text of a region in a list of regions.
    /// </summary>
    /// <param name="region">The region</param>
    /// <param name="regions">All regions</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string RegionText(RegionView region, IReadOnlyList<RegionView> regions, UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}