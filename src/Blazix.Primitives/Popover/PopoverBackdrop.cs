using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// POP-STA-13, §3.7 state object (popover/backdrop/PopoverBackdrop.tsx:29-32, 55-64). TransitionStatus is the `transitions`
// module's (ADR 04 §2): mirrored.
/// <summary>Represents the state of a popover backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popover is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the popover's enter or exit transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record PopoverBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// popover/backdrop/PopoverBackdropDataAttributes.ts (POP-DOM-36).
/// <summary>Provides the names of the data attributes a popover backdrop renders.</summary>
public static class PopoverBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the popover is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popover is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Popover.Backdrop (popover/backdrop/PopoverBackdrop.tsx); the contract is docs/components/popover.md §3.7, §5.6.
// role="presentation", hidden while unmounted (POP-DOM-34), user-select: none always and pointer-events: none while
// hover-opened, under the consumer's style (POP-DOM-35); its own TransitionsTwin writes the status attributes (the
// Collapsible precedent, TRN-ANI-02). It may render anywhere under the Root (POP-STR-08).
/// <summary>Represents an overlay beneath a popover, Base UI's <c>Popover.Backdrop</c>, rendered as a <c>div</c>.</summary>
public sealed class PopoverBackdrop : PartBase<PopoverBackdropState>
{
    private const string BaseStyle = "user-select: none; -webkit-user-select: none";
    private const string HoverStyle = "user-select: none; -webkit-user-select: none; pointer-events: none";
    private const string OpenSelector = "[data-open]";

    private static readonly StateAttributesMapping<PopoverBackdropState> s_mapping = PopupStateMapping.CreateOpen<PopoverBackdropState>();

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PopoverSubscription? subscription;
    private PopoverSelection<bool>? open;
    private PopoverSelection<bool>? mounted;
    private PopoverSelection<TransitionStatus?>? status;
    private PopoverSelection<bool>? hoverOpened;
    private PopoverSelection<int>? unmountRequests;
    private PopoverSelection<bool>? retainExit;
    private IDisposable? retainRegistration;
    private PopoverBackdropState? state;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private bool announcedRetain;
    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override PopoverBackdropState State
    {
        get
        {
            bool isOpen = open!.Value;
            TransitionStatus? current = status!.Value;
            if (state is null || state.Open != isOpen || state.TransitionStatus != current)
            {
                state = new PopoverBackdropState(isOpen, current);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PopoverBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the backdrop's transitions handle has attached the first time.</summary>
    internal Task TwinAttached => attached.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The backdrop is not inside a <see cref="PopoverRoot{TPayload}"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext root = PopoverRootContext.Require(RootContext);
        subscription = root.Subscribe(StateHasChanged);
        open = subscription.Select(PopoverSelectors.Open);
        mounted = subscription.Select(PopoverSelectors.Mounted);
        status = subscription.Select(PopoverSelectors.TransitionStatus);
        hoverOpened = subscription.Select(PopoverSelectors.HoverOpened);
        unmountRequests = subscription.Select(PopoverSelectors.UnmountRequests);
        retainExit = subscription.Select(PopoverSelectors.PreventUnmountingOnClose);

        // Issue 147 ((B1)): the backdrop's own handle keeps a retained close's ending phase as the popup's does, so it hears
        // the store at the PreventUnmountOnClose() call as the popup does.
        retainRegistration = root.OnUnmountPreventionChanging(AnnounceRetainExit);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is null && Id is null)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);

        // Issue 149's residual: the portal's move can complete after the part was disposed, which must create no twin.
        if (disposed)
        {
            return;
        }

        transitions ??= new TransitionsTwin(JSRuntime);
        if (transitionsHandle is null)
        {
            attaching ??= AttachAsync(transitions);
            await attaching;
        }
        else if (TransitionsOptions() is { } options && options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposed = true;
            subscription?.Dispose();
            subscription = null;
            retainRegistration?.Dispose();
            retainRegistration = null;
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
        attributes.Set("role", "presentation");
        if (!mounted!.Value)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set(MergeProps.StyleAttribute, hoverOpened!.Value ? HoverStyle : BaseStyle);
    }

    private TransitionsOptions TransitionsOptions() => new()
    {
        Open = open!.Value,
        Mounted = mounted!.Value,
        Status = status!.Value,
        UnmountRequests = unmountRequests!.Value,
        OpenSelector = OpenSelector,
        RetainExit = retainExit!.Value,
    };

    // A twin not created yet has nothing to address, nor has a backdrop without an element reference (a render function's):
    // its attach or its next update sends the store's value then.
    private void AnnounceRetainExit(bool retain)
    {
        if (transitions is null || Element is not { } element || announcedRetain == retain)
        {
            return;
        }

        announcedRetain = retain;
        _ = transitions.RetainExitAsync(element, retain).AsTask();
    }

    // As the popup's (issue 147 (B1), as built): the options are read once the module is imported, right before the attach.
    private async Task AttachAsync(TransitionsTwin twin)
    {
        await twin.PrepareAsync();
        if (disposed || transitions != twin)
        {
            return;
        }

        TransitionsOptions options = TransitionsOptions();
        sentTransitions = options;
        transitionsHandle = Element is { } element
            ? await twin.AttachAsync(element, options)
            : await twin.AttachAsync(Id!, options);
        attaching = null;
        attached.TrySetResult();
    }
}
