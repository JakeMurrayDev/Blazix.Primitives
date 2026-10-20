using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Events;

/// <summary>
/// The details objects (EVD §3.1–3.3, §3.7; EVD-ALG-01/02, EVD-API-01/02/04..09; ADR 05 §3). A details object is a plain
/// class, so its creation is tested here and its use by parts at L2.
/// </summary>
public sealed class ChangeEventDetailsTests
{
    private static readonly EventSnapshot s_click = new() { Kind = EventKind.PointerEvent, Type = "click", PointerType = "mouse", Detail = 1 };

    [Fact(DisplayName = "calls onOpenChange with eventDetails")]
    [Trait("Requirement", "EVD-ALG-01")]
    [Trait("Requirement", "EVD-API-01")]
    public void Calls_onOpenChange_with_eventDetails()
    {
        ChangeEventDetails details = new(EventReason.TriggerPress, s_click);

        Assert.Equal(EventReason.TriggerPress, details.Reason);
        Assert.Same(s_click, details.Event);
        Assert.True(details.Event.IsInstanceOf(EventKind.MouseEvent));
        Assert.False(details.IsCanceled);
        Assert.False(details.IsPropagationAllowed);
        Assert.Null(details.Trigger);
    }

    [Fact(DisplayName = "a request with no native event carries a fresh base-ui placeholder")]
    [Trait("Requirement", "EVD-API-04")]
    public void A_request_with_no_native_event_carries_a_fresh_base_ui_placeholder()
    {
        ChangeEventDetails first = new(EventReason.None);
        ChangeEventDetails second = new(EventReason.None);

        Assert.Equal("base-ui", first.Event.Type);
        Assert.Equal(EventKind.Event, first.Event.Kind);
        Assert.True(first.Event.IsInstanceOf(EventKind.Event));
        Assert.NotSame(first.Event, second.Event);
    }

    [Fact(DisplayName = "isCanceled and isPropagationAllowed are live")]
    [Trait("Requirement", "EVD-API-06")]
    public void IsCanceled_and_isPropagationAllowed_are_live()
    {
        ChangeEventDetails details = new(EventReason.EscapeKey);
        Func<bool> canceled = () => details.IsCanceled;
        Func<bool> propagationAllowed = () => details.IsPropagationAllowed;
        Assert.False(canceled());
        Assert.False(propagationAllowed());

        details.Cancel();
        Assert.True(canceled());
        Assert.False(propagationAllowed());

        details.AllowPropagation();
        Assert.True(propagationAllowed());
    }

    [Fact(DisplayName = "the trigger carries the id and the element reference")]
    [Trait("Requirement", "EVD-API-07")]
    public void The_trigger_carries_the_id_and_the_element_reference()
    {
        ElementReference element = new("element-1");
        EventTrigger trigger = new("trigger-1", element);

        ChangeEventDetails details = new(EventReason.TriggerPress, s_click, trigger);

        Assert.Same(trigger, details.Trigger);
        Assert.Equal("trigger-1", details.Trigger!.Id);
        Assert.Equal(element.Id, details.Trigger.Element!.Value.Id);
    }

    [Fact(DisplayName = "open-change details record preventUnmountOnClose()")]
    [Trait("Requirement", "EVD-API-09")]
    public void Open_change_details_record_preventUnmountOnClose()
    {
        OpenChangeEventDetails details = new(EventReason.ClosePress);
        Assert.False(details.UnmountOnClosePrevented);

        details.PreventUnmountOnClose();

        Assert.True(details.UnmountOnClosePrevented);
        Assert.False(details.IsCanceled);
        Assert.IsAssignableFrom<ChangeEventDetails>(details);
    }

    [Fact(DisplayName = "generic details carry the reason literal passed and no cancel")]
    [Trait("Requirement", "EVD-ALG-02")]
    [Trait("Requirement", "EVD-API-02")]
    public void Generic_details_carry_the_reason_literal_passed_and_no_cancel()
    {
        GenericEventDetails incrementDetails = new(EventReason.IncrementPress);
        GenericEventDetails keyboardDetails = new(EventReason.Keyboard, new EventSnapshot { Kind = EventKind.KeyboardEvent, Type = "keydown", Key = "ArrowUp" });

        Assert.Equal(EventReason.IncrementPress, incrementDetails.Reason);
        Assert.Equal("base-ui", incrementDetails.Event.Type);
        Assert.Equal(EventReason.Keyboard, keyboardDetails.Reason);
        Assert.Equal("ArrowUp", keyboardDetails.Event.Key);
        Assert.False(typeof(ChangeEventDetails).IsAssignableFrom(typeof(GenericEventDetails)));
        Assert.Null(typeof(GenericEventDetails).GetMethod(nameof(ChangeEventDetails.Cancel)));
        Assert.Null(typeof(GenericEventDetails).GetProperty(nameof(ChangeEventDetails.Trigger)));
    }

    [Fact(DisplayName = "component details carry their extensions")]
    [Trait("Requirement", "EVD-API-08")]
    public void Component_details_carry_their_extensions()
    {
        TabsChangeEventDetails tabs = new(EventReason.None, TabsActivationDirection.None);
        SliderChangeEventDetails slider = new(EventReason.Drag, 2);
        HighlightEventDetails highlight = new(EventReason.Pointer, -1);

        Assert.Equal(TabsActivationDirection.None, tabs.ActivationDirection);
        Assert.Equal(2, slider.ActiveThumbIndex);
        Assert.Equal(-1, highlight.Index);
        Assert.Equal(EventReason.Pointer, highlight.Reason);
        Assert.IsAssignableFrom<ChangeEventDetails>(tabs);
        Assert.IsAssignableFrom<ChangeEventDetails>(slider);
        Assert.IsAssignableFrom<GenericEventDetails>(highlight);
    }

    [Fact(DisplayName = "ValueChangeEventArgs carries the value and the details object itself")]
    [Trait("Requirement", "EVD-EVT-04")]
    public void ValueChangeEventArgs_carries_the_value_and_the_details_object_itself()
    {
        ChangeEventDetails details = new(EventReason.None);

        ValueChangeEventArgs<bool> args = new(true, details);

        Assert.True(args.Value);
        Assert.Same(details, args.Details);
        Assert.True(typeof(ValueChangeEventArgs<bool>).IsValueType);
    }
}
