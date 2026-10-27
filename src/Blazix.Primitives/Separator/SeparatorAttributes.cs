using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// SEP-ARIA-01/02, TLB-DOM-10, TLB-ARIA-07: the attributes Separator and ToolbarSeparator both render (separator/Separator.tsx:
// 16-24; ToolbarSeparator renders Base UI's Separator). One static helper, so neither part composes or derives from the other
// (ADR 16 rule 10; ADR 08 Amendments 2026-10-22; chosen by the user 2026-10-26).
/// <summary>Writes the attributes every separator part renders.</summary>
internal static class SeparatorAttributes
{
    private const string HorizontalLiteral = "horizontal";
    private const string VerticalLiteral = "vertical";

    /// <summary>Adds <c>role="separator"</c> and the <c>aria-orientation</c> of an orientation to the part-supplied attributes.</summary>
    /// <param name="attributes">The part-supplied attributes, merged before the consumer's.</param>
    /// <param name="orientation">The separator's effective orientation.</param>
    public static void Write(AttributeList attributes, Orientation orientation)
    {
        // aria-orientation is written for horizontal too (SEP-ARIA-02). A value outside the two members (SEP-EDGE-01 has no
        // reachable case in a typed port) reads as horizontal, and the projection writes no data-orientation for it.
        attributes.Set("role", "separator");
        attributes.Set("aria-orientation", orientation == Orientation.Vertical ? VerticalLiteral : HorizontalLiteral);
    }
}
