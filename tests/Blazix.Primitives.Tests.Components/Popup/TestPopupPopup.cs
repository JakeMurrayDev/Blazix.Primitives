using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of the test-only popup: whether it is open.</summary>
[PartState]
public sealed partial record TestPopupPopupState(bool Open);

/// <summary>
/// A test-only popup: a <c>div</c> with <c>role="dialog"</c>, rendered while mounted, that registers its element and the id
/// it renders (the popup's generated id unless the consumer gives one) with its store (PSH-STA-04, PSH-ARIA-03/08), and
/// registers as the store's boundary part, since it selects the open and mounted state (design point 8). With
/// <see cref="AttachesTransitions"/> it attaches the popup's <c>transitions</c> handle as the families' Popups do
/// (<c>ReportOpenComplete</c>, <c>OpenSelector="[data-open]"</c>): its status reports mirror into the store and its
/// completions run the store's completion and unmount.
/// </summary>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class TestPopupPopup<TPayload> : PartBase<TestPopupPopupState>
{
    private static readonly StateAttributesMapping<TestPopupPopupState> s_mapping = PopupStateMapping.CreateOpen<TestPopupPopupState>();

    private StoreSelection<TestPopupStoreState<TPayload>, bool>? open;
    private StoreSelection<TestPopupStoreState<TPayload>, bool>? mounted;
    private IDisposable? boundaryPart;
    private TestPopupPopupState state = new(false);
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;

    [CascadingParameter]
    private TestPopupStore<TPayload>? Store { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool KeepMounted { get; set; }

    [Parameter]
    public bool AttachesTransitions { get; set; }

    internal TransitionsTwin? Transitions => transitions;

    public int Renders { get; private set; }

    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
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
        SelectorSubscription<TestPopupStoreState<TPayload>> subscription = SubscribeTo(Store!);
        open = subscription.Select(PopupSelectors<TestPopupStoreState<TPayload>, TPayload>.Open);
        mounted = subscription.Select(PopupSelectors<TestPopupStoreState<TPayload>, TPayload>.Mounted);
        boundaryPart = Store!.RegisterBoundaryPart();
    }

    // Base UI's Portal renders nothing while unmounted; the test popup decides on each render, its store's selection included.
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        Enabled = KeepMounted || mounted!.Value;
        base.BuildRenderTree(builder);
    }

    // The popup part runs the store's render-batch boundary after each of its renders (design point 8).
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is { } element && Enabled)
        {
            Store!.SetPopupElement(element, RenderedId);
            if (AttachesTransitions)
            {
                await SyncTransitionsAsync(element);
            }
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
        Renders++;
        if (!HasConsumerId)
        {
            attributes.Set("id", Store!.Popup.FloatingId);
        }

        attributes.Set("role", "dialog");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Store is not null && Store.Popup.PopupElementKnown)
        {
            Store.ClearPopupElement();
        }

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
        PopupStoreState<TPayload> popup = Store!.Popup;
        TransitionsOptions options = new()
        {
            Open = popup.EffectiveOpen,
            Mounted = popup.Mounted,
            Status = popup.DerivedTransitionStatus,
            KeepMounted = KeepMounted,
            ReportOpenComplete = true,
            OpenSelector = "[data-open]",
            UnmountRequests = popup.UnmountRequests,
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

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : Store!.Popup.FloatingId;
}
