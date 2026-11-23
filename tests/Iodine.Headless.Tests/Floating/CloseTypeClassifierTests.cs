// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="CloseTypeClassifier"/> at L1: FloatingFocusManager.tsx's <c>getEventType</c> over the close event's
/// snapshot and the type the <c>focus-manager</c> module last recorded (FLT-FOC-10), one title per branch.
/// </summary>
public sealed class CloseTypeClassifierTests
{
    private static EventSnapshot Snapshot(EventKind kind, string type, string? pointerType = null, int detail = 0) =>
        new() { Kind = kind, Type = type, PointerType = pointerType, Detail = detail };

    [Theory(DisplayName = "a keyboard event closes with the keyboard type, whatever was recorded")]
    [InlineData(null)]
    [InlineData(InteractionType.Mouse)]
    public void A_keyboard_event_closes_with_the_keyboard_type_whatever_was_recorded(InteractionType? recorded)
    {
        Assert.Equal(InteractionType.Keyboard, CloseTypeClassifier.Classify(Snapshot(EventKind.KeyboardEvent, "keydown"), recorded));
    }

    [Theory(DisplayName = "a focus event closes with the recorded type, else the keyboard type")]
    [InlineData(InteractionType.Touch, InteractionType.Touch)]
    [InlineData(InteractionType.Mouse, InteractionType.Mouse)]
    [InlineData(null, InteractionType.Keyboard)]
    [InlineData(InteractionType.Unknown, InteractionType.Keyboard)]
    public void A_focus_event_closes_with_the_recorded_type_else_the_keyboard_type(InteractionType? recorded, InteractionType expected)
    {
        Assert.Equal(expected, CloseTypeClassifier.Classify(Snapshot(EventKind.FocusEvent, "focusout"), recorded));
    }

    [Theory(DisplayName = "a pointer event closes with its pointer type")]
    [InlineData("mouse", InteractionType.Mouse)]
    [InlineData("touch", InteractionType.Touch)]
    [InlineData("pen", InteractionType.Pen)]
    public void A_pointer_event_closes_with_its_pointer_type(string pointerType, InteractionType expected)
    {
        Assert.Equal(expected, CloseTypeClassifier.Classify(Snapshot(EventKind.PointerEvent, "click", pointerType, detail: 1), InteractionType.Keyboard));
    }

    [Theory(DisplayName = "a pointer event without a pointer type closes with the keyboard type for a virtual click, else the recorded type, else the mouse type")]
    [InlineData(0, null, InteractionType.Keyboard)]
    [InlineData(0, InteractionType.Touch, InteractionType.Keyboard)]
    [InlineData(1, InteractionType.Touch, InteractionType.Touch)]
    [InlineData(1, null, InteractionType.Mouse)]
    [InlineData(1, InteractionType.Unknown, InteractionType.Mouse)]
    public void A_pointer_event_without_a_pointer_type_closes_with_the_keyboard_type_for_a_virtual_click_else_the_recorded_type_else_the_mouse_type(
        int detail,
        InteractionType? recorded,
        InteractionType expected)
    {
        Assert.Equal(expected, CloseTypeClassifier.Classify(Snapshot(EventKind.PointerEvent, "click", "", detail), recorded));
        Assert.Equal(expected, CloseTypeClassifier.Classify(Snapshot(EventKind.PointerEvent, "click", null, detail), recorded));
    }

    [Fact(DisplayName = "a touch event closes with the touch type")]
    public void A_touch_event_closes_with_the_touch_type()
    {
        Assert.Equal(InteractionType.Touch, CloseTypeClassifier.Classify(Snapshot(EventKind.TouchEvent, "touchend"), InteractionType.Mouse));
    }

    [Theory(DisplayName = "another mouse event closes with the recorded type, else the keyboard type when detail is 0, else the mouse type")]
    [InlineData(EventKind.MouseEvent, 1, InteractionType.Pen, InteractionType.Pen)]
    [InlineData(EventKind.MouseEvent, 0, null, InteractionType.Keyboard)]
    [InlineData(EventKind.MouseEvent, 1, null, InteractionType.Mouse)]
    [InlineData(EventKind.MouseEvent, 1, InteractionType.Unknown, InteractionType.Mouse)]
    [InlineData(EventKind.WheelEvent, 1, null, InteractionType.Mouse)]
    public void Another_mouse_event_closes_with_the_recorded_type_else_the_keyboard_type_when_detail_is_0_else_the_mouse_type(
        EventKind kind,
        int detail,
        InteractionType? recorded,
        InteractionType expected)
    {
        Assert.Equal(expected, CloseTypeClassifier.Classify(Snapshot(kind, "click", detail: detail), recorded));
    }

    [Fact(DisplayName = "any other event, the placeholder of a close no event caused and a missing snapshot close with the empty type")]
    public void Any_other_event_the_placeholder_of_a_close_no_event_caused_and_a_missing_snapshot_close_with_the_empty_type()
    {
        Assert.Equal(InteractionType.Unknown, CloseTypeClassifier.Classify(Snapshot(EventKind.InputEvent, "input"), InteractionType.Mouse));
        Assert.Equal(InteractionType.Unknown, CloseTypeClassifier.Classify(EventSnapshot.CreatePlaceholder(), InteractionType.Mouse));
        Assert.Equal(InteractionType.Unknown, CloseTypeClassifier.Classify(null, InteractionType.Mouse));
    }
}
