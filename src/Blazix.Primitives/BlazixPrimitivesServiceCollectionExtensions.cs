using Blazix.Primitives;
using Blazix.Primitives.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

// ADR 02 consequences
/// <summary>Provides the extension method that registers the services the library's parts need.</summary>
public static class BlazixPrimitivesServiceCollectionExtensions
{
    // ADR 02 decision 5; UBT-EDGE-03 (the once-per-session set of the button development messages)
    /// <summary>Registers the services every part needs.</summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Registers two scoped services: the id generator, <see cref="PartIdGenerator"/>, and the set of development
    /// messages already written in the session.
    /// </remarks>
    public static IServiceCollection AddBlazixPrimitives(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PartIdGenerator>();
        services.AddScoped<ButtonDevelopmentLog>();
        return services;
    }
}
