# Field and form infrastructure

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/internals/field-root-context/`, `internals/field-register-control/`, `internals/form-context/`, `internals/labelable-provider/`, `internals/field-constants/`, `internals/serializeValue.ts`, `field/root/FieldRoot.tsx`, `field/root/useFieldValidation.ts`, `field/utils/getCombinedFieldValidityData.ts`, `form/Form.tsx`, `utils/useRegisteredLabelId.ts`, `utils/resolveAriaLabelledBy.ts`, `packages/utils/src/getDefaultFormSubmitter.ts` |
| Tests | `field/root/FieldRoot.test.tsx`, `field/root/FieldRoot.react17.test.tsx`, `field/control/FieldControl.test.tsx`, `field/label/FieldLabel.test.tsx`, `field/description/FieldDescription.test.tsx`, `field/error/FieldError.test.tsx`, `field/validity/FieldValidity.test.tsx`, `field/item/FieldItem.test.tsx`, `form/Form.test.tsx`, `fieldset/root/FieldsetRoot.test.tsx`, `fieldset/legend/FieldsetLegend.test.tsx`, `internals/labelable-provider/useLabel.test.tsx`, `utils/useRegisteredLabelId.test.tsx`, `packages/utils/src/getDefaultFormSubmitter.test.ts`, `test/formData.ts` (helper), plus the `Form` / `Field` describe blocks inside every control's test file (cited inline in §6); the fieldset-only titles "sets the native disabled attribute" and "throws a descriptive error when rendered outside <Fieldset.Root>" are mapped in `components/fieldset.md` §13 |
| Prefix | `FFI` |
| Status | approved |
| Revised | 2026-10-22 |
| Used by | `components/field.md`, `components/fieldset.md`, `components/form.md`, `components/input.md`, `components/checkbox.md`, `components/checkbox-group.md`, `components/switch.md`, `components/radio.md`, `components/radio-group.md`, `components/select.md`, `components/combobox.md`, `components/autocomplete.md`, `components/slider.md`, `components/number-field.md`, `components/otp-field.md` |

## 1. Purpose

Base UI's form controls are not native `<input>` elements, yet they must behave as if they were: a `<Form>` must know which fields it owns, what their current values are, whether they are valid, and which one to focus when submission is blocked; a `<Field.Root>` must know whether "its" control is dirty, touched, filled, focused and valid even though the control may be a `<span role="checkbox">`, a `<button role="combobox">`, or a set of `<input type="range">` elements; a `<Field.Label>` must point at the right element; and a `<Field.Description>`/`<Field.Error>` must be announced by screen readers for whichever element actually receives focus.

This document specifies the contract between three ancestor-provided scopes and every form control:

1. the **form scope** created by `<Form>` (a registry of fields, external error map, submission pipeline);
2. the **field scope** created by `<Field.Root>` (state flags, validity data, the validation pipeline, control registration);
3. the **labelable scope** created by `<Field.Root>` and `<Field.Item>` (control id, label id, description/error ids).

It also specifies the validation pipeline (native constraint validation merged with a consumer `validate` callback, synchronous or asynchronous, in three validation modes), the serialization of control values into `FormData` and into the `onFormSubmit` values object, and the **control registration matrix** (§6) that says, for every control, which native input it renders and how it feeds each of the above. Component documents link to §6 instead of re-describing the mechanism.

A port cannot skip this: every assertion in the `Form` and `Field` describe blocks of every control's test suite depends on this mechanism behaving exactly as specified here, and the public `validationMode`, `validate`, `errors`, `onFormSubmit` and `actionsRef` contracts of `Field.Root` and `Form` are implemented entirely by it.

## 2. Concepts and terminology

| Base UI / React name | Neutral term used below | Meaning |
|---|---|---|
| `FormContext` / `useFormContext` | **form scope** | Shared state an enclosing `<Form>` provides to all descendants (including portaled ones): the field registry, the external error map, `clearErrors`, the resolved `validationMode`, the submission counter and a handle to the `<form>` element. Outside a `<Form>` a **default form scope** exists with an empty registry and no-op operations. |
| `formRef.current.fields` | **field registry** | An insertion-ordered map keyed by *control id* whose entries describe one logical field: `{ name, validate(), validityData, controlRef, getValue() }`. |
| `FieldRootContext` / `useFieldRootContext` | **field scope** | Shared state `<Field.Root>` provides to all descendants: the state flags, `validityData`, the setters (`setTouched`, `setDirty`, `setFilled`, `setFocused`), the resolved `validationMode`, `shouldValidateOnChange()`, `registerFieldControl`, and the **validation service**. Outside a `<Field.Root>` a **default field scope** exists whose setters are no-ops. |
| `validation` (`UseFieldValidationReturnValue`) | **validation service** | The field-scope object a control uses to participate in validation: `change(value)`, `commit(value)`, `getValidationProps(disabled, props)`, `registerInput(element, …)`, `registeredInputs`, `getInputControl()`, `inputRef`. |
| `useRegisterFieldControl` / `registerFieldControl` | **control registration** | The act by which a control declares itself the field's *active control*: its DOM handle, its control id, its current value, an optional `getValue()` projection for form submission, and its own `name`. |
| `FieldControlRegistration` | **registration record** | `{ controlRef, id, name?, getValue?, value }`. |
| `validation.inputRef` / `registeredInputs` | **validation input(s)** | The native `<input>` element(s) whose `ValidityState` and `validationMessage` are read by the pipeline. A field may have one (`inputRef`) or several (`registeredInputs`, used by checkbox and radio groups). |
| `findRepresentativeInput` | **representative input** | Among the eligible validation inputs, the first invalid one in registration order, otherwise the first eligible one. |
| `isEligibleInput` | **eligible input** | A validation input that is not `:disabled` and belongs to the surrounding form (see FFI-ALG-04). |
| `FieldValidityData` | **validity data** | `{ state, error, errors, value, initialValue }` — see FFI-STA-03. `state` mirrors the DOM `ValidityState` flags plus `valid: boolean | null`. |
| `valid: null` | **neutral validity** | "Not yet validated" or "unknown while an asynchronous validator is in flight". Produces neither `data-valid` nor `data-invalid`. |
| `markedDirtyRef` | **dirty latch** | A latch that becomes `true` the first time the field is marked dirty or imperatively validated and is used to suppress `valueMissing` noise on pristine fields. |
| `LabelableContext` / `LabelableProvider` | **labelable scope** | Shared state that associates a control with its label and descriptions: `controlId`, `labelId`, `messageIds`, `registerControlId`, `resetControlId`, `setLabelId`, `setMessageIds`, `getDescriptionProps`. |
| `useLabelableId` | **control-id claim** | How a control claims (or declines) ownership of the labelable scope's control id. Claiming `null` means "I am named by `aria-labelledby`, suppress `htmlFor`". |
| `useLabel` | **label behavior** | The attributes a label part emits: its id, `htmlFor` (native) or click-to-focus (non-native). |
| `useAriaLabelledBy` | **labelled-by resolution** | Explicit prop → field label id → discovered native label. |
| `getDescriptionProps` | **described-by composition** | Merging consumer `aria-describedby` with all message ids of the scope and its parent scope. |
| `getValidationProps` | **validation attributes** | Described-by composition plus `aria-invalid`. |
| `useSetFieldFocused` | **focus ownership** | The rule that only the control that last acquired the field's focused flag may release it. |
| `serializeValue` | **string serialization** | `null`/`undefined` → `""`, strings as-is, anything else `JSON.stringify` (falling back to `String`). |
| `stringifyAsValue` | **item stringification** | Select/Combobox value → string for submission (`{value,label}` objects use `value`, else `itemToStringValue`, else `String`). Defined in `internals/resolveValueLabel.tsx`; owned by `shared-utilities.md`. |
| `getDefaultFormSubmitter` | **default submitter** | The button a browser would click for implicit submission. |
| hidden input | **form-participation input** | A visually hidden native `<input>` a non-native control renders so that it takes part in `FormData`, constraint validation and autofill. |
| `Form.Values` | **values object** | `{ [fieldName]: getValue() }` built from the field registry, passed to `onFormSubmit` and as the second argument of `validate`. |
| `fieldValidityMapping` | **validity attribute mapping** | `valid: true → data-valid=""`, `false → data-invalid=""`, `null → nothing`. |

Timing vocabulary follows `conventions.md` §4: **post-commit, pre-paint** (synchronously after the DOM has been updated for a state change, before the browser paints), **after paint**, **microtask**, and **macrotask / N ms**. "Within the same update" means the state change is applied in the same batch as the triggering event, so the DOM reflects it before control returns to the event loop.

## 3. Public surface

Nothing in this slice is exported under its own name. The public props below are *declared* on `Field.Root`, `Form`, `Field.Label` and the individual controls (and appear in their component documents' props tables with JSDoc), but their **semantics are defined by this mechanism**; component documents cite the IDs in the last column instead of re-describing them.

### 3.1 Props whose semantics this mechanism defines

| Owner | Prop | Type | Default | Semantics |
|---|---|---|---|---|
| `Field.Root` | `validate` | `(value, formValues) => string \| string[] \| null \| void \| Promise<…>` | `() => null` | FFI-API-01, FFI-EVT-05, FFI-FRM-07, FFI-FRM-11..14 |
| `Field.Root` | `validationMode` | `'onSubmit' \| 'onBlur' \| 'onChange'` | inherited from `Form`, else `'onSubmit'` | FFI-API-02, FFI-FRM-01..05 |
| `Field.Root` | `validationDebounceTime` | `number` (ms) | `0` | FFI-API-03, FFI-FRM-02, FFI-FRM-15 |
| `Field.Root` | `invalid` | `boolean` | `undefined` | FFI-API-05, FFI-STA-02, FFI-FRM-27 |
| `Field.Root` | `dirty` | `boolean` | `undefined` (component-owned) | FFI-API-05, FFI-STA-05, FFI-STA-06 |
| `Field.Root` | `touched` | `boolean` | `undefined` (component-owned) | FFI-API-05, FFI-STA-07 |
| `Field.Root` | `name` | `string` | `undefined` (falls back to the control's name) | FFI-STA-10, FFI-API-07 |
| `Field.Root` | `disabled` | `boolean` | `false` | FFI-STR-06, FFI-STA-11, FFI-STA-02, FFI-FRM-17 |
| `Field.Root` | `actionsRef` | `{ current: { validate() } }` | — | FFI-API-06, FFI-FRM-21 |
| `Field.Label` | `nativeLabel` | `boolean` | `true` | FFI-ARIA-04, FFI-PTR-01, FFI-PTR-02, FFI-EDGE-02 |
| `Field.Error` | `match` | `boolean \| keyof ValidityState` | `undefined` | FFI-ALG-15, FFI-FRM-25 |
| `Form` | `validationMode` | `'onSubmit' \| 'onBlur' \| 'onChange'` | `'onSubmit'` | FFI-API-02 |
| `Form` | `errors` | `Record<string, string \| string[]>` | `{}` | FFI-API-04, FFI-EVT-03, FFI-EVT-04, FFI-FRM-25, FFI-FRM-26 |
| `Form` | `onFormSubmit` | `(values, eventDetails) => void` | — | FFI-API-07, FFI-EVT-01 |
| `Form` | `onSubmit` | native submit handler | — | FFI-EVT-01 (called only when validation passes) |
| `Form` | `actionsRef` | `{ current: { validate(fieldName?) } }` | — | FFI-API-06, FFI-FRM-20 |
| `Form` | `noValidate` | `boolean` | `true` | FFI-DOM-05 |
| every control | `name` | `string` | `undefined` | FFI-STA-10, FFI-FRM-28, §6 table C |
| every control with a hidden input | `form` | `string` (form element id) | `undefined` | FFI-DOM-08, FFI-FRM-23 |
| every control | `disabled` | `boolean` | `false` | FFI-STA-11, FFI-FRM-17, FFI-ARIA-03, §6 table C |
| every control with a native constraint | `required`, `readOnly` | `boolean` | `false` | FFI-FRM-06, FFI-FRM-08, §6 table A/C |
| Checkbox, Switch, Radio(Group), Select, Combobox, NumberField, Slider.Thumb | `inputRef` | DOM handle | — | handle to the form-participation input of §6 table A |
| Checkbox, Switch | `uncheckedValue` | `string` | `undefined` | FFI-DOM-07 |
| Checkbox, Switch | `value` | `string` | `undefined` (native `"on"`) | FFI-FRM-28 |
| Checkbox, Switch, Radio | `nativeButton` | `boolean` | `false` | FFI-DOM-04, FFI-ARIA-05 (fallback disabled) |

### 3.2 Types

The following exported **types** are defined by this mechanism and referenced by the component documents:

| Type | Shape | Source |
|---|---|---|
| `Field.ValidityData` (`FieldValidityData`) | `{ state: { badInput, customError, patternMismatch, rangeOverflow, rangeUnderflow, stepMismatch, tooLong, tooShort, typeMismatch, valueMissing: boolean; valid: boolean \| null }, error: string, errors: string[], value: unknown, initialValue: unknown }` | `field/root/FieldRoot.tsx:215-233`, exported `field/index.parts.ts:9` |
| `Field.Root.State` (`FieldRootState`) | `{ disabled, touched, dirty, filled, focused: boolean; valid: boolean \| null }` | `field/root/FieldRoot.tsx:239-264` |
| `Field.Root.Actions` | `{ validate(): void }` | `field/root/FieldRoot.tsx:235-237` |
| `Form.ValidationMode` | `'onSubmit' \| 'onBlur' \| 'onChange'` | `form/Form.tsx:181` |
| `Form.Values` | `Record<string, any>` (generic) | `form/Form.tsx:239` |
| `Form.Actions` | `{ validate(fieldName?: string): void }` | `form/Form.tsx:183-185` |
| `Form.SubmitEventReason` / `Form.SubmitEventDetails` | `'none'` / generic event details with `reason: 'none'` and the native submit event (see `event-details-and-reasons.md`) | `form/Form.tsx:178-179` |
| `Errors` (form context) | `Record<string, string \| string[]>` | `internals/form-context/FormContext.ts:7` |

## 4. Behavioral requirements

### 4.1 STR — scopes and their nesting

- **FFI-STR-01** — MUST. `<Field.Root>` provides the field scope to every descendant, including descendants rendered through a portal; a control rendered outside any `<Field.Root>` receives the default field scope, in which every setter and `registerFieldControl` is a no-op, `validation.change`/`commit` do nothing, `getValidationProps` returns its input unchanged, `validationMode` is `'onSubmit'`, `shouldValidateOnChange()` is `false`, `disabled`/`name`/`invalid` are `undefined` and `state` is the default state (FFI-STA-01).
  Source: `internals/field-root-context/FieldRootContext.ts:37-69`, `field/root/FieldRoot.tsx:153-195`. Test: [L2] `SelectRoot.test.tsx` "sets a hidden input id when name is not provided" (standalone control), `CheckboxGroup.test.tsx` "includes a group fully portaled outside the form element".
- **FFI-STR-02** — MUST. `Field.Label`, `Field.Description`, `Field.Error`, `Field.Validity` and `Field.Item` MUST throw when rendered outside a `<Field.Root>` with the message `Base UI: FieldRootContext is missing. Field parts must be placed within <Field.Root>.`; the check is "the scope's `setValidityData` is the no-op". `Field.Control` and every other form control MUST NOT throw (they use the optional lookup).
  Source: `internals/field-root-context/FieldRootContext.ts:71-81`; callers `field/label/FieldLabel.tsx:33`, `field/description/FieldDescription.tsx:27`, `field/error/FieldError.tsx:37`, `field/validity/FieldValidity.tsx:18`, `field/item/FieldItem.tsx:29`; optional caller `field/control/FieldControl.tsx:49-59`. Test: none.
- **FFI-STR-03** — MUST. `<Field.Root>` wraps its subtree in a labelable scope, and `<Field.Item>` opens a nested labelable scope. A control inside `<Field.Item>` claims the *item's* control id (so each item's `Field.Label` points at its own control), and its described-by composition includes the parent scope's message ids followed by the item scope's message ids.
  Source: `field/root/FieldRoot.tsx:204-213`, `field/item/FieldItem.tsx:43-47`, `internals/labelable-provider/LabelableProvider.tsx:24,68-81`. Test: [L2] `FieldItem.test.tsx` "associates a Field.Item label with a parent checkbox", `CheckboxGroup.test.tsx` "gives each checkbox in a shared Field.Root its own accessible name".
- **FFI-STR-04** — MUST. `<Form>` provides the form scope to every descendant including portaled ones; a field whose DOM lies outside the `<form>` element still registers with, validates for, and submits through the form scope it is rendered under (subject to FFI-ALG-04 for native inputs).
  Source: `form/Form.tsx:34-39,157-169`, `field/root/useFieldValidation.ts:25-44`. Test: [L2] `CheckboxGroup.test.tsx` "includes a group fully portaled outside the form element", "includes a context-portaled checkbox without native form association"; `RadioGroup.test.tsx` "includes a group fully portaled outside the form element in onFormSubmit".
- **FFI-STR-05** — MUST. Outside a `<Form>`, the default form scope has an empty registry, `errors = {}`, a no-op `clearErrors`, `validationMode = 'onSubmit'`, `submitCount = 0` and no form element. Consequently a field in the default `onSubmit` mode outside a `<Form>` never validates on change or blur on its own.
  Source: `internals/form-context/FormContext.ts:33-46`, `field/root/FieldRoot.tsx:88-92`. Test: [L2] `FieldRoot.test.tsx` "when not in <Form> the function does not run by default".
- **FFI-STR-06** — MUST. A `<Field.Root>` nested (at any depth) in a disabled `<Fieldset.Root>` is disabled: field `disabled = fieldsetDisabled || disabledProp`.
  Source: `field/root/FieldRoot.tsx:44-48`. Test: [L2] `FieldsetRoot.test.tsx` "keeps nested fieldsets disabled when an ancestor fieldset is disabled", "updates nested disabled precedence in both directions"; `Form.test.tsx` "excludes disabled fieldset fields from validation and onFormSubmit values".
- **FFI-STR-07** — MUST. A field has at most one **active control**. When a second control registers while another is active, the newcomer becomes active, the previous control's registry entry (if its id differs) is removed, and any pending validation of the previous control is cancelled (FFI-ALG-12).
  Source: `internals/field-register-control/useFieldControlRegistration.ts:147-167`. Test: [L2] `Form.test.tsx` "removes the previous registered field id when another control takes over"; `FieldRoot.test.tsx` "drops a pending validation when another field-aware control takes ownership", "submits the replacement control value when swapping field-aware controls".

### 4.2 API — contract parameters

- **FFI-API-01** — MUST. `validate(value, formValues)` MAY return `string`, `string[]`, `null`, `undefined`, or a promise of any of these. The result is normalized to an error list: a falsy result is the empty list; a string becomes a one-element list; an array is kept with empty-string entries removed. An empty list means *valid*.
  Source: `field/root/useFieldValidation.ts:300-332`, `field/root/FieldRoot.tsx:278-290`. Test: [L1] `FieldRoot.test.tsx` "treats $label result as valid" (empty array, undefined, empty string, array of empty strings), "accepts synchronous and async validators with no return value"; `FieldValidity.test.tsx` "should correctly pass errors when validate function returns an array of strings".
- **FFI-API-02** — MUST. `validationMode` accepts exactly `'onSubmit'`, `'onBlur'` and `'onChange'`. The effective mode of a field is `Field.Root.validationMode ?? Form.validationMode ?? 'onSubmit'`.
  Source: `form/Form.tsx:25,181`, `field/root/FieldRoot.tsx:33`, `internals/form-context/FormContext.ts:42`. Test: [L2] `FieldRoot.test.tsx` "prop: validationMode" (all three describes); precedence: none.
- **FFI-API-03** — MUST. `validationDebounceTime` (milliseconds, default `0`) delays only commits that are triggered by a value change while change-validation is active (FFI-FRM-02) and only when the new value is not the empty string.
  Source: `field/root/FieldRoot.tsx:32`, `field/root/useFieldValidation.ts:356-364`. Test: [L1] `FieldRoot.test.tsx` "should debounce validation".
- **FFI-API-04** — MUST. `Form.errors` is an object whose own enumerable keys are field names and whose values are a string or an array of strings. A field has a form error when `Object.hasOwn(errors, name)` and the value is a non-empty string or a non-empty array. Inherited properties (e.g. `constructor`) MUST be ignored; objects without a prototype MUST be accepted.
  Source: `internals/form-context/FormContext.ts:7`, `field/root/FieldRoot.tsx:94-96`, `field/error/FieldError.tsx:42-43`. Test: [L2] `FieldError.test.tsx` "ignores inherited Form error properties", "ignores empty Form error arrays"; `Form.test.tsx` "focuses asynchronously replaced external errors and clears only the changed own property".
- **FFI-API-05** — MUST. When `Field.Root` receives `dirty`, `touched` or `invalid` as a defined prop, that prop is the field's value for the flag and the corresponding internal setter becomes a no-op for user interaction. `invalid` is only honored when strictly `true`.
  Source: `field/root/FieldRoot.tsx:56-57,70-86,97`. Test: [L2] `FieldRoot.test.tsx` "controls the dirty state", "does not update controlled dirty state from user input", "controls the touched state", "does not update controlled touched state on blur"; `Form.test.tsx` "does not submit when invalid prop remains true even if validate returns null".
- **FFI-API-06** — MUST. `Field.Root.actionsRef.current.validate()` validates the field imperatively (FFI-FRM-21). `Form.actionsRef.current.validate(fieldName?)` validates every registered field, or only the first registered field whose `name` equals `fieldName`.
  Source: `field/root/FieldRoot.tsx:149-151`, `form/Form.tsx:86-102`. Test: [L2] `FieldRoot.test.tsx` "validates the field when the `validate` method is called"; `Form.test.tsx` "validates the form when the `validate` method is called", "validates a field when the `validate` method is called with the field name".
- **FFI-API-07** — MUST. The values object (`Form.Values`) is built from the field registry in registration order as `{ [entry.name]: entry.getValue() }` for every entry whose `name` is non-empty; values are the controls' typed projections (booleans, numbers, arrays, `null`, strings), not `FormData` strings.
  Source: `form/Form.tsx:128-135`, `field/root/useFieldValidation.ts:293-298`. Test: [L2] `FieldRoot.test.tsx` "receives all form values as the 2nd argument"; `Form.test.tsx` "runs when the form is submitted".

### 4.3 STA — state model

- **FFI-STA-01** — MUST. The field state object has exactly the fields `disabled`, `touched`, `dirty`, `filled`, `focused` (booleans, default `false`) and `valid` (`boolean | null`, default `null`).
  Source: `internals/field-constants/constants.ts:18-32`, `field/root/FieldRoot.tsx:112-122,239-264`. Test: [L2] `FieldValidity.test.tsx` "should pass validity data" (`valid` is `null` before validation).
- **FFI-STA-02** — MUST. `invalid = (invalidProp === true) || hasFormError` and `state.valid = !invalid && (disabled ? null : validityData.state.valid)`. Hence an app-controlled invalidity (prop or form error) keeps `valid === false` even while disabled, whereas computed validity is neutralized by `disabled`.
  Source: `field/root/FieldRoot.tsx:94-97,107-110`. Test: [L2] `FieldRoot.test.tsx` "keeps an explicitly invalid field marked invalid while disabled", "keeps a disabled field with form errors marked invalid"; `Form.test.tsx` "clears invalid UI when a fieldset field becomes disabled".
- **FFI-STA-03** — MUST. Validity data is `{ state, error, errors, value, initialValue }`. `state` holds the eleven `ValidityState` flags with `valid: boolean | null`. A fresh `Field.Root` starts with every flag `false`, `valid: null`, `error: ''`, `errors: []`, `value: null`, `initialValue: null`. (The default field scope outside `Field.Root` starts with `value: ''`.)
  Source: `field/root/FieldRoot.tsx:99-105,215-233`, `internals/field-constants/constants.ts:4-16`, `internals/field-root-context/FieldRootContext.ts:40-46`. Test: [L2] `FieldValidity.test.tsx` "should pass validity data" (both modes).
- **FFI-STA-04** — MUST. `initialValue` is captured exactly once per `Field.Root` lifetime, at the first control registration, as the registration's `value` (or `getValue()` when `value` is `undefined`). Later registrations, remounts of the same control, or replacement by a different control MUST NOT recapture it; only remounting `Field.Root` itself does.
  Source: `internals/field-register-control/useFieldControlRegistration.ts:86-103,166`. Test: [L2] `FieldRoot.test.tsx` "keeps the original baseline when a controlled control remounts", "keeps the field baseline when the control is swapped", "captures the baseline only once in StrictMode", "clears dirty after an empty text control returns to empty following a null-valued control".
  Port note 2026-10-22 (ADR 06 Amendments 2026-10-18): "captures the baseline only once in StrictMode" does not arise in the port, since Blazor runs no effect twice. The baseline is captured at the field's first registration, and a registration is keyed by the control, so a repeated one updates in place.
- **FFI-STA-05** — MUST. `dirty` is computed by the active control on every value change as "current value differs from `initialValue`" using the control's own equality (see §6 table B) and written through `setDirty`; it therefore returns to `false` when the value returns to the initial value. `setDirty(true)` also sets the dirty latch.
  Source: `field/root/FieldRoot.tsx:70-79`; per-control sites in §6. Test: [L2] `FieldRoot.test.tsx` "should apply [data-dirty] style hook to all components when dirty", "should clear [data-dirty] when a null-valued control returns to its empty initial value", "should clear [data-dirty] when a Select returns to its null initial value", "keeps [data-dirty] on a RadioGroup when returning to the first picked value"; `FieldControl.test.tsx` "clears dirty state when a numeric controlled value returns to its initial value".
- **FFI-STA-06** — MUST. The dirty latch starts as the initial `dirty` value, is set to `true` by `setDirty(true)` and by every imperative/submit validation (FFI-FRM-21), is synchronized to the `dirty` prop whenever that prop is defined, and is otherwise never reset.
  Source: `field/root/FieldRoot.tsx:59,64-68,75-77`, `internals/field-register-control/useFieldControlRegistration.ts:55`. Test: [L2] `FieldRoot.test.tsx` "should mark field as invalid for valueMissing if dirty", "uses the controlled dirty state for required validation".
- **FFI-STA-07** — MUST. `touched` becomes `true` through `setTouched(true)` at the control-specific boundary in §6 table B (blur in most controls) and is never reset by the mechanism.
  Source: `field/root/FieldRoot.tsx:81-86`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-touched] style hook to all components when touched".
- **FFI-STA-08** — MUST. `filled` is the control-specific "has a value" predicate in §6 table B, recomputed post-commit, pre-paint whenever the control's value (or default value) changes, including programmatic changes and remounts.
  Source: `field/root/FieldRoot.tsx:52`, `field/control/FieldControl.tsx:89-94`. Test: [L2] `FieldRoot.test.tsx` "should apply [data-filled] style hook to all components when filled", "changes [data-filled] when the value is changed externally"; `FieldControl.test.tsx` "sets filled state on mount when the control is prefilled", "does not set filled state on mount for an empty controlled value", "sets filled state from a controlled value on a custom element".
- **FFI-STA-09** — MUST. `focused` follows focus-ownership (FFI-ALG-13): a control acquires the flag on focus only when it is not disabled; only the owner can release it; the owner releases it on blur, when it becomes disabled, and when it unmounts (even though no blur event fires); a control that is already the active element when it mounts or becomes enabled acquires the flag post-commit, pre-paint.
  Source: `internals/field-root-context/useSetFieldFocused.ts:15-46`. Test: [L2] `FieldControl.test.tsx` "[data-focused]" ("is removed when the focused control becomes disabled", "is removed when the field root becomes disabled", "can be re-acquired after the control is re-enabled", "is removed when the focused control unmounts"), "should sync focused state when autoFocus is used with SSR"; `CheckboxRoot.test.tsx` "does not set [data-focused] when disabled", "is kept when the checkbox is focused during mount in StrictMode"; `RadioRoot.test.tsx` "does not acquire the field focused state when focused programmatically", "acquires the field focused state on focus"; `RadioGroup.test.tsx` "is kept when a previously focused radio unmounts after focus moves to a sibling", "is not reacquired when a radio disabled while focused is refocused".
  Port note 2026-10-22 (ADR 04 and ADR 01 Amendments 2026-10-22, settled by the orchestrator 2026-10-22, ADR 02 verification item 1 and ADR 15): the `form` module reads the active element at attach and when the control is enabled again. Because the interactive render replaces a prerendered element, the module also focuses an `AutoFocus` control at attach when nothing else has focus. The StrictMode title is Checkbox's (track 4).
- **FFI-STA-10** — MUST. The field's effective name is `Field.Root.name ?? <name registered by the active control>`; the control-registered name is forgotten when the control unregisters. The effective name is what form errors are looked up by, what `clearErrors` is called with, and what the registry entry carries.
  Source: `field/root/FieldRoot.tsx:61-62,156`, `internals/field-register-control/useFieldControlRegistration.ts:111,141,157-159`. Test: [L2] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values", "updates the Field.Control name fallback when the name changes", "uses the Field.Control name fallback when the Field.Root name is removed", "updates field-aware control name fallbacks when the name changes"; `FieldError.test.tsx` "uses the Field.Control name fallback for Form errors".
- **FFI-STA-11** — MUST. Disabled resolution is monotonic downward: field `disabled = fieldsetDisabled || rootDisabled`; a control's disabled is `fieldDisabled || fieldItemDisabled || groupDisabled || ownDisabled` (each term only where the control participates in that scope, see §6 table C). Every field part reflects `fieldDisabled || fieldItemDisabled` in its own state.
  Source: `field/root/FieldRoot.tsx:48`, `field/item/FieldItem.tsx:31`, `field/label/FieldLabel.tsx:39`, `field/description/FieldDescription.tsx:33`, `field/control/FieldControl.tsx:62`, `checkbox/root/CheckboxRoot.tsx:91-92`, `radio/root/RadioRoot.tsx:79`. Test: [L2] `FieldRoot.test.tsx` "should add data-disabled style hook to all components"; `FieldItem.test.tsx` "reflects disabled state on the item", "disables a wrapped checkbox", "disables a wrapped radio"; `FieldLabel.test.tsx` / `FieldDescription.test.tsx` "reflects the disabled state from Field.Item"; `FieldsetRoot.test.tsx` "passes disabled to rendered Base UI roots".

### 4.4 DOM — rendered output shared by all participants

- **FFI-DOM-01** — MUST. Every part and control that spreads the field state into its own state renders `data-disabled`, `data-touched`, `data-dirty`, `data-filled` and `data-focused` (empty-string valued) when the corresponding flag is `true`, and renders `data-valid=""` when `valid === true`, `data-invalid=""` when `valid === false`, and neither when `valid === null`. State → attribute derivation is the generic mapping of `rendering-and-composition.md` with the validity attribute mapping as the only override.
  Source: `internals/field-constants/constants.ts:34-44`, `internals/getStateAttributesProps.ts:24-28`; applied at `field/root/FieldRoot.tsx:192`, `field/control/FieldControl.tsx:196`, `field/label/FieldLabel.tsx:82`, `field/description/FieldDescription.tsx:52`, `field/error/FieldError.tsx:18-21`, `field/item/FieldItem.tsx:40`, and in every control's own mapping. Test: [L2] `FieldRoot.test.tsx` "should apply [data-field] style hooks to field components", "should apply [data-touched] style hook to all components when touched", "should apply [data-dirty] style hook to all components when dirty", "should apply [data-filled] style hook to all components when filled", "should apply [data-focused] style hook to all components when focused"; `internals/stateAttributesMapping.test.ts` "fieldValidityMapping" › "emits the validity data attributes", "emits nothing while validity is unknown".
- **FFI-DOM-02** — MUST. A form-participation input is a native `<input>` with `aria-hidden`, `tabindex="-1"`, and a visually-hidden inline style: when the input has a `name` the style is *absolute* positioning (`clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute`) so that the browser's native validation bubble anchors near the control; when nameless the style is the same but `position: fixed; top: 0; left: 0; margin: 0`.
  Source: `packages/utils/src/visuallyHidden.ts:3-25`; `checkbox/root/CheckboxRoot.tsx:207-210`, `switch/root/SwitchRoot.tsx:167-170`, `radio/root/RadioRoot.tsx:187-190`, `select/root/SelectRoot.tsx:579-582`, `combobox/root/AriaCombobox.tsx:1700-1703`, `number-field/root/NumberFieldRoot.tsx:536-538`, `otp-field/root/OTPFieldRoot.tsx:454-456`. Test: none (inline style not asserted). Docs: `handbook/forms/page.mdx` §Constraint validation ("Positioning hidden inputs").
- **FFI-DOM-03** — MUST. Select, Combobox and OTP Field give their shared form-participation input the id `${id}-hidden-input` when, and only when, it has no `name`; a named one has no id.
  Source: `select/root/SelectRoot.tsx:570`, `combobox/root/AriaCombobox.tsx:1691`, `otp-field/root/OTPFieldRoot.tsx:442`. Test: [L2] `SelectRoot.test.tsx` "sets a hidden input id when name is not provided", "does not set a hidden input id when name is provided"; `OTPFieldRoot.test.tsx` "renders a fallback hidden input id when name is not provided".
- **FFI-DOM-04** — MUST. Checkbox, Switch and Radio apply the labelable control id to the hidden `<input>` (so a native `<label for>` and the browser's unassociated-input heuristics target it) unless `nativeButton` is set, in which case the control id goes on the root element and the hidden input has no id.
  Source: `checkbox/root/CheckboxRoot.tsx:104-106,204,297`, `switch/root/SwitchRoot.tsx:84-85,121,164`, `radio/root/RadioRoot.tsx:118-119,134,186`. Test: [L2] `CheckboxRoot.test.tsx` "falls back to the Field control id when id is empty", "assigns an input id to a valueless child in a parent checkbox group", "assigns a root id to a valueless native button in a parent checkbox group".
- **FFI-DOM-05** — MUST. `<Form>` renders `<form novalidate>` by default; the consumer MAY override `noValidate` through element props.
  Source: `form/Form.tsx:104-109,139`. Test: [L2] `Form.test.tsx` "should disable native validation if set to true (default)", "should enable native validation if set to false".
- **FFI-DOM-06** — MUST. In multiple-selection mode Select and Combobox render one `<input type="hidden" name={name} value={stringified} form={form} disabled={disabled}>` per selected value (keyed by the stringified value) and leave the shared form-participation input nameless; with an empty selection no named input exists and the shared input's value is `""`. Its `required` attribute is dropped as soon as a selection exists.
  Source: `select/root/SelectRoot.tsx:489-509,572,576,584`, `combobox/root/AriaCombobox.tsx:1588-1610,1693,1696,1705`. Test: [L2] `SelectRoot.test.tsx` "should serialize multiple values correctly for form submission", "does not submit multiple values when disabled", "should serialize empty array as empty string in multiple mode", "does not mark the hidden input as required when selection exists", "keeps the hidden input required when no selection exists"; `ComboboxRoot.test.tsx` "should create multiple hidden inputs for form submission", "does not submit multiple values when disabled", "does not mark the hidden input as required when selection exists in multiple mode", "keeps the hidden input required when no selection exists in multiple mode".
- **FFI-DOM-07** — MUST. Checkbox (outside a group, non-parent) and Switch render an additional `<input type="hidden" name={name} value={uncheckedValue} form={form} disabled={disabled}>` only while unchecked, named, and `uncheckedValue` is defined; otherwise an unchecked box submits nothing, matching native checkboxes.
  Source: `checkbox/root/CheckboxRoot.tsx:400-402`, `switch/root/SwitchRoot.tsx:233-235`. Test: [L2] `CheckboxRoot.test.tsx` "submits uncheckedValue to an external form when unchecked", "does not submit uncheckedValue when disabled"; `SwitchRoot.test.tsx` "should submit custom uncheckedValue when switch is off", "submits uncheckedValue to an external form when off", "does not submit uncheckedValue when disabled".
- **FFI-DOM-08** — MUST. Every form-participation input forwards the control's `form` prop as its `form` attribute so a control rendered outside the `<form>` element still submits with it; this also makes it eligible for that form (FFI-ALG-04).
  Source: `checkbox/root/CheckboxRoot.tsx:199`, `switch/root/SwitchRoot.tsx:163`, `radio/root/RadioRoot.tsx:185`, `select/root/SelectRoot.tsx:571`, `combobox/root/AriaCombobox.tsx:1692`, `slider/thumb/SliderThumb.tsx:327`, `number-field/root/NumberFieldRoot.tsx:525`, `otp-field/root/OTPFieldRoot.tsx:443`. Test: [L2] `RadioGroup.test.tsx` "submits to an external form when `form` is provided"; `SliderRoot.test.tsx` "submits to an external form when `form` is provided"; `NumberFieldRoot.test.tsx` "submits to an external form when `form` is provided"; `CheckboxGroup.test.tsx` "includes a portaled checkbox explicitly associated with the Form".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- FFI-ARIA-08 — row "FFI registration": the label id is published after paint plus a round trip on Server; observable only as latency of the dependent `aria-labelledby` / `for` updates.
- FFI-EVT-01, FFI-FRM-04 — not an ADR 01 row: per ADR 04 Amendments the `form` module always prevents the native `submit` and C# re-issues it after validation, on both render modes, instead of deciding `preventDefault()` inside the dispatch.
- FFI-FRM-17 — row "FFI registration": the registry entry is created or refreshed after paint plus a round trip; observable only as latency of dependent attribute updates.
- FFI-FRM-32 — row FLD-FRM-17: the re-commit runs in `OnAfterRender` instead of one microtask after the blur commit; on Server after paint plus a round trip, during which the field keeps its pre-normalised value.

### 4.5 ARIA — labels, descriptions, invalidity

- **FFI-ARIA-01** — MUST. Described-by composition (FFI-ALG-09): the control's `aria-describedby` is the consumer-supplied ids (space-split), then the parent labelable scope's message ids, then this scope's message ids, de-duplicated and space-joined; it is omitted when the list is empty.
  Source: `internals/labelable-provider/LabelableProvider.tsx:68-81`. Test: [L2] `FieldDescription.test.tsx` "should set aria-describedby on the control automatically", "should preserve user aria-describedby values on the control"; `FieldError.test.tsx` "should set aria-describedby on the control automatically"; `SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset".
