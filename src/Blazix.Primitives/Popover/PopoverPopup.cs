using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// POP-STA-13, §3.5 state object (popover/popup/PopoverPopup.tsx:76-82, 128-149). Side and Align are C#-rendered from the
// Positioner's mirror (POP-DOM-27; ADR 08 Amendments 2026-10-27), a round trip behind the Positioner's on Server;
// TransitionStatus is the `transitions` module's (ADR 04 §2): mirrored.
/// <summary>Represents the state of a popover popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popover is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popover is placed on; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment against the anchor; projected as <c>data-align</c>.</param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the popup's enter or exit transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record PopoverPopupState(
    bool Open,
    Side? Side,
    Align? Align,
    PopoverInstantType? Instant,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// popover/popup/PopoverPopupDataAttributes.ts (POP-DOM-25..28).
/// <summary>Provides the names of the data attributes a popover popup renders.</summary>
public static class PopoverPopupDataAttributes
{
    /// <summary>Represents the attribute present while the popover is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popover is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the popup is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the popup is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute whose value says why the next transition is skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// popover/popup/PopoverPopupCssVars.ts (POP-DOM-30).
/// <summary>Provides the names of the CSS variables a popover popup sets.</summary>
public static class PopoverPopupCssVars
{
    /// <summary>Represents the popup's width while a viewport animates a content change: <c>--popup-width</c>.</summary>
    public const string PopupWidth = PopupCssVars.PopupWidth;

    /// <summary>Represents the popup's height while a viewport animates a content change: <c>--popup-height</c>.</summary>
    public const string PopupHeight = PopupCssVars.PopupHeight;
}

// Base UI's Popover.Popup (popover/popup/PopoverPopup.tsx); the contract is docs/components/popover.md §3.5, §5.4, §6.3, §9.
// - role="dialog", the floating id unless the consumer's (POP-DOM-23), tabindex="-1" and data-base-ui-focusable, never
//   managed (POP-DOM-24; §16 Q3 mirrored, settled by the orchestrator 2026-10-27), aria-labelledby and aria-describedby
//   from the Title and Description (POP-ARIA-05/06; the consumer's wins).
// - Its TransitionsTwin: the completion watcher, OpenSelector [data-open], ReportOpenComplete, UnmountRequests (the
//   Collapsible precedent); its style is handed over at attach (ADR 04 Amendments 2026-10-18), so the `transitions` and
//   `popup` modules write `transition: none` and the auto-resize variables over the consumer's style.
// - The close-part count, cascaded fixed: FocusManagerModal = Modal is not off and a PopoverClose is inside (POP-STA-10).
// - It selects the open and mounted state, so it registers as a boundary part (ADR 16 Amendments 2026-10-27).
/// <summary>Represents the container of a popover's content, Base UI's <c>Popover.Popup</c>, rendered as a <c>div</c>.</summary>
public sealed class PopoverPopup : PartBase<PopoverPopupState>
{
    private const string OpenSelector = "[data-open]";
    private const string StartingStyle = "transition: none";

