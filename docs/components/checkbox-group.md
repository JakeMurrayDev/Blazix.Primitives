# Checkbox Group

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/checkbox-group/` |
| Tests | `packages/react/src/checkbox-group/**/*.test.tsx` (2 files) |
| Docs page | `docs/src/app/(docs)/react/components/checkbox-group/page.mdx` |
| Prefix | `CKG` |
| Status | approved |
| Revised | 2026-10-23 |
| Builds on | [checkbox](checkbox.md) (members and the parent are `Checkbox.Root` parts) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

CheckboxGroup turns any number of [Checkbox](checkbox.md) parts into one multi-select value: a `string[]` listing the identities of the ticked boxes. The user sees an ordinary set of checkboxes (there is no roving focus and no arrow-key navigation; each box is its own tab stop); ticking a box adds its identity to the group value, unticking removes it. Optionally one box is declared the **parent** (`Checkbox.Root parent`): given the full list of member identities (`allValues`), the group derives the parent's checked ("all") / unchecked ("none") / indeterminate ("some") rendering from its value, and a click on the parent selects all members, then none, then — if the group was in a mixed state when the cycle began — restores that mixed selection.

The group renders a single `<div role="group">` and no inputs of its own; form participation happens through the members' hidden inputs (one `name=value` entry per ticked member). Within a named `Field.Root` the group, not the individual boxes, is the field's control: it registers the whole array as the field value, derives `filled`/`dirty` from it, runs the field's `validate` on the array, takes the `Field.Label` as `aria-labelledby` (suppressing the label's `for`), and exposes a *representative* member for focus when the form blocks submission. Each member keeps its own accessible name (a wrapping `<label>` or a `Field.Item` with `Field.Label`).

Nothing builds on CheckboxGroup; it builds on Checkbox, whose requirements (CHK-…) govern every member's own DOM, keyboard and pointer behavior.

## 2. Anatomy

```
<CheckboxGroup>                    required, 1          default element: div (role="group"; no hidden inputs)
  <Checkbox.Root value="…">        optional, 0..n       member (identity = value ?? effective name)
  <Checkbox.Root parent>           optional, 0..n       parent (derived only when the group has allValues)
  <Checkbox.Root>                  optional, 0..n       detached (no identity; keeps its own state)
```

Members need not be direct children: a `Checkbox.Root` anywhere in the group's subtree (including through portals, `Field.Item`, `Fieldset.Root`, native `<label>`s) belongs to the nearest enclosing group.

- **CKG-STR-01** — MUST. `CheckboxGroup` MUST render exactly one element (default `<div>`) and MUST NOT render hidden inputs, labels or any other element of its own.
  Source: `checkbox-group/CheckboxGroup.tsx:161-178`. Test: [L2] `CheckboxGroup.test.tsx` conformance suite "Base UI component API"; "is forwarded to the root element".
- **CKG-STR-02** — MUST. Every `Checkbox.Root` rendered in the group's subtree, at any depth and across portals, MUST take its role from the nearest enclosing `CheckboxGroup` per [CHK-STR-04](checkbox.md): members (identity, not parent) read and write the group value; the parent derives from it; detached boxes ignore it. A nested `CheckboxGroup` shadows the outer one for its own subtree.
  Source: `checkbox-group/CheckboxGroupContext.ts:26-32`, `checkbox-group/CheckboxGroup.tsx:148-178`, `checkbox/root/CheckboxRoot.tsx:87-94`. Test: [L2] `CheckboxGroup.test.tsx` "includes a context-portaled checkbox without native form association", "should control the value"; docs demo `checkbox-group/demos/nested` (nested groups).
  Port note 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23): "includes a context-portaled checkbox without native form association" render a portal, which the port has from M4; it stays open to M4 by ID, apart from this row's Form dependency.
- **CKG-STR-03** — MUST. Parent-checkbox behavior (derived state, select-all/none cycle, `aria-controls`, disabled-aware selection) MUST be active only when the group has an `allValues` prop; without it a `parent` box is inert ([CHK-EDGE-11](checkbox.md)).
  Source: `checkbox-group/CheckboxGroup.tsx:81-85`, `checkbox/root/CheckboxRoot.tsx:88-89`, `checkbox-group/useCheckboxGroupParent.ts:11`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes"; `CheckboxGroup.test.tsx` "keeps ids unique without allValues in a shared Field.Root (nativeButton=%s)" (no parent behavior asserted; members function).
- **CKG-STR-04** — MUST. The group MAY be composed with `Field.Root` (as the field's control), with `Fieldset.Root` through element substitution (`<Fieldset.Root render={<CheckboxGroup />}>`, the legend then labels the group), and members MAY each be wrapped in `Field.Item` with their own `Field.Label`/`Field.Description`; all four combinations MUST work together.
  Source: `checkbox-group/CheckboxGroup.tsx:47-57,89,166-171`, `fieldset/root/FieldsetRoot.tsx:34-44`, `field/item/FieldItem.tsx:43-47`. Test: [L2] `CheckboxGroup.test.tsx` "implicit association", "explicit association", "links the group and individual checkboxes"; docs `components/checkbox-group/page.mdx` §Form integration, `handbook/forms/page.mdx` §Labeling control groups. Fieldset substitution: none (docs only).

## 3. Public API

### 3.1 `CheckboxGroup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `string[]` | — (uncontrolled) | Identities of the ticked members. Supplying it (non-`undefined`) on first render makes the value consumer-owned. | CKG-API-01 |
| `defaultValue` | `string[]` | `[]` | Initial value when the value is component-owned. `null`/`undefined` are treated as `[]`. | CKG-API-02 |
| `onValueChange` | `(value: string[], eventDetails: CheckboxGroup.ChangeEventDetails) => void` | — | Called when a member or the parent is toggled, before the value changes. | CKG-API-03 |
| `allValues` | `string[]` | — | Identities of all members; enables the parent checkbox. | CKG-API-04 |
| `disabled` | `boolean` | `false` | Disables every member (OR-ed with each member's own and `Field.Root`'s disabled). | CKG-API-05 |
| `id` | `string` | — | Forwarded verbatim to the root element (no generated id is written to the element). | CKG-API-06 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | CKG-API-07 |
| `aria-labelledby` | `string` | — | Overrides the `Field.Label`-derived value (§5). | CKG-ARIA-02 |
| `aria-describedby` | `string` | — | Merged with Field description/error ids (§5). | CKG-ARIA-04 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element. | CKG-API-08 |

- **CKG-API-01** — MUST. When `value` is not `undefined` on the first render, the group's value MUST equal `value` on every render and MUST NOT be changed internally by user interaction (members and parent only request changes through `onValueChange`).
  Source: `checkbox-group/CheckboxGroup.tsx:62-67,69-79`, `packages/utils/src/useControlled.ts:41-46,83-90`; see [UTL-STA-01](../architecture/shared-utilities.md). Test: [L2] `CheckboxGroup.test.tsx` "should control the value", "does not update the group when onValueChange cancels the event" (uncontrolled counterpart).
- **CKG-API-02** — MUST. When `value` is `undefined` on the first render, the initial value MUST be `defaultValue`, with `undefined` and `null` both yielding an empty array; each group instance MUST get its own independent empty value.
  Source: `checkbox-group/CheckboxGroup.tsx:60,62-67`, `packages/utils/src/empty.ts:6`. Test: [L2] `CheckboxGroup.test.tsx` "should set the initial value", "should treat an omitted defaultValue as an empty array", "treats null as an empty array", "keeps omitted defaults isolated between groups in Strict Mode".
- **CKG-API-03** — MUST. `onValueChange` MUST receive the proposed next value array as its first argument and a change-event-details object (§3.3) as its second. Full firing rules are in §7.
  Source: `checkbox-group/CheckboxGroup.tsx:69-79,205-206`. Test: [L2] `CheckboxGroup.test.tsx` "should be called when the value changes".
- **CKG-API-04** — MUST. `allValues`, when supplied, MUST be the list used for the parent's derived state, the select-all target, and the `aria-controls` order (CKG-STA-06, CKG-STA-07, CKG-ARIA-06). When absent, no parent behavior exists (CKG-STR-03).
  Source: `checkbox-group/CheckboxGroup.tsx:35,81-85,149`, `checkbox-group/useCheckboxGroupParent.ts:11,24-25,51-53,58-66`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes", "should apply space-separated aria-controls attribute with child names".
- **CKG-API-05** — MUST. `disabled` defaults to `false`. The group's effective disabled MUST be `fieldDisabled || disabled`; every member and parent MUST be disabled while it is `true`, regardless of the member's own `disabled` prop (including `disabled={false}`).
  Source: `checkbox-group/CheckboxGroup.tsx:38,59,154`, `checkbox/root/CheckboxRoot.tsx:91-92`; see [FFI-STA-11](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "disables all checkboxes when `true`", "does not disable all checkboxes when `false`", "takes precedence over individual checkboxes".
- **CKG-API-06** — MUST. The `id` prop MUST be applied verbatim to the root element; when absent, the root element MUST have no `id` attribute (the generated id used for field registration is not written to the DOM).
  Source: `checkbox-group/CheckboxGroup.tsx:91,166`. Test: [L2] `CheckboxGroup.test.tsx` "is forwarded to the root element"; "keeps checkbox ids unique when the group shares one Field.Root (nativeButton=%s)" (id count excludes the group).
  Port note 2026-10-23 (M2 track 6, design point G2, settled by the orchestrator 2026-10-23: no base change; RadioGroup has the same need, RDG-API-10): the registration id is the consumer's `id`, else one drawn from the part-id generator in render order (ADR 02 decision 5); the drawn id is never written to the DOM.
- **CKG-API-07** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md); the state object they receive is the one below. None of these three is forwarded as a plain attribute.
  Source: `checkbox-group/CheckboxGroup.tsx:34-45,161-162`. Test: [L2] `CheckboxGroup.test.tsx` conformance suite "Base UI component API".
- **CKG-API-08** — MUST. Every other prop MUST be forwarded to the root element (merged after the built-in attributes, so a consumer value for the same attribute wins — see CKG-DOM-05). The following are consumed and MUST NOT be forwarded: `allValues`, `defaultValue`, `disabled`, `onValueChange`, `value`, `className`, `style`, `render`.
  Source: `checkbox-group/CheckboxGroup.tsx:34-45,170`. Test: [L2] `CheckboxGroup.test.tsx` "links the group and individual checkboxes" (`aria-describedby` forwarded and merged).

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes. It is the enclosing field's state (all `false`/`null` outside a Field) with the group's `disabled` layered on top.

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (CKG-API-05). |
| `touched` | `boolean` | Field: a member has been blurred at least once. |
| `dirty` | `boolean` | Field: value differs (element-wise) from the value captured at first registration. |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated or while the field is disabled. |
| `filled` | `boolean` | Field: the group value is non-empty. |
| `focused` | `boolean` | Field: a member owns focus. |

- **CKG-STA-01** — MUST. The state object MUST contain exactly the six fields above, with `disabled` taken from the effective value.
  Source: `checkbox-group/CheckboxGroup.tsx:143-146,181-186`. Test: none (shape documented in `components/checkbox-group/types.md`).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effective disabled | CKG-DOM-02 |
