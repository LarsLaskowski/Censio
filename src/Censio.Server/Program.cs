var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorComponents<Censio.Server.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync();