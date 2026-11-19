using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TST §3.11 state object (toast/positioner/ToastPositioner.tsx:92-96); TST-STA-26. The placement is mirrored from the
// `positioning` module's report (ADR 08 Amendments 2026-10-27; the RND-STA-01 row of ADR 01), as TooltipPositionerState.
/// <summary>Represents the state of a toast positioner, which the part exposes as data attributes.</summary>
/// <param name="Side">The side of the anchor the toast is placed on, once positioned.</param>
/// <param name="Align">The alignment of the toast on that side, once positioned.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden.</param>
[PartState]
public sealed partial record ToastPositionerState(
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden);

// toast/positioner/ToastPositionerDataAttributes.ts:6-16 (TST-DOM-21), in its order.
/// <summary>Provides the data attributes a toast positioner renders.</summary>
public static class ToastPositionerDataAttributes
{
    /// <summary>Present while the anchor is hidden.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>The side the toast is placed on: <c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>, <c>inline-start</c> or <c>inline-end</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>The alignment of the toast on its side: <c>start</c>, <c>center</c> or <c>end</c>.</summary>
    public const string Align = PopupDataAttributes.Align;
}

// toast/positioner/ToastPositionerCssVars.ts:5-25 (TST-DOM-21), in its order.
/// <summary>Provides the CSS variables a toast positioner sets.</summary>
public static class ToastPositionerCssVars
{
    /// <summary>The available width between the anchor and the edge of the viewport.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>The available height between the anchor and the edge of the viewport.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>The anchor's width.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>The anchor's height.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>The coordinates the toast is anchored to, for its animations and transitions.</summary>
    public const string TransformOrigin = "--transform-origin";
}

// toast/positioner/ToastPositionerContext.ts:14-22 (TST-EDGE-03): what the Arrow reads, the shared PositionerContext.
internal sealed class ToastPositionerContext(PositionerContext positioner)
{
    // TST-EDGE-03, with the port's prefix and code quoting.
    internal const string MissingMessage = "Blazix: `ToastPositionerContext` is missing. `ToastPositioner` parts must be placed within `<ToastPositioner>`.";

    public PositionerContext Positioner { get; } = positioner;

    public static ToastPositionerContext Require(ToastPositionerContext? context) => context ?? throw new InvalidOperationException(MissingMessage);
}

// Base UI's Toast.Positioner (toast/positioner/ToastPositioner.tsx); TST-STR-10, TST-POS-01..05, TST-DOM-21, TST-ARIA-09,
// TST-EDGE-01, TST-EDGE-10. The shared PositionerBehavior (Floating/) with Toast's fixed inputs: always open, mounted and
// kept mounted, the toast's transition status (the starting `transition: none`, TRN-DOM-05), no session (DP4 (a) of the
// Toast track, chosen by the user 2026-11-19). Every positioning parameter has three levels (TST-POS-02): the parameter,
// else the toast's positioner settings, else Base UI's default (ToastPositioner.tsx:42-53: top, center, offsets 0, a
// collision and an arrow padding of 5, the popup collision avoidance, absolute). Only an element reference anchors the
// toast (TST-POS-03). `--toast-index` goes ahead of the consumer's style (TST-DOM-21, RND-DOM-16). Sticky and
// DisableAnchorTracking are nullable here (unlike the Tooltip's), so an unset parameter defers to the toast's settings.
/// <summary>
/// Positions a toast against an anchor element. Base UI's <c>Toast.Positioner</c>; it renders a <c>div</c> around the
/// toast's root.
/// </summary>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
/// <remarks>
/// Each positioning parameter that is not given takes the toast's <see cref="ToastObject{TData}.Positioner"/> setting, and
/// then the positioner's default. Render anchored toasts in a provider of their own, so the stacking variables of other
/// toasts do not apply to them.
/// </remarks>
public sealed class ToastPositioner<TData> : PartBase<ToastPositionerState>
{
    private ToastProviderContext<TData>? provider;
    private PositionerBehavior? behavior;
    private ToastPositionerContext? context;
    private ToastPositionerState? state;
    private StoreSelection<ToastStoreState<TData>, string, ToastObject<TData>?>? stored;
    private StoreSelection<ToastStoreState<TData>, string, int>? domIndex;
    private StoreSelection<ToastStoreState<TData>, string, int>? visibleIndex;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    // The store's copy of the toast when it holds one, else the parameter (TST-EDGE-10).
    private ToastObject<TData> Current => stored?.Value ?? Toast;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ToastProviderContext<TData>? ProviderContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <summary>Gets or sets the toast to position, the same one as its root's.</summary>
    /// <value>The toast; it is found by its <see cref="ToastObject{TData}.Id"/>, so a copy of it may be passed.</value>
    [EditorRequired]
    [Parameter]
    public ToastObject<TData> Toast { get; set; } = null!;

    /// <summary>Gets or sets the element to position the toast against.</summary>
    /// <value>An element reference, or <see langword="null"/> for the toast's setting. Any other value positions against nothing.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position the toast uses.</summary>
    /// <value>The method, or <see langword="null"/> for the toast's setting, else absolute.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor to place the toast on.</summary>
    /// <value>The side, or <see langword="null"/> for the toast's setting, else <see cref="Primitives.Side.Top"/>.</value>
    [Parameter]
    public Side? Side { get; set; }

