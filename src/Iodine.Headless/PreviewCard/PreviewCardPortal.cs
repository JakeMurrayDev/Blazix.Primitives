using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// preview-card/portal/PreviewCardPortalContext.ts:6-12 (PRV-EDGE-03): the Portal's keepMounted, which the Positioner reads.
/// <summary>What a preview card portal gives the positioner inside it.</summary>
/// <param name="KeepMounted">Whether the portal keeps its subtree rendered while the preview card is closed.</param>
internal sealed record PreviewCardPortalContext(bool KeepMounted)
{
    // PRV-EDGE-03, with the port's prefix and code quoting (issue 122).
    /// <summary>The message thrown when a preview card positioner is placed outside a <c>PreviewCardPortal</c>.</summary>
    internal const string MissingMessage = "Blazix: `<PreviewCardPortal>` is missing.";

    /// <summary>Returns <paramref name="context"/>, or throws when the positioner is placed outside a preview card portal.</summary>
    /// <param name="context">The cascaded context.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static PreviewCardPortalContext Require(PreviewCardPortalContext? context) => context ?? throw new InvalidOperationException(MissingMessage);
}

// Base UI's PreviewCard.Portal (preview-card/portal/PreviewCardPortal.tsx); PRV-STR-03, PRV-DOM-04, PRV-API-16/17, PRV-ANI-04; TooltipPortal's shape (M4 track 6).
// Track 1's lite portal (PopupPortalBehavior with Lite: no outside guards, no aria-owns owner; FLT-DOM-11, PSH-STR-11):
// the node <div id data-base-ui-portal> inside the pipeline's display: contents wrapper, rendered while the preview card is
// mounted or KeepMounted is set. The container is a PortalContainerValue, `null` meaning not supplied (chosen by the user
// 2026-10-27, ADR 04 and ADR 08 Amendments 2026-10-27). It selects the mounted state, so it brings the render-batch
// boundary (ADR 16 Amendments 2026-10-27).
/// <summary>
/// Moves the preview card's popup to another part of the page, by default the end of the document body: Base UI's
/// <c>PreviewCard.Portal</c>, rendered as a <c>div</c> node.
/// </summary>
/// <remarks>The part must be placed within a <see cref="PreviewCardRoot{TPayload}"/>.</remarks>
public sealed class PreviewCardPortal : PartBase<EmptyState>
{
    private PreviewCardStoreView? view;
    private PopupPortalBehavior? portal;
    private PreviewCardPortalContext? context;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PreviewCardRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    /// <summary>Gets or sets a value indicating whether the portal keeps its subtree rendered while the preview card is closed.</summary>
    /// <value><see langword="true"/> to keep the popup rendered, its positioner <c>hidden</c>, while closed. The default is <see langword="false"/>.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <summary>Gets or sets the element the portal node is appended to.</summary>
    /// <value>
    /// An element, a script-held node, a selector, or <see cref="PortalContainerValue.Pending"/> to render nothing until a
    /// container is given; <see langword="null"/> (the default) for the nearest enclosing portal's node, else the document body.
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
    /// <exception cref="InvalidOperationException">The portal is placed outside a <see cref="PreviewCardRoot{TPayload}"/>.</exception>
    protected override void OnInitialized()
    {
        // PRV-EDGE-01.
        PreviewCardRootContext root = PreviewCardRootContext.Require(RootContext);
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged) { Lite = true };
        view = root.CreateView(StateHasChanged, PreviewCardSelection.Mounted);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.NodeId = Id;

        // A parameter set that does not supply ChildContent leaves this part's own cascade in it.
        RenderFragment? supplied = cascade is not null && ReferenceEquals(ChildContent, cascade) ? cascadedContent : ChildContent;
        if (context is null || context.KeepMounted != KeepMounted)
        {
            context = new PreviewCardPortalContext(KeepMounted);
            cascade = null;
        }

        if (cascade is null || !ReferenceEquals(cascadedContent, supplied))
        {
            cascadedContent = supplied;
            RenderFragment? content = cascadedContent;
            PreviewCardPortalContext current = context;
            cascade = portal.CascadeContent(builder =>
            {
                builder.OpenComponent<CascadingValue<PreviewCardPortalContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<PreviewCardPortalContext>.Value), current);
                builder.AddComponentParameter(2, nameof(CascadingValue<PreviewCardPortalContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<PreviewCardPortalContext>.ChildContent), content);
                builder.CloseComponent();
            });
        }

        ChildContent = cascade;
    }

    // PRV-DOM-04: the subtree exists while mounted or kept mounted; the store's selection is read on each render.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = (KeepMounted || view!.Mounted) && PopupPortalBehavior.RendersNode(Container);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The view is read before the await: a portal disposed meanwhile still runs the boundary of its last render.
        PreviewCardStoreView current = view!;
        await portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);
        await current.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            view?.Dispose();
            view = null;
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
}
