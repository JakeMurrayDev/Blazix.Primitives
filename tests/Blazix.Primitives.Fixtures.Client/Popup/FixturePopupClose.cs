using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>The test-only popup close at L4: a <c>button</c> that closes the popup with the reason <c>close-press</c>.</summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class FixturePopupClose<TPayload> : PartBase<EmptyState>
{
    private readonly EventCallback onClick;

    public FixturePopupClose()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => Store!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, EventSnapshot.FromEventArgs(e))));
    }

    [CascadingParameter]
    private FixturePopupStore<TPayload>? Store { get; set; }

    protected override string DefaultTagName => "button";

    protected override EmptyState State => EmptyState.Instance;

    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("onclick", onClick);
}
