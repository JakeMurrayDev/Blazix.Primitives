using Blazix.Primitives.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Rendering;

/// <summary>
/// What <c>AddBlazixPrimitives()</c> registers (ADR 05 Amendments 2026-11-18 item 3, chosen by the user 2026-11-18: the
/// toast manager is the library's first registered stateful service, scoped, never a singleton). The count pins the
/// extension's documentation; it read "five" while six were registered (<c>PlatformFlags</c>, added 2026-11-04, was not
/// counted) and nothing checked it.
/// </summary>
public sealed class ServiceRegistrationTests
{
    [Fact(DisplayName = "port: AddBlazixPrimitives registers seven scoped services, the toast manager among them")]
    [Trait("Requirement", "TST-API-03")]
    public void AddBlazixPrimitives_registers_seven_scoped_services_the_toast_manager_among_them()
    {
        ServiceCollection services = new();

        services.AddBlazixPrimitives();

        Assert.Equal(7, services.Count);
        Assert.All(services, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
        Assert.Equal(
            [
                typeof(PartIdGenerator),
                typeof(PlatformFlags),
                typeof(ButtonDevelopmentLog),
                typeof(CollapsibleDevelopmentLog),
                typeof(ToggleDevelopmentLog),
                typeof(DefaultFormScope),
                typeof(ToastManager),
            ],
            services.Select(descriptor => descriptor.ServiceType));
    }

    [Fact(DisplayName = "port: AddBlazixPrimitives registers one toast manager per scope")]
    [Trait("Requirement", "TST-API-03")]
    public void AddBlazixPrimitives_registers_one_toast_manager_per_scope()
    {
        ServiceCollection services = new();
        services.AddBlazixPrimitives();
        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scopeA = provider.CreateScope();
        using IServiceScope scopeB = provider.CreateScope();
        ToastManager a = scopeA.ServiceProvider.GetRequiredService<ToastManager>();

        Assert.Same(a, scopeA.ServiceProvider.GetRequiredService<ToastManager>());
        Assert.NotSame(a, scopeB.ServiceProvider.GetRequiredService<ToastManager>());
    }
}
