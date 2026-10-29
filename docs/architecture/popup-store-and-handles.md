# Popup store and handles

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/utils/popups/{index,store,popupStoreUtils,popupHandle,popupTriggerMap,usePopupHandleStore,useTriggerFocusGuards,inlineRect}.ts`, `packages/react/src/utils/{FocusGuard,InternalBackdrop,FloatingPortalLite,usePopupViewport,closePart}.tsx`, `packages/react/src/utils/{useOpenInteractionType,usePopupAutoResize,useAnchoredPopupScrollLock,useSwipeDismiss,useMixedToggleClickHandler,popupStateMapping,CommonPopupCssVars,CommonPopupDataAttributes,CommonTriggerDataAttributes,CommonViewportDataAttributes}.ts`; per-component handle classes `dialog/store/DialogHandle.ts`, `alert-dialog/handle.ts`, `drawer/handle.ts`, `menu/store/MenuHandle.ts`, `popover/store/PopoverHandle.ts`, `preview-card/store/PreviewCardHandle.ts`, `tooltip/store/TooltipHandle.ts` (only their shared contract is specified here) |
| Tests | `packages/react/test/popupConformanceTests.tsx`, `test/detachedTriggersConformanceTests.tsx`, `test/viewportConformanceTests.tsx`, `test/popupFocusPropsTests.tsx`; `utils/popups/{store,popupTriggerMap,popupHandle,inlineRect}.test.ts`, `utils/popups/{popupStoreUtils,useTriggerFocusGuards}.test.tsx`, `utils/popups/popupStoreUtils.spec.ts` (type-level only), `utils/{useSwipeDismiss,usePopupViewport}.test.tsx`, `utils/popupStateMapping.test.ts` |
| Prefix | `PSH` |
| Status | approved |
| Revised | 2026-10-29 |
| Used by | `dialog.md` (DLG), `alert-dialog.md` (ADL), `drawer.md` (DRW), `popover.md` (POP), `menu.md` (MNU), `context-menu.md` (CTX), `menubar.md` (MBR), `tooltip.md` (TIP), `preview-card.md` (PRV), `select.md` (SEL), `combobox.md` (CMB), `autocomplete.md` (AUT), `navigation-menu.md` (NAV), `toast.md` (TST). See §6 for which pieces each uses. |

## 1. Purpose

Every Base UI popup (Dialog, AlertDialog, Drawer, Popover, Menu, ContextMenu, Menubar, Tooltip, PreviewCard, Select, Combobox, Autocomplete, NavigationMenu, Toast) is built on the same model:

1. A **popup store** owned by the Root part holds the open state, the mounted state, the identity of the trigger that opened the popup, that trigger's `payload`, and the DOM handles of the trigger, positioner and popup. Floating positioning (`floating-and-positioning.md`, FLT) is fed from this store; it does not own open state itself.
2. A **trigger registry** inside the store lets any number of Trigger parts register with one popup. Exactly one of them (or none) is the *active* trigger at any time: it anchors the popup, receives `aria-expanded="true"`/`aria-controls`/`data-popup-open`, gets focus back when the popup closes, and supplies the `payload` that the Root's children render.
3. A **handle** (`Dialog.createHandle()`, `Menu.createHandle()`, `Popover.createHandle()`, `Tooltip.createHandle()`, `PreviewCard.createHandle()`, `AlertDialog.createHandle()`, `Drawer.createHandle()`) is a small object that connects **detached triggers** rendered anywhere in the tree with a Root rendered somewhere else, and exposes an imperative `open()`/`close()`/`isOpen` API. Handles are the only way to open a popup programmatically with trigger association, and the only way to render a Trigger outside its Root.
4. Shared presentational helpers used by several popups: an invisible **focus guard** element, an internal full-screen **backdrop** used by modal popups, a lightweight **portal**, the morphing **viewport** that animates content when the active trigger changes, **auto-resize** of the popup when its content changes, the touch-aware **scroll lock** decision for anchored popups, **swipe-to-dismiss** gesture tracking (Drawer), tracking of the **input modality** that opened a popup (mouse/touch/pen/keyboard), a **mixed mousedown/click toggle** guard (Menubar), a **close-part registry** (Popover), and the shared **state-attribute mappings** and attribute/CSS-variable names for Trigger, Popup and Viewport parts.

A port cannot skip this document: the component documents cite it for everything that is not component-specific, and the four shared conformance suites (popup, detached triggers, viewport, focus props) are run against every popup family. A translation of those suites must pass against the port.

Out of scope here (covered elsewhere): anchor positioning and the floating-ui interaction hooks (`useClick`, `useDismiss`, hover, `FloatingFocusManager`, `FloatingPortal`) — FLT; transition status and animation-finished detection, `useUnmountAfterClose`, `keepMounted` — TRN; `useScrollLock` internals, the reactive store class, `NullStore`, `visuallyHidden`, platform detection — UTL; the event-details object and reason strings — EVD; `useRenderElement` and state → `data-*` mapping — RND; button press normalization and `useEnhancedClickHandler` — UBT.

## 2. Concepts and terminology

| Base UI / React name | Neutral term used below | Meaning |
|---|---|---|
| `PopupStore` (`DialogStore`, `PopoverStore`, `MenuStore`, `TooltipStore`, `PreviewCardStore`) | **popup store** | The reactive state container owned by one mounted Root. Its common fields are listed in §4 STA. Component stores add fields (listed in the component docs) but never remove these. |
| `ReactStore` (`@base-ui/utils/store`) | store | A key/value state object with subscribers, selectors with arguments, and a mutable non-reactive `context` bag. Specified in UTL. |
| `store.context` | store context | Non-reactive data kept next to the state: the trigger registry, DOM handles (`popupRef`, focus-guard handles), and the `onOpenChange`/`onOpenChangeComplete` callbacks. |
| `PopupTriggerMap` | **trigger registry** | An insertion-ordered map from trigger id to trigger DOM element. One per store. |
| trigger id | trigger id | The `id` attribute value of a Trigger part: the consumer's `id` prop when given, otherwise a generated id (`useBaseUiId`, RND). Used as the key in the registry and as the value of the Root's `triggerId`/`defaultTriggerId` props. |
| `activeTriggerId` / `activeTriggerElement` | **active trigger** | The trigger that currently owns the popup. Determines anchoring, ARIA relationships, `data-popup-open`, focus return and payload. |
| `payload` | payload | An arbitrary consumer value attached to a Trigger (`payload` prop) and copied into the store when that trigger becomes active; exposed to the Root's function-as-child. Its static type is the type argument of `createHandle<Payload>()` / `Root<Payload>`. |
| `openedWithoutTrigger` | trigger-less open | The popup was opened by a request that deliberately carried no trigger (`handle.open(null)` or `handle.openWithPayload()`). |
| "implicit active trigger" / lone-trigger claim | lone-trigger claim | When the popup is open, no trigger is active, the open was not trigger-less, and exactly one trigger is registered, that trigger is treated as the active trigger. |
| "contained" trigger | contained trigger | A Trigger rendered as a descendant of its Root with no `handle` prop; it finds the store through ancestor-provided shared state. |
| "detached" trigger | **detached trigger** | A Trigger given a `handle` prop. It reads the store the handle currently exposes and may be rendered anywhere, before or after the Root, including outside it. |
| `BasePopupHandle` / `XHandle` / `createXHandle()` | **handle** | The object returned by `X.createHandle()`. Holds a fallback store, a stack of attached Root stores, and listeners. See §3. |
| fallback store / `createNullXStore()` | **inert fallback store** | A read-only store with the component's initial (closed) state that the handle exposes while no Root is attached. Reads work; writes (`set`, `update`, `setOpen`) are no-ops; its trigger registry is live. |
| `attachStore` / `PopupHandleAttachment` | attach / detach | A Root binds its store to its `handle` for as long as the Root is mounted and keeps the same handle. |
| `floatingRootContext` (`FloatingRootStore`) | floating root state | The FLT-owned state object the store keeps in sync (open, reference element, floating element, floating id). Specified in FLT. |
| `floatingId` | popup id | The generated id used for the popup element when the consumer gives none (`popupId` selector, §4 STA). |
| `activeTriggerProps` / `inactiveTriggerProps` / `popupProps` | interaction prop bags | Attribute/handler bundles produced by the Root's FLT interactions (click, dismiss, hover) that Trigger and Popup parts merge into their elements. The active trigger receives the first bag, other triggers the second. |
| `FOCUSABLE_POPUP_PROPS` | focusable-popup attributes | `tabindex="-1"` plus the `data-base-ui-focusable=""` marker that tells the FLT focus manager which element inside the floating element is the popup. |
| `FocusGuard` | **focus guard** | An invisible, tabbable `<span>` placed around a trigger or around popup content to detect Tab leaving a region. |
| `InternalBackdrop` | **internal backdrop** | A fixed, full-viewport `<div>` rendered by modal popups behind the popup to swallow outside pointer interaction. Distinct from the consumer-styled `*.Backdrop` parts. |
| `FloatingPortalLite` | lite portal | A portal without the tab-order plumbing of FLT's `FloatingPortal`; used by Tooltip, PreviewCard and Toast. |
| `usePopupViewport` / `*.Viewport` | **viewport** | The optional part that wraps popup content and renders a `[data-current]` container plus, during a content transition, a `[data-previous]` snapshot container. |
| `usePopupAutoResize` | auto-resize | Measures the popup when its content changes and exposes the sizes as CSS variables so the size change can be animated. |
| `useAnchoredPopupScrollLock` | anchored scroll lock | The decision of whether a modal anchored popup locks page scroll, taking touch opens into account. Locking itself is UTL `useScrollLock`. |
| `useSwipeDismiss` | **swipe tracker** | Pointer/touch gesture tracker that reports progress, velocity and dismissal along allowed directions. Drawer uses it for its Popup (via Viewport) and SwipeArea. |
| `useOpenInteractionType` / `openMethod` | **open interaction type** | `'mouse' \| 'touch' \| 'pen' \| 'keyboard' \| ''` recorded when a trigger opens the popup; `null` while closed. Fed to the focus manager's `initialFocus`/`finalFocus` functions and to touch-specific behavior. |
| `useMixedToggleClickHandler` | mixed toggle guard | Prevents a popup that opens on mousedown from closing on the click that follows the same press. |
| `closePart` | close-part registry | Lets `Popover.Popup` know whether a `Popover.Close` is rendered inside it. |
| `StateAttributesMapping` (`triggerOpenStateMapping`, `pressableTriggerOpenStateMapping`, `popupStateMapping`, `popupTransitionStateMapping`) | state-attribute mapping | Functions from a part's state field to `data-*` attributes (mechanism in RND). |
| `REASONS.*` | reason | The `reason` string on event details (EVD). Reasons used by this document: `trigger-press`, `trigger-hover`, `trigger-focus`, `escape-key`, `close-press`, `focus-out`, `imperative-action`, `none`. |
| microtask / frame | microtask / frame | "After a microtask" = after the current synchronous work, before the next task; "next frame" = at the next animation frame (`requestAnimationFrame`). |

Throughout, "open" without qualification means the **effective** open state (§4 STA: `openProp` when controlled, else internal `open`).

## 3. Public surface

### 3.1 `createHandle()` and `Handle`

Exported as `X.createHandle` and `X.Handle` for X in Dialog, AlertDialog, Drawer, Menu, Popover, PreviewCard, Tooltip (`dialog/index.parts.ts:10`, `alert-dialog/index.parts.ts:10`, `drawer/index.parts.ts:16`, `menu/index.parts.ts:21`, and the popover/preview-card/tooltip equivalents).

| Member | Signature | Meaning |
|---|---|---|
| `X.createHandle<Payload>()` | `() => X.Handle<Payload>` | Creates a new, independent handle. The type argument types the `payload` prop of triggers using the handle and the `payload` the Root's function-as-child receives. |
| `handle.open(triggerId)` | Menu, Popover, PreviewCard, Tooltip: `(triggerId: string) => void`. Dialog, AlertDialog, Drawer: `(triggerId: string \| null) => void` | Opens the attached Root's popup, associating it with the registered trigger whose id is `triggerId`. The Dialog family accepts `null` to open without a trigger. |
| `handle.openWithPayload(payload)` | Dialog, AlertDialog, Drawer only: `(payload: Payload) => void` | Sets the store payload, then opens without associating a trigger. |
| `handle.close()` | `() => void` | Closes the attached Root's popup. |
| `handle.isOpen` | `readonly boolean` | Effective open state of the attached Root's store; `false` while no Root is attached. |
| `handle.store`, `handle.serverStore`, `handle.subscribeStore(listener)`, `handle.attachStore(store)` | internal (`@internal`) | Used by Trigger and Root parts; listed in §4 STA. Not part of the documented consumer API. |

Documented guarantees (`docs/src/app/(docs)/react/components/dialog/page.mdx:170-171`, `menu/page.mdx:210-211`, `popover/page.mdx:58-59`, `drawer/page.mdx:221-222`): the imperative methods require a Root using the same handle to be mounted; calls made while no Root is attached (before one mounts, after it unmounts) are ignored; each time a Root mounts it starts from fresh state — a call made while detached is not replayed and no open state carries over from a previous mount. "This method should only be called in an event handler or an effect (not during rendering)" is stated on every imperative method's JSDoc.

`AlertDialog.Handle` and `Drawer.Handle` are nominal subtypes of `Dialog.Handle` with identical runtime behavior (`alert-dialog/handle.ts:11-15`, `drawer/handle.ts:11-15`), each carrying a type-only private brand. The non-assignability is therefore one-directional: a plain `Dialog` handle is rejected by the `AlertDialog`/`Drawer` Root and Trigger `handle` props, and the two branded siblings reject each other, but an `AlertDialog`/`Drawer` handle *is* assignable to `Dialog.Root`/`Dialog.Trigger` (whose prop type is the base `DialogHandle`). Only the Dialog-handle-into-AlertDialog direction is type-tested (`alert-dialog/root/AlertDialogRoot.spec.tsx:32-43`, `@ts-expect-error`); the rest follows from the declarations (`dialog/root/DialogRoot.tsx:74`, `alert-dialog/root/AlertDialogRoot.tsx:42`, `drawer/root/DrawerRoot.tsx:299`) and is not tested. Warning text for the Dialog family always says `DialogHandle` (`popupHandle.test.ts:366-390`).

### 3.2 Shared Root props

Every Root of a handle-capable popup accepts these in addition to its component-specific props (`popover/root/PopoverRoot.tsx:134-201` is the reference; Dialog `dialog/root/DialogRoot.tsx`, Menu, Tooltip, PreviewCard, Drawer, AlertDialog are identical for these props):

| Prop | Type | Default | Meaning |
|---|---|---|---|
| `open` | `boolean \| undefined` | `undefined` | Controlled open state. When defined it wins over internal state (§4 STA). |
| `defaultOpen` | `boolean` | `false` | Initial open state when uncontrolled. |
| `onOpenChange` | `(open, eventDetails) => void` | — | Called before every open-state change request is applied; may `cancel()` it (EVD). |
| `onOpenChangeComplete` | `(open: boolean) => void` | — | Called after the enter/exit transition finishes (TRN). |
| `actionsRef` | `RefObject<{ unmount(): void; close(): void } \| null>` | — | Imperative actions: `unmount` ends the closing phase after an externally controlled closing animation (requires `preventUnmountOnClose()` to have been called in `onOpenChange`); `close` closes the popup with reason `imperative-action`. Menu's actions object differs (`{ setOpen }`, MNU). |
| `triggerId` | `string \| null \| undefined` | `undefined` | Controlled id of the active trigger. Meant to be used together with `open`. When defined (including `null`) it wins over the internally tracked active trigger. |
| `defaultTriggerId` | `string \| null \| undefined` | `null` | Initial active trigger id, for use with `defaultOpen`. |
| `handle` | `X.Handle<Payload> \| undefined` | `undefined` | Binds this Root to a handle so detached triggers and imperative calls can control it. |
| `children` | `ReactNode \| ({ payload }: { payload: Payload \| undefined }) => ReactNode` | — | Function-as-child receives the active trigger's payload (or the payload set by `openWithPayload`). |

### 3.3 Shared Trigger props

| Prop | Type | Default | Meaning |
|---|---|---|---|
| `handle` | `X.Handle<Payload> \| undefined` | `undefined` | Makes this a detached trigger reading from the handle's current store instead of an ancestor Root. |
| `payload` | `Payload \| undefined` | `undefined` | Value copied into the store's `payload` when this trigger is (or becomes) the active trigger. |
| `id` | `string \| undefined` | generated | The trigger id (registry key, `triggerId` value, `id` attribute). |

Source: `popover/trigger/PopoverTrigger.tsx:199-211`, `dialog/trigger/DialogTrigger.tsx:112-125`.

### 3.4 Shared Trigger / Popup / Viewport data attributes and CSS variables

Exact names and when they are present are specified in §4 DOM. Summary:

| Part | Attribute / variable | Source |
|---|---|---|
| Trigger | `data-popup-open`, `data-pressed` | `utils/CommonTriggerDataAttributes.ts:4,9` |
| Popup (and Positioner, Backdrop, Arrow where the component applies the mapping) | `data-open`, `data-closed`, `data-starting-style`, `data-ending-style`, `data-anchor-hidden`, `data-side`, `data-align` | `utils/CommonPopupDataAttributes.ts:6-38` |
| Popup element (auto-resize) and `[data-previous]` viewport container | `--popup-width`, `--popup-height` | `utils/CommonPopupCssVars.ts:6,12` |
| Viewport | `data-activation-direction`; children `[data-current]`, `[data-previous]`; `data-transitioning` | `utils/CommonViewportDataAttributes.ts:7`, `utils/usePopupViewport.tsx:233-266` |
| Any focus guard | `data-base-ui-focus-guard=""` | `utils/FocusGuard.tsx:39` |
| Internal backdrop | `data-base-ui-inert=""` | `utils/InternalBackdrop.tsx:24` |
| Popup element | `data-base-ui-focusable=""` | `utils/popups/popupStoreUtils.ts:27-30` |
| Click-opened triggers (Dialog, Popover) | `data-base-ui-click-trigger=""` | `dialog/trigger/DialogTrigger.tsx:91`, `popover/trigger/PopoverTrigger.tsx:141` (read by the FLT focus manager) |

### 3.5 Internal helpers (not exported to consumers)

`InternalBackdrop` (`cutout?: Element | null`), `FloatingPortalLite` (`container?: HTMLElement | ShadowRoot | RefObject | null`), `useSwipeDismiss` options, `usePopupViewport`, `usePopupAutoResize`, `useAnchoredPopupScrollLock`, `useOpenInteractionType`, `useMixedToggleClickHandler`, `closePart` and the store utilities are internal. Their behavior is normative because it is observable through the parts that use them; their option surface is tabulated in §6.

## 4. Behavioral requirements

### 4.1 STR — structure

- **PSH-STR-01** — MUST. Each mounted Root part owns exactly one popup store, created once when the Root mounts and kept for the Root's whole mounted lifetime; later changes to the Root's props (including a different `handle`) update or re-attach that same store rather than creating a new one.
  Source: `utils/popups/popupStoreUtils.ts:76-90`, `popover/root/PopoverRoot.tsx:119-124`. Test: [L4] `detachedTriggersConformanceTests.tsx` "keeps a detached trigger working during Fast Refresh-like handle recreation".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`).
- **PSH-STR-02** — MUST. A Trigger part resolves its store as: the store currently exposed by its `handle` prop when one is given; otherwise the store provided by the nearest ancestor Root of the same family. A `handle` takes precedence even when the Trigger is also inside a Root.
  Source: `popover/trigger/PopoverTrigger.tsx:49-51`, `dialog/trigger/DialogTrigger.tsx:38-40`, `utils/popups/usePopupHandleStore.ts:17-36`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active" (detached mode).
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`).
- **PSH-STR-03** — MUST. A Root with a `handle` prop attaches its store to that handle for as long as the Root stays mounted with that handle, and detaches it when the Root unmounts or the `handle` prop changes (attaching to the new handle in the same step).
  Source: `utils/popups/popupStoreUtils.ts:103-115`, `popover/root/PopoverRoot.tsx:90`. Test: [L2] `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts".
- **PSH-STR-04** — MUST. The attachment is established post-commit, pre-paint of the Root's own mount, before any descendant's post-commit work, so an imperative handle call made by a descendant in that same commit already reaches the Root's store.
  Source: `utils/popups/popupStoreUtils.ts:93-102,110-112`, `popover/root/PopoverRoot.tsx:88-93`. Test: [L2] `detachedTriggersConformanceTests.tsx` "resolves a trigger still registered to the previous root during a transient overlap".
- **PSH-STR-05** — MUST. A detached trigger MAY be rendered before or after its Root in document order, and inside or outside it; registration and activation behave identically in all cases.
  Source: `utils/popups/popupStoreUtils.ts:139-178,368-380`. Test: [L2] `detachedTriggersConformanceTests.tsx` "registers a detached trigger declared after the root".
- **PSH-STR-06** — MUST. Any number of Trigger parts (contained, detached, or a mix) MAY target one popup; at most one of them is the active trigger at a time.
  Source: `utils/popups/popupTriggerMap.ts:28-129`, `utils/popups/store.ts:51-55`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active". Docs: `components/dialog/page.mdx` §Multiple triggers.
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`).
- **PSH-STR-07** — MUST. A Viewport part renders its children inside a `<div data-current>` child; while a content transition is in progress it additionally renders a `<div data-previous>` sibling placed *before* the current container, and no `[data-previous]` element exists otherwise.
  Source: `utils/usePopupViewport.tsx:229-268`. Test: [L2] `viewportConformanceTests.tsx` "renders children in the `current` container by default", "creates morphing containers during transitions".
- **PSH-STR-08** — MUST. A focus guard is a `<span>` element.
  Source: `utils/FocusGuard.tsx:33-41`; see FLT-DOM-13 (full rendered shape). Test: none.
- **PSH-STR-09** — MUST. The internal backdrop is a `<div>` element rendered as a sibling immediately before the positioner/popup subtree of the part that renders it (Dialog.Portal, Popover/Menu/Select/Combobox.Positioner).
  Source: `utils/InternalBackdrop.tsx:18-33`, `dialog/portal/DialogPortal.tsx:36-39`, `popover/positioner/PopoverPositioner.tsx:128-131`. Test: none.
- **PSH-STR-10** — MUST. When a trigger's focus guards are enabled (PSH-FOC-07), one focus guard is rendered immediately before and one immediately after the trigger element; toggling the guards MUST NOT replace the trigger's DOM element.
  Source: `popover/trigger/PopoverTrigger.tsx:153-170`, `menu/trigger/MenuTrigger.tsx:288-306`. Test: [L4] `useTriggerFocusGuards.test.tsx` "preserves the tab destination after the exit transition with finalFocus as a %s and trigger tabIndex=%s".
- **PSH-STR-11** — MUST. The lite portal renders its children into a portal node appended to the resolved container (default: `<body>`), and renders nothing until that node exists.
  Source: `utils/FloatingPortalLite.tsx:21-37`; node creation in `floating-ui-react/components/FloatingPortal.tsx:69-150` (see [floating-and-positioning](floating-and-positioning.md)). Test: none.
- **PSH-STR-12** — MUST. The lite portal renders its portal node element through the shared rendering pipeline: `className`, `style` and `render` apply to that element, and every other prop except `children` and `container` (e.g. `data-*` attributes) is forwarded to it.
  Source: `utils/FloatingPortalLite.tsx:19-26`; `floating-ui-react/components/FloatingPortal.tsx:72, 125-133`. Test: [L2] `floating-ui-react/components/FloatingPortal.test.tsx` "FloatingPortalLite forwards HTML props to the portal element".

### 4.2 API — public parameters

- **PSH-API-01** — MUST. `X.createHandle()` returns a new handle on every call; handles are independent (separate fallback store, attachment stack and listeners).
  Source: `dialog/store/DialogHandle.ts:79-81`, `menu/store/MenuHandle.ts:53-55`, `utils/popups/popupHandle.ts:102-106`. Test: [L1] `popupHandle.test.ts` "exposes the fallback store until a root store attaches, and again after it detaches".
- **PSH-API-02** — MUST. `handle.open(triggerId)` accepts a trigger id string for Menu, Popover, PreviewCard and Tooltip; for Dialog, AlertDialog and Drawer it additionally accepts `null`, meaning "open without associating a trigger".
  Source: `menu/store/MenuHandle.ts:29-31`, `dialog/store/DialogHandle.ts:31-33`. Test: [L1] `popupHandle.test.ts` "opens without a trigger when the id is $triggerId".
- **PSH-API-03** — MUST. `handle.openWithPayload(payload)` exists only on Dialog, AlertDialog and Drawer handles.
  Source: `dialog/store/DialogHandle.ts:42-57`, `alert-dialog/handle.ts:11`, `drawer/handle.ts:11`. Test: [L1] `popupHandle.test.ts` "$component handle ignores openWithPayload() with a warning while detached".
- **PSH-API-04** — MUST. `handle.close()` exists on every handle and takes no arguments.
  Source: `utils/popups/popupHandle.ts:271-285`, `dialog/store/DialogHandle.ts:64-66`. Test: [L1] `popupHandle.test.ts` "closes the attached root".
- **PSH-API-05** — MUST. `handle.isOpen` is a read-only getter returning the effective open state of the attached Root's store, or `false` while no Root is attached.
  Source: `dialog/store/DialogHandle.ts:71-73`, `popover/store/PopoverHandle.ts:45-47`. Test: [L1] `detachedTriggersConformanceTests.tsx` "opens by trigger id with its payload and closes" (L4 for the `detachedTriggersConformanceTests.tsx` citation); `popupHandle.test.ts` "$component handle is closed and names itself in warnings while no root is attached".
- **PSH-API-06** — MUST. Root `defaultTriggerId` defaults to `null`; Root `triggerId` defaults to `undefined` (uncontrolled); Root and Trigger `handle` default to `undefined`; Trigger `payload` defaults to `undefined`.
  Source: `popover/root/PopoverRoot.tsx:35-36`, `dialog/root/useRenderDialogRoot.tsx:30-32`, `popover/trigger/PopoverTrigger.tsx:40-41`. Test: none.
- **PSH-API-07** — MUST. When Root `children` is a function it is called with one argument `{ payload }`, where `payload` is the store's current payload (`undefined` when none).
  Source: `popover/root/PopoverRoot.tsx:52,92`, `utils/popups/popupStoreUtils.ts:396-398`. Test: [L4] `detachedTriggersConformanceTests.tsx` "sets the payload of the active trigger and renders content based on it".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`).
- **PSH-API-08** — MUST. A Trigger's `id` prop, when given, is both its rendered `id` attribute and its trigger id; when absent a stable generated id is used for both.
  Source: `popover/trigger/PopoverTrigger.tsx:58,142`, `dialog/trigger/DialogTrigger.tsx:47,92`. Test: [L2] `detachedTriggersConformanceTests.tsx` "marks the trigger matching a controlled `triggerId` as active".
