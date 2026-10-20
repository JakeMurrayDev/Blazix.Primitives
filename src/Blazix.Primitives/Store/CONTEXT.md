# src/Blazix.Primitives/Store

The observable state container and its reactive reads. Namespace `Blazix.Primitives.Store` (ADR 08 §1): a library-author surface.

## Inputs

- `docs/architecture/shared-utilities.md` (UTL) §4.2 (UTL-STA-07..27), UTL-ALG-03, §8.2, UTL-EDGE-25.
- Decision records 01 decision 4 (prop-to-store sync in `OnParametersSet`), 05 §1, 16 rules 7 and 9.

## Job

- `Store<TState>`: one immutable record, an insertion-ordered listener set iterated live, the UTL-ALG-03 recursion guard, `SetState`, `Update`, `Set` through a `StoreField<TState, TValue>`, `NotifyAll`, `Select`, `Observe`. Writes happen on the renderer's synchronization context; the store does not marshal.
- `IStoreState<TSelf>`: the state record supplies `Copy` (for `NotifyAll`) and `ShallowEquals` (member-wise `Equality.Is`, for `Update`'s no-op skip), because a generic store cannot copy or compare a record without reflection.
- `Equality.Is<T>`: `Object.is` by static type with no boxing (records by reference, `NaN` equal, `-0` distinct).
- `ComponentStore<TState>`: the prop-to-store sync (`SyncValue`, `SyncValues`, `SyncControlledValue`) parts call from `OnParametersSet`; its subclass's own fields are the non-reactive context bag. `SyncedValueWithCleanup` is the per-site form of `useSyncedValueWithCleanup`.
- `SelectorSubscription<TState>`: one store subscription per part, every selection re-evaluated per notification, one callback when any changed. A part cascades its store with `IsFixed = true` (checked by BLZX2001).
- Not ported: `createSelector` / `createSelectorMemoized` (UTL-STA-29..33, UTL-ALG-04; no consumer at Base UI 1.9.0, deferred under UTL §1), `StoreInspector` (UTL-STA-28). `NullStore` and the popup store are M4 (PSH).

## Outputs

- The stores of the popup families and the floating layer (UTL §4.2 "Used by").
- Tested at L1 by `tests/Blazix.Primitives.Tests/Store/`, including the notification budget (zero bytes for N subscribers whose selections did not change).

## Human check

- Every `IStoreState.ShallowEquals` names every member of its record.
- No store field is compared structurally; a record member is compared by reference.
- The notification budget stays at zero (ADR 16 rule 7: budgets only tighten).
