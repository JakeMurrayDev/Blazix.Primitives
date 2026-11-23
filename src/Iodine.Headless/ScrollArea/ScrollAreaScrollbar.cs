using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iodine.Headless;

// SCR §3.4 (ScrollArea.Scrollbar.State): the Root's fields with `scrolling` replaced by this axis's flag, plus `hovering` and
// `orientation` (SCR-STA-03); ADR 08 Amendments 2026-11-18 (one record per part). Scrolling and Hovering are written by the
// `scroll-area` module and mirrored here (RND-STA-01).
/// <summary>Represents the state of a scroll area scrollbar, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Scrolling">Whether the user scrolled the viewport along this scrollbar's axis within the last 500 milliseconds; written as <c>data-scrolling</c> in the browser.</param>
/// <param name="HasOverflowX">Whether the content is wider than the viewport; projected as <c>data-has-overflow-x</c>.</param>
/// <param name="HasOverflowY">Whether the content is taller than the viewport; projected as <c>data-has-overflow-y</c>.</param>
/// <param name="OverflowXStart">Whether there is overflow on the inline start side; projected as <c>data-overflow-x-start</c>.</param>
/// <param name="OverflowXEnd">Whether there is overflow on the inline end side; projected as <c>data-overflow-x-end</c>.</param>
/// <param name="OverflowYStart">Whether there is overflow on the top side; projected as <c>data-overflow-y-start</c>.</param>
/// <param name="OverflowYEnd">Whether there is overflow on the bottom side; projected as <c>data-overflow-y-end</c>.</param>
/// <param name="CornerHidden">Whether the corner is hidden, because at least one axis does not overflow; never projected.</param>
/// <param name="Hovering">Whether a pointer other than touch is over the scroll area; written as <c>data-hovering</c> in the browser.</param>
/// <param name="Orientation">The axis the scrollbar controls; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ScrollAreaScrollbarState(
    [property: MirroredField] bool Scrolling,
    bool HasOverflowX,
    bool HasOverflowY,
    bool OverflowXStart,
    bool OverflowXEnd,
    bool OverflowYStart,
    bool OverflowYEnd,
    [property: NotProjected] bool CornerHidden,
    [property: MirroredField] bool Hovering,
    Orientation Orientation);

// SCR §3.4; scroll-area/scrollbar/ScrollAreaScrollbarDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a scroll area scrollbar renders.</summary>
public static class ScrollAreaScrollbarDataAttributes
{
    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the pointer is over the scroll area: <c>data-hovering</c>.</summary>
    public const string Hovering = "data-hovering";

    /// <summary>Represents the attribute present, with an empty value, while the user scrolls along the scrollbar's axis: <c>data-scrolling</c>.</summary>
    public const string Scrolling = "data-scrolling";

    /// <summary>Represents the attribute present, with an empty value, while the content is wider than the viewport: <c>data-has-overflow-x</c>.</summary>
    public const string HasOverflowX = "data-has-overflow-x";

    /// <summary>Represents the attribute present, with an empty value, while the content is taller than the viewport: <c>data-has-overflow-y</c>.</summary>
    public const string HasOverflowY = "data-has-overflow-y";

    /// <summary>Represents the attribute present, with an empty value, while there is overflow on the horizontal start side: <c>data-overflow-x-start</c>.</summary>
    public const string OverflowXStart = "data-overflow-x-start";

    /// <summary>Represents the attribute present, with an empty value, while there is overflow on the horizontal end side: <c>data-overflow-x-end</c>.</summary>
    public const string OverflowXEnd = "data-overflow-x-end";

    /// <summary>Represents the attribute present, with an empty value, while there is overflow on the vertical start side: <c>data-overflow-y-start</c>.</summary>
    public const string OverflowYStart = "data-overflow-y-start";

    /// <summary>Represents the attribute present, with an empty value, while there is overflow on the vertical end side: <c>data-overflow-y-end</c>.</summary>
    public const string OverflowYEnd = "data-overflow-y-end";
}

// SCR §3.4; scroll-area/scrollbar/ScrollAreaScrollbarCssVars.ts.
/// <summary>Provides the names of the CSS custom properties a scroll area scrollbar sets.</summary>
public static class ScrollAreaScrollbarCssVars
{
    /// <summary>Represents the property that holds the thumb's height, in pixels, on a vertical scrollbar: <c>--scroll-area-thumb-height</c>.</summary>
    public const string ScrollAreaThumbHeight = "--scroll-area-thumb-height";

    /// <summary>Represents the property that holds the thumb's width, in pixels, on a horizontal scrollbar: <c>--scroll-area-thumb-width</c>.</summary>
    public const string ScrollAreaThumbWidth = "--scroll-area-thumb-width";
}

