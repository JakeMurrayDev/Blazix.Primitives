namespace Blazix.Primitives;

// PRV §4.1 (instantType), PRV-STA-10; ADR 08 Amendments 2026-10-27 (the per-family instant enums, chosen by the user
// 2026-10-27). Rendered by C# as data-instant on the Positioner, the Popup and the Viewport (ADR 04 Amendments 2026-10-27;
// PRV §16 Q3), in Base UI's kebab-case literals.
/// <summary>Specifies why a preview card's parts skip their transitions, rendered as <c>data-instant</c>.</summary>
public enum PreviewCardInstantType
{
    /// <summary>The card closed by Escape: <c>dismiss</c>.</summary>
    Dismiss,

    /// <summary>The card opened by keyboard focus: <c>focus</c>.</summary>
    Focus,
}
