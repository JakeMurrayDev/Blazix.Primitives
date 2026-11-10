namespace Blazix.Primitives;

// MNU-STA-04, MNU-ANI-07; menu/store/MenuStore.ts:43 (`'dismiss' | 'click' | 'group' | 'trigger-change'`). ADR 08
// Amendments 2026-10-27's per-family instant enums (the PopoverInstantType precedent): an enum beside its first consumer,
// named in ADR 08 §3's companion table by a dated line; the literals are Base UI's, rendered as data-instant.
/// <summary>Specifies why a menu's next transition is skipped: the value its parts render as <c>data-instant</c>.</summary>
public enum MenuInstantType
{
    /// <summary>The menu was dismissed with the Escape key, or closed without a reason (<c>dismiss</c>).</summary>
    Dismiss,

    /// <summary>The trigger or an item was activated by the keyboard or an assistive technology (<c>click</c>).</summary>
    Click,

    /// <summary>The menu opened or closed as one of a menubar's menus, moving between them (<c>group</c>).</summary>
    Group,

    /// <summary>The menu moved to another trigger while open, and that move has finished (<c>trigger-change</c>).</summary>
    TriggerChange,
}
