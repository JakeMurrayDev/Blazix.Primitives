using Blazix.Primitives.Fixtures.Client.Harness;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The same harness services as the host (ADR 06 §2): one session per app instance here, one per circuit there.
builder.Services.AddFixtureHarness(builder.Logging);

await builder.Build().RunAsync();
