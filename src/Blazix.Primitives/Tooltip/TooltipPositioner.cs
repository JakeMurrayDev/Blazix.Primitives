using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TIP §3.5 (tooltip/positioner/TooltipPositioner.tsx:76-92, 110-131); ADR 08 Amendments 2026-10-27 ("State records"): the
// placement is mirrored from the `positioning` module's report (RND-STA-01 row of ADR 01; TIP-STA-12's relaxation).
/// <summary>Represents the state of a tooltip positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the tooltip is open; rendered as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on, as the browser-side behavior last reported it.</param>
/// <param name="Align">The alignment of the popup against the anchor, as the browser-side behavior last reported it.</param>
/// <param name="AnchorHidden">Whether the anchor is hidden, as the browser-side behavior last reported it.</param>
/// <param name="Instant">
/// Why transitions are skipped: <see cref="TooltipInstantType.TrackingCursor"/> while the tooltip follows the pointer, else
/// the tooltip's instant type; rendered as <c>data-instant</c>.
/// </param>
[PartState]
public sealed partial record TooltipPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    TooltipInstantType? Instant);

// TIP §3.5; tooltip/positioner/TooltipPositionerDataAttributes.ts, in its order, with data-instant (TIP §16 Q2's resolution).
/// <summary>Provides the names of the data attributes a tooltip positioner renders.</summary>
public static class TooltipPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the tooltip is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the tooltip is closed: <c>data-closed</c>.</summary>
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

// TIP §3.5; tooltip/positioner/TooltipPositionerCssVars.ts, in its order.
/// <summary>Provides the names of the CSS variables a tooltip positioner sets on itself.</summary>
public static class TooltipPositionerCssVars
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

// tooltip/positioner/TooltipPositionerContext.ts:14-21 (TIP-EDGE-04): the placement the Popup, Arrow and Viewport read.
// It wraps track 2's PositionerContext, whose listeners the parts join (the Arrow's behavior joins it itself).
/// <summary>What a tooltip positioner gives the popup, arrow and viewport inside it.</summary>
internal sealed class TooltipPositionerContext(PositionerContext positioner)
{
    // TIP-EDGE-04, with the port's prefix and code quoting (issue 121).
    /// <summary>The message thrown when a tooltip arrow is placed outside a <c>TooltipPositioner</c>.</summary>
    internal const string MissingMessage = "Blazix: `TooltipPositionerContext` is missing. `TooltipPositioner` parts must be placed within `<TooltipPositioner>`.";

    /// <summary>Gets the positioning context the arrow's behavior registers with.</summary>
    public PositionerContext Positioner { get; } = positioner;

    /// <summary>Gets the placement as the browser-side behavior last reported it.</summary>
    public PositionerMirror Mirror => Positioner.Mirror;

    /// <summary>Returns <paramref name="context"/>, or throws when a part is placed outside a tooltip positioner.</summary>
    /// <param name="context">The cascaded context.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static TooltipPositionerContext Require(TooltipPositionerContext? context) => context ?? throw new InvalidOperationException(MissingMessage);
}

// Base UI's Tooltip.Positioner (tooltip/positioner/TooltipPositioner.tsx); the contract is docs/components/tooltip.md §3.5,
// §5.3, §10.
// - Track 2's PositionerBehavior (Floating/) with Tooltip's defaults (TIP-POS-01: Side.Top, Align.Center, a collision
//   and an arrow padding of 5, POPUP_COLLISION_AVOIDANCE as null); `role="presentation"`, `hidden` while unmounted and
//   the placement through the behavior's attribute helpers. KeepMounted is the Portal's (TIP-POS-09: no parameter).
// - `pointer-events: none` while `!open || TrackCursorAxis == Both || DisableHoverablePopup` (TIP-DOM-07): the module
//   writes it while closed (FLT-DOM-01); while open and unhoverable the part puts it ahead of the consumer's style, which
//   the layered writer applies as the consumer band (ADR 04 Amendments 2026-10-27), so a consumer `pointer-events` still
//   wins (TIP-DOM-17). C# renders it before attach and freezes it from attach.
// - The floating element of the tooltip's session (M4 track 3's InteractionsFloatingBehavior, `treatPopupAsFloatingElement`
//   false): the hover leave with the store's close delay (TIP-PTR-26, TIP §16 Q16), the inside-press marks and the floating
//   Escape (TIP-DOM-14). Tooltip takes no part in the popup tree (TIP-DIS-08). Both handles await the portal's move.
// - data-instant from the store, `tracking-cursor` while tracking (TIP-STA-12), rendered by C# (ADR 04 Amendments
//   2026-10-27; TIP §16 Q2's resolution).
/// <summary>
/// Positions the tooltip's popup against its trigger: Base UI's <c>Tooltip.Positioner</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>The part must be placed within a <see cref="TooltipPortal"/> inside a <see cref="TooltipRoot{TPayload}"/>.</remarks>
public sealed class TooltipPositioner : PartBase<TooltipPositionerState>
{
    private const string InertStyle = "pointer-events: none;";

