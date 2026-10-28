using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the titles of <c>PopoverTrigger.test.tsx</c>, <c>PopoverClose.test.tsx</c>, <c>PopoverTitle.test.tsx</c>,
/// <c>PopoverDescription.test.tsx</c>, <c>PopoverBackdrop.test.tsx</c>, <c>PopoverPopup.test.tsx</c> and
/// <c>PopoverPositioner.test.tsx</c> that bUnit observes, and the port titles of the rows Base UI leaves untested.
/// </summary>
public sealed class PopoverPartsTests : IDisposable
{
    private readonly PopoverTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered without a root or a handle")]
    [Trait("Requirement", "POP-EDGE-01")]
    [Trait("Requirement", "POP-STR-03")]
    public void Throws_a_descriptive_error_when_rendered_without_a_root_or_a_handle()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<PopoverTrigger<NoPayload>>());
        Assert.Equal("Blazix: `<PopoverTrigger>` must be either used within a `<PopoverRoot>` component or provided with a handle.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Popover.Root>")]
    [Trait("Requirement", "POP-EDGE-02")]
    [Trait("Requirement", "POP-STR-06")]
    public void Throws_a_descriptive_error_when_rendered_outside_Popover_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<PopoverPopup>());
        Assert.Equal("Blazix: The `PopoverRoot` context is missing. Popover parts must be placed within `<PopoverRoot>`.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Popover.Positioner>")]
    [Trait("Requirement", "POP-EDGE-04")]
    [Trait("Requirement", "POP-STR-06")]
    public void Throws_a_descriptive_error_when_rendered_outside_Popover_Positioner()
    {
        test.Context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<PopoverPopup>());
        Assert.Equal("Blazix: The `PopoverPositioner` context is missing. `PopoverPositioner` parts must be placed within `<PopoverPositioner>`.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Popover.Portal>")]
    [Trait("Requirement", "POP-EDGE-03")]
    [Trait("Requirement", "POP-STR-05")]
    public void Throws_a_descriptive_error_when_rendered_outside_Popover_Portal()
    {
        test.Context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<PopoverPositioner>());
        Assert.Equal("Blazix: `<PopoverPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "disables the popover")]
    [Trait("Requirement", "POP-API-12")]
    [Trait("Requirement", "POP-DOM-03")]
    [Trait("Requirement", "POP-DOM-07")]
    public void Disables_the_popover()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { TriggerDisabled = true });

        IElement trigger = cut.Find("[data-testid=\"trigger-1\"]");
        Assert.True(trigger.HasAttribute("disabled"));
        Assert.True(trigger.HasAttribute("data-disabled"));
        Assert.False(trigger.HasAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "custom element")]
    [Trait("Requirement", "POP-API-11")]
    [Trait("Requirement", "POP-API-12")]
    [Trait("Requirement", "POP-DOM-06")]
    [Trait("Requirement", "POP-DOM-07")]
    public void Custom_element()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<PopoverRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(PopoverRoot<NoPayload>.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<PopoverTrigger<NoPayload>>(0);
                b.AddComponentParameter(1, nameof(PopoverTrigger<NoPayload>.Disabled), true);
                b.AddComponentParameter(2, nameof(PopoverTrigger<NoPayload>.NativeButton), false);
                b.AddComponentParameter(3, nameof(PopoverTrigger<NoPayload>.Render), (RenderFragment)(r =>
                {
                    r.OpenElement(0, "span");
                    r.CloseElement();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        IElement trigger = cut.Find("span");
        Assert.Equal("button", trigger.GetAttribute("role"));
        Assert.False(trigger.HasAttribute("disabled"));
        Assert.True(trigger.HasAttribute("data-disabled"));
        Assert.Equal("true", trigger.GetAttribute("aria-disabled"));
        Assert.Equal("-1", trigger.GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "should have the data-popup-open and data-pressed attributes when open by clicking")]
    [Trait("Requirement", "POP-DOM-01")]
    [Trait("Requirement", "POP-DOM-02")]
    [Trait("Requirement", "POP-STA-06")]
    public async Task Should_have_the_data_popup_open_and_data_pressed_attributes_when_open_by_clicking()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec());

        await test.PressAsync(cut, "trigger-1", open: true);

        IElement trigger = cut.Find("[data-testid=\"trigger-1\"]");
        Assert.True(trigger.HasAttribute("data-popup-open"));
        Assert.True(trigger.HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "should have the data-popup-open but not the data-pressed attribute when open by hover")]
    [Trait("Requirement", "POP-DOM-02")]
    [Trait("Requirement", "POP-STA-06")]
    public async Task Should_have_the_data_popup_open_but_not_the_data_pressed_attribute_when_open_by_hover()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { OpenOnHover = true });

        await test.HoverAsync(cut, "trigger-1", open: true);

        IElement trigger = cut.Find("[data-testid=\"trigger-1\"]");
        Assert.True(trigger.HasAttribute("data-popup-open"));
        Assert.False(trigger.HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "should have the data-popup-open and data-pressed attributes when open by click when `openOnHover=true`")]
    [Trait("Requirement", "POP-DOM-02")]
    public async Task Should_have_the_data_popup_open_and_data_pressed_attributes_when_open_by_click_when_openOnHover()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { OpenOnHover = true });

        await test.PressAsync(cut, "trigger-1", open: true);

        IElement trigger = cut.Find("[data-testid=\"trigger-1\"]");
        Assert.True(trigger.HasAttribute("data-popup-open"));
        Assert.True(trigger.HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "port: a press within the patient-click window after a hover open adds data-pressed")]
    [Trait("Requirement", "POP-DOM-02")]
    public async Task A_press_after_a_hover_open_adds_data_pressed()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { OpenOnHover = true });

        await test.HoverAsync(cut, "trigger-1", open: true);
        Assert.False(cut.Find("[data-testid=\"trigger-1\"]").HasAttribute("data-pressed"));

        // The module keeps a hover-opened popover open on an impatient press and requests open again (`stickIfOpen`).
        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.True(cut.Find("[data-testid=\"trigger-1\"]").HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "port: the trigger renders its ARIA attributes and the click-trigger marker")]
    [Trait("Requirement", "POP-ARIA-01")]
    [Trait("Requirement", "POP-ARIA-02")]
    [Trait("Requirement", "POP-ARIA-03")]
    [Trait("Requirement", "POP-DOM-04")]
    [Trait("Requirement", "POP-DOM-05")]
    [Trait("Requirement", "POP-DOM-06")]
    public async Task The_trigger_renders_its_ARIA_attributes_and_the_click_trigger_marker()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Triggers = 2 });
        IElement first = cut.Find("[data-testid=\"trigger-1\"]");
        Assert.Equal("dialog", first.GetAttribute("aria-haspopup"));
        Assert.Equal("false", first.GetAttribute("aria-expanded"));
        Assert.False(first.HasAttribute("aria-controls"));
        Assert.Equal("", first.GetAttribute("data-base-ui-click-trigger"));
        Assert.Equal("button", first.GetAttribute("type"));
        Assert.Equal("trigger-1", first.Id);

        await test.PressAsync(cut, "trigger-2", open: true);
        IElement popup = cut.Find("[data-testid=\"popup\"]");
        IElement second = cut.Find("[data-testid=\"trigger-2\"]");
        Assert.Equal("true", second.GetAttribute("aria-expanded"));
        Assert.Equal(popup.Id, second.GetAttribute("aria-controls"));
        Assert.Equal("dialog", second.GetAttribute("aria-haspopup"));
        first = cut.Find("[data-testid=\"trigger-1\"]");
        Assert.Equal("false", first.GetAttribute("aria-expanded"));
        Assert.False(first.HasAttribute("aria-controls"));
        Assert.Equal("dialog", first.GetAttribute("aria-haspopup"));
    }

    [Fact(DisplayName = "renders when popover is closed")]
    [Trait("Requirement", "POP-STR-11")]
    [Trait("Requirement", "POP-PTR-22")]
    public void Renders_when_popover_is_closed()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { CloseOutside = true });

        Assert.Equal("button", cut.Find("[data-testid=\"close-outside\"]").TagName, ignoreCase: true);
    }

    [Fact(DisplayName = "should close popover when clicked")]
    [Trait("Requirement", "POP-PTR-22")]
    public async Task Should_close_popover_when_clicked()
    {
        List<(bool, EventReason)> calls = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            DefaultOpen = true,
            CloseInside = true,
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => calls.Add((args.Value, args.Details.Reason))),
        });

        await cut.Find("[data-testid=\"close\"]").ClickAsync(new());
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);

        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([(false, EventReason.ClosePress)], calls);
    }

    [Fact(DisplayName = "port: a disabled close button does not close")]
    [Trait("Requirement", "POP-API-25")]
    public async Task A_disabled_close_button_does_not_close()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<PopoverRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(PopoverRoot<NoPayload>.DefaultOpen), true);
            builder.AddComponentParameter(2, nameof(PopoverRoot<NoPayload>.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<PopoverClose>(0);
                b.AddComponentParameter(1, nameof(PopoverClose.Disabled), true);
                b.CloseComponent();
                b.OpenComponent<PopoverClose>(2);
                b.AddComponentParameter(3, nameof(PopoverClose.Disabled), true);
                b.AddComponentParameter(4, nameof(PopoverClose.NativeButton), false);
                b.AddComponentParameter(5, nameof(PopoverClose.Render), (RenderFragment)(r =>
                {
                    r.OpenElement(0, "span");
                    r.CloseElement();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        IElement native = cut.Find("button");
        Assert.True(native.HasAttribute("disabled"));
        IElement span = cut.Find("span");
        Assert.Equal("true", span.GetAttribute("aria-disabled"));
        Assert.Equal("-1", span.GetAttribute("tabindex"));

        await span.ClickAsync(new());
        Assert.True(cut.FindComponent<PopoverRoot<NoPayload>>().Instance.Store.IsOpen);
    }

    [Fact(DisplayName = "falls back to the active trigger element when the active trigger id is unregistered")]
    [Trait("Requirement", "POP-EVT-06")]
    [Trait("Requirement", "POP-STA-02")]
    public async Task Falls_back_to_the_active_trigger_element_when_the_active_trigger_id_is_unregistered()
    {
        List<OpenChangeEventDetails> calls = [];
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) => PopoverTree.Build(builder, new PopoverSpec
            {
                Open = true,
                TriggerIdSupplied = true,
                TriggerId = host.Get<string>("triggerId"),
                CloseInside = true,
                OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => calls.Add((OpenChangeEventDetails)args.Details)),
            }),
            new() { ["triggerId"] = "trigger-1" });
        ElementReference? triggerElement = cut.FindComponent<PopoverTrigger<int>>().Instance.Element;

        await cut.Instance.SetAsync("triggerId", "unregistered");
        await cut.Find("[data-testid=\"close\"]").ClickAsync(new());

        OpenChangeEventDetails details = Assert.Single(calls);
        Assert.Equal(EventReason.ClosePress, details.Reason);
        Assert.NotNull(details.Trigger);
        Assert.Equal("trigger-1", details.Trigger.Id);
        Assert.Equal(triggerElement?.Id, details.Trigger.Element?.Id);
    }

    [Fact(DisplayName = "reports no trigger when the active trigger id has no mounted trigger")]
    [Trait("Requirement", "POP-EVT-06")]
    [Trait("Requirement", "POP-API-08")]
    public async Task Reports_no_trigger_when_the_active_trigger_id_has_no_mounted_trigger()
    {
        List<OpenChangeEventDetails> calls = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            DefaultOpen = true,
            DefaultTriggerId = "never-mounted",
            Triggers = 0,
            CloseInside = true,
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => calls.Add((OpenChangeEventDetails)args.Details)),
        });

        await cut.Find("[data-testid=\"close\"]").ClickAsync(new());
        await PopoverTestContext.CompleteTransitionAsync<int>(cut, opened: false);

        Assert.Empty(cut.FindAll("[data-testid=\"content\"]"));
        OpenChangeEventDetails details = Assert.Single(calls);
        Assert.Equal(EventReason.ClosePress, details.Reason);
        Assert.Null(details.Trigger);
    }

    [Fact(DisplayName = "labels the popup element with its id")]
    [Trait("Requirement", "POP-STA-09")]
    [Trait("Requirement", "POP-DOM-42")]
    [Trait("Requirement", "POP-ARIA-05")]
    public void Labels_the_popup_element_with_its_id()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { DefaultOpen = true, Title = true });

        IElement title = cut.Find("[data-testid=\"title\"]");
        Assert.Equal("h2", title.TagName, ignoreCase: true);
        Assert.False(string.IsNullOrEmpty(title.Id));
        Assert.Equal(title.Id, cut.Find("[data-testid=\"popup\"]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "describes the popup element with its id")]
    [Trait("Requirement", "POP-STA-09")]
    [Trait("Requirement", "POP-DOM-42")]
    [Trait("Requirement", "POP-ARIA-06")]
    public void Describes_the_popup_element_with_its_id()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { DefaultOpen = true, Description = true });

        IElement description = cut.Find("[data-testid=\"description\"]");
        Assert.Equal("p", description.TagName, ignoreCase: true);
        Assert.False(string.IsNullOrEmpty(description.Id));
        Assert.Equal(description.Id, cut.Find("[data-testid=\"popup\"]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "port: the popup is a dialog with the floating id, an unmanaged tabindex and no label without a title")]
    [Trait("Requirement", "POP-ARIA-04")]
    [Trait("Requirement", "POP-DOM-23")]
    [Trait("Requirement", "POP-DOM-24")]
    [Trait("Requirement", "POP-DOM-25")]
    public async Task The_popup_is_a_dialog_with_the_floating_id()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec());

        await test.PressAsync(cut, "trigger-1", open: true);

        IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.Equal("dialog", popup.GetAttribute("role"));
        Assert.Equal("-1", popup.GetAttribute("tabindex"));
        Assert.True(popup.HasAttribute("data-base-ui-focusable"));
        Assert.True(popup.HasAttribute("data-open"));
        Assert.False(popup.HasAttribute("data-closed"));
        Assert.False(popup.HasAttribute("aria-labelledby"));
        Assert.False(popup.HasAttribute("aria-describedby"));
        Assert.Equal(cut.FindComponent<PopoverRoot<int>>().Instance.Store.Popup.FloatingId, popup.Id);
        Assert.Equal(popup.Id, cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-controls"));
    }

    [Theory(DisplayName = "does not start an entry phase when initially open (controlled=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "POP-STA-13")]
    [Trait("Requirement", "POP-DOM-36")]
    [Trait("Requirement", "POP-ANI-04")]
    public void Does_not_start_an_entry_phase_when_initially_open(bool controlled)
    {
        HashSet<TransitionStatus?> statuses = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            Open = controlled ? true : null,
            DefaultOpen = !controlled,
            Portal = false,
            Backdrop = true,
            BackdropClass = state =>
            {
                statuses.Add(state.TransitionStatus);
                return null;
            },
        });

        Assert.Equal([null], statuses);
        IElement backdrop = cut.Find("[data-testid=\"backdrop\"]");
        Assert.True(backdrop.HasAttribute("data-open"));
        Assert.False(backdrop.HasAttribute("data-starting-style"));
    }

    [Fact(DisplayName = "sets `pointer-events: none` style on backdrop if opened by hover")]
    [Trait("Requirement", "POP-DOM-35")]
    [Trait("Requirement", "POP-STA-06")]
    public async Task Sets_pointer_events_none_style_on_backdrop_if_opened_by_hover()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { OpenOnHover = true, Backdrop = true });

        await test.HoverAsync(cut, "trigger-1", open: true);

        Assert.Contains("pointer-events: none", cut.Find("[data-testid=\"backdrop\"]").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not set `pointer-events: none` style on backdrop if opened by click")]
    [Trait("Requirement", "POP-DOM-35")]
    [Trait("Requirement", "POP-DOM-33")]
    [Trait("Requirement", "POP-DOM-34")]
    public async Task Does_not_set_pointer_events_none_style_on_backdrop_if_opened_by_click()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { OpenOnHover = true, Backdrop = true });
        IElement closed = cut.Find("[data-testid=\"backdrop\"]");
        Assert.True(closed.HasAttribute("hidden"));
        Assert.Equal("presentation", closed.GetAttribute("role"));

        await test.PressAsync(cut, "trigger-1", open: true);

        IElement backdrop = cut.Find("[data-testid=\"backdrop\"]");
        string style = backdrop.GetAttribute("style") ?? string.Empty;
        Assert.Contains("user-select: none", style, StringComparison.Ordinal);
        Assert.DoesNotContain("pointer-events", style, StringComparison.Ordinal);
        Assert.False(backdrop.HasAttribute("hidden"));
    }

    private IRenderedComponent<PopupTestHost> Render(PopoverSpec spec) => test.Render((builder, _) => PopoverTree.Build(builder, spec));
}