| `data-touched` | `""` while field `touched` (Field only) | CKG-DOM-03 |
| `data-dirty` | `""` while field `dirty` (Field only) | CKG-DOM-03 |
| `data-filled` | `""` while field `filled` (Field only) | CKG-DOM-03 |
| `data-focused` | `""` while field `focused` (Field only) | CKG-DOM-03 |
| `data-valid` | `""` while field `valid === true` (Field only) | CKG-DOM-03 |
| `data-invalid` | `""` while field `valid === false` (Field only) | CKG-DOM-03 |

Only `data-disabled` appears in the API outline and in `CheckboxGroupDataAttributes`; the field attributes follow from the state object and are asserted by tests (see §16).

#### CSS variables

None.

### 3.2 Shared types

- `CheckboxGroup.ChangeEventReason` = `'none'` — the only literal. Source: `checkbox-group/CheckboxGroup.tsx:218`, `internals/reason-parts.ts:1`.
- `CheckboxGroup.ChangeEventDetails` = the common change-event-details shape from [EVD](../architecture/event-details-and-reasons.md) ([EVD-API-01](../architecture/event-details-and-reasons.md)) specialised to reason `'none'`. It is the very object created by the toggled `Checkbox.Root` (CKG-EVT-03). Source: `checkbox-group/CheckboxGroup.tsx:219-220`.
- `CheckboxGroup.State` — the state object in §3.1. `CheckboxGroup.Props` — the prop table above. Source: `checkbox-group/CheckboxGroup.tsx:181-216,222-227`.
- Exported constant namespace `CheckboxGroupDataAttributes` = `{ disabled: 'data-disabled' }`. Source: `checkbox-group/CheckboxGroupDataAttributes.ts`, `checkbox-group/index.ts:4`.

## 4. State model

The group owns one piece of state, the `value` array, plus two private pieces used only by the parent algorithm: a **snapshot** of the value as of the last member-driven change, and a **cycle status** (`'mixed' | 'on' | 'off'`). Members and the parent derive their rendering from `value`; `Field.Root` state (`filled`, `dirty`, `touched`, `focused`, `valid`) is written by the group at defined moments.

**Ownership.** `value`/`defaultValue` is the controlled/uncontrolled pair, decided once on the first render. Members and the parent never write `value` directly: every change goes through `onValueChange` and, if not canceled, is committed only in component-owned mode.

**Derived values** (recomputed every render):

- member `checked` = `value.includes(identity)`
- parent `checked` = `value.length === allValues.length`; parent `indeterminate` = `value.length !== allValues.length && value.length > 0`
- `filled` (field) = `value.length > 0`; `dirty` (field) = `!areArraysEqual(value, initialValue ?? [])`
- `aria-controls` (parent) = registered member ids ordered by `allValues`

**Parent toggle algorithm** (CKG-STA-07) in terms of `value` (current), `snapshot`, `status`, `allValues`, and a per-identity `disabled` map reported by the members:

```
none  := allValues where disabled[v] && snapshot includes v        // disabled-and-checked boxes cannot be unchecked
all   := allValues where !disabled[v] || snapshot includes v       // disabled-and-unchecked boxes cannot be checked
if snapshot.length == all.length or snapshot.length == 0:          // the snapshot is not a genuine mixed set
    propose (value.length == all.length) ? none : all              // status untouched
else:                                                               // genuine mixed snapshot: three-step cycle
    'mixed' → propose all,      next status 'on'
    'on'    → propose none,     next status 'off'
    'off'   → propose snapshot, next status 'mixed'
    if the proposal is not canceled: status := next status
```

A committed **member** toggle sets `snapshot := the new value` and `status := 'mixed'`. The parent's own toggles do not update the snapshot.

- **CKG-STA-02** — MUST. Ownership of `value` MUST be fixed on the first render: consumer-owned if `value !== undefined`, otherwise component-owned. Later changes between `undefined` and an array MUST NOT change the mode (a dev error is logged instead, CKG-EDGE-01).
  Source: `packages/utils/src/useControlled.ts:41,46`; see [UTL-STA-01](../architecture/shared-utilities.md). Test: none.
- **CKG-STA-03** — MUST. In component-owned mode, a non-canceled member toggle MUST commit a **new** array equal to the previous value with the member's identity appended at the end (when checking) or with that identity removed (when unchecking), preserving the order of the other entries; a non-canceled parent toggle MUST commit the array proposed by CKG-STA-07.
  Source: `checkbox/root/CheckboxRoot.tsx:239-245`, `checkbox-group/useCheckboxGroupParent.ts:104-118`, `checkbox-group/CheckboxGroup.tsx:69-79`. Test: [L1] `CheckboxGroup.test.tsx` "should be called when the value changes" (`['red']`, `['red','green']`, `['red','green','blue']`), "should treat an omitted defaultValue as an empty array" (`['green']` after unchecking `red`); "prop: validationMode=onSubmit" (`['two','one']`, `['one','three']`).
- **CKG-STA-04** — MUST. In consumer-owned mode, a toggle MUST NOT change the rendered value unless the consumer passes the new array back; when `value` later becomes `undefined`, the rendered value MUST fall back to the initial default (an empty array when none was given).
  Source: `packages/utils/src/useControlled.ts:43-46,83-90`; see [UTL-STA-03](../architecture/shared-utilities.md). Test: [L2] `CheckboxGroup.test.tsx` "treats a controlled value that becomes undefined as an empty array", "does not update the group when onValueChange cancels the event".
- **CKG-STA-05** — MUST. Every member MUST render checked exactly when the current value contains its identity (string equality; an empty-string identity is valid), re-evaluated on every value change, including values that have no corresponding member.
  Source: `checkbox/root/CheckboxRoot.tsx:136-140`, `checkbox-group/useCheckboxGroupParent.ts:103`. Test: [L2] `CheckboxGroup.test.tsx` "should control the value", "supports an empty string item value", "omits selected unmounted checkboxes while retaining group state across remounts" (remounted member reads `true`).
- **CKG-STA-06** — MUST. With `allValues`, the parent MUST render checked when `value.length === allValues.length`, indeterminate when `value.length > 0` and `value.length !== allValues.length` (including a value longer than `allValues`), and unchecked otherwise (`value.length === 0` with a non-empty `allValues`); the comparison is by length, not by membership (§16).
  Source: `checkbox-group/useCheckboxGroupParent.ts:24-25,46-48`. Test: [L2] `useCheckboxGroupParent.test.tsx` "parent should be marked as mixed if some children are checked", "should correctly initialize the values array", "should update the values array when a child checkbox is clicked", "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks".
- **CKG-STA-07** — MUST. A parent toggle MUST propose the value computed by the algorithm above: from "all off" it proposes all selectable members (`all`); from "all on" it proposes the unremovable ones (`none`); from a genuine mixed snapshot it cycles all → none → the snapshot, advancing the cycle status only when the proposal is not canceled. Disabled members that are checked in the snapshot MUST stay checked; disabled members that are unchecked in the snapshot MUST stay unchecked.
  Source: `checkbox-group/useCheckboxGroupParent.ts:54-96`. Test: [L1] `useCheckboxGroupParent.test.tsx` "should control child checkboxes", "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks", "preserves initial state if mixed when parent is clicked", "handles unchecked disabled checkboxes", "handles checked disabled checkboxes", "does not advance the parent toggle cycle when the group cancels a parent change". Docs: CHANGELOG.md v1.6.0 "Fix parent group cancellation and indeterminate state".
- **CKG-STA-08** — MUST. The snapshot MUST be initialized to the group's value at first render and replaced by the committed value after every non-canceled member toggle (which also resets the cycle status to `'mixed'`); a canceled member toggle MUST leave both untouched, and a canceled parent toggle MUST leave the cycle status untouched. Consumer-driven changes of a controlled `value` and parent toggles MUST NOT update the snapshot.
  Source: `checkbox-group/useCheckboxGroupParent.ts:13,16,93-95,112-117`. Test: [L1] `useCheckboxGroupParent.test.tsx` "does not pollute the parent snapshot when the group cancels a child change", "does not advance the parent toggle cycle when the group cancels a parent change", "preserves initial state if mixed when parent is clicked".
- **CKG-STA-09** — MUST. Each member with an identity MUST report its effective disabled state to the group (after paint, on mount and on every change of that state or of its identity) and withdraw it on unmount; the parent algorithm reads the latest reported states.
  Source: `checkbox/root/CheckboxRoot.tsx:265-276`, `checkbox-group/useCheckboxGroupParent.ts:14,58-66`. Test: [L2] `useCheckboxGroupParent.test.tsx` "handles unchecked disabled checkboxes", "handles checked disabled checkboxes".
  Port note 2026-10-23 (M2 track 6, design point G4, settled by the orchestrator 2026-10-23; §16 Q8): a member updates its disabled entry together with its state, before its render, rather than after paint, so the parent reads the current states in the same task (`CheckboxGroupTests`); this drops the after-paint stale read that §14 allows.
