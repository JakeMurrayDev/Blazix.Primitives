using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// PRV §3.7, PRV-STA-07 (preview-card/arrow/PreviewCardArrow.tsx:27-32): no `instant`, unlike Tooltip's arrow; ADR 08 Amendments 2026-10-27 ("State records"): the placement and
// `uncentered` are written by the `positioning` module from the Positioner's attach (FLT-DOM-09; RND-STA-01 row).
/// <summary>Represents the state of a preview card arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the preview card is open; rendered as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on, as the browser-side behavior last reported it.</param>
/// <param name="Align">The alignment of the popup against the anchor, as the browser-side behavior last reported it.</param>
/// <param name="Uncentered">Whether the arrow cannot point at the center of the anchor, as the browser-side behavior last reported it.</param>
[PartState]
public sealed partial record PreviewCardArrowState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered);

// PRV §3.7; preview-card/arrow/PreviewCardArrowDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a preview card arrow renders.</summary>
public static class PreviewCardArrowDataAttributes
{
    /// <summary>Represents the attribute present while the preview card is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the preview card is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the arrow cannot point at the center of the anchor: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the popup's alignment: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;
}

// Base UI's PreviewCard.Arrow (preview-card/arrow/PreviewCardArrow.tsx); PRV-STR-06, PRV-DOM-14, PRV-ARIA-03, PRV-POS-06/07. Track 2's
// PositionerArrowBehavior: `aria-hidden="true"`, its generated id published through the positioner's context (ADR 04
// Amendments 2026-10-27), the style and placement frozen from the Positioner's attach that carries the id.
/// <summary>
/// Points from the preview card's popup to its anchor: Base UI's <c>PreviewCard.Arrow</c>, rendered as a <c>div</c> hidden from
/// assistive technology.
/// </summary>
/// <remarks>The part must be placed within a <see cref="PreviewCardPositioner"/>, normally inside the <see cref="PreviewCardPopup"/>.</remarks>
public sealed class PreviewCardArrow : PartBase<PreviewCardArrowState>
{
    private static readonly StateAttributesMapping<PreviewCardArrowState> s_mapping = PopupStateMapping.CreateOpen<PreviewCardArrowState>();

    private PreviewCardStoreView? view;
    private PositionerArrowBehavior? behavior;
    private PreviewCardArrowState? state;

    [CascadingParameter]
    private PreviewCardRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PreviewCardPositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override PreviewCardArrowState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            PreviewCardStoreView current = view!;
            if (state is null
                || state.Open != current.Open
                || state.Side != mirror.Side
                || state.Align != mirror.Align
                || state.Uncentered != mirror.Uncentered)
            {
                state = new PreviewCardArrowState(current.Open, mirror.Side, mirror.Align, mirror.Uncentered);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PreviewCardArrowState>? StateAttributesMapping => s_mapping;

    // The positioning module finds the arrow by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arrow is placed outside a <see cref="PreviewCardRoot{TPayload}"/> or a <see cref="PreviewCardPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        // PRV-EDGE-01, then PRV-EDGE-04.
        PreviewCardRootContext root = PreviewCardRootContext.Require(RootContext);
        PreviewCardPositionerContext positioner = PreviewCardPositionerContext.Require(PositionerContext);
        behavior = new PositionerArrowBehavior(OnMirrorChanged);
        behavior.SetContext(positioner.Positioner);
        view = root.CreateView(StateHasChanged, PreviewCardSelection.Open);
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
