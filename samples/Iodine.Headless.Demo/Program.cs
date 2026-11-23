using Iodine.Headless.Demo.Components;

var builder = WebApplication.CreateBuilder(args);

// The services a consumer's Program.cs registers for the library's parts.
builder.Services.AddIodineHeadless();

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
    .AddAdditionalAssemblies(typeof(Iodine.Headless.Demo.Client._Imports).Assembly);

app.Run();
