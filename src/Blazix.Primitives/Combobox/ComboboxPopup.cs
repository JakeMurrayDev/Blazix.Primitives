using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.15, STA-10 (combobox/popup/ComboboxPopup.tsx): Side, Align and AnchorHidden read from the Positioner's mirror (rendered by C#, a dated Writer note), the status mirrored.
/// <summary>Represents the state of a combobox popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment against the anchor; projected as <c>data-align</c>.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; projected as <c>data-anchor-hidden</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record ComboboxPopupState(
    bool Open,
    Side? Side,
    Align? Align,
    bool AnchorHidden,
    [property: MirroredField] TransitionStatus? TransitionStatus,
    bool Empty);

// combobox/popup/ComboboxPopupDataAttributes.ts (CMB-DOM-12).
/// <summary>Provides the names of the data attributes a combobox popup renders.</summary>
public static class ComboboxPopupDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the popup animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the popup animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute present while the filtered list is empty: <c>data-empty</c>.</summary>
    public const string Empty = "data-empty";
}

// Base UI's Combobox.Popup (combobox/popup/ComboboxPopup.tsx; CMB §3.15): a sealed thin part over ComboboxPopupBehavior. Its
// side, align and anchor-hidden attributes are rendered from the Positioner's mirror (the dated Writer note of combobox.md).
/// <summary>Represents the container of a combobox's list, Base UI's <c>Combobox.Popup</c>, rendered as a <c>div</c>.</summary>
/// <remarks>Place it inside a <see cref="ComboboxPositioner"/>. With the input inside it, it is a dialog that traps the focus.</remarks>
public sealed class ComboboxPopup : PartBase<ComboboxPopupState>
{
    private static readonly StateAttributesMapping<ComboboxPopupState> s_mapping = PopupStateMapping.CreatePopup<ComboboxPopupState>();

    private readonly Action positionerChanged;
    private ComboboxPopupBehavior? behavior;
    private ComboboxPopupState? state;

    /// <summary>Initializes a new instance of the <see cref="ComboboxPopup"/> class.</summary>
    public ComboboxPopup()
    {
        positionerChanged = OnPositionerChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxPopup> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxPositionerContext? ComboboxPositioner { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // CMB-API-51
    /// <summary>Gets or sets where focus moves when the popup opens.</summary>
    /// <value>
    /// <see langword="null"/>, the default: with the input outside the popup, focus stays on it; with the input inside, the
    /// input, or the popup for a touch open.
    /// </value>
    [Parameter]
    public FocusTargetValue? InitialFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the popup opens, from how it was opened.</summary>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? InitialFocusResolver { get; set; }

    // CMB-API-52
    /// <summary>Gets or sets where focus moves when the popup closes.</summary>
    /// <value><see langword="null"/>, the default: the trigger with the input inside the popup; otherwise focus stays where it is.</value>
    [Parameter]
    public FocusTargetValue? FinalFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the popup closes, from how it was closed.</summary>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? FinalFocusResolver { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxPopupState State
    {
        get
        {
            ComboboxPopupBehavior popup = behavior!;
            PositionerMirror mirror = popup.Mirror;
            ComboboxStoreState root = popup.Store.State;
            bool open = root.Lifecycle.EffectiveOpen;
            TransitionStatus? status = popup.TransitionStatus;
            bool empty = root.FilteredCount == 0;
            if (state is null || state.Open != open || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden || state.TransitionStatus != status || state.Empty != empty)
            {
                state = new ComboboxPopupState(open, mirror.Side, mirror.Align, mirror.AnchorHidden, status, empty);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ComboboxPopupState>? StateAttributesMapping => s_mapping;

    // For the fixtures and the tests.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxPopupBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? consumerId = AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;
        behavior!.Apply(InitialFocus, InitialFocusResolver, FinalFocus, FinalFocusResolver, consumerId);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ComboboxPositioner?.Positioner.RemoveListener(positionerChanged);
            if (behavior is not null)
            {
                _ = behavior.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        behavior!.Subscribe(Root, ComboboxPositioner?.Positioner, Portal, SubscribeTo);
        ComboboxPositioner!.Positioner.AddListener(positionerChanged);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);

    // CMB-DOM-12: the Popup renders the Positioner's mirrored placement, only when its state record changed.
    private void OnPositionerChanged()
    {
        ComboboxPopupState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
