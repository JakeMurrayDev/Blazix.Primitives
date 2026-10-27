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
        // ADR 04 Amendments 2026-10-22 (chosen by the user): the composite module writes every item's roving tabindex from
        // attach; C# renders "-1" (CMP-STA-15) and a consumer tabindex wins (RAD-DOM-16); radio.md §5.1 reads
        // `C# (standalone); C# → JS (in a group, from attach)`. There is no composite entry: the lint reads only
        // docs/components/*.md.
        new("radio", "Root", "tabindex", Writer.CsharpToJs),
        // Toggle (M3 tracks 2-3; ADR 04 Amendments 2026-10-22, chosen by the user; the radio row's shape): the composite module
        // writes a grouped toggle's roving tabindex from attach; C# renders the standalone values and "-1" in a group
        // (CMP-STA-15); toggle.md §5.1 reads `C# (standalone); C# → JS (in a group/toolbar, from attach)`.
        new("toggle", "Toggle", "tabindex", Writer.CsharpToJs),
        // ToggleGroup (M3 track 3): toggle-group.md §5.1 gives the standalone group's composite keydown and focus listeners
        // the Writer `JS` (TGG-DOM-06); the composite module attaches them and C# renders no handler for them.
        new("toggle-group", "Group", "handlers", Writer.Js),
        // Toolbar (M3 track 4; the toggle-group row's shape): toolbar.md §5.1 gives the root's composite keydown and focus
        // listeners the Writer `JS` (TLB-DOM-02), and §5.4 the input's click and pointerdown guards, keydown suppression and
        // composite focus and mousemove listeners (TLB-PTR-02, TLB-KEY-13); the modules attach them and C# renders none.
        new("toolbar", "Root", "handlers", Writer.Js),
        new("toolbar", "Input", "handlers", Writer.Js),
        // The always-item parts take the second half of the settled tabindex cell, `C# → JS (from attach)` (ADR 04
        // Amendments 2026-10-22, chosen by the user; the cell text chosen by the user 2026-10-26, M3 plan): C# renders "-1"
        // (CMP-STA-15) and the composite module writes the roving value from attach. Design point 14.1 (a), pending the
        // user: the button module clears the DOM disabled property a substituted part rendered (TLB-DOM-08, UBT-ALG-04).
        new("toolbar", "Button", "tabindex", Writer.CsharpToJs),
        new("toolbar", "Link", "tabindex", Writer.CsharpToJs),
        new("toolbar", "Input", "tabindex", Writer.CsharpToJs),
        new("toolbar", "Button", "disabled", Writer.CsharpToJs),
        // Radio (M2 tracks 7-8; ADR 04 Amendments 2026-10-22): the dom primitive owns the hidden radio input's checked
        // property from attach, C# rendering the initial value once (the Switch shape); the transitions module writes the
        // Indicator's transition attributes, mirrored in its state's TransitionStatus.
        new("radio", "Hidden input", "checked", Writer.CsharpToJs),
        new("radio", "Indicator", "data-starting-style", Writer.Js, "RadioIndicatorState.TransitionStatus"),
        new("radio", "Indicator", "data-ending-style", Writer.Js, "RadioIndicatorState.TransitionStatus"),
        // field.md §5 FLD-DOM-18 (M2 track 1, PR 1c): the transitions module writes the Error's enter and exit attributes,
        // mirrored in FieldErrorState.TransitionStatus; C# renders data-starting-style once, on the element it creates (the
        // handover, ADR 04 Amendments 2026-10-18).
        new("field", "Error", "data-starting-style", Writer.Js, "FieldErrorState.TransitionStatus"),
        new("field", "Error", "data-ending-style", Writer.Js, "FieldErrorState.TransitionStatus"),
        // field.md §5.3 FLD-DOM-11 (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): C# renders the value once; the form
        // module owns the element's value and defaultValue from attach. Input adopts the row (INP-STR-04).
        new("field", "Control", "value", Writer.CsharpToJs),
        new("field", "Control", "defaultValue", Writer.CsharpToJs),
        // Switch (M2 track 4; ADR 04 Amendments 2026-10-22): the dom primitive owns the hidden checkbox input's checked
        // property from attach; C# renders the initial value once (the handover).
        new("switch", "Hidden checkbox input", "checked", Writer.CsharpToJs),
        // Checkbox (M2 track 5; ADR 04 Amendments 2026-10-20 and 2026-10-22): the dom primitive owns the hidden checkbox
        // input's indeterminate property (JS) and its checked property from attach (C# renders the initial value once);
        // the transitions module writes the Indicator's transition attributes, mirrored in its state's TransitionStatus.
        new("checkbox", "Hidden checkbox input", "checked", Writer.CsharpToJs),
        new("checkbox", "Hidden checkbox input", "indeterminate", Writer.Js),
        new("checkbox", "Indicator", "data-starting-style", Writer.Js, "CheckboxIndicatorState.TransitionStatus"),
        new("checkbox", "Indicator", "data-ending-style", Writer.Js, "CheckboxIndicatorState.TransitionStatus"),
    ];
}
