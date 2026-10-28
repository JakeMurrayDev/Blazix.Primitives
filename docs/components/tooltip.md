# Tooltip

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/tooltip/` |
| Tests | `packages/react/src/tooltip/**/*.test.tsx` (9 files) + `positioner/TooltipPositioner.spec.tsx`, `trigger/TooltipTrigger.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/tooltip/page.mdx` |
| Prefix | `TIP` |
| Status | approved |
| Revised | 2026-10-28 |
| Builds on | none |
| Derived by | none ([preview-card](preview-card.md) shares most of the mechanisms but is a separate component) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Tooltip is a hover/focus-revealed hint: a **Trigger** (default `<button>`) that, after a rest delay (600 ms by default), shows a floating **Popup** positioned against it by a **Positioner** inside a **Portal**. The popup opens on mouse/pen hover and on keyboard (focus-visible) focus, and closes when the pointer leaves (immediately by default; through a safe-polygon corridor into a hoverable popup), when the trigger loses focus, on Escape, on an outside press, on a press of the trigger itself (`closeOnClick`), when the Root becomes `disabled`, or imperatively. Touch pointers never open it (the official docs treat tooltips as mouse/keyboard-only affordances). An optional **Provider** groups tooltips so that, once one has opened, hovering a sibling opens it instantly (no delay, no animation) for a grace period (`timeout`, 400 ms) after the previous one closed; an **Arrow** part and a **Viewport** part (for content transitions between triggers) are optional.

Tooltip is one of the handle-capable popups described in [popup-store-and-handles](../architecture/popup-store-and-handles.md): several triggers (inside the Root or detached anywhere in the tree through `Tooltip.createHandle()`) can share one popup, the active trigger's `payload` is exposed to a function child of the Root, and the handle offers `open(triggerId)`, `close()` and `isOpen`. Compared with [popover](popover.md) it is not focusable-on-open, never traps or moves focus, never renders a backdrop, exposes no `aria-expanded`/`aria-controls`, and in 1.9.0 renders **no ARIA role and no `aria-describedby` wiring at all** (§5.7, §16 Q1): it is a purely visual hint, and the official docs require the trigger to carry its own accessible name. Compared with [preview-card](preview-card.md) it opens on focus, supports nested triggers (a tooltip trigger inside another tooltip's trigger), cursor tracking (`trackCursorAxis`) and the delay-group Provider.

## 2. Anatomy

```
<Tooltip.Provider>          optional, 0..1 per group       renders no element
  <Tooltip.Root>            required, 1 per tooltip        renders no element
    <Tooltip.Trigger>       optional, 0..n                 default element: button
    <Tooltip.Portal>        required for a visible popup   portal node: div (appended to the container)
      <Tooltip.Positioner>  required, 1                    default element: div
        <Tooltip.Popup>     required, 1                    default element: div
          <Tooltip.Arrow>   optional, 0..1                 default element: div
          <Tooltip.Viewport> optional, 0..1                default element: div
```

Detached form (`handle`): `<Tooltip.Trigger handle={h}>` may be rendered anywhere, before or after `<Tooltip.Root handle={h}>`, inside or outside it. The Portal/Positioner/Popup subtree is always inside the Root.

- **TIP-STR-01** — MUST. `Tooltip.Provider` renders no element of its own; it only supplies a shared open delay and a delay group to every `Tooltip.Root` below it. It is optional and may enclose any number of Roots.
  Source: `tooltip/provider/TooltipProvider.tsx:12-24`. Test: [L2] `TooltipProvider.test.tsx` "opens an adjacent tooltip instantly while the group is active".
- **TIP-STR-02** — MUST. `Tooltip.Root` renders no element of its own; it owns the popup store and renders its children (or, for a function child, the result of calling it with `{ payload }`).
  Source: `tooltip/root/TooltipRoot.tsx:139-147`. Test: [L2] `TooltipRoot.test.tsx` popupConformanceTests "opens the popup with the `open` prop".
- **TIP-STR-03** — MUST. `Tooltip.Trigger` renders a `<button>` by default. It MUST either be a descendant of a `Tooltip.Root` or receive a `handle`; otherwise rendering throws TIP-EDGE-01. A `handle` takes precedence over an enclosing Root (PSH-STR-02).
  Source: `tooltip/trigger/TooltipTrigger.tsx:62-69,223`. Test: [L2] `TooltipTrigger.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`), "throws a descriptive error when rendered without a root or a handle".
- **TIP-STR-04** — MUST. `Tooltip.Portal` MUST be a descendant of a `Tooltip.Root` (else TIP-EDGE-02) and is the required parent of `Tooltip.Positioner` (a Positioner outside a Portal throws TIP-EDGE-03). It renders the lite portal node of PSH-STR-11 / FLT-DOM-11 (a `<div>` appended to the resolved container) only while the popup is mounted or `keepMounted` is set (TIP-DOM-06).
  Source: `tooltip/portal/TooltipPortal.tsx:19-33`; `tooltip/portal/TooltipPortalContext.ts:6-12`. Test: [L2] `TooltipPortal.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`); `TooltipPositioner.test.tsx` "throws a descriptive error when rendered outside <Tooltip.Portal>".
- **TIP-STR-05** — MUST. `Tooltip.Positioner` renders a `<div>` by default and MUST be a descendant of both a `Tooltip.Root` (else TIP-EDGE-02) and a `Tooltip.Portal` (else TIP-EDGE-03). Exactly one Positioner is expected per Root (it publishes itself as the store's positioner element).
  Source: `tooltip/positioner/TooltipPositioner.tsx:45-46,94-101`. Test: [L2] `TooltipPositioner.test.tsx` describeConformance, "throws a descriptive error when rendered outside <Tooltip.Root>", "throws a descriptive error when rendered outside <Tooltip.Portal>".
- **TIP-STR-06** — MUST. `Tooltip.Popup` renders a `<div>` by default and MUST be a descendant of a `Tooltip.Positioner` (else TIP-EDGE-04) and of a `Tooltip.Root`. It renders its `children` verbatim.
  Source: `tooltip/popup/TooltipPopup.tsx:27-28,63-73`. Test: [L2] `TooltipPopup.test.tsx` describeConformance, "should render the children", "throws a descriptive error when rendered outside <Tooltip.Positioner>".
- **TIP-STR-07** — MUST. `Tooltip.Arrow` renders a `<div>` by default and MUST be a descendant of a `Tooltip.Positioner` (else TIP-EDGE-04) and of a `Tooltip.Root`; it is optional and is normally placed inside the Popup.
  Source: `tooltip/arrow/TooltipArrow.tsx:22-23,36-41`. Test: [L2] `TooltipArrow.test.tsx` describeConformance (`refInstanceof: Element`).
- **TIP-STR-08** — MUST. `Tooltip.Viewport` renders a `<div>` by default, MUST be a descendant of a `Tooltip.Positioner` and a `Tooltip.Root`, and is optional: it is only needed when one popup is opened by several triggers with per-trigger content and the switch is animated (PSH viewport).
  Source: `tooltip/viewport/TooltipViewport.tsx:23-24,40-45`. Test: [L2] `TooltipViewport.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`). Docs: `components/tooltip/page.mdx` §Viewport (line 249).
- **TIP-STR-09** — MUST. Any number of Triggers (contained, detached through a shared handle, or a mix) MAY target one Root; at most one is the active trigger at a time (PSH-STR-06), and the popup's anchor is the active trigger.
  Source: `tooltip/trigger/TooltipTrigger.tsx:80-89`; `utils/popups/popupStoreUtils.ts:323-394`. Test: [L4] `TooltipRoot.test.tsx` "when using multiple detached triggers" (whole group); `TooltipRoot.detached-triggers.test.tsx` "hands off open state and payload to a trigger with its own DOM id while open" (L4 for the anchor clause). Docs: `components/tooltip/page.mdx` §Multiple triggers.
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the group's hover and focus run in the `interactions` module, so its titles are browser-only; green in `tests/browser/tooltip.spec.ts` ("when using multiple detached triggers", the hand-off titles on `tooltip-handoff`) on both render modes. L2 keeps the store's clause (`TooltipDetachedTriggersTests`).
- **TIP-STR-10** — MUST. A `Tooltip.Root` MAY be rendered *inside another tooltip's Trigger* (nested tooltips, e.g. an icon button with its own tooltip inside a row that has a tooltip). The inner Trigger is a *nested trigger* of the outer one; the outer tooltip suppresses itself while the pointer is over an enabled nested trigger (§6.2.4). Nesting depth is unbounded.
  Source: `tooltip/trigger/TooltipTrigger.tsx:31,121-144`. Test: [L4] `TooltipRoot.test.tsx` "should not open ancestor tooltips when hovering over a third-level nested trigger". Docs: CHANGELOG.md v1.4.0 "Fix flickering when nesting tooltip inside another tooltip's trigger (#4501)".
- **TIP-STR-11** — MUST. The Root, Provider, Portal and the Positioner/Popup/Arrow/Viewport chain impose no DOM adjacency: parts locate their Root (and Positioner) through ancestor-provided shared state, so arbitrary wrapper elements may sit between them.
  Source: `tooltip/root/TooltipRootContext.ts:9-20`; `tooltip/positioner/TooltipPositionerContext.ts:14-21`. Test: [L2] `TooltipRoot.test.tsx` `ContainedTriggerTooltip` fixture (`beforeTrigger`/`betweenTriggerAndPortal`/`afterPortal` slots).

## 3. Public API

### 3.1 `Tooltip.Provider`

**Default element:** none (renders children only). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (no state; `Tooltip.Provider.State` is `{}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `delay` | `number \| undefined` | `undefined` | Hover open delay (ms) used by every Trigger below that has no `delay` of its own. | TIP-API-01 |
| `closeDelay` | `number \| undefined` | `undefined` | Hover close delay (ms) used by every Trigger below that has no `closeDelay` of its own. Read live (a later value applies to the next close). | TIP-API-02 |
| `timeout` | `number` | `400` | Grace period (ms) after a grouped tooltip closes during which another grouped tooltip opens instantly. | TIP-API-03 |
| `children` | node | — | Content. | — |

**Consumed (not forwarded):** all of them (there is no element).

- **TIP-API-01** — MUST. `Provider.delay` defaults to `undefined`. It is the second fallback in the open-delay resolution of TIP-API-15: a Trigger with its own `delay` ignores it; a Trigger without one uses it; when both are absent 600 ms is used.
  Source: `tooltip/provider/TooltipProvider.tsx:13,18`; `tooltip/provider/TooltipProviderContext.ts:7-11`; `tooltip/trigger/TooltipTrigger.tsx:91,118`. Test: [L2] `TooltipProvider.test.tsx` "waits for the delay before showing the tooltip", "respects delay=0", "respects trigger delay prop over provider delay prop".
- **TIP-API-02** — MUST. `Provider.closeDelay` defaults to `undefined` (immediate close). It is passed to the delay group as the group's close delay and is used by a Trigger that has no `closeDelay` of its own (TIP-API-16); changing it while tooltips are mounted takes effect on the next hover close. Server: relaxed, see §9.
  Source: `tooltip/provider/TooltipProvider.tsx:15,19`; `floating-ui-react/components/FloatingDelayGroup.tsx:73-85` (FLT-EDGE-12); `tooltip/trigger/TooltipTrigger.tsx:152-157`. Test: [L2] `TooltipProvider.test.tsx` "waits for the closeDelay before hiding the tooltip", "uses the latest closeDelay after the prop updates".
- **TIP-API-03** — MUST. `Provider.timeout` defaults to `400` and is passed to the delay group as `timeoutMs`: after the active grouped tooltip closes, the group stays "active" for `timeout` ms, during which hovering any grouped Trigger opens with a 0 ms delay (TIP-PTR-04) and without transitions (TIP-ANI-03); once it elapses the full delay is required again.
  Source: `tooltip/provider/TooltipProvider.tsx:13,19`; `floating-ui-react/components/FloatingDelayGroup.tsx:178-201` (FLT-ALG-23). Test: [L2] `TooltipProvider.test.tsx` "opens an adjacent tooltip instantly while the group is active", "requires the full delay again once the timeout elapses".

#### State object

None (`Tooltip.Provider.State = {}`). Source: `tooltip/provider/TooltipProvider.tsx:26`.

#### Data attributes / CSS variables

None.

### 3.2 `Tooltip.Root`

