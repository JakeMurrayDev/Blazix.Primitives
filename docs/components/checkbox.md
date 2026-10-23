# Checkbox

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/checkbox/` |
| Tests | `packages/react/src/checkbox/**/*.test.tsx` (3 files) |
| Docs page | `docs/src/app/(docs)/react/components/checkbox/page.mdx` |
| Prefix | `CHK` |
| Status | approved |
| Revised | 2026-10-23 |
| Builds on | none |
| Derived by | [checkbox-group](checkbox-group.md) (composes `Checkbox.Root` as its members and parent) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Checkbox is the WAI-ARIA `checkbox` role: a control that is checked, unchecked, or — when the consumer says so — *indeterminate* ("mixed"). The user sees a box (`Checkbox.Root`) that optionally contains a mark (`Checkbox.Indicator`), which appears while the box is checked or indeterminate and disappears (after its exit animation) otherwise. Activating the box with a pointer, <kbd>Space</kbd>, or an associated `<label>` flips it between checked and unchecked. <kbd>Enter</kbd> never toggles a checkbox; instead it submits the owning form, as <kbd>Enter</kbd> does in a native text field.

The visible root is a `<span>` by default (so it can sit inside a `<label>`), paired with a visually hidden native `<input type="checkbox">` rendered as its next sibling. The hidden input is the single source of form participation, native constraint validation, and label activation: every user-initiated toggle is routed through a click on that input, and the component's `checked` state is updated from the input's resulting `checked` property. Checked state may be owned by the consumer (`checked`), by the component (`defaultChecked`), or — when the checkbox is a member of a [CheckboxGroup](checkbox-group.md) — by the group's `value` array. A `parent` checkbox inside a group with `allValues` derives its checked/indeterminate state from the group and selects all / none of the members when toggled.

Within `Field.Root` a standalone checkbox registers as the field's control and reports touched/dirty/filled/focused/validity through the field's state attributes; inside a group, the group is the field's control and the checkbox only contributes its hidden input to the group's validation registry. Switch is the two-state sibling of this component (no indeterminate state, no group); Radio is the single-selection sibling.

## 2. Anatomy

```
<Checkbox.Root>               required, 1          default element: span  (+ optional hidden <input type="hidden"> sibling,
                                                                             + hidden <input type="checkbox"> sibling)
  <Checkbox.Indicator>        optional, 0..n       default element: span  (in the DOM only while checked/indeterminate,
                                                                             during the exit phase, or with keepMounted)
```

- **CHK-STR-01** — MUST. `Checkbox.Root` MUST render, in document order: the root element (default `<span>`), then, only under the conditions of CHK-DOM-19, an `<input type="hidden">`, then always an `<input type="checkbox">`. All three are siblings; the inputs are never descendants of the root element.
  Source: `checkbox/root/CheckboxRoot.tsx:397-405`. Test: [L2] `CheckboxRoot.test.tsx` "should change its state when clicked" (hidden checkbox exists), "submits uncheckedValue to an external form when unchecked" (L4 for the `FormData` citation).
  Port note 2026-10-23: "submits uncheckedValue to an external form when unchecked" builds `FormData` in a browser, so its clause is L4 (ADR 06 §1, ADR 13 §3); the sibling structure is asserted at L2 by "should change its state when clicked" and the port title "the unchecked-value input sits between the root and the checkbox only while unchecked, named, not a parent and given a value".
- **CHK-STR-02** — MUST. `Checkbox.Indicator` MUST be rendered somewhere inside the subtree of a `Checkbox.Root`; rendering it elsewhere MUST throw the error in CHK-EDGE-01. Nothing limits the number of Indicators; each one mirrors the root's state.
  Source: `checkbox/root/CheckboxRootContext.ts:9-17`, `checkbox/indicator/CheckboxIndicator.tsx:26`. Test: [L2] `CheckboxIndicator.test.tsx` "throws a descriptive error when rendered outside <Checkbox.Root>".
- **CHK-STR-03** — MUST. `Checkbox.Indicator` MUST NOT attach handlers, ARIA attributes or an `id` of its own; it renders an element carrying the root's state attributes, its own transition attributes (§9) and forwarded attributes.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:60-65`. Test: [L2] `CheckboxRoot.test.tsx` "should place the style hooks on the root and the indicator".
- **CHK-STR-04** — MUST. A `Checkbox.Root` placed anywhere inside a `CheckboxGroup` (including through portals) takes one of three roles decided per render: **member** (it has an identity, CHK-API-06, and `parent` is `false`), **parent** (`parent` is `true`), or **detached** (no identity, `parent` false). Only members read and write the group's value; only a parent in a group that has `allValues` derives its state from the group (§4).
  Source: `checkbox/root/CheckboxRoot.tsx:87-94,108-115,136-149,239-245`. Test: [L2] `useCheckboxGroupParent.test.tsx` "does not select a child without an identifying value"; `CheckboxGroup.test.tsx` "should control the value".

If Indicator is omitted the root still functions fully; Indicator exists as a styling hook with an enter/exit lifecycle.

## 3. Public API

### 3.1 `Checkbox.Root`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `checked` | `boolean` | — (uncontrolled) | Whether the box is ticked. Supplying it (non-`undefined`) on first render makes the value consumer-owned — unless the box is a group member, in which case the group's value owns it. | CHK-API-01 |
| `defaultChecked` | `boolean` | `false` | Initial state when the value is component-owned. | CHK-API-02 |
| `onCheckedChange` | `(checked: boolean, eventDetails: Checkbox.Root.ChangeEventDetails) => void` | — | Called when the user toggles the box, before the state changes. | CHK-API-03 |
| `indeterminate` | `boolean` | `false` | Render the box as "mixed": `aria-checked="mixed"`, `data-indeterminate`, native `indeterminate` property. Does not change `checked`. | CHK-API-04 |
| `parent` | `boolean` | `false` | Declare this box the parent of the enclosing `CheckboxGroup`: derives checked/indeterminate from the group's value vs `allValues`, toggles all/none, carries `aria-controls`, never submits. | CHK-API-05 |
| `value` | `string` | — (identity falls back to effective name; browser submits `"on"`) | The box's identity inside a `CheckboxGroup` and the value submitted when checked. | CHK-API-06 |
| `uncheckedValue` | `string` | — (nothing submitted) | Value submitted when unchecked (standalone boxes only). | CHK-API-07 |
| `name` | `string` | — | Form field name. Overridden by the enclosing `Field.Root`'s `name` when that is set. | CHK-API-08 |
| `form` | `string` | — | `id` of the owning `<form>` when rendered outside it. | CHK-API-09 |
| `required` | `boolean` | `false` | Must be ticked to pass native constraint validation. | CHK-API-10 |
| `disabled` | `boolean` | `false` | Ignore user interaction. Combined (OR) with `Field.Root`, `Field.Item` and `CheckboxGroup` disabled states. | CHK-API-11 |
| `readOnly` | `boolean` | `false` | User cannot toggle; still focusable, still submits, <kbd>Enter</kbd> still submits the form. | CHK-API-12 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the hidden `<input type="checkbox">`. | CHK-API-13 |
| `id` | `string` | generated | `id` of the hidden input; when `nativeButton` is `true`, applied to the root element instead. An empty string falls back to the labelable scope's control id. | CHK-API-14 |
| `nativeButton` | `boolean` | `false` | Declare that the substituted root element is a native `<button>`. | CHK-API-15 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | CHK-API-16 |
| `aria-labelledby`, `aria-label` | `string` | — | Consumed to compute the root's accessible name (§5). `aria-label` is also forwarded to the root. | CHK-ARIA-06 |
| `aria-describedby` | `string` | — | Merged with Field description/error ids (§5). | CHK-ARIA-10 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the root element (not to the hidden input). | CHK-API-17 |

- **CHK-API-01** — MUST. For a standalone box (not a group member), when `checked` is not `undefined` on the first render, the checked state MUST equal `checked` on every render and MUST NOT be changed internally by user interaction. For a group member, `checked` MUST be ignored and the state is owned by the group (CHK-STA-07).
  Source: `checkbox/root/CheckboxRoot.tsx:117-118,136-144`, `packages/utils/src/useControlled.ts:41-46,83-90`. Test: [L2] `CheckboxRoot.test.tsx` "should update its state when changed from outside"; `CheckboxGroup.test.tsx` "should control the value".
- **CHK-API-02** — MUST. When the state is component-owned, the initial checked state MUST be `defaultChecked`, whose default is `false`.
  Source: `checkbox/root/CheckboxRoot.tsx:52,141`. Test: [L2] `CheckboxRoot.test.tsx` "should change its state when clicked" (starts `false`), "should place the style hooks on the root and the indicator" (`defaultChecked` → `data-checked`).
- **CHK-API-03** — MUST. `onCheckedChange` MUST receive the next checked value as its first argument and a change-event-details object (§3.3) as its second. Full firing rules are in §7.
  Source: `checkbox/root/CheckboxRoot.tsx:222-225,471-472`. Test: [L2] `CheckboxRoot.test.tsx` "should call onCheckedChange when clicked".
- **CHK-API-04** — MUST. `indeterminate` defaults to `false`. While the effective indeterminate value (CHK-STA-09) is `true`, the root MUST report `aria-checked="mixed"` and `data-indeterminate`, the hidden input's `indeterminate` property MUST be `true`, and the Indicator MUST be rendered; the prop MUST NOT alter the underlying `checked` state.
  Source: `checkbox/root/CheckboxRoot.tsx:57,147-149,173-177,299`, `checkbox/indicator/CheckboxIndicator.tsx:28`. Test: [L2] `CheckboxRoot.test.tsx` "should set the `aria-checked` attribute as \"mixed\"", "sets the native input state when indeterminate", "sets indeterminate style hooks on the root and indicator", "should not be overridden by `checked` prop"; `CheckboxIndicator.test.tsx` "should keep indicator mounted when indeterminate" (L4 for the hidden input's `indeterminate` property clause).
- **CHK-API-05** — MUST. `parent` defaults to `false`. When `true`: the hidden input MUST carry no `name` (so the box never submits), the box MUST NOT register its hidden input with any validation registry, MUST NOT render the `uncheckedValue` input, MUST carry `data-parent=""`, and, when the enclosing `CheckboxGroup` has `allValues`, MUST derive its checked/indeterminate state and toggle behavior from the group (CHK-STA-08, [CKG-STA-06](checkbox-group.md), [CKG-STA-07](checkbox-group.md)) and carry `aria-controls` (CHK-ARIA-12).
  Source: `checkbox/root/CheckboxRoot.tsx:61,88-89,108-115,163,201,303,400`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes"; `CheckboxGroup.test.tsx` "excludes parent checkboxes from form submission". Docs: `components/checkbox-group/page.mdx` §Parent checkbox.
- **CHK-API-06** — MUST. The box's **identity** MUST be `value` when `value` is defined (an empty string is a valid identity), else the effective name (CHK-API-08), else none. Inside a `CheckboxGroup` the identity is the string looked up in and written to the group's `value` array. The `value` prop MUST be placed on the hidden input only, per CHK-DOM-16; when `value` is `undefined` the input has no `value` attribute (the browser submits `"on"`).
  Source: `checkbox/root/CheckboxRoot.tsx:93-94,138-139,257-260`. Test: [L2] `CheckboxGroup.test.tsx` "supports an empty string item value", "preserves the logical field-name value when Checkbox.Root has no value prop" (identity from the field name); `CheckboxRoot.test.tsx` "should include the custom checkbox value in form submission, matching native checkbox behavior" (L4 for the `FormData` citation).
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-API-07** — MUST. `uncheckedValue` MUST be submitted (via the hidden `<input type="hidden">`, CHK-DOM-19) only while a standalone, non-parent box is unchecked and an effective name exists; when `undefined`, an unchecked box MUST submit nothing. Inside a `CheckboxGroup` the prop MUST have no effect.
  Source: `checkbox/root/CheckboxRoot.tsx:400-402`. Test: [L4] `CheckboxRoot.test.tsx` "should submit uncheckedValue when checkbox is unchecked and uncheckedValue is specified", "should submit custom uncheckedValue when checkbox is unchecked". Docs: CHANGELOG.md v1.0.0-rc.0 "Match native unchecked state in form submission".
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run on native forms in `checkbox.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-API-08** — MUST. The effective name MUST be the enclosing `Field.Root`'s effective name when that is defined, otherwise the `name` prop. It is placed on the hidden input(s) only (never on the root) and omitted for a parent box.
  Source: `checkbox/root/CheckboxRoot.tsx:93,201,401`. Test: [L2] `CheckboxRoot.test.tsx` "should receive name prop from Field.Root", "should set the name attribute only on the input".
- **CHK-API-09** — MUST. `form` MUST be forwarded as the `form` attribute of both hidden inputs and MUST NOT appear on the root element. The owning form resolved from it is also the form submitted by <kbd>Enter</kbd> (CHK-KEY-04).
  Source: `checkbox/root/CheckboxRoot.tsx:199,333,401`. Test: [L2] `CheckboxRoot.test.tsx` "submits to an external form when `form` is provided", "submits to an external form with Enter when `form` is provided" (L4 for the Enter-submission clause).
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-API-10** — MUST. `required` defaults to `false`. When `true`, the hidden input MUST carry the `required` attribute and the root MUST carry `aria-required="true"`.
  Source: `checkbox/root/CheckboxRoot.tsx:64,205,301`. Test: [L2] `CheckboxRoot.test.tsx` "sets the correct aria attributes", "triggers native HTML validation on submit".
  Port note 2026-10-23 (M2 track 6): "triggers native HTML validation on submit" asserts only its `Field.Error`, so it stays open to PR 1c; the hidden input's `required` reaching a blocked submission is asserted by the port title of CHK-FRM-08's note.
- **CHK-API-11** — MUST. The effective disabled state MUST be `fieldDisabled || fieldItemDisabled || groupDisabled || disabled` (each term `false` when that ancestor is absent). The default of the prop is `false`; a `disabled={false}` on the box MUST NOT override a disabled ancestor.
  Source: `checkbox/root/CheckboxRoot.tsx:54,91-92`; see [FFI-STA-11](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "should receive disabled prop from Field.Root"; `CheckboxGroup.test.tsx` "disables all checkboxes when `true`", "takes precedence over individual checkboxes".
- **CHK-API-12** — MUST. `readOnly` defaults to `false`. When `true`, user interaction MUST NOT change the checked state (§6), `aria-readonly="true"` MUST be set (§5), and <kbd>Enter</kbd> MUST still submit the form (CHK-KEY-06).
  Source: `checkbox/root/CheckboxRoot.tsx:62,217-220,300,364-366`. Test: [L2] `CheckboxRoot.test.tsx` "prop: readOnly" (all four tests), "submits the form with Enter when readOnly" (L4 for the Enter-submission clause).
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-API-13** — MUST. `inputRef` MUST be attached to the hidden `<input type="checkbox">` element. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden input stays internal and no public handle to it exists.
  Source: `checkbox/root/CheckboxRoot.tsx:163,206`. Test: none (the equivalent Switch test has no Checkbox counterpart; `useCheckboxGroupParent.test.tsx` "does not select a child without an identifying value" asserts the hidden input is the root's next sibling).
- **CHK-API-14** — MUST. When `nativeButton` is `false`, the `id` prop MUST be applied to the hidden checkbox input and the root element MUST receive an auto-generated id. When `nativeButton` is `true`, the `id` prop MUST be applied to the root element and the hidden input MUST have no `id`. An empty-string `id` MUST be treated as absent (the labelable scope's control id is used).
  Source: `checkbox/root/CheckboxRoot.tsx:103-106,204,297`; see [FFI-DOM-04](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "should toggle the checkbox when a explicitly linked <label> is clicked", "should associate `id` with the native button when `nativeButton=true`", "falls back to the Field control id when id is empty".
- **CHK-API-15** — MUST. `nativeButton` defaults to `false`. It selects native-button vs. non-native-button behavior from [UBT](../architecture/button-behavior.md) (keyboard activation, `tabindex`, `disabled` attribute vs `aria-disabled`, `type="button"`) and the `id`/label wiring in CHK-API-14 and §5.
  Source: `checkbox/root/CheckboxRoot.tsx:67,129-132`; see [UBT-API-03](../architecture/button-behavior.md). Test: [L2] `CheckboxRoot.test.tsx` "can render a native button" (L4 for the keyboard-activation clause).
- **CHK-API-16** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md); the state object they receive is the one in "State object" below. None of these three is forwarded as a plain attribute.
  Source: `checkbox/root/CheckboxRoot.tsx:49-70,292-293`. Test: [L2] `CheckboxRoot.test.tsx` conformance suite "Base UI component API" (see §13).
- **CHK-API-17** — MUST. Every other prop MUST be forwarded to the root element (merged after the built-in attributes, so a consumer value for the same attribute wins — see CHK-DOM-09). The following are consumed and MUST NOT be forwarded: `checked`, `defaultChecked`, `onCheckedChange`, `indeterminate`, `parent`, `value`, `uncheckedValue`, `name`, `form`, `required`, `disabled`, `readOnly`, `inputRef`, `id`, `nativeButton`, `aria-labelledby`, `className`, `style`, `render`. `onChange` and the native `value` attribute type are excluded from the accepted parameter type.
  Source: `checkbox/root/CheckboxRoot.tsx:49-70,378,431-434`. Test: [L2] `CheckboxRoot.test.tsx` "can override the built-in attributes", "should set the name attribute only on the input".

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes. It is the enclosing field's state (all `false`/`null` outside a Field) with five checkbox fields layered on top.

| Field | Type | Meaning |
|---|---|---|
| `checked` | `boolean` | Effective checked state (CHK-STA-07/08 when grouped). |
| `disabled` | `boolean` | Effective disabled (CHK-API-11). |
| `readOnly` | `boolean` | The `readOnly` prop. |
| `required` | `boolean` | The `required` prop. |
| `indeterminate` | `boolean` | Effective indeterminate (CHK-STA-09). |
| `touched` | `boolean` | Field: the control has been blurred at least once. |
| `dirty` | `boolean` | Field: value differs from the value captured at first registration. |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated or while the field is disabled. |
| `filled` | `boolean` | Field: standalone → equals `checked`; grouped → the group's value is non-empty. |
| `focused` | `boolean` | Field: a control of the field owns focus (never `true` while disabled). |

- **CHK-STA-01** — MUST. The state object MUST contain exactly the ten fields above, with `disabled` taken from the effective disabled value (CHK-API-11), not the field's own `disabled`, and `checked`/`indeterminate` taken from the effective values.
  Source: `checkbox/root/CheckboxRoot.tsx:278-288,408-429`. Test: [L2] `CheckboxIndicator.test.tsx` (test context shape, lines 8-19).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-checked` | `""` while effective `checked` is `true` and not indeterminate | CHK-DOM-02 |
| `data-unchecked` | `""` while effective `checked` is `false` and not indeterminate | CHK-DOM-02 |
| `data-indeterminate` | `""` while effective `indeterminate` is `true` | CHK-DOM-03 |
| `data-disabled` | `""` while effective disabled | CHK-DOM-04 |
| `data-readonly` | `""` while `readOnly` | CHK-DOM-04 |
| `data-required` | `""` while `required` | CHK-DOM-04 |
| `data-parent` | `""` while `parent` (root only; not in the public attribute namespace, see §16) | CHK-DOM-07 |
| `data-touched` | `""` while field `touched` (Field only) | CHK-DOM-05 |
| `data-dirty` | `""` while field `dirty` (Field only) | CHK-DOM-05 |
| `data-filled` | `""` while field `filled` (Field only) | CHK-DOM-05 |
| `data-focused` | `""` while field `focused` (Field only) | CHK-DOM-05 |
| `data-valid` | `""` while field `valid === true` (Field only) | CHK-DOM-06 |
| `data-invalid` | `""` while field `valid === false` (Field only) | CHK-DOM-06 |

#### CSS variables

None.

### 3.2 `Checkbox.Indicator`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while the box is neither checked nor indeterminate. | CHK-API-18 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the Indicator state object. | CHK-API-19 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the indicator element. | CHK-API-19 |

- **CHK-API-18** — MUST. `keepMounted` defaults to `false`. When `true` the indicator element MUST remain in the DOM regardless of the box's state; it MUST NOT change the transition-status sequence (§9).
  Source: `checkbox/indicator/CheckboxIndicator.tsx:24,58,67-69`; see [TRN-API-01](../architecture/transitions-and-mounting.md), [TRN-API-02](../architecture/transitions-and-mounting.md). Test: [L2] `CheckboxIndicator.test.tsx` "should keep indicator mounted when unchecked", "should keep indicator mounted when checked", "should keep indicator mounted when indeterminate".
- **CHK-API-19** — MUST. All Indicator props other than `keepMounted`, `className`, `style`, `render` MUST be forwarded to the indicator element; the three customization props MUST behave per RND using the Indicator state object.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:24,60-65`. Test: [L2] `CheckboxIndicator.test.tsx` conformance suite "Base UI component API"; `CheckboxRoot.test.tsx` "sets indeterminate style hooks on the root and indicator" (`data-testid` forwarded).

#### State object

The root's state object (same ten fields, same values at the same time) plus:

| Field | Type | Meaning |
|---|---|---|
| `transitionStatus` | `'starting' \| 'ending' \| undefined` | Enter/exit phase (§9). `'idle'` is never produced by this part. |

- **CHK-STA-02** — MUST. The Indicator state object MUST be exactly the root's state object with one additional field `transitionStatus`. Server: relaxed, see §9.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:34-37,74-79`. Test: [L2] `CheckboxRoot.test.tsx` "should place the style hooks on the root and the indicator"; docs demo `checkbox-group/demos/parent` (reads `state.indeterminate` in the Indicator `render` callback).

#### Data attributes

The root's mapping (`data-checked`, `data-unchecked`, `data-indeterminate`, `data-disabled`, `data-readonly`, `data-required`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid` — Req CHK-DOM-08; never `data-parent`) plus:

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-starting-style` | `""` while `transitionStatus === 'starting'` | CHK-ANI-03 | JS |
| `data-ending-style` | `""` while `transitionStatus === 'ending'` | CHK-ANI-04 | JS |

Port note 2026-10-23 (docs/conventions.md §3.4, ADR 04 §2): this table gains the `Writer` column §5 does not give the Indicator. The `transitions` module writes both attributes from attach; C# renders `data-starting-style` only on an element it creates open, as the handover value of that element (ADR 04 Amendments 2026-10-18), and mirrors the status in the state's `TransitionStatus` (§9, CHK-STA-02).

#### CSS variables

None.

### 3.3 Shared types

- `Checkbox.Root.ChangeEventReason` = `'none'` — the only literal. Source: `checkbox/root/CheckboxRoot.tsx:511`, `internals/reason-parts.ts:1`.
- `Checkbox.Root.ChangeEventDetails` = the common change-event-details shape from [EVD](../architecture/event-details-and-reasons.md) ([EVD-API-01](../architecture/event-details-and-reasons.md)) specialised to reason `'none'`: `{ reason: 'none'; event: Event; cancel(); allowPropagation(); isCanceled; isPropagationAllowed; trigger: Element | undefined }`. Source: `checkbox/root/CheckboxRoot.tsx:512-513`, `internals/createBaseUIEventDetails.ts:56-85,126-157`.
- `Checkbox.Root.State`, `Checkbox.Indicator.State` — the state objects in §3.1 / §3.2. `Checkbox.Indicator.State` extends `Checkbox.Root.State` with `transitionStatus`. Source: `checkbox/root/CheckboxRoot.tsx:408-429`, `checkbox/indicator/CheckboxIndicator.tsx:74-79`.
- `Checkbox.Root.Props`, `Checkbox.Indicator.Props` — the prop tables above.
- Exported constant namespaces `CheckboxRootDataAttributes` (12 names: checked, unchecked, indeterminate, disabled, readonly, required, valid, invalid, touched, dirty, filled, focused) and `CheckboxIndicatorDataAttributes` (the same 12 plus `startingStyle`, `endingStyle`). Neither lists `data-parent`. Source: `checkbox/root/CheckboxRootDataAttributes.ts`, `checkbox/indicator/CheckboxIndicatorDataAttributes.ts`, `checkbox/index.ts:6-7`.

## 4. State model

The component owns one piece of state: the underlying `checked` boolean. Everything else is a prop echo (`disabled`, `readOnly`, `required`, `indeterminate`), derived from the enclosing group, or field state owned by `Field.Root` and written to by the checkbox at defined moments.

**Ownership of `checked`** (decided per render, in this order):

1. **Group member** (inside a `CheckboxGroup`, identity defined, `parent` false): `checked = groupValue.includes(identity)`. `checked`/`defaultChecked` props are ignored.
2. **Parent in a group with `allValues`**: `checked = groupValue.length === allValues.length`; `indeterminate = 0 < groupValue.length < allValues.length || indeterminateProp` (lengths, not set membership — see §16).
3. **Otherwise** (standalone, detached member, or parent in a group without `allValues`): the `checked`/`defaultChecked` pair; ownership fixed at first render by `checked !== undefined`.

**Derived values.**

- effective disabled = `fieldDisabled || fieldItemDisabled || groupDisabled || disabled`
- effective name = `fieldName ?? name`; identity = `value ?? effective name`
- effective checked = case 2 → derived; else the owned/looked-up value
- effective indeterminate = case 2 → derived `|| indeterminate`; else `indeterminate`
- `aria-checked` = `"mixed"` if effective indeterminate, else effective checked; `data-*` per §5
- `filled` (field, standalone only) = `checked`; `dirty` (field, standalone only) = `checked !== initialValue`

No value normalization applies. Root and hidden inputs have no mount/unmount lifecycle; the Indicator does (§9).

- **CHK-STA-03** — MUST. For a standalone box, ownership of `checked` MUST be fixed on the first render: consumer-owned if `checked !== undefined`, otherwise component-owned. Later changes of `checked` between `undefined` and a boolean MUST NOT change the mode (a dev error is logged instead, CHK-EDGE-02).
  Source: `packages/utils/src/useControlled.ts:41,46`; see [UTL-STA-01](../architecture/shared-utilities.md). Test: none.
- **CHK-STA-04** — MUST. In component-owned mode, a non-canceled user toggle MUST set `checked` to the hidden input's post-click `checked` property (the inverse of the previous state).
  Source: `checkbox/root/CheckboxRoot.tsx:222,237`. Test: [L2] `CheckboxRoot.test.tsx` "should change its state when clicked".
- **CHK-STA-05** — MUST. In consumer-owned mode, a user toggle MUST NOT change the rendered state unless the consumer updates `checked`; when the consumer updates `checked` (from any cause) the root's `aria-checked`, data attributes, Indicator and hidden input `checked` property MUST follow. Server: relaxed, see §9.
  Source: `checkbox/root/CheckboxRoot.tsx:136-144,197,299`, `packages/utils/src/useControlled.ts:83-90`. Test: [L2] `CheckboxRoot.test.tsx` "should update its state when changed from outside", "does not update its state when onCheckedChange cancels the event" (`input.checked` restored).
- **CHK-STA-06** — MUST. When `checked` was consumer-owned at first render and later becomes `undefined`, the rendered state MUST fall back to `defaultChecked` as of the first render.
  Source: `packages/utils/src/useControlled.ts:43-46`; see [UTL-STA-03](../architecture/shared-utilities.md). Test: none.
- **CHK-STA-07** — MUST. A group member's checked state MUST equal "the group's current value contains the member's identity" on every render, regardless of the `checked`/`defaultChecked` props; a user toggle on a member MUST request a group value change (CHK-EVT-09) and MUST NOT change the member's own state otherwise.
  Source: `checkbox/root/CheckboxRoot.tsx:136-144,146,239-245`, `checkbox-group/useCheckboxGroupParent.ts:103`. Test: [L2] `CheckboxGroup.test.tsx` "should control the value", "should set the initial value", "does not update the group when onValueChange cancels the event".
- **CHK-STA-08** — MUST. A parent box in a group that has `allValues` MUST render `checked = (group value length === allValues length)` and `indeterminate = (0 < group value length < allValues length) || indeterminate prop`, re-derived on every group value change.
  Source: `checkbox/root/CheckboxRoot.tsx:110-111,146-149`, `checkbox-group/useCheckboxGroupParent.ts:24-25,46-48`. Test: [L2] `useCheckboxGroupParent.test.tsx` "parent should be marked as mixed if some children are checked", "should correctly initialize the values array", "should update the values array when a child checkbox is clicked"; `CheckboxRoot.test.tsx` "sets grouped parent aria when manually indeterminate".
- **CHK-STA-09** — MUST. While the effective indeterminate value is `true`, the rendered state (`aria-checked`, data attributes, Indicator) MUST be "mixed" regardless of `checked`; a user toggle MUST still flip the underlying `checked` (and fire `onCheckedChange` with the flipped value) without leaving the mixed rendering until the consumer clears `indeterminate`.
  Source: `checkbox/root/CheckboxRoot.tsx:147-149,222-237,299`, `checkbox/utils/getCheckboxStateAttributesMapping.ts:10-14`. Test: [L2] `CheckboxRoot.test.tsx` "should not change its state when clicked" (indeterminate; `aria-checked` stays `mixed`), "keeps the native input state when checked changes while indeterminate remains" (`input.checked` becomes `true`, `input.indeterminate` stays `true`), "should not be overridden by `checked` prop".
- **CHK-STA-10** — MUST. Within a `Field.Root` and outside a `CheckboxGroup`, the field's `filled` state MUST be set to `checked` immediately after the box mounts and again after every change of `checked`, post-commit, pre-paint. Inside a group the box MUST NOT write `filled` (the group does, [CKG-STA-11](checkbox-group.md)). Server: relaxed, see §9.
  Source: `checkbox/root/CheckboxRoot.tsx:178-182`. Test: [L2] `CheckboxRoot.test.tsx` "adds [data-filled] attribute when checked after being initially unchecked", "removes [data-filled] attribute when unchecked after being initially checked", "clears [data-filled] when a controlled checkbox remounts unchecked", "adds [data-filled] attribute when any checkbox is filled when inside a group".
- **CHK-STA-11** — MUST. Within a `Field.Root` and outside a group, after every change of `checked` (not on mount) the field's `dirty` state MUST be set to `checked !== initialValue`, so toggling back to the initial value clears `dirty`. Inside a group the box MUST NOT write `dirty`.
  Source: `checkbox/root/CheckboxRoot.tsx:184-193`, `internals/field-register-control/useFieldControlRegistration.ts:92-103`. Test: [L2] `CheckboxRoot.test.tsx` "[data-dirty]".
- **CHK-STA-12** — MUST. A detached box inside a group (no identity) and a parent box inside a group without `allValues` MUST keep their own `checked`/`defaultChecked` state exactly as a standalone box does, MUST NOT read or write the group's value, and MUST NOT be affected by a parent toggle.
  Source: `checkbox/root/CheckboxRoot.tsx:88-89,112,136-140,239`. Test: [L2] `useCheckboxGroupParent.test.tsx` "does not select a child without an identifying value".

## 5. Rendered DOM and ARIA

### Root element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `id` | generated `base-ui-…` id when `nativeButton=false`; the labelable control id (`id` prop, else scope-provided id, else generated) when `nativeButton=true` | CHK-DOM-01 | C# |
| `role` | `"checkbox"` (consumer may override) | CHK-ARIA-01 | C# |
| `aria-checked` | `"mixed"` / `"true"` / `"false"` | CHK-ARIA-02 | C# |
| `aria-readonly` | `"true"` only when `readOnly` | CHK-ARIA-03 | C# |
| `aria-required` | `"true"` only when `required` | CHK-ARIA-04 | C# |
| `aria-disabled` | `"true"` only when disabled and `nativeButton=false` | CHK-ARIA-05 | C# |
| `disabled` | present only when disabled and `nativeButton=true` | CHK-ARIA-05 | C# |
| `tabindex` | `0` (both modes); `-1` when disabled and `nativeButton=false` | CHK-FOC-01, CHK-FOC-02 | C# |
| `type` | `"button"` when `nativeButton=true` | CHK-DOM-10 | C# |
| `aria-labelledby` | see CHK-ARIA-06..09 | | C# |
| `aria-controls` | parent box only: registered child ids | CHK-ARIA-12 | C# |
| `aria-describedby` | consumer value + Field description/error ids | CHK-ARIA-10 | C# |
| `aria-invalid` | `"true"` when the field is invalid and the box is not disabled | CHK-ARIA-11 | C# |
| `data-parent` | `""` when `parent` | CHK-DOM-07 | C# |
| `data-*` | §3.1 table | CHK-DOM-02..06 | C# |

- **CHK-DOM-01** — MUST. When `nativeButton` is `false`, the root element MUST always carry an auto-generated `id` (never the `id` prop). When `nativeButton` is `true`, the root MUST carry the labelable control id: the `id` prop if non-empty, else the id supplied by the enclosing labelable scope (`Field.Root` or `Field.Item`), else a generated id.
  Source: `checkbox/root/CheckboxRoot.tsx:96,104-106,297`, `internals/labelable-provider/useLabelableId.ts:82`. Test: [L2] `CheckboxRoot.test.tsx` "should associate `id` with the native button when `nativeButton=true`", "assigns a root id to a valueless native button in a parent checkbox group".
  Port note 2026-10-23 (M2 track 6, design point G3, settled by the orchestrator 2026-10-23: the part's own read, no Rendering/ change): the root's `id` is decided in the part's attribute finish, which the pipeline runs after a substitute element's attributes merged. A substitute element's own `id` wins (RND-DOM-32: before this, the part's id replaced it); otherwise the part writes the id this row states. A group member registers the id it rendered for its parent's `aria-controls` (CHK-ARIA-12; `CheckboxGroupTests` "keeps a rendered child id in aria-controls (nativeButton=%s)").
- **CHK-DOM-02** — MUST. While effective indeterminate is `false`, exactly one of `data-checked` / `data-unchecked` MUST be present on the root, with empty-string value, matching effective `checked`.
  Source: `checkbox/utils/getCheckboxStateAttributesMapping.ts:16-20`. Test: [L2] `CheckboxRoot.test.tsx` "should place the style hooks on the root and the indicator", "prop: validationMode=onSubmit".
- **CHK-DOM-03** — MUST. `data-indeterminate=""` MUST be present exactly while effective indeterminate is `true`; while it is present, neither `data-checked` nor `data-unchecked` MUST be present.
  Source: `checkbox/utils/getCheckboxStateAttributesMapping.ts:11-14`, `internals/getStateAttributesProps.ts:24-25`. Test: [L2] `CheckboxRoot.test.tsx` "sets indeterminate style hooks on the root and indicator" (presence; absence of checked/unchecked is untested).
- **CHK-DOM-04** — MUST. `data-disabled`, `data-readonly`, `data-required` MUST be present (value `""`) exactly while the corresponding state field is `true`.
  Source: `internals/getStateAttributesProps.ts:24-28`, `checkbox/root/CheckboxRoot.tsx:278-288`. Test: [L2] `CheckboxRoot.test.tsx` "should place the style hooks on the root and the indicator".
- **CHK-DOM-05** — MUST. `data-touched`, `data-dirty`, `data-filled`, `data-focused` MUST be present (value `""`) exactly while the corresponding field state is `true`; outside a `Field.Root` they MUST never appear. Inside a group they reflect the field state that the group and the members jointly maintain, so every member shows the same values.
  Source: `checkbox/root/CheckboxRoot.tsx:280`, `internals/field-constants/constants.ts:18-32`, `internals/field-root-context/FieldRootContext.ts:37-67` (no-op setters outside a Field). Test: [L2] `CheckboxRoot.test.tsx` "[data-touched]", "[data-dirty]", "[data-filled]" group (incl. "adds [data-filled] attribute when any checkbox is filled when inside a group"), "[data-focused]".
- **CHK-DOM-06** — MUST. `data-valid` MUST be present when field `valid === true`, `data-invalid` when `valid === false`, and neither when `valid === null`.
  Source: `internals/field-constants/constants.ts:34-44`, `checkbox/utils/getCheckboxStateAttributesMapping.ts:22`. Test: [L2] `CheckboxRoot.test.tsx` "[data-invalid]", "[data-valid]".
- **CHK-DOM-07** — MUST. The root MUST carry `data-parent=""` exactly while the `parent` prop is `true` (inside or outside a group); the Indicator never carries it.
  Source: `checkbox/root/CheckboxRoot.tsx:37,303`. Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes" (filters members by `data-parent == null`).
- **CHK-DOM-08** — MUST. The Indicator element MUST carry the same state data attributes as the root (same mapping, same state, including `data-indeterminate` and the field attributes), updated in the same render as the root's, plus the transition attributes of §9.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:51-56,60-65`. Test: [L2] `CheckboxRoot.test.tsx` "should place the style hooks on the root and the indicator", "sets indeterminate style hooks on the root and indicator" (L4 for the transition-attribute clause).
- **CHK-DOM-09** — MUST. Attributes supplied by the consumer on `Checkbox.Root` MUST take precedence over the built-in root attributes with the same name (e.g. `role="switch"` replaces `role="checkbox"`), except `className`/`style` which merge per RND and event handlers which chain per RND (consumer handler first). Exception: on a parent box in a group with `allValues`, the group-derived `aria-controls` (CHK-ARIA-12) is merged after the consumer's props and therefore replaces a consumer `aria-controls` (even with "absent").
  Source: `checkbox/root/CheckboxRoot.tsx:378-379` (merge order), `merge-props/mergeProps.ts:150-188`; see [RND-DOM-19](../architecture/rendering-and-composition.md). Test: [L2] `CheckboxRoot.test.tsx` "can override the built-in attributes".
- **CHK-DOM-10** — MUST. When `nativeButton` is `true`, the root MUST receive `type="button"` (so it never submits a form by its own activation); when `false`, no `type` attribute is added.
  Source: `internals/use-button/useButton.ts:226`; see [UBT-DOM-01](../architecture/button-behavior.md). Test: none.

### ARIA

- **CHK-ARIA-01** — MUST. The root MUST have `role="checkbox"` unless the consumer supplies a `role`.
  Source: `checkbox/root/CheckboxRoot.tsx:298`. Test: [L2] `CheckboxRoot.test.tsx` "sets the correct aria attributes" (`getByRole('checkbox')` is the root), "can override the built-in attributes".
- **CHK-ARIA-02** — MUST. The root MUST have `aria-checked="mixed"` while effective indeterminate is `true`, otherwise the string form of effective `checked` (`"true"`/`"false"`), updated in the same render as the state change.
  Source: `checkbox/root/CheckboxRoot.tsx:299`. Test: [L2] `CheckboxRoot.test.tsx` "should change its state when clicked", "should set the `aria-checked` attribute as \"mixed\"", "should not have the aria attribute when `indeterminate` is not set".
- **CHK-ARIA-03** — MUST. `aria-readonly="true"` MUST be present when `readOnly` is `true` and absent otherwise (never `"false"`).
  Source: `checkbox/root/CheckboxRoot.tsx:300`. Test: [L2] `CheckboxRoot.test.tsx` "should have the `aria-readonly` attribute", "should not have the aria attribute when `readOnly` is not set".
- **CHK-ARIA-04** — MUST. `aria-required="true"` MUST be present when `required` is `true` and absent otherwise.
  Source: `checkbox/root/CheckboxRoot.tsx:301`. Test: [L2] `CheckboxRoot.test.tsx` "sets the correct aria attributes".
- **CHK-ARIA-05** — MUST. When disabled and `nativeButton` is `false`, the root MUST carry `aria-disabled="true"` and MUST NOT carry a `disabled` attribute. When disabled and `nativeButton` is `true`, the root MUST carry the `disabled` attribute and no `aria-disabled`. When not disabled, neither is present.
  Source: `utils/useFocusableWhenDisabled.ts:38-47`, `checkbox/root/CheckboxRoot.tsx:129-132`; see [UBT-ARIA-02](../architecture/button-behavior.md), [UBT-DOM-06](../architecture/button-behavior.md). Test: [L2] `CheckboxRoot.test.tsx` "uses aria-disabled instead of HTML disabled"; `CheckboxGroup.test.tsx` "disables all checkboxes when `true`", "does not disable all checkboxes when `false`".
- **CHK-ARIA-06** — MUST. The root's `aria-labelledby` MUST be resolved in this precedence: (1) the consumer's `aria-labelledby` prop; else (2) if the consumer's `aria-label` is a non-blank string (after trimming), no `aria-labelledby` at all — even when a `Field.Label` is present; else (3) the id of the `Field.Label` in the box's labelable scope (`Field.Item` when present, else `Field.Root`), when one is mounted; else (4) the fallback in CHK-ARIA-07; else absent.
  Source: `checkbox/root/CheckboxRoot.tsx:164-171,302`, `internals/labelable-provider/useAriaLabelledBy.ts:16-20`; see [FFI-ARIA-05](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "prefers `aria-label` over an associated label", "when label and checkbox are siblings" (Field.Label id), "sets `aria-labelledby` from a sibling label associated with the hidden input"; `CheckboxGroup.test.tsx` "implicit association" (Field.Item label) (L4 for the native-label fallback clause). Docs: CHANGELOG.md v1.9.0 "Prefer `aria-label` over associated labels".
- **CHK-ARIA-07** — MUST. Fallback (`nativeButton=false` only): the component MUST look for a native `<label>` associated with the hidden checkbox input, in this order: the input's parent element if it is a `<label>`; else, if the input has an `id`, its next element sibling if that is a `<label>` whose `htmlFor` equals the input's id; else the first entry of the input's `labels` list. If a label is found and has no `id`, the component MUST assign it the id `"<hidden input id>-label"`; the label's id (existing or assigned) becomes the root's `aria-labelledby`.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:16,40-74`; see [FFI-ARIA-06](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input", "ignores a blank `aria-label` (%j)" (wrapping and sibling labels name the box); `CheckboxGroup.test.tsx` "gives each checkbox in a shared Field.Root its own accessible name". Docs: CHANGELOG.md v1.2.0 "Add automatic `aria-labelledby` support".
  Port note 2026-10-23 (M2 track 5, design point C4, settled by the orchestrator 2026-10-23: one report per change of the result, ADR 16 rule 6; ADR 04 §1, the `form` row's native label lookup): the hidden input's `form` handle runs this discovery while nothing earlier in CHK-ARIA-06 names the box and the box is not a native button, and reports the label's id, which C# renders as the root's `aria-labelledby`.
- **CHK-ARIA-08** — MUST. The fallback in CHK-ARIA-07 MUST be re-evaluated post-commit, pre-paint after every update of the root (each time its attributes are recomputed, for any cause, not only when its own props change), so that a change of the hidden input's `id`, a label mounting/unmounting, or `aria-label` being added, blanked or removed updates the accessible name without any other prop change. Server: relaxed, see §9.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:22-35`. Test: [L4] `CheckboxRoot.test.tsx` "updates fallback `aria-labelledby` when the hidden input id changes", "updates the accessible name when `aria-label` is added, cleared, or removed", "updates the accessible name when the associated label mounts and unmounts" (both `native` and `Field.Label` variants).
- **CHK-ARIA-09** — MUST. When `nativeButton` is `true`, the fallback in CHK-ARIA-07 MUST be disabled (steps 1-3 of CHK-ARIA-06 still apply); a native `<label htmlFor>` then names the `<button>` through native label semantics.
  Source: `checkbox/root/CheckboxRoot.tsx:168`, `internals/labelable-provider/useAriaLabelledBy.ts:28-30`. Test: [L2] `CheckboxRoot.test.tsx` "should associate `id` with the native button when `nativeButton=true`" (no `aria-labelledby` asserted; implied).
- **CHK-ARIA-10** — MUST. Within a `Field.Root`, the root's `aria-describedby` MUST be the consumer's `aria-describedby` tokens followed by the message ids of the enclosing labelable scopes — the `Field.Root` scope's `Field.Description`/`Field.Error` ids, then, when the box is inside a `Field.Item`, that item's ids — deduplicated and space-separated; absent when the result is empty.
  Source: `checkbox/root/CheckboxRoot.tsx:381-382`, `internals/labelable-provider/LabelableProvider.tsx:68-81`, `field/root/useFieldValidation.ts:367-376`; see [FFI-ARIA-01](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "Field.Description"; `CheckboxGroup.test.tsx` "links the group and individual checkboxes", "explicit association", "appends the id attribute of the error to aria-describedby of individual checkboxes".
- **CHK-ARIA-11** — MUST. Within a `Field.Root`, the root MUST carry `aria-invalid="true"` exactly while the field's `valid` is `false` and neither the field nor the box is disabled; otherwise `aria-invalid` is absent. Inside a group every non-disabled member reports the group's field validity this way.
  Source: `field/root/useFieldValidation.ts:367-376`, `checkbox/root/CheckboxRoot.tsx:134,382`; see [FFI-ARIA-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "prop: validationMode=onSubmit", "clears external errors on change"; `CheckboxGroup.test.tsx` "prop: validationMode=onChange".
- **CHK-ARIA-12** — MUST. A parent box in a group with `allValues` MUST carry `aria-controls` = the space-separated rendered root ids of the mounted members, ordered by the position of their identity in `allValues` (members sharing an identity in mount order), and MUST omit the attribute while no member is registered. Members register their rendered id (the id actually present on their root element, including a substitute element's own id or a consumer `id` under `nativeButton`) post-commit, pre-paint, and withdraw it on unmount. See [CKG-ARIA-06](checkbox-group.md) for the group-side rule. Server: relaxed, see §9.
  Source: `checkbox/root/CheckboxRoot.tsx:379,388-395`, `checkbox-group/useCheckboxGroupParent.ts:29-43,51-53`; see [RND-DOM-32](../architecture/rendering-and-composition.md). Test: [L2] `useCheckboxGroupParent.test.tsx` "should apply space-separated aria-controls attribute with child names", "keeps a custom child id in aria-controls", "keeps a rendered child id in aria-controls (nativeButton=%s)", "references the exposed child rather than its custom-id input without nativeButton", "drops an unmounted child from aria-controls", "keeps both ids for checkboxes sharing a value and retains the survivor"; `CheckboxRoot.react17.test.tsx` "wires parent aria-controls once the fallback ids are assigned (nativeButton=%s)".
  Port note 2026-10-23 (ADR 02 decision 5, §16 Q10): generated ids are drawn eagerly, so `CheckboxRoot.react17.test.tsx`'s intermediate render without ids does not arise; its titles are translated once, by their end state.

### Hidden checkbox input

Rendered always, as the root's next sibling (after the optional `<input type="hidden">`).

| Attribute / property | Value | Req | Writer |
|---|---|---|---|
| `type` | `"checkbox"` | CHK-DOM-11 | C# |
| `aria-hidden` | `"true"` | CHK-DOM-11 | C# |
| `tabindex` | `-1` | CHK-DOM-11 | C# |
| `checked` (property) | underlying `checked` state | CHK-DOM-12 | C# → JS |
| `indeterminate` (property) | effective indeterminate | CHK-DOM-13 | JS |
| `disabled` | effective disabled | CHK-DOM-12 | C# |
| `required` | `required` | CHK-DOM-12 | C# |
| `name` | effective name; absent for a parent box or when no name | CHK-DOM-12 | C# |
| `form` | `form` prop | CHK-DOM-12 | C# |
| `id` | labelable control id when `nativeButton=false`; absent when `true` | CHK-DOM-15 | C# |
| `value` | see CHK-DOM-16 | CHK-DOM-16 | C# |
| `style` | visually-hidden declarations (two variants) | CHK-DOM-14 | C# |
| `aria-describedby` | Field description/error ids | CHK-DOM-18 | C# |
| `aria-invalid` | `"true"` when field invalid and not disabled | CHK-DOM-18 | C# |

- **CHK-DOM-11** — MUST. The hidden input MUST be `<input type="checkbox">` with `aria-hidden="true"` and `tabindex="-1"`.
  Source: `checkbox/root/CheckboxRoot.tsx:208-210`. Test: [L2] `CheckboxRoot.test.tsx` `getAllByRole('checkbox', { hidden: true })` queries (second element).
- **CHK-DOM-12** — MUST. The hidden input's `checked` property MUST always equal the underlying `checked` state (not the "mixed" rendering); its `disabled`, `required` and `form` attributes MUST mirror the effective disabled, `required` and `form` values; its `name` MUST be the effective name except for a parent box, which MUST have no `name`.
  Source: `checkbox/root/CheckboxRoot.tsx:197-205`. Test: [L2] `CheckboxRoot.test.tsx` "should change its state when clicked" (`input.checked`), "should receive name prop from Field.Root", "keeps the native input state when checked changes while indeterminate remains"; `CheckboxGroup.test.tsx` "excludes parent checkboxes from form submission".
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22, the ownership row of the hidden input's `checked`, as SWT-DOM-08's note of 2026-10-22): the `checked` property's `Writer` cell is `C# → JS`. C# renders the initial `checked` once; from attach the `dom` primitive owns the property, sets it on every `update` and is asked to restore it (`DomOptions.CheckedRestores`) when a request leaves the box's value different from the input; it is re-asserted after the owning form's `reset` (§16 Q7).
- **CHK-DOM-13** — MUST. The hidden input's `indeterminate` property MUST be set to the effective indeterminate value post-commit, pre-paint after mount and after every change of `checked` or of effective indeterminate (a native click clears the property, so it is re-asserted after each toggle). Server: relaxed, see §9.
  Source: `checkbox/root/CheckboxRoot.tsx:173-177`. Test: [L4] `CheckboxRoot.test.tsx` "sets the native input state when indeterminate", "keeps the native input state when checked changes while indeterminate remains", "sets grouped parent native input state when manually indeterminate".
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-20, `indeterminate` is the `dom` primitive's; ADR 01 relaxation table, the row "JS-owned values that originate in C# state"): the `Writer` cell is `JS`. C# sends the effective value in `DomOptions.Indeterminate`; the primitive sets it on attach and on every `update`, and again inside every native click, which clears it, so no listener after the click sees it cleared. Met on both modes: the value C# sends lags a C# change by the update's round trip on Server, while the re-assertion inside the click is immediate.
- **CHK-DOM-14** — MUST. The hidden input MUST be visually hidden by inline style. With no effective name the declarations are: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0`. With an effective name they are: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute` (kept in flow near the root so native validation UI appears beside the box).
  Source: `checkbox/root/CheckboxRoot.tsx:207`, `packages/utils/src/visuallyHidden.ts:3-25`; see [FFI-DOM-02](../architecture/field-and-form-infrastructure.md), [UTL-DOM-13](../architecture/shared-utilities.md), [UTL-DOM-14](../architecture/shared-utilities.md). Test: none.
- **CHK-DOM-15** — MUST. When `nativeButton` is `false`, the hidden input MUST carry the labelable control id — the `id` prop when non-empty; else, when the box owns its labelable scope's control id (standalone, or inside a `Field.Item`), the scope's resolved control id; else a generated id — and it MUST never be absent. When `nativeButton` is `true`, the hidden input MUST have no `id`. A box that shares its labelable scope with an enclosing `CheckboxGroup` MUST NOT claim that scope's control id (so several members never render the same id).
  Source: `checkbox/root/CheckboxRoot.tsx:98-104,204`, `internals/labelable-provider/useLabelableId.ts:82`; see [FFI-DOM-04](../architecture/field-and-form-infrastructure.md), [FFI-ARIA-09](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "should toggle the checkbox when a explicitly linked <label> is clicked", "falls back to the Field control id when id is empty", "assigns an input id to a valueless child in a parent checkbox group", "sets `for` on the label"; `CheckboxGroup.test.tsx` "keeps checkbox ids unique when the group shares one Field.Root (nativeButton=%s)".
- **CHK-DOM-16** — MUST. The hidden input MUST have a `value` attribute only when the `value` prop is defined. Standalone, the attribute MUST equal the `value` prop (an empty string stays `""`). Inside a `CheckboxGroup`, the attribute MUST equal the `value` prop while the box is checked and `""` while it is unchecked. A `value` derived from the name (CHK-API-06) is never written to the attribute.
  Source: `checkbox/root/CheckboxRoot.tsx:256-260`. Test: [L2] `CheckboxRoot.test.tsx` "should include the custom checkbox value in form submission, matching native checkbox behavior" (L4 for the `FormData` citation); `CheckboxGroup.test.tsx` "clears custom validity from disabled registered inputs when the group becomes valid" (queries `input[value="http"]` after checking).
  Port note 2026-10-23: the `FormData` citation is L4 (ADR 06 §1, ADR 13 §3); the attribute itself is asserted at L2 by the port title "the input's value is the prop when given, an empty string included, and absent otherwise".
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-DOM-17** — MUST. `name` and `value` MUST NOT appear on the root element.
  Source: `checkbox/root/CheckboxRoot.tsx:49-70` (consumed before forwarding). Test: [L2] `CheckboxRoot.test.tsx` "should set the name attribute only on the input".
- **CHK-DOM-18** — MUST. Within a `Field.Root`, the hidden input MUST also receive `aria-describedby` (the labelable scopes' message ids only, without the consumer's tokens) and `aria-invalid="true"` under the same condition as CHK-ARIA-11.
  Source: `checkbox/root/CheckboxRoot.tsx:261-262`, `field/root/useFieldValidation.ts:367-376`. Test: none (the Checkbox "Field.Description" test asserts the root, not the input).

### Hidden `type="hidden"` input (unchecked value)

- **CHK-DOM-19** — MUST. An `<input type="hidden">` with `name` = effective name, `value` = `uncheckedValue`, `form` = `form` prop and `disabled` = effective disabled MUST be rendered between the root and the checkbox input exactly while `checked` is `false` AND the box is not inside a `CheckboxGroup` AND an effective name is non-empty AND `parent` is `false` AND `uncheckedValue !== undefined`; otherwise it MUST NOT exist.
  Source: `checkbox/root/CheckboxRoot.tsx:400-402`; see [FFI-DOM-07](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "submits uncheckedValue to an external form when unchecked", "should submit uncheckedValue when checkbox is unchecked and uncheckedValue is specified", "does not submit uncheckedValue when disabled".

## 6. Interactions

All toggles funnel through one path: a `click` is delivered to the hidden checkbox input, the browser flips the input's `checked`, and the component reads the input's new `checked` to produce the next state (see §7 for the callback and cancel step). The root's own click handler is the bridge from the visible element to the input. <kbd>Enter</kbd> is handled separately: it never reaches the input and instead triggers implicit form submission.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Space</kbd> | root focused, `nativeButton=false`, not disabled | On keydown: default prevented (no page scroll), no toggle. On keyup (if its default was not prevented): a click with modifier state is dispatched on the root → toggle. | CHK-KEY-01 |
| <kbd>Space</kbd> | root focused, `nativeButton=true` | Not intercepted; the browser's native button activation produces the click → toggle. | CHK-KEY-02 |
| <kbd>Enter</kbd> | root focused, either mode, not disabled | Never toggles. The native default is cancelled (so a native `<button>` root does not click itself). After a microtask, unless any handler called `preventDefault()` during propagation, the owning form's default submit button is clicked → form submits with that button as submitter. | CHK-KEY-03, CHK-KEY-04, CHK-KEY-05 |
| <kbd>Enter</kbd> | `readOnly` | Submits as above (no toggle anyway). | CHK-KEY-06 |
| <kbd>Enter</kbd> | disabled | Nothing (no submit, no `preventDefault`). | CHK-KEY-06 |
| any key | event target is a descendant of the root, not the root itself | No toggle. <kbd>Enter</kbd> is still handled as above (native default cancelled, form submitted). | CHK-KEY-07 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Not intercepted; native focus movement. | CHK-KEY-08 |
| any key | disabled | Not handled (no preventDefault, no toggle). | CHK-KEY-09 |
| <kbd>Space</kbd> | `readOnly` | Click is still dispatched on the root; the root click handler ignores it, so no toggle. | CHK-KEY-12 |
| Modifier combinations | any of the above | Modifiers never change behavior; their state is carried into the resulting click/change event. | CHK-KEY-10 |

There are no orientation or RTL variants and no arrow-key behavior (also inside a group — [CKG-KEY-01](checkbox-group.md)).

- **CHK-KEY-01** — MUST. With `nativeButton=false`, a <kbd>Space</kbd> keydown whose target is the root MUST have its default prevented without toggling; the subsequent <kbd>Space</kbd> keyup on the root MUST toggle the box (by dispatching a click on the root) unless the keyup's default was prevented.
  Source: `internals/use-button/useButton.ts:155-170,207-216`, `checkbox/root/CheckboxRoot.tsx:129-132,363-376`; see [UBT-KEY-02](../architecture/button-behavior.md). Test: [L4] `CheckboxRoot.test.tsx` "can be activated with Space key".
- **CHK-KEY-02** — MUST. With `nativeButton=true`, <kbd>Space</kbd> MUST NOT be intercepted; the browser's native click MUST toggle the box.
  Source: `internals/use-button/useButton.ts:131,155-163`; see [UBT-KEY-09](../architecture/button-behavior.md). Test: [L4] `CheckboxRoot.test.tsx` "can render a native button" (`[Space]` toggles, `[Enter]` does not).
- **CHK-KEY-03** — MUST. An <kbd>Enter</kbd> keydown whose target is the root MUST NOT toggle the box in either mode: the component MUST suppress the button mechanism's Enter activation and MUST cancel the native default action of the keydown (on the native event only, so that ancestor handlers still observe `defaultPrevented === false`).
  Source: `checkbox/root/CheckboxRoot.tsx:320-332,349-352`, `internals/use-button/useButton.ts:121-125`; see [UBT-KEY-06](../architecture/button-behavior.md). Test: [L4] `CheckboxRoot.test.tsx` "does not activate with Enter key", "can render a native button", "submits the form once with Enter when rendered as a native button" (`aria-checked` stays `false`), "does not submit the form with Enter when an ancestor prevents default" (ancestor sees `defaultPrevented === false`).
  Port note 2026-10-23 (M2 track 5, design point C2, settled by the orchestrator 2026-10-23; ADR 04 §1, the `form` row owns the Enter-to-submit path): the `form` module's listener runs in the capture phase at the root and cancels the native default at once, so a JavaScript ancestor observes `defaultPrevented === true`; the port has one event, not React's synthetic and native pair. A later `preventDefault()` is still recorded and blocks the submission (CHK-KEY-05). A C# ancestor has no `defaultPrevented` (ADR 02 decision 3).
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-KEY-04** — MUST. After an <kbd>Enter</kbd> keydown on an enabled root, the component MUST, after a microtask (queued on the root's window), click the default submit button of the hidden input's owning form (the first `<button>`/`<input>` of `type="submit"` among the form's elements, honoring the `form` attribute) provided no `preventDefault()` was observed during the keydown's propagation. Clicking the submitter MUST run its own click handlers exactly once and submit with `SubmitEvent.submitter` set to it; a disabled default submitter, a form with no submit button, or a box with no owning form MUST result in no submission.
  Source: `checkbox/root/CheckboxRoot.tsx:333-361`, `packages/utils/src/getDefaultFormSubmitter.ts:14-34`; see [FFI-KEY-02](../architecture/field-and-form-infrastructure.md), [FFI-ALG-10](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "submits the form when Enter is pressed while focused", "submits to an external form with Enter when `form` is provided", "does not submit the form with Enter when there is no submit button", "does not submit the form with Enter when the default button is disabled", "submits the form once with Enter when rendered as a native button". Docs: CHANGELOG.md v1.5.0 "Submit the associated form when pressing Enter".
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-KEY-05** — MUST. The submission of CHK-KEY-04 MUST NOT happen when the consumer's `onKeyDown` on the root called `preventDefault()` before the component's handler ran, nor when any handler later in the propagation (an ancestor) calls `preventDefault()` on the keydown before the microtask runs.
  Source: `checkbox/root/CheckboxRoot.tsx:329-331,336-347,358-360`. Test: [L4] `CheckboxRoot.test.tsx` "does not submit the form with Enter when the consumer prevents default", "does not submit the form with Enter when an ancestor prevents default".
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-KEY-06** — MUST. <kbd>Enter</kbd> on a `readOnly` box MUST still submit per CHK-KEY-04; <kbd>Enter</kbd> on a disabled box MUST do nothing (no submission, no `preventDefault`).
  Source: `checkbox/root/CheckboxRoot.tsx:320-362` (no `readOnly` check), `internals/use-button/useButton.ts:117-119` (disabled short-circuit before the component's handler). Test: [L4] `CheckboxRoot.test.tsx` "submits the form with Enter when readOnly"; disabled: none.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-KEY-07** — MUST. Key events whose target is not the root element itself MUST NOT toggle the box. The <kbd>Enter</kbd> handling of CHK-KEY-03..05 is not filtered by target: an <kbd>Enter</kbd> keydown bubbling to the root from a focusable descendant MUST likewise have its native default cancelled and MUST trigger the submission of CHK-KEY-04.
  Source: `internals/use-button/useButton.ts:127,131,208`, `checkbox/root/CheckboxRoot.tsx:320-362` (no target check); see [UBT-KEY-08](../architecture/button-behavior.md). Test: none.
- **CHK-KEY-08** — MUST. <kbd>Tab</kbd> MUST NOT be intercepted.
  Source: `internals/use-button/useButton.ts:116-176` (no Tab branch), `checkbox/root/CheckboxRoot.tsx:321-323`; see [UBT-KEY-21](../architecture/button-behavior.md). Test: [L4] `CheckboxRoot.test.tsx` "can be activated with Space key" (`[Tab]` focuses root).
- **CHK-KEY-09** — MUST. While disabled, keydown/keyup on the root MUST have no effect (no toggle, no submission, no `preventDefault`, consumer key handlers not invoked).
  Source: `internals/use-button/useButton.ts:117-119,178-180`; see [UBT-KEY-19](../architecture/button-behavior.md). Test: none.
- **CHK-KEY-10** — MUST. When a toggle originates from the keyboard, the `event` in the change details (§7) MUST report the keyboard event's `shiftKey`, `ctrlKey`, `altKey`, `metaKey`.
  Source: `utils/dispatchClickWithModifiers.ts:20-41`, `internals/use-button/useButton.ts:215`, `checkbox/root/CheckboxRoot.tsx:375`; see [UBT-EVT-05](../architecture/button-behavior.md). Test: none for keyboard (`CheckboxRoot.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange" covers a pointer click with <kbd>Shift</kbd> held). Docs: CHANGELOG.md v1.1.0 "Preserve modifier key properties in the change event".
- **CHK-KEY-11** — MUST. With `nativeButton=false`, a consumer `onKeyUp` handler that calls `preventDefault()` MUST cancel <kbd>Space</kbd> activation; a prevented <kbd>Space</kbd> *keydown* does not cancel the keyup activation (see [UBT-KEY-05](../architecture/button-behavior.md)). Server: relaxed, see §9.
  Source: `internals/use-button/useButton.ts:204-213`; see [UBT-KEY-04](../architecture/button-behavior.md). Test: none.
- **CHK-KEY-12** — MUST. When `readOnly`, <kbd>Space</kbd> activation MUST NOT change the state.
  Source: `checkbox/root/CheckboxRoot.tsx:364-366`. Test: none (pointer equivalent: "should not change its state when clicked" under `prop: readOnly`).

### 6.2 Pointer and touch

Pointer type (mouse/touch/pen) makes no difference. There is no hover, drag, long-press or right-click behavior.

| Gesture | Behavior | Req |
|---|---|---|
| Click on root (any pointer, or synthetic `click()`) | Root click default prevented; a click carrying modifier state is dispatched on the hidden input → toggle. | CHK-PTR-01, CHK-PTR-02 |
| Click on root while disabled | Default prevented, nothing else (`pointerdown` default is also prevented). | CHK-PTR-08 |
| Click on root while `readOnly` | Ignored entirely (default not prevented). | CHK-PTR-07 |
| Click on root while indeterminate | Underlying `checked` flips; rendering stays "mixed". | CHK-PTR-12 |
| Click on the hidden input directly | Toggle (unless `readOnly`/disabled). | CHK-PTR-04 |
| Click on a wrapping native `<label>` | Browser activates the hidden input → toggle. | CHK-PTR-05 |
| Click on a native `<label htmlFor=…>` | Browser activates the element with that id (hidden input or native-button root) → toggle. | CHK-PTR-06 |
| Click on a non-native `Field.Label` (`nativeLabel={false}`) | Focuses the box; no toggle. | CHK-PTR-10 |

- **CHK-PTR-01** — MUST. A click on the root (not disabled, not read-only) MUST toggle the box by dispatching a `click` on the hidden checkbox input; that click MUST be a bubbling, cancelable, composed pointer-type click with `detail` 0, empty `pointerType`, and the root click's `shiftKey`/`ctrlKey`/`altKey`/`metaKey`.
  Source: `checkbox/root/CheckboxRoot.tsx:363-376`, `utils/dispatchClickWithModifiers.ts:20-41`; see [UBT-EVT-09](../architecture/button-behavior.md), [UBT-EVT-04](../architecture/button-behavior.md). Test: [L4] `CheckboxRoot.test.tsx` "should change its state when clicked", "should report keyboard modifier event properties when calling onCheckedChange".
- **CHK-PTR-02** — MUST. The root's click handler MUST call `preventDefault()` on the root click before dispatching to the input (so that an enclosing `<label>`'s activation behavior does not deliver a second click to the input). This happens only when not disabled and not read-only; the hidden input MUST be dispatched to only if it is mounted.
  Source: `checkbox/root/CheckboxRoot.tsx:364-375`. Test: [L4] `CheckboxRoot.test.tsx` "should toggle the checkbox when a wrapping <label> is clicked", "should change state when clicking the checkbox if it has a wrapping label" (single toggle per click).
- **CHK-PTR-03** — MUST. The click dispatched on the hidden input MUST NOT propagate to ancestors: for one user click on the root, ancestor click listeners MUST observe exactly one click event (the original), with both `nativeButton` values.
  Source: `checkbox/root/CheckboxRoot.tsx:247-251`. Test: [L4] `CheckboxRoot.test.tsx` "propagates a single click event to ancestors per user click (nativeButton=%s)" (2 variants). Docs: CHANGELOG.md v1.7.0 "Stop internal input clicks from reaching ancestors".
- **CHK-PTR-04** — MUST. A click delivered directly to the hidden input (e.g. by a label or script) MUST toggle the box exactly as a root click does.
  Source: `checkbox/root/CheckboxRoot.tsx:211-246`. Test: [L2] `CheckboxRoot.test.tsx` "should update its state if the underlying input is toggled" (both occurrences).
- **CHK-PTR-05** — MUST. With `nativeButton=false`, clicking anywhere on a native `<label>` that wraps the box MUST toggle it once.
  Source: `checkbox/root/CheckboxRoot.tsx:364-375,403` (input is a labelable descendant of the label). Test: [L4] `CheckboxRoot.test.tsx` "should toggle the checkbox when a wrapping <label> is clicked", "sets `for` on the label" (implicit `Field.Label`). Docs: `components/checkbox/page.mdx` §Labeling a checkbox.
- **CHK-PTR-06** — MUST. Clicking a native `<label htmlFor={id}>` MUST toggle the box when `id` equals the box's `id` prop, for both `nativeButton=false` (label targets the hidden input) and `nativeButton=true` (label targets the root `<button>`).
  Source: `checkbox/root/CheckboxRoot.tsx:104-106,204,297`. Test: [L4] `CheckboxRoot.test.tsx` "should toggle the checkbox when a explicitly linked <label> is clicked", "should associate `id` with the native button when `nativeButton=true`". Docs: `components/checkbox/page.mdx` §Rendering as a native button.
- **CHK-PTR-07** — MUST. When `readOnly`, a click on the root MUST be ignored (no `preventDefault`, no dispatch), and a click reaching the hidden input (from a label or directly) MUST NOT change the state: the input's change is reverted and `onCheckedChange` is not called.
  Source: `checkbox/root/CheckboxRoot.tsx:217-220,364-366`. Test: [L4] `CheckboxRoot.test.tsx` "should not change its state when clicked" (readOnly), "should not change its state when its label is clicked". Docs: CHANGELOG.md v1.4.0 "Prevent input state changes in `readOnly` mode".
- **CHK-PTR-08** — MUST. When disabled, a click on the root MUST have its default prevented and MUST NOT toggle; `pointerdown` on the root MUST also have its default prevented; consumer `onClick`/`onMouseDown`/`onPointerDown` handlers MUST NOT be invoked.
  Source: `internals/use-button/useButton.ts:104-115,218-224`, `checkbox/root/CheckboxRoot.tsx:364-366`; see [UBT-PTR-01](../architecture/button-behavior.md)…[UBT-PTR-03](../architecture/button-behavior.md). Test: [L4] `CheckboxRoot.test.tsx` "should not change its state when clicked" (disabled).
- **CHK-PTR-09** — MUST. A consumer `onClick` on the root MUST run before the internal toggle for the same click, exactly once per click. Calling `stopPropagation()` in it MUST stop the click reaching ancestors but MUST NOT prevent the toggle. Calling `preventBaseUIHandler()` on the event MUST prevent the toggle. Server: relaxed, see §9.
  Source: `merge-props/mergeProps.ts:210-239`, `checkbox/root/CheckboxRoot.tsx:378-380`; see [RND-EVT-02](../architecture/rendering-and-composition.md), [RND-EVT-03](../architecture/rendering-and-composition.md). Test: [L4] `CheckboxRoot.test.tsx` "does not propagate to ancestors when stopPropagation() is called (nativeButton=%s)" (2 variants).
- **CHK-PTR-10** — MUST. Clicking a `Field.Label` rendered as a non-native label (`nativeLabel={false}`) MUST focus the box's root and MUST NOT toggle it.
  Source: `internals/labelable-provider/useLabel.ts:46-62,70-76`, `checkbox/root/CheckboxRoot.tsx:252-254`; see [FFI-PTR-01](../architecture/field-and-form-infrastructure.md). Test: none in Checkbox (Switch has the equivalent test).
- **CHK-PTR-11** — MUST. A click event delivered to the hidden input whose default was already prevented before dispatch MUST be ignored: `onCheckedChange` MUST NOT be called and the state MUST NOT change.
  Source: `checkbox/root/CheckboxRoot.tsx:212-215`. Test: [L4] `CheckboxRoot.test.tsx` "ignores a hidden input click canceled before React handles it".
- **CHK-PTR-12** — MUST. A click on an indeterminate box (not read-only, not disabled) MUST flip the underlying `checked` (firing `onCheckedChange` with the flipped value and updating the hidden input's `checked`) while the root keeps `aria-checked="mixed"` and the hidden input's `indeterminate` property is re-asserted to `true`.
  Source: `checkbox/root/CheckboxRoot.tsx:173-177,222-237,299`. Test: [L4] `CheckboxRoot.test.tsx` "should not change its state when clicked" (indeterminate), "keeps the native input state when checked changes while indeterminate remains".

### 6.3 Focus management

- **CHK-FOC-01** — MUST. The root MUST carry `tabindex="0"` in both modes (with `nativeButton=true` too, including while disabled, where the `disabled` attribute still removes it from the tab order), except per CHK-FOC-02, making it the only tab stop of the component; the hidden input (`tabindex="-1"`) MUST never be a tab stop. A consumer `tabIndex` prop overrides the built-in value.
  Source: `utils/useFocusableWhenDisabled.ts:30-36`, `checkbox/root/CheckboxRoot.tsx:208`; see [UBT-DOM-03](../architecture/button-behavior.md). Test: [L2] `CheckboxRoot.test.tsx` "can be activated with Space key" (`[Tab]` focuses the root), "can render a native button" (L4 for the tab-order clause).
- **CHK-FOC-02** — MUST. When disabled and `nativeButton=false`, the root MUST have `tabindex="-1"` (removed from the tab order but still focusable programmatically). When disabled and `nativeButton=true`, the `disabled` attribute makes it unfocusable.
  Source: `utils/useFocusableWhenDisabled.ts:33-35,45-47`; see [UBT-FOC-01](../architecture/button-behavior.md), [UBT-FOC-02](../architecture/button-behavior.md). Test: [L2] `CheckboxRoot.test.tsx` "does not set [data-focused] when disabled" (programmatic focus event on a disabled root).
- **CHK-FOC-03** — MUST. If the hidden input receives focus (e.g. from a native validation bubble, a label click or a focus-first-invalid routine), focus MUST be moved immediately to the root element.
  Source: `checkbox/root/CheckboxRoot.tsx:252-254`; see [FFI-FOC-05](../architecture/field-and-form-infrastructure.md). Test: none.
- **CHK-FOC-04** — MUST. Within a `Field.Root`, the field's `focused` state MUST become `true` when the root receives focus (unless disabled) and `false` when it loses focus, provided this box is the control that last set it.
  Source: `checkbox/root/CheckboxRoot.tsx:127,304-315`, `internals/field-root-context/useSetFieldFocused.ts:22-30`; see [FFI-STA-09](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "[data-focused]", "does not set [data-focused] when disabled".
- **CHK-FOC-05** — MUST. Within a `Field.Root`, if the root is focused and then becomes disabled or unmounts without a blur event, the field's `focused` state MUST be cleared; conversely, a root that is already the active element when its focus tracking is (re)initialized (focused during mount) MUST acquire the `focused` state without a focus event.
  Source: `internals/field-root-context/useSetFieldFocused.ts:36-43`. Test: [L4] `CheckboxRoot.test.tsx` "is removed when the focused checkbox becomes disabled", "is removed when the focused checkbox unmounts", "is kept when the checkbox is focused during mount in StrictMode".
  Port note 2026-10-23 (ADR 06 Amendments 2026-10-18): the StrictMode title "is kept when the checkbox is focused during mount in StrictMode" is restated as its port property, "a root that is already focused when its tracking starts acquires the focused state": the `form` module reports a root that is the active element when it attaches (FFI-STA-09).
- **CHK-FOC-06** — MUST. When form submission is blocked by a field whose control is a standalone box, the root element MUST be the element focused by the form (it is the field's registered control). Inside a group, the focus target is the group's representative member ([CKG-FOC-02](checkbox-group.md)).
  Source: `checkbox/root/CheckboxRoot.tsx:126,151`, `form/Form.tsx:41-69`; see [FFI-FOC-01](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "triggers native HTML validation on submit" (error shown; focus not asserted); `CheckboxGroup.test.tsx` "focuses the first checkbox when the field receives an error from Form".
  Port note 2026-10-23 (M2 track 6): the focus clause is asserted by `checkbox.spec.ts` "port: a required box blocks the submit of its form, which focuses the root"; the group clause by `checkbox-group.spec.ts` (CKG-FOC-02's titles that render no Field.Error).

### 6.4 Dismissal

Not applicable — Checkbox has no popup or dismissable surface.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onCheckedChange` | `(checked: boolean, eventDetails) => void` | A click reaches the hidden input and is not ignored (not read-only, not disabled, not pre-canceled), before the state updates | `'none'` | yes (`cancel()`) |
| `onClick`, `onFocus`, `onBlur`, `onKeyDown`, `onKeyUp`, … | standard DOM handler props forwarded to the root | per DOM | — | see CHK-PTR-09 / CHK-KEY-05 / CHK-KEY-11 |

The Indicator has no callbacks (there is no `onOpenChangeComplete` for it).

- **CHK-EVT-01** — MUST. `onCheckedChange(nextChecked, details)` MUST be called synchronously inside the hidden input's change handling, with `nextChecked` = the input's post-click `checked` property, before the component's state (and, in a group, the group's value) is updated.
  Source: `checkbox/root/CheckboxRoot.tsx:222-225`; see [EVD-EVT-02](../architecture/event-details-and-reasons.md). Test: [L2] `CheckboxRoot.test.tsx` "should call onCheckedChange when clicked" (called once with `true`).
- **CHK-EVT-02** — MUST. `details.reason` MUST always be `'none'`; keyboard, pointer and label activations are not distinguished.
  Source: `checkbox/root/CheckboxRoot.tsx:223,511`; see [EVD-EVT-28](../architecture/event-details-and-reasons.md) (reason matrix row). Test: none.
- **CHK-EVT-03** — MUST. `details.event` MUST be the native `click` event received by the hidden input (the component-dispatched click for root/keyboard activation; the browser's own click for label or direct-input activation), so that it carries the originating modifier-key state. `details.trigger` is `undefined`.
  Source: `checkbox/root/CheckboxRoot.tsx:223`, `internals/createBaseUIEventDetails.ts:138-155`. Test: [L4] `CheckboxRoot.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange".
- **CHK-EVT-04** — MUST. If the callback calls `details.cancel()`, the state MUST NOT change (in any ownership mode), the hidden input's `checked` property MUST be restored to the current state, no field side effects (dirty, filled, validation, error clearing) MUST occur, and no group value change MUST be requested. Server: relaxed, see §9.
  Source: `checkbox/root/CheckboxRoot.tsx:227-245`; see [EVD-EVT-05](../architecture/event-details-and-reasons.md), [EVD-EVT-08](../architecture/event-details-and-reasons.md). Test: [L2] `CheckboxRoot.test.tsx` "does not update its state when onCheckedChange cancels the event"; `useCheckboxGroupParent.test.tsx` "lets a child checkbox cancel a parent-enabled group change", "lets a parent checkbox cancel a parent-enabled group change".
- **CHK-EVT-05** — MUST. `onCheckedChange` MUST NOT be called when `checked` changes because the consumer changed the prop, on mount, or because the group's value changed through another box (a parent toggle MUST NOT call the members' `onCheckedChange`; a member toggle MUST NOT call the parent's).
  Source: `checkbox/root/CheckboxRoot.tsx:211-246` (only path to the callback is the own input's change). Test: [L2] `useCheckboxGroupParent.test.tsx` "should control child checkboxes" (`childCheckedChange` never called on parent clicks), "parent should be marked as mixed if some children are checked"; `CheckboxRoot.test.tsx` "revalidates when a controlled value changes externally" (implied).
- **CHK-EVT-06** — MUST. `onCheckedChange` MUST be called exactly once per user toggle, regardless of whether the toggle came from the root, keyboard, label or direct input click.
  Source: `checkbox/root/CheckboxRoot.tsx:225,250,364-375`. Test: [L2] `CheckboxRoot.test.tsx` "should call onCheckedChange when clicked" (`calls.length === 1`); `CheckboxGroup.test.tsx` "implicit association", "explicit association" (label click → one call) (L4 for the keyboard and label clauses).
- **CHK-EVT-07** — MUST. `onCheckedChange` MUST NOT be called while `readOnly` or disabled, nor for a pre-canceled input click (CHK-PTR-11).
  Source: `checkbox/root/CheckboxRoot.tsx:212-220,364-366`. Test: [L4] `CheckboxRoot.test.tsx` "ignores a hidden input click canceled before React handles it".
- **CHK-EVT-08** — MUST. `details.allowPropagation()` has no effect for this component (nothing stops propagation of the original user event; the internal input click is always stopped, CHK-PTR-03); `isPropagationAllowed` merely reflects whether it was called.
  Source: `internals/createBaseUIEventDetails.ts:144-152` (no consumer in Checkbox). Test: none.
- **CHK-EVT-09** — MUST. For a member or parent box inside a `CheckboxGroup`, the sequence per toggle MUST be: own `onCheckedChange` → (if not canceled) the group's `onValueChange` with the **same** details object → (if not canceled) own state commit and group value commit. A `cancel()` in the box's callback MUST prevent the group callback from being invoked at all; a `cancel()` in the group callback MUST prevent both commits.
  Source: `checkbox/root/CheckboxRoot.tsx:225-245`, `checkbox-group/useCheckboxGroupParent.ts:54-96,104-118`, `checkbox-group/CheckboxGroup.tsx:69-79`; see [EVD-EVT-04](../architecture/event-details-and-reasons.md), [EVD-EVT-09](../architecture/event-details-and-reasons.md). Test: [L2] `useCheckboxGroupParent.test.tsx` "lets a child checkbox cancel a parent-enabled group change" (`onValueChange` not called), "lets a parent checkbox cancel a parent-enabled group change"; `CheckboxGroup.test.tsx` "does not update the group when onValueChange cancels the event".

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md). This section states what a **standalone** box (not inside a `CheckboxGroup`) contributes; a grouped box delegates to the group (CHK-FRM-20, [checkbox-group](checkbox-group.md) §8).

**What a standalone box registers with the field.** Control element = the root (focused by `Form` when the field is first-invalid on submit); registration id = the root's generated id; value = `checked` (a boolean — this is what `validate(value, formValues)` receives and what `Form`'s `onFormSubmit` values contain under the field name); name = the `name` prop (used only when `Field.Root` has no `name`); registration is dropped while disabled. The hidden checkbox input is registered in the field's validation registry (without an identity), so native constraint validation (`required` → `valueMissing`) is read from it.

**Timing of field writes** (all post-commit, pre-paint after the commit that reflects the new `checked`):

1. `filled ← checked` (on mount and on every change).
2. On every change of `checked` (not on mount): clear the enclosing `Form`'s external error for the effective name; `dirty ← checked !== initialValue`; notify validation of the new value (`validation.change(checked)`).
3. On blur of the root (only if the hidden input exists): `touched ← true`; `focused ← false`; if the field's validation mode is `onBlur`, commit a full validation of the hidden input's current `checked`.

- **CHK-FRM-01** — MUST. When checked and an effective name exists, form submission MUST include one entry `name = value` where `value` is the `value` prop or `"on"` when undefined.
  Source: `checkbox/root/CheckboxRoot.tsx:197,201,258-260`; see [FFI-FRM-28](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "should include the checkbox value in form submission, matching native checkbox behavior", "should include the custom checkbox value in form submission, matching native checkbox behavior", "matches native checkbox form submission behavior".
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run on native forms in `checkbox.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-FRM-02** — MUST. When unchecked and `uncheckedValue` is undefined, form submission MUST include no entry for the name.
  Source: `checkbox/root/CheckboxRoot.tsx:400`. Test: [L4] `CheckboxRoot.test.tsx` "should include the checkbox value in form submission, matching native checkbox behavior" (`null`), "matches native checkbox form submission behavior" (`getAll` → `[]`).
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run on native forms in `checkbox.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-FRM-03** — MUST. When unchecked and `uncheckedValue` is defined, form submission MUST include exactly one entry `name = uncheckedValue`.
  Source: `checkbox/root/CheckboxRoot.tsx:400-402`. Test: [L4] `CheckboxRoot.test.tsx` "should submit uncheckedValue when checkbox is unchecked and uncheckedValue is specified", "should submit custom uncheckedValue when checkbox is unchecked", "submits uncheckedValue to an external form when unchecked".
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run on native forms in `checkbox.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-FRM-04** — MUST. When disabled, form submission MUST include no entry for the name in either state.
  Source: `checkbox/root/CheckboxRoot.tsx:198,401`. Test: [L4] `CheckboxRoot.test.tsx` "does not submit uncheckedValue when disabled".
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run on native forms in `checkbox.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **CHK-FRM-05** — MUST. With `form="<id>"`, both hidden inputs MUST be submitted with the `<form>` of that id even when rendered outside it.
  Source: `checkbox/root/CheckboxRoot.tsx:199,401`; see [FFI-DOM-08](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "submits to an external form when `form` is provided", "submits uncheckedValue to an external form when unchecked".
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run on native forms in `checkbox.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **CHK-FRM-06** — MUST. Within a `Field.Root` and outside a group, the box MUST register as the field's control with value = `checked`, control element = root, id = the root's generated id, and name = `name` prop; it MUST unregister while disabled and on unmount, and MUST NOT register while inside a `CheckboxGroup`.
  Source: `checkbox/root/CheckboxRoot.tsx:151`, `internals/field-register-control/useRegisterFieldControl.ts:21-45`; see [FFI-FRM-17](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "prop: validationMode=onSubmit" (field validated on submit), "validates once when changed by the user" (`validate` receives `true`).
- **CHK-FRM-07** — MUST. The field's dirty baseline (`initialValue`) MUST be the `checked` value at the box's first registration within that field and MUST NOT be re-captured on later changes.
  Source: `internals/field-register-control/useFieldControlRegistration.ts:86-103`; see [FFI-STA-04](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "[data-dirty]".
- **CHK-FRM-08** — MUST. With `required` and unchecked, submitting an enclosing `Form` MUST mark the field invalid with native `valueMissing` (`aria-invalid="true"` on the root; `Field.Error match="valueMissing"` rendered), MUST prevent the submit, and MUST focus the root.
  Source: `checkbox/root/CheckboxRoot.tsx:205`, `field/root/useFieldValidation.ts:194-223`, `form/Form.tsx:41-69,109-121`; see [FFI-FRM-04](../architecture/field-and-form-infrastructure.md), [FFI-FRM-06](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "triggers native HTML validation on submit", "prop: validationMode=onSubmit".
  Port note 2026-10-23 (M2 track 6, Form on main): "prop: validationMode=onSubmit" runs at L4 (`checkbox.spec.ts`, Form (track 2)), with the hidden input's state read in the submit task (FFI-FRM-04's dated note). The port title "a required box blocks the submit of its form, which focuses the root" covers "triggers native HTML validation on submit" without its `Field.Error match="valueMissing"` clause, which stays open to PR 1c.
- **CHK-FRM-09** — MUST. The field's `validate` function MUST be invoked with the boolean `checked` value exactly once per user toggle when on-change validation applies (mode `onChange`, or mode `onSubmit` after the first submit).
  Source: `checkbox/root/CheckboxRoot.tsx:184-193`, `field/root/useFieldValidation.ts:349-365`, `field/root/FieldRoot.tsx:88-92`; see [FFI-EVT-02](../architecture/field-and-form-infrastructure.md), [FFI-FRM-01](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "validates once when changed by the user", "props: validationMode=onChange". Docs: CHANGELOG.md v1.6.0 "Fix extra `validate` fn calls".
- **CHK-FRM-10** — MUST. A change of a consumer-owned `checked` that did not come from user interaction MUST trigger the same on-change validation as a user toggle (one additional `validate` call with the new value).
  Source: `checkbox/root/CheckboxRoot.tsx:184-193`, `internals/useValueChanged.ts:10-16`; see [UTL-STA-36](../architecture/shared-utilities.md). Test: [L2] `CheckboxRoot.test.tsx` "revalidates when a controlled value changes externally".
- **CHK-FRM-11** — MUST. In validation mode `onBlur`, blurring the root MUST run a full validation of the hidden input's current `checked` (native constraints then `validate`); changing the value alone MUST NOT mark the field invalid.
  Source: `checkbox/root/CheckboxRoot.tsx:316-318`; see [FFI-FRM-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "prop: validationMode=onBlur", "[data-valid]". Docs: CHANGELOG.md v1.8.0 "Fix controlled blur validation and stale filled state".
- **CHK-FRM-12** — MUST. In validation mode `onSubmit`, after the first submit every toggle MUST re-validate (so a required box flips between invalid and valid as it is toggled off and on).
  Source: `field/root/FieldRoot.tsx:88-92`, `field/root/useFieldValidation.ts:356-364`. Test: [L2] `CheckboxRoot.test.tsx` "prop: validationMode=onSubmit".
  Port note 2026-10-23 (M2 track 6): green at L2 (`CheckboxFormTests`) and L4 (`checkbox.spec.ts`).
- **CHK-FRM-13** — MUST. On every change of `checked`, any external `Form` error registered under the effective name MUST be cleared (removing `aria-invalid` and the `Field.Error` content).
  Source: `checkbox/root/CheckboxRoot.tsx:189`, `form/Form.tsx:143-155`; see [FFI-EVT-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "clears external errors on change".
  Port note 2026-10-23 (M2 track 6): green at L2 (`CheckboxFormTests`) for the `aria-invalid` clause; the `Field.Error` clause stays open to PR 1c.
- **CHK-FRM-14** — MUST. The field's `touched` state MUST become `true` on the root's blur, except when the hidden input is not mounted.
  Source: `checkbox/root/CheckboxRoot.tsx:307-314`. Test: [L2] `CheckboxRoot.test.tsx` "[data-touched]".
- **CHK-FRM-15** — MUST. A native form reset MUST NOT change the box's checked state, its `aria-invalid`, or its `aria-describedby`.
  Source: `checkbox/root/CheckboxRoot.tsx:136-144,403` (state is component-held, not read back from the input); see [FFI-FRM-30](../architecture/field-and-form-infrastructure.md). Test: none in Checkbox (`SwitchRoot.test.tsx` covers the shared mechanism).
- **CHK-FRM-16** — MUST. Within the box's labelable scope (`Field.Root`, or `Field.Item` when present), `Field.Label` MUST be associated with the box: its `for` MUST equal the hidden input's id when `nativeButton=false`, or the root's id when `nativeButton=true`, and the root's `aria-labelledby` MUST equal the label's id (subject to CHK-ARIA-06). Clicking a native `Field.Label` MUST toggle the box (wrapping or sibling). When an explicit `id` is removed, or an id-less box replaces one that had an explicit id, the control MUST fall back to a generated id and the label's `for` MUST follow in the same update; server-rendered markup MUST associate label and control through the scope's generated id, with the explicit `id` taking over after hydration.
  Source: `checkbox/root/CheckboxRoot.tsx:98-106,164-171,204,297`, `internals/labelable-provider/useLabel.ts:64-69`, `internals/labelable-provider/useLabelableId.ts:32-82`; see [FFI-ARIA-04](../architecture/field-and-form-infrastructure.md), [FFI-FRM-34](../architecture/field-and-form-infrastructure.md), [FFI-FRM-36](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "when label and checkbox are siblings", "sets `for` on the label", "drops an explicit id when the prop is removed (nativeButton=%s)", "does not reuse an unmounted Checkbox id for a keyed id-less Checkbox (nativeButton=%s)", "defers an explicit id until hydration but keeps Field.Label associated during SSR (nativeButton=%s)"; `CheckboxRoot.react17.test.tsx` same two titles. Docs: `components/checkbox/page.mdx` §Form integration; `handbook/forms/page.mdx` §Naming form controls; CHANGELOG.md v1.0.0-beta.0 "Support implicit `Field.Label`", v1.8.0 "Fix stale and duplicated control IDs".
- **CHK-FRM-17** — MUST. `Field.Description` and `Field.Error` ids MUST be appended to the root's `aria-describedby` (CHK-ARIA-10) and set on the hidden input (CHK-DOM-18).
  Source: `checkbox/root/CheckboxRoot.tsx:261,381`. Test: [L2] `CheckboxRoot.test.tsx` "Field.Description".
- **CHK-FRM-18** — MUST. When `Field.Root` has no `name`, a standalone box's `name` prop MUST serve as the field's name for `Form` error lookup and submitted values.
  Source: `checkbox/root/CheckboxRoot.tsx:151` (name passed to registration), `field/root/FieldRoot.tsx:61-62`; see [FFI-STA-10](../architecture/field-and-form-infrastructure.md). Test: none.
- **CHK-FRM-19** — MUST. A non-parent box MUST register its hidden input in the validation registry of the validation scope it belongs to (the enclosing group's when inside a `CheckboxGroup`, else the field's), with identity = `value ?? name` inside a group and no identity otherwise, for as long as the input is mounted; a parent box MUST never register its input. The registered input is what native validity (`required`, custom validity messages) is read from and written to.
  Source: `checkbox/root/CheckboxRoot.tsx:134,155-163`, `field/root/useFieldValidation.ts:108-116`; see [FFI-FRM-24](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "keeps a required error while another required checkbox in the group is unchecked", "clears custom validity from disabled registered inputs when the group becomes valid", "focuses the first child checkbox instead of a parent checkbox for Form errors".
- **CHK-FRM-20** — MUST. Inside a `CheckboxGroup` a box MUST NOT register as the field's control, MUST NOT write `filled`/`dirty`, MUST NOT clear form errors or notify validation on its own change, and MUST NOT render the `uncheckedValue` input; on blur in `onBlur` mode it MUST commit the **group's value** (not its own `checked`) to validation; `touched`/`focused` are written as for a standalone box. Everything else about group form integration is specified in [checkbox-group](checkbox-group.md) §8.
  Source: `checkbox/root/CheckboxRoot.tsx:151,178-193,316-318,400`. Test: [L2] `CheckboxGroup.test.tsx` "prop: validationMode=onBlur" (blur commits `[]` then `['three']`), "[data-dirty]", "[data-filled] follows the group value even without a matching rendered checkbox".
- **CHK-FRM-21** — MUST. A box inside a `Field.Item` MUST claim the item's labelable scope (its `Field.Label` points at this box, its `Field.Description` ids are appended after the `Field.Root` scope's ids) and MUST be disabled when the item is disabled.
  Source: `checkbox/root/CheckboxRoot.tsx:84,91-92,101-104`, `field/item/FieldItem.tsx:29-47`; see [FFI-STR-03](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxGroup.test.tsx` "implicit association", "explicit association", "links the group and individual checkboxes", "associates Field.Label with a grouped Checkbox during SSR (nativeButton=$nativeButton, parent=$parent)".
  Port note 2026-10-23 (ADR 02 decision 5, §16 Q10): generated ids are drawn eagerly, so `CheckboxRoot.react17.test.tsx`'s intermediate render without ids does not arise; its titles are translated once, by their end state.

## 9. Lifecycle, mounting and animation

Vocabulary (conventions §4): **post-commit, pre-paint**; **next frame**; **microtask**; **on animations finished**. Mechanism: [TRN](../architecture/transitions-and-mounting.md). The Indicator's "open" state is `rendered = checked || indeterminate` (effective values). It uses the default status machine (no idle state, no deferred ending, no initial-open animation) and a **batched** completion watcher armed only while not rendered.

State diagram (Indicator):

```
unmounted ──rendered=true──▶ mounted, 'starting' ──next frame──▶ mounted, (unset)
                                     ▲                                  │
                                     │ (reopen: no 'starting')          │ rendered=false
                                     │                                  ▼
                              mounted, 'ending' ◀───────────────────────┘
                                     │ on animations finished (first inspection next frame; sync when disabled)
                                     ▼
                              unmounted (element removed unless keepMounted)
```

- **CHK-ANI-01** — MUST. The Indicator element MUST be in the DOM if and only if `keepMounted || mounted`, where `mounted` becomes `true` in the commit that makes `rendered` true and `false` only after the exit of CHK-ANI-04 completes.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:28-30,58,67-71`; see [TRN-ANI-01](../architecture/transitions-and-mounting.md). Test: [L4] `CheckboxIndicator.test.tsx` "should not render indicator by default", "should render indicator when checked", "should keep indicator mounted when unchecked".
- **CHK-ANI-02** — MUST. An Indicator whose box is already checked or indeterminate at mount MUST NOT go through `'starting'` (no `data-starting-style`, no frame requested).
  Source: `internals/useTransitionStatus.ts:29`; see [TRN-STA-13](../architecture/transitions-and-mounting.md). Test: none.
- **CHK-ANI-03** — MUST. When `rendered` becomes `true` while unmounted, the element MUST appear with `data-starting-style=""` in that same commit, and the attribute MUST be removed on the next frame (one frame request, cancelled if the state changes first). Server: relaxed, see §9.
  Source: `internals/useTransitionStatus.ts:31-34,58-73`, `internals/stateAttributesMapping.ts:10-20`; see [TRN-STA-03](../architecture/transitions-and-mounting.md), [TRN-STA-04](../architecture/transitions-and-mounting.md), [TRN-DOM-01](../architecture/transitions-and-mounting.md). Test: [L4] `CheckboxIndicator.test.tsx` "triggers enter animation via data-starting-style when mounting".
  Port note 2026-10-23 (M2 track 5, design point C6, settled by the orchestrator 2026-10-23, on the `collapsible` module's precedent, ADR 04 Amendments 2026-10-22): an Indicator C# creates open is rendered with `data-starting-style` and attached with that status (the handover, ADR 04 Amendments 2026-10-18). On a fast Server circuit the render, the attach and the module's clearing frame landed before the frame that first styles the element, so no transition ran; the `transitions` module now computes the element's style when it writes or takes over the attribute (`js/transitions.test.ts` "computes the style of an element that shows data-starting-style before the frame that clears it").
- **CHK-ANI-04** — MUST. When `rendered` becomes `false` while mounted, the element MUST receive `data-ending-style=""` in that same commit and stay in the DOM until on animations finished (first inspection next frame), after which `mounted` becomes `false` in a synchronous pre-paint commit, `data-ending-style` is cleared, and the element is removed (unless `keepMounted`). With no finite animations the removal happens on the next frame plus a microtask; when the animations-disabled flag is set (or `getAnimations` is unavailable) it happens synchronously. Server: relaxed, see §9.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:39-49`, `internals/useOpenChangeComplete.tsx:9-28`, `internals/useAnimationsFinished.ts:93-99,161`, `internals/useTransitionStatus.ts:36-42`; see [TRN-STA-06](../architecture/transitions-and-mounting.md), [TRN-STA-08](../architecture/transitions-and-mounting.md), [TRN-ANI-03](../architecture/transitions-and-mounting.md), [TRN-ANI-11](../architecture/transitions-and-mounting.md), [TRN-ANI-15](../architecture/transitions-and-mounting.md), [TRN-DOM-02](../architecture/transitions-and-mounting.md). Test: [L4] `CheckboxIndicator.test.tsx` "applies data-ending-style before unmount", "should remove the indicator when there is no exit animation defined", "should remove the indicator when the animation finishes".
- **CHK-ANI-05** — MUST. Exit completions of several Indicators that become ready in the same microtask MUST be applied in a single synchronous commit (no intermediate commit in which only some of them are removed). Server: relaxed, see §9.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:40`, `internals/useAnimationsFinished.ts:11-34,85-90`; see [TRN-ANI-10](../architecture/transitions-and-mounting.md). Test: [L4] `CheckboxIndicator.test.tsx` "removes all indicators in a single commit when multiple checkboxes are unchecked".
  Port note 2026-10-23 (M2 track 5, design point C1): each Indicator reports to its own `transitions` twin and the module's close watchers stay unbatched. The animations of indicators unchecked together finish in one frame, so their completions, their convergent `hidden` writes and, on WebAssembly, their C# renders all land before the same paint: `checkbox.spec.ts` "removes all indicators in a single commit when multiple checkboxes are unchecked" records every removal in one `MutationObserver` callback on WebAssembly, and every `hidden` write in one callback on Server (the TRN-ANI-10 row).
- **CHK-ANI-06** — MUST. The completion watcher MUST be armed only while `rendered` is `false`; on the enter side the element's animations MUST NOT be inspected (no `getAnimations` call).
  Source: `checkbox/indicator/CheckboxIndicator.tsx:41-42`, `internals/useOpenChangeComplete.tsx:15-27`; see [TRN-ANI-17](../architecture/transitions-and-mounting.md), [TRN-ANI-18](../architecture/transitions-and-mounting.md). Test: [L3] `CheckboxIndicator.test.tsx` "triggers enter animation via data-starting-style when mounting" (`getAnimations` spy not called).
  Port note 2026-10-23 (the Avatar precedent on AVA-ALG-02): the Indicator attaches its `transitions` twin with `ReportOpenComplete` false, so the module never inspects the enter animations; `js/transitions.test.ts` "never inspects the animations of an open element unless the open completion is asked for" is the L3 evidence, green on `main`, so no test was added.
- **CHK-ANI-07** — MUST. With `keepMounted`, the element MUST stay in the DOM while not mounted, MUST NOT receive `hidden` or any inline style, and MUST still carry `data-ending-style` during the exit phase (so consumer animations run and their end events fire) before it is cleared.
  Source: `checkbox/indicator/CheckboxIndicator.tsx:58,67-69`; see [TRN-DOM-07](../architecture/transitions-and-mounting.md), [TRN-API-02](../architecture/transitions-and-mounting.md). Test: [L4] `CheckboxIndicator.test.tsx` "should keep indicator mounted when unchecked", "should remove the indicator when the animation finishes" (`onAnimationEnd` observed with `keepMounted`). Docs: CHANGELOG.md v1.2.0 "Allow exit animations on `<Checkbox.Indicator>` when `keepMounted={false}`", v1.0.0-alpha.6 "Avoid applying `hidden` attr when `keepMounted=true` for indicators".
- **CHK-ANI-08** — MUST. If `rendered` becomes `true` again during the exit phase, the pending completion MUST be aborted, the element MUST NOT go through `'starting'`, and `data-ending-style` MUST be cleared on the next frame. Server: relaxed, see §9.
  Source: `internals/useOpenChangeComplete.tsx:20-26`, `internals/useTransitionStatus.ts:58-73`; see [TRN-STA-09](../architecture/transitions-and-mounting.md), [TRN-ANI-18](../architecture/transitions-and-mounting.md). Test: none.
- **CHK-ANI-09** — MUST. The root element and both hidden inputs have no mount/unmount lifecycle: they are rendered for the component's whole lifetime (the `uncheckedValue` input per CHK-DOM-19).
  Source: `checkbox/root/CheckboxRoot.tsx:397-405`. Test: none.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- CHK-ANI-03 (TRN-STA-04 row) — met on both modes: the next-frame removal of `data-starting-style` is JS-owned with C# mirroring; listed so nobody moves it back to C#.
- CHK-ANI-08 (TRN-STA-09 row) — met on both modes for the next-frame clear of `data-ending-style`, for the same reason.
- CHK-ANI-04 (TRN-ANI-03 row) — the Indicator's removal lands in the first render batch after the completion message, about two round trips later; the optimistic convergent write lets JS set `hidden` at animation end so the element is not visible meanwhile.
- CHK-ANI-05 (TRN-ANI-10 row) — the batched removal of several Indicators lands in the first render batch after the completion message, about two round trips later (same convergent write).
- CHK-STA-02 (RND-STA-01 row, mirrored field `transitionStatus`) — `data-starting-style`/`data-ending-style` are JS-written at frame timing; the Indicator's C# `transitionStatus` (and so `ClassValue`/`StyleValue`) updates one round trip later.
- CHK-PTR-09, CHK-KEY-11 (EVD-EVT-26 against button handling; for CHK-KEY-11 also the UBT-KEY-03/04 row) — a C# consumer's `PreventBaseUIHandler()` is synchronous on WebAssembly through `decide()`; on Server the press or key is handled regardless (fallback "run"); a consumer JavaScript listener's `preventDefault()` is honoured on both modes.
- CHK-STA-10, CHK-ARIA-12 (FFI registration and post-commit, pre-paint C# state work) — runs after paint plus a round trip; observable only as latency of the dependent `data-filled` / `aria-controls` updates.
- CHK-DOM-13 — row "JS-owned values that originate in C# state" (2026-10-23): met on both modes. The `dom` primitive sets `indeterminate` on attach, on every `update` and inside every native click, which clears it; a change of C#'s effective value reaches the property with the `update`, one round trip after the render on Server.
- CHK-EVT-04, CHK-STA-05 (the restore of the hidden input's `checked`) — row "C#-origin consumer vetoes of a toggle on a hidden input" (2026-10-22, M2 track 4): met on WebAssembly, where the `toggle` decision reverts the box inside the click; on Server the visually hidden input stays as the click left it for about two round trips (SWT-EVT-04's measurement), while the root's `aria-checked`, its data attributes and the Indicator never change.
- CHK-ARIA-08 — row "FFI registration" (2026-10-23, M2 track 5, design point C4, settled by the orchestrator 2026-10-23): the `form` module looks for the hidden input's native label after every change of the document's labels and ids and reports the label's id; C# renders it as `aria-labelledby`, a round trip after the change on Server.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (Root) | [RND](../architecture/rendering-and-composition.md) | default tag `span`; props merged in order: built-in root props → consumer props → group-derived props (`aria-controls` for a parent) → button-behavior props (getter receiving the merged props) → labelable description props (getter) → field validation props (getter); three DOM handles merged (button handle, internal control handle, consumer); state attribute mapping = `checked` → `data-checked`/`data-unchecked`/nothing-when-indeterminate + field validity mapping; the rendered id is read back for child registration |
| `useRenderElement` (Indicator) | RND | default tag `span`; Root mapping + transition-status mapping; consumer props only; two handles (consumer, internal) |
| `getStateAttributesProps` | RND | booleans → `data-<lowercase key>=""` (this yields `data-indeterminate`); custom mapping for `checked` and `valid` |
| `useButton` | [UBT](../architecture/button-behavior.md) | `{ disabled: effectiveDisabled, native: nativeButton }`; `focusableWhenDisabled` unset (→ `false`), `tabIndex` default `0`, `composite` inferred (never composite in its own tests); the component's own `onKeyDown` suppresses the mechanism's Enter activation (UBT-KEY-06) |
| `useFocusableWhenDisabled` (via `useButton`) | UBT | non-native: `tabindex` `0`/`-1`, `aria-disabled`; native: `tabindex="0"`, `disabled` attribute |
| `dispatchClickWithModifiers` | UBT | called with the hidden input and the root click (default `detail: 0`, `pointerType: ''`) |
| `getDefaultFormSubmitter` | [FFI](../architecture/field-and-form-infrastructure.md) (FFI-ALG-10) | called with the hidden input's `form` after an Enter keydown, in a microtask |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | reason `'none'`, event = native input click, no trigger, no custom fields; the same object is passed on to the group |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `{ controlled: <per CHK-STA-07/08, else checked>, default: defaultChecked, name: 'Checkbox', state: 'checked' }` |
| `useBaseUiId` / `useId` | UTL / RND | one generated id for the root (non-native) and field registration; `"<input id>-label"` for an auto-labelled native `<label>` |
| `visuallyHidden` / `visuallyHiddenInput` | UTL | hidden input style chosen by presence of effective name |
| `useValueChanged` | UTL | watches `checked`; runs error clearing, dirty, validation change — skipped inside a group |
| `useTransitionStatus`, `useOpenChangeComplete`, `useAnimationsFinished` | [TRN](../architecture/transitions-and-mounting.md) | Indicator: `useTransitionStatus(rendered)` with all options default; `useOpenChangeComplete({ batch: true, enabled: !rendered, open: rendered, ref: indicator, onComplete: setMounted(false) if !rendered })` |
| `useFieldRootContext`, `useFieldItemContext`, `useSetFieldFocused`, `useRegisterFieldControl`, `useFormContext` | FFI | see §8: registration `(controlHandle, generatedId, checked, undefined, !groupContext && !disabled, nameProp)`; focus target = root; `clearErrors(effectiveName)`; `setFilled(checked)`; `setDirty(checked !== initialValue)`; `validation.change(checked)`; `validation.commit(group ? groupValue : input.checked)` on blur in `onBlur` mode; `validation.getValidationProps(disabled, props)` on root and hidden input; `validation.registerInput(input, { controlRef, value: group ? identity : undefined })` unless parent; `validation` = group's when grouped else the field's |
| `useLabelableContext`, `useLabelableId`, `useAriaLabelledBy` | FFI | `useLabelableId({ id: idProp || undefined, enabled: ownsControlId })` where `ownsControlId` = "not sharing the labelable scope with an enclosing group"; `useAriaLabelledBy(ariaLabelledByProp, labelId, hiddenInputHandle, enableFallback = !nativeButton, controlId, ariaLabel)`; `getDescriptionProps` applied to root and hidden input |
| `useCheckboxGroupContext` | [CKG](checkbox-group.md) | reads `value`, `setValue`, `allValues`, `parent` (getParentProps / getChildProps / registerChildId / disabledStates), `disabled`, `validation`, `registerControlId` |
| `mergeProps` | RND | consumer handlers run before built-in ones; `preventBaseUIHandler()` suppresses the built-in handler |

## 12. Edge cases, invariants and dev warnings

All messages below are emitted only in non-production builds, through `console.error`, prefixed `Base UI: `, and each distinct message is logged at most once per page lifetime ([UTL-EDGE-11](../architecture/shared-utilities.md), [UTL-EDGE-12](../architecture/shared-utilities.md)).

- **CHK-EDGE-01** — MUST. Rendering `Checkbox.Indicator` outside a `Checkbox.Root` MUST throw an error with the message `Base UI: CheckboxRootContext is missing. Checkbox parts must be placed within <Checkbox.Root>.`
  Source: `checkbox/root/CheckboxRootContext.ts:11-15`. Test: [L2] `CheckboxIndicator.test.tsx` "throws a descriptive error when rendered outside <Checkbox.Root>".
  Port note 2026-10-23: the port's words, thrown in every build: ``Blazix: The `CheckboxRoot` context is missing. Checkbox parts must be placed within `<CheckboxRoot>`.`` (the Switch precedent, SWT-EDGE-01).
- **CHK-EDGE-02** — SHOULD. If the ownership input of `checked` (CHK-STA-03) switches between `undefined` and a boolean after the first render, the dev error of [UTL-EDGE-22](../architecture/shared-utilities.md) SHOULD be logged with `name: 'Checkbox'`, `state: 'checked'`. (For a group member the ownership input is the group membership boolean, which is never `undefined`.)
  Source: `checkbox/root/CheckboxRoot.tsx:142-143`, `packages/utils/src/useControlled.ts:48-64`. Test: none.
- **CHK-EDGE-03** — SHOULD. If `defaultChecked` changes after the first render of a component-owned box, the dev error of [UTL-EDGE-23](../architecture/shared-utilities.md) SHOULD be logged with `name: 'Checkbox'`, `state: 'checked'`. The rendered state is unaffected.
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: none.
- **CHK-EDGE-04** — SHOULD. If `nativeButton` is `true` but the rendered root is not a `<button>`, or `false` but it is a `<button>`, the dev errors of [UBT-EDGE-01](../architecture/button-behavior.md) / [UBT-EDGE-02](../architecture/button-behavior.md) SHOULD be logged.
  Source: `internals/use-button/useButton.ts:36-66`. Test: none in Checkbox.
- **CHK-EDGE-05** — MUST. Calling `focus()`, `blur()` and `click()` on the root element before the hidden input is attached (e.g. from a DOM-handle callback during mount) MUST NOT throw and MUST leave the box unchecked.
  Source: `checkbox/root/CheckboxRoot.tsx:308-311,370-373`. Test: [L4] `CheckboxRoot.test.tsx` "tolerates imperative interaction in its ref callback before the hidden input mounts".
- **CHK-EDGE-06** — MUST. `readOnly` MUST NOT affect form submission, focusability, `tabindex`, `data-disabled`, or <kbd>Enter</kbd> submission; it only blocks state changes from user input and adds `aria-readonly`/`data-readonly`.
  Source: `checkbox/root/CheckboxRoot.tsx:217,300,364`. Test: [L4] `CheckboxRoot.test.tsx` "submits the form with Enter when readOnly".
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, the incidental case; settled by the orchestrator 2026-10-23): re-cited under conventions §3.5. Base UI's test renders `Form` only as the enclosing form, which takes no part in what this row states; the titles run on a native `<form>` in `checkbox.spec.ts`, and the row is green at its layer.
- **CHK-EDGE-07** — MUST. A consumer `aria-label` consisting only of whitespace (`''`, `' '`, `'\n\t '`) MUST be treated as absent for the purpose of CHK-ARIA-06 (the label association is kept), while still being forwarded as an attribute.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:18`. Test: [L2] `CheckboxRoot.test.tsx` "ignores a blank `aria-label` (%j)".
- **CHK-EDGE-08** — MUST. Several standalone boxes inside one `Field.Root` without a `CheckboxGroup` are not a supported configuration; the last registered control owns the field's value, and only the focus-owning box clears `focused`. A port MUST at least reproduce the single-box behavior; use `CheckboxGroup` for several boxes.
  Source: `internals/field-root-context/useSetFieldFocused.ts:7-14`, `internals/field-register-control/useFieldControlRegistration.ts:147-168`; see [FFI-STR-07](../architecture/field-and-form-infrastructure.md). Test: none.
- **CHK-EDGE-09** — MUST. No behavior depends on writing direction (RTL) or locale.
  Source: `checkbox/root/CheckboxRoot.tsx:1-35` (no direction or locale import). Test: none.
- **CHK-EDGE-10** — MUST. Server-rendered markup MUST contain the same root/hidden-input structure and attributes as the client's initial render for the initial state, with deterministic ids, except: an explicit `id` prop inside a labelable scope is not applied until hydration (the scope's generated id is used on both label and control meanwhile, CHK-FRM-16), and a parent box's `aria-controls` is absent on the server (CHK-ARIA-12). An implementation whose generated ids become available only after the first render MUST render no `id`/`for`/`aria-controls` derived from them until then, and MUST establish the associations once they exist.
  Source: `checkbox/root/CheckboxRoot.tsx:389-395,403` (`suppressHydrationWarning` on the input only), `internals/labelable-provider/useLabelableId.ts:79-82`; see [FFI-FRM-34](../architecture/field-and-form-infrastructure.md). Test: [L4] `CheckboxRoot.test.tsx` "defers an explicit id until hydration but keeps Field.Label associated during SSR (nativeButton=%s)"; `CheckboxRoot.react17.test.tsx` "assigns the label association once the fallback ids arrive (nativeButton=%s)", "wires parent aria-controls once the fallback ids are assigned (nativeButton=%s)"; `CheckboxGroup.test.tsx` "points parent aria-controls at the children once they register (nativeButton=%s)".
  Port note 2026-10-23 (ADR 01 Amendments 2026-10-22; ADR 02 decision 5): the explicit `id` is a control-id claim registered after the render, so the prerendered markup associates `Field.Label` and the control through the field's generated id and the explicit `id` takes over once the page is interactive, as the pinned implementation does (`checkbox.spec.ts` "defers an explicit id until hydration but keeps Field.Label associated during SSR (nativeButton=%s)"). Generated ids are drawn eagerly, so `CheckboxRoot.react17.test.tsx`'s intermediate render without ids does not arise in the port (§16 Q10).
- **CHK-EDGE-11** — MUST. A `parent` box outside any `CheckboxGroup`, or inside a group without `allValues`, MUST behave as a standalone box for its checked state (CHK-STA-12) while still carrying `data-parent`, having no `name` on its hidden input, not registering its input, and never rendering the `uncheckedValue` input; it MUST NOT throw. (The docs say the prop "must be used in a Checkbox Group"; the source tolerates misuse.)
  Source: `checkbox/root/CheckboxRoot.tsx:88-89,109-115,163,201,400`. Test: none.
- **CHK-EDGE-12** — MUST. A detached box (no identity) inside a group MUST still receive an id on its hidden input (`nativeButton=false`) or root (`nativeButton=true`), and a parent toggle MUST NOT change it.
  Source: `checkbox/root/CheckboxRoot.tsx:101-106`. Test: [L2] `CheckboxRoot.test.tsx` "assigns an input id to a valueless child in a parent checkbox group", "assigns a root id to a valueless native button in a parent checkbox group"; `useCheckboxGroupParent.test.tsx` "does not select a child without an identifying value".
- **CHK-EDGE-13** — MUST. The accessible name MUST track runtime changes without a remount: adding a non-blank `aria-label` names the box by it; blanking or removing it restores the associated label's name; unmounting the associated label (native or `Field.Label`) removes `aria-labelledby`; remounting it restores the association — for both a native sibling `<label htmlFor>` and a `Field.Label`.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:17-35`, `utils/useRegisteredLabelId.ts:12-17`; see [FFI-ARIA-08](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` "updates the accessible name when `aria-label` is added, cleared, or removed", "updates the accessible name when the associated label mounts and unmounts" (`dynamic native label` and `dynamic Field.Label label`) (L4 for the native-label clauses).

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND; `Field.Label` `nativeLabel` mismatch errors from FFI-EDGE-02.

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. New IDs are introduced only where no earlier requirement captured the assertion.

### `CheckboxRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, `testRenderPropWith: 'span'`, `button: true`) | CHK-DOM-20 |
| "sets the correct aria attributes" | CHK-ARIA-01, CHK-ARIA-02, CHK-ARIA-04, CHK-API-10 |
| "can override the built-in attributes" | CHK-DOM-09 |
| "drops an explicit id when the prop is removed (nativeButton=%s)" / "does not reuse an unmounted Checkbox id for a keyed id-less Checkbox (nativeButton=%s)" (2 variants each) | CHK-FRM-16, CHK-DOM-21 |
| "defers an explicit id until hydration but keeps Field.Label associated during SSR (nativeButton=%s)" (2 variants) | CHK-FRM-16, CHK-EDGE-10, CHK-DOM-21 |
| "propagates a single click event to ancestors per user click (nativeButton=%s)" (2 variants) | CHK-PTR-03 |
| "does not propagate to ancestors when stopPropagation() is called (nativeButton=%s)" (2 variants) | CHK-PTR-09 |
| "tolerates imperative interaction in its ref callback before the hidden input mounts" | CHK-EDGE-05 |
| "should change its state when clicked" | CHK-PTR-01, CHK-STA-04, CHK-ARIA-02, CHK-DOM-12 |
| "should update its state when changed from outside" | CHK-STA-05 |
| "should call onCheckedChange when clicked" | CHK-EVT-01, CHK-EVT-06 |
| "does not update its state when onCheckedChange cancels the event" | CHK-EVT-04 |
| "should report keyboard modifier event properties when calling onCheckedChange" | CHK-EVT-03, CHK-PTR-01 |
| "should update its state if the underlying input is toggled" (both) | CHK-PTR-04 |
| "ignores a hidden input click canceled before React handles it" | CHK-PTR-11, CHK-EVT-07 |
| "can be activated with Space key" | CHK-KEY-01, CHK-FOC-01, CHK-KEY-08 |
| "does not activate with Enter key" | CHK-KEY-03 |
| "uses aria-disabled instead of HTML disabled" | CHK-ARIA-05 |
| "should not change its state when clicked" (disabled) | CHK-PTR-08 |
| "should have the `aria-readonly` attribute" / "should not have the aria attribute when `readOnly` is not set" | CHK-ARIA-03 |
| "should not change its state when clicked" (readOnly) / "should not change its state when its label is clicked" | CHK-PTR-07 |
| "should set the `aria-checked` attribute as \"mixed\"" / "should not have the aria attribute when `indeterminate` is not set" | CHK-ARIA-02, CHK-API-04 |
| "should not change its state when clicked" (indeterminate) | CHK-STA-09, CHK-PTR-12 |
| "should not be overridden by `checked` prop" | CHK-STA-09 |
| "sets the native input state when indeterminate" | CHK-DOM-13 |
| "keeps the native input state when checked changes while indeterminate remains" | CHK-DOM-13, CHK-PTR-12, CHK-DOM-12 |
| "sets indeterminate style hooks on the root and indicator" | CHK-DOM-03, CHK-DOM-08 |
| "sets grouped parent aria when manually indeterminate" | CHK-STA-08 |
| "sets grouped parent native input state when manually indeterminate" | CHK-DOM-13 |
| "should place the style hooks on the root and the indicator" | CHK-DOM-02, CHK-DOM-04, CHK-DOM-08, CHK-STA-02 |
| "should set the name attribute only on the input" | CHK-API-08, CHK-DOM-17 |
| "should toggle the checkbox when a wrapping <label> is clicked" | CHK-PTR-05 |
| "should toggle the checkbox when a explicitly linked <label> is clicked" | CHK-PTR-06, CHK-DOM-15 |
| "should associate `id` with the native button when `nativeButton=true`" | CHK-DOM-01, CHK-DOM-15, CHK-PTR-06 |
| "falls back to the Field control id when id is empty" | CHK-API-14, CHK-DOM-15 |
| "assigns an input id to a valueless child in a parent checkbox group" / "assigns a root id to a valueless native button in a parent checkbox group" | CHK-EDGE-12, CHK-DOM-15, CHK-DOM-01 |
| "triggers native HTML validation on submit" | CHK-FRM-08 |
| "clears external errors on change" | CHK-FRM-13 |
| "should include the checkbox value in form submission, matching native checkbox behavior" | CHK-FRM-01, CHK-FRM-02 |
| "submits the form when Enter is pressed while focused" | CHK-KEY-04 |
| "does not submit the form with Enter when the consumer prevents default" / "does not submit the form with Enter when an ancestor prevents default" | CHK-KEY-05, CHK-KEY-03 |
| "submits the form with Enter when readOnly" | CHK-KEY-06 |
| "submits the form once with Enter when rendered as a native button" | CHK-KEY-03, CHK-KEY-04 |
| "does not submit the form with Enter when there is no submit button" / "does not submit the form with Enter when the default button is disabled" | CHK-KEY-04 |
| "submits to an external form when `form` is provided" | CHK-FRM-05 |
| "submits to an external form with Enter when `form` is provided" | CHK-KEY-04, CHK-API-09 |
| "submits uncheckedValue to an external form when unchecked" | CHK-FRM-03, CHK-FRM-05 |
| "should include the custom checkbox value in form submission, matching native checkbox behavior" | CHK-FRM-01, CHK-DOM-16 |
| "matches native checkbox form submission behavior" | CHK-FRM-01, CHK-FRM-02 |
| "should submit uncheckedValue when checkbox is unchecked and uncheckedValue is specified" / "should submit custom uncheckedValue when checkbox is unchecked" | CHK-FRM-01, CHK-FRM-03 |
| "does not submit uncheckedValue when disabled" | CHK-FRM-04 |
| "should receive disabled prop from Field.Root" | CHK-API-11 |
| "should receive name prop from Field.Root" | CHK-API-08 |
| "[data-touched]" | CHK-FRM-14, CHK-DOM-05 |
| "[data-dirty]" | CHK-STA-11 |
| "adds [data-filled] attribute when checked after being initially unchecked" / "removes [data-filled] attribute when unchecked after being initially checked" / "clears [data-filled] when a controlled checkbox remounts unchecked" | CHK-STA-10 |
| "adds [data-filled] attribute when any checkbox is filled when inside a group" | CHK-STA-10, CHK-DOM-05 |
| "[data-focused]" / "does not set [data-focused] when disabled" | CHK-FOC-04 |
| "is removed when the focused checkbox becomes disabled" / "is removed when the focused checkbox unmounts" / "is kept when the checkbox is focused during mount in StrictMode" | CHK-FOC-05 |
| "[data-invalid]" / "[data-valid]" | CHK-DOM-06 |
| "[data-valid]" (blur after check in onBlur mode) | CHK-FRM-11 |
| "prop: validationMode=onSubmit" | CHK-FRM-08, CHK-FRM-12, CHK-ARIA-11, CHK-DOM-02 |
| "props: validationMode=onChange" | CHK-FRM-09 |
| "validates once when changed by the user" | CHK-FRM-09 |
| "revalidates when a controlled value changes externally" | CHK-FRM-10 |
| "prop: validationMode=onBlur" | CHK-FRM-11 |
| "when label and checkbox are siblings" / "sets `for` on the label" | CHK-FRM-16, CHK-ARIA-06 |
| "Field.Description" | CHK-ARIA-10, CHK-FRM-17 |
| "should change state when clicking the checkbox if it has a wrapping label" | CHK-PTR-02 |
| "sets `aria-labelledby` from a sibling label associated with the hidden input" | CHK-ARIA-07 |
| "updates fallback `aria-labelledby` when the hidden input id changes" | CHK-ARIA-08, CHK-DOM-22 |
| "prefers `aria-label` over an associated label" | CHK-ARIA-06 |
| "ignores a blank `aria-label` (%j)" (3 variants: `""`, `" "`, `"\n\t "`) | CHK-EDGE-07, CHK-ARIA-07 |
| "updates the accessible name when `aria-label` is added, cleared, or removed" / "updates the accessible name when the associated label mounts and unmounts" (`describe.each` "dynamic %s label": native / Field.Label; 2 variants each) | CHK-EDGE-13, CHK-ARIA-08 |
| "can render a native button" | CHK-KEY-02, CHK-KEY-03, CHK-FOC-01, CHK-API-15 |

### `CheckboxRoot.react17.test.tsx`

| Test | Requirements |
|---|---|
| "drops an explicit id when the prop is removed (nativeButton=%s)" / "does not reuse an unmounted Checkbox id for a keyed id-less Checkbox (nativeButton=%s)" (2 variants each) | CHK-FRM-16, CHK-DOM-21 |
| "assigns the label association once the fallback ids arrive (nativeButton=%s)" (2 variants) | CHK-EDGE-10, CHK-FRM-21 |
| "wires parent aria-controls once the fallback ids are assigned (nativeButton=%s)" (2 variants) | CHK-ARIA-12, CHK-EDGE-10 |

### `CheckboxIndicator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, rendered inside a provided root state) | CHK-DOM-20 |
| "throws a descriptive error when rendered outside <Checkbox.Root>" | CHK-EDGE-01, CHK-STR-02 |
| "should not render indicator by default" / "should render indicator when checked" | CHK-ANI-01 |
| "should keep indicator mounted when unchecked" / "should keep indicator mounted when checked" / "should keep indicator mounted when indeterminate" | CHK-API-18, CHK-ANI-07, CHK-API-04 |
| "should remove the indicator when there is no exit animation defined" | CHK-ANI-04 |
| "should remove the indicator when the animation finishes" | CHK-ANI-04, CHK-ANI-07 |
| "triggers enter animation via data-starting-style when mounting" | CHK-ANI-03, CHK-ANI-06 |
| "applies data-ending-style before unmount" | CHK-ANI-04 |
| "removes all indicators in a single commit when multiple checkboxes are unchecked" | CHK-ANI-05 |

### Additional requirements introduced here

- **CHK-DOM-20** — MUST. Both parts MUST pass the shared conformance suite from RND ([RND-DOM-36](../architecture/rendering-and-composition.md) … [RND-DOM-48](../architecture/rendering-and-composition.md)): custom attributes forwarded to the default element and to a substituted element (function and element forms, with `nativeButton={false}` passed for Root); `style` from the component and from the substituted element both applied; DOM handle resolves to an `HTMLSpanElement`; substituted element may be wrapped; component and substituted-element DOM handles both receive the element; `className` string and function merge with the substituted element's class; string `className` applied. For Root, `testRenderPropWith` is `span`, so the suite's extra "non-native button" pass (RND-DOM-49) does not run.
  Source: `test/describeConformance.tsx:55-76`, `test/conformanceTests/propForwarding.tsx:22-101`, `test/conformanceTests/renderProp.tsx:40-160`, `test/conformanceTests/refForwarding.tsx:13-26`, `test/conformanceTests/className.tsx:14-25`. Test: [L2] `CheckboxRoot.test.tsx` / `CheckboxIndicator.test.tsx` "Base UI component API".
- **CHK-DOM-21** — MUST. Inside a `Field.Root`, when the `id` prop is removed at runtime, or when a keyed, id-less box replaces a box that had an explicit id, the labelable control (hidden input, or root when `nativeButton`) MUST end up with a non-empty generated id different from the former explicit id, and `Field.Label`'s `for` MUST equal that id in the same update — with and without legacy (post-mount) id generation.
  Source: `internals/labelable-provider/useLabelableId.ts:15-17,40-50`, `internals/labelable-provider/LabelableProvider.tsx:26-66`; see [FFI-FRM-36](../architecture/field-and-form-infrastructure.md). Test: [L2] `CheckboxRoot.test.tsx` and `CheckboxRoot.react17.test.tsx` "drops an explicit id when the prop is removed (nativeButton=%s)", "does not reuse an unmounted Checkbox id for a keyed id-less Checkbox (nativeButton=%s)".
  Port note 2026-10-23 (ADR 02 decision 5, §16 Q10): generated ids are drawn eagerly, so `CheckboxRoot.react17.test.tsx`'s intermediate render without ids does not arise; its titles are translated once, by their end state.
- **CHK-DOM-22** — MUST. When the `id` prop changes at runtime (`nativeButton=false`), the hidden input's `id` MUST change accordingly and the root's fallback `aria-labelledby` MUST switch to the label now associated with the new id, assigning that label an id if it has none; the previously assigned label id is left in place.
  Source: `checkbox/root/CheckboxRoot.tsx:104,204`, `internals/labelable-provider/useAriaLabelledBy.ts:26-35,46-48`. Test: [L2] `CheckboxRoot.test.tsx` "updates fallback `aria-labelledby` when the hidden input id changes" (L4 for the native-label fallback clause).

## 14. React-specific notes and porting considerations

Non-normative.

- **Change detection via the native input.** React's `onChange` for a checkbox fires from the input's `click` event; Base UI reads `event.currentTarget.checked` after the browser has flipped it. A port that does not drive a real `<input type="checkbox">` must compute `next = !checked` itself and construct an equivalent event object (carrying modifier keys) for `details.event`, and must re-assert the "indeterminate" display state after each toggle (CHK-DOM-13) because a native click clears it. Port: see decisions/05-state-store-and-event-details.md §2 (request sequence for the hidden input's `change`/`click`, C#-rendered input) and decisions/04-js-interop-boundary.md §3 (`details.event` snapshot record).
- **Pre-canceled clicks.** The `nativeEvent.defaultPrevented` guard works around React issue 9023 (React fires `onChange` even for a click whose default was prevented, in which case the browser reverts the checkbox). A port should simply ignore canceled clicks (CHK-PTR-11). Port: see decisions/04-js-interop-boundary.md §3 (the event snapshot's `DefaultPrevented` field).
- **Controlled-input restoration.** After `cancel()` or in `readOnly` mode, React restores the hidden input's `checked` to the prop value as part of its controlled-input machinery (`readOnly` additionally calls `preventDefault()` on the change/click so the browser reverts). A port must explicitly re-sync the hidden input (CHK-EVT-04, CHK-PTR-07). Port: see decisions/05-state-store-and-event-details.md §2 (cancel stops the request).
- **Enter handling.** The keydown handler temporarily replaces `preventDefault` on both the synthetic and native event to detect a later `preventDefault()` by ancestors, calls the *native* `preventDefault` directly (so React's synthetic `defaultPrevented` stays `false` for ancestors), and finishes in a microtask queued on the element's window. A port needs: cancel the native default of the keydown without marking the event handled for ancestor handlers; observe whether any handler cancels the event during propagation; then click the default submitter after the current task's handlers have run (CHK-KEY-03..05). Port: see decisions/01-hosting-model-and-timing.md §Decision item 3 (C2: the microtask exists only in JavaScript) and decisions/04-js-interop-boundary.md §3 (`decide()` for the consumer/ancestor `preventDefault()` veto; Server falls back per the EVD-EVT-26 row of decisions/01-hosting-model-and-timing.md §Relaxation table). ADR 04 §1 does not name the module that owns this Enter path (see §16 item 12).
- **Group ownership through `useControlled`.** In a group the "controlled" input to the controlled/uncontrolled helper is the membership boolean rather than the `checked` prop, so the helper always runs in consumer-owned mode and the local setter is a no-op; the real commit is the group's value change. The `defaultChecked` default of `false` is also what keeps the helper's return type a defined boolean (CHANGELOG v1.4.0 "Fix uncontrolled default initialization"). Port: see decisions/05-state-store-and-event-details.md §1 and §2 (`Controlled<T>`, controlled iff supplied at first parameter set).
- **`value` only when defined.** React < 19 writes an empty `value=""` attribute when `undefined` is passed explicitly, so the source spreads `value` conditionally. Ports should just omit the attribute when undefined (CHK-DOM-16). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-19/32 explicit `undefined`).
- **`suppressHydrationWarning` on the hidden input** silences React's hydration diff for browser-managed input state; not observable, but server markup must match (CHK-EDGE-10). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 (prerender and interactive passes draw the same ids).
- **Layout-effect timing.** `indeterminate` re-assertion, `setFilled`, the value-changed reaction (clear errors, dirty, validation), field-control registration, label-id registration, child-id registration for `aria-controls` and the `aria-labelledby` fallback all run in layout effects: synchronously after the DOM update, before paint. The fallback runs after *every* commit with no dependency list. The disabled-state registry for the parent algorithm runs in a passive effect (after paint). A port should run these as "after render, before paint" hooks (the disabled registry may be after paint, since it is only read in a later event). Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (post-commit, pre-paint C# state work → `OnAfterRender`) and §Relaxation table (FFI registration row; Server lands after paint plus a round trip).
- **Rendered id read-back.** The root reads the `id` actually placed on its rendered element (`element.props.id`) and reports it to the group for `aria-controls`, so a substitute element's own `id` wins (RND-DOM-32). A port must publish the id that ends up in the DOM, not the generated one. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 1 (frame rewriting reads the substitute's attributes) and §Requirements relaxed or withdrawn for the port (RND-DOM-32).
- **StrictMode / remount guards.** `useSetFieldFocused` re-runs on mount to reclaim focus that no focus event reported (the "focused during mount in StrictMode" test). `useFieldControlRegistration` captures the dirty baseline once per field, not per control instance. The frozen shared `EMPTY_OBJECT`/`EMPTY_ARRAY` singletons make StrictMode double-invocation safe; a port must not share mutable defaults between instances.
- **Prop merging order.** `useRenderElement` receives `[rootProps, elementProps, otherGroupProps, getButtonProps, getDescriptionProps, validationPropsGetter]`. Objects merge left-to-right (later attribute wins, including `undefined`), but event handlers chain right-to-left (consumer handler before built-in) with `preventBaseUIHandler()` as the opt-out. The three getters receive the merged props so far and wrap/augment them. A port needs an equivalent "consumer handler first, can veto internal handler" composition. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3.
- **Ids.** With React ≥ 18, generated ids are `base-ui-` + `React.useId()` output; React 17 falls back to a global counter assigned after mount (ids are `undefined` for one render, which is what `CheckboxRoot.react17.test.tsx` exercises). Ports should generate stable, SSR-safe ids eagerly; CHK-EDGE-10's "no partially built id" clause then holds trivially. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5.
- **Indicator unmount flush.** The exit completion uses `ReactDOM.flushSync` inside a microtask (batched across indicators) so that the removal commits before the browser paints the frame in which the animation ended. The porting requirement is "DOM removal lands in the same microtask as the animation's completion, once for all indicators that completed together" (CHK-ANI-04/05). Port: see decisions/04-js-interop-boundary.md §1 (`transitions` module) and §2 (`data-starting-style`/`data-ending-style` JS-owned), and decisions/01-hosting-model-and-timing.md §Relaxation table (TRN-ANI-03/10 row).
- **Context identity.** The root publishes its state object (memoized) to Indicators; the group publishes a memoized context. No port-relevant behavior beyond CHK-STA-02. Port: see decisions/05-state-store-and-event-details.md §1 (state cascaded as a fixed value).
- **`error()` logging** is dev-only and deduplicated per message string; porting it as a once-per-message console error is sufficient.
- **Mechanism placement.** Root press and keyboard activation through `useButton` (<kbd>Space</kbd> keydown/keyup click synthesis with modifiers, disabled click/`pointerdown` suppression, `tabindex`/`aria-disabled`/`disabled`; CHK-KEY-01/02/09/11, CHK-PTR-08, CHK-FOC-01/02, CHK-ARIA-05) is browser-event work; a consumer `preventBaseUIHandler()` against it is a decision point. The state `data-*` attributes (CHK-DOM-02..08) are a projection of the state object. Field and Form integration (§8: registration, validation, `filled`/`dirty`/`touched`/`focused` writes) is C#. Port: see decisions/04-js-interop-boundary.md §1 (`button` module; field and form infrastructure is C#) and §3 (`decide()`, EVD-EVT-26), decisions/02-render-pipeline-and-attribute-merging.md §Decision 4.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| CHK-STR-01 | MUST | Root renders root element, optional hidden-value input, hidden checkbox as siblings | 2 |
| CHK-STR-02 | MUST | Indicator must be inside Root; throws otherwise | 2 |
| CHK-STR-03 | MUST | Indicator attaches no handlers/ARIA/id | 2 |
| CHK-STR-04 | MUST | Group roles: member / parent / detached | 2 |
| CHK-API-01 | MUST | `checked` defined on first render → consumer-owned; ignored for members | 3.1 |
| CHK-API-02 | MUST | initial state `defaultChecked` (default `false`) | 3.1 |
| CHK-API-03 | MUST | `onCheckedChange(checked, details)` signature | 3.1 |
| CHK-API-04 | MUST | `indeterminate` default false; mixed rendering, input property, Indicator | 3.1 |
| CHK-API-05 | MUST | `parent` default false; no name/registration/uncheckedValue; `data-parent`; derived with `allValues` | 3.1 |
| CHK-API-06 | MUST | identity = `value ?? effective name`; `value` on input only | 3.1 |
| CHK-API-07 | MUST | `uncheckedValue` submitted only when standalone, off and named | 3.1 |
| CHK-API-08 | MUST | effective name = field name ?? prop; on inputs only; none for parent | 3.1 |
| CHK-API-09 | MUST | `form` on both inputs, not root; owns Enter submission | 3.1 |
| CHK-API-10 | MUST | `required` default false; input `required`; `aria-required` | 3.1 |
| CHK-API-11 | MUST | effective disabled = field OR item OR group OR prop | 3.1 |
| CHK-API-12 | MUST | `readOnly` default false; blocks user toggles; `aria-readonly`; Enter still submits | 3.1 |
| CHK-API-13 | MUST | `inputRef` → hidden checkbox. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 3.1 |
| CHK-API-14 | MUST | `id` → hidden input (non-native) or root (native); `''` → scope id | 3.1 |
| CHK-API-15 | MUST | `nativeButton` default false; selects UBT mode | 3.1 |
| CHK-API-16 | MUST | `className`/`style`/`render` per RND with state object | 3.1 |
| CHK-API-17 | MUST | other props forwarded to root; consumed list | 3.1 |
| CHK-API-18 | MUST | Indicator `keepMounted` default false | 3.2 |
| CHK-API-19 | MUST | Indicator props forwarded; customization per RND | 3.2 |
| CHK-STA-01 | MUST | Root state object has exactly ten fields | 3.1 |
| CHK-STA-02 | MUST | Indicator state = root state + `transitionStatus` | 3.2 |
| CHK-STA-03 | MUST | standalone ownership fixed on first render | 4 |
| CHK-STA-04 | MUST | component-owned toggle sets state from input | 4 |
| CHK-STA-05 | MUST | consumer-owned: DOM follows prop only | 4 |
| CHK-STA-06 | MUST | controlled → undefined falls back to initial default | 4 |
| CHK-STA-07 | MUST | member checked = group value contains identity | 4 |
| CHK-STA-08 | MUST | parent checked/indeterminate derived from lengths | 4 |
| CHK-STA-09 | MUST | indeterminate rendering wins; underlying checked still toggles | 4 |
| CHK-STA-10 | MUST | field `filled` = checked (standalone), pre-paint | 4 |
| CHK-STA-11 | MUST | field `dirty` = checked !== initialValue (standalone) | 4 |
| CHK-STA-12 | MUST | detached member / parent without allValues keep own state | 4 |
| CHK-DOM-01 | MUST | root id: generated (non-native) / control id (native) | 5 |
| CHK-DOM-02 | MUST | exactly one of data-checked/data-unchecked unless indeterminate | 5 |
| CHK-DOM-03 | MUST | data-indeterminate; suppresses checked/unchecked | 5 |
| CHK-DOM-04 | MUST | data-disabled/readonly/required | 5 |
| CHK-DOM-05 | MUST | data-touched/dirty/filled/focused only within Field | 5 |
| CHK-DOM-06 | MUST | data-valid/data-invalid/neither | 5 |
| CHK-DOM-07 | MUST | data-parent on root iff `parent` | 5 |
| CHK-DOM-08 | MUST | Indicator carries same data attributes + transition attributes | 5 |
| CHK-DOM-09 | MUST | consumer attributes override built-in; group aria-controls exception | 5 |
| CHK-DOM-10 | MUST | `type="button"` when native | 5 |
| CHK-DOM-11 | MUST | hidden input type/aria-hidden/tabindex | 5 |
| CHK-DOM-12 | MUST | hidden input checked/disabled/required/form/name mirror; no name for parent | 5 |
| CHK-DOM-13 | MUST | hidden input `indeterminate` property re-asserted pre-paint | 5 |
| CHK-DOM-14 | MUST | hidden input visually-hidden styles (two variants) | 5 |
| CHK-DOM-15 | MUST | hidden input id = control id (non-native) / none (native); no claim of group scope | 5 |
| CHK-DOM-16 | MUST | `value` attribute: iff prop defined; grouped: value when checked else "" | 5 |
| CHK-DOM-17 | MUST | no `name`/`value` on root | 5 |
| CHK-DOM-18 | MUST | hidden input gets field describedby/invalid | 5 |
| CHK-DOM-19 | MUST | hidden-value input rendering conditions | 5 |
| CHK-DOM-20 | MUST | both parts pass RND conformance suite | 13 |
| CHK-DOM-21 | MUST | explicit id removal / keyed id-less remount → generated id, label follows | 13 |
| CHK-DOM-22 | MUST | runtime `id` change re-targets hidden input and fallback label | 13 |
| CHK-ARIA-01 | MUST | `role="checkbox"` | 5 |
| CHK-ARIA-02 | MUST | `aria-checked` mixed/true/false | 5 |
| CHK-ARIA-03 | MUST | `aria-readonly` only when readOnly | 5 |
| CHK-ARIA-04 | MUST | `aria-required` only when required | 5 |
| CHK-ARIA-05 | MUST | `aria-disabled` (non-native) vs `disabled` (native) | 5 |
| CHK-ARIA-06 | MUST | `aria-labelledby` precedence; aria-label suppresses | 5 |
| CHK-ARIA-07 | MUST | native label fallback discovery and assigned id | 5 |
| CHK-ARIA-08 | MUST | fallback re-evaluated every render | 5 |
| CHK-ARIA-09 | MUST | fallback disabled when native | 5 |
| CHK-ARIA-10 | MUST | `aria-describedby` = consumer + scope message ids | 5 |
| CHK-ARIA-11 | MUST | `aria-invalid` when field invalid and not disabled | 5 |
| CHK-ARIA-12 | MUST | parent `aria-controls` = registered child rendered ids in allValues order | 5 |
| CHK-KEY-01 | MUST | non-native Space: keydown prevents, keyup toggles | 6.1 |
| CHK-KEY-02 | MUST | native Space not intercepted | 6.1 |
| CHK-KEY-03 | MUST | Enter never toggles; native default cancelled quietly | 6.1 |
| CHK-KEY-04 | MUST | Enter clicks default submitter after a microtask | 6.1 |
| CHK-KEY-05 | MUST | consumer/ancestor preventDefault cancels Enter submission | 6.1 |
| CHK-KEY-06 | MUST | Enter: readOnly submits; disabled nothing | 6.1 |
| CHK-KEY-07 | MUST | descendant key events never toggle; Enter submission not target-filtered | 6.1 |
| CHK-KEY-08 | MUST | Tab not intercepted | 6.1 |
| CHK-KEY-09 | MUST | disabled: keys inert | 6.1 |
| CHK-KEY-10 | MUST | modifier state carried to change event | 6.1 |
| CHK-KEY-11 | MUST | consumer keyup preventDefault cancels Space | 6.1 |
| CHK-KEY-12 | MUST | readOnly: Space no-op | 6.1 |
| CHK-PTR-01 | MUST | root click → dispatched click on input with modifiers | 6.2 |
| CHK-PTR-02 | MUST | root click preventDefault; only if input mounted | 6.2 |
| CHK-PTR-03 | MUST | internal input click does not propagate; one click per user click | 6.2 |
| CHK-PTR-04 | MUST | direct input click toggles | 6.2 |
| CHK-PTR-05 | MUST | wrapping native label toggles once | 6.2 |
| CHK-PTR-06 | MUST | `htmlFor` label toggles (both modes) | 6.2 |
| CHK-PTR-07 | MUST | readOnly: root click ignored; input change reverted | 6.2 |
| CHK-PTR-08 | MUST | disabled: click/pointerdown prevented; no consumer handlers | 6.2 |
| CHK-PTR-09 | MUST | consumer onClick first; stopPropagation keeps toggle; preventBaseUIHandler cancels | 6.2 |
| CHK-PTR-10 | MUST | non-native Field.Label click focuses, no toggle | 6.2 |
| CHK-PTR-11 | MUST | pre-canceled input click ignored | 6.2 |
| CHK-PTR-12 | MUST | click on indeterminate box flips underlying checked, stays mixed | 6.2 |
| CHK-FOC-01 | MUST | tabindex 0 (both modes); input never a tab stop | 6.3 |
| CHK-FOC-02 | MUST | disabled: tabindex -1 (non-native) / unfocusable (native) | 6.3 |
| CHK-FOC-03 | MUST | focus on hidden input redirected to root | 6.3 |
| CHK-FOC-04 | MUST | field focused set/cleared on focus/blur | 6.3 |
| CHK-FOC-05 | MUST | field focused cleared on disable/unmount; reclaimed when focused at mount | 6.3 |
| CHK-FOC-06 | MUST | invalid-submit focus target = root (standalone) | 6.3 |
| CHK-EVT-01 | MUST | callback before state update with input's checked | 7 |
| CHK-EVT-02 | MUST | reason always 'none' | 7 |
| CHK-EVT-03 | MUST | event = native input click; trigger undefined | 7 |
| CHK-EVT-04 | MUST | cancel(): no state change, input restored, no field/group effects | 7 |
| CHK-EVT-05 | MUST | not called for prop changes, mount, or group-driven changes | 7 |
| CHK-EVT-06 | MUST | exactly once per toggle | 7 |
| CHK-EVT-07 | MUST | not called when readOnly/disabled/pre-canceled | 7 |
| CHK-EVT-08 | MUST | allowPropagation no effect | 7 |
| CHK-EVT-09 | MUST | grouped: own callback → group callback (same details) → commits; cancel chain | 7 |
| CHK-FRM-01 | MUST | checked → name=value ?? "on" | 8 |
| CHK-FRM-02 | MUST | unchecked, no uncheckedValue → no entry | 8 |
| CHK-FRM-03 | MUST | unchecked + uncheckedValue → one entry | 8 |
| CHK-FRM-04 | MUST | disabled → no entries | 8 |
| CHK-FRM-05 | MUST | `form` attribute → external form | 8 |
| CHK-FRM-06 | MUST | registers with Field (value, control, name); not when disabled/grouped | 8 |
| CHK-FRM-07 | MUST | dirty baseline captured once | 8 |
| CHK-FRM-08 | MUST | required + unchecked → valueMissing on submit, blocks, focuses | 8 |
| CHK-FRM-09 | MUST | validate(checked) once per toggle in on-change modes | 8 |
| CHK-FRM-10 | MUST | external controlled change revalidates | 8 |
| CHK-FRM-11 | MUST | onBlur mode: full validation on blur | 8 |
| CHK-FRM-12 | MUST | onSubmit mode: revalidate on change after first submit | 8 |
| CHK-FRM-13 | MUST | external Form error cleared on change | 8 |
| CHK-FRM-14 | MUST | touched on blur unless unmounted input | 8 |
| CHK-FRM-15 | MUST | form reset does not change state/aria-invalid/describedby | 8 |
| CHK-FRM-16 | MUST | Field.Label `for`/`aria-labelledby` association; id fallback; SSR deferral | 8 |
| CHK-FRM-17 | MUST | description/error ids on root and input | 8 |
| CHK-FRM-18 | MUST | box `name` serves as field name when Field has none | 8 |
| CHK-FRM-19 | MUST | hidden input registered in validation registry (group's or field's); parent never | 8 |
| CHK-FRM-20 | MUST | grouped: no field registration/filled/dirty/validation writes; blur commits group value | 8 |
| CHK-FRM-21 | MUST | Field.Item scope claim and disabled | 8 |
| CHK-ANI-01 | MUST | Indicator in DOM iff keepMounted \|\| mounted | 9 |
| CHK-ANI-02 | MUST | initially rendered Indicator: no 'starting' | 9 |
| CHK-ANI-03 | MUST | enter: data-starting-style same commit, cleared next frame | 9 |
| CHK-ANI-04 | MUST | exit: data-ending-style, wait on animations finished, sync pre-paint unmount | 9 |
| CHK-ANI-05 | MUST | batched exit completions in one commit | 9 |
| CHK-ANI-06 | MUST | watcher armed only while not rendered; no enter inspection | 9 |
| CHK-ANI-07 | MUST | keepMounted: stays, no hidden/style, ending phase still runs | 9 |
| CHK-ANI-08 | MUST | reopen during exit: abort, no 'starting', ending cleared next frame | 9 |
| CHK-ANI-09 | MUST | root and inputs always mounted | 9 |
| CHK-EDGE-01 | MUST | Indicator outside Root throws (message) | 12 |
| CHK-EDGE-02 | SHOULD | controlled/uncontrolled dev error (`Checkbox`, `checked`) | 12 |
| CHK-EDGE-03 | SHOULD | defaultChecked change dev error | 12 |
| CHK-EDGE-04 | SHOULD | nativeButton/element mismatch dev errors | 12 |
| CHK-EDGE-05 | MUST | imperative focus/blur/click before input mounts tolerated | 12 |
| CHK-EDGE-06 | MUST | readOnly affects only user toggles + aria/data | 12 |
| CHK-EDGE-07 | MUST | blank aria-label treated as absent for labelling | 12 |
| CHK-EDGE-08 | MUST | multiple standalone boxes per Field unsupported; single-box behavior required | 12 |
| CHK-EDGE-09 | MUST | no RTL/locale dependence | 12 |
| CHK-EDGE-10 | MUST | SSR markup matches; explicit id deferred; no server aria-controls | 12 |
| CHK-EDGE-11 | MUST | parent outside group / without allValues behaves standalone, no name | 12 |
| CHK-EDGE-12 | MUST | detached member gets an id; parent toggle ignores it | 12 |
| CHK-EDGE-13 | MUST | accessible name tracks aria-label / label mount changes | 12 |

## 16. Open questions

1. **`data-parent` is undocumented.** `checkbox/root/CheckboxRoot.tsx:37,303` emits `data-parent=""` on a parent box, but `CheckboxRootDataAttributes.ts` and the API outline do not list it, and only a test relies on it (`useCheckboxGroupParent.test.tsx:31`). Documented as CHK-DOM-07 (MUST, since the test filters by it); confirm whether it is public.
   Resolved 2026-10-23 (M2 track 5): CHK-DOM-07 stays as written, and `CheckboxRootDataAttributes` has no `data-parent` constant, as Base UI's (§3.3).
2. **Group-derived `aria-controls` overrides the consumer's.** `CheckboxRoot.tsx:379` merges `otherGroupProps` (`{ 'aria-controls': … }`, possibly `undefined`) *after* `elementProps`, so a consumer `aria-controls` on a parent box is always replaced, even by "absent" (RND-DOM-19). Recorded in CHK-DOM-09; probably unintended. Resolved (port side, "absent" clause only): decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port makes an explicitly absent value non-overriding (RND-DOM-19), so in the port only a defined group `aria-controls` replaces the consumer's; whether the defined case is intended remains open.
   Resolved 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23): mirrored for the defined case: a parent's group-derived `aria-controls` replaces the consumer's in the part's attribute finish, part-owned per CHK-DOM-09 (`CheckboxGroupTests` "port: the parent's group-derived aria-controls replaces the consumer's").
3. **Parent derivation uses lengths, not membership** (`useCheckboxGroupParent.ts:24-25`). A group value containing entries outside `allValues` can make the parent read as checked (e.g. `value=['cherry']`, `allValues=['apple']`), and `allValues=[]` with `value=[]` renders the parent as checked. Recorded as-is in CHK-STA-08 / CKG-STA-06.
4. **`preventBaseUIHandler()` in a consumer `onKeyDown` on a native-button root.** It suppresses the component's Enter handler (CHK-KEY-03) before the native default is cancelled, so the browser's own Enter activation of the `<button>` dispatches a click and toggles the box (`CheckboxRoot.tsx:320-332`, `useButton.ts:121-125`). No test; not stated as a requirement.
   Resolved 2026-10-23 (M2 track 5, design point C2, settled by the orchestrator 2026-10-23): fixed, not mirrored: an untested quirk with no requirement (ADR 04 Amendments 2026-10-18, quirks) that contradicts CHK-KEY-03's MUST. The `button` module's Enter suppression is a library-internal option, not a veto (ADR 04 Amendments 2026-10-18 item 4), and the `form` module cancels the native default before any consumer handler can veto it, so Enter never toggles the box, in line with CHK-KEY-03's MUST.
5. **Underlying toggle while indeterminate.** Clicking an indeterminate box flips `checked` and fires `onCheckedChange` while the rendering stays mixed (CHK-STA-09). The docs page does not describe this; the demos (`checkbox-group/demos/nested`) rely on the consumer dropping `indeterminate` in response. Confirm this is the intended contract.
   Resolved 2026-10-23 (M2 track 5): kept as CHK-STA-09 states it (`CheckboxRootTests` "should not change its state when clicked", `checkbox.spec.ts` "keeps the native input state when checked changes while indeterminate remains").
6. **`aria-describedby`/`aria-invalid` on an `aria-hidden` input** (`CheckboxRoot.tsx:261-262`): harmless but redundant; kept as CHK-DOM-18 with `Test: none` for parity with Switch.
7. **Form reset vs. hidden input.** As for Switch: no `reset` listener (`checkbox/root/CheckboxRoot.tsx:195-263,403` render the input from state only); whether the hidden input's `checked` is re-synchronized after a native reset depends on React's controlled-input handling. CHK-FRM-15 requires only that the state is stable; a port should re-sync the hidden input after `reset`.
   Resolved 2026-10-23 (as switch §16 Q1 (b), chosen by the user 2026-10-22; FFI-FRM-30's dated note): the `dom` primitive re-asserts the hidden input's `checked` one task after the owning form's `reset` (`checkbox.spec.ts` "port: a native form reset keeps the state and re-syncs the hidden input, so the next submit matches it").
8. **Fallback label id format** `"<hidden input id>-label"` (`useAriaLabelledBy.ts:16`) is only tested for non-emptiness; CHK-ARIA-07 states the implemented format.
9. **Disabled-state registry timing.** Members write their disabled state for the parent algorithm after paint (`CheckboxRoot.tsx:265-276`, passive effect) while everything else is pre-paint; a parent click in the same task as a member's disabled change could read a stale value. Not observable in tests.
   Resolved 2026-10-23 (M2 track 6, design point G4, settled by the orchestrator 2026-10-23): a member updates its disabled entry together with its state, so the parent reads the current states in the same task (checkbox-group CKG-STA-09's note).
10. **`CheckboxRoot.react17.test.tsx`** exercises a legacy id-generation path (ids assigned after mount). The observable obligations are folded into CHK-EDGE-10/CHK-DOM-21; a port with eager ids need not reproduce the intermediate id-less render. Resolved: decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 draws every generated id at first render, so the legacy path does not arise in the port.
11. **Docs vs. source on `parent`.** The JSDoc (`checkbox/root/CheckboxRoot.tsx:495`) says the prop "Must be used in a Checkbox Group"; the source tolerates a parent outside a group or without `allValues` (CHK-EDGE-11). The guidance is advisory.
   Resolved 2026-10-23 (M2 track 5): kept as CHK-EDGE-11 (`CheckboxRootTests` "port: a parent box outside a group keeps its own state, carries data-parent and submits nothing").
12. **Owner of the Enter-submission path in the port.** The Enter handling of CHK-KEY-03..05 (`checkbox/root/CheckboxRoot.tsx:320-362`) needs a native-only `preventDefault()`, propagation-wide observation of later `preventDefault()` calls and a microtask before clicking the default submitter. decisions/01-hosting-model-and-timing.md §Decision item 3 puts the microtask in JavaScript and decisions/04-js-interop-boundary.md §3 covers the consumer veto, but the module table in decisions/04-js-interop-boundary.md §1 names no module for this checkbox-specific path (the `button` module covers only generic key-to-click synthesis). Needs a decision on which module owns it. Decided (2026-10-18): the `form` mechanism module (ADR 04 Amendments, module placement rule).
