using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// SCR §3.1 (ScrollArea.Root.State); ADR 08 Amendments 2026-11-18 (one record per part, chosen by the user 2026-11-18).
// Scrolling is written by the `scroll-area` module and mirrored here (RND-STA-01); CornerHidden is in the state but never an
// attribute (SCR-DOM-04).
/// <summary>Represents the state of a scroll area root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Scrolling">Whether the user scrolled the viewport within the last 500 milliseconds; written as <c>data-scrolling</c> in the browser.</param>
/// <param name="HasOverflowX">Whether the content is wider than the viewport; projected as <c>data-has-overflow-x</c>.</param>
/// <param name="HasOverflowY">Whether the content is taller than the viewport; projected as <c>data-has-overflow-y</c>.</param>
/// <param name="OverflowXStart">Whether there is overflow on the inline start side; projected as <c>data-overflow-x-start</c>.</param>
/// <param name="OverflowXEnd">Whether there is overflow on the inline end side; projected as <c>data-overflow-x-end</c>.</param>
/// <param name="OverflowYStart">Whether there is overflow on the top side; projected as <c>data-overflow-y-start</c>.</param>
/// <param name="OverflowYEnd">Whether there is overflow on the bottom side; projected as <c>data-overflow-y-end</c>.</param>
/// <param name="CornerHidden">Whether the corner is hidden, because at least one axis does not overflow; never projected.</param>
[PartState]
public sealed partial record ScrollAreaRootState(
    [property: MirroredField] bool Scrolling,
    bool HasOverflowX,
    bool HasOverflowY,
    bool OverflowXStart,
    bool OverflowXEnd,
    bool OverflowYStart,
    bool OverflowYEnd,
    [property: NotProjected] bool CornerHidden);

// SCR §3.1; scroll-area/root/ScrollAreaRootDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a scroll area root renders.</summary>
public static class ScrollAreaRootDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the user scrolls inside the scroll area: <c>data-scrolling</c>.</summary>
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

// SCR §3.1; scroll-area/root/ScrollAreaRootCssVars.ts.
/// <summary>Provides the names of the CSS custom properties a scroll area root sets.</summary>
public static class ScrollAreaRootCssVars
{
    /// <summary>Represents the property that holds the corner's height, in pixels: <c>--scroll-area-corner-height</c>.</summary>
    public const string ScrollAreaCornerHeight = "--scroll-area-corner-height";

    /// <summary>Represents the property that holds the corner's width, in pixels: <c>--scroll-area-corner-width</c>.</summary>
    public const string ScrollAreaCornerWidth = "--scroll-area-corner-width";
}

// Base UI's ScrollArea.Root (scroll-area/root/ScrollAreaRoot.tsx); the contract is docs/components/scroll-area.md §3.1, §5.1,
// §5.2.
// - The only part that owns state (SCR-STR-01): a ScrollAreaRootContext cascaded fixed to every part.
// - Renders the scrollbar-hiding style element immediately before its own element unless the nearest CspProvider disables
//   style elements (SCR-DOM-05, UTL-DOM-32/33). One per emitter on prerender too (M5 track 4's design point 5, settled by
//   the orchestrator 2026-11-04; a dated deviation on DOM-05's de-duplication clause).
// - `role="presentation"` (SCR-ARIA-01) and `position: relative` with the two corner variables at 0px (SCR-DOM-01/02,
//   SCR-EDGE-13), the part's style under the consumer's (RND-DOM-16).
// - Draws one generated id, never rendered on the Root: the Viewport's and the Scrollbars' data-id suffix it
//   (SCR-DOM-09/21, RND-DOM-27).
/// <summary>
/// Represents the root of a scroll area: Base UI's <c>ScrollArea.Root</c>, rendered as a <c>div</c>. It groups the
/// viewport, its scrollbars and the corner, and holds the state they share.
/// </summary>
public sealed class ScrollAreaRoot : PartBase<ScrollAreaRootState>
{
    private const string DefaultTag = "div";
    private const string OwnStyle = "position: relative";
    private const string InitialStyle = "position: relative; --scroll-area-corner-height: 0px; --scroll-area-corner-width: 0px";

    private static readonly StateAttributesMapping<ScrollAreaRootState> s_mapping = ScrollAreaStateMapping.Create<ScrollAreaRootState>();

