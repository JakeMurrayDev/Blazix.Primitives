using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// SEL-STA-22 (select/arrow/SelectArrow.tsx:28-33, 49-66): Side, Align and Uncentered are the `positioning` module's from
// attach (ADR 04 §2): mirrored. Side is never `none` here: the arrow renders nothing while aligned (SEL-STR-09).
/// <summary>Represents the state of a select arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="Uncentered">A value indicating whether the arrow cannot be centered on the anchor; its <c>data-uncentered</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record SelectArrowState(bool Open, [property: MirroredField] SelectSide? Side, [property: MirroredField] Align? Align, [property: MirroredField] bool Uncentered);

// select/arrow/SelectArrowDataAttributes.ts (SEL-DOM-40).
/// <summary>Provides the names of the data attributes a select arrow renders.</summary>
public static class SelectArrowDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the arrow is not centered on the anchor: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";
}

// Base UI's Select.Arrow (select/arrow/SelectArrow.tsx); SEL-STR-09, SEL-POS-20, SEL-DOM-40, SEL-ARIA-13: track 2 of M4's
// PositionerArrowBehavior (the Popover precedent), `aria-hidden`, nothing rendered while aligned placement is active.
/// <summary>Represents an arrow pointing from a select's popup to its anchor, Base UI's <c>Select.Arrow</c>, rendered as a <c>div</c>.</summary>
/// <remarks>It renders nothing while the popup is placed over the trigger.</remarks>
public sealed class SelectArrow : PartBase<SelectArrowState>
{
    private static readonly StateAttributesMapping<SelectArrowState> s_mapping = PopupStateMapping.CreateOpen<SelectArrowState>();

    private SelectStore? store;
    private PositionerArrowBehavior? arrow;
    private StoreSelection<SelectStoreState, bool>? open;
    private StoreSelection<SelectStoreState, bool>? aligned;
    private SelectArrowState? state;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private SelectPositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectArrowState State
    {
        get
        {
            PositionerMirror mirror = arrow!.Mirror;
            bool isOpen = open!.Value;
            SelectSide? side = SelectSideAttributes.From(mirror.Side, aligned: false);
            if (state is null || state.Open != isOpen || state.Side != side || state.Align != mirror.Align || state.Uncentered != mirror.Uncentered)
            {
                state = new SelectArrowState(isOpen, side, mirror.Align, mirror.Uncentered);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectArrowState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arrow is not inside a <see cref="SelectRoot{TValue}"/> and a <see cref="SelectPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        SelectPositionerContext.Require(PositionerContext);
        arrow = new PositionerArrowBehavior(OnMirrorChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => arrow!.SetContext(PositionerContext!.Positioning);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!aligned!.Value)
        {
            base.BuildRenderTree(builder);
        }
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => arrow!.AfterRenderAsync();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            arrow?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        open = subscription.Select(SelectStoreState.Open);
        aligned = subscription.Select(SelectStoreState.AlignedActiveSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => arrow!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => arrow!.FinishAttributes(attributes, Id!);

    private void OnMirrorChanged()
    {
        SelectArrowState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
