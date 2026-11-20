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

    #region Methods

    /// <summary>
    /// Shows a count, in the error colour when it is above 0.
    /// </summary>
    /// <param name="value">The count</param>
    /// <returns>The markup of the count</returns>
    private static RenderFragment Count(int value)
    {
        return builder =>
               {
                   if (value > 0)
                   {
                       builder.OpenElement(0, "span");
                       builder.AddAttribute(1, "class", "mud-error-text");
                       builder.AddContent(2, PageFormat.Count(value));
                       builder.CloseElement();
                   }
                   else
                   {
                       builder.AddContent(3, PageFormat.Count(value));
                   }
               };
    }

    #endregion // Methods

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile);

    /// <inheritdoc />
    protected override bool FollowsNewestProfile => Profile is null;

    /// <inheritdoc />
    protected override bool ReadsTradeRoutes => true;

    /// <inheritdoc />
    protected override bool HasData(ProfileView view)
    {
        return true;
    }

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