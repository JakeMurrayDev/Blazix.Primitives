using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SCR §3.3: the Root's state object unchanged (SCR-STA-02); ADR 08 Amendments 2026-11-18 (one record per part).
/// <summary>Represents the state of a scroll area content, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Scrolling">Whether the user scrolled the viewport within the last 500 milliseconds; written as <c>data-scrolling</c> in the browser.</param>
/// <param name="HasOverflowX">Whether the content is wider than the viewport; projected as <c>data-has-overflow-x</c>.</param>
/// <param name="HasOverflowY">Whether the content is taller than the viewport; projected as <c>data-has-overflow-y</c>.</param>
/// <param name="OverflowXStart">Whether there is overflow on the inline start side; projected as <c>data-overflow-x-start</c>.</param>
/// <param name="OverflowXEnd">Whether there is overflow on the inline end side; projected as <c>data-overflow-x-end</c>.</param>
/// <param name="OverflowYStart">Whether there is overflow on the top side; projected as <c>data-overflow-y-start</c>.</param>
/// <param name="OverflowYEnd">Whether there is overflow on the bottom side; projected as <c>data-overflow-y-end</c>.</param>
/// <param name="CornerHidden">Whether the corner is hidden, because at least one axis does not overflow; never projected.</param>
[PartState]
public sealed partial record ScrollAreaContentState(
    [property: MirroredField] bool Scrolling,
    bool HasOverflowX,
    bool HasOverflowY,
    bool OverflowXStart,
    bool OverflowXEnd,
    bool OverflowYStart,
    bool OverflowYEnd,
    [property: NotProjected] bool CornerHidden);

// SCR §3.3; scroll-area/content/ScrollAreaContentDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a scroll area content renders.</summary>
public static class ScrollAreaContentDataAttributes
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

// Base UI's ScrollArea.Content (scroll-area/content/ScrollAreaContent.tsx); the contract is docs/components/scroll-area.md
// §3.3, §5.4.
// - Inside a Viewport (SCR-STR-03): the viewport's context is read first, as Base UI does (`useScrollAreaViewportContext`
//   before `useScrollAreaRootContext`), so a Content outside both throws SCR-EDGE-02's error.
// - `role="presentation"` (SCR-ARIA-02) and `min-width: fit-content` under the consumer's style (SCR-DOM-13), rendered by C#
//   throughout: the module writes no style here.
/// <summary>
/// Represents the wrapper of a scroll area's content: Base UI's <c>ScrollArea.Content</c>, rendered as a <c>div</c> that
/// grows to its content's width, so that horizontal overflow can be measured.
/// </summary>
public sealed class ScrollAreaContent : PartBase<ScrollAreaContentState>
{
    private const string DefaultTag = "div";
    private const string PartStyle = "min-width: fit-content";

    private static readonly StateAttributesMapping<ScrollAreaContentState> s_mapping = ScrollAreaStateMapping.Create<ScrollAreaContentState>();

    private readonly Action contextChanged;
    private ScrollAreaRootContext? context;
    private ScrollAreaContentState? state;
    private ScrollAreaStateKey stateKey;
    private ScrollAreaHandle? handle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaContent"/> class.</summary>
    public ScrollAreaContent()
    {
        contextChanged = OnContextChanged;
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ScrollAreaViewportContext? ViewportContext { get; set; }

    [CascadingParameter]
    private ScrollAreaRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ScrollAreaContentState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ScrollAreaContentState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    internal Task TwinAttached => handle!.Attached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The content is not placed within a <see cref="ScrollAreaViewport"/>.</exception>
    protected override void OnInitialized()
    {
        ScrollAreaViewportContext.Require(ViewportContext);
        context = ScrollAreaRootContext.Require(RootContext);
        context.AddListener(contextChanged);
        handle = new ScrollAreaHandle(JSRuntime, handsOverStyle: false);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => UpdateState();

    // SCR-ANI-05: the content's handle observes its resizes; its style stays C#'s throughout.
    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => disposed
        ? Task.CompletedTask
        : handle!.SyncAsync(Element, new ScrollAreaOptions { Part = ScrollAreaTwin.ContentPart, RootRef = 0 }, context!.RootElement());

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
        attributes.Set("role", "presentation");
        attributes.Set(MergeProps.StyleAttribute, PartStyle);
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
        state = new ScrollAreaContentState(key.Scrolling, overflow.HasOverflowX, overflow.HasOverflowY, overflow.XStart, overflow.XEnd, overflow.YStart, overflow.YEnd, context!.CornerHidden);
        return true;
    }
}
