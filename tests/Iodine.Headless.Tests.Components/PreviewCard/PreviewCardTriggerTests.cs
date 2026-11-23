using System.Text.Json;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The preview card trigger at L2 (PreviewCardTrigger.test.tsx and the trigger rows of PreviewCardRoot.test.tsx): its
/// element and attributes, the options its <c>interactions</c> handle is sent, and the forwarded close delay.
/// </summary>
public sealed class PreviewCardTriggerTests : IDisposable
{
    private readonly PreviewCardTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "port: renders an anchor with the consumer's href, its trigger id, and no type, role, tabindex or ARIA attribute of its own")]
    [Trait("Requirement", "PRV-API-15")]
    [Trait("Requirement", "PRV-DOM-03")]
    [Trait("Requirement", "PRV-ARIA-02")]
    [Trait("Requirement", "PRV-API-14")]
    public void Renders_an_anchor_with_no_attribute_of_its_own()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement trigger = cut.Find("#trigger");
        Assert.Equal("A", trigger.TagName);
        Assert.Equal("#", trigger.GetAttribute("href"));
        Assert.True(trigger.HasAttribute("data-popup-open"));
        Assert.False(trigger.HasAttribute("type"));
        Assert.False(trigger.HasAttribute("role"));
        Assert.False(trigger.HasAttribute("tabindex"));
        Assert.DoesNotContain(trigger.Attributes, attribute => attribute.Name.StartsWith("aria-", StringComparison.Ordinal));
        // bUnit's renderer adds the element reference's capture attribute, which a browser never sees.
        Assert.Equal(
            ["data-popup-open", "href", "id"],
            trigger.Attributes.Select(attribute => attribute.Name).Where(name => !name.StartsWith("blazor:", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Equal("trigger", host.TriggerOptions("trigger").GetProperty("triggerId").GetString());
    }

    [Fact(DisplayName = "port: the trigger's hover is mouse-only and move-free with the safe polygon, focus opens after the same delay, and the hovered line is recorded")]
    [Trait("Requirement", "PRV-PTR-01")]
    [Trait("Requirement", "PRV-PTR-04")]
    [Trait("Requirement", "PRV-PTR-15")]
    [Trait("Requirement", "PRV-KEY-01")]
    [Trait("Requirement", "PRV-PTR-12")]
    [Trait("Requirement", "PRV-PTR-11")]
    public void The_triggers_hover_and_focus_options()
    {
        host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.Delay, 250).Add(x => x.CloseDelay, 100));

        JsonElement trigger = host.TriggerOptions("trigger");
        JsonElement hover = trigger.GetProperty("hover");
        Assert.Equal(250, hover.GetProperty("delayOpen").GetInt32());
        Assert.Equal(100, hover.GetProperty("delayClose").GetInt32());
        Assert.False(hover.TryGetProperty("restMs", out _));
        Assert.False(hover.GetProperty("move").GetBoolean());
        Assert.True(hover.GetProperty("mouseOnly").GetBoolean());
        Assert.True(hover.GetProperty("safePolygon").GetBoolean());
        Assert.Equal(250, trigger.GetProperty("focus").GetProperty("delay").GetInt32());
        Assert.True(trigger.GetProperty("hoveredLine").GetBoolean());

        // No press handling (PRV-PTR-11): neither the reference press nor the pending-open cancel is sent.
        Assert.False(trigger.TryGetProperty("referencePress", out _));
        Assert.False(trigger.TryGetProperty("cancelPendingOpenOnPress", out _));
        Assert.False(trigger.TryGetProperty("clientPoint", out _));
    }

    [Fact(DisplayName = "port: a trigger without delays opens after 600 ms and closes after 300 ms")]
    [Trait("Requirement", "PRV-API-10")]
    [Trait("Requirement", "PRV-API-11")]
    public void A_trigger_without_delays_uses_the_fallbacks()
    {
        host.Context.Render<PreviewCardBasicHost>();

        JsonElement trigger = host.TriggerOptions("trigger");
        Assert.Equal(600, trigger.GetProperty("hover").GetProperty("delayOpen").GetInt32());
        Assert.Equal(300, trigger.GetProperty("hover").GetProperty("delayClose").GetInt32());
        Assert.Equal(600, trigger.GetProperty("focus").GetProperty("delay").GetInt32());
    }

    [Fact(DisplayName = "port: the active trigger forwards its close delay to the store and the positioner's hover leave, and keeps it after the close")]
    [Trait("Requirement", "PRV-STA-11")]
    [Trait("Requirement", "PRV-API-11")]
    [Trait("Requirement", "PRV-PTR-10")]
    public async Task The_active_trigger_forwards_its_close_delay()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.CloseDelay, 75));
        PreviewCardStore<NoPayload> store = cut.Instance.Root!.Store;
        Assert.Equal(300, store.State.CloseDelay);

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        cut.WaitForAssertion(() => Assert.Equal(75, store.State.CloseDelay));
        cut.WaitForAssertion(() => Assert.Contains(
            host.InteractionsAttaches.Select(attach => (string)attach.Arguments[1]!).Concat(host.InteractionsUpdates),
            json => json.Contains("\"floating\":{\"hover\":{\"closeDelay\":75}}", StringComparison.Ordinal)));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        // PRV §16 Q6, replicated: the forwarded value is never reset.
        Assert.Equal(75, store.State.CloseDelay);
    }

    [Fact(DisplayName = "removes `data-popup-open` as soon as `open` becomes false")]
    [Trait("Requirement", "PRV-DOM-02")]
    [Trait("Requirement", "PRV-STA-04")]
    public async Task Removes_data_popup_open_as_soon_as_open_becomes_false()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open")));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
    }
}