**Default element:** none. **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (`Tooltip.Root.State` is `{}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `defaultOpen` | `boolean` | `false` | Initial open state when `open` is not supplied. | TIP-API-04 |
| `open` | `boolean \| undefined` | `undefined` (uncontrolled) | Consumer-owned open state. | TIP-API-05 |
| `onOpenChange` | `(open: boolean, eventDetails: Tooltip.Root.ChangeEventDetails) => void` | `undefined` | Open-change request callback (§7). | TIP-API-06 |
| `onOpenChangeComplete` | `(open: boolean) => void` | `undefined` | Called after the enter/exit animation of the Popup completes (§9). | TIP-API-07 |
| `actionsRef` | `RefObject<Tooltip.Root.Actions \| null>` | `undefined` | Receives `{ unmount(), close() }`. | TIP-API-08 |
| `disabled` | `boolean` | `false` | Disables the tooltip: never reported open, all Triggers disabled unless they opt back in. | TIP-API-09 |
| `disableHoverablePopup` | `boolean` | `false` | When `true` the popup cannot be hovered: leaving the trigger closes even if the pointer moves onto the popup, and the Positioner gets `pointer-events: none`. | TIP-API-10 |
| `trackCursorAxis` | `'none' \| 'x' \| 'y' \| 'both'` | `'none'` | Anchors the popup to the pointer position on the given axis/axes instead of the trigger box. | TIP-API-11 |
| `handle` | `Tooltip.Handle<Payload> \| undefined` | `undefined` | Connects detached Triggers and enables imperative control (PSH). | TIP-API-12 |
| `triggerId` | `string \| null \| undefined` | `undefined` | Consumer-owned active trigger id (use with `open`). | TIP-API-13 |
| `defaultTriggerId` | `string \| null` | `null` | Initial active trigger id (use with `defaultOpen`). | TIP-API-13 |
| `children` | `ReactNode \| ({ payload }) => ReactNode` | — | Content, or a function receiving the active trigger's payload. | TIP-API-14 |

**Consumed (not forwarded):** all (no element).

- **TIP-API-04** — MUST. `defaultOpen` defaults to `false` and seeds the component-owned open state once; later changes are ignored. A Root that mounts with `defaultOpen` and no `defaultTriggerId` claims its lone registered trigger as the active trigger (PSH-STA-18), so hover-leaving that trigger closes it.
  Source: `tooltip/root/TooltipRoot.tsx:35,52`; `utils/popups/popupStoreUtils.ts:501-514`. Test: [L2] `TooltipRoot.test.tsx` "closes when the trigger is unhovered after opening by default"; popupConformanceTests "opens initially and remains uncontrolled" (L4 for the hover-leave clause).
- **TIP-API-05** — MUST. `open`, when not `undefined`, makes the open state consumer-owned: interactions only request changes through `onOpenChange` and the popup follows the prop (PSH-STA-02, EVD-STA-04). With `open={true}` Escape (or any other dismissal) requests a close but cannot close the popup by itself.
  Source: `tooltip/root/TooltipRoot.tsx:36,53,62`; `utils/popups/store.ts:150`. Test: [L2] `TooltipRoot.test.tsx` popupConformanceTests "opens the popup with the `open` prop", "is ignored when `open={false}` is passed", "is ignored when `open={true}` is passed, so Escape cannot close"; "should call onOpenChange when the open state changes".
- **TIP-API-06** — MUST. `onOpenChange` is optional; when absent, interactions change the component-owned state directly (uncontrolled) or do nothing (controlled).
  Source: `tooltip/root/TooltipRoot.tsx:40,65`; `packages/utils/src/store/ReactStore.ts` via UTL-STA-16. Test: [L2] `TooltipRoot.test.tsx` "should open when the trigger is hovered" (no callback).
- **TIP-API-07** — MUST. `onOpenChangeComplete` is optional and follows TRN-EVT-01/02: `(true)` after the Popup's enter animation, `(false)` after its exit animation or the unmount action; never on a closed mount.
  Source: `tooltip/root/TooltipRoot.tsx:41,66`; `tooltip/popup/TooltipPopup.tsx:38-46`; `utils/popups/popupStoreUtils.ts:594`. Test: [L4] `TooltipRoot.test.tsx` popupConformanceTests "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes".
- **TIP-API-08** — MUST. `actionsRef`, when supplied, receives an object with `unmount()` (ends the closing phase, TRN-EVT-08 / PSH-API-09) and `close()` (requests a close with reason `imperative-action`, TIP-EVT-01).
  Source: `tooltip/root/TooltipRoot.tsx:128-135,220-223`. Test: [L2] `TooltipRoot.test.tsx` "unmounts the tooltip when the `unmount` method is called", "closes the tooltip when the `close` method is called".
- **TIP-API-09** — MUST. `disabled` defaults to `false`. When `true`: (a) the effective open state reported to every part is `false` regardless of the stored state (TIP-STA-02); (b) an open tooltip closes with reason `disabled` (TIP-DIS-06); (c) every Trigger that does not set its own `disabled` is disabled (TIP-API-18) and carries `data-trigger-disabled`; (d) the Root's dismiss and cursor-tracking interactions are turned off. A Trigger that opts back in with `disabled={false}` loses `data-trigger-disabled` but still cannot show the tooltip (its open request is reverted per (b); see §16 Q6).
  Source: `tooltip/root/TooltipRoot.tsx:34,69,78,96-100,137,261,265`; `tooltip/trigger/TooltipTrigger.tsx:102-104,260`. Test: [L2] `TooltipRoot.test.tsx` "should not open when disabled", "should close if open when becoming disabled", "marks the trigger as disabled when the root is disabled", "keeps the tooltip disabled when the root is disabled and the trigger opts back in".
- **TIP-API-10** — MUST. `disableHoverablePopup` defaults to `false`. With `false` the hover interaction uses the safe-polygon close strategy (TIP-PTR-06) so the pointer can travel into the popup; with `true` no strategy is used (leaving the trigger closes after `closeDelay` even when heading to the popup) and the Positioner carries inline `pointer-events: none` (TIP-DOM-07).
  Source: `tooltip/root/TooltipRoot.tsx:37,77`; `tooltip/trigger/TooltipTrigger.tsx:106,150`; `tooltip/positioner/TooltipPositioner.tsx:51,100`. Test: [L4] `TooltipRoot.test.tsx` "applies pointer-events: none to the positioner when `disableHoverablePopup = true`", "does not apply pointer-events: none to the positioner when `disableHoverablePopup = false`", "closes on that same trip when `disableHoverablePopup` is set". Docs: CHANGELOG.md v1.0.0-beta.5 "Change `hoverable` to `disableHoverablePopup`".
- **TIP-API-11** — MUST. `trackCursorAxis` defaults to `'none'` and accepts `'none' | 'x' | 'y' | 'both'`. Any value other than `'none'` enables cursor anchoring (TIP-POS-03) and marks every part's `instant` state as `'tracking-cursor'` on the Positioner (TIP-DOM-08); `'both'` additionally makes the popup non-hoverable (no safe polygon, `pointer-events: none` on the Positioner), while `'x'`/`'y'` keep it hoverable.
  Source: `tooltip/root/TooltipRoot.tsx:38,76,137,264-267`; `tooltip/trigger/TooltipTrigger.tsx:105,150,218`; `tooltip/positioner/TooltipPositioner.tsx:50,82,100`. Test: [L4] `TooltipRoot.test.tsx` "makes the positioner inert when tracking both axes", "keeps the positioner hoverable when tracking a single axis", "tracks the cursor on the first delayed hover when trackCursorAxis is x". Docs: CHANGELOG.md v1.1.0 "Fix `trackCursorAxis` handling (#3679)".
- **TIP-API-12** — MUST. `handle` defaults to `undefined`. When given, the Root attaches its store to the handle for its mounted lifetime (PSH-STR-03/04) so that detached Triggers with the same handle register with it and `handle.open()`/`close()`/`isOpen` act on this Root.
  Source: `tooltip/root/TooltipRoot.tsx:42,141`; `utils/popups/popupStoreUtils.ts:103-115`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "opens by trigger id with its payload and closes", "ignores imperative calls while no root is attached and follows the root across remounts".
- **TIP-API-13** — MUST. `triggerId` (default `undefined`, consumer-owned when defined; `null` means "no active trigger") and `defaultTriggerId` (default `null`) follow PSH-API-06 / PSH-STA-03; `triggerId` is meant to be used together with `open`, `defaultTriggerId` with `defaultOpen`.
  Source: `tooltip/root/TooltipRoot.tsx:43-44,54-55,63,207-217`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "marks the trigger matching a controlled `triggerId` as active", "opens initially with the payload of a non-first `defaultTriggerId`", "allows controlling the open state and active trigger programmatically". Docs: `components/tooltip/page.mdx` §Controlled mode with multiple triggers.
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite (`tests/browser/suites/`), green on both render modes. L2 keeps the clause the store alone observes (`TooltipDetachedTriggersTests` "opens initially with the payload of a non-first `defaultTriggerId`").
- **TIP-API-14** — MUST. When `children` is a function it is called with `{ payload }` where `payload` is the active trigger's `payload` (`undefined` when none; PSH-API-07); otherwise `children` is rendered as-is.
  Source: `tooltip/root/TooltipRoot.tsx:73,145,206`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it"; `TooltipViewport.test.tsx` viewportConformanceTests (function child fixture).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the suite's payload title runs in the browser suite, green on both render modes; L2 keeps the payload template (`TooltipDetachedTriggersTests` "sets the payload of the active trigger and renders content based on it"). The Viewport's function-child fixture waits for track 5's `popup` module (design point 13.2).

#### State object

None (`Tooltip.Root.State = {}`). Source: `tooltip/root/TooltipRoot.tsx:150`.

#### Data attributes / CSS variables

None (no element).

### 3.3 `Tooltip.Trigger`

**Default element:** `<button>` (rendered with `type="button"`, RND-STR-05). **Accepts `render` element substitution:** yes (the substitute callback receives typed button attributes — `TooltipTrigger.spec.tsx`). **Accepts `className`/`style` as function of state:** yes (state = `Tooltip.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `handle` | `Tooltip.Handle<Payload> \| undefined` | `undefined` | Binds this (detached) trigger to the Root using the same handle. | TIP-API-12, PSH-STR-02 |
| `payload` | `Payload \| undefined` | `undefined` | Value exposed to the Root's function child while this trigger is active. | TIP-API-19 |
| `delay` | `number \| undefined` | `undefined` → Provider `delay` → `600` | Hover rest delay before opening (ms). | TIP-API-15 |
| `closeDelay` | `number \| undefined` | `undefined` → Provider `closeDelay` → `0` | Delay before closing after the pointer leaves the trigger (ms); leaving the Popup uses this value without the Provider fallback (§16 Q16). | TIP-API-16 |
| `closeOnClick` | `boolean` | `true` | Pressing this trigger closes an open tooltip and cancels a pending hover open. | TIP-API-17 |
| `disabled` | `boolean \| undefined` | Root `disabled` | Disables the tooltip for this trigger only; does **not** set the `disabled` attribute. | TIP-API-18 |
| `id` | `string` | generated | The element id and the trigger id (PSH-API-08). | TIP-API-20 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<button>` attribute / handler | | — | Forwarded; consumer handlers are merged after Base UI's (RND handler merging, EVD-API-11). | — |

**Consumed (not forwarded to the element):** `handle`, `payload`, `delay`, `closeOnClick`, `closeDelay`, `disabled`.

- **TIP-API-15** — MUST. The effective hover open delay of a Trigger is resolved on every hover as: `0` while the enclosing Provider's delay group is active (TIP-PTR-04); otherwise the Trigger's `delay` if defined, else the Provider's `delay` if defined, else `600` ms. A Trigger `delay` therefore wins over a Provider `delay` of `0` outside the instant phase.
  Source: `tooltip/trigger/TooltipTrigger.tsx:113-119,151`; `tooltip/utils/constants.ts:1`. Test: [L4] `TooltipRoot.test.tsx` "should open after rest delay"; `TooltipProvider.test.tsx` "respects trigger delay prop over provider delay prop", "respects a trigger delay over delay=0 outside the instant phase". Docs: CHANGELOG.md v1.8.0 "Respect trigger delay with zero provider delay (#5444)".
- **TIP-API-16** — MUST. The effective hover close delay is: the Trigger's `closeDelay` if defined; else, when a Provider encloses the trigger, the Provider's current `closeDelay` (through the delay group; `undefined` → immediate); else `0`. While this trigger is active, the store's `closeDelay`, which the Popup's own leave handler reads (TIP-PTR-26), receives the Trigger's own `closeDelay` defaulted to `0`; it does not fall back to the Provider's `closeDelay`, so with only a Provider `closeDelay` set, leaving the Popup closes immediately (§16 Q16).
  Source: `tooltip/trigger/TooltipTrigger.tsx:78,87,152-157`; `floating-ui-react/hooks/useHoverShared.ts:46-57` (FLT-API-03); `tooltip/popup/TooltipPopup.tsx:36,48-51`. Test: [L4] `TooltipRoot.test.tsx` "should close after delay"; `TooltipProvider.test.tsx` "waits for the closeDelay before hiding the tooltip".
- **TIP-API-17** — MUST. `closeOnClick` defaults to `true`. It is written to the store on every `pointerdown` of this trigger and forwarded as trigger state while active; it gates both the "cancel pending open on press" behavior (TIP-PTR-10) and the "close on press while open" behavior (TIP-PTR-11).
  Source: `tooltip/trigger/TooltipTrigger.tsx:56,86,247-258`; `tooltip/root/TooltipRoot.tsx:260-263`; `tooltip/store/TooltipStore.ts:41,134`. Test: [L4] `TooltipRoot.test.tsx` "should open when the trigger was clicked before delay duration and closeOnClick is false", "should not close when the trigger is clicked after delay duration and closeOnClick is false". Docs: CHANGELOG.md v1.3.0 "Add `closeOnClick` prop (#4140)".
- **TIP-API-18** — MUST. A Trigger's `disabled`, when not `undefined`, replaces the Root's `disabled` for this trigger; `undefined` inherits it. A disabled trigger: does not open on hover or focus (its hover and focus interactions are off), carries `data-trigger-disabled=""`, loses the nested-trigger marker (TIP-DOM-04), and is treated as "not a trigger" by other triggers' leave/blur logic (so moving the pointer or focus onto it closes an open tooltip, TIP-PTR-08 / TIP-FOC-03). The native `disabled` attribute is **not** set; use `render` to disable the element itself.
  Source: `tooltip/trigger/TooltipTrigger.tsx:54,102-104,147,166,260-261,311-317`; `floating-ui-react/utils/element.ts:10-31`. Test: [L4] `TooltipRoot.test.tsx` "should not open on focus when the trigger is disabled", "should allow the parent tooltip to open when a nested trigger is disabled"; `TooltipRoot.detached-triggers.test.tsx` "should close when focusing a disabled trigger while another trigger is open", "should close when hovering a disabled trigger while another trigger is open" (L4 for the hover, focus and leave/blur clauses). Docs: CHANGELOG.md v1.2.0 "Prevent opening when focusing a disabled Trigger (#3902)", "Fix disabled prop on Triggers (#4049)".
  Deviation note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.6, settled by the orchestrator 2026-10-28): a disabled Trigger leaves its Provider's group (TIP-STA-08's note (b)).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the hover and focus titles are browser-only, green in `tooltip.spec.ts` and on `tooltip-handoff` on both render modes; L2 keeps the attributes and the options a disabled trigger sends (`TooltipRootTests`, `TooltipDetachedTriggersTests`).
- **TIP-API-19** — MUST. `payload` defaults to `undefined` and is copied into the store's `payload` whenever this trigger is (or becomes) the active trigger while open (PSH-STA-28/29/30); the Root's function child receives it.
  Source: `tooltip/trigger/TooltipTrigger.tsx:53,85`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "hands off open state and payload to a trigger with its own DOM id while open"; detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the hand-off is the `interactions` module's; green on `tooltip-handoff` and through the browser suite on both render modes. L2 keeps the payload write (`TooltipDetachedTriggersTests`).
- **TIP-API-20** — MUST. The Trigger's `id` prop is both its rendered `id` and its registry/trigger id; when absent a generated `base-ui-…` id is used for both (RND-DOM-27, PSH-API-08). When a substitute element supplies its own `id` (e.g. `render={<button id="custom" />}`), the rendered id is the substitute's while the registry id stays the generated one; the store reassociates ownership to the registered id (PSH-STA-20) so hover still opens and marks the trigger.
  Source: `tooltip/trigger/TooltipTrigger.tsx:58,71,259`; `internals/useBaseUiId.ts:9-11`. Test: [L4] `TooltipTrigger.test.tsx` "opens when the rendered trigger element has its own id"; `TooltipRoot.detached-triggers.test.tsx` "hands off open state and payload to a trigger with its own DOM id while open" (L4 for the hover-opens clause).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the hover-opens clause is the `interactions` module's, green on `tooltip-handoff` on both render modes; L2 keeps the rendered id and the attach by id (`TooltipTriggerTests`).

#### State object (`Tooltip.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | `true` iff the tooltip is effectively open **and** this trigger is the active trigger (PSH-STA-08 `isOpenedByTrigger(ownId)`). |

Source: `tooltip/trigger/TooltipTrigger.tsx:73,221,277-282`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | `open === true` (empty value); absent otherwise, including during the exit transition | TIP-DOM-02 |
| `data-trigger-disabled` | effective `disabled` is `true` (empty value) | TIP-DOM-03 |
| `data-base-ui-tooltip-trigger` | private marker, present iff **not** disabled (not in the outline; §16 Q3) | TIP-DOM-04 |

#### CSS variables

None.

### 3.4 `Tooltip.Portal`

**Default element:** the portal node `<div>` (FLT-DOM-11). **Accepts `render` element substitution:** yes (applied to the portal node). **Accepts `className`/`style` as function of state:** yes, with an empty state (`Tooltip.Portal.State = {}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the portal subtree in the DOM while closed (hidden). | TIP-API-21 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject<…> \| null \| undefined` | `undefined` → nearest enclosing portal node, else `document.body` | Where the portal node is appended (FLT-API-13). | TIP-API-22 |
| `className`, `style`, `render`, other `<div>` attributes | per RND / FLT-DOM-11 | — | Applied to the portal node. | — |

**Consumed:** `keepMounted`, `container`.

- **TIP-API-21** — MUST. `keepMounted` defaults to `false`. With `false` the Portal renders nothing while the popup is not mounted (closed and not animating out); with `true` the subtree stays rendered and the Positioner carries the `hidden` attribute while not mounted (TIP-DOM-06/07). The value is also handed to the Positioner's positioning session (FLT-API-17 `keepMounted`).
  Source: `tooltip/portal/TooltipPortal.tsx:19-33`; `tooltip/positioner/TooltipPositioner.tsx:46,71`. Test: [L2] `TooltipPortal.test.tsx` "renders the closed popup as hidden instead of unmounting it", "unmounts the closed popup by default".
- **TIP-API-22** — MUST. `container` is resolved per FLT-API-13; `null` renders nothing until a container is supplied. Shadow roots are accepted.
  Source: `tooltip/portal/TooltipPortal.tsx:31,47-48`; `utils/FloatingPortalLite.tsx:19-37`. Test: none in this component (FLT `FloatingPortal.test.tsx`). Docs: CHANGELOG.md v1.0.0-beta.1 "Support `ShadowRoot` containers (#2236)".

#### State object

None (`{}`). **Data attributes / CSS variables:** the portal node's own (`data-base-ui-portal`, FLT-DOM-11).

### 3.5 `Tooltip.Positioner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tooltip.Positioner.State`). The consumer `style` is merged after the positioning styles (RND-DOM-16).

#### Props

All shared positioner props of FLT §3.1 with these defaults (differences from the shared defaults in bold):

| Prop | Type | Default | Req |
|---|---|---|---|
| `anchor` | element / virtual element / ref / function / `null` | `undefined` (the active trigger, or the cursor anchor when tracking) | TIP-POS-02 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | TIP-POS-01 |
| `side` | `Side` | **`'top'`** | TIP-POS-01 |
| `align` | `Align` | `'center'` | TIP-POS-01 |
| `sideOffset` | `number \| OffsetFunction` | `0` | TIP-POS-01 |
| `alignOffset` | `number \| OffsetFunction` | `0` | TIP-POS-01 |
| `collisionBoundary` | `Boundary` | `'clipping-ancestors'` | TIP-POS-01 |
| `collisionPadding` | `Padding` | `5` | TIP-POS-01 |
| `sticky` | `boolean` | `false` | TIP-POS-01 |
| `arrowPadding` | `number` | `5` | TIP-POS-01 |
| `disableAnchorTracking` | `boolean` | `false` | TIP-POS-01 |
| `collisionAvoidance` | `CollisionAvoidance` | `{ fallbackAxisSide: 'end' }` (`POPUP_COLLISION_AVOIDANCE`) | TIP-POS-01 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | — |

`keepMounted` is **not** a Positioner prop (it lives on the Portal; TIP-POS-09, `TooltipPositioner.spec.tsx`).

**Consumed:** every positioning prop above.

#### State object (`Tooltip.Positioner.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Rendered (logical) side: `'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-end' \| 'inline-start'`. |
| `align` | `Align` | Rendered alignment: `'start' \| 'center' \| 'end'`. |
| `anchorHidden` | `boolean` | Anchor fully clipped (FLT-POS-09). |
| `instant` | `string \| undefined` | `'tracking-cursor'` when `trackCursorAxis !== 'none'`, otherwise the store's instant type (`'delay' \| 'focus' \| 'dismiss' \| undefined`, TIP-STA-05). |

Source: `tooltip/positioner/TooltipPositioner.tsx:76-92,110-131`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | exactly one, from `open` | TIP-DOM-08 |
| `data-side` | `side` value | TIP-DOM-08 |
| `data-align` | `align` value | TIP-DOM-08 |
| `data-anchor-hidden` | `anchorHidden === true` | TIP-DOM-08 |
| `data-instant` | `instant` value when defined (`tracking-cursor`, `delay`, `focus`, `dismiss`); not listed in the outline (§16 Q2) | TIP-DOM-08 |
| `hidden` | not mounted (only observable with `keepMounted`) | TIP-DOM-07 |
| `role="presentation"` | always | TIP-ARIA-04 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--available-width`, `--available-height` | Positioner inline style | FLT-DOM-05 (seeded `100vw`/`100vh`) | TIP-POS-08 |
| `--anchor-width`, `--anchor-height` | Positioner inline style | FLT-DOM-06 | TIP-POS-08 |
| `--transform-origin` | Positioner inline style | FLT-DOM-07 | TIP-POS-08 |
| `--positioner-width`, `--positioner-height` | Positioner inline style | PSH-DOM-11 (only while a Viewport is mounted) | TIP-POS-08 |

### 3.6 `Tooltip.Popup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tooltip.Popup.State`).

#### Props

Only the common parameters (`className`, `style`, `render`, `children`, any `<div>` attribute). No component-specific props. **Consumed:** none beyond `className`/`style`/`render`.

#### State object (`Tooltip.Popup.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | From the Positioner. |
| `align` | `Align` | From the Positioner. |
| `instant` | `'delay' \| 'focus' \| 'dismiss' \| undefined` | Store instant type (TIP-STA-05). |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Store transition status (PSH-STA-05). |

Source: `tooltip/popup/TooltipPopup.tsx:55-61,78-99`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | exactly one, from `open` | TIP-DOM-12 |
| `data-starting-style` / `data-ending-style` | `transitionStatus` `'starting'` / `'ending'` (TRN-DOM-01/02) | TIP-DOM-12 |
| `data-side`, `data-align` | rendered placement | TIP-DOM-12 |
| `data-instant` | `instant` value when defined (`delay`, `focus`, `dismiss`) | TIP-DOM-12 |
| `tabindex="-1"`, `data-base-ui-focusable` | always | TIP-DOM-11 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--popup-width`, `--popup-height` | Popup inline style (auto-resize, only while a Viewport is mounted) | PSH-DOM-11 | TIP-DOM-16 |

### 3.7 `Tooltip.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tooltip.Arrow.State`); the consumer `style` is merged after the arrow positioning styles.

#### Props

Only the common parameters. **Consumed:** none beyond `className`/`style`/`render`.

#### State object (`Tooltip.Arrow.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | From the Positioner. |
| `align` | `Align` | From the Positioner. |
| `uncentered` | `boolean` | The arrow cannot point at the anchor's center (FLT-DOM-09). |
| `instant` | `'delay' \| 'focus' \| 'dismiss' \| undefined` | Store instant type. |

Source: `tooltip/arrow/TooltipArrow.tsx:28-34,46-67`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | exactly one | TIP-DOM-15 |
| `data-side`, `data-align` | rendered placement | TIP-DOM-15 |
| `data-uncentered` | `uncentered === true` | TIP-DOM-15 |
| `data-instant` | `instant` when defined | TIP-DOM-15 |
| `aria-hidden="true"` | always | TIP-ARIA-03 |

#### CSS variables

None.

### 3.8 `Tooltip.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tooltip.Viewport.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | node | — | Content to transition; wrapped in `[data-current]`/`[data-previous]` containers (PSH-STR-07). | TIP-DOM-16 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | | — |

#### State object (`Tooltip.Viewport.State`)

| Field | Type | Meaning |
|---|---|---|
| `activationDirection` | `string \| undefined` | PSH-DOM-07 token pair. |
| `transitioning` | `boolean` | A previous container is rendered (PSH-DOM-08). |
| `instant` | `'delay' \| 'focus' \| 'dismiss' \| undefined` | Store instant type (mirrors the Popup). |

Source: `tooltip/viewport/TooltipViewport.tsx:34-38,48-61`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-activation-direction` | per PSH-DOM-07 | TIP-DOM-16 |
| `data-transitioning` | `transitioning === true` | TIP-DOM-16 |
| `data-instant` | `instant` when defined | TIP-DOM-16 |
| `data-current` / `data-previous` | on the child containers, not on the Viewport element (PSH-STR-07) | TIP-DOM-16 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--popup-width`, `--popup-height` | the `[data-previous]` container (PSH-DOM-09) | previous content size | TIP-DOM-16 |

### 3.9 `Tooltip.createHandle()` and `Tooltip.Handle`

| Member | Signature | Behavior | Req |
|---|---|---|---|
| `Tooltip.createHandle<Payload>()` | `() => Tooltip.Handle<Payload>` | New independent handle (PSH-API-01). | TIP-API-23 |
| `handle.open(triggerId)` | `(triggerId: string) => void` | Opens the attached Root with reason `imperative-action`, associating the registered trigger; throws when no trigger with that id is registered (TIP-EDGE-05); ignored (dev warning) when no Root is attached. | TIP-API-23 |
| `handle.close()` | `() => void` | Closes the attached Root with reason `imperative-action`; ignored (dev warning) when no Root is attached. | TIP-API-23 |
| `handle.isOpen` | `boolean` (getter) | Effective open state of the attached Root, `false` when none. | TIP-API-23 |

- **TIP-API-23** — MUST. `Tooltip.createHandle()` returns a `Tooltip.Handle` whose `open(triggerId)` requires a string id (no `null` form, PSH-API-02), whose `close()` takes no arguments, and whose `isOpen` reads the attached Root's effective open state (PSH-API-04/05). Calls made while no Root is attached are ignored and never replayed (PSH-STA-40).
  Source: `tooltip/store/TooltipHandle.ts:13-55`; `utils/popups/popupHandle.ts:213-285`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "opens by trigger id with its payload and closes", "throws when opened with an unregistered trigger id", "ignores imperative calls while no root is attached and follows the root across remounts". Docs: `components/tooltip/page.mdx` §Detached triggers.
- **TIP-API-24** — MUST. The complete export surface of `@base-ui/react/tooltip` is: parts `Provider`, `Root`, `Trigger`, `Portal`, `Positioner`, `Popup`, `Arrow`, `Viewport`; `createHandle`, `Handle`; the namespaces `TooltipTriggerDataAttributes`, `TooltipPositionerCssVariables`, `TooltipPositionerDataAttributes`, `TooltipPopupDataAttributes`, `TooltipArrowDataAttributes`, `TooltipViewportCssVariables`, `TooltipViewportDataAttributes`; and the types `Tooltip.<Part>.Props`/`.State` for every part, `Tooltip.Root.Actions`, `Tooltip.Root.ChangeEventReason`, `Tooltip.Root.ChangeEventDetails`, `Tooltip.Provider.Props`/`.State` (plus the flat aliases `TooltipRootProps`, …).
  Source: `tooltip/index.parts.ts:1-12`; `tooltip/index.ts:1-18`. Test: none (outline `docs/src/app/(docs)/react/components/page.mdx` §Tooltip "Exports").

### 3.10 Shared types

- `Tooltip.Root.ChangeEventReason` = `'trigger-hover' | 'trigger-focus' | 'trigger-press' | 'outside-press' | 'escape-key' | 'disabled' | 'imperative-action' | 'none'`. Source: `tooltip/root/TooltipRoot.tsx:225-233`; `internals/reason-parts.ts:1,3-5,7,24,37,40`.
- `Tooltip.Root.ChangeEventDetails` = the EVD change-event details (EVD-API-01) specialized to those reasons, plus `preventUnmountOnClose(): void` (EVD-API-09, TRN-EVT-07). Source: `tooltip/root/TooltipRoot.tsx:235-239`.
- `Tooltip.Root.Actions` = `{ unmount(): void; close(): void }`. Source: `tooltip/root/TooltipRoot.tsx:220-223`.
- `Side`, `Align`, `Boundary`, `Padding`, `OffsetFunction`, `CollisionAvoidance`, `VirtualElement`: FLT §3.4. `TransitionStatus = 'starting' | 'ending' | 'idle' | undefined`: `internals/useTransitionStatus.ts:6`.
- `PayloadChildRenderFunction<Payload> = (arg: { payload: Payload | undefined }) => ReactNode`. Source: `utils/popups/popupStoreUtils.ts:396-398`.

## 4. State model

### 4.1 Popup store

The Root owns one popup store for its mounted lifetime (PSH-STR-01). Besides the common fields of PSH §4.3, the Tooltip store adds the fields below.

| Field | Type | Initial | Written by |
|---|---|---|---|
| `disabled` | `boolean` | `false` | Root `disabled` prop (TIP-STA-16) |
| `instantType` | `'delay' \| 'dismiss' \| 'focus' \| undefined` | `undefined` | open-change pipeline (TIP-STA-05), Root override (TIP-STA-06/07) |
| `isInstantPhase` | `boolean` | `false` | the Trigger's delay-group membership (TIP-STA-08) |
| `trackCursorAxis` | `'none' \| 'x' \| 'y' \| 'both'` | `'none'` | Root prop (TIP-STA-16) |
| `disableHoverablePopup` | `boolean` | `false` | Root prop (TIP-STA-16) |
| `openChangeReason` | `ChangeEventReason \| null` | `null` | every applied open change (TIP-STA-04) |
| `closeOnClick` | `boolean` | `true` | active Trigger's forwarded state and every Trigger `pointerdown` (TIP-STA-09) |
| `closeDelay` | `number` | `0` | active Trigger's forwarded state (TIP-STA-09) |
| `adaptiveOrigin` | middleware \| `undefined` | `undefined` | PSH viewport (`usePopupViewport`) while a Viewport is mounted |

- **TIP-STA-01** — MUST. A freshly created Tooltip store has the common initial values of PSH-STA-01 plus exactly the Tooltip fields and initial values of the table above; the Root seeds `open` from `defaultOpen`, `openProp` from `open`, `activeTriggerId` from `defaultTriggerId` and `triggerIdProp` from `triggerId`.
  Source: `tooltip/store/TooltipStore.ts:17-27,120-141`; `tooltip/root/TooltipRoot.tsx:48-60`. Test: [L1] `TooltipRoot.test.tsx` popupConformanceTests "opens initially and remains uncontrolled".
- **TIP-STA-02** — MUST. The *effective open state* that every part renders and that `handle.isOpen` reports is `openProp ?? open` (PSH-STA-02) **and** `disabled === false`: a disabled Root is never open, even when `open={true}` or `defaultOpen` is set.
  Source: `tooltip/root/TooltipRoot.tsx:68-69,82`; `utils/popups/store.ts:150`. Test: [L2] `TooltipRoot.test.tsx` "does not throw error when combined with defaultOpen", "should not open when disabled".
  Deviation note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.7, chosen by the user 2026-10-28, ADR 05 Amendments 2026-10-28): the Source citation is corrected: `utils/popups/store.ts:150`'s `open` selector is the plain `openProp ?? open`, and only `tooltip/root/TooltipRoot.tsx:69,82` gate the transitions with `!disabled`. The port keeps the statement as written, which deviates in one corner: for a controlled `Open=true` with `Disabled`, Base UI keeps `data-popup-open` on the trigger and `handle.isOpen` true while the popup is unmounted, where the port reports closed everywhere (`PopupStoreState.EffectiveOpen`).
- **TIP-STA-03** — MUST. The open state is consumer-owned if `open !== undefined` on the Root's first render and component-owned otherwise; the mode is fixed for the instance's lifetime (EVD-STA-01, UTL-STA-22) and switching logs the diagnostics of TIP-EDGE-08. In consumer-owned mode an interaction only calls `onOpenChange` and the popup follows the prop; in component-owned mode the store applies the requested value unless `onOpenChange` canceled it (EVD-STA-08).
  Source: `tooltip/root/TooltipRoot.tsx:36,62`; `utils/popups/popupStoreUtils.ts:246-313`. Test: [L2] `TooltipRoot.test.tsx` "should call onOpenChange when the open state changes"; popupConformanceTests "keeps the popup closed when the open change is canceled", "is ignored when `open={true}` is passed, so Escape cannot close".
- **TIP-STA-04** — MUST. Every applied (non-canceled) open change records its `reason` as `openChangeReason` (`lastOpenChangeReason`), including changes requested by the delay group, the trigger-unmount watcher and the `disabled` close. The value is read by the instant-type override (TIP-STA-06) and by the nested-trigger close rule (TIP-PTR-18).
  Source: `tooltip/store/TooltipStore.ts:23,40,84-91`; see EVD-EVT-19. Test: [L2] `TooltipRoot.test.tsx` "should keep a focus-opened outer tooltip open when hovering over a nested trigger" (branches on the recorded reason).
- **TIP-STA-05** — MUST. On every applied open change the base instant type is derived per EVD-EVT-17: an open with reason `trigger-focus` sets `instantType = 'focus'`; a close with reason `trigger-press` or `escape-key` sets `'dismiss'`; any change with reason `trigger-hover` clears it to `undefined`; every other reason leaves it unchanged.
  Source: `utils/popups/popupStoreUtils.ts:265-269,296-302`. Test: [L4] `TooltipRoot.test.tsx` "marks the popup as instant when opened by focus, but not when opened by hover" (L4 for the `data-instant` observation of the cited test).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L1] → [L4]: the cited title reads `data-instant` after a real focus and a real hover, green on both render modes; the reason table, the stale value included, stays at L1 (`TooltipStoreTests` "port: the instant type follows the reason table of EVD-EVT-17", "port: a close or open with an unmapped reason keeps the stale instant type").
- **TIP-STA-06** — MUST. Post-commit, pre-paint, the Root overrides `instantType` with `'delay'` whenever (a) the transition status is `'ending'` and the last open-change reason is `none` (the tooltip is closing because a grouped sibling opened), or (b) the transition status is not `'ending'` and the store's `isInstantPhase` is `true` (the tooltip opened during the group's instant phase). The value being overridden is remembered (unless it was already `'delay'`).
  Source: `tooltip/root/TooltipRoot.tsx:92-118`. Test: [L4] `TooltipRoot.test.tsx` "toggles instant animations for adjacent tooltips only while opening" (`data-instant="delay"` on the second popup while open, absent while it exits normally) (L4 for the `data-instant` observation of the cited test).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the cited title needs the delay group (the `interactions` module's) and computed transitions; green on `tooltip-provider` on both render modes, with §9's per-mode title "port: data-instant="delay" renders in the batch of data-open on both modes". L1 and L2 keep the override (`TooltipStoreTests`, `TooltipRootTests`).
- **TIP-STA-07** — MUST. When neither condition of TIP-STA-06 holds and a value was remembered, `instantType` is restored to the remembered value (which may be `undefined`) post-commit, pre-paint, and the memory is cleared. An ordinary hover close of a tooltip that opened in the instant phase therefore exits with `instantType` cleared, so its exit transition plays.
  Source: `tooltip/root/TooltipRoot.tsx:114-117`. Test: [L4] `TooltipRoot.test.tsx` "toggles instant animations for adjacent tooltips only while opening" (second popup: `data-ending-style` present, `data-instant` absent, one running transition) (L4 for the `data-instant`/`data-ending-style` observation of the cited test).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: as TIP-STA-06; the restore is kept at L1 (`TooltipStoreTests`).
- **TIP-STA-08** — MUST. Each Trigger registers with the enclosing Provider's delay group using `open = isOpenedByTrigger(ownId)` and writes the group's `isInstantPhase` result (FLT-ANI-01) into the store's `isInstantPhase` post-commit, pre-paint. Without a Provider the value is always `false`.
  Source: `tooltip/trigger/TooltipTrigger.tsx:92-100`; `floating-ui-react/components/FloatingDelayGroup.tsx:216-251`. Test: [L4] `TooltipProvider.test.tsx` "opens an adjacent tooltip instantly while the group is active" (together with TIP-STA-06).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design points 13.5 and 13.6, settled by the orchestrator 2026-10-28, ADR 04 Amendments 2026-10-28): (a) provider-less tooltips share no delay group, as this row states ("without a Provider the value is always `false`"); Base UI 1.9.0 gives them a default group, an untested quirk noted under ADR 04 Amendments 2026-10-27 and reported upstream. (b) Deviation: a Trigger whose hover role is `null` (the Trigger or the Root disabled) leaves its group, where Base UI keeps a disabled Trigger a member; observable only through `OpenAsync(id)` or a controlled `TriggerId` on a disabled Trigger.
- **TIP-STA-09** — MUST. `closeOnClick` and `closeDelay` (resolved per TIP-API-16) are the Trigger's forwarded state (PSH-STA-28/29/30): they are written to the store when the Trigger becomes the active trigger while open and whenever they change while it owns the mounted popup. Additionally, every Trigger writes its `closeOnClick` to the store on its own `pointerdown`, so a press on an inactive trigger evaluates that trigger's `closeOnClick`.
  Source: `tooltip/trigger/TooltipTrigger.tsx:80-89,249`; `utils/popups/popupStoreUtils.ts:339-364,382-391`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` "closes a cursor-tracking tooltip when an inactive trigger is pressed".
- **TIP-STA-10** — MUST. Post-commit, pre-paint of any commit in which the tooltip is effectively open and no active trigger id is resolved, the store's `payload` is cleared to `undefined`.
  Source: `tooltip/root/TooltipRoot.tsx:120-126`. Test: none.
- **TIP-STA-11** — MUST. The Trigger's state object is exactly `{ open }` with `open = isOpenedByTrigger(ownId)` (PSH-STA-08): `true` only while the popup is effectively open **and** this trigger is the active one; it becomes `false` on the closing commit, before the exit animation ends.
  Source: `tooltip/trigger/TooltipTrigger.tsx:73,221`. Test: [L2] `TooltipTrigger.test.tsx` "removes `data-popup-open` as soon as `open` becomes false".
- **TIP-STA-12** — MUST. The Positioner's state object is `{ open, side, align, anchorHidden, instant }` where `instant` is the literal `'tracking-cursor'` whenever `trackCursorAxis !== 'none'`, and otherwise the store's `instantType`. Server: relaxed, see §9.
  Source: `tooltip/positioner/TooltipPositioner.tsx:76-92`. Test: none (`data-instant="tracking-cursor"` is untested; §16 Q2).
- **TIP-STA-13** — MUST. The Popup's state object is `{ open, side, align, instant, transitionStatus }`, the Arrow's `{ open, side, align, uncentered, instant }`, and the Viewport's `{ activationDirection, transitioning, instant }`, where `instant` is the store's `instantType` and `transitionStatus` is the derived PSH-STA-05 status. Server: relaxed, see §9.
  Source: `tooltip/popup/TooltipPopup.tsx:55-61`; `tooltip/arrow/TooltipArrow.tsx:28-34`; `tooltip/viewport/TooltipViewport.tsx:34-38`. Test: [L4] `TooltipViewport.test.tsx` "should mirror the instant animation type of the tooltip"; `TooltipArrow.test.tsx` "is hidden from assistive technology and mirrors the resolved side" (L4 for the mirrored-field timing on Server).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the mirrored fields' timing is a browser observation, green in §9's per-mode title "port: the mirrored side follows the data-side the module writes" and on `tooltip-positioner`; L2 keeps the rendered state (`TooltipRootTests`, `TooltipPopupPartsTests`). The Viewport's clause waits for track 5.
- **TIP-STA-14** — MUST. Provider, Root and Portal expose an empty state object (`{}`); Portal class/style functions receive `{}`.
  Source: `tooltip/provider/TooltipProvider.tsx:26`; `tooltip/root/TooltipRoot.tsx:150`; `tooltip/portal/TooltipPortal.tsx:36`. Test: [L2] `TooltipPortal.test.tsx` describeConformance.
- **TIP-STA-15** — MUST. The Root's dismiss and cursor-tracking interactions (and therefore the interaction attribute bags of PSH-STA-42) are active iff the tooltip is effectively open, or still mounted, or (`!disabled && trackCursorAxis !== 'none'`); a Root with a `handle` but closed, unmounted and not tracking has none (this differs from PSH-STA-43, §16 Q7). The Trigger's hover and focus interactions are independent of this and are always active while the Trigger is not disabled.
  Source: `tooltip/root/TooltipRoot.tsx:137,142-144,249-282`; `tooltip/trigger/TooltipTrigger.tsx:146-166`. Test: [L4] `TooltipRoot.test.tsx` "stops tracking the cursor after trackCursorAxis is disabled while closed".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: cursor tracking is the `interactions` module's and needs layout; green on `tooltip-layout` ("stops tracking the cursor after trackCursorAxis is disabled while closed") on both render modes. L2 keeps the session inputs (`TooltipRootTests`).
- **TIP-STA-16** — MUST. The Root's `trackCursorAxis`, `disableHoverablePopup` and `disabled` props are mirrored into the store post-commit, pre-paint whenever they change (UTL-STA-21), so Trigger/Positioner/Popup parts read the current values.
  Source: `tooltip/root/TooltipRoot.tsx:75-79`. Test: [L4] `TooltipRoot.test.tsx` "should close if open when becoming disabled", "stops tracking the cursor after trackCursorAxis is disabled while closed".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the cursor-tracking clause is the `interactions` module's, green on `tooltip-layout` on both render modes; L2 keeps the disabled close and the session inputs (`TooltipRootTests`).
- **TIP-STA-17** — MUST. `mounted` and `transitionStatus` follow the TRN popup machine configured with `animateInitialOpen = false` (TRN-STA-13: a tooltip that mounts open does not play an enter transition), `enableIdleState = false` and `deferEndingState = false`, keyed on the *effective* open state of TIP-STA-02.
  Source: `tooltip/root/TooltipRoot.tsx:82`; `utils/popups/popupStoreUtils.ts:573-608`; `internals/useUnmountAfterClose.ts:59-64`. Test: [L4] `TooltipRoot.test.tsx` popupConformanceTests "removes the popup when there is no exit animation defined", "hides the kept-mounted popup once the exit animation finishes".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the suite's removal titles need the `transitions` module; green through `popupConformanceTests` on both render modes. L2 keeps the unmount (`TooltipPopupPartsTests` "unmounts the closed popup by default").

### 4.2 Open/close state diagram

```
closed, unmounted ──(open request applied)──▶ open, mounted, status 'starting' ──(next frame)──▶ open, mounted, status unset
        ▲                                                                                              │
        │ on animations finished (Popup) / actions.unmount()                                           │ close request applied
        │                                                                                              ▼
