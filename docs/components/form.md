# Form

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/form/` |
| Tests | `packages/react/src/form/Form.test.tsx` (1 file; `Form.spec.tsx` is a type-level test) |
| Docs page | `docs/src/app/(docs)/react/components/form/page.mdx` |
| Prefix | `FRM` |
| Status | approved |
| Revised | 2026-10-23 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Form is a native `<form>` element that knows which Base UI fields it contains. On submission it validates every registered field, blocks the submission and focuses the first invalid control when any fails, and otherwise hands the consumer either the native submit event (`onSubmit`) or a typed values object (`onFormSubmit`). It also accepts a map of externally produced errors (`errors`, typically from a server) and routes each entry to the `Field.Root` of the same name, where it is displayed by `Field.Error` and cleared as soon as that field's value changes. Native browser validation UI is turned off by default (`novalidate`) because the fields render their own.

The user sees nothing beyond the native form: Form adds no elements, no ARIA attributes and no styling hooks. Almost all of its behavior is the submission pipeline and the field registry specified in [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) (FFI); this document specifies the public surface of the `Form` part and cites FFI for the mechanism. Form is composed with [Field](field.md) (`Field.Root`, `Field.Control`, `Field.Error`) and any Base UI control; it builds on nothing and nothing builds on it.

## 2. Anatomy

```
<Form>                        required, 1          default element: form
  …any content: Field.Root subtrees, native controls, a submit button
```

Form has a single part. Fields are discovered by *registration* through the form scope (FFI §2), not by DOM containment: a `Field.Root` rendered through a portal outside the `<form>` element still belongs to the Form it is rendered under.

- **FRM-STR-01** — MUST. `Form` MUST render exactly one element (default `<form>`) and MUST make a *form scope* — the field registry, the current error map, `clearErrors`, the resolved `validationMode`, the submission counter and a handle to the form element — available to every descendant, including descendants rendered through a portal (FFI-STR-04).
  Source: `form/Form.tsx:104-141,157-169`, `internals/form-context/FormContext.ts:9-31`. Test: [L2] `Form.test.tsx` "keeps the registration-order focus fallback stable across disconnected trees" (portaled fields register and are focused).
- **FRM-STR-02** — MUST. A `Form` MAY contain any number of fields, including zero; with no registered field a submission passes validation immediately (FRM-EVT-01 step 3 finds nothing).
  Source: `form/Form.tsx:113-120`. Test: [L2] `Form.test.tsx` "should disable native validation if set to true (default)" (empty form renders).

Outside a `Form`, fields see the *default form scope* (empty registry, `errors = {}`, no-op `clearErrors`, `validationMode = 'onSubmit'`, counter `0`, no element; FFI-STR-05). Nested `<Form>` elements are not supported by HTML and are not handled specially (§16).

## 3. Public API

### 3.1 `Form`

**Default element:** `<form>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (the state object is empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `validationMode` | `'onSubmit' \| 'onBlur' \| 'onChange'` | `'onSubmit'` | When fields validate. A `Field.Root`'s own `validationMode` takes precedence. | FRM-API-01 |
| `errors` | `Record<string, string \| string[]>` | — (treated as `{}`) | Externally produced errors keyed by field name. | FRM-API-02 |
| `onFormSubmit` | `(formValues: FormValues, eventDetails: Form.SubmitEventDetails) => void` | — | Called after validation passes with the typed values object; the native submission is prevented when this prop is given. | FRM-API-03 |
| `onSubmit` | `(event: React.FormEvent<HTMLFormElement>) => void` (native attribute, intercepted) | — | Called after validation passes with the submit event; the native submission proceeds unless the consumer prevents it. Not listed in the API outline (native attribute). | FRM-API-04 |
| `actionsRef` | `{ current: Form.Actions \| null }` | — | Receives `{ validate(fieldName?) }`. | FRM-API-05 |
| `noValidate` | `boolean` (native attribute) | `true` | Rendered as the `novalidate` attribute; consumer may pass `false`. Not listed in the API outline (native attribute). | FRM-API-06 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; the `render` callback receives the native `<form>` attribute set (including `noValidate` and `onSubmit`) and an empty state object. | FRM-API-07 |
| *other `<form>` attributes and handlers* (`action`, `method`, `onReset`, `id`, …) | — | — | Forwarded to the form element. | FRM-API-08 |

**Consumed (not forwarded to the element):** `validationMode`, `errors`, `onSubmit` (re-attached inside the built-in handler), `onFormSubmit`, `actionsRef`, `className`, `style`, `render`.

- **FRM-API-01** — MUST. `validationMode` MUST accept exactly `'onSubmit'`, `'onBlur'` and `'onChange'`, default to `'onSubmit'`, and be published to the form scope so that every field without its own `validationMode` uses it (FFI-API-02: `Field.Root.validationMode ?? Form.validationMode ?? 'onSubmit'`).
  Source: `form/Form.tsx:25,161,181,192-202`; see [FFI-API-02](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "blocks submission on a resolved async error in $validationMode mode" (modes set on `Field.Root`); Form-level precedence: none.
- **FRM-API-02** — MUST. `errors` MUST be an object whose own enumerable keys are field names and whose values are a string or an array of strings; `undefined` (the default) and `null` MUST be treated as an empty map. A key counts as an error for a field only when the value is a non-empty string or a non-empty array (FFI-API-04); inherited properties MUST be ignored and prototype-less objects MUST be accepted.
  Source: `form/Form.tsx:26,71,162,203-208`, `internals/form-context/FormContext.ts:7`; see [FFI-API-04](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "should mark <Field.Control> as invalid and populate <Field.Error>", "focuses asynchronously replaced external errors and clears only the changed own property" (prototype-less object).
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22): `Errors` is a `FormErrors?`, an insertion-ordered map from field name to `FormErrorMessages`, which a `string` or a `string[]` converts to implicitly (`new FormErrors { ["url"] = "…", ["tags"] = new[] { "…", "…" } }`). The inherited-property and prototype-less clauses have no C# meaning, since a dictionary has no prototype chain; vacuous, as RND-EDGE-01/02 are (ADR 02 Amendments 2026-10-20). The form never modifies the consumer's instance: the keys fields clear are kept beside it until a new instance arrives.
- **FRM-API-03** — MUST. `onFormSubmit` MUST be called as `onFormSubmit(values, details)` where `values` is the values object (FRM-EVT-04) and `details` is a generic (non-cancelable) event-details object with `reason: 'none'` and `event` = the native `submit` event (EVD-API-02). The `Form` component accepts a type parameter `FormValues` (default `Record<string, any>`) that types `values`; it has no runtime effect.
  Source: `form/Form.tsx:19-21,125-136,170-176,178-179,209-214`, `internals/createBaseUIEventDetails.ts:159-174`. Test: [L2] `Form.test.tsx` "runs when the form is submitted"; `Form.spec.tsx` (typed `values.name`/`values.age`).
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22): `OnFormSubmit` is `EventCallback<FormSubmitEventArgs>`, whose `Values : FormValues` has typed accessors (`TryGetValue<T>(string, out T)`) and whose `Details : GenericEventDetails` has `EventReason.None`. There is no `Form<TValues>`, so the type check of `Form.spec.tsx` does not arise. The `NumberField` clause of "runs when the form is submitted" (`quantity: 5`) is open to M6; its `Field.Control` clauses are translated over `FieldControl`.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, re-cited): the port title "runs OnFormSubmit with the named fields' values and a prevented submit event" [L2] replaces the cited title, whose NumberField half is open to M6.
- **FRM-API-04** — MUST. The consumer's `onSubmit` MUST NOT be merged into the element's handler chain; it is consumed and invoked by the built-in submit handler only after validation has passed (FRM-EVT-02). Consequently it never runs for a blocked submission and cannot veto the built-in handler through `preventBaseUIHandler()`.
  Source: `form/Form.tsx:27,109,123`. Test: [L2] `Form.test.tsx` "does not submit if there are errors", "submits when a valid async validator is pending".
  Port note 2026-10-23: `OnSubmit` is a parameter the built-in pipeline invokes, never a forwarded handler. Blazor binds component parameters case-insensitively, so Razor's `@onsubmit` directive on `Form` sets `OnSubmit` itself with an `EventCallback<EventArgs>`, and rendering fails with Blazor's cast error (L2 port title "a Razor @onsubmit on Form binds to OnSubmit and fails, so OnSubmit is the only submit handler"). Nothing named `onsubmit` reaches the forwarded attributes, so no second Blazor handler runs for the event the `form` module owns (ADR 04 Amendments 2026-10-18 item 1).
