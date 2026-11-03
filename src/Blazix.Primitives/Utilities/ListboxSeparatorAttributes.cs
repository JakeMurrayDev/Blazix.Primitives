using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Utilities;

// RND-STR-11, RND-API-13, RND-DOM-53, RND-ARIA-02 (utils/listbox-separator/ListboxSeparator.tsx): the attributes the
// separators of Select, Combobox and Autocomplete render. One static helper their AddPartAttributes call, the
// SeparatorAttributes precedent (ADR 16 rule 10), placed in Utilities/ with the other shared list internals (design point 1
// of M5 track 4, settled by the orchestrator 2026-11-04). Unlike Separator, a listbox separator is presentational: no
// role="separator" and no aria-orientation; its data-orientation comes from the shell's state record (default horizontal).
/// <summary>Writes the attributes of a separator between the items of a listbox.</summary>
internal static class ListboxSeparatorAttributes
{
    /// <summary>Writes <c>role="presentation"</c>, the only attribute the separator owns.</summary>
    /// <param name="attributes">The part's attributes, before the consumer's are merged.</param>
    public static void Write(AttributeList attributes) => attributes.Set("role", "presentation");
}
