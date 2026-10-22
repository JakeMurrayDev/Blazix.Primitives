# Input

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/input/` (delegates entirely to `packages/react/src/field/control/FieldControl.tsx`) |
| Tests | `packages/react/src/input/Input.test.tsx` (1 file; `Input.spec.tsx` is a type-level test) |
| Docs page | `docs/src/app/(docs)/react/components/input/page.mdx` |
| Prefix | `INP` |
| Status | approved |
| Revised | 2026-10-22 |
| Builds on | [field](field.md) (`Field.Control`) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Input is a native `<input>` element that participates in Base UI's field and form machinery without any wrapping part. Used alone it is an ordinary text input that forwards every native attribute. Placed inside `Field.Root` it becomes the field's control: `Field.Label` points at it, `Field.Description`/`Field.Error` describe it, and its value feeds the field's `dirty`/`touched`/`filled`/`focused`/`valid` state, which it renders as `data-*` attributes. Placed inside `Form` it is validated on submit (native constraints plus the field's `validate`), focused when it is the first invalid control, and cleared of server errors when its value changes.

`Input` is **the same implementation as `Field.Control`**: it renders `<Field.Control>` with its props and DOM handle passed through unchanged (`input/Input.tsx:13-18`). The two differ only in their public names and in the TypeScript declaration of `value` (Input re-declares it with the native input value type). Every behavior below is therefore `Field.Control`'s behavior, cited at its implementation in `field/control/FieldControl.tsx`; the field document ([field](field.md)) owns `Field.Control`, and the mechanism — registration, validation pipeline, label/description wiring — is owned by [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) (FFI), whose IDs are cited rather than restated. The value is a string owned either by the consumer (`value` + `onValueChange`) or by the browser (`defaultValue`); the component itself never stores it.

## 2. Anatomy

```
<Input>                       required, 1          default element: input
```

Input has a single part and renders a single element. Typical compositions:

```
<Input />                                         standalone (no field state, native behavior only)
<label>Name <Input /></label>                     standalone, natively labelled
<Field.Root><Field.Label/><Input/><Field.Error/></Field.Root>   as the field's control
```

- **INP-STR-01** — MUST. `Input` MUST render exactly one element (default `<input>`) and no hidden input, wrapper or portal; the element itself is the form-participation input and the validation input of the field (FFI §6.3 table A, row `Field.Control` / `Input`).
  Source: `input/Input.tsx:13-18`, `field/control/FieldControl.tsx:111-197`. Test: [L2] `Input.test.tsx` conformance suite "Base UI component API".
- **INP-STR-02** — MUST. `Input` MUST be behaviorally indistinguishable from `Field.Control`: it forwards all of its props and its DOM handle to the `Field.Control` implementation, adds nothing and intercepts nothing. Every requirement of this document applies equally to `Field.Control`, and every `Field.Control` requirement in [field](field.md) applies to `Input`.
  Source: `input/Input.tsx:13-18`; see [FFI §6.3](../architecture/field-and-form-infrastructure.md) ("`Input` is an alias of `Field.Control`"). Test: [L2] `Input.test.tsx` conformance suite "Base UI component API" (`refInstanceof: HTMLInputElement`).
- **INP-STR-03** — MUST. `Input` MUST work in all three placements: outside any `Field.Root` (default field scope, FFI-STR-01: no registration, no flags, no validation), inside a `Field.Root` (becomes the field's active control, FFI-STR-07), and inside a `Form` with or without a `Field.Root` (§8). It MUST NOT throw in any placement.
  Source: `field/control/FieldControl.tsx:49-60` (optional lookups), `internals/field-root-context/FieldRootContext.ts:37-67,71-81`; see [FFI-STR-02](../architecture/field-and-form-infrastructure.md), FLD-STR-02. Test: [L2] `Input.test.tsx` conformance suite (standalone); `Form.test.tsx` "runs when the form is submitted" (`Field.Control` inside `Field.Root` inside `Form`).
  Port note 2026-10-22: the standalone placement is also tested by the L2 port title "an input outside a field renders only data-disabled and registers nowhere" and at L4 by `input.spec.ts` "reports value changes outside a field" over the `input-showcase` page. The `Form.test.tsx` placement is track 2's.
- **INP-STR-04** — MUST. Input satisfies the `Field.Control` requirements of [field](field.md) — FLD-API-15…23, FLD-STA-03, FLD-STA-12…14, FLD-DOM-04, FLD-DOM-08…12, FLD-ARIA-01…03, FLD-ARIA-07, FLD-KEY-01…04, FLD-FOC-01…07, FLD-EVT-01…09, FLD-FRM-01…18, FLD-EDGE-09, FLD-EDGE-10, FLD-EDGE-12, FLD-EDGE-13 — with these substitutions: `Field.Control` → `Input`; `Field.Control.Props` → `Input.Props`; `Field.Control.State` → `Input.State`; `Field.Control.ChangeEventReason`/`ChangeEventDetails` → `Input.ChangeEventReason`/`Input.ChangeEventDetails`; `FieldControlDataAttributes` → `InputDataAttributes`. Those FLD IDs are the normative text for §4–§12; this document states individually only the Input-specific declarations (§3), behavior field.md does not state (INP-EVT-02, INP-FRM-07, INP-EDGE-08) and the `input/` test evidence (§13). Withdrawn INP IDs name the FLD IDs that supersede them.
  Source: `input/Input.tsx:13-18,35-45`, `input/InputDataAttributes.ts:1-28`; see [field](field.md) §3.3. Test: see field.md §13 (`FieldControl.test.tsx` runs against the same implementation).
  Port note 2026-10-22 (M2 track 1): the adoption is proven by running one translation of `FieldControl.test.tsx` over both parts. `FieldControlSuite<TControl>` is generic in the part, and `FieldControlTests` and `InputFieldControlTests` derive it, so a divergence between `Input` and `FieldControl` fails a title. The same holds for the EditContext participation suite (ADR 05 Amendments 2026-10-22). The two parts share one internal `FieldControlBehavior`, and neither renders nor derives from the other (ADR 08 Amendments 2026-10-22).

## 3. Public API

### 3.1 `Input`

**Default element:** `<input>`. **Accepts `render` element substitution:** yes (e.g. `<textarea>`; see INP-API-08). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `string \| number \| readonly string[]` (native input value type) | — (browser-owned) | The value when consumer-owned. Supplying any non-`undefined` value makes the element controlled for that render. | INP-API-01 |
| `defaultValue` | `string \| number \| readonly string[]` | — | Initial value when browser-owned. | INP-API-02 |
| `onValueChange` | `(value: string, eventDetails: Input.ChangeEventDetails) => void` | — | Called on every user edit with the element's new string value. | INP-API-03 |
| `name` | `string` | — | Form field name; overridden by the enclosing `Field.Root`'s `name`. Not listed in the API outline (native attribute). | INP-API-04 |
| `disabled` | `boolean` | `false` | Native `disabled`; combined (OR) with the enclosing `Field.Root`'s disabled state. Not listed in the API outline. | INP-API-05 |
| `id` | `string` | Field-provided or generated | The element id; also what `Field.Label`'s `for` points at. Not listed in the API outline. | INP-API-06 |
| `autoFocus` | `boolean` | `false` | Native autofocus. Not listed in the API outline. | INP-API-07 |
| `type`, `required`, `readOnly`, `pattern`, `minLength`, `maxLength`, `min`, `max`, `step`, `placeholder`, `autoComplete`, `form`, … | native `<input>` attributes | browser defaults (`type` → `text`) | Forwarded unchanged; `required`/`pattern`/… drive native constraint validation (FFI-FRM-06). | INP-API-08 |
| `onChange`, `onFocus`, `onBlur`, `onKeyDown`, other handlers | native handlers | — | Merged with the component's own handlers (consumer's runs first, may veto). | INP-API-10 |
| `aria-describedby`, `aria-invalid`, `aria-labelledby` | `string` / `boolean` | — | Composed with / overridden by the field wiring (§5.3). | FLD-ARIA-01…03 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | INP-API-09 |

**Consumed (not forwarded verbatim):** `render`, `className`, `style`, `id`, `name`, `value`, `disabled`, `onValueChange`, `defaultValue`, `autoFocus`. Of these, `id`, `name`, `disabled`, `autoFocus` and exactly one of `value`/`defaultValue` are re-applied with their computed values (FLD-DOM-08…12).

- **INP-API-01** — MUST. The element MUST be consumer-owned (controlled) in any render in which `value` is not `undefined`: the `value` attribute/property is set from the prop and user edits are reported through `onValueChange` only, never written to field state directly (FLD-EVT-06). The determination is made per render, not latched at first render (§14, §16). Port: relaxed (ADR 05 Amendments item 1).
  Source: `field/control/FieldControl.tsx:74,122,130-132`, `input/Input.tsx:29-32`; see FLD-API-15, FLD-STA-12. Test: [L2] `FieldControl.test.tsx` "validates once when a controlled value is changed by the user", "renders once per keystroke for controlled input changes".
  Port note 2026-10-22 (ADR 05 Amendments 2026-10-18 item 1; ADR 08 Amendments 2026-10-22): `Value` is `string?` with `ValueChanged`. Whether the part is controlled is fixed by the first parameter set (`Controlled<string?>`). From attach the `form` module shows the consumer's value and re-asserts it after an edit the consumer does not apply (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22). On Server that revert lands a round trip later (ADR 01 relaxation row for FLD-API-15).
- **INP-API-02** — MUST. When `value` is `undefined`, `defaultValue` MUST be forwarded as the element's default value (the browser owns the live value). A later change of `defaultValue` MUST be forwarded without any warning and MUST recompute `filled` from the element's *current* value, keeping a user-edited value (FFI-EDGE-06).
  Source: `field/control/FieldControl.tsx:43,89-94,122`, `input/Input.tsx:25-28`; see [FFI-EDGE-06](../architecture/field-and-form-infrastructure.md); see FLD-API-16. Test: [L2] `FieldControl.test.tsx` "forwards a changing defaultValue without warning", "keeps filled state from the edited value when defaultValue changes".
- **INP-API-03** — MUST. `onValueChange` MUST receive the element's new value as a string (`event.currentTarget.value`) as its first argument and a change-event-details object (§3.2) as its second. Full firing rules are in §7.
  Source: `field/control/FieldControl.tsx:123-126,204-211`, `input/Input.tsx:21-24`; see FLD-API-17. Test: none in `input/`; `Docs: CHANGELOG.md v1.0.0-alpha.7 "Allow onValueChange to fire when defaultValue/value are not set (#2600)"`, `CHANGELOG.md v1.0.0-alpha.8 "Fix onValueChange value type (#2112)"`.
- **INP-API-04** — MUST. The effective form name MUST be the enclosing `Field.Root`'s `name` when that is defined, otherwise the `name` prop; it is placed on the element as the `name` attribute. The `name` prop (not the effective name) is also registered with the field as the fallback name used for form errors and the values object when `Field.Root` has no `name` (FFI-STA-10).
  Source: `field/control/FieldControl.tsx:39,63,86,118`; see [FFI-STA-10](../architecture/field-and-form-infrastructure.md); see FLD-API-18. Test: [L2] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values", "updates the Field.Control name fallback when the name changes", "uses the Field.Control name fallback when the Field.Root name is removed"; `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors".
