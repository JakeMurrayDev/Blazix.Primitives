using System.Reflection;
using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Popover;

/// <summary>
/// L1: the Popover store's own steps (POP-STA-07's instant type, POP-STA-12's selectors, POP-EVT-06's back-filled close
/// trigger, POP-STA-06's reason) and POP-EDGE-10's reflection test, over <see cref="PopoverStore{TPayload}"/> alone.
/// </summary>
public sealed class PopoverStoreTests
{
    private static EventSnapshot Click(int detail, string pointerType = "mouse") => new() { Kind = EventKind.PointerEvent, Type = "click", Detail = detail, PointerType = pointerType };

    private static EventSnapshot KeyDown(string key) => new() { Kind = EventKind.KeyboardEvent, Type = "keydown", Key = key };

    [Theory(DisplayName = "port: the instant type follows the reason and the press's detail")]
    [InlineData(true, EventReason.TriggerPress, 0, PopoverInstantType.Click)]
    [InlineData(false, EventReason.TriggerPress, 0, PopoverInstantType.Click)]
    [InlineData(true, EventReason.TriggerPress, 1, null)]
    [InlineData(false, EventReason.EscapeKey, -1, PopoverInstantType.Dismiss)]
    [InlineData(false, EventReason.FocusOut, -1, PopoverInstantType.Focus)]
    [InlineData(true, EventReason.TriggerHover, 1, null)]
    [InlineData(false, EventReason.OutsidePress, 1, null)]
    [InlineData(false, EventReason.ClosePress, 1, null)]
    [InlineData(false, EventReason.ImperativeAction, -1, null)]
    [Trait("Requirement", "POP-STA-07")]
    public void The_instant_type_follows_the_reason_and_the_press_detail(bool open, EventReason reason, int detail, PopoverInstantType? expected)
    {
        EventSnapshot snapshot = detail < 0 ? (reason == EventReason.EscapeKey ? KeyDown("Escape") : EventSnapshot.CreatePlaceholder()) : Click(detail);

        Assert.Equal(expected, PopoverStore<NoPayload>.InstantTypeFor(open, new OpenChangeEventDetails(reason, snapshot)));
    }

    [Fact(DisplayName = "port: an applied open change writes its reason and instant type in one write, and a canceled one writes nothing")]
    [Trait("Requirement", "POP-STA-06")]
    [Trait("Requirement", "POP-STA-07")]
    [Trait("Requirement", "POP-EVT-03")]
    public async Task An_applied_open_change_writes_its_reason_and_instant_type_in_one_write()
    {
        PopoverStore<NoPayload> store = new("popup", "token");
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        int writes = 0;
        using IDisposable subscription = store.Subscribe(_ => writes++);

        Assert.True(await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(0))));
        Assert.Equal(1, writes);
        Assert.Equal(EventReason.TriggerPress, store.State.OpenChangeReason);
        Assert.Equal(PopoverInstantType.Click, store.State.InstantType);

        store.OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => args.Details.Cancel());
        Assert.False(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.EscapeKey, KeyDown("Escape"))));
        Assert.Equal(1, writes);
        Assert.Equal(PopoverInstantType.Click, store.State.InstantType);
    }

    [Fact(DisplayName = "port: the stored trigger-change instant reads as none once closed, and is dropped when closed")]
    [Trait("Requirement", "POP-STA-07")]
    [Trait("Requirement", "POP-ANI-07")]
    public async Task The_trigger_change_instant_reads_as_none_once_closed()
    {
        PopoverStore<NoPayload> store = new("popup", "token");
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1)));

        store.CompleteTriggerChange();
        Assert.Equal(PopoverInstantType.TriggerChange, PopoverSelectors.InstantType(store.State));

        await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.OutsidePress, Click(1)));
        Assert.Null(PopoverSelectors.InstantType(store.State));

        store.CompleteTriggerChange();
        Assert.Null(store.State.InstantType);
    }

    [Fact(DisplayName = "port: the selectors of the triggers derive from the reason, the open method and the focus trap")]
    [Trait("Requirement", "POP-STA-12")]
    public async Task The_selectors_of_the_triggers_derive_from_the_reason_the_open_method_and_the_focus_trap()
    {
        PopoverStore<NoPayload> store = new("popup", "token");
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        store.RegisterTrigger("a", new PopupTriggerElement(new object()));
        store.SetOpenInteractionType(InteractionType.Touch);
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1, "touch"), new EventTrigger("a", null)));

        Assert.True(PopoverSelectors.IsPressOpenedByTrigger(store.State, "a"));
        Assert.False(PopoverSelectors.IsPressOpenedByTrigger(store.State, "b"));
        Assert.True(PopoverSelectors.IsTouchPressOpen(store.State, true));
        Assert.False(PopoverSelectors.IsTouchPressOpen(store.State, false));
        Assert.True(PopoverSelectors.HasTriggerFocusGuards(store.State, "a"));

        store.SetFocusManagerModal(true);
        Assert.False(PopoverSelectors.HasTriggerFocusGuards(store.State, "a"));

        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover, Click(1), new EventTrigger("a", null)));
        Assert.False(PopoverSelectors.IsPressOpenedByTrigger(store.State, "a"));
        Assert.False(PopoverSelectors.IsTouchPressOpen(store.State, true));
    }

    [Fact(DisplayName = "port: a close press without a trigger reports the registered active trigger")]
    [Trait("Requirement", "POP-EVT-06")]
    public async Task A_close_press_without_a_trigger_reports_the_registered_active_trigger()
    {
        PopoverStore<NoPayload> store = new("popup", "token");
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        ElementReference element = new("element-a");
        store.RegisterTrigger("a", new PopupTriggerElement(new object(), element));
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, Click(1), new EventTrigger("a", element)));
        EventTrigger? reported = null;
        store.OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => reported = args.Details.Trigger);

        await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, Click(1)));

        Assert.Equal(new EventTrigger("a", element), reported);
        Assert.Equal(EventReason.ClosePress, store.State.OpenChangeReason);
    }

    [Fact(DisplayName = "port: the positioner takes no KeepMounted parameter; the portal's is read")]
    [Trait("Requirement", "POP-EDGE-10")]
    public void The_positioner_takes_no_KeepMounted_parameter()
    {
        PropertyInfo? portal = typeof(PopoverPortal).GetProperty(nameof(PopoverPortal.KeepMounted));
        Assert.NotNull(portal?.GetCustomAttribute<ParameterAttribute>());

        Assert.Null(typeof(PopoverPositioner).GetProperty("KeepMounted"));
    }
}
