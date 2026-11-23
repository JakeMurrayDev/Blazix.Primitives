using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The services a consumer's Program.cs registers for the library's parts.
builder.Services.AddIodineHeadless();

await builder.Build().RunAsync();