- **PSH-API-09** — MUST. `actionsRef.current.close()` closes the popup with reason `imperative-action` and no trigger; `actionsRef.current.unmount()` forces the end of the closing phase (TRN `forceUnmount`) and is a no-op once already unmounted.
  Source: `popover/root/PopoverRoot.tsx:74-81`, `dialog/root/useRenderDialogRoot.tsx:80-87`, `utils/popups/popupStoreUtils.ts:570-572,579`. Test: none.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

* PSH-STA-23 — the close-on-active-trigger-unmount check runs in `OnAfterRender` instead of one microtask after the commit; on Server after paint plus a round trip, during which the popup stays open.
* PSH-EDGE-06 — the "one frame later" check of the duplicate-root warning runs at the next render-batch boundary (`OnAfterRender`), the PSH-STA-23 row's substitute (ADR 01 Amendments 2026-10-27; added 2026-10-27, M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)).
* PSH-KEY-07 — the focus-guard close is committed a round trip later instead of synchronously.
* PSH-ANI-03 — the `trigger-hover` open commit lands a round trip later; same-task animation queries do not see it.

### 4.3 STA — state model

#### 4.3.1 Store fields

The common popup store state (`utils/popups/store.ts:12-88`) and its initial values (`store.ts:90-125`):

| Field | Type | Initial | Meaning |
|---|---|---|---|
| `open` | `boolean` | `false` (Root passes `defaultOpen`) | Internal open state. |
| `openProp` | `boolean \| undefined` (read-only) | `undefined` | Mirror of the Root's controlled `open` prop. |
| `mounted` | `boolean` | `false` (`true` if initially open) | Whether the popup subtree is mounted; lags `open` during exit transitions (TRN). |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | `undefined` | Current transition phase (TRN). |
| `floatingRootContext` | floating root state | new, `syncOnly: true`, `nested` per Root | FLT state kept in sync by PSH-STA-46 (corrected 2026-10-27 from PSH-STA-37, the fallback store's row; M4 track 1, fidelity correction, `_qa-notes.md`). |
| `floatingId` | `string \| undefined` | generated per Root | Generated popup id. |
| `triggerCount` | `number` | `0` | Number of registered triggers *while open* (PSH-STA-17). |
| `preventUnmountingOnClose` | `boolean` | `false` | A close requested keeping the popup mounted until `actionsRef.unmount()`. |
| `payload` | `Payload \| undefined` | `undefined` | Active trigger's payload or `openWithPayload` value. |
| `activeTriggerId` | `string \| null` | `null` (Root passes `defaultTriggerId`) | Internal active trigger id. |
| `activeTriggerElement` | `Element \| null` | `null` | Active trigger's DOM element. |
| `openedWithoutTrigger` | `boolean` | `false` | Trigger-less open flag. |
| `triggerIdProp` | `string \| null \| undefined` (read-only) | `undefined` | Mirror of the Root's controlled `triggerId` prop. |
| `popupElement` | `HTMLElement \| null` | `null` | Popup part element. |
| `positionerElement` | `HTMLElement \| null` | `null` | Positioner part element. |
| `activeTriggerProps` | attribute bag | `{}` | Interaction attributes for the active trigger. |
| `inactiveTriggerProps` | attribute bag | `{}` | Interaction attributes for other triggers. |
| `popupProps` | attribute bag | `{}` | Interaction attributes for the popup. |

Store context (`store.ts:127-144`): `triggerElements` (the trigger registry), `popupRef` (popup DOM handle), `onOpenChange`, `onOpenChangeComplete`.

- **PSH-STA-01** — MUST. A freshly created popup store has exactly the initial values in the table above, except that a Root initializes `open` from `defaultOpen`, `openProp` from `open`, `activeTriggerId` from `defaultTriggerId`, `triggerIdProp` from `triggerId`, and sets `mounted` to `true` when the initial `open` is `true`.
  Source: `utils/popups/store.ts:90-125`, `popover/root/PopoverRoot.tsx:39-45`, `popover/store/PopoverStore.ts:204-231`, `dialog/root/useRenderDialogRoot.tsx:49-63`. Test: [L2] `popupConformanceTests.tsx` "opens initially and remains uncontrolled".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the cited title closes with Escape, which is PSH-DIS-01's and M4 track 3's (`interactions`), so the row is green by an L2 port title on the test-only hosts (`PopupStorePortTests`, "port: …"); the cited title is listed open to track 3 in `popup.spec.ts`. 2026-10-28 (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): the test-only popup root composes the `interactions` session, and the cited title runs in `popup.spec.ts` on both modes.

#### 4.3.2 Derived values (selectors)

- **PSH-STA-02** — MUST. The effective open state is `openProp` when it is not `undefined`, otherwise `open`.
  Source: `utils/popups/store.ts:150`. Test: [L1] `store.test.ts` "uses the controlled open state when present", "uses the internal open state when uncontrolled"; `popupConformanceTests.tsx` "opens the popup with the `open` prop", "is ignored when `open={false}` is passed" (L4 for the `popupConformanceTests.tsx` citation).
- **PSH-STA-03** — MUST. The effective active trigger id is `triggerIdProp` when it is not `undefined` (a controlled `null` means "no active trigger"), otherwise `activeTriggerId`.
  Source: `utils/popups/store.ts:148`. Test: [L2] `detachedTriggersConformanceTests.tsx` "marks the trigger matching a controlled `triggerId` as active".
- **PSH-STA-04** — MUST. The popup id is the popup element's `id` attribute whenever a popup element is known — even when that attribute is the empty string — and `floatingId` only when no popup element is known (nullish fallback, not empty-string fallback); the chosen value resolves to `undefined` when it is empty. Hence a known popup element with an empty `id` yields `undefined`, not `floatingId`.
  Source: `utils/popups/store.ts:152-155`. Test: [L2] `popupStoreUtils.test.tsx` "prefers an explicit popup element id over the generated floating id", "omits popup id when the floating id is empty", "syncs the floating id into the popup store for trigger ownership selectors".
- **PSH-STA-05** — MUST. The derived transition status follows [TRN-STA-14](transitions-and-mounting.md): `'starting'` whenever the *effective* open state (PSH-STA-02, so a controlled `open` counts) is `true` and `mounted` is `false`; otherwise the stored `transitionStatus` unchanged.
  Source: `utils/popups/store.ts:180-183`. Test: [L1] `store.test.ts` "reports the starting phase while the popup is open but not yet mounted", "uses the controlled open state to detect the starting phase", "keeps the stored status once the opening cycle is mounted", "does not start a new enter phase when an exit is reversed", "keeps the stored status while the popup is closed".
- **PSH-STA-06** — MUST. The derived active trigger element is `null` while `mounted` is `false`, otherwise the stored element.
  Source: `utils/popups/store.ts:190`. Test: none.
- **PSH-STA-07** — MUST. `isTriggerActive(id)` is `true` iff `id` is defined and equals the effective active trigger id (regardless of open state).
  Source: `utils/popups/store.ts:195-196`. Test: none.
- **PSH-STA-08** — MUST. `isOpenedByTrigger(id)` is `true` iff `isTriggerActive(id)` and the effective open state is `true`.
  Source: `utils/popups/store.ts:157-161,200-201`. Test: [L1] `store.test.ts` "requires the trigger to be active".
- **PSH-STA-09** — MUST. `isMountedByTrigger(id)` is `true` iff `isTriggerActive(id)` and `mounted` is `true`.
  Source: `utils/popups/store.ts:205-206`. Test: none.
- **PSH-STA-10** — MUST. `triggerPopupId(id)` (the value a trigger uses for `aria-controls`) is the popup id when either `isOpenedByTrigger(id)`, or (`id` is defined, the popup is effectively open, the effective active trigger id is `null`/`undefined`, `openedWithoutTrigger` is `false`, and `triggerCount` is exactly 1); otherwise `undefined`.
  Source: `utils/popups/store.ts:163-175,212-213`. Test: [L2] `popupStoreUtils.test.tsx` "associates a lone trigger with the popup id unless the popup opened without a trigger".
- **PSH-STA-11** — MUST. `triggerProps(isActive)` returns `activeTriggerProps` when `isActive` is `true`, otherwise `inactiveTriggerProps`; a Trigger passes `isMountedByTrigger(ownId)` as `isActive`.
  Source: `utils/popups/store.ts:207-208`, `popover/trigger/PopoverTrigger.tsx:105`. Test: none.
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 04 Amendments 2026-10-27, the interactions session): the interaction prop bags are not store state in the port: the `interactions` module owns its listeners per session, keyed by the popup's session token, and each trigger, popup and backdrop attaches its role to that session (M4 track 3); the store holds no bag. A Trigger's `isMountedByTrigger(ownId)` (`PopupTriggerBehavior.IsMountedByTrigger`) selects which role its handle takes; the row is proven with the session by track 3.

#### 4.3.3 Open-state transitions

- **PSH-STA-12** — MUST. When an open request (`nextOpen === true`) is applied, `preventUnmountingOnClose` becomes `false`; when a close request is applied it becomes `true` iff `preventUnmountOnClose()` was called on the event details during `onOpenChange`, otherwise it keeps its previous value.
  Source: `utils/popups/popupStoreUtils.ts:195-201,228-236`. Test: [L1] `popupStoreUtils.test.tsx` "clears a previous unmount-prevention request when opening", "sets the unmount-prevention request when closing".
- **PSH-STA-13** — MUST. When an open-state change is applied with a trigger element (`eventDetails.trigger`), or when it is an open request, `activeTriggerId` becomes that element's `id` attribute (`null` if no trigger) and `activeTriggerElement` becomes the element (`null` if none); a close request without a trigger leaves both unchanged so exit animations and focus return still target the previous trigger.
  Source: `utils/popups/popupStoreUtils.ts:203-212`. Test: [L1] `popupStoreUtils.test.tsx` "preserves the active trigger when closing without a trigger".
- **PSH-STA-14** — MUST. `openedWithoutTrigger` becomes `true` when an open request carries no trigger and `false` when it carries one; a close request leaves it unchanged.
  Source: `utils/popups/popupStoreUtils.ts:219-224`. Test: [L1] `popupStoreUtils.test.tsx` "records whether an open request carried a trigger", "keeps the trigger-less open flag through a close request".
- **PSH-STA-15** — MUST. Opens that happen through the controlled `open` prop or `defaultOpen` do not pass through the open-change sequence and therefore never set `openedWithoutTrigger`; a lone trigger can claim such a popup.
  Source: `utils/popups/popupStoreUtils.ts:219-224` (comment), `:352-363`. Test: [L2] `popupStoreUtils.test.tsx` "lets a trigger registering into a popup opened by a controlled prop claim it".
- **PSH-STA-16** — MUST. Post-commit, pre-paint of any commit in which the effective open state is `false`, the Root resets `openedWithoutTrigger` to `false` (if set) and `triggerCount` to `0` (if non-zero).
  Source: `utils/popups/popupStoreUtils.ts:445-457`. Test: [L2] `popupStoreUtils.test.tsx` "claims the only registered trigger again after a trigger-less open is closed by a controlled prop", "resets triggerCount when the popup closes".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 8, ADR 16 Amendments 2026-10-27, settled by the orchestrator 2026-10-27 as the worker recommended): the reconciliation runs at the first popup part's render boundary of the batch, `PopupStore.AtBatchBoundaryAsync` from the `OnAfterRender` of the Root, the trigger behaviour and every part that selects the open or mounted state (each registers as a boundary part at subscription); while no such part is registered, the store renders the Root on the reconciliation's inputs. Every reconciliation title runs under both wirings at L2 (`PopupStoreUtilsTests`), and the render counts are measured through `ClassValue`: with a popup part registered, an open and a close render neither the Root nor an inactive trigger.
- **PSH-STA-17** — MUST. While the popup is effectively open, `triggerCount` equals the number of registered triggers and updates synchronously as triggers register/unregister; while closed, registering or unregistering a trigger MUST NOT write to the store at all (the registry alone changes).
  Source: `utils/popups/popupStoreUtils.ts:117-122,459-467`. Test: [L2] `popupStoreUtils.test.tsx` "registers and unregisters closed triggers through the context map without notifying the store", "keeps triggerCount reactive while the popup is open".

#### 4.3.4 Active-trigger reconciliation (Root, post-commit pre-paint while open)

- **PSH-STA-18** — MUST. Lone-trigger claim: post-commit, pre-paint of any commit in which the popup is effectively open, no active trigger id is resolved (PSH-STA-03 yields `null`), `openedWithoutTrigger` is `false`, and exactly one trigger is registered, that trigger's id and element become the active trigger.
  Source: `utils/popups/popupStoreUtils.ts:501-514`. Test: [L2] `popupStoreUtils.test.tsx` "claims the only registered trigger when a closed popup opens".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 8, ADR 16 Amendments 2026-10-27, settled by the orchestrator 2026-10-27 as the worker recommended): the reconciliation runs at the first popup part's render boundary of the batch, `PopupStore.AtBatchBoundaryAsync` from the `OnAfterRender` of the Root, the trigger behaviour and every part that selects the open or mounted state (each registers as a boundary part at subscription); while no such part is registered, the store renders the Root on the reconciliation's inputs. Every reconciliation title runs under both wirings at L2 (`PopupStoreUtilsTests`), and the render counts are measured through `ClassValue`: with a popup part registered, an open and a close render neither the Root nor an inactive trigger.
- **PSH-STA-19** — MUST. The lone-trigger claim MUST NOT happen while `openedWithoutTrigger` is `true`, so a popup opened by `handle.open(null)` / `openWithPayload()` keeps its programmatic payload even when one trigger is registered.
  Source: `utils/popups/popupStoreUtils.ts:504`, `:352-363`. Test: [L2] `popupStoreUtils.test.tsx` "does not claim the only registered trigger when the popup opened without a trigger", "does not let a trigger registering into an open trigger-less popup claim it". Docs: `CHANGELOG.md` v1.9.0 "Don't associate a programmatically opened popup with a trigger that did not open it".
- **PSH-STA-20** — MUST. If the effective active trigger id is not in the registry but the stored `activeTriggerElement` is registered under a different id, the active trigger id is reassociated to that registered id (the element is unchanged) instead of being treated as lost.
  Source: `utils/popups/popupStoreUtils.ts:472-483`. Test: [L2] `popupStoreUtils.test.tsx` "keeps the popup open when the active trigger element is registered with another id", "reassociates the active trigger when ownership moves to another rendered-id trigger while open".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): identity settled by the orchestrator 2026-10-27 (design point 6 of the brief's deliverable 3): an element is the registering part plus its `ElementReference` (`PopupTriggerElement`), registered at the trigger's parameter set with the part alone and re-registered after the render that gives it an element, so two registrations are the same element only when the same part registers the same element reference; the stored element is reassociated by that pair (L2 "keeps the popup open when the active trigger element is registered with another id").
- **PSH-STA-21** — MUST. If the effective active trigger id is in the registry with an element different from the stored `activeTriggerElement`, the stored element is replaced by the registered one.
  Source: `utils/popups/popupStoreUtils.ts:491-496`. Test: [L2] `popupStoreUtils.test.tsx` "keeps the popup open when the active trigger is replaced with the same id".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): identity settled by the orchestrator 2026-10-27 (design point 6 of the brief's deliverable 3): an element is the registering part plus its `ElementReference` (`PopupTriggerElement`), registered at the trigger's parameter set with the part alone and re-registered after the render that gives it an element, so two registrations are the same element only when the same part registers the same element reference; a replacement instance under the same id is a different element, which the reconciliation stores (L2 "keeps the popup open when the active trigger is replaced with the same id", per wiring).
- **PSH-STA-22** — MUST. By default (`closeOnActiveTriggerUnmount` off), when the active trigger unregisters while the popup is open, ownership is preserved (id and element unchanged) and the popup stays open; no other trigger is claimed.
  Source: `utils/popups/popupStoreUtils.ts:484-490,524-526`. Test: [L2] `popupStoreUtils.test.tsx` "preserves active trigger ownership without closing by default".
- **PSH-STA-23** — MUST. With `closeOnActiveTriggerUnmount` on (Tooltip, PreviewCard), when a *resolved* active trigger (one that was seen registered under that id in an earlier reconciliation) is no longer registered, a close with reason `none` is requested after a microtask, but only if at that time the popup is still open, the effective active trigger id is still the lost id, and no trigger with that id has registered since. Server: see ADR 01 relaxation table.
  Source: `utils/popups/popupStoreUtils.ts:484-490,524-545`, `tooltip/root/TooltipRoot.tsx:81`, `preview-card/root/PreviewCardRoot.tsx:59`. Test: [L4] `popupStoreUtils.test.tsx` "closes when the active trigger unregisters while open", "closes when an implicitly claimed trigger unmounts during the claim commit", "closes when the active trigger unmounts after registering in a count-neutral commit", "keeps the popup open when the active trigger is replaced with the same id".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 8, ADR 16 Amendments 2026-10-27, settled by the orchestrator 2026-10-27 as the worker recommended): the reconciliation runs at the first popup part's render boundary of the batch, `PopupStore.AtBatchBoundaryAsync` from the `OnAfterRender` of the Root, the trigger behaviour and every part that selects the open or mounted state (each registers as a boundary part at subscription); while no such part is registered, the store renders the Root on the reconciliation's inputs. Every reconciliation title runs under both wirings at L2 (`PopupStoreUtilsTests`), and the render counts are measured through `ClassValue`: with a popup part registered, an open and a close render neither the Root nor an inactive trigger. The close for a lost trigger is requested at the boundary that detects the loss, after every parameter set of the batch; per render mode at L4 (`popup.spec.ts`, "closes when the active trigger unregisters while open" on `popup-unmount`): on Server, under a 150 ms one-way circuit delay, the popup outlives its trigger by at least a round trip (ADR 01's row "PSH-STA-23, FLD-FRM-17").
- **PSH-STA-24** — MUST. An active trigger id that has never been matched to a registered trigger is *pending* and MUST NOT request a close under PSH-STA-23, even after it is set, cleared and set again.
  Source: `utils/popups/popupStoreUtils.ts:430-431,485-489`. Test: [L2] `popupStoreUtils.test.tsx` "keeps the popup open when ownership returns to a pending trigger after %s".
- **PSH-STA-25** — MUST. After the PSH-STA-23 close request, if it was not canceled, `activeTriggerId` and `activeTriggerElement` are set to `null`; if it was canceled (a controlled Root declined it), ownership is left unchanged.
  Source: `utils/popups/popupStoreUtils.ts:533-542`. Test: [L2] `popupStoreUtils.test.tsx` "closes when the active trigger unregisters while open".

#### 4.3.5 Trigger registration and data forwarding

- **PSH-STA-26** — MUST. A Trigger registers `(id, element)` in its store's registry when its element is attached and `id` is defined, and unregisters when the element detaches, the Trigger unmounts, its id changes, or its store changes (migration: unregister from the previous store, then register in the current one). Unregistering only removes the entry if the registry still maps that id to the same element.
  Source: `utils/popups/popupStoreUtils.ts:139-178,377-380`. Test: [L2] `popupStoreUtils.test.tsx` "returns a stable callback that unregisters from the store it registered in", "re-registers closed triggers when the trigger id changes without notifying the store", "follows an id change", "registers once the id resolves after the first commit".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): identity settled by the orchestrator 2026-10-27 (design point 6 of the brief's deliverable 3): an element is the registering part plus its `ElementReference` (`PopupTriggerElement`), registered at the trigger's parameter set with the part alone and re-registered after the render that gives it an element, so two registrations are the same element only when the same part registers the same element reference. Registration follows the trigger's id, element and store: an id change, an element change or a store change (a detached trigger whose handle moves to another Root) unregisters and registers again (L2 "returns a stable callback that unregisters from the store it registered in", "re-registers closed triggers when the trigger id changes without notifying the store").
- **PSH-STA-27** — MUST. Registering the same element under the same id in the same store again is a no-op.
  Source: `utils/popups/popupStoreUtils.ts:152-160`. Test: [L2] `popupStoreUtils.test.tsx` "registers and unregisters closed triggers through the context map without notifying the store" (`repeat={3}`).
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): identity settled by the orchestrator 2026-10-27 (design point 6 of the brief's deliverable 3): an element is the registering part plus its `ElementReference` (`PopupTriggerElement`), registered at the trigger's parameter set with the part alone and re-registered after the render that gives it an element, so two registrations are the same element only when the same part registers the same element reference; the same part re-registering the same element reference under the same id is a no-op (L2 "registers and unregisters closed triggers through the context map without notifying the store"; L1 "does not duplicate when the same element is added twice with the same id").
- **PSH-STA-28** — MUST. When a Trigger registers and its id equals the effective active trigger id, the store's `activeTriggerElement` is updated to the registering element, and, if the popup is effectively open, the trigger's forwarded state (its `payload` and the component-specific fields listed in §6) is written to the store.
  Source: `utils/popups/popupStoreUtils.ts:343-350`. Test: [L2] `detachedTriggersConformanceTests.tsx` "opens initially with the payload of a non-first `defaultTriggerId`".
  Fidelity note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.8, settled by the orchestrator 2026-10-28; conventions §3.5): fixed by M4 track 6 after PR 126 merged. Base UI's `useTriggerDataForwarding` re-runs whenever `isMountedByThisTrigger` becomes true (`utils/popups/popupStoreUtils.ts:382-391`), so a trigger that takes the mounted popup back from another trigger writes its forwarded state again; track 1's `PopupTriggerBehavior` wrote it once per registration and wrote nothing on the way back (L2 "port: a trigger that takes the popup back from another trigger writes its payload again", `PopupTriggerForwardingTests`).
- **PSH-STA-29** — MUST. When a Trigger registers while the popup is effectively open, no active trigger id is resolved and `openedWithoutTrigger` is `false`, that trigger immediately becomes the active trigger and its forwarded state is written.
  Source: `utils/popups/popupStoreUtils.ts:352-363`. Test: [L2] `popupStoreUtils.test.tsx` "lets a trigger registering into a popup opened by a controlled prop claim it".
- **PSH-STA-30** — MUST. While `isMountedByTrigger(ownId)` is `true`, any change to a Trigger's forwarded state values is written to the store post-commit, pre-paint, together with the current trigger element.
  Source: `utils/popups/popupStoreUtils.ts:382-391`. Test: [L2] `viewportConformanceTests.tsx` "cleans up the transition when a lagging payload remounts the current container".
  Fidelity note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.8, settled by the orchestrator 2026-10-28; conventions §3.5): "while" includes the moment `isMountedByTrigger(ownId)` becomes `true`: the effect's dependencies include it, so every time a trigger comes to own the mounted popup its current values are written, whether or not they changed since it last owned it (PSH-STA-28's note; L2 "port: a trigger that takes the popup back from another trigger writes its payload again").
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.12, settled by the orchestrator 2026-10-28): the effect also depends on the store, so Base UI forwards again when a trigger that stays the owner changes store (a handle's root replaced). The port's registration path covers it: a store change always re-registers the trigger, and PSH-STA-28's `ApplyTriggerData` writes the payload into the new store, so the trigger records no store of its own; a title for the case passes on `main` as on this branch, which ADR 11 §3's red check refuses.
- **PSH-STA-31** — MUST. Switching the active trigger while the popup stays open (hover/focus/click on another trigger) updates `activeTriggerId`, `activeTriggerElement` and `payload` without closing: `onOpenChange` is not called with `false` and the popup/positioner DOM elements are reused.
  Source: `utils/popups/popupStoreUtils.ts:203-212,343-350`. Test: [L4] `detachedTriggersConformanceTests.tsx` "reuses the popup DOM nodes when switching triggers", "does not leave an inline scale style on the popup after switching triggers".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`). The second title ("does not leave an inline scale style on the popup after switching triggers") needs a Viewport and stays open to M4 track 5.
  Port note 2026-10-29 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.8, settled by the orchestrator 2026-10-28): the payload clause, for a trigger that takes the popup back from another (A, B, then A), is also proved by the L2 title "port: a trigger that takes the popup back from another trigger writes its payload again" (PSH-STA-28/30's notes).
- **PSH-STA-32** — MUST. The store does not clear `payload` when the popup closes or its subtree unmounts; the value persists until replaced by a trigger activation or `openWithPayload`. A remounted Root starts with `payload === undefined`.
  Source: `utils/popups/popupStoreUtils.ts:586-595` (only `activeTriggerId`, `activeTriggerElement`, `mounted`, `preventUnmountingOnClose` are reset). Test: [L2] `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts" (remount shows "No payload").

#### 4.3.6 Trigger registry

- **PSH-STA-33** — MUST. The registry maps ids to elements in insertion order; adding an id that already exists replaces its element (the old element is no longer registered); deleting removes the id; `size` is the number of ids; lookups by element scan all entries.
  Source: `utils/popups/popupTriggerMap.ts:36-128`. Test: [L1] `popupTriggerMap.test.ts` "adds and retrieves elements by id", "replaces an existing element when the id is reused", "deletes elements by id", "does not duplicate when the same element is added twice with the same id"; `popupStoreUtils.test.tsx` "stores and retrieves elements by id", "replaces a registered element when the id is reused", "deletes an element and no longer matches it".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): identity settled by the orchestrator 2026-10-27 (design point 6 of the brief's deliverable 3): an element is the registering part plus its `ElementReference` (`PopupTriggerElement`), registered at the trigger's parameter set with the part alone and re-registered after the render that gives it an element, so two registrations are the same element only when the same part registers the same element reference (L1 `PopupTriggerRegistryTests`, `popupTriggerMap.test.ts` and `popupStoreUtils.test.tsx` titles, and "port: two registrations are the same element only when the same part registers the same element reference").

#### 4.3.7 Handles

- **PSH-STA-34** — MUST. A handle exposes its inert fallback store while no Root is attached, and the most recently attached still-attached Root store otherwise; its server-rendering store is always the fallback store.
  Source: `utils/popups/popupHandle.ts:117-128`. Test: [L1] `popupHandle.test.ts` "exposes the fallback store until a root store attaches, and again after it detaches".
- **PSH-STA-35** — MUST. Attachments form a stack: detaching a Root that is not the most recent leaves the exposed store unchanged; detaching the most recent restores the previous still-attached Root (or the fallback when none remain); attaching the same store again after detaching it re-attaches it.
  Source: `utils/popups/popupHandle.ts:149-152,175-184`. Test: [L1] `popupHandle.test.ts` "restores control to the most recently attached root that is still attached", "stays attached when the same root store detaches and reattaches (Strict Mode replay)".
- **PSH-STA-36** — MUST. Store-pointer listeners are notified synchronously on every change of the exposed store and only then; detached triggers re-read the store and migrate their registration (PSH-STA-26) in response.
  Source: `utils/popups/popupHandle.ts:135-141,191-198`, `utils/popups/usePopupHandleStore.ts:20-35`. Test: [L1] `popupHandle.test.ts` "notifies subscribers only when the exposed store changes".
- **PSH-STA-37** — MUST. The inert fallback store reports the component's initial closed state for every read, has a live (mutable) trigger registry, and ignores every write: `set`, `update`, `setOpen` (and Tooltip's `cancelPendingOpen`) are no-ops.
  Source: `utils/NullStore.ts:11-26`, `popover/store/PopoverStore.ts:193-202`, `dialog/store/DialogStore.ts:103-111`, `tooltip/store/TooltipStore.ts:109-118`. Test: [L1] `popupHandle.test.ts` "ignores open() and close() with a warning before a root attaches and after it detaches" (fallback `setOpen` never called).
