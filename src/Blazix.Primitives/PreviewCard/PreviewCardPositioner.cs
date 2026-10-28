using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// PRV §3.5, PRV-STA-05 (preview-card/positioner/PreviewCardPositioner.tsx:88-94); ADR 08 Amendments 2026-10-27 ("State
// records"): the placement is mirrored from the `positioning` module's report (RND-STA-01 row of ADR 01).
/// <summary>Represents the state of a preview card positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the preview card is open; rendered as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on, as the browser-side behavior last reported it.</param>
/// <param name="Align">The alignment of the popup against the anchor, as the browser-side behavior last reported it.</param>
/// <param name="AnchorHidden">Whether the anchor is hidden, as the browser-side behavior last reported it.</param>
/// <param name="Instant">Why transitions are skipped, the card's instant type; rendered as <c>data-instant</c>.</param>
[PartState]
public sealed partial record PreviewCardPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    PreviewCardInstantType? Instant);

// PRV §3.5, PRV-DOM-09; preview-card/positioner/PreviewCardPositionerDataAttributes.ts, in its order, with data-instant (PRV §16 Q3).
/// <summary>Provides the names of the data attributes a preview card positioner renders.</summary>
public static class PreviewCardPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the preview card is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the preview card is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the popup's alignment: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute whose value says why transitions are skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// PRV §3.5, PRV-DOM-10; preview-card/positioner/PreviewCardPositionerCssVars.ts, in its order.
/// <summary>Provides the names of the CSS variables a preview card positioner sets on itself.</summary>
public static class PreviewCardPositionerCssVars
{
    /// <summary>Represents the variable holding the space available between the trigger and the edge of the viewport, horizontally: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the variable holding the space available between the trigger and the edge of the viewport, vertically: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the variable holding the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the variable holding the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the variable holding the coordinates the popup scales from: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";

    /// <summary>Represents the variable holding the positioner's width while a viewport is rendered: <c>--positioner-width</c>.</summary>
    public const string PositionerWidth = "--positioner-width";

    /// <summary>Represents the variable holding the positioner's height while a viewport is rendered: <c>--positioner-height</c>.</summary>
    public const string PositionerHeight = "--positioner-height";
}

// preview-card/positioner/PreviewCardPositionerContext.ts (PRV-EDGE-04): the placement the Popup, Arrow and Viewport read.
// It wraps track 2's PositionerContext, whose listeners the parts join (the Arrow's behavior joins it itself).
/// <summary>What a preview card positioner gives the popup, arrow and viewport inside it.</summary>
internal sealed class PreviewCardPositionerContext(PositionerContext positioner)
{
    // PRV-EDGE-04, with the port's prefix and code quoting (issue 122).
    /// <summary>The message thrown when a preview card arrow is placed outside a <c>PreviewCardPositioner</c>.</summary>
    internal const string MissingMessage = "Blazix: `PreviewCardPositionerContext` is missing. `PreviewCardPositioner` parts must be placed within `<PreviewCardPositioner>`.";

    /// <summary>Gets the positioning context the arrow's behavior registers with.</summary>
    public PositionerContext Positioner { get; } = positioner;

    /// <summary>Gets the placement as the browser-side behavior last reported it.</summary>
    public PositionerMirror Mirror => Positioner.Mirror;

    /// <summary>Returns <paramref name="context"/>, or throws when a part is placed outside a preview card positioner.</summary>
    /// <param name="context">The cascaded context.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static PreviewCardPositionerContext Require(PreviewCardPositionerContext? context) => context ?? throw new InvalidOperationException(MissingMessage);
}