    private static readonly StateAttributesMapping<TooltipPositionerState> s_mapping = PopupStateMapping.CreateOpen<TooltipPositionerState>();
    private static readonly InteractionsFloatingOptions s_disabledFloating = new() { Node = false };

    private TooltipRootContext? root;
    private TooltipPortalContext? portalContext;
    private TooltipStoreView? view;
    private PositionerBehavior? behavior;
    private InteractionsFloatingBehavior? floating;
    private TooltipPositionerContext? context;
    private TooltipPositionerState? state;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;
    private InteractionsFloatingOptions? hoverFloating;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private TooltipRootContext? RootContext { get; set; }

    [CascadingParameter]
    private TooltipPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <summary>Gets or sets the element or point the popup is positioned against.</summary>
    /// <value>An element, an element id or a virtual rectangle; <see langword="null"/> (the default) for the active trigger, or the pointer while tracking it.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position the positioner uses.</summary>
    /// <value>The position method, or <see langword="null"/> (the default) for <see cref="Blazix.Primitives.PositionMethod.Absolute"/>.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor the popup is placed on.</summary>
    /// <value>The side, or <see langword="null"/> (the default) for <see cref="Blazix.Primitives.Side.Top"/>.</value>
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
    protected override TooltipPositionerState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            TooltipStoreView current = view!;
            if (state is null
                || state.Open != current.Open
                || state.Side != mirror.Side
                || state.Align != mirror.Align
                || state.AnchorHidden != mirror.AnchorHidden
                || state.Instant != current.PositionerInstantType)
            {
                state = new TooltipPositionerState(current.Open, mirror.Side, mirror.Align, mirror.AnchorHidden, current.PositionerInstantType);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<TooltipPositionerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client).
    /// <summary>Gets a task that completes when the positioner's positioning handle is attached.</summary>
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The positioner is placed outside a <see cref="TooltipRoot{TPayload}"/> or a <see cref="TooltipPortal"/>.</exception>
    protected override void OnInitialized()
    {
        // TIP-EDGE-02, then TIP-EDGE-03 (TooltipPositioner.tsx:45-46: the Root lookup first).
        root = TooltipRootContext.Require(RootContext);
        TooltipPortalContext.Require(PortalContext);
        behavior = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        floating = new InteractionsFloatingBehavior(JSRuntime, RequestAsync, DispatchExceptionAsync);
        context = new TooltipPositionerContext(behavior.Context);
        view = root.CreateView(StateHasChanged, TooltipSelection.Open | TooltipSelection.Mounted | TooltipSelection.TransitionStatus | TooltipSelection.Positioner);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portalContext = TooltipPortalContext.Require(PortalContext);
        behavior!.SetDirectionContext(DirectionContext);
        behavior.SetPortalContext(Portal);
        floating!.PortalContext = Portal;
        // A parameter set that does not supply ChildContent leaves this part's own cascade in it.
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            RenderFragment? content = cascadedContent;
            TooltipPositionerContext current = context!;
            cascade = behavior.Context.Cascade(builder =>
            {
                builder.OpenComponent<CascadingValue<TooltipPositionerContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<TooltipPositionerContext>.Value), current);
                builder.AddComponentParameter(2, nameof(CascadingValue<TooltipPositionerContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<TooltipPositionerContext>.ChildContent), content);
                builder.CloseComponent();
            });
        }

        ChildContent = cascade;
    }

    // The store's selections move without a parameter set, so the inputs are set on each render.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        TooltipStoreView current = view!;
        behavior!.SetInputs(new PositionerInputs
        {
            Anchor = Anchor,
            PositionMethod = PositionMethod,
            Side = Side ?? Blazix.Primitives.Side.Top,
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
            TriggerId = current.ActiveTriggerId,
            SessionToken = root!.SessionToken,
        });
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        TooltipStoreView current = view!;
        current.SetPositionerElement(Element);

        // TIP-PTR-26: the popup's own leave, enabled while the root is not disabled, with the store's close delay.
        floating!.SessionToken = root!.SessionToken;
        floating.Options = current.Disabled ? s_disabledFloating : HoverFloating(current.CloseDelay);
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
    protected override void FinishAttributes(AttributeList attributes)
    {
        // TIP-DOM-07: while open and unhoverable, `pointer-events: none` goes ahead of the consumer's style.
        TooltipStoreView current = view!;
        if (current.Open && (current.DisableHoverablePopup || current.TrackCursorAxis == TooltipTrackCursorAxis.Both))
        {
            string? merged = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
            attributes.Set(MergeProps.StyleAttribute, string.IsNullOrEmpty(merged) ? InertStyle : InertStyle + " " + merged);
        }

        behavior!.FinishAttributes(attributes);
    }

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
            hoverFloating = new InteractionsFloatingOptions { Node = false, Hover = new InteractionsFloatingHoverOptions(closeDelay) };
        }

        return hoverFloating;
    }
}
