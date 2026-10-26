# Accordion

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/accordion/` |
| Tests | `packages/react/src/accordion/**/*.test.tsx` (5 files) + `root/AccordionRoot.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/accordion/page.mdx` |
| Prefix | `ACC` |
| Status | approved |
| Revised | 2026-10-26 |
| Builds on | [collapsible](collapsible.md) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) (item registry only), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Accordion is a vertically stacked set of disclosure sections. Each section (**Item**) has a heading (**Header**) containing a button (**Trigger**) that shows and hides the section's content region (**Panel**). The **Root** owns a single value: the array of item values that are currently open. With `multiple` unset (the default) at most one item is open at a time and opening another item closes the open one; with `multiple` set, items open and close independently.

Each Item is a Collapsible in all but name. It runs the same open-state, transition-status and panel-measurement logic as `Collapsible.Root`/`Collapsible.Panel` (specified in [collapsible](collapsible.md)), with the item's open state derived from the Root's value array instead of an `open`/`defaultOpen` pair. This document cites COL IDs for everything inherited unchanged and specifies only what Accordion adds or overrides:

- the Root value array, `multiple`, and the two-level change callbacks (`Accordion.Item.onOpenChange` then `Accordion.Root.onValueChange`);
- Root-level defaults for `disabled`, `keepMounted` and `hiddenUntilFound`;
- the Header part, the per-item index (`data-index`), `data-orientation`, `data-hidden`;
- Trigger ↔ Panel id wiring in both directions (`aria-controls` and `aria-labelledby`) and `role="region"` on the Panel;
- CSS variables named `--accordion-panel-height`/`--accordion-panel-width` instead of the Collapsible names.

Keyboard navigation between triggers (arrow keys, Home/End) does **not** exist in v1.9.0. It was removed in v1.6.0 to follow the updated WAI-ARIA APG accordion pattern; `orientation` and `loopFocus` remain as deprecated parameters with no effect on focus (§6.1, §16 Q1).

## 2. Anatomy

```
<Accordion.Root>             required, exactly 1                 default element: div
  <Accordion.Item>           required for content, 0..n          default element: div
    <Accordion.Header>       optional, 0..1 per Item             default element: h3
      <Accordion.Trigger>    optional, 0..n per Item (normally 1) default element: button
    <Accordion.Panel>        optional, 0..1 per Item             default element: div
```

Header, Trigger and Panel find their Item through ancestor-provided shared state, not through DOM adjacency; Items find their Root the same way. The Header is purely structural: if it is omitted, the Trigger may be placed directly inside the Item (the Trigger conformance tests do exactly this). Placing the Trigger outside a heading loses the heading semantics the APG pattern calls for, but Base UI does not check for it.

- **ACC-STR-01** — MUST. `Accordion.Root` renders a `<div>` by default, owns the value array, provides the Root-level settings to every descendant Item, and maintains an item registry that assigns each descendant Item its index (ACC-STA-13). It attaches no event handlers.
  Source: `accordion/root/AccordionRoot.tsx:58,100-123`. Test: [L2] `AccordionRoot.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): the item registry (track 1's registration-only composite list, ADR 04 Amendments 2026-10-26) is open until that pull request is on `main`; ACC-STA-13 and ACC-DOM-05 wait for it, and nothing else in this document does.
- **ACC-STR-02** — MUST. `Accordion.Item` renders a `<div>` by default and MUST be a descendant of an `Accordion.Root` (ACC-EDGE-01). Each Item owns one independent collapsible state machine (open flag derived from the Root value, `mounted` flag, transition status: COL-STA-04/05) and provides it to its Trigger and Panel exactly as `Collapsible.Root` does.
  Source: `accordion/item/AccordionItem.tsx:41-46,68-85,115-128`. Test: [L2] `AccordionItem.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`), "throws when rendered outside an Accordion.Root".
- **ACC-STR-03** — MUST. `Accordion.Header` renders an `<h3>` by default, MUST be a descendant of an `Accordion.Item` (ACC-EDGE-02), and has no behavior: it renders only the Item's state attributes (§5.2). Substituting another heading level (`render` → `<h2>`) is the documented way to fit the page outline.
  Source: `accordion/header/AccordionHeader.tsx:19-30`. Test: [L2] `AccordionHeader.test.tsx` describeConformance (`refInstanceof: HTMLHeadingElement`), "throws when rendered outside an Accordion.Item". Docs: `handbook/composition/page.mdx` §"Render prop" (lines 55-71).
- **ACC-STR-04** — MUST. `Accordion.Trigger` renders a `<button>` by default and MUST be a descendant of an `Accordion.Item` (ACC-EDGE-03). It does not need a Header ancestor.
  Source: `accordion/trigger/AccordionTrigger.tsx:40,49,67-72`. Test: [L2] `AccordionTrigger.test.tsx` describeConformance (Trigger rendered directly inside Item).
- **ACC-STR-05** — MUST. `Accordion.Panel` renders a `<div>` by default and MUST be a descendant of an `Accordion.Item` (ACC-EDGE-01/03). An Item is designed for at most one Panel; COL-STR-03 applies per Item.
  Source: `accordion/panel/AccordionPanel.tsx:38-50,105-140`. Test: [L2] `AccordionPanel.test.tsx` describeConformance (`<Accordion.Panel keepMounted />`, `refInstanceof: HTMLDivElement`).
- **ACC-STR-06** — MUST. When an Item has no Panel, its Trigger still toggles the item and still renders `aria-expanded`; while open it renders `aria-controls` pointing at the Item's generated panel id although no element carries it (COL-STR-04 applies per Item). Symmetrically, when an Item has a Panel but no Trigger has ever been rendered, the Panel renders `aria-labelledby` pointing at the Item's generated trigger id although no element carries it (§16 Q10).
  Source: `collapsible/root/useCollapsibleRoot.ts:26-29`; `accordion/item/AccordionItem.tsx:98-102`; `accordion/panel/AccordionPanel.tsx:97,117`. Test: [L2] `AccordionTrigger.test.tsx` "removes a disabled non-native trigger from the tab order" (Item without Panel).
- **ACC-STR-07** — MUST. Nested accordions (an `Accordion.Root` inside another accordion's Panel) are independent: each inner Item binds to its nearest Root, contributes to that Root's value only, and is indexed within that Root's registry only.
  Source: `accordion/root/AccordionRoot.tsx:119-122`; `accordion/item/AccordionItem.tsx:39,41-46`; `internals/composite/list/CompositeList.tsx:204-207`. Test: none.

## 3. Public API

### 3.1 `Accordion.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Accordion.Root.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value[] \| undefined` | `undefined` (uncontrolled) | Consumer-owned array of open item values. | ACC-API-01 |
| `defaultValue` | `Value[] \| undefined` | `[]` (a shared, frozen empty array) | Initial open item values when `value` is not supplied. | ACC-API-02 |
| `onValueChange` | `(value: Value[], eventDetails: Accordion.Root.ChangeEventDetails) => void` | `undefined` | Called with the requested next value whenever an item asks to open or close (§7). | ACC-API-03 |
| `multiple` | `boolean` | `false` | Whether more than one item may be open at a time. | ACC-API-04 |
| `disabled` | `boolean` | `false` | Ignores user interaction on every item. | ACC-API-05 |
| `hiddenUntilFound` | `boolean` | `false` | Default `hiddenUntilFound` for every Panel. | ACC-API-06 |
| `keepMounted` | `boolean` | `false` | Default `keepMounted` for every Panel. | ACC-API-07 |
| `orientation` | `'vertical' \| 'horizontal'` | `'vertical'` | Deprecated. Reflected in state and `data-orientation` only. | ACC-API-08 |
| `loopFocus` | `boolean` | `undefined` | Deprecated. Accepted and discarded. | ACC-API-09 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<div>` attribute | | — | Forwarded to the rendered element. | — |

**Consumed (not forwarded to the element):** `value`, `defaultValue`, `onValueChange`, `multiple`, `disabled`, `hiddenUntilFound`, `keepMounted`, `orientation`, `loopFocus`.

- **ACC-API-01** — MUST. `value` is the consumer-owned open-values array. When it is not `undefined` on the first render the component never changes the value itself; it only reports requested values through `onValueChange` (ACC-STA-07). Ownership is fixed at first render per the shared controlled-state contract (UTL, `useControlled` with `name: 'Accordion'`, `state: 'value'`).
  Source: `accordion/root/AccordionRoot.tsx:60-65`; `packages/utils/src/useControlled.ts:41-46`. Test: [L2] `AccordionRoot.test.tsx` controlled "open state", "custom item value".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 08 Amendments 2026-10-26, chosen by the user): `AccordionRoot<TValue>` with `Value : IReadOnlyList<TValue>?`, `ValueChanged : EventCallback<IReadOnlyList<TValue>>`, `DefaultValue : IReadOnlyList<TValue>?` and `OnValueChange : EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>>`, through `Controlled<IReadOnlyList<TValue>>("Accordion", "value")`, fixed at the first parameter set; each commit is a new list. A `null` value reads as the shared empty list (ACC-API-02).
- **ACC-API-02** — MUST. `defaultValue` is the initial component-owned value and is read once. When omitted (or `undefined`/`null`) the initial value is an empty array; that default array is shared and immutable, so the `value` exposed in state MUST be treated as read-only.
  Source: `accordion/root/AccordionRoot.tsx:44,60-65,131-135`; `packages/utils/src/empty.ts` (`EMPTY_ARRAY = Object.freeze([])`). Test: [L2] `AccordionRoot.test.tsx` uncontrolled "custom item value".
- **ACC-API-03** — MUST. `onValueChange` is optional. When absent, item requests still update the component-owned value (uncontrolled) or do nothing (controlled).
  Source: `accordion/root/AccordionRoot.tsx:83,87`. Test: [L2] `AccordionRoot.test.tsx` uncontrolled "open state" (no handler).
- **ACC-API-04** — MUST. `multiple` defaults to `false`. It selects between the single-open and multi-open value computations of ACC-STA-05/06.
  Source: `accordion/root/AccordionRoot.tsx:36,74-81`. Test: [L2] `AccordionRoot.test.tsx` "when false only one item can be open", "multiple items can be open when `multiple = true`". Docs: CHANGELOG.md v1.0.0-beta.5 "Change `multiple` prop to be false by default".
- **ACC-API-05** — MUST. `disabled` defaults to `false`. When `true`, every Item is effectively disabled regardless of its own `disabled` (ACC-API-11), so every Trigger ignores activation and every part renders `data-disabled`.
  Source: `accordion/root/AccordionRoot.tsx:31,91-98`; `accordion/item/AccordionItem.tsx:52`. Test: [L2] `AccordionRoot.test.tsx` "can disable the whole accordion", "does not toggle or fire callbacks when the root is disabled".
- **ACC-API-06** — MUST. `hiddenUntilFound` defaults to `false` and is the value every Panel uses when the Panel's own `hiddenUntilFound` is `undefined` (ACC-API-17).
  Source: `accordion/root/AccordionRoot.tsx:104`; `accordion/panel/AccordionPanel.tsx:52`. Test: [L2] `AccordionPanel.test.tsx` "passes root hiddenUntilFound to closed panels and allows panel overrides".
- **ACC-API-07** — MUST. `keepMounted` defaults to `false` and is the value every Panel uses when the Panel's own `keepMounted` is `undefined` (ACC-API-16).
  Source: `accordion/root/AccordionRoot.tsx:105`; `accordion/panel/AccordionPanel.tsx:53`. Test: [L2] `AccordionPanel.test.tsx` "passes root keepMounted to closed panels".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): an explicit `KeepMounted="false"` is found with `ParameterView.TryGetValue` for ACC-EDGE-04 (settled by the orchestrator 2026-10-25, the `CollapsiblePanel` precedent).
- **ACC-API-08** — MUST. `orientation` defaults to `'vertical'`. Its only effects are the `orientation` field of every part's state and the `data-orientation` attribute (ACC-DOM-02, ACC-DOM-07). It MUST NOT change keyboard behavior, focus behavior, or any ARIA attribute.
  Source: `accordion/root/AccordionRoot.tsx:37,95,207-215`. Test: none. Docs: CHANGELOG.md v1.6.0 "Align keyboard navigation with APG (#4965)".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 08 Amendments 2026-10-26): `Orientation : Orientation = Orientation.Vertical`, documented as deprecated in the XML documentation, without `[Obsolete]`.
- **ACC-API-09** — MUST. `loopFocus` is accepted and consumed (not forwarded to the element) and has no effect.
  Source: `accordion/root/AccordionRoot.tsx:34,187-194`. Test: none.
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 08 Amendments 2026-10-26): `LoopFocus : bool`, consumed and discarded, documented as deprecated, without `[Obsolete]`; `AccordionPortTests` "port: the root renders data-disabled and data-orientation and no role, aria, dir, data-value or open attribute" shows it is not rendered.

#### State object (`Accordion.Root.State`)

| Field | Type | Meaning |
|---|---|---|
| `value` | `Value[]` | Current open-values array (consumer- or component-owned). |
| `disabled` | `boolean` | The Root's `disabled` prop. |
| `orientation` | `'vertical' \| 'horizontal'` | The Root's `orientation` prop (deprecated). |

- **ACC-STA-01** — MUST. The Root state object passed to `className`/`style`/`render` functions contains exactly `{ value, disabled, orientation }`.
  Source: `accordion/root/AccordionRoot.tsx:91-98,130-150`. Test: [L2] `AccordionRoot.test.tsx` describeConformance (className/style callbacks); `AccordionRoot.spec.tsx` (type).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 02 Amendments 2026-10-26, chosen by the user; the marker named by this track): the value is in every state record, never projected (`[NotProjected]`), so the five records are generic: `AccordionRootState<TValue>(Value, Disabled, Orientation)`, and `AccordionItemState<TValue>`, `AccordionHeaderState<TValue>`, `AccordionTriggerState<TValue>` with `(Value, Disabled, Orientation, Hidden, Index, Open)`; `AccordionPanelState<TValue>` adds `[MirroredField] TransitionStatus? TransitionStatus`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-disabled` | `disabled === true`; empty value | ACC-DOM-01 | C# |
| `data-orientation` | always; `"vertical"` or `"horizontal"` | ACC-DOM-02 | C# |

#### CSS variables

None.

### 3.2 `Accordion.Item`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Accordion.Item.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` | a generated unique string | Identifies the item in the Root's value array. | ACC-API-10 |
| `disabled` | `boolean` | `false` | Ignores user interaction on this item. | ACC-API-11 |
| `onOpenChange` | `(open: boolean, eventDetails: Accordion.Item.ChangeEventDetails) => void` | `undefined` | Called when this item asks to open or close, before the Root is notified (§7). | ACC-API-12 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<div>` attribute | | — | Forwarded. | — |

**Consumed:** `value`, `disabled`, `onOpenChange`.

- **ACC-API-10** — MUST. An Item's identity is its `value` prop when that is neither `undefined` nor `null`; otherwise it is a generated string (`base-ui-` + a framework-unique token) that is stable for the lifetime of the Item instance. Items without an explicit `value` can therefore be opened by the user but cannot be opened initially through `defaultValue`/`value` unless the consumer knows the generated string. Values are compared by identity (ACC-STA-04).
  Source: `accordion/item/AccordionItem.tsx:48-50`; `internals/useBaseUiId.ts:9-11`. Test: [L2] `AccordionRoot.test.tsx` uncontrolled "open state" (no `value`), "default item value" / "custom item value" (explicit values). Docs: CHANGELOG.md v1.0.0-beta.4 "Use `useId` instead of composite index as fallback value".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 08 Amendments 2026-10-26, chosen by the user): the Item is `AccordionItem<TValue>`; its identity is `Value` (presence read with `TryGetValue`) unless absent or `null`, else the id it drew first, which needs a `TValue` of `string`. Under any other `TValue` an Item without a value throws ``Blazix: An `AccordionItem` without a `Value` is identified by a generated id, which needs a `TValue` of `string`. …`` in every build (ADR 02 Amendments 2026-10-20), as does an Item whose `TValue` differs from its Root's (the R7 pattern). Tested by `AccordionValuesTests` and `AccordionPortTests`.
- **ACC-API-11** — MUST. `disabled` defaults to `false`. The Item's **effective disabled** is `item.disabled || root.disabled`; an Item cannot re-enable itself inside a disabled Root.
  Source: `accordion/item/AccordionItem.tsx:31,52`. Test: [L2] `AccordionRoot.test.tsx` "can disable one accordion item", "does not toggle or fire callbacks when the item is disabled".
- **ACC-API-12** — MUST. `onOpenChange` is optional; its contract is ACC-EVT-01/02.
  Source: `accordion/item/AccordionItem.tsx:32,56-66`. Test: [L2] `AccordionRoot.test.tsx` "onOpenChange cancel() prevents opening while uncontrolled".

#### State object (`Accordion.Item.State`)

| Field | Type | Meaning |
|---|---|---|
| `value` | `any[]` | The **Root's** value array (not the Item's own value). |
| `disabled` | `boolean` | The Item's effective disabled (ACC-API-11). |
| `orientation` | `'vertical' \| 'horizontal'` | The Root's orientation. |
| `hidden` | `boolean` | `!open && !mounted` for this Item (ACC-STA-10). |
| `index` | `number` | The Item's index within its Root (ACC-STA-13); `-1` before the registry has published. |
| `open` | `boolean` | Whether this Item is open (ACC-STA-04). |

- **ACC-STA-02** — MUST. The Item state object contains exactly `{ value, disabled, orientation, hidden, index, open }` as defined above, and contains no transition status. `Accordion.Header` and `Accordion.Trigger` receive this same object unchanged.
  Source: `accordion/item/AccordionItem.tsx:87-96,131-144`; `accordion/header/AccordionHeader.tsx:21,33`; `accordion/trigger/AccordionTrigger.tsx:49,77`. Test: [L2] `AccordionItem.test.tsx` "does not report hidden=true after the item has started opening" (reads `state.open`/`state.hidden` in a render function).

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` | Item open; empty value | ACC-DOM-04 | C# |
| `data-closed` | Item closed; empty value (not in outline, §16 Q3) | ACC-DOM-04 | C# |
| `data-index` | always; decimal index, including `"0"` | ACC-DOM-05 | C# |
| `data-disabled` | effective disabled | ACC-DOM-06 | C# |
| `data-orientation` | always (not in outline for Item, §16 Q3) | ACC-DOM-07 | C# |
| `data-hidden` | `hidden === true` (not in outline, §16 Q3) | ACC-DOM-08 | C# |

#### CSS variables

None.

### 3.3 `Accordion.Header`

**Default element:** `<h3>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Accordion.Header.State`, identical to the Item state).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | | — |
| any other `<h3>` attribute | | — | Forwarded. | — |

