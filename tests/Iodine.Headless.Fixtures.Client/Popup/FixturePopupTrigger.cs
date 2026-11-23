using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The test-only popup trigger at L4: a <c>button</c> composing <see cref="PopupTriggerBehavior{TStore, TState, TPayload}"/>.
/// It requests its toggle from a C# <c>onclick</c> (design point 4 of M4 track 1, settled by the orchestrator 2026-10-27:
/// test-only code; the families press through the <c>interactions</c> module).
/// </summary>
/// <typeparam name="TPayload">The type of the payload the trigger carries.</typeparam>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class FixturePopupTrigger<TPayload> : PartBase<FixturePopupTriggerState>
{
    private static readonly StateAttributesMapping<FixturePopupTriggerState> s_mapping = PopupStateMapping.CreateTrigger<FixturePopupTriggerState>();

    private readonly PopupTriggerBehavior<FixturePopupStore<TPayload>, FixturePopupStoreState<TPayload>, TPayload> behavior;
    private readonly EventCallback onClick;
    private FixturePopupTriggerState state = new(false);
    private bool payloadSupplied;

    public FixturePopupTrigger()
    {
        behavior = new(this, "FixturePopupTrigger", StateHasChanged);
        onClick = CreateHandler<MouseEventArgs>("onclick", OnClickAsync);
    }

    [CascadingParameter]
    private FixturePopupStore<TPayload>? RootStore { get; set; }

    [Parameter]
    public FixturePopupHandle<TPayload>? Handle { get; set; }

    [Parameter]
    public TPayload? Payload { get; set; }

    protected override string DefaultTagName => "button";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    protected override FixturePopupTriggerState State
    {
        get
        {
            if (state.Open != behavior.IsOpenedByTrigger)
            {
                state = new FixturePopupTriggerState(behavior.IsOpenedByTrigger);
            }

            return state;
        }
    }

    protected override StateAttributesMapping<FixturePopupTriggerState>? StateAttributesMapping => s_mapping;

    protected override bool UsesGeneratedId => true;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        payloadSupplied = parameters.TryGetValue<TPayload>(nameof(Payload), out _);
        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        behavior.Apply(RootStore, Handle, Id, Element, payloadSupplied, Payload, "Blazix: `FixturePopupTrigger` must be either used within a `FixturePopupRoot` component or provided with a handle.");
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
        attributes.Set("onclick", onClick);
        behavior.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: "dialog");
    }

    // FLT-ALG-16's toggle, reduced: a press closes the popup this trigger owns, and otherwise opens it with this trigger.
    private Task OnClickAsync(MouseEventArgs e)
    {
        FixturePopupStore<TPayload> store = behavior.Store;
        bool open = !(store.IsOpen && store.Popup.IsTriggerActive(Id));
        return store.RequestOpenAsync(open, new OpenChangeEventDetails(EventReason.TriggerPress, EventSnapshot.FromEventArgs(e), new EventTrigger(Id, Element)));
    }
}