- **FFI-ARIA-02** — MUST. `Field.Description` publishes its id to the labelable scope's message ids for as long as it is mounted; `Field.Error` publishes its id only while it is rendered (FFI-ALG-15); an empty-string id is never published. Publication and removal happen post-commit, pre-paint when the part mounts/unmounts or starts/stops rendering.
  Source: `field/description/FieldDescription.tsx:36-46`, `field/error/FieldError.tsx:59-69`. Test: [L2] `FieldDescription.test.tsx` "does not register an empty description id"; `FieldError.test.tsx` "does not register an empty error id".
- **FFI-ARIA-03** — MUST. Validation attributes: `getValidationProps(controlDisabled, props)` returns the described-by composition of `props` plus `aria-invalid="true"` when `state.valid === false` and neither the field nor the control is disabled; otherwise `aria-invalid` is whatever the consumer passed (normally absent).
  Source: `field/root/useFieldValidation.ts:367-376`. Test: [L2] `FieldRoot.test.tsx` "should apply aria-invalid prop to control once validation finishes", "keeps an explicitly invalid field marked invalid while disabled" (no `aria-invalid` while disabled); `Form.test.tsx` "clears invalid attributes when a field control becomes disabled".
- **FFI-ARIA-04** — MUST. A native `Field.Label` renders `for={controlId}` where `controlId` is the labelable scope's resolved control id (FFI-ALG-06); when the resolved id is `null` (a control named by `aria-labelledby`, e.g. `CheckboxGroup`) no `for` attribute is rendered.
  Source: `internals/labelable-provider/useLabel.ts:28,64-69`, `internals/labelable-provider/LabelableProvider.tsx:18-20`. Test: [L2] `FieldLabel.test.tsx` "should set htmlFor referencing the control automatically"; `FieldRoot.test.tsx` "re-associates the label when a CheckboxGroup is replaced by another control", "updates label association when replacing one control with another", "updates label associations when the control id changes".
