namespace Blazix.Primitives;

// DLG-API-03: Base UI's `modal: boolean | 'trap-focus'` as the store holds it, so the record compares without a union.
/// <summary>Specifies the modality regime of a dialog.</summary>
internal enum DialogModality
{
    /// <summary>Focus is trapped, the outside is hidden and covered by the internal backdrop, and page scroll is locked.</summary>
    Modal,

    /// <summary>Focus is trapped and the outside is hidden, but the page stays scrollable and clickable.</summary>
    TrapFocus,

    /// <summary>Nothing is trapped or hidden, and focus leaving the popup closes it.</summary>
    NonModal,
}
