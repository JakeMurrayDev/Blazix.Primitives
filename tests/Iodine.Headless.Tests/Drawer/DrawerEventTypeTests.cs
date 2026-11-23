using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Iodine.Headless.Tests;

/// <summary>
/// DRW-EVT-02's type-level row (<c>DrawerRoot.spec.tsx</c>) at L1. TypeScript narrows a swipe change's event to
/// <c>PointerEvent | TouchEvent</c> by its reason; C#'s details carry one <see cref="EventSnapshot"/> type for every reason, so
/// the port asserts the shape the root's callback receives and that the two kinds a swipe snapshot takes are pointer and
/// touch events and never keyboard events.
/// </summary>
public sealed class DrawerEventTypeTests
{
    [Fact(DisplayName = "port: a drawer's open change carries an event snapshot, and a swipe's is a pointer or a touch event, never a keyboard event")]
    [Trait("Requirement", "DRW-EVT-02")]
    public void A_drawers_open_change_carries_an_event_snapshot()
    {
        Assert.Equal(typeof(EventCallback<ValueChangeEventArgs<bool>>), typeof(DrawerRoot<int>).GetProperty(nameof(DrawerRoot<int>.OnOpenChange))?.PropertyType);
        Assert.Equal(typeof(ChangeEventDetails), typeof(ValueChangeEventArgs<bool>).GetProperty(nameof(ValueChangeEventArgs<bool>.Details))?.PropertyType);
        Assert.Equal(typeof(EventSnapshot), typeof(ChangeEventDetails).GetProperty(nameof(ChangeEventDetails.Event))?.PropertyType);

        EventSnapshot pointer = new() { Kind = EventKind.PointerEvent, Type = "pointerup" };
        EventSnapshot touch = new() { Kind = EventKind.TouchEvent, Type = "touchend" };
        Assert.True(pointer.IsInstanceOf(EventKind.PointerEvent));
        Assert.True(touch.IsInstanceOf(EventKind.TouchEvent));
        Assert.False(pointer.IsInstanceOf(EventKind.KeyboardEvent));
        Assert.False(touch.IsInstanceOf(EventKind.KeyboardEvent));
        Assert.Equal(EventReason.Swipe, new ChangeEventDetails(EventReason.Swipe, pointer, null).Reason);
    }
}
