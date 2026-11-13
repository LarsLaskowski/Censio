using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The region overview of a profile.
/// </summary>
public partial class Regions
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
    private string Heading => Texts.RegionsEntry;

    #endregion // Properties

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile);

    /// <inheritdoc />
    protected override bool FollowsNewestProfile => Profile is null;

    /// <inheritdoc />
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        island = null;

        if (Profile is not null)
        {
            return PageAddress.TryParseProfile(Profile, out profileId)
                       ? PageMode.Loading
                       : PageMode.NotFound;
        }

        if (State.NewestProfile is { } newest)
        {
            profileId = newest;

            return PageMode.Loading;
        }

        profileId = Guid.Empty;

        return PageMode.NoData;
    }

    /// <inheritdoc />
    protected override bool IsKnown(ProfileView view)
    {
        return true;
    }

    #endregion // DataPageBase
}