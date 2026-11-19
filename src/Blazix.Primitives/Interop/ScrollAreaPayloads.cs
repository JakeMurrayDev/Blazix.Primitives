namespace Blazix.Primitives.Interop;

// Design point 3 (a) of the ScrollArea track: one report per change, never per frame (ADR 04 §3); a scroll through the
// middle of the content reports nothing (SCR-STA-20).

/// <summary>The overflow state a measurement found, reported once per change.</summary>
/// <param name="HasOverflowX">Whether the content is wider than the viewport.</param>
/// <param name="HasOverflowY">Whether the content is taller than the viewport.</param>
/// <param name="XStart">Whether the viewport is scrolled from the inline start edge by more than its threshold.</param>
/// <param name="XEnd">Whether the viewport is scrolled from the inline end edge by more than its threshold.</param>
/// <param name="YStart">Whether the viewport is scrolled from the top edge by more than its threshold.</param>
/// <param name="YEnd">Whether the viewport is scrolled from the bottom edge by more than its threshold.</param>
/// <param name="Measured">Whether the viewport has been measured at least once.</param>
internal sealed record ScrollAreaOverflowReport(bool HasOverflowX, bool HasOverflowY, bool XStart, bool XEnd, bool YStart, bool YEnd, bool Measured);

/// <summary>The scrolling flags the module wrote, reported once per change.</summary>
/// <param name="X">Whether the horizontal axis was scrolled by the user within the last 500 milliseconds.</param>
/// <param name="Y">Whether the vertical axis was scrolled by the user within the last 500 milliseconds.</param>
internal sealed record ScrollAreaScrollingReport(bool X, bool Y);

/// <summary>The hovering flag the module wrote, reported once per change.</summary>
/// <param name="Hovering">Whether a pointer other than touch is over the scroll area.</param>
internal sealed record ScrollAreaHoverReport(bool Hovering);