- **PSH-STA-38** — MUST. `handle.open(id)` resolves the trigger element by searching the attached Root stores' registries from most recently attached to least, then the fallback store's registry; the first match wins. The open request is always sent to the *current* attached store.
  Source: `utils/popups/popupHandle.ts:226-239,259-262`. Test: [L1] `popupHandle.test.ts` "resolves a trigger that is still registered in $location", "prefers the newest attached store when several register the same id".
- **PSH-STA-39** — MUST. `handle.openWithPayload(p)` writes `payload = p` to the attached store and then requests an open with reason `imperative-action` and no trigger (so `openedWithoutTrigger` becomes `true`).
  Source: `dialog/store/DialogHandle.ts:42-57`. Test: none (ignored-while-detached case: `popupHandle.test.ts` "$component handle ignores openWithPayload() with a warning while detached").
- **PSH-STA-40** — MUST. Each Root mount starts from fresh store state: a Root unmounted while open and remounted with the same handle is closed and has no payload, and imperative calls made while it was unmounted are not replayed.
  Source: `utils/popups/popupStoreUtils.ts:79`, `utils/popups/popupHandle.ts:213-224`. Test: [L2] `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts". Docs: `components/dialog/page.mdx` §Detached triggers; `CHANGELOG.md` v1.6.0 "Prevent `<Dialog.Root>` from reopening after remounting with a reused handle".

#### 4.3.8 Other synced state

- **PSH-STA-41** — MUST. `openMethod` (the open interaction type) is reset to `null` post-commit, pre-paint whenever the effective open state becomes `false`, and when the Root unmounts.
  Source: `utils/popups/popupStoreUtils.ts:633-652`, `utils/useOpenInteractionType.ts:50-54`. Test: none.
- **PSH-STA-42** — MUST. The three interaction prop bags are written by the Root's interactions while they are mounted and reset to empty objects when the interactions unmount.
  Source: `utils/popups/popupStoreUtils.ts:612-631`. Test: [L2] `popupStoreUtils.test.tsx` "clears stored interaction props when unmounting".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 04 Amendments 2026-10-27, the interactions session): the interaction prop bags are not store state in the port: the `interactions` module owns its listeners per session, keyed by the popup's session token, and each trigger, popup and backdrop attaches its role to that session (M4 track 3); the store holds no bag. The session ends when its last role detaches; the row is proven by track 3.
- **PSH-STA-43** — MUST. The Root's interactions (and therefore the prop bags) are mounted whenever the popup is open, mounted, or the Root has a `handle` — so detached triggers always receive stable interaction attributes. Exception: Tooltip has no `handle` term — its Root mounts the interactions iff open, mounted, or (`!disabled && trackCursorAxis !== 'none'`) (TIP-STA-15); its hover/focus bags are per Trigger and unaffected.
  Source: `popover/root/PopoverRoot.tsx:83-91`, `dialog/root/useRenderDialogRoot.tsx:89-103`, `preview-card/root/PreviewCardRoot.tsx:84`; exception `tooltip/root/TooltipRoot.tsx:137-145`. Test: none. Docs: `CHANGELOG.md` v1.9.0 "Avoid re-rendering inactive triggers on open and close".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 04 Amendments 2026-10-27, the interactions session): the interaction prop bags are not store state in the port: the `interactions` module owns its listeners per session, keyed by the popup's session token, and each trigger, popup and backdrop attaches its role to that session (M4 track 3); the store holds no bag. The Root attaches the session whenever the popup is open, mounted, or has a handle; the row is proven by track 3.
- **PSH-STA-44** — MUST. `mounted` in the store is seeded synchronously at Root creation to the transition hook's initial mounted value, before any part subscribes, so an initially open Root does not appear to be re-opening.
  Source: `utils/popups/popupStoreUtils.ts:598-605`. Test: none. Docs: `CHANGELOG.md` v1.9.0 "Keep transition state consistent when reopening a retained popup".
- **PSH-STA-45** — MUST. When the popup subtree unmounts (automatically after the exit transition, or via `actionsRef.unmount()`), the store sets `activeTriggerId = null`, `activeTriggerElement = null`, `mounted = false`, `preventUnmountingOnClose = false`, then runs the component's unmount hook, then calls `onOpenChangeComplete(false)`.
  Source: `utils/popups/popupStoreUtils.ts:586-595`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active" (no trigger active after close); `popupConformanceTests.tsx` "is called on close when there is no exit animation".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`).
- **PSH-STA-46** — MUST. The floating root state is kept in sync with the store post-commit, pre-paint: `open` ← effective open, `floatingId` ← store `floatingId`, `referenceElement` (and `domReferenceElement` when it is an element) ← derived active trigger element, `floatingElement` ← `positionerElement` (or `popupElement` for Dialog, AlertDialog and Drawer, which pass `treatPopupAsFloatingElement`).
  Source: `floating-ui-react/hooks/useSyncedFloatingRootContext.ts:49-87`, `utils/popups/popupStoreUtils.ts:81-88`, `dialog/root/useRenderDialogRoot.tsx:48-63`; see [floating-and-positioning](floating-and-positioning.md). Test: [L2] `popupStoreUtils.test.tsx` "syncs the floating id into the popup store for trigger ownership selectors".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 04 Amendments 2026-10-27, the interactions session): the floating root state lives in the modules' session, not in C#: the store provides what it reads (the floating id, the derived active trigger element, the positioner and popup elements, the effective open state through the relay), and the `interactions` and `positioning` handles carry it in their options after each render batch (M4 tracks 2 and 3).

### 4.4 DOM — rendered output, data attributes, CSS variables

Attribute names (`utils/CommonTriggerDataAttributes.ts`, `utils/CommonPopupDataAttributes.ts`, `utils/CommonViewportDataAttributes.ts`, `utils/CommonPopupCssVars.ts`):

| Name | Where | Present when / value |
|---|---|---|
| `data-popup-open` | Trigger | the popup is open *and this trigger is the active trigger* (PSH-DOM-01) |
| `data-pressed` | pressable Trigger | same condition as `data-popup-open` (PSH-DOM-02) |
| `data-open` / `data-closed` | Popup, Positioner, Backdrop, Arrow | exactly one of the two, by effective open state (PSH-DOM-03) |
| `data-starting-style` / `data-ending-style` | Popup, Backdrop (parts using `popupTransitionStateMapping`); viewport containers | transition phase (TRN); viewport usage PSH-DOM-09/10 |
| `data-anchor-hidden` | Popup, Positioner, Arrow | the anchor is hidden (FLT `hideMiddleware`) (PSH-DOM-04) |
| `data-side` | Popup, Positioner, Arrow | `'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-end' \| 'inline-start'` (FLT) |
| `data-align` | Popup, Positioner, Arrow | `'start' \| 'center' \| 'end'` (FLT) |
| `data-activation-direction` | Viewport | `"<h> <v>"` token list (PSH-DOM-07) |
| `data-transitioning` | Viewport | a content transition is in progress (PSH-DOM-08) |
| `data-current` / `data-previous` | Viewport child containers | PSH-STR-07 |
| `data-instant` | Viewport | component-specific `instantType` (not produced by this mechanism; see component docs) |
| `--popup-width` / `--popup-height` | Popup element; `[data-previous]` container | PSH-DOM-11, PSH-DOM-09 |
| `--positioner-width` / `--positioner-height` | Positioner element | PSH-DOM-11 (names owned by FLT `CommonPositionerCssVars`) |
| `data-base-ui-focus-guard` | focus guard | always, empty value |
| `data-base-ui-inert` | internal backdrop | always, empty value |
| `data-base-ui-focusable` | Popup element | always, empty value (PSH-DOM-15) |
| `data-base-ui-click-trigger` | Dialog/Popover Trigger | always, empty value (PSH-DOM-16) |

- **PSH-DOM-01** — MUST. A Trigger using the plain trigger mapping renders `data-popup-open=""` iff its `open` state is `true`, where a Trigger's `open` state is `isOpenedByTrigger(ownId)` (PSH-STA-08); otherwise the attribute is absent. Consequently, with several triggers only the active one carries it, and none does while the popup is closed.
  Source: `utils/popupStateMapping.ts:9-11,30-37`, `popover/trigger/PopoverTrigger.tsx:127-130`, `dialog/trigger/DialogTrigger.tsx:76-79,100`. Test: [L1] `popupStateMapping.test.ts` "emits the trigger data attributes only while open"; `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active"; `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled" (L4 for the two conformance-suite citations).
- **PSH-DOM-02** — MUST. A Trigger using the pressable trigger mapping renders both `data-popup-open=""` and `data-pressed=""` iff its `open` state is `true`, and neither otherwise. Popover.Trigger uses the pressable mapping only when the popup was opened by this trigger with reason `trigger-press` (`isPressOpenedByTrigger`), and the plain mapping otherwise.
  Source: `utils/popupStateMapping.ts:13-16,39-46`, `popover/trigger/PopoverTrigger.tsx:112-120`, `popover/store/PopoverStore.ts:60-62`, `menu/trigger/MenuTrigger.tsx:266`. Test: [L1] `popupStateMapping.test.ts` "emits the pressed data attribute on pressable triggers only while open".
- **PSH-DOM-03** — MUST. A part using the popup mapping renders `data-open=""` when its `open` state is `true` and `data-closed=""` when it is `false`; exactly one of the two is always present.
  Source: `utils/popupStateMapping.ts:18-24,48-54`. Test: [L1] `popupStateMapping.test.ts` "emits the open and closed data attributes"; `popupConformanceTests.tsx` "opens on trigger click and sets `aria-expanded` on the trigger" (L4 for the `popupConformanceTests.tsx` citation).
- **PSH-DOM-04** — MUST. A part using the popup mapping renders `data-anchor-hidden=""` iff its `anchorHidden` state is `true`.
  Source: `utils/popupStateMapping.ts:26-28,55-60`. Test: [L1] `popupStateMapping.test.ts` "emits the anchor-hidden data attribute only when the anchor is hidden".
- **PSH-DOM-05** — MUST. `popupTransitionStateMapping` is the popup mapping plus the TRN transition-status mapping (`data-starting-style` / `data-ending-style`); the two never conflict and both sets are emitted from the same state object.
  Source: `utils/popupStateMapping.ts:63-70`; see [transitions-and-mounting](transitions-and-mounting.md). Test: [L4] `popupConformanceTests.tsx` "hides the kept-mounted popup once the exit animation finishes".
- **PSH-DOM-06** — MUST. `data-side` and `data-align` on Popup/Positioner/Arrow carry the resolved placement; their value sets are those listed in the table above. (When they are set is specified in FLT.)
  Source: `utils/CommonPopupDataAttributes.ts:28-38`. Test: none here (see `positionerConformanceTests`).
- **PSH-DOM-07** — MUST. The Viewport renders `data-activation-direction` only while a direction string exists; its value is the horizontal token (`right`, `left`, or empty) followed by a space and the vertical token (`down`, `up`, or empty), computed per PSH-ALG-13 from the centers of the previous and new trigger. Consumers match single tokens with `~=`.
  Source: `utils/usePopupViewport.tsx:21-30,291-294,310-343`. Test: [L3] `usePopupViewport.test.tsx` "returns [$expected] for $offset.horizontal, $offset.vertical"; `viewportConformanceTests.tsx` "sets `data-activation-direction` from the previous to the new trigger" (L4 for the `viewportConformanceTests.tsx` citation).
- **PSH-DOM-08** — MUST. The Viewport renders `data-transitioning=""` iff a `[data-previous]` container is currently rendered.
  Source: `utils/usePopupViewport.tsx:229,291-294`, `popover/viewport/PopoverViewport.tsx:34-38` (boolean state → attribute per RND). Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions", "renders children in the `current` container by default".
- **PSH-DOM-09** — MUST. The `[data-previous]` container: carries the `inert` attribute; has inline `position: absolute`; once the previous content size is known has inline `--popup-width: <w>px` and `--popup-height: <h>px` equal to the popup's dimensions when the previous content was rendered; contains a deep DOM clone of the previous `[data-current]` children (not live nodes); and carries `data-ending-style=""` except during the first frame of the transition (PSH-ANI-08).
  Source: `utils/usePopupViewport.tsx:240-257,271-278,209-227`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions".
- **PSH-DOM-10** — MUST. During a transition the `[data-current]` container carries `data-starting-style=""` for the first frame only, after which the attribute is removed and the previous container gains `data-ending-style`.
  Source: `utils/usePopupViewport.tsx:187-205,258-265`. Test: [L4] `viewportConformanceTests.tsx` "keeps the latest transition active during rapid trigger changes".
- **PSH-DOM-11** — MUST. Auto-resize (active while a Viewport is mounted): (a) on each open the popup element's inline `--popup-width`/`--popup-height` are set to `auto` and the positioner's inline `--positioner-width`/`--positioner-height` to the measured popup size in `px`; (b) when the content key changes while open, the popup's variables are set to the *previous* size in `px`, then next frame to the *new* size in `px`, then on animations finished back to `auto`, while the positioner's variables are set to the new size immediately; (c) no inline `scale` style remains on the popup after measurement.
  Source: `utils/usePopupAutoResize.ts:59-68,80-124,213-225`. Test: [L4] `detachedTriggersConformanceTests.tsx` "does not leave an inline scale style on the popup after switching triggers". Docs: `CHANGELOG.md` v1.0.0-beta.x "Refactor popup auto resize logic…".
- **PSH-DOM-12** — MUST. While a Viewport is mounted and the popup is mounted, the popup element has inline anchoring styles depending on the resolved side and text direction: side `top` → `position: absolute; bottom: 0; left: 0`; a physically-left side (`left`, or `inline-start` in LTR, or `inline-end` in RTL) → `position: absolute; top: 0; right: 0`; both conditions → `position: absolute; bottom: 0; right: 0`; any other side → no inline styles. They are removed when the popup unmounts.
  Source: `utils/usePopupAutoResize.ts:54-57,91,123,173-188`. Test: [L4] `viewportConformanceTests.tsx` "anchors the popup for side=$side in $direction mode".
- **PSH-DOM-13** — MUST. A focus guard renders with `tabindex="0"`, `data-base-ui-focus-guard=""`, the visually-hidden inline style (UTL), and `aria-hidden="true"`; except that when VoiceOver on a WebKit engine is detected post-commit, pre-paint, it instead renders `role="button"` and no `aria-hidden`.
  Source: `utils/FocusGuard.tsx:14-41`; see FLT-DOM-13 (owner of the rendered shape; this requirement restates it for PSH consumers). Test: none.
- **PSH-DOM-14** — MUST. The internal backdrop renders `role="presentation"`, `data-base-ui-inert=""`, and inline `position: fixed; inset: 0; user-select: none; -webkit-user-select: none`; when a `cutout` element is given it additionally has an inline `clip-path` polygon that excludes that element's bounding rectangle (PSH-ALG-21). Consumers pass `inert` while the popup is not open.
  Source: `utils/InternalBackdrop.tsx:10-33`, `dialog/portal/DialogPortal.tsx:37`, `popover/positioner/PopoverPositioner.tsx:129`. Test: none.
  Port note 2026-10-28 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design point 3 (a), settled by the orchestrator 2026-10-28; ADR 04 Amendments 2026-10-28): the markup has one writer, `Popup/PopupInternalBackdrop.Render(RenderTreeBuilder, string id, bool inert, Action<ElementReference>? capture)`, which Dialog's Portal and Popover's Positioner call; it renders a generated `id`, by which the outside press names the backdrop, and no cut-out (the `clip-path` is the `popup` module's write, PSH-ALG-21). Test: [L2] `PopupInternalBackdropTests` "port: the internal backdrop renders its id, role presentation, the inert marker and the covering style", "port: the internal backdrop carries inert while its popup closes, and hands its element to the capture".
- **PSH-DOM-15** — MUST. Every Popup element carries `tabindex="-1"` and `data-base-ui-focusable=""` (the focusable-popup attributes), which the FLT focus manager uses to find the popup inside the positioner.
  Source: `utils/popups/popupStoreUtils.ts:27-30`, `popover/popup/PopoverPopup.tsx:92`, `dialog/popup/DialogPopup.tsx:80`, `floating-ui-react/utils/element.ts:94-96`. Test: none.
- **PSH-DOM-16** — MUST. Dialog.Trigger and Popover.Trigger render `data-base-ui-click-trigger=""`, which the FLT focus manager uses to recognize a pointer press on a trigger.
  Source: `dialog/trigger/DialogTrigger.tsx:91`, `popover/trigger/PopoverTrigger.tsx:141`, `floating-ui-react/components/FloatingFocusManager.tsx:389`. Test: none.
- **PSH-DOM-17** — MUST. The swipe tracker's drag styles on the tracked element: at rest (not swiping, zero delta, not dismissed) only the two movement variables, both `0px`; while swiping, inline `transition: none`, inline `transform: translate3d(<x>px,<y>px,0) scale(<s>)` where `<x>,<y>` is the current drag offset and `<s>` the element's initial scale, and the movement variables equal the offset minus the initial translation in `px`; after release or cancel the element's original inline `transition` and `transform` (snapshotted at swipe start) are restored and the movement variables keep the final delta (`0px` after a snap-back).
  Source: `utils/useSwipeDismiss.ts:60-62,214-247,969-994`. Test: [L3] `useSwipeDismiss.test.tsx` "applies the drag transform imperatively while swiping", "preserves inline transform and transition when resetting drag styles", "keeps the drag transform on a render that lags behind the swiping state", "cancels pointer swipe when the primary mouse button is released without pointerup".
- **PSH-DOM-18** — MUST. The movement CSS variable names are supplied by the consumer: Drawer uses `--drawer-swipe-movement-x`/`-y`; Toast's own swipe implementation uses `--toast-swipe-movement-x`/`-y` (TST, not this tracker).
  Source: `drawer/viewport/DrawerViewport.tsx:316-319`, `drawer/popup/DrawerPopupCssVars.ts:20,25`, `toast/root/ToastRootCssVars.ts:20,25`. Test: none.

### 4.5 ARIA

- **PSH-ARIA-01** — MUST. A click-opened Trigger (Dialog, AlertDialog, Drawer, Popover, Menu, Select, Combobox) renders `aria-expanded="true"` iff `isOpenedByTrigger(ownId)` and `aria-expanded="false"` otherwise; Tooltip and PreviewCard triggers do not render `aria-expanded`.
  Source: `popover/trigger/PopoverTrigger.tsx:144`, `dialog/trigger/DialogTrigger.tsx:94`, `test/detachedTriggersConformanceTests.tsx:216-224` (`ariaExpanded` flag). Test: [L4] `popupConformanceTests.tsx` "opens on trigger click and sets `aria-expanded` on the trigger"; `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`).
- **PSH-ARIA-02** — MUST. Such a Trigger renders `aria-controls="<popup id>"` iff `triggerPopupId(ownId)` resolves (PSH-STA-10), and omits the attribute otherwise; so while open only the owning trigger (or the lone registered trigger) references the popup, and no trigger does while closed.
  Source: `popover/trigger/PopoverTrigger.tsx:62,145`, `dialog/trigger/DialogTrigger.tsx:50,95`, `menu/store/MenuStore.ts` `triggerControlsId`. Test: [L2] `popupConformanceTests.tsx` "has the `aria-controls` attribute on the trigger", "allows a custom `id` prop"; `detachedTriggersConformanceTests.tsx` (`expectActiveTrigger`).
- **PSH-ARIA-03** — MUST. `aria-controls` uses the popup element's own `id` when the consumer sets one on the Popup, otherwise the generated popup id.
  Source: `utils/popups/store.ts:152-155`. Test: [L2] `popupConformanceTests.tsx` "allows a custom `id` prop".
- **PSH-ARIA-04** — MUST. `aria-haspopup` on a click-opened Trigger has the component's fixed value (`dialog` for Dialog/Popover, `menu` for Menu, `listbox` for Select/Combobox) regardless of open state. Exception: `Combobox.Trigger` renders `"dialog"` instead of `"listbox"` when the combobox input is rendered inside the popup (the Combobox input itself, when it carries the combobox role, renders `"grid"` in grid mode and `"listbox"` otherwise — CMB).
  Source: `popover/trigger/PopoverTrigger.tsx:143`, `dialog/trigger/DialogTrigger.tsx:93`, `menu/trigger/MenuTrigger.tsx:240`, `select/trigger/SelectTrigger.tsx:137`; exception `combobox/trigger/ComboboxTrigger.tsx:160`, `combobox/root/AriaCombobox.tsx:1342, 1369`. Test: [L2] `popupConformanceTests.tsx` "has the `aria-haspopup` attribute on the trigger".
- **PSH-ARIA-05** — MUST. A focus guard is `aria-hidden="true"`, except under VoiceOver on WebKit where it is `role="button"` without `aria-hidden` so the virtual cursor can land on it.
  Source: `utils/FocusGuard.tsx:16-24,37`; see FLT-DOM-13, FLT-ARIA-05. Test: none.
- **PSH-ARIA-06** — MUST. The internal backdrop has `role="presentation"`.
  Source: `utils/InternalBackdrop.tsx:21`. Test: none.
- **PSH-ARIA-07** — MUST. The `[data-previous]` viewport container has the `inert` attribute for its whole lifetime.
  Source: `utils/usePopupViewport.tsx:242`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions".
- **PSH-ARIA-08** — MUST. A Popup part rendered open carries its component's fixed popup `role` (the value each component passes to the popup conformance suite as `expectedPopupRole`): Dialog and Popover `dialog`, AlertDialog `alertdialog`, Menu `menu`, Select and Combobox `listbox`; how each Popup derives it is stated in the component doc. Components that pass no expected role (Tooltip, PreviewCard, Drawer) are not checked by this rule.
  Source: `test/popupConformanceTests.tsx:161-167`; `dialog/popup/DialogPopup.tsx:42,79`, `menu/popup/MenuPopup.tsx:160-162`; `expectedPopupRole` in `dialog/root/DialogRoot.test.tsx`, `alert-dialog/root/AlertDialogRoot.test.tsx`, `popover/root/PopoverRoot.test.tsx`, `menu/root/MenuRoot.test.tsx`, `select/root/SelectRoot.test.tsx`, `combobox/root/ComboboxRoot.test.tsx`. Test: [L2] `popupConformanceTests.tsx` "has the ${expectedPopupRole} role on the popup" (one variant per component that passes a role).

### 4.6 KEY — keyboard interaction

Keys handled by this mechanism (all others pass through to FLT/UBT/the component):

| Key | Where | Condition | Effect |
|---|---|---|---|
| <kbd>Tab</kbd> | on the active trigger | trigger focus guards enabled (PSH-FOC-07) | focus lands on the after-guard from outside the positioner → focus moves to the popup's before-content guard, i.e. into the popup (FLT then focuses the first tabbable); popup stays open (PSH-KEY-01) |
| <kbd>Tab</kbd> | on the last tabbable inside the popup | same | FLT moves focus to the after-guard from inside the positioner → popup closes with reason `focus-out`, focus moves to the next tabbable after the guard in document order, excluding the positioner subtree (PSH-KEY-02) |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | on the active trigger | same | focus lands on the before-guard → popup closes with reason `focus-out`, focus moves to the previous tabbable before the guard (PSH-KEY-03) |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | on the first tabbable inside a Popover popup | same | FLT returns focus to the trigger (not this mechanism); a second <kbd>Shift</kbd>+<kbd>Tab</kbd> then behaves as above |
| <kbd>Escape</kbd> | anywhere while open | FLT `useDismiss` | close with reason `escape-key` (PSH-DIS-01) |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | on a trigger | UBT press normalization | the resulting click has `detail === 0` → open interaction type `keyboard` (PSH-PTR-01) |

- **PSH-KEY-01** — MUST. When focus arrives on the trigger's after-guard from an element outside the positioner (typically <kbd>Tab</kbd> from the trigger), focus is moved to the popup's before-content guard; the popup is not closed.
  Source: `utils/popups/useTriggerFocusGuards.ts:62-66`, `popover/popup/PopoverPopup.tsx:120-121`. Test: [L4] `useTriggerFocusGuards.test.tsx` (forward direction setup: tab origin is inside, so this path is exercised by `openPopup` + `focusTabOrigin`); none directly.
- **PSH-KEY-02** — MUST. When focus arrives on the trigger's after-guard from inside the positioner (<kbd>Tab</kbd> past the last popup tabbable), the popup is closed synchronously with reason `focus-out` and `trigger` = the guard element, and focus is then moved to the next tabbable element after the guard (or after the trigger if the guard has been removed) in the *current* tab order, skipping the positioner subtree.
  Source: `utils/popups/useTriggerFocusGuards.ts:38-56,67`. Test: [L4] `useTriggerFocusGuards.test.tsx` "preserves the tab destination after the exit transition with finalFocus as a %s and trigger tabIndex=%s" (forward), "uses the current tab order when an adjacent control is %s during close" (forward).
- **PSH-KEY-03** — MUST. When focus arrives on the trigger's before-guard (<kbd>Shift</kbd>+<kbd>Tab</kbd> from the trigger), the popup is closed synchronously with reason `focus-out` and `trigger` = the guard element, and focus is moved to the previous tabbable element before the guard (or before the trigger if the guard has been removed), skipping the positioner subtree.
  Source: `utils/popups/useTriggerFocusGuards.ts:38-60`. Test: [L4] `useTriggerFocusGuards.test.tsx` "preserves the tab destination after the exit transition…" (backward), "uses the current tab order when an adjacent control is %s during close" (backward).
- **PSH-KEY-04** — MUST. The destination of PSH-KEY-02/03 is resolved *after* the close has been applied (so tab-order changes made by the close callback are honored), is found even when the trigger's own `tabindex` is `-1`, and the trigger's `tabindex` attribute is unchanged by the close.
  Source: `utils/popups/useTriggerFocusGuards.ts:45-55`. Test: [L4] `useTriggerFocusGuards.test.tsx` "uses the current tab order when an adjacent control is %s during close", "preserves the tab destination … trigger tabIndex=%s".
