using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The charts of one good of an island.
/// </summary>
public partial class GoodTrend
{
    #region Fields

    /// <summary>
    /// The read shown in place of a chart's own once a chart read failed.
    /// </summary>
    private static readonly TrendRead _failedRead = new()
                                                    {
                                                        View = null,
                                                        IsFailed = true
                                                    };

    /// <summary>
    /// The read of the island's chart, or <see langword="null"/> while there is none.
    /// </summary>
    private TrendRead? _islandRead;

    /// <summary>
    /// The read of the region's chart, or <see langword="null"/> while there is none.
    /// </summary>
    private TrendRead? _regionRead;

    /// <summary>
    /// The number of the island's read.
    /// </summary>
    private int _islandNumber;

    /// <summary>
    /// The number of the region's read.
    /// </summary>
    private int _regionNumber;

    /// <summary>
    /// The count of the reads the page took, which numbers them.
    /// </summary>
    private int _readCount;

    /// <summary>
    /// The count of the addresses the page showed, which drops the reads of an address that is gone.
    /// </summary>
    private int _generation;

    /// <summary>
    /// A value that indicates whether a chart read failed.
    /// </summary>
    private bool _isFailed;

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
    /// Gets or sets the product GUID of the good from the address.
    /// </summary>
    [Parameter]
    public string? Good { get; set; }

    /// <summary>
    /// Gets or sets the chart reader.
    /// </summary>
    [Inject]
    private TrendReader Trends { get; set; } = null!;

    /// <summary>
    /// Gets the heading of the page: the good's name once it is shown.
    /// </summary>
    private string Heading => Mode == PageMode.Ready && PageAddress.TryParseGood(Good, out var productGuid)
                                  ? PageNames.Good(productGuid, Reader.Catalog).For(Language)
                                  : Texts.RegionsEntry;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Takes a chart read, unless its address is gone, and shows it.
    /// </summary>
    /// <param name="read">The read</param>
    /// <param name="generation">The address the read was made for</param>
    /// <param name="take">Stores a read that did not fail</param>
    /// <returns><see langword="true"/> when the page may read on</returns>
    private bool Accept(TrendRead read, int generation, Action<TrendRead> take)
    {
        if (generation != _generation)
        {
            return false;
        }

        if (read.IsFailed)
        {
            _isFailed = true;
        }
        else
        {
            take(read);
        }

        StateHasChanged();

        return _isFailed == false;
    }

    #endregion // Methods

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile, Session, Island, Area, Good);

    /// <inheritdoc />
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        island = null;

        if (PageAddress.TryParseProfile(Profile, out profileId)
            && PageAddress.TryParseSession(Session, out var sessionGuid)
            && PageAddress.TryParseByte(Island, out var islandId)
            && PageAddress.TryParseByte(Area, out var areaIndex)
            && PageAddress.TryParseGood(Good, out _))
        {
            island = new IslandKey(sessionGuid, islandId, areaIndex);

            return PageMode.Loading;
        }

        return PageMode.NotFound;
    }

    /// <inheritdoc />
    protected override bool IsKnown(ProfileView view)
    {
        return IslandDetail.FindRegion(view) is not null;
    }

    /// <inheritdoc />
    protected override async Task ReadDetailsAsync(ProfileView view, CancellationToken cancellationToken)
    {
        if (_isFailed
            || view.Island is not { } island
            || view.AsOf is not { } asOf
            || IslandDetail.FindRegion(view) is not { } region
            || PageAddress.TryParseGood(Good, out var productGuid) == false)
        {
            return;
        }

        var generation = _generation;
        var islandRead = await Trends.ReadGoodAsync(view.ProfileId, [island], productGuid, new TrendRequest { LatestTick = asOf, Choice = ChartChoice.Default, Held = null }, cancellationToken).ConfigureAwait(true);

        if (Accept(islandRead, generation, read => (_islandRead, _islandNumber) = (read, ++_readCount)) == false)
        {
            return;
        }

        var regionRead = await Trends.ReadGoodAsync(view.ProfileId, [.. region.Islands.Select(regionIsland => regionIsland.Key)], productGuid, new TrendRequest { LatestTick = asOf, Choice = ChartChoice.Default, Held = null }, cancellationToken).ConfigureAwait(true);

        Accept(regionRead, generation, read => (_regionRead, _regionNumber) = (read, ++_readCount));
    }

    /// <inheritdoc />
    protected override void ClearDetails()
    {
        _generation++;
        _islandRead = null;
        _regionRead = null;
        _isFailed = false;
    }

    #endregion // DataPageBase
}