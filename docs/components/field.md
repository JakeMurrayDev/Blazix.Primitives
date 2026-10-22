# Field

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/field/` |
| Tests | `packages/react/src/field/**/*.test.tsx` (8 files) + `field/control/FieldControl.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/field/page.mdx` |
| Prefix | `FLD` |
| Status | approved |
| Revised | 2026-10-22 |
| Builds on | none |
| Derived by | [input](input.md) (`Input` is an alias of `Field.Control`, `input/Input.tsx:13-18`) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Field groups one form control with the pieces that give it an accessible name, an accessible description, a validation message and styling hooks. `Field.Root` is the grouping element and the owner of the field's state: whether the control has been touched, whether its value is dirty, filled or focused, and whether it is valid. `Field.Label` is a `<label>` wired to the control; `Field.Description` is a paragraph announced as the control's description; `Field.Error` is a message that appears only while the field is invalid (with an enter/exit transition); `Field.Validity` is a render function that exposes the raw validity data; `Field.Item` wraps one option of a checkbox or radio group so that the option gets its own label and description. `Field.Control` is a native `<input>` that participates in the field; it may be omitted and replaced by any Base UI form control (Checkbox, Switch, Radio Group, Select, Combobox, Number Field, Slider, OTP Field, …), each of which registers with the field the same way.

What the user experiences: the label focuses or activates the control; the control reports its validity through `aria-invalid`, `aria-describedby` and the `data-valid`/`data-invalid` attributes that every part carries; the error message appears when validation fails at the configured moment (`validationMode`: on submit, on blur or on change) and disappears, animated, when the value becomes valid again; server-side errors supplied through `Form.errors` are shown by the same error part and cleared as soon as the value changes.

Field builds on no other component. The internal contract between `Field.Root` and every control (registration, the validation pipeline, native constraint handling, `Form` integration, label/description id wiring) is owned by [FFI](../architecture/field-and-form-infrastructure.md) and is only cited here; this document owns the public parts: what each renders, its props, its data attributes and the behavior observable through the parts themselves. `Input` (`input/Input.tsx`) renders `Field.Control` unchanged and is specified in [input](input.md).

## 2. Anatomy

```
<Field.Root>                     required, 1            default element: div
  <Field.Label>                  optional, 0..n         default element: label
  <Field.Control>                optional, 0..1 active  default element: input   (or any Base UI form control)
  <Field.Description>            optional, 0..n         default element: p
  <Field.Error>                  optional, 0..n         default element: div     (mounted only while it has a message to show)
  <Field.Validity>               optional, 0..n         renders no element (render function)
  <Field.Item>                   optional, 0..n         default element: div     (opens a nested label/description scope)
    <Field.Label>                optional               (labels the control inside this item)
    <Field.Description>          optional               (describes the control inside this item)
    <Checkbox.Root> / <Radio.Root> …                    (a group member; registers in the item's scope)
```

Parts may appear in any document order and at any depth below `Field.Root` (including through portals). The order shown is conventional, not required; the hero demo places `Field.Error` before `Field.Description`.

- **FLD-STR-01** — MUST. `Field.Root` MUST render a `<div>` and MUST provide the field scope and a labelable scope (FFI-STR-01, FFI-STR-03) to every descendant, including descendants rendered through a portal.
  Source: `field/root/FieldRoot.tsx:188-195,204-213`. Test: [L2] `FieldRoot.test.tsx` "Base UI component API" (conformance), "should apply [data-field] style hooks to field components".
- **FLD-STR-02** — MUST. `Field.Label`, `Field.Description`, `Field.Error`, `Field.Validity` and `Field.Item` MUST throw the missing-scope error (FFI-STR-02, FFI-EDGE-01) when rendered outside a `Field.Root`; `Field.Control` MUST NOT throw and instead behaves as a plain labelled `<input>` with the default (all-false) field state.
  Source: `field/label/FieldLabel.tsx:33`, `field/description/FieldDescription.tsx:27`, `field/error/FieldError.tsx:37`, `field/validity/FieldValidity.tsx:18`, `field/item/FieldItem.tsx:29`, `field/control/FieldControl.tsx:49-59`, `internals/field-root-context/FieldRootContext.ts:71-81`. Test: none.
- **FLD-STR-03** — MUST. `Field.Control` is optional: any Base UI form control placed under `Field.Root` registers as the field's control per the registration matrix (FFI §6.3). When several controls are present, at most one is the *active* control at a time (the most recently registered, FFI-STR-07) and the label association keeps the first registered control id that is still registered (FFI-ALG-06).
  Source: `field/control/FieldControl.tsx:80-87`, `internals/field-register-control/useFieldControlRegistration.ts:133-169`, `internals/labelable-provider/LabelableProvider.tsx:26-60`. Test: [L2] `FieldLabel.test.tsx` "keeps the selected control id when another control unmounts", "falls over to the remaining control when the selected one unmounts"; `FieldRoot.test.tsx` "updates label association when replacing one control with another", "submits the replacement control value when swapping field-aware controls".
- **FLD-STR-04** — MUST. `Field.Item` MUST open a nested labelable scope: a `Field.Label` and `Field.Description` inside the item associate with the control inside the same item (not with the field's active control), and that control's described-by list is the parent scope's message ids followed by the item scope's message ids (FFI-STR-03, FFI-ALG-09).
  Source: `field/item/FieldItem.tsx:43-47`, `internals/labelable-provider/LabelableProvider.tsx:24,68-81`. Test: [L2] `FieldItem.test.tsx` "associates a Field.Item label with a parent checkbox"; `FieldRoot.test.tsx` "receives all form values as the 2nd argument" (items inside a checkbox group).
- **FLD-STR-05** — MUST. `Field.Validity` MUST render no element of its own: its output is exactly what its `children` function returns, placed inline where the part sits.
  Source: `field/validity/FieldValidity.tsx:38`. Test: [L2] `FieldRoot.test.tsx` "does not publish errors while `valueMissing` is suppressed" (function returns `null`, nothing is rendered).
- **FLD-STR-06** — MUST. `Field.Item` is structural: beyond opening the nested scope and publishing its `disabled` flag to the parts and controls inside it, it MUST attach no handlers, ARIA attributes or id of its own.
  Source: `field/item/FieldItem.tsx:36-41`. Test: [L2] `FieldItem.test.tsx` "Base UI component API", "reflects disabled state on the item".

If `Field.Label` is omitted the control has no Base UI-provided accessible name (a consumer `aria-label`/`aria-labelledby` on the control is forwarded unchanged). If `Field.Description` and `Field.Error` are both omitted the control carries no `aria-describedby` beyond what the consumer passes. If `Field.Error` is omitted validation still runs and still flips `data-valid`/`data-invalid` and `aria-invalid`. Several `Field.Description`s each publish their id (document order of mounting); several `Field.Error`s each decide independently whether they render and publish their id only while rendered. Several `Field.Label`s in one scope share one label id (see §12, FLD-EDGE-06).

## 3. Public API

### 3.1 `Field.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `name` | `string` | `undefined` (falls back to the control's own `name`) | Identifies the field in `Form.Values`, `Form.errors` and as the `name` attribute of the active control. Takes precedence over the control's `name`. | FLD-API-01 |
| `disabled` | `boolean` | `false` | Disables the field: every part reflects it and every control inside is disabled. Combined (OR) with an enclosing disabled `Fieldset.Root`. | FLD-API-02 |
| `invalid` | `boolean` | `undefined` | Externally controlled invalidity (for form libraries). Only `true` has an effect. | FLD-API-03 |
| `validate` | `(value: unknown, formValues: Form.Values) => string \| string[] \| null \| void \| Promise<…>` | `() => null` | Custom validator; a string or non-empty array of strings is an error list, anything else is "valid". | FLD-API-04 |
| `validationMode` | `'onSubmit' \| 'onBlur' \| 'onChange'` | the enclosing `Form`'s `validationMode`, else `'onSubmit'` | When the field validates (§8, FLD-FRM-03). | FLD-API-05 |
| `validationDebounceTime` | `number` (ms) | `0` | Delay between a value change and the validator call while change-validation is active. | FLD-API-06 |
| `dirty` | `boolean` | `undefined` (component-owned) | Externally controlled dirty flag. | FLD-API-07 |
| `touched` | `boolean` | `undefined` (component-owned) | Externally controlled touched flag. | FLD-API-07 |
| `actionsRef` | `{ current: Field.Root.Actions \| null }` | — | Receives `{ validate() }`. | FLD-API-08 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FLD-API-09 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element. | FLD-API-10 |

**Consumed (not forwarded to the element):** `render`, `className`, `style`, `validate`, `validationDebounceTime`, `validationMode`, `name`, `disabled`, `invalid`, `dirty`, `touched`, `actionsRef`.

- **FLD-API-01** — MUST. The field's effective name MUST be `name ?? <name registered by the active control>` (FFI-STA-10). `name` MUST NOT be rendered as an attribute of the root element; it reaches the DOM only through the active control's own `name` attribute.
  Source: `field/root/FieldRoot.tsx:34,61-62,156`. Test: [L2] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values", "uses the Field.Control name fallback when the Field.Root name is removed"; `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors".
- **FLD-API-02** — MUST. `disabled` defaults to `false`; the field's effective disabled state MUST be `fieldsetDisabled || disabled` (FFI-STR-06) and MUST be reflected by every part's `data-disabled` and by every control inside the field (FFI-STA-11). `disabled` MUST NOT be rendered as an attribute of the root element.
  Source: `field/root/FieldRoot.tsx:35,44,48,114`. Test: [L2] `FieldRoot.test.tsx` "should add data-disabled style hook to all components"; `FieldControl.test.tsx` "is removed when the field root becomes disabled".
- **FLD-API-03** — MUST. `invalid` MUST be honored only when strictly `true`; the field is then invalid regardless of native constraints and `validate` (FFI-API-05, FFI-STA-02), it blocks form submission (FFI-FRM-27), and it stays invalid while disabled.
  Source: `field/root/FieldRoot.tsx:36,97,110`. Test: [L2] `FieldRoot.test.tsx` "keeps an explicitly invalid field marked invalid while disabled"; `FieldError.test.tsx` conformance suite (rendered under `<Field.Root invalid>`).
- **FLD-API-04** — MUST. `validate` defaults to a function returning `null`. It MUST be called as `validate(value, formValues)` (FFI-EVT-05) and its result MUST be normalized per FFI-API-01 (falsy → valid; string → one error; array → errors with empty strings removed; a promise of any of these → asynchronous validation, FFI-FRM-11). The latest `validate` passed is the one invoked, even when the validation was scheduled (debounced) under an earlier one.
  Source: `field/root/FieldRoot.tsx:31,46,278-290`, `field/root/useFieldValidation.ts:300-332`. Test: [L2] `FieldRoot.test.tsx` "treats $label result as valid" (four variants), "accepts synchronous and async validators with no return value", "receives all form values as the 2nd argument".
- **FLD-API-05** — MUST. `validationMode` accepts exactly `'onSubmit'`, `'onBlur'`, `'onChange'`; when omitted it MUST equal the enclosing `Form`'s `validationMode`, and `'onSubmit'` outside a `Form` (FFI-API-02). The trigger timing for each value is FLD-FRM-03.
  Source: `field/root/FieldRoot.tsx:26,33,291-301`, `internals/form-context/FormContext.ts:42`, `form/Form.tsx:25`. Test: [L2] `FieldRoot.test.tsx` "prop: validationMode" (all tests); precedence over `Form`: none.
- **FLD-API-06** — MUST. `validationDebounceTime` defaults to `0`. A positive value delays only change-triggered commits with a non-empty value while change-validation is active, restarting on every change (FFI-API-03, FFI-FRM-02); it never delays blur, submit, Enter or imperative validation.
  Source: `field/root/FieldRoot.tsx:32,302-307`, `field/root/useFieldValidation.ts:356-364`. Test: [L2] `FieldRoot.test.tsx` "should debounce validation", "should debounce validation for field-aware controls", "should debounce validation for radio groups".
- **FLD-API-07** — MUST. When `dirty` (resp. `touched`) is not `undefined`, the field's dirty (resp. touched) flag MUST equal the prop on every render and MUST NOT change through user interaction (FFI-API-05); a defined `dirty` prop also drives the dirty latch used for `valueMissing` suppression (FFI-STA-06). There is no `defaultDirty`/`defaultTouched`: when the prop is `undefined` the flag is component-owned and starts `false`.
  Source: `field/root/FieldRoot.tsx:37-38,50-51,56-57,64-86`. Test: [L2] `FieldRoot.test.tsx` "controls the dirty state", "does not update controlled dirty state from user input", "uses the controlled dirty state for required validation", "controls the touched state", "does not update controlled touched state on blur".
- **FLD-API-08** — MUST. When `actionsRef` is given, its `current` MUST be set to an object `{ validate }` whose `validate()` runs the imperative validation of FFI-FRM-21 (sets the dirty latch, commits the active control's current value, or the last committed value when no control is mounted). The object identity MUST be stable for the lifetime of the `Field.Root`.
  Source: `field/root/FieldRoot.tsx:39,149-151,235-237,323-327`. Test: [L2] `FieldRoot.test.tsx` "validates the field when the `validate` method is called", "validates a logical field without a mounted control", "validates the current control value when the `validate` method is called"; `FieldRoot.react17.test.tsx` "allows mount-time imperative validation before the fallback id is assigned".
  Port note 2026-10-22 (ADR 08 Amendments 2026-10-22): Base UI's `actionsRef` is `public Task FieldRoot.ValidateAsync()`, reached through `@ref`, so no object identity arises. The task completes after the active control has reported its current value and validity, which C# asks for through `update` (no event started this commit, design point DP6 of M2 track 1, settled by the orchestrator 2026-10-22), and the validation has finished. A validator's exception faults the task, and the field keeps its previous verdict. "allows mount-time imperative validation before the fallback id is assigned" renders `Field.Error` and lands with PR 1c.
- **FLD-API-09** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md), receiving the state object of §3.1; none of them is forwarded as a plain attribute.
  Source: `field/root/FieldRoot.tsx:28-30,40,188-193`. Test: [L2] `FieldRoot.test.tsx` "Base UI component API".
- **FLD-API-10** — MUST. Every prop not in the consumed list MUST be forwarded to the root `<div>` (including `id`, `data-*`, `aria-*`, `children` and handlers); a consumer attribute with the same name as a state data attribute overrides it (RND-DOM-10).
  Source: `field/root/FieldRoot.tsx:41,191`. Test: [L2] `FieldRoot.test.tsx` "Base UI component API" (prop forwarding), "should add data-disabled style hook to all components" (`data-testid`).

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes. It is also the object every other part spreads into its own state.

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (`Fieldset.Root` disabled OR `disabled` prop). |
| `touched` | `boolean` | The control has reached its touched boundary at least once (blur, or Enter in `Field.Control`); or the `touched` prop. |
| `dirty` | `boolean` | The control's value differs from the value captured at first registration; or the `dirty` prop. |
| `valid` | `boolean \| null` | Combined validity: `null` before any validation, while an asynchronous validator is pending, or while the field is disabled (unless externally invalid); `false` when invalid; `true` when validated and valid. |
| `filled` | `boolean` | The control reports a value (`Field.Control`: non-empty string). |
| `focused` | `boolean` | The control currently owns focus (never `true` while disabled). |

- **FLD-STA-01** — MUST. The root state object MUST contain exactly the six fields above (FFI-STA-01), with `valid` derived as in FLD-STA-09 and `disabled` as in FLD-API-02; the object identity MUST change only when one of the six values changes.
  Source: `field/root/FieldRoot.tsx:112-122,239-264`. Test: [L2] `FieldItem.test.tsx` "reflects disabled state on the item" (render-function state), `FieldValidity.test.tsx` "should pass validity data" (`valid` is `null` before validation).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while `disabled` | FLD-DOM-02 |
| `data-touched` | `""` while `touched` | FLD-DOM-02 |
| `data-dirty` | `""` while `dirty` | FLD-DOM-02 |
| `data-filled` | `""` while `filled` | FLD-DOM-02 |
| `data-focused` | `""` while `focused` | FLD-DOM-02 |
| `data-valid` | `""` while `valid === true` | FLD-DOM-03 |
| `data-invalid` | `""` while `valid === false` | FLD-DOM-03 |

#### CSS variables

None.

### 3.2 `Field.Label`

**Default element:** `<label>`. **Accepts `render` element substitution:** yes (set `nativeLabel={false}` when the substitute is not a `<label>`). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeLabel` | `boolean` | `true` | Whether the rendered element is a native `<label>` (association through `for`) or not (association through the control's `aria-labelledby`, click focuses the control). | FLD-API-11 |
| `id` | `string` | generated (`base-ui-…`) | The label's id; published to the scope so controls can reference it. Not listed in the API outline (native attribute). | FLD-API-12 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FLD-API-13 |
| *other `<label>` attributes and handlers* | — | — | Forwarded to the label element; `htmlFor` from the consumer overrides the automatic one. | FLD-API-14 |

**Consumed (not forwarded to the element):** `render`, `className`, `style`, `id` (re-applied as the resolved id), `nativeLabel`.

- **FLD-API-11** — MUST. `nativeLabel` defaults to `true`. When `true` the label MUST render `for={controlId}` and the native double-click guard (FFI-ARIA-04, FFI-PTR-02); when `false` it MUST render no `for`, MUST prevent the default of `pointerdown`, and MUST focus the control on `click` (FFI-PTR-01). A mismatch between the prop and the rendered element is reported in development (FLD-EDGE-02).
  Source: `field/label/FieldLabel.tsx:29,43-46`, `internals/labelable-provider/useLabel.ts:64-76`. Test: [L2] `FieldLabel.test.tsx` "should set htmlFor referencing the control automatically", "when nativeLabel={false}, clicking focuses the associated control" (L4 for the click-to-focus clause).
- **FLD-API-12** — MUST. The label's id MUST be the `id` prop when given on first render, otherwise a generated id; the resolved id MUST be rendered as the `id` attribute and published to the labelable scope (FFI-ARIA-08) so that non-native controls carry it in `aria-labelledby`. Once a label id is published in the scope, that id is used by this label for the rest of its life even if the `id` prop later changes (FLD-EDGE-05).
  Source: `field/label/FieldLabel.tsx:28,35,43-46`, `internals/labelable-provider/useLabel.ts:26`, `utils/useRegisteredLabelId.ts:10-17`. Test: [L2] `FieldRoot.test.tsx` "keeps `aria-labelledby` valid when toggling from Checkbox.Root to Select.Root after hydration" (label id non-empty and referenced); explicit id: none.
- **FLD-API-13** — MUST. `className`, `style` and `render` MUST behave per RND, receiving the state object below.
  Source: `field/label/FieldLabel.tsx:24-27,78-83`. Test: [L2] `FieldLabel.test.tsx` "Base UI component API".
- **FLD-API-14** — MUST. Every other prop MUST be forwarded to the label element, merged after the built-in attributes: a consumer `htmlFor` (or `id`) replaces the automatic one (RND-DOM-19), and consumer `onMouseDown`/`onClick`/`onPointerDown` run before the built-in handler and may suppress it with `preventBaseUIHandler()` (EVD-API-11).
  Source: `field/label/FieldLabel.tsx:30,81`. Test: [L2] `FieldLabel.test.tsx` "Base UI component API" (prop forwarding); handler chaining: none.
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): the consumer's `onmousedown`, `onclick` and `onpointerdown` on `FieldLabel` run first, inside the `form` module's dispatch, through the decision point `label` (fallback `run`), and `PreventPartHandler()` suppresses the module's handling. Blazor's own dispatch of the same event does not run them again. Tested by the L2 port title "runs the consumer's label pointer handler inside the decision, and Blazor's dispatch of the same event does not run it again" (FLD §16 Q12).

#### State object

The root state object (§3.1) with `disabled` replaced by `rootDisabled || itemDisabled`.

- **FLD-STA-02** — MUST. The label's state MUST equal the root state object except that `disabled` MUST be `true` when either the field or an enclosing `Field.Item` is disabled (FFI-STA-11).
  Source: `field/label/FieldLabel.tsx:37-40`. Test: [L2] `FieldLabel.test.tsx` "reflects the disabled state from Field.Item"; `FieldRoot.test.tsx` "should add data-disabled style hook to all components".

#### Data attributes

The same seven attributes as `Field.Root` (`data-disabled`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid`), derived from the label's state object. Req: FLD-DOM-04.

#### CSS variables

None.

### 3.3 `Field.Control`

**Default element:** `<input>`. **Accepts `render` element substitution:** yes (e.g. `<textarea />`; type-checked by `FieldControl.spec.tsx`). **Accepts `className`/`style` as function of state:** yes.

`Input` (`input/Input.tsx:13-18`) renders `Field.Control` with its props unchanged; [input](input.md) cites the requirements below rather than restating them.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `string \| number \| readonly string[]` (native `value` type) | `undefined` (component-owned) | The control's value when consumer-owned. | FLD-API-15 |
| `defaultValue` | native `defaultValue` type | `undefined` | Initial value when component-owned; later changes are forwarded to the element. | FLD-API-16 |
| `onValueChange` | `(value: string, eventDetails: Field.Control.ChangeEventDetails) => void` | — | Called on every native `change`/`input` of the element, before any field state is updated (§7). | FLD-API-17 |
| `name` | `string` | `undefined` | Form name; overridden by `Field.Root.name`, and used as the field's name fallback. Not in the API outline (native attribute). | FLD-API-18 |
| `disabled` | `boolean` | `false` | Combined (OR) with the field's disabled state. Not in the API outline. | FLD-API-19 |
| `id` | `string` | generated | The control's id; claims the labelable scope's control id so `Field.Label` points at it. Not in the API outline. | FLD-API-20 |
| `autoFocus` | `boolean` | `false` | Forwarded as the `autofocus` attribute; focus acquired before hydration is reclaimed (FLD-FOC-04). Not in the API outline. | FLD-API-21 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FLD-API-22 |
| *other `<input>` attributes and handlers* (`type`, `required`, `pattern`, `minLength`, `maxLength`, `min`, `max`, `step`, `readOnly`, `placeholder`, `aria-*`, `onChange`, `onFocus`, `onBlur`, `onKeyDown`, …) | — | — | Forwarded to the element; native constraint attributes drive validation (§8). | FLD-API-23 |

**Consumed (not forwarded as given):** `render`, `className`, `style`, `id`, `name`, `value`, `disabled`, `onValueChange`, `defaultValue`, `autoFocus` — of these, `id`, `name`, `disabled`, `value`/`defaultValue` and `autoFocus` are re-applied with their effective values.

- **FLD-API-15** — MUST. On any render where `value` is not `undefined` the control is consumer-owned: the element's `value` MUST be bound to the prop (so a change the consumer does not apply is not shown), and the field's dirty/filled/validation reactions MUST follow the prop (FLD-STA-12, FLD-EVT-06). The serialized form `String(value)` (`undefined` for `null`) is what the field compares and validates.
  Source: `field/control/FieldControl.tsx:40,74-76,122,130-132`. Test: [L2] `FieldControl.test.tsx` "validates once when a controlled value is changed by the user", "syncs state and validates when the controlled value changes programmatically", "clears dirty state when a numeric controlled value returns to its initial value"; `FieldRoot.test.tsx` "changes [data-filled] when the value is changed externally".
  Port note 2026-10-22 (ADR 08 Amendments 2026-10-22; ADR 04 and ADR 01 Amendments 2026-10-22, chosen by the user 2026-10-22): `Value` is `string?`, so the numeric clause of the serialization does not arise. Base UI's numeric title is translated over strings. From attach the `form` module shows `Value` and re-asserts it after an edit the consumer does not apply. On Server the revert lands a round trip later (ADR 01 relaxation row added 2026-10-22). Tested at L4 on both modes by the port title "reverts an edit the consumer does not apply".
- **FLD-API-16** — MUST. When `value` is `undefined` the control is component-owned: `defaultValue` MUST be applied as the element's default value and a later change of `defaultValue` MUST be forwarded to the element without a warning and MUST recompute `filled` from the element's current value (FFI-EDGE-06).
  Source: `field/control/FieldControl.tsx:43,89-94,122`. Test: [L2] `FieldControl.test.tsx` "sets filled state on mount when the control is prefilled", "forwards a changing defaultValue without warning", "keeps filled state from the edited value when defaultValue changes".
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): the `form` module sets the element's `defaultValue` from `DefaultValue` at attach and on every change, and the live value too while nothing has changed it since the module or the renderer last set it. A user's edit is kept (FFI-EDGE-06; L4 port title "keeps the user's text when the default value changes").
- **FLD-API-17** — MUST. `onValueChange` MUST receive the element's current string value as its first argument and a change-event-details object (§3.8) as its second. Firing rules are in §7.
  Source: `field/control/FieldControl.tsx:42,123-126,204-209`. Test: [L2] `FieldControl.test.tsx` "does not validate when the change is canceled", "does not clear errors or validate when change is prevented" (`handleValueChange` called once).