closed, mounted, status 'ending'  ◀────────────────────────────────────────────────────────────────────┘
        │
        └─(open request while 'ending')──▶ open, mounted (no new 'starting'; TRN-STA-09)
```

The Portal subtree exists in the DOM exactly while `mounted` (or `keepMounted`). `data-popup-open` on the Trigger and `data-open` on the popup parts follow the effective open state, not `mounted` (§5).

### 4.3 Invariants

- At most one active trigger; while closed and unmounted no trigger is active (PSH-STA-45).
- `instantType` is `'delay'` only during the delay-group instant phase or a sibling-induced close (TIP-STA-06); it is never `'delay'` during a user-initiated exit.
- The Positioner's `instant` is `'tracking-cursor'` for the whole time `trackCursorAxis !== 'none'`, regardless of how the tooltip opened.

## 5. Rendered DOM and ARIA

### 5.1 Trigger element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| element | `<button type="button">` by default; any element via `render` | TIP-DOM-01 | C# |
| `id` | the trigger id (consumer `id` or generated) | TIP-DOM-01 | C# |
| `data-popup-open` | `""` iff `isOpenedByTrigger(ownId)` | TIP-DOM-02 | C# |
| `data-trigger-disabled` | `""` iff effective `disabled` | TIP-DOM-03 | C# |
| `data-base-ui-tooltip-trigger` | `""` iff **not** disabled (private nested-trigger marker) | TIP-DOM-04 | C# |
| handlers | see TIP-DOM-05 | TIP-DOM-05 | JS |

No `aria-*` attribute, no `tabindex` and no `disabled` attribute is added by the part (§5.7).

Writer note (2026-10-28, M4 track 6, issue 121): the handlers row is named `handlers`, as in the other families' tables; the `interactions` trigger handle attaches them (ADR 04 Amendments 2026-10-27).

- **TIP-DOM-01** — MUST. The Trigger renders a `<button type="button">` unless substituted, with `id` equal to its trigger id (TIP-API-20). It does not use the shared button behavior (no `nativeButton` prop, no `role="button"` emulation, no keyboard activation handling): a substituted non-button element receives no extra attributes beyond those listed in this section.
  Source: `tooltip/trigger/TooltipTrigger.tsx:223-266`; `internals/useRenderElement.tsx:225-228` (RND-STR-05). Test: [L2] `TooltipTrigger.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`, no `button` option); `TooltipPositioner.test.tsx` positionerConformanceTests (trigger substituted with `<div />`).
- **TIP-DOM-02** — MUST. `data-popup-open=""` is present iff the Trigger's `open` state (TIP-STA-11) is `true`; it is removed in the same commit the tooltip stops being effectively open, even if the popup stays mounted (exit animation or `preventUnmountOnClose`).
  Source: `tooltip/trigger/TooltipTrigger.tsx:265`; `utils/popupStateMapping.ts:30-37` (PSH-DOM-01). Test: [L2] `TooltipTrigger.test.tsx` "removes `data-popup-open` as soon as `open` becomes false"; `TooltipRoot.test.tsx` "closes the tooltip when the `close` method is called".
- **TIP-DOM-03** — MUST. `data-trigger-disabled=""` is present iff the Trigger's effective `disabled` (TIP-API-18) is `true`. Other triggers' leave/blur logic reads this attribute to decide whether a pointer/focus destination is an *enabled* trigger (FLT `isTargetInsideEnabledTrigger`).
  Source: `tooltip/trigger/TooltipTrigger.tsx:260`; `tooltip/trigger/TooltipTriggerDataAttributes.ts:10`; `floating-ui-react/utils/element.ts:10-31`. Test: [L2] `TooltipRoot.test.tsx` "marks the trigger as disabled when the root is disabled", "keeps the tooltip disabled when the root is disabled and the trigger opts back in" (attribute absent).
- **TIP-DOM-04** — MUST. `data-base-ui-tooltip-trigger=""` is present iff the Trigger is **not** disabled. It is a private marker used by enclosing Tooltip Triggers to recognize nested enabled triggers (TIP-PTR-17); it is not part of the documented attribute set (§16 Q3) but a port MUST emit it (or an equivalent marker with the same enabled/disabled semantics) for nested-trigger detection to work.
  Source: `tooltip/trigger/TooltipTrigger.tsx:31,127,261`. Test: [L2] `TooltipRoot.test.tsx` "should allow the parent tooltip to open when a nested trigger is disabled", "should not open the outer tooltip when hovering over a nested trigger inside a shadow root".
- **TIP-DOM-05** — MUST. The Trigger element carries, merged in this order (later wins for same-name attributes; handlers are chained per RND §4.11): (1) the hover prop bag — `onPointerDown`, `onPointerEnter` (record pointer type), `onMouseMove` (rest timer) — plus native `mouseenter`/`mouseleave` listeners on the element; (2) the focus prop bag — `onMouseLeave`, `onFocus`, `onBlur`; (3) **only when this trigger owns the mounted popup or `trackCursorAxis !== 'none'`**, the Root's interaction bag — dismiss `onKeyDown` (Escape), `onPointerDown`/`onClick` (reference press), and when tracking the cursor `onPointerDown`/`onPointerEnter`/`onMouseMove`/`onMouseEnter` (cursor anchor); (4) the Trigger's own `onMouseOver`, `onFocus`, `onMouseLeave`, `onPointerEnter`, `onPointerDown`, `onClick` (nested-trigger and `closeOnClick` logic); (5) consumer props.
  Source: `tooltip/trigger/TooltipTrigger.tsx:146-166,216-219,226-264`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:385-389,423-425`; `floating-ui-react/hooks/useFocus.ts:122-126,185`; `floating-ui-react/hooks/useDismiss.ts:775-781`; `floating-ui-react/hooks/useClientPoint.ts:234-245`; `tooltip/store/TooltipStore.ts:44-45`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "closes a cursor-tracking tooltip when an inactive trigger is pressed" (bag (3) present on an inactive trigger only when tracking).

### 5.2 Portal node

