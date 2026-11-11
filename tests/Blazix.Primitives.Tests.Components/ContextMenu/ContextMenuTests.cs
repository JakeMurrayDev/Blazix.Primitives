using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: ContextMenu's <c>[L2]</c> rows and the port titles of its <c>none</c> rows (the brief's Tests first), in bUnit's loose
/// JS interop. A gesture is played as the module reports it: <see cref="ContextMenuTwin"/>'s <c>OnOpen</c>,
/// <c>OnCancelOpen</c>, <c>OnPress</c> and the <c>press</c> decision on the Trigger's twin. What the module receives is read
/// from the recorded <c>attach</c> and <c>update</c> invocations: the Root's handle with its elements, and the positioning
/// options of the four <c>MenuPositioner.test.tsx</c> context-menu titles.
/// </summary>
public sealed class ContextMenuTests : BunitContext
{
    private const string ContextMenuEvent = """
        {"kind":"MouseEvent","type":"contextmenu","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":2,
         "target":{"id":null,"tagName":"DIV"},"defaultPrevented":true,"timeStamp":3}
        """;

    public ContextMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside ContextMenu.Root")]
    [Trait("Requirement", "CTX-STR-04")]
    [Trait("Requirement", "CTX-EDGE-01")]
    public void Throws_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<ContextMenuHost>(p => p.Add(x => x.Case, "trigger-alone")));

        Assert.Equal("Blazix: The `ContextMenuRoot` context is missing. ContextMenu parts must be placed within `<ContextMenuRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: a positioner outside ContextMenuPortal throws the portal error")]
    [Trait("Requirement", "CTX-EDGE-02")]
    public void A_positioner_outside_the_portal_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<ContextMenuHost>(p => p.Add(x => x.Case, "positioner-outside-portal")));

        Assert.Equal("Blazix: `<ContextMenuPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "adds open state attributes")]
    [Trait("Requirement", "CTX-DOM-01")]
    [Trait("Requirement", "CTX-STA-01")]
    [Trait("Requirement", "CTX-API-01")]
    public void Adds_open_state_attributes()
    {
        IRenderedComponent<ContextMenuHost> closed = Render<ContextMenuHost>();
        IElement closedTrigger = closed.Find("[data-testid=\"trigger\"]");
        Assert.False(closedTrigger.HasAttribute("data-popup-open"));
        Assert.False(closedTrigger.HasAttribute("data-pressed"));

        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true));

        IElement trigger = cut.Find("[data-testid=\"trigger\"]");
        Assert.Equal("", trigger.GetAttribute("data-popup-open"));
        Assert.Equal("", trigger.GetAttribute("data-pressed"));
    }

    [Fact(DisplayName = "port: the trigger is a div with the touch-callout style before the consumer's, a received id, and no role, tabindex or ARIA")]
    [Trait("Requirement", "CTX-DOM-02")]
    [Trait("Requirement", "CTX-ARIA-01")]
    [Trait("Requirement", "CTX-STR-08")]
    [Trait("Requirement", "CTX-KEY-01")]
    public void The_trigger_renders_the_touch_callout_style_and_nothing_else_of_its_own()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p
            .Add(x => x.TriggerAttributes, new Dictionary<string, object> { ["id"] = "area", ["style"] = "color: red" }));

        IElement trigger = cut.Find("[data-testid=\"trigger\"]");
        Assert.Equal("DIV", trigger.TagName);
        Assert.Equal("-webkit-touch-callout: none; color: red;", trigger.GetAttribute("style"));
        Assert.Equal("area", trigger.Id);
        Assert.Null(trigger.GetAttribute("role"));
        Assert.Null(trigger.GetAttribute("tabindex"));
        Assert.Null(trigger.GetAttribute("data-disabled"));
        Assert.DoesNotContain(trigger.Attributes, attribute => attribute.Name.StartsWith("aria-", StringComparison.Ordinal));
        // CTX-STR-08: no focus guards or other siblings.
        Assert.Equal("outside", trigger.PreviousElementSibling?.GetAttribute("data-testid"));
        Assert.Empty(cut.FindAll("[data-base-ui-focus-guard]"));
    }

    [Fact(DisplayName = "port: the trigger attaches its context-menu handle with the session token, the root id, the disabled flag and the consumer's handlers")]
    [Trait("Requirement", "CTX-PTR-01")]
    [Trait("Requirement", "CTX-API-06")]
    [Trait("Requirement", "CTX-STA-10")]
    public void The_trigger_attaches_its_handle()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.TriggerAttributes, new Dictionary<string, object> { ["ontouchmove"] = EventCallback.Factory.Create<TouchEventArgs>(this, () => { }) }));
        ContextMenuRoot root = cut.FindComponent<ContextMenuRoot>().Instance;

        cut.WaitForAssertion(() => Assert.NotEmpty(TriggerOptions()), TimeSpan.FromSeconds(2));
        string options = TriggerOptions().Last();

        Assert.Equal(
            $$"""{"part":"trigger","token":"{{root.Store.SessionToken}}","rootId":"{{root.Context.RootId}}","disabled":true,"handlers":["touchmove"]}""",
            options);
        // The declined handler is not rendered for Blazor's dispatch.
        Assert.Null(cut.Find("[data-testid=\"trigger\"]").GetAttribute("blazor:ontouchmove"));
    }

    [Fact(DisplayName = "port: the root attaches its handle by the session token and sends the backdrops and the root positioner")]
    [Trait("Requirement", "CTX-STA-04")]
    [Trait("Requirement", "CTX-PTR-03")]
    [Trait("Requirement", "CTX-EDGE-05")]
    public void The_root_attaches_its_handle_and_sends_its_elements()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.WithBackdrop, true));
        ContextMenuRoot root = cut.FindComponent<ContextMenuRoot>().Instance;

        cut.WaitForAssertion(() =>
        {
            JSRuntimeInvocation last = RootInvocations().Last();
            string json = (string)last.Arguments[last.Identifier == "attach" ? 1 : 0]!;
            Assert.Equal($$"""{"part":"root","token":"{{root.Store.SessionToken}}","backdrop":0,"internalBackdrop":1,"positioner":2}""", json);
            object?[] references = (object?[])last.Arguments[last.Identifier == "attach" ? 3 : 1]!;
            Assert.Equal(3, references.Length);
        }, TimeSpan.FromSeconds(2));
        Assert.Null(RootInvocations().First().Arguments[0]);
    }

    // MenuBackdrop.tsx:29-38 takes the ContextMenu context's backdrop ref whatever the menu's parent kind: a plain menu's
    // Backdrop inside the popup is the element the root's handle receives as the consumer backdrop.
    [Fact(DisplayName = "port: a plain menu's Backdrop inside the context menu's popup registers as the context menu's backdrop")]
    [Trait("Requirement", "CTX-EDGE-05")]
    [Trait("Requirement", "MNU-EDGE-15")]
    public void A_plain_menus_backdrop_registers_with_the_context_menu()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.PlainMenu, true).Add(x => x.PlainBackdrop, true));
        ContextMenuRoot root = cut.FindComponent<ContextMenuRoot>().Instance;
        string? id = cut.Find("[data-testid=\"plain-backdrop\"]").GetAttribute("blazor:elementReference");

        cut.WaitForAssertion(() =>
        {
            JSRuntimeInvocation last = RootInvocations().Last();
            string json = (string)last.Arguments[last.Identifier == "attach" ? 1 : 0]!;
            Assert.Equal($$"""{"part":"root","token":"{{root.Store.SessionToken}}","backdrop":0,"internalBackdrop":1,"positioner":2}""", json);
            object?[] references = (object?[])last.Arguments[last.Identifier == "attach" ? 3 : 1]!;
            Assert.Equal(id, Assert.IsType<ElementReference>(references[0]).Id);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact(DisplayName = "blocks native context menus on both internal and external backdrops")]
    [Trait("Requirement", "CTX-STA-05")]
    [Trait("Requirement", "CTX-DOM-04")]
    [Trait("Requirement", "CTX-EDGE-05")]
    public void Renders_the_internal_backdrop_before_the_root_positioner_with_no_cutout()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.WithBackdrop, true).Add(x => x.SubmenuOpen, true));

        IElement positioner = cut.Find("[data-testid=\"positioner\"]");
        IElement internalBackdrop = positioner.PreviousElementSibling!;
        Assert.Equal("presentation", internalBackdrop.GetAttribute("role"));
        Assert.True(internalBackdrop.HasAttribute("data-base-ui-inert"));
        Assert.DoesNotContain("clip-path", internalBackdrop.GetAttribute("style") ?? "", StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid=\"backdrop\"]"));
        // Submenus render none.
        Assert.Single(cut.FindAll("[role=\"presentation\"][data-base-ui-inert]"));
    }

    [Fact(DisplayName = "keeps the root menu open when the context-menu gesture ends in a portaled submenu")]
    [Trait("Requirement", "CTX-STA-10")]
    [Trait("Requirement", "CTX-DOM-03")]
    public void Every_popup_of_the_tree_renders_the_roots_id()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.SubmenuOpen, true));
        ContextMenuRoot root = cut.FindComponent<ContextMenuRoot>().Instance;

        Assert.NotNull(root.Context.RootId);
        Assert.Equal(root.Context.RootId, cut.Find("[data-testid=\"popup\"]").GetAttribute("data-rootownerid"));
        Assert.Equal(root.Context.RootId, cut.Find("[data-testid=\"submenu-popup\"]").GetAttribute("data-rootownerid"));
    }

    [Fact(DisplayName = "should handle nested context menus correctly")]
    [Trait("Requirement", "CTX-STR-03")]
    [Trait("Requirement", "CTX-STR-09")]
    public async Task Nested_context_menus_are_independent()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.Case, "nested"));
        IReadOnlyList<IRenderedComponent<ContextMenuRoot>> roots = cut.FindComponents<ContextMenuRoot>();
        ContextMenuRoot outer = roots[0].Instance;
        ContextMenuRoot inner = roots[1].Instance;
        ContextMenuTrigger innerTrigger = cut.FindComponents<ContextMenuTrigger>()[1].Instance;

        Assert.NotEqual(outer.Context.RootId, inner.Context.RootId);
        Assert.NotEqual(outer.Store.SessionToken, inner.Store.SessionToken);
        Assert.Equal(MenuParentKind.ContextMenu, inner.Store.State.Parent.Kind);
        Assert.Same(inner.Context, inner.Store.State.Parent.Context);

        await cut.InvokeAsync(() => innerTrigger.ModuleTwin.OnOpen($$"""{"x":5,"y":5,"pointerType":"mouse","event":{{ContextMenuEvent}}}"""));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"inner-menu\"]")), TimeSpan.FromSeconds(2));
        Assert.Empty(cut.FindAll("[data-testid=\"outer-menu\"]"));
        Assert.Equal(inner.Context.RootId, cut.Find("[data-testid=\"inner-menu\"]").GetAttribute("data-rootownerid"));
    }

    [Fact(DisplayName = "port: the trigger's open request reaches the menu with trigger-press, the contextmenu event and no trigger, and cancel-open closes it")]
    [Trait("Requirement", "CTX-EVT-02")]
    [Trait("Requirement", "CTX-EVT-03")]
    [Trait("Requirement", "CTX-EVT-05")]
    [Trait("Requirement", "CTX-STA-03")]
    [Trait("Requirement", "CTX-STA-09")]
    public async Task The_open_request_and_cancel_open_reach_the_menu()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>();
        ContextMenuTrigger trigger = cut.FindComponent<ContextMenuTrigger>().Instance;
        ContextMenuRoot root = cut.FindComponent<ContextMenuRoot>().Instance;
        string open = $$"""{"x":5,"y":5,"pointerType":"mouse","event":{{ContextMenuEvent}}}""";

        await cut.InvokeAsync(() => trigger.ModuleTwin.OnOpen(open));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")), TimeSpan.FromSeconds(2));
        // CTX-STA-03, PTR-14: an open request while open is not deduplicated.
        await cut.InvokeAsync(() => trigger.ModuleTwin.OnOpen(open));
        await cut.InvokeAsync(() => trigger.ModuleTwin.OnCancelOpen(ContextMenuEvent.Replace("\"contextmenu\"", "\"mouseup\"", StringComparison.Ordinal) is var mouseUp ? $$"""{"event":{{mouseUp}}}""" : ""));

        ContextMenuHost host = cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(3, host.OpenChanges.Count), TimeSpan.FromSeconds(2));
        Assert.Equal((true, EventReason.TriggerPress, "contextmenu"), host.OpenChanges[0]);
        Assert.Equal((true, EventReason.TriggerPress, "contextmenu"), host.OpenChanges[1]);
        Assert.Equal((false, EventReason.CancelOpen, "mouseup"), host.OpenChanges[2]);
        Assert.Null(root.Store.State.Popup.EffectiveActiveTriggerId);
        Assert.Null(root.Store.State.Popup.OpenMethod);
    }

    [Fact(DisplayName = "blocks the native context menu when onContextMenu skips the Base UI handler")]
    [Trait("Requirement", "CTX-API-06")]
    [Trait("Requirement", "EVD-EVT-26")]
    public async Task A_consumer_veto_skips_the_open_through_press_and_through_the_report()
    {
        List<MouseEventArgs> calls = [];
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.TriggerAttributes, new Dictionary<string, object>
        {
            ["oncontextmenu"] = EventCallback.Factory.Create<MouseEventArgs>(this, (MouseEventArgs e) =>
            {
                calls.Add(e);
                e.PreventPartHandler();
            }),
        }));
        ContextMenuTrigger trigger = cut.FindComponent<ContextMenuTrigger>().Instance;
        string press = $$"""{"clientX":40,"clientY":50,"screenX":0,"screenY":0,"pageX":40,"pageY":50,"offsetX":4,"offsetY":5,"button":2,"buttons":2,"touches":[],"targetTouches":[],"changedTouches":[],"event":{{ContextMenuEvent}}}""";
        string open = $$"""{"x":40,"y":50,"pointerType":"mouse","event":{{ContextMenuEvent}}}""";

        // WebAssembly: the hand-over, then press, whose verdict stops the module's handling.
        trigger.ModuleTwin.OnPressing(press);
        Assert.Equal("stop", trigger.ModuleTwin.Decide(ContextMenuTwin.PressPoint, ContextMenuEvent));
        // Server: the report precedes the open, which is refused.
        await cut.InvokeAsync(() => trigger.ModuleTwin.OnPress(press));
        await cut.InvokeAsync(() => trigger.ModuleTwin.OnOpen(open));

        Assert.Equal(2, calls.Count);
        Assert.Equal((40, 50, 2L, "contextmenu"), (calls[0].ClientX, calls[0].ClientY, calls[0].Button, calls[0].Type));
        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "port: a consumer handler that does not veto lets the open through on both modes")]
    [Trait("Requirement", "CTX-API-06")]
    public async Task A_consumer_handler_without_a_veto_lets_the_open_through()
    {
        List<TouchEventArgs> calls = [];
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.TriggerAttributes, new Dictionary<string, object>
        {
            ["ontouchstart"] = EventCallback.Factory.Create<TouchEventArgs>(this, (TouchEventArgs e) => calls.Add(e)),
        }));
        ContextMenuTrigger trigger = cut.FindComponent<ContextMenuTrigger>().Instance;
        string touchStart = ContextMenuEvent.Replace("\"contextmenu\"", "\"touchstart\"", StringComparison.Ordinal).Replace("MouseEvent", "TouchEvent", StringComparison.Ordinal);
        string press = $$"""{"clientX":0,"clientY":0,"screenX":0,"screenY":0,"pageX":0,"pageY":0,"offsetX":0,"offsetY":0,"button":0,"buttons":0,"touches":[{"identifier":7,"clientX":10,"clientY":20,"screenX":0,"screenY":0,"pageX":10,"pageY":20}],"targetTouches":[],"changedTouches":[],"event":{{touchStart}}}""";

        await cut.InvokeAsync(() => trigger.ModuleTwin.OnPress(press));
        await cut.InvokeAsync(() => trigger.ModuleTwin.OnOpen($$"""{"x":10,"y":20,"pointerType":"touch","event":{{touchStart}}}"""));

        TouchEventArgs call = Assert.Single(calls);
        Assert.Equal((7L, 10.0, 20.0), (Assert.Single(call.Touches).Identifier, call.Touches[0].ClientX, call.Touches[0].ClientY));
        cut.WaitForAssertion(() => Assert.Equal((true, EventReason.TriggerPress, "touchstart"), Assert.Single(cut.Instance.OpenChanges)), TimeSpan.FromSeconds(2));
    }

    [Fact(DisplayName = "uses the layout viewport for a root context menu")]
    [Trait("Requirement", "CTX-POS-03")]
    [Trait("Requirement", "CTX-API-12")]
    [Trait("Requirement", "CTX-API-11")]
    public void Uses_the_layout_viewport_for_a_root_context_menu()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true));

        string positioning = PositioningOptions(cut.FindComponent<ContextMenuRoot>().Instance.Store.SessionToken);
        Assert.Contains("\"positionMethod\":\"fixed\"", positioning, StringComparison.Ordinal);
        Assert.Contains("\"shift\":{\"crossAxis\":true,\"rootBoundary\":\"layoutViewport\"}", positioning, StringComparison.Ordinal);
        Assert.Contains("\"arrowPadding\":0", positioning, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "disables cross-axis shifting when side collision avoidance is flip")]
    [Trait("Requirement", "CTX-POS-03")]
    [Trait("Requirement", "CTX-API-13")]
    public void Disables_cross_axis_shifting_when_side_avoidance_is_flip()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.PositionerAttributes, new Dictionary<string, object> { ["CollisionAvoidance"] = new CollisionAvoidance(Side: CollisionAvoidanceMode.Flip) }));

        string positioning = PositioningOptions(cut.FindComponent<ContextMenuRoot>().Instance.Store.SessionToken);
        Assert.Contains("\"shift\":{\"crossAxis\":false,\"rootBoundary\":\"layoutViewport\"}", positioning, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "preserves explicit context-menu placement and offsets")]
    [Trait("Requirement", "CTX-POS-05")]
    [Trait("Requirement", "CTX-API-07")]
    [Trait("Requirement", "CTX-API-08")]
    [Trait("Requirement", "CTX-API-09")]
    public void Preserves_explicit_placement_and_offsets()
    {
        IRenderedComponent<ContextMenuHost> first = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true));
        string defaults = PositioningOptions(first.FindComponent<ContextMenuRoot>().Instance.Store.SessionToken);
        Assert.Contains("\"side\":\"bottom\"", defaults, StringComparison.Ordinal);
        Assert.Contains("\"align\":\"start\"", defaults, StringComparison.Ordinal);
        Assert.Contains("\"sideOffset\":-5", defaults, StringComparison.Ordinal);
        Assert.Contains("\"alignOffset\":2", defaults, StringComparison.Ordinal);

        IRenderedComponent<ContextMenuHost> second = Render<ContextMenuHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.PositionerAttributes, new Dictionary<string, object>
            {
                ["Side"] = Side.Top,
                ["Align"] = Align.End,
                ["SideOffset"] = (OffsetValue)8,
                ["AlignOffset"] = (OffsetValue)(-3),
            }));
        string explicitOptions = PositioningOptions(second.FindComponent<ContextMenuRoot>().Instance.Store.SessionToken);
        Assert.Contains("\"side\":\"top\"", explicitOptions, StringComparison.Ordinal);
        Assert.Contains("\"align\":\"end\"", explicitOptions, StringComparison.Ordinal);
        Assert.Contains("\"sideOffset\":8", explicitOptions, StringComparison.Ordinal);
        Assert.Contains("\"alignOffset\":-3", explicitOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "uses the visual viewport for a context menu submenu")]
    [Trait("Requirement", "CTX-POS-06")]
    [Trait("Requirement", "CTX-STR-07")]
    [Trait("Requirement", "CTX-API-12")]
    public void Uses_the_visual_viewport_for_a_submenu()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.SubmenuOpen, true).Add(x => x.PlainMenu, true));

        string submenu = PositioningOptions(cut.FindComponent<ContextMenuSubmenuRoot>().Instance.Store.SessionToken);
        Assert.Contains("\"positionMethod\":\"fixed\"", submenu, StringComparison.Ordinal);
        Assert.DoesNotContain("\"shift\"", submenu, StringComparison.Ordinal);
        // CTX-API-12: a plain menu inside the context menu is positioned with `fixed` too.
        string plain = PositioningOptions(cut.FindComponent<MenuRoot<NoPayload>>().Instance.Store.SessionToken);
        Assert.Contains("\"positionMethod\":\"fixed\"", plain, StringComparison.Ordinal);
    }

    // FloatingPortal.tsx renders the owner span with its guards, which a modal popup does not render: the always-modal root
    // portal renders none, so CTX-ARIA-05's root clause holds by absence (a dated note on the row).
    [Fact(DisplayName = "port: the root portal renders no owner span while modal, and a submenu portal's owner span has the group role")]
    [Trait("Requirement", "CTX-ARIA-05")]
    public void The_root_portal_renders_no_owner_span_and_a_submenus_has_the_group_role()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.SubmenuOpen, true));

        IElement owner = Assert.Single(cut.FindAll("span[aria-owns]"));
        Assert.Equal("group", owner.GetAttribute("role"));
        Assert.Equal(cut.Find("[data-testid=\"submenu-positioner\"]").ParentElement!.Id, owner.GetAttribute("aria-owns"));
    }

    [Fact(DisplayName = "port: the popup is a menu with no generated aria-labelledby, and a consumer's label is rendered as given")]
    [Trait("Requirement", "CTX-ARIA-02")]
    public void The_popup_has_no_generated_label()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true));
        IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.Equal("menu", popup.GetAttribute("role"));
        Assert.Null(popup.GetAttribute("aria-labelledby"));

        IRenderedComponent<ContextMenuHost> labelled = Render<ContextMenuHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.PopupAttributes, new Dictionary<string, object> { ["aria-labelledby"] = "heading" }));
        Assert.Equal("heading", labelled.Find("[data-testid=\"popup\"]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "port: ContextMenuSeparator renders the Separator attributes and needs no menu")]
    [Trait("Requirement", "CTX-STR-05")]
    [Trait("Requirement", "CTX-DOM-06")]
    [Trait("Requirement", "CTX-ARIA-03")]
    public void The_separator_needs_no_menu()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.Case, "separator-alone"));

        IElement separator = cut.Find("[data-testid=\"separator\"]");
        Assert.Equal("separator", separator.GetAttribute("role"));
        Assert.Equal("vertical", separator.GetAttribute("aria-orientation"));
        Assert.Equal("vertical", separator.GetAttribute("data-orientation"));
    }

    [Fact(DisplayName = "port: a Menu item inside ContextMenuPopup is an item of the context menu, as a ContextMenuItem is")]
    [Trait("Requirement", "CTX-STR-06")]
    public void A_menu_item_inside_the_context_menu_behaves_the_same()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(p => p.Add(x => x.DefaultOpen, true));

        IElement item = cut.Find("[data-testid=\"item\"]");
        IElement menuItem = cut.Find("[data-testid=\"menu-item\"]");
        Assert.Equal("menuitem", item.GetAttribute("role"));
        Assert.Equal("menuitem", menuItem.GetAttribute("role"));
        Assert.Equal("-1", item.GetAttribute("tabindex"));
        Assert.Equal("-1", menuItem.GetAttribute("tabindex"));
        Assert.Equal(item.GetAttribute("data-rootownerid"), menuItem.GetAttribute("data-rootownerid"));
    }

    [Fact(DisplayName = "port: a gesture open re-renders the trigger and the opened tree, not the content around the root")]
    [Trait("Requirement", "ADR16-2")]
    public async Task A_gesture_open_renders_only_the_trigger_and_the_tree()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>();
        ContextMenuTrigger trigger = cut.FindComponent<ContextMenuTrigger>().Instance;
        Dictionary<string, int> before = new(cut.Instance.Renders);

        await cut.InvokeAsync(() => trigger.ModuleTwin.OnOpen($$"""{"x":5,"y":5,"pointerType":"mouse","event":{{ContextMenuEvent}}}"""));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")), TimeSpan.FromSeconds(2));

        Dictionary<string, int> renders = cut.Instance.Renders;
        Assert.Equal(before["outside"], renders["outside"]);
        Assert.Equal(before["trigger"] + 1, renders["trigger"]);
        Assert.True(renders.GetValueOrDefault("popup") >= 1);
    }

    private IEnumerable<JSRuntimeInvocation> RootInvocations() =>
        JSInterop.Invocations.Where(invocation => invocation.Identifier is "attach" or "update"
            && invocation.Arguments.OfType<string>().Any(argument => argument.StartsWith("{\"part\":\"root\"", StringComparison.Ordinal)));

    private IEnumerable<string> TriggerOptions() =>
        JSInterop.Invocations.Where(invocation => invocation.Identifier is "attach" or "update")
            .SelectMany(invocation => invocation.Arguments.OfType<string>())
            .Where(argument => argument.StartsWith("{\"part\":\"trigger\"", StringComparison.Ordinal));

    // The latest positioning options sent for the menu of a session token: the positioner's attach or update naming it.
    private string PositioningOptions(string? token) =>
        JSInterop.Invocations
            .SelectMany(invocation => invocation.Arguments.OfType<string>())
            .Last(argument => argument.Contains("\"positionMethod\"", StringComparison.Ordinal) && argument.Contains($"\"sessionToken\":\"{token}\"", StringComparison.Ordinal));
}
