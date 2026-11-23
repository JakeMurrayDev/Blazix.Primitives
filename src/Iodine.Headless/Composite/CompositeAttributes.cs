namespace Blazix.Primitives;

// CMP §6.11 ACTIVE_COMPOSITE_ITEM (internals/composite/constants.ts:1), CMP-DOM-02: a component writes it on the item that
// takes the initial tab stop (Radio, RAD-DOM-02; Tabs in M3) and the composite module only reads it (CMP-STA-23/27).
/// <summary>Provides the names of the attributes the composite items of a part carry.</summary>
internal static class CompositeAttributes
{
    /// <summary>Represents the attribute, with an empty value, on the item that takes a group's initial tab stop: <c>data-composite-item-active</c>.</summary>
    public const string ActiveItem = "data-composite-item-active";
}
