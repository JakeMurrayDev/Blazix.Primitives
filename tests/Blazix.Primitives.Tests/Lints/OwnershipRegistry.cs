namespace Blazix.Primitives.Tests.Lints;

/// <summary>
/// The code's ownership registry (ADR 04 section 2, ADR 11 section 5): every attribute, data attribute and CSS variable
/// a JavaScript module writes, by family and part. The lint compares it with the <c>Writer</c> column of
/// <c>docs/components/*.md</c>.
/// </summary>
/// <remarks>
/// A family's pull request adds its entries when it adds the part, the state record and the twin options that carry the
/// value. The <c>[MirroredField]</c> properties of the library's <c>[PartState]</c> records are cross-checked: each one
/// must be named by an entry's <c>StateField</c>.
/// </remarks>
internal static class OwnershipRegistry
{
    /// <summary>Gets the entries written by hand.</summary>
    /// <value>The registry; entries for a family without a folder under <c>src/Blazix.Primitives/</c> are ignored.</value>
    public static IReadOnlyList<RegistryEntry> Declared { get; } =
    [
        // avatar.md §5.2 (ADR 04 §2 and Amendments 2026-10-18): the transitions module writes the Image's transition
        // attributes, mirrored in AvatarImageState.TransitionStatus; the element's presence is rendered by C# and hidden
        // by the dom module at load (the convergent write).
        new("avatar", "Image", "data-starting-style", Writer.Js, "AvatarImageState.TransitionStatus"),
        new("avatar", "Image", "data-ending-style", Writer.Js, "AvatarImageState.TransitionStatus"),
        new("avatar", "Image", "element presence", Writer.CsharpToJs),
        // Collapsible (M1 track 6): the transitions module on the Root and Trigger, the collapsible module on the Panel.
        new("collapsible", "Root", "data-starting-style", Writer.Js, "CollapsibleRootState.TransitionStatus"),
        new("collapsible", "Root", "data-ending-style", Writer.Js, "CollapsibleRootState.TransitionStatus"),
        new("collapsible", "Trigger", "data-starting-style", Writer.Js, "CollapsibleTriggerState.TransitionStatus"),
        new("collapsible", "Trigger", "data-ending-style", Writer.Js, "CollapsibleTriggerState.TransitionStatus"),
        new("collapsible", "Panel", "data-starting-style", Writer.Js, "CollapsiblePanelState.TransitionStatus"),
        new("collapsible", "Panel", "data-ending-style", Writer.Js, "CollapsiblePanelState.TransitionStatus"),
        // The `C# (boolean); JS ("until-found")` cell names no attribute in either note, so the lint reads it as JS.
        new("collapsible", "Panel", "hidden", Writer.Js),
        new("collapsible", "Panel", "--collapsible-panel-height", Writer.Js),
        new("collapsible", "Panel", "--collapsible-panel-width", Writer.Js),
        // field.md §5 FLD-DOM-18 (M2 track 1): the transitions module writes the Error's enter and exit attributes. The part
        // and its state record arrive in PR 1c, which adds the StateField (FieldErrorState.TransitionStatus).
        new("field", "Error", "data-starting-style", Writer.Js),
        new("field", "Error", "data-ending-style", Writer.Js),
        // field.md §5.3 FLD-DOM-11 (ADR 04 Amendments 2026-10-22, pending the user): C# renders the value once; the form
        // module owns the element's value and defaultValue from attach. Input adopts the row (INP-STR-04).
        new("field", "Control", "value", Writer.CsharpToJs),
        new("field", "Control", "defaultValue", Writer.CsharpToJs),
    ];
}