- **INP-API-05** — MUST. `disabled` defaults to `false`; the effective disabled state MUST be `fieldDisabled || disabled`, where `fieldDisabled` is the enclosing `Field.Root`'s disabled state (which already includes an enclosing `Fieldset.Root`, FFI-STA-11), rendered as the native `disabled` attribute.
  Source: `field/control/FieldControl.tsx:41,62,117`; see FLD-API-19. Test: [L2] `FieldRoot.test.tsx` "should add data-disabled style hook to all components"; `FieldsetRoot.test.tsx` "keeps nested fieldsets disabled when an ancestor fieldset is disabled"; `Form.test.tsx` "clears invalid attributes when a field control becomes disabled".
- **INP-API-06** — MUST. The element's `id` MUST be resolved by the labelable-scope control-id claim (FFI-ALG-06/07): inside a `Field.Root`, the scope's control id, which is the scope's generated default until this control's registration has run (first render, server markup — even when an explicit `id` is given) and the explicit `id` prop afterwards (FLD-DOM-08); outside a `Field.Root`, the `id` prop verbatim, else a generated `base-ui-<token>` id. Removing an explicit id later MUST fall back to a generated id in the same update (FFI-FRM-36). Server: relaxed, see §9.
  Source: `field/control/FieldControl.tsx:38,72,116`, `internals/labelable-provider/useLabelableId.ts:10-83`; see FLD-API-20, FLD-DOM-08. Test: [L2] `FieldRoot.test.tsx` "falls back to a generated id when the control id is removed", "updates label associations when the control id changes"; `FieldControl.test.tsx` "updates the label association when the control is swapped".
- **INP-API-07** — MUST. `autoFocus` defaults to `false` and MUST be forwarded to the element; an input that is already the active element when it mounts (autofocus, pre-hydration focus) MUST acquire the field's `focused` flag post-commit, pre-paint without a focus event (FFI-STA-09). Server: relaxed, see §9.
  Source: `field/control/FieldControl.tsx:44,121`, `internals/field-root-context/useSetFieldFocused.ts:36-43`; see FLD-API-21, FLD-FOC-04. Test: [L2] `FieldControl.test.tsx` "should sync focused state when autoFocus is used with SSR". Docs: `CHANGELOG.md v1.0.0-beta.1 "Fix autofocus in SSR environments (#3871)"`. (L4 for the already-active-element clause)
  Port note 2026-10-22 (ADR 01 and ADR 04 Amendments 2026-10-22, settled by the orchestrator 2026-10-22, ADR 02 verification item 1 and ADR 15): the interactive render replaces a prerendered element, which loses the browser's autofocus. The `form` module therefore focuses an `AutoFocus` input once, at attach, when nothing else has focus, and reports the active element at attach and when the input is enabled again. The flag is acquired on both modes; the §9 relaxation stands, since the acquisition still follows the attach. Tested at L4 on both modes (`input.spec.ts` "should sync focused state when autoFocus is used with SSR" over the prerendered `field-autofocus?control=input` page).
- **INP-API-08** — MUST. `type` and every other native `<input>` attribute not listed as consumed MUST be forwarded unchanged; the component sets no `type` of its own (browser default `text`) and attaches no attribute-specific behavior — native constraints (`required`, `pattern`, `minLength`, `type="email"`, …) are read back through the element's `ValidityState` by the field (FFI-FRM-06).
  Source: `field/control/FieldControl.tsx:46,193`; see FLD-API-23. Test: [L2] `Form.test.tsx` "does not submit if there are errors" (`required`), `FieldRoot.test.tsx` "handles both `required` and `typeMismatch`" (`type="email"`); `Input.test.tsx` conformance suite (arbitrary attributes). (L4 for the native-constraint clause)
- **INP-API-09** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md) with the state object in "State object" below; the DOM handle is typed `HTMLElement` so that a substitute such as `<textarea>` can be used with a matching handle type. When the substituted element is not an `<input>`, the Enter handling (FLD-KEY-01, FLD-KEY-02) and the text selection on invalid submit (FLD-FOC-06, FFI-FOC-01) do not apply; everything else (registration, flags, `aria-*`, validation through the element's own `ValidityState`) still does.
  Source: `input/Input.tsx:15,17`, `field/control/FieldControl.tsx:33,111-113,173`, `form/Form.tsx:63-65`; see FLD-API-22. Test: [L2] `Input.test.tsx` conformance suite "Base UI component API"; `Input.spec.tsx` (`render={<textarea />}` with an `HTMLTextAreaElement` handle); `FieldControl.test.tsx` "sets filled state from a controlled value on a custom element". Docs: `CHANGELOG.md v1.0.0-beta.1 "Update ref type to HTMLElement (#3866)"`.
- **INP-API-10** — MUST. Consumer handlers (`onChange`, `onFocus`, `onBlur`, `onKeyDown`, …) MUST be merged with the component's own handlers of the same name per RND-EVT-01/02: the consumer's handler runs first and may call `preventBaseUIHandler()` to skip the component's handler (RND-EVT-03), in which case `onValueChange` is not called and no field state is written for that event.
  Source: `field/control/FieldControl.tsx:114-193` (component handlers first in the props list, consumer `elementProps` after); see FLD-API-23, FLD-EVT-09. Test: none (handler merge is specified and tested in RND).

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes. It is the enclosing field's state (FFI-STA-01) with `disabled` replaced by the effective disabled value.

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (INP-API-05). |
| `touched` | `boolean` | Field: the element has been blurred, or Enter pressed in it, at least once. |
| `dirty` | `boolean` | Field: current string value differs from the value captured at first registration. |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated, while an async validator is in flight, or while disabled (computed validity only). |
| `filled` | `boolean` | Field: current value is not the empty string. |
| `focused` | `boolean` | Field: the element owns focus (never `true` while disabled). |

- **INP-STA-01** — MUST. The state object MUST contain exactly the six fields above. Outside a `Field.Root` the five field-derived fields MUST hold their defaults (`touched`/`dirty`/`filled`/`focused` `false`, `valid` `null`) at all times, since the default field scope never updates them.
  Source: `field/control/FieldControl.tsx:65-68,202`, `input/Input.tsx:35`, `internals/field-constants/constants.ts:18-32`; see FLD-STA-03, FLD-STR-02. Test: [L2] `FieldRoot.test.tsx` "should apply [data-field] style hooks to field components"; outside a field: none.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effectively disabled | FLD-DOM-04 |
| `data-touched` | `""` while field `touched` | FLD-DOM-04 |
| `data-dirty` | `""` while field `dirty` | FLD-DOM-04 |
| `data-filled` | `""` while field `filled` | FLD-DOM-04 |
| `data-focused` | `""` while field `focused` | FLD-DOM-04 |
| `data-valid` | `""` while field `valid === true` | FLD-DOM-04 |
| `data-invalid` | `""` while field `valid === false` | FLD-DOM-04 |

#### CSS variables

None.

### 3.2 Shared types

- `Input.ChangeEventReason` = `'none'` — the only literal (identical to `Field.Control.ChangeEventReason`). Source: `input/Input.tsx:37,43`, `field/control/FieldControl.tsx:213`, `internals/reason-parts.ts:1`.
- `Input.ChangeEventDetails` = `Field.Control.ChangeEventDetails` = the change-event details shape from [EVD](../architecture/event-details-and-reasons.md) §3.1 specialised to reason `'none'`: `{ reason: 'none'; event: Event; cancel(); allowPropagation(); isCanceled; isPropagationAllowed; trigger: Element | undefined }`. Source: `input/Input.tsx:38,44`, `field/control/FieldControl.tsx:215-216`, `internals/createBaseUIEventDetails.ts:126-157`.
- `Input.State` = `Field.Control.State` = `Field.Root.State` (the six fields in §3.1). Source: `input/Input.tsx:35,42`, `field/control/FieldControl.tsx:202`.
- `Input.Props` — the props table; extends the native `<input>` attributes (minus `className`, `style`, `color`, `defaultValue`, `defaultChecked`, which are re-declared or excluded, RND-API-04) with `onValueChange`, `defaultValue`, `value`. Source: `input/Input.tsx:20-33,41`, `internals/types.ts:36-61`.
- Exported constant namespace `InputDataAttributes` = `{ disabled, valid, invalid, touched, dirty, filled, focused }` mapping to the attribute names above (same set as `FieldControlDataAttributes`). Source: `input/InputDataAttributes.ts:1-28`, `input/index.ts:5`.

## 4. State model

Input stores nothing itself. Its state model is `Field.Control`'s and is specified in [field](field.md) §4: value ownership is decided per render by `value !== undefined` (FLD-STA-12; §16 Q3); `filled` is written per FLD-STA-13 and `dirty` per FLD-STA-14; the state object is the field's with the effective `disabled` (FLD-STA-03); `touched`, `focused` and `valid` have the field's semantics (FLD-STA-09…11, FFI-STA-07, FFI-STA-09). Input adds no state of its own. The requirements formerly stated here are withdrawn in favor of the FLD IDs below.

- **INP-STA-02** — Withdrawn: superseded by FLD-EVT-02, FLD-EVT-07.
- **INP-STA-03** — Withdrawn: superseded by FLD-API-15, FLD-FRM-01, FLD-FRM-02.
- **INP-STA-04** — Withdrawn: superseded by FLD-API-15, FLD-STA-12, FLD-EVT-06.
- **INP-STA-05** — Withdrawn: superseded by FLD-STA-13.

## 5. Rendered DOM and ARIA

### 5.1 `Input` element

The element is `Field.Control`'s element ([field](field.md) §5.3); Input adds and removes nothing.

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | resolved control id | FLD-API-20, FLD-DOM-08 | C# |
| `name` | effective name; absent when neither Field nor prop gives one | FLD-API-18, FLD-DOM-09 | C# |
| `disabled` | present while effectively disabled | FLD-DOM-10 | C# |
| `autofocus` | present when `autoFocus` | FLD-DOM-12 | C# |
| `value` / default value | `value` prop (controlled) or `defaultValue` (uncontrolled) | FLD-DOM-11 | C# |
| `type`, other native attributes | forwarded | INP-API-08, FLD-API-23 | C# |
| `aria-labelledby` | field label id | FLD-ARIA-01 | C# |
| `aria-describedby` | composed description/error ids | FLD-ARIA-02 | C# |
| `aria-invalid` | `"true"` while invalid and not disabled | FLD-ARIA-03 | C# |
| `data-*` | §3.1 table | FLD-DOM-04 | C# |
| `role` | none set | FLD-ARIA-07 | C# |

- **INP-DOM-01** — Withdrawn: superseded by FLD-DOM-08, FLD-DOM-09, FLD-DOM-10, FLD-DOM-11, FLD-DOM-12, FLD-API-23.
- **INP-DOM-02** — Withdrawn: superseded by FLD-DOM-04, FLD-STR-02.
- **INP-DOM-03** — Withdrawn: superseded by FLD-API-23, FLD-ARIA-01, FLD-ARIA-02, FLD-ARIA-03.
- **INP-DOM-04** — Withdrawn: superseded by FLD-FRM-02 (no hidden input: INP-STR-01).

### 5.2 Hidden `<input>`

Not applicable — the element is itself the native input (INP-STR-01, FLD-FRM-02).

### 5.3 ARIA

Input's ARIA output is `Field.Control`'s (FLD-ARIA-01…03, FLD-ARIA-07); the label association is FLD-DOM-06.

- **INP-ARIA-01** — Withdrawn: superseded by FLD-ARIA-01.
- **INP-ARIA-02** — Withdrawn: superseded by FLD-ARIA-02.
- **INP-ARIA-03** — Withdrawn: superseded by FLD-ARIA-03.
- **INP-ARIA-04** — Withdrawn: superseded by FLD-ARIA-07.
- **INP-ARIA-05** — Withdrawn: superseded by FLD-DOM-06, FLD-API-20.

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | rendered element is an `<input>` (any `type`), any modifiers | Field marked `touched`; value committed (deferred 0 ms inside the enclosing `Form`, yielding to implicit submission; immediate otherwise). Default not prevented. | FLD-KEY-01 |
| <kbd>Enter</kbd> | `<input>` element; consumer `onKeyDown` called `preventBaseUIHandler()` / `preventDefault()` | Built-in handling skipped entirely / value committed immediately (no deferral). | FLD-KEY-04 |
| <kbd>Enter</kbd> | rendered element is not an `<input>` (e.g. `<textarea>`) | Not intercepted (native newline). | FLD-KEY-02 |
| <kbd>Tab</kbd>, arrows, printable keys, all others | any | Not intercepted; native editing and focus movement. | FLD-KEY-03 |

- **INP-KEY-01** — Withdrawn: superseded by FLD-KEY-01, FLD-KEY-04.
- **INP-KEY-02** — Withdrawn: superseded by FLD-KEY-02, FLD-KEY-03.

### 6.2 Pointer and touch

Not applicable — Input attaches no pointer handlers; clicking, selecting and native label activation are browser behavior. (Label pointer behavior belongs to `Field.Label`, FLD-PTR-02, FFI-PTR-02.)

### 6.3 Focus management

Focus tracking is `Field.Control`'s (FLD-FOC-01…07).

- **INP-FOC-01** — Withdrawn: superseded by FLD-FOC-01, FLD-FOC-02, FLD-FOC-03.
- **INP-FOC-02** — Withdrawn: superseded by FLD-FOC-05 (mechanism FFI-FOC-01, FFI-FOC-03).
- **INP-FOC-03** — Withdrawn: superseded by FLD-FOC-07.

### 6.4 Dismissal

Not applicable — no popup.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: string, details: Input.ChangeEventDetails) => void` | Every native change event from a user edit (typing, paste, cut, drag-drop, autofill, programmatic `input` event), controlled or not (FLD-EVT-01…03) | `'none'` | yes (uncontrolled: skips error clearing and validation only; FLD-EVT-04) |

- **INP-EVT-01** — Withdrawn: superseded by FLD-API-17, FLD-EVT-01, FLD-EVT-02.
- **INP-EVT-02** — MUST. Input satisfies FLD-EVT-01 with `Input.ChangeEventDetails` in place of `Field.Control.ChangeEventDetails` (the same type, §3.2). In addition, `allowPropagation()` and `isPropagationAllowed` have no component effect, because the part never stops propagation of the change event.
  Source: `field/control/FieldControl.tsx:125,213-216`, `input/Input.tsx:38,44`, `internals/createBaseUIEventDetails.ts:126-157`; see FLD-EVT-01. Test: none.
- **INP-EVT-03** — Withdrawn: superseded by FLD-EVT-04.
- **INP-EVT-04** — Withdrawn: superseded by FLD-EVT-05.
- **INP-EVT-05** — Withdrawn: superseded by FLD-EVT-03, FLD-FRM-10.
- **INP-EVT-06** — Withdrawn: superseded by FLD-EVT-06, FLD-EVT-07.

## 8. Form integration

Mechanism: [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md). Inside a `Field.Root`, Input's registration, `FormData` participation, validation triggers, form-error handling and reset behavior are `Field.Control`'s ([field](field.md) §8: FLD-FRM-01…14), and the timing of its field writes is FLD-EVT-06/07, FLD-STA-13, FLD-FOC-02 and FLD-KEY-01. This section keeps only what field.md does not state: the behavior of an Input with no `Field.Root` (INP-FRM-07). The controlled `'onBlur'` re-commit is FLD-FRM-17, and a field-less Input's `clearErrors` call inside a `Form` is FLD-FRM-18.

- **INP-FRM-01** — Withdrawn: superseded by FLD-FRM-01, FLD-FRM-11, FLD-FRM-14.
- **INP-FRM-02** — Withdrawn: superseded by FLD-FRM-02, FLD-DOM-09.
- **INP-FRM-03** — Withdrawn: superseded by FLD-FRM-03, FLD-FRM-04, FLD-FRM-05, FLD-FRM-07, FLD-FRM-13.
- **INP-FRM-04** — Withdrawn: superseded by FLD-FRM-17.
- **INP-FRM-05** — Withdrawn: superseded by FLD-FRM-09, FLD-FRM-03.
- **INP-FRM-06** — Withdrawn: superseded by FLD-FRM-10.
- **INP-FRM-07** — MUST. Refining FLD-STR-02 ("behaves as a plain labelled `<input>` with the default field state"): outside a `Field.Root`, Input MUST behave as a plain native input with respect to fields and forms: no registration (so a surrounding `Form` neither validates it on submit nor focuses it nor includes it in the values object; its only form-scope interaction is the `clearErrors(name)` call of FLD-FRM-18), no `data-*` state except `data-disabled`, no `aria-labelledby`/`aria-invalid` additions, `aria-describedby` passed through unchanged, and no validation of any kind by Base UI. Because `Form` renders `novalidate` by default, a bare `<Input required>` inside a `Form` does not block submission either.
  Source: `field/control/FieldControl.tsx:49-60`, `internals/field-root-context/FieldRootContext.ts:37-67`, `form/Form.tsx:108`; see FLD-STR-02. Test: [L2] port title "an input outside a field renders only data-disabled and registers nowhere" (added 2026-10-22); the `Form` clauses wait for track 2.
- **INP-FRM-08** — Withdrawn: superseded by FLD-FRM-18.
- **INP-FRM-09** — Withdrawn: superseded by FLD-ARIA-07, FLD-API-23, FLD-FRM-05, FLD-FRM-06.

## 9. Lifecycle, mounting and animation

Not applicable — Input is always mounted while rendered; no transition statuses. Its timed behaviors are `Field.Control`'s: post-commit, pre-paint registration and `filled`/controlled-change sync (FLD-FRM-01, FLD-STA-13, FLD-EVT-06), the 0 ms Enter deferral (FLD-KEY-01), the blur re-commit microtask (FLD-FRM-17), and the field's debounce timer (FLD-FRM-04), which is cancelled when the element unregisters (FFI-FRM-15).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- INP-API-06 — FFI registration row (post-commit, pre-paint C# state work): the control-id claim runs in `OnAfterRender`; on Server the switch from the scope's generated id to the explicit `id` lands after paint plus a round trip, observable only as latency of the dependent `id`/`for` updates.
- INP-API-07 — FFI registration row: the `focused` flag of an input that is already the active element at mount is acquired in `OnAfterRender`; on Server after paint plus a round trip.
- 2026-10-22 (ADR 01 and ADR 04 Amendments 2026-10-22, settled by the orchestrator 2026-10-22, ADR 02 verification item 1 and ADR 15): the form module acquires INP-API-07's flag when it attaches, focusing an `AutoFocus` input when nothing else has focus. The acquisition still follows the attach, a round trip after the render on Server.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

Input configures every mechanism exactly as `Field.Control` does ([field](field.md) §11); the rows below restate that configuration for reference.

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `Field.Control` | [field](field.md) | `Input` renders `<Field.Control ref={forwardedRef} {...props} />` with no additional props. (`input/Input.tsx:17`) |
| `useRenderElement` | [rendering-and-composition](../architecture/rendering-and-composition.md) | Tag `input`; state = field state with effective `disabled`; `ref: [consumer handle, internal focus-target handle]`; props `[ built-ins (id, disabled, name, validation-input handle, aria-labelledby, autoFocus, value|defaultValue, onChange, onFocus, onBlur, onKeyDown), consumerProps, validation-props function ]`; `stateAttributesMapping: fieldValidityMapping`. (`FieldControl.tsx:111-197`) |
| Control registration | [FFI-ALG-12](../architecture/field-and-form-infrastructure.md), FFI-FRM-17/33 | `(validation.inputRef, id, serializedValue, getValueFromInput, !disabled, nameProp)`. (`FieldControl.tsx:80-87`) |
| Control-id claim | FFI-ALG-06/07 | `useLabelableId({ id: idProp })` — always claims (no `enabled` gating). (`FieldControl.tsx:72`) |
| Focus ownership | FFI-ALG-13, FFI-STA-09 | `useSetFieldFocused(disabled, inputRef)`; no `onFocusedChange`. (`FieldControl.tsx:107-108`) |
| Validation attributes | FFI-ARIA-01/03 | `validation.getValidationProps(disabled, props)` applied last. (`FieldControl.tsx:194`) |
| Value-change detection | [UTL-STA-36](../architecture/shared-utilities.md) | `useValueChanged(serializedValue, …)` for controlled sync. (`FieldControl.tsx:96`) |
| Enter deferral timer | [UTL-EVT-01…03](../architecture/shared-utilities.md) | `useTimeout()`; `start(0, …)`; cleared on unmount. (`FieldControl.tsx:109,182`) |
| Change-event details | [EVD](../architecture/event-details-and-reasons.md) EVD-API-01/05 | `createChangeEventDetails('none', event.nativeEvent)`. (`FieldControl.tsx:125`) |
| Form scope | FFI-STR-04/05 | Reads `clearErrors`, `elementRef`, `submitCountRef`. (`FieldControl.tsx:60`) |

## 12. Edge cases, invariants and dev warnings

Input and `Field.Control` emit **no** dev-only warnings or errors of their own (no `warn(`, `console.error(` or `throw` in `input/Input.tsx` or `field/control/FieldControl.tsx`). Diagnostics a consumer may see come from the framework (switching controlled/uncontrolled) or from RND (invalid `render` element). The edge cases are `Field.Control`'s ([field](field.md) §12).

- **INP-EDGE-01** — Withdrawn: superseded by FLD-API-15, FLD-EDGE-10.
- **INP-EDGE-02** — Withdrawn: superseded by FLD-EDGE-10.
- **INP-EDGE-03** — Withdrawn: superseded by FLD-EDGE-12, FLD-DOM-11.
- **INP-EDGE-04** — Withdrawn: superseded by FLD-STA-13, FLD-STA-14, FLD-FRM-02.
- **INP-EDGE-05** — Withdrawn: superseded by FLD-EDGE-13.
- **INP-EDGE-06** — Withdrawn: superseded by FLD-API-19, FLD-DOM-10, FLD-FRM-11, FLD-FOC-03, FLD-STA-09.
- **INP-EDGE-07** — Withdrawn: superseded by FLD-API-20, FLD-DOM-06, FLD-FRM-14.
- **INP-EDGE-08** — MUST. Refining FLD-EVT-02 and FLD-EVT-07: browser autofill into the element MUST be handled as an ordinary change event (FFI-FRM-31): `onValueChange` fires and the uncontrolled change sequence runs.
  Source: see [FFI-FRM-31](../architecture/field-and-form-infrastructure.md). Test: none for Input.

**RTL / server rendering.** Per FLD-EDGE-08 and FLD-EDGE-09. Server markup carries the scope's generated id, `name`, `disabled`, default value and forwarded attributes; `aria-labelledby`/`aria-describedby` appear only after hydration when the label/description register (FFI-FRM-34), and an autofocused element acquires `focused` post-hydration (FLD-FOC-04).

## 13. Behaviors evidenced by tests

Mapping of every test in `input/` to requirements, plus the `FieldControl.test.tsx` tests that evidence shared behavior (they run against the same implementation and are cited in the `Test:` lines above). One ID is introduced here for the conformance suite. Rows for behavior now owned by field.md cite the superseding FLD IDs (INP-STR-04).

### `Input.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLInputElement`, default `render` (standalone, no `Field.Root`), default `testRenderPropWith: 'div'`) | INP-DOM-05, INP-STR-01, INP-STR-02, INP-STR-03, INP-API-08, INP-API-09 |

### `Input.spec.tsx` (type-level)

| Check | Requirements |
|---|---|
| `<Input ref={React.useRef<HTMLTextAreaElement>(null)} render={<textarea />} />` type-checks (handle typed `HTMLElement`) | INP-API-09 |

### `FieldControl.test.tsx` (shared implementation)

| Test | Requirements |
|---|---|
| "avoids rerendering for uncontrolled input changes" / "renders once per keystroke for controlled input changes" | FLD-EDGE-13 |
| "validates once when changed by the user" | FLD-EVT-02, FLD-EVT-07 |
| "validates once when a controlled value is changed by the user" | INP-API-01, FLD-EVT-06 |
| "clears dirty state when a numeric controlled value returns to its initial value" | FLD-API-15, FLD-STA-14, FLD-EDGE-10 |
| "syncs state and validates when the controlled value changes programmatically" | FLD-EVT-06, FLD-EVT-03 |
| "validates the final controlled value when it is normalized on blur" / "keeps the final async validation when a controlled value is normalized on blur" / "does not validate when a controlled value is reset to the initial value on blur" | FLD-FRM-17 |
| "sets filled state on mount when the control is prefilled" / "does not set filled state on mount for an empty controlled value" | FLD-STA-13 |
| "clears filled state when a controlled control remounts empty" / "clears filled state when an uncontrolled control remounts empty" | FLD-STA-13 |
| "forwards a changing defaultValue without warning" / "keeps filled state from the edited value when defaultValue changes" | INP-API-02 |
| "sets filled state from a controlled value on a custom element" | INP-API-09 |
| "does not validate when the change is canceled" | FLD-EVT-04 |
| "does not clear errors or validate when change is prevented" | FLD-EVT-05 |
| "validates once when Enter implicitly submits a form" / "validates when Enter does not implicitly submit the form" / "validates when a disabled submit button blocks implicit submission" / "validates the latest value when Enter does not submit the form" / "validates when Enter is pressed outside a form" | FLD-KEY-01 |
| "shows a required error when a prefilled value is cleared" | FLD-EVT-07, FLD-FRM-04 |
| [data-focused] "is removed when the focused control becomes disabled" / "is removed when the field root becomes disabled" / "can be re-acquired after the control is re-enabled" / "is removed when the focused control unmounts" | FLD-FOC-03, FLD-FOC-04 |
| "should sync focused state when autoFocus is used with SSR" | INP-API-07 |
| id — "updates the label association when the control is swapped" | INP-API-06, FLD-API-20, FLD-DOM-06 |

- **INP-DOM-05** — MUST. Input MUST pass the shared conformance suite of [RND §4.9](../architecture/rendering-and-composition.md) (RND-DOM-36…48): custom attributes forwarded to the default `<input>` and to a substituted `div` (element and callback forms), `style` from the part and from the substitute both applied, the DOM handle resolving to an `HTMLInputElement`, wrapper substitution allowed, both DOM handles receiving the element, and `className` string/function merged with the substitute's class. Input does not accept `nativeButton`, so RND-DOM-49 does not apply. The suite runs Input standalone, so it also evidences INP-STR-03 (no `Field.Root` required).
  Source: `test/describeConformance.tsx:55-78`. Test: [L2] `Input.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Alias by composition.** `Input` is a `forwardRef` component that returns `<Field.Control ref={forwardedRef} {...props} />`; there is no separate state, context or effect. A port can expose `Input` as a type alias or thin wrapper over its `Field.Control` implementation; keeping two copies would risk divergence. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 4 (parts are C# classes over `PartBase<TState>`) and decisions/04-js-interop-boundary.md §1 (field infrastructure is C#).
- **Controlled/uncontrolled is decided per render** (`isControlled = value !== undefined`, `FieldControl.tsx:74`), not latched like `useControlled`-based components (Switch, Checkbox). React itself warns when an input switches between the two; Base UI adds no warning. A port with a latched model should document the difference (§16). Port: see decisions/05-state-store-and-event-details.md §2 (latched at first parameter set; conflict recorded in §16 item 3).
- **`onChange` quirk (facebook/react#9023).** React fires `onChange` even for `input` events whose default was prevented, hence the `event.nativeEvent.defaultPrevented` check (`FieldControl.tsx:139`). The requirement is FLD-EVT-05 (formerly INP-EVT-04). Port: see decisions/04-js-interop-boundary.md §3 (the event snapshot carries `DefaultPrevented`).
- **Controlled sync is a layout effect** (`useValueChanged`, `useIsoLayoutEffect`): the `clearErrors → setDirty → change` sequence for controlled values runs post-commit, pre-paint, once per actual string change, including programmatic ones. `filled` is a separate layout effect keyed on `[serializedValue, defaultValue]`. Uncontrolled edits run the same writes synchronously in the event handler because no re-render carries the value. Port: see decisions/05-state-store-and-event-details.md §1 (prop-to-store sync in `OnParametersSet`) and decisions/01-hosting-model-and-timing.md §Relaxation table ("FFI registration" row).
- **Enter deferral** uses `useTimeout` (cleared on unmount) and compares the Form's submission counter captured at keydown with its value when the 0 ms timer fires; implicit submission runs between the two. Ports need the same "did a submit happen in between" check rather than a fixed delay. Port: see decisions/04-js-interop-boundary.md §1 (timers whose result is state are C#) and decisions/01-hosting-model-and-timing.md §Decision ("macrotask" row).
- **Focus ownership token.** `useSetFieldFocused` uses the identity of its stable callback as the owner token; the layout effect re-runs on `disabled` changes to release the flag without a blur event and reclaims focus on StrictMode re-runs or after hydration when the element is already `activeElement`. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table ("FFI registration" row) for the post-commit, pre-paint flag writes.
- **`autoFocus`** is forwarded as the React prop (React focuses on mount rather than emitting the attribute in client renders; in server markup the attribute is emitted), which is why the SSR test checks `focused` after hydration.
- **`suppressHydrationWarning`** is not used here (unlike hidden inputs of other controls) because the element's value is the consumer's own.
- **React 17.** The generated id is `undefined` on the first render and assigned after mount (`packages/utils/src/useId.ts:9-31`); `useLabelableId` treats `undefined` as "not yet available" and does not register (`useLabelableId.ts:54-57`). Ports with synchronous ids can ignore this. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| INP-STR-01 | MUST | Single `<input>` element; it is the form-participation and validation input | 2 |
| INP-STR-02 | MUST | Identical to `Field.Control`; props and handle passed through | 2 |
| INP-STR-03 | MUST | Works standalone, in `Field.Root`, in `Form`; never throws | 2 |
| INP-STR-04 | MUST | Satisfies the `Field.Control` FLD IDs with Input name substitutions | 2 |
| INP-API-01 | MUST | Controlled per render when `value !== undefined`; edits reported only via `onValueChange` | 3.1 |
| INP-API-02 | MUST | `defaultValue` forwarded when uncontrolled; later changes forwarded, `filled` from current value | 3.1 |
| INP-API-03 | MUST | `onValueChange(value: string, details)` | 3.1 |
| INP-API-04 | MUST | Name = field name ?? prop; prop registered as fallback | 3.1 |
| INP-API-05 | MUST | `disabled` default `false`; effective = field OR prop; native attribute | 3.1 |
| INP-API-06 | MUST | `id` = prop, else field control id, else generated; atomic fallback | 3.1 |
| INP-API-07 | MUST | `autoFocus` default `false`; already-focused element acquires `focused` | 3.1 |
| INP-API-08 | MUST | `type` and native attributes forwarded; no `type` set; constraints read natively | 3.1 |
| INP-API-09 | MUST | `className`/`style`/`render` per RND; `HTMLElement` handle; non-input substitutes skip Enter/select | 3.1 |
| INP-API-10 | MUST | Consumer handlers merged, run first, may veto | 3.1 |
| INP-STA-01 | MUST | State object = six field fields; defaults outside a field | 3.1 |
| INP-STA-02 | — | Withdrawn: superseded by FLD-EVT-02, FLD-EVT-07 | 4 |
| INP-STA-03 | — | Withdrawn: superseded by FLD-API-15, FLD-FRM-01, FLD-FRM-02 | 4 |
| INP-STA-04 | — | Withdrawn: superseded by FLD-API-15, FLD-STA-12, FLD-EVT-06 | 4 |
| INP-STA-05 | — | Withdrawn: superseded by FLD-STA-13 | 4 |
| INP-DOM-01 | — | Withdrawn: superseded by FLD-DOM-08, FLD-DOM-09, FLD-DOM-10, FLD-DOM-11, FLD-DOM-12, FLD-API-23 | 5.1 |
| INP-DOM-02 | — | Withdrawn: superseded by FLD-DOM-04, FLD-STR-02 | 5.1 |
| INP-DOM-03 | — | Withdrawn: superseded by FLD-API-23, FLD-ARIA-01, FLD-ARIA-02, FLD-ARIA-03 | 5.1 |
| INP-DOM-04 | — | Withdrawn: superseded by FLD-FRM-02 | 5.1 |
| INP-DOM-05 | MUST | Passes the RND conformance suite (standalone) | 13 |
| INP-ARIA-01 | — | Withdrawn: superseded by FLD-ARIA-01 | 5.3 |
| INP-ARIA-02 | — | Withdrawn: superseded by FLD-ARIA-02 | 5.3 |
| INP-ARIA-03 | — | Withdrawn: superseded by FLD-ARIA-03 | 5.3 |
| INP-ARIA-04 | — | Withdrawn: superseded by FLD-ARIA-07 | 5.3 |
| INP-ARIA-05 | — | Withdrawn: superseded by FLD-DOM-06, FLD-API-20 | 5.3 |
| INP-KEY-01 | — | Withdrawn: superseded by FLD-KEY-01, FLD-KEY-04 | 6.1 |
| INP-KEY-02 | — | Withdrawn: superseded by FLD-KEY-02, FLD-KEY-03 | 6.1 |
| INP-FOC-01 | — | Withdrawn: superseded by FLD-FOC-01, FLD-FOC-02, FLD-FOC-03 | 6.3 |
| INP-FOC-02 | — | Withdrawn: superseded by FLD-FOC-05 | 6.3 |
| INP-FOC-03 | — | Withdrawn: superseded by FLD-FOC-07 | 6.3 |
| INP-EVT-01 | — | Withdrawn: superseded by FLD-API-17, FLD-EVT-01, FLD-EVT-02 | 7 |
| INP-EVT-02 | MUST | Satisfies FLD-EVT-01 (Input type names); `allowPropagation()` no effect | 7 |
| INP-EVT-03 | — | Withdrawn: superseded by FLD-EVT-04 | 7 |
| INP-EVT-04 | — | Withdrawn: superseded by FLD-EVT-05 | 7 |
| INP-EVT-05 | — | Withdrawn: superseded by FLD-EVT-03, FLD-FRM-10 | 7 |
| INP-EVT-06 | — | Withdrawn: superseded by FLD-EVT-06, FLD-EVT-07 | 7 |
| INP-FRM-01 | — | Withdrawn: superseded by FLD-FRM-01, FLD-FRM-11, FLD-FRM-14 | 8 |
| INP-FRM-02 | — | Withdrawn: superseded by FLD-FRM-02, FLD-DOM-09 | 8 |
| INP-FRM-03 | — | Withdrawn: superseded by FLD-FRM-03, FLD-FRM-04, FLD-FRM-05, FLD-FRM-07, FLD-FRM-13 | 8 |
| INP-FRM-04 | — | Withdrawn: superseded by FLD-FRM-17 | 8 |
| INP-FRM-05 | — | Withdrawn: superseded by FLD-FRM-09, FLD-FRM-03 | 8 |
| INP-FRM-06 | — | Withdrawn: superseded by FLD-FRM-10 | 8 |
| INP-FRM-07 | MUST | Outside `Field.Root`: plain native input; no Form validation/focus/values (refines FLD-STR-02) | 8 |
| INP-FRM-08 | — | Withdrawn: superseded by FLD-FRM-18 | 8 |
| INP-FRM-09 | — | Withdrawn: superseded by FLD-ARIA-07, FLD-API-23, FLD-FRM-05, FLD-FRM-06 | 8 |
| INP-EDGE-01 | — | Withdrawn: superseded by FLD-API-15, FLD-EDGE-10 | 12 |
| INP-EDGE-02 | — | Withdrawn: superseded by FLD-EDGE-10 | 12 |
| INP-EDGE-03 | — | Withdrawn: superseded by FLD-EDGE-12, FLD-DOM-11 | 12 |
| INP-EDGE-04 | — | Withdrawn: superseded by FLD-STA-13, FLD-STA-14, FLD-FRM-02 | 12 |
| INP-EDGE-05 | — | Withdrawn: superseded by FLD-EDGE-13 | 12 |
| INP-EDGE-06 | — | Withdrawn: superseded by FLD-API-19, FLD-DOM-10, FLD-FRM-11, FLD-FOC-03, FLD-STA-09 | 12 |
| INP-EDGE-07 | — | Withdrawn: superseded by FLD-API-20, FLD-DOM-06, FLD-FRM-14 | 12 |
| INP-EDGE-08 | MUST | Autofill handled as an ordinary change (refines FLD-EVT-02/07) | 12 |

## 16. Open questions

1. **Relationship to `Field.Control` is total** (`input/Input.tsx:17`); this document therefore cites `field/control/FieldControl.tsx` for every behavior and `FieldControl.test.tsx` for evidence, even though those tests live outside `input/`. The [field](field.md) document (another author) will specify `Field.Control` under FLD IDs; the two documents should be reconciled at review so that each observable statement has one owner. Suggested rule: FLD owns the normative text, INP keeps the alias statement (INP-STR-02), the Input-specific type declarations (§3.2) and the §13 matrix, and cites FLD IDs. Resolved (QA 2026-10-14): field.md owns the normative `Field.Control` text; INP-STR-04 adopts those FLD IDs with Input name substitutions, §4–§12 INP IDs fully covered by FLD are withdrawn (each naming its superseding FLD IDs), and only Input deltas remain (INP-FOC-03, INP-EVT-02, INP-FRM-04, INP-FRM-07, INP-FRM-08, INP-EDGE-08). (Review 2026-10-18: field.md's QA pass added FLD-FOC-06/07 and FLD-FRM-15…18; INP-STR-04 now adopts FLD-FOC-01…07 and FLD-FRM-01…18, and INP-FOC-03, INP-FRM-04 and INP-FRM-08 are withdrawn as superseded by FLD-FOC-07, FLD-FRM-17 and FLD-FRM-18. Remaining deltas: INP-EVT-02, INP-FRM-07, INP-EDGE-08.)
2. **`cancel()` does not suppress `dirty`/`filled`** for uncontrolled edits (`FieldControl.tsx:134-142` writes them before the cancel/prevented check). This contradicts the general rule EVD-EVT-08 ("a canceled change MUST suppress … field `dirty`/`filled`/`touched` updates") and FFI-EVT-02's phrasing. Specified as implemented (INP-EVT-03/04); no test asserts `data-dirty`/`data-filled` after a cancel. Flag for the EVD and FLD authors. (QA 2026-10-14: INP-EVT-03/04 withdrawn; field.md now states the same behavior in FLD-EVT-04/05 and tracks it as its Q4. The EVD-EVT-08/FFI-EVT-02 wording fix remains open.) Resolved (EVD side): EVD-EVT-08 now states that an uncontrolled `Field.Control` suppresses only `clearErrors` and validation on cancel.
3. **Controlled/uncontrolled mode is not latched** (`FieldControl.tsx:74`), unlike every `useControlled`-based component. Switching `value` between `undefined` and a string mid-life changes the element binding on the next render (React warns). Documented in INP-API-01 and FLD-STA-12; whether a port should latch is a product decision. Port: decisions/05-state-store-and-event-details.md §2 fixes controlled-ness at the first parameter set for every value; applied to `Input`/`Field.Control` that relaxes INP-API-01 and FLD-STA-12, which needs the user's confirmation. Decided (2026-10-18): ADR 05 §2 applies uniformly; relaxed for the port (ADR 05 Amendments item 1).
4. **`name`, `disabled`, `id`, `autoFocus` and `type` are absent from the API outline** (`components/page.mdx:915` lists `className, defaultValue, onValueChange, render, style, value`) because the generator omits native attributes; all five are intercepted or defaulted by `FieldControl.tsx:38-44` and are documented in §3.1.
   Resolved 2026-10-22: the port declares `Name`, `Disabled` and `AutoFocus` as typed parameters re-applied with their effective values, takes `id` from the forwarded attributes through the control-id claim, and forwards `type` (ADR 08 Amendments 2026-10-22; the XML documentation lists them).
5. **Bare `Input` inside `Form`** (INP-FRM-07, FLD-FRM-18): with `novalidate` on the form and no registration, a `required` bare Input neither blocks submission nor is validated natively; and its edits still call `clearErrors(nameProp)` (`FieldControl.tsx:60,101,140`), which can remove a server error displayed by a *different* `Field.Root` of the same name. Both follow from the code; untested.
   Resolved 2026-10-22: the port follows the source (INP-FRM-07, FLD-FRM-18); the `Form` half is tested with track 2.
6. **`value={null}`** (INP-EDGE-02, now FLD-EDGE-10) and **array values** (INP-EDGE-01, now FLD-API-15/FLD-EDGE-10) are type-permitted but untested; the generated docs show `value: string | string[] | number`.
   Resolved 2026-10-22: `Value` is `string?` (ADR 08 Amendments 2026-10-22), so array and numeric values do not arise. A controlled `null` is FLD-EDGE-10's `undefined`: the module does not re-assert it, and the element keeps what it shows, as React treats `value={null}` as uncontrolled.
7. **`onValueChange` on controlled prop changes** (INP-EVT-05, now FLD-EVT-03): no test asserts that the callback is *not* called on a programmatic `value` change; the statement follows from `FieldControl.tsx:96-105` calling only `clearErrors`/`setDirty`/`validation.change`.
   Resolved 2026-10-22: a programmatic `Value` change commits through clearErrors, setDirty and change without calling `OnValueChange`; tested by "syncs state and validates when the controlled value changes programmatically" over both parts.
8. **Consumer `aria-labelledby` override** (INP-ARIA-01, now FLD-ARIA-01) and **consumer `id`/`name` override** (INP-DOM-03, now FLD-API-23) follow from prop order plus RND-DOM-19 and are untested; note that an explicit `aria-labelledby={undefined}` would also remove the field label association. Resolved (explicit-`undefined` clause only): not expressible in the port, see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-19).
   Resolved 2026-10-22 (the remaining clauses): a consumer `aria-labelledby` or `id` follows the pipeline's merge order (RND-DOM-19, the consumer last), which the conformance suite tests on `Input`. A consumer `name` is the typed `Name` parameter, which is re-applied with its effective value (FLD-API-18).
