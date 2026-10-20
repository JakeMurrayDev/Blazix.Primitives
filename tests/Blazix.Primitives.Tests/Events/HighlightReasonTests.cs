namespace Blazix.Primitives.Tests.Events;

/// <summary><c>getHighlightReason</c> (EVD-ALG-11).</summary>
public sealed class HighlightReasonTests
{
    [Fact(DisplayName = "reports a pointer highlight after keyboard navigation")]
    [Trait("Requirement", "EVD-ALG-11")]
    public void Reports_a_pointer_highlight_after_keyboard_navigation()
    {
        Assert.Equal(EventReason.Keyboard, HighlightReason.From(new EventSnapshot { Kind = EventKind.KeyboardEvent, Type = "keydown" }));
        Assert.Equal(EventReason.Pointer, HighlightReason.From(new EventSnapshot { Kind = EventKind.PointerEvent, Type = "pointermove" }));
    }

    [Theory(DisplayName = "derives the highlight reason from the event type")]
    [Trait("Requirement", "EVD-ALG-11")]
    [InlineData("keyup", EventReason.Keyboard)]
    [InlineData("mousemove", EventReason.Pointer)]
    [InlineData("mouseenter", EventReason.Pointer)]
    [InlineData("pointerleave", EventReason.Pointer)]
    [InlineData("input", EventReason.None)]
    [InlineData("focus", EventReason.None)]
    [InlineData("base-ui", EventReason.None)]
    public void Derives_the_highlight_reason_from_the_event_type(string type, EventReason expected)
    {
        Assert.Equal(expected, HighlightReason.From(new EventSnapshot { Type = type }));
    }

    [Fact(DisplayName = "no event means no reason")]
    [Trait("Requirement", "EVD-ALG-11")]
    public void No_event_means_no_reason()
    {
        Assert.Equal(EventReason.None, HighlightReason.From(null));
    }
}
