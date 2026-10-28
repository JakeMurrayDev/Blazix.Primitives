# Tabs

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/tabs/` |
| Tests | `packages/react/src/tabs/**/*.test.tsx` (5 files) |
| Docs page | `docs/src/app/(docs)/react/components/tabs/page.mdx` |
| Prefix | `TAB` |
| Status | implemented |
| Revised | 2026-10-28 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [button-behavior](../architecture/button-behavior.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Tabs is a tabbed interface: a **List** of **Tab** buttons, exactly one of which is *active* (selected) at a time, and a set of **Panel** regions of which only the one whose `value` equals the active value is shown. The **Root** owns the active value (consumer-owned through `value`, or component-owned through `defaultValue`, which defaults to `0`), reports every change through `onValueChange`, and derives an *activation direction* (`'left' | 'right' | 'up' | 'down' | 'none'`) from the on-screen position of the previously active tab relative to the newly active one so that consumers can slide panels in from the correct side. An optional **Indicator** inside the List measures the active Tab and exposes its offsets and size as CSS variables (`--active-tab-left/right/top/bottom/width/height`) so that a sliding highlight can be styled with plain CSS.

The List is a composite roving-tabindex group (one tab stop for the whole list; arrow keys, <kbd>Home</kbd>/<kbd>End</kbd> move focus between Tabs, horizontally or vertically depending on `orientation`, with RTL-aware arrow mapping). By default (`activateOnFocus={false}`) moving focus does not change the selection; the focused Tab is activated with <kbd>Enter</kbd>/<kbd>Space</kbd> or a click. With `activateOnFocus` the selection follows focus. Disabled Tabs stay focusable (so keyboard users can discover them) but cannot be activated. An uncontrolled Root performs *automatic* selection changes — initial selection when no explicit default was given, and fallback to the first enabled Tab when the selected Tab is disabled or removed — and reports them with reasons `'initial'`, `'disabled'` and `'missing'`; these automatic changes are not cancelable.

Panels unmount when inactive unless `keepMounted`; they carry transition status attributes (`data-starting-style` / `data-ending-style`) and the activation direction so that enter/exit motion can be authored in CSS. Nothing floats, nothing is dismissed, and no form integration exists.

## 2. Anatomy

```
<Tabs.Root>              required, exactly 1            default element: div
  <Tabs.List>            optional, 0..n (usually 1)     default element: div   role="tablist"
    <Tabs.Tab>           optional, 0..n                 default element: button role="tab"
    <Tabs.Indicator>     optional, 0..n                 default element: span  role="presentation"
  <Tabs.Panel>           optional, 0..n                 default element: div   role="tabpanel"
```

Tab and Indicator must be descendants of a List (they read List-provided shared state); List and Panel must be descendants of a Root. Panels may be placed anywhere under the Root (not inside the List). Parts locate their owners through ancestor-provided shared state, not DOM adjacency, so wrappers between parts are allowed. Nested Tabs (a Root inside a Panel of another Root) are independent.

- **TAB-STR-01** — MUST. `Tabs.Root` renders a `<div>` by default, owns the active value and the activation direction, provides them to every descendant part, and attaches no event handlers of its own. It renders its children as given (including `null` / no children, which yields an empty element).
  Source: `tabs/root/TabsRoot.tsx:337-348`. Test: [L2] `TabsRoot.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`), "should accept a null child", "should support empty children".
- **TAB-STR-02** — MUST. `Tabs.List` renders a `<div>` by default with `role="tablist"` and is the composite roving-focus root for the Tabs inside it ([composite-and-list-navigation](../architecture/composite-and-list-navigation.md), CMP-STR-01). Rendering it outside a `Tabs.Root` fails with TAB-EDGE-01.
  Source: `tabs/list/TabsList.tsx:32,89-92,109-128`. Test: [L2] `TabsList.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **TAB-STR-03** — MUST. `Tabs.Tab` renders a `<button>` by default with `role="tab"`, is a composite item of the nearest List, and MUST be a descendant of a `Tabs.List`: rendering it inside a Root but outside a List fails with TAB-EDGE-02; outside a Root it fails with TAB-EDGE-01.
  Source: `tabs/tab/TabsTab.tsx:42-53,186-208`; `tabs/list/TabsListContext.ts:13-22`. Test: [L2] `TabsTab.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`), "throws a descriptive error when rendered outside <Tabs.List>".
- **TAB-STR-04** — MUST. `Tabs.Indicator` renders a `<span>` by default and MUST be a descendant of a `Tabs.List` (it needs the List element and the List's resize notifications). Any number of Indicators may exist in one List and all of them update (TAB-DOM-33).
  Source: `tabs/indicator/TabsIndicator.tsx:47-50,146-161`. Test: [L2] `TabsIndicator.test.tsx` describeConformance (`refInstanceof: HTMLSpanElement`, `testRenderPropWith: 'div'`), "updates all indicators when a different tab resizes" (L4 for the resize-notification clause).
- **TAB-STR-05** — MUST. `Tabs.Panel` renders a `<div>` by default with `role="tabpanel"` and MUST be a descendant of a `Tabs.Root` (TAB-EDGE-01 otherwise); it need not be inside the List. Several Panels may share a `value`; the last one to register owns the Tab's `aria-controls` reference (TAB-STA-21).
  Source: `tabs/panel/TabsPanel.tsx:37-43,64-81`. Test: [L2] `TabsPanel.test.tsx` describeConformance (`keepMounted`, `refInstanceof: HTMLDivElement`), "throws a descriptive error when rendered outside <Tabs.Root>", "keeps the surviving registration when a shadowed panel unmounts".
- **TAB-STR-06** — MUST. A `Tabs.Root` nested inside another Root's Panel is independent: each Root wires only its own Tabs and Panels (`aria-controls`/`aria-labelledby`), arrow keys inside the nested List move only within that List, activating a nested Tab does not change the outer selection, and selecting another outer Tab unmounts the nested Root with its Panel.
  Source: `tabs/root/TabsRootContext.ts:43-54` (nearest-ancestor lookup); `tabs/list/TabsList.tsx:111-126`. Test: [L2] `TabsRoot.test.tsx` "keeps a nested root independent from the one hosting its panel" (L4 for the arrow-key clause).

## 3. Public API

### 3.1 `Tabs.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tabs.Root.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Tabs.Tab.Value \| undefined` (`any \| null`) | `undefined` (uncontrolled) | The active Tab's value. `null` means no Tab is active. Supplying a non-`undefined` value on first render makes the value consumer-owned. | TAB-API-01 |
| `defaultValue` | `Tabs.Tab.Value \| undefined` | `0` | Initial value when `value` is not supplied. An explicitly supplied value (including `null`) is "explicit"; omitted or `undefined` is "implicit" (TAB-API-02). | TAB-API-02 |
| `onValueChange` | `(value: Tabs.Tab.Value, eventDetails: Tabs.Root.ChangeEventDetails) => void` | `undefined` | Called for user activations (cancelable) and automatic selections (not cancelable); §7. | TAB-API-03 |
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Layout flow direction; selects the arrow keys, `aria-orientation`, and the axis of the activation direction. | TAB-API-04 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<div>` attribute | | — | Forwarded to the rendered element. | — |

**Consumed (not forwarded to the element):** `value`, `defaultValue`, `onValueChange`, `orientation`, `className`, `style`, `render`.

- **TAB-API-01** — MUST. `value` is the consumer-owned (controlled) active value. When it is not `undefined` on the first render the component never changes the value itself (user activations only call `onValueChange`, TAB-STA-03) and performs no automatic fallback (TAB-STA-13). `null` selects nothing.
  Source: `tabs/root/TabsRoot.tsx:48-55,240-243`; `packages/utils/src/useControlled.ts:41-46,83-90`. Test: [L2] `TabsRoot.test.tsx` "should pass selected prop to children", "does not call onValueChange when a controlled selected tab becomes disabled", "should still honor explicit value prop even if it points to a disabled tab".
- **TAB-API-02** — MUST. `defaultValue` defaults to `0` and is the initial component-owned value; it is read once (later changes are ignored and log TAB-EDGE-04). The Root distinguishes an *explicit* default (`defaultValue` present and not `undefined`, including `null`) from an *implicit* one (omitted or `undefined`): only an implicit default produces the `'initial'` notification (TAB-STA-08) and only an explicit default may keep a disabled Tab selected (TAB-STA-07).
  Source: `tabs/root/TabsRoot.tsx:30,41,229-235`. Test: [L2] `TabsRoot.test.tsx` "should support a function as the default value", "does not call onValueChange on initial render when defaultValue is provided", "does not call onValueChange on initial render when defaultValue is null", "treats defaultValue={undefined} as an implicit default when the first tab is disabled", "continues honoring an initially disabled explicit defaultValue after defaultValue changes".
  Port note 2026-10-27 (ADR 08 Amendments 2026-10-26, chosen by the user 2026-10-26; design point 3 of the Tabs track, settled by the orchestrator 2026-10-26): `DefaultValue` is explicit when supplied, `null` included, read once through `Controlled<TValue>`. With neither `Value` nor `DefaultValue` the Root holds an implicit default that is not a `TValue`: a Tab matches it only when `TValue` is a numeric primitive (or its nullable) and the Tab's value is zero (`-0.0` included, `NaN` not; enums, `char` and `bool` never), and the first commit clears it. Without a match, the first enabled Tab is committed with reason `initial` (TAB-STA-08, with track 1's E2).
- **TAB-API-03** — MUST. `onValueChange` is optional; when absent, user activations still commit (uncontrolled) and automatic selections still happen silently.
  Source: `tabs/root/TabsRoot.tsx:31,115,128`. Test: [L2] `TabsRoot.test.tsx` "should select the second tab when the first one is disabled" (no callback supplied).
- **TAB-API-04** — MUST. `orientation` defaults to `'horizontal'`; it is the `orientation` of the composite root (TAB-KEY-01), the source of `aria-orientation` (TAB-ARIA-02), the `data-orientation` value on every part, and the axis of the activation direction (TAB-STA-15).
  Source: `tabs/root/TabsRoot.tsx:32,83,183-193,332-335`; `tabs/list/TabsList.tsx:90,122`. Test: [L2] `TabsRoot.test.tsx` "adds the proper aria-orientation when vertical", "should set the `data-activation-direction` attribute on the tabs root with orientation=vertical" (L4 for the activation-direction clause).

#### State object (`Tabs.Root.State`)

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | The `orientation` prop. |
| `tabActivationDirection` | `'left' \| 'right' \| 'up' \| 'down' \| 'none'` | Direction from the previously active Tab to the current one (TAB-STA-14..18). |

Source: `tabs/root/TabsRoot.tsx:332-335,411-420`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; `"horizontal"` or `"vertical"` | TAB-DOM-01 |
| `data-activation-direction` | always; `"left" \| "right" \| "up" \| "down" \| "none"` | TAB-DOM-02 |

#### CSS variables

None.

### 3.2 `Tabs.List`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tabs.List.State`, same shape as Root's).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `activateOnFocus` | `boolean` | `false` | When `true`, a Tab becomes active as soon as it receives focus (arrow keys, <kbd>Home</kbd>/<kbd>End</kbd>, programmatic focus, primary-button press). When `false`, focus moves without changing the selection and <kbd>Enter</kbd>/<kbd>Space</kbd>/click activate. | TAB-API-05 |
| `loopFocus` | `boolean` | `true` | Whether arrow-key focus wraps from the last Tab to the first and vice versa. | TAB-API-06 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<div>` attribute / handler | | — | Forwarded; `aria-label`/`aria-labelledby` name the tablist (TAB-ARIA-10); `onKeyDown` chains before the navigation handler (TAB-KEY-09). | — |

**Consumed:** `activateOnFocus`, `loopFocus`, `className`, `style`, `render`.

- **TAB-API-05** — MUST. `activateOnFocus` defaults to `false`. It is read by every Tab's focus handler (TAB-KEY-10, TAB-PTR-05) and has no effect on which keys move focus.
  Source: `tabs/list/TabsList.tsx:24,96`; `tabs/tab/TabsTab.tsx:145-151`. Test: [L4] `TabsRoot.test.tsx` "when `activateOnFocus = true` should call onValueChange if an unactive tab gets focused", "when `activateOnFocus = false` should not call onValueChange if an unactive tab gets focused".
- **TAB-API-06** — MUST. `loopFocus` defaults to `true` and is passed unchanged as the composite root's `loopFocus` (CMP-API-01).
  Source: `tabs/list/TabsList.tsx:26,121`; `internals/composite/root/useCompositeRoot.ts:282-300`. Test: [L4] `TabsList.test.tsx` "does not wrap focus past the first tab when `loopFocus` is false", "does not wrap focus past the last tab when `loopFocus` is false".

#### State object (`Tabs.List.State`)

Identical in shape and value to `Tabs.Root.State` (`orientation`, `tabActivationDirection`). Source: `tabs/list/TabsList.tsx:84-87,131`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always | TAB-DOM-03 |
| `data-activation-direction` | always | TAB-DOM-03 |

#### CSS variables

None.

### 3.3 `Tabs.Tab`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes (e.g. `<a href>` with `nativeButton={false}`). **Accepts `className`/`style` as function of state:** yes (state = `Tabs.Tab.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Tabs.Tab.Value` (`any \| null`) | **required** | Identifies the Tab; compared to the Root's value by strict identity. | TAB-API-07 |
| `disabled` | `boolean` | `false` | Ignores activation; the Tab stays focusable (`aria-disabled`). | TAB-API-08 |
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>`. | TAB-API-09 |
| `id` | `string` | generated | Element id, also referenced by the matching Panel's `aria-labelledby`. Not listed in the API outline (§16 Q6). | TAB-API-10 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; `onClick`/`onFocus`/`onPointerDown`/`onKeyDown` chain with the Tab's own handlers (TAB-PTR-09). | — |

**Consumed:** `value`, `disabled`, `nativeButton`, `id`, `className`, `style`, `render`.

- **TAB-API-07** — MUST. `value` is required and may be any value, including `0`, strings, objects, functions, symbols and regular expressions; "this Tab is active" means `tab.value === root.value` (strict identity; no coercion, no deep equality). There is no index-based implicit value (§16 Q2).
  Source: `tabs/tab/TabsTab.tsx:35,69,213,259`. Test: [L2] `TabsRoot.test.tsx` "should support values of different types", "should support a function as the default value".
- **TAB-API-08** — MUST. `disabled` defaults to `false`. A disabled Tab is not activated by click, focus, <kbd>Enter</kbd> or <kbd>Space</kbd> (TAB-PTR-03, TAB-KEY-12), is excluded from automatic selection (TAB-STA-05/06), is reported as `disabled` in the tab registry, carries `data-disabled`, and remains focusable with arrow keys (TAB-KEY-07).
  Source: `tabs/tab/TabsTab.tsx:33,57,111-115,133,141`. Test: [L4] `TabsRoot.test.tsx` "does not select the clicked disabled tab", "moves focus to a disabled tab without activating it".
- **TAB-API-09** — MUST. `nativeButton` defaults to `true` and is passed as the `native` option of the shared button behavior together with `disabled` and `focusableWhenDisabled: true` (§11). Composite mode is inferred from the List (UBT-STA-01).
  Source: `tabs/tab/TabsTab.tsx:37,111-115`; `internals/use-button/useButton.ts:19,25-34`. Test: [L2] `TabsTab.test.tsx` describeConformance (`button: true`, non-native re-run), "renders as an anchor and toggles selection when `nativeButton` is false", "renders a react-router Link" (L4 for the `<a href>` substitute).
  Port note 2026-10-27 (ADR 13 Amendments 2026-10-22): "renders a react-router Link" is re-cited to an `<a href>` substitute with `NativeButton="false"`, the L4 port title "port: an `<a href>` substitute keeps its href and is activated by Space and by Enter".
- **TAB-API-10** — MUST. The Tab's `id` prop, when supplied, is used verbatim as the element id and as the id published to the Root (so the matching Panel's `aria-labelledby` references it); otherwise a generated `base-ui-…` id is used (RND-DOM-27..29).
  Source: `tabs/tab/TabsTab.tsx:36,55,57,195`; `internals/useBaseUiId.ts:9-11`. Test: [L2] `TabsRoot.test.tsx` "sets the aria-labelledby attribute on tab panels to the corresponding tab id" (explicit ids on two tabs).

#### State object (`Tabs.Tab.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The `disabled` prop. |
| `active` | `boolean` | `value === root value`. |
| `orientation` | `'horizontal' \| 'vertical'` | Root orientation. |
| `tabActivationDirection` | activation direction | Root activation direction (same on every Tab, not only the active one). |

Source: `tabs/tab/TabsTab.tsx:179-184,235-252`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-active` | `active === true`; empty value | TAB-DOM-05 |
| `data-disabled` | `disabled === true`; empty value | TAB-DOM-06 |
| `data-orientation` | always | TAB-DOM-07 |
| `data-activation-direction` | always (Root's value) | TAB-DOM-07 |
| `data-composite-item-active` | `active === true`; empty value (internal marker, not in outline) | TAB-DOM-08 |

#### CSS variables

None.

### 3.4 `Tabs.Indicator`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tabs.Indicator.State`; a consumer `style` is merged over the CSS variables, TAB-DOM-32).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `renderBeforeHydration` | `boolean` | `false` | Emit an inline script after the element in server-rendered HTML that positions the indicator before the client takes over. Port: withdrawn, no parameter (TAB-API-11). | TAB-API-11 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<span>` attribute | | — | Forwarded; a consumer `hidden` overrides the computed one. | — |

**Consumed:** `renderBeforeHydration`, `className`, `style`, `render`.

- **TAB-API-11** — MUST. `renderBeforeHydration` defaults to `false`. When `true`, and only in server-rendered output and during the hydration pass, a `<script>` element (TAB-DOM-34) follows the Indicator element; it is never rendered for client-only mounts and is removed once hydration completes. Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): the port emits no pre-hydration script and has no `RenderBeforeHydration` parameter. Blazor replaces the prerendered subtree at activation (ADR 02 verification item 1), so the script's work would be discarded; the prerendered Indicator stays hidden until its `tabs` handle attaches (TAB-EDGE-09).
  Source: `tabs/indicator/TabsIndicator.tsx:42,167-172`; `internals/PrehydrationScript.tsx:32-49`. Test: [L4] `TabsIndicator.test.tsx` "renders the inline pre-hydration script during server-side rendering", "keeps the script during hydration and removes it afterwards".

#### State object (`Tabs.Indicator.State`)

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | Root orientation. |
| `tabActivationDirection` | activation direction | Root activation direction. |
| `activeTabPosition` | `{ left, right, top, bottom } \| null` | Measured offsets (px numbers) of the active Tab relative to the List (TAB-DOM-24..29); `null` when no active Tab element is measurable. |
| `activeTabSize` | `{ width, height } \| null` | Measured size of the active Tab; `null` likewise. |

Source: `tabs/indicator/TabsIndicator.tsx:122-124,139-144,175-188`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always | TAB-DOM-22 |
| `data-activation-direction` | always | TAB-DOM-22 |
| `hidden` | no measurable active Tab (TAB-DOM-23) | TAB-DOM-23 |

`activeTabPosition` and `activeTabSize` are never projected as data attributes (their mapping returns nothing). Source: `tabs/indicator/TabsIndicator.tsx:19-23`.

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--active-tab-left` | Indicator inline style | `<left>px` | TAB-DOM-24 |
| `--active-tab-right` | Indicator inline style | `<right>px` | TAB-DOM-24 |
| `--active-tab-top` | Indicator inline style | `<top>px` | TAB-DOM-24 |
| `--active-tab-bottom` | Indicator inline style | `<bottom>px` | TAB-DOM-24 |
| `--active-tab-width` | Indicator inline style | `<width>px` | TAB-DOM-24 |
| `--active-tab-height` | Indicator inline style | `<height>px` | TAB-DOM-24 |

### 3.5 `Tabs.Panel`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Tabs.Panel.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Tabs.Tab.Value` | **required** | The Panel is open when `value === root value`. | TAB-API-12 |
| `keepMounted` | `boolean` | `false` | Keep the element in the document while the Panel is closed (hidden with `hidden` + `inert`). | TAB-API-13 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<div>` attribute | | — | Forwarded; a consumer `id`/`hidden`/`tabIndex`/`inert` overrides the Panel's (see §16 Q1 for `id`). | — |

**Consumed:** `value`, `keepMounted`, `className`, `style`, `render`.

- **TAB-API-12** — MUST. `value` is required; the Panel is open exactly when `value === root value` (strict identity).
  Source: `tabs/panel/TabsPanel.tsx:35,49,132`. Test: [L2] `TabsRoot.test.tsx` "should support values of different types" (each panel un-hides when its tab is clicked).
- **TAB-API-13** — MUST. `keepMounted` defaults to `false`. When `false`, the Panel element is absent from the document while the Panel is not mounted (TAB-DOM-12); when `true`, the element stays in the document carrying `hidden` (TAB-DOM-14). It does not change the open state or the transition sequence (TRN-API-01/02).
  Source: `tabs/panel/TabsPanel.tsx:35,104-109`. Test: [L2] `TabsRoot.test.tsx` "syncs aria-controls to the mounted tab panel when keepMounted is false" (absent), "selects the clicked tab" (`keepMounted`, hidden attribute).

#### State object (`Tabs.Panel.State`)

| Field | Type | Meaning |
|---|---|---|
| `hidden` | `boolean` | `!mounted` (TAB-STA-22): `true` while the Panel is closed and its exit has completed. |
| `orientation` | `'horizontal' \| 'vertical'` | Root orientation. |
| `tabActivationDirection` | activation direction | Root activation direction. |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Panel transition status (§9). |

Source: `tabs/panel/TabsPanel.tsx:55-60,117-126`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-hidden` | `hidden === true`; empty value | TAB-DOM-14 |
| `data-index` | always; the Panel's index among the Root's rendered Panels | TAB-DOM-17 |
| `data-orientation` | always | TAB-DOM-18 |
| `data-activation-direction` | always | TAB-DOM-18 |
| `data-starting-style` | `transitionStatus === 'starting'` | TAB-DOM-18 |
| `data-ending-style` | `transitionStatus === 'ending'` | TAB-DOM-18 |

#### CSS variables

None.

### 3.6 Shared types

