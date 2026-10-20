# Toast

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/toast/` |
| Tests | `packages/react/src/toast/**/*.test.tsx`, `toast/store.test.ts`, `toast/utils/isRenderableNode.test.ts` (16 files) + `createToastManager.spec.tsx`, `useToastManager.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/toast/page.mdx` |
| Prefix | `TST` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [button-behavior](../architecture/button-behavior.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Toast is a notification system. A **Provider** owns a store of toast objects and exposes an imperative API (`add`, `update`, `close`, `promise`) through the `useToastManager()` hook (inside the component tree) or a `createToastManager()` object (outside it). The consumer renders the current toasts inside a **Viewport** (a `region` landmark and polite live region), one **Root** per toast, with optional **Content**, **Title**, **Description**, **Action** and **Close** parts, usually inside a **Portal**. A toast can also be anchored to an arbitrary element through a **Positioner** (and **Arrow**) that wraps its Root.

What the user experiences: new toasts appear at the front of a stack (newest first); each auto-dismisses after a timeout (default 5000 ms) unless it is a `loading` toast or its timeout is `0`; timers pause while the pointer hovers the viewport, while keyboard focus is inside it, or while the browser window is not focused, and resume afterwards with the remaining time. Hovering or focusing the viewport "expands" the stack (a `data-expanded` attribute plus per-toast offset variables let CSS fan the toasts out). Toasts beyond the `limit` stay in the DOM but are marked `data-limited` and made inert. A toast can be dismissed by its Close button, by Escape while focused, by swiping it in an allowed direction (default down or right), or programmatically. <kbd>F6</kbd> moves keyboard focus into the viewport from anywhere in the window; Tab then walks the toasts and Shift+Tab (or tabbing past the last one) returns focus to the element focused before F6. Closing a focused toast moves focus to the next toast, or back to that element when none remains. High-priority toasts are additionally announced through a hidden `role="alert"` region.

Unlike the other popup components, Toast has no open/closed state of its own, no trigger parts, no outside-press dismissal and no focus trap; its lifecycle is driven entirely by the store. It reuses the shared button mechanism (Close, Action), the lite portal, focus guards, the animation-completion watcher and the anchor-positioning machinery, but implements its own swipe-to-dismiss gesture (it does not use the shared swipe tracker specified in PSH; see §6.2).

## 2. Anatomy

```
<Toast.Provider>                  required, exactly 1        renders no element
  <Toast.Portal>                  optional, 0..1             default element: div (portal node in <body>)
    <Toast.Viewport>              required for display, 1    default element: div
      <Toast.Root>                0..n (one per toast)       default element: div
        <Toast.Content>           optional, 0..1             default element: div
          <Toast.Title>           optional, 0..1             default element: h2
          <Toast.Description>     optional, 0..1             default element: p
          <Toast.Action>          optional, 0..n             default element: button
          <Toast.Close>           optional, 0..n             default element: button
      <Toast.Positioner>          optional (anchored toasts) default element: div
        <Toast.Root>              exactly 1 per Positioner
          <Toast.Arrow>           optional, 0..1             default element: div
          <Toast.Content> …
```

The consumer, not the library, decides which Roots exist: it reads `toasts` from `useToastManager()` and renders a `Toast.Root` for each entry, passing the entry as the `toast` prop. Content, Title, Description, Action and Close locate their Root through ancestor-provided shared state (not DOM adjacency); Root, Viewport, Close and Positioner locate the Provider's store the same way. Content is optional but is the part that keeps the toast's measured height current when its contents change (TST-ANI-03); Title and Description are optional and render nothing when they have no content.

- **TST-STR-01** — MUST. `Toast.Provider` renders no element of its own; it creates one store per Provider instance (created once, kept for the Provider's lifetime) and makes it available to every descendant part and to `useToastManager()`. Two Providers are fully independent: a toast added to one never appears in, and an update in one never affects, the other.
  Source: `toast/provider/ToastProvider.tsx:15-65`. Test: [L2] `useToastManager.test.tsx` "keeps multiple providers isolated when one provider updates".
- **TST-STR-02** — MUST. `Toast.Viewport` renders a `<div>` by default and MUST be a descendant of a `Toast.Provider`; rendering it outside one throws TST-EDGE-01.
  Source: `toast/viewport/ToastViewport.tsx:28,241`. Test: [L2] `ToastViewport.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`), "throws a descriptive error when rendered outside <Toast.Provider>".
- **TST-STR-03** — MUST. `Toast.Root` renders a `<div>` by default, MUST be a descendant of a `Toast.Provider` (else TST-EDGE-01), and represents exactly one toast object given through its required `toast` prop. All per-toast lookups are by `toast.id`, never by object identity, so a consumer may pass a freshly created copy of the store's object on every render.
  Source: `toast/root/ToastRoot.tsx:69,99-101,506`. Test: [L2] `ToastRoot.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`), "works correctly when toast objects are recreated (not referentially equal)", "correctly calculates indices for multiple toasts with recreated objects".
- **TST-STR-04** — MUST. `Toast.Content` renders a `<div>` by default and MUST be a descendant of a `Toast.Root`; outside one it throws TST-EDGE-02.
  Source: `toast/content/ToastContent.tsx:20,51`; `toast/root/ToastRootContext.ts:16-23`. Test: [L2] `ToastContent.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **TST-STR-05** — MUST. `Toast.Title` renders an `<h2>` by default and MUST be a descendant of a `Toast.Root` (else TST-EDGE-02); it renders nothing at all when it has no renderable content (TST-DOM-16).
  Source: `toast/title/ToastTitle.tsx:26-36`; `toast/utils/useToastLabelPart.ts:18,38-51`. Test: [L2] `ToastTitle.test.tsx` describeConformance (`refInstanceof: HTMLHeadingElement`); `useToastLabelPart.test.tsx` "<Toast.Title /> throws a descriptive error when rendered outside <Toast.Root>".
- **TST-STR-06** — MUST. `Toast.Description` renders a `<p>` by default and MUST be a descendant of a `Toast.Root` (else TST-EDGE-02); it renders nothing when it has no renderable content (TST-DOM-16).
  Source: `toast/description/ToastDescription.tsx:27-37`; `toast/utils/useToastLabelPart.ts:18,38-51`. Test: [L2] `ToastDescription.test.tsx` describeConformance (`refInstanceof: HTMLParagraphElement`); `useToastLabelPart.test.tsx` "<Toast.Description /> throws a descriptive error when rendered outside <Toast.Root>".
- **TST-STR-07** — MUST. `Toast.Action` renders a `<button>` by default and MUST be a descendant of a `Toast.Root` (else TST-EDGE-02); it renders nothing when it has no renderable content (TST-DOM-19).
  Source: `toast/action/ToastAction.tsx:28,41-54`. Test: [L2] `ToastAction.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`), "does not render if it has no children".
- **TST-STR-08** — MUST. `Toast.Close` renders a `<button>` by default and MUST be a descendant of both a `Toast.Root` (else TST-EDGE-02) and a `Toast.Provider` (else TST-EDGE-01).
  Source: `toast/close/ToastClose.tsx:28-29,42`. Test: [L2] `ToastClose.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`).
- **TST-STR-09** — MUST. `Toast.Portal` is the lite portal of [popup-store-and-handles](../architecture/popup-store-and-handles.md) (PSH-STR-11): it renders a `<div>` portal node appended to the resolved container (default `<body>`) and renders its children inside that node. It needs no Provider and carries no toast behavior.
  Source: `toast/portal/ToastPortal.tsx:13-18`; `utils/FloatingPortalLite.tsx:15-38`. Test: [L2] `ToastPortal.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`, rendered without a Provider). (L4 for the container-append clause)
- **TST-STR-10** — MUST. `Toast.Positioner` renders a `<div>` by default, MUST be a descendant of a `Toast.Provider` (else TST-EDGE-01), takes the same `toast` prop as the Root it wraps, and is the anchored alternative to placing the Root directly in the Viewport.
  Source: `toast/positioner/ToastPositioner.tsx:31-33,98-107`. Test: [L2] `ToastPositioner.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`), "throws a descriptive error when rendered outside <Toast.Provider>".
- **TST-STR-11** — MUST. `Toast.Arrow` renders a `<div>` by default and MUST be a descendant of a `Toast.Positioner`; outside one it throws TST-EDGE-03.
  Source: `toast/arrow/ToastArrow.tsx:20,28`; `toast/positioner/ToastPositionerContext.ts:14-22`. Test: [L2] `ToastArrow.test.tsx` describeConformance (`refInstanceof: Element`), "throws a descriptive error when rendered outside <Toast.Positioner>".
- **TST-STR-12** — MUST. In addition to its host element, the Viewport renders up to three focus guards (one sibling immediately before the host element, one as the host's first child and one as its last child; TST-DOM-04) and, after the host element, a visually hidden alert region for high-priority toasts (TST-DOM-05). Neither is part of the consumer's `children`.
  Source: `toast/viewport/ToastViewport.tsx:239-273`. Test: [L2] `ToastViewport.test.tsx` "skips toasts animating out when tabbing into the viewport" (`[data-base-ui-focus-guard]`); `useToastManager.test.tsx` "applies correct ARIA attributes for high priority toasts" (`role="alert"`).
- **TST-STR-13** — MUST. The Portal is optional: a Viewport rendered directly in the tree (or inside any other portal) behaves identically, binding its global listeners to its own owner window and document (TST-EDGE-08).
  Source: `toast/viewport/ToastViewport.tsx:49-55`. Test: [L4] `ToastViewport.test.tsx` "rebinds owner-document listeners once across empty store cycles" (Viewport portaled into an iframe without `Toast.Portal`).

## 3. Public API

### 3.1 `Toast.Provider`

**Default element:** none (renders only its children). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (`Toast.Provider.State` is an empty object type).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `timeout` | `number` | `5000` | Default auto-dismiss delay in ms for toasts that do not set their own `timeout`. `0` means never auto-dismiss. | TST-API-01 |
| `limit` | `number` | `3` | Maximum number of toasts shown at once; older toasts beyond it are marked `limited` (and inert) rather than removed. | TST-API-02 |
| `toastManager` | `ToastManager \| undefined` | `undefined` | A manager created by `Toast.createToastManager()` whose `add`/`update`/`close`/`promise` calls are forwarded to this Provider's store. | TST-API-03 |
| `children` | node | — | Subtree that can use the parts and `useToastManager()`. | — |

**Consumed (not forwarded to any element):** all of them (there is no element).

- **TST-API-01** — MUST. `timeout` defaults to `5000`. It is the delay used by TST-STA-18 for every toast whose own `timeout` is `undefined`; changing it affects only timers scheduled after the change lands (TST-STA-29).
  Source: `toast/provider/ToastProvider.tsx:16,76-78`; `toast/store.ts:119-138,193`. Test: [L2] `useToastManager.test.tsx` "adds a toast to the viewport that auto-dismisses after 5s by default", "applies a changed timeout to toasts added afterwards".
- **TST-API-02** — MUST. `limit` defaults to `3`. The newest `limit` non-ending toasts are unlimited; every older non-ending toast is `limited` (TST-STA-03). A change of `limit` re-evaluates the flags of the existing toasts (TST-STA-04). `limit={0}` marks every toast limited.
  Source: `toast/provider/ToastProvider.tsx:16,76-78`; `toast/store.ts:74-84,119-138`. Test: [L2] `useToastManager.test.tsx` "marks toasts as limited when the limit is exceeded", "recomputes limited toasts when the limit prop changes"; `ToastViewport.test.tsx` "returns focus when no toast can receive focus" (`limit={0}`).
- **TST-API-03** — MUST. When `toastManager` is given, the Provider subscribes to it after paint of the mount (and re-subscribes whenever the prop identity changes, unsubscribing from the previous manager) and performs, for each event the manager emits: `promise` → TST-STA-16 with the event's promise and options; `update` (with an id) → TST-STA-08/09 with the event's id and updates; `close` → TST-STA-11/12 with the event's id (possibly `undefined`); anything else (`add`) → TST-STA-05…07 with the event's options.
  Source: `toast/provider/ToastProvider.tsx:34-57`; `toast/createToastManager.ts:12-84`. Test: [L2] `createToastManager.test.tsx` "adds a toast", "updates a toast", "closes a toast", "closes all toasts", "adds a toast with the loading state that is updated with the success state".
- **TST-API-04** — MUST. A change of `timeout` or `limit` is written to the store post-commit, pre-paint of the commit that renders the new value, and before any descendant's post-commit work in that same commit, so a toast added by a descendant in that phase already uses the new value. A render that is abandoned before commit (for example suspended) MUST NOT write its values.
  Source: `toast/provider/ToastProvider.tsx:59-81` (synchronizer rendered as the first child). Test: [L2] `ToastProvider.test.tsx` "syncs a changed timeout before descendant layout effects", "syncs a changed limit before descendant layout effects", "does not sync provider props from an abandoned render".

#### State object (`Toast.Provider.State`)

Empty (`{}`); the Provider has no element and no state attributes.
Source: `toast/provider/ToastProvider.tsx:83,107`.

### 3.2 Imperative API: `useToastManager()` and `createToastManager()`

These are not parts but they are the only way toasts come into existence, so they are specified here.

#### `Toast.useToastManager<Data>()`

- **TST-API-05** — MUST. `useToastManager()` MUST be called below a `Toast.Provider` (else TST-EDGE-01) and returns an object `{ toasts, add, close, update, promise }` where `toasts` is the Provider's current toast array (TST-STA-01) and the four methods act on the Provider's store with the semantics of §4. `toasts` is reactive: whenever the array changes, consumer output rendered from it is updated.
  Source: `toast/useToastManager.ts:9-24,109-122`. Test: [L2] `useToastManager.test.tsx` (entire file; e.g. "adds a toast to the viewport that auto-dismisses after 5s by default"); `useToastManager.spec.tsx` (types).
- **TST-API-06** — MUST. `add(options)` returns the toast's id as a string: `options.id` when it is a non-empty string, otherwise a generated id of the form `toast-<4 random base-36 chars>-<counter>` (UTL-EDGE-10).
  Source: `toast/store.ts:165-167,202`; `packages/utils/src/generateId.ts:1-5`. Test: [L1] `createToastManager.test.tsx` "returns a toast id", "upserts a toast when adding with an existing id" (`firstToastId === 'save'`).
- **TST-API-07** — MUST. `close(id?)` closes the toast with that id, or every toast when `id` is `undefined` (TST-STA-11/12). `update(id, updatesOrFunction)` applies an object of changes, or calls a function with the current toast object and applies what it returns (TST-STA-08…10). `promise(promise, options)` runs the three-state lifecycle of TST-STA-16 and returns a promise (TST-EVT-07/08).
  Source: `toast/useToastManager.ts:14-23,109-122`; `toast/store.ts:205-225,288-320,322-362`. Test: [L1] `useToastManager.test.tsx` "closes a toast", "closes all toasts", "updates the toast", "displays success state as description after promise resolves".

#### `Toast.createToastManager<Data>()`

- **TST-API-08** — MUST. `createToastManager()` returns a new, independent manager object `{ add, close, update, promise, ' subscribe' }` on every call. `add` assigns the id exactly as TST-API-06 (so the id is known before any Provider handles the event), stamps `transitionStatus: 'starting'`, emits an `add` event and returns the id; `close`, `update` and `promise` emit `close`, `update` and `promise` events with their arguments. Events are delivered synchronously to every subscribed Provider; a manager with no subscribed Provider discards them (nothing is queued).
  Source: `toast/createToastManager.ts:12-84`. Test: [L1] `createToastManager.test.tsx` "returns a toast id" (no Provider), "adds a toast", "derives the update from the current toast when given a function"; `createToastManager.spec.tsx` (types).
- **TST-API-09** — MUST. `manager.promise(p, options)` returns the handled promise produced by the subscribed Provider (TST-EVT-07/08) when a Provider handled the event synchronously; otherwise it returns `p` itself.
  Source: `toast/createToastManager.ts:64-82`; `toast/store.ts:355-361`. Test: [L1] `createToastManager.test.tsx` "adds a toast with the loading state that is updated with the error state" (`.catch` on the returned promise).
- **TST-API-10** — MUST. The key `' subscribe'` (with a leading space) is the Provider's subscription hook: `' subscribe'(listener)` registers `listener` and returns an unsubscribe function. It is not part of the documented API (the docs page lists only `add`, `close`, `update`, `promise`), but a port must expose an equivalent for its Provider.
  Source: `toast/createToastManager.ts:20-27,87`. Test: none (exercised indirectly by every `createToastManager.test.tsx` test).

#### The toast object (`Toast.Root.ToastObject<Data>` / `ToastObject<Data>`)

| Field | Type | Set by | Meaning |
|---|---|---|---|
| `id` | `string` | consumer or generated | Unique id (TST-API-06). |
| `title` | `ReactNode` | consumer | Default content of `Toast.Title`; for high-priority toasts also the announced text. |
| `description` | `ReactNode` | consumer | Default content of `Toast.Description`; also what `promise` string shorthands set. |
| `type` | `string \| undefined` | consumer / `promise` | Free-form type, exposed as `data-type` on Root, Title, Description, Action, Close. `'loading'` suppresses auto-dismiss (TST-STA-18). `promise` sets `'loading'`, `'success'`, `'error'`. |
| `timeout` | `number \| undefined` | consumer | Per-toast auto-dismiss delay (ms); `0` = never; `undefined` = Provider `timeout`. |
| `priority` | `'low' \| 'high' \| undefined` | consumer | `'high'` → `role="alertdialog"` plus the hidden alert announcement (TST-ARIA-02, TST-A11Y-01). Default `'low'`. |
| `transitionStatus` | `'starting' \| 'ending' \| undefined` | store | `'starting'` from add until the first measurement; `'ending'` from close until removal; `undefined` otherwise (§4.4, §9). |
| `updateKey` | `number` | store | `0` on add; incremented by every `update` and every upsert (TST-STA-05/09). |
| `limited` | `boolean \| undefined` | store | Set when the toast exceeds `limit` (TST-STA-03). |
| `height` | `number \| undefined` | store | Measured natural height in px (TST-ANI-02); `0` while ending. |
| `ref` | DOM handle | store | The Root element of this toast, registered by the Root (used for focus hand-off). |
| `onClose` | `() => void` | consumer | Called when the toast starts closing (TST-EVT-01). |
| `onRemove` | `() => void` | consumer | Called when the toast is removed from the list after its exit animation (TST-EVT-03). |
| `actionProps` | `<button>` attributes | consumer | Attributes merged onto `Toast.Action` (TST-DOM-19). |
| `positionerProps` | `ToastManagerPositionerProps` | consumer | Positioner defaults for anchored toasts (`anchor?: Element \| null`, plus every Positioner prop except `toast`); `anchor !== undefined` disables swiping (TST-PTR-22). |
| `data` | `Data \| undefined` | consumer | Arbitrary payload, replaced wholesale on update (TST-STA-09); never invoked if it is a function (TST-STA-27). |

Source: `toast/useToastManager.ts:26-107`. Test: [L2] `useToastManager.test.tsx` "option: *" blocks; `store.test.ts` "stores a function passed as custom data instead of calling it".

- **TST-API-11** — MUST. `ToastManagerAddOptions<Data>` is the toast object without `id` (optional instead of required), `height`, `ref`, `limited` and `updateKey`; `transitionStatus` MAY be passed but is overridden on a fresh add (TST-STA-07) and ignored on an upsert (TST-STA-05). `ToastManagerUpdateOptions<Data>` is a partial toast object without `id`, `ref`, `height`, `transitionStatus`, `limited` and `updateKey`.
  Source: `toast/useToastManager.ts:124-137`. Test: [L1] `useToastManager.test.tsx` "ignores transitionStatus when upserting an existing toast"; `createToastManager.spec.tsx`, `useToastManager.spec.tsx` (type-level: `data` must be the whole `Data` value).
- **TST-API-12** — MUST. `ToastManagerPromiseOptions<Value, Data>` has three required members: `loading` (a string or update options), `success` and `error` (a string, update options, or a function of the resolved value / rejection reason returning either). A string resolves to `{ description: <string> }`; a function is called once with the value and its result is resolved the same way; an object is used as-is.
  Source: `toast/useToastManager.ts:139-149`; `toast/utils/resolvePromiseOptions.ts:3-22`. Test: [L1] `useToastManager.test.tsx` "displays success state as description after promise resolves", "passes data when success is a function", "accepts a function that returns full options for the success state", "passes data when error is a function", "supports custom options".

### 3.3 `Toast.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Toast.Viewport.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). A consumer `style` is merged after the part's `--toast-frontmost-height` entry. | — |
| `children` | node | — | Rendered between the two inner focus guards (TST-DOM-04). | — |
| any other `<div>` attribute / handler | | — | Forwarded; overrides the part's defaults of TST-DOM-01 and TST-ARIA-01 (handlers are chained per RND-EVT-02). | TST-DOM-23 |

**Consumed (not forwarded to the element):** `render`, `className`, `style`, `children` (re-inserted between the guards).

#### State object (`Toast.Viewport.State`)

| Field | Type | Meaning |
|---|---|---|
| `expanded` | `boolean` | `hovering || focused` of the store (TST-STA-14). |

Source: `toast/viewport/ToastViewport.tsx:235-237,277-282`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-expanded` | `expanded === true`; empty value | TST-DOM-02 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--toast-frontmost-height` | Viewport inline style | `<height>px` of the first toast in the list when its `height` is a positive number; otherwise not set | TST-DOM-03 |

### 3.4 `Toast.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Toast.Root.State`). A consumer `style` is merged after the part's inline style (drag styles and CSS variables of TST-DOM-10…14), so consumer keys win.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `toast` | `Toast.Root.ToastObject<any>` | — (required) | The toast object to render (from `useToastManager().toasts`). | TST-API-13 |
| `swipeDirection` | `'up' \| 'down' \| 'left' \| 'right' \| Array<…>` | `['down', 'right']` | Direction(s) in which a drag dismisses the toast. | TST-API-14 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<div>` attribute / handler | | — | Forwarded; overrides the defaults of TST-DOM-06…09 and TST-ARIA-02…06 (handlers chained). | — |

**Consumed (not forwarded to the element):** `toast`, `swipeDirection`, `render`, `className`, `style`.

- **TST-API-13** — MUST. `toast` is required. The Root reads `id`, `transitionStatus`, `limited`, `type`, `priority`, `height` and `positionerProps` from it and publishes its own DOM handle to the store under `toast.id` (TST-ANI-02). The Root does not read `title`/`description`/`actionProps` itself; Title, Description and Action do.
  Source: `toast/root/ToastRoot.tsx:52,60,99-101,131-137,461-483,497-504`. Test: [L2] `ToastRoot.test.tsx` "sets the vertical offset CSS variable".
- **TST-API-14** — MUST. `swipeDirection` defaults to `['down', 'right']`; a single string is treated as a one-element list. The list is the ordered set of dismiss directions used by §6.2. When `toast.positionerProps.anchor` is not `undefined` (an anchored toast), the effective list is empty and swiping is disabled regardless of the prop (TST-PTR-22).
  Source: `toast/root/ToastRoot.tsx:55,60-67`. Test: [L2] `ToastRoot.test.tsx` "closes toast when swiping in the specified direction beyond threshold" (string form), "supports multiple swipe directions" (array form), "ignores swipe gestures when toast is anchored". (L4 for the swipe outcomes)

#### State object (`Toast.Root.State`)

| Field | Type | Meaning |
|---|---|---|
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | `toast.transitionStatus` (`'idle'` is in the type but never produced by the store). |
| `expanded` | `boolean` | Store `hovering || focused` (TST-STA-14). |
| `limited` | `boolean` | `toast.limited || false`. |
| `type` | `string \| undefined` | `toast.type`. |
| `swiping` | `boolean` | A swipe gesture is in progress (from pointer-down to release). |
| `swipeDirection` | `'up' \| 'down' \| 'left' \| 'right' \| undefined` | The armed or dismissing swipe direction (§6.2). |

Source: `toast/root/ToastRoot.tsx:497-504,518-543`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-expanded` | `expanded === true`; empty value | TST-DOM-06 |
| `data-limited` | `limited === true`; empty value | TST-DOM-06 |
| `data-type` | `type !== undefined`; value is the type string | TST-DOM-06 |
| `data-swiping` | `swiping === true`; empty value | TST-DOM-06 |
| `data-swipe-direction` | `swipeDirection !== undefined`; value `up`/`down`/`left`/`right` | TST-DOM-06 |
| `data-starting-style` | `transitionStatus === 'starting'` | TST-DOM-07 |
| `data-ending-style` | `transitionStatus === 'ending'` | TST-DOM-07 |
| `inert` | `limited === true` (native attribute, not a data attribute) | TST-DOM-09 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--toast-index` | Root inline style | position among non-ending toasts (newest = `0`); while ending, the position in the full list | TST-DOM-10 |
| `--toast-offset-y` | Root inline style | `<n>px`, sum of the heights of all toasts before this one in the list | TST-DOM-11 |
| `--toast-height` | Root inline style | `<height>px` when `toast.height` is a positive number; otherwise not set | TST-DOM-12 |
| `--toast-swipe-movement-x` | Root inline style | `<dx>px` drag delta on the x axis (`0px` at rest) | TST-DOM-13 |
| `--toast-swipe-movement-y` | Root inline style | `<dy>px` drag delta on the y axis (`0px` at rest) | TST-DOM-13 |

### 3.5 `Toast.Content`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Toast.Content.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | | — |
| any other `<div>` attribute | | — | Forwarded. | — |

Content has no behavioral props; its job is to re-measure the toast when its subtree changes (TST-ANI-03).

#### State object (`Toast.Content.State`)

| Field | Type | Meaning |
|---|---|---|
| `expanded` | `boolean` | Store `hovering || focused`. |
| `behind` | `boolean` | The toast's visible index is greater than `0` (it sits behind the frontmost toast). |

Source: `toast/content/ToastContent.tsx:44-49,58-67`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-expanded` | `expanded === true` | TST-DOM-15 |
| `data-behind` | `behind === true` | TST-DOM-15 |

#### CSS variables

None.

### 3.6 `Toast.Title`

**Default element:** `<h2>`. **Accepts `render` element substitution:** yes (a substitute element's own children count as content; a childless substitute is still rendered only when the toast has a title — TST-DOM-16). **Accepts `className`/`style` as function of state:** yes (state = `Toast.Title.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | node | `toast.title` | Content; when `undefined`/`null`, the toast object's `title` is used. Not listed in the outline (§16 Q2). | TST-API-15 |
| `id` | `string` | generated | Element id, also referenced by the Root's `aria-labelledby`. Not listed in the outline (§16 Q2). | TST-API-15 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<h2>` attribute | | — | Forwarded. | — |

- **TST-API-15** — MUST. Title and Description resolve their content as `children ?? toast.title` (Title) / `children ?? toast.description` (Description) — a nullish `children` falls back, any other value (including `0`) is used as-is — and their id as the consumer `id` when given, else a generated id (RND-DOM-27…29) — one of RND-DOM-27's listed exceptions: the bare unique token without the `base-ui-` prefix (`toast/utils/useToastLabelPart.ts:23`).
  Source: `toast/utils/useToastLabelPart.ts:20-25`; `toast/title/ToastTitle.tsx:17-26`; `toast/description/ToastDescription.tsx:18-27`. Test: [L2] `useToastLabelPart.test.tsx` "renders the toast title by default", "renders the toast description by default", "renders a numeric zero child"; `ToastRoot.test.tsx` "keeps dynamic title and description ids synchronized with mounted label parts".

#### State object (`Toast.Title.State`)

| Field | Type | Meaning |
|---|---|---|
| `type` | `string \| undefined` | `toast.type`. |

Source: `toast/title/ToastTitle.tsx:28,39-44`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-type` | `type !== undefined`; value is the type string | TST-DOM-17 |

### 3.7 `Toast.Description`

**Default element:** `<p>`. **Accepts `render` element substitution:** yes (same content rule as Title). **Accepts `className`/`style` as function of state:** yes (state = `Toast.Description.State`, `{ type }`).

Props, state and data attributes are identical to `Toast.Title` (TST-API-15, TST-DOM-16, TST-DOM-17) with `toast.description` as the fallback content and the Root's `aria-describedby` as the consumer of the id.
Source: `toast/description/ToastDescription.tsx:18-37,40-51`.

### 3.8 `Toast.Action`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes (`testRenderPropWith: 'button'`; a childless substitute is rendered only when `toast.actionProps.children` provides content — TST-DOM-19). **Accepts `className`/`style` as function of state:** yes (state = `Toast.Action.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Whether the rendered element is a native `<button>` (UBT-API-02/03). | TST-API-16 |
| `disabled` | `boolean \| undefined` | `undefined` | Passed to the button mechanism (UBT). Not listed in the outline (§16 Q2). | TST-API-16 |
| `children` | node | `toast.actionProps.children` | Content; `toast.actionProps.children` takes precedence when it is not nullish. | TST-DOM-19 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; overridden by the same key in `toast.actionProps` (handlers chained). | TST-DOM-19 |

- **TST-API-16** — MUST. `Toast.Action` and `Toast.Close` pass `native: nativeButton` (default `true`) and `disabled` to the shared button mechanism and nothing else (no `focusableWhenDisabled`); the resulting `type="button"`/`role="button"`, `tabindex`, `disabled`/`aria-disabled` and keyboard activation follow UBT-DOM-01…09, UBT-ARIA-02/03 and UBT-KEY-01/02/09.
  Source: `toast/action/ToastAction.tsx:24,32-35`; `toast/close/ToastClose.tsx:24,33-36`; `internals/use-button/useButton.ts` (see [button-behavior](../architecture/button-behavior.md) §6.2 rows `Toast.Close`, `Toast.Action`). Test: [L2] `ToastAction.test.tsx` / `ToastClose.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`, including the "with a non-native button" re-run).

#### State object (`Toast.Action.State`)

| Field | Type | Meaning |
|---|---|---|
| `type` | `string \| undefined` | `toast.type`. |

Source: `toast/action/ToastAction.tsx:37-39,57-62`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-type` | `type !== undefined` | TST-DOM-18 |

### 3.9 `Toast.Close`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes (`testRenderPropWith: 'button'`). **Accepts `className`/`style` as function of state:** yes (state = `Toast.Close.State`, `{ type }`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | See TST-API-16. | TST-API-16 |
| `disabled` | `boolean \| undefined` | `undefined` | See TST-API-16. Not listed in the outline (§16 Q2). | TST-API-16 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; a consumer `aria-hidden` overrides TST-ARIA-07; `onClick`/`onFocus`/`onBlur` are chained with the part's own handlers. | — |

State object: `{ type: toast.type }` (`toast/close/ToastClose.tsx:38-40,64-69`). Data attributes: `data-type` as for Action (TST-DOM-18).

### 3.10 `Toast.Portal`

**Default element:** `<div>` (the portal node). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Toast.Portal.State`, empty).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `container` | `HTMLElement \| ShadowRoot \| RefObject<…> \| null \| undefined` | `undefined` (→ `<body>`, or the enclosing portal node) | Parent element for the portal node. | TST-API-17 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | Applied to the portal node. | — |

