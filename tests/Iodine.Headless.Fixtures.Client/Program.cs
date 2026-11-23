using Blazix.Primitives.Fixtures.Client.Harness;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The same harness services as the host (ADR 06 §2): one session per app instance here, one per circuit there.
builder.Services.AddFixtureHarness(builder.Logging);

// The services a consumer's Program.cs registers for the library's parts (the first fixture that renders a part needs them).
builder.Services.AddBlazixPrimitives();

await builder.Build().RunAsync();
