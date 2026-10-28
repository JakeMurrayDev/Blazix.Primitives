# Transitions and mounting

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `internals/useTransitionStatus.ts`, `internals/TransitionStatusDataAttributes.ts`, `internals/stateAttributesMapping.ts`, `internals/useAnimationsFinished.ts`, `utils/getFiniteAnimations.ts`, `internals/useOpenChangeComplete.tsx`, `internals/useUnmountAfterClose.ts`, `internals/useTriggerSwitchTransition.ts`, `internals/getDisabledMountTransitionStyles.ts`, `internals/constants.ts` (`DISABLED_TRANSITIONS_STYLE`), `utils/useIsHydrating.ts`, `packages/utils/src/hydration.ts`, `internals/PrehydrationScript.tsx`, `internals/prehydrationScript.stub.ts`, `packages/utils/src/useAnimationFrame.ts` (frame scheduler), `utils/popups/popupStoreUtils.ts` (`useOpenStateTransitions`, `createPopupOpenState`, `attachPreventUnmountOnClose`), `utils/popups/store.ts` (`transitionStatus` selector), `collapsible/panel/useCollapsiblePanel.ts` (shared `hidden="until-found"` / `beforematch` contract only) |
| Tests | `internals/useTransitionStatus.test.tsx`, `internals/useAnimationsFinished.test.tsx`, `internals/stateAttributesMapping.test.ts`, `test/popupConformanceTests.tsx` ("prop: onOpenChangeComplete", "animations"), `popover/popup/PopoverPopupTransitionState.test.tsx`, `menu/popup/MenuPopupTransitionState.test.tsx`, `menu/popup/MenuPopupEnterTransition.test.tsx`, `autocomplete/root/AutocompleteRoot.test.tsx` (actions / opt-out block), `popover/root/PopoverRoot.detached-triggers.test.tsx` (`trigger-change` block), `collapsible/panel/CollapsiblePanel.test.tsx` (`hiddenUntilFound` block), component "animations" blocks cited inline |
| Prefix | `TRN` |
| Status | approved |
| Revised | 2026-10-27 |
| Used by | `components/accordion.md`, `components/alert-dialog.md`, `components/autocomplete.md`, `components/avatar.md`, `components/checkbox.md`, `components/collapsible.md`, `components/combobox.md`, `components/context-menu.md`, `components/dialog.md`, `components/drawer.md`, `components/field.md`, `components/menu.md`, `components/menubar.md`, `components/navigation-menu.md`, `components/popover.md`, `components/preview-card.md`, `components/radio.md`, `components/select.md`, `components/tabs.md`, `components/toast.md`, `components/tooltip.md`; `architecture/popup-store-and-handles.md` (PSH) consumes `useOpenStateTransitions`; `architecture/floating-and-positioning.md` (FLT) consumes `transitionStatus` and `keepMounted` |

Path conventions in this document: paths are relative to `reference/base-ui/packages/react/src/` unless they start with `packages/utils/` or `docs/`. `test/<file>` means `reference/base-ui/packages/react/test/<file>` (shared conformance suites). The repo-root test bootstrap is cited explicitly as `reference/base-ui/test/setupVitest.ts`.

## 1. Purpose

Every Base UI part that appears and disappears (popups, dialog popups and viewports, collapsible and accordion panels, tab panels, toasts, checkbox/radio/menu-item indicators, the avatar image, the combobox clear button, select scroll arrows, field error messages, navigation-menu content) goes through the same lifecycle: it is mounted into the DOM, marked as "starting", left alone while open, marked as "ending" when it closes, kept in the DOM until its CSS transitions or animations finish, and only then removed (or hidden, when the consumer asked for it to stay mounted). The consumer styles the enter and exit phases with two data attributes (`data-starting-style`, `data-ending-style`) and is told when a phase has finished (`onOpenChangeComplete`). JavaScript animation libraries get an escape hatch (`preventUnmountOnClose()` + `actionsRef.current.unmount()`) for animations the library cannot detect.

This document specifies that shared lifecycle so that every component document can say "closing follows TRN" and only list its configuration. A port cannot skip it: the exact commit in which each attribute appears, the one-frame deferrals, the animation-completion algorithm (including cancelled and infinite animations), the unmount-after-animation timing, the `keepMounted`/`hidden` contract and the `hidden="until-found"` find-in-page behavior are all asserted by the Base UI test suite and relied upon by the official animation handbook.

The companion mechanisms that *read* the transition status but are owned elsewhere: popup store wiring and `instantType` assignment from change reasons (`architecture/popup-store-and-handles.md`), anchored positioning while kept mounted (`architecture/floating-and-positioning.md`), state → data-attribute mapping machinery (`architecture/rendering-and-composition.md`), event details and `reason` values (`architecture/event-details-and-reasons.md`), `CSPProvider` nonce (`architecture/shared-utilities.md`).

## 2. Concepts and terminology

### 2.1 Timing vocabulary

This document uses the canonical timing terms from `conventions.md` §4 exactly:

| Term | Meaning here |
|---|---|
| **in the same commit** | The DOM update that applies the triggering state change. "X happens in the same commit as Y" means no DOM state is ever observable in which Y has been applied but X has not. |
| **post-commit, pre-paint** | Synchronously after the DOM has been updated and before the browser paints. |
| **after paint** | After the browser has painted the updated DOM; no specific frame guaranteed. |
| **next frame** | One animation frame after the triggering point (`requestAnimationFrame`). Base UI batches all frame requests made in one task into one native frame (`packages/utils/src/useAnimationFrame.ts:15-84`), and cancelling a request is O(1) without re-arming the native frame. |
| **microtask** | Before the next macrotask, after the current synchronous work (`Promise.then`, `queueMicrotask`). |
| **macrotask / 0 ms** | A timer with the stated delay. |
| **on animations finished** | When all finite running animations on the element have ended or been cancelled, as defined by TRN-ALG-03. Component docs use this phrase to reference TRN-ALG-03 without restating it. |
| **synchronous commit** | A DOM update forced to commit immediately inside the current microtask (React `flushSync`), so no paint can occur between the trigger and the committed result. |

### 2.2 Glossary

| Base UI / React name | Neutral term used below | Definition |
|---|---|---|
| `open` | **open state** | The logical visibility the consumer or component requested. Changes immediately when the user acts; never waits for animations. |
| `mounted` (from `useTransitionStatus`) | **mounted flag** | Whether the part is in its "present" lifecycle: `true` from the moment opening is requested until the exit animation has finished (or the unmount action ran). Drives whether the element exists in the DOM (unless `keepMounted`) and whether it carries `hidden`. |
| `transitionStatus` | **transition status** | One of `'starting'`, `'ending'`, `'idle'`, or *unset* (`undefined`). The phase of the enter/exit transition. |
| `'starting'` | **starting phase** | The first commit in which the part is mounted and open. Lasts until the next frame. Styled with `[data-starting-style]`. |
| `'ending'` | **ending phase** | From the commit that applies `open = false` (or the next frame, see `deferEndingState`) until the part is unmounted. Styled with `[data-ending-style]`. |
| `'idle'` | **idle status** | Explicit "open and settled" value, only produced when the consumer of the state machine enables it (`enableIdleState`, used by Collapsible/Accordion). Otherwise the settled value is *unset*. |
| `undefined` status | **unset status** | No transition in progress: settled open (default machine) or fully closed/unmounted. |
| `keepMounted` | **keep-mounted option** | Consumer request to leave the element in the DOM while it is not mounted. The element then carries `hidden` (popups, panels) or nothing (indicators). |
| `hiddenUntilFound` | **hidden-until-found option** | Consumer request to hide a closed panel with `hidden="until-found"` so the browser's find-in-page can reveal it. Implies keep-mounted. |
| `beforematch` | **reveal event** | Browser event fired on a `hidden="until-found"` element when find-in-page (or a fragment navigation) is about to reveal it. |
| `Animation` / `element.getAnimations()` | **running animation** | A Web Animations API `Animation` object currently associated with the element (CSS transitions, CSS animations and script-created animations alike). |
| finite animation | **finite animation** | A running animation whose effect timing has neither `duration: Infinity` nor `iterations: Infinity`. Only finite animations are awaited. |
| `animation.finished` resolved / rejected | **animation finished / animation cancelled** | The promise resolves when the animation completes and rejects when it is cancelled (for example because the property it animates changed mid-flight). |
| `onOpenChangeComplete(open)` | **completion callback** | Consumer callback fired once per open phase (`true`) and once per close phase (`false`) after the corresponding animations finish. |
| `eventDetails.preventUnmountOnClose()` | **unmount opt-out** | Called inside `onOpenChange` while closing: suspends automatic unmounting for *this* close cycle. |
| `actionsRef.current.unmount()` | **unmount action** | Imperative call that ends the closing phase immediately (fires the completion callback with `false`). |
| `instantType` / `data-instant` | **instant marker** | A string on the popup/positioner/viewport state (`instant`) that tells CSS the current open/close/move should not animate. The value names the cause (`'click'`, `'dismiss'`, `'focus'`, `'delay'`, `'group'`, `'trigger-change'`, `'tracking-cursor'`). Assignment of the reason-derived values is PSH's; this document owns `'trigger-change'`. |
| `DISABLED_TRANSITIONS_STYLE` | **mount-transition suppression** | Inline `style="transition: none"` applied during the starting phase to positioners and most popups so the first paint does not transition from the browser's default/unpositioned styles. |
| hydration | **hydration pass** | The first client render that adopts server-rendered HTML instead of creating it. |
| pre-hydration script | **inline positioning script** | A `<script>` element emitted in server HTML immediately after (or inside) a part so the browser positions it before the client takes over. |
| `BASE_UI_ANIMATIONS_DISABLED` | **animations-disabled flag** | A global boolean; when `true`, every animation wait completes synchronously. The test suite sets it to `true` by default. |

## 3. Public surface

Nothing in this document is a standalone export, but the following consumer-facing props and attributes have their semantics defined here and are shared verbatim by many components. Component documents list which parts carry each item and cite the IDs below instead of re-describing them.

### 3.1 Shared props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while it is not mounted. Official wording (portals): "Whether to keep the portal mounted in the DOM while the popup is hidden." Carried by: Portal parts of Dialog, AlertDialog, Drawer, Popover, Menu (and Context Menu / Menubar menus), Tooltip, PreviewCard, Combobox/Autocomplete, NavigationMenu; `Tabs.Panel`; `Checkbox.Indicator`; `Radio.Indicator`; `Menu.CheckboxItemIndicator`; `Menu.RadioItemIndicator`; `Select.ItemIndicator`; `Combobox.ItemIndicator`; `Avatar.Image`; `Select.ScrollUpArrow` / `Select.ScrollDownArrow`; `Combobox.Clear`; `NavigationMenu.Content`; `Collapsible.Panel`; `Accordion.Root` / `Accordion.Panel`; `ScrollArea.Scrollbar` (SCR-owned, no transition status). `Select.Portal` has no such prop: the select popup is removed until first opened and stays mounted afterwards (`select/positioner/SelectPositioner.tsx:111`; handbook line 180). `Toast.Positioner` is always kept mounted internally (`toast/positioner/ToastPositioner.tsx:88`). | TRN-API-01, TRN-API-02 |
| `hiddenUntilFound` | `boolean` | `false` | "Allows the browser's built-in page search to find and expand the panel contents. Overrides the `keepMounted` prop and uses `hidden="until-found"` to hide the element without removing it from the DOM." Carried by `Collapsible.Panel`, `Accordion.Root`, `Accordion.Panel`. | TRN-API-03 |
| `onOpenChangeComplete` | `(open: boolean) => void` | — | Completion callback. Carried by the roots of Dialog, AlertDialog, Drawer, Popover, Menu, ContextMenu, Tooltip, PreviewCard, Combobox, Autocomplete, Select, NavigationMenu. | TRN-API-04 |
| `actionsRef` | `Ref<{ unmount(): void; close(): void; … } \| null>` | — | `unmount()` is the unmount action. `close()` requests a close with reason `'imperative-action'` (EVD/PSH). Same roots as `onOpenChangeComplete`. | TRN-API-05 |
| `eventDetails.preventUnmountOnClose()` | `() => void` (method on the change event details) | — | Unmount opt-out for the current close cycle, available on the second argument of `onOpenChange` on the same roots (`onValueChange` for NavigationMenu). Official wording: "Call `preventUnmountOnClose()` in `onOpenChange` first, otherwise the <component> completes closing on its own. Whether it leaves the DOM is decided by `keepMounted` on the portal." (`popover/root/PopoverRoot.tsx:158-159`, mirrored on every root.) | TRN-API-06 |

- **TRN-API-01** — MUST. `keepMounted` (default `false`) keeps the part's element in the DOM while the part is not mounted (render predicate TRN-ANI-01; presentation while kept TRN-DOM-06/TRN-DOM-07). It MUST NOT change the open state.
  Source: `popover/portal/PopoverPortal.tsx:19-27,39-43`, `tabs/panel/TabsPanel.tsx:35,104-107,133-137`, `checkbox/indicator/CheckboxIndicator.tsx:24,58,85-89`. Test: [L2] `menu/positioner/MenuPositioner.test.tsx` "when keepMounted=true, should keep the content mounted when closed"; `checkbox/indicator/CheckboxIndicator.test.tsx` "should keep indicator mounted when unchecked".