- `Tabs.Tab.Value` = `any | null`. Source: `tabs/tab/TabsTab.tsx:213`.
- `Tabs.Tab.ActivationDirection` = `'left' | 'right' | 'up' | 'down' | 'none'`. Source: `tabs/tab/TabsTab.tsx:215`.
- `Tabs.Tab.Position` = `{ left: number; right: number; top: number; bottom: number }`; `Tabs.Tab.Size` = `{ width: number; height: number }`. Source: `tabs/tab/TabsTab.tsx:217-227`.
- `Tabs.Tab.Metadata` = `{ disabled: boolean; id: string | undefined; value: Tabs.Tab.Value | undefined }` — what a Tab publishes to the Root's tab registry (plus `index`, TAB-STA-04). Source: `tabs/tab/TabsTab.tsx:229-233`.
- `Tabs.Panel.Metadata` = `{ id?: string; value: Tabs.Tab.Value }` (declared type of the Root's panel registry; the registry stores no metadata in 1.9.0). Source: `tabs/panel/TabsPanel.tsx:112-115`; `tabs/root/TabsRoot.tsx:346`.
- `Tabs.Root.Orientation` = `'horizontal' | 'vertical'`. Source: `tabs/root/TabsRoot.tsx:409`; `internals/types.ts` (`Orientation`).
- `Tabs.Root.ChangeEventReason` = `'none' | 'disabled' | 'missing' | 'initial'`. Source: `tabs/root/TabsRoot.tsx:461-462`; `internals/reason-parts.ts:1,37-39`.
- `Tabs.Root.ChangeEventDetails` = the common change-event-details shape of [event-details-and-reasons](../architecture/event-details-and-reasons.md) (EVD-API-01) specialized to those reasons and extended with `activationDirection: Tabs.Tab.ActivationDirection` (EVD-API-08): `{ reason, event, trigger, cancel(), allowPropagation(), isCanceled, isPropagationAllowed, activationDirection }`. `event` is the native `click` (`MouseEvent`/`PointerEvent`) or `FocusEvent` for `'none'` and a generic `Event` of type `base-ui` for the automatic reasons. Source: `tabs/root/TabsRoot.tsx:463-466`; `internals/createBaseUIEventDetails.ts:126-157`.
- `Tabs.Root.Props/State`, `Tabs.List.Props/State`, `Tabs.Tab.Props/State`, `Tabs.Indicator.Props/State`, `Tabs.Panel.Props/State` as above; flat aliases (`TabsRootProps`, `TabsTabState`, …) and the attribute-name namespaces `TabsRootDataAttributes`, `TabsListDataAttributes`, `TabsTabDataAttributes`, `TabsPanelDataAttributes`, `TabsIndicatorDataAttributes`, `TabsIndicatorCssVariables` are also exported. Source: `tabs/index.ts:1-14`.
- `TransitionStatus` = `'starting' | 'ending' | 'idle' | undefined`. Source: `internals/useTransitionStatus.ts:6`.

## 4. State model

### 4.1 Active value

- **TAB-STA-01** — MUST. The active value is consumer-owned if `value !== undefined` on the first render, otherwise component-owned starting from `defaultValue` (default `0`). The mode is fixed for the lifetime of the instance (switching logs TAB-EDGE-03). A function supplied as `defaultValue` is stored as the value, not invoked.
  Source: `tabs/root/TabsRoot.tsx:48-53`; `packages/utils/src/useControlled.ts:41-46` (UTL-STA-01/02). Test: [L2] `TabsRoot.test.tsx` "should pass selected prop to children", "should support a function as the default value".
- **TAB-STA-02** — MUST. The *active Tab* is the Tab whose `value === root value` (strict identity). The root value may match no Tab (missing) or be `null`; then no Tab is active, every Tab has `aria-selected="false"`, every Panel is closed and the Indicator renders nothing (TAB-DOM-19).
  Source: `tabs/tab/TabsTab.tsx:69`; `tabs/panel/TabsPanel.tsx:49`; `tabs/indicator/TabsIndicator.tsx:163-165`. Test: [L2] `TabsRoot.test.tsx` "does not select any tab when all tabs are disabled", "does not call onValueChange on initial render when defaultValue is null"; `TabsIndicator.test.tsx` "should not render when no tab is active".
  Port note 2026-10-27 (ADR 13 §3): the Indicator clause is C#'s in the port (the part renders no element while nothing is selected), so "should not render when no tab is active", browser-only in Base UI, runs at L2 in `TabsIndicatorTests`. "does not select any tab when all tabs are disabled" runs at L2 on the ordered-ids report of track 1's E2.
- **TAB-STA-03** — MUST. A *user activation* (click, keyboard activation, or focus with `activateOnFocus`) of an inactive, enabled Tab computes the activation direction from the current value to the Tab's value, calls `onValueChange(tabValue, details)` with that direction, and then — unless `details.cancel()` was called — commits the Tab's value when the value is component-owned. When the value is consumer-owned nothing is committed; the value changes only when the consumer renders a new `value`. A Tab's value is committed through a function wrapper so that function-typed values are stored as-is.
  Source: `tabs/root/TabsRoot.tsx:109-124`; `tabs/tab/TabsTab.tsx:123-130`. Test: [L4] `TabsRoot.test.tsx` "does not move an uncontrolled selection when a user-initiated change is canceled", "should update `data-activation-direction` on programmatic change after a controlled parent ignores click"; `TabsList.test.tsx` "sets the aria-selected attribute on the active tab".

### 4.2 Tab registry and derived lookups

Explanatory: the List's composite root publishes, post-commit and pre-paint, an ordered map from each connected Tab element to its metadata (`{ value, disabled, id, index }`) in document order (CMP-STA-01/08/10). The Root keeps the latest published map and derives everything below from it.

- **TAB-STA-04** — MUST. The Root's *tab registry* is the ordered list of currently connected Tabs of its List(s) in document order, each with the Tab's current `value`, `disabled` and `id`. It is updated post-commit, pre-paint whenever a Tab mounts, unmounts, moves, or changes any of those three fields (the metadata object is re-published when any of them changes). Server: relaxed, see §9.
  Source: `tabs/list/TabsList.tsx:124`; `tabs/tab/TabsTab.tsx:57,59-67`; `internals/composite/list/CompositeList.tsx:143-171,188-192`. Test: [L4] `TabsRoot.test.tsx` "calls onValueChange when the selected tab becomes disabled" (disabled change republishes), "calls onValueChange when the selected tab is removed".
- **TAB-STA-05** — MUST. Derived lookups, each scanning the registry in document order and taking the *first* match: (a) *selected tab metadata* = the entry whose `value === root value`; (b) *first enabled value* = the `value` of the first entry whose `disabled` is `false` (`undefined` when none); (c) *tab id for a panel value* = the `id` of the first entry whose `value === panelValue`; (d) *tab element for a value* = the element of the first entry whose `value === value`. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:63-66,170-180,207-214,218-225,351-362`. Test: [L2] `TabsRoot.test.tsx` "should select the third tab when first two tabs are disabled", "does not emit a second change when the fallback resolves to the current value" (first match with duplicate values).

### 4.3 Automatic selection (component-owned value only)

Explanatory: after every commit in which the registry, the value, or the derived lookups changed, an uncontrolled Root re-validates its selection post-commit, pre-paint. The Root tracks three private flags: *initial notification pending* (starts `true` iff the default was implicit), *honor disabled default* (starts `true` iff the default was explicit), and *tabs have registered* (starts `false`). The initial default value is captured once.

- **TAB-STA-06** — MUST. When the registry is non-empty and the selection is *disabled* (selected tab metadata exists with `disabled: true`) or *missing* (root value is not `null` and matches no entry), the Root commits the *fallback value* = first enabled value, or `null` when every Tab is disabled, and notifies `onValueChange(fallback, details)` with the reason of TAB-STA-09 and `activationDirection: 'none'`; the activation direction state is reset to `'none'` in the same commit. The commit happens before the notification; the notification cannot cancel it (TAB-EVT-04). Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:245-261,279-280,296-316`. Test: [L2] `TabsRoot.test.tsx` "should select the second tab when the first one is disabled", "calls onValueChange with null when all tabs are initially disabled", "calls onValueChange when the selected tab becomes disabled", "calls onValueChange when the selected tab is removed", "keeps activation direction none after automatic disabled fallback" (L4 for the activation-direction clause).
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26: `data-activation-direction` is the `tabs` module's from attach; ADR 13 §3; design point 7 of the Tabs track, settled by the orchestrator 2026-10-26): the direction clause runs in `tabs.spec.ts`, and the module's rule at L3 ("port: a change the module did not start is none in an uncontrolled root"). In an uncontrolled Root every change the module did not start is an automatic fallback, so it writes `none`. The fallback itself runs at L2 on the ordered-ids report of track 1's E2 (`TabsAutomaticSelectionTests`), and its L4 titles in `tabs.spec.ts` (automatic selection).
  Port note 2026-10-27 (ADR 08 Amendments 2026-10-26, chosen by the user 2026-10-26): the fallback `null` is "nothing selected". The callback receives `null` for a nullable `TValue` and `default(TValue)` otherwise, and in both cases the Root clears its selection, so no Tab is active even where `default(TValue)` equals a Tab's value.
- **TAB-STA-07** — MUST. An *explicit* `defaultValue` that points at a disabled Tab is kept selected (no fallback, no notification) for as long as the *honor* flag is set. The flag is cleared the first time the Root observes, while the value still equals the initial default, that the selection is not disabled (the Tab became enabled, or no Tab with that value is registered). Once cleared it never returns, so a later re-disable falls back with reason `'disabled'`, and an explicit default that never matches a Tab falls back immediately with reason `'missing'`. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:232-235,282-292`. Test: [L2] `TabsRoot.test.tsx` "should still honor explicit defaultValue even if it points to a disabled tab", "calls onValueChange when an explicit disabled default becomes disabled again", "calls onValueChange when an explicit defaultValue points at a tab that is never present", "continues honoring an initially disabled explicit defaultValue after defaultValue changes".
- **TAB-STA-08** — MUST. With an *implicit* default, exactly one `'initial'` notification is issued when Tabs first register: if the implicit value `0` matches an enabled Tab, `onValueChange(0, { reason: 'initial' })` is called without any commit; otherwise the fallback of TAB-STA-06 is committed and reported with reason `'initial'` (value = first enabled value, or `null`). With an explicit default (including `null`) no initial notification is issued. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:229,294,306-321`. Test: [L2] `TabsRoot.test.tsx` "calls onValueChange when auto-selecting the first tab on mount", "calls onValueChange with the selected value when the implicit default matches a later tab", "calls onValueChange when the implicit first tab is disabled", "calls onValueChange with null when all tabs are initially disabled", "does not call onValueChange on initial render when defaultValue is provided".
  Port note 2026-10-27 (ADR 08 Amendments 2026-10-26, chosen by the user 2026-10-26): the implicit value is TAB-API-02's implicit default, matched by numeric zero only. When it matches an enabled Tab the notification carries that Tab's value; a fallback to `null` is "nothing selected" as in TAB-STA-06's note.
- **TAB-STA-09** — MUST. The reason of an automatic fallback is `'initial'` while the initial notification is still pending; otherwise `'disabled'` when the selection is disabled; otherwise `'missing'`. The pending flag is cleared after the consumer callback returns (or when TAB-STA-10 applies). Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:256-260,306-312`. Test: [L2] `TabsRoot.test.tsx` "calls onValueChange when the implicit first tab is disabled" (`initial`), "calls onValueChange when the selected tab becomes disabled" (`disabled`), "calls onValueChange when the selected tab is removed" (`missing`).
- **TAB-STA-10** — MUST. When the fallback value equals the current value (possible only with duplicate Tab values, where the first registered duplicate is disabled and a later one enabled), nothing is committed and nothing is notified, but the pending initial flag is cleared; the Root therefore never re-emits for an unchanged value. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:297-304`. Test: [L2] `TabsRoot.test.tsx` "does not emit a second change when the fallback resolves to the current value".
- **TAB-STA-11** — MUST. When the registry becomes empty after Tabs had registered, the value is not `null`, and the last known Tab element is no longer connected to the document, the Root commits `null` with reason `'missing'`. If the previous Tabs are still connected (their registrations were torn down by a suspended subtree, not by removal), nothing happens. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:263-277`. Test: [L2] `TabsRoot.test.tsx` "calls onValueChange with null when the selected tab is removed and no tabs remain", "calls onValueChange with null when a populated tab list is replaced by an empty one"; `TabsPanel.test.tsx` "renders a panel that suspends when opened with the boundary outside the root".
  Port note 2026-10-27 (ADR 08 Amendments 2026-10-26, chosen by the user 2026-10-26): the committed `null` is "nothing selected" as in TAB-STA-06's note. The port has no suspension, so an empty registry with the last Tab disconnected is always a removal (§14 "Suspense").
- **TAB-STA-12** — MUST. A `null` selection is never automatically replaced: when an enabled Tab appears (or a disabled one becomes enabled) while the value is `null`, the value stays `null` and no notification is issued.
  Source: `tabs/root/TabsRoot.tsx:280,296` (`selectionIsMissing` requires `value !== null`). Test: [L2] `TabsRoot.test.tsx` "does not emit missing when an enabled tab appears after all tabs were disabled".
- **TAB-STA-13** — MUST. A consumer-owned Root performs none of TAB-STA-06..12: a controlled value that points at a disabled or missing Tab is rendered as-is (the disabled Tab is `aria-selected="true"`; a missing value selects nothing) and `onValueChange` is not called.
  Source: `tabs/root/TabsRoot.tsx:55,240-243`. Test: [L2] `TabsRoot.test.tsx` "does not call onValueChange when a controlled selected tab becomes disabled", "should still honor explicit value prop even if it points to a disabled tab", "keeps a roving focus entry point when a controlled selected tab is removed".

### 4.4 Activation direction

- **TAB-STA-14** — MUST. The Root keeps a *previous value* snapshot and a committed activation direction (initially `'none'`). Whenever the rendered value differs from the snapshot, the direction is recomputed (TAB-STA-15/16) and *committed in the same render batch* that first reflects the new value, so every part (Root, List, Tab, Panel, Indicator) observes the new direction together with the new active state — in particular the first render of a Panel part as open already carries the new direction. The snapshot is then updated to the new value post-commit, pre-paint (except in the case of TAB-STA-17). Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:68-107`. Test: [L2] `TabsRoot.test.tsx` "should set the `data-activation-direction` attribute on the tabs root with orientation=horizontal" (first panel render state), "should update `data-activation-direction` on programmatic value changes with orientation=horizontal"; `TabsTab.test.tsx` "exposes tab activation direction through the render prop" (L4 for the attribute clauses).
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26: `data-activation-direction` is the `tabs` module's from attach; ADR 13 §3): the module writes the attribute on every part in the observer callback of the batch that moves `[data-active]`, a Panel the batch creates included, so the Panel's first open render carries it on both modes; the cited titles run in `tabs.spec.ts`, where a mutation-record witness reads the created Panel. The state clause (what `ClassValue`, `StyleValue` and `RenderFunction` see) is the mirror of ADR 01's row "RND-STA-01 for mirrored fields": its C# reaction to a report runs at L2 ("port: the render function's state mirrors the reported activation direction"), its timing per mode at L4 ("port: the C# mirror of the activation direction follows the attribute the module writes").
- **TAB-STA-15** — MUST. *Position-based direction.* Given the old and new values, both non-`null`, and both resolving to registered Tab elements: with `orientation` `'horizontal'` compare the elements' bounding-rect `left`; with `'vertical'` compare `top`. New position less than old → backward (`'left'` / `'up'`); greater → forward (`'right'` / `'down'`); equal → `'none'`. Positions are visual (viewport coordinates), so under RTL moving to the next Tab in reading order yields `'left'` (TAB-I18N-02).
  Source: `tabs/root/TabsRoot.tsx:374-377,396-406`. Test: [L4] `TabsRoot.test.tsx` "should set the `data-activation-direction` attribute on the tabs root with orientation=horizontal", "…orientation=vertical".
  Port note 2026-10-27 (ADR 13 §3; ADR 04 Amendments 2026-10-26; conventions §3.5): re-tagged from L3, because the cited titles are browser-only (`describe.skipIf(isJSDOM)`); they run in `tabs.spec.ts`. The `tabs` module's comparison runs at L3 with stubbed rects as port titles in `js/tabs.test.ts` ("port: horizontal tabs compare left, vertical tabs compare top, equal positions give none", "port: the direction is physical, so the next tab of an rtl list is to the left"). The module measures the two Tabs when a user activation starts, and otherwise in the observer callback of the batch that moves `[data-active]`, so both Tabs are elements of the applied DOM (§16 Q15).
- **TAB-STA-16** — MUST. The direction is `'none'` when the old or the new value is `null`/`undefined` (clearing the selection, selecting from a cleared state), and after every automatic fallback (TAB-STA-06). When exactly one of the two values has no registered element yet, and the old value is a `number` or `string` of the same type as the new value, the direction is provisionally `newValue > oldValue ? forward : backward` (JavaScript relational comparison); in every other unresolvable case it is `'none'`.
  Source: `tabs/root/TabsRoot.tsx:370-372,379-394`; `tabs/root/TabsRoot.tsx:252-255`. Test: [L2] `TabsRoot.test.tsx` "resets `data-activation-direction` when the selection is cleared and restored", "keeps activation direction none after automatic disabled fallback", "should compute correct direction when adding and selecting a new tab in one controlled update" (L4 for the direction clauses).
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26: `data-activation-direction` is the `tabs` module's from attach; design point 7 of the Tabs track, settled by the orchestrator 2026-10-26; §16 Q9): every clause is the module's, so the cited titles run in `tabs.spec.ts`, with the module's rule at L3 ("port: clearing and restoring the selection is none", "port: a change the module did not start is none in an uncontrolled root", "port: a change the module did not start is positional in a controlled root", "port: a tab added and selected in one batch takes the positional direction at once"). The module sees `[data-active]` move, not the values: the direction is `none` when the old or the new selection is empty, when the change is one it did not start in an uncontrolled Root (an automatic fallback), or when the old active Tab left the document in the same batch; otherwise it is positional. The new Tab is always in the batch it reads, so the provisional value-based guess is never needed. Deviation: a controlled change whose old Tab is removed in the same update reads `none`, where Base UI compares the disconnected element's collapsed rect.
- **TAB-STA-17** — MUST. When a value change is observed while the *new* value has no registered Tab element (a Tab added and selected in the same update), the previous-value snapshot is left unchanged so that the direction is recomputed from element positions (TAB-STA-15) once the new Tab registers; the final rendered direction is the position-based one even when the provisional value-based guess of TAB-STA-16 differed (e.g. out-of-order string values).
  Source: `tabs/root/TabsRoot.tsx:85-96`. Test: [L4] `TabsRoot.test.tsx` "should compute correct direction when adding and selecting a new tab in one controlled update", "should compute correct direction on final render when adding and selecting a new tab in one controlled update with out of order string values".
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26; design point 7 of the Tabs track, settled by the orchestrator 2026-10-26): the `tabs` module reads the applied batch, in which the new Tab already exists, so the snapshot rule is not needed: the first direction written is the position-based one (L4, both titles, with a mutation-record witness of the created Panel). Inexact case: a controlled change whose old Tab leaves the document in the same batch writes `none`, where Base UI compares the disconnected element's zero rect and usually yields `right` or `down`.
- **TAB-STA-18** — MUST. A user activation that is canceled, or that a controlled consumer ignores, leaves the snapshot and the committed direction unchanged (still `'none'` if nothing had changed), so a later programmatic change computes its direction from the actual previous value.
  Source: `tabs/root/TabsRoot.tsx:82,117-119`. Test: [L4] `TabsRoot.test.tsx` "should update `data-activation-direction` on programmatic change after a canceled click", "…after a controlled parent ignores click".

### 4.5 Roving highlight

- **TAB-STA-19** — MUST. The List owns the composite *highlighted index* (the Tab that holds the single tab stop), initially `0`: it passes its own `highlightedIndex` state and that state's setter as `onHighlightedIndexChange` to the composite root, so the index is consumer-owned from the composite mechanism's point of view (CMP-STA-22); the composite mechanism reports every change through that callback (CMP-EVT-01) and the List renders what it stores. Each Tab reads the same `highlightedIndex`/`onHighlightedIndexChange` pair from the composite root's ancestor-provided shared state and uses the callback to pull the tab stop to the active Tab (TAB-FOC-04); the active-item marker (TAB-DOM-08) alone does not move the tab stop after the first registry publication. Its evolution is specified in §6.3.
  Source: `tabs/list/TabsList.tsx:34,119,123`; `tabs/tab/TabsTab.tsx:53,83-109`; see CMP-STA-22, CMP-EVT-01. Test: [L4] `TabsRoot.test.tsx` "should allow to focus first tab when there are no active tabs".
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's `tabindex` from attach; settled by the orchestrator 2026-10-25; ADR 13 §3; conventions §3.5): re-tagged from L2, because the tab stop is the module's after attach, which only a browser observes; the titles run in `tabs.spec.ts` with recorded `tabindex` moves. The pull of the tab stop to the active Tab (TAB-FOC-04) is track 1's E5, the `composite` module's follow of the active marker (`FollowActiveItem`), asserted in `tabs.spec.ts` group E.

### 4.6 Part state objects

- **TAB-STA-20** — MUST. The state objects exposed to `className`/`style`/`render` contain exactly the fields listed in §3 for each part: Root and List `{ orientation, tabActivationDirection }`; Tab `{ disabled, active, orientation, tabActivationDirection }`; Indicator `{ orientation, activeTabPosition, activeTabSize, tabActivationDirection }`; Panel `{ hidden, orientation, tabActivationDirection, transitionStatus }`. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:332-335`; `tabs/list/TabsList.tsx:84-87`; `tabs/tab/TabsTab.tsx:179-184`; `tabs/indicator/TabsIndicator.tsx:139-144`; `tabs/panel/TabsPanel.tsx:55-60`. Test: [L2] `TabsTab.test.tsx` "exposes tab activation direction through the render prop"; `TabsIndicator.test.tsx` "exposes null active tab state when the selected value has no matching tab"; `TabsRoot.test.tsx` activation-direction tests (Panel render state) (L4 for the values the modules report).
  Port note 2026-10-27 (ADR 04 §2 and Amendments 2026-10-26; ADR 13 §3; design point 2 of the Tabs track, settled by the orchestrator 2026-10-26): `TabActivationDirection` on the five records, the Indicator's `ActiveTabPosition` (`TabsTabPosition`) and `ActiveTabSize` (`TabsTabSize`), and the Panel's `TransitionStatus` are `[MirroredField]`: the modules write the attributes and report the values, which C# mirrors (ADR 01's row "RND-STA-01 for mirrored fields"). The C# reaction to each report runs at L2 through the twin contract ("port: the render function's state mirrors the reported activation direction", "port: a measurement report reaches the render function's state once and renders only the indicator", "port: the mirrored transition status reaches the panel's state"); the reports themselves at L3 and L4.

### 4.7 Panel registry and Panel state

- **TAB-STA-21** — MUST. The Root keeps a *mounted-panel registry* mapping a Panel value to the id of the Panel element currently rendered for it. A Panel registers (value → its id) post-commit, pre-paint whenever it is rendered and either open-or-exiting (mounted) or `keepMounted`; it unregisters when it stops being rendered or becomes hidden without `keepMounted`. A later registration for the same value replaces the earlier; an unregistration removes the entry only if the entry still holds that Panel's id (a shadowed Panel unmounting leaves the owner's registration intact).
  Source: `tabs/root/TabsRoot.tsx:43-46,137-167`; `tabs/panel/TabsPanel.tsx:93-102`. Test: [L2] `TabsPanel.test.tsx` "keeps the surviving registration when a shadowed panel unmounts"; `TabsRoot.test.tsx` "syncs aria-controls to the mounted tab panel when keepMounted is false", "cleans and replaces panel registrations in Strict Mode".
  Port note 2026-10-27 (ADR 06 Amendments 2026-10-18 rule 2; ADR 13 §3): "cleans and replaces panel registrations in Strict Mode" tests React's double invocation and is restated as twin idempotency and keyed replacement: L2 "port: replaces and cleans panel registrations when a keyed panel is replaced, removed and remounted", and the module's repeated attach at L3 ("port: a repeated attach of the same element returns the live handle, and dispose is idempotent"). The registry key compares values as SameValueZero (`Equality.Is` or `Equality.StrictEquals`), so a `NaN` Panel still registers.
- **TAB-STA-22** — MUST. Panel `open = value === root value`; `mounted` and `transitionStatus` follow the default transition-status machine of [transitions-and-mounting](../architecture/transitions-and-mounting.md) (TRN-ALG-01 with idle state disabled, ending not deferred, no initial-open animation); `hidden = !mounted`. The Panel unmounts itself (sets `mounted` to `false`) when its exit animations finish while still closed (§9). Server: relaxed, see §9.
  Source: `tabs/panel/TabsPanel.tsx:49-51,83-91`; `internals/useTransitionStatus.ts:17-42`. Test: [L4] `TabsPanel.test.tsx` "applies data-ending-style before unmount", "triggers enter animation via data-starting-style when mounting".
  Port note 2026-10-27 (ADR 01 Amendments 2026-10-21, the `openSelector` row; design point 1 of the Tabs track, settled by the orchestrator 2026-10-26): the Panel's `transitions` twin reads the open state from the applied batch through `OpenSelector = ":not([inert])"`, an attribute the Panel renders while closed. A consumer `inert` overrides the part's (RND-DOM-10), and the module then reads the Panel as closed while C# shows it open; the XML documentation of `TabsPanel` says so.

## 5. Rendered DOM and ARIA

All state data attributes are projected per [rendering-and-composition](../architecture/rendering-and-composition.md) §4.4 (RND-DOM-01..10) with one custom mapping shared by every part: `tabActivationDirection` → `data-activation-direction="<value>"` (always emitted, including `"none"`). Every other field uses the default projection (`true` → empty attribute, strings → their value, `false`/`null`/`undefined` → nothing). Consumer attributes with the same name as a part-supplied attribute override it (RND-DOM-10/19).

### 5.1 Root element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-orientation` | `"horizontal"` \| `"vertical"`, always | TAB-DOM-01 | C# |
| `data-activation-direction` | `"left"` \| `"right"` \| `"up"` \| `"down"` \| `"none"`, always | TAB-DOM-02 | JS |

- **TAB-DOM-01** — MUST. The Root element has `data-orientation` equal to the effective `orientation` prop.
  Source: `tabs/root/TabsRoot.tsx:332-342`; `internals/getStateAttributesProps.ts:26-27`. Test: none directly (the vertical activation-direction tests render `orientation="vertical"`).
- **TAB-DOM-02** — MUST. The Root element always has `data-activation-direction`, whose value is the current activation direction (`"none"` initially and after automatic fallbacks).
  Source: `tabs/root/stateAttributesMapping.ts:5-9`; `tabs/root/TabsRootDataAttributes.ts:5`. Test: [L2] `TabsRoot.test.tsx` "should set the `data-activation-direction` attribute on the tabs root with orientation=horizontal", "resets `data-activation-direction` when the selection is cleared and restored" (L4 for every value after attach).
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; the handover at attach of ADR 04 Amendments 2026-10-18; conventions §3.5): the `Writer` cell of `data-activation-direction` reads `JS` on §5.1–§5.5. The `tabs` module's List handle writes the attribute on the Root, the List, every Tab, every Panel and every Indicator from attach, in the `MutationObserver` callback of the batch that moves `[data-active]` (pre-paint on both modes), including on a Panel element the same batch creates. C# renders the value its part holds when the element is created, once per element, and mirrors `TabActivationDirection` in the five state records (`[MirroredField]`; ADR 01's row "RND-STA-01 for mirrored fields"). §5.2 gains its table for the same reason.
  Port note 2026-10-27 (ADR 13 §3): the value C# renders when the element is created runs at L2 ("port: the root renders its orientation and the initial activation direction, and no id or handler"); every later value is the module's, so the cited titles run in `tabs.spec.ts`.

### 5.2 List element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-orientation` | the Root's orientation, always | TAB-DOM-03 | C# |
| `data-activation-direction` | the Root's activation direction, always | TAB-DOM-03 | JS |

