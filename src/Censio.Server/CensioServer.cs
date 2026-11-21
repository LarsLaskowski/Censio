using System.Net;
using System.Net.Sockets;

using Censio.Server;
using Censio.Server.Data;

using Microsoft.AspNetCore.HostFiltering;

using MudBlazor.Services;

namespace Censio.Server;

/// <summary>
/// Hosts the Censio server: the standalone process and the one the Companion starts in its own process.
/// </summary>
public static partial class CensioServer
{
    #region Methods

    /// <summary>
    /// Runs the standalone server until it stops.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code: <c>0</c> after a regular stop, <c>1</c> when a start check failed.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        using var parts = new ServerParts();

        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseStaticWebAssets();

        AddServices(builder.Services, parts);

        var app = builder.Build();

        await using var appDisposal = app.ConfigureAwait(false);

        var setUp = await TrySetUpAsync(app, parts, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        if (setUp is null)
        {
            return 1;
        }

        MapEndpoints(app, setUp.Value.Access, setUp.Value.State);

        await app.RunAsync().ConfigureAwait(false);

        return 0;
    }

    /// <summary>
    /// Starts the server for local mode on the loopback addresses of the given port.
    /// </summary>
    /// <param name="options">The options of the local server.</param>
    /// <param name="cancellationToken">The token that cancels the start.</param>
    /// <returns>The result of the start.</returns>
    public static async Task<LocalServerStart> StartLocalAsync(LocalServerOptions options, CancellationToken cancellationToken)
    {
        var parts = new ServerParts();
        var app = BuildLocalApp(options, parts);
        string token;

        try
        {
            var setUp = await TrySetUpAsync(app, parts, cancellationToken).ConfigureAwait(false);

            if (setUp is null)
            {
                return await FailAsync(app, parts, LocalServerStartKind.Failed, null).ConfigureAwait(false);
            }

            var readToken = IngestTokenSetup.ReadTokenFile(Path.Combine(options.DataDirectory, IngestTokenSetup.TokenFileName));

            if (readToken is null)
            {
                LogTokenNotReadBack(app.Logger);

                return await FailAsync(app, parts, LocalServerStartKind.Failed, null).ConfigureAwait(false);
            }

            token = readToken;

            MapEndpoints(app, setUp.Value.Access, setUp.Value.State);

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsAddressInUse(exception))
        {
            return await FailAsync(app, parts, LocalServerStartKind.PortInUse, exception).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return await FailAsync(app, parts, LocalServerStartKind.Failed, exception).ConfigureAwait(false);
        }

        return new LocalServerStart
               {
                   Kind = LocalServerStartKind.Running,
                   Server = new LocalServerHost(app, parts, options.Port, token)
               };
    }

    /// <summary>
    /// Lists the loopback endpoints a local server listens on.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="listenOnIPv6">Whether the IPv6 loopback address is listened on as well.</param>
    /// <returns>The endpoints.</returns>
    public static IReadOnlyList<IPEndPoint> LocalEndpoints(int port, bool listenOnIPv6)
    {
        var endpoints = new List<IPEndPoint>
                        {
                            new(IPAddress.Loopback, port)
                        };

        if (listenOnIPv6)
        {
            endpoints.Add(new IPEndPoint(IPAddress.IPv6Loopback, port));
        }

        return endpoints;
    }

    /// <summary>
    /// Registers the services both entry points share.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="parts">The parts the services resolve once the start checks have created them.</param>
    private static void AddServices(IServiceCollection services, ServerParts parts)
    {
        services.AddRazorComponents()
                .AddInteractiveServerComponents();
        services.AddMudServices();
        services.AddHealthChecks();
        services.AddCensioDataProtection();
        services.AddCensioIngest();
        services.AddSingleton(_ => parts.State ?? throw new InvalidOperationException("The server state is not restored yet."));
        services.AddSingleton<IProfileStateSource>(_ => parts.State ?? throw new InvalidOperationException("The server state is not restored yet."));
        services.AddSingleton(_ => parts.Retention ?? throw new InvalidOperationException("The history retention is not created yet."));
        services.AddSingleton<IHistoryTrends>(_ => parts.HistoryStore ?? throw new InvalidOperationException("The history store is not created yet."));
        services.AddSingleton<PageReader>();
        services.AddSingleton<TrendReader>();
    }