- **TRN-API-02** — MUST. `keepMounted` MUST NOT alter the transition status sequence or the completion callbacks: a kept-mounted part goes through the same `'starting'` / `'ending'` phases, fires the same callbacks and reuses the same element across open cycles.
  Source: `internals/useTransitionStatus.ts:17-97` (no `keepMounted` input), `popover/portal/PopoverPortal.tsx:22-27`. Test: [L4] `popover/popup/PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted"; `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (controlled=$controlled, keepMounted=$keepMounted, defaultOpen=$defaultOpen)" (8 variants; cited below as "starts each opening once (…)"). Docs: CHANGELOG.md v1.9.0 "Keep transition state consistent when reopening a retained popup (#5738)".

- **TRN-API-03** — MUST. `hiddenUntilFound` (default `false`) forces the panel to stay mounted while closed (TRN-ANI-02), hides it with `hidden="until-found"` (TRN-DOM-08) and makes the panel open on the reveal event (TRN-ANI-32). It overrides `keepMounted={false}` (TRN-EDGE-04).
  Source: `collapsible/panel/CollapsiblePanel.tsx:59-60,81-83,146-157`, `accordion/root/AccordionRoot.tsx:104-105,176-186`, `collapsible/panel/useCollapsiblePanel.ts:461`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "uses `hidden=\"until-found\" to hide panel when true"; `accordion/panel/AccordionPanel.test.tsx` "passes root hiddenUntilFound to closed panels and allows panel overrides".

- **TRN-API-04** — MUST. `onOpenChangeComplete` receives exactly one argument, the boolean open state whose phase completed, and no event details (TRN-EVT-01..05 define when it fires).
  Source: `popover/root/PopoverRoot.tsx:150-154`, `utils/popups/store.ts:143`. Test: [L2] `test/popupConformanceTests.tsx` "is called on close when there is no exit animation" (`calls` equal `[[true],[false]]`).
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host (M4 track 1): L2 `PopupTransitionsTests` ("is called on close when there is no exit animation"), and L4 `popup.spec.ts` through `popupConformanceTests`, both modes.

- **TRN-API-05** — MUST. `actionsRef.current.unmount()` is the unmount action (TRN-EVT-08, TRN-ALG-05); the ref MAY be `null`/absent, in which case nothing is attached.
  Source: `popover/root/PopoverRoot.tsx:74-80,196-204`, `combobox/root/AriaCombobox.tsx:1017-1024`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once". Docs: CHANGELOG.md v1.1.0 "Allow `actionsRef` to be `null` (#3682)".

- **TRN-API-06** — MUST. The change event details passed to `onOpenChange` (and NavigationMenu's `onValueChange`) carry a `preventUnmountOnClose()` method whose effect is defined by TRN-EVT-07.
  Source: `popover/root/PopoverRoot.tsx:216-221`, `navigation-menu/root/NavigationMenuRoot.tsx:449-453`, `utils/popups/popupStoreUtils.ts:228-236`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing". Docs: CHANGELOG.md v1.9.0 "Decouple automatic unmounting from `actionsRef`. Call `eventDetails.preventUnmountOnClose()` in `onOpenChange` to opt out of automatic unmounting (#5734)".

### 3.2 Shared state field and data attributes

| Item | Value | Meaning | Req |
|---|---|---|---|
| `state.transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Present on the state object handed to `className`/`style`/`render` functions of every animated part. | TRN-API-07 |
| `data-starting-style` | `""` | Present while `transitionStatus === 'starting'`. Official description: "Present when the component begins animating in." | TRN-DOM-01 |
| `data-ending-style` | `""` | Present while `transitionStatus === 'ending'`. "Present when the component is animating out." | TRN-DOM-02 |
| `data-instant` | string (see TRN-DOM-10) | Present when the popup's current change should be instant; the value names the cause. | TRN-DOM-10 |
| `globalThis.BASE_UI_ANIMATIONS_DISABLED` | `boolean` | Declared in `global.d.ts:6`: "When `true`, disables animation-related code, even if supported by the runtime environment." Not documented publicly; used by the test harness. | TRN-EDGE-01 |

- **TRN-API-07** — MUST. Every part that emits `data-starting-style`/`data-ending-style` exposes the same value as `transitionStatus` on its state object, so consumer `className`/`style`/`render` functions observe the phase (including `'idle'`/unset, which have no attribute). Server: see ADR 01 relaxation table.
  Source: `tabs/panel/TabsPanel.tsx:55-60,117-126`, `popover/popup/PopoverPopup.tsx:76-82`, `checkbox/indicator/CheckboxIndicator.tsx:34-37`. Test: [L2] `popover/popup/PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted" (reads `state.transitionStatus` through `render`).

### 3.3 Types

| Type | Definition | Source |
|---|---|---|
| `TransitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | `internals/useTransitionStatus.ts:6` |

### 3.4 Handbook guarantees

The animation handbook (`docs/src/app/(docs)/react/handbook/animation/page.mdx`) makes these promises; each is a requirement below.

| Handbook statement (lines) | Requirement |
|---|---|
| `[data-starting-style]` is "the initial style to transition from"; `[data-ending-style]` "the final style to transition to" (12-13) | TRN-DOM-01, TRN-DOM-02, TRN-STA-03, TRN-STA-04, TRN-STA-06 |
| Transitions can be smoothly cancelled midway: closing before opening finishes animates to the closed state without abrupt changes (15-16) | TRN-STA-09, TRN-STA-11, TRN-ANI-07 |
| `[data-open]` / `[data-closed]` are the CSS-animation hooks (42-43) | PSH (`popupStateMapping`); the interplay is TRN-STA-06 (closed and ending land in the same commit) |
| "When a popup closes, `open` becomes `false` right away, but the popup stays rendered in a closing phase until its closing animation finishes." (81) | TRN-STA-06, TRN-ANI-03 |
| "Base UI detects animations on the popup element. CSS animations are always detected, and so are Motion animations that include `opacity`." (82) | TRN-ANI-04 |
| "Once the animation ends, the popup is removed from the DOM, or hidden if the `<Portal>` has `keepMounted`, and `onOpenChangeComplete(false)` fires." (83) | TRN-ANI-01, TRN-DOM-06, TRN-EVT-02 |
| Manual unmounting: call `preventUnmountOnClose()` in `onOpenChange` when closing, then `unmount()` on `actionsRef` once the animation finishes; "This ends the closing phase, and `onOpenChangeComplete(false)` fires. Whether the popup leaves the DOM is still decided by `keepMounted`." (188-191) | TRN-EVT-07, TRN-EVT-08, TRN-ANI-23 |

## 4. Behavioral requirements

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

* TRN-ANI-03, TRN-ANI-09, TRN-ANI-10 — removal lands in the first render batch after the completion message, about two round trips later; the optimistic convergent write (ADR 04 §2) lets JavaScript set `hidden` at animation end so the element is not visible meanwhile.
* TRN-STA-04, TRN-STA-05, TRN-STA-07, TRN-STA-09 — met on both modes: the `data-starting-style` / `data-ending-style` flips are JS-owned with C# mirroring; listed so the flips are not moved back to C#.
* TRN-API-07 — RND-STA-01 row for mirrored fields (`transitionStatus`): the data attribute is JS-written at frame timing; the C# state field, and so `ClassValue`/`StyleValue`, updates one round trip later.
* TRN-DOM-05 — not a row of the table; covered by ADR 04 Amendments 2026-10-18 (handover at attach): C# renders the initial disabled-transition declarations (`transition: none`, Collapsible's `animation-name: none`) in prerender and the first interactive render, and JavaScript owns `style` from attach on both modes.
* TRN-STA-03, TRN-STA-05, TRN-STA-06, TRN-STA-10 on an element whose twin is already attached — met on both modes. The `transitions` module reads the open state from the attributes C# renders, in the applied render batch: the part's `openSelector`, read in a `MutationObserver` callback before paint (ADR 04 Amendments 2026-10-21). It writes `data-starting-style` / `data-ending-style` in that pass. Listed so the input is not moved back to `update`. (Added 2026-10-21.)
* TRN-STA-06 on `Avatar.Image` without `keepMounted` (AVA-ANI-03) — no attribute C# renders on the `<img>` changes in the commit where the image status leaves `'loaded'`, so `data-ending-style` follows `update`. That is before paint on WebAssembly, and a round trip after that render batch on Server. (Added 2026-10-21.)
* TRN-ANI-33, TRN-ANI-34, TRN-EVT-09 (cancel clause) — the decision point `reveal`, fallback run (ADR 04 Amendments 2026-10-21). On both modes the module removes `data-starting-style` and arms the one-shot motion skip inside the dispatch, so TRN-ANI-32 holds. On Server the request reaches C# a round trip later, and its answer (whether the part is open once the request ran) decides the revert. The revert applies on the next frame, instead of after a 0 ms macrotask and a frame. A `cancel()`, or a request that a controlled consumer ignores, therefore shows the revealed content for about that round trip before `hidden="until-found"` returns. (Added 2026-10-21.)

### 4.1 STA — transition status state machine

The machine has two variables, the **mounted flag** and the **transition status**, driven by one input, the **open state**, and four configuration options: `enableIdleState` (default `false`), `deferEndingState` (default `false`), `animateInitialOpen` (default `false`), and the consumer's ability to set the mounted flag to `false` once exit animations finish (`setMounted(false)`).

```
                       open := true while mounted = false
  ┌─────────────┐  ───────────────────────────────────────►  ┌──────────────────────┐
  │  UNMOUNTED  │  (same commit: mounted := true,            │  MOUNTED · starting  │
  │ mounted=0   │   status := 'starting')                    │  data-starting-style │
  │ status=∅    │                                            │  style: transition:none (popups) │
  └─────────────┘                                            └──────────┬───────────┘
        ▲                                                               │ next frame
        │ consumer: setMounted(false)                                   │ (status := ∅, or 'idle'
        │ on animations finished, or                                    │  when enableIdleState)
        │ unmount action                                                ▼
  ┌─────┴───────────────┐     open := false while mounted       ┌──────────────────────┐
  │  MOUNTED · ending   │ ◄─────────────────────────────────── │  MOUNTED · settled   │
  │  data-ending-style  │  (same commit; next frame when        │  status = ∅ / 'idle' │
  └─────────┬───────────┘   deferEndingState)                   └──────────────────────┘
            │
            │ open := true (reopen during exit, mounted still true)
            └──────────────────────────────────────────────────────────►  MOUNTED · settled
              default machine: no 'starting'; 'ending' cleared next frame
              enableIdleState: status := 'starting' post-commit pre-paint, then 'idle' next frame

  close during 'starting' (open := false before the clearing frame fires):
     → MOUNTED · ending in the same commit (deferEndingState=false); the pending clearing frame is cancelled
```

- **TRN-STA-01** — MUST. The transition status takes exactly one of four values: `'starting'`, `'ending'`, `'idle'`, or unset.
  Source: `internals/useTransitionStatus.ts:6`. Test: [L2] `internals/stateAttributesMapping.test.ts` "emits nothing outside a transition" (covers `'idle'` and unset).

- **TRN-STA-02** — MUST. Initial values: the mounted flag starts as `open && !animateInitialOpen`; the status starts as `'idle'` when `open && enableIdleState`, otherwise unset.
  Source: `internals/useTransitionStatus.ts:23-29`. Test: [L2] `internals/useTransitionStatus.test.tsx` "does not request a frame for an element that mounts already open".

- **TRN-STA-03** — MUST. When the open state becomes `true` while the mounted flag is `false`, the mounted flag becomes `true` and the status becomes `'starting'` in the same commit that first renders the part as open (there is no intermediate commit where the part is open without `data-starting-style`). Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:26-34`. Test: [L2] `internals/useTransitionStatus.test.tsx` "clears the starting status on the next frame after opening"; `popover/popup/PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted" (observed sequence `[{open:true,starting},{open:true,undefined}]`). (L4 for the `data-starting-style` clause)

- **TRN-STA-04** — MUST. With `enableIdleState = false`, a `'starting'` status is cleared to unset on the next frame after the commit that set it. Exactly one frame request is made; it is cancelled if the open state or status changes before it fires. Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:58-73`. Test: [L4] `internals/useTransitionStatus.test.tsx` "clears the starting status on the next frame after opening" (asserts `frames.length === 1`).

- **TRN-STA-05** — MUST. With `enableIdleState = true`, a `'starting'` status becomes `'idle'` on the next frame; additionally, whenever the part is open and mounted with a status other than `'idle'` (for example `'ending'` after a reopen), the status is set to `'starting'` post-commit, pre-paint and then `'idle'` on the next frame. Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:75-91`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "applies data-starting-style while opening"; "keeps exit transitions working after a close is interrupted by reopening".

- **TRN-STA-06** — MUST. With `deferEndingState = false`, when the open state becomes `false` while mounted and the status is not already `'ending'`, the status becomes `'ending'` in the same commit that applies the closed state (so `data-closed` and `data-ending-style` appear together). Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:36-38`. Test: [L2] `test/popupConformanceTests.tsx` "is called on close once the exit animation finishes" (waits for `data-ending-style` right after `open=false`). (L4 for the `data-ending-style` clause)
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host (M4 track 1): L2 port title (the ending status in the write that closes reaches the transitions handle); the `data-ending-style` clause at L4 ("is called on close once the exit animation finishes", witnessed by a recorded `animationstart`).

- **TRN-STA-07** — MUST. With `deferEndingState = true`, the `'ending'` status is applied on the next frame after the closing commit; the frame request is cancelled if the open state, mounted flag or status changes before it fires. During that frame the part is closed but still shows its open styles, which lets the consumer measure it (Collapsible/Accordion capture the panel's expanded size there). Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:44-56`; consumer at `collapsible/panel/useCollapsiblePanel.ts:231-243`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "restores a measured height before applying closing transition styles".

- **TRN-STA-08** — MUST. When the consumer sets the mounted flag to `false` while the open state is `false` and the status is `'ending'`, the status becomes unset in the same commit.
  Source: `internals/useTransitionStatus.ts:40-42`. Test: [L2] `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (…)" (after close and reopen the recorded sequence starts again at `'starting'`, which requires the unset step).

- **TRN-STA-09** — MUST. Reopen during the ending phase (open state becomes `true` while the mounted flag is still `true`), default machine: the part MUST NOT re-enter `'starting'`; the `'ending'` status is cleared to unset on the next frame. The exit transition is therefore reversed by the CSS cascade (`[data-ending-style]` removed) rather than restarted. Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:31,58-73`. Test: [L4] `internals/useTransitionStatus.test.tsx` "clears a stale ending status when reopened before the exit settles"; `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (…)" (final phase: "A reversed exit … must not start a new entry phase or report completion for the canceled close").

- **TRN-STA-10** — MUST. Reopen during the ending phase with `enableIdleState = true`: the status becomes `'starting'` post-commit, pre-paint and `'idle'` on the next frame (TRN-STA-05 applies). Server: see ADR 01 relaxation table.
  Source: `internals/useTransitionStatus.ts:80-82`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "keeps exit transitions working after a close is interrupted by reopening".

- **TRN-STA-11** — MUST. Close during the starting phase (open state becomes `false` before the clearing frame fires), default machine: the status becomes `'ending'` in the closing commit and the pending clearing frame is cancelled; the element never shows a settled open frame.
  Source: `internals/useTransitionStatus.ts:36-38,70-72`. Test: none.

- **TRN-STA-12** — MUST. With `animateInitialOpen = true`, a part whose open state is `true` on its very first render still goes through `'starting'` (its mounted flag starts `false` and TRN-STA-03 fires on the first render).
  Source: `internals/useTransitionStatus.ts:13-15,29`. Test: [L2] `menu/popup/MenuPopupEnterTransition.test.tsx` "plays the enter transition for a submenu whose parent opened instantly", "plays the enter transition for a submenu that is open when its parent opens", "plays the enter transition for a controlled-open submenu when its parent opens" (the Menu condition for `animateInitialOpen` is MNU-ANI-03; "plays the enter transition for a submenu opened by the user" is the ordinary TRN-STA-03 path; the pinned limitation "does not play the enter transition for a submenu when the parent popup is kept mounted" is MNU-ANI-05). Docs: CHANGELOG.md v1.8.0 "Play the enter transition for an initially open submenu (#4383)".

- **TRN-STA-13** — MUST. With `animateInitialOpen = false` (default), a part that mounts already open MUST NOT go through `'starting'`; its status is unset (or `'idle'`) from the first commit. The default machine MUST NOT request a frame for it; with `enableIdleState` one frame is requested that only re-writes `'idle'` (no observable change). Rationale (source comment): content that was open on first render (`defaultOpen` on page load, server-rendered markup) must not animate in.
  Source: `internals/useTransitionStatus.ts:13-15,29,60,75-86`. Test: [L2] `internals/useTransitionStatus.test.tsx` "does not request a frame for an element that mounts already open"; `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (… defaultOpen=true)" (`expectInitialOpen` sees only `{open:true, transitionStatus: undefined}`); `menu/popup/MenuPopupEnterTransition.test.tsx` "does not play the enter transition for a menu that is open on the first render", "does not play the enter transition for a submenu inside a menu that is open on the first render". (L3 for the frame-request clauses)

- **TRN-STA-14** — MUST. Popup roots that keep their status in the popup store expose a derived status that reads `'starting'` whenever `open && !mounted` (the window between the open commit and the store synchronisation), and the stored value otherwise. Consumers reading the status through the store therefore never observe "open but unset" during opening.
  Source: `utils/popups/store.ts:180-183`; store seeding at `utils/popups/popupStoreUtils.ts:598-605`. Test: none. (The store itself is specified in PSH.)
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green through PSH-STA-05's L1 titles on the popup store (the status evaluated at the write that flips the effective open state, `PopupStoreSelectorTests`).

### 4.2 DOM — attributes and styles

- **TRN-DOM-01** — MUST. While the status is `'starting'`, the part's element carries `data-starting-style=""` and no `data-ending-style`.
  Source: `internals/TransitionStatusDataAttributes.ts:4`, `internals/stateAttributesMapping.ts:7,12-14`. Test: [L4] `internals/stateAttributesMapping.test.ts` "emits the transition data attributes".

- **TRN-DOM-02** — MUST. While the status is `'ending'`, the element carries `data-ending-style=""` and no `data-starting-style`.
  Source: `internals/TransitionStatusDataAttributes.ts:9`, `internals/stateAttributesMapping.ts:8,15-17`. Test: [L4] `internals/stateAttributesMapping.test.ts` "emits the transition data attributes".

- **TRN-DOM-03** — MUST. While the status is `'idle'` or unset, neither attribute is present, and the generic state-attribute rule (which would emit `data-transitionstatus="idle"`) MUST NOT apply to this field: the mapping consumes the key and returns nothing.
  Source: `internals/stateAttributesMapping.ts:18`, `internals/getStateAttributesProps.ts:15-21`. Test: [L2] `internals/stateAttributesMapping.test.ts` "emits nothing outside a transition" (the same file's `fieldValidityMapping` block, "emits the validity data attributes" and "emits nothing while validity is unknown", is FFI-DOM-01).

- **TRN-DOM-04** — MUST. Every part whose state object contains `transitionStatus` MUST apply this mapping. In 1.9.0 that is: all popup parts via `popupTransitionStateMapping` (`utils/popupStateMapping.ts:63-70`), Dialog/AlertDialog/Drawer popup, backdrop and viewport (`dialog/utils/stateAttributesMapping.ts:18`), Menu parts (`menu/utils/stateAttributesMapping.ts:16`), Combobox popup/backdrop/clear, Select popup/backdrop/scroll arrows, Toast root, Tabs panel, Checkbox indicator, Radio indicator (`radio/utils/stateAttributesMapping.ts:14`), Accordion panel (the mapping in `accordion/item/stateAttributesMapping.ts:10` is shared by Item, Header, Trigger and Panel, but only `AccordionPanel`'s state carries `transitionStatus`, so Item/Header/Trigger never emit transition attributes), Collapsible root/trigger/panel, Field error, Avatar image, NavigationMenu content, the shared item indicator (`utils/ItemIndicator.tsx:38`).
  Source: the `transitionStatusMapping` imports listed in §6. Test: [L4] per-component "animations" blocks (for example `checkbox/indicator/CheckboxIndicator.test.tsx` "triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount").

- **TRN-DOM-05** — MUST. While the status is `'starting'`, positioners (all anchored popups through `usePositioner`) and the popup element of Tooltip, PreviewCard, Popover, Menu, Combobox, Select and NavigationMenu (plus the NavigationMenu arrow) carry the inline style `transition: none`. It is merged *before* the consumer's own props, so a consumer inline `transition` wins. The style disappears together with `data-starting-style` on the next frame, which is when the enter transition actually starts. Dialog/Drawer popups and Toast do not apply it. Server: see ADR 04 Amendments (handover at attach).
  Source: `internals/getDisabledMountTransitionStyles.ts:5-9`, `internals/constants.ts:5`, `utils/usePositioner.tsx:35-39`, `popover/popup/PopoverPopup.tsx:101`, `tooltip/popup/TooltipPopup.tsx:69`, `preview-card/popup/PreviewCardPopup.tsx:65`, `menu/popup/MenuPopup.tsx:176`, `combobox/popup/ComboboxPopup.tsx:101`, `select/popup/SelectPopup.tsx:471`, `navigation-menu/popup/NavigationMenuPopup.tsx:64`, `navigation-menu/arrow/NavigationMenuArrow.tsx:39`; see [floating-and-positioning](floating-and-positioning.md) for `usePositioner`. Test: none. Docs: CHANGELOG.md v1.0.0 "Fix mount transitions on `Positioner` in Firefox (#3424)".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 04 Amendments 2026-10-28, the starting-style `transition: none` in the `transitions` module's layer, settled by the orchestrator as gap 13's option (b)): as built, a part that this row lists sets `TransitionsOptions.DisableTransitionWhileStarting`, and the `transitions` module writes `transition: none` into its `transitions` band of `dom`'s layered writer while the shown status is `'starting'`, under the consumer band (a consumer inline `transition` wins), and removes it with `data-starting-style` on the clearing frame, on both modes; the handed-over style is the writer's consumer band. C# renders the declaration only until attach. Popover's Popup sets the flag; a positioner's declaration is `positioning`'s (FLT-ANI-05); Tooltip and PreviewCard set it at their next `main` merge; Dialog, Drawer and Toast do not. L3: `transitions.test.ts` "writes transition: none while starting when the part asks, under the consumer's style, and removes it on the clearing frame".

- **TRN-DOM-06** — MUST. A kept-mounted container element that is not mounted MUST carry the boolean `hidden` attribute, and while closed (open state `false`, even if still mounted) MUST be non-interactive (`pointer-events: none` inline, or `inert`). The table lists the element that receives `hidden` and the exact predicate:

  | Part | `hidden` when | Non-interactive when | Source |
  |---|---|---|---|
  | Anchored positioner (Popover, Menu, Tooltip, PreviewCard, Combobox, Select, NavigationMenu) | `!mounted` | `pointer-events: none` when `!open` | `utils/usePositioner.tsx:24-36`, `popover/positioner/PopoverPositioner.tsx:122-123` |
  | `Dialog.Popup` / `AlertDialog.Popup` | `!mounted` | FloatingFocusManager (FLT) | `dialog/popup/DialogPopup.tsx:81` |
  | `Dialog.Viewport` | `!mounted` | `pointer-events: none` when `!open` | `dialog/viewport/DialogViewport.tsx:52-55` |
  | `Drawer.Popup` | `!mounted` | — | `drawer/popup/DrawerPopup.tsx:368` |
  | `Tabs.Panel` | `!mounted` | `inert` when `!open`, `tabIndex=-1` | `tabs/panel/TabsPanel.tsx:51,70,73-74` |
  | `Collapsible.Panel` / `Accordion.Panel` | `!open && !mounted` | — | `collapsible/panel/useCollapsiblePanel.ts:74,469` |
  | `NavigationMenu.Content` | `keepMounted && !mounted` | `inert` while closing unless focus is inside | `navigation-menu/content/NavigationMenuContent.tsx:127-136` |

  Source: per row above; the shared predicate lives in `utils/usePositioner.tsx:24-36` and `dialog/viewport/DialogViewport.tsx:52-55`. Test: [L2] `test/popupConformanceTests.tsx` "hides the kept-mounted popup once the exit animation finishes" (popup `toBeInaccessible` only after the animation); `navigation-menu/content/NavigationMenuContent.test.tsx` "keeps the content mounted (hidden) in the DOM when keepMounted is true". (L4 for the after-exit-animation timing of the conformance citation)
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host (M4 track 1): the popup clause: a kept-mounted popup that is not mounted carries `hidden`, written by the module as its exit ends (`HiddenWhenUnmounted`) and by the part's next render; L4 "hides the kept-mounted popup once the exit animation finishes".

- **TRN-DOM-07** — MUST. Kept-mounted *indicator-style* parts (Checkbox/Radio indicators, Menu checkbox/radio item indicators, Select/Combobox item indicators, Avatar image, Select scroll arrows, Combobox clear) MUST NOT receive `hidden` or any inline style when not mounted; the element simply stays in the DOM without `data-starting-style`/`data-ending-style`, and visibility is left to the consumer's CSS on the part's other state attributes (`data-unchecked`, `data-visible`, `data-loading`, …).
  Source: `checkbox/indicator/CheckboxIndicator.tsx:58-71`, `radio/indicator/RadioIndicator.tsx:37-60`, `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:48-57`, `avatar/image/AvatarImage.tsx:154`, `select/scroll-arrow/SelectScrollArrow.tsx:145-150`, `combobox/clear/ComboboxClear.tsx:139-144`. Test: [L2] `checkbox/indicator/CheckboxIndicator.test.tsx` "should keep indicator mounted when unchecked"; `combobox/item-indicator/ComboboxItemIndicator.test.tsx` "preserves the kept-mounted element across selection changes". Docs: CHANGELOG.md v1.0.0-beta.5 "Avoid applying `hidden` attribute to indicator elements when they specify `keepMounted` and are invisible (#3228)"; CHANGELOG.md v1.0.0-alpha.6 "Avoid applying `hidden` attr when `keepMounted=true` for indicators (#1329)".

- **TRN-DOM-08** — MUST. A hidden-until-found panel that is hidden (TRN-DOM-06 predicate) MUST carry the literal attribute value `hidden="until-found"`, written directly to the DOM post-commit, pre-paint by an effect that runs only when `hidden` or `hiddenUntilFound` changes (and on mount) and writes the attribute when both are true (the rendering layer only supports a boolean `hidden`). It is not rewritten after other commits while the panel stays hidden; the reveal-revert path of TRN-ALG-07 restores it after a canceled or ignored `beforematch`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:355-366`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "uses `hidden=\"until-found\" to hide panel when true"; "re-collapses the panel when the open state never arrives" (asserts `hidden="until-found"`).

- **TRN-DOM-09** — MUST. While a hidden-until-found panel is hidden and its detected motion type is not a CSS keyframe animation, the panel MUST carry `data-starting-style=""` even though its status is unset, so that the consumer's collapsed starting styles (for example `height: 0`) apply while hidden and the reveal transitions from them.
  Source: `collapsible/panel/useCollapsiblePanel.ts:93-94,466-468`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "expands the panel while beforematch is being dispatched" (expects `data-starting-style` on the closed panel); "does not keep a hidden transition running after a hiddenUntilFound panel closes".

- **TRN-DOM-10** — MUST. The instant marker is emitted as `data-instant` through the default state-attribute rule: absent when the state field `instant` is `undefined`, `data-instant="<value>"` for a string, `data-instant=""` for `true`. Value domains in 1.9.0: Popover popup/viewport `'dismiss' | 'click' | 'focus' | 'trigger-change'`; Menu popup/viewport `'dismiss' | 'click' | 'group' | 'trigger-change'`; Tooltip popup/viewport/arrow `'delay' | 'focus' | 'dismiss'` and Tooltip positioner additionally `'tracking-cursor'`; PreviewCard popup/positioner/viewport `'dismiss' | 'focus'`; NavigationMenu positioner `boolean` (`true` for the first macrotask of an initially open menu). The reason → value assignment is PSH (`applyPopupOpenChange`); `'trigger-change'` is TRN-ANI-28..31; `'delay'`, `'tracking-cursor'` and the NavigationMenu boolean are owned by TIP and NAV.
  Source: `popover/popup/PopoverPopup.tsx:80,148`, `popover/popup/PopoverPopupDataAttributes.ts:33`, `menu/popup/MenuPopup.tsx:147,289`, `tooltip/popup/TooltipPopup.tsx:59,94`, `tooltip/positioner/TooltipPositioner.tsx:82`, `preview-card/popup/PreviewCardPopup.tsx:55,90`, `navigation-menu/positioner/NavigationMenuPositioner.tsx:80,138,206`, `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `tooltip/root/TooltipRoot.test.tsx` "marks the popup as instant when opened by focus, but not when opened by hover"; `menu/popup/MenuPopupEnterTransition.test.tsx` "marks an initially open submenu as instant when its parent opened instantly" (`data-instant="click"`), and the MNU-owned seeding/clearing tests of the same file (MNU-ANI-04: "clears the inherited instant type after the initial open", "clears the inherited instant type when a controlled close interrupts the entry", "clears the inherited instant type when the popup is not initially rendered", "does not mark a closed submenu as instant when it is later opened programmatically"); `navigation-menu/root/NavigationMenuRoot.test.tsx` "suppresses the positioner transition for the first frame while initially open". (L4 for the Tooltip and Popover values, which JavaScript writes)

- **TRN-DOM-11** — MUST. The inline positioning script is a `<script>` element whose text content is the script body and whose `nonce` is the `CSPProvider` nonce (absent when no provider). It is emitted in server-rendered HTML and during the hydration pass only; once hydration has completed the element is removed from the DOM. In client bundles the body is the empty string, so the script never executes on the client; on the server it executes exactly once, from the HTML, before hydration. Which parts render it, under which prop (`renderBeforeHydration`), and what the scripts compute (`--active-tab-*` variables and removal of `hidden` on `Tabs.Indicator`; `--position`, `--start-position`, `--end-position`, `--relative-size` and removal of inline `visibility` on `Slider.Thumb`/`Slider.Indicator`) are TAB and SLD requirements (`tabs/indicator/prehydrationScript.template.js`, `slider/thumb/prehydrationScript.template.js`).
  Source: `internals/PrehydrationScript.tsx:32-48`, `internals/prehydrationScript.stub.ts:6`, `reference/base-ui/packages/react/package.json:112-119` (`#prehydration/*` with `browser` → stub), `internals/csp-context/CSPContext.ts:15-17`; see [shared-utilities](shared-utilities.md) for `CSPProvider`. Test: none. Docs: CHANGELOG.md v1.7.0 "Exclude the prehydration script from client bundles (#5003)"; CHANGELOG.md v1.4.0 "Skip client-only prehydration scripts (#4530)".

### 4.3 ANI — mounting, animation completion, unmounting

**Mount predicate**

- **TRN-ANI-01** — MUST. A part with a `keepMounted` prop renders its element if and only if `keepMounted || mounted` (portals: `mounted || keepMounted`, Combobox portal additionally `|| forceMounted` for inline mode; menu item indicators implement it as the render-element `enabled` flag, which yields no element when `false`). Exception: `Select.ItemIndicator` and `Combobox.ItemIndicator` render iff `keepMounted || selected` — without `keepMounted` they are removed in the deselecting commit with no ending phase; with `keepMounted` the shared indicator plays the ending phase in place.
  Source: `popover/portal/PopoverPortal.tsx:24-27`, `dialog/portal/DialogPortal.tsx:28`, `combobox/portal/ComboboxPortal.tsx:26`, `tabs/panel/TabsPanel.tsx:104-107`, `checkbox/indicator/CheckboxIndicator.tsx:58,67-69`, `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:56`, `internals/useRenderElement.tsx:41-43`, `avatar/image/AvatarImage.tsx:154`, `select/scroll-arrow/SelectScrollArrow.tsx:145`, `combobox/clear/ComboboxClear.tsx:139`, `dialog/viewport/DialogViewport.tsx:42-45`, `navigation-menu/content/NavigationMenuContent.tsx:157`, `select/item-indicator/SelectItemIndicator.tsx:20-23`, `combobox/item-indicator/ComboboxItemIndicator.tsx:20-23`. Test: [L2] `menu/positioner/MenuPositioner.test.tsx` "when keepMounted=true, should keep the content mounted when closed", "when keepMounted=false, should unmount the content when closed"; `checkbox/indicator/CheckboxIndicator.test.tsx` "should keep indicator mounted when unchecked"; `combobox/item-indicator/ComboboxItemIndicator.test.tsx` "mounts only while selected when keepMounted is false".

- **TRN-ANI-02** — MUST. Collapsible/Accordion panels render if and only if `keepMounted || hiddenUntilFound || mounted || open`; the hidden-until-found option therefore forces mounting regardless of `keepMounted`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:461`. Test: [L2] `collapsible/panel/CollapsiblePanel.test.tsx` "does not unmount the panel when true"; `accordion/panel/AccordionPanel.test.tsx` "passes root hiddenUntilFound to closed panels and allows panel overrides".

- **TRN-ANI-03** — MUST. Closing a mounted part keeps it mounted (and therefore in the DOM, carrying `data-ending-style`) until on animations finished (TRN-ALG-03); the mounted flag then becomes `false` in a synchronous commit before the next paint. A closed part with no running animations is unmounted on the next frame after the completion wait was armed, plus one microtask. Server: see ADR 01 relaxation table.
  Source: `internals/useOpenChangeComplete.tsx:15-27`, `internals/useUnmountAfterClose.ts:118-127`, `internals/useAnimationsFinished.ts:75-82,101-110,161`. Test: [L4] `test/popupConformanceTests.tsx` "removes the popup when there is no exit animation defined"; `dialog/root/DialogRoot.test.tsx` "waits for the exit transition to finish before unmounting"; `checkbox/indicator/CheckboxIndicator.test.tsx` "should remove the indicator when the animation finishes".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host through `popupConformanceTests` in `tests/browser/popup.spec.ts`, both modes: "is called on open once the enter animation finishes", "is called on close once the exit animation finishes" (the popup clause; the running animation witnessed by a recorded `animationstart`).

**Animation detection**

- **TRN-ANI-04** — MUST. Running animations are read from the watched element itself with `element.getAnimations()` and no options; animations running only on descendants are not awaited. (The `getFiniteAnimations` helper accepts the standard `{ subtree }` options object; the mount/unmount completion watcher never passes it. Its one `{ subtree: true }` caller, `ScrollArea.Viewport`, uses it only to re-measure thumb geometry after descendant animations finish, not for any open/close completion — scroll-area.md.) CSS transitions, CSS animations and script-created animations attached to the element are all detected; a script animation that creates no `Animation` on the element (for example a transform-only library animation applied through inline style mutation) is not.
  Source: `utils/getFiniteAnimations.ts:4-5`, `internals/useAnimationsFinished.ts:104,116`; non-completion caller `scroll-area/viewport/ScrollAreaViewport.tsx:338-341`. Test: none. Docs: `docs/src/app/(docs)/react/handbook/animation/page.mdx` lines 82 and 188 ("Use [manual unmounting] when Base UI can't detect your closing animation, for example when it doesn't animate `opacity`").

- **TRN-ANI-05** — MUST. Animations whose effect timing has `duration === Infinity` or `iterations === Infinity` are excluded from the wait (so a looping spinner inside the popup does not block closing). An element whose only animations are infinite completes as if it had none.
  Source: `utils/getFiniteAnimations.ts:5-8`. Test: [L3] `internals/useAnimationsFinished.test.tsx` "ignores infinite animations when a finite animation completes via %s" (2 variants: `finish`, `cancel`); "finishes when the element only has an animation with an infinite %s" (2 variants: `iteration count`, `duration`).

- **TRN-ANI-06** — MUST. The wait completes when every finite animation present at inspection time has finished; completion runs in the microtask following the last `finished` resolution.
  Source: `internals/useAnimationsFinished.ts:101-110`. Test: [L3] `internals/useAnimationsFinished.test.tsx` "ignores infinite animations when a finite animation completes via %s" (`finish` variant).

- **TRN-ANI-07** — MUST. If any awaited animation is cancelled (its `finished` promise rejects), the element is re-inspected: if any finite animation is now pending or not in the `finished` play state, the wait restarts on the current set (this covers a transition replaced by a new one when the animated property changes mid-flight, including an interrupted exit reversed into an enter); otherwise completion runs immediately.
  Source: `internals/useAnimationsFinished.ts:111-130`. Test: [L3] `internals/useAnimationsFinished.test.tsx` "waits for a replacement animation after an animation is canceled"; "finishes when a canceled animation has no replacement". Docs: CHANGELOG.md v1.7.0 "Complete popup unmounting after a canceled exit transition (#5401)".

- **TRN-ANI-08** — MUST. A wait is cancellable through an abort signal: once aborted, the callback MUST NOT run, the restart of TRN-ANI-07 MUST NOT happen, and a pending attribute observer (TRN-ANI-12) is disconnected. Every consumer aborts its pending wait whenever the open state flips or the part unmounts, so a reopen during closing never produces a close completion.
  Source: `internals/useAnimationsFinished.ts:87,107,112-114,157`, `internals/useOpenChangeComplete.tsx:20-26`. Test: [L3] `internals/useAnimationsFinished.test.tsx` "commits each callback separately by default so later callbacks observe earlier updates"; "skips a callback whose signal aborts while the batch is flushing". (L2 twin contract for the consumer-abort sentence)

- **TRN-ANI-09** — MUST. By default each completion callback runs inside its own synchronous commit (the DOM change caused by the callback, typically removal, lands in the same microtask as the animation's completion and is never painted as an intermediate frame). Later completions in the same microtask observe the state changes of earlier ones. Server: see ADR 01 relaxation table.
  Source: `internals/useAnimationsFinished.ts:75-82`. Test: [L4] `internals/useAnimationsFinished.test.tsx` "commits each callback separately by default so later callbacks observe earlier updates".

- **TRN-ANI-10** — MUST. With the `batch` option, completions that become ready in the same microtask are coalesced into a single synchronous commit; each callback re-checks its abort signal at flush time. Only consumers whose callback does not read state another completion can change opt in (the five indicator parts, §6). Server: see ADR 01 relaxation table.
  Source: `internals/useAnimationsFinished.ts:11-34,85-90`. Test: [L4] `internals/useAnimationsFinished.test.tsx` "batches opted-in callbacks that finish in the same microtask into a single commit"; `checkbox/indicator/CheckboxIndicator.test.tsx` "removes all indicators in a single commit when multiple checkboxes are unchecked". Docs: CHANGELOG.md v1.8.0 "Reduce animation completion work (#5535)".

- **TRN-ANI-11** — MUST. When not waiting for the starting attribute (TRN-ANI-12), the first inspection of the element's animations happens on the next frame after the wait was armed, so that transitions/animations triggered by the commit that armed the wait are registered by the browser first.
  Source: `internals/useAnimationsFinished.ts:161`. Test: none. (Collapsible adds a second frame for the same reason, `collapsible/panel/useCollapsiblePanel.ts:302-339`; Docs: CHANGELOG.md v1.0.0-beta.5 "Fix flaky exit transition (#3101)".)

- **TRN-ANI-12** — MUST. When waiting for the starting attribute (the *open* completion): if the element carries `data-starting-style` when the wait is armed, the first inspection happens in the microtask that delivers the attribute's removal (attribute mutation observation, which reports changes as a microtask after the mutation; the removal itself is next frame per TRN-STA-04); otherwise the first inspection happens on the next frame.
  Source: `internals/useAnimationsFinished.ts:134-159`, `internals/useOpenChangeComplete.tsx:13` (passes `open` as the flag). Test: [L4] `popover/popup/PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted" (completion `true` only after the enter transition that starts when `starting` clears); `test/popupConformanceTests.tsx` "is called on open once the enter animation finishes". Docs: CHANGELOG.md v1.1.0 "Fix `onOpenChangeComplete(true)` timing (#3558)".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host through `popupConformanceTests` in `tests/browser/popup.spec.ts`, both modes: "removes the popup when there is no exit animation defined", "hides the kept-mounted popup once the exit animation finishes" (the popup clauses); the Popover title stays open to M4 track 5.

- **TRN-ANI-13** — MUST. Arming a new wait on the same watcher cancels a previously armed but not yet started (frame-deferred) inspection. An inspection already in progress, and a pending attribute observation (TRN-ANI-12), are not cancelled by a new request; only their abort signal stops them.
  Source: `internals/useAnimationsFinished.ts:66,145-157`, `packages/utils/src/useAnimationFrame.ts:127-140`. Test: none.

- **TRN-ANI-14** — MUST. If the watched element does not exist when a wait is armed, nothing is scheduled and the callback never runs. Consumers that can lose their element mid-close MUST complete closing themselves (Collapsible does: a missing panel element during the ending phase unmounts in the same pass).
  Source: `internals/useAnimationsFinished.ts:68-71`; `collapsible/panel/useCollapsiblePanel.ts:313-320`. Test: [L2] `collapsible/panel/CollapsiblePanel.test.tsx` "finishes closing when the render function removes the panel element". (L3 for the watcher clause)

- **TRN-ANI-15** — MUST. If the element has no `getAnimations` function, or the animations-disabled flag is `true`, the callback runs synchronously and immediately when the wait is armed (no frame, no microtask, no synchronous-commit wrapper).
  Source: `internals/useAnimationsFinished.ts:93-99`, `global.d.ts:6`. Test: [L3] `test/popupConformanceTests.tsx` "is called on open when there is no enter animation" / "is called on close when there is no exit animation" (run with the flag set); `tabs/panel/TabsPanel.test.tsx` "triggers enter animation via data-starting-style when mounting" (flag cleared).

- **TRN-ANI-16** — MUST NOT. The mechanism does not consult `prefers-reduced-motion` or any other media query. A consumer stylesheet that removes transitions under reduced motion yields zero finite animations, so completion follows the no-animation path (TRN-ANI-03 timing). A port MUST NOT add a reduced-motion special case that changes timing or attribute emission.
  Source: absence of any `matchMedia`/`prefers-reduced-motion` reference in the files listed in the header. Test: none.

**Open-change completion**

- **TRN-ANI-17** — MUST. An open-change completion watcher, while enabled, arms a wait (TRN-ALG-03) on every change of the open state and when it first becomes enabled, including on initial mount; the previously armed wait is aborted first. Arming happens after paint (or post-commit for discrete user events; only the relative ordering is normative). The wait passes the open state as the "wait for starting attribute" flag, so an open completion waits for `data-starting-style` to clear (TRN-ANI-12) and a close completion inspects on the next frame (TRN-ANI-11).
  Source: `internals/useOpenChangeComplete.tsx:9-27`. Test: [L4] `test/popupConformanceTests.tsx` "is called on open once the enter animation finishes".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host through `popupConformanceTests` in `tests/browser/popup.spec.ts`, both modes: "hides the kept-mounted popup once the exit animation finishes" (`hidden` written by the module as the exit ends, `HiddenWhenUnmounted`, and by the part's next render).

- **TRN-ANI-18** — MUST. Automatic unmount-after-close is enabled only while `mounted && !open && !preventUnmountingOnClose`; on completion, if the open state is still `false`, the part is unmounted (TRN-ALG-05). A completion that arrives after a reopen (open state `true`) is ignored.
  Source: `internals/useUnmountAfterClose.ts:118-127`. Test: [L2] `test/popupConformanceTests.tsx` "is called on close once the exit animation finishes"; `collapsible/panel/CollapsiblePanel.test.tsx` "does not restart the entrance transition when a close animation finishes after reopening".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host (M4 track 1): the popup clause at L2 (an exit completion after a reopen is ignored, `PopupTransitionsTests`) and L4 ("is called on close once the exit animation finishes").

- **TRN-ANI-19** — MUST. The unmount cleanup (`onUnmount`) runs exactly once per unmount, whether automatic or through the unmount action; calling the unmount action after the part is already unmounted is a no-op and MUST NOT repeat the completion callback.
  Source: `internals/useUnmountAfterClose.ts:82-90,102-105`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once"; `popover/root/PopoverRoot.test.tsx` "does not repeat closing completion when `unmount` is called after the automatic unmount".

- **TRN-ANI-20** — MUST. The unmount action invoked while the open state is `true` is deferred until the current batch of updates commits: if the committed state is closed, the part is unmounted post-commit, pre-paint; otherwise the call is dropped (no cleanup, no completion callback) and the part stays open. This makes `close(); unmount()` in one event handler work while a stale exit-animation callback after a quick reopen does nothing.
  Source: `internals/useUnmountAfterClose.ts:92-116`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "ignores `unmount` while the popup is open"; "unmounts when `close` and `unmount` are called in one batch"; "still unmounts on a later close after `unmount` was called while open"; "still unmounts on a later close after `unmount` and a reopen in one batch".

- **TRN-ANI-21** — MUST. Two unmount-action calls in one batch complete closing once (the mounted flag is mirrored synchronously so the second call sees "already unmounted").
  Source: `internals/useUnmountAfterClose.ts:78-90,102-105`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "completes closing once when `unmount` is called twice in one batch".

- **TRN-ANI-22** — MUST. The unmount action invoked while the part is closed and still animating out unmounts it in the same commit; the still-running animation is ignored and its later completion produces no second callback (its wait is aborted by the unmount).
  Source: `internals/useUnmountAfterClose.ts:102-116`, `internals/useOpenChangeComplete.tsx:24-26`. Test: [L2] `navigation-menu/root/NavigationMenuRoot.test.tsx` "unmounts immediately when called while the popup is closing".

- **TRN-ANI-23** — MUST. When the current close cycle opted out (TRN-EVT-07), automatic unmounting is suspended: the part stays mounted with `data-ending-style` and no completion callback fires until the unmount action is called, which then unmounts it and fires `onOpenChangeComplete(false)` once. Whether the element leaves the DOM afterwards is decided by `keepMounted` (TRN-ANI-01).
  Source: `internals/useUnmountAfterClose.ts:71,119`, `utils/popups/popupStoreUtils.ts:195-201`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing"; `select/root/SelectRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing". Docs: `docs/src/app/(docs)/react/handbook/animation/page.mdx` §"Manual unmounting".

- **TRN-ANI-24** — MUST. The opt-out belongs to one close cycle: when the open state becomes `true` again (by any route, including a controlled prop change that never calls `onOpenChange`), the stored opt-out is cleared before the next close is evaluated, so a later ordinary close unmounts automatically.
  Source: `internals/useUnmountAfterClose.ts:66-76`, `utils/popups/popupStoreUtils.ts:196-198`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "clears the opt-out when a controlled reopen interrupts a pending unmount"; `popover/root/PopoverRoot.test.tsx` "unmounts on a normal close after preventUnmountOnClose and external reopen"; `menu/root/MenuRoot.test.tsx` "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen". Docs: CHANGELOG.md v1.6.0 "Reset preventUnmountOnClose on reopen (#4885)".

- **TRN-ANI-25** — MUST. An opt-out recorded during an `onOpenChange` call that is then cancelled (`cancel()`) has no effect: the opt-out is read only when the close is applied.
  Source: `utils/popups/popupStoreUtils.ts:271-289`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "ignores an opt-out on a canceled close".

- **TRN-ANI-26** — MUST. An opt-out recorded while a controlled close is still pending (the consumer applies `open=false` later, for example in a low-priority transition) MUST survive until that close commits: the clearing of TRN-ANI-24 happens only when the open state actually changes to `true`, not on every render that still sees `open=true`.
  Source: `internals/useUnmountAfterClose.ts:66-76`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "keeps the opt-out when a controlled close is applied in a transition"; `popover/root/PopoverRoot.test.tsx` "keeps the opt-out when a controlled close is applied in a transition".

- **TRN-ANI-27** — MUST. For store-backed popups the unmount cleanup, in order: clears the active trigger (`activeTriggerId`, `activeTriggerElement` → `null`), sets `mounted=false` and `preventUnmountingOnClose=false` in the store, runs the component's own cleanup, then fires `onOpenChangeComplete(false)`. Select, Combobox and NavigationMenu follow the same order with their own cleanup (reset highlighted index / scroll arrows / return focus) before the callback.
  Source: `utils/popups/popupStoreUtils.ts:586-595`, `select/root/SelectRoot.tsx:206-214`, `combobox/root/AriaCombobox.tsx:952-960`, `navigation-menu/root/NavigationMenuRoot.tsx:133-157`; see [popup-store-and-handles](popup-store-and-handles.md). Test: [L2] none specific (ordering); `autocomplete/root/AutocompleteRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once" (count).

**Trigger switch while open (`data-instant="trigger-change"`)**

- **TRN-ANI-28** — MUST. When a popup that supports detached triggers (Popover, Menu) is open and its active trigger element changes to a different element, the popup's instant marker is cleared post-commit, pre-paint in the commit that applies the new trigger (so the positioner may transition to the new anchor), and on animations finished for the positioner (next-frame inspection, TRN-ANI-11) the marker is set to `'trigger-change'` provided the popup is still open.
  Source: `internals/useTriggerSwitchTransition.ts:74-111`, call sites `popover/positioner/PopoverPositioner.tsx:91-96`, `menu/positioner/MenuPositioner.tsx:229-234`. Test: [L4] `popover/root/PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant after switching back to the original trigger" (waits for `data-instant="trigger-change"` after the switch); `test/viewportConformanceTests.tsx` "keeps the latest transition active during rapid trigger changes". Docs: CHANGELOG.md v1.9.0 "Fix hover-out animations when switching between detached triggers (#4261)".

- **TRN-ANI-29** — MUST. When the popup closes (by any route, including a controlled `open={false}`), a pending `'trigger-change'` restoration is aborted and a stored `'trigger-change'` value is cleared post-commit, pre-paint; additionally the store's public `instantType` reads `undefined` instead of `'trigger-change'` whenever the popup is closed. The exit transition therefore never sees `data-instant="trigger-change"`.
  Source: `internals/useTriggerSwitchTransition.ts:56-72`, `popover/store/PopoverStore.ts:54-57`, `menu/store/MenuStore.ts:120-123`. Test: [L4] `popover/root/PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers"; "does not apply the trigger-change instant to a non-hover close after switching triggers"; "does not restore the trigger-change instant when a controlled close commits late"; "does not apply the trigger-change instant to a prop-driven close after a switch"; "does not restore the trigger-change instant after a reopen on the same trigger"; `menu/root/MenuRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant when a controlled close commits late".

- **TRN-ANI-30** — MUST NOT. A trigger element change while the popup is closed (for example a trigger remounting) MUST NOT start a switch transition or schedule a restoration.
  Source: `internals/useTriggerSwitchTransition.ts:86-92`. Test: none. Docs: CHANGELOG.md v1.9.0 "Don't start a viewport transition when the trigger remounts while closed (#5887)".

- **TRN-ANI-31** — MUST. If the positioner's animations finish in the same frame in which a close commits, the restoration MUST NOT write `'trigger-change'` (checked against the live open state at completion time).
  Source: `internals/useTriggerSwitchTransition.ts:99-106`. Test: [L4] `menu/root/MenuRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant after a hover close has started"; `popover/root/PopoverRoot.detached-triggers.test.tsx` (same title).

**Hidden until found / reveal event**

- **TRN-ANI-32** — MUST. On a reveal event (`beforematch`) on a hidden-until-found panel: `onOpenChange(true, details)` is called with `reason: 'none'` and the native event; if not cancelled, `data-starting-style` is removed from the panel synchronously inside the event handler (before the browser measures the match, so the panel is not zero-sized), the open state is set to `true`, and the *next* open cycle skips author-defined motion once: during its starting phase the panel's `transition-duration` (CSS-transition panels) or `animation-duration` (keyframe panels) is forced to `0s` inline, the panel is reported `'idle'` in that same pass, and the inline override is restored before the first subsequent close is analysed.
  Source: `collapsible/panel/useCollapsiblePanel.ts:421-448,188-228,162-168`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "expands the panel while beforematch is being dispatched"; "uses `hidden=\"until-found\" to hide panel when true"; "restores the transition duration before the first close after a beforematch open"; "keeps the temporary zero animation duration until the panel closes"; "does not suppress a later animated open after a no-motion beforematch open". Docs: CHANGELOG.md v1.9.0 "Expand `hiddenUntilFound` panels synchronously on `beforematch` (#5514)".

- **TRN-ANI-33** — MUST. If the consumer cancels the reveal (`details.cancel()`), the panel MUST stay hidden: after a 0 ms macrotask and then the next frame, `hidden="until-found"` is re-applied to undo the browser's own removal; the motion-skip of TRN-ANI-32 is not armed. Server: see ADR 01 relaxation table.
  Source: `collapsible/panel/useCollapsiblePanel.ts:426-431,390-417`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "does not open or suppress the next trigger open when beforematch is canceled".

- **TRN-ANI-34** — MUST. If the reveal is not cancelled but the open state never arrives (a controlled consumer ignores the request), then after a 0 ms macrotask and then the next frame the panel is returned to its fully closed presentation: `hidden="until-found"` and (if it had one) `data-starting-style` are restored, and the one-shot motion skip is disarmed so the next ordinary open animates normally. The decision is taken in a macrotask (not a frame) so that a pending open commit is always observed first. Server: see ADR 01 relaxation table.
  Source: `collapsible/panel/useCollapsiblePanel.ts:376-417,443-447`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "re-collapses the panel when the open state never arrives"; "re-collapses a keyframe panel when the open state never arrives".

**Hydration**

- **TRN-ANI-35** — MUST. Content that is already visible when the client takes over server-rendered HTML MUST NOT play an enter transition: parts that mount open start with the mounted flag `true` (TRN-STA-13), and `Avatar.Image` with `keepMounted` additionally promotes an image whose load already completed on the first commit straight to mounted without `'starting'`.
  Source: `internals/useTransitionStatus.ts:29`, `avatar/image/AvatarImage.tsx:85-89`. Test: [L4] `avatar/image/AvatarImage.test.tsx` "does not replay the enter animation for a cached image on hydration"; `collapsible/panel/CollapsiblePanel.test.tsx` "does not replay open transitions when revealing an initially open panel".

- **TRN-ANI-36** — MUST. "Is hydrating" is `true` during the hydration pass of server-rendered markup and `false` for client-only mounts and for every render after hydration. (Implementation detail for the React 17 fallback: both a hydration pass and a client-only mount report "hydrating" for exactly one render.)
  Source: `packages/utils/src/hydration.ts:11-16,27-35,38,44-46`. Test: none.

- **TRN-ANI-37** — MUST. The inline positioning script (TRN-DOM-11) executes exactly once, from the server HTML before the client takes over, and MUST NOT execute again on the client; its element is removed once hydration finishes. Parts that use it accept the pre-positioned state the script produced (they do not re-hide the element or reset the custom properties) and recompute with full precision on their first client layout.
  Source: `internals/PrehydrationScript.tsx:16-21,37-48`, `tabs/indicator/TabsIndicator.tsx:150-158,170`, `slider/thumb/SliderThumb.tsx:199,473-475`, `slider/indicator/SliderIndicator.tsx:62,73`. Test: none.

### 4.4 ARIA / KEY / PTR / FOC / DIS / FRM / POS / A11Y / I18N

Not applicable — this mechanism emits no ARIA attributes, handles no keyboard or pointer input, performs no focus management, and is direction-agnostic. `hidden` and `inert` emission is covered under DOM (TRN-DOM-06). The `beforematch` event is a browser-initiated reveal, documented under ANI.

### 4.5 EVT — completion callbacks and manual unmounting

- **TRN-EVT-01** — MUST. `onOpenChangeComplete(true)` fires once per open phase, from the popup element's completion watcher, on animations finished for the popup element where the inspection begins when `data-starting-style` is removed (next frame after the opening commit) or, if the attribute was already absent when the watcher was armed, next frame after arming; with no animations this resolves in the following microtask. It is guarded by the current open state (`if (open)`), so a wait that completes after a close fires nothing.
  Source: `popover/popup/PopoverPopup.tsx:56-64`, `tooltip/popup/TooltipPopup.tsx:38-46`, `preview-card/popup/PreviewCardPopup.tsx:37-45`, `dialog/popup/DialogPopup.tsx:47-56`, `drawer/popup/DrawerPopup.tsx:296-304`, `menu/popup/MenuPopup.tsx:98-106`, `combobox/popup/ComboboxPopup.tsx:64-72`, `select/popup/SelectPopup.tsx:178-186`. Test: [L4] `test/popupConformanceTests.tsx` "is called on open when there is no enter animation" (calls equal `[[true]]`); "is called on open once the enter animation finishes".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host through `popupConformanceTests` in `tests/browser/popup.spec.ts`, both modes: "is called on open when there is no enter animation", "is called on open once the enter animation finishes".

- **TRN-EVT-02** — MUST. `onOpenChangeComplete(false)` fires once per close phase, from the unmount cleanup (TRN-ANI-27), on animations finished for the popup element or when the unmount action runs. With no exit animation it fires on the next frame after the closing commit plus one microtask.
  Source: `utils/popups/popupStoreUtils.ts:594`, `select/root/SelectRoot.tsx:213`, `combobox/root/AriaCombobox.tsx:953`, `navigation-menu/root/NavigationMenuRoot.tsx:151`. Test: [L4] `test/popupConformanceTests.tsx` "is called on close when there is no exit animation" (calls equal `[[true],[false]]`); "is called on close once the exit animation finishes" (calls equal `[[false]]` after clearing).
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host through `popupConformanceTests` in `tests/browser/popup.spec.ts`, both modes: "is called on close when there is no exit animation", "is called on close once the exit animation finishes".

- **TRN-EVT-03** — MUST NOT. The completion callback is not called when the component mounts closed.
  Source: popups are not rendered while unmounted (TRN-ANI-01); with `keepMounted` the popup's watcher runs with `open=false` and the `if (open)` guard suppresses the call (`popover/popup/PopoverPopup.tsx:60`). Test: [L2] `test/popupConformanceTests.tsx` "is not called on mount when closed".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host (M4 track 1): L2 `PopupTransitionsTests` ("is not called on mount when closed", kept mounted or not) and L4 through the suite.

- **TRN-EVT-04** — MUST. A component that mounts open (`defaultOpen`, controlled `open`) fires `onOpenChangeComplete(true)` once after its (non-animated, per TRN-STA-13) first commit settles.
  Source: `internals/useOpenChangeComplete.tsx:15-27` (runs on first enable). Test: [L4] `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (… defaultOpen=true)" (`expectInitialOpen`: completed exactly once with `true`).

- **TRN-EVT-05** — MUST. Reopening during the exit MUST NOT fire `onOpenChangeComplete(false)` for the cancelled close; `onOpenChangeComplete(true)` fires once the (reversed) animations finish.
  Source: `internals/useOpenChangeComplete.tsx:24-26` (abort on open change), TRN-STA-09. Test: [L4] `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (…)" (final reversed-exit phase).

- **TRN-EVT-06** — SHOULD. `onOpenChangeComplete(false)` is invoked synchronously inside the same task as the unmount: the mounted flag update is queued, the callback runs, and the DOM removal/hiding commits before control returns (and before the next paint). Consumers must not assume the element is already detached when the callback runs.
  Source: `internals/useUnmountAfterClose.ts:86-90` (`setMounted(false)` then `onUnmount()`), `internals/useAnimationsFinished.ts:81` (synchronous commit wrapper). Test: none (see §10).

- **TRN-EVT-07** — MUST. Calling `preventUnmountOnClose()` during a close (`nextOpen === false`) marks the close cycle as opted out (TRN-ANI-23). Calling it during an open has no effect (the flag is reset on open, `utils/popups/popupStoreUtils.ts:196-198`). On Dialog/AlertDialog/Drawer the call writes the store flag directly; on the other roots it is read when the close is applied.
  Source: `utils/popups/popupStoreUtils.ts:228-236,271`, `dialog/store/DialogStore.ts:74-78`, `popover/root/PopoverRoot.tsx:221`, `navigation-menu/root/NavigationMenuRoot.tsx:453`; see [event-details-and-reasons](event-details-and-reasons.md) for the details object. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing"; `navigation-menu/root/NavigationMenuRoot.test.tsx` "keeps the opt-out when focus leaves the trigger during a manual exit".

- **TRN-EVT-08** — MUST. `actionsRef.current.unmount()` is the unmount action of TRN-ALG-05 (`forceUnmount`): it ends the closing phase, runs the unmount cleanup once and fires `onOpenChangeComplete(false)`; it does not change the open state. Roots expose it alongside `close()`, which requests a close with `reason: 'imperative-action'` through `onOpenChange` so the consumer can opt out there.
  Source: `popover/root/PopoverRoot.tsx:74-80,204`, `dialog/root/useRenderDialogRoot.tsx:78-86`, `menu/root/MenuRoot.tsx:484-485`, `select/root/SelectRoot.tsx:320`, `combobox/root/AriaCombobox.tsx:1017-1024`, `tooltip/root/TooltipRoot.tsx:128-135`, `preview-card/root/PreviewCardRoot.tsx:75`, `navigation-menu/root/NavigationMenuRoot.tsx:248`, `internals/reason-parts.ts:40`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out"; `combobox/root/ComboboxRoot.test.tsx` "exposes an unmount action that is ignored while open". (The same Autocomplete block's "does not call `onOpenChange` when the `close` action is called while closed" is the no-op-request rule of CMB-API-07/AUT-EVT-03, not a TRN behavior.)

- **TRN-EVT-09** — MUST. The reveal event requests an open through `onOpenChange(true, details)` with `details.reason === 'none'` and `details.event` set to the `beforematch` event; `details.cancel()` keeps the panel hidden (TRN-ANI-33). Exactly one `onOpenChange` call is made per reveal event. Server: see ADR 01 relaxation table.
  Source: `collapsible/panel/useCollapsiblePanel.ts:421-431`, `internals/reason-parts.ts:1`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "uses `hidden=\"until-found\" to hide panel when true" (`onOpenChange` called once, panel `data-open`); "re-collapses the panel when the open state never arrives" (consumer filters on `reason === 'none'`).

## 5. Algorithms

All pseudo-code is language-neutral and uses the §2.1 timing vocabulary.

- **TRN-ALG-01** — MUST. The implementation reproduces the following procedure exactly (transition status machine):

```
state:
  mounted : bool
  status  : 'starting' | 'ending' | 'idle' | UNSET
options (fixed per part): enableIdleState, deferEndingState, animateInitialOpen
timers: clearFrame, endingFrame, idleFrame   (each holds at most one pending frame)

init(open):
  status  := (open and enableIdleState) ? 'idle' : UNSET
  mounted := open and not animateInitialOpen

on every evaluation (input `open` changed, or `mounted`/`status` changed), before producing output:
  if open and not mounted:
      mounted := true; status := 'starting'          -- lands in the same commit as `open`
  if not open and mounted and status != 'ending' and not deferEndingState:
      status := 'ending'                             -- same commit as `open := false`
  if not open and not mounted and status == 'ending':
      status := UNSET                                -- same commit as setMounted(false)

post-commit, pre-paint, after every commit:
  -- (A) deferred ending
  cancel endingFrame
  if not open and mounted and status != 'ending' and deferEndingState:
      endingFrame := nextFrame(() => status := 'ending')
  -- (B) default settle
  cancel clearFrame
  if open and not enableIdleState and status != UNSET:
      clearFrame := nextFrame(() => status := UNSET)         -- clears 'starting' OR a stale 'ending'
  -- (C) idle settle
  cancel idleFrame
  if open and enableIdleState:
      if mounted and status != 'idle': status := 'starting'  -- post-commit, pre-paint
      idleFrame := nextFrame(() => status := 'idle')

external: setMounted(false)  -- called by the owner on animations finished / unmount action
```

  Note on (B): cancelling and re-arming on every commit means the clearing frame is the first frame after the *last* commit in which the condition held; in practice that is the frame after the opening commit (TRN-STA-04). Note on (C): the `status := 'starting'` write re-runs the block once (status changed), re-arming `idleFrame`; the observable result is one frame of `'starting'` (TRN-STA-05).
  Source: `internals/useTransitionStatus.ts:17-97`. Test: [L2] `internals/useTransitionStatus.test.tsx` (all three tests); `menu/popup/MenuPopupTransitionState.test.tsx` "starts each opening once (…)" (eight configurations). (L4 for the next-frame steps)

- **TRN-ALG-02** — MUST. The implementation reproduces the following procedure exactly (finite animation filter):

```
finiteAnimations(element):
  return [a for a in element.getAnimations()
          if not (a.effect?.getTiming().duration == Infinity
                  or a.effect?.getTiming().iterations == Infinity)]
```

  Source: `utils/getFiniteAnimations.ts:4-8`. Test: [L3] `internals/useAnimationsFinished.test.tsx` "finishes when the element only has an animation with an infinite iteration count" / "… infinite duration".

- **TRN-ALG-03** — MUST. The implementation reproduces the following procedure exactly (wait for animations to finish; this is what "on animations finished" means everywhere in these documents):

```
watcher(elementRef, waitForStartingStyleRemoved = false, batch = false):
  frame := new FrameSlot()                        -- one pending frame per watcher

  run(callback, signal = null):                   -- a null signal counts as "not aborted"
    frame.cancel()                                -- only the pending frame; an armed observer below survives (TRN-ANI-13)
    element := resolve(elementRef)
    if element == null: return                   -- never completes (TRN-ANI-14)

    done():
      if not batch:
          synchronousCommit(callback)            -- own commit, before paint (TRN-ANI-09)
      else:
          enqueueBatched(() => if not signal.aborted: callback())   -- one commit per microtask (TRN-ANI-10)

    if element.getAnimations is not a function or ANIMATIONS_DISABLED:
        callback(); return                       -- synchronous (TRN-ANI-15)

    inspect():
      all(finiteAnimations(element).map(a => a.finished)).then(
        onAllFinished: if not signal.aborted: done(),
        onAnyCancelled:
          if signal.aborted: return
          current := finiteAnimations(element)
          if any(a.pending or a.playState != 'finished' for a in current): inspect()   -- TRN-ANI-07
          else: done())

    if waitForStartingStyleRemoved:
        if not element.hasAttribute('data-starting-style'):
            frame.request(inspect); return       -- next frame
        observe attribute 'data-starting-style' on element:
            on each mutation record, if attribute absent: stop observing; inspect()
                                                 -- mutation-observer microtask after removal (TRN-ANI-12)
        on signal abort: stop observing
        return
    frame.request(inspect)                       -- next frame (TRN-ANI-11)

enqueueBatched(fn):   -- module-wide
  if no batch pending:
      pending := []
      queueMicrotask(() => { list := pending; pending := none; synchronousCommit(() => for f in list: f()) })
  pending.push(fn)
```

  Source: `internals/useAnimationsFinished.ts:11-34,46-163`. Test: [L3] `internals/useAnimationsFinished.test.tsx` (all seven tests). Docs: CHANGELOG.md v1.9.0 "Fix animation waits retaining detached elements (#5719)" (the `.then(NOOP)` that drops fulfilled `Animation` values is an implementation detail of the `all(...)` line).

- **TRN-ALG-04** — MUST. The implementation reproduces the following procedure exactly (open-change completion watcher):

```
openChangeComplete(enabled = true, open, elementRef, batch = false, onComplete):
  waiter := watcher(elementRef, waitForStartingStyleRemoved = open, batch)
  whenever (enabled, open) changes, and once on mount (after paint):
    abort previous controller
    if not enabled: return
    controller := new AbortController()
    waiter.run(onComplete, controller.signal)
  on unmount: abort controller
```

  The consumer's `onComplete` always re-reads the live open state (`if (open)` / `if (!open)`) because a completion can land after a flip but before the abort (TRN-EDGE-05).
  Source: `internals/useOpenChangeComplete.tsx:9-28`. Test: [L4] `test/popupConformanceTests.tsx` "prop: onOpenChangeComplete" block (five tests).
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green on the test-only popup host through `popupConformanceTests` in `tests/browser/popup.spec.ts`, both modes: the completion titles above, the store applying the reported completion (`PopupStore.CompleteTransitionAsync`).

- **TRN-ALG-05** — MUST. The implementation reproduces the following procedure exactly (unmount after close and the unmount action):

```
unmountAfterClose(open, elementRef, preventUnmountOnClose, setPreventUnmountOnClose, onUnmount, animateInitialOpen):
  { mounted, setMounted, status } := transitionMachine(open, false, false, animateInitialOpen)
  preventUnmountingOnClose := open ? false : preventUnmountOnClose     -- derived each evaluation
  post-commit, pre-paint, when open became true: setPreventUnmountOnClose(false)    -- TRN-ANI-24

  mountedMirror := mounted          -- updated synchronously on unmount and re-synced after every commit
  pendingForce := false

  unmount():
    mountedMirror := false
    setMounted(false)               -- queued; commits with the caller's batch
    onUnmount()                     -- store cleanup, then onOpenChangeComplete(false)

  post-commit, pre-paint, after every commit:
    mountedMirror := mounted
    if pendingForce:
        pendingForce := false
        if not open and mounted: unmount()          -- `close(); unmount()` in one batch
        -- otherwise dropped (TRN-ANI-20)

  forceUnmount():                   -- exposed as actionsRef.current.unmount()
    if not mountedMirror: return    -- no-op after unmount (TRN-ANI-19/21)
    if open: pendingForce := true; scheduleRerender(); return
    unmount()

  openChangeComplete(enabled = mounted and not open and not preventUnmountingOnClose,
                     open, elementRef, batch = false,
                     onComplete = () => if not open: forceUnmount())
  return { mounted, status, preventUnmountingOnClose, forceUnmount }
```

  Source: `internals/useUnmountAfterClose.ts:48-130`, `utils/popups/popupStoreUtils.ts:573-608`. Test: [L2] `autocomplete/root/AutocompleteRoot.test.tsx` actions block ("automatically unmounts with an actions ref and completes closing once" through "completes closing once when `unmount` is called twice in one batch"). Docs: CHANGELOG.md v1.9.0 "Decouple automatic unmounting from `actionsRef`. Call `eventDetails.preventUnmountOnClose()` in `onOpenChange` to opt out of automatic unmounting (#5734)".

- **TRN-ALG-06** — MUST. The implementation reproduces the following procedure exactly (trigger-switch transition):

```
triggerSwitchTransition(store, activeTriggerElement, positionerElement, open):
  previousTrigger := null; pending := null
  waiter := watcher(positionerElement)

  post-commit, pre-paint, when `open` changed:                      -- effect 1
    if open: return
    pending?.abort(); pending := null
    if store.rawInstantType == 'trigger-change': store.instantType := UNSET

  post-commit, pre-paint, when activeTriggerElement changed:        -- effect 2 (runs after effect 1)
    current := activeTriggerElement
    prev := previousTrigger
    if current != null: previousTrigger := current
    if prev == null or current == null or current == prev: return
    if not store.open: return                                      -- TRN-ANI-30
    store.instantType := UNSET                                     -- allow the move to animate
    pending := new AbortController()
    waiter.run(() => if store.open: store.instantType := 'trigger-change', pending.signal)
    cleanup (next change of activeTriggerElement, or unmount): pending.abort()

store selector (Popover, Menu): instantType := (raw == 'trigger-change' and not open) ? UNSET : raw
```

  Source: `internals/useTriggerSwitchTransition.ts:49-112`, `popover/store/PopoverStore.ts:54-57`, `menu/store/MenuStore.ts:120-123`. Test: [L4] `popover/root/PopoverRoot.detached-triggers.test.tsx` `trigger-change` tests (seven, each cited under TRN-ANI-28, TRN-ANI-29 or TRN-ANI-31); `menu/root/MenuRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers", "does not restore the trigger-change instant when a controlled close commits late", "does not restore the trigger-change instant after a hover close has started".

- **TRN-ALG-07** — MUST. The implementation reproduces the following procedure exactly (reveal-event handling for hidden-until-found panels):

```
state: skipNextOpenMotion := false; decisionTask; revertFrame

on 'beforematch' at panel:
  details := changeEventDetails(reason = 'none', event)
  onOpenChange(true, details)
  if details.isCanceled:
      scheduleRevert(resetSkip = false, restoreStartingStyle = false); return
  skipNextOpenMotion := true
  hadStartingStyle := panel.hasAttribute('data-starting-style')
  panel.removeAttribute('data-starting-style')          -- synchronous, before the browser measures
  setOpen(true)
  scheduleRevert(resetSkip = true, restoreStartingStyle = hadStartingStyle)

scheduleRevert(resetSkip, restoreStartingStyle):
  decisionTask := macrotask(0 ms, () =>
    if latestOpen: return                                -- the open arrived; nothing to undo
    revertFrame := nextFrame(() =>
      if latestOpen: return
      if resetSkip: skipNextOpenMotion := false
      if restoreStartingStyle: panel.setAttribute('data-starting-style', '')
      panel.setAttribute('hidden', 'until-found')))

on the opening pass (open and status == 'starting'), post-commit, pre-paint:
  skip := skipNextOpenMotion; skipNextOpenMotion := false
  if skip and motionType == 'css-transition': inline transition-duration := '0s' (restored on the next close analysis); report status 'idle' now
  if skip and motionType == 'css-animation':  inline animation-duration := '0s' (restored on the next close analysis); report status 'idle' now

post-commit, pre-paint, whenever hidden or hiddenUntilFound changed (and on mount):
  if hidden and hiddenUntilFound: panel.setAttribute('hidden', 'until-found')

on panel teardown (or listener re-registration when onOpenChange/setOpen change):
  cancel decisionTask; cancel revertFrame; remove the 'beforematch' listener
                                                         -- a pending revert never runs
```

  Source: `collapsible/panel/useCollapsiblePanel.ts:368-459` (listener), `:188-228` (opening pass), `:355-366` (attribute write, deps `[hidden, hiddenUntilFound]`), `:452-456` (cleanup). Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` `hiddenUntilFound` block ("expands the panel while beforematch is being dispatched", "re-collapses the panel when the open state never arrives", "re-collapses a keyframe panel when the open state never arrives", "does not open or suppress the next trigger open when beforematch is canceled", "uses `hidden=\"until-found\" to hide panel when true").

- **TRN-ALG-08** — MUST. The implementation reproduces the following procedure exactly (inline positioning script lifecycle):

```
render(script, nonce = cspNonce):
  if not isHydrating(): render nothing            -- after hydration the element is dropped
  render <script nonce={nonce}>{script}</script>  -- server: real body; client bundle: ''
                                                  -- hydration mismatch of the body is suppressed
isHydrating():
  true  during the hydration pass of server-rendered markup
  false for client-only mounts and after hydration
```

  Source: `internals/PrehydrationScript.tsx:32-48`, `packages/utils/src/hydration.ts:11-16,38,44-46`. Test: none.

## 6. Configuration surface used by components

### 6.1 Transition status machine (`useTransitionStatus`)

| Option | Type | Default | Meaning |
|---|---|---|---|
| `open` | boolean | — | The open/visible state driving the machine. |
| `enableIdleState` | boolean | `false` | Report `'idle'` when settled instead of unset; makes reopen-during-exit pass through `'starting'` (TRN-STA-05/10). |
| `deferEndingState` | boolean | `false` | Apply `'ending'` on the next frame after the close instead of in the closing commit (TRN-STA-07). |
| `animateInitialOpen` | boolean | `false` | Play the enter phase for a part that mounts open (TRN-STA-12). |

| Consumer | `open` input | `enableIdleState` | `deferEndingState` | `animateInitialOpen` | Source |
|---|---|---|---|---|---|
| Popup roots via `useOpenStateTransitions` (Popover, Menu, Tooltip, PreviewCard, Dialog, AlertDialog, Drawer) | store `open` | false | false | Menu: `(open ?? defaultOpen) && parent menu status === 'starting'` (submenus that mount during the parent's enter); others `false` | `utils/popups/popupStoreUtils.ts:579-585`, `menu/root/MenuRoot.tsx:146-147,226-233` |
| Select root, Combobox root, NavigationMenu root (own `useUnmountAfterClose`) | `open` | false | false | false | `select/root/SelectRoot.tsx:196-215`, `combobox/root/AriaCombobox.tsx:1005-1015`, `navigation-menu/root/NavigationMenuRoot.tsx:122-158` |
| Collapsible root (and Accordion items through it) | `open` | **true** | **true** | false | `collapsible/root/useCollapsibleRoot.ts:24` |
| `Tabs.Panel` | `value === selectedValue` | false | false | false | `tabs/panel/TabsPanel.tsx:49-50` |
| `Checkbox.Indicator` | `checked \|\| indeterminate` | false | false | false | `checkbox/indicator/CheckboxIndicator.tsx:28-30` |
| `Radio.Indicator` | `checked` | false | false | false | `radio/indicator/RadioIndicator.tsx:26-28` |
| `Menu.CheckboxItemIndicator`, `Menu.RadioItemIndicator` | `item.checked` | false | false | false | `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:27`, `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:27` |
| Shared `ItemIndicator` (Select/Combobox item indicators; only rendered while `keepMounted \|\| selected`, see TRN-ANI-01) | `selected` | false | false | false | `utils/ItemIndicator.tsx:21`, `select/item-indicator/SelectItemIndicator.tsx:20` |
| `Avatar.Image` | `imageLoadingStatus === 'loaded'` | false | false | false | `avatar/image/AvatarImage.tsx:54-55` |
| `Select.ScrollUpArrow` / `ScrollDownArrow` | `visible && openMethod !== 'touch'` | false | false | false | `select/scroll-arrow/SelectScrollArrow.tsx:40,46` |
| `Combobox.Clear` | `visible` | false | false | false | `combobox/clear/ComboboxClear.tsx:71` |
| `Field.Error` | `rendered` (has matching error) | false | false | false | `field/error/FieldError.tsx:57` |
| `Field.Validity` (render-function state only) | `valid === false` | false | false | false | `field/validity/FieldValidity.tsx:24-25` |
| `NavigationMenu.Content` | `popupMounted && value === itemValue`; mounted flag reset to `false` whenever the popup unmounts mid-exit | false | false | false | `navigation-menu/content/NavigationMenuContent.tsx:62,69-75` |
| Toast root | status comes from the toast store (TST); only the completion watcher is used | — | — | — | `toast/root/ToastRoot.tsx:105-113` |

Component-specific overrides of the shared contract (owned by the component docs, listed for completeness): `Avatar.Image` with `keepMounted` reports `'ending'` as unset so no exit transition plays on an element that never leaves (`avatar/image/AvatarImage.tsx:149-151`; AVA); Tooltip forces `instantType='delay'` while ending because a sibling opened or while in the provider's instant phase (`tooltip/root/TooltipRoot.tsx:102-118`; TIP); a submenu under a `keepMounted` parent gets no enter transition when the parent opens, a pinned limitation (`menu/popup/MenuPopupEnterTransition.test.tsx` "does not play the enter transition for a submenu when the parent popup is kept mounted"; MNU).

### 6.2 Animation watcher (`useAnimationsFinished`)

| Option | Type | Default | Meaning |
|---|---|---|---|
| `elementOrRef` | element or DOM handle | — | The element whose animations are awaited. |
| `waitForStartingStyleRemoved` | boolean | `false` | Begin inspecting when `data-starting-style` is removed (TRN-ANI-12). |
| `batch` | boolean | `false` | Coalesce completions in the same microtask into one commit (TRN-ANI-10). |
| `run(fn, signal)` | — | `signal = null` | Arm a wait; `signal` aborts it. |

| Consumer | Element | wait for starting | batch | Purpose | Source |
|---|---|---|---|---|---|
| `useOpenChangeComplete` | caller's element | `= open` | caller's | all completion watchers | `internals/useOpenChangeComplete.tsx:13` |
| `useTriggerSwitchTransition` | positioner | false | false | restore `'trigger-change'` | `internals/useTriggerSwitchTransition.ts:54` |
| Collapsible panel close sequencing | panel | false | false | unmount after exit, one extra frame | `collapsible/panel/useCollapsiblePanel.ts:72,337-339` |
| Popup viewport content morphing (PSH `usePopupViewport`) | current content container | **true** | false | drop previous content after the morph | `utils/usePopupViewport.tsx:101` |
| Popup auto-resize (PSH `usePopupAutoResize`) | popup | **true** | false | measure after enter | `utils/usePopupAutoResize.ts:29` |
| Menu root | popup | false | false | post-exit cleanup (MNU) | `menu/root/MenuRoot.tsx:235` |
| NavigationMenu trigger | popup | false | false | size reset after switch (NAV) | `navigation-menu/trigger/NavigationMenuTrigger.tsx:132` |

### 6.3 Completion watcher (`useOpenChangeComplete`)

| Option | Type | Default | Meaning |
|---|---|---|---|
| `enabled` | boolean | `true` | Whether a wait is armed at all. |
| `open` | boolean | — | Open state; doubles as the "wait for starting attribute" flag. |
| `ref` | DOM handle | — | Element to watch. |
| `batch` | boolean | `false` | See TRN-ANI-10. |
| `onComplete` | `() => void` | — | Completion callback; must re-check live state. |

| Consumer | `enabled` | `open` | `batch` | `onComplete` effect | Source |
|---|---|---|---|---|---|
| Popup parts (Popover, Tooltip, PreviewCard, Dialog, Drawer, Menu, Combobox, Select) | true | `open` | false | `if open: onOpenChangeComplete(true)` | §4.5 TRN-EVT-01 |
| `useUnmountAfterClose` (all popup roots, Select, Combobox, NavigationMenu root) | `mounted && !open && !preventUnmountingOnClose` | `open` | false | `if !open: forceUnmount()` | `internals/useUnmountAfterClose.ts:118-127` |
| NavigationMenu root (second watcher on the viewport target element) | same predicate | `open` | false | `if !open: forceUnmount()` | `navigation-menu/root/NavigationMenuRoot.tsx:161-170` |
| `Tabs.Panel` | true | `open` | false | `if !open: setMounted(false)` | `tabs/panel/TabsPanel.tsx:83-91` |
| `Checkbox.Indicator`, `Radio.Indicator`, `Menu.CheckboxItemIndicator`, `Menu.RadioItemIndicator`, shared `ItemIndicator` | `!rendered` | `rendered` | **true** | `if !rendered: setMounted(false)` | `checkbox/indicator/CheckboxIndicator.tsx:39-49`, `radio/indicator/RadioIndicator.tsx:46-56`, `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:29-39`, `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:29-39`, `utils/ItemIndicator.tsx:41-51` |
| `Avatar.Image` | `!isVisible` | `isVisible` | false | `if !isVisible: setMounted(false)` | `avatar/image/AvatarImage.tsx:136-145` |
| `Select.ScrollUpArrow` / `ScrollDownArrow` | true | `visible` | false | `if !visible: setMounted(false)` | `select/scroll-arrow/SelectScrollArrow.tsx:63-71` |
| `Combobox.Clear` | true | `visible` | false | `if !visible: setMounted(false)` | `combobox/clear/ComboboxClear.tsx:80-88` |
| `Field.Error` | true | `rendered` | false | `if !rendered: setMounted(false)` | `field/error/FieldError.tsx:103-111` |
| `NavigationMenu.Content` | true | `open` | false | `if !open: setMounted(false)` | `navigation-menu/content/NavigationMenuContent.tsx:77-85` |
| Collapsible panel (open side only) | `open && mounted && status === 'idle'` | `true` | false | `if open: clear measured dimensions` | `collapsible/panel/useCollapsiblePanel.ts:284-300` |
| Toast root | true | `status !== 'ending'` | false | `if ending: remove toast` | `toast/root/ToastRoot.tsx:105-113` |

### 6.4 Unmount-after-close (`useUnmountAfterClose`)

| Option | Type | Default | Meaning |
|---|---|---|---|
| `open` | boolean | — | Open state. |
| `ref` | DOM handle | — | Element whose exit animations are awaited (popup element; Combobox: the popup, or the enclosing `[role="dialog"]` when rendered inline; NavigationMenu: the popup, with a second watcher on the viewport target). |
| `preventUnmountOnClose` | boolean | — | Stored opt-out for the current close cycle. |
| `setPreventUnmountOnClose` | setter | — | Cleared by the mechanism on open. |
| `onUnmount` | `() => void` | — | Runs once per unmount; fires `onOpenChangeComplete(false)`. |
| `animateInitialOpen` | boolean | `false` | Forwarded to the state machine. |

Consumers: `useOpenStateTransitions` for every store-backed popup (`utils/popups/popupStoreUtils.ts:579-596`), `select/root/SelectRoot.tsx:197-215`, `combobox/root/AriaCombobox.tsx:1005-1015`, `navigation-menu/root/NavigationMenuRoot.tsx:123-158`.

### 6.5 Trigger-switch transition (`useTriggerSwitchTransition`)

| Option | Meaning |
|---|---|
| `store` | Popup store exposing raw `instantType`, `set('instantType', …)`, `select('open')`. |
| `domReference` | The active trigger element (floating root context). |
| `positionerElement` | Element whose move animations are awaited. |
| `open` | Open state. |

Consumers: `popover/positioner/PopoverPositioner.tsx:91-96`, `menu/positioner/MenuPositioner.tsx:229-234`. Tooltip and PreviewCard do not use it (their positioners teleport between detached triggers).

### 6.6 Mount-transition suppression (`getDisabledMountTransitionStyles`)

Input: the transition status. Output: `{ style: { transition: 'none' } }` when `'starting'`, otherwise an empty props object. Consumers: see TRN-DOM-05.

### 6.7 `keepMounted` and anchored positioning (FLT-owned, listed for cross-reference)

When a positioner is kept mounted, the floating root context's elements are cleared while `!mounted` so positioning does not run for a closed popup, and anchor auto-update is started only while `mounted` (`internals/useAnchorPositioning.ts:459-470,494,498-500,581-586`; Docs: CHANGELOG.md v1.4.0 "Fix `Positioner` not repositioning to a different trigger when reopened with `keepMounted` (#4407)"). Component documents cite [floating-and-positioning](floating-and-positioning.md) for this.

## 7. Edge cases and dev warnings

- **TRN-EDGE-01** — MUST. When the global flag `BASE_UI_ANIMATIONS_DISABLED` is `true` (it is absent/`false` in production), every animation wait completes synchronously (TRN-ANI-15). The Base UI suite sets it to `true` globally and after every test; individual "animations" tests clear it. A port's test harness needs an equivalent switch to run translated tests.
  Source: `global.d.ts:6`, `internals/useAnimationsFinished.ts:93-99`, `reference/base-ui/test/setupVitest.ts:30-43`. Test: [L4] `internals/useAnimationsFinished.test.tsx` (all tests toggle the flag).

- **TRN-EDGE-02** — MUST. In an environment whose elements lack `getAnimations` (older engines, some test DOMs), completion is synchronous (TRN-ANI-15); `data-ending-style` is therefore present for at most one commit and `onOpenChangeComplete` fires without delay.
  Source: `internals/useAnimationsFinished.ts:94`. Test: [L3] `test/popupConformanceTests.tsx` "is called on close when there is no exit animation" (JSDOM-skipped variants document the browser timing). (L4 for the `data-ending-style` and `onOpenChangeComplete` clause)

- **TRN-EDGE-03** — MUST. If the watched element is missing when a close completes (for example a `render` function that returns nothing while closed), the generic watcher never completes (TRN-ANI-14). Collapsible/Accordion detect this and unmount in the same pass; popups rely on the popup element always existing while mounted. Ports MUST reproduce the Collapsible fallback; the popup case is an open question (§10).
  Source: `collapsible/panel/useCollapsiblePanel.ts:313-320`. Test: [L2] `collapsible/panel/CollapsiblePanel.test.tsx` "finishes closing when the render function removes the panel element". (L3 for the watcher clause)

- **TRN-EDGE-04** — MUST. `hiddenUntilFound` overrides `keepMounted={false}` (TRN-ANI-02). In development, the owning parts warn once when both are set. Messages, verbatim (quoted here once; COL and ACC cite this ID): Collapsible.Panel — "The `keepMounted={false}` prop on `Collapsible.Panel` is ignored when `hiddenUntilFound` is enabled, since the panel must remain mounted while closed."; Accordion.Root — "The `keepMounted={false}` prop on `Accordion.Root` is ignored when `hiddenUntilFound` is enabled, since panels must remain mounted while closed."; Accordion.Panel — "The `keepMounted={false}` prop on an `Accordion.Panel` is ignored when `hiddenUntilFound` is enabled on the panel or root, since the panel must remain mounted while closed."
  Source: `collapsible/panel/CollapsiblePanel.tsx:36-45`, `accordion/root/AccordionRoot.tsx:50-55`, `accordion/panel/AccordionPanel.tsx:61-66`. Test: [L2] `collapsible/panel/CollapsiblePanel.test.tsx` "warns when hiddenUntilFound overrides keepMounted={false}"; `accordion/root/AccordionRoot.test.tsx` "warns when hiddenUntilFound overrides keepMounted={false}"; `accordion/panel/AccordionPanel.test.tsx` "warns when a panel enables hiddenUntilFound and disables keepMounted".

- **TRN-EDGE-05** — MUST. Completion callbacks MUST re-read the live open/visible state at the moment they run: an animation's `finished` promise can resolve after the commit that flipped the state but before that commit's cleanup aborted the wait. Every consumer in §6.3 guards with `if (open)` / `if (!open)`; Collapsible additionally reads the latest open value through a DOM-handle-like reference in its close watcher.
  Source: `internals/useAnimationsFinished.test.tsx:319-329` (comment), `collapsible/panel/useCollapsiblePanel.ts:288-296,325-331`, `internals/useUnmountAfterClose.ts:122-126`. Test: [L2] `internals/useAnimationsFinished.test.tsx` "commits each callback separately by default so later callbacks observe earlier updates"; `collapsible/panel/CollapsiblePanel.test.tsx` "keeps the measured size when an open animation finishes during a close commit". (L3 for the `useAnimationsFinished` citation)

- **TRN-EDGE-06** — MUST. The frame scheduler is process-global: all frame requests issued during one task share a single native frame request, and a cancelled request becomes a no-op slot rather than cancelling the native frame. Observable consequence: two parts that open in the same commit clear `'starting'` in the same frame callback (TRN-STA-04 "next frame" is the same frame for both).
  Source: `packages/utils/src/useAnimationFrame.ts:15-84,109-145`. Test: [L3] `internals/useTransitionStatus.test.tsx` "clears the starting status on the next frame after opening" (asserts a single request).

- **TRN-EDGE-07** — SHOULD. A consumer that defines *both* a CSS transition and a CSS keyframe animation on a Collapsible/Accordion panel receives the development warning "CSS transitions and CSS animations both detected on Collapsible or Accordion panel." "Only one of either animation type should be used." and the transition path is used. (COL-owned diagnostic; mentioned because the shared `data-starting-style`/`data-ending-style` contract supports either style but the Collapsible size-measuring logic needs to know which.)
  Source: `collapsible/panel/useCollapsiblePanel.ts:501-511`. Test: none in this slice (COL).

- **TRN-EDGE-08** — MUST. The `hidden="until-found"` literal is written through the DOM API post-commit, pre-paint because the rendering layer coerces `hidden` to a boolean; after the browser removes `hidden` during a reveal the rendering layer, still rendering the same boolean, will not restore it — hence the explicit revert of TRN-ANI-33/34. A port whose renderer supports string `hidden` values natively still MUST restore the attribute after a cancelled or ignored reveal.
  Source: `collapsible/panel/useCollapsiblePanel.ts:362-365,376-381`. Test: [L4] `collapsible/panel/CollapsiblePanel.test.tsx` "re-collapses the panel when the open state never arrives".

## 8. React-specific notes and porting considerations

Non-normative.

* **"During render" state writes.** `useTransitionStatus` calls `setMounted(true)` / `setTransitionStatus('starting')` *while rendering* (lines 31-42). React re-renders the component before committing, so the DOM only ever sees the final values. A port must compute the mounted flag and status *before* producing output for a given open state (treat TRN-ALG-01's "on every evaluation" block as a pure function of the previous state and the new input, applied before rendering). Port: see decisions/04-js-interop-boundary.md §2 (the `data-starting-style`/`data-ending-style` flips and the popup `transition: none` of TRN-DOM-05 are JS-owned; C# mirrors `transitionStatus` into the state object) and decisions/01-hosting-model-and-timing.md §Relaxation table (TRN-STA-04/05/07/09 met on both modes because the flip is JS-owned).

* **Layout effect vs passive effect.** Frame requests in the state machine and the `hidden="until-found"` write happen in layout effects (post-commit, pre-paint). The completion watcher (`useOpenChangeComplete`) runs in a passive effect, which React flushes after paint for most updates but synchronously post-commit for discrete user events. Only the ordering guarantees in §4 are normative (ending attribute in the closing commit; first inspection on the next frame; completion in a synchronous commit). Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term table, item 4) and decisions/04-js-interop-boundary.md §1 (the `transitions` module owns the animations-finished watcher TRN-ALG-03, the next-frame flips, `hidden="until-found"`/`beforematch` TRN-ALG-07 and the completion notification).

* **`flushSync`.** TRN-ANI-09/10 use `ReactDOM.flushSync` inside a promise microtask so that the unmount commits before the browser paints the frame in which the animation ended (issue #979). The porting requirement is "DOM removal lands in the same microtask as the animation's completion"; how a framework achieves a synchronous commit is up to the port. The batching variant exists because a single `flushSync` per indicator in a list of 10 checkboxes produced 10 commits (issue #5481). Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (TRN-ANI-03/09/10: on Server the removal lands about two round trips after the animation ends) and decisions/04-js-interop-boundary.md §2 (the optimistic convergent write lets JS set `hidden` at exit-animation end).

* **`useStableCallback` / abort controllers.** Callbacks passed to the watcher are identity-stable so effects do not re-run; cancellation is expressed with `AbortController`. A port can use any token/cancellation primitive as long as TRN-ANI-08 holds.

* **StrictMode double effects.** Not relied upon; the `AnimationFrame` instance cleanup (`useOnMount(disposeEffect)`) and the controller aborts make the effects idempotent.

* **Store synchronisation.** Store-backed popups keep `mounted` and `transitionStatus` in their store (`store.useSyncedValues`), seeded on first render so an initially open popup is not mistaken for a reopen (`utils/popups/popupStoreUtils.ts:598-605`). The `transitionStatus` selector (TRN-STA-14) papers over the one-render gap between the open commit and the sync. A port with a single source of truth does not need the selector but must keep the observable sequence of TRN-STA-03. Port: see decisions/05-state-store-and-event-details.md §1.

* **`hidden` as a boolean prop.** React only renders boolean `hidden` (facebook/react#24740); the string value is set imperatively. See TRN-EDGE-08. Port: see decisions/04-js-interop-boundary.md §2 (`hidden="until-found"` on Collapsible/Accordion panels is JS-owned).

* **Pre-hydration script bundling.** The `#prehydration/*` package import map resolves to a stub in browser bundles so the script body is never shipped to the client, while the `<script>` element is still rendered during hydration to keep the server/client trees identical (`suppressHydrationWarning` hides the body difference). A port that does not do SSR can omit TRN-DOM-11/TRN-ANI-37 entirely; a port that does SSR must emit the script once in server HTML and not re-run it.

* **React 17 hydration detection.** `useIsHydrating` is exact on React ≥ 18 (`useSyncExternalStore` server snapshot); on React 17 it reports "hydrating" for one render on every mount. Only the ≥ 18 semantics (TRN-ANI-36) are normative.

* **State → attribute mapping.** `transitionStatusMapping` replaces the generic `data-transitionstatus` projection (TRN-DOM-03). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 4 (`transitionStatusMapping` is a hand-written custom mapping).

* **Tests and the frame scheduler.** The test bootstrap replaces `requestAnimationFrame` with `setTimeout(0)` under JSDOM and resets the global scheduler between tests. Browser-only tests (`describe.skipIf(isJSDOM)`) are the ones that pin real timing. Port: see decisions/06-testing-harness.md §2 (the `transitions` module exposes the TRN-EDGE-01 `BASE_UI_ANIMATIONS_DISABLED` switch as a harness requirement) and §1 (TRN-ANI-15's synchronous path is tested at L3).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TRN-API-01 | MUST | keepMounted keeps element in DOM while not mounted; no effect on open state | 3.1 |
| TRN-API-02 | MUST | keepMounted does not alter status sequence or callbacks; element reused | 3.1 |
| TRN-API-03 | MUST | hiddenUntilFound forces mounting, hidden="until-found", reveal opens; overrides keepMounted=false | 3.1 |
| TRN-API-04 | MUST | onOpenChangeComplete(open) signature, no event details | 3.1 |
| TRN-API-05 | MUST | actionsRef.unmount() is the unmount action; ref may be null | 3.1 |
| TRN-API-06 | MUST | eventDetails.preventUnmountOnClose() exists on onOpenChange/onValueChange details | 3.1 |
| TRN-API-07 | MUST | transitionStatus exposed on the state object of every animated part | 3.2 |
| TRN-STA-01 | MUST | Status domain: starting / ending / idle / unset | 4.1 |
| TRN-STA-02 | MUST | Initial mounted = open && !animateInitialOpen; initial status idle/unset | 4.1 |
| TRN-STA-03 | MUST | Open while unmounted → mounted + 'starting' in the same commit | 4.1 |
| TRN-STA-04 | MUST | 'starting' cleared next frame (default machine); one request, cancellable | 4.1 |
| TRN-STA-05 | MUST | enableIdleState: 'starting' → 'idle' next frame; non-idle open → 'starting' post-commit pre-paint | 4.1 |
| TRN-STA-06 | MUST | Close while mounted → 'ending' in the closing commit (deferEndingState=false) | 4.1 |
| TRN-STA-07 | MUST | deferEndingState: 'ending' next frame after close, cancellable | 4.1 |
| TRN-STA-08 | MUST | setMounted(false) while closed & ending → status unset same commit | 4.1 |
| TRN-STA-09 | MUST | Reopen during exit: no 'starting'; 'ending' cleared next frame | 4.1 |
| TRN-STA-10 | MUST | Reopen during exit with idle state: 'starting' pre-paint, 'idle' next frame | 4.1 |
| TRN-STA-11 | MUST | Close during 'starting' → 'ending' in the closing commit; clearing frame cancelled | 4.1 |
| TRN-STA-12 | MUST | animateInitialOpen: initially open part goes through 'starting' | 4.1 |
| TRN-STA-13 | MUST | Default: initially open part never 'starting', no frame requested | 4.1 |
| TRN-STA-14 | MUST | Store selector reads 'starting' while open && !mounted | 4.1 |
| TRN-DOM-01 | MUST | data-starting-style="" iff 'starting' | 4.2 |
| TRN-DOM-02 | MUST | data-ending-style="" iff 'ending' | 4.2 |
| TRN-DOM-03 | MUST | No attribute for idle/unset; no data-transitionstatus | 4.2 |
| TRN-DOM-04 | MUST | Every part with transitionStatus applies the mapping (list) | 4.2 |
| TRN-DOM-05 | MUST | Inline transition:none during 'starting' on positioners and listed popups | 4.2 |
| TRN-DOM-06 | MUST | Kept-mounted containers: hidden when !mounted; non-interactive when !open (table) | 4.2 |
| TRN-DOM-07 | MUST | Kept-mounted indicators receive no hidden/style | 4.2 |
| TRN-DOM-08 | MUST | hidden="until-found" literal on hidden hidden-until-found panels | 4.2 |
| TRN-DOM-09 | MUST | data-starting-style persisted on hidden hidden-until-found panels (non-keyframe) | 4.2 |
| TRN-DOM-10 | MUST | data-instant emission and value domains | 4.2 |
| TRN-DOM-11 | MUST | Inline positioning <script> with nonce; server + hydration only; empty on client | 4.2 |
| TRN-ANI-01 | MUST | Render iff keepMounted \|\| mounted (item indicators: keepMounted \|\| selected) | 4.3 |
| TRN-ANI-02 | MUST | Collapsible panel renders iff keepMounted \|\| hiddenUntilFound \|\| mounted \|\| open | 4.3 |
| TRN-ANI-03 | MUST | Closing keeps part mounted until on animations finished; no-animation timing | 4.3 |
| TRN-ANI-04 | MUST | Detection via element.getAnimations() on the element only | 4.3 |
| TRN-ANI-05 | MUST | Infinite-duration/iteration animations excluded | 4.3 |
| TRN-ANI-06 | MUST | Complete when all finite animations finish (microtask) | 4.3 |
| TRN-ANI-07 | MUST | Cancelled animation → re-inspect; wait for replacements | 4.3 |
| TRN-ANI-08 | MUST | Abort signal suppresses callback/restart; consumers abort on flip | 4.3 |
| TRN-ANI-09 | MUST | Default: each completion in its own synchronous commit | 4.3 |
| TRN-ANI-10 | MUST | batch: same-microtask completions in one commit; re-check signal | 4.3 |
| TRN-ANI-11 | MUST | First inspection next frame (non-starting wait) | 4.3 |
| TRN-ANI-12 | MUST | Open wait: inspect when data-starting-style removed, else next frame | 4.3 |
| TRN-ANI-13 | MUST | New arm cancels a pending frame-deferred inspection only | 4.3 |
| TRN-ANI-14 | MUST | Missing element → never completes; Collapsible completes itself | 4.3 |
| TRN-ANI-15 | MUST | No getAnimations or flag → synchronous immediate callback | 4.3 |
| TRN-ANI-16 | MUST NOT | No reduced-motion special case | 4.3 |
| TRN-ANI-17 | MUST | Completion watcher arms on each open change and on enable; aborts previous | 4.3 |
| TRN-ANI-18 | MUST | Auto-unmount enabled iff mounted && !open && !optOut; ignores late completion after reopen | 4.3 |
| TRN-ANI-19 | MUST | onUnmount once per unmount; unmount action no-op when already unmounted | 4.3 |
| TRN-ANI-20 | MUST | Unmount action while open deferred to commit: unmount if closed, else dropped | 4.3 |
| TRN-ANI-21 | MUST | Two unmount actions in one batch complete once | 4.3 |
| TRN-ANI-22 | MUST | Unmount action while closing unmounts in the same commit; animation ignored | 4.3 |
| TRN-ANI-23 | MUST | Opt-out suspends auto-unmount until the unmount action | 4.3 |
| TRN-ANI-24 | MUST | Opt-out cleared when open becomes true | 4.3 |
| TRN-ANI-25 | MUST | Opt-out on a cancelled close ignored | 4.3 |
| TRN-ANI-26 | MUST | Opt-out survives a pending controlled close | 4.3 |
| TRN-ANI-27 | MUST | Unmount cleanup order: clear trigger, mounted=false, own cleanup, callback(false) | 4.3 |
| TRN-ANI-28 | MUST | Trigger switch while open: clear instant, restore 'trigger-change' on animations finished | 4.3 |
| TRN-ANI-29 | MUST | Close aborts restoration, clears 'trigger-change'; selector hides it while closed | 4.3 |
| TRN-ANI-30 | MUST NOT | No switch transition while closed | 4.3 |
| TRN-ANI-31 | MUST | Restoration skipped if closed at completion | 4.3 |
| TRN-ANI-32 | MUST | beforematch: onOpenChange(true,'none'), sync attribute drop, open, one-shot motion skip | 4.3 |
| TRN-ANI-33 | MUST | Cancelled reveal: restore hidden after 0 ms macrotask + next frame | 4.3 |
| TRN-ANI-34 | MUST | Ignored reveal: restore hidden + starting-style after 0 ms macrotask + next frame; disarm skip | 4.3 |
| TRN-ANI-35 | MUST | No enter transition for content visible at hydration | 4.3 |
| TRN-ANI-36 | MUST | isHydrating semantics | 4.3 |
| TRN-ANI-37 | MUST | Inline script runs once from server HTML, never on client; element removed after hydration | 4.3 |
| TRN-EVT-01 | MUST | onOpenChangeComplete(true) once per open, on animations finished, guarded by open | 4.5 |
| TRN-EVT-02 | MUST | onOpenChangeComplete(false) once per close, from unmount cleanup | 4.5 |
| TRN-EVT-03 | MUST NOT | Not called on closed mount | 4.5 |
| TRN-EVT-04 | MUST | Initially open component fires (true) once | 4.5 |
| TRN-EVT-05 | MUST | Reopen during exit: no (false); (true) after animations | 4.5 |
| TRN-EVT-06 | SHOULD | (false) invoked synchronously in the unmount task, before removal commits | 4.5 |
| TRN-EVT-07 | MUST | preventUnmountOnClose() during a close marks the cycle; no effect on open | 4.5 |
| TRN-EVT-08 | MUST | actionsRef.unmount() = unmount action; close() goes through onOpenChange | 4.5 |
| TRN-EVT-09 | MUST | Reveal → one onOpenChange(true, reason 'none', event); cancel keeps hidden | 4.5 |
| TRN-ALG-01 | MUST | Transition status machine pseudo-code | 5 |
| TRN-ALG-02 | MUST | Finite animation filter | 5 |
| TRN-ALG-03 | MUST | Animations-finished wait ("on animations finished") | 5 |
| TRN-ALG-04 | MUST | Open-change completion watcher | 5 |
| TRN-ALG-05 | MUST | Unmount after close and unmount action | 5 |
| TRN-ALG-06 | MUST | Trigger-switch transition | 5 |
| TRN-ALG-07 | MUST | Reveal-event handling | 5 |
| TRN-ALG-08 | MUST | Inline positioning script lifecycle | 5 |
| TRN-EDGE-01 | MUST | BASE_UI_ANIMATIONS_DISABLED flag | 7 |
| TRN-EDGE-02 | MUST | No getAnimations → synchronous completion | 7 |
| TRN-EDGE-03 | MUST | Missing element on close: Collapsible fallback | 7 |
| TRN-EDGE-04 | MUST | hiddenUntilFound overrides keepMounted=false; dev warnings (verbatim) | 7 |
| TRN-EDGE-05 | MUST | Completion callbacks re-read live state | 7 |
| TRN-EDGE-06 | MUST | Global frame scheduler: same-task requests share one frame | 7 |
| TRN-EDGE-07 | SHOULD | Transition + animation both defined → COL warning, transition path | 7 |
| TRN-EDGE-08 | MUST | hidden="until-found" restored imperatively after cancelled/ignored reveal | 7 |

Counts: API 7, STA 14, DOM 11, ANI 37, EVT 9, ALG 8, EDGE 8 — 94 requirements, no gaps per `TRN-AREA`.

## 10. Open questions

1. **Missing popup element during closing (TRN-ANI-14 / TRN-EDGE-03).** `internals/useAnimationsFinished.ts:68-71` returns without scheduling when the element is `null`, and `useUnmountAfterClose` has no fallback. If a consumer's `render` function omits the popup element while closed, the popup would stay mounted forever. No test covers this for popups; it is unclear whether this is intended (popup element assumed always present while mounted) or an unhandled case.

2. **Ordering of `onOpenChangeComplete(false)` relative to DOM removal (TRN-EVT-06).** `internals/useUnmountAfterClose.ts:86-90` calls `onUnmount()` (which fires the callback) after queuing `setMounted(false)` but before the synchronous commit completes. No test asserts whether the element is still in the DOM when the callback runs; the requirement is marked SHOULD pending confirmation.

3. **`subtree` animations (TRN-ANI-04).** `utils/getFiniteAnimations.ts:4` accepts `GetAnimationsOptions` but no caller passes `{ subtree: true }`; animations on children (for example a Motion child element) are therefore not awaited. The handbook (line 82) says "Base UI detects animations on the popup element", consistent with this. Confirm the port should also ignore descendant animations.
   Correction (scroll-area.md §16 Q2): `scroll-area/viewport/ScrollAreaViewport.tsx:341` does pass `{ subtree: true }`, but only to recompute thumb geometry; the open/close completion watcher still reads the element only. TRN-ANI-04 updated accordingly.

4. **TRN-STA-14 ownership.** The `transitionStatus` store selector (`utils/popups/store.ts:180-183`) is documented here because it changes the observable status sequence; PSH may prefer to own it. Whichever document keeps it, the other should link.

5. **NavigationMenu first-tick `data-instant` (TRN-DOM-10).** `navigation-menu/positioner/NavigationMenuPositioner.tsx:78-81` emits a boolean `instant` for one 0 ms macrotask on an initially open menu. It is a different mechanism from `getDisabledMountTransitionStyles` (which the NavigationMenu positioner does not use through `usePositioner`). Listed as NAV-owned; NAV should confirm.
   Resolved: navigation-menu.md §16 Q9 confirms the separate mechanism (NAV-ANI-13/14/15, NAV-DOM-28; it also covers 100 ms after a window resize). Correction to the parenthetical above: the NavigationMenu Positioner *does* render through `usePositioner` (`navigation-menu/positioner/NavigationMenuPositioner.tsx:170`) and therefore also receives the shared `'starting'` transition suppression (`utils/usePositioner.tsx:37`); the boolean `instant` is additional to it.

6. **`'tracking-cursor'` and `'delay'` instant values** are produced by Tooltip-specific logic (`tooltip/positioner/TooltipPositioner.tsx:82`, `tooltip/root/TooltipRoot.tsx:102-118`); their timing is left to TIP. The complete `instantType` reason mapping (`'click'`, `'dismiss'`, `'focus'`, `'group'`) is PSH's (`utils/popups/popupStoreUtils.ts:296-301`).
   Correction (second-pass review): `utils/popups/popupStoreUtils.ts:296-301` assigns only `'focus'`, `'dismiss'` and the hover reset to `undefined`; `'click'` is assigned in `popover/store/PopoverStore.ts:176` and `menu/root/MenuRoot.tsx:432`, and `'group'` in `menu/root/MenuRoot.tsx:430` (POP/MNU own those call sites).

7. **Select/Combobox item indicators without `keepMounted` (TRN-ANI-01).** `select/item-indicator/SelectItemIndicator.tsx:20-23` and `combobox/item-indicator/ComboboxItemIndicator.tsx:20-23` unmount the shared `ItemIndicator` in the deselecting commit, so its ending phase and completion watcher only ever run with `keepMounted`. `combobox/item-indicator/ComboboxItemIndicator.test.tsx` "mounts only while selected when keepMounted is false" pins this. Whether the lack of an exit animation without `keepMounted` is intentional (it contradicts the Checkbox/Radio indicator pattern) should be confirmed by SEL/CMB.
   Resolved: confirmed by select.md §16 Q12 (SEL-STR-11, SEL-ANI-12) and combobox.md (CMB-API-61, CMB-ANI-05) — no exit phase without `keepMounted`.

8. **Source files that may belong elsewhere.** `utils/usePopupViewport.tsx` and `utils/usePopupAutoResize.ts` (PSH per conventions §3.2) are the only consumers of `waitForStartingStyleRemoved=true` besides the open completion; `collapsible/panel/useCollapsiblePanel.ts` is COL-owned but hosts the only implementation of the shared `hidden="until-found"` / `beforematch` contract (documented here per the assignment; COL should reference TRN-ANI-32..34, TRN-DOM-08/09 and TRN-ALG-07 rather than restate them).

9. **Server-rendered disabled-transition styles (TRN-DOM-05 vs COL-ANI-14; routed from collapsible.md's review).** TRN-DOM-05 is the only TRN requirement that specifies inline transition-suppression styles (`getDisabledMountTransitionStyles`, `internals/getDisabledMountTransitionStyles.ts:5-9`), and it applies only while the status is `'starting'`. A part that is open in server-rendered markup never enters `'starting'` (TRN-STA-13, TRN-ANI-35), so no TRN ID puts a disabled-transition style into server markup. The `animation-name: none` that COL-ANI-14 requires in server markup for an initially open panel is Collapsible's own one-shot suppression (`collapsible/panel/useCollapsiblePanel.ts:59,76-81`, merged last at `collapsible/panel/CollapsiblePanel.tsx:122`), tested by `collapsible/panel/CollapsiblePanel.test.tsx:873-939` with `renderToString`. Port question: ADR 04 §2, read literally, gives the panel no C#-rendered `style` (its measured `--collapsible-panel-height/width` are JS-owned, and prerender emits no JS-owned attribute), so the prerendered panel would carry no `animation-name: none`. Either (a) C# prerenders the JS-unowned declarations (consumer `style` plus `animation-name: none`) and JavaScript takes over `style` on attach, or (b) COL-ANI-14 is relaxed for the port. Needs a decision recorded against ADR 04 §2. Decided (2026-10-18): C# renders the initial value once and JavaScript owns the attribute from attach (ADR 04 Amendments, handover at attach); COL-ANI-14 / ACC-ANI-03 hold without relaxation.

10. **Server relaxations of 2026-10-21 (M1 track 5, issue 62; ADR 01 relaxation table, rows added 2026-10-21; ADR 04 Amendments 2026-10-21).** Dated 2026-10-21.
   - The "Server relaxations" list before §4.1 gains the three items of ADR 01's new rows:
     - TRN-STA-03/05/06/10 on an attached element, met on both modes by reading the open state from the applied batch;
     - TRN-STA-06 on `Avatar.Image` without `keepMounted`;
     - TRN-ANI-33/34 and TRN-EVT-09's cancel clause under the decision point `reveal`.
   - The bullets TRN-STA-03, TRN-STA-06, TRN-STA-10, TRN-ANI-33, TRN-ANI-34 and TRN-EVT-09 gain "Server: see ADR 01 relaxation table." No statement, level, `Source:` or `Test:` line changes.
   - The evaluation step of TRN-ALG-01 runs in both languages, pinned by one case table (ADR 01 decision 2 note).
11. **Popup-host revision of 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); ADR 12 Amendments 2026-10-27).** Dated 2026-10-27. The popup store evaluates mounted and the status at the write that flips the effective open state, mirrors the status the popup's `transitions` handle reports, and runs the completion and the unmount cleanup from its completion reports (`PopupStore.MirrorStatus`, `CompleteTransitionAsync`, `UnmountAsync`). The rows with a popup clause are green on the test-only popup host at L2 and L4 (the dated notes on TRN-API-04, STA-06, EVT-03, DOM-06 and ANI-18), and `popupConformanceTests`' onOpenChangeComplete and animation titles run on `popup-conformance` on both modes. Open: API-02, API-07, ANI-12's Popover title, ANI-19, 24, 26, 28..31 and ALG-06 → M4 track 5; DOM-10 → track 6; DOM-05 → track 2 and the families. The mechanism rows with M5 titles are built here and re-cited by tracks 5 and 8.
