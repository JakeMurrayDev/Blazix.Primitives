namespace Iodine.Headless;

// TST §3.2, the toast object's `priority` ('low' | 'high'; useToastManager.ts:57-63); ADR 08 §3 (Enums row), placed
// beside its first consumer, ToastObject<TData>, and named in §3's companion table by a dated note (M6 track 9a).
/// <summary>Specifies how urgently a toast is announced to assistive technology.</summary>
public enum ToastPriority
{
    /// <summary>The toast is announced politely, through the viewport's live region (<c>low</c>).</summary>
    Low,

    /// <summary>
    /// The toast is announced urgently, through a hidden alert, and its root renders with the <c>alertdialog</c> role
    /// (<c>high</c>).
    /// </summary>
    High,
}