    /// <summary>Gets or sets the distance between the anchor and the toast.</summary>
    /// <value>The offset, or <see langword="null"/> for the toast's setting, else 0.</value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    /// <summary>Gets or sets the alignment of the toast on its side.</summary>
    /// <value>The alignment, or <see langword="null"/> for the toast's setting, else <see cref="Primitives.Align.Center"/>.</value>
    [Parameter]
    public Align? Align { get; set; }

    /// <summary>Gets or sets the offset along the alignment axis.</summary>
    /// <value>The offset, or <see langword="null"/> for the toast's setting, else 0.</value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    /// <summary>Gets or sets the boundary the toast avoids colliding with.</summary>
    /// <value>The boundary, or <see langword="null"/> for the toast's setting, else the clipping ancestors.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    /// <summary>Gets or sets the padding kept from the collision boundary.</summary>
    /// <value>The padding, or <see langword="null"/> for the toast's setting, else 5 pixels.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the toast stays in view when the anchor scrolls out.</summary>
    /// <value>The value, or <see langword="null"/> for the toast's setting, else <see langword="false"/>.</value>
    [Parameter]
    public bool? Sticky { get; set; }

    /// <summary>Gets or sets the minimum distance between the arrow and the toast's edges.</summary>
    /// <value>The padding in CSS pixels, or <see langword="null"/> for the toast's setting, else 5.</value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the toast stops following layout shifts of the anchor.</summary>
    /// <value>The value, or <see langword="null"/> for the toast's setting, else <see langword="false"/>.</value>
    [Parameter]
    public bool? DisableAnchorTracking { get; set; }

    /// <summary>Gets or sets how the toast avoids collisions with the boundary.</summary>
    /// <value>The strategy, or <see langword="null"/> for the toast's setting, else the popups' default.</value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ToastPositionerState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            if (state is null || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden)
            {
                state = new ToastPositionerState(mirror.Side, mirror.Align, mirror.AnchorHidden);
            }

            return state;
        }
    }

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures: completes when the positioner's handle has attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // TST-EDGE-01.
        provider = ToastProviderContext<TData>.Require(ProviderContext);
        behavior = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        context = new ToastPositionerContext(behavior.Context);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Toast);
        string id = Toast.Id;
        stored!.Arg = id;
        domIndex!.Arg = id;
        visibleIndex!.Arg = id;
        behavior!.SetDirectionContext(DirectionContext);
        behavior.SetPortalContext(Portal);

        // A parameter set that does not supply ChildContent leaves this part's own cascade in it.
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            RenderFragment? content = cascadedContent;
            ToastPositionerContext current = context!;
            cascade = behavior.Context.Cascade(builder =>
            {
                builder.OpenComponent<CascadingValue<ToastPositionerContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<ToastPositionerContext>.Value), current);
                builder.AddComponentParameter(2, nameof(CascadingValue<ToastPositionerContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<ToastPositionerContext>.ChildContent), content);
                builder.CloseComponent();
            });
        }

        ChildContent = cascade;
    }

    // TST-POS-01..04: the inputs follow the store's copy of the toast, set on each render.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ToastObject<TData> toast = Current;
        ToastPositionerSettings? settings = toast.Positioner;
        AnchorValue? anchor = Anchor ?? settings?.Anchor;
        behavior!.SetInputs(new PositionerInputs
        {
            Anchor = anchor is { } value && value.TryGetValue(out ElementReference element) ? element : null,
            PositionMethod = PositionMethod ?? settings?.PositionMethod ?? Primitives.PositionMethod.Absolute,
            Side = Side ?? settings?.Side ?? Primitives.Side.Top,
            SideOffset = SideOffset ?? settings?.SideOffset,
            Align = Align ?? settings?.Align ?? Primitives.Align.Center,
            AlignOffset = AlignOffset ?? settings?.AlignOffset,
            CollisionBoundary = CollisionBoundary ?? settings?.CollisionBoundary,
            CollisionPadding = CollisionPadding ?? settings?.CollisionPadding ?? 5d,
            Sticky = Sticky ?? settings?.Sticky ?? false,
            ArrowPadding = ArrowPadding ?? settings?.ArrowPadding ?? 5d,
            DisableAnchorTracking = DisableAnchorTracking ?? settings?.DisableAnchorTracking ?? false,
            CollisionAvoidance = CollisionAvoidance ?? settings?.CollisionAvoidance,
            Open = true,
            Mounted = true,
            KeepMounted = true,
            TransitionStatus = toast.TransitionStatus,
        });
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element, Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<ToastStoreState<TData>> subscription = SubscribeTo(provider!.Store);
        string id = Toast?.Id ?? string.Empty;
        stored = subscription.Select(ToastSelectors<TData>.Toast, id);
        domIndex = subscription.Select(ToastSelectors<TData>.ToastIndex, id);
        visibleIndex = subscription.Select(ToastSelectors<TData>.ToastVisibleIndex, id);
    }

    // TST-DOM-21: the placement attributes, then `--toast-index` (the visible index, the DOM index while ending) ahead of
    // the consumer's style.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes);
        int index = Current.TransitionStatus == TransitionStatus.Ending ? domIndex!.Value : visibleIndex!.Value;
        attributes.Set(MergeProps.StyleAttribute, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{ToastRootCssVars.Index}: {index}"));
    }

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
}
