using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The island list of a region.
/// </summary>
public partial class RegionIslands
{
    #region Properties

    /// <summary>
    /// Gets or sets the profile identity from the address.
    /// </summary>
    [Parameter]
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets the session GUID of the region from the address.
    /// </summary>
    [Parameter]
    public string? Session { get; set; }

    /// <summary>
    /// Gets the heading of the page: the region's name once it is shown.
    /// </summary>
    private string Heading => Mode == PageMode.Ready && View is { } view && FindRegion(view) is { } region
                                  ? PageNames.RegionText(region, view.Regions, Language)
                                  : Texts.RegionsEntry;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Finds the region that holds the session of the address.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The region, or <see langword="null" /> when the view has none</returns>
    private RegionView? FindRegion(ProfileView view)
    {
        return PageAddress.TryParseSession(Session, out var sessionGuid)
                   ? view.Regions.FirstOrDefault(region => region.SessionGuids.Contains(sessionGuid))
                   : null;
    }

    /// <summary>
    /// Orders the islands of a region by shown name, ties by key.
    /// </summary>
    /// <param name="region">The region</param>
    /// <returns>The islands</returns>
    private IEnumerable<IslandView> OrderedIslands(RegionView region)
    {
        return region.Islands.OrderBy(island => PageNames.Island(island.Name, Language), StringComparer.OrdinalIgnoreCase)
                             .ThenBy(island => island.Key.SessionGuid)
                             .ThenBy(island => island.Key.IslandId)
                             .ThenBy(island => island.Key.AreaIndex);
    }

    #endregion // Methods

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile, Session);

    /// <inheritdoc />
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        island = null;

        return PageAddress.TryParseProfile(Profile, out profileId) && PageAddress.TryParseSession(Session, out _)
                   ? PageMode.Loading
                   : PageMode.NotFound;
    }

    /// <inheritdoc />
    protected override bool IsKnown(ProfileView view)
    {
        return FindRegion(view) is not null;
    }

    #endregion // DataPageBase
}