    private readonly Action contextChanged;
    private readonly Action directionChanged;
    private readonly Action<ScrollAreaOverflowReport> onOverflow;
    private readonly Action<ScrollAreaScrollingReport> onScrolling;
    private readonly Action<ScrollAreaHoverReport> onHovering;
    private ScrollAreaRootContext? context;
    private ScrollAreaHandle? handle;
    private ScrollAreaRootState? state;
    private ScrollAreaStateKey stateKey;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private string? sentStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaRoot"/> class.</summary>
    public ScrollAreaRoot()
    {
        contextChanged = OnContextChanged;
        directionChanged = StateHasChanged;
        onOverflow = OnOverflow;
        onScrolling = OnScrolling;
        onHovering = OnHovering;
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private CspProviderContext? Csp { get; set; }

    // SCR-I18N-01: the nearest DirectionProvider, never CSS (design point 7 (a) of the ScrollArea track).
    [CascadingParameter]
    private DirectionProviderContext? Direction { get; set; }

    /// <summary>Gets or sets the distance, in pixels, the viewport must be scrolled from an edge before that edge's overflow attribute applies.</summary>
    /// <value>
    /// One number for every edge, such as <c>OverflowEdgeThreshold="20"</c>, or a <see cref="ScrollAreaOverflowEdges"/> per
    /// edge, a missing edge being 0; a negative value counts as 0. The default is <see langword="null"/>, which is 0 for
    /// every edge.
    /// </value>
    [Parameter]
    public OverflowEdgeThresholdValue? OverflowEdgeThreshold { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ScrollAreaRootState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ScrollAreaRootState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets the state the root shares with its parts.</summary>
    /// <value>The context, created at the root's first parameter set.</value>
    internal ScrollAreaRootContext Context => context!;

    /// <summary>Gets the thresholds of the last parameter set, normalized.</summary>
    /// <value>Each at least 0.</value>
    internal ScrollAreaThresholds Thresholds { get; private set; }

    /// <summary>Gets a task that completes when the root's handle has attached.</summary>
    /// <value>A task that never faults.</value>
    internal Task TwinAttached => handle!.Attached;

    /// <summary>Gets a value indicating whether the browser-side behavior has measured the viewport at least once.</summary>
    internal bool Measured => context!.Measured;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        context = new ScrollAreaRootContext(IdGenerator.Next()) { RootElement = () => Element };
        context.SetRootListener(contextChanged);
        handle = new ScrollAreaHandle(JSRuntime, handsOverStyle: true);
        handle.Twin.OverflowReported += onOverflow;
        handle.Twin.ScrollingReported += onScrolling;
        handle.Twin.HoveringReported += onHovering;
        Direction?.AddListener(directionChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Thresholds = ScrollAreaThresholds.From(OverflowEdgeThreshold);
        UpdateState();

        // The cascade wraps the consumer's content inside the root's element; it is rebuilt only when the consumer supplied
        // other content (the Tabs precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context!.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        ScrollAreaThresholds thresholds = Thresholds;
        return handle!.SyncAsync(
            Element,
            new ScrollAreaOptions
            {
                Part = ScrollAreaTwin.RootPart,
                Style = sentStyle,
                Rtl = DirectionProviderContext.Read(Direction) == TextDirection.Rtl,
                Thresholds = new ScrollAreaThresholdOptions(thresholds.XStart, thresholds.XEnd, thresholds.YStart, thresholds.YEnd),
            },
            root: null);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            Direction?.RemoveListener(directionChanged);
            if (handle is not null)
            {
                handle.Twin.OverflowReported -= onOverflow;
                handle.Twin.ScrollingReported -= onScrolling;
                handle.Twin.HoveringReported -= onHovering;
                handle.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder)
    {
        builder.OpenRegion(0);
        DisableScrollbarStyle.Render(builder, Csp);
        builder.CloseRegion();
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
    }

    // The handover: C#'s declarations under the consumer's, the corner variables at 0px until the module owns them.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? consumer = ScrollAreaStyles.Consumer(attributes);
        sentStyle = MergeProps.MergeStyles(OwnStyle, consumer);
        ScrollAreaStyles.Set(attributes, handle!.RenderStyle(MergeProps.MergeStyles(InitialStyle, consumer)));
    }

    private void OnOverflow(ScrollAreaOverflowReport report) =>
        context!.ApplyOverflow(new ScrollAreaOverflow(report.HasOverflowX, report.HasOverflowY, report.XStart, report.XEnd, report.YStart, report.YEnd), report.Measured);

    private void OnScrolling(ScrollAreaScrollingReport report) => context!.MirrorScrolling(report.X, report.Y);

    private void OnHovering(ScrollAreaHoverReport report) => context!.MirrorHovering(report.Hovering);

    private void OnContextChanged()
    {
        // The projected overflow fields always render; the mirrored flag only for a state function (ADR 16 rule 8).
        if (ScrollAreaStateKey.From(context!, HasStateFunctions) != stateKey)
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        ScrollAreaStateKey key = ScrollAreaStateKey.From(context!, HasStateFunctions);
        if (state is not null && key == stateKey)
        {
            return false;
        }

        stateKey = key;
        ScrollAreaOverflow overflow = key.Overflow;
        state = new ScrollAreaRootState(key.Scrolling, overflow.HasOverflowX, overflow.HasOverflowY, overflow.XStart, overflow.XEnd, overflow.YStart, overflow.YEnd, context!.CornerHidden);
        return true;
    }
}

// What a Root, Viewport or Content renders from: the overflow fields always, the mirrored flag only while a state function
// reads it (ADR 16 rule 8: a render without one would change nothing on the page).
/// <summary>The fields a scroll area part with the root's state renders from.</summary>
/// <param name="Overflow">The overflow state.</param>
/// <param name="Scrolling">The mirrored scrolling flag, or <see langword="false"/> when no state function reads it.</param>
internal readonly record struct ScrollAreaStateKey(ScrollAreaOverflow Overflow, bool Scrolling)
{
    /// <summary>Reads the key from the context.</summary>
    /// <param name="context">The root's context.</param>
    /// <param name="mirrors">Whether the part has a state function that reads the mirrored flag.</param>
    /// <returns>The key.</returns>
    public static ScrollAreaStateKey From(ScrollAreaRootContext context, bool mirrors) => new(context.Overflow, mirrors && context.Scrolling);
}
