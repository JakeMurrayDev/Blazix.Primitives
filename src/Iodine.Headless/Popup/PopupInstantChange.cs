namespace Blazix.Primitives;

// EVD-EVT-17, PSH-ANI-04: how a committed open change moves the shared stores' instant phase (popupStoreUtils.ts:261-304).
// Each family maps it to its own enum, rendered as data-instant (ADR 08 Amendments 2026-10-27).
/// <summary>Specifies how a committed open change moves a popup's instant phase.</summary>
internal enum PopupInstantChange
{
    /// <summary>The phase keeps its value.</summary>
    Unchanged,

    /// <summary>The phase is cleared: a hover change.</summary>
    Clear,

    /// <summary>The phase becomes focus: an open on trigger focus.</summary>
    Focus,

    /// <summary>The phase becomes dismiss: a close on trigger press or Escape.</summary>
    Dismiss,
}
