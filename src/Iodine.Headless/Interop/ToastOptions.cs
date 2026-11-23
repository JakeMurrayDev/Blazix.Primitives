namespace Iodine.Headless.Interop;

// ADR 04 Amendments 2026-11-18 (the `toast` scope cell) and 2026-11-19 (the 9b design points DP1 and DP3, chosen by the
// user): one handle per part, told its part literal. The Viewport sends the store's flags the module's listeners read, the
// first focusable toast for the guards and the hand-off of the latest close; the Root its dismiss directions and lifecycle;
// the Content its Root's element. The Viewport and the Root also send the revision of the last report they applied
// (decision 6 (a), chosen by the user 2026-11-18). Unused fields are null and are not written.
/// <summary>The options of the <c>toast</c> module, sent on <c>attach</c> and on every <c>update</c> of one part's handle.</summary>
internal sealed record ToastOptions : TwinOptions
{
    /// <summary>Gets the part the handle serves.</summary>
    /// <value><c>viewport</c>, <c>root</c> or <c>content</c>; the default is <c>root</c>.</value>
    public string Part { get; init; } = "root";

    /// <summary>Gets the revision of the last report the part applied.</summary>
    /// <value>Sent by the viewport and the root; <see langword="null"/> for the content.</value>
    public int? Revision { get; init; }

    /// <summary>Gets a value indicating whether the provider holds no toast.</summary>
    /// <value>Sent by the viewport, whose window and document listeners exist only while toasts do.</value>
    public bool? Empty { get; init; }

    /// <summary>Gets a value indicating whether the store records the pointer over the viewport or a swipe.</summary>
    /// <value>Sent by the viewport.</value>
    public bool? Hovering { get; init; }

    /// <summary>Gets a value indicating whether the store records keyboard focus inside the viewport.</summary>
    /// <value>Sent by the viewport.</value>
    public bool? Focused { get; init; }

    /// <summary>Gets a value indicating whether some toast is closing, which defers the collapse of a pointer leaving.</summary>
    /// <value>Sent by the viewport.</value>
    public bool? HasEnding { get; init; }

    /// <summary>Gets the index, among the references, of the first toast that is neither closing nor limited.</summary>
    /// <value>Sent by the viewport; <see langword="null"/> when no such toast has an element.</value>
    public int? FocusTargetRef { get; init; }

    /// <summary>Gets the focus hand-off of the latest close.</summary>
    /// <value>Sent by the viewport; <see langword="null"/> before the first close.</value>
    public ToastHandOffOptions? HandOff { get; init; }

    /// <summary>Gets the directions in which a swipe dismisses the toast, in the order a release tries them.</summary>
    /// <value>Sent by the root: <c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>; empty for an anchored toast.</value>
    public IReadOnlyList<string>? Directions { get; init; }

    /// <summary>Gets the root's lifecycle, which increases when it starts rendering another toast or a toast added again.</summary>
    /// <value>Sent by the root; a change resets the swipe state and measures the toast again, whatever its revision.</value>
    public int? Lifecycle { get; init; }

    /// <summary>Gets the index, among the references, of the content's root element.</summary>
    /// <value>Sent by the content; <see langword="null"/> for the other parts.</value>
    public int? RootRef { get; init; }
}

// DP3 (i) of the Toast track (chosen by the user 2026-11-19): C# names the target at the close; the module focuses it once,
// re-checking that focus-visible focus is inside the viewport and the target can take it.
/// <summary>The focus hand-off a close computed.</summary>
/// <param name="Revision">The close's number, which increases with each close; the module acts on a number once.</param>
/// <param name="ToPrevious">Whether focus returns to the element focused before F6 rather than to a toast.</param>
/// <param name="TargetRef">The index, among the references, of the toast to focus; <see langword="null"/> otherwise.</param>
internal sealed record ToastHandOffOptions(int Revision, bool ToPrevious, int? TargetRef);
