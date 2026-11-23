using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iodine.Headless;

// SCR §3.6 (ScrollArea.Corner.State, an empty object; SCR-STA-05, RND-STA-02); ADR 08 Amendments 2026-11-18 names the
// Corner's own empty record (chosen by the user 2026-11-18).
/// <summary>Represents the state of a scroll area corner, which has no fields and projects no attributes.</summary>
[PartState]
public sealed partial record ScrollAreaCornerState
{
    /// <summary>Gets the shared instance.</summary>
    /// <value>The single shared <see cref="ScrollAreaCornerState"/> instance.</value>
    public static ScrollAreaCornerState Instance { get; } = new();
}

// Base UI's ScrollArea.Corner (scroll-area/corner/ScrollAreaCorner.tsx); the contract is docs/components/scroll-area.md §3.6,
// §5.7.
// - Rendered only while both axes overflow (SCR-DOM-26), with no transition status (SCR-ANI-10).
// - `aria-hidden="true"` (SCR-ARIA-05); `position: absolute; bottom: 0; inset-inline-end: 0` and the size at 0px on the
//   render in which it appears (SCR-DOM-27, SCR-ANI-09), under the consumer's style; no data attribute (SCR-DOM-28).
/// <summary>
/// Represents the corner of a scroll area: Base UI's <c>ScrollArea.Corner</c>, rendered as a <c>div</c> that fills the square
/// where the vertical and the horizontal scrollbar meet.
/// </summary>
/// <remarks>The corner renders nothing unless the content overflows on both axes.</remarks>
public sealed class ScrollAreaCorner : PartBase<ScrollAreaCornerState>
{
    private const string DefaultTag = "div";
    private const string OwnStyle = "position: absolute; bottom: 0; inset-inline-end: 0";
    private const string InitialStyle = "position: absolute; bottom: 0; inset-inline-end: 0; width: 0px; height: 0px";

    private readonly Action contextChanged;
    private ScrollAreaRootContext? context;
    private bool renderedShown;
    private ScrollAreaHandle? handle;
    private string? sentStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaCorner"/> class.</summary>
    public ScrollAreaCorner()
    {
        contextChanged = OnContextChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ScrollAreaRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ScrollAreaCornerState State => ScrollAreaCornerState.Instance;

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    internal Task TwinAttached => handle!.Attached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The corner is not placed within a <see cref="ScrollAreaRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ScrollAreaRootContext.Require(RootContext);
        context.AddListener(contextChanged);
        handle = new ScrollAreaHandle(JSRuntime, handsOverStyle: true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedShown = !context!.CornerHidden;
        if (!renderedShown)
        {
            // SCR-DOM-26: no element, so no handle.
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    // SCR-DOM-26: an element that left disposes its handle; a new one attaches its own.
    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => disposed
        ? Task.CompletedTask
        : handle!.SyncAsync(Element, new ScrollAreaOptions { Part = ScrollAreaTwin.CornerPart, RootRef = 0, Style = sentStyle }, context!.RootElement());

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
        attributes.Set("aria-hidden", "true");
    }

    // The handover: C#'s declarations under the consumer's; the size at 0px until the module owns it (SCR-ANI-09).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? consumer = ScrollAreaStyles.Consumer(attributes);
        sentStyle = MergeProps.MergeStyles(OwnStyle, consumer);
        ScrollAreaStyles.Set(attributes, handle!.RenderStyle(MergeProps.MergeStyles(InitialStyle, consumer)));
    }

    private void OnContextChanged()
    {
        if (context!.CornerHidden == renderedShown)
        {
            StateHasChanged();
        }
    }
}
