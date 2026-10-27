using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of the test-only trigger: whether the popup is open and owned by it.</summary>
[PartState]
internal sealed partial record TestPopupTriggerState(bool Open);

/// <summary>
/// A test-only popup trigger: a <c>button</c> that composes <see cref="PopupTriggerBehavior{TStore, TState, TPayload}"/>.
/// It requests its toggle from a C# <c>onclick</c> (design point 4 of M4 track 1, settled by the orchestrator 2026-10-27:
/// test-only code, never a part's; the families press through the <c>interactions</c> module's <c>press</c>).
/// </summary>
[CascadingTypeParameter(nameof(TPayload))]
internal sealed class TestPopupTrigger<TPayload> : PartBase<TestPopupTriggerState>
{
    private static readonly StateAttributesMapping<TestPopupTriggerState> s_mapping = PopupStateMapping.CreateTrigger<TestPopupTriggerState>();

    private readonly PopupTriggerBehavior<TestPopupStore<TPayload>, TestPopupStoreState<TPayload>, TPayload> behavior;
    private readonly EventCallback onClick;
    private TestPopupTriggerState state = new(false);
    private bool payloadSupplied;

    public TestPopupTrigger()
    {
        behavior = new(this, StateHasChanged);
        onClick = CreateHandler<MouseEventArgs>("onclick", OnClickAsync);
    }

    [CascadingParameter]
    private TestPopupStore<TPayload>? RootStore { get; set; }

    [Parameter]
    public TestPopupHandle<TPayload>? Handle { get; set; }

    [Parameter]
    public TPayload? Payload { get; set; }

    public PopupTriggerBehavior<TestPopupStore<TPayload>, TestPopupStoreState<TPayload>, TPayload> Behavior => behavior;

    public string? TriggerId => Id;

    public int Renders { get; private set; }

    protected override string DefaultTagName => "button";

    protected override TestPopupTriggerState State => state;

    protected override StateAttributesMapping<TestPopupTriggerState>? StateAttributesMapping => s_mapping;

    protected override bool UsesGeneratedId => true;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        payloadSupplied = parameters.TryGetValue<TPayload>(nameof(Payload), out _);
        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        behavior.Apply(RootStore, Handle, Id, Element, payloadSupplied, Payload, "Blazix: `TestPopupTrigger` must be either used within a `TestPopupRoot` component or provided with a handle.");
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => behavior.OnAfterRenderAsync(Element);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        Renders++;
        if (state.Open != behavior.IsOpenedByTrigger)
        {
            state = new TestPopupTriggerState(behavior.IsOpenedByTrigger);
        }

        attributes.Set("onclick", onClick);
        behavior.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: "dialog");
    }

    // FLT-ALG-16's toggle, reduced: a press closes the popup this trigger owns, and otherwise opens it with this trigger.
    private Task OnClickAsync(MouseEventArgs e)
    {
        TestPopupStore<TPayload> store = behavior.Store;
        bool open = !(store.IsOpen && store.Popup.IsTriggerActive(Id));
        return store.RequestOpenAsync(open, new OpenChangeEventDetails(EventReason.TriggerPress, EventSnapshot.FromEventArgs(e), new EventTrigger(Id, Element)));
    }
}
