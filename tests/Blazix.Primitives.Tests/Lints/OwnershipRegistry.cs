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
        // Accordion (M3 track 6; ADR 04 note dated 2026-10-26): the collapsible module on the Panel, under the Accordion names;
        // the transition attributes are mirrored in the generic AccordionPanelState<TValue>.TransitionStatus.
        new("accordion", "Panel", "data-starting-style", Writer.Js, "AccordionPanelState`1.TransitionStatus"),
        new("accordion", "Panel", "data-ending-style", Writer.Js, "AccordionPanelState`1.TransitionStatus"),
        new("accordion", "Panel", "hidden", Writer.Js),
        new("accordion", "Panel", "--accordion-panel-height", Writer.Js),
        new("accordion", "Panel", "--accordion-panel-width", Writer.Js),
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
        // Tabs (M3 track 5). ADR 04 Amendments 2026-10-22 (the composite row names the items of Tabs): the composite module
        // writes every Tab's roving tabindex from attach; C# renders "-1" (CMP-STA-15) and a consumer tabindex wins.
        new("tabs", "Tab", "tabindex", Writer.CsharpToJs),
        // ADR 04 Amendments 2026-10-26 (chosen by the user 2026-10-26): the tabs module's List handle writes
        // data-activation-direction on the five parts from attach; C# renders the initial value once per element (the
        // handover) and mirrors it in each state's TabActivationDirection.
        new("tabs", "Root", "data-activation-direction", Writer.Js, "TabsRootState.TabActivationDirection"),
        new("tabs", "List", "data-activation-direction", Writer.Js, "TabsListState.TabActivationDirection"),
        new("tabs", "Tab", "data-activation-direction", Writer.Js, "TabsTabState.TabActivationDirection"),
        new("tabs", "Panel", "data-activation-direction", Writer.Js, "TabsPanelState.TabActivationDirection"),
        new("tabs", "Indicator", "data-activation-direction", Writer.Js, "TabsIndicatorState.TabActivationDirection"),
        // ADR 04 §2 (the Tabs.Indicator row) and Amendments 2026-10-18 and 2026-10-26: the Indicator's tabs handle writes
        // hidden and the element's whole style (the six --active-tab-* variables over the consumer's string), C# rendering
        // the initial values under the handover; the measurement is mirrored in ActiveTabPosition and ActiveTabSize.
        new("tabs", "Indicator", "hidden", Writer.Js),
        new("tabs", "Indicator", "style", Writer.Js, "TabsIndicatorState.ActiveTabPosition"),
        new("tabs", "Indicator", "style", Writer.Js, "TabsIndicatorState.ActiveTabSize"),
        // ADR 04 §2 (every animated part): the transitions module writes the Panel's transition attributes, mirrored in
        // TabsPanelState.TransitionStatus; C# renders data-starting-style once, on an element it creates open.
        new("tabs", "Panel", "data-starting-style", Writer.Js, "TabsPanelState.TransitionStatus"),
        new("tabs", "Panel", "data-ending-style", Writer.Js, "TabsPanelState.TransitionStatus"),
        // AlertDialog (M4 track 9, issue 124; design point 2, settled by the orchestrator 2026-10-29, ADR 04 Amendments
        // 2026-10-29): Dialog's transition rows under alert-dialog.md §5.1's own Writer tables, mirrored in each AlertDialog
        // state's TransitionStatus; §5.2's outside aria-hidden is the focus manager's outside marking.
        new("alert-dialog", "Backdrop", "data-starting-style", Writer.Js, "AlertDialogBackdropState.TransitionStatus"),
        new("alert-dialog", "Backdrop", "data-ending-style", Writer.Js, "AlertDialogBackdropState.TransitionStatus"),
        new("alert-dialog", "Viewport", "data-starting-style", Writer.Js, "AlertDialogViewportState.TransitionStatus"),
        new("alert-dialog", "Viewport", "data-ending-style", Writer.Js, "AlertDialogViewportState.TransitionStatus"),
        new("alert-dialog", "Popup", "data-starting-style", Writer.Js, "AlertDialogPopupState.TransitionStatus"),
        new("alert-dialog", "Popup", "data-ending-style", Writer.Js, "AlertDialogPopupState.TransitionStatus"),
        new("alert-dialog", "ARIA", "aria-hidden", Writer.Js),
        // Dialog (M4 track 8; ADR 04 §2 and Amendments 2026-10-27): the transitions module writes the Backdrop's, the
        // Viewport's and the Popup's transition attributes, mirrored in each state's TransitionStatus; C# renders the
        // initial status under the handover. `--nested-dialogs` is C#'s, written in the commit path (no entry).
        new("dialog", "Backdrop", "data-starting-style", Writer.Js, "DialogBackdropState.TransitionStatus"),
        new("dialog", "Backdrop", "data-ending-style", Writer.Js, "DialogBackdropState.TransitionStatus"),
        new("dialog", "Viewport", "data-starting-style", Writer.Js, "DialogViewportState.TransitionStatus"),
        new("dialog", "Viewport", "data-ending-style", Writer.Js, "DialogViewportState.TransitionStatus"),
        new("dialog", "Popup", "data-starting-style", Writer.Js, "DialogPopupState.TransitionStatus"),
        new("dialog", "Popup", "data-ending-style", Writer.Js, "DialogPopupState.TransitionStatus"),
        // dialog.md §5.2's Writer table (ADR 04 Amendments 2026-10-27, the portal node of Dialog): the node renders
        // `display: none` until the portal module moves it, which owns `style` from attach.
        new("dialog", "Portal", "style", Writer.CsharpToJs),
        // dialog.md §5.8 (DLG-ARIA-11): the focus manager's outside marking writes `aria-hidden` on elements no part owns,
        // under the section's heading words; its page-global state is recorded in js/CONTEXT.md.
        new("dialog", "ARIA", "aria-hidden", Writer.Js),
        // dialog.md §5.8's focus guards (ADR 04 Amendments 2026-10-27, the VoiceOver swap as a JavaScript write after
        // attach): under VoiceOver on WebKit the modules swap `aria-hidden` for `role="button"`; C# renders `aria-hidden`.
        new("dialog", "ARIA", "role", Writer.Js),
        // Popover (M4 track 5, issue 125; ADR 04 §2 and Amendments 2026-10-27, chosen by the user 2026-10-27). The
        // positioning module writes the Positioner's style, placement and anchor-hidden attributes from attach (C# renders
        // the requested values and the pre-position style under the handover; the placement is mirrored in the state), and
        // the Arrow's; the transitions module writes the Popup's and the Backdrop's transition attributes and the Popup's
        // `transition: none`; the popup module writes the internal backdrop's clip-path, the Popup's auto-resize variables
        // and the Viewport's containers, direction and transitioning flag (mirrored in PopoverViewportState).
        new("popover", "Positioner", "pointer-events", Writer.CsharpToJs),
        new("popover", "Positioner", "data-side", Writer.CsharpToJs, "PopoverPositionerState.Side"),
        new("popover", "Positioner", "data-align", Writer.CsharpToJs, "PopoverPositionerState.Align"),
        new("popover", "Positioner", "data-anchor-hidden", Writer.Js, "PopoverPositionerState.AnchorHidden"),
        new("popover", "Positioner", "transition", Writer.Js),
        new("popover", "Positioner", "positioning styles and CSS variables", Writer.CsharpToJs),
        new("popover", "Positioner", "clip-path", Writer.Js),
        // D11 (2026-11-10, settled by the orchestrator; TRN-STA-03, ADR 04 Amendments 2026-10-21): the popup parts' transition
        // attributes are a handover, rendered by C# until the transitions handle attaches (PopupTransitionAttributes, Popup/),
        // here and on Popover's Backdrop, Tooltip's Popup and PreviewCard's Popup and Backdrop.
        new("popover", "Popup", "data-starting-style", Writer.CsharpToJs, "PopoverPopupState.TransitionStatus"),
        new("popover", "Popup", "data-ending-style", Writer.CsharpToJs, "PopoverPopupState.TransitionStatus"),
        new("popover", "Popup", "transition", Writer.Js),
        new("popover", "Popup", "--popup-width", Writer.Js),
        new("popover", "Popup", "--popup-height", Writer.Js),
        new("popover", "Arrow", "data-side", Writer.CsharpToJs, "PopoverArrowState.Side"),
        new("popover", "Arrow", "data-align", Writer.CsharpToJs, "PopoverArrowState.Align"),
        new("popover", "Arrow", "data-uncentered", Writer.CsharpToJs, "PopoverArrowState.Uncentered"),
        new("popover", "Arrow", "style", Writer.CsharpToJs),
        new("popover", "Backdrop", "data-starting-style", Writer.CsharpToJs, "PopoverBackdropState.TransitionStatus"),
        new("popover", "Backdrop", "data-ending-style", Writer.CsharpToJs, "PopoverBackdropState.TransitionStatus"),
        new("popover", "Viewport", "[data-previous] container", Writer.Js),
        new("popover", "Viewport", "data-activation-direction", Writer.Js, "PopoverViewportState.ActivationDirection"),
        // NavigationMenu (M5 track 5, issue 163; ADR 04 §2 and Amendments 2026-10-18, 2026-10-20 and 2026-11-03 (4), chosen by
        // the user). The transitions module writes the Content's, the Popup's and the Backdrop's transition attributes; the
        // positioning module writes the Positioner's placement, anchor-hidden flag and variables and the Arrow's; the
        // navigation-menu module's size pass writes --popup-* and --positioner-* (§3 Writer cells, sheet F §4.6).
        new("navigation-menu", "Content", "data-starting-style", Writer.Js, "NavigationMenuContentState.TransitionStatus"),
        new("navigation-menu", "Content", "data-ending-style", Writer.Js, "NavigationMenuContentState.TransitionStatus"),
        new("navigation-menu", "Backdrop", "data-starting-style", Writer.Js, "NavigationMenuBackdropState.TransitionStatus"),
        new("navigation-menu", "Backdrop", "data-ending-style", Writer.Js, "NavigationMenuBackdropState.TransitionStatus"),
        new("navigation-menu", "Positioner", "data-side", Writer.CsharpToJs, "NavigationMenuPositionerState.Side"),
        new("navigation-menu", "Positioner", "data-align", Writer.CsharpToJs, "NavigationMenuPositionerState.Align"),
        new("navigation-menu", "Positioner", "data-anchor-hidden", Writer.Js, "NavigationMenuPositionerState.AnchorHidden"),
        new("navigation-menu", "Positioner", "--anchor-width", Writer.Js),
        new("navigation-menu", "Positioner", "--anchor-height", Writer.Js),
        new("navigation-menu", "Positioner", "--available-width", Writer.CsharpToJs),
        new("navigation-menu", "Positioner", "--available-height", Writer.CsharpToJs),
        new("navigation-menu", "Positioner", "--transform-origin", Writer.Js),
        new("navigation-menu", "Positioner", "--positioner-width", Writer.Js),
        new("navigation-menu", "Positioner", "--positioner-height", Writer.Js),
        new("navigation-menu", "Popup", "data-starting-style", Writer.Js, "NavigationMenuPopupState.TransitionStatus"),
        new("navigation-menu", "Popup", "data-ending-style", Writer.Js, "NavigationMenuPopupState.TransitionStatus"),
        new("navigation-menu", "Popup", "--popup-width", Writer.Js),
        new("navigation-menu", "Popup", "--popup-height", Writer.Js),
        new("navigation-menu", "Arrow", "data-side", Writer.CsharpToJs, "NavigationMenuArrowState.Side"),
        new("navigation-menu", "Arrow", "data-align", Writer.CsharpToJs, "NavigationMenuArrowState.Align"),
        new("navigation-menu", "Arrow", "data-uncentered", Writer.Js, "NavigationMenuArrowState.Uncentered"),
        new("popover", "Viewport", "data-transitioning", Writer.Js, "PopoverViewportState.Transitioning"),
        new("popover", "Viewport", "--popup-width", Writer.Js),
        new("popover", "Viewport", "--popup-height", Writer.Js),
        // Select (M5 track 9, issue 173; ADR 04 §2 and Amendments 2026-10-27, select.md §5): positioning writes the
        // Positioner's and the Arrow's placement, style and CSS variables from attach (mirrored in the state records); the
        // select module writes the aligned placement and layout styles, the Popup's --transform-origin, the List's style
        // and the scroll arrows' data-visible (mirrored in SelectScrollArrowState.Visible); transitions writes the
        // transition attributes and `transition: none`; composite writes the items' highlight and tabindex (mirrored in
        // SelectItemState.Highlighted); the popup module writes the internal backdrop's clip-path; the form module writes
        // the hidden input's value property from attach, C# rendering the initial value once (the Radio shape).
        new("select", "Hidden form-participation `<input>`", "value", Writer.CsharpToJs),
        new("select", "Backdrop", "data-starting-style", Writer.Js, "SelectBackdropState.TransitionStatus"),
        new("select", "Backdrop", "data-ending-style", Writer.Js, "SelectBackdropState.TransitionStatus"),
        new("select", "Positioner", "pointer-events", Writer.CsharpToJs),
        new("select", "Positioner", "data-side", Writer.CsharpToJs, "SelectPositionerState.Side"),
        new("select", "Positioner", "data-align", Writer.CsharpToJs, "SelectPositionerState.Align"),
        new("select", "Positioner", "data-anchor-hidden", Writer.Js, "SelectPositionerState.AnchorHidden"),
        new("select", "Positioner", "positioning styles and CSS variables", Writer.CsharpToJs),
        new("select", "Positioner", "aligned placement styles", Writer.Js),
        new("select", "Positioner", "transition", Writer.Js),
        new("select", "Positioner", "clip-path", Writer.Js),
        new("select", "Popup", "data-starting-style", Writer.Js, "SelectPopupState.TransitionStatus"),
        new("select", "Popup", "data-ending-style", Writer.Js, "SelectPopupState.TransitionStatus"),
        new("select", "Popup", "aligned layout styles", Writer.Js),
        new("select", "Popup", "transition", Writer.Js),
        new("select", "Popup", "--transform-origin", Writer.Js),
        new("select", "List", "style", Writer.CsharpToJs),
        new("select", "Item", "data-highlighted", Writer.Js, "SelectItemState.Highlighted"),
        new("select", "Item", "tabindex", Writer.CsharpToJs),
        new("select", "ItemIndicator", "data-starting-style", Writer.Js, "SelectItemIndicatorState.TransitionStatus"),
        new("select", "ItemIndicator", "data-ending-style", Writer.Js, "SelectItemIndicatorState.TransitionStatus"),
        new("select", "Arrow", "data-side", Writer.CsharpToJs, "SelectArrowState.Side"),
        new("select", "Arrow", "data-align", Writer.CsharpToJs, "SelectArrowState.Align"),
        new("select", "Arrow", "data-uncentered", Writer.CsharpToJs, "SelectArrowState.Uncentered"),
        new("select", "Arrow", "style", Writer.CsharpToJs),
        new("select", "Scroll-arrow", "data-visible", Writer.CsharpToJs, "SelectScrollArrowState.Visible"),
        new("select", "Scroll-arrow", "data-starting-style", Writer.Js, "SelectScrollArrowState.TransitionStatus"),
        new("select", "Scroll-arrow", "data-ending-style", Writer.Js, "SelectScrollArrowState.TransitionStatus"),
        // Tooltip (M4 track 6, issue 121; ADR 04 §2 as amended 2026-10-27, tooltip.md §5): the interactions module attaches
        // the trigger's and the floating element's listeners; positioning writes the Positioner's and the Arrow's style and
        // placement from attach (mirrored in the state records); transitions writes the Popup's transition attributes and
        // its style. data-instant is C#'s on every part.
        new("tooltip", "Trigger", "handlers", Writer.Js),
        new("tooltip", "Positioner", "style", Writer.CsharpToJs),
        new("tooltip", "Positioner", "data-side", Writer.CsharpToJs, "TooltipPositionerState.Side"),
        new("tooltip", "Positioner", "data-align", Writer.CsharpToJs, "TooltipPositionerState.Align"),
        new("tooltip", "Positioner", "data-anchor-hidden", Writer.Js, "TooltipPositionerState.AnchorHidden"),
        new("tooltip", "Popup", "data-starting-style", Writer.CsharpToJs, "TooltipPopupState.TransitionStatus"),
        new("tooltip", "Popup", "data-ending-style", Writer.CsharpToJs, "TooltipPopupState.TransitionStatus"),
        new("tooltip", "Popup", "style", Writer.Js),
        new("tooltip", "Popup", "handlers", Writer.Js),
        new("tooltip", "Arrow", "style", Writer.CsharpToJs),
        new("tooltip", "Arrow", "data-side", Writer.CsharpToJs, "TooltipArrowState.Side"),
        new("tooltip", "Arrow", "data-align", Writer.CsharpToJs, "TooltipArrowState.Align"),
        new("tooltip", "Arrow", "data-uncentered", Writer.CsharpToJs, "TooltipArrowState.Uncentered"),
        // The Viewport (tooltip.md §5.6, at PR 137's re-merge): Popover's, the `popup` module writing its containers,
        // direction and transitioning flag (mirrored in TooltipViewportState).
        new("tooltip", "Viewport", "[data-previous] container", Writer.Js),
        new("tooltip", "Viewport", "data-activation-direction", Writer.Js, "TooltipViewportState.ActivationDirection"),
        new("tooltip", "Viewport", "data-transitioning", Writer.Js, "TooltipViewportState.Transitioning"),
        new("tooltip", "Viewport", "--popup-width", Writer.Js),
        new("tooltip", "Viewport", "--popup-height", Writer.Js),
        // PreviewCard (M4 track 7, issue 122; ADR 04 §2 as amended 2026-10-27, preview-card.md §5): the interactions module
        // attaches the trigger's and the popup's listeners; positioning writes the Positioner's and the Arrow's style and
        // placement from attach (mirrored in the state records); transitions writes the Popup's and the Backdrop's
        // transition attributes and the Popup's `transition: none`; the popup module writes the Viewport's containers,
        // direction and transitioning flag. data-instant is C#'s on every part.
        new("preview-card", "Trigger", "handlers", Writer.Js),
        new("preview-card", "Positioner", "pointer-events", Writer.CsharpToJs),
        new("preview-card", "Positioner", "transition", Writer.CsharpToJs),
        new("preview-card", "Positioner", "data-side", Writer.CsharpToJs, "PreviewCardPositionerState.Side"),
        new("preview-card", "Positioner", "data-align", Writer.CsharpToJs, "PreviewCardPositionerState.Align"),
        new("preview-card", "Positioner", "data-anchor-hidden", Writer.Js, "PreviewCardPositionerState.AnchorHidden"),
        new("preview-card", "Positioner", "CSS variables", Writer.CsharpToJs),
        new("preview-card", "Positioner", "position", Writer.CsharpToJs),
        new("preview-card", "Positioner", "top", Writer.CsharpToJs),
        new("preview-card", "Positioner", "left", Writer.CsharpToJs),
        new("preview-card", "Positioner", "transform", Writer.CsharpToJs),
        new("preview-card", "Positioner", "opacity", Writer.CsharpToJs),
        new("preview-card", "Popup", "transition", Writer.Js),
        new("preview-card", "Popup", "data-starting-style", Writer.CsharpToJs, "PreviewCardPopupState.TransitionStatus"),
        new("preview-card", "Popup", "data-ending-style", Writer.CsharpToJs, "PreviewCardPopupState.TransitionStatus"),
        new("preview-card", "Popup", "handlers", Writer.Js),
        new("preview-card", "Arrow", "style", Writer.CsharpToJs),
        new("preview-card", "Arrow", "data-side", Writer.CsharpToJs, "PreviewCardArrowState.Side"),
        new("preview-card", "Arrow", "data-align", Writer.CsharpToJs, "PreviewCardArrowState.Align"),
        new("preview-card", "Arrow", "data-uncentered", Writer.CsharpToJs, "PreviewCardArrowState.Uncentered"),
        new("preview-card", "Backdrop", "data-starting-style", Writer.CsharpToJs, "PreviewCardBackdropState.TransitionStatus"),
        new("preview-card", "Backdrop", "data-ending-style", Writer.CsharpToJs, "PreviewCardBackdropState.TransitionStatus"),
        new("preview-card", "Viewport", "[data-previous] container", Writer.Js),
        new("preview-card", "Viewport", "data-activation-direction", Writer.Js, "PreviewCardViewportState.ActivationDirection"),
        new("preview-card", "Viewport", "data-transitioning", Writer.Js, "PreviewCardViewportState.Transitioning"),
        new("preview-card", "Viewport", "--popup-width", Writer.Js),
        new("preview-card", "Viewport", "--popup-height", Writer.Js),
        // Menu (M5 track 6, issue 172; ADR 04 §2 and Amendments 2026-10-27 and 2026-11-03 (2), menu.md §3): transitions
        // writes the Backdrop's, the Popup's and the indicators' transition attributes; positioning writes the
        // Positioner's and the Arrow's placement and variables from attach; the popup module writes the Viewport's
        // containers, direction and transitioning flag; composite's list handle writes every item's data-highlighted and
        // tabindex (mirrored in the state records).
        new("menu", "Backdrop", "data-starting-style", Writer.Js, "MenuBackdropState.TransitionStatus"),
        new("menu", "Backdrop", "data-ending-style", Writer.Js, "MenuBackdropState.TransitionStatus"),
        new("menu", "Popup", "data-starting-style", Writer.Js, "MenuPopupState.TransitionStatus"),
        new("menu", "Popup", "data-ending-style", Writer.Js, "MenuPopupState.TransitionStatus"),
        new("menu", "Positioner", "data-anchor-hidden", Writer.Js, "MenuPositionerState.AnchorHidden"),
        new("menu", "Positioner", "data-side", Writer.CsharpToJs, "MenuPositionerState.Side"),
        new("menu", "Positioner", "data-align", Writer.CsharpToJs, "MenuPositionerState.Align"),
        new("menu", "Positioner", "--anchor-width", Writer.CsharpToJs),
        new("menu", "Positioner", "--anchor-height", Writer.CsharpToJs),
        new("menu", "Positioner", "--available-width", Writer.CsharpToJs),
        new("menu", "Positioner", "--available-height", Writer.CsharpToJs),
        new("menu", "Positioner", "--transform-origin", Writer.CsharpToJs),
        new("menu", "Positioner", "--positioner-width", Writer.CsharpToJs),
        new("menu", "Positioner", "--positioner-height", Writer.CsharpToJs),
        new("menu", "Arrow", "data-side", Writer.CsharpToJs, "MenuArrowState.Side"),
        new("menu", "Arrow", "data-align", Writer.CsharpToJs, "MenuArrowState.Align"),
        new("menu", "Arrow", "data-uncentered", Writer.Js, "MenuArrowState.Uncentered"),
        new("menu", "Viewport", "data-activation-direction", Writer.Js, "MenuViewportState.ActivationDirection"),
        new("menu", "Viewport", "data-transitioning", Writer.Js, "MenuViewportState.Transitioning"),
        new("menu", "Viewport", "data-previous", Writer.Js),
        // Menubar (M5 track 8, issue 195; sheet C contradiction 6, Q14 (a); ADR 04 §2's tabindex cell): inside a bar, the bar's
        // composite handle writes each Trigger's roving tabindex from attach; C# renders "-1" until then (CMP-STA-15).
        new("menu", "Trigger", "tabindex", Writer.CsharpToJs),
        new("menu", "Viewport", "--popup-width", Writer.Js),
        new("menu", "Viewport", "--popup-height", Writer.Js),
        new("menu", "Item", "data-highlighted", Writer.CsharpToJs, "MenuItemState.Highlighted"),
        new("menu", "Item", "tabindex", Writer.CsharpToJs),
        new("menu", "Item", "aria-selected", Writer.Js),
        new("menu", "LinkItem", "data-highlighted", Writer.CsharpToJs, "MenuLinkItemState.Highlighted"),
        new("menu", "LinkItem", "tabindex", Writer.CsharpToJs),
        new("menu", "LinkItem", "aria-selected", Writer.Js),
        new("menu", "CheckboxItem", "data-highlighted", Writer.CsharpToJs, "MenuCheckboxItemState.Highlighted"),
        new("menu", "CheckboxItem", "tabindex", Writer.CsharpToJs),
        new("menu", "CheckboxItem", "aria-selected", Writer.Js),
        new("menu", "RadioItem", "data-highlighted", Writer.CsharpToJs, "MenuRadioItemState.Highlighted"),
        new("menu", "RadioItem", "tabindex", Writer.CsharpToJs),
        new("menu", "RadioItem", "aria-selected", Writer.Js),
        new("menu", "SubmenuTrigger", "data-highlighted", Writer.CsharpToJs, "MenuSubmenuTriggerState.Highlighted"),
        new("menu", "Input", "data-highlighted", Writer.Js, "MenuInputState.Highlighted"),
        new("menu", "Input", "value", Writer.CsharpToJs),
        new("menu", "Input", "aria-activedescendant", Writer.Js),
        new("menu", "SubmenuTrigger", "tabindex", Writer.CsharpToJs),
        new("menu", "SubmenuTrigger", "aria-selected", Writer.Js),
        new("menu", "CheckboxItemIndicator", "data-starting-style", Writer.Js, "MenuCheckboxItemIndicatorState.TransitionStatus"),
        new("menu", "CheckboxItemIndicator", "data-ending-style", Writer.Js, "MenuCheckboxItemIndicatorState.TransitionStatus"),
        new("menu", "RadioItemIndicator", "data-starting-style", Writer.Js, "MenuRadioItemIndicatorState.TransitionStatus"),
        new("menu", "RadioItemIndicator", "data-ending-style", Writer.Js, "MenuRadioItemIndicatorState.TransitionStatus"),
        // Combobox (M5 track 10, issue 174; ADR 04 §2, Amendments 2026-11-03 (2) and (6), chosen by the user 2026-11-03, and
        // the `form` list additions of 2026-11-04). The list handle writes the Item's data-highlighted and the Input's
        // aria-activedescendant from attach (Highlighted mirrored); the combobox module writes the Input's value from update
        // and the form module the hidden input's; positioning writes the Positioner's placement and variables and the
        // Arrow's (mirrored); transitions writes the Popup's, Backdrop's, Clear's and ItemIndicator's transition attributes.
        new("combobox", "Root", "value", Writer.CsharpToJs),
        new("combobox", "Input", "aria-activedescendant", Writer.Js),
        new("combobox", "Input", "value", Writer.CsharpToJs),
        new("combobox", "Clear", "data-starting-style", Writer.Js, "ComboboxClearState.TransitionStatus"),
        new("combobox", "Clear", "data-ending-style", Writer.Js, "ComboboxClearState.TransitionStatus"),
        new("combobox", "Backdrop", "data-starting-style", Writer.Js, "ComboboxBackdropState.TransitionStatus"),
        new("combobox", "Backdrop", "data-ending-style", Writer.Js, "ComboboxBackdropState.TransitionStatus"),
        new("combobox", "Positioner", "data-side", Writer.CsharpToJs, "ComboboxPositionerState.Side"),
        new("combobox", "Positioner", "data-align", Writer.CsharpToJs, "ComboboxPositionerState.Align"),
        new("combobox", "Positioner", "data-anchor-hidden", Writer.Js, "ComboboxPositionerState.AnchorHidden"),
        new("combobox", "Positioner", "--available-width", Writer.CsharpToJs),
        new("combobox", "Positioner", "--available-height", Writer.CsharpToJs),
        new("combobox", "Positioner", "--anchor-width", Writer.CsharpToJs),
        new("combobox", "Positioner", "--anchor-height", Writer.CsharpToJs),
        new("combobox", "Positioner", "--transform-origin", Writer.CsharpToJs),
        new("combobox", "Popup", "data-starting-style", Writer.Js, "ComboboxPopupState.TransitionStatus"),
        new("combobox", "Popup", "data-ending-style", Writer.Js, "ComboboxPopupState.TransitionStatus"),
        new("combobox", "Arrow", "data-side", Writer.CsharpToJs, "ComboboxArrowState.Side"),
        new("combobox", "Arrow", "data-align", Writer.CsharpToJs, "ComboboxArrowState.Align"),
        new("combobox", "Arrow", "data-uncentered", Writer.CsharpToJs, "ComboboxArrowState.Uncentered"),
        new("combobox", "Item", "data-highlighted", Writer.CsharpToJs, "ComboboxItemState.Highlighted"),
        new("combobox", "ItemIndicator", "data-starting-style", Writer.Js, "ComboboxItemIndicatorState.TransitionStatus"),
        new("combobox", "ItemIndicator", "data-ending-style", Writer.Js, "ComboboxItemIndicatorState.TransitionStatus"),

        // ContextMenu (M5 track 7, issue 194; ADR 04 §2 and Amendments 2026-11-03 (2), context-menu.md §5): Menu's rows on
        // ContextMenu's shells, less the Viewport, which ContextMenu has not (CTX-STR-01); and ARIA-04's aria-hidden outside
        // the tree, the modal focus manager's (as AlertDialog's).
        new("context-menu", "Backdrop", "data-starting-style", Writer.Js, "ContextMenuBackdropState.TransitionStatus"),
        new("context-menu", "Backdrop", "data-ending-style", Writer.Js, "ContextMenuBackdropState.TransitionStatus"),
        new("context-menu", "Popup", "data-starting-style", Writer.Js, "ContextMenuPopupState.TransitionStatus"),
        new("context-menu", "Popup", "data-ending-style", Writer.Js, "ContextMenuPopupState.TransitionStatus"),
        new("context-menu", "Positioner", "data-anchor-hidden", Writer.Js, "ContextMenuPositionerState.AnchorHidden"),
        new("context-menu", "Positioner", "data-side", Writer.CsharpToJs, "ContextMenuPositionerState.Side"),
        new("context-menu", "Positioner", "data-align", Writer.CsharpToJs, "ContextMenuPositionerState.Align"),
        new("context-menu", "Positioner", "--anchor-width", Writer.CsharpToJs),
        new("context-menu", "Positioner", "--anchor-height", Writer.CsharpToJs),
        new("context-menu", "Positioner", "--available-width", Writer.CsharpToJs),
        new("context-menu", "Positioner", "--available-height", Writer.CsharpToJs),
        new("context-menu", "Positioner", "--transform-origin", Writer.CsharpToJs),
        new("context-menu", "Positioner", "--positioner-width", Writer.CsharpToJs),
        new("context-menu", "Positioner", "--positioner-height", Writer.CsharpToJs),
        new("context-menu", "Arrow", "data-side", Writer.CsharpToJs, "ContextMenuArrowState.Side"),
        new("context-menu", "Arrow", "data-align", Writer.CsharpToJs, "ContextMenuArrowState.Align"),
        new("context-menu", "Arrow", "data-uncentered", Writer.Js, "ContextMenuArrowState.Uncentered"),
        new("context-menu", "Item", "data-highlighted", Writer.CsharpToJs, "ContextMenuItemState.Highlighted"),
        new("context-menu", "Item", "tabindex", Writer.CsharpToJs),
        new("context-menu", "LinkItem", "data-highlighted", Writer.CsharpToJs, "ContextMenuLinkItemState.Highlighted"),
        new("context-menu", "LinkItem", "tabindex", Writer.CsharpToJs),
        new("context-menu", "CheckboxItem", "data-highlighted", Writer.CsharpToJs, "ContextMenuCheckboxItemState.Highlighted"),
        new("context-menu", "CheckboxItem", "tabindex", Writer.CsharpToJs),
        new("context-menu", "RadioItem", "data-highlighted", Writer.CsharpToJs, "ContextMenuRadioItemState.Highlighted"),
        new("context-menu", "RadioItem", "tabindex", Writer.CsharpToJs),
        new("context-menu", "SubmenuTrigger", "data-highlighted", Writer.CsharpToJs, "ContextMenuSubmenuTriggerState.Highlighted"),
        new("context-menu", "SubmenuTrigger", "tabindex", Writer.CsharpToJs),
        new("context-menu", "CheckboxItemIndicator", "data-starting-style", Writer.Js, "ContextMenuCheckboxItemIndicatorState.TransitionStatus"),
        new("context-menu", "CheckboxItemIndicator", "data-ending-style", Writer.Js, "ContextMenuCheckboxItemIndicatorState.TransitionStatus"),
        new("context-menu", "RadioItemIndicator", "data-starting-style", Writer.Js, "ContextMenuRadioItemIndicatorState.TransitionStatus"),
        new("context-menu", "RadioItemIndicator", "data-ending-style", Writer.Js, "ContextMenuRadioItemIndicatorState.TransitionStatus"),
        new("context-menu", "ARIA", "aria-hidden", Writer.Js),
    ];
}
