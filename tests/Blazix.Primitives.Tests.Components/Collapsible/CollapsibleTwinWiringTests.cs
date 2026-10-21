using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The Collapsible twin contract (ADR 06 §1, L2): what the parts send on <c>attach</c> and <c>update</c> to the
/// <c>button</c>, <c>transitions</c> and <c>collapsible</c> modules, the handover at attach (the frozen style, the consumer's
/// string as <c>Style</c>), idempotent attach and dispose (COL-EDGE-09), and each module message driven into C# as one
/// state change: the forced idle (COL-STA-06), the end of the exit (COL-STA-04), the detected type and its warning
/// (COL-EDGE-03, COL-ANI-20), the root's mirrored status (COL-STA-05/07) and the reveal (COL-EVT-04/05).
/// </summary>
public sealed class CollapsibleTwinWiringTests : IDisposable
{
    private const string BeforeMatchJson = """
        {"kind":"Event","type":"beforematch","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-1","tagName":"DIV"},"defaultPrevented":false,"timeStamp":12.5}
        """;

    private readonly CollapsibleTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "the trigger attaches the button module with the effective disabled state and no focusableWhenDisabled")]
    [Trait("Requirement", "COL-API-05")]
    [Trait("Requirement", "COL-PTR-02")]
    [Trait("Requirement", "COL-KEY-03")]
    public void The_trigger_attaches_the_button_module_with_the_effective_disabled_state()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.Add(x => x.NativeButton, false)));

        Assert.Equal(
            """{"disabled":true,"native":false,"composite":false,"reportInteractionType":false,"developmentChecks":true}""",
            Assert.Single(host.AttachOptions(host.ButtonModule)));

        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.Equal(
            ["""{"disabled":false,"native":false,"composite":false,"reportInteractionType":false,"developmentChecks":true}"""],
            host.UpdateOptions(host.ButtonHandle));
    }

    [Fact(DisplayName = "the root and trigger attach the transitions module reading their open attribute, ending with the panel")]
    [Trait("Requirement", "COL-STA-05")]
    [Trait("Requirement", "COL-DOM-03")]
    [Trait("Requirement", "COL-DOM-06")]
    public void The_root_and_trigger_attach_the_transitions_module()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>());

        Assert.Equal(
            [
                """{"open":true,"mounted":true,"status":"idle","enableIdleState":true,"deferEndingState":true,"keepMounted":true,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":true,"openSelector":"[data-open]"}""",
                """{"open":true,"mounted":true,"status":"idle","enableIdleState":true,"deferEndingState":true,"keepMounted":true,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":true,"openSelector":"[data-panel-open]"}""",
            ],
            host.AttachOptions(host.TransitionsModule).Order(StringComparer.Ordinal));

        // The panel's exit ends the root's and the trigger's: each gets one update with the raised count.
        cut.Find("button").Click();
        host.CompletePanelExit(cut);
        Assert.Equal(2, host.UpdateOptions(host.TransitionsHandle).Count(options => options!.Contains("\"unmountRequests\":1", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "the root mirrors its status from its transitions twin, once per message")]
    [Trait("Requirement", "COL-STA-05")]
    [Trait("Requirement", "COL-STA-07")]
    public void The_root_mirrors_its_status_from_its_transitions_twin()
    {
        List<TransitionStatus?> seen = [];
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.ClassValue, (Func<CollapsibleRootState, string?>)(state =>
            {
                seen.Add(state.TransitionStatus);
                return null;
            }))
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>());
        TransitionsTwin rootTwin = host.TransitionsTwinAt(0);

        cut.Find("button").Click();
        // The root's class function runs once per render of the root.
        int renders = seen.Count;
        cut.InvokeAsync(() => rootTwin.OnStatusChange("""{"status":"starting","mounted":true}"""));
        Assert.Equal(renders, seen.Count);
        cut.InvokeAsync(() => rootTwin.OnStatusChange("""{"status":"idle","mounted":true}"""));
        Assert.Equal(renders + 1, seen.Count);

        // The click renders the opening state with the evaluated 'starting'; the report of the same status renders nothing.
        Assert.Equal([null, TransitionStatus.Starting, TransitionStatus.Idle], seen.Distinct());
    }

    [Fact(DisplayName = "the panel attaches with the values it rendered and hands its style over")]
    [Trait("Requirement", "COL-DOM-17")]
    [Trait("Requirement", "COL-DOM-18")]
    [Trait("Requirement", "COL-ANI-13")]
    [Trait("Requirement", "COL-ANI-14")]
    public void The_panel_attaches_with_the_values_it_rendered_and_hands_its_style_over()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel
                .AddUnmatched("id", "panel")
                .Add(x => x.StyleValue, (Func<CollapsiblePanelState, string?>)(state => state.Open ? "color: red" : "color: blue"))));
        string renderedStyle = cut.Find("#panel").GetAttribute("style")!;

        Assert.Equal(
            """{"open":true,"mounted":true,"status":"idle","keepMounted":false,"hiddenUntilFound":false,"suppressOpenAnimation":true,"panelId":"panel","openSelector":"[data-open]","style":"color: red"}""",
            Assert.Single(host.AttachOptions(host.CollapsibleModule)));
        Assert.Equal("--collapsible-panel-height: auto; --collapsible-panel-width: auto; color: red; animation-name: none;", renderedStyle);

        // From attach the style is frozen in the markup and the new value travels with the update.
        cut.Find("button").Click();
        Assert.Equal(renderedStyle, cut.Find("#panel").GetAttribute("style"));
        Assert.Equal(
            """{"open":false,"mounted":true,"status":"idle","keepMounted":false,"hiddenUntilFound":false,"suppressOpenAnimation":false,"panelId":"panel","openSelector":"[data-open]","style":"color: blue"}""",
            host.UpdateOptions(host.CollapsibleHandle)[^1]);
    }

    [Fact(DisplayName = "a repeated attach of the same panel element and a repeated dispose send nothing more")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "COL-EDGE-09")]
    public async Task A_repeated_attach_of_the_same_panel_element_and_a_repeated_dispose_send_nothing_more()
    {
        await using CollapsibleTwin twin = new(Context.JSInterop.JSRuntime);

        TwinHandle<CollapsibleOptions> first = await twin.AttachAsync("base-ui-1", new CollapsibleOptions { PanelId = "base-ui-1" });
        TwinHandle<CollapsibleOptions> second = await twin.AttachAsync("base-ui-1", new CollapsibleOptions { PanelId = "base-ui-1", Open = true });
        await first.DisposeAsync();
        await first.DisposeAsync();
        await first.UpdateAsync(new CollapsibleOptions { Open = true });

        Assert.Same(first, second);
        Assert.Single(host.CollapsibleModule.Invocations["attach"]);
        Assert.Single(host.CollapsibleHandle.Invocations["dispose"]);
        Assert.Empty(host.CollapsibleHandle.Invocations["update"]);
    }

    [Fact(DisplayName = "the forced idle is one render of the panel's status")]
    [Trait("Requirement", "COL-STA-06")]
    public void The_forced_idle_is_one_render_of_the_panels_status()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true)));
        IRenderedComponent<CollapsiblePanel> panel = cut.FindComponent<CollapsiblePanel>();
        cut.Find("button").Click();
        int renders = panel.RenderCount;

        cut.InvokeAsync(() => host.PanelTwin().OnForcedIdleChange("""{"forcedIdle":true}"""));
        cut.InvokeAsync(() => host.PanelTwin().OnForcedIdleChange("""{"forcedIdle":true}"""));

        Assert.Equal(renders + 1, panel.RenderCount);
        Assert.Equal(TransitionStatus.Idle, PanelState(panel).TransitionStatus);
        // The root keeps its own status: only the panel shows itself settled.
        Assert.Equal(TransitionStatus.Starting, cut.Instance.Context.State.TransitionStatus);
    }

    [Fact(DisplayName = "the end of the exit unmounts the panel in one state change and is dropped after a reopen")]
    [Trait("Requirement", "COL-STA-04")]
    [Trait("Requirement", "COL-ANI-16")]
    public void The_end_of_the_exit_unmounts_the_panel_and_is_dropped_after_a_reopen()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("data-testid", "panel")));
        CollapsibleTwin twin = host.PanelTwin();

        cut.Find("button").Click();
        cut.Find("button").Click();
        cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        Assert.True(cut.Instance.Context.State.Mounted);
        Assert.Single(cut.FindAll("[data-testid=panel]"));

        cut.Find("button").Click();
        cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        Assert.False(cut.Instance.Context.State.Mounted);
        Assert.Empty(cut.FindAll("[data-testid=panel]"));
        Assert.Equal(1, cut.Instance.Context.State.UnmountRequests);
    }

    [Fact(DisplayName = "both detected motions log the warning once per session")]
    [Trait("Requirement", "COL-EDGE-03")]
    [Trait("Requirement", "COL-ANI-02")]
    public void Both_detected_motions_log_the_warning_once_per_session()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsiblePanel>()
            .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("id", "second")));

        foreach (JSRuntimeInvocation attach in host.CollapsibleModule.Invocations["attach"])
        {
            CollapsibleTwin twin = ((DotNetObjectReference<CollapsibleTwin>)attach.Arguments[2]!).Value;
            cut.InvokeAsync(() => twin.OnAnimationTypeChange("""{"type":"css-transition","mixed":true}"""));
        }

        (LogLevel level, string message) = Assert.Single(host.PanelLogger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "Blazix: CSS transitions and CSS animations both detected on a collapsible or accordion panel. Only one of either animation type should be used.",
            message);
    }

    [Fact(DisplayName = "a keyframe panel detached while open renders the suppression when it is attached again")]
    [Trait("Requirement", "COL-ANI-20")]
    public void A_keyframe_panel_detached_while_open_renders_the_suppression_when_it_is_attached_again()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "panel")));
        cut.Find("button").Click();
        cut.InvokeAsync(() => host.PanelTwin().OnAnimationTypeChange("""{"type":"css-animation","mixed":false}"""));
        IRenderedComponent<CollapsiblePanel> panel = cut.FindComponent<CollapsiblePanel>();

        panel.Render(p => p.Add(x => x.Enabled, false));
        Assert.Empty(cut.FindAll("[data-testid=panel]"));
        panel.Render(p => p.Add(x => x.Enabled, true));

        Assert.Contains("animation-name: none", cut.Find("[data-testid=panel]").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("\"suppressOpenAnimation\":true", host.AttachOptions(host.CollapsibleModule)[^1], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the reveal decision requests the open with reason none inside the dispatch and answers stop when canceled")]
    [Trait("Requirement", "COL-EVT-04")]
    [Trait("Requirement", "COL-EVT-05")]
    public async Task The_reveal_decision_requests_the_open_and_answers_stop_when_canceled()
    {
        List<ValueChangeEventArgs<bool>> calls = [];
        bool cancel = true;
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                calls.Add(args);
                if (cancel)
                {
                    args.Details.Cancel();
                }
            })
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.HiddenUntilFound, true)));
        CollapsibleTwin twin = host.PanelTwin();

        Assert.Equal("stop", await cut.InvokeAsync(() => twin.Decide(CollapsibleTwin.RevealPoint, BeforeMatchJson)));
        Assert.False(cut.Instance.Context.State.Open);

        cancel = false;
        Assert.Equal("run", await cut.InvokeAsync(() => twin.Decide(CollapsibleTwin.RevealPoint, BeforeMatchJson)));
        Assert.True(cut.Instance.Context.State.Open);

        Assert.Equal(2, calls.Count);
        Assert.All(calls, call =>
        {
            Assert.True(call.Value);
            Assert.Equal(EventReason.None, call.Details.Reason);
            Assert.Equal("beforematch", call.Details.Event.Type);
            Assert.Null(call.Details.Trigger);
        });
    }

    [Fact(DisplayName = "the reveal request answers whether the panel is open once the request ran")]
    [Trait("Requirement", "COL-EVT-04")]
    [Trait("Requirement", "COL-ANI-24")]
    public async Task The_reveal_request_answers_whether_the_panel_is_open_once_the_request_ran()
    {
        IRenderedComponent<CollapsibleRoot> ignored = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Open, false)
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.HiddenUntilFound, true)));
        CollapsibleTwin ignoredTwin = host.PanelTwin();
        IRenderedComponent<CollapsibleRoot> uncontrolled = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.HiddenUntilFound, true)));
        CollapsibleTwin uncontrolledTwin = host.PanelTwin();

        Assert.False(await ignored.InvokeAsync(() => ignoredTwin.OnRevealRequest($$"""{"event":{{BeforeMatchJson}}}""")));
        Assert.True(await uncontrolled.InvokeAsync(() => uncontrolledTwin.OnRevealRequest($$"""{"event":{{BeforeMatchJson}}}""")));
    }

    private static CollapsiblePanelState PanelState(IRenderedComponent<CollapsiblePanel> panel)
    {
        CollapsiblePanelState? state = null;
        panel.Render(p => p.Add(x => x.ClassValue, (Func<CollapsiblePanelState, string?>)(s =>
        {
            state = s;
            return null;
        })));
        return state!;
    }
}
