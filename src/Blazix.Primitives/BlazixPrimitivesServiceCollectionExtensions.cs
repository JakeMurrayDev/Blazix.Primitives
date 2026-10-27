using Blazix.Primitives;
using Blazix.Primitives.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

// ADR 02 consequences
/// <summary>Provides the extension method that registers the services the library's parts need.</summary>
public static class BlazixPrimitivesServiceCollectionExtensions
{
    // ADR 02 decision 5; UBT-EDGE-03 and COL-EDGE-02/03 (the once-per-session sets of the button and collapsible development
    // messages); FFI-STR-05 (the registry of the fields outside a form, settled by the orchestrator 2026-10-23); TGL-EDGE-01
    // (the toggle's missing-value message, chosen by the user 2026-10-26)
    /// <summary>Registers the services every part needs.</summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Registers five scoped services: the id generator, <see cref="PartIdGenerator"/>; the sets of the button, collapsible
    /// and toggle development messages already written in the session; and the form scope of the fields outside a form.
    /// </remarks>
    public static IServiceCollection AddBlazixPrimitives(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PartIdGenerator>();
        services.AddScoped<ButtonDevelopmentLog>();
        services.AddScoped<CollapsibleDevelopmentLog>();
        services.AddScoped<ToggleDevelopmentLog>();
        services.AddScoped<DefaultFormScope>();
        return services;
    }
}
