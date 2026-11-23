using System.Text.Json;
using Blazix.Primitives.Fixtures.Client.Harness;
using Blazix.Primitives.Fixtures.Components;

var builder = WebApplication.CreateBuilder(args);

// The harness (ADR 06 §2, ADR 09 §1): one session per circuit with its fake TimeProvider and captured diagnostics.
builder.Services.AddFixtureHarness(builder.Logging);

// The services a consumer's Program.cs registers for the library's parts (the first fixture that renders a part needs them).
builder.Services.AddBlazixPrimitives();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Short keep-alive so ADR-06/2 (issue 14) can watch a circuit outlive the SignalR timeout within one test; the
    // host's JS initializer mirrors the two values on the client.
    .AddHubOptions(options =>
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(2);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(10);
    });
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();

// Test-only endpoints (ADR 09 §1). Readiness is what Playwright's webServer polls; diagnostics are per session token.
app.MapGet("/_fixture/ready", () => Results.Text("ready"));
app.MapGet("/_fixture/diagnostics/{token}", (string token, FixtureSessionRegistry registry) =>
    registry.TryGet(token, out FixtureSession? session)
        ? Results.Text(session.DiagnosticsJson(), "application/json")
        : Results.NotFound());

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Blazix.Primitives.Fixtures.Client._Imports).Assembly);

app.Run();
