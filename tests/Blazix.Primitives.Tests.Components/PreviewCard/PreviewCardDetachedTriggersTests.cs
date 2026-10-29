using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Detached triggers and the handle at L2 (PreviewCardRoot.detached-triggers.test.tsx; the detached-triggers suite's titles
/// the store observes): registration and migration, the payload of the active trigger, the immediate switch, the imperative
/// calls and their errors, the trigger-unmount close and the render counts of bystander triggers.
/// </summary>
public sealed class PreviewCardDetachedTriggersTests : IDisposable
{
    private readonly PreviewCardTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "opens by trigger id with its payload and closes")]
    [Trait("Requirement", "PRV-API-21")]
    [Trait("Requirement", "PRV-DIS-11")]
    [Trait("Requirement", "PRV-EVT-11")]
    public async Task Opens_by_trigger_id_with_its_payload_and_closes()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));

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
    [Trait("Requirement", "PRV-API-22")]
    [Trait("Requirement", "PRV-EDGE-05")]
    public void Throws_when_opened_with_an_unregistered_trigger_id()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => cut.InvokeAsync(() => handle.OpenAsync("missing")).GetAwaiter().GetResult());

        Assert.Equal(
            "Blazix: `PreviewCardHandle.OpenAsync()` was called with the trigger id \"missing\", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted `PreviewCardTrigger` that has this handle set on its `Handle` parameter.",
            error.Message);
        Assert.False(handle.IsOpen);
    }

    [Fact(DisplayName = "ignores imperative calls while no root is attached and follows the root across remounts")]
    [Trait("Requirement", "PRV-API-06")]
    [Trait("Requirement", "PRV-API-23")]
    [Trait("Requirement", "PRV-EDGE-06")]
    public async Task Ignores_imperative_calls_while_no_root_is_attached_and_follows_the_root_across_remounts()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, false));

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
    [Trait("Requirement", "PRV-EDGE-07")]
    public void Warns_when_two_roots_stay_attached_to_one_handle()
    {
        CapturingLoggerProvider logs = new();
        host.Context.Services.AddLogging(builder => builder.AddProvider(logs));
        PreviewCardHandle<int> handle = new();

        host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));
        host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger1, false).Add(x => x.ShowTrigger2, false));

        Assert.Contains(logs.Messages, message => message.StartsWith("Blazix: A handle is attached to more than one mounted root at the same time.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "sets the payload of the active trigger and renders content based on it")]
    [Trait("Requirement", "PRV-API-07")]
    [Trait("Requirement", "PRV-PTR-08")]
    [Trait("Requirement", "PRV-STR-09")]
    public async Task Sets_the_payload_of_the_active_trigger_and_renders_content_based_on_it()
    {
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>();

        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.Equal("Payload 1", cut.Find("[data-testid=\"popup\"]").TextContent));

        // The hand-off: the second trigger re-targets the open card, then the first takes it back (PSH-STA-31).
        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-hover", "trigger-2");
        cut.WaitForAssertion(() => Assert.Equal("Payload 2", cut.Find("[data-testid=\"popup\"]").TextContent));
        Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));

        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-focus", "trigger-1", eventJson: PreviewCardTestContext.FocusJson);
        cut.WaitForAssertion(() => Assert.Equal("Payload 1", cut.Find("[data-testid=\"popup\"]").TextContent));
        Assert.Equal([(true, EventReason.TriggerHover), (true, EventReason.TriggerHover), (true, EventReason.TriggerFocus)], cut.Instance.Calls);
    }

    [Fact(DisplayName = "opens initially with the payload of a non-first `defaultTriggerId`")]
    [Trait("Requirement", "PRV-API-09")]
    public void Opens_initially_with_the_payload_of_a_non_first_defaultTriggerId()
    {
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.DefaultTriggerId, "trigger-2"));

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"payload\"]").TextContent));
        Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));
        Assert.Empty(cut.Instance.Calls);
    }

    [Fact(DisplayName = "should close when the active detached trigger unmounts")]
    [Trait("Requirement", "PRV-STA-14")]
    [Trait("Requirement", "PRV-DIS-07")]
    [Trait("Requirement", "PSH-STA-23")]
    public async Task Should_close_when_the_active_detached_trigger_unmounts()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger1, false));

        cut.WaitForAssertion(() => Assert.False(handle.IsOpen));
        Assert.Equal((false, EventReason.None), cut.Instance.Calls[^1]);
    }

    [Fact(DisplayName = "should remain open when the active trigger unmount close is canceled")]
    [Trait("Requirement", "PRV-DIS-07")]
    [Trait("Requirement", "PRV-DIS-10")]
    [Trait("Requirement", "PRV-EVT-02")]
    [Trait("Requirement", "PRV-EVT-06")]
    public async Task Should_remain_open_when_the_active_trigger_unmount_close_is_canceled()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger1, false).Add(x => x.Cancel, true));

        cut.WaitForAssertion(() => Assert.Equal((false, EventReason.None), cut.Instance.Calls[^1]));
        Assert.True(handle.IsOpen);
        Assert.False(cut.Find("#trigger-2").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "port: a detached trigger declared before the root registers with it, and keeps working when the root remounts")]
    [Trait("Requirement", "PRV-API-12")]
    public async Task A_detached_trigger_declared_before_the_root_registers_with_it()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-1").HasAttribute("data-popup-open")));
        await cut.InvokeAsync(() => handle.CloseAsync());

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, false));
        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowRoot, true));
        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));

        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-1").HasAttribute("data-popup-open")));
    }

    [Fact(DisplayName = "keeps a default-open root open while a detached trigger migrates after the initial commit")]
    [Trait("Requirement", "PRV-STA-18")]
    [Trait("Requirement", "PRV-EVT-08")]
    public void Keeps_a_default_open_root_open_while_a_detached_trigger_migrates_after_the_initial_commit()
    {
        // Base UI renders the trigger after the root, so it registers after the root's first commit; here the second trigger
        // is the one the root names, rendered after the root, and mounts in a later render.
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p
            .Add(x => x.Handle, handle).Add(x => x.DefaultOpen, true).Add(x => x.DefaultTriggerId, "trigger-2").Add(x => x.ShowTrigger2, false));

        Assert.True(handle.IsOpen);

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.DefaultOpen, true).Add(x => x.DefaultTriggerId, "trigger-2").Add(x => x.ShowTrigger2, true));

        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open")));
        Assert.True(handle.IsOpen);
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-open"));
        Assert.Empty(cut.Instance.Calls);
    }

    [Fact(DisplayName = "should open the preview card immediately when hovering another trigger")]
    [Trait("Requirement", "PRV-API-13")]
    [Trait("Requirement", "PRV-PTR-08")]
    public async Task Should_open_the_preview_card_immediately_when_hovering_another_trigger()
    {
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>();
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.Equal("Payload 1", cut.Find("[data-testid=\"popup\"]").TextContent));

        // The module skips the open delay while the card is open (PRV-PTR-08, L3); the switch is one request.
        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-hover", "trigger-2");

        cut.WaitForAssertion(() => Assert.Equal("Payload 2", cut.Find("[data-testid=\"popup\"]").TextContent));
        Assert.Equal([(true, EventReason.TriggerHover), (true, EventReason.TriggerHover)], cut.Instance.Calls);
    }

    [Fact(DisplayName = "should switch immediately when focusing another trigger while open")]
    [Trait("Requirement", "PRV-KEY-02")]
    public async Task Should_switch_immediately_when_focusing_another_trigger_while_open()
    {
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>();
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-focus", "trigger-1", eventJson: PreviewCardTestContext.FocusJson);
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-1").HasAttribute("data-popup-open")));

        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-focus", "trigger-2", eventJson: PreviewCardTestContext.FocusJson);

        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open")));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));
        Assert.Equal("Payload 2", cut.Find("[data-testid=\"popup\"]").TextContent);
    }

    [Fact(DisplayName = "should open again after escape when focusing another trigger")]
    [Trait("Requirement", "PRV-KEY-01")]
    [Trait("Requirement", "PRV-KEY-04")]
    [Trait("Requirement", "PRV-DIS-01")]
    public async Task Should_open_again_after_escape_when_focusing_another_trigger()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-focus", "trigger-1", eventJson: PreviewCardTestContext.FocusJson);
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        await host.RequestAsync(host.SessionTwin(), open: false, "escape-key", eventJson: null);
        Assert.False(handle.IsOpen);

        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-focus", "trigger-2", eventJson: PreviewCardTestContext.FocusJson);

        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger-2").HasAttribute("data-popup-open")));
        Assert.True(handle.IsOpen);
    }

    [Fact(DisplayName = "should close when the active trigger unmounts")]
    [Trait("Requirement", "PRV-STA-14")]
    [Trait("Requirement", "PRV-DIS-07")]
    public async Task Should_close_when_the_active_trigger_unmounts()
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-2"), open: true, "trigger-hover", "trigger-2");
        cut.WaitForAssertion(() => Assert.True(handle.IsOpen));

        cut.Render(p => p.Add(x => x.Handle, handle).Add(x => x.ShowTrigger2, false));

        cut.WaitForAssertion(() => Assert.False(handle.IsOpen));
        Assert.Equal((false, EventReason.None), cut.Instance.Calls[^1]);
    }

    [Theory(DisplayName = "port: does not re-render inactive triggers when $name")]
    [InlineData("opened and closed imperatively")]
    [InlineData("the popup moves to another trigger")]
    [InlineData("focus opens and closes the popup")]
    [InlineData("hover opens and closes the popup")]
    [Trait("Requirement", "PRV-API-14")]
    [Trait("Requirement", "PRV-STA-17")]
    public async Task Does_not_re_render_inactive_triggers(string name)
    {
        PreviewCardHandle<int> handle = new();
        IRenderedComponent<PreviewCardDetachedHost> cut = host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, handle).Add(x => x.Bystanders, 3));
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
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-focus", "trigger-1", eventJson: PreviewCardTestContext.FocusJson);
                await host.RequestAsync(host.TriggerTwin("trigger-1"), open: false, "trigger-focus", eventJson: PreviewCardTestContext.FocusJson);
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
