using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The charts of one good of an island.
/// </summary>
public partial class GoodTrend
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
    /// Gets or sets the product GUID of the good from the address.
    /// </summary>
    [Parameter]
    public string? Good { get; set; }

    #endregion // Properties

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile, Session, Island, Area, Good);

    /// <inheritdoc />
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    /// <inheritdoc />
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    protected override bool IsKnown(ProfileView view)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // DataPageBase
}