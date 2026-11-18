using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The island list of a region.
/// </summary>
public partial class RegionIslands
{
    #region Fields

    /// <summary>
    /// The loader of the trend column, replaced when the address changes.
    /// </summary>
    private SparklineLoader<IslandKey> _sparklines = new();

    /// <summary>
    /// A value that indicates whether the loader belongs to an address already.
    /// </summary>
    private bool _isSparklinesUsed;

    #endregion // Fields

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
    /// Gets or sets the chart reader.
    /// </summary>
    [Inject]
    private TrendReader Trends { get; set; } = null!;

    /// <summary>
    /// Gets the heading of the page: the region's name once it is shown.
    /// </summary>
    private string Heading => Mode == PageMode.Ready && View is { } view && FindRegion(view) is { } region
                                  ? PageNames.RegionText(region, view.Regions, Language)
                                  : Texts.RegionsEntry;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gives what an island's trend cell shows.
    /// </summary>
    /// <param name="key">The key of the island</param>
    /// <param name="held">The read of the trends, or <see langword="null" /> before there is one</param>
    /// <param name="isFailed">A value that indicates whether the read of the trends failed</param>
    /// <returns>Whether the cell is read, and its line</returns>
    private static (bool IsRead, SparklineView? Line) TrendOf(IslandKey key, SparklineRead<IslandKey>? held, bool isFailed)
    {
        if (isFailed)
        {
            return (true, null);
        }

        return held is not null && held.Lines.TryGetValue(key, out var line)
                   ? (true, line)
                   : (false, null);
    }

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

    /// <inheritdoc />
    protected override Task ReadDetailsAsync(ProfileView view, CancellationToken cancellationToken)
    {
        if (view.AsOf is not { } asOf || FindRegion(view) is not { } region)
        {
            return Task.CompletedTask;
        }

        var islands = region.Islands.Select(island => island.Key).ToList();

        if (islands.Count > 0)
        {
            _sparklines.Request(AddressKey,
                                asOf,
                                (held, token) => Trends.ReadIslandSparklinesAsync(view.ProfileId, islands, asOf, held, token),
                                () => InvokeAsync(StateHasChanged));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void ClearDetails()
    {
        if (_isSparklinesUsed)
        {
            _sparklines.Dispose();
            _sparklines = new SparklineLoader<IslandKey>();
        }

        _isSparklinesUsed = true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sparklines.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion // DataPageBase
}