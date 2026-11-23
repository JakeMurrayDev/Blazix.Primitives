using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Events;

/// <summary>
/// The snapshot C# builds from Blazor's event arguments (ADR 04 §3, Amendments 2026-10-21): the
/// <c>ChangeEventDetails.Event</c> of a request that a C# <c>onclick</c> or key handler starts (CollapsibleTrigger,
/// Toggle), and the inverse the button decider uses to hand a consumer's key handler the event JavaScript snapshotted.
/// What Blazor's arguments do not carry is absent: no target, never default-prevented, time stamp 0.
/// </summary>
public sealed class EventSnapshotFromEventArgsTests
{
    [Fact(DisplayName = "a mouse click keeps its type, detail, buttons and modifiers")]
    [Trait("Requirement", "EVD-API-04")]
    public void A_mouse_click_keeps_its_type_detail_buttons_and_modifiers()
    {
        EventSnapshot snapshot = EventSnapshot.FromEventArgs(new MouseEventArgs
        {
            Type = "click",
            Detail = 2,
            Buttons = 1,
            AltKey = true,
            CtrlKey = false,
            MetaKey = true,
            ShiftKey = true,
        });

        Assert.Equal(EventKind.MouseEvent, snapshot.Kind);
        Assert.Equal("click", snapshot.Type);
        Assert.Equal(2, snapshot.Detail);
        Assert.Equal(1, snapshot.Buttons);
        Assert.Equal((true, false, true, true), (snapshot.AltKey, snapshot.CtrlKey, snapshot.MetaKey, snapshot.ShiftKey));
        Assert.Null(snapshot.PointerType);
        Assert.Null(snapshot.Key);
        Assert.Null(snapshot.Code);
    }

    [Fact(DisplayName = "a keyboard-generated click keeps detail 0, so it still reads as keyboard activation")]
    [Trait("Requirement", "EVD-EVT-27")]
    public void A_keyboard_generated_click_keeps_detail_0()
    {
        EventSnapshot snapshot = EventSnapshot.FromEventArgs(new MouseEventArgs { Type = "click", Detail = 0 });

        Assert.Equal(0, snapshot.Detail);
    }

    [Fact(DisplayName = "pointer and wheel arguments give their own kind, and pointer arguments their pointer type")]
    [Trait("Requirement", "EVD-API-05")]
    public void Pointer_and_wheel_arguments_give_their_own_kind_and_pointer_arguments_their_pointer_type()
    {
        EventSnapshot pointer = EventSnapshot.FromEventArgs(new PointerEventArgs { Type = "pointerdown", PointerType = "pen", Buttons = 1 });
        EventSnapshot wheel = EventSnapshot.FromEventArgs(new WheelEventArgs { Type = "wheel" });

        Assert.Equal(EventKind.PointerEvent, pointer.Kind);
        Assert.Equal("pen", pointer.PointerType);
        Assert.True(pointer.IsInstanceOf(EventKind.MouseEvent));
        Assert.Equal(EventKind.WheelEvent, wheel.Kind);
        Assert.True(wheel.IsInstanceOf(EventKind.MouseEvent));
    }

    [Fact(DisplayName = "a key event keeps its type, key, code and modifiers")]
    [Trait("Requirement", "EVD-API-04")]
    public void A_key_event_keeps_its_type_key_code_and_modifiers()
    {
        EventSnapshot snapshot = EventSnapshot.FromEventArgs(new KeyboardEventArgs
        {
            Type = "keydown",
            Key = "Enter",
            Code = "NumpadEnter",
            CtrlKey = true,
            ShiftKey = true,
        });

        Assert.Equal(EventKind.KeyboardEvent, snapshot.Kind);
        Assert.Equal(("keydown", "Enter", "NumpadEnter"), (snapshot.Type, snapshot.Key, snapshot.Code));
        Assert.Equal((false, true, false, true), (snapshot.AltKey, snapshot.CtrlKey, snapshot.MetaKey, snapshot.ShiftKey));
        Assert.Equal(0, snapshot.Detail);
    }

    [Fact(DisplayName = "what Blazor's arguments do not carry is absent: no target, not default-prevented, time stamp 0")]
    [Trait("Requirement", "EVD-API-04")]
    public void What_Blazors_arguments_do_not_carry_is_absent()
    {
        EventSnapshot mouse = EventSnapshot.FromEventArgs(new MouseEventArgs { Type = "click" });
        EventSnapshot key = EventSnapshot.FromEventArgs(new KeyboardEventArgs { Type = "keyup", Key = " " });

        foreach (EventSnapshot snapshot in new[] { mouse, key })
        {
            Assert.Null(snapshot.Target);
            Assert.False(snapshot.DefaultPrevented);
            Assert.Equal(0, snapshot.TimeStamp);
            Assert.NotEqual(EventSnapshot.PlaceholderType, snapshot.Type);
        }
    }

    [Fact(DisplayName = "a key snapshot converts back to keyboard arguments with its type, key, code and modifiers")]
    [Trait("Requirement", "UBT-KEY-06")]
    public void A_key_snapshot_converts_back_to_keyboard_arguments()
    {
        EventSnapshot snapshot = new()
        {
            Kind = EventKind.KeyboardEvent,
            Type = "keyup",
            Key = " ",
            Code = "Space",
            AltKey = true,
            MetaKey = true,
            TimeStamp = 12.5,
        };

        KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();

        Assert.Equal(("keyup", " ", "Space"), (args.Type, args.Key, args.Code));
        Assert.Equal((true, false, true, false), (args.AltKey, args.CtrlKey, args.MetaKey, args.ShiftKey));
    }

    [Fact(DisplayName = "a snapshot without a key converts back to empty key and code")]
    [Trait("Requirement", "UBT-KEY-06")]
    public void A_snapshot_without_a_key_converts_back_to_empty_key_and_code()
    {
        KeyboardEventArgs args = new EventSnapshot { Kind = EventKind.KeyboardEvent, Type = "keydown" }.ToKeyboardEventArgs();

        Assert.Equal(("", ""), (args.Key, args.Code));
    }
}
