using Censio.Server;
using Censio.Server.Data;

using MudBlazor.Services;

ServerState? state = null;
HistoryRetention? retention = null;
HistoryStore? historyStore = null;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddHealthChecks();
builder.Services.AddCensioDataProtection();
builder.Services.AddCensioIngest();
builder.Services.AddSingleton(_ => state ?? throw new InvalidOperationException("The server state is not restored yet."));
builder.Services.AddSingleton<IProfileStateSource>(_ => state ?? throw new InvalidOperationException("The server state is not restored yet."));
builder.Services.AddSingleton(_ => retention ?? throw new InvalidOperationException("The history retention is not created yet."));
builder.Services.AddSingleton<IHistoryTrends>(_ => historyStore ?? throw new InvalidOperationException("The history store is not created yet."));
builder.Services.AddSingleton<PageReader>();
builder.Services.AddSingleton<TrendReader>();

var app = builder.Build();

await using var appDisposal = app.ConfigureAwait(false);

if (DataProtectionSetup.TryCreateKeyRing(app.Services, app.Configuration, app.Logger) == false)
{
    return 1;
}

var ingestAccess = IngestTokenSetup.TryCreateAccess(app.Configuration, app.Logger);

if (ingestAccess is null)
{
    return 1;
}

using var database = await DatabaseSetup.TryOpenAsync(app.Configuration, app.Services.GetRequiredService<ILoggerFactory>(), app.Logger, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

if (database is null)
{
    return 1;
}

state = new ServerState(new ServerStore(database), app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Censio.Server.State"));

if (await ServerStateSetup.TryRestoreAsync(state, database.DatabaseFile, app.Logger, app.Lifetime.ApplicationStopping).ConfigureAwait(false) == false)
{
    return 1;
}

historyStore = new HistoryStore(database);

using var history = new HistoryRetention(state, historyStore, TimeProvider.System, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Censio.Server.History"));

retention = history;
app.Lifetime.ApplicationStarted.Register(() => history.Start(app.Lifetime.ApplicationStopping));

app.UseWebSockets();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapCensioIngest(ingestAccess, state);
app.MapCensioIcons(state);
app.MapRazorComponents<Censio.Server.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);

return 0;