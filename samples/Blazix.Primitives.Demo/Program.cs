using Blazix.Primitives.Demo.Components;

var builder = WebApplication.CreateBuilder(args);

// The services a consumer's Program.cs registers for the library's parts.
builder.Services.AddBlazixPrimitives();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Blazix.Primitives.Demo.Client._Imports).Assembly);

app.Run();