State object, data attributes: as §3.2 (ACC-STA-02; ACC-DOM-04 … ACC-DOM-09). CSS variables: none.

### 3.4 `Accordion.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Accordion.Trigger.State`, identical to the Item state).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Whether the rendered element is a native `<button>`; COL-API-05 applies. | ACC-API-13 |
| `disabled` | `boolean \| undefined` | `undefined` | Additionally disables this Trigger; cannot re-enable it (not in outline, §16 Q3). | ACC-API-14 |
| `id` | `string \| undefined` | generated | The Trigger's `id`; also what the Panel's `aria-labelledby` references (not in outline, §16 Q3). | ACC-API-15 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; handlers chained per COL-PTR-03. | — |

**Consumed:** `nativeButton`, `disabled`, `id` (re-emitted per ACC-API-15).

- **ACC-API-13** — MUST. `nativeButton` defaults to `true` and configures the shared button behavior exactly as COL-API-05.
  Source: `accordion/trigger/AccordionTrigger.tsx:35,44-47`. Test: [L2] `AccordionTrigger.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`).
- **ACC-API-14** — MUST. The Trigger's **effective disabled** is `trigger.disabled || item effective disabled`. Unlike `Collapsible.Trigger` (COL-API-06), `disabled={false}` on an Accordion Trigger MUST NOT enable it inside a disabled Item or Root. A Trigger's own `disabled={true}` disables activation and the button attributes (ACC-DOM-12) but does not change the state object, so it does not add `data-disabled` (ACC-DOM-06).
  Source: `accordion/trigger/AccordionTrigger.tsx:31,40-47`. Test: [L2] `AccordionRoot.test.tsx` "does not toggle or fire callbacks when the %s is disabled" (both variants; the Trigger passes `disabled={false}`).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (settled by the orchestrator 2026-10-25): `Disabled : bool`, which only adds.
- **ACC-API-15** — MUST. The Trigger's rendered `id` is its `id` prop when that is a non-empty string, otherwise the Item's generated trigger id; the Trigger therefore always has an `id`. A non-empty `id` is also registered with the Item so that the Panel's `aria-labelledby` follows it (ACC-STA-11).
  Source: `accordion/trigger/AccordionTrigger.tsx:33,49-58,63`. Test: [L2] `AccordionRoot.test.tsx` "references manual trigger id in panel aria-labelledby", "renders correct ARIA attributes".

### 3.5 `Accordion.Panel`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes (render function receives `Accordion.Panel.State` and may return nothing; COL-ANI-15/18/19 apply). **Accepts `className`/`style` as function of state:** yes (state = `Accordion.Panel.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean \| undefined` | Root's `keepMounted` | Keep the element in the document while closed. | ACC-API-16 |
| `hiddenUntilFound` | `boolean \| undefined` | Root's `hiddenUntilFound` | Hide with `hidden="until-found"` while closed; implies kept mounted. | ACC-API-17 |
| `id` | `string \| undefined` | generated | The Panel's `id`; see ACC-API-18 for the empty-string case (not in outline, §16 Q3). | ACC-API-18 |
| `className`, `style`, `render` | per RND | — | `style` is resolved against Panel state and merged after the CSS variables (ACC-DOM-18). | — |
| any other `<div>` attribute | | — | Forwarded; overrides `role`, `aria-labelledby`, `hidden` emitted by the part. | — |

**Consumed:** `keepMounted`, `hiddenUntilFound`, `id` (re-emitted), `style` (re-merged).

- **ACC-API-16** — MUST. The Panel's effective `keepMounted` is its own prop when not `undefined`, otherwise the Root's `keepMounted`. With the effective value, COL-API-08 applies.
  Source: `accordion/panel/AccordionPanel.tsx:31,38-39,53`. Test: [L2] `AccordionPanel.test.tsx` "passes root keepMounted to closed panels".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (settled by the orchestrator 2026-10-25): `KeepMounted : bool?` and `HiddenUntilFound : bool?` on the Panel; `null` takes the Root's.
- **ACC-API-17** — MUST. The Panel's effective `hiddenUntilFound` is its own prop when not `undefined`, otherwise the Root's `hiddenUntilFound`. A Panel-level `false` overrides a Root-level `true`. With the effective value, COL-API-09 and TRN-API-03 apply.
  Source: `accordion/panel/AccordionPanel.tsx:30,38-39,52`. Test: [L2] `AccordionPanel.test.tsx` "passes root hiddenUntilFound to closed panels and allows panel overrides" (overridden panel is absent while closed).
- **ACC-API-18** — MUST. The Panel's rendered `id` is its `id` prop when that is not `undefined`/`null` — **including the empty string** — otherwise the Item's generated panel id. Only a non-empty `id` is registered with the Item for the Trigger's `aria-controls`. Consequently `id=""` renders `id=""` on the Panel while the Trigger's `aria-controls` references the generated panel id (which no element carries). This differs from `Collapsible.Panel` (COL-API-10), which treats `""` as absent for both purposes (§16 Q2).
  Source: `accordion/panel/AccordionPanel.tsx:32,54-55,69-74,87`; `collapsible/panel/useCollapsiblePanel.ts:470`. Test: [L2] `AccordionRoot.test.tsx` "references manual panel id in trigger aria-controls" (non-empty case only).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (accordion §16 Q2 follows the source; settled by the orchestrator 2026-10-25; ADR 04 note dated 2026-10-26): `CollapsiblePanelBehavior.RendersEmptyId` keeps `id=""` as written and registers nothing; `AccordionRootTests` "port: an empty panel id renders as written while aria-controls names the generated panel id".

#### State object (`Accordion.Panel.State`)

All Item state fields (§3.2) plus:

| Field | Type | Meaning |
|---|---|---|
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | The **Panel** transition status (COL-STA-06): the Item's status, forced to `'idle'` during motion-skipping opens. |

- **ACC-STA-03** — MUST. The Panel state object is the Item state object (ACC-STA-02) plus `transitionStatus` (Panel transition status per COL-STA-06). A consumer `style` function on the Panel is evaluated against this object (COL-EDGE-10 applies). Server: relaxed, see §9.
  Source: `accordion/panel/AccordionPanel.tsx:97-103,143-148`. Test: [L2] `AccordionPanel.test.tsx` describeConformance (className/style callbacks).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): the Item has no root element twin, so the Panel's `collapsible` module reports the panel's status into the Item's shared state (`CollapsibleOptions.ReportStatus`, `OnPanelStatusChange`; ADR 04 note dated 2026-10-26; design point D1 of track 6, option (a), **pending the user**).

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | Item open / closed (`data-closed` not in outline) | ACC-DOM-04 | C# |
| `data-index` | always | ACC-DOM-05 | C# |
| `data-disabled` | Item effective disabled | ACC-DOM-06 | C# |
| `data-orientation` | always | ACC-DOM-07 | C# |
| `data-hidden` | Item `hidden` (not in outline) | ACC-DOM-08 | C# |
| `data-starting-style` | Panel status `'starting'`; also persisted on a hidden until-found non-keyframe panel | ACC-DOM-14 | JS |
| `data-ending-style` | Panel status `'ending'` | ACC-DOM-14 | JS |
| `hidden` | closed and not in a closing transition; boolean, or `"until-found"` | ACC-DOM-15 | C# (boolean); JS (`"until-found"`) |

#### CSS variables

| Variable | Set on | Value | Req | Writer |
|---|---|---|---|---|
| `--accordion-panel-height` | Panel element inline style | `auto`, or `<scrollHeight>px` while a measured size is held (COL §9.2) | ACC-DOM-17 | JS |
| `--accordion-panel-width` | Panel element inline style | `auto`, or `<scrollWidth>px` while a measured size is held | ACC-DOM-17 | JS |

### 3.6 Shared types

- `Accordion.Root.Value<V>` = `AccordionValue<V>` = `V[]`. The Root is generic over the item value type `V` (default `any`); `onValueChange` receives `V[]`. Source: `accordion/root/AccordionRoot.tsx:24-27,124-128,223-229`. Test: [L1] `AccordionRoot.spec.tsx` (type assertions, including `(string | null)[]`).
- `Accordion.Root.ChangeEventReason` = `Accordion.Item.ChangeEventReason` = `'trigger-press' | 'none'`. Source: `accordion/root/AccordionRoot.tsx:218`; `accordion/item/AccordionItem.tsx:171`; `internals/reason-parts.ts`.
- `Accordion.Root.ChangeEventDetails` / `Accordion.Item.ChangeEventDetails` = the common change-event-details shape ([event-details-and-reasons](../architecture/event-details-and-reasons.md)) specialized to those reasons: `event` is `MouseEvent | PointerEvent | TouchEvent | KeyboardEvent` for `'trigger-press'` and `Event` (the `beforematch` event) for `'none'`; `trigger` is always `undefined`. Source: `accordion/root/AccordionRoot.tsx:220-221`; `accordion/item/AccordionItem.tsx:173-174`.
- `Orientation` = `'horizontal' | 'vertical'`. Source: `internals/types.ts`.
- `Accordion.{Root,Item,Header,Trigger,Panel}.{Props,State}` namespaced and as flat aliases (`AccordionRootProps`, `AccordionPanelState`, …); attribute-name namespaces `AccordionRootDataAttributes`, `AccordionItemDataAttributes`, `AccordionHeaderDataAttributes`, `AccordionTriggerDataAttributes`, `AccordionPanelDataAttributes`, `AccordionPanelCssVariables`. Source: `accordion/index.ts:1-14`.

