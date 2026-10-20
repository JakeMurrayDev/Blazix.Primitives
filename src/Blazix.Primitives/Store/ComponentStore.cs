using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Store;

/// <summary>
/// A component's store (UTL §4.2.2, Base UI's <c>ReactStore</c>; ADR 05 §1): a <see cref="Store{TState}"/> plus the
/// prop-to-store sync that parts call from <c>OnParametersSet</c> (ADR 01 decision 4, which meets UTL-STA-19..22 on both
/// render modes). The non-reactive context bag (UTL-STA-15) is the subclass's own typed fields and properties: element
/// references, callbacks and timers that never notify. A part cascades its store with <c>IsFixed = true</c> (ADR 16 rule 9)
/// and re-renders through a <see cref="SelectorSubscription{TState}"/>.
/// </summary>
public class ComponentStore<TState> : Store<TState>
    where TState : class, IStoreState<TState>
{
    // The mode first seen per field by SyncControlledValue, kept only while development warnings are on (UTL-EDGE-25).
    private Dictionary<object, bool>? controlledModes;

    /// <summary>Creates a component store holding <paramref name="state"/>, with no listeners and no <see cref="Logger"/>.</summary>
    public ComponentStore(TState state)
        : base(state)
    {
    }

    /// <summary>
    /// The logger for the store's development warning: when <see cref="SyncControlledValue{TValue}"/> is called with a
    /// <c>controlled</c> flag different from the first one it saw for that field, it logs, at <c>Error</c> and on every such
    /// call, "A component is changing the [un]controlled state of {field} to be [un]controlled. Elements should not switch
    /// from uncontrolled to controlled (or vice versa)." Written only while the <c>Blazix.Primitives.DevelopmentWarnings</c>
    /// AppContext switch is on; <c>null</c> writes nothing (UTL-EDGE-25).
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// <c>useSyncedValue</c> (UTL-STA-19): writes <paramref name="value"/> when the member is not strictly equal to it
    /// (JavaScript <c>!==</c>, then <see cref="Set{TValue}"/>'s <c>Object.is</c>). Call it on every parameter set, on the
    /// store the part currently holds.
    /// </summary>
    public void SyncValue<TValue>(StoreField<TState, TValue> field, TValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Equality.StrictEquals(field.Get(State), value))
        {
            Set(field, value);
        }
    }

    /// <summary>
    /// <c>useSyncedValues</c> (UTL-STA-21): writes <c>apply(State, values)</c> with one notification, and nothing when every
    /// member already equals the result's (<see cref="Store{TState}.Update{TArg}(Func{TState, TArg, TState}, TArg)"/>).
    /// <typeparamref name="TValues"/> is typically a value tuple of the synced parameters.
    /// </summary>
    public void SyncValues<TValues>(Func<TState, TValues, TState> apply, TValues values)
    {
        Update(apply, values);
    }

    /// <summary>
    /// <c>useControlledProp</c> (UTL-STA-22): when <paramref name="controlled"/> is <c>true</c> and the member differs from
    /// <paramref name="value"/> by <see cref="Equality.Is{T}(T, T)"/>, writes it; when <c>false</c> the member is left to the
    /// component. A change of <paramref name="controlled"/> from its first value for this field logs UTL-EDGE-25 on every call.
    /// </summary>
    public void SyncControlledValue<TValue>(StoreField<TState, TValue> field, bool controlled, TValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (Logger is not null && DevelopmentWarnings.IsEnabled)
        {
            WarnOnModeSwitch(field, controlled);
        }

        if (controlled && !Equality.Is(field.Get(State), value))
        {
            SetState(field.With(State, value));
        }
    }

    private void WarnOnModeSwitch<TValue>(StoreField<TState, TValue> field, bool controlled)
    {
        controlledModes ??= new Dictionary<object, bool>(ReferenceEqualityComparer.Instance);
        if (!controlledModes.TryGetValue(field, out bool wasControlled))
        {
            controlledModes.Add(field, controlled);
            return;
        }

        if (wasControlled != controlled)
        {
            DevelopmentWarnings.Write(
                Logger!,
                $"A component is changing the {(wasControlled ? "" : "un")}controlled state of {field.Name} to be {(wasControlled ? "un" : "")}controlled. Elements should not switch from uncontrolled to controlled (or vice versa).");
        }
    }
}
