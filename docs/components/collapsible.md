# Collapsible

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/collapsible/` |
| Tests | `packages/react/src/collapsible/**/*.test.tsx` (3 files) + `root/CollapsibleRoot.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/collapsible/page.mdx` |
| Prefix | `COL` |
| Status | approved |
| Builds on | none |
| Derived by | [accordion](accordion.md) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [button-behavior](../architecture/button-behavior.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Collapsible is a disclosure widget: a button (**Trigger**) that shows and hides a region of content (**Panel**), both grouped under a **Root** that owns the open/closed state. It has no floating positioning, no focus trapping and no dismissal behavior; the whole of its complexity is in the Panel, which measures its own expanded size and exposes it as CSS variables so that consumers can animate `height`/`width` between `0` and the measured value with CSS transitions or keyframe animations, and which supports the HTML `hidden="until-found"` attribute so that closed content stays searchable by the browser's find-in-page.

The Root's state logic (`useCollapsibleRoot`) and the Panel's measurement/animation logic (`useCollapsiblePanel`) are shared verbatim with **Accordion**, which builds `Accordion.Item`/`Accordion.Trigger`/`Accordion.Panel` on top of them (see [accordion](accordion.md)). Everything in this document that is stated for the Panel applies to `Accordion.Panel` as well, except where Accordion is noted to pass different inputs (§11).

What the user experiences: pressing the Trigger toggles the Panel; the Trigger reports `aria-expanded` and, while open, `aria-controls`; the Panel is removed from the document when closed unless `keepMounted` or `hiddenUntilFound` is set; with `hiddenUntilFound`, Ctrl/Cmd+F matches inside the closed Panel expand it immediately without the authored motion.

## 2. Anatomy

```
<Collapsible.Root>        required, exactly 1            default element: div
  <Collapsible.Trigger>   optional, 0..n                 default element: button
  <Collapsible.Panel>     optional, 0..1 (see COL-STR-03) default element: div
```

Trigger and Panel may appear in any order and at any depth below Root; they locate the Root through ancestor-provided shared state, not through DOM adjacency. Nested Collapsibles (a Root inside another Root's Panel) are independent; a Trigger binds to its nearest ancestor Root.

- **COL-STR-01** — MUST. `Collapsible.Root` renders a `<div>` by default and is the only part that owns state; it carries state `data-*` attributes (§5) but attaches no event handlers of its own.
  Source: `collapsible/root/CollapsibleRoot.tsx:61-66`. Test: [L2] `CollapsibleRoot.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **COL-STR-02** — MUST. `Collapsible.Trigger` renders a `<button>` by default and MUST be a descendant of a `Collapsible.Root`; rendering it outside one throws the error in COL-EDGE-01.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:27-33,49`; `collapsible/root/CollapsibleRootContext.ts:15-23`. Test: [L2] `CollapsibleTrigger.test.tsx` "throws when rendered outside a Collapsible.Root".
- **COL-STR-03** — MUST. `Collapsible.Panel` renders a `<div>` by default and MUST be a descendant of a `Collapsible.Root`. A Root is designed for at most one Panel: the Root tracks a single registered panel id (COL-STA-09), and a second Panel would overwrite the first's registration. Multiple Triggers are permitted and all reflect the same state.
  Source: `collapsible/panel/CollapsiblePanel.tsx:47-69,99`; `collapsible/root/useCollapsibleRoot.ts:26-29`. Test: [L2] `CollapsiblePanel.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **COL-STR-04** — MUST. When the Panel is omitted, the Trigger still toggles the open state and still renders `aria-expanded`; while open it renders `aria-controls` pointing at the Root's generated default panel id even though no element carries it (see §16 Q4).
  Source: `collapsible/root/useCollapsibleRoot.ts:26-29`; `collapsible/trigger/CollapsibleTrigger.tsx:54`. Test: [L2] `CollapsibleTrigger.test.tsx` "renders the disabled attribute when disabled" (Root + Trigger only, no Panel).

## 3. Public API

### 3.1 `Collapsible.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Collapsible.Root.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `open` | `boolean \| undefined` | `undefined` (uncontrolled) | Whether the panel is open. Supplying a non-`undefined` value on first render makes the open state consumer-owned. | COL-API-01 |
| `defaultOpen` | `boolean` | `false` | Initial open state when `open` is not supplied. | COL-API-02 |
| `onOpenChange` | `(open: boolean, eventDetails: Collapsible.Root.ChangeEventDetails) => void` | `undefined` | Called when the component wants to open or close (§7). | COL-API-03 |
| `disabled` | `boolean` | `false` | Ignores user interaction on every Trigger and is exposed in every part's state. | COL-API-04 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<div>` attribute | | — | Forwarded to the rendered element. | — |

- **COL-API-01** — MUST. `open` is the consumer-owned (controlled) open state. When it is not `undefined` on the first render, the component never changes the open state itself; it only reports the requested state through `onOpenChange` (COL-STA-03).
  Source: `collapsible/root/useCollapsibleRoot.ts:17-22`; `packages/utils/src/useControlled.ts:41-46,83-90`. Test: [L2] `CollapsibleRoot.test.tsx` "does not change controlled open state without an external update".
- **COL-API-02** — MUST. `defaultOpen` defaults to `false` and is the initial value of the component-owned open state. It is read once; later changes have no effect (and log the error in COL-EDGE-06).
  Source: `collapsible/root/CollapsibleRoot.tsx:26`; `collapsible/root/useCollapsibleRoot.ts:15,17-22`. Test: [L2] `CollapsibleRoot.test.tsx` "uncontrolled mode".
- **COL-API-03** — MUST. `onOpenChange` is optional. When absent, trigger presses still toggle the component-owned state (uncontrolled) or do nothing (controlled).
  Source: `collapsible/root/CollapsibleRoot.tsx:34`; `packages/utils/src/useStableCallback.ts:35-52`. Test: [L2] `CollapsibleRoot.test.tsx` "controlled mode" (no `onOpenChange`, external toggle).
- **COL-API-04** — MUST. `disabled` defaults to `false`. When `true`, every Trigger that does not override it is disabled (COL-PTR-02, COL-KEY-03) and `disabled: true` appears in the state of Root, Trigger and Panel (so `data-disabled` is rendered on all three).
  Source: `collapsible/root/CollapsibleRoot.tsx:27,43-50`; `collapsible/trigger/CollapsibleTrigger.tsx:32,37`. Test: [L2] `CollapsibleRoot.test.tsx` "disabled status".

#### State object (`Collapsible.Root.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Current open state (consumer- or component-owned). |
| `disabled` | `boolean` | The Root's `disabled` prop. |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Root transition status (§4, COL-STA-05). |

Source: `collapsible/root/CollapsibleRoot.tsx:43-50,75-78`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` | `open === true`; empty value | COL-DOM-01 | C# |
| `data-closed` | `open === false`; empty value | COL-DOM-01 | C# |
| `data-disabled` | `disabled === true`; empty value (not listed in the outline, see §16 Q2) | COL-DOM-02 | C# |
| `data-starting-style` | `transitionStatus === 'starting'` | COL-DOM-03 | JS |
| `data-ending-style` | `transitionStatus === 'ending'` | COL-DOM-03 | JS |

#### CSS variables

None.

### 3.2 `Collapsible.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Collapsible.Trigger.State`, identical in shape and value to the Root's state).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>`. Set `false` when substituting a non-button element. | COL-API-05 |
| `disabled` | `boolean \| undefined` | Root's `disabled` | Per-trigger override of the Root's `disabled` (native `<button>` attribute reused as a prop; not listed in the outline, see §16 Q3). | COL-API-06 |
| `id` | `string` | — | Forwarded unchanged. | COL-API-07 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; handlers are chained per COL-PTR-03. | — |

- **COL-API-05** — MUST. `nativeButton` defaults to `true` and is passed as the `native` option of the shared button behavior (§11). With `true` the element receives `type="button"`; with `false` it receives `role="button"` and the keyboard activation emulation described in §6.1.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:39,44-47`; `internals/use-button/useButton.ts:226`. Test: [L2] `CollapsibleTrigger.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`, and the "with a non-native button" re-run).
- **COL-API-06** — MUST. A Trigger's own `disabled` prop, when not `undefined`, replaces the Root's `disabled` for that Trigger only; `undefined` falls back to the Root's value.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:32,37`. Test: none (Root-level `disabled` is tested by "renders the disabled attribute when disabled").
- **COL-API-07** — MUST. The Trigger's `id` prop is rendered as the element's `id` without modification; no id is generated for the Trigger.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:41,58`. Test: [L2] `CollapsibleTrigger.test.tsx` "forwards the id prop".

#### State object (`Collapsible.Trigger.State`)

Same fields as `Collapsible.Root.State` (`open`, `disabled`, `transitionStatus`); the Trigger receives the Root's state object unchanged.
Source: `collapsible/trigger/CollapsibleTrigger.tsx:31,50,67`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-panel-open` | `open === true`; empty value. No attribute is rendered while closed. | COL-DOM-04 | C# |
| `data-disabled` | `disabled === true` (effective, after COL-API-06) | COL-DOM-05 | C# |
| `data-starting-style` | Root `transitionStatus === 'starting'` (not in outline, §16 Q2) | COL-DOM-06 | JS |
| `data-ending-style` | Root `transitionStatus === 'ending'` (not in outline, §16 Q2) | COL-DOM-06 | JS |

#### CSS variables

None.

### 3.3 `Collapsible.Panel`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes (the render function receives `Collapsible.Panel.State`, and may return a different element or nothing — see COL-ANI-15/18/19). **Accepts `className`/`style` as function of state:** yes (state = `Collapsible.Panel.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the document while closed (hidden with the `hidden` attribute). | COL-API-08 |
| `hiddenUntilFound` | `boolean` | `false` | Keep the element in the document while closed and hide it with `hidden="until-found"`; implies `keepMounted`. | COL-API-09 |
| `id` | `string` | generated | The Panel's `id`; also what the Trigger's `aria-controls` references. Empty string is treated as absent. | COL-API-10 |
| `className`, `style`, `render` | per RND | — | `style` (object or function) is resolved against the Panel state and merged after the CSS-variable style (COL-DOM-18). | — |
| any other `<div>` attribute | | — | Forwarded; a consumer `hidden` value overrides the component's (COL-DOM-13). | — |

- **COL-API-08** — MUST. `keepMounted` defaults to `false`. When `false` (and `hiddenUntilFound` is `false`), the Panel element is absent from the document whenever it is closed and not in a closing transition (COL-DOM-16). When `true`, the element stays in the document and is hidden per COL-DOM-13.
  Source: `collapsible/panel/CollapsiblePanel.tsx:60,157`; `collapsible/panel/useCollapsiblePanel.ts:461`. Test: [L2] `CollapsiblePanel.test.tsx` "does not unmount the panel when true".
- **COL-API-09** — MUST. `hiddenUntilFound` defaults to `false`. When `true`, the Panel element is always present in the document regardless of `keepMounted`, is hidden with `hidden="until-found"` while closed (COL-DOM-14), and opens in response to the browser's `beforematch` event (§9.5).
  Source: `collapsible/panel/CollapsiblePanel.tsx:59,151`; `collapsible/panel/useCollapsiblePanel.ts:355-366,461`. Test: [L4] `CollapsiblePanel.test.tsx` "uses `hidden=\"until-found\" to hide panel when true".
- **COL-API-10** — MUST. The Panel's `id` prop, when a non-empty string, is used verbatim as the element id and registered with the Root so the Trigger's `aria-controls` references it. An empty string or `undefined` falls back to the Root's generated default id.
  Source: `collapsible/panel/CollapsiblePanel.tsx:61-69`; `collapsible/panel/useCollapsiblePanel.ts:470`. Test: [L2] `CollapsibleRoot.test.tsx` "references manual panel id in trigger aria-controls".

#### State object (`Collapsible.Panel.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root open state. |
| `disabled` | `boolean` | Root `disabled`. |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | **Panel** transition status: the Root's, except that it is forced to `'idle'` during motion-skipping opens (COL-STA-06). |