- **FLD-API-18** — MUST. The element's `name` attribute MUST be `fieldName ?? name` (FFI-STA-10); when the field has no `name`, the control's `name` becomes the field's effective name (used for `Form.errors`, `Form.Values` and `clearErrors`) and is forgotten when the control unregisters.
  Source: `field/control/FieldControl.tsx:39,63,86,118`. Test: [L2] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values", "updates the Field.Control name fallback when the name changes"; `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors".
- **FLD-API-19** — MUST. `disabled` defaults to `false`; the effective disabled state MUST be `fieldDisabled || disabled`, rendered as the native `disabled` attribute, reflected in the control's own `data-disabled`, and used to unregister the control from the field and form (FLD-FRM-11).
  Source: `field/control/FieldControl.tsx:41,62,65-68,85,117`. Test: [L2] `FieldRoot.test.tsx` "should add data-disabled style hook to all components"; `FieldControl.test.tsx` "is removed when the focused control becomes disabled".
- **FLD-API-20** — MUST. The rendered `id` MUST be the labelable scope's resolved control id (FFI-ALG-07): the explicit `id` prop once the control has registered it, otherwise the scope's generated id; removing a previously explicit `id` falls back to a generated id (not the scope default) with the label following in the same update (FFI-FRM-36).
  Source: `field/control/FieldControl.tsx:38,72,116`, `internals/labelable-provider/useLabelableId.ts:32-71,82`. Test: [L2] `FieldRoot.test.tsx` "updates label associations when the control id changes", "falls back to a generated id when the control id is removed", "drops a stale explicit id when an id-less control replaces the control that owned it"; `FieldControl.test.tsx` "updates the label association when the control is swapped".
- **FLD-API-21** — MUST. `autoFocus` defaults to `false` and MUST be forwarded as the element's `autofocus` attribute.
  Source: `field/control/FieldControl.tsx:44,121`. Test: [L2] `FieldControl.test.tsx` "should sync focused state when autoFocus is used with SSR" (attribute present).
  Port note 2026-10-22 (ADR 04 and ADR 01 Amendments 2026-10-22, settled by the orchestrator 2026-10-22, ADR 02 verification item 1 and ADR 15): `AutoFocus` renders `autofocus`. Because the interactive render replaces a prerendered element, the `form` module also focuses the control once, at attach, when nothing else has focus. This is React's `autoFocus` on mount, and it restores the focus the browser gave the prerendered element.
- **FLD-API-22** — MUST. `className`, `style` and `render` MUST behave per RND, receiving the state object below; a substituted non-`<input>` element still receives the id, name, disabled, value binding, handlers and validation attributes.
  Source: `field/control/FieldControl.tsx:36-37,45,111-114`. Test: [L2] `FieldControl.test.tsx` "Base UI component API", "sets filled state from a controlled value on a custom element".
- **FLD-API-23** — MUST. Every other prop MUST be forwarded to the element, merged after the built-in attributes (RND-DOM-19) and before the validation attributes (FLD-ARIA-02, FLD-ARIA-03); consumer `onChange`, `onFocus`, `onBlur` and `onKeyDown` run before the built-in handlers and may suppress them with `preventBaseUIHandler()` (EVD-API-11). `onChange` is accepted (it is not excluded from the prop type).
  Source: `field/control/FieldControl.tsx:46,193-194`. Test: [L2] `FieldControl.test.tsx` "Base UI component API"; `FieldRoot.test.tsx` "changes [data-filled] when the value is changed externally" (consumer `onChange`), "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty" (`type="email"` forwarded).

#### State object

The root state object with `disabled` replaced by the control's effective disabled state.

- **FLD-STA-03** — MUST. The control's state MUST equal the root state object except that `disabled` MUST be `fieldDisabled || disabledProp`. (`Field.Item`'s disabled flag is not consulted by `Field.Control`; only group members consult it.)
  Source: `field/control/FieldControl.tsx:62,65-68`. Test: [L2] `FieldRoot.test.tsx` "should add data-disabled style hook to all components".

#### Data attributes

The same seven attributes as `Field.Root`, derived from the control's state object. Req: FLD-DOM-04.

#### CSS variables

None.

### 3.4 `Field.Description`

**Default element:** `<p>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated | Rendered as the element's id and published as a description id. Not in the API outline. | FLD-API-24 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FLD-API-25 |
| *other `<p>` attributes and handlers* | — | — | Forwarded to the element. | FLD-API-26 |

**Consumed (not forwarded as given):** `render`, `className`, `style`, `id` (re-applied as the resolved id).

- **FLD-API-24** — MUST. The description's id MUST be the `id` prop when given, otherwise a generated id; it MUST be rendered as the `id` attribute and published to the enclosing labelable scope (the `Field.Item` scope when inside an item, else the field scope) for as long as the part is mounted (FFI-ARIA-02). An empty-string id MUST NOT be published.
  Source: `field/description/FieldDescription.tsx:23,25,36-46,51`. Test: [L2] `FieldDescription.test.tsx` "should set aria-describedby on the control automatically", "does not register an empty description id".
- **FLD-API-25** — MUST. `className`, `style` and `render` MUST behave per RND, receiving the state object below.
  Source: `field/description/FieldDescription.tsx:23,48-53`. Test: [L2] `FieldDescription.test.tsx` "Base UI component API".
- **FLD-API-26** — MUST. Every other prop (including `children`) MUST be forwarded to the element, merged after the built-in `id`.
  Source: `field/description/FieldDescription.tsx:23,51`. Test: [L2] `FieldDescription.test.tsx` "Base UI component API".

#### State object

The root state object with `disabled` replaced by `rootDisabled || itemDisabled`.

- **FLD-STA-04** — MUST. The description's state MUST equal the root state object except that `disabled` MUST be `true` when either the field or an enclosing `Field.Item` is disabled.
  Source: `field/description/FieldDescription.tsx:31-34`. Test: [L2] `FieldDescription.test.tsx` "reflects the disabled state from Field.Item"; `FieldRoot.test.tsx` "should add data-disabled style hook to all components".

#### Data attributes

The same seven attributes as `Field.Root`, derived from the description's state object. Req: FLD-DOM-04.

#### CSS variables

None.

### 3.5 `Field.Item`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables the control wrapped by this item; `Field.Root.disabled` takes precedence (OR). | FLD-API-27 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FLD-API-28 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the element. | FLD-API-29 |

**Consumed (not forwarded to the element):** `render`, `className`, `style`, `disabled`.

- **FLD-API-27** — MUST. `disabled` defaults to `false`. The item's effective disabled state MUST be `rootDisabled || disabled`; it MUST be published to the `Field.Label` and `Field.Description` inside the item (FLD-STA-02, FLD-STA-04) and to the group member control inside the item, which MUST ignore user interaction while the item is disabled (FFI-STA-11). It MUST NOT be rendered as an attribute of the item element.
  Source: `field/item/FieldItem.tsx:25,31,34,45`. Test: [L2] `FieldItem.test.tsx` "reflects disabled state on the item", "disables a wrapped checkbox", "disables a wrapped radio".
- **FLD-API-28** — MUST. `className`, `style` and `render` MUST behave per RND, receiving the state object below (the render function receives the item's state as its second argument).
  Source: `field/item/FieldItem.tsx:22-24,36-41`. Test: [L2] `FieldItem.test.tsx` "Base UI component API", "reflects disabled state on the item" (`state.disabled === true` in the render function).
- **FLD-API-29** — MUST. Every other prop MUST be forwarded to the element.
  Source: `field/item/FieldItem.tsx:26,39`. Test: [L2] `FieldItem.test.tsx` "Base UI component API".

#### State object

The root state object with `disabled` replaced by the item's effective disabled state.

- **FLD-STA-05** — MUST. The item's state MUST equal the root state object except that `disabled` MUST be `rootDisabled || disabledProp`.
  Source: `field/item/FieldItem.tsx:29-32`. Test: [L2] `FieldItem.test.tsx` "reflects disabled state on the item".

#### Data attributes

The same seven attributes as `Field.Root`, derived from the item's state object. Req: FLD-DOM-04.

#### CSS variables

None.

### 3.6 `Field.Error`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `match` | `boolean \| keyof ValidityState` | `undefined` | When the message renders: `true` always; a `ValidityState` key when that flag of the field's validity state is `true`; `false`/omitted when the field is invalid for any reason (the default slot, which also shows `Form.errors`). | FLD-API-30 |
| `id` | `string` | generated | Rendered as the element's id and published as a description id while the message is rendered. Not in the API outline. | FLD-API-31 |
| `children` | `ReactNode` | automatic message | Replaces the automatic message text. Not in the API outline. | FLD-API-32 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FLD-API-33 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the element. | FLD-API-34 |

**Consumed (not forwarded as given):** `render`, `className`, `style`, `id` (re-applied), `match`.

The accepted `keyof ValidityState` literals are exactly: `'badInput'`, `'customError'`, `'patternMismatch'`, `'rangeOverflow'`, `'rangeUnderflow'`, `'stepMismatch'`, `'tooLong'`, `'tooShort'`, `'typeMismatch'`, `'valid'`, `'valueMissing'`. There is no array form and no `forceShow` prop (`forceShow` was folded into `match={true}`; CHANGELOG v1.0.0-beta.0 "Consolidate `Field.Error` `forceShow` into `match` prop").

- **FLD-API-30** — MUST. Whether the error is *rendered* MUST follow FFI-ALG-15 exactly: `match === true` → rendered (even while the field is disabled or valid); else field disabled → not rendered; else `match` is a string → rendered iff `validityData.state[match]` is `true` (a form error or the `invalid` prop does not satisfy a string `match`); else (`false`/`undefined`) → rendered iff the field has a form error for its effective name or `validityData.state.valid === false`. The value shown is FLD-DOM-15.
  Source: `field/error/FieldError.tsx:33,42-55,146-154`. Test: [L2] `FieldError.test.tsx` "always renders the error message when `match` is true", "should only render when `match` matches constraint validation", "should show custom errors", "uses `match={false}` as the default slot for Form errors", "uses an omitted `match` as the default slot for Form errors", "uses `match={false}` as the default slot for client validation errors", "uses the client validation path for specific matches when Form errors are present", "ignores empty Form error arrays"; `FieldRoot.test.tsx` "should apply [data-field] style hooks to field components" (not rendered while valid).
- **FLD-API-31** — MUST. The error's id MUST be the `id` prop when given, otherwise a generated id; it MUST be rendered as the `id` attribute and published to the enclosing labelable scope only while the error is rendered (FFI-ARIA-02); an empty-string id MUST NOT be published.
  Source: `field/error/FieldError.tsx:33,35,59-69,123`. Test: [L2] `FieldError.test.tsx` "should set aria-describedby on the control automatically", "does not register an empty error id".
- **FLD-API-32** — MUST. When `children` is supplied it MUST replace the automatic message entirely (the automatic message and its list markup are not rendered); when omitted the element's content is the automatic message of FLD-DOM-15.
  Source: `field/error/FieldError.tsx:121-127` (consumer props merge after `children`). Test: [L2] `FieldError.test.tsx` "should show error messages by default" (`Message`), "should show custom errors"; automatic message: `FieldRoot.test.tsx` "should validate the field on submit" (`error` text from `validate`).
- **FLD-API-33** — MUST. `className`, `style` and `render` MUST behave per RND, receiving the state object below (which includes `transitionStatus`).
  Source: `field/error/FieldError.tsx:33,118-130`. Test: [L2] `FieldError.test.tsx` "Base UI component API".
- **FLD-API-34** — MUST. Every other prop MUST be forwarded to the element, merged after the built-in `id` and `children`.
  Source: `field/error/FieldError.tsx:33,126`. Test: [L2] `FieldError.test.tsx` "Base UI component API" (prop forwarding), "triggers enter animation via data-starting-style when mounting" (`onTransitionEnd` forwarded).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled`, `touched`, `dirty`, `valid`, `filled`, `focused` | as §3.1 | The root state object, unchanged (`disabled` is the *field's* disabled state; an enclosing `Field.Item`'s disabled flag is not reflected, FLD-EDGE-04). |
| `transitionStatus` | `'starting' \| 'ending' \| undefined` | Enter/exit phase of the message (§9). |

