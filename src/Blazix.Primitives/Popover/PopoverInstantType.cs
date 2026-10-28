namespace Blazix.Primitives;

// POP-STA-07, POP-ANI-07; popover/store/PopoverStore.ts:24 (`'dismiss' | 'click' | 'focus' | 'trigger-change'`). ADR 08
// Amendments 2026-10-27: the family's instant type is an enum beside its first consumer, named in ADR 08 §3's companion
// table by a dated line; the literals are Base UI's, rendered as data-instant.
/// <summary>Specifies why a popover's next transition is skipped: the value its parts render as <c>data-instant</c>.</summary>
public enum PopoverInstantType
{
    /// <summary>The trigger was activated by the keyboard or an assistive technology (<c>click</c>).</summary>
    Click,

    /// <summary>The popover was dismissed with the Escape key (<c>dismiss</c>).</summary>
    Dismiss,

    /// <summary>The popover closed because focus left it (<c>focus</c>).</summary>
    Focus,

    /// <summary>The popover moved to another trigger while open, and that move has finished (<c>trigger-change</c>).</summary>
    TriggerChange,
}

/// <summary>Provides the literals of <see cref="PopoverInstantType"/>.</summary>
internal static class PopoverInstantTypeLiterals
{
    /// <summary>Returns the literal Base UI renders for an instant type.</summary>
    /// <param name="type">The instant type.</param>
    /// <returns><c>click</c>, <c>dismiss</c>, <c>focus</c> or <c>trigger-change</c>.</returns>
    public static string ToLiteral(this PopoverInstantType type) => type switch
    {
        PopoverInstantType.Click => "click",
        PopoverInstantType.Dismiss => "dismiss",
        PopoverInstantType.Focus => "focus",
        _ => "trigger-change",
    };
}