- **TST-API-17** — MUST. `container` resolves per the lite portal (PSH-STR-11; `floating-ui-react/components/FloatingPortal.tsx:93-123`): an element or shadow root is used directly, a DOM handle is dereferenced, `undefined` falls back to an enclosing portal node and then to `document.body`, and `null` renders nothing until a container is provided. The portal node carries a generated `id` and `data-base-ui-portal=""`.
  Source: `toast/portal/ToastPortal.tsx:13-28`; `floating-ui-react/components/FloatingPortal.tsx:69-134`. Test: [L4] `ToastPortal.test.tsx` describeConformance.

### 3.11 `Toast.Positioner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Toast.Positioner.State`). A consumer `style` is merged after the positioning styles.

#### Props

Every prop except `toast` has a three-level default: the element prop when given, else the same key in `toast.positionerProps`, else the built-in default (TST-POS-02).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `toast` | `ToastObject<any>` | — (required) | The toast being positioned (same object as the wrapped Root's). | TST-POS-01 |
| `anchor` | `Element \| null \| undefined` | `toast.positionerProps.anchor` | Element to position against; non-elements (handles, functions, virtual elements) are treated as `null`. | TST-POS-03 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | CSS positioning strategy. | TST-POS-01 |
| `side` | `Side` (`'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-start' \| 'inline-end'`) | `'top'` | Preferred side (note: not `'bottom'` as for other popups). | TST-POS-01 |
| `align` | `'start' \| 'center' \| 'end'` | `'center'` | Alignment on the side. | TST-POS-01 |
| `sideOffset` | `number \| OffsetFunction` | `0` | Distance from the anchor. | TST-POS-01 |
| `alignOffset` | `number \| OffsetFunction` | `0` | Offset along the alignment axis. | TST-POS-01 |
| `collisionBoundary` | `'clipping-ancestors' \| Element \| Element[] \| Rect` | `'clipping-ancestors'` | Collision boundary. | TST-POS-01 |
| `collisionPadding` | `Padding` | `5` | Padding from the boundary. | TST-POS-01 |
| `collisionAvoidance` | `CollisionAvoidance` | `{ fallbackAxisSide: 'end' }` | Collision strategy (`POPUP_COLLISION_AVOIDANCE`). | TST-POS-01 |
| `arrowPadding` | `number` | `5` | Minimum arrow distance from the positioner edges. | TST-POS-01 |
| `sticky` | `boolean` | `false` | Keep in view when the anchor scrolls out. | TST-POS-01 |
| `disableAnchorTracking` | `boolean` | `false` | Do not follow anchor layout shifts. | TST-POS-01 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | | — |

**Consumed:** `toast`, every positioning prop above, `render`, `className`, `style`.

#### State object (`Toast.Positioner.State`)

| Field | Type | Meaning |
|---|---|---|
| `side` | `Side` | Resolved side after collision handling. |
| `align` | `Align` | Resolved alignment. |
| `anchorHidden` | `boolean` | The anchor is hidden/clipped (FLT hide middleware). |

Source: `toast/positioner/ToastPositioner.tsx:92-96,114-127`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-side` | always; resolved side | TST-DOM-21 |
| `data-align` | always; resolved alignment | TST-DOM-21 |
| `data-anchor-hidden` | `anchorHidden === true` | TST-DOM-21 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--toast-index` | Positioner inline style | as TST-DOM-10, for the wrapped toast | TST-DOM-21 |
| `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin` | Positioner inline style | from the shared anchor positioning (FLT `useAnchorPositioning` `positionerStyles`) | TST-DOM-21 |

### 3.12 `Toast.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Toast.Arrow.State`). A consumer `style` is merged after the arrow positioning styles.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | A consumer `aria-hidden` overrides TST-ARIA-08. | — |

State object: `{ side, align, uncentered }` where `uncentered` is `true` when the arrow could not be centered on the anchor (`toast/arrow/ToastArrow.tsx:22-26,35-48`). Data attributes: `data-side`, `data-align` (always) and `data-uncentered` (when `uncentered`), TST-DOM-22.

### 3.13 Shared types

- `Toast.Root.ToastObject<Data = any>` (alias `ToastRootToastObject`, `ToastObject`): the table in §3.2. Source: `toast/useToastManager.ts:26-97`; `toast/root/ToastRoot.tsx:516,559`.
- `ToastManager<Data = any>`: `{ ' subscribe', add, close, update, promise }` (TST-API-08). `ToastManagerEvent`: `{ action: 'add' | 'close' | 'update' | 'promise', options: any }`. Source: `toast/createToastManager.ts:86-104`.
- `UseToastManagerReturnValue<Data = any>`: `{ toasts, add, close, update, promise }` (TST-API-05). Source: `toast/useToastManager.ts:109-122`.
- `ToastManagerAddOptions<Data>`, `ToastManagerUpdateOptions<Data>`, `ToastManagerPromiseOptions<Value, Data>` (TST-API-11/12), `ToastManagerPositionerProps` (Positioner props minus `anchor`/`toast`, plus `anchor?: Element | null`). Source: `toast/useToastManager.ts:99-149`.
- `Toast.<Part>.Props` / `Toast.<Part>.State` for Provider, Portal, Viewport, Root, Content, Title, Description, Action, Close, Positioner, Arrow (all listed in the outline); the attribute-name namespaces `ToastViewportCssVariables`, `ToastViewportDataAttributes`, `ToastRootCssVariables`, `ToastRootDataAttributes`, `ToastContentDataAttributes`, `ToastDescriptionDataAttributes`, `ToastTitleDataAttributes`, `ToastCloseDataAttributes`, `ToastActionDataAttributes`, `ToastPositionerCssVariables`, `ToastPositionerDataAttributes`, `ToastArrowDataAttributes`. Source: `toast/index.ts:3-28`.
- Toast defines no `ChangeEventReason`/`ChangeEventDetails`: its callbacks (`onClose`, `onRemove`) take no arguments and no `reason` literal is emitted (see [event-details-and-reasons](../architecture/event-details-and-reasons.md) §3.6, "Components with no event-details callbacks").
- `TransitionStatus` = `'starting' | 'ending' | 'idle' | undefined` (`internals/useTransitionStatus.ts:6`); `Side`, `Align`, `Boundary`, `OffsetFunction`, `CollisionAvoidance` (`internals/useAnchorPositioning.ts:66-123`, owned by FLT).

## 4. State model

Toast has no consumer-owned (controlled) state: there is no `open`/`defaultOpen` pair and no change callback with event details. All state lives in the Provider's store, is mutated only through the four manager methods and the Root's measurement, and is observed by the parts.

### 4.1 Store record

| Field | Type | Initial | Meaning |
|---|---|---|---|
| `toasts` | `StoredToast[]` | `[]` | The toast list, newest first (TST-STA-01). |
| `toastMetadata` | map id → `{ domIndex, visibleIndex, offsetY }` | derived | Recomputed on every list write (TST-STA-02). |
| `hovering` | `boolean` | `false` | Pointer is over the viewport, or a swipe is in progress (§6.2). |
| `focused` | `boolean` | `false` | Keyboard (focus-visible) focus is inside the viewport (§6.3). |
| `isWindowFocused` | `boolean` | `true` | The browser window has focus (TST-FOC-11/12). |
| `timeout`, `limit` | `number` | Provider props | TST-API-01/02. |
| `viewport` | element or `null` | `null` | The Viewport's host element (set when it attaches, `null` when it detaches). |
| `prevFocusElement` | element or `null` | `null` | Element focused before the last <kbd>F6</kbd> (TST-FOC-01). |

Source: `toast/store.ts:25-35`; `toast/provider/ToastProvider.tsx:18-30`. Store write semantics (`set`/`update`, `Object.is` no-op checks, synchronous notification) are UTL-STA-07…11.

- **TST-STA-01** — MUST. `toasts` is ordered newest first: a freshly added toast is inserted at index `0` and existing toasts keep their relative order. Updating a toast keeps its position; closing keeps it in the list (as `ending`) until removal; removal splices it out.
  Source: `toast/store.ts:190,254,305-311,160-162`. Test: [L1] `store.test.ts` "keeps a toast added from inside an updater" (`['b','a']`); `ToastContent.test.tsx` "marks content behind the frontmost toast with data-behind".
- **TST-STA-02** — MUST. For every list write the store derives, per toast: `domIndex` = its index in `toasts`; `visibleIndex` = its index counting only non-ending toasts before it, or `-1` when it is itself ending; `offsetY` = the sum of `height || 0` of every toast before it in the list (ending toasts contribute their `height`, which TST-STA-11 sets to `0`). Lookups for unknown ids yield `undefined` toast, `-1` indexes and `0` offset.
  Source: `toast/store.ts:46-68,86-97`. Test: [L1] `store.test.ts` "keeps toast metadata synchronized after mutations".
- **TST-STA-03** — MUST. `limited` is derived over the non-ending toasts in list order: the first `limit` of them are `limited: false`, every further one `limited: true`; ending toasts keep whatever flag they had. Re-deriving leaves a toast object untouched (same reference) when its flag is unchanged.
  Source: `toast/store.ts:74-84`. Test: [L1] `useToastManager.test.tsx` "marks toasts as limited when the limit is exceeded"; `store.test.ts` "recomputes limited flags when the limit changes".
- **TST-STA-04** — MUST. The `limited` flags are re-derived on every fresh add, on every close (single or all), and when the Provider's `limit` changes — so closing a front toast un-limits the oldest limited one. They are NOT re-derived by `update` or by an upsert of an existing toast, which therefore preserve the flag.
  Source: `toast/store.ts:131-136,191,310`. Test: [L1] `useToastManager.test.tsx` "unmarks toasts as limited when the limit is not exceeded", "recomputes limited toasts when the limit prop changes", "preserves limited state when upserting a limited toast".

### 4.2 Adding and upserting

- **TST-STA-05** — MUST. `add(options)` with an `options.id` that matches a toast that is **not** ending is an upsert: the existing toast is updated in place (TST-STA-09 semantics) with every option except `id` and `transitionStatus`, its `updateKey` is incremented, its `limited` flag and list position are preserved, its auto-dismiss timer is restarted from the (possibly new) full duration (TST-STA-19 with the forced-restart rule), and the same id is returned. No second toast is created.
  Source: `toast/store.ts:169-179,227-286`. Test: [L1] `useToastManager.test.tsx` "ignores transitionStatus when upserting an existing toast", "increments updateKey when adding again with the same id", "preserves limited state when upserting a limited toast"; `createToastManager.test.tsx` "upserts a toast when adding with an existing id" (timer restarted: 900 ms + 200 ms alive, gone at 1000 ms after the upsert); `store.test.ts` "replaces custom data when re-adding a toast under an existing id", "stores a function as the value when re-adding a toast under an existing id".
- **TST-STA-06** — MUST. `add(options)` with an `options.id` that matches a toast that **is** ending removes that toast immediately **without** calling its `onRemove`, then proceeds as a fresh add (TST-STA-07). The list therefore never holds two toasts with the same id.
  Source: `toast/store.ts:172-175,149-163`. Test: [L1] `useToastManager.test.tsx` "replaces a closing toast when adding again with the same id", "does not call onRemove when replacing an ending toast", "calls onRemove once after replacing an ending toast and later removing the replacement".
- **TST-STA-07** — MUST. A fresh add stores `{ ...options, id, updateKey: 0, transitionStatus: 'starting' }` (overriding any `transitionStatus` in the options) at the front of the list, re-derives `limited`, and schedules the auto-dismiss timer per TST-STA-18. Adding does not change `hovering`/`focused`.
  Source: `toast/store.ts:183-203`. Test: [L1] `useToastManager.test.tsx` "adds a toast to the viewport that auto-dismisses after 5s by default", "increments updateKey when adding again with the same id" (`0` first); `ToastContent.test.tsx` "marks content behind the frontmost toast with data-behind".

### 4.3 Updating

- **TST-STA-08** — MUST. `update(id, …)` is a no-op when no toast has that id or when the toast is ending; in those cases a function argument is never invoked. (This is what keeps a dismissed `promise` toast closed, TST-STA-17.)
  Source: `toast/store.ts:211-215,234-244`. Test: [L1] `store.test.ts` "ignores mutations that target an unknown toast", "does not call the updater for a missing or ending toast"; `useToastManager.test.tsx` "does not reopen a dismissed promise toast when it resolves".
- **TST-STA-09** — MUST. An applied update produces a new toast object `{ ...previous, ...updates, updateKey: previous.updateKey + 1 }`: every key present in `updates` replaces the previous value wholesale (including `data`, which is never merged, and including explicit `undefined`), keys absent from `updates` are kept, and the toast's position, `limited` flag and `transitionStatus` are unchanged (TST-API-11 excludes `transitionStatus` from the type; an upsert strips it explicitly).
  Source: `toast/store.ts:246-254`. Test: [L1] `store.test.ts` "replaces custom data wholesale when updating a toast", "derives the update from the current toast when given a function" (`updateKey` 1); `useToastManager.test.tsx` "updates the toast", "increments updateKey when updating a toast".
- **TST-STA-10** — MUST. When the second argument of `update` is a function, it is called exactly once with the current toast object (its `data` is `undefined` when none was set) and its return value is the `updates` object. The store re-reads its state after the function returns, so store calls made inside it are honored: a toast added inside the updater stays in the list, and a toast closed inside its own updater stays closed and the returned updates are discarded (TST-STA-08).
  Source: `toast/store.ts:205-225,233-235`. Test: [L1] `store.test.ts` "derives the update from the current toast when given a function", "passes a toast without custom data to the updater", "keeps a toast added from inside an updater", "keeps a toast closed from inside its updater closed"; `createToastManager.test.tsx` "derives the update from the current toast when given a function". Docs: CHANGELOG.md v1.8.0 "Allow functional toast updates derived from the current toast (#5464, #5611, #5629)".
- **TST-STA-27** — MUST. A function stored in `data` (on add, update, upsert or any `promise` state) is kept as the value and never invoked; only the *second argument* of `update` is treated as an updater function.
  Source: `toast/store.ts:221,246-248`; `toast/utils/resolvePromiseOptions.ts:16-18` (only the options object itself may be a function). Test: [L1] `store.test.ts` "stores a function passed as custom data instead of calling it", "stores a function as the value when re-adding a toast under an existing id", "stores a function passed as custom data on a promise toast".

### 4.4 Closing and removing

- **TST-STA-11** — MUST. `close(id)` for a known, non-ending toast: clears its timer, sets `transitionStatus: 'ending'` and `height: 0` on it (a new object; other toasts are untouched), re-derives `limited`, calls `onClose` (TST-EVT-01), then performs the focus hand-off of TST-FOC-07…09. For an unknown id nothing happens; for an already-ending toast the state write is repeated but `onClose` is not called again (TST-EVT-02).
  Source: `toast/store.ts:288-320`. Test: [L1] `useToastManager.test.tsx` "closes a toast", "calls onClose when the toast is closed"; `store.test.ts` "ignores mutations that target an unknown toast".
- **TST-STA-12** — MUST. `close()` with no id: every timer is cleared and the paused flag reset (TST-STA-25), every toast becomes `ending` with `height: 0`, `onClose` is called for each toast that was not already ending, in list order, and focus returns to the previously focused element (TST-FOC-09). The interaction flags are cleared because no active toast remains (TST-STA-15).
  Source: `toast/store.ts:289-295,305-320`. Test: [L1] `useToastManager.test.tsx` "closes all toasts"; `createToastManager.test.tsx` "closes all toasts", "does not call onClose when closing toasts that are already ending". Docs: CHANGELOG.md v1.2.0 "Enable closing all toasts (#3979)".
- **TST-STA-13** — MUST. Removal (performed by the Root on animations finished, TST-ANI-05, and by TST-STA-06) deletes the toast from the list and calls its `onRemove` unless the caller suppressed it; removing an id that is not in the list does nothing (so `onRemove` can never fire twice for one removal).
  Source: `toast/store.ts:149-163`. Test: [L1] `store.test.ts` "does not invoke onRemove for a toast that is no longer in the store"; `useToastManager.test.tsx` "calls onRemove when the toast is removed".
- **TST-STA-14** — MUST. The derived `expanded` flag is `hovering || focused`; it is what the Viewport, Root, Content and Close parts read (`data-expanded`, Close `aria-hidden`). The derived "expanded or out of focus" predicate used by the timer logic is `hovering || focused || !isWindowFocused`.
  Source: `toast/store.ts:93-95`. Test: [L1] `ToastViewport.test.tsx` "removes expanded on mouseleave when focus-visible not inside", "keeps expanded on mouseleave when focus-visible is inside"; `ToastContent.test.tsx` "reflects the expanded state when the viewport is hovered".
- **TST-STA-15** — MUST. `hovering` and `focused` are both reset to `false` whenever a list write leaves the list empty, and whenever a close leaves no non-ending toast in the list; other list writes leave them alone.
  Source: `toast/store.ts:311,466-478`. Test: [L1] `ToastViewport.test.tsx` "collapses a deferred mouseleave after a closing toast is removed while blurred" (viewport loses `data-expanded` once the closing toast is removed); `store.test.ts` "re-pauses timers after the last toast is closed and a new one is added".

### 4.5 Promise toasts

- **TST-STA-16** — MUST. `promise(p, options)`: synchronously, the `loading` options are resolved (TST-API-12) and a toast is added with those options plus `type: 'loading'` (so no timer runs). When `p` fulfills with `v`, the `success` options are resolved with `v` and the toast is updated with them plus `type: 'success'` and `timeout: successOptions.timeout` (explicitly present even when `undefined`, so a timer is scheduled from the Provider default and a `loading` timeout is never inherited). When `p` rejects with `e`, the `error` options are resolved with `e` and the toast is updated with them plus `type: 'error'` and `timeout: errorOptions.timeout` the same way.
  Source: `toast/store.ts:322-353`; `toast/utils/resolvePromiseOptions.ts:3-22`. Test: [L1] `useToastManager.test.tsx` "displays success state as description after promise resolves", "displays error state as description after promise rejects", "auto-dismisses success toast after default timeout when promise resolves", "uses custom timeout from success options when promise resolves", "uses custom timeout from error options when promise rejects", "uses provider timeout when no custom timeout is specified", "does not inherit a loading timeout when success does not specify one", "does not auto-dismiss when timeout is set to 0". Docs: CHANGELOG.md v1.0.0-beta.2 "Fix `promise` method timeout option handling (#2294)".
- **TST-STA-17** — MUST. If the loading toast was closed before `p` settles, the success/error update is ignored (TST-STA-08) and the toast MUST NOT reappear or stay mounted because of the settlement.
  Source: `toast/store.ts:239-244`. Test: [L1] `useToastManager.test.tsx` "does not reopen a dismissed promise toast when it resolves"; `createToastManager.test.tsx` "does not reopen a dismissed promise toast when it resolves". Docs: CHANGELOG.md v1.1.0 "Prevent dismissed promise toast from reopening on updates (#4040)".

### 4.6 Auto-dismiss timers

Each toast has at most one timer record `{ delay, remaining, start, running? }` keyed by id. "Running" means a macrotask timeout (UTL-EVT-01) is pending for it; "paused" is a single store-wide flag.

- **TST-STA-18** — MUST. A timer is scheduled for a toast on add (and upsert, TST-STA-05) iff `type !== 'loading'` and `duration > 0`, where `duration = toast.timeout ?? providerTimeout`. The record stores `delay = remaining = duration` and `start = now`; the timeout is actually started only if the store is not "expanded or out of focus" (TST-STA-14), otherwise the record is created without a running timeout and the store is marked paused (TST-STA-23).
  Source: `toast/store.ts:193-200,418-435`. Test: [L1] `useToastManager.test.tsx` "dismisses the toast after the specified timeout", "does not auto-dismiss when timeout is set to 0", "schedules a timer when updating a loading toast to a non-loading type"; `store.test.ts` "re-pauses timers after the last timed toast closes while untimed toasts remain".
- **TST-STA-28** — MUST. When a running timer fires, its record is deleted (resetting the paused flag if it was the last record, TST-STA-24) and the toast is closed exactly as by `close(id)` (TST-STA-11, including `onClose`).
  Source: `toast/store.ts:195,280,390-393,423-426,453-456`. Test: [L1] `useToastManager.test.tsx` "calls onClose when the toast auto-dismisses", "adds a toast to the viewport that auto-dismisses after 5s by default".
- **TST-STA-19** — MUST. After an update (or upsert) let `shouldHaveTimer = !ending && type !== 'loading' && nextDuration > 0` with `nextDuration = next.timeout ?? providerTimeout`. If `shouldHaveTimer` and (no record exists, or `nextDuration` differs from the previous effective duration, or the `timeout` key was present in the updates (even with the same or an `undefined` value), or the previous `type` was `'loading'`, or the operation is an upsert), the existing record is discarded and a fresh timer with the full `nextDuration` is scheduled per TST-STA-18 (starting paused if the store is expanded or out of focus). Otherwise an existing record keeps running untouched.
  Source: `toast/store.ts:256-285`. Test: [L1] `createToastManager.test.tsx` "resets the auto-dismiss timer when updating with the same timeout value", "resets the auto-dismiss timer when updating from 0 to a timeout, then updating with the same timeout again", "auto-dismisses when updating timeout from 0 to a positive value", "schedules a timer when updating a loading toast to a non-loading type", "does not clear the auto-dismiss timer when updated twice before a re-render"; `store.test.ts` "keeps a rescheduled timer paused while expanded, and runs it once collapsed". Docs: CHANGELOG.md v1.0.0 "Fix timers not being rescheduled when updated (#3564)".
- **TST-STA-20** — MUST. If after an update `shouldHaveTimer` is `false` and a record exists (the toast became `loading` or its effective duration became `0`), the record is discarded (timer cleared) and the paused flag is reset if no records remain.
  Source: `toast/store.ts:268-271,445-451`. Test: [L1] `store.test.ts` "re-pauses timers after the last timed toast becomes untimed".
- **TST-STA-21** — MUST. Pausing (while not already paused): the store is marked paused and, for every record with a running timeout, the timeout is cancelled and `remaining := max(remaining − (now − start), 0)`; records without a running timeout keep their `remaining`. Pausing while already paused does nothing.
  Source: `toast/store.ts:364-380`. Test: [L1] `store.test.ts` "does not extend the remaining time across repeated pause/resume cycles", "accumulates active time across hover cycles so the toast still dismisses"; `ToastViewport.test.tsx` "pauses timers when hovering". Docs: CHANGELOG.md v1.7.0 "Fix remaining toast timer calculation (#5261)".
- **TST-STA-22** — MUST. Resuming (while paused): the paused flag is cleared and every record starts a timeout for `remaining` if `remaining > 0`, else for the full `delay` (a timer whose remaining time was exhausted while paused — for example because the clock jumped in a throttled background tab — restarts from scratch rather than firing immediately); `start := now`. Resuming while not paused does nothing.
  Source: `toast/store.ts:382-396`. Test: [L1] `store.test.ts` "restarts the full delay when the clock jumped past the timeout before pausing"; `ToastViewport.test.tsx` "resumes timers when not hovering" (4999 ms alive, gone at 5001 ms after mouseleave).
- **TST-STA-23** — MUST. A timer scheduled while the store is expanded or out of focus is created without a running timeout (`remaining = delay`) and the store is marked paused (if not already); it first starts on the next resume, with its full delay.
  Source: `toast/store.ts:198-200,282-284,418-435`. Test: [L1] `store.test.ts` "keeps a rescheduled timer paused while expanded, and runs it once collapsed"; `ToastViewport.test.tsx` "keeps timers paused on viewport blur while the window is blurred".
- **TST-STA-24** — MUST. Whenever the last timer record disappears (fired, cleared by close/update, or all cleared), the paused flag is reset to not-paused so that the next toast's timer can be paused again by a later hover/focus.
  Source: `toast/store.ts:437-464`. Test: [L1] `store.test.ts` "re-pauses timers after the last toast is closed and a new one is added", "re-pauses timers after all toasts are closed and a new one is added", "re-pauses timers after the last active toast closes while ending toasts remain", "re-pauses timers after the last timed toast becomes untimed". Docs: CHANGELOG.md v1.5.0 "Fix timer and limit edge cases (#4933)".
- **TST-STA-25** — MUST. `close(id)` discards that toast's record; `close()` discards every record and resets the paused flag; unmounting the Provider discards every record.
  Source: `toast/store.ts:295,302,437-451,140-147`; `toast/provider/ToastProvider.tsx:32`. Test: [L1] `store.test.ts` "re-pauses timers after all toasts are closed and a new one is added"; `ToastProvider.test.tsx` (teardown between tests).
- **TST-STA-29** — MUST. A change of the Provider's `timeout` does not alter existing records; it is read only when a later timer is scheduled (TST-STA-18/19).
  Source: `toast/store.ts:119-138,193,256-257`. Test: [L1] `useToastManager.test.tsx` "applies a changed timeout to toasts added afterwards".

### 4.7 Part state objects

- **TST-STA-26** — MUST. The state objects exposed to `className`/`style`/`render` functions contain exactly: Viewport `{ expanded }`; Root `{ transitionStatus, expanded, limited, type, swiping, swipeDirection }`; Content `{ expanded, behind }`; Title, Description, Action and Close `{ type }`; Positioner `{ side, align, anchorHidden }`; Arrow `{ side, align, uncentered }`; Provider and Portal `{}`. Server: relaxed, see §9.
  Source: `toast/viewport/ToastViewport.tsx:235-237`; `toast/root/ToastRoot.tsx:497-504`; `toast/content/ToastContent.tsx:46-49`; `toast/title/ToastTitle.tsx:28`; `toast/description/ToastDescription.tsx:29`; `toast/action/ToastAction.tsx:37-39`; `toast/close/ToastClose.tsx:38-40`; `toast/positioner/ToastPositioner.tsx:92-96`; `toast/arrow/ToastArrow.tsx:22-26`. Test: [L2] describeConformance of every part (className/style function calls); `ToastRoot.test.tsx` "maps the active swipe direction to its data attribute".

### 4.8 Lifecycle diagram (per toast)

```
add() ──► [starting]  ──(Root measured, post-commit pre-paint)──►  [open: status unset]
                                                                      │
             close(id) / timer / Escape / Close / swipe ──────────────┘
                                                                      ▼
                                                  [ending: height 0, timer gone]
                                                      │                      │
                add(same id) ──► removed silently,    │ on animations finished (Root)
                fresh [starting]                      ▼
                                                  removed (onRemove)
```

Transition attributes and completion are specified in §9.

## 5. Rendered DOM and ARIA

### 5.1 Viewport element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `tabindex` | `-1` (focusable programmatically, not in the tab order) | TST-DOM-01 | C# |
| `role` | `region` | TST-ARIA-01 | C# |
| `aria-live` | `polite` | TST-ARIA-01 | C# |
| `aria-atomic` | `false` | TST-ARIA-01 | C# |
| `aria-relevant` | `additions text` | TST-ARIA-01 | C# |
| `aria-label` | `Notifications` | TST-ARIA-01 | C# |
| `data-expanded` | `expanded` (TST-STA-14) | TST-DOM-02 | C# |
| `style="--toast-frontmost-height: <n>px"` | first toast has positive `height` | TST-DOM-03 | C# |

- **TST-DOM-01** — MUST. The Viewport host element renders `tabindex="-1"` and attaches handlers for `mouseenter`, `mousemove`, `mouseleave`, `focus`, `blur`, `keydown`, `click`, `pointerdown`, `pointerup` and `pointercancel` (§6). It is focusable by script and by <kbd>F6</kbd> only.
  Source: `toast/viewport/ToastViewport.tsx:211-227`. Test: [L2] `ToastViewport.test.tsx` "gets focused when F6 is pressed". (L4 for the F6 clause)
- **TST-DOM-02** — MUST. `data-expanded=""` is present on the Viewport iff `hovering || focused` (TST-STA-14).
  Source: `toast/viewport/ToastViewport.tsx:38,235-237`; `toast/viewport/ToastViewportDataAttributes.ts:5`. Test: [L2] `ToastViewport.test.tsx` "removes expanded on mouseleave when focus-visible not inside", "keeps expanded during an active touch swipe even if mouseleave fires".
- **TST-DOM-03** — MUST. The Viewport's inline style sets `--toast-frontmost-height` to `<height>px` where `height` is the `height` of the **first toast in the list** (index 0, whether or not it is ending) when that value is a positive number; when the list is empty, the first toast is unmeasured, or its height is `0` (an ending toast), the variable is not set. The entry is merged before the consumer's `style` (RND-DOM-16).
  Source: `toast/viewport/ToastViewport.tsx:40,228-232`; `toast/viewport/ToastViewportCssVars.ts:5`. Test: [L2] `ToastViewport.test.tsx` "sets the measured frontmost toast height CSS variable". Docs: `components/toast/page.mdx` §Stacking and animations.
- **TST-DOM-04** — MUST. While the list is non-empty **and** a previous-focus element is recorded (TST-FOC-01), the Viewport renders three focus guards (PSH-STR-08, PSH-DOM-13: `<span tabindex="0" data-base-ui-focus-guard="" aria-hidden="true">`, visually hidden): one as the previous sibling of the host element, one as the host's first child and one as its last child; the consumer's `children` sit between the inner two. When either condition is false no guard exists. Each guard's `focus` handler performs TST-FOC-02/03.
  Source: `toast/viewport/ToastViewport.tsx:104-119,239,247-255,259-262`; `utils/FocusGuard.tsx:10-42`. Test: [L2] `ToastViewport.test.tsx` "skips toasts animating out when tabbing into the viewport", "returns focus when no toast can receive focus", "focuses first toast upon tab after viewport is focused". (L4 for the guard focus-handler clause)
- **TST-DOM-05** — MUST. While the store is not `focused` and at least one toast in the list (including ending ones) has `priority: 'high'`, the Viewport renders, as a sibling **after** its host element, a `<div>` with the visually-hidden inline style (UTL-DOM-13) containing, for each such toast in list order, `<div role="alert" aria-atomic="true"><div>{title}</div><div>{description}</div></div>`. The container is absent otherwise (TST-A11Y-01/03).
  Source: `toast/viewport/ToastViewport.tsx:43,263-272`. Test: [L2] `useToastManager.test.tsx` "applies correct ARIA attributes for high priority toasts" (`role="alert"`, `aria-atomic="true"`, gone after close), "high priority toasts in dialogs have correct accessibility structure".
- **TST-DOM-23** — MUST. Consumer attributes on the Viewport are merged after the part's defaults (TST-DOM-01, TST-ARIA-01), so any of `role`, `aria-*`, `tabIndex` can be overridden and consumer handlers chain with the part's handlers (RND-EVT-01/02); the `children` wrapper of TST-DOM-04 is applied last and cannot be replaced.
  Source: `toast/viewport/ToastViewport.tsx:241-257`. Test: [L2] `ToastViewport.test.tsx` describeConformance (prop forwarding, RND-DOM-36…49).

### 5.2 Root element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `alertdialog` when `priority === 'high'`, else `dialog` | TST-ARIA-02 | C# |
| `tabindex` | `0` | TST-DOM-08 | C# |
| `aria-modal` | `false` | TST-ARIA-03 | C# |
| `aria-labelledby` | id of the mounted Title | TST-ARIA-04 | C# |
| `aria-describedby` | id of the mounted Description | TST-ARIA-05 | C# |
| `aria-hidden` | `true` when `priority === 'high'` and the store is not `focused` | TST-ARIA-06 | C# |
| `inert` | `limited === true` | TST-DOM-09 | C# |
| `data-expanded`, `data-limited`, `data-type`, `data-swiping`, `data-swipe-direction` | per state | TST-DOM-06 | C# (`data-expanded`, `data-limited`, `data-type`); JS (`data-swiping`, `data-swipe-direction`) |
| `data-starting-style`, `data-ending-style` | per `transitionStatus` | TST-DOM-07 | C# → JS |
| inline `--toast-index`, `--toast-offset-y`, `--toast-height`, `--toast-swipe-movement-x/y` | always (height only when measured) | TST-DOM-10…13 | C# (`--toast-index`, `--toast-offset-y`, `--toast-height`); JS (`--toast-swipe-movement-x/y`) |
| inline `transition: none`, `transform: …` | while `swiping` | TST-DOM-14 | JS |

- **TST-DOM-06** — MUST. The Root renders `data-expanded=""` iff `expanded`; `data-limited=""` iff `limited`; `data-type="<type>"` iff `type` is defined; `data-swiping=""` iff a swipe gesture is active (from pointer-down to release); `data-swipe-direction="<dir>"` iff a direction is armed or the toast was dismissed by a swipe in that direction (it stays on the element during the exit phase).
  Source: `toast/root/ToastRoot.tsx:28-33,497-511`; `toast/root/ToastRootDataAttributes.ts:7-27`; `internals/getStateAttributesProps.ts` (default projection, RND-DOM-06…09). Test: [L2] `ToastRoot.test.tsx` "maps the active swipe direction to its data attribute", "applies [data-swiping] attribute when swiping", "clears swipe state when re-adding a swipe-dismissed toast" (`data-swipe-direction="right"` while ending); `useToastManager.test.tsx` "marks toasts as limited when the limit is exceeded", "renders the type". (L4 for the `data-swiping`/`data-swipe-direction` clauses)
- **TST-DOM-07** — MUST. The Root applies the shared transition mapping (RND-DOM-11, TRN-DOM-01…03): `data-starting-style=""` while `toast.transitionStatus === 'starting'`, `data-ending-style=""` while `'ending'`, neither otherwise. The status values themselves come from the store (§4.8, §9), not from the TRN state machine.
  Source: `toast/root/ToastRoot.tsx:28-29,498`; `internals/stateAttributesMapping.ts:10-20`. Test: [L4] `ToastRoot.test.tsx` "clears the starting state and restores the height when re-adding an ending toast".
- **TST-DOM-08** — MUST. The Root renders `tabindex="0"`: every non-limited toast is a tab stop inside the viewport.
  Source: `toast/root/ToastRoot.tsx:465`. Test: [L2] `ToastViewport.test.tsx` "focuses first toast upon tab after viewport is focused", "returns focus to previous element when pressing shift+Tab on last toast" (tab sequence root → close → action → next root).
- **TST-DOM-09** — MUST. A limited toast's Root carries the `inert` attribute (present/empty, UTL-DOM-16) and therefore cannot be focused, clicked or reached by assistive technology; it is removed when the toast is un-limited. Limited toasts stay in the DOM.
  Source: `toast/root/ToastRoot.tsx:475`; `packages/utils/src/inertValue.ts:3-9`. Test: [L2] `ToastViewport.test.tsx` "returns focus when no toast can receive focus" (`limit={0}`: the guard skips the inert toast). Docs: CHANGELOG.md v1.0.0-alpha.8 "Avoid removing limited toasts from the DOM"; `components/toast/page.mdx` §Stacking and animations.
- **TST-DOM-10** — MUST. `--toast-index` is the toast's `visibleIndex` (TST-STA-02) while it is not ending — `0` for the frontmost (newest) non-ending toast — and its `domIndex` while it is ending, so a closing toast keeps a real stacking index instead of `-1`. The value is a bare number (no unit).
  Source: `toast/root/ToastRoot.tsx:99-100,478-479`; `toast/root/ToastRootCssVars.ts:5`. Test: [L2] `ToastPositioner.test.tsx` "keeps the DOM index while a toast animates out" (same rule on the Positioner); `ToastRoot.test.tsx` "keeps stacking intact when re-adding an ending toast among other toasts", "works correctly when toast objects are recreated (not referentially equal)".
- **TST-DOM-11** — MUST. `--toast-offset-y` is `<offsetY>px` with `offsetY` from TST-STA-02 (`0px` for the frontmost toast).
  Source: `toast/root/ToastRoot.tsx:101,480`; `toast/root/ToastRootCssVars.ts:10`. Test: [L2] `ToastRoot.test.tsx` "sets the vertical offset CSS variable".
- **TST-DOM-12** — MUST. `--toast-height` is `<height>px` when `toast.height` is a positive number and is not set otherwise; it therefore appears after the first measurement (TST-ANI-02) and disappears while the toast is ending (TST-STA-11).
  Source: `toast/root/ToastRoot.tsx:481`; `toast/root/ToastRootCssVars.ts:15`. Test: [L2] `ToastRoot.test.tsx` "recalculates height when content mutates", "clears the starting state and restores the height when re-adding an ending toast" (`''` while ending, restored after re-add).
- **TST-DOM-13** — MUST. `--toast-swipe-movement-x` and `--toast-swipe-movement-y` are always set, to `<dx>px`/`<dy>px` where `(dx, dy)` is the current drag offset minus the element's transform at gesture start (§6.2); both are `0px` at rest and after a cancelled or sub-threshold release, and they keep the final drag delta after a dismissing release (so exit styles can continue from the released position).
  Source: `toast/root/ToastRoot.tsx:445-459`; `toast/root/ToastRootCssVars.ts:20,25`. Test: [L4] `ToastRoot.test.tsx` "locks to a single axis on the first move of a two-axis swipe", "cancels a vertical swipe when the pointer changes its mind" (`0px`), "resets drag state after releasing a far swipe even when the release lands on the document".
- **TST-DOM-14** — MUST. While `swiping`, the Root's inline style has `transition: none` and `transform: translateX(<x>px) translateY(<y>px) scale(<s>)` where `(x, y)` is the absolute drag offset and `s` the scale read from the element's computed transform at gesture start (UTL-DOM-24), freezing the element at its current visual position; when not swiping neither inline property is set (the consumer's CSS transform applies).
  Source: `toast/root/ToastRoot.tsx:445-459`. Test: [L4] `ToastRoot.test.tsx` "resets drag state after releasing a far swipe even when the release lands on the document" (`style.transition === ''`, `style.transform === ''` after release). Docs: CHANGELOG.md v1.0.0-beta.4 "Ensure toast is frozen at its current visual transform while swiping (#2769)".

### 5.3 Content element

- **TST-DOM-15** — MUST. The Content renders `data-expanded=""` iff `expanded` and `data-behind=""` iff its toast's `visibleIndex > 0` (an ending toast, index `-1`, is never "behind").
  Source: `toast/content/ToastContent.tsx:44-49`; `toast/content/ToastContentDataAttributes.ts:5,10`. Test: [L2] `ToastContent.test.tsx` "marks content behind the frontmost toast with data-behind", "reflects the expanded state when the viewport is hovered"; `ToastRoot.test.tsx` "keeps stacking intact when re-adding an ending toast among other toasts".

### 5.4 Title and Description elements

- **TST-DOM-16** — MUST. Title/Description render their element only when the element that *would* be rendered has renderable children: `null`, `undefined`, booleans, the empty string, and arrays containing only such values (recursively) are not renderable; everything else (including `0`, `NaN`, bigint `0n`, non-empty strings) is. The check is applied to the final element, so a substitute element's own children count, a substitute callback that returns no element renders nothing, and a childless substitute element is rendered only when the fallback content (TST-API-15) exists. While rendered, the element carries the resolved `id` and publishes it to the Root (TST-ARIA-04/05); when it stops rendering, the publication is withdrawn.
  Source: `toast/utils/isRenderableNode.ts:3-18`; `toast/utils/useToastLabelPart.ts:33-52`; `toast/title/ToastTitle.tsx:30-36`. Test: [L2] `isRenderableNode.test.ts` (all five tests); `useToastLabelPart.test.tsx` "does not render if it has no children", "renders a numeric zero child", "renders content passed through the render prop", "renders content passed through a render function", "renders the toast title through a childless render prop", "does not render a childless render prop when there is no content", "does not render when a render function returns no element", "clears aria-labelledby from the root when the content is removed". Docs: CHANGELOG.md v1.7.0 "Render content passed through the `render` prop in `<Toast.Title>`, `<Toast.Description>`, and `<Toast.Action>` (#5210)".
- **TST-DOM-17** — MUST. Title and Description render `data-type="<type>"` iff the toast's `type` is defined.
  Source: `toast/title/ToastTitle.tsx:28`; `toast/description/ToastDescription.tsx:29`; `toast/title/ToastTitleDataAttributes.ts:5`; `toast/description/ToastDescriptionDataAttributes.ts:5`. Test: none (conformance only).

### 5.5 Action and Close elements

- **TST-DOM-18** — MUST. Action and Close render `data-type="<type>"` iff the toast's `type` is defined, plus the button-mechanism attributes of TST-API-16 (`type="button"` in native mode).
  Source: `toast/action/ToastAction.tsx:37-39`; `toast/close/ToastClose.tsx:38-40`; `toast/action/ToastActionDataAttributes.ts:5`; `toast/close/ToastCloseDataAttributes.ts:5`. Test: [L2] `ToastAction.test.tsx` / `ToastClose.test.tsx` describeConformance.
- **TST-DOM-19** — MUST. The Action's attribute set is, in increasing precedence: the consumer's props, then `toast.actionProps` (so an `id`, `onClick`, etc. given when the toast was added override the consumer's, handlers chaining per RND-EVT-02), then the button mechanism's props, then `children = toast.actionProps.children ?? consumer children`. The element is rendered only if the resulting element has renderable children (TST-DOM-16 rule); otherwise the Action renders nothing.
  Source: `toast/action/ToastAction.tsx:30,41-54`. Test: [L2] `ToastAction.test.tsx` "forwards actionProps to the button" (`id="action"`), "performs an action when clicked", "does not render if it has no children", "renders content passed through the render prop", "does not render a childless render prop when there is no action content", "renders the toast action content through a childless render prop".

### 5.6 Portal node

- **TST-DOM-20** — MUST. The Portal's node is a `<div>` with a generated `id` and `data-base-ui-portal=""`, appended to the resolved container (TST-API-17); consumer `className`/`style`/attributes land on this node.
  Source: `floating-ui-react/components/FloatingPortal.tsx:125-134`; `floating-ui-react/utils/createAttribute.ts:1-3`. Test: [L2] `ToastPortal.test.tsx` describeConformance. (L4 for the container-append clause)

### 5.7 Positioner and Arrow elements

- **TST-DOM-21** — MUST. The Positioner renders `role="presentation"`, `data-side="<side>"` and `data-align="<align>"` (resolved values, PSH-DOM-06 value sets), `data-anchor-hidden=""` iff the anchor is hidden (PSH-DOM-04), the shared positioning inline styles and CSS variables (`--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`; FLT), `--toast-index` per TST-DOM-10 for its toast, and `transition: none` inline while the toast's `transitionStatus` is `'starting'` (TRN-DOM-05, merged before consumer props).
  Source: `toast/positioner/ToastPositioner.tsx:60-61,92-107`; `utils/usePositioner.tsx:21-42`; `internals/getDisabledMountTransitionStyles.ts:5-9`; `toast/positioner/ToastPositionerCssVars.ts:5-25`; `toast/positioner/ToastPositionerDataAttributes.ts:6-16`. Test: [L4] `ToastPositioner.test.tsx` "falls back to the viewport when no anchor is provided" (`data-side="top"`, `data-align="center"`), "positions an anchored toast against its anchor element" (`data-side="bottom"`), "keeps the DOM index while a toast animates out".
- **TST-DOM-22** — MUST. The Arrow renders the shared arrow inline styles (FLT `arrowStyles`), `data-side` and `data-align` mirroring the Positioner's resolved placement, and `data-uncentered=""` when the arrow cannot be centered on the anchor.
  Source: `toast/arrow/ToastArrow.tsx:20-32`; `toast/arrow/ToastArrowDataAttributes.ts:7-16`. Test: [L4] `ToastArrow.test.tsx` "mirrors the resolved side of its positioner".

### 5.8 ARIA

- **TST-ARIA-01** — MUST. The Viewport renders `role="region"`, `aria-live="polite"`, `aria-atomic="false"`, `aria-relevant="additions text"` and `aria-label="Notifications"` by default (each overridable by the consumer, TST-DOM-23). It is thus both a landmark reachable by assistive technology and a polite live region that announces added toasts and text changes.
  Source: `toast/viewport/ToastViewport.tsx:213-217`. Test: none (CHANGELOG.md v1.0.0-beta.2 "Make `Toast.Viewport` an announce container (#2246)"). Docs: `components/toast/page.mdx` §General usage.
- **TST-ARIA-02** — MUST. The Root renders `role="alertdialog"` when `toast.priority === 'high'` and `role="dialog"` otherwise.
  Source: `toast/root/ToastRoot.tsx:461,464`. Test: [L2] `useToastManager.test.tsx` "applies correct ARIA attributes for high priority toasts", "toasts in dialogs are accessible and not aria-hidden" (`getByRole('dialog')` is the Dialog; the toast root is found by test id).
- **TST-ARIA-03** — MUST. The Root renders `aria-modal="false"` (a non-modal dialog).
  Source: `toast/root/ToastRoot.tsx:466`. Test: [L2] `useToastManager.test.tsx` "applies correct ARIA attributes for high priority toasts".
- **TST-ARIA-04** — MUST. The Root renders `aria-labelledby="<id>"` where `<id>` is the id published by the currently rendered Title (TST-DOM-16), and omits the attribute while no Title is rendered.
  Source: `toast/root/ToastRoot.tsx:78,467`. Test: [L2] `useToastLabelPart.test.tsx` "<Toast.Title /> adds aria-labelledby to the root element", "wires aria-labelledby to a part rendered through the render prop", "clears aria-labelledby from the root when the content is removed"; `ToastRoot.test.tsx` "keeps dynamic title and description ids synchronized with mounted label parts".
- **TST-ARIA-05** — MUST. The Root renders `aria-describedby="<id>"` where `<id>` is the id published by the currently rendered Description, and omits it otherwise.
  Source: `toast/root/ToastRoot.tsx:79,468`. Test: [L2] `useToastLabelPart.test.tsx` "<Toast.Description /> adds aria-describedby to the root element", "clears aria-describedby from the root when the content is removed".
- **TST-ARIA-06** — MUST. A high-priority Root renders `aria-hidden="true"` while the store is not `focused` (the toast is then announced only through the alert region, TST-A11Y-01); it has no `aria-hidden` while focused or when its priority is not high.
  Source: `toast/root/ToastRoot.tsx:469`. Test: [L2] `useToastManager.test.tsx` "high priority toasts in dialogs have correct accessibility structure" (`aria-hidden="true"`).
- **TST-ARIA-07** — MUST. The Close button renders `aria-hidden="true"` while the stack is not `expanded` and the button itself does not have focus, and `aria-hidden="false"` otherwise (the attribute is always present unless overridden by the consumer). Focus is tracked by the button's own `focus`/`blur` events, so a Close that receives keyboard focus is never hidden from assistive technology.
  Source: `toast/close/ToastClose.tsx:31,47,51-56`. Test: [L2] `ToastViewport.test.tsx` "skips toasts animating out when tabbing into the viewport" (comment: "The close button is `aria-hidden` until expanded"). Docs: CHANGELOG.md v1.0.0-rc.2 "Fix `<Toast.Close>` emitting `aria-hidden` warning on click (#3469)".
- **TST-ARIA-08** — MUST. The Arrow renders `aria-hidden="true"` (overridable by the consumer).
  Source: `toast/arrow/ToastArrow.tsx:31`. Test: [L2] `ToastArrow.test.tsx` "mirrors the resolved side of its positioner".
- **TST-ARIA-09** — MUST. The Positioner renders `role="presentation"` (overridable).
  Source: `utils/usePositioner.tsx:36`. Test: none.
- **TST-ARIA-10** — MUST. When more than one Title (or Description) renders under one Root, the most recently rendered one's id wins; when a Title stops rendering, its withdrawal clears the Root's reference only if the Root still holds that same id (an older part's withdrawal never clears a newer part's id).
  Source: `toast/utils/useToastLabelPart.ts:40-49`. Test: [L2] `useToastLabelPart.test.tsx` "does not let an older part cleanup clear a newer part".

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>F6</kbd> | anywhere in the viewport's window while toasts exist, target is not the Viewport | `preventDefault`; remember the focused element; focus the Viewport (`preventScroll`); mark `focused`; pause timers | TST-KEY-01, TST-FOC-01 |
| <kbd>F6</kbd> | target is the Viewport itself | not handled (no `preventDefault`, nothing changes) | TST-KEY-02 |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | Viewport element focused (target is the Viewport) | `preventDefault`; focus the remembered element (TST-FOC-04) | TST-KEY-03 |
| <kbd>Tab</kbd> | Viewport element focused | not intercepted; native focus movement lands on the first inner focus guard, which forwards to the first focusable toast (TST-FOC-02) | TST-KEY-04 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | inside a toast | not intercepted; native order (Root, then its tabbable children, then the next Root…); leaving the last/first toast lands on a guard → TST-FOC-03 | TST-KEY-04 |
| <kbd>Escape</kbd> | keydown bubbling to a Root while the document's active element is inside that Root's DOM subtree | close that toast (TST-STA-11) | TST-KEY-05 |
| <kbd>Escape</kbd> | keydown bubbling to a Root while the active element is outside its DOM subtree (e.g. content portaled elsewhere) | ignored | TST-KEY-06 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | Close or Action focused | native button activation (or UBT emulation for `nativeButton={false}`) → the part's click behavior | TST-API-16 |
| any other key | | not handled | — |

- **TST-KEY-01** — MUST. While the list is non-empty, a `keydown` for `F6` dispatched anywhere in the Viewport's owner window whose target is not the Viewport element calls `preventDefault()` and performs TST-FOC-01. (The listener is on the window, bubble phase, so a handler that stops propagation before it wins.)
  Source: `toast/viewport/ToastViewport.tsx:57-66,97`. Test: [L4] `ToastViewport.test.tsx` "gets focused when F6 is pressed", "pauses timers when the viewport is focused", "rebinds owner-document listeners once across empty store cycles" (F6 in an iframe window).
- **TST-KEY-02** — MUST. `F6` whose target is the Viewport element is not handled.
  Source: `toast/viewport/ToastViewport.tsx:59`. Test: none.
- **TST-KEY-03** — MUST. A `Shift+Tab` keydown whose target is the Viewport element itself calls `preventDefault()` and focuses the remembered element (TST-FOC-04); timers resume through the resulting Viewport blur (TST-FOC-06), not directly, so that a remembered element that lives inside the viewport keeps them paused.
  Source: `toast/viewport/ToastViewport.tsx:121-134`. Test: [L4] `ToastViewport.test.tsx` "restores focus and resumes timers on shift+Tab out of the focused viewport", "keeps timers paused when shift+Tab returns focus inside the viewport".
- **TST-KEY-04** — MUST. `Tab` without Shift on the Viewport, and Tab in either direction inside a toast, are not intercepted.
  Source: `toast/viewport/ToastViewport.tsx:121-134` (only the shift branch). Test: [L4] `ToastViewport.test.tsx` "keeps the viewport focused when Tab is pressed without shift", "returns focus to previous element when pressing shift+Tab on last toast".
- **TST-KEY-05** — MUST. An `Escape` keydown reaching a Root's handler closes that toast iff the Root element contains the document's active element (shadow-DOM aware, UTL-DOM-19/20). `preventDefault` is not called.
  Source: `toast/root/ToastRoot.tsx:410-421`. Test: [L4] `ToastRoot.test.tsx` "closes when pressing escape", "moves focus to the next toast when closing in an index-keyed list".
- **TST-KEY-06** — MUST. The same keydown is ignored when the active element is not inside the Root's DOM subtree, even if the event bubbles to the Root through the component tree (portaled children).
  Source: `toast/root/ToastRoot.tsx:412-417`. Test: [L4] `ToastRoot.test.tsx` "ignores Escape when focus is in portaled content".

### 6.2 Pointer and touch

#### Viewport hover and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `mouseenter` / `mousemove` on the Viewport (or bubbling from a toast) | any | pause timers; `hovering := true`; cancel any deferred mouseleave | TST-PTR-01 |
| `mouseleave` | no ending toast, no touch active | `hovering := false`; resume timers unless still `focused` or window unfocused | TST-PTR-02 |
| `mouseleave` | some toast is ending, or a touch gesture is active | deferred: applied (as above) once no toast is ending and no touch is active | TST-PTR-03, TST-PTR-04 |
| `pointerdown` (touch) on the Viewport | | mark touch active (affects mouseleave deferral) | TST-PTR-04 |
| `pointerup` / `pointercancel` (touch) on the Viewport | | clear touch active; apply a deferred mouseleave if any | TST-PTR-04 |
| `pointerdown` (touch) anywhere in the document outside the Viewport | capture phase | resume timers; `hovering := false`; `focused := false` | TST-PTR-05 |
| `pointerdown` (mouse/pen) outside the Viewport | | ignored | TST-PTR-06 |
| `pointerdown` (touch) inside the Viewport | document listener | ignored by the document listener (the toast's own handler may start a swipe) | TST-PTR-07 |
| `click` inside the Viewport | | same as a Viewport `focus` event (TST-FOC-05): sets `focused` only if the active element is focus-visible | TST-PTR-25 |
| `click` on Close | enabled | close the toast | TST-PTR-26 |
| `click` on Action | enabled | run `toast.actionProps.onClick` (and the consumer's `onClick`) | TST-PTR-27 |

- **TST-PTR-01** — MUST. `mouseenter` and `mousemove` on the Viewport pause the timers (TST-STA-21) and set `hovering := true`; either event also cancels a pending deferred mouseleave.
  Source: `toast/viewport/ToastViewport.tsx:153-157,218-219`. Test: [L2] `ToastViewport.test.tsx` "pauses timers when hovering"; `ToastContent.test.tsx` "reflects the expanded state when the viewport is hovered".
- **TST-PTR-02** — MUST. `mouseleave` on the Viewport, when no toast is ending and no touch gesture is active, sets `hovering := false` and resumes the timers (TST-STA-22) unless the store is still `focused` or the window is unfocused.
  Source: `toast/viewport/ToastViewport.tsx:136-149,159-164,220`. Test: [L2] `ToastViewport.test.tsx` "resumes timers when not hovering", "removes expanded on mouseleave when focus-visible not inside", "keeps expanded on mouseleave when focus-visible is inside", "keeps timers paused while still focused after the other interaction ends", "keeps timers paused on mouseleave while the window is blurred".
- **TST-PTR-03** — MUST. While at least one toast is ending, a `mouseleave` only records the intent; the collapse of TST-PTR-02 is applied after paint of the first commit in which no toast is ending any more (typically when the closing toast is removed), provided no `mouseenter`/`mousemove` cancelled it meanwhile.
  Source: `toast/viewport/ToastViewport.tsx:42,136-151`. Test: [L2] `ToastViewport.test.tsx` "collapses a deferred mouseleave after a closing toast is removed while blurred". Docs: CHANGELOG.md v1.0.0-beta.4 "Reduce stickiness of expanded state (#2770)".
- **TST-PTR-04** — MUST. A touch `pointerdown` on the Viewport marks a touch gesture active until the matching touch `pointerup`/`pointercancel` on the Viewport; a `mouseleave` arriving while a touch gesture is active is deferred and applied on that release (so a touch swipe keeps the stack expanded until the finger lifts). Non-touch pointer events do not set the flag.
  Source: `toast/viewport/ToastViewport.tsx:166-179,225-227`. Test: [L2] `ToastViewport.test.tsx` "keeps expanded during an active touch swipe even if mouseleave fires", "keeps expanded when a touch swipe is canceled without leaving the viewport", "collapses after a touch swipe is canceled if mouseleave already fired". Docs: CHANGELOG.md v1.3.0 "Keep viewport expanded while touch swiping (#4411)".
- **TST-PTR-05** — MUST. A touch `pointerdown` whose target is outside the Viewport (document capture listener, while toasts exist) resumes the timers and clears both `hovering` and `focused`, regardless of window focus state.
  Source: `toast/store.ts:402-416`; `toast/viewport/ToastViewport.tsx:100`. Test: [L4] `ToastViewport.test.tsx` "collapses and resumes timers on a touch outside the viewport".
- **TST-PTR-06** — MUST. A non-touch `pointerdown` outside the Viewport is ignored by that listener (a mouse press outside is followed by `mouseleave`, which handles the collapse).
  Source: `toast/store.ts:403-405`. Test: [L4] `ToastViewport.test.tsx` "ignores a mouse pointerdown outside the viewport".
- **TST-PTR-07** — MUST. A touch `pointerdown` inside the Viewport does not collapse the stack or resume timers.
  Source: `toast/store.ts:407-410`. Test: [L4] `ToastViewport.test.tsx` "stays expanded on a touch inside the viewport".
- **TST-PTR-25** — MUST. A `click` on the Viewport runs the Viewport focus logic of TST-FOC-05 (it sets `focused` only when the active element matches `:focus-visible`, so a mouse click inside a toast does not keep the stack expanded).
  Source: `toast/viewport/ToastViewport.tsx:181-198,224`. Test: none.
- **TST-PTR-26** — MUST. A click on an enabled Close closes its toast (TST-STA-11). The handler is merged before the consumer's props and the button mechanism, so a consumer `onClick` runs first and a disabled Close does not close (UBT-PTR-01).
  Source: `toast/close/ToastClose.tsx:45-60`. Test: [L2] `ToastClose.test.tsx` "closes the toast when clicked"; `useToastManager.test.tsx` "unmarks toasts as limited when the limit is not exceeded".
- **TST-PTR-27** — MUST. A click on an enabled Action invokes `toast.actionProps.onClick` (and a consumer `onClick`, chained); the Action does not close the toast by itself.
  Source: `toast/action/ToastAction.tsx:41-52`. Test: [L2] `ToastAction.test.tsx` "performs an action when clicked".

#### Swipe to dismiss (Root)

Toast implements its own drag gesture in the Root; it shares only the displacement helper and the constants with the swipe tracker specified in [popup-store-and-handles](../architecture/popup-store-and-handles.md) (PSH-PTR-09…28). Same values: dismiss threshold 40 px, reverse-cancel threshold 10 px, √ damping, 1 px activation. Differences: no velocity or release-velocity logic, no `swipeThreshold` option, no scrollable-ancestor checks, no touch-event (`touchstart`/`touchmove`) path (only pointer events plus a native `touchmove` default-prevention), no `onRelease` override, and a two-axis **axis lock** that the tracker lacks. Definitions used below: *displacement* of a delta `(dx, dy)` in a direction is `−dy` for `up`, `dy` for `down`, `−dx` for `left`, `dx` for `right` (`utils/useSwipeDismiss.ts:41-54`); *allowed directions* is the list of TST-API-14; an axis is *allowed* if at least one of its directions is.

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `pointerdown` on the Root | button ≠ 0 (not primary) | ignored | TST-PTR-08 |
| `pointerdown` (touch) | any target in the Root | pause timers (before the target check) | TST-PTR-09 |
| `pointerdown` | target inside `button, a, input, textarea, [role="button"], [data-base-ui-swipe-ignore], [data-swipe-ignore]` | no gesture | TST-PTR-08 |
| `pointerdown` | otherwise, swipe enabled | start gesture (TST-PTR-10) | TST-PTR-10 |
| first `pointermove` of the gesture | | re-anchor the drag origin to this position | TST-PTR-11 |
| `pointermove` (same pointer) | | `preventDefault`; update baselines, lock axis, arm direction, apply damped offset | TST-PTR-11…16, TST-PTR-23 |
| `pointerup` on the Root or the document | same pointer | release: dismiss or snap back | TST-PTR-17, 18, 20 |
| `pointercancel` on the Root or the document | same pointer | cancel: snap back | TST-PTR-19 |
| native `touchmove` inside the Root | gesture active | `preventDefault` (blocks native scrolling) | TST-PTR-21 |
| any of the above | anchored toast (`positionerProps.anchor !== undefined`) | no swipe handlers at all | TST-PTR-22 |

- **TST-PTR-08** — MUST. A `pointerdown` on the Root starts a gesture only if `event.button === 0` and the event target (composed-path aware, UTL-DOM-22) has no ancestor-or-self matching `button,a,input,textarea,[role="button"],[data-base-ui-swipe-ignore],[data-swipe-ignore]` (the Root element itself included in the search). Otherwise nothing is set — no `data-swiping`, no capture.
  Source: `toast/root/ToastRoot.tsx:39,240-258`; `internals/constants.ts:8-12`. Test: [L3] `ToastRoot.test.tsx` "does not start a swipe from a non-primary pointer button", "does not start swiping from elements with the data-base-ui-swipe-ignore attribute", "does not start swiping from elements with the legacy data-swipe-ignore attribute". Docs: `components/toast/page.mdx` §General usage.
- **TST-PTR-09** — MUST. A primary-button touch `pointerdown` on the Root pauses the timers (TST-STA-21) even when the target is an interactive/ignored element; mouse and pen presses do not pause by themselves (hover already does).
  Source: `toast/root/ToastRoot.tsx:241-247`. Test: none.
- **TST-PTR-10** — MUST. Gesture start: the pointer id is recorded; the drag origin and the cancel baseline are set to the press position; the element's current translation and scale are read from its computed `transform` (UTL-DOM-24) and become the *initial transform* and the initial drag offset; `hovering := true` (expanding the stack); `swiping := true` (`data-swiping`), "real swipe" `false`, axis lock cleared, "first move" armed; `pointerup`/`pointercancel` listeners are attached to the document (replacing any from a previous gesture); and pointer capture is requested on the element.
  Source: `toast/root/ToastRoot.tsx:260-291`. Test: [L4] `ToastRoot.test.tsx` "applies [data-swiping] attribute when swiping"; `ToastViewport.test.tsx` "keeps expanded when a touch swipe is canceled without leaving the viewport" (`data-expanded` from the press).
- **TST-PTR-11** — MUST. The first `pointermove` of a gesture re-anchors the drag origin to the move's position (so the press-to-first-move gap, which can be large on iOS, is not counted). Every move of the active pointer calls `preventDefault()`.
  Source: `toast/root/ToastRoot.tsx:294-307`. Test: [L3] `ToastRoot.test.tsx` `simulateSwipe` helper (all swipe tests send an initial 1 px move for this reason). Docs: CHANGELOG.md v1.0.0-alpha.8 "Fix swipe jump on iOS (#1785)".
- **TST-PTR-12** — MUST. Cancel baseline: on each move, per axis, when the pointer is moving back toward the baseline — `movementY < 0` while `clientY > baseline.y`, or `movementY > 0` while `clientY < baseline.y` (same for x) — the baseline on that axis is reset to the current coordinate. (`movementX/Y` are the event's own properties; when they are absent/zero the baseline never moves.) The *cancel delta* is the current position minus the baseline; the *drag delta* is the current position minus the origin.
  Source: `toast/root/ToastRoot.tsx:309-328`. Test: [L3] `ToastRoot.test.tsx` "cancels a vertical swipe when the pointer changes its mind", "cancels a horizontal swipe when the pointer changes its mind".
- **TST-PTR-13** — MUST. Activation and axis lock: the gesture becomes a *real swipe* on the first move whose drag delta has Euclidean length ≥ 1 px. At that moment, if both axes are allowed, the gesture is locked to `horizontal` when `|dx| > |dy|`, else `vertical`; with a single allowed axis no lock is set. The lock never changes for the rest of the gesture.
  Source: `toast/root/ToastRoot.tsx:38,330-348`. Test: [L3] `ToastRoot.test.tsx` "locks to a single axis on the first move of a two-axis swipe", "locks the gesture to the horizontal axis when it starts sideways", "locks the gesture to the vertical axis when it starts upright". Docs: CHANGELOG.md v1.7.0 "Fix swipe direction locking for two-axis swipes (#5295)".
- **TST-PTR-14** — MUST. Arming: while no direction is armed, each move computes a candidate: with a `vertical` lock, `down` if `dy > 0`, `up` if `dy < 0`, none if `0`; with a `horizontal` lock, `right`/`left` by the sign of `dx`, none if `0`; without a lock, `right`/`left` by the sign of `dx` when `|dx| ≥ |dy|` (a `0` delta yields `left`), else `down`/`up` by the sign of `dy`. If the candidate is in the allowed directions it becomes the *armed direction*, its displacement at that moment is recorded as the *armed displacement*, and `swipeDirection` state (`data-swipe-direction`) is set to it; otherwise nothing is armed yet.
  Source: `toast/root/ToastRoot.tsx:350-374`. Test: [L3] `ToastRoot.test.tsx` "locks the gesture to the horizontal axis when it starts sideways" (no attribute for `left`/`0`, then `right`), "cancels a vertical swipe when the pointer changes its mind" (`data-swipe-direction="up"`).
- **TST-PTR-15** — MUST. Once a direction is armed, each move computes the displacement of the *cancel delta* in that direction: if it exceeds 40 px the gesture is marked not-cancelled and `swipeDirection` is (re)set to the armed direction; otherwise, when the allowed directions do not contain both `left` and `right` and do not contain both `up` and `down`, and `armedDisplacement − currentDisplacement ≥ 10`, the gesture is marked *cancelled* (a change of mind). On an axis that allows both directions a reversal is never a cancellation (the opposite direction is a valid dismissal).
  Source: `toast/root/ToastRoot.tsx:36,375-390`. Test: [L3] `ToastRoot.test.tsx` "cancels a vertical swipe when the pointer changes its mind", "cancels a horizontal swipe when the pointer changes its mind", "right + left", "up + down". Docs: CHANGELOG.md v1.0.0-beta.7 "Fix multiple swipe directions on same axis (#3392)".
- **TST-PTR-16** — MUST. Offset and damping: a delta component in a direction that is not allowed (`dx > 0` without `right`, `dx < 0` without `left`, likewise for y) is damped to `sign(d)·|d|^0.5`; an allowed component is used as-is. The new drag offset is the initial transform plus the (damped) delta, applied on the x axis only if the gesture is not locked `vertical` and the x axis is allowed, and on the y axis only if not locked `horizontal` and the y axis is allowed. The offset drives TST-DOM-13/14 on every move.
  Source: `toast/root/ToastRoot.tsx:37,180-197,392-407`. Test: [L3] `ToastRoot.test.tsx` "locks to a single axis on the first move of a two-axis swipe" (`--toast-swipe-movement-x` stays `0px`), "resets drag state after releasing a far swipe even when the release lands on the document" (damped opposite-direction drag).
- **TST-PTR-17** — MUST. Release (`pointerup` of the active pointer, not cancelled): the gesture ends (`swiping := false`, lock cleared, document listeners removed); let `(dx, dy)` = final drag offset − initial transform; the **first** allowed direction, in the order given by `swipeDirection`, whose displacement of `(dx, dy)` exceeds 40 px becomes the dismiss direction: `swipeDirection` state is set to it and the toast is closed (TST-STA-11). The drag offset is not reset, so TST-DOM-13 keeps the released delta during the exit.
  Source: `toast/root/ToastRoot.tsx:35,199-233`. Test: [L3] `ToastRoot.test.tsx` "dismisses with a real %s pointer swipe" (×4), "closes toast when swiping in the specified direction beyond threshold", "dismisses toast when swiped down/left/right…", "allows swiping in multiple directions when specified", "clears swipe state when re-adding a swipe-dismissed toast" (`data-ending-style` + `data-swipe-direction="right"`). (L4 for the close clause)
- **TST-PTR-18** — MUST. Release with no direction over the threshold, or in a non-allowed direction only: the drag offset snaps back to the initial transform (movement variables `0px`) and `swipeDirection` is cleared; the toast stays open.
  Source: `toast/root/ToastRoot.tsx:234-237`. Test: [L3] `ToastRoot.test.tsx` "does not close toast when swiping in the specified direction below threshold", "does not close toast when swiping in a non-specified direction", "does not dismiss when swiped in non-specified direction", "does not dismiss when swipe distance is below threshold".
- **TST-PTR-19** — MUST. `pointercancel` of the active pointer, or a release of a gesture marked cancelled (TST-PTR-15), ends the gesture and snaps back exactly as TST-PTR-18, without evaluating the threshold.
  Source: `toast/root/ToastRoot.tsx:213-217`. Test: [L3] `ToastRoot.test.tsx` "resets drag state after a document pointercancel", "cancels a vertical swipe when the pointer changes its mind"; `ToastViewport.test.tsx` "keeps expanded when a touch swipe is canceled without leaving the viewport" (`data-swiping` removed).
- **TST-PTR-20** — MUST. The release/cancel is honored whether the event is delivered to the Root or to the document (listeners are attached to both for the gesture's duration); events for a different pointer id are ignored. After any release the inline `transition`/`transform` of TST-DOM-14 are gone and a new gesture can start.
  Source: `toast/root/ToastRoot.tsx:199-206,283-289,470-473`. Test: [L3] `ToastRoot.test.tsx` "resets drag state after releasing a far swipe even when the release lands on the document", "resets drag state after a document pointercancel". Docs: CHANGELOG.md v1.4.0 "Fix stuck drag state on release (#4742)".
- **TST-PTR-21** — MUST. While swiping is enabled, a non-passive native `touchmove` listener on the Root calls `preventDefault()` on touch moves whose target is inside the Root **only while a gesture is active** (a pointer id is recorded); before the gesture starts and after it ends, touch moves are not prevented.
  Source: `toast/root/ToastRoot.tsx:423-443`. Test: [L3] `ToastRoot.test.tsx` "prevents native touchmove only during an active touch swipe".
- **TST-PTR-22** — MUST. When the toast is anchored (`toast.positionerProps.anchor !== undefined`, including `null`), the Root attaches no pointer handlers and no `touchmove` listener: a drag never sets `data-swiping`, never moves the toast and never dismisses it.
  Source: `toast/root/ToastRoot.tsx:60-67,423-426,470-473`. Test: [L4] `ToastRoot.test.tsx` "ignores swipe gestures when toast is anchored".
- **TST-PTR-23** — MUST. Pointer moves of the active pointer call `preventDefault()` (suppresses text selection, e.g. in Safari); moves of other pointers are ignored entirely.
  Source: `toast/root/ToastRoot.tsx:294-300`. Test: none.
- **TST-PTR-24** — MUST. Swipe state is purely per Root instance and is reset when the instance is reinitialised for a new toast lifecycle (TST-ANI-11) and when the instance unmounts (document listeners removed).
  Source: `toast/root/ToastRoot.tsx:157-163,174-178`. Test: [L4] `ToastRoot.test.tsx` "clears swipe state when re-adding a swipe-dismissed toast", "clears swipe state when a retained root is reused for another toast".

### 6.3 Focus management

- **TST-FOC-01** — MUST. On <kbd>F6</kbd> (TST-KEY-01): the document's current active element (shadow-DOM aware, UTL-DOM-19) is stored as the *previous-focus element*, the Viewport is focused with `preventScroll: true`, the timers are paused and `focused := true` (so the stack expands even though the Viewport itself may not be focus-visible).
  Source: `toast/viewport/ToastViewport.tsx:59-65`. Test: [L4] `ToastViewport.test.tsx` "gets focused when F6 is pressed", "pauses timers when the viewport is focused".
- **TST-FOC-02** — MUST. When a focus guard receives focus **from the Viewport element** (`relatedTarget` is the Viewport, i.e. Tab pressed while the Viewport is focused), focus moves to the Root of the first toast in the list that is neither ending nor limited; if there is none, focus returns to the previous-focus element.
  Source: `toast/viewport/ToastViewport.tsx:104-119`. Test: [L4] `ToastViewport.test.tsx` "focuses first toast upon tab after viewport is focused", "skips toasts animating out when tabbing into the viewport", "returns focus when no toast can receive focus", "returns focus to the trigger when every toast is closed".
- **TST-FOC-03** — MUST. When a focus guard receives focus from anywhere else (Shift+Tab out of the first toast, Tab out of the last toast's last tabbable element, or Tab from the page onto the outer guard), focus returns to the previous-focus element.
  Source: `toast/viewport/ToastViewport.tsx:104-119`. Test: [L4] `ToastViewport.test.tsx` "returns focus to previous element when pressing shift+Tab on first toast", "returns focus to previous element when pressing shift+Tab on last toast".
- **TST-FOC-04** — MUST. "Return focus to the previous-focus element" means calling `focus({ preventScroll: true })` on the stored element (no-op when none is stored).
  Source: `toast/store.ts:398-400`. Test: [L4] `ToastViewport.test.tsx` "restores focus and resumes timers on shift+Tab out of the focused viewport".
- **TST-FOC-15** — MUST. The Viewport `focus` handler ignores the focus event caused by a guard hand-off (the guard sets a one-shot flag that the next Viewport focus consumes), so TST-FOC-05 does not run for it.
  Source: `toast/viewport/ToastViewport.tsx:31,105,181-185`. Test: none.
- **TST-FOC-05** — MUST. A `focus` event on the Viewport (or bubbling from inside it), when the store is not already `focused`, sets `focused := true` and pauses the timers iff the document's active element matches `:focus-visible` (TST-A11Y-02); a non-focus-visible focus (mouse click into a toast) changes nothing.
  Source: `toast/viewport/ToastViewport.tsx:181-198,221`. Test: [L4] `ToastViewport.test.tsx` "keeps timers paused while still hovering/focused after the other interaction ends" (focusing the Action pauses), "pauses timers when the viewport is focused".
- **TST-FOC-06** — MUST. A `blur` inside the Viewport whose `relatedTarget` is outside the Viewport (or `null`), while `focused` is `true`, sets `focused := false` and resumes the timers unless the store is still hovering or the window is unfocused. A blur whose `relatedTarget` is inside the Viewport is ignored.
  Source: `toast/viewport/ToastViewport.tsx:200-209,222`. Test: [L4] `ToastViewport.test.tsx` "resumes timers when the viewport is blurred", "keeps timers paused when shift+Tab returns focus inside the viewport", "keeps timers paused on viewport blur while the window is blurred", "keeps timers paused while still hovering after the other interaction ends".
- **TST-FOC-07** — MUST. When a toast is closed (TST-STA-11, any trigger) while the Viewport exists, the active element is inside it, and that element is focus-visible: focus moves to the Root of the nearest non-ending toast **after** the closed one in list order; if none, to the nearest non-ending toast **before** it; the hand-off uses the DOM handle each Root registered for its toast id (TST-ANI-02).
  Source: `toast/store.ts:319,480-515`. Test: [L4] `ToastViewport.test.tsx` "moves focus past toasts animating out when one is closed"; `ToastRoot.test.tsx` "moves focus to the next toast when closing in an index-keyed list", "registers an active toast after its root remounts".
- **TST-FOC-08** — MUST. If no non-ending toast remains after a single close, or on close-all, focus returns to the previous-focus element (TST-FOC-04), under the same "focus is inside the viewport and focus-visible" precondition.
  Source: `toast/store.ts:480-493,510-514`. Test: [L4] `ToastViewport.test.tsx` "returns focus to the trigger when every toast is closed"; `ToastRoot.test.tsx` "closes when pressing escape" (Escape on the only toast returns focus).
- **TST-FOC-09** — MUST. When the active element is outside the Viewport, or is not focus-visible, closing never moves focus.
  Source: `toast/store.ts:481-487`. Test: [L4] `ToastViewport.test.tsx` "leaves focus alone when it is outside the viewport".
- **TST-FOC-10** — MUST. Limited toasts (inert, TST-DOM-09) and ending toasts are skipped by every automatic focus target computation (TST-FOC-02/07); limited toasts are also skipped by native Tab because they are inert.
  Source: `toast/viewport/ToastViewport.tsx:109-112`; `toast/store.ts:498-508`. Test: [L4] `ToastViewport.test.tsx` "returns focus when no toast can receive focus", "skips toasts animating out when tabbing into the viewport".
- **TST-FOC-11** — MUST. A window `blur` event whose target is the window itself (capture phase, while toasts exist) sets `isWindowFocused := false` and pauses the timers; `hovering`/`focused` are unchanged, so a later `mouseleave` or Viewport blur does not resume them while the window stays unfocused.
  Source: `toast/viewport/ToastViewport.tsx:68-75,98`. Test: [L4] `ToastViewport.test.tsx` "resumes timers when the window regains focus", "keeps timers paused on mouseleave while the window is blurred", "keeps timers paused on viewport blur while the window is blurred".
- **TST-FOC-12** — MUST. A window `focus` event (capture phase) with no `relatedTarget`: if its target is the window, or is outside the Viewport, or the active element is not focus-visible, the timers are resumed immediately; in every case `isWindowFocused := true` is set after a 0 ms macrotask (so a focus landing inside the viewport can mark `focused` first). Focus events with a `relatedTarget` are ignored.
  Source: `toast/viewport/ToastViewport.tsx:77-94,99`. Test: [L4] `ToastViewport.test.tsx` "resumes timers when the window regains focus" (4 s remaining after 1 s elapsed). Docs: CHANGELOG.md v1.3.0 "Fix timers not resuming after window regains focus (#4438)".
- **TST-FOC-13** — MUST. The previous-focus element is recorded only by <kbd>F6</kbd> and is never cleared (a later F6 overwrites it); the focus guards exist only while it is set and the list is non-empty (TST-DOM-04).
  Source: `toast/viewport/ToastViewport.tsx:61,239`. Test: [L4] `ToastViewport.test.tsx` "returns focus when no toast can receive focus".
- **TST-FOC-14** — MUST. The global listeners (F6, window blur/focus, document touch) are bound on the Viewport's owner window/document when the list becomes non-empty and the Viewport element exists, unbound when the list becomes empty, and bound again on the next non-empty cycle — exactly once per cycle.
  Source: `toast/viewport/ToastViewport.tsx:45-102`. Test: [L4] `ToastViewport.test.tsx` "rebinds owner-document listeners once across empty store cycles".

### 6.4 Dismissal

Toast has no outside-press, scroll or ancestor-close dismissal. The dismissal routes are:

| Route | Requirement |
|---|---|
| Auto-dismiss timer | TST-STA-18, TST-STA-28 |
| Close button click | TST-PTR-26 |
| Escape while focus is inside the toast | TST-KEY-05 |
| Swipe past 40 px in an allowed direction | TST-PTR-17 |
| `close(id)` / `close()` from the manager | TST-STA-11/12 |
| Re-adding with the same id while ending | TST-STA-06 (silent replacement) |

- **TST-DIS-01** — MUST NOT. A pointer press outside a toast, scrolling, or the closing of an enclosing popup MUST NOT close a toast; the only effect of an outside touch is TST-PTR-05.
  Source: `toast/root/ToastRoot.tsx` (no outside-press handling), `toast/store.ts:402-416`. Test: [L4] `ToastViewport.test.tsx` "collapses and resumes timers on a touch outside the viewport" (toast still present until its timer fires); `useToastManager.test.tsx` "toasts in dialogs are accessible and not aria-hidden".

## 7. Events and callbacks

Toast has no change callbacks with event details ([event-details-and-reasons](../architecture/event-details-and-reasons.md) §3.6 lists it among the components without them): no `reason` literal is ever emitted. Its callbacks are per-toast fields of the toast object, plus the attribute handlers of `actionProps`.

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `toast.onClose` | `() => void` | the toast starts closing (close method, timer, Close click, Escape, swipe, close-all) | — | no |
| `toast.onRemove` | `() => void` | the toast is removed from the list after its exit animation (or immediately when there is none) | — | no |
| `toast.actionProps.onClick` (and any other `actionProps` handler) | DOM handler | the Action button's corresponding DOM event | — | DOM only |
| `manager.promise` return value | `Promise<Value>` | settles with the input promise | — | — |

- **TST-EVT-01** — MUST. `onClose` is called synchronously inside the close operation, after the toast has been marked `ending` in the store and before the focus hand-off; once per toast per lifecycle.
  Source: `toast/store.ts:305-317`. Test: [L1] `useToastManager.test.tsx` "calls onClose when the toast is closed", "calls onClose when the toast auto-dismisses".
- **TST-EVT-02** — MUST. `onClose` is not called again for a toast that is already ending when it is closed again (including by a `close()` issued from inside another toast's `onClose`).
  Source: `toast/store.ts:313-317`. Test: [L1] `createToastManager.test.tsx` "does not call onClose when closing toasts that are already ending". Docs: CHANGELOG.md v1.2.0 "Prevent duplicate `onClose` calls for ending toasts (#4280)".
- **TST-EVT-03** — MUST. `onRemove` is called synchronously when the toast is removed from the list by its Root on animations finished (TST-ANI-05); it is called before the list write, exactly once, and never for a toast that is no longer in the list.
  Source: `toast/store.ts:149-163`; `toast/root/ToastRoot.tsx:105-113`. Test: [L1] `useToastManager.test.tsx` "calls onRemove when the toast is removed"; `store.test.ts` "does not invoke onRemove for a toast that is no longer in the store".
- **TST-EVT-04** — MUST. `onRemove` is not called when an ending toast is silently replaced by a re-add with the same id (TST-STA-06); the replacement's own `onRemove` (if any) is called when the replacement is later removed.
  Source: `toast/store.ts:172-175`. Test: [L1] `useToastManager.test.tsx` "does not call onRemove when replacing an ending toast", "calls onRemove once after replacing an ending toast and later removing the replacement".
- **TST-EVT-05** — MUST. A toast whose Root is never rendered (no Viewport/Root for it) is never removed and its `onRemove` never fires; removal is driven solely by the Root's completion watcher.
  Source: `toast/root/ToastRoot.tsx:105-113` (only caller of `removeToast` besides TST-STA-06). Test: [L2] `useToastManager.test.tsx` "calls onRemove once after replacing an ending toast and later removing the replacement" (`onRemove` fires only after the Viewport is shown and the toast closed again).
- **TST-EVT-06** — MUST. Manager events are delivered synchronously, in call order, to every Provider subscribed at that moment; a Provider that subscribes later does not receive earlier events.
  Source: `toast/createToastManager.ts:15-17,22-27`; `toast/provider/ToastProvider.tsx:34-57`. Test: [L1] `createToastManager.test.tsx` "adds a toast"; `store.test.ts` (none for late subscription).
- **TST-EVT-07** — MUST. The promise returned by `promise(p, …)` (from the hook, or from a manager with a subscribed Provider) fulfills with `p`'s value after the success update has been applied.
  Source: `toast/store.ts:333-343,361`. Test: [L1] `useToastManager.test.tsx` "displays success state as description after promise resolves"; experiment `docs/src/app/(private)/experiments/toast.tsx:104-117` (`.then(data => …)`).
- **TST-EVT-08** — MUST. When `p` rejects, the returned promise rejects with the same reason after the error update has been applied; the consumer must handle that rejection.
  Source: `toast/store.ts:344-353`. Test: [L1] `useToastManager.test.tsx` "displays error state as description after promise rejects" (`.catch`), `createToastManager.test.tsx` "adds a toast with the loading state that is updated with the error state".
- **TST-EVT-09** — MUST. Every attribute in `toast.actionProps` is applied to the Action element (TST-DOM-19); its handlers receive the DOM event and chain with consumer handlers of the same name.
  Source: `toast/action/ToastAction.tsx:44-52`. Test: [L2] `ToastAction.test.tsx` "performs an action when clicked", "forwards actionProps to the button".
- **TST-EVT-10** — MUST. Store calls made from inside `onClose` (for example `close()` for every toast) are applied immediately and consistently: the outer close continues with the state it already wrote, and already-ending toasts are not re-notified (TST-EVT-02).
  Source: `toast/store.ts:313-317`. Test: [L1] `createToastManager.test.tsx` "does not call onClose when closing toasts that are already ending".
- **TST-EVT-11** — MUST. `useToastManager().toasts` reflects every store write (add, update, close, remove, limit change) in the consumer's next render; the four methods are stable across renders.
  Source: `toast/useToastManager.ts:12-23`. Test: [L2] `useToastManager.test.tsx` "replaces a closing toast when adding again with the same id" (`toast-count` text), "increments updateKey when updating a toast".

## 8. Form integration

Not applicable — Toast renders no form control, has no `name`/`value`, and does not participate in Field or Form.

## 9. Lifecycle, mounting and animation

Vocabulary per conventions §4: **post-commit, pre-paint**, **after paint**, **next frame**, **on animations finished** ([transitions-and-mounting](../architecture/transitions-and-mounting.md), TRN-ALG-03). Toast does not use the TRN status machine; its `transitionStatus` is a store field (§4.8) and the Root consumes only the completion watcher (`useOpenChangeComplete`, TRN §6.3) with `open = transitionStatus !== 'ending'`, `batch = false`, on its own element.

### 9.1 Entering

- **TST-ANI-01** — MUST. A fresh toast renders its Root with `data-starting-style` in the first commit (status `'starting'`, TST-STA-07). Post-commit, pre-paint of that commit the Root measures itself (TST-ANI-02); the measurement writes `transitionStatus: undefined` to the store, and the resulting DOM update, applied before the browser paints, removes `data-starting-style`. Because the measurement reads layout (`offsetHeight`), the element's starting styles are computed before the attribute is removed, so a CSS transition from the starting styles plays. No animation frame is involved (compare TRN-STA-04).
  Source: `toast/root/ToastRoot.tsx:118-144,149-167`; `toast/store.ts:227-254`. Test: [L4] `ToastRoot.test.tsx` "clears the starting state and restores the height when re-adding an ending toast" (`data-starting-style` absent after add), "registers an active toast after its root remounts".
- **TST-ANI-02** — MUST. Measurement: the Root temporarily sets its inline `height` to `auto`, reads `offsetHeight`, restores the previous inline `height`, then writes `{ ref: <its element handle>, height: <measured>, transitionStatus: undefined }` for its toast id (an ordinary internal update: no `updateKey` increment, no timer change unless the rules of TST-STA-19 trigger — they do not, since neither `timeout` nor `type` is touched). The write is ignored while the toast is ending (TST-ANI-10). It runs post-commit, pre-paint when the Root mounts, whenever the Root starts rendering a toast with status `'starting'`, and whenever the toast id it renders changes.
  Source: `toast/root/ToastRoot.tsx:118-167`. Test: [L4] `ToastRoot.test.tsx` "recalculates height when content mutates", "registers an active toast after its root remounts" (new height measured after remount); `ToastViewport.test.tsx` "sets the measured frontmost toast height CSS variable".
- **TST-ANI-03** — MUST. While a `Toast.Content` is rendered, it re-measures its toast (TST-ANI-02) post-commit, pre-paint on mount and then on every size change (resize observation) and every DOM mutation of its subtree (child list, subtree, character data); observer-driven re-measurements are applied in a synchronous commit. Environments without `ResizeObserver`/`MutationObserver` get only the mount measurement.
  Source: `toast/content/ToastContent.tsx:22-42`. Test: [L4] `ToastRoot.test.tsx` "recalculates height when content mutates". Docs: CHANGELOG.md v1.0.0-beta.7 "Recalculate content height when layout size is fixed (#3359)".

### 9.2 Exiting and removal

- **TST-ANI-04** — MUST. Closing a toast sets `data-ending-style` on its Root in the commit that applies the `ending` status (same commit as `height: 0`, TST-STA-11); the Root stays in the DOM with that attribute until TST-ANI-05.
  Source: `toast/store.ts:305-311`; `toast/root/ToastRoot.tsx:28-29,498`. Test: [L4] `ToastRoot.test.tsx` "clears the starting state and restores the height when re-adding an ending toast", "keeps stacking intact when re-adding an ending toast among other toasts" (`data-ending-style` on the closed toast).
- **TST-ANI-05** — MUST. When `transitionStatus` becomes `'ending'`, the Root arms the shared completion watcher on its element (after paint; TRN-ANI-17): the first inspection of the element's animations happens on the next frame (TRN-ANI-11); when every finite animation/transition on the Root element has finished (TRN-ANI-04…07), the toast is removed from the store in a synchronous commit before the next paint (TRN-ANI-09), calling `onRemove` (TST-EVT-03). The completion callback re-checks that the toast is still ending before removing. Server: relaxed, see §9.
  Source: `toast/root/ToastRoot.tsx:105-113`; `internals/useOpenChangeComplete.tsx:9-28`; `internals/useAnimationsFinished.ts:46-164`. Test: [L4] `ToastViewport.test.tsx` "collapses a deferred mouseleave after a closing toast is removed while blurred" (mocked `getAnimations`); `useToastManager.test.tsx` "calls onRemove when the toast is removed".
- **TST-ANI-06** — MUST. With no finite animation on the Root (or with the animations-disabled test flag / no `getAnimations`, TRN-ANI-15), the toast is removed immediately when the watcher is armed, so in such environments `close` removes the Root synchronously within the same user event. Server: relaxed, see §9.
  Source: `internals/useAnimationsFinished.ts:93-99`. Test: [L4] `useToastManager.test.tsx` "closes a toast" (root gone right after the click), "closes all toasts"; `ToastClose.test.tsx` "closes the toast when clicked".
- **TST-ANI-07** — MUST. Re-adding an ending toast under the same id (TST-STA-06) while its Root instance is retained (same list key): the pending exit wait is abandoned (open flips back to `true`, TRN-ANI-08), the Root re-measures (TST-ANI-02) so `data-starting-style` is cleared and `--toast-height` is restored to the measured value, the toast is frontmost (`--toast-index` `0`, its Content not `data-behind`), and unrelated toasts keep their status and shift back by one.
  Source: `toast/root/ToastRoot.tsx:105-113,149-167`; `toast/store.ts:169-191`. Test: [L4] `ToastRoot.test.tsx` "clears the starting state and restores the height when re-adding an ending toast", "keeps stacking intact when re-adding an ending toast among other toasts". Docs: CHANGELOG.md v1.7.0 "Fix re-adding a closing toast (#5258)".
- **TST-ANI-08** — MUST. When a Root instance starts rendering a different toast id (index-keyed lists), it reinitialises for that toast: swipe state is reset (TST-ANI-11), the element handle is registered under the new id and the new toast is measured, even if the new toast's status is not `'starting'`.
  Source: `toast/root/ToastRoot.tsx:146-167`. Test: [L4] `ToastRoot.test.tsx` "clears swipe state when a retained root is reused for another toast", "moves focus to the next toast when closing in an index-keyed list".
- **TST-ANI-09** — MUST. A Root that unmounts and remounts for the same, still-open toast re-registers its new element handle and re-measures, so focus hand-off (TST-FOC-07) and Tab (TST-FOC-02) target the remounted element and `--toast-height` reflects its current size.
  Source: `toast/root/ToastRoot.tsx:149-167` (first run of a new instance has no previous id). Test: [L4] `ToastRoot.test.tsx` "registers an active toast after its root remounts".
- **TST-ANI-10** — MUST. Measurement writes (TST-ANI-02) and all other updates are ignored while a toast is ending: its `height` stays `0` and its status stays `'ending'`, so the completion watcher is never disarmed by a late content-observer callback.
  Source: `toast/store.ts:239-244`. Test: [L1] `store.test.ts` "ignores height recalculations while a toast is transitioning out".
- **TST-ANI-11** — MUST. When the Root reinitialises for a new lifecycle (TST-ANI-07/08) and it had rendered a toast before, its swipe state is cleared: `swipeDirection` unset (attribute removed), drag offset and initial transform zeroed (movement variables `0px`, no inline transform).
  Source: `toast/root/ToastRoot.tsx:157-163`. Test: [L4] `ToastRoot.test.tsx` "clears swipe state when re-adding a swipe-dismissed toast", "clears swipe state when a retained root is reused for another toast".
- **TST-ANI-12** — MUST. The Root never receives the shared `transition: none` mount style (TRN-DOM-05) — only the Positioner does (TST-DOM-21). The Root's only inline `transition` is the swipe freeze of TST-DOM-14.
  Source: `toast/root/ToastRoot.tsx:445-459,463-483` (no `getDisabledMountTransitionStyles`). Test: none.
- **TST-ANI-13** — MUST. There is no `keepMounted`, `actionsRef`, `onOpenChangeComplete` or `hidden="until-found"` behavior on any Toast part; a toast is in the DOM exactly while it is in the list and rendered by the consumer.
  Source: `toast/root/ToastRoot.tsx:545-556` (props). Test: none (absence).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- TST-ANI-05 — via TRN-ANI-09 (adopted above): the removal on animations finished lands in the first render batch after the `transitions` module's completion message, about two round trips later; the optimistic convergent write keeps the element invisible meanwhile.
- TST-ANI-06 — via TRN-ANI-09: with no finite animation the removal still lands in the first render batch after the completion message, not within the same user event.
- TST-STA-26 — RND-STA-01 for mirrored fields: `data-swiping` and `data-swipe-direction` are JS-written at frame timing; the Root state's `swiping`/`swipeDirection` (and so `ClassValue`/`StyleValue`) update one round trip later.

## 10. Positioning

Only `Toast.Positioner`/`Toast.Arrow` position anything; a Root placed directly in the Viewport is laid out by the consumer's CSS using the variables of §5.2. The mechanism is the shared anchor positioning of [floating-and-positioning](../architecture/floating-and-positioning.md); this section states only what Toast passes.

- **TST-POS-01** — MUST. The Positioner calls the shared anchor positioning with: `anchor` per TST-POS-03; `positionMethod` default `'absolute'`; `side` default `'top'`; `align` default `'center'`; `sideOffset` and `alignOffset` default `0`; `collisionBoundary` default `'clipping-ancestors'`; `collisionPadding` default `5`; `arrowPadding` default `5`; `sticky` default `false`; `disableAnchorTracking` default `false`; `collisionAvoidance` default `{ fallbackAxisSide: 'end' }`; and the fixed values `mounted: true`, `keepMounted: true`, a floating root state that is always `open: true` with a no-op open-change handler, `floating` = the Positioner element, `reference` = the anchor.
  Source: `toast/positioner/ToastPositioner.tsx:39-56,63-90`; `internals/constants.ts:26-28`. Test: [L2] `ToastPositioner.test.tsx` "falls back to the viewport when no anchor is provided" (`top`/`center` defaults), "positions an anchored toast against its anchor element" (`side: 'bottom'`, `sideOffset: 8` → top edge at anchor bottom + 8). (L4 for the pixel assertions)
- **TST-POS-02** — MUST. For every positioning prop, the value is resolved as: the Positioner element prop when not `undefined`; else the same key of `toast.positionerProps` when not `undefined`; else the default of TST-POS-01. The two sources are merged key by key (an element `side` with a toast `align` yields both).
  Source: `toast/positioner/ToastPositioner.tsx:35-56`. Test: [L2] `ToastPositioner.test.tsx` "lets positioner props override the ones carried on the toast" (`side="left"` element prop, `align: 'end'` from the toast).
- **TST-POS-03** — MUST. The anchor is the resolved `anchor` value if it is a DOM `Element`; any other value (`null`, `undefined`, a DOM handle, a function, a virtual element) yields no anchor, and the Positioner still renders with its default placement attributes and no reference-based coordinates.
  Source: `toast/positioner/ToastPositioner.tsx:42,63`. Test: [L4] `ToastPositioner.test.tsx` "falls back to the viewport when no anchor is provided".
- **TST-POS-04** — MUST. The Positioner is always treated as mounted and open: positioning runs for the whole life of the element (there is no closed state), and the Arrow receives live `side`/`align`/`uncentered`/arrow styles from it.
  Source: `toast/positioner/ToastPositioner.tsx:65-90,109-111`; `toast/arrow/ToastArrow.tsx:20-32`. Test: [L4] `ToastArrow.test.tsx` "mirrors the resolved side of its positioner".
- **TST-POS-05** — MUST. Anchored toasts are meant to live in a separate Provider from stacked ones (so stack variables and the frontmost-height clamp do not apply to them); the library does not enforce this, and an anchored toast in a shared Provider still gets `--toast-index`/`--toast-offset-y` and counts toward `limit`.
  Source: `toast/positioner/ToastPositioner.tsx:60-61,98-103`. Test: none. Docs: `components/toast/page.mdx` §Anchored toasts ("Anchored toasts should be rendered in a separate `<Toast.Provider>`").

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` | [rendering-and-composition](../architecture/rendering-and-composition.md) §6.1 | Every part; default elements per §2. Root passes `stateAttributesMapping = transitionStatusMapping + { swipeDirection }`; Positioner (via `usePositioner`) passes `popupStateMapping`; all others use the default projection. Viewport puts the consumer's attributes before its `children` wrapper; Close puts the button props getter last; Action puts `actionProps`, the button getter and `children` after the consumer's attributes. |
| `useButton` | [button-behavior](../architecture/button-behavior.md) §6.2 | `Toast.Close`, `Toast.Action`: `native: nativeButton` (default `true`), `disabled: props.disabled`, no `focusableWhenDisabled`, composite inferred (none). |
| `useOpenChangeComplete` / `useAnimationsFinished` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) §6.2/6.3 | Root: `open: transitionStatus !== 'ending'`, `ref: root element`, `batch: false`, `onComplete`: remove the toast if still ending (TST-ANI-05). |
| `transitionStatusMapping` | TRN / RND-DOM-11 | Root only; status values from the store (TST-DOM-07). |
| `getDisabledMountTransitionStyles` | TRN-DOM-05 | Positioner only, keyed on the toast's `transitionStatus` (TST-DOM-21). |
| `FloatingPortalLite` | [popup-store-and-handles](../architecture/popup-store-and-handles.md) PSH-STR-11 | `Toast.Portal` forwards all props (`container`, DOM handle, attributes) unchanged. |
| `FocusGuard` | PSH-STR-08, PSH-DOM-13, PSH-ARIA-05 | Viewport renders up to three with a shared `onFocus` (TST-DOM-04, TST-FOC-02/03). |
| `popupStateMapping`, `CommonPopupDataAttributes` | PSH-DOM-04/06 | Positioner state `{ side, align, anchorHidden }` (no `open` key, so `data-open`/`data-closed` are never emitted). |
| `getDisplacement` (from `useSwipeDismiss.ts`), `BASE_UI_SWIPE_IGNORE_SELECTOR`, `LEGACY_SWIPE_IGNORE_SELECTOR` | PSH (swipe tracker, PSH-PTR-09…28) / UTL-EDGE-19 | Root uses only the pure displacement helper and the ignore selectors; the gesture itself is Toast's own (§6.2). |
| `useAnchorPositioning`, `usePositioner`, `useFloatingRootContext` | [floating-and-positioning](../architecture/floating-and-positioning.md) | Positioner options per TST-POS-01; `usePositioner` with `styles = positionerStyles + --toast-index`, `transitionStatus = toast.transitionStatus`, no `hidden`/`inert`. |
| `ReactStore` (`set`/`update`/`select`/`useState`) | [shared-utilities](../architecture/shared-utilities.md) UTL-STA-07…18 | One `ToastStore` per Provider, created once (`useRefWithInit`), selectors of `toast/store.ts:86-97`; no `context` bag, no controlled keys. |
| `Timeout` | UTL-EVT-01/02 | One instance per timer record (created lazily on first start) for auto-dismiss; one `useTimeout` in the Viewport for the 0 ms window-focus deferral (TST-FOC-12). |
| `generateId('toast')` | UTL-EDGE-10 | Toast ids (TST-API-06). |
| `addEventListener`, `mergeCleanups`, `ownerDocument`, `ownerWindow`, `activeElement`, `contains`, `closest`, `getTarget` | UTL-EVT-13/14, UTL-DOM-18…22 | Viewport global listeners; Root Escape/swipe target checks; store focus hand-off. |
| `matchesFocusVisible` (`floating-ui-react/utils/element.ts:71-82`) | specified as FLT-A11Y-08 (added in the second pass, 2026-10-18); TST-A11Y-02 restates it for Toast | Viewport focus/blur, window focus, store focus hand-off. |
| `getElementTransform` | UTL-DOM-24 | Root reads translation/scale at swipe start (TST-PTR-10). |
| `inertValue` | UTL-DOM-16 | Root `inert` for limited toasts (TST-DOM-09). |
| `visuallyHidden` | UTL-DOM-13 | Alert region container (TST-DOM-05); focus guards (PSH). |
| `useId` | RND-DOM-27…30 | Title/Description ids (TST-API-15). |
| `useStableCallback`, `useOnMount`, `useIsoLayoutEffect` | UTL §8 (React notes) | Stable `recalculateHeight`/`handleSwipeEnd`; store disposal on Provider unmount; pre-paint measurement and prop sync. |

## 12. Edge cases, invariants and dev warnings

All three errors below are thrown unconditionally (not development-only) when a part or hook cannot find its ancestor-provided state; they are specific to Toast.

- **TST-EDGE-01** — MUST. Rendering `Toast.Viewport`, `Toast.Root`, `Toast.Close` or `Toast.Positioner`, or calling `useToastManager()`, outside a `Toast.Provider` throws `Error` with the message `Base UI: useToastManager must be used within <Toast.Provider>.`
  Source: `toast/provider/ToastProviderContext.ts:9-15`. Test: [L2] `ToastViewport.test.tsx` "throws a descriptive error when rendered outside <Toast.Provider>"; `ToastPositioner.test.tsx` "throws a descriptive error when rendered outside <Toast.Provider>".
- **TST-EDGE-02** — MUST. Rendering `Toast.Content`, `Toast.Title`, `Toast.Description`, `Toast.Action` or `Toast.Close` outside a `Toast.Root` throws `Error` with the message `Base UI: ToastRootContext is missing. Toast parts must be used within <Toast.Root>.`
  Source: `toast/root/ToastRootContext.ts:16-24`. Test: [L2] `useToastLabelPart.test.tsx` "throws a descriptive error when rendered outside <Toast.Root>" (Title and Description).
- **TST-EDGE-03** — MUST. Rendering `Toast.Arrow` outside a `Toast.Positioner` throws `Error` with the message `Base UI: ToastPositionerContext is missing. ToastPositioner parts must be placed within <Toast.Positioner>.`
  Source: `toast/positioner/ToastPositionerContext.ts:14-22`. Test: [L2] `ToastArrow.test.tsx` "throws a descriptive error when rendered outside <Toast.Positioner>".
- **TST-EDGE-04** — MUST. `close`, `update` and removal for an id that is not in the list are silent no-ops that leave the store record untouched (same reference).
  Source: `toast/store.ts:150-153,211-215,297-300`. Test: [L1] `store.test.ts` "ignores mutations that target an unknown toast".
- **TST-EDGE-05** — MUST. One manager MAY be passed to several Providers; each subscribed Provider receives every event and acts on its own store (a toast is added to each). A Provider whose `toastManager` prop changes unsubscribes from the old manager and subscribes to the new one.
  Source: `toast/createToastManager.ts:15-27`; `toast/provider/ToastProvider.tsx:34-57`. Test: none.
- **TST-EDGE-06** — MUST. Unmounting a Provider clears every pending auto-dismiss timer of its store; the store object itself is not reused by a later Provider instance.
  Source: `toast/provider/ToastProvider.tsx:18-32`; `toast/store.ts:140-147`. Test: none.
- **TST-EDGE-07** — MUST. The Viewport's global listeners are attached only while the list is non-empty (TST-FOC-14); while it is empty, <kbd>F6</kbd>, window blur/focus and outside touches have no effect.
  Source: `toast/viewport/ToastViewport.tsx:49-52`. Test: [L4] `ToastViewport.test.tsx` "rebinds owner-document listeners once across empty store cycles".
- **TST-EDGE-08** — MUST. All global listeners, `activeElement` reads and focus calls use the Viewport element's **owner** window/document (UTL-DOM-18), so a Viewport portaled into an iframe works against that iframe's window/document and ignores the host page's events.
  Source: `toast/viewport/ToastViewport.tsx:54-55,61,83`; `toast/store.ts:481`. Test: [L4] `ToastViewport.test.tsx` "rebinds owner-document listeners once across empty store cycles".
- **TST-EDGE-09** — MUST. Provider prop values from a render that never commits are never written to the store (TST-API-04): a toast added while such a render is pending still uses the committed `timeout`.
  Source: `toast/provider/ToastProvider.tsx:67-81` (post-commit sync only). Test: [L2] `ToastProvider.test.tsx` "does not sync provider props from an abandoned render".
- **TST-EDGE-10** — MUST. Every per-toast lookup in Root, Positioner, Content and the store is keyed by `toast.id`; passing a toast object that is a copy of the store's (not the same reference) MUST produce identical indices, variables, close behavior and focus hand-off.
  Source: `toast/root/ToastRoot.tsx:99-101`; `toast/store.ts:86-97`. Test: [L2] `ToastRoot.test.tsx` "works correctly when toast objects are recreated (not referentially equal)", "correctly calculates indices for multiple toasts with recreated objects".
- **TST-EDGE-11** — MUST. `limit={0}` marks every toast limited (inert): tabbing from the Viewport finds no focusable toast and returns focus to the previous-focus element (TST-FOC-02).
  Source: `toast/store.ts:74-84`. Test: [L4] `ToastViewport.test.tsx` "returns focus when no toast can receive focus".
- **TST-EDGE-12** — MUST. A toast rendered inside a modal Dialog's subtree (or a Viewport outside the Dialog while the Dialog is open) is not hidden by Toast itself: Toast sets `aria-hidden` only per TST-ARIA-06/07, so toasts remain accessible while a dialog is open.
  Source: `toast/root/ToastRoot.tsx:469`. Test: [L2] `useToastManager.test.tsx` "toasts in dialogs are accessible and not aria-hidden", "high priority toasts in dialogs have correct accessibility structure".
- **TST-I18N-01** — MUST. Swipe directions are physical (`left`/`right` refer to screen x), not mirrored for right-to-left text direction; the Positioner's logical sides (`inline-start`/`inline-end`) are resolved by the shared positioning mechanism (FLT).
  Source: `toast/root/ToastRoot.tsx:62-67,180-197` (no direction lookup). Test: none.
- **TST-A11Y-01** — MUST. High-priority toasts are announced assertively through the hidden alert region of TST-DOM-05 — one `role="alert" aria-atomic="true"` element per such toast, containing the toast's `title` and `description` only (not the rendered Root content) — while the Viewport is not keyboard-focused; this, together with `aria-hidden` on the Root (TST-ARIA-06), prevents a double announcement. Low-priority toasts rely on the Viewport's polite live region (TST-ARIA-01).
  Source: `toast/viewport/ToastViewport.tsx:43,263-272`; `toast/root/ToastRoot.tsx:469`. Test: [L2] `useToastManager.test.tsx` "applies correct ARIA attributes for high priority toasts". Docs: `components/toast/page.mdx` §`add` method ("For high priority toasts, the `title` and `description` strings are what are used to announce the toast to screen readers").
- **TST-A11Y-02** — MUST. "Focus-visible" means `element.matches(':focus-visible')`; it is `true` for a `null` element, in environments where `:focus-visible` is unsupported (the check throws), and in the JSDOM test environment. It gates TST-FOC-05/07/12.
  Source: `floating-ui-react/utils/element.ts:71-82`; `toast/utils/focusVisible.ts:1`. Test: [L4] `ToastRoot.test.tsx` "closes when pressing escape" (browser-only, comment "requires :focus-visible check").
- **TST-A11Y-03** — MUST. The alert region is removed while the Viewport is keyboard-focused (`focused`), since the user is then reading the toasts directly, and reappears when focus leaves if high-priority toasts remain.
  Source: `toast/viewport/ToastViewport.tsx:263`. Test: none.

## 13. Behaviors evidenced by tests

Coverage matrix for all 16 test files plus the two type-level spec files. Tests marked *browser* run only in a real browser (`skipIf(isJSDOM)`); tests marked *jsdom* run only under JSDOM (`skipIf(!isJSDOM)`). `BASE_UI_ANIMATIONS_DISABLED` is `true` for the suite by default (TRN-EDGE-01); tests that clear it are noted.

### `store.test.ts`

| Test | Requirements |
|---|---|
| "keeps toast metadata synchronized after mutations" | TST-STA-01, TST-STA-02, TST-STA-11, TST-STA-13 |
| "ignores height recalculations while a toast is transitioning out" | TST-ANI-10, TST-STA-08 |
| "ignores mutations that target an unknown toast" | TST-EDGE-04 |
| "replaces custom data wholesale when updating a toast" | TST-STA-09 |
| "derives the update from the current toast when given a function" | TST-STA-10, TST-STA-09 |
| "passes a toast without custom data to the updater" | TST-STA-10 |
| "stores a function passed as custom data instead of calling it" | TST-STA-27 |
| "does not call the updater for a missing or ending toast" | TST-STA-08 |
| "keeps a toast added from inside an updater" | TST-STA-10, TST-STA-01 |
| "keeps a toast closed from inside its updater closed" | TST-STA-10, TST-STA-08 |
| "stores a function as the value when re-adding a toast under an existing id" / "replaces custom data when re-adding a toast under an existing id" | TST-STA-05, TST-STA-27 |
| "stores a function passed as custom data on a promise toast" | TST-STA-27, TST-STA-16 |
| "does not invoke onRemove for a toast that is no longer in the store" | TST-STA-13, TST-EVT-03 |
| limit: "recomputes limited flags when the limit changes" | TST-STA-03, TST-STA-04 |
| timer pausing: "re-pauses timers after the last toast is closed and a new one is added" / "re-pauses timers after all toasts are closed and a new one is added" / "re-pauses timers after the last active toast closes while ending toasts remain" | TST-STA-24, TST-STA-25, TST-STA-21 |
| "re-pauses timers after the last timed toast closes while untimed toasts remain" | TST-STA-18, TST-STA-24 |
| "keeps a rescheduled timer paused while expanded, and runs it once collapsed" | TST-STA-19, TST-STA-23, TST-STA-22 |
| "does not extend the remaining time across repeated pause/resume cycles" | TST-STA-21, TST-STA-22 |
| "restarts the full delay when the clock jumped past the timeout before pausing" | TST-STA-22 |
| "re-pauses timers after the last timed toast becomes untimed" | TST-STA-20, TST-STA-24 |
| "accumulates active time across hover cycles so the toast still dismisses" | TST-STA-21, TST-STA-22, TST-STA-28 |

### `root/ToastRoot.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Provider + Viewport) | TST-STR-03; RND-DOM-36…48 |
| "maps the active swipe direction to its data attribute" | TST-DOM-06, TST-STA-26 |
| "sets the vertical offset CSS variable" | TST-DOM-11 |
| "keeps dynamic title and description ids synchronized with mounted label parts" | TST-ARIA-04, TST-ARIA-05, TST-API-15, TST-DOM-16 |
| "recalculates height when content mutates" (*browser*) | TST-ANI-02, TST-ANI-03, TST-DOM-12 |
| "clears the starting state and restores the height when re-adding an ending toast" (*browser*, animations enabled) | TST-ANI-01, TST-ANI-04, TST-ANI-07, TST-DOM-07, TST-DOM-12, TST-STA-06 |
| "registers an active toast after its root remounts" (*browser*) | TST-ANI-09, TST-ANI-02, TST-FOC-02, TST-FOC-07 |
| "keeps stacking intact when re-adding an ending toast among other toasts" (*browser*, animations enabled) | TST-ANI-07, TST-ANI-04, TST-DOM-10, TST-DOM-15 |
| "clears swipe state when re-adding a swipe-dismissed toast" (*browser*, animations enabled) | TST-ANI-11, TST-PTR-17, TST-PTR-24, TST-DOM-06, TST-DOM-13 |
| "clears swipe state when a retained root is reused for another toast" (*browser*, animations enabled) | TST-ANI-08, TST-ANI-11, TST-PTR-24 |
| "moves focus to the next toast when closing in an index-keyed list" (*browser*, animations enabled) | TST-ANI-08, TST-FOC-07, TST-KEY-05 |
| "closes when pressing escape" (*browser*) | TST-KEY-05, TST-FOC-08, TST-A11Y-02 |
| "ignores Escape when focus is in portaled content" | TST-KEY-06 |
| swipe behavior (*browser*): "dismisses with a real %s pointer swipe" ×4 (`left`, `right`, `up`, `down`) | TST-PTR-17, TST-PTR-13, TST-PTR-14 |
| "closes toast when swiping in the specified direction beyond threshold" | TST-PTR-17, TST-API-14 |
| "locks to a single axis on the first move of a two-axis swipe" | TST-PTR-13, TST-PTR-16, TST-DOM-13 |
| "does not close toast when swiping in the specified direction below threshold" / "does not dismiss when swipe distance is below threshold" | TST-PTR-18 |
| "does not close toast when swiping in a non-specified direction" / "does not dismiss when swiped in non-specified direction" | TST-PTR-18, TST-PTR-16 |
| supports multiple swipe directions: "up + right" / "right + left" / "up + down" | TST-PTR-17, TST-PTR-15, TST-API-14 |
| "cancels a vertical swipe when the pointer changes its mind" | TST-PTR-12, TST-PTR-14, TST-PTR-15, TST-PTR-19, TST-DOM-13 |
| "cancels a horizontal swipe when the pointer changes its mind" | TST-PTR-12, TST-PTR-15, TST-PTR-19 |
| "locks the gesture to the horizontal axis when it starts sideways" / "locks the gesture to the vertical axis when it starts upright" | TST-PTR-13, TST-PTR-14, TST-PTR-17 |
| "does not start a swipe from a non-primary pointer button" | TST-PTR-08 |
| "applies [data-swiping] attribute when swiping" | TST-PTR-10, TST-DOM-06 |
| "dismisses toast when swiped down with downward swipe direction" / "dismisses toast when swiped left with leftward swipe direction" / "dismisses toast when swiped right with rightward swipe direction" / "allows swiping in multiple directions when specified" | TST-PTR-17, TST-API-14 |
| "prevents native touchmove only during an active touch swipe" | TST-PTR-21 |
| "does not start swiping from elements with the data-base-ui-swipe-ignore attribute" / "does not start swiping from elements with the legacy data-swipe-ignore attribute" | TST-PTR-08 |
| "ignores swipe gestures when toast is anchored" | TST-PTR-22, TST-API-14 |
| drag behavior regression: "resets drag state after releasing a far swipe even when the release lands on the document" | TST-PTR-16, TST-PTR-18, TST-PTR-20, TST-DOM-13, TST-DOM-14 |
| "resets drag state after a document pointercancel" | TST-PTR-19, TST-PTR-20, TST-DOM-14 |
| object identity: "works correctly when toast objects are recreated (not referentially equal)" | TST-EDGE-10, TST-STR-03, TST-DOM-10, TST-PTR-26 |
| "correctly calculates indices for multiple toasts with recreated objects" | TST-EDGE-10, TST-DOM-10 |

### `viewport/ToastViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Provider) | TST-STR-02, TST-DOM-23; RND-DOM-36…48 |
| "sets the measured frontmost toast height CSS variable" (*browser*) | TST-DOM-03, TST-ANI-02 |
| "rebinds owner-document listeners once across empty store cycles" (*jsdom*) | TST-FOC-14, TST-EDGE-07, TST-EDGE-08, TST-KEY-01, TST-STR-13 |
| "throws a descriptive error when rendered outside <Toast.Provider>" | TST-EDGE-01 |
| "gets focused when F6 is pressed" | TST-KEY-01, TST-FOC-01 |
| "focuses first toast upon tab after viewport is focused" | TST-FOC-02, TST-KEY-04, TST-DOM-04, TST-DOM-08 |
| "returns focus to previous element when pressing shift+Tab on first toast" | TST-FOC-03, TST-FOC-04 |
| "returns focus to previous element when pressing shift+Tab on last toast" | TST-FOC-03, TST-DOM-08, TST-KEY-04 |
| "removes expanded on mouseleave when focus-visible not inside" | TST-PTR-01, TST-PTR-02, TST-DOM-02 |
| "keeps expanded on mouseleave when focus-visible is inside" | TST-PTR-02, TST-FOC-05, TST-STA-14 |
| "keeps expanded during an active touch swipe even if mouseleave fires" | TST-PTR-04, TST-PTR-10, TST-DOM-02 |
| "keeps expanded when a touch swipe is canceled without leaving the viewport" | TST-PTR-04, TST-PTR-19, TST-PTR-10 |
| "collapses after a touch swipe is canceled if mouseleave already fired" | TST-PTR-04 |
| timers: "pauses timers when hovering" | TST-PTR-01, TST-STA-21 |
| "resumes timers when not hovering" | TST-PTR-02, TST-STA-22 |
| "pauses timers when the viewport is focused" | TST-KEY-01, TST-FOC-01 |
| "keeps timers paused while still %s after the other interaction ends" ×2 (`hovering`, `focused`) | TST-PTR-02, TST-FOC-05, TST-FOC-06, TST-STA-14 |
| "restores focus and resumes timers on shift+Tab out of the focused viewport" | TST-KEY-03, TST-FOC-04, TST-FOC-06 |
| "keeps timers paused when shift+Tab returns focus inside the viewport" | TST-KEY-03, TST-FOC-06 |
| "keeps the viewport focused when Tab is pressed without shift" | TST-KEY-04 |
| "collapses and resumes timers on a touch outside the viewport" | TST-PTR-05, TST-DIS-01 |
| "stays expanded on a touch inside the viewport" | TST-PTR-07 |
| "ignores a mouse pointerdown outside the viewport" | TST-PTR-06 |
| "resumes timers when the viewport is blurred" (*jsdom*) | TST-FOC-06 |
| "resumes timers when the window regains focus" (*jsdom*) | TST-FOC-11, TST-FOC-12 |
| "keeps timers paused on mouseleave while the window is blurred" (*jsdom*) | TST-FOC-11, TST-PTR-02 |
| "keeps timers paused on viewport blur while the window is blurred" (*jsdom*) | TST-FOC-11, TST-FOC-06, TST-STA-23 |
| "collapses a deferred mouseleave after a closing toast is removed while blurred" (*jsdom*, animations enabled, mocked `getAnimations`) | TST-PTR-03, TST-STA-15, TST-ANI-05 |
| focus management: "skips toasts animating out when tabbing into the viewport" (*jsdom*) | TST-FOC-02, TST-FOC-10, TST-DOM-04, TST-ARIA-07 |
| "returns focus when no toast can receive focus" (*jsdom*) | TST-FOC-02, TST-FOC-10, TST-FOC-13, TST-EDGE-11, TST-DOM-09 |
| "returns focus to the trigger when every toast is closed" (*jsdom*) | TST-FOC-08, TST-STA-12, TST-FOC-02 |
| "moves focus past toasts animating out when one is closed" (*jsdom*) | TST-FOC-07, TST-FOC-10 |
| "leaves focus alone when it is outside the viewport" (*jsdom*) | TST-FOC-09 |

### `useToastManager.test.tsx` (*jsdom*, fake timers)

| Test | Requirements |
|---|---|
| add: "adds a toast to the viewport that auto-dismisses after 5s by default" | TST-API-01, TST-API-05, TST-STA-07, TST-STA-18, TST-STA-28 |
| "keeps multiple providers isolated when one provider updates" | TST-STR-01 |
| "replaces a closing toast when adding again with the same id" | TST-STA-06, TST-EVT-11 |
| "does not call onRemove when replacing an ending toast" | TST-EVT-04, TST-STA-06 |
| "calls onRemove once after replacing an ending toast and later removing the replacement" | TST-EVT-04, TST-EVT-05 |
| "ignores transitionStatus when upserting an existing toast" | TST-STA-05, TST-API-11 |
| "increments updateKey when adding again with the same id" | TST-STA-05, TST-STA-07 |
| option: timeout "dismisses the toast after the specified timeout" | TST-STA-18 |
| option: title "renders the title" / option: description "renders the description" | TST-API-15 |
| option: type "renders the type" | toast object table (§3.2), TST-DOM-06 |
| option: onClose "calls onClose when the toast is closed" | TST-EVT-01 |
| "calls onClose when the toast auto-dismisses" | TST-EVT-01, TST-STA-28 |
| option: onRemove "calls onRemove when the toast is removed" | TST-EVT-03, TST-STA-13 |
| option: priority "applies correct ARIA attributes for high priority toasts" | TST-ARIA-02, TST-ARIA-03, TST-DOM-05, TST-A11Y-01 |
| promise: "displays success state as description after promise resolves" | TST-STA-16, TST-API-12, TST-EVT-07 |
| "displays error state as description after promise rejects" | TST-STA-16, TST-API-12, TST-EVT-08 |
| "passes data when success is a function" / "passes data when error is a function" / "supports custom options" | TST-API-12 |
| "accepts a function that returns full options for the success state" | TST-API-12, TST-STA-16 |
| "does not reopen a dismissed promise toast when it resolves" | TST-STA-17, TST-STA-08 |
| timeout handling: "auto-dismisses success toast after default timeout when promise resolves" / "auto-dismisses error toast after default timeout when promise rejects" / "does not inherit a loading timeout when success does not specify one" | TST-STA-16 |
| "uses custom timeout from success options when promise resolves" / "uses custom timeout from error options when promise rejects" | TST-STA-16, TST-STA-19 |
| "uses provider timeout when no custom timeout is specified" | TST-STA-16, TST-API-01 |
| "does not auto-dismiss when timeout is set to 0" | TST-STA-16, TST-STA-18 |
| "pauses timers when hovering over toast" | TST-PTR-01, TST-PTR-02, TST-STA-21, TST-STA-22 |
| update: "updates the toast" | TST-STA-09, TST-API-07 |
| "increments updateKey when updating a toast" | TST-STA-09 |
| "auto-dismisses when timeout changes from 0 to a positive value" | TST-STA-19 |
| "schedules a timer when updating a loading toast to a non-loading type" | TST-STA-18, TST-STA-19 |
| close: "closes a toast" / "closes all toasts" | TST-STA-11 / TST-STA-12, TST-ANI-06 |
| prop: timeout "applies a changed timeout to toasts added afterwards" | TST-API-01, TST-STA-29 |
| prop: limit "marks toasts as limited when the limit is exceeded" | TST-API-02, TST-STA-03, TST-DOM-06 |
| "unmarks toasts as limited when the limit is not exceeded" | TST-STA-04, TST-PTR-26 |
| "preserves limited state when upserting a limited toast" | TST-STA-04, TST-STA-05 |
| "recomputes limited toasts when the limit prop changes" | TST-STA-04, TST-API-02 |
| in dialog: "toasts in dialogs are accessible and not aria-hidden" | TST-EDGE-12, TST-ARIA-02 |
| "high priority toasts in dialogs have correct accessibility structure" | TST-ARIA-06, TST-DOM-05, TST-EDGE-12 |

### `createToastManager.test.tsx` (*jsdom*, fake timers)

| Test | Requirements |
|---|---|
| add: "adds a toast" | TST-API-03, TST-API-08 |
| "returns a toast id" | TST-API-08, TST-API-06 |
| "upserts a toast when adding with an existing id" | TST-STA-05, TST-API-06 |
| promise: "adds a toast with the loading state that is updated with the success state" | TST-API-03, TST-STA-16 |
| "does not inherit a loading timeout when success does not specify one" | TST-STA-16 |
| "adds a toast with the loading state that is updated with the error state" | TST-STA-16, TST-API-09, TST-EVT-08 |
| "does not reopen a dismissed promise toast when it resolves" | TST-STA-17 |
| update: "updates a toast" | TST-API-03, TST-STA-09 |
| "derives the update from the current toast when given a function" | TST-STA-10 |
| "resets the auto-dismiss timer when updating with the same timeout value" / "resets the auto-dismiss timer when updating from 0 to a timeout, then updating with the same timeout again" / "auto-dismisses when updating timeout from 0 to a positive value" / "schedules a timer when updating a loading toast to a non-loading type" / "does not clear the auto-dismiss timer when updated twice before a re-render" | TST-STA-19 |
| close: "closes a toast" | TST-API-03, TST-STA-11 |
| "closes all toasts" | TST-STA-12 |
| "does not call onClose when closing toasts that are already ending" | TST-EVT-02, TST-EVT-10 |

### `provider/ToastProvider.test.tsx`

| Test | Requirements |
|---|---|
| "syncs a changed timeout before descendant layout effects" | TST-API-04 |
| "syncs a changed limit before descendant layout effects" | TST-API-04, TST-STA-04 |
| "does not sync provider props from an abandoned render" | TST-API-04, TST-EDGE-09 |

### `action/ToastAction.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`) | TST-STR-07, TST-API-16; RND-DOM-36…49 |
| "performs an action when clicked" | TST-PTR-27, TST-EVT-09 |
| "forwards actionProps to the button" | TST-DOM-19, TST-EVT-09 |
| "does not render if it has no children" | TST-DOM-19, TST-STR-07 |
| "renders content passed through the render prop" / "does not render a childless render prop when there is no action content" / "renders the toast action content through a childless render prop" | TST-DOM-19 |

### `close/ToastClose.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`) | TST-STR-08, TST-API-16; RND-DOM-36…49 |
| "closes the toast when clicked" | TST-PTR-26, TST-ANI-06 |

### `content/ToastContent.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`) | TST-STR-04 |
| "marks content behind the frontmost toast with data-behind" | TST-DOM-15, TST-STA-01, TST-STA-02 |
| "reflects the expanded state when the viewport is hovered" | TST-DOM-15, TST-STA-14, TST-PTR-01 |

### `title/ToastTitle.test.tsx` / `description/ToastDescription.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLHeadingElement` / `HTMLParagraphElement`) | TST-STR-05 / TST-STR-06 |

### `utils/useToastLabelPart.test.tsx` (each test runs for Title with `aria-labelledby` and for Description with `aria-describedby`)

| Test | Requirements |
|---|---|
| "throws a descriptive error when rendered outside <Toast.Root>" | TST-EDGE-02 |
| "renders the toast title/description by default" | TST-API-15 |
| "adds aria-labelledby/aria-describedby to the root element" | TST-ARIA-04 / TST-ARIA-05 |
| "does not render if it has no children" | TST-DOM-16, TST-ARIA-04 / TST-ARIA-05 |
| "renders a numeric zero child" | TST-DOM-16, TST-API-15 |
| prop: render "renders content passed through the render prop" / "renders content passed through a render function" / "does not render a childless render prop when there is no content" / "does not render when a render function returns no element" | TST-DOM-16 |
| "wires aria-labelledby/aria-describedby to a part rendered through the render prop" | TST-DOM-16, TST-ARIA-04 / TST-ARIA-05 |
| "renders the toast title/description through a childless render prop" | TST-DOM-16, TST-API-15 |
| "clears aria-labelledby/aria-describedby from the root when the content is removed" | TST-DOM-16, TST-ARIA-04 / TST-ARIA-05 |
| "does not let an older part cleanup clear a newer part" | TST-ARIA-10 |

### `utils/isRenderableNode.test.ts`

| Test | Requirements |
|---|---|
| "treats renderable primitives as content" / "treats non-rendering values as empty" / "recurses into arrays" / "requires an element whose children are renderable" | TST-DOM-16 |

### `portal/ToastPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, no Provider) | TST-STR-09, TST-API-17, TST-DOM-20 |

### `positioner/ToastPositioner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Provider) | TST-STR-10 |
| "positions an anchored toast against its anchor element" (*browser*) | TST-POS-01, TST-POS-03, TST-DOM-21 |
| "falls back to the viewport when no anchor is provided" | TST-POS-03, TST-POS-01, TST-DOM-21 |
| "lets positioner props override the ones carried on the toast" | TST-POS-02 |
| --toast-index: "keeps the DOM index while a toast animates out" (fake timers) | TST-DOM-10, TST-DOM-21 |
| "throws a descriptive error when rendered outside <Toast.Provider>" | TST-EDGE-01 |

### `arrow/ToastArrow.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: Element`, inside Provider + Positioner) | TST-STR-11 |
| "mirrors the resolved side of its positioner" (*browser*) | TST-DOM-22, TST-ARIA-08, TST-POS-04 |
| "throws a descriptive error when rendered outside <Toast.Positioner>" | TST-EDGE-03 |

### `createToastManager.spec.tsx`, `useToastManager.spec.tsx`

Type-level only: the generic `Data` parameter of `createToastManager<Data>()` / `useToastManager<Data>()` types `toasts[].data`, `add({ data })`, `update(id, { data })` (whole `Data` value required), the updater's `prevToast.data` (`Data | undefined`), and `promise` state options; per-call generics (`add<T>()`, `update<T>()`, `promise<V, T>()`) are also accepted; functions are valid `Data` values. Covered by TST-API-05, TST-API-08, TST-API-11, TST-STA-27.

### Requirements that exist only because of a test

None: every assertion above maps to a requirement already stated in §2–§12. Test-environment notes: the swipe tests stub `setPointerCapture`/`releasePointerCapture` on the Root element because the test runner's synthetic pointer events do not support capture; the Viewport "window" tests invoke the captured `blur`/`focus` listeners directly with events whose `composedPath()` is `[window]` because JSDOM cannot blur the window.

## 14. React-specific notes and porting considerations

Non-normative.

- **Store, not component state.** All toast state lives in a `ReactStore` (`toast/store.ts`) created once per Provider with `useRefWithInit`; parts subscribe with `store.useState(selector, …args)` (UTL-STA-18). `useToastManager()` subscribes to the whole `toasts` array and memoizes its return object on it, which is why `toasts` identity changes on every store write while the four methods are stable class fields. A port needs an equivalent observable record with `Object.is` no-op semantics (UTL-STA-10/11): several Toast behaviors rely on "no notification when nothing changed" (for example `set('focused', true)` while already focused). Port: see decisions/05-state-store-and-event-details.md §1 (the store; the shape of the manager object returned by `useToastManager`/`createToastManager` is not settled there).
- **Starting-style removal without a frame.** The entry phase is cleared by the Root's measurement in a layout effect (`useIsoLayoutEffect`, post-commit pre-paint) that writes `transitionStatus: undefined` to the store; React re-renders synchronously before paint. The `offsetHeight` read inside the measurement forces style/layout with `data-starting-style` present, which is what makes the subsequent attribute removal start a CSS transition. A port that removes the attribute without first forcing a style computation will see no enter transition; a port with asynchronous rendering must ensure the attribute removal commits before the first paint of the new toast. Port: see decisions/04-js-interop-boundary.md §2 (Toast root is a TRN-DOM-04 part, so `data-starting-style`/`data-ending-style` are JS-owned) and decisions/01-hosting-model-and-timing.md Decision item 4 ("post-commit, pre-paint" DOM-only vs C# state work).
- **`flushSync` sites.** Observer-driven re-measurements (`ToastContent`) call `ReactDOM.flushSync` so the new `--toast-height` lands in the same task as the observer callback (TST-ANI-03), and removal on animations finished runs inside the TRN synchronous commit (TRN-ANI-09). CHANGELOG v1.0.0-rc.2 "Fix `flushSync` dev error when toast is added (#3443)" is why the mount measurement is *not* flushed synchronously (it would run during a commit). Port: see decisions/01-hosting-model-and-timing.md Decision item 4 ("committed synchronously") and Relaxation table (TRN-ANI-09 row).
- **Layout-effect ordering for Provider props.** `ToastProviderPropsSynchronizer` is rendered as the Provider's first child so its layout effect runs before descendants' layout effects (React runs child effects in tree order, siblings before later siblings' subtrees). This is the mechanism behind TST-API-04; a port should apply prop changes to the store before any descendant's post-commit work. Port: see decisions/05-state-store-and-event-details.md §1 (prop-to-store sync in `OnParametersSet`).
- **Passive-effect timing for global listeners and the completion watcher.** The Viewport binds window/document listeners in `useEffect` (after paint) keyed on `isEmpty`; the Root arms the completion watcher in `useEffect`. For discrete user events React flushes passive effects synchronously, which is why JSDOM tests observe removal (TST-ANI-06) right after a click. Only the ordering of §9 is normative. Port: see decisions/01-hosting-model-and-timing.md Decision items 2 and 4 ("after paint"; window/document listeners are browser-API work in JS) and decisions/04-js-interop-boundary.md §1 (`transitions` watcher).
- **Stable callbacks.** `recalculateHeight` and `handleSwipeEnd` are `useStableCallback` trampolines (UTL-STA-16): stable identity, latest closure. `handleSwipeEnd` is registered on the document once per gesture and must see the latest `swipeDirections`; a port can simply close over current values at release time. Port: see decisions/01-hosting-model-and-timing.md Decision item 2 (constraint C1: the swipe loop is JS end to end).
- **Refs read in handlers.** Swipe bookkeeping (`dragOffsetRef`, `initialTransformRef`, `intendedSwipeDirectionRef`, `maxSwipeDisplacementRef`, `cancelledSwipeRef`, `swipeCancelBaselineRef`, `isFirstPointerMoveRef`, `activePointerIdRef`, `dragAbortControllerRef`) is kept in mutable refs alongside mirrored state (`dragOffset`, `initialTransform`, `isSwiping`, `isRealSwipe`, `lockedDirection`, `currentSwipeDirection`) that drives rendering. The observable contract is §6.2; a port can keep one set of instance fields and re-render from them. Port: see decisions/04-js-interop-boundary.md §2 (`Toast.Root` swipe movement variables, `data-swiping`, `data-swipe-direction` are JS-owned, C# mirrors) and §1 (no module yet hosts Toast's own gesture: `swipe` is the shared tracker, which Toast does not use).
- **Retained Root instances.** `lastToastIdRef` distinguishes a first mount from a reinitialisation (TST-ANI-07/08/09). Consumers normally key Roots by `toast.id`; index keys are supported but shift instances between toasts, so the Root re-registers its handle per id (`ref` stored in the toast object via `updateToastInternal`). The `ref` field on the toast object is therefore a live DOM-handle cell that the store reads at focus hand-off time. Port: see decisions/02-render-pipeline-and-attribute-merging.md "Requirements relaxed or withdrawn for the port" (RND-DOM-20..24: handles become `Element`).
- **`inert` across React versions.** `inertValue` renders `'true'` on React < 19 and a boolean on ≥ 19 (UTL-DOM-16); the DOM result is identical.
- **Focus guards.** `FocusGuard` is a plain `<span tabindex="0">` with a VoiceOver/WebKit role tweak (PSH-DOM-13). The same element is rendered three times from one variable; `handlingFocusGuardRef` suppresses the Viewport `focus` handler for the hand-off focus that the guard itself causes. Port: see decisions/04-js-interop-boundary.md §1 (`portal` module owns focus guards).
- **`useId` and ids.** Title/Description ids come from `useId` (`base-ui`-less raw React id here: `useId(idProp)` with no prefix, so the generated id is React's `:r1:`/`«r1»` form on ≥ 18 and a `mui-<n>` counter on 17 — see RND-DOM-27…30; note this part does **not** go through `useBaseUiId`, so the generated id lacks the `base-ui-` prefix). Toast ids (`generateId('toast')`) are random-plus-counter strings, not SSR-stable; toasts are never server-rendered because they only exist after an `add` call. Port: see decisions/02-render-pipeline-and-attribute-merging.md Decision item 5 and "Requirements relaxed or withdrawn for the port" (RND-DOM-27 exceptions collapsed to the prefixed form).
- **Abandoned renders / Suspense.** TST-EDGE-09 exists because layout effects of a suspended render never run; a port with no equivalent concept can treat prop changes as applied when committed. Port: see decisions/05-state-store-and-event-details.md §1 (prop-to-store sync in `OnParametersSet`).
- **Timers.** `Timeout` wraps `setTimeout`; the pause/resume arithmetic uses `Date.now()` wall-clock time, so fake-timer harnesses must mock both (the store tests use `vi.useFakeTimers` plus `vi.setSystemTime`). Port: see decisions/01-hosting-model-and-timing.md Decision item 4 ("macrotask / N ms": C# timers when the result is state) and decisions/06-testing-harness.md §2 "Clocks" (`TimeProvider`).
- **Portal and alert region.** `Toast.Portal` is the lite portal (TST-STR-09); the Viewport's high-priority alert region (TST-DOM-05) is ordinary rendered markup. Port: see decisions/04-js-interop-boundary.md §5 (portal by DOM move) and §1 (everything not listed, including all parts, is C#).
- **React 17 vs 18/19.** `useSyncExternalStore` shim on 17; `useId` fallback; `inert` string; `flushSync` behaves the same. No Toast behavior differs otherwise.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TST-STR-01 | MUST | Provider renders no element; one independent store per Provider | 2 |
| TST-STR-02 | MUST | Viewport div inside Provider | 2 |
| TST-STR-03 | MUST | Root div inside Provider, one per toast, id-based lookups | 2 |
| TST-STR-04 | MUST | Content div inside Root | 2 |
| TST-STR-05 | MUST | Title h2 inside Root, conditional | 2 |
| TST-STR-06 | MUST | Description p inside Root, conditional | 2 |
| TST-STR-07 | MUST | Action button inside Root, conditional | 2 |
| TST-STR-08 | MUST | Close button inside Root and Provider | 2 |
| TST-STR-09 | MUST | Portal = lite portal div in body | 2 |
| TST-STR-10 | MUST | Positioner div inside Provider wrapping a Root | 2 |
| TST-STR-11 | MUST | Arrow div inside Positioner | 2 |
| TST-STR-12 | MUST | Viewport also renders focus guards and alert region | 2 |
| TST-STR-13 | MUST | Portal optional; Viewport uses its owner window | 2 |
| TST-API-01 | MUST | `timeout` default 5000; used for later schedules | 3.1 |
| TST-API-02 | MUST | `limit` default 3; re-evaluated on change | 3.1 |
| TST-API-03 | MUST | `toastManager` subscription and event dispatch | 3.1 |
| TST-API-04 | MUST | Prop sync post-commit pre-paint before descendants; not from abandoned renders | 3.1 |
| TST-API-05 | MUST | `useToastManager()` shape; reactive `toasts` | 3.2 |
| TST-API-06 | MUST | `add` returns id; generated `toast-xxxx-n` | 3.2 |
| TST-API-07 | MUST | `close`/`update`/`promise` signatures | 3.2 |
| TST-API-08 | MUST | `createToastManager()` shape; synchronous emit; no queuing | 3.2 |
| TST-API-09 | MUST | manager `promise` returns handled promise when a Provider handled it | 3.2 |
| TST-API-10 | MUST | `' subscribe'` hook | 3.2 |
| TST-API-11 | MUST | Add/Update option types (`transitionStatus` overridden/ignored) | 3.2 |
| TST-API-12 | MUST | Promise options resolution (string/object/function) | 3.2 |
| TST-API-13 | MUST | Root `toast` prop required; fields read | 3.4 |
| TST-API-14 | MUST | `swipeDirection` default `['down','right']`; anchored → none | 3.4 |
| TST-API-15 | MUST | Title/Description `children ?? toast.*`, `id` generated | 3.6 |
| TST-API-16 | MUST | Action/Close button mechanism options | 3.8 |
| TST-API-17 | MUST | Portal `container` resolution | 3.10 |
| TST-STA-01 | MUST | Newest-first ordering | 4.1 |
| TST-STA-02 | MUST | domIndex / visibleIndex / offsetY derivation | 4.1 |
| TST-STA-03 | MUST | `limited` derivation over non-ending toasts | 4.1 |
| TST-STA-04 | MUST | When `limited` is re-derived (not on update/upsert) | 4.1 |
| TST-STA-05 | MUST | Upsert of a non-ending toast | 4.2 |
| TST-STA-06 | MUST | Re-add of an ending toast replaces silently | 4.2 |
| TST-STA-07 | MUST | Fresh add: updateKey 0, status starting, timer | 4.2 |
| TST-STA-08 | MUST | Update ignored for missing/ending; updater not called | 4.3 |
| TST-STA-09 | MUST | Update replaces keys wholesale; updateKey+1 | 4.3 |
| TST-STA-10 | MUST | Updater function contract; nested store calls honored | 4.3 |
| TST-STA-11 | MUST | `close(id)`: ending, height 0, re-limit, onClose, focus hand-off | 4.4 |
| TST-STA-12 | MUST | `close()` closes all | 4.4 |
| TST-STA-13 | MUST | Removal semantics; onRemove once | 4.4 |
| TST-STA-14 | MUST | `expanded` and "expanded or out of focus" predicates | 4.4 |
| TST-STA-15 | MUST | hovering/focused cleared when list empties or all ending | 4.4 |
| TST-STA-16 | MUST | Promise lifecycle (loading → success/error, timeout handling) | 4.5 |
| TST-STA-17 | MUST | Dismissed promise toast never reopens | 4.5 |
| TST-STA-18 | MUST | Timer scheduling rule on add | 4.6 |
| TST-STA-19 | MUST | Timer rescheduling rules on update/upsert | 4.6 |
| TST-STA-20 | MUST | Timer cleared when toast becomes untimed | 4.6 |
| TST-STA-21 | MUST | Pause arithmetic | 4.6 |
| TST-STA-22 | MUST | Resume arithmetic; zero remaining restarts full delay | 4.6 |
| TST-STA-23 | MUST | Timers created while paused start on resume with full delay | 4.6 |
| TST-STA-24 | MUST | Paused flag reset when no timers remain | 4.6 |
| TST-STA-25 | MUST | Close/close-all/unmount clear timers | 4.6 |
| TST-STA-26 | MUST | Part state objects | 4.7 |
| TST-STA-27 | MUST | Function `data` stored, not invoked | 4.3 |
| TST-STA-28 | MUST | Timer fire closes like `close(id)` | 4.6 |
| TST-STA-29 | MUST | Provider `timeout` change does not touch existing timers | 4.6 |
| TST-DOM-01 | MUST | Viewport tabindex -1 and handlers | 5.1 |
| TST-DOM-02 | MUST | Viewport `data-expanded` | 5.1 |
| TST-DOM-03 | MUST | `--toast-frontmost-height` | 5.1 |
| TST-DOM-04 | MUST | Three focus guards while non-empty and prev focus recorded | 5.1 |
| TST-DOM-05 | MUST | Hidden alert region markup | 5.1 |
| TST-DOM-06 | MUST | Root data-expanded/limited/type/swiping/swipe-direction | 5.2 |
| TST-DOM-07 | MUST | Root starting/ending style attributes from store status | 5.2 |
| TST-DOM-08 | MUST | Root tabindex 0 | 5.2 |
| TST-DOM-09 | MUST | Root `inert` when limited | 5.2 |
| TST-DOM-10 | MUST | `--toast-index` (visible index; DOM index while ending) | 5.2 |
| TST-DOM-11 | MUST | `--toast-offset-y` | 5.2 |
| TST-DOM-12 | MUST | `--toast-height` when measured, absent while ending | 5.2 |
| TST-DOM-13 | MUST | `--toast-swipe-movement-x/y` always set | 5.2 |
| TST-DOM-14 | MUST | Inline transition none + transform while swiping | 5.2 |
| TST-DOM-15 | MUST | Content data-expanded / data-behind | 5.3 |
| TST-DOM-16 | MUST | Title/Description conditional rendering and id publication | 5.4 |
| TST-DOM-17 | MUST | Title/Description data-type | 5.4 |
| TST-DOM-18 | MUST | Action/Close data-type + button attrs | 5.5 |
| TST-DOM-19 | MUST | Action attribute precedence and conditional rendering | 5.5 |
| TST-DOM-20 | MUST | Portal node attributes | 5.6 |
| TST-DOM-21 | MUST | Positioner role/data attrs/vars/transition none | 5.7 |
| TST-DOM-22 | MUST | Arrow styles and data attrs | 5.7 |
| TST-DOM-23 | MUST | Viewport consumer attribute precedence; children wrapper last | 5.1 |
| TST-ARIA-01 | MUST | Viewport region + polite live region attributes | 5.8 |
| TST-ARIA-02 | MUST | Root role dialog / alertdialog | 5.8 |
| TST-ARIA-03 | MUST | Root aria-modal=false | 5.8 |
| TST-ARIA-04 | MUST | Root aria-labelledby from Title | 5.8 |
| TST-ARIA-05 | MUST | Root aria-describedby from Description | 5.8 |
| TST-ARIA-06 | MUST | High-priority Root aria-hidden while viewport not focused | 5.8 |
| TST-ARIA-07 | MUST | Close aria-hidden unless expanded or focused | 5.8 |
| TST-ARIA-08 | MUST | Arrow aria-hidden | 5.8 |
| TST-ARIA-09 | MUST | Positioner role presentation | 5.8 |
| TST-ARIA-10 | MUST | Latest label part wins; stale withdrawal ignored | 5.8 |
| TST-KEY-01 | MUST | Global F6 focuses viewport | 6.1 |
| TST-KEY-02 | MUST | F6 on viewport ignored | 6.1 |
| TST-KEY-03 | MUST | Shift+Tab on viewport restores previous focus | 6.1 |
| TST-KEY-04 | MUST | Tab not intercepted | 6.1 |
| TST-KEY-05 | MUST | Escape closes toast containing focus | 6.1 |
| TST-KEY-06 | MUST | Escape ignored when focus outside root subtree | 6.1 |
| TST-PTR-01 | MUST | mouseenter/mousemove pause + hovering | 6.2 |
| TST-PTR-02 | MUST | mouseleave collapses and resumes | 6.2 |
| TST-PTR-03 | MUST | mouseleave deferred while toasts ending | 6.2 |
| TST-PTR-04 | MUST | mouseleave deferred while touch active | 6.2 |
| TST-PTR-05 | MUST | Outside touch collapses and resumes | 6.2 |
| TST-PTR-06 | MUST | Outside mouse press ignored | 6.2 |
| TST-PTR-07 | MUST | Inside touch no effect | 6.2 |
| TST-PTR-08 | MUST | Swipe start conditions (button 0, ignore selectors) | 6.2 |
| TST-PTR-09 | MUST | Touch press on root pauses timers | 6.2 |
| TST-PTR-10 | MUST | Gesture start state | 6.2 |
| TST-PTR-11 | MUST | First move re-anchors; moves preventDefault | 6.2 |
| TST-PTR-12 | MUST | Cancel baseline tracking | 6.2 |
| TST-PTR-13 | MUST | 1 px activation and axis lock | 6.2 |
| TST-PTR-14 | MUST | Direction candidate and arming | 6.2 |
| TST-PTR-15 | MUST | Reverse cancel (10 px) and re-arm (40 px) | 6.2 |
| TST-PTR-16 | MUST | Damping and offset application | 6.2 |
| TST-PTR-17 | MUST | Release dismiss (first allowed direction > 40 px) | 6.2 |
| TST-PTR-18 | MUST | Release snap back | 6.2 |
| TST-PTR-19 | MUST | pointercancel / cancelled release snaps back | 6.2 |
| TST-PTR-20 | MUST | Release on root or document; styles restored | 6.2 |
| TST-PTR-21 | MUST | Native touchmove prevented only during gesture | 6.2 |
| TST-PTR-22 | MUST | Anchored toasts have no swipe | 6.2 |
| TST-PTR-23 | MUST | Active-pointer moves preventDefault; others ignored | 6.2 |
| TST-PTR-24 | MUST | Swipe state per instance, reset on reinit/unmount | 6.2 |
| TST-PTR-25 | MUST | Click on viewport runs focus logic | 6.2 |
| TST-PTR-26 | MUST | Close click closes | 6.2 |
| TST-PTR-27 | MUST | Action click runs actionProps.onClick | 6.2 |
| TST-FOC-01 | MUST | F6 records previous focus, focuses viewport, pauses | 6.3 |
| TST-FOC-02 | MUST | Guard from viewport → first focusable toast | 6.3 |
| TST-FOC-03 | MUST | Guard from elsewhere → previous focus | 6.3 |
| TST-FOC-04 | MUST | Restore = focus(prev, preventScroll) | 6.3 |
| TST-FOC-05 | MUST | Viewport focus (focus-visible) sets focused + pauses | 6.3 |
| TST-FOC-06 | MUST | Viewport blur clears focused, resumes unless hovering/unfocused window | 6.3 |
| TST-FOC-07 | MUST | Close moves focus to next, else previous toast | 6.3 |
| TST-FOC-08 | MUST | No toast left / close-all → previous focus | 6.3 |
| TST-FOC-09 | MUST | Focus outside viewport untouched | 6.3 |
| TST-FOC-10 | MUST | Limited/ending toasts skipped | 6.3 |
| TST-FOC-11 | MUST | Window blur pauses, sets isWindowFocused false | 6.3 |
| TST-FOC-12 | MUST | Window focus resumes; isWindowFocused after 0 ms | 6.3 |
| TST-FOC-13 | MUST | Previous-focus element only from F6, never cleared | 6.3 |
| TST-FOC-14 | MUST | Global listeners bound once per non-empty cycle | 6.3 |
| TST-FOC-15 | MUST | Guard hand-off focus ignored by viewport focus handler | 6.3 |
| TST-DIS-01 | MUST NOT | No outside-press/scroll/ancestor dismissal | 6.4 |
| TST-EVT-01 | MUST | onClose synchronous in close, once | 7 |
| TST-EVT-02 | MUST | onClose not repeated for ending toasts | 7 |
| TST-EVT-03 | MUST | onRemove on removal, once | 7 |
| TST-EVT-04 | MUST | onRemove skipped on silent replacement | 7 |
| TST-EVT-05 | MUST | Removal only via a rendered Root | 7 |
| TST-EVT-06 | MUST | Manager events synchronous to current subscribers | 7 |
| TST-EVT-07 | MUST | promise() fulfills with value | 7 |
| TST-EVT-08 | MUST | promise() rejects with reason | 7 |
| TST-EVT-09 | MUST | actionProps handlers applied and chained | 7 |
| TST-EVT-10 | MUST | Store calls from onClose applied consistently | 7 |
| TST-EVT-11 | MUST | `toasts` reflects every write; methods stable | 7 |
| TST-ANI-01 | MUST | Starting style cleared post-commit pre-paint by measurement | 9.1 |
| TST-ANI-02 | MUST | Measurement procedure and write | 9.1 |
| TST-ANI-03 | MUST | Content observers re-measure | 9.1 |
| TST-ANI-04 | MUST | Ending style in the closing commit | 9.2 |
| TST-ANI-05 | MUST | Removal on animations finished (Root watcher) | 9.2 |
| TST-ANI-06 | MUST | Immediate removal without animations | 9.2 |
| TST-ANI-07 | MUST | Re-add of ending toast with retained Root | 9.2 |
| TST-ANI-08 | MUST | Root reinitialises for a new toast id | 9.2 |
| TST-ANI-09 | MUST | Remounted Root re-registers and re-measures | 9.2 |
| TST-ANI-10 | MUST | Writes ignored while ending | 9.2 |
| TST-ANI-11 | MUST | Swipe state cleared on reinit | 9.2 |
| TST-ANI-12 | MUST | Root never gets transition:none mount style | 9.2 |
| TST-ANI-13 | MUST | No keepMounted/actionsRef/onOpenChangeComplete | 9.2 |
| TST-POS-01 | MUST | Positioner defaults and fixed options | 10 |
| TST-POS-02 | MUST | Element prop > toast.positionerProps > default | 10 |
| TST-POS-03 | MUST | Anchor must be an Element; else unanchored | 10 |
| TST-POS-04 | MUST | Always mounted/open; Arrow mirrors | 10 |
| TST-POS-05 | MUST | Anchored toasts not special-cased in a shared Provider | 10 |
| TST-EDGE-01 | MUST | Provider-missing error message | 12 |
| TST-EDGE-02 | MUST | Root-context-missing error message | 12 |
| TST-EDGE-03 | MUST | Positioner-context-missing error message | 12 |
| TST-EDGE-04 | MUST | Unknown ids are no-ops | 12 |
| TST-EDGE-05 | MUST | One manager, several Providers | 12 |
| TST-EDGE-06 | MUST | Provider unmount clears timers | 12 |
| TST-EDGE-07 | MUST | Global listeners only while non-empty | 12 |
| TST-EDGE-08 | MUST | Owner window/document (iframes) | 12 |
| TST-EDGE-09 | MUST | Abandoned renders never sync | 12 |
| TST-EDGE-10 | MUST | Id-based, not identity-based | 12 |
| TST-EDGE-11 | MUST | limit 0 → everything limited | 12 |
| TST-EDGE-12 | MUST | Toasts in dialogs not hidden by Toast | 12 |
| TST-I18N-01 | MUST | Swipe directions physical | 12 |
| TST-A11Y-01 | MUST | High-priority alert announcement | 12 |
| TST-A11Y-02 | MUST | Focus-visible predicate | 12 |
| TST-A11Y-03 | MUST | Alert region removed while viewport focused | 12 |

## 16. Open questions

1. **Outline omissions.** `components/page.mdx` lists no `id`/`children` for Title/Description and no `disabled` for Close/Action, yet all are read by the source (`ToastTitle.tsx:21-22`, `ToastClose.tsx:23`, `ToastAction.tsx:23`) and `id` is load-bearing for `aria-labelledby`. Documented as TST-API-15/16. The outline also omits the Viewport's focus guards and alert region (TST-STR-12) and the `' subscribe'` manager member (TST-API-10). Decided (2026-10-18): `ToastManager` class, not a store (ADR 05 Amendments item 3).
2. **`--toast-frontmost-height` while the newest toast is ending.** `ToastViewport.tsx:40` reads `toasts[0]?.height`, which is `0` for a closing frontmost toast, so the variable disappears during that toast's exit and the stack behind it briefly loses its clamp (TST-DOM-03 documents the source behavior). Possibly unintended; a port could read the first *non-ending* toast, but that diverges.
3. **Forward Tab into the Viewport from the page.** After an F6 has recorded a previous-focus element, the outer focus guard (rendered *before* the Viewport host) restores focus to that element whenever it is focused from anything other than the Viewport (`ToastViewport.tsx:104-119`). If the recorded element precedes the Viewport in tab order, pressing Tab from it lands on the outer guard and focus bounces back — the viewport is then reachable only via F6. No test covers this ordering (tests render the Viewport before the button). Flagged as a possible focus trap; TST-FOC-03 states the source behavior.
4. **Reverse-cancel reference value.** `maxSwipeDisplacementRef` (`ToastRoot.tsx:372`) is set once when the direction is armed and never updated to the maximum reached, despite its name; the cancel test `armedDisplacement − currentDisplacement ≥ 10` therefore compares against the *arming* displacement (usually ~1 px in real use) and effectively cancels once the pointer has reversed ~9–10 px past the latest turning point. Corrected (second pass 2026-10-18): the shared tracker does the same — PSH-PTR-15 now states that the reference displacement is set once when the direction is first detected (`useSwipeDismiss.ts:644`), not a running maximum, so Toast and the tracker share this rule; they differ in the 1 px minimum, axis locking, the fixed 40 px threshold, the 0.5 damping factor and the ignore selector (TST-PTR-15). Decided (2026-10-18): Toast keeps its own gesture in a `toast` component module (ADR 04 Amendments, module placement rule).
5. **`movementX`/`movementY` dependence.** The cancel baseline (TST-PTR-12, `ToastRoot.tsx:314-326`) only moves when the pointer event carries non-zero `movementX/Y`. Browsers set these for mouse; for touch/pen some report `0`, in which case the baseline stays at the press point and the change-of-mind cancel degenerates to "reverse 10 px from the press point". No test covers non-mouse pointers.
6. **`handleFocusManagement` after `close()` from a timer.** The hand-off runs for timer-driven closes too (`store.ts:195,319`); if the user had focus-visible focus on a toast that auto-dismissed, focus jumps to the next toast. Intended (documented in TST-FOC-07 generally) but worth confirming for the port since timers are paused while focused only when the Viewport focus was focus-visible.
7. **Alert region includes ending toasts.** `highPriorityToasts` (`ToastViewport.tsx:43`) filters the whole list, so a closing high-priority toast stays announced (as a still-present alert) until removal. Harmless for `role="alert"` (announced on insertion only) but noted for fidelity.
8. **`updateToastInternal` ignores `limited` re-derivation on upsert.** TST-STA-04 documents that an upsert keeps `limited` (`store.ts:177,246-255` spread the previous toast and never call `applyLimited`); the test "preserves limited state when upserting a limited toast" asserts it, so this is intended — but it means a limited toast re-added by id stays inert even though the user "re-triggered" it.
9. **Generated Title/Description ids lack the `base-ui-` prefix** (`useId(idProp)` directly, `useToastLabelPart.ts:23`), unlike most parts (RND-DOM-27). Cosmetic; noted for the RND author. Resolved: RND-DOM-27 now lists `Toast.Title`/`Toast.Description` among its unprefixed exceptions (TST-API-15 cites it); the port collapses the exception to the prefixed form per decisions/02-render-pipeline-and-attribute-merging.md "Requirements relaxed or withdrawn for the port".
10. **Hinted items that do not exist.** The assignment hints at `data-front` and `--toast-swipe-direction`; neither exists (`ToastRootDataAttributes.ts`, `ToastRootCssVars.ts`). The attribute is `data-swipe-direction`; "front" is expressed only by `--toast-index: 0` / Content without `data-behind`. There is no `onOpenChange`, `actionsRef`, `keepMounted` or `reason` on any Toast part (TST-ANI-13, §3.13). Resolved: recorded as hint-vs-source corrections by the orchestrator; no requirement depends on the hinted names.
