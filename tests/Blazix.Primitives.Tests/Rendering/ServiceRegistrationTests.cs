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
    // M6 track 7 (issue 278; granted by the orchestrator 2026-11-19): the one-time code field's warnings log joins them.
    [Fact(DisplayName = "port: AddBlazixPrimitives registers eight scoped services, the toast manager and the one-time code field's log among them")]
    [Trait("Requirement", "TST-API-03")]
    [Trait("Requirement", "OTP-EDGE-02")]
    public void AddBlazixPrimitives_registers_eight_scoped_services()
    {
        ServiceCollection services = new();

        services.AddBlazixPrimitives();

        Assert.Equal(8, services.Count);
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
                typeof(OtpFieldDevelopmentLog),
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
