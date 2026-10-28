using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TIP §3.7 (tooltip/arrow/TooltipArrow.tsx:28-34, 46-67); ADR 08 Amendments 2026-10-27 ("State records"): the placement and
// `uncentered` are written by the `positioning` module from the Positioner's attach (FLT-DOM-09; RND-STA-01 row).
/// <summary>Represents the state of a tooltip arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the tooltip is open; rendered as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on, as the browser-side behavior last reported it.</param>
/// <param name="Align">The alignment of the popup against the anchor, as the browser-side behavior last reported it.</param>
/// <param name="Uncentered">Whether the arrow cannot point at the center of the anchor, as the browser-side behavior last reported it.</param>
/// <param name="Instant">Why transitions are skipped, or <see langword="null"/> when they play; rendered as <c>data-instant</c>.</param>
[PartState]
public sealed partial record TooltipArrowState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered,
    TooltipInstantType? Instant);

// TIP §3.7; tooltip/arrow/TooltipArrowDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tooltip arrow renders.</summary>
public static class TooltipArrowDataAttributes
{
    /// <summary>Represents the attribute present while the tooltip is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the tooltip is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the arrow cannot point at the center of the anchor: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the popup's alignment: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute whose value says why transitions are skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// Base UI's Tooltip.Arrow (tooltip/arrow/TooltipArrow.tsx); TIP-STR-07, TIP-DOM-15, TIP-ARIA-03, TIP-POS-06/07. Track 2's
// PositionerArrowBehavior: `aria-hidden="true"`, its generated id published through the positioner's context (ADR 04
// Amendments 2026-10-27), the style and placement frozen from the Positioner's attach that carries the id.
/// <summary>
/// Points from the tooltip's popup to its anchor: Base UI's <c>Tooltip.Arrow</c>, rendered as a <c>div</c> hidden from
/// assistive technology.
/// </summary>
/// <remarks>The part must be placed within a <see cref="TooltipPositioner"/>, normally inside the <see cref="TooltipPopup"/>.</remarks>
public sealed class TooltipArrow : PartBase<TooltipArrowState>
{
    private static readonly StateAttributesMapping<TooltipArrowState> s_mapping = PopupStateMapping.CreateOpen<TooltipArrowState>();

    private TooltipStoreView? view;
    private PositionerArrowBehavior? behavior;
    private TooltipArrowState? state;

    [CascadingParameter]
    private TooltipRootContext? RootContext { get; set; }

    [CascadingParameter]
    private TooltipPositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override TooltipArrowState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            TooltipStoreView current = view!;
            if (state is null
                || state.Open != current.Open
                || state.Side != mirror.Side
                || state.Align != mirror.Align
                || state.Uncentered != mirror.Uncentered
                || state.Instant != current.InstantType)
            {
                state = new TooltipArrowState(current.Open, mirror.Side, mirror.Align, mirror.Uncentered, current.InstantType);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<TooltipArrowState>? StateAttributesMapping => s_mapping;

    // The positioning module finds the arrow by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arrow is placed outside a <see cref="TooltipRoot{TPayload}"/> or a <see cref="TooltipPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        // TIP-EDGE-02, then TIP-EDGE-04.
        TooltipRootContext root = TooltipRootContext.Require(RootContext);
        TooltipPositionerContext positioner = TooltipPositionerContext.Require(PositionerContext);
        behavior = new PositionerArrowBehavior(OnMirrorChanged);
        behavior.SetContext(positioner.Positioner);
        view = root.CreateView(StateHasChanged, TooltipSelection.Open | TooltipSelection.InstantType);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            view?.Dispose();
            view = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes, Id!);

    // Track 2's rule: render on a mirror change only when the state record changed.
    private void OnMirrorChanged()
    {
        PositionerMirror mirror = behavior!.Mirror;
        if (state is null || state.Side != mirror.Side || state.Align != mirror.Align || state.Uncentered != mirror.Uncentered)
        {
            StateHasChanged();
        }
    }
}
