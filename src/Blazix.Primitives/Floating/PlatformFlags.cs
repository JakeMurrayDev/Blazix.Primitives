using System.Text.Json;
using Blazix.Primitives.Interop;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-11-04 (design point 4, settled by the orchestrator 2026-11-04; MNU-A11Y-01), M5 track 2: the
// platform hints C# renders with, read once per circuit or runtime from the `interactions` module's readPlatformFlags.
// Until then they read null, which a part reads as neither VoiceOver nor macOS (prerender and the first render; ADR 01's
// row of the same date). A Root calls EnsureAsync from its first interactive OnAfterRenderAsync; a lost circuit leaves
// the defaults.
/// <summary>Holds the platform hints the browser reports, read once per circuit or runtime.</summary>
internal sealed class PlatformFlags
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/interactions.js";

    private Task? reading;

    /// <summary>Gets a value indicating whether VoiceOver may be running.</summary>
    /// <value>The hint once read; <see langword="null"/> before, which reads as no VoiceOver.</value>
    public bool? VoiceOver { get; private set; }

    /// <summary>Gets a value indicating whether the platform is macOS.</summary>
    /// <value>The flag once read; <see langword="null"/> before, which reads as not macOS.</value>
    public bool? Mac { get; private set; }

    /// <summary>Reads the flags from the browser the first time it is called; later calls return the same read.</summary>
    /// <param name="jsRuntime">The runtime the module is imported through.</param>
    /// <returns>A task that completes when the flags are read.</returns>
    public Task EnsureAsync(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        return reading ??= ReadAsync(jsRuntime);
    }

    private async Task ReadAsync(IJSRuntime jsRuntime)
    {
        try
        {
            IJSObjectReference module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await using (module.ConfigureAwait(false))
            {
                string json = await module.InvokeAsync<string>("readPlatformFlags");
                PlatformFlagsPayload? flags = JsonSerializer.Deserialize(json, BlazixJsonContext.Default.PlatformFlagsPayload);
                VoiceOver = flags?.VoiceOver ?? false;
                Mac = flags?.Mac ?? false;
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone: the defaults stand.
        }
    }
}
