using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// PRV §3.6 (preview-card/popup/PreviewCardPopup.tsx:55-61, 78-99); ADR 08 Amendments 2026-10-27 ("State records"). The placement
// is the positioner's mirror, rendered by C# (PRV-STA-09's relaxation); the transition status is the `transitions`
// module's (TRN-DOM-01/02).
/// <summary>Represents the state of a preview card popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the preview card is open; rendered as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on; rendered as <c>data-side</c>.</param>
/// <param name="Align">The alignment of the popup against the anchor; rendered as <c>data-align</c>.</param>
/// <param name="Instant">Why transitions are skipped, or <see langword="null"/> when they play; rendered as <c>data-instant</c>.</param>
/// <param name="TransitionStatus">The transition status, whose attributes the browser-side behavior writes.</param>
[PartState]
public sealed partial record PreviewCardPopupState(
    bool Open,
    Side? Side,
    Align? Align,
    PreviewCardInstantType? Instant,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// PRV §3.6; preview-card/popup/PreviewCardPopupDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a preview card popup renders.</summary>
public static class PreviewCardPopupDataAttributes
{
    /// <summary>Represents the attribute present while the preview card is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the preview card is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the popup is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the popup is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the popup's alignment: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute whose value says why transitions are skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// Base UI's PreviewCard.Popup (preview-card/popup/PreviewCardPopup.tsx); the contract is docs/components/preview-card.md §3.6, §5.4.
// - `tabindex="-1"` and data-base-ui-focusable (PRV-DOM-12), no role (PRV-ARIA-01; PRV-ARIA-01).
// - The popup's `transitions` handle (OpenSelector "[data-open]", ReportOpenComplete): the status mirrored into the store,
//   the completion running onOpenChangeComplete and the unmount (PRV-EVT-07, PRV-ANI-04). It selects the open and mounted
//   state, so it brings the render-batch boundary (ADR 16 Amendments 2026-10-27).
// - The hover and dismiss listeners are the session's floating element's, the Positioner (PRV-PTR-06; M4 track 3).
/// <summary>
/// The container of the preview card's content: Base UI's <c>PreviewCard.Popup</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>The part must be placed within a <see cref="PreviewCardPositioner"/>.</remarks>
public sealed class PreviewCardPopup : PartBase<PreviewCardPopupState>
{
    private const string TransitionNone = "transition: none";

    private static readonly StateAttributesMapping<PreviewCardPopupState> s_mapping = PopupStateMapping.CreateOpen<PreviewCardPopupState>();

    private readonly Action mirrorChanged;
    private PreviewCardStoreView? view;
    private PreviewCardPositionerContext? positioner;
    private PreviewCardPortalContext? portalContext;
    private PreviewCardPopupState? state;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private string? consumerStyle;
    private string? frozenStyle;

    /// <summary>Initializes a new instance of the <see cref="PreviewCardPopup"/> class.</summary>
    public PreviewCardPopup()
    {
        mirrorChanged = OnMirrorChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PreviewCardRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PreviewCardPositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PreviewCardPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override PreviewCardPopupState State
    {
        get
        {
            PositionerMirror mirror = positioner!.Mirror;
            PreviewCardStoreView current = view!;
            if (state is null
                || state.Open != current.Open
                || state.Side != mirror.Side
                || state.Align != mirror.Align
                || state.Instant != current.InstantType
                || state.TransitionStatus != current.TransitionStatus)
            {
                state = new PreviewCardPopupState(current.Open, mirror.Side, mirror.Align, current.InstantType, current.TransitionStatus);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PreviewCardPopupState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The popup is placed outside a <see cref="PreviewCardRoot{TPayload}"/> or a <see cref="PreviewCardPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        // PRV-EDGE-01, then PRV-EDGE-04.
        PreviewCardRootContext root = PreviewCardRootContext.Require(RootContext);
        positioner = PreviewCardPositionerContext.Require(PositionerContext);
        positioner.Positioner.AddListener(mirrorChanged);
        view = root.CreateView(StateHasChanged, PreviewCardSelection.Open | PreviewCardSelection.Mounted | PreviewCardSelection.TransitionStatus | PreviewCardSelection.InstantType | PreviewCardSelection.UnmountRequests);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => portalContext = PortalContext;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        PreviewCardStoreView current = view!;
        if (Element is { } element)
        {
            current.SetPopupElement(element);
            await SyncTransitionsAsync(element, current);
        }

        await current.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            positioner?.Positioner.RemoveListener(mirrorChanged);
            view?.ClearPopupElement();
            view?.Dispose();
            view = null;
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // PRV-DOM-12 (FOCUSABLE_POPUP_PROPS, popupStoreUtils.ts:27-30).
        attributes.Set("tabindex", "-1");
        attributes.Set(PopupMarkerAttributes.Focusable, string.Empty);
    }

    // PRV-ANI-02's TRN-DOM-05 clause (ADR 04 Amendments 2026-10-18, handover at attach; Amendments 2026-10-28, gap 13 of
    // M4 track 5): before attach C# renders `transition: none` while starting, ahead of the consumer's style; from attach the
    // style is frozen and the transitions module writes `transition: none` on each starting frame in its own band, under
    // the consumer's style, which it applies as its writer's consumer band.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string text) && text.Length > 0 ? text : null;
        if (frozenStyle is null)
        {
            string? own = view!.TransitionStatus == TransitionStatus.Starting ? TransitionNone : null;
            string? style = own is null ? consumerStyle : consumerStyle is null ? own : own + "; " + consumerStyle;
            if (style is null)
            {
                attributes.Remove(MergeProps.StyleAttribute);
                return;
            }

            attributes.Set(MergeProps.StyleAttribute, style);
            if (transitionsHandle is not null || attaching is not null)
            {
                frozenStyle = style;
            }

            return;
        }

        attributes.Set(MergeProps.StyleAttribute, frozenStyle);
    }

    private void OnMirrorChanged()
    {
        PositionerMirror mirror = positioner!.Mirror;
        if (state is not null && (state.Side != mirror.Side || state.Align != mirror.Align))
        {
            StateHasChanged();
        }
    }

    private async Task SyncTransitionsAsync(ElementReference element, PreviewCardStoreView current)
    {
        TransitionsOptions options = new()
        {
            Open = current.Open,
            Mounted = current.Mounted,
            Status = current.TransitionStatus,
            KeepMounted = portalContext?.KeepMounted ?? false,
            ReportOpenComplete = true,
            OpenSelector = "[data-open]",
            UnmountRequests = current.UnmountRequests,
            DisableTransitionWhileStarting = true,
            Style = consumerStyle ?? string.Empty,
        };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(JSRuntime);
            transitions.StatusChanged += change => view?.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = InvokeAsync(() => view?.CompleteTransitionAsync(opened) ?? Task.CompletedTask);
        }

        if (transitionsHandle is null)
        {
            // A render during the pending first attach awaits it: TwinBase hands a repeated attach the pending handle.
            attaching ??= AttachTransitionsAsync(element, options);
            await attaching;
        }
        else if (options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    // Design point 3 of M4 track 1: the portal node is `display: none` in place until the portal module moves it, so the
    // first attach waits for the move, or the module reads no animation on an element not rendered yet.
    // On Server the move can complete after the close that unmounts this popup: the portal detaches from its own
    // after-render, a round trip after the render that disposed this part, so a disposed popup attaches nothing.
    private async Task AttachTransitionsAsync(ElementReference element, TransitionsOptions options)
    {
        await PopupPortalContext.WhenMoved(Portal);
        if (transitions is not { } twin)
        {
            return;
        }

        sentTransitions = options;
        transitionsHandle = await twin.AttachAsync(element, options);
        attaching = null;
    }
}