## 4. State model

### 4.1 Root value and item open state

The only consumer-visible state pair is `value`/`defaultValue` on the Root. Every Item's open flag is derived from it; Items have no `open`/`defaultOpen` of their own. Internally each Item runs the Collapsible open-state logic in consumer-owned mode with `open` = the derived flag, so every Collapsible requirement that is stated for "consumer-owned `open`" applies to each Item (COL-STA-03), and the Root's value update is what changes it.

- **ACC-STA-04** — MUST. An Item is open exactly when the Root's current value array contains the Item's identity (ACC-API-10), using SameValueZero comparison (identity for objects, `NaN` equals `NaN`). Several Items with the same identity are open or closed together.
  Source: `accordion/item/AccordionItem.tsx:50,54`. Test: [L1] `AccordionRoot.test.tsx` uncontrolled/controlled "custom item value".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (settled by the orchestrator 2026-10-25; ADR 05 note dated 2026-10-26): membership is `Equality.SameValueZero<T>`, the single-mode check and the removal `Equality.StrictEquals<T>`, in loops over the list (`AccordionValues`), never `Contains` or LINQ.
- **ACC-STA-05** — MUST. With `multiple` `false`, when an Item requests a change (open or close), the requested next value is: `[]` if the first element of the current value is strictly equal (`===`) to the Item's identity, otherwise `[itemIdentity]`. The requested open/closed direction is not consulted. Pressing the open Item therefore closes it; pressing a closed Item opens it and closes whichever was open.
  Source: `accordion/root/AccordionRoot.tsx:74-75`. Test: [L1] `AccordionRoot.test.tsx` "when false only one item can be open", "`multiple` is false" (`['one']` then `['two']`).
- **ACC-STA-06** — MUST. With `multiple` `true`: a request to open appends the Item's identity to the end of a copy of the current value (order is activation order; an identity that is itself an array is appended as one element, never flattened); a request to close produces a copy of the current value with every element strictly equal (`!==` filter) to the Item's identity removed.
  Source: `accordion/root/AccordionRoot.tsx:76-81`. Test: [L1] `AccordionRoot.test.tsx` "default item value" (`[0]` then `[0, 1]`), "custom item value" (`['two']` then `['two', 'one']`), "keeps an array item value intact while multiple", "multiple items can be open when `multiple = true`".
- **ACC-STA-07** — MUST. After computing the requested next value and notifying the consumer (§7), the Root applies it only if the change was not canceled and the value is component-owned. In consumer-owned mode the value changes only when the consumer supplies a new `value`.
  Source: `accordion/root/AccordionRoot.tsx:83-87`; `packages/utils/src/useControlled.ts:83-90`. Test: [L2] `AccordionRoot.test.tsx` "onValueChange cancel() prevents opening while controlled", controlled "open state".
- **ACC-STA-08** — MUST. When an update to the Root value closes Items other than the one that requested the change (single mode, or a consumer replacing `value`), each affected Item closes through its own transition lifecycle (COL §9.3) independently and concurrently with any Item that opens in the same update; no callback is invoked on the implicitly closed Items (ACC-EVT-07).
  Source: `accordion/item/AccordionItem.tsx:54,68-72`. Test: [L4] `AccordionPanel.test.tsx` "keeps the closing panel visible until its exit transition completes when switching items".

### 4.2 Per-item transition state and derived flags

Each Item owns `mounted` and `transitionStatus` exactly as a Collapsible Root does (COL-STA-04, COL-STA-05; state diagram in [collapsible](collapsible.md) §4.2), driven by the Item's derived open flag. Items open at first render start `mounted` with status `'idle'` (no entrance motion). Only the Panel exposes the transition status (ACC-STA-03); Item, Header and Trigger do not.

- **ACC-STA-09** — MUST. Each Item's `mounted` flag and transition status follow COL-STA-04 and COL-STA-05 with `open` = the Item's derived open flag, and the Panel transition status follows COL-STA-06. Server: relaxed, see §9.
  Source: `accordion/item/AccordionItem.tsx:68-72`; `collapsible/root/useCollapsibleRoot.ts:24`. Test: [L4] `AccordionPanel.test.tsx` "keeps the closing panel visible until its exit transition completes when switching items".
- **ACC-STA-10** — MUST. The Item's `hidden` flag is `!open && !mounted`. It MUST be `false` in every state where `open` is `true` (it becomes `false` in the same update that opens the Item).
  Source: `accordion/item/AccordionItem.tsx:90`. Test: [L2] `AccordionItem.test.tsx` "does not report hidden=true after the item has started opening".

### 4.3 Id resolution

- **ACC-STA-11** — MUST. Each Item resolves a *trigger id*: the id registered by a Trigger with a non-empty `id` prop, if any; otherwise the Item's generated default trigger id; otherwise **no id** once a Trigger has registered and then unmounted. A Trigger registers when it mounts or its `id` prop changes and unregisters when it unmounts; an unregistration clears the registration only if it still holds that Trigger's id; changing the Trigger's `id` from a non-empty value to `undefined` restores the generated id. The mechanism mirrors COL-STA-09 and COL-EDGE-09.
  Source: `accordion/item/AccordionItem.tsx:98-102`; `accordion/trigger/AccordionTrigger.tsx:50-58`. Test: [L2] `AccordionRoot.test.tsx` "updates panel labeling when a manual trigger id is added or changed", "restores panel labeling when a manual trigger id is removed", "unregisters generated part ids when the trigger or panel unmounts".
- **ACC-STA-12** — MUST. Each Item resolves a *panel id* exactly per COL-STA-09, with the Panel registering only a non-empty `id` (ACC-API-18).
  Source: `accordion/panel/AccordionPanel.tsx:54,69-74`; `collapsible/root/useCollapsibleRoot.ts:26-29`. Test: [L2] `AccordionRoot.test.tsx` "references manual panel id in trigger aria-controls", "unregisters generated part ids when the trigger or panel unmounts".

### 4.4 Item index

- **ACC-STA-13** — MUST. Each Item's `index` is its zero-based position, in document order, among the connected Item elements registered with the nearest Root, as resolved by the shared item registry (CMP-STA-01, CMP-STA-02, CMP-STA-03, CMP-STA-11, CMP-STA-18). Before the registry first publishes (including in server-rendered markup) the index is `-1` (CMP-STA-15); the published index is applied post-commit, pre-paint.
  Source: `accordion/item/AccordionItem.tsx:39,91`; `internals/composite/list/useCompositeListItem.ts:42-55,84-96`; `internals/composite/list/CompositeList.tsx:192-196,217-251`. Test: [L2] `AccordionTrigger.test.tsx` "renders data-index on every trigger including the first".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): open to track 1 ([issue 100](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/100), E1, the registration-only list with its index report); every part renders `data-index="-1"` until then, which is the value before the first publication.

Invariants: exactly one of `data-open`/`data-closed` on each Item, Header and Panel at all times; in single mode, user interaction alone can never leave more than one Item open (a consumer-supplied `value` can, §12 ACC-EDGE-12); `aria-controls` is present on a Trigger only while its Item is open.

## 5. Rendered DOM and ARIA

State attributes are projected with the default rules of [rendering-and-composition](../architecture/rendering-and-composition.md) (RND-DOM-01 … RND-DOM-07) plus the custom mappings named below. Consumer attributes of the same name override part-supplied ones (RND-DOM-10).

### 5.1 Root element

- **ACC-DOM-01** — MUST. The Root element has `data-disabled=""` when the Root's `disabled` is `true` and no `data-disabled` otherwise.
  Source: `internals/getStateAttributesProps.ts:24-28`; `accordion/root/AccordionRoot.tsx:91-98`. Test: none for Root (Items/Headers/Triggers/Panels: "can disable the whole accordion").
- **ACC-DOM-02** — MUST. The Root element always has `data-orientation` equal to the `orientation` value (`"vertical"` by default).
  Source: `internals/getStateAttributesProps.ts:26-28`; `accordion/root/AccordionRoot.tsx:37,95`. Test: none.
- **ACC-DOM-03** — MUST. The Root element carries no `data-value`, no `data-open`/`data-closed`, no `role`, no `aria-*` attribute and no `dir` attribute of its own.
  Source: `accordion/root/AccordionRoot.tsx:14-16,112-117`. Test: none. Docs: CHANGELOG.md v1.6.0 "Remove region role from `Accordion.Root`", v1.7.0 "Remove the implicit `dir` attribute from `<Accordion.Root>`".

### 5.2 Item, Header, Trigger and Panel: shared item attributes

Item, Header, Trigger and Panel all project the Item state (Panel: plus `transitionStatus`) through the item mapping: `open` → open/closed attributes, `index` → `data-index`, `transitionStatus` → starting/ending attributes, `value` → nothing; `disabled`, `orientation`, `hidden` use the default projection. The Trigger replaces the `open` projection (ACC-DOM-11).

- **ACC-DOM-04** — MUST. The Item, Header and Panel elements have `data-open=""` while the Item is open and `data-closed=""` while it is closed (never both, never neither), including on a closed Panel kept in the document.
  Source: `accordion/item/stateAttributesMapping.ts:7-8`; `utils/collapsibleOpenStateMapping.ts:26-35`. Test: [L2] `AccordionRoot.test.tsx` uncontrolled "open state" (Panel `data-open`), "when false only one item can be open".
- **ACC-DOM-05** — MUST. The Item, Header, Trigger and Panel elements always have `data-index` equal to the decimal string of the Item's index, including `"0"` for the first Item (and `"-1"` before the index is published, ACC-STA-13).
  Source: `accordion/item/stateAttributesMapping.ts:9`. Test: [L2] `AccordionTrigger.test.tsx` "renders data-index on every trigger including the first". Docs: CHANGELOG.md v1.9.0 "Fix `<Accordion.Trigger>`'s `data-value` and `data-index` attributes".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): the custom mapping writes `data-index` always, `"0"` and `"-1"` included (settled by the orchestrator 2026-10-25: the generator's integer default omits `0`); the published index is open to track 1 with ACC-STA-13.
- **ACC-DOM-06** — MUST. The Item, Header, Trigger and Panel elements have `data-disabled=""` exactly when the Item's effective disabled (ACC-API-11) is `true`. A Trigger's own `disabled` prop does not affect `data-disabled` (ACC-API-14).
  Source: `accordion/item/AccordionItem.tsx:52,92`; `internals/getStateAttributesProps.ts:24-25`. Test: [L2] `AccordionRoot.test.tsx` "can disable the whole accordion", "can disable one accordion item".
- **ACC-DOM-07** — MUST. The Item, Header, Trigger and Panel elements always have `data-orientation` equal to the Root's `orientation`.
  Source: `accordion/item/AccordionItem.tsx:89`; `internals/getStateAttributesProps.ts:26-28`. Test: none.
- **ACC-DOM-08** — MUST. The Item, Header, Trigger and Panel elements have `data-hidden=""` exactly while the Item's `hidden` flag (ACC-STA-10) is `true`.
  Source: `accordion/item/AccordionItem.tsx:90`; `internals/getStateAttributesProps.ts:24-25`. Test: none (§16 Q3).
- **ACC-DOM-09** — MUST. No part renders a `data-value` attribute (the Root value array is suppressed in every mapping).
  Source: `accordion/root/AccordionRoot.tsx:14-16`; `accordion/item/stateAttributesMapping.ts:11`. Test: [L2] `AccordionTrigger.test.tsx` "does not render the root value as data-value".
- **ACC-DOM-10** — MUST. The Item, Header and Trigger elements never carry `data-starting-style` or `data-ending-style` (their state has no transition status). This differs from `Collapsible.Root`/`Collapsible.Trigger` (COL-DOM-03, COL-DOM-06).
  Source: `accordion/item/AccordionItem.tsx:87-96`; `accordion/item/stateAttributesMapping.ts:10`. Test: none.

### 5.3 Trigger element

- **ACC-DOM-11** — MUST. The Trigger element has `data-panel-open=""` while its Item is open and no such attribute while closed; it never carries `data-open` or `data-closed`.
  Source: `accordion/trigger/AccordionTrigger.tsx:14-17`; `utils/collapsibleOpenStateMapping.ts:13-24`. Test: [L2] `AccordionTrigger.test.tsx` "keeps data-panel-open instead of data-open"; `AccordionRoot.test.tsx` "multiple items can be open when `multiple = true`".
- **ACC-DOM-12** — MUST. The Trigger's button attributes (`type="button"` vs `role="button"`, `disabled`, `aria-disabled`, `tabindex`) follow COL-DOM-07, COL-DOM-08 and COL-DOM-09 using the Trigger's effective disabled (ACC-API-14), in **non-composite** mode (an Accordion Root is not a composite root, UBT-STA-01): native + disabled → `disabled` attribute, no `aria-disabled`; non-native + disabled → `aria-disabled="true"`, `tabindex="-1"`, no `disabled`; enabled → `tabindex="0"` in both modes.
  Source: `accordion/trigger/AccordionTrigger.tsx:42-47,67-72`; `internals/use-button/useButton.ts:25-34`; `utils/useFocusableWhenDisabled.ts:30-47`. Test: [L2] `AccordionTrigger.test.tsx` "renders the disabled attribute when disabled", "removes a disabled non-native trigger from the tab order", "keeps a non-native trigger tabbable". Docs: CHANGELOG.md v1.9.0 "Apply the native `disabled` attribute to `<Accordion.Trigger>` when using native buttons".
- **ACC-DOM-13** — MUST. The Trigger element always has an `id` (ACC-API-15).
  Source: `accordion/trigger/AccordionTrigger.tsx:51,63`. Test: [L2] `AccordionRoot.test.tsx` "updates panel labeling when a manual trigger id is added or changed" (`toHaveAttribute('id')` before any manual id).

### 5.4 Panel element

The Panel is rendered by the Collapsible panel mechanism, configured with the effective `keepMounted`/`hiddenUntilFound` (ACC-API-16/17) and the id of ACC-API-18. Everything COL §5.3 states for the Panel holds, with the substitutions below.

- **ACC-DOM-14** — MUST. The Panel's `data-starting-style`/`data-ending-style` follow COL-DOM-12 (Panel transition status), and the persisted `data-starting-style` on a hidden until-found non-keyframe Panel follows COL-DOM-15 / TRN-DOM-09.
  Source: `accordion/panel/AccordionPanel.tsx:99-102,114-115,132`; `collapsible/panel/useCollapsiblePanel.ts:93-94,466-468`. Test: [L4] `AccordionPanel.test.tsx` "keeps the closing panel visible until its exit transition completes when switching items" (`data-ending-style` on the closing panel).
- **ACC-DOM-15** — MUST. The Panel's `hidden` attribute follows COL-DOM-13 (boolean `hidden` when `!open && !mounted`, consumer `hidden` wins) and, with effective `hiddenUntilFound`, COL-DOM-14 / TRN-DOM-08 (`hidden="until-found"`).
  Source: `collapsible/panel/useCollapsiblePanel.ts:74,355-366,469`. Test: [L2] `AccordionPanel.test.tsx` "passes root keepMounted to closed panels", "passes root hiddenUntilFound to closed panels and allows panel overrides", "warns when a panel enables hiddenUntilFound and disables keepMounted" (L4 for the `hidden="until-found"` clause).
- **ACC-DOM-16** — MUST. The Panel element is in the document exactly when `keepMounted || hiddenUntilFound || mounted || open`, using the effective values (COL-DOM-16, TRN-ANI-02).
  Source: `collapsible/panel/useCollapsiblePanel.ts:461`; `accordion/panel/AccordionPanel.tsx:136-138`. Test: [L2] `AccordionRoot.test.tsx` uncontrolled "open state" (absent while closed); `AccordionPanel.test.tsx` "passes root hiddenUntilFound to closed panels and allows panel overrides" (overridden panel absent).
- **ACC-DOM-17** — MUST. The Panel's inline style always defines `--accordion-panel-height` and `--accordion-panel-width`, with the values and timing COL-DOM-17 specifies for `--collapsible-panel-height`/`--collapsible-panel-width` (`auto`, or integer `px` while a measured size is held, including the cached-size rule of COL-ANI-12). The Collapsible-named variables are NOT set on an Accordion Panel.
  Source: `accordion/panel/AccordionPanel.tsx:118-124`; `accordion/panel/AccordionPanelCssVars.ts:5,10`. Test: [L4] `AccordionPanel.test.tsx` "keeps the closing panel visible until its exit transition completes when switching items" (`auto` after open; `px` during close).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 04 Amendments 2026-10-26, chosen by the user): the names are an option of the `collapsible` module (`CollapsibleOptions.HeightVariable`/`WidthVariable`) and of `CollapsiblePanelBehavior` (`CollapsiblePanelVariables.Accordion`); ADR 04 §2's row reads `--accordion-panel-height/width` for `Accordion.Panel`.
