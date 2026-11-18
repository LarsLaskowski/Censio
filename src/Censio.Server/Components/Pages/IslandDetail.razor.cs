using System.Globalization;

using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The goods of an island.
/// </summary>
public partial class IslandDetail
{
    #region Fields

    /// <summary>
    /// The read shown in place of the overview's own once its read failed.
    /// </summary>
    private static readonly TrendRead _failedRead = new()
                                                    {
                                                        View = null,
                                                        IsFailed = true
                                                    };

    /// <summary>
    /// The read of the overview chart, or <see langword="null"/> while there is none.
    /// </summary>
    private TrendRead? _overviewRead;

    /// <summary>
    /// The number of the overview's read.
    /// </summary>
    private int _overviewNumber;

    /// <summary>
    /// The count of the addresses the page showed, which drops the read of an address that is gone.
    /// </summary>
    private int _generation;

    /// <summary>
    /// A value that indicates whether the overview read failed.
    /// </summary>
    private bool _isOverviewFailed;

    /// <summary>
    /// The loader of the trend column, replaced when the profile or the island changes.
    /// </summary>
    private SparklineLoader<int> _sparklines = new();

    /// <summary>
    /// The profile, session, island and area the loader belongs to, or <see langword="null" /> before the first address.
    /// </summary>
    private (string? Profile, string? Session, string? Island, string? Area)? _sparklineAddress;

    #endregion // Fields

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
    /// Gets or sets the chart reader.
    /// </summary>
    [Inject]
    private TrendReader Trends { get; set; } = null!;

    /// <summary>
    /// Gets the heading of the page: the island's name once it is shown.
    /// </summary>
    private string Heading => Mode == PageMode.Ready && View is { } view && FindIsland(view) is { } island
                                  ? PageNames.Island(island.Name, Language)
                                  : Texts.RegionsEntry;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Finds the island of the address.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The island, or <see langword="null" /> when the view has none</returns>
    internal static IslandView? FindIsland(ProfileView view)
    {
        return FindRegion(view)?.Islands.First(island => island.Key == view.Island);
    }

    /// <summary>
    /// Finds the region that holds the island of the address.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The region, or <see langword="null" /> when the view has none</returns>
    internal static RegionView? FindRegion(ProfileView view)
    {
        return view.Island is { } key
                   ? view.Regions.FirstOrDefault(region => region.Islands.Any(island => island.Key == key))
                   : null;
    }

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
    /// Gives what a row's trend cell shows.
    /// </summary>
    /// <param name="row">The row</param>
    /// <param name="held">The read of the trends, or <see langword="null" /> before there is one</param>
    /// <param name="isFailed">A value that indicates whether the read of the trends failed</param>
    /// <param name="firstRows">The first row of each good</param>
    /// <returns>Whether the cell is read, and its line</returns>
    private static (bool IsRead, SparklineView? Line) TrendOf(GoodRow row, SparklineRead<int>? held, bool isFailed, HashSet<GoodRow> firstRows)
    {
        if (isFailed)
        {
            return (true, null);
        }

        if (held is null)
        {
            return (false, null);
        }

        if (firstRows.Contains(row) == false)
        {
            return (true, null);
        }

        return held.Lines.TryGetValue(row.ProductGuid, out var line)
                   ? (true, line)
                   : (false, null);
    }

    /// <summary>
    /// Finds the first row of each good in the order of the view.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The rows</returns>
    private static HashSet<GoodRow> FirstRows(ProfileView view)
    {
        return new HashSet<GoodRow>(view.Rows.GroupBy(row => row.ProductGuid).Select(group => group.First()), ReferenceEqualityComparer.Instance);
    }

    /// <summary>
    /// Gives the key under which the loader holds the trends of a view.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The key</returns>
    private static (Guid ProfileId, IslandKey? Island) SparklineKey(ProfileView view)
    {
        return ValueTuple.Create(view.ProfileId, view.Island);
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
    protected override object AddressKey => ValueTuple.Create(Profile, Session, Island, Area, ChartChoice.Range);

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

    /// <inheritdoc />
    protected override async Task ReadDetailsAsync(ProfileView view, CancellationToken cancellationToken)
    {
        if (view.Island is not { } island || view.AsOf is not { } asOf)
        {
            return;
        }

        if (_isOverviewFailed == false)
        {
            var generation = _generation;
            var read = await Trends.ReadFiguresAsync(view.ProfileId,
                                                     island,
                                                     new TrendRequest
                                                     {
                                                         LatestTick = asOf,
                                                         Choice = ChartChoice,
                                                         Held = _overviewRead?.View
                                                     },
                                                     cancellationToken).ConfigureAwait(true);

            if (generation != _generation)
            {
                return;
            }

            if (read.IsFailed)
            {
                _isOverviewFailed = true;
            }
            else
            {
                _overviewRead = read;
                _overviewNumber++;
            }

            StateHasChanged();
        }

        var products = view.Rows.Select(row => row.ProductGuid).Distinct().ToList();

        if (products.Count == 0)
        {
            return;
        }

        _sparklines.Request(SparklineKey(view),
                            asOf,
                            (held, token) => Trends.ReadGoodSparklinesAsync(view.ProfileId, island, products, asOf, held, token),
                            () => InvokeAsync(StateHasChanged));
    }

    /// <inheritdoc />
    protected override void ClearDetails()
    {
        _generation++;
        _overviewRead = null;
        _isOverviewFailed = false;

        var address = (Profile, Session, Island, Area);

        if (address.Equals(_sparklineAddress) == false)
        {
            if (_sparklineAddress is not null)
            {
                _sparklines.Dispose();
                _sparklines = new SparklineLoader<int>();
            }

            _sparklineAddress = address;
        }
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