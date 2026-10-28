using System.Globalization;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// POP-STA-13, §3.8 state object (popover/viewport/PopoverViewport.tsx:34-38, 48-61). ActivationDirection and Transitioning
// are the `popup` module's (ADR 04 Amendments 2026-10-27): mirrored. Instant is C#'s.
/// <summary>Represents the state of a popover viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="ActivationDirection">
/// The direction from the previous trigger to the new one, such as <c>right down</c>, or <see langword="null"/>; its
/// <c>data-activation-direction</c> is written by the browser-side behavior.
/// </param>
/// <param name="Transitioning">
/// A value indicating whether the previous content is still shown during a change; its <c>data-transitioning</c> is
/// written by the browser-side behavior.
/// </param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record PopoverViewportState(
    [property: MirroredField] string? ActivationDirection,
    [property: MirroredField] bool Transitioning,
    PopoverInstantType? Instant);

// popover/viewport/PopoverViewportDataAttributes.ts (POP-DOM-38..40).
/// <summary>Provides the names of the data attributes a popover viewport renders.</summary>
public static class PopoverViewportDataAttributes
{
    /// <summary>Represents the attribute whose value is the direction from the previous trigger to the new one: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = PopupViewportDataAttributes.ActivationDirection;

    /// <summary>Represents the attribute present while the previous content is still shown: <c>data-transitioning</c>.</summary>
    public const string Transitioning = PopupViewportDataAttributes.Transitioning;

    /// <summary>Represents the attribute whose value says why the next transition is skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// popover/viewport/PopoverViewportCssVars.ts (POP-DOM-41).
/// <summary>Provides the names of the CSS variables a popover viewport's previous content sets.</summary>
public static class PopoverViewportCssVars
{
    /// <summary>Represents the popup's width when the previous content was shown: <c>--popup-width</c>.</summary>
    public const string PopupWidth = PopupCssVars.PopupWidth;

    /// <summary>Represents the popup's height when the previous content was shown: <c>--popup-height</c>.</summary>
    public const string PopupHeight = PopupCssVars.PopupHeight;
}

// Base UI's Popover.Viewport (popover/viewport/PopoverViewport.tsx, utils/usePopupViewport.tsx); the contract is
// docs/components/popover.md §3.8, §5.7, PSH-STR-07, PSH-ALG-14. C# renders the content in `<div data-current>` keyed by
// usePopupContentKey's rule: a new key when the active trigger changes, and once more when the payload arrives a render
// later. The clone in `[data-previous]`, the direction, data-transitioning and the auto-resize are the `popup` module's;
// while mounted the Viewport asks the positioning module for the adaptive origin (PSH-POS-02).
/// <summary>
/// Represents a container that animates a popover's content when it moves between triggers, Base UI's
/// <c>Popover.Viewport</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>
/// The content renders inside a <c>div</c> with <c>data-current</c>; while the popover moves to another trigger, a copy of
/// the previous content is shown in a <c>div</c> with <c>data-previous</c> before it.
/// </remarks>
public sealed class PopoverViewport : PartBase<PopoverViewportState>
{
    private readonly RenderFragment current;
    private PopupModuleAttachment? module;
    private PopoverSubscription? subscription;
    private PopoverSelection<PopoverInstantType?>? instant;
    private PopoverSelection<string?>? activeTriggerId;
    private PopoverSelection<int>? payloadVersion;
    private PopoverSelection<bool>? open;
    private PopoverSelection<string?>? popupId;
    private PopoverViewportState? state;
    private RenderFragment? consumerContent;
    private string? keyedTriggerId;
    private int keyedPayloadVersion;
    private bool payloadPending;
    private int contentKey;
    private string? key;
    private string? activationDirection;
    private bool transitioning;

