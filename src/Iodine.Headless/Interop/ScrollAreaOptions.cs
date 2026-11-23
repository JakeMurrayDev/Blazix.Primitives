namespace Iodine.Headless.Interop;

// ADR 04 Amendments 2026-11-18 (the `scroll-area` scope cell); design points 3 (a), 6 (a) and 7 (a) of the ScrollArea
// track: one handle per part, each told its part literal, the root element among its references and, for a scrollbar or a
// thumb, its axis; the root's handle also carries the reading direction and the four normalized thresholds. Values never
// cross: the measurement, the thumb and corner sizes and the flags are the module's, which reports what C# projects.
/// <summary>
/// The options of the <c>scroll-area</c> module, sent on <c>attach</c> and on every <c>update</c> of one part's handle.
/// </summary>
internal sealed record ScrollAreaOptions : TwinOptions
{
    /// <summary>Gets the part the handle serves.</summary>
    /// <value>One of the part literals of <see cref="ScrollAreaTwin"/>; the default is <see cref="ScrollAreaTwin.RootPart"/>.</value>
    public string Part { get; init; } = ScrollAreaTwin.RootPart;

    /// <summary>Gets the index of the root element among the handle's references.</summary>
    /// <value>0 for every part but the root; <see langword="null"/> for the root.</value>
    public int? RootRef { get; init; }

    /// <summary>Gets the axis of a scrollbar or a thumb.</summary>
    /// <value><c>vertical</c> or <c>horizontal</c>; <see langword="null"/> for the other parts.</value>
    public string? Orientation { get; init; }

    /// <summary>Gets a value indicating whether the root's reading direction is right to left.</summary>
    /// <value>Sent by the root only; <see langword="null"/> for the other parts.</value>
    public bool? Rtl { get; init; }

    /// <summary>Gets the root's four normalized overflow edge thresholds.</summary>
    /// <value>Sent by the root only; <see langword="null"/> for the other parts.</value>
    public ScrollAreaThresholdOptions? Thresholds { get; init; }
}

/// <summary>The four normalized overflow edge thresholds a scroll area root sends, in CSS pixels.</summary>
/// <param name="XStart">The threshold of the inline start edge.</param>
/// <param name="XEnd">The threshold of the inline end edge.</param>
/// <param name="YStart">The threshold of the top edge.</param>
/// <param name="YEnd">The threshold of the bottom edge.</param>
internal sealed record ScrollAreaThresholdOptions(double XStart, double XEnd, double YStart, double YEnd);