// Base UI's PreviewCard.Positioner (preview-card/positioner/PreviewCardPositioner.tsx); the contract is
// docs/components/preview-card.md §3.5, §5.3, §10, in TooltipPositioner's shape (M4 track 6).
// - Track 2's PositionerBehavior (Floating/) with PreviewCard's defaults (PRV-API-18: Side.Bottom, Align.Center, a
//   collision and an arrow padding of 5, POPUP_COLLISION_AVOIDANCE as null); `role="presentation"`, `hidden` while
//   unmounted and the placement through the behavior's attribute helpers. KeepMounted is the Portal's (PRV-API-19: no
//   parameter). `pointer-events: none` only while closed (PRV-DOM-07), which the module writes (FLT-DOM-01).
// - The floating element of the card's session (PRV-POS-02; M4 track 3's InteractionsFloatingBehavior): the hover leave
//   with the store's close delay, only for a hover-opened card (PRV-PTR-06, the recorded open event), the inside-press
//   marks and the floating Escape. The card takes part in the popup tree (PRV-STR-10): a nested Root's session names the
//   outer one (ParentToken). Both handles await the portal's move.
// - data-instant from the store (PRV-DOM-09), rendered by C# (ADR 04 Amendments 2026-10-27; PRV §16 Q3).
// - The inline middleware (PRV-POS-03, PRV-PTR-14; PreviewCardPositioner.tsx:78): `HoveredLine` makes the module narrow a
//   wrapping trigger to the session's hovered line, else the line on the card's side (PSH-ALG-16).
/// <summary>
/// Positions the preview card's popup against its trigger: Base UI's <c>PreviewCard.Positioner</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>The part must be placed within a <see cref="PreviewCardPortal"/> inside a <see cref="PreviewCardRoot{TPayload}"/>.</remarks>
public sealed class PreviewCardPositioner : PartBase<PreviewCardPositionerState>
{

    private static readonly StateAttributesMapping<PreviewCardPositionerState> s_mapping = PopupStateMapping.CreateOpen<PreviewCardPositionerState>();

    private PreviewCardRootContext? root;
    private PreviewCardPortalContext? portalContext;
    private PreviewCardStoreView? view;
    private PositionerBehavior? behavior;
    private InteractionsFloatingBehavior? floating;
    private PreviewCardPositionerContext? context;
    private PreviewCardPositionerState? state;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;
    private InteractionsFloatingOptions? hoverFloating;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PreviewCardRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PreviewCardPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <summary>Gets or sets the element or point the popup is positioned against.</summary>
    /// <value>An element, an element id or a virtual rectangle; <see langword="null"/> (the default) for the active trigger.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position the positioner uses.</summary>
    /// <value>The position method, or <see langword="null"/> (the default) for <see cref="Blazix.Primitives.PositionMethod.Absolute"/>.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor the popup is placed on.</summary>
    /// <value>The side, or <see langword="null"/> (the default) for <see cref="Blazix.Primitives.Side.Bottom"/>.</value>
    [Parameter]
    public Side? Side { get; set; }

    /// <summary>Gets or sets the distance between the anchor and the popup.</summary>
    /// <value>A number of pixels or an offset computed from the sizes, or <see langword="null"/> (the default) for 0.</value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    /// <summary>Gets or sets how the popup is aligned against the anchor.</summary>
    /// <value>The alignment, or <see langword="null"/> (the default) for <see cref="Blazix.Primitives.Align.Center"/>.</value>
    [Parameter]
    public Align? Align { get; set; }

    /// <summary>Gets or sets the offset along the alignment axis.</summary>
    /// <value>A number of pixels or an offset computed from the sizes, or <see langword="null"/> (the default) for 0.</value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    /// <summary>Gets or sets the area the popup is kept within.</summary>
    /// <value>The boundary, or <see langword="null"/> (the default) for the clipping ancestors.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    /// <summary>Gets or sets the space kept between the popup and the edges of the boundary.</summary>
    /// <value>A number of pixels for every side or one per side, or <see langword="null"/> (the default) for 5.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stays in view while the anchor scrolls out of it.</summary>
    /// <value><see langword="true"/> to keep it in view. The default is <see langword="false"/>.</value>
    [Parameter]
    public bool Sticky { get; set; }

