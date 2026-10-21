# Shared utilities

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/utils/src/**` (all files except those owned elsewhere, see §1), `packages/react/src/internals/{filter,itemEquality,resolveValueLabel,resolvePopupLabel,RequestQueue,TimeoutManager,useInitialLiveRegionTextMutation,useValueChanged,constants,noop}.ts(x)`, `packages/react/src/utils/{getCssDimensions,getElementTransform,getPseudoElementBounds,getElementAtPoint,scrollEdges,scrollable,valueToPercent,NullStore}.ts`, `packages/react/src/direction-provider/`, `packages/react/src/internals/direction-context/`, `packages/react/src/csp-provider/`, `packages/react/src/internals/csp-context/`, `packages/react/src/unstable-use-media-query/` |
| Tests | `packages/utils/src/*.test.ts(x)`, `packages/utils/src/store/*.test.ts(x)`, `internals/{filter,itemEquality,resolveValueLabel,resolvePopupLabel,RequestQueue,TimeoutManager,useInitialLiveRegionTextMutation,useValueChanged}.test.ts(x)`, `utils/{getCssDimensions,getPseudoElementBounds,scrollEdges}.test.ts`, `direction-provider/DirectionProvider.test.tsx`, `csp-provider/CSPProvider.test.tsx`, `unstable-use-media-query/index.test.tsx` |
| Prefix | `UTL` |
| Status | approved |
| Revised | 2026-10-21 |
| Used by | Every component document. Specifically: controlled state (§4.1) — ACC, CHK, CKG, COL, CMB, DRW, MNU, NAV, NUM, RDG, SEL, SLD, SWT, TAB, TGL, TGG; store (§4.2) — CMB, AUT, DLG, ADL, DRW, MNU, CTX, MBR, POP, PRV, SEL, TST, TIP, NAV, plus FLT/PSH; scroll lock (§4.8) — DLG, ADL, DRW and every anchored popup through PSH `useAnchoredPopupScrollLock`; number formatting (§4.15) — NUM, SLD, MTR, PRG; locale filter (§4.15, §5) — CMB, AUT, MNU (filter parts); item equality and label resolution (§5) — CMB, AUT, SEL; live-region marker (§4.13) — CMB, AUT; direction (§4.15) — CMB, CMP, FLT, MNU, NAV, OTP, SCR, SEL, SLD, PSH; CSP (§4.12) — SCR, SEL, TRN; platform detection (§4.16) — CMB, DRW, FLT, MNU, NUM, OTP, SCR, SEL, PSH |

## 1. Purpose

Base UI's components are thin behavioral layers over a small set of shared primitives: a controlled/uncontrolled value contract, an observable state store with selectors, timer wrappers, a document scroll lock, DOM helpers that understand shadow DOM, platform sniffing used to branch on browser quirks, locale-aware text filtering and number formatting, and two tiny public providers (`DirectionProvider`, `CSPProvider`). None of these are large, but every one of them encodes decisions that are observable from the outside — which warnings fire, in what order subscribers are notified, which inline styles a scroll lock writes, how a query matches an accented string. A port that reimplements the components without reproducing these primitives will diverge in exactly the places the Base UI test suite checks.

This document is a catalogue. Each mechanism gets a short subsection under §4 (grouped by AREA code) with the requirements that must hold, and §5 transcribes the algorithms that must be reproduced exactly. Trivial helpers are kept to one line.

The following files live in the same directories but are owned by other documents and are **not** documented here: `useMergedRefs`, `getReactElementRef`, `useId` → [rendering-and-composition.md](rendering-and-composition.md) (RND); `getDefaultFormSubmitter`, `serializeValue` → [field-and-form-infrastructure.md](field-and-form-infrastructure.md) (FFI); `useEnhancedClickHandler` → [button-behavior.md](button-behavior.md) (UBT); `hydration.ts`, `useIsHydrating`, `PrehydrationScript` → [transitions-and-mounting.md](transitions-and-mounting.md) (TRN); `useRenderElement`, `stateAttributesMapping`, `getStateAttributesProps`, `resolveRenderedId`, `styles.tsx` (documented here only for its CSP interaction) → RND; `internals/composite` → [composite-and-list-navigation.md](composite-and-list-navigation.md) (CMP); `floating-ui-react` → [floating-and-positioning.md](floating-and-positioning.md) (FLT); `utils/popups`, `useAnchoredPopupScrollLock` → [popup-store-and-handles.md](popup-store-and-handles.md) (PSH); `createBaseUIEventDetails`, `reasons`, `reason-parts` → [event-details-and-reasons.md](event-details-and-reasons.md) (EVD).

Several utilities exist in the tree but have **no consumer** among the exported components at v1.9.0: `RequestQueue`, `TimeoutManager`, `createSelector`, `createSelectorMemoized`, `useIdleCallback`, `formatErrorMessage`, `fastObjectShallowCompare`, `createLogOnce` (only through `warn`/`error`), and the deprecated `packages/utils/src/isMouseWithinBounds.ts`. They are documented because `@base-ui/utils` is a published package (0.5.0) and because the brief asks for them, but a port may defer them until a component needs them.

## 2. Concepts and terminology

| Base UI / React name | Neutral term used below | Meaning |
|---|---|---|
| controlled / uncontrolled | consumer-owned / component-owned value | Whether the consumer supplies the current value (`value`, `open`, …) or only an initial one (`defaultValue`, `defaultOpen`, …). The mode is fixed at first render. |
| `useControlled` | controlled-state contract | The helper that implements the mode decision, the fallback rules and the dev warnings for every value-bearing component. |
| `Store` | observable state container | An object holding one immutable `state` record plus a set of listeners; writes replace the record and notify listeners synchronously. |
| `ReactStore` | component store | A `Store` plus a non-reactive `context` bag, named selectors, and helpers that sync component parameters into the store after each render (post-commit, pre-paint). |
| selector | state projection | A pure function `(state, a1?, a2?, a3?) → value`. "Memoized selector" caches the last result per state record. |
| `useStore` / `store.useState` | reactive read | A read that re-renders the reading component when the projected value changes (by `Object.is`). |
| `store.state` / `store.select` | non-reactive read | A synchronous read of the current record; never triggers a re-render. |
| `context` (store) | store context | Mutable bag of non-reactive values (DOM handles, callbacks) owned by the store. Not to be confused with React context. |
| hook (`useTimeout`, …) | per-instance scheduler | A timer object created once per component instance and cancelled automatically when the instance is removed. |
| layout effect / `useIsoLayoutEffect` | post-commit, pre-paint | Work that runs synchronously after the DOM has been updated for a render but before the browser paints. On a server render it does not run at all. |
| effect / `useOnMount` | after paint (mount) | Work that runs once after the component's first paint; its returned function runs when the component is removed. |
| `useStableCallback` | stable trampoline | A function whose identity never changes but which always calls the most recently supplied implementation; must not be invoked during render. |
| scroll lock | document scroll lock | Preventing the page (the viewport scroller) from scrolling while a modal surface is open, without layout shift. |
| viewport scroller | viewport scroller | Whichever of `<html>` or `<body>` establishes the page's scroll container (see UTL-DOM-04). |
| overlay / inset scrollbars | overlay / inset scrollbars | Overlay scrollbars take no layout space (macOS default, iOS); inset ("classic") scrollbars consume width and must be compensated for when hidden. |
| composed tree | composed tree | The DOM as seen across shadow roots and slots. "Composed-tree walk" means stepping `assignedSlot → parentNode → shadow host`. |
| `platform.os/engine/env/screenReader/mediaQuery` | platform flags | Booleans computed once at load from `navigator`, used to branch on OS or rendering-engine quirks. |
| `Intl.Collator` filter | locale filter | Substring / prefix / suffix matching that ignores case, diacritics and punctuation per the locale's collation. |
| `DirectionProvider` / `useDirection` | reading direction | `'ltr'` or `'rtl'`, supplied by an ancestor provider; default `'ltr'`. |
| `CSPProvider` | CSP configuration | A `nonce` for inline `<style>`/`<script>` elements the library emits, and a switch to suppress the inline `<style>` elements entirely. |
| dev warning | development-only diagnostic | A message written to the console only in non-production builds; most are deduplicated per unique text. |

## 3. Public surface

Three items are public from `@base-ui/react`; everything else in this document is internal to the library (though published separately as `@base-ui/utils` 0.5.0, whose `package.json` exposes `./store`, `./platform` and every top-level file as a subpath).

### 3.1 `DirectionProvider`, `useDirection`, `TextDirection`

Exported from `@base-ui/react/direction-provider` and from the package root. Renders no element of its own.

| Prop | Type | Default | Description |
|---|---|---|---|
| `direction` | `TextDirection` | `'ltr'` | The reading direction of the text. |
| `children` | `ReactNode` | — | Subtree that reads the direction. |

`useDirection(): TextDirection` — returns the nearest provider's `direction`, or `'ltr'` when there is no provider.

`TextDirection = 'ltr' | 'rtl'`.

Types in the outline: `DirectionProvider.Props` (alias `DirectionProviderProps`), `DirectionProvider.State` (empty object), `TextDirection`. Docs: `docs/src/app/(docs)/react/utils/direction-provider/page.mdx`.

### 3.2 `CSPProvider`

Exported from `@base-ui/react/csp-provider` and from the package root. Renders no element of its own.

| Prop | Type | Default | Description |
|---|---|---|---|
| `nonce` | `string` | — | The nonce value to apply to inline `<style>` and `<script>` tags. |
| `disableStyleElements` | `boolean` | `false` | Whether inline `<style>` elements created by Base UI components should not be rendered. Instead, components must specify the CSS styles via custom class names or other methods. |
| `children` | `ReactNode` | — | Subtree that reads the configuration. |

Types in the outline: `CSPProvider.Props` (alias `CSPProviderProps`), `CSPProvider.State` (empty object). Docs: `docs/src/app/(docs)/react/utils/csp-provider/page.mdx`.

### 3.3 `useMediaQuery(query, options)` (unstable)

Exported only from the subpath `@base-ui/react/unstable-use-media-query` (not re-exported from the package root, not listed in the docs outline). Returns `boolean`.

| Option | Type | Default | Description |
|---|---|---|---|
| `defaultMatches` | `boolean` | `false` | Value returned during server rendering / first mount when `matchMedia` is unavailable. |
| `matchMedia` | `typeof window.matchMedia` | `window.matchMedia` when defined, else `null` | Custom implementation (for example an iframe's window). Passing `null` disables live matching. |
| `noSsr` | `boolean` | `false` | When `true` and `matchMedia` exists, the server/hydration snapshot is the live result instead of `defaultMatches` (skips the double render). |
| `ssrMatchMedia` | `(query) => { matches: boolean }` | — | Server-side implementation used for the server snapshot. |

Types: `useMediaQuery.Options` (alias `UseMediaQueryOptions`), `useMediaQuery.State` (empty).

### 3.4 Indirectly public pieces

* The locale filter `getFilter` (§4.15, UTL-ALG-05) is re-exported as `Menu.useFilter` (type `GetFilterParameters`), and wrapped by `Combobox.useFilter` / `Autocomplete.useFilter` (types `ComboboxFilter`/`AutocompleteFilter`, `UseFilterOptions`). The public wrappers belong to [menu.md](../components/menu.md), [combobox.md](../components/combobox.md) and [autocomplete.md](../components/autocomplete.md); this document specifies the core matching they delegate to.
* `StoreInspector` (§4.2.4) is exported from `@base-ui/utils/store` as a development aid.

### 3.5 Public props whose semantics are defined here

These props belong to individual components, but their behavior is specified once in this document so component docs cite the IDs below instead of re-describing them.

| Public prop(s) | Components | Semantics defined by |
|---|---|---|
| `value`/`defaultValue`, `open`/`defaultOpen`, `checked`/`defaultChecked`, `inputValue`/`defaultInputValue`, `pressed`/`defaultPressed`, `expanded`/`defaultExpanded`, `activeIndex`/`defaultActiveIndex` and every other `x`/`defaultX` pair | all value-bearing components | UTL-STA-01 … UTL-STA-06 (mode decision, fallback, setter), UTL-EDGE-22 … UTL-EDGE-24 (diagnostics); store-backed roots additionally UTL-STA-22, UTL-EDGE-25 |
| `locale`, `format` | NumberField, Slider, Meter, Progress | UTL-I18N-05 … UTL-I18N-07 (`Intl.NumberFormat` resolution and caching) |
| `useFilter({ locale, ...Intl.CollatorOptions })` and the `Filter` object (`contains`/`startsWith`/`endsWith`) | Combobox, Autocomplete, Menu | UTL-I18N-08 … UTL-I18N-13, UTL-ALG-05 |
| `isItemEqualToValue` / item-equality comparers and `items` (flat, grouped, record) | Combobox, Autocomplete, Select | UTL-ALG-06 (equality, selection index), UTL-ALG-07 (label resolution), UTL-I18N-13 |
| `direction` | DirectionProvider (read by Composite, positioning, Slider, Scroll Area, …) | UTL-I18N-01 … UTL-I18N-04 |
| `nonce`, `disableStyleElements` | CSPProvider (affects Scroll Area, Select, pre-hydration script) | UTL-DOM-32 … UTL-DOM-36 |
| `modal` (scroll-lock aspect only) | Dialog, Alert Dialog, Drawer, anchored popups via PSH | UTL-DOM-01 … UTL-DOM-12, UTL-ALG-01/02 |

## 4. Behavioral requirements

Source paths are relative to `reference/base-ui/packages/react/src/` unless they begin with `packages/utils/`. Test citations name the test file and title.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- UTL-STA-19 — met on both modes: prop-to-store sync runs in `OnParametersSet`, before the render that carries the new parameter (ADR 05 §1).
- UTL-STA-20 — met on both modes, as UTL-STA-19.
- UTL-STA-21 — met on both modes, as UTL-STA-19.
- UTL-STA-22 — met on both modes, as UTL-STA-19.

### 4.1 STA — Controlled-state contract (`useControlled`)

`useControlled({ controlled, default, name, state = 'value' })` returns `[value, setValue]`. Every component with a `value`/`defaultValue`, `open`/`defaultOpen`, `checked`/`defaultChecked` (and similar) pair implements that pair through this contract; component documents reference these requirements instead of restating them. The dev warnings are quoted in §7 (UTL-EDGE-22 … UTL-EDGE-24); [event-details-and-reasons.md](event-details-and-reasons.md) links here for them. The component-facing controlled/uncontrolled contract (what consumers observe, including callback timing and cancelation) is owned by EVD-STA-01..10; the requirements below specify only the helper that implements it and map onto those IDs as follows: UTL-STA-01 ↔ EVD-STA-01; UTL-STA-02 ↔ EVD-STA-02/03; UTL-STA-03 ↔ EVD-STA-05; UTL-STA-04 ↔ EVD-STA-04 and EVD-EVT-06. Where the two differ in wording, EVD governs observable component behavior.

- **UTL-STA-01** — MUST. The mode is decided once, at first render: the value is consumer-owned ("controlled") if and only if `controlled !== undefined` at that time, and the mode never changes for the lifetime of the component instance.
  Source: `packages/utils/src/useControlled.ts:41`. Test: [L1] `useControlled.test.tsx` "works correctly when is controlled", "warns when switching from uncontrolled to controlled".
- **UTL-STA-02** — MUST. In component-owned mode the initial value is `default` exactly as supplied; a function supplied as `default` is stored as the value, not invoked as an initializer.
  Source: `packages/utils/src/useControlled.ts:43`. Test: [L1] `useControlled.test.tsx` "stores a function default value instead of calling it".
- **UTL-STA-03** — MUST. In consumer-owned mode the returned value is `controlled` on every render while it is defined; if `controlled` later becomes `undefined`, the returned value falls back to the initial `default` captured at first render (not to any value set through the setter).
  Source: `packages/utils/src/useControlled.ts:46`. Test: [L1] `useControlled.test.tsx` "warns and falls back to the default when switching from controlled to uncontrolled".
- **UTL-STA-04** — MUST. The returned setter updates the component-owned value only in component-owned mode; in consumer-owned mode it is a no-op (including after `controlled` has become `undefined`).
  Source: `packages/utils/src/useControlled.ts:83-90`. Test: [L1] `useControlled.test.tsx` "works correctly when is not controlled", "warns and falls back to the default when switching from controlled to uncontrolled" (setter call to `'next'` leaves the value at `'default'`).
- **UTL-STA-05** — MUST. Changing `default` after the first render never changes the component-owned value (it is read once); it only triggers the diagnostic in UTL-EDGE-23.
  Source: `packages/utils/src/useControlled.ts:43,66-80`. Test: none (the "warns when changed after initial rendering" test asserts only the diagnostic).
- **UTL-STA-06** — MUST. The setter identity is stable for the lifetime of the component (safe to capture once).
  Source: `packages/utils/src/useControlled.ts:83-90`. Test: none.

### 4.2 STA — Observable state container (`Store`, `ReactStore`, `useStore`)

Every complex component (dialogs, menus, combobox, select, toast, tooltip, popover, preview card, navigation menu, plus the floating-ui layer) keeps its state in a `ReactStore`. A port needs an equivalent with the same write semantics, notification order and equality rules; component documents describe their state records in terms of `set`/`update`/`select`/`useState` on such a store.

#### 4.2.1 `Store`

- **UTL-STA-07** — MUST. A store holds exactly one state record, readable synchronously through `state` (and `getSnapshot()`); the record is replaced, never mutated in place, and the new record is visible synchronously to any reader immediately after a write returns.
  Source: `packages/utils/src/store/Store.ts:27,56-58,65-70`. Test: [L1] `Store.test.ts` "notifies subscribers with the new state" (`store.state.value` is `1` right after `setState`).
- **UTL-STA-08** — MUST. `setState(next)`: if `next` is the same reference as the current record, nothing happens (no notification); otherwise the record is replaced and every subscribed listener is invoked synchronously, in subscription order, with the new record as its only argument.
  Source: `packages/utils/src/store/Store.ts:65-82`. Test: [L1] `Store.test.ts` "notifies subscribers with the new state", "does not notify when setState receives the current state reference".
- **UTL-STA-09** — MUST. If a listener performs a nested write during a notification pass, the nested write runs its own full notification pass with the newest record, and the outer pass stops immediately so that listeners not yet reached do not receive the stale record. (Transcribed as UTL-ALG-03.)
  Source: `packages/utils/src/store/Store.ts:71-81`. Test: [L1] `Store.test.ts` "a nested setState from a listener stops the outer notification pass".
- **UTL-STA-10** — MUST. `set(key, value)`: if `Object.is(state[key], value)` nothing happens; otherwise a new record is produced as a shallow copy with `key` replaced, and `setState` runs.
  Source: `packages/utils/src/store/Store.ts:106-110`. Test: [L1] `Store.test.ts` "set() writes a single key and skips same-value writes".
- **UTL-STA-11** — MUST. `update(changes)`: if every key in `changes` is `Object.is`-equal to the current value, nothing happens; if at least one differs, a single new record is produced by shallow-merging **all** of `changes` over the current record (one notification, not one per key).
  Source: `packages/utils/src/store/Store.ts:91-98`. Test: [L1] `Store.test.ts` "update() merges changed keys and skips no-op updates".
- **UTL-STA-12** — MUST. `notifyAll()` replaces the record with a shallow copy holding the same values (new reference, `Object.is`-equal members) and notifies all listeners; it is the mechanism by which a store re-broadcasts when a nested store it holds changes.
  Source: `packages/utils/src/store/Store.ts:115-118`. Test: [L1] `Store.test.ts` "notifyAll() renews the state reference and notifies"; `ReactStore.test.tsx` "supports nested stores as state values".
- **UTL-STA-13** — MUST. `subscribe(listener)` registers a listener and returns a function that unregisters it; an unregistered listener is never called again. Registering the same function twice has no additional effect (set semantics).
  Source: `packages/utils/src/store/Store.ts:29,46-51`. Test: [L1] `Store.test.ts` "unsubscribing stops notifications".
- **UTL-STA-14** — MUST. `Store.create(state)` (and `ReactStore.create`) constructs an instance of the class it is called on, seeded with `state`; each call yields an independent instance.
  Source: `packages/utils/src/store/Store.ts:15-17`. Test: [L1] `Store.test.ts` "returns a Store instance seeded with the given state", "constructs an instance of the subclass it is called on", "produces an independent instance per call".

#### 4.2.2 `ReactStore` — context, selectors, synced values

- **UTL-STA-15** — MUST. A component store carries a `context` bag (default `{}`) of non-reactive values; writing to it never notifies listeners.
  Source: `packages/utils/src/store/ReactStore.ts:26-35`. Test: [L1] `ReactStore.test.tsx` "create() constructs a fully wired ReactStore instance" (`context` equals `{}`).
- **UTL-STA-16** — MUST. `useContextCallback(key, fn)` stores under `context[key]` a stable trampoline (the same instance on every render of the owning part, for the part's lifetime) that invokes the most recently supplied `fn`, or does nothing when `fn` is `undefined`.
  Source: `packages/utils/src/store/ReactStore.ts:188-195`; `packages/utils/src/useStableCallback.ts:35-52`. Test: none.
- **UTL-STA-17** — MUST. `select(key, ...args)` evaluates the named selector against the current record synchronously (`selectors[key](state, a1, a2, a3)`) without subscribing.
  Source: `packages/utils/src/store/ReactStore.ts:154-162`. Test: [L1] `ReactStore.test.tsx` "supports nested stores as state values" (`childStore.select('count')`).
- **UTL-STA-18** — MUST. `useState(key, ...args)` (a reactive read) returns the named selector's result and re-renders the reading component whenever that result changes by `Object.is`, including when the extra arguments change between renders.
  Source: `packages/utils/src/store/ReactStore.ts:171-179`; `packages/utils/src/store/useStore.ts:16-46,159-175`. Test: [L2] `ReactStore.test.tsx` "updates useState result when selector arguments change in a fast component", "supports nested stores as state values".
- **UTL-STA-19** — MUST. `useSyncedValue(key, value)`: post-commit, pre-paint, if `state[key] !== value` (strict inequality) the store performs `set(key, value)`. Re-targeting to a different store instance syncs the value into the new store. Server: see ADR 01 relaxation table.
  Source: `packages/utils/src/store/ReactStore.ts:45-54`. Test: [L2] `ReactStore.test.tsx` "useProp updates a single key when the passed value changes", "useProp syncs the same value when the store changes".
- **UTL-STA-20** — MUST. `useSyncedValueWithCleanup(key, value)` behaves as UTL-STA-19 and additionally performs `set(key, undefined)` when the owning component is removed or before re-syncing after `key`, `value` or the store instance changes. Server: see ADR 01 relaxation table.
  Source: `packages/utils/src/store/ReactStore.ts:63-78`. Test: [L2] `ReactStore.test.tsx` "useSyncedValueWithCleanup synchronizes value and resets on cleanup".
- **UTL-STA-21** — MUST. `useSyncedValues(record)`: post-commit, pre-paint, `update(record)` runs only when at least one entry value differs (by `Object.is`) from the previous render's entry at the same position; a new object with identical entry values does not trigger an update. Server: see ADR 01 relaxation table.
  Source: `packages/utils/src/store/ReactStore.ts:90-114`. Test: [L2] `ReactStore.test.tsx` "useProps applies multiple keys from a props object", "useSyncedValues depends on entries instead of object identity".
- **UTL-STA-22** — MUST. `useControlledProp(key, controlled)`: post-commit, pre-paint, when `controlled !== undefined` and `!Object.is(state[key], controlled)`, the record is replaced with `key` set to `controlled` (listeners notified); when `controlled` is `undefined` the store value is left alone (component-owned). Re-targeting to another store instance applies the same rule to it. Server: see ADR 01 relaxation table.
  Source: `packages/utils/src/store/ReactStore.ts:120-131`. Test: [L2] `ReactStore.test.tsx` "syncs internal state from controlled prop", "syncs internal state from controlled prop when the store changes".
- **UTL-STA-23** — MUST. `useStateSetter(key)` returns a function with stable identity across renders that performs `set(key, value)` when called.
  Source: `packages/utils/src/store/ReactStore.ts:203-211`. Test: [L2] `ReactStore.test.tsx` "useStateSetter returns a stable callback that updates the store state".
- **UTL-STA-24** — MUST. `observe(selectorOrKey, listener)` invokes `listener(current, current, store)` synchronously once at subscription, then after every notification evaluates the selector and, only if the result changed by `Object.is`, invokes `listener(newValue, previousValue, store)`; it returns an unsubscribe function. Multiple observers on the same or different selectors are independent.
  Source: `packages/utils/src/store/ReactStore.ts:233-257`. Test: [L1] `ReactStore.test.tsx` "observeSelector" group (nine tests): "accepts selector functions", "calls listener immediately with current selector result on subscription", "calls listener when selector result changes", "does not call listener when selector result is unchanged", "calls listener when any dependency of the selector changes", "provides the store instance to the listener", "returns an unsubscribe function that stops observing", "supports multiple observers on the same selector", "supports observers on different selectors".

#### 4.2.3 `useStore` (reactive read primitive)

- **UTL-STA-25** — MUST. `useStore(store, selector, a1?, a2?, a3?)` returns `selector(store.state, a1, a2, a3)` on every render of the part, and the subscribing part re-renders whenever a store notification yields a different result by `Object.is`; a notification whose projected result is unchanged does not re-render the subscribing part (render count observed through bUnit, L2).
  Source: `packages/utils/src/store/useStore.ts:16-61,181-194`. Test: [L2] `ReactStore.test.tsx` "supports nested stores as state values" (rendered `output` tracks the selected value).
- **UTL-STA-26** — MUST. Reads never tear: within one render of the part every reactive read of a store observes the same record, and a change that lands between the part's render and its subscription makes the subscribing part re-render rather than being lost.
  Source: `packages/utils/src/store/useStore.ts:48-61` (external-store subscription contract). Test: none.
- **UTL-STA-27** — SHOULD. An implementation MAY collapse several reactive reads inside one component into a single subscription per store (Base UI does so on React 19 for components wrapped in `fastComponent`: dialog, menu, popover, preview card, tooltip); the observable behavior (what re-renders and what value is read) must be identical to one subscription per read.
  Source: `packages/utils/src/store/useStore.ts:13-14,79-179`; `packages/utils/src/fastHooks.ts:63-92`. Test: [L2] `ReactStore.test.tsx` "updates useState result when selector arguments change in a fast component".

#### 4.2.4 `StoreInspector` (development aid)

- **UTL-STA-28** — MAY. A development-only floating panel that renders `JSON.stringify(store.state, replacer, 2)`, the `context` (when non-empty) and optional `additionalData`, re-rendering 1 ms after each store notification; the replacer prints DOM elements as `Element(tag#id)`, `undefined` as `'[undefined]'`, `Map`/`Set` as arrays, and repeated ancestors as `'[circular reference]'`. It is portable only as a convenience and carries no behavioral contract for components.
  Source: `packages/utils/src/store/StoreInspector.tsx:177-213,242-327`. Test: none.

### 4.3 STA — Selectors (`createSelector`, `createSelectorMemoized`)

No exported component at v1.9.0 calls these factories directly (stores declare plain selector functions), but they are part of `@base-ui/utils/store` and the memoized variant defines the equality semantics a port should adopt if it caches projections.

- **UTL-STA-29** — MUST. `createSelector(f)` with a single function returns `f` itself (same identity, no memoization). `createSelector(s1 … sN, combiner)` with 1 ≤ N ≤ 7 input selectors returns `(state, a1, a2, a3) → combiner(s1(state,a1,a2,a3), …, sN(state,a1,a2,a3), a1, a2, a3)`; nothing is cached. N > 7 throws `Error('Unsupported number of selectors')`; no arguments throws `Error('Missing arguments')`.
  Source: `packages/utils/src/store/createSelector.ts:148-228`. Test: [L1] `createSelector.test.ts` "returns the input function when called with a single selector", "supports one input selector plus a combiner", "supports six input selectors plus a combiner", "supports seven input selectors plus a combiner", "throws when given one more than the maximum (eight input selectors plus a combiner)", "passes extra args through to every input selector and to the combiner".
- **UTL-STA-30** — MUST. `createSelectorMemoized(...)` returns a selector that caches its last result **per state record** (keyed on the record's identity, with shallow copies of a record sharing its key) and re-runs the combiner only when an input-selector result or an extra argument differs by `Object.is` from the cached call. (Transcribed as UTL-ALG-04.)
  Source: `packages/utils/src/store/createSelectorMemoized.ts:8-16,59-155`. Test: [L1] `createSelectorMemoized.test.ts` "re-runs the combiner when input selector results change", "caches separately per state identity", "passes one extra argument to the combiner and memoizes on it", "passes two extra arguments to the combiner", "passes three extra arguments to the combiner".
- **UTL-STA-31** — MUST. In the single-function form the function is both input and combiner, wired behind an identity input selector, so it receives `(state, a1, a2, a3)` and re-runs whenever the state reference changes; a zero-parameter function is called with the state and returns a constant.
  Source: `packages/utils/src/store/createSelectorMemoized.ts:65-68,142-151`. Test: [L1] `createSelectorMemoized.test.ts` "uses an identity input selector when only a combiner is provided", "supports a zero-parameter combiner called with the state", "passes extra arguments to a single combiner alongside the state".
- **UTL-STA-32** — MUST. The number of extra arguments is derived from the combiner's declared parameter count: `argsLength = combiner.length === 0 ? 0 : combiner.length − N`; if that is negative (rest parameters, wrappers) or greater than 3, construction throws `Error('Unsupported number of arguments')`.
  Source: `packages/utils/src/store/createSelectorMemoized.ts:74-78`. Test: [L1] `createSelectorMemoized.test.ts` "throws when the combiner takes more than three extra arguments", "throws when Function.length under-reports the combiner parameters".
- **UTL-STA-33** — MUST. `createSelectorMemoizedWithOptions(options)`: with no options it behaves as `createSelectorMemoized`; object-form `memoizeOptions` are merged over the defaults `{ maxSize: 1, equalityCheck: Object.is }`, while a bare equality function or array form replaces them (passed to `reselect` unchanged); a custom `memoize` without `memoizeOptions` receives no options at all; custom `memoizeOptions` with a custom `memoize` are forwarded verbatim; a `resultEqualityCheck` that reports equality makes the selector return the previous result reference.
  Source: `packages/utils/src/store/createSelectorMemoized.ts:27-57`. Test: [L1] `createSelectorMemoized.test.ts` "createSelectorMemoizedWithOptions" group (seven tests): "produces a working factory when no options are provided", "forwards options through to the underlying reselect creator", "merges object memoizeOptions over the Object.is equality default", "does not forward the lruMemoize defaults to a custom memoizer", "does not break a custom memoizer that defaults its options parameter", "forwards custom memoizeOptions to a custom memoizer verbatim", "preserves the result reference with a resultEqualityCheck option".

### 4.4 STA — `NullStore`, `usePreviousValue`, `useValueChanged`

- **UTL-STA-34** — MUST. `NullStore` is a component store whose `setState`, `update`, `set` and `notifyAll` are no-ops: reads (`state`, `select`, `useState`, `observe`) work, writes never change the record or notify. Used as the detached fallback store for dialog, menu, popover, preview-card and tooltip parts rendered outside their root.
  Source: `utils/NullStore.ts:11-26`. Test: none.
- **UTL-STA-35** — MUST. `usePreviousValue(value)` returns `null` on the first render and thereafter the value from the most recent render in which `value` differed (by `Object.is`) from the one before; renders in which `value` is unchanged do not shift the history. `NaN → NaN` is unchanged; `0 → -0` is a change.
  Source: `packages/utils/src/usePreviousValue.ts:9-20`. Test: [L2] `usePreviousValue.test.tsx` "should return null on the first render", "should return the previous value on subsequent renders", "should work with primitive values", "should treat NaN as unchanged", "should return the previous value when changing to NaN", "should distinguish positive and negative zero", "should ignore renders where the value does not change", "should work with object values", "should handle undefined and null values", "should handle rapid value changes", "should maintain type safety".
- **UTL-STA-36** — MUST. `useValueChanged(value, onChange)`: the check runs post-commit, pre-paint, only in renders where `value` differs from the previous render's value by `Object.is`; in such a render `onChange(recorded)` is invoked once when `value !== recorded` (strict inequality), and `recorded` is then set to `value`. Consequently `0 → -0` records `-0` without invoking `onChange`, a later `-0 → 1` invokes `onChange(-0)`, `NaN → NaN` invokes nothing, and the first render never invokes it.
  Source: `internals/useValueChanged.ts:6-17`. Test: [L2] `useValueChanged.test.tsx` "retains -0 as the previous value without treating it as a change from 0 (strict: %s)" (2 variants).

### 4.5 STA — `useMediaQuery`

- **UTL-STA-37** — MUST. A leading `@media` (with optional following space) is stripped from `query` before it is passed to any `matchMedia` implementation.
  Source: `unstable-use-media-query/index.ts:13`. Test: [L3] `index.test.tsx` "subscribes to the provided matchMedia and updates on change" (`matchMedia` called with `'(min-width: 600px)'`).
- **UTL-STA-38** — MUST. When no `matchMedia` implementation is available (no `window.matchMedia` and none supplied, or `matchMedia: null`), the hook returns `defaultMatches` (default `false`) and subscribes to nothing.
  Source: `unstable-use-media-query/index.ts:10-20,36-39`. Test: [L3] `index.test.tsx` "returns defaultMatches when matchMedia is unavailable".
- **UTL-STA-39** — MUST. With a `matchMedia` implementation, the hook returns `matchMedia(query).matches`, subscribes to the list's `change` event, re-renders with the new value on change, and removes the listener when the component is removed or the query/implementation changes.
  Source: `unstable-use-media-query/index.ts:41-49`. Test: [L3] `index.test.tsx` "subscribes to the provided matchMedia and updates on change".
- **UTL-STA-40** — MUST. The server-render / hydration snapshot resolves in this order: if `noSsr` and a `matchMedia` exists → the live `matchMedia(query).matches`; else if `ssrMatchMedia` is supplied → `ssrMatchMedia(query).matches` (evaluated once per query/implementation); else `defaultMatches`. (UTL-ALG-15.)
  Source: `unstable-use-media-query/index.ts:24-34`. Test: [L3] `index.test.tsx` "uses ssrMatchMedia for the server snapshot", "uses the client matchMedia result as the server snapshot when noSsr is set".

### 4.6 EVT — Timers (`Timeout`, `AnimationFrame`, `Interval`, `IdleCallback`, `TimeoutManager`)

Components never call `setTimeout`/`requestAnimationFrame` directly; they use these wrappers, which add two guarantees: a new `start`/`request` replaces any pending call on the same instance, and the per-instance variants cancel automatically when the owning component is removed. Component documents cite "a `Timeout` of N ms" etc. and rely on these semantics.

- **UTL-EVT-01** — MUST. `Timeout.start(delay, fn)` cancels any pending call on the same instance, then schedules `fn` once after `delay` milliseconds (macrotask timer). When it fires, the instance is marked idle **before** `fn` runs, so `fn` may call `start` again.
  Source: `packages/utils/src/useTimeout.ts:19-25`. Test: none (behavior mirrored by `useIdleCallback.test.tsx` "start() clears any previously scheduled callback", "can be reused after the callback has run").
- **UTL-EVT-02** — MUST. `Timeout.clear()` cancels a pending call and is a no-op when nothing is pending; `isStarted()` is `true` exactly while a call is pending.
  Source: `packages/utils/src/useTimeout.ts:27-36`. Test: none.
- **UTL-EVT-03** — MUST. `useTimeout()` returns one `Timeout` instance per component instance (same identity across renders) and clears it when the component is removed.
  Source: `packages/utils/src/useTimeout.ts:46-52`; `packages/utils/src/useOnMount.ts:8-13`. Test: none (mirrored by `useIdleCallback.test.tsx` "returns a stable scheduler across re-renders", "cancels a pending callback on unmount").
- **UTL-EVT-04** — MUST. `AnimationFrame.request(fn)` (instance method) cancels any pending frame callback on the same instance, then schedules `fn` for the next frame; `cancel()` discards a pending callback; the instance is marked idle before `fn` runs. `useAnimationFrame()` gives one instance per component, cancelled on removal.
  Source: `packages/utils/src/useAnimationFrame.ts:122-156`. Test: none.
- **UTL-EVT-05** — MUST. All frame callbacks requested through `AnimationFrame` (static `request`/`cancel` and instances) share one scheduler: every callback requested before the next frame fires is run in that single frame, in request order, each callback passed to the static `request` receiving the frame timestamp (instance callbacks are invoked without arguments); a cancelled callback is skipped; callbacks requested while the frame is running go to the following frame; cancelling an id twice, or an id from an already-run frame, has no effect. (UTL-ALG-09.)
  Source: `packages/utils/src/useAnimationFrame.ts:15-84`. Test: [L3] `useAnimationFrame.test.ts` "keeps callback accounting correct when a frame is canceled more than once".
- **UTL-EVT-06** — MUST. `Interval.start(delay, fn)` cancels any pending interval on the instance and invokes `fn` every `delay` ms until `clear()`; it reports `isStarted()` while running. `useInterval()` gives one instance per component, cleared on removal.
  Source: `packages/utils/src/useInterval.ts:10-42`. Test: none.
- **UTL-EVT-07** — MUST. `IdleCallback.start(fn)` cancels any pending call on the instance and schedules `fn` to run asynchronously after the current task: through `requestIdleCallback` when the platform provides it, otherwise as a 0 ms macrotask. `clear()` cancels; the handle is reset to idle before `fn` runs so a later `clear()` cannot cancel an unrelated call.
  Source: `packages/utils/src/useIdleCallback.ts:5-46`. Test: [L1] `useIdleCallback.test.tsx` "IdleCallback class" group: "create() returns a new instance each time", "runs the scheduled callback after the current task", "clear() cancels a pending callback", "start() clears any previously scheduled callback", "can be reused after the callback has run", "disposeEffect() returns the clear function"; `useIdleCallback.fallback.test.ts` "schedules and cancels callbacks when requestIdleCallback is unavailable".
- **UTL-EVT-08** — MUST. `useIdleCallback()` returns one `IdleCallback` instance per component and cancels a pending callback when the component is removed.
  Source: `packages/utils/src/useIdleCallback.ts:56-62`. Test: [L2] `useIdleCallback.test.tsx` "returns a stable scheduler across re-renders", "cancels a pending callback on unmount".
- **UTL-EVT-09** — MUST. `TimeoutManager` keeps independent timers by string key: `start(key, delay, fn)` replaces any pending timer under `key`; `clear(key)` cancels it and is safe for unknown or already-fired keys; `clearAll()` cancels every pending timer; a fired timer removes its own key before running `fn`.
  Source: `internals/TimeoutManager.ts:4-29`. Test: [L1] `TimeoutManager.test.ts` "calls the callback after the specified delay", "replaces a timeout when start is called with the same key", "clears a pending timeout", "does not error when clearing a non-existent key", "clears all active timeouts", "allows safe clear after a timeout has fired".

### 4.7 EVT — `RequestQueue`, `addEventListener`, `mergeCleanups`

- **UTL-EVT-10** — MUST. `RequestQueue({ fetchFn, maxConcurrentRequests = Infinity, getKeyId = String })`: `queue(keys)` adds each key whose id is **not currently pending** to the queued map (a queued id is simply overwritten, never duplicated), then starts processing; processing moves up to `maxConcurrentRequests − pending` queued entries, oldest first, into the pending map and calls `fetchFn(key)` for each. (UTL-ALG-10.)
  Source: `internals/RequestQueue.ts:37-41,47-55,57-94`. Test: [L1] `RequestQueue.test.ts` "calls fetchFn for each queued key", "respects maxConcurrentRequests", "does not re-queue a key that is already pending", "supports custom getKeyId for complex key types", "processes keys in FIFO order".
- **UTL-EVT-11** — MUST. `getRequestStatus(key)` returns `'pending'` while the id is in the pending map, `'queued'` while it is waiting, otherwise `'unknown'`.
  Source: `internals/RequestQueue.ts:113-122`. Test: [L1] `RequestQueue.test.ts` "reports correct request status".
- **UTL-EVT-12** — MUST. A fetch that **rejects** removes its id from the pending map automatically and processing continues with the next queued entry; a fetch that **resolves** leaves its id pending until the owner calls `setRequestSettled(key)` or `clearPendingRequest(key)`, both of which remove the id and resume processing. `clear()` empties both maps without cancelling in-flight fetches.
  Source: `internals/RequestQueue.ts:73-84,96-111`. Test: [L1] `RequestQueue.test.ts` "continues processing when fetchFn rejects", "processes next queued item on setRequestSettled", "processes next queued item on clearPendingRequest", "clears all queued and pending requests".
- **UTL-EVT-13** — MUST. `addEventListener(target, type, listener, options?)` registers the listener and returns a function that removes it with the identical `(type, listener, options)` triple.
  Source: `packages/utils/src/addEventListener.ts:52-62`. Test: [L3] `addEventListener.test.ts` "adds the listener and returns an unsubscribe function".
- **UTL-EVT-14** — MUST. `mergeCleanups(...cleanups)` returns a function that invokes each supplied cleanup in argument order, skipping `false`, `null` and `undefined` entries.
  Source: `packages/utils/src/mergeCleanups.ts:6-15`. Test: [L3] `mergeCleanups.test.ts` "calls each cleanup in order and skips empty values".

### 4.8 DOM — Document scroll lock (`useScrollLock`)

`useScrollLock(enabled = true, referenceElement = null)` is called by modal dialogs (`dialog/root/useDialogRoot.ts:87`: `useScrollLock(open && modal === true, popupElement)`) and, through PSH `useAnchoredPopupScrollLock`, by anchored popups. The algorithm is transcribed in UTL-ALG-01/02; the requirements below state what is observable.

- **UTL-DOM-01** — MUST. Scroll locking is reference-counted across the whole page: every enabled caller increments a global count; the page is locked when the count goes from 0 to 1 and unlocked when it returns to 0. Overlapping (nested) locks never re-apply or restore styles in between.
  Source: `packages/utils/src/useScrollLock.ts:228-247,304,312-319`. Test: none in `packages/utils`; component tests use `test/scrollLock.ts` `isScrollLocked()` (see §8.3).
- **UTL-DOM-02** — MUST. Both the lock and the unlock are deferred by a 0 ms macrotask timer: a lock acquired and released within the same task (for example a re-render caused by a consumer-owned value change) applies nothing; an unlock followed by a re-acquire within the same task keeps the existing lock in place.
  Source: `packages/utils/src/useScrollLock.ts:234-259`. Test: none. Docs: CHANGELOG.md v1.4.0 "Prevent scroll lock when a controlled value triggers a re-render (#4507)".
- **UTL-DOM-03** — MUST. When the lock fires and the page is already locked by someone else (computed `overflow-y` of the viewport scroller matches `/hidden|clip/`), no styles are written; instead attribute mutations on `<html>` and `<body>` are observed and the lock is applied as soon as the page is no longer locked, provided the count is still positive. Releasing while waiting just stops observing.
  Source: `packages/utils/src/useScrollLock.ts:20-22,269-288`. Test: none. Docs: CHANGELOG.md v1.7.0 "Fix scroll lock handoff with external overlays (#4665)".
- **UTL-DOM-04** — MUST. The *viewport scroller* is `<html>` when `<html>` is an overflow element (computed `overflow`/`overflow-x`/`overflow-y` contain any of `auto`, `scroll`, `overlay`, `hidden`, `clip` and computed `display` is neither `inline` nor `contents`), otherwise `<body>`.
  Source: `packages/utils/src/useScrollLock.ts:16-18` (`isOverflowElement` from `@floating-ui/utils/dom`). Test: none.
- **UTL-DOM-05** — MUST. *Simple mode* is used when `platform.os.ios` is true **or** when `window.innerWidth − documentElement.clientWidth` is not greater than 0 (no inset scrollbar currently consumes width): it writes inline `overflow-y: hidden; overflow-x: hidden` on the viewport scroller only and restores the previous inline values on unlock. Nothing else changes (no attribute, no scroll offset handling).
  Source: `packages/utils/src/useScrollLock.ts:24-31,64-87,290-300`. Test: none.
- **UTL-DOM-06** — MUST. *Inset mode* (all other cases) first checks for Safari pinch-zoom: when `platform.engine.webkit` and `window.visualViewport.scale` is not 1, nothing is locked.
  Source: `packages/utils/src/useScrollLock.ts:100-103`. Test: none.
- **UTL-DOM-07** — MUST. Inset mode, *gutter path*: when `CSS.supports('scrollbar-gutter', 'stable')` and a probe shows the viewport scroller's `offsetWidth` is the same with `overflow-y: scroll` and `overflow-y: hidden` while `<html>` has inline `scrollbar-gutter: stable`, the lock writes only `scrollbar-gutter: stable` (or `stable both-edges` when the computed `<html>` value already contains `both-edges`) on `<html>` and `overflow-y/x: hidden` on the viewport scroller; unlock restores the inline values. No attribute is set and scroll offsets are untouched.
  Source: `packages/utils/src/useScrollLock.ts:33-62,105-163,196-199`. Test: none.
- **UTL-DOM-08** — MUST. Inset mode, *full path* (gutter path unavailable): the lock snapshots `html.scrollTop/scrollLeft`, then writes the inline styles of UTL-ALG-02 — on `<html>`: `scrollbar-gutter`, `overflow-y`/`overflow-x` (`scroll` when the axis is scrollable or already constant `scroll`, else `hidden`), `scroll-behavior: unset`; on `<body>`: `position: relative`, `height: calc(100dvh − (marginTop+marginBottom+scrollbarHeight)px)` (plain `100dvh` when that sum is 0), `width: calc(100vw − (marginLeft+marginRight+scrollbarWidth)px)` (plain `100vw` when 0), `box-sizing: border-box`, `overflow-y/x: hidden`, `scroll-behavior: unset` — assigns the snapshotted offsets to `body.scrollTop/scrollLeft`, and sets the attribute `data-base-ui-scroll-locked=""` on `<html>`.
  Source: `packages/utils/src/useScrollLock.ts:114-150,165-194`. Test: none (`test/scrollLock.ts` reads the attribute).
- **UTL-DOM-09** — MUST. Full-path unlock restores every inline style it changed on `<html>` and `<body>` to the captured values, restores `html.scrollTop/scrollLeft` to the snapshot, removes `data-base-ui-scroll-locked`, and restores `<html>` inline `scroll-behavior`.
  Source: `packages/utils/src/useScrollLock.ts:117-132,196-206`. Test: none.
- **UTL-DOM-10** — MUST. While an inset-mode lock is active, a `resize` event on the window restores the styles immediately and re-applies the lock on the next frame (re-measuring scrollbars and margins); the pending frame is cancelled on unlock.
  Source: `packages/utils/src/useScrollLock.ts:208-225`. Test: none.
- **UTL-DOM-11** — MUST. All measurements and writes target the document that owns `referenceElement` (the global document when `null`), so popups rendered into another document lock that document. One read is an exception: the overlay-vs-inset decision (UTL-DOM-05) compares `innerWidth` of the window resolved from the *document node* — which is the global window, because a document has no `ownerDocument` — with that document's `documentElement.clientWidth`.
  Source: `packages/utils/src/useScrollLock.ts:261-264,28-29,41,65-66,90-93`; `packages/utils/src/owner.ts:1`; `reference/vendor/@floating-ui_utils/dist/floating-ui.utils.dom.mjs:13-16` (`getWindow`). Test: none.
- **UTL-DOM-12** — MUST. The scroll lock is applied post-commit, pre-paint for the render that enables it, and released when the enabling render is undone or the owner is removed.
  Source: `packages/utils/src/useScrollLock.ts:312-319`. Test: none.

### 4.9 DOM — Visually hidden styles, `inert`, disabled detection

- **UTL-DOM-13** — MUST. `visuallyHidden` is exactly: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0`. It is used for hidden inputs/owner elements that must not contribute to any ancestor's scrollable overflow, in either reading direction.
  Source: `packages/utils/src/visuallyHidden.ts:3-20`. Test: [L2] `visuallyHidden.test.tsx` "does not overflow a %s scroller with %s content" (four direction combinations; browser only) (L4 for the cited browser-only overflow test).
- **UTL-DOM-14** — MUST. `visuallyHiddenInput` is exactly: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute`.
  Source: `packages/utils/src/visuallyHidden.ts:3-12,22-25`. Test: none.
- **UTL-DOM-15** — MUST. `ownerVisuallyHidden` (for the empty `aria-owns` owner span) is exactly: `clip-path: inset(50%); position: fixed; top: 0; left: 0`.
  Source: `internals/constants.ts:36-41`. Test: none.
- **UTL-DOM-16** — MUST. Where a part sets the `inert` attribute from a boolean, the rendered DOM has the attribute present (empty value) when true and absent when false — the React-version branch in `inertValue` (`'true'` string on React < 19, boolean on ≥ 19) exists only to reach that DOM result.
  Source: `packages/utils/src/inertValue.ts:3-9`. Test: none.
- **UTL-DOM-17** — MUST. `isElementDisabled(el)` is `true` when `el` is `null`, has a `disabled` attribute, or has `aria-disabled="true"`.
  Source: `packages/utils/src/isElementDisabled.ts:1-7`. Test: none.

### 4.10 DOM — Owner and shadow-DOM helpers

- **UTL-DOM-18** — MUST. `ownerDocument(node)` is `node.ownerDocument` or, for `null`, the global document; `ownerWindow(node)` is that document's `defaultView` or the global window.
  Source: `packages/utils/src/owner.ts:1-5`. Test: none.
- **UTL-DOM-19** — MUST. `activeElement(doc)` starts at `doc.activeElement` and, while the current element has a shadow root whose `activeElement` is non-null, descends into it; the deepest such element is returned.
  Source: `packages/utils/src/shadowDom.ts:3-11`. Test: none.
- **UTL-DOM-20** — MUST. `contains(parent, child)` is `false` when either is missing; `true` when native `parent.contains(child)`; otherwise, when `child`'s root node is a shadow root, `true` if walking `child → parentNode → … → shadow host → …` reaches `parent`; else `false`.
  Source: `packages/utils/src/shadowDom.ts:13-37`. Test: none.
- **UTL-DOM-21** — MUST. `closest(node, selector)` walks the composed tree from `node` (which may be a non-element such as a text node): at each step an element matching `selector` is returned; the next node is `assignedSlot`, else `parentNode`, else (for a shadow root) its `host`; a detached subtree yields `null` (never the document element); an invalid selector throws the native error. `:scope` is unsupported.
  Source: `packages/utils/src/shadowDom.ts:55-70`. Test: [L3] `shadowDom.test.ts` "closest" group: "finds an ancestor across nested shadow roots from a non-Element node", "follows the composed tree through slots", "returns null for a detached node instead of falling back to the document element", "preserves native selector validation", "finds an interactive ancestor outside a composed event target shadow root".
- **UTL-DOM-22** — MUST. `getTarget(event)` is `event.composedPath()[0]` while the event is being dispatched (the real target inside shadow DOM), falling back to `event.target` when the composed path is empty (after dispatch) or unsupported.
  Source: `packages/utils/src/shadowDom.ts:72-82`. Test: [L3] `shadowDom.test.ts` "returns the composed-path target while the event is being dispatched", "falls back to `target` once dispatch has completed", "finds an interactive ancestor outside a composed event target shadow root".

### 4.11 DOM — Measurement and scroll helpers

- **UTL-DOM-23** — MUST. `getCssDimensions(el)` reads computed `width`/`height` (non-numeric → 0); for HTML elements, if the rounded CSS value differs from `offsetWidth`/`offsetHeight` (e.g. transforms, subpixel), the offset values are used instead; for non-HTML elements (SVG) the CSS values are used as-is.
  Source: `utils/getCssDimensions.ts:5-25`. Test: [L3] `getCssDimensions.test.ts` "reads the CSS size of an SVG element" (L4 for the cited SVG test, which needs real layout).
- **UTL-DOM-24** — MUST. `getElementTransform(el, computedStyle?)` returns `{ x, y, scale }` parsed from the computed `transform` matrix per UTL-ALG-13; `none`/absent → `{0, 0, 1}`. The separate `translate`/`scale`/`rotate` properties are not considered.
  Source: `utils/getElementTransform.ts:10-33`. Test: none.
- **UTL-DOM-25** — MUST. `getPseudoElementBounds(el)` returns the bounding rect extended symmetrically so that it covers `::before`/`::after` pseudo-elements whose `content` is not `none`, per UTL-ALG-14; without pseudo-elements it is the plain bounding rect; under jsdom (`platform.env.jsdom`) it is always the plain rect and pseudo-element styles are not read.
  Source: `utils/getPseudoElementBounds.ts:31-69`. Test: [L3] `getPseudoElementBounds.test.ts` "does not read pseudo-element styles in jsdom", "includes pseudo-element bounds in browsers" (L4 for the browser-only bounds test).
- **UTL-DOM-26** — MUST. `isMouseWithinBounds(event, el)` (in `utils/getPseudoElementBounds.ts`) is `true` when `event.clientX/clientY` lie within the pseudo-element bounds expanded by 5 px on every side (inclusive).
  Source: `utils/getPseudoElementBounds.ts:14,20-29`. Test: [L3] `getPseudoElementBounds.test.ts` "allows up to 5px of mouse drift around element bounds".
- **UTL-DOM-27** — MUST. `getElementAtPoint(root, x, y)` returns `root.elementFromPoint(x, y)` when `root` (a document or shadow root) implements it, else `null`; callers pass the popup's root node so that shadow content is not retargeted to its host.
  Source: `utils/getElementAtPoint.ts:6-12`. Test: none.
- **UTL-DOM-28** — MUST. `isScrollableY(el, allowOverflowIntent = false)` is `true` iff computed `overflow-y` is `auto` or `scroll` and (`allowOverflowIntent` ? `clientHeight > 0` : `scrollHeight > clientHeight`); `isScrollableX` is the same on the horizontal axis; `isScrollable(el, axis, …)` dispatches on `'vertical'`/`'horizontal'`.
  Source: `utils/scrollable.ts:11-40`. Test: none.
- **UTL-DOM-29** — MUST. `findScrollableAncestor(target, root, axis, allowOverflowIntent)` walks from `target` up the composed tree (crossing shadow boundaries and slots), stopping before `root` and before `html`/`body`/document, and returns the first HTML element that is scrollable on `axis`; non-HTML nodes are skipped but their ancestors are still visited. `findScrollableTouchTarget` falls back to `root` itself when it is scrollable, else `null`.
  Source: `utils/scrollable.ts:42-71`. Test: none.
- **UTL-DOM-30** — MUST. `normalizeScrollOffset(value, max)`: `max ≤ 0` → 0; otherwise clamp to `[0, max]` and snap to `0` when within 1 px of the start, to `max` when within 1 px of the end; when both tolerances apply, the nearer edge wins and a tie goes to 0. `getMaxScrollOffset(scrollSize, clientSize) = max(0, scrollSize − clientSize)`. `SCROLL_EDGE_TOLERANCE_PX = 1`.
  Source: `utils/scrollEdges.ts:3-33`. Test: [L3] `scrollEdges.test.ts` "returns 0 when max is non-positive", "snaps to the start edge within the tolerance", "snaps to the end edge within the tolerance", "keeps values away from edges unchanged", "chooses the closest edge when tolerances overlap".
- **UTL-DOM-31** — MUST. `valueToPercent(value, min, max) = ((value − min) × 100) / (max − min)` (no clamping, no guard for `max === min`).
  Source: `utils/valueToPercent.ts:1-3`. Test: none.

### 4.12 DOM — CSP: inline `<style>`/`<script>` elements

Exactly three places in the library emit inline elements, and `CSPProvider` governs all of them.

- **UTL-DOM-32** — MUST. The scrollbar-hiding stylesheet is a single `<style>` element whose text is exactly `.base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}`; it is emitted whenever a `ScrollArea.Root` or a `Select.Popup` renders (unconditionally, whether or not the class is currently applied; the class itself is applied by `ScrollArea.Viewport`, by `Select.Popup` while item-aligned without a `Select.List`, and by `Select.List` while it shows scroll arrows outside touch interaction), is de-duplicated per document (hoisted, keyed by `href="base-ui-disable-scrollbar"`, precedence `base-ui:low`), and carries `nonce` equal to the provider's `nonce` when one is set.
  Source: `utils/styles.tsx:1-12`; `scroll-area/root/ScrollAreaRoot.tsx:53,345`; `select/popup/SelectPopup.tsx:68,461,478`; `scroll-area/viewport/ScrollAreaViewport.tsx:370`; `select/list/SelectList.tsx:43`. Test: [L2] `CSPProvider.test.tsx` "applies nonce to inline style tags", "renders inline style tags by default".
- **UTL-DOM-33** — MUST. When an ancestor `CSPProvider` has `disableStyleElements` true, neither `ScrollArea.Root` nor `Select.Popup` emits that `<style>` element; the components still apply the `base-ui-disable-scrollbar` class so the consumer's own stylesheet can provide the rule.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:345`; `select/popup/SelectPopup.tsx:478`; `utils/styles.tsx:4`. Test: [L2] `CSPProvider.test.tsx` "does not render inline style tags when disableStyleElements is true", "does not render Select inline style tags when disableStyleElements is true".
- **UTL-DOM-34** — MUST. The pre-hydration `<script>` element (TRN, `internals/PrehydrationScript.tsx`) receives the provider's `nonce`; it is not affected by `disableStyleElements` (scripts are opt-in per component and have no disable flag).
  Source: `internals/PrehydrationScript.tsx:34,42-43`; docs `utils/csp-provider/page.mdx` §"Disable inline style elements". Test: none.
- **UTL-DOM-35** — MUST. Without a `CSPProvider` the effective configuration is `{ disableStyleElements: false, nonce: undefined }`; with one, the nearest ancestor's values apply to every descendant.
  Source: `internals/csp-context/CSPContext.tsx:11-17`; `csp-provider/CSPProvider.tsx:12-24`. Test: [L2] `CSPProvider.test.tsx` "renders inline style tags by default". Docs: CHANGELOG.md v1.1.0 "Add `CSPProvider` (#3553)".
- **UTL-DOM-36** — MUST. `CSPProvider` and `DirectionProvider` render no DOM element of their own; their children are rendered in place.
  Source: `csp-provider/CSPProvider.tsx:23`; `direction-provider/DirectionProvider.tsx:16-18`. Test: none.

### 4.12.1 DOM — Number serialization in attributes and inline styles

Base UI writes JavaScript numbers into the DOM without explicit formatting: as attribute values handed to the renderer (`'aria-valuenow': clampedValue`), through `String(n)`, and through template-literal interpolation into inline styles and CSS custom properties (`` width: `${percentageValue}%` ``, `` `${size}px` ``). All three paths use the ECMAScript Number-to-String algorithm with radix 10. Component documents cite this ID instead of restating it; the localized *text* (`aria-valuetext`, formatted value parts) goes through `formatNumber` (UTL-I18N-05) and is not affected.

- **UTL-DOM-37** — MUST. Every number the library writes into an attribute value, an inline CSS property value or a CSS custom property value without an explicit formatter MUST be serialized exactly as ECMAScript `Number::toString(x, 10)` (ECMA-262 §6.1.6.1.20) does, which the port reproduces as follows:

  ```
  toString(x):
    if x is NaN                      → "NaN"
    if x is +0 or −0                 → "0"
    if x < 0                         → "-" + toString(−x)
    if x is +∞                       → "Infinity"
    let n, k, s be integers with k ≥ 1, 10^(k−1) ≤ s < 10^k, s × 10^(n−k) = x
      (as a mathematical value), and k as small as possible
      — i.e. s is the shortest digit string that round-trips to the same IEEE-754 double;
      when several s of that length qualify, choose the one for which s × 10^(n−k) is
      closest to x, and if two are equally close, the even one
    let D = the k decimal digits of s (no leading zeros)
    if k ≤ n ≤ 21    → D followed by (n − k) "0" characters                       // 30 → "30", 1e20 → "100000000000000000000"
    if 0 < n ≤ 21    → first n digits of D, ".", remaining (k − n) digits          // 12.5 → "12.5", 100/3 → "33.333333333333336"
    if −6 < n ≤ 0    → "0.", then (−n) "0" characters, then D                      // 0.5 → "0.5", 0.000001 → "0.000001"
    let e = n − 1; sign = "+" if e ≥ 0 else "-"
    if k = 1         → D, "e", sign, decimal digits of |e|                          // 1e21 → "1e+21", 1e-7 → "1e-7"
    otherwise        → first digit of D, ".", remaining k − 1 digits, "e", sign, decimal digits of |e|   // 1.5e-7 → "1.5e-7"
  ```

  No rounding, digit grouping, locale-specific separators or trailing-zero padding is applied; the result is culture-invariant. The rule applies equally when the number is interpolated into a longer string with a unit or inside a CSS function — `<n>%` (Slider: SLD-DOM-05/06/09/10), `<n>px` (ScrollArea: SCR-DOM-02/11/20/25) and `translate3d(0,<n>px,0)` (SCR-DOM-24) — because string interpolation of a number applies the same `Number::toString` conversion. (A port's built-in "round-trip" double formatting usually differs at the exponent thresholds and in exponent spelling — e.g. `1E+21`, `1E-07`, or exponent notation from `1e15` — and must not be used unmodified.)
  Source: external — ECMA-262 §6.1.6.1.20 `Number::toString`; relied on at `meter/root/MeterRoot.tsx:55-57`, `meter/indicator/MeterIndicator.tsx:29`, `progress/root/ProgressRoot.tsx:66-68`, `progress/indicator/ProgressIndicator.tsx:29` (and every other numeric attribute/style site). Test: [L1] `meter/root/MeterRoot.test.tsx` "formats the value as its position within a custom range and keeps the indicator in sync" (`aria-valuenow="0.5"`); `progress/root/ProgressRoot.test.tsx` "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range" (`String(expectedValue)`) (L2 for the cited component render tests).

### 4.13 A11Y — Live-region initial announcement

`useInitialLiveRegionTextMutation(present = true)` returns a DOM handle for a live region whose first text content must be announced on mount (Combobox/Autocomplete status regions). Screen readers announce *changes*; text that is present when the region appears is not reliably announced, so the library forces a harmless mutation.

- **UTL-A11Y-01** — MUST. After paint following a render in which the live-region element is present with text (first mount, or each time `present` becomes true while the handle points at an element), the **last non-empty text node** inside the element has U+2060 WORD JOINER appended to its data.
  Source: `internals/useInitialLiveRegionTextMutation.ts:7,11-23,34-51`. Test: [L3] `useInitialLiveRegionTextMutation.test.tsx` "marks the text when the element appears after mount", "skips empty text nodes when finding the announcement text". Docs: CHANGELOG.md v1.4.0 "Fix initial live region announcements (#4286)".
- **UTL-A11Y-02** — MUST. 200 ms after the marker is appended, the text node is restored to its original data, but only if its data is still exactly the marked value (text that changed in the meantime is left alone).
  Source: `internals/useInitialLiveRegionTextMutation.ts:9,53-57`. Test: [L3] `useInitialLiveRegionTextMutation.test.tsx` "does not overwrite text that changes before the reset".
- **UTL-A11Y-03** — MUST. If the owner is removed (or `present` becomes false) before the 200 ms elapse, the pending restore is cancelled and the marker is removed immediately under the same "still marked" condition.
  Source: `internals/useInitialLiveRegionTextMutation.ts:59-65`. Test: none.
- **UTL-A11Y-04** — MUST. No marker is applied on iOS (`platform.os.ios`), nor when the handle is not attached to an element, nor when the element contains no non-empty text node.
  Source: `internals/useInitialLiveRegionTextMutation.ts:35-46`. Test: [L3] `useInitialLiveRegionTextMutation.test.tsx` "does nothing when its ref is not attached".
### 4.14 ARIA — Popup label resolution (`resolvePopupLabel`)

`resolvePopupLabel` computes the `aria-labelledby` value for popup parts (menu popups, submenus) from the part's own props, a substituted element's props, and the active trigger. Component documents cite UTL-ARIA-01 and state only which trigger they pass.

- **UTL-ARIA-01** — MUST. `resolvePopupLabel(props, activeTriggerElement, activeTriggerId)` yields the popup's `aria-labelledby`: an explicit `aria-labelledby` or `aria-label` on the part's props wins; failing that, the same two attributes on a substituted `render` element (unwrapped if lazy); only when neither an `aria-label` nor an `aria-labelledby` was found does it fall back to the active trigger's live `id` (the element's current `id` attribute when an element is known, else the registered id), `undefined` when empty. (UTL-ALG-16.)
  Source: `internals/resolvePopupLabel.ts:10-30`. Test: [L2] `resolvePopupLabel.test.tsx` "falls back to the trigger id", "uses a render element's own label instead of the trigger", "reads a server-created render element's label" (skipped below React 19).

### 4.15 I18N — Reading direction, number formatting, locale filter

#### 4.15.1 `DirectionProvider` / `useDirection`

- **UTL-I18N-01** — MUST. `useDirection()` returns the `direction` of the nearest enclosing `DirectionProvider`, and `'ltr'` when there is none; changing the provider's `direction` updates every descendant reader.
  Source: `internals/direction-context/DirectionContext.tsx:10-16`; `direction-provider/DirectionProvider.tsx:14-18`. Test: [L2] `DirectionProvider.test.tsx` "defaults useDirection to ltr outside a provider", "provides the configured direction to descendants".
- **UTL-I18N-02** — MUST. `DirectionProvider`'s `direction` defaults to `'ltr'` and accepts only `'ltr'` or `'rtl'`.
  Source: `direction-provider/DirectionProvider.tsx:14,23-30`; `internals/direction-context/DirectionContext.tsx:4`. Test: [L1] `DirectionProvider.spec.tsx` (type-level).
- **UTL-I18N-03** — MUST. Direction is resolved **only** from the provider: the library does not read the `dir` attribute, `document.dir`, or computed `direction` of any element. Consumers must set `dir="rtl"`/CSS themselves; the provider affects only component behavior (keyboard mapping, positioning logic), not HTML/CSS layout.
  Source: `internals/direction-context/DirectionContext.tsx:12-16`; docs `utils/direction-provider/page.mdx` §"Anatomy". Test: none.
- **UTL-I18N-04** — MUST. The direction reaches portaled subtrees (popups rendered outside the app root) because it is passed through component state, not through DOM ancestry; the docs recommend wrapping such subtrees in their own provider when the app root's `dir` would not apply.
  Source: docs `utils/direction-provider/page.mdx` §"useDirection"; consumers listed in §6. Test: none.

#### 4.15.2 Number formatting

- **UTL-I18N-05** — MUST. `formatNumber(value, locale?, options?)` returns `''` for `null`/`undefined`, otherwise `new Intl.NumberFormat(locale, options).format(value)`.
  Source: `packages/utils/src/formatNumber.ts:19-28`. Test: [L1] `formatNumber.test.ts` "formats a number", "formats a number with different options", "returns an empty string for null".
  Port note 2026-10-21 (ADR 01 decision 2 as amended 2026-10-18; ADR 08 Amendments 2026-10-21): the port has no `formatNumber` function and no `Intl.NumberFormat`. The shared helper `Utilities/NumberFormatting.Format(value, format, culture)` is `double.ToString(format, culture ?? CultureInfo.CurrentCulture)` with `format` a .NET numeric format string and `culture` a `CultureInfo`; output is equivalent, not byte-identical, to `Intl.NumberFormat`. The `null` row (`''`) has no counterpart: the callers pass a `double`, and a caller with no value skips formatting.
- **UTL-I18N-06** — MUST. Formatters are cached for the page lifetime under the key `JSON.stringify({ locale: stringifyLocale(locale), options })`; equal keys return the same formatter instance, different locales (including `Intl.Locale` objects) different instances.
  Source: `packages/utils/src/formatNumber.ts:3-17`. Test: [L1] `formatNumber.test.ts` "caches the formatter based on options", "caches different Intl.Locale objects separately".
  Port note 2026-10-21 (ADR 01 decision 2 as amended 2026-10-18; ADR 08 Amendments 2026-10-21): not applicable. `double.ToString(format, culture)` builds no formatter object, so there is nothing to cache and no key to compute; equal `(culture, format)` pairs give equal text by construction (L1 `NumberFormattingTests` "gives equal text for equal culture and format pairs").
- **UTL-I18N-07** — MUST. `stringifyLocale(locale)`: an array is mapped recursively and joined with `,`; `null`/`undefined` → `''`; anything else → `String(locale)` (so an `Intl.Locale` yields its tag).
  Source: `packages/utils/src/stringifyLocale.ts:1-11`. Test: [L1] `stringifyLocale.test.ts` "stringifies Intl locale arguments for cache keys".
  Port note 2026-10-21 (ADR 01 decision 2 as amended 2026-10-18; ADR 08 Amendments 2026-10-21): not applicable. The port takes a `CultureInfo?` (`null` meaning `CultureInfo.CurrentCulture`), never a locale string or an array of them, so there is no locale to stringify.

#### 4.15.3 Locale filter (`getFilter`)

- **UTL-I18N-08** — MUST. `getFilter({ locale, ...collatorOptions })` builds an `Intl.Collator(locale, { usage: 'search', sensitivity: 'base', ignorePunctuation: true, ...collatorOptions })` (caller options override the defaults) and returns a filter object cached under the key `` `${stringifyLocale(locale)}|${JSON.stringify(collatorOptions)}` ``; equivalent inputs (`'fr-FR'` vs `new Intl.Locale('fr-FR')`) share one filter.
  Source: `internals/filter.ts:4-23,64-65`. Test: [L1] `filter.test.ts` "caches different locales separately", "caches equivalent locale inputs".
- **UTL-I18N-09** — MUST. `contains(item, query, itemToString?)` is `true` iff some window of `query.length` code units in the item string compares equal (`collator.compare(...) === 0`) to `query`; with the default options this ignores case, diacritics and punctuation (`'Résumé'` contains `'resume'`). (UTL-ALG-05.)
  Source: `internals/filter.ts:25-39`. Test: [L1] `filter.test.ts` "matches locale-aware substrings without accent sensitivity".
- **UTL-I18N-10** — MUST. `startsWith(item, query, itemToString?)` is `true` iff the first `query.length` code units of the item string compare equal to `query`.
  Source: `internals/filter.ts:40-48`. Test: none.
- **UTL-I18N-11** — MUST. `endsWith(item, query, itemToString?)` is `true` iff the item string is at least `query.length` long and its last `query.length` code units compare equal to `query`.
  Source: `internals/filter.ts:49-61`. Test: none.
- **UTL-I18N-12** — MUST. For all three predicates an empty query matches every item.
  Source: `internals/filter.ts:26-28,41-43,50-52`. Test: none.
- **UTL-I18N-13** — MUST. The item string is `stringifyAsLabel(item, itemToString)`: `itemToString(item)` (or `''` if it returns nullish) when supplied and the item is non-null; else an object's non-null `label` stringified, else its `value` stringified when present; else `serializeValue(item)` (`''` for nullish, the string itself, else JSON, falling back to `String`).
  Source: `internals/resolveValueLabel.tsx:68-81`; `internals/serializeValue.ts:1-13`. Test: none.
- **UTL-I18N-14** — MUST. `platform.mediaQuery.iOS` is the constant string `@supports (-webkit-touch-callout: none)`, usable in CSS to target iOS/iPadOS WebKit.
  Source: `packages/utils/src/platform/media-query.ts:4`. Test: none.

### 4.16 EDGE — Platform detection (`platform.os`, `platform.engine`, `platform.screenReader`, `platform.env`)

All flags are computed once when the module loads, from lower-cased `navigator.userAgent`, lower-cased `navigator.platform` and `navigator.maxTouchPoints`; they never change at runtime and are all `false` without a `navigator` (server). Transcribed as UTL-ALG-11. The flags gate behavior in: `useScrollLock` (ios, webkit), `useInitialLiveRegionTextMutation` (ios), `getPseudoElementBounds` (jsdom), `ComboboxInput`, `DrawerRoot`, `FloatingFocusManager`, `useDismiss`, `useFocus`, `useListNavigation`, `floating-ui-react/utils/{element,event}`, `useMenuItemCommonProps`, `MenuStore`, `MenuSubmenuTrigger`, `NumberFieldRoot`, `NumberFieldScrubArea(Cursor)`, `OTPFieldInput`, `ScrollAreaViewport`, `SelectPopup`, `FocusGuard`, `useOpenInteractionType`. The component documents state *which* flag they branch on; this section defines what each flag means.

- **UTL-EDGE-01** — MUST. `platform.engine.webkit` is `true` iff `CSS.supports('-webkit-backdrop-filter:none')` is `true` (WebKit: Safari and every iOS browser; excludes Blink).
  Source: `packages/utils/src/platform/engine.ts:7-8`. Test: none.
- **UTL-EDGE-02** — MUST. `platform.engine.gecko` is `true` iff not webkit and the user agent contains `firefox`.
  Source: `packages/utils/src/platform/engine.ts:15`. Test: none.
- **UTL-EDGE-03** — MUST. `platform.engine.blink` is `true` iff not webkit and the user agent contains `chrom` (Chrome, Chromium, Edge, Opera, Brave …). The three engine flags are mutually exclusive.
  Source: `packages/utils/src/platform/engine.ts:22`. Test: none.
- **UTL-EDGE-04** — MUST. `platform.os.ios` is `true` iff the platform string matches `/^i(os$|p)/` (iOS, iPhone, iPad, iPod) **or** equals `macintel` with `maxTouchPoints > 1` (iPadOS 13+ masquerading as macOS).
  Source: `packages/utils/src/platform/os.ts:7-8`. Test: none. Docs: CHANGELOG.md v1.0.0-alpha.6 "Fix iPad detection when applying scroll lock (#1342)".
- **UTL-EDGE-05** — MUST. `platform.os.android` is `true` iff the platform string equals `android` or the user agent contains `android`.
  Source: `packages/utils/src/platform/os.ts:11-12`. Test: none.
- **UTL-EDGE-06** — MUST. `platform.os.mac` is `true` iff not ios and the platform string starts with `mac`; `platform.os.windows` iff it starts with `win`; `platform.os.linux` iff not android and it matches `/^(linux|chrome os)/`; `platform.os.apple` = `mac || ios`.
  Source: `packages/utils/src/platform/os.ts:15-24`. Test: none.
- **UTL-EDGE-07** — MUST. `platform.screenReader.voiceOver` equals `platform.os.apple` — it means "VoiceOver *may* be running", never that it is.
  Source: `packages/utils/src/platform/screen-reader.ts:12`. Test: none.
- **UTL-EDGE-08** — MUST. `platform.env.jsdom` is `true` iff the user agent matches `/jsdom|happydom/` (unit-test DOMs); `@base-ui/utils/testUtils` `isJSDOM` is the stricter `/jsdom/` test on `window.navigator.userAgent`.
  Source: `packages/utils/src/platform/env.ts:4`; `packages/utils/src/testUtils.ts:4`. Test: none.
- **UTL-EDGE-09** — SHOULD. In development builds, when `navigator.userAgentData.brands` exists, the "user agent" string used for the substring checks is the brands joined as `brand/version` separated by spaces and the platform is `userAgentData.platform` (avoids deprecation warnings); production builds always read `navigator.userAgent`/`navigator.platform`. The production path is the normative one.
  Source: `packages/utils/src/platform/shared.ts:23-46`. Test: none.

### 4.17 EDGE — Logging, ids, error formatting, small pure helpers, constants

- **UTL-EDGE-10** — MUST. `generateId(prefix)` returns `` `${prefix}-${r}-${n}` `` where `r` is four characters of `Math.random().toString(36).slice(2, 6)` and `n` is a process-wide counter incremented before use (first call yields `1`). Used by toast ids.
  Source: `packages/utils/src/generateId.ts:1-5`. Test: none.
- **UTL-EDGE-11** — MUST. `createLogOnce(severity, prefix?)` returns a development-only logger: it joins its string arguments with a single space, prepends `` `${prefix}: ` `` when a prefix was given, and writes the result through `console.warn` or `console.error` **at most once per unique `severity:output` string** for the page lifetime; in production it does nothing.
  Source: `packages/utils/src/createLogOnce.ts:1-23`. Test: [L1] `createLogOnce.test.ts` "creates a logger with a custom prefix", "creates a logger without a prefix", "joins multiple messages with a space", "deduplicates each severity independently", "logs again after reset".
- **UTL-EDGE-12** — MUST. `warn` and `error` are the loggers `createLogOnce('warn', 'Base UI')` and `createLogOnce('error', 'Base UI')`; every development diagnostic quoted in this specification set that goes through them therefore appears as `Base UI: <message>` and fires once per unique message text. `reset()` clears the seen set (tests only).
  Source: `packages/utils/src/warn.ts:3`; `packages/utils/src/error.ts:3`; `packages/utils/src/createLogOnce.ts:25-27`. Test: [L1] `createLogOnce.test.ts` "logs again after reset", "deduplicates each severity independently".
- **UTL-EDGE-13** — MUST. `formatErrorMessage(code, ...args)` (production error minification) returns `` `Base UI error #${code}; visit ${url} for the full message.` `` where `url` is `https://base-ui.com/production-error` with `code=<code>` and one `args[]=<arg>` query parameter per argument, URL-encoded (`+` for spaces, `%5B%5D` for the brackets). `createFormatErrorMessage(baseUrl, prefix)` produces the same shape for another base URL/prefix.
  Source: `packages/utils/src/formatErrorMessage.ts:7-31`. Test: [L1] `formatErrorMessage.test.ts` "formats error message with code only", "formats error message with code and args", "creates a formatter with custom URL and prefix", "creates a formatter that handles args correctly", "handles special characters in args".
- **UTL-EDGE-14** — MUST. `clamp(value, min = Number.MIN_SAFE_INTEGER, max = Number.MAX_SAFE_INTEGER) = max(min, min(value, max))`.
  Source: `packages/utils/src/clamp.ts:1-7`. Test: [L1] `clamp.test.ts` "clamps a value based on min and max".
- **UTL-EDGE-15** — MUST. `areArraysEqual(a, b, comparer = Object.is)` is `false` when lengths differ, otherwise `true` iff `comparer(a[i], b[i])` for every index; the comparer is consulted even when `a` and `b` are the same array. Default semantics: `NaN` equals `NaN`, `0` differs from `-0`, objects compare by reference.
  Source: `packages/utils/src/areArraysEqual.ts:9-27`. Test: [L1] `areArraysEqual.test.ts` "returns true for arrays with the same elements in the same order", "returns false when elements differ", "returns false when lengths differ", "returns true for the same array reference", "returns true for two empty arrays", "treats NaN as equal to NaN (Object.is semantics)", "treats 0 as different from -0 (Object.is semantics)", "compares by reference by default", "uses the provided comparer", "lets the provided comparer override Object.is semantics", "consults the provided comparer even for the same array reference".
- **UTL-EDGE-16** — MUST. `mergeObjects(a, b)`: `a` alone → `a` (same reference); `b` alone → `b`; both → `{ ...a, ...b }`; neither → `undefined`. This is the same function RND-ALG-06 specifies (as `shallowMerge`); RND-ALG-06 is normative and this ID only indexes it under UTL.
  Source: `packages/utils/src/mergeObjects.ts:1-15`; see RND-ALG-06. Test: see RND-ALG-06.
- **UTL-EDGE-17** — MUST. `fastObjectShallowCompare(a, b)`: `true` for the same reference; `false` if either is not an object; otherwise `true` iff every key of `a` exists in `b` with an `Object.is`-equal value and `b` has no additional keys.
  Source: `packages/utils/src/fastObjectShallowCompare.ts:4-32`. Test: none.
- **UTL-EDGE-18** — MUST. `EMPTY_ARRAY` and `EMPTY_OBJECT` are frozen shared singletons (writes through them throw in strict mode); `NOOP` is a function that does nothing and returns `undefined`.
  Source: `packages/utils/src/empty.ts:1-8`; `internals/noop.ts:1`. Test: [L1] `empty.test.ts` "keeps the shared EMPTY_ARRAY frozen", "keeps the shared EMPTY_OBJECT frozen".
- **UTL-EDGE-19** — MUST. The shared constants are: `TYPEAHEAD_RESET_MS = 500`, `PATIENT_CLICK_THRESHOLD = 500`, `DISABLED_TRANSITIONS_STYLE = { style: { transition: 'none' } }`, `CLICK_TRIGGER_IDENTIFIER = 'data-base-ui-click-trigger'`, `BASE_UI_SWIPE_IGNORE_ATTRIBUTE = 'data-base-ui-swipe-ignore'`, `LEGACY_SWIPE_IGNORE_ATTRIBUTE = 'data-swipe-ignore'` (with the corresponding `[attr]` selectors), `DROPDOWN_COLLISION_AVOIDANCE = { fallbackAxisSide: 'none' }`, `POPUP_COLLISION_AVOIDANCE = { fallbackAxisSide: 'end' }`. Their meaning is defined where they are consumed (CMP typeahead, PSH, UBT, FLT, DRW/TST swipe).
  Source: `internals/constants.ts:3-28`. Test: none.
- **UTL-EDGE-20** — MUST. `isHTMLElement(value)` is a cross-realm check (`value instanceof value.ownerWindow.HTMLElement`), re-exported from `@floating-ui/utils/dom`.
  Source: `packages/utils/src/isHTMLElement.ts:5`. Test: none.
- **UTL-EDGE-21** — MAY. The deprecated `packages/utils/src/isMouseWithinBounds(event)` (distinct from the `utils/getPseudoElementBounds.ts` function of the same name) reports whether `clientX/Y` lie strictly inside `currentTarget`'s bounding rect shrunk by 1 px on each side; it has no consumer at v1.9.0 and need not be ported.
  Source: `packages/utils/src/isMouseWithinBounds.ts:4-20`. Test: none.

## 5. Algorithms

Pseudo-code is language-neutral. `≡` means "is the same reference as"; `Object.is` is the SameValue comparison (NaN equals NaN, +0 differs from −0).

- **UTL-ALG-01** — MUST. The implementation reproduces the following procedure exactly: Scroll-lock acquisition and release (global, reference-counted, deferred).
  Source: `packages/utils/src/useScrollLock.ts:228-319`. Test: none.

```
global lockCount = 0, restore = null, timeoutLock, timeoutUnlock   // one per process

acquire(referenceElement):
  lockCount += 1
  if lockCount == 1 and restore == null:
    timeoutLock.start(0 ms, () => lock(referenceElement))        // UTL-EVT-01 semantics
  return release

release():
  lockCount -= 1
  if lockCount == 0 and restore != null:
    timeoutUnlock.start(0 ms, unlock)

unlock():
  if lockCount == 0 and restore != null:
    restore(); restore = null

lock(referenceElement):
  if lockCount == 0 or restore != null: return                   // released meanwhile / already locked
  doc  = ownerDocument(referenceElement); html = doc.documentElement; body = doc.body; win = ownerWindow(html)
  if isPageScrollLocked(win, html, body):                        // computed overflowY of viewportScroller ~ /hidden|clip/
    observer = MutationObserver(() =>
      if isPageScrollLocked(...): return
      observer.disconnect(); restore = null; lock(referenceElement))
    observer.observe(html, {attributes: true}); observer.observe(body, {attributes: true})
    restore = () => observer.disconnect()
    return
  overlay = platform.os.ios or not hasInsetScrollbars(referenceElement)   // innerWidth − html.clientWidth > 0 ⇒ inset
  restore = overlay ? preventScrollOverlayScrollbars(referenceElement)    // UTL-DOM-05
                    : preventScrollInsetScrollbars(referenceElement)      // UTL-ALG-02

viewportScroller(html, body) = isOverflowElement(html) ? html : body     // UTL-DOM-04
```

- **UTL-ALG-02** — MUST. The implementation reproduces the following procedure exactly: Inset-scrollbar lock (`preventScrollInsetScrollbars`): styles written, restoration, resize handling.
  Source: `packages/utils/src/useScrollLock.ts:89-226`. Test: none.

```
preventScrollInsetScrollbars(referenceElement):
  doc, html, body, win as above
  if platform.engine.webkit and (win.visualViewport?.scale ?? 1) != 1: return noop   // Safari pinch-zoom
  scrollTop = scrollLeft = 0; gutterOnly = false; resizeFrame = AnimationFrame()

  lockScroll():
    // ---- reads ----
    htmlStyles = computedStyle(html); bodyStyles = computedStyle(body)
    gutterValue = (htmlStyles.scrollbarGutter contains 'both-edges') ? 'stable both-edges' : 'stable'
    scrollTop = html.scrollTop; scrollLeft = html.scrollLeft
    originalHtml = { scrollbarGutter, overflowY, overflowX } of html.style (inline)
    originalHtmlScrollBehavior = html.style.scrollBehavior
    originalBody = { position, height, width, boxSizing, overflowY, overflowX, scrollBehavior } of body.style
    scrollableY = html.scrollHeight > html.clientHeight;  scrollableX = html.scrollWidth > html.clientWidth
    constantY = htmlStyles.overflowY == 'scroll' or bodyStyles.overflowY == 'scroll'
    constantX = htmlStyles.overflowX == 'scroll' or bodyStyles.overflowX == 'scroll'
    scrollbarWidth  = max(0, win.innerWidth  − body.clientWidth)
    scrollbarHeight = max(0, win.innerHeight − body.clientHeight)
    marginY = parseFloat(bodyStyles.marginTop)  + parseFloat(bodyStyles.marginBottom)
    marginX = parseFloat(bodyStyles.marginLeft) + parseFloat(bodyStyles.marginRight)
    target = viewportScroller(html, body)
    gutterOnly = supportsStableScrollbarGutter(referenceElement)         // UTL-DOM-07 probe
    // ---- writes ----
    if gutterOnly:
      html.style.scrollbarGutter = gutterValue; target.style.overflowY = target.style.overflowX = 'hidden'
      return
    html.style := { scrollbarGutter: gutterValue, overflowY: 'hidden', overflowX: 'hidden' }
    if scrollableY or constantY: html.style.overflowY = 'scroll'
    if scrollableX or constantX: html.style.overflowX = 'scroll'
    body.style := {
      position: 'relative',
      height: (marginY or scrollbarHeight) ? `calc(100dvh - ${marginY + scrollbarHeight}px)` : '100dvh',
      width:  (marginX or scrollbarWidth)  ? `calc(100vw - ${marginX + scrollbarWidth}px)`   : '100vw',
      boxSizing: 'border-box', overflowY: 'hidden', overflowX: 'hidden', scrollBehavior: 'unset' }
    body.scrollTop = scrollTop; body.scrollLeft = scrollLeft
    html.setAttribute('data-base-ui-scroll-locked', '')
    html.style.scrollBehavior = 'unset'

  cleanup():
    assign originalHtml back onto html.style; assign originalBody back onto body.style
    if not gutterOnly:
      html.scrollTop = scrollTop; html.scrollLeft = scrollLeft
      html.removeAttribute('data-base-ui-scroll-locked'); html.style.scrollBehavior = originalHtmlScrollBehavior

  lockScroll()
  off = addEventListener(win, 'resize', () => { cleanup(); resizeFrame.request(lockScroll) })
  return () => { resizeFrame.cancel(); cleanup(); off() }
```

- **UTL-ALG-03** — MUST. The implementation reproduces the following procedure exactly: Store write with recursion guard (notification order and early exit).
  Source: `packages/utils/src/store/Store.ts:65-82`. Test: [L1] `Store.test.ts` "a nested setState from a listener stops the outer notification pass".

```
setState(next):
  if next ≡ state: return
  state = next; updateTick += 1; myTick = updateTick
  for listener in listeners (insertion order, live iteration of the set):
    if myTick != updateTick: return         // a nested write already notified everyone with newer state
    listener(next)
```

- **UTL-ALG-04** — MUST. The implementation reproduces the following procedure exactly: Memoized selector (`createSelectorMemoized`): per-state-record cache plus last-call memoization.
  Source: `packages/utils/src/store/createSelectorMemoized.ts:59-155`. Test: [L1] `createSelectorMemoized.test.ts`.

```
make(inputs...):                                  // inputs = s1..sN, combiner   (N ≥ 0)
  combiner = last(inputs)
  selectors = (len(inputs) == 1) ? [identity, combiner] : inputs
  N = len(selectors) − 1
  argsLength = combiner.paramCount == 0 ? 0 : combiner.paramCount − N
  if argsLength < 0 or argsLength > 3: throw 'Unsupported number of arguments'
  cache = WeakMap<cacheKey, memo>; nextId = 1

  return selector(state, a1, a2, a3):
    key = state.__cacheKey__ ?? (state.__cacheKey__ = {id: nextId++})   // own property; shallow copies inherit it
    memo = cache[key] ?? (cache[key] = buildMemo(selectors, combiner, argsLength))
    memo.args[0..argsLength) = (a1, a2, a3)[0..argsLength)
    return memo.evaluate(state, a1.., up to argsLength args)

buildMemo: a two-level memo —
  level 1 (argument memo): if every call argument (state, a1..) is reference-identical to the last call, return the last result without running anything;
  level 2 (combiner memo, single entry, equality Object.is): run s1..sN on (state, a1..a3) and read the a_i getters; if every one of the N + argsLength inputs is Object.is-equal to the inputs of the last combiner run, return the previous result; otherwise run combiner(s1.., a1..) and cache the result.
  With a resultEqualityCheck option: if the new result is "equal" to the previous one, return the previous reference.
```

Consequence: with the single-function form the only input is the state itself, so any new record re-runs the combiner and yields a new reference (`createSelectorMemoized.ts:159-168` note).

- **UTL-ALG-05** — MUST. The implementation reproduces the following procedure exactly: Locale filter matching.
  Source: `internals/filter.ts:6-66`. Test: [L1] `filter.test.ts` "matches locale-aware substrings without accent sensitivity".

```
collator = Intl.Collator(locale, { usage:'search', sensitivity:'base', ignorePunctuation:true, ...callerOptions })
eq(a, b) = collator.compare(a, b) == 0

contains(item, query, itemToString):
  if query == '': return true
  s = stringifyAsLabel(item, itemToString)                       // UTL-I18N-13
  for i in 0 .. s.length − query.length (inclusive):             // code-unit windows, fixed width = query.length
    if eq(s.slice(i, i + query.length), query): return true
  return false

startsWith(item, query, itemToString):
  if query == '': return true
  s = stringifyAsLabel(item, itemToString); return eq(s.slice(0, query.length), query)

endsWith(item, query, itemToString):
  if query == '': return true
  s = stringifyAsLabel(item, itemToString)
  return s.length >= query.length and eq(s.slice(s.length − query.length), query)
```

Note: windows are measured in UTF-16 code units of the *query*, so a match whose normalized length differs from the query's (e.g. ligatures expanding under the collator) is not found; the port must keep this window rule rather than "improve" it.

- **UTL-ALG-06** — MUST. The implementation reproduces the following procedure exactly: Item equality and selection-index resolution (Combobox/Autocomplete/Select).
  Source: `internals/itemEquality.ts:9-134`. Test: [L1] `itemEquality.test.ts` "findSelectionIndex" group: "anchors to the first selected item in rendered order, not value order", "returns null when nothing in the value array is rendered", "treats an array as a single value outside multiple mode", "anchors to the first selected item with a custom comparer", "reads the selected values once instead of rescanning them for every item", "keeps `+0` and `-0` distinct, like `Object.is`", "matches `NaN` and `null` against themselves", "never matches an `undefined` item or an `undefined` selected value", "never matches a hole left by an unmounted item"; "resolveSelectedIndex" group: "does not claim an unselected item", "claims when no item holds the index yet", "claims from a later holder", "leaves the index with an earlier selected holder", "takes over from an earlier holder that is no longer selected", "takes over when the earlier holder has left the registry".

```
defaultItemEquality(a, b) = Object.is(a, b)             // compared by identity below; never wrap it

compareItemEquality(itemValue, selectedValue, comparer):
  if itemValue is null/undefined or selectedValue is null/undefined: return Object.is(itemValue, selectedValue)
  return comparer(itemValue, selectedValue)

isSelectedValueDirty(current, initial, comparer):
  if both arrays: return not areArraysEqual(current, initial, (x, y) => compareItemEquality(x, y, comparer))
  return current !== initial

selectedValueIncludes(selectedValues, itemValue, comparer):
  if selectedValues is null/undefined: return false
  return exists v in selectedValues with v !== undefined and compareItemEquality(itemValue, v, comparer)

findItemIndex(itemValues, selectedValue, comparer):
  if itemValues is null/undefined: return −1
  return first index i with itemValues[i] !== undefined and compareItemEquality(itemValues[i], selectedValue, comparer), else −1

matcher(selectedValues, comparer):                       // used only in multiple mode
  if comparer is not defaultItemEquality (by identity): return item => selectedValueIncludes(selectedValues, item, comparer)
  index = Set(selectedValues) minus undefined
  return item => index.has(item) and (item !== 0 or exists v in selectedValues with Object.is(item, v))   // keep +0/−0 distinct

findSelectionIndex(itemValues, selectedValue, comparer, multiple):
  i = (multiple and isArray(selectedValue)) ? first index in itemValues matching matcher(selectedValue, comparer)
                                            : findItemIndex(itemValues, selectedValue, comparer)
  return i == −1 ? null : i                              // anchored to the first selected item in rendered order

resolveSelectedIndex(index, itemValue, registry, selectedValues, comparer, currentIndex):
  // called as items register/change; returns the new "selected index" anchor
  if selectedValueIncludes(selectedValues, itemValue, comparer):
    if currentIndex != null and index > currentIndex and selectedValueIncludes(selectedValues, registry[currentIndex], comparer):
      return currentIndex                                 // an earlier item still holds the anchor
    return index
  if index == currentIndex: return findSelectionIndex(registry, selectedValues, comparer, true)  // holder deselected: re-elect
  return currentIndex

removeItem(selectedValues, itemValue, comparer) = selectedValues.filter(v => not compareItemEquality(itemValue, v, comparer))
```

Sparse arrays (holes left by unmounted items) read as `undefined` and therefore never match.

- **UTL-ALG-07** — MUST. The implementation reproduces the following procedure exactly: Label resolution for a selected value (Select/Combobox value display).
  Source: `internals/resolveValueLabel.tsx:18-158`. Test: [L1] `resolveValueLabel.test.ts` "classifies %s" (4 variants: undefined, non-array and array `items` field, list starting with a flat item), "resolves a flat item label when the item has an unrelated items field"; "hasNullItemLabel" group: "returns true when grouped items contain a null-valued item with a label", "returns false when grouped items contain a null-valued item without a label", "returns false when grouped items do not contain a null-valued item", "supports grouped items with custom heading keys", "returns true when flat items contain a null-valued item with a label", "returns false when flat items do not contain a null-valued item", "returns false when items is a Record without a "null" key", "returns true when items is a Record with a "null" key", "returns false when items is undefined"; "record items with prototype member names" group: "falls back to the stringified label when the value matches an Object.prototype member", "resolves an own key that matches an Object.prototype member", "keeps resolving the null placeholder key in a record", "falls back to the stringified label when an own key has a nullish label".

```
isGroup(x)          = x is object and isArray(x.items)
isGroupedItems(items) = isGroup(items?[0])                 // decided by the FIRST entry only
flattenLeafItems(items) = isGroupedItems(items) ? concat(group.items for group in items) : items

hasNullItemLabel(items):
  if items is not an array: return items != null and 'null' in items      // record form: has key "null"
  leaves = isGroupedItems(items) ? all group.items : items
  return exists item in leaves with item and item.value == null and item.label != null

stringifyAsLabel(item, toLabel):                           // UTL-I18N-13
stringifyAsValue(item, toValue):
  if toValue and item != null: return toValue(item) ?? ''
  if item is object with both 'value' and 'label' keys: return serializeValue(item.value)
  return serializeValue(item)

resolveSelectedLabel(value, items, toLabel):
  fallback() = stringifyAsLabel(value, toLabel)
  if toLabel and value != null: return toLabel(value)
  if value is object and 'label' in value and value.label != null: return value.label
  if items is a non-array object:                           // Record<string, label>
    label = hasOwn(items, value) ? items[value] : undefined  // own keys only: 'constructor', '__proto__' … do not resolve
    return label ?? fallback()
  if items is an array:
    leaves = flattenLeafItems(items)
    if value == null or value is not an object:
      m = first leaf with leaf.value === value; return (m and m.label != null) ? m.label : fallback()
    if 'value' in value:
      m = first leaf with leaf and leaf.value === value.value; if m and m.label != null: return m.label
  return fallback()

resolveMultipleLabels(values, items, toLabel) = labels of each value joined by the literal ', '
```

- **UTL-ALG-08** — MUST. The implementation reproduces the following procedure exactly: Development serialization used to detect a changed `default` (no false positives for NaN, arrays, elements, functions, bigint, cycles).
  Source: `packages/utils/src/useControlled.ts:95-126`. Test: [L1] `useControlled.test.tsx` "does not warn when NaN", "does not warn when an array", "does not throw when defaultValue has React elements", "does not throw when defaultValue has function", "does not throw when defaultValue has bigint", "should warn only when defaultValue has functions/components and changes", "should not fail on null values".

```
serializeToDevModeString(input):
  seen = WeakMap<object, id>; nextId = 0
  try:
    result = JSON.stringify(input, replacer(key, value):
      if key == '_owner' and holder is an object with a '$$typeof' key: return undefined    // drop React element owner
      if typeof value == 'bigint': return `__bigint__:${value}`
      if value is a non-null object:
        if seen has value: return `__object__:${seen[value]}`                              // shared or cyclic reference
        seen[value] = nextId++
      return value)                                                                           // functions/symbols/undefined vanish per JSON rules; NaN → null
    return result ?? `__top__:${typeof input}`                                               // top-level undefined/function
  catch: return '__unserializable__'
```

Two defaults are "the same" iff their serializations are string-equal; `null` (`"null"`) and `undefined` (`"__top__:undefined"`) differ.

- **UTL-ALG-09** — MUST. The implementation reproduces the following procedure exactly: Shared animation-frame scheduler.
  Source: `packages/utils/src/useAnimationFrame.ts:15-84`. Test: [L3] `useAnimationFrame.test.ts` "keeps callback accounting correct when a frame is canceled more than once".

```
callbacks = []; count = 0; nextId = 1; startId = 1; scheduled = false

request(fn):
  id = nextId++; callbacks.push(fn); count += 1
  if not scheduled: nativeRequestAnimationFrame(tick); scheduled = true
     // development only: also re-request when the global requestAnimationFrame function was replaced
     // since the last request (fake timers installed/removed), so a stale `scheduled` flag cannot strand callbacks
  return id

cancel(id):
  i = id − startId
  if i < 0 or i ≥ callbacks.length or callbacks[i] == null: return
  callbacks[i] = null; count −= 1

tick(timestamp):
  scheduled = false
  current = callbacks; currentCount = count
  callbacks = []; count = 0; startId = nextId          // reset BEFORE running so re-requests land in the next frame
  if currentCount > 0: for fn in current: if fn != null: fn(timestamp)
```

- **UTL-ALG-10** — MUST. The implementation reproduces the following procedure exactly: Request-queue processing.
  Source: `internals/RequestQueue.ts:47-111`. Test: [L1] `RequestQueue.test.ts`.

```
process():
  if queued.size == 0 or pending.size ≥ maxConcurrent: return
  n = min(maxConcurrent − pending.size, queued.size)
  batch = first n entries of queued (insertion order)
  for (id, key) in batch:
    queued.delete(id); pending.set(id, key)
    promises.push(fetchFn(key).catch(() => pending.delete(id)))      // rejection frees the slot; resolution does not
  await all(promises)
  if queued.size > 0: await process()

queue(keys):    for key in keys: id = getKeyId(key); if not pending.has(id): queued.set(id, key);   await process()
setRequestSettled(key) / clearPendingRequest(key):   pending.delete(getKeyId(key)); await process()
clear():        queued.clear(); pending.clear()
status(key):    pending.has(id) ? 'pending' : queued.has(id) ? 'queued' : 'unknown'
```

- **UTL-ALG-11** — MUST. The implementation reproduces the following procedure exactly: Platform detection (evaluated once at load).
  Source: `packages/utils/src/platform/shared.ts:23-51`; `packages/utils/src/platform/engine.ts:7-22`; `packages/utils/src/platform/os.ts:7-24`; `packages/utils/src/platform/screen-reader.ts:12`; `packages/utils/src/platform/env.ts:4`; `packages/utils/src/platform/media-query.ts:4`. Test: none.

```
if navigator is undefined: ua = ''; plat = ''; touch = 0
else: ua = navigator.userAgent; plat = navigator.platform ?? ''; touch = navigator.maxTouchPoints ?? 0
   // development only, Chromium: ua = brands.map(b => `${b.brand}/${b.version}`).join(' '); plat = userAgentData.platform ?? plat
ua = lower(ua); plat = lower(plat)

engine.webkit = CSS exists and CSS.supports('-webkit-backdrop-filter:none')
engine.gecko  = not webkit and ua contains 'firefox'
engine.blink  = not webkit and ua contains 'chrom'
os.ios        = /^i(os$|p)/.test(plat) or (plat == 'macintel' and touch > 1)
os.android    = plat == 'android' or ua contains 'android'
os.mac        = not ios and plat startsWith 'mac'
os.windows    = plat startsWith 'win'
os.linux      = not android and /^(linux|chrome os)/.test(plat)
os.apple      = mac or ios
screenReader.voiceOver = apple
env.jsdom     = /jsdom|happydom/.test(ua)
mediaQuery.iOS = '@supports (-webkit-touch-callout: none)'
```

- **UTL-ALG-12** — MUST. The implementation reproduces the following procedure exactly: Scroll-edge normalization.
  Source: `utils/scrollEdges.ts:9-33`. Test: [L3] `scrollEdges.test.ts`.

```
normalizeScrollOffset(value, max):
  if max ≤ 0: return 0
  c = clamp(value, 0, max); start = c; end = max − c
  nearStart = start ≤ 1; nearEnd = end ≤ 1
  if nearStart and nearEnd: return start ≤ end ? 0 : max
  if nearStart: return 0
  if nearEnd: return max
  return c
```

- **UTL-ALG-13** — MUST. The implementation reproduces the following procedure exactly: Transform extraction.
  Source: `utils/getElementTransform.ts:10-33`. Test: none.

```
t = (computedStyle ?? computed(element)).transform
x = y = 0; scale = 1
if t and t != 'none':
  m = t.match(/matrix(?:3d)?\(([^)]+)\)/)
  if m: v = m[1].split(', ').map(parseFloat)
    if v.length == 6:  x = v[4]; y = v[5]; scale = sqrt(v[0]² + v[1]²)
    if v.length == 16: x = v[12]; y = v[13]; scale = v[0]
return { x, y, scale }
```

- **UTL-ALG-14** — MUST. The implementation reproduces the following procedure exactly: Pseudo-element-aware bounds.
  Source: `utils/getPseudoElementBounds.ts:31-69`. Test: [L3] `getPseudoElementBounds.test.ts` (L4 for its browser-only bounds test).

```
rect = element.getBoundingClientRect()
if platform.env.jsdom: return rect
before = computed(element, '::before'); after = computed(element, '::after')
if before.content == 'none' and after.content == 'none': return rect
bw = parseFloat(before.width) || 0; bh = parseFloat(before.height) || 0; aw = …after.width; ah = …after.height
totalW = max(rect.width, bw, aw); totalH = max(rect.height, bh, ah)
dw = totalW − rect.width; dh = totalH − rect.height
return { left: rect.left − dw/2, right: rect.right + dw/2, top: rect.top − dh/2, bottom: rect.bottom + dh/2 }
```

- **UTL-ALG-15** — MUST. The implementation reproduces the following procedure exactly: Media-query snapshot resolution.
  Source: `unstable-use-media-query/index.ts:10-49`. Test: [L3] `index.test.tsx`.

```
query = query.replace(/^@media( ?)/m, '')
matchMedia = options.matchMedia ?? (window.matchMedia if defined else null)
serverSnapshot = (noSsr and matchMedia) ? () => matchMedia(query).matches
               : (ssrMatchMedia != null) ? constant(ssrMatchMedia(query).matches)
               : constant(defaultMatches)
if matchMedia == null: clientSnapshot = constant(defaultMatches); subscribe = none
else: mql = matchMedia(query); clientSnapshot = () => mql.matches; subscribe = listen(mql, 'change')
value = externalStore(subscribe, clientSnapshot, serverSnapshot)     // server/hydration render uses serverSnapshot, then clientSnapshot
```

- **UTL-ALG-16** — MUST. The implementation reproduces the following procedure exactly: Popup label resolution.
  Source: `internals/resolvePopupLabel.ts:10-30`. Test: [L1] `resolvePopupLabel.test.tsx`.

```
rendered = unwrapLazyRenderProp(props.render); rp = isElement(rendered) ? rendered.props : undefined
ariaLabel      = props['aria-label']      ?? rp?['aria-label']
ariaLabelledBy = props['aria-labelledby'] ?? rp?['aria-labelledby']
if ariaLabelledBy == null and not ariaLabel:
  ariaLabelledBy = activeTriggerElement ? (activeTriggerElement.id || undefined) : (activeTriggerId || undefined)
return ariaLabelledBy
```

- **UTL-ALG-17** — MUST. The implementation reproduces the following procedure exactly: Composed-tree `closest` and `contains`.
  Source: `packages/utils/src/shadowDom.ts:13-70`. Test: [L3] `shadowDom.test.ts`.

```
closest(node, selector):
  cur = node
  while cur:
    if cur is Element and cur.matches(selector): return cur
    cur = cur.assignedSlot ?? cur.parentNode ?? (cur is ShadowRoot ? cur.host : null)
  return null

contains(parent, child):
  if not parent or not child: return false
  if parent.contains(child): return true
  if child.getRootNode() is ShadowRoot:
    n = child
    while n: if parent ≡ n: return true; n = n.parentNode ?? n.host
  return false
```

## 6. Configuration surface used by components

What component code passes into the mechanisms above. Component documents reference these rows from their §11.

| Mechanism | Option / parameter | Type | Default | Meaning |
|---|---|---|---|---|
| `useControlled` | `controlled` | `T \| undefined` | — | Consumer-owned value (`value`, `open`, `checked`, …). `undefined` at first render ⇒ component-owned mode. |
| `useControlled` | `default` | `T \| undefined` | — | Initial component-owned value (`defaultValue`, `defaultOpen`, …). |
| `useControlled` | `name` | `string` | — | Component name used in diagnostics (e.g. `'Switch'`, `'Select'`). |
| `useControlled` | `state` | `string` | `'value'` | State name used in diagnostics (e.g. `'open'`, `'checked'`, `'activeIndex'`). |
| `ReactStore` | constructor `(state, context, selectors)` | record, bag, map | `{}` context, no selectors | Initial record, non-reactive bag, named projections. |
| `ReactStore.useControlledProp` | `(key, controlled)` | key, value | — | Same contract as `useControlled` but for a store key; used by popup roots for `open`, Select/Combobox for `value`/`inputValue`, etc. |
| `ReactStore.useSyncedValue(s)` | `(key, value)` / `(record)` | — | — | Mirrors parameters (disabled, modal, element handles, callbacks) into the store after each render. |
| `useScrollLock` | `enabled` | `boolean` | `true` | Dialog: `open && modal === true`; anchored popups: PSH `useAnchoredPopupScrollLock` result. |
| `useScrollLock` | `referenceElement` | `Element \| null` | `null` | Element whose document is locked (the popup element). |
| `getFilter` | `locale` | `Intl.LocalesArgument` | runtime locale | Collation locale (Combobox/Autocomplete/Menu `useFilter({ locale })`). |
| `getFilter` | other `Intl.CollatorOptions` | — | `usage:'search'`, `sensitivity:'base'`, `ignorePunctuation:true` | Caller may override any collator option (e.g. `sensitivity: 'accent'`). |
| `formatNumber` | `locale`, `options` | `Intl.LocalesArgument`, `Intl.NumberFormatOptions` | runtime locale, `{}` | NumberField `locale`/`format`, Slider/Meter/Progress `locale`/`format`. |
| `useInitialLiveRegionTextMutation` | `present` | `boolean` | `true` | Whether the live-region element is currently rendered (Combobox status/empty regions). |
| `useMediaQuery` | `defaultMatches`, `matchMedia`, `noSsr`, `ssrMatchMedia` | see §3.3 | `false`, `window.matchMedia`, `false`, — | Public hook; no internal consumer. |
| `DirectionProvider` | `direction` | `'ltr' \| 'rtl'` | `'ltr'` | Read by Composite (arrow-key mapping), anchor positioning (logical sides), Combobox input/chips, Menu, Navigation Menu, OTP field, Scroll Area, Select popup, Slider control/thumb, popup viewport. |
| `CSPProvider` | `nonce`, `disableStyleElements` | `string`, `boolean` | —, `false` | Read by Scroll Area root, Select popup, PrehydrationScript (TRN). |
| `Timeout`/`AnimationFrame`/`Interval`/`IdleCallback` | `delay`, `fn` | ms, callback | — | Per-component schedulers; delays are stated in each component document. |
| `TimeoutManager` | `key`, `delay`, `fn` | — | — | Keyed timers (no consumer at 1.9.0). |
| `RequestQueue` | `fetchFn`, `maxConcurrentRequests`, `getKeyId` | — | —, `Infinity`, `String` | Concurrency-limited fetch queue (no consumer at 1.9.0). |
| `getPseudoElementBounds` / `isMouseWithinBounds` | tolerance | px | 5 | Menu/Select/Combobox item press-release drift tolerance. |
| `scrollable.*` | `allowOverflowIntent` | `boolean` | `false` | Drawer keyboard-slack scroll containers count as scrollable when sized on the axis. |
| `normalizeScrollOffset` | tolerance | px | 1 | Select/Scroll Area edge snapping. |
| constants | `TYPEAHEAD_RESET_MS` | ms | 500 | CMP typeahead buffer reset. |
| constants | `PATIENT_CLICK_THRESHOLD` | ms | 500 | PSH/UBT "patient click" window. |
| constants | `DISABLED_TRANSITIONS_STYLE` | style | `{ transition: 'none' }` | TRN instant-mount styles. |
| constants | `CLICK_TRIGGER_IDENTIFIER` | attribute name | `data-base-ui-click-trigger` | PSH trigger marker. |
| constants | `*_SWIPE_IGNORE_*` | attribute/selector | `data-base-ui-swipe-ignore`, `data-swipe-ignore` | DRW/TST swipe exclusion. |
| constants | `DROPDOWN_COLLISION_AVOIDANCE` / `POPUP_COLLISION_AVOIDANCE` | `{ fallbackAxisSide }` | `'none'` / `'end'` | FLT collision presets for list popups vs free popups. |

## 7. Edge cases and dev warnings

Messages are quoted verbatim. "Deduplicated" means emitted through `error`/`warn` (UTL-EDGE-11/12): once per unique text per page lifetime, development builds only.

- **UTL-EDGE-22** — MUST. When the controlled/uncontrolled mode of `useControlled` would change after the first render (`controlled` crosses between `undefined` and defined), a development error is emitted (deduplicated), with `${c}` = `''` if the component started consumer-owned else `'un'`, `${u}` = the opposite, `${state}` and `${name}` from the parameters:
  `Base UI: A component is changing the ${c}controlled ${state} state of ${name} to be ${u}controlled.` ⏎ `Elements should not switch from uncontrolled to controlled (or vice versa).` ⏎ `Decide between using a controlled or uncontrolled ${name} element for the lifetime of the component.` ⏎ `The nature of the state is determined during the first render. It's considered controlled if the value is not `undefined`.` ⏎ `More info: https://fb.me/react-controlled-components`
  Source: `packages/utils/src/useControlled.ts:48-64`. Test: [L2] `useControlled.test.tsx` "warns when switching from uncontrolled to controlled", "warns and falls back to the default when switching from controlled to uncontrolled".
- **UTL-EDGE-23** — MUST. In component-owned mode, when `default` on a later render serializes differently (UTL-ALG-08) from the `default` captured at first render, a development error is emitted (deduplicated):
  `Base UI: A component is changing the default ${state} state of an uncontrolled ${name} after being initialized. To suppress this warning opt to use a controlled ${name}.`
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: [L2] `useControlled.test.tsx` "warns when changed after initial rendering", "should warn only when defaultValue changes", "should not fail on null values".
- **UTL-EDGE-24** — MUST. UTL-EDGE-23 is never emitted in consumer-owned mode, and never for a `default` that serializes identically (including `NaN`, empty arrays, objects holding elements or functions, bigint). Because the comparison is always against the *first* default and the message text is constant per `name`/`state`, it fires at most once per component type/state regardless of how many times `default` changes.
  Source: `packages/utils/src/useControlled.ts:66-80,95-126`. Test: [L2] `useControlled.test.tsx` "does not warn when controlled", "does not warn when NaN", "does not warn when an array", "does not throw when defaultValue has React elements", "does not throw when defaultValue has function", "does not throw when defaultValue has bigint", "should warn only when defaultValue has functions/components and changes".
- **UTL-EDGE-25** — MUST. `ReactStore.useControlledProp` emits, **on every render** where the mode differs from the mode recorded at first use of that key (not deduplicated; written directly to `console.error`), with `${c}`/`${u}` as in UTL-EDGE-22 and `${key}` the state key:
  `A component is changing the ${c}controlled state of ${key} to be ${u}controlled. Elements should not switch from uncontrolled to controlled (or vice versa).`
  Source: `packages/utils/src/store/ReactStore.ts:133-147`. Test: [L2] `ReactStore.test.tsx` "warns on switching from uncontrolled to controlled", "warns on switching from controlled to uncontrolled" (two messages expected because the test renderer renders twice).
- **UTL-EDGE-26** — MUST. `ReactStore.useSyncedValues` emits (every render, not deduplicated) when the set or order of keys differs from the first render:
  `ReactStore.useSyncedValues expects the same prop keys on every render. Keys should be stable.`
  Source: `packages/utils/src/store/ReactStore.ts:93-106`. Test: [L2] `ReactStore.test.tsx` "warns if useSyncedValues keys change between renders".
- **UTL-EDGE-27** — SHOULD. Invoking a stable trampoline (`useStableCallback`, and therefore any store context callback or `useValueChanged` handler) during the render phase of its owner throws `Error('Base UI: Cannot call an event handler while rendering.')` in development builds; in production the call is silently ignored until the implementation has been attached after commit.
  Source: `packages/utils/src/useStableCallback.ts:35-62`. Test: none.
- **UTL-EDGE-28** — MUST. Scroll-lock release is tolerant of teardown: if the window no longer exposes `removeEventListener` (test environments torn down before the 0 ms unlock timer fires), the resize listener removal is skipped instead of throwing; style restoration still runs.
  Source: `packages/utils/src/useScrollLock.ts:216-225`. Test: none.

Other defensive behaviors already carried by requirements above: `formatNumber(null) → ''` (UTL-I18N-05); empty filter query matches everything (UTL-I18N-12); `getCssDimensions` treats non-numeric CSS sizes as 0 (UTL-DOM-23); `getElementAtPoint` returns `null` for roots without `elementFromPoint` (UTL-DOM-27); `TimeoutManager.clear` on unknown keys (UTL-EVT-09); double `AnimationFrame.cancel` (UTL-EVT-05); selector construction errors (UTL-STA-29, UTL-STA-32); `closest` on detached nodes (UTL-DOM-21); all platform flags `false` on the server (UTL-ALG-11); `useMediaQuery` without `matchMedia` (UTL-STA-38).

## 8. React-specific notes and porting considerations

Non-normative.

### 8.1 Reading the source

* **Timing vocabulary.** `useIsoLayoutEffect` = React `useLayoutEffect` in a browser and a no-op during server rendering (`packages/utils/src/useIsoLayoutEffect.ts:6`). "post-commit, pre-paint" in §4 maps to it; "after paint / on mount" maps to `useEffect`/`useOnMount` (`useOnMount.ts:8-13`, runs once, cleanup on removal). `useOnFirstRender(fn)` runs `fn` synchronously *during* the first render (`useOnFirstRender.ts:4-10`). A Blazor port has no exact analogue of layout effects; `OnAfterRender(firstRender)` runs after paint, so store syncing (`useSyncedValue*`, `useControlledProp`) and scroll locking should be done synchronously at the end of the parameter-set phase when they only touch the store, and in `OnAfterRender` when they touch the DOM. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4) and §Relaxation table (UTL-STA-19..22); decisions/05-state-store-and-event-details.md §1 (prop-to-store sync in `OnParametersSet`); decisions/04-js-interop-boundary.md §1 (`scroll-lock` module).
* **Per-instance objects.** `useRefWithInit(init, arg?)` creates a value once per component instance (`useRefWithInit.ts:15-23`); `useValueAsRef(value)` exposes the latest value through a handle updated after commit (`useValueAsRef.ts:10-30`); `useForcedRerendering()` returns a stable "re-render me" function (`useForcedRerendering.ts:7-13`). These are implementation vehicles; a port uses instance fields.
* **Stable callbacks.** `useStableCallback` (`useStableCallback.ts:35-52`) swaps the implementation in an insertion effect (or synchronously where that effect is missing/aliased, e.g. React 17 or Preact). The observable contract is UTL-STA-16/UTL-EDGE-27: stable identity, always-latest body, must not run during render. Port: see decisions/05-state-store-and-event-details.md §1 (store context bag of typed fields).
* **Double rendering.** The test renderer wraps components in StrictMode, so render-phase code runs twice; that is why `ReactStore` tests expect two copies of the non-deduplicated warnings while `useControlled` tests expect one (deduplicated). A port's test translation should not require the duplicate.
* **Version shims.** `safeReact.ts` clones the React namespace so optional APIs can be probed; `reactVersion.ts` exposes `isReactVersionAtLeast(17|18|19)`; `inertValue` and `useStore`'s legacy path exist only for React 17/18. None of these have a port equivalent.
* **Externals.** `@floating-ui/utils/dom` supplies `getWindow`, `isElement`, `isHTMLElement`, `isShadowRoot`, `isOverflowElement`, `getParentNode`, `isLastTraversableNode`, `getComputedStyle`; their semantics as used here are stated inline (UTL-DOM-04, UTL-DOM-29, UTL-EDGE-20). `reselect` supplies `lruMemoize`/`createSelectorCreator` for UTL-ALG-04. `use-sync-external-store` supplies the subscription primitive behind UTL-STA-25/26 and UTL-STA-39. Port: see decisions/03-positioning-engine.md §Decision (vendored floating-ui) and decisions/05-state-store-and-event-details.md §1 (part selector subscription).

### 8.2 Porting the store

* The record must be treated as immutable; every write produces a new record (UTL-STA-10/11). Readers that compare records by reference (`notifyAll`, selector caches) depend on it. Port: see decisions/05-state-store-and-event-details.md §1 (immutable record class, `Is<T>` equality matching `Object.is`).
* Listener iteration must be over a *live* ordered set so that a listener unsubscribing itself or others during a pass is honored, and the recursion guard (UTL-ALG-03) must be reproduced exactly: the nested pass completes first, then the outer pass aborts. Port: see decisions/05-state-store-and-event-details.md §1.
* Reactive reads (`useStore`) are implemented with React's external-store protocol: subscribe, `getSnapshot`, and a selector compared by `Object.is`. The React 19 "fast" path (`useStore.ts:79-179`) batches all reads in a `fastComponent` into one subscription whose snapshot is a tick counter incremented when any selected value changes. A port should subscribe per component, compute all projections on notification, and re-render only if any changed. Port: see decisions/05-state-store-and-event-details.md §1 (one subscription per part, `StateHasChanged` only on a changed selection; store cascaded fixed).
* `createSelectorMemoized` stamps a hidden `__cacheKey__` own property onto the state record (UTL-ALG-04). A port with a typed state record cannot do this; keying the cache on the record reference (with an explicit "derived from" link for shallow copies, or simply accepting a cache miss per new record) is an acceptable substitute because no component relies on the memoized factory at 1.9.0. Port: see decisions/05-state-store-and-event-details.md §1 (named selectors `Func<TState, TValue>` / `Func<TState, TArg, TValue>`).
* `NullStore` must stay inert even if a port adds new mutators to the base store (its source overrides each mutator for exactly this reason, `utils/NullStore.ts:16-25`). Port: see decisions/05-state-store-and-event-details.md §1 (inert fallback store, PSH-STA-34..40).

### 8.3 What a port's test harness must provide

The Base UI suite relies on the following helpers from `packages/react/test/` and `packages/utils/src/testUtils.ts`. A translated suite needs equivalents.

* **Controllable clock.** `createRenderer({ clockOptions })` with `clock.withFakeTimers()`, `clock.tick(ms)`, `clock.tickAsync(ms)`; `advanceReactClock(clock, ms)` = flush microtasks, advance `ms`, flush microtasks again (`advanceReactClock.ts:4-8`). Every `Timeout`/`Interval`/`IdleCallback` must run on the fake clock; animation frames must be flushable (`resetAnimationFrameScheduler()` drops frames leaked across tests, `useAnimationFrame.ts:96-107`). `wait(ms)` and `waitSingleFrame()` (`wait.ts`) wait on real time / one real frame. Port: see decisions/06-testing-harness.md §1 (C# timers under `TimeProvider`/`FakeTimeProvider` at L1) and §2 "Clocks" (`page.clock` for browser timers); decisions/01-hosting-model-and-timing.md §Decision items 3–4 (UTL-EVT-01..09 as C# timers when the result is state, JS timers when it is DOM; frames never emulated in C#, constraint C2).
* **Render wrapper.** `createRenderer().render(element)` returns `{ rerender(newElement), setProps(partial), unmount, container, … }`, each awaited inside an "act" boundary that flushes effects (`createRenderer.ts:26-48`). `setProps` clones the originally rendered element with new props — tests use it to flip controlled/uncontrolled values. Port: see decisions/06-testing-harness.md §1 (L2 bUnit).
* **Pointer sequences.** `enterWithMouse(el)` = `pointerenter(mouse)`, `mouseenter`, `mousemove`; `moveMouse(from, to)` = `pointerleave`/`mouseleave` on `from` with `relatedTarget: to`, then `pointerenter`/`mouseenter` on `to` with `relatedTarget: from`, then `mousemove`; `pressWithTouch(el)` = `pointerdown(touch)` then `mousedown` (`pointer.ts:3-21,67-70`). `firePointer.down/move/up(el, init)` **require** `init.timeStamp > 0` and stamp the event with it (throwing `firePointer: timeStamp must be greater than 0, received ${timeStamp}.` otherwise) so velocity-based logic (swipe dismissal) sees a fixed timeline (`pointer.ts:23-61`). `resetBrowserPointer()` unhovers the body between real-browser tests. Port: see decisions/06-testing-harness.md §2 "Pointer realism".
* **Clipboard.** `pasteText(target, value)` dispatches a bubbling, cancelable `paste` event whose `clipboardData.getData('text/plain')` returns `value` (`clipboard.ts:7-16`).
* **Form data capture.** `createFormDataSpy(name)` is an `onSubmit` handler that prevents submission and records `new FormData(form).get(name)` (`formData.ts:8-13`); see FFI.
* **Scroll-lock predicate.** `isScrollLocked(doc)` = `html.style.overflow === 'hidden'` or `html` has `data-base-ui-scroll-locked` or `body.style.overflow === 'hidden'` (`scrollLock.ts:4-10`). Note it reads the `overflow` shorthand, which is set when both `overflow-x` and `overflow-y` are `hidden`, so simple-mode and gutter-mode locks are detected through the shorthand and full-mode locks through the attribute. Port: see decisions/06-testing-harness.md §1 (scroll lock at L3; cross-boundary rows at L4).
* **Floating-ui interaction merging.** `useTestInteractions(propsList)` reproduces FLT's prop-getter merging for tests: `getFloatingProps` always adds `tabIndex: -1` and the focusable marker attribute; same-named event handlers from several sources are chained in list order (user props last) and the first non-`undefined` return value wins; item getters receive the `active`/`selected` flags and strip them from the output (`useTestInteractions.ts:30-161`).
* **Environment flags.** `isJSDOM` (`testUtils.ts:4`) gates browser-only assertions (visually-hidden overflow, SVG dimensions, pseudo-element bounds); `expectType<Expected, Actual>` is a compile-time type assertion. Port: see decisions/06-testing-harness.md §1 (L3 jsdom vs L4 browser).
* **Diagnostics.** Tests assert development messages with `toErrorDev`/`toWarnDev` matchers; `reset()` from `createLogOnce` clears the once-per-message set between tests. Port: see decisions/06-testing-harness.md §2 "Diagnostics".

### 8.4 Other porting notes

* `inertValue` (UTL-DOM-16) and `visuallyHidden*` are style/attribute constants; `isHTMLElement`/`isElement` must be cross-realm (iframes) if the port supports popups in other documents.
* Platform flags are evaluated once; a Blazor port can compute them once per circuit/session through JS interop and cache them. Port: see decisions/04-js-interop-boundary.md §4 (JS initializer computes the flags, UTL-ALG-11) and §1 (`dom` module for §4.10/§4.11/§4.16 helpers). Server rendering must treat them all as `false` (UTL-ALG-11) — notably `platform.os.ios` controls whether scroll lock uses simple mode (UTL-DOM-05) and whether the live-region marker is skipped (UTL-A11Y-04).
* `StoreInspector` is a dev tool with no behavioral contract (UTL-STA-28); skipping it is acceptable.
* `DirectionProvider` is a pure ancestor-provided value (UTL-I18N-03: no DOM read); positioning reads it from the C# side. Port: see decisions/03-positioning-engine.md §Decision (FLT-I18N-01..03 read the `DirectionProvider` value supplied by C#).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| UTL-STA-01 | MUST | Controlled mode decided once at first render (`controlled !== undefined`) | 4.1 |
| UTL-STA-02 | MUST | Uncontrolled initial value is `default` as supplied (functions stored, not called) | 4.1 |
| UTL-STA-03 | MUST | Controlled value tracks `controlled`; falls back to initial default when it becomes undefined | 4.1 |
| UTL-STA-04 | MUST | Setter writes only in uncontrolled mode | 4.1 |
| UTL-STA-05 | MUST | Later `default` changes never change the value | 4.1 |
| UTL-STA-06 | MUST | Setter identity is stable | 4.1 |
| UTL-STA-07 | MUST | One immutable record, synchronously readable after writes | 4.2.1 |
| UTL-STA-08 | MUST | `setState`: same-reference no-op; else replace and notify in order | 4.2.1 |
| UTL-STA-09 | MUST | Nested write aborts outer notification pass | 4.2.1 |
| UTL-STA-10 | MUST | `set(key, value)`: Object.is no-op; else shallow copy + notify | 4.2.1 |
| UTL-STA-11 | MUST | `update(changes)`: single merge/notify if any key differs | 4.2.1 |
| UTL-STA-12 | MUST | `notifyAll()` renews reference and notifies | 4.2.1 |
| UTL-STA-13 | MUST | `subscribe` returns unsubscribe; set semantics | 4.2.1 |
| UTL-STA-14 | MUST | `create()` constructs the called class, independent instances | 4.2.1 |
| UTL-STA-15 | MUST | `context` bag is non-reactive, default `{}` | 4.2.2 |
| UTL-STA-16 | MUST | `useContextCallback` stores a stable latest-implementation trampoline | 4.2.2 |
| UTL-STA-17 | MUST | `select(key, ...args)` synchronous, non-subscribing | 4.2.2 |
| UTL-STA-18 | MUST | `useState(key, ...args)` re-renders on Object.is change incl. arg changes | 4.2.2 |
| UTL-STA-19 | MUST | `useSyncedValue` sets after commit when `!==` | 4.2.2 |
| UTL-STA-20 | MUST | `useSyncedValueWithCleanup` also resets to undefined on cleanup | 4.2.2 |
| UTL-STA-21 | MUST | `useSyncedValues` updates only when an entry value changed | 4.2.2 |
| UTL-STA-22 | MUST | `useControlledProp` syncs defined controlled value after commit | 4.2.2 |
| UTL-STA-23 | MUST | `useStateSetter` returns a stable setter | 4.2.2 |
| UTL-STA-24 | MUST | `observe` fires immediately then on Object.is change; returns unsubscribe | 4.2.2 |
| UTL-STA-25 | MUST | `useStore` returns projection; the subscribing part re-renders only when it changes | 4.2.3 |
| UTL-STA-26 | MUST | Reactive reads never tear | 4.2.3 |
| UTL-STA-27 | SHOULD | Reads may be batched per store without observable difference | 4.2.3 |
| UTL-STA-28 | MAY | `StoreInspector` dev panel | 4.2.4 |
| UTL-STA-29 | MUST | `createSelector` arities, pass-through, errors | 4.3 |
| UTL-STA-30 | MUST | `createSelectorMemoized` caches per record, Object.is inputs | 4.3 |
| UTL-STA-31 | MUST | Single-function memoized form uses identity input | 4.3 |
| UTL-STA-32 | MUST | Extra-arg count from parameter count; 0–3 else throw | 4.3 |
| UTL-STA-33 | MUST | `createSelectorMemoizedWithOptions` option merging | 4.3 |
| UTL-STA-34 | MUST | `NullStore` writes are no-ops | 4.4 |
| UTL-STA-35 | MUST | `usePreviousValue` semantics (Object.is) | 4.4 |
| UTL-STA-36 | MUST | `useValueChanged` checks only on `Object.is` change, fires on strict inequality (`NaN → NaN` silent) | 4.4 |
| UTL-STA-37 | MUST | `useMediaQuery` strips leading `@media` | 4.5 |
| UTL-STA-38 | MUST | No `matchMedia` ⇒ `defaultMatches`, no subscription | 4.5 |
| UTL-STA-39 | MUST | Live `matches`, subscribe to `change`, unsubscribe on removal | 4.5 |
| UTL-STA-40 | MUST | Server snapshot order: noSsr live → ssrMatchMedia → defaultMatches | 4.5 |
| UTL-EVT-01 | MUST | `Timeout.start` replaces pending; idle before fn runs | 4.6 |
| UTL-EVT-02 | MUST | `Timeout.clear`/`isStarted` | 4.6 |
| UTL-EVT-03 | MUST | `useTimeout` one instance per component, cleared on removal | 4.6 |
| UTL-EVT-04 | MUST | `AnimationFrame` instance request/cancel; per-component cleanup | 4.6 |
| UTL-EVT-05 | MUST | Shared frame scheduler batching/order/cancel semantics | 4.6 |
| UTL-EVT-06 | MUST | `Interval` repeats until cleared | 4.6 |
| UTL-EVT-07 | MUST | `IdleCallback` with `requestIdleCallback` or 0 ms fallback | 4.6 |
| UTL-EVT-08 | MUST | `useIdleCallback` stable instance, cancelled on removal | 4.6 |
| UTL-EVT-09 | MUST | `TimeoutManager` keyed timers | 4.6 |
| UTL-EVT-10 | MUST | `RequestQueue` enqueue/dedupe/FIFO/concurrency | 4.7 |
| UTL-EVT-11 | MUST | `getRequestStatus` values | 4.7 |
| UTL-EVT-12 | MUST | Rejection frees slot; resolution waits for settle; clear | 4.7 |
| UTL-EVT-13 | MUST | `addEventListener` returns matching remover | 4.7 |
| UTL-EVT-14 | MUST | `mergeCleanups` order and skipping | 4.7 |
| UTL-DOM-01 | MUST | Scroll lock is global and reference-counted | 4.8 |
| UTL-DOM-02 | MUST | Lock and unlock deferred by 0 ms timer | 4.8 |
| UTL-DOM-03 | MUST | Already-locked page: observe attributes, take over later | 4.8 |
| UTL-DOM-04 | MUST | Viewport scroller = overflow html else body | 4.8 |
| UTL-DOM-05 | MUST | Simple mode (iOS or no inset scrollbar): overflow hidden on scroller only | 4.8 |
| UTL-DOM-06 | MUST | Safari pinch-zoom: no lock | 4.8 |
| UTL-DOM-07 | MUST | Gutter path: `scrollbar-gutter: stable` + overflow hidden | 4.8 |
| UTL-DOM-08 | MUST | Full path: exact html/body inline styles, offsets, attribute | 4.8 |
| UTL-DOM-09 | MUST | Full path restoration | 4.8 |
| UTL-DOM-10 | MUST | Re-lock on next frame after `resize` | 4.8 |
| UTL-DOM-11 | MUST | Targets the reference element's document (inset check reads the global window) | 4.8 |
| UTL-DOM-12 | MUST | Applied after commit of enabling render; released on disable/removal | 4.8 |
| UTL-DOM-13 | MUST | `visuallyHidden` exact CSS | 4.9 |
| UTL-DOM-14 | MUST | `visuallyHiddenInput` exact CSS | 4.9 |
| UTL-DOM-15 | MUST | `ownerVisuallyHidden` exact CSS | 4.9 |
| UTL-DOM-16 | MUST | `inert` attribute present/absent from boolean | 4.9 |
| UTL-DOM-17 | MUST | `isElementDisabled` rule | 4.9 |
| UTL-DOM-18 | MUST | `ownerDocument`/`ownerWindow` fallbacks | 4.10 |
| UTL-DOM-19 | MUST | `activeElement` descends shadow roots | 4.10 |
| UTL-DOM-20 | MUST | `contains` crosses shadow roots | 4.10 |
| UTL-DOM-21 | MUST | `closest` composed-tree walk; detached → null | 4.10 |
| UTL-DOM-22 | MUST | `getTarget` composed path first element, fallback target | 4.10 |
| UTL-DOM-23 | MUST | `getCssDimensions` CSS vs offset fallback | 4.11 |
| UTL-DOM-24 | MUST | `getElementTransform` from computed matrix | 4.11 |
| UTL-DOM-25 | MUST | `getPseudoElementBounds` extension; jsdom plain rect | 4.11 |
| UTL-DOM-26 | MUST | `isMouseWithinBounds` 5 px tolerance | 4.11 |
| UTL-DOM-27 | MUST | `getElementAtPoint` on document/shadow root | 4.11 |
| UTL-DOM-28 | MUST | `isScrollableY/X/isScrollable` rules | 4.11 |
| UTL-DOM-29 | MUST | `findScrollableAncestor`/`findScrollableTouchTarget` walk | 4.11 |
| UTL-DOM-30 | MUST | `normalizeScrollOffset`/`getMaxScrollOffset` | 4.11 |
| UTL-DOM-31 | MUST | `valueToPercent` formula | 4.11 |
| UTL-DOM-32 | MUST | Scrollbar-hiding `<style>` text, unconditional emitters, nonce | 4.12 |
| UTL-DOM-33 | MUST | `disableStyleElements` suppresses the `<style>` | 4.12 |
| UTL-DOM-34 | MUST | Nonce on pre-hydration `<script>`; not disableable | 4.12 |
| UTL-DOM-35 | MUST | Default CSP configuration; nearest provider wins | 4.12 |
| UTL-DOM-36 | MUST | Providers render no element | 4.12 |
| UTL-DOM-37 | MUST | Numbers in attributes/inline styles serialized as ECMAScript `Number::toString` | 4.12.1 |
| UTL-A11Y-01 | MUST | Word-joiner appended to last non-empty text node on appearance | 4.13 |
| UTL-A11Y-02 | MUST | Restored after 200 ms if still marked | 4.13 |
| UTL-A11Y-03 | MUST | Restored immediately on removal/`present` false | 4.13 |
| UTL-A11Y-04 | MUST | Skipped on iOS, unattached, or no text | 4.13 |
| UTL-ARIA-01 | MUST | `resolvePopupLabel` precedence (`aria-labelledby`) | 4.14 |
| UTL-I18N-01 | MUST | `useDirection` = nearest provider or `'ltr'` | 4.15.1 |
| UTL-I18N-02 | MUST | `direction` default `'ltr'`, values `'ltr'|'rtl'` | 4.15.1 |
| UTL-I18N-03 | MUST | Direction not read from `dir`/CSS | 4.15.1 |
| UTL-I18N-04 | MUST | Direction reaches portaled subtrees via state | 4.15.1 |
| UTL-I18N-05 | MUST | `formatNumber` null → '' else Intl.NumberFormat | 4.15.2 |
| UTL-I18N-06 | MUST | Formatter cache key | 4.15.2 |
| UTL-I18N-07 | MUST | `stringifyLocale` rules | 4.15.2 |
| UTL-I18N-08 | MUST | `getFilter` collator defaults and cache key | 4.15.3 |
| UTL-I18N-09 | MUST | `contains` window match | 4.15.3 |
| UTL-I18N-10 | MUST | `startsWith` | 4.15.3 |
| UTL-I18N-11 | MUST | `endsWith` | 4.15.3 |
| UTL-I18N-12 | MUST | Empty query matches all | 4.15.3 |
| UTL-I18N-13 | MUST | Item stringification (`stringifyAsLabel`) | 4.15.3 |
| UTL-I18N-14 | MUST | `platform.mediaQuery.iOS` constant | 4.15.3 |
| UTL-EDGE-01 | MUST | `engine.webkit` via CSS.supports | 4.16 |
| UTL-EDGE-02 | MUST | `engine.gecko` | 4.16 |
| UTL-EDGE-03 | MUST | `engine.blink`; engines exclusive | 4.16 |
| UTL-EDGE-04 | MUST | `os.ios` incl. iPadOS | 4.16 |
| UTL-EDGE-05 | MUST | `os.android` | 4.16 |
| UTL-EDGE-06 | MUST | `os.mac/windows/linux/apple` | 4.16 |
| UTL-EDGE-07 | MUST | `screenReader.voiceOver = apple` | 4.16 |
| UTL-EDGE-08 | MUST | `env.jsdom`; `isJSDOM` | 4.16 |
| UTL-EDGE-09 | SHOULD | Dev `userAgentData` path; production normative | 4.16 |
| UTL-EDGE-10 | MUST | `generateId` format | 4.17 |
| UTL-EDGE-11 | MUST | `createLogOnce` semantics | 4.17 |
| UTL-EDGE-12 | MUST | `warn`/`error` prefix `Base UI:`; `reset` | 4.17 |
| UTL-EDGE-13 | MUST | `formatErrorMessage` format | 4.17 |
| UTL-EDGE-14 | MUST | `clamp` | 4.17 |
| UTL-EDGE-15 | MUST | `areArraysEqual` | 4.17 |
| UTL-EDGE-16 | MUST | `mergeObjects` (= RND-ALG-06) | 4.17 |
| UTL-EDGE-17 | MUST | `fastObjectShallowCompare` | 4.17 |
| UTL-EDGE-18 | MUST | Frozen `EMPTY_ARRAY`/`EMPTY_OBJECT`; `NOOP` | 4.17 |
| UTL-EDGE-19 | MUST | Shared constant values | 4.17 |
| UTL-EDGE-20 | MUST | `isHTMLElement` cross-realm | 4.17 |
| UTL-EDGE-21 | MAY | Deprecated utils `isMouseWithinBounds` | 4.17 |
| UTL-EDGE-22 | MUST | Mode-switch error message (useControlled) | 7 |
| UTL-EDGE-23 | MUST | Default-change error message (useControlled) | 7 |
| UTL-EDGE-24 | MUST | No false positives; at most once per type/state | 7 |
| UTL-EDGE-25 | MUST | `useControlledProp` mode-switch message, every render | 7 |
| UTL-EDGE-26 | MUST | `useSyncedValues` unstable keys message | 7 |
| UTL-EDGE-27 | SHOULD | Stable callback called during render throws (dev) | 7 |
| UTL-EDGE-28 | MUST | Scroll-lock release tolerant of missing `removeEventListener` | 7 |
| UTL-ALG-01 | MUST | Scroll-lock acquire/release/lock | 5 |
| UTL-ALG-02 | MUST | Inset-scrollbar lock styles and restore | 5 |
| UTL-ALG-03 | MUST | Store notification with recursion guard | 5 |
| UTL-ALG-04 | MUST | Memoized selector | 5 |
| UTL-ALG-05 | MUST | Locale filter matching | 5 |
| UTL-ALG-06 | MUST | Item equality and selection index | 5 |
| UTL-ALG-07 | MUST | Selected-label resolution | 5 |
| UTL-ALG-08 | MUST | Dev-mode default serialization | 5 |
| UTL-ALG-09 | MUST | Animation-frame scheduler | 5 |
| UTL-ALG-10 | MUST | Request-queue processing | 5 |
| UTL-ALG-11 | MUST | Platform detection | 5 |
| UTL-ALG-12 | MUST | Scroll-edge normalization | 5 |
| UTL-ALG-13 | MUST | Transform extraction | 5 |
| UTL-ALG-14 | MUST | Pseudo-element bounds | 5 |
| UTL-ALG-15 | MUST | Media-query snapshot resolution | 5 |
| UTL-ALG-16 | MUST | Popup label resolution | 5 |
| UTL-ALG-17 | MUST | Composed-tree `closest`/`contains` | 5 |

## 10. Open questions

1. **External dependencies not in the pinned checkout.** `reselect` (`lruMemoize`, `createSelectorCreator`, `weakMapMemoize`) and `@floating-ui/utils/dom` (`isOverflowElement`, `getParentNode`, `isLastTraversableNode`, `getWindow`, `isElement`, `isHTMLElement`, `isShadowRoot`) are not vendored under `reference/base-ui` (no `node_modules`). Their semantics in UTL-ALG-04, UTL-DOM-04 and UTL-DOM-29 are stated from the libraries' public behavior and from what the Base UI tests assert (`createSelectorMemoized.test.ts:190-209` for `Object.is` equality and argument short-circuiting), not from pinned source. A reviewer with the dependency sources should confirm UTL-DOM-04's overflow regex and display exclusions. Resolved: both are now vendored under `reference/vendor` — `@floating-ui/utils` 0.2.12 `dist/floating-ui.utils.dom.mjs:45-53` (`isOverflowElement`: `/auto|scroll|overlay|hidden|clip/` over `overflow + overflowY + overflowX`, `display` not `inline`/`contents`) confirms UTL-DOM-04; `:100-102,118-132` (`isLastTraversableNode`, `getParentNode`) confirm UTL-DOM-29; `reselect` 5.3.0 `src/lruMemoize.ts:235-236` confirms the `maxSize: 1` / reference-equality defaults behind UTL-ALG-04.
2. **`useMediaQuery` scope.** It is exported only from `@base-ui/react/unstable-use-media-query` (`index.ts` does not re-export it; no docs outline entry). Whether the port must ship it is a product decision; it is specified here (UTL-STA-37…40) because the brief lists it.
3. **Scroll lock has no direct tests.** `packages/utils` contains no `useScrollLock` test; coverage comes from component suites through `test/scrollLock.ts`. Note that `isScrollLocked` detects simple/gutter-mode locks through the `overflow` shorthand and full-mode locks through the attribute (§8.3); a port's harness must mirror both signals. The gutter-support probe (`useScrollLock.ts:33-62`) writes and reverts inline styles synchronously and may force layout; whether that is acceptable in Blazor interop (one round trip per probe) is open.
4. **`supportsStableScrollbarGutter` sets `scrollbar-gutter` on `<html>` while measuring the viewport scroller**, which may be `<body>` (`useScrollLock.ts:45-55`). When `<body>` is the scroller the gutter on `<html>` may not affect `<body>`'s `offsetWidth`, so the probe may report `true` and choose the gutter path; it is unclear whether this is intended. Documented as-is.
5. **Equality inconsistencies.** `ReactStore.useSyncedValue` compares with `!==` before calling `set` (which uses `Object.is`), and `useValueChanged` uses `!==` (so `NaN → NaN` fires `onChange` every render while `0 → -0` never does; only the latter is tested). Ported faithfully (UTL-STA-19, UTL-STA-36); flagging in case the project prefers `Object.is` everywhere. Correction (review): `useValueChanged`'s effect only re-runs when `value` changes by `Object.is` (its dependency list, `useValueChanged.ts:10-16`), so `NaN → NaN` never fires `onChange`; the only divergence is `0 → -0` (recorded silently, UTL-STA-36). The question stands for `useSyncedValue`.
6. **Filter window rule.** `contains` compares fixed-width code-unit windows (UTL-ALG-05 note); strings whose collation-equal form differs in length from the query will not match. This matches Base UI exactly but may surprise; confirm the port should not "fix" it.
7. **`hasNullItemLabel` on record items uses `'null' in items`** (prototype chain included, `resolveValueLabel.tsx:43`), whereas `resolveSelectedLabel` uses own-property lookup (`:113`). The difference is only observable for a record whose prototype defines `null`; documented as-is.
8. **`RequestQueue` resolved fetches stay pending** until `setRequestSettled` (UTL-EVT-12). With no in-tree consumer at 1.9.0 the intended caller protocol cannot be confirmed; the tests ("processes next queued item on setRequestSettled") support this reading.
9. **`platform.engine.webkit` in test DOMs.** `CSS.supports` is absent in jsdom, so `webkit` is `false` there and every webkit-gated branch is exercised only in browser runs. The port's test translation should run those branches in a WebKit-like configuration or mark them browser-only.
10. **StoreInspector.** Treated as non-normative (UTL-STA-28). Confirm it is out of scope for the port.
