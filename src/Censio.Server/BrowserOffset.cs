using Microsoft.JSInterop;

namespace Censio.Server;

/// <summary>
/// Asks the browser for its UTC offset.
/// </summary>
public static class BrowserOffset
{
    #region Methods

    /// <summary>
    /// Asks the browser for its UTC offset at an instant. The answer is untrusted.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime</param>
    /// <param name="instant">The instant</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The offset in minutes, or <see langword="null"/> when there is no usable answer</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static Task<int?> AskAsync(IJSRuntime jsRuntime, DateTimeOffset instant, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Methods
}