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
        if (_isOverviewFailed || view.Island is not { } island || view.AsOf is not { } asOf)
        {
            return;
        }

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

    /// <inheritdoc />
    protected override void ClearDetails()
    {
        _generation++;
        _overviewRead = null;
        _isOverviewFailed = false;
    }

    #endregion // DataPageBase
}