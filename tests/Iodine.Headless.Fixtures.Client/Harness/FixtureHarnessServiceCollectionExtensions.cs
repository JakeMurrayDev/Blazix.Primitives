using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Fixtures.Client.Harness;

public static class FixtureHarnessServiceCollectionExtensions
{
    /// <summary>
    /// Registers the harness in either host: the session registry and the <see cref="HarnessTimeProvider"/> as
    /// <see cref="TimeProvider"/> (singletons), the session and the activation counter (scoped), and the capturing
    /// logger provider.
    /// </summary>
    public static IServiceCollection AddFixtureHarness(this IServiceCollection services, ILoggingBuilder logging)
    {
        FixtureSessionRegistry registry = new();
        services.AddSingleton(registry);
        services.AddScoped<FixtureSession>();
        services.AddSingleton<TimeProvider>(new HarnessTimeProvider(registry));
        services.AddScoped<ActivationCounter>();
        logging.AddProvider(new CapturingLoggerProvider(registry));
        return services;
    }
}
