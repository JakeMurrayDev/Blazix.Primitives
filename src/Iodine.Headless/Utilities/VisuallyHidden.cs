namespace Blazix.Primitives.Utilities;

// UTL-DOM-13, UTL-DOM-14, UTL-DOM-15: exact declaration lists, in Base UI's order, as inline style text (MTR-DOM-01).
/// <summary>Provides the inline style declarations that hide an element visually while keeping it available to assistive technology.</summary>
internal static class VisuallyHidden
{
    /// <summary>Represents the declarations for a hidden helper element that must not add to an ancestor's scrollable overflow in either reading direction.</summary>
    public const string Style = "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0";

    /// <summary>Represents the declarations for a hidden form input.</summary>
    public const string InputStyle = "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute";

    /// <summary>Represents the declarations for the empty element that owns another through <c>aria-owns</c>.</summary>
    public const string OwnerStyle = "clip-path: inset(50%); position: fixed; top: 0; left: 0";
}
