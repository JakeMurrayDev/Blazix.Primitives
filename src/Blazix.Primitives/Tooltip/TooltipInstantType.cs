namespace Blazix.Primitives;

// TIP §4.1 (instantType), TIP-STA-05/06/12; ADR 08 Amendments 2026-10-27 (the per-family instant enums, chosen by the user
// 2026-10-27). Rendered by C# as data-instant (ADR 04 Amendments 2026-10-27; TIP §16 Q2's resolution), in Base UI's
// kebab-case literals.
/// <summary>Specifies why a tooltip's parts skip their transitions, rendered as <c>data-instant</c>.</summary>
public enum TooltipInstantType
{
    /// <summary>The tooltip opened, or closed, while its provider's group moved between tooltips: <c>delay</c>.</summary>
    Delay,

    /// <summary>The tooltip opened by keyboard focus: <c>focus</c>.</summary>
    Focus,

    /// <summary>The tooltip closed by a press of its trigger or by Escape: <c>dismiss</c>.</summary>
    Dismiss,

    /// <summary>The tooltip follows the pointer; rendered on the positioner only: <c>tracking-cursor</c>.</summary>
    TrackingCursor,
}
