namespace Blazix.Primitives;

// FLT-FOC-18 (FloatingFocusManager.tsx:972-990): what a focus manager inside a portal publishes to it, the C# side of the
// slot (the module's side is the PortalState under Symbol.for('blazix.portal')). The Portal renders its outside guards and
// owner while a publication is non-modal and open (FLT-DOM-14); track 4's FocusManagerBehavior publishes it ("C# renders the
// outside guards from the Popup's state cascaded to the Portal", ADR 04 Amendments 2026-10-27).
/// <summary>Describes the focus manager state a portal renders its outside focus guards from.</summary>
/// <param name="Modal">Whether the focus manager traps focus.</param>
/// <param name="Open">Whether the popup is open.</param>
/// <param name="CloseOnFocusOut">Whether tabbing out of the popup closes it.</param>
internal sealed record PortalFocusPublication(bool Modal, bool Open, bool CloseOnFocusOut);
