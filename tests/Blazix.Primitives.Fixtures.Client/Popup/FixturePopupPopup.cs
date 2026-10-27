using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The test-only popup at L4: a <c>div</c> with <c>role="dialog"</c>, rendered while mounted, that reports its element and
/// rendered id to the store and attaches its <c>transitions</c> handle as the families' Popups do
/// (<c>ReportOpenComplete</c>, <c>OpenSelector="[data-open]"</c>), after the enclosing portal has moved its node (design
/// point 3). It selects the open and mounted state, so it registers as the store's boundary part.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class FixturePopupPopup<TPayload> : PartBase<FixturePopupPopupState>
{
    private static readonly StateAttributesMapping<FixturePopupPopupState> s_mapping = PopupStateMapping.CreateOpen<FixturePopupPopupState>();

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private StoreSelection<FixturePopupStoreState<TPayload>, bool>? open;
    private StoreSelection<FixturePopupStoreState<TPayload>, bool>? mounted;
    private IDisposable? boundaryPart;
    private FixturePopupPopupState state = new(false);
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private FixturePopupStore<TPayload>? Store { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [Parameter]
    public bool KeepMounted { get; set; }

    /// <summary>Gets a task that completes once the popup's transitions handle has attached the first time.</summary>
    public Task TwinAttached => attached.Task;

    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    protected override FixturePopupPopupState State
    {
        get
        {
            if (state.Open != open!.Value)
            {
                state = new FixturePopupPopupState(open.Value);
            }

            return state;
        }
    }

    protected override StateAttributesMapping<FixturePopupPopupState>? StateAttributesMapping => s_mapping;

    protected override void SubscribeSelectors()
    {
        SelectorSubscription<FixturePopupStoreState<TPayload>> subscription = SubscribeTo(Store!);
        open = subscription.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.Open);
        mounted = subscription.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.Mounted);
        boundaryPart = Store!.RegisterBoundaryPart();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = KeepMounted || mounted!.Value;
        base.BuildRenderTree(builder);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is { } element && Enabled)
        {
            Store!.SetPopupElement(element, RenderedId);
            await PopupPortalContext.WhenMoved(Portal);
            await SyncTransitionsAsync(element);
        }
        else if (transitionsHandle is not null)
        {
            TwinHandle<TransitionsOptions> previous = transitionsHandle;
            transitionsHandle = null;
            sentTransitions = null;
            attaching = null;
            await previous.DisposeAsync();
        }

        await Store!.AtBatchBoundaryAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Store is not null)
        {
            if (Store.Popup.PopupElementKnown)
            {
                Store.ClearPopupElement();
            }

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

    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (!HasConsumerId)
        {
            attributes.Set("id", Store!.Popup.FloatingId);
        }

        attributes.Set("role", "dialog");

        // TRN-DOM-06: a kept-mounted popup that is not mounted is hidden (the module hides it as its exit ends, ahead of this).
        if (KeepMounted && !mounted!.Value)
        {
            attributes.Set("hidden", true);
        }
    }

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        PopupStoreState<TPayload> popup = Store!.Popup;
        TransitionsOptions options = new()
        {
            Open = popup.EffectiveOpen,
            Mounted = popup.Mounted,
            Status = popup.DerivedTransitionStatus,
            KeepMounted = KeepMounted,
            HiddenWhenUnmounted = KeepMounted,
            ReportOpenComplete = true,
            OpenSelector = "[data-open]",
            UnmountRequests = popup.UnmountRequests,
        };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(JSRuntime);
            transitions.StatusChanged += change => Store.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = InvokeAsync(() => Store.CompleteTransitionAsync(opened));
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

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : Store!.Popup.FloatingId;
}