    private static readonly StateAttributesMapping<PopoverPopupState> s_mapping = PopupStateMapping.CreateOpen<PopoverPopupState>();

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PopoverClosePartCount closeParts;
    private readonly Action positionerChanged;
    private readonly RenderFragment cascadedContent;
    private PopoverSubscription? subscription;
    private PopoverSelection<bool>? open;
    private PopoverSelection<bool>? mounted;
    private PopoverSelection<TransitionStatus?>? status;
    private PopoverSelection<PopoverInstantType?>? instant;
    private PopoverSelection<string?>? titleId;
    private PopoverSelection<string?>? descriptionId;
    private PopoverSelection<PopoverModalKind>? modal;
    private PopoverSelection<int>? unmountRequests;
    private IDisposable? boundaryPart;
    private PopoverPopupState? state;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private string? frozenStyle;
    private string? consumerStyle;
    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="PopoverPopup"/> class.</summary>
    public PopoverPopup()
    {
        closeParts = new PopoverClosePartCount(OnClosePartsChanged);
        positionerChanged = OnPositionerChanged;
        cascadedContent = RenderCascade;
    }

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : RootContext!.State.FloatingId;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override PopoverPopupState State
    {
        get
        {
            PositionerMirror mirror = PositionerContext!.Mirror;
            bool isOpen = open!.Value;
            PopoverInstantType? instantType = instant!.Value;
            TransitionStatus? current = status!.Value;
            if (state is null || state.Open != isOpen || state.Side != mirror.Side || state.Align != mirror.Align || state.Instant != instantType || state.TransitionStatus != current)
            {
                state = new PopoverPopupState(isOpen, mirror.Side, mirror.Align, instantType, current);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PopoverPopupState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes once the popup's transitions handle has attached the first time.</summary>
    internal Task TwinAttached => attached.Task;

    /// <summary>Gets a value indicating whether a close part is mounted inside the popup.</summary>
    internal bool HasClosePart => closeParts.HasClosePart;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The popup is not inside a <see cref="PopoverRoot{TPayload}"/> and a <see cref="PopoverPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext root = PopoverRootContext.Require(RootContext);
        PopoverArrow.RequirePositioner(PositionerContext);
        subscription = root.Subscribe(StateHasChanged);
        open = subscription.Select(PopoverSelectors.Open);
        mounted = subscription.Select(PopoverSelectors.Mounted);
        status = subscription.Select(PopoverSelectors.TransitionStatus);
        instant = subscription.Select(PopoverSelectors.InstantType);
        titleId = subscription.Select(PopoverSelectors.TitleElementId);
        descriptionId = subscription.Select(PopoverSelectors.DescriptionElementId);
        modal = subscription.Select(PopoverSelectors.Modal);
        unmountRequests = subscription.Select(PopoverSelectors.UnmountRequests);
        boundaryPart = root.RegisterBoundaryPart();
        PositionerContext!.AddListener(positionerChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerContent = ChildContent;
        ChildContent = cascadedContent;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // POP-STA-10: written every render, so the Trigger's guards and the trap agree.
        RootContext!.SetFocusManagerModal(modal!.Value != PopoverModalKind.NonModal && closeParts.HasClosePart);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        PopoverRootContext root = RootContext!;
        if (Element is { } element)
        {
            root.SetPopupElement(element, RenderedId);
            await PopupPortalContext.WhenMoved(Portal);
            await SyncTransitionsAsync(element);
        }

        await root.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PositionerContext?.RemoveListener(positionerChanged);
            subscription?.Dispose();
            subscription = null;
            boundaryPart?.Dispose();
            boundaryPart = null;
            if (RootContext is not null)
            {
                RootContext.ClearPopupElement();
                RootContext.SetFocusManagerModal(false);
            }

            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
                transitionsHandle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (!HasConsumerId)
        {
            attributes.Set("id", RootContext!.State.FloatingId);
        }

        attributes.Set("role", "dialog");
        attributes.Set("tabindex", "-1");
        attributes.Set(PopupMarkerAttributes.Focusable, string.Empty);
        if (titleId!.Value is { } labelledBy)
        {
            attributes.Set("aria-labelledby", labelledBy);
        }

        if (descriptionId!.Value is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
    }

    // ADR 04 Amendments 2026-10-18 (handover at attach): before attach C# renders `transition: none` while starting
    // (TRN-DOM-05, POP-DOM-29) before the consumer's style; from attach the style is frozen and the modules own it.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string text) && text.Length > 0 ? text : null;
        if (frozenStyle is null)
        {
            string? own = status!.Value == TransitionStatus.Starting ? StartingStyle : null;
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

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        TransitionsOptions options = new()
        {
            Open = open!.Value,
            Mounted = mounted!.Value,
            Status = status!.Value,
            ReportOpenComplete = true,
            OpenSelector = OpenSelector,
            UnmountRequests = unmountRequests!.Value,
        };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(JSRuntime);
            transitions.StatusChanged += change => RootContext?.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = InvokeAsync(() => RootContext!.CompleteTransitionAsync(opened));
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

    private async Task AttachTransitionsAsync(ElementReference element, TransitionsOptions options)
    {
        sentTransitions = options;
        transitionsHandle = await transitions!.AttachAsync(element, options);
        attaching = null;
        attached.TrySetResult();
    }

    private void RenderCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<PopoverClosePartCount>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PopoverClosePartCount>.Value), closeParts);
        builder.AddComponentParameter(2, nameof(CascadingValue<PopoverClosePartCount>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<PopoverClosePartCount>.ChildContent), consumerContent);
        builder.CloseComponent();
    }

    private void OnClosePartsChanged() => StateHasChanged();

    // POP-DOM-27: the Popup renders the Positioner's mirrored placement, only when its state record changed.
    private void OnPositionerChanged()
    {
        PopoverPopupState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