- **PSH-KEY-05** — MUST. When no tabbable element exists in the requested direction, focus ends on the trigger after the close (the FLT focus manager's focus return), and the popup still closes.
  Source: `utils/popups/useTriggerFocusGuards.ts:51-55` (`?.focus()` no-op), FLT return focus. Test: [L4] `useTriggerFocusGuards.test.tsx` "returns focus to the trigger when no outside element is tabbable and trigger tabIndex=%s".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the clause through `PopoverRoot.test.tsx`'s two "does not loop focus between guards" titles (POP-KEY-11, contained and detached triggers, both modes): with no other tabbable element, Tab out of the popup and Shift+Tab out of the trigger each close it once and leave focus on the trigger. On Server the `popup` module resolves the destination as the closed document gives it (POP-KEY-11's port note).
- **PSH-KEY-06** — MUST. Closing through the trigger focus guards inside a modal Dialog keeps the Dialog's focus trap intact: the destination is the next/previous tabbable inside the dialog.
  Source: `utils/popups/useTriggerFocusGuards.ts:51-55` (tabbable search is document-wide; the dialog's trap, FLT, constrains it). Test: [L4] `useTriggerFocusGuards.test.tsx` "preserves the surrounding modal dialog focus trap".
- **PSH-KEY-07** — MUST. The focus-guard close is applied synchronously (flushed before the handler returns) so that the destination resolution in PSH-KEY-04 observes the closed state. Server: see ADR 01 relaxation table.
  Source: `utils/popups/useTriggerFocusGuards.ts:45-47`. Test: none.

### 4.7 PTR — pointer, mouse, touch and pen

#### 4.7.1 Open interaction type

- **PSH-PTR-01** — MUST. A trigger's open interaction type is determined on activation: on `pointerdown` the event's `pointerType` (`mouse`, `touch`, `pen`) is remembered; on `click`, if `detail === 0` the type is `keyboard`; otherwise the click's `pointerType` when the event carries one, else the remembered `pointerdown` type, else `''`.
  Source: `packages/utils/src/useEnhancedClickHandler.ts:14-45` (UBT), `utils/useOpenInteractionType.ts:13-29`. Test: [L4] `popupFocusPropsTests.tsx` "passes the interaction type to a function, and does nothing when it returns undefined" (`mouse`, `keyboard`, `touch`).
- **PSH-PTR-02** — MUST. The interaction type is written to `openMethod` only when the popup is *closed* at the moment of the handler (so a closing click does not overwrite it); an empty type resolves to `touch` on iOS and stays `''` elsewhere.
  Source: `utils/useOpenInteractionType.ts:13-27`. Test: none.
- **PSH-PTR-03** — MUST. Root-level variant: `useOpenInteractionType(open)` keeps `openMethod` in local state, resets it to `null` when `open` goes from `true` to `false`, and returns the same `onClick`/`onPointerDown` handlers for the trigger.
  Source: `utils/useOpenInteractionType.ts:45-63`. Test: none.

#### 4.7.2 Mixed mousedown/click toggle guard

- **PSH-PTR-04** — MUST. When enabled with `mouseDownAction: 'open'`, a `mousedown` on the trigger while the popup is closed (or, with `'close'`, while open) arms a one-shot suppression: the next `click` dispatched to the trigger is marked as handled (`preventBaseUIHandler()`, EVD) so the toggle does not immediately reverse; the suppression is also disarmed by the next `click` anywhere in the document. When disabled, no handlers are attached.
  Source: `utils/useMixedToggleClickHandler.ts:12-43`, `menu/trigger/MenuTrigger.tsx:209-213` (Menubar only). Test: none here (see MBR).

#### 4.7.3 Internal backdrop and anchored scroll lock

- **PSH-PTR-05** — MUST. While a modal popup is mounted, the internal backdrop covers the whole viewport beneath the popup so pointer interaction with outside elements is blocked, except within the `cutout` element's rectangle (the trigger for Popover/Select/Menu; the input group, input or trigger for Combobox) which stays interactive. Dialog renders it only when `modal === true`; Popover only when `modal === true` and the popup was not opened by hover.
  Source: `utils/InternalBackdrop.tsx:13-33`, `dialog/portal/DialogPortal.tsx:36-38`, `popover/positioner/PopoverPositioner.tsx:98,128-130`, `select/positioner/SelectPositioner.tsx:228`, `combobox/positioner/ComboboxPositioner.tsx:125-130`, `menu/positioner/MenuPositioner.tsx:280-286`. Test: none here (component docs).
- **PSH-PTR-06** — MUST. The internal backdrop is marked `data-base-ui-inert` so FLT's outside-press detection treats it as an element that existed when the popup opened (a press on it counts as an outside press).
  Source: `utils/InternalBackdrop.tsx:22-24`, `floating-ui-react/utils/markOthers.ts:13`, `floating-ui-react/hooks/useDismiss.ts:405-411`. Test: none.
- **PSH-PTR-07** — MUST. Anchored scroll lock: when `enabled` is `true` and the popup was not opened by touch, page scroll is locked (UTL `useScrollLock` with the trigger as the reference element); when it was opened by touch, scroll is locked only if `positioner.offsetWidth >= documentElement.clientWidth − 20` (both widths > 0), measured post-commit, pre-paint whenever `enabled`, the touch flag or the positioner element changes; otherwise scroll stays free so the user can swipe outside to dismiss.
  Source: `utils/useAnchoredPopupScrollLock.ts:11,26-42`; consumers `popover/positioner/PopoverPositioner.tsx:100-105`, `menu/positioner/MenuPositioner.tsx:248-253`, `select/positioner/SelectPositioner.tsx:89-94`, `combobox/positioner/ComboboxPositioner.tsx:91-96`. Test: none. Docs: `popover/root/PopoverRoot.tsx:169` JSDoc ("On touch devices, a `true` modal blocks outside taps but leaves the page scrollable unless the popup spans nearly the full viewport width").
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through `popover.spec.ts` "applies scroll lock when a touch-opened popup covers the viewport width" and "does not apply scroll lock when a touch-opened popup is narrower than the viewport" (L4, both modes); `popup.test.ts` "port: the anchored lock holds while enabled, and for a touch open only when the popup spans the viewport" (L3); `PopoverModuleWiringTests` "port: the positioner sends the cut-out and the anchored lock for a modal press, not for a hover, and the touch open" (L2).

#### 4.7.4 Inline-rect trigger tracking (PreviewCard)

- **PSH-PTR-08** — MUST. A trigger using inline-rect tracking records the pointer's client coordinates, the index of the line box under the pointer (±2px tolerance) and the trigger element on `mouseenter` and `mousemove` while the popup is **not** open by this trigger; it records `undefined` instead when the trigger renders fewer than two line boxes; it ignores those events while open; and it clears the record on `focus`.
  Source: `utils/popups/inlineRect.ts:96-129,203-235`, `preview-card/trigger/PreviewCardTrigger.tsx:85-88`. Test: [L3] `inlineRect.test.ts` "clears stored coords on focus", "stores undefined when the trigger does not wrap", "updates stored coords on mouse move while closed", "does not update stored coords on mouse move while open", "does not update stored coords on mouse enter while open".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-18 and 2026-10-27, chosen by the user): the record lives in the `interactions` session (the trigger option `HoveredLine`), captured on `mouseenter` and `mousemove` while the popup is not open by that trigger and cleared on focus; track 7's inline middleware reads it through `readHoveredLine(session)`. L3: the five titles in `interactions.test.ts`.
- **PSH-PTR-29** — MUST. When a non-canceled `trigger-hover` open request carries a trigger element and an event with client coordinates, and the stored inline-rect record does not belong to that trigger, the record is recomputed from that trigger and the event's coordinates (PSH-PTR-08's line-box rules) before the open is dispatched, so the card anchors to the hovered point even though the trigger's own handlers ignore moves while it is open.
  Source: `preview-card/store/PreviewCardStore.ts:78-96`, `utils/popups/inlineRect.ts:226-235`, `utils/popups/popupStoreUtils.ts:279`. Test: [L3] `inlineRect.test.ts` "updates stored coords from a native mouse event while open".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-18 and 2026-10-27, chosen by the user): the hover region recomputes the record just before it reports a hover open (`recordHoveredLineBeforeOpen`), in the module, so the request C# commits already finds it. L3: "updates stored coords from a native mouse event while open".

#### 4.7.5 Swipe tracker

Constants (`utils/useSwipeDismiss.ts:32-39`): default threshold **40 px**; reverse-cancel threshold **10 px**; minimum overall velocity duration **50 ms**; minimum per-sample duration **4 ms**; maximum age of the last sample for release velocity **80 ms**; minimum sample distance **1 px**; ignored-target selector `button,a,input,select,textarea,label,[role="button"]`. Displacement along a direction is `up: −Δy`, `down: +Δy`, `left: −Δx`, `right: +Δx` (`:41-54`).

- **PSH-PTR-09** — MUST. A gesture is considered for starting on `pointerdown`/`touchstart` only when the tracker is enabled, the event is not default-prevented, and (for pointer events) `button === 0`; a start that is refused keeps the press *pending* so later moves can start it.
  Source: `utils/useSwipeDismiss.ts:524-560`. Test: [L3] `useSwipeDismiss.test.tsx` "ignores pointer interactions that were default prevented".
- **PSH-PTR-10** — MUST. Starting is refused when: the `canStart` predicate (given the position, native event and primary direction) returns `false`; for touch-like input the element under the point is inside a scrollable touch target on the gesture axis other than the page scroller (`body`/`documentElement`), unless the start was approved from a scroll edge (PSH-ALG-12); the element under the point matches the ignored-target selector (always for mouse/pen, and for touch only when `ignoreSelectorWhenTouch` is `true`); or `ignoreScrollableAncestors` is `true` and a scrollable ancestor on the gesture axis exists between the target and the tracked element.
  Source: `utils/useSwipeDismiss.ts:335-351,361-387,547-553`. Test: [L3] `useSwipeDismiss.test.tsx` "does not start swiping within a scrollable element when ignoreScrollableAncestors is true", "starts a touch swipe when the page scroller is `body`", "keeps gating on a scrollable descendant of the other axis when `body` scrolls the page".
- **PSH-PTR-11** — MUST. On start: the element's current translation and scale are read from its computed transform and become the initial offset; the element size (`offsetWidth`/`offsetHeight`) is captured; the threshold is resolved (default or the `swipeThreshold` function for the primary direction) and snapshotted for the gesture; for pointer events pointer capture is set on the element; `onSwipeStart(nativeEvent)` fires; swiping becomes `true` (`onSwipingChange(true)`); progress is reported as 0; drag styles are applied (PSH-DOM-17).
  Source: `utils/useSwipeDismiss.ts:389-425`. Test: [L3] `useSwipeDismiss.test.tsx` "fires onSwipingChange on start and end", "applies exponential damping for opposite-direction movement" (`transition: none` right after start), "snapshots swipeThreshold for the active gesture (strict: %s)".
- **PSH-PTR-12** — MUST. `swipeThreshold` numbers below 0 are clamped to 0; the default is 40 px; a function form is re-evaluated with `{ element, direction }` when the intended direction is first detected (PSH-PTR-14).
  Source: `utils/useSwipeDismiss.ts:110-113,168-182,646`. Test: [L3] `useSwipeDismiss.test.tsx` "respects custom swipeThreshold".
- **PSH-PTR-13** — MUST. The first move after a start, when `trackDrag` is `true`, re-anchors the drag origin and the velocity start time to that move's position/time and discards earlier velocity samples (absorbing the press-to-first-move gap); when `trackDrag` is `false`, the original press position is kept so a quick flick whose whole travel lands in one move still registers.
  Source: `utils/useSwipeDismiss.ts:584-602`. Test: [L3] `useSwipeDismiss.test.tsx` "measures release velocity from the first move rather than the press".
- **PSH-PTR-14** — MUST. Direction detection (PSH-ALG-07): until a direction is intended, each move picks the axis with the larger absolute delta from the origin (ties → horizontal) and the sign gives the candidate; the candidate becomes the intended direction only if it is in `directions`; otherwise no direction is set and the move is still tracked (damped).
  Source: `utils/useSwipeDismiss.ts:628-648`. Test: [L3] `useSwipeDismiss.test.tsx` "does not prevent touch scrolling during swipe interactions".
- **PSH-PTR-15** — MUST. Reverse cancel: once a direction is intended, and only when *neither* axis allows both of its directions, a move whose displacement measured from the running *cancel baseline* is at least 10 px less than the *reference displacement* marks the gesture cancelled; a displacement from the baseline above the threshold clears the cancellation. The reference displacement is the displacement from the origin recorded on the move that first set the intended direction; it is not updated afterwards (it is not a running maximum). On each axis the baseline moves to the pointer whenever that axis's movement heads back toward the baseline from beyond it (PSH-ALG-07).
  Source: `utils/useSwipeDismiss.ts:609-626,644,649-663`. Test: [L3] `useSwipeDismiss.test.tsx` "preserves inline transform and transition when resetting drag styles" (12 px then back to 16 px releases without dismiss).
- **PSH-PTR-16** — MUST. Damping: movement on an axis with no allowed direction, or in the disallowed direction of an axis, is damped to `sign(d)·√|d|`; allowed movement is applied 1:1; the drag offset only changes on axes that have at least one allowed direction.
  Source: `utils/useSwipeDismiss.ts:469-482,665-674`. Test: [L3] `useSwipeDismiss.test.tsx` "applies exponential damping for opposite-direction movement".
- **PSH-PTR-17** — MUST. Progress is `displacement / (size × scale)` along the primary direction (single-direction trackers) or the intended direction, where `size` is the element width for `left`/`right` and height for `up`/`down`; it is clamped to `[0, 1]`, is 0 when displacement ≤ 0, and `onProgress(progress, { deltaX, deltaY, direction })` fires only when the progress value or any detail changed.
  Source: `utils/useSwipeDismiss.ts:184-212,691-713`. Test: [L3] `useSwipeDismiss.test.tsx` "fires onProgress relative to the element size", "continues firing onProgress when swipe progress is clamped".
- **PSH-PTR-18** — MUST. Pointer-event moves while swiping call `preventDefault()` (text selection); touch-event moves never do, so native touch scrolling is not blocked by the tracker.
  Source: `utils/useSwipeDismiss.ts:579-582`. Test: [L3] `useSwipeDismiss.test.tsx` "does not prevent touch scrolling during swipe interactions".
- **PSH-PTR-19** — MUST. A touch-like move while swiping whose target is inside a scrollable touch target (other than the page scroller) on the gesture axis is ignored unless the gesture was approved from a scroll edge.
  Source: `utils/useSwipeDismiss.ts:571-577`. Test: [L3] `useSwipeDismiss.test.tsx` "resets swiping when touch ends over a scrollable descendant".
- **PSH-PTR-20** — MUST. A pointer move with a non-primary button pressed (`buttons` set and not including the primary bit) cancels the gesture: swiping → `false`, offset reset to the initial transform, direction cleared, pointer capture released, drag styles restored, progress 0 reported with zero deltas, `onCancel(nativeEvent)`.
  Source: `utils/useSwipeDismiss.ts:439-467,843-848`. Test: none.
- **PSH-PTR-21** — MUST. A pointer move with `buttons === 0` after a primary press is treated as the release: if the gesture never activated it is discarded; otherwise the move is applied first (it may carry the threshold-crossing displacement and the peak velocity) and then the release decision runs as for `pointerup`.
  Source: `utils/useSwipeDismiss.ts:850-864,952-954`. Test: [L3] `useSwipeDismiss.test.tsx` "cancels pointer swipe when the primary mouse button is released without pointerup", "commits the swipe when the primary button is released past the threshold without pointerup", "uses the final buttons:0 release move to decide dismissal when it crosses the threshold".
- **PSH-PTR-22** — MUST. Velocity sampling: each applied move records `(offset, time)` unless it is the *first* stationary sample (both axes moved < 1 px) after a moving one; a second consecutive stationary sample is recorded and drives the velocity to ≈0; per-sample velocity is `Δoffset / max(Δt, 4 ms)`.
  Source: `utils/useSwipeDismiss.ts:249-276`. Test: [L3] `useSwipeDismiss.test.tsx` "handles a stationary move at $trailingTime and release at $releaseTime", "decays release velocity while the pointer stays pinned".
- **PSH-PTR-23** — MUST. On release, `velocityX/Y = Δ / max(duration, 50 ms)` where `Δ` is the total drag delta and `duration` the time from the (re-anchored) start to the release (0 → velocity 0); `releaseVelocityX/Y` is the last sampled velocity, replaced per axis by `(offset − lastSample) / max(age, 4 ms)` when the last sample is ≤ 80 ms old and that axis moved ≥ 1 px since it, and forced to 0 on both axes when the last sample is older than 80 ms.
  Source: `utils/useSwipeDismiss.ts:748-778`. Test: [L3] `useSwipeDismiss.test.tsx` "provides swipe velocity on release", "clamps short swipe durations when computing velocity", "provides release velocity from the latest swipe movement", "measures release velocity from the latest movement ($name)", "keeps release velocity when moves arrive sparsely", "does not report a previous gesture velocity for a release without movement".
- **PSH-PTR-24** — MUST. Release decision (PSH-ALG-11): `onRelease(details)` is called first; a boolean return overrides everything (true → dismiss in the intended or primary direction, false → snap back); otherwise a cancelled gesture snaps back; otherwise the first direction in `directions` whose displacement exceeds the snapshotted threshold dismisses; otherwise snap back.
  Source: `utils/useSwipeDismiss.ts:780-826`. Test: [L3] `useSwipeDismiss.test.tsx` "allows onRelease to override dismissal", "respects custom swipeThreshold".
- **PSH-PTR-25** — MUST. Dismissal sets `swipeDirection` to the dismissing direction and `dragDismissed` to `true`, restores the inline styles, and calls `onDismiss(nativeEvent, { direction })`; a snap-back resets the offset, clears `swipeDirection`, restores the styles and reports progress 0 with the release deltas.
  Source: `utils/useSwipeDismiss.ts:816-826`. Test: [L3] `useSwipeDismiss.test.tsx` "respects custom swipeThreshold".
- **PSH-PTR-26** — MUST. On release and cancel, swiping becomes `false` (`onSwipingChange(false)`), the pending state is cleared and, for pointer events, pointer capture is released. A release that arrives while the gesture never activated only clears pending state and reports progress 0.
  Source: `utils/useSwipeDismiss.ts:731-746`. Test: [L3] `useSwipeDismiss.test.tsx` "fires onSwipingChange on start and end", "resets swiping when touch ends over a scrollable descendant".
- **PSH-PTR-27** — MUST. `reset()` returns the tracker to its initial state (no direction, not swiping, not dismissed, progress 0, default threshold, zero offsets) and restores the element's inline styles.
  Source: `utils/useSwipeDismiss.ts:278-306`. Test: [L3] `useSwipeDismiss.test.tsx` "preserves inline transform and transition when resetting drag styles".
- **PSH-PTR-28** — MUST. The tracker exposes `getPointerProps()` (`onPointerDown/Move/Up/Cancel`) and `getTouchProps()` (`onTouchStart/Move/End/Cancel`), both empty when disabled, plus `moveNative(touchEvent, currentTarget)` for feeding a native `touchmove` captured elsewhere into the same pipeline; it also exposes `getDragStyles()` (PSH-DOM-17), `reset()` (PSH-PTR-27) and the state values `swiping`, `swipeDirection` and `dragDismissed`.
  Source: `utils/useSwipeDismiss.ts:959-1031,1107-1126`. Test: none.

### 4.8 FOC — focus management

The focus trap, initial focus and focus return are implemented by the FLT focus manager; this section specifies the shared `initialFocus`/`finalFocus` value contract that `popupFocusPropsTests` enforces for Dialog, AlertDialog, Drawer, Popover, Menu, Select and Combobox, the default `initialFocus` resolver, and the trigger focus guards.

- **PSH-FOC-01** — MUST. Default `initialFocus` (Dialog, Popover): when the popup was opened by touch (open interaction type `touch`) the popup element itself receives focus (so no virtual keyboard opens); for every other interaction type the focus manager's default target (first tabbable element inside the popup) is used.
  Source: `utils/popups/popupStoreUtils.ts:37-40`, `dialog/popup/DialogPopup.tsx:57-58`, `popover/popup/PopoverPopup.tsx:68-69`. Test: [L4] `popupFocusPropsTests.tsx` "focuses the first focusable element by default".
- **PSH-FOC-02** — MUST. `initialFocus` resolves as: an element or a DOM handle → that element is focused on open; `true` or `null` → the default target; `false` → focus is not moved (the trigger keeps focus); `undefined` → PSH-FOC-01.
  Source: FLT `FloatingFocusManager` (`initialFocus`); `popupFocusPropsTests.tsx:76-142`. Test: [L4] `popupFocusPropsTests.tsx` "focuses the element of a ref", "focuses the right element when a function returns $name", "does not move focus when `false`".
- **PSH-FOC-03** — MUST. When `initialFocus` is a function it is called exactly once per open with the open interaction type (`mouse`, `touch`, `pen`, `keyboard` or `''`), after the popup is mounted and never again on close; its return value resolves per PSH-FOC-02, and a return of `undefined` leaves focus where it is.
  Source: FLT `FloatingFocusManager`; `popover/popup/PopoverPopup.tsx:108-113`, `dialog/popup/DialogPopup.tsx:100`. Test: [L4] `popupFocusPropsTests.tsx` "passes the interaction type to a function, and does nothing when it returns undefined", "is not called again when the popup closes". Docs: `CHANGELOG.md` v1.0.0-beta "Fix `initialFocus` as function being called on close".
- **PSH-FOC-04** — MUST. `finalFocus` resolves on close as: `undefined`, `true` or `null` → the element that opened the popup (the active trigger, or the previously focused element for a programmatic open); an element or DOM handle → that element; `false` → focus is not moved (when the focused close control is then removed from the DOM, focus falls back to `<body>`; a kept-mounted control retains it).
  Source: FLT `FloatingFocusManager` (`returnFocus`); `popover/popup/PopoverPopup.tsx:114-119`. Test: [L4] `popupFocusPropsTests.tsx` "focuses the trigger by default", "focuses the element of a ref", "focuses the right element when a function returns $name", "does not move focus when `false`".
