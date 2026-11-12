using Microsoft.AspNetCore.Http;

namespace Censio.Server;

/// <summary>
/// Registers and maps the ingest endpoint.
/// </summary>
public static class IngestEndpoint
{
    #region Constants

    /// <summary>
    /// The route of the endpoint.
    /// </summary>
    public const string Path = "/ingest";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the keep-alive interval of the WebSocket (15 seconds).
    /// </summary>
    public static TimeSpan KeepAliveInterval => TimeSpan.Zero;

    /// <summary>
    /// Gets the keep-alive timeout of the WebSocket (35 seconds).
    /// </summary>
    public static TimeSpan KeepAliveTimeout => TimeSpan.Zero;

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S1172

    /// <summary>
    /// Creates the context the WebSocket is accepted with.
    /// </summary>
    /// <returns>The context</returns>
    public static WebSocketAcceptContext CreateAcceptContext()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Registers the connection counters and, if none is registered, the system clock.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddCensioIngest(this IServiceCollection services)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Maps the ingest route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="access">The access that checks the token</param>
    /// <returns>The endpoint convention builder</returns>
    public static IEndpointConventionBuilder MapCensioIngest(this IEndpointRouteBuilder endpoints, IngestAccess access)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S1172

    #endregion // Methods
}