using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace (a test namespace is never named like a part).
namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: Menu's store over <see cref="MenuStore{TPayload}"/> alone: MNU-STA-01's sequence (a close while closed, the dedupe,
/// a trigger-less request), STA-03's keyboard open, STA-04's instant table, STA-05's touch guard, STA-06's and STA-07's
/// selectors, the parent kind (MNU-STR-19), STA-10's highlight reasons and API-63's handle.
/// </summary>
public sealed class MenuStoreTests
{
    private static EventSnapshot Click(int detail, string pointerType = "mouse") => new() { Kind = EventKind.PointerEvent, Type = "click", Detail = detail, PointerType = pointerType };

    private static EventSnapshot KeyDown(string key) => new() { Kind = EventKind.KeyboardEvent, Type = "keydown", Key = key };

    private static EventSnapshot Move() => new() { Kind = EventKind.MouseEvent, Type = "mousemove", PointerType = "mouse" };

    private static MenuStore<NoPayload> Store(TimeProvider? time = null)
    {
        MenuStore<NoPayload> store = new("popup", "token", timeProvider: time);
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        return store;
    }

    [Theory(DisplayName = "port: the instant type follows the parent kind, the reason and the press's detail")]
    [InlineData("Menubar", true, EventReason.TriggerFocus, 1, MenuInstantType.Group)]
    [InlineData("Menubar", false, EventReason.FocusOut, 1, MenuInstantType.Group)]
    [InlineData("Menubar", true, EventReason.TriggerHover, 1, MenuInstantType.Group)]
    [InlineData("Menubar", true, EventReason.ListNavigation, -1, MenuInstantType.Group)]
    [InlineData("Menubar", false, EventReason.SiblingOpen, 1, MenuInstantType.Group)]
    [InlineData("Menubar", true, EventReason.TriggerPress, 0, MenuInstantType.Click)]
    [InlineData("None", true, EventReason.TriggerPress, 0, MenuInstantType.Click)]
    [InlineData("None", false, EventReason.ItemPress, 0, MenuInstantType.Click)]
    [InlineData("None", true, EventReason.TriggerPress, 1, null)]
    [InlineData("None", false, EventReason.EscapeKey, -1, MenuInstantType.Dismiss)]
    [InlineData("Menu", false, EventReason.EscapeKey, -1, MenuInstantType.Dismiss)]
    [InlineData("None", true, EventReason.TriggerFocus, 1, null)]
    [InlineData("None", true, EventReason.ListNavigation, -1, null)]
    [InlineData("None", false, EventReason.OutsidePress, 1, null)]
    [InlineData("None", false, EventReason.FocusOut, -1, null)]
    [Trait("Requirement", "MNU-STA-04")]
    public void The_instant_type_follows_the_parent_kind_the_reason_and_the_press_detail(string parent, bool open, EventReason reason, int detail, MenuInstantType? expected)
    {
        EventSnapshot snapshot = detail < 0 ? KeyDown(reason == EventReason.EscapeKey ? "Escape" : "ArrowDown") : Click(detail);

        Assert.Equal(expected, MenuStore<NoPayload>.InstantTypeFor(Enum.Parse<MenuParentKind>(parent), open, new OpenChangeEventDetails(reason, snapshot)));
    }

    [Theory(DisplayName = "port: a keyboard open is a navigation key's, or a press whose click has no detail")]
    [InlineData(true, EventReason.ListNavigation, -1, true)]
    [InlineData(true, EventReason.TriggerPress, 0, true)]
    [InlineData(true, EventReason.ItemPress, 0, true)]
    [InlineData(true, EventReason.TriggerPress, 1, false)]
    [InlineData(true, EventReason.TriggerHover, 1, false)]
    [InlineData(false, EventReason.ListNavigation, -1, false)]
    [InlineData(false, EventReason.TriggerPress, 0, false)]
    [Trait("Requirement", "MNU-STA-03")]
    public void A_keyboard_open_is_a_navigation_key_or_a_press_with_no_detail(bool open, EventReason reason, int detail, bool expected)
    {
        EventSnapshot snapshot = detail < 0 ? KeyDown("ArrowDown") : Click(detail);

        Assert.Equal(expected, MenuStore<NoPayload>.IsKeyboardOpen(open, new OpenChangeEventDetails(reason, snapshot)));
    }

    [Fact(DisplayName = "closes with a `detail === 0` click event on keyboard item activation")]
    [Trait("Requirement", "MNU-STA-03")]
    public async Task Closes_with_a_detail_zero_click_on_keyboard_item_activation()
    {
        MenuStore<NoPayload> store = Store();
        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(0))));
        Assert.True(store.State.KeyboardOpen);
        Assert.Equal(EventReason.TriggerPress, store.State.OpenChangeReason);

        Assert.True(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ItemPress, Click(0))));

        Assert.False(store.State.KeyboardOpen);
        Assert.Equal(EventReason.ItemPress, store.State.OpenChangeReason);
        Assert.Equal(MenuInstantType.Click, store.State.InstantType);
    }

    [Fact(DisplayName = "port: a close requested while closed is dropped before the consumer's callback")]
    [Trait("Requirement", "MNU-STA-01")]
    [Trait("Requirement", "MNU-EVT-01")]
    public async Task A_close_while_closed_is_dropped()
    {
        MenuStore<NoPayload> store = Store();
        int calls = 0;
        store.OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, () => calls++);

        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1))));
        Assert.True(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.OutsidePress, Click(1))));
        bool applied = await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.SiblingOpen));

        Assert.False(applied);
        Assert.Equal(2, calls);
    }

    [Fact(DisplayName = "port: a request for the current state from the active trigger with the last reason is dropped")]
    [Trait("Requirement", "MNU-STA-01")]
    public async Task An_identical_request_is_dropped()
    {
        MenuStore<NoPayload> store = Store();
        int calls = 0;
        store.OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, () => calls++);
        EventTrigger trigger = new("trigger", null);

        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover, Move()) { Trigger = trigger }));
        bool repeated = await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover, Move()) { Trigger = trigger });
        bool pressed = await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1)) { Trigger = trigger });

        Assert.False(repeated);
        Assert.True(pressed);
        Assert.Equal(2, calls);
    }

    [Fact(DisplayName = "port: a trigger-less open request is not deduped onto a menu with no active trigger")]
    [Trait("Requirement", "MNU-STA-01")]
    public async Task A_trigger_less_request_is_not_deduped_onto_no_active_trigger()
    {
        MenuStore<NoPayload> store = Store();
        int calls = 0;
        store.OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, () => calls++);

        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ImperativeAction)));
        // Base UI compares `eventDetails.trigger === activeTriggerElement`, `undefined === null`: never equal.
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ImperativeAction));

        Assert.Equal(2, calls);
    }

    [Fact(DisplayName = "port: a touch click closing the menu within 300 ms of a trigger-focus open is dropped; an item press is not")]
    [Trait("Requirement", "MNU-STA-05")]
    public async Task A_touch_click_close_soon_after_a_focus_open_is_dropped()
    {
        FakeTimeProvider time = new();
        MenuStore<NoPayload> store = Store(time);
        int calls = 0;
        store.OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, () => calls++);
        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerFocus, new EventSnapshot { Kind = EventKind.FocusEvent, Type = "focus" })));

        time.Advance(TimeSpan.FromMilliseconds(299));
        bool dropped = await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1, "touch")));

        Assert.False(dropped);
        Assert.True(store.IsOpen);
        Assert.Equal(2, calls);

        time.Advance(TimeSpan.FromMilliseconds(2));
        Assert.True(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1, "touch"))));
        Assert.False(store.IsOpen);
    }

    [Fact(DisplayName = "port: an item press closes a touch-opened menu at once after a trigger-focus open")]
    [Trait("Requirement", "MNU-STA-05")]
    public async Task An_item_press_is_not_guarded()
    {
        FakeTimeProvider time = new();
        MenuStore<NoPayload> store = Store(time);
        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerFocus, new EventSnapshot { Kind = EventKind.FocusEvent, Type = "focus" })));

        Assert.True(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ItemPress, Click(1, "touch"))));
        Assert.False(store.IsOpen);
    }

    [Theory(DisplayName = "prop: modal")]
    [InlineData("None", null, true)]
    [InlineData("None", true, true)]
    [InlineData("None", false, false)]
    [InlineData("ContextMenu", null, true)]
    [InlineData("Menu", null, false)]
    [InlineData("Menu", true, false)]
    [InlineData("Menubar", true, false)]
    [Trait("Requirement", "MNU-STA-06")]
    public void Prop_modal(string parentName, bool? modal, bool expected)
    {
        MenuParentKind parent = Enum.Parse<MenuParentKind>(parentName);
        MenuStore<NoPayload> store = Store();
        store.SetParent(new MenuParent(parent, null, parent is MenuParentKind.Menubar or MenuParentKind.ContextMenu ? new TestParent(parent) : null));
        store.SetRootFields(disabled: false, modal, rootId: "root", highlightItemOnHover: true);

        Assert.Equal(expected, ((IMenuState)store.State).Modal);
    }

    [Fact(DisplayName = "treats hover-opened menus as modal after a click")]
    [Trait("Requirement", "MNU-STA-06")]
    public async Task Treats_hover_opened_menus_as_modal_after_a_click()
    {
        MenuStore<NoPayload> store = Store();
        EventTrigger trigger = new("trigger", null);
        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover, Move()) { Trigger = trigger }));
        Assert.True(((IMenuState)store.State).HoverOpened);

        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1)) { Trigger = trigger }));

        Assert.False(((IMenuState)store.State).HoverOpened);
        Assert.True(((IMenuState)store.State).Modal);
    }

    [Theory(DisplayName = "prop: disabled")]
    [InlineData("None", false, false, false)]
    [InlineData("None", true, false, true)]
    [InlineData("Menubar", false, true, true)]
    [InlineData("Menubar", true, false, true)]
    [InlineData("ContextMenu", false, true, false)]
    [Trait("Requirement", "MNU-STA-07")]
    public void Prop_disabled(string parentName, bool own, bool parentDisabled, bool expected)
    {
        MenuParentKind parent = Enum.Parse<MenuParentKind>(parentName);
        MenuStore<NoPayload> store = Store();
        store.SetParent(new MenuParent(parent, null, parent == MenuParentKind.None ? null : new TestParent(parent) { Disabled = parentDisabled }));
        store.SetRootFields(own, modal: null, rootId: "root", highlightItemOnHover: true);

        Assert.Equal(expected, ((IMenuState)store.State).Disabled);
    }

    [Fact(DisplayName = "port: the root id is the parent's, else the root's own")]
    [Trait("Requirement", "MNU-STA-08")]
    public void The_root_id_is_the_parents_else_the_roots_own()
    {
        MenuStore<NoPayload> store = Store();
        store.SetRootFields(disabled: false, modal: null, rootId: "own", highlightItemOnHover: true);
        Assert.Equal("own", ((IMenuState)store.State).RootId);

        store.SetParent(new MenuParent(MenuParentKind.Menubar, null, new TestParent(MenuParentKind.Menubar) { RootId = "bar" }));
        Assert.Equal("bar", ((IMenuState)store.State).RootId);
    }

    [Fact(DisplayName = "port: the trigger-change instant reads as none once closed")]
    [Trait("Requirement", "MNU-STA-04")]
    [Trait("Requirement", "TRN-ANI-29")]
    public async Task The_trigger_change_instant_reads_as_none_once_closed()
    {
        MenuStore<NoPayload> store = Store();
        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1))));
        store.CompleteTriggerChange();
        Assert.Equal(MenuInstantType.TriggerChange, ((IMenuState)store.State).PublicInstantType);

        Assert.True(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerHover, Move())));
        store.CompleteTriggerChange();

        Assert.NotEqual(MenuInstantType.TriggerChange, ((IMenuState)store.State).PublicInstantType);
    }

    [Theory(DisplayName = "port: the parent kind resolves a submenu, then a menubar, then a context menu with no enclosing menu")]
    [InlineData(true, true, "Menubar", "Menu")]
    [InlineData(false, true, "Menubar", "Menubar")]
    [InlineData(false, false, "Menubar", "Menubar")]
    [InlineData(false, false, "ContextMenu", "ContextMenu")]
    [InlineData(false, true, "ContextMenu", "None")]
    [InlineData(false, true, "None", "None")]
    [InlineData(false, false, "None", "None")]
    [Trait("Requirement", "MNU-STR-19")]
    public void The_parent_kind_resolves_in_base_ui_order(bool isSubmenu, bool enclosingMenu, string contextName, string expectedName)
    {
        MenuParentKind context = Enum.Parse<MenuParentKind>(contextName);
        MenuParentKind expected = Enum.Parse<MenuParentKind>(expectedName);
        MenuRootContext? enclosing = enclosingMenu ? new MenuStore<NoPayload>("parent", "parent-token").CreateContext("Menu") : null;
        IMenuParentContext? parent = context == MenuParentKind.None ? null : new TestParent(context);

        MenuParent resolved = MenuParent.Resolve(isSubmenu, enclosing, parent);

        Assert.Equal(expected, resolved.Kind);
        Assert.Equal(expected == MenuParentKind.Menu ? enclosing : null, resolved.Menu);
    }

    [Fact(DisplayName = "does not report a stale highlight reason for a later list change")]
    [Trait("Requirement", "MNU-STA-10")]
    [Trait("Requirement", "MNU-EVT-11")]
    public void Does_not_report_a_stale_highlight_reason_for_a_later_list_change()
    {
        MenuHighlightReporter reporter = new();

        MenuItemHighlightedEventArgs? keyed = reporter.Report(1, "item-1", KeyDown("ArrowDown"), "One");
        MenuItemHighlightedEventArgs? shifted = reporter.Report(0, "item-1", KeyDown("ArrowDown"), "One");
        MenuItemHighlightedEventArgs? unchanged = reporter.Report(0, "item-1", null, "One");
        MenuItemHighlightedEventArgs? cleared = reporter.Report(null, null, Move(), null);

        Assert.Equal(EventReason.Keyboard, keyed!.Value.Details.Reason);
        Assert.Equal("item-1", keyed.Value.ItemId);
        Assert.Equal("One", keyed.Value.Details.Label);
        Assert.Equal(EventReason.None, shifted!.Value.Details.Reason);
        Assert.Null(unchanged);
        Assert.Equal(EventReason.Pointer, cleared!.Value.Details.Reason);
        Assert.Null(cleared.Value.ItemId);
    }

    [Fact(DisplayName = "port: `new MenuHandle<TPayload>()` creates independent handles")]
    [Trait("Requirement", "MNU-API-63")]
    public void New_handles_are_independent()
    {
        MenuHandle<NoPayload> first = new();
        MenuHandle<NoPayload> second = new();

        Assert.NotSame(first.Store, second.Store);
        Assert.False(first.IsOpen);
        Assert.False(second.IsOpen);
    }

    private sealed class TestParent(MenuParentKind kind) : IMenuParentContext
    {
        public MenuParentKind Kind => kind;

        public bool Modal { get; init; } = true;

        public bool Disabled { get; init; }

        public string? RootId { get; init; }

        public Orientation Orientation => Orientation.Horizontal;

        public bool HasSubmenuOpen => false;

        public string? CutoutElementId => null;

        public ElementReference? RelayTarget => null;

        public bool KeepOpenOnCrossOrientationKey => kind == MenuParentKind.ContextMenu;

        public int OutsidePressGrace => kind == MenuParentKind.ContextMenu ? 500 : 0;

        public void SetBackdropElement(ElementReference? element)
        {
        }

        public void SetInternalBackdropElement(ElementReference? element)
        {
        }

        public void OnMenuOpenChange(MenuRootContext menu, bool open, EventReason reason)
        {
        }

        public IDisposable Subscribe(Action changed) => new Noop();

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