- **TIP-DOM-06** — MUST. The Portal renders its portal node (FLT-DOM-11: a `<div>` with a generated `id` and `data-base-ui-portal=""`, appended to the resolved container, carrying the consumer's `className`/`style`/`render`/attributes) and its subtree iff `mounted || keepMounted`; otherwise it renders nothing and the Positioner/Popup/Arrow/Viewport parts do not exist in the DOM.
  Source: `tooltip/portal/TooltipPortal.tsx:19-33`; `utils/FloatingPortalLite.tsx:15-38`. Test: [L2] `TooltipPortal.test.tsx` "unmounts the closed popup by default", "renders the closed popup as hidden instead of unmounting it".

### 5.3 Positioner element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"presentation"` | TIP-ARIA-04 | C# |
| `hidden` | boolean attribute iff `!mounted` (visible only with `keepMounted`) | TIP-DOM-07 | C# |
| `style` | FLT positioning styles (position, inset/transform, CSS variables) + `pointer-events: none` when `!open \|\| trackCursorAxis === 'both' \|\| disableHoverablePopup` + `transition: none` while `'starting'` + consumer style | TIP-DOM-07, TIP-DOM-09, TIP-DOM-10 | C# → JS |
| `data-open` / `data-closed` | from effective open | TIP-DOM-08 | C# |
| `data-side`, `data-align` | rendered placement | TIP-DOM-08 | C# → JS |
| `data-anchor-hidden` | `""` iff anchor hidden | TIP-DOM-08 | JS |
| `data-instant` | `tracking-cursor` \| `delay` \| `focus` \| `dismiss` when defined | TIP-DOM-08 | C# |

Writer note (2026-10-28, M4 track 6, issue 121): `data-instant` is rendered by C# from the store on every part (chosen by the user 2026-10-27, ADR 04 Amendments 2026-10-27; §16 Q2's resolution), so its cells here and in §5.4 read `C#` (sheet D Contradiction 1). `pointer-events: none` while the popup is open and unhoverable is put ahead of the consumer's style, which the layered writer applies as the consumer band; the `positioning` module writes it while closed (FLT-DOM-01).

- **TIP-DOM-07** — MUST. The Positioner is the shared positioner element of FLT-DOM-01 configured with `hidden: !mounted` and `inert: !open || trackCursorAxis === 'both' || disableHoverablePopup`; "inert" here means **only** the inline style `pointer-events: none` (no `inert` attribute). The inline style is `''` (absent) when the popup is open, hoverable and not tracking both axes. Server: relaxed, see §9.
  Source: `tooltip/positioner/TooltipPositioner.tsx:94-101`; `utils/usePositioner.tsx:21-42`. Test: [L4] `TooltipRoot.test.tsx` "applies pointer-events: none to the positioner when `disableHoverablePopup = true`", "does not apply pointer-events: none to the positioner when `disableHoverablePopup = false`", "makes the positioner inert when tracking both axes", "keeps the positioner hoverable when tracking a single axis".
- **TIP-DOM-08** — MUST. The Positioner emits `data-open`/`data-closed` (exactly one) and `data-anchor-hidden` through the PSH popup mapping, and `data-side="<side>"`, `data-align="<align>"`, `data-instant="<instant>"` (when `instant` is defined) through the default state projection (RND-DOM-03/04). The `instant` values on the Positioner are `'tracking-cursor'` (TIP-STA-12) or the store's `'delay' | 'focus' | 'dismiss'`.
  Source: `tooltip/positioner/TooltipPositioner.tsx:76-92,94-101`; `utils/popupStateMapping.ts:48-61`; `tooltip/positioner/TooltipPositionerDataAttributes.ts:6-24`. Test: [L4] `TooltipViewport.test.tsx` "anchors the positioner from the far edge while it transitions" (`data-side="left"`); positionerConformanceTests (`data-side`/`data-align` through FLT).
- **TIP-DOM-09** — MUST. While the transition status is `'starting'` the Positioner carries inline `transition: none` (TRN-DOM-05), merged before the consumer's style.
  Source: `tooltip/positioner/TooltipPositioner.tsx:96`; `utils/usePositioner.tsx:37`; `internals/getDisabledMountTransitionStyles.ts:5-9`. Test: none (TRN).
- **TIP-DOM-10** — MUST. Until the first positioning pass of an open completes, the Positioner's inline style is `position: fixed; top: 0; left: 0; opacity: 0` (FLT-DOM-02/FLT-ANI-02); the `opacity: 0` is removed when the computed position is applied, **before** any consumer CSS transition on the Popup can observe it, so a consumer `transition: opacity` on the Popup does not fade in from 0 on open.
  Source: `internals/useAnchorPositioning.ts:537-539`; `tooltip/positioner/TooltipPositioner.tsx:95`. Test: [L4] `TooltipRoot.test.tsx` "inline opacity: 0 is removed before user CSS transitions run".

### 5.4 Popup element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `tabindex` | `"-1"` | TIP-DOM-11 | C# |
| `data-base-ui-focusable` | `""` | TIP-DOM-11 | C# |
| `data-open` / `data-closed` | from effective open | TIP-DOM-12 | C# |
| `data-starting-style` / `data-ending-style` | from `transitionStatus` | TIP-DOM-12 | JS |
| `data-side`, `data-align` | rendered placement | TIP-DOM-12 | C# |
| `data-instant` | `delay` \| `focus` \| `dismiss` when defined | TIP-DOM-12 | C# |
| `style` | `transition: none` while `'starting'` + `--popup-width/height` (Viewport only) + consumer style | TIP-DOM-13, TIP-DOM-16 | JS |
| handlers | dismiss floating bag (Escape keydown, press-inside marking) + hover floating listeners | TIP-DOM-14 | JS |
| `role` | **none** | TIP-ARIA-01 | C# |

- **TIP-DOM-11** — MUST. The Popup carries `tabindex="-1"` and `data-base-ui-focusable=""` (PSH-DOM-15). It is never focused automatically (TIP-FOC-06); the attributes only make it programmatically focusable and let focus-inside checks (TIP-FOC-02) recognize it.
  Source: `tooltip/popup/TooltipPopup.tsx:67`; `utils/popups/popupStoreUtils.ts:27-30`. Test: none (PSH).
- **TIP-DOM-12** — MUST. The Popup emits `data-open`/`data-closed`, `data-starting-style`/`data-ending-style` (PSH-DOM-05 = popup mapping + TRN-DOM-01/02), and `data-side`, `data-align`, `data-instant` through the default projection. `data-instant` is absent after a hover open and carries `focus` after a keyboard-focus open, `dismiss` after a press/Escape close (while the exit plays), and `delay` per TIP-STA-06.
  Source: `tooltip/popup/TooltipPopup.tsx:55-61,72`; `utils/popupStateMapping.ts:63-70`; `tooltip/popup/TooltipPopupDataAttributes.ts:6-33`. Test: [L4] `TooltipRoot.test.tsx` "marks the popup as instant when opened by focus, but not when opened by hover", "toggles instant animations for adjacent tooltips only while opening".
- **TIP-DOM-13** — MUST. While `'starting'` the Popup carries inline `transition: none` (TRN-DOM-05), merged before consumer props so a consumer inline `transition` wins.
  Source: `tooltip/popup/TooltipPopup.tsx:69`. Test: none (TRN).
- **TIP-DOM-14** — MUST. The Popup receives the Root's popup interaction bag: the dismiss floating handlers (`onKeyDown` Escape, capture-phase press marking so presses inside the popup are not outside presses, FLT-DIS-12/13) and, as native listeners while the popup element exists, `mouseenter`/`mouseleave`/capture `pointerdown` of the hover floating interaction (TIP-PTR-26).
  Source: `tooltip/popup/TooltipPopup.tsx:33,48-51,68`; `tooltip/root/TooltipRoot.tsx:275-279`; `floating-ui-react/hooks/useDismiss.ts:784-800`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:247-255`. Test: [L4] `TooltipRoot.test.tsx` "survives the trip from the trigger to the popup, then closes after leaving it".

### 5.5 Arrow element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `aria-hidden` | `"true"` | TIP-ARIA-03 | C# |
| `data-open` / `data-closed` | from effective open | TIP-DOM-15 | C# |
| `style` | `position: absolute` + the arrow middleware's `top`/`left` + consumer style | TIP-DOM-15 | C# → JS |
| `data-side`, `data-align` | rendered placement | TIP-DOM-15 | C# → JS |
| `data-uncentered` | `""` iff the arrow cannot point at the anchor's center | TIP-DOM-15 | C# → JS |
| `data-instant` | `delay` \| `focus` \| `dismiss` when defined | TIP-DOM-15 | C# |

Writer note (2026-10-28, M4 track 6, issue 121): the Arrow has no twin of its own; its style and placement attributes are written by the `positioning` module from the attach of the Positioner that carries its id, and frozen in C# from then (track 2's `PositionerArrowBehavior`; ADR 04 Amendments 2026-10-27, the Arrow's id through the context).

- **TIP-DOM-15** — MUST. The Arrow renders with inline `position: absolute; top: <y>px; left: <x>px` from the arrow middleware (FLT-DOM-09, merged before the consumer style), `aria-hidden="true"`, `data-open`/`data-closed`, `data-side`, `data-align`, `data-uncentered` (iff the arrow cannot point at the anchor's center) and `data-instant` (when defined). It registers itself as the positioning session's arrow element.
  Source: `tooltip/arrow/TooltipArrow.tsx:22-41`; `tooltip/arrow/TooltipArrowDataAttributes.ts:6-29`; `utils/popupStateMapping.ts:48-61`. Test: [L4] `TooltipArrow.test.tsx` "is hidden from assistive technology and mirrors the resolved side", "is marked uncentered when it cannot point at the anchor", "is not marked uncentered when it can point at the anchor".

### 5.6 Viewport element and containers

- **TIP-DOM-16** — MUST. The Viewport is the PSH viewport (PSH-STR-07, PSH-DOM-07..12): its children render inside a `<div data-current>` child; during a content transition a `<div data-previous inert>` sibling precedes it carrying `--popup-width`/`--popup-height`; the Viewport element emits `data-activation-direction` (when defined), `data-transitioning` (while a previous container exists) and `data-instant` (store instant type). While a Viewport is mounted the Popup gets inline `--popup-width`/`--popup-height` and the Positioner inline `--positioner-width`/`--positioner-height` (PSH-DOM-11), and the Positioner switches to top/left positioning anchored from the far edge of the rendered side (PSH-DOM-12, FLT-DOM-04).
  Source: `tooltip/viewport/TooltipViewport.tsx:28-45`; `utils/usePopupViewport.tsx:21-30,75-110`; `tooltip/viewport/TooltipViewportDataAttributes.ts:4-24`; `tooltip/viewport/TooltipViewportCssVars.ts:6,12`. Test: [L4] `TooltipViewport.test.tsx` viewportConformanceTests (all), "anchors the positioner from the far edge while it transitions", "should mirror the instant animation type of the tooltip"; `TooltipPositioner.test.tsx` positionerConformanceTests "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
- **TIP-DOM-17** — MUST. On every part, a consumer-supplied attribute with the same name as a part-supplied attribute or state data attribute overrides it (RND-DOM-10/19); consumer handlers run after Base UI's and may call `preventBaseUIHandler()` (EVD-EVT-26). In particular a consumer `style={{ pointerEvents }}` on the Positioner overrides TIP-DOM-07 and a consumer `id` on the Popup becomes the popup id (PSH-STA-04).
  Source: `internals/useRenderElement.tsx` (RND); `tooltip/trigger/TooltipTrigger.tsx:263`; `tooltip/popup/TooltipPopup.tsx:70`. Test: [L2] `TooltipRoot.test.tsx` fixtures with `style={{ pointerEvents: 'none' }}` on triggers (detached-triggers suite) (L4 for the Positioner `pointer-events` clause).

### 5.7 ARIA

Tooltip in 1.9.0 wires **no** ARIA relationship between trigger and popup: the popup has no role, the trigger gets no `aria-describedby`, `aria-expanded`, `aria-controls` or `aria-haspopup`. The official docs position the tooltip as a visual-only hint and require the trigger to carry its own accessible name (e.g. `aria-label`). See §16 Q1.

- **TIP-ARIA-01** — MUST. The Popup element carries no `role` attribute (in particular not `role="tooltip"`) unless the consumer supplies one.
  Source: `tooltip/popup/TooltipPopup.tsx:63-73` (no role in the merged props). Test: [L2] `TooltipRoot.test.tsx` popupConformanceTests is invoked without `expectedPopupRole` (the ARIA block of the suite is skipped). Docs: `components/tooltip/page.mdx` §Usage guidelines ("Tooltips alone are not accessible to touch or screen reader users").
- **TIP-ARIA-02** — MUST NOT. The Trigger MUST NOT receive `aria-describedby`, `aria-labelledby` or any other reference to the popup id from the component; the popup id (PSH-STA-04) is maintained in the store but is not rendered on the trigger. Consumers are expected to give the Trigger an accessible name matching the tooltip content.
  Source: `tooltip/trigger/TooltipTrigger.tsx:223-266` (no `aria-*` in the merged props). Test: none. Docs: `components/tooltip/page.mdx` §Usage guidelines ("Provide an accessible name for the trigger").
- **TIP-ARIA-03** — MUST. The Arrow carries `aria-hidden="true"`.
  Source: `tooltip/arrow/TooltipArrow.tsx:39`. Test: [L2] `TooltipArrow.test.tsx` "is hidden from assistive technology and mirrors the resolved side". Docs: CHANGELOG.md v1.0.0-alpha.5 "Apply `aria-hidden` to `Arrow` parts".
- **TIP-ARIA-04** — MUST. The Positioner carries `role="presentation"`.
  Source: `utils/usePositioner.tsx:36`. Test: none (FLT-DOM-01).
- **TIP-ARIA-05** — MUST NOT. The Trigger MUST NOT render `aria-expanded`, `aria-controls` or `aria-haspopup` (PSH-ARIA-01: hover-opened triggers do not), and its open state is exposed only through `data-popup-open`.
  Source: `tooltip/trigger/TooltipTrigger.tsx:259-262`; `test/detachedTriggersConformanceTests.tsx:216-224` (`ariaExpanded: false`). Test: [L4] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "opens with any trigger on $interaction and marks only that trigger as active" (`ariaExpanded: false`).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the suite's interaction loop is a browser one, green on both render modes; L2 keeps the absent attributes (`TooltipTriggerTests`).

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Escape</kbd> | tooltip open; keydown anywhere in the document (bubble phase), on the Trigger or inside the Popup | Close with reason `escape-key`; `preventDefault()` unless canceled; `stopPropagation()` unless `allowPropagation()` | TIP-KEY-01 |
| <kbd>Escape</kbd> | during IME composition (`isComposing`, or within the composition-settle window) | Ignored | TIP-KEY-02 |
| <kbd>Tab</kbd> / <kbd>Shift+Tab</kbd> | Trigger focused | Not intercepted; native focus movement; the resulting blur closes the tooltip (TIP-FOC-02) | TIP-KEY-03 |
| any key that moves focus **onto** a Trigger (Tab, Shift+Tab, arrow keys in a composite) | — | Keyboard focus is focus-visible, so the tooltip opens (TIP-FOC-01) | TIP-KEY-03 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | Trigger focused | Not intercepted by the tooltip (native button activation only); the `click` they generate on a native button runs TIP-PTR-10/11 | TIP-KEY-03 |

- **TIP-KEY-01** — MUST. The Root passes `enabled: !disabled` and the default Escape options (`escapeKey: true`; `bubbles` unset, so Escape does not bubble) to the dismiss mechanism; the observable result is per FLT-KEY-01 and FLT-EVT-05: while the tooltip is effectively open and the Root is not disabled, an <kbd>Escape</kbd> keydown reaching the document (or the Trigger's / Popup's own keydown handler) requests a close with reason `escape-key` and the keyboard event, `preventDefault()` unless canceled and `stopPropagation()` unless `allowPropagation()` was called (EVD-DIS-01/EVD-EVT-11). Server: relaxed, see §9.
  Source: `tooltip/root/TooltipRoot.tsx:260-263`; `floating-ui-react/hooks/useDismiss.ts:201-232,721,775-787`; see FLT-KEY-01, FLT-EVT-05. Test: [L4] `TooltipRoot.test.tsx` "should close when Escape is pressed", "allowPropagation() prevents stopPropagation on Escape while still closing".
- **TIP-KEY-02** — MUST. The dismiss configuration of TIP-KEY-01 keeps the mechanism's IME-composition guard; the observable result is per FLT-KEY-03: an <kbd>Escape</kbd> keydown during an IME composition (`isComposing`, or before the composition-settled timer fires) MUST NOT close the tooltip.
  Source: `tooltip/root/TooltipRoot.tsx:260-263`; `floating-ui-react/hooks/useDismiss.ts:208-214,316-335`; see FLT-KEY-03. Test: none in this component (FLT `useDismiss.test.tsx`).
- **TIP-KEY-03** — MUST. No other key is handled by any Tooltip part: <kbd>Tab</kbd> and activation keys are not intercepted, and keyboard focus arriving on a Trigger opens the tooltip only through the focus path of TIP-FOC-01.
  Source: `tooltip/trigger/TooltipTrigger.tsx:223-266` (no `onKeyDown` of its own). Test: [L4] `TooltipRoot.test.tsx` "should open when the trigger is focused", "should close when the trigger is blurred".
- **TIP-KEY-04** — MUST. Each Trigger passes `enabled: !disabled` and no `delay` to the focus interaction (TIP-FOC-01); the observable result is per FLT-FOC-01/FLT-FOC-03: after an <kbd>Escape</kbd> close (or a press close, TIP-PTR-11) the focus still on the Trigger MUST NOT reopen the tooltip; the next `focus` on that trigger element is ignored until a `mouseleave` or `blur` of the trigger clears the block.
  Source: `tooltip/trigger/TooltipTrigger.tsx:166`; `floating-ui-react/hooks/useFocus.ts:100-114,117-135`; see FLT-FOC-01, FLT-FOC-03. Test: none in this component (FLT `useFocus.test.tsx`).

### 6.2 Pointer and touch

#### 6.2.1 Hover open and close

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| pointer enters Trigger | pointer type `mouse`/`pen` (or unknown); effective delay `d > 0` | Nothing yet (rest-only mode); the open is driven by `mousemove` | TIP-PTR-02 |
| pointer moves over Trigger | `d > 0`, tooltip closed | Start/restart a `d` ms rest timer; a move with squared displacement `< 2` while a timer is pending does not restart it; when it fires, open with reason `trigger-hover`, `trigger` = this element | TIP-PTR-02 |
| pointer enters Trigger | effective delay `0` (explicit `delay={0}`, Provider `delay={0}`, or instant phase) | Open immediately on `mouseenter` with reason `trigger-hover` | TIP-PTR-03, TIP-PTR-04 |
| pointer enters Trigger | pointer type `touch` (last `pointerdown`/`pointerenter`) | Ignored: touch never opens by hover | TIP-PTR-01 |
| pointer leaves Trigger | open by hover, not hoverable-strategy applicable | Close after the effective `closeDelay` (0 → immediately) with reason `trigger-hover` | TIP-PTR-05 |
| pointer leaves Trigger | pending (not yet fired) open | Pending open cancelled | TIP-PTR-05 |
| pointer leaves Trigger toward the Popup | hoverable (`!disableHoverablePopup && trackCursorAxis !== 'both'`) | Safe-polygon corridor keeps it open; leaving the corridor/popup closes after `closeDelay` | TIP-PTR-06 |
| pointer leaves Trigger toward the Popup | `disableHoverablePopup` or `trackCursorAxis='both'` | Closes after `closeDelay` as if leaving to empty space; the Positioner has `pointer-events: none` | TIP-PTR-06 |
| pointer leaves Trigger onto another **enabled** Trigger of the same tooltip | open | Not a close; entering the other trigger re-targets the tooltip immediately (no delay) | TIP-PTR-07 |
| pointer leaves Trigger onto a **disabled** Trigger | open | Closes (a disabled trigger counts as outside) | TIP-PTR-08 |
| pointer re-enters the same Trigger | tooltip closing after a hover close (exit animation running) | Reopens immediately, no delay | TIP-PTR-09 |
| pointer enters the Popup | open, hoverable | Cancels a pending close | TIP-PTR-26 |
| pointer leaves the Popup | open by hover, not onto an enabled trigger | Closes after `closeDelay` (reason `trigger-hover`) | TIP-PTR-26 |

- **TIP-PTR-01** — MUST. Hover opening is restricted to mouse-like pointers: the pointer type recorded by the last `pointerdown`/`pointerenter` on the Trigger must be `'mouse'`, `'pen'`, `''` or unknown; when it is `'touch'`, `mouseenter`/`mousemove` do nothing (FLT-PTR-11 with `mouseOnly: true`). The open delay is not shortened for any pointer type because it is passed as the rest delay, which is resolved without a pointer type; pen pointers get the full open and close delays. The Trigger-leave and Popup-leave close delays are resolved with the recorded pointer type (FLT-API-03), so when that type is `'touch'` the close happens immediately regardless of `closeDelay`.
  Source: `tooltip/trigger/TooltipTrigger.tsx:148,150-156`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-206,219-221,434-436`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:166`; `floating-ui-react/hooks/useHoverShared.ts:31-37`; `floating-ui-react/utils/event.ts:50-58`. Test: [L4] `TooltipRoot.test.tsx` "does not open when a touch pointer hovers the trigger". Docs: `components/tooltip/page.mdx` §Alternatives to tooltips ("tooltips are disabled on touch devices").
- **TIP-PTR-02** — MUST. With an effective open delay `d > 0` (TIP-API-15) the hover interaction is configured in *rest-only* mode (`restMs = d`, no open delay, `move: false`): `mouseenter` alone never opens; each `mousemove` over the Trigger while closed (re)starts a `d` ms timer, except that a move whose `movementX² + movementY² < 2` while a timer is already pending is ignored; when the timer fires and the tooltip is still closed, the pointer has not left, no press cancelled it (TIP-PTR-10) and no nested trigger is hovered (TIP-PTR-17), the tooltip opens with reason `trigger-hover`, the native `mousemove` event and the Trigger element as `trigger`.
  Source: `tooltip/trigger/TooltipTrigger.tsx:146-164`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:224-226,269,285-287,426-497` (FLT-PTR-12/16). Test: [L4] `TooltipRoot.test.tsx` "should open when the trigger is hovered", "should open after rest delay"; `TooltipProvider.test.tsx` "waits for the delay before showing the tooltip".
- **TIP-PTR-03** — MUST. With an effective open delay of `0`, `mouseenter` on a closed tooltip's Trigger opens it immediately (same task) with reason `trigger-hover`; `mousemove` does nothing extra. Server: relaxed, see §9.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:269,295-299,453-456` (FLT-ALG-24). Test: [L4] `TooltipProvider.test.tsx` "respects delay=0"; `TooltipRoot.test.tsx` "should not open when disabled" (`delay: 0` fixture semantics).
- **TIP-PTR-04** — MUST. While the enclosing Provider's delay group is active (a grouped tooltip is open, or closed less than `timeout` ms ago without another member taking over), every grouped Trigger's effective open delay is `0`, so hovering it opens instantly; the previously open member is closed by the group with reason `none` (TIP-DIS-04). When the `timeout` elapses (or the active member's Root unmounts) the group resets and the full delay applies again.
  Source: `tooltip/trigger/TooltipTrigger.tsx:113-117`; `floating-ui-react/components/FloatingDelayGroup.tsx:165-214,216-251` (FLT-ALG-23, FLT-EVT-07). Test: [L4] `TooltipProvider.test.tsx` "opens an adjacent tooltip instantly while the group is active", "requires the full delay again once the timeout elapses"; `TooltipRoot.test.tsx` "keeps the tooltip open when moving across spaced triggers without a closeDelay".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.5): "the enclosing Provider's delay group": without a Provider there is no group (TIP-STA-08's note (a)).
- **TIP-PTR-05** — MUST. `mouseleave` of the Trigger while the tooltip is open by hover (and no hoverable strategy applies) closes it with reason `trigger-hover` after the effective `closeDelay` (immediately when `0`); `mouseleave` while an open is pending cancels the pending open (rest timer and nested-reopen timer). The close timer is cancelled if the pointer re-enters the Trigger (or the Popup, TIP-PTR-26) before it fires.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-206,302-313,352-359` (FLT-PTR-14/15); `tooltip/trigger/TooltipTrigger.tsx:239-243`. Test: [L4] `TooltipRoot.test.tsx` "should close when the trigger is unhovered", "should close after delay", "closes when the trigger is unhovered after opening by default"; `TooltipProvider.test.tsx` "waits for the closeDelay before hiding the tooltip".
- **TIP-PTR-06** — MUST. When `!disableHoverablePopup && trackCursorAxis !== 'both'`, the Trigger's hover interaction uses the safe-polygon close strategy (FLT-ALG-15, created with default options, i.e. `blockPointerEvents: false`): on `mouseleave` a document `mousemove` tracker decides whether the pointer is heading into the Popup; the tooltip closes (after `closeDelay`, reason `trigger-hover`) only when the pointer leaves the corridor/popup, and only if the Trigger that was left is still the active anchor. Otherwise (`disableHoverablePopup` or `'both'`), no strategy is used and leaving the Trigger closes per TIP-PTR-05 even when the pointer moves onto the Popup.
  Source: `tooltip/trigger/TooltipTrigger.tsx:150`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:321-350`; `floating-ui-react/safePolygon.ts:90-451`. Test: [L4] `TooltipRoot.test.tsx` "survives the trip from the trigger to the popup, then closes after leaving it", "closes on that same trip when `disableHoverablePopup` is set".
- **TIP-PTR-07** — MUST. While open, `mouseleave` of the active Trigger whose `relatedTarget` is inside another *enabled* registered Trigger MUST NOT close; `mouseenter` on an inactive enabled Trigger while open (or during a hover-initiated exit) MUST immediately (no delay) re-target the tooltip to that Trigger with reason `trigger-hover` and `trigger` = that element, keeping the popup mounted (PSH-STA-31). A safe-polygon tracker left running from the previous trigger MUST NOT close the re-targeted tooltip (its anchor changed), so moving across spaced triggers with `closeDelay` 0 keeps the tooltip open. Server: relaxed, see §9.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:254-283,317-319,336-342` (FLT-PTR-12/14). Test: [L4] `TooltipRoot.test.tsx` "keeps the tooltip open when moving across spaced triggers without a closeDelay"; `TooltipRoot.detached-triggers.test.tsx` "when hover opens and closes the popup", "should switch to a rendered disabled button trigger when trigger hover is enabled". Docs: CHANGELOG.md v1.0.0 "Fix shared tooltip closing with trigger gaps (#3452)".
- **TIP-PTR-08** — MUST. A disabled Trigger (TIP-API-18) is not an enabled trigger for TIP-PTR-07: leaving the active Trigger onto a disabled Trigger closes the tooltip, and hovering the disabled Trigger opens nothing.
  Source: `floating-ui-react/utils/element.ts:10-31`; `tooltip/trigger/TooltipTrigger.tsx:147,260`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "should close when hovering a disabled trigger while another trigger is open".
- **TIP-PTR-09** — MUST. `mouseenter` on the same Trigger while the tooltip is closing after a hover close (transition status `'ending'`, last close reason `trigger-hover`) reopens it immediately with reason `trigger-hover`, without the rest delay.
  Source: `tooltip/trigger/TooltipTrigger.tsx:159-160` (`isClosing`); `floating-ui-react/hooks/useHoverReferenceInteraction.ts:259-283` (FLT-PTR-12/13). Test: none in this component (FLT `useHoverReferenceInteraction.test.tsx`).

#### 6.2.2 Pressing the Trigger (`closeOnClick`)

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `pointerdown` or `click` on a Trigger | tooltip closed, hover open pending, `closeOnClick` (default) | Pending open cancelled without any `onOpenChange`; the focus that follows does not open | TIP-PTR-10 |
| `pointerdown` or `click` on the active Trigger (any Trigger when tracking the cursor) | tooltip open, `closeOnClick` | Close with reason `trigger-press`; `data-instant="dismiss"` | TIP-PTR-11 |
| same | `closeOnClick={false}` | No effect on the tooltip (pending open proceeds; open stays open) | TIP-PTR-12 |
| hover after a press close | — | `mouseenter`/`mousemove` reopen after the delay as usual | TIP-PTR-13 |

- **TIP-PTR-10** — MUST. A `pointerdown` or `click` on a Trigger with `closeOnClick` while the tooltip is **not** open issues the internal "cancel pending open" notification: an internal open-change event `{ open: false, reason: 'trigger-press' }` that (a) cancels the pending rest/open timers and blocks further `mousemove`-driven opens until the pointer re-enters, and (b) blocks the next `focus` on this trigger (FLT-FOC-03), but **does not** call `onOpenChange` and does not change the stored state (EVD §10 Q10). The pending open therefore never fires.
  Source: `tooltip/trigger/TooltipTrigger.tsx:247-258`; `tooltip/store/TooltipStore.ts:93-99`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:165-187`; `floating-ui-react/hooks/useFocus.ts:100-114`. Test: [L4] `TooltipRoot.test.tsx` "should not open when the trigger was clicked before delay duration", "should not open when the trigger receives pointerdown before delay duration".
- **TIP-PTR-11** — MUST. A `pointerdown` or `click` on a Trigger that carries the Root's interaction bag (the trigger that owns the mounted popup; every trigger when `trackCursorAxis !== 'none'`) while the tooltip is open and the store's `closeOnClick` is `true` closes it with reason `trigger-press` and the native event (dismiss reference press, FLT-PTR-30/FLT-API-07); the instant type becomes `'dismiss'` (TIP-STA-05).
  Source: `tooltip/root/TooltipRoot.tsx:260-263`; `tooltip/trigger/TooltipTrigger.tsx:216-219,249`; `floating-ui-react/hooks/useDismiss.ts:187-199,775-781`. Test: [L4] `TooltipRoot.test.tsx` "should close when the trigger is clicked after delay duration"; `TooltipRoot.detached-triggers.test.tsx` "closes a cursor-tracking tooltip when an inactive trigger is pressed".
- **TIP-PTR-12** — MUST. With `closeOnClick={false}` on the pressed Trigger, neither TIP-PTR-10 nor TIP-PTR-11 applies: a press before the delay elapses does not cancel the open, and a press while open does not close.
  Source: `tooltip/trigger/TooltipTrigger.tsx:250,255`; `tooltip/root/TooltipRoot.tsx:262`. Test: [L4] `TooltipRoot.test.tsx` "should open when the trigger was clicked before delay duration and closeOnClick is false", "should not close when the trigger is clicked after delay duration and closeOnClick is false".
- **TIP-PTR-13** — MUST. After a press close, a subsequent `mouseenter` + `mousemove` on the same Trigger (even without a new `pointerenter`) reopens the tooltip after the effective delay.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:216-218` (`blockMouseMove` cleared on `mouseenter`). Test: [L4] `TooltipRoot.test.tsx` "reopens on hover after the trigger is clicked closed".

#### 6.2.3 Outside press, cursor tracking, pointer bookkeeping

- **TIP-PTR-14** — MUST. The Root passes the default outside-press options (`outsidePress: true`, `outsidePressEvent: 'sloppy'`; `bubbles` unset, so outside presses bubble) with `enabled: !disabled` to the dismiss mechanism; the observable result is per FLT-API-02, FLT-PTR-26/27, FLT-ALG-17 and FLT-DIS-04..08/FLT-DIS-11: while open and not disabled, an outside press (mouse/pen: a `pointerdown` outside the Positioner subtree and the active Trigger; touch: the sloppy touch rules) closes the tooltip with reason `outside-press`; a press on any registered Trigger (enabled or not), on a scrollbar, on an element injected after the popup opened, or inside the popup does not count.
  Source: `tooltip/root/TooltipRoot.tsx:260-263`; `floating-ui-react/hooks/useDismiss.ts:387-520,720-745` (FLT-ALG-17); see FLT-PTR-26, FLT-PTR-27, FLT-DIS-04..08, FLT-DIS-11. Test: [L4] `TooltipRoot.test.tsx` `afterEach` body click (every test relies on it); none asserts the reason directly (FLT `useDismiss.test.tsx`).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); early review item 5): detachedTriggersConformanceTests' "resolves a trigger still registered to the previous root during a transient overlap" runs as written: Base UI changes the phase with `setProps`, and the suite presses the fixture's phase control with `pressControl`, a dispatched Shift keydown on a control marked `data-key-control`, which no dismissal mode reads (PR 140, gap 11 of M4 track 5); a click on that control would be a press outside the open tooltip, which this row closes it on.
- **TIP-PTR-15** — MUST. A press on an inactive registered Trigger is never an outside press (FLT-DIS-05). It closes the tooltip only when that trigger carries the dismiss reference handlers, i.e. when `trackCursorAxis !== 'none'` (TIP-DOM-05 bag (3)) and its `closeOnClick` is `true`; otherwise the press leaves the tooltip open on its current trigger.
  Source: `tooltip/trigger/TooltipTrigger.tsx:216-219,247-253`; `floating-ui-react/hooks/useDismiss.ts:414-423`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "closes a cursor-tracking tooltip when an inactive trigger is pressed".
- **TIP-PTR-16** — MUST. With `trackCursorAxis !== 'none'` and the Root not disabled, the Root passes `enabled: true` and `axis: trackCursorAxis` to the cursor-anchoring mechanism and applies its prop bag to every Trigger; the observable result is per FLT-PTR-07..10 and FLT-ALG-25: `mouseenter`/`mousemove` while closed replace the position anchor with a virtual anchor at the pointer restricted to the configured axis (TIP-POS-03); while open (and, for mouse-like pointers, during the exit) it follows the pointer for moves outside the Positioner; it is not installed for focus opens; it is reset to the trigger when tracking is disabled; and it is rebuilt from the pointer position on every reopen.
  Source: `tooltip/root/TooltipRoot.tsx:264-279`; `floating-ui-react/hooks/useClientPoint.ts:120-250`; see FLT-PTR-07..10, FLT-ALG-25. Test: [L4] `TooltipRoot.test.tsx` "tracks the cursor on the first delayed hover when trackCursorAxis is x", "stops tracking the cursor after trackCursorAxis is disabled while closed", "updates the tracked cursor position after closing and reopening".
- **TIP-PTR-24** — MUST. Pointer-type bookkeeping: the hover interaction records the type of every `pointerdown`/`pointerenter` on a Trigger (shared per Root) and the Trigger keeps its own copy (used by TIP-PTR-19); the Root's hover interaction resets the shared type when the tooltip closes, and the Trigger clears its copy on `mouseleave`, so a touch interaction followed by leaving the trigger does not suppress a later mouse hover.
  Source: `tooltip/trigger/TooltipTrigger.tsx:111,239-246`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:81-88` (FLT-PTR-20). Test: [L4] `TooltipRoot.test.tsx` "should allow mouse hover after leaving a touch interaction".
- **TIP-PTR-25** — MUST. Trigger `disabled` is independent of the rendered element being disabled: a Trigger substituted with a natively disabled or `aria-disabled` element (e.g. a disabled toolbar button) still opens on hover and is a valid hand-off target, provided it receives the pointer events.
  Source: `tooltip/trigger/TooltipTrigger.tsx:311-317` (prop JSDoc), `:146-164`. Test: [L4] `TooltipTrigger.test.tsx` "opens on delayed hover when rendered as a disabled toolbar button"; `TooltipRoot.detached-triggers.test.tsx` "should switch to a rendered disabled button trigger when trigger hover is enabled".
- **TIP-PTR-26** — MUST. The Popup passes `enabled: !disabled` and `closeDelay: store.closeDelay` (TIP-API-16), with no `nodeId`, to the popup-side hover mechanism; the observable result is per FLT-PTR-23/24 (delayed close per FLT-PTR-15): `mouseenter` on the Popup cancels a pending hover close; `mouseleave` whose `relatedTarget` is not inside an enabled Trigger, while the tooltip is hover-open and not click-like open, closes after `closeDelay` with reason `trigger-hover`, delegating to the safe-polygon tracker when one is active.
  Source: `tooltip/popup/TooltipPopup.tsx:48-51`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:165-178,190-233,247-255`; see FLT-PTR-15, FLT-PTR-23, FLT-PTR-24. Test: [L4] `TooltipRoot.test.tsx` "survives the trip from the trigger to the popup, then closes after leaving it".

#### 6.2.4 Nested triggers

A *nested trigger* of Trigger O is an enabled `Tooltip.Trigger` (carrying `data-base-ui-tooltip-trigger`, TIP-DOM-04) that is a DOM descendant of O and is not O itself. Detection happens on `mouseover` events bubbling to O (and on `focus` events): the nearest ancestor-or-self of the event target carrying the marker is looked up through the composed path (crossing shadow roots), falling back to the event target when the composed path is empty.

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| pointer moves onto a nested enabled trigger | outer closed, outer open pending | Outer pending open cancelled; outer will not open while the pointer stays over the nested trigger (including through `mousemove`/safe-polygon paths) | TIP-PTR-17 |
| pointer moves onto a nested enabled trigger | outer open by hover | Outer closes immediately with reason `trigger-hover` | TIP-PTR-18 |
| same | outer open by focus, controlled `open`, or any non-hover reason | Outer stays open; no `onOpenChange` | TIP-PTR-18 |
| pointer moves from a nested trigger back onto O's own area | outer closed, not disabled, mouse-like pointer | Outer reopens after its effective delay (0 → immediately) with reason `trigger-hover` | TIP-PTR-19 |
| pointer leaves O, or re-enters a nested trigger, before that reopen fires | — | Reopen cancelled | TIP-PTR-20 |
| outer is opened by other means before the reopen fires | — | Reopen is a no-op (no second `onOpenChange`) | TIP-PTR-20 |
| pointer moves onto a **disabled** nested trigger | — | Not a nested trigger: the outer opens normally | TIP-PTR-21 |
| `mouseover` inside a nested trigger's shadow root / with empty composed path | — | Still detected | TIP-PTR-21 |
| touch pointer on nested → outer area | — | No local reopen | TIP-PTR-22 |
| hovering the nested tooltip's Popup | outer closed | Outer does not open (the popup is portaled outside O) | TIP-PTR-20 |

- **TIP-PTR-17** — MUST. When a `mouseover` reaching Trigger O targets an enabled nested trigger, O MUST cancel its pending hover open (rest timer, open/close timer and nested-reopen timer) and MUST veto every hover-driven open attempt (mouseenter, rest timer, safe-polygon/mousemove paths; FLT-PTR-12/16 `shouldOpen`) for as long as the nested trigger is the last `mouseover` target. This applies at every nesting depth (a third-level trigger suppresses both ancestors) and to detached outer triggers.
  Source: `tooltip/trigger/TooltipTrigger.tsx:121-144,161-163,231-233`. Test: [L4] `TooltipRoot.test.tsx` "should not open the outer tooltip when hovering over a nested tooltip trigger", "should not open the outer tooltip when moving between sibling nested triggers", "should not open ancestor tooltips when hovering over a third-level nested trigger", "should not open the outer tooltip when the pointer moves onto a nested trigger before the delay expires", "should suppress the safePolygon-driven open while a nested trigger is hovered", "should support nested triggers inside a detached parent trigger", "should support nested triggers with a Provider delay={0}".
- **TIP-PTR-18** — MUST. When a `mouseover` targeting an enabled nested trigger reaches O while O's tooltip is open **and** its last open-change reason is `trigger-hover`, O closes its tooltip immediately (ignoring `closeDelay`) with reason `trigger-hover` and the `mouseover` event — also when the pointer comes from O's own popup. When the tooltip was opened by focus, by the controlled `open` prop, imperatively or by any other reason, it stays open and `onOpenChange` is not called.
  Source: `tooltip/trigger/TooltipTrigger.tsx:177-184`. Test: [L4] `TooltipRoot.test.tsx` "should close the outer tooltip when the pointer moves from outer area onto a nested trigger", "should not open the outer tooltip when moving from the outer popup to a nested trigger", "should keep a focus-opened outer tooltip open when hovering over a nested trigger", "should keep a controlled-open outer tooltip open when hovering over a nested trigger".
- **TIP-PTR-19** — MUST. When a `mouseover` reaching O targets an element inside O that is **not** inside a nested trigger, and the previous `mouseover` target was a nested trigger, O schedules a *local reopen* provided O is not disabled, its tooltip is not open, and the pointer type last recorded on O is mouse-like: after O's effective open delay (immediately when `0`) the tooltip opens with reason `trigger-hover`, the `mouseover` event and O as `trigger`, unless by then a nested trigger is hovered again, O became disabled, or the tooltip is already open. A reopen scheduled with a positive delay restarts the full delay (it does not resume a previous partial delay).
  Source: `tooltip/trigger/TooltipTrigger.tsx:186-213`. Test: [L4] `TooltipRoot.test.tsx` "should open the outer tooltip when moving from a nested trigger to the parent area with zero delay", "should reopen the delayed outer tooltip when moving from a nested trigger to the parent area", "should restart the parent delay when moving from a nested trigger to the parent area", "should not open a disabled outer tooltip when moving from a nested trigger to the parent area with zero delay".
- **TIP-PTR-20** — MUST. The scheduled local reopen is cancelled by `mouseleave` of O and by a later `mouseover` targeting a nested trigger; it is a no-op when the tooltip was opened by other means in the meantime (no additional `onOpenChange`). Hovering the nested tooltip's own popup (portaled outside O) does not schedule a reopen.
  Source: `tooltip/trigger/TooltipTrigger.tsx:137-142,196-200,239-243`. Test: [L4] `TooltipRoot.test.tsx` "should cancel the pending parent reopen when the pointer leaves the parent trigger", "should not reopen the outer tooltip when rapidly moving back to a nested trigger", "should not re-announce an open outer tooltip when the pending reopen fires", "should not reopen the outer tooltip when hovering the nested tooltip popup".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): "should not re-announce an open outer tooltip when the pending reopen fires" is listed open in `tooltip.spec.ts` (`test.skip`). With real input, the pointer reaching the parent area also moves over it, which starts the hover hook's rest timer (`useHoverReferenceInteraction.ts:426-497`; Tooltip's `restMs` is the open delay). That timer's open is not gated by the open state while the popup has no active trigger (`isOverInactiveTrigger` is true for no reference), so Base UI's own code announces the outside-opened tooltip again in a browser. Base UI's title fires no `mousemove` on the parent area. The local reopen's own `!open` guard has no title: an L3 one would pass against `main`, which ADR 11 §3's red check refuses. The row's other three titles are green on both render modes.
- **TIP-PTR-21** — MUST. Nested-trigger detection considers only elements carrying the enabled marker (TIP-DOM-04): a disabled nested trigger does not suppress or close the outer tooltip. The lookup walks the event's composed path (so a target inside a nested trigger's shadow root is found) and falls back to `event.target` when the composed path is empty.
  Source: `tooltip/trigger/TooltipTrigger.tsx:33-36,121-131`; `floating-ui-react/utils/element.ts` (`closest`, UTL-DOM-21). Test: [L4] `TooltipRoot.test.tsx` "should allow the parent tooltip to open when a nested trigger is disabled", "should not open the outer tooltip when hovering over a nested trigger inside a shadow root", "falls back to the event target when the composed path is empty", "should open the outer tooltip when hovering over the non-nested area".
- **TIP-PTR-22** — MUST. The local reopen (TIP-PTR-19) MUST NOT run for a `touch` pointer type recorded on O; the ordinary hover paths already exclude touch (TIP-PTR-01).
  Source: `tooltip/trigger/TooltipTrigger.tsx:193-194,244-246`. Test: [L4] `TooltipRoot.test.tsx` "should not reopen the outer tooltip via the local reopen path for touch pointers".
- **TIP-PTR-23** — MUST. If the hovered Trigger is removed from the document while its hover open is pending, the pending open MUST NOT open the tooltip (the registration is removed and the timer's open is ignored).
  Source: `tooltip/trigger/TooltipTrigger.tsx:80-89,225`; `utils/popups/popupStoreUtils.ts:139-178`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:161,189-212` (listeners torn down with the element). Test: [L4] `TooltipRoot.test.tsx` "does not open once the hovered trigger has unmounted".

### 6.3 Focus management

Tooltip never moves focus: no initial focus, no trap, no return focus, no focus guards. Focus only *opens* and *closes* it.

- **TIP-FOC-01** — MUST. A `focus` event on a non-disabled Trigger whose target matches `:focus-visible` (keyboard focus; see TIP-A11Y-01/02 for the heuristics) opens the tooltip immediately — no delay — with reason `trigger-focus`, the native `FocusEvent` and the Trigger as `trigger`; the instant type becomes `'focus'`. Pointer-initiated focus (not focus-visible) does not open. A focus that is blocked (TIP-KEY-04, TIP-FOC-07) is ignored.
  Source: `tooltip/trigger/TooltipTrigger.tsx:166`; `floating-ui-react/hooks/useFocus.ts:126-168` (FLT-FOC-01, `delay` undefined). Test: [L4] `TooltipRoot.test.tsx` "should open when the trigger is focused", "marks the popup as instant when opened by focus, but not when opened by hover"; `TooltipRoot.detached-triggers.test.tsx` "when focus opens and closes the popup".
- **TIP-FOC-02** — MUST. The Trigger's focus interaction (configuration of TIP-FOC-01) closes on Trigger `blur` per FLT-FOC-04: after a 0 ms macrotask, with reason `trigger-focus` and the native `FocusEvent`, unless focus left the page, moved into the Popup or the Trigger, moved to an outside focus guard, or was handed to another enabled registered Trigger (TIP-FOC-04).
  Source: `tooltip/trigger/TooltipTrigger.tsx:166`; `floating-ui-react/hooks/useFocus.ts:185-233`; see FLT-FOC-04. Test: [L4] `TooltipRoot.test.tsx` "should close when the trigger is blurred", "should close a focus-opened inner tooltip when the inner trigger loses focus".
- **TIP-FOC-03** — MUST. Focus moving from the active Trigger to a **disabled** Trigger of the same tooltip closes it (a disabled trigger is not an enabled trigger for the hand-off exception), and the disabled trigger does not reopen it.
  Source: `floating-ui-react/hooks/useFocus.ts:226-231`; `floating-ui-react/utils/element.ts:10-31`; `tooltip/trigger/TooltipTrigger.tsx:166`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "should close when focusing a disabled trigger while another trigger is open".
- **TIP-FOC-04** — MUST. Focus moving from the active Trigger to another enabled Trigger of the same tooltip hands the open tooltip over: the blur does not close, and the receiving trigger's focus (focus-visible or not, since the tooltip is open and focus came from another enabled trigger) re-targets the tooltip immediately with reason `trigger-focus`, updating the active trigger and payload without unmounting the popup. Server: relaxed, see §9.
  Source: `floating-ui-react/hooks/useFocus.ts:151-167,226-230`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "hands off open state and payload to a trigger with its own DOM id while open"; detachedTriggersConformanceTests "opens with any trigger on focus and marks only that trigger as active".
- **TIP-FOC-05** — MUST. A `focus` event that reaches an outer Trigger O but targets an enabled nested trigger (TIP-DOM-04) MUST NOT open O's tooltip (O suppresses its own focus handling for that event); the nested tooltip opens and later closes on its own blur.
  Source: `tooltip/trigger/TooltipTrigger.tsx:234-238`. Test: [L4] `TooltipRoot.test.tsx` "should not open the outer tooltip when focusing a nested tooltip trigger", "should close a focus-opened inner tooltip when the inner trigger loses focus".
- **TIP-FOC-06** — MUST NOT. Opening a tooltip MUST NOT move focus (the Popup, although `tabindex="-1"`, is not focused), and closing MUST NOT restore or move focus; no focus trap or focus guard is rendered by any Tooltip part.
  Source: `tooltip/popup/TooltipPopup.tsx:63-73` (no focus manager); `tooltip/portal/TooltipPortal.tsx:31` (lite portal without guards, PSH-STR-11). Test: none (absence).
- **TIP-FOC-07** — MUST. The Trigger's focus interaction (configuration of TIP-FOC-01) keeps the window-blur guard of FLT-FOC-02: when the window blurs while the tooltip is closed and a Trigger is the active element, returning to the tab does not open the tooltip; `mouseleave` or `blur` of the Trigger clears the block.
  Source: `tooltip/trigger/TooltipTrigger.tsx:166`; `floating-ui-react/hooks/useFocus.ts:68-78,117-120`; see FLT-FOC-02. Test: none in this component (FLT `useFocus.test.tsx` "does not reopen when focus is restored after leaving the tab").
- **TIP-FOC-08** — MUST. A disabled Trigger (TIP-API-18) MUST NOT open on focus, including when the Root is disabled.
  Source: `tooltip/trigger/TooltipTrigger.tsx:166`. Test: [L4] `TooltipRoot.test.tsx` "should not open on focus when the trigger is disabled", "should not open when disabled".

### 6.4 Dismissal

| Trigger of dismissal | Reason | Req |
|---|---|---|
| <kbd>Escape</kbd> | `escape-key` | TIP-DIS-01 |
| outside press (sloppy) | `outside-press` | TIP-DIS-02 |
| scroll, resize, window blur | — (never) | TIP-DIS-03 |
| another grouped tooltip opens | `none` | TIP-DIS-04 |
| the active Trigger unmounts | `none` | TIP-DIS-05 |
| Root `disabled` becomes `true` | `disabled` | TIP-DIS-06 |
| `actionsRef.current.close()`, `handle.close()` | `imperative-action` | TIP-DIS-07 |
| pointer leave / blur / trigger press | `trigger-hover` / `trigger-focus` / `trigger-press` | §6.2, §6.3 |

- **TIP-DIS-01** — MUST. Escape dismissal per TIP-KEY-01; it is disabled while the Root is `disabled`.
  Source: `tooltip/root/TooltipRoot.tsx:260-261`. Test: [L4] `TooltipRoot.test.tsx` "should close when Escape is pressed".
- **TIP-DIS-02** — MUST. Outside-press dismissal per TIP-PTR-14; disabled while the Root is `disabled`.
  Source: `tooltip/root/TooltipRoot.tsx:260-261`. Test: none direct (see TIP-PTR-14).
- **TIP-DIS-03** — MUST NOT. Scrolling, resizing, or the window losing focus MUST NOT close an open tooltip (there is no scroll/resize dismissal; the positioning layer only repositions, FLT-POS-11). A window blur while open leaves it open (TIP-FOC-02 "focus left the page").
  Source: `tooltip/root/TooltipRoot.tsx:249-282` (only dismiss + client point); `floating-ui-react/hooks/useFocus.ts:203-206`. Test: none (absence).
- **TIP-DIS-04** — MUST. When another tooltip in the same Provider group opens while this one is the group's active member, this one is closed by the group with reason `none` and no native event (`event.type === 'base-ui'`, EVD-API-04); the close is applied through the normal pipeline (so `onOpenChange(false, { reason: 'none' })` is called and may be canceled) and the closing tooltip's instant type becomes `'delay'` for its exit (TIP-STA-06).
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:234-238` (FLT-EVT-07); `tooltip/root/TooltipRoot.tsx:103-113`. Test: [L4] `TooltipProvider.test.tsx` "opens an adjacent tooltip instantly while the group is active" (first content gone); `TooltipRoot.test.tsx` "unmounts an exiting tooltip when another tooltip opens".
- **TIP-DIS-05** — MUST. When the resolved active Trigger unregisters while the tooltip is open (contained or detached), a close with reason `none` is requested one microtask later unless a trigger with the same id registered meanwhile (PSH-STA-23/24, EVD-EVT-13); if the close is canceled the tooltip keeps its previous trigger association and `data-popup-open` does not move to another trigger (PSH-STA-25). Server: relaxed, see §9.
  Source: `tooltip/root/TooltipRoot.tsx:81`; `utils/popups/popupStoreUtils.ts:484-490,524-546`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "should close when the active trigger unmounts", "should close when the active detached trigger unmounts", "should remain open when the active trigger unmount close is canceled" (L4 for the one-microtask timing clause). Docs: CHANGELOG.md v1.5.0 "Close when active trigger unmounts (#4886)".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the close runs at the render-batch boundary after the trigger's element leaves; green on `tooltip-root` (the detached case) and §9's per-mode title "port: the trigger-unmount close lands a round trip after the trigger leaves on Server". L2 keeps the store's clause (`TooltipDetachedTriggersTests`).
- **TIP-DIS-06** — MUST. Post-commit, pre-paint of a commit in which the stored open state is `true` and `disabled` is `true`, the Root requests a close with reason `disabled` and no native event; the tooltip is already not effectively open from that commit on (TIP-STA-02). `defaultOpen` together with `disabled` therefore renders closed and throws nothing. Server: relaxed, see §9.
  Source: `tooltip/root/TooltipRoot.tsx:96-100`. Test: [L2] `TooltipRoot.test.tsx` "should close if open when becoming disabled", "does not throw error when combined with defaultOpen". Docs: CHANGELOG.md v1.0.0-beta.1 "Fix error when combining `defaultOpen` and `disabled` (#2374)".
- **TIP-DIS-07** — MUST. `actionsRef.current.close()` and `handle.close()` request a close with reason `imperative-action`, no native event and no trigger; `handle.close()` is ignored (with the PSH dev warning) while no Root is attached.
  Source: `tooltip/root/TooltipRoot.tsx:132`; `tooltip/store/TooltipHandle.ts:38-40`; `utils/popups/popupHandle.ts:271-285`. Test: [L2] `TooltipRoot.test.tsx` "closes the tooltip when the `close` method is called"; `TooltipRoot.detached-triggers.test.tsx` "when opened and closed imperatively".
- **TIP-DIS-08** — MUST. An Escape close does not propagate to ancestor popups unless `allowPropagation()` was called (TIP-KEY-01), and an open nested tooltip (a tooltip whose Root sits inside another tooltip's Trigger) is an independent popup: Escape closes whichever tooltip(s) are open; the outer is not notified of the inner's dismissal. Server: relaxed, see §9.
  Source: `floating-ui-react/hooks/useDismiss.ts:228-230`; `tooltip/root/TooltipRoot.tsx:48-60` (no floating-tree node registration). Test: [L4] `TooltipRoot.test.tsx` "allowPropagation() prevents stopPropagation on Escape while still closing".

### 6.5 Focus-visible and assistive-technology heuristics

- **TIP-A11Y-01** — MUST. "Keyboard focus" for TIP-FOC-01 is decided by `element.matches(':focus-visible')` on the focused target at the time of the `focus` event; when the environment cannot evaluate the selector (it throws) or is a non-visual test DOM, the focus is treated as focus-visible.
  Source: `floating-ui-react/utils/element.ts:71-82`; `floating-ui-react/hooks/useFocus.ts:139-149`. Test: [L4] `TooltipRoot.test.tsx` "should open when the trigger is focused" (skipped in jsdom precisely because of this heuristic).
- **TIP-A11Y-02** — SHOULD. The Trigger's focus interaction (configuration of TIP-FOC-01) keeps the Mac Safari modality rule of FLT-A11Y-06: a `focus` with no `relatedTarget` opens the tooltip only if the last input modality was keyboard or the target is typeable.
  Source: `tooltip/trigger/TooltipTrigger.tsx:166`; `floating-ui-react/hooks/useFocus.ts:23,49-52,80-91,142-145`; see FLT-A11Y-06. Test: none.
- **TIP-A11Y-03** — MUST NOT. Tooltip MUST NOT announce its content: no live region is rendered, the popup has no role or relationship to the trigger (§5.7), and touch users cannot open it (TIP-PTR-01). Consumers are directed to give the Trigger an accessible name and to use Popover/Toast for content that must reach assistive technology.
  Source: `tooltip/popup/TooltipPopup.tsx:63-73`; docs `components/tooltip/page.mdx` §Usage guidelines, §Alternatives to tooltips. Test: none.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `Root.onOpenChange` | `(open: boolean, details: Tooltip.Root.ChangeEventDetails) => void` | Synchronously inside the handling of the originating input, before any state/DOM update (EVD-EVT-02) | `trigger-hover`, `trigger-focus`, `trigger-press`, `outside-press`, `escape-key`, `disabled`, `imperative-action`, `none` | yes (`details.cancel()`); also `preventUnmountOnClose()` |
| `Root.onOpenChangeComplete` | `(open: boolean) => void` | After the Popup's enter animation (`true`) / after unmount (`false`) | — | no |

| Reason | `open` | User action / origin | `details.event` | `details.trigger` |
|---|---|---|---|---|
| `trigger-hover` | `true` | rest delay elapsed (`mousemove`), immediate `mouseenter` (delay 0 / instant phase / hand-off / re-entry during exit), nested-trigger local reopen (`mouseover`) | `MouseEvent` | the Trigger element |
| `trigger-hover` | `false` | pointer left Trigger/Popup/safe polygon (after `closeDelay`); pointer entered a nested trigger | `MouseEvent` | `undefined` |
| `trigger-focus` | `true` | keyboard (focus-visible) focus on a Trigger; focus hand-off between Triggers | `FocusEvent` | the Trigger element |
| `trigger-focus` | `false` | Trigger blur (0 ms later) | `FocusEvent` | `undefined` |
| `trigger-press` | `false` | `pointerdown`/`click` on the active Trigger while open with `closeOnClick` | `PointerEvent`/`MouseEvent` | `undefined` |
| `outside-press` | `false` | sloppy outside press | `PointerEvent`/`MouseEvent`/`TouchEvent` | `undefined` |
| `escape-key` | `false` | Escape keydown | `KeyboardEvent` | `undefined` |
| `disabled` | `false` | Root `disabled` became `true` while open | generic `base-ui` event | `undefined` |
| `imperative-action` | `true` | `handle.open(triggerId)` | generic `base-ui` event | the registered Trigger element |
| `imperative-action` | `false` | `actionsRef.current.close()`, `handle.close()` | generic `base-ui` event | `undefined` |
| `none` | `false` | another grouped tooltip opened; the active Trigger unmounted | generic `base-ui` event | `undefined` |

- **TIP-EVT-01** — MUST. `onOpenChange(nextOpen, details)` is called with a change-event details object (EVD-API-01) extended with `preventUnmountOnClose()` (EVD-API-09), whose `reason` is one of the eight literals of §3.10 under exactly the conditions of the table above; no other reason is ever emitted by Tooltip.
  Source: `tooltip/root/TooltipRoot.tsx:225-239`; emitters: `tooltip/trigger/TooltipTrigger.tsx:182,198` and FLT hover (`useHoverReferenceInteraction.ts:198,203,280,292,297,482`), `useFocus.ts:159-183,231`, `useDismiss.ts:192-198,220-222,518`, `tooltip/root/TooltipRoot.tsx:98,132`, `utils/popups/popupHandle.ts:259-262,284`, `floating-ui-react/components/FloatingDelayGroup.tsx:237`, `utils/popups/popupStoreUtils.ts:533-534`. Test: [L4] `TooltipRoot.test.tsx` popupConformanceTests "is called with the new open state and the change details" (`trigger-hover`), "should close when Escape is pressed" (`escape-key`), "closes the tooltip when the `close` method is called" (`imperative-action`); `TooltipRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled" (`none`).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the hover and Escape titles are browser-only, green through `popupConformanceTests` and `tooltip.spec.ts` on both render modes; L2 keeps the recorded reasons (`TooltipRootTests`, `TooltipDetachedTriggersTests`).
- **TIP-EVT-02** — MUST. `onOpenChange` runs before the store, the DOM and the internal open-change notification are updated (EVD-STA-08, EVD-EVT-07); a `trigger-hover` change is committed synchronously before the handler returns (EVD-EVT-18); other reasons commit in the ordinary update cycle. Server: relaxed, see §9.
  Source: `utils/popups/popupStoreUtils.ts:271-281,306-312`. Test: [L2] `TooltipRoot.test.tsx` "should call onOpenChange when the open state changes" (handler observes the pre-change `open`) (L4 for the `trigger-hover` synchronous-commit clause).
- **TIP-EVT-03** — MUST. Calling `details.cancel()` inside `onOpenChange` prevents the change in component-owned mode (nothing opens/closes, no instant type change, no trigger re-association, no internal notification — EVD-EVT-05/07); in consumer-owned mode the component never commits anyway and the consumer decides. A canceled Escape close leaves the keydown's default action intact (EVD-EVT-11). Server: relaxed, see §9.
  Source: `utils/popups/popupStoreUtils.ts:273-277`; `floating-ui-react/hooks/useDismiss.ts:224-226`. Test: [L4] `TooltipRoot.test.tsx` popupConformanceTests "keeps the popup closed when the open change is canceled"; `TooltipRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled" (L4 for the Escape default-action clause). Docs: `experiments/tooltip/prevent-open.tsx` (cancels `trigger-press`).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the Escape default-action clause is §9's per-mode title "port: a canceled Escape close keeps the tooltip open on both modes and keeps the key's default only on WebAssembly"; L2 keeps the canceled change (`TooltipRootTests`, `TooltipDetachedTriggersTests`).
- **TIP-EVT-04** — MUST. `details.preventUnmountOnClose()` called during a non-canceled close keeps the popup mounted (with `data-ending-style`, and `data-popup-open` already removed) until `actionsRef.current.unmount()` is called (TRN-EVT-07/08, TRN-ANI-23); it applies to that close only — the next close without the call unmounts normally (TRN-ANI-24).
  Source: `utils/popups/popupStoreUtils.ts:228-236,271,283-289`; `tooltip/root/TooltipRoot.tsx:131`. Test: [L2] `TooltipRoot.test.tsx` "does not prevent unmounting on later closes", "unmounts the tooltip when the `unmount` method is called", "unmounts on a normal close after a prevented close and initially open remount"; `TooltipTrigger.test.tsx` "removes `data-popup-open` as soon as `open` becomes false" (L4 for the `data-ending-style` clause).
- **TIP-EVT-05** — MUST. `onOpenChangeComplete(true)` fires once per open on animations finished for the Popup element (TRN-EVT-01), including for a tooltip that mounts open (TRN-EVT-04); `onOpenChangeComplete(false)` fires once per close from the unmount cleanup (TRN-EVT-02, PSH-STA-45) — after the Popup's exit animation or when `unmount()` is called; it never fires on a closed mount and a reopen during the exit cancels the pending `false` (TRN-EVT-05). Server: relaxed, see §9.
  Source: `tooltip/popup/TooltipPopup.tsx:38-46`; `utils/popups/popupStoreUtils.ts:586-595`. Test: [L4] `TooltipRoot.test.tsx` popupConformanceTests "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes".
- **TIP-EVT-06** — MUST NOT. `onOpenChange` MUST NOT be called for changes of the controlled `open` prop or for the initial `defaultOpen` state (EVD-STA-06), nor when the consumer supplies the same `open` value again.
  Source: `tooltip/root/TooltipRoot.tsx:62` (prop sync bypasses the pipeline); `utils/popups/store.ts:150`. Test: [L4] `TooltipRoot.test.tsx` "should keep a controlled-open outer tooltip open when hovering over a nested trigger" (`onOpenChange` never called); `TooltipRoot.detached-triggers.test.tsx` "keeps a default-open root open while a detached trigger migrates after the initial commit".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the nested-trigger hover is the `interactions` module's, green on `tooltip-nested` on both render modes; L2 keeps the trigger migration (`TooltipDetachedTriggersTests`).
- **TIP-EVT-07** — MUST NOT. Re-hovering (mouseenter/mousemove) the Trigger that already owns the open tooltip MUST NOT request another open: `onOpenChange` is not called again and the rest timer is not started while open over the active trigger.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:274,454-456` (`shouldOpen = !isOpen || isOverInactive`). Test: [L4] `TooltipRoot.test.tsx` "does not call onOpenChange again when the trigger is re-hovered while open".
- **TIP-EVT-08** — MUST. The "cancel pending open" press (TIP-PTR-10) emits an **internal** open-change notification `{ open: false, reason: 'trigger-press' }` to the Root's interactions without calling `onOpenChange` and without changing the stored state; a port must reproduce its two effects (hover timers cancelled and mousemove blocked until re-entry; next focus on that trigger blocked) without surfacing a callback.
  Source: `tooltip/store/TooltipStore.ts:93-99`; `floating-ui-react/components/FloatingRootStore.ts:106-118`; see EVD §10 Q10. Test: [L4] `TooltipRoot.test.tsx` "should not open when the trigger was clicked before delay duration" (no `Content`, and no callback assertion exists — §16 Q5).
- **TIP-EVT-09** — MUST. For hover/focus opens `details.trigger` is the Trigger element that received the event (so `details.trigger?.id` identifies the trigger for a controlled `triggerId`); for `handle.open(id)` it is the registered element; for every close it is `undefined` (the store keeps the previous active trigger for the exit, PSH-STA-13). `details.event` is the native event for input-driven reasons and a generic `base-ui` event for `disabled`, `imperative-action` and `none` (EVD-API-04/05).
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:280,292,297,482`; `floating-ui-react/hooks/useFocus.ts:159-165`; `utils/popups/popupHandle.ts:259-262`; `tooltip/root/TooltipRoot.tsx:98,132`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "allows controlling the open state and active trigger programmatically". Docs: `components/tooltip/page.mdx` §Controlled mode with multiple triggers (`eventDetails.trigger?.id`).
- **TIP-EVT-10** — MUST. Calling `details.allowPropagation()` inside `onOpenChange` for an `escape-key` close lets the keydown propagate (no `stopPropagation()`) while the tooltip still closes; `cancel()` and `allowPropagation()` are independent. Server: relaxed, see §9.
  Source: `floating-ui-react/hooks/useDismiss.ts:228-230` (FLT-EVT-05). Test: [L4] `TooltipRoot.test.tsx` "allowPropagation() prevents stopPropagation on Escape while still closing".

## 8. Form integration

Not applicable — Tooltip has no value, renders no form control and does not participate in Field/Form.

## 9. Lifecycle, mounting and animation

Vocabulary: *post-commit, pre-paint*, *next frame*, *microtask* and *on animations finished* as defined in conventions §4. "Exit" = the period during which the tooltip is closed but still mounted (`transitionStatus === 'ending'`).

- **TIP-ANI-01** — MUST. The Portal subtree is mounted when the tooltip becomes effectively open and stays mounted through the exit until on animations finished for the Popup element (TRN-ANI-03), then unmounts in a synchronous commit before the next paint; with `keepMounted` it stays in the DOM and the Positioner gets `hidden` instead (TRN-ANI-01, TIP-DOM-06/07). A closed tooltip with no exit animation unmounts on the next frame plus one microtask. Server: relaxed, see §9.
  Source: `tooltip/root/TooltipRoot.tsx:82`; `tooltip/portal/TooltipPortal.tsx:24-27`; `utils/popups/popupStoreUtils.ts:573-608`. Test: [L4] `TooltipRoot.test.tsx` popupConformanceTests "removes the popup when there is no exit animation defined", "hides the kept-mounted popup once the exit animation finishes"; `TooltipPortal.test.tsx` "renders the closed popup as hidden instead of unmounting it".
- **TIP-ANI-02** — MUST. On open the Popup and Positioner carry `data-starting-style` (and inline `transition: none`) for the opening commit and lose both on the next frame (TRN-STA-04, TRN-DOM-05); on close they carry `data-ending-style` for the whole exit (TRN-DOM-02). A tooltip that is open on its first render does not go through `'starting'` (TRN-STA-13).
  Source: `tooltip/popup/TooltipPopup.tsx:69,72`; `utils/usePositioner.tsx:37-40`; `internals/useUnmountAfterClose.ts:59-64`. Test: [L4] `TooltipRoot.test.tsx` "toggles instant animations for adjacent tooltips only while opening" (`data-starting-style` removed, `data-ending-style` set on close).
- **TIP-ANI-03** — MUST. Delay-group instant animations: a grouped tooltip that opens while the group is active (TIP-PTR-04) renders `data-instant="delay"` on Popup, Arrow, Viewport and Positioner from its opening commit (consumers disable transitions on `[data-instant]`), so it appears without an enter transition; when it later closes for an ordinary reason the attribute is absent during the exit and the exit transition plays (TIP-STA-06/07). The previously active member, closed with reason `none`, renders `data-instant="delay"` during *its* exit.
  Source: `tooltip/root/TooltipRoot.tsx:87-118`. Test: [L4] `TooltipRoot.test.tsx` "toggles instant animations for adjacent tooltips only while opening". Docs: `components/tooltip/demos/hero/css-modules/index.module.css` (`&[data-instant] { transition: none }`); CHANGELOG.md v1.0.0-beta.5 "Fix `data-instant` ending transition of same tooltip (#2962)".
- **TIP-ANI-04** — MUST. When a grouped tooltip is still exiting (exit transition running) and another grouped tooltip opens, the exiting one is unmounted promptly: its `data-instant="delay"` lets consumer CSS cancel the transition, the animations-finished wait completes, and the element leaves the DOM (while the group's `timeout` keeps the group active). Server: relaxed, see §9.
  Source: `tooltip/root/TooltipRoot.tsx:103-113`; `floating-ui-react/components/FloatingDelayGroup.tsx:234-238`. Test: [L4] `TooltipRoot.test.tsx` "unmounts an exiting tooltip when another tooltip opens".
- **TIP-ANI-05** — MUST. `actionsRef.current.unmount()` ends the closing phase immediately (TRN-EVT-08): the popup unmounts (or hides with `keepMounted`) and `onOpenChangeComplete(false)` fires; it is a no-op when already unmounted and deferred to the commit when called while open (TRN-ANI-20/21).
  Source: `tooltip/root/TooltipRoot.tsx:131`; `internals/useUnmountAfterClose.ts:102-116`. Test: [L2] `TooltipRoot.test.tsx` "unmounts the tooltip when the `unmount` method is called".
- **TIP-ANI-06** — MUST. A `preventUnmountOnClose()` opt-out belongs to one close cycle: reopening (by any route) clears it (TRN-ANI-24), the next close unmounts normally, and a Root unmounted while kept mounted and remounted open (`defaultOpen` + reused handle) starts a fresh cycle whose first normal close unmounts (PSH-STA-40).
  Source: `utils/popups/popupStoreUtils.ts:195-201`; `internals/useUnmountAfterClose.ts:66-76`. Test: [L4] `TooltipRoot.test.tsx` "does not prevent unmounting on later closes", "unmounts on a normal close after a prevented close and initially open remount". Docs: CHANGELOG.md v1.5.0 "Reset preventUnmountOnClose on reopen (#4885)", v1.6.0 "Prevent `<Tooltip.Root>` from reopening after remounting with a reused handle (#5149)".
- **TIP-ANI-07** — MUST. The Positioner's pre-positioning `opacity: 0` (TIP-DOM-10) is removed by the first positioning pass of each open, before the Popup's `data-starting-style` frame ends, so consumer opacity transitions on the Popup start from the consumer's own starting style rather than from an inherited `0`; a consumer `transition: opacity` on the Popup produces no running opacity transition after an open with no starting style.
  Source: `internals/useAnchorPositioning.ts:537-539` (FLT-ANI-02). Test: [L4] `TooltipRoot.test.tsx` "inline opacity: 0 is removed before user CSS transitions run".
- **TIP-ANI-08** — MUST. The instant type produced by TIP-STA-05 is observable as `data-instant="focus"` from the opening commit of a keyboard-focus open (so consumers can skip the enter transition for keyboard users) and as `data-instant="dismiss"` during the exit after a press or Escape close; a hover open or hover close renders no `data-instant`.
  Source: `utils/popups/popupStoreUtils.ts:296-302`; `tooltip/popup/TooltipPopup.tsx:59`. Test: [L4] `TooltipRoot.test.tsx` "marks the popup as instant when opened by focus, but not when opened by hover".
- **TIP-ANI-09** — MUST. Switching the active trigger while open (hover/focus hand-off, `handle.open(otherId)`) keeps the Popup and Positioner elements mounted (PSH-STA-31, TRN-ANI-28 does not apply to Tooltip — no trigger-switch transition styles are added); with a Viewport the content transition of PSH-DOM-10 runs and the Positioner/Popup size variables of PSH-DOM-11 animate the move.
  Source: `tooltip/positioner/TooltipPositioner.tsx:57-74` (no `useTriggerSwitchTransition`); `tooltip/viewport/TooltipViewport.tsx:28-32`. Test: [L4] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "reuses the popup DOM nodes when switching triggers", "does not leave an inline scale style on the popup after switching triggers"; `TooltipViewport.test.tsx` viewportConformanceTests "creates morphing containers during transitions". Docs: `components/tooltip/page.mdx` §Animating the Tooltip.
- **TIP-ANI-10** — MUST. The Viewport mirrors the store's instant type (`data-instant` with the same value and timing as the Popup) so content transitions can be disabled together with the popup transitions.
  Source: `tooltip/viewport/TooltipViewport.tsx:26,37`. Test: [L4] `TooltipViewport.test.tsx` "should mirror the instant animation type of the tooltip".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- TIP-STA-12 — RND-STA-01 for mirrored fields: `data-side`, `data-align`, `data-anchor-hidden` and `data-instant` are JS-written at frame timing; the Positioner state's `side`, `align`, `anchorHidden` and `instant` (and so `ClassValue`/`StyleValue`) update one round trip later.
- TIP-STA-13 — RND-STA-01 for mirrored fields: the Popup/Arrow/Viewport state's `side`, `align`, `uncentered`, `instant` and `transitionStatus` update one round trip after the JS-written attributes.
- TIP-POS-07 — RND-STA-01 for mirrored fields: the post-collision `side`/`align`/`anchorHidden` reach the state objects one round trip after `data-side`/`data-align`/`data-anchor-hidden` are written.
- TIP-PTR-03 — EVD-EVT-18, PSH-ANI-03: the `trigger-hover` open commit lands a round trip after `mouseenter`; same-task animation queries do not see it.
- TIP-EVT-02 — EVD-EVT-18, PSH-ANI-03: the `trigger-hover` change is committed a round trip later, not before the handler returns.
- TIP-KEY-01 — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after dispatch; JS prevents and stops the Escape keydown whenever the tooltip is the innermost open popup (ADR 04 §3 fallback); WebAssembly meets the spec through `decide()`.
- TIP-DIS-08 — EVD-DIS-01, FLT-KEY-01: as TIP-KEY-01; an `allowPropagation()` call does not reach the dispatch on Server, so the keydown is stopped per the fallback policy.
- TIP-EVT-03 — EVD-EVT-11: a canceled Escape close cannot restore the default action on Server; JS applies the fallback policy (prevent when innermost open).
- TIP-EVT-10 — EVD-DIS-01: `allowPropagation()` arrives after dispatch; the keydown is stopped per the fallback policy while the tooltip still closes.
- TIP-DIS-05 — PSH-STA-23: the close-on-trigger-unmount check runs in `OnAfterRender`, after paint plus a round trip, during which the popup stays open.
- TIP-ANI-01 — TRN-ANI-03 (adopted via TRN): removal after animations finished lands in the first render batch after the completion message, about two round trips later; the optimistic convergent write hides the element meanwhile.
- TIP-ANI-04 — TRN-ANI-09: the exiting tooltip leaves the DOM in the first render batch after the completion message, about two round trips later; the convergent write hides it meanwhile.
- TIP-EVT-05 — TRN-ANI-09, TRN-ANI-10: `onOpenChangeComplete` runs when the completion message reaches C#, about one round trip after the animation ends, not in the same microtask.
- TIP-PTR-07, TIP-FOC-04 — ADR 01's hand-off row (added 2026-10-28, M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); chosen by the user 2026-10-27): the popup re-anchors to the second trigger at once on both modes (the `interactions` module's pending anchor and `positioning`'s trigger case through it, ADR 04 Amendments 2026-10-28, design point 13.4 (1)); the active trigger and `data-popup-open` follow a round trip later and the payload a further one (the trigger forwards it from `OnAfterRender`, PSH-STA-30). Measured locally with 250 ms each way: anchor 0 ms, `data-popup-open` 510 ms, payload 950 ms after `mouseenter` ("port: a hover hand-off re-anchors at once; on Server the active trigger follows a round trip later and the payload a further one"); forwarding the payload from the commit path is [issue 142](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/142) (design point 13.10, settled by the orchestrator 2026-10-28).
- TIP-DOM-07 — ADR 01's "JS-owned values that originate in C# state" row (added 2026-10-28, M4 track 6): `pointer-events: none` is in the style C# renders before the Positioner's handle attaches; after a parameter change (`DisableHoverablePopup`, `TrackCursorAxis`) it reaches the layered style the module owns with the twin's `update`, a round trip later on Server.
- TIP-API-02 — the same row (added 2026-10-28, M4 track 6): a changed Provider `CloseDelay` reaches the delay group's timers with the Provider twin's `update`, a round trip after the render on Server.
- TIP-DIS-06 — ADR 01 decision 4's C# state row (added 2026-10-28, M4 track 6): the tooltip renders closed in the commit that disables it on both modes (TIP-STA-02's effective open, ADR 05 Amendments 2026-10-28); the `disabled` close request, and so `onOpenChange(false)` and the stored open state's revert, run from `OnAfterRender`, after paint plus a round trip on Server.

Met on both modes (§14's allowance; added 2026-10-28, M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): TIP-STA-06, TIP-STA-07, TIP-STA-08 and TIP-ANI-03, since the delay group's phase travels on the open request and the store sets the instant type in the batch that renders `data-open` (chosen by the user 2026-10-27, ADR 04 Amendments 2026-10-27; "port: data-instant="delay" renders in the batch of data-open on both modes"); TIP-STA-10, since the payload is cleared in the Root's commit path, before the render.

## 10. Positioning

Tooltip delegates positioning to FLT (`useAnchorPositioning` + `usePositioner`); only its configuration is stated here.

- **TIP-POS-01** — MUST. The Positioner's defaults are: `side: 'top'` (FLT's generic default is `'bottom'`), `align: 'center'`, `sideOffset: 0`, `alignOffset: 0`, `positionMethod: 'absolute'`, `collisionBoundary: 'clipping-ancestors'`, `collisionPadding: 5`, `arrowPadding: 5`, `sticky: false`, `disableAnchorTracking: false`, `collisionAvoidance: { fallbackAxisSide: 'end' }` (so `side`/`align` default to `'flip'`, FLT-API-10). Every prop is passed through unchanged to the positioning session.
  Source: `tooltip/positioner/TooltipPositioner.tsx:26-43,57-74`; `internals/constants.ts:26-28`. Test: [L4] `TooltipPositioner.test.tsx` positionerConformanceTests "offsets the positioner when a number is specified", "offsets the positioner when a function is specified", "rests exactly at collisionPadding from the colliding edge"; `TooltipArrow.test.tsx` (`side="bottom"`).
- **TIP-POS-02** — MUST. The default anchor is the store's active trigger element (FLT-STR-10: the root store's reference element follows `activeTriggerElement`), replaced by the cursor virtual anchor while tracking (TIP-POS-03); an explicit `anchor` prop (element, ref, function or virtual element) overrides both (FLT-API-11).
  Source: `tooltip/positioner/TooltipPositioner.tsx:28,58-60`; `floating-ui-react/hooks/useSyncedFloatingRootContext.ts:49-91`. Test: [L4] `TooltipRoot.test.tsx` "stops tracking the cursor after trackCursorAxis is disabled while closed" (centered on the trigger once tracking stops).
- **TIP-POS-03** — MUST. With `trackCursorAxis` `'x'`, `'y'` or `'both'` the anchor rect is the cursor virtual anchor of FLT-ALG-25 built around the trigger: `'x'` → width `0`, x = cursor x, y/height = the trigger's; `'y'` → height `0`, y = cursor y, x/width = the trigger's; `'both'` → a 0×0 rect at the cursor. The anchor is captured from the `mouseenter`/`mousemove` events that precede the open (so a delayed open appears at the resting cursor position), follows the cursor while open (and while exiting, for mouse-like pointers), is not used for focus opens, and is rebuilt from the new pointer position on each reopen.
  Source: `tooltip/root/TooltipRoot.tsx:264-267`; `floating-ui-react/hooks/useClientPoint.ts:10-75,120-160,166-212`. Test: [L4] `TooltipRoot.test.tsx` "tracks the cursor on the first delayed hover when trackCursorAxis is x", "updates the tracked cursor position after closing and reopening".
- **TIP-POS-04** — MUST. The Portal's `keepMounted` is forwarded to the positioning session (FLT-API-17), which then uses `mounted` as its open input (FLT-POS-12) so a kept-mounted tooltip repositions when reshown; the session is also given the store's `mounted` flag and floating root context.
  Source: `tooltip/positioner/TooltipPositioner.tsx:46,60-61,71`. Test: none in this component (FLT-STA-09).
- **TIP-POS-05** — MUST. While a Viewport is mounted the store's `adaptiveOrigin` middleware (PSH) is passed to positioning, so the Positioner is placed with `top`/`left`/`right`/`bottom` anchored from the far edge of the rendered side (FLT-DOM-04, PSH-DOM-12) instead of a transform; without a Viewport transform positioning is used.
  Source: `tooltip/positioner/TooltipPositioner.tsx:55,73`. Test: [L4] `TooltipViewport.test.tsx` "anchors the positioner from the far edge while it transitions"; `TooltipPositioner.test.tsx` positionerConformanceTests "uses transform positioning without Viewport", "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
- **TIP-POS-06** — MUST. The Arrow is positioned by the FLT arrow middleware with `arrowPadding` (default `5`) as the minimum distance from the Popup's edges; `data-uncentered` is present when the arrow cannot point at the anchor's center (e.g. a wide popup over a narrow trigger with `arrowPadding={40}`), absent when it can (FLT-DOM-09, FLT-POS-08).
  Source: `tooltip/positioner/TooltipPositioner.tsx:37,69`; `tooltip/arrow/TooltipArrow.tsx:23,32,38-39`; `internals/useAnchorPositioning.ts:625`. Test: [L4] `TooltipArrow.test.tsx` "is marked uncentered when it cannot point at the anchor", "is not marked uncentered when it can point at the anchor".
- **TIP-POS-07** — MUST. The rendered `side`/`align` exposed on Positioner, Popup and Arrow state (and `data-side`/`data-align`) are the logical values after collision handling (FLT-POS-10); `anchorHidden` follows FLT-POS-09. Server: relaxed, see §9.
  Source: `tooltip/positioner/TooltipPositioner.tsx:79-81`; `tooltip/positioner/TooltipPositionerContext.ts:5-8`. Test: [L4] `TooltipArrow.test.tsx` "is hidden from assistive technology and mirrors the resolved side" (`data-side="bottom"` on the arrow).
- **TIP-POS-08** — MUST. The Positioner's inline style carries `--available-width`, `--available-height` (seeded `100vw`/`100vh`), `--anchor-width`, `--anchor-height` and `--transform-origin` as specified by FLT-DOM-05..07, and, only while a Viewport is mounted, `--positioner-width`/`--positioner-height` (PSH-DOM-11). No other CSS variable is set by Tooltip.
  Source: `tooltip/positioner/TooltipPositionerCssVars.ts:5-37`; `internals/useAnchorPositioning.ts:362-378,392-450,534-535`; `utils/usePopupAutoResize.ts:223`. Test: none in this component (FLT/PSH).
- **TIP-POS-09** — MUST NOT. `Tooltip.Positioner` MUST NOT accept a `keepMounted` prop (it is a type error); `keepMounted` lives on `Tooltip.Portal` only.
  Source: `tooltip/positioner/TooltipPositioner.tsx:133-143`; `tooltip/positioner/TooltipPositioner.spec.tsx:3-4`. Test: [L1] `TooltipPositioner.spec.tsx` (type-level). Docs: CHANGELOG.md v1.0.0-alpha.5 "Require `Portal` part … The `keepMounted` prop was removed from the Positioner".

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Popup store (`usePopupRootStore`, `TooltipStore`) | [popup-store-and-handles](../architecture/popup-store-and-handles.md) PSH-STR-01, PSH-STA-01 | Extra fields of §4.1; `treatPopupAsFloatingElement` **false** (the Positioner is the floating element); `setOpen` runs `applyPopupOpenChange` with `extraState: { openChangeReason: reason }` (`TooltipStore.ts:84-91`); extra `cancelPendingOpen` (TIP-EVT-08). |
| Handle (`BasePopupHandle`) | PSH §3.1, PSH-API-01..05, PSH-STA-34..40 | `componentName: 'Tooltip'`, `throwOnMissingTrigger: true` (default); fallback store = `createNullTooltipStore()` with no-op `setOpen`/`cancelPendingOpen` (PSH-STA-37). |
| `PopupHandleAttachment` | PSH-STR-03/04 | Rendered first inside Root iff `handle` is given (`TooltipRoot.tsx:141`). |
| `useTriggerDataForwarding` | PSH-STA-26..30 | Forwarded state `{ payload, closeOnClick, closeDelay }` (`TooltipTrigger.tsx:80-89`). |
| `useImplicitActiveTrigger` | PSH-STA-16..25 | `closeOnActiveTriggerUnmount: true` (`TooltipRoot.tsx:81`). |
| `useOpenStateTransitions` / `useUnmountAfterClose` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) TRN-ALG-05, PSH-STA-44/45 | `open` = effective open (TIP-STA-02); `animateInitialOpen` unset (false); no `onUnmount` hook. |
| `useOpenChangeComplete` | TRN-ALG-04, TRN-EVT-01 | On the Popup element; fires `onOpenChangeComplete(true)` only (`TooltipPopup.tsx:38-46`). |
| `usePopupInteractionProps` | PSH-STA-42 | `activeTriggerProps === inactiveTriggerProps` = merged `useClientPoint` + `useDismiss` reference bags; `popupProps` = `useDismiss` floating bag (`TooltipRoot.tsx:271-279`). Mounted per TIP-STA-15. |
| `useDismiss` | [floating-and-positioning](../architecture/floating-and-positioning.md) FLT-DIS-*, FLT-ALG-17 | `enabled: !disabled`, `referencePress: () => store.closeOnClick`; defaults otherwise (`escapeKey: true`, `outsidePress: true`, `outsidePressEvent: 'sloppy'`, `bubbles` → escape false / outside true). |
| `useClientPoint` | FLT-PTR-07..10, FLT-ALG-25 | `enabled: !disabled && trackCursorAxis !== 'none'`, `axis: trackCursorAxis`. Tooltip is the only consumer of this hook in 1.9.0. |
| `useHoverReferenceInteraction` | FLT-PTR-11..19, FLT-ALG-24 | Per Trigger: `enabled: !disabled`, `mouseOnly: true`, `move: false`, `handleClose: safePolygon()` iff `!disableHoverablePopup && trackCursorAxis !== 'both'` else `null`, `restMs: getOpenDelay` (TIP-API-15), `delay: () => ({ close })` (TIP-API-16; no `open` delay), `triggerElementRef`, `isActiveTrigger: isTriggerActive(ownId)`, `isClosing: () => transitionStatus === 'ending'`, `shouldOpen: () => !nestedTriggerHovered`; `guardStaleOpen` off (`TooltipTrigger.tsx:146-164`). |
| `useHoverFloatingInteraction` | FLT-PTR-20..25 | On the Popup: `enabled: !disabled`, `closeDelay: store.closeDelay`; no `nodeId` (`TooltipPopup.tsx:48-51`). |
| `safePolygon()` | FLT-ALG-15 | Default options (`blockPointerEvents: false`). |
| `useFocus` | FLT-FOC-01..04, FLT-A11Y-06 | Per Trigger: `enabled: !disabled`, no `delay` (`TooltipTrigger.tsx:166`). |
| `FloatingDelayGroup` / `useDelayGroup` | FLT-API-16, FLT-ALG-23, FLT-ANI-01, FLT-EVT-07, FLT-EDGE-11/12 | Provider: `delay: { open: delay, close: closeDelay }`, `timeoutMs: timeout` (`TooltipProvider.tsx:15-19`). Trigger: `useDelayGroup(floatingRootContext, { open: isOpenedByThisTrigger })`; reads `activeIdRef`, `delayRef`, `isInstantPhase`, `hasProvider` (`TooltipTrigger.tsx:92-97`). |
| `useAnchorPositioning` / `usePositioner` | FLT §3.1, FLT-DOM-01..08, FLT-POS-* | Defaults of TIP-POS-01; `hidden: !mounted`; `inert` per TIP-DOM-07; refs: consumer + store `positionerElement`; `keepMounted` from the Portal; `adaptiveOrigin` from the store. |
| `FloatingPortalLite` / `useFloatingPortalNode` | PSH-STR-11, FLT-DOM-11, FLT-API-13 | Rendered iff `mounted \|\| keepMounted`; `container` forwarded; no focus guards. |
| `usePopupViewport` | PSH-STR-07, PSH-DOM-07..12 | `{ store, side: positioner.side, children }` (`TooltipViewport.tsx:28-32`). |
| `useRenderElement` + state mappings | [rendering-and-composition](../architecture/rendering-and-composition.md) RND §4.4, §4.9 | Trigger: `triggerOpenStateMapping`; Positioner/Arrow: `popupStateMapping`; Popup: `popupTransitionStateMapping`; Viewport: `popupViewportStateMapping`; default projection for `side`, `align`, `instant`, `uncentered`, `transitioning`. |
| `useBaseUiId` | RND-DOM-27..33 | Trigger id (`TooltipTrigger.tsx:71`). |
| `ReactStore` helpers (`useControlledProp`, `useContextCallback`, `useSyncedValue(s)`, `useStateSetter`) | [shared-utilities](../architecture/shared-utilities.md) UTL-STA-16, UTL-STA-19..23 | Root: `openProp`, `triggerIdProp`; `onOpenChange`, `onOpenChangeComplete`; `{ trackCursorAxis, disableHoverablePopup, disabled }`. Trigger: `isInstantPhase`. Positioner/Popup: `positionerElement`/`popupElement` setters. |
| `Timeout` (`useTimeout`) | UTL-EVT-01..03 | Trigger's nested-reopen timer (`TooltipTrigger.tsx:109`). |
| `NullStore` | UTL-STA-34 | Detached fallback store. |
| `getDisabledMountTransitionStyles` | TRN-DOM-05 | Popup and Positioner. |

## 12. Edge cases, invariants and dev warnings

- **TIP-EDGE-01** — MUST. Rendering `Tooltip.Trigger` with neither an enclosing `Tooltip.Root` nor a `handle` throws an error with the exact message `Base UI: <Tooltip.Trigger> must be either used within a <Tooltip.Root> component or provided with a handle.` (in all builds).
  Source: `tooltip/trigger/TooltipTrigger.tsx:62-69`. Test: [L2] `TooltipTrigger.test.tsx` "throws a descriptive error when rendered without a root or a handle".
- **TIP-EDGE-02** — MUST. Rendering `Tooltip.Portal`, `Tooltip.Positioner`, `Tooltip.Popup`, `Tooltip.Arrow` or `Tooltip.Viewport` outside a `Tooltip.Root` throws `Base UI: TooltipRootContext is missing. Tooltip parts must be placed within <Tooltip.Root>.`
  Source: `tooltip/root/TooltipRootContext.ts:11-19`. Test: [L2] `TooltipPositioner.test.tsx` "throws a descriptive error when rendered outside <Tooltip.Root>".
- **TIP-EDGE-03** — MUST. Rendering `Tooltip.Positioner` inside a Root but outside a `Tooltip.Portal` throws `Base UI: <Tooltip.Portal> is missing.`
  Source: `tooltip/portal/TooltipPortalContext.ts:6-12`; `tooltip/positioner/TooltipPositioner.tsx:46`. Test: [L2] `TooltipPositioner.test.tsx` "throws a descriptive error when rendered outside <Tooltip.Portal>".
- **TIP-EDGE-04** — MUST. Rendering `Tooltip.Popup`, `Tooltip.Arrow` or `Tooltip.Viewport` outside a `Tooltip.Positioner` throws `Base UI: TooltipPositionerContext is missing. TooltipPositioner parts must be placed within <Tooltip.Positioner>.`
  Source: `tooltip/positioner/TooltipPositionerContext.ts:14-21`. Test: [L2] `TooltipPopup.test.tsx` "throws a descriptive error when rendered outside <Tooltip.Positioner>".
- **TIP-EDGE-05** — MUST. `handle.open(id)` with an id that matches no registered trigger (searching the attached Roots' registries newest-first, then the fallback registry, PSH-STA-38) throws the PSH missing-trigger error with `componentName = 'Tooltip'`: `Base UI: TooltipHandle.open() was called with the trigger id "<id>", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted Tooltip.Trigger that has this handle set on its "handle" prop.`; the tooltip stays closed.
  Source: `tooltip/store/TooltipHandle.ts:17-18,29-31`; `utils/popups/popupHandle.ts:241-249`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "throws when opened with an unregistered trigger id" (`throwOnMissingTrigger: true`).
- **TIP-EDGE-06** — SHOULD. `handle.open()` / `handle.close()` called while no Root is attached log the PSH development warnings (`TooltipHandle.open() was called while no root using this handle is mounted…` / `TooltipHandle.close() was called while no root using this handle is mounted. The call was ignored.`) and do nothing.
  Source: `utils/popups/popupHandle.ts:216-224,274-281`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "ignores imperative calls while no root is attached and follows the root across remounts".
- **TIP-EDGE-07** — SHOULD. Two mounted Roots sharing one handle beyond a one-frame overlap log the PSH "A handle is attached to more than one mounted root at the same time…" warning (next frame after the second attachment); the most recently attached Root is controlled.
  Source: `utils/popups/popupHandle.ts:153-173` (PSH-STA-35). Test: [L2] detachedTriggersConformanceTests "resolves a trigger still registered to the previous root during a transient overlap".
- **TIP-EDGE-08** — SHOULD. Switching `open` (or `triggerId`) between defined and `undefined` after the first render logs the store's controlled/uncontrolled diagnostics (UTL-EDGE-25) with the Tooltip store's key names (`openProp`, `triggerIdProp`); the mode does not change.
  Source: `tooltip/root/TooltipRoot.tsx:62-63`. Test: none.
- **TIP-EDGE-09** — SHOULD. Registering the same DOM element under two different trigger ids in one store throws in development: `Base UI: A trigger element cannot be registered under multiple IDs in PopupTriggerMap.` (PSH).
  Source: `utils/popups/popupTriggerMap.ts:36-57`. Test: none in this component.
- **TIP-EDGE-10** — MUST. `defaultOpen` (or `open={true}`) together with `disabled` renders the tooltip closed and MUST NOT throw; the stored `open` is reverted with reason `disabled` post-commit (TIP-DIS-06).
  Source: `tooltip/root/TooltipRoot.tsx:68-69,96-100`. Test: [L2] `TooltipRoot.test.tsx` "does not throw error when combined with defaultOpen".
- **TIP-EDGE-11** — MUST NOT. Opening, closing or moving the tooltip between triggers (imperatively, by focus or by hover) MUST NOT update Triggers that are neither the previous nor the new active trigger: a bystander Trigger's substitute callback is not invoked and its DOM is untouched. (The shared interaction attribute bag is the same object for active and inactive triggers, and inactive triggers select `undefined` instead of it.)
  Source: `tooltip/store/TooltipStore.ts:42-45`; `tooltip/trigger/TooltipTrigger.tsx:216-219`; `tooltip/root/TooltipRoot.tsx:269-277`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` "when opened and closed imperatively", "when the popup moves to another trigger", "when focus opens and closes the popup", "when hover opens and closes the popup". Docs: CHANGELOG.md v1.0.0-rc.0 "Improve contained triggers performance (#3385)"; v1.9.0 "Avoid re-rendering inactive triggers on open and close".
- **TIP-EDGE-12** — MUST. Server rendering / hydration: a Root rendered `defaultOpen` with `defaultTriggerId` and a detached Trigger that hydrates later (e.g. inside a suspended boundary) stays open (`handle.isOpen === true`, no `onOpenChange`) until the trigger hydrates, after which the trigger shows `data-popup-open`; the server markup of the trigger has no `data-popup-open` (the server store is the fallback store, PSH-STA-34).
  Source: `utils/popups/usePopupHandleStore.ts:35`; `utils/popups/popupStoreUtils.ts:430-431,484-490` (pending active id is never "lost"). Test: [L4] `TooltipRoot.detached-triggers.test.tsx` "keeps a default-open root open until its detached trigger hydrates", "keeps a default-open root open while a detached trigger migrates after the initial commit" (L4 for the server-markup clause).
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the hydration titles are React SSR's and jsdom-only; restated as port titles: L4 "port: keeps a default-open root open until its detached trigger registers, and the prerendered trigger shows no data-popup-open" on the prerendered `tooltip-prerender` (`Prerender: true`), and L2 "port: a detached trigger declared before the root registers with it, and keeps working when the root remounts", "port: a default-open root keeps its trigger-less open state until a trigger with its default trigger id registers".
- **TIP-EDGE-13** — MUST. Moving a controlled `triggerId` to an id whose Trigger has not registered yet (not hydrated / not mounted) keeps the tooltip open with no `onOpenChange`; when that Trigger later registers it becomes active and shows `data-popup-open` (PSH-STA-24).
  Source: `utils/popups/popupStoreUtils.ts:484-490`. Test: [L2] `TooltipRoot.detached-triggers.test.tsx` "keeps an open root open when ownership moves to a trigger that has not hydrated".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): the hydration title is React SSR's and jsdom-only; restated as the L2 port title "port: a default-open root keeps its trigger-less open state until a trigger with its default trigger id registers" (a trigger that registers after the Root's first render).