- **FRM-API-05** — MUST. `actionsRef.current` MUST be `{ validate(fieldName?) }`: with no argument every registered field is validated in registration order; with a name only the *first* registered field whose effective name equals it is validated (FFI-API-06, FFI-FRM-20). Neither call increments the submission counter, focuses anything or fires a callback (FRM-FRM-11).
  Source: `form/Form.tsx:86-102,183-185,215-227`. Test: [L2] `Form.test.tsx` "validates the form when the `validate` method is called", "validates a field when the `validate` method is called with the field name", "targets only the current Strict Mode registration after name, id, and control replacement".
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22): Base UI's `actionsRef` is `public Task ValidateAsync(string? fieldName = null)` and `public void ClearErrors(string? name)` on `Form`, reached through `@ref`, so no actions-object identity arises. `null` or `""` validates every registered field in registration order, and a name validates only the first registered field with that effective name.
- **FRM-API-06** — MUST. `noValidate` MUST default to `true` (rendered as the `novalidate` attribute, FRM-DOM-01) and a consumer value MUST override it (FFI-DOM-05).
  Source: `form/Form.tsx:108,139`. Test: [L2] `Form.test.tsx` "should disable native validation if set to true (default)", "should enable native validation if set to false".
- **FRM-API-07** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md) with the empty state object (FRM-STA-01); a `render` callback MUST receive the attribute set of a native `<form>` (typed `React.ComponentPropsWithRef<'form'>`, so `noValidate` is `boolean | undefined`).
  Source: `form/Form.tsx:23,30,104,189-191`, `internals/types.ts:36-61`. Test: [L2] `Form.test.tsx` conformance suite "Base UI component API"; `Form.spec.tsx` (render callback `props.noValidate`).
- **FRM-API-08** — MUST. Every prop not listed as consumed MUST be forwarded to the form element, merged after the built-in `noValidate`/`onSubmit` pair so a consumer attribute of the same name wins (RND-DOM-19). In particular `action` is forwarded; because `onFormSubmit` prevents the native submission (FRM-EVT-03), an `action` only runs when the consumer uses `onSubmit` (or neither) and does not prevent the event.
  Source: `form/Form.tsx:31,139`. Test: [L2] `Form.test.tsx` conformance suite "Base UI component API" (`data-testid`, custom attributes); `action`: none (demo `form/demos/form-action`).

#### State object

| Field | Type | Meaning |
|---|---|---|
| — | — | `Form.State` is the empty object `{}`. |

- **FRM-STA-01** — MUST. The state object MUST be empty: `className`/`style`/`render` functions receive `{}` and no state data attribute is ever emitted (RND-STA-02).
  Source: `form/Form.tsx:104-141` (no `state` passed), `:187`, `internals/useRenderElement.tsx:67`. Test: [L2] `Form.test.tsx` conformance suite "Base UI component API".

#### Data attributes

None.

#### CSS variables

None.

### 3.2 Shared types

- `Form.SubmitEventReason` = `'none'` — the only literal. Source: `form/Form.tsx:178,236`, `internals/reason-parts.ts:1`.
- `Form.SubmitEventDetails` = generic-event details from [EVD](../architecture/event-details-and-reasons.md) §3.2 specialised to reason `'none'`: `{ reason: 'none'; event: Event }` — no `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed` or `trigger` (EVD-API-02). Source: `form/Form.tsx:179,237`, `internals/createBaseUIEventDetails.ts:109-112,159-174`.
- `Form.ValidationMode` = `'onSubmit' | 'onBlur' | 'onChange'`. Source: `form/Form.tsx:181,235`.
- `Form.Actions` = `{ validate: (fieldName?: string) => void }`. Source: `form/Form.tsx:183-185,234`.
- `Form.Values<FormValues = Record<string, any>>` = `FormValues` — the values object type. Source: `form/Form.tsx:239`.
- `Form.State` = `{}`; `Form.Props<FormValues>` = the props table. Source: `form/Form.tsx:187,189-228,231-233`.
- The error map type `Errors = Record<string, string | string[]>` is not exported under its own name; `Form.Props['errors']` is the public way to refer to it. Source: `internals/form-context/FormContext.ts:7`, `form/Form.tsx:208`.

## 4. State model

Form owns four pieces of state; none is exposed through the state object.

1. **Field registry** — an insertion-ordered map keyed by control id whose entries `{ name, validate(), validityData, controlRef, getValue() }` are written exclusively by fields (FFI-FRM-17/18). Form only reads it (submission, `actionsRef.validate`, focusing).
2. **Error map** — initialized from `errors`; replaced whenever the `errors` prop changes identity; individual own keys removed by `clearErrors` (called by controls on value change).
3. **Submission counter** — number of `submit` events seen; fields read it to decide whether an `'onSubmit'`-mode field has started revalidating on change (FFI-FRM-01).
4. **Submitted latch** — set when a submission passes validation; consumed by the next error-map change to focus the first invalid field (FRM-FOC-02).

There is no controlled/uncontrolled pair: `errors` is consumer-owned input that Form copies into the error map, and the map diverges from the prop only through `clearErrors` until the prop's identity changes again.

- **FRM-STA-02** — MUST. The error map MUST equal the `errors` prop (or `{}` when the prop is `undefined`/`null`) after the first render, and MUST be replaced by the new prop value post-commit, pre-paint whenever the prop's identity changes (strict inequality, UTL-STA-36); keys removed by `clearErrors` MUST stay removed until the prop identity changes again (FFI-EVT-04).
  Source: `form/Form.tsx:71-75,162`, `internals/useValueChanged.ts:10-16`. Test: [L2] `Form.test.tsx` "focuses asynchronously replaced external errors and clears only the changed own property", "runs field validation on first change after Form error is set".
- **FRM-STA-03** — MUST. The submission counter MUST start at `0`, increment by one at the start of every `submit` event handled by the Form (before any field is validated, whether or not the submission is later blocked), and never be reset while the Form is mounted.
  Source: `form/Form.tsx:39,110,164`. Test: [L2] `Form.test.tsx` "runs field validation on first change after Form error is set" (`'onSubmit'` field revalidates on change only after a submission); `FieldControl.test.tsx` "validates once when Enter implicitly submits a form". (L4 for the Enter implicit-submission citation)
- **FRM-STA-04** — MUST. The submitted latch MUST be set exactly when a submission passes validation (before `onSubmit`/`onFormSubmit` run) and cleared when it is consumed by an error-map change (FRM-FOC-02); a blocked submission MUST NOT set it.
  Source: `form/Form.tsx:38,77-84,122`. Test: [L2] `Form.test.tsx` "focuses the first invalid field only on submit", "does not swap focus immediately on change after two submissions". (L4 for the focus assertions; L2 asserts the focus-first-invalid request in the twin contract)
  Port note 2026-10-23: the L2 assertion of the focus-first-invalid request is FormSubmitTests "asks the module to focus the first invalid field on the first change of the errors after a passed submission" (`FormSubmitOptions.FocusRevision` and `Focus`).
- **FRM-STA-05** — MUST. Form MUST treat the registry as read-only and MUST iterate it in registration order for validation and for building the values object; it MUST NOT reorder, deduplicate by name or prune entries itself (fields add and remove their own entries, FFI-FRM-17).
  Source: `form/Form.tsx:34-36,91-98,113-115,129-133`. Test: [L2] `Form.test.tsx` "keeps same-name field validity scoped on submit", "unmounted fields should be removed from the form".

