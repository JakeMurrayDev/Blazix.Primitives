using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of the test-only popup: whether it is open.</summary>
[PartState]
internal sealed partial record TestPopupPopupState(bool Open);

/// <summary>
/// A test-only popup: a <c>div</c> with <c>role="dialog"</c>, rendered while mounted, that registers its element and the id
/// it renders (the popup's generated id unless the consumer gives one) with its store (PSH-STA-04, PSH-ARIA-03/08), and
/// registers as the store's boundary part, since it selects the open and mounted state (design point 8).
/// </summary>
[CascadingTypeParameter(nameof(TPayload))]
internal sealed class TestPopupPopup<TPayload> : PartBase<TestPopupPopupState>
{
    private static readonly StateAttributesMapping<TestPopupPopupState> s_mapping = PopupStateMapping.CreateOpen<TestPopupPopupState>();

    private StoreSelection<TestPopupStoreState<TPayload>, bool>? open;
    private StoreSelection<TestPopupStoreState<TPayload>, bool>? mounted;
    private IDisposable? boundaryPart;
    private TestPopupPopupState state = new(false);

    [CascadingParameter]
    private TestPopupStore<TPayload>? Store { get; set; }

    [Parameter]
    public bool KeepMounted { get; set; }

    public int Renders { get; private set; }

    protected override string DefaultTagName => "div";

    protected override TestPopupPopupState State => state;

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
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is { } element)
        {
            Store!.SetPopupElement(element, RenderedId);
        }

        return Store!.AtBatchBoundaryAsync();
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        Renders++;
        if (state.Open != open!.Value)
        {
            state = new TestPopupPopupState(open.Value);
        }

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
        }

        base.Dispose(disposing);
    }

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : Store!.Popup.FloatingId;
}
