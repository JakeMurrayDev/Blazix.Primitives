using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// SCR §3.2: the Root's state object unchanged (SCR-STA-02); ADR 08 Amendments 2026-11-18 (one record per part).
/// <summary>Represents the state of a scroll area viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Scrolling">Whether the user scrolled the viewport within the last 500 milliseconds; written as <c>data-scrolling</c> in the browser.</param>
/// <param name="HasOverflowX">Whether the content is wider than the viewport; projected as <c>data-has-overflow-x</c>.</param>
/// <param name="HasOverflowY">Whether the content is taller than the viewport; projected as <c>data-has-overflow-y</c>.</param>
/// <param name="OverflowXStart">Whether there is overflow on the inline start side; projected as <c>data-overflow-x-start</c>.</param>
/// <param name="OverflowXEnd">Whether there is overflow on the inline end side; projected as <c>data-overflow-x-end</c>.</param>
/// <param name="OverflowYStart">Whether there is overflow on the top side; projected as <c>data-overflow-y-start</c>.</param>
/// <param name="OverflowYEnd">Whether there is overflow on the bottom side; projected as <c>data-overflow-y-end</c>.</param>
/// <param name="CornerHidden">Whether the corner is hidden, because at least one axis does not overflow; never projected.</param>
[PartState]
public sealed partial record ScrollAreaViewportState(
    [property: MirroredField] bool Scrolling,
    bool HasOverflowX,
    bool HasOverflowY,
    bool OverflowXStart,
    bool OverflowXEnd,
    bool OverflowYStart,
    bool OverflowYEnd,
    [property: NotProjected] bool CornerHidden);

// SCR §3.2; scroll-area/viewport/ScrollAreaViewportDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a scroll area viewport renders.</summary>
public static class ScrollAreaViewportDataAttributes
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

// SCR §3.2; scroll-area/viewport/ScrollAreaViewportCssVars.ts.
/// <summary>Provides the names of the CSS custom properties a scroll area viewport sets.</summary>
/// <remarks>The properties do not inherit, except in WebKit browsers; a descendant reads one by declaring it <c>inherit</c>.</remarks>
public static class ScrollAreaViewportCssVars
{
    /// <summary>Represents the property that holds the distance from the horizontal start edge, in pixels: <c>--scroll-area-overflow-x-start</c>.</summary>
    public const string ScrollAreaOverflowXStart = "--scroll-area-overflow-x-start";

    /// <summary>Represents the property that holds the distance from the horizontal end edge, in pixels: <c>--scroll-area-overflow-x-end</c>.</summary>
    public const string ScrollAreaOverflowXEnd = "--scroll-area-overflow-x-end";

    /// <summary>Represents the property that holds the distance from the vertical start edge, in pixels: <c>--scroll-area-overflow-y-start</c>.</summary>
    public const string ScrollAreaOverflowYStart = "--scroll-area-overflow-y-start";

    /// <summary>Represents the property that holds the distance from the vertical end edge, in pixels: <c>--scroll-area-overflow-y-end</c>.</summary>
    public const string ScrollAreaOverflowYEnd = "--scroll-area-overflow-y-end";
}

// Base UI's ScrollArea.Viewport (scroll-area/viewport/ScrollAreaViewport.tsx); the contract is docs/components/scroll-area.md
// §3.2, §5.3.
// - The class that hides the native scrollbars (SCR-DOM-06), `overflow: scroll` under the consumer's style (SCR-DOM-07).
// - `tabindex` 0 while either axis overflows and -1 otherwise (SCR-DOM-08); a consumer tabindex stays in the unmatched bag,
//   merged last, and replaces it (SCR-API-05, RND-DOM-19).
// - `data-id` = the Root's id + `-viewport` (SCR-DOM-09; §16 Q1 keeps it as rendered); no role (SCR-ARIA-03).
// - The overflow attributes are C#-projected from the module's report (ADR 04 Amendments 2026-11-18).
/// <summary>
/// Represents the scroll container of a scroll area: Base UI's <c>ScrollArea.Viewport</c>, rendered as a <c>div</c> whose
/// native scrollbars are hidden.
/// </summary>
/// <remarks>The viewport is in the tab order only while its content overflows.</remarks>
public sealed class ScrollAreaViewport : PartBase<ScrollAreaViewportState>
{
    private const string DefaultTag = "div";
    private const string PartStyle = "overflow: scroll";

    private static readonly StateAttributesMapping<ScrollAreaViewportState> s_mapping = ScrollAreaStateMapping.Create<ScrollAreaViewportState>();

    private readonly ScrollAreaViewportContext viewportContext = new();
    private readonly Action contextChanged;
    private ScrollAreaRootContext? context;
    private ScrollAreaViewportState? state;
    private ScrollAreaStateKey stateKey;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private string? dataId;
    private ScrollAreaHandle? handle;
    private string? sentStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaViewport"/> class.</summary>
    public ScrollAreaViewport()
    {
        contextChanged = OnContextChanged;
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ScrollAreaRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ScrollAreaViewportState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ScrollAreaViewportState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    internal Task TwinAttached => handle!.Attached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The viewport is not placed within a <see cref="ScrollAreaRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ScrollAreaRootContext.Require(RootContext);
        dataId = context.RootId + "-viewport";
        context.AddListener(contextChanged);
        handle = new ScrollAreaHandle(JSRuntime, handsOverStyle: true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        UpdateState();
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = viewportContext.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => disposed
        ? Task.CompletedTask
        : handle!.SyncAsync(Element, new ScrollAreaOptions { Part = ScrollAreaTwin.ViewportPart, RootRef = 0, Style = sentStyle }, context!.RootElement());

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
        ScrollAreaOverflow overflow = stateKey.Overflow;
        attributes.Set("data-id", dataId!);
        attributes.Set("tabindex", overflow.HasOverflowX || overflow.HasOverflowY ? "0" : "-1");
        attributes.Set(MergeProps.ClassAttribute, DisableScrollbarStyle.ClassName);
    }

    // The handover: `overflow: scroll` under the consumer's style; the module writes the overflow variables and, during a
    // drag, scroll-snap-type over it.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        sentStyle = MergeProps.MergeStyles(PartStyle, ScrollAreaStyles.Consumer(attributes));
        ScrollAreaStyles.Set(attributes, handle!.RenderStyle(sentStyle));
    }

    private void OnContextChanged()
    {
        if (ScrollAreaStateKey.From(context!, HasStateFunctions) != stateKey)
        {
            StateHasChanged();
        }
    }

    private bool UpdateState()
    {
        ScrollAreaStateKey key = ScrollAreaStateKey.From(context!, HasStateFunctions);
        if (state is not null && key == stateKey)
        {
            return false;
        }

        stateKey = key;
        ScrollAreaOverflow overflow = key.Overflow;
        state = new ScrollAreaViewportState(key.Scrolling, overflow.HasOverflowX, overflow.HasOverflowY, overflow.XStart, overflow.XEnd, overflow.YStart, overflow.YEnd, context!.CornerHidden);
        return true;
    }
}
