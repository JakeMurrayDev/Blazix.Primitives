using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The tooltip trigger at L2 (TooltipTrigger.test.tsx and the trigger rows of TooltipRoot.test.tsx): its element, id and
/// attributes, the misuse error, the options its <c>interactions</c> handle is sent, and the forwarded state.
/// </summary>
public sealed class TooltipTriggerTests : IDisposable
{
    private readonly TooltipTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered without a root or a handle")]
    [Trait("Requirement", "TIP-EDGE-01")]
    [Trait("Requirement", "TIP-STR-03")]
    public void Throws_a_descriptive_error_when_rendered_without_a_root_or_a_handle()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.Context.Render<TooltipTrigger<NoPayload>>());

        Assert.Equal("Blazix: `<TooltipTrigger>` must be either used within a `<TooltipRoot>` component or provided with a handle.", error.Message);
    }

    [Fact(DisplayName = "port: renders a button of type button with its trigger id, the enabled marker and no ARIA attribute")]
    [Trait("Requirement", "TIP-DOM-01")]
    [Trait("Requirement", "TIP-DOM-04")]
    [Trait("Requirement", "TIP-ARIA-02")]
    [Trait("Requirement", "TIP-ARIA-05")]
    public void Renders_a_button_with_its_trigger_id_and_no_ARIA_attribute()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement trigger = cut.Find("#trigger");
        Assert.Equal("BUTTON", trigger.TagName);
        Assert.Equal("button", trigger.GetAttribute("type"));
        Assert.True(trigger.HasAttribute("data-base-ui-tooltip-trigger"));
        Assert.True(trigger.HasAttribute("data-popup-open"));
        Assert.DoesNotContain(trigger.Attributes, attribute => attribute.Name.StartsWith("aria-", StringComparison.Ordinal));
        Assert.False(trigger.HasAttribute("tabindex"));
        Assert.Equal("trigger", host.TriggerOptions("trigger").GetProperty("triggerId").GetString());
    }

    [Fact(DisplayName = "removes `data-popup-open` as soon as `open` becomes false")]
    [Trait("Requirement", "TIP-DOM-02")]
    [Trait("Requirement", "TIP-STA-11")]
    [Trait("Requirement", "TIP-EVT-04")]
    public async Task Removes_data_popup_open_as_soon_as_open_becomes_false()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose())));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open")));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "port: the trigger's hover is mouse-only, rest-only and nested-aware, with its delays resolved, the safe polygon and focus")]
    [Trait("Requirement", "TIP-API-15")]
    [Trait("Requirement", "TIP-API-16")]
    [Trait("Requirement", "TIP-API-10")]
    [Trait("Requirement", "TIP-API-17")]
    public void The_triggers_hover_options()
    {
        host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.Delay, 250).Add(x => x.CloseDelay, 100));

        JsonElement trigger = host.TriggerOptions("trigger");
        JsonElement hover = trigger.GetProperty("hover");
        Assert.Equal(250, hover.GetProperty("restMs").GetInt32());
        Assert.Equal(100, hover.GetProperty("delayClose").GetInt32());
        Assert.False(hover.TryGetProperty("delayOpen", out _));
        Assert.False(hover.GetProperty("move").GetBoolean());
        Assert.True(hover.GetProperty("mouseOnly").GetBoolean());
        Assert.True(hover.GetProperty("safePolygon").GetBoolean());
        Assert.True(hover.GetProperty("nestedTooltipTriggers").GetBoolean());
        Assert.False(hover.TryGetProperty("delayGroupToken", out _));
        Assert.Equal(JsonValueKind.Object, trigger.GetProperty("focus").ValueKind);
        Assert.True(trigger.GetProperty("cancelPendingOpenOnPress").GetBoolean());
        Assert.False(trigger.TryGetProperty("referencePress", out _));
    }

    [Fact(DisplayName = "port: a trigger without delays opens after 600 ms and closes at once; disableHoverablePopup drops the safe polygon")]
    [Trait("Requirement", "TIP-API-15")]
    [Trait("Requirement", "TIP-API-16")]
    [Trait("Requirement", "TIP-API-10")]
    public void A_trigger_without_delays_uses_the_fallbacks()
    {
        host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DisableHoverablePopup, true).Add(x => x.CloseOnClick, false));

        JsonElement trigger = host.TriggerOptions("trigger");
        JsonElement hover = trigger.GetProperty("hover");
        Assert.Equal(600, hover.GetProperty("restMs").GetInt32());
        Assert.Equal(0, hover.GetProperty("delayClose").GetInt32());
        Assert.False(hover.TryGetProperty("safePolygon", out _));
        Assert.False(trigger.TryGetProperty("cancelPendingOpenOnPress", out _));
    }

    [Fact(DisplayName = "port: the trigger that owns the mounted tooltip carries the reference press, gated by its own closeOnClick")]
    [Trait("Requirement", "TIP-PTR-11")]
    [Trait("Requirement", "TIP-PTR-12")]
    [Trait("Requirement", "TIP-DOM-05")]
    public async Task The_trigger_that_owns_the_mounted_tooltip_carries_the_reference_press()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>();
        Assert.False(host.TriggerOptions("trigger").TryGetProperty("referencePress", out _));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        cut.WaitForAssertion(() => Assert.True(host.TriggerOptions("trigger").GetProperty("referencePress").GetBoolean()));

        cut.Render(p => p.Add(x => x.CloseOnClick, false));

        cut.WaitForAssertion(() => Assert.False(host.TriggerOptions("trigger").TryGetProperty("referencePress", out _)));
        Assert.False(cut.Instance.Root!.Store.State.CloseOnClick);
    }

    [Fact(DisplayName = "port: the active trigger forwards its closeOnClick and its own close delay, without the provider's")]
    [Trait("Requirement", "TIP-STA-09")]
    [Trait("Requirement", "TIP-API-16")]
    public async Task The_active_trigger_forwards_its_closeOnClick_and_its_own_close_delay()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.CloseDelay, 75).Add(x => x.CloseOnClick, false));
        TooltipStore<NoPayload> store = cut.Instance.Root!.Store;
        Assert.True(store.State.CloseOnClick);

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        cut.WaitForAssertion(() => Assert.Equal(75, store.State.CloseDelay));
        Assert.False(store.State.CloseOnClick);
        cut.WaitForAssertion(() => Assert.Contains(host.InteractionsAttaches.Select(attach => (string)attach.Arguments[1]!).Concat(host.InteractionsUpdates), json => json.Contains("\"floating\":{\"node\":false,\"hover\":{\"closeDelay\":75}}", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "port: a consumer's data-base-ui-tooltip-trigger wins over the part's, merged before it")]
    [Trait("Requirement", "TIP-EDGE-16")]
    [Trait("Requirement", "TIP-DOM-17")]
    public void A_consumers_marker_wins_over_the_parts()
    {
        IRenderedComponent<TooltipRoot<NoPayload>> cut = host.Context.Render<TooltipRoot<NoPayload>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<TooltipTrigger<NoPayload>>(0);
            builder.AddAttribute(1, "id", "trigger");
            builder.AddAttribute(2, "data-base-ui-tooltip-trigger", "custom");
            builder.AddAttribute(3, "data-trigger-disabled", "custom");
            builder.CloseComponent();
        })));

        AngleSharp.Dom.IElement trigger = cut.Find("#trigger");
        Assert.Equal("custom", trigger.GetAttribute("data-base-ui-tooltip-trigger"));
        Assert.Equal("custom", trigger.GetAttribute("data-trigger-disabled"));
    }

    [Fact(DisplayName = "opens when the rendered trigger element has its own id")]
    [Trait("Requirement", "TIP-API-20")]
    [Trait("Requirement", "PSH-STA-20")]
    public async Task Opens_when_the_rendered_trigger_element_has_its_own_id()
    {
        IRenderedComponent<TooltipRoot<NoPayload>> cut = host.Context.Render<TooltipRoot<NoPayload>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<TooltipTrigger<NoPayload>>(0);
            builder.AddAttribute(1, nameof(TooltipTrigger<NoPayload>.Render), (RenderFragment)(substitute =>
            {
                substitute.OpenElement(0, "button");
                substitute.AddAttribute(1, "id", "custom");
                substitute.CloseElement();
            }));
            builder.CloseComponent();
            builder.OpenComponent<TooltipPortal>(2);
            builder.AddAttribute(3, nameof(TooltipPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<TooltipPositioner>(0);
                portal.AddAttribute(1, nameof(TooltipPositioner.ChildContent), (RenderFragment)(positioner =>
                {
                    positioner.OpenComponent<TooltipPopup>(0);
                    positioner.AddAttribute(1, "data-testid", "popup");
                    positioner.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            builder.CloseComponent();
        })));
        AngleSharp.Dom.IElement trigger = cut.Find("button");
        Assert.Equal("custom", trigger.Id);
        JSRuntimeInvocation attach = host.InteractionsAttaches.Single(invocation => ((string)invocation.Arguments[1]!).Contains("\"trigger\"", StringComparison.Ordinal));
        string registryId = JsonDocument.Parse((string)attach.Arguments[1]!).RootElement.GetProperty("trigger").GetProperty("triggerId").GetString()!;
        Assert.StartsWith("base-ui-", registryId, StringComparison.Ordinal);
        Assert.IsType<ElementReference>(attach.Arguments[0]);

        await host.RequestAsync(host.TriggerTwin(registryId), open: true, "trigger-hover", registryId);

        cut.WaitForAssertion(() => Assert.True(cut.Find("#custom").HasAttribute("data-popup-open")));
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "port: a trigger under a substitute callback attaches by the id it renders")]
    [Trait("Requirement", "TIP-API-20")]
    public void A_trigger_under_a_substitute_callback_attaches_by_its_id()
    {
        host.Context.Render<TooltipRoot<NoPayload>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<TooltipTrigger<NoPayload>>(0);
            builder.AddAttribute(1, "id", "callback-trigger");
            builder.AddAttribute(2, nameof(TooltipTrigger<NoPayload>.RenderFunction), (RenderFragment<Blazix.Primitives.Rendering.RenderProps<TooltipTriggerState>>)(props => substitute =>
            {
                substitute.OpenElement(0, "span");
                substitute.AddMultipleAttributes(1, props.Attributes);
                substitute.CloseElement();
            }));
            builder.CloseComponent();
        })));

        JSRuntimeInvocation attach = host.InteractionsAttaches.Single(invocation => ((string)invocation.Arguments[1]!).Contains("\"trigger\"", StringComparison.Ordinal));
        Assert.Equal("callback-trigger", attach.Arguments[0]);
    }

    [Fact(DisplayName = "port: a provider's token, delay and close delay reach its triggers, and a later close delay is sent")]
    [Trait("Requirement", "TIP-API-01")]
    [Trait("Requirement", "TIP-API-02")]
    [Trait("Requirement", "TIP-API-03")]
    [Trait("Requirement", "TIP-STR-01")]
    [Trait("Requirement", "TIP-STA-14")]
    public void A_providers_delays_reach_its_triggers()
    {
        IRenderedComponent<TooltipProviderHost> cut = host.Context.Render<TooltipProviderHost>(p => p.Add(x => x.Delay, 200).Add(x => x.CloseDelay, 50).Add(x => x.TriggerDelay, 300));

        JSRuntimeInvocation provider = host.InteractionsAttaches.Single(invocation => ((string)invocation.Arguments[1]!).Contains("\"provider\"", StringComparison.Ordinal));
        JsonElement group = JsonDocument.Parse((string)provider.Arguments[1]!).RootElement.GetProperty("provider");
        Assert.Null(provider.Arguments[0]);
        string token = group.GetProperty("token").GetString()!;
        Assert.Equal(50, group.GetProperty("delayClose").GetInt32());
        Assert.Equal(400, group.GetProperty("timeout").GetInt32());
        Assert.False(group.TryGetProperty("delayOpen", out _));
        Assert.DoesNotContain(token, cut.Markup, StringComparison.Ordinal);
        Assert.Equal(300, host.TriggerOptions("trigger-1").GetProperty("hover").GetProperty("restMs").GetInt32());
        Assert.Equal(200, host.TriggerOptions("trigger-2").GetProperty("hover").GetProperty("restMs").GetInt32());
        Assert.Equal(token, host.TriggerOptions("trigger-2").GetProperty("hover").GetProperty("delayGroupToken").GetString());
        Assert.Equal(50, host.TriggerOptions("trigger-2").GetProperty("hover").GetProperty("delayClose").GetInt32());

        cut.Render(p => p.Add(x => x.Delay, 200).Add(x => x.CloseDelay, 80).Add(x => x.TriggerDelay, 300).Add(x => x.Timeout, 30000));

        cut.WaitForAssertion(() => Assert.Equal(80, host.TriggerOptions("trigger-2").GetProperty("hover").GetProperty("delayClose").GetInt32()));
        Assert.Contains(host.InteractionsUpdates, json => json.Contains("\"timeout\":30000", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the group's close of the previous member runs that tooltip's sequence with reason none and renders delay for its exit")]
    [Trait("Requirement", "TIP-DIS-04")]
    [Trait("Requirement", "TIP-PTR-04")]
    [Trait("Requirement", "TIP-ANI-03")]
    public async Task The_groups_close_of_the_previous_member()
    {
        IRenderedComponent<TooltipProviderHost> cut = host.Context.Render<TooltipProviderHost>(p => p.Add(x => x.Delay, 0));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup-1\"]")));

        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-hover", "trigger-2", instant: true);
        await host.RequestAsync(host.SessionTwin(cut.Instance.First!.Store.SessionToken), open: false, "none", instant: true, eventJson: null);

        Assert.Equal("delay", cut.Find("[data-testid=\"popup-2\"]").GetAttribute("data-instant"));
        AngleSharp.Dom.IElement first = cut.Find("[data-testid=\"popup-1\"]");
        Assert.True(first.HasAttribute("data-closed"));
        Assert.Equal("delay", first.GetAttribute("data-instant"));
    }
}
