namespace Blazix.Primitives;

/// <summary>
/// The cause of a request (EVD §3.5; glossary "Reason"): the 36 Base UI reasons in one enum (ADR 05 §3, named by ADR 08 §4).
/// Members are the PascalCase forms of the kebab-case literals, in the order of EVD §3.5; <see cref="EventReasonExtensions.ToLiteral"/>
/// gives the literal. Which reasons a callback may emit is documentation (EVD §4.3.7), not a type.
/// </summary>
public enum EventReason
{
    /// <summary><c>none</c>: no specific interaction, or a programmatic or automatic change.</summary>
    None,

    /// <summary><c>trigger-press</c>: the trigger was clicked or pressed.</summary>
    TriggerPress,

    /// <summary><c>trigger-hover</c>: the pointer entered or left a hover-enabled trigger or its popup.</summary>
    TriggerHover,

    /// <summary><c>trigger-focus</c>: the trigger received or lost keyboard focus.</summary>
    TriggerFocus,

    /// <summary><c>outside-press</c>: a press landed outside the popup and its triggers.</summary>
    OutsidePress,

    /// <summary><c>item-press</c>: a list item, menu item or option was activated.</summary>
    ItemPress,

    /// <summary><c>close-press</c>: a dedicated Close part was activated.</summary>
    ClosePress,

    /// <summary><c>link-press</c>: a navigation menu link with close-on-click was activated.</summary>
    LinkPress,

    /// <summary><c>clear-press</c>: a Clear part was activated.</summary>
    ClearPress,

    /// <summary><c>chip-remove-press</c>: a chip's remove button was activated.</summary>
    ChipRemovePress,

    /// <summary><c>track-press</c>: the slider control or track was pressed.</summary>
    TrackPress,

    /// <summary><c>increment-press</c>: the number field's increment button was pressed.</summary>
    IncrementPress,

    /// <summary><c>decrement-press</c>: the number field's decrement button was pressed.</summary>
    DecrementPress,

    /// <summary><c>input-change</c>: text was typed or a composition committed into a text input.</summary>
    InputChange,

    /// <summary><c>input-clear</c>: the text input became empty.</summary>
    InputClear,

    /// <summary><c>input-blur</c>: the number field input lost focus and its text was committed.</summary>
    InputBlur,

    /// <summary><c>input-paste</c>: text was pasted.</summary>
    InputPaste,

    /// <summary><c>input-press</c>: the combobox or autocomplete input was pressed to open.</summary>
    InputPress,

    /// <summary><c>focus-out</c>: focus left the popup.</summary>
    FocusOut,

    /// <summary><c>escape-key</c>: Escape was pressed.</summary>
    EscapeKey,

    /// <summary><c>close-watcher</c>: the platform close watcher requested a close.</summary>
    CloseWatcher,

    /// <summary><c>list-navigation</c>: an arrow key opened the popup or closed a submenu.</summary>
    ListNavigation,

    /// <summary><c>keyboard</c>: a keyboard interaction that is not list navigation.</summary>
    Keyboard,

    /// <summary><c>pointer</c>: a hover moved the highlight.</summary>
    Pointer,

    /// <summary><c>drag</c>: a slider thumb was dragged.</summary>
    Drag,

    /// <summary><c>wheel</c>: the mouse wheel changed a value.</summary>
    Wheel,

    /// <summary><c>scrub</c>: a scrub area drag changed the value.</summary>
    Scrub,

    /// <summary><c>popup-close</c>: the filter query is reset because its popup closed.</summary>
    PopupClose,

    /// <summary><c>cancel-open</c>: a press-open was aborted.</summary>
    CancelOpen,

    /// <summary><c>sibling-open</c>: a sibling submenu opened or the parent menu closed.</summary>
    SiblingOpen,

    /// <summary><c>disabled</c>: the component became disabled.</summary>
    Disabled,

    /// <summary><c>missing</c>: the selected tab was removed or never matched.</summary>
    Missing,

    /// <summary><c>initial</c>: the first automatic selection.</summary>
    Initial,

    /// <summary><c>imperative-action</c>: an imperative open or close through actions or a handle.</summary>
    ImperativeAction,

    /// <summary><c>swipe</c>: a drawer swipe gesture.</summary>
    Swipe,

    /// <summary><c>window-resize</c>: the window resized while a select popup was aligned to its trigger.</summary>
    WindowResize,
}

/// <summary>The literal form of <see cref="EventReason"/> (ADR 05 §3): a <c>switch</c>, no reflection.</summary>
public static class EventReasonExtensions
{
    /// <summary>The kebab-case Base UI literal of <paramref name="reason"/> (EVD §3.5), e.g. <c>trigger-press</c>.</summary>
    public static string ToLiteral(this EventReason reason)
    {
        return reason switch
        {
            EventReason.None => "none",
            EventReason.TriggerPress => "trigger-press",
            EventReason.TriggerHover => "trigger-hover",
            EventReason.TriggerFocus => "trigger-focus",
            EventReason.OutsidePress => "outside-press",
            EventReason.ItemPress => "item-press",
            EventReason.ClosePress => "close-press",
            EventReason.LinkPress => "link-press",
            EventReason.ClearPress => "clear-press",
            EventReason.ChipRemovePress => "chip-remove-press",
            EventReason.TrackPress => "track-press",
            EventReason.IncrementPress => "increment-press",
            EventReason.DecrementPress => "decrement-press",
            EventReason.InputChange => "input-change",
            EventReason.InputClear => "input-clear",
            EventReason.InputBlur => "input-blur",
            EventReason.InputPaste => "input-paste",
            EventReason.InputPress => "input-press",
            EventReason.FocusOut => "focus-out",
            EventReason.EscapeKey => "escape-key",
            EventReason.CloseWatcher => "close-watcher",
            EventReason.ListNavigation => "list-navigation",
            EventReason.Keyboard => "keyboard",
            EventReason.Pointer => "pointer",
            EventReason.Drag => "drag",
            EventReason.Wheel => "wheel",
            EventReason.Scrub => "scrub",
            EventReason.PopupClose => "popup-close",
            EventReason.CancelOpen => "cancel-open",
            EventReason.SiblingOpen => "sibling-open",
            EventReason.Disabled => "disabled",
            EventReason.Missing => "missing",
            EventReason.Initial => "initial",
            EventReason.ImperativeAction => "imperative-action",
            EventReason.Swipe => "swipe",
            EventReason.WindowResize => "window-resize",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };
    }
}
