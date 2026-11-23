using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MNU §3.8 state object (menu/arrow/MenuArrow.tsx). Side, Align and Uncentered are the `positioning` module's: mirrored.
/// <summary>Represents the state of a context menu arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side the menu is placed on, or <see langword="null"/>; projected as <c>data-side</c>.</param>
/// <param name="Align">The menu's alignment, or <see langword="null"/>; projected as <c>data-align</c>.</param>
/// <param name="Uncentered">A value indicating whether the arrow cannot point at the anchor's center; projected as <c>data-uncentered</c>.</param>
[PartState]
public sealed partial record ContextMenuArrowState(bool Open, [property: MirroredField] Side? Side, [property: MirroredField] Align? Align, [property: MirroredField] bool Uncentered);

// menu/arrow/MenuArrowDataAttributes.ts (MNU-DOM-11).
/// <summary>Provides the names of the data attributes a context menu arrow renders.</summary>
public static class ContextMenuArrowDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute that names the side the menu is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute that names the menu's alignment: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the arrow cannot point at the anchor's center: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";
}

// Base UI's ContextMenu.Arrow is Menu.Arrow (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.Arrow (menu/arrow/MenuArrow.tsx); the contract is docs/components/menu.md §3.8 (MNU-DOM-11, POS-07). A thin
// shell over MenuArrowBehavior (PositionerArrowBehavior, as Popover's).
/// <summary>Represents an arrow that points at the menu's anchor, Base UI's <c>ContextMenu.Arrow</c>, rendered as a <c>div</c>.</summary>
public sealed class ContextMenuArrow : PartBase<ContextMenuArrowState>
{
    private static readonly StateAttributesMapping<ContextMenuArrowState> s_mapping = PopupStateMapping.CreateOpen<ContextMenuArrowState>();

    private MenuArrowBehavior? behavior;
    private ContextMenuArrowState? state;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override ContextMenuArrowState State
    {
        get
        {
            MenuArrowBehavior current = behavior!;
            if (state is null || state.Open != current.Open || state.Side != current.Side || state.Align != current.Align || state.Uncentered != current.Uncentered)
            {
                state = new ContextMenuArrowState(current.Open, current.Side, current.Align, current.Uncentered);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ContextMenuArrowState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuArrowBehavior("ContextMenu", StateHasChanged, OnMirrorChanged);
        behavior.Subscribe(RootContext, PositionerContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(PositionerContext!);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync();

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

    // A part renders on a mirror change only when its state record changed (track 2's rule).
    private void OnMirrorChanged()
    {
        ContextMenuArrowState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
