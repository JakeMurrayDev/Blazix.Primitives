using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.16, STA-11 (combobox/arrow/AutocompleteArrow.tsx): the arrow's placement mirrored.
/// <summary>Represents the state of an autocomplete arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="Uncentered">A value indicating whether the arrow could not be centered on the anchor; its <c>data-uncentered</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record AutocompleteArrowState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered);

// combobox/arrow/AutocompleteArrowDataAttributes.ts (CMB-DOM-15).
/// <summary>Provides the names of the data attributes an autocomplete arrow renders.</summary>
public static class AutocompleteArrowDataAttributes
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

// Base UI's Combobox.Arrow (combobox/arrow/AutocompleteArrow.tsx; CMB §3.16). Over ComboboxArrowBehavior: aria-hidden, the
// positioner's arrow mirror (side, align, uncentered) and its arrow element registration.
/// <summary>Represents an arrow pointing from an autocomplete's popup to its anchor, Base UI's <c>Autocomplete.Arrow</c>, rendered as a <c>div</c>.</summary>
public sealed class AutocompleteArrow : PartBase<AutocompleteArrowState>
{
    private static readonly StateAttributesMapping<AutocompleteArrowState> s_mapping = PopupStateMapping.CreateOpen<AutocompleteArrowState>();

    private ComboboxArrowBehavior? behavior;

    private AutocompleteArrowState state = new(false, null, null, false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompleteArrow> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxPositionerContext? AutocompletePositioner { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AutocompleteArrowState State
    {
        get
        {
            ComboboxArrowBehavior arrow = behavior!;
            PositionerMirror mirror = arrow.Arrow.Mirror;
            if (state.Open != arrow.Open || state.Side != mirror.Side || state.Align != mirror.Align || state.Uncentered != mirror.Uncentered)
            {
                state = new AutocompleteArrowState(arrow.Open, mirror.Side, mirror.Align, mirror.Uncentered);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AutocompleteArrowState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxArrowBehavior(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.Arrow.AfterRenderAsync();

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, AutocompletePositioner?.Positioner, SubscribeTo);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.Arrow.FinishAttributes(attributes, Id!);
}
