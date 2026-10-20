using Blazix.Primitives.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

// ADR 02 consequences
/// <summary>Provides the extension method that registers the services the library's parts need.</summary>
/// <remarks>
/// <see cref="AddBlazixPrimitives"/> is the one line a consumer adds to service registration to use the library's parts.
/// </remarks>
public static class BlazixPrimitivesServiceCollectionExtensions
{
    // ADR 02 decision 5
    /// <summary>Registers the services every part needs.</summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>The only service is the id generator, <see cref="PartIdGenerator"/>, registered as a scoped service.</remarks>
    public static IServiceCollection AddBlazixPrimitives(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PartIdGenerator>();
        return services;
    }
}