- **PSH-FOC-05** — MUST. When `finalFocus` is a function it is called exactly once per close with the close interaction type: `mouse` when the close control was pressed with a mouse (components whose close control cannot report a pointer type document their value via the suite's `mouseCloseType`), `keyboard` for <kbd>Escape</kbd>.
  Source: FLT `FloatingFocusManager`; `popupFocusPropsTests.tsx:301-342`. Test: [L4] `popupFocusPropsTests.tsx` "passes the close type to a function".
- **PSH-FOC-06** — MUST. When a popup opened programmatically (controlled `open`/`triggerId`) is closed from inside with its Close part, focus returns to the element that had focus when the popup opened.
  Source: FLT `FloatingFocusManager` return-focus; `popover/popup/PopoverPopup.tsx:115-119`. Test: [L4] `detachedTriggersConformanceTests.tsx` "allows controlling the open state and active trigger programmatically".
- **PSH-FOC-07** — MUST. Trigger focus guards (PSH-STR-10, PSH-KEY-01..05) are rendered around a Popover.Trigger iff `isOpenedByTrigger(ownId)` and the popup is not in focus-manager-modal mode (PSH-FOC-08); around a Menu.Trigger iff `isOpenedByTrigger(ownId)` and the trigger is not inside a Menubar.
  Source: `popover/store/PopoverStore.ts:67-68`, `popover/trigger/PopoverTrigger.tsx:80,157-168`, `menu/trigger/MenuTrigger.tsx:272,288-306`. Test: [L4] `useTriggerFocusGuards.test.tsx` (all cases render `modal={false}`).
- **PSH-FOC-08** — MUST. Popover's focus-manager-modal flag is `true` iff `modal !== false` **and** at least one Popover.Close part is currently mounted inside the Popup; Close parts register post-commit, pre-paint on mount and unregister on unmount, and the count never goes below 0.
  Source: `utils/closePart.tsx:12-37`, `popover/popup/PopoverPopup.tsx:37,71-72,123`, `popover/close/PopoverClose.tsx:37`. Test: none here (see POP). Docs: `popover/root/PopoverRoot.tsx:171-173` JSDoc.
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through `PopoverFocusTests` "port: the focus manager traps focus only for a modal popover with a close part" (L2); `popover.spec.ts` "enables modal focus management when `modal=true` and close is rendered", "… `modal=trap-focus` …" and "does not enable modal focus management when close is not rendered" (L4, both modes).
- **PSH-FOC-09** — MUST. The before-trigger guard is reported to the focus manager as an "inside" element, so moving focus from the trigger onto it is not treated as focus leaving the popup before the guard's own handler runs.
  Source: `popover/popup/PopoverPopup.tsx:116`, `menu/popup/MenuPopup.tsx:205`, `utils/popups/useTriggerFocusGuards.ts:16-20`. Test: [L4] `useTriggerFocusGuards.test.tsx` "preserves the tab destination after the exit transition…" (backward).

### 4.9 DIS — dismissal

The dismissal interactions themselves (outside press, Escape, hover-out) are FLT `useDismiss`/hover behavior wired through the interaction prop bags (PSH-STA-42/43). This section specifies what every popup family guarantees through this mechanism.

- **PSH-DIS-01** — MUST. Pressing <kbd>Escape</kbd> while an uncontrolled popup is open closes it, with `onOpenChange(false, { reason: 'escape-key' })`.
  Source: FLT `useDismiss`; `popover/root/PopoverRoot.tsx:241-248`, `utils/popups/popupStoreUtils.ts:273-312`. Test: [L4] `popupConformanceTests.tsx` "opens initially and remains uncontrolled", "is called with the new open state and the change details".
  Port note 2026-10-28 (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): green at L4 on the test-only popup host, both modes. The host's root composes the `interactions` session on the store's token, with Escape dismissal only.
- **PSH-DIS-02** — MUST. With a controlled `open={true}` whose owner does not react to `onOpenChange`, <kbd>Escape</kbd> leaves the popup open (the request is reported but the effective state is the prop).
  Source: `utils/popups/store.ts:150`. Test: [L4] `popupConformanceTests.tsx` "is ignored when `open={true}` is passed, so Escape cannot close".
  Port note 2026-10-28 (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): the title runs with Escape dismissal on the test-only popup host, both modes (PSH-DIS-01's note).
- **PSH-DIS-03** — MUST. `handle.close()` and `actionsRef.current.close()` request a close with reason `imperative-action` and `trigger === undefined`.
  Source: `utils/popups/popupHandle.ts:284`, `popover/root/PopoverRoot.tsx:78`. Test: [L1] `popupHandle.test.ts` "closes the attached root"; `detachedTriggersConformanceTests.tsx` "opens by trigger id with its payload and closes" (L4 for the `detachedTriggersConformanceTests.tsx` citation).
- **PSH-DIS-04** — MUST. Tabbing out through a trigger focus guard requests a close with reason `focus-out` (PSH-KEY-02/03).
  Source: `utils/popups/useTriggerFocusGuards.ts:46`. Test: [L4] `useTriggerFocusGuards.test.tsx` (popup removed after tab).
- **PSH-DIS-05** — MUST. The active-trigger-unmount close (PSH-STA-23) uses reason `none` and no event.
  Source: `utils/popups/popupStoreUtils.ts:533-534`. Test: [L2] `popupStoreUtils.test.tsx` "closes when the active trigger unregisters while open".
- **PSH-DIS-06** — MUST. A canceled open request (`cancel()` inside `onOpenChange`) leaves the popup closed and the trigger without `data-popup-open`; no further `onOpenChange` call is made for that request.
  Source: `utils/popups/popupStoreUtils.ts:275-277`, `popover/store/PopoverStore.ts:141-143`, `dialog/store/DialogStore.ts:88-90`. Test: [L4] `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled" (L4 for the `popupConformanceTests.tsx` citation); `popupStoreUtils.test.tsx` "notifies onOpenChange but short-circuits before dispatch when canceled".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 13 §3, settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): re-tagged [L2] → [L4], since the cited title sits in a browser-only suite block or presses a trigger; green on the test-only popup host on both render modes (`tests/browser/popup.spec.ts` through `tests/browser/suites/`, the trigger pressed from a C# `onclick`, design point 4), and an L2 port title keeps the store's clause (`PopupStorePortTests`). The `popupStoreUtils.test.tsx` title stays [L1] (`PopupOpenChangeTests`).

### 4.10 EVT — callbacks

- **PSH-EVT-01** — MUST. Every open-state change request runs the popup open-change sequence specified in [event-details-and-reasons](event-details-and-reasons.md) §5 (EVD-ALG-04, with the cancel semantics of EVD-EVT-07 and the internal notification of EVD-EVT-14); the store state it commits is PSH-ALG-02 (= EVD-ALG-05) merged with any store-specific extra state, with `open` always equal to the requested value. This document does not restate the sequence.
  Source: `utils/popups/popupStoreUtils.ts:246-313`; equivalent inline sequences in `popover/store/PopoverStore.ts:110-183`, `dialog/store/DialogStore.ts:72-95`, `menu/root/MenuRoot.tsx:395-420`. Test: [L1] `popupStoreUtils.test.tsx` "runs the full sequence in order when the change is not canceled", "notifies onOpenChange but short-circuits before dispatch when canceled", "merges extraState into the update with `open` always reflecting nextOpen".
- **PSH-EVT-02** — MUST. Opening from a trigger calls `onOpenChange` exactly once with `(true, details)` where `details.reason` is `trigger-press` (click) or `trigger-hover` (hover) and `details.trigger` is the trigger element; closing with <kbd>Escape</kbd> calls it exactly once with `(false, details)` and `details.reason === 'escape-key'`.
  Source: `utils/popups/popupStoreUtils.ts:273`, FLT `useClick`/hover. Test: [L4] `popupConformanceTests.tsx` "is called with the new open state and the change details".
- **PSH-EVT-03** — MUST. `onOpenChange` is not called on mount, and not called for a controlled prop change applied by the owner.
  Source: `utils/popups/popupStoreUtils.ts:246-313` (only reachable from `setOpen`). Test: [L2] `popupConformanceTests.tsx` "is called with the new open state and the change details" (`not.toHaveBeenCalled()` before interaction).
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the cited title closes with Escape, which is PSH-DIS-01's and M4 track 3's (`interactions`), so the row is green by an L2 port title on the test-only hosts (`PopupStorePortTests`, "port: …"); the cited title is listed open to track 3 in `popup.spec.ts`. 2026-10-28 (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): the test-only popup root composes the `interactions` session, and the cited title runs in `popup.spec.ts` on both modes.
- **PSH-EVT-04** — MUST. `eventDetails.trigger` on a close request: Popover sets it to the active trigger's registered element (falling back to the stored element) when the reason is `close-press` and no trigger was supplied; Dialog sets it to the active trigger element on any close without a supplied trigger; the shared sequence passes it through unchanged.
  Source: `popover/store/PopoverStore.ts:125-137`, `dialog/store/DialogStore.ts:80-84`. Test: [L4] `detachedTriggersConformanceTests.tsx` "allows controlling the open state and active trigger programmatically" (consumer reads `details.trigger?.id`).
- **PSH-EVT-05** — MUST. During `onOpenChange`, `eventDetails.preventUnmountOnClose()` is available; calling it on a close request keeps the popup mounted until `actionsRef.current.unmount()` (PSH-STA-12, TRN). Calling it on an open request has no effect.
  Source: `utils/popups/popupStoreUtils.ts:228-236,271,288`, `dialog/store/DialogStore.ts:76-78`. Test: [L4] `viewportConformanceTests.tsx` "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)".
- **PSH-EVT-06** — MUST. `handle.open(id)` requests an open with `reason: 'imperative-action'`, `event: undefined`, `trigger: <registered element>`; `handle.open(null)`/`openWithPayload()` use `trigger: undefined`.
  Source: `utils/popups/popupHandle.ts:259-262`, `dialog/store/DialogHandle.ts:56`. Test: [L1] `popupHandle.test.ts` "opens the attached root with the trigger registered in its store", "opens without a trigger when the id is $triggerId".
- **PSH-EVT-07** — MUST. `onOpenChangeComplete` is not called on mount while closed; it is called with `true` once the enter transition finishes (immediately when there is none) and with `false` once the exit transition finishes and the popup unmounts (immediately when there is none); exactly one call per transition.
  Source: `utils/popups/popupStoreUtils.ts:594`, TRN `useOpenChangeComplete`. Test: [L4] `popupConformanceTests.tsx` "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes".
- **PSH-EVT-08** — MUST. There is no separate active-trigger change callback: a controlled consumer derives the new `triggerId` from `eventDetails.trigger?.id` inside `onOpenChange`.
  Source: `popover/root/PopoverRoot.tsx:180-185`. Test: [L4] `detachedTriggersConformanceTests.tsx` "allows controlling the open state and active trigger programmatically". Docs: `components/popover/page.mdx` §Controlled mode with multiple triggers.
- **PSH-EVT-09** — MUST. Swipe tracker callbacks: `onSwipeStart(nativeEvent)`; `onProgress(progress, details?)` with `details = { deltaX, deltaY, direction }`; `onCancel(nativeEvent)`; `onSwipingChange(boolean)`; `onRelease({ event, direction, deltaX, deltaY, velocityX, velocityY, releaseVelocityX, releaseVelocityY })` returning `boolean | void`; `onDismiss(nativeEvent, { direction })`. Velocities are in px/ms.
  Source: `utils/useSwipeDismiss.ts:1079-1104`. Test: [L3] `useSwipeDismiss.test.tsx` "provides swipe velocity on release", "allows onRelease to override dismissal".

### 4.11 FRM — form integration

Not applicable — popups do not participate in Field/Form registration; Select and Combobox do so through their own parts (SEL, CMB, FFI).

### 4.12 ANI — mount lifecycle and transitions

- **PSH-ANI-01** — MUST. `mounted` becomes `true` synchronously with an open request and stays `true` after a close until the popup's exit animations finish (TRN `useUnmountAfterClose` observing the popup element via `popupRef`), after which PSH-STA-45 applies.
  Source: `utils/popups/popupStoreUtils.ts:573-608`; see [transitions-and-mounting](transitions-and-mounting.md). Test: [L4] `popupConformanceTests.tsx` "removes the popup when there is no exit animation defined", "hides the kept-mounted popup once the exit animation finishes".
- **PSH-ANI-02** — MUST. A popup that mounts already open (`defaultOpen`, server-rendered open state) does not play its enter transition unless the Root opts in (`animateInitialOpen`; Menu does so for submenus).
  Source: `utils/popups/popupStoreUtils.ts:564-568,585`, `menu/root/MenuRoot.tsx:226-231`. Test: none.
- **PSH-ANI-03** — MUST. A `trigger-hover` open change is committed synchronously as specified by [EVD-EVT-18](event-details-and-reasons.md); the shared sequence implements this by flushing the store update inside the event. Server: see ADR 01 relaxation table.
  Source: `utils/popups/popupStoreUtils.ts:307-312`, `popover/store/PopoverStore.ts:161-172`. Test: none.
- **PSH-ANI-04** — MUST. The shared open-change sequence writes the reason-derived instant-phase flag (`instantType`) exactly as specified by [EVD-EVT-17](event-details-and-reasons.md) for the shared stores (Tooltip, PreviewCard); the rendering consequences are in TRN.
  Source: `utils/popups/popupStoreUtils.ts:265-269,296-302`. Test: [L1] `popupStoreUtils.test.tsx` "maps the change reason to instantType".
- **PSH-ANI-05** — MUST. A viewport content transition starts post-commit, pre-paint when all hold: the popup is open; an active trigger element exists; a *previous* active trigger element exists (the last one seen while open); they differ; this trigger has not already started a transition; and a content snapshot exists. Then the `[data-previous]` container is created with the snapshot and `data-activation-direction` is computed from the two triggers' centers (PSH-ALG-13).
  Source: `utils/usePopupViewport.tsx:88,156-179`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions", "remounts the `current` container when the active trigger changes".
- **PSH-ANI-06** — MUST. A trigger that remounts (new element, same id) while the popup is closed but retained (`preventUnmountOnClose`) MUST NOT start a transition: no `[data-previous]` container and no `data-transitioning`.
  Source: `utils/usePopupViewport.tsx:88,150-154,159-166`. Test: [L4] `viewportConformanceTests.tsx` "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)". Docs: `CHANGELOG.md` v1.9.0 "Don't start a viewport transition when the trigger remounts while closed".
- **PSH-ANI-07** — MUST. After every commit the viewport captures a deep clone of the current container's children as the candidate snapshot, so that on a switch T1→T2→T3 the previous container always shows the immediately preceding content (T2 when switching to T3).
  Source: `utils/usePopupViewport.tsx:209-227`. Test: [L4] `viewportConformanceTests.tsx` "keeps the latest transition active during rapid trigger changes", "shows the latest content after rapid trigger changes".
- **PSH-ANI-08** — MUST. Transition choreography: in the commit that starts (or restarts) a transition the current container has `data-starting-style` and the previous container lacks `data-ending-style`; next frame, `data-starting-style` is removed (flushed synchronously) and `data-ending-style` appears on the previous container; at that moment the cleanup watcher is armed.
  Source: `utils/usePopupViewport.tsx:187-205,256,262`. Test: [L4] `viewportConformanceTests.tsx` "keeps the latest transition active during rapid trigger changes".
- **PSH-ANI-09** — MUST. Cleanup: on animations finished of the current container (subtree included), the previous container is removed, the stored previous dimensions are cleared and `data-transitioning` disappears; a newer transition aborts the pending watcher and re-arms it.
  Source: `utils/usePopupViewport.tsx:101,137-146,192-204`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions", "cleans up the transition when a lagging payload remounts the current container".
- **PSH-ANI-10** — MUST. The current container is keyed by the content key (PSH-ALG-14): it is remounted (new element) when the active trigger id changes, and once more if the active trigger's payload arrives in a later commit; a remount mid-transition re-runs PSH-ANI-08 instead of leaving the previous container stranded or tearing it down early.
  Source: `utils/usePopupViewport.tsx:91,181-205,233,261,370-399`. Test: [L4] `viewportConformanceTests.tsx` "remounts the `current` container when the active trigger changes", "cleans up the transition when a lagging payload remounts the current container".
- **PSH-ANI-11** — MUST. The transition machinery works again after a kept-mounted popup closes and reopens.
  Source: `utils/usePopupViewport.tsx:150-154`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers after a kept-mounted popup closes and reopens".
- **PSH-ANI-12** — MUST. During an auto-resize measurement the popup has inline `position: static; transform: none; scale: 1`, the positioner's available-size variables are `max-content`, the current container has inline `animation: none; transition: none` and the previous container `display: none`; all of these are restored to their prior inline values before the measurement step returns, and the measured previous size is recorded for PSH-DOM-09.
  Source: `utils/usePopupAutoResize.ts:59-75,88,104,119-135`, `utils/usePopupViewport.tsx:119-135`. Test: [L4] `detachedTriggersConformanceTests.tsx` "does not leave an inline scale style on the popup after switching triggers".
- **PSH-ANI-13** — MUST. Auto-resize state resets when the popup unmounts (`mounted` false), so the next open measures afresh (PSH-DOM-11(a)); while mounted, a change of the content key (the payload for viewports) triggers PSH-DOM-11(b).
  Source: `utils/usePopupAutoResize.ts:43-48,94-124`, `utils/usePopupViewport.tsx:280-289`. Test: none.
- **PSH-ANI-14** — MUST. A popup rendered with `keepMounted` stays in the DOM after closing and becomes inaccessible (hidden) once its exit animation finishes; without `keepMounted` it is removed from the DOM at that point. Popups that are always kept mounted after their first open (Select, Combobox) behave like the former.
  Source: TRN; `test/popupConformanceTests.tsx:52-59,364-417`. Test: [L4] `popupConformanceTests.tsx` "hides the kept-mounted popup once the exit animation finishes", "removes the popup when there is no exit animation defined".

### 4.13 POS — positioning hooks exposed by this mechanism

- **PSH-POS-01** — MUST. The anchor used for positioning is the derived active trigger element (PSH-STA-06, PSH-STA-46); switching the active trigger while open repositions the popup to the new trigger (positioner left edge within 1px of the trigger's for `side: 'bottom', align: 'start'`).
  Source: `floating-ui-react/hooks/useSyncedFloatingRootContext.ts:50,78-85`. Test: [L4] `detachedTriggersConformanceTests.tsx` "allows controlling the open state and active trigger programmatically".
- **PSH-POS-02** — MUST. While a Viewport is mounted the store's `adaptiveOrigin` middleware is set (and cleared when it unmounts), so FLT positions with the adaptive transform-origin (FLT `adaptiveOriginMiddleware`).
  Source: `utils/usePopupViewport.tsx:112-117`. Test: none.
- **PSH-POS-03** — MUST. PreviewCard's inline-rect middleware (PSH-ALG-16) replaces the reference rectangle with the hovered line box of a wrapping trigger, and resets positioning only when the resulting rectangle differs from the current one.
  Source: `utils/popups/inlineRect.ts:237-292`, `preview-card/positioner/PreviewCardPositioner.tsx:78`. Test: [L3] `inlineRect.test.ts` (all middleware cases).

### 4.14 A11Y

Not applicable beyond PSH-ARIA-05/PSH-DOM-13 (VoiceOver-on-WebKit focus-guard heuristic) — this mechanism emits no live regions or announcements.

### 4.15 I18N

- **PSH-I18N-01** — MUST. Viewport anchoring styles (PSH-DOM-12) resolve logical sides physically: `inline-start` is left in LTR and right in RTL; `inline-end` is right in LTR and left in RTL; only physically-left and `top` sides receive anchoring styles.
  Source: `utils/usePopupAutoResize.ts:173-188`. Test: [L4] `viewportConformanceTests.tsx` "anchors the popup for side=$side in $direction mode".
- **PSH-I18N-02** — MUST. Swipe directions (`up`, `down`, `left`, `right`) are physical; the tracker performs no RTL mirroring.
  Source: `utils/useSwipeDismiss.ts:12,41-54`. Test: none.

## 5. Algorithms

Pseudo-code is language-neutral. "post-commit, pre-paint" and "next frame" follow the conventions vocabulary. The popup open-change sequence, the committed open state and close completion are owned by [event-details-and-reasons](event-details-and-reasons.md) (EVD-ALG-04, EVD-ALG-05, EVD-ALG-06) and are only referenced here.

- **PSH-ALG-01** — MUST. The implementation reproduces the popup open-change sequence exactly as EVD-ALG-04 specifies (shared routine for Tooltip/PreviewCard; equivalent inline sequences for Dialog, Popover, Menu).
  Source: `utils/popups/popupStoreUtils.ts:246-313`. Test: [L1] `popupStoreUtils.test.tsx` "runs the full sequence in order when the change is not canceled".
- **PSH-ALG-02** — MUST. The implementation reproduces the committed-open-state derivation exactly as EVD-ALG-05 specifies (`createPopupOpenState`).
  Source: `utils/popups/popupStoreUtils.ts:189-226`. Test: [L1] `popupStoreUtils.test.tsx` "getPopupOpenState" (all four cases).

- **PSH-ALG-03** — MUST. The implementation reproduces the following procedure exactly for active-trigger reconciliation, run by the Root post-commit, pre-paint after every commit in which the effective open state, `triggerCount`, the effective active trigger id, the stored active trigger element, or the `closeOnActiveTriggerUnmount` option changed.

```
reconcile(store, closeOnActiveTriggerUnmount):
  // `resolvedId` persists across runs for this Root; it names the active id last seen registered
  if not effectiveOpen(store):
    resolvedId ← null
    if store.triggerCount ≠ 0: store.triggerCount ← 0
    if store.openedWithoutTrigger: store.openedWithoutTrigger ← false
    return
  registry ← store.context.triggerElements
  updates ← {}
  if store.triggerCount ≠ registry.size: updates.triggerCount ← registry.size
  currentId ← effectiveActiveTriggerId(store)          // PSH-STA-03
  lostId ← null
  if currentId ≠ null:
    el ← registry.getById(currentId)
    if el is undefined:
      for (id, element) in registry.entries():          // insertion order
        if element == store.activeTriggerElement:
          updates.activeTriggerId ← id; updates.activeTriggerElement ← element
          resolvedId ← id; break
      if updates.activeTriggerId is undefined:
        if resolvedId == currentId: lostId ← currentId  // a resolved trigger really unregistered
        else: resolvedId ← null                         // pending id: never registered yet
    else:
      resolvedId ← currentId
      if el ≠ store.activeTriggerElement: updates.activeTriggerElement ← el
  else:
    resolvedId ← null
  if lostId == null and currentId == null and not store.openedWithoutTrigger and registry.size == 1:
    (id, element) ← first entry of registry
    updates.activeTriggerId ← id; updates.activeTriggerElement ← element; resolvedId ← id
  if updates is not empty: store.update(updates)       // one batched write
  if lostId ≠ null and closeOnActiveTriggerUnmount:
    queueMicrotask:
      if effectiveOpen(store) and effectiveActiveTriggerId(store) == lostId and registry.getById(lostId) is undefined:
        details ← changeEventDetails(reason 'none')
        store.setOpen(false, details)                   // PSH-ALG-01
        if not details.isCanceled: store.update({ activeTriggerId: null, activeTriggerElement: null })
```

  Source: `utils/popups/popupStoreUtils.ts:423-555`. Test: [L2] `popupStoreUtils.test.tsx` "claims the only registered trigger when a closed popup opens", "keeps the popup open when the active trigger element is registered with another id", "keeps the popup open when ownership returns to a pending trigger after %s", "closes when the active trigger unmounts after registering in a count-neutral commit", "preserves active trigger ownership without closing by default", "resets triggerCount when the popup closes".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 8, ADR 16 Amendments 2026-10-27, settled by the orchestrator 2026-10-27 as the worker recommended): the reconciliation runs at the first popup part's render boundary of the batch, `PopupStore.AtBatchBoundaryAsync` from the `OnAfterRender` of the Root, the trigger behaviour and every part that selects the open or mounted state (each registers as a boundary part at subscription); while no such part is registered, the store renders the Root on the reconciliation's inputs. Every reconciliation title runs under both wirings at L2 (`PopupStoreUtilsTests`), and the render counts are measured through `ClassValue`: with a popup part registered, an open and a close render neither the Root nor an inactive trigger.

- **PSH-ALG-04** — MUST. The implementation reproduces the following procedure exactly for trigger registration (the callback a Trigger attaches to its element, re-run post-commit, pre-paint when its id or store changes, and with `null` on unmount).

```
register(element):                        // closure state: registration = {store, id, element} | null
  if registration ≠ null:
    if registration.element == element and registration.store == store and registration.id == id: return
    previous ← registration; registration ← null
    if previous.store.context.triggerElements.getById(previous.id) == previous.element:
      previous.store.context.triggerElements.delete(previous.id)
      syncTriggerCount(previous.store)
  if element ≠ null and id is defined:
    registration ← { store, id, element }
    store.context.triggerElements.add(id, element)
    syncTriggerCount(store)

syncTriggerCount(store):
  n ← store.context.triggerElements.size
  if effectiveOpen(store) and store.triggerCount ≠ n: store.triggerCount ← n   // no write while closed
```

  Source: `utils/popups/popupStoreUtils.ts:117-178`. Test: [L2] `popupStoreUtils.test.tsx` "returns a stable callback that unregisters from the store it registered in", "registers once the id resolves after the first commit", "keeps triggerCount reactive while the popup is open".

- **PSH-ALG-05** — MUST. The implementation reproduces the following procedure exactly when a Trigger with forwarded state (`payload` plus component fields, §6) registers.

```
registerWithData(element):
  register(element)                                     // PSH-ALG-04
  if element == null: return
  activeId ← effectiveActiveTriggerId(store); open ← effectiveOpen(store)
  if activeId == id:
    store.update({ activeTriggerElement: element, ...(open ? forwardedState : {}) })
  else if activeId == null and open and not store.openedWithoutTrigger:
    store.update({ activeTriggerId: id, activeTriggerElement: element, ...forwardedState })
// and, post-commit pre-paint, whenever isMountedByTrigger(id) is true and a forwarded value changed:
  store.update({ activeTriggerElement: element, ...forwardedState })
```

  Source: `utils/popups/popupStoreUtils.ts:323-394`. Test: [L2] `popupStoreUtils.test.tsx` "does not let a trigger registering into an open trigger-less popup claim it", "lets a trigger registering into a popup opened by a controlled prop claim it".

- **PSH-ALG-06** — MUST. The implementation reproduces the following procedure exactly for handle attachment and imperative open.

```
attachStore(rootStore):
  attached.push(rootStore); setActive(rootStore)
  if dev and attached.length > 1: next frame → if attached.length > 1: warn(OVERLAP)   // PSH-EDGE-06
  return detach():
    i ← attached.lastIndexOf(rootStore); if i ≥ 0: attached.splice(i, 1)
    setActive(attached.last ?? null)
setActive(s): if active ≠ s: active ← s; for l in listeners: l()
store getter: active ?? fallback;  serverStore getter: fallback

openByTrigger(triggerId):
  if active == null: dev-warn(NO_ROOT_OPEN); return
  element ← undefined
  if triggerId:
    for i from attached.length−1 down to 0 while element is undefined: element ← attached[i].registry.getById(triggerId)
    element ← element ?? fallback.registry.getById(triggerId)
  if triggerId and element is undefined:
    if throwOnMissingTrigger: throw MISSING_TRIGGER      // PSH-EDGE-03
    dev-warn(UNASSOCIATED)                                // PSH-EDGE-04
  active.setOpen(true, changeEventDetails('imperative-action', undefined, element))
closePopup():
  if active == null: dev-warn(NO_ROOT_CLOSE); return
  active.setOpen(false, changeEventDetails('imperative-action'))
isOpen: active ? effectiveOpen(active) : false
```

  Source: `utils/popups/popupHandle.ts:117-285`. Test: [L1] `popupHandle.test.ts` "restores control to the most recently attached root that is still attached", "notifies subscribers only when the exposed store changes", "resolves a trigger that is still registered in $location", "prefers the newest attached store when several register the same id".

- **PSH-ALG-07** — MUST. The implementation reproduces the following procedure exactly for swipe move handling (after start, PSH-PTR-11).

```
onMove(pos, movement):                                  // movement = pos − lastMovePos (0,0 on first)
  if first move after start and trackDrag: origin ← pos; startTime ← now; samples ← ∅
  // cancel baseline: reset per axis when motion reverses past the baseline
  if (movement.y < 0 and pos.y > base.y) or (movement.y > 0 and pos.y < base.y): base.y ← pos.y
  if (movement.x < 0 and pos.x > base.x) or (movement.x > 0 and pos.x < base.x): base.x ← pos.x
  Δ ← pos − origin;  Δc ← pos − base
  if intended is undefined:
    cand ← |Δ.x| ≥ |Δ.y| ? (Δ.x > 0 ? right : left) : (Δ.y > 0 ? down : up)
    if cand ∈ directions: intended ← cand; maxDisp ← disp(cand, Δ); resolveThreshold(cand)
  else:
    cur ← disp(intended, Δc)
    if cur > threshold: cancelled ← false
    else if not (left∧right allowed) and not (up∧down allowed) and maxDisp − cur ≥ 10: cancelled ← true
  d ← damp(Δ)                                            // PSH-ALG-08
  offset ← initial.translation; if horizontalAllowed: offset.x += d.x; if verticalAllowed: offset.y += d.y
  if offset changed: write drag styles                   // PSH-DOM-17
  recordSample(offset, now)                              // PSH-ALG-10
  report progress                                        // PSH-ALG-09
```

  Source: `utils/useSwipeDismiss.ts:562-714`. Test: [L3] `useSwipeDismiss.test.tsx` "applies exponential damping for opposite-direction movement", "preserves inline transform and transition when resetting drag styles".

- **PSH-ALG-08** — MUST. The implementation reproduces the following procedure exactly for directional damping.

```
exponent(v) = sign(v) · √|v|
dampAxis(v, allowNeg, allowPos) = (v < 0 and not allowNeg) or (v > 0 and not allowPos) ? exponent(v) : v
damp(Δ) = { x: horizontalAllowed ? dampAxis(Δ.x, left, right) : exponent(Δ.x),
            y: verticalAllowed   ? dampAxis(Δ.y, up, down)    : exponent(Δ.y) }
```

  Source: `utils/useSwipeDismiss.ts:469-482`. Test: [L3] `useSwipeDismiss.test.tsx` "applies exponential damping for opposite-direction movement".

- **PSH-ALG-09** — MUST. The implementation reproduces the following procedure exactly for swipe progress.

```
dir ← primaryDirection ?? intended;  progress ← 0
if dir: size ← dir ∈ {left,right} ? element.width : element.height;  s ← initial.scale || 1
        p ← disp(dir, offset − initial.translation)
        if size > 0 and s > 0 and p > 0: progress ← p / (size · s)
progress ← finite(progress) ? clamp(progress, 0, 1) : 0
details ← { deltaX, deltaY, direction: intended }
if progress ≠ last or details ≠ lastDetails: onProgress(progress, details)
```

  Source: `utils/useSwipeDismiss.ts:184-212,691-713`. Test: [L3] `useSwipeDismiss.test.tsx` "fires onProgress relative to the element size", "continues firing onProgress when swipe progress is clamped".

- **PSH-ALG-10** — MUST. The implementation reproduces the following procedure exactly for velocity sampling and release velocities.

```
recordSample(offset, t):                    // t invalid (≤0 / non-finite) → skip
  if last and t > last.t:
    stationary ← |offset − last| < 1 on both axes
    skip ← stationary and not hadStationary; hadStationary ← stationary
    if skip: return
    dt ← max(t − last.t, 4);  velocity ← (offset − last) / dt
  last ← { offset, t }

onRelease(t):
  duration ← (start valid and t valid and t > start) ? t − start : 0
  vd ← duration > 0 ? max(duration, 50) : 0
  velocityX/Y ← vd > 0 ? Δ / vd : 0
  releaseV ← velocity
  if last and t valid and t ≥ last.t:
    age ← t − last.t
    if age ≤ 80: sdt ← max(age, 4); for each axis a: if |offset.a − last.a| ≥ 1: releaseV.a ← (offset.a − last.a) / sdt
    else: releaseV ← (0, 0)
```

  Source: `utils/useSwipeDismiss.ts:249-276,748-778`. Test: [L3] `useSwipeDismiss.test.tsx` "handles a stationary move at $trailingTime and release at $releaseTime", "measures release velocity from the latest movement ($name)", "clamps short swipe durations when computing velocity".

- **PSH-ALG-11** — MUST. The implementation reproduces the following procedure exactly for the release decision.

```
decision ← onRelease?.({ event, direction: intended, Δ, velocity, releaseV })
if decision is boolean: close ← decision; dir ← intended ?? primaryDirection
else if cancelled: snapBack; return
else: close ← false; for d in directions (given order): if disp(d, Δ) > threshold: close ← true; dir ← d; break
if close and dir: swipeDirection ← dir; dragDismissed ← true; restore styles; onDismiss(event, { direction: dir })
else: snapBack   // offset ← initial, swipeDirection ← undefined, restore styles, progress 0 with release Δ
```

  Source: `utils/useSwipeDismiss.ts:780-826`. Test: [L3] `useSwipeDismiss.test.tsx` "allows onRelease to override dismissal", "respects custom swipeThreshold".

- **PSH-ALG-12** — MUST. The implementation reproduces the following procedure exactly for starting a pending touch gesture from a scroll edge.

```
onPendingTouchMove(pos):
  target ← elementAt(pos) ?? event target
  scroller ← scrollableTouchTarget(target, body, axis) excluding body/documentElement
  if scroller and (element contains scroller or scroller contains element):
    Δ ← pos − pendingStartPos
    edgeOk ← vertical axis and Δ.y ≠ 0 ? (Δ.y > 0 and scrollTop ≤ 0 and down allowed) or (Δ.y < 0 and scrollTop ≥ max(0, scrollHeight − clientHeight) and up allowed)
           : horizontal axis and Δ.x ≠ 0 ? (Δ.x > 0 and scrollLeft ≤ 0 and right allowed) or (Δ.x < 0 and scrollLeft ≥ max(0, scrollWidth − clientWidth) and left allowed)
           : null
    if edgeOk == false: return                                  // let the scroller scroll
    if edgeOk == true: ignoreScrollable ← true
  if start(pos, ignoreScrollable):
    if pendingStartPos and ignoreScrollable: origin ← base ← lastMovePos ← pendingStartPos; firstMove ← false   // keep flick distance
    clear pending
```

  Source: `utils/useSwipeDismiss.ts:484-522,867-939`. Test: [L3] `useSwipeDismiss.test.tsx` "starts a touch swipe when the page scroller is `body`", "keeps gating on a scrollable descendant of the other axis when `body` scrolls the page".

- **PSH-ALG-13** — MUST. The implementation reproduces the following procedure exactly for the viewport activation direction.

```
relative(from, to): centers of bounding rects; return { h: to.cx − from.cx, v: to.cy − from.cy }
label(v, tol=5, pos, neg) = v > tol ? pos : v < −tol ? neg : ''
activationDirection(offset) = offset ? label(offset.h, 5, 'right', 'left') + ' ' + label(offset.v, 5, 'down', 'up') : undefined
```

  Source: `utils/usePopupViewport.tsx:310-365`. Test: [L3] `usePopupViewport.test.tsx` "returns undefined without an offset", "returns [$expected] for $offset.horizontal, $offset.vertical", "$name" (calculateRelativePosition cases).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through viewportConformanceTests on Popover in `popover.spec.ts`, "sets `data-activation-direction` from the previous to the new trigger" (L4, both modes); `popup.test.ts` "port: a move between triggers while open clones the removed content into [data-previous] and reports the direction" (L3).

- **PSH-ALG-14** — MUST. The implementation reproduces the following procedure exactly for the viewport content key (post-commit, pre-paint when the active trigger id or payload changed).

```
if activeTriggerId ≠ prevId: counter += 1; pendingPayload ← (payload == prevPayload)
else if pendingPayload and payload ≠ prevPayload: counter += 1; pendingPayload ← false
prevId ← activeTriggerId; prevPayload ← payload
key = (activeTriggerId ?? 'current') + '-' + counter
```

  Source: `utils/usePopupViewport.tsx:370-399`. Test: [L4] `viewportConformanceTests.tsx` "remounts the `current` container when the active trigger changes", "cleans up the transition when a lagging payload remounts the current container".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through viewportConformanceTests on Popover in `popover.spec.ts`, "remounts the `current` container when the active trigger changes" and "shows the latest content after rapid trigger changes" (L4, both modes); "port: a C# re-render keeps [data-previous] before [data-current], and a dispose removes both" (L4).

- **PSH-ALG-15** — MUST. The implementation reproduces the following procedure exactly for auto-resize (post-commit, pre-paint when `mounted`, the popup/positioner elements, the content key, side or direction change).

```
if not mounted: committed ← null; return
if no popup or no positioner: return
apply anchoring styles (PSH-DOM-12); set popup --popup-width/height ← auto
override popup position:static, transform:none, scale:1; positioner --available-width/height ← max-content
onMeasureLayout()                                   // viewport: current container animation/transition ← none, previous display ← none
if committed == null:                               // first measurement of this open
  positioner --positioner-width/height ← max-content; dims ← cssDimensions(popup); committed ← dims
  positioner vars ← dims px; restore overrides; onMeasureLayoutComplete(null, dims); return (cleanup: remove anchoring styles)
positioner vars ← max-content; prev ← committed; new ← cssDimensions(popup); committed ← new
popup vars ← prev px; restore overrides; onMeasureLayoutComplete(prev, new); positioner vars ← new px
next frame: popup vars ← new px; on animations finished (popup, subtree): popup vars ← auto
cleanup: abort pending watcher, cancel frame, remove anchoring styles
```

  Source: `utils/usePopupAutoResize.ts:43-136`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions" (previous container `--popup-*` set).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through `popup.test.ts` "auto-resize" ("port: the first measurement sizes the positioner, leaves the popup at auto, and restores every override", "port: a content change animates from the previous size to the new one, then returns to auto", L3); viewportConformanceTests on Popover, "anchors the popup for side=$side in $direction mode" (L4, both modes).
  Port note 2026-10-29 (M4 track 6, gap 19's residual, settled by the orchestrator 2026-10-29 as option (i')): a side change that lands while a viewport morph runs (a `[data-previous]` container exists) re-applies the anchoring styles at once, as a plain style write, and defers the measurement to the morph's cleanup. The flip lands after the morph starts on both modes: Server 1-2 ms unthrottled (about 15 ms at 4x CPU), as the Positioner's update comes a message after the content batch, and WebAssembly 0-1 ms. The measurement's overrides on the containers (`onMeasureLayout`) would cancel the morph's transitions. The measurement does not change with the side, so nothing visible differs from Base UI, which resolves the side in the switching commit. L3 `popup.test.ts` "port: a side change during a morph re-anchors at once and re-measures at the morph's cleanup"; L4 viewportConformanceTests "cleans up the transition when a lagging payload remounts the current container" on Tooltip's fixture, whose default side flips between the suite's triggers.

- **PSH-ALG-16** — MUST. The implementation reproduces the following procedure exactly for inline-rect line grouping and reference-rect selection.

```
lineRects(rects):                                   // rects sorted by top
  for r in rects: union ← union ∪ r
    if no previous or r.top − previous.top > previous.height / 2: lines.push(copy r)
    else: extend last line (left=min, right=max, bottom=max)
    previous ← r
  return { lines, fallback: union }
lineIndexAt(lines, x, y) = index of first line with x ∈ (left−2, right+2) and y ∈ (top−2, bottom+2), else −1
coordsFor(el, x, y): lines ← lineRects(el.getClientRects()); if lines < 2: undefined else { x, y, lineIndex: lineIndexAt(...) or undefined, element: el }
referenceRect(ref, placement, coords):
  lines, fallback ← lineRects(ref.getClientRects()); if lines < 2: return null
  if coords.lineIndex valid: return lines[coords.lineIndex]
  if coords.x,y: i ← lineIndexAt(lines, x, y); if i ≥ 0: return lines[i]
  if lines == 2 and lines[0].left > lines[1].right and coords.x,y: return fallback
  side ← placement[0]
  if side ∈ {t, b}: target ← side == t ? first : last; return rect(target.left, first.top, target.right, last.bottom)
  // left/right: pick the line(s) with the extreme edge on that side; span all lines horizontally
  edge ← side == l ? +∞ : −∞; first ← last ← lines[0]; left ← lines[0].left; right ← lines[0].right
  for r in lines: left ← min; right ← max; e ← side == l ? r.left : r.right
    if (l and e < edge) or (r and e > edge): edge ← e; first ← last ← r
    else if e == edge: last ← r
  return rect(left, first.top, right, last.bottom)
middleware(state): if reference lacks getClientRects: {}; coords valid only if coords.element is the reference or its contextElement
  rect ← referenceRect(...); if null or no platform.getElementRects: {}
  rects ← platform.getElementRects({ reference: virtual(rect, contextElement), floating, strategy })
  if rects.reference equals state.rects.reference (x,y,w,h): {} else { reset: { rects } }
```

  Source: `utils/popups/inlineRect.ts:62-193,237-292`. Test: [L3] `inlineRect.test.ts` "creates inline middleware rects from the hovered line", "reuses the captured line index if client coordinates become stale", "uses the edge-aligned rect for right placements", "uses the edge-aligned rect for left placements", "uses the fallback rect for disjoint two-line rects when coords miss", "does not create inline rects when the reference does not wrap", "does not run when the reference has no client rects", "does not reset when the inline rect matches the current reference rect", "ignores stored coords from a different element", "accepts stored coords from a virtual reference context element", "converts client-space inline rects through the positioning platform".

- **PSH-ALG-17** — MUST. The implementation reproduces the following procedure exactly for the anchored scroll-lock decision.

```
post-commit pre-paint when (enabled, touchOpen, positioner) change:
  touchLock ← enabled and touchOpen and positioner ≠ null and viewportWidth > 0 and popupWidth > 0 and popupWidth ≥ viewportWidth − 20
    where viewportWidth = ownerDocument(positioner).documentElement.clientWidth, popupWidth = positioner.offsetWidth
lockScroll(enabled and (not touchOpen or touchLock), referenceElement)
```

  Source: `utils/useAnchoredPopupScrollLock.ts:11-43`. Test: none.

- **PSH-ALG-18** — MUST. The implementation reproduces the following procedure exactly for the trigger focus guards.

```
closeAndFocus(event, direction, positioner):
  guard ← event.currentTarget
  flush synchronously: store.setOpen(false, changeEventDetails('focus-out', event, guard))
  anchor ← guard.isConnected ? guard : trigger
  tabbableNear(anchor, direction, exclude = positioner)?.focus()
onBeforeGuardFocus(e): closeAndFocus(e, −1, store.positionerElement)
onAfterGuardFocus(e):
  p ← store.positionerElement
  if p and (e.relatedTarget is null or not inside p): beforeContentGuard.focus()   // entering the popup
  else: closeAndFocus(e, +1, p)
```

  Source: `utils/popups/useTriggerFocusGuards.ts:38-71`. Test: [L4] `useTriggerFocusGuards.test.tsx` (all cases).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through `popup.test.ts` "the trigger guards" (L3); `popover.spec.ts` "does not loop focus between guards when tabbing out of the popup", "does not loop focus between guards when shift-tabbing out of the trigger" and "renders focus guards around the active detached trigger and reports it on close" (L4, both modes).

- **PSH-ALG-19** — MUST. The implementation reproduces the following procedure exactly for the open interaction type (trigger-level variant).

```
onPointerDown(e): if not e.defaultPrevented: last ← e.pointerType; handle(e, e.pointerType)
onClick(e): type ← e.detail == 0 ? 'keyboard' : ('pointerType' in e ? e.pointerType : last); last ← ''; handle(e, type)
handle(_, type): if not isOpen(): openMethod ← type || (isIOS ? 'touch' : '')
```

  Source: `utils/useOpenInteractionType.ts:9-38`, `packages/utils/src/useEnhancedClickHandler.ts:14-48`. Test: [L4] `popupFocusPropsTests.tsx` "passes the interaction type to a function, and does nothing when it returns undefined".

- **PSH-ALG-20** — MUST. The implementation reproduces the following procedure exactly for the mixed mousedown/click toggle guard.

```
onMouseDown(e): if (action == 'open' and not open) or (action == 'close' and open):
                  ignoreClick ← true; ownerDocument(e.currentTarget).addEventListener('click', () → ignoreClick ← false, { once: true })
onClick(e): if ignoreClick: ignoreClick ← false; e.preventBaseUIHandler()
```

  Source: `utils/useMixedToggleClickHandler.ts:16-42`. Test: none.

- **PSH-ALG-21** — MUST. The implementation reproduces the following procedure exactly for the internal backdrop cutout.

```
r ← cutout.getBoundingClientRect()
clip-path ← polygon(0% 0%, 100% 0%, 100% 100%, 0% 100%, 0% 0%, r.left r.top, r.left r.bottom, r.right r.bottom, r.right r.top, r.left r.top)   // px units
```

  Source: `utils/InternalBackdrop.tsx:13-16`. Test: none.

- **PSH-ALG-22** — MUST. The implementation reproduces the following procedure exactly for the popup-id and trigger-ownership selectors.

```
open(s) = s.openProp ?? s.open;  activeId(s) = s.triggerIdProp ?? s.activeTriggerId
popupId(s) = (s.popupElement?.id ?? s.floatingId) || undefined
ownsOpen(s, id) = id defined and open(s) and activeId(s) == id
ownsOrLone(s, id) = ownsOpen(s, id) or (id defined and open(s) and activeId(s) == null and not s.openedWithoutTrigger and s.triggerCount == 1)
isTriggerActive(s, id) = id defined and activeId(s) == id
isMountedByTrigger(s, id) = isTriggerActive(s, id) and s.mounted
triggerPopupId(s, id) = ownsOrLone(s, id) ? popupId(s) : undefined
activeTriggerElement(s) = s.mounted ? s.activeTriggerElement : null
transitionStatus(s) = open(s) and not s.mounted ? 'starting' : s.transitionStatus
```

  Source: `utils/popups/store.ts:148-218`. Test: [L1] `store.test.ts` (all), `popupStoreUtils.test.tsx` "popupId selector" (all).

## 6. Configuration surface used by components

### 6.1 Options

| Mechanism | Option | Type | Default | Meaning |
|---|---|---|---|---|
| Root store creation (`usePopupRootStore`) | `treatPopupAsFloatingElement` | `boolean` | `false` | Pass the Popup element (not the Positioner) to FLT as the floating element. Dialog, AlertDialog, Drawer: `true`. |
| Active-trigger reconciliation (`useImplicitActiveTrigger`) | `closeOnActiveTriggerUnmount` | `boolean` | `false` | PSH-STA-23. Tooltip, PreviewCard: `true`. |
| Open-state transitions (`useOpenStateTransitions`) | `onUnmount` | `() => void` | — | Store-specific reset run inside PSH-STA-45 (Popover: `stickIfOpen ← true`, clear `openChangeReason`; Menu: `allowMouseEnter ← false`; PreviewCard: clear inline-rect coordinates). |
| | `animateInitialOpen` | `boolean` | `false` | PSH-ANI-02. Menu passes its submenu flag. |
| Trigger data forwarding (`useTriggerDataForwarding`) | `stateUpdates` | partial store state | — | Fields copied when the trigger is active: Dialog family `{ payload }`; Popover `{ payload, disabled, openOnHover, closeDelay }`; Tooltip `{ payload, closeOnClick, closeDelay }`; PreviewCard `{ payload, closeDelay }`; Menu `{ payload, closeDelay, parent, floatingTreeRoot, floatingNodeId, floatingParentNodeId, keyboardEventRelay }`. |
| Interaction prop bags (`usePopupInteractionProps`) | `activeTriggerProps`, `inactiveTriggerProps`, `popupProps` (+ store-specific extras) | attribute bags | — | Dialog/Popover: FLT dismiss `reference`/`trigger` and `floating` bags; Menu/Tooltip/PreviewCard: their click/hover/dismiss bags. All three keys are mandatory. |
| Handle (`BasePopupHandle`) | `componentName` | `string` | — | Prefix in diagnostics: `Dialog` (also AlertDialog, Drawer), `Menu`, `Popover`, `PreviewCard`, `Tooltip`. |
| | `throwOnMissingTrigger` | `boolean` | `true` | PSH-EDGE-03/04. Dialog family: `false`. |
| Internal backdrop | `cutout` | `Element \| null` | — | PSH-DOM-14. Popover/Select/Menu: trigger; Combobox: input group ?? input ?? trigger; Dialog: none. |
| | `inert` | `boolean` | — | Consumers pass `!open`. |
| Lite portal | `container` | `HTMLElement \| ShadowRoot \| DOMHandle \| null` | `<body>` | Portal target (FLT `useFloatingPortalNode`). |
| Open interaction type | `open` | `boolean \| () => boolean` | — | Current open state read at click time (PSH-PTR-02). |
| Mixed toggle guard | `enabled` | `boolean` | `true` | Menu.Trigger: `parent.type === 'menubar'`. |
| | `mouseDownAction` | `'open' \| 'close'` | — | Menu.Trigger: `'open'`. |
| Anchored scroll lock | `enabled` | `boolean` | — | Popover: `open && modal === true && reason !== 'trigger-hover'`; Menu: `open && (menubarModal \|\| (modal && reason !== 'trigger-hover'))`; Select: `(alignItemWithTrigger \|\| modal) && open`; Combobox: `open && modal`. |
| | `touchOpen` | `boolean` | — | `openMethod === 'touch'` in all consumers. |
| Viewport (`usePopupViewport`) | `side` | FLT `Side` | — | From the positioner (PSH-DOM-12). |
| Swipe tracker (`useSwipeDismiss`) | `enabled` | `boolean` | — | Drawer.Viewport: `mounted && !nestedDrawerOpen`; Drawer.SwipeArea: its `enabled`. |
| | `directions` | `SwipeDirection[]` | — | Single-axis list. Drawer.Viewport: derived from `swipeDirection`; SwipeArea: `[resolvedSwipeDirection]`. |
| | `elementRef` | DOM handle | — | Tracked element (Drawer popup / swipe area). |
| | `movementCssVars` | `{ x, y }` | — | PSH-DOM-18. |
| | `swipeThreshold` | `number \| ({ element, direction }) => number` | `40` | PSH-PTR-12. Drawer.Viewport passes a function. |
| | `canStart` | predicate | — | PSH-PTR-10. Drawer.Viewport gates on the touch/popup state. |
| | `ignoreScrollableAncestors` | `boolean` | `false` | Drawer.Viewport: `true`. |
| | `ignoreSelectorWhenTouch` | `boolean` | `true` | Drawer.Viewport: `false` (touch may start on buttons/links). |
| | `trackDrag` | `boolean` | `true` | Drawer.SwipeArea: `false` (PSH-PTR-13, PSH-DOM-17 not applied). |
| | callbacks | see PSH-EVT-09 | — | |
| Close-part registry | — | — | — | Popover.Popup provides; Popover.Close registers. |

### 6.2 Used by

| Component doc | Store / handle | Root plumbing | Trigger plumbing | Presentational helpers | State mappings |
|---|---|---|---|---|---|
| dialog (DLG), alert-dialog (ADL), drawer (DRW) | `DialogStore` (own `setOpen`), `DialogHandle` (`open(null)`, `openWithPayload`, no throw on missing trigger) | root store (`treatPopupAsFloatingElement`), reconciliation (default), open-state transitions, root sync, interaction props | data forwarding `{ payload }`, open interaction type (trigger variant), click-trigger marker; Drawer.SwipeArea: bare registration | internal backdrop (Portal, `modal === true`), default initial focus, focusable-popup attributes; Drawer: swipe tracker (Viewport + SwipeArea) | trigger mapping; popup + transition mapping (Popup, Backdrop, Viewport) |
| popover (POP) | `PopoverStore` (own `setOpen`, `hasTriggerFocusGuards`, `isPressOpenedByTrigger`), `PopoverHandle` | same as Dialog but positioner is the floating element | data forwarding `{ payload, disabled, openOnHover, closeDelay }`, open interaction type, trigger focus guards, click-trigger marker | internal backdrop (Positioner, true-modal non-hover, cutout trigger), anchored scroll lock, close-part registry, default initial focus, viewport, focusable-popup attributes | pressable/plain trigger mapping (per reason); popup + transition mapping |
| menu (MNU), context-menu (CTX), menubar (MBR) | `MenuStore`, `MenuHandle` (top-level menus only) | reconciliation (default), open-state transitions (`animateInitialOpen` for submenus), interaction props, open interaction type (Root variant) | data forwarding (menu fields), trigger focus guards (not in Menubar), mixed toggle guard (Menubar); SubmenuTrigger: bare registration | internal backdrop (Positioner), anchored scroll lock, viewport, focusable-popup attributes | pressable trigger mapping; popup + transition mapping |
| tooltip (TIP) | `TooltipStore` (shared sequence), `TooltipHandle` | root store, reconciliation (`closeOnActiveTriggerUnmount`), open-state transitions, interaction props | data forwarding `{ payload, closeOnClick, closeDelay }` | lite portal, viewport, focusable-popup attributes | plain trigger mapping; popup + transition mapping |
| preview-card (PRV) | `PreviewCardStore` (shared sequence + inline-rect capture), `PreviewCardHandle` | as Tooltip | data forwarding `{ payload, closeDelay }`, inline-rect trigger tracking | lite portal, viewport, inline-rect middleware, focusable-popup attributes | plain trigger mapping; popup + transition mapping |
| select (SEL) | own store (not a popup store; uses `openMethod`) | open interaction type (Root variant) | — | internal backdrop (Positioner, cutout trigger), anchored scroll lock, focusable-popup attributes | pressable trigger mapping; plain mapping on Icon; popup + transition mapping |
| combobox (CMB), autocomplete (AUT) | own store (uses `openMethod`) | open interaction type (Root variant) | — | internal backdrop (Positioner, cutout input group/input/trigger), anchored scroll lock, focusable-popup attributes | pressable trigger mapping; plain mapping on Clear; popup mapping |
| navigation-menu (NAV) | own store | — | — | focus guards (Trigger, Viewport); `data-activation-direction` name reused by its own viewport | pressable trigger mapping; plain mapping on Icon; popup + transition mapping |
| toast (TST) | own store | — | — | lite portal (Portal), focus guards (Viewport); its own swipe implementation reuses the `data-swiping`/`data-swipe-direction`/`--toast-swipe-movement-*` naming but does **not** use the swipe tracker (`useSwipeDismiss`); it imports only the pure `getDisplacement(direction, dx, dy)` helper from that module (`toast/root/ToastRoot.tsx:23, 225, 372`), and its reverse-cancel rule has the same shape as PSH-PTR-15 (reference displacement set once when the direction is armed, 10 px, neither-axis-bidirectional condition) but its gesture differs elsewhere (1 px real-swipe minimum and axis locking before arming, fixed 40 px threshold, its own damping and ignore selector; TST) | — |

## 7. Edge cases and dev warnings

Diagnostic texts below are quoted verbatim once; component documents cite the ID and give only the substituted component name. Per conventions §3.1, warnings asserted by a test are MUST; untested dev-only warnings are SHOULD.

- **PSH-EDGE-01** — MUST. A Trigger rendered with neither an ancestor Root of its family nor a `handle` prop throws (in every environment) an error whose message is, per component: `Base UI: <Dialog.Trigger> must be used within <Dialog.Root> or provided with a handle.`; `Base UI: <Popover.Trigger> must be either used within a <Popover.Root> component or provided with a handle.`; `Base UI: <Menu.Trigger> must be either used within a <Menu.Root> component or provided with a handle.`; `Base UI: <Tooltip.Trigger> must be either used within a <Tooltip.Root> component or provided with a handle.`; `Base UI: <PreviewCard.Trigger> must be either used within a <PreviewCard.Root> component or provided with a handle.`
  Source: `dialog/trigger/DialogTrigger.tsx:41-45`, `popover/trigger/PopoverTrigger.tsx:52-56`, `menu/trigger/MenuTrigger.tsx:69-73`, `tooltip/trigger/TooltipTrigger.tsx:65-69`, `preview-card/trigger/PreviewCardTrigger.tsx:43-47`. Test: none.
- **PSH-EDGE-02** — MUST. `handle.open()`, `handle.close()` and `handle.openWithPayload()` called while no Root is attached are ignored (no state change, `isOpen` stays `false`) and, outside production, log one warning each: `Base UI: <Name>Handle.open() was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.`; `Base UI: <Name>Handle.close() was called while no root using this handle is mounted. The call was ignored.`; `Base UI: DialogHandle.openWithPayload() was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively.` — where `<Name>` is `Dialog`, `Menu`, `Popover`, `PreviewCard` or `Tooltip` (AlertDialog and Drawer report `Dialog`).
  Source: `utils/popups/popupHandle.ts:216-224,274-282`, `dialog/store/DialogHandle.ts:45-53`. Test: [L1] `popupHandle.test.ts` "ignores open() and close() with a warning before a root attaches and after it detaches", "$component handle is closed and names itself in warnings while no root is attached", "$component handle ignores openWithPayload() with a warning while detached"; `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 6, settled by the orchestrator 2026-10-27 under ADR 02 Amendments 2026-10-20 and the plan's §Settled "Diagnostics"): a handle logs through the logger of the Root it last attached to, behind `DevelopmentWarnings` (as PSH-EDGE-04 and PSH-EDGE-06 do); a warning raised before any Root ever attached has no logger and is dropped, where Base UI warns twice (L1 "ignores open() and close() with a warning before a root attaches and after it detaches"; the suite title at L2 and L4 asserts the port's count).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); settled by the orchestrator 2026-10-29, ADR 04 Amendments 2026-10-29): `DialogHandle`'s mode constructor, which `AlertDialogHandle` uses, passed the family name to the warnings, so an AlertDialog handle warned as `AlertDialogHandle`; it now passes `Dialog` in every mode, the mode choosing only the fallback store. Test: [L1] `AlertDialogApiTests` "AlertDialog handle is closed and names itself in warnings while no root is attached", "AlertDialog handle ignores openWithPayload() with a warning while detached".
