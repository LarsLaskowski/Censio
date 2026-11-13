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

    #endregion // Properties

    #region ComponentBase

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // ComponentBase
}