Source: `collapsible/panel/CollapsiblePanel.tsx:92-95,135-140`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` | `open === true` | COL-DOM-11 | C# |
| `data-closed` | `open === false` | COL-DOM-11 | C# |
| `data-disabled` | `disabled === true` (not in outline, §16 Q2) | COL-DOM-19 | C# |
| `data-starting-style` | Panel `transitionStatus === 'starting'`; additionally persisted on a closed `hiddenUntilFound` panel (COL-DOM-15) | COL-DOM-12, COL-DOM-15 | JS |
| `data-ending-style` | Panel `transitionStatus === 'ending'` | COL-DOM-12 | JS |
| `hidden` | closed and not in a closing transition; boolean, or `"until-found"` with `hiddenUntilFound` | COL-DOM-13, COL-DOM-14 | C# (boolean); JS (`"until-found"`) |

#### CSS variables

| Variable | Set on | Value | Req | Writer |
|---|---|---|---|---|
| `--collapsible-panel-height` | Panel element inline style | `auto`, or `<scrollHeight>px` while a measured size is held (§9.2) | COL-DOM-17 | JS |
| `--collapsible-panel-width` | Panel element inline style | `auto`, or `<scrollWidth>px` while a measured size is held (§9.2) | COL-DOM-17 | JS |

### 3.4 Shared types

- `Collapsible.Root.ChangeEventReason` = `'trigger-press' | 'none'`. Source: `collapsible/root/CollapsibleRoot.tsx:106`; `internals/reason-parts.ts:1,3`.
- `Collapsible.Root.ChangeEventDetails` = the common change-event-details shape from [event-details-and-reasons](../architecture/event-details-and-reasons.md) specialized to those reasons: `{ reason, event, cancel(), allowPropagation(), isCanceled, isPropagationAllowed, trigger }`, where `event` is a `MouseEvent | PointerEvent | TouchEvent | KeyboardEvent` for `'trigger-press'` and a plain `Event` (the `beforematch` event) for `'none'`. Source: `collapsible/root/CollapsibleRoot.tsx:107-108`; `internals/createBaseUIEventDetails.ts:5,7,56-93`.
- `Collapsible.Root.Props/State`, `Collapsible.Trigger.Props/State`, `Collapsible.Panel.Props/State` as above. Flat aliases (`CollapsibleRootProps`, `CollapsiblePanelState`, …) and the attribute-name namespaces `CollapsibleRootDataAttributes`, `CollapsibleTriggerDataAttributes`, `CollapsiblePanelDataAttributes`, `CollapsiblePanelCssVariables` are also exported. Source: `collapsible/index.ts:1-10`. Test: [L1] `CollapsibleRoot.spec.tsx` (type assertions only).
- `TransitionStatus` = `'starting' | 'ending' | 'idle' | undefined`. Source: `internals/useTransitionStatus.ts:6`.

## 4. State model

### 4.1 Open state

- **COL-STA-01** — MUST. The open state is consumer-owned if `open !== undefined` on the first render, otherwise component-owned starting from `defaultOpen`. The mode is fixed for the lifetime of the instance (switching logs COL-EDGE-05).
  Source: `packages/utils/src/useControlled.ts:41-46`. Test: [L2] `CollapsibleRoot.test.tsx` "controlled mode", "uncontrolled mode".
- **COL-STA-02** — MUST. In component-owned mode, a trigger press (§6) sets the open state to its negation unless `onOpenChange` canceled the change (COL-EVT-03).
  Source: `collapsible/root/useCollapsibleRoot.ts:31-42`. Test: [L2] `CollapsibleRoot.test.tsx` "uncontrolled mode", "eventDetails.cancel() prevents opening while uncontrolled".
- **COL-STA-03** — MUST. In consumer-owned mode, a trigger press calls `onOpenChange(!open, details)` and changes nothing; the state changes only when the consumer renders a new `open` value. A consumer-owned `open` that later becomes `undefined` falls back to the initial `defaultOpen` value (and logs COL-EDGE-05).
  Source: `collapsible/root/useCollapsibleRoot.ts:31-42`; `packages/utils/src/useControlled.ts:46,83-90`. Test: [L2] `CollapsibleRoot.test.tsx` "controlled trigger presses request open and close state changes", "does not change controlled open state without an external update".

### 4.2 Mounted flag and transition status

Two further pieces of state are derived from `open` by the shared transition-status mechanism ([transitions-and-mounting](../architecture/transitions-and-mounting.md)), which the Root configures with **idle state enabled** and **ending state deferred by one frame** (`useTransitionStatus(open, true, true)`), and with no entrance transition for an initially-open instance:

- **mounted** — whether the Panel is considered present for transition purposes. Distinct from "in the document": with `keepMounted`/`hiddenUntilFound` the element can be in the document while `mounted` is `false`.
- **transitionStatus** — `undefined` | `'starting'` | `'idle'` | `'ending'`.

- **COL-STA-04** — MUST. `mounted` is initially equal to `open`. It becomes `true` in the same update cycle in which `open` becomes `true`. It becomes `false` only when a close completes: immediately (before the next paint) when the Panel has no detectable motion (COL-ANI-09), or after the closing animations finish (COL-ANI-11), or immediately when the panel element is absent during the closing phase (COL-ANI-18). Server: relaxed, see §9.
  Source: `internals/useTransitionStatus.ts:29,31-34`; `collapsible/panel/useCollapsiblePanel.ts:235-238,252-255,260-263,313-335`. Test: [L4] `CollapsiblePanel.test.tsx` "hides the panel after external controlled close when true and no animations are applied".
- **COL-STA-05** — MUST. Root `transitionStatus` follows this cycle: initially `'idle'` if `defaultOpen`/`open` is `true`, else `undefined`. On open (from not-mounted): `'starting'` in the same update cycle in which the Panel appears, then `'idle'` on the next animation frame. On open while still `'ending'` (reopen during close): `'starting'` before the next paint, then `'idle'` on the next frame. On close: unchanged (`'idle'`) in the closing update cycle, then `'ending'` on the next animation frame, then `undefined` once `mounted` becomes `false`. If the close completes before the deferred frame fires (no-motion close), the status remains `'idle'` while closed; this is unobservable because `'idle'` renders no attribute.
  Source: `internals/useTransitionStatus.ts:23-25,31-42,44-56,75-91`; `collapsible/root/useCollapsibleRoot.ts:24`. Test: [L4] `CollapsiblePanel.test.tsx` "applies data-starting-style while opening", "restores a measured height before applying closing transition styles", "keeps exit transitions working after a close is interrupted by reopening".
- **COL-STA-06** — MUST. The Panel's state `transitionStatus` equals the Root's, except that it is forced to `'idle'` from before the first paint of an open that skips motion — an open whose detected animation type is `'none'` (COL-ANI-06) or an open triggered by `beforematch` (COL-ANI-22) — until the Root's status next leaves `'starting'`. During that window the Root and Trigger carry `data-starting-style` while the Panel does not.
  Source: `collapsible/panel/useCollapsiblePanel.ts:66,75,131-140,196,210,226`. Test: [L4] `CollapsiblePanel.test.tsx` "hides the panel after external controlled close when true and no animations are applied" (no `data-ending-style` ever appears on a no-motion panel), "keeps the temporary zero animation duration until the panel closes".

Text state diagram (Root status / mounted), for a Panel with CSS motion:

```
CLOSED        open=false mounted=false status=undefined   (panel absent unless keepMounted/hiddenUntilFound; then hidden)
  | press / open=true
OPENING       open=true  mounted=true  status='starting'  (same update cycle; data-open + data-starting-style; size measured before paint)
  | next frame
OPEN          open=true  mounted=true  status='idle'       (data-starting-style removed; after motion finishes, size vars -> auto)
  | press / open=false
CLOSE-PENDING open=false mounted=true  status='idle'       (same cycle; data-closed; size vars -> px before paint)
  | next frame
CLOSING       open=false mounted=true  status='ending'     (data-ending-style; wait 1 frame, then wait for animations)
  | animations finished            | press / open=true
CLOSED                              OPENING (status 'starting' before paint)
```

With animation type `'none'`, CLOSE-PENDING goes directly to CLOSED before the next paint, and OPENING's Panel status is forced to `'idle'` before the first paint.

### 4.3 Derived state

- **COL-STA-07** — MUST. The Root, Trigger and Panel state objects are `{ open, disabled, transitionStatus }`. Root and Trigger share one object; the Panel's object substitutes the Panel transition status (COL-STA-06). These objects are what `className`/`style`/`render` functions receive. Server: relaxed, see §9.
  Source: `collapsible/root/CollapsibleRoot.tsx:43-50`; `collapsible/trigger/CollapsibleTrigger.tsx:31,50`; `collapsible/panel/CollapsiblePanel.tsx:92-95`. Test: [L2] `CollapsibleRoot.test.tsx` "passes state to className and style callbacks".
- **COL-STA-08** — MUST. `hidden` (the Panel's hidden condition) is `!open && !mounted`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:74`. Test: [L1] `CollapsiblePanel.test.tsx` "does not unmount the panel when true".
- **COL-STA-09** — MUST. The Root resolves a *panel id* as follows: the Panel's registered id (its non-empty `id` prop) if one is registered; otherwise the Root's generated default id (`base-ui-` + a framework-unique token, see §14); otherwise **no id** when a Panel has registered and then unregistered (unmounted). Registration happens when the Panel part mounts or its `id` prop changes; unregistration happens when the Panel part unmounts. Re-mounting the Panel restores a registration.
  Source: `collapsible/root/useCollapsibleRoot.ts:26-29`; `collapsible/panel/CollapsiblePanel.tsx:61-69`; `internals/useBaseUiId.ts:9-11`. Test: [L2] `CollapsibleRoot.test.tsx` "unregisters and restores the generated panel id when the panel remounts".

Invariants: exactly one of `data-open`/`data-closed` is present on Root and Panel at all times; `mounted` implies the Panel part is rendering its element (unless a consumer render function withholds it); `aria-controls` is present only while `open` and a panel id resolves.

## 5. Rendered DOM and ARIA

### 5.1 Root

- **COL-DOM-01** — MUST. The Root element has `data-open=""` when open and `data-closed=""` when closed (never both, never neither).
  Source: `utils/collapsibleOpenStateMapping.ts:26-35`; `collapsible/root/stateAttributesMapping.ts:6-9`. Test: [L2] `CollapsibleRoot.test.tsx` "passes state to className and style callbacks" (class derived from `state.open`); `CollapsiblePanel.test.tsx` "does not unmount the panel when true" (Panel equivalents).
- **COL-DOM-02** — MUST. The Root element has `data-disabled=""` when `disabled` is `true` and no `data-disabled` attribute otherwise.
  Source: `internals/getStateAttributesProps.ts:24-28` (default mapping; `disabled` has no custom mapping). Test: none for Root (Trigger: "disabled status").
- **COL-DOM-03** — MUST. The Root element has `data-starting-style=""` while Root `transitionStatus` is `'starting'` and `data-ending-style=""` while it is `'ending'`; neither for `'idle'`/`undefined`.
  Source: `internals/stateAttributesMapping.ts:10-20`; `collapsible/root/stateAttributesMapping.ts:6-9`. Test: none directly (Panel equivalents tested).

### 5.2 Trigger

- **COL-DOM-04** — MUST. The Trigger element has `data-panel-open=""` while open and no `data-panel-open` attribute while closed. The Trigger never carries `data-open`/`data-closed`.
  Source: `utils/collapsibleOpenStateMapping.ts:13-24`; `collapsible/trigger/CollapsibleTrigger.tsx:12-15`. Test: [L2] `CollapsibleRoot.test.tsx` "uncontrolled mode", "controlled mode".
- **COL-DOM-05** — MUST. The Trigger element has `data-disabled=""` when its effective `disabled` is `true`.
  Source: `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `CollapsibleRoot.test.tsx` "disabled status".
- **COL-DOM-06** — MUST. The Trigger element has `data-starting-style=""`/`data-ending-style=""` under the same conditions as the Root (COL-DOM-03), and loses `data-ending-style` once the close completes.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:12-15`. Test: [L4] `CollapsiblePanel.test.tsx` "finishes closing when the render function removes the panel element" (asserts the Trigger has no `data-ending-style` after close).
- **COL-DOM-07** — MUST. With `nativeButton` `true` the Trigger has `type="button"` (overridable by a consumer `type`); with `nativeButton` `false` it has `role="button"`.
  Source: `internals/use-button/useButton.ts:226,228`. Test: [L2] `CollapsibleTrigger.test.tsx` "removes a disabled non-native trigger from the tab order" (`getByRole('button')` on a `<span>`).
- **COL-DOM-08** — MUST. Disabled attribute rendering, as configured through the shared button behavior with `focusableWhenDisabled` unset: native + disabled → `disabled` attribute present and **no** `aria-disabled`; non-native + disabled → `aria-disabled="true"`, `tabindex="-1"` and **no** `disabled` attribute; enabled (either mode) → neither `disabled` nor `aria-disabled`.
  Source: `utils/useFocusableWhenDisabled.ts:30-47`; `internals/use-button/useButton.ts:28-34,227`. Test: [L2] `CollapsibleTrigger.test.tsx` "renders the disabled attribute when disabled", "removes a disabled non-native trigger from the tab order".
- **COL-DOM-09** — MUST. An enabled Trigger renders `tabindex="0"` in both native and non-native modes (overridable by a consumer `tabIndex`).
  Source: `utils/useFocusableWhenDisabled.ts:30-36`. Test: [L2] `CollapsibleRoot.test.tsx` "key: $key should toggle the Collapsible" (Tab focuses the trigger).