- **ACC-DOM-18** — MUST. Panel attribute and inline-style precedence, lowest to highest: (1) the mechanism's `hidden`, `id` and persisted `data-starting-style`; (2) `role`, `aria-labelledby` and the two CSS variables; (3) consumer attributes (so a consumer `role`, `aria-labelledby` or `hidden` replaces the part's); (4) the consumer `style` (object, or function result against Panel state); (5) inline `animation-name: none` while open-animation suppression is active (COL-ANI-13, COL-ANI-20). Otherwise as COL-DOM-18.
  Source: `accordion/panel/AccordionPanel.tsx:103-133`. Test: [L4] `AccordionPanel.test.tsx` "suppresses the initial keyframe animation from inline styles when rendered open" (`animation-name: none` wins; `animation-duration: 100ms` preserved).
- **ACC-DOM-19** — MUST. The Panel element always has an `id` attribute, valued per ACC-API-18 (possibly the empty string).
  Source: `accordion/panel/AccordionPanel.tsx:55,87`; `collapsible/panel/useCollapsiblePanel.ts:470`. Test: [L2] `AccordionRoot.test.tsx` "renders correct ARIA attributes".

### 5.5 ARIA

- **ACC-ARIA-01** — MUST. The Trigger always has `aria-expanded`: `"true"` while its Item is open, `"false"` while closed (COL-ARIA-01).
  Source: `accordion/trigger/AccordionTrigger.tsx:62`. Test: [L2] `AccordionRoot.test.tsx` uncontrolled/controlled "open state".
- **ACC-ARIA-02** — MUST. The Trigger has `aria-controls` equal to the Item's resolved panel id (ACC-STA-12) while its Item is open and a panel id resolves, and no `aria-controls` otherwise (COL-ARIA-02, COL-ARIA-03).
  Source: `accordion/trigger/AccordionTrigger.tsx:61`. Test: [L2] `AccordionRoot.test.tsx` "renders correct ARIA attributes", "references manual panel id in trigger aria-controls".
- **ACC-ARIA-03** — MUST. The Panel has `role="region"` whenever it is in the document (open, closing, or closed but kept mounted).
  Source: `accordion/panel/AccordionPanel.tsx:118`. Test: [L2] `AccordionRoot.test.tsx` "renders correct ARIA attributes".
- **ACC-ARIA-04** — MUST. The Panel has `aria-labelledby` equal to the Item's resolved trigger id (ACC-STA-11), and no `aria-labelledby` when none resolves. It follows a consumer-supplied Trigger `id`, updates when that `id` changes, and returns to the Trigger's generated id when the manual `id` is removed; in every case it equals the Trigger element's `id`.
  Source: `accordion/panel/AccordionPanel.tsx:97,117`; `accordion/item/AccordionItem.tsx:98-102`. Test: [L2] `AccordionRoot.test.tsx` "renders correct ARIA attributes", "references manual trigger id in panel aria-labelledby", "updates panel labeling when a manual trigger id is added or changed", "restores panel labeling when a manual trigger id is removed".
- **ACC-ARIA-05** — MUST. When the Trigger part is removed from an Item, the Panel drops `aria-labelledby`; when it is rendered again, `aria-labelledby` returns with the Trigger's id. When the Panel part is removed from an open Item, the Trigger drops `aria-controls`; when it is rendered again, `aria-controls` returns with the Panel's id (COL-ARIA-04). Both hold under repeated mount/teardown of the parts' setup work.
  Source: `accordion/trigger/AccordionTrigger.tsx:53-58`; `accordion/panel/AccordionPanel.tsx:69-74`. Test: [L2] `AccordionRoot.test.tsx` "unregisters generated part ids when the trigger or panel unmounts" (under StrictMode).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 06 Amendments 2026-10-18 rule 2): StrictMode's repeated mount and teardown is restated as registration idempotency: the L2 title unmounts and remounts each part twice.
- **ACC-ARIA-06** — MUST. Generated Trigger and Panel ids are identical in server-rendered markup and after the client takes over, so `aria-controls`/`aria-labelledby` stay consistent through hydration.
  Source: `accordion/item/AccordionItem.tsx:98`; `collapsible/root/useCollapsibleRoot.ts:26`; `internals/useBaseUiId.ts:9-11`. Test: [L4] `AccordionRoot.test.tsx` "preserves generated part associations during hydration".
- **ACC-ARIA-07** — MUST. The Root, Item and Header render no `role` and no `aria-*` attributes of their own; the Header's heading semantics (level 3 by default) come from its element.
  Source: `accordion/root/AccordionRoot.tsx:112-117`; `accordion/item/AccordionItem.tsx:115-120`; `accordion/header/AccordionHeader.tsx:23-28`. Test: [L2] `AccordionRoot.test.tsx` "can disable the whole accordion" (`getAllByRole('heading')`).

## 6. Interactions

### 6.1 Keyboard

All keyboard handling on the Trigger comes from the shared button behavior ([button-behavior](../architecture/button-behavior.md)) in non-composite mode, configured with `{ disabled: <effective>, native: nativeButton }`; Accordion attaches no key handlers of its own and has **no** roving focus.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | Trigger focused, enabled, native | Browser activation `click` → item toggles (§4.1). | ACC-KEY-01 |
| <kbd>Enter</kbd> | Trigger focused, enabled, non-native | keydown: default prevented, synthetic `click` dispatched → toggles. | ACC-KEY-01 |
| <kbd>Space</kbd> | Trigger focused, enabled, native or non-native | keydown: no toggle (non-native: default prevented); keyup: `click` → toggles. | ACC-KEY-02 |
| <kbd>Enter</kbd>/<kbd>Space</kbd> | Trigger disabled (any of Root, Item, Trigger) | Nothing; no callbacks. | ACC-KEY-03 |
| <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>/<kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd>/<kbd>Home</kbd>/<kbd>End</kbd> | any; any `orientation`, `loopFocus`, direction | Not handled: no focus movement, no `preventDefault`. | ACC-KEY-04 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Not intercepted; native order through every enabled Trigger. | ACC-KEY-04 |

- **ACC-KEY-01** — MUST. <kbd>Enter</kbd> on a focused, enabled Trigger toggles its Item through the request path of §4.1/§7 with reason `'trigger-press'` (COL-KEY-01).
  Source: `accordion/trigger/AccordionTrigger.tsx:44-47,64`; `internals/use-button/useButton.ts:116-176`. Test: [L4] `AccordionRoot.test.tsx` "key: $key toggles the Accordion open state with $triggerKind triggers" (the two Enter variants).
- **ACC-KEY-02** — MUST. <kbd>Space</kbd> on a focused, enabled Trigger toggles its Item on **keyup**, for both native and non-native Triggers; the keydown alone changes nothing and calls no callback (UBT-KEY-02, non-composite).
  Source: `internals/use-button/useButton.ts:177-217`. Test: [L4] `AccordionRoot.test.tsx` "key: $key toggles the Accordion open state with $triggerKind triggers" (the two Space variants), "opens and closes on Space keyup when rendering $triggerKind triggers" (2 variants).
- **ACC-KEY-03** — MUST. While a Trigger's effective disabled is `true`, <kbd>Enter</kbd>/<kbd>Space</kbd> MUST NOT toggle and MUST NOT call `Accordion.Item.onOpenChange` or `Accordion.Root.onValueChange`; a native disabled Trigger is unreachable by <kbd>Tab</kbd> and a non-native one has `tabindex="-1"` (COL-KEY-03).
  Source: `internals/use-button/useButton.ts:104-119`; `utils/useFocusableWhenDisabled.ts:33-35,45-47`. Test: [L4] `AccordionRoot.test.tsx` "does not toggle or fire callbacks when the %s is disabled" (2 variants); `AccordionTrigger.test.tsx` "removes a disabled non-native trigger from the tab order".
- **ACC-KEY-04** — MUST NOT. The implementation MUST NOT handle arrow keys, <kbd>Home</kbd> or <kbd>End</kbd> on Triggers (no focus movement between Triggers, no wrapping, no `preventDefault`), whatever the `orientation`, `loopFocus` or reading direction; <kbd>Tab</kbd> is never intercepted. Focus moves between Triggers only by the native tab sequence.
  Source: `accordion/` (no key handlers; `loopFocus` discarded at `root/AccordionRoot.tsx:34`); `accordion/trigger/AccordionTrigger.tsx:60-65` (only `onClick` attached). Test: [L4] `AccordionRoot.test.tsx` "key: $key toggles the Accordion open state with $triggerKind triggers" (Tab reaches the Trigger). Docs: CHANGELOG.md v1.6.0 "Align keyboard navigation with APG (#4965)"; deprecation text on `loopFocus`/`orientation`.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| click (any pointer type) | Trigger enabled | Item toggle request, §4.1/§7 | ACC-PTR-01 |
| click / pointerdown | Trigger disabled | Ignored (COL-PTR-02) | ACC-PTR-02 |
| any handled or unhandled event | consumer handler on Trigger | Runs before the part's; may veto with `preventBaseUIHandler()` | ACC-PTR-03 |

- **ACC-PTR-01** — MUST. A `click` on an enabled Trigger requests the toggle of its Item: `Accordion.Item.onOpenChange(!open, details)` then `Accordion.Root.onValueChange(next, details)` (§7), then the value update (§4.1). No hover, long-press, drag or right-click behavior exists.
  Source: `collapsible/root/useCollapsibleRoot.ts:31-42`; `accordion/item/AccordionItem.tsx:56-66`; `accordion/root/AccordionRoot.tsx:67-89`. Test: [L2] `AccordionRoot.test.tsx` uncontrolled "open state", "default item value".
- **ACC-PTR-02** — MUST. A click on a Trigger whose effective disabled is `true` does nothing (no toggle, no callbacks), per COL-PTR-02.
  Source: `internals/use-button/useButton.ts:104-110,218-224`. Test: [L2] `AccordionRoot.test.tsx` "does not toggle or fire callbacks when the %s is disabled" (2 variants).
- **ACC-PTR-03** — MUST. Consumer handlers on the Trigger are chained per COL-PTR-03; every consumer handler (including for events the part does not itself handle, such as `mouseup`) receives an event on which `preventBaseUIHandler()` can be called without error (RND-API-06).
  Source: `accordion/trigger/AccordionTrigger.tsx:67-72`; `merge-props/mergeProps.ts:257-263`. Test: [L2] `AccordionRoot.test.tsx` "allows onMouseUp to call preventBaseUIHandler on the trigger".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (RND-API-06): the port's veto is `PreventPartHandler()` on a C# `onmouseup`.

### 6.3 Focus management

- **ACC-FOC-01** — MUST. Opening or closing an Item MUST NOT move focus, and the Accordion MUST NOT manage a roving tabindex: every enabled Trigger is an independent tab stop (`tabindex="0"`, ACC-DOM-12) and Panels are not focusable by default (COL-FOC-01).
  Source: `accordion/` (no focus calls); `utils/useFocusableWhenDisabled.ts:30-36`. Test: [L4] `AccordionTrigger.test.tsx` "keeps a non-native trigger tabbable".

### 6.4 Dismissal

Not applicable — panels close only through their Trigger, through another Item opening in single mode, or through a consumer `value` change; there is no outside-press, Escape, scroll or blur dismissal.

### 6.5 Reading direction

