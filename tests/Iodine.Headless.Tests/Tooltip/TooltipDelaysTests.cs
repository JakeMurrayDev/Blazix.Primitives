// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The tooltip's millisecond fallbacks at L1 (TIP-API-01..03, TIP-API-15/16; tooltip/utils/constants.ts:1,
/// TooltipTrigger.tsx:78, 113-119, 152-157, TooltipProvider.tsx:13): resolved in C# before they cross (issue 121; ADR 08
/// Amendments 2026-10-27, `null` meaning not supplied).
/// </summary>
public sealed class TooltipDelaysTests
{
    private static TooltipProviderContext Provider(int? delay, int? closeDelay)
    {
        TooltipProviderContext provider = new("base-ui-9");
        provider.SetDelays(delay, closeDelay);
        return provider;
    }

    [Theory(DisplayName = "port: the open delay is the trigger's, else the provider's, else 600 ms")]
    [InlineData(null, false, null, 600)]
    [InlineData(null, true, null, 600)]
    [InlineData(null, true, 0, 0)]
    [InlineData(null, true, 200, 200)]
    [InlineData(100, true, 0, 100)]
    [InlineData(0, true, 200, 0)]
    [InlineData(100, false, null, 100)]
    [Trait("Requirement", "TIP-API-01")]
    [Trait("Requirement", "TIP-API-15")]
    public void The_open_delay_falls_back_from_the_trigger_to_the_provider_to_600(int? trigger, bool hasProvider, int? provider, int expected)
    {
        Duration? delay = trigger is { } value ? value : null;

        Assert.Equal(expected, TooltipDelays.Open(delay, hasProvider ? Provider(provider, null) : null));
    }

    [Theory(DisplayName = "port: the close delay is the trigger's, else the provider's, else 0; the forwarded one has no provider fallback")]
    [InlineData(null, false, null, 0, 0)]
    [InlineData(null, true, 300, 300, 0)]
    [InlineData(50, true, 300, 50, 50)]
    [InlineData(0, true, 300, 0, 0)]
    [Trait("Requirement", "TIP-API-02")]
    [Trait("Requirement", "TIP-API-16")]
    public void The_close_delay_falls_back_from_the_trigger_to_the_provider_to_0(int? trigger, bool hasProvider, int? provider, int expected, int forwarded)
    {
        Duration? closeDelay = trigger is { } value ? value : null;

        Assert.Equal(expected, TooltipDelays.Close(closeDelay, hasProvider ? Provider(null, provider) : null));
        Assert.Equal(forwarded, TooltipDelays.Forwarded(closeDelay));
    }

    [Fact(DisplayName = "port: the provider's timeout defaults to 400 ms and a time span is read in milliseconds")]
    [Trait("Requirement", "TIP-API-03")]
    public void The_providers_timeout_defaults_to_400()
    {
        Assert.Equal(400, TooltipDelays.Timeout(null));
        Assert.Equal(30000, TooltipDelays.Timeout(30000));
        Assert.Equal(1500, TooltipDelays.Timeout(TimeSpan.FromSeconds(1.5)));
    }

    [Fact(DisplayName = "port: the provider tells its triggers when a delay changed, and only then")]
    [Trait("Requirement", "TIP-API-02")]
    public void The_provider_tells_its_triggers_when_a_delay_changed()
    {
        TooltipProviderContext provider = new("base-ui-9");
        int notified = 0;
        provider.AddListener(() => notified++);

        provider.SetDelays(null, 100);
        provider.SetDelays(null, 100);
        provider.SetDelays(null, 200);

        Assert.Equal(2, notified);
        Assert.Equal(200, provider.CloseDelay);
    }
}
