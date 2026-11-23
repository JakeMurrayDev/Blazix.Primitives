namespace Blazix.Primitives;

// EVD §3.5; glossary "Reason"; ADR 05 §3, named by ADR 08 §4; EVD §4.3.7
/// <summary>
/// Specifies the cause of a request: one member for each of Base UI's 36 reasons.
/// </summary>
/// <remarks>
/// Members are the PascalCase forms of Base UI's kebab-case literals, in Base UI's catalog order;
/// <see cref="EventReasonExtensions.ToLiteral"/> returns the literal. Which reasons a callback can report is stated on that
/// callback, not encoded in a type.
/// </remarks>
public enum EventReason
{
    /// <summary>The change had no specific interaction, or was programmatic or automatic (<c>none</c>).</summary>
    None,

    /// <summary>The trigger was clicked or pressed (<c>trigger-press</c>).</summary>
    TriggerPress,

    /// <summary>The pointer entered or left a hover-enabled trigger or its popup (<c>trigger-hover</c>).</summary>
    TriggerHover,

    /// <summary>The trigger received or lost keyboard focus (<c>trigger-focus</c>).</summary>
    TriggerFocus,

    /// <summary>A press landed outside the popup and its triggers (<c>outside-press</c>).</summary>
    OutsidePress,

    /// <summary>A list item, menu item or option was activated (<c>item-press</c>).</summary>
    ItemPress,

    /// <summary>A dedicated Close part was activated (<c>close-press</c>).</summary>
    ClosePress,

    /// <summary>A navigation menu link with close-on-click was activated (<c>link-press</c>).</summary>
    LinkPress,

    /// <summary>A Clear part was activated (<c>clear-press</c>).</summary>
    ClearPress,

    /// <summary>A chip's remove button was activated (<c>chip-remove-press</c>).</summary>
    ChipRemovePress,

    /// <summary>The slider control or track was pressed (<c>track-press</c>).</summary>
    TrackPress,

    /// <summary>The number field's increment button was pressed (<c>increment-press</c>).</summary>
    IncrementPress,

    /// <summary>The number field's decrement button was pressed (<c>decrement-press</c>).</summary>
    DecrementPress,

    /// <summary>Text was typed or a composition committed into a text input (<c>input-change</c>).</summary>
    InputChange,

    /// <summary>The text input became empty (<c>input-clear</c>).</summary>
    InputClear,

    /// <summary>The number field input lost focus and its text was committed (<c>input-blur</c>).</summary>
    InputBlur,

    /// <summary>Text was pasted (<c>input-paste</c>).</summary>
    InputPaste,

    /// <summary>The combobox or autocomplete input was pressed to open (<c>input-press</c>).</summary>
    InputPress,

    /// <summary>Focus left the popup (<c>focus-out</c>).</summary>
    FocusOut,

    /// <summary>Escape was pressed (<c>escape-key</c>).</summary>
    EscapeKey,

    /// <summary>The platform close watcher requested a close (<c>close-watcher</c>).</summary>
    CloseWatcher,

    /// <summary>An arrow key opened the popup or closed a submenu (<c>list-navigation</c>).</summary>
    ListNavigation,

    /// <summary>A keyboard interaction other than list navigation changed a value or a highlight (<c>keyboard</c>).</summary>
    Keyboard,

    /// <summary>A hover moved the highlight (<c>pointer</c>).</summary>
    Pointer,

    /// <summary>A slider thumb was dragged (<c>drag</c>).</summary>
    Drag,

    /// <summary>The mouse wheel changed a value (<c>wheel</c>).</summary>
    Wheel,

    /// <summary>A scrub area drag changed the value (<c>scrub</c>).</summary>
    Scrub,

    /// <summary>The filter query was reset because its popup closed (<c>popup-close</c>).</summary>
    PopupClose,

    /// <summary>A press-open was aborted (<c>cancel-open</c>).</summary>
    CancelOpen,

    /// <summary>A sibling submenu opened or the parent menu closed (<c>sibling-open</c>).</summary>
    SiblingOpen,

    /// <summary>The component became disabled (<c>disabled</c>).</summary>
    Disabled,

    /// <summary>The selected tab was removed or never matched (<c>missing</c>).</summary>
    Missing,

    /// <summary>A component made its first automatic selection (<c>initial</c>).</summary>
    Initial,

    /// <summary>Code opened or closed the popup through its actions or a handle (<c>imperative-action</c>).</summary>
    ImperativeAction,

    /// <summary>A drawer swipe gesture opened, closed or moved the drawer (<c>swipe</c>).</summary>
    Swipe,

    /// <summary>The window resized while a select popup was aligned to its trigger (<c>window-resize</c>).</summary>
    WindowResize,
}

// ADR 05 §3
/// <summary>
/// Provides the literal form of an <see cref="EventReason"/>.
/// </summary>
public static class EventReasonExtensions
{
    // EVD §3.5
    /// <summary>
    /// Returns Base UI's kebab-case literal for a reason.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The literal, for example <c>trigger-press</c> for <see cref="EventReason.TriggerPress"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a member of <see cref="EventReason"/>.</exception>
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
            // The value is formatted as an integer: passing the enum as the actual value would box it.
            _ => throw new ArgumentOutOfRangeException(nameof(reason), $"Blazix: `{(int)reason}` is not a member of `EventReason`."),
        };
    }
}