- **FFI-ARIA-05** — MUST. Labelled-by resolution for non-native controls (FFI-ALG-08): explicit `aria-labelledby` prop → the scope's label id, unless the control has a non-blank `aria-label` → a discovered native `<label>` associated with the hidden input (only when fallback is enabled, i.e. the control is not a native button). Re-evaluated post-commit, pre-paint after every update so label mount/unmount and id changes are reflected.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:6-38`. Test: [L4] `CheckboxRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input", "updates fallback `aria-labelledby` when the hidden input id changes", "prefers `aria-label` over an associated label"; `SwitchRoot.test.tsx` same three; `RadioRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input"; `FieldRoot.test.tsx` "removes `aria-labelledby` when Field.Label is removed after hydration".
- **FFI-ARIA-06** — MUST. When the discovered native label has no id and the control has a control id, the label is assigned the generated id `${controlId}-label` so it can be referenced.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:16,40-51`. Test: [L4] `CheckboxRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input" (asserts `label.id` is non-empty).
- **FFI-ARIA-07** — MUST. Controls that have their own label part (`Select.Label`, `Combobox.Label`, `Slider.Label`) resolve `aria-labelledby = fieldLabelId ?? componentLabelId`; a `Field.Label` always wins over the component's own label. The component label's id defaults to `${rootId}-label`.
  Source: `utils/resolveAriaLabelledBy.ts:3-12`, `select/trigger/SelectTrigger.tsx:84`, `combobox/trigger/ComboboxTrigger.tsx:91`, `slider/root/SliderRoot.tsx:85,112`, `select/label/SelectLabel.tsx:32-42`, `combobox/label/ComboboxLabel.tsx:36,55-63`, `slider/label/SliderLabel.tsx:30,48-52`. Test: [L2] `SelectRoot.test.tsx` "Field.Label", "Field.Label links to trigger and focuses it"; `ComboboxRoot.test.tsx` "Field.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id"; `SliderRoot.test.tsx` "Field.Label"; precedence: none.
