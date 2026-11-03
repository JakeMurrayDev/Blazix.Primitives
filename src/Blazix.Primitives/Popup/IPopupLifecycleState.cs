using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// ADR 05 Amendments 2026-11-03 (chosen by the user 2026-11-03): the lifecycle state is one member of a store's record, and
// PopupLifecycleStore<TState> is generic over this interface, so BLZX1010 (no derived records), ADR 05 §1's one subscription
// per part and the generated ShallowEquals stand (the IPopupStoreState<TSelf, TPayload> precedent of Amendments 2026-10-27).
/// <summary>Defines a store record that holds a popup's open lifecycle and how to replace it.</summary>
/// <typeparam name="TSelf">The type of the store record.</typeparam>
internal interface IPopupLifecycleState<TSelf>
    where TSelf : class, IStoreState<TSelf>, IPopupLifecycleState<TSelf>
{
    /// <summary>Gets the open lifecycle.</summary>
    PopupLifecycleState Lifecycle { get; }

    /// <summary>Returns a copy of <paramref name="state"/> that holds <paramref name="lifecycle"/>.</summary>
    /// <param name="state">The store record.</param>
    /// <param name="lifecycle">The open lifecycle to hold.</param>
    /// <returns>The copy.</returns>
    static abstract TSelf WithLifecycle(TSelf state, in PopupLifecycleState lifecycle);
}