- **FLD-STA-06** — MUST. The error's state MUST be the root state object plus `transitionStatus` (TRN-API-07); `'idle'` is never produced because the idle state is not enabled. Server: relaxed, see §9.
  Source: `field/error/FieldError.tsx:57,113-116,139-144`, `internals/useTransitionStatus.ts:17-22`. Test: [L4] `FieldError.test.tsx` "applies data-ending-style before unmount".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid` | as `Field.Root`, from the error's state object | FLD-DOM-04 |
| `data-starting-style` | `""` while `transitionStatus === 'starting'` | FLD-DOM-18 |
| `data-ending-style` | `""` while `transitionStatus === 'ending'` | FLD-DOM-18 |

#### CSS variables

None.

### 3.7 `Field.Validity`

**Default element:** none (renders its function's result in a fragment). **Accepts `render` element substitution:** no. **Accepts `className`/`style`:** no.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `(state: Field.Validity.State) => ReactNode` | required | Render function receiving the field's validity state. | FLD-API-35 |

- **FLD-API-35** — MUST. `children` MUST be a function; it MUST be invoked on every render of the part (invocation count observed through bUnit render counts, L2) with the validity state object of FLD-STA-07, and its return value MUST be rendered in place. No other prop is accepted or forwarded.
  Source: `field/validity/FieldValidity.tsx:15-16,38,52-65`. Test: [L2] `FieldValidity.test.tsx` "should pass validity data" (both modes).

#### State object (render-function argument)

| Field | Type | Meaning |
|---|---|---|
| `validity` | `{ badInput, customError, patternMismatch, rangeOverflow, rangeUnderflow, stepMismatch, tooLong, tooShort, typeMismatch, valueMissing: boolean; valid: boolean \| null }` | The combined validity state: the field's computed `ValidityState` flags with `valid` folded with the `invalid` prop and form errors (`valid = !invalid && computedValid`). |
| `error` | `string` | First error message (`''` when none). |
| `errors` | `string[]` | All error messages (`[]` when none). |
| `value` | `unknown` | The value that was last committed to validation (`null` before any validation). |
| `initialValue` | `unknown` | The value captured at the first control registration (`null` before). |
| `transitionStatus` | `'starting' \| 'ending' \| undefined` | Enter/exit phase of the *invalid* state (FLD-ANI-06). |

- **FLD-STA-07** — MUST. The argument MUST contain exactly the six fields above: the field's validity data (FFI-STA-03, FFI-ALG-14) with its `state` field renamed `validity` and combined per FFI-ALG-14 (`valid = !invalid && state.valid`, where `invalid` is `invalidProp === true || hasFormError`), plus `transitionStatus`. There is no `state` field.
  Source: `field/validity/FieldValidity.tsx:18-36,41-50`, `field/utils/getCombinedFieldValidityData.ts:7-18`. Test: [L2] `FieldValidity.test.tsx` "should pass validity data" (`validity.valid`, `validity.valueMissing`, `value`, `transitionStatus`), "should correctly pass errors when validate function returns a string", "should correctly pass errors when validate function returns an array of strings"; `FieldRoot.test.tsx` "does not publish errors while `valueMissing` is suppressed".
- **FLD-STA-08** — MUST. The argument object's identity MUST be stable across renders of the part in which neither the field's validity data, nor the `invalid` flag, nor `transitionStatus` changed (changes of `focused`, `dirty`, `filled`, `touched`, `disabled` or of unrelated ancestors MUST NOT produce a new object); it MUST be a new object whenever validity data is published (FFI-ALG-14), even if the published values are equal to the previous ones.
  Source: `field/validity/FieldValidity.tsx:20-23,27-36`. Test: none (FFI-EDGE-12 records the same obligation).

#### Data attributes

None (no element).

#### CSS variables

None.

### 3.8 Shared types

- `Field.ValidityData` (`FieldValidityData`) — `{ state, error, errors, value, initialValue }`, see FFI §3.2. Source: `field/root/FieldRoot.tsx:215-233`, exported `field/index.parts.ts:9`.
- `Field.Root.State` — §3.1 state object (`FieldRootState`). `Field.Root.Props`, `Field.Root.Actions` (`{ validate(): void }`). Source: `field/root/FieldRoot.tsx:235-237,239-264,330-334`.
- `Field.Control.ChangeEventReason` = `'none'` — the only literal. Source: `field/control/FieldControl.tsx:213`, `internals/reason-parts.ts:1`.
- `Field.Control.ChangeEventDetails` — the common change-event-details shape from [EVD](../architecture/event-details-and-reasons.md) (EVD-API-01) specialised to reason `'none'`: `{ reason: 'none'; event: Event; trigger: Element | undefined; cancel(); allowPropagation(); isCanceled; isPropagationAllowed }`. Source: `field/control/FieldControl.tsx:215-216`, `internals/createBaseUIEventDetails.ts`.
- `Field.Control.State`, `Field.Label.State`, `Field.Description.State`, `Field.Item.State` — each extends `Field.Root.State` with no additional fields (their `disabled` value is computed differently, §3.2–3.5). `Field.Error.State` adds `transitionStatus`. `Field.Validity.State` is the render-function argument of §3.7. Source: `field/control/FieldControl.tsx:202`, `field/label/FieldLabel.tsx:88`, `field/description/FieldDescription.tsx:58`, `field/item/FieldItem.tsx:50`, `field/error/FieldError.tsx:139-144`, `field/validity/FieldValidity.tsx:41-50`.
- `Field.Control.Props`, `Field.Label.Props`, `Field.Description.Props`, `Field.Item.Props`, `Field.Error.Props`, `Field.Validity.Props` — the prop tables above.
- `Form.ValidationMode`, `Form.Values` — defined by [form](form.md) / FFI §3.2.
- Exported constant namespaces `FieldRootDataAttributes`, `FieldLabelDataAttributes`, `FieldControlDataAttributes`, `FieldDescriptionDataAttributes`, `FieldItemDataAttributes` each list the seven field attributes; `FieldErrorDataAttributes` additionally lists `startingStyle`/`endingStyle`. There is no `FieldValidityDataAttributes`. Source: `field/index.ts:11-16`, `field/*/*DataAttributes.ts`.

## 4. State model

`Field.Root` owns four component-owned boolean flags (`touched`, `dirty`, `filled`, `focused`), the validity data (`{ state, error, errors, value, initialValue }`), the name registered by the active control, the dirty latch and the focus owner. Three of these can be overridden one-way by props: `dirty`, `touched` (the flag *is* the prop while the prop is defined) and `invalid` (which forces `valid` to `false`). There is no `default*` pair for any of them. The field never changes a value itself: every flag is written by the active control at the moments defined in FFI §6 table B (for `Field.Control`: FLD-STA-13, FLD-STA-14, FLD-FOC-01..04), and validity data is written only by the validation pipeline (FFI-ALG-14).

`Field.Control` owns nothing beyond the native element's value. Its value may be consumer-owned (`value`) or component-owned (`defaultValue`); unlike the other controls in the library the mode is not latched at first render but evaluated on every render (FLD-STA-12).

**Derived values**

- field `disabled` = `fieldsetDisabled || disabledProp` (FLD-API-02)
- effective `name` = `nameProp ?? registeredControlName` (FLD-API-01)
- `invalid` = `invalidProp === true || hasFormError` where `hasFormError` = `Form.errors` has an own property for the effective name whose value is a non-empty string or non-empty array (FFI-API-04)
- `valid` = `!invalid && (disabled ? null : validityData.state.valid)` (FLD-STA-09)
- `dirty` = `dirtyProp ?? dirtyState`; `touched` = `touchedProp ?? touchedState` (FLD-STA-10)
- `Field.Control` `filled` = `(String(value) ?? element.value) !== ''` (FLD-STA-13); `dirty` = `String(value) !== (initialValue ?? '')` (FLD-STA-14)

**Invariants**

- `data-valid` and `data-invalid` are never both present; neither is present while `valid === null`.
- `focused` is never `true` while the owning control is disabled (FFI-ALG-13).
- `initialValue` is captured once per `Field.Root` lifetime (FFI-STA-04); `dirty` therefore returns to `false` whenever the value returns to that baseline, including after a control remount or swap.
- `touched` is never reset by the field (FFI-STA-07).

No value normalization applies to `Field.Root`. For `Field.Control` the only normalization is string serialization of a consumer-owned `value` (`String(value)`, `null` → treated as no value).

`Field.Error` has a mount/unmount lifecycle (§9); every other part is always mounted.

- **FLD-STA-09** — MUST. `valid` MUST be computed as `!invalid && (disabled ? null : validityData.state.valid)`: an externally invalid field (prop or form error) is `false` even while disabled; a disabled field without external invalidity is `null` (neutral) regardless of computed validity; otherwise it is the published computed validity (FFI-STA-02).
  Source: `field/root/FieldRoot.tsx:94-97,107-110`. Test: [L2] `FieldRoot.test.tsx` "keeps an explicitly invalid field marked invalid while disabled", "keeps a disabled field with form errors marked invalid"; `FieldError.test.tsx` "ignores inherited Form error properties".
- **FLD-STA-10** — MUST. The dirty and touched flags MUST be `dirtyProp ?? dirtyState` and `touchedProp ?? touchedState`, where the internal states start `false` and are written only through the field's setters, which are no-ops while the corresponding prop is defined (FFI-API-05). The dirty latch MUST start as the initial `dirty` value and MUST be synchronized to `dirtyProp` post-commit, pre-paint whenever that prop is defined (FFI-STA-06).
  Source: `field/root/FieldRoot.tsx:50-51,56-59,64-86`. Test: [L2] `FieldRoot.test.tsx` "prop: dirty" (3 tests), "prop: touched" (2 tests).
- **FLD-STA-11** — MUST. `filled` and `focused` MUST start `false` and MUST change only through the active control's writes (FFI-STA-08, FFI-STA-09); `Field.Root` itself MUST NOT infer them from the DOM.
  Source: `field/root/FieldRoot.tsx:52-54,162-164`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-filled] style hook to all components when filled", "should apply [data-focused] style hook to all components when focused".
- **FLD-STA-12** — MUST. `Field.Control` MUST decide ownership per render: when `value !== undefined` the element is bound to `value` (the element's displayed value follows the prop, and a user edit the consumer does not apply is reverted by the binding); when `value === undefined` the element is bound to `defaultValue` and keeps the user's edits. Switching between the two between renders is permitted and simply changes the binding. Port: relaxed (ADR 05 Amendments item 1).
  Source: `field/control/FieldControl.tsx:74,122`. Test: [L2] `FieldControl.test.tsx` "renders once per keystroke for controlled input changes", "avoids rerendering for uncontrolled input changes".
- **FLD-STA-13** — MUST. `Field.Control` MUST write the field's `filled` flag as `currentValue !== ''`, where `currentValue` is `String(value)` when consumer-owned and otherwise the element's live value, post-commit, pre-paint after mount and after every change of `value` or `defaultValue`; when neither a consumer value nor an element value exists (a substituted element without a `value` property and no `value` prop) the flag MUST be left unchanged. In the component-owned mode the flag is additionally written synchronously inside the change handler.
  Source: `field/control/FieldControl.tsx:89-94,136`. Test: [L2] `FieldControl.test.tsx` "sets filled state on mount when the control is prefilled", "does not set filled state on mount for an empty controlled value", "clears filled state when a controlled control remounts empty", "clears filled state when an uncontrolled control remounts empty", "keeps filled state from the edited value when defaultValue changes", "sets filled state from a controlled value on a custom element".
- **FLD-STA-14** — MUST. `Field.Control` MUST write the field's `dirty` flag as `currentValue !== (initialValue ?? '')` (FFI-FRM-33): synchronously inside the change handler when component-owned, and post-commit, pre-paint whenever the serialized consumer value changes when consumer-owned (FFI-EVT-02). The write happens before validation so the dirty latch is observed by the same commit.
  Source: `field/control/FieldControl.tsx:96-105,134-136`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-dirty] style hook to all components when dirty", "keeps the original baseline when a controlled control remounts", "keeps the field baseline when the control is swapped", "clears dirty after an empty text control returns to empty following a null-valued control"; `FieldControl.test.tsx` "clears dirty state when a numeric controlled value returns to its initial value".

## 5. Rendered DOM and ARIA

### 5.1 `Field.Root` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (element) | `<div>`; no `id`, `role` or `aria-*` of its own | FLD-DOM-01 | C# |
| `data-disabled`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | `""` while the flag is `true` | FLD-DOM-02 | C# |
| `data-valid` / `data-invalid` | `""` while `valid === true` / `valid === false`; neither while `null` | FLD-DOM-03 | C# |

- **FLD-DOM-01** — MUST. The root element MUST be a `<div>` (or the substituted element) carrying only the forwarded consumer attributes and the state data attributes; the part adds no `id`, `role`, `aria-*` or `name`.
  Source: `field/root/FieldRoot.tsx:188-193`. Test: [L2] `FieldRoot.test.tsx` "Base UI component API".
- **FLD-DOM-02** — MUST. The root MUST carry `data-disabled`, `data-touched`, `data-dirty`, `data-filled` and `data-focused`, each with the empty-string value, exactly while the corresponding state field is `true` (FFI-DOM-01, RND-DOM-01).
  Source: `field/root/FieldRoot.tsx:190-192`, `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `FieldRoot.test.tsx` "should add data-disabled style hook to all components", "should apply [data-touched] style hook to all components when touched", "should apply [data-dirty] style hook to all components when dirty", "should apply [data-filled] style hook to all components when filled", "should apply [data-focused] style hook to all components when focused".
- **FLD-DOM-03** — MUST. The root MUST carry `data-valid=""` while `valid === true`, `data-invalid=""` while `valid === false`, and neither while `valid === null`; the generic projection (which would emit `data-valid="false"`) MUST NOT apply (FFI-DOM-01).
  Source: `field/root/FieldRoot.tsx:192`, `internals/field-constants/constants.ts:34-44`. Test: [L2] `FieldRoot.test.tsx` "publishes neutral validity while a validator is in flight in %s mode" (neither), "should apply [data-field] style hooks to field components" (both).
- **FLD-DOM-04** — MUST. `Field.Label`, `Field.Control`, `Field.Description`, `Field.Item` and `Field.Error` MUST each carry the same seven attributes, derived by the same rules from their own state object (§3.2–3.6; only `disabled` can differ from the root's), and MUST update them in the same commit as the root.
  Source: `field/label/FieldLabel.tsx:82`, `field/control/FieldControl.tsx:196`, `field/description/FieldDescription.tsx:52`, `field/item/FieldItem.tsx:40`, `field/error/FieldError.tsx:18-21,128`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-field] style hooks to field components", "should apply [data-touched] style hook to all components when touched", "should apply [data-dirty] style hook to all components when dirty", "should apply [data-filled] style hook to all components when filled", "should apply [data-focused] style hook to all components when focused", "controls the dirty state", "controls the touched state"; `FieldLabel.test.tsx` / `FieldDescription.test.tsx` / `FieldItem.test.tsx` disabled tests.

### 5.2 `Field.Label` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (element) | `<label>` by default | FLD-API-11 | C# |
| `id` | the resolved label id | FLD-DOM-05 | C# |
| `for` | the scope's resolved control id, native labels only; absent when the control id is `null` | FLD-DOM-06 | C# |
| `for` (non-native) | never rendered | FLD-DOM-07 | C# |
| state data attributes | per FLD-DOM-04 | FLD-DOM-04 | C# |

- **FLD-DOM-05** — MUST. The label element MUST carry `id` equal to the id resolved per FLD-API-12 (never empty).
  Source: `field/label/FieldLabel.tsx:43-46,81`, `internals/labelable-provider/useLabel.ts:26,66,71`. Test: [L2] `FieldError.test.tsx`/`FieldRoot.test.tsx` (label ids referenced through `aria-labelledby`), e.g. "keeps `aria-labelledby` valid when toggling from Checkbox.Root to Select.Root after hydration".
  Port note 2026-10-22 (ADR 13 Amendments 2026-10-22, re-cited): the cited title toggles Checkbox.Root and Select.Root, which arrive later. The L2 port title "references the mounted label from the control, and removes the reference when the label is removed" replaces it.
- **FLD-DOM-06** — MUST. A native label (`nativeLabel` `true`) MUST carry `for` equal to the labelable scope's resolved control id (FFI-ARIA-04, FFI-ALG-06): the registered control's id (explicit or generated), the scope's default id before any control registers, and no `for` at all when the registered control claimed `null` (e.g. `CheckboxGroup`). The attribute MUST follow control replacement, id changes and id removal in the same update as the control's own `id` (FFI-FRM-36).
  Source: `internals/labelable-provider/useLabel.ts:28,64-69`, `internals/labelable-provider/LabelableProvider.tsx:12-20`. Test: [L2] `FieldLabel.test.tsx` "should set htmlFor referencing the control automatically", "keeps the selected control id when another control unmounts", "falls over to the remaining control when the selected one unmounts"; `FieldRoot.test.tsx` "updates label association when replacing one control with another", "re-associates the label when a CheckboxGroup is replaced by another control", "updates label associations when the control id changes", "falls back to a generated id when the control id is removed", "keeps an explicit control id while the subtree is hidden", "keeps the group label suppressed while its subtree is hidden"; `FieldRoot.react17.test.tsx` "falls back to a generated id when an explicit control id is removed"; `FieldItem.test.tsx` "associates a Field.Item label with a parent checkbox" (`label.control` is the hidden checkbox input).
- **FLD-DOM-07** — MUST. A non-native label (`nativeLabel` `false`) MUST NOT carry `for`; its association is through the control's `aria-labelledby` (FLD-ARIA-01) and the click-to-focus behavior (FLD-PTR-03).
  Source: `internals/labelable-provider/useLabel.ts:70-76`. Test: [L2] `FieldLabel.test.tsx` "when nativeLabel={false}, clicking focuses the associated control" (`not.toHaveAttribute('for')`); `FieldRoot.test.tsx` "preserves a non-native label association when a control is unmounted and remounted".

### 5.3 `Field.Control` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (element) | `<input>` by default; `type` and all native constraint attributes come from the consumer | FLD-API-23 | C# |
| `id` | the resolved control id | FLD-DOM-08 | C# |
| `name` | effective name; absent when neither the field nor the control has one | FLD-DOM-09 | C# |
| `disabled` | present while effectively disabled | FLD-DOM-10 | C# |
| `value` / `defaultValue` | consumer-owned binding / component-owned initial value | FLD-DOM-11 | C# → JS |
| `autofocus` | present while `autoFocus` | FLD-DOM-12 | C# |
| `aria-labelledby` | the scope's label id when a `Field.Label` is mounted | FLD-ARIA-01 | C# |
| `aria-describedby` | composed description ids | FLD-ARIA-02 | C# |
| `aria-invalid` | `"true"` while invalid and enabled | FLD-ARIA-03 | C# |
| state data attributes | per FLD-DOM-04 | FLD-DOM-04 | C# |

- **FLD-DOM-08** — MUST. The element MUST carry `id` equal to the resolved control id (FLD-API-20); it is never empty. Before the control's registration has run (first render, server markup) the id is the scope's generated default id even when an explicit `id` prop is given; after registration it is the explicit id (FFI-FRM-34).
  Source: `field/control/FieldControl.tsx:72,116`, `internals/labelable-provider/useLabelableId.ts:79-82`. Test: [L2] `FieldRoot.test.tsx` "falls back to a generated id when the control id is removed" (`updatedId` non-empty and not the explicit id); `FieldControl.test.tsx` "updates the label association when the control is swapped" (`id` equals the explicit prop after mount).
- **FLD-DOM-09** — MUST. The element MUST carry `name` equal to the effective name (FLD-API-18) and no `name` when that is `undefined`.
  Source: `field/control/FieldControl.tsx:63,118`. Test: [L2] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values" (native `FormData` and `Form.Values` keyed by it). (L4 for the native `FormData` clause, re-tagged 2026-10-22 per the M2 plan; the title renders `Form` and lands with PR 1c.)
- **FLD-DOM-10** — MUST. The element MUST carry the native `disabled` attribute exactly while `fieldDisabled || disabledProp`.
  Source: `field/control/FieldControl.tsx:62,117`. Test: [L2] `FieldControl.test.tsx` "is removed when the focused control becomes disabled" (control disabled through the prop); `FieldRoot.test.tsx` "clears its own message on an input that became disabled".
- **FLD-DOM-11** — MUST. While consumer-owned the element's `value` property MUST equal `value` on every render; while component-owned the element's default value MUST equal `defaultValue` (re-applied when the prop changes) and the live value MUST NOT be rewritten by the part (a programmatic assignment to the element's `value` is kept, FFI-EDGE-07).
  Source: `field/control/FieldControl.tsx:122`. Test: [L2] `FieldControl.test.tsx` "forwards a changing defaultValue without warning" (`toHaveValue('value')`); `FieldRoot.test.tsx` "should not reset to defaultValue when input value is programmatically changed and then focused", "should not reset to defaultValue when input value is programmatically changed to non-empty value and then focused".
  Port note 2026-10-22 (ADR 13 §3 `Writer` correction; ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): Blazor writes `value` only as a property and only when the rendered frame changes, so C# alone could not meet this row. C# renders the value once, and the `form` module owns `value` and `defaultValue` from attach. The `Writer` cell is `C# → JS`. The platform facts are confirmed at L4 on both modes (`field.spec.ts`, "platform: the value Blazor renders"). The two programmatic-change titles are tested at L4 on both modes (the row's `Test:` line names them under [L2]; their FFI rows tag them L4).
- **FLD-DOM-12** — MUST. The element MUST carry `autofocus` exactly while `autoFocus` is `true`.
  Source: `field/control/FieldControl.tsx:44,121`. Test: [L2] `FieldControl.test.tsx` "should sync focused state when autoFocus is used with SSR".

### 5.4 `Field.Description` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (element) | `<p>` by default | FLD-API-25 | C# |
| `id` | the resolved description id | FLD-DOM-13 | C# |
| state data attributes | per FLD-DOM-04 | FLD-DOM-04 | C# |

- **FLD-DOM-13** — MUST. The element MUST carry `id` equal to the id resolved per FLD-API-24 (the `id` prop verbatim, including an empty string, else a generated id).
  Source: `field/description/FieldDescription.tsx:25,51`. Test: [L2] `FieldDescription.test.tsx` "should set aria-describedby on the control automatically" (`screen.getByText('Message').id`).

### 5.5 `Field.Item` element

A `<div>` (by default) with the forwarded consumer attributes and the state data attributes (FLD-DOM-04); no id, role or ARIA attribute (FLD-STR-06).

### 5.6 `Field.Error` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| (element) | `<div>` by default; exists only while the message is mounted (§9) | FLD-DOM-14 | C# |
| `id` | the resolved error id | FLD-DOM-14 | C# |
| (content) | the automatic message (text or `<ul><li>…` list) unless `children` is given | FLD-DOM-15, FLD-DOM-16, FLD-API-32 | C# |
| (content while exiting) | the last message rendered while visible | FLD-DOM-17 | C# |
| `data-starting-style` / `data-ending-style` | enter / exit phase | FLD-DOM-18 | JS |
| state data attributes | per FLD-DOM-04 | FLD-DOM-04 | C# |

- **FLD-DOM-14** — MUST. The error element MUST exist in the DOM exactly while the part is *mounted* (FLD-ANI-01: from the commit in which it becomes rendered until its exit transition completes) and MUST carry `id` equal to the id resolved per FLD-API-31.
  Source: `field/error/FieldError.tsx:57,123,129-136`. Test: [L2] `FieldError.test.tsx` "should show error messages by default" (absent, then present), "applies data-ending-style before unmount" (removed after exit); `FieldRoot.test.tsx` "should apply [data-field] style hooks to field components" (`queryByTestId('error')` is `null` while valid) (L4 for the exit-transition clause).
- **FLD-DOM-15** — MUST. The automatic message MUST be selected as: if `match` is not a string and the field has a form error for its effective name → that form error (string or array); else if the field's `errors` list has more than one entry → that list; else the field's `error` string (`''` when none). Whether the element renders at all is FLD-API-30; the message is not re-evaluated by the match key, so a specific `match` always shows the client-side message even when a form error exists.
  Source: `field/error/FieldError.tsx:42-44,75-80`. Test: [L2] `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors", "uses the client validation path for specific matches when Form errors are present", "should show custom errors"; `FieldRoot.test.tsx` "custom validity ownership" tests (`toHaveTextContent('external error')`).
- **FLD-DOM-16** — MUST. When the selected message is an array with more than one entry it MUST be rendered as a `<ul>` with one `<li>` per entry, in order (entries keyed by their text); a one-entry array MUST be rendered as plain text equal to that entry.
  Source: `field/error/FieldError.tsx:82-94`. Test: [L2] `FieldError.test.tsx` "renders Form error arrays as a list", "renders single-item Form error arrays as text", "renders client validation error arrays as a list".
- **FLD-DOM-17** — MUST. While the part is mounted but no longer rendered (exit phase), the element's automatic content MUST be the last message that was shown while rendered (the field's current `error`/`errors`, which may already be empty, is not shown); the remembered message is updated whenever the rendered message's key (the string, or the JSON of the array) changes while rendered.
  Source: `field/error/FieldError.tsx:71-73,96-101,124`. Test: none (exit content not asserted; `FieldError.test.tsx` "applies data-ending-style before unmount" uses consumer `children`).
- **FLD-DOM-18** — MUST. The error element MUST carry `data-starting-style=""` while `transitionStatus === 'starting'` and `data-ending-style=""` while `'ending'`, and neither otherwise (TRN-DOM-01..03, RND-DOM-11).
  Source: `field/error/FieldError.tsx:18-21,113-116,128`, `internals/stateAttributesMapping.ts:10-20`. Test: [L4] `FieldError.test.tsx` "triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount".

### 5.7 `Field.Validity`

Renders no element (FLD-STR-05). The render function's output is inserted where the part sits, with no wrapper.

### 5.8 ARIA

No Field part sets a `role`, `aria-live`, `aria-atomic` or `aria-hidden`; `Field.Error` is not a live region (consumers add `role="alert"` themselves if desired). The ARIA output is confined to the control: `aria-labelledby`, `aria-describedby`, `aria-invalid`. The same three are applied by every other Base UI control under the field (FFI §6 table C) and, for `Field.Control`, are specified here.

- **FLD-ARIA-01** — MUST. `Field.Control` MUST carry `aria-labelledby` equal to the labelable scope's current label id (the id published by a mounted `Field.Label` in the same scope, FFI-ARIA-08) and no `aria-labelledby` when no label is published; a consumer `aria-labelledby` replaces it. The attribute MUST appear in the same update in which the label publishes its id and MUST be removed when the label unmounts.
  Source: `field/control/FieldControl.tsx:70,120,193`, `utils/useRegisteredLabelId.ts:12-17`. Test: none for `Field.Control` directly (`FieldRoot.test.tsx` "removes `aria-labelledby` when Field.Label is removed after hydration" and "preserves a non-native label association when a control is unmounted and remounted" assert the same scope mechanism on a Select trigger).
- **FLD-ARIA-02** — MUST. `Field.Control` MUST carry `aria-describedby` composed per FFI-ARIA-01/FFI-ALG-09: the consumer's `aria-describedby` ids (space-split, in order), then the parent scope's message ids, then this scope's message ids, de-duplicated and space-joined; absent when the list is empty. Message ids are those of every mounted `Field.Description` (FLD-API-24) and every currently rendered `Field.Error` (FLD-API-31), in the order they were published.
  Source: `field/control/FieldControl.tsx:194`, `field/root/useFieldValidation.ts:367-376`, `internals/labelable-provider/LabelableProvider.tsx:68-81`. Test: [L2] `FieldDescription.test.tsx` "should set aria-describedby on the control automatically", "should preserve user aria-describedby values on the control", "does not register an empty description id"; `FieldError.test.tsx` "should set aria-describedby on the control automatically", "does not register an empty error id".
- **FLD-ARIA-03** — MUST. `Field.Control` MUST carry `aria-invalid="true"` exactly while the field's `valid` is `false` and neither the field nor the control is disabled (FFI-ARIA-03); otherwise the attribute is whatever the consumer passed (normally absent). The built-in `true` takes precedence over a consumer value while invalid. The attribute MUST update in the same commit as `data-invalid` (synchronously for synchronous validators; after settlement for asynchronous ones).
  Source: `field/control/FieldControl.tsx:194`, `field/root/useFieldValidation.ts:367-376`. Test: [L2] `FieldRoot.test.tsx` "should apply aria-invalid prop to control once validation finishes", "validates the field on change", "keeps an explicitly invalid field marked invalid while disabled" (no `aria-invalid` while disabled), "publishes neutral validity while a validator is in flight in %s mode"; `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors" (`aria-invalid` from a form error, removed on change).
- **FLD-ARIA-04** — MUST. `Field.Description` MUST publish its id to its scope post-commit, pre-paint when it mounts (or its id changes) and withdraw it when it unmounts, so the control's `aria-describedby` reflects mounted descriptions only (FFI-ARIA-02). Server: relaxed, see §9.
  Source: `field/description/FieldDescription.tsx:36-46`. Test: [L4] `FieldDescription.test.tsx` "should set aria-describedby on the control automatically".
  Port note 2026-10-22 (ADR 01 Amendments 2026-10-22): the description publishes its id in `OnParametersSet` and withdraws it when disposed, inside the batch that creates or removes it. The row is met on both modes, as §9 notes; tested at L4 on both.
- **FLD-ARIA-05** — MUST. `Field.Error` MUST publish its id only while it is *rendered* (FLD-API-30), post-commit, pre-paint, and MUST withdraw it in the commit in which it stops being rendered — before its exit transition finishes and the element leaves the DOM (FFI-ARIA-02). Server: relaxed, see §9.
  Source: `field/error/FieldError.tsx:59-69`. Test: [L4] `FieldError.test.tsx` "should set aria-describedby on the control automatically" (`match` true); withdrawal timing: none.
- **FLD-ARIA-06** — MUST. `Field.Label` MUST publish its id to its scope post-commit, pre-paint when it mounts and withdraw it on unmount only if the scope still holds that id (FFI-ARIA-08), so that non-native controls (and `Field.Control`'s `aria-labelledby`) reference a mounted label only. Server: relaxed, see §9.
  Source: `internals/labelable-provider/useLabel.ts:21-26`, `utils/useRegisteredLabelId.ts:12-17`. Test: [L4] `FieldRoot.test.tsx` "removes `aria-labelledby` when Field.Label is removed after hydration", "keeps `aria-labelledby` valid when toggling from Checkbox.Root to Select.Root after hydration".
  Port note 2026-10-22 (ADR 01 Amendments 2026-10-22; ADR 13 Amendments 2026-10-22, re-cited): the label publishes its id in `OnParametersSet` and withdraws it only while it is current (`LabelIdSlot`), inside the batch that creates or removes it. The row is met on both modes. Both cited titles render Select.Root and are open to M5. The L2 port title "references the mounted label from the control, and removes the reference when the label is removed" covers the row meanwhile.
- **FLD-ARIA-07** — MUST. No Field part MUST set a `role` or any `aria-*` attribute other than the three above on `Field.Control`; in particular `Field.Error` MUST NOT be given `role="alert"` or `aria-live` by the component.
  Source: `field/error/FieldError.tsx:118-130`, `field/root/FieldRoot.tsx:188-193`, `field/description/FieldDescription.tsx:48-53`, `field/item/FieldItem.tsx:36-41`. Test: none.

## 6. Interactions

### 6.1 Keyboard

Only `Field.Control` handles a key. `Field.Label`, `Field.Description`, `Field.Item`, `Field.Error` and `Field.Root` attach no key handlers.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | `Field.Control` whose rendered element is an `<input>`; any modifiers | Marks the field touched; then commits the input's value for validation — deferred by a 0 ms macrotask and skipped if the form submitted in the meantime, when the input belongs to the surrounding `Form`'s element and the keydown is not default-prevented; immediately otherwise. Default not prevented. | FLD-KEY-01 |
| <kbd>Enter</kbd> | `Field.Control` rendered as a non-`<input>` element (`<textarea>`, `<div>`) | Not intercepted. | FLD-KEY-02 |
| <kbd>Tab</kbd>, <kbd>Shift</kbd>+<kbd>Tab</kbd>, every other key | any part | Not intercepted; native behavior. Tab moves focus and the resulting blur runs FLD-FOC-02. | FLD-KEY-03 |

- **FLD-KEY-01** — MUST. On <kbd>Enter</kbd> keydown on a `Field.Control` whose element is an `<input>`, the control MUST set `touched` and then run the Enter commit of FFI-KEY-01/FFI-ALG-16: when the input's owning form is the enclosing `Form`'s element and the keydown has not been default-prevented, it MUST start a 0 ms timer and, when it fires, commit the input's *current* value only if the form's submission counter is unchanged (so an implicit submission, which validates every field itself, never causes a second validation); otherwise (no form, a form that is not the enclosing `Form`, or a default-prevented keydown) it MUST commit the value immediately. A pending timer MUST be cancelled when the control unmounts. The keydown's default MUST NOT be prevented by the part.
  Source: `field/control/FieldControl.tsx:109,172-191`. Test: [L4] `FieldControl.test.tsx` "validates once when Enter implicitly submits a form", "validates when Enter does not implicitly submit the form", "validates when a disabled submit button blocks implicit submission", "validates the latest value when Enter does not submit the form", "validates when Enter is pressed outside a form".
  Port note 2026-10-22 (ADR 04 and ADR 01 Amendments 2026-10-22, settled by the orchestrator 2026-10-22): the 0 ms task is the `form` module's. After an Enter keydown on an `<input>` whose form owner is the enclosing form's element, and that is not default-prevented, the module schedules the task. When it runs, the module reports the current value, the validity and the owner comparison, and C# commits unless the form's submission counter moved since the keydown report. Outside a form the commit runs at the keydown report; tested at L4 on both modes ("validates when Enter is pressed outside a form"). The four titles with `Form` land with PR 1c.
- **FLD-KEY-02** — MUST. When the rendered element's tag is not `INPUT`, <kbd>Enter</kbd> MUST NOT be intercepted (no touched write, no commit).
  Source: `field/control/FieldControl.tsx:173`. Test: none.
- **FLD-KEY-03** — MUST. No other key MUST be intercepted by any Field part; <kbd>Tab</kbd> moves focus natively.
  Source: `field/control/FieldControl.tsx:172-191` (only `Enter` is tested). Test: none.
- **FLD-KEY-04** — MUST. A consumer `onKeyDown` on `Field.Control` MUST run before the built-in handler; calling `preventBaseUIHandler()` on the event MUST suppress FLD-KEY-01 entirely, while calling `preventDefault()` MUST switch FLD-KEY-01 to the immediate-commit branch.
  Source: `field/control/FieldControl.tsx:177,193`, `merge-props/mergeProps.ts` (handler chaining, RND). Test: [L2] port title "runs the consumer's input, blur and keydown handlers before the control's own, which a veto skips" (added 2026-10-22); none (`FieldControl.test.tsx` "validates the latest value when Enter does not submit the form" uses a form-level `onKeyDown` that runs *after* the part's handler and only evidences FLD-KEY-01's read of the current value).
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, settled by the orchestrator 2026-10-22): `FieldControl` and `Input` decline the consumer's `onkeydown` and run it from the module's keydown report before their own logic; `PreventPartHandler()` suppresses FLD-KEY-01 on both modes. The module reports every keydown only while the consumer has an `onkeydown`. The `preventDefault()` clause is reachable only from a consumer JavaScript listener that runs before the module, since a C# handler cannot call it.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `mousedown`, any button, `event.detail > 1` (double/triple click) | native `Field.Label` | `preventDefault()` (no text selection); activation stays native | FLD-PTR-01 |
| `mousedown` / `click` whose target is inside a `button`, `input`, `select` or `textarea` descendant of the label | `Field.Label` (either mode) | Ignored by the part | FLD-PTR-02 |
| `pointerdown` (any pointer type) | non-native `Field.Label` | `preventDefault()` (no focus theft, no selection) | FLD-PTR-03 |
| `click` | non-native `Field.Label` | Focuses the resolved control with `focus({ focusVisible: true })` | FLD-PTR-03 |
| click on a native `Field.Label` | native label with `for` | Browser focuses/activates the control natively (not implemented by the part) | — |
| any pointer event | `Field.Control`, `Field.Root`, `Field.Description`, `Field.Item`, `Field.Error` | Not intercepted | — |

- **FLD-PTR-01** — MUST. A native `Field.Label` MUST handle `mousedown`: when `event.detail > 1` and the event is not already default-prevented it MUST call `preventDefault()`; it MUST NOT otherwise interfere with native label activation (FFI-PTR-02).
  Source: `internals/labelable-provider/useLabel.ts:46-58,64-69`. Test: none in `field/` (`useLabel.test.tsx` "preserves native label activation inside a shadow root").
- **FLD-PTR-02** — MUST. Label interaction (both modes) MUST be ignored when the event target, resolved through shadow boundaries, is inside a `button`, `input`, `select` or `textarea` descendant of the label (FFI-PTR-03) — so a Checkbox/Switch placed inside the label is not double-activated.
  Source: `internals/labelable-provider/useLabel.ts:46-50`. Test: none in `field/` (`useLabel.test.tsx` "does not focus the control when a composed click originates inside a nested button"); `FieldItem.test.tsx` "associates a Field.Item label with a parent checkbox" (clicking the label text, not the control, toggles once).
- **FLD-PTR-03** — MUST. A non-native `Field.Label` MUST call `preventDefault()` on `pointerdown` and, on `click` (after the FLD-PTR-02 guard and the double-click guard), MUST focus the element whose id is the scope's resolved control id, looked up from the label's root node outward through enclosing shadow roots to the document (FFI-PTR-01, FFI-PTR-04), using the focus-visible option; when no control id resolves or no element is found nothing happens. It MUST NOT open popups or toggle controls.
  Source: `internals/labelable-provider/useLabel.ts:30-44,70-76,103-121`. Test: [L4] `FieldLabel.test.tsx` "when nativeLabel={false}, clicking focuses the associated control".
- **FLD-PTR-04** — MUST. Consumer `onMouseDown`/`onClick`/`onPointerDown` handlers on `Field.Label` MUST run before the built-in ones and MUST be able to suppress them with `preventBaseUIHandler()` (EVD-API-11); a consumer `preventDefault()` on `mousedown` does not change the outcome (the guard only adds a `preventDefault`).
  Source: `field/label/FieldLabel.tsx:81`. Test: [L2] port title "runs the consumer's label pointer handler inside the decision, and Blazor's dispatch of the same event does not run it again" (added 2026-10-22).
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): see FLD-API-14. A C# handler cannot call `preventDefault()`; a consumer JavaScript listener's `preventDefault()` on `mousedown` leaves the guard as stated.

### 6.3 Focus management

No Field part sets or changes `tabindex` (FLD-FOC-07); `Field.Control` is focusable because it is a native `<input>` (or whatever element is substituted). Focus tracking exists only to publish the field's `focused` flag.

- **FLD-FOC-01** — MUST. When `Field.Control` receives focus it MUST acquire the field's `focused` flag (set to `true`) unless it is effectively disabled, and MUST record itself as the focus owner (FFI-ALG-13).
  Source: `field/control/FieldControl.tsx:107-108,144-146`, `internals/field-root-context/useSetFieldFocused.ts:22-30`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-focused] style hook to all components when focused".
- **FLD-FOC-02** — MUST. When `Field.Control` loses focus it MUST set the field `touched` (FFI-STA-07), release the `focused` flag if it is the owner, and then — only in `'onBlur'` mode — commit its value (FLD-FRM-03). The order is touched → focused → commit, all synchronously inside the blur event.
  Source: `field/control/FieldControl.tsx:147-153`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-touched] style hook to all components when touched", "should apply [data-focused] style hook to all components when focused", "validates the field on blur".
- **FLD-FOC-03** — MUST. The `focused` flag MUST be released without a blur event when the owning `Field.Control` becomes effectively disabled (through its own prop or the field's) or unmounts; a different control in the same field disabling or unmounting MUST NOT release it (FFI-STA-09).
  Source: `internals/field-root-context/useSetFieldFocused.ts:36-43`. Test: [L2] `FieldControl.test.tsx` "is removed when the focused control becomes disabled", "is removed when the field root becomes disabled", "is removed when the focused control unmounts".
- **FLD-FOC-04** — MUST. When `Field.Control` mounts, or its effective disabled state changes to enabled, while its element is already the active element of its root node (e.g. focused by the browser's `autofocus` before hydration), it MUST acquire the `focused` flag post-commit, pre-paint without a focus event; re-enabling while focus is elsewhere MUST NOT acquire it.
  Source: `internals/field-root-context/useSetFieldFocused.ts:36-43`. Test: [L4] `FieldControl.test.tsx` "should sync focused state when autoFocus is used with SSR", "can be re-acquired after the control is re-enabled".
  Port note 2026-10-22 (settled by the orchestrator 2026-10-22, ADR 02 verification item 1 and ADR 15): interactive activation replaces the prerendered subtree, so the focus the browser's `autofocus` gave the prerendered input is lost by the platform. At its first attach the `form` module focuses a control rendered with `AutoFocus` when the active element is `<body>` (Base UI's `autoFocus` is React's client-mount `focus()`), then reports the active element as this row states. No ADR 01 row. Evidence: the L4 title "should sync focused state when autoFocus is used with SSR" on both modes (`field.spec.ts`, `input.spec.ts`).
- **FLD-FOC-05** — MUST. Field parts MUST NOT move focus on their own except through the label click (FLD-PTR-03) and the enclosing `Form`'s blocked-submission focus (FFI-FOC-01), which focuses the registered `Field.Control` element and, when it is an `<input>`, selects its text (FLD-FOC-06).
  Source: `field/control/FieldControl.tsx:80-87` (registers the element handle), `form/Form.tsx:41-69`. Test: none in `field/` (`Form.test.tsx` "blocks submit and focuses the first invalid field across custom and native validation").
- **FLD-FOC-06** — MUST. The blocked-submission focus (FFI-FOC-01) MUST call `select()` on the focused `Field.Control` element only when that element's `tagName` is `INPUT`; a control substituted with another element (e.g. `<textarea>`, a `contenteditable` element) is focused but its content is not selected.
  Source: `form/Form.tsx:61-65`; see FFI-FOC-01. Test: none (no test asserts the selection or its absence).
- **FLD-FOC-07** — MUST. No Field part MUST set, change or remove `tabindex`: `Field.Control` passes no `tabIndex` of its own, so its focusability is that of the rendered element (native `<input>` by default, or the substitute), and a consumer-supplied `tabIndex` is forwarded unchanged.
  Source: `field/control/FieldControl.tsx:111-195`. Test: none.

### 6.4 Dismissal

Not applicable — Field has no popup; nothing is dismissed. (`Field.Error` disappears through validation state, §9, not through a dismissal gesture.)

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `Field.Control.onValueChange` | `(value: string, details: Field.Control.ChangeEventDetails) => void` | On every native change of the element's value by the user, synchronously inside the native `input`/`change` event, before any field state is written | `'none'` | yes — cancel suppresses error clearing and validation only (FLD-EVT-04) |
| `Field.Root.validate` | `(value: unknown, formValues: Form.Values) => string \| string[] \| null \| void \| Promise<…>` | At every commit (§8): submit, blur (`onBlur`), change (`onChange`, or `onSubmit` after a submission), Enter, imperative `validate()` | — | no |
| consumer `onChange`, `onFocus`, `onBlur`, `onKeyDown` on `Field.Control` | native | Before the built-in handler for the same event | — | `preventBaseUIHandler()` |
| consumer `onMouseDown`/`onClick`/`onPointerDown` on `Field.Label` | native | Before the built-in handler | — | `preventBaseUIHandler()` |

- **FLD-EVT-01** — MUST. `onValueChange` MUST be called with `(element.value, details)` where `details` is a change-event-details object (EVD-API-01) with `reason === 'none'`, `event` the native `input`/`change` event (EVD-API-05) and `trigger` `undefined`.
  Source: `field/control/FieldControl.tsx:124-126,213-216`. Test: [L2] `FieldControl.test.tsx` "does not validate when the change is canceled" (`details.cancel()` available), "renders once per keystroke for controlled input changes" (`value` string).
- **FLD-EVT-02** — MUST. `onValueChange` MUST be invoked exactly once per native change event, synchronously, before the control writes `dirty`/`filled` or starts validation (EVD-EVT-02, EVD-EVT-03).
  Source: `field/control/FieldControl.tsx:123-143`. Test: [L2] `FieldControl.test.tsx` "validates once when changed by the user", "does not clear errors or validate when change is prevented" (called once).
- **FLD-EVT-03** — MUST. `onValueChange` MUST NOT be called for mount, for a change of the `value`/`defaultValue` prop, or for a programmatic assignment to the element's `value`; it is user-change driven only (EVD-STA-06).
  Source: `field/control/FieldControl.tsx:96-105` (prop changes handled separately, no callback). Test: [L2] `FieldControl.test.tsx` "syncs state and validates when the controlled value changes programmatically" (`validate` called, `onValueChange` is `setValue` and not re-invoked by the programmatic set — asserted indirectly by a single `validate` call).
- **FLD-EVT-04** — MUST. In the component-owned mode, when the consumer calls `details.cancel()`: the element keeps the typed value (there is no component state to revert), the field's `dirty` and `filled` flags are still written, but `clearErrors` and validation MUST NOT run (so a form error stays and `validate` is not called). In the consumer-owned mode `cancel()` has no effect on the part (EVD-EVT-06); the consumer simply does not apply the value.
  Source: `field/control/FieldControl.tsx:130-142`. Test: [L2] `FieldControl.test.tsx` "does not validate when the change is canceled"; dirty/filled after cancel: none.
- **FLD-EVT-05** — MUST. When the native change event was default-prevented before the part handled it, `onValueChange` MUST still be called, `dirty`/`filled` MUST still be written (component-owned mode), but `clearErrors` and validation MUST NOT run (FFI-EVT-02).
  Source: `field/control/FieldControl.tsx:138-142`. Test: [L2] `FieldControl.test.tsx` "does not clear errors or validate when change is prevented".
- **FLD-EVT-06** — MUST. In the consumer-owned mode the field reactions MUST be driven by the serialized `value` prop, not by the event: whenever `String(value)` differs from its value in the part's previous render (user-applied or programmatic), post-commit, pre-paint, the control MUST run `clearErrors(effectiveName)` → `setDirty(String(value) !== (initialValue ?? ''))` → `validation.change(String(value))` exactly once (FFI-EVT-02); `filled` is recomputed by FLD-STA-13. A value that becomes `undefined` MUST NOT run the sequence.
  Source: `field/control/FieldControl.tsx:96-105`, `internals/useValueChanged.ts:10-16`. Test: [L2] `FieldControl.test.tsx` "validates once when a controlled value is changed by the user", "syncs state and validates when the controlled value changes programmatically", "renders once per keystroke for controlled input changes".
- **FLD-EVT-07** — MUST. In the component-owned mode the reactions MUST run synchronously inside the change event, in the order `setDirty` → `setFilled` → (unless cancelled/prevented) `clearErrors(effectiveName)` → `validation.change(inputValue)`.
  Source: `field/control/FieldControl.tsx:134-142`. Test: [L2] `FieldControl.test.tsx` "validates once when changed by the user", "shows a required error when a prefilled value is cleared", "avoids rerendering for uncontrolled input changes".
- **FLD-EVT-08** — MUST. `validate` MUST be called with the committed value as first argument (the serialized string for `Field.Control`; the raw logical value for other controls) and the registry-derived values object as second (FFI-EVT-05), at most once per commit; the latest `validate` function passed to `Field.Root` is used even for a commit scheduled earlier.
  Source: `field/root/FieldRoot.tsx:46`, `field/root/useFieldValidation.ts:293-300`. Test: [L2] `FieldRoot.test.tsx` "receives all form values as the 2nd argument", "unmounted fields are excluded from the validate fn", "validates the current control value when the `validate` method is called".
- **FLD-EVT-09** — MUST. Consumer native handlers on `Field.Control` (`onChange`, `onFocus`, `onBlur`, `onKeyDown`) MUST run before the corresponding built-in handler and MUST be able to suppress it with `preventBaseUIHandler()`; `stopPropagation()` MUST NOT suppress it (EVD-API-11).
  Source: `field/control/FieldControl.tsx:193`. Test: [L2] `FieldRoot.test.tsx` "changes [data-filled] when the value is changed externally" (consumer `onChange` runs and the built-in still tracks `filled`); `FieldControl.test.tsx` "validates the final controlled value when it is normalized on blur" (consumer `onBlur` runs before the built-in commit reads the DOM).
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, settled by the orchestrator 2026-10-22): the consumer's `oninput`, `onchange`, `onblur` and `onkeydown` run from the `form` module's report, before the part's logic, with arguments built from the report. `PreventPartHandler()` vetoes the part's reaction on both modes with no decision point. `onfocus` stays chained by the render pipeline. Tested by the L2 port title "runs the consumer's input, blur and keydown handlers before the control's own, which a veto skips" (FLD §16 Q12).

## 8. Form integration

The mechanism (registration, the commit algorithm, native state reading, custom-validity ownership, asynchronous validators, form errors, `Form.Values`) is specified in [FFI](../architecture/field-and-form-infrastructure.md). This section states what is observable through the Field parts and how `Field.Control` configures the mechanism; other controls' configurations are in FFI §6.3 and their own documents.

**Validation triggers by `validationMode`** (FLD-FRM-03):

| Trigger | `'onSubmit'` (default) | `'onBlur'` | `'onChange'` |
|---|---|---|---|
| Enclosing `Form` submit | full commit of every registered field (dirty latch set) | same | same |
| Control value change, before the first submission | **revalidate pass** only (FFI-FRM-05): acts only while already invalid, clears a resolved `valueMissing`, defers other native errors | revalidate pass only | **full commit** (debounced by `validationDebounceTime` when > 0 and the value is non-empty; immediate otherwise) |
| Control value change, after a submission (`submitCount > 0`) | full commit (debounced as in `onChange`) | revalidate pass only | full commit |
| Blur of the control | `touched` only | **full commit** of the value at blur (+ FFI-FRM-32 microtask re-commit for consumer-owned `Field.Control`) | `touched` only |
| <kbd>Enter</kbd> in `Field.Control` `<input>` | full commit per FLD-KEY-01 | same | same |
| `actionsRef.current.validate()` / `Form.actionsRef.validate(name)` | full commit (dirty latch set) | same | same |
| Outside any `Form` | never validates on its own (FFI-STR-05) except Enter and imperative | blur commits | change commits |

- **FLD-FRM-01** — MUST. `Field.Control` MUST register itself as the field's active control post-commit, pre-paint on mount and whenever its id, effective name, serialized value or enabled state changes (FFI-ALG-12), with: its element as the control handle (focused on blocked submission), its rendered id as the registry key, `String(value)` (consumer-owned) or `undefined` (component-owned) as the registered value, a getter returning the element's live value as the form projection, `enabled = !effectiveDisabled`, and its own `name` prop as the name fallback. It MUST unregister on unmount. Server: relaxed, see §9.
  Source: `field/control/FieldControl.tsx:78-87`, `internals/field-register-control/useRegisterFieldControl.ts:21-45`. Test: [L4] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values", "unmounted fields are excluded from the validate fn", "keeps the original baseline when a controlled control remounts"; `FieldControl.test.tsx` "is removed when the focused control unmounts".
- **FLD-FRM-02** — MUST. `Field.Control` MUST participate in native `FormData` through its own element (`name=value`, FFI-FRM-28) and MUST project its live element value (a string) into `Form.Values` and into other fields' `formValues` (FFI-API-07, FFI-FRM-33); its `initialValue` is the string present at first registration.
  Source: `field/control/FieldControl.tsx:78,84`. Test: [L2] `FieldRoot.test.tsx` "receives all form values as the 2nd argument" (`input: 'https://base-ui.com'`), "updates the Field.Control name fallback when the name changes" (L4 for the native `FormData` clause).
  Port note 2026-10-22: the native `FormData` clause is L4 (M2 plan), and the titles that render `Form` land with PR 1c.
- **FLD-FRM-03** — MUST. Validation MUST run exactly at the moments in the table above for the effective `validationMode` (FFI-FRM-01..04): `'onChange'` commits on every value change; `'onBlur'` commits on blur with the value present at blur; `'onSubmit'` commits on form submission and, after the first submission, on every change; in every mode a value change while already invalid runs at least the revalidate pass, and form submission, Enter and imperative validation commit regardless of mode.
  Source: `field/root/FieldRoot.tsx:88-92`, `field/root/useFieldValidation.ts:349-365`, `field/control/FieldControl.tsx:147-171`. Test: [L2] `FieldRoot.test.tsx` "should validate the field on submit", "revalidates on change", "validates the field on change", "validates the field on blur", "when not in <Form> the function does not run by default"; `FieldValidity.test.tsx` "should pass validity data" (both modes).
- **FLD-FRM-04** — MUST. With `validationDebounceTime > 0`, a change-triggered full commit with a non-empty value MUST be delayed by that many milliseconds, restarting on each change; an empty value MUST commit immediately; the pending timer MUST be cancelled when the control unmounts, is disabled, is replaced by another control, or when a blur/submit/imperative commit runs first (FFI-FRM-02, FFI-FRM-15). The pending commit MUST use the value captured when it was scheduled.
  Source: `field/root/useFieldValidation.ts:278,349-365`, `internals/field-register-control/useFieldControlRegistration.ts:136-153`. Test: [L2] `FieldRoot.test.tsx` "should debounce validation", "drops a pending debounced validation when the control unmounts", "drops a pending validation when another field-aware control takes ownership", "keeps a pending debounce armed across the first registration", "ignores async validation results superseded during debounce"; `FieldControl.test.tsx` "shows a required error when a prefilled value is cleared".
- **FLD-FRM-05** — MUST. Native constraints are the element's own (`required`, `type`, `pattern`, `minLength`, `maxLength`, `min`, `max`, `step`), read from its `ValidityState` at each commit (FFI-FRM-06); a `valueMissing` that is the only failure MUST be reported as valid until the dirty latch is set (a user change, a defined `dirty` prop, a submission or an imperative validation), and any other native failure MUST be reported immediately even on a pristine field.
  Source: `field/root/useFieldValidation.ts:194-223`. Test: [L2] `FieldRoot.test.tsx` "should not mark invalid if `valueMissing` is the only error and not yet dirtied", "should mark invalid if `valueMissing` is the only error and dirtied", "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty", "uses the controlled dirty state for required validation", "does not publish errors while `valueMissing` is suppressed"; `FieldError.test.tsx` "should show error messages by default".
- **FLD-FRM-06** — MUST. When `validate` yields errors, the messages joined with `\n` (CR/CRLF normalized to LF) MUST be installed as the control element's custom validity (so native `validationMessage`/`reportValidity` see them) if the element validates; the field MUST clear only the message it installed before each read, restoring a displaced consumer message; a consumer `setCustomValidity` message MUST be reported as `customError`, shown by `Field.Error`, and MUST block submission (FFI-FRM-07..10).
  Source: `field/root/useFieldValidation.ts:167-182,254,280-281,337-340`. Test: [L2] `FieldRoot.test.tsx` "custom validity ownership" (all 12 tests).
- **FLD-FRM-07** — MUST. `validate` MUST run only when the element reports no native error message, or when change-validation is active (`'onChange'`, or `'onSubmit'` after a submission): in `'onBlur'` mode and on the first `'onSubmit'` submission a native failure short-circuits it (FFI-FRM-07). When it runs and fails, the published state MUST be the native flags with `valid: false, customError: true`.
  Source: `field/root/useFieldValidation.ts:283-346`. Test: [L2] `FieldRoot.test.tsx` "runs after native validations", "keeps a native constraint failure published while the validator is in flight in %s mode"; `FieldError.test.tsx` "should show custom errors".
- **FLD-FRM-08** — MUST. Asynchronous validators MUST follow FFI-FRM-11..14: publish immediately (native failure kept; otherwise neutral `valid: null` with no `data-valid`/`data-invalid`/`aria-invalid`/error, except that an already-published custom error in `'onBlur'`/`'onChange'` stays), discard superseded results, keep the published state on rejection (and propagate the rejection to the caller of `commit`), and never block submission by themselves.
  Source: `field/root/useFieldValidation.ts:303-326`. Test: [L2] `FieldRoot.test.tsx` "async validation pending state" (all 7 tests), "supports async validation", "ignores stale async validation results", "drops an in-flight async validation when the control unmounts", "keeps the published error when async validation rejects".
- **FLD-FRM-09** — MUST. A `Form.errors` entry for the field's effective name MUST make the field invalid (every part `data-invalid`, control `aria-invalid`, `Field.Error` default slot shows it) and MUST be cleared on the next value change (FFI-FRM-25, FFI-FRM-26); the `invalid` prop MUST make the field invalid without a message and MUST block submission (FFI-FRM-27). Keys are looked up as own properties only (FFI-API-04).
  Source: `field/root/FieldRoot.tsx:94-97`, `field/control/FieldControl.tsx:101,140`. Test: [L2] `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors", "ignores inherited Form error properties", "ignores empty Form error arrays"; `FieldRoot.test.tsx` "uses the Field.Control name fallback when the Field.Root name is removed", "keeps a disabled field with form errors marked invalid"; `FieldControl.test.tsx` "does not clear errors or validate when change is prevented".
- **FLD-FRM-10** — MUST. No Field part listens for the form `reset` event: a native reset restores a component-owned `Field.Control`'s element value through the browser only, and MUST NOT change `dirty`, `touched`, `filled`, validity data, `aria-invalid` or `Field.Error` (FFI-FRM-30).
  Source: absence of any `reset` listener under `field/`. Test: none in `field/` (`SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset").
- **FLD-FRM-11** — MUST. While `Field.Control` is effectively disabled it MUST be unregistered from the field and the form (removed from submission validation and `Form.Values`, FFI-FRM-17), its custom validity installed by the field MUST be cleared, `aria-invalid` MUST be absent, and computed validity MUST read as neutral (FLD-STA-09); re-enabling MUST re-register it (keeping the field's `initialValue`).
  Source: `field/control/FieldControl.tsx:85`, `field/root/useFieldValidation.ts:179,371`. Test: [L2] `FieldRoot.test.tsx` "clears its own message on an input that became disabled", "keeps an explicitly invalid field marked invalid while disabled"; `FieldControl.test.tsx` "can be re-acquired after the control is re-enabled".
- **FLD-FRM-12** — MUST. A `Field.Root` without a registered control (only `Field.Error`/`Field.Validity` inside, or the control unmounted) MUST still run its `validate` on `actionsRef.current.validate()` and on form submission when registered, committing the last committed value (`null` initially), and MUST render the result through `Field.Error` (FFI-FRM-21, FFI-EDGE-10).
  Source: `internals/field-register-control/useFieldControlRegistration.ts:53-63`. Test: [L2] `FieldRoot.test.tsx` "validates a logical field without a mounted control"; `FieldRoot.react17.test.tsx` "allows mount-time imperative validation before the fallback id is assigned".
- **FLD-FRM-13** — MUST. The revalidate pass on change (FFI-FRM-05) MUST be observable through the parts as: a resolved `valueMissing` clears `data-invalid`, `aria-invalid` and the default `Field.Error` immediately on the keystroke (even in `'onBlur'`/`'onSubmit'` mode and even if a `typeMismatch` is now present, which is reported only at the next blur/submit); a still-missing value with no other native error re-publishes `valueMissing` immediately; a `Field.Error match="valueMissing"` therefore appears/disappears per keystroke while `match="typeMismatch"` waits for the boundary.
  Source: `field/root/useFieldValidation.ts:244-276`. Test: [L2] `FieldRoot.test.tsx` "revalidates on change for `valueMissing`", "handles both `required` and `typeMismatch`", "revalidates on change when clearing a type mismatch leaves only `valueMissing`", "clears valueMissing on change but defers other native errors like typeMismatch until blur when both are active"; `FieldError.test.tsx` "should only render when `match` matches constraint validation"; `FieldValidity.test.tsx` "surfaces valueMissing immediately after a stale custom error in $validationMode mode", "defers badInput during required change revalidation".
- **FLD-FRM-14** — MUST. Replacing the active control (a different control registers while one is active) MUST cancel the previous control's pending/in-flight validation, keep the field's `initialValue`, and from then on validate and submit the replacement's value (FFI-STR-07, FFI-STA-04).
  Source: `internals/field-register-control/useFieldControlRegistration.ts:147-167`. Test: [L2] `FieldRoot.test.tsx` "drops a pending validation when another field-aware control takes ownership", "submits the replacement control value when swapping field-aware controls", "keeps the field baseline when the control is swapped", "clears dirty after an empty text control returns to empty following a null-valued control".
- **FLD-FRM-15** — MUST. Changing `Field.Root.name` or the control's `name` at runtime MUST re-key the field's effective name in the same update: `Form.errors` lookups, `Form.Values` keys and `clearErrors` all use the new name, and removing every name drops the field from `Form.Values` (FFI-STA-10, FFI-FRM-19).
  Source: `field/root/FieldRoot.tsx:61-62`, `internals/field-register-control/useFieldControlRegistration.ts:105-120`. Test: [L2] `FieldRoot.test.tsx` "updates the Field.Control name fallback when the name changes", "uses the Field.Control name fallback when the Field.Root name is removed", "updates field-aware control name fallbacks when the name changes", "excludes registration-gated controls from onFormSubmit when their field name is removed".
- **FLD-FRM-17** — MUST. A consumer-owned (`value` given) `Field.Control` in `'onBlur'` mode MUST, after committing the value present at blur, queue one microtask that commits again with the element's then-current value only when that value is defined, differs from the blurred value and differs from the field's initial value (`initialValue ?? ''`), so a blur handler that normalizes the value is validated with the final value while a rewrite back to the initial value is not validated. The component-owned mode queues no re-commit. Observable result per FFI-FRM-32. Server: relaxed, see §9.
  Source: `field/control/FieldControl.tsx:151-169`; see FFI-FRM-32. Test: [L4] `FieldControl.test.tsx` "validates the final controlled value when it is normalized on blur", "keeps the final async validation when a controlled value is normalized on blur", "does not validate when a controlled value is reset to the initial value on blur".
- **FLD-FRM-18** — MUST. A `Field.Control` rendered inside a `Form` but outside any `Field.Root` reads the default (no-op) field context — no `touched`/`dirty`/`filled`/`focused` tracking and no validation — but MUST still call the enclosing Form's `clearErrors` with its own `name` prop: on every uncontrolled change that is neither cancelled nor default-prevented (FLD-EVT-07) and on every change of the serialized consumer-owned `value` (FLD-EVT-06). A `Form.errors` entry keyed by that `name` is therefore removed on change; without a `name` (`clearErrors(undefined)`) nothing is cleared.
  Source: `field/control/FieldControl.tsx:49-63,96-105,138-142`; `internals/field-root-context/FieldRootContext.ts:37-69`; `form/Form.tsx:143-155`. Test: none.

## 9. Lifecycle, mounting and animation

Vocabulary (conventions §4): **same commit** — in the DOM update that applies the triggering state change; **post-commit, pre-paint**; **next frame**; **on animations finished** — per TRN-ALG-03 (all finite CSS animations/transitions on the element itself have ended or been cancelled; with none running, next frame plus one microtask; when the animations-disabled flag is set, immediately).

Only `Field.Error` mounts and unmounts. It has no `keepMounted`, no `onOpenChangeComplete`, no `actionsRef` and no `hidden="until-found"` mode. "Rendered" below means FLD-API-30 is `true`; "mounted" means the element exists (FLD-DOM-14).

State diagram (`Field.Error`):

```
not mounted ──(rendered becomes true)──▶ mounted, status 'starting'   [same commit]
                                              │ next frame
                                              ▼
                                         mounted, status unset
                                              │ (rendered becomes false)  [same commit]
                                              ▼
                                         mounted, status 'ending', content = last message
                                              │ on animations finished → unmounted, status unset
                                              │ (rendered becomes true again) → status cleared next frame, stays mounted (no 'starting')
```

- **FLD-ANI-01** — MUST. `Field.Error` MUST become mounted in the same commit in which it becomes rendered (FLD-API-30), and MUST remain mounted after it stops being rendered until its exit completes (FLD-ANI-03); the element MUST NOT exist while not mounted (the part renders nothing). Toggling `match`, the field's validity, a form error, or the field's disabled state are all routes to the same rendered predicate.
  Source: `field/error/FieldError.tsx:46-57,129-136`, `internals/useTransitionStatus.ts:29-34`. Test: [L2] `FieldError.test.tsx` "should show error messages by default", "should only render when `match` matches constraint validation", "applies data-ending-style before unmount"; `FieldRoot.test.tsx` "drops a pending debounced validation when the control unmounts" (never mounts) (L4 for the exit-completion clause).
- **FLD-ANI-02** — MUST. When `Field.Error` becomes rendered after having been unmounted, its element MUST carry `data-starting-style=""` in the mounting commit and the attribute MUST be removed on the next frame (TRN-STA-03, TRN-STA-04), so a CSS transition from the `[data-starting-style]` state runs.
  Source: `field/error/FieldError.tsx:57`, `internals/useTransitionStatus.ts:31-34,58-73`. Test: [L4] `FieldError.test.tsx` "triggers enter animation via data-starting-style when mounting".
- **FLD-ANI-03** — MUST. When `Field.Error` stops being rendered while mounted, its element MUST carry `data-ending-style=""` in that same commit (TRN-STA-06) and MUST be removed from the DOM on animations finished (TRN-ANI-03, TRN-ANI-17): the completion watcher is armed after the commit, inspects the element's own animations on the next frame, waits for the finite ones, and then unmounts in a synchronous commit; with no animations the element is gone one frame plus one microtask after the commit; with the animations-disabled flag it is gone immediately. The status becomes unset when unmounted (TRN-STA-08). Server: relaxed, see §9.
  Source: `field/error/FieldError.tsx:103-111,129-136`, `internals/useOpenChangeComplete.tsx:15-27`, `internals/useTransitionStatus.ts:36-42`. Test: [L4] `FieldError.test.tsx` "applies data-ending-style before unmount".
- **FLD-ANI-04** — MUST. If `Field.Error` becomes rendered again while still mounted in its exit phase, it MUST NOT re-enter `'starting'`: the `'ending'` status is cleared on the next frame, the pending completion is aborted so no unmount happens (TRN-STA-09, TRN-ANI-08), and the content switches back to the live message in the same commit.
  Source: `internals/useTransitionStatus.ts:31,58-73`, `internals/useOpenChangeComplete.tsx:20-26`. Test: none.
- **FLD-ANI-05** — MUST. A `Field.Error` that is rendered in the part's first render (e.g. `match` already `true`, or a form error present at mount) MUST mount without `data-starting-style` and without requesting a frame (TRN-STA-13); observed through bUnit (L2): the first render batch carries no `data-starting-style`.
  Source: `internals/useTransitionStatus.ts:23-29` (`animateInitialOpen` defaults to `false`). Test: [L2] `FieldError.test.tsx` "always renders the error message when `match` is true" (present synchronously after render).
- **FLD-ANI-06** — MUST. `Field.Validity`'s `transitionStatus` MUST follow the same machine applied to the *combined invalid* state (`validity.valid === false`) with no unmount step: it is unset initially; becomes `'starting'` in the commit in which the field first becomes invalid and unset on the next frame; becomes `'ending'` in the commit in which the field stops being invalid and then **stays `'ending'`** (nothing sets the mounted flag back) until the field becomes invalid again, at which point it is cleared to unset on the next frame without passing through `'starting'`. A field that is invalid on its first render starts with the status unset.
  Source: `field/validity/FieldValidity.tsx:24-25,34`, `internals/useTransitionStatus.ts:23-42,58-73`. Test: [L2] `FieldValidity.test.tsx` "should pass validity data" (`transitionStatus` property present); sequence: none (see §16).
- **FLD-ANI-07** — MUST. `Field.Root`, `Field.Label`, `Field.Control`, `Field.Description` and `Field.Item` MUST be mounted for as long as they are rendered by the consumer; they have no transition status and emit no `data-starting-style`/`data-ending-style`.
  Source: `field/root/FieldRoot.tsx:188-193`, `field/label/FieldLabel.tsx:78-83`, `field/control/FieldControl.tsx:111-197`, `field/description/FieldDescription.tsx:48-53`, `field/item/FieldItem.tsx:36-41`. Test: none.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- FLD-STA-06 — row RND-STA-01 for mirrored fields: `transitionStatus` is JS-written at frame timing (`data-starting-style`/`data-ending-style`); the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.
- FLD-ARIA-04 — row EVD-STA-07, CMP-STA-10, FFI registration (post-commit, pre-paint C# state work): the description id publication runs in `OnAfterRender`; on Server after paint plus a round trip, observable only as latency of the dependent attribute updates.
- FLD-ARIA-05 — row EVD-STA-07, CMP-STA-10, FFI registration (post-commit, pre-paint C# state work): the error id publication runs in `OnAfterRender`; on Server after paint plus a round trip, observable only as latency of the dependent attribute updates.
- FLD-ARIA-06 — row EVD-STA-07, CMP-STA-10, FFI registration (post-commit, pre-paint C# state work): the label id publication runs in `OnAfterRender`; on Server after paint plus a round trip, observable only as latency of the dependent attribute updates.
- FLD-FRM-01 — row EVD-STA-07, CMP-STA-10, FFI registration (post-commit, pre-paint C# state work): the control registration runs in `OnAfterRender`; on Server after paint plus a round trip, observable only as latency of the dependent attribute updates.
- FLD-FRM-17 — row PSH-STA-23, FLD-FRM-17: the blur re-commit runs in `OnAfterRender`; on Server after paint plus a round trip, during which the field keeps its pre-normalised value.
- FLD-ANI-03 — row TRN-ANI-03, TRN-ANI-09, TRN-ANI-10: the error's removal lands in the first render batch after the completion message, about two round trips later; JS's optimistic convergent write hides the element at animation end meanwhile.
- 2026-10-22 (ADR 01 Amendments 2026-10-22): FLD-ARIA-04 and FLD-ARIA-06 are met on both modes in the port, since the description and the label publish their ids in `OnParametersSet` (port notes on both rows). FLD-ARIA-05 follows with `FieldError` in PR 1c. FLD-FRM-01 stays relaxed: the control-id claim registers the control, so it stays post-render (§16 item 7). ADR 01's row for FLD-API-15 (added 2026-10-22, chosen by the user 2026-10-22) adds a controlled edit the consumer does not apply, reverted a round trip later on Server.

## 10. Positioning

Not applicable — no Field part is a floating element.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (element rendering, `className`/`style` functions, state attributes, prop merging) | [RND](../architecture/rendering-and-composition.md) | Root: `'div'`, mapping `fieldValidityMapping`. Label: `'label'`, props `[labelProps, elementProps]`. Control: `'input'`, props `[builtIn, elementProps, validationPropsGetter]`, refs `[forwardedRef, inputRef]` + `validation.inputRef`. Description: `'p'`. Item: `'div'`. Error: `'div'`, mapping `fieldValidityMapping + transitionStatusMapping`, `enabled: mounted`, refs `[forwardedRef, errorRef]`. Validity: not used. |
| `getStateAttributesProps` + `fieldValidityMapping` | [RND](../architecture/rendering-and-composition.md) / [FFI](../architecture/field-and-form-infrastructure.md) FFI-DOM-01 | `valid: true → data-valid`, `false → data-invalid`, `null → nothing`; every other field by the default boolean rule. |
| Field scope (`FieldRootContext`), `useFieldRootContext(optional)` | [FFI](../architecture/field-and-form-infrastructure.md) FFI-STR-01, FFI-STR-02 | Root provides it; Label/Description/Error/Validity/Item require it (`optional = false`); Control reads it optionally. |
| Labelable scope (`LabelableProvider`, `useLabelableId`, `useLabel`, `useRegisteredLabelId`, `getDescriptionProps`) | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ALG-06..09, FFI-ARIA-01/02/04/08 | Root and Item each open a scope. Control claims the id with `{ id: idProp }` (enabled always). Label: `useLabel({ id: labelId ?? idProp, native: nativeLabel })`. Description/Error publish ids via `setMessageIds`. |
| Control registration (`useRegisterFieldControl` / `useFieldControlRegistration`) | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ALG-12, FFI-STA-04, FFI-FRM-17 | Root hosts the registry side and exposes `validate` through `actionsRef`. Control registers `(validation.inputRef, id, String(value) \| undefined, () => element.value, !disabled, nameProp)`. |
| Validation pipeline (`useFieldValidation`) | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ALG-01..05, FFI-ALG-14, FFI-FRM-01..33 | Root creates it with `validate`, `validationDebounceTime`, `validationMode`, `invalid`, the dirty latch and `shouldValidateOnChange`. Control: `change(serialized)` on value change, `commit(value)` on blur (`'onBlur'`) and Enter, `getValidationProps(disabled, props)` on the element, `inputRef` merged into the element. |
| Focus ownership (`useSetFieldFocused`) | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ALG-13, FFI-STA-09 | Control: `useSetFieldFocused(effectiveDisabled, elementRef)`. |
| Form scope (`useFormContext`) | [FFI](../architecture/field-and-form-infrastructure.md) FFI-STR-04/05, FFI-API-04, FFI-EVT-03 | Root reads `errors`, `validationMode`, `submitCountRef`; Control reads `clearErrors`, `elementRef`, `submitCountRef`; Error reads `errors`. |
| Fieldset scope (`useFieldsetRootContext(true)`) | [fieldset](fieldset.md) / FFI-STR-06 | Root reads `disabled`. |
| `useValueChanged` | [UTL](../architecture/shared-utilities.md) | Control: reacts to `String(value)` identity changes (FLD-EVT-06). |
| `useTimeout` | [UTL](../architecture/shared-utilities.md) | Control: the 0 ms Enter timer (FLD-KEY-01); Root (through the pipeline): the debounce timer. Both cleared on unmount. |
| `useBaseUiId` / `useId` | [RND](../architecture/rendering-and-composition.md) RND-DOM id rules, FFI-FRM-36 | Description, Error, Label and the scopes generate `base-ui-…` ids; an explicit id (including `''`) is used verbatim. |
| `useTransitionStatus`, `useOpenChangeComplete`, `useAnimationsFinished`, `transitionStatusMapping` | [TRN](../architecture/transitions-and-mounting.md) TRN-STA-01..09, TRN-DOM-01..03, TRN-ANI-03/08/17, TRN-ALG-03 | Error: `useTransitionStatus(rendered)` with all options default; `useOpenChangeComplete({ open: rendered, ref: errorRef, onComplete: () => !rendered && setMounted(false) })`. Validity: `useTransitionStatus(isInvalid)` only (no completion watcher). |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) EVD-API-01, EVD-API-05 | Control: `createChangeEventDetails('none', nativeEvent)` for `onValueChange`. |
| Dev `error()` channel + `SafeReact.captureOwnerStack` | [UTL](../architecture/shared-utilities.md) | Label: the two `nativeLabel` mismatch messages (FLD-EDGE-02), once per `nativeLabel` value after paint. |
| `describeConformance` | [RND](../architecture/rendering-and-composition.md) §4.9 | See §13. |

## 12. Edge cases, invariants and dev warnings

- **FLD-EDGE-01** — MUST. `Field.Label`, `Field.Description`, `Field.Error`, `Field.Validity` and `Field.Item` rendered outside `Field.Root` MUST throw the FFI-EDGE-01 error (message quoted there; no substituted parameters) at render time.
  Source: `internals/field-root-context/FieldRootContext.ts:74-78` and the callers in FLD-STR-02. Test: [L2] port title "throws a descriptive error when a field part is rendered outside <Field.Root>" (added 2026-10-22).
  Port note 2026-10-22 (PRG-EDGE-01 precedent, ADR 08 Amendments 2026-10-20): the message is ``Blazix: The `FieldRoot` context is missing. Field parts must be placed within `<FieldRoot>`.``, an `InvalidOperationException` thrown in every build. `FieldControl` (and `Input`) outside a field does not throw (FLD-STR-02). `Field.Error` and `Field.Validity` arrive with PR 1c.
- **FLD-EDGE-02** — MUST. In development, `Field.Label` MUST report (through the dev error channel, after paint, once per change of `nativeLabel`, and only when an element was actually rendered) the two mismatch messages of FFI-EDGE-02: the "expected a `<label>` element" message when `nativeLabel` is `true` and the rendered element's tag is not `LABEL`; the "expected a non-`<label>` element" message when `nativeLabel` is `false` and it is `LABEL`. Exactly one report is emitted per mismatch, prefixed `Base UI: ` and suffixed with an owner stack when the framework provides one (and unsuffixed otherwise). No report is emitted for the default `<label>` with `nativeLabel` `true`, nor when the substitute renders nothing. Not emitted in production.
  Source: `field/label/FieldLabel.tsx:48-76`. Test: [L2] `FieldLabel.test.tsx` "does not warn by default", "does not warn when the render function returns no element", "errors if nativeLabel=true but ref is not a label", "errors if nativeLabel=false but ref is a label"; `FieldRoot.react17.test.tsx` "reports label mismatches without the owner-stack API", "reports non-native label mismatches without the owner-stack API".
  Port note 2026-10-22 (ADR 06 Amendments 2026-10-18): the owner-stack half is React-only; the port has no owner stack. The `FieldRoot.react17.test.tsx` titles assert the message alone. The module checks the rendered element at attach and when `NativeLabel` changes, and reports a mismatch. C# logs it through `DevelopmentWarnings` (`Blazix: `-prefixed, `LogLevel.Error`) once per `NativeLabel` value. A substitute that renders no element is never attached, so it is never checked.
- **FLD-EDGE-03** — MUST. `Field.Description` and `Field.Error` given `id=""` MUST render `id=""` verbatim and MUST NOT publish the empty id (the control's `aria-describedby` is unaffected). `Field.Label` given `id=""` likewise publishes `''` as the label id, which reads as "no label" for `aria-labelledby` purposes.
  Source: `field/description/FieldDescription.tsx:25,37`, `field/error/FieldError.tsx:35,60`, `packages/utils/src/useId.ts:42-47`. Test: [L2] `FieldDescription.test.tsx` "does not register an empty description id"; `FieldError.test.tsx` "does not register an empty error id".
- **FLD-EDGE-04** — MUST. `Field.Error` MUST NOT reflect an enclosing `Field.Item`'s `disabled` flag: its `data-disabled` and its "not rendered while disabled" rule (FLD-API-30) use the field's disabled state only. (`Field.Label` and `Field.Description` do reflect the item.)
  Source: `field/error/FieldError.tsx:37,49,113-116` (no item scope read). Test: none.
- **FLD-EDGE-05** — SHOULD. Once a `Field.Label` has published its id to its scope, a later change of its `id` prop SHOULD be ignored (the published id keeps being rendered and referenced); only the id present when the label first registered is ever used. A port MAY instead honor the new id consistently (see §16 item 2).
  Source: `field/label/FieldLabel.tsx:44` (`labelId ?? idProp`), `utils/useRegisteredLabelId.ts:10-17`. Test: none.
  Port note 2026-10-22 (design point DP8 of M2 track 1, settled by the orchestrator 2026-10-22; §16 item 2): the port takes the MAY. Each `FieldLabel` renders and publishes its own `id`, and a change is published at once (the last publication wins, `LabelIdSlot`). Tested by the L2 port title "references the mounted label from the control, and removes the reference when the label is removed".
- **FLD-EDGE-06** — SHOULD. When several `Field.Label`s are mounted in the same labelable scope, every label after the first SHOULD render the first label's id (the scope's published id) as its own `id` and the scope keeps one label id; `aria-labelledby` on non-native controls therefore references a single id. A port SHOULD treat multiple labels per scope as unsupported (see §16 item 2).
  Source: `field/label/FieldLabel.tsx:35,44`. Test: none.
  Port note 2026-10-22 (DP8, settled by the orchestrator 2026-10-22): each label renders its own id and the last publication names the scope; several labels in one scope stay unsupported.
- **FLD-EDGE-07** — MUST. `match="valid"` is accepted (it is a `ValidityState` key) and renders the error exactly while the field's computed `valid` flag is `true`; `match="customError"` renders while the field or a consumer-set custom message is the active error; a string `match` ignores `Form.errors` and the `invalid` prop entirely (FFI-ALG-15).
  Source: `field/error/FieldError.tsx:51-52,153`. Test: [L2] `FieldError.test.tsx` "should show custom errors", "uses the client validation path for specific matches when Form errors are present"; `match="valid"`: none.
- **FLD-EDGE-08** — MUST. No Field behavior depends on text direction, locale or platform; `Field.Control`'s value is compared and submitted as a string without locale formatting.
  Source: `field/` (no direction/locale reads). Test: none.
- **FLD-EDGE-09** — MUST. Server-rendered markup MUST use the scope's generated default id for both `Field.Label`'s `for` and `Field.Control`'s `id` (so they match before any registration), MUST NOT carry an `aria-labelledby` on `Field.Control` pointing at an absent label, and an explicit control `id` MUST take effect after hydration in the same update as the label's `for` (FFI-FRM-34, FFI-FRM-36). Generated ids have the form `base-ui-<unique>`.
  Source: `internals/labelable-provider/useLabelableId.ts:79-82`, `internals/labelable-provider/LabelableProvider.tsx:12-20`, `internals/useBaseUiId.ts:9-11`. Test: [L2] `FieldRoot.test.tsx` "does not set `aria-labelledby` during SSR when Field.Label is absent" (Select trigger), "keeps `aria-labelledby` valid when toggling from Checkbox.Root to Select.Root after hydration"; `FieldControl.test.tsx` "should sync focused state when autoFocus is used with SSR".
- **FLD-EDGE-10** — MUST. `Field.Control` with `value={null}` MUST be treated as consumer-owned with no value for field purposes (serialized value `undefined`: no dirty/validation reaction, `filled` from the element), while the element receives `value={null}`; a numeric `value` MUST be serialized with `String()` for dirty comparison and validation.
  Source: `field/control/FieldControl.tsx:74-76,96-99`. Test: [L2] `FieldControl.test.tsx` "clears dirty state when a numeric controlled value returns to its initial value"; `null`: none.
  Port note 2026-10-22 (ADR 08 Amendments 2026-10-22): `Value` is `string?`. A controlled `null` is the serialized `undefined`: no dirty or validation reaction, and the module does not re-assert it, so the element keeps what it shows, as React treats `value={null}` as uncontrolled. The numeric clause does not arise; its title is translated over strings.
- **FLD-EDGE-11** — MUST. A `Field.Root` nested inside another `Field.Root` MUST provide fresh scopes: parts and controls bind to the nearest enclosing `Field.Root`, and the inner field's validity does not propagate to the outer one. A `Field.Item` outside any group simply scopes its label/description to whichever control registers inside it.
  Source: `field/root/FieldRoot.tsx:195,204-213`, `field/item/FieldItem.tsx:43-47`. Test: none.
- **FLD-EDGE-12** — MUST. `Field.Control` MUST NOT observe direct assignments to its element's `value` property (no flag or validation update) and MUST NOT rewrite the element's value on focus (FFI-EDGE-07); the next user change or validation reads whatever the element holds.
  Source: `field/control/FieldControl.tsx:111-122`. Test: [L4] `FieldRoot.test.tsx` "should not reset to defaultValue when input value is programmatically changed and then focused", "should not reset to defaultValue when input value is programmatically changed to non-empty value and then focused", "should apply [data-field] style hooks to field components" (`control.value = 'value'` then blur validates the assigned value).

Behavior under `disabled`: the field's parts keep rendering with `data-disabled`; `Field.Error` never renders unless `match === true` (FLD-API-30); the control carries the native `disabled` attribute and is unregistered (FLD-FRM-11); `focused` is released (FLD-FOC-03); `data-valid`/`data-invalid` show `null` unless externally invalid (FLD-STA-09). `Field.Control` has no `readOnly` handling of its own; a `readOnly` input is barred from constraint validation and the pipeline treats it per FFI-FRM-08 (`FieldRoot.test.tsx` "ignores a message set outside the field on a control barred from validation", "does not write its own message to a barred control"). Missing parts: §2. Zero/duplicate values: not applicable (a single string value).

## 13. Behaviors evidenced by tests

### `FieldRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, plain `render`) | FLD-DOM-19 |
| "updates label association when replacing one control with another" | FLD-STR-03, FLD-DOM-06 |
| "drops a stale explicit id when an id-less control replaces the control that owned it" | FLD-API-20, FLD-DOM-06 |
| "re-associates the label when a CheckboxGroup is replaced by another control" | FLD-DOM-06 |
| "updates label associations when the control id changes" | FLD-API-20, FLD-DOM-06 |
| "falls back to a generated id when the control id is removed" | FLD-API-20, FLD-DOM-06, FLD-DOM-08 |
| "does not set `aria-labelledby` during SSR when Field.Label is absent" | FLD-EDGE-09 |
| "keeps `aria-labelledby` valid when toggling from Checkbox.Root to Select.Root after hydration" | FLD-API-12, FLD-DOM-05, FLD-ARIA-06, FLD-EDGE-09 |
| "removes `aria-labelledby` when Field.Label is removed after hydration" | FLD-ARIA-01, FLD-ARIA-06 |
| "preserves label association without looping when a control is unmounted and remounted" / "keeps an explicit control id while the subtree is hidden" / "keeps the group label suppressed while its subtree is hidden" | FLD-DOM-06, FLD-FRM-16 |
| "preserves a non-native label association when a control is unmounted and remounted" | FLD-DOM-07, FLD-FRM-16 |
| "should add data-disabled style hook to all components" | FLD-API-02, FLD-API-10, FLD-API-19, FLD-STA-02, FLD-STA-03, FLD-STA-04, FLD-DOM-02, FLD-DOM-04 |
| "keeps an explicitly invalid field marked invalid while disabled" | FLD-API-03, FLD-STA-09, FLD-ARIA-03, FLD-FRM-11 |
| "keeps a disabled field with form errors marked invalid" | FLD-STA-09, FLD-FRM-09 |
| "when not in <Form> the function does not run by default" | FLD-FRM-03 |
| "runs after native validations" | FLD-FRM-07, FLD-FRM-13 |
| "treats $label result as valid" (an empty array / an undefined / an empty string / an array of empty strings) | FLD-API-04 |
| "publishes neutral validity while a validator is in flight in %s mode" (onSubmit / onChange / onBlur) | FLD-DOM-03, FLD-ARIA-03, FLD-FRM-08 |
| "retires a previously valid result to neutral while revalidating in %s mode" / "keeps a previously resolved error while revalidating in %s mode" (onChange / onBlur) / "retires a stale native error to neutral once the constraint passes again" / "retires a resolved async error to neutral while revalidating in onSubmit mode" | FLD-FRM-08 |
| "keeps a native constraint failure published while the validator is in flight in %s mode" (3 modes) | FLD-FRM-07, FLD-FRM-08 |
| "accepts synchronous and async validators with no return value" | FLD-API-04 |
| "should apply aria-invalid prop to control once validation finishes" | FLD-ARIA-03 |
| "receives all form values as the 2nd argument" | FLD-STR-04, FLD-EVT-08, FLD-FRM-02 |
| "unmounted fields are excluded from the validate fn" | FLD-EVT-08, FLD-FRM-01 |
| "submits the replacement control value when swapping field-aware controls" | FLD-STR-03, FLD-FRM-14 |
| "excludes registration-gated controls from onFormSubmit when their field name is removed" | FLD-FRM-15 |
| "uses the Field.Control name for form submission and form validation values" | FLD-API-01, FLD-API-18, FLD-DOM-09, FLD-FRM-01, FLD-FRM-02 |
| "updates the Field.Control name fallback when the name changes" | FLD-API-18, FLD-FRM-02, FLD-FRM-15 |
| "uses the Field.Control name fallback when the Field.Root name is removed" | FLD-API-01, FLD-FRM-09, FLD-FRM-15 |
| "updates field-aware control name fallbacks when the name changes" | FLD-FRM-15 |
| "should validate the field on submit" | FLD-API-32, FLD-FRM-03 |
| "revalidates on change" | FLD-FRM-03, FLD-FRM-13 |
| "validates the field on change" | FLD-ARIA-03, FLD-FRM-03 |
| "validates the field on blur" | FLD-FOC-02, FLD-FRM-03 |
| "should not mark invalid if `valueMissing` is the only error and not yet dirtied" / "should mark invalid if `valueMissing` is the only error and dirtied" / "should not mark field as invalid for valueMissing if not dirty" / "should mark field as invalid for valueMissing if dirty" | FLD-FRM-05 |
| "does not publish errors while `valueMissing` is suppressed" | FLD-STR-05, FLD-STA-07, FLD-FRM-05 |
| "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty" | FLD-API-23, FLD-FRM-05 |
| "supports async validation" / "ignores stale async validation results" / "drops an in-flight async validation when the control unmounts" / "keeps the published error when async validation rejects" | FLD-FRM-08 |
| "should apply [data-field] style hooks to field components" | FLD-STR-01, FLD-DOM-03, FLD-DOM-04, FLD-DOM-14, FLD-EDGE-12 |
| "revalidates on change for `valueMissing`" / "handles both `required` and `typeMismatch`" / "revalidates on change when clearing a type mismatch leaves only `valueMissing`" / "clears valueMissing on change but defers other native errors like typeMismatch until blur when both are active" | FLD-FRM-13 |
| "should debounce validation" / "should debounce validation for field-aware controls" / "should debounce validation for radio groups" | FLD-API-06, FLD-FRM-04 |
| "drops a pending debounced validation when the control unmounts" | FLD-FRM-04, FLD-ANI-01 |
| "ignores async validation results superseded during debounce" | FLD-FRM-04, FLD-FRM-08 |
| "drops a pending validation when another field-aware control takes ownership" | FLD-FRM-04, FLD-FRM-14 |
| "keeps a pending debounce armed across the first registration" | FLD-FRM-04 |
| "keeps a message set outside the field when submitting" / "keeps a message set outside the field when validating on change" / "keeps a message set outside the field when revalidating on change" / "clears the message it set itself" / "restores a message its own one displaced" / "does not restore a message that was withdrawn while its own one was installed" / "does not adopt a native message as the message it displaced" / "keeps a message set outside the field on another input of the same field" / "ignores a message set outside the field on a control barred from validation" / "does not write its own message to a barred control" | FLD-FRM-06, FLD-DOM-15 |
| "keeps other native errors deferred while a message set outside the field survives" | FLD-FRM-06, FLD-FRM-13 |
| "clears its own message on an input that became disabled" | FLD-DOM-10, FLD-FRM-06, FLD-FRM-11 |
| "should apply [data-touched] style hook to all components when touched" | FLD-DOM-02, FLD-DOM-04, FLD-FOC-02 |
| "should apply [data-dirty] style hook to all components when dirty" | FLD-STA-14, FLD-DOM-02, FLD-DOM-04 |
| "should clear [data-dirty] when a null-valued control returns to its empty initial value" / "should clear [data-dirty] when a Select returns to its null initial value" / "keeps [data-dirty] on a RadioGroup when returning to the first picked value" / "captures the baseline only once in StrictMode" | FLD-DOM-02 (control-side rule: FFI-STA-04, FFI-STA-05) |
| "clears dirty after an empty text control returns to empty following a null-valued control" | FLD-STA-14, FLD-FRM-14 |
| "keeps the original baseline when a controlled control remounts" | FLD-STA-14, FLD-FRM-01 |
| "keeps the field baseline when the control is swapped" | FLD-STA-14, FLD-FRM-14 |
| "should apply [data-filled] style hook to all components when filled" | FLD-STA-11, FLD-STA-13, FLD-DOM-02, FLD-DOM-04 |
| "changes [data-filled] when the value is changed externally" | FLD-API-15, FLD-STA-13, FLD-EVT-09 |
| "should apply [data-focused] style hook to all components when focused" | FLD-STA-11, FLD-DOM-02, FLD-DOM-04, FLD-FOC-01, FLD-FOC-02 |
| "should not reset to defaultValue when input value is programmatically changed and then focused" / "should not reset to defaultValue when input value is programmatically changed to non-empty value and then focused" | FLD-DOM-11, FLD-EDGE-12 |
| "controls the dirty state" / "controls the touched state" | FLD-API-07, FLD-STA-10, FLD-DOM-04 |
| "uses the controlled dirty state for required validation" | FLD-API-07, FLD-STA-10, FLD-FRM-05 |
| "does not update controlled dirty state from user input" / "does not update controlled touched state on blur" | FLD-API-07, FLD-STA-10 |
| "validates the field when the `validate` method is called" | FLD-API-08 |
| "validates a logical field without a mounted control" | FLD-API-08, FLD-FRM-12 |
| "validates the current control value when the `validate` method is called" | FLD-API-08, FLD-EVT-08 |

### `FieldRoot.react17.test.tsx`

| Test | Requirements |
|---|---|
| "falls back to a generated id when an explicit control id is removed" | FLD-API-20, FLD-DOM-06 |
| "allows mount-time imperative validation before the fallback id is assigned" | FLD-API-08, FLD-FRM-12 |
| "reports label mismatches without the owner-stack API" / "reports non-native label mismatches without the owner-stack API" | FLD-EDGE-02 |

### `FieldControl.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLInputElement`, rendered inside `<Field.Root>`) | FLD-DOM-19 |
| "avoids rerendering for uncontrolled input changes" / "renders once per keystroke for controlled input changes" | FLD-STA-12, FLD-EDGE-13 |
| "validates once when changed by the user" | FLD-EVT-02, FLD-EVT-07 |
| "validates once when a controlled value is changed by the user" | FLD-API-15, FLD-EVT-06 |
| "clears dirty state when a numeric controlled value returns to its initial value" | FLD-API-15, FLD-STA-14, FLD-EDGE-10 |
| "syncs state and validates when the controlled value changes programmatically" | FLD-API-15, FLD-EVT-03, FLD-EVT-06 |
| "validates the final controlled value when it is normalized on blur" | FLD-EVT-09, FLD-FRM-03, FLD-FRM-17 (FFI-FRM-32) |
| "keeps the final async validation when a controlled value is normalized on blur" | FLD-FRM-08, FLD-FRM-17 (FFI-FRM-32) |
| "does not validate when a controlled value is reset to the initial value on blur" | FLD-FRM-03, FLD-FRM-17 (FFI-FRM-32) |
| "sets filled state on mount when the control is prefilled" | FLD-API-16, FLD-STA-13 |
| "does not set filled state on mount for an empty controlled value" / "clears filled state when a controlled control remounts empty" / "clears filled state when an uncontrolled control remounts empty" | FLD-STA-13 |
| "forwards a changing defaultValue without warning" | FLD-API-16, FLD-DOM-11 |
| "keeps filled state from the edited value when defaultValue changes" | FLD-API-16, FLD-STA-13 |
| "sets filled state from a controlled value on a custom element" | FLD-API-22, FLD-STA-13 |
| "does not validate when the change is canceled" | FLD-EVT-01, FLD-EVT-04 |
| "does not clear errors or validate when change is prevented" | FLD-API-17, FLD-EVT-02, FLD-EVT-05, FLD-FRM-09 |
| "validates once when Enter implicitly submits a form" / "validates when Enter does not implicitly submit the form" / "validates when a disabled submit button blocks implicit submission" / "validates when Enter is pressed outside a form" | FLD-KEY-01 |
| "validates the latest value when Enter does not submit the form" | FLD-KEY-01 |
| "shows a required error when a prefilled value is cleared" | FLD-EVT-07, FLD-FRM-04 |
| "[data-focused]" › "is removed when the focused control becomes disabled" | FLD-API-19, FLD-DOM-10, FLD-FOC-03 |
| "[data-focused]" › "is removed when the field root becomes disabled" | FLD-API-02, FLD-FOC-03 |
| "[data-focused]" › "can be re-acquired after the control is re-enabled" | FLD-FOC-04, FLD-FRM-11 |
| "[data-focused]" › "is removed when the focused control unmounts" | FLD-FOC-03, FLD-FRM-01 |
| "should sync focused state when autoFocus is used with SSR" | FLD-API-21, FLD-DOM-12, FLD-FOC-04, FLD-EDGE-09 |
| "id" › "updates the label association when the control is swapped" | FLD-API-20, FLD-DOM-08 |

### `FieldControl.spec.tsx`

Type-level only (no runtime assertions): a `Field.Control` rendered with `render={<textarea />}` accepts a `ref` typed as `HTMLTextAreaElement`. Maps to FLD-API-22 (the DOM handle type follows the substituted element, RND-DOM-41).

### `FieldLabel.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLLabelElement`, `testRenderPropWith: 'label'`, rendered inside `<Field.Root>`) | FLD-DOM-19 |
| "should set htmlFor referencing the control automatically" | FLD-API-11, FLD-DOM-06 |
| "when nativeLabel={false}, clicking focuses the associated control" | FLD-API-11, FLD-DOM-07, FLD-PTR-03 |
| "control selection" › "keeps the selected control id when another control unmounts" / "falls over to the remaining control when the selected one unmounts" | FLD-STR-03, FLD-DOM-06 |
| "reflects the disabled state from Field.Item" | FLD-STA-02, FLD-DOM-04 |
| "dev warnings" › "does not warn by default" / "does not warn when the render function returns no element" / "errors if nativeLabel=true but ref is not a label" / "errors if nativeLabel=false but ref is a label" | FLD-EDGE-02 |

### `FieldDescription.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLParagraphElement`, rendered inside `<Field.Root>`) | FLD-DOM-19 |
| "should set aria-describedby on the control automatically" | FLD-API-24, FLD-DOM-13, FLD-ARIA-02, FLD-ARIA-04 |
| "should preserve user aria-describedby values on the control" | FLD-ARIA-02 |
| "does not register an empty description id" | FLD-API-24, FLD-ARIA-02, FLD-EDGE-03 |
| "reflects the disabled state from Field.Item" | FLD-STA-04, FLD-DOM-04 |

### `FieldError.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; element `<Field.Error match />` rendered inside `<Field.Root invalid>`) | FLD-DOM-19 |
| "should set aria-describedby on the control automatically" | FLD-API-31, FLD-ARIA-02, FLD-ARIA-05 |
| "should show error messages by default" | FLD-API-30, FLD-API-32, FLD-DOM-14, FLD-FRM-05 |
| "prop: match" › "should only render when `match` matches constraint validation" | FLD-API-30, FLD-FRM-13 |
| "prop: match" › "should show custom errors" | FLD-API-30, FLD-FRM-07, FLD-EDGE-07 |
| "prop: match" › "uses `match={false}` as the default slot for Form errors" / "uses an omitted `match` as the default slot for Form errors" | FLD-API-30, FLD-DOM-15 |
| "prop: match" › "uses the Field.Control name fallback for Form errors" | FLD-API-01, FLD-API-18, FLD-DOM-15, FLD-ARIA-03, FLD-FRM-09 |
| "prop: match" › "ignores inherited Form error properties" | FLD-STA-09, FLD-FRM-09 |
| "prop: match" › "renders Form error arrays as a list" / "renders single-item Form error arrays as text" / "renders client validation error arrays as a list" | FLD-DOM-16 |
| "prop: match" › "does not register an empty error id" | FLD-API-31, FLD-EDGE-03 |
| "prop: match" › "ignores empty Form error arrays" | FLD-API-30, FLD-FRM-09 |
| "prop: match" › "uses `match={false}` as the default slot for client validation errors" | FLD-API-30 |
| "prop: match" › "uses the client validation path for specific matches when Form errors are present" | FLD-API-30, FLD-DOM-15, FLD-EDGE-07 |
| "prop: match" › "always renders the error message when `match` is true" | FLD-API-30, FLD-ANI-05 |
| "animations" › "triggers enter animation via data-starting-style when mounting" | FLD-API-34, FLD-DOM-18, FLD-ANI-02 |
| "animations" › "applies data-ending-style before unmount" | FLD-STA-06, FLD-DOM-14, FLD-DOM-18, FLD-ANI-03 |

### `FieldItem.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Field.Root>`) | FLD-DOM-19 |
| "prop: disabled" › "reflects disabled state on the item" | FLD-STR-06, FLD-API-27, FLD-API-28, FLD-STA-05 |
| "prop: disabled" › "disables a wrapped checkbox" / "disables a wrapped radio" | FLD-API-27 |
| "associates a Field.Item label with a parent checkbox" | FLD-STR-04, FLD-DOM-06, FLD-PTR-02 |

### `FieldValidity.test.tsx`

| Test | Requirements |
|---|---|
| "surfaces valueMissing immediately after a stale custom error in $validationMode mode" (onBlur / onSubmit) | FLD-STA-07, FLD-FRM-13 |
| "defers badInput during required change revalidation" | FLD-FRM-13 |
| "validationMode=onSubmit" › "should pass validity data" | FLD-API-35, FLD-STA-01, FLD-STA-07, FLD-FRM-03 |
| "validationMode=onBlur" › "should pass validity data" | FLD-API-35, FLD-STA-07, FLD-FRM-03 |
| "validationMode=onBlur" › "should correctly pass errors when validate function returns a string" / "should correctly pass errors when validate function returns an array of strings" | FLD-API-04, FLD-STA-07 |

### Additional requirements introduced here

- **FLD-DOM-19** — MUST. `Field.Root`, `Field.Label`, `Field.Control`, `Field.Description`, `Field.Item` and `Field.Error` MUST pass the shared conformance suite of RND §4.9 (RND-DOM-36…RND-DOM-48): custom attributes forwarded to the default element, to a substitute callback's attribute set and to a substitute element; `style` from the part and from the substitute both applied; the DOM handle resolves to `HTMLDivElement` (Root, Item, Error), `HTMLLabelElement` (Label), `HTMLInputElement` (Control), `HTMLParagraphElement` (Description); wrapping allowed; part and substitute handles both receive the element; string and function `className` merge with the substitute's class. Label is substituted with a `label` (`testRenderPropWith: 'label'`); the others with the default `div`. `Field.Error` is tested with `match` set and the field `invalid` so that it is mounted. `Field.Validity` is not part of the suite (no element).
  Source: `test/describeConformance.tsx:26-49`, `test/conformanceTests/propForwarding.tsx:23-101`, `test/conformanceTests/renderProp.tsx:40-160`, `test/conformanceTests/refForwarding.tsx:14`, `test/conformanceTests/className.tsx:21`. Test: [L2] `FieldRoot.test.tsx` / `FieldLabel.test.tsx` / `FieldControl.test.tsx` / `FieldDescription.test.tsx` / `FieldItem.test.tsx` / `FieldError.test.tsx` "Base UI component API".
- **FLD-FRM-16** — MUST. When the subtree containing the control is hidden but kept in the DOM (a mode in which its registrations are torn down while its elements persist), the labelable scope MUST keep the last selected control id — including an explicit id and a `null` claim — so `Field.Label`'s `for` (or its absence) and the control's `aria-labelledby` stay valid while hidden and after re-showing, without churn or repeated updates (FFI-FRM-35).
  Source: `internals/labelable-provider/LabelableProvider.tsx:36-41,62-66`. Test: [L2] `FieldRoot.test.tsx` "preserves label association without looping when a control is unmounted and remounted", "preserves a non-native label association when a control is unmounted and remounted", "keeps an explicit control id while the subtree is hidden", "keeps the group label suppressed while its subtree is hidden".
- **FLD-EDGE-13** — SHOULD. A component-owned `Field.Control` SHOULD NOT be rendered again on keystrokes after the first (the first keystroke flips `dirty`/`filled`; later ones change no field state), and a consumer-owned one SHOULD be rendered exactly once per keystroke (the consumer's value update is committed in the same render batch and does not schedule a second render of the part); render count observed through bUnit (L2) (FFI-EDGE-09).
  Source: `field/control/FieldControl.tsx:123-143`. Test: [L2] `FieldControl.test.tsx` "avoids rerendering for uncontrolled input changes", "renders once per keystroke for controlled input changes".

## 14. React-specific notes and porting considerations

Non-normative.

- **Scopes are React contexts.** The field scope (`FieldRootContext`), the labelable scope (`LabelableContext`, one from `Field.Root` and one per `Field.Item`), the item scope (`FieldItemContext`) and the form scope cross portal boundaries; a port needs an ancestor lookup over the *logical* tree. Parts that require the field scope detect its absence by comparing the scope's `setValidityData` against a shared no-op (`FieldRootContext.ts:74`), not by a flag. Port: see decisions/04-js-interop-boundary.md §1 (field and form infrastructure, including these scopes, is C#).
- **Layout-effect timing.** Control registration, the `filled` recomputation, the consumer-owned value-change reaction (`useValueChanged`), description/error/label id publication, label-id withdrawal and the focus-ownership reclaim all run in `useIsoLayoutEffect`: synchronously after the DOM update, before paint, child-before-parent, cleanup-before-setup on replacement. The `Field.Label` mismatch report is a passive effect (after paint) and only in development. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 and §Relaxation table ("FFI registration" row: after paint plus a round trip on Server).
- **`useValueChanged` identity semantics.** `Field.Control`'s consumer-owned reaction fires only when `String(value)` is not identical to the previous render's; a re-render with the same string does nothing, and the first render never fires. A port should compare the serialized value against the last *rendered* one, not against the DOM. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (value-changed side effects run in `OnAfterRender`).
- **Ownership is per render.** `isControlled = value !== undefined` is evaluated on every render and selects `value` vs `defaultValue` on the element; React itself warns when an input switches between the two. There is no `useControlled` here, so no "controlled → uncontrolled" dev warning comes from Base UI (contrast with `Switch`/`Checkbox`). Port: see decisions/05-state-store-and-event-details.md §2, which latches ownership at the first parameter set and so conflicts with FLD-STA-12 (§16 item 13).
- **`onChange` quirk (facebook/react#9023).** The `event.nativeEvent.defaultPrevented` check exists because React fires `onChange` even for a prevented `input` event; the requirement is FLD-EVT-05. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 (`preventDefault` is JavaScript policy, not on the C# surface) and decisions/04-js-interop-boundary.md §3 (`details.event` snapshot).
- **Enter and implicit submission.** The 0 ms `useTimeout` (cleared on unmount) lets the browser's implicit submission — which fires `submit` after keydown — run first; the form's submission counter is the tie-breaker (FLD-KEY-01). Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (macrotask / N ms: a C# timer when the result is state).
- **Ids.** `useBaseUiId` prefixes `React.useId()` with `base-ui-`; an explicit id (including `''`) is returned verbatim (`??`, so `''` is kept). On React 17 `useId` returns `undefined` on the first render and assigns `base-ui-N` in an effect (`FieldRoot.react17.test.tsx`); `useLabelableId` treats `undefined` as "not yet available" and `null` as a deliberate claim. The control renders the *scope's* id (not its own `id` prop) until its registration effect has run, which is why server markup and the first client render show a generated id even with an explicit prop (FLD-DOM-08, FLD-EDGE-09). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 (scoped `base-ui-<n>` counter; prerender/interactive stability per RND-DOM-29).
- **Stable callbacks.** `validate`, `setDirty`, `setTouched`, `shouldValidateOnChange`, `commit`, `change`, `register` and `setFieldFocused` are `useStableCallback` wrappers: the latest closure runs, the identity never changes (and doubles as the focus-owner token). `actionsRef.current` is created once by `useImperativeHandle` with a stable dependency. Port: see decisions/04-js-interop-boundary.md §1 (the validation pipeline, including asynchronous `validate`, and focus ownership are C#) and decisions/06-testing-harness.md §1 (focus/blur are C#-dispatched events, so `data-focused` is observable at L2).
- **`Field.Error` exit content.** `lastRenderedMessage`/`lastRenderedMessageKey` are React state written *during render* (`FieldError.tsx:98-101`), a React-sanctioned way to derive state; a port keeps "last message shown while rendered" as plain state updated whenever the part renders while `rendered` is true.
- **`enabled: mounted`.** `useRenderElement` is always called (hook-order stability) with `enabled: mounted`; when not mounted it computes nothing and the component returns `null` (RND-DOM-25). A port simply renders nothing. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 4 (`Enabled = false` renders nothing).
- **Transition machine.** `useTransitionStatus` performs synchronous "set state during render" transitions (`starting`/`ending` in the same commit) and clears `starting`/`ending` with a single `requestAnimationFrame`; `useOpenChangeComplete` arms `useAnimationsFinished` in a passive effect on every change of `rendered` and aborts the previous wait. `Field.Validity` uses the machine without the completion watcher, which is why its status can stay `'ending'` (FLD-ANI-06). Port: see decisions/04-js-interop-boundary.md §2 (`data-starting-style`/`data-ending-style` flips and the animations-finished watcher are JS-owned, mirrored into C# state) and decisions/01-hosting-model-and-timing.md §Relaxation table (TRN-ANI-03 and TRN-STA-04/09 rows).
- **Memoized render-prop argument.** `Field.Validity` memoizes the combined validity data on `[validityData, invalid]` and the argument on `[combined, transitionStatus]`; because every publish creates a new `validityData` object, identity changes on each publish even when values are equal (FLD-STA-08). Port: see decisions/05-state-store-and-event-details.md §1 (`Is<T>` reference equality for record-typed values) and §2.
- **Dev error channel.** `error()` from `@base-ui/utils/error` logs through `console.error` with the `Base UI: ` prefix; `SafeReact.captureOwnerStack` appends an owner stack on React ≥ 19 and is absent on React 17 (`FieldRoot.react17.test.tsx` mocks it away). The effect dependency `[nativeLabel]` means a change of the substituted element alone (same `nativeLabel`) does not re-report.
- **StrictMode.** Registration and id claims use per-instance `Symbol()` sources so a re-run re-registers the same control in place; the dirty baseline is captured once per `Field.Root` (`FieldRoot.test.tsx` "captures the baseline only once in StrictMode"); focus ownership is re-acquired on effect setup (`useSetFieldFocused.ts:36-43`). Several label/id tests run without StrictMode because StrictMode's effect replay masks id hand-off bugs (`FieldRoot.test.tsx:35-37`).
- **React 19 `Activity`.** The hidden-subtree tests (`FLD-FRM-16`) use `React.Activity`, which destroys effects (unregistering the control) while keeping the DOM; the labelable scope's "keep the selection while no registrations remain" rule exists for this.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| FLD-STR-01 | MUST | Root renders div; provides field + labelable scope to all descendants | 2 |
| FLD-STR-02 | MUST | Label/Description/Error/Validity/Item throw outside Root; Control does not | 2 |
| FLD-STR-03 | MUST | Control optional; one active control; label keeps first registered id | 2 |
| FLD-STR-04 | MUST | Item opens nested labelable scope | 2 |
| FLD-STR-05 | MUST | Validity renders no element | 2 |
| FLD-STR-06 | MUST | Item is structural only | 2 |
| FLD-API-01 | MUST | Root `name`: effective name = prop ?? control name; not rendered | 3.1 |
| FLD-API-02 | MUST | Root `disabled` default false; OR with fieldset; propagates | 3.1 |
| FLD-API-03 | MUST | `invalid` only when `true`; blocks submit; survives disabled | 3.1 |
| FLD-API-04 | MUST | `validate` default `() => null`; call shape; result normalization | 3.1 |
| FLD-API-05 | MUST | `validationMode` values; inherits Form, else onSubmit | 3.1 |
| FLD-API-06 | MUST | `validationDebounceTime` default 0; delays change commits only | 3.1 |
| FLD-API-07 | MUST | `dirty`/`touched` props control the flags; no default pair | 3.1 |
| FLD-API-08 | MUST | `actionsRef.current = { validate }`, stable identity | 3.1 |
| FLD-API-09 | MUST | Root `className`/`style`/`render` per RND | 3.1 |
| FLD-API-10 | MUST | Root other props forwarded; consumed list | 3.1 |
| FLD-API-11 | MUST | Label `nativeLabel` default true; selects for/click mode | 3.2 |
| FLD-API-12 | MUST | Label id: prop or generated; published to scope | 3.2 |
| FLD-API-13 | MUST | Label `className`/`style`/`render` per RND | 3.2 |
| FLD-API-14 | MUST | Label other props forwarded; `htmlFor` override; handler chaining | 3.2 |
| FLD-API-15 | MUST | Control `value`: consumer-owned binding; `String(value)` used by field | 3.3 |
| FLD-API-16 | MUST | Control `defaultValue`: initial value; later changes forwarded, no warning | 3.3 |
| FLD-API-17 | MUST | Control `onValueChange(value, details)` signature | 3.3 |
| FLD-API-18 | MUST | Control `name`: `fieldName ?? name`; name fallback for the field | 3.3 |
| FLD-API-19 | MUST | Control `disabled` default false; OR with field; native attr; unregisters | 3.3 |
| FLD-API-20 | MUST | Control `id`: resolved scope control id; explicit after registration; fallback | 3.3 |
| FLD-API-21 | MUST | Control `autoFocus` default false → `autofocus` attr | 3.3 |
| FLD-API-22 | MUST | Control `className`/`style`/`render` per RND; substitute keeps wiring | 3.3 |
| FLD-API-23 | MUST | Control other props forwarded; native handlers chain; `onChange` accepted | 3.3 |
| FLD-API-24 | MUST | Description id: prop or generated; published while mounted; `''` not published | 3.4 |
| FLD-API-25 | MUST | Description `className`/`style`/`render` per RND | 3.4 |
| FLD-API-26 | MUST | Description other props forwarded | 3.4 |
| FLD-API-27 | MUST | Item `disabled` default false; OR with root; published to parts/control | 3.5 |
| FLD-API-28 | MUST | Item `className`/`style`/`render` per RND | 3.5 |
| FLD-API-29 | MUST | Item other props forwarded | 3.5 |
| FLD-API-30 | MUST | Error `match` rendering rule (FFI-ALG-15) | 3.6 |
| FLD-API-31 | MUST | Error id: prop or generated; published only while rendered | 3.6 |
| FLD-API-32 | MUST | Error `children` replaces automatic message | 3.6 |
| FLD-API-33 | MUST | Error `className`/`style`/`render` per RND | 3.6 |
| FLD-API-34 | MUST | Error other props forwarded | 3.6 |
| FLD-API-35 | MUST | Validity `children` function required; invoked every render | 3.7 |
| FLD-STA-01 | MUST | Root state has exactly six fields | 3.1 |
| FLD-STA-02 | MUST | Label state: root state, disabled = root OR item | 3.2 |
| FLD-STA-03 | MUST | Control state: root state, disabled = field OR prop | 3.3 |
| FLD-STA-04 | MUST | Description state: root state, disabled = root OR item | 3.4 |
| FLD-STA-05 | MUST | Item state: root state, disabled = root OR prop | 3.5 |
| FLD-STA-06 | MUST | Error state = root state + `transitionStatus` (never idle) | 3.6 |
| FLD-STA-07 | MUST | Validity argument shape (six fields, `validity` combined) | 3.7 |
| FLD-STA-08 | MUST | Validity argument identity stability rules | 3.7 |
| FLD-STA-09 | MUST | `valid = !invalid && (disabled ? null : computed)` | 4 |
| FLD-STA-10 | MUST | `dirty`/`touched` = prop ?? internal; latch synced to prop | 4 |
| FLD-STA-11 | MUST | `filled`/`focused` start false; written only by controls | 4 |
| FLD-STA-12 | MUST | Control ownership decided per render | 4 |
| FLD-STA-13 | MUST | Control `filled` predicate and timing | 4 |
| FLD-STA-14 | MUST | Control `dirty` predicate and timing | 4 |
| FLD-DOM-01 | MUST | Root div; no id/role/aria/name of its own | 5.1 |
| FLD-DOM-02 | MUST | Root five boolean state attributes | 5.1 |
| FLD-DOM-03 | MUST | Root `data-valid`/`data-invalid`/neither | 5.1 |
| FLD-DOM-04 | MUST | Other parts carry the same seven attributes, same commit | 5.1 |
| FLD-DOM-05 | MUST | Label `id` attribute | 5.2 |
| FLD-DOM-06 | MUST | Native label `for` = resolved control id; none for `null` claim | 5.2 |
| FLD-DOM-07 | MUST | Non-native label has no `for` | 5.2 |
| FLD-DOM-08 | MUST | Control `id` = resolved id; scope default before registration | 5.3 |
| FLD-DOM-09 | MUST | Control `name` attribute = effective name | 5.3 |
| FLD-DOM-10 | MUST | Control native `disabled` attribute | 5.3 |
| FLD-DOM-11 | MUST | Control value/defaultValue binding; no rewrite of live value | 5.3 |
| FLD-DOM-12 | MUST | Control `autofocus` attribute | 5.3 |
| FLD-DOM-13 | MUST | Description `id` attribute | 5.4 |
| FLD-DOM-14 | MUST | Error element exists iff mounted; `id` attribute | 5.6 |
| FLD-DOM-15 | MUST | Error automatic message selection | 5.6 |
| FLD-DOM-16 | MUST | Error array → `<ul><li>`; single-entry array → text | 5.6 |
| FLD-DOM-17 | MUST | Error exit content = last rendered message | 5.6 |
| FLD-DOM-18 | MUST | Error `data-starting-style`/`data-ending-style` | 5.6 |
| FLD-DOM-19 | MUST | Six element parts pass the RND conformance suite | 13 |
| FLD-ARIA-01 | MUST | Control `aria-labelledby` = scope label id | 5.8 |
| FLD-ARIA-02 | MUST | Control `aria-describedby` composition | 5.8 |
| FLD-ARIA-03 | MUST | Control `aria-invalid="true"` while invalid and enabled | 5.8 |
| FLD-ARIA-04 | MUST | Description publishes id while mounted | 5.8 |
| FLD-ARIA-05 | MUST | Error publishes id only while rendered; withdrawn before exit ends | 5.8 |
| FLD-ARIA-06 | MUST | Label publishes id; withdraws only its own | 5.8 |
| FLD-ARIA-07 | MUST | No roles/live regions/other aria on any part | 5.8 |
| FLD-KEY-01 | MUST | Enter in Control input: touched + (deferred 0 ms / immediate) commit | 6.1 |
| FLD-KEY-02 | MUST | Enter on non-input element not intercepted | 6.1 |
| FLD-KEY-03 | MUST | No other key intercepted; Tab native | 6.1 |
| FLD-KEY-04 | MUST | Consumer `onKeyDown` first; preventBaseUIHandler / preventDefault effects | 6.1 |
| FLD-PTR-01 | MUST | Native label: double-click `preventDefault`; native activation | 6.2 |
| FLD-PTR-02 | MUST | Label ignores events from nested interactive descendants | 6.2 |
| FLD-PTR-03 | MUST | Non-native label: pointerdown prevented; click focuses control (focusVisible) | 6.2 |
| FLD-PTR-04 | MUST | Consumer label handlers run first; preventBaseUIHandler | 6.2 |
| FLD-FOC-01 | MUST | Control focus acquires `focused` unless disabled | 6.3 |
| FLD-FOC-02 | MUST | Control blur: touched → release focused → onBlur commit | 6.3 |
| FLD-FOC-03 | MUST | `focused` released on disable/unmount of the owner only | 6.3 |
| FLD-FOC-04 | MUST | Active element at mount/enable acquires `focused` without event | 6.3 |
| FLD-FOC-05 | MUST | No focus moves except label click and blocked-submit focus | 6.3 |
| FLD-FOC-06 | MUST | Blocked-submit focus selects text only for an `INPUT` element | 6.3 |
| FLD-FOC-07 | MUST | No Field part sets or changes `tabindex` | 6.3 |
| FLD-EVT-01 | MUST | `onValueChange(value, details)`; reason none; native event; no trigger | 7 |
| FLD-EVT-02 | MUST | `onValueChange` once per change, before field writes | 7 |
| FLD-EVT-03 | MUST | `onValueChange` not fired for props/mount/programmatic | 7 |
| FLD-EVT-04 | MUST | `cancel()` (uncontrolled): keeps value, dirty/filled still written, no clearErrors/validation | 7 |
| FLD-EVT-05 | MUST | Default-prevented native event: callback fires; no clearErrors/validation | 7 |
| FLD-EVT-06 | MUST | Controlled reactions driven by `String(value)` identity, post-commit | 7 |
| FLD-EVT-07 | MUST | Uncontrolled reaction order inside the change event | 7 |
| FLD-EVT-08 | MUST | `validate(value, formValues)` at most once per commit; latest function | 7 |
| FLD-EVT-09 | MUST | Consumer native handlers on Control run first; preventBaseUIHandler | 7 |
| FLD-FRM-01 | MUST | Control registration parameters and timing | 8 |
| FLD-FRM-02 | MUST | Control FormData via element; live string projection | 8 |
| FLD-FRM-03 | MUST | Validation triggers per mode (table) | 8 |
| FLD-FRM-04 | MUST | Debounce semantics and cancellation | 8 |
| FLD-FRM-05 | MUST | Native constraints; `valueMissing` suppressed until dirty latch | 8 |
| FLD-FRM-06 | MUST | Custom validity install/clear/restore; consumer message honored | 8 |
| FLD-FRM-07 | MUST | `validate` after native pass or in change-validation | 8 |
| FLD-FRM-08 | MUST | Asynchronous validator behavior (FFI-FRM-11..14) | 8 |
| FLD-FRM-09 | MUST | `Form.errors` and `invalid` make the field invalid; cleared on change | 8 |
| FLD-FRM-10 | MUST | Form reset not observed | 8 |
| FLD-FRM-11 | MUST | Disabled Control unregistered; aria-invalid off; neutral validity | 8 |
| FLD-FRM-12 | MUST | Logical field without control still validates | 8 |
| FLD-FRM-13 | MUST | Revalidate pass observable per keystroke | 8 |
| FLD-FRM-14 | MUST | Control replacement cancels pending validation; keeps baseline | 8 |
| FLD-FRM-15 | MUST | Runtime name changes re-key errors/values | 8 |
| FLD-FRM-16 | MUST | Hidden subtree keeps label association | 13 |
| FLD-FRM-17 | MUST | Consumer-owned `'onBlur'` microtask re-commit of a normalized value | 8 |
| FLD-FRM-18 | MUST | Field-less `Field.Control` inside a `Form` still calls `clearErrors(name)` | 8 |
| FLD-ANI-01 | MUST | Error mounted from rendered-commit until exit completes | 9 |
| FLD-ANI-02 | MUST | Error enter: `data-starting-style` same commit, cleared next frame | 9 |
| FLD-ANI-03 | MUST | Error exit: `data-ending-style` same commit; unmount on animations finished | 9 |
| FLD-ANI-04 | MUST | Re-render during exit: no restart; completion aborted | 9 |
| FLD-ANI-05 | MUST | Rendered at first render: no starting style | 9 |
| FLD-ANI-06 | MUST | Validity `transitionStatus` machine (stays `'ending'`) | 9 |
| FLD-ANI-07 | MUST | Other parts always mounted; no transition attributes | 9 |
| FLD-EDGE-01 | MUST | Missing-root error (FFI-EDGE-01) | 12 |
| FLD-EDGE-02 | MUST | `nativeLabel` mismatch dev errors (FFI-EDGE-02) | 12 |
| FLD-EDGE-03 | MUST | `id=""` rendered verbatim, not published | 12 |
| FLD-EDGE-04 | MUST | Error ignores `Field.Item` disabled | 12 |
| FLD-EDGE-05 | SHOULD | Label `id` prop change after mount ignored | 12 |
| FLD-EDGE-06 | SHOULD | Multiple labels share one id | 12 |
| FLD-EDGE-07 | MUST | `match="valid"`/string matches ignore form errors | 12 |
| FLD-EDGE-08 | MUST | No RTL/locale dependence | 12 |
| FLD-EDGE-09 | MUST | SSR ids and association | 12 |
| FLD-EDGE-10 | MUST | `value={null}` and numeric `value` handling | 12 |
| FLD-EDGE-11 | MUST | Nested Field.Root scopes | 12 |
| FLD-EDGE-12 | MUST | Programmatic DOM value not observed; no rewrite on focus | 12 |
| FLD-EDGE-13 | SHOULD | Render counts per keystroke | 13 |

## 16. Open questions

1. **`Field.Validity` `transitionStatus` never leaves `'ending'`.** `field/validity/FieldValidity.tsx:24-25` runs the transition machine on "is invalid" but nothing ever sets the machine's mounted flag back to `false`, so after the first invalid→valid change `transitionStatus` stays `'ending'` for as long as the field is valid, and the next invalid phase skips `'starting'` (FLD-ANI-06). Only the property's presence is tested (`FieldValidity.test.tsx` "should pass validity data"). Confirm whether a port should reproduce this or reset to unset once the exit would be complete.
    Open 2026-10-22: `Field.Validity` arrives with PR 1c of M2 track 1, which resolves this item or files it as an issue.
2. **`Field.Label` id handling.** `field/label/FieldLabel.tsx:44` passes `labelId ?? idProp` to the label mechanism: once a label id is in the scope, the `id` prop is ignored (FLD-EDGE-05) and a second `Field.Label` in the same scope renders the first one's id (FLD-EDGE-06). Neither is tested; both look unintended. Decide whether the port follows the source (SHOULD) or honors `id` changes and keeps distinct ids.
    Resolved 2026-10-22 (design point DP8 of M2 track 1, settled by the orchestrator 2026-10-22): the port honours the `id` parameter consistently. Each label renders and publishes its own id, and the last publication names the scope; see the port notes on FLD-EDGE-05/06.
3. **`match="valid"`.** `field/error/FieldError.tsx:52,153` accepts every `keyof ValidityState`, so `match="valid"` renders the error while the field is valid (FLD-EDGE-07). Not tested, not documented; probably an artifact of the type.
    Open 2026-10-22: `Field.Error` arrives with PR 1c, which resolves this item or files it as an issue.
4. **`cancel()` on an uncontrolled `Field.Control`.** `field/control/FieldControl.tsx:134-142` writes `dirty`/`filled` before checking `details.isCanceled`, so a cancelled change still flips `data-dirty`/`data-filled` while skipping validation (FLD-EVT-04). EVD-EVT-08 says a cancelled change suppresses field `dirty`/`filled` updates; `Field.Control` deviates because the native element already holds the value. Only the "no validation" half is tested. Resolved (cross-document half): EVD-EVT-08 now states the `Field.Control` carve-out explicitly (only `clearErrors` and validation are suppressed for an uncontrolled `Field.Control`), so FLD-EVT-04 and EVD agree; whether the dirty/filled write is intended remains open.
    Resolved 2026-10-22 (ADR 05 Amendments 2026-10-18 item 2): the port follows the source. An uncontrolled edit commits `dirty` and `filled` despite `Cancel()`, since the browser already changed the text, and skips clearErrors and validation; tested by "writes dirty and filled for a canceled uncontrolled change" over both parts.
5. **`Field.Error` ignores `Field.Item` disabled.** `field/error/FieldError.tsx:37,49` reads the field's disabled flag only, whereas `Field.Label`/`Field.Description` were changed to reflect the item (CHANGELOG v1.4.0 "Reflect disabled `Field.Item` state in `Field.Label`", "… in `Field.Description`"). An error placed inside a disabled item therefore still renders and carries no `data-disabled` (FLD-EDGE-04). Possibly an oversight.
    Open 2026-10-22: `Field.Error` arrives with PR 1c.
6. **`value={null}`.** `field/control/FieldControl.tsx:74-76,122` treats `null` as consumer-owned (`isControlled` is `true`) but serializes it to `undefined`, and passes `value={null}` to the element (React warns and treats the input as uncontrolled). FLD-EDGE-10 states the field-side behavior; the element-side behavior is framework-defined.
    Resolved 2026-10-22: `Value` is `string?` (ADR 08 Amendments 2026-10-22). A controlled `null` is FLD-EDGE-10's `undefined`; the module does not re-assert it, so the element keeps what it shows, as React does with `value={null}`.
7. **Explicit control `id` absent from server markup.** `internals/labelable-provider/useLabelableId.ts:79-82` renders the scope's generated id until registration runs, so an explicit `id` prop on `Field.Control` does not appear in SSR output or on the first client render (FLD-DOM-08). Intentional (keeps `for`/`id` paired), but a port with synchronous registration could render the explicit id immediately as long as the label's `for` matches in the same output.
    Resolved 2026-10-22 (ADR 01 Amendments 2026-10-22): the control-id claim registers the control, so it is not "a part that only publishes an id" and stays post-render. An explicit `id` therefore appears after the first interactive render, in the same batch as the label's `for` (FLD-DOM-08 as written).
8. **Exit-phase content untested.** FLD-DOM-17 (`lastRenderedMessage`, `field/error/FieldError.tsx:71-73,96-101,124`) is derived from the source only; `FieldError.test.tsx` "applies data-ending-style before unmount" uses consumer `children`.
    Open 2026-10-22: `Field.Error` arrives with PR 1c.
9. **Enter with a default-prevented keydown.** `field/control/FieldControl.tsx:177` commits immediately when the keydown was default-prevented (e.g. by a consumer `onKeyDown`), on the theory that implicit submission cannot happen; a consumer that prevents Enter to run its own submit logic therefore gets an extra synchronous validation. Untested; FLD-KEY-01/FLD-KEY-04 document the behavior as implemented.
    Resolved 2026-10-22: the port follows the source (FLD-KEY-01, FLD-KEY-04). A keydown a consumer JavaScript listener prevented commits at once; a C# handler cannot call `preventDefault()`, and its `PreventPartHandler()` suppresses the commit entirely.
10. **Dev error re-reporting.** `field/label/FieldLabel.tsx:50-75` re-evaluates the mismatch only when `nativeLabel` changes; swapping the substituted element from `<label>` to `<div>` at runtime without changing `nativeLabel` is not reported. Noted in §14; not a requirement.
    Resolved 2026-10-22: the port follows the source. The module checks the element at attach and when `NativeLabel` changes (FLD-EDGE-02).
11. **API outline discrepancies** (`docs/src/app/(docs)/react/components/page.mdx` "## Field"): the outline omits the native props the parts treat specially — `Field.Root` none; `Field.Label` `id`; `Field.Control` `id`, `name`, `disabled`, `autoFocus`, `value` (only `defaultValue`/`onValueChange` are listed); `Field.Description` `id`; `Field.Error` `id`, `children`. It lists no data attributes for `Field.Validity` (correct: none). `FieldRootDataAttributes.ts:14-20` documents `data-valid`/`data-invalid` as "field is valid/invalid" whereas the other parts say "in a valid/invalid state"; same semantics. The orchestrator's hints `validateOnChange`, `forceShow` and an array form of `match` do not exist in v1.9.0 (`forceShow` was folded into `match` in v1.0.0-beta.0).
    Resolved 2026-10-22: the port's XML documentation lists every parameter, including the typed `Name`, `Disabled` and `AutoFocus` of the controls (ADR 08 Amendments 2026-10-22).
12. **Handler-chaining tests.** FLD-API-14, FLD-PTR-04 and the `preventBaseUIHandler()` half of FLD-EVT-09/FLD-KEY-04 are specified from `mergeProps` (RND) and have no Field-level test.
    Resolved 2026-10-22: L2 port titles cover FLD-API-14, FLD-PTR-04, FLD-EVT-09 and FLD-KEY-04: "runs the consumer's label pointer handler inside the decision, and Blazor's dispatch of the same event does not run it again" and "runs the consumer's input, blur and keydown handlers before the control's own, which a veto skips".
13. **Ownership latch vs. ADR 05.** `field/control/FieldControl.tsx:74,122` decides consumer- vs component-owned on every render (FLD-STA-12; INP §16 item 3). `decisions/05-state-store-and-event-details.md` §2 makes a port component controlled iff `Value` was supplied at the first parameter set, fixed for its lifetime. Applied to `Field.Control`/`Input` that would latch ownership and contradict the "switching between the two between renders is permitted" clause of FLD-STA-12 (untested). Decide whether `Field.Control` is an exception to ADR 05 §2 or FLD-STA-12's switching clause is withdrawn for the port. Decided (2026-10-18): ADR 05 §2 applies uniformly; the switching clause is relaxed for the port (ADR 05 Amendments item 1).
