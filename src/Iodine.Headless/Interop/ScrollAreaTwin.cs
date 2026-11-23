using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `scroll-area` component module), §2 (the ScrollArea rows as corrected 2026-11-18) and Amendments
// 2026-11-18: the module measures on every viewport scroll and resize, runs the thumb drag, the track press and the wheel
// over a scrollbar, tracks hover and the scrolling flags, and writes data-scrolling, data-hovering and the measured styles.
// One twin per part (design point 6 (a) of the ScrollArea track); only the root's receives reports. No decision point
// (ADR 04 Amendments 2026-11-18): a C# consumer handler runs after the module and cannot veto it (ADR 01's C#-origin
// consumer-veto row).
/// <summary>
/// The C# half of the <c>scroll-area</c> module for one part of a scroll area; on the root, the receiver of the overflow,
/// scrolling and hovering reports.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class ScrollAreaTwin(IJSRuntime jsRuntime)
    : TwinBase<ScrollAreaTwin, ScrollAreaOptions>(jsRuntime, LibraryModule("scroll-area"), BlazixJsonContext.Default.ScrollAreaOptions)
{
    /// <summary>Represents the part literal of a root's handle.</summary>
    public const string RootPart = "root";

    /// <summary>Represents the part literal of a viewport's handle.</summary>
    public const string ViewportPart = "viewport";

    /// <summary>Represents the part literal of a content's handle.</summary>
    public const string ContentPart = "content";

    /// <summary>Represents the part literal of a scrollbar's handle.</summary>
    public const string ScrollbarPart = "scrollbar";

    /// <summary>Represents the part literal of a thumb's handle.</summary>
    public const string ThumbPart = "thumb";

    /// <summary>Represents the part literal of a corner's handle.</summary>
    public const string CornerPart = "corner";

    /// <summary>Occurs when a measurement changed the overflow state or measured the viewport for the first time.</summary>
    public event Action<ScrollAreaOverflowReport>? OverflowReported;

    /// <summary>Occurs when the module changed a scrolling flag.</summary>
    public event Action<ScrollAreaScrollingReport>? ScrollingReported;

    /// <summary>Occurs when the module changed the hovering flag.</summary>
    public event Action<ScrollAreaHoverReport>? HoveringReported;

    /// <summary>Receives a change of the overflow state.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ScrollAreaOverflowReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnOverflow(string payloadJson)
    {
        OverflowReported?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ScrollAreaOverflowReport));
    }

    /// <summary>Receives a change of the scrolling flags.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ScrollAreaScrollingReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnScrolling(string payloadJson)
    {
        ScrollingReported?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ScrollAreaScrollingReport));
    }

    /// <summary>Receives a change of the hovering flag.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ScrollAreaHoverReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnHovering(string payloadJson)
    {
        HoveringReported?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ScrollAreaHoverReport));
    }

    /// <inheritdoc />
    /// <remarks>The module asks no decision point, so every point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}