- **TIP-EDGE-14** — MUST. A Root unmounted while open (or while kept mounted after a prevented close) and remounted with the same handle starts closed with no payload; imperative calls made while it was unmounted are not replayed (PSH-STA-40).
  Source: `utils/popups/popupStoreUtils.ts:79`; `utils/popups/popupHandle.ts:213-224`. Test: [L2] `TooltipRoot.test.tsx` "unmounts on a normal close after a prevented close and initially open remount"; detachedTriggersConformanceTests "ignores imperative calls while no root is attached and follows the root across remounts". Docs: `components/tooltip/page.mdx` §Detached triggers.
- **TIP-EDGE-15** — MUST. Detached Triggers keep working when their ancestors are added/removed (reparenting) and when the handle object is recreated with the same Root (Fast-Refresh-like recreation): the trigger migrates its registration to the current store (PSH-STA-26/36).
  Source: `tooltip/trigger/TooltipTrigger.tsx:62-64,80-89`; `utils/popups/popupStoreUtils.ts:368-380`. Test: [L4] detachedTriggersConformanceTests "keeps a detached trigger working $name" (×4), "registers a detached trigger declared after the root".
  Port note 2026-10-29 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27; found in the final review): re-tagged [L2] → [L4]: the five cited titles run in the browser suite, `detachedTriggersConformanceTests` in `tests/browser/suites/`; the four "keeps a detached trigger working $name" titles are green on both render modes. "registers a detached trigger declared after the root" is listed open until PR 136 lands `InteractionsTriggerBehavior`'s session rebinding (a trigger declared after the root attaches under no session; with TIP-STR-09 and PSH-STR-03).
