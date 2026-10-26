# src/Blazix.Primitives/Store

The observable state container and its reactive reads. Namespace `Blazix.Primitives.Store` (ADR 08 §1): a library-author surface.

## Inputs

- `docs/architecture/shared-utilities.md` (UTL) §4.2 (UTL-STA-07..27), UTL-ALG-03, §8.2, UTL-EDGE-25.
- Decision records 01 decision 4 (prop-to-store sync in `OnParametersSet`), 05 §1, 16 rules 7 and 9.

## Job

The public surface, with what each member does (requirement IDs follow for traceability):

- `Equality.Is<T>(T left, T right) → bool`: JavaScript's `Object.is` decided by the static type, with no boxing. Value types compare with `EqualityComparer<T>.Default`, except that `double`, `float`, `Half` and their nullable forms treat every `NaN` as equal to every `NaN` and `0` as different from `-0`. `string` compares by ordinal value. Every other reference type compares by reference, records included, never structurally. A value typed `object` or an interface compares by reference even when it holds a boxed value.
- Internal beside it: `Equality.StrictEquals<T>` (JavaScript's `===`: `NaN` never equal, `0` equal to `-0`) and `Equality.SameValueZero<T>(T left, T right) → bool` (`Array.prototype.includes`' comparison: `Is<T>` with `0` equal to `-0` for `double`, `float`, `Half` and their nullables, `NaN` still equal to `NaN`; allocation-free for R8's primitives). Accordion's membership test uses `SameValueZero` and its single-mode check and removal use `StrictEquals` (ACC-STA-04..06, ACC-EDGE-10; ADR 05 note dated 2026-10-26). Never `Contains` or LINQ for these: they box on the interpreter and compare records structurally.
- `interface IStoreState<TSelf>`, implemented by every state record (a `sealed record` class), with two static members:
  - `static TSelf Copy(TSelf state)`: returns `state with { }`.
  - `static bool ShallowEquals(TSelf left, TSelf right)`: `true` when every member is equal by `Equality.Is`.

  `NotifyAll` must give the record a new reference whose members are all `Object.is`-equal (UTL-STA-12), and a write whose every member is `Object.is`-equal must be a no-op (UTL-STA-10/11). These two members are exactly what those rules need, and the no-reflection rule (ADR 16 rule 1) leaves a generic store no other way to copy or compare a record. Nobody writes them by hand: see `[StoreState]`.
- `StoreStateAttribute`, `[StoreState]`: marks a state record, declared `[StoreState] public sealed partial record XState(…) : IStoreState<XState>;`. The `[StoreState]` generator (`src/Blazix.Primitives.Generators`) writes both members of `IStoreState` into it. `Copy` is `state with { }`. `ShallowEquals` compares every instance member that `with` copies, in declaration order, stopping at the first difference: positional, auto-implemented, get-only and init-only properties through the property, whatever their accessibility, and fields directly. A computed property, which holds no value of its own, and static members take no part. Each comparison follows `Equality.Is` for the member's static type but is written out for that type rather than calling the generic `Is<T>`, so that it never boxes (see the Generators contract for the table). A member holding a record or a store therefore compares by reference, and an `ElementReference` by its id and its renderer context. The record must be sealed, partial (with its containing types), implement `IStoreState` of itself, derive from no other record and declare neither member itself (BLZX1005..1010). A struct member with neither `==` nor `IEquatable<T>` falls back to `Equality.Is` with warning BLZX1011.
- `StoreField<TState, TValue>(string name, Func<TState, TValue> get, Func<TState, TValue, TState> with)`: a typed key for one member, declared `static readonly` per member. `Get(state)` reads the member; `With(state, value)` returns a copy with the member replaced; `Name` names it in warnings.
- `Store<TState>(TState state)`, where `TState : class, IStoreState<TState>`:
  - `State`: the current record. Replaced on every write, never mutated (UTL-STA-07).
  - `Subscribe(Action<TState> listener) → IDisposable`: adds the listener to an insertion-ordered set. Registering the same delegate instance twice has no further effect. Disposing the result removes the listener, even during a notification pass (UTL-STA-13).
  - `SetState(TState next)`: does nothing if `next` is the current reference. Otherwise it replaces the record and calls each listener in order with it. If a listener writes during the pass, that nested write notifies everyone with the newer record and the outer pass stops (UTL-STA-08/09, UTL-ALG-03). The list is iterated live: a listener removed mid-pass is skipped, and one added mid-pass is reached.
  - `Update(Func<TState, TState> change)` and `Update<TArg>(Func<TState, TArg, TState> change, TArg arg)`: write `change(State[, arg])` with one notification, or nothing when `ShallowEquals(State, result)` (UTL-STA-11).
  - `Set<TValue>(StoreField<TState, TValue> field, TValue value)`: does nothing when `Equality.Is(field.Get(State), value)`; otherwise it writes `field.With(State, value)` (UTL-STA-10).
  - `NotifyAll()`: writes `TState.Copy(State)`, which notifies with a new reference and the same values (UTL-STA-12).
  - `Select(Func<TState, TValue>)` and `Select(Func<TState, TArg, TValue>, TArg)`: evaluate a named selector against `State` without subscribing (UTL-STA-17). A named selector is a `static readonly Func<…>` field.
  - `Observe(Func<TState, TValue> selector, Action<TValue, TValue, Store<TState>> listener) → IDisposable`: calls `listener(current, current, store)` at once. After each notification it calls `listener(new, previous, store)` only when the selection changed by `Equality.Is` (UTL-STA-24).

  The store does not marshal across threads. Write to it on the renderer's synchronization context.
- `ComponentStore<TState>(TState state) : Store<TState>`: the store a component owns. Its subclass's own fields and properties are the non-reactive context bag: element references, callbacks and timers that never notify (UTL-STA-15). Parts call its sync methods from `OnParametersSet`:
  - `SyncValue<TValue>(StoreField<TState, TValue> field, TValue value)`: writes `value` when the member is not strictly equal to it, using JavaScript `!==`, then `Set` (UTL-STA-19).
  - `SyncValues<TValues>(Func<TState, TValues, TState> apply, TValues values)`: `Update(apply, values)`, i.e. one notification or none. `TValues` is typically a value tuple of the synced parameters (UTL-STA-21).
  - `SyncControlledValue<TValue>(StoreField<TState, TValue> field, bool controlled, TValue value)`: when `controlled` is `true` and the member differs from `value` by `Equality.Is`, writes it. When `controlled` is `false`, the member is left alone. A `controlled` flag that differs from the first one seen for that field logs a warning through `Logger` on every such call (UTL-STA-22, UTL-EDGE-25).
  - `Logger`: an `ILogger?` for that warning, written only while the `Blazix.Primitives.DevelopmentWarnings` switch is on.
- `SyncedValueWithCleanup<TState, TValue>(StoreField<TState, TValue?> field)`, one per call site: `Sync(ComponentStore<TState> store, TValue? value)` writes as `SyncValue` does. When the store or the value changed since the previous call, it first sets the member to `default` in the previous store. `Dispose()` sets the member to `default` in the last store (UTL-STA-20).
- `SelectorSubscription<TState>(Store<TState> store, Action onSelectionChanged) : IDisposable`: one store subscription per owner, so a part passes `StateHasChanged`.
  - `Select(Func<TState, TValue>) → StoreSelection<TState, TValue>` and `Select(Func<TState, TArg, TValue>, TArg) → StoreSelection<TState, TArg, TValue>` add selections. Each one's `Value` holds the selector's result for the latest record.
  - On every notification all selections are re-evaluated, and `onSelectionChanged` runs once if any changed by `Equality.Is`.
  - Setting `StoreSelection<…>.Arg` to a different argument re-evaluates `Value` at once without calling back.
  - `Dispose()` unsubscribes.
  - A notification in which no selection changed allocates nothing (UTL-STA-25..27).

  A part cascades its store with `IsFixed = true`, which BLZX2001 checks.
- Not ported: `createSelector` and `createSelectorMemoized` (UTL-STA-29..33, UTL-ALG-04), which nothing in Base UI 1.9.0 uses, and `StoreInspector` (UTL-STA-28). `NullStore` and the popup store come in M4 (PSH).

## Outputs

- The stores of the popup families and the floating layer (UTL §4.2 "Used by").
- Tested at L1 by `tests/Blazix.Primitives.Tests/Store/`, including the notification budget (zero bytes for N subscribers whose selections did not change) and a generated `ShallowEquals` over a record with every kind of member (zero bytes on CoreCLR; the WebAssembly interpreter cost is pinned by Track 3's harness test).

## Human check

- Every state record is marked `[StoreState]` and declares neither `Copy` nor `ShallowEquals` (BLZX1009 rejects a hand-written one), so no comparison can miss a member.
- No store field is compared structurally; a record member is compared by reference.
- The notification budget stays at zero (ADR 16 rule 7: budgets only tighten).