## 5. Rendered DOM and ARIA

### 5.1 `Form` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `novalidate` | present by default; absent when `noValidate={false}` | FRM-DOM-01 | C# |
| `data-*` (state) | none | FRM-STA-01 | C# |
| `role`, `aria-*` | none added (native `form` landmark semantics) | FRM-DOM-02 | C# |
| *consumer attributes* | forwarded | FRM-API-08 | C# |

- **FRM-DOM-01** — MUST. The form element MUST carry the `novalidate` attribute unless the consumer passes `noValidate={false}`, in which case the attribute MUST be absent and the browser's native constraint validation UI applies on submission.
  Source: `form/Form.tsx:108,139`; see [FFI-DOM-05](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "should disable native validation if set to true (default)", "should enable native validation if set to false".
- **FRM-DOM-02** — MUST. Form MUST NOT add any `role`, `aria-*`, `data-*` or `id` attribute, MUST NOT render any additional element (no hidden inputs, wrappers or live regions), and MUST expose the `<form>` element (an `HTMLFormElement` by default) to the consumer's DOM handle.
  Source: `form/Form.tsx:104-107,169`. Test: [L2] `Form.test.tsx` conformance suite "Base UI component API" (`refInstanceof: HTMLFormElement`).
  Port note 2026-10-23 (design point F6 of M2 track 2, settled by the orchestrator 2026-10-23; FRM-DOM-02, ADR 04 §3's one-way contract, ADR 02 decision 1 for the substitute): the form renders no attribute. Its handle (FRM-DOM-03) is a token it draws and never renders, under which its attach registers the element in the `form` module. Only a substituted form with no element reference (a render function, or a `Render` fragment whose root is a component) renders a generated `id` (the consumer's `id` wins), and the form's module attaches by that id: this row's "no `id`" clause is relaxed for that case only.
- **FRM-DOM-03** — MUST. Form MUST keep an internal handle to its rendered element and publish it through the form scope, so that controls can tell whether their native owner form *is* this Form (used by `Field.Control`'s Enter handling, FFI-KEY-01, and by validation-input eligibility, FFI-FRM-23).
  Source: `form/Form.tsx:37,105,159`, `field/control/FieldControl.tsx:60,177`. Test: [L4] `FieldControl.test.tsx` "validates once when Enter implicitly submits a form", "validates when Enter does not implicitly submit the form".
  Port note 2026-10-23 (design point F6, settled by the orchestrator 2026-10-23; FRM-DOM-02, ADR 04 §3's one-way contract, ADR 02 decision 1 for the substitute): the form scope publishes the token as `FormToken`, and each control sends it as `FormControlOptions.FormId`. The `form` module keeps one map from token to the form element and its enrolled controls. A control enrols by the token whichever attaches first, and resolves its owner form at each use, for the owner-form comparison and so the form reads its state at submit (FRM-EVT-01).

### 5.2 ARIA

Not applicable — Form sets no ARIA attributes; the accessible semantics are those of the native `<form>` element (or of whatever element the consumer substitutes).

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| any | Form element | Not intercepted. Implicit submission (<kbd>Enter</kbd> in a text control) is performed by the browser and reaches Form only as a native `submit` event, handled like any other submission. | FRM-KEY-01 |
| <kbd>Enter</kbd> | `Field.Control` inside this Form | Specified in FFI-KEY-01: one validation per press, deferred by a 0 ms macrotask when the control's owner form is this Form so that implicit submission (which increments the counter) wins. | — (FFI) |
| <kbd>Enter</kbd> | `Checkbox.Root` inside this Form | Specified in FFI-KEY-02: clicks the form's default submitter (FFI-ALG-10) after a microtask. | — (FFI) |

- **FRM-KEY-01** — MUST. Form MUST NOT attach keyboard handlers. A `submit` event produced by implicit submission, by activating a submit button, by `form.requestSubmit()` or by a synthetic dispatch MUST all run the same pipeline (FRM-EVT-01).
  Source: `form/Form.tsx:104-141` (only `onSubmit` is attached). Test: [L4] `Form.test.tsx` "keeps the registration-order focus fallback stable across disconnected trees" (dispatched `submit`), "does not submit if there are errors" (button click); `FieldControl.test.tsx` "validates once when Enter implicitly submits a form".

### 6.2 Pointer and touch

Not applicable — no pointer handlers; submit-button clicks are native.

### 6.3 Focus management

- **FRM-FOC-01** — MUST. When a submission is blocked, Form MUST — synchronously, inside the `submit` event, before the handler returns — focus the control of the first invalid registry entry by *document order* (registration order for controls in disconnected trees such as separate shadow roots), and, if that control is an `<input>`, select its text. An invalid entry without a usable control still blocks but is skipped for focusing; if no invalid entry has a control, focus does not move (FFI-FOC-01, FFI-FOC-02, FFI-ALG-05).
  Source: `form/Form.tsx:41-69,117-120,243-249`; see [FFI-FOC-01](../architecture/field-and-form-infrastructure.md). Test: [L4] `Form.test.tsx` "blocks submit and focuses the first invalid field across custom and native validation" (`select()` called once), "focuses the first invalid field in document order when keyed fields are reordered", "keeps the registration-order focus fallback stable across disconnected trees", "keeps focusing the first invalid field after a control value changes".
  Port note 2026-10-23 (design point F3 of M2 track 2, settled by the orchestrator 2026-10-23; FRM-EVT-01's relaxation on both modes, as FFI §4 records): with the submit always prevented (§9), the focus moves when C# answers: before paint on WebAssembly, a round trip later on Server, rather than inside the `submit` event. The document-order rule, the registration order across disconnected trees and the `select()` call on an `<input>` are the module's (L3). L4 on both modes: "blocks submit and focuses the first invalid field across custom and native validation" (`select()` once), and the port title "focuses the first invalid control by document order after the fields are reordered" (Base UI's reorder title renders Checkbox and is open to track 5).
- **FRM-FOC-02** — MUST. After a submission that passed validation, the next change of the error map (normally the consumer setting `errors` from a server response) MUST, once and after paint, focus the first invalid field as in FRM-FOC-01 (the registry already reflects the new form errors, FFI-FRM-27); later changes MUST NOT move focus until another submission passes (FFI-FOC-03).
  Source: `form/Form.tsx:77-84,122`. Test: [L4] `Form.test.tsx` "focuses asynchronously replaced external errors and clears only the changed own property", "focuses the first invalid field only on submit", "does not swap focus immediately on change after two submissions".
  Port note 2026-10-23: after a passed submission, the first change of the error map (a new `Errors` instance, or a field's `clearErrors` that removes a key) sends the module a focus request after the render (`FormSubmitOptions.FocusRevision`). The port title "focuses the first invalid field when errors arrive after a passed submission" [L4] asserts it on both modes. The cited titles render `Field.Error` and land with PR 1c.

Form has no tab-order, roving-tabindex or focus-trap behavior.

### 6.4 Dismissal

Not applicable — no popup.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onSubmit` | `(event) => void` | After all registered fields validated and none is invalid; before `onFormSubmit` | — (native event) | native `preventDefault()` only |
| `onFormSubmit` | `(values, details) => void` | After `onSubmit`, same conditions; native default already prevented | `'none'` | no (generic details) |
| `actionsRef.current.validate` | `(fieldName?) => void` | On demand (imperative) | — | — |

- **FRM-EVT-01** — MUST. On every native `submit` event the Form MUST run, in order: (1) increment the submission counter; (2) call `validate()` on every registry entry in registration order, which commits each field's current value with its dirty latch set (FFI-FRM-04) and synchronously updates the entry's validity; (3) if any entry's validity is now `false`, prevent the event's default, focus per FRM-FOC-01 and stop — no callback fires; (4) otherwise set the submitted latch; (5) call `onSubmit(event)` if given; (6) if `onFormSubmit` is given, prevent the default, build the values object (FRM-EVT-04) and call it (FFI-EVT-01, FFI-ALG-05). Server: relaxed, see §9.
  Source: `form/Form.tsx:109-137`; see [FFI-EVT-01](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "does not submit if there are errors", "runs when the form is submitted", "does not run when the form is invalid". (L4 for the native default-prevention and focus steps)
  Port note 2026-10-23 (design points F1 and F2 of M2 track 2, settled by the orchestrator 2026-10-23; ADR 04 Amendments 2026-10-21, ADR 16 rule 6): the `form` module prevents every native `submit` and reports it, with the value and native validity it reads from every control enrolled on the form in the submit task, through the invocation `OnSubmitRequest`. C# applies each state to its field (FFI-FRM-04, FFI-EDGE-07), runs steps (1)–(6) and answers `submit`, `blocked` (with the invalid fields' control ids) or `handled`. The module then re-issues the submission with `requestSubmit(submitter)` behind a one-shot pass flag, or focuses the first invalid control (FRM-FOC-01). Asserted on both modes at L4 (`form.spec.ts` "prevents the native submit and re-issues it with the same submitter once C# answers"). "does not submit if there are errors" and "does not run when the form is invalid" render `Field.Error` and land with PR 1c.
  Port note 2026-10-23 (M2 track 6, settled by the orchestrator 2026-10-23 (F2/DP6, ADR 16 rule 6)): step (2) first reports the state read from each enrolled control to the field entry keyed by that element's id (`ApplyState`), and otherwise to the validation input registered under that id (`FieldRegistry.SetInput`): the hidden inputs of checkbox and radio controls, whose field entry is keyed by a root or, for a group, by no element. A native-button box renders no id on its input, so its input keeps its last report.
- **FRM-EVT-02** — MUST. `onSubmit` MUST be called with the submit event exactly once per passed submission and never for a blocked one; the event's default is not prevented on the consumer's behalf, so native submission (page navigation, `action`) proceeds unless the consumer prevents it or `onFormSubmit` is also given. Server: native submit is always prevented by the `form` module and re-issued after C# validation, see ADR 04 Amendments.
  Source: `form/Form.tsx:122-123`. Test: [L2] `Form.test.tsx` "submits when a valid async validator is pending" (called once), "does not submit if an unnamed registered field control is invalid" (not called), "unmounted fields should be removed from the form". (L4 for the native-submission clause)
  Port note 2026-10-23 (design point F4 of M2 track 2, settled by the orchestrator 2026-10-23; the ADR 08 Amendments 2026-10-22 table delegated the member): Base UI's `preventDefault()` in `onSubmit` is `FormSubmitEventArgs.PreventNativeSubmit()`: the module then does not re-issue the submission. `OnSubmit` receives the module's snapshot of the submit event, whose default the module has already prevented.
- **FRM-EVT-03** — MUST. `onFormSubmit` MUST be called after `onSubmit`, with the native submit event's default already prevented (`details.event.defaultPrevented === true` inside the callback), `details.reason === 'none'`, and `details.event` the *native* event (EVD-API-05). Server: native submit is always prevented by the `form` module and re-issued after C# validation, see ADR 04 Amendments.
  Source: `form/Form.tsx:125-136`. Test: [L2] `Form.test.tsx` "runs when the form is submitted" (`eventDetails.event.defaultPrevented === true`); `SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit" (`reason: 'none'`). (L4 for the native `defaultPrevented` clause)
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, re-cited): the `SliderRoot.test.tsx` clause is open to M5. "runs when the form is submitted" is translated over `FieldControl` as the port title "runs OnFormSubmit with the named fields' values and a prevented submit event" [L2], whose NumberField half is open to M6. At L4, "passes the values to OnFormSubmit and never lets the browser submit" asserts on both modes that the browser never submits.
- **FRM-EVT-04** — MUST. The values object passed to `onFormSubmit` MUST be built at call time from the registry in registration order as `{ [entry.name]: entry.getValue() }` for every entry whose name is non-empty; values are the controls' typed projections (strings for `Field.Control`, numbers for `NumberField`, booleans, arrays, `null`), not `FormData` strings; unnamed, unmounted and disabled fields are absent (FFI-API-07, FFI-FRM-29).
  Source: `form/Form.tsx:128-133`. Test: [L2] `Form.test.tsx` "runs when the form is submitted" (`{ username: 'alice132', quantity: 5 }`), "excludes disabled fieldset fields from validation and onFormSubmit values", "clears invalid attributes when a field control becomes disabled" (`{}`), "re-registers field controls when they become enabled again".
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22): the typed-number clause (`quantity: 5`) needs NumberField and is open to M6. The `FieldControl` clauses are green at L2 (FormSubmitTests: the values titles and the disabled and fieldset titles).
- **FRM-EVT-05** — MUST. No callback exists for a blocked submission; the only observable effects are the prevented default, the fields' published validity (`aria-invalid`, `data-invalid`, `Field.Error`) and the focus move of FRM-FOC-01.
  Source: `form/Form.tsx:117-120`. Test: [L2] `Form.test.tsx` "does not run when the form is invalid", "does not submit if there are errors". (L4 for the prevented-default and focus clauses)
- **FRM-EVT-06** — MUST. `clearErrors(name)` (published through the form scope and invoked by controls on every actual value change, FFI-EVT-02) MUST remove exactly the own property `name` from the error map, MUST be a no-op when `name` is empty/undefined or not an own key, and MUST apply correctly when several fields clear in the same update (FFI-EVT-03).
  Source: `form/Form.tsx:143-155`. Test: [L2] `Form.test.tsx` "removes errors upon change", "removes errors for every field that changes within a single commit", "focuses asynchronously replaced external errors and clears only the changed own property".
- **FRM-EVT-07** — MUST. The details object given to `onFormSubmit` MUST NOT expose `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed` or `trigger`; `onFormSubmit` cannot cancel anything because the native default is already prevented (EVD-API-02).
  Source: `form/Form.tsx:135,179`, `internals/createBaseUIEventDetails.ts:159-174`. Test: none.

`validationMode` fires nothing itself; `actionsRef.validate` publishes validity to the fields but fires no Form callback (FRM-FRM-11).

## 8. Form integration

Form *is* the form integration. The mechanism — registration, validation pipeline, serialization, focus — is specified in [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md); the requirements below state what a consumer of `Form` can observe and cite the FFI IDs that implement them.

**Validation on submit**

- **FRM-FRM-01** — MUST. Submission MUST validate every registered field regardless of its `validationMode` (including `'onBlur'` fields whose validator depends on other fields' values, which are read live at that moment, FFI-EVT-05), and a field that is invalid for any reason — native constraint, `validate` result, `invalid` prop, or a form error for its name — MUST block the submission (FFI-FRM-04, FFI-FRM-27).
  Source: `form/Form.tsx:113-120`. Test: [L2] `Form.test.tsx` "re-runs an onBlur cross-field validator on submit", "does not submit when invalid prop remains true even if validate returns null", "blocks submit and focuses the first invalid field across custom and native validation". (L4 for the native-constraint clause)
- **FRM-FRM-02** — MUST. Only the *synchronously* published validity is inspected: a pending asynchronous validator MUST NOT block submission (the form submits and the stale earlier error is retired to neutral), whereas an already resolved asynchronous error (`'onBlur'`/`'onChange'` modes) MUST block until it is retired (FFI-FRM-14).
  Source: `form/Form.tsx:112-118`; see [FFI-FRM-14](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "submits when a valid async validator is pending", "retires a stale async validation result on the next submit", "blocks submission on a resolved async error in $validationMode mode".
- **FRM-FRM-03** — MUST. Registry entries are keyed by control id, so two fields sharing a `name` MUST be validated and reported independently (FFI-FRM-18); a registered field without a name MUST still block submission and be focused, while being absent from the values object (FFI-FRM-19).
  Source: `form/Form.tsx:113-133`. Test: [L2] `Form.test.tsx` "keeps same-name field validity scoped on submit", "does not submit if an unnamed registered field control is invalid", "clears invalid state for an unnamed registered field control on change".
- **FRM-FRM-04** — MUST. Fields that unmount, become disabled (directly, through `Field.Root`, or through an enclosing `Fieldset.Root`), or lose their active control MUST leave the registry (FFI-FRM-17, FFI-STR-07) and therefore MUST NOT be validated on submit nor appear in the values object; re-enabling MUST re-register them so they validate and submit again. Server: relaxed, see §9.
  Source: `form/Form.tsx:113-133`; see [FFI-FRM-17](../architecture/field-and-form-infrastructure.md), [FFI-STR-06](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "unmounted fields should be removed from the form", "excludes disabled fieldset fields from validation and onFormSubmit values", "clears invalid UI when a fieldset field becomes disabled", "clears invalid attributes when a field control becomes disabled", "re-registers field controls when they become enabled again", "removes the previous registered field id when another control takes over".

**External (server) errors**

- **FRM-FRM-05** — MUST. An entry `errors[name]` with a non-empty value MUST make the `Field.Root` whose effective name (FFI-STA-10) is `name` invalid: `data-invalid` on its parts, `aria-invalid="true"` on its control, and the message rendered by a `Field.Error` without a specific `match` (string as text; array of several as a list; single-element array as text), taking precedence over client-side messages (FFI-FRM-25). Keys that match no registered field have no effect.
  Source: `form/Form.tsx:162`, `field/root/FieldRoot.tsx:94-97`; see [FFI-FRM-25](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "should mark <Field.Control> as invalid and populate <Field.Error>", "should not mark <Field.Control> as invalid if no error is provided".
- **FRM-FRM-06** — MUST. A form error for a field MUST be cleared from the error map as soon as that field's value changes (FRM-EVT-06 via FFI-EVT-02), leaving other keys intact; afterwards, if change-validation is active for the field (`'onChange'`, or `'onSubmit'` after a submission) its `validate` MUST run on that change and its verdict replaces the server error, while in `'onBlur'` mode the error is cleared without validating until the next blur (FFI-FRM-26).
  Source: `form/Form.tsx:143-155`; see [FFI-FRM-26](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "removes errors upon change", "runs field validation on first change after Form error is set", "runs field validation on change when invalid prop is true and validationMode is onChange", "does not run field validation on change for onBlur mode when invalid prop is true".
- **FRM-FRM-07** — MUST. A form error for a field that is disabled MUST keep that field marked invalid (`data-invalid`), but the disabled field is unregistered and so neither blocks submission nor is focused (FFI-STA-02, FFI-FRM-17).
  Source: see [FFI-STA-02](../architecture/field-and-form-infrastructure.md). Test: [L2] `FieldRoot.test.tsx` "keeps a disabled field with form errors marked invalid".

**Values and native submission**

- **FRM-FRM-08** — MUST. Form MUST NOT alter native form data: `new FormData(form)` inside `onSubmit` MUST reflect exactly the native controls and form-participation inputs rendered by the fields (FFI-FRM-28), and Form itself contributes no entries.
  Source: `form/Form.tsx:104-141` (no inputs rendered). Test: [L4] `Form.test.tsx` "focuses the first invalid field only on submit" (`formData.get('name')` inside `onSubmit`), "runs field validation on first change after Form error is set".
- **FRM-FRM-09** — MUST. A field rendered outside the `<form>` element (portal) but inside the `Form` component MUST register, validate and submit through this Form; its native inputs are eligible for validation only per FFI-FRM-23 (no foreign owner form, or `form` attribute pointing at this form).
  Source: see [FFI-STR-04](../architecture/field-and-form-infrastructure.md). Test: [L2] `Form.test.tsx` "keeps the registration-order focus fallback stable across disconnected trees"; `CheckboxGroup.test.tsx` "includes a group fully portaled outside the form element". (L4 for the native-input eligibility clause)
- **FRM-FRM-10** — MUST. Form MUST NOT listen for the `reset` event; a native reset restores only browser-managed values and leaves field flags and the error map untouched (FFI-FRM-30).
  Source: `form/Form.tsx:104-141` (no `onReset`). Test: [L4] `SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset".

**Imperative validation**

- **FRM-FRM-11** — MUST. `actionsRef.current.validate()` MUST validate the targeted fields exactly as a submission does (dirty latch set, validity published so `Field.Error` renders) but MUST NOT increment the submission counter, MUST NOT focus any control, MUST NOT prevent or trigger a submission, and MUST NOT fire `onSubmit`/`onFormSubmit`. Consequently an `'onSubmit'`-mode field validated this way does not start revalidating its custom errors on change until a real submission occurs (FFI-FRM-01; `valueMissing` still clears through the revalidate pass, FFI-FRM-05).
  Source: `form/Form.tsx:86-102` (no counter, focus or callbacks). Test: [L2] `Form.test.tsx` "validates the form when the `validate` method is called" (two errors rendered, nothing submitted), "validates a field when the `validate` method is called with the field name"; counter/focus assertions: none.
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22, re-cited): the cited titles render NumberField and `Field.Error`. The port title "validates every registered field in registration order, or only the first with a name" [L2] covers the targets, and its translation of the cited titles lands with PR 1c. `ValidateAsync` neither counts a submission, focuses nor runs a callback (FormTests).
- **FRM-FRM-12** — MUST. `validate(fieldName)` MUST resolve the name against the *current* registry only: a field that was renamed, unmounted or replaced MUST no longer be reachable under its old name, and a replacement control registered under the same name MUST be the one validated (FFI-FRM-20).
  Source: `form/Form.tsx:89-93`. Test: [L2] `Form.test.tsx` "targets only the current Strict Mode registration after name, id, and control replacement".

## 9. Lifecycle, mounting and animation

Not applicable — Form is always mounted while rendered and has no transition status. Its only timed behaviors are: error-map replacement post-commit, pre-paint (FRM-STA-02) and the post-submission focus after paint (FRM-FOC-02); both are stated where they occur.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- FRM-EVT-01 — the native `submit` is always prevented by the `form` module (ADR 04 Amendments 2026-10-18); C# runs steps (1)–(6) and, when the submission passes and neither the consumer nor `onFormSubmit` prevented it, re-issues the native submission; a port relaxation on both render modes, not a timing row.
- FRM-FRM-04 — FFI registration row (post-commit, pre-paint C# state work): on Server a field's removal from and re-entry into the registry lands after paint plus a round trip; observable only as latency of dependent attribute updates.
- FRM-EVT-02, FRM-EVT-03 (added 2026-10-23) — the same port relaxation as FRM-EVT-01: `OnSubmit` and `OnFormSubmit` see the module's snapshot of a submit event it already prevented, and the browser submits only when the module re-issues the submission.
- FRM-FOC-01 (added 2026-10-23, design point F3 of M2 track 2, settled by the orchestrator 2026-10-23; no ADR 01 row, since the relaxation is the port's on both modes) — under FRM-EVT-01's relaxation the blocked submission's focus moves when C# answers: before paint on WebAssembly, a round trip later on Server.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` | [rendering-and-composition](../architecture/rendering-and-composition.md) | Tag `form`; no state (empty object); `ref: [consumer handle, internal element handle]`; props `[ { noValidate: true, onSubmit }, consumerProps ]`. (`Form.tsx:104-141`) |
| Form scope (`FormContext`) | [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) §2, FFI-STR-04/05 | Provides `{ elementRef, formRef (registry), validationMode, errors: errorMap ?? {}, clearErrors, submitCountRef }`. (`Form.tsx:157-169`) |
| Submission pipeline / focus-first-invalid | FFI-EVT-01, FFI-FOC-01…03, FFI-ALG-05 | Implemented in `Form.tsx:41-84,109-137,243-249`; this document's FRM-EVT-01, FRM-FOC-01/02 cite it. |
| Field registry entries | FFI-FRM-17…21, FFI-ALG-12 | Written by `Field.Root` via control registration; Form reads `name`, `validate`, `validityData.state.valid`, `controlRef`, `getValue`. |
| Generic event details | [event-details-and-reasons](../architecture/event-details-and-reasons.md) EVD-API-02, EVD-API-05 | `createGenericEventDetails('none', event.nativeEvent)` for `onFormSubmit`. (`Form.tsx:135`) |
| `useValueChanged` | [shared-utilities](../architecture/shared-utilities.md) UTL-STA-36 | Detects identity changes of the `errors` prop. (`Form.tsx:73-75`) |
| `useStableCallback` | [shared-utilities](../architecture/shared-utilities.md) UTL-STA-16 / §8 | `focusFirstInvalid` and `clearErrors` have stable identity (so the scope value only changes with the error map or `validationMode`). (`Form.tsx:41,143`) |
| Default submitter | FFI-ALG-10, FFI-EDGE-04 | Not used by Form itself; used by `Checkbox.Root` Enter handling to submit *this* form. |

## 12. Edge cases, invariants and dev warnings

Form emits **no** dev-only warnings or errors; the only thrown error reachable through it is the RND substitution error for an invalid `render` element (`internals/useRenderElement.tsx:187`, owned by RND).

- **FRM-EDGE-01** — MUST. `actionsRef.current.validate('')` (empty string) MUST behave like `validate()` with no argument and validate every registered field, because an empty name is treated as "no name".
  Source: `form/Form.tsx:90`. Test: none.
- **FRM-EDGE-02** — MUST. `validate(name)` with a name matching no registered field MUST be a silent no-op (no error, no validation).
  Source: `form/Form.tsx:91-93`. Test: [L2] `Form.test.tsx` "targets only the current Strict Mode registration after name, id, and control replacement" (`validate('initial')` after rename and `validate('current')` after unmount validate nothing).
- **FRM-EDGE-03** — MUST. `errors` set to `undefined` or `null` MUST be treated as an empty map: no field is marked invalid and nothing is rendered by `Field.Error` on that account.
  Source: `form/Form.tsx:162`. Test: [L2] `Form.test.tsx` "should not mark <Field.Control> as invalid if no error is provided" (prop omitted), "focuses asynchronously replaced external errors and clears only the changed own property" (initially `undefined`).
- **FRM-EDGE-04** — MUST. A submission with no registered fields MUST pass immediately: `onSubmit` and `onFormSubmit` (with `{}`) are called and nothing is focused.
  Source: `form/Form.tsx:113-120`. Test: [L2] `Form.test.tsx` "clears invalid attributes when a field control becomes disabled" (only field disabled → `onFormSubmit({})`).
- **FRM-EDGE-05** — MUST. A blocked submission in which the first invalid entries have no usable control (e.g. a group whose inputs are all unmounted) MUST still be blocked, and focus MUST move to the first invalid entry that *does* have a control (FFI-FOC-02).
  Source: `form/Form.tsx:49-68`. Test: [L4] `CheckboxGroup.test.tsx` "focuses a later invalid field when an inputless group is invalid without a control", "focuses a later invalid field when every checkbox in an invalid group is disabled".
- **FRM-EDGE-06** — MUST. When `Form` is substituted with an element that is not a `<form>`, the built-in handler is still attached as that element's `submit` handler and `novalidate` is still emitted; but because the element is not a form, controls have no native owner form, `Field.Control`'s Enter handling takes the "foreign form" path (immediate commit, FFI-KEY-01), and no native submission can occur. Registration, `errors` and `actionsRef` keep working.
  Source: `form/Form.tsx:104-141`, `field/control/FieldControl.tsx:176-189`. Test: [L2] `Form.test.tsx` conformance suite "Base UI component API" (`div` substitution renders). (L4 for the Enter-handling clause)
- **FRM-EDGE-07** — MUST. Repeated submissions MUST be independent: each one re-validates all fields with their *current* values and focuses the first *currently* invalid control (which may differ from the previous one), and a submission that passes after earlier blocked ones re-arms the post-submission focus (FRM-FOC-02).
  Source: `form/Form.tsx:109-122`. Test: [L2] `Form.test.tsx` "focuses the first invalid field only on submit", "keeps focusing the first invalid field after a control value changes". (L4 for the focus clause)

**Behavior under `disabled`.** Form has no `disabled` prop; disabling is done per field or through `Fieldset.Root` (FRM-FRM-04). A `<form>` cannot be disabled natively.

**Nested forms / RTL / server rendering.** Nested `Form`s are not handled (§16). Nothing is direction-dependent. Server-rendered markup is identical to the client's first render (`novalidate` plus forwarded attributes); the registry is empty until fields register on the client.

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. One ID is introduced here for the shared conformance suite.

### `Form.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLFormElement`, default `render`, default `testRenderPropWith: 'div'`) | FRM-DOM-04, FRM-API-07, FRM-API-08, FRM-STA-01, FRM-DOM-02, FRM-EDGE-06 |
| "does not submit if there are errors" | FRM-EVT-01, FRM-EVT-05, FRM-API-04 |
| "blocks submit and focuses the first invalid field across custom and native validation" | FRM-FOC-01, FRM-FRM-01, FRM-EVT-05 |
| "keeps focusing the first invalid field after a control value changes" | FRM-FOC-01, FRM-EDGE-07 |
| "keeps the registration-order focus fallback stable across disconnected trees" | FRM-STR-01, FRM-FOC-01, FRM-FRM-09, FRM-KEY-01 |
| "focuses the first invalid field in document order when keyed fields are reordered" | FRM-FOC-01 |
| "submits when a valid async validator is pending" | FRM-FRM-02, FRM-EVT-02, FRM-API-04 |
| "re-runs an onBlur cross-field validator on submit" | FRM-FRM-01 |
| "retires a stale async validation result on the next submit" | FRM-FRM-02 |
| "blocks submission on a resolved async error in $validationMode mode" (onBlur / onChange) | FRM-FRM-02, FRM-API-01 |
| "does not submit if an unnamed registered field control is invalid" | FRM-FRM-03, FRM-EVT-02 |
| "clears invalid state for an unnamed registered field control on change" | FRM-FRM-03 |
| "keeps same-name field validity scoped on submit" | FRM-FRM-03, FRM-STA-05 |
| "removes the previous registered field id when another control takes over" | FRM-FRM-04 |
| "unmounted fields should be removed from the form" | FRM-FRM-04, FRM-STA-05, FRM-EVT-02 |
| "excludes disabled fieldset fields from validation and onFormSubmit values" | FRM-FRM-04, FRM-EVT-04 |
| "clears invalid UI when a fieldset field becomes disabled" | FRM-FRM-04 |
| "clears invalid attributes when a field control becomes disabled" | FRM-FRM-04, FRM-EVT-04, FRM-EDGE-04 |
| "re-registers field controls when they become enabled again" | FRM-FRM-04, FRM-EVT-04 |
| prop: errors — "should mark <Field.Control> as invalid and populate <Field.Error>" | FRM-API-02, FRM-FRM-05 |
| prop: errors — "should not mark <Field.Control> as invalid if no error is provided" | FRM-FRM-05, FRM-EDGE-03 |
| prop: errors — "focuses asynchronously replaced external errors and clears only the changed own property" | FRM-API-02, FRM-STA-02, FRM-FOC-02, FRM-EVT-06, FRM-EDGE-03 |
| prop: errors — "focuses the first invalid field only on submit" | FRM-STA-04, FRM-FOC-02, FRM-FRM-08, FRM-EDGE-07 |
| prop: errors — "does not swap focus immediately on change after two submissions" | FRM-STA-04, FRM-FOC-02 |
| prop: errors — "removes errors upon change" | FRM-EVT-06, FRM-FRM-06 |
| prop: errors — "runs field validation on first change after Form error is set" | FRM-STA-02, FRM-STA-03, FRM-FRM-06, FRM-FRM-08 |
| prop: errors — "runs field validation on change when invalid prop is true and validationMode is onChange" | FRM-FRM-06 |
| prop: errors — "does not run field validation on change for onBlur mode when invalid prop is true" | FRM-FRM-06 |
| prop: errors — "removes errors for every field that changes within a single commit" | FRM-EVT-06 |
| prop: onFormSubmit — "runs when the form is submitted" | FRM-API-03, FRM-EVT-01, FRM-EVT-03, FRM-EVT-04 |
| prop: onFormSubmit — "does not run when the form is invalid" | FRM-EVT-01, FRM-EVT-05 |
| "does not submit when invalid prop remains true even if validate returns null" | FRM-FRM-01 |
| prop: noValidate — "should disable native validation if set to true (default)" | FRM-API-06, FRM-DOM-01, FRM-STR-02 |
| prop: noValidate — "should enable native validation if set to false" | FRM-API-06, FRM-DOM-01 |
| prop: actionsRef — "validates the form when the `validate` method is called" | FRM-API-05, FRM-FRM-11 |
| prop: actionsRef — "validates a field when the `validate` method is called with the field name" | FRM-API-05, FRM-FRM-11 |
| prop: actionsRef — "targets only the current Strict Mode registration after name, id, and control replacement" | FRM-API-05, FRM-FRM-12, FRM-EDGE-02 |

### `Form.spec.tsx` (type-level)

| Check | Requirements |
|---|---|
| `Form<Values>` types `onFormSubmit`'s `values` (`name: string`, `age: number`; unknown key is a type error) | FRM-API-03 |
| `render` callback receives native form props (`props.noValidate: boolean \| undefined`) | FRM-API-07 |

### Tests in other suites that exercise Form

| Test | Requirements |
|---|---|
| `FieldControl.test.tsx` "validates once when Enter implicitly submits a form" / "validates when Enter does not implicitly submit the form" | FRM-DOM-03, FRM-STA-03, FRM-KEY-01 |
| `SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit" | FRM-EVT-03 |
| `FieldRoot.test.tsx` "keeps a disabled field with form errors marked invalid" | FRM-FRM-07 |
| `CheckboxGroup.test.tsx` "includes a group fully portaled outside the form element" / "focuses a later invalid field when an inputless group is invalid without a control" / "focuses a later invalid field when every checkbox in an invalid group is disabled" | FRM-FRM-09, FRM-EDGE-05 |
| `SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset" | FRM-FRM-10 |

- **FRM-DOM-04** — MUST. Form MUST pass the shared conformance suite of [RND §4.9](../architecture/rendering-and-composition.md) (RND-DOM-36…48): custom attributes forwarded to the default `<form>` and to a substituted `div` (element and callback forms), `style` from the part and from the substitute both applied, the DOM handle resolving to an `HTMLFormElement`, wrapper substitution allowed, both DOM handles receiving the element, and `className` string/function merged with the substitute's class. Form does not accept `nativeButton`, so RND-DOM-49 does not apply.
  Source: `test/describeConformance.tsx:55-78`. Test: [L2] `Form.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Scope is a React context** (`FormContext`) with a *default value* that is itself a complete no-op scope (`internals/form-context/FormContext.ts:33-46`), which is why fields never need to check for a missing Form. The context value is memoized on `(validationMode, errorMap, clearErrors)`; the registry, element handle and counter are mutable ref objects so that updating them does not re-render the tree. A port should keep the registry and counter as shared mutable state and only notify fields when the error map or mode changes. Port: see decisions/04-js-interop-boundary.md §1 (form infrastructure is C#) and decisions/05-state-store-and-event-details.md §1 (fixed cascaded scope; parts subscribe).
- **`errors` is copied into state** and compared by identity (`useValueChanged`, a layout effect). Passing a new object literal on every render therefore resets `clearErrors` deletions on every render; consumers are expected to keep the object stable (the demos hold it in state). The copy also means the first render after a prop change still shows the *previous* map until the layout effect's synchronous re-render — not observable before paint. Port: see decisions/05-state-store-and-event-details.md §1 (prop-to-store sync in `OnParametersSet`, so the new map is in place before the render that follows the parameter change).
- **Post-submission focus is a passive effect** (`React.useEffect` on `[errors]`), i.e. after paint; the fields' registry updates that fold the new form errors into `validityData` run in layout effects, so by the time the effect runs the registry is current. The effect fires for *any* error-map state change, including deletions by `clearErrors` (see §16). Port: see decisions/01-hosting-model-and-timing.md §Decision (timing table, "after paint").
- **`onSubmit` is deliberately not chained through `mergeProps`** (`Form.tsx:27,123`); the consumer's handler is called by the built-in one. In the `render` callback form the consumer receives the built-in `onSubmit` in the props set and must spread it onto the element for the pipeline to work. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 (the part invokes the chain itself; `preventDefault` is not on the C# surface) — the conditional prevention of FRM-EVT-01 is open, §16 item 9.
- **`event` types.** `onSubmit` receives the framework's synthetic event (`event as any`), while `onFormSubmit` receives `event.nativeEvent`. `defaultPrevented` on the synthetic event mirrors the native one, which is what the test asserts. Port: see decisions/04-js-interop-boundary.md §3 (`details.event` is a snapshot record) and decisions/05-state-store-and-event-details.md §3 (`GenericEventDetails`).
- **Synchronous validation only.** `field.validate()` is `async` internally but its synchronous prefix publishes the registry verdict before the first `await` (FFI §8 "Asynchrony"); the Form reads the registry immediately after the loop. A port whose validators are asynchronous must preserve a synchronous "current verdict" per field for the submit decision. Port: see decisions/04-js-interop-boundary.md §1 (the validation pipeline is C#).
- **StrictMode.** Double effect invocation re-registers each field under the same id (updating in place); `Form.test.tsx` "targets only the current Strict Mode registration…" relies on stale registrations being removed in the layout phase. No Form code is StrictMode-specific.
- **React 17.** No version-specific behavior in `Form.tsx`; id generation differences live in the fields. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| FRM-STR-01 | MUST | Single `<form>` element; form scope to all descendants incl. portals | 2 |
| FRM-STR-02 | MUST | Any number of fields; zero fields submits immediately | 2 |
| FRM-API-01 | MUST | `validationMode` values, default `'onSubmit'`, Field precedence | 3.1 |
| FRM-API-02 | MUST | `errors` shape; `undefined`/`null` → empty; own non-empty keys only | 3.1 |
| FRM-API-03 | MUST | `onFormSubmit(values, details)`; `FormValues` type parameter | 3.1 |
| FRM-API-04 | MUST | `onSubmit` consumed, called only after validation passes | 3.1 |
| FRM-API-05 | MUST | `actionsRef.validate(fieldName?)` semantics | 3.1 |
| FRM-API-06 | MUST | `noValidate` default `true`, consumer override | 3.1 |
| FRM-API-07 | MUST | `className`/`style`/`render` per RND; callback gets native form props | 3.1 |
| FRM-API-08 | MUST | Other attributes forwarded; consumer wins; `action` interplay | 3.1 |
| FRM-STA-01 | MUST | Empty state object; no state data attributes | 3.1 |
| FRM-STA-02 | MUST | Error map init/replacement on identity change; deletions persist | 4 |
| FRM-STA-03 | MUST | Submission counter starts 0, increments per submit, never reset | 4 |
| FRM-STA-04 | MUST | Submitted latch set on passed submission, consumed by next map change | 4 |
| FRM-STA-05 | MUST | Registry read-only for Form; iterated in registration order | 4 |
| FRM-DOM-01 | MUST | `novalidate` present by default; absent with `noValidate={false}` | 5.1 |
| FRM-DOM-02 | MUST | No role/aria/data/id attributes; no extra elements; `HTMLFormElement` handle | 5.1 |
| FRM-DOM-03 | MUST | Internal element handle published to scope for owner-form checks | 5.1 |
| FRM-DOM-04 | MUST | Passes the RND conformance suite | 13 |
| FRM-KEY-01 | MUST | No keyboard handlers; every submit source runs the same pipeline | 6.1 |
| FRM-FOC-01 | MUST | Blocked submit focuses first invalid by document order, selects input text, synchronously | 6.3 |
| FRM-FOC-02 | MUST | After passed submission, next error-map change focuses once, after paint | 6.3 |
| FRM-EVT-01 | MUST | Submit pipeline order (count, validate all, block/focus, latch, onSubmit, onFormSubmit) | 7 |
| FRM-EVT-02 | MUST | `onSubmit` once per passed submission; native default not prevented for it | 7 |
| FRM-EVT-03 | MUST | `onFormSubmit` after `onSubmit`, default prevented, reason `'none'`, native event | 7 |
| FRM-EVT-04 | MUST | Values object: named entries, registration order, typed projections | 7 |
| FRM-EVT-05 | MUST | No callback on blocked submission | 7 |
| FRM-EVT-06 | MUST | `clearErrors(name)` removes own key; no-op otherwise; batch-safe | 7 |
| FRM-EVT-07 | MUST | Submit details are non-cancelable (no `cancel`/`trigger`) | 7 |
| FRM-FRM-01 | MUST | Submit validates all fields regardless of mode; any invalidity blocks | 8 |
| FRM-FRM-02 | MUST | Pending async never blocks; resolved async error blocks | 8 |
| FRM-FRM-03 | MUST | Same-name fields independent; unnamed fields block but absent from values | 8 |
| FRM-FRM-04 | MUST | Unmounted/disabled/replaced fields leave the registry; re-enable re-registers | 8 |
| FRM-FRM-05 | MUST | Server error → field invalid, `aria-invalid`, `Field.Error` text/list | 8 |
| FRM-FRM-06 | MUST | Server error cleared on that field's change; validator re-run gating | 8 |
| FRM-FRM-07 | MUST | Disabled field with form error stays `data-invalid` but does not block | 8 |
| FRM-FRM-08 | MUST | Native `FormData` untouched; Form contributes no entries | 8 |
| FRM-FRM-09 | MUST | Portaled fields participate; eligibility per FFI-FRM-23 | 8 |
| FRM-FRM-10 | MUST | No `reset` handling | 8 |
| FRM-FRM-11 | MUST | Imperative validate: no counter, focus, submission or callbacks | 8 |
| FRM-FRM-12 | MUST | `validate(name)` resolves against the current registry only | 8 |
| FRM-EDGE-01 | MUST | `validate('')` validates all | 12 |
| FRM-EDGE-02 | MUST | `validate(unknownName)` is a no-op | 12 |
| FRM-EDGE-03 | MUST | `errors` `undefined`/`null` → nothing invalid | 12 |
| FRM-EDGE-04 | MUST | Zero registered fields → passes, `onFormSubmit({})` | 12 |
| FRM-EDGE-05 | MUST | Controlless invalid entries block; focus skips to next with control | 12 |
| FRM-EDGE-06 | MUST | Non-`<form>` substitution: handler and `novalidate` still attached; Enter takes foreign-form path | 12 |
| FRM-EDGE-07 | MUST | Each submission independent; passing re-arms post-submission focus | 12 |

## 16. Open questions

1. **`onClearErrors` does not exist in v1.9.0.** The assignment hinted at it; it was removed in v1.0.0-alpha.9 (`CHANGELOG.md` "**Breaking change:** The `onClearErrors` prop has been removed", line 1809). Errors are cleared internally (FRM-EVT-06) and the consumer observes nothing; a consumer that needs to know must compare its own `errors` object against field changes itself. Not documented as a prop.
   Resolved 2026-10-23: the port has no `OnClearErrors` either; `Form.ClearErrors(string? name)` is the imperative counterpart (FRM-API-05 port note).
2. **`onSubmit` and `noValidate` are absent from the API outline** (`components/page.mdx:893` lists `actionsRef, className, errors, onFormSubmit, render, style, validationMode`) because the generator omits native attributes; both are intercepted/defaulted by `Form.tsx:27,108` and are documented here as FRM-API-04/06.
   Resolved 2026-10-23: `OnSubmit` and `NoValidate` are typed parameters of `Form` with XML documentation (FRM-API-04, FRM-API-06).
3. **Post-submission focus latch is consumed by any error-map change**, not only by a new `errors` prop: the effect at `Form.tsx:77-84` depends on the internal map, which `clearErrors` also mutates. After a passed submission, the first field change that deletes a key (possible only for keys of fields that did not block — e.g. an error keyed to a disabled field's name, or a bare `Input` with that `name` outside any `Field.Root`) would consume the latch and run focus-first-invalid once. Untested; FFI-FOC-03 describes the common case ("next change of the `errors` prop"). A port should mirror the state-change trigger rather than the prop-change trigger to be safe. Resolved: FFI-FOC-03 now specifies the state-change trigger (any identity change of the error map, including a `clearErrors` deletion), and FRM-FOC-02 follows it; the deletion case remains untested.
4. **`validationMode` precedence (Field over Form)** is stated in JSDoc (`Form.tsx:194`) and FFI-API-02 but has no test (also FFI §10 item 5).
   Resolved 2026-10-23: the port follows the source. `FieldRoot.ValidationMode` is `ValidationMode?`, and `null` defers to the form scope's mode (FFI §10 item 5, resolved in PR 1a).
5. **Nested `Form`.** HTML drops a nested `<form>` element at parse time, and `Form.tsx` has no guard; an inner `Form` component would create a second scope whose fields register with the inner (DOM-less or parse-dropped) form. Not supported; a port need not handle it.
   Resolved 2026-10-23: unsupported in the port as in the source. An inner `Form` cascades its own scope, and its fields register there; no guard is added.
6. **`actionsRef.validate` and the submission counter** (FRM-FRM-11): the consequence that `'onSubmit'` fields do not switch to change-revalidation after an imperative validation follows from `Form.tsx:86-102` and `FieldRoot.tsx:88-92` but is untested.
   Resolved 2026-10-23: the port follows the source. `ValidateAsync` neither counts as a submission nor switches `OnSubmit`-mode fields to change validation (FRM-FRM-11).
7. **`errors` identity churn.** Because replacement is by identity (`Form.tsx:73`), a consumer passing a fresh literal each render re-asserts cleared errors every render. Documented as FRM-STA-02; whether a port should compare structurally instead is a product decision (the demos avoid the issue by storing `errors` in state).
   Resolved 2026-10-23: identity, as the source (FRM-STA-02). A new `FormErrors` instance replaces the map and re-asserts cleared keys; the same instance keeps them cleared. The sample stores its `FormErrors` in a field.
8. **Substituting a non-`<form>` element** (FRM-EDGE-06) is only exercised by the conformance suite's `div` substitution, which asserts attribute forwarding, not submission; the stated consequences follow from `FieldControl.tsx:176-189`.
   Resolved 2026-10-23: FRM-EDGE-06 holds as written; the conformance suite's `div` substitution is the port's test of its attribute half.
9. **Port: conditional prevention of the submit event.** FRM-EVT-01 prevents the native `submit` default only when validation blocks (`Form.tsx:117-118`) or when `onFormSubmit` is given (`Form.tsx:125-126`), and focuses the first invalid control inside the event (FRM-FOC-01). decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 makes `preventDefault` JavaScript policy, and decisions/04-js-interop-boundary.md §3 lists no `decide()` point for form submission, so on Server the verdict cannot be returned synchronously. Unresolved: needs a decision (a `decide()` point with a fallback policy, or always preventing and re-submitting from C#). Decided (2026-10-18): native submit is always prevented by the `form` module and re-issued after C# validation (ADR 04 Amendments).