- **PSH-EDGE-03** — MUST. For anchored popups (Menu, Popover, PreviewCard, Tooltip), `handle.open(id)` with an id that no registered trigger matches throws (in every environment) `Base UI: <Name>Handle.open() was called with the trigger id "<id>", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted <Name>.Trigger that has this handle set on its "handle" prop.` and does not open.
  Source: `utils/popups/popupHandle.ts:241-249`. Test: [L1] `popupHandle.test.ts` "throws for an unregistered trigger id by default and does not open"; `detachedTriggersConformanceTests.tsx` "throws when opened with an unregistered trigger id" (L4 for the `detachedTriggersConformanceTests.tsx` citation).
- **PSH-EDGE-04** — MUST. For Dialog, AlertDialog and Drawer, `handle.open(id)` with an unmatched id opens the popup *unassociated* (no active trigger, `openedWithoutTrigger`) and, outside production, warns once: `Base UI: DialogHandle.open: No trigger found with id "<id>". The popup will open, but the trigger will not be associated with it.`
  Source: `utils/popups/popupHandle.ts:251-262`, `dialog/store/DialogHandle.ts:20`. Test: [L1] `popupHandle.test.ts` "opens unassociated with a warning for an unregistered trigger id when throwing is disabled"; `detachedTriggersConformanceTests.tsx` "opens unassociated with a warning when opened with an unregistered trigger id" (L4 for the `detachedTriggersConformanceTests.tsx` citation).
