using Censio.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();
builder.Services.AddCensioDataProtection();
builder.Services.AddCensioIngest();

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

app.UseWebSockets();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapCensioIngest(ingestAccess);
app.MapRazorComponents<Censio.Server.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);

return 0;