using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL-STA-19 (select/backdrop/SelectBackdrop.tsx:34-37, 59-68): TransitionStatus is the `transitions` module's (ADR 04 §2).
/// <summary>Represents the state of a select backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the enter or exit transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record SelectBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// select/backdrop/SelectBackdropDataAttributes.ts (SEL-DOM-17).
/// <summary>Provides the names of the data attributes a select backdrop renders.</summary>
public static class SelectBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Select.Backdrop (select/backdrop/SelectBackdrop.tsx); SEL-DOM-17, SEL-ARIA-15: `role="presentation"`,
// `user-select: none` under the consumer's style, `hidden` while not mounted, its own `transitions` handle (the
// PopoverBackdrop precedent) after the portal's move.
/// <summary>Represents an overlay displayed beneath a select's popup, Base UI's <c>Select.Backdrop</c>, rendered as a <c>div</c>.</summary>
public sealed class SelectBackdrop : PartBase<SelectBackdropState>
{
    private const string BaseStyle = "user-select: none; -webkit-user-select: none";
    private const string OpenSelector = "[data-open]";

    private static readonly StateAttributesMapping<SelectBackdropState> s_mapping = PopupStateMapping.CreateOpen<SelectBackdropState>();

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private SelectStore? store;
    private StoreSelection<SelectStoreState, bool>? open;
    private StoreSelection<SelectStoreState, bool>? mounted;
    private StoreSelection<SelectStoreState, TransitionStatus?>? status;
    private StoreSelection<SelectStoreState, int>? unmountRequests;
    private StoreSelection<SelectStoreState, bool>? retainExit;
    private SelectBackdropState? state;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectBackdropState State
    {
        get
        {
            bool isOpen = open!.Value;
            TransitionStatus? current = status!.Value;
            if (state is null || state.Open != isOpen || state.TransitionStatus != current)
            {
                state = new SelectBackdropState(isOpen, current);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the backdrop's transitions handle has attached the first time.</summary>
    internal Task TwinAttached => attached.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The backdrop is not inside a <see cref="SelectRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
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

        // Issue 213 (main's PR 215): a render during the attach sends what the attach did not carry.
        if (transitionsHandle is not null && !disposed && TransitionsOptions() is { } options && options != sentTransitions)
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
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        open = subscription.Select(SelectStoreState.Open);
        mounted = subscription.Select(SelectStoreState.Mounted);
        status = subscription.Select(SelectStoreState.TransitionStatus);
        unmountRequests = subscription.Select(SelectStoreState.UnmountRequests);
        retainExit = subscription.Select(SelectStoreState.PreventUnmountingOnClose);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!mounted!.Value)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set(MergeProps.StyleAttribute, BaseStyle);
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
