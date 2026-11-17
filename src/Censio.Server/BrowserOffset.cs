using System.Globalization;
using System.Text.Json;

using Microsoft.JSInterop;

namespace Censio.Server;

/// <summary>
/// Asks the browser for its UTC offset.
/// </summary>
public static class BrowserOffset
{
    #region Constants

    /// <summary>
    /// The greatest UTC offset in minutes the browser may give, in either direction.
    /// </summary>
    private const int MaxOffsetMinutes = 840;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Asks the browser for its UTC offset at an instant. The answer is untrusted.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime</param>
    /// <param name="instant">The instant</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The offset in minutes, or <see langword="null"/> when there is no usable answer</returns>
    public static async Task<int?> AskAsync(IJSRuntime jsRuntime, DateTimeOffset instant, CancellationToken cancellationToken)
    {
        try
        {
            var offset = await jsRuntime.InvokeAsync<int>("censio.utcOffsetMinutes", cancellationToken, instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)).ConfigureAwait(false);

            return offset is >= -MaxOffsetMinutes and <= MaxOffsetMinutes
                       ? offset
                       : null;
        }
        catch (Exception exception) when (exception is JSException or JsonException or JSDisconnectedException or OperationCanceledException)
        {
            return null;
        }
    }

    #endregion // Methods
}