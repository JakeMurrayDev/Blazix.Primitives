using Blazix.Primitives.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

// ADR 02 consequences
/// <summary>The one line a consumer adds to service registration to use the library's parts.</summary>
public static class BlazixPrimitivesServiceCollectionExtensions
{
    // ADR 02 decision 5
    /// <summary>Registers the services every part needs: the scoped id generator.</summary>
    public static IServiceCollection AddBlazixPrimitives(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PartIdGenerator>();
        return services;
    }
}
