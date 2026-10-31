using Censio.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();
builder.Services.AddCensioDataProtection();

await using var app = builder.Build();

if (DataProtectionSetup.TryCreateKeyRing(app.Services, app.Configuration, app.Logger))
{
    app.UseAntiforgery();
    app.MapStaticAssets();
    app.MapHealthChecks("/health");
    app.MapRazorComponents<Censio.Server.Components.App>()
       .AddInteractiveServerRenderMode();

    await app.RunAsync();

    return 0;
}

return 1;