- **TAB-DOM-03** — MUST. The List element has `data-orientation` and `data-activation-direction` with the Root's values (TAB-DOM-01/02).
  Source: `tabs/list/TabsList.tsx:84-87,118`; `tabs/list/TabsListDataAttributes.ts:5,10`. Test: none.
  Port note 2026-10-27: the table above was added for the `Writer` cell of `data-activation-direction` (TAB-DOM-02's note).

The List element carries the composite root's `keydown` and `focus` handlers (CMP-KEY-01, CMP-KEY-17) and the ARIA attributes of §5.6; it carries no `tabindex` of its own.

### 5.3 Tab element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | consumer `id` or generated `base-ui-…` | TAB-DOM-04 | C# |
| `data-active` | active; empty value | TAB-DOM-05 | C# |
| `data-disabled` | disabled; empty value | TAB-DOM-06 | C# |
| `data-orientation` | always | TAB-DOM-07 | C# |
| `data-activation-direction` | always | TAB-DOM-07 | JS |
| `data-composite-item-active` | active; empty value | TAB-DOM-08 | C# |
| `tabindex` | `"0"` on the highlighted Tab, `"-1"` on every other | TAB-DOM-09 | C# → JS |
| `type` | `"button"` (native mode only; consumer `type` overrides) | TAB-DOM-10 | C# |
| `disabled` | never emitted by the part | TAB-DOM-10 | C# |
| `aria-disabled` | native: `"true"`/`"false"` always; non-native: `"true"` only while disabled | TAB-ARIA-06 | C# |

- **TAB-DOM-04** — MUST. The Tab element always has an `id`: the consumer's `id` prop verbatim, else a generated unique id prefixed `base-ui-` that is stable for the Tab's lifetime and identical between server-rendered markup and the first client render (RND-DOM-27..29).
  Source: `tabs/tab/TabsTab.tsx:55,195`; `internals/useBaseUiId.ts:9-11`; `packages/utils/src/useId.ts:42-52`. Test: [L2] `TabsRoot.test.tsx` "sets aria-controls on the first tab when no value is provided" (generated ids referenced by panels).
- **TAB-DOM-05** — MUST. The Tab element has `data-active=""` while it is the active Tab and no `data-active` attribute otherwise.
  Source: `tabs/tab/TabsTab.tsx:179-184`; `tabs/tab/TabsTabDataAttributes.ts:18`. Test: none directly (asserted through `aria-selected`; the pre-hydration script relies on it, TAB-DOM-35). Docs: CHANGELOG.md v1.0.0-beta.5 "Fix selected/active state naming consistency" (renamed from `data-selected`).
- **TAB-DOM-06** — MUST. The Tab element has `data-disabled=""` while its `disabled` prop is `true` and no `data-disabled` attribute otherwise.
  Source: `tabs/tab/TabsTab.tsx:180`; `tabs/tab/TabsTabDataAttributes.ts:14`. Test: none.
- **TAB-DOM-07** — MUST. Every Tab element (active or not) has `data-orientation` and `data-activation-direction` with the Root's current values; the direction attribute on a Tab therefore changes on every selection change, not only on the newly active Tab.
  Source: `tabs/tab/TabsTab.tsx:182-183,207`; `tabs/tab/TabsTabDataAttributes.ts:5,10`. Test: [L2] `TabsRoot.test.tsx` "should update `data-activation-direction` on programmatic value changes with orientation=horizontal" (`tabs[0]` has `"none"`, then `tabs[1]` has `"right"`, then `tabs[0]` has `"left"`) (L4 for the `data-activation-direction` clause).
  Port note 2026-10-27: the row is split so that `data-activation-direction` reads `JS` (TAB-DOM-02's note). The cited title runs in `tabs.spec.ts`; `data-orientation` and the value rendered at creation run at L2 ("port: every tab renders tabindex -1, the active marker and the handover direction").
- **TAB-DOM-08** — MUST. The active Tab element carries the composite active-item marker `data-composite-item-active=""` (CMP-DOM-02) and inactive Tabs do not; the composite mechanism reads it to choose the initial tab stop (TAB-FOC-02) and the fallback tab stop (TAB-FOC-08).
  Source: `tabs/tab/TabsTab.tsx:199`; `internals/composite/constants.ts:1`; `internals/composite/root/useCompositeRoot.ts:139-150,345-365`. Test: [L2] `TabsRoot.test.tsx` "puts the selected child in tab order"; `TabsList.test.tsx` "moves the tab stop off a hidden successor when the highlighted tab is removed" (L4 for the tab-stop clause).
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22; settled by the orchestrator 2026-10-25; ADR 13 §3): the marker is C#'s and runs at L2 ("port: every tab renders tabindex -1, the active marker and the handover direction"); the tab stop it chooses is the `composite` module's, so that clause's titles run in `tabs.spec.ts`.
- **TAB-DOM-09** — MUST. Each Tab renders `tabindex="0"` when its composite index equals the List's highlighted index and `tabindex="-1"` otherwise (CMP-DOM-01); the button mechanism emits no `tabindex` in composite mode (UBT-DOM-05), and this holds for disabled Tabs too (they are focusable when disabled). A consumer `tabIndex` overrides.
  Source: `internals/composite/item/useCompositeItem.ts:19,25`; `tabs/tab/TabsTab.tsx:189-190`; `utils/useFocusableWhenDisabled.ts:30-36`. Test: [L4] `TabsRoot.test.tsx` "puts the selected child in tab order", "does not set tabIndex=0 on disabled tabs when they are programmatically selected"; `TabsTab.test.tsx` "settles when the rendered component recreates its merged ref".
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22, the composite row, which names the items of Tabs; conventions §3.5): the `Writer` cell reads `C# → JS`. C# renders `tabindex="-1"` on every Tab on every render (CMP-STA-15) and the `composite` module writes the roving value from attach; a consumer `tabindex` wins and travels in the registration, which the module never writes over.
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's `tabindex` from attach; settled by the orchestrator 2026-10-25; ADR 13 §3; conventions §3.5): re-tagged from L2, because the tab stop is the module's after attach, which only a browser observes; the titles run in `tabs.spec.ts` with recorded `tabindex` moves. "puts the selected child in tab order" needs the tab stop to follow a programmatic selection (TAB-FOC-04), track 1's E5. "settles when the rendered component recreates its merged ref" is restated under TAB-EDGE-11.
- **TAB-DOM-10** — MUST. In native mode (`nativeButton` `true`) the Tab element has `type="button"` and **never** the `disabled` attribute, even while disabled (the Tab is focusable when disabled: UBT-DOM-07). In non-native mode it has no `type` and no `disabled`; the button mechanism's `role="button"` is overridden by `role="tab"` (TAB-ARIA-03). The dev-mode mode-mismatch errors of UBT-EDGE-01/02 apply (TAB-EDGE-05).
  Source: `tabs/tab/TabsTab.tsx:111-115,186-206`; `internals/use-button/useButton.ts:226-229`; `utils/useFocusableWhenDisabled.ts:45-47`. Test: [L2] `TabsTab.test.tsx` "does not activate a disabled tab that is pressed and focused" (disabled tab receives focus); `TabsRoot.test.tsx` "moves focus to a disabled tab without activating it".
- **TAB-DOM-11** — MUST. When the consumer substitutes a `<button>` element that itself carries the `disabled` attribute while the Tab's `disabled` prop is `false`, the attribute is left in place (the element is natively disabled, unfocusable, and skipped by arrow navigation per TAB-KEY-07). When the Tab's `disabled` prop is `true` and the substituted `<button>` is natively disabled, the composite correction of UBT-DOM-10 removes the DOM `disabled` so the Tab stays focusable.
  Source: `internals/use-button/useButton.ts:72-89,234-237`. Test: [L2] `TabsList.test.tsx` "skips a natively disabled tab in a single keypress" (L4 for the arrow-navigation clause).

### 5.4 Panel element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (element present) | `keepMounted \|\| mounted` | TAB-DOM-12 | C# |
| `id` | generated `base-ui-…` (consumer `id` overrides the attribute only, §16 Q1) | TAB-DOM-13 | C# |
| `hidden` | `!mounted` (boolean attribute) | TAB-DOM-14 | C# |
| `data-hidden` | `!mounted`; empty value | TAB-DOM-14 | C# |
| `inert` | `!open` (closed, including during the exit transition) | TAB-DOM-15 | C# |
| `tabindex` | `"0"` while open, `"-1"` while closed | TAB-DOM-16 | C# |
| `data-index` | index among the Root's rendered Panels | TAB-DOM-17 | C# |
| `data-orientation` | always | TAB-DOM-18 | C# |
| `data-activation-direction` | always | TAB-DOM-18 | JS |
| `data-starting-style` / `data-ending-style` | transition status `'starting'` / `'ending'` | TAB-DOM-18 | JS |

- **TAB-DOM-12** — MUST. The Panel element is rendered only when `keepMounted || mounted` (TRN-ANI-01); otherwise the part renders nothing (and holds no registration, TAB-STA-21). Consequently without `keepMounted` only the open Panel (plus any Panel still animating out) exists in the document.
  Source: `tabs/panel/TabsPanel.tsx:104-109`. Test: [L2] `TabsRoot.test.tsx` "syncs aria-controls to the mounted tab panel when keepMounted is false" (`getAllByRole('tabpanel')` has one element); `TabsPanel.test.tsx` "triggers enter animation via data-starting-style when mounting" (`queryByTestId` null before click) (L4 for the animating-out clause).
- **TAB-DOM-13** — MUST. The Panel element has a generated `base-ui-…` id (RND-DOM-27..29); this generated id is what the Root's panel registry stores and what the matching Tab's `aria-controls` references (TAB-ARIA-05).
  Source: `tabs/panel/TabsPanel.tsx:45,71,101`. Test: [L2] `TabsRoot.test.tsx` "sets the aria-controls attribute on tabs to the corresponding tab panel id".
- **TAB-DOM-14** — MUST. While the Panel is not mounted (closed and exit complete) and still rendered because of `keepMounted`, the element has the boolean `hidden` attribute and `data-hidden=""`; while mounted (open, or closed and animating out) it has neither. A consumer `hidden` prop replaces the component's value.
  Source: `tabs/panel/TabsPanel.tsx:51,56,70`. Test: [L2] `TabsRoot.test.tsx` "selects the clicked tab", "does not select any tab when all tabs are disabled", "calls onValueChange when the selected tab becomes disabled with keepMounted panels".
- **TAB-DOM-15** — MUST. The Panel element has the `inert` attribute (present, empty value) whenever it is not open — from the closing commit onward, including during the exit transition — and no `inert` attribute while open (UTL-DOM-16 for the attribute form).
  Source: `tabs/panel/TabsPanel.tsx:74`; `packages/utils/src/inertValue.ts:3-9`. Test: none.
- **TAB-DOM-16** — MUST. The Panel element has `tabindex="0"` while open and `tabindex="-1"` while closed, so an open Panel is a sequential tab stop after the List. A consumer `tabIndex` overrides.
  Source: `tabs/panel/TabsPanel.tsx:73`. Test: none.
  Port note 2026-10-27 (design point 1 of the Tabs track, settled by the orchestrator 2026-10-26; ADR 01 Amendments 2026-10-21): the Panel's `transitions` twin reads the open state from the `inert` the part renders while closed (`OpenSelector = ":not([inert])"`). A consumer `inert` overrides the part's (RND-DOM-10) and then also tells the module the Panel is closed while C# shows it open; there is no guard, and the XML documentation of `TabsPanel` says so.
- **TAB-DOM-17** — MUST. The Panel element has `data-index="<n>"` where `n` is the Panel's index in document order among the *currently rendered* Panel elements of the same Root (Panels that are not rendered, TAB-DOM-12, do not count). Before the first registry publication (server-rendered markup, first client render) the value is `"-1"`; it is corrected post-commit, pre-paint, so the first painted frame shows the real index (CMP-STA-15). Server: relaxed, see §9.
  Source: `tabs/panel/TabsPanel.tsx:47,76`; `tabs/root/TabsRoot.tsx:43,346`; `internals/composite/list/useCompositeListItem.ts:42-55,84-96`. Test: [L2] `TabsPanel.test.tsx` "sets the panel index data attribute".
- **TAB-DOM-18** — MUST. The Panel element has `data-orientation` and `data-activation-direction` with the Root's current values (so a Panel animating out carries the direction of the change that closed it), `data-starting-style=""` while its transition status is `'starting'` and `data-ending-style=""` while it is `'ending'` (RND-DOM-11, TRN-DOM-01..03).
  Source: `tabs/panel/TabsPanel.tsx:20-23,55-60,80`; `tabs/panel/TabsPanelDataAttributes.ts:11,16,24,28`. Test: [L2] `TabsPanel.test.tsx` "triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount"; `TabsRoot.test.tsx` activation-direction tests (Panel render state) (L4 for the `data-activation-direction`, `data-starting-style` and `data-ending-style` clauses). Docs: `components/tabs/page.mdx` §"Animated panels"; CHANGELOG.md v1.1.0 "Add transition attributes to `<Tabs.Panel>` part".
  Port note 2026-10-27: the row is split so that `data-activation-direction` reads `JS` (TAB-DOM-02's note); `data-starting-style` and `data-ending-style` stay `JS` (the `transitions` module, mirrored in `TabsPanelState.TransitionStatus`; C# renders `data-starting-style` on an element it creates open, under the handover). The module-written clauses run in `tabs.spec.ts` (groups F and G).

### 5.5 Indicator element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (part renders nothing) | root value is `null`/`undefined` | TAB-DOM-19 | C# |
| `role` | `"presentation"` | TAB-ARIA-09 | C# |
| `hidden` | no active Tab element, or its measured width or height is `0` | TAB-DOM-23 | JS |
| `style` | the six `--active-tab-*` variables while an active Tab element exists | TAB-DOM-24 | JS |
| `data-orientation` | always | TAB-DOM-22 | C# |
| `data-activation-direction` | always | TAB-DOM-22 | JS |

- **TAB-DOM-19** — MUST. When the root value is `null` (or `undefined`), the Indicator renders no element at all (and no pre-hydration script); it reappears when a non-`null` value is set.
  Source: `tabs/indicator/TabsIndicator.tsx:163-165`. Test: [L2] `TabsIndicator.test.tsx` "should not render when no tab is active"; `TabsRoot.test.tsx` "resets `data-activation-direction` when the selection is cleared and restored".
  Port note 2026-10-27 (ADR 13 §3): C#'s in the port. "should not render when no tab is active" runs at L2; "resets `data-activation-direction` when the selection is cleared and restored", browser-only, runs in `tabs.spec.ts`, where it also asserts the Indicator's removal and return.
- **TAB-DOM-20** — MUST. When the root value is not `null` but the List element is not yet available or no registered Tab has that value, the Indicator element is rendered with the `hidden` attribute, no `--active-tab-*` variables, and `activeTabPosition`/`activeTabSize` both `null` in its state.
  Source: `tabs/indicator/TabsIndicator.tsx:65-71,122-137,153`. Test: [L4] `TabsIndicator.test.tsx` "exposes null active tab state when the selected value has no matching tab".
- **TAB-DOM-21** — MUST. The Indicator measures the active Tab *on every render of the part* (not in a deferred phase). Its first render happens before the List element is available, so a freshly mounted Indicator renders hidden once and is re-rendered, post-commit and pre-paint of the same mount, with its measured variables; thereafter it re-renders (and re-measures) on every change of the root value, of the tab registry, of the activation direction, and on every resize notification (TAB-DOM-30).
  Source: `tabs/indicator/TabsIndicator.tsx:52-56,67-120`; `tabs/list/TabsList.tsx:35,116`. Test: [L4] `TabsIndicator.test.tsx` "exposes null active tab state when the selected value has no matching tab" (`indicatorStates.length > 1`), "should update the position and movement variables when the active tab changes".
- **TAB-DOM-22** — MUST. The Indicator element has `data-orientation` and `data-activation-direction` with the Root's values; `activeTabPosition` and `activeTabSize` produce no data attribute.
  Source: `tabs/indicator/TabsIndicator.tsx:19-23,139-144,160`; `tabs/indicator/TabsIndicatorDataAttributes.ts:5,10`. Test: none.
  Port note 2026-10-27: the row is split so that `data-activation-direction` reads `JS` (TAB-DOM-02's note).
- **TAB-DOM-23** — MUST. The Indicator element has the boolean `hidden` attribute unless an active Tab element was found **and** its measured width and height (TAB-DOM-25) are both greater than `0`; "measured" refers to CSS/offset dimensions, not the bounding rect, so a Tab inside a `scale(0)` ancestor still counts as displayable. A consumer `hidden` prop replaces this value.
  Source: `tabs/indicator/TabsIndicator.tsx:137,153`. Test: [L4] `TabsIndicator.test.tsx` "overlays the active tab when an ancestor has a 2D rotation" (not hidden), "falls back to offset positions when the tab list is scaled to zero" (not hidden), "exposes null active tab state when the selected value has no matching tab" (hidden).
  Port note 2026-10-27 (ADR 04 §2's `Tabs.Indicator` row and Amendments 2026-10-18 and 2026-10-26): the `Writer` cell stays `JS`. C# renders the initial `hidden` (a consumer's value, else `hidden`) under the handover, in prerender and the first interactive render, and freezes it from attach; the Indicator's `tabs` handle writes it from then on and never writes over a consumer's `hidden`, which travels as `TabsOptions.Hidden`.
- **TAB-DOM-24** — MUST. While an active Tab element exists, the Indicator's inline style defines exactly `--active-tab-left`, `--active-tab-right`, `--active-tab-top`, `--active-tab-bottom`, `--active-tab-width`, `--active-tab-height`, each as `<number>px` (unrounded, sub-pixel values allowed) with the numbers of TAB-DOM-25..29; otherwise none of them is set. The same numbers are exposed as `activeTabPosition` / `activeTabSize` in the state. Server: relaxed, see §9.
  Source: `tabs/indicator/TabsIndicator.tsx:122-135`; `tabs/indicator/TabsIndicatorCssVars.ts:5-30`. Test: [L4] `TabsIndicator.test.tsx` "should set CSS variables corresponding to the active tab".
  Port note 2026-10-27 (ADR 04 §2 and Amendments 2026-10-18): the `Writer` cell stays `JS`. C# renders the consumer's merged style under the handover and freezes it from attach; the Indicator's `tabs` handle composes the element's whole `style` from the consumer's string (`TwinOptions.Style`) and the six variables (TAB-DOM-32), and reports the measurement once per change, which C# mirrors in `ActiveTabPosition` and `ActiveTabSize` (ADR 01's row "RND-STA-01 for mirrored fields"; design point 2 of the Tabs track, settled by the orchestrator 2026-10-26).
- **TAB-DOM-25** — MUST. *Size.* `width`/`height` are the active Tab's CSS dimensions per UTL-DOM-23: the computed `width`/`height` unless their rounded values differ from `offsetWidth`/`offsetHeight`, in which case the offset values are used. They are not affected by ancestor transforms.
  Source: `tabs/indicator/TabsIndicator.tsx:73,115-116`; `utils/getCssDimensions.ts:5-25`. Test: [L4] `TabsIndicator.test.tsx` "falls back to offset positions when the tab list is scaled to zero" (`--active-tab-width` is `100px`), "should set CSS variables corresponding to the active tab". Docs: CHANGELOG.md v1.0.0-beta.3 "Fix tab size rounding".
- **TAB-DOM-26** — MUST. *Layout offset* (transform-immune baseline). `layoutLeft`/`layoutTop` = (cumulative offset of the Tab) − (cumulative offset of the List) − List `clientLeft`/`clientTop`, where the cumulative offset of an element sums, up the `offsetParent` chain, each element's `offsetLeft`/`offsetTop` plus its `offsetParent`'s `clientLeft`/`clientTop`. Then, walking from the Tab's parent up to (but excluding) the List and stopping at the document root (`html`/`body`/document), subtract each intermediate element's `scrollLeft`/`scrollTop`; the List's own scroll is **not** subtracted. The walk crosses shadow boundaries (slot/host).
  Source: `tabs/indicator/TabsIndicator.tsx:81-83,204-249`. Test: [L4] `TabsIndicator.test.tsx` "sets transformed offsets relative to the tab list when the list is not the offset parent" (`--active-tab-left` = 160, relative to the List, not its offset parent), "accounts for a scrolled container between the tab list and the active tab".
- **TAB-DOM-27** — MUST. *Rect offset* (sub-pixel-precise). With `scaleX = listRect.width / listCssWidth` when `listCssWidth > 0` else `1` (likewise `scaleY`): `rectLeft = (tabRect.left − listRect.left) / scaleX + List.scrollLeft − List.clientLeft`, `rectTop = (tabRect.top − listRect.top) / scaleY + List.scrollTop − List.clientTop`, using `getBoundingClientRect()` of the Tab and the List.
  Source: `tabs/indicator/TabsIndicator.tsx:74-78,85-92`. Test: [L4] `TabsIndicator.test.tsx` "should account for scroll and border when the tab list is transformed" (scaled ancestor, border, padding, `scrollLeft = 80`).
- **TAB-DOM-28** — MUST. *Choice of offset.* Let `(tx, ty)` be the active Tab's own 2D translation (TAB-DOM-29). If `|rectLeft − tx − layoutLeft| ≤ 2` and `|rectTop − ty − layoutTop| ≤ 2` (pixels; the tolerance for `offsetLeft`/`offsetTop` rounding), then `left = rectLeft`, `top = rectTop` (so the Indicator follows a translated Tab); otherwise — an ancestor rotation, skew, flip, perspective or 3D transform, or a List scaled to `0` (division yields `NaN`/`Infinity`, which fails the comparison) — `left = layoutLeft`, `top = layoutTop`.
  Source: `tabs/indicator/TabsIndicator.tsx:27,94-113`. Test: [L4] `TabsIndicator.test.tsx` "overlays the active tab when an ancestor has a 2D rotation", "overlays the active tab when an ancestor has a size-preserving flip", "overlays the active tab when an ancestor uses the rotate longhand", "overlays the active tab when an ancestor uses a flipping scale longhand", "overlays the active tab when an ancestor has a 3D rotation (#4837)", "follows the active tab when it has its own transform translation", "falls back to offset positions when the tab list is scaled to zero". Docs: CHANGELOG.md v1.8.0 "Consider 3D transforms when positioning the indicator", v1.0.0 "Fix indicator positioning in transformed containers".
- **TAB-DOM-29** — MUST. *Tab translation.* `(tx, ty)` is the translation component of the Tab's computed `transform` matrix (UTL-DOM-24 / UTL-ALG-13; `none` → 0) plus the Tab's computed `translate` longhand when it is not `none`: each of its first two space-separated components parsed as a number, a `%` resolving against the Tab's `offsetWidth` (x) / `offsetHeight` (y), and anything non-numeric (e.g. `calc(...)`) or absent treated as `0`. Rotation and scale on the Tab itself are not compensated (they make the comparison of TAB-DOM-28 fail, selecting the layout offset). `right = List.scrollWidth − left − width`; `bottom = List.scrollHeight − top − height`.
  Source: `tabs/indicator/TabsIndicator.tsx:106,117-118,257-290`; `utils/getElementTransform.ts:10-33`. Test: [L4] `TabsIndicator.test.tsx` "follows the active tab when it uses the translate longhand", "follows the active tab when the translate longhand uses percentages", "should set CSS variables corresponding to the active tab" (right/bottom).
- **TAB-DOM-30** — MUST. *Resize tracking.* The List installs one `ResizeObserver` (when the API exists) that observes the List element and every Tab element of that List, including Tabs mounted later and the new element when a Tab's rendered element is swapped by element substitution; each observation callback re-renders every registered Indicator so it re-measures (TAB-DOM-21). The observer is disconnected when the List element changes or the List unmounts. Scrolling (of the List or of intermediate containers) does not by itself trigger a re-measure; font loading, content or style changes trigger one only through the resize they cause.
  Source: `tabs/list/TabsList.tsx:37-82`; `tabs/tab/TabsTab.tsx:72-79,188`; `tabs/indicator/TabsIndicator.tsx:52-56`. Test: [L4] `TabsIndicator.test.tsx` "should update the position variables when the tab list is resized", "updates position when a different tab resizes", "updates position when a new tab is inserted and then resized", "keeps observing a tab whose rendered element type changes", "accounts for a scrolled container between the tab list and the active tab" (explicit comment: scroll alone does not notify). Docs: CHANGELOG.md v1.2.0 "Re-render indicator position on tab resize".
- **TAB-DOM-31** — SHOULD. A single Tab resize produces fewer than five renders of the Indicator part (render count observed through bUnit (L2)); the resize notification re-renders the Indicator alone and does not re-render the List or the Tabs.
  Source: `tabs/list/TabsList.tsx:46-50`; `tabs/indicator/TabsIndicator.tsx:52-56`; `packages/utils/src/useForcedRerendering.ts:7-13`. Test: [L2] `TabsIndicator.test.tsx` "perf: single tab resize does not fan out excessive indicator rerenders".
  Port note 2026-10-27 (ADR 06 Amendments 2026-10-18 rule 2; ADR 13 §3; design point 2 of the Tabs track, settled by the orchestrator 2026-10-26): restated for the port as a mirror count, since the `tabs` module re-measures without a C# render. A resize that changes the measurement sends one report and renders the Indicator once, the List and the Tabs not at all; a resize that changes nothing sends nothing. L2 "port: a measurement report reaches the render function's state once and renders only the indicator"; the once-per-change report at L3 ("port: the indicator shows itself with the variables once the active tab measures, and reports each change once").
- **TAB-DOM-32** — MUST. Inline-style precedence on the Indicator, lowest to highest: the six CSS variables, then the consumer's `style` (object, or function of the Indicator state), then a substitute element's own `style` (RND-DOM-16). Consumer attributes (`hidden`, `role`, `data-*`) override the Indicator's.
  Source: `tabs/indicator/TabsIndicator.tsx:149-159`; `internals/useRenderElement.tsx:115-117`. Test: [L2] `TabsIndicator.test.tsx` describeConformance (style forwarding) (L4 for the precedence of the JS-written CSS variables).
- **TAB-DOM-33** — MUST. Every Indicator in a List measures independently and every one of them re-measures (TAB-DOM-21) on each resize notification of that List.
  Source: `tabs/list/TabsList.tsx:46-50,68-73`. Test: [L4] `TabsIndicator.test.tsx` "updates all indicators when a different tab resizes".

#### 5.5.1 Pre-hydration script (`renderBeforeHydration`)

Port note 2026-10-27 (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15): the section is withdrawn for the port with TAB-API-11. The prerendered Indicator stays hidden, with no variables, until its `tabs` handle attaches and measures (TAB-EDGE-09).

- **TAB-DOM-34** — MUST. With `renderBeforeHydration`, server-rendered output contains, immediately after the Indicator element, a `<script>` whose text is the minified positioning script and whose `nonce` is the `CSPProvider` nonce when one is set (UTL-DOM-34/35). The element is kept through the hydration pass and removed once hydration completes (TRN-DOM-11, TRN-ANI-36/37); the script body is excluded from client bundles, so the client renders the element empty during hydration and never executes it. Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): there is no pre-hydration script in the port (TAB-API-11).
  Source: `tabs/indicator/TabsIndicator.tsx:6,170`; `internals/PrehydrationScript.tsx:32-49`; `packages/react/package.json` `imports` `#prehydration/tabs/indicator` (browser condition → `internals/prehydrationScript.stub.ts`). Test: [L4] `TabsIndicator.test.tsx` "renders the inline pre-hydration script during server-side rendering", "inlines the script contents during server-side rendering", "applies the CSP nonce to the pre-hydration script", "keeps the script during hydration and removes it afterwards". Docs: CHANGELOG.md v1.6.0 "Exclude the prehydration script from client bundles".
- **TAB-DOM-35** — MUST. The script, executing synchronously from the server HTML before the client takes over, locates: the Indicator = the script's previous element sibling; the List = the Indicator's closest `[role="tablist"]` ancestor; the active Tab = the first `[data-active]` descendant of the List. It exits without effect when any is missing, or when the active Tab's computed `transform`, `translate`, `rotate` or `scale` is not `none` (the component positions it on hydration instead). Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): there is no pre-hydration script in the port (TAB-API-11).
  Source: `tabs/indicator/prehydrationScript.template.js:2-29`. Test: [L4] `TabsIndicator.test.tsx` "pre-hydration script execution" (markup built by hand with `[role="tablist"]` / `[data-active]`).
- **TAB-DOM-36** — MUST. The script computes `width`/`height` per TAB-DOM-25, the layout offset per TAB-DOM-26 (walking `parentElement`, which does not cross shadow boundaries), clamps `left = min(layoutLeft, List.scrollWidth − width)` and `top = min(layoutTop, List.scrollHeight − height)` (so a rounded-up offset never produces a transient scrollbar and `right`/`bottom` stay ≥ 0), derives `right`/`bottom` as in TAB-DOM-29, writes the six `--active-tab-*` variables as `<n>px` onto the Indicator's inline style, and removes the `hidden` attribute when `width > 0 && height > 0`. It never uses bounding rects. Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): there is no pre-hydration script in the port (TAB-API-11).
  Source: `tabs/indicator/prehydrationScript.template.js:31-127`. Test: [L4] `TabsIndicator.test.tsx` "positions the indicator once a hidden streamed segment becomes visible" (`--active-tab-width` equals `offsetWidth`).
- **TAB-DOM-37** — MUST. If the first attempt leaves the Indicator hidden (the active Tab is not yet measurable, e.g. inside a hidden streamed segment or before styles/images load) and `ResizeObserver` exists, the script observes the active Tab and re-runs the positioning on each resize until it succeeds, then disconnects. It also disconnects without positioning when the Indicator is no longer connected, when the Indicator is no longer `hidden` (hydration revealed it first — the script must then leave the hydrated variables untouched), or when the captured Tab has lost `data-active`. The observer is unconditionally stopped after 10 000 ms. Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): there is no pre-hydration script in the port (TAB-API-11).
  Source: `tabs/indicator/prehydrationScript.template.js:129-159`. Test: [L4] `TabsIndicator.test.tsx` "positions the indicator once a hidden streamed segment becomes visible", "does not resurrect the indicator when the captured tab is no longer active", "stops once hydration has revealed the indicator", "keeps observing until the active tab is fully measurable". Docs: CHANGELOG.md v1.6.0 "Position pre-hydration indicator inside streamed Suspense".
