namespace Iodine.Headless.Interop;

// ADR 04 §3: one message per state change, never per move or per frame. Each report carries the module's revision
// (decision 6 (a), chosen by the user 2026-11-18). The kinds a viewport sends: `enter`, `leave`, `focus`, `blur`, `f6`,
// `window-blur`, `window-focus`, `window-focused` and `touch-outside`; a root: `height`, `swipe-start`, `direction`,
// `release`, `escape` and `touch-press`.
/// <summary>One report of the <c>toast</c> module, which the part applies to the provider's store or its mirrored state.</summary>
/// <param name="Revision">The module's revision of this report, which increases with each report of the handle.</param>
/// <param name="Kind">What happened, as a literal.</param>
internal sealed record ToastReport(int Revision, string Kind)
{
    /// <summary>Gets the measured height of a <c>height</c> report.</summary>
    /// <value>The height in pixels; <see langword="null"/> for the other kinds.</value>
    public double? Height { get; init; }

    /// <summary>Gets the lifecycle a <c>height</c> report was measured in.</summary>
    /// <value>The root's lifecycle; <see langword="null"/> for the other kinds.</value>
    public int? Lifecycle { get; init; }

    /// <summary>Gets the armed or dismissing direction of a <c>direction</c> or <c>release</c> report.</summary>
    /// <value><c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>; <see langword="null"/> for a release that snaps back.</value>
    public string? Direction { get; init; }

    /// <summary>Gets a value indicating whether a <c>release</c> dismisses the toast.</summary>
    /// <value><see langword="null"/> for the other kinds.</value>
    public bool? Dismiss { get; init; }

    /// <summary>Gets a value indicating whether a <c>window-focus</c> resumes the timers.</summary>
    /// <value><see langword="null"/> for the other kinds.</value>
    public bool? Resume { get; init; }
}