- **TIP-EDGE-16** — MUST. Nested tooltips rely on the private marker of TIP-DOM-04: a port MUST ensure the marker survives element substitution (`render`) and consumer props (it is merged before consumer props, so a consumer could override it; §16 Q3), and MUST evaluate "disabled" for the marker from the *effective* `disabled` of TIP-API-18.
  Source: `tooltip/trigger/TooltipTrigger.tsx:259-263`. Test: [L2] `TooltipRoot.test.tsx` nested tooltips (all).
- **TIP-EDGE-17** — MUST. RTL: Tooltip has no direction-specific logic of its own; `side: 'inline-start' | 'inline-end'` and `data-side` resolve through FLT-I18N (FLT-POS-10). Shadow DOM: Portal `container` may be a `ShadowRoot`; nested-trigger detection and outside-press checks use composed paths.
  Source: `tooltip/portal/TooltipPortal.tsx:47-48`; `tooltip/trigger/TooltipTrigger.tsx:33-36,127`. Test: [L4] `TooltipRoot.test.tsx` "should not open the outer tooltip when hovering over a nested trigger inside a shadow root".

## 13. Behaviors evidenced by tests

Coverage of each test by the requirements above. Conformance suites are listed with the options Tooltip passes; their generic obligations are specified in RND §4.9 (`describeConformance`), PSH (popup/detached-trigger/viewport suites) and FLT (positioner suite).

### `root/TooltipRoot.test.tsx`

Tests under `describe.for([contained triggers, detached triggers, multiple detached triggers])` run three times (one Root with a contained Trigger; one detached Trigger + Root sharing a handle; two detached Triggers + Root); the mapping is identical for each variant. The block covers the 35 titles from "uncontrolled open" through "dismissal" › "reopens on hover after the trigger is clicked closed" (3 variants each); the popupConformanceTests rows and the rows from the second "preventUnmountOnClose()" block onward run once.

