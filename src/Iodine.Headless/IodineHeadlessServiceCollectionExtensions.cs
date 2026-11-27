using Iodine.Headless;
using Iodine.Headless.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

// ADR 02 consequences
/// <summary>Provides the extension method that registers the services the library's parts need.</summary>
public static class IodineHeadlessServiceCollectionExtensions
{
    // ADR 02 decision 5; UBT-EDGE-03 and COL-EDGE-02/03 (the once-per-session sets of the button and collapsible development
    // messages); FFI-STR-05 (the registry of the fields outside a form, settled by the orchestrator 2026-10-23); TGL-EDGE-01
    // (the toggle's missing-value message, chosen by the user 2026-10-26); ADR 05 Amendments 2026-11-18 item 3 (the toast
    // manager, scoped, never a singleton, which would span circuits on the server; chosen by the user 2026-11-18). The
    // remark said "five" while six were registered (PlatformFlags, 2026-11-04, was not counted); it states the count that
    // ServiceRegistrationTests checks. The second paragraph of the remarks: design point 10-411a (a), chosen by the user
    // 2026-11-27, its consumer-facing home chosen by the user the same day (issue 411; ADR 01 Amendments 2026-11-27).
    /// <summary>Registers the services every part needs.</summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Registers eight scoped services: the id generator, <see cref="PartIdGenerator"/>; the platform hints; the sets of the
    /// button, collapsible and toggle development messages already written in the session; the form scope of the fields
    /// outside a form; the <see cref="ToastManager"/> that toast providers without a manager of their own use; and the set
    /// of the one-time code fields' development messages already written in the session.
    /// <para>
    /// On Blazor Server, an input can make more render batches than the circuit sends before the browser acknowledges them
    /// (<c>CircuitOptions.MaxBufferedUnacknowledgedRenderBatches</c>, 10 by default). A press on a slider makes ten, so a
    /// render of your own that the press causes, such as a <c>@onpointerdown</c> handler's, waits a further round trip. To
    /// have it arrive in the first round trip, raise the limit where the host adds the Server render mode, for example
    /// <c>AddInteractiveServerComponents(options =&gt; options.MaxBufferedUnacknowledgedRenderBatches = 32)</c>. This
    /// library cannot set it. A higher limit lets more unacknowledged batches queue for a slow client, which holds more
    /// memory on the server for each circuit.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddIodineHeadless(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PartIdGenerator>();

        // ADR 04 Amendments 2026-11-04 (design point 4, settled by the orchestrator 2026-11-04): the platform hints, read
        // once per circuit or runtime.
        services.AddScoped<PlatformFlags>();
        services.AddScoped<ButtonDevelopmentLog>();
        services.AddScoped<CollapsibleDevelopmentLog>();
        services.AddScoped<ToggleDevelopmentLog>();
        services.AddScoped<DefaultFormScope>();
        services.AddScoped<ToastManager>();

        // M6 track 7 (issue 278; granted by the orchestrator 2026-11-19): the one-time code fields' warnings, once per session.
        services.AddScoped<OtpFieldDevelopmentLog>();
        return services;
    }
}
