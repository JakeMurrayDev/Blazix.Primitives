using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// combobox/input-group/ComboboxInputGroup.tsx (CMB §3.3, §5.3): role="group", the pressable trigger mapping plus
// data-popup-side, data-list-empty, data-placeholder and the field validity (combobox/utils/stateAttributesMapping.ts). Its
// `combobox` handle prevents the mousedown default outside the input (CMB-PTR-08) and its id joins the dismissal's exemption
// list (CMB-DIS-01).
/// <summary>The behavior of a combobox-family input group: its state, its id and its press default.</summary>
internal sealed class ComboboxInputGroupBehavior : IAsyncDisposable
{
    /// <summary>Initializes a new instance of the <see cref="ComboboxInputGroupBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxInputGroupBehavior(ComboboxPartServices services) => throw new NotImplementedException();

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => throw new NotImplementedException();

    /// <summary>Gets the field's state the group projects.</summary>
    public FieldStateSnapshot Field => throw new NotImplementedException();

    /// <summary>Gets a task that completes once the group's handle has attached.</summary>
    public Task TwinAttached => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new NotImplementedException();

    /// <summary>Reads the root and the field and subscribes the part.</summary>
    /// <param name="store">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="field">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? store, FieldRootContext? field, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe) =>
        throw new NotImplementedException();

    /// <summary>Applies the part's rendered id and the consumer's attributes.</summary>
    /// <param name="id">The id the part renders.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(string? id, IReadOnlyDictionary<string, object>? consumerAttributes) => throw new NotImplementedException();

    /// <summary>Adds the group's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes) => throw new NotImplementedException();

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => throw new NotImplementedException();

    /// <summary>Attaches the group's handle.</summary>
    /// <param name="element">The group's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public Task AfterRenderAsync(ElementReference? element) => throw new NotImplementedException();
}