- **TAB-DOM-38** — MUST. The minified script shipped in `prehydrationScript.min.ts` is the exact compiled form of the template; a port that emits an inline script MUST reproduce the template's behavior (TAB-DOM-35..37), and the generated file is the artifact executed by the browser tests. Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): no inline script is emitted, so there is no artifact to reproduce (TAB-API-11).
  Source: `tabs/indicator/prehydrationScript.min.ts:5`. Test: [L3] `TabsIndicator.test.tsx` "pre-hydration script execution" (imports the generated script).

### 5.6 ARIA

- **TAB-ARIA-01** — MUST. The List element has `role="tablist"` (consumer `role` overrides).
  Source: `tabs/list/TabsList.tsx:91`. Test: [L2] `TabsRoot.test.tsx` "does not add aria-orientation by default" (`getByRole('tablist')`); `TabsList.test.tsx` "can be named via `aria-label`".
- **TAB-ARIA-02** — MUST. The List element has `aria-orientation="vertical"` when `orientation` is `'vertical'` and no `aria-orientation` attribute when horizontal (the composite root adds none of its own, CMP-DOM-03).
  Source: `tabs/list/TabsList.tsx:90`. Test: [L2] `TabsRoot.test.tsx` "does not add aria-orientation by default", "adds the proper aria-orientation when vertical".
