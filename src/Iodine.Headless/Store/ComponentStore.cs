using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Store;

// UTL-STA-15; ADR 16 rule 9; UTL §4.2.2; ADR 05 §1; ADR 01 decision 4 (UTL-STA-19..22 met on both render modes)
/// <summary>
/// Represents the store a component owns (Base UI's <c>ReactStore</c>): a <see cref="Store{TState}"/> plus the methods that
/// copy parameters into the store from <see cref="Microsoft.AspNetCore.Components.ComponentBase.OnParametersSet"/>.
/// </summary>
/// <typeparam name="TState">The type of the immutable state record.</typeparam>
/// <remarks>
/// Parts call the sync methods before the render that carries a new parameter. The non-reactive context bag is the
/// subclass's own typed fields and properties: element references, callbacks and timers, none of which notify. A part
/// cascades its store with <c>IsFixed = true</c> and re-renders through a <see cref="SelectorSubscription{TState}"/>.
/// </remarks>
public class ComponentStore<TState> : Store<TState>
    where TState : class, IStoreState<TState>
{
    // The mode first seen per field by SyncControlledValue, kept only while development warnings are on (UTL-EDGE-25).
    private Dictionary<object, bool>? controlledModes;

    // UTL-STA-20 in Blazor's order (fidelity correction 2026-10-28, M4 track 8): the SyncedValueWithCleanup that synced
    // each member last, so a replaced part's cleanup, which Blazor runs after its replacement synced, leaves the member alone.
    private Dictionary<object, object>? syncedOwners;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentStore{TState}"/> class that holds <paramref name="state"/>, with no
    /// listeners and no <see cref="Logger"/>.
    /// </summary>
    /// <param name="state">The initial record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public ComponentStore(TState state)
        : base(state)
    {
    }

    // UTL-EDGE-25
    /// <summary>
    /// Gets or sets the logger that receives the store's development warning.
    /// </summary>
    /// <value>The logger, or <see langword="null"/> to write nothing. The default is <see langword="null"/>.</value>
    /// <remarks>
    /// When <see cref="SyncControlledValue{TValue}"/> is called with a <c>controlled</c> flag different from the first one it saw
    /// for that field, the store logs, at <see cref="LogLevel.Error"/> and on every such call, "A component is changing the
    /// [un]controlled state of {field} to be [un]controlled. Elements should not switch from uncontrolled to controlled (or vice
    /// versa)." The warning is written only while the <c>Blazix.Primitives.DevelopmentWarnings</c> switch is on.
    /// </remarks>
    public ILogger? Logger { get; set; }

    // UTL-STA-19
    /// <summary>
    /// Copies a parameter into one member of the record when the member is not strictly equal to it (Base UI's <c>useSyncedValue</c>).
    /// </summary>
    /// <typeparam name="TValue">The type of the member.</typeparam>
    /// <param name="field">The key of the member.</param>
    /// <param name="value">The parameter's value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The member is written when it differs from <paramref name="value"/> by JavaScript's <c>!==</c>, and then only if
    /// <see cref="Store{TState}.Set{TValue}(StoreField{TState, TValue}, TValue)"/> also finds it different. Call it on every
    /// parameter set, on the store the part currently holds.
    /// </remarks>
    public void SyncValue<TValue>(StoreField<TState, TValue> field, TValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Equality.StrictEquals(field.Get(State), value))
        {
            Set(field, value);
        }
    }

    // UTL-STA-21
    /// <summary>
    /// Copies several parameters into the record with one notification (Base UI's <c>useSyncedValues</c>).
    /// </summary>
    /// <typeparam name="TValues">The type that carries the parameters, typically a value tuple.</typeparam>
    /// <param name="apply">A function from the current record and <paramref name="values"/> to the new record.</param>
    /// <param name="values">The parameters' values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="apply"/> is <see langword="null"/>.</exception>
    /// <remarks>Nothing is written when every member of the result equals the current one by <see cref="Equality.Is{T}(T, T)"/>.</remarks>
    public void SyncValues<TValues>(Func<TState, TValues, TState> apply, TValues values)
    {
        Update(apply, values);
    }

    // UTL-STA-22; UTL-EDGE-25
    /// <summary>
    /// Copies a controlled parameter into one member of the record while the parameter is supplied (Base UI's <c>useControlledProp</c>).
    /// </summary>
    /// <typeparam name="TValue">The type of the member.</typeparam>
    /// <param name="field">The key of the member.</param>
    /// <param name="controlled"><see langword="true"/> if the consumer supplied the parameter; otherwise, <see langword="false"/>.</param>
    /// <param name="value">The parameter's value, used only when <paramref name="controlled"/> is <see langword="true"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// When <paramref name="controlled"/> is <see langword="true"/> and the member differs from <paramref name="value"/> by
    /// <see cref="Equality.Is{T}(T, T)"/>, the member is written; when it is <see langword="false"/>, the member is left to the
    /// component. A <paramref name="controlled"/> flag different from the first one seen for this field logs a development
    /// warning through <see cref="Logger"/> on every such call.
    /// </remarks>
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

    /// <summary>Records <paramref name="owner"/> as the last to sync the member <paramref name="field"/> names.</summary>
    /// <param name="field">The key of the member.</param>
    /// <param name="owner">The synced value that wrote it.</param>
    internal void ClaimSynced(object field, object owner) => (syncedOwners ??= [])[field] = owner;

    /// <summary>Forgets <paramref name="owner"/> as the member's last syncer, if it still is.</summary>
    /// <param name="field">The key of the member.</param>
    /// <param name="owner">The synced value that is cleaning up.</param>
    /// <returns><see langword="true"/> when <paramref name="owner"/> synced the member last; otherwise, <see langword="false"/>.</returns>
    internal bool ReleaseSynced(object field, object owner)
    {
        if (syncedOwners is null || !syncedOwners.TryGetValue(field, out object? current) || !ReferenceEquals(current, owner))
        {
            return false;
        }

        syncedOwners.Remove(field);
        return true;
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
