using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The standalone test host's popup: a <c>div</c> with <c>role="dialog"</c> and the floating id, rendered while mounted, that
/// registers as the store's boundary part and attaches the popup's <c>transitions</c> handle as the families' Popups do
/// (<c>ReportOpenComplete</c>, <c>OpenSelector="[data-open]"</c>): its status reports mirror into the lifecycle and its
/// completions run the lifecycle's completion and unmount.
/// </summary>
internal sealed class TestLifecyclePopup : PartBase<TestPopupPopupState>
{
    private static readonly StateAttributesMapping<TestPopupPopupState> s_mapping = PopupStateMapping.CreateOpen<TestPopupPopupState>();

    private StoreSelection<TestLifecycleStoreState, bool>? open;
    private StoreSelection<TestLifecycleStoreState, bool>? mounted;
    private IDisposable? boundaryPart;
    private TestPopupPopupState state = new(false);
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;

    [CascadingParameter]
    private TestLifecycleStore? Store { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    internal TransitionsTwin? Transitions => transitions;

    protected override string DefaultTagName => "div";

    protected override TestPopupPopupState State
    {
        get
        {
            if (state.Open != open!.Value)
            {
                state = new TestPopupPopupState(open.Value);
            }

            return state;
        }
    }

    protected override StateAttributesMapping<TestPopupPopupState>? StateAttributesMapping => s_mapping;

    protected override void SubscribeSelectors()
    {
        SelectorSubscription<TestLifecycleStoreState> subscription = SubscribeTo(Store!);
        open = subscription.Select(TestLifecycleStoreState.Open);
        mounted = subscription.Select(TestLifecycleStoreState.Mounted);
        subscription.Select(TestLifecycleStoreState.Status);
        subscription.Select(TestLifecycleStoreState.UnmountRequests);
        boundaryPart = Store!.RegisterBoundaryPart();
    }

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        Enabled = mounted!.Value;
        base.BuildRenderTree(builder);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is { } element && Enabled)
        {
            await SyncTransitionsAsync(element);
        }
        else if (transitionsHandle is not null)
        {
            TwinHandle<TransitionsOptions> previous = transitionsHandle;
            transitionsHandle = null;
            sentTransitions = null;
            await previous.DisposeAsync();
        }

        await Store!.AtBatchBoundaryAsync();
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("id", Store!.Lifecycle.FloatingId);
        attributes.Set("role", "dialog");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            boundaryPart?.Dispose();
            boundaryPart = null;
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
            }
        }

        base.Dispose(disposing);
    }

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        PopupLifecycleState lifecycle = Store!.Lifecycle;
        TransitionsOptions options = new()
        {
            Open = lifecycle.EffectiveOpen,
            Mounted = lifecycle.Mounted,
            Status = lifecycle.DerivedTransitionStatus,
            ReportOpenComplete = true,
            OpenSelector = "[data-open]",
            UnmountRequests = lifecycle.UnmountRequests,
        };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(JSRuntime);
            transitions.StatusChanged += change => Store.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = Store.CompleteTransitionAsync(opened);
        }

        if (transitionsHandle is null)
        {
            sentTransitions = options;
            transitionsHandle = await transitions.AttachAsync(element, options);
        }
        else if (options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }
}
