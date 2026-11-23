using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// POP-EDGE-03 (popover/portal/PopoverPortalContext.ts:6-12): what the Positioner reads from its Portal.
/// <summary>The state a popover portal shares with its positioner.</summary>
internal sealed class PopoverPortalContext
{
    /// <summary>Gets a value indicating whether the portal keeps its subtree mounted while the popover is closed.</summary>
    public bool KeepMounted { get; internal set; }

    /// <summary>Throws the error of a positioner rendered outside a popover portal.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static PopoverPortalContext Require(PopoverPortalContext? context)
    {
        // POP-EDGE-03: thrown in every build.
        return context ?? throw new InvalidOperationException("Blazix: `<PopoverPortal>` is missing.");
    }
}

// Base UI's Popover.Portal (popover/portal/PopoverPortal.tsx); the contract is docs/components/popover.md §3.3, §5.2.
// The portal node over track 1's PopupPortalBehavior and PortalTwin (ADR 04 and 08 Amendments 2026-10-27): the pipeline's
// display: contents host wrapper, the node `<div id data-base-ui-portal>` rendered display: none until the `portal` module
// moves it, the outside guards and the aria-owns owner beside the wrapper while a non-modal open focus manager publishes
// (POP-DOM-11). It renders while mounted or KeepMounted (POP-DOM-10) and selects the mounted state, so it registers as a
// boundary part (ADR 16 Amendments 2026-10-27). A DefaultOpen popover prerenders its node display: none (ADR 04 §5;
// settled by the orchestrator 2026-10-27, plan §Settled "Prerender").
/// <summary>
/// Represents the node a popover's positioner and backdrop are moved into, Base UI's <c>Popover.Portal</c>, rendered as a
/// <c>div</c> appended to the document body by default.
/// </summary>
public sealed class PopoverPortal : PartBase<EmptyState>
{
    private readonly PopoverPortalContext portalContext = new();
    private readonly RenderFragment cascadedContent;
    private PopupPortalBehavior? portal;
    private PopoverSubscription? subscription;
    private PopoverSelection<bool>? mounted;
    private IDisposable? boundaryPart;
    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="PopoverPortal"/> class.</summary>
    public PopoverPortal()
    {
        cascadedContent = RenderCascade;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    // POP-API-19
    /// <summary>Gets or sets a value indicating whether the portal keeps its content mounted while the popover is closed.</summary>
    /// <value><see langword="false"/> by default; when <see langword="true"/>, the closed positioner is hidden instead.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // POP-API-20
    /// <summary>Gets or sets the element the portal node is appended to.</summary>
    /// <value>
    /// An element, a script-held node, a selector, or <see cref="PortalContainerValue.Pending"/> to render nothing yet;
    /// <see langword="null"/>, the default, for the enclosing portal's node, else the document body.
    /// </value>
    [Parameter]
    public PortalContainerValue? Container { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override string? HostWrapperTagName => "div";

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The portal is not inside a <see cref="PopoverRoot{TPayload}"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext root = PopoverRootContext.Require(RootContext);
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged);
        subscription = root.Subscribe(StateHasChanged);
        mounted = subscription.Select(PopoverSelectors.Mounted);
        boundaryPart = root.RegisterBoundaryPart();
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.NodeId = Id;
        portalContext.KeepMounted = KeepMounted;
        consumerContent = ChildContent;
        ChildContent = portal.CascadeContent(cascadedContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = (KeepMounted || mounted!.Value) && PopupPortalBehavior.RendersNode(Container);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);
        await RootContext!.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            boundaryPart?.Dispose();
            boundaryPart = null;
            subscription?.Dispose();
            subscription = null;
            if (portal is not null)
            {
                _ = portal.DisposeAsync().AsTask();
                portal = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set(PopupMarkerAttributes.Portal, string.Empty);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => portal!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => portal!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => portal!.BuildAfterElement(builder);

    private void RenderCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<PopoverPortalContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PopoverPortalContext>.Value), portalContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<PopoverPortalContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<PopoverPortalContext>.ChildContent), consumerContent);
        builder.CloseComponent();
    }
}
