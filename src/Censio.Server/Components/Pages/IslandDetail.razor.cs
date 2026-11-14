using System.Globalization;

using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The goods of an island.
/// </summary>
public partial class IslandDetail
{
    #region Properties

    /// <summary>
    /// Gets or sets the profile identity from the address.
    /// </summary>
    [Parameter]
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets the session GUID from the address.
    /// </summary>
    [Parameter]
    public string? Session { get; set; }

    /// <summary>
    /// Gets or sets the island identifier from the address.
    /// </summary>
    [Parameter]
    public string? Island { get; set; }

    /// <summary>
    /// Gets or sets the area index from the address.
    /// </summary>
    [Parameter]
    public string? Area { get; set; }

    /// <summary>
    /// Gets the heading of the page: the island's name once it is shown.
    /// </summary>
    private string Heading => Mode == PageMode.Ready && View is { } view && FindIsland(view) is { } island
                                  ? PageNames.Island(island.Name, Language)
                                  : Texts.RegionsEntry;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gives the address of an icon.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <returns>The address</returns>
    private static string IconAddress(string reference)
    {
        return "/icons/" + Uri.EscapeDataString(reference);
    }

    /// <summary>
    /// Gives the colour class of a delta: its sign decides, after rounding as the shown text does.
    /// </summary>
    /// <param name="delta">The delta</param>
    /// <returns>The class, or <see langword="null" /> for the text colour</returns>
    private static string? DeltaClass(float? delta)
    {
        if (delta is not { } value)
        {
            return null;
        }

        return PageFormat.Round(value) switch
               {
                   < 0 => "mud-error-text",
                   > 0 => "mud-success-text",
                   _ => null
               };
    }

    /// <summary>
    /// Finds the island of the address.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The island, or <see langword="null" /> when the view has none</returns>
    private static IslandView? FindIsland(ProfileView view)
    {
        return FindRegion(view)?.Islands.First(island => island.Key == view.Island);
    }

    /// <summary>
    /// Finds the region that holds the island of the address.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The region, or <see langword="null" /> when the view has none</returns>
    private static RegionView? FindRegion(ProfileView view)
    {
        return view.Island is { } key
                   ? view.Regions.FirstOrDefault(region => region.Islands.Any(island => island.Key == key))
                   : null;
    }

    /// <summary>
    /// Orders the rows by shown good name, ties by product GUID.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The rows</returns>
    private IEnumerable<GoodRow> OrderedRows(ProfileView view)
    {
        return view.Rows.OrderBy(row => row.Name.For(Language), StringComparer.OrdinalIgnoreCase).ThenBy(row => row.ProductGuid);
    }

    #endregion // Methods

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile, Session, Island, Area);

    /// <inheritdoc />
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        island = null;

        if (PageAddress.TryParseProfile(Profile, out profileId)
            && PageAddress.TryParseSession(Session, out var sessionGuid)
            && PageAddress.TryParseByte(Island, out var islandId)
            && PageAddress.TryParseByte(Area, out var areaIndex))
        {
            island = new IslandKey(sessionGuid, islandId, areaIndex);

            return PageMode.Loading;
        }

        return PageMode.NotFound;
    }

    /// <inheritdoc />
    protected override bool IsKnown(ProfileView view)
    {
        return FindRegion(view) is not null;
    }

    #endregion // DataPageBase
}