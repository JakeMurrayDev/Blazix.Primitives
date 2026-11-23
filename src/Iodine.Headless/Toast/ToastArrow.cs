using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// TST §3.12 state object (toast/arrow/ToastArrow.tsx:22-26); TST-STA-26. Written by the `positioning` module from the
// Positioner's attach (FLT-DOM-09; the RND-STA-01 row), as TooltipArrowState.
/// <summary>Represents the state of a toast arrow, which the part exposes as data attributes.</summary>
/// <param name="Side">The side of the anchor the toast is placed on, once positioned.</param>
/// <param name="Align">The alignment of the toast on that side, once positioned.</param>
/// <param name="Uncentered">A value indicating whether the arrow cannot be centered on the anchor.</param>
[PartState]
public sealed partial record ToastArrowState(
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered);

// toast/arrow/ToastArrowDataAttributes.ts:7-16 (TST-DOM-22), in its order.
/// <summary>Provides the data attributes a toast arrow renders.</summary>
public static class ToastArrowDataAttributes
{
    /// <summary>The side the toast is placed on: <c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>, <c>inline-start</c> or <c>inline-end</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>The alignment of the toast on its side: <c>start</c>, <c>center</c> or <c>end</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Present while the arrow cannot be centered on the anchor.</summary>
    public const string Uncentered = "data-uncentered";
}

// Base UI's Toast.Arrow (toast/arrow/ToastArrow.tsx); TST-STR-11, TST-DOM-22, TST-ARIA-08, TST-POS-04, TST-EDGE-03. The
// shared PositionerArrowBehavior: `aria-hidden="true"`, its generated id published through the positioner's context, the
// style and placement frozen from the Positioner's attach (the TooltipArrow shape). No parameters of its own.
/// <summary>Displays an element positioned against the toast's anchor. Base UI's <c>Toast.Arrow</c>; it renders a <c>div</c>.</summary>
public sealed class ToastArrow : PartBase<ToastArrowState>
{
    private PositionerArrowBehavior? behavior;
    private ToastArrowState? state;

    [CascadingParameter]
    private ToastPositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ToastArrowState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            if (state is null || state.Side != mirror.Side || state.Align != mirror.Align || state.Uncentered != mirror.Uncentered)
            {
                state = new ToastArrowState(mirror.Side, mirror.Align, mirror.Uncentered);
            }

            return state;
        }
    }

    // The positioning module finds the arrow by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // TST-EDGE-03.
        ToastPositionerContext positioner = ToastPositionerContext.Require(PositionerContext);
        behavior = new PositionerArrowBehavior(OnMirrorChanged);
        behavior.SetContext(positioner.Positioner);
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
