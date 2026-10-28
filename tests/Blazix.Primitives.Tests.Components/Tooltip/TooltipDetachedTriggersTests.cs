using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Detached triggers and the handle at L2 (TooltipRoot.detached-triggers.test.tsx; the detached-triggers suite's titles the
/// store observes): registration and migration, the payload of the active trigger, the hand-off, the imperative calls and
/// their errors, the trigger-unmount close and the render counts of bystander triggers.
/// </summary>
public sealed class TooltipDetachedTriggersTests : IDisposable
{
    private readonly TooltipTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "opens by trigger id with its payload and closes")]
    [Trait("Requirement", "TIP-API-23")]
    [Trait("Requirement", "TIP-DIS-07")]
    [Trait("Requirement", "TIP-API-12")]
    public async Task Opens_by_trigger_id_with_its_payload_and_closes()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-2"));

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"payload\"]").TextContent));
        Assert.True(handle.IsOpen);
        Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));
        Assert.Equal((true, EventReason.ImperativeAction), cut.Instance.Calls[^1]);

        await cut.InvokeAsync(() => handle.CloseAsync());

        Assert.False(handle.IsOpen);
        Assert.Equal((false, EventReason.ImperativeAction), cut.Instance.Calls[^1]);
    }

    [Fact(DisplayName = "throws when opened with an unregistered trigger id")]
    [Trait("Requirement", "TIP-EDGE-05")]
    public void Throws_when_opened_with_an_unregistered_trigger_id()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => cut.InvokeAsync(() => handle.OpenAsync("missing")).GetAwaiter().GetResult());

        Assert.Equal(
            "Blazix: `TooltipHandle.OpenAsync()` was called with the trigger id \"missing\", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted `TooltipTrigger` that has this handle set on its `Handle` parameter.",
            error.Message);
        Assert.False(handle.IsOpen);
    }

    [Fact(DisplayName = "ignores imperative calls while no root is attached and follows the root across remounts")]
    [Trait("Requirement", "TIP-API-23")]
    [Trait("Requirement", "TIP-EDGE-06")]
    [Trait("Requirement", "TIP-EDGE-14")]
    public async Task Ignores_imperative_calls_while_no_root_is_attached_and_follows_the_root_across_remounts()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, false));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        Assert.False(handle.IsOpen);

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, true));
        Assert.False(handle.IsOpen);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, false));
        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, true));

        Assert.False(handle.IsOpen);
        Assert.Equal("0", cut.Find("[data-testid=\"payload\"]").TextContent);
    }

    [Fact(DisplayName = "port: warns when two roots stay attached to one handle at the next render-batch boundary")]
    [Trait("Requirement", "TIP-EDGE-07")]
    public void Warns_when_two_roots_stay_attached_to_one_handle()
    {
        CapturingLoggerProvider logs = new();
        host.Context.Services.AddLogging(builder => builder.AddProvider(logs));
        TooltipHandle<int> handle = new();

        host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));
        host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger1, false).Add(x => x.ShowTrigger2, false));

        Assert.Contains(logs.Messages, message => message.StartsWith("Blazix: A handle is attached to more than one mounted root at the same time.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "sets the payload of the active trigger and renders content based on it")]
    [Trait("Requirement", "TIP-API-14")]
    [Trait("Requirement", "TIP-API-19")]
    [Trait("Requirement", "TIP-STR-09")]
    public async Task Sets_the_payload_of_the_active_trigger_and_renders_content_based_on_it()
    {
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>();

        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.Equal("Payload 1", cut.Find("[data-testid=\"popup\"]").TextContent));

        // The hand-off: the second trigger re-targets the open tooltip, then the first takes it back (PSH-STA-31).
        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-hover", "trigger-2");
        cut.WaitForAssertion(() => Assert.Equal("Payload 2", cut.Find("[data-testid=\"popup\"]").TextContent));
        Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));

        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-focus", "trigger-1", eventJson: TooltipTestContext.FocusJson);
        cut.WaitForAssertion(() => Assert.Equal("Payload 1", cut.Find("[data-testid=\"popup\"]").TextContent));
        Assert.Equal([(true, EventReason.TriggerHover), (true, EventReason.TriggerHover), (true, EventReason.TriggerFocus)], cut.Instance.Calls);
    }

    [Fact(DisplayName = "opens initially with the payload of a non-first `defaultTriggerId`")]
    [Trait("Requirement", "TIP-API-13")]
    [Trait("Requirement", "TIP-API-04")]
    public void Opens_initially_with_the_payload_of_a_non_first_defaultTriggerId()
    {
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.DefaultTriggerId, "trigger-2"));

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"payload\"]").TextContent));
        Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));
        Assert.Empty(cut.Instance.Calls);
    }

    [Fact(DisplayName = "should close when the active detached trigger unmounts")]
    [Trait("Requirement", "TIP-DIS-05")]
    [Trait("Requirement", "PSH-STA-23")]
    public async Task Should_close_when_the_active_detached_trigger_unmounts()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger1, false));

        cut.WaitForAssertion(() => Assert.False(handle.IsOpen));
        Assert.Equal((false, EventReason.None), cut.Instance.Calls[^1]);
    }

    [Fact(DisplayName = "should remain open when the active trigger unmount close is canceled")]
    [Trait("Requirement", "TIP-DIS-05")]
    [Trait("Requirement", "TIP-EVT-03")]
    [Trait("Requirement", "TIP-EVT-01")]
    public async Task Should_remain_open_when_the_active_trigger_unmount_close_is_canceled()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger1, false).Add(x => x.Cancel, true));

        cut.WaitForAssertion(() => Assert.Equal((false, EventReason.None), cut.Instance.Calls[^1]));
        Assert.True(handle.IsOpen);
        Assert.False(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "port: a disabled detached trigger renders data-trigger-disabled, sends no hover or focus, and loses the nested-trigger marker")]
    [Trait("Requirement", "TIP-API-18")]
    [Trait("Requirement", "TIP-DOM-03")]
    [Trait("Requirement", "TIP-DOM-04")]
    public void A_disabled_detached_trigger()
    {
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Trigger1Disabled, true));

        AngleSharp.Dom.IElement trigger = cut.Find("#trigger-1");
        Assert.True(trigger.HasAttribute("data-trigger-disabled"));
        Assert.False(trigger.HasAttribute("data-base-ui-tooltip-trigger"));
        Assert.False(host.TriggerOptions("trigger-1").TryGetProperty("hover", out _));
        Assert.False(host.TriggerOptions("trigger-1").TryGetProperty("focus", out _));
        Assert.True(cut.Find("#trigger-2").HasAttribute("data-base-ui-tooltip-trigger"));
    }

    [Fact(DisplayName = "port: a detached trigger declared before the root registers with it, and keeps working when the root remounts")]
    [Trait("Requirement", "TIP-EDGE-15")]
    [Trait("Requirement", "TIP-EDGE-12")]
    public async Task A_detached_trigger_declared_before_the_root_registers_with_it()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-1").HasAttribute("data-popup-open")));
        await cut.InvokeAsync(() => handle.CloseAsync());

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, false));
        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, true));
        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));

        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-1").HasAttribute("data-popup-open")));
    }

    [Fact(DisplayName = "port: a default-open root keeps its trigger-less open state until a trigger with its default trigger id registers")]
    [Trait("Requirement", "TIP-EDGE-12")]
    [Trait("Requirement", "TIP-EDGE-13")]
    [Trait("Requirement", "TIP-EVT-06")]
    public void A_default_open_root_stays_open_until_its_trigger_registers()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p
            .Add(x => x.Handle, handle).Add(x => x.DefaultOpen, true).Add(x => x.DefaultTriggerId, "trigger-2").Add(x => x.ShowTrigger2, false));

        Assert.True(handle.IsOpen);
        Assert.Empty(cut.Instance.Calls);

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.DefaultOpen, true).Add(x => x.DefaultTriggerId, "trigger-2").Add(x => x.ShowTrigger2, true));

        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open")));
        Assert.True(handle.IsOpen);
        Assert.Empty(cut.Instance.Calls);
    }

    [Theory(DisplayName = "port: does not re-render inactive triggers when $name")]
    [InlineData("opened and closed imperatively")]
    [InlineData("the popup moves to another trigger")]
    [InlineData("focus opens and closes the popup")]
    [InlineData("hover opens and closes the popup")]
    [Trait("Requirement", "TIP-EDGE-11")]
    public async Task Does_not_re_render_inactive_triggers(string name)
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle).Add(x => x.Bystanders, 3));
        Dictionary<int, int> before = new(cut.Instance.ClassCalls);

        switch (name)
        {
            case "opened and closed imperatively":
                await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
                await cut.InvokeAsync(() => handle.CloseAsync());
                break;
            case "the popup moves to another trigger":
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
                await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-hover", "trigger-2");
                break;
            case "focus opens and closes the popup":
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-focus", "trigger-1", eventJson: TooltipTestContext.FocusJson);
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: false, "trigger-focus", eventJson: TooltipTestContext.FocusJson);
                break;
            default:
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: false, "trigger-hover");
                break;
        }

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Instance.Calls));

        // The figure measured on this branch: 0 renders of each of the three bystander triggers (ClassValue invocations).
        Assert.Equal(before, cut.Instance.ClassCalls);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => provider.Messages.Add(formatter(state, exception));
        }
    }
}