    /// <summary>
    /// Builds the host of a local server: no outside setting reaches it.
    /// </summary>
    /// <param name="options">The options of the local server.</param>
    /// <param name="parts">The parts the services resolve once the start checks have created them.</param>
    /// <returns>The built application.</returns>
    private static WebApplication BuildLocalApp(LocalServerOptions options, ServerParts parts)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                                                   {
                                                       ApplicationName = "Censio.Server",
                                                       EnvironmentName = Environments.Production,
                                                       ContentRootPath = options.ContentRoot,
                                                       WebRootPath = Path.Combine(options.ContentRoot, "wwwroot"),
                                                       Args = ["--preventHostingStartup=true", "--preferHostingUrls=false"]
                                                   });

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                                                    {
                                                        [DataProtectionSetup.DataDirectoryKey] = options.DataDirectory,
                                                        ["AllowedHosts"] = "localhost;127.0.0.1;[::1]"
                                                    });
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.ConfigureKestrel(kestrel =>
                                         {
                                             foreach (var endpoint in LocalEndpoints(options.Port, options.ListenOnIPv6))
                                             {
                                                 kestrel.Listen(endpoint);
                                             }
                                         });
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddProvider(new BorrowedLoggerProvider(options.LogProvider));
        builder.Services.Configure<HostFilteringOptions>(filtering => filtering.AllowEmptyHosts = false);
        builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(2));

        AddServices(builder.Services, parts);

        return builder.Build();
    }

    /// <summary>
    /// Runs the start checks both entry points share and creates the state and the history retention.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <param name="parts">The parts the checks create.</param>
    /// <param name="cancellationToken">The token that cancels the checks.</param>
    /// <returns>The ingest access and the state, or <c>null</c> when a check failed (its line is logged).</returns>
    private static async Task<(IngestAccess Access, ServerState State)?> TrySetUpAsync(WebApplication app, ServerParts parts, CancellationToken cancellationToken)
    {
        if (DataProtectionSetup.TryCreateKeyRing(app.Services, app.Configuration, app.Logger) == false)
        {
            return null;
        }

        var ingestAccess = IngestTokenSetup.TryCreateAccess(app.Configuration, app.Logger);

        if (ingestAccess is null)
        {
            return null;
        }

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var database = await DatabaseSetup.TryOpenAsync(app.Configuration, loggerFactory, app.Logger, cancellationToken).ConfigureAwait(false);

        if (database is null)
        {
            return null;
        }

        parts.Database = database;

        var state = new ServerState(new ServerStore(database), loggerFactory.CreateLogger("Censio.Server.State"));

        parts.State = state;

        if (await ServerStateSetup.TryRestoreAsync(state, database.DatabaseFile, app.Logger, cancellationToken).ConfigureAwait(false) == false)
        {
            return null;
        }

        var historyStore = new HistoryStore(database);
        var history = new HistoryRetention(state, historyStore, TimeProvider.System, loggerFactory.CreateLogger("Censio.Server.History"));

        parts.HistoryStore = historyStore;
        parts.Retention = history;
        app.Lifetime.ApplicationStarted.Register(() => history.Start(app.Lifetime.ApplicationStopping));

        return (ingestAccess, state);
    }

    /// <summary>
    /// Adds the middleware and the endpoints both entry points share.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="ingestAccess">The ingest access.</param>
    /// <param name="state">The server state.</param>
    private static void MapEndpoints(WebApplication app, IngestAccess ingestAccess, ServerState state)
    {
        app.UseWebSockets();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapHealthChecks("/health");
        app.MapCensioIngest(ingestAccess, state);
        app.MapCensioIcons(state);
        app.MapRazorComponents<Censio.Server.Components.App>()
           .AddInteractiveServerRenderMode();
    }

    /// <summary>
    /// Determines whether an exception of a start comes from an address another program holds.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> when the chain holds a socket exception for an address in use; otherwise, <c>false</c>.</returns>
    private static bool IsAddressInUse(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Releases what a start created, in the order retention, database, application, and builds the failed result.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <param name="parts">The parts the start created.</param>
    /// <param name="kind">The outcome.</param>
    /// <param name="exception">The exception behind the outcome, or <c>null</c>.</param>
    /// <returns>The failed result.</returns>
    private static async Task<LocalServerStart> FailAsync(WebApplication app, ServerParts parts, LocalServerStartKind kind, Exception? exception)
    {
        parts.Dispose();
        await app.DisposeAsync().ConfigureAwait(false);

        return new LocalServerStart
               {
                   Kind = kind,
                   Exception = exception
               };
    }

    /// <summary>
    /// Logs that the token file cannot be read back after the start checks.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The ingest token file could not be read back; the server stops.")]
    private static partial void LogTokenNotReadBack(ILogger logger);

    #endregion // Methods
}