- **PSH-EDGE-05** — MUST. `handle.open(id)` with an empty-string id behaves like `open(null)` (no lookup, no throw, no warning, trigger-less open).
  Source: `utils/popups/popupHandle.ts:234,241` (truthiness checks). Test: none.
- **PSH-EDGE-06** — MUST. When more than one Root is attached to the same handle, the check is deferred by one frame and, if more than one Root is still attached then, a warning is logged outside production: `Base UI: A handle is attached to more than one mounted root at the same time. The most recently mounted root takes over and the previous one stops being controlled by the handle. A handle should be used by a single root that stays mounted for the lifetime of the handle.` A transient overlap that resolves within the frame (either Root detaching first) MUST NOT warn.
  Source: `utils/popups/popupHandle.ts:153-173`. Test: [L2] `popupHandle.test.ts` "warns when more than one root is still attached a frame later", "does not warn when $name before the next frame", "does not warn for a single attached root".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the "one frame later" check runs at the next render-batch boundary (ADR 01's row "PSH-STA-23, FLD-FRM-17", which PSH-EDGE-06 joins; ADR 01 Amendments 2026-10-27), and the warning goes through the last attached Root's logger behind `DevelopmentWarnings` (design point 6). The three `popupHandle.test.ts` titles are restated at that boundary at L2 (`PopupStorePortTests`): "port: warns when more than one root is still attached at the next render-batch boundary", "port: does not warn when $name before the next render-batch boundary" (the older and the newer root), "port: does not warn for a single attached root".
- **PSH-EDGE-07** — MUST. Outside production, registering an element in a registry under a second, different id throws `Base UI: A trigger element cannot be registered under multiple IDs in PopupTriggerMap.`; re-registering it under the *same* id, registering it again after it was deleted, or after it was evicted by id reuse, is allowed. In production no check is made and the element is registered under both ids.
  Source: `utils/popups/popupTriggerMap.ts:36-60,65-74`. Test: [L1] `popupTriggerMap.test.ts` "throws in non-production when the same element is registered under multiple ids", "allows re-registering an element under a new id after it was deleted", "keeps an unrelated element claim when another id is deleted", "allows an element evicted by id reuse to register under a new id", "still throws when a re-added element is registered under a second id", "does not throw in production when the same element is registered under multiple ids".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 6, settled by the orchestrator 2026-10-27 under ADR 02 Amendments 2026-10-20 and the plan's §Settled "Diagnostics"): the duplicate-id check throws in every build under the always-on misuse rule, stricter than Base UI's "outside production" (L1 "port: throws in every build when the same element is registered under multiple ids, and leaves the first registration").
- **PSH-EDGE-08** — MUST. Replacing the handle object on both the Root and its detached Triggers with a freshly created one (hot-reload style), with or without simultaneously reparenting the Triggers, keeps the triggers working: the next activation opens the popup with that trigger active and closing works.
  Source: `utils/popups/popupStoreUtils.ts:139-178,377-380`, `utils/popups/popupHandle.ts:149-185`. Test: [L4] `detachedTriggersConformanceTests.tsx` "keeps a detached trigger working $name" (four cases). Docs: `CHANGELOG.md` v1.2.0 "Fix detached trigger HMR with recreated handles"; v1.0.0 "Fix detached trigger remounting".
- **PSH-EDGE-09** — MUST. During a transient overlap (a new Root attaches while the old one, holding the trigger registrations, is still mounted) an imperative `open(id)` in that same commit resolves the trigger from the older Root's registry (PSH-STA-38) and opens the new Root with that trigger active; completing the handoff keeps the popup open and associated.
  Source: `utils/popups/popupHandle.ts:226-239`. Test: [L4] `detachedTriggersConformanceTests.tsx` "resolves a trigger still registered to the previous root during a transient overlap". Docs: `CHANGELOG.md` v1.7.0 "Fix detached trigger store migration".
- **PSH-EDGE-10** — MUST. Setting or releasing pointer capture in the swipe tracker swallows a `NotFoundError` (pointer already gone) and does nothing when the element lacks the capture methods; any other error propagates.
  Source: `utils/useSwipeDismiss.ts:68-86`. Test: none.
- **PSH-EDGE-11** — MUST. Swipe event timestamps that are non-finite or ≤ 0 are ignored for velocity purposes (no sample recorded; duration 0 → velocity 0).
  Source: `utils/useSwipeDismiss.ts:56-58,394,595,690,751`. Test: none.
- **PSH-EDGE-12** — MUST. A touch start/move/end without a primary touch point (empty `touches`) is ignored by the swipe tracker.
  Source: `utils/useSwipeDismiss.ts:308-317,537-540,830-833`. Test: none.
- **PSH-EDGE-13** — MUST. The page scroller (`<body>` or `<html>` made scrollable by a CSS reset) never gates a swipe start or move; only other scrollable touch targets do.
  Source: `utils/useSwipeDismiss.ts:335-351`. Test: [L3] `useSwipeDismiss.test.tsx` "starts a touch swipe when the page scroller is `body`".
- **PSH-EDGE-14** — MUST. Detaching a Root store and immediately attaching the same store again (an attach/detach/attach replay of one mount) leaves the handle exposing that store, and imperative calls reach it; store-pointer subscribers are notified on both steps, because the exposed store passes through the fallback in between (PSH-STA-36).
  Source: `utils/popups/popupHandle.ts:149-152,175-198`. Test: [L1] `popupHandle.test.ts` "stays attached when the same root store detaches and reattaches (Strict Mode replay)".

## 8. React-specific notes and porting considerations

Non-normative.

* **Store identity vs. handle identity.** The store belongs to the Root instance (`useRefWithInit`), not to the handle; the handle only *points* at it. A port should likewise make the handle a thin pointer holder so hot-reload-style handle replacement (PSH-EDGE-08) and Strict-Mode double-attach (PSH-STA-35) are harmless. Port: see decisions/05-state-store-and-event-details.md §1.
* **Detached triggers subscribe to the handle pointer** with `useSyncExternalStore` (`usePopupHandleStore`), with `serverStore` as the hydration snapshot so server and client render the same closed trigger. A port with a different reactivity model needs an equivalent "store pointer changed → re-read and migrate registration" signal (PSH-STA-36). Port: see decisions/05-state-store-and-event-details.md §1.
* **Layout-effect ordering is load-bearing.** `PopupHandleAttachment` is rendered as the Root's first child so its layout effect runs before descendants' (PSH-STR-04); trigger registration also runs in layout effects so a trigger exists in the registry before the Root's reconciliation (PSH-ALG-03) runs in the same commit. The conformance test "resolves a trigger still registered to the previous root during a transient overlap" depends on both. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4). *Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)):* the handle attaches at the Root's first `OnAfterRender`, which Blazor calls parent first (PSH-STR-04), and a trigger registers at its parameter set, so every registration of a batch precedes the reconciliation at the batch boundary (design point 8, ADR 16 Amendments 2026-10-27); the overlap title is green at L2 and L4.
* **Stable callbacks and ref merging.** `useTriggerRegistration` returns a *stable* callback ref; migration on id/store change is therefore done by an explicit effect (`[registerTrigger, store, triggerId]`) rather than by React re-invoking the ref. React 17's `useId` fallback (id `undefined` on first render) is why registration tolerates an undefined id (PSH-ALG-04). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5) (popup and trigger ids drawn from the scoped counter at first render; the `floatingId` bare-id exception is collapsed to the `base-ui-` form).
* **Keyed fragments around the trigger element** (`<Fragment key={id}>`) keep the same DOM node when focus guards toggle (PSH-STR-10); a port that conditionally wraps the trigger must likewise avoid recreating it.
* **`flushSync`** is used for hover-reason commits (PSH-ANI-03) and for the focus-guard close (PSH-KEY-07) so DOM/animation state is observable synchronously. In a framework with synchronous rendering these are no-ops. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (PSH-ANI-03, PSH-KEY-07).
* **`NullStore`** (UTL) is a frozen-state store whose mutators are overridden to no-ops; component null stores add `setOpen: NOOP`. A port can model the inert fallback as a read-only snapshot plus a mutable registry. Port: see decisions/05-state-store-and-event-details.md §1.
* **Viewport snapshots are DOM clones, not component trees**: previous content is captured with `cloneNode(true)` because the real children may be stateful. Ports must clone rendered output, not re-render the previous view model. Port: see decisions/04-js-interop-boundary.md §1 (`dom` module) and §2.
* **Microtask deferral** of the close-on-unmount (PSH-STA-23) exists so a same-tick replacement trigger with the same id can register first; `queueMicrotask` semantics should be preserved. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 3, constraint C2); the deferral is C# store work and PSH-STA-23 is not in the Relaxation table (§10 Q11). *Stale since 2026-10-18; port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)):* the deferral maps to the render-batch boundary (ADR 01 decision 3's amendment) and PSH-STA-23 is in the relaxation table; §10 item 11 is decided. As built, the close is requested at the boundary that detects the loss (PSH-STA-23's note).
* **Dev-only reverse index** in `PopupTriggerMap` (`WeakMap` keyed by map and element) exists purely to make the duplicate-id check O(1); the production map is a plain ordered `Map<string, Element>`.
* **`process.env.NODE_ENV` gating**: PSH-EDGE-02/04/06/07 warnings and the registry duplicate check are compiled out in production builds; PSH-EDGE-01 and PSH-EDGE-03 throw unconditionally.
* **`preventUnmountOnClose` on event details** (PSH-EVT-05) is attached to the details object by the shared sequence. Port: see decisions/05-state-store-and-event-details.md §3 (`OpenChangeEventDetails.PreventUnmountOnClose`).
* **Browser-side loops.** The swipe tracker (§4.7.5), focus guards, the lite portal, and the auto-resize/viewport measurements and observers need browser APIs (pointer capture and frame-rate style writes, DOM moves, focus events, measurement, `ResizeObserver`); the popup store, registry and handles stay in the host language. Port: see decisions/04-js-interop-boundary.md §1 (`swipe`, `portal`, `focus-manager`, `dom` modules).
* **Lite portal** (PSH-STR-12) renders through `createPortal`. Port: see decisions/04-js-interop-boundary.md §5 (portal by DOM move; focus guards).
* **Shared suites** (popup, detached-triggers, viewport, popup-focus-props) run under vitest in jsdom and Chromium. Port: see decisions/06-testing-harness.md §1 (L4 Playwright per render mode).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| PSH-STR-01 | MUST | Each mounted Root part owns exactly one popup store, created once when the Root mounts and kept for the Root's whole mounted li… | 4.1 |
| PSH-STR-02 | MUST | A Trigger part resolves its store as: the store currently exposed by its `handle` prop when one is given; otherwise the store p… | 4.1 |
| PSH-STR-03 | MUST | A Root with a `handle` prop attaches its store to that handle for as long as the Root stays mounted with that handle, and detac… | 4.1 |
| PSH-STR-04 | MUST | The attachment is established post-commit, pre-paint of the Root's own mount, before any descendant's post-commit work, so an i… | 4.1 |
| PSH-STR-05 | MUST | A detached trigger MAY be rendered before or after its Root in document order, and inside or outside it; registration and activ… | 4.1 |
| PSH-STR-06 | MUST | Any number of Trigger parts (contained, detached, or a mix) MAY target one popup; at most one of them is the active trigger at … | 4.1 |
| PSH-STR-07 | MUST | A Viewport part renders its children inside a `<div data-current>` child; while a content transition is in progress it addition… | 4.1 |
| PSH-STR-08 | MUST | A focus guard is a `<span>` element. | 4.1 |
| PSH-STR-09 | MUST | The internal backdrop is a `<div>` element rendered as a sibling immediately before the positioner/popup subtree of the part th… | 4.1 |
| PSH-STR-10 | MUST | When a trigger's focus guards are enabled (PSH-FOC-07), one focus guard is rendered immediately before and one immediately afte… | 4.1 |
| PSH-STR-11 | MUST | The lite portal renders its children into a portal node appended to the resolved container (default: `<body>`), and renders not… | 4.1 |
| PSH-STR-12 | MUST | The lite portal renders its portal node element through the shared rendering pipeline: `className`, `style` and `rend… | 4.1 |
| PSH-API-01 | MUST | `X.createHandle()` returns a new handle on every call; handles are independent (separate fallback store, attachment stack and l… | 4.2 |
| PSH-API-02 | MUST | `handle.open(triggerId)` accepts a trigger id string for Menu, Popover, PreviewCard and Tooltip; for Dialog, AlertDialog and Dr… | 4.2 |
| PSH-API-03 | MUST | `handle.openWithPayload(payload)` exists only on Dialog, AlertDialog and Drawer handles. | 4.2 |
| PSH-API-04 | MUST | `handle.close()` exists on every handle and takes no arguments. | 4.2 |
| PSH-API-05 | MUST | `handle.isOpen` is a read-only getter returning the effective open state of the attached Root's store, or `false` while no Root… | 4.2 |
| PSH-API-06 | MUST | Root `defaultTriggerId` defaults to `null`; Root `triggerId` defaults to `undefined` (uncontrolled); Root and Trigger `handle` … | 4.2 |
| PSH-API-07 | MUST | When Root `children` is a function it is called with one argument `{ payload }`, where `payload` is the store's current payload… | 4.2 |
| PSH-API-08 | MUST | A Trigger's `id` prop, when given, is both its rendered `id` attribute and its trigger id; when absent a stable generated id is… | 4.2 |
| PSH-API-09 | MUST | `actionsRef.current.close()` closes the popup with reason `imperative-action` and no trigger; `actionsRef.current.unmount()` fo… | 4.2 |
| PSH-STA-01 | MUST | A freshly created popup store has exactly the initial values in the table above, except that a Root initializes `open` from `de… | 4.3 |
| PSH-STA-02 | MUST | The effective open state is `openProp` when it is not `undefined`, otherwise `open`. | 4.3 |
| PSH-STA-03 | MUST | The effective active trigger id is `triggerIdProp` when it is not `undefined` (a controlled `null` means "no active trigger"), … | 4.3 |
| PSH-STA-04 | MUST | The popup id is the popup element's `id` attribute when the popup element is known and its id is non-empty; otherwise `floating… | 4.3 |
| PSH-STA-05 | MUST | The derived transition status follows [TRN-STA-14](transitions-and-mounting.md): `'starting'` whenever the *effective* open sta… | 4.3 |
| PSH-STA-06 | MUST | The derived active trigger element is `null` while `mounted` is `false`, otherwise the stored element. | 4.3 |
| PSH-STA-07 | MUST | `isTriggerActive(id)` is `true` iff `id` is defined and equals the effective active trigger id (regardless of open state). | 4.3 |
| PSH-STA-08 | MUST | `isOpenedByTrigger(id)` is `true` iff `isTriggerActive(id)` and the effective open state is `true`. | 4.3 |
| PSH-STA-09 | MUST | `isMountedByTrigger(id)` is `true` iff `isTriggerActive(id)` and `mounted` is `true`. | 4.3 |
| PSH-STA-10 | MUST | `triggerPopupId(id)` (the value a trigger uses for `aria-controls`) is the popup id when either `isOpenedByTrigger(id)`, or (`i… | 4.3 |
| PSH-STA-11 | MUST | `triggerProps(isActive)` returns `activeTriggerProps` when `isActive` is `true`, otherwise `inactiveTriggerProps`; a Trigger pa… | 4.3 |
| PSH-STA-12 | MUST | When an open request (`nextOpen === true`) is applied, `preventUnmountingOnClose` becomes `false`; when a close request is appl… | 4.3 |
| PSH-STA-13 | MUST | When an open-state change is applied with a trigger element (`eventDetails.trigger`), or when it is an open request, `activeTri… | 4.3 |
| PSH-STA-14 | MUST | `openedWithoutTrigger` becomes `true` when an open request carries no trigger and `false` when it carries one; a close request … | 4.3 |
| PSH-STA-15 | MUST | Opens that happen through the controlled `open` prop or `defaultOpen` do not pass through the open-change sequence and therefor… | 4.3 |
| PSH-STA-16 | MUST | Post-commit, pre-paint of any commit in which the effective open state is `false`, the Root resets `openedWithoutTrigger` to `f… | 4.3 |
| PSH-STA-17 | MUST | While the popup is effectively open, `triggerCount` equals the number of registered triggers and updates synchronously as trigg… | 4.3 |
| PSH-STA-18 | MUST | Lone-trigger claim: post-commit, pre-paint of any commit in which the popup is effectively open, no active trigger id is resolv… | 4.3 |
| PSH-STA-19 | MUST | The lone-trigger claim MUST NOT happen while `openedWithoutTrigger` is `true`, so a popup opened by `handle.open(null)` / `open… | 4.3 |
| PSH-STA-20 | MUST | If the effective active trigger id is not in the registry but the stored `activeTriggerElement` is registered under a different… | 4.3 |
| PSH-STA-21 | MUST | If the effective active trigger id is in the registry with an element different from the stored `activeTriggerElement`, the sto… | 4.3 |
| PSH-STA-22 | MUST | By default (`closeOnActiveTriggerUnmount` off), when the active trigger unregisters while the popup is open, ownership is prese… | 4.3 |
| PSH-STA-23 | MUST | With `closeOnActiveTriggerUnmount` on (Tooltip, PreviewCard), when a *resolved* active trigger (one that was seen registered un… | 4.3 |
| PSH-STA-24 | MUST | An active trigger id that has never been matched to a registered trigger is *pending* and MUST NOT request a close under PSH-ST… | 4.3 |
| PSH-STA-25 | MUST | After the PSH-STA-23 close request, if it was not canceled, `activeTriggerId` and `activeTriggerElement` are set to `null`; if … | 4.3 |
| PSH-STA-26 | MUST | A Trigger registers `(id, element)` in its store's registry when its element is attached and `id` is defined, and unregisters w… | 4.3 |
| PSH-STA-27 | MUST | Registering the same element under the same id in the same store again is a no-op. | 4.3 |
| PSH-STA-28 | MUST | When a Trigger registers and its id equals the effective active trigger id, the store's `activeTriggerElement` is updated to th… | 4.3 |
| PSH-STA-29 | MUST | When a Trigger registers while the popup is effectively open, no active trigger id is resolved and `openedWithoutTrigger` is `f… | 4.3 |
| PSH-STA-30 | MUST | While `isMountedByTrigger(ownId)` is `true`, any change to a Trigger's forwarded state values is written to the store post-comm… | 4.3 |
| PSH-STA-31 | MUST | Switching the active trigger while the popup stays open (hover/focus/click on another trigger) updates `activeTriggerId`, `acti… | 4.3 |
| PSH-STA-32 | MUST | The store does not clear `payload` when the popup closes or its subtree unmounts; the value persists until replaced by a trigge… | 4.3 |
| PSH-STA-33 | MUST | The registry maps ids to elements in insertion order; adding an id that already exists replaces its element (the old element is… | 4.3 |
| PSH-STA-34 | MUST | A handle exposes its inert fallback store while no Root is attached, and the most recently attached still-attached Root store o… | 4.3 |
| PSH-STA-35 | MUST | Attachments form a stack: detaching a Root that is not the most recent leaves the exposed store unchanged; detaching the most r… | 4.3 |
| PSH-STA-36 | MUST | Store-pointer listeners are notified synchronously on every change of the exposed store and only then; detached triggers re-rea… | 4.3 |
| PSH-STA-37 | MUST | The inert fallback store reports the component's initial closed state for every read, has a live (mutable) trigger registry, an… | 4.3 |
| PSH-STA-38 | MUST | `handle.open(id)` resolves the trigger element by searching the attached Root stores' registries from most recently attached to… | 4.3 |
| PSH-STA-39 | MUST | `handle.openWithPayload(p)` writes `payload = p` to the attached store and then requests an open with reason `imperative-action… | 4.3 |
| PSH-STA-40 | MUST | Each Root mount starts from fresh store state: a Root unmounted while open and remounted with the same handle is closed and has… | 4.3 |
| PSH-STA-41 | MUST | `openMethod` (the open interaction type) is reset to `null` post-commit, pre-paint whenever the effective open state becomes `f… | 4.3 |
| PSH-STA-42 | MUST | The three interaction prop bags are written by the Root's interactions while they are mounted and reset to empty objects when t… | 4.3 |
| PSH-STA-43 | MUST | The Root's interactions (and therefore the prop bags) are mounted whenever the popup is open, mounted, or the Root has a `handl… | 4.3 |
| PSH-STA-44 | MUST | `mounted` in the store is seeded synchronously at Root creation to the transition hook's initial mounted value, before any part… | 4.3 |
| PSH-STA-45 | MUST | When the popup subtree unmounts (automatically after the exit transition, or via `actionsRef.unmount()`), the store sets `activ… | 4.3 |
| PSH-STA-46 | MUST | The floating root state is kept in sync with the store post-commit, pre-paint: `open` ← effective open, `floatingId` ← stor… | 4.3 |
| PSH-DOM-01 | MUST | A Trigger using the plain trigger mapping renders `data-popup-open=""` iff its `open` state is `true`, where a Trigger's `open`… | 4.4 |
| PSH-DOM-02 | MUST | A Trigger using the pressable trigger mapping renders both `data-popup-open=""` and `data-pressed=""` iff its `open` state is `… | 4.4 |
| PSH-DOM-03 | MUST | A part using the popup mapping renders `data-open=""` when its `open` state is `true` and `data-closed=""` when it is `false`; … | 4.4 |
| PSH-DOM-04 | MUST | A part using the popup mapping renders `data-anchor-hidden=""` iff its `anchorHidden` state is `true`. | 4.4 |
| PSH-DOM-05 | MUST | `popupTransitionStateMapping` is the popup mapping plus the TRN transition-status mapping (`data-starting-style` / `data-ending… | 4.4 |
| PSH-DOM-06 | MUST | `data-side` and `data-align` on Popup/Positioner/Arrow carry the resolved placement; their value sets are those listed in the t… | 4.4 |
| PSH-DOM-07 | MUST | The Viewport renders `data-activation-direction` only while a direction string exists; its value is the horizontal token (`righ… | 4.4 |
| PSH-DOM-08 | MUST | The Viewport renders `data-transitioning=""` iff a `[data-previous]` container is currently rendered. | 4.4 |
| PSH-DOM-09 | MUST | The `[data-previous]` container: carries the `inert` attribute; has inline `position: absolute`; once the previous content size… | 4.4 |
| PSH-DOM-10 | MUST | During a transition the `[data-current]` container carries `data-starting-style=""` for the first frame only, after which the a… | 4.4 |
| PSH-DOM-11 | MUST | Auto-resize (active while a Viewport is mounted): (a) on each open the popup element's inline `--popup-width`/`--popup-height` … | 4.4 |
| PSH-DOM-12 | MUST | While a Viewport is mounted and the popup is mounted, the popup element has inline anchoring styles depending on the resolved s… | 4.4 |
| PSH-DOM-13 | MUST | A focus guard renders with `tabindex="0"`, `data-base-ui-focus-guard=""`, the visually-hidden inline style (UTL), and `aria-hid… | 4.4 |
| PSH-DOM-14 | MUST | The internal backdrop renders `role="presentation"`, `data-base-ui-inert=""`, and inline `position: fixed; inset: 0; user-selec… | 4.4 |
| PSH-DOM-15 | MUST | Every Popup element carries `tabindex="-1"` and `data-base-ui-focusable=""` (the focusable-popup attributes), which the FLT foc… | 4.4 |
| PSH-DOM-16 | MUST | Dialog.Trigger and Popover.Trigger render `data-base-ui-click-trigger=""`, which the FLT focus manager uses to recognize a poin… | 4.4 |
| PSH-DOM-17 | MUST | The swipe tracker's drag styles on the tracked element: at rest (not swiping, zero delta, not dismissed) only the two movement … | 4.4 |
| PSH-DOM-18 | MUST | The movement CSS variable names are supplied by the consumer: Drawer uses `--drawer-swipe-movement-x`/`-y`; Toast's own swipe i… | 4.4 |
| PSH-ARIA-01 | MUST | A click-opened Trigger (Dialog, AlertDialog, Drawer, Popover, Menu, Select, Combobox) renders `aria-expanded="true"` iff `isOpe… | 4.5 |
| PSH-ARIA-02 | MUST | Such a Trigger renders `aria-controls="<popup id>"` iff `triggerPopupId(ownId)` resolves (PSH-STA-10), and omits the attribute … | 4.5 |
| PSH-ARIA-03 | MUST | `aria-controls` uses the popup element's own `id` when the consumer sets one on the Popup, otherwise the generated popup id. | 4.5 |
| PSH-ARIA-04 | MUST | `aria-haspopup` on a click-opened Trigger has the component's fixed value (`dialog` for Dialog/Popover, `menu` for Menu, `listb… | 4.5 |
| PSH-ARIA-05 | MUST | A focus guard is `aria-hidden="true"`, except under VoiceOver on WebKit where it is `role="button"` without `aria-hidden` so th… | 4.5 |
| PSH-ARIA-06 | MUST | The internal backdrop has `role="presentation"`. | 4.5 |
| PSH-ARIA-07 | MUST | The `[data-previous]` viewport container has the `inert` attribute for its whole lifetime. | 4.5 |
| PSH-ARIA-08 | MUST | A Popup part rendered open carries its component's fixed popup `role` (`expectedPopupRole`: dialog, alertdialog, menu, listbox)… | 4.5 |
| PSH-KEY-01 | MUST | When focus arrives on the trigger's after-guard from an element outside the positioner (typically <kbd>Tab</kbd> from the trigg… | 4.6 |
| PSH-KEY-02 | MUST | When focus arrives on the trigger's after-guard from inside the positioner (<kbd>Tab</kbd> past the last popup tabbable), the p… | 4.6 |
| PSH-KEY-03 | MUST | When focus arrives on the trigger's before-guard (<kbd>Shift</kbd>+<kbd>Tab</kbd> from the trigger), the popup is closed synchr… | 4.6 |
| PSH-KEY-04 | MUST | The destination of PSH-KEY-02/03 is resolved *after* the close has been applied (so tab-order changes made by the close callbac… | 4.6 |
| PSH-KEY-05 | MUST | When no tabbable element exists in the requested direction, focus ends on the trigger after the close (the FLT focus manager's … | 4.6 |
| PSH-KEY-06 | MUST | Closing through the trigger focus guards inside a modal Dialog keeps the Dialog's focus trap intact: the destination is the nex… | 4.6 |
| PSH-KEY-07 | MUST | The focus-guard close is applied synchronously (flushed before the handler returns) so that the destination resolution in PSH-K… | 4.6 |
| PSH-PTR-01 | MUST | A trigger's open interaction type is determined on activation: on `pointerdown` the event's `pointerType` (`mouse`, `touch`, `p… | 4.7 |
| PSH-PTR-02 | MUST | The interaction type is written to `openMethod` only when the popup is *closed* at the moment of the handler (so a closing clic… | 4.7 |
| PSH-PTR-03 | MUST | Root-level variant: `useOpenInteractionType(open)` keeps `openMethod` in local state, resets it to `null` when `open` goes from… | 4.7 |
| PSH-PTR-04 | MUST | When enabled with `mouseDownAction: 'open'`, a `mousedown` on the trigger while the popup is closed (or, with `'close'`, while … | 4.7 |
| PSH-PTR-05 | MUST | While a modal popup is mounted, the internal backdrop covers the whole viewport beneath the popup so pointer interaction with o… | 4.7 |
| PSH-PTR-06 | MUST | The internal backdrop is marked `data-base-ui-inert` so FLT's outside-press detection treats it as an element that existed when… | 4.7 |
| PSH-PTR-07 | MUST | Anchored scroll lock: when `enabled` is `true` and the popup was not opened by touch, page scroll is locked (UTL `useScrollLock… | 4.7 |
| PSH-PTR-08 | MUST | A trigger using inline-rect tracking records the pointer's client coordinates, the index of the line box under the pointer (±2… | 4.7 |
| PSH-PTR-09 | MUST | A gesture is considered for starting on `pointerdown`/`touchstart` only when the tracker is enabled, the event is not default-p… | 4.7 |
| PSH-PTR-10 | MUST | Starting is refused when: the `canStart` predicate (given the position, native event and primary direction) returns `false`; fo… | 4.7 |
| PSH-PTR-11 | MUST | On start: the element's current translation and scale are read from its computed transform and become the initial offset; the e… | 4.7 |
| PSH-PTR-12 | MUST | `swipeThreshold` numbers below 0 are clamped to 0; the default is 40 px; a function form is re-evaluated with `{ element, direc… | 4.7 |
| PSH-PTR-13 | MUST | The first move after a start, when `trackDrag` is `true`, re-anchors the drag origin and the velocity start time to that move's… | 4.7 |
| PSH-PTR-14 | MUST | Direction detection (PSH-ALG-07): until a direction is intended, each move picks the axis with the larger absolute delta from t… | 4.7 |
| PSH-PTR-15 | MUST | Reverse cancel: once a direction is intended, and only when neither axis allows both of its directions, a move whose displaceme… | 4.7 |
| PSH-PTR-16 | MUST | Damping: movement on an axis with no allowed direction, or in the disallowed direction of an axis, is damped to `sign(d)·√\|d… | 4.7 |
| PSH-PTR-17 | MUST | Progress is `displacement / (size × scale)` along the primary direction (single-direction trackers) or the intended direction,… | 4.7 |
| PSH-PTR-18 | MUST | Pointer-event moves while swiping call `preventDefault()` (text selection); touch-event moves never do, so native touch scrolli… | 4.7 |
| PSH-PTR-19 | MUST | A touch-like move while swiping whose target is inside a scrollable touch target (other than the page scroller) on the gesture … | 4.7 |
| PSH-PTR-20 | MUST | A pointer move with a non-primary button pressed (`buttons` set and not including the primary bit) cancels the gesture: swiping… | 4.7 |
| PSH-PTR-21 | MUST | A pointer move with `buttons === 0` after a primary press is treated as the release: if the gesture never activated it is disca… | 4.7 |
| PSH-PTR-22 | MUST | Velocity sampling: each applied move records `(offset, time)` unless it is the *first* stationary sample (both axes moved < 1 p… | 4.7 |
| PSH-PTR-23 | MUST | On release, `velocityX/Y = Δ / max(duration, 50 ms)` where `Δ` is the total drag delta and `duration` the time from the (re-a… | 4.7 |
| PSH-PTR-24 | MUST | Release decision (PSH-ALG-11): `onRelease(details)` is called first; a boolean return overrides everything (true → dismiss in… | 4.7 |
| PSH-PTR-25 | MUST | Dismissal sets `swipeDirection` to the dismissing direction and `dragDismissed` to `true`, restores the inline styles, and call… | 4.7 |
| PSH-PTR-26 | MUST | On release and cancel, swiping becomes `false` (`onSwipingChange(false)`), the pending state is cleared and, for pointer events… | 4.7 |
| PSH-PTR-27 | MUST | `reset()` returns the tracker to its initial state (no direction, not swiping, not dismissed, progress 0, default threshold, ze… | 4.7 |
| PSH-PTR-28 | MUST | The tracker exposes `getPointerProps()` (`onPointerDown/Move/Up/Cancel`) and `getTouchProps()` (`onTouchStart/Move/End/Cancel`)… | 4.7 |
| PSH-PTR-29 | MUST | When a non-canceled `trigger-hover` open request carries a trigger and client coordinates and the stored inline-rect record doe… | 4.7 |
| PSH-FOC-01 | MUST | Default `initialFocus` (Dialog, Popover): when the popup was opened by touch (open interaction type `touch`) the popup element … | 4.8 |
| PSH-FOC-02 | MUST | `initialFocus` resolves as: an element or a DOM handle → that element is focused on open; `true` or `null` → the default ta… | 4.8 |
| PSH-FOC-03 | MUST | When `initialFocus` is a function it is called exactly once per open with the open interaction type (`mouse`, `touch`, `pen`, `… | 4.8 |
| PSH-FOC-04 | MUST | `finalFocus` resolves on close as: `undefined`, `true` or `null` → the element that opened the popup (the active trigger, or … | 4.8 |
| PSH-FOC-05 | MUST | When `finalFocus` is a function it is called exactly once per close with the close interaction type: `mouse` when the close con… | 4.8 |
| PSH-FOC-06 | MUST | When a popup opened programmatically (controlled `open`/`triggerId`) is closed from inside with its Close part, focus returns t… | 4.8 |
| PSH-FOC-07 | MUST | Trigger focus guards (PSH-STR-10, PSH-KEY-01..05) are rendered around a Popover.Trigger iff `isOpenedByTrigger(ownId)` and the … | 4.8 |
| PSH-FOC-08 | MUST | Popover's focus-manager-modal flag is `true` iff `modal !== false` **and** at least one Popover.Close part is currently mounted… | 4.8 |
| PSH-FOC-09 | MUST | The before-trigger guard is reported to the focus manager as an "inside" element, so moving focus from the trigger onto it is n… | 4.8 |
| PSH-DIS-01 | MUST | Pressing <kbd>Escape</kbd> while an uncontrolled popup is open closes it, with `onOpenChange(false, { reason: 'escape-key' })`. | 4.9 |
| PSH-DIS-02 | MUST | With a controlled `open={true}` whose owner does not react to `onOpenChange`, <kbd>Escape</kbd> leaves the popup open (the requ… | 4.9 |
| PSH-DIS-03 | MUST | `handle.close()` and `actionsRef.current.close()` request a close with reason `imperative-action` and `trigger === undefined`. | 4.9 |
| PSH-DIS-04 | MUST | Tabbing out through a trigger focus guard requests a close with reason `focus-out` (PSH-KEY-02/03). | 4.9 |
| PSH-DIS-05 | MUST | The active-trigger-unmount close (PSH-STA-23) uses reason `none` and no event. | 4.9 |
| PSH-DIS-06 | MUST | A canceled open request (`cancel()` inside `onOpenChange`) leaves the popup closed and the trigger without `data-popup-open`; n… | 4.9 |
| PSH-EVT-01 | MUST | Every open-state change request runs the popup open-change sequence specified in [event-details-and-reasons](event-details-and-… | 4.10 |
| PSH-EVT-02 | MUST | Opening from a trigger calls `onOpenChange` exactly once with `(true, details)` where `details.reason` is `trigger-press` (clic… | 4.10 |
| PSH-EVT-03 | MUST | `onOpenChange` is not called on mount, and not called for a controlled prop change applied by the owner. | 4.10 |
| PSH-EVT-04 | MUST | `eventDetails.trigger` on a close request: Popover sets it to the active trigger's registered element (falling back to the stor… | 4.10 |
| PSH-EVT-05 | MUST | During `onOpenChange`, `eventDetails.preventUnmountOnClose()` is available; calling it on a close request keeps the popup mount… | 4.10 |
| PSH-EVT-06 | MUST | `handle.open(id)` requests an open with `reason: 'imperative-action'`, `event: undefined`, `trigger: <registered element>`; `ha… | 4.10 |
| PSH-EVT-07 | MUST | `onOpenChangeComplete` is not called on mount while closed; it is called with `true` once the enter transition finishes (immedi… | 4.10 |
| PSH-EVT-08 | MUST | There is no separate active-trigger change callback: a controlled consumer derives the new `triggerId` from `eventDetails.trigg… | 4.10 |
| PSH-EVT-09 | MUST | Swipe tracker callbacks: `onSwipeStart(nativeEvent)`; `onProgress(progress, details?)` with `details = { deltaX, deltaY, direct… | 4.10 |
| PSH-ANI-01 | MUST | `mounted` becomes `true` synchronously with an open request and stays `true` after a close until the popup's exit animations fi… | 4.12 |
| PSH-ANI-02 | MUST | A popup that mounts already open (`defaultOpen`, server-rendered open state) does not play its enter transition unless the Root… | 4.12 |
| PSH-ANI-03 | MUST | A `trigger-hover` open change is committed synchronously as specified by [EVD-EVT-18](event-details-and-reasons.md); the shared… | 4.12 |
| PSH-ANI-04 | MUST | The shared open-change sequence writes the reason-derived instant-phase flag (`instantType`) exactly as specified by [EVD-EVT-1… | 4.12 |
| PSH-ANI-05 | MUST | A viewport content transition starts post-commit, pre-paint when all hold: the popup is open; an active trigger element exists;… | 4.12 |
| PSH-ANI-06 | MUST | A trigger that remounts (new element, same id) while the popup is closed but retained (`preventUnmountOnClose`) MUST NOT start … | 4.12 |
| PSH-ANI-07 | MUST | After every commit the viewport captures a deep clone of the current container's children as the candidate snapshot, so that on… | 4.12 |
| PSH-ANI-08 | MUST | Transition choreography: in the commit that starts (or restarts) a transition the current container has `data-starting-style` a… | 4.12 |
| PSH-ANI-09 | MUST | Cleanup: on animations finished of the current container (subtree included), the previous container is removed, the stored prev… | 4.12 |
| PSH-ANI-10 | MUST | The current container is keyed by the content key (PSH-ALG-14): it is remounted (new element) when the active trigger id change… | 4.12 |
| PSH-ANI-11 | MUST | The transition machinery works again after a kept-mounted popup closes and reopens. | 4.12 |
| PSH-ANI-12 | MUST | During an auto-resize measurement the popup has inline `position: static; transform: none; scale: 1`, the positioner's availabl… | 4.12 |
| PSH-ANI-13 | MUST | Auto-resize state resets when the popup unmounts (`mounted` false), so the next open measures afresh (PSH-DOM-11(a)); while mou… | 4.12 |
| PSH-ANI-14 | MUST | A popup rendered with `keepMounted` stays in the DOM after closing and becomes inaccessible (hidden) once its exit animation fi… | 4.12 |
| PSH-POS-01 | MUST | The anchor used for positioning is the derived active trigger element (PSH-STA-06, PSH-STA-46); switching the active trigger wh… | 4.13 |
| PSH-POS-02 | MUST | While a Viewport is mounted the store's `adaptiveOrigin` middleware is set (and cleared when it unmounts), so FLT positions wit… | 4.13 |
| PSH-POS-03 | MUST | PreviewCard's inline-rect middleware (PSH-ALG-16) replaces the reference rectangle with the hovered line box of a wrapping trig… | 4.13 |
| PSH-I18N-01 | MUST | Viewport anchoring styles (PSH-DOM-12) resolve logical sides physically: `inline-start` is left in LTR and right in RTL; `inlin… | 4.15 |
| PSH-I18N-02 | MUST | Swipe directions (`up`, `down`, `left`, `right`) are physical; the tracker performs no RTL mirroring. | 4.15 |
| PSH-ALG-01 | MUST | The implementation reproduces the popup open-change sequence exactly as EVD-ALG-04 specifies (shared routine for Tooltip/Previe… | 5 |
| PSH-ALG-02 | MUST | The implementation reproduces the committed-open-state derivation exactly as EVD-ALG-05 specifies (`createPopupOpenState`). | 5 |
| PSH-ALG-03 | MUST | The implementation reproduces the following procedure exactly for active-trigger reconciliation, run by the Root post-commit, p… | 5 |
| PSH-ALG-04 | MUST | The implementation reproduces the following procedure exactly for trigger registration (the callback a Trigger attaches to its … | 5 |
| PSH-ALG-05 | MUST | The implementation reproduces the following procedure exactly when a Trigger with forwarded state (`payload` plus component fie… | 5 |
| PSH-ALG-06 | MUST | The implementation reproduces the following procedure exactly for handle attachment and imperative open. | 5 |
| PSH-ALG-07 | MUST | The implementation reproduces the following procedure exactly for swipe move handling (after start, PSH-PTR-11). | 5 |
| PSH-ALG-08 | MUST | The implementation reproduces the following procedure exactly for directional damping. | 5 |
| PSH-ALG-09 | MUST | The implementation reproduces the following procedure exactly for swipe progress. | 5 |
| PSH-ALG-10 | MUST | The implementation reproduces the following procedure exactly for velocity sampling and release velocities. | 5 |
| PSH-ALG-11 | MUST | The implementation reproduces the following procedure exactly for the release decision. | 5 |
| PSH-ALG-12 | MUST | The implementation reproduces the following procedure exactly for starting a pending touch gesture from a scroll edge. | 5 |
| PSH-ALG-13 | MUST | The implementation reproduces the following procedure exactly for the viewport activation direction. | 5 |
| PSH-ALG-14 | MUST | The implementation reproduces the following procedure exactly for the viewport content key (post-commit, pre-paint when the act… | 5 |
| PSH-ALG-15 | MUST | The implementation reproduces the following procedure exactly for auto-resize (post-commit, pre-paint when `mounted`, the popup… | 5 |
| PSH-ALG-16 | MUST | The implementation reproduces the following procedure exactly for inline-rect line grouping and reference-rect selection. | 5 |
| PSH-ALG-17 | MUST | The implementation reproduces the following procedure exactly for the anchored scroll-lock decision. | 5 |
| PSH-ALG-18 | MUST | The implementation reproduces the following procedure exactly for the trigger focus guards. | 5 |
| PSH-ALG-19 | MUST | The implementation reproduces the following procedure exactly for the open interaction type (trigger-level variant). | 5 |
| PSH-ALG-20 | MUST | The implementation reproduces the following procedure exactly for the mixed mousedown/click toggle guard. | 5 |
| PSH-ALG-21 | MUST | The implementation reproduces the following procedure exactly for the internal backdrop cutout. | 5 |
| PSH-ALG-22 | MUST | The implementation reproduces the following procedure exactly for the popup-id and trigger-ownership selectors. | 5 |
| PSH-EDGE-01 | MUST | A Trigger rendered with neither an ancestor Root of its family nor a `handle` prop throws (in every environment) an error whose… | 7 |
| PSH-EDGE-02 | MUST | `handle.open()`, `handle.close()` and `handle.openWithPayload()` called while no Root is attached are ignored (no state change,… | 7 |
| PSH-EDGE-03 | MUST | For anchored popups (Menu, Popover, PreviewCard, Tooltip), `handle.open(id)` with an id that no registered trigger matches thro… | 7 |
| PSH-EDGE-04 | MUST | For Dialog, AlertDialog and Drawer, `handle.open(id)` with an unmatched id opens the popup *unassociated* (no active trigger, `… | 7 |
| PSH-EDGE-05 | MUST | `handle.open(id)` with an empty-string id behaves like `open(null)` (no lookup, no throw, no warning, trigger-less open). | 7 |
| PSH-EDGE-06 | MUST | When more than one Root is attached to the same handle, the check is deferred by one frame and, if more than one Root is still … | 7 |
| PSH-EDGE-07 | MUST | Outside production, registering an element in a registry under a second, different id throws `Base UI: A trigger element cannot… | 7 |
| PSH-EDGE-08 | MUST | Replacing the handle object on both the Root and its detached Triggers with a freshly created one (hot-reload style), with or w… | 7 |
| PSH-EDGE-09 | MUST | During a transient overlap (a new Root attaches while the old one, holding the trigger registrations, is still mounted) an impe… | 7 |
| PSH-EDGE-10 | MUST | Setting or releasing pointer capture in the swipe tracker swallows a `NotFoundError` (pointer already gone) and does nothing wh… | 7 |
| PSH-EDGE-11 | MUST | Swipe event timestamps that are non-finite or ≤ 0 are ignored for velocity purposes (no sample recorded; duration 0 → veloc… | 7 |
| PSH-EDGE-12 | MUST | A touch start/move/end without a primary touch point (empty `touches`) is ignored by the swipe tracker. | 7 |
| PSH-EDGE-13 | MUST | The page scroller (`<body>` or `<html>` made scrollable by a CSS reset) never gates a swipe start or move; only other scrollabl… | 7 |
| PSH-EDGE-14 | MUST | Detaching a Root store and immediately attaching the same store again leaves the handle exposing that store, and imperative cal… | 7 |

## 10. Open questions

1. **`payload` retention after close (PSH-STA-32).** `utils/popups/popupStoreUtils.ts:586-595` clears the active trigger on unmount but never clears `payload`; the function-as-child keeps receiving the last payload while closed. The docs do not state whether this is intended (it is convenient for exit animations). A port should preserve it; confirm with the component authors whether DLG/POP/MNU docs should promise it. Resolved 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); settled by the orchestrator 2026-10-27, plan §Settled "§16 port choices"): as the source does. The port's unmount cleanup clears the active trigger and keeps the payload, so the Root's payload template keeps the last payload while closed (L2: "ignores imperative calls while no root is attached and follows the root across remounts", where a remounted Root starts with no payload).
2. **`handle.open('')` (PSH-EDGE-05).** `popupHandle.ts:234,241` treat an empty-string id as "no trigger" via truthiness rather than a `null` check, so `Menu.createHandle().open('')` opens an anchored popup without a trigger instead of throwing. Unclear whether intentional; no test. Resolved 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); settled by the orchestrator 2026-10-27, plan §Settled "§16 port choices"): as the source does. `PopupHandle.OpenAsync` treats `null` and the empty string alike as "no trigger" (L1: "opens without a trigger when the id is $triggerId", both values).
3. **`data-instant` on Viewport parts.** The API outline lists `data-instant` on Menu/Popover/PreviewCard/Tooltip Viewport, but `usePopupViewport` does not produce it; each Viewport part adds `instant: store.instantType` itself (`popover/viewport/PopoverViewport.tsx:26-38`). Documented here as component-specific; the value sets differ per component (Popover: `click | dismiss | focus | trigger-change`; Menu: `click | dismiss | group | trigger-change`).
4. **Toast swipe.** Toast reuses the `data-swiping` / `data-swipe-direction` / `--toast-swipe-movement-*` naming but implements its own gesture logic in `toast/root/ToastRoot.tsx` rather than `useSwipeDismiss`. Left to TST; the TST author should confirm whether the thresholds coincide (this document's PSH-PTR-* do not apply to Toast).
   Resolved: toast.md Q4 confirms the separate implementation (max displacement set once at arming, not a running maximum); §6.2 Toast row now states that Toast does not use `useSwipeDismiss` beyond the `getDisplacement` helper. Second-pass correction: the "running maximum" contrast was wrong — `useSwipeDismiss` also sets the reference displacement once (`utils/useSwipeDismiss.ts:644`), exactly like `toast/root/ToastRoot.tsx:372`; PSH-PTR-15 and the §6.2 row were reworded. The PSH swipe requirements (PSH-PTR-09..28, PSH-ALG-07..12) describe the shared tracker used by Drawer.Viewport and Drawer.SwipeArea only.
5. **Anchored scroll-lock tolerance (PSH-PTR-07).** The 20px full-width tolerance (`useAnchoredPopupScrollLock.ts:11`) has no test; it is written as MUST because the Popover docs promise the "nearly the full viewport width" behavior, but a reviewer may downgrade it to SHOULD.
6. **PSH-KEY-01 (Tab from trigger enters the popup) has no direct test**; it is implied by the `useTriggerFocusGuards` fixtures (focus is placed inside before tabbing). Chromium-only suite (`describe.skipIf(isJSDOM)`).
7. **Open-change sequence ownership.** PSH-EVT-01/PSH-ALG-01/PSH-ALG-02 defer to EVD-ALG-04/05 and PSH-STA-45 overlaps EVD-ALG-06; if EVD renumbers, these cross-references must be updated. PSH-STA-05 defers to TRN-STA-14.
   Resolved: verified 2026-10-14 — EVD-ALG-04 (open-change sequence), EVD-ALG-05 (committed open state), EVD-ALG-06 (close completion, same field order as PSH-STA-45 minus the component unmount hook) and TRN-STA-14 (`'starting'` while `open && !mounted`) match the statements that cite them.
8. **Outline vs. source: `--popup-width`/`--popup-height` on Popup.** The outline lists these CSS variables on Popup parts and on Viewport parts; the source sets them on the popup element (auto-resize) and on the `[data-previous]` container (viewport). The Viewport listing presumably refers to the previous container. No discrepancy in names.
9. **`FocusGuard` VoiceOver heuristic (PSH-DOM-13/ARIA-05)** depends on UTL `platform.screenReader.voiceOver` detection, which is heuristic; no test exists.
10. **Menu detached triggers are limited to top-level menus** (`components/menu/page.mdx:207-208`); this document does not encode the restriction because it is enforced by Menu's parent-type logic (MNU).
11. **PSH-STA-23 microtask deferral in the port.** `utils/popups/popupStoreUtils.ts:524-543` defers the close-on-active-trigger-unmount by `queueMicrotask` so a same-tick replacement trigger can register first. ADR 04 §1 places the popup store in C#, and ADR 01 §Decision item 3 (constraint C2) forbids C# emulating microtasks, yet the ADR 01 Relaxation table has no PSH-STA-23 row. The port needs a decision: either an equivalent "after the current registration batch" deferral or a Relaxation-table row. Decided (2026-10-18): maps to the render-batch boundary (`OnAfterRender`), with a relaxation row for Server (ADR 01 constraint C2 as amended).
12. **Implementation revision of 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 05, ADR 04, ADR 16 and ADR 12 Amendments 2026-10-27; ADR 13 Amendments 2026-10-22).** Dated 2026-10-27. The popup store is C#'s (`src/Blazix.Primitives/Popup/`): `PopupStoreState<TPayload>` nested as one member of each family's record (`IPopupStoreState`), `PopupStore<TState, TPayload>` with the open-change sequence, the trigger registry (`PopupTriggerRegistry`, an element being the registering part plus its `ElementReference`, settled by the orchestrator 2026-10-27), the reconciliation at the render-batch boundary (design point 8), the transitions plumbing and the relay (`OpenChangeRelay`, with its coalesced close); `PopupHandle<TPayload>`; the Root and Trigger behaviours; the portal (`PopupPortalBehavior`, the `portal` module) and the scroll lock (`PopupScrollLockBehavior`, the `scroll-lock` module). No public part ships: every row is proven on test-only hosts, `TestPopup*` at L2 and `FixturePopup*`, `FixturePortal` and `FixtureScrollLock` at L4, the shared suites running as TypeScript helpers under `tests/browser/suites/` on fixtures inheriting `PopupSuiteFixtureBase` (design point 7, ADR 06 Amendments 2026-10-27). Green here, both render modes where the layer is L4: the [L1] rows (`tests/Blazix.Primitives.Tests/Popup/`; their "(L4 for …)" clauses through the suites); the [L2] rows on `popupStoreUtils.test.tsx`, `popupHandle.test.ts` and `FloatingPortal.test.tsx` titles and on the suite titles the store observes (`tests/Blazix.Primitives.Tests.Components/Popup/`); the eight rows re-tagged to [L4] and the [L4] rows of the two suites run on the popup host (`tests/browser/popup.spec.ts`), with PSH-STA-23 per mode. Open, each listed in the pull request by ID: the titles that press Escape (PSH-DIS-01's: PSH-STA-01's and PSH-EVT-03's cited titles, PSH-EVT-02, "is ignored when `open={true}` is passed, so Escape cannot close") → M4 track 3 (green on both modes since 2026-10-28: the test-only popup root composes the `interactions` session with Escape dismissal, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)); the focus return of PSH-EVT-08's title ("allows controlling the open state and active trigger programmatically") and the focus rows → M4 track 4; the Viewport, the auto-resize and the trigger focus guards (`viewportConformanceTests` and `popupFocusPropsTests` are written for the families to run first) → M4 track 5 and the families; the swipe rows → M6; the Menu and Select clauses → M5. No title run on the popup host asserts the `interactions` module's press semantics (the pointer type of a press, an open on press rather than on click, the mixed toggle): every one presses the trigger with a click through its C# `onclick` (design point 4), so none is listed open to M4 track 3 for press semantics; the families re-run the suites over `press`. The render counts the L2 title measured through `ClassValue` ("port: with a popup part registered, an open and a close render neither the root nor an inactive trigger, and without one the store renders the root on the reconciliation's inputs"): with a popup part registered, an open and a close by a trigger add 0 renders to the Root and 0 to an inactive trigger; without one, an open adds 1 to each. The Root re-render's byte budget is deferred to M4 track 5 (settled by the orchestrator 2026-10-27), which adds it with Popover's budgets once PR 127's non-parallel budget collection is on `main`, since local figures run about 168 bytes above CI's on this runtime; a popup write that changes nothing is measured here at 0 bytes (L1), and a portal render with unchanged inputs at 0 bytes (L2). Design points 1–8 of the track, each settled by the orchestrator 2026-10-27 or chosen by the user, are recorded on the decision records they amend (ADR 02, 04, 05, 06, 08, 16).