- **TAB-ARIA-03** — MUST. The Tab element has `role="tab"` in both native and non-native mode (it overrides the button mechanism's `role="button"`, UBT-DOM-09).
  Source: `tabs/tab/TabsTab.tsx:192,205`. Test: [L2] `TabsTab.test.tsx` "renders as an anchor and toggles selection when `nativeButton` is false" (`getAllByRole('tab')` on anchors).
  Port note 2026-10-27 (design point 4 of the Tabs track, settled by the orchestrator 2026-10-26; ADR 01 decision 2): the `tabs` module finds a List's Tabs as the `[role="tab"]` elements the List owns, so a consumer `role` that replaces `tab` (RND-DOM-10) hides that Tab from the module: no activation, no direction, no Indicator measurement. There is no guard.
- **TAB-ARIA-04** — MUST. The Tab element always has `aria-selected`: `"true"` while active, `"false"` otherwise (including disabled and when no Tab is active).
  Source: `tabs/tab/TabsTab.tsx:194`. Test: [L2] `TabsList.test.tsx` "sets the aria-selected attribute on the active tab"; `TabsRoot.test.tsx` "should pass selected prop to children", "does not select any tab when all tabs are disabled".
- **TAB-ARIA-05** — MUST. The Tab element has `aria-controls="<panel id>"` exactly while the Root's panel registry holds an entry for the Tab's value (a Panel with that value is rendered and mounted or `keepMounted`, TAB-STA-21); the value is that Panel's element id (TAB-DOM-13). When no such Panel is registered the attribute is absent. Several Panels sharing the value resolve to the last registered one.
  Source: `tabs/tab/TabsTab.tsx:117,193`; `tabs/root/TabsRoot.tsx:162-167`. Test: [L2] `TabsRoot.test.tsx` "sets the aria-controls attribute on tabs to the corresponding tab panel id", "sets aria-controls on the first tab when no value is provided", "syncs aria-controls to the mounted tab panel when keepMounted is false", "cleans and replaces panel registrations in Strict Mode"; `TabsPanel.test.tsx` "keeps the surviving registration when a shadowed panel unmounts".
  Port note 2026-10-27: "cleans and replaces panel registrations in Strict Mode" is restated under TAB-STA-21's note.
- **TAB-ARIA-06** — MUST. `aria-disabled` follows the focusable-when-disabled composite rules of UBT-ARIA-01/02: in native mode the Tab always carries `aria-disabled` with the stringified `disabled` value (`"false"` on enabled Tabs, `"true"` on disabled ones); in non-native mode it carries `aria-disabled="true"` only while disabled and no attribute otherwise. The `disabled` attribute is never used to convey the state (TAB-DOM-10).
  Source: `tabs/tab/TabsTab.tsx:111-115`; `utils/useFocusableWhenDisabled.ts:15,38-43`. Test: none in `tabs/` (covered by UBT; see §16 Q7).
- **TAB-ARIA-07** — MUST. The Panel element has `role="tabpanel"` (consumer `role` overrides).
  Source: `tabs/panel/TabsPanel.tsx:72`. Test: [L2] `TabsRoot.test.tsx` "sets the aria-labelledby attribute on tab panels to the corresponding tab id" (`getAllByRole('tabpanel', { hidden: true })`).
- **TAB-ARIA-08** — MUST. The Panel element has `aria-labelledby="<tab id>"` where the id is that of the first registered Tab whose value equals the Panel's value (TAB-STA-05c); when no such Tab is registered (never mounted, removed, or the List emptied) the attribute is absent. Server: relaxed, see §9.
  Source: `tabs/panel/TabsPanel.tsx:53,69`; `tabs/root/TabsRoot.tsx:170-180`. Test: [L2] `TabsRoot.test.tsx` "sets the aria-labelledby attribute on tab panels to the corresponding tab id", "should support values of different types", "calls onValueChange with null when a populated tab list is replaced by an empty one" (absent).
- **TAB-ARIA-09** — MUST. The Indicator element has `role="presentation"` (consumer `role` overrides).
  Source: `tabs/indicator/TabsIndicator.tsx:151`. Test: none.
- **TAB-ARIA-10** — MUST. The tablist's accessible name comes only from consumer attributes forwarded to the List element (`aria-label`, `aria-labelledby`); the component sets no name of its own.
  Source: `tabs/list/TabsList.tsx:117` (`elementProps` forwarded). Test: [L2] `TabsList.test.tsx` "can be named via `aria-label`", "can be named via `aria-labelledby`".

## 6. Interactions

### 6.1 Keyboard

Focus movement is implemented by the composite root of [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) (§4.5, CMP-KEY-01..17), which the List configures as: `orientation` = Root `orientation`, `loopFocus` = List `loopFocus`, `enableHomeAndEndKeys: true`, `disabledIndices: []` (an empty list — see TAB-KEY-07), `stopEventPropagation: true` (composite-root part default), no `modifierKeys` (every modifier blocks), no `grid`, `highlightItemOnHover: false`, reading direction from `DirectionProvider` (`'ltr'` when absent, UTL-I18N-01). Activation on <kbd>Enter</kbd>/<kbd>Space</kbd> comes from the shared button behavior in composite mode ([button-behavior](../architecture/button-behavior.md)) and the Tab's `click` handler. The tables record the observable result. "F" = forward key, "B" = backward key per TAB-KEY-02.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowRight</kbd> (LTR) / <kbd>ArrowLeft</kbd> (RTL) | horizontal List, Tab focused, no modifier | F: focus the next navigable Tab (wrapping to the first when `loopFocus`); keydown default prevented and propagation stopped when focus moves | TAB-KEY-01, -02, -04, -05, -09 |
| <kbd>ArrowLeft</kbd> (LTR) / <kbd>ArrowRight</kbd> (RTL) | horizontal List | B: focus the previous navigable Tab (wrapping to the last when `loopFocus`) | same |
| <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> | vertical List | F / B as above, regardless of reading direction | TAB-KEY-01, -02 |
| <kbd>ArrowUp</kbd> / <kbd>ArrowDown</kbd> | horizontal List | not handled: no focus move, no `preventDefault` (page scrolls) | TAB-KEY-03 |
| <kbd>ArrowLeft</kbd> / <kbd>ArrowRight</kbd> | vertical List | not handled | TAB-KEY-03 |
| <kbd>Home</kbd> / <kbd>End</kbd> | any orientation | focus the first / last navigable Tab; default prevented, propagation stopped | TAB-KEY-06 |
| any of the above + <kbd>Shift</kbd>, <kbd>Control</kbd>, <kbd>Alt</kbd> or <kbd>Meta</kbd> | any | not handled (no focus move, no `preventDefault`) | TAB-KEY-08 |
| any navigation key that moves focus | `activateOnFocus` | the newly focused Tab is activated on its `focus` event (reason `'none'`, `FocusEvent`) unless it is disabled or already active | TAB-KEY-10 |
| any navigation key that moves focus | `activateOnFocus` false (default) | selection unchanged; `onValueChange` not called | TAB-KEY-11 |
| navigation key at a list end, `loopFocus={false}` | any | focus stays; key not consumed | TAB-KEY-05 |
| <kbd>Enter</kbd> | enabled, inactive Tab focused, native `<button>` | browser-native activation produces a `click` → activate | TAB-KEY-11 |
| <kbd>Enter</kbd> | non-native Tab, not a link | keydown default prevented, synthetic click dispatched → activate | TAB-KEY-11 |
| <kbd>Enter</kbd> | non-native Tab that is an `<a href>` | not intercepted; native link activation produces the click → activate | TAB-KEY-16 |
| <kbd>Space</kbd> | enabled, inactive Tab focused (any mode) | keydown default prevented; a synthetic click is dispatched on **keydown** (composite mode) → activate; the keyup on a native `<button>` is default-prevented so no second click fires | TAB-KEY-11 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | active Tab | no callback (already active) | TAB-PTR-02 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | disabled Tab | no activation, no callback; keydown default prevented | TAB-KEY-12 |
| any key except <kbd>Tab</kbd> | disabled Tab focused | keydown default prevented at the Tab (propagation continues, so arrow keys still navigate) | TAB-KEY-12, TAB-KEY-15 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | not intercepted; focus leaves the List (single tab stop) to the next tabbable element (the open Panel, TAB-DOM-16, when it follows) | TAB-KEY-13 |
| any other key | any | not handled | TAB-KEY-01 |

- **TAB-KEY-01** — MUST. The List handles exactly <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Home</kbd> and <kbd>End</kbd> (CMP-KEY-01 with `enableHomeAndEndKeys: true`) for keydown events reaching the List element from a Tab; every other key is ignored by the List.
  Source: `tabs/list/TabsList.tsx:111-126`; `internals/composite/root/useCompositeRoot.ts:206-210`. Test: [L4] `TabsRoot.test.tsx` "keyboard navigation when focus is on a tab" (all cases).
- **TAB-KEY-02** — MUST. With `orientation` `'horizontal'`, the forward key is <kbd>ArrowRight</kbd> and the backward key <kbd>ArrowLeft</kbd> under LTR, swapped under RTL (`DirectionProvider direction="rtl"`); with `'vertical'`, forward is <kbd>ArrowDown</kbd> and backward <kbd>ArrowUp</kbd> in both directions (CMP-KEY-03).
  Source: `internals/composite/root/useCompositeRoot.ts:221-226,267-272`; `tabs/list/TabsList.tsx:122`. Test: [L4] `TabsRoot.test.tsx` "when focus is on a tab element in a horizontal ltr tablist" / "…horizontal rtl…" (browser only) / "…vertical…" — "moves focus to the next tab without activating it", "moves focus to the previous tab without activating it".
- **TAB-KEY-03** — MUST. Arrow keys outside the orientation (<kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> in a horizontal List, <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> in a vertical one) are not handled: focus does not move and the event is neither default-prevented nor stopped (CMP-KEY-11).
  Source: `internals/composite/root/useCompositeRoot.ts:267-272,282,302`. Test: none (CMP-API-02).
- **TAB-KEY-04** — MUST. With `loopFocus` `true` (default), the forward key on the last navigable Tab focuses the first navigable Tab and the backward key on the first focuses the last (CMP-KEY-06/07).
  Source: `tabs/list/TabsList.tsx:26,121`; `internals/composite/root/useCompositeRoot.ts:283-292`; see CMP-ALG-04, CMP-ALG-05, CMP-ALG-14. Test: [L4] `TabsRoot.test.tsx` "moves focus to the last tab without activating it if focus is on the first tab", "moves focus to the first tab without activating it if focus is on the last tab", "moves focus to the last tab while activating it if focus is on the first tab", "moves focus to the first tab while activating it if focus is on the last tab".
- **TAB-KEY-05** — MUST. With `loopFocus={false}`, a forward key on the last navigable Tab or a backward key on the first leaves focus where it is and does not consume the event (CMP-KEY-09/11).
  Source: `internals/composite/root/useCompositeRoot.ts:293-300,302`. Test: [L4] `TabsList.test.tsx` "does not wrap focus past the first tab when `loopFocus` is false", "does not wrap focus past the last tab when `loopFocus` is false".
- **TAB-KEY-06** — MUST. <kbd>Home</kbd> focuses the first navigable Tab and <kbd>End</kbd> the last, in every orientation and reading direction, consuming the event (CMP-KEY-05); with `activateOnFocus` the target Tab is activated by its focus event unless disabled.
  Source: `tabs/list/TabsList.tsx:120`; `internals/composite/root/useCompositeRoot.ts:274-280`; see CMP-ALG-05. Test: [L4] `TabsRoot.test.tsx` "Home" / "End" — "when `activateOnFocus = false`, moves focus to the first tab without activating it", "when `activateOnFocus = true`, moves focus to the first tab while activating it", "when `activateOnFocus = $activateOnFocus`, moves focus to a disabled tab without activating it" (and the End equivalents).
- **TAB-KEY-07** — MUST. *Navigable Tabs.* Because the List passes an empty `disabledIndices` list, a Tab counts as non-navigable (skipped by arrow/Home/End movement) only when its element matches `:disabled` (natively disabled, e.g. a substituted `<button disabled>`) or is not visible (`display: none`, `visibility: hidden/collapse`, the `hidden` attribute, or otherwise failing `checkVisibility()`); `aria-disabled` is **not** considered. Hence a Tab with `disabled` (TAB-API-08) is reachable with the arrow keys and <kbd>Home</kbd>/<kbd>End</kbd>, receives focus, and is simply not activated.
  Source: `tabs/list/TabsList.tsx:125`; `floating-ui-react/utils/composite.ts:520-554,560-572`; see CMP-ALG-02 (disabled detection), CMP-ALG-03 (visibility), CMP-ARIA-03. Test: [L4] `TabsRoot.test.tsx` "moves focus to a disabled tab without activating it" (every orientation/direction, both `activateOnFocus` values); `TabsList.test.tsx` "moves focus to a tab disabled with the `disabled` prop", "skips a natively disabled tab in a single keypress". Docs: CHANGELOG.md v1.0.0-alpha.7 "Fix keyboard navigation involving disabled Tabs".
- **TAB-KEY-08** — MUST. A navigation key pressed while <kbd>Shift</kbd>, <kbd>Control</kbd>, <kbd>Alt</kbd> or <kbd>Meta</kbd> is held does nothing: focus stays, the selection is unchanged, and the keydown is not default-prevented (CMP-KEY-02; the consumer's `onKeyDown` still fires).
  Source: `internals/composite/root/useCompositeRoot.ts:212-214,367-377`. Test: [L4] `TabsRoot.test.tsx` "does not move focus when modifier key: $modifierKey is pressed". Docs: CHANGELOG.md v1.0.0-alpha.7 "Handle modifier keys".
- **TAB-KEY-09** — MUST. When a navigation key moves focus, the keydown's default is prevented and its propagation is stopped (CMP-KEY-10, `stopEventPropagation: true`), the target Tab's index becomes the highlighted index, the target is scrolled into view within the List (CMP-STA-30), and the target element receives focus one microtask after the keydown. A consumer `onKeyDown` on the List runs before the navigation handler for the same event and observes `defaultPrevented === true` afterwards; calling `preventBaseUIHandler()` in it suppresses the navigation (RND-EVT-02/03). Server: relaxed, see §9.
  Source: `internals/composite/root/useCompositeRoot.ts:302-316`; `internals/composite/root/CompositeRoot.tsx:34,70`; `tabs/list/TabsList.tsx:117`. Test: [L4] `TabsRoot.test.tsx` every "moves focus …" case asserts `handleKeyDown` called once with `defaultPrevented: true`.
- **TAB-KEY-10** — MUST. With `activateOnFocus`, a Tab that receives focus from keyboard navigation (or programmatically) is activated through the user-activation path (TAB-STA-03) with `reason: 'none'` and `details.event` the native `FocusEvent`, unless it is disabled or already active; `onValueChange` is called exactly once per focus move. Server: relaxed, see §9.
  Source: `tabs/tab/TabsTab.tsx:140-152`. Test: [L4] `TabsRoot.test.tsx` "when `activateOnFocus = true` should call onValueChange if an unactive tab gets focused", "moves focus to the next tab while activating it", "moves focus to the previous tab while activating it"; `TabsTab.test.tsx` "activates on focus again once a secondary-button press has ended".
  Port note 2026-10-27 (design point 8 of the Tabs track, settled by the orchestrator 2026-10-26; ADR 01, the row "TAB-PTR-01, TAB-KEY-10"): met on WebAssembly, where the request runs inside the `select` decision of the focus; on Server the commit lands a round trip later. L4 "port: a focus activation commits inside the focus on WebAssembly and a round trip later on Server".
- **TAB-KEY-11** — MUST. With `activateOnFocus` `false`, moving focus never calls `onValueChange`; the focused enabled, inactive Tab is activated by <kbd>Enter</kbd> or <kbd>Space</kbd>, both of which produce a `click` on the Tab: native `<button>` — <kbd>Enter</kbd> via the browser's native activation, <kbd>Space</kbd> via a synthetic click dispatched on keydown with the keyup default-prevented (UBT-KEY-12/15); non-native element — both keys via synthetic clicks (UBT-KEY-01/12). The resulting `onValueChange` has `reason: 'none'` and `details.event` the click event.
  Source: `tabs/tab/TabsTab.tsx:132-138`; `internals/use-button/useButton.ts:138-152,172-175,187-196`. Test: [L4] `TabsTab.test.tsx` "activates the focused tab with %s when `activateOnFocus` is false" (Enter, Space); `TabsRoot.test.tsx` "when `activateOnFocus = false` should not call onValueChange if an unactive tab gets focused", "keeps controlled async activation and focus aligned with activateOnFocus=false".
- **TAB-KEY-12** — MUST. On a disabled Tab, <kbd>Enter</kbd>/<kbd>Space</kbd> MUST NOT activate it or call `onValueChange`; the button mechanism skips its keydown/keyup handling while disabled (UBT-KEY-19) and, because the Tab is focusable when disabled, default-prevents every keydown except <kbd>Tab</kbd> at the Tab element without stopping propagation (UBT-KEY-20/21).
  Source: `tabs/tab/TabsTab.tsx:111-115,133`; `utils/useFocusableWhenDisabled.ts:23-27`; `internals/use-button/useButton.ts:116-119,177-181`. Test: [L4] `TabsTab.test.tsx` "does not activate a disabled tab that is pressed and focused" (focus does not activate); none for Enter/Space on a disabled tab (covered by UBT).
- **TAB-KEY-13** — MUST. <kbd>Tab</kbd> and <kbd>Shift</kbd>+<kbd>Tab</kbd> are never intercepted: the List is one position in the sequential tab order (its highlighted Tab, CMP-FOC-01), tabbing into it lands on that Tab, and tabbing again leaves the List.
  Source: `internals/composite/root/useCompositeRoot.ts:207-210` (`Tab` not in the key set); `internals/composite/item/useCompositeItem.ts:25`. Test: [L4] `TabsRoot.test.tsx` "keeps a roving focus entry point when a controlled selected tab is removed" (`user.tab()` lands on the tab with `tabindex="0"`), "does not move focus when modifier key: $modifierKey is pressed" (`[Tab]` focuses the first tab).
- **TAB-KEY-14** — MUST. Keyboard focus movement never changes a consumer-owned value by itself; with `activateOnFocus` the Root only reports the request (TAB-STA-03).
  Source: `tabs/root/TabsRoot.tsx:109-124`. Test: [L4] `TabsRoot.test.tsx` "moves focus to the next tab while activating it" (controlled `value={1}`; `handleChange` called with `2`, DOM selection unchanged).
- **TAB-KEY-15** — MUST. Navigation keys pressed while a disabled Tab has focus still move focus (the Tab's own keydown default-prevention does not stop propagation to the List).
  Source: `utils/useFocusableWhenDisabled.ts:23-27`; `internals/composite/root/useCompositeRoot.ts:206`. Test: [L4] `TabsRoot.test.tsx` "moves focus to a disabled tab without activating it" (second keydown from the disabled tab focuses the third tab); "keeps controlled async activation and focus aligned with activateOnFocus=%s".
- **TAB-KEY-16** — MUST. A non-native Tab rendered as an anchor with an `href` is activated by <kbd>Enter</kbd> through the browser's native link activation (not intercepted, UBT-KEY-10) and by <kbd>Space</kbd> through the composite synthetic click (UBT-KEY-12); the click both activates the Tab and follows the link.
  Source: `internals/use-button/useButton.ts:130-131,138-152,155-163`. Test: [L4] `TabsTab.test.tsx` "renders as an anchor and toggles selection when `nativeButton` is false" (click path), "renders a react-router Link"; keyboard path: none (UBT).
  Port note 2026-10-27 (ADR 13 Amendments 2026-10-22): "renders a react-router Link" is re-cited to the L4 port title "port: an `<a href>` substitute keeps its href and is activated by Space and by Enter", which also covers the keyboard path.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `click` (primary button, any pointer type) | enabled, inactive Tab | activate: `onValueChange(value, { reason: 'none', event: <click> })`, then commit (uncontrolled) | TAB-PTR-01 |
| `click` | active Tab | nothing; no callback | TAB-PTR-02 |
| `click` | disabled Tab | nothing; click default-prevented; no callback | TAB-PTR-03 |
| `click` with `button === 2` | any Tab | nothing (no activation) | TAB-PTR-04 |
| `pointerdown` (primary) then focus | `activateOnFocus`, enabled inactive Tab | activated on the `focus` event that the press causes (before `click`); the later click is a no-op | TAB-PTR-05 |
| `pointerdown` (secondary/middle) then focus | `activateOnFocus` | not activated by that focus; pressing state cleared on `pointerup`/`pointercancel` anywhere in the document, after which focus activates again | TAB-PTR-06 |
| `pointerdown` | disabled Tab | default prevented (no focus by the press in most browsers); nothing else | TAB-PTR-07 |
| `pointerdown` | active Tab | nothing (pressing state not tracked) | TAB-PTR-07 |
| hover / `mousemove` | any | nothing | TAB-PTR-08 |

- **TAB-PTR-01** — MUST. A `click` on an enabled, inactive Tab (mouse, touch, pen, or keyboard-originated click) activates it through TAB-STA-03 with `reason: 'none'`, `details.event` = the native click event (`MouseEvent`/`PointerEvent`), `details.trigger` = `undefined`, and `details.activationDirection` computed per §4.4. In an uncontrolled Root the DOM reflects the new selection synchronously after the click (`aria-selected`, Panel visibility). Server: relaxed, see §9.
  Source: `tabs/tab/TabsTab.tsx:123-138`; `tabs/root/TabsRoot.tsx:109-124`. Test: [L4] `TabsRoot.test.tsx` "should call onValueChange when clicking" (`activationDirection === 'right'`), "selects the clicked tab"; `TabsList.test.tsx` "sets the aria-selected attribute on the active tab".
  Port note 2026-10-27 (design point 8 of the Tabs track, settled by the orchestrator 2026-10-26; ADR 01, the row "TAB-PTR-01, TAB-KEY-10"): met on WebAssembly, where the request runs inside the `select` decision of the click; on Server the module reports the activation and the commit lands a round trip later. L4 "port: the selection commits inside the click on WebAssembly and a round trip later on Server".
- **TAB-PTR-02** — MUST. A click (or press, or keyboard activation) on the already active Tab MUST NOT call `onValueChange` and MUST NOT re-commit the value.
  Source: `tabs/tab/TabsTab.tsx:122,133,141,155`. Test: [L4] `TabsRoot.test.tsx` "should not call onValueChange when already active"; `TabsTab.test.tsx` "does not re-commit the value when the active tab is pressed".
- **TAB-PTR-03** — MUST. A click on a disabled Tab MUST NOT activate it or call `onValueChange`; the button mechanism default-prevents the click (UBT-PTR-01) and the consumer's `onClick` is not invoked.
  Source: `tabs/tab/TabsTab.tsx:133`; `internals/use-button/useButton.ts:104-110`. Test: [L4] `TabsRoot.test.tsx` "does not select the clicked disabled tab"; `TabsTab.test.tsx` "does not activate a disabled tab that is pressed and focused". Docs: CHANGELOG.md v1.0.0-alpha.6 "Fix being able to activate a disabled tab".
- **TAB-PTR-04** — MUST. A `click` event whose `button` is `2` (secondary button) MUST NOT activate the Tab (see §14 for the mechanism: the framework does not deliver such clicks to `click` handlers).
  Source: `tabs/tab/TabsTab.tsx:132-138` (no button check; React filters `button === 2` clicks). Test: [L4] `TabsRoot.test.tsx` "should not call onValueChange on non-main button clicks".
  Port note 2026-10-27 (settled by the orchestrator 2026-10-25; §16 Q12): the `tabs` module checks it explicitly: a `click` whose `button` is not `0` activates nothing (L3 "port: a click whose button is not the primary one activates nothing"; the cited title at L4).
- **TAB-PTR-05** — MUST. With `activateOnFocus`, a primary-button `pointerdown` on an enabled, inactive Tab records a *pressing* state with *main button*; the `focus` event the press causes activates the Tab immediately (reason `'none'`, `FocusEvent`), before any `click`; the subsequent `click` finds the Tab active and does nothing, so `onValueChange` is called exactly once for the press. The consumer's `onPointerDown` is invoked once.
  Source: `tabs/tab/TabsTab.tsx:140-177`. Test: [L4] `TabsRoot.test.tsx` "when `activateOnFocus = true` should call onValueChange on pointerdown". Docs: CHANGELOG.md v1.0.0-alpha.6 "Fix tabs activating incorrectly on non-primary button clicks".
- **TAB-PTR-06** — MUST. With `activateOnFocus`, a `pointerdown` with a non-primary button (`event.button !== 0`, e.g. right or middle) records *pressing* without *main button*, so a `focus` arriving while that press is held MUST NOT activate the Tab. Both flags are cleared by the next `pointerup` or `pointercancel` anywhere in the Tab's document (listeners registered on the document for every press and removed when either fires), after which focus-driven activation works again.
  Source: `tabs/tab/TabsTab.tsx:145-151,159-176`. Test: [L4] `TabsTab.test.tsx` "does not activate a tab focused by a held secondary-button press", "activates on focus again once a secondary-button press has ended", "activates on focus again once a secondary-button press is cancelled".
- **TAB-PTR-07** — MUST. A `pointerdown` on a disabled Tab is default-prevented by the button mechanism and the Tab's own handler does not run (UBT-PTR-03); a `pointerdown` on the active Tab records nothing.
  Source: `tabs/tab/TabsTab.tsx:155-157`; `internals/use-button/useButton.ts:218-224`. Test: [L4] `TabsTab.test.tsx` "does not activate a disabled tab that is pressed and focused".
- **TAB-PTR-08** — MUST. Hovering a Tab has no effect: the composite root is configured without hover highlighting (CMP-PTR-01) and the Tab attaches no hover handlers.
  Source: `internals/composite/root/CompositeRoot.tsx:38`; `tabs/list/TabsList.tsx:111-126` (no `highlightItemOnHover`). Test: none.
- **TAB-PTR-09** — MUST. Consumer `onClick`, `onFocus`, `onPointerDown`, `onKeyDown`, `onKeyUp` and `onKeyDownCapture` handlers on a Tab run before the Tab's own handler for the same event (RND-EVT-02) and are subject to the button mechanism's disabled gate (UBT-EVT-03: not invoked while disabled for click/pointerdown/keydown/keyup); calling `preventBaseUIHandler()` in a consumer `onClick`/`onFocus` suppresses the activation. Server: relaxed, see §9.
  Source: `tabs/tab/TabsTab.tsx:189-206` (merge order: composite props → Tab handlers → consumer props → button props getter). Test: [L4] `TabsRoot.test.tsx` "when `activateOnFocus = true` should call onValueChange on pointerdown" (`handlePointerDown` called once).

### 6.3 Focus management

Roving tabindex is owned by the composite mechanism (CMP-STA-22..30, CMP-DOM-01, CMP-FOC-01/02); this section states what Tabs configures and adds.

- **TAB-FOC-01** — MUST. Exactly one Tab of a List is in the sequential tab order at any time (the *highlighted* Tab, `tabindex="0"`); all others have `tabindex="-1"` (TAB-DOM-09).
  Source: `internals/composite/item/useCompositeItem.ts:19,25`. Test: [L4] `TabsRoot.test.tsx` "puts the selected child in tab order", "should allow to focus first tab when there are no active tabs".
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's `tabindex` from attach; settled by the orchestrator 2026-10-25; ADR 13 §3; conventions §3.5): re-tagged from L2, because the tab stop is the module's after attach, which only a browser observes; the titles run in `tabs.spec.ts` with recorded `tabindex` moves.
- **TAB-FOC-02** — MUST. *Initial tab stop.* When the Tabs first register, the highlighted index becomes the index of the Tab carrying the active marker (the active Tab, TAB-DOM-08) if there is one; otherwise it stays `0` unless the Tab at index `0` is non-navigable per TAB-KEY-07, in which case it becomes the first navigable index (CMP-STA-23/24). With no active Tab (value `null` or missing) the first Tab is therefore the tab stop.
  Source: `internals/composite/root/useCompositeRoot.ts:137-161`; `tabs/tab/TabsTab.tsx:199`; see CMP-ALG-05, CMP-ALG-18. Test: [L4] `TabsRoot.test.tsx` "puts the selected child in tab order" (`[-1, 0]` for `value={1}`), "should allow to focus first tab when there are no active tabs".
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's `tabindex` from attach; settled by the orchestrator 2026-10-25; ADR 13 §3; conventions §3.5): re-tagged from L2, because the tab stop is the module's after attach, which only a browser observes; the titles run in `tabs.spec.ts` with recorded `tabindex` moves.
- **TAB-FOC-03** — MUST. A Tab receiving focus from any source (keyboard move, pointer press, programmatic `focus()`) becomes the highlighted Tab (CMP-PTR-02), so the tab stop follows the focused Tab even when `activateOnFocus` is `false`.
  Source: `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L4] `TabsRoot.test.tsx` "when focus is inside the tablist, highlight stays put on external change and arrow keys continue from the focused tab" (arrow navigation continues from the focused, non-active tab).
- **TAB-FOC-04** — MUST. When the active Tab changes while focus is **outside** the List (controlled `value` change, automatic fallback, or a click that did not focus the Tab), the tab stop moves to the newly active Tab provided that Tab is enabled (`disabled` prop `false`): post-commit, pre-paint of the change, the highlighted index becomes the active Tab's index. The Tab does this itself by calling the composite root's `onHighlightedIndexChange(index)` (TAB-STA-19); the composite root does not re-read the active marker for this (CMP-DOM-02).
  Source: `tabs/tab/TabsTab.tsx:53,83-109`; see CMP-EVT-01. Test: [L4] `TabsRoot.test.tsx` "puts the selected child in tab order", "when focus is outside the tablist, highlight follows the new active tab (tabIndex=0 moves)", "calls onValueChange when the selected tab is removed" (fallback tab gets `tabindex="0"`). Docs: CHANGELOG.md v1.0.0-beta.0 "Fix focus going out of sync when selected value is changed externally".
- **TAB-FOC-05** — MUST. When the active Tab changes while the document's active element (resolved through shadow roots, UTL-DOM-19/20) is **inside** the List, the highlighted index is left unchanged: the focused Tab keeps `tabindex="0"` and arrow navigation continues from it, even though another Tab is now `aria-selected="true"`.
  Source: `tabs/tab/TabsTab.tsx:93-102`. Test: [L4] `TabsRoot.test.tsx` "when focus is inside the tablist, highlight stays put on external change and arrow keys continue from the focused tab"; `TabsList.test.tsx` "keeps the tab stop on the selected tab when focus is inside the list". Docs: CHANGELOG.md v1.0.0-beta.3 "Fix highlight sync when focus is inside list".
- **TAB-FOC-06** — MUST. A disabled Tab that becomes active through a consumer-owned value never receives the tab stop through TAB-FOC-04; the previously highlighted Tab keeps `tabindex="0"`.
  Source: `tabs/tab/TabsTab.tsx:104-108`. Test: [L4] `TabsRoot.test.tsx` "does not set tabIndex=0 on disabled tabs when they are programmatically selected".
  Port note 2026-10-27 (CMP-STA-22's dated divergence; design point 5 of track 1, option (a), settled by the orchestrator 2026-10-26): the follow (track 1's E5) reads `aria-disabled="true"` from the marked Tab in the applied batch, not the Tab's `Disabled`. The two agree for every Tab the port renders, because only a disabled Tab renders `aria-disabled="true"` (TAB-DOM-10, TAB-ARIA-06); a consumer's `aria-disabled="true"` on an enabled Tab also blocks the follow, where Base UI follows.
- **TAB-FOC-07** — MUST. A keyboard navigation step (any keydown on a Tab) marks that Tab so that its next highlight re-synchronization (TAB-FOC-04) is skipped once; consequently, after an arrow move away from the active Tab whose activation is pending or declined, the active Tab does not pull the tab stop back and focus/tab stop stay together on the newly focused Tab. With an asynchronously applied controlled value (the consumer commits the change in a later task), focus and selection end up on the same Tab for both `activateOnFocus` values.
  Source: `tabs/tab/TabsTab.tsx:71,84-87,200-202`. Test: [L4] `TabsRoot.test.tsx` "keeps controlled async activation and focus aligned with activateOnFocus=%s".
- **TAB-FOC-08** — MUST. *Removal of the highlighted Tab.* When the Tab holding the tab stop is removed (or its element replaced), the composite rule CMP-STA-26/27 applies with Tabs' marker: if another navigable Tab now occupies the same index it takes the tab stop; otherwise the tab stop moves to the first navigable Tab carrying the active marker, else the first navigable Tab, else index `0`. "Navigable" here follows TAB-KEY-07 (a Tab hidden with the `hidden` attribute is not; a Tab with the `disabled` prop is), so a disabled Tab can be the fallback entry point. The tab stop then keeps following that element through later removals.
  Source: `internals/composite/root/useCompositeRoot.ts:115-135,345-365`; `tabs/list/TabsList.tsx:125`; see CMP-ALG-15 (fallback index), CMP-ALG-18. Test: [L4] `TabsList.test.tsx` "moves the tab stop off a hidden successor when the highlighted tab is removed", "can fall back to a tab that is focusable when disabled", "keeps tracking a successor through subsequent removals"; `TabsRoot.test.tsx` "keeps a roving focus entry point when a controlled selected tab is removed".
- **TAB-FOC-09** — MUST. An open Panel is a sequential tab stop (`tabindex="0"`, TAB-DOM-16) and a closed one is not (`tabindex="-1"`, `inert`, and `hidden` once unmounted).
  Source: `tabs/panel/TabsPanel.tsx:70,73-74`. Test: none.
- **TAB-FOC-10** — MUST. Focus moved by a navigation key lands on the target Tab one microtask after the keydown (CMP-FOC-02), which lets a surrounding focus manager (Popover, Dialog) finish its own focus work first; Tabs inside such popups receive initial focus on the highlighted Tab and navigate normally.
  Source: `internals/composite/root/useCompositeRoot.ts:312-315`. Test: [L4] `TabsRoot.test.tsx` "works inside Popover", "works inside Dialog".
  Port note 2026-10-28 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123)): "works inside Dialog" runs in `tabs.spec.ts` on the `tabs-dialog` fixture, both modes; "works inside Popover" is open to Popover (M4 track 5).
- **TAB-FOC-11** — MUST. When a navigation key moves the highlight, the target Tab is scrolled into view inside the List (nearest-edge alignment on the orientation's axis, honoring `scroll-margin`/`scroll-padding`, RTL-aware; CMP-STA-30); focusing by pointer or programmatically does not scroll (the only other scroll is the initial one of TAB-FOC-13).
  Source: `internals/composite/root/useCompositeRoot.ts:101-108,310`; `internals/composite/composite.ts:44-144`; see CMP-ALG-19. Test: none in `tabs/`.
- **TAB-FOC-12** — MUST. Activation never moves focus by itself: a click focuses the Tab only through the browser's native behavior, a controlled value change does not focus the new Tab, and an automatic fallback does not focus anything.
  Source: `tabs/root/TabsRoot.tsx:109-135` (no focus calls); `tabs/tab/TabsTab.tsx:123-130`. Test: [L4] `TabsRoot.test.tsx` "when focus is outside the tablist, highlight follows the new active tab (tabIndex=0 moves)" (only `tabIndex` asserted).
- **TAB-FOC-13** — MUST. When the Tabs of a List first register, the Tab carrying the active marker (TAB-DOM-08), if any, is scrolled into view inside the List by the same nearest-edge procedure as TAB-FOC-11 (CMP-STA-25); focus does not move. Later registry changes (Tabs added, removed or reordered) and selection changes do not scroll the List.
  Source: `internals/composite/root/useCompositeRoot.ts:137-143,163`; `internals/composite/composite.ts:44-144`; `tabs/tab/TabsTab.tsx:199`; see CMP-ALG-19. Test: none in `tabs/`.

### 6.4 Dismissal

Not applicable — Tabs has no popup; nothing closes on outside press, <kbd>Escape</kbd>, scroll or blur.

### 6.5 Reading direction

- **TAB-I18N-01** — MUST. Reading direction is taken only from the nearest `DirectionProvider` (`'ltr'` without one; `dir` attributes are not consulted, UTL-I18N-01..03). Under `'rtl'` the horizontal arrow mapping of TAB-KEY-02 is swapped (<kbd>ArrowLeft</kbd> moves forward, <kbd>ArrowRight</kbd> backward); vertical Lists, <kbd>Home</kbd>/<kbd>End</kbd>, activation and the Indicator are unaffected.
  Source: `internals/composite/root/CompositeRoot.tsx:43`; `internals/composite/root/useCompositeRoot.ts:221-224`. Test: [L4] `TabsRoot.test.tsx` "when focus is on a tab element in a horizontal rtl tablist" (browser only; `previousItemKey: 'ArrowRight'`, `nextItemKey: 'ArrowLeft'`).
- **TAB-I18N-02** — MUST. The activation direction is visual, not logical (TAB-STA-15): it compares bounding-rect `left` (horizontal) or `top` (vertical) coordinates, so in a right-to-left layout activating the Tab that follows in reading order (rendered further left) yields `'left'`. The Indicator's `--active-tab-left`/`--active-tab-right` are likewise physical offsets from the List's left and right edges.
  Source: `tabs/root/TabsRoot.tsx:374-377,396-406`; `tabs/indicator/TabsIndicator.tsx:85-92,117`. Test: none (`docs/src/app/(private)/experiments/tabs/tabs-animations.tsx` exercises RTL manually).

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: Tabs.Tab.Value, details: Tabs.Root.ChangeEventDetails) => void` | user activation of an inactive, enabled Tab (click, keyboard activation, focus with `activateOnFocus`); automatic selection in an uncontrolled Root (§4.3) | `'none'` (user), `'initial'`, `'disabled'`, `'missing'` (automatic) | user: yes; automatic: no |

There is no `onValueChangeComplete`, no `actionsRef`, no highlight callback and no per-part callback in this version.

- **TAB-EVT-01** — MUST. `onValueChange` receives `(value, details)` where `details` is a change-event details object (EVD-API-01) extended with `activationDirection` (EVD-API-08): `{ reason, event, trigger, cancel(), allowPropagation(), isCanceled, isPropagationAllowed, activationDirection }`. `trigger` is always `undefined` (EVD-API-07).
  Source: `tabs/root/TabsRoot.tsx:109-135,457-466`; `tabs/tab/TabsTab.tsx:123-130`. Test: [L2] `TabsRoot.test.tsx` "does not cancel automatic value changes" (`trigger === undefined`, `activationDirection === 'none'`).
- **TAB-EVT-02** — MUST. For a user activation, `onValueChange` is called synchronously inside the originating event's handling, before any state or DOM change (EVD-EVT-02), with `reason: 'none'`, `details.event` = the native `click` event (`MouseEvent`/`PointerEvent`; keyboard-originated clicks included) or, for `activateOnFocus` activations, the native `FocusEvent`; `details.activationDirection` = the direction from the current value to the requested value (§4.4, computed from element positions since both Tabs are registered). It fires regardless of ownership mode, even if the consumer will not apply the change.
  Source: `tabs/root/TabsRoot.tsx:109-116`; `tabs/tab/TabsTab.tsx:123-130,137,150`; `internals/createBaseUIEventDetails.ts:126-157`. Test: [L4] `TabsRoot.test.tsx` "should call onValueChange when clicking" (`activationDirection === 'right'`), "when `activateOnFocus = true` should call onValueChange on pointerdown", "should update `data-activation-direction` on programmatic change after a controlled parent ignores click".
- **TAB-EVT-03** — MUST. Calling `details.cancel()` during a user activation prevents the commit in an uncontrolled Root (selection, `aria-selected`, Panels and activation direction all stay as before, EVD-EVT-05); in a controlled Root it has no effect on the component (EVD-EVT-06) — the consumer decides whether to apply the value.
  Source: `tabs/root/TabsRoot.tsx:117-122`. Test: [L4] `TabsRoot.test.tsx` "does not move an uncontrolled selection when a user-initiated change is canceled", "should update `data-activation-direction` on programmatic change after a canceled click".
- **TAB-EVT-04** — MUST. For an automatic selection (§4.3), `onValueChange` is called post-commit, pre-paint, *after* the fallback value (and the `'none'` direction) has been scheduled for commit and before the synchronous re-render that applies it (EVD-EVT-10), so the change is not cancelable, a callback that reads the DOM still sees the previous selection, and the fallback is applied before paint whatever the callback does; it is called with `reason` ∈ `{'initial', 'disabled', 'missing'}` (TAB-STA-09), `details.event` = a freshly created generic `Event` whose `type` is `'base-ui'` (EVD-API-04), `details.trigger` = `undefined`, `details.activationDirection` = `'none'`, and the committed value as first argument (possibly `null`). `details.cancel()` has no effect; a callback that throws does not undo the commit. Server: relaxed, see §9.
  Source: `tabs/root/TabsRoot.tsx:126-135,245-261,314,319`. Test: [L2] `TabsRoot.test.tsx` "does not cancel automatic value changes" (`event instanceof Event`, `event.type === 'base-ui'`), "calls onValueChange when auto-selecting the first tab on mount", "calls onValueChange with null when all tabs are initially disabled".
- **TAB-EVT-05** — MUST. `onValueChange` is called exactly once per change request (EVD-EVT-03) and never for: activating the already active Tab (TAB-PTR-02); a consumer-owned `value` prop change; a change the automatic-selection pass resolves to the current value (TAB-STA-10); a `null` selection remaining `null` (TAB-STA-12); mount with an explicit `defaultValue` that matches an enabled Tab.
  Source: `tabs/tab/TabsTab.tsx:122-138`; `tabs/root/TabsRoot.tsx:297-304,318-321`. Test: [L4] `TabsRoot.test.tsx` "should not call onValueChange when already active", "does not call onValueChange on initial render when defaultValue is provided", "does not emit a second change when the fallback resolves to the current value", "does not emit missing when an enabled tab appears after all tabs were disabled", "does not call onValueChange when a controlled selected tab becomes disabled".
- **TAB-EVT-06** — MUST. A user activation whose Panel content suspends (the Panel's subtree is not yet renderable when it opens, with the suspense boundary outside the Root) still produces exactly one `onValueChange` with `reason: 'none'`, the selection is committed, and the Panel renders once its content is ready; the Root does not treat the temporarily torn-down Tab registrations as a removal (TAB-STA-11). Port: withdrawn (ADR 02 Amendments).
  Source: `tabs/root/TabsRoot.tsx:263-274`. Test: [L2] `TabsPanel.test.tsx` "renders a panel that suspends when opened with the boundary outside the root". Docs: CHANGELOG.md v1.5.0 "Fix suspended panel activation".

## 8. Form integration

Not applicable — Tabs renders no form control, has no `name`/`value` serialization and does not participate in Field validation.

## 9. Lifecycle, mounting and animation

Vocabulary: "post-commit, pre-paint", "next frame", "microtask" and "on animations finished" as defined in conventions §4 and [transitions-and-mounting](../architecture/transitions-and-mounting.md). Only the Panel mounts and unmounts; the Root, List, Tab and Indicator have no transition state (the Indicator's visibility is attribute-driven, §5.5). The Panel uses the default transition-status machine (TRN-ALG-01 with `enableIdleState = false`, `deferEndingState = false`, `animateInitialOpen = false`) and the generic open-change completion watcher (TRN-ALG-04) on its own element, with `batch: false`.

- **TAB-ANI-01** — MUST. When a Panel becomes open while not mounted, it becomes mounted and its transition status becomes `'starting'` in the same commit that first renders it open (TRN-STA-03): the element appears with `data-starting-style=""`, `tabindex="0"`, no `hidden`, no `inert`, and the new `data-activation-direction`; the `'starting'` status is cleared to unset on the next frame (TRN-STA-04), removing `data-starting-style` so that CSS transitions declared on the Panel run from the starting styles. Server: relaxed, see §9.
  Source: `tabs/panel/TabsPanel.tsx:49-51,64-81`; `internals/useTransitionStatus.ts:31-34,58-73`. Test: [L4] `TabsPanel.test.tsx` "triggers enter animation via data-starting-style when mounting" (the `transitionend` of a 1 ms opacity transition is observed); `TabsRoot.test.tsx` "should set the `data-activation-direction` attribute on the tabs root with orientation=horizontal" (first open render carries the direction).
- **TAB-ANI-02** — MUST. A Panel that is open on its first render (initial selection) mounts without the `'starting'` phase: no `data-starting-style`, no frame request (TRN-STA-13).
  Source: `internals/useTransitionStatus.ts:23-29`. Test: none in `tabs/` (TRN).
- **TAB-ANI-03** — MUST. When an open Panel becomes closed, its status becomes `'ending'` in the closing commit (TRN-STA-06): `data-ending-style=""` and `inert` appear together while the element stays in the document (still registered for `aria-controls`, TAB-ANI-05). The Panel stays mounted until on animations finished for its element (TRN-ALG-03: finite CSS animations/transitions present after the next frame; completion in the following microtask, applied in a synchronous commit before paint); if no animation runs, it unmounts on the next frame after the closing commit plus a microtask. Unmounting sets `mounted` to `false`: without `keepMounted` the element is removed; with `keepMounted` it stays with `hidden` and `data-hidden` (TAB-DOM-14) and the status becomes unset (TRN-STA-08). Server: relaxed, see §9.
  Source: `tabs/panel/TabsPanel.tsx:83-91,104-109`; `internals/useOpenChangeComplete.tsx:9-28`; `internals/useAnimationsFinished.ts:93-99`. Test: [L4] `TabsPanel.test.tsx` "applies data-ending-style before unmount" (100 ms keyframe; attribute observed, then element removed); `TabsRoot.test.tsx` "syncs aria-controls to the mounted tab panel when keepMounted is false" (`waitFor` the previous panel to disappear).
- **TAB-ANI-04** — MUST. The completion callback re-reads the live open state: a completion arriving after the Panel was reopened (the Tab re-selected during the exit) does not unmount it (TRN-EDGE-05); reopening during `'ending'` clears the status on the next frame without a new `'starting'` phase (TRN-STA-09).
  Source: `tabs/panel/TabsPanel.tsx:86-90`; `internals/useTransitionStatus.ts:58-73`. Test: none in `tabs/` (TRN).
- **TAB-ANI-05** — MUST. The Panel's registration in the Root (TAB-STA-21) exists for every commit in which the Panel element is rendered and (`mounted` or `keepMounted`): it is created post-commit, pre-paint after the opening commit (so the Tab's `aria-controls` appears in the next render, one commit after the Panel element), persists through the exit animation, and is removed when the Panel unmounts (or, with `keepMounted`, when it becomes hidden). `attach` and `dispose` of the part's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op.
  Source: `tabs/panel/TabsPanel.tsx:93-102`; `tabs/root/TabsRoot.tsx:137-159`. Test: [L2] `TabsRoot.test.tsx` "syncs aria-controls to the mounted tab panel when keepMounted is false" (`waitFor` on `aria-controls` after the click), "cleans and replaces panel registrations in Strict Mode"; twin contract (a repeated `attach` of the same Panel element is a no-op) (L4 for the exit-animation clause).
  Port note 2026-10-27: "cleans and replaces panel registrations in Strict Mode" is restated under TAB-STA-21's note.
- **TAB-ANI-06** — MUST. When the global animations-disabled flag is set (`BASE_UI_ANIMATIONS_DISABLED`, TRN-EDGE-01) or the element has no `getAnimations`, a closed Panel unmounts synchronously when the completion watcher arms (after paint of the closing commit), so `data-ending-style` is present for at most one commit. Server: relaxed, see §9.
  Source: `internals/useAnimationsFinished.ts:93-99`; `internals/useOpenChangeComplete.tsx:15-27`. Test: [L4] `TabsPanel.test.tsx` "animations" `afterEach` restores the flag; every non-animation test runs under it.
- **TAB-ANI-07** — MUST. The Indicator's pre-hydration script (§5.5.1) runs once from server HTML; after hydration the client Indicator re-measures (TAB-DOM-21) and rewrites the variables with sub-pixel precision, accepting the pre-positioned state without re-hiding the element (hydration differences on the Indicator element are suppressed). Port: withdrawn (ADR 06 Amendments 2026-10-26, chosen by the user 2026-10-26; §16 Q15; 2026-10-27): nothing is pre-positioned; the prerendered Indicator is hidden and its `tabs` handle measures it on attach (TAB-API-11, TAB-DOM-21).
  Source: `tabs/indicator/TabsIndicator.tsx:156-158,170`; `internals/PrehydrationScript.tsx:37-48`. Test: [L4] `TabsIndicator.test.tsx` "keeps the script during hydration and removes it afterwards", "stops once hydration has revealed the indicator".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):
- TAB-STA-04 — composite registration (CMP-STA-10 row): the registry update lands after paint plus a round trip; observable only as latency of dependent attribute updates
- TAB-STA-05, TAB-STA-06, TAB-STA-07, TAB-STA-08, TAB-STA-09, TAB-STA-10, TAB-STA-11, TAB-EVT-04, TAB-DOM-17, TAB-ARIA-08 — "EVD-STA-07, CMP-STA-10, FFI registration" row (added 2026-10-27, settled by the orchestrator 2026-10-25): the automatic selection, its notification, `data-index` and `aria-labelledby` follow the composite's ordered-ids and index reports, after paint plus a round trip; meanwhile the raw default and `data-index="-1"` show
- TAB-KEY-09 — "SLD-PTR-01 (default-prevented clause), EVD-EVT-26 against composite/button key handling" row (added 2026-10-27): the key is handled regardless; the List's and the focused Tab's consumer `onkeydown` run a round trip later, from the `composite` module's handled-key report, and cannot veto the navigation
- TAB-PTR-09 — "C#-origin consumer vetoes" row (added 2026-10-27): the `tabs` module reports the activation and C# runs the consumer's `onclick` or `onfocus` before the request, a round trip after the dispatch, so `PreventPartHandler()` still vetoes the activation, whose commit lands a round trip later
- TAB-STA-14 (class, style and render-function clause), TAB-STA-20, TAB-DOM-24 (state clause) — "RND-STA-01 for mirrored fields" row (added 2026-10-27): every part's `TabActivationDirection` and the Indicator's `ActiveTabPosition`/`ActiveTabSize` follow the module's report a round trip after the attribute and the variables, which the module writes before paint on both modes
- TAB-ANI-01, TAB-ANI-03, TAB-STA-22 — "TRN-STA-03, TRN-STA-05, TRN-STA-06, TRN-STA-10 on an element whose `transitions` twin is already attached" row (added 2026-10-27): met on both modes through `OpenSelector = ":not([inert])"`, an attribute the Panel renders while closed (design point 1 of the Tabs track, settled by the orchestrator 2026-10-26); a consumer `inert` overrides the part's and misleads the module; listed so the open input is not moved to `update`
- TAB-ANI-03, TAB-ANI-06, TAB-STA-22 — "TRN-ANI-03, TRN-ANI-09, TRN-ANI-10" row (added 2026-10-27): the unmount after the exit lands in the first render batch after the completion message, about two round trips after the exit ends
- TAB-PTR-01, TAB-KEY-10 — "TAB-PTR-01, TAB-KEY-10" row (added 2026-10-27, design point 8 of the Tabs track, settled by the orchestrator 2026-10-26): a user activation is committed inside the click or the focus on WebAssembly, through the `select` decision; on Server the `tabs` module reports it and the commit lands a round trip later

## 10. Positioning

Not applicable — no part floats; the Indicator is positioned by consumer CSS from the measured variables of §5.5.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` / state attributes | [rendering-and-composition](../architecture/rendering-and-composition.md) | Root `'div'`, List `'div'` (through the composite root part), Tab `'button'`, Indicator `'span'`, Panel `'div'`. Mapping on every part: `tabActivationDirection` → `data-activation-direction` (always emitted); Panel adds `transitionStatusMapping`; Indicator adds `activeTabPosition`/`activeTabSize` → nothing. Tab merge order: composite item props → Tab's own attributes/handlers → consumer props → button props getter (function form). Panel: Panel attributes → consumer props. Indicator: `{ role, style, hidden }` → consumer props → `{ suppressHydrationWarning }`. |
| `useBaseUiId` | [rendering-and-composition](../architecture/rendering-and-composition.md) | Tab: `useBaseUiId(idProp)`; Panel: `useBaseUiId()` (no override). |
| `CompositeRoot` / `useCompositeItem` / `CompositeList` / `useCompositeListItem` | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) | List: `orientation`, `loopFocus`, `enableHomeAndEndKeys: true`, `disabledIndices: []`, `highlightedIndex` owned by the List (initial `0`) with `onHighlightedIndexChange` = the List's setter (TAB-STA-19; also called by the active Tab, TAB-FOC-04), `onMapChange` → Root tab registry, `stopEventPropagation` (default `true`), no `grid`, no `modifierKeys`, `highlightItemOnHover` default `false`. Tab: composite item with metadata `{ disabled, id, value }`; marker `data-composite-item-active` when active. Root: a second list (`CompositeList`) for Panels (index only, no metadata); Panel: `useCompositeListItem()` for `data-index`. |
| `useButton` + `useFocusableWhenDisabled` | [button-behavior](../architecture/button-behavior.md) | Tab: `{ disabled, native: nativeButton, focusableWhenDisabled: true }`, composite inferred (`true`), `tabIndex` default unused (composite emits none). Yields `type="button"` (native), `aria-disabled` per UBT-ARIA-01/02, never `disabled`, composite Space-on-keydown activation, disabled gating of click/pointerdown/keydown/keyup. |
| `useControlled` | [shared-utilities](../architecture/shared-utilities.md) | `{ controlled: value, default: defaultValue ?? 0, name: 'Tabs', state: 'value' }`. |
| `createChangeEventDetails` / reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | User: `('none', click/focus nativeEvent, undefined, { activationDirection: 'none' })`, then the Root overwrites `activationDirection` with the computed value before invoking the consumer. Automatic: `(reason, undefined, undefined, { activationDirection: 'none' })`. |
| `useTransitionStatus` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | Panel: `(open)` — idle state off, ending not deferred, no initial-open animation. |
| `useOpenChangeComplete` / `useAnimationsFinished` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | Panel: `{ open, ref: panel element, onComplete: unmount if still closed }`, `enabled` default, `batch: false`, no starting-attribute wait. |
| `PrehydrationScript` / `CSPProvider` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) (TRN-DOM-11, TRN-ALG-08), [shared-utilities](../architecture/shared-utilities.md) (UTL-DOM-34/35) | Indicator: rendered after the element when `renderBeforeHydration`; script source `#prehydration/tabs/indicator`. Port note 2026-10-27: not used by the port (ADR 06 Amendments 2026-10-26; TAB-API-11 withdrawn). |
| `DirectionProvider` / `useDirection` | [shared-utilities](../architecture/shared-utilities.md) | Read by the composite root for arrow mapping (UTL-I18N-01..03); Tabs reads nothing else. |
| `getCssDimensions`, `getElementTransform`, `ownerDocument`/`ownerWindow`, `activeElement`, `contains` | [shared-utilities](../architecture/shared-utilities.md) | Indicator measurement (UTL-DOM-23/24); Tab highlight sync (UTL-DOM-18..20). |
| `inertValue` | [shared-utilities](../architecture/shared-utilities.md) | Panel `inert` from `!open` (UTL-DOM-16). |
| `ResizeObserver` | native | List observes itself and every Tab element; Indicator re-renders per callback (TAB-DOM-30). |
| `useControlled` / `error` diagnostics | [shared-utilities](../architecture/shared-utilities.md) | TAB-EDGE-03/04 with `name: 'Tabs'`, `state: 'value'`. |

## 12. Edge cases, invariants and dev warnings

- **TAB-EDGE-01** — MUST. Rendering `Tabs.List`, `Tabs.Tab`, `Tabs.Indicator` or `Tabs.Panel` without an ancestor `Tabs.Root` throws an `Error` (in all builds) with the message `Base UI: TabsRootContext is missing. Tabs parts must be placed within <Tabs.Root>.`
  Source: `tabs/root/TabsRootContext.ts:45-54`. Test: [L2] `TabsPanel.test.tsx` "throws a descriptive error when rendered outside <Tabs.Root>".
  Port note 2026-10-27 (ADR 02 Amendments 2026-10-20: errors in every build, messages start with `Blazix: `): the message is ``Blazix: The `TabsRoot` context is missing. Tabs parts must be placed within `<TabsRoot>`.``
- **TAB-EDGE-02** — MUST. Rendering `Tabs.Tab` or `Tabs.Indicator` inside a Root but without an ancestor `Tabs.List` throws an `Error` with the message `Base UI: TabsListContext is missing. TabsList parts must be placed within <Tabs.List>.` (The composite-item lookup would otherwise fail with CMP-STR-02; the List lookup runs first for Tab and after the Root lookup for Indicator.)
  Source: `tabs/list/TabsListContext.ts:13-22`; `tabs/tab/TabsTab.tsx:42-53`; `tabs/indicator/TabsIndicator.tsx:47-50`. Test: [L2] `TabsTab.test.tsx` "throws a descriptive error when rendered outside <Tabs.List>".
  Port note 2026-10-27 (ADR 02 Amendments 2026-10-20): the message is ``Blazix: The `TabsList` context is missing. `TabsTab` and `TabsIndicator` must be placed within `<TabsList>`.`` A Tab outside a Root reports TAB-EDGE-01's message first.
- **TAB-EDGE-03** — SHOULD. In development, switching `value` between `undefined` and defined after the first render logs once the controlled-state error of UTL-EDGE-22 with `name: 'Tabs'`, `state: 'value'` (`Base UI: A component is changing the uncontrolled value state of Tabs to be controlled.` / `…the controlled value state of Tabs to be uncontrolled.`, followed by the standard explanatory lines).
  Source: `tabs/root/TabsRoot.tsx:48-53`; `packages/utils/src/useControlled.ts:48-64`. Test: none.
- **TAB-EDGE-04** — MUST. In development, changing `defaultValue` after the first render of an uncontrolled Root logs once `Base UI: A component is changing the default value state of an uncontrolled Tabs after being initialized. To suppress this warning opt to use a controlled Tabs.` (UTL-EDGE-23) and the new default is ignored: the selection, including an honored disabled explicit default (TAB-STA-07), is unchanged.
  Source: `packages/utils/src/useControlled.ts:66-80`; `tabs/root/TabsRoot.tsx:230-232`. Test: [L2] `TabsRoot.test.tsx` "continues honoring an initially disabled explicit defaultValue after defaultValue changes" (`toErrorDev`).
- **TAB-EDGE-05** — SHOULD. In development, a Tab whose rendered element does not match `nativeButton` logs once the mode-mismatch error of UBT-EDGE-01 (native mode, non-`<button>` element) or UBT-EDGE-02 (non-native mode, `<button>` element), prefixed `Base UI: `.
  Source: `internals/use-button/useButton.ts:36-66`. Test: none in `tabs/`.
- **TAB-EDGE-06** — MUST. Duplicate Tab values are not a supported configuration, but the implementation stays consistent: every lookup takes the first registered match (TAB-STA-05), `aria-labelledby` of a Panel references the first Tab with its value, every duplicate Tab is `active` at once, and the automatic selection never re-emits for an unchanged value (TAB-STA-10).
  Source: `tabs/root/TabsRoot.tsx:170-180,207-214,297-304`. Test: [L2] `TabsRoot.test.tsx` "does not emit a second change when the fallback resolves to the current value".
- **TAB-EDGE-07** — MUST. Values are compared by strict identity only: two structurally equal objects are different values; a function value is never invoked; `0`, `''`, `false` and `NaN` are legal values (`NaN` never equals itself, so a `NaN` value selects nothing).
  Source: `tabs/tab/TabsTab.tsx:69`; `tabs/root/TabsRoot.tsx:64,122,173,209,249,355`. Test: [L2] `TabsRoot.test.tsx` "should support values of different types", "should support a function as the default value". Docs: CHANGELOG.md v1.9.0 "Fix selecting tabs with function values".
- **TAB-EDGE-08** — MUST. Without a `ResizeObserver` implementation the List installs no observer and the Indicator re-measures only on render-driven updates (selection, registry, direction changes); the pre-hydration script likewise skips its retry loop.
  Source: `tabs/list/TabsList.tsx:42-44`; `tabs/indicator/prehydrationScript.template.js:129-131`. Test: none.
- **TAB-EDGE-09** — MUST. *Server rendering.* The automatic disabled/missing fallback (§4.3) runs only on the client after Tabs register, so server-rendered markup reflects the raw default (`0` when implicit): if that Tab is disabled, the server output shows it selected and the client falls back after hydration. The source documents the workaround: set `defaultValue`/`value` to an enabled Tab's value. Server output also has `data-index="-1"` on Panels (TAB-DOM-17) and a hidden Indicator unless `renderBeforeHydration` (TAB-DOM-34).
  Source: `tabs/tab/TabsTab.tsx:261-268` (JSDoc); `tabs/root/TabsRoot.tsx:240`. Test: none. Docs: `Tabs.Tab` `disabled` prop description.
- **TAB-EDGE-10** — MUST. A List with no Tabs renders an empty `tablist`; a Root with a List whose Tabs are all removed after registering commits `null` with `'missing'` (TAB-STA-11), hides every `keepMounted` Panel and drops their `aria-labelledby`.
  Source: `tabs/root/TabsRoot.tsx:263-274`; `tabs/panel/TabsPanel.tsx:53,69`. Test: [L2] `TabsRoot.test.tsx` "calls onValueChange with null when the selected tab is removed and no tabs remain", "calls onValueChange with null when a populated tab list is replaced by an empty one".
- **TAB-EDGE-11** — MUST. A Tab whose rendered element is swapped by element substitution (e.g. `<button>` → `<a>` when `nativeButton` changes) keeps its registry entry, its resize observation (TAB-DOM-30) and its roving `tabindex`; a substitute component that re-creates its DOM handle on every render must not cause an update loop (the registration and observation are attached from the handle callback and settle).
  Source: `tabs/tab/TabsTab.tsx:74-79,188`; `internals/composite/list/useCompositeListItem.ts:59-82`. Test: [L2] `TabsTab.test.tsx` "settles when the rendered component recreates its merged ref"; `TabsIndicator.test.tsx` "keeps observing a tab whose rendered element type changes" (L4 for the resize-observation and `tabindex` clauses). Docs: CHANGELOG.md v1.8.0 "Prevent update loops from unstable refs".
  Port note 2026-10-27 (ADR 06 Amendments 2026-10-18 rule 2; ADR 13 §3): "settles when the rendered component recreates its merged ref" tests React's callback refs and is restated as an element replacement that keeps the registration (L2, "port: a tab whose rendered element is replaced keeps its registration"), the activation and the roving `tabindex` (L4, "port: a Tab whose element is replaced keeps its registration, its activation and its tabindex"). A Blazor element reference is not re-created by a render, so the update-loop clause has no counterpart in the port.
- **TAB-EDGE-12** — MUST. A consumer `id` passed to `Tabs.Panel` lands on the element (RND-DOM-19) but the Root's panel registry and the Tab's `aria-controls` keep using the generated id, so the reference no longer resolves (§16 Q1). A port MAY fix this; the 1.9.0 behavior is as stated.
  Source: `tabs/panel/TabsPanel.tsx:45,71,78,101`. Test: none.
  Port note 2026-10-27 (design point 9 of the Tabs track, settled by the orchestrator 2026-10-26; §16 Q1 resolved): the port keeps the 1.9.0 behavior as stated; L2 "port: a consumer id lands on the panel while aria-controls keeps the generated id".
- **TAB-EDGE-13** — MUST. `disabled` on a Tab affects only activation and automatic selection; a consumer-owned `value` pointing at a disabled Tab is rendered selected (`aria-selected="true"`, `data-active`, its Panel open), and in an uncontrolled Root an honored explicit default behaves the same (TAB-STA-07).
  Source: `tabs/root/TabsRoot.tsx:240-243,286-292`. Test: [L2] `TabsRoot.test.tsx` "should still honor explicit value prop even if it points to a disabled tab", "should still honor explicit defaultValue even if it points to a disabled tab".
- **TAB-EDGE-14** — MUST. Indicator measurement is defensive: a List scaled to zero (bounding rects collapse) yields layout offsets and the CSS size (TAB-DOM-28), a Tab or ancestor with a non-translation transform yields layout offsets, and a scrolled container between the List and the Tab is compensated (TAB-DOM-26); none of these hides the Indicator while the Tab has a non-zero CSS size.
  Source: `tabs/indicator/TabsIndicator.tsx:77-78,94-113,204-228`. Test: [L4] `TabsIndicator.test.tsx` "falls back to offset positions when the tab list is scaled to zero", "accounts for a scrolled container between the tab list and the active tab".

## 13. Behaviors evidenced by tests

Coverage of each test by the requirements above. Environment notes: tests marked `skipIf(isJSDOM)` run only in a real browser (Chromium; the whole suite is skipped on Safari because of `scrollLeft` assignment behavior); `describe.each` keyboard groups run the RTL case only in the browser; the Suspense test needs React ≥ 19; `globalThis.BASE_UI_ANIMATIONS_DISABLED` is `true` except inside the "animations" blocks (TRN-EDGE-01). Test-derived requirements have been placed in their areas (notably TAB-DOM-31, TAB-FOC-07, TAB-EDGE-11, TAB-EVT-06); no requirement exists solely to carry a test.

### `root/TabsRoot.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Tabs.Root value={0} />`, `refInstanceof: HTMLDivElement`) | TAB-STR-01; RND conformance |
| "should accept a null child" / "should support empty children" | TAB-STR-01 |
| "puts the selected child in tab order" | TAB-DOM-09, TAB-FOC-01, TAB-FOC-02, TAB-FOC-04 |
| "sets the aria-labelledby attribute on tab panels to the corresponding tab id" | TAB-ARIA-07, TAB-ARIA-08, TAB-API-10 |
| "sets the aria-controls attribute on tabs to the corresponding tab panel id" | TAB-ARIA-05, TAB-DOM-13 |
| "sets aria-controls on the first tab when no value is provided" | TAB-API-02, TAB-DOM-04, TAB-ARIA-05, TAB-ARIA-08 |
| "syncs aria-controls to the mounted tab panel when keepMounted is false" | TAB-API-13, TAB-STA-21, TAB-DOM-12, TAB-ARIA-05, TAB-ANI-03, TAB-ANI-05 |
| "cleans and replaces panel registrations in Strict Mode" | TAB-STA-21, TAB-ARIA-05, TAB-ANI-05 |
| "should pass selected prop to children" | TAB-API-01, TAB-STA-01, TAB-ARIA-04 |
| "should support values of different types" | TAB-API-07, TAB-API-12, TAB-ARIA-08, TAB-EDGE-07 |
| "should support a function as the default value" | TAB-API-02, TAB-STA-01, TAB-EDGE-07 |
| "should select the second tab when the first one is disabled" | TAB-API-03, TAB-STA-06, TAB-DOM-14 |
| "should select the third tab when first two tabs are disabled" | TAB-STA-05, TAB-STA-06 |
| "should still honor explicit defaultValue even if it points to a disabled tab" | TAB-STA-07, TAB-EDGE-13 |
| "continues honoring an initially disabled explicit defaultValue after defaultValue changes" | TAB-API-02, TAB-STA-07, TAB-EDGE-04 |
| "should still honor explicit value prop even if it points to a disabled tab" | TAB-API-01, TAB-STA-13, TAB-EDGE-13 |
| "does not set tabIndex=0 on disabled tabs when they are programmatically selected" | TAB-DOM-09, TAB-FOC-06 |
| "does not select any tab when all tabs are disabled" | TAB-STA-02, TAB-STA-06, TAB-ARIA-04, TAB-DOM-14 |
| "when `activateOnFocus = true` should call onValueChange on pointerdown" | TAB-PTR-05, TAB-PTR-09, TAB-EVT-02 |
| "should call onValueChange when clicking" (browser) | TAB-PTR-01, TAB-EVT-02 |
| "should not call onValueChange on non-main button clicks" | TAB-PTR-04 |
| "should not call onValueChange when already active" | TAB-PTR-02, TAB-EVT-05 |
| "when `activateOnFocus = true` should call onValueChange if an unactive tab gets focused" | TAB-API-05, TAB-KEY-10 |
| "when `activateOnFocus = false` should not call onValueChange if an unactive tab gets focused" | TAB-API-05, TAB-KEY-11 |
| "calls onValueChange when auto-selecting the first tab on mount" | TAB-STA-08, TAB-EVT-04 |
| "calls onValueChange with the selected value when the implicit default matches a later tab" | TAB-STA-08 |
| "calls onValueChange when the implicit first tab is disabled" | TAB-STA-08, TAB-STA-09 |
| "does not cancel automatic value changes" | TAB-EVT-01, TAB-EVT-04 |
| "does not move an uncontrolled selection when a user-initiated change is canceled" | TAB-STA-03, TAB-EVT-03 |
| "calls onValueChange with null when all tabs are initially disabled" | TAB-STA-06, TAB-STA-08, TAB-EVT-04 |
| "does not emit missing when an enabled tab appears after all tabs were disabled" | TAB-STA-12, TAB-EVT-05 |
| "does not call onValueChange on initial render when defaultValue is provided" | TAB-STA-08, TAB-EVT-05 |
| "does not call onValueChange on initial render when defaultValue is null" | TAB-API-02, TAB-STA-02, TAB-STA-08 |
| "treats defaultValue={undefined} as an implicit default when the first tab is disabled" | TAB-API-02, TAB-STA-08 |
| "calls onValueChange when the selected tab becomes disabled" | TAB-STA-04, TAB-STA-06, TAB-STA-09 |
| "calls onValueChange when an explicit disabled default becomes disabled again" | TAB-STA-07, TAB-STA-09 |
| "calls onValueChange when the selected tab becomes disabled with keepMounted panels" | TAB-STA-06, TAB-DOM-14 |
| "calls onValueChange when the selected tab is removed" | TAB-STA-04, TAB-STA-06, TAB-STA-09, TAB-FOC-04 |
| "calls onValueChange with null when the selected tab is removed and no tabs remain" | TAB-STA-11, TAB-EDGE-10 |
| "calls onValueChange with null when a populated tab list is replaced by an empty one" | TAB-STA-11, TAB-ARIA-08, TAB-EDGE-10 |
| "calls onValueChange when an explicit defaultValue points at a tab that is never present" | TAB-STA-07 |
| "does not emit a second change when the fallback resolves to the current value" | TAB-STA-05, TAB-STA-10, TAB-EVT-05, TAB-EDGE-06 |
| "does not call onValueChange when a controlled selected tab becomes disabled" | TAB-API-01, TAB-STA-13, TAB-EVT-05 |
| "keeps a roving focus entry point when a controlled selected tab is removed" | TAB-STA-13, TAB-KEY-13, TAB-FOC-08 |
| "does not add aria-orientation by default" | TAB-ARIA-01, TAB-ARIA-02 |
| "adds the proper aria-orientation when vertical" | TAB-API-04, TAB-ARIA-02 |
| "selects the clicked tab" | TAB-API-13, TAB-DOM-14, TAB-PTR-01 |
| "does not select the clicked disabled tab" | TAB-API-08, TAB-PTR-03 |
| horizontal ltr / horizontal rtl (browser) / vertical — "moves focus to the last tab without activating it if focus is on the first tab" / "moves focus to the first tab without activating it if focus is on the last tab" | TAB-KEY-01, TAB-KEY-02, TAB-KEY-04, TAB-KEY-09, TAB-KEY-11; rtl: TAB-I18N-01 |
| … — "moves focus to the previous tab without activating it" / "moves focus to the next tab without activating it" | TAB-KEY-01, TAB-KEY-02, TAB-KEY-09, TAB-KEY-11 |
| … — "moves focus to a disabled tab without activating it" (previous/next, `activateOnFocus` false and default) | TAB-KEY-07, TAB-KEY-15, TAB-DOM-10 |
| … — "moves focus to the last tab while activating it if focus is on the first tab" / "moves focus to the first tab while activating it if focus is on the last tab" | TAB-KEY-04, TAB-KEY-10, TAB-KEY-14 |
| … — "moves focus to the previous tab while activating it" / "moves focus to the next tab while activating it" | TAB-KEY-10, TAB-KEY-14 |
| … — "does not move focus when modifier key: $modifierKey is pressed" (4 variants: Shift, Control, Alt, Meta) | TAB-KEY-08, TAB-KEY-13 |
| Home — "when `activateOnFocus = false`, moves focus to the first tab without activating it"; End — "when `activateOnFocus = false`, moves focus to the last tab without activating it" | TAB-KEY-06, TAB-KEY-11 |
| Home — "when `activateOnFocus = true`, moves focus to the first tab while activating it"; End — "when `activateOnFocus = true`, moves focus to the last tab while activating it" | TAB-KEY-06, TAB-KEY-10 |
| Home / End — "when `activateOnFocus = $activateOnFocus`, moves focus to a disabled tab without activating it" (2 variants each: `false`, `true`) | TAB-KEY-06, TAB-KEY-07 |
| "should allow to focus first tab when there are no active tabs" | TAB-STA-19, TAB-FOC-01, TAB-FOC-02 |
| "should set the `data-activation-direction` attribute on the tabs root with orientation=horizontal" / "should set the `data-activation-direction` attribute on the tabs root with orientation=vertical" (browser) | TAB-STA-14, TAB-STA-15, TAB-DOM-02, TAB-DOM-18, TAB-ANI-01 |
| "should update `data-activation-direction` on programmatic value changes with orientation=horizontal" / "should update `data-activation-direction` on programmatic value changes with orientation=vertical" (browser) | TAB-STA-14, TAB-STA-15, TAB-DOM-02, TAB-DOM-07, TAB-DOM-18 |
| "keeps activation direction none after automatic disabled fallback" (browser) | TAB-STA-06, TAB-STA-16 |
| "resets `data-activation-direction` when the selection is cleared and restored" (browser) | TAB-STA-16, TAB-DOM-02, TAB-DOM-19 |
| "should update `data-activation-direction` on programmatic change after a canceled click" (browser) | TAB-STA-18, TAB-EVT-03 |
| "should update `data-activation-direction` on programmatic change after a controlled parent ignores click" (browser) | TAB-STA-18, TAB-EVT-02 |
| "should compute correct direction when adding and selecting a new tab in one controlled update" (browser) | TAB-STA-16, TAB-STA-17 |
| "should compute correct direction on final render when adding and selecting a new tab in one controlled update with out of order string values" (browser) | TAB-STA-17 |
| "keeps a nested root independent from the one hosting its panel" | TAB-STR-06 |
| "works inside Popover" / "works inside Dialog" | TAB-FOC-10 |
| "keeps controlled async activation and focus aligned with activateOnFocus=%s" (2 variants: `true`, `false`) | TAB-FOC-07, TAB-KEY-11, TAB-KEY-15 |
| "when focus is outside the tablist, highlight follows the new active tab (tabIndex=0 moves)" | TAB-FOC-04, TAB-FOC-12 |
| "when focus is inside the tablist, highlight stays put on external change and arrow keys continue from the focused tab" | TAB-FOC-03, TAB-FOC-05 |

### `list/TabsList.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Tabs.List />` inside Root, `refInstanceof: HTMLDivElement`) | TAB-STR-02; RND conformance |
| "sets the aria-selected attribute on the active tab" | TAB-STA-03, TAB-ARIA-04, TAB-PTR-01 |
| "does not wrap focus past the first tab when `loopFocus` is false" / "does not wrap focus past the last tab when `loopFocus` is false" | TAB-API-06, TAB-KEY-05 |
| "moves focus to a tab disabled with the `disabled` prop" | TAB-KEY-07 |
| "skips a natively disabled tab in a single keypress" | TAB-KEY-07, TAB-DOM-11 |
| "moves the tab stop off a hidden successor when the highlighted tab is removed" | TAB-DOM-08, TAB-FOC-08 |
| "can fall back to a tab that is focusable when disabled" | TAB-FOC-08 |
| "keeps the tab stop on the selected tab when focus is inside the list" | TAB-FOC-05 |
| "keeps tracking a successor through subsequent removals" | TAB-FOC-08 |
| "can be named via `aria-label`" / "can be named via `aria-labelledby`" | TAB-ARIA-01, TAB-ARIA-10 |

### `tab/TabsTab.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Tabs.Tab value="1" />` inside Root/List, `refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true` with the non-native `div` re-run) | TAB-STR-03, TAB-API-09; RND conformance (RND-DOM-49) |
| "renders as an anchor and toggles selection when `nativeButton` is false" | TAB-API-09, TAB-ARIA-03, TAB-ARIA-04, TAB-PTR-01, TAB-KEY-16 |
| "renders a react-router Link" | TAB-API-09 |
| "settles when the rendered component recreates its merged ref" | TAB-DOM-09, TAB-EDGE-11 |
| "throws a descriptive error when rendered outside <Tabs.List>" | TAB-EDGE-02 |
| "does not re-commit the value when the active tab is pressed" | TAB-PTR-02 |
| "does not activate a disabled tab that is pressed and focused" | TAB-DOM-10, TAB-KEY-12, TAB-PTR-03, TAB-PTR-07 |
| "does not activate a tab focused by a held secondary-button press" | TAB-PTR-06 |
| "activates on focus again once a secondary-button press has ended" | TAB-PTR-06, TAB-KEY-10 |
| "activates on focus again once a secondary-button press is cancelled" | TAB-PTR-06 |
| "activates the focused tab with %s when `activateOnFocus` is false" (2 variants: Enter, Space) | TAB-KEY-11 |
| "exposes tab activation direction through the render prop" (browser) | TAB-STA-14, TAB-STA-20 |

### `panel/TabsPanel.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Tabs.Panel value="1" keepMounted />` inside Root, `refInstanceof: HTMLDivElement`) | TAB-STR-05; RND conformance |
| "throws a descriptive error when rendered outside <Tabs.Root>" | TAB-EDGE-01 |
| "keeps the surviving registration when a shadowed panel unmounts" | TAB-STR-05, TAB-STA-21, TAB-ARIA-05 |
| "sets the panel index data attribute" | TAB-DOM-17 |
| "renders a panel that suspends when opened with the boundary outside the root" (React ≥ 19) | TAB-STA-11, TAB-EVT-06 |
| "triggers enter animation via data-starting-style when mounting" (browser) | TAB-DOM-12, TAB-DOM-18, TAB-ANI-01 |
| "applies data-ending-style before unmount" (browser) | TAB-DOM-18, TAB-ANI-03 |

### `indicator/TabsIndicator.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Tabs.Indicator />` inside Root/List with one Tab, `refInstanceof: HTMLSpanElement`, `testRenderPropWith: 'div'`) | TAB-STR-04, TAB-DOM-32; RND conformance |
| "exposes null active tab state when the selected value has no matching tab" | TAB-STA-20, TAB-DOM-20, TAB-DOM-21, TAB-DOM-23 |
| "should not render when no tab is active" (browser) | TAB-DOM-19 |
| "should set CSS variables corresponding to the active tab" (browser) | TAB-DOM-24, TAB-DOM-25, TAB-DOM-27, TAB-DOM-29 |
| "should update the position and movement variables when the active tab changes" (browser) | TAB-DOM-21, TAB-DOM-24 |
| "should update the position variables when the tab list is resized" (browser) | TAB-DOM-30 |
| "should account for scroll and border when the tab list is transformed" (browser) | TAB-DOM-27 |
| "accounts for a scrolled container between the tab list and the active tab" (browser) | TAB-DOM-26, TAB-DOM-30, TAB-EDGE-14 |
| "overlays the active tab when an ancestor has a 2D rotation" / "overlays the active tab when an ancestor has a size-preserving flip" / "overlays the active tab when an ancestor uses the rotate longhand" / "overlays the active tab when an ancestor uses a flipping scale longhand" / "overlays the active tab when an ancestor has a 3D rotation (#4837)" (browser) | TAB-DOM-23, TAB-DOM-28 |
| "sets transformed offsets relative to the tab list when the list is not the offset parent" (browser) | TAB-DOM-26 |
| "follows the active tab when it has its own transform translation" / "follows the active tab when it uses the translate longhand" / "follows the active tab when the translate longhand uses percentages" (browser) | TAB-DOM-28, TAB-DOM-29 |
| "updates position when a different tab resizes" (browser) | TAB-DOM-30 |
| "keeps observing a tab whose rendered element type changes" (browser) | TAB-DOM-30, TAB-EDGE-11 |
| "falls back to offset positions when the tab list is scaled to zero" (browser) | TAB-DOM-23, TAB-DOM-25, TAB-DOM-28, TAB-EDGE-14 |
| "updates position when a new tab is inserted and then resized" (browser) | TAB-DOM-30 |
| "updates all indicators when a different tab resizes" (browser) | TAB-STR-04, TAB-DOM-33 |
| "perf: single tab resize does not fan out excessive indicator rerenders" (browser) | TAB-DOM-31 |
| "pre-hydration rendering": "renders the inline pre-hydration script during server-side rendering" / "inlines the script contents during server-side rendering" (jsdom) / "applies the CSP nonce to the pre-hydration script" / "keeps the script during hydration and removes it afterwards" (jsdom) | TAB-API-11, TAB-DOM-34, TAB-ANI-07 |
| "pre-hydration script execution": "positions the indicator once a hidden streamed segment becomes visible" (browser) | TAB-DOM-35, TAB-DOM-36, TAB-DOM-37, TAB-DOM-38 |
| "does not resurrect the indicator when the captured tab is no longer active" / "stops once hydration has revealed the indicator" / "keeps observing until the active tab is fully measurable" (browser) | TAB-DOM-37, TAB-DOM-38, TAB-ANI-07 |

## 14. React-specific notes and porting considerations

Non-normative.

- **Update-cycle phases.** "Post-commit, pre-paint" in this document is React's layout-effect phase (`useIsoLayoutEffect`): the automatic-selection pass (`TabsRoot.tsx:240-330`), the activation-direction snapshot sync (`:98-107`), the Tab highlight sync (`TabsTab.tsx:83-109`), the Panel registration (`TabsPanel.tsx:93-102`), the List's `ResizeObserver` setup (`TabsList.tsx:41-66`) and the composite registry rebuild all run there, and state they set is flushed synchronously before paint. "After paint" (passive effects) is used only for the Indicator's resize-listener registration (`TabsIndicator.tsx:54-56`) and the Panel's completion-watcher arming (`useOpenChangeComplete`). A port must preserve the ordering: DOM committed → registries rebuilt → Root re-validates selection → Tabs re-sync tab stop → paint. Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping) and §Relaxation table (CMP-STA-10 row: registry rebuild, selection re-validation and tab-stop re-sync are post-commit C# state work, after paint plus a round trip on Server).
- **Direction computed during render.** `computeActivationDirection` is called in the Root's render function (`TabsRoot.tsx:82-91`) reading `getBoundingClientRect()` so that the first render of the newly active Panel already sees the direction (issue #3873). A port without "render-time derived state" must compute the direction synchronously at the point the value changes, before producing the new tree. Port: see decisions/04-js-interop-boundary.md §1 (rect reads belong to the JavaScript `dom` module) and decisions/05-state-store-and-event-details.md §3 (`TabsChangeEventDetails.ActivationDirection`). Corrected 2026-10-27: ADR 04 Amendments 2026-10-26 settles it. The `tabs` module measures the two Tabs when a user activation starts and hands the direction over with the activation; it writes `data-activation-direction` on the five parts in the observer callback of the batch that moves `[data-active]`, so the first render of the newly active Panel carries it (TAB-STA-14), and C# mirrors it (§16 Q15).
- **Render-time state updates.** `useTransitionStatus` sets state during render to enter `'starting'` in the same commit (TRN §8); the direction snapshot is committed in a layout effect, producing one extra synchronous render with identical output. Port: see decisions/04-js-interop-boundary.md §2 (`data-starting-style`/`data-ending-style` are JS-owned with C# mirroring).
- **Function-typed values.** `setValue(() => newValue)` (`TabsRoot.tsx:122,249`) and `useState(() => defaultProp)` exist only because React would call a function value as an updater/initializer; a port stores values opaquely. Port: see decisions/05-state-store-and-event-details.md §2.
- **Secondary-button clicks.** TAB-PTR-04 relies on React DOM dropping `click` events with `button === 2` before they reach `onClick` (its `SimpleEventPlugin` filter for Firefox's right-click `click`). A port must ignore secondary-button clicks explicitly.
- **Generated ids.** `useBaseUiId` uses `React.useId` (React ≥ 18, stable across SSR/hydration) with the `base-ui-` prefix; on React 17 the id resolves one commit late, which is why the Panel skips registration while `id == null` (`TabsPanel.tsx:94-99`). A port needs a deterministic id generator shared by server and client. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (scoped `base-ui-<n>` generator) and §Requirements relaxed or withdrawn (RND-DOM-27).
- **Strict Mode / double invocation.** Effects and callback DOM handles may run twice in development; the panel registry's owner check (`TabsRoot.tsx:145-157`) and the composite registry's dirty flag make register → unregister → register sequences idempotent ("cleans and replaces panel registrations in Strict Mode"). Composite index `-1` appears on the first render before the registry publishes (CMP-STA-15).
- **Suspense.** A Tab registration can be torn down and restored without the element leaving the document when a boundary *outside* the Root suspends; the Root distinguishes this from removal by checking `isConnected` of the last known Tab element (`TabsRoot.tsx:263-274`). Ports with no equivalent of suspension can treat "registry empty" as removal.
- **`inert` prop.** `inertValue` maps a boolean to the string `'true'` on React < 19 because older React did not know the attribute; the DOM result (attribute present/absent) is what matters (UTL-DOM-16).
- **Hydration of the Indicator.** `suppressHydrationWarning` on the Indicator and on the pre-hydration `<script>` lets the server-positioned inline style / executed script body differ from the client's first render without a hydration error; the client then recomputes. The script body is replaced by an empty string in browser bundles through the package `imports` `browser` condition (`package.json`), so client code never contains it. Port: see decisions/04-js-interop-boundary.md §2 (`Tabs.Indicator` `--active-tab-*` and `hidden` are JS-owned) and decisions/01-hosting-model-and-timing.md §Consequences (prerender is in scope for markup only). Corrected 2026-10-27: the sentence "prerender emits no JS-owned attribute" was withdrawn by ADR 04 Amendments 2026-10-18 (the handover at attach). C# renders the initial `hidden` and the consumer's style in prerender and in the first interactive render, and the Indicator's `tabs` handle owns them from attach. `renderBeforeHydration` is withdrawn for the port (ADR 06 Amendments 2026-10-26; TAB-API-11).
- **Synchronous flush on animation completion.** `useAnimationsFinished` wraps the Panel's unmount in `ReactDOM.flushSync` so no frame is painted between "exit animation finished" and "element removed" (TRN-ANI-09). Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (TRN-ANI-03/09/10 row and the optimistic convergent write of decisions/04-js-interop-boundary.md §2).
- **Stable callbacks.** `useStableCallback` wrappers (`onValueChange`, `notifyAutomaticValueChange`, `registerMountedTabPanel`, `observeTabElement`, …) keep handler identities stable so context values do not churn; they carry no behavior a port must reproduce beyond "the latest callback is invoked".
- **`useForcedRerendering`.** The Indicator re-measures by forcing a re-render from the `ResizeObserver` callback; a port with explicit measurement can call the measurement routine directly on each observation (TAB-DOM-30) and should keep the one-render-per-notification cost (TAB-DOM-31). Port: see decisions/04-js-interop-boundary.md §1 (the `tabs` component module owns the measurement loop, the `dom` module the `ResizeObserver` subscription) and §2 (JS writes the variables; C# mirrors `activeTabPosition`/`activeTabSize` one round trip late on Server).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TAB-STR-01 | MUST | `Tabs.Root` renders a `<div>` by default, owns the active value and the activation direction, provides them to every… | 2 |
| TAB-STR-02 | MUST | `Tabs.List` renders a `<div>` by default with `role="tablist"` and is the composite roving-focus root for the Tabs in… | 2 |
| TAB-STR-03 | MUST | `Tabs.Tab` renders a `<button>` by default with `role="tab"`, is a composite item of the nearest List, and MUST be a… | 2 |
| TAB-STR-04 | MUST | `Tabs.Indicator` renders a `<span>` by default and MUST be a descendant of a `Tabs.List` (it needs the List element a… | 2 |
| TAB-STR-05 | MUST | `Tabs.Panel` renders a `<div>` by default with `role="tabpanel"` and MUST be a descendant of a `Tabs.Root` (TAB-EDGE-… | 2 |
| TAB-STR-06 | MUST | A `Tabs.Root` nested inside another Root's Panel is independent: each Root wires only its own Tabs and Panels (`aria-… | 2 |
| TAB-API-01 | MUST | `value` is the consumer-owned (controlled) active value | 3.1 |
| TAB-API-02 | MUST | `defaultValue` defaults to `0` and is the initial component-owned value; it is read once (later changes are ignored a… | 3.1 |
| TAB-API-03 | MUST | `onValueChange` is optional; when absent, user activations still commit (uncontrolled) and automatic selections still… | 3.1 |
| TAB-API-04 | MUST | `orientation` defaults to `'horizontal'`; it is the `orientation` of the composite root (TAB-KEY-01), the source of `… | 3.1 |
| TAB-API-05 | MUST | `activateOnFocus` defaults to `false` | 3.2 |
| TAB-API-06 | MUST | `loopFocus` defaults to `true` and is passed unchanged as the composite root's `loopFocus` (CMP-API-01). | 3.2 |
| TAB-API-07 | MUST | `value` is required and may be any value, including `0`, strings, objects, functions, symbols and regular expressions… | 3.3 |
| TAB-API-08 | MUST | `disabled` defaults to `false` | 3.3 |
| TAB-API-09 | MUST | `nativeButton` defaults to `true` and is passed as the `native` option of the shared button behavior together with `d… | 3.3 |
| TAB-API-10 | MUST | The Tab's `id` prop, when supplied, is used verbatim as the element id and as the id published to the Root (so the ma… | 3.3 |
| TAB-API-11 | MUST | `renderBeforeHydration` defaults to `false`. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 3.4 |
| TAB-API-12 | MUST | `value` is required; the Panel is open exactly when `value === root value` (strict identity). | 3.5 |
| TAB-API-13 | MUST | `keepMounted` defaults to `false` | 3.5 |
| TAB-STA-01 | MUST | The active value is consumer-owned if `value !== undefined` on the first render, otherwise component-owned starting f… | 4.1 |
| TAB-STA-02 | MUST | The *active Tab* is the Tab whose `value === root value` (strict identity) | 4.1 |
| TAB-STA-03 | MUST | A *user activation* (click, keyboard activation, or focus with `activateOnFocus`) of an inactive, enabled Tab compute… | 4.1 |
| TAB-STA-04 | MUST | The Root's *tab registry* is the ordered list of currently connected Tabs of its List(s) in document order, each with… | 4.2 |
| TAB-STA-05 | MUST | Derived lookups, each scanning the registry in document order and taking the *first* match: (a) *selected tab metadat… | 4.2 |
| TAB-STA-06 | MUST | When the registry is non-empty and the selection is *disabled* (selected tab metadata exists with `disabled: true`) o… | 4.3 |
| TAB-STA-07 | MUST | An *explicit* `defaultValue` that points at a disabled Tab is kept selected (no fallback, no notification) for as lon… | 4.3 |
| TAB-STA-08 | MUST | With an *implicit* default, exactly one `'initial'` notification is issued when Tabs first register: if the implicit… | 4.3 |
| TAB-STA-09 | MUST | The reason of an automatic fallback is `'initial'` while the initial notification is still pending; otherwise `'disab… | 4.3 |
| TAB-STA-10 | MUST | When the fallback value equals the current value (possible only with duplicate Tab values, where the first registered… | 4.3 |
| TAB-STA-11 | MUST | When the registry becomes empty after Tabs had registered, the value is not `null`, and the last known Tab element is… | 4.3 |
| TAB-STA-12 | MUST | A `null` selection is never automatically replaced: when an enabled Tab appears (or a disabled one becomes enabled) w… | 4.3 |
| TAB-STA-13 | MUST | A consumer-owned Root performs none of TAB-STA-06..12: a controlled value that points at a disabled or missing Tab is… | 4.3 |
| TAB-STA-14 | MUST | The Root keeps a *previous value* snapshot and a committed activation direction (initially `'none'`) | 4.4 |
| TAB-STA-15 | MUST | *Position-based direction.* Given the old and new values, both non-`null`, and both resolving to registered Tab eleme… | 4.4 |
| TAB-STA-16 | MUST | The direction is `'none'` when the old or the new value is `null`/`undefined` (clearing the selection, selecting from… | 4.4 |
| TAB-STA-17 | MUST | When a value change is observed while the *new* value has no registered Tab element (a Tab added and selected in the… | 4.4 |
| TAB-STA-18 | MUST | A user activation that is canceled, or that a controlled consumer ignores, leaves the snapshot and the committed dire… | 4.4 |
| TAB-STA-19 | MUST | The List owns the composite *highlighted index*, initially `0`, passing `highlightedIndex`/`onHighlightedIndexChange`… | 4.5 |
| TAB-STA-20 | MUST | The state objects exposed to `className`/`style`/`render` contain exactly the fields listed in §3 for each part: Root… | 4.6 |
| TAB-STA-21 | MUST | The Root keeps a *mounted-panel registry* mapping a Panel value to the id of the Panel element currently rendered for it | 4.7 |
| TAB-STA-22 | MUST | Panel `open = value === root value`; `mounted` and `transitionStatus` follow the default transition-status machine of… | 4.7 |
| TAB-DOM-01 | MUST | The Root element has `data-orientation` equal to the effective `orientation` prop. | 5.1 |
| TAB-DOM-02 | MUST | The Root element always has `data-activation-direction`, whose value is the current activation direction (`"none"` in… | 5.1 |
| TAB-DOM-03 | MUST | The List element has `data-orientation` and `data-activation-direction` with the Root's values (TAB-DOM-01/02). | 5.2 |
| TAB-DOM-04 | MUST | The Tab element always has an `id`: the consumer's `id` prop verbatim, else a generated unique id prefixed `base-ui-`… | 5.3 |
| TAB-DOM-05 | MUST | The Tab element has `data-active=""` while it is the active Tab and no `data-active` attribute otherwise. | 5.3 |
| TAB-DOM-06 | MUST | The Tab element has `data-disabled=""` while its `disabled` prop is `true` and no `data-disabled` attribute otherwise. | 5.3 |
| TAB-DOM-07 | MUST | Every Tab element (active or not) has `data-orientation` and `data-activation-direction` with the Root's current valu… | 5.3 |
| TAB-DOM-08 | MUST | The active Tab element carries the composite active-item marker `data-composite-item-active=""` (CMP-DOM-02) and inac… | 5.3 |
| TAB-DOM-09 | MUST | Each Tab renders `tabindex="0"` when its composite index equals the List's highlighted index and `tabindex="-1"` othe… | 5.3 |
| TAB-DOM-10 | MUST | In native mode (`nativeButton` `true`) the Tab element has `type="button"` and **never** the `disabled` attribute, ev… | 5.3 |
| TAB-DOM-11 | MUST | When the consumer substitutes a `<button>` element that itself carries the `disabled` attribute while the Tab's `disa… | 5.3 |
| TAB-DOM-12 | MUST | The Panel element is rendered only when `keepMounted \|\| mounted` (TRN-ANI-01); otherwise the part renders nothing (an… | 5.4 |
| TAB-DOM-13 | MUST | The Panel element has a generated `base-ui-…` id (RND-DOM-27..29); this generated id is what the Root's panel registr… | 5.4 |
| TAB-DOM-14 | MUST | While the Panel is not mounted (closed and exit complete) and still rendered because of `keepMounted`, the element ha… | 5.4 |
| TAB-DOM-15 | MUST | The Panel element has the `inert` attribute (present, empty value) whenever it is not open — from the closing commit… | 5.4 |
| TAB-DOM-16 | MUST | The Panel element has `tabindex="0"` while open and `tabindex="-1"` while closed, so an open Panel is a sequential ta… | 5.4 |
| TAB-DOM-17 | MUST | The Panel element has `data-index="<n>"` where `n` is the Panel's index in document order among the *currently render… | 5.4 |
| TAB-DOM-18 | MUST | The Panel element has `data-orientation` and `data-activation-direction` with the Root's current values (so a Panel a… | 5.4 |
| TAB-DOM-19 | MUST | When the root value is `null` (or `undefined`), the Indicator renders no element at all (and no pre-hydration script)… | 5.5 |
| TAB-DOM-20 | MUST | When the root value is not `null` but the List element is not yet available or no registered Tab has that value, the… | 5.5 |
| TAB-DOM-21 | MUST | The Indicator measures the active Tab *on every render of the part* (not in a deferred phase) | 5.5 |
| TAB-DOM-22 | MUST | The Indicator element has `data-orientation` and `data-activation-direction` with the Root's values; `activeTabPositi… | 5.5 |
| TAB-DOM-23 | MUST | The Indicator element has the boolean `hidden` attribute unless an active Tab element was found **and** its measured… | 5.5 |
| TAB-DOM-24 | MUST | While an active Tab element exists, the Indicator's inline style defines exactly `--active-tab-left`, `--active-tab-r… | 5.5 |
| TAB-DOM-25 | MUST | *Size.* `width`/`height` are the active Tab's CSS dimensions per UTL-DOM-23: the computed `width`/`height` unless the… | 5.5 |
| TAB-DOM-26 | MUST | *Layout offset* (transform-immune baseline) | 5.5 |
| TAB-DOM-27 | MUST | *Rect offset* (sub-pixel-precise) | 5.5 |
| TAB-DOM-28 | MUST | *Choice of offset.* Let `(tx, ty)` be the active Tab's own 2D translation (TAB-DOM-29) | 5.5 |
| TAB-DOM-29 | MUST | *Tab translation.* `(tx, ty)` is the translation component of the Tab's computed `transform` matrix (UTL-DOM-24 / UTL… | 5.5 |
| TAB-DOM-30 | MUST | *Resize tracking.* The List installs one `ResizeObserver` (when the API exists) that observes the List element and ev… | 5.5 |
| TAB-DOM-31 | SHOULD | A single Tab resize produces fewer than five renders of the Indicator part (render count observed through bUnit… | 5.5 |
| TAB-DOM-32 | MUST | Inline-style precedence on the Indicator, lowest to highest: the six CSS variables, then the consumer's `style` (obje… | 5.5 |
| TAB-DOM-33 | MUST | Every Indicator in a List measures independently and every one of them re-measures on each resize notification of that List. | 5.5 |
| TAB-DOM-34 | MUST | With `renderBeforeHydration`, server-rendered output contains, immediately after the Indicator element, a `<script>`…. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 5.5.1 |
| TAB-DOM-35 | MUST | The script, executing synchronously from the server HTML before the client takes over, locates: the Indicator = the s…. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 5.5.1 |
| TAB-DOM-36 | MUST | The script computes `width`/`height` per TAB-DOM-25, the layout offset per TAB-DOM-26 (walking `parentElement`, which…. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 5.5.1 |
| TAB-DOM-37 | MUST | If the first attempt leaves the Indicator hidden (the active Tab is not yet measurable, e.g. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 5.5.1 |
| TAB-DOM-38 | MUST | The minified script shipped in `prehydrationScript.min.ts` is the exact compiled form of the template; a port that em…. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 5.5.1 |
| TAB-ARIA-01 | MUST | The List element has `role="tablist"` (consumer `role` overrides). | 5.6 |
| TAB-ARIA-02 | MUST | The List element has `aria-orientation="vertical"` when `orientation` is `'vertical'` and no `aria-orientation` attri… | 5.6 |
| TAB-ARIA-03 | MUST | The Tab element has `role="tab"` in both native and non-native mode (it overrides the button mechanism's `role="butto… | 5.6 |
| TAB-ARIA-04 | MUST | The Tab element always has `aria-selected`: `"true"` while active, `"false"` otherwise (including disabled and when n… | 5.6 |
| TAB-ARIA-05 | MUST | The Tab element has `aria-controls="<panel id>"` exactly while the Root's panel registry holds an entry for the Tab's… | 5.6 |
| TAB-ARIA-06 | MUST | `aria-disabled` follows the focusable-when-disabled composite rules of UBT-ARIA-01/02: in native mode the Tab always… | 5.6 |
| TAB-ARIA-07 | MUST | The Panel element has `role="tabpanel"` (consumer `role` overrides). | 5.6 |
| TAB-ARIA-08 | MUST | The Panel element has `aria-labelledby="<tab id>"` where the id is that of the first registered Tab whose value equal… | 5.6 |
| TAB-ARIA-09 | MUST | The Indicator element has `role="presentation"` (consumer `role` overrides). | 5.6 |
| TAB-ARIA-10 | MUST | The tablist's accessible name comes only from consumer attributes forwarded to the List element (`aria-label`, `aria-… | 5.6 |
| TAB-KEY-01 | MUST | The List handles exactly <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>… | 6.1 |
| TAB-KEY-02 | MUST | With `orientation` `'horizontal'`, the forward key is <kbd>ArrowRight</kbd> and the backward key <kbd>ArrowLeft</kbd>… | 6.1 |
| TAB-KEY-03 | MUST | Arrow keys outside the orientation (<kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> in a horizontal List, <kbd>ArrowLeft</kbd… | 6.1 |
| TAB-KEY-04 | MUST | With `loopFocus` `true` (default), the forward key on the last navigable Tab focuses the first navigable Tab and the… | 6.1 |
| TAB-KEY-05 | MUST | With `loopFocus={false}`, a forward key on the last navigable Tab or a backward key on the first leaves focus where i… | 6.1 |
| TAB-KEY-06 | MUST | <kbd>Home</kbd> focuses the first navigable Tab and <kbd>End</kbd> the last, in every orientation and reading directi… | 6.1 |
| TAB-KEY-07 | MUST | *Navigable Tabs.* Because the List passes an empty `disabledIndices` list, a Tab counts as non-navigable (skipped by… | 6.1 |
| TAB-KEY-08 | MUST | A navigation key pressed while <kbd>Shift</kbd>, <kbd>Control</kbd>, <kbd>Alt</kbd> or <kbd>Meta</kbd> is held does n… | 6.1 |
| TAB-KEY-09 | MUST | When a navigation key moves focus, the keydown's default is prevented and its propagation is stopped (CMP-KEY-10, `st… | 6.1 |
| TAB-KEY-10 | MUST | With `activateOnFocus`, a Tab that receives focus from keyboard navigation (or programmatically) is activated through… | 6.1 |
| TAB-KEY-11 | MUST | With `activateOnFocus` `false`, moving focus never calls `onValueChange`; the focused enabled, inactive Tab is activa… | 6.1 |
| TAB-KEY-12 | MUST | On a disabled Tab, <kbd>Enter</kbd>/<kbd>Space</kbd> MUST NOT activate it or call `onValueChange`; the button mechani… | 6.1 |
| TAB-KEY-13 | MUST | <kbd>Tab</kbd> and <kbd>Shift</kbd>+<kbd>Tab</kbd> are never intercepted: the List is one position in the sequential… | 6.1 |
| TAB-KEY-14 | MUST | Keyboard focus movement never changes a consumer-owned value by itself; with `activateOnFocus` the Root only reports… | 6.1 |
| TAB-KEY-15 | MUST | Navigation keys pressed while a disabled Tab has focus still move focus (the Tab's own keydown default-prevention doe… | 6.1 |
| TAB-KEY-16 | MUST | A non-native Tab rendered as an anchor with an `href` is activated by <kbd>Enter</kbd> through the browser's native l… | 6.1 |
| TAB-PTR-01 | MUST | A `click` on an enabled, inactive Tab (mouse, touch, pen, or keyboard-originated click) activates it through TAB-STA-… | 6.2 |
| TAB-PTR-02 | MUST | A click (or press, or keyboard activation) on the already active Tab MUST NOT call `onValueChange` and MUST NOT re-co… | 6.2 |
| TAB-PTR-03 | MUST | A click on a disabled Tab MUST NOT activate it or call `onValueChange`; the button mechanism default-prevents the cli… | 6.2 |
| TAB-PTR-04 | MUST | A `click` event whose `button` is `2` (secondary button) MUST NOT activate the Tab (see §14 for the mechanism: the fr… | 6.2 |
| TAB-PTR-05 | MUST | With `activateOnFocus`, a primary-button `pointerdown` on an enabled, inactive Tab records a *pressing* state with *m… | 6.2 |
| TAB-PTR-06 | MUST | With `activateOnFocus`, a `pointerdown` with a non-primary button (`event.button !== 0`, e.g | 6.2 |
| TAB-PTR-07 | MUST | A `pointerdown` on a disabled Tab is default-prevented by the button mechanism and the Tab's own handler does not run… | 6.2 |
| TAB-PTR-08 | MUST | Hovering a Tab has no effect: the composite root is configured without hover highlighting (CMP-PTR-01) and the Tab at… | 6.2 |
| TAB-PTR-09 | MUST | Consumer `onClick`, `onFocus`, `onPointerDown`, `onKeyDown`, `onKeyUp` and `onKeyDownCapture` handlers on a Tab run b… | 6.2 |
| TAB-FOC-01 | MUST | Exactly one Tab of a List is in the sequential tab order at any time (the *highlighted* Tab, `tabindex="0"`); all oth… | 6.3 |
| TAB-FOC-02 | MUST | *Initial tab stop.* When the Tabs first register, the highlighted index becomes the index of the Tab carrying the act… | 6.3 |
| TAB-FOC-03 | MUST | A Tab receiving focus from any source (keyboard move, pointer press, programmatic `focus()`) becomes the highlighted… | 6.3 |
| TAB-FOC-04 | MUST | When the active Tab changes while focus is **outside** the List (controlled `value` change, automatic fallback, or a… | 6.3 |
| TAB-FOC-05 | MUST | When the active Tab changes while the document's active element (resolved through shadow roots, UTL-DOM-19/20) is **i… | 6.3 |
| TAB-FOC-06 | MUST | A disabled Tab that becomes active through a consumer-owned value never receives the tab stop through TAB-FOC-04; the… | 6.3 |
| TAB-FOC-07 | MUST | A keyboard navigation step (any keydown on a Tab) marks that Tab so that its next highlight re-synchronization (TAB-F… | 6.3 |
| TAB-FOC-08 | MUST | *Removal of the highlighted Tab.* When the Tab holding the tab stop is removed (or its element replaced), the composi… | 6.3 |
| TAB-FOC-09 | MUST | An open Panel is a sequential tab stop (`tabindex="0"`, TAB-DOM-16) and a closed one is not (`tabindex="-1"`, `inert`… | 6.3 |
| TAB-FOC-10 | MUST | Focus moved by a navigation key lands on the target Tab one microtask after the keydown (CMP-FOC-02), which lets a su… | 6.3 |
| TAB-FOC-11 | MUST | When a navigation key moves the highlight, the target Tab is scrolled into view inside the List (nearest-edge alignme… | 6.3 |
| TAB-FOC-12 | MUST | Activation never moves focus by itself: a click focuses the Tab only through the browser's native behavior, a control… | 6.3 |
| TAB-FOC-13 | MUST | When the Tabs of a List first register, the Tab carrying the active marker, if any, is scrolled into view inside the List… | 6.3 |
| TAB-EVT-01 | MUST | `onValueChange` receives `(value, details)` where `details` is a change-event details object (EVD-API-01) extended wi… | 7 |
| TAB-EVT-02 | MUST | For a user activation, `onValueChange` is called synchronously inside the originating event's handling, before any st… | 7 |
| TAB-EVT-03 | MUST | Calling `details.cancel()` during a user activation prevents the commit in an uncontrolled Root (selection, `aria-sel… | 7 |
| TAB-EVT-04 | MUST | For an automatic selection (§4.3), `onValueChange` is called post-commit, pre-paint, *after* the fallback value has been scheduled… | 7 |
| TAB-EVT-05 | MUST | `onValueChange` is called exactly once per change request (EVD-EVT-03) and never for: activating the already active T… | 7 |
| TAB-EVT-06 | MUST | A user activation whose Panel content suspends (the Panel's subtree is not yet renderable when it opens, with the sus… | 7 |
| TAB-ANI-01 | MUST | When a Panel becomes open while not mounted, it becomes mounted and its transition status becomes `'starting'` in the… | 9 |
| TAB-ANI-02 | MUST | A Panel that is open on its first render (initial selection) mounts without the `'starting'` phase: no `data-starting… | 9 |
| TAB-ANI-03 | MUST | When an open Panel becomes closed, its status becomes `'ending'` in the closing commit (TRN-STA-06): `data-ending-sty… | 9 |
| TAB-ANI-04 | MUST | The completion callback re-reads the live open state: a completion arriving after the Panel was reopened (the Tab re-… | 9 |
| TAB-ANI-05 | MUST | The Panel's registration in the Root (TAB-STA-21) exists for every commit in which the Panel element is rendered and… | 9 |
| TAB-ANI-06 | MUST | When the global animations-disabled flag is set (`BASE_UI_ANIMATIONS_DISABLED`, TRN-EDGE-01) or the element has no `g… | 9 |
| TAB-ANI-07 | MUST | The Indicator's pre-hydration script (§5.5.1) runs once from server HTML; after hydration the client Indicator re-mea…. Port: withdrawn (ADR 06 Amendments 2026-10-26) | 9 |
| TAB-I18N-01 | MUST | Reading direction is taken only from the nearest `DirectionProvider` (`'ltr'` without one; `dir` attributes are not c… | 6.5 |
| TAB-I18N-02 | MUST | The activation direction is visual, not logical (TAB-STA-15): it compares bounding-rect `left` (horizontal) or `top`… | 6.5 |
| TAB-EDGE-01 | MUST | Rendering `Tabs.List`, `Tabs.Tab`, `Tabs.Indicator` or `Tabs.Panel` without an ancestor `Tabs.Root` throws an `Error`… | 12 |
| TAB-EDGE-02 | MUST | Rendering `Tabs.Tab` or `Tabs.Indicator` inside a Root but without an ancestor `Tabs.List` throws an `Error` with the… | 12 |
| TAB-EDGE-03 | SHOULD | In development, switching `value` between `undefined` and defined after the first render logs once the controlled-sta… | 12 |
| TAB-EDGE-04 | MUST | In development, changing `defaultValue` after the first render of an uncontrolled Root logs once `Base UI: A componen… | 12 |
| TAB-EDGE-05 | SHOULD | In development, a Tab whose rendered element does not match `nativeButton` logs once the mode-mismatch error of UBT-E… | 12 |
| TAB-EDGE-06 | MUST | Duplicate Tab values are not a supported configuration, but the implementation stays consistent: every lookup takes t… | 12 |
| TAB-EDGE-07 | MUST | Values are compared by strict identity only: two structurally equal objects are different values; a function value is… | 12 |
| TAB-EDGE-08 | MUST | Without a `ResizeObserver` implementation the List installs no observer and the Indicator re-measures only on render-… | 12 |
| TAB-EDGE-09 | MUST | *Server rendering.* The automatic disabled/missing fallback (§4.3) runs only on the client after Tabs register, so se… | 12 |
| TAB-EDGE-10 | MUST | A List with no Tabs renders an empty `tablist`; a Root with a List whose Tabs are all removed after registering commi… | 12 |
| TAB-EDGE-11 | MUST | A Tab whose rendered element is swapped by element substitution (e.g | 12 |
| TAB-EDGE-12 | MUST | A consumer `id` passed to `Tabs.Panel` lands on the element (RND-DOM-19) but the Root's panel registry and the Tab's… | 12 |
| TAB-EDGE-13 | MUST | `disabled` on a Tab affects only activation and automatic selection; a consumer-owned `value` pointing at a disabled… | 12 |
| TAB-EDGE-14 | MUST | Indicator measurement is defensive: a List scaled to zero (bounding rects collapse) yields layout offsets and the CSS… | 12 |

## 16. Open questions

1. **`Tabs.Panel` ignores a consumer `id` for `aria-controls`.** `tabs/panel/TabsPanel.tsx:45` always generates the id and `:101` registers that generated id, while a consumer `id` in `elementProps` (`:78`) overrides the attribute on the element. A Tab's `aria-controls` therefore references an id that no element carries whenever the consumer sets one (TAB-EDGE-12). The Tab, by contrast, honors its `id` prop (`TabsTab.tsx:55`). Likely an oversight; a port may want to register the resolved id. Untested either way. Resolved 2026-10-27 (design point 9 of the Tabs track, settled by the orchestrator 2026-10-26; the plan's "§16 port choices mirror the pinned source", accepted by the user 2026-10-26): the port mirrors the pinned source, as TAB-EDGE-12 allows ("MAY fix"). The Panel registers its generated id and a consumer `id` lands on the element only; L2 "port: a consumer id lands on the panel while aria-controls keeps the generated id" asserts the dangling reference.
2. **No index-based implicit `value`.** The assignment asked whether `value` may be omitted on Tab/Panel. In 1.9.0 `value` is required on both (`TabsTab.tsx:259`, `TabsPanel.tsx:132`; CHANGELOG v1.0.0-rc.0 "Fix Panel `keepMounted` behavior … `value` prop is now required"). Only the Root's `defaultValue` has an implicit default (`0`), which matches a Tab only if a Tab has the number value `0`.
3. **No `hiddenUntilFound` on `Tabs.Panel`.** The hint lists it; `TabsPanel.tsx:128-138` defines only `value` and `keepMounted`. Closed Panels are not searchable by find-in-page in 1.9.0.
4. **Controlled value becoming `undefined`.** `isControlled` is recomputed every render (`TabsRoot.tsx:55`) while the controlled-state hook fixes the mode at first render (`useControlled.ts:41`). If a controlled Root later renders `value={undefined}`, the automatic-selection pass starts running (`:240-243`) and may call `onValueChange` with `'missing'`/`'disabled'`, but its commits are no-ops (UTL-STA-04), so the callback could repeat on every re-validation. Not tested and warned against by TAB-EDGE-03; a port should treat the mode as fixed. Resolved (port): decisions/05-state-store-and-event-details.md §2 fixes the controlled mode at the first parameter set for the component's lifetime (EVD-STA-01), so the automatic-selection pass is skipped for a Root that started controlled.
5. **`data-index` counts rendered Panels only.** Because an unrendered Panel (TAB-DOM-12) has no element to register, `data-index` is the index among currently rendered Panels, not among all declared Panels; without `keepMounted` the open Panel is usually `data-index="0"`. The docs describe it only as "the index of the tab panel" (`TabsPanelDataAttributes.ts:3-6`). Worth confirming the intended semantics before relying on it; the single test uses one Panel. Resolved 2026-10-27: the port transcribes TAB-DOM-17 as stated, the index among the rendered Panels, through track 1's E1 (a registration-only composite list that reports the indexes of the Panels it has); `-1` until the first report.
6. **Outline omissions.** `components/page.mdx` lists Tab props as `className, disabled, nativeButton, render, style, value` but the part consumes `id` (TAB-API-10). It omits the internal `data-composite-item-active` marker on Tab (TAB-DOM-08) and the computed `hidden` on Indicator and Panel, which are attributes rather than state data attributes. No listed prop, data attribute, CSS variable or type is missing from this document.
7. **`aria-disabled="false"` on enabled native Tabs.** `focusableWhenDisabled: true` makes the shared mechanism emit `aria-disabled` with the stringified boolean in native mode (`useFocusableWhenDisabled.ts:38-43`, UBT-ARIA-01), so every enabled native Tab renders `aria-disabled="false"`. Harmless but noisy; no Tabs test asserts it. A port must reproduce it to pass a translated UBT suite, but reviewers may want to confirm it is intentional. Resolved 2026-10-27: reproduced by `ButtonAttributeWriter.Write(…, focusableWhenDisabled: true, …)`, which renders `aria-disabled="false"` on an enabled native Tab; L2 "port: a native tab is a button of type button with aria-disabled and never the disabled attribute".
8. **Initial tab stop on a disabled active Tab.** On first registration the composite root highlights the marked active item regardless of `aria-disabled` (`useCompositeRoot.ts:140-150`), so `value={0}` with a disabled Tab `0` gives that disabled Tab `tabindex="0"`, whereas a later programmatic selection of a disabled Tab is refused by the Tab's sync (`TabsTab.tsx:106`, TAB-FOC-06). The two paths are inconsistent; untested. Documented as-is in TAB-FOC-02/06.
9. **Provisional value-based direction for strings.** `computeActivationDirection`'s fallback (`TabsRoot.tsx:386-392`) compares strings lexicographically, so adding-and-selecting `'Account'` after `'Overview'` first renders `'left'` and corrects to `'right'` once the element registers (TAB-STA-16/17). The test pins only the final render; an intermediate frame with the wrong direction is possible if a paint happens before the corrective render (it should not, since the registry publishes pre-paint). Resolved 2026-10-27 (design point 7 of the Tabs track, settled by the orchestrator 2026-10-26): the provisional guess is never needed. The `tabs` module reads the applied batch, which already contains the new Tab, so the first direction it writes is the positional one (TAB-STA-16's note; L4 "should compute correct direction on final render when adding and selecting a new tab in one controlled update with out of order string values" asserts it on the created Panel). One case stays inexact: a controlled change whose old Tab leaves the document in the same batch writes `none`, where Base UI compares the stale element's zero rect (TAB-STA-16/17's notes).
10. **Composite algorithm IDs.** `composite-and-list-navigation.md` references `CMP-ALG-02/04/05/15/19` (disabled detection, step, min/max, fallback index, scroll-into-view) but its §5 does not yet define them at the time of writing; this document cites the behavioral IDs (CMP-STA-23..30, CMP-KEY-01..17, CMP-PTR-02, CMP-FOC-01/02) and names the algorithms in prose (TAB-KEY-07, TAB-FOC-08, TAB-FOC-11). Re-check the citations once that document is complete. Resolved (QA 2026-10-14): all five IDs now exist in `composite-and-list-navigation.md` §5 with the described content — CMP-ALG-02 (disabled detection), CMP-ALG-04 (step), CMP-ALG-05 (min/max), CMP-ALG-15 (fallback index), CMP-ALG-19 (scroll into view) — and are cited in the `Source:` lines of TAB-KEY-04/06/07, TAB-FOC-02/08/11 (plus CMP-ALG-03/14/18 where applicable). The highlight sync CMP §10 Q9 asks about is stated in TAB-STA-19/TAB-FOC-04: Tabs use `highlightedIndex`/`onHighlightedIndexChange`, not the active marker.
11. **Indicator does not track scrolling.** Scrolling the List or an intermediate container moves the active Tab on screen but triggers no re-measure (test comment at `TabsIndicator.test.tsx:337-339`); only resizes and render-driven updates do. For the List's own scroll this is by design (the Indicator scrolls with the Tabs); for an intermediate scroll container the variables are stale until the next re-measure. Documented in TAB-DOM-30.
12. **Secondary-button click filtering is framework behavior.** TAB-PTR-04 is satisfied in Base UI only because React never dispatches `onClick` for `button === 2` (`TabsTab.tsx:132-138` has no check). A port must add the check explicitly; middle-button (`button === 1`) presses normally produce `auxclick`, not `click`, and are not covered by any test. Resolved 2026-10-27 (settled by the orchestrator 2026-10-25): the `tabs` module checks `button !== 0` explicitly (TAB-PTR-04's note). It does not listen for `auxclick`, so a middle-button press activates nothing, as in Base UI.
13. **`Tabs.Panel.Metadata` is declared but unused.** The Root's panel list is typed with it (`TabsRoot.tsx:346`) but Panels register no metadata (`TabsPanel.tsx:47`); the type exists only for the public API surface. No behavior depends on it.
14. **Server-rendered disabled default.** TAB-EDGE-09 documents that the server output selects the raw default even when that Tab is disabled (JSDoc at `TabsTab.tsx:261-268`); the client then changes the selection after hydration (an `'initial'`/`'missing'`/`'disabled'` notification and a visible switch). Whether a port should pre-compute the fallback on the server (it can, since it knows the disabled props) is a design decision that would diverge from 1.9.0 observable behavior only in the server markup. Resolved 2026-10-27 (settled by the orchestrator 2026-10-25): the port mirrors 1.9.0. Prerender shows the raw default with no server-side fallback, and the fallback runs once the composite's ordered-ids report arrives (TAB-EDGE-09; §9).
15. **Rect reads for the activation direction and the pre-hydration script under the port's hosting model.** TAB-STA-15 reads the old and new Tabs' bounding rects synchronously while the new tree is produced (`TabsRoot.tsx:82-91,111`, `:374-406`), and TAB-DOM-34..37 emit an inline script during server rendering (`TabsIndicator.tsx:170`). decisions/04-js-interop-boundary.md §1 places rect reads in JavaScript and decisions/01-hosting-model-and-timing.md §Decision item 1 forbids C# code that depends on pre-paint execution or same-task commit, and its §Consequences treats prerender as markup only. No ADR yet says whether the direction is computed in the `tabs` JavaScript module before the activation is reported to C# (so `details.activationDirection` and `data-activation-direction` are correct on the first render), or whether `renderBeforeHydration` is kept. Needs a decision before Tabs is implemented. Decided (2026-10-18): the `tabs` module measures the two Tab rects on activation and reports `activationDirection` with the index; the pre-hydration script is replaced by the initial C# render plus the module on attach (ADR 04 Amendments). Resolved 2026-10-27 for the parts left open: ADR 04 Amendments 2026-10-26 (the `tabs` module's activation, the `select` point on the `tabs` twin and `data-activation-direction` on the five parts) and ADR 06 Amendments 2026-10-26 (the pre-hydration rows withdrawn: TAB-API-11, TAB-DOM-34..38, TAB-ANI-07).