| Test | Requirements |
|---|---|
| popupConformanceTests (`triggerMouseAction: 'hover'`, no `expectedPopupRole` → ARIA block skipped, `exitAnimation` default): "opens the popup with the `open` prop" / "is ignored when `open={false}` is passed" / "is ignored when `open={true}` is passed, so Escape cannot close" | TIP-API-05, TIP-STA-03; PSH-STA-02 |
| popupConformanceTests "opens initially and remains uncontrolled" | TIP-API-04, TIP-STA-01 |
| popupConformanceTests "is called with the new open state and the change details" | TIP-EVT-01, TIP-EVT-09 (`trigger-hover`) |
| popupConformanceTests "keeps the popup closed when the open change is canceled" | TIP-EVT-03 |
| popupConformanceTests "is not called on mount when closed" / "is called on open when there is no enter animation" / "is called on close when there is no exit animation" / "is called on open once the enter animation finishes" / "is called on close once the exit animation finishes" | TIP-API-07, TIP-EVT-05; TRN-EVT-01..04 |
| popupConformanceTests "removes the popup when there is no exit animation defined" / "hides the kept-mounted popup once the exit animation finishes" | TIP-ANI-01, TIP-STA-17; TRN-ANI-03 |
| "trigger unmount during the open delay" › "does not open once the hovered trigger has unmounted" | TIP-PTR-23 |
| "uncontrolled open" › "should open when the trigger is hovered" | TIP-PTR-02, TIP-API-15, TIP-STR-09 |
| "does not open when a touch pointer hovers the trigger" | TIP-PTR-01 |
| "should close when the trigger is unhovered" | TIP-PTR-05 |
| "should open when the trigger is focused" | TIP-FOC-01, TIP-A11Y-01, TIP-KEY-03 |
| "should close when the trigger is blurred" | TIP-FOC-02, TIP-KEY-03 |
| "controlled open" › "should call onOpenChange when the open state changes" | TIP-STA-03, TIP-API-05, TIP-EVT-02 |
| "does not call onOpenChange again when the trigger is re-hovered while open" | TIP-EVT-07 |
| "prop: defaultOpen" › "closes when the trigger is unhovered after opening by default" | TIP-API-04, TIP-PTR-05 |
| "prop: delay" › "should open after rest delay" | TIP-API-15, TIP-PTR-02 |
| "prop: closeDelay" › "should close after delay" | TIP-API-16, TIP-PTR-05 |
| "preventUnmountOnClose()" › "does not prevent unmounting on later closes" | TIP-EVT-04, TIP-ANI-06 |
| "prop: actionsRef" › "unmounts the tooltip when the `unmount` method is called" | TIP-API-08, TIP-ANI-05, TIP-EVT-04 |
| "closes the tooltip when the `close` method is called" | TIP-API-08, TIP-DIS-07, TIP-DOM-02, TIP-EVT-01 (`imperative-action`) |
| "animations" › "toggles instant animations for adjacent tooltips only while opening" | TIP-STA-06, TIP-STA-07, TIP-ANI-02, TIP-ANI-03, TIP-DOM-12 |
| "unmounts an exiting tooltip when another tooltip opens" | TIP-ANI-04, TIP-DIS-04 |
| "inline opacity: 0 is removed before user CSS transitions run" | TIP-DOM-10, TIP-ANI-07 |
| "prop: disabled" › "should not open when disabled" | TIP-API-09, TIP-STA-02, TIP-FOC-08, TIP-PTR-03 |
| "should not open on focus when the trigger is disabled" | TIP-API-18, TIP-FOC-08 |
| "should close if open when becoming disabled" | TIP-DIS-06, TIP-STA-16 |
| "does not throw error when combined with defaultOpen" | TIP-EDGE-10, TIP-STA-02, TIP-DIS-06 |
| "marks the trigger as disabled when the root is disabled" | TIP-DOM-03, TIP-API-09 |
| "keeps the tooltip disabled when the root is disabled and the trigger opts back in" | TIP-API-09, TIP-DOM-03 |
| "prop: disableHoverablePopup" › "applies pointer-events: none to the positioner when `disableHoverablePopup = true`" / "does not apply pointer-events: none to the positioner when `disableHoverablePopup = false`" | TIP-API-10, TIP-DOM-07 |
| "prop: trackCursorAxis" › "makes the positioner inert when tracking both axes" / "keeps the positioner hoverable when tracking a single axis" | TIP-API-11, TIP-DOM-07 |
| "instant animations" › "marks the popup as instant when opened by focus, but not when opened by hover" | TIP-STA-05, TIP-ANI-08, TIP-DOM-12, TIP-FOC-01 |
| "BaseUIChangeEventDetails" › "allowPropagation() prevents stopPropagation on Escape while still closing" | TIP-KEY-01, TIP-EVT-10, TIP-DIS-08 |
| "dismissal" › "should close when Escape is pressed" | TIP-KEY-01, TIP-DIS-01, TIP-EVT-01 (`escape-key`) |
| "should not open when the trigger was clicked before delay duration" / "should not open when the trigger receives pointerdown before delay duration" | TIP-PTR-10, TIP-EVT-08, TIP-API-17 |
| "should open when the trigger was clicked before delay duration and closeOnClick is false" | TIP-PTR-12, TIP-API-17 |
| "should close when the trigger is clicked after delay duration" | TIP-PTR-11, TIP-API-17 |
| "should not close when the trigger is clicked after delay duration and closeOnClick is false" | TIP-PTR-12 |
| "reopens on hover after the trigger is clicked closed" | TIP-PTR-13 |
| "preventUnmountOnClose()" › "unmounts on a normal close after a prevented close and initially open remount" | TIP-ANI-06, TIP-EDGE-14, TIP-EVT-04 |
| "tracks the cursor on the first delayed hover when trackCursorAxis is x" | TIP-POS-03, TIP-PTR-16, TIP-API-11 |
| "stops tracking the cursor after trackCursorAxis is disabled while closed" | TIP-PTR-16, TIP-POS-02, TIP-STA-15, TIP-STA-16 |
| "updates the tracked cursor position after closing and reopening" | TIP-POS-03, TIP-PTR-16 |
| "hoverable popup" › "survives the trip from the trigger to the popup, then closes after leaving it" | TIP-PTR-06, TIP-PTR-26, TIP-DOM-14 |
| "closes on that same trip when `disableHoverablePopup` is set" | TIP-PTR-06, TIP-API-10 |
| "keeps the tooltip open when moving across spaced triggers without a closeDelay" | TIP-PTR-07, TIP-PTR-04 |
| "nested tooltips" › "should not open the outer tooltip when hovering over a nested tooltip trigger" / "should not open the outer tooltip when moving between sibling nested triggers" / "should not open ancestor tooltips when hovering over a third-level nested trigger" / "should not open the outer tooltip when the pointer moves onto a nested trigger before the delay expires" / "should suppress the safePolygon-driven open while a nested trigger is hovered" / "should support nested triggers with a Provider delay={0}" | TIP-PTR-17, TIP-STR-10, TIP-DOM-04 |
| "should support nested triggers inside a detached parent trigger" | TIP-PTR-17, TIP-PTR-19, TIP-STR-10 |
| "should open the outer tooltip when moving from a nested trigger to the parent area with zero delay" / "should reopen the delayed outer tooltip when moving from a nested trigger to the parent area" / "should restart the parent delay when moving from a nested trigger to the parent area" | TIP-PTR-19 |
| "should not open a disabled outer tooltip when moving from a nested trigger to the parent area with zero delay" | TIP-PTR-19, TIP-API-18 |
| "should not re-announce an open outer tooltip when the pending reopen fires" / "should not reopen the outer tooltip when rapidly moving back to a nested trigger" / "should not reopen the outer tooltip when hovering the nested tooltip popup" / "should cancel the pending parent reopen when the pointer leaves the parent trigger" | TIP-PTR-20 |
| "should close the outer tooltip when the pointer moves from outer area onto a nested trigger" / "should not open the outer tooltip when moving from the outer popup to a nested trigger" | TIP-PTR-18, TIP-STA-04 |
| "should keep a focus-opened outer tooltip open when hovering over a nested trigger" / "should keep a controlled-open outer tooltip open when hovering over a nested trigger" | TIP-PTR-18, TIP-STA-04, TIP-EVT-06 |
| "should not open the outer tooltip when focusing a nested tooltip trigger" | TIP-FOC-05 |
| "should close a focus-opened inner tooltip when the inner trigger loses focus" | TIP-FOC-05, TIP-FOC-02 |
| "should allow the parent tooltip to open when a nested trigger is disabled" | TIP-PTR-21, TIP-DOM-04, TIP-API-18 |
| "should not open the outer tooltip when hovering over a nested trigger inside a shadow root" / "falls back to the event target when the composed path is empty" | TIP-PTR-21, TIP-EDGE-17 |
| "should open the outer tooltip when hovering over the non-nested area" | TIP-PTR-21, TIP-PTR-02 |
| "should not reopen the outer tooltip via the local reopen path for touch pointers" | TIP-PTR-22 |
| "should allow mouse hover after leaving a touch interaction" | TIP-PTR-24, TIP-PTR-19 |

### `root/TooltipRoot.detached-triggers.test.tsx`

| Test | Requirements |
|---|---|
| "keeps a default-open root open until its detached trigger hydrates" | TIP-EDGE-12 |
| "keeps an open root open when ownership moves to a trigger that has not hydrated" | TIP-EDGE-13, TIP-EVT-06 |
| detachedTriggersConformanceTests (`triggerProps: { delay: 0, style: { pointerEvents: 'none' } }`, `openInteractions: ['hover', 'focus']`, `ariaExpanded: false`, `throwOnMissingTrigger: true`): "opens with any trigger on hover and marks only that trigger as active" / "… on focus …" (contained and detached modes) | TIP-STR-09, TIP-DOM-02, TIP-ARIA-05, TIP-PTR-03, TIP-FOC-01, TIP-FOC-04; PSH-STA-07/08, PSH-STA-45 |
| "sets the payload of the active trigger and renders content based on it" | TIP-API-14, TIP-API-19; PSH-STA-28/29 |
| "reuses the popup DOM nodes when switching triggers" / "does not leave an inline scale style on the popup after switching triggers" | TIP-ANI-09; PSH-STA-31 |
| "marks the trigger matching a controlled `triggerId` as active" / "opens initially with the payload of a non-first `defaultTriggerId`" / "allows controlling the open state and active trigger programmatically" | TIP-API-13, TIP-EVT-09; PSH-STA-03 |
| "opens by trigger id with its payload and closes" | TIP-API-23, TIP-DIS-07, TIP-EVT-09 |
| "throws when opened with an unregistered trigger id" | TIP-EDGE-05 |
| "ignores imperative calls while no root is attached and follows the root across remounts" | TIP-API-23, TIP-EDGE-06, TIP-EDGE-14; PSH-STA-34/40 |
| "registers a detached trigger declared after the root" / "resolves a trigger still registered to the previous root during a transient overlap" / "keeps a detached trigger working when wrappers are removed" / "… when wrappers are added" / "… during Fast Refresh-like handle recreation" / "… when reparented during Fast Refresh-like handle recreation" | TIP-EDGE-15, TIP-EDGE-07; PSH-STA-26/35/36/38 |
| "does not re-render inactive triggers" › "when opened and closed imperatively" / "when the popup moves to another trigger" / "when focus opens and closes the popup" / "when hover opens and closes the popup" | TIP-EDGE-11, TIP-DIS-07, TIP-FOC-04, TIP-PTR-07 |
| "closes a cursor-tracking tooltip when an inactive trigger is pressed" | TIP-PTR-15, TIP-PTR-11, TIP-STA-09, TIP-DOM-05 |
| "handle-backed root ownership" › "keeps a default-open root open while a detached trigger migrates after the initial commit" | TIP-EDGE-12, TIP-EVT-06 |
| "multiple triggers within Root" › "hands off open state and payload to a trigger with its own DOM id while open" | TIP-FOC-04, TIP-API-19, TIP-API-20, TIP-STR-09 |
| "should close when the active trigger unmounts" / "multiple detached triggers" › "should close when the active detached trigger unmounts" | TIP-DIS-05 |
| "should remain open when the active trigger unmount close is canceled" | TIP-DIS-05, TIP-EVT-03, TIP-EVT-01 (`none`) |
| "should close when focusing a disabled trigger while another trigger is open" | TIP-FOC-03, TIP-API-18 |
| "should close when hovering a disabled trigger while another trigger is open" | TIP-PTR-08, TIP-API-18 |
| "should switch to a rendered disabled button trigger when trigger hover is enabled" | TIP-PTR-25, TIP-PTR-07 |

### `provider/TooltipProvider.test.tsx`

| Test | Requirements |
|---|---|
| "prop: delay" › "waits for the delay before showing the tooltip" | TIP-API-01, TIP-API-15, TIP-PTR-02 |
| "respects delay=0" | TIP-API-01, TIP-PTR-03 |
| "respects trigger delay prop over provider delay prop" | TIP-API-15 |
| "prop: closeDelay" › "waits for the closeDelay before hiding the tooltip" | TIP-API-02, TIP-API-16, TIP-PTR-05 |
| "uses the latest closeDelay after the prop updates" | TIP-API-02 |
| "prop: timeout" › "opens an adjacent tooltip instantly while the group is active" | TIP-API-03, TIP-PTR-04, TIP-STA-08, TIP-DIS-04, TIP-STR-01 |
| "respects a trigger delay over delay=0 outside the instant phase" | TIP-API-15, TIP-PTR-04 |
| "requires the full delay again once the timeout elapses" | TIP-API-03, TIP-PTR-04 |

### `trigger/TooltipTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, rendered inside `Tooltip.Root`; no `button` option) | TIP-STR-03, TIP-DOM-01; RND-DOM-36..48 |
| "throws a descriptive error when rendered without a root or a handle" | TIP-EDGE-01, TIP-STR-03 |
| "removes `data-popup-open` as soon as `open` becomes false" | TIP-DOM-02, TIP-STA-11, TIP-EVT-04 |
| "opens when the rendered trigger element has its own id" | TIP-API-20 |
| "opens on delayed hover when rendered as a disabled toolbar button" | TIP-PTR-25 |

### `arrow/TooltipArrow.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: Element`, inside open Root/Portal/Positioner/Popup) | TIP-STR-07; RND-DOM-36..48 |
| "is hidden from assistive technology and mirrors the resolved side" | TIP-ARIA-03, TIP-DOM-15, TIP-POS-07, TIP-STA-13 |
| "is marked uncentered when it cannot point at the anchor" / "is not marked uncentered when it can point at the anchor" | TIP-POS-06, TIP-DOM-15 |

### `popup/TooltipPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root/Portal/Positioner) | TIP-STR-06; RND-DOM-36..48 |
| "should render the children" | TIP-STR-06 |
| "throws a descriptive error when rendered outside <Tooltip.Positioner>" | TIP-EDGE-04 |

### `portal/TooltipPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root) | TIP-STR-04, TIP-STA-14; RND-DOM-36..48 |
| "prop: keepMounted" › "renders the closed popup as hidden instead of unmounting it" | TIP-API-21, TIP-DOM-06, TIP-DOM-07, TIP-ANI-01 |
| "unmounts the closed popup by default" | TIP-API-21, TIP-DOM-06 |

### `positioner/TooltipPositioner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root/Portal) | TIP-STR-05; RND-DOM-36..48 |
| "throws a descriptive error when rendered outside <Tooltip.Root>" | TIP-EDGE-02 |
| "throws a descriptive error when rendered outside <Tooltip.Portal>" | TIP-EDGE-03, TIP-STR-04 |
| positionerConformanceTests (`viewport: true`, trigger substituted with `<div />`): "offsets the positioner when a number is specified" / "offsets the positioner when a function is specified" / "rests exactly at collisionPadding from the colliding edge" / "reads $description inside the function" (cited 2026-10-28, M4 track 6; the port restates it as "applies the override for $description", design point 2 of the positioning track) | TIP-POS-01, TIP-DOM-01; FLT-POS-06, FLT-POS-15 |
| positionerConformanceTests "uses transform positioning without Viewport" / "uses top/left positioning with Viewport" / "updates positioning when Viewport mounts and unmounts" | TIP-POS-05, TIP-DOM-16; FLT-DOM-03/04 |
| `TooltipPositioner.spec.tsx` (type-level: `keepMounted` rejected) | TIP-POS-09 |

### `viewport/TooltipViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root with Trigger/Portal/Positioner/Popup) | TIP-STR-08; RND-DOM-36..48 |
| viewportConformanceTests (`openOn: 'focus'`, triggers with `delay={0}`, Root function child): "renders children in the `current` container by default" / "remounts the `current` container when the active trigger changes" / "creates morphing containers during transitions" / "sets `data-activation-direction` from the previous to the new trigger" / "creates morphing containers after a kept-mounted popup closes and reopens" / "shows the latest content after rapid trigger changes" / "keeps the latest transition active during rapid trigger changes" / "cleans up the transition when a lagging payload remounts the current container"; the two templates, cited 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)" and "anchors the popup for side=$side in $direction mode" | TIP-DOM-16, TIP-API-14, TIP-FOC-04, TIP-ANI-09; PSH-STR-07, PSH-DOM-07..11 |
| "anchors the positioner from the far edge while it transitions" | TIP-POS-05, TIP-DOM-08 |
| "should mirror the instant animation type of the tooltip" | TIP-ANI-10, TIP-STA-13, TIP-DOM-16 |

### `trigger/TooltipTrigger.spec.tsx`

Type-level only: the substitute callback's `props` argument is typed (not `any`) — covered by RND-API-03. No requirement.

### Requirements that exist only because of a test

TIP-PTR-13, TIP-PTR-22, TIP-PTR-23, TIP-PTR-24, TIP-PTR-25, TIP-FOC-05, TIP-EVT-07, TIP-ANI-04, TIP-ANI-07, TIP-DOM-10, TIP-EDGE-11, TIP-EDGE-12, TIP-EDGE-13 were added to their areas because a test asserts them; they are not stated in the public docs.

## 14. React-specific notes and porting considerations

Non-normative.