- **ACC-I18N-01** — MUST. Accordion does not consume the reading direction: it renders no `dir` attribute, and right-to-left context changes no key handling (ACC-KEY-04) and no measurement. Horizontal layouts rely solely on consumer CSS using `--accordion-panel-width` and `data-orientation`.
  Source: `accordion/` (no direction lookup); CHANGELOG.md v1.7.0 "Remove the implicit `dir` attribute from `<Accordion.Root>` (#5117)". Test: none (exercised by `docs/src/app/(private)/experiments/accordion/horizontal.tsx:61-74`).

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `Accordion.Item.onOpenChange` | `(open: boolean, details: Accordion.Item.ChangeEventDetails) => void` | That Item's Trigger is activated; `beforematch` on that Item's Panel | `'trigger-press'`, `'none'` | yes |
| `Accordion.Root.onValueChange` | `(value: Value[], details: Accordion.Root.ChangeEventDetails) => void` | After a non-canceled `Item.onOpenChange`, same occasions | `'trigger-press'`, `'none'` | yes |

There is no `onOpenChangeComplete`, `actionsRef` or `preventUnmountOnClose()` on any Accordion part in v1.9.0.

- **ACC-EVT-01** — MUST. On a Trigger activation, the Item's `onOpenChange` is called exactly once with `open = !itemOpen` and fresh details `{ reason: 'trigger-press', event: <the native click event> }`, before any value or DOM change, in both ownership modes.
  Source: `collapsible/root/useCollapsibleRoot.ts:31-35`; `accordion/item/AccordionItem.tsx:56-58`. Test: [L2] `AccordionRoot.test.tsx` "opens and closes on Space keyup when rendering $triggerKind triggers" (called with `true`, then `false`).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): at L2 the title's activation is the click the `button` module dispatches; the Space keyup timing runs at L4 (ACC-KEY-02).
- **ACC-EVT-02** — MUST. If `details.cancel()` was called during the Item's `onOpenChange`, the Root's `onValueChange` MUST NOT be called and the value MUST NOT change (in either ownership mode).
  Source: `accordion/item/AccordionItem.tsx:60-62`. Test: [L2] `AccordionRoot.test.tsx` "onOpenChange cancel() prevents opening while uncontrolled", "onOpenChange cancel() prevents onValueChange while controlled".
- **ACC-EVT-03** — MUST. Otherwise the Root's `onValueChange` is called exactly once with the requested next value (ACC-STA-05/06) and the **same** details object the Item received (EVD-EVT-04), before the value changes, in both ownership modes. It is called even when the next value equals the current one in content.
  Source: `accordion/item/AccordionItem.tsx:64`; `accordion/root/AccordionRoot.tsx:67-83`. Test: [L2] `AccordionRoot.test.tsx` "default item value", "custom item value", "`multiple` is false".
- **ACC-EVT-04** — MUST. If `details.cancel()` was called during `onValueChange`, a component-owned value MUST NOT change; this holds for opening and closing in both single and multiple mode. Reading `details.isCanceled` inside the handler after calling `cancel()` returns `true`.
  Source: `accordion/root/AccordionRoot.tsx:84-87`. Test: [L2] `AccordionRoot.test.tsx` "onValueChange cancel() prevents opening while uncontrolled", "onValueChange cancel() prevents closing while uncontrolled", "onValueChange cancel() prevents opening while controlled", "onValueChange cancel() prevents opening while multiple", "onValueChange cancel() prevents closing while multiple".
- **ACC-EVT-05** — MUST. `details.event` for `'trigger-press'` is the native event underlying the click (pointer click, or the click produced by keyboard activation), never a synthetic placeholder event.
  Source: `collapsible/root/useCollapsibleRoot.ts:33`. Test: [L2] `AccordionRoot.test.tsx` "default item value" (`event.type` is not `'base-ui'`, for both a pointer press and Space) (L4 for the keyboard-activation clause).
- **ACC-EVT-06** — MUST. A `beforematch` event on a Panel element produces `Item.onOpenChange(true, { reason: 'none', event: <beforematch Event> })`, then (unless canceled there) `Root.onValueChange(next, sameDetails)` with `next` computed per ACC-STA-05/06 (so in single mode the previously open Item closes). A `cancel()` in **either** callback counts as a canceled reveal (COL-EVT-05, TRN-ANI-33); an ignored request in consumer-owned mode is reverted per TRN-ANI-34.
  Source: `collapsible/panel/useCollapsiblePanel.ts:421-447`; `accordion/panel/AccordionPanel.tsx:41-50,84-95`; `accordion/item/AccordionItem.tsx:56-66,74-85`. Test: none in `accordion/` (mechanism tested by `CollapsiblePanel.test.tsx`; §16 Q12).
- **ACC-EVT-07** — MUST NOT. Neither callback is called when the consumer changes `value`, when an Item is closed implicitly because another Item opened in single mode, nor when a transition completes. Only the Item whose Trigger (or Panel, for `beforematch`) originated the request has its `onOpenChange` called.
  Source: `accordion/item/AccordionItem.tsx:56-66` (sole call path from `useCollapsibleRoot.ts:35` and `useCollapsiblePanel.ts:424`). Test: [L2] `AccordionRoot.test.tsx` controlled "open state" (external value changes, no handlers); "`multiple` is false" (one call per press).
- **ACC-EVT-08** — MUST. For both callbacks `details.trigger` is `undefined` and `details.allowPropagation()` has no effect, as COL-EVT-07 states for Collapsible (the Item and Root receive the details object created by the Collapsible trigger or panel mechanism).
  Source: `collapsible/root/useCollapsibleRoot.ts:33`; `collapsible/panel/useCollapsiblePanel.ts:422`; `accordion/item/AccordionItem.tsx:56-64`. Test: none in `accordion/` (COL-EVT-07's `CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails" exercises the shared details creation).
- **ACC-EVT-09** — MUST. The latest `Accordion.Item.onOpenChange` and `Accordion.Root.onValueChange` functions supplied by the consumer are the ones invoked, even if the consumer replaced them between renders (COL-EVT-08). The development-only "Cannot call an event handler while rendering" guard of the wrappers is not adopted: as in COL-EVT-08, the wrapped handlers are never exposed to consumer code.
  Source: `accordion/root/AccordionRoot.tsx:67-89`; `accordion/item/AccordionItem.tsx:56-66`; `packages/utils/src/useStableCallback.ts:35-62`. Test: none.

## 8. Form integration

Not applicable — Accordion has no `name`, renders no hidden input, and does not participate in Field/Form validation.

## 9. Lifecycle, mounting and animation

Every Panel runs the Collapsible panel lifecycle unchanged: animation-type detection, measurement, `data-starting-style`/`data-ending-style` sequencing, held/cached sizes, initially-open suppression, re-attachment, and `beforematch` handling (COL-ANI-01 … COL-ANI-30; TRN-ANI-32 … TRN-ANI-34, TRN-DOM-08/09, TRN-ALG-07). Timing vocabulary is as in [collapsible](collapsible.md) §9 and conventions §4.

- **ACC-ANI-01** — MUST. Each Panel MUST satisfy COL-ANI-01 through COL-ANI-30, reading "the Panel's open state" as its Item's derived open flag, "the Root" (its `mounted` flag and transition status) as the Panel's Item (ACC-STA-09), "`--collapsible-panel-height`/`--collapsible-panel-width`" as `--accordion-panel-height`/`--accordion-panel-width`, and `keepMounted`/`hiddenUntilFound` as the effective values of ACC-API-16/17. Server: relaxed, see §9.
  Source: `accordion/panel/AccordionPanel.tsx:76-95,118-130`; `collapsible/panel/useCollapsiblePanel.ts`. Test: [L4] `AccordionPanel.test.tsx` (all motion tests).
- **ACC-ANI-02** — MUST. When opening Item B closes Item A in single mode, A's Panel MUST run its full closing lifecycle while B's opens: A keeps its element in the document without `hidden`, shows `data-ending-style` with `--accordion-panel-height` in `px`, and only becomes hidden (or is removed) when its own exit motion finishes; B is open and not hidden throughout. Server: relaxed, see §9.
  Source: `accordion/root/AccordionRoot.tsx:74-75`; `collapsible/panel/useCollapsiblePanel.ts:231-353`. Test: [L4] `AccordionPanel.test.tsx` "keeps the closing panel visible until its exit transition completes when switching items". Docs: CHANGELOG.md v1.0.0-beta.5 "Fix flaky exit transition (#3101)".
- **ACC-ANI-03** — MUST. Server-rendered markup of an Item open at first render contains inline `animation-name: none` on its Panel while preserving other consumer inline animation properties (COL-ANI-14).
  Source: `accordion/panel/AccordionPanel.tsx:128-130`. Test: [L2] `AccordionPanel.test.tsx` "suppresses the initial keyframe animation from inline styles when rendered open".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 06 Amendments 2026-10-18 rule 3): at L2 the SSR title runs on the first render; the prerendered markup is asserted at L4 (ACC-DOM-18).
- **ACC-ANI-04** — MUST. A Panel opened by the user whose subtree is detached and re-attached by a state-preserving keep-alive facility MUST NOT replay its opening keyframe animation (COL-ANI-20).
  Source: `collapsible/panel/useCollapsiblePanel.ts:62,76-81,122-129,142-147`. Test: [L4] `AccordionPanel.test.tsx` "does not replay open keyframe animations from inline styles when revealing a panel opened by the user".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (collapsible §16 item 14): React.Activity is restated as the Panel's `Enabled` (`accordion-reattach`).
- **ACC-ANI-05** — MUST. A `beforematch` reveal opens the Item through the Root value (ACC-EVT-06), not through a local open flag; the motion skip, `data-starting-style` removal and revert timing are those of TRN-ALG-07 (TRN-ANI-32 … TRN-ANI-34).
  Source: `collapsible/panel/useCollapsiblePanel.ts:421-447`; `accordion/item/AccordionItem.tsx:68-72` (the Item's open is consumer-owned internally, so the mechanism's own open request is inert and only the value change opens it). Test: none in `accordion/`. Docs: `components/accordion/page.mdx` §"Hidden until found"; CHANGELOG.md v1.9.0 "Expand `hiddenUntilFound` panels synchronously on `beforematch`".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (settled by the orchestrator 2026-10-25, M3 plan): the reveal through the Item's chain has port titles on both modes (`accordion.spec.ts` "<Accordion.Panel /> beforematch": open with the single-mode close of the other item, cancel in the Item callback, cancel in `OnValueChange`, an ignored controlled request) and at L2 (`AccordionPortTests`).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- ACC-STA-09 — TRN-ANI-03/09/10 row (through COL-STA-04): an Item's `mounted` becomes `false` in the first render batch after the `transitions` module's completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- ACC-ANI-01 — TRN-ANI-03/09/10 row, through the adopted COL-STA-04, COL-ANI-09, COL-ANI-10 and COL-ANI-11 relaxations (collapsible §9): unmounts and held-size clearing land in the first render batch after JS reports completion or detection; JS sets `hidden` meanwhile.
- ACC-ANI-02 — TRN-ANI-03/09/10 row: the closing Item A's Panel becomes hidden (or is removed) in the first render batch after its completion message, about two round trips after its exit motion ends; JS sets `hidden` at animation end meanwhile.
- ACC-STA-03 — RND-STA-01 row for mirrored fields: the Panel state's `transitionStatus` (and so `ClassValue`/`StyleValue`) updates one round trip after JS writes `data-starting-style`/`data-ending-style`.
- ACC-DOM-14 — TRN-STA-04/05/07/09 row, met on both modes: the next-frame `data-starting-style`/`data-ending-style` flips are JS-owned with C# mirroring; listed so they are not moved back to C#.
- ACC-ANI-03 — met on both modes by the handover at attach (decisions/04-js-interop-boundary.md Amendments 2026-10-18): C# renders `animation-name: none` and the consumer's merged `style` in prerender and the first interactive render; JS owns the Panel `style` from `attach`. Listed so the initial value is not withheld from prerender.

Added 2026-10-26 (M3 track 6; the decisions/01-hosting-model-and-timing.md rows that COL §9 lists, ADR 01's table unchanged; settled by the orchestrator 2026-10-25):

- ACC-STA-09, ACC-ANI-01, ACC-ANI-02 on a kept or hidden-until-found Panel — TRN-STA-03/05/06/10 row, met on both modes: the `collapsible` module reads the open state from the applied render batch (`openSelector`, `[data-open]`) and runs the opening and closing passes before paint.
- ACC-ANI-01 on a Panel element that the opening render creates (not kept mounted) — the row of 2026-10-21 for a new panel element: met on WebAssembly; on a Server with latency the new element shows its `[data-starting-style]` look for about a round trip after the first paint (`accordion.spec.ts` "a new panel element shows its starting look until its module attaches").
- ACC-EVT-06, ACC-ANI-05 — the reveal row (decision point `reveal`, fallback run): on Server the Item's `OnOpenChange` and the Root's `OnValueChange` run a round trip after the dispatch, and a `Cancel()` in either or an ignored controlled request shows the revealed content for that round trip before `hidden="until-found"` returns.
- ACC-STA-09, ACC-ANI-02 — measured under the TRN-ANI-03 row (ADR 01 verification item 2): the removal lands about one round trip after the exit ends, where the bullets above read "about two"; the convergent `hidden` keeps the closed Panel hidden meanwhile (`accordion.spec.ts` "a closed panel is hidden at its exit end ahead of its removal on Server").
- ACC-STA-13, ACC-DOM-05 — the "EVD-STA-07, CMP-STA-10, FFI registration" row: the published index lands after paint plus a round trip on Server (open to track 1 with ACC-STA-13).
- ACC-KEY-01, ACC-KEY-02 on a non-native Trigger with a consumer `onkeydown` — the "C#-origin consumer vetoes" row (`activate`): on Server the consumer's veto of the keyboard activation runs a round trip after the key.

## 10. Positioning

Not applicable — Accordion has no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` / state attributes | [rendering-and-composition](../architecture/rendering-and-composition.md) | Root: `'div'`, mapping `{ value: → null }`. Item and Header (`'h3'`): `accordionStateAttributesMapping` = `collapsibleOpenStateMapping` + `index → data-index` + `transitionStatusMapping` + `value → null`. Trigger: `'button'`, the item mapping with `open` replaced by `triggerOpenStateMapping`. Panel: `'div'`, item mapping against Panel state; `style` resolved by the Panel and re-merged (ACC-DOM-18). |
| `useControlled` | [shared-utilities](../architecture/shared-utilities.md) | Root: `{ controlled: value, default: defaultValue ?? EMPTY_ARRAY, name: 'Accordion', state: 'value' }`. Each Item (inside `useCollapsibleRoot`): `{ controlled: <derived open>, default: false, name: 'Collapsible', state: 'open' }` — always consumer-owned. |
| `useCollapsibleRoot` | [collapsible](collapsible.md) | Per Item: `{ open: isOpen, onOpenChange: <Item wrapper → Root>, disabled: <effective> }`, no `defaultOpen`. Its returned state is re-provided as Collapsible shared state for the Item's Trigger/Panel. |
| `useCollapsiblePanel` | [collapsible](collapsible.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md) | Same parameters as `Collapsible.Panel`, except `hiddenUntilFound`/`keepMounted` = Panel prop ?? Root prop and `id` = `idProp ?? defaultPanelId`. |
| `useTransitionStatus` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | Via `useCollapsibleRoot`: `(open, enableIdleState = true, deferEndingState = true)`. |
| `CompositeList` + `useCompositeListItem` | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) | Item registry only: Root provides a list over its Items' elements; Items register without explicit index, `guess` off, no metadata/labels. No `CompositeRoot`, no list navigation, no typeahead. |
| `useButton` + `useFocusableWhenDisabled` | [button-behavior](../architecture/button-behavior.md) | `{ disabled: trigger.disabled \|\| item effective disabled, native: nativeButton }`; `composite` inferred **false** (no composite root ancestor), `focusableWhenDisabled` unset, `tabIndex` 0. |
| `useBaseUiId` | [rendering-and-composition](../architecture/rendering-and-composition.md) | Three generated ids per Item: fallback item value, default trigger id, default panel id (the last inside `useCollapsibleRoot`). |
| `createChangeEventDetails` / reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | `'trigger-press'` (click native event), `'none'` (`beforematch` event); one details object shared by Item and Root callbacks. |
| `warn` (log-once) | [shared-utilities](../architecture/shared-utilities.md) | Two Accordion warnings (TRN-EDGE-04) plus the shared Collapsible panel warning (COL-EDGE-03). |

## 12. Edge cases, invariants and dev warnings

- **ACC-EDGE-01** — MUST. Rendering `Accordion.Item` (or `Accordion.Panel`) without an ancestor `Accordion.Root` throws an `Error` with message `Base UI: AccordionRootContext is missing. Accordion parts must be placed within <Accordion.Root>.` in all builds.
  Source: `accordion/root/AccordionRootContext.ts:22-29`; `accordion/item/AccordionItem.tsx:41-46`; `accordion/panel/AccordionPanel.tsx:38-39`. Test: [L2] `AccordionItem.test.tsx` "throws when rendered outside an Accordion.Root".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (the PRG-EDGE-01 precedent; ADR 08 §4): the port's message reads ``Blazix: The `AccordionRoot` context is missing. Accordion parts must be placed within `<AccordionRoot>`.``, thrown in every build.
- **ACC-EDGE-02** — MUST. Rendering `Accordion.Header` without an ancestor `Accordion.Item` throws `Base UI: AccordionItemContext is missing. Accordion parts must be placed within <Accordion.Item>.` in all builds.
  Source: `accordion/item/AccordionItemContext.ts:17-24`; `accordion/header/AccordionHeader.tsx:21`. Test: [L2] `AccordionHeader.test.tsx` "throws when rendered outside an Accordion.Item".
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): the port's message reads ``Blazix: The `AccordionItem` context is missing. Accordion parts must be placed within `<AccordionItem>`.``, thrown in every build.
- **ACC-EDGE-03** — MUST. Rendering `Accordion.Trigger` without an ancestor `Accordion.Item`, or `Accordion.Panel` inside a Root but without an Item, throws the Collapsible message of COL-EDGE-01 (`Base UI: CollapsibleRootContext is missing. Collapsible parts must be placed within <Collapsible.Root>.`), because those parts look up the per-item collapsible state first. An `Accordion.Trigger` inside a `Collapsible.Root` but outside any `Accordion.Item` throws the ACC-EDGE-02 message.
  Source: `accordion/trigger/AccordionTrigger.tsx:40,49`; `accordion/panel/AccordionPanel.tsx:41-50`; `collapsible/root/CollapsibleRootContext.ts:15-23`. Test: none.
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): the lookup order is kept: the Trigger reads the collapsible state first (COL-EDGE-01's port message, ``Blazix: The `CollapsibleRoot` context is missing. …``), the Panel the Root's first; `AccordionPortTests` "port: a trigger outside an item and a panel without an item throw the collapsible message, a trigger in a collapsible root the item message".
- **ACC-EDGE-04** — MUST. In development, a Root with `hiddenUntilFound` truthy and `keepMounted` explicitly `false` logs (once) the Accordion.Root message quoted in TRN-EDGE-04; panels still render with `hidden="until-found"`. Only the Root's own two props are inspected.
  Source: `accordion/root/AccordionRoot.tsx:46-56`. Test: [L2] `AccordionRoot.test.tsx` "warns when hiddenUntilFound overrides keepMounted={false}" (L4 for the `hidden="until-found"` clause).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (settled by the orchestrator 2026-10-25; worded as collapsible §16 item 17): ``Blazix: The `KeepMounted` parameter set to `false` on `AccordionRoot` is ignored when `HiddenUntilFound` is enabled, since panels must remain mounted while closed.``
- **ACC-EDGE-05** — MUST. In development, a Panel whose own `keepMounted` is explicitly `false` and whose effective `hiddenUntilFound` (own or inherited from the Root) is `true` logs (once) the Accordion.Panel message quoted in TRN-EDGE-04; the Panel renders with `hidden="until-found"`. A Root-level `keepMounted={false}` does not trigger the Panel warning.
  Source: `accordion/panel/AccordionPanel.tsx:57-67`. Test: [L2] `AccordionPanel.test.tsx` "warns when a panel enables hiddenUntilFound and disables keepMounted" (L4 for the `hidden="until-found"` clause).
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): ``Blazix: The `KeepMounted` parameter set to `false` on an `AccordionPanel` is ignored when `HiddenUntilFound` is enabled on the panel or root, since the panel must remain mounted while closed.``
- **ACC-EDGE-06** — SHOULD. Panels emit the mixed transition/animation warning of COL-EDGE-03, and Triggers emit the `nativeButton` mismatch errors of COL-EDGE-04, under the same conditions as in Collapsible. (No Accordion test asserts either message.)
  Source: `collapsible/panel/useCollapsiblePanel.ts:501-511`; `internals/use-button/useButton.ts:36-66`. Test: none in `accordion/`.
- **ACC-EDGE-07** — SHOULD. In development, switching the Root's `value` between `undefined` and defined after the first render emits the UTL-EDGE-22 error, and changing `defaultValue` (compared by serialized content, so a fresh but equal array literal each render does not trigger it) emits UTL-EDGE-23, with `name: 'Accordion'`, `state: 'value'`.
  Source: `accordion/root/AccordionRoot.tsx:60-65`; `packages/utils/src/useControlled.ts:48-81`. Test: none.
- **ACC-EDGE-08** — MUST. `disabled` (Root, Item or Trigger) affects only user interaction: open Items stay open when disabled becomes `true`, and consumer `value` changes still open/close disabled Items (COL-EDGE-07).
  Source: `accordion/root/AccordionRoot.tsx:60-89` (`disabled` not read by value logic). Test: [L2] `AccordionRoot.test.tsx` "can disable the whole accordion" (`defaultValue={[0]}` item open while disabled).
- **ACC-EDGE-09** — MUST. An Item `value` of `null` or `undefined` is replaced by the generated identity (ACC-API-10); a Root value containing `null` therefore never opens an Item declared with `value={null}` (§16 Q7).
  Source: `accordion/item/AccordionItem.tsx:50`. Test: none.
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)) (ADR 08 Amendments 2026-10-26): `null` is replaced by the generated identity only under `string`; under any other `TValue` an Item whose value is `null` throws the error of ACC-API-10's note.
- **ACC-EDGE-10** — MUST. Membership (ACC-STA-04) uses SameValueZero while removal in multiple mode (ACC-STA-06) and the single-mode check (ACC-STA-05) use strict equality; an Item whose identity is `NaN` can be opened but a close request leaves `NaN` in the value (§16 Q8).
  Source: `accordion/item/AccordionItem.tsx:54`; `accordion/root/AccordionRoot.tsx:75,80`. Test: none.
- **ACC-EDGE-11** — MUST. Items sharing one identity open and close together; in multiple mode, a close request removes every occurrence of that identity.
  Source: `accordion/item/AccordionItem.tsx:54`; `accordion/root/AccordionRoot.tsx:80`. Test: none.
- **ACC-EDGE-12** — MUST. With `multiple` `false`, a consumer-supplied value with more than one element opens every listed Item; a subsequent request from any Item replaces the whole value per ACC-STA-05 (pressing the first listed Item yields `[]`; pressing another listed, open Item yields `[thatItem]`, i.e. it stays open and the others close).
  Source: `accordion/root/AccordionRoot.tsx:74-75`; `accordion/item/AccordionItem.tsx:54`. Test: none.
- **ACC-EDGE-13** — MUST. Several Triggers in one Item all render the same generated id when none supplies an `id` (duplicate ids), and all reflect and toggle the same Item.
  Source: `accordion/trigger/AccordionTrigger.tsx:49-51`. Test: none.
- **ACC-EDGE-14** — MUST. `Collapsible.Trigger` and `Collapsible.Panel` placed inside an `Accordion.Item` bind to that Item's collapsible state (they toggle and reflect the Item, and the toggle goes through the Item and Root callbacks); they render Collapsible attributes and variable names, not Accordion ones.
  Source: `accordion/item/AccordionItem.tsx:74-85,122-127`. Test: none.
  Port note 2026-10-26 (M3 track 6, [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105)): the Item cascades its `CollapsibleRootContext` and its `AccordionItemContext<TValue>`, both fixed (settled; ADR 16 rule 9). A `CollapsibleTrigger` inside an Item mirrors the status a panel reports; without an `AccordionPanel` it reads `Starting` after an open (design point D5 of track 6, **pending the user**).

## 13. Behaviors evidenced by tests

### `root/AccordionRoot.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (Root; `refInstanceof: HTMLDivElement`) | ACC-STR-01, ACC-STA-01; RND-DOM-36 … RND-DOM-48 |
| "warns when hiddenUntilFound overrides keepMounted={false}" | ACC-EDGE-04, ACC-API-06, ACC-DOM-15 |
| "renders correct ARIA attributes" | ACC-ARIA-02, ACC-ARIA-03, ACC-ARIA-04, ACC-API-15, ACC-DOM-19 |
| "references manual panel id in trigger aria-controls" | ACC-API-18, ACC-ARIA-02, ACC-STA-12 |
| "references manual trigger id in panel aria-labelledby" | ACC-API-15, ACC-ARIA-04 |
| "updates panel labeling when a manual trigger id is added or changed" | ACC-STA-11, ACC-ARIA-04, ACC-DOM-13 |
| "restores panel labeling when a manual trigger id is removed" | ACC-STA-11, ACC-ARIA-04 |
| "unregisters generated part ids when the trigger or panel unmounts" | ACC-STA-11, ACC-STA-12, ACC-ARIA-05 |
| "preserves generated part associations during hydration" | ACC-ARIA-06 |
| uncontrolled "open state" | ACC-API-03, ACC-API-10, ACC-PTR-01, ACC-ARIA-01, ACC-DOM-04, ACC-DOM-11, ACC-DOM-16 |
| uncontrolled "custom item value" (defaultValue) | ACC-API-02, ACC-STA-04 |
| controlled "open state" | ACC-API-01, ACC-STA-07, ACC-EVT-07, ACC-ARIA-01 |
| controlled "custom item value" (value) | ACC-API-01, ACC-STA-04 |
| "can disable the whole accordion" | ACC-API-05, ACC-DOM-06, ACC-ARIA-07, ACC-EDGE-08 |
| "can disable one accordion item" | ACC-API-11, ACC-DOM-06 |
| "does not toggle or fire callbacks when the %s is disabled" (2 variants: `root`, `item`) | ACC-API-05, ACC-API-11, ACC-API-14, ACC-KEY-03, ACC-PTR-02 |
| "allows onMouseUp to call preventBaseUIHandler on the trigger" | ACC-PTR-03 |
| "key: $key toggles the Accordion open state with $triggerKind triggers" (4 variants: Enter/Space × interactive/non-interactive) | ACC-KEY-01, ACC-KEY-02, ACC-KEY-04 |
| "opens and closes on Space keyup when rendering $triggerKind triggers" (2 variants: interactive, non-interactive) | ACC-KEY-02, ACC-EVT-01, ACC-DOM-12 (non-composite) |
| "onOpenChange cancel() prevents opening while uncontrolled" | ACC-EVT-02, ACC-API-12 |
| "onValueChange cancel() prevents opening while uncontrolled" | ACC-EVT-04 |
| "onValueChange cancel() prevents closing while uncontrolled" | ACC-EVT-04 |
| "onOpenChange cancel() prevents onValueChange while controlled" | ACC-EVT-02 |
| "onValueChange cancel() prevents opening while controlled" | ACC-EVT-04, ACC-STA-07 |
| "onValueChange cancel() prevents opening while multiple" | ACC-EVT-04 |
| "onValueChange cancel() prevents closing while multiple" | ACC-EVT-04 |
| "keeps an array item value intact while multiple" | ACC-STA-06 |
| "multiple items can be open when `multiple = true`" | ACC-API-04, ACC-STA-06, ACC-DOM-11 |
| "when false only one item can be open" | ACC-API-04, ACC-STA-05, ACC-DOM-04 |
| "default item value" | ACC-STA-06, ACC-EVT-03, ACC-EVT-05 |
| "custom item value" (onValueChange) | ACC-STA-06, ACC-EVT-03 |
| "`multiple` is false" | ACC-STA-05, ACC-EVT-03, ACC-EVT-07 |

### `item/AccordionItem.test.tsx`

| Test | Requirements |
|---|---|
| "throws when rendered outside an Accordion.Root" | ACC-EDGE-01, ACC-STR-02 |
| describeConformance (Item in Root; `refInstanceof: HTMLDivElement`) | ACC-STR-02; RND-DOM-36 … RND-DOM-48 |
| "does not report hidden=true after the item has started opening" | ACC-STA-10, ACC-STA-02 |

### `header/AccordionHeader.test.tsx`

| Test | Requirements |
|---|---|
| "throws when rendered outside an Accordion.Item" | ACC-EDGE-02 |
| describeConformance (Header in Root > Item; `refInstanceof: HTMLHeadingElement`) | ACC-STR-03; RND-DOM-36 … RND-DOM-48 |

### `trigger/AccordionTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`button: true`, `testRenderPropWith: 'button'`, `refInstanceof: HTMLButtonElement`; Trigger directly in Item) | ACC-STR-04, ACC-API-13; RND-DOM-36 … RND-DOM-49 |
| "renders the disabled attribute when disabled" | ACC-DOM-12 |
| "removes a disabled non-native trigger from the tab order" | ACC-DOM-12, ACC-KEY-03, ACC-STR-06 |
| "keeps a non-native trigger tabbable" | ACC-DOM-12, ACC-FOC-01 |
| "does not render the root value as data-value" | ACC-DOM-09 |
| "renders data-index on every trigger including the first" | ACC-DOM-05, ACC-STA-13 |
| "keeps data-panel-open instead of data-open" | ACC-DOM-11 |

### `panel/AccordionPanel.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Accordion.Panel keepMounted />` in Root > Item) | ACC-STR-05, ACC-STA-03; RND-DOM-36 … RND-DOM-48 |
| "warns when a panel enables hiddenUntilFound and disables keepMounted" | ACC-EDGE-05, ACC-DOM-15 |
| "suppresses the initial keyframe animation from inline styles when rendered open" | ACC-ANI-03, ACC-DOM-18 |
| "passes root keepMounted to closed panels" | ACC-API-07, ACC-API-16, ACC-DOM-15 |
| "passes root hiddenUntilFound to closed panels and allows panel overrides" | ACC-API-06, ACC-API-17, ACC-DOM-15, ACC-DOM-16 |
| "keeps the closing panel visible until its exit transition completes when switching items" | ACC-ANI-02, ACC-STA-08, ACC-STA-09, ACC-DOM-14, ACC-DOM-17 |
| React.Activity "does not replay open keyframe animations from inline styles when revealing a panel opened by the user" | ACC-ANI-04, ACC-ANI-01 |

No requirement exists only because of a test; every asserted behavior is captured in §3–§12.

### `root/AccordionRoot.spec.tsx`

Type-level only: `onValueChange` value type follows `value`/`defaultValue` element types or the explicit generic (`string[]`, `number[]`, `('a' | 'b')[]`, `(string | null)[]`), defaults to `any[]` with no generic, and a `value` not matching an explicit generic is a type error. Covered by §3.6.

Test-environment notes: tests marked `skipIf(isJSDOM)` run only in a real browser; the React.Activity block also requires React ≥ 19. `globalThis.BASE_UI_ANIMATIONS_DISABLED` makes animation completion immediate (COL §9.6).

## 14. React-specific notes and porting considerations

Non-normative.

- **Two layers of shared state.** Each Item publishes two ancestor-provided objects: the Collapsible root state (so Trigger and Panel reuse Collapsible code, and real `Collapsible.*` parts also work inside an Item, ACC-EDGE-14) and the Accordion item state (index, trigger-id registration, Item state object). A port can merge these into one per-item object; the observable contract is §3–§7.
- **Internally controlled Collapsible.** The Item calls `useCollapsibleRoot` with `open` always defined, so the internal `setOpen` is a no-op. Opening happens only via `handleValueChange` → Root `setValue` → re-render → `isOpen`. This is why `beforematch` works through the Item's `onOpenChange` wrapper rather than the panel's own `setOpen` (ACC-ANI-05), and why a Root-level cancel is treated as a canceled reveal by the panel mechanism (it reads `isCanceled` after `onOpenChange` returns). Port: see decisions/05-state-store-and-event-details.md §2 (`Value`/`ValueChanged`/`DefaultValue` plus `OnValueChange(details)`, called first; cancel stops the request) for the Root value; the Item's open flag stays derived, never a second controlled pair.
- **Id registration in layout effects.** Trigger and Panel register their ids with `useIsoLayoutEffect` and a functional `setState` that distinguishes `undefined` (never registered → generated id) from `null` (registered then unmounted → no id). The test "unregisters generated part ids…" runs under StrictMode, which runs effect cleanup + setup twice; the guarded unregister (`currentId === registeredId ? null : currentId`) and re-register keep the result stable. A port must preserve the three-state distinction. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, post-commit pre-paint mapping) and §Relaxation table (row UTL-STA-19..22, EVD-STA-07, CMP-STA-10, FFI registration: post-commit C# state work lands after paint plus a round trip on Server).
- **Item index timing.** `useCompositeListItem` starts at `-1` and receives its index from `CompositeList`'s layout-effect flush, which triggers one synchronous re-render before paint. Server markup therefore contains `data-index="-1"` on every Item/Header/Trigger/Panel; clients correct it before first paint after hydration. `CompositeList` also uses a `MutationObserver` to re-index after DOM moves performed outside React. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry move detection) and decisions/01-hosting-model-and-timing.md §Relaxation table (row CMP-STA-10).
- **Generated ids.** Three `useBaseUiId()` calls per Item (fallback value, trigger id, panel id) — `base-ui-` + `React.useId()` on React ≥ 18 (stable across SSR/hydration, ACC-ARIA-06), a post-mount counter on React 17 (first render has no id, and the fallback item value is `undefined` there). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5, generated ids `base-ui-<n>` from a scoped counter).
- **Stable callbacks.** `handleValueChange` (Root) and the Item's `onOpenChange` are `useStableCallback`s: they always read the latest `value`, `multiple` and consumer callbacks without changing identity.
- **Frozen default.** `EMPTY_ARRAY` is `Object.freeze([])`, shared by every uncontrolled Root without `defaultValue`; consumers mutating `state.value` would throw in strict mode. Ports should expose an immutable empty collection or document read-only semantics.
- **Panel motion and measurement.** The Panel's measurement, `data-starting-style`/`data-ending-style` flips and `hidden="until-found"`/`beforematch` handling are the Collapsible panel mechanism's (ACC-ANI-01). Port: see decisions/04-js-interop-boundary.md §1 (`transitions` module: TRN-ALG-03, next-frame flips, `hidden="until-found"`/`beforematch` TRN-ALG-07; `dom` module: Collapsible height) and §2 (ownership list: `Accordion.Panel` `hidden="until-found"` and the measured size variables, here `--accordion-panel-height/width`); decisions/01-hosting-model-and-timing.md §Relaxation table (rows TRN-ANI-03/09/10 and TRN-STA-04/05/07/09). How §2's "prerender emits no JS-owned attribute; C# never renders `style`" squares with ACC-ANI-03 and the `auto` variables in server markup is open (§16 Q14).
- **Generic component typing.** `AccordionRoot` is cast to a generic call signature so that `Value` flows from `value`/`defaultValue` to `onValueChange`; this is purely a TypeScript concern.
- **Development-only effects.** The two `keepMounted` warnings are registered inside `process.env.NODE_ENV !== 'production'` branches (conditional hooks, safe because the condition is a build-time constant).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| ACC-STR-01 | MUST | Root div; owns value; item registry; no handlers | 2 |
| ACC-STR-02 | MUST | Item div inside Root; one collapsible state machine per Item | 2 |
| ACC-STR-03 | MUST | Header h3 inside Item; structural only | 2 |
| ACC-STR-04 | MUST | Trigger button inside Item; Header optional | 2 |
| ACC-STR-05 | MUST | Panel div inside Item; at most one per Item | 2 |
| ACC-STR-06 | MUST | Missing Panel/Trigger → dangling generated id references | 2 |
| ACC-STR-07 | MUST | Nested accordions independent | 2 |
| ACC-API-01 | MUST | `value` consumer-owned array | 3.1 |
| ACC-API-02 | MUST | `defaultValue` read once; default frozen `[]` | 3.1 |
| ACC-API-03 | MUST | `onValueChange` optional | 3.1 |
| ACC-API-04 | MUST | `multiple` default false | 3.1 |
| ACC-API-05 | MUST | Root `disabled` disables every item | 3.1 |
| ACC-API-06 | MUST | Root `hiddenUntilFound` = Panel default | 3.1 |
| ACC-API-07 | MUST | Root `keepMounted` = Panel default | 3.1 |
| ACC-API-08 | MUST | `orientation` default vertical; attribute/state only | 3.1 |
| ACC-API-09 | MUST | `loopFocus` consumed, no effect | 3.1 |
| ACC-API-10 | MUST | Item identity = value ?? generated id | 3.2 |
| ACC-API-11 | MUST | Item effective disabled = item ‖ root | 3.2 |
| ACC-API-12 | MUST | Item `onOpenChange` optional | 3.2 |
| ACC-API-13 | MUST | Trigger `nativeButton` per COL-API-05 | 3.4 |
| ACC-API-14 | MUST | Trigger disabled = own ‖ item; cannot re-enable; no data-disabled | 3.4 |
| ACC-API-15 | MUST | Trigger id: non-empty prop else generated; registered | 3.4 |
| ACC-API-16 | MUST | Panel keepMounted = own ?? root | 3.5 |
| ACC-API-17 | MUST | Panel hiddenUntilFound = own ?? root | 3.5 |
| ACC-API-18 | MUST | Panel id = prop ?? generated; `""` rendered but not registered | 3.5 |
| ACC-STA-01 | MUST | Root state `{value, disabled, orientation}` | 3.1 |
| ACC-STA-02 | MUST | Item/Header/Trigger state fields; no transitionStatus | 3.2 |
| ACC-STA-03 | MUST | Panel state = Item state + Panel transitionStatus | 3.5 |
| ACC-STA-04 | MUST | Item open ⇔ value includes identity (SameValueZero) | 4.1 |
| ACC-STA-05 | MUST | Single mode: `value[0] === id ? [] : [id]` | 4.1 |
| ACC-STA-06 | MUST | Multiple mode: append on open, filter on close | 4.1 |
| ACC-STA-07 | MUST | Apply only if not canceled and uncontrolled | 4.1 |
| ACC-STA-08 | MUST | Implicitly closed items transition independently, no callback | 4.1 |
| ACC-STA-09 | MUST | Per-item mounted/status per COL-STA-04/05/06 | 4.2 |
| ACC-STA-10 | MUST | hidden = !open && !mounted; never true while open | 4.2 |
| ACC-STA-11 | MUST | Trigger id resolution/registration | 4.3 |
| ACC-STA-12 | MUST | Panel id resolution per COL-STA-09 | 4.3 |
| ACC-STA-13 | MUST | Item index = document order in registry; -1 before publish | 4.4 |
| ACC-DOM-01 | MUST | Root data-disabled | 5.1 |
| ACC-DOM-02 | MUST | Root data-orientation | 5.1 |
| ACC-DOM-03 | MUST | Root: no data-value/open/closed, role, aria, dir | 5.1 |
| ACC-DOM-04 | MUST | Item/Header/Panel data-open/data-closed | 5.2 |
| ACC-DOM-05 | MUST | data-index on all item parts incl. "0" | 5.2 |
| ACC-DOM-06 | MUST | data-disabled from item effective disabled | 5.2 |
| ACC-DOM-07 | MUST | data-orientation on all item parts | 5.2 |
| ACC-DOM-08 | MUST | data-hidden on all item parts | 5.2 |
| ACC-DOM-09 | MUST | No data-value anywhere | 5.2 |
| ACC-DOM-10 | MUST | No starting/ending style on Item/Header/Trigger | 5.2 |
| ACC-DOM-11 | MUST | Trigger data-panel-open only | 5.3 |
| ACC-DOM-12 | MUST | Trigger button attrs, non-composite | 5.3 |
| ACC-DOM-13 | MUST | Trigger always has id | 5.3 |
| ACC-DOM-14 | MUST | Panel starting/ending style per COL | 5.4 |
| ACC-DOM-15 | MUST | Panel hidden / until-found per COL | 5.4 |
| ACC-DOM-16 | MUST | Panel presence predicate with effective values | 5.4 |
| ACC-DOM-17 | MUST | `--accordion-panel-height/width` | 5.4 |
| ACC-DOM-18 | MUST | Panel attribute/style precedence | 5.4 |
| ACC-DOM-19 | MUST | Panel always has id attribute | 5.4 |
| ACC-ARIA-01 | MUST | Trigger aria-expanded | 5.5 |
| ACC-ARIA-02 | MUST | Trigger aria-controls while open | 5.5 |
| ACC-ARIA-03 | MUST | Panel role="region" | 5.5 |
| ACC-ARIA-04 | MUST | Panel aria-labelledby = trigger id | 5.5 |
| ACC-ARIA-05 | MUST | aria-labelledby/aria-controls dropped and restored with parts | 5.5 |
| ACC-ARIA-06 | MUST | Generated ids stable through hydration | 5.5 |
| ACC-ARIA-07 | MUST | No ARIA on Root/Item/Header | 5.5 |
| ACC-KEY-01 | MUST | Enter toggles | 6.1 |
| ACC-KEY-02 | MUST | Space toggles on keyup | 6.1 |
| ACC-KEY-03 | MUST | Disabled: no toggle, no callbacks | 6.1 |
| ACC-KEY-04 | MUST NOT | No arrow/Home/End handling; Tab native | 6.1 |
| ACC-PTR-01 | MUST | Click requests toggle through Item then Root | 6.2 |
| ACC-PTR-02 | MUST | Disabled click ignored | 6.2 |
| ACC-PTR-03 | MUST | Consumer handlers chained; preventBaseUIHandler safe | 6.2 |
| ACC-FOC-01 | MUST | No focus movement; no roving tabindex | 6.3 |
| ACC-I18N-01 | MUST | Direction not consumed; no dir | 6.5 |
| ACC-EVT-01 | MUST | Item onOpenChange first, before change | 7 |
| ACC-EVT-02 | MUST | Item cancel blocks Root callback and change | 7 |
| ACC-EVT-03 | MUST | Root onValueChange with next value, same details | 7 |
| ACC-EVT-04 | MUST | Root cancel blocks uncontrolled change | 7 |
| ACC-EVT-05 | MUST | trigger-press event is native event | 7 |
| ACC-EVT-06 | MUST | beforematch → 'none' via Item then Root | 7 |
| ACC-EVT-07 | MUST NOT | No callbacks for prop changes, implicit closes, completions | 7 |
| ACC-EVT-08 | MUST | trigger undefined; allowPropagation no-op | 7 |
| ACC-EVT-09 | MUST | Latest consumer callbacks invoked | 7 |
| ACC-ANI-01 | MUST | Panels satisfy COL-ANI-01…30 with substitutions | 9 |
| ACC-ANI-02 | MUST | Switching items: concurrent exit/enter | 9 |
| ACC-ANI-03 | MUST | SSR initially open: animation-name none | 9 |
| ACC-ANI-04 | MUST | No keyframe replay on re-attachment | 9 |
| ACC-ANI-05 | MUST | beforematch opens through Root value | 9 |
| ACC-EDGE-01 | MUST | Missing Root error | 12 |
| ACC-EDGE-02 | MUST | Missing Item error (Header) | 12 |
| ACC-EDGE-03 | MUST | Trigger/Panel outside Item → Collapsible error | 12 |
| ACC-EDGE-04 | MUST | Root keepMounted/hiddenUntilFound warning | 12 |
| ACC-EDGE-05 | MUST | Panel keepMounted/hiddenUntilFound warning | 12 |
| ACC-EDGE-06 | SHOULD | Panel mixed-animation warning; Trigger nativeButton mismatch | 12 |
| ACC-EDGE-07 | SHOULD | useControlled warnings (Accordion, value) | 12 |
| ACC-EDGE-08 | MUST | disabled does not affect open state | 12 |
| ACC-EDGE-09 | MUST | null/undefined Item value → generated identity | 12 |
| ACC-EDGE-10 | MUST | SameValueZero vs strict equality asymmetry | 12 |
| ACC-EDGE-11 | MUST | Duplicate identities open/close together | 12 |
| ACC-EDGE-12 | MUST | Single mode with multi-element value | 12 |
| ACC-EDGE-13 | MUST | Multiple Triggers share generated id | 12 |
| ACC-EDGE-14 | MUST | Collapsible parts inside Item bind to Item | 12 |

