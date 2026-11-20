using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The trade routes of a profile.
/// </summary>
public partial class TradeRoutes
{
    #region Properties

    /// <summary>
    /// Gets or sets the profile identity from the address, or <see langword="null"/> for the newest profile.
    /// </summary>
    [Parameter]
    public string? Profile { get; set; }

    /// <summary>
    /// Gets the heading of the page.
    /// </summary>
    private string Heading => Texts.TradeRoutesEntry;

    #endregion // Properties

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile);

    /// <inheritdoc />
    protected override bool FollowsNewestProfile => Profile is null;

    /// <inheritdoc />
    protected override bool ReadsTradeRoutes => true;

    // Skeleton: removed with the implementation
#pragma warning disable RH2003
    /// <inheritdoc />
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override bool IsKnown(ProfileView view)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // DataPageBase
}