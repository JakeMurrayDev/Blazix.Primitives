using AngleSharp.Dom;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The toast root at L2 (TST §3.4, §5.2): the C#-rendered attributes, the id-based lookups of
/// <c>ToastRoot.test.tsx</c>'s "object identity" block, the high-priority and dialog titles of
/// <c>useToastManager.test.tsx</c>, and the root's <c>TData</c> inferred through its <c>Toast</c> parameter (ADR 08
/// Amendments 2026-11-18). The transition attributes, the measurement, the swipe and the removal are the module's (M6
/// track 9b).
/// </summary>
public sealed class ToastRootTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "works correctly when toast objects are recreated (not referentially equal)")]
    [Trait("Requirement", "TST-STR-03")]
    [Trait("Requirement", "TST-EDGE-10")]
    [Trait("Requirement", "TST-DOM-10")]
    public void Works_correctly_when_toast_objects_are_recreated_not_referentially_equal()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Recreate, true));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "test-toast", Title = "Test Title", Description = "Test Description" }));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Contains("--toast-index: 0;", root.GetAttribute("style"), StringComparison.Ordinal);

        // The close finds its toast by id. Its removal after the exit is the root's completion watcher (M6 track 9b).
        cut.Find("[data-testid=close]").Click();
        Assert.Equal(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
        Assert.Contains("--toast-index: 0;", cut.Find("[data-testid=root]").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "correctly calculates indices for multiple toasts with recreated objects")]
    [Trait("Requirement", "TST-STR-03")]
    [Trait("Requirement", "TST-EDGE-10")]
    [Trait("Requirement", "TST-DOM-10")]
    public void Correctly_calculates_indices_for_multiple_toasts_with_recreated_objects()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Recreate, true));

        test.Run(cut, () =>
        {
            test.Manager.Add(new ToastObject { Title = "Toast 1" });
            test.Manager.Add(new ToastObject { Title = "Toast 2" });
            test.Manager.Add(new ToastObject { Title = "Toast 3" });
        });

        IReadOnlyList<string?> styles = ToastTestContext.RootStyles(cut);
        Assert.Equal(["Toast 3", "Toast 2", "Toast 1"], cut.FindAll("[data-testid=title]").Select(title => title.TextContent));
        Assert.Equal(3, styles.Count);
        for (int i = 0; i < styles.Count; i++)
        {
            Assert.Contains($"--toast-index: {i};", styles[i], StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "port: a closing toast keeps its DOM index while the others shift up")]
    [Trait("Requirement", "TST-DOM-10")]
    public void A_closing_toast_keeps_its_DOM_index_while_the_others_shift_up()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () =>
        {
            test.Manager.Add(new ToastObject { Id = "c" });
            test.Manager.Add(new ToastObject { Id = "b" });
            test.Manager.Add(new ToastObject { Id = "a" });
        });

        test.Run(cut, () => test.Manager.Close("a"));

        // List order a, b, c: the closing a keeps DOM index 0; b and c are the visible 0 and 1.
        // M6 track 9b: after the attach the module writes the style the root sends (the handover).
        IReadOnlyList<string?> styles = ToastTestContext.RootStyles(cut);
        Assert.Contains("--toast-index: 0;", styles[0], StringComparison.Ordinal);
        Assert.Contains("--toast-index: 0;", styles[1], StringComparison.Ordinal);
        Assert.Contains("--toast-index: 1;", styles[2], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "renders the type")]
    [Trait("Requirement", "TST-DOM-06")]
    [Trait("Requirement", "TST-DOM-17")]
    [Trait("Requirement", "TST-DOM-18")]
    public void Renders_the_type()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title", Description = "description", Type = "success", ActionContent = Text("undo") }));

        Assert.Equal("success", cut.Find("[data-testid=root]").GetAttribute("data-type"));
        Assert.Equal("success", cut.Find("[data-testid=title]").GetAttribute("data-type"));
        Assert.Equal("success", cut.Find("[data-testid=description]").GetAttribute("data-type"));
        Assert.Equal("success", cut.Find("[data-testid=action]").GetAttribute("data-type"));
        Assert.Equal("success", cut.Find("[data-testid=close]").GetAttribute("data-type"));
    }

    [Fact(DisplayName = "applies correct ARIA attributes for high priority toasts")]
    [Trait("Requirement", "TST-ARIA-02")]
    [Trait("Requirement", "TST-ARIA-03")]
    [Trait("Requirement", "TST-ARIA-06")]
    [Trait("Requirement", "TST-DOM-05")]
    [Trait("Requirement", "TST-STR-12")]
    [Trait("Requirement", "TST-A11Y-01")]
    public void Applies_correct_ARIA_attributes_for_high_priority_toasts()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "high", Title = "Alert title", Description = "Alert description", Priority = ToastPriority.High }));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("alertdialog", root.GetAttribute("role"));
        Assert.Equal("false", root.GetAttribute("aria-modal"));
        Assert.Equal("true", root.GetAttribute("aria-hidden"));

        // The hidden alert region follows the viewport: one alert per high-priority toast, its title and description text.
        IElement alert = cut.Find("[role=alert]");
        Assert.Equal("true", alert.GetAttribute("aria-atomic"));
        Assert.Equal(["Alert title", "Alert description"], alert.Children.Select(child => child.TextContent));
        Assert.Same(cut.Find("[data-testid=viewport]").NextElementSibling, alert.ParentElement);
        Assert.Contains("clip-path: inset(50%)", alert.ParentElement!.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a low-priority toast renders the dialog role and no alert")]
    [Trait("Requirement", "TST-ARIA-02")]
    [Trait("Requirement", "TST-ARIA-06")]
    [Trait("Requirement", "TST-DOM-05")]
    public void A_low_priority_toast_renders_the_dialog_role_and_no_alert()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title", Priority = ToastPriority.Low }));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("dialog", root.GetAttribute("role"));
        Assert.False(root.HasAttribute("aria-hidden"));
        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact(DisplayName = "port: the alert region is removed while the viewport is focused and still announces a closing toast")]
    [Trait("Requirement", "TST-A11Y-03")]
    [Trait("Requirement", "TST-DOM-05")]
    [Trait("Requirement", "TST-ARIA-06")]
    public void The_alert_region_is_removed_while_the_viewport_is_focused_and_still_announces_a_closing_toast()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "high", Title = "Alert", Priority = ToastPriority.High }));
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;

        // The keyboard focus the module reports (M6 track 9b).
        test.Run(cut, () => store.SetFocused(true));
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-hidden"));

        test.Run(cut, () => store.SetFocused(false));
        Assert.Single(cut.FindAll("[role=alert]"));

        // TST §16 Q7, replicated: the list includes ending toasts, so a closing high-priority toast stays announced.
        test.Run(cut, () => test.Manager.Close("high"));
        Assert.Single(cut.FindAll("[role=alert]"));
    }

    [Fact(DisplayName = "toasts in dialogs are accessible and not aria-hidden")]
    [Trait("Requirement", "TST-EDGE-12")]
    [Trait("Requirement", "TST-ARIA-02")]
    public void Toasts_in_dialogs_are_accessible_and_not_aria_hidden()
    {
        IRenderedComponent<ToastDialogHost> cut = test.Context.Render<ToastDialogHost>();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "In dialog" }));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("dialog", root.GetAttribute("role"));
        Assert.False(root.HasAttribute("aria-hidden"));
        Assert.NotNull(cut.Find("[data-testid=dialog-popup]"));
    }

    [Fact(DisplayName = "high priority toasts in dialogs have correct accessibility structure")]
    [Trait("Requirement", "TST-EDGE-12")]
    [Trait("Requirement", "TST-ARIA-06")]
    [Trait("Requirement", "TST-DOM-05")]
    public void High_priority_toasts_in_dialogs_have_correct_accessibility_structure()
    {
        IRenderedComponent<ToastDialogHost> cut = test.Context.Render<ToastDialogHost>();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "Urgent", Description = "Now", Priority = ToastPriority.High }));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("alertdialog", root.GetAttribute("role"));
        Assert.Equal("true", root.GetAttribute("aria-hidden"));
        Assert.Equal(["Urgent", "Now"], cut.Find("[role=alert]").Children.Select(child => child.TextContent));
    }

    [Fact(DisplayName = "port: the root infers its data type through its Toast parameter, a ToastObject included")]
    [Trait("Requirement", "TST-API-13")]
    public void The_root_infers_its_data_type_through_its_Toast_parameter_a_ToastObject_included()
    {
        // ADR 08 Amendments 2026-11-18 (the Toast generic surface, chosen by the user): <ToastRoot Toast="…"> with no TData
        // compiles for a ToastObject (through its base class) and for a ToastObject<TData>; ToastInferenceHost is the proof.
        IRenderedComponent<ToastInferenceHost> cut = test.Context.Render<ToastInferenceHost>(p => p.Add(x => x.Toast, new ToastObject { Id = "plain", Title = "plain" }));

        Assert.IsType<ToastRoot<NoPayload>>(cut.FindComponent<ToastRoot<NoPayload>>().Instance);
        Assert.Equal("plain", cut.Find("h2").TextContent);
    }

    [Fact(DisplayName = "port: the root's state carries the swipe fields, at rest unset, and C# renders neither swipe attribute")]
    [Trait("Requirement", "TST-STA-26")]
    [Trait("Requirement", "TST-DOM-06")]
    public void The_roots_state_carries_the_swipe_fields_at_rest_unset_and_CSharp_renders_neither_swipe_attribute()
    {
        List<ToastRootState> states = [];
        IRenderedComponent<ToastProvider<NoPayload>> cut = test.Context.Render<ToastProvider<NoPayload>>(p => p.AddChildContent<ToastViewport<NoPayload>>(v => v
            .AddChildContent<ToastRoot<NoPayload>>(r => r
                .Add(x => x.Toast, new ToastObject { Id = "a", Title = "a" })
                .Add(x => x.ClassValue, state =>
                {
                    states.Add(state);
                    return "root";
                }))));

        ToastRootState state = Assert.Single(states.Distinct());
        Assert.False(state.Swiping);
        Assert.Null(state.SwipeDirection);
        IElement root = cut.Find(".root");
        Assert.False(root.HasAttribute("data-swiping"));
        Assert.False(root.HasAttribute("data-swipe-direction"));
    }

    [Fact(DisplayName = "port: the swipe directions default to down then right, keep the order given, and an anchored toast has none")]
    [Trait("Requirement", "TST-API-14")]
    [Trait("Requirement", "TST-PTR-22")]
    [Trait("Requirement", "TST-I18N-01")]
    public void The_swipe_directions_default_to_down_then_right_keep_the_order_given_and_an_anchored_toast_has_none()
    {
        ToastRoot<NoPayload> RenderRoot(ToastObject toast, IReadOnlyList<SwipeDirection>? directions)
        {
            IRenderedComponent<ToastProvider<NoPayload>> cut = test.Context.Render<ToastProvider<NoPayload>>(p => p.AddChildContent<ToastViewport<NoPayload>>(v => v
                .AddChildContent<ToastRoot<NoPayload>>(r => r.Add(x => x.Toast, toast).Add(x => x.SwipeDirections, directions))));
            return cut.FindComponent<ToastRoot<NoPayload>>().Instance;
        }

        Assert.Equal([SwipeDirection.Down, SwipeDirection.Right], RenderRoot(new ToastObject { Id = "a" }, null).EffectiveSwipeDirections);
        Assert.Equal([SwipeDirection.Left, SwipeDirection.Up], RenderRoot(new ToastObject { Id = "b" }, [SwipeDirection.Left, SwipeDirection.Up]).EffectiveSwipeDirections);

        // An anchor set even to none disables swiping.
        Assert.Empty(RenderRoot(new ToastObject { Id = "c", Positioner = new ToastPositionerSettings { Anchor = null } }, null).EffectiveSwipeDirections);
        Assert.Equal([SwipeDirection.Down, SwipeDirection.Right], RenderRoot(new ToastObject { Id = "d", Positioner = new ToastPositionerSettings { Side = Side.Top } }, null).EffectiveSwipeDirections);
    }

    private static Microsoft.AspNetCore.Components.RenderFragment Text(string text) => builder => builder.AddContent(0, text);
}
