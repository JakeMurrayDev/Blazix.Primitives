using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// menu/utils/stateAttributesMapping.ts:5-17 (`itemMapping`, MNU §5): a checkbox or radio item's, and its indicator's,
// Checked is data-checked or data-unchecked, exactly one of them; the transition status is the `transitions` module's (ADR 04
// §2), so in C# it is the mirror. The shared static arrays make a render allocate nothing.
/// <summary>Provides the state mapping of a menu's checkable items and their indicators.</summary>
internal static class MenuItemStateMapping
{
    private const string CheckedField = "Checked";

    private static readonly StateDataAttribute[] s_checked = [new(MenuCheckboxItemDataAttributes.Checked, string.Empty)];
    private static readonly StateDataAttribute[] s_unchecked = [new(MenuCheckboxItemDataAttributes.Unchecked, string.Empty)];
    private static readonly StateFieldProjection<bool> s_checkedProjection = static value => value ? s_checked : s_unchecked;

    /// <summary>Creates the mapping of a checkable item's or an indicator's state.</summary>
    /// <typeparam name="TState">The type of the part's state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>() => new StateAttributesMapping<TState>().Map(CheckedField, s_checkedProjection);
}
