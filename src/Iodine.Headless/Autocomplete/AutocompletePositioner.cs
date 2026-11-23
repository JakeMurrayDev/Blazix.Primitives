using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.14, STA-09 (combobox/positioner/AutocompletePositioner.tsx): Side, Align and AnchorHidden are the `positioning` module's from attach (ADR 04 §2): mirrored.
/// <summary>Represents the state of an autocomplete positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on, after collisions; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor, after collisions; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; its <c>data-anchor-hidden</c> is written by the browser-side behavior.</param>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record AutocompletePositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    bool Empty);

// combobox/positioner/AutocompletePositionerDataAttributes.ts (CMB-DOM-13).
/// <summary>Provides the names of the data attributes an autocomplete positioner renders.</summary>
public static class AutocompletePositionerDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the filtered list is empty: <c>data-empty</c>.</summary>
    public const string Empty = "data-empty";
}

// combobox/positioner/AutocompletePositionerCssVars.ts (CMB-DOM-13).
/// <summary>Provides the names of the CSS variables an autocomplete positioner sets.</summary>
public static class AutocompletePositionerCssVars
{
    /// <summary>Represents the width available to the popup: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the height available to the popup: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the transform origin of the popup: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";
}

// Base UI's Combobox.Positioner (combobox/positioner/AutocompletePositioner.tsx; CMB §3.14, §10): a sealed thin part over
// ComboboxPositionerBehavior with Popover's parameters (CMB-API-50).
/// <summary>Represents the element that positions an autocomplete's popup against its anchor, Base UI's <c>Autocomplete.Positioner</c>.</summary>
/// <remarks>Place it inside a <see cref="AutocompletePortal"/>. Its anchor is the input group, else the input; the trigger when the input is inside the popup.</remarks>
public sealed class AutocompletePositioner : PartBase<AutocompletePositionerState>
{
    private static readonly StateAttributesMapping<AutocompletePositionerState> s_mapping = PopupStateMapping.CreateOpen<AutocompletePositionerState>();

    private ComboboxPositionerBehavior? behavior;
    private AutocompletePositionerState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompletePositioner> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <summary>Gets or sets the element the popup is positioned against.</summary>
    /// <value><see langword="null"/>, the default, for the input group, else the input; the trigger when the input is inside the popup.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position the popup uses.</summary>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor the popup is placed on.</summary>
    /// <value><see langword="null"/>, the default, for the bottom.</value>
    [Parameter]
    public Side? Side { get; set; }

    /// <summary>Gets or sets the distance between the anchor and the popup.</summary>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    /// <summary>Gets or sets the alignment against the anchor.</summary>
    /// <value><see langword="null"/>, the default, for the center.</value>
    [Parameter]
    public Align? Align { get; set; }

    /// <summary>Gets or sets the offset along the alignment axis.</summary>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    /// <summary>Gets or sets the area the popup stays inside.</summary>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    /// <summary>Gets or sets the space kept between the popup and the edges of the collision boundary.</summary>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stays in view after the anchor scrolls out of it.</summary>
    [Parameter]
    public bool Sticky { get; set; }

    /// <summary>Gets or sets the minimum space between the arrow and the popup's edges.</summary>
    [Parameter]
    public double? ArrowPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stops following the anchor's layout changes.</summary>
    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    /// <summary>Gets or sets how the popup avoids collisions.</summary>
    /// <value><see langword="null"/>, the default, for the list preset: flip, shift, and no fallback to the other axis.</value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AutocompletePositionerState State
    {
        get
        {
            PositionerMirror mirror = behavior!.Mirror;
            ComboboxStoreState root = behavior.Store.State;
            bool open = root.Lifecycle.EffectiveOpen;
            bool empty = root.FilteredCount == 0;
            if (state is null || state.Open != open || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden || state.Empty != empty)
            {
                state = new AutocompletePositionerState(open, mirror.Side, mirror.Align, mirror.AnchorHidden, empty);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AutocompletePositionerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures and the tests.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxPositionerBehavior(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), OnMirrorChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(Inputs(), DirectionContext);
        ChildContent = behavior.Cascade(ChildContent);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element, Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, PortalContext, Portal, SubscribeTo);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.SetInputs(Inputs());
        attributes.Set("role", "presentation");
        behavior.Positioner.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.Positioner.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    private PositionerInputs Inputs() => new()
    {
        Anchor = Anchor,
        PositionMethod = PositionMethod,
        Side = Side,
        SideOffset = SideOffset,
        Align = Align,
        AlignOffset = AlignOffset,
        CollisionBoundary = CollisionBoundary,
        CollisionPadding = CollisionPadding,
        Sticky = Sticky,
        ArrowPadding = ArrowPadding,
        DisableAnchorTracking = DisableAnchorTracking,
        CollisionAvoidance = CollisionAvoidance,
    };

    // A part renders on a mirror change only when its state record changed (M4 track 2's rule).
    private void OnMirrorChanged()
    {
        AutocompletePositionerState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
