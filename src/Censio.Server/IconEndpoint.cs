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

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Maps the icon route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="state">The server state</param>
    /// <returns>The endpoint convention builder of the route</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static IEndpointConventionBuilder MapCensioIcons(this IEndpointRouteBuilder endpoints, ServerState state)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}