    /// <summary>Initializes a new instance of the <see cref="PopoverViewport"/> class.</summary>
    public PopoverViewport()
    {
        current = RenderCurrent;
    }

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

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirrors here.
    /// <inheritdoc />
    protected override PopoverViewportState State
    {
        get
        {
            PopoverInstantType? instantType = instant!.Value;
            if (state is null || state.Instant != instantType || state.ActivationDirection != activationDirection || state.Transitioning != transitioning)
            {
                state = new PopoverViewportState(activationDirection, transitioning, instantType);
            }

            return state;
        }
    }

    /// <summary>Gets the key the current content renders with, for the tests.</summary>
    internal string? ContentKey => key;

    /// <summary>Gets a task that completes when the viewport's <c>popup</c> handle has attached.</summary>
    internal Task TwinAttached => module!.Attached;

    /// <summary>Gets the viewport's <c>popup</c> twin, for the tests.</summary>
    internal PopupTwin Twin => module!.Twin;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The viewport is not inside a <see cref="PopoverRoot{TPayload}"/> and a <see cref="PopoverPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext root = PopoverRootContext.Require(RootContext);
        PopoverArrow.RequirePositioner(PositionerContext);
        subscription = root.Subscribe(StateHasChanged);
        instant = subscription.Select(PopoverSelectors.InstantType);
        activeTriggerId = subscription.Select(PopoverSelectors.ActiveTriggerId);
        payloadVersion = subscription.Select(PopoverSelectors.PayloadVersion);
        open = subscription.Select(PopoverSelectors.Open);
        popupId = subscription.Select(PopoverSelectors.PopupId);
        module = new PopupModuleAttachment(JSRuntime);
        module.Twin.ViewportChanged += report => _ = InvokeAsync(() => Mirror(report.ActivationDirection, report.Transitioning));
        keyedTriggerId = activeTriggerId.Value;
        keyedPayloadVersion = payloadVersion.Value;
        root.SetAdaptiveOrigin(true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerContent = ChildContent;
        ChildContent = current;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        UpdateContentKey();
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is not { } element)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);
        await module!.SyncAsync(element, new PopupOptions
        {
            Viewport = new PopupViewportOptions
            {
                Open = open!.Value,
                PopupId = popupId!.Value,
                ContentVersion = payloadVersion!.Value,
            },
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            RootContext?.SetAdaptiveOrigin(false);
            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>Mirrors the direction and the transitioning flag the browser-side behavior wrote.</summary>
    /// <param name="direction">The activation direction, or <see langword="null"/>.</param>
    /// <param name="isTransitioning">Whether the previous content is shown.</param>
    internal void Mirror(string? direction, bool isTransitioning)
    {
        if (string.Equals(activationDirection, direction, StringComparison.Ordinal) && transitioning == isTransitioning)
        {
            return;
        }

        activationDirection = direction;
        transitioning = isTransitioning;
        StateHasChanged();
    }

    // usePopupViewport.tsx:368-399 (usePopupContentKey): remount on a trigger change, and once more if the payload had not
    // caught up with it.
    private void UpdateContentKey()
    {
        string? triggerId = activeTriggerId!.Value;
        int version = payloadVersion!.Value;
        bool triggerChanged = !string.Equals(triggerId, keyedTriggerId, StringComparison.Ordinal);
        bool payloadChanged = version != keyedPayloadVersion;
        if (triggerChanged)
        {
            contentKey++;
            payloadPending = !payloadChanged;
            key = null;
        }
        else if (payloadPending && payloadChanged)
        {
            contentKey++;
            payloadPending = false;
            key = null;
        }

        keyedTriggerId = triggerId;
        keyedPayloadVersion = version;
        key ??= string.Create(CultureInfo.InvariantCulture, $"{triggerId ?? "current"}-{contentKey}");
    }

    private void RenderCurrent(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.SetKey(key);
        builder.AddAttribute(1, PopupViewportDataAttributes.Current, string.Empty);
        builder.AddContent(2, consumerContent);
        builder.CloseElement();
    }
}
