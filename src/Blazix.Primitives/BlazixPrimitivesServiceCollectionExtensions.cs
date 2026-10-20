using Blazix.Primitives.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>The one setup line of ADR 02 (consequences).</summary>
public static class BlazixPrimitivesServiceCollectionExtensions
{
    /// <summary>Registers the services every part needs: the scoped id generator (ADR 02 decision 5).</summary>
    public static IServiceCollection AddBlazixPrimitives(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PartIdGenerator>();
        return services;
    }
}