- **FFI-ARIA-08** — MUST. A label part publishes its id to its scope post-commit, pre-paint and withdraws it on unmount only if the scope still holds that same id (so an older label's cleanup never clears a newer label's id). Server: see ADR 01 relaxation table.
  Source: `utils/useRegisteredLabelId.ts:6-20`. Test: [L4] `useRegisteredLabelId.test.tsx` "does not let an older label cleanup clear a newer label"; `FieldsetLegend.test.tsx` "updates and clears the legend association", "should set aria-labelledby on the fieldset automatically", "should set aria-labelledby on the fieldset with custom id".
  Port note 2026-10-22 (ADR 01 Amendments 2026-10-22): `Field.Label` and `Fieldset.Legend` publish through `LabelIdSlot` in `OnParametersSet` and withdraw only while current, inside the creating or removing batch. The row is met on both modes. Tested at L4 on both (`fieldset.spec.ts`, the four cited titles, `useRegisteredLabelId`'s over two legends) and at L2.
- **FFI-ARIA-09** — MUST. `CheckboxGroup` claims the control id `null` (suppressing `for`) and carries `aria-labelledby={labelId}` itself; its member checkboxes do not claim the group's scope id (they claim their own `Field.Item` scope when present). `RadioGroup` carries `aria-labelledby = fieldLabelId ?? fieldsetLegendId`.
  Source: `checkbox-group/CheckboxGroup.tsx:87-89,166-168`, `checkbox/root/CheckboxRoot.tsx:98-104`, `radio-group/RadioGroup.tsx:185,229`. Test: [L2] `CheckboxGroup.test.tsx` "labels the group rather than pointing Field.Label at one checkbox inside it"; `RadioGroup.test.tsx` "labels the radio group from the fieldset legend"; `FieldRoot.test.tsx` "keeps the group label suppressed while its subtree is hidden".
- **FFI-ARIA-10** — MUST. A `Combobox.Input` rendered inside the popup is not the field's control: it receives no validation attributes, renders the default (all-false, `valid: null`) field state attributes, and, when rendered through a field-aware element (e.g. `<Input>`), sees the default field scope. The trigger carries `aria-invalid` and the described-by ids instead.
  Source: `combobox/input/ComboboxInput.tsx:98,127-129,428-434`, `combobox/trigger/ComboboxTrigger.tsx:258`. Test: [L2] `ComboboxRoot.test.tsx` "does not apply validation ARIA attributes to input inside popup", "focuses trigger and surfaces errors when input is inside popup".

### 4.6 KEY — keyboard

| Key | Where | Behavior |
|---|---|---|
| <kbd>Enter</kbd> | `Field.Control` rendering an `<input>` | FFI-KEY-01 |
| <kbd>Enter</kbd> | `Checkbox.Root` or any descendant (bubbling) | FFI-KEY-02 |
| any other key | infrastructure | FFI-KEY-03 (not intercepted) |

- **FFI-KEY-01** — MUST. On <kbd>Enter</kbd> keydown in a `Field.Control` whose element is an `<input>`: mark the field touched; then, if the input's owning form is the surrounding `<Form>`'s element and the keydown was not default-prevented, start a 0 ms macrotask timer and, when it fires, commit the input's *current* value only if the form's submission counter is unchanged (i.e. implicit submission did not happen, for example because there is no submit button, the submit button is disabled, or another text input prevents implicit submission); otherwise (no form, a foreign form, or a prevented keydown) commit the value immediately. This guarantees exactly one validation per Enter press.
  Source: `field/control/FieldControl.tsx:109,172-191`. Test: [L4] `FieldControl.test.tsx` "validates once when Enter implicitly submits a form", "validates when Enter does not implicitly submit the form", "validates when a disabled submit button blocks implicit submission", "validates the latest value when Enter does not submit the form", "validates when Enter is pressed outside a form".
  Port note 2026-10-22 (ADR 04 and ADR 01 Amendments 2026-10-22, settled by the orchestrator 2026-10-22 under ADR 01 decision 3's ordering clause): the 0 ms macrotask is the `form` module's. It runs after the implicit submission and reports the input's current value; C# compares the submission counter. Outside a form the commit runs at the keydown report; tested at L4 on both modes. The titles with `Form` land with PR 1c.
- **FFI-KEY-02** — MUST. On <kbd>Enter</kbd> keydown reaching `Checkbox.Root`, whether dispatched on the Root itself or bubbling up from any focusable descendant (the handler has no event-target check), the checkbox MUST NOT toggle and the remaining Base UI Enter handling is vetoed; if the keydown was already default-prevented by a consumer handler that ran first, nothing else happens; otherwise the native default is cancelled without marking the keydown as handled for ancestors; after a microtask, unless a `preventDefault()` was observed during propagation, the owning form's default submitter (FFI-ALG-10) is clicked, which triggers the form's normal submit pipeline (FFI-EVT-01).
  Source: `checkbox/root/CheckboxRoot.tsx:320-362`, `packages/utils/src/getDefaultFormSubmitter.ts:14-34`. Test: [L4] `CheckboxRoot.test.tsx` "submits the form when Enter is pressed while focused".
- **FFI-KEY-03** — MUST. The infrastructure intercepts no other key. <kbd>Tab</kbd> moves focus natively (blur handlers then run, FFI-FRM-03); <kbd>Enter</kbd> in a `Field.Control` rendered as a non-`<input>` element (e.g. `<textarea>`) is not intercepted; Enter handling of Combobox, Autocomplete, Select, Number Field and OTP Field is specified in their own documents.
  Source: `field/control/FieldControl.tsx:173` (tag guard). Test: none.

### 4.7 PTR — pointer interaction of labels

- **FFI-PTR-01** — MUST. A non-native label (`nativeLabel={false}`, `Select.Label`, `Combobox.Label`, `Slider.Label`) prevents the default of `pointerdown` (no text selection, no focus theft) and on `click` focuses the resolved control with `focus({ focusVisible: true })`, looked up by id in a shadow-root-aware manner (FFI-ALG-08 step "control lookup"); it never opens a popup by itself.
  Source: `internals/labelable-provider/useLabel.ts:30-44,70-76,103-121`. Test: [L4] `FieldLabel.test.tsx` "when nativeLabel={false}, clicking focuses the associated control"; `useLabel.test.tsx` "focuses the trigger without opening the popup in the document" (Select.Label, Combobox.Label, Field.Label), "does not throw when a label is clicked in a detached React root".
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): the `form` module prevents `pointerdown` and focuses the control on `click` inside the dispatch; a consumer's C# pointer handler runs first through the decision point `label`. Tested at L4 on both modes ("when nativeLabel={false}, clicking focuses the associated control"); the `useLabel.test.tsx` titles render Select and Combobox (open to M5), and the detached-root title is React-only (ADR 06 Amendments 2026-10-18).
- **FFI-PTR-02** — MUST. A native label (`<label>`) handles `mousedown`: when `event.detail > 1` (double click) and not already default-prevented it prevents the default to stop text selection; activation and focus are otherwise left to the browser's native label behavior.
  Source: `internals/labelable-provider/useLabel.ts:46-69`. Test: [L4] `useLabel.test.tsx` "preserves native label activation inside a shadow root".
- **FFI-PTR-03** — MUST. Label interaction is ignored when the event target (resolved through shadow boundaries) is inside a `button`, `input`, `select` or `textarea` descendant of the label.
  Source: `internals/labelable-provider/useLabel.ts:46-50`. Test: [L4] `useLabel.test.tsx` "does not focus the control when a composed click originates inside a nested button".
- **FFI-PTR-04** — MUST. Control lookup from a label walks from the label's root node outward through every enclosing shadow root's host up to the document, returning the first element with the control id, so labels and controls may live in different (nested) shadow roots or in the light DOM.
  Source: `internals/labelable-provider/useLabel.ts:103-113`. Test: [L4] `useLabel.test.tsx` "focuses the trigger in its shadow root without opening the popup (outer matching ID: %s)", "focuses a control in the light DOM from a label in a shadow root", "focuses a control in an ancestor shadow root from a label in a nested shadow root".
- **FFI-PTR-05** — MUST. `Slider.Label` uses a custom focus target: the control id when resolvable, otherwise the single `input[type="range"]` under the slider control (none when there are several thumbs).
  Source: `slider/label/SliderLabel.tsx:32-52`. Test: [L4] `SliderLabel.test.tsx` "focuses the registered thumb when composed within a Field", "does nothing when a Field slider has no thumb to focus".

### 4.8 FOC — focus management

- **FFI-FOC-01** — MUST. When submission is blocked (FFI-EVT-01), the form focuses the control of the first invalid registry entry **by document order** (an entry whose control precedes the current candidate in the same tree wins; for controls in disconnected trees — separate shadow roots — registration order is kept) and, if that control is an `<input>`, selects its text.
  Source: `form/Form.tsx:41-69,242-249`. Test: [L4] `Form.test.tsx` "blocks submit and focuses the first invalid field across custom and native validation", "focuses the first invalid field in document order when keyed fields are reordered", "keeps the registration-order focus fallback stable across disconnected trees", "keeps focusing the first invalid field after a control value changes".
- **FFI-FOC-02** — MUST. An invalid entry without a usable control (its `controlRef` is `null`, e.g. a group whose inputs are all unmounted or disabled) still blocks submission; focus moves to the first invalid entry that does have a control, and if none has one, submission is blocked without moving focus.
  Source: `form/Form.tsx:42-68`. Test: [L4] `CheckboxGroup.test.tsx` "focuses a later invalid field when an inputless group is invalid without a control", "focuses a later invalid field when every checkbox in an invalid group is disabled", "focuses a remaining checkbox when Form errors unmount the first checkbox".
- **FFI-FOC-03** — MUST. After a submission that passed client-side validation, the next change of the form's current error map (and only the next one) focuses the first invalid field (per FFI-FOC-01) once, after paint. The latch is consumed by *any* identity change of that map: normally a new `errors` prop (FFI-EVT-04), but also a `clearErrors` call that actually removes a key (FFI-EVT-03 — e.g. a change in a non-blocking field whose name has a form error); a value change that leaves the map unchanged does not move focus, and a second submission re-arms the behavior.
  Source: `form/Form.tsx:38,71-84,122,143-155`. Test: [L4] `Form.test.tsx` "focuses asynchronously replaced external errors and clears only the changed own property", "focuses the first invalid field only on submit", "does not swap focus immediately on change after two submissions".
- **FFI-FOC-04** — MUST. Group controls (`CheckboxGroup`, `RadioGroup`) register a control handle that resolves, at focus time, to the control element of the representative input (first invalid eligible input, else first eligible) so that the invalid or first enabled member is focused rather than the group container or a parent checkbox.
  Source: `checkbox-group/CheckboxGroup.tsx:92-101`, `radio-group/RadioGroup.tsx:94-102`, `field/root/useFieldValidation.ts:51-66,118-121`. Test: [L4] `CheckboxGroup.test.tsx` "focuses the first checkbox when the field receives an error from Form", "focuses the invalid checkbox when a later checkbox in the group fails validation", "focuses the first child checkbox instead of a parent checkbox for Form errors", "skips a checkbox disabled by a fieldset when focusing Form errors", "validates and focuses required portaled checkboxes within the form".
- **FFI-FOC-05** — MUST. Focusing a form-participation input (as browsers do for native validation bubbles and autofill) redirects focus to the visible control: Select → trigger (`focusVisible: true`); Combobox → trigger when the input is inside the popup, else the input (falling back to the trigger); Number Field → the visible input (caret placed at the end); OTP Field → the first slot; Checkbox/Switch/Radio → the root element.
  Source: `select/root/SelectRoot.tsx:518-524`, `combobox/root/AriaCombobox.tsx:1618-1625`, `number-field/root/NumberFieldRoot.tsx:351-359,502-504`, `otp-field/root/OTPFieldRoot.tsx:407-409`, `checkbox/root/CheckboxRoot.tsx:252-254`, `switch/root/SwitchRoot.tsx:199-201`, `radio/root/RadioRoot.tsx:221-223`. Test: [L4] `SelectRoot.test.tsx` "redirects focus to the trigger when the hidden input is focused"; `OTPFieldRoot.test.tsx` "redirects hidden validation input focus to the first visible slot"; `ComboboxRoot.test.tsx` "falls back to the trigger when the hidden control is focused".
- **FFI-FOC-06** — MUST. Slider registers the shared validation input handle (the last-mounted thumb's `<input type="range">`) as its control, so a blocked submission focuses a range input rather than the non-focusable root.
  Source: `slider/root/SliderRoot.tsx:176`, `slider/thumb/SliderThumb.tsx:460`. Test: [L4] `SliderRoot.test.tsx` "moves focus to the range input on invalid submit".

### 4.9 DIS — dismissal

Not applicable — the infrastructure has no popup; popup dismissal reasons (`focus-out`, `outside-press`) are only *consumed* by Select/Combobox as blur boundaries (§6 table B).

### 4.10 EVT — callbacks and ordering

- **FFI-EVT-01** — MUST. Form submission pipeline (FFI-ALG-05): on the `submit` event the form (1) increments its submission counter, (2) calls `validate()` on every registry entry in registration order, (3) if any entry is now invalid: prevents the default, focuses per FFI-FOC-01 and stops; otherwise (4) records that a submission passed, (5) calls the consumer's `onSubmit(event)` if given, and (6) if `onFormSubmit` is given, prevents the default, builds the values object (FFI-API-07) and calls `onFormSubmit(values, details)` with `details.reason === 'none'` and `details.event` the native submit event. Server: native submit is always prevented and re-issued after C# validation, see ADR 04 Amendments.
  Source: `form/Form.tsx:109-137`. Test: [L2] `Form.test.tsx` "runs when the form is submitted" (`event.defaultPrevented === true`), "does not run when the form is invalid", "does not submit if there are errors"; `SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit" (`reason: 'none'`) (L4 for the native `submit` interception and the FFI-FOC-01 focus step).
- **FFI-EVT-02** — MUST. Every control runs the same value-change sequence once per actual value change (user, programmatic, controlled, or autofill): `clearErrors(effectiveName)` → `setDirty(valueDiffersFromInitial)` → `validation.change(value)` (`filled` is updated separately, FFI-STA-08). Dirty is written before `validation.change` because the synchronous commit reads the dirty latch to decide whether `valueMissing` is suppressed (FFI-FRM-06). Two controls deviate from this order (§6 table B): Slider runs `clearErrors` → `validation.change` → `setDirty` (SLD-FRM-04), which is conformant only because its range inputs carry no `valueMissing` constraint, so a port MAY use either order for Slider; Number Field writes `dirty` inside its value setter when the change is accepted (before the new value is stored) and runs `clearErrors` → `validation.change` on the stored value change, skipping `validation.change` once after a blur-time normalization while change-validation is inactive. The sequence MUST NOT run when the consumer cancelled the change through `details.cancel()` or when the native `input`/`change` event was default-prevented before the component handled it — except that an *uncontrolled* `Field.Control` writes `setDirty` and `setFilled` from the raw input value before checking cancelation/prevention and only skips `clearErrors` and `validation.change` (so for that path the order is `setDirty` → `setFilled` → `clearErrors` → `validation.change`); a controlled `Field.Control` runs the sequence only from its `value`-prop sync.
  Source: `field/control/FieldControl.tsx:96-105,123-143`, `checkbox/root/CheckboxRoot.tsx:184-193,211-215`, `switch/root/SwitchRoot.tsx:100-105,172-176`, `select/root/SelectRoot.tsx:281-286`, `slider/root/SliderRoot.tsx:178-191`, `number-field/root/NumberFieldRoot.tsx:259-260`, `number-field/input/NumberFieldInput.tsx:103-112,203-215`; per-control sites in §6. Test: [L2] `FieldControl.test.tsx` "validates once when changed by the user", "validates once when a controlled value is changed by the user", "syncs state and validates when the controlled value changes programmatically", "does not validate when the change is canceled", "does not clear errors or validate when change is prevented"; `CheckboxRoot.test.tsx` "validates once when changed by the user", "revalidates when a controlled value changes externally"; `SwitchRoot.test.tsx`, `RadioGroup.test.tsx`, `CheckboxGroup.test.tsx`, `SelectRoot.test.tsx`, `SliderRoot.test.tsx`, `NumberFieldRoot.test.tsx` "revalidates when the controlled value changes externally"; `SelectRoot.test.tsx` "does not update field state when autofill is canceled".
- **FFI-EVT-03** — MUST. `clearErrors(name)` removes exactly the own property `name` from the form's current error map; it is a no-op when `name` is empty/undefined or absent. Several fields clearing in the same update each succeed (functional update).
  Source: `form/Form.tsx:143-155`. Test: [L1] `Form.test.tsx` "removes errors upon change", "removes errors for every field that changes within a single commit"; `CheckboxRoot.test.tsx` / `SwitchRoot.test.tsx` / `SelectRoot.test.tsx` / `ComboboxRoot.test.tsx` / `AutocompleteRoot.test.tsx` / `SliderRoot.test.tsx` "clears external errors on change".
- **FFI-EVT-04** — MUST. The form's current error map is replaced by the `errors` prop whenever that prop's identity changes; keys cleared by `clearErrors` stay cleared until the prop identity changes again.
  Source: `form/Form.tsx:71-75,162`. Test: [L2] `Form.test.tsx` "focuses asynchronously replaced external errors and clears only the changed own property", "runs field validation on first change after Form error is set".
- **FFI-EVT-05** — MUST. `validate` is invoked at most once per commit with `(value, formValues)`, where `formValues` is computed at invocation time from the registry (unmounted, disabled or unregistered fields are absent; nameless fields are absent) and `value` is the control's raw field value (not its stringified projection).
  Source: `field/root/useFieldValidation.ts:293-300`. Test: [L2] `FieldRoot.test.tsx` "receives all form values as the 2nd argument", "unmounted fields are excluded from the validate fn"; `Form.test.tsx` "re-runs an onBlur cross-field validator on submit"; `SelectRoot.test.tsx` / `ComboboxRoot.test.tsx` "passes raw value to validate when itemToStringValue is provided".

### 4.11 FRM — the validation pipeline and form integration

**When validation runs**

- **FFI-FRM-01** — MUST. `shouldValidateOnChange()` is `true` iff the effective mode is `'onChange'`, or the mode is `'onSubmit'` and the surrounding form's submission counter is greater than zero. It gates both the change path (FFI-FRM-02) and whether `validate` runs despite native errors (FFI-FRM-07).
  Source: `field/root/FieldRoot.tsx:88-92`. Test: [L1] `FieldRoot.test.tsx` "revalidates on change" (onSubmit after submit), "validates the field on change" (onChange); `Form.test.tsx` "runs field validation on first change after Form error is set".
- **FFI-FRM-02** — MUST. `validation.change(value)` (FFI-ALG-03) cancels any pending debounced commit and invalidates any in-flight asynchronous result; then, if change-validation is active: when `value !== ''` and the debounce time is positive, it schedules a full commit after that many milliseconds (restarting the timer on each call), otherwise it commits immediately; if change-validation is inactive it runs the **revalidate pass** (FFI-FRM-05).
  Source: `field/root/useFieldValidation.ts:349-365`. Test: [L1] `FieldRoot.test.tsx` "should debounce validation", "should debounce validation for field-aware controls", "should debounce validation for radio groups", "keeps a pending debounce armed across the first registration"; `FieldControl.test.tsx` "shows a required error when a prefilled value is cleared" (empty value commits immediately).
- **FFI-FRM-03** — MUST. In `'onBlur'` mode each control commits its current value when focus leaves its blur boundary (§6 table B); in the other modes the blur boundary only marks the field touched and releases the focused flag.
  Source: `field/control/FieldControl.tsx:147-171`; per-control sites in §6. Test: [L2] `FieldRoot.test.tsx` "validates the field on blur"; `RadioGroup.test.tsx` "onBlur validates only when focus leaves the group"; `SliderThumb.test.tsx` "does not commit field validation when moving focus between range thumbs"; `SelectRoot.test.tsx` "does not mark as touched when focus moves into the popup", "validates when the popup is blurred"; `OTPFieldRoot.test.tsx` "validates the latest value only after focus leaves the OTP field in onBlur mode".
- **FFI-FRM-04** — MUST. On form submission every registry entry is committed regardless of mode (FFI-EVT-01), with its dirty latch set so `valueMissing` is no longer suppressed; the synchronous part of each commit MUST update the registry entry's validity before the form inspects it. Server: native submit is always prevented and re-issued after C# validation, see ADR 04 Amendments.
  Source: `form/Form.tsx:112-115`, `internals/field-register-control/useFieldControlRegistration.ts:53-63`, `field/root/useFieldValidation.ts:127-150,184-192`. Test: [L2] `FieldRoot.test.tsx` "should validate the field on submit"; `Form.test.tsx` "re-runs an onBlur cross-field validator on submit"; `CheckboxRoot.test.tsx` / `SwitchRoot.test.tsx` / `RadioGroup.test.tsx` / `SelectRoot.test.tsx` / `ComboboxRoot.test.tsx` / `NumberFieldRoot.test.tsx` "triggers native HTML validation on submit"; `AutocompleteRoot.test.tsx` "triggers native validation when required and empty" (L4 for the native-validation citations).
- **FFI-FRM-05** — MUST. The revalidate pass (FFI-ALG-01, `revalidate = true`) only acts while the field is currently invalid and a representative input exists. If that input's `valueMissing` is now `false`, the field's own custom message is cleared, and the field is published as *valid* — unless another registered input now carries a foreign custom error, in which case that error is published — and the registry entry ignores the external `invalid` flag for this pass; other native errors (`typeMismatch`, `badInput`, …) are deferred to the next full commit. If `valueMissing` is still `true` and no other native error is present, a full commit runs (so the native state and new value become observable immediately); if other native errors are present nothing is published.
  Source: `field/root/useFieldValidation.ts:244-276`. Test: [L1] `FieldRoot.test.tsx` "revalidates on change for `valueMissing`", "clears valueMissing on change but defers other native errors like typeMismatch until blur when both are active", "revalidates on change when clearing a type mismatch leaves only `valueMissing`", "handles both `required` and `typeMismatch`"; `FieldValidity.test.tsx` "surfaces valueMissing immediately after a stale custom error in $validationMode mode", "defers badInput during required change revalidation"; `Form.test.tsx` "clears invalid state for an unnamed registered field control on change"; `RadioGroup.test.tsx` "clears required validation when a value is selected".

**Native constraint state**

- **FFI-FRM-06** — MUST. Native state is read (FFI-ALG-02) from the representative input's `ValidityState` as the eleven flags `badInput, customError, patternMismatch, rangeOverflow, rangeUnderflow, stepMismatch, tooLong, tooShort, typeMismatch, valid, valueMissing`. When `valueMissing` is the *only* failing flag and the dirty latch is `false`, the state is rewritten to `valid: true, valueMissing: false` (pristine required fields are not reported invalid). A representative input with `willValidate === false` yields the all-false state with `valid: true`.
  Source: `field/root/useFieldValidation.ts:16,68-70,194-223,238-242`. Test: [L4] `FieldRoot.test.tsx` "should not mark invalid if `valueMissing` is the only error and not yet dirtied", "should not mark field as invalid for valueMissing if not dirty", "does not publish errors while `valueMissing` is suppressed", "should mark invalid if `valueMissing` is the only error and dirtied", "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty"; `FieldError.test.tsx` "should show error messages by default" (blur after clearing a prefilled value does not show, submit does); `NumberFieldRoot.test.tsx` "prevents Form/Field submission when the value does not match the step".
- **FFI-FRM-07** — MUST. The consumer validator runs only when the native state reports no error message, **or** change-validation is active (FFI-FRM-01); in `'onBlur'` mode (and in `'onSubmit'` mode before the first submission) a native failure short-circuits it. When the validator yields errors the published state is the native state with `valid: false, customError: true`, the errors are joined with `\n` and installed as the representative input's custom validity (if it validates), and `error`/`errors` carry the validator's messages; when it yields none, `error`/`errors` carry the native `validationMessage` (if any).
  Source: `field/root/useFieldValidation.ts:283-346`. Test: [L1] `FieldRoot.test.tsx` "runs after native validations", "keeps a native constraint failure published while the validator is in flight in %s mode" (validator not called in onBlur); `FieldError.test.tsx` "should show custom errors", "renders client validation error arrays as a list"; `FieldValidity.test.tsx` "should correctly pass errors when validate function returns a string".
- **FFI-FRM-08** — MUST. A barred input (`willValidate === false`, e.g. `readOnly`/`disabled`) exposes no native constraint, never receives the field's custom message, and any externally set custom message on it is ignored; the field's own custom errors are then held in validity data only.
  Source: `field/root/useFieldValidation.ts:178-181,238-242,337-340`. Test: [L4] `FieldRoot.test.tsx` "ignores a message set outside the field on a control barred from validation", "does not write its own message to a barred control".
- **FFI-FRM-09** — MUST. Custom-validity ownership: before reading native state the field clears only the custom message it installed itself (identified by element and exact message), restoring the custom message it displaced (recorded at install time; a *native* message is never recorded as displaced); if the owned message was replaced or withdrawn by someone else in the meantime nothing is restored. A custom message set by the consumer (`setCustomValidity`) is therefore reported as `customError` and surfaces through `Field.Error`, blocks submission, survives change-validation, and remains after the field's own error retires.
  Source: `field/root/useFieldValidation.ts:100-103,167-182,254,280-281`. Test: [L4] `FieldRoot.test.tsx` "custom validity ownership" (all 12 tests: "keeps a message set outside the field when submitting", "keeps a message set outside the field when validating on change", "keeps a message set outside the field when revalidating on change", "keeps other native errors deferred while a message set outside the field survives", "clears the message it set itself", "restores a message its own one displaced", "does not restore a message that was withdrawn while its own one was installed", "clears its own message on an input that became disabled", "does not adopt a native message as the message it displaced", "keeps a message set outside the field on another input of the same field", …); `CheckboxGroup.test.tsx` "clears custom validity from disabled registered inputs when the group becomes valid".
- **FFI-FRM-10** — MUST. The installed custom message normalizes `\r\n` and `\r` to `\n`.
  Source: `field/root/useFieldValidation.ts:170`. Test: [L4] `FieldRoot.test.tsx` "clears the message it set itself" (`'custom error\r\nmore'` → `'custom error\nmore'`).

**Asynchronous validators**

- **FFI-FRM-11** — MUST. When `validate` returns a thenable, the field publishes immediately, before awaiting: if the fresh native state is invalid, that native failure (so it keeps blocking submission); otherwise, if the mode is `'onSubmit'` or the previously published state was not a custom error, the fresh native state with `valid: null` (neutral: no `data-valid`/`data-invalid`, no `aria-invalid`, no error rendered); otherwise (an earlier custom error in `'onBlur'`/`'onChange'` mode) nothing, so the earlier error stays published and keeps blocking submission while the validator runs.
  Source: `field/root/useFieldValidation.ts:303-317`. Test: [L1] `FieldRoot.test.tsx` "publishes neutral validity while a validator is in flight in %s mode", "retires a previously valid result to neutral while revalidating in %s mode", "keeps a previously resolved error while revalidating in %s mode", "retires a stale native error to neutral once the constraint passes again", "retires a resolved async error to neutral while revalidating in onSubmit mode", "keeps a native constraint failure published while the validator is in flight in %s mode".
- **FFI-FRM-12** — MUST. Each commit carries a sequence number; `change()` and every new commit advance it. When an awaited result arrives for a superseded commit (a later change, commit, unregistration or replacement happened), it is discarded. When the result is current, native state is re-read before merging the result.
  Source: `field/root/useFieldValidation.ts:99,124-125,321-326,351`, `internals/field-register-control/useFieldControlRegistration.ts:138,152`. Test: [L1] `FieldRoot.test.tsx` "ignores stale async validation results", "ignores async validation results superseded during debounce", "drops an in-flight async validation when the control unmounts"; `FieldControl.test.tsx` "keeps the final async validation when a controlled value is normalized on blur".
- **FFI-FRM-13** — MUST. If the validator's promise rejects, the previously published state is kept (so a transient failure cannot retire an error) and the rejection propagates to the caller of `commit`.
  Source: `field/root/useFieldValidation.ts:319-321`. Test: [L1] `FieldRoot.test.tsx` "keeps the published error when async validation rejects".
- **FFI-FRM-14** — MUST. Asynchronous validators never block submission by themselves: the submit pipeline inspects only the synchronously published registry state, so a pending validator in `'onSubmit'` mode lets the form submit (and a stale earlier async error is retired to neutral by the new commit), whereas an already *resolved* async error in `'onBlur'`/`'onChange'` mode (published as `customError`) blocks until it is retired.
  Source: `form/Form.tsx:112-118`, `field/root/useFieldValidation.ts:312-317`. Test: [L2] `Form.test.tsx` "submits when a valid async validator is pending", "retires a stale async validation result on the next submit", "blocks submission on a resolved async error in $validationMode mode". Docs: `handbook/forms/page.mdx` §Custom validation.

**Publishing and the registry**

- **FFI-FRM-15** — MUST. Debounced commits (FFI-FRM-02) are cancelled, without running, when the active control unregisters (unmount, disable) or is replaced; the pending timer must not fire for a control that is no longer active.
  Source: `internals/field-register-control/useFieldControlRegistration.ts:135-144,150-153`, `field/root/useFieldValidation.ts:349-354`. Test: [L2] `FieldRoot.test.tsx` "drops a pending debounced validation when the control unmounts", "drops a pending validation when another field-aware control takes ownership".
- **FFI-FRM-16** — MUST. Publishing a result (FFI-ALG-14) produces validity data `{ value: committedValue, state, error: errors[0] ?? '', errors, initialValue: unchanged }` where `errors` is the message list only when `state.valid === false` and `[]` otherwise; the registry entry (if the field is registered) is updated first with the *combined* validity (`valid = !externalInvalid && state.valid`), then the field state is updated so that `data-valid`/`data-invalid`, `aria-invalid`, `Field.Error` and `Field.Validity` reflect it within the same update (synchronously for synchronous validators, after settlement for asynchronous ones).
  Source: `field/root/useFieldValidation.ts:127-165,184-192`, `field/utils/getCombinedFieldValidityData.ts:7-18`. Test: [L1] `FieldValidity.test.tsx` "should pass validity data" (onSubmit, onBlur), "should correctly pass errors when validate function returns a string"; `FieldRoot.test.tsx` "should apply aria-invalid prop to control once validation finishes", "supports async validation" (published after settlement).
- **FFI-FRM-17** — MUST. A registry entry `{ name, validate, validityData, controlRef, getValue }` keyed by the control id is created or refreshed post-commit, pre-paint whenever the active control registers, its value changes, the field's `name`, `invalid` or validity data change (refreshing in place keeps the entry's position in the ordered registry); it is deleted when the control unregisters (unmount, or `enabled` false — every control passes `!disabled`, and `CheckboxGroup` additionally requires a field name) or when `Field.Root` unmounts. Deleted entries are excluded from submission validation and from the values object; re-enabling re-registers. Server: see ADR 01 relaxation table.
  Source: `internals/field-register-control/useRegisterFieldControl.ts:7-46`, `internals/field-register-control/useFieldControlRegistration.ts:65-131`, `checkbox-group/CheckboxGroup.tsx:123`. Test: [L4] `Form.test.tsx` "unmounted fields should be removed from the form", "clears invalid attributes when a field control becomes disabled", "re-registers field controls when they become enabled again", "excludes disabled fieldset fields from validation and onFormSubmit values"; `FieldRoot.test.tsx` "excludes registration-gated controls from onFormSubmit when their field name is removed".
- **FFI-FRM-18** — MUST. Registry entries are keyed by control id, not by name: two fields with the same `name` are validated and reported independently.
  Source: `form/Form.tsx:113-116`, `internals/form-context/FormContext.ts:13-28`. Test: [L2] `Form.test.tsx` "keeps same-name field validity scoped on submit".
- **FFI-FRM-19** — MUST. A registered field without a name still participates in submission validation (it can block submission and gets focused) but is absent from `formValues` and the values object.
  Source: `form/Form.tsx:113-135`. Test: [L2] `Form.test.tsx` "does not submit if an unnamed registered field control is invalid", "clears invalid state for an unnamed registered field control on change".
- **FFI-FRM-20** — MUST. `Form.actionsRef.validate(name)` finds the *first* registry entry whose `name` matches and calls its `validate`; entries of unmounted, renamed or replaced controls must already be gone so only the current registration is targeted.
  Source: `form/Form.tsx:89-98`. Test: [L2] `Form.test.tsx` "targets only the current Strict Mode registration after name, id, and control replacement".
- **FFI-FRM-21** — MUST. A field's imperative `validate()` (also used by the form on submit) sets the dirty latch and commits the active control's current value (`getValue()` projection when present, else the registered value); with no active control it commits the last committed value, so a logical field (e.g. a group whose inputs are all gone, or a field with only `Field.Error`) can still run its custom validator.
  Source: `internals/field-register-control/useFieldControlRegistration.ts:36-63`. Test: [L2] `FieldRoot.test.tsx` "validates the current control value when the `validate` method is called", "validates a logical field without a mounted control"; `FieldRoot.react17.test.tsx` "allows mount-time imperative validation before the fallback id is assigned"; `CheckboxGroup.test.tsx` "validates an initially empty group through the imperative action".
- **FFI-FRM-22** — MUST. A field whose registered inputs are all ineligible (and whose shared validation input is absent) has no native constraint but still runs the consumer validator on its logical value at every validation boundary.
  Source: `field/root/useFieldValidation.ts:225-242`. Test: [L2] `CheckboxGroup.test.tsx` "still runs custom validation after every checkbox in the group unmounts", "clears a custom error when an inputless group becomes valid after submission", "validates an inputless controlled group on change with validationMode=onChange"; `RadioGroup.test.tsx` "runs the custom validator after every radio in the group unmounts", "unblocks submission after every radio in the group unmounts".
- **FFI-FRM-23** — MUST. Eligibility (FFI-ALG-04): a validation input is ignored when it matches `:disabled` (own, fieldset or group disabled), or when it is natively owned by a *different* form, or when it has an explicit `form` attribute that does not resolve to the surrounding form; an input with no native form owner and no `form` attribute (portaled outside the `<form>`) is eligible.
  Source: `field/root/useFieldValidation.ts:25-44`. Test: [L4] `CheckboxGroup.test.tsx` "ignores required checkboxes associated with a different form", "stops validating a checkbox after it changes form owner", "ignores a checkbox portaled into another form without a form attribute", "omits checkboxes disabled by a fieldset", "ignores a disabled required checkbox when validating the group", "omits selected checkboxes associated with another form".
- **FFI-FRM-24** — MUST. For fields with several validation inputs, the representative input is the first eligible invalid one in registration (mount) order, else the first eligible one; consequently a `required` on individual checkboxes must be satisfied by each checkbox (ticking a sibling does not clear it), and disabling or unmounting the representative moves representation to the next eligible input.
  Source: `field/root/useFieldValidation.ts:46-66,104-116,225-232`. Test: [L4] `CheckboxGroup.test.tsx` "keeps a required error while another required checkbox in the group is unchecked", "keeps validating the remaining required checkbox after a checked sibling unmounts", "validationMode=onChange keeps the error until every required checkbox is ticked", "validationMode=onBlur keeps the error until every required checkbox is ticked", "updates duplicate-value registrations before a parent layout effect submits".

**External (server / library) errors**

- **FFI-FRM-25** — MUST. A form error for the field's effective name (FFI-API-04) makes the field invalid (FFI-STA-02) — `data-invalid` on every part, `aria-invalid` on the control — and is rendered by a `Field.Error` without a specific `match` (string as text, arrays with more than one entry as a list, single-entry arrays as text) in preference to client validation messages; a `Field.Error` with a specific `match` keeps rendering the client-side flag/message (FFI-ALG-15).
  Source: `field/root/FieldRoot.tsx:94-97`, `field/error/FieldError.tsx:42-55,75-101`. Test: [L2] `Form.test.tsx` "should mark <Field.Control> as invalid and populate <Field.Error>", "should not mark <Field.Control> as invalid if no error is provided"; `FieldError.test.tsx` "uses `match={false}` as the default slot for Form errors", "uses an omitted `match` as the default slot for Form errors", "renders Form error arrays as a list", "renders single-item Form error arrays as text", "uses the client validation path for specific matches when Form errors are present", "uses `match={false}` as the default slot for client validation errors". Docs: `handbook/forms/page.mdx` §Server-side validation.
- **FFI-FRM-26** — MUST. A form error is cleared as soon as the field's value changes (FFI-EVT-02); afterwards, if change-validation is active (mode `'onChange'`, or `'onSubmit'` after a submission) the consumer validator runs on that change and its verdict replaces the server error; in `'onBlur'` mode the error is cleared without running the validator until the next blur.
  Source: `field/root/FieldRoot.tsx:88-92`, `field/root/useFieldValidation.ts:356-364`. Test: [L2] `Form.test.tsx` "runs field validation on first change after Form error is set", "runs field validation on change when invalid prop is true and validationMode is onChange", "does not run field validation on change for onBlur mode when invalid prop is true". Docs: `handbook/forms/page.mdx` §Server-side validation ("Once a field's value changes, any corresponding error in `errors` will be cleared").
- **FFI-FRM-27** — MUST. The `invalid` prop and form errors are folded into the registry entry's validity (`valid = !invalid && state.valid`), so a field flagged invalid externally blocks submission even when native and custom validation pass, and `Field.Validity` reports the combined verdict.
  Source: `field/utils/getCombinedFieldValidityData.ts:7-18`, `internals/field-register-control/useFieldControlRegistration.ts:75,117`, `field/validity/FieldValidity.tsx:20-24`. Test: [L2] `Form.test.tsx` "does not submit when invalid prop remains true even if validate returns null".

**Values and serialization**

- **FFI-FRM-28** — MUST. Native `FormData` participation is per control as in §6 table A: Select/Combobox submit the item stringification (`{value,label}` objects → `value`; else `itemToStringValue(item)`; else `String(item)`), one entry per selected value in multiple mode; Radio submits `serializeValue(value)` (FFI-ALG-11); Number Field submits the raw numeric string (never the formatted text); Slider submits one entry per thumb; Checkbox/Switch submit `value` (default `"on"`) only when checked; OTP Field and Autocomplete submit the string value; `Field.Control` submits its native value. (L4 for the `Field.Control` row, re-tagged 2026-10-22 per the M2 plan.)
  Source: `select/root/SelectRoot.tsx:225-240,497,574`, `combobox/root/AriaCombobox.tsx:1581-1586,1598,1698`, `radio/root/RadioRoot.tsx:191`, `number-field/root/NumberFieldRoot.tsx:527`, `slider/thumb/SliderThumb.tsx:331,454`, `checkbox/root/CheckboxRoot.tsx:258-260`, `otp-field/root/OTPFieldRoot.tsx:445`. Test: [L2] `SelectRoot.test.tsx` "uses itemToStringValue for form submission", "uses itemToStringValue for multiple selection form submission"; `ComboboxRoot.test.tsx` "serializes {value,label} objects using their value field", "serializes multiple {value,label} objects into multiple hidden inputs"; `NumberFieldRoot.test.tsx` "should include the input value in the form submission", "should not include formatting in the submitted value"; `SliderRoot.test.tsx` "should include the slider value", "should include range slider value"; `CheckboxRoot.test.tsx` "should include the checkbox value in form submission, matching native checkbox behavior"; `SwitchRoot.test.tsx` "should include the switch value in form submission, matching native checkbox behavior"; `CheckboxGroup.test.tsx` "includes the checkbox group value in form submission", "excludes parent checkboxes from form submission"; `AutocompleteRoot.test.tsx` "submits the typed input value when wrapped in Field.Root", "submits the input value directly (not selection value)", "submits the inline input value through native FormData" (L4 for the native `FormData` read).
- **FFI-FRM-29** — MUST. The values object (FFI-API-07) uses each control's typed projection (§6 table A, column "Value in `Form.Values`"): groups project only the values of inputs that are checked **and** eligible (so a disabled or other-form checkbox is omitted and a radio group whose selected radio is disabled projects `null`), while the raw logical value is what the field's own validator receives.
  Source: `form/Form.tsx:128-135`, `checkbox-group/CheckboxGroup.tsx:103-123`, `radio-group/RadioGroup.tsx:154-169`, `select/root/SelectRoot.tsx:235-252`, `combobox/root/AriaCombobox.tsx:551-560,582-591`. Test: [L2] `FieldRoot.test.tsx` "receives all form values as the 2nd argument", "submits the replacement control value when swapping field-aware controls"; `CheckboxGroup.test.tsx` "projects selected enabled checkboxes while preserving the logical validation value", "omits selected unmounted checkboxes while retaining group state across remounts", "preserves the logical field-name value when Checkbox.Root has no value prop"; `RadioGroup.test.tsx` "submits null to onFormSubmit when no radio is selected", "excludes a disabled selected radio from onFormSubmit to match native form data", "includes a selected radio again when it is re-enabled before form submission", "excludes an initially disabled selected radio from onFormSubmit to match native form data"; `SelectRoot.test.tsx` / `ComboboxRoot.test.tsx` "submits stringified value to onFormSubmit when itemToStringValue is provided"; `SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit", "submits a clamped single-thumb slider value to onFormSubmit"; `NumberFieldRoot.test.tsx` "prevents Form/Field submission when the value does not match the step" (`quantity: 0.1`).
- **FFI-FRM-30** — MUST. No control listens for the form `reset` event. A native reset therefore does not alter component-managed state (checked, selected, dirty, touched, filled, validity); only the browser-managed value of an uncontrolled native `Field.Control` is restored by the browser itself.
  Source: absence (no `reset` listener anywhere under `packages/react/src`). Test: [L4] `SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset".
  Port note 2026-10-22 (switch.md §16 Q1 option (b), chosen by the user 2026-10-22; ADR 04 Amendments 2026-10-22, the hidden input's `checked`): the component-managed state is still never altered by a reset, but the `dom` primitive that owns a hidden checkbox or radio input's `checked` property listens for its owning form's `reset` and re-asserts the property one task later, after the reset algorithm has run. Without it the browser would put the input back to the `checked` attribute C# rendered at first, and a submit after the reset would not match the state, as React 19 avoids by keeping `defaultChecked` in sync. No component's C# listens for `reset`.
- **FFI-FRM-31** — MUST. Browser autofill written into a form-participation input flows through the control's normal value-change path (FFI-EVT-02): Select/Combobox resolve the autofilled text to an item by case-insensitive comparison with each item's stringified value, then label, then rendered text (deferred by a microtask after force-mounting the list), and change nothing when no item matches; Number Field uses the input's `valueAsNumber` (`NaN` → `null`); OTP Field normalizes the text; `Field.Control` receives it as an ordinary change. A cancelled resulting change leaves field state untouched.
  Source: `select/root/SelectRoot.tsx:525-568`, `combobox/root/AriaCombobox.tsx:1626-1689`, `number-field/root/NumberFieldRoot.tsx:505-521`, `otp-field/root/OTPFieldRoot.tsx:410-438`. Test: [L2] `SelectRoot.test.tsx` "marks the field dirty and validates after successful autofill", "does not update field state when autofill is canceled", "leaves the value untouched when autofill matches no item"; `ComboboxRoot.test.tsx` "marks the field dirty and validates after successful autofill", "does not update field state when autofill is canceled", "does not mark the field dirty when input autofill is canceled"; `NumberFieldRoot.test.tsx` "validates the parsed number when handling browser autofill", "validates the autofilled value even when the change is canceled", "clears the value when autofill empties the hidden input"; `OTPFieldRoot.test.tsx` "handles password manager autofill through the hidden input", "accepts valid hidden-input autofill and preserves focus when autofill is cleared" (L4 for the Select/Combobox resolution deferred by a microtask).
- **FFI-FRM-32** — MUST. A controlled `Field.Control` in `'onBlur'` mode commits the value present at blur and then, after a microtask, commits again only if the DOM value was rewritten by the blur handler to something that is neither the blurred value nor the initial value (a rewrite back to the initial value is treated as a programmatic reset and not validated). Server: see ADR 01 relaxation table.
  Source: `field/control/FieldControl.tsx:151-169`. Test: [L4] `FieldControl.test.tsx` "validates the final controlled value when it is normalized on blur", "keeps the final async validation when a controlled value is normalized on blur", "does not validate when a controlled value is reset to the initial value on blur".
- **FFI-FRM-33** — MUST. `Field.Control` registers its live DOM value as its form value (`getValue()` reads the input) and, when controlled, `String(value)` as its registered value; its `initialValue` is therefore the string present at first registration, and dirty is `String(value) !== (initialValue ?? '')`.
  Source: `field/control/FieldControl.tsx:74-87,102,135`. Test: [L2] `FieldRoot.test.tsx` "uses the Field.Control name for form submission and form validation values"; `FieldControl.test.tsx` "clears dirty state when a numeric controlled value returns to its initial value".

**Label and id wiring**

- **FFI-FRM-34** — MUST. Server-rendered markup never carries an `aria-labelledby`/`for` that points at an absent label, and the association appears after hydration; the generated ids used for the association are stable across server and client (the labelable scope's default id is used by both the label's `for` and the control's `id` before any registration runs).
  Source: `internals/labelable-provider/useLabelableId.ts:79-82`, `internals/labelable-provider/LabelableProvider.tsx:12-20`, `internals/labelable-provider/useAriaLabelledBy.ts:14,26-35`. Test: [L4] `FieldRoot.test.tsx` "does not set `aria-labelledby` during SSR when Field.Label is absent", "keeps `aria-labelledby` valid when toggling from Checkbox.Root to Select.Root after hydration"; `FieldsetLegend.test.tsx` "does not set `aria-labelledby` during SSR when legend is absent", "sets `aria-labelledby` after hydration without a custom legend id"; `AutocompleteRoot.test.tsx` "server-renders only the inline input name for native FormData".
  Port note 2026-10-22 (ADR 01 Amendments 2026-10-22): the legend clause reads differently in the port. The legend publishes while it initializes, so prerendered markup carries the root's `aria-labelledby`, pointing at a legend that is present and whose generated id the interactive render keeps (the FST-DOM-08 port note). The field half holds as written: the control-id claim stays post-render, so the scope's default id pairs `for` and `id` in server markup. The Select and Autocomplete titles are open to M5.
- **FFI-FRM-35** — MUST. When a control's subtree is hidden but kept in the DOM (a mode in which its registrations are torn down while its elements persist), the labelable scope keeps the last selected control id (including a `null` claim), so `for`/`aria-labelledby` stay valid and do not loop or churn on re-show.
  Source: `internals/labelable-provider/LabelableProvider.tsx:36-41,47-50`. Test: [L2] `FieldRoot.test.tsx` "keeps an explicit control id while the subtree is hidden", "keeps the group label suppressed while its subtree is hidden", "preserves label association without looping when a control is unmounted and remounted", "preserves a non-native label association when a control is unmounted and remounted".
- **FFI-FRM-36** — MUST. Generated ids have the form `base-ui-<unique>`; a consumer-supplied id is used verbatim. When an explicit control id is later removed, the control falls back to a generated id and the label follows within the same update (no commit may render the pair unassociated).
  Source: `internals/useBaseUiId.ts:9-11`, `packages/utils/src/useId.ts:42-52`, `internals/labelable-provider/useLabelableId.ts:17,40-46`. Test: [L2] `FieldRoot.test.tsx` "falls back to a generated id when the control id is removed", "drops a stale explicit id when an id-less control replaces the control that owned it"; `FieldRoot.react17.test.tsx` "falls back to a generated id when an explicit control id is removed".

### 4.12 ANI — transitions

Not applicable — `Field.Error`'s mount/unmount transition (`data-starting-style`/`data-ending-style`) uses `transitions-and-mounting.md` and is specified in `components/field.md`; the infrastructure only decides *whether* the error renders (FFI-ALG-15). The two `FieldError.test.tsx` "animations" tests ("triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount") are mapped in `components/field.md` §13.

### 4.13 POS — positioning

Not applicable.

### 4.14 A11Y — other accessibility behavior

Not applicable — the mechanism emits no live regions or announcements. Three requirements originally filed here (server-rendered associations, hidden-subtree id retention, id format) concern label/id wiring, which `conventions.md` §3.3 now assigns to FRM; they were withdrawn and re-issued as FFI-FRM-34..36.

- **FFI-A11Y-01** — Withdrawn: label/id wiring belongs to FRM per `conventions.md` §3.3; re-issued as FFI-FRM-34.
- **FFI-A11Y-02** — Withdrawn: re-issued as FFI-FRM-35.
- **FFI-A11Y-03** — Withdrawn: re-issued as FFI-FRM-36.

### 4.15 I18N

Not applicable — nothing in the mechanism is locale-dependent; Number Field's locale parsing and Slider's formatting are excluded from the submitted value (FFI-FRM-28).

### 4.16 EDGE — see §7.

## 5. Algorithms

All pseudo-code is language-neutral. `∅` denotes "no element". "Publish" always means FFI-ALG-14.

- **FFI-ALG-01** — MUST. The implementation reproduces the following procedure exactly (**Commit**).
  Source: `field/root/useFieldValidation.ts:123-347`. Test: [L1] `FieldRoot.test.tsx` "runs after native validations", "publishes neutral validity while a validator is in flight in %s mode", "revalidates on change for `valueMissing`"; `FieldValidity.test.tsx` "surfaces valueMissing immediately after a stale custom error in $validationMode mode" (L4 for the native-state read and custom-validity steps).

```
commit(value, revalidate = false):
  seq = ++commitSequence                              # FFI-FRM-12
  element = representative()                          # FFI-ALG-04; inputRef when registry empty
  refreshState():
    element = representative()
    return element ≠ ∅ and element.willValidate ? readNativeState(element) : ALL_FALSE_VALID_TRUE

  if revalidate:                                      # FFI-FRM-05
    if fieldState.valid ≠ false or element = ∅: return
    if not element.validity.valueMissing:
      clearOwnCustomValidity()
      current = representative()
      foreign = (current ≠ ∅ and current.validity.customError) ? [current.validationMessage] : []
      publish(ALL_FALSE with valid = foreign.isEmpty, customError = not foreign.isEmpty,
              foreign, externalInvalid = false)
      return
    for key in VALIDITY_KEYS − {valid, valueMissing, customError}:
      if element.validity[key]: return                 # defer other native errors
    # still value-missing and nothing else wrong: fall through to a full commit

  cancelDebounceTimer()
  clearOwnCustomValidity()                             # FFI-FRM-09
  nextState = refreshState()
  messages  = (element ≠ ∅ and element.validationMessage ≠ "") ? [element.validationMessage] : []
  onChange  = shouldValidateOnChange()                 # FFI-FRM-01

  if messages.isEmpty or onChange:                     # FFI-FRM-07
    formValues = { entry.name: entry.getValue() for entry in registry if entry.name }
    result = validate(value, formValues)
    if result is thenable:                             # FFI-FRM-11
      if nextState.valid = false:
        publish(nextState, messages)
      else if mode = 'onSubmit' or not currentValidityData.state.customError:
        nextState.valid = null
        publish(nextState, messages)
      result = await result                            # rejection: keep published state, rethrow
      if seq ≠ commitSequence: return                  # superseded
      nextState = refreshState()
    errors = normalize(result)                         # FFI-API-01
    if errors not empty:
      nextState.valid = false; nextState.customError = true
      if element ≠ ∅ and element.willValidate:
        installOwnCustomValidity(element, errors.join("\n"))
      messages = errors
    else:
      messages = (element ≠ ∅ and element.validationMessage ≠ "") ? [element.validationMessage] : []

  publish(nextState, messages)
```

- **FFI-ALG-02** — MUST. The implementation reproduces the following procedure exactly (**Native state read**).
  Source: `field/root/useFieldValidation.ts:16,194-223`. Test: [L4] `FieldRoot.test.tsx` "should not mark invalid if `valueMissing` is the only error and not yet dirtied", "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty".

```
readNativeState(el):
  s = { key: el.validity[key] for key in [badInput, customError, patternMismatch, rangeOverflow,
        rangeUnderflow, stepMismatch, tooLong, tooShort, typeMismatch, valid, valueMissing] }
  onlyValueMissing = false
  for key in keys − {valid}:
    if key = valueMissing and s[key]: onlyValueMissing = true
    else if s[key]: return s                            # some other error is present
  if onlyValueMissing and not dirtyLatch:               # FFI-FRM-06
    s.valid = true; s.valueMissing = false
  return s
```

- **FFI-ALG-03** — MUST. The implementation reproduces the following procedure exactly (**Change**).
  Source: `field/root/useFieldValidation.ts:349-365`. Test: [L1] `FieldRoot.test.tsx` "should debounce validation", "revalidates on change"; `FieldControl.test.tsx` "shows a required error when a prefilled value is cleared".

```
change(value, cancelPending = false):
  cancelDebounceTimer(); commitSequence += 1
  if cancelPending: return                              # used on unregister / replacement
  onChange = shouldValidateOnChange()
  if onChange and value ≠ "" and debounceMs > 0:
    startDebounceTimer(debounceMs, () => commit(value))
  else:
    commit(value, revalidate = not onChange)
```

- **FFI-ALG-04** — MUST. The implementation reproduces the following procedure exactly (**Eligibility and representative input**).
  Source: `field/root/useFieldValidation.ts:32-66`. Test: [L4] `CheckboxGroup.test.tsx` "ignores required checkboxes associated with a different form", "keeps a required error while another required checkbox in the group is unchecked", "ignores a disabled required checkbox when validating the group".

```
isEligible(input, formElement):
  if input matches ':disabled': return false
  if formElement = ∅ or input.form = formElement: return true
  return input.form = ∅ and input has no "form" attribute

representative():
  if registeredInputs not empty:
    fallback = ∅
    for input in registeredInputs (registration order):
      if not isEligible(input, formElement): continue
      if not input.validity.valid: return input
      if fallback = ∅: fallback = input
    return fallback
  return inputRef                                       # may be ∅
```

- **FFI-ALG-05** — MUST. The implementation reproduces the following procedure exactly (**Submit and focus-first-invalid**).
  Source: `form/Form.tsx:41-69,109-137,242-249`. Test: [L2] `Form.test.tsx` "blocks submit and focuses the first invalid field across custom and native validation", "focuses the first invalid field in document order when keyed fields are reordered", "runs when the form is submitted", "focuses the first invalid field only on submit" (L4 for the native `submit` interception and the focus-first-invalid `select()` step).

```
onSubmit(event):
  submitCount += 1
  for entry in registry: entry.validate()               # synchronous part publishes to registry
  if focusFirstInvalid(): event.preventDefault(); return
  submitted = true
  consumerOnSubmit?(event)
  if onFormSubmit:
    event.preventDefault()
    values = { entry.name: entry.getValue() for entry in registry if entry.name }
    onFormSubmit(values, eventDetails(reason = 'none', event))

focusFirstInvalid():
  hasInvalid = false; first = ∅
  for entry in registry (registration order):
    if entry.validityData.state.valid ≠ false: continue
    hasInvalid = true
    c = entry.controlRef.current
    if c ≠ ∅ and (first = ∅ or comesBeforeInSameTree(c, first)): first = c
  if first ≠ ∅:
    first.focus(); if first.tagName = 'INPUT': first.select()
    return true
  return hasInvalid

comesBeforeInSameTree(a, b):
  pos = a.compareDocumentPosition(b)
  return not (pos & DISCONNECTED) and (pos & FOLLOWING)

onErrorsChanged():                                      # FFI-FOC-03
  if submitted: submitted = false; focusFirstInvalid()
```

- **FFI-ALG-06** — MUST. The implementation reproduces the following procedure exactly (**Control-id selection in a labelable scope**).
  Source: `internals/labelable-provider/LabelableProvider.tsx:12-66`. Test: [L2] `FieldLabel.test.tsx` "keeps the selected control id when another control unmounts", "falls over to the remaining control when the selected one unmounts"; `FieldRoot.test.tsx` "keeps an explicit control id while the subtree is hidden".

```
state: defaultId (generated), controlIdState = defaultId, registrations: ordered map source → (id | null)
controlId = controlIdState = undefined ? defaultId : controlIdState   # null is a valid value

registerControlId(source, nextId):
  if nextId = undefined: registrations.delete(source) else registrations.set(source, nextId)
  controlIdState = select(prev = controlIdState):
    if registrations.isEmpty: return prev               # hidden subtree keeps its selection
    for id in registrations.values (insertion order):
      if id = prev: return prev                         # keep current while still registered
    return first value of registrations

resetControlId():
  if registrations.isEmpty: controlIdState = defaultId
```

- **FFI-ALG-07** — MUST. The implementation reproduces the following procedure exactly (**Control-id claim**).
  Source: `internals/labelable-provider/useLabelableId.ts:10-83`. Test: [L2] `FieldRoot.test.tsx` "falls back to a generated id when the control id is removed", "drops a stale explicit id when an id-less control replaces the control that owned it", "re-associates the label when a CheckboxGroup is replaced by another control"; `FieldControl.test.tsx` "updates the label association when the control is swapped".

```
claim(idProp /* string | null | undefined */, enabled = true):
  ownId = generated id (never seeded from idProp)
  post-commit, pre-paint after every update:
    if not enabled or no labelable scope: unregister(); stop
    if idProp ≠ undefined: hadExplicit = true; next = idProp          # may be null
    else if hadExplicit:   next = ownId                               # explicit id removed → fall back
    else:                  resetControlId(); stop                     # never had an id: use scope default
    if next = undefined: unregister(); stop
    registered = true; registerControlId(source, next)
  on unmount (layout phase, before a replacement registers): unregister()
  rendered id = (enabled ? scope.controlId : undefined) ?? idProp ?? ownId
```

- **FFI-ALG-08** — MUST. The implementation reproduces the following procedure exactly (**Labelled-by resolution, native-label discovery and control lookup**).
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:6-74`, `internals/labelable-provider/useLabel.ts:103-121`. Test: [L4] `CheckboxRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input", "prefers `aria-label` over an associated label"; `useLabel.test.tsx` "focuses a control in an ancestor shadow root from a label in a nested shadow root".

```
ariaLabelledBy(explicit, labelId, hiddenInput, fallbackEnabled = true, controlId, ariaLabel):
  hasAriaLabel = ariaLabel.trim() ≠ ""
  implicit = hasAriaLabel ? undefined : labelId
  post-commit, pre-paint after every update:
    fallback = (explicit or labelId or hasAriaLabel or not fallbackEnabled)
               ? undefined : discover(hiddenInput, controlId ? controlId + "-label" : undefined)
  return explicit ?? implicit ?? fallback

discover(input, generatedLabelId):
  label = (input.parentElement is LABEL) ? parent
        : (input.id and input.nextElementSibling is LABEL with htmlFor = input.id) ? that sibling
        : input.labels?[0]
  if label = ∅: return undefined
  if label.id = "" and generatedLabelId: label.id = generatedLabelId
  return label.id or undefined

controlLookup(label, id):                               # used by non-native label click
  root = label.getRootNode()
  while root is a ShadowRoot:
    if root.getElementById(id): return it
    root = root.host.getRootNode()
  return ownerDocument(label).getElementById(id)
```

- **FFI-ALG-09** — MUST. The implementation reproduces the following procedure exactly (**Described-by composition**).
  Source: `internals/labelable-provider/LabelableProvider.tsx:68-81`. Test: [L1] `FieldDescription.test.tsx` "should preserve user aria-describedby values on the control"; `OTPFieldRoot.test.tsx` "applies the Field description to the group".

```
describedBy(props):
  ids = props["aria-describedby"] ? props["aria-describedby"].split(" ") : []
  ids += parentScope.messageIds; ids += thisScope.messageIds
  return { ...props, "aria-describedby": dedupe(ids).join(" ") or undefined }
```

- **FFI-ALG-10** — MUST. The implementation reproduces the following procedure exactly (**Default submitter**).
  Source: `packages/utils/src/getDefaultFormSubmitter.ts:14-34`. Test: [L3] `getDefaultFormSubmitter.test.ts` (all five cases, FFI-EDGE-04).

```
defaultSubmitter(form):
  if form = ∅: return ∅
  for candidate in form.elements:                       # includes controls associated via form="…"
    if candidate.tagName ∈ {BUTTON, INPUT} and candidate.type = 'submit': return candidate   # even if disabled
  return ∅                                              # input[type=image] is never considered
```

- **FFI-ALG-11** — MUST. The implementation reproduces the following procedure exactly (**String serialization**).
  Source: `internals/serializeValue.ts:1-13`. Test: none (exercised indirectly by `RadioGroup.test.tsx` "submits to an external form when `form` is provided").

```
serializeValue(v): if v = null/undefined: ""  else if v is string: v
                   else try JSON.stringify(v) catch: String(v)
```

- **FFI-ALG-12** — MUST. The implementation reproduces the following procedure exactly (**Control registration**).
  Source: `internals/field-register-control/useFieldControlRegistration.ts:17-172`, `internals/field-register-control/useRegisterFieldControl.ts:7-46`. Test: [L2] `FieldRoot.test.tsx` "keeps the field baseline when the control is swapped", "captures the baseline only once in StrictMode", "validates a logical field without a mounted control"; `Form.test.tsx` "removes the previous registered field id when another control takes over", "re-registers field controls when they become enabled again".

```
control side (post-commit, pre-paint whenever any argument changed; and on unmount with ∅):
  registerFieldControl(source, enabled ? {controlRef, id, name, getValue, value} : ∅)

field side:
  register(source, reg):
    if reg = ∅:
      if activeSource = source:
        activeSource = ∅; change(∅, cancelPending = true)      # FFI-FRM-15
        registry.delete(registration.id); registration = ∅
        registeredName = undefined; registeredId = undefined
      return
    if activeSource ≠ ∅ and activeSource ≠ source: change(∅, cancelPending = true)   # FFI-STR-07
    previousId = registration?.id
    activeSource = source; registration = reg
    if fieldName = undefined: registeredName = reg.name
    registeredId = reg.id
    if previousId and previousId ≠ reg.id: registry.delete(previousId)
    if not initialCaptured:                                   # FFI-STA-04
      initialCaptured = true
      validityData.initialValue = reg.value ≠ undefined ? reg.value : valueForForm()
    refresh()

  valueForForm(): registration.getValue ? registration.getValue() : registration.value
  refresh(): if registration.id:
    registry.set(registration.id, { getValue: valueForForm, name: fieldName ?? registration.name,
                                    controlRef: registration.controlRef,
                                    validityData: combined(validityData, invalid), validate })
  post-commit, pre-paint whenever invalid, fieldName or validityData changed: refresh()
  on Field.Root unmount: registry.delete(registration.id)

  validate():                                                 # FFI-FRM-21
    dirtyLatch = true
    commit(registration = ∅ ? validityData.value
                            : (registration.value ≠ undefined ? registration.value : valueForForm()))
```

- **FFI-ALG-13** — MUST. The implementation reproduces the following procedure exactly (**Focus ownership**).
  Source: `internals/field-root-context/useSetFieldFocused.ts:15-46`. Test: [L2] `FieldControl.test.tsx` "is removed when the focused control becomes disabled", "is removed when the focused control unmounts", "can be re-acquired after the control is re-enabled"; `RadioGroup.test.tsx` "is kept when a previously focused radio unmounts after focus moves to a sibling".

```
setFieldFocused(focused):                               # per control; the function identity is the owner token
  if focused ? not disabled : owner = self:
    owner = focused ? self : ∅
    onFocusedChange?(focused)
    fieldScope.setFocused(focused)

post-commit, pre-paint whenever `disabled` changed (and on mount):
  if focusTarget = activeElement(focusTarget.getRootNode()): setFieldFocused(true)
cleanup (before re-run and on unmount): setFieldFocused(false)
```

- **FFI-ALG-14** — MUST. The implementation reproduces the following procedure exactly (**Publish and combined validity**).
  Source: `field/root/useFieldValidation.ts:127-192`, `field/utils/getCombinedFieldValidityData.ts:7-18`. Test: [L1] `FieldValidity.test.tsx` "should pass validity data"; `Form.test.tsx` "does not submit when invalid prop remains true even if validate returns null".

```
publish(state, messages, externalInvalid = invalid):
  errors = state.valid = false ? messages : []
  data = { value: committedValue, state, error: errors[0] ?? "", errors, initialValue: validityData.initialValue }
  id = registeredId ?? scope.controlId
  if id ≠ ∅ and registry.has(id):
    registry.set(id, { ...registry.get(id), validityData: combined(data, externalInvalid) })
  validityData = data                                   # drives state.valid, data-valid/invalid, aria-invalid, Field.Error, Field.Validity

combined(data, invalid): { ...data, state: { ...data.state, valid: not invalid and data.state.valid } }
```

- **FFI-ALG-15** — MUST. The implementation reproduces the following procedure exactly (**Error-slot resolution (`Field.Error`)**). Rendering details (list markup, transitions) are in `components/field.md`.
  Source: `field/error/FieldError.tsx:42-101`. Test: [L2] `FieldError.test.tsx` "should only render when `match` matches constraint validation", "uses an omitted `match` as the default slot for Form errors", "uses the client validation path for specific matches when Form errors are present", "always renders the error message when `match` is true".

```
formError = fieldName and hasOwn(errors, fieldName) ? errors[fieldName] : null
hasFormError = isArray(formError) ? formError.length > 0 : truthy(formError)
rendered = match = true          ? true
         : fieldState.disabled   ? false
         : match is string       ? validityData.state[match] = true
         :                         hasFormError or validityData.state.valid = false
message  = (match not string and hasFormError) ? formError
         : validityData.errors.length > 1       ? validityData.errors
         :                                        validityData.error
# the error id is published to the labelable scope only while `rendered` (FFI-ARIA-02)
```

- **FFI-ALG-16** — MUST. The implementation reproduces the following procedure exactly (**`Field.Control` Enter commit**).
  Source: `field/control/FieldControl.tsx:172-191`. Test: [L2] `FieldControl.test.tsx` "validates once when Enter implicitly submits a form", "validates the latest value when Enter does not submit the form", "validates when Enter is pressed outside a form" (L4 for the implicit-submission step).
  Port note 2026-10-22: see FFI-KEY-01. The timer step runs in the `form` module (settled by the orchestrator 2026-10-22), the counter comparison in C#.

```
onKeyDown(Enter) on an INPUT element:
  setTouched(true); v = input.value; form = input.form
  if form ≠ ∅ and form = formScope.element and not event.defaultPrevented:
    count = submitCount
    after a 0 ms macrotask: if submitCount = count: commit(input.value)   # no implicit submission happened
  else: commit(v)
```

## 6. Configuration surface used by components

### 6.1 Field-scope operations a control may call

| Operation | Signature | Meaning / contract |
|---|---|---|
| `registerFieldControl` (via control registration) | `(controlRef, id, value, getValue?, enabled = true, name?)` | Declare this control as the field's active control (FFI-ALG-12). `enabled` is `!disabled` for every control; `CheckboxGroup` also requires a field `name`. `getValue` is the projection used for `Form.Values`/`formValues`; `value` is the raw logical value (used for `initialValue` and dirty baselines). |
| `setTouched(true)` | | Mark touched (FFI-STA-07). |
| `setDirty(boolean)` | | Report whether the value differs from `initialValue` (FFI-STA-05). |
| `setFilled(boolean)` | | Report the control-specific "has a value" predicate (FFI-STA-08). |
| `setFocused` (via focus ownership) | `useSetFieldFocused(disabled, focusTargetRef, onFocusedChange?)` → `(focused) => void` | Acquire/release the focused flag (FFI-ALG-13). |
| `validation.change(value)` | | Value changed (FFI-ALG-03). Always preceded by `clearErrors(name)` and `setDirty`. |
| `validation.commit(value)` | | Blur-boundary commit in `'onBlur'` mode (FFI-FRM-03); returns a promise that rejects if the validator rejects. |
| `validation.getValidationProps(controlDisabled, props)` | | Described-by + `aria-invalid` (FFI-ARIA-03). Applied to the focusable element and to the form-participation input. |
| `validation.registerInput(element, { controlRef, value })` | returns unregister | Group members add their native input to the registry of validation inputs (FFI-FRM-24). `value` is the member's value for form projection (`undefined` for radios). |
| `validation.inputRef` | DOM handle | The single validation input of non-group controls (merged into the hidden input's handle). Groups MUST NOT write it. |
| `validation.getInputControl()` | | Control element of the representative input (used as the group's `controlRef`). |
| `validityData.initialValue` | | Baseline for dirty comparisons. |
| `validationMode`, `shouldValidateOnChange()` | | Read to decide blur commits / revalidation gating. |
| `clearErrors(name)` (form scope) | | Drop the external error for this field on change (FFI-EVT-03). |
| `name`, `disabled`, `invalid` (field scope) | | Field-level overrides: `name = fieldName ?? ownName`, `disabled = fieldDisabled \|\| …`; `invalid` is read directly only by Number Field and OTP Field for `aria-invalid` on their visible slots. |

### 6.2 Labelable-scope operations

| Operation | Signature | Used by |
|---|---|---|
| control-id claim | `useLabelableId({ id, enabled })` → rendered id | `Field.Control`, `Checkbox.Root` (`enabled` = owns the scope), `Switch.Root`, `Radio.Root`, `CheckboxGroup` (`id: null`), `Select.Root` + `Select.Trigger`, `Combobox.Root` + `Combobox.Trigger` (trigger only when input is inside popup), `NumberField.Root`, `OTPField.Root`, `Slider.Thumb` (single-thumb only) |
| label behavior | `useLabel({ id, fallbackControlId, native, setLabelId, focusControl })` | `Field.Label` (native by default), `Select.Label`, `Combobox.Label`, `Slider.Label` (non-native, custom focus) |
| label id publication | `useRegisteredLabelId(id, setLabelId)` | `Field.Label` (through `useLabel`), `Fieldset.Legend`, `Meter.Label`, `Progress.Label` |
| labelled-by resolution | `useAriaLabelledBy(explicit, labelId, hiddenInputRef, fallbackEnabled, controlId, ariaLabel)` | `Checkbox.Root`, `Switch.Root`, `Radio.Root` (fallback disabled when `nativeButton`), `OTPField.Root` (fallback always enabled) |
| field-label precedence | `resolveAriaLabelledBy(fieldLabelId, localLabelId)` | `Select.Trigger`, `Combobox.Trigger`, `Slider.Root` |
| described-by composition | `getDescriptionProps(props)` | every control's focusable element and hidden input (directly or through `getValidationProps`); `OTPField.Root` merges it onto its group element |
| message-id publication | `setMessageIds` | `Field.Description` (while mounted), `Field.Error` (while rendered) |

### 6.3 Control registration matrix

Rows are the controls; `Input` is an alias of `Field.Control` (`input/Input.tsx:13-18`). `Toggle` and `ToggleGroup` are **not** form controls: they import nothing from this mechanism, render no hidden input and have no `name` (`toggle-group/ToggleGroup.tsx:1-23`).

**Table A — registration, native input, serialization**

| Control | Registered control handle → focused on invalid submit | Registered value (raw; `initialValue`, dirty, `validate` arg) | Value in `Form.Values` (`getValue`) | Native / form-participation input(s) | `FormData` entry | Validation path |
|---|---|---|---|---|---|---|
| `Field.Control` / `Input` (`field/control/FieldControl.tsx:78-87,111-122`) | the input itself (`validation.inputRef`) | controlled: `String(value)`; uncontrolled: live DOM value | live DOM value (string) | the element itself (`<input>` by default; `name`, `disabled`, `required`/`type`/`pattern`/`min`… passed through) | `name=value` natively | native constraints of the element + `validate` |
| `Checkbox.Root` standalone (`checkbox/root/CheckboxRoot.tsx:151,155-163,195-263,400-403`) | root `<span role=checkbox>` | `checked` (boolean) | `checked` | hidden `<input type="checkbox" checked name required disabled form value? id>` registered as validation input; plus `<input type="hidden" value={uncheckedValue}>` when unchecked and named | `name=value` (`value ?? "on"`) when checked; `uncheckedValue` when unchecked and set | native `required` on hidden input + `validate` |
| `Checkbox.Root` inside `CheckboxGroup` (`checkbox/root/CheckboxRoot.tsx:134,151,157-163,239-245,259`) | — (group registers) | — | — | hidden checkbox input registered in the group's validation registry with `value`; `value` attribute `checked && value \|\| ""`; parent checkbox has no `name` and is not registered | `name=value` for each checked member; parent never submits | per-member native `required` (each must be satisfied) + group `validate` |
| `CheckboxGroup` (`checkbox-group/CheckboxGroup.tsx:89-141,161-174`) | control of the representative member input | `value: string[]` | `value` filtered to members whose input is checked **and** eligible (`:not(:disabled)`, same form) | none of its own (`div role=group`) | via members | representative member's native state + `validate`; registers only when the field has a `name` |
| `Switch.Root` (`switch/root/SwitchRoot.tsx:76-77,94,159-205,233-236`) | root `<span role=switch>` | `checked` | `checked` | hidden `<input type="checkbox">` (= `validation.inputRef`); `uncheckedValue` hidden input when off | as Checkbox | native `required` + `validate` |
| `Radio.Root` (`radio/root/RadioRoot.tsx:90-96,182-224`) | — (group registers) | — | — | hidden `<input type="radio" name form value=serializeValue(value) checked required readOnly disabled id>` registered in the group's validation registry | `name=serializeValue(value)` for the checked radio (JSON for non-strings) | per group |
| `RadioGroup` (`radio-group/RadioGroup.tsx:94-102,154-169,223-246`) | control of the representative radio | `checkedValue ?? null` | `checkedValue ?? null` if some checked radio is eligible, else `null` | none of its own (`div role=radiogroup`) | via radios | representative radio's native state (`required`) + `validate`; never writes `validation.inputRef` |
| `Select.Root` (`select/root/SelectRoot.tsx:97,225-262,487-585`) | trigger element | raw `value` (item or `null`; array in multiple) | `stringifyAsValue(value)`; array of strings in multiple | hidden `<input>` (= `validation.inputRef`) with `name` (single) / nameless + per-value `<input type="hidden">` (multiple); `required` dropped once a multiple selection exists; `readOnly`, `disabled`, `autoComplete`, `form` | single: `name=stringified`; multiple: one entry per value | native `required` on hidden input + `validate` |
| `Combobox.Root` (`combobox/root/AriaCombobox.tsx:160,551-591,1569-1705`) | input element, or trigger when the input is inside the popup | `selectedValue` (raw) in single/multiple; `inputValue` in `selectionMode="none"` | stringified selected value(s); the input string in mode `none` | hidden `<input>` (= `validation.inputRef`) named unless multiple or (mode `none` and the visible input owns the form value); per-value hidden inputs in multiple; the visible `Combobox.Input` carries `name` (and native `required`) only in mode `none` outside the popup | as Select; mode `none`: the visible input's text | native `required` + `validate` |
| `Autocomplete.Root` (`autocomplete/root/AutocompleteRoot.tsx:107-125`) | as Combobox with `selectionMode="none"` | `inputValue` | the typed string | visible input carries `name` (outside popup / inline); hidden input carries it when the input is inside the popup | `name=<typed text>` | native `required` + `validate` |
| `Slider.Root` + `Slider.Thumb` (`slider/root/SliderRoot.tsx:174-191`, `slider/thumb/SliderThumb.tsx:315-460`) | shared validation input = last-mounted thumb's `<input type="range">` | clamped, sorted `number` or `number[]` | same | one `<input type="range" name form min max step value disabled>` per thumb (`validation.inputRef` merged on each) | one entry per thumb (`"25"`, `"50"`) | `validate` only (range inputs carry no constraint) |
| `NumberField.Root` + `NumberField.Input` (`number-field/root/NumberFieldRoot.tsx:112-127,500-540`, `number-field/input/NumberFieldInput.tsx:91-112`) | the visible text input (registered by `NumberField.Input`) | `value: number \| null` | same (number, never the formatted text) | hidden `<input type="number" name form value min max step required readOnly disabled>` (= `validation.inputRef`); visible `<input type="text">` has no `name` | `name=String(value)` or `""` | native `required`/`min`/`max`/`step` on hidden input + `validate` |
| `OTPField.Root` (`otp-field/root/OTPFieldRoot.tsx:124-125,163,201-205,404-457`) | first slot input | `value: string` | same | hidden `<input type="text" name form value minLength=length maxLength=length pattern required readOnly disabled autoComplete inputMode>` (= `validation.inputRef`) when `length` is a positive integer; slots have no `name` | `name=value` | native `required`/`pattern`/length + `validate` |
| `Toggle`, `ToggleGroup` | — | — | — | none | none | none |

**Table B — state flag triggers and blur boundary**

| Control | `touched` set when | `dirty` equality | `filled` predicate | `focused` target | Blur-boundary commit value (`'onBlur'` mode) |
|---|---|---|---|---|---|
| `Field.Control` (`FieldControl.tsx:89-105,130-136,144-191`) | blur; Enter keydown | `String(value) !== (initialValue ?? '')` | DOM/controlled value `!== ''` | the input | `input.value` (+ post-blur normalization, FFI-FRM-32) |
| `Checkbox.Root` (`CheckboxRoot.tsx:173-193,304-319`) | blur of root | `checked !== initialValue` (standalone only) | `checked` (standalone; group derives its own) | root | `input.checked`, or the group value when grouped |
| `CheckboxGroup` (`CheckboxGroup.tsx:125-141`) | via members | `!arraysEqual(value, initialValue ?? [])` | `value.length > 0` | members | via member blur (`groupValue`) |
| `Switch.Root` (`SwitchRoot.tsx:96-105,127-142`) | blur of root (ignored while disabled) | `checked !== initialValue` | `checked` | root | `input.checked` |
| `Radio.Root` (`RadioRoot.tsx:98-102,156-173,196-215`) | on change (selecting a radio) | — | `true` on mount if its input is checked | root (released only when ungrouped) | — |
| `RadioGroup` (`RadioGroup.tsx:171-183,230-240`) | blur leaving the group (`relatedTarget` outside) | `checkedValue !== initialValue` | `checkedValue != null` | radios (group releases on leaving) | `checkedValue` |
| `Select.Root` / `Select.Trigger` (`SelectRoot.tsx:256-262,281-313`, `SelectTrigger.tsx:145-172`) | trigger blur not into the popup; popup close by `focus-out` / `outside-press` | `isSelectedValueDirty(value, initialValue, isItemEqualToValue)` (order-sensitive for arrays) | `hasSelectedValue` (non-null and stringified `!== ''`; non-empty array) | trigger | `value` |
| `Combobox` (`AriaCombobox.tsx:838-850,1244-1252,1291-1324`, `ComboboxInput.tsx:188-222`, `ComboboxTrigger.tsx:169-191`) | input blur; trigger blur not into the popup; popup close by `focus-out`/`outside-press` when the input is inside the popup | `isSelectedValueDirty(selectedValue, …)`; mode `none`: `inputValue !== initialValue` | mode `none`: `String(inputValue) !== ''`; else selection non-empty | input or trigger | `selectedValue`, or `inputValue` in mode `none` |
| `Autocomplete` | as Combobox mode `none` | `inputValue !== initialValue` | `inputValue !== ''` | input | `inputValue` |
| `Slider` (`SliderRoot.tsx:178-191,228-247`, `SliderThumb.tsx:335-367`) | any value change through an input/keyboard; thumb blur not to a sibling thumb | `!arraysEqual` / `!==` against `initialValue` (set *after* `change`) | **never set** (Slider does not call `setFilled`) | the thumb's range input | the blurred thumb's value merged into the current values |
| `NumberField` (`NumberFieldRoot.tsx:125-127,260`, `NumberFieldInput.tsx:131-230`) | visible input blur (unless default-prevented or disabled) | `validatedValue !== initialValue` (set inside `setValue`) | `value !== null` | the visible input | the committed (parsed, clamped) value or `null` |
| `OTPField` (`OTPFieldRoot.tsx:150-152,201-205,286-298`, `OTPFieldInput.tsx:70,172-187`) | blur leaving the root | `value !== initialValue` | `value !== ''` | the focused slot (root-level flag) | the current value |

**Table C — propagation of field-level props, labels, reset**

| Control | `name` | `disabled` | `required` / `readOnly` | `Field.Label` association | Form reset |
|---|---|---|---|---|---|
| `Field.Control` | `fieldName ?? nameProp` on the element | `fieldDisabled \|\| prop` | own props, native | native `for` ↔ control id; `aria-labelledby={labelId}` | browser restores an uncontrolled value; flags unchanged |
| `Checkbox.Root` | `fieldName ?? nameProp` on hidden input (parent: none) | `fieldDisabled \|\| itemDisabled \|\| groupDisabled \|\| prop` | own props on hidden input; `aria-required`/`aria-readonly` on root | `for` ↔ hidden input id (root id when `nativeButton`); `aria-labelledby` via FFI-ALG-08; in a group, claims a `Field.Item` scope only | none |
| `CheckboxGroup` | `fieldName` (required for registration) | `fieldDisabled \|\| prop` | — | claims `null` → no `for`; `aria-labelledby={labelId}` on the group | none |
| `Switch.Root` | `fieldName ?? nameProp` on hidden input | `fieldDisabled \|\| prop` | own props | as Checkbox | none (FFI-FRM-30) |
| `Radio.Root` | group `name` on hidden input | `fieldDisabled \|\| itemDisabled \|\| groupDisabled \|\| prop` | `groupProp \|\| ownProp` | `for` ↔ hidden input id; `aria-labelledby` via FFI-ALG-08 | none |
| `RadioGroup` | `fieldName ?? nameProp` | `fieldDisabled \|\| prop` | own props; `aria-required`/`aria-disabled`/`aria-readonly` on the group | `aria-labelledby = labelId ?? legendId` | none (registration/unregistration: RDG-FRM-05; projected form value: RDG-FRM-06) |
| `Select` | `fieldName ?? nameProp` on hidden input(s) | `fieldDisabled \|\| prop` (trigger adds its own) | on hidden input; `aria-required`/`aria-readonly` on trigger | `for` ↔ trigger id (= root id); `aria-labelledby = fieldLabel ?? Select.Label` | none |
| `Combobox` / `Autocomplete` | `fieldName ?? nameProp` on hidden input or visible input (mode `none`) | `fieldDisabled \|\| prop` (input/trigger add their own) | hidden input; visible input `required` only in mode `none`; `aria-required`/`aria-readonly` on input (or trigger when input inside popup) | `for` ↔ input id (or trigger id when the input is inside the popup); `aria-labelledby = fieldLabel ?? Combobox.Label` (trigger), `fieldLabel` (input) | none |
| `Slider` | `fieldName ?? nameProp` on each range input | `fieldDisabled \|\| prop` (thumbs add their own) | — (no native constraints) | single thumb: `for` ↔ thumb input id; root/thumb `aria-labelledby = prop ?? fieldLabel ?? Slider.Label` | none |
| `NumberField` | `fieldName ?? nameProp` on hidden input | `fieldDisabled \|\| prop` | on hidden and visible input | `for` ↔ visible input id; `aria-labelledby={labelId}` | none |
| `OTPField` | `fieldName ?? nameProp` on hidden input | `fieldDisabled \|\| prop` | on hidden input and every slot | `for` ↔ first slot id; group `aria-labelledby` via FFI-ALG-08 (first slot as label source), slots inherit it | none |

Tests cited for the matrix (per control): `CheckboxRoot.test.tsx` "Form", "Field" (incl. "should receive disabled prop from Field.Root", "should receive name prop from Field.Root", "[data-touched]", "[data-dirty]", "[data-filled]", "[data-focused]", "prop: validationMode=onSubmit", "props: validationMode=onChange", "prop: validationMode=onBlur", "Field.Label", "Field.Description"); `CheckboxGroup.test.tsx` "Field", "Form values", "Field.Label", "Field.Description"; `SwitchRoot.test.tsx` "Form", "Field"; `RadioGroup.test.tsx` "Field", "Fieldset", "Form"; `RadioRoot.test.tsx` "prop: disabled"/"prop: readOnly" focused-state tests; `SelectRoot.test.tsx` "with Field.Root parent", "Form", "Field"; `ComboboxRoot.test.tsx` "Form", "Field"; `AutocompleteRoot.test.tsx` "Form", "Field"; `SliderRoot.test.tsx` "Form", "Field"; `SliderThumb.test.tsx` "does not commit field validation when moving focus between range thumbs"; `NumberFieldRoot.test.tsx` "prop: name", "Form", "Field", "hidden input"; `OTPFieldRoot.test.tsx` "Field", "Form", "[data-focused] without a blur event".

## 7. Edge cases and dev warnings

- **FFI-EDGE-01** — MUST. The missing-scope error for field parts is thrown with exactly: `Base UI: FieldRootContext is missing. Field parts must be placed within <Field.Root>.`
  Source: `internals/field-root-context/FieldRootContext.ts:74-78`. Test: none.
  Port note 2026-10-22 (PRG-EDGE-01 precedent, ADR 08 Amendments 2026-10-20): the message is ``Blazix: The `FieldRoot` context is missing. Field parts must be placed within `<FieldRoot>`.``, an `InvalidOperationException` thrown in every build; tested by the L2 port title "throws a descriptive error when a field part is rendered outside <Field.Root>".
- **FFI-EDGE-02** — MUST. In development, after paint, `Field.Label` reports an error (via the dev `error` channel, once per `nativeLabel` change, skipped when no element was rendered) when `nativeLabel` is `true` but the rendered element is not a `<label>`: `<Field.Label> expected a <label> element because the `nativeLabel` prop is true. Rendering a non-<label> disables native label association, so `htmlFor` will not work. Use a real <label> in the `render` prop, or set `nativeLabel` to `false`.`; and when `nativeLabel` is `false` but a `<label>` was rendered: `<Field.Label> expected a non-<label> element because the `nativeLabel` prop is false. Rendering a <label> assumes native label behavior while Base UI treats it as non-native, which can cause unexpected pointer behavior. Use a non-<label> in the `render` prop, or set `nativeLabel` to `true`.` The emitted message is prefixed `Base UI: ` and suffixed with an owner stack when available.
  Source: `field/label/FieldLabel.tsx:48-76`. Test: [L2] `FieldLabel.test.tsx` "does not warn by default", "does not warn when the render function returns no element", "errors if nativeLabel=true but ref is not a label", "errors if nativeLabel=false but ref is a label"; `FieldRoot.react17.test.tsx` "reports label mismatches without the owner-stack API", "reports non-native label mismatches without the owner-stack API".
  Port note 2026-10-22: the owner-stack half is React-only (ADR 06 Amendments 2026-10-18). The `form` module checks the element at attach and when `NativeLabel` changes, and C# logs the port's wording of the two messages through `DevelopmentWarnings`. A substitute that renders no element is never attached and never checked.
- **FFI-EDGE-03** — MUST. In development, `Combobox.Label` reports an error when a `Combobox.Input` exists outside the popup (so the input, not the trigger, is the form control): `<Combobox.Label> labels <Combobox.Trigger> only. When <Combobox.Input> is the form control, use a native <label> or <Field.Label> instead.`
  Source: `combobox/label/ComboboxLabel.tsx:40-53`. Test: [L2] `ComboboxRoot.test.tsx` "Combobox.Label does not label Combobox.Input and warns when input is the form control".
- **FFI-EDGE-04** — MUST. The default submitter (FFI-ALG-10) is the first `button`/`input` of submit type in `form.elements` order — including a submitter associated through `form="…"` that precedes the form in the DOM, a `<button>` with the default (implicit) type, and a *disabled* submitter (clicking it is then a no-op); `type="button"`/`type="reset"` controls are skipped; `null` when none exists.
  Source: `packages/utils/src/getDefaultFormSubmitter.ts:14-34`. Test: [L3] `getDefaultFormSubmitter.test.ts` "returns the first submit button associated with the form", "returns a disabled submit button when it is first in form.elements", "supports buttons with the default submit type", "supports input submitters from form.elements and ignores non-submit controls", "returns null when there is no default submitter".
- **FFI-EDGE-05** — MUST. `serializeValue` falls back to `String(value)` when JSON serialization throws (cyclic structures, BigInt).
  Source: `internals/serializeValue.ts:8-12`. Test: none.
- **FFI-EDGE-06** — MUST. Changing `Field.Control`'s `defaultValue` after mount MUST be forwarded to the element without a warning and recompute `filled` from the element's *current* value, which keeps a user-edited value.
  Source: `field/control/FieldControl.tsx:89-94,122`. Test: [L2] `FieldControl.test.tsx` "forwards a changing defaultValue without warning", "keeps filled state from the edited value when defaultValue changes".
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-22): the `form` module owns `defaultValue` from attach and keeps a user's edit; tested at L4 on both modes ("keeps the user's text when the default value changes").
- **FFI-EDGE-07** — MUST. Direct assignment to an uncontrolled `Field.Control`'s DOM `value` is not observed (no flag update) and MUST NOT be reverted to `defaultValue` on focus.
  Source: `field/control/FieldControl.tsx:111-122` (no value re-application). Test: [L4] `FieldRoot.test.tsx` "should not reset to defaultValue when input value is programmatically changed and then focused", "should not reset to defaultValue when input value is programmatically changed to non-empty value and then focused".
- **FFI-EDGE-08** — MUST. A control that remounts with an empty value clears `filled` post-commit, pre-paint; a remount never recaptures `initialValue` (FFI-STA-04).
  Source: `field/control/FieldControl.tsx:89-94`, `checkbox/root/CheckboxRoot.tsx:173-182`. Test: [L2] `FieldControl.test.tsx` "clears filled state when a controlled control remounts empty", "clears filled state when an uncontrolled control remounts empty"; `CheckboxRoot.test.tsx` "clears [data-filled] when a controlled checkbox remounts unchecked"; `SwitchRoot.test.tsx` "clears [data-filled] when a controlled switch remounts unchecked".
- **FFI-EDGE-09** — SHOULD. An uncontrolled `Field.Control` SHOULD NOT render the part again on keystrokes after the first (the field flags settle), and a controlled one SHOULD render the part exactly once per keystroke: the controlled echo is committed in the same render batch and does not schedule a second render (render count observed through bUnit, L2).
  Source: `field/control/FieldControl.tsx:123-143` (no state writes on the uncontrolled path beyond flag changes). Test: [L2] `FieldControl.test.tsx` "avoids rerendering for uncontrolled input changes", "renders once per keystroke for controlled input changes".
- **FFI-EDGE-10** — MUST. Imperative validation at mount time, before any control id exists, still publishes to the field (the registry update is skipped when there is no id) and renders `Field.Error`.
  Source: `field/root/useFieldValidation.ts:131-139`. Test: [L2] `FieldRoot.react17.test.tsx` "allows mount-time imperative validation before the fallback id is assigned".
  Port note 2026-10-22 (ADR 06 Amendments 2026-10-18): the cited title is React 17's id fallback, which does not arise with synchronous ids. Its behaviour is `FieldRoot.ValidateAsync()` before any control registered: the registry update is skipped and the field publishes. It renders `Field.Error`, so its translation lands with PR 1c.
- **FFI-EDGE-11** — MUST. A group whose checkbox values are duplicated across registrations updates the duplicate's registration before an ancestor's post-render work submits, so the projected value is current.
  Source: `checkbox-group/CheckboxGroup.tsx:103-123`, `internals/field-register-control/useRegisterFieldControl.ts:18-38`. Test: [L2] `CheckboxGroup.test.tsx` "updates duplicate-value registrations before a parent layout effect submits".
- **FFI-EDGE-12** — MUST. A `Field.Validity` render-prop receives `{ ...validityData, validity: combinedState, transitionStatus }` where `validity.valid` already folds in `invalid`/form errors; its identity is stable across unrelated field-state changes (focus, dirty, filled).
  Source: `field/validity/FieldValidity.tsx:18-38`. Test: [L2] `FieldValidity.test.tsx` "should pass validity data" (property `transitionStatus`); identity: none.

## 8. React-specific notes and porting considerations

Non-normative.

- **Scopes are React contexts.** `FieldRootContext`, `FormContext`, `LabelableContext` cross portal boundaries, which is why portaled fields register with the form and why `isEligibleInput` needs the explicit "no native owner and no `form` attribute" rule. A port needs an equivalent ancestor-lookup that follows the *logical* tree, not the DOM tree. Port: see decisions/04-js-interop-boundary.md §1 (field and form infrastructure is C#: "everything not listed is C#"); the native-constraint, label-discovery and submit-veto steps that need browser APIs are not assigned to any JavaScript module (§10 Q9).
- **Layout-phase registration.** Every registration (`useRegisterFieldControl`, `useLabelableId`, `useRegisteredLabelId`, `setMessageIds`) runs in `useIsoLayoutEffect`, and unregistration deliberately also runs in the layout phase so that a replacement control's registration sees a clean scope (`useLabelableId.ts:73-77`). Ports must run "after DOM update, before paint" callbacks in child-before-parent order and cleanup-before-setup order on replacement. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (row "FFI registration": on Server this post-commit, pre-paint C# state work lands after paint plus a round trip, observable only as latency of dependent attribute updates) and §Decision item 4.
- **StrictMode double effects.** Several guards exist only to survive React's effect re-run on mount: the once-only `initialValue` capture (`useFieldControlRegistration.ts:86-103`), the per-instance `Symbol()` source tokens so a re-run re-registers the *same* control, and the focus-ownership re-acquisition on effect setup (`useSetFieldFocused.ts:36-43`). A port without such re-runs still needs the *semantics* (one baseline per field; same control re-registering updates in place and keeps registry order).
- **`useValueChanged`** (`internals/useValueChanged.ts`) calls its callback in a layout effect only when the value identity changed since the previous render. That is why `clearErrors → setDirty → change` runs once per *actual* change (Slider and Number Field use a different order, FFI-EVT-02) and also for programmatic/controlled changes, and why `Select`/`Combobox` autofill relies on it to avoid validating cancelled changes. The `change` callback runs *after* the control's own `setDirty`/`setFilled` in the uncontrolled `Field.Control` path (they are called synchronously in the event handler before `validation.change`), which matters because `commit` reads the dirty latch. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 ("value-changed side effects" are post-commit, pre-paint C# state work in `OnAfterRender`; after paint plus a round trip on Server).
- **`useStableCallback`** gives `commit`, `change`, `validate`, `register`, `setFieldFocused` a stable identity; `setFieldFocused`'s identity doubles as the focus-owner token. A port needs an equivalent per-control owner token.
- **Asynchrony.** `commit` is an `async` function; everything before the first `await` runs synchronously inside the event (including the registry update), which is what lets `Form`'s submit handler inspect validity synchronously. `queueMicrotask` is used for the controlled-blur re-commit, for the checkbox Enter-to-submitter click and for Select/Combobox autofill resolution; a `0 ms` timer is used for `Field.Control` Enter (FFI-KEY-01). The `useTimeout` utility clears on unmount, so a pending debounce dies with the field. Port: see decisions/01-hosting-model-and-timing.md §Decision items 3–4 (the debounce and the `0 ms` Enter timer are C# timers because their result is state; microtasks exist only in JavaScript, constraint C2, so the three microtask steps need a JavaScript owner — §10 Q9).
- **React 17 id fallback.** `useBaseUiId`/`useId` return `undefined` during the first render on React 17 and assign `base-ui-N` in an effect; `useLabelableId` and `useRegisteredLabelId` are written so that an `undefined` id is "not yet available" (no registration) while `null` is a deliberate "no id" claim. Ports with synchronous id generation can collapse this but must keep the `null` semantics. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (label `for` and control `id` come from the scoped `base-ui-<n>` counter at first render; prerender/interactive stability per RND-DOM-29, which FFI-FRM-34/36 rely on).
- **`onChange` React quirk (facebook/react#9023).** Every native `onChange` handler first checks `event.nativeEvent.defaultPrevented` because React fires `onChange` even for prevented `input` events; the requirement (FFI-EVT-02) is simply "a default-prevented native input event does not change state".
- **`suppressHydrationWarning`** on every hidden input exists because the hidden input's `value` may differ between server and client (locale formatting, controlled values); it has no behavioral meaning.
- **`focus({ focusVisible: true })`** is a newer DOM option (Chrome 144+); ports targeting other platforms need an equivalent "show focus ring" when focusing from a label click.
- **Rendering of `Field.Error`'s content** uses React state to remember the last rendered message so the exit transition can show it; see `components/field.md`.
- **Event handler merging.** `Form` deliberately destructures `onSubmit` and calls it itself after validation rather than letting `mergeProps` chain it, which is why the consumer's `onSubmit` never runs for a blocked submission. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 3 (handler chaining); whether the submit default can be prevented conditionally on Server is open (§10 Q9).
- **Controlled flags.** `Field.Root`'s `dirty`/`touched` are "defined prop wins" per render (`dirtyProp ?? dirtyState`, FFI-API-05) with no change callback, and `invalid`/`validationMode` are plain inputs; none is a `value`/`onValueChange` pair. Port: see decisions/05-state-store-and-event-details.md §2 (controlled iff supplied at the first parameter set, `Controlled<T>`); no Base UI test switches `dirty`/`touched` between defined and undefined, and no FFI requirement depends on `Field.Control` switching between controlled and uncontrolled mid-life (the FLD-STA-12 switching clause).
- **Testing.** Form integration is observable without a browser except for native constraint state and `FormData` construction. Port: see decisions/06-testing-harness.md §Decision 1 (L2 row: form integration (FFI) is tested in bUnit); the `test/formData.ts` capture listed in UTL §8.3 becomes a harness helper, at L4 (Playwright) wherever the assertion needs the browser to build `FormData` or report `ValidityState`.

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| FFI-STR-01 | MUST | Field scope reaches all descendants incl. portals; default no-op scope outside Field.Root | 4.1 |
| FFI-STR-02 | MUST | Label/Description/Error/Validity/Item throw outside Field.Root; Control does not | 4.1 |
| FFI-STR-03 | MUST | Field.Root and Field.Item open labelable scopes; item descriptions appended after parent's | 4.1 |
| FFI-STR-04 | MUST | Form scope reaches portaled descendants | 4.1 |
| FFI-STR-05 | MUST | Default form scope: empty registry, onSubmit mode, no auto-validation | 4.1 |
| FFI-STR-06 | MUST | Disabled Fieldset disables nested Field.Root | 4.1 |
| FFI-STR-07 | MUST | One active control per field; newcomer replaces, cancels pending work | 4.1 |
| FFI-API-01 | MUST | validate return contract and normalization | 4.2 |
| FFI-API-02 | MUST | validationMode values and Field → Form → onSubmit precedence | 4.2 |
| FFI-API-03 | MUST | validationDebounceTime semantics | 4.2 |
| FFI-API-04 | MUST | errors map shape; own non-empty keys only | 4.2 |
| FFI-API-05 | MUST | dirty/touched/invalid props are controlled overrides | 4.2 |
| FFI-API-06 | MUST | Field and Form actionsRef.validate | 4.2 |
| FFI-API-07 | MUST | Form.Values built from registry with typed getValue | 4.2 |
| FFI-STA-01 | MUST | Field state fields and defaults | 4.3 |
| FFI-STA-02 | MUST | invalid and valid computation incl. disabled | 4.3 |
| FFI-STA-03 | MUST | validityData shape and initial values | 4.3 |
| FFI-STA-04 | MUST | initialValue captured once per Field.Root | 4.3 |
| FFI-STA-05 | MUST | dirty computed per change vs initialValue | 4.3 |
| FFI-STA-06 | MUST | dirty latch semantics | 4.3 |
| FFI-STA-07 | MUST | touched set at control boundary, never reset | 4.3 |
| FFI-STA-08 | MUST | filled recomputed post-commit, pre-paint | 4.3 |
| FFI-STA-09 | MUST | focused follows ownership | 4.3 |
| FFI-STA-10 | MUST | effective name = root name ?? control name | 4.3 |
| FFI-STA-11 | MUST | disabled resolution chain | 4.3 |
| FFI-DOM-01 | MUST | data-* derivation incl. valid/invalid/null | 4.4 |
| FFI-DOM-02 | MUST | hidden input attributes and visually-hidden styles | 4.4 |
| FFI-DOM-03 | MUST | `${id}-hidden-input` id when nameless | 4.4 |
| FFI-DOM-04 | MUST | Checkbox/Switch/Radio hidden input id vs nativeButton | 4.4 |
| FFI-DOM-05 | MUST | Form renders novalidate by default | 4.4 |
| FFI-DOM-06 | MUST | multiple mode: per-value hidden inputs, nameless shared input, required dropped | 4.4 |
| FFI-DOM-07 | MUST | uncheckedValue hidden input | 4.4 |
| FFI-DOM-08 | MUST | form attribute forwarded to hidden inputs | 4.4 |
| FFI-ARIA-01 | MUST | aria-describedby composition | 4.5 |
| FFI-ARIA-02 | MUST | Description/Error id publication rules | 4.5 |
| FFI-ARIA-03 | MUST | aria-invalid via getValidationProps | 4.5 |
| FFI-ARIA-04 | MUST | native label `for` from scope control id; null suppresses | 4.5 |
| FFI-ARIA-05 | MUST | aria-labelledby resolution order | 4.5 |
| FFI-ARIA-06 | MUST | discovered label gets `${controlId}-label` id | 4.5 |
| FFI-ARIA-07 | MUST | Field.Label beats component labels | 4.5 |
| FFI-ARIA-08 | MUST | label id publication/withdrawal | 4.5 |
| FFI-ARIA-09 | MUST | CheckboxGroup null claim; RadioGroup legend fallback | 4.5 |
| FFI-ARIA-10 | MUST | Combobox input inside popup gets no validation ARIA | 4.5 |
| FFI-KEY-01 | MUST | Field.Control Enter commit with implicit-submission detection | 4.6 |
| FFI-KEY-02 | MUST | Checkbox Enter clicks default submitter | 4.6 |
| FFI-KEY-03 | MUST | no other keys intercepted | 4.6 |
| FFI-PTR-01 | MUST | non-native label click focuses control with focus-visible | 4.7 |
| FFI-PTR-02 | MUST | native label double-click prevents selection | 4.7 |
| FFI-PTR-03 | MUST | nested interactive targets ignored | 4.7 |
| FFI-PTR-04 | MUST | shadow-root-aware control lookup | 4.7 |
| FFI-PTR-05 | MUST | Slider.Label custom focus target | 4.7 |
| FFI-FOC-01 | MUST | focus first invalid by document order, select inputs | 4.8 |
| FFI-FOC-02 | MUST | controlless invalid field still blocks; next focusable chosen | 4.8 |
| FFI-FOC-03 | MUST | focus on next error-map change (errors prop or key-removing clearErrors) after a passed submission | 4.8 |
| FFI-FOC-04 | MUST | groups expose representative input's control | 4.8 |
| FFI-FOC-05 | MUST | hidden input focus redirected to visible control | 4.8 |
| FFI-FOC-06 | MUST | Slider registers range input as control | 4.8 |
| FFI-EVT-01 | MUST | submit pipeline and onFormSubmit details | 4.10 |
| FFI-EVT-02 | MUST | clearErrors → setDirty → change per real change (Slider/Number Field deviations named); cancel rules (uncontrolled Field.Control still writes dirty/filled) | 4.10 |
| FFI-EVT-03 | MUST | clearErrors semantics | 4.10 |
| FFI-EVT-04 | MUST | errors prop replaces map on identity change | 4.10 |
| FFI-EVT-05 | MUST | validate called once per commit with raw value and live formValues | 4.10 |
| FFI-FRM-01 | MUST | shouldValidateOnChange definition | 4.11 |
| FFI-FRM-02 | MUST | change(): cancel, debounce, commit or revalidate | 4.11 |
| FFI-FRM-03 | MUST | onBlur commits at blur boundary | 4.11 |
| FFI-FRM-04 | MUST | submit commits all fields with dirty latch | 4.11 |
| FFI-FRM-05 | MUST | revalidate pass semantics | 4.11 |
| FFI-FRM-06 | MUST | native state read; valueMissing suppression; barred inputs | 4.11 |
| FFI-FRM-07 | MUST | validate gating; customError installation; message precedence | 4.11 |
| FFI-FRM-08 | MUST | barred inputs never receive custom validity | 4.11 |
| FFI-FRM-09 | MUST | custom-validity ownership and restoration | 4.11 |
| FFI-FRM-10 | MUST | CRLF normalization | 4.11 |
| FFI-FRM-11 | MUST | async pending publish rules | 4.11 |
| FFI-FRM-12 | MUST | commit sequence; superseded results dropped | 4.11 |
| FFI-FRM-13 | MUST | rejection keeps state and propagates | 4.11 |
| FFI-FRM-14 | MUST | async never blocks submit by itself | 4.11 |
| FFI-FRM-15 | MUST | debounce cancelled on unregister/replacement | 4.11 |
| FFI-FRM-16 | MUST | publish shape; registry updated first | 4.11 |
| FFI-FRM-17 | MUST | registry entry lifecycle | 4.11 |
| FFI-FRM-18 | MUST | entries keyed by control id, not name | 4.11 |
| FFI-FRM-19 | MUST | unnamed fields validate but are absent from values | 4.11 |
| FFI-FRM-20 | MUST | Form validate(name) targets current registration | 4.11 |
| FFI-FRM-21 | MUST | imperative validate semantics | 4.11 |
| FFI-FRM-22 | MUST | inputless fields still run validate | 4.11 |
| FFI-FRM-23 | MUST | eligibility rules | 4.11 |
| FFI-FRM-24 | MUST | representative input; per-checkbox required | 4.11 |
| FFI-FRM-25 | MUST | form error makes field invalid; default error slot | 4.11 |
| FFI-FRM-26 | MUST | form error cleared on change; validator re-run rules | 4.11 |
| FFI-FRM-27 | MUST | invalid prop folded into registry validity | 4.11 |
| FFI-FRM-28 | MUST | FormData serialization per control | 4.11 |
| FFI-FRM-29 | MUST | values object projections (eligible-only for groups) | 4.11 |
| FFI-FRM-30 | MUST | no reset handling | 4.11 |
| FFI-FRM-31 | MUST | autofill flows through the change path | 4.11 |
| FFI-FRM-32 | MUST | controlled blur normalization re-commit | 4.11 |
| FFI-FRM-33 | MUST | Field.Control value registration | 4.11 |
| FFI-FRM-34 | MUST | SSR never emits dangling associations; stable ids | 4.11 |
| FFI-FRM-35 | MUST | hidden subtrees keep the selected control id | 4.11 |
| FFI-FRM-36 | MUST | id format; explicit-id removal falls back atomically | 4.11 |
| FFI-A11Y-01 | — | Withdrawn: re-issued as FFI-FRM-34 | 4.14 |
| FFI-A11Y-02 | — | Withdrawn: re-issued as FFI-FRM-35 | 4.14 |
| FFI-A11Y-03 | — | Withdrawn: re-issued as FFI-FRM-36 | 4.14 |
| FFI-ALG-01 | MUST | commit algorithm | 5 |
| FFI-ALG-02 | MUST | native state read | 5 |
| FFI-ALG-03 | MUST | change/debounce | 5 |
| FFI-ALG-04 | MUST | eligibility and representative input | 5 |
| FFI-ALG-05 | MUST | submit and focus-first-invalid | 5 |
| FFI-ALG-06 | MUST | control-id selection | 5 |
| FFI-ALG-07 | MUST | control-id claim | 5 |
| FFI-ALG-08 | MUST | labelled-by resolution, label discovery, control lookup | 5 |
| FFI-ALG-09 | MUST | described-by composition | 5 |
| FFI-ALG-10 | MUST | default submitter | 5 |
| FFI-ALG-11 | MUST | serializeValue | 5 |
| FFI-ALG-12 | MUST | control registration | 5 |
| FFI-ALG-13 | MUST | focus ownership | 5 |
| FFI-ALG-14 | MUST | publish and combined validity | 5 |
| FFI-ALG-15 | MUST | error-slot resolution | 5 |
| FFI-ALG-16 | MUST | Field.Control Enter commit | 5 |
| FFI-EDGE-01 | MUST | missing-scope error text | 7 |
| FFI-EDGE-02 | MUST | Field.Label nativeLabel mismatch dev errors | 7 |
| FFI-EDGE-03 | MUST | Combobox.Label dev error | 7 |
| FFI-EDGE-04 | MUST | default submitter edge cases | 7 |
| FFI-EDGE-05 | MUST | serializeValue fallback | 7 |
| FFI-EDGE-06 | MUST | changing defaultValue forwarded, filled from current value | 7 |
| FFI-EDGE-07 | MUST | direct DOM value writes not reverted on focus | 7 |
| FFI-EDGE-08 | MUST | remount with empty value clears filled | 7 |
| FFI-EDGE-09 | SHOULD | render-count guarantees for Field.Control | 7 |
| FFI-EDGE-10 | MUST | mount-time imperative validation without id | 7 |
| FFI-EDGE-11 | MUST | duplicate-value registrations updated before ancestor submit | 7 |
| FFI-EDGE-12 | MUST | Field.Validity payload shape and identity | 7 |

## 10. Open questions

1. **Slider dirty ordering.** `slider/root/SliderRoot.tsx:178-191` calls `validation.change(fieldValue)` *before* `setDirty(...)`, whereas every other control sets dirty first. On the first change in `'onChange'` mode the synchronous commit therefore reads a `false` dirty latch; since range inputs never report `valueMissing` this is unobservable today, but a port that reuses the generic order for Slider would be equally conformant. Flag for `components/slider.md`.
   Resolved: slider.md verified and documented the order (SLD-FRM-04, SLD-STA-21). FFI-EVT-02 now names Slider (and Number Field, which writes `dirty` inside its value setter, `number-field/root/NumberFieldRoot.tsx:259-260`) as deviating from the common order and permits either order for Slider.
2. **Slider never sets `filled`.** `SliderRoot` does not call `setFilled` (grep: no `setFilled` in `slider/`), so a `Field.Root` around a slider never renders `data-filled`. No test asserts either behavior; documented as-is in §6 table B.
   Resolved: slider.md SLD-STA-22 (Slider never writes `filled`).
3. **Form reset vs. `Field.Control` flags.** After a native reset, an uncontrolled `Field.Control`'s DOM value is restored by the browser but `filled`/`dirty`/validity are not recomputed (no `reset` listener anywhere). Only the Switch case is tested (`SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset"). Whether a port should recompute flags on reset is a product decision; this document specifies the current (no-op) behavior (FFI-FRM-30).
   Resolved 2026-10-22: the port follows the source (no reset listener; FFI-FRM-30). Since the `form` module sets the element's `defaultValue` from `DefaultValue` (ADR 04 Amendments 2026-10-22), a native reset restores it in the DOM, and the flags are not recomputed.
4. **`CheckboxGroup` registration requires a field name** (`checkbox-group/CheckboxGroup.tsx:123`), so a nameless `<Field.Root><CheckboxGroup/></Field.Root>` with `required` members never blocks submission, unlike a nameless `RadioGroup` or `Switch` (`Form.test.tsx` "does not submit if an unnamed registered field control is invalid"). Intentional per the test "excludes registration-gated controls from onFormSubmit when their field name is removed", but the asymmetry is undocumented.
   Resolved: checkbox-group.md CKG-FRM-01 documents the name-gated registration.
5. **`validationMode` precedence** (Field over Form) is stated in both components' JSDoc (`FieldRoot.tsx:293-300`, `Form.tsx:194-201`) but has no test.
   Resolved 2026-10-22: the port follows the source. `FieldRoot.ValidationMode` is `ValidationMode?`, and `null` defers to the form scope's mode (`OnSubmit` by default, FFI-STR-05). The precedence titles with `Form` land with track 2.
6. **`FFI-DOM-02` styles** are not asserted by any test; the values are transcribed from `packages/utils/src/visuallyHidden.ts`.
   Open 2026-10-22: no Field part renders the visually-hidden input; the first control track that does (tracks 4-8) transcribes and tests the styles.
7. **Revalidate pass with no registry and no `inputRef`** (`useFieldValidation.ts:245`): a field whose active control registered no validation input (e.g. a custom control) returns early from the revalidate pass and never clears a previous error on change in `'onBlur'` mode; only the next blur/submit clears it. This follows from the code but is untested.
   Resolved 2026-10-22: the port follows the code. The revalidate pass returns when the field has no representative validation input (`FieldValidation.Commit`).
8. **OTP Field / Number Field `aria-invalid` on visible slots** uses the field scope's `invalid` flag (`NumberFieldInput.tsx:126`, `OTPFieldInput.tsx:162`) *in addition to* `getValidationProps`, so an `invalid` prop marks them `aria-invalid` even when the computed validity is neutral — consistent with FFI-STA-02 but a second code path; documented in their component docs.
9. **Browser-API steps inside a C# mechanism (port).** `decisions/04-js-interop-boundary.md` §1 assigns field and form infrastructure to C#, and `decisions/01-hosting-model-and-timing.md` §Decision items 2–3 put every browser-API read and every microtask in JavaScript, but no JavaScript module in ADR 04 §1 owns the steps of this mechanism that need the browser: reading `ValidityState`/`validationMessage`/`willValidate` and calling `setCustomValidity` (FFI-ALG-01/02, FFI-FRM-06..10), `:disabled`/form-owner eligibility (FFI-ALG-04, `field/root/useFieldValidation.ts:32-44`), native-label discovery (FFI-ALG-08, `internals/labelable-provider/useAriaLabelledBy.ts:6-51`), `select()` on focus-first-invalid (`form/Form.tsx:62-65`), and the three microtask steps (FFI-KEY-02 `checkbox/root/CheckboxRoot.tsx:354-361`, FFI-FRM-32 `field/control/FieldControl.tsx:158-168`, FFI-FRM-31 Select/Combobox autofill). Two requirements also need a synchronous verdict inside one native dispatch: FFI-EVT-01/FFI-FRM-04 (the `submit` handler validates and conditionally calls `preventDefault()`, `form/Form.tsx:109-137`) and FFI-KEY-02 (submitter click unless an ancestor prevented the keydown). ADR 04 §3's `decide()` list (Escape, drag start, composite/button key vetoes) does not include form submission, so on Server either native submission is always prevented and re-issued after C# validation, or the requirement is relaxed; neither ADR says which. Decided (2026-10-18): a `form` mechanism module owns these steps; native submit is always prevented and re-issued after C# validation (ADR 04 Amendments).