### 5.3 Panel

- **COL-DOM-10** — MUST. The Panel element always has an `id`: the Panel's non-empty `id` prop, else the Root's generated default id (COL-STA-09).
  Source: `collapsible/panel/CollapsiblePanel.tsx:61-62`; `collapsible/panel/useCollapsiblePanel.ts:470`. Test: [L2] `CollapsibleRoot.test.tsx` "sets ARIA attributes", "references manual panel id in trigger aria-controls".
- **COL-DOM-11** — MUST. The Panel element has `data-open=""` when open and `data-closed=""` when closed, including while it is in the document hidden (`keepMounted`) and while a closing transition runs.
  Source: `collapsible/root/stateAttributesMapping.ts:6-9`; `collapsible/panel/CollapsiblePanel.tsx:124`. Test: [L2] `CollapsiblePanel.test.tsx` "does not unmount the panel when true".
- **COL-DOM-12** — MUST. The Panel element has `data-starting-style=""` while the Panel transition status is `'starting'` and `data-ending-style=""` while it is `'ending'`.
  Source: `internals/stateAttributesMapping.ts:10-20`; `collapsible/panel/CollapsiblePanel.tsx:106,124`. Test: [L4] `CollapsiblePanel.test.tsx` "applies data-starting-style while opening", "restores a measured height before applying closing transition styles".
- **COL-DOM-13** — MUST. The Panel element has the boolean `hidden` attribute exactly when `!open && !mounted` (COL-STA-08) and `hiddenUntilFound` is `false`. A consumer-supplied `hidden` prop (including an explicit `undefined`) replaces the component's value, because consumer attributes are merged after the component's own.
  Source: `collapsible/panel/useCollapsiblePanel.ts:74,469`; `collapsible/panel/CollapsiblePanel.tsx:108-118`; `merge-props/mergeProps.ts:177-183`. Test: [L2] `CollapsiblePanel.test.tsx` "hides the panel after external controlled close when true and no animations are applied".
- **COL-DOM-14** — MUST. The Panel passes its `hiddenUntilFound` value and its hidden predicate (COL-STA-08) to the shared hidden-until-found attribute write; the observable result is per TRN-DOM-08 (and TRN-EDGE-08): with `hiddenUntilFound` `true`, whenever the Panel is hidden the attribute is the literal `hidden="until-found"`; while not hidden there is no `hidden` attribute.
  Source: `collapsible/panel/useCollapsiblePanel.ts:355-366`; see TRN-DOM-08, TRN-EDGE-08. Test: [L4] `CollapsiblePanel.test.tsx` "warns when hiddenUntilFound overrides keepMounted={false}", "re-collapses a keyframe panel when the open state never arrives".
- **COL-DOM-15** — MUST. The Panel supplies its last detected animation type (§9.1) as the motion type of the shared persisted-starting-style rule; the observable result is per TRN-DOM-09: with `hiddenUntilFound` `true`, while the Panel is hidden and that type is not `'css-animation'` (`'css-transition'`, `'none'`, or not yet detected) the element carries `data-starting-style=""`; a keyframe-animated hidden panel does not.
  Source: `collapsible/panel/useCollapsiblePanel.ts:93-94,466-468`; see TRN-DOM-09. Test: [L4] `CollapsiblePanel.test.tsx` "expands the panel while beforematch is being dispatched" (present before reveal), "re-collapses a keyframe panel when the open state never arrives" (absent for keyframes).
- **COL-DOM-16** — MUST. The Panel element is rendered only when `keepMounted || hiddenUntilFound || mounted || open`; otherwise the part renders nothing (while still keeping its id registration, COL-STA-09).
  Source: `collapsible/panel/useCollapsiblePanel.ts:461`; `collapsible/panel/CollapsiblePanel.tsx:128-130`. Test: [L2] `CollapsibleRoot.test.tsx` "uncontrolled mode" (content absent while closed).
- **COL-DOM-17** — MUST. The Panel element's inline style always defines `--collapsible-panel-height` and `--collapsible-panel-width`. Each is `auto` when no measured size is held and `<n>px` (integer `scrollHeight`/`scrollWidth`) while one is held. When they are measured, set and cleared is specified in §9.2.
  Source: `collapsible/panel/CollapsiblePanel.tsx:110-117`; `collapsible/panel/useCollapsiblePanel.ts:480-485`. Test: [L4] `CollapsiblePanel.test.tsx` "restores a measured height before applying closing transition styles".
- **COL-DOM-18** — MUST. Panel inline-style precedence, lowest to highest: (1) the CSS variables of COL-DOM-17; (2) the consumer's `style` (object, or function result against Panel state); (3) `animation-name: none` while open-animation suppression is active (COL-ANI-13, COL-ANI-20). Temporary inline overrides written directly to the element during measurement (§9.2) sit outside this merge and are restored as specified there.
  Source: `collapsible/panel/CollapsiblePanel.tsx:97,108-123`; `merge-props/mergeProps.ts:166-171`. Test: [L4] `CollapsiblePanel.test.tsx` "suppresses the initial keyframe animation from inline styles when rendered open" (`animation-name: none` wins over a consumer inline `animationName`; consumer `animationDuration` preserved).
- **COL-DOM-19** — MUST. The Panel element has `data-disabled=""` when the Root is disabled.
  Source: `internals/getStateAttributesProps.ts:24-28`; `collapsible/panel/CollapsiblePanel.tsx:92-95`. Test: none.

### 5.4 ARIA

- **COL-ARIA-01** — MUST. The Trigger always has `aria-expanded`, `"true"` while open and `"false"` while closed.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:55`. Test: [L2] `CollapsibleRoot.test.tsx` "sets ARIA attributes", "controlled mode".
- **COL-ARIA-02** — MUST. The Trigger has `aria-controls="<panel id>"` while open (and a panel id resolves per COL-STA-09) and no `aria-controls` attribute while closed.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:54`. Test: [L2] `CollapsibleRoot.test.tsx` "sets ARIA attributes", "uncontrolled mode" (absent when closed).
- **COL-ARIA-03** — MUST. `aria-controls` references the consumer-supplied Panel `id` when one is given, and the generated id otherwise; the value always equals the Panel element's `id`.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:54`; `collapsible/root/useCollapsibleRoot.ts:29`. Test: [L2] `CollapsibleRoot.test.tsx` "references manual panel id in trigger aria-controls".
- **COL-ARIA-04** — MUST. When the Panel part is removed from the tree (unregistered) while open, the Trigger drops `aria-controls`; when the Panel part is rendered again, `aria-controls` returns with the Panel's current id.
  Source: `collapsible/panel/CollapsiblePanel.tsx:64-69`; `collapsible/root/useCollapsibleRoot.ts:28-29`. Test: [L2] `CollapsibleRoot.test.tsx` "unregisters and restores the generated panel id when the panel remounts".
- **COL-ARIA-05** — MUST. The Panel renders no `role` and no `aria-labelledby`; the Root renders no ARIA attributes. (Accordion adds `role="region"`/`aria-labelledby` on its own Panel.)
  Source: `collapsible/panel/useCollapsiblePanel.ts:465-471`; `collapsible/root/CollapsibleRoot.tsx:61-66`. Test: none.

Consumer attributes with the same names (`aria-expanded`, `aria-controls`, `type`, `tabIndex`, `hidden`, …) take precedence over the component's because consumer props are merged later (RND); `id` on the Panel is handled by COL-API-10 rather than merged.

## 6. Interactions

### 6.1 Keyboard

All keyboard handling on the Trigger comes from the shared button behavior ([button-behavior](../architecture/button-behavior.md)) configured with `{ disabled, native: nativeButton }` and no `composite`/`focusableWhenDisabled`; Collapsible attaches no key handlers of its own. The table records the observable result.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | Trigger focused, enabled, native | Browser-native activation fires `click` → toggle (COL-PTR-01). | COL-KEY-01 |
| <kbd>Enter</kbd> | Trigger focused, enabled, non-native | On keydown: default prevented, a synthetic `click` is dispatched on the element → toggle. | COL-KEY-01 |
| <kbd>Space</kbd> | Trigger focused, enabled, native | Browser-native activation fires `click` on keyup → toggle. | COL-KEY-02 |
| <kbd>Space</kbd> | Trigger focused, enabled, non-native | keydown: default prevented (no page scroll); keyup: a synthetic `click` is dispatched → toggle. | COL-KEY-02 |
| <kbd>Enter</kbd>/<kbd>Space</kbd> | Trigger disabled | No toggle, `onOpenChange` not called. Native disabled triggers cannot be focused at all; non-native ones are removed from the tab order. | COL-KEY-03 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Not intercepted; native focus movement. | COL-KEY-04 |
| any other key | any | Not handled. | COL-KEY-04 |

Modifier combinations with Enter/Space are passed through to the dispatched click (non-native) by the button behavior; Collapsible does not inspect modifiers. There is no orientation or RTL variant.

