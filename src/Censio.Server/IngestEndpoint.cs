using Censio.Contracts;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Net.Http.Headers;

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

    /// <summary>
    /// The category of the ingest log lines.
    /// </summary>
    private const string LogCategory = "Censio.Server.Ingest";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the keep-alive interval of the WebSocket (15 seconds).
    /// </summary>
    public static TimeSpan KeepAliveInterval => TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets the keep-alive timeout of the WebSocket (35 seconds).
    /// </summary>
    public static TimeSpan KeepAliveTimeout => TimeSpan.FromSeconds(35);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the context the WebSocket is accepted with.
    /// </summary>
    /// <returns>The context</returns>
    public static WebSocketAcceptContext CreateAcceptContext()
    {
        return new WebSocketAcceptContext
               {
                   KeepAliveInterval = KeepAliveInterval,
                   KeepAliveTimeout = KeepAliveTimeout
               };
    }

    /// <summary>
    /// Registers the connection counters and, if none is registered, the system clock.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddCensioIngest(this IServiceCollection services)
    {
        services.TryAddSingleton<IngestConnections>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Maps the ingest route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="access">The access that checks the token</param>
    /// <param name="state">The server state the messages are stored through</param>
    /// <returns>The endpoint convention builder</returns>
    public static IEndpointConventionBuilder MapCensioIngest(this IEndpointRouteBuilder endpoints, IngestAccess access, ServerState state)
    {
        var serverVersion = ProductVersion.Read(typeof(IngestEndpoint).Assembly);

        return endpoints.Map(Path, context => HandleAsync(context, access, state, serverVersion));
    }

    /// <summary>
    /// Answers a request to the ingest route.
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="access">The access that checks the token</param>
    /// <param name="state">The server state the messages are stored through</param>
    /// <param name="serverVersion">The version of the server</param>
    /// <returns>A task that completes when the request ends</returns>
    private static async Task HandleAsync(HttpContext context, IngestAccess access, ServerState state, string serverVersion)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            return;
        }

        if (context.Request.Headers.ContainsKey(HeaderNames.Origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return;
        }

        var services = context.RequestServices;
        var connections = services.GetRequiredService<IngestConnections>();

        if (connections.TryEnterPendingHello() == false)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            return;
        }

        var sessionStarted = false;

        try
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync(CreateAcceptContext()).ConfigureAwait(false);
            using var session = new IngestSession(webSocket, access, connections, state, services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory), serverVersion);

            // From its first line the session owns the pending place.
            sessionStarted = true;

            await session.RunAsync(services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping).ConfigureAwait(false);
        }
        finally
        {
            if (sessionStarted == false)
            {
                connections.LeavePendingHello();
            }
        }
    }

    #endregion // Methods
}