- **CKG-STA-10** — MUST. An omitted `defaultValue` MUST NOT create shared mutable state between group instances: toggling members or the parent in one group MUST NOT affect another group that also omitted `defaultValue`. `attach` and `dispose` of the part's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op.
  Source: `checkbox-group/CheckboxGroup.tsx:60`, `packages/utils/src/empty.ts:3-6`; see [UTL-EDGE-18](../architecture/shared-utilities.md). Test: [L2] `CheckboxGroup.test.tsx` "keeps omitted defaults isolated between groups in Strict Mode"; twin contract (repeated `attach`/`dispose` of the members' twins).
- **CKG-STA-11** — MUST. Within a `Field.Root`, the field's `filled` state MUST be set to `value.length > 0` post-commit, pre-paint after mount and after every value change — including when no rendered member corresponds to the value's entries. Server: relaxed, see §9.
  Source: `checkbox-group/CheckboxGroup.tsx:125-127`; see [FFI-STA-08](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "[data-filled] follows the group value even without a matching rendered checkbox"; `CheckboxRoot.test.tsx` "adds [data-filled] attribute when any checkbox is filled when inside a group".
- **CKG-STA-12** — MUST. Within a `Field.Root`, after every value change (not on mount) the field's `dirty` state MUST be set to "the value differs element-wise (same length, `Object.is` per index) from the registered initial value, treating a non-array initial value as `[]`"; returning to the initial selection in the same order clears `dirty`.
  Source: `checkbox-group/CheckboxGroup.tsx:129-141`, `packages/utils/src/areArraysEqual.ts:9-27`; see [FFI-STA-05](../architecture/field-and-form-infrastructure.md), [UTL-EDGE-15](../architecture/shared-utilities.md). Test: [L2] `CheckboxGroup.test.tsx` "[data-dirty]".
- **CKG-STA-13** — MUST. The value MAY contain identities with no mounted member; such entries MUST be preserved across member mounts/unmounts and MUST count for `filled`, `dirty`, the parent's derived state and the validator, while being excluded from the form projection (CKG-FRM-03).
  Source: `checkbox-group/CheckboxGroup.tsx:103-121,126`. Test: [L2] `CheckboxGroup.test.tsx` "[data-filled] follows the group value even without a matching rendered checkbox", "omits selected unmounted checkboxes while retaining group state across remounts", "still runs custom validation after every checkbox in the group unmounts".

## 5. Rendered DOM and ARIA

### Root element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `id` | `id` prop verbatim; absent otherwise | CKG-DOM-01 | C# |
| `role` | `"group"` (consumer may override) | CKG-ARIA-01 | C# |
| `aria-labelledby` | consumer prop, else the `Field.Label` id of the group's labelable scope, else absent | CKG-ARIA-02 | C# |
| `aria-describedby` | consumer value + Field description/error ids | CKG-ARIA-04 | C# |
| `data-disabled` | `""` while effective disabled | CKG-DOM-02 | C# |
| `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid` | field state (Field only) | CKG-DOM-03 | C# |

- **CKG-DOM-01** — MUST. The root MUST be a `<div>` by default, carrying the `id` prop verbatim when given and no `id` otherwise.
  Source: `checkbox-group/CheckboxGroup.tsx:161-166`. Test: [L2] `CheckboxGroup.test.tsx` "is forwarded to the root element", conformance suite (`refInstanceof: HTMLDivElement`).
- **CKG-DOM-02** — MUST. `data-disabled=""` MUST be present exactly while the effective disabled state is `true`.
  Source: `checkbox-group/CheckboxGroup.tsx:143-146`, `internals/getStateAttributesProps.ts:24-25`. Test: none (asserted on members: "disables all checkboxes when `true`").
- **CKG-DOM-03** — MUST. `data-touched`, `data-dirty`, `data-filled`, `data-focused` MUST be present (value `""`) exactly while the corresponding field state is `true`; `data-valid` when `valid === true`, `data-invalid` when `valid === false`, neither when `null`; outside a `Field.Root` none of them appears.
  Source: `checkbox-group/CheckboxGroup.tsx:143-146,173`, `internals/field-constants/constants.ts:34-44`; see [FFI-DOM-01](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "[data-dirty]", "[data-filled] follows the group value even without a matching rendered checkbox".
- **CKG-DOM-04** — MUST. The group element MUST carry no `name`, `value`, `tabindex`, `aria-checked` or `aria-invalid`; form participation and validity reporting happen through the members.
  Source: `checkbox-group/CheckboxGroup.tsx:164-172` (no such attributes; `getDescriptionProps` only, not the validation props getter). Test: none.
- **CKG-DOM-05** — MUST. Attributes supplied by the consumer on `CheckboxGroup` MUST take precedence over the built-in `id`, `role` and `aria-labelledby` (so `<Fieldset.Root render={<CheckboxGroup />}>` labels the group by its legend through the fieldset's forwarded `aria-labelledby`), except `className`/`style` which merge per RND and event handlers which chain per RND.
  Source: `checkbox-group/CheckboxGroup.tsx:164-170` (merge order), `fieldset/root/FieldsetRoot.tsx:37-43`; see [RND-DOM-19](../architecture/rendering-and-composition.md). Test: none (docs `components/checkbox-group/page.mdx` §Labeling a checkbox group and §Form integration).
- **CKG-DOM-06** — MUST. Inside one `Field.Root`, all ids rendered by the group's subtree MUST be unique: with `nativeButton=false` each member renders its own generated hidden-input id and root id, with `nativeButton=true` each renders one root id, and the `Field.Label` keeps its id — for a parent plus two members that is 7 and 4 ids respectively, on the client and in server markup.
  Source: `checkbox/root/CheckboxRoot.tsx:96-106`, `checkbox-group/CheckboxGroupContext.ts:19-23`; see [FFI-ARIA-09](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "keeps checkbox ids unique when the group shares one Field.Root (nativeButton=%s)", "keeps checkbox ids unique in a shared Field.Root during SSR (nativeButton=%s)", "keeps ids unique without allValues in a shared Field.Root (nativeButton=%s)". Docs: CHANGELOG.md v1.8.0 "Fix stale and duplicated control IDs".

### ARIA

- **CKG-ARIA-01** — MUST. The root MUST have `role="group"` unless the consumer supplies a `role`.
  Source: `checkbox-group/CheckboxGroup.tsx:167`. Test: [L2] `CheckboxGroup.test.tsx` every `getByRole('group')` query.
- **CKG-ARIA-02** — MUST. The root's `aria-labelledby` MUST be the consumer's `aria-labelledby` when given; else the id of the `Field.Label` mounted in the group's own labelable scope (the enclosing `Field.Root`), when present; else absent. A legend supplied through `Fieldset.Root` substitution arrives as a consumer `aria-labelledby`.
  Source: `checkbox-group/CheckboxGroup.tsx:56,168,170`. Test: [L2] `CheckboxGroup.test.tsx` "labels the group rather than pointing Field.Label at one checkbox inside it" (`aria-labelledby` = label id after hydration). Docs: `components/checkbox-group/page.mdx` §Labeling a checkbox group; CHANGELOG.md v1.0.0-beta.0 "Enable submitting checkbox group value as one field".
- **CKG-ARIA-03** — MUST. The group MUST claim its labelable scope's control id as `null`, so that a `Field.Label` in the same scope renders no `for` attribute (it names the group through `aria-labelledby`, not a member). The claim is made post-commit, pre-paint, so server-rendered markup still carries the scope's pre-registration `for`; it MUST be removed after hydration. Server: relaxed, see §9.
  Source: `checkbox-group/CheckboxGroup.tsx:87-89`, `internals/labelable-provider/useLabelableId.ts:32-62`, `internals/labelable-provider/useLabel.ts:64-69`; see [FFI-ARIA-04](../architecture/field-and-form-infrastructure.md), [FFI-ARIA-09](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "labels the group rather than pointing Field.Label at one checkbox inside it".
- **CKG-ARIA-04** — MUST. Within a `Field.Root`, the root's `aria-describedby` MUST be the consumer's tokens followed by the field scope's `Field.Description`/`Field.Error` ids (deduplicated, space-separated; absent when empty). Members rendered inside a `Field.Item` MUST include the group scope's ids before their item's own ids ([CHK-ARIA-10](checkbox.md)).
  Source: `checkbox-group/CheckboxGroup.tsx:171`, `internals/labelable-provider/LabelableProvider.tsx:24,68-81`; see [FFI-ARIA-01](../architecture/field-and-form-infrastructure.md), [FFI-STR-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "links the group and individual checkboxes", "appends the id attribute of the error to aria-describedby of individual checkboxes". Docs: CHANGELOG.md v1.0.0-beta.7 "Fix `aria-describedby` on checkbox group".
- **CKG-ARIA-05** — MUST. While the field is invalid, every non-disabled member (and the parent) MUST carry `aria-invalid="true"` ([CHK-ARIA-11](checkbox.md)); the group element itself MUST NOT carry `aria-invalid` (it carries `data-invalid`).
  Source: `checkbox/root/CheckboxRoot.tsx:134,382`, `checkbox-group/CheckboxGroup.tsx:155,164-172`. Test: [L2] `CheckboxGroup.test.tsx` "prop: validationMode=onSubmit", "prop: validationMode=onChange", "does not leave a stale custom error when toggling checkboxes in a group", "focuses the first checkbox when the field receives an error from Form".
- **CKG-ARIA-06** — MUST. With `allValues`, a parent's `aria-controls` MUST list the rendered root ids of the mounted members whose identity is in `allValues`, ordered by `allValues` and, within one identity, by registration (mount) order, space-separated; it MUST be absent while the list is empty (including in server markup, since members register post-commit); members MUST be added when they mount and removed when they unmount; identities MUST be matched as plain strings (an identity such as `"constructor"` must not resolve to anything but a registered member). Server: relaxed, see §9.
  Source: `checkbox-group/useCheckboxGroupParent.ts:17-22,29-43,51-53`, `checkbox/root/CheckboxRoot.tsx:388-395`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should apply space-separated aria-controls attribute with child names", "keeps a custom child id in aria-controls", "keeps a rendered child id in aria-controls (nativeButton=%s)", "references the exposed child rather than its custom-id input without nativeButton", "does not read aria-controls ids off Object.prototype", "drops an unmounted child from aria-controls", "keeps both ids for checkboxes sharing a value and retains the survivor"; `CheckboxGroup.test.tsx` "keeps checkbox ids unique when the group shares one Field.Root (nativeButton=%s)", "points parent aria-controls at the children once they register (nativeButton=%s)".
  Port note 2026-10-23 (M2 track 6, design point G3): a member registers its id after the render. The pipeline calls the part's attribute finish after a substitute element's attributes merged, so the id registered is the one on the element (RND-DOM-32). The per-mode clause runs in `checkbox-group.spec.ts` "port: a parent box controls the members that registered, and drops one that unmounts" and "points parent aria-controls at the children once they register (nativeButton=%s)".
- **CKG-ARIA-07** — MUST. Each member and the parent MUST keep its own accessible name per [CHK-ARIA-06](checkbox.md)…[CHK-ARIA-09](checkbox.md) — a wrapping native `<label>`, a sibling `<label htmlFor>`, or a `Field.Item`'s `Field.Label` — and the group's `Field.Label` MUST NOT become a member's name.
  Source: `checkbox/root/CheckboxRoot.tsx:101-104,164-171`. Test: [L2] `CheckboxGroup.test.tsx` "gives each checkbox in a shared Field.Root its own accessible name", "implicit association", "explicit association", "associates Field.Label with a grouped Checkbox during SSR (nativeButton=$nativeButton, parent=$parent)".
  Port note 2026-10-23 (M2 track 6): the port follows this row as written: a box that shares the group's labelable scope claims no control id and takes no label from that scope. The pinned `CheckboxRoot.tsx:164-171` passes the scope's label id to such a box, so there a `Field.Label` beside the group also names every member that shares its scope. "gives each checkbox in a shared Field.Root its own accessible name" and the SSR titles run at L4 (`checkbox-group.spec.ts`; the native label lookup is the `form` module's, CHK-ARIA-07).

## 6. Interactions

The group adds no interaction of its own; everything below is the members' behavior ([checkbox](checkbox.md) §6) seen from the group's perspective.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | anywhere in the group | Native focus movement; every enabled member is its own tab stop. | CKG-KEY-01 |
| Arrow keys, <kbd>Home</kbd>, <kbd>End</kbd> | member focused | Not handled. | CKG-KEY-01 |
| <kbd>Space</kbd> | member/parent focused | Toggles that box ([CHK-KEY-01](checkbox.md)/[CHK-KEY-02](checkbox.md)) → group value change. | CKG-PTR-01, CKG-PTR-02 |
| <kbd>Enter</kbd> | member/parent focused | Never toggles; submits the owning form ([CHK-KEY-03](checkbox.md)/[CHK-KEY-04](checkbox.md)). | — |

- **CKG-KEY-01** — MUST. The group MUST NOT intercept any key: there is no roving tabindex, no arrow-key, <kbd>Home</kbd>/<kbd>End</kbd> or typeahead navigation, and the group element itself is not focusable; <kbd>Tab</kbd> moves between members natively.
  Source: `checkbox-group/CheckboxGroup.tsx:161-174` (no key handlers, no `tabIndex`, no composite root). Test: none (absence).

### 6.2 Pointer and touch

| Gesture | Behavior | Req |
|---|---|---|
| Click on a member (root, hidden input, or its label) | Member toggles per CHK §6.2; the group value gains/loses its identity. | CKG-PTR-01 |
| Click on the parent | Proposes all / none / snapshot per CKG-STA-07; members follow the committed value. | CKG-PTR-02 |
| Click on any box while the group is disabled | Nothing (members are disabled). | CKG-PTR-03 |

- **CKG-PTR-01** — MUST. A non-canceled toggle of a member MUST result in exactly one `onValueChange` with the array of CKG-STA-03 and, when committed, in the member rendering its new checked state and the parent re-deriving.
  Source: `checkbox/root/CheckboxRoot.tsx:231-245`, `checkbox-group/useCheckboxGroupParent.ts:104-118`. Test: [L2] `CheckboxGroup.test.tsx` "should control the value", "should be called when the value changes"; `useCheckboxGroupParent.test.tsx` "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks".
- **CKG-PTR-02** — MUST. A non-canceled toggle of the parent MUST result in exactly one `onValueChange` with the array of CKG-STA-07 and, when committed, in every member rendering per CKG-STA-05.
  Source: `checkbox-group/useCheckboxGroupParent.ts:54-96`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes", "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks".
- **CKG-PTR-03** — MUST. While the group is disabled, every member and the parent MUST behave as a disabled checkbox ([CHK-PTR-08](checkbox.md), [CHK-KEY-09](checkbox.md)); no `onValueChange` MUST fire.
  Source: `checkbox-group/CheckboxGroup.tsx:59,154`, `checkbox/root/CheckboxRoot.tsx:91-92`. Test: [L2] `CheckboxGroup.test.tsx` "disables all checkboxes when `true`" (attribute only; interaction implied by CHK) (L4 for the CHK-KEY-09 keyboard clause).

### 6.3 Focus management

- **CKG-FOC-01** — MUST. The group element MUST NOT be focusable by default (no `tabindex`), and focus MUST NOT be moved by the group on value changes.
  Source: `checkbox-group/CheckboxGroup.tsx:161-174`. Test: none.
- **CKG-FOC-02** — MUST. When the form blocks submission or receives external errors for the group's field, the element focused MUST be the root of the group's **representative member**: the first member (registration order) whose hidden input is eligible ([FFI-FRM-23](../architecture/field-and-form-infrastructure.md)) and currently invalid, else the first eligible member; a parent box is never chosen (it registers no input); disabled, fieldset-disabled, unmounted and other-form members are skipped; a portaled member is eligible. If no member is eligible the group still blocks submission and focus moves to the next invalid field that has a control.
  Source: `checkbox-group/CheckboxGroup.tsx:92-101,123`, `field/root/useFieldValidation.ts:51-66,118-121`, `form/Form.tsx:41-69`; see [FFI-FOC-04](../architecture/field-and-form-infrastructure.md), [FFI-FOC-01](../architecture/field-and-form-infrastructure.md), [FFI-FOC-02](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "focuses the first checkbox when the field receives an error from Form", "focuses the invalid checkbox when a later checkbox in the group fails validation", "focuses the first child checkbox instead of a parent checkbox for Form errors", "skips a checkbox disabled by a fieldset when focusing Form errors", "focuses a remaining checkbox when Form errors unmount the first checkbox", "skips a disabled representative checkbox on a later focus attempt", "validates and focuses required portaled checkboxes within the form", "focuses a later invalid field when an inputless group is invalid without a control", "focuses a later invalid field when every checkbox in an invalid group is disabled". Docs: CHANGELOG.md v1.7.0 "Focus invalid checkbox".
  Port note 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23): "validates and focuses required portaled checkboxes within the form" render a portal, which the port has from M4; it stays open to M4 by ID, apart from this row's Form dependency.
- **CKG-FOC-03** — MUST. The field's `focused` state (and `data-focused` on the group and members) MUST follow the members' focus per [CHK-FOC-04](checkbox.md)/[CHK-FOC-05](checkbox.md); the group writes nothing to it.
  Source: `checkbox/root/CheckboxRoot.tsx:127,304-315`. Test: none in the group suite.

### 6.4 Dismissal

Not applicable — no popup or dismissable surface.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: string[], eventDetails) => void` | A member or parent toggle that was not canceled by the box's own `onCheckedChange`, before the value is committed | `'none'` | yes (`cancel()`) |
| `onClick`, `onFocus`, … | standard DOM handler props forwarded to the group element | per DOM (bubbling from members) | — | — |

- **CKG-EVT-01** — MUST. `onValueChange(nextValue, details)` MUST be called synchronously inside the toggled box's change handling, after that box's own `onCheckedChange` returned without canceling, and before the group's value (and the box's rendering) is updated.
  Source: `checkbox-group/CheckboxGroup.tsx:69-79`, `checkbox/root/CheckboxRoot.tsx:225-245`; see [EVD-EVT-02](../architecture/event-details-and-reasons.md). Test: [L2] `CheckboxGroup.test.tsx` "should be called when the value changes"; `useCheckboxGroupParent.test.tsx` "lets a child checkbox cancel a parent-enabled group change" (not called when the child canceled).
- **CKG-EVT-02** — MUST. `details.reason` MUST always be `'none'`; `details.event` MUST be the native `click` received by the toggled box's hidden input (carrying modifier state); `details.trigger` is `undefined`.
  Source: `checkbox/root/CheckboxRoot.tsx:223`, `checkbox-group/CheckboxGroup.tsx:218`; see [EVD-EVT-28](../architecture/event-details-and-reasons.md) (reason matrix row), [CHK-EVT-03](checkbox.md). Test: none.
- **CKG-EVT-03** — MUST. The details object passed to `onValueChange` MUST be the same object the toggled box passed to its own `onCheckedChange` (child first, then group).
  Source: `checkbox/root/CheckboxRoot.tsx:223-231,244`; see [EVD-EVT-04](../architecture/event-details-and-reasons.md). Test: none directly (cancel propagation tests imply it).
- **CKG-EVT-04** — MUST. If `onValueChange` calls `details.cancel()`, the value MUST NOT be committed, the toggled box MUST NOT change its rendered state (its hidden input is restored), no field side effects MUST occur, and the parent algorithm's snapshot and cycle status MUST stay unchanged.
  Source: `checkbox-group/CheckboxGroup.tsx:73-75`, `checkbox/root/CheckboxRoot.tsx:233-237`, `checkbox-group/useCheckboxGroupParent.ts:93-95,114-117`; see [EVD-EVT-05](../architecture/event-details-and-reasons.md), [EVD-EVT-08](../architecture/event-details-and-reasons.md). Test: [L2] `CheckboxGroup.test.tsx` "does not update the group when onValueChange cancels the event"; `useCheckboxGroupParent.test.tsx` "does not advance the parent toggle cycle when the group cancels a parent change", "does not pollute the parent snapshot when the group cancels a child change".
- **CKG-EVT-05** — MUST. `onValueChange` MUST be called exactly once per user toggle (never twice for one click in a parent-enabled uncontrolled group) and MUST NOT be called when the consumer changes `value`, on mount, or for toggles of detached boxes.
  Source: `checkbox/root/CheckboxRoot.tsx:231,239-245` (the two write paths are mutually exclusive on `isGroupedWithParent`), `checkbox-group/CheckboxGroup.tsx:69-79`. Test: [L2] `useCheckboxGroupParent.test.tsx` "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks" (call counts 1, 2, 3); `CheckboxGroup.test.tsx` "revalidates when the controlled value changes externally" (validation runs; no callback). Docs: CHANGELOG.md v1.0.0-beta.1 "Fix `onCheckedChange` not running when parent checkbox is present".
- **CKG-EVT-06** — MUST. A parent toggle MUST NOT invoke the members' `onCheckedChange`, and a member toggle MUST NOT invoke the parent's; each box's callback fires only for its own activation.
  Source: `checkbox/root/CheckboxRoot.tsx:211-246`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes" (`childCheckedChange` 0 calls, `parentCheckedChange` 2 calls), "parent should be marked as mixed if some children are checked".
- **CKG-EVT-07** — MUST. `details.allowPropagation()` has no effect for this component; `isPropagationAllowed` merely reflects whether it was called.
  Source: `internals/createBaseUIEventDetails.ts:144-152` (no consumer in CheckboxGroup). Test: none.

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md). This section states what the group contributes; members contribute their hidden inputs ([CHK-FRM-19](checkbox.md), [CHK-FRM-20](checkbox.md)).

**What the group registers with the field** (only when `Field.Root` has a `name` and the group is not disabled). Control handle = a live getter resolving to the representative member's root (CKG-FOC-02); registration id = the `id` prop or a generated id; raw value = the `value` array (what `validate(value, formValues)` receives and what the dirty baseline is captured from); form-values projection = `getFormValue()` (CKG-FRM-03); name = the field's name. The group also passes the field's validation scope to its members, whose hidden inputs populate that scope's validation registry keyed by identity; the field's native validity is read from the representative input ([FFI-FRM-24](../architecture/field-and-form-infrastructure.md)).

**Timing of field writes** (post-commit, pre-paint after the commit that reflects the new value):

1. `filled ← value.length > 0` (on mount and on every change).
2. On every value change (not on mount): clear the enclosing `Form`'s external error for the field name (only when the field has a name); `dirty ← !areArraysEqual(value, initialValue ?? [])`; notify validation of the new array (`validation.change(value)`).
3. Member blur: `touched ← true`; `focused ← false`; in `onBlur` mode the member commits the **group value** to validation.

- **CKG-FRM-01** — MUST. The group MUST register as the field's control only while the enclosing `Field.Root` has a `name` and the group is not disabled, and MUST unregister otherwise and on unmount. Unlike a standalone box ([CHK-FRM-18](checkbox.md)), the group never supplies a name of its own: a group in a nameless `Field.Root` has no registry entry (no submission validation, no entry in the values object, no error lookup), although its members still validate natively and submit through `FormData`.
  Source: `checkbox-group/CheckboxGroup.tsx:123`, `internals/field-register-control/useRegisterFieldControl.ts:21-45`; see [FFI-FRM-17](../architecture/field-and-form-infrastructure.md) (`CheckboxGroup additionally requires a field name`). Test: [L2] `CheckboxGroup.test.tsx` "prop: validationMode=onSubmit", "is validated as a group upon form submission" (named field); nameless: none.
  Port note 2026-10-23 (ADR 05 Amendments 2026-10-22, verification item ADR05-5): with a cascaded `EditContext` and `ValueExpression`, the identifier's field name names a nameless field, so the group registers there under that name (`CheckboxGroupFieldTests` "port: CheckboxGroup.ValueExpression notifies the cascaded EditContext and names a nameless field"). Without both, the row holds as written. A member's own `CheckedExpression` is inert inside a group, because the group's `ValueExpression` is the field's (ADR 05 Amendments 2026-10-22; design point G5, settled by the orchestrator 2026-10-23).
- **CKG-FRM-02** — MUST. The registered raw value MUST be the current `value` array: it is the first argument of the field's `validate`, the source of the dirty baseline (captured once, at first registration), and the value committed by the field's imperative `validate()`.
  Source: `checkbox-group/CheckboxGroup.tsx:123,140`, `internals/field-register-control/useFieldControlRegistration.ts:53-63,92-103`; see [FFI-STA-04](../architecture/field-and-form-infrastructure.md), [FFI-FRM-21](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "is validated as a group upon form submission" (`['fuji-apple','gala-apple']`), "projects selected enabled checkboxes while preserving the logical validation value" (`validate` receives `['apple','banana']`), "validates an initially empty group through the imperative action".
- **CKG-FRM-03** — MUST. The group's entry in `Form.Values` (the `onFormSubmit` argument and other fields' `formValues`) MUST be the current value filtered to identities whose registered hidden input is checked and eligible — not disabled (own, group, or ancestor `<fieldset disabled>`), and owned by the surrounding form or portaled without a `form` attribute — in value order; entries with no mounted member, members disabled at submit time, and members associated with another form are omitted. Outside a `Form` the raw value is used.
  Source: `checkbox-group/CheckboxGroup.tsx:103-121`, `field/root/useFieldValidation.ts:32-44`; see [FFI-FRM-29](../architecture/field-and-form-infrastructure.md), [FFI-FRM-23](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "projects selected enabled checkboxes while preserving the logical validation value", "omits selected unmounted checkboxes while retaining group state across remounts", "preserves the logical field-name value when Checkbox.Root has no value prop", "omits selected checkboxes associated with another form", "includes a context-portaled checkbox without native form association", "includes a group fully portaled outside the form element", "includes a portaled checkbox explicitly associated with the Form", "omits checkboxes disabled by a fieldset".
  Port note 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23): "includes a context-portaled checkbox without native form association", "includes a group fully portaled outside the form element", "includes a portaled checkbox explicitly associated with the Form" render a portal, which the port has from M4; they stay open to M4 by ID, apart from this row's Form dependency.
- **CKG-FRM-04** — MUST. Native `FormData` submission MUST contain one entry per checked, enabled member — `name` = the member's effective name (the field's name when inside a named `Field.Root`), value = the member's `value` prop (or `"on"` when only a name identifies it) — and no entry for the parent or for unchecked members; `uncheckedValue` is never submitted inside a group.
  Source: `checkbox/root/CheckboxRoot.tsx:197-201,258-260,400`; see [FFI-FRM-28](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "includes the checkbox group value in form submission" (`getAll('apple')` → two values), "excludes parent checkboxes from form submission".
  Port note 2026-10-23: re-tagged from L2, because both titles build `FormData` in a browser (ADR 06 §1, ADR 13 §3). They run on native forms in `checkbox-group.spec.ts`; the `Form` that Base UI renders around them takes no part in what they assert.
- **CKG-FRM-05** — MUST. `required` on individual members MUST be enforced per member: the field is `valueMissing` while any eligible required member is unchecked; checking a sibling MUST NOT clear it; disabling, unmounting or re-associating the representative member MUST move representation to the next eligible member. The error MUST clear only when every eligible required member is checked, in every validation mode.
  Source: `field/root/useFieldValidation.ts:46-66,104-116,225-242`; see [FFI-FRM-24](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "keeps a required error while another required checkbox in the group is unchecked", "ignores a disabled required checkbox when validating the group", "keeps validating the remaining required checkbox after a checked sibling unmounts", "validationMode=onChange keeps the error until every required checkbox is ticked", "validationMode=onBlur keeps the error until every required checkbox is ticked", "focuses the invalid checkbox when a later checkbox in the group fails validation". Docs: CHANGELOG.md v1.6.0 "Fix validation with multiple required checkboxes".
- **CKG-FRM-06** — MUST. A member's hidden input MUST be ignored for validation, projection and focus when it is disabled (any source), when it is natively owned by a different form, or when its `form` attribute names another form; a member portaled outside the `<form>` without a `form` attribute MUST remain eligible, and one portaled *into* another `<form>` MUST NOT be. A member whose `form` attribute changes MUST change eligibility accordingly.
  Source: `field/root/useFieldValidation.ts:32-44`; see [FFI-FRM-23](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "ignores required checkboxes associated with a different form", "stops validating a checkbox after it changes form owner", "validates and focuses required portaled checkboxes within the form", "ignores a checkbox portaled into another form without a form attribute", "omits checkboxes disabled by a fieldset".
  Port note 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23): "validates and focuses required portaled checkboxes within the form", "ignores a checkbox portaled into another form without a form attribute" render a portal, which the port has from M4; they stay open to M4 by ID, apart from this row's Form dependency.
- **CKG-FRM-07** — MUST. The field's `validate` MUST be invoked with the current value array exactly once per committed toggle when on-change validation applies (`onChange`, or `onSubmit` after the first submit); its verdict MUST be applied to every member (`aria-invalid`), and a passing verdict MUST clear a previous custom error without leaving a stale one. A parent toggle MUST validate with the full resulting array.
  Source: `checkbox-group/CheckboxGroup.tsx:129-141`, `field/root/useFieldValidation.ts:283-346,349-365`, `field/root/FieldRoot.tsx:88-92`; see [FFI-EVT-02](../architecture/field-and-form-infrastructure.md), [FFI-FRM-01](../architecture/field-and-form-infrastructure.md), [FFI-FRM-07](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "prop: validationMode=onChange" (calls 1, 2, 3 with `[]`, `['two']`, `['two','one']`), "prop: validationMode=onSubmit", "does not leave a stale custom error when toggling checkboxes in a group", "validates with the group value when toggling the parent checkbox". Docs: CHANGELOG.md v1.6.0 "Fix parent checkbox with custom `validate` fn".
- **CKG-FRM-08** — MUST. A change of a consumer-owned `value` that did not come from user interaction MUST trigger the same on-change validation as a toggle (one additional `validate` call with the new array) and the same `filled`/`dirty`/error-clearing writes.
  Source: `checkbox-group/CheckboxGroup.tsx:125-141`, `internals/useValueChanged.ts:10-16`; see [UTL-STA-36](../architecture/shared-utilities.md), [EVD-STA-07](../architecture/event-details-and-reasons.md). Test: [L2] `CheckboxGroup.test.tsx` "revalidates when the controlled value changes externally", "clears a custom error when an inputless group becomes valid after submission", "validates an inputless controlled group on change with validationMode=onChange".
- **CKG-FRM-09** — MUST. In validation mode `onBlur`, blurring any member MUST run a full validation of the **group value** (native state of the representative input, then `validate(value)`); toggles alone MUST NOT validate.
  Source: `checkbox/root/CheckboxRoot.tsx:316-318`; see [FFI-FRM-03](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "prop: validationMode=onBlur" (`validate` only on blur, with `[]`, `['three']`, `['three','one']`), "validationMode=onBlur keeps the error until every required checkbox is ticked".
- **CKG-FRM-10** — MUST. On every value change, any external `Form` error registered under the field's name MUST be cleared; when the field has no name nothing is cleared.
  Source: `checkbox-group/CheckboxGroup.tsx:130-132`, `form/Form.tsx:143-155`; see [FFI-EVT-03](../architecture/field-and-form-infrastructure.md), [FFI-FRM-26](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "clears a custom error when an inputless group becomes valid after submission" (custom error path); external-error clearing: none directly.
- **CKG-FRM-11** — MUST. A group whose eligible member inputs are all absent (none mounted, all disabled, or all other-form) MUST still run the field's custom validator on its logical value at every validation boundary — submit, imperative `validate()`, and on-change when change-validation is active — and MUST NOT run it on change in `onSubmit` (before the first submit) or `onBlur` mode; such a group blocks submission only through the validator (no native `required` applies).
  Source: `field/root/useFieldValidation.ts:225-242`, `checkbox-group/CheckboxGroup.tsx:129-141`; see [FFI-FRM-22](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "unblocks submission after every checkbox in the group unmounts", "still runs custom validation after every checkbox in the group unmounts", "clears a custom error when an inputless group becomes valid after submission", "does not validate on change with validationMode=%s after the final checkbox unmounts", "respects validationMode=%s when a controlled group starts without inputs", "validates an inputless controlled group on change with validationMode=onChange".
- **CKG-FRM-12** — MUST. `Field.Root`'s imperative `validate()` MUST validate the group's current value (including an empty array) with the current form values as the second argument.
  Source: `internals/field-register-control/useFieldControlRegistration.ts:53-63`; see [FFI-FRM-21](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "validates an initially empty group through the imperative action" (`validate([], { group: [] })`).
- **CKG-FRM-13** — MUST. A custom validity message installed on a member's hidden input by the field MUST be cleared from that input when the group later validates as valid, even if that member has become disabled (and therefore ineligible) in the meantime.
  Source: `field/root/useFieldValidation.ts:167-182,254,280-281`; see [FFI-FRM-09](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "clears custom validity from disabled registered inputs when the group becomes valid".
- **CKG-FRM-14** — MUST. An external `Form` error for the field's name MUST make the field invalid (`data-invalid` on the group; `aria-invalid` on every non-disabled member), MUST render in a `Field.Error` without `match`, MUST append the error's id to the members' `aria-describedby`, and MUST focus the representative member (CKG-FOC-02) when the error arrives after a passed submission.
  Source: `field/root/FieldRoot.tsx:94-97`, `form/Form.tsx:71-84`; see [FFI-FRM-25](../architecture/field-and-form-infrastructure.md), [FFI-FOC-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "focuses the first checkbox when the field receives an error from Form", "appends the id attribute of the error to aria-describedby of individual checkboxes" (L4 for the representative-focus clause).
- **CKG-FRM-15** — MUST. The field's `touched` state MUST become `true` on the blur of any member; the group writes nothing to it.
  Source: `checkbox/root/CheckboxRoot.tsx:307-314`. Test: none in the group suite.
- **CKG-FRM-16** — MUST. When every member unmounts, native `required` constraints MUST no longer block submission (no eligible input remains), while the group's registration, value, `filled`/`dirty` and custom validation persist (CKG-FRM-11); members that remount MUST render from the retained value.
  Source: `field/root/useFieldValidation.ts:108-116,228-232`, `checkbox-group/CheckboxGroup.tsx:103-121`. Test: [L2] `CheckboxGroup.test.tsx` "unblocks submission after every checkbox in the group unmounts", "omits selected unmounted checkboxes while retaining group state across remounts".
- **CKG-FRM-17** — MUST. When several members share one identity, their registrations MUST be kept current post-commit, pre-paint — including before an ancestor's own post-commit work submits the form — so that the projection reflects the members mounted at that moment. Server: relaxed, see §9.
  Source: `checkbox-group/CheckboxGroup.tsx:103-123`, `internals/field-register-control/useRegisterFieldControl.ts:18-38`; see [FFI-EDGE-11](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxGroup.test.tsx` "updates duplicate-value registrations before a parent layout effect submits".
- **CKG-FRM-18** — MUST. A member rendered via `Field.Item` MUST be labelled and described by that item's parts and still participate in the group exactly as a direct member (identity, projection, validation, representative focus).
  Source: `field/item/FieldItem.tsx:29-47`, `checkbox/root/CheckboxRoot.tsx:84,101-104`; see [FFI-STR-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "[data-dirty]", "implicit association", "explicit association", "includes the checkbox group value in form submission" (L4 for the projection and representative-focus clauses).
- **CKG-FRM-19** — MUST. A native form reset MUST NOT change the group's value, `dirty`, `touched`, `filled` or validity.
  Source: absence (no `reset` listener); see [FFI-FRM-30](../architecture/field-and-form-infrastructure.md). Test: none.

## 9. Lifecycle, mounting and animation

Not applicable — the group element is always mounted and has no transition status; member Indicators animate per [checkbox](checkbox.md) §9.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- CKG-STA-11 — FFI registration: the field's `filled` write lands after paint plus a round trip; observable only as latency of `data-filled`.
- CKG-ARIA-03 — FFI registration: the `null` control-id claim lands after paint plus a round trip, so the `Field.Label`'s `for` disappears one round trip after the interactive render.
- CKG-ARIA-06 — FFI registration: member registration for the parent's `aria-controls` lands after paint plus a round trip; observable only as latency of `aria-controls`.
- CKG-FRM-17 — FFI registration: duplicate-identity registrations are updated after paint plus a round trip; observable only as latency of the form-values projection.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` | [RND](../architecture/rendering-and-composition.md) | default tag `div`; props merged in order: `{ id: idProp, role: 'group', 'aria-labelledby': labelId }` → consumer props → labelable description props (getter); one DOM handle (consumer); state attribute mapping = field validity mapping (`valid` → `data-valid`/`data-invalid`), other fields default |
| `getStateAttributesProps` | RND | booleans → `data-<lowercase key>=""` |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `{ controlled: value, default: defaultValue ?? EMPTY_ARRAY, name: 'CheckboxGroup', state: 'value' }` |
| `useStableCallback` | UTL | wraps `setValue` (callback + commit) and `getFormValue`; the parent algorithm's `onValueChange`, `registerChildId` |
| `areArraysEqual` | UTL (UTL-EDGE-15) | dirty computation, default `Object.is` comparer |
| `EMPTY_ARRAY` | UTL (UTL-EDGE-18) | frozen default value and default `allValues` |
| `useBaseUiId` | UTL / RND | registration id when `id` prop absent (not rendered) |
| `useValueChanged` | UTL (UTL-STA-36) | watches `value`; runs error clearing, dirty, `validation.change(value)` |
| `useFieldRootContext`, `useRegisterFieldControl`, `useFormContext` | [FFI](../architecture/field-and-form-infrastructure.md) | registration `(representativeControlHandle, id, value, getFormValue, !!fieldName && !disabled, fieldName)`; `setFilled(value.length > 0)`; `setDirty(!areArraysEqual(value, initialValue ?? []))`; `validation.change(value)`; `isEligibleInput` + `validation.registeredInputs` for the projection; `validation.getInputControl` for the control handle; `elementRef` (the `<form>` element) to decide whether to project |
| `useLabelableContext`, `useLabelableId` | FFI | `useLabelableId({ id: null })` (claim `null` → label omits `for`); `labelId` → `aria-labelledby`; `getDescriptionProps` on the root |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | not created here — the member's details object is reused (reason `'none'`) |
| `CheckboxGroupContext` (provided) | this doc / [CHK](checkbox.md) | `{ allValues, value, setValue, parent: { getParentProps, getChildProps, registerChildId, disabledStatesRef }, disabled, validation, registerControlId }` consumed by every `Checkbox.Root` in the subtree |

## 12. Edge cases, invariants and dev warnings

All messages below are emitted only in non-production builds, through `console.error`, prefixed `Base UI: `, and deduplicated per message ([UTL-EDGE-11](../architecture/shared-utilities.md), [UTL-EDGE-12](../architecture/shared-utilities.md)).

- **CKG-EDGE-01** — SHOULD. If `value` switches between `undefined` and an array after the first render, the dev error of [UTL-EDGE-22](../architecture/shared-utilities.md) SHOULD be logged with `name: 'CheckboxGroup'`, `state: 'value'`; the rendered value then follows CKG-STA-04.
  Source: `checkbox-group/CheckboxGroup.tsx:65-66`, `packages/utils/src/useControlled.ts:48-64`. Test: [L2] `CheckboxGroup.test.tsx` "treats a controlled value that becomes undefined as an empty array" (console error silenced; value asserted).
- **CKG-EDGE-02** — SHOULD. If `defaultValue` changes (by serialized content) after the first render of a component-owned group, the dev error of [UTL-EDGE-23](../architecture/shared-utilities.md) SHOULD be logged with `name: 'CheckboxGroup'`, `state: 'value'`. The rendered value is unaffected.
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: none.
- **CKG-EDGE-03** — MUST. A `defaultValue` of `null` (an unsupported value a non-typed consumer may pass) MUST be treated as an empty array and MUST NOT throw.
  Source: `checkbox-group/CheckboxGroup.tsx:60`. Test: [L2] `CheckboxGroup.test.tsx` "treats null as an empty array".
- **CKG-EDGE-04** — MUST. Identities MUST be handled as opaque strings in every registry (child ids, disabled states): identities such as `"constructor"`, `"__proto__"` or `"toString"` MUST NOT be confused with inherited object members.
  Source: `checkbox-group/useCheckboxGroupParent.ts:14,17-22`. Test: [L1] `useCheckboxGroupParent.test.tsx` "does not read aria-controls ids off Object.prototype".
- **CKG-EDGE-05** — MUST. The empty string MUST be a valid identity: a member with `value=""` is checked when `value` contains `""` and toggles it like any other identity.
  Source: `checkbox/root/CheckboxRoot.tsx:94,138-139`. Test: [L2] `CheckboxGroup.test.tsx` "supports an empty string item value".
- **CKG-EDGE-06** — MUST. Groups MAY be nested; a box belongs to the nearest enclosing group only, and the outer group's value, parent and `allValues` MUST NOT be affected by the inner group's members. (Linking an inner group to an entry of the outer value is consumer logic, as in the "Nested parent checkbox" demo.)
  Source: `checkbox-group/CheckboxGroupContext.ts:26-32`. Test: none (docs demo `checkbox-group/demos/nested`).
- **CKG-EDGE-07** — MUST. No behavior depends on writing direction (RTL) or locale.
  Source: `checkbox-group/CheckboxGroup.tsx:1-23` (no direction or locale import). Test: none.
- **CKG-EDGE-08** — MUST. Server-rendered markup MUST contain the group with `role="group"` and no `aria-controls` on the parent; a `Field.Label` in the group's scope carries the scope's pre-registration `for` on the server and loses it after hydration (CKG-ARIA-03); all ids in the subtree MUST be unique on the server (CKG-DOM-06); members inside `Field.Item` MUST be associated with their labels in server markup.
  Source: `checkbox-group/CheckboxGroup.tsx:89`, `checkbox-group/useCheckboxGroupParent.ts:29-43`, `internals/labelable-provider/useLabelableId.ts:79-82`; see [FFI-FRM-34](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "labels the group rather than pointing Field.Label at one checkbox inside it", "keeps checkbox ids unique in a shared Field.Root during SSR (nativeButton=%s)", "associates Field.Label with a grouped Checkbox during SSR (nativeButton=$nativeButton, parent=$parent)", "points parent aria-controls at the children once they register (nativeButton=%s)".
- **CKG-EDGE-09** — MUST. A `parent` box in a group without `allValues` MUST NOT derive state, MUST NOT carry `aria-controls`, and MUST NOT change the group value when toggled ([CHK-EDGE-11](checkbox.md)); the group MUST still function for its members.
  Source: `checkbox/root/CheckboxRoot.tsx:88-89,109-115`. Test: [L2] `CheckboxGroup.test.tsx` "keeps ids unique without allValues in a shared Field.Root (nativeButton=%s)" (members only).
- **CKG-EDGE-10** — MUST. Because the parent derives from lengths (CKG-STA-06), value entries that are not in `allValues` count toward "all"; an implementation MUST reproduce this (a port that prefers membership semantics changes observable `aria-checked` values — see §16).
  Source: `checkbox-group/useCheckboxGroupParent.ts:24-25`. Test: none.
- **CKG-EDGE-11** — MUST. Changing `allValues` at runtime MUST re-derive the parent's state, `aria-controls` order and the next parent toggle from the new list; no snapshot or status reset occurs.
  Source: `checkbox-group/useCheckboxGroupParent.ts:24-25,45-53,98`. Test: none.

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND; `Field.Label` `nativeLabel` mismatch errors from FFI-EDGE-02; per-member checks from [checkbox](checkbox.md) §12.

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. New IDs are introduced only where no earlier requirement captured the assertion.

### `CheckboxGroup.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`) | CKG-DOM-07 |
| "is forwarded to the root element" | CKG-API-06, CKG-DOM-01 |
| "should control the value" | CKG-API-01, CKG-STA-05, CKG-PTR-01 |
| "supports an empty string item value" | CKG-EDGE-05, CKG-STA-05 |
| "treats a controlled value that becomes undefined as an empty array" | CKG-STA-04, CKG-EDGE-01 |
| "should be called when the value changes" | CKG-API-03, CKG-EVT-01, CKG-STA-03 |
| "should treat an omitted defaultValue as an empty array" | CKG-API-02, CKG-STA-03 |
| "does not update the group when onValueChange cancels the event" | CKG-EVT-04, CKG-STA-04 |
| "treats null as an empty array" | CKG-EDGE-03, CKG-API-02 |
| "should set the initial value" | CKG-API-02, CKG-STA-05 |
| "keeps omitted defaults isolated between groups in Strict Mode" | CKG-STA-10 |
| "disables all checkboxes when `true`" / "does not disable all checkboxes when `false`" / "takes precedence over individual checkboxes" | CKG-API-05, CKG-PTR-03 |
| "[data-dirty]" | CKG-STA-12, CKG-DOM-03, CKG-FRM-18 |
| "[data-filled] follows the group value even without a matching rendered checkbox" | CKG-STA-11, CKG-STA-13, CKG-DOM-03 |
| "keeps a required error while another required checkbox in the group is unchecked" / "ignores a disabled required checkbox when validating the group" / "keeps validating the remaining required checkbox after a checked sibling unmounts" / "validationMode=onChange keeps the error until every required checkbox is ticked" / "validationMode=onBlur keeps the error until every required checkbox is ticked" | CKG-FRM-05, CKG-FRM-06 |
| "does not leave a stale custom error when toggling checkboxes in a group" | CKG-FRM-07, CKG-ARIA-05 |
| "clears custom validity from disabled registered inputs when the group becomes valid" | CKG-FRM-13 |
| "prop: validationMode=onSubmit" | CKG-FRM-07, CKG-FRM-01, CKG-STA-03, CKG-ARIA-05 |
| "prop: validationMode=onChange" | CKG-FRM-07, CKG-ARIA-05 |
| "validates with the group value when toggling the parent checkbox" | CKG-FRM-07, CKG-STA-07 |
| "revalidates when the controlled value changes externally" | CKG-FRM-08, CKG-EVT-05 |
| "prop: validationMode=onBlur" | CKG-FRM-09 |
| "keeps checkbox ids unique when the group shares one Field.Root (nativeButton=%s)" (it.each, 2 variants: false, true) / "keeps checkbox ids unique in a shared Field.Root during SSR (nativeButton=%s)" (it.each, 2 variants: false, true) / "keeps ids unique without allValues in a shared Field.Root (nativeButton=%s)" (it.each, 2 variants: false, true) | CKG-DOM-06, CKG-ARIA-06, CKG-EDGE-08, CKG-EDGE-09 |
| "labels the group rather than pointing Field.Label at one checkbox inside it" | CKG-ARIA-02, CKG-ARIA-03, CKG-EDGE-08 |
| "gives each checkbox in a shared Field.Root its own accessible name" | CKG-ARIA-07 |
| "associates Field.Label with a grouped Checkbox during SSR (nativeButton=$nativeButton, parent=$parent)" (it.each, 4 variants) | CKG-ARIA-07, CKG-EDGE-08 |
| "points parent aria-controls at the children once they register (nativeButton=%s)" (it.each, 2 variants: false, true) | CKG-ARIA-06, CKG-EDGE-08 |
| "implicit association" / "explicit association" | CKG-ARIA-07, CKG-FRM-18, CKG-STR-04 |
| "links the group and individual checkboxes" | CKG-ARIA-04, CKG-API-08 |
| "projects selected enabled checkboxes while preserving the logical validation value" | CKG-FRM-02, CKG-FRM-03 |
| "omits selected unmounted checkboxes while retaining group state across remounts" | CKG-FRM-03, CKG-FRM-16, CKG-STA-13 |
| "preserves the logical field-name value when Checkbox.Root has no value prop" | CKG-FRM-03 |
| "updates duplicate-value registrations before a parent layout effect submits" | CKG-FRM-17 |
| "omits selected checkboxes associated with another form" / "includes a context-portaled checkbox without native form association" / "includes a group fully portaled outside the form element" / "includes a portaled checkbox explicitly associated with the Form" / "omits checkboxes disabled by a fieldset" | CKG-FRM-03, CKG-FRM-06 |
| "includes the checkbox group value in form submission" | CKG-FRM-04 |
| "is validated as a group upon form submission" | CKG-FRM-02, CKG-FRM-01 |
| "focuses the first checkbox when the field receives an error from Form" | CKG-FOC-02, CKG-FRM-14 |
| "focuses the invalid checkbox when a later checkbox in the group fails validation" | CKG-FOC-02, CKG-FRM-05 |
| "ignores required checkboxes associated with a different form" / "stops validating a checkbox after it changes form owner" / "ignores a checkbox portaled into another form without a form attribute" | CKG-FRM-06 |
| "validates and focuses required portaled checkboxes within the form" | CKG-FRM-06, CKG-FOC-02 |
| "skips a checkbox disabled by a fieldset when focusing Form errors" / "focuses a remaining checkbox when Form errors unmount the first checkbox" / "skips a disabled representative checkbox on a later focus attempt" / "focuses the first child checkbox instead of a parent checkbox for Form errors" | CKG-FOC-02 |
| "unblocks submission after every checkbox in the group unmounts" | CKG-FRM-16, CKG-FRM-11 |
| "still runs custom validation after every checkbox in the group unmounts" / "clears a custom error when an inputless group becomes valid after submission" / "does not validate on change with validationMode=%s after the final checkbox unmounts" (it.each, 2 variants: onSubmit, onBlur) / "respects validationMode=%s when a controlled group starts without inputs" (it.each, 2 variants: onSubmit, onBlur) / "validates an inputless controlled group on change with validationMode=onChange" | CKG-FRM-11, CKG-FRM-08 |
| "validates an initially empty group through the imperative action" | CKG-FRM-12 |
| "focuses a later invalid field when an inputless group is invalid without a control" / "focuses a later invalid field when every checkbox in an invalid group is disabled" | CKG-FOC-02 |
| "excludes parent checkboxes from form submission" | CKG-FRM-04, CKG-STA-06 |
| "appends the id attribute of the error to aria-describedby of individual checkboxes" | CKG-ARIA-04, CKG-FRM-14 |

### `useCheckboxGroupParent.test.tsx`

| Test | Requirements |
|---|---|
| "should control child checkboxes" | CKG-STA-07, CKG-PTR-02, CKG-EVT-06 |
| "parent should be marked as mixed if some children are checked" | CKG-STA-06, CKG-EVT-06 |
| "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks" | CKG-EVT-05, CKG-STA-03, CKG-STA-07 |
| "should correctly initialize the values array" / "should update the values array when a child checkbox is clicked" | CKG-STA-06 |
| "should apply space-separated aria-controls attribute with child names" / "keeps a custom child id in aria-controls" / "keeps a rendered child id in aria-controls (nativeButton=%s)" (it.each, 2 variants: false, true) / "references the exposed child rather than its custom-id input without nativeButton" / "drops an unmounted child from aria-controls" / "keeps both ids for checkboxes sharing a value and retains the survivor" | CKG-ARIA-06 |
| "does not read aria-controls ids off Object.prototype" | CKG-EDGE-04, CKG-ARIA-06 |
| "does not select a child without an identifying value" | CKG-STR-02 (detached role), CKG-STA-07 |
| "preserves initial state if mixed when parent is clicked" | CKG-STA-07, CKG-STA-08 |
| "lets a parent checkbox cancel a parent-enabled group change" / "lets a child checkbox cancel a parent-enabled group change" | CKG-EVT-01 (not called when the box canceled) |
| "does not advance the parent toggle cycle when the group cancels a parent change" / "does not pollute the parent snapshot when the group cancels a child change" | CKG-EVT-04, CKG-STA-08 |
| "handles unchecked disabled checkboxes" / "handles checked disabled checkboxes" | CKG-STA-07, CKG-STA-09 |

Group-related tests in `checkbox/root/CheckboxRoot.react17.test.tsx` ("assigns the label association once the fallback ids arrive", "wires parent aria-controls once the fallback ids are assigned") are mapped in [checkbox](checkbox.md) §13.

### Additional requirements introduced here

- **CKG-DOM-07** — MUST. `CheckboxGroup` MUST pass the shared conformance suite from RND ([RND-DOM-36](../architecture/rendering-and-composition.md) … [RND-DOM-48](../architecture/rendering-and-composition.md)) with the DOM handle resolving to an `HTMLDivElement`: custom attributes forwarded to the default and substituted element (element and function forms), `style` from both sources applied, substituted element may be wrapped, both DOM handles receive the element, `className` string and function merge with the substituted element's class.
  Source: `test/describeConformance.tsx:55-76`, `test/conformanceTests/propForwarding.tsx:22-101`, `test/conformanceTests/renderProp.tsx:40-160`, `test/conformanceTests/refForwarding.tsx:13-26`, `test/conformanceTests/className.tsx:14-25`. Test: [L2] `CheckboxGroup.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Two member write paths.** A member inside a parent-enabled group (`allValues` present) commits through the parent helper's `getChildProps().onCheckedChange` (which also maintains the snapshot/status); a member in a plain group commits through `groupContext.setValue` directly. Both produce the array of CKG-STA-03; a port can implement one path and maintain the snapshot/status whenever `allValues` exists. Port: see decisions/05-state-store-and-event-details.md §2 (request sequence); decisions/04-js-interop-boundary.md §1 (group and parent state are C#).
- **Snapshot and status are refs/state inside the parent helper.** `uncontrolledStateRef` is initialized once from the first-render value (not re-synced to a controlled `value`), `status` is component state. They are private; only their effect on the proposed array (CKG-STA-07/08) is observable. Port: see decisions/04-js-interop-boundary.md §1 (C#-owned state).
- **Disabled registry timing.** Members write `disabledStatesRef` in a passive effect (after paint); the parent reads it in a click handler. A port may update it synchronously. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4.
- **Stable callbacks.** `setValue`, `getFormValue`, the parent's `onValueChange` and `registerChildId` are identity-stable wrappers over the latest closure, so the context value only changes when `value`, `allValues`, `disabled`, `parent` or `validation` change; `registerChildId` re-wraps the registry object to re-render the parent without copying the map. No observable behavior beyond CKG-ARIA-06 ordering. Port: see decisions/05-state-store-and-event-details.md §1.
- **`getFormValue` reads the DOM.** The projection inspects each registered hidden input's `checked`, `:disabled` and `form` at call time (submit, other fields' validation). A port that keeps the eligibility state elsewhere must evaluate it at the same moments.
- **Live control handle.** `controlRef` is an object with a `current` getter resolving the representative input's control at focus time; the form stores the handle, not the element, so the target can change between submissions (CKG-FOC-02 "later focus attempt").
- **Labelable `null` claim in a layout effect.** `useLabelableId({ id: null })` registers post-commit, so SSR output carries `for` on the label until hydration (CKG-ARIA-03). A port with eager registration can omit `for` on the server too; the test only requires the post-hydration state. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table row "FFI registration".
- **`useControlled` fallback.** When a controlled `value` becomes `undefined`, the value returned is the initial default (`EMPTY_ARRAY`), not the last committed one (UTL-STA-03); the dev error (CKG-EDGE-01) accompanies it. Port: see decisions/05-state-store-and-event-details.md §2 (`Controlled<T>`).
- **Fieldset substitution.** `<Fieldset.Root render={<CheckboxGroup />}>` works because the fieldset's merged attributes (`aria-labelledby` = legend id, `disabled`) are passed to `CheckboxGroup` as ordinary props: `disabled` is consumed as the group's prop and `aria-labelledby` overrides the Field-label value (RND-STR-03, RND-DOM-19). Note that the fieldset always passes an `aria-labelledby` key, even when `undefined` (no legend), which then suppresses a `Field.Label`-derived value. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-19: a null value emits no frame, so the suppression does not occur).
- **Ids.** As for Checkbox: React ≥ 18 generates ids eagerly; React 17 assigns them after mount, which is why `aria-controls` and label association appear "once the fallback ids arrive". Ports should generate ids eagerly. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| CKG-STR-01 | MUST | single div, no inputs of its own | 2 |
| CKG-STR-02 | MUST | membership by nearest enclosing group, any depth, portals | 2 |
| CKG-STR-03 | MUST | parent behavior only with `allValues` | 2 |
| CKG-STR-04 | MUST | composes with Field.Root, Fieldset substitution, Field.Item | 2 |
| CKG-API-01 | MUST | `value` defined on first render → consumer-owned | 3.1 |
| CKG-API-02 | MUST | `defaultValue` default `[]`; null → `[]`; per-instance | 3.1 |
| CKG-API-03 | MUST | `onValueChange(value, details)` signature | 3.1 |
| CKG-API-04 | MUST | `allValues` drives parent derivation, select-all, aria-controls order | 3.1 |
| CKG-API-05 | MUST | `disabled` default false; field OR prop; overrides members | 3.1 |
| CKG-API-06 | MUST | `id` forwarded verbatim; none otherwise | 3.1 |
| CKG-API-07 | MUST | `className`/`style`/`render` per RND | 3.1 |
| CKG-API-08 | MUST | other props forwarded; consumed list | 3.1 |
| CKG-STA-01 | MUST | state object has exactly six fields | 3.1 |
| CKG-STA-02 | MUST | ownership fixed on first render | 4 |
| CKG-STA-03 | MUST | uncontrolled commit: append / remove, new array | 4 |
| CKG-STA-04 | MUST | controlled follows prop; undefined → initial default | 4 |
| CKG-STA-05 | MUST | member checked = value contains identity | 4 |
| CKG-STA-06 | MUST | parent derived from lengths | 4 |
| CKG-STA-07 | MUST | parent toggle algorithm (all/none/snapshot; disabled rules) | 4 |
| CKG-STA-08 | MUST | snapshot/status maintenance and cancel behavior | 4 |
| CKG-STA-09 | MUST | members report disabled state to the parent registry | 4 |
| CKG-STA-10 | MUST | omitted defaults isolated between instances; idempotent twin `attach`/`dispose` | 4 |
| CKG-STA-11 | MUST | field `filled` = value non-empty, pre-paint | 4 |
| CKG-STA-12 | MUST | field `dirty` = element-wise difference from initial | 4 |
| CKG-STA-13 | MUST | value entries without members preserved and counted | 4 |
| CKG-DOM-01 | MUST | div with verbatim id | 5 |
| CKG-DOM-02 | MUST | `data-disabled` | 5 |
| CKG-DOM-03 | MUST | field data attributes within Field | 5 |
| CKG-DOM-04 | MUST | no name/value/tabindex/aria-checked/aria-invalid on group | 5 |
| CKG-DOM-05 | MUST | consumer attributes override built-in (Fieldset legend) | 5 |
| CKG-DOM-06 | MUST | unique ids in a shared Field.Root (client and SSR) | 5 |
| CKG-DOM-07 | MUST | passes RND conformance suite | 13 |
| CKG-ARIA-01 | MUST | `role="group"` | 5 |
| CKG-ARIA-02 | MUST | `aria-labelledby` = consumer ?? Field.Label id | 5 |
| CKG-ARIA-03 | MUST | Field.Label gets no `for` (null claim); removed post-hydration | 5 |
| CKG-ARIA-04 | MUST | `aria-describedby` composition on group; members include group ids | 5 |
| CKG-ARIA-05 | MUST | members carry `aria-invalid`; group does not | 5 |
| CKG-ARIA-06 | MUST | parent `aria-controls`: rendered member ids in allValues order; absent when empty | 5 |
| CKG-ARIA-07 | MUST | each member keeps its own accessible name | 5 |
| CKG-KEY-01 | MUST | no key handling; Tab native; group not focusable | 6.1 |
| CKG-PTR-01 | MUST | member toggle → one onValueChange, members/parent re-render | 6.2 |
| CKG-PTR-02 | MUST | parent toggle → one onValueChange, members follow | 6.2 |
| CKG-PTR-03 | MUST | disabled group: boxes inert | 6.2 |
| CKG-FOC-01 | MUST | group not focusable; never moves focus | 6.3 |
| CKG-FOC-02 | MUST | invalid-submit focus = representative member; parent never; skip ineligible | 6.3 |
| CKG-FOC-03 | MUST | field focused follows members | 6.3 |
| CKG-EVT-01 | MUST | onValueChange after box callback, before commit | 7 |
| CKG-EVT-02 | MUST | reason 'none'; event = input click; trigger undefined | 7 |
| CKG-EVT-03 | MUST | same details object as the box's callback | 7 |
| CKG-EVT-04 | MUST | cancel(): no commit, box restored, snapshot/status unchanged | 7 |
| CKG-EVT-05 | MUST | exactly once per toggle; not for prop/mount/detached | 7 |
| CKG-EVT-06 | MUST | parent toggle does not fire members' callbacks (and vice versa) | 7 |
| CKG-EVT-07 | MUST | allowPropagation no effect | 7 |
| CKG-FRM-01 | MUST | registers only with a field name and not disabled; no own name | 8 |
| CKG-FRM-02 | MUST | registered value = array (validate arg, baseline, imperative) | 8 |
| CKG-FRM-03 | MUST | Form.Values projection = checked AND eligible members, value order | 8 |
| CKG-FRM-04 | MUST | FormData: one entry per checked enabled member; parent never | 8 |
| CKG-FRM-05 | MUST | per-member `required`; representative moves | 8 |
| CKG-FRM-06 | MUST | eligibility: disabled/other-form excluded; portaled included | 8 |
| CKG-FRM-07 | MUST | validate(array) once per committed toggle in on-change modes; applied to all members | 8 |
| CKG-FRM-08 | MUST | external controlled change revalidates and writes field state | 8 |
| CKG-FRM-09 | MUST | onBlur: member blur commits group value | 8 |
| CKG-FRM-10 | MUST | clearErrors(fieldName) on change | 8 |
| CKG-FRM-11 | MUST | inputless group still runs validator at boundaries only | 8 |
| CKG-FRM-12 | MUST | imperative validate on current (even empty) value | 8 |
| CKG-FRM-13 | MUST | custom validity cleared from disabled inputs when valid | 8 |
| CKG-FRM-14 | MUST | form errors: invalid state, Field.Error, describedby, focus | 8 |
| CKG-FRM-15 | MUST | touched via member blur | 8 |
| CKG-FRM-16 | MUST | all members unmounted: native required lifted, state retained | 8 |
| CKG-FRM-17 | MUST | duplicate-identity registrations current before ancestor submit | 8 |
| CKG-FRM-18 | MUST | Field.Item members participate as direct members | 8 |
| CKG-FRM-19 | MUST | no form reset handling | 8 |
| CKG-EDGE-01 | SHOULD | controlled/uncontrolled dev error (`CheckboxGroup`, `value`) | 12 |
| CKG-EDGE-02 | SHOULD | defaultValue change dev error | 12 |
| CKG-EDGE-03 | MUST | `defaultValue={null}` tolerated | 12 |
| CKG-EDGE-04 | MUST | identities are opaque strings (no prototype lookup) | 12 |
| CKG-EDGE-05 | MUST | empty-string identity valid | 12 |
| CKG-EDGE-06 | MUST | nested groups: nearest wins | 12 |
| CKG-EDGE-07 | MUST | no RTL/locale dependence | 12 |
| CKG-EDGE-08 | MUST | SSR: no aria-controls; label `for` until hydration; unique ids | 12 |
| CKG-EDGE-09 | MUST | parent without allValues inert; members work | 12 |
| CKG-EDGE-10 | MUST | entries outside allValues count toward lengths | 12 |
| CKG-EDGE-11 | MUST | runtime `allValues` change re-derives | 12 |

## 16. Open questions

1. **Registration asymmetry.** `checkbox-group/CheckboxGroup.tsx:123` registers the group only when `Field.Root` has a `name` (`!!fieldName && !disabled`), whereas a standalone `Checkbox.Root` (`CheckboxRoot.tsx:151`) registers with its own `name` so a nameless field adopts it. A group in a nameless field therefore never enters the form registry (no `validate` on submit, no `onFormSubmit` entry, no representative focus) even though its members still submit via `FormData`. Documented as CKG-FRM-01; confirm whether the asymmetry is intended or a port should let the group adopt a shared member name.
   Resolved 2026-10-23 (M2 track 6): kept as CKG-FRM-01 states it; the one addition is ADR05-5's identifier name (CKG-FRM-01's note).
2. **Parent derivation by length** (`useCheckboxGroupParent.ts:24-25`): entries outside `allValues` count, and `allValues=[]` renders the parent checked. CKG-STA-06/CKG-EDGE-10 reproduce the source; a membership-based port would differ observably.
   Resolved 2026-10-23 (M2 track 6): reproduced (`CheckboxGroupParent`, `CheckboxGroupParentTests`).
3. **Field data attributes on the group.** The API outline and `CheckboxGroupDataAttributes` list only `data-disabled`, but the state object spreads the field state, and `CheckboxGroup.test.tsx` asserts `data-dirty`/`data-filled` on the group. Documented as CKG-DOM-03; the outline is incomplete.
4. **Fieldset always forwards `aria-labelledby`.** `fieldset/root/FieldsetRoot.tsx:37-43` passes `'aria-labelledby': legendId` even when no legend is mounted, which, through RND-DOM-19, replaces a `Field.Label`-derived `aria-labelledby` with "absent" under `<Fieldset.Root render={<CheckboxGroup />}>` without a `Fieldset.Legend`. Not tested; noted in §14. Resolved: decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-19 row) — in the port a `null` legend id emits no attribute, so the `Field.Label`-derived value survives; the React suppression is not reproduced.
5. **Unchecking an identity absent from the value** (`useCheckboxGroupParent.ts:109`): `splice(indexOf(childValue), 1)` with `indexOf === -1` would remove the last entry. Unreachable through the UI (a member can only be unchecked when its identity is present) but a port should guard it.
   Resolved 2026-10-23 (M2 track 6): guarded; a proposal that removes an absent identity returns the value's entries unchanged, as a new list (`CheckboxGroupParentTests`).
6. **Snapshot not re-synced to controlled changes** (`useCheckboxGroupParent.ts:13`): after the consumer changes a controlled `value` without a member toggle, the parent's "restore mixed" step proposes the stale snapshot. Documented as-is in CKG-STA-08; confirm intent.
   Resolved 2026-10-23 (M2 track 6): kept as CKG-STA-08 states it.
7. **`aria-controls` overrides the consumer's** on the parent (see [checkbox](checkbox.md) §16 item 2).
   Resolved 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23): mirrored, as checkbox §16 Q2 is resolved.
8. **Disabled-registry timing** is after paint (`CheckboxRoot.tsx:265-276`), unlike every other registration; a same-task parent click after a member becomes disabled could read a stale value. Not observable in tests.
   Resolved 2026-10-23 (M2 track 6, design point G4, settled by the orchestrator 2026-10-23): the entry is updated with the member's state, so no stale read arises (CKG-STA-09's note).
9. **No test for `Fieldset.Root render={<CheckboxGroup />}`** although it is the documented form-integration pattern (`components/checkbox-group/page.mdx` §Form integration, `handbook/forms/page.mdx`); CKG-STR-04/CKG-DOM-05 rely on source reading of the substitution mechanics.
   Resolved 2026-10-23 (M2 track 6): `CheckboxGroupFieldTests` "port: a fieldset rendered as the group labels it by its legend and disables its boxes".
