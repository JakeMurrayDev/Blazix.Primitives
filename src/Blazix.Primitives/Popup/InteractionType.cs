namespace Blazix.Primitives;

// ADR 08 §3 (enums) and Amendments 2026-10-27 (M4 planning: the enum, in Popup/ beside its first consumers; `Unknown`
// stands for Base UI's empty literal, the sentinel §3 allows where Base UI has the literal; `InteractionType?` is the
// closed state, Base UI's `null`). PSH §2 "open interaction type", PSH-PTR-01/02, useEnhancedClickHandler.ts.
/// <summary>Specifies how the user interacted with a popup's trigger or close control.</summary>
/// <remarks>A nullable <see cref="InteractionType"/> is <see langword="null"/> while the popup is closed.</remarks>
public enum InteractionType
{
    /// <summary>A mouse pointer.</summary>
    Mouse,

    /// <summary>A touch pointer.</summary>
    Touch,

    /// <summary>A pen pointer.</summary>
    Pen,

    /// <summary>The keyboard: a click whose <c>detail</c> is 0.</summary>
    Keyboard,

    /// <summary>A pointer whose type the browser did not report.</summary>
    Unknown,
}