Counts: STR 7, API 18, STA 13, DOM 19, ARIA 7, KEY 4, PTR 3, FOC 1, I18N 1, EVT 9, ANI 5, EDGE 14 — total 101.

## 16. Open questions

1. **Keyboard navigation between triggers does not exist.** The assignment hints at ArrowUp/Down or Left/Right by orientation and RTL, Home/End and looping. v1.9.0 has none of it: no key handler in `accordion/`, `loopFocus` is destructured and discarded (`root/AccordionRoot.tsx:34`), `orientation` only feeds state/`data-orientation` (`:37,95`), and both are documented as deprecated "following the APG guidance update … to remove roving focus" (`:187-194,207-215`; CHANGELOG v1.6.0 "Align keyboard navigation with APG (#4965)"). The Root's `CompositeList` is an item registry only (index assignment). Specified as ACC-KEY-04. Disabled-trigger skipping is therefore only the native tab-order effect (ACC-KEY-03). Resolved (2026-10-26, M3 track 6): specified as observed and built as written: no key handler, `LoopFocus` discarded, `Orientation` in the state only (`accordion.spec.ts` "key: $key toggles the Accordion open state with $triggerKind triggers" asserts an arrow is not handled).
2. **Panel `id=""`.** `AccordionPanel.tsx:54-55` registers `idProp || undefined` but renders `idProp ?? defaultPanelId`, so `id=""` yields `<div id="">` while the Trigger's `aria-controls` references the generated panel id. Collapsible (`CollapsiblePanel.tsx:61-62`) uses `||` for both. Accordion's own Trigger uses `||` for both (`AccordionTrigger.tsx:50-51`). Likely an oversight; specified as observed in ACC-API-18. A port may prefer `||`, which would diverge from source only for `id=""`. Resolved (2026-10-26, M3 track 6): by ACC-API-18, following the source (settled by the orchestrator 2026-10-25): `id=""` renders `""` and registers nothing.
3. **Outline vs source data attributes and props.** `components/page.mdx` and the `*DataAttributes.ts` files omit attributes the source emits through the default projection: `data-closed` (Item, Header, Panel), `data-hidden` (Item, Header, Trigger, Panel), `data-orientation` (Item, Header, Trigger; listed only for Root and Panel). The outline also omits Trigger `disabled`/`id` and Panel `id`, which the source handles specially (ACC-API-14/15/18). `data-hidden` is untested and possibly an unintended by-product of the `hidden` state field (no custom mapping in `item/stateAttributesMapping.ts:7-12`). Resolved (2026-10-26, M3 track 6): kept as observed: `data-closed`, `data-hidden` and every part's `data-orientation` come from the default projection (`AccordionPortTests` "port: the item parts render data-orientation and data-hidden, and only the panel carries transition attributes").
4. **Architecture doc discrepancies.** (a) `button-behavior.md` §usage table (line 575) says Accordion.Trigger's composite mode comes from "ctx (Accordion root is composite)". `AccordionRoot` renders a `CompositeList`, not a `CompositeRoot`, so `useCompositeRootContext(true)` is `undefined` and composite mode is off; the test "opens and closes on Space keyup…" confirms keyup (non-composite) activation. (b) `transitions-and-mounting.md` TRN-DOM-04 lists "Accordion item (`accordion/item/stateAttributesMapping.ts:10`)" as applying the transition mapping, but `AccordionItemState` has no `transitionStatus`, so it only takes effect on the Panel (ACC-DOM-10). (c) `event-details-and-reasons.md` reason matrix row for Accordion `'none'` cites a Collapsible test; no Accordion test exercises `beforematch`. Resolved: all three are corrected in the architecture docs (`button-behavior.md` usage-table row for `Accordion.Trigger` now says "not composite"; TRN-DOM-04 now names the Accordion panel only; `event-details-and-reasons.md` §reason matrix row for Accordion `none` now cites "none in `accordion/`"). Resolved (2026-10-26, M3 track 6): specified as observed; the three architecture rows stand as corrected.
5. **Trigger `disabled` semantics differ from Collapsible.** `AccordionTrigger.tsx:42` uses `disabledProp || contextDisabled` (can only add), whereas `CollapsibleTrigger.tsx:37` uses the context value only as a default (can override both ways). Also the Trigger's own `disabled` never reaches its state object, so a Trigger disabled only by its own prop has `disabled`/`aria-disabled` but no `data-disabled`. Specified as observed (ACC-API-14). Resolved (2026-10-26, M3 track 6): specified as observed and built as written (`AccordionTrigger.Disabled : bool`, adds only, never in the state).
6. **Single-mode request ignores direction.** `AccordionRoot.tsx:74-75` computes `value[0] === newValue ? [] : [newValue]` without consulting `nextOpen`. Harmless when the value has at most one element (the user-only invariant), but with a consumer-supplied multi-element value in single mode, closing a non-first open Item produces `[thatItem]` (it stays open). ACC-EDGE-12. Resolved (2026-10-26, M3 track 6): specified as observed and built as written (`AccordionValues.Next`; `AccordionValuesTests` "port: in single mode a request replaces a value of several items").
7. **`null` item values.** `AccordionRoot.spec.tsx:29-34` types `(string | null)[]` values as legal, but `AccordionItem.tsx:50` (`valueProp ?? fallbackValue`) replaces an Item `value={null}` with a generated id, so `null` in the Root value can never open an Item. ACC-EDGE-09. Resolved (2026-10-26, M3 track 6): specified as observed under `string`; under any other `TValue` the Item without a value is an always-on error (ADR 08 Amendments 2026-10-26; ACC-EDGE-09's note).
8. **Equality asymmetry.** Membership uses `Array.prototype.includes` (SameValueZero, `AccordionItem.tsx:54`), removal uses `!==` (`AccordionRoot.tsx:80`). Only observable for `NaN`. ACC-EDGE-10. Resolved (2026-10-26, M3 track 6): specified as observed and built as written (`Equality.SameValueZero` and `StrictEquals`; `AccordionValuesTests` on `double`).
9. **Server-rendered `data-index="-1"`.** Because the registry assigns indexes post-commit (`useCompositeListItem.ts:42-55`, no `guess`), SSR markup carries `data-index="-1"` until hydration. CSS keyed on `data-index` will mismatch on first paint for server-rendered pages. Not tested. Resolved (2026-10-26, M3 track 6): by `-1` until the registry reports the index: prerender renders `data-index="-1"` (the report is open to track 1 with ACC-STA-13).
10. **Dangling `aria-labelledby` when no Trigger is rendered.** Mirrors COL §16 Q4: with a Panel but no Trigger ever mounted, `registeredTriggerId` is `undefined`, so `aria-labelledby` references the default trigger id (`AccordionItem.tsx:101-102`) that no element carries. ACC-STR-06. Resolved (2026-10-26, M3 track 6): specified as observed and built as written (the three-state trigger registration of `AccordionItemContext`).
11. **No `onOpenChange` for implicitly closed items.** In single mode, opening B closes A without calling A's `onOpenChange` (ACC-EVT-07). This matches the code's single call path but may surprise consumers using per-item callbacks to track state; the Root `onValueChange` is the only complete signal. Resolved (2026-10-26, M3 track 6): specified as observed and built as written (ACC-EVT-07; `AccordionRootTests` "open state (controlled)", "`multiple` is false").
12. **No Accordion-level `beforematch` tests.** ACC-EVT-06/ACC-ANI-05 rest on the Collapsible panel tests and on reading `AccordionItem.tsx:56-66` together with `useCollapsiblePanel.ts:421-447`. In particular, that a `cancel()` in Root `onValueChange` during `beforematch` is handled as a canceled reveal is inferred, not tested. Resolved (2026-10-26, M3 track 6): by the reveal port titles (settled by the orchestrator 2026-10-25): open, cancel in the Item callback, cancel in `OnValueChange`, the single-mode close of the other item and an ignored controlled request, on both modes (ACC-ANI-05's note).
13. **Duplicate trigger ids.** Several Triggers in one Item without explicit ids all render the same generated id (`AccordionTrigger.tsx:51`), producing invalid HTML. ACC-EDGE-13. Resolved (2026-10-26, M3 track 6): specified as observed and built as written (`AccordionPortTests` "port: several triggers in one item share the generated id and all toggle the item").
14. **Server markup vs ADR 04's style ownership (mirrors COL §16 Q11).** ACC-ANI-03 requires server-rendered markup of an initially-open Panel to carry inline `animation-name: none` while keeping consumer animation properties (asserted by "suppresses the initial keyframe animation from inline styles when rendered open", `panel/AccordionPanel.test.tsx:55-95`), and ACC-DOM-17 says the inline style always defines `--accordion-panel-height/width` (`auto` in server markup; `panel/AccordionPanel.tsx:119-130`). ADR 04 §2 lists the Accordion Panel's measured size variables as JS-owned and states that C# never renders `style` on an element with JS-owned style properties and that prerender emits no JS-owned attribute; read literally, the prerendered Panel has no inline `style`, so ACC-ANI-03 and the `auto` server values would fail on prerender. The decision taken for COL §16 Q11 applies here unchanged. Decided (2026-10-18): C# renders the initial value once and JavaScript owns the attribute from attach (ADR 04 Amendments, handover at attach); COL-ANI-14 / ACC-ANI-03 hold without relaxation. Resolved (2026-10-26, M3 track 6): decided 2026-10-18 (the handover at attach); built as written.
15. **Rows whose cited Base UI titles run only in a browser (port).** Decided (2026-10-26, M3 track 6; settled by the orchestrator 2026-10-25, M3 plan): the fifteen `[L2]` rows (ACC-API-01, API-03, API-04, API-10; STA-02, STA-07, STA-10; DOM-04, DOM-11, DOM-16; ARIA-01; PTR-01; EVT-03, EVT-05, EVT-07) and the two `[L1]` rows (ACC-STA-05, STA-06) stay at their layer: bUnit and the pure helpers observe what they assert (C#-rendered attributes, callback calls, the value algorithm). `toBeVisible()` reads as "rendered and not `hidden`" at L2; a closing panel leaves the document when the `collapsible` module reports the end of its exit, as Base UI's `BASE_UI_ANIMATIONS_DISABLED` ends it at once. No requirement changes.
16. **Design points of the implementing pull request (port; [issue 105](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/105) deliverable 17).** Built under the recommended options and **pending the user**: D1, the panel-status report's shape (an opt-in option and one report of the machine's status before the forced idle, once per change; about two Server messages per open and close of a panel); D2, the Item's request keeps `CollapsibleRootContext.RequestOpenAsync`'s trailing re-apply, which the three ownership titles show is a no-op; D3, the Item's element id, waiting on track 1's resolver; D4, the render cost of the value in every record, measured: on a committed toggle the Root renders once, each Item twice and each Header, Trigger and open Panel three times, at 3 and at 20 items (the Root's and each Item's cascade re-render their content in a later queue entry), and the request allocates 352 bytes at 3 items and 1712 at 20 (`AccordionPortTests`, `AccordionAllocationBudgetTests`); D5, real Collapsible parts inside an Item. The notes here and in ACC-STA-03, ACC-EDGE-14 change wording when the answers arrive.