    /// <summary>Gets or sets the smallest distance between the arrow and the popup's edges.</summary>
    /// <value>A number of pixels, or <see langword="null"/> (the default) for 5.</value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stops following the anchor when it moves.</summary>
    /// <value><see langword="true"/> to stop tracking the anchor. The default is <see langword="false"/>.</value>
    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    /// <summary>Gets or sets how the popup avoids collisions with the boundary.</summary>
    /// <value>The strategy, or <see langword="null"/> (the default) for flipping on both axes, falling back on the end side.</value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override PreviewCardPositionerState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            PreviewCardStoreView current = view!;
            if (state is null
                || state.Open != current.Open
                || state.Side != mirror.Side
                || state.Align != mirror.Align
                || state.AnchorHidden != mirror.AnchorHidden
                || state.Instant != current.InstantType)
            {
                state = new PreviewCardPositionerState(current.Open, mirror.Side, mirror.Align, mirror.AnchorHidden, current.InstantType);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PreviewCardPositionerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client).
    /// <summary>Gets a task that completes when the positioner's positioning handle is attached.</summary>
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The positioner is placed outside a <see cref="PreviewCardRoot{TPayload}"/> or a <see cref="PreviewCardPortal"/>.</exception>
    protected override void OnInitialized()
    {
        // PRV-EDGE-01, then PRV-EDGE-03 (PreviewCardPositioner.tsx: the Root lookup first).
        root = PreviewCardRootContext.Require(RootContext);
        PreviewCardPortalContext.Require(PortalContext);
        behavior = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        floating = new InteractionsFloatingBehavior(JSRuntime, RequestAsync, DispatchExceptionAsync);
        context = new PreviewCardPositionerContext(behavior.Context);
        view = root.CreateView(StateHasChanged, PreviewCardSelection.Open | PreviewCardSelection.Mounted | PreviewCardSelection.TransitionStatus | PreviewCardSelection.InstantType | PreviewCardSelection.Positioner);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portalContext = PreviewCardPortalContext.Require(PortalContext);
        behavior!.SetDirectionContext(DirectionContext);
        behavior.SetPortalContext(Portal);
        floating!.PortalContext = Portal;
        // A parameter set that does not supply ChildContent leaves this part's own cascade in it.
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            RenderFragment? content = cascadedContent;
            PreviewCardPositionerContext current = context!;
            cascade = behavior.Context.Cascade(builder =>
            {
                builder.OpenComponent<CascadingValue<PreviewCardPositionerContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<PreviewCardPositionerContext>.Value), current);
                builder.AddComponentParameter(2, nameof(CascadingValue<PreviewCardPositionerContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<PreviewCardPositionerContext>.ChildContent), content);
                builder.CloseComponent();
            });
        }

        ChildContent = cascade;
    }

    // The store's selections move without a parameter set, so the inputs are set on each render.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        PreviewCardStoreView current = view!;
        behavior!.SetInputs(new PositionerInputs
        {
            Anchor = Anchor,
            PositionMethod = PositionMethod,
            Side = Side ?? Blazix.Primitives.Side.Bottom,
            SideOffset = SideOffset,
            Align = Align,
            AlignOffset = AlignOffset,
            CollisionBoundary = CollisionBoundary,
            CollisionPadding = CollisionPadding ?? 5d,
            Sticky = Sticky,
            ArrowPadding = ArrowPadding ?? 5d,
            DisableAnchorTracking = DisableAnchorTracking,
            CollisionAvoidance = CollisionAvoidance,
            Open = current.Open,
            Mounted = current.Mounted,
            KeepMounted = portalContext!.KeepMounted,
            TransitionStatus = current.TransitionStatus,
            AdaptiveOrigin = current.AdaptiveOrigin,
            HoveredLine = true,
            TriggerId = current.ActiveTriggerId,
            SessionToken = root!.SessionToken,
        });
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        PreviewCardStoreView current = view!;
        current.SetPositionerElement(Element);

        // PRV-PTR-06: the card's own leave, with the store's close delay (PRV-STA-11).
        floating!.SessionToken = root!.SessionToken;
        floating.Options = HoverFloating(current.CloseDelay);
        Task sync = floating.AfterRenderAsync(Enabled, Element, Id);
        await behavior!.AfterRenderAsync(Element, Id);

        await current.AtBatchBoundaryAsync();
        await sync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            view?.SetPositionerElement(null);
            view?.Dispose();
            view = null;
            behavior?.Dispose();
            if (floating is not null)
            {
                _ = floating.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    // Track 2's rule: render on a mirror change only when the state record changed.
    private void OnMirrorChanged()
    {
        PositionerMirror mirror = behavior!.Mirror;
        if (state is null || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden)
        {
            StateHasChanged();
        }
    }

    private Task RequestAsync(OpenChangeRequest request) => root!.RequestAsync(request);

    // ADR 16: the options object is rebuilt only when the close delay changes, so an unchanged render sends nothing.
    private InteractionsFloatingOptions HoverFloating(int closeDelay)
    {
        if (hoverFloating?.Hover is not { } hover || hover.CloseDelay != closeDelay)
        {
            hoverFloating = new InteractionsFloatingOptions { Hover = new InteractionsFloatingHoverOptions(closeDelay) };
        }

        return hoverFloating;
    }
}
