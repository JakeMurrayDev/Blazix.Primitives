using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The tooltip root at L2 (TooltipRoot.test.tsx, titles verbatim where Base UI has one): its store, its open state controlled
/// and uncontrolled, the disabled state, the imperative close and unmount, the session's dismissal, and the requests the
/// <c>interactions</c> module reports, driven through <see cref="TooltipTestContext.RequestAsync"/> (ADR 06 §1).
/// </summary>
public sealed class TooltipRootTests : IDisposable
{
    private readonly TooltipTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should open when the trigger is hovered")]
    [Trait("Requirement", "TIP-API-06")]
    [Trait("Requirement", "TIP-STR-02")]
    [Trait("Requirement", "TIP-STR-11")]
    public async Task Should_open_when_the_trigger_is_hovered()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>();
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        cut.WaitForAssertion(() => Assert.Contains("Content", cut.Find("[data-testid=\"popup\"]").TextContent, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("[data-testid=\"before-trigger\"]"));
        Assert.NotNull(cut.Find("[data-testid=\"between-trigger-and-portal\"]"));
        Assert.NotNull(cut.Find("[data-testid=\"after-portal\"]"));
        Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "port: the root renders no element and the popup renders no role, a focusable tabindex and the open state")]
    [Trait("Requirement", "TIP-STR-02")]
    [Trait("Requirement", "TIP-ARIA-01")]
    [Trait("Requirement", "TIP-DOM-11")]
    [Trait("Requirement", "TIP-DOM-12")]
    public void The_root_renders_no_element_and_the_popup_renders_no_role()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.False(popup.HasAttribute("role"));
        Assert.Equal("-1", popup.GetAttribute("tabindex"));
        Assert.True(popup.HasAttribute("data-base-ui-focusable"));
        Assert.True(popup.HasAttribute("data-open"));
        Assert.Equal("top", popup.GetAttribute("data-side"));
        Assert.Equal("center", popup.GetAttribute("data-align"));
        Assert.False(popup.HasAttribute("data-instant"));
        Assert.Equal("before-trigger", cut.Nodes.OfType<AngleSharp.Dom.IElement>().First().GetAttribute("data-testid"));
    }

    [Fact(DisplayName = "closes when the trigger is unhovered after opening by default")]
    [Trait("Requirement", "TIP-API-04")]
    [Trait("Requirement", "PSH-STA-18")]
    public async Task Closes_when_the_trigger_is_unhovered_after_opening_by_default()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open")));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        cut.WaitForAssertion(() => Assert.False(cut.Instance.Root!.Store.IsOpen));
        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "should call onOpenChange when the open state changes")]
    [Trait("Requirement", "TIP-STA-03")]
    [Trait("Requirement", "TIP-API-05")]
    [Trait("Requirement", "TIP-EVT-02")]
    public async Task Should_call_onOpenChange_when_the_open_state_changes()
    {
        IRenderedComponent<TooltipControlledHost> cut = host.Context.Render<TooltipControlledHost>();

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        // The handler observes the state before the change (EVD-STA-08).
        Assert.Equal([(true, EventReason.TriggerHover, false)], cut.Instance.Calls);
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Root!.Store.IsOpen));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")));
    }

    [Fact(DisplayName = "is ignored when `open={true}` is passed, so Escape cannot close")]
    [Trait("Requirement", "TIP-API-05")]
    [Trait("Requirement", "TIP-STA-03")]
    [Trait("Requirement", "TIP-EVT-06")]
    public async Task Is_ignored_when_open_true_is_passed_so_Escape_cannot_close()
    {
        IRenderedComponent<TooltipControlledHost> cut = host.Context.Render<TooltipControlledHost>(p => p.Add(x => x.Open, true).Add(x => x.Accept, false));

        await host.RequestAsync(host.SessionTwin(), open: false, "escape-key", eventJson: null);

        Assert.Equal([(false, EventReason.EscapeKey, true)], cut.Instance.Calls);
        Assert.True(cut.Instance.Root!.Store.IsOpen);
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "keeps the popup closed when the open change is canceled")]
    [Trait("Requirement", "TIP-EVT-03")]
    [Trait("Requirement", "TIP-STA-03")]
    public async Task Keeps_the_popup_closed_when_the_open_change_is_canceled()
    {
        List<EventReason> reasons = [];
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(
            x => x.OnOpenChange,
            EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                reasons.Add(args.Details.Reason);
                args.Details.Cancel();
            })));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        Assert.Equal([EventReason.TriggerHover], reasons);
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.Null(cut.Instance.Root.Store.State.LastOpenChangeReason);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "should not open when disabled")]
    [Trait("Requirement", "TIP-API-09")]
    [Trait("Requirement", "TIP-STA-02")]
    public async Task Should_not_open_when_disabled()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.Disabled, true));

        // A disabled trigger sends no hover or focus to the module (TIP-API-18); a request that reached the root anyway is
        // kept closed, then reverted with reason `disabled`.
        JsonElement trigger = host.TriggerOptions("trigger");
        Assert.False(trigger.TryGetProperty("hover", out _));
        Assert.False(trigger.TryGetProperty("focus", out _));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        cut.WaitForAssertion(() => Assert.False(cut.Instance.Root.Store.Popup.Open));
        Assert.Equal(EventReason.Disabled, cut.Instance.Root.Store.State.LastOpenChangeReason);
    }

    [Fact(DisplayName = "should close if open when becoming disabled")]
    [Trait("Requirement", "TIP-DIS-06")]
    [Trait("Requirement", "TIP-STA-16")]
    public async Task Should_close_if_open_when_becoming_disabled()
    {
        List<(bool, EventReason)> calls = [];
        EventCallback<ValueChangeEventArgs<bool>> onOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => calls.Add((args.Value, args.Details.Reason)));
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.OnOpenChange, onOpenChange));
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));

        cut.Render(p => p.Add(x => x.Disabled, true).Add(x => x.OnOpenChange, onOpenChange));

        // The exit starts in the batch that disables (TIP-STA-02), and the disabled close follows at its boundary.
        cut.WaitForAssertion(() => Assert.Equal([(false, EventReason.Disabled)], calls));
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.False(cut.Instance.Root.Store.Popup.Open);
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
        Assert.True(cut.Find("#trigger").HasAttribute("data-trigger-disabled"));
    }

    [Fact(DisplayName = "does not throw error when combined with defaultOpen")]
    [Trait("Requirement", "TIP-EDGE-10")]
    [Trait("Requirement", "TIP-STA-02")]
    [Trait("Requirement", "TIP-DIS-06")]
    public void Does_not_throw_error_when_combined_with_defaultOpen()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.Disabled, true));

        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        cut.WaitForAssertion(() => Assert.False(cut.Instance.Root.Store.Popup.Open));
        Assert.Equal(EventReason.Disabled, cut.Instance.Root.Store.State.LastOpenChangeReason);
    }

    [Fact(DisplayName = "marks the trigger as disabled when the root is disabled")]
    [Trait("Requirement", "TIP-DOM-03")]
    [Trait("Requirement", "TIP-API-09")]
    [Trait("Requirement", "TIP-DOM-04")]
    public void Marks_the_trigger_as_disabled_when_the_root_is_disabled()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.Disabled, true));

        AngleSharp.Dom.IElement trigger = cut.Find("#trigger");
        Assert.True(trigger.HasAttribute("data-trigger-disabled"));
        Assert.False(trigger.HasAttribute("data-base-ui-tooltip-trigger"));
        Assert.False(trigger.HasAttribute("disabled"));
    }

    [Fact(DisplayName = "keeps the tooltip disabled when the root is disabled and the trigger opts back in")]
    [Trait("Requirement", "TIP-API-09")]
    [Trait("Requirement", "TIP-DOM-03")]
    [Trait("Requirement", "TIP-API-18")]
    public async Task Keeps_the_tooltip_disabled_when_the_root_is_disabled_and_the_trigger_opts_back_in()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.Disabled, true).Add(x => x.TriggerDisabled, false));
        AngleSharp.Dom.IElement trigger = cut.Find("#trigger");
        Assert.False(trigger.HasAttribute("data-trigger-disabled"));
        Assert.True(trigger.HasAttribute("data-base-ui-tooltip-trigger"));
        Assert.True(host.TriggerOptions("trigger").TryGetProperty("hover", out _));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        cut.WaitForAssertion(() => Assert.False(cut.Instance.Root!.Store.Popup.Open));
    }

    [Fact(DisplayName = "closes the tooltip when the `close` method is called")]
    [Trait("Requirement", "TIP-API-08")]
    [Trait("Requirement", "TIP-DIS-07")]
    [Trait("Requirement", "TIP-DOM-02")]
    [Trait("Requirement", "TIP-EVT-01")]
    public async Task Closes_the_tooltip_when_the_close_method_is_called()
    {
        List<(bool, EventReason)> calls = [];
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true).Add(
            x => x.OnOpenChange,
            EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => calls.Add((args.Value, args.Details.Reason)))));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open")));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());

        Assert.Equal([(false, EventReason.ImperativeAction)], calls);
        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "unmounts the tooltip when the `unmount` method is called")]
    [Trait("Requirement", "TIP-API-08")]
    [Trait("Requirement", "TIP-ANI-05")]
    [Trait("Requirement", "TIP-EVT-04")]
    public async Task Unmounts_the_tooltip_when_the_unmount_method_is_called()
    {
        List<bool> completes = [];
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()))
            .Add(x => x.OnOpenChangeComplete, EventCallback.Factory.Create<bool>(this, completes.Add)));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());

        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
        Assert.True(cut.Instance.Root!.Store.Popup.Mounted);

        await cut.InvokeAsync(cut.Instance.Root.Unmount);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=\"popup\"]")));
        Assert.Equal([false], completes);
    }

    [Fact(DisplayName = "does not prevent unmounting on later closes")]
    [Trait("Requirement", "TIP-EVT-04")]
    [Trait("Requirement", "TIP-ANI-06")]
    public async Task Does_not_prevent_unmounting_on_later_closes()
    {
        int closes = 0;
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                if (!args.Value && closes++ == 0)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                }
            })));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());
        Assert.True(cut.Instance.Root!.Store.Popup.Mounted);
        await cut.InvokeAsync(cut.Instance.Root.Unmount);
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        // The opted-out exit belongs to one close cycle (TRN-ANI-24): the next close leaves the exit to the popup's handle.
        Assert.False(cut.Instance.Root.Store.Popup.PreventUnmountingOnClose);
        Assert.Equal(TransitionStatus.Ending, cut.Instance.Root.Store.Popup.TransitionStatus);
    }

    [Fact(DisplayName = "port: every applied change records its reason, the delay group's and the disabled close included")]
    [Trait("Requirement", "TIP-STA-04")]
    [Trait("Requirement", "TIP-EVT-01")]
    public async Task Every_applied_change_records_its_reason()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>();
        TooltipStore<NoPayload> store = cut.Instance.Root!.Store;

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-focus", "trigger", eventJson: TooltipTestContext.FocusJson);
        Assert.Equal(EventReason.TriggerFocus, store.State.LastOpenChangeReason);

        await host.RequestAsync(host.SessionTwin(), open: false, "none", instant: true, eventJson: null);
        Assert.Equal(EventReason.None, store.State.LastOpenChangeReason);
        Assert.Equal(TooltipInstantType.Delay, store.State.InstantType);
    }

    [Fact(DisplayName = "port: the delay group's phase on an open request renders data-instant=\"delay\" in the render of data-open, and an ordinary close removes it")]
    [Trait("Requirement", "TIP-STA-06")]
    [Trait("Requirement", "TIP-STA-07")]
    [Trait("Requirement", "TIP-STA-13")]
    public async Task The_delay_groups_phase_renders_delay_with_the_open_state()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>();

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger", instant: true);

        AngleSharp.Dom.IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.True(popup.HasAttribute("data-open"));
        Assert.Equal("delay", popup.GetAttribute("data-instant"));
        Assert.Equal("delay", cut.Find("[data-testid=\"positioner\"]").GetAttribute("data-instant"));
        Assert.Equal("delay", cut.Find("[data-testid=\"arrow\"]").GetAttribute("data-instant"));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        popup = cut.Find("[data-testid=\"popup\"]");
        Assert.True(popup.HasAttribute("data-closed"));
        Assert.False(popup.HasAttribute("data-instant"));
    }

    [Fact(DisplayName = "port: a focus open renders data-instant=\"focus\" and a press close renders dismiss on every popup part")]
    [Trait("Requirement", "TIP-STA-05")]
    [Trait("Requirement", "TIP-STA-13")]
    public async Task A_focus_open_renders_focus_and_a_press_close_renders_dismiss()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>();

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-focus", "trigger", eventJson: TooltipTestContext.FocusJson);

        Assert.Equal("focus", cut.Find("[data-testid=\"popup\"]").GetAttribute("data-instant"));
        Assert.Equal("focus", cut.Find("[data-testid=\"arrow\"]").GetAttribute("data-instant"));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-press");

        Assert.Equal("dismiss", cut.Find("[data-testid=\"popup\"]").GetAttribute("data-instant"));
        Assert.Equal("dismiss", cut.Find("[data-testid=\"positioner\"]").GetAttribute("data-instant"));
    }

    [Fact(DisplayName = "port: the positioner renders data-instant=\"tracking-cursor\" while the root tracks the pointer")]
    [Trait("Requirement", "TIP-STA-12")]
    [Trait("Requirement", "TIP-API-11")]
    public void The_positioner_renders_tracking_cursor_while_the_root_tracks_the_pointer()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.TrackCursorAxis, TooltipTrackCursorAxis.X));

        Assert.Equal("tracking-cursor", cut.Find("[data-testid=\"positioner\"]").GetAttribute("data-instant"));
        Assert.False(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-instant"));
        Assert.Equal("x", host.TriggerOptions("trigger").GetProperty("clientPoint").GetProperty("axis").GetString());
    }

    [Fact(DisplayName = "stops tracking the cursor after trackCursorAxis is disabled while closed")]
    [Trait("Requirement", "TIP-STA-15")]
    [Trait("Requirement", "TIP-STA-16")]
    [Trait("Requirement", "TIP-PTR-16")]
    public void Stops_tracking_the_cursor_after_trackCursorAxis_is_disabled_while_closed()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.TrackCursorAxis, TooltipTrackCursorAxis.Both));
        Assert.Equal("both", host.TriggerOptions("trigger").GetProperty("clientPoint").GetProperty("axis").GetString());
        Assert.True(host.TriggerOptions("trigger").GetProperty("referencePress").GetBoolean());
        Assert.False(host.TriggerOptions("trigger").GetProperty("hover").TryGetProperty("safePolygon", out _));

        cut.Render(p => p.Add(x => x.TrackCursorAxis, TooltipTrackCursorAxis.None));

        cut.WaitForAssertion(() => Assert.False(host.TriggerOptions("trigger").TryGetProperty("clientPoint", out _)));
        Assert.False(host.TriggerOptions("trigger").TryGetProperty("referencePress", out _));
        Assert.True(host.TriggerOptions("trigger").GetProperty("hover").GetProperty("safePolygon").GetBoolean());
    }

    [Fact(DisplayName = "port: the session holds Escape and the outside press only while the tooltip is open, mounted or tracking, never while disabled")]
    [Trait("Requirement", "TIP-STA-15")]
    [Trait("Requirement", "TIP-KEY-01")]
    [Trait("Requirement", "TIP-PTR-14")]
    public async Task The_session_holds_dismissal_only_while_open_mounted_or_tracking()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>();
        Assert.Null(LastDismiss());

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.Equal("{}", LastDismiss()));

        cut.Render(p => p.Add(x => x.Disabled, true));
        cut.WaitForAssertion(() => Assert.Null(LastDismiss()));
    }

    [Fact(DisplayName = "port: a request with no trigger while open clears the payload in the commit")]
    [Trait("Requirement", "TIP-STA-10")]
    public async Task A_request_with_no_trigger_while_open_clears_the_payload()
    {
        TooltipHandle<int> handle = new();
        IRenderedComponent<TooltipDetachedHost> cut = host.Context.Render<TooltipDetachedHost>(p => p.Add(x => x.Handle, handle));
        await host.RequestAsync(host.TriggerTwin("trigger-1"), open: true, "trigger-hover", "trigger-1");
        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid=\"payload\"]").TextContent));

        await cut.InvokeAsync(() => handle.CloseAsync());
        await cut.InvokeAsync(() => handle.OpenAsync());

        cut.WaitForAssertion(() => Assert.Equal("0", cut.Find("[data-testid=\"payload\"]").TextContent));
        Assert.False(cut.Instance.Root!.Store.HasPayload);
    }

    [Fact(DisplayName = "port: an open and a close by hover render neither the root nor the trigger's siblings outside the popup")]
    [Trait("Requirement", "TIP-EDGE-11")]
    [Trait("Requirement", "ADR16-7")]
    public async Task An_open_and_a_close_render_neither_the_root_nor_its_content()
    {
        int contentRenders = 0;
        IRenderedComponent<TooltipRoot<NoPayload>> cut = host.Context.Render<TooltipRoot<NoPayload>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            contentRenders++;
            builder.OpenComponent<TooltipTrigger<NoPayload>>(0);
            builder.AddAttribute(1, "id", "trigger");
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
        int before = contentRenders;

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed")));

        // The figure measured on this branch: 0 renders of the root's content for an open and a close.
        Assert.Equal(before, contentRenders);
    }

    private string? LastDismiss()
    {
        string? last = null;
        bool seen = false;
        foreach (string json in host.InteractionsAttaches.Select(attach => (string)attach.Arguments[1]!).Concat(host.InteractionsUpdates))
        {
            JsonElement root = JsonDocument.Parse(json).RootElement;
            if (root.TryGetProperty("session", out JsonElement session))
            {
                seen = true;
                last = session.TryGetProperty("dismiss", out JsonElement dismiss) ? dismiss.GetRawText() : null;
            }
        }

        Assert.True(seen);
        return last;
    }
}
