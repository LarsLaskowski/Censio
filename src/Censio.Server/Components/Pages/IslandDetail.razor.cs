using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The goods of an island.
/// </summary>
public partial class IslandDetail
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