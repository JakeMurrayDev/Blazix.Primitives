using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>PopoverRoot.test.tsx</c>'s titles bUnit observes, over the three trigger shapes of its <c>describe.for</c>
/// (contained, detached, multiple detached; POP-STR-12). A press, a hover or a dismissal is the <c>interactions</c>
/// module's report on Server; the popup's exit is the <c>transitions</c> module's report, stood in for by the test.
/// </summary>
public sealed class PopoverRootTests : IDisposable
{
    private readonly PopoverTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    public static TheoryData<string> Shapes => ["contained triggers", "detached triggers", "multiple detached triggers"];

    [Theory(DisplayName = "should render the children")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-STR-01")]
    [Trait("Requirement", "POP-STR-12")]
    public void Should_render_the_children(string shape)
    {
        IRenderedComponent<PopupTestHost> cut = Render(Shape(shape));

        Assert.Equal("Toggle", cut.Find("[data-testid=\"trigger-1\"]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
    }

    [Theory(DisplayName = "should close when the anchor is clicked twice")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-API-03")]
    [Trait("Requirement", "POP-DOM-10")]
    public async Task Should_close_when_the_anchor_is_clicked_twice(string shape)
    {
        IRenderedComponent<PopupTestHost> cut = Render(Shape(shape));

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Equal("Content", cut.Find("[data-testid=\"content\"]").TextContent);

        await test.PressAsync(cut, "trigger-1", open: false);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.Empty(cut.FindAll("[data-testid=\"content\"]"));
    }

    [Theory(DisplayName = "rewires dismiss interactions after closing and reopening")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-ANI-11")]
    public async Task Rewires_dismiss_interactions_after_closing_and_reopening(string shape)
    {
        IRenderedComponent<PopupTestHost> cut = Render(Shape(shape) with { Modal = false, CloseInside = true });

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Contains("\"dismiss\":", test.LastSessionOptions(), StringComparison.Ordinal);

        await test.SessionRequestAsync(cut, open: false, "escape-key", PopoverTestContext.Key("Escape"));
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Contains("\"dismiss\":", test.LastSessionOptions(), StringComparison.Ordinal);

        await test.SessionRequestAsync(cut, open: false, "outside-press");
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
    }

    [Theory(DisplayName = "should call onChange when the open state changes")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-API-01")]
    [Trait("Requirement", "POP-EVT-01")]
    public async Task Should_call_onChange_when_the_open_state_changes(string shape)
    {
        List<bool> calls = [];
        IRenderedComponent<PopupTestHost> cut = null!;
        cut = test.Render(
            (builder, host) => PopoverTree.Build(builder, Shape(shape) with
            {
                Open = host.Get<bool>("open"),
                OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(host, args =>
                {
                    calls.Add(host.Get<bool>("open"));
                    return host.SetAsync("open", args.Value);
                }),
            }),
            new() { ["open"] = false });

        Assert.Empty(cut.FindAll("[data-testid=\"content\"]"));
        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.NotEmpty(cut.FindAll("[data-testid=\"content\"]"));

        await test.PressAsync(cut, "trigger-1", open: false);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.Empty(cut.FindAll("[data-testid=\"content\"]"));
        Assert.Equal([false, true], calls);
    }

    [Theory(DisplayName = "remains uncontrolled, so a trigger click closes it")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-API-02")]
    public async Task Remains_uncontrolled_so_a_trigger_click_closes_it(string shape)
    {
        IRenderedComponent<PopupTestHost> cut = Render(Shape(shape) with { DefaultOpen = true });
        Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]"));

        await test.PressAsync(cut, "trigger-1", open: false);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
    }

    [Theory(DisplayName = "onOpenChange cancel() prevents closing from a close press without changing the trigger")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-EVT-03")]
    public async Task OnOpenChange_cancel_prevents_closing_from_a_close_press_without_changing_the_trigger(string shape)
    {
        List<(bool Open, EventReason Reason, string? Trigger)> calls = [];
        IRenderedComponent<PopupTestHost> cut = Render(Shape(shape) with
        {
            CloseInside = true,
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                calls.Add((args.Value, args.Details.Reason, args.Details.Trigger?.Id));
                if (!args.Value && args.Details.Reason == EventReason.ClosePress)
                {
                    args.Details.Cancel();
                }
            }),
        });

        await test.PressAsync(cut, "trigger-1", open: true);
        await cut.Find("[data-testid=\"close\"]").ClickAsync(new());

        Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal("true", cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-expanded"));
        Assert.Equal((false, EventReason.ClosePress, "trigger-1"), calls[^1]);
    }

    [Theory(DisplayName = "port: a trigger that becomes active again writes its payload again, after a move or a reopen")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "POP-API-14")]
    [Trait("Requirement", "POP-STA-05")]
    public async Task A_trigger_that_becomes_active_again_writes_its_payload_again(bool reopen)
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { DetachedTriggers = true, Handle = new PopoverHandle<int>(), Triggers = 3, KeepMounted = true });

        await test.PressAsync(cut, "trigger-1", open: true);
        await test.PressAsync(cut, "trigger-2", open: true);
        Assert.Equal("2", cut.Find("[data-testid=\"root-payload\"]").TextContent);
        if (reopen)
        {
            await test.PressAsync(cut, "trigger-2", open: false);
            await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        }
        else
        {
            await test.PressAsync(cut, "trigger-3", open: true);
        }

        await test.PressAsync(cut, "trigger-1", open: true);

        Assert.Equal("true", cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-expanded"));
        Assert.Equal("1", cut.Find("[data-testid=\"root-payload\"]").TextContent);
    }

    [Fact(DisplayName = "should render an internal backdrop when `true`")]
    [Trait("Requirement", "POP-STR-13")]
    [Trait("Requirement", "POP-DOM-22")]
    public async Task Should_render_an_internal_backdrop_when_true()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = true });

        await test.PressAsync(cut, "trigger-1", open: true);

        IElement? backdrop = cut.Find("[data-testid=\"positioner\"]").PreviousElementSibling;
        Assert.NotNull(backdrop);
        Assert.Equal("presentation", backdrop.GetAttribute("role"));
        Assert.True(backdrop.HasAttribute("data-base-ui-inert"));
        Assert.Equal("position: fixed; inset: 0; user-select: none; -webkit-user-select: none", backdrop.GetAttribute("style"));
        Assert.False(backdrop.HasAttribute("inert"));
    }

    [Fact(DisplayName = "should not render an internal backdrop when `false`")]
    [Trait("Requirement", "POP-STR-13")]
    public async Task Should_not_render_an_internal_backdrop_when_false()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = false });

        await test.PressAsync(cut, "trigger-1", open: true);

        Assert.Null(cut.Find("[data-testid=\"positioner\"]").PreviousElementSibling);
    }

    [Fact(DisplayName = "enables modal behavior after a hover-open is clicked")]
    [Trait("Requirement", "POP-STR-13")]
    [Trait("Requirement", "POP-PTR-04")]
    public async Task Enables_modal_behavior_after_a_hover_open_is_clicked()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = true, OpenOnHover = true });

        await test.HoverAsync(cut, "trigger-1", open: true);
        Assert.Null(cut.Find("[data-testid=\"positioner\"]").PreviousElementSibling);

        await test.PressAsync(cut, "trigger-1", open: true);

        Assert.Equal("presentation", cut.Find("[data-testid=\"positioner\"]").PreviousElementSibling?.GetAttribute("role"));
    }

    [Fact(DisplayName = "closes the popover when the `close` method is called")]
    [Trait("Requirement", "POP-API-05")]
    [Trait("Requirement", "POP-EVT-11")]
    public async Task Closes_the_popover_when_the_close_method_is_called()
    {
        PopoverRoot<int>? root = null;
        List<EventReason> reasons = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            DefaultOpen = true,
            CaptureRoot = r => root = r,
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => reasons.Add(args.Details.Reason)),
        });

        await cut.InvokeAsync(root!.CloseAsync);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);

        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([EventReason.ImperativeAction], reasons);
    }

    [Fact(DisplayName = "unmounts the popover when the `unmount` method is called")]
    [Trait("Requirement", "POP-API-05")]
    [Trait("Requirement", "POP-ANI-05")]
    public async Task Unmounts_the_popover_when_the_unmount_method_is_called()
    {
        PopoverRoot<int>? root = null;
        List<bool> completes = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            DefaultOpen = true,
            CaptureRoot = r => root = r,
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()),
            OnOpenChangeComplete = EventCallback.Factory.Create<bool>(this, completes.Add),
        });

        await test.PressAsync(cut, "trigger-1", open: false);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Empty(completes);

        await cut.InvokeAsync(root!.Unmount);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=\"popup\"]")));
        Assert.Equal([false], completes);
    }

    [Fact(DisplayName = "ignores `unmount` while the popup is open")]
    [Trait("Requirement", "POP-ANI-04")]
    [Trait("Requirement", "POP-ANI-05")]
    public async Task Ignores_unmount_while_the_popup_is_open()
    {
        PopoverRoot<int>? root = null;
        List<bool> completes = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            DefaultOpen = true,
            CaptureRoot = r => root = r,
            OnOpenChangeComplete = EventCallback.Factory.Create<bool>(this, completes.Add),
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                }
            }),
        });
        string? popupId = cut.Find("[data-testid=\"popup\"]").Id;

        await cut.InvokeAsync(root!.Unmount);

        IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.Equal(popupId, popup.Id);
        Assert.True(popup.HasAttribute("data-open"));
        Assert.False(popup.HasAttribute("data-starting-style"));
        Assert.DoesNotContain(false, completes);

        // The same call is live once the popup is closed and kept mounted: it unmounts the popup.
        await cut.InvokeAsync(root.CloseAsync);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]"));
        await cut.InvokeAsync(root.Unmount);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "does not repeat closing completion when `unmount` is called after the automatic unmount")]
    [Trait("Requirement", "POP-ANI-05")]
    public async Task Does_not_repeat_closing_completion_when_unmount_is_called_after_the_automatic_unmount()
    {
        PopoverRoot<int>? root = null;
        List<bool> completes = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            DefaultOpen = true,
            CaptureRoot = r => root = r,
            OnOpenChangeComplete = EventCallback.Factory.Create<bool>(this, completes.Add),
        });

        await test.PressAsync(cut, "trigger-1", open: false);
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);
        Assert.Equal([false], completes);

        await cut.InvokeAsync(root!.Unmount);
        Assert.Equal([false], completes);
    }

    [Fact(DisplayName = "should keep trigger focus guards when `true` without a close part")]
    [Trait("Requirement", "POP-STR-14")]
    [Trait("Requirement", "POP-DOM-08")]
    [Trait("Requirement", "POP-STA-10")]
    public async Task Should_keep_trigger_focus_guards_when_true_without_a_close_part()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = true });

        await test.PressAsync(cut, "trigger-1", open: true);

        IElement trigger = cut.Find("[data-testid=\"trigger-1\"]");
        IElement? before = trigger.PreviousElementSibling;
        IElement? after = trigger.NextElementSibling;
        Assert.NotNull(before);
        Assert.NotNull(after);
        foreach (IElement guard in new[] { before, after })
        {
            Assert.Equal("span", guard.TagName, ignoreCase: true);
            Assert.Equal("0", guard.GetAttribute("tabindex"));
            Assert.True(guard.HasAttribute("data-base-ui-focus-guard"));
            Assert.Equal("true", guard.GetAttribute("aria-hidden"));
        }
    }

    [Theory(DisplayName = "port: the trigger focus guards render without replacing the trigger element, and leave with the close")]
    [MemberData(nameof(Shapes))]
    [Trait("Requirement", "POP-STR-14")]
    public async Task The_trigger_focus_guards_render_without_replacing_the_trigger_element(string shape)
    {
        IRenderedComponent<PopupTestHost> cut = Render(Shape(shape));
        // A replaced element would capture a new reference: the id of the capture is the element's identity in bUnit.
        string? element = TriggerElementId(cut);
        Assert.NotNull(element);
        Assert.Equal(0, TriggerGuards(cut));

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Equal(2, TriggerGuards(cut));
        Assert.Equal(element, TriggerElementId(cut));

        await test.PressAsync(cut, "trigger-1", open: false);
        Assert.Equal(0, TriggerGuards(cut));
        Assert.Equal(element, TriggerElementId(cut));
    }

    [Theory(DisplayName = "enables modal focus management when `modal=%s` and close is rendered")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "POP-STA-10")]
    [Trait("Requirement", "POP-API-06")]
    public async Task Enables_modal_focus_management_when_modal_and_close_is_rendered(bool trapFocus)
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = trapFocus ? ModalValue.TrapFocus : true, CloseInside = true });

        await test.PressAsync(cut, "trigger-1", open: true);

        // The trap's aria-hidden of the outside is L4 (POP-ARIA-09); C# observes the decision through the guards.
        Assert.True(cut.FindComponent<PopoverRoot<int>>().Instance.Store.State.FocusManagerModal);
        Assert.Equal(0, TriggerGuards(cut));
    }

    [Fact(DisplayName = "does not enable modal focus management when close is not rendered")]
    [Trait("Requirement", "POP-STA-10")]
    public async Task Does_not_enable_modal_focus_management_when_close_is_not_rendered()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = true, CloseOutside = true });

        await test.PressAsync(cut, "trigger-1", open: true);

        Assert.False(cut.FindComponent<PopoverRoot<int>>().Instance.Store.State.FocusManagerModal);
        Assert.Equal(2, TriggerGuards(cut));
    }

    private static PopoverSpec Shape(string shape) => shape switch
    {
        "detached triggers" => new PopoverSpec { DetachedTriggers = true, Handle = new PopoverHandle<int>() },
        "multiple detached triggers" => new PopoverSpec { DetachedTriggers = true, Handle = new PopoverHandle<int>(), Triggers = 3 },
        _ => new PopoverSpec(),
    };

    private static string? TriggerElementId(IRenderedComponent<PopupTestHost> cut) =>
        cut.FindComponents<PopoverTrigger<int>>().Single(trigger => trigger.Find("button").Id == "trigger-1").Instance.Element?.Id;

    // The trigger's own guards, its focus-guard siblings; the focus manager's and the portal's render beside the popup.
    private static int TriggerGuards(IRenderedComponent<PopupTestHost> cut)
    {
        IElement trigger = cut.Find("[data-testid=\"trigger-1\"]");
        return (trigger.PreviousElementSibling?.HasAttribute("data-base-ui-focus-guard") == true ? 1 : 0)
            + (trigger.NextElementSibling?.HasAttribute("data-base-ui-focus-guard") == true ? 1 : 0);
    }

    private IRenderedComponent<PopupTestHost> Render(PopoverSpec spec) => test.Render((builder, _) => PopoverTree.Build(builder, spec));
}