- **Store and subscriptions.** The Root owns a `ReactStore` (`TooltipStore`); parts subscribe to individual selectors (`useState('open')`, `useState('isOpenedByTrigger', id)`, …) so only parts whose selected value changed re-render. TIP-EDGE-11 (bystander triggers do not re-render) depends on this: the interaction attribute bag is one object for active and inactive triggers and inactive triggers select `undefined` (`appliedTriggerProps`). A port with a different reactivity model must still avoid touching inactive triggers' DOM and must not invoke their substitute callbacks. Port: see decisions/05-state-store-and-event-details.md §1.
- **Layout-effect ("post-commit, pre-paint") phases in order.** On each commit the Root runs: `PopupHandleAttachment` (handle attach, before any descendant effect), controlled-prop sync (`useControlledProp`), synced values (`trackCursorAxis`/`disableHoverablePopup`/`disabled`), implicit-active-trigger reconciliation (`useImplicitActiveTrigger`), the transitions sync (`useOpenStateTransitions`), the `disabled` close (TIP-DIS-06), the instant-type override (TIP-STA-06/07), the payload clear (TIP-STA-10). Triggers run their registration/migration and forwarded-state effects; the delay-group effects (`useDelayGroup`) run in the Trigger and write `isInstantPhase` through `useSyncedValue`. The Root reads `isInstantPhase` from the store in a *later* commit than the one in which the Trigger wrote it; the test "toggles instant animations…" therefore uses `waitFor` for `data-instant="delay"`. A port may apply the override in the same update as long as the observable attribute is present before the popup is painted. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (post-commit, pre-paint C# state work) and §Relaxation table (row UTL-STA-19..22); the delay group itself is in the `interactions` module, decisions/04-js-interop-boundary.md §1.
- **Synchronous flush for hover.** `applyPopupOpenChange` wraps the state commit in `ReactDOM.flushSync` for `trigger-hover` changes (EVD-EVT-18) so the DOM reflects the open state before the hover handler returns; `onMouseMove` for touch pointer types also flushes. Other reasons commit in React's batched update. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (row EVD-EVT-18, PSH-ANI-03).
- **Native vs. synthetic listeners.** The hover interaction attaches `mouseenter`/`mouseleave` as *native* listeners on the trigger element (effect keyed on the element), while `onPointerDown`/`onPointerEnter`/`onMouseMove` are merged React props; the Trigger's own `onMouseOver` (nested detection) is a React prop that sees bubbling `mouseover` from descendants (including composed events from shadow roots). The Popup's hover listeners are native and attached only while the popup element exists. A port must reproduce the same event kinds (enter/leave do not bubble; over/out do). Port: see decisions/04-js-interop-boundary.md §1 (`interactions` module: hover with safe polygon, focus-to-open, dismissal, cursor anchoring).
- **Timer ownership.** The hover open/close timer and rest timer live in a per-Root shared object (`HoverInteraction` stored in the floating root context data) so all Triggers of one tooltip cancel each other's pending opens; the nested-reopen timer is per Trigger (`useTimeout`). Delay-group timers are per Provider. All are macrotask timers (UTL-EVT-01) cleared on unmount. Port: see decisions/04-js-interop-boundary.md §1 (hover and delay-group timers live in the `interactions` module) and decisions/01-hosting-model-and-timing.md §Decision item 4 (macrotask / N ms row).
- **Refs read during render.** `isNestedTriggerHoveredRef`, `pointerTypeRef`, `previousInstantTypeRef` and `triggerElementRef` are mutable cells; `disabledRef` mirrors the latest `disabled` for timer callbacks. Model them as instance fields.
- **`fastComponent`/`fastComponentRef`.** Root and Trigger are wrapped in a memoizing component factory; this only affects re-render frequency, not behavior. Port: see decisions/05-state-store-and-event-details.md §1.
- **Server rendering and hydration.** Detached triggers read `handle.serverStore` (the fallback store) on the server and during hydration (`useSyncExternalStore` server snapshot), then migrate to the live store after hydration; TIP-EDGE-12/13 rely on the active-trigger reconciliation treating an id that was never seen registered as *pending* rather than lost (PSH-STA-24). React 17 has no `useId`, so generated trigger ids appear only after the first commit (RND-EDGE-09); the registration effect re-runs when the id resolves. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (ids drawn at first render) and decisions/05-state-store-and-event-details.md §1 (handles with attached-store stack and NullStore fallback).
- **StrictMode.** Registration (`useTriggerRegistration`) and handle attachment are written to survive effect double-invocation; `popupConformanceTests` runs the `onOpenChangeComplete` tests with `strict: false` because StrictMode replays effects and would report an instant completion twice.
- **`React.useImperativeHandle`** fills `actionsRef.current` after commit; calling `unmount()` while open defers to the next commit (TRN-ANI-20).
- **Portals.** The Popup subtree lives in a portal node under `document.body` (or `container`); React event bubbling still follows the component tree, which is why the Trigger's `onMouseOver` does **not** fire for hovers over the outer popup's own content unless the pointer actually moves onto a nested trigger element, and why the test "should not open the outer tooltip when moving from the outer popup to a nested trigger" works through a real `mouseover` on the nested trigger. Port: see decisions/04-js-interop-boundary.md §5.
- **Test environment flags.** `BASE_UI_ANIMATIONS_DISABLED` (TRN-EDGE-01) makes every animations-finished wait synchronous; jsdom always matches `:focus-visible` (TIP-A11Y-01). Port: see decisions/06-testing-harness.md §2.

## 15. Requirement index

Every ID defined in this document, grouped by area (conventions §3.3 order) and numeric within each area.

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TIP-STR-01 | MUST | `Tooltip.Provider` renders no element of its own… | 2 |
| TIP-STR-02 | MUST | `Tooltip.Root` renders no element of its own… | 2 |
| TIP-STR-03 | MUST | `Tooltip.Trigger` renders a `<button>` by default… | 2 |
| TIP-STR-04 | MUST | `Tooltip.Portal` MUST be a descendant of a `Tooltip.Root` (else TIP-EDGE-02) and is the require… | 2 |
| TIP-STR-05 | MUST | `Tooltip.Positioner` renders a `<div>` by default and MUST be a descendant of both a… | 2 |
| TIP-STR-06 | MUST | `Tooltip.Popup` renders a `<div>` by default and MUST be a descendant of a `Tooltip.Positioner`… | 2 |
| TIP-STR-07 | MUST | `Tooltip.Arrow` renders a `<div>` by default and MUST be a descendant of a `Tooltip.Positioner`… | 2 |
| TIP-STR-08 | MUST | `Tooltip.Viewport` renders a `<div>` by default, MUST be a descendant of a `Tooltip.Positioner`… | 2 |
| TIP-STR-09 | MUST | Any number of Triggers (contained, detached through a shared handle, or a mix) MAY target one R… | 2 |
| TIP-STR-10 | MUST | A `Tooltip.Root` MAY be rendered *inside another tooltip's Trigger* (nested tooltips, e.g… | 2 |
| TIP-STR-11 | MUST | The Root, Provider, Portal and the Positioner/Popup/Arrow/Viewport chain impose no DOM adjacenc… | 2 |
| TIP-API-01 | MUST | `Provider.delay` defaults to `undefined`… | 3.1 |
| TIP-API-02 | MUST | `Provider.closeDelay` defaults to `undefined` (immediate close)… | 3.1 |
| TIP-API-03 | MUST | `Provider.timeout` defaults to `400` and is passed to the delay group as `timeoutMs`… | 3.1 |
| TIP-API-04 | MUST | `defaultOpen` defaults to `false` and seeds the component-owned open state once… | 3.2 |
| TIP-API-05 | MUST | `open`, when not `undefined`, makes the open state consumer-owned… | 3.2 |
| TIP-API-06 | MUST | `onOpenChange` is optional… | 3.2 |
| TIP-API-07 | MUST | `onOpenChangeComplete` is optional and follows TRN-EVT-01/02… | 3.2 |
| TIP-API-08 | MUST | `actionsRef`, when supplied, receives an object with `unmount()` (ends the closing phase, TRN-E… | 3.2 |
| TIP-API-09 | MUST | `disabled` defaults to `false`… | 3.2 |
| TIP-API-10 | MUST | `disableHoverablePopup` defaults to `false`… | 3.2 |
| TIP-API-11 | MUST | `trackCursorAxis` defaults to `'none'` and accepts `'none' \| 'x' \| 'y' \| 'both'`… | 3.2 |
| TIP-API-12 | MUST | `handle` defaults to `undefined`… | 3.2 |
| TIP-API-13 | MUST | `triggerId` (default `undefined`, consumer-owned when defined… | 3.2 |
| TIP-API-14 | MUST | When `children` is a function it is called with `{ payload }` where `payload` is the active tri… | 3.2 |
| TIP-API-15 | MUST | The effective hover open delay of a Trigger is resolved on every hover as… | 3.3 |
| TIP-API-16 | MUST | The effective hover close delay is… | 3.3 |
| TIP-API-17 | MUST | `closeOnClick` defaults to `true`… | 3.3 |
| TIP-API-18 | MUST | A Trigger's `disabled`, when not `undefined`, replaces the Root's `disabled` for this trigger… | 3.3 |
| TIP-API-19 | MUST | `payload` defaults to `undefined` and is copied into the store's `payload` whenever this trigge… | 3.3 |
| TIP-API-20 | MUST | The Trigger's `id` prop is both its rendered `id` and its registry/trigger id… | 3.3 |
| TIP-API-21 | MUST | `keepMounted` defaults to `false`… | 3.4 |
| TIP-API-22 | MUST | `container` is resolved per FLT-API-13… | 3.4 |
| TIP-API-23 | MUST | `Tooltip.createHandle()` returns a `Tooltip.Handle` whose `open(triggerId)` requires a string i… | 3.9 |
| TIP-API-24 | MUST | The complete export surface of `@base-ui/react/tooltip` is… | 3.9 |
| TIP-STA-01 | MUST | A freshly created Tooltip store has the common initial values of PSH-STA-01 plus exactly the To… | 4.1 |
| TIP-STA-02 | MUST | The *effective open state* that every part renders and that `handle.isOpen` reports is… | 4.1 |
| TIP-STA-03 | MUST | The open state is consumer-owned if `open !== undefined` on the Root's first render and compone… | 4.1 |
| TIP-STA-04 | MUST | Every applied (non-canceled) open change records its `reason` as `openChangeReason`… | 4.1 |
| TIP-STA-05 | MUST | On every applied open change the base instant type is derived per EVD-EVT-17… | 4.1 |
| TIP-STA-06 | MUST | Post-commit, pre-paint, the Root overrides `instantType` with `'delay'` whenever (a) the transi… | 4.1 |
| TIP-STA-07 | MUST | When neither condition of TIP-STA-06 holds and a value was remembered, `instantType` is restore… | 4.1 |
| TIP-STA-08 | MUST | Each Trigger registers with the enclosing Provider's delay group using… | 4.1 |
| TIP-STA-09 | MUST | `closeOnClick` and `closeDelay` (resolved per TIP-API-16) are the Trigger's forwarded state (PS… | 4.1 |
| TIP-STA-10 | MUST | Post-commit, pre-paint of any commit in which the tooltip is effectively open and no active tri… | 4.1 |
| TIP-STA-11 | MUST | The Trigger's state object is exactly `{ open }` with `open = isOpenedByTrigger(ownId)` (PSH-ST… | 4.1 |
| TIP-STA-12 | MUST | The Positioner's state object is `{ open, side, align, anchorHidden, instant }` where `instant`… | 4.1 |
| TIP-STA-13 | MUST | The Popup's state object is `{ open, side, align, instant, transitionStatus }`, the Arrow's… | 4.1 |
| TIP-STA-14 | MUST | Provider, Root and Portal expose an empty state object (`{}`)… | 4.1 |
| TIP-STA-15 | MUST | The Root's dismiss and cursor-tracking interactions (and therefore the interaction attribute ba… | 4.1 |
| TIP-STA-16 | MUST | The Root's `trackCursorAxis`, `disableHoverablePopup` and `disabled` props are mirrored into th… | 4.1 |
| TIP-STA-17 | MUST | `mounted` and `transitionStatus` follow the TRN popup machine configured with… | 4.1 |
| TIP-DOM-01 | MUST | The Trigger renders a `<button type="button">` unless substituted, with `id` equal to its trigg… | 5.1 |
| TIP-DOM-02 | MUST | `data-popup-open=""` is present iff the Trigger's `open` state (TIP-STA-11) is `true`… | 5.1 |
| TIP-DOM-03 | MUST | `data-trigger-disabled=""` is present iff the Trigger's effective `disabled` (TIP-API-18) is… | 5.1 |
| TIP-DOM-04 | MUST | `data-base-ui-tooltip-trigger=""` is present iff the Trigger is **not** disabled… | 5.1 |
| TIP-DOM-05 | MUST | The Trigger element carries, merged in this order (later wins for same-name attributes… | 5.1 |
| TIP-DOM-06 | MUST | The Portal renders its portal node (FLT-DOM-11… | 5.2 |
| TIP-DOM-07 | MUST | The Positioner is the shared positioner element of FLT-DOM-01 configured with… | 5.3 |
| TIP-DOM-08 | MUST | The Positioner emits `data-open`/`data-closed` (exactly one) and `data-anchor-hidden` through t… | 5.3 |
| TIP-DOM-09 | MUST | While the transition status is `'starting'` the Positioner carries inline… | 5.3 |
| TIP-DOM-10 | MUST | Until the first positioning pass of an open completes, the Positioner's inline style is… | 5.3 |
| TIP-DOM-11 | MUST | The Popup carries `tabindex="-1"` and `data-base-ui-focusable=""` (PSH-DOM-15)… | 5.4 |
| TIP-DOM-12 | MUST | The Popup emits `data-open`/`data-closed`, `data-starting-style`/`data-ending-style` (PSH-DOM-05)… | 5.4 |
| TIP-DOM-13 | MUST | While `'starting'` the Popup carries inline… | 5.4 |
| TIP-DOM-14 | MUST | The Popup receives the Root's popup interaction bag… | 5.4 |
| TIP-DOM-15 | MUST | The Arrow renders with inline… | 5.5 |
| TIP-DOM-16 | MUST | The Viewport is the PSH viewport (PSH-STR-07, PSH-DOM-07..12)… | 5.6 |
| TIP-DOM-17 | MUST | On every part, a consumer-supplied attribute with the same name as a part-supplied attribute or… | 5.6 |
| TIP-ARIA-01 | MUST | The Popup element carries no `role` attribute (in particular not `role="tooltip"`) unless the c… | 5.7 |
| TIP-ARIA-02 | MUST NOT | The Trigger MUST NOT receive `aria-describedby`, `aria-labelledby` or any other reference to th… | 5.7 |
| TIP-ARIA-03 | MUST | The Arrow carries `aria-hidden="true"`. | 5.7 |
| TIP-ARIA-04 | MUST | The Positioner carries `role="presentation"`. | 5.7 |
| TIP-ARIA-05 | MUST NOT | The Trigger MUST NOT render `aria-expanded`, `aria-controls` or `aria-haspopup` (PSH-ARIA-01… | 5.7 |
| TIP-KEY-01 | MUST | Root configures the dismiss mechanism's Escape handling; result per FLT-KEY-01/FLT-EVT-05… | 6.1 |
| TIP-KEY-02 | MUST | Escape during IME composition does not close (dismiss configuration of TIP-KEY-01; per FLT-KEY-03) | 6.1 |
| TIP-KEY-03 | MUST | No other key is handled by any Tooltip part… | 6.1 |
| TIP-KEY-04 | MUST | Focus interaction config; per FLT-FOC-01/03, focus after an Escape or press close does not reopen | 6.1 |
| TIP-PTR-01 | MUST | Hover opening is restricted to mouse-like pointers… | 6.2 |
| TIP-PTR-02 | MUST | With an effective open delay `d > 0` (TIP-API-15) the hover interaction is configured in *rest-… | 6.2 |
| TIP-PTR-03 | MUST | With an effective open delay of `0`, `mouseenter` on a closed tooltip's Trigger opens it immedi… | 6.2 |
| TIP-PTR-04 | MUST | While the enclosing Provider's delay group is active (a grouped tooltip is open, or closed less… | 6.2 |
| TIP-PTR-05 | MUST | `mouseleave` of the Trigger while the tooltip is open by hover (and no hoverable strategy appli… | 6.2 |
| TIP-PTR-06 | MUST | When `!disableHoverablePopup && trackCursorAxis !== 'both'`, the Trigger's hover interaction us… | 6.2 |
| TIP-PTR-07 | MUST | While open, `mouseleave` of the active Trigger whose `relatedTarget` is inside another *enabled… | 6.2 |
| TIP-PTR-08 | MUST | A disabled Trigger (TIP-API-18) is not an enabled trigger for TIP-PTR-07… | 6.2 |
| TIP-PTR-09 | MUST | `mouseenter` on the same Trigger while the tooltip is closing after a hover close (transition s… | 6.2 |
| TIP-PTR-10 | MUST | A `pointerdown` or `click` on a Trigger with `closeOnClick` while the tooltip is **not** open i… | 6.2 |
| TIP-PTR-11 | MUST | A `pointerdown` or `click` on a Trigger that carries the Root's interaction bag (the trigger th… | 6.2 |
| TIP-PTR-12 | MUST | With `closeOnClick={false}` on the pressed Trigger, neither TIP-PTR-10 nor TIP-PTR-11 applies… | 6.2 |
| TIP-PTR-13 | MUST | After a press close, a subsequent `mouseenter` + `mousemove` on the same Trigger (even without… | 6.2 |
| TIP-PTR-14 | MUST | Root configures sloppy outside-press dismissal; result per FLT-PTR-26/27, FLT-ALG-17, FLT-DIS-04..08/11 | 6.2 |
| TIP-PTR-15 | MUST | A press on an inactive registered Trigger is never an outside press (FLT-DIS-05)… | 6.2 |
| TIP-PTR-16 | MUST | Root configures cursor anchoring for every Trigger when tracking; result per FLT-PTR-07..10, FLT-ALG-25 | 6.2 |
| TIP-PTR-17 | MUST | When a `mouseover` reaching Trigger O targets an enabled nested trigger, O MUST cancel its pend… | 6.2 |
| TIP-PTR-18 | MUST | When a `mouseover` targeting an enabled nested trigger reaches O while O's tooltip is open **an… | 6.2 |
| TIP-PTR-19 | MUST | When a `mouseover` reaching O targets an element inside O that is **not** inside a nested trigg… | 6.2 |
| TIP-PTR-20 | MUST | The scheduled local reopen is cancelled by `mouseleave` of O and by a later `mouseover` targeti… | 6.2 |
| TIP-PTR-21 | MUST | Nested-trigger detection considers only elements carrying the enabled marker (TIP-DOM-04)… | 6.2 |
| TIP-PTR-22 | MUST | The local reopen (TIP-PTR-19) MUST NOT run for a `touch` pointer type recorded on O… | 6.2 |
| TIP-PTR-23 | MUST | If the hovered Trigger is removed from the document while its hover open is pending, the pendin… | 6.2 |
| TIP-PTR-24 | MUST | Pointer-type bookkeeping… | 6.2 |
| TIP-PTR-25 | MUST | Trigger `disabled` is independent of the rendered element being disabled… | 6.2 |
| TIP-PTR-26 | MUST | Popup configures the popup-side hover mechanism; result per FLT-PTR-23/24… | 6.2 |
| TIP-FOC-01 | MUST | A `focus` event on a non-disabled Trigger whose target matches `:focus-visible` (keyboard focus… | 6.3 |
| TIP-FOC-02 | MUST | Trigger blur closes after 0 ms with `trigger-focus`, exceptions per FLT-FOC-04 | 6.3 |
| TIP-FOC-03 | MUST | Focus moving from the active Trigger to a **disabled** Trigger of the same tooltip closes it (a… | 6.3 |
| TIP-FOC-04 | MUST | Focus moving from the active Trigger to another enabled Trigger of the same tooltip hands the o… | 6.3 |
| TIP-FOC-05 | MUST | A `focus` event that reaches an outer Trigger O but targets an enabled nested trigger (TIP-DOM-… | 6.3 |
| TIP-FOC-06 | MUST NOT | Opening a tooltip MUST NOT move focus (the Popup, although `tabindex="-1"`, is not focused), an… | 6.3 |
| TIP-FOC-07 | MUST | Window-blur focus block on the Trigger per FLT-FOC-02 | 6.3 |
| TIP-FOC-08 | MUST | A disabled Trigger (TIP-API-18) MUST NOT open on focus, including when the Root is disabled. | 6.3 |
| TIP-DIS-01 | MUST | Escape dismissal per TIP-KEY-01… | 6.4 |
| TIP-DIS-02 | MUST | Outside-press dismissal per TIP-PTR-14… | 6.4 |
| TIP-DIS-03 | MUST NOT | Scrolling, resizing, or the window losing focus MUST NOT close an open tooltip (there is no scr… | 6.4 |
| TIP-DIS-04 | MUST | When another tooltip in the same Provider group opens while this one is the group's active memb… | 6.4 |
| TIP-DIS-05 | MUST | When the resolved active Trigger unregisters while the tooltip is open (contained or detached)… | 6.4 |
| TIP-DIS-06 | MUST | Post-commit, pre-paint of a commit in which the stored open state is `true` and `disabled` is… | 6.4 |
| TIP-DIS-07 | MUST | `actionsRef.current.close()` and `handle.close()` request a close with reason… | 6.4 |
| TIP-DIS-08 | MUST | An Escape close does not propagate to ancestor popups unless `allowPropagation()` was called (T… | 6.4 |
| TIP-EVT-01 | MUST | `onOpenChange(nextOpen, details)` is called with a change-event details object (EVD-API-01) ext… | 7 |
| TIP-EVT-02 | MUST | `onOpenChange` runs before the store, the DOM and the internal open-change notification are upd… | 7 |
| TIP-EVT-03 | MUST | Calling `details.cancel()` inside `onOpenChange` prevents the change in component-owned mode (n… | 7 |
| TIP-EVT-04 | MUST | `details.preventUnmountOnClose()` called during a non-canceled close keeps the popup mounted (w… | 7 |
| TIP-EVT-05 | MUST | `onOpenChangeComplete(true)` fires once per open on animations finished for the Popup element (… | 7 |
| TIP-EVT-06 | MUST NOT | `onOpenChange` MUST NOT be called for changes of the controlled `open` prop or for the initial… | 7 |
| TIP-EVT-07 | MUST NOT | Re-hovering (mouseenter/mousemove) the Trigger that already owns the open tooltip MUST NOT requ… | 7 |
| TIP-EVT-08 | MUST | The "cancel pending open" press (TIP-PTR-10) emits an **internal** open-change notification… | 7 |
| TIP-EVT-09 | MUST | For hover/focus opens `details.trigger` is the Trigger element that received the event (so… | 7 |
| TIP-EVT-10 | MUST | Calling `details.allowPropagation()` inside `onOpenChange` for an `escape-key` close lets the k… | 7 |
| TIP-ANI-01 | MUST | The Portal subtree is mounted when the tooltip becomes effectively open and stays mounted throu… | 9 |
| TIP-ANI-02 | MUST | On open the Popup and Positioner carry `data-starting-style` (and inline… | 9 |
| TIP-ANI-03 | MUST | Delay-group instant animations… | 9 |
| TIP-ANI-04 | MUST | When a grouped tooltip is still exiting (exit transition running) and another grouped tooltip o… | 9 |
| TIP-ANI-05 | MUST | `actionsRef.current.unmount()` ends the closing phase immediately (TRN-EVT-08)… | 9 |
| TIP-ANI-06 | MUST | A `preventUnmountOnClose()` opt-out belongs to one close cycle… | 9 |
| TIP-ANI-07 | MUST | The Positioner's pre-positioning… | 9 |
| TIP-ANI-08 | MUST | The instant type produced by TIP-STA-05 is observable as `data-instant="focus"` from the openin… | 9 |
| TIP-ANI-09 | MUST | Switching the active trigger while open (hover/focus hand-off, `handle.open(otherId)`) keeps th… | 9 |
| TIP-ANI-10 | MUST | The Viewport mirrors the store's instant type (`data-instant` with the same value and timing as… | 9 |
| TIP-POS-01 | MUST | The Positioner's defaults are… | 10 |
| TIP-POS-02 | MUST | The default anchor is the store's active trigger element (FLT-STR-10… | 10 |
| TIP-POS-03 | MUST | With `trackCursorAxis` `'x'`, `'y'` or `'both'` the anchor rect is the cursor virtual anchor of… | 10 |
| TIP-POS-04 | MUST | The Portal's `keepMounted` is forwarded to the positioning session (FLT-API-17), which then use… | 10 |
| TIP-POS-05 | MUST | While a Viewport is mounted the store's `adaptiveOrigin` middleware (PSH) is passed to position… | 10 |
| TIP-POS-06 | MUST | The Arrow is positioned by the FLT arrow middleware with `arrowPadding` (default `5`) as the mi… | 10 |
| TIP-POS-07 | MUST | The rendered `side`/`align` exposed on Positioner, Popup and Arrow state (and `data-side`/… | 10 |
| TIP-POS-08 | MUST | The Positioner's inline style carries `--available-width`, `--available-height` (seeded `100vw`… | 10 |
| TIP-POS-09 | MUST NOT | `Tooltip.Positioner` MUST NOT accept a `keepMounted` prop (it is a type error)… | 10 |
| TIP-A11Y-01 | MUST | "Keyboard focus" for TIP-FOC-01 is decided by `element.matches(':focus-visible')` on the focuse… | 6.5 |
| TIP-A11Y-02 | SHOULD | Mac Safari focus-from-outside modality rule per FLT-A11Y-06 | 6.5 |
| TIP-A11Y-03 | MUST NOT | Tooltip MUST NOT announce its content… | 6.5 |
| TIP-EDGE-01 | MUST | Rendering `Tooltip.Trigger` with neither an enclosing `Tooltip.Root` nor a `handle` throws an e… | 12 |
| TIP-EDGE-02 | MUST | Rendering `Tooltip.Portal`, `Tooltip.Positioner`, `Tooltip.Popup`, `Tooltip.Arrow` or… | 12 |
| TIP-EDGE-03 | MUST | Rendering `Tooltip.Positioner` inside a Root but outside a `Tooltip.Portal` throws… | 12 |
| TIP-EDGE-04 | MUST | Rendering `Tooltip.Popup`, `Tooltip.Arrow` or `Tooltip.Viewport` outside a `Tooltip.Positioner`… | 12 |
| TIP-EDGE-05 | MUST | `handle.open(id)` with an id that matches no registered trigger (searching the attached Roots'… | 12 |
| TIP-EDGE-06 | SHOULD | `handle.open()` / `handle.close()` called while no Root is attached log the PSH development war… | 12 |
| TIP-EDGE-07 | SHOULD | Two mounted Roots sharing one handle beyond a one-frame overlap log the PSH "A handle is attach… | 12 |
| TIP-EDGE-08 | SHOULD | Switching `open` (or `triggerId`) between defined and `undefined` after the first render logs t… | 12 |
| TIP-EDGE-09 | SHOULD | Registering the same DOM element under two different trigger ids in one store throws in develop… | 12 |
| TIP-EDGE-10 | MUST | `defaultOpen` (or `open={true}`) together with `disabled` renders the tooltip closed and MUST N… | 12 |
| TIP-EDGE-11 | MUST NOT | Opening, closing or moving the tooltip between triggers (imperatively, by focus or by hover) MU… | 12 |
| TIP-EDGE-12 | MUST | Server rendering / hydration… | 12 |
| TIP-EDGE-13 | MUST | Moving a controlled `triggerId` to an id whose Trigger has not registered yet (not hydrated / n… | 12 |
| TIP-EDGE-14 | MUST | A Root unmounted while open (or while kept mounted after a prevented close) and remounted with… | 12 |
| TIP-EDGE-15 | MUST | Detached Triggers keep working when their ancestors are added/removed (reparenting) and when th… | 12 |
| TIP-EDGE-16 | MUST | Nested tooltips rely on the private marker of TIP-DOM-04… | 12 |
| TIP-EDGE-17 | MUST | RTL… | 12 |


## 16. Open questions

1. **No `role="tooltip"` and no `aria-describedby` in 1.9.0.** The assignment hints at "Trigger `aria-describedby` wiring to the popup id" and "Popup `role="tooltip"`"; neither exists: `tooltip/popup/TooltipPopup.tsx:63-73` merges no `role`, `tooltip/trigger/TooltipTrigger.tsx:223-266` merges no `aria-*`, and `popupConformanceTests` is invoked without `expectedPopupRole` (`root/TooltipRoot.test.tsx:27-40`), which skips the suite's ARIA block. The docs frame the component as visual-only (`components/tooltip/page.mdx` §Usage guidelines). Documented as TIP-ARIA-01/02/05 and TIP-A11Y-03. A port that wants APG-conformant semantics could add `role="tooltip"` + `aria-describedby` as an extension, but must not depend on them for test parity, and the trigger's accessible name remains the consumer's responsibility. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): resolved as written: `TooltipPopup` renders no `role` and `TooltipTrigger` no `aria-*` (TIP-ARIA-01..05); no `role="tooltip"` or `aria-describedby` extension (out of the brief's scope).
2. **Positioner emits `data-instant` (including the undocumented value `tracking-cursor`).** `TooltipPositioner.tsx:82` puts `instant: trackCursorAxis !== 'none' ? 'tracking-cursor' : instantType` into the Positioner state, and the default projection emits it as `data-instant`; the outline (`components/page.mdx` Positioner data attributes) and `TooltipPositionerDataAttributes.ts` do not list it, and no test asserts it. Documented as TIP-STA-12 / TIP-DOM-08; confirm whether the Blazor port should emit it (demo CSS targets `.Positioner[data-instant]` to disable position transitions, so it is relied upon by the official demos: `demos/detached-triggers-full/css-modules/index.module.css:92-94`). Resolved: [transitions-and-mounting](../architecture/transitions-and-mounting.md) TRN-DOM-10 lists the Tooltip Positioner value domain including `'tracking-cursor'`, so the port emits it per TIP-DOM-08. `data-instant` is not on the JavaScript-owned attribute list of decisions/04-js-interop-boundary.md §2, so C# writes it. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); chosen by the user 2026-10-27, ADR 04 Amendments 2026-10-27): every part's state carries `Instant` and C# renders `data-instant` through the default projection, the Positioner's `tracking-cursor` included (`TooltipInstantType.TrackingCursor`); §5.3/§5.4's `Writer` cells read `C#`.
3. **Private marker attribute `data-base-ui-tooltip-trigger`.** Emitted on every enabled Trigger (`TooltipTrigger.tsx:31,261`) and used only for nested-trigger detection; it is not in the outline. Because it is merged before consumer props, a consumer could override it. Documented as TIP-DOM-04 / TIP-EDGE-16; the port may rename it but must keep the enabled/disabled semantics. Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): the name is kept, `data-base-ui-tooltip-trigger`, on every enabled Trigger, with the enabled/disabled semantics of TIP-DOM-04; the `interactions` module's nested-trigger lookup reads it.
4. **Hinted Root props that do not exist.** `delay`, `closeDelay` and `hoverable` are not Root props in 1.9.0: `delay`/`closeDelay` moved to the Trigger and `hoverable` became `disableHoverablePopup` (CHANGELOG v1.0.0-beta.5, `TooltipRoot.tsx:152-218`). Documented on the Trigger (TIP-API-15/16) and Root (TIP-API-10).
5. **Internal `trigger-press` notification without `onOpenChange`.** `TooltipStore.cancelPendingOpen` (`TooltipStore.ts:93-99`) dispatches `{ open: false, reason: 'trigger-press' }` on the floating root bus to cancel a pending hover open and block the following focus, but never calls `onOpenChange` and never changes state (EVD §10 Q10). No test asserts the absence of a callback. Documented as TIP-PTR-10 / TIP-EVT-08; a port must implement the two side effects without surfacing a callback. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): replicated: the pending-open cancel stays in the `interactions` module (`cancelPendingOpen`), which emits the session's `openchange` alone, reports nothing to C# and calls no `OnOpenChange` (TIP-PTR-10, TIP-EVT-08).
6. **Root `disabled` + Trigger `disabled={false}` issues a transient open request.** With the Root disabled and a Trigger opting back in, the Trigger's hover/focus interactions are enabled (`TooltipTrigger.tsx:147,166` use the trigger-level `disabled`), so a hover calls `store.setOpen(true)` → `onOpenChange(true, trigger-hover)`, the stored `open` becomes `true`, and the Root's post-commit effect immediately reverts it with `onOpenChange(false, { reason: 'disabled' })` (`TooltipRoot.tsx:96-100`). The test only asserts that no content appears. Whether the pair of callbacks is intended is unclear; documented in TIP-API-09 as observed. Re-verified (QA 2026-10-14) against the pinned source: Tooltip is the only one of the three hover popups with this pair. [popover](popover.md) §16 Q13: `Popover.Root` has no `disabled`, which exists only per Trigger. [preview-card](preview-card.md) §16 Q11: Preview Card has no `disabled` at all. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); plan §Settled): mirrored: a Trigger with `Disabled="false"` under a disabled Root keeps its hover and focus, so its request runs `OnOpenChange(true, trigger-hover)` and stores the open state, which the disabled close reverts at the render-batch boundary (TIP-DIS-06; `TooltipRoot` observes the request's commit when it renders nothing).
7. **Interactions-mounted condition differs from PSH-STA-43.** `TooltipRoot.tsx:137` mounts the dismiss/cursor interactions when `open || mounted || (!disabled && trackCursorAxis !== 'none')` — there is no `handle` term (Popover/Dialog mount them whenever a handle is present). Detached tooltip triggers therefore receive the dismiss reference bag only once the popup is mounted by them, which is sufficient because hover/focus bags are per Trigger. Documented as TIP-STA-15; PSH-STA-43 should list Tooltip as an exception. Resolved: PSH-STA-43 now states the Tooltip exception (`popup-store-and-handles.md`).
8. **`isInstantPhase` written by every Trigger.** Each Trigger runs `store.useSyncedValue('isInstantPhase', isInstantPhase)` (`TooltipTrigger.tsx:100`) with its own `useDelayGroup` result; with several Triggers in one Root, only the trigger that opened the tooltip receives `true`, and the others keep writing `false` in the same commit, so the final store value depends on effect order (document order of triggers). Multi-trigger instant-phase behavior is untested; documented as TIP-STA-08 for the single-trigger case. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 04 Amendments 2026-10-27): the question disappears: the delay group's phase travels on the open request (`OpenChangeRequest.Instant`), so no Trigger writes an `isInstantPhase` value and the store computes the override from the request it applies (TIP-STA-06).
9. **`delay` resolution ignores pointer type.** `getOpenDelay` is passed as `restMs` and the close delay through `delay()`; neither is resolved with a pointer type, so FLT-API-03's "0 for non-mouse pointers" rule never applies to Tooltip (touch is excluded earlier by `mouseOnly`). Pen pointers get the full delay. Stated in TIP-PTR-01; confirm no shortcut is intended for pen. Correction (QA 2026-10-18): only the open delay is resolved without a pointer type. The Trigger-leave and Popup-leave close delays are resolved with the recorded pointer type (`floating-ui-react/hooks/useHoverReferenceInteraction.ts:195`, `floating-ui-react/hooks/useHoverFloatingInteraction.ts:166`), so a `'touch'` pointer type closes immediately; pen is mouse-like and keeps both delays. TIP-PTR-01 updated.
10. **`TooltipTrigger` is not a button-behavior part.** It renders `<button type="button">` only through the generic default-tag rule (RND-STR-05) and has no `nativeButton` prop; a substituted `<span>`/`<div>` gets no `role`, `tabindex` or key handling (TIP-DOM-01), unlike Popover/Dialog triggers. The nested-tooltip tests rely on `render={<span />}` triggers that are therefore not focusable. Flagged so the port does not add button semantics by analogy.
11. **Positioner `hidden` with `keepMounted` while a close is prevented.** With `keepMounted` and `preventUnmountOnClose()`, the popup stays `mounted` (no `hidden`) until `unmount()`; the Trigger already lost `data-popup-open`. This is the intended TRN contract (TRN-ANI-23) but means a kept-mounted, prevented tooltip is visible-but-"closed" for consumers reading only the trigger attribute; mention for the port's state model. Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): as TRN-ANI-23: with `KeepMounted` and a prevented close the Positioner keeps no `hidden` until `Unmount()`, while the Trigger loses `data-popup-open` at the close (L4 "does not prevent unmounting on later closes", "unmounts the tooltip when the `unmount` method is called").
12. **Outline omissions.** Beyond Q2/Q3, the outline lists `Tooltip.Trigger` props without `id` (treated specially, TIP-API-20) and `Tooltip.Root.State`/`Provider.State`/`Portal.State` as empty types, matching the source. No other discrepancy found between the outline and the source.
13. **Stale `instantType` after closes with unmapped reasons (shared with [preview-card](preview-card.md) §16 Q4; [popover](popover.md) §16 Q11 only partly).** TIP-STA-05 follows the shared open-change routine (`utils/popups/popupStoreUtils.ts:296-302`), which writes `instantType` only for focus opens, press/Escape closes and hover changes. Closes with reason `outside-press`, `trigger-focus` (blur), `imperative-action`, `none` (delay-group hand-off, trigger unmount) or `disabled`, and controlled `open` changes, keep the previous value. So after a focus open, an outside-press close leaves `data-instant="focus"` on the exiting Popup, Arrow and Viewport (and on the Positioner unless the cursor is tracked, TIP-STA-12). The stale value also survives to a later open that is neither hover nor focus (`handle.open`, a controlled `open={true}`). The delay-group override (`tooltip/root/TooltipRoot.tsx:103-118`) temporarily replaces the value with `'delay'` and later restores the captured, possibly stale, value. Not asserted by any test. Suspected bug: TIP-STA-05 records the observed behavior; confirm whether a port should reproduce it. Popover's own store rewrites `instantType` on every applied change, so there only the controlled-close residue exists. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): replicated in `TooltipStore` (L1 "port: a close or open with an unmapped reason keeps the stale instant type"; the overridden value is restored stale by the first change the override no longer covers); Tooltip's note is under ADR 04 Amendments 2026-10-27's untested-quirks bullet; reported upstream.
14. **Spurious `onOpenChange(false)` on a hover-leave of a closed tooltip (shared with [preview-card](preview-card.md) §16 Q5 and [popover](popover.md) §16 Q12).** A Trigger `mouseleave` takes the safe-polygon branch only when a close strategy is configured *and* `dataRef.current.floatingContext` exists (`floating-ui-react/hooks/useHoverReferenceInteraction.ts:315,321-350`). That context is set only once a Positioner has rendered (`floating-ui-react/hooks/useFloating.ts:113`) and is never cleared. In every other case the leave calls `closeWithDelay` (`:194-206,352-359`). The leave path is not gated by `mouseOnly`, so touch-recorded pointers reach it too. Tooltip's close delay defaults to 0 (TIP-API-16), so `store.setOpen(false, trigger-hover)` runs at once (after `closeDelay` ms when positive). `applyPopupOpenChange` has no equality guard (`popupStoreUtils.ts:273`), so `onOpenChange(false, { reason: 'trigger-hover' })` fires on a tooltip that is already closed. This happens (i) on a hover-leave before any Positioner has rendered (never opened, no `keepMounted`) and (ii) on *every* hover-leave of a closed tooltip when no strategy is configured (`disableHoverablePopup` or `trackCursorAxis: 'both'`, TIP-PTR-06). Cancelling the pending open (TIP-PTR-05) is intended and is a requirement; the extra callback is not asserted by any test and is not made one. A port should cancel the pending open without emitting a close. Preview Card shows the same behavior after its 300 ms default close delay; Popover shows it only with `openOnHover`. Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121)): not emitted: a leave while the tooltip is closed cancels the pending open only (the `interactions` module's `closeWithDelay`, FLT §10 Q12's reading); Tooltip's note is under ADR 04 Amendments 2026-10-27's untested-quirks bullet; reported upstream.
15. **Substitute callbacks and owner-computed IDREFs (rendering-and-composition §10 Q8) — not applicable.** Tooltip renders no IDREF attribute: no `aria-describedby`, `aria-controls` or `aria-owns` (TIP-ARIA-02/05), and the lite portal has no owner span. A substitute callback that drops the `id` it is handed therefore leaves nothing dangling. Contrast [popover](popover.md) POP-EDGE-15.
16. **Popup-leave close delay ignores the Provider's `closeDelay`.** The Trigger writes `closeDelay ?? 0` to the store (`tooltip/trigger/TooltipTrigger.tsx:78,87`), and the Popup's leave handler reads that store value (`tooltip/popup/TooltipPopup.tsx:36,48-51`). The Trigger's own leave handler, by contrast, uses the Provider's group close delay when the Trigger has no `closeDelay` (`tooltip/trigger/TooltipTrigger.tsx:152-157`). Inside a Provider with `closeDelay` set and no Trigger `closeDelay`, leaving the Trigger therefore waits the Provider delay, but leaving the hoverable Popup closes at once. No test covers the Popup-leave case. Suspected inconsistency: TIP-API-16 and TIP-PTR-26 record the observed behavior; confirm whether a port should reproduce it. Decided (2026-10-18): replicated by the port and to be reported upstream (ADR 04 Amendments). Resolution 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); ADR 04 Amendments 2026-10-18): replicated: the Trigger forwards `CloseDelay ?? 0` without the Provider's fallback (`TooltipDelays.Forwarded`), and the Popup's leave reads that store value (TIP-STA-09, TIP-PTR-26).
