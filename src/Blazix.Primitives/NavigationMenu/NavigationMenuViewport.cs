using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's NavigationMenu.Viewport (navigation-menu/viewport/NavigationMenuViewport.tsx); the contract is
// docs/components/navigation-menu.md §3.12, §5.12.
// - A `div` with its generated id unless the consumer's (NAV-API-18; prefixed, ADR 02 decision 5), the id the active
//   Trigger's owner names (NAV-ARIA-06).
// - With a Positioner: its two guards are its siblings and it is the target the contents move into. Without one: a guard,
//   an inner target `div` holding the children and a guard inside it, the guards only while a trigger is active
//   (NAV-DOM-34), `inert` while viewportInert (NAV-DOM-35), the interactions floating role (NAV-PTR-09), and the second
//   unmount watcher on its target (§4.2: the module reports the target's exit, after which the lifecycle completes the
//   close unless the opt-out holds).
// - C# renders the guards; the `navigation-menu` module routes them and reports the blur that sets viewportInert (design
//   point 5, settled by the orchestrator 2026-11-04).
/// <summary>
/// Represents the clipping viewport of the navigation menu's current content: Base UI's <c>NavigationMenu.Viewport</c>,
/// rendered as a <c>div</c>.
/// </summary>
public sealed class NavigationMenuViewport : PartBase<EmptyState>
{
    private readonly RenderFragment inlineContent;
    private readonly Action<ElementReference> captureTarget;
    private NavigationMenuRootContext? root;
    private NavigationMenuModule? module;
    private InteractionsFloatingBehavior? floating;
    private StoreSelection<NavigationMenuStoreState, bool>? viewportInert;
    private StoreSelection<NavigationMenuStoreState, string?>? activeTriggerId;
    private RenderFragment? consumerContent;
    private ElementReference? target;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuViewport"/> class.</summary>
    public NavigationMenuViewport()
    {
        inlineContent = RenderInline;
        captureTarget = element => target = element;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private bool HasPositioner => PositionerContext is not null;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes once the viewport's handles have attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(module!.Attached, floating?.TwinAttached ?? Task.CompletedTask);

    /// <summary>Gets the viewport's <c>navigation-menu</c> twin, whose reports the tests play.</summary>
    internal NavigationMenuTwin ModuleTwin => module!.Twin;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        module = new NavigationMenuModule(JSRuntime);
        module.Twin.ViewportInertChanged += inert => _ = InvokeAsync(() => root.Store.SetViewportInert(inert));
        module.Twin.ExitCompleted += () => _ = InvokeAsync(CompleteExitAsync);
        if (!HasPositioner)
        {
            floating = new InteractionsFloatingBehavior(JSRuntime, request => root.RouteAsync(request), DispatchExceptionAsync);
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!HasPositioner)
        {
            consumerContent = ChildContent;
            ChildContent = inlineContent;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        NavigationMenuRootContext menu = root!;
        if (disposed || Element is not { } element)
        {
            return;
        }

        menu.Store.SetViewportId(Id);
        menu.SetViewportTarget(HasPositioner ? element : target);
        await PopupPortalContext.WhenMoved(Portal);
        if (disposed)
        {
            return;
        }

        Task moduleSync = module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.ViewportPart, HasPositioner = HasPositioner }, menu.RootElement);
        if (floating is not null)
        {
            SetFloatingOptions();
            floating.SessionToken = menu.SessionToken;
            floating.PortalContext = Portal;
            await floating.AfterRenderAsync(Enabled, element, Id);
        }

        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            if (root is not null)
            {
                root.SetViewportTarget(null);
                root.Store.SetViewportId(null);
            }

            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }

            if (floating is not null)
            {
                _ = floating.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<NavigationMenuStoreState> subscription = SubscribeTo(RootContext!.Store);
        viewportInert = subscription.Select(NavigationMenuSelectors.ViewportInert);
        activeTriggerId = subscription.Select(NavigationMenuSelectors.ActiveTriggerId);
    }

    // NAV-DOM-35: only a Viewport without a Positioner renders `inert`.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (!HasPositioner && viewportInert!.Value)
        {
            attributes.Set("inert", true);
        }
    }

    // NAV-DOM-34: with a Positioner, the guards are the Viewport's siblings.
    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder)
    {
        if (HasPositioner)
        {
            RenderGuard(builder);
        }
    }

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (HasPositioner)
        {
            RenderGuard(builder);
        }
    }

    private static void RenderGuard(RenderTreeBuilder builder)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        builder.AddAttribute(3, "aria-hidden", "true");
        builder.AddAttribute(4, "style", VisuallyHidden.Style);
        builder.CloseElement();
        builder.CloseRegion();
    }

    // NAV-DOM-34: without a Positioner, a guard, the target and a guard inside the Viewport; the guards only while a trigger
    // is active (NavigationMenuViewport.tsx:34-36).
    private void RenderInline(RenderTreeBuilder builder)
    {
        bool guarded = activeTriggerId!.Value is not null;
        if (guarded)
        {
            RenderGuard(builder);
        }

        builder.OpenElement(10, "div");
        builder.AddElementReferenceCapture(11, captureTarget);
        builder.AddContent(12, consumerContent);
        builder.CloseElement();
        if (guarded)
        {
            RenderGuard(builder);
        }
    }

    // NAV-PTR-09: without a Positioner the Viewport is the floating element, with the hover-close while a trigger is active.
    private void SetFloatingOptions()
    {
        bool active = activeTriggerId!.Value is not null;
        int? delay = active ? root!.CloseDelayMs : null;
        InteractionsFloatingOptions current = floating!.Options;
        if (current.Node != true || current.Hover?.CloseDelay != delay || (current.Hover is null) == active)
        {
            floating.Options = new InteractionsFloatingOptions { Node = true, Hover = active ? new InteractionsFloatingHoverOptions(delay) : null };
        }
    }

    // NavigationMenuRoot.tsx:160-170: the target's exit completes the close, unless the opt-out holds (NAV-EVT-05).
    private Task CompleteExitAsync()
    {
        NavigationMenuStore store = root!.Store;
        NavigationMenuStoreState current = store.State;
        return !disposed && current.Mounted && !current.Open && !current.Lifecycle.PreventUnmountingOnClose
            ? store.CompleteTransitionAsync(false)
            : Task.CompletedTask;
    }
}
