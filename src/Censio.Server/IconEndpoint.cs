namespace Censio.Server;

/// <summary>
/// The route that serves the stored icons.
/// </summary>
public static class IconEndpoint
{
    #region Constants

    /// <summary>
    /// The route pattern of an icon.
    /// </summary>
    public const string IconPath = "/icons/{reference}";

    /// <summary>
    /// The length of an icon reference.
    /// </summary>
    private const int ReferenceLength = 64;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Maps the icon route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="state">The server state</param>
    /// <returns>The endpoint convention builder of the route</returns>
    public static IEndpointConventionBuilder MapCensioIcons(this IEndpointRouteBuilder endpoints, ServerState state)
    {
        return endpoints.MapGet(IconPath, (string reference, HttpContext context) => ServeAsync(reference, context, state));
    }

    /// <summary>
    /// Answers a request for an icon.
    /// </summary>
    /// <param name="reference">The icon reference of the address</param>
    /// <param name="context">The HTTP context</param>
    /// <param name="state">The server state</param>
    /// <returns>The result</returns>
    private static async Task<IResult> ServeAsync(string reference, HttpContext context, ServerState state)
    {
        if (IsReference(reference) == false)
        {
            return Results.NotFound();
        }

        var cancellationToken = context.RequestAborted;
        var png = await state.ReadIconAsync(reference, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (png is null || PngHeader.IsPng(png) == false)
        {
            return Results.NotFound();
        }

        var headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "public, max-age=31536000, immutable";
        headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

        return Results.Bytes(png, "image/png");
    }

    /// <summary>
    /// Tells whether a text is an icon reference: 64 lowercase hexadecimal digits.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><see langword="true"/> when the text is a reference</returns>
    private static bool IsReference(string text)
    {
        return text.Length == ReferenceLength && text.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    #endregion // Methods
}