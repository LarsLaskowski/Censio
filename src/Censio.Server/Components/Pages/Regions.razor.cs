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