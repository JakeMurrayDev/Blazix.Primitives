using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// POP-STA-13, §3.6 state object (popover/arrow/PopoverArrow.tsx:26-31, 43-60). Side, Align and Uncentered are the
// `positioning` module's from the Positioner's attach (ADR 04 §2, Amendments 2026-10-27): mirrored.
/// <summary>Represents the state of a popover arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popover is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popover is placed on; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="Uncentered">A value indicating whether the arrow cannot be centered on the anchor; its <c>data-uncentered</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record PopoverArrowState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered);

// popover/arrow/PopoverArrowDataAttributes.ts (POP-DOM-32).
/// <summary>Provides the names of the data attributes a popover arrow renders.</summary>
public static class PopoverArrowDataAttributes
{
    /// <summary>Represents the attribute present while the popover is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popover is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the arrow cannot be centered on the anchor: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";
}

// Base UI's Popover.Arrow (popover/arrow/PopoverArrow.tsx); the contract is docs/components/popover.md §3.6, §5.5,
// POP-ARIA-07. Track 2's PositionerArrowBehavior: aria-hidden="true", position: absolute under the consumer's style, the
// placement from the mirror until the Positioner's module writes it; the module finds the Arrow by its generated id.
/// <summary>Represents an arrow that points from a popover to its anchor, Base UI's <c>Popover.Arrow</c>, rendered as a <c>div</c>.</summary>
public sealed class PopoverArrow : PartBase<PopoverArrowState>
{
    private static readonly StateAttributesMapping<PopoverArrowState> s_mapping = PopupStateMapping.CreateOpen<PopoverArrowState>();

    private PositionerArrowBehavior? arrow;
    private PopoverSubscription? subscription;
    private PopoverSelection<bool>? open;
    private PopoverArrowState? state;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override PopoverArrowState State
    {
        get
        {
            PositionerMirror mirror = arrow!.Mirror;
            bool isOpen = open!.Value;
            if (state is null || state.Open != isOpen || state.Side != mirror.Side || state.Align != mirror.Align || state.Uncentered != mirror.Uncentered)
            {
                state = new PopoverArrowState(isOpen, mirror.Side, mirror.Align, mirror.Uncentered);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PopoverArrowState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arrow is not inside a <see cref="PopoverRoot{TPayload}"/> and a <see cref="PopoverPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext root = PopoverRootContext.Require(RootContext);
        RequirePositioner(PositionerContext);
        arrow = new PositionerArrowBehavior(OnMirrorChanged);
        subscription = root.Subscribe(StateHasChanged);
        open = subscription.Select(PopoverSelectors.Open);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => arrow!.SetContext(PositionerContext!);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => arrow!.AfterRenderAsync();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            arrow?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => arrow!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => arrow!.FinishAttributes(attributes, Id!);

    /// <summary>Throws the error of a popover part rendered outside a popover positioner.</summary>
    /// <param name="context">The cascaded positioner context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    internal static PositionerContext RequirePositioner(PositionerContext? context)
    {
        // POP-EDGE-04: thrown in every build.
        return context ?? throw new InvalidOperationException(
            "Blazix: The `PopoverPositioner` context is missing. `PopoverPositioner` parts must be placed within `<PopoverPositioner>`.");
    }

    // A part renders on a mirror change only when its state record changed (track 2's rule).
    private void OnMirrorChanged()
    {
        PopoverArrowState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
