using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components;

/// <summary>
/// The Companion banner and the as-of line of a data page.
/// </summary>
public partial class DataStatus
{
    #region Properties

    /// <summary>
    /// Gets or sets the receive time of the shown data, or <see langword="null"/> for none.
    /// </summary>
    [Parameter]
    public DateTimeOffset? AsOf { get; set; }

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