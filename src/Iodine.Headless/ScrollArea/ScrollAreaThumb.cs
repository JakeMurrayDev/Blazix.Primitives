using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SCR §3.5 (ScrollArea.Thumb.State): exactly `scrolling`, this axis's flag, and `orientation` (SCR-STA-04); ADR 08
// Amendments 2026-11-18. Scrolling is written by the `scroll-area` module and mirrored here (RND-STA-01).
/// <summary>Represents the state of a scroll area thumb, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Scrolling">Whether the user scrolled the viewport along the thumb's axis within the last 500 milliseconds; written as <c>data-scrolling</c> in the browser.</param>
/// <param name="Orientation">The axis of the enclosing scrollbar; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ScrollAreaThumbState(
    [property: MirroredField] bool Scrolling,
    Orientation Orientation);

// SCR §3.5; scroll-area/thumb/ScrollAreaThumbDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a scroll area thumb renders.</summary>
public static class ScrollAreaThumbDataAttributes
{
    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the user scrolls along the thumb's axis: <c>data-scrolling</c>.</summary>
    public const string Scrolling = "data-scrolling";
}

// Base UI's ScrollArea.Thumb (scroll-area/thumb/ScrollAreaThumb.tsx); the contract is docs/components/scroll-area.md §3.5,
// §5.6.
// - Binds to the Root, then to its Scrollbar (the source's order), so a Thumb outside both throws SCR-EDGE-01's error and one
//   inside a Root but outside a Scrollbar SCR-EDGE-03's (SCR-STR-05).
// - `visibility: hidden` until the first measurement, kept mounted or not, and its length from the Scrollbar's variable,
//   under the consumer's style (SCR-DOM-23). The transform and the overscroll variable are the module's (SCR-DOM-24/25).
// - No overflow or hovering attributes (SCR-DOM-22): the default projection.
/// <summary>
/// Represents the thumb of a scroll area scrollbar: Base UI's <c>ScrollArea.Thumb</c>, rendered as a <c>div</c> whose length
/// and position follow the viewport's scroll.
/// </summary>
public sealed class ScrollAreaThumb : PartBase<ScrollAreaThumbState>
{
    private const string DefaultTag = "div";
    private const string VerticalStyle = "height: var(--scroll-area-thumb-height)";
    private const string HorizontalStyle = "width: var(--scroll-area-thumb-width)";
    private const string HiddenVerticalStyle = "visibility: hidden; height: var(--scroll-area-thumb-height)";
    private const string HiddenHorizontalStyle = "visibility: hidden; width: var(--scroll-area-thumb-width)";

    private readonly Action contextChanged;
    private ScrollAreaRootContext? context;
    private ScrollAreaScrollbarContext? scrollbar;
    private ScrollAreaThumbState? state;
    private ThumbKey stateKey;
    private ScrollAreaHandle? handle;
    private string? sentStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaThumb"/> class.</summary>
    public ScrollAreaThumb()
    {
        contextChanged = OnContextChanged;
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ScrollAreaRootContext? RootContext { get; set; }

    [CascadingParameter]
    private ScrollAreaScrollbarContext? ScrollbarContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ScrollAreaThumbState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    internal Task TwinAttached => handle!.Attached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The thumb is not placed within a <see cref="ScrollAreaScrollbar"/> of a <see cref="ScrollAreaRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ScrollAreaRootContext.Require(RootContext);
        scrollbar = ScrollAreaScrollbarContext.Require(ScrollbarContext);
        context.AddListener(contextChanged);
        scrollbar.SetListener(contextChanged);
        handle = new ScrollAreaHandle(JSRuntime, handsOverStyle: true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => UpdateState();

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => disposed
        ? Task.CompletedTask
        : handle!.SyncAsync(
            Element,
            new ScrollAreaOptions { Part = ScrollAreaTwin.ThumbPart, RootRef = 0, Orientation = stateKey.Orientation == Orientation.Vertical ? "vertical" : "horizontal", Style = sentStyle },
            context!.RootElement());

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveListener(contextChanged);
            scrollbar?.SetListener(null);
            handle?.Dispose();
        }

        base.Dispose(disposing);
    }

    // The handover: C#'s declarations under the consumer's; the module writes the transform and the overscroll variable
    // over it.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        bool vertical = stateKey.Orientation == Orientation.Vertical;
        string own = stateKey.Measured
            ? vertical ? VerticalStyle : HorizontalStyle
            : vertical ? HiddenVerticalStyle : HiddenHorizontalStyle;
        sentStyle = MergeProps.MergeStyles(own, ScrollAreaStyles.Consumer(attributes));
        ScrollAreaStyles.Set(attributes, handle!.RenderStyle(sentStyle));
    }

    private void OnContextChanged()
    {
        if (ThumbKey.From(context!, scrollbar!.Orientation, HasStateFunctions) != stateKey)
        {
            StateHasChanged();
        }
    }

    private bool UpdateState()
    {
        ThumbKey key = ThumbKey.From(context!, scrollbar!.Orientation, HasStateFunctions);
        if (state is not null && key == stateKey)
        {
            return false;
        }

        stateKey = key;
        state = new ScrollAreaThumbState(key.Scrolling, key.Orientation);
        return true;
    }

    // What the thumb renders from: the measured flag (its visibility) and the orientation always; the mirrored flag only
    // while a state function reads it (ADR 16 rule 8).
    private readonly record struct ThumbKey(bool Measured, Orientation Orientation, bool Scrolling)
    {
        public static ThumbKey From(ScrollAreaRootContext context, Orientation orientation, bool mirrors) => new(
            context.Measured,
            orientation,
            mirrors && (orientation == Orientation.Vertical ? context.ScrollingY : context.ScrollingX));
    }
}
