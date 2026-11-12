using Censio.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();
builder.Services.AddCensioDataProtection();

var app = builder.Build();

await using var appDisposal = app.ConfigureAwait(false);

if (DataProtectionSetup.TryCreateKeyRing(app.Services, app.Configuration, app.Logger) == false)
{
    return 1;
}

if (IngestTokenSetup.TryCreateAccess(app.Configuration, app.Logger) is null)
{
    return 1;
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorComponents<Censio.Server.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);

return 0;