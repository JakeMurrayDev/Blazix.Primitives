namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `drawer` component module) and Amendments 2026-11-18 (the `dismiss` and `snap-point` points; decision
// 21 (a)); M6 track 8b, issue 279. Slice 1: the Viewport handle of the mouse and pen dismiss gesture. AnsweredDismiss
// is the revision rule of design point 10 (a) (chosen by the user 2026-11-19): the module rolls a dismissal back when
// an update answers it while the drawer is still open.
/// <summary>
/// The options of the <c>drawer</c> module, sent on <c>attach</c> and on every <c>update</c>: the state a drawer's
/// viewport needs to run the swipe gesture on its popup.
/// </summary>
internal sealed record DrawerOptions : TwinOptions
{
    /// <summary>Gets the part the handle serves.</summary>
    /// <value><see cref="DrawerTwin.ViewportRole"/>, the default.</value>
    public string Role { get; init; } = DrawerTwin.ViewportRole;

    /// <summary>Gets a value indicating whether the drawer is open.</summary>
    /// <value><see langword="true"/> while open; the default is <see langword="false"/>.</value>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether the drawer's popup is mounted.</summary>
    /// <value><see langword="true"/> while mounted; the default is <see langword="false"/>.</value>
    public bool Mounted { get; init; }

    /// <summary>Gets a value indicating whether the drawer is nested in another dialog.</summary>
    /// <value><see langword="true"/> while nested; the default is <see langword="false"/>.</value>
    public bool Nested { get; init; }

    /// <summary>Gets how many drawers nested in this one are open; a gesture starts only while none is.</summary>
    /// <value>The count; the default is 0.</value>
    public int NestedOpenDrawerCount { get; init; }

    /// <summary>Gets the direction the drawer is swiped to dismiss it.</summary>
    /// <value><c>down</c>, the default, <c>up</c>, <c>left</c> or <c>right</c>.</value>
    public string SwipeDirection { get; init; } = "down";

    /// <summary>Gets the number of the last dismiss request the drawer has answered.</summary>
    /// <value>The number; the default is 0, for none.</value>
    public int AnsweredDismiss { get; init; }

    /// <summary>Gets the index of the popup element among the references sent with the options.</summary>
    /// <value>The index, or <see langword="null"/>, the default, while no popup is rendered.</value>
    public int? Popup { get; init; }

    /// <summary>Gets the index of the backdrop element among the references sent with the options.</summary>
    /// <value>The index, or <see langword="null"/>, the default, while no backdrop is rendered.</value>
    public int? Backdrop { get; init; }
}