- **COL-KEY-01** — MUST. Pressing <kbd>Enter</kbd> on a focused, enabled Trigger toggles the open state and calls `onOpenChange` with reason `'trigger-press'` and a `KeyboardEvent`-originated click (native: the browser's click; non-native: a click dispatched on keydown).
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:56`; `internals/use-button/useButton.ts:116-176`. Test: [L4] `CollapsibleRoot.test.tsx` "key: Enter should toggle the Collapsible".
- **COL-KEY-02** — MUST. Pressing <kbd>Space</kbd> on a focused, enabled Trigger toggles the open state (native: browser click on keyup; non-native: keydown default prevented, click dispatched on keyup).
  Source: `internals/use-button/useButton.ts:116-176,177-217`. Test: [L4] `CollapsibleRoot.test.tsx` "key: Space should toggle the Collapsible".
- **COL-KEY-03** — MUST. While the Trigger is disabled, <kbd>Enter</kbd>/<kbd>Space</kbd> MUST NOT toggle nor call `onOpenChange`; a native disabled Trigger is not reachable with <kbd>Tab</kbd>, and a non-native disabled Trigger has `tabindex="-1"` and is likewise skipped.
  Source: `internals/use-button/useButton.ts:104-110,116-119`; `utils/useFocusableWhenDisabled.ts:33-35,45-47`. Test: [L4] `CollapsibleRoot.test.tsx` "key: $key does not toggle or call onOpenChange when disabled"; `CollapsibleTrigger.test.tsx` "removes a disabled non-native trigger from the tab order".
- **COL-KEY-04** — MUST. No key other than <kbd>Enter</kbd>/<kbd>Space</kbd> is handled; <kbd>Tab</kbd> is never intercepted.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:52-60` (only `onClick` attached). Test: [L4] `CollapsibleRoot.test.tsx` "key: $key should toggle the Collapsible" (Tab moves focus to the trigger).

### 6.2 Pointer and touch

- **COL-PTR-01** — MUST. A `click` on an enabled Trigger (any pointer type, any button that produces `click`) toggles the open state: `onOpenChange(!open, { reason: 'trigger-press', event: <the native click event> })` is called first, then the state changes unless canceled (COL-STA-02/03). There is no press-and-hold, hover, long-press, drag or right-click behavior.
  Source: `collapsible/root/useCollapsibleRoot.ts:31-42`; `collapsible/trigger/CollapsibleTrigger.tsx:56`. Test: [L2] `CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails", "uncontrolled mode".
- **COL-PTR-02** — MUST. A click on a disabled Trigger MUST NOT toggle nor call `onOpenChange`; in non-native mode the click's default action is prevented, and `pointerdown`'s default is prevented as well.
  Source: `internals/use-button/useButton.ts:104-110,218-224`. Test: [L2] `CollapsibleRoot.test.tsx` "does not toggle or call onOpenChange when clicked while disabled" (L4 for the non-native `preventDefault` clause).
- **COL-PTR-03** — MUST. A consumer `onClick` on the Trigger runs **before** the toggle; if it calls `event.preventBaseUIHandler()` the toggle (and `onOpenChange`) is skipped. The same chaining applies to consumer `onKeyDown`/`onKeyUp`/`onMouseDown`/`onPointerDown` relative to the button behavior's handlers.
  Source: `collapsible/trigger/CollapsibleTrigger.tsx:52-60`; `merge-props/mergeProps.ts:17-19,210-239`; `internals/use-button/useButton.ts:91-110`. Test: none.

### 6.3 Focus management

- **COL-FOC-01** — MUST. Opening or closing MUST NOT move focus. The Trigger is a normal tab stop (COL-DOM-09) and the Panel is not focusable by default.
  Source: `collapsible/root/useCollapsibleRoot.ts:31-42` (no focus calls anywhere in `collapsible/`). Test: none.

### 6.4 Dismissal

Not applicable — the Panel closes only through the Trigger or a consumer-driven `open` change; there is no outside-press, Escape, scroll or blur dismissal.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onOpenChange` | `(open: boolean, details: Collapsible.Root.ChangeEventDetails) => void` | Trigger press (click, incl. keyboard-originated click); `beforematch` on a `hiddenUntilFound` Panel | `'trigger-press'`, `'none'` | yes |

There is **no** `onOpenChangeComplete`, `actionsRef` or `preventUnmountOnClose()` on Collapsible in this version (see §16 Q1); animation completion is consumed internally only (§9).

- **COL-EVT-01** — MUST. On a Trigger press, `onOpenChange` is called exactly once with `open = !currentOpen` and details `{ reason: 'trigger-press', event: <native click event> }`, before any state or DOM change. It fires regardless of ownership mode and even if the consumer will not apply the change.
  Source: `collapsible/root/useCollapsibleRoot.ts:31-35`; `internals/createBaseUIEventDetails.ts:126-157`. Test: [L2] `CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails", "does not change controlled open state without an external update".
- **COL-EVT-02** — MUST. `details.event` for `'trigger-press'` is the native event underlying the click: a `MouseEvent` (or `PointerEvent`) for pointer clicks, and for keyboard activation the native event of the resulting click (`MouseEvent`/`PointerEvent` for native buttons; for non-native buttons the dispatched click carries the originating `KeyboardEvent`'s modifiers per UBT).
  Source: `collapsible/root/useCollapsibleRoot.ts:33`; `internals/createBaseUIEventDetails.ts:7`. Test: [L2] `CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails" (`instanceof MouseEvent`) (L4 for the keyboard-activation clause).
- **COL-EVT-03** — MUST. Calling `details.cancel()` inside `onOpenChange` for a `'trigger-press'` prevents the component-owned state from changing (both opening and closing). In consumer-owned mode cancel has no additional effect (the state was never going to change by itself). `details.isCanceled` is `false` on entry.
  Source: `collapsible/root/useCollapsibleRoot.ts:37-41`. Test: [L2] `CollapsibleRoot.test.tsx` "eventDetails.cancel() prevents opening while uncontrolled", "eventDetails.cancel() prevents closing while uncontrolled".
- **COL-EVT-04** — MUST. The Panel attaches the shared reveal-event handler (TRN-ALG-07) to its own element, routing it to the Root's `onOpenChange` and open-state setter; the observable result is per TRN-ANI-32: on `beforematch`, `onOpenChange(true, { reason: 'none', event: <the beforematch Event> })` is called synchronously inside the dispatch, before `data-starting-style` is removed and before the open state is requested. Collapsible-specific: the listener is attached whenever the Panel element exists, so the call happens even if the Panel is already open (in practice only while closed, since browsers dispatch `beforematch` only on hidden content; §16 Q9).
  Source: `collapsible/panel/useCollapsiblePanel.ts:368-375,421-424,450`; see TRN-ANI-32, TRN-ALG-07. Test: [L4] `CollapsiblePanel.test.tsx` "uses `hidden=\"until-found\" to hide panel when true".
- **COL-EVT-05** — MUST. Calling `details.cancel()` for a `'none'` (beforematch) change prevents the open request; the observable result is per TRN-ANI-33: the state is not changed, `data-starting-style` is not removed, no motion-skip is armed, and the `hidden="until-found"` attribute the browser removed is restored (COL-ANI-25).
  Source: `collapsible/panel/useCollapsiblePanel.ts:426-431`; see TRN-ANI-33. Test: [L4] `CollapsiblePanel.test.tsx` "does not open or suppress the next trigger open when beforematch is canceled".
- **COL-EVT-06** — MUST. `onOpenChange` MUST NOT be called when the consumer changes the `open` prop directly, nor when the component itself finishes a transition.
  Source: `collapsible/root/useCollapsibleRoot.ts:31-42` (the only two call sites are the trigger handler and the beforematch handler at `useCollapsiblePanel.ts:424`). Test: [L2] `CollapsibleRoot.test.tsx` "controlled mode" (external toggle, no handler), `CollapsiblePanel.test.tsx` "finishes closing when the render function removes the panel element" (called once with `false`).
- **COL-EVT-07** — MUST. `details.trigger` is always `undefined` for Collapsible (no trigger element is recorded), and `details.allowPropagation()` has no effect because Collapsible never stops event propagation.
  Source: `collapsible/root/useCollapsibleRoot.ts:33`; `collapsible/panel/useCollapsiblePanel.ts:422` (no third argument); `internals/createBaseUIEventDetails.ts:153`. Test: [L2] `CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails" (`allowPropagation` is a function).
- **COL-EVT-08** — MUST. The latest `onOpenChange` function supplied by the consumer is the one invoked, even if the consumer replaced it between renders. (The wrapper's development-only guard, which throws `Base UI: Cannot call an event handler while rendering.` when the wrapped handler is called during rendering, is not reachable from consumer code because the wrapped handler is never exposed; it is not a requirement.)
  Source: `collapsible/root/CollapsibleRoot.tsx:34`; `packages/utils/src/useStableCallback.ts:35-62`. Test: none.

## 8. Form integration

Not applicable — Collapsible has no `name`/`value`, renders no hidden input, and does not participate in Field/Form validation.

## 9. Lifecycle, mounting and animation

The Panel's motion handling is the heart of this component. The shared mechanisms are specified in [transitions-and-mounting](../architecture/transitions-and-mounting.md); this section states how the Panel configures and extends them. Terms used below:

- **measured size** — `{ height: element.scrollHeight, width: element.scrollWidth }` read from the Panel element.
- **held size** — the size currently rendered into the CSS variables (COL-DOM-17); "cleared" means both become `auto`.
- **cached size** — the last measured size, remembered separately from the held size; used only by keyframe (`'css-animation'`) panels (COL-ANI-12/13).
- **animation type** — `'css-transition'`, `'css-animation'` or `'none'`, detected per COL-ANI-01 and remembered until the next detection.
- **before the next paint** — performed in a layout phase after the DOM update and before the browser paints that update.
- **one frame later** — on the next `requestAnimationFrame` callback.

### 9.1 Animation type detection

- **COL-ANI-01** — MUST. Each time `open`, `mounted` or the Root transition status changes while the Panel element exists, the Panel reads the element's computed style and classifies it, before the next paint: let *hasTransition* = any comma-separated entry of `transition-duration` parses to a number > 0; let *hasAnimation* = (any entry of `animation-name` is neither empty nor `none`, **or** open-animation suppression is active (COL-ANI-13/20)) **and** any entry of `animation-duration` parses to > 0. Type is `'css-transition'` if *hasTransition* (regardless of *hasAnimation*), else `'css-animation'` if *hasAnimation*, else `'none'`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:170-171,487-529`. Test: [L4] `CollapsiblePanel.test.tsx` "hides the panel after external controlled close when true and no animations are applied" (`'none'`), "applies data-starting-style while opening" (`'css-transition'`), "still animates on close and reopen after being initially open" (`'css-animation'`).
- **COL-ANI-02** — MUST. If both *hasTransition* and *hasAnimation* are true, the development warning in COL-EDGE-03 is emitted and the type is `'css-transition'`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:501-511`. Test: [L4] `CollapsiblePanel.test.tsx` "preserves inline alignment styles while measuring an opening panel".
- **COL-ANI-03** — MUST. Before detecting the type for a close (`open` is `false`), any temporary `transition-duration`/`animation-duration` override still pending from a `beforematch` open (COL-ANI-22) is restored first, so the close is classified from the authored durations.
  Source: `collapsible/panel/useCollapsiblePanel.ts:162-168`. Test: [L4] `CollapsiblePanel.test.tsx` "restores the transition duration before the first close after a beforematch open", "keeps the temporary zero animation duration until the panel closes".

### 9.2 Opening

- **COL-ANI-04** — MUST. When the Panel becomes open from not-mounted, the element appears (or stops being hidden) in the same update cycle with `data-open` and `data-starting-style` (Root status `'starting'`, COL-STA-05) and the held size cleared (`auto`), then, before the next paint, the size is measured and held (CSS variables become `<n>px`). One frame later `data-starting-style` is removed (status `'idle'`). Consumers therefore style `[data-starting-style] { height: 0 }` and `height: var(--collapsible-panel-height)` to get a 0 → measured transition.
  Source: `collapsible/panel/useCollapsiblePanel.ts:188-229`; `internals/useTransitionStatus.ts:31-34,75-91`. Test: [L4] `CollapsiblePanel.test.tsx` "applies data-starting-style while opening"; Docs: `components/collapsible/demos/hero/css-modules/index.module.css:87-105`.
- **COL-ANI-05** — MUST. For a `'css-transition'` open, during the measurement the element's inline `justify-content`, `align-items`, `align-content` and `justify-items` are set to `initial !important` (so flex alignment cannot distort `scrollHeight`/`scrollWidth`), and the previous inline values (or absence) are restored one frame later, or immediately if the open is interrupted before that frame.
  Source: `collapsible/panel/useCollapsiblePanel.ts:200-205,562-591`. Test: [L4] `CollapsiblePanel.test.tsx` "preserves inline alignment styles while measuring an opening panel".
- **COL-ANI-06** — MUST. For a `'css-animation'` open, the size is measured and held before the next paint with the authored animation left in place; the source then sets inline `animation-name: none` and restores it synchronously with no style flush in between, which has no observable effect (same pattern as §16 Q6; a port need not reproduce it). For a `'none'` open, the size is measured and held and the Panel transition status is forced to `'idle'` before the first paint (COL-STA-06).
  Source: `collapsible/panel/useCollapsiblePanel.ts:194-198,214-219,539-553`. Test: [L4] `CollapsiblePanel.test.tsx` "still animates on reopen after being initially open when only open keyframes are defined"; "hides the panel after external controlled close when true and no animations are applied".
- **COL-ANI-07** — MUST. Once the Panel is open, mounted and its transition status is `'idle'`, the component waits for `data-starting-style` to be absent, then one frame, then for every finite CSS animation/transition on the element to finish; it then clears the held size (variables back to `auto`) so content can resize naturally. If the Panel was closed again before completion, the held size MUST NOT be cleared (it is needed as the close's start value).
  Source: `collapsible/panel/useCollapsiblePanel.ts:284-300`; `internals/useOpenChangeComplete.tsx:9-28`; `internals/useAnimationsFinished.ts:93-162`. Test: [L4] `CollapsiblePanel.test.tsx` "does not replay open transitions when revealing a panel opened by the user" (height becomes `auto` after the opening transition), "keeps the measured size when an open animation finishes during a close commit".
- **COL-ANI-08** — MUST. The Panel MUST NOT re-measure on content or viewport resize. While the held size is `auto` the element follows its content; while a measured size is held (during an open or close motion) the value is frozen until the motion completes.
  Source: `collapsible/panel/useCollapsiblePanel.ts:149-282` (measurement sites are only the open/close passes); CHANGELOG v1.0.0-beta.1 "Allow content to resize naturally (#2043)". Test: none.

### 9.3 Closing

- **COL-ANI-09** — MUST. When `open` becomes `false` while mounted and the status is `'idle'` or `'starting'`: the element renders `data-closed` (and keeps any `data-starting-style` until the next frame); then, before the next paint, the animation type is detected and (a) if `'none'`: the held size is cleared, `mounted` becomes `false` (so `hidden` applies / the element is removed, COL-DOM-13/16) and no `data-ending-style` is ever rendered; (b) otherwise the size is measured and held, so the closing motion starts from the expanded pixel size. Server: relaxed, see §9.
  Source: `collapsible/panel/useCollapsiblePanel.ts:231-243`. Test: [L4] `CollapsiblePanel.test.tsx` "hides the panel after external controlled close when true and no animations are applied", "restores a measured height before applying closing transition styles".
- **COL-ANI-10** — MUST. One frame after the closing update, the Root status becomes `'ending'` and the element renders `data-ending-style`. Before that paint the Panel re-runs detection and: if the type is `'none'`, `mounted` becomes `false` immediately; else it re-measures, and if **both** height and width measure 0 it sets `mounted` to `false` immediately without waiting for any animation; else it holds the new measurement and, for `'css-animation'`, sets inline `animation-name: none` and restores it synchronously (see §16 Q6). Server: relaxed, see §9.
  Source: `collapsible/panel/useCollapsiblePanel.ts:245-272`; `internals/useTransitionStatus.ts:44-56`. Test: [L4] `CollapsiblePanel.test.tsx` "unmounts a zero-size panel without waiting for unrelated transitions", "unmounts a panel that mounts after the close has already entered the ending phase".
- **COL-ANI-11** — MUST. While closed, mounted and the Panel status is `'ending'`: the component waits one frame (so a transition registered late by the browser is seen), then waits for every finite animation/transition on the element to finish (re-waiting if animations are replaced), then — if the Panel has not been reopened meanwhile — sets `mounted` to `false` and clears the held size, in a synchronous flush so no intermediate frame is painted. If the Panel was reopened, nothing happens (the open path takes over). The wait is abandoned if `open`/`mounted`/status change. Server: relaxed, see §9.
  Source: `collapsible/panel/useCollapsiblePanel.ts:302-353`; `internals/useAnimationsFinished.ts:46-164`. Test: [L4] `CollapsiblePanel.test.tsx` "restores a measured height before applying closing transition styles", "does not restart the entrance transition when a close animation finishes after reopening", "keeps exit transitions working after a close is interrupted by reopening".
- **COL-ANI-12** — MUST. For a `'css-animation'` Panel, in the closing update itself (before the measurement of COL-ANI-09 lands), if the held size is `auto`, the CSS variables render the **cached** size instead, so a keyframe such as `from { height: var(--collapsible-panel-height) }` that starts on `[data-closed]` reads a pixel value. The cache is updated by every measurement (open or close) and is never cleared by the `auto` resets.
  Source: `collapsible/panel/useCollapsiblePanel.ts:82-92,99-107`. Test: [L4] `CollapsiblePanel.test.tsx` "restores measured dimensions before applying a closing keyframe animation".

### 9.4 Initially-open panels, re-attachment and server rendering

- **COL-ANI-13** — MUST. A Panel that is open on its very first render MUST NOT play an opening keyframe animation: while it remains open, its inline style includes `animation-name: none` (highest precedence, COL-DOM-18), its status starts at `'idle'` (no `data-starting-style`), and if its authored styles declare a keyframe animation with non-zero duration the expanded size is measured into the cache (not held) so the first close can start from pixels. The suppression ends the first time the Panel closes, after which opens animate normally. Transition-only panels are unaffected except that they never see `data-starting-style` on first render.
  Source: `collapsible/panel/useCollapsiblePanel.ts:59,76-81,152-155,176-184`; `collapsible/panel/CollapsiblePanel.tsx:122`; `internals/useTransitionStatus.ts:23-29`. Test: [L4] `CollapsiblePanel.test.tsx` "does not run the mount animation when initially open", "still animates on close and reopen after being initially open", "still animates on reopen after being initially open when only open keyframes are defined".
- **COL-ANI-14** — MUST. Server-rendered markup for an initially-open Panel contains the inline `animation-name: none` (other inline animation properties such as `animation-duration` are preserved), so hydration does not replay a keyframe open.
  Source: `collapsible/panel/useCollapsiblePanel.ts:59,76-81`; `collapsible/panel/CollapsiblePanel.tsx:122`. Test: [L2] `CollapsiblePanel.test.tsx` "suppresses the initial keyframe animation when rendered open", "suppresses the initial keyframe animation from inline styles when rendered open" (L4 for the hydration no-replay clause).
- **COL-ANI-15** — MUST. If an open Panel is removed by a consumer render function (render function returns nothing while `state.open` is `false`) and later re-rendered on reopen, the reopen animates normally: the one-shot suppressions of COL-ANI-13/20 are cleared on every close even when no element exists.
  Source: `collapsible/panel/useCollapsiblePanel.ts:150-155`. Test: [L4] `CollapsiblePanel.test.tsx` "animates on reopen after an initially open panel is removed by the render function".
- **COL-ANI-16** — MUST. Re-opening during `'ending'` (interrupted close) MUST resume as a normal open: `data-open` returns, status goes `'starting'` → `'idle'`, and a subsequent close MUST again produce `data-ending-style` on the same element. A close-completion that resolves after the reopen MUST NOT unmount the element or restart the entrance motion.
  Source: `collapsible/panel/useCollapsiblePanel.ts:325-335`; `internals/useTransitionStatus.ts:80-83`. Test: [L4] `CollapsiblePanel.test.tsx` "keeps exit transitions working after a close is interrupted by reopening", "does not restart the entrance transition when a close animation finishes after reopening".
- **COL-ANI-17** — MUST. An open-completion (COL-ANI-07) that resolves after the Panel has been closed MUST NOT clear the held size (the close transition must start from the measured pixel height, not `0`/`auto`).
  Source: `collapsible/panel/useCollapsiblePanel.ts:288-299`. Test: [L4] `CollapsiblePanel.test.tsx` "keeps the measured size when an open animation finishes during a close commit".
- **COL-ANI-18** — MUST. If, during the `'ending'` phase, no Panel element exists (a consumer render function withheld it), the close completes immediately: `mounted` becomes `false`, the held size is cleared, and Root/Trigger lose `data-ending-style`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:313-320`. Test: [L4] `CollapsiblePanel.test.tsx` "finishes closing when the render function removes the panel element", "unmounts a panel that mounts after the close has already entered the ending phase".
- **COL-ANI-19** — MUST. The Panel's transition status is exposed to a consumer render function (via state) during `'ending'`, so a render function may choose to render the element only while `open || transitionStatus === 'ending'`; such an element that first appears during `'ending'` is still closed correctly per COL-ANI-10.
  Source: `collapsible/panel/CollapsiblePanel.tsx:92-95,99-106`. Test: [L4] `CollapsiblePanel.test.tsx` "unmounts a panel that mounts after the close has already entered the ending phase".
- **COL-ANI-20** — MUST. If the Panel's JavaScript twin is disposed and its observers cancelled while the Panel is open, mounted and of type `'css-animation'` (e.g. the subtree is hidden by a keep-alive facility that preserves component state but detaches the twin) and the twin is later attached again, the re-attached Panel MUST NOT replay its opening keyframe animation: `animation-name: none` is applied inline on re-attachment until the Panel next closes. Transition-based panels MUST NOT replay their opening transition either (they re-attach in `'idle'` with no `data-starting-style`, and this holds for both initially-open and user-opened panels).
  Source: `collapsible/panel/useCollapsiblePanel.ts:62,76-81,122-129,142-147`. Test: [L4] `CollapsiblePanel.test.tsx` "does not replay open transitions when revealing an initially open panel", "does not replay open transitions when revealing a panel opened by the user", "does not replay open keyframe animations when revealing an initially open panel", "does not replay open keyframe animations when revealing a panel opened by the user", "does not replay open keyframe animations from inline styles when revealing a panel opened by the user".
- **COL-ANI-21** — MUST. When the Panel's JavaScript twin is disposed and its observers cancelled (unmount or detachment) any pending temporary duration override from a `beforematch` open (COL-ANI-22) is restored immediately.
  Source: `collapsible/panel/useCollapsiblePanel.ts:109-120,142-147`. Test: [L3] `CollapsiblePanel.test.tsx` "restores the inline transition duration when an instant open is interrupted".

### 9.5 `hiddenUntilFound` and `beforematch`

The browser dispatches `beforematch` on a `hidden="until-found"` element when find-in-page (or a fragment navigation) matches text inside it, then removes `hidden` and scrolls to the match in the same task. The Panel listens for this event on its own element for the whole time the element exists (the listener is attached whenever the Panel element is present, independent of `hiddenUntilFound`). The reveal handling is the shared mechanism of [transitions-and-mounting](../architecture/transitions-and-mounting.md) (TRN-ANI-32…34, TRN-DOM-08/09, TRN-ALG-07), for which Collapsible is the implementing component; the bullets below state what the Panel supplies to it and the Collapsible-observable outcome.

- **COL-ANI-22** — MUST. On a non-canceled `beforematch` (COL-EVT-04/05) the Panel arms the shared one-shot motion skip using its own detected animation type (§9.1); the observable result is per TRN-ANI-32 and TRN-ALG-07: `data-starting-style` is removed synchronously inside the event, `open = true` is requested, and on the opening pass the inline `transition-duration` (`'css-transition'`) or `animation-duration` (`'css-animation'`) is set to `0s` (nothing extra for `'none'`), with the Panel status forced to `'idle'` before the first paint in all three cases (COL-STA-06). Collapsible-specific: the `0s` override remains until the Panel next closes, when the previous inline value (or absence) is restored before the close is classified (COL-ANI-03), so the close animates with the authored duration.
  Source: `collapsible/panel/useCollapsiblePanel.ts:433-447,188-229,109-120`; see TRN-ANI-32, TRN-ALG-07. Test: [L4] `CollapsiblePanel.test.tsx` "expands the panel while beforematch is being dispatched", "keeps the temporary zero animation duration until the panel closes", "restores the transition duration before the first close after a beforematch open".
- **COL-ANI-23** — MUST. The skip-motion flag is consumed by exactly one open, per TRN-ALG-07 (cleared on the opening pass, not when motion is detected): if the Panel had no motion at the time of the `beforematch` open and motion is added later, a later ordinary open MUST animate.
  Source: `collapsible/panel/useCollapsiblePanel.ts:191-192`; see TRN-ALG-07. Test: [L4] `CollapsiblePanel.test.tsx` "does not suppress a later animated open after a no-motion beforematch open".
- **COL-ANI-24** — MUST. The Panel supplies its latest rendered `open` (read through a DOM-handle-like reference) to the shared reveal-revert check; the observable result is per TRN-ANI-34 and TRN-ALG-07 (`scheduleRevert`): after a non-canceled `beforematch` whose open never arrives (a consumer-owned `open` that ignored the request), after a 0 ms macrotask and then the next frame the skip-motion flag is cleared, `data-starting-style` is restored if it was present before the event, and `hidden="until-found"` is set again, returning the element to its fully collapsed, searchable state without any framework re-render.
  Source: `collapsible/panel/useCollapsiblePanel.ts:390-417,440,447`; see TRN-ANI-34, TRN-ALG-07. Test: [L4] `CollapsiblePanel.test.tsx` "re-collapses the panel when the open state never arrives", "re-collapses a keyframe panel when the open state never arrives".
- **COL-ANI-25** — MUST. After a canceled `beforematch` the observable result is per TRN-ANI-33: the same 0 ms-macrotask-then-frame check runs but only re-applies `hidden="until-found"`; `data-starting-style` was never removed and the skip flag never set. A following Trigger press opens normally with the authored durations.
  Source: `collapsible/panel/useCollapsiblePanel.ts:426-431,404-414`; see TRN-ANI-33, TRN-ALG-07. Test: [L4] `CollapsiblePanel.test.tsx` "does not open or suppress the next trigger open when beforematch is canceled".
- **COL-ANI-26** — MUST. The pending reveal check (timeout and frame) is canceled if the Panel element is torn down.
  Source: `collapsible/panel/useCollapsiblePanel.ts:452-456`. Test: none.
- **COL-ANI-27** — MUST. When a `hiddenUntilFound` Panel finishes closing, the element is `hidden="until-found"`, carries `data-starting-style` (for non-keyframe panels, COL-DOM-15), and has no running (unfinished) animations; the consumer's `[data-starting-style]` styles are what the element shows if revealed.
  Source: `collapsible/panel/useCollapsiblePanel.ts:93-94,355-366,466-468`. Test: [L4] `CollapsiblePanel.test.tsx` "does not keep a hidden transition running after a hiddenUntilFound panel closes".
- **COL-ANI-28** — MUST. In browsers that do not implement `hidden="until-found"`, a `hiddenUntilFound` Panel behaves as a `keepMounted` Panel hidden with a plain `hidden` attribute (the string value is treated as boolean by such browsers); find-in-page does not reveal it.
  Source: `collapsible/panel/useCollapsiblePanel.ts:365`. Docs: `components/collapsible/page.mdx` §Hidden until found. Test: none.

### 9.6 Animation-completion detection details that the Panel relies on

Specified in TRN; restated here only as the Panel's dependencies: completion = all `Element.getAnimations()` entries whose effect timing has finite `duration` and `iterations` have settled (`finished`), re-checked when an animation is canceled and replaced; when `getAnimations` is unavailable or the global test switch `BASE_UI_ANIMATIONS_DISABLED` is set, completion is immediate; the completion callback runs in a synchronous flush. Source: `internals/useAnimationsFinished.ts:93-132`; `utils/getFiniteAnimations.ts:4-9`.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- COL-STA-04 — TRN-ANI-03/09/10 row: `mounted` becomes `false` (and the closed Panel is hidden or removed) in the first render batch after the `transitions` module's completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- COL-ANI-09 — TRN-ANI-03 row, no-motion clause (a): the `'none'` close lands in the first render batch after JS reports the detected type, not before the next paint; JS sets `hidden` meanwhile (optimistic convergent write).
- COL-ANI-10 — TRN-ANI-03 row, `'none'` and zero-size clauses: the immediate unmount lands in the first render batch after JS reports the detection or measurement, about two round trips later; JS sets `hidden` meanwhile.
- COL-ANI-11 — TRN-ANI-03/09/10 row: unmount and held-size clearing land in the first render batch after the completion message instead of a synchronous flush in the microtask the animations finish; JS sets `hidden` at animation end so no intermediate frame shows the element.
- COL-STA-07 — RND-STA-01 row for mirrored fields: `transitionStatus` reaches the C# state objects (and so `ClassValue`/`StyleValue`) one round trip after JS writes `data-starting-style`/`data-ending-style`.
- COL-STA-05, COL-ANI-04 (next-frame removal of `data-starting-style`), COL-ANI-10 (next-frame `data-ending-style`) — TRN-STA-04/05/07/09 row, met on both modes: the flips are JS-owned with C# mirroring; listed so they are not moved back to C#.
- COL-ANI-14 — met on both modes by the handover at attach (decisions/04-js-interop-boundary.md Amendments 2026-10-18): C# renders `animation-name: none` and the consumer's merged `style` in prerender and the first interactive render; JS owns the Panel `style` from `attach`. Listed so the initial value is not withheld from prerender.

## 10. Positioning

Not applicable — Collapsible has no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` / state attributes | [rendering-and-composition](../architecture/rendering-and-composition.md) | Root: `'div'`, mapping = `collapsibleOpenStateMapping` + `transitionStatusMapping` (`open` → `data-open`/`data-closed`; `transitionStatus` → starting/ending; `disabled` default). Trigger: `'button'`, mapping = `triggerOpenStateMapping` + `transitionStatusMapping` (`open` → `data-panel-open` only). Panel: `'div'`, same mapping as Root but against Panel state; `style` resolved by the Panel itself and re-merged after the CSS variables (COL-DOM-18). |
| `useControlled` | [shared-utilities](../architecture/shared-utilities.md) | `{ controlled: open, default: defaultOpen, name: 'Collapsible', state: 'open' }`. |
| `useBaseUiId` / `useId` | [rendering-and-composition](../architecture/rendering-and-composition.md) | One generated id per Root (default panel id), prefix `base-ui`. |
| `createChangeEventDetails` / reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | `'trigger-press'` with the click's native event; `'none'` with the `beforematch` event; no `trigger` element. |
| `useButton` + `useFocusableWhenDisabled` | [button-behavior](../architecture/button-behavior.md) | `{ disabled: <effective>, native: nativeButton }`; `focusableWhenDisabled` unset, `tabIndex` default `0`, `composite` inferred (none for a bare Collapsible; Accordion wraps triggers in a composite). |
| `useTransitionStatus` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | `(open, enableIdleState = true, deferEndingState = true, animateInitialOpen = false)`. |
| `useAnimationsFinished` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | Closing: `(panelRef)` with defaults (no starting-style wait, no batching), invoked one frame after `'ending'`. Opening: via `useOpenChangeComplete` with `open: true` (waits for `data-starting-style` removal), `batch: false`. |
| `useOpenChangeComplete` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | `enabled: open && mounted && panelStatus === 'idle'`, `open: true`, `onComplete` clears the held size unless closed meanwhile. |
| `mergeProps` | [rendering-and-composition](../architecture/rendering-and-composition.md) | Trigger prop order: component ARIA/onClick → consumer props → button props getter. Panel prop order: hook props (`hidden`, `id`, persisted starting style) → CSS variables → consumer props → consumer `style` → `animation-name: none`. |
| `warn` (log-once) | [shared-utilities](../architecture/shared-utilities.md) | Two development warnings (COL-EDGE-02/03), each emitted at most once per page session per message. |
| `AnimationFrame` / `Timeout` | [shared-utilities](../architecture/shared-utilities.md) | Frame deferrals: `'ending'` status, `'idle'` status, layout-style restore, close-wait start, reveal revert; one `0 ms` timeout for the reveal decision. |

Accordion reuses `useCollapsibleRoot` with `{ open: <derived from Accordion value>, onOpenChange: <Accordion's>, disabled }` (no `defaultOpen`) and `useCollapsiblePanel` with identical parameters to `Collapsible.Panel`, differing only in where `hiddenUntilFound`/`keepMounted` defaults come from (Accordion Root) and in the id fallback (`idProp ?? defaultPanelId`, so an empty-string id is kept there). Source: `accordion/item/AccordionItem.tsx:68-72`; `accordion/panel/AccordionPanel.tsx:52-55,76-95`.

## 12. Edge cases, invariants and dev warnings

- **COL-EDGE-01** — MUST. Rendering `Collapsible.Trigger` or `Collapsible.Panel` without an ancestor `Collapsible.Root` throws an `Error` with message `Base UI: CollapsibleRootContext is missing. Collapsible parts must be placed within <Collapsible.Root>.` (in all builds).
  Source: `collapsible/root/CollapsibleRootContext.ts:15-23`. Test: [L2] `CollapsibleTrigger.test.tsx` "throws when rendered outside a Collapsible.Root".
- **COL-EDGE-02** — MUST. In development, rendering a Panel with `hiddenUntilFound` `true` and `keepMounted` explicitly `false` logs (once) the warning `Base UI: The `keepMounted={false}` prop on `Collapsible.Panel` is ignored when `hiddenUntilFound` is enabled, since the panel must remain mounted while closed.` The Panel behaves as `hiddenUntilFound` (stays in the document, `hidden="until-found"`).
  Source: `collapsible/panel/CollapsiblePanel.tsx:36-45`; `packages/utils/src/createLogOnce.ts:7-23`; see TRN-EDGE-04. Test: [L2] `CollapsiblePanel.test.tsx` "warns when hiddenUntilFound overrides keepMounted={false}".
- **COL-EDGE-03** — MUST. In development, detecting both a non-zero transition and a non-zero keyframe animation on the Panel (COL-ANI-02) logs (once) `Base UI: CSS transitions and CSS animations both detected on Collapsible or Accordion panel. Only one of either animation type should be used.`
  Source: `collapsible/panel/useCollapsiblePanel.ts:501-511`; see TRN-EDGE-07. Test: [L4] `CollapsiblePanel.test.tsx` "preserves inline alignment styles while measuring an opening panel".
- **COL-EDGE-04** — SHOULD. In development, a Trigger whose rendered element does not match `nativeButton` logs (once) an error from the shared button behavior: with `nativeButton` `true` and a non-`<button>` element: `A component that acts as a button expected a native <button> because the `nativeButton` prop is true. Rendering a non-<button> removes native button semantics, which can impact forms and accessibility. Use a real <button> in the `render` prop, or set `nativeButton` to `false`.`; with `nativeButton` `false` and a `<button>`: `A component that acts as a button expected a non-<button> because the `nativeButton` prop is false. Rendering a <button> keeps native behavior while Base UI applies non-native attributes and handlers, which can add unintended extra attributes (such as `role` or `aria-disabled`). Use a non-<button> in the `render` prop, or set `nativeButton` to `true`.` (each prefixed `Base UI: ` and possibly suffixed with an owner stack).
  Source: `internals/use-button/useButton.ts:36-66`. Test: none in `collapsible/` (covered by UBT).
- **COL-EDGE-05** — SHOULD. In development, switching `open` between `undefined` and defined after the first render logs (once) `Base UI: A component is changing the uncontrolled open state of Collapsible to be controlled.` (or `…the controlled open state of Collapsible to be uncontrolled.`) followed by the standard explanatory lines (see UTL).
  Source: `packages/utils/src/useControlled.ts:48-64`. Test: none.
- **COL-EDGE-06** — SHOULD. In development, changing `defaultOpen` after the first render of a component-owned instance logs (once) `Base UI: A component is changing the default open state of an uncontrolled Collapsible after being initialized. To suppress this warning opt to use a controlled Collapsible.`; the new default is ignored.
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: none.
- **COL-EDGE-07** — MUST. `disabled` affects only user interaction through Triggers: an open Panel stays open when `disabled` becomes `true`, and consumer-owned `open` changes still apply while disabled.
  Source: `collapsible/root/useCollapsibleRoot.ts:15-42` (`disabled` is never read by the state logic). Test: none.
- **COL-EDGE-08** — MUST. Nested Collapsibles are independent: a Trigger toggles its nearest ancestor Root only, and an inner Panel's size changes are reflected in the outer Panel only while the outer held size is `auto` (COL-ANI-08).
  Source: `collapsible/root/CollapsibleRootContext.ts:11-24`. Test: none (exercised by `docs/src/app/(private)/experiments/collapsible/transitions.tsx:109-123`).
- **COL-EDGE-09** — MUST. `attach` and `dispose` of the Panel's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op. The Panel's id registration survives an unregistration followed by a re-registration in one cycle (the id stays registered), and an unregistration only clears the registration if the id being unregistered is the one currently registered.
  Source: `collapsible/panel/CollapsiblePanel.tsx:64-69`. Test: [L2] twin contract (a repeated `attach`/`dispose` of the same Panel element records no further effect); `CollapsibleRoot.test.tsx` "unregisters and restores the generated panel id when the panel remounts" (Base UI runs it under StrictMode).
- **COL-EDGE-10** — MUST. A consumer `style` function on the Panel is evaluated against the Panel state (with the Panel transition status), not the Root state, and is re-evaluated on every state change.
  Source: `collapsible/panel/CollapsiblePanel.tsx:97`. Test: [L2] `CollapsibleRoot.test.tsx` "passes state to className and style callbacks".

## 13. Behaviors evidenced by tests

Coverage of each test by the requirements above, plus requirements that exist only because a test asserts them.

### `root/CollapsibleRoot.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (Root; `refInstanceof: HTMLDivElement`) | COL-STR-01; RND-DOM-36 … RND-DOM-48 |
| "sets ARIA attributes" | COL-ARIA-01, COL-ARIA-02, COL-DOM-10 |
| "references manual panel id in trigger aria-controls" | COL-API-10, COL-ARIA-03 |
| "unregisters and restores the generated panel id when the panel remounts" | COL-STA-09, COL-ARIA-04, COL-EDGE-09 |
| "disabled status" | COL-API-04, COL-DOM-05 |
| "does not toggle or call onOpenChange when clicked while disabled" | COL-PTR-02 |
| "calls onOpenChange with eventDetails" | COL-EVT-01, COL-EVT-02, COL-EVT-07 |
| "eventDetails.cancel() prevents opening while uncontrolled" | COL-EVT-03, COL-STA-02 |
| "eventDetails.cancel() prevents closing while uncontrolled" | COL-EVT-03, COL-STA-02 |
| "controlled trigger presses request open and close state changes" | COL-STA-03 |
| "does not change controlled open state without an external update" | COL-API-01, COL-STA-03, COL-EVT-01 |
| "controlled mode" | COL-API-03, COL-STA-01, COL-ARIA-02, COL-DOM-04, COL-DOM-11, COL-EVT-06, COL-DOM-20 |
| "uncontrolled mode" | COL-API-02, COL-STA-02, COL-DOM-04, COL-DOM-16 |
| "passes state to className and style callbacks" | COL-STA-07, COL-EDGE-10 |
| "key: $key does not toggle or call onOpenChange when disabled" (2 variants: Enter, Space) | COL-KEY-03 |
| "key: $key should toggle the Collapsible" (2 variants: Enter, Space) | COL-KEY-01, COL-KEY-02, COL-KEY-04, COL-DOM-09 |

- **COL-DOM-20** — MUST. When the Panel becomes visible (open), its content is visible in the computed sense (not `display: none`, not `hidden`) in the same update cycle in which `aria-expanded` becomes `"true"`.
  Source: `collapsible/panel/useCollapsiblePanel.ts:74,469`. Test: [L4] `CollapsibleRoot.test.tsx` "controlled mode", "uncontrolled mode" (`toBeVisible()` right after the click).

### `trigger/CollapsibleTrigger.test.tsx`

| Test | Requirements |
|---|---|
| "throws when rendered outside a Collapsible.Root" | COL-EDGE-01 |
| describeConformance (`button: true`, `testRenderPropWith: 'button'`, `refInstanceof: HTMLButtonElement`, inside Root; non-native re-run with `div`) | COL-API-05, COL-STR-02; RND-DOM-36 … RND-DOM-49 |
| "renders the disabled attribute when disabled" | COL-DOM-08 |
| "removes a disabled non-native trigger from the tab order" | COL-DOM-07, COL-DOM-08, COL-KEY-03 |
| "forwards the id prop" | COL-API-07 |

### `panel/CollapsiblePanel.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (Panel, `refInstanceof: HTMLDivElement`, inside `Root defaultOpen`) | COL-STR-03; RND-DOM-36 … RND-DOM-48 |
| "warns when hiddenUntilFound overrides keepMounted={false}" | COL-EDGE-02, COL-DOM-14 |
| "does not unmount the panel when true" | COL-API-08, COL-DOM-11, COL-DOM-13, COL-ARIA-02, COL-DOM-04 |
| "hides the panel after external controlled close when true and no animations are applied" | COL-STA-04, COL-STA-06, COL-ANI-09(a), COL-DOM-13 |
| "unmounts a panel that mounts after the close has already entered the ending phase" | COL-ANI-10, COL-ANI-18, COL-ANI-19 |
| "applies data-starting-style while opening" | COL-ANI-04, COL-DOM-12, COL-STA-05 |
| "restores a measured height before applying closing transition styles" | COL-ANI-07, COL-ANI-09(b), COL-ANI-10, COL-DOM-17 |
| "unmounts a zero-size panel without waiting for unrelated transitions" | COL-ANI-10 (zero-size early exit) |
| "finishes closing when the render function removes the panel element" | COL-ANI-18, COL-DOM-06, COL-EVT-06 |
| "preserves inline alignment styles while measuring an opening panel" | COL-ANI-02, COL-ANI-05, COL-EDGE-03 |
| "keeps exit transitions working after a close is interrupted by reopening" | COL-ANI-16, COL-STA-05 |
| "keeps the measured size when an open animation finishes during a close commit" | COL-ANI-17 |
| "does not restart the entrance transition when a close animation finishes after reopening" | COL-ANI-16, COL-ANI-11 |
| "does not run the mount animation when initially open" | COL-ANI-13, COL-ANI-29 |
| "animates on reopen after an initially open panel is removed by the render function" | COL-ANI-15 |
| "still animates on close and reopen after being initially open" | COL-ANI-13, COL-ANI-12 |
| "restores measured dimensions before applying a closing keyframe animation" | COL-ANI-12, COL-ANI-09(b) |
| "still animates on reopen after being initially open when only open keyframes are defined" | COL-ANI-13, COL-ANI-06 |
| "suppresses the initial keyframe animation when rendered open" | COL-ANI-14, COL-DOM-18 |
| "suppresses the initial keyframe animation from inline styles when rendered open" | COL-ANI-14, COL-DOM-18 |
| "does not replay open transitions when revealing an initially open panel" | COL-ANI-20 |
| "does not replay open transitions when revealing a panel opened by the user" | COL-ANI-20 |
| "does not replay open keyframe animations when revealing an initially open panel" | COL-ANI-20 |
| "does not replay open keyframe animations when revealing a panel opened by the user" | COL-ANI-20 |
| "does not replay open keyframe animations from inline styles when revealing a panel opened by the user" | COL-ANI-20 |
| "keeps the temporary zero animation duration until the panel closes" | COL-ANI-22, COL-ANI-03, COL-STA-06 |
| "restores the transition duration before the first close after a beforematch open" | COL-ANI-22, COL-ANI-03 |
| "does not suppress a later animated open after a no-motion beforematch open" | COL-ANI-23 |
| "restores the inline transition duration when an instant open is interrupted" | COL-ANI-21 |
| "does not keep a hidden transition running after a hiddenUntilFound panel closes" | COL-ANI-27 |
| "does not open or suppress the next trigger open when beforematch is canceled" | COL-EVT-05, COL-ANI-25, COL-DOM-15 |
| "expands the panel while beforematch is being dispatched" | COL-ANI-22, COL-DOM-15, COL-ANI-30 |
| "re-collapses the panel when the open state never arrives" | COL-ANI-24 |
| "re-collapses a keyframe panel when the open state never arrives" | COL-ANI-24, COL-DOM-15 |
| "uses `hidden=\"until-found\" to hide panel when true" | COL-API-09, COL-EVT-04 |

- **COL-ANI-29** — MUST. An initially-open keyframe Panel has zero running animations and a computed `animation-name` of `none` after mount.
  Source: `collapsible/panel/useCollapsiblePanel.ts:76-81`; `collapsible/panel/CollapsiblePanel.tsx:122`. Test: [L4] `CollapsiblePanel.test.tsx` "does not run the mount animation when initially open".
- **COL-ANI-30** — MUST. A listener registered on the Panel element *after* the component's own `beforematch` listener observes, within the same dispatch, that `data-starting-style` is already absent and that the element's computed height is no longer `0px` once `hidden` is removed.
  Source: `collapsible/panel/useCollapsiblePanel.ts:440-441,450`. Test: [L4] `CollapsiblePanel.test.tsx` "expands the panel while beforematch is being dispatched".

### `root/CollapsibleRoot.spec.tsx`

Type-level only: asserts that `Collapsible.Root.Props/State/ChangeEventReason/ChangeEventDetails`, `Collapsible.Trigger.Props/State` and `Collapsible.Panel.Props/State` are exported both namespaced and as flat aliases, and that `onOpenChange` receives `(boolean, CollapsibleRootChangeEventDetails)`. Covered by §3.4.

Test-environment notes: tests marked `skipIf(isJSDOM)` run only in a real browser (Chromium); tests under "interrupted beforematch opens" and "prop: hiddenUntilFound" additionally require `'onbeforematch' in window` (not Firefox at the time of writing) and, for the former group, React ≥ 19. `globalThis.BASE_UI_ANIMATIONS_DISABLED` is a test switch that makes animation completion immediate (§9.6).

## 14. React-specific notes and porting considerations

Non-normative.

- **Update-cycle phases.** Many requirements say "before the next paint" (React layout effects: `useIsoLayoutEffect`) versus "after paint" (passive effects: the `'ending'` watcher at `useCollapsiblePanel.ts:308-353` and the `beforematch` listener registration). The measurement, status forcing, mount-flag clearing on no-motion closes, and the `hidden="until-found"` write all happen in the pre-paint phase; a port must ensure the same ordering: DOM updated → measure/mutate inline styles → (re-render synchronously if state changed) → paint. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, timing-term mapping); decisions/04-js-interop-boundary.md §1 (`dom` module: Collapsible height) and §2 (ownership list: measured `--collapsible-panel-height/width` are JS-owned, C# mirrors).
- **Synchronous re-render before paint.** Several paths set state inside the pre-paint phase (`setDimensions`, `setForcePanelIdle`, `setMounted(false)` on no-motion close, `useTransitionStatus`'s `'starting'` on reopen). React flushes these synchronously so the user never sees the intermediate frame. A port with batched/async rendering must flush equivalently (e.g. apply the measured size directly to the element before yielding). Port: see decisions/04-js-interop-boundary.md §2 (JS writes the measured variables itself; C# mirrors them one round trip late on Server).
- **Synchronous flush on animation completion.** `useAnimationsFinished` calls `ReactDOM.flushSync` so the unmount happens in the same task as the `finished` promise resolution (no painted frame between "animation done" and "element hidden"). Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (row TRN-ANI-03/09/10) and decisions/04-js-interop-boundary.md §2 (optimistic convergent write).
- **Status transitions on reopen.** `useTransitionStatus` computes `'starting'` during render when `open && !mounted`; when reopening during `'ending'` (`mounted` still true) the first render still shows `'ending'`, then a layout effect sets `'starting'`. The observable contract is COL-STA-05's "before the next paint". Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (row TRN-STA-04/05/07/09, met on both modes because the `data-starting-style`/`data-ending-style` flips are JS-owned) and decisions/04-js-interop-boundary.md §2.
- **`hidden="until-found"` and React.** React coerces `hidden` to a boolean (react#24740), so the component writes the string value directly to the DOM and must re-write it after the browser removes it (the reveal-revert logic in COL-ANI-24/25). A framework that supports string `hidden` natively can render it declaratively but must still handle the browser removing the attribute without a state change. Port: see decisions/04-js-interop-boundary.md §1 (`transitions` module: `hidden="until-found"` and `beforematch`, TRN-ALG-07) and §2 (ownership list: `Collapsible.Panel` `hidden="until-found"` is JS-owned).
- **Refs read during render.** `shouldPreventMountAnimationRef`, `shouldPreventActivityResumeAnimationRef`, `animationTypeRef` and `lastMeasuredDimensionsRef` are mutable cells read during render (source comments at `useCollapsiblePanel.ts:78-91`). They hold "last committed" values; a port should model them as ordinary instance fields updated in the pre-paint phase. Port: see decisions/04-js-interop-boundary.md §1 (`dom` module: Collapsible height), which places the measurement loop and these last-committed values in JavaScript.
- **StrictMode / double-invoked effects.** The id registration (`CollapsiblePanel.tsx:64-69`) is written to survive mount → cleanup → mount of the same effect; the test "unregisters and restores the generated panel id…" runs under StrictMode. The `null` vs `undefined` distinction in the registration (`null` = unregistered, `undefined` = never registered → use default id) must be preserved.
- **React.Activity.** The "activity" tests use `React.Activity` (React 19.2+): hiding detaches effects and preserves state. COL-ANI-20 is the framework-neutral statement; the passive-effect cleanup at `useCollapsiblePanel.ts:142-147` is the hook.
- **`useId` format.** On React ≥ 18 the generated panel id is `base-ui-` + `React.useId()` (e.g. `base-ui-«r1»` or `base-ui-:r1:` depending on React version), stable across server/client. On React 17 a global counter `base-ui-<n>` is assigned after the first commit, so the first render has no id. A port needs any document-unique, SSR-stable id. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5, generated ids).
- **Stable callbacks.** `onOpenChange` is wrapped so the latest consumer function is always called without re-subscribing listeners; the development-only render-time guard is unreachable from consumer code (COL-EVT-08). Port: see decisions/05-state-store-and-event-details.md §2 (`Open`/`OpenChanged`/`DefaultOpen` plus `OnOpenChange(details)`, called first; cancel stops the request).
- **Render function removing the element.** Because `render` may return nothing, every effect null-checks the element; the port must tolerate a Panel part that is "rendered" but has no DOM node (COL-ANI-15/18/19). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 1, substitute callback) and §Requirements relaxed or withdrawn for the port (row RND-DOM-20..24: no element reference under a substitute callback).
- **Server rendering.** The pre-paint effects do not run on the server; only COL-ANI-14 (inline `animation-name: none`) and the static attributes (`hidden`, `data-*`, `id`) are in server markup. The CSS variables are `auto` in server markup. Port: see decisions/04-js-interop-boundary.md §2 (prerender emits no JS-owned attribute, and C# never renders `style` on an element with JS-owned style properties); how that squares with COL-ANI-14 and the `auto` variables in server markup is open (§16 Q11).
- **React 17 vs 18/19.** Besides `useId`, `useInsertionEffect` (used for the stable callback) is absent on 17 and falls back to a synchronous call; behavior is otherwise identical.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| COL-STR-01 | MUST | Root renders div, owns state, no handlers | 2 |
| COL-STR-02 | MUST | Trigger renders button, must be inside Root | 2 |
| COL-STR-03 | MUST | Panel renders div, inside Root, at most one | 2 |
| COL-STR-04 | MUST | Without a Panel, Trigger still toggles; aria-controls points at default id | 2 |
| COL-API-01 | MUST | `open` = consumer-owned state | 3.1 |
| COL-API-02 | MUST | `defaultOpen` default false, read once | 3.1 |
| COL-API-03 | MUST | `onOpenChange` optional | 3.1 |
| COL-API-04 | MUST | `disabled` default false; disables Triggers; in all states | 3.1 |
| COL-API-05 | MUST | `nativeButton` default true → type=button / role=button | 3.2 |
| COL-API-06 | MUST | Trigger `disabled` overrides Root's | 3.2 |
| COL-API-07 | MUST | Trigger `id` forwarded | 3.2 |
| COL-API-08 | MUST | `keepMounted` default false | 3.3 |
| COL-API-09 | MUST | `hiddenUntilFound` default false; implies kept mounted, until-found | 3.3 |
| COL-API-10 | MUST | Panel `id`: non-empty used & registered; else generated | 3.3 |
| COL-STA-01 | MUST | Ownership mode fixed at first render | 4.1 |
| COL-STA-02 | MUST | Uncontrolled press toggles unless canceled | 4.1 |
| COL-STA-03 | MUST | Controlled press only reports | 4.1 |
| COL-STA-04 | MUST | `mounted` lifecycle | 4.2 |
| COL-STA-05 | MUST | Root transition status cycle | 4.2 |
| COL-STA-06 | MUST | Panel status forced idle on motion-skipping opens | 4.2 |
| COL-STA-07 | MUST | State objects `{open, disabled, transitionStatus}` | 4.3 |
| COL-STA-08 | MUST | hidden = !open && !mounted | 4.3 |
| COL-STA-09 | MUST | Panel id resolution/registration | 4.3 |
| COL-DOM-01 | MUST | Root data-open/data-closed | 5.1 |
| COL-DOM-02 | MUST | Root data-disabled | 5.1 |
| COL-DOM-03 | MUST | Root starting/ending style attributes | 5.1 |
| COL-DOM-04 | MUST | Trigger data-panel-open | 5.2 |
| COL-DOM-05 | MUST | Trigger data-disabled | 5.2 |
| COL-DOM-06 | MUST | Trigger starting/ending style attributes | 5.2 |
| COL-DOM-07 | MUST | type=button vs role=button | 5.2 |
| COL-DOM-08 | MUST | disabled / aria-disabled / tabindex=-1 matrix | 5.2 |
| COL-DOM-09 | MUST | Enabled Trigger tabindex=0 | 5.2 |
| COL-DOM-10 | MUST | Panel always has id | 5.3 |
| COL-DOM-11 | MUST | Panel data-open/data-closed | 5.3 |
| COL-DOM-12 | MUST | Panel starting/ending style attributes | 5.3 |
| COL-DOM-13 | MUST | Boolean hidden when !open && !mounted; consumer override | 5.3 |
| COL-DOM-14 | MUST | hidden="until-found" with hiddenUntilFound | 5.3 |
| COL-DOM-15 | MUST | Persisted data-starting-style on hidden until-found non-keyframe panel | 5.3 |
| COL-DOM-16 | MUST | Panel element rendered iff keepMounted‖hiddenUntilFound‖mounted‖open | 5.3 |
| COL-DOM-17 | MUST | CSS variables auto / px | 5.3 |
| COL-DOM-18 | MUST | Panel inline style precedence | 5.3 |
| COL-DOM-19 | MUST | Panel data-disabled | 5.3 |
| COL-DOM-20 | MUST | Content visible in same cycle as aria-expanded=true | 13 |
| COL-ARIA-01 | MUST | aria-expanded always | 5.4 |
| COL-ARIA-02 | MUST | aria-controls only while open | 5.4 |
| COL-ARIA-03 | MUST | aria-controls equals Panel id | 5.4 |
| COL-ARIA-04 | MUST | aria-controls dropped/restored with Panel part | 5.4 |
| COL-ARIA-05 | MUST | No role on Panel, no ARIA on Root | 5.4 |
| COL-KEY-01 | MUST | Enter toggles | 6.1 |
| COL-KEY-02 | MUST | Space toggles | 6.1 |
| COL-KEY-03 | MUST | Disabled: no toggle, not focusable/tab-skipped | 6.1 |
| COL-KEY-04 | MUST | No other keys; Tab not intercepted | 6.1 |
| COL-PTR-01 | MUST | Click toggles via onOpenChange then state | 6.2 |
| COL-PTR-02 | MUST | Disabled click ignored | 6.2 |
| COL-PTR-03 | MUST | Consumer onClick first; preventBaseUIHandler skips toggle | 6.2 |
| COL-FOC-01 | MUST | No focus movement | 6.3 |
| COL-EVT-01 | MUST | onOpenChange on press, before state change | 7 |
| COL-EVT-02 | MUST | details.event is the native click event | 7 |
| COL-EVT-03 | MUST | cancel() blocks uncontrolled change | 7 |
| COL-EVT-04 | MUST | beforematch → onOpenChange(true, 'none') synchronously | 7 |
| COL-EVT-05 | MUST | cancel() on beforematch blocks reveal | 7 |
| COL-EVT-06 | MUST | Not called for prop changes or completions | 7 |
| COL-EVT-07 | MUST | trigger undefined; allowPropagation no-op | 7 |
| COL-EVT-08 | MUST | Latest consumer handler is the one invoked | 7 |
| COL-ANI-01 | MUST | Animation type detection rule | 9.1 |
| COL-ANI-02 | MUST | Both present → warn, treat as transition | 9.1 |
| COL-ANI-03 | MUST | Restore pending 0s override before classifying a close | 9.1 |
| COL-ANI-04 | MUST | Open sequence: starting + measure before paint, idle next frame | 9.2 |
| COL-ANI-05 | MUST | Transition open: alignment styles neutralized during measure | 9.2 |
| COL-ANI-06 | MUST | Keyframe open: measure with animation in place; none-type forced idle | 9.2 |
| COL-ANI-07 | MUST | After open motion finishes: clear held size; not if closed meanwhile | 9.2 |
| COL-ANI-08 | MUST | No re-measure on resize | 9.2 |
| COL-ANI-09 | MUST | Close request: none → unmount before paint; else measure | 9.3 |
| COL-ANI-10 | MUST | Ending frame: re-detect; zero-size → unmount immediately | 9.3 |
| COL-ANI-11 | MUST | Ending: wait 1 frame + animations, then unmount & clear | 9.3 |
| COL-ANI-12 | MUST | Keyframe close renders cached size when held is auto | 9.3 |
| COL-ANI-13 | MUST | Initially open: no mount keyframe; cache size; cleared on first close | 9.4 |
| COL-ANI-14 | MUST | SSR markup has animation-name: none when initially open | 9.4 |
| COL-ANI-15 | MUST | Reopen animates after render function removed element | 9.4 |
| COL-ANI-16 | MUST | Interrupted close resumes as open; late completion ignored | 9.4 |
| COL-ANI-17 | MUST | Late open-completion does not clear size after close | 9.4 |
| COL-ANI-18 | MUST | Missing element during ending → finish immediately | 9.4 |
| COL-ANI-19 | MUST | Ending status visible to render function; late-mounted element closes | 9.4 |
| COL-ANI-20 | MUST | Re-attachment never replays open motion | 9.4 |
| COL-ANI-21 | MUST | Twin disposal restores pending 0s override | 9.4 |
| COL-ANI-22 | MUST | beforematch open: strip starting style, skip motion via 0s override until next close | 9.5 |
| COL-ANI-23 | MUST | Skip flag consumed by exactly one open | 9.5 |
| COL-ANI-24 | MUST | Open never arrives → revert after 0 ms + 1 frame | 9.5 |
| COL-ANI-25 | MUST | Canceled beforematch → only hidden re-applied | 9.5 |
| COL-ANI-26 | MUST | Reveal check canceled on teardown | 9.5 |
| COL-ANI-27 | MUST | Closed until-found panel: hidden, starting style, no running animations | 9.5 |
| COL-ANI-28 | MUST | Unsupported browsers: behaves as keepMounted + hidden | 9.5 |
| COL-ANI-29 | MUST | Initially-open keyframe panel: 0 animations, computed name none | 13 |
| COL-ANI-30 | MUST | Later beforematch listener sees starting style gone and non-zero height | 13 |
| COL-EDGE-01 | MUST | Missing Root error message | 12 |
| COL-EDGE-02 | MUST | keepMounted={false} + hiddenUntilFound warning | 12 |
| COL-EDGE-03 | MUST | Mixed transition/animation warning | 12 |
| COL-EDGE-04 | SHOULD | nativeButton mismatch errors | 12 |
| COL-EDGE-05 | SHOULD | Controlled/uncontrolled switch error | 12 |
| COL-EDGE-06 | SHOULD | defaultOpen change error | 12 |
| COL-EDGE-07 | MUST | disabled does not affect open state | 12 |
| COL-EDGE-08 | MUST | Nested Collapsibles independent | 12 |
| COL-EDGE-09 | MUST | Twin attach/dispose idempotent; guarded unregister | 12 |
| COL-EDGE-10 | MUST | Panel style function sees Panel state | 12 |

Counts: STR 4, API 10, STA 9, DOM 20, ARIA 5, KEY 4, PTR 3, FOC 1, EVT 8, ANI 30, EDGE 10 — total 104.

## 16. Open questions

1. **`onOpenChangeComplete` / `actionsRef` do not exist on Collapsible in v1.9.0.** The assignment brief lists `onOpenChangeComplete` among the props to cover, but `collapsible/root/CollapsibleRoot.tsx:80-104` defines only `open`, `defaultOpen`, `onOpenChange`, `disabled`; `grep` of `collapsible/` finds no `onOpenChangeComplete`, `actionsRef` or `preventUnmountOnClose`. Animation completion is consumed internally (COL-ANI-07/11) and not reported. Confirm this is intentional for the port (an implementation may still want to expose completion, but it is not part of the 1.9.0 contract).
2. **Outline vs source: extra data attributes.** `components/page.mdx` lists Trigger data attributes as `data-panel-open` only and omits `data-disabled` from Root/Trigger/Panel. The source renders `data-disabled` on all three (default mapping, `internals/getStateAttributesProps.ts:24-28`; asserted by "disabled status") and `data-starting-style`/`data-ending-style` on the Trigger (`collapsible/trigger/CollapsibleTrigger.tsx:12-15`; asserted by "finishes closing when the render function removes the panel element"). Documented here as requirements (COL-DOM-02/05/06/19); the outline is incomplete.
3. **Trigger `disabled` prop.** The outline lists Trigger props as `className, nativeButton, render, style`, but `CollapsibleTrigger.tsx:37` reads `disabled` from the Trigger's own props as an override of the Root's (CHANGELOG v1.0.0-alpha.7 "Update props destructuring to fix Trigger disabled state (#1469)"). Documented as COL-API-06; untested.
4. **Dangling `aria-controls` with no Panel.** With a Trigger but no Panel ever rendered, the Root's registered id is `undefined`, so `aria-controls` references the generated default id while open (`useCollapsibleRoot.ts:29`), pointing at no element. Only after a Panel mounts and unmounts does the id become `null` → attribute omitted. Possibly unintended; a port could choose to omit `aria-controls` until a Panel registers, but that would diverge from source.
5. **Panel `id` empty-string handling differs from Accordion.** `CollapsiblePanel.tsx:61` uses `idProp || undefined` (empty string → generated id) whereas `AccordionPanel.tsx:55` uses `idProp ?? defaultPanelId` for the rendered id (empty string kept) while registering `idProp || undefined`. For the Accordion author. Resolved: accordion.md ACC-API-18 and ACC-DOM-19 document the Accordion behavior (empty string kept as the rendered id, only a non-empty id registered); Collapsible keeps COL-API-10.
6. **Purpose of the `animation-name: none` set-and-restore in the ending phase.** `useCollapsiblePanel.ts:267-270` sets inline `animation-name: none` and immediately restores it, synchronously, with no style flush in between. The opening pass does the same after its measurement (`useCollapsiblePanel.ts:214-219`; COL-ANI-06). Per CSS Animations semantics this should have no observable effect (the computed value is unchanged at the next style recalculation). No test depends on it. Flagged so a port does not invent a "restart the closing animation" behavior on its basis; if a reviewer can show it is observable, COL-ANI-10 should be amended.
7. **Stale `'idle'` status after a no-motion close.** When the close completes before the deferred `'ending'` frame (COL-ANI-09(a)), `transitionStatus` stays `'idle'` while closed and unmounted (`internals/useTransitionStatus.ts:40-42` only resets from `'ending'`). Unobservable (no attribute for `'idle'`), but a port must not assert `undefined` there; noted in COL-STA-05.
8. **First commit of a `'none'`-type open carries `data-starting-style` on the Panel.** The forced-idle override is applied in the pre-paint phase after the first commit, so a synchronous observer (as in "applies data-starting-style while opening", which uses a transition) could see `data-starting-style` on a no-motion panel for one commit. Documented in COL-STA-06 as "from before the first paint"; whether the single pre-paint commit must be reproduced exactly is a porting decision.
9. **`beforematch` listener is attached regardless of `hiddenUntilFound`.** `useCollapsiblePanel.ts:368-459` registers the listener whenever the element exists. Since browsers only dispatch `beforematch` for `hidden="until-found"` content, this is unobservable in practice, but a synthetic `beforematch` dispatched on a `keepMounted` panel would open it with reason `'none'`. Documented in COL-EVT-04 as "when a `beforematch` event is dispatched".
10. **Width variable is measured but never exercised.** `--collapsible-panel-width` follows exactly the height rules (COL-DOM-17) but no test, demo or experiment uses it; the "both zero" unmount check (COL-ANI-10) is the only place width participates in logic.
11. **Server markup vs ADR 04's style ownership.** COL-ANI-14 requires server-rendered markup of an initially-open Panel to carry inline `animation-name: none` (asserted by "suppresses the initial keyframe animation when rendered open", `panel/CollapsiblePanel.test.tsx:873-903`, and its inline-styles twin at `:904-939`), and COL-DOM-17 says the inline style always defines the two CSS variables (`auto` in server markup; `collapsible/panel/CollapsiblePanel.tsx:110-122`). ADR 04 §2 lists the measured `--collapsible-panel-height/width` as JS-owned and states that C# never renders `style` on an element with JS-owned style properties and that prerender emits no JS-owned attribute; read literally, the prerendered Panel would have no inline `style` at all, so COL-ANI-14 and the `auto` server values would fail on prerender. Needs a decision: either C# prerenders the non-JS-owned declarations (`animation-name: none`, consumer style, `auto` variables) and JS takes over `style` on attach, or COL-ANI-14 is relaxed for the port. Decided (2026-10-18): C# renders the initial value once and JavaScript owns the attribute from attach (ADR 04 Amendments, handover at attach); COL-ANI-14 / ACC-ANI-03 hold without relaxation.