// Base UI's ScrollArea.Scrollbar (scroll-area/scrollbar/ScrollAreaScrollbar.tsx); the contract is docs/components/scroll-area.md
// §3.4, §5.5.
// - Rendered only while KeepMounted or its axis overflows (SCR-DOM-15), with no transition status (SCR-ANI-10); the axis's
//   overflow is C#-projected from the module's report.
// - `aria-hidden="true"` (SCR-ARIA-04), data-id = the Root's id + `-scrollbar` on both orientations (SCR-DOM-21, §16 Q1).
// - The inline styles of SCR-DOM-17..20 under the consumer's: position, touch-action, user-select, the insets, `visibility:
//   hidden` while not measured and not kept mounted (§16 Q3: unreachable, kept as the source does), and the thumb-size
//   variable at 0px before the first measurement (SCR-EDGE-13).
// - Cascades its orientation to its Thumb (SCR-STR-05).
/// <summary>
/// Represents a scrollbar of a scroll area: Base UI's <c>ScrollArea.Scrollbar</c>, rendered as a <c>div</c> laid over one
/// edge of the viewport.
/// </summary>
/// <remarks>Without <see cref="KeepMounted"/>, the scrollbar renders nothing while its axis does not overflow.</remarks>
public sealed class ScrollAreaScrollbar : PartBase<ScrollAreaScrollbarState>
{
    private const string DefaultTag = "div";
    private const string CommonStyle = "position: absolute; touch-action: none; -webkit-user-select: none; user-select: none; ";
    private const string HiddenStyle = "visibility: hidden; ";
    private const string VerticalStyle = "top: 0; bottom: var(--scroll-area-corner-height); inset-inline-end: 0";
    private const string HorizontalStyle = "inset-inline-start: 0; inset-inline-end: var(--scroll-area-corner-width); bottom: 0";
    private const string VerticalThumbSize = "; --scroll-area-thumb-height: 0px";
    private const string HorizontalThumbSize = "; --scroll-area-thumb-width: 0px";

    private static readonly StateAttributesMapping<ScrollAreaScrollbarState> s_mapping = ScrollAreaStateMapping.Create<ScrollAreaScrollbarState>();

    private readonly ScrollAreaScrollbarContext scrollbarContext = new();
    private readonly Action contextChanged;
    private ScrollAreaRootContext? context;
    private ScrollAreaScrollbarState? state;
    private ScrollbarKey stateKey;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private string? dataId;
    private ScrollAreaHandle? handle;
    private string? sentStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaScrollbar"/> class.</summary>
    public ScrollAreaScrollbar()
    {
        contextChanged = OnContextChanged;
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    private bool Vertical => Orientation == Orientation.Vertical;

    // SCR-DOM-15: `keepMounted || !hidden<axis>`.
    private bool Shown => KeepMounted || (Vertical ? context!.Overflow.HasOverflowY : context!.Overflow.HasOverflowX);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ScrollAreaRootContext? RootContext { get; set; }

    /// <summary>Gets or sets the axis the scrollbar controls.</summary>
    /// <value>The default is <see cref="Orientation.Vertical"/>.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    /// <summary>Gets or sets a value indicating whether the scrollbar stays rendered while its axis does not overflow.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ScrollAreaScrollbarState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ScrollAreaScrollbarState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    internal Task TwinAttached => handle!.Attached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The scrollbar is not placed within a <see cref="ScrollAreaRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ScrollAreaRootContext.Require(RootContext);
        dataId = context.RootId + "-scrollbar";
        context.AddListener(contextChanged);
        handle = new ScrollAreaHandle(JSRuntime, handsOverStyle: true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        scrollbarContext.SetOrientation(Orientation);
        UpdateState();
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = scrollbarContext.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!Shown)
        {
            // SCR-DOM-15: no element, so no handle.
            UpdateState();
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    // SCR-DOM-15: an element that left disposes its handle; a new one attaches its own.
    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => disposed
        ? Task.CompletedTask
        : handle!.SyncAsync(
            Element,
            new ScrollAreaOptions { Part = ScrollAreaTwin.ScrollbarPart, RootRef = 0, Orientation = Vertical ? "vertical" : "horizontal", Style = sentStyle },
            context!.RootElement());

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveListener(contextChanged);
            handle?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("data-id", dataId!);
        attributes.Set("aria-hidden", "true");
    }

    // The handover: C#'s declarations under the consumer's; the thumb-size variable at 0px until the module owns it.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        bool hideTrack = !stateKey.Measured && !KeepMounted;
        string own = Vertical
            ? hideTrack ? CommonStyle + HiddenStyle + VerticalStyle : CommonStyle + VerticalStyle
            : hideTrack ? CommonStyle + HiddenStyle + HorizontalStyle : CommonStyle + HorizontalStyle;
        string? consumer = ScrollAreaStyles.Consumer(attributes);
        sentStyle = MergeProps.MergeStyles(own, consumer);
        ScrollAreaStyles.Set(attributes, handle!.RenderStyle(MergeProps.MergeStyles(own + (Vertical ? VerticalThumbSize : HorizontalThumbSize), consumer)));
    }

    private void OnContextChanged()
    {
        if (ScrollbarKey.From(context!, Orientation, HasStateFunctions) != stateKey)
        {
            StateHasChanged();
        }
    }

    private bool UpdateState()
    {
        ScrollbarKey key = ScrollbarKey.From(context!, Orientation, HasStateFunctions);
        if (state is not null && key == stateKey)
        {
            return false;
        }

        stateKey = key;
        ScrollAreaOverflow overflow = key.Overflow;
        state = new ScrollAreaScrollbarState(key.Scrolling, overflow.HasOverflowX, overflow.HasOverflowY, overflow.XStart, overflow.XEnd, overflow.YStart, overflow.YEnd, context!.CornerHidden, key.Hovering, key.Orientation);
        return true;
    }

    // What the scrollbar renders from: the overflow, the measured flag (the track's visibility) and the orientation always;
    // the mirrored flags only while a state function reads them (ADR 16 rule 8).
    private readonly record struct ScrollbarKey(ScrollAreaOverflow Overflow, bool Measured, Orientation Orientation, bool Scrolling, bool Hovering)
    {
        public static ScrollbarKey From(ScrollAreaRootContext context, Orientation orientation, bool mirrors) => new(
            context.Overflow,
            context.Measured,
            orientation,
            mirrors && (orientation == Orientation.Vertical ? context.ScrollingY : context.ScrollingX),
            mirrors && context.Hovering);
    }
}
