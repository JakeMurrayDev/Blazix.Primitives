# Radio Group

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/radio-group/` |
| Tests | `packages/react/src/radio-group/**/*.test.tsx` (1 file) |
| Docs page | `docs/src/app/(docs)/react/components/radio-group/page.mdx` |
| Prefix | `RDG` |
| Status | implemented |
| Revised | 2026-10-23 |
| Builds on | none (pairs with [radio](radio.md), which supplies the items) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [button-behavior](../architecture/button-behavior.md) (via Radio), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

`RadioGroup` owns the single selected value of a set of `Radio.Root` buttons (the WAI-ARIA `radiogroup` pattern) and makes them one roving-tabindex composite: the group occupies one tab stop, arrow keys move focus between radios and select the newly focused radio, and the value may be any JavaScript value (compared by identity). The group renders a `<div role="radiogroup">` and no form input of its own; every radio renders its own visually hidden `<input type="radio">` carrying the group's `name`, `form` and `required`, so native form submission and constraint validation behave as a native radio group would. Value ownership is the `value`/`defaultValue` pair; changes are reported once through `onValueChange` with reason `'none'`.

Within `Field.Root` the group (not the radios) registers as the field's control: it reports `dirty`/`filled`/`touched`/`focused`, exposes a focus target that resolves to the representative radio, projects its value into `Form` values exactly as native submission would (a disabled selected radio projects `null`) and takes its accessible name from `Field.Label` or an enclosing `Fieldset.Legend`. The radio parts themselves are specified in [radio](radio.md) (prefix `RAD`); this document covers only what the group adds and how it configures the shared composite and field mechanisms.

## 2. Anatomy

```
<RadioGroup>                 required, 1          default element: div   (composite root; no hidden input)
  <Radio.Root>               optional, 0..n       default element: span  (+ hidden <input type="radio"> sibling) — see radio.md
    <Radio.Indicator>        optional, 0..n       default element: span  — see radio.md
```

- **RDG-STR-01** — MUST. `RadioGroup` MUST render exactly one host element (default `<div>`) and MUST NOT render any `<input>` of its own; form participation is delegated to each radio's hidden input.
  Source: `radio-group/RadioGroup.tsx:248-266`. Test: [L4] `RadioGroup.test.tsx` "should return null in form data when no radio is selected" (only radio inputs exist).
  Port note 2026-10-23: re-tagged from L2, because the cited titles build `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). They run in `radio-group.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **RDG-STR-02** — MUST. The group MUST act as the composite root for every `Radio.Root` in its subtree regardless of DOM depth or intermediate elements (`Field.Item`, `<label>`, `Fieldset.Root`, portals): membership is established through ancestor-provided shared state, and the item registry orders radios by document position (CMP-STA-02).
  Source: `radio-group/RadioGroup.tsx:249-265`, `radio/root/RadioRoot.tsx:54,239,263-272`. Test: [L2] `RadioGroup.test.tsx` "includes a context-portaled radio without native form association in onFormSubmit", "associates implicitly" (radios inside `<label>`) (L3 for the document-order clause).
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22; the CheckboxGroup portal precedent): the `<label>` and `Field.Item` clauses are green (`radio-group.spec.ts` "associates implicitly", `RadioGroupFieldTests` "associates implicitly (Field.Label)"); the portal clause and its title "includes a context-portaled radio without native form association in onFormSubmit" are open to M4 (no portal before it) and do not hold the status. The document-order clause is the `composite` module's (CMP-STA-02).
- **RDG-STR-03** — MUST. A group with zero radios is valid: it MUST render its element with `role="radiogroup"` and ARIA attributes, register with an enclosing field, and report `null` as its value.
  Source: `radio-group/RadioGroup.tsx:169,223-246`. Test: [L2] `RadioGroup.test.tsx` conformance suite (renders `<RadioGroup />`), "unblocks submission after every radio in the group unmounts".
- **RDG-STR-04** — MUST. `RadioGroup` MAY be substituted into `Fieldset.Root` (`<Fieldset.Root render={<RadioGroup />}>`); the merged element MUST then be both the fieldset and the radiogroup, and the legend MUST label the group (RDG-ARIA-05).
  Source: `radio-group/RadioGroup.tsx:68,185`. Test: [L2] `RadioGroup.test.tsx` "labels the radio group from the fieldset legend". Docs: `components/radio-group/page.mdx` §Form integration.

## 3. Public API

### 3.1 `RadioGroup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value` (any type) | — (uncontrolled) | Selected radio's value. Supplying it (non-`undefined`) on first render makes the value consumer-owned. | RDG-API-01 |
| `defaultValue` | `Value` | `undefined` (nothing selected) | Initial value when component-owned. | RDG-API-02 |
| `onValueChange` | `(value: Value, eventDetails: RadioGroup.ChangeEventDetails) => void` | — | Called when the user selects a radio, before the state changes. | RDG-API-03 |
| `disabled` | `boolean` | `false` | Ignore user interaction; combined (OR) with `Field.Root`'s `disabled`; inherited by every radio. | RDG-API-04 |
| `readOnly` | `boolean` | `false` | User cannot change the selection; inherited by every radio. | RDG-API-05 |
| `required` | `boolean` | `false` | A radio must be selected to pass native validation; inherited by every radio's hidden input. | RDG-API-06 |
| `name` | `string` | — | Form field name, placed on every radio's hidden input. Overridden by `Field.Root`'s `name`. | RDG-API-07 |
| `form` | `string` | — | `id` of the owning `<form>`; placed on every radio's hidden input. | RDG-API-08 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the *forwarded input*: the checked radio's hidden input, else the first enabled radio's (§4). | RDG-API-09 |
| `id` | `string` | — (no attribute) | Forwarded verbatim to the host element; also used as the field registration id. Not in the API outline (native attribute). | RDG-API-10 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | RDG-API-11 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the host element (`aria-labelledby`, `aria-describedby`, `role`, `onKeyDown`, … merge per §5). | RDG-API-12 |

**Consumed (not forwarded to the element):** `value`, `defaultValue`, `onValueChange`, `disabled`, `readOnly`, `required`, `name`, `form`, `inputRef`, `className`, `style`, `render`.

- **RDG-API-01** — MUST. When `value` is not `undefined` on the first render the selection MUST be consumer-owned: the group renders whatever `value` it is given and MUST NOT change it internally; a `null` value is a legitimate consumer-owned value (no radio selected unless a radio's own `value` is `null`).
  Source: `radio-group/RadioGroup.tsx:74-79`, `packages/utils/src/useControlled.ts:41-46,83-90`; see UTL-STA-01/03/04. Test: [L2] `RadioGroup.test.tsx` "revalidates when the controlled value changes externally"; `RadioGroupTests` "port: a controlled group renders the value it is given and only requests a change".
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22; ADR 02 Amendments 2026-10-22 item 1): `null` means nothing selected, so a consumer-owned `null` checks no radio (RDG-EDGE-03's note). "keeps inputRef pointing to the first radio when the value is cleared" is re-cited to `RadioGroupTests` "port: a controlled group renders the value it is given and only requests a change" and "port: a null value selects no radio".
- **RDG-API-02** — MUST. When `value` is `undefined` on the first render the initial selection MUST be `defaultValue` as supplied (`undefined` when omitted, meaning no radio is checked); later changes of `defaultValue` MUST NOT change the selection.
  Source: `radio-group/RadioGroup.tsx:74-79`, `packages/utils/src/useControlled.ts:43`; see UTL-STA-02/05. Test: [L2] `RadioGroup.test.tsx` "sets tabIndex=0 to the correct element initially" (`defaultValue="b"` checks b), "should automatically select radio upon navigation" (no default → nothing checked).
  Port note 2026-10-23 (design point R1 of M2 tracks 7–8, option (b), settled by the orchestrator 2026-10-23: the "nothing selected" state ADR 08 Amendments 2026-10-22 names for `null`, applied to a non-nullable `TValue`): an uncontrolled group without `DefaultValue` holds no selection until its first committed one, so a non-nullable `TValue` starts with nothing checked as Base UI's `undefined` does: the radio whose value is `default(TValue)` (`0` in a `RadioGroup<int>`) is not checked, `filled` is `false` and the projected value is `null` (`RadioGroupTests` "port: an uncontrolled group of a value type without a default selects nothing"). A controlled group holds the value it is given.
- **RDG-API-03** — MUST. `onValueChange` MUST receive the selected radio's `value` (the identical object/primitive the radio was given) as its first argument and the change-event details of §3.2 as its second. Firing rules are in §7.
  Source: `radio-group/RadioGroup.tsx:82-92,325`. Test: [L2] `RadioGroup.test.tsx` "should call onValueChange when an item is clicked".
  Port note 2026-10-23 (design point R7 of M2 tracks 7–8, option (a), settled by the orchestrator 2026-10-23; ADR 02 Amendments 2026-10-20, part-misuse errors are always on; ADR 08 Amendments 2026-10-22; ADR 16 rule 7): the value is the selecting radio's `Value`, of the group's `TValue`, unboxed. A radio whose `TValue` differs from its group's (Razor infers it from the radio's own `Value` first, so `Value="1"` in a `RadioGroup<int?>` is a `RadioRoot<int>`) throws ``Blazix: A `RadioRoot` must have the `TValue` of its `RadioGroup`. …`` when it is initialized, in every build, instead of rendering as a static radio outside the group; both parts' remarks say so (`RadioGroupTypeTests` "port: a radio whose TValue differs from its group's throws a descriptive error"). A compile-time analyzer is a follow-up outside M2.
- **RDG-API-04** — MUST. The effective disabled state MUST be `fieldDisabled || disabled` (`fieldDisabled` is the enclosing `Field.Root`'s disabled, itself including an enclosing `Fieldset.Root`'s; `false` outside a field). It MUST be published to every radio, which ORs it with its own (RAD-API-02), and it MUST drop the group's field registration while `true` (RDG-FRM-05).
  Source: `radio-group/RadioGroup.tsx:70,169,197`; see FFI-STA-11. Test: [L2] `RadioGroup.test.tsx` "should have the `aria-disabled` attribute", "should receive disabled prop from Field.Root", "should not change its state when clicked" (disabled).
- **RDG-API-05** — MUST. `readOnly` defaults to `false`; when `true` it MUST be published to every radio so that no user action changes the selection (RAD-PTR-07, RAD-KEY-06) while focus, tab order and form submission are unaffected, and the group MUST carry `aria-readonly="true"` (RDG-ARIA-04).
  Source: `radio-group/RadioGroup.tsx:41,201,228`, `radio/root/RadioRoot.tsx:80`. Test: [L2] `RadioGroup.test.tsx` "should have the `aria-readonly` attribute", "should not change its state when clicked" (readOnly) (L4 for the user-action clause).
- **RDG-API-06** — MUST. `required` defaults to `false`; when `true` every radio's hidden input MUST carry the `required` attribute (RAD-DOM-09) and the group MUST carry `aria-required="true"` (RDG-ARIA-02).
  Source: `radio-group/RadioGroup.tsx:42,203,226`, `radio/root/RadioRoot.tsx:81,194`. Test: [L2] `RadioGroup.test.tsx` "triggers native HTML validation on submit", "should place the style hooks on the root and subcomponents".
- **RDG-API-07** — MUST. The effective name MUST be the enclosing `Field.Root`'s `name` when defined, otherwise the `name` prop; it MUST be placed on every radio's hidden input (RAD-DOM-09) and MUST NOT appear on the group element.
  Source: `radio-group/RadioGroup.tsx:71,200`, `radio/root/RadioRoot.tsx:187`; see FFI-STA-10. Test: [L2] `RadioGroup.test.tsx` "should set the name attribute on each radio input", "passes the `name` prop to the radio input" (Field name wins), "should receive name prop from Field.Root".
- **RDG-API-08** — MUST. `form` MUST be forwarded as the `form` attribute of every radio's hidden input and MUST NOT appear on the group element.
  Source: `radio-group/RadioGroup.tsx:46,198`, `radio/root/RadioRoot.tsx:82,185`; see FFI-DOM-08. Test: [L4] `RadioGroup.test.tsx` "submits to an external form when `form` is provided", "omits a radio associated to another form via the `form` prop".
  Port note 2026-10-23: re-tagged from L2, because the cited titles build `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). They run in `radio-group.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **RDG-API-09** — MUST. `inputRef` MUST always reference the *forwarded input* defined in RDG-STA-07..10, accept object and callback forms, and be detached (object `.current = null`; callback invoked with `null`, or its returned cleanup invoked) whenever the forwarded input is removed or the `inputRef` prop identity changes. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden inputs stay internal and no public handle to them exists.
  Source: `radio-group/RadioGroup.tsx:103-152,179-182`, `packages/utils/src/useMergedRefs.ts:95-152`. Test: [L2] `RadioGroup.test.tsx` "points inputRef to the checked radio input when present", "supports inputRef as a function", "transfers the current input when the object inputRef changes", "detaches the previous callback inputRef when it changes (cleanup: %s)". Docs: CHANGELOG v1.9.0 "Correct `inputRef` forwarding and cleanup".
- **RDG-API-10** — MUST. The `id` prop MUST be forwarded verbatim to the host element; when omitted the host element MUST have no `id` attribute (no generated id is rendered). The field registration id (RDG-FRM-05) MUST be `id` when given, else a generated `base-ui-…` id.
  Source: `radio-group/RadioGroup.tsx:49,72,169,224`. Test: [L2] `RadioGroup.test.tsx` "is forwarded to the root element". Docs: CHANGELOG v1.6.0 "Forward group ids".
- **RDG-API-11** — MUST. `className`, `style` and `render` MUST behave per RND with the state object below; none is forwarded as a plain attribute.
  Source: `radio-group/RadioGroup.tsx:38-39,50,251-254`. Test: [L2] `RadioGroup.test.tsx` conformance suite "Base UI component API".
- **RDG-API-12** — MUST. Every other prop MUST be forwarded to the host element, merged after the built-in attributes (consumer attribute wins, handlers chain consumer-first per RND-EVT-02) and before the field validation attributes; `value` MUST NOT be forwarded. Server: relaxed, see §9.
  Source: `radio-group/RadioGroup.tsx:51,255-259`. Test: [L2] `RadioGroup.test.tsx` "can override the built-in attributes", "does not forward `value` prop".

#### State object

The enclosing field's state (all `false`/`null` outside a field) plus three group fields. The selected value is **not** part of the state object.

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (RDG-API-04), `false` when undefined. |
| `readOnly` | `boolean` | `readOnly` prop, `false` when undefined. |
| `required` | `boolean` | `required` prop, `false` when undefined. |
| `touched` | `boolean` | Field: a radio was selected or focus left the group at least once. |
| `dirty` | `boolean` | Field: selected value differs (identity) from the value captured at first registration. |
| `valid` | `boolean \| null` | Field validity verdict; `null` until validated or while disabled. |
| `filled` | `boolean` | Field: a non-null value is selected. |
| `focused` | `boolean` | Field: a radio of this group owns focus. |

- **RDG-STA-01** — MUST. The state object MUST contain exactly the eight fields above; `disabled` is the effective value, not the field's own `disabled`.
  Source: `radio-group/RadioGroup.tsx:187-192,271-280`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effective disabled | RDG-DOM-02 |
| `data-readonly` | `""` while `readOnly` | RDG-DOM-02 |
| `data-required` | `""` while `required` | RDG-DOM-02 |
| `data-touched`, `data-dirty`, `data-filled`, `data-focused` | `""` while the field flag is `true` (Field only) | RDG-DOM-03 |
| `data-valid` / `data-invalid` | `""` while field `valid` is `true` / `false` | RDG-DOM-04 |

The exported `RadioGroupDataAttributes` namespace lists only `data-disabled` (`radio-group/RadioGroupDataAttributes.ts:1-4`); the other attributes above are emitted by the generic state projection and asserted by tests (see §16 item 2).

#### CSS variables

None.

### 3.2 Shared types

- `RadioGroup.ChangeEventReason` = `'none'` (the only literal). Source: `radio-group/RadioGroup.tsx:332`, `internals/reason-parts.ts:1`.
- `RadioGroup.ChangeEventDetails` = the common change-event details of [EVD](../architecture/event-details-and-reasons.md) (EVD-API-01) specialised to reason `'none'`: `{ reason: 'none'; event: Event; cancel(); allowPropagation(); isCanceled; isPropagationAllowed; trigger: Element | undefined }`. Source: `radio-group/RadioGroup.tsx:334`, `internals/createBaseUIEventDetails.ts:56-85,126-157`.
- `RadioGroup.State`, `RadioGroup.Props<Value = any>` — above. `Value` is a free type parameter; the public type tests (`RadioGroup.spec.tsx:4-57`) require inference from `value`/`defaultValue` (including literal unions and `null`) and an explicit type argument.
- `Radio.Root.Props<Value>` / `Radio.Root.State`, `Radio.Indicator.Props` / `.State` — see [radio](radio.md) §3.

## 4. State model

The group owns one value (`checkedValue`, type `Value | undefined`) plus one transient keyboard flag. Everything else in its state object is a prop echo or field state.

**Ownership.** `value`/`defaultValue` per RDG-API-01/02; ownership is fixed on the first render (UTL-STA-01). In consumer-owned mode a selection only fires `onValueChange`; the DOM reflects the new selection when the consumer passes it back.

**Derived values.**

- effective disabled = `fieldDisabled || disabled`; effective name = `fieldName ?? name`.
- a radio is checked iff `checkedValue === radio.value` (strict identity, RAD-STA-01); with duplicate radio values several radios are checked at once (RDG-EDGE-02).
- `filled` (field) = `checkedValue != null`; `dirty` (field) = `checkedValue !== initialValue`.
- *forwarded input* = the hidden input exposed through `inputRef` (RDG-STA-07..10).
- *pending-navigation flag* = set by an arrow keydown inside the group, consumed by the radio that next receives focus (RDG-KEY-03, RAD-FOC-05).

- **RDG-STA-02** — MUST. In component-owned mode a non-canceled selection MUST set `checkedValue` to the selecting radio's `value` (identity preserved: objects are stored as given, not cloned or stringified).
  Source: `radio-group/RadioGroup.tsx:82-92`. Test: [L2] `RadioGroup.test.tsx` "should update its state if the underlying input is toggled", "allows `null` value" (`RadioRoot.test.tsx`).
  Port note 2026-10-23 (design point R1 of M2 tracks 7–8, option (b), settled by the orchestrator 2026-10-23; ADR 08 Amendments 2026-10-22): the first non-canceled selection also clears the group's "no selection" state, so a non-nullable `TValue` group that started with nothing selected holds the selecting radio's value from then on (`RadioGroupTests` "port: an uncontrolled group of a value type without a default selects nothing"). A radio whose `Value` is `null` never selects (RAD-EVT-03), so "allows `null` value" is restated as `RadioRootTests` "port: a radio whose value is null is never selected (allows `null` value)".
- **RDG-STA-03** — MUST. In consumer-owned mode a user selection MUST NOT change which radio is checked unless the consumer updates `value`; when `value` changes for any reason, every radio's `aria-checked`, data attributes, active-item marker and hidden input `checked` property MUST follow in the same render. Server: relaxed, see §9.
  Source: `radio-group/RadioGroup.tsx:74-79,196`, `radio/root/RadioRoot.tsx:84,131,133,193`. Test: [L2] `RadioGroup.test.tsx` "revalidates when the controlled value changes externally"; `RadioGroupTests` "port: a controlled group renders the value it is given and only requests a change".
  Port note 2026-10-23 (ADR 02 Amendments 2026-10-22 item 1; ADR 04 Amendments 2026-10-22, the hidden input's `checked` is the `dom` primitive's from attach): "cleans up inputRef bindings when selecting, clearing, and unmounting" is re-cited to `RadioGroupTests` "port: a controlled group renders the value it is given and only requests a change" and `radio-group.spec.ts` "port: a controlled group follows its value, and the hidden inputs follow it". The inputs' `checked` follows in the `update` the render sends (ADR 01, the row "JS-owned values that originate in C# state").
- **RDG-STA-04** — MUST. When `value` was consumer-owned at first render and later becomes `undefined`, the rendered selection MUST fall back to the initial `defaultValue` (UTL-STA-03).
  Source: `packages/utils/src/useControlled.ts:44-46`. Test: none.
- **RDG-STA-05** — MUST. Within a `Field.Root`, after every change of `checkedValue` (not on mount) the field's `filled` MUST be set to `checkedValue != null` and `dirty` to `checkedValue !== initialValue` (strict identity against the value captured at first registration, FFI-STA-04), post-commit, pre-paint. Independently, a radio whose hidden input is checked when it mounts sets `filled` to `true` (RAD-STA-05), which is how an initial selection becomes `filled`. Server: relaxed, see §9.
  Source: `radio-group/RadioGroup.tsx:171-175`, `radio/root/RadioRoot.tsx:98-102`. Test: [L2] `RadioGroup.test.tsx` "does not change state when canceled via a root click" (neither flag set), "should automatically select radio upon navigation".
- **RDG-STA-06** — MUST. The pending-navigation flag MUST become `true` on any keydown inside the group whose `key` starts with `"Arrow"` (captured before item handlers, regardless of modifiers or whether the composite acts on it) and MUST become `false` when a radio consumes it on focus (RAD-FOC-05) or when focus leaves the group (RDG-FOC-05). It is never exposed in the state object.
  Source: `radio-group/RadioGroup.tsx:80,205-206,232,241-245`, `radio/root/RadioRoot.tsx:62,159-165`. Test: [L4] `RadioGroup.test.tsx` "should automatically select radio upon navigation", "does not select on refocus after an ignored arrow (%s)". Docs: CHANGELOG v1.9.0 "Prevent ignored arrow keys from selecting on refocus".
- **RDG-STA-07** — MUST. *Forwarded input selection on registration.* Each radio registers its hidden input with the group when the input mounts and again (post-commit, pre-paint) whenever the radio's `checked` or disabled state changes, except that a radio that is both disabled and checked does not re-register. On each registration of an input whose `disabled` **property** is `false`: (a) if no "first enabled input" is recorded, this input is recorded as it; (b) if the input is checked, or no forwarded input exists, or the current forwarded input's `disabled` property is `true`, this input becomes the forwarded input. Registration of a disabled input changes nothing. Server: relaxed, see §9. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden inputs stay internal and no public handle to them exists; its §9 entry is removed.
  Source: `radio-group/RadioGroup.tsx:125-139`, `radio/root/RadioRoot.tsx:96,104-115`. Test: [L2] `RadioGroup.test.tsx` "points inputRef to the checked radio input when present", "skips disabled radios when assigning inputRef", "points inputRef to the first radio input when nativeButton wraps a button", "detaches inputRef when an initially disabled radio is enabled and then unmounted", "keeps inputRef available after an ancestor fieldset is enabled" (fieldset-disabled inputs have `disabled === false` and are not skipped).
- **RDG-STA-08** — MUST. When a radio's hidden input unmounts: if it was the recorded first enabled input that record MUST be cleared (no replacement is chosen until another registration occurs), and if it was the forwarded input the forwarded input MUST become `null` (consumer `inputRef` detached). Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden inputs stay internal and no public handle to them exists.
  Source: `radio-group/RadioGroup.tsx:144-151`. Test: [L2] `RadioGroup.test.tsx` "detaches inputRef when its current radio unmounts", "detaches inputRef when a radio selected after mount unmounts", "cleans up inputRef bindings when selecting, clearing, and unmounting" (unmount → no bindings).
- **RDG-STA-09** — MUST. When `checkedValue` changes to `null`/`undefined` and a first enabled input is recorded whose `disabled` property is `false`, that input MUST become the forwarded input (post-commit, pre-paint, after the field writes of RDG-STA-05). Server: relaxed, see §9. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden inputs stay internal and no public handle to them exists; its §9 entry is removed.
  Source: `radio-group/RadioGroup.tsx:179-182`. Test: [L2] `RadioGroup.test.tsx` "keeps inputRef pointing to the first radio when the value is cleared", "cleans up inputRef bindings when selecting, clearing, and unmounting" (clear → first input).
- **RDG-STA-10** — MUST. The consumer `inputRef` MUST be (re)applied only when the forwarded input changes or the `inputRef` prop identity changes: unrelated updates of the group MUST NOT re-invoke a callback DOM handle; when the prop changes, the previous ref MUST be detached (object → `null`; callback → its cleanup, else called with `null`) and the new ref MUST receive the current forwarded input (including after a period with no `inputRef`); on group unmount every ref MUST be detached. The forwarded input is readable synchronously post-commit, pre-paint (a layout-phase effect in the consumer sees it). Server: relaxed, see §9. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden inputs stay internal and no public handle to them exists; its §9 entry is removed.
  Source: `radio-group/RadioGroup.tsx:105-123`. Test: [L2] `RadioGroup.test.tsx` "does not detach a stable inputRef callback on unrelated re-renders", "transfers the current input when the object inputRef changes", "detaches the previous callback inputRef when it changes (cleanup: %s)", "allows reading inputRef.current in an effect".
- **RDG-STA-11** — MUST. The group MUST NOT write the field's shared validation input handle; validation, focus targeting and value projection MUST use the registry of per-radio inputs (FFI-FRM-24), so that an unmounted radio never remains the field's validation input.
  Source: `radio-group/RadioGroup.tsx:108-111,160-164`, `field/root/useFieldValidation.ts:228-232`. Test: [L2] `RadioGroup.test.tsx` "unblocks submission after every radio in the group unmounts".

## 5. Rendered DOM and ARIA

### 5.1 Group element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | `id` prop verbatim; absent otherwise | RDG-DOM-01 | C# |
| `role` | `"radiogroup"` (consumer may override) | RDG-ARIA-01 | C# |
| `aria-required` | `"true"` only when `required` | RDG-ARIA-02 | C# |
| `aria-disabled` | `"true"` only when effective disabled | RDG-ARIA-03 | C# |
| `aria-readonly` | `"true"` only when `readOnly` | RDG-ARIA-04 | C# |
| `aria-labelledby` | consumer prop, else `Field.Label` id, else `Fieldset.Legend` id | RDG-ARIA-05 | C# |
| `aria-describedby` | consumer tokens + `Field.Root`-scope description/error ids | RDG-ARIA-06 | C# |
| `aria-invalid` | `"true"` while the field is invalid and the group is not disabled | RDG-ARIA-07 | C# |
| `tabindex`, `aria-orientation` | never added (the radios carry the roving `tabindex`) | RDG-DOM-05 | C# |
| `data-*` | §3.1 table | RDG-DOM-02..04 | C# |

- **RDG-DOM-01** — MUST. The host element MUST carry the `id` prop verbatim and no `id` when the prop is absent.
  Source: `radio-group/RadioGroup.tsx:224`. Test: [L2] `RadioGroup.test.tsx` "is forwarded to the root element".
- **RDG-DOM-02** — MUST. `data-disabled`, `data-readonly` and `data-required` MUST be present (value `""`) exactly while the corresponding state field is `true`.
  Source: `radio-group/RadioGroup.tsx:187-192`, `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents", "should receive disabled prop from Field.Root".
- **RDG-DOM-03** — MUST. `data-touched`, `data-dirty`, `data-filled`, `data-focused` MUST be present (value `""`) exactly while the corresponding field flag is `true`; outside a `Field.Root` they MUST never appear.
  Source: `radio-group/RadioGroup.tsx:188`, `internals/field-root-context/FieldRootContext.ts:37-67`; see FFI-DOM-01. Test: [L2] `RadioGroup.test.tsx` "should automatically select radio upon navigation" (`data-touched`), "does not change state when canceled via a root click" (none).
- **RDG-DOM-04** — MUST. `data-valid` MUST be present when field `valid === true`, `data-invalid` when `valid === false`, neither when `null`.
  Source: `radio-group/RadioGroup.tsx:261`, `internals/field-constants/constants.ts:34-44`. Test: none (asserted through `aria-invalid` in "onSubmit").
- **RDG-DOM-05** — MUST. The group element MUST NOT receive `tabindex`, `aria-orientation`, `name`, `value` or `form` attributes from the component (CMP-DOM-03).
  Source: `radio-group/RadioGroup.tsx:37-52,223-229`, `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L2] `RadioGroup.test.tsx` "does not forward `value` prop".
- **RDG-DOM-06** — MUST. Consumer attributes MUST override the built-in attributes of the same name (`role`, `aria-*`, `id`), except `className`/`style` (merged per RND) and handlers (chained, consumer first, RND-EVT-02); the field validation attributes (`aria-describedby`, `aria-invalid`) are merged last and compose with the consumer's `aria-describedby` (RDG-ARIA-06).
  Source: `radio-group/RadioGroup.tsx:255-259`. Test: [L2] `RadioGroup.test.tsx` "can override the built-in attributes", "updates label precedence without retaining replaced or unmounted IDs" (explicit `aria-labelledby` wins).

### 5.2 Hidden `<input>`

None of its own. Each radio renders `<input type="radio">` with the group's effective `name`, `form`, `required` and the radio's serialized `value` — see RAD-DOM-08..12.

### 5.3 ARIA

- **RDG-ARIA-01** — MUST. The group MUST have `role="radiogroup"` unless the consumer supplies `role`.
  Source: `radio-group/RadioGroup.tsx:225`. Test: [L2] `RadioGroup.test.tsx` every `getByRole('radiogroup')` query; "can override the built-in attributes".
- **RDG-ARIA-02** — MUST. `aria-required="true"` MUST be present when `required` is `true` and absent otherwise (never `"false"`).
  Source: `radio-group/RadioGroup.tsx:226`. Test: none directly (`required` tests assert the radio inputs). Docs: CHANGELOG v1.0.0-beta.2 "Add aria-required attribute".
- **RDG-ARIA-03** — MUST. `aria-disabled="true"` MUST be present when the effective disabled is `true` and absent otherwise.
  Source: `radio-group/RadioGroup.tsx:227`. Test: [L2] `RadioGroup.test.tsx` "should have the `aria-disabled` attribute", "should not have the aria attribute when `disabled` is not set", "should receive disabled prop from Field.Root".
- **RDG-ARIA-04** — MUST. `aria-readonly="true"` MUST be present when `readOnly` is `true` and absent otherwise.
  Source: `radio-group/RadioGroup.tsx:228`. Test: [L2] `RadioGroup.test.tsx` "should have the `aria-readonly` attribute", "should not have the aria attribute when `readOnly` is not set".
- **RDG-ARIA-05** — MUST. `aria-labelledby` MUST resolve, in order: (1) the consumer's `aria-labelledby`; else (2) the id of a mounted `Field.Label` in the enclosing `Field.Root` scope; else (3) the id of a mounted `Fieldset.Legend` of the nearest enclosing `Fieldset.Root`; else absent. Each source MUST be re-resolved when a label/legend mounts, unmounts or is replaced, and an unmounted or replaced id MUST NOT be retained (FFI-ARIA-08/09). A consumer `aria-label` does not suppress the attribute.
  Source: `radio-group/RadioGroup.tsx:66,68,185,229`, `fieldset/legend/FieldsetLegend.tsx:20-22`, `utils/useRegisteredLabelId.ts:6-20`. Test: [L2] `RadioGroup.test.tsx` "labels the radio group from the fieldset legend", "updates label precedence without retaining replaced or unmounted IDs". Docs: `components/radio-group/page.mdx` §Labeling a radio group.
- **RDG-ARIA-06** — MUST. Within a `Field.Root`, the group's `aria-describedby` MUST be the consumer's tokens followed by the ids of `Field.Description`/`Field.Error` elements mounted in the `Field.Root` scope (not those inside `Field.Item`s), de-duplicated and space-joined; absent when empty (FFI-ARIA-01).
  Source: `radio-group/RadioGroup.tsx:258`, `field/root/useFieldValidation.ts:367-376`, `internals/labelable-provider/LabelableProvider.tsx:68-81`. Test: [L2] `RadioGroup.test.tsx` "links the group and individual radios" (`external-description <groupDescriptionId>`).
- **RDG-ARIA-07** — MUST. Within a `Field.Root`, the group MUST carry `aria-invalid="true"` exactly while the field's `valid` is `false` and neither the field nor the group is disabled (FFI-ARIA-03).
  Source: `radio-group/RadioGroup.tsx:258`, `field/root/useFieldValidation.ts:370-373`. Test: [L2] `RadioGroup.test.tsx` "onSubmit", "onBlur validates only when focus leaves the group", "clears external errors on change".

## 6. Interactions

### 6.1 Keyboard

The group is a composite root (CMP §4.5) with orientation `'both'`, looping enabled, Home/End disabled and <kbd>Shift</kbd> allowed. Keys are handled on `keydown` reaching the group element; *min*/*max* are the first/last radio that is not disabled (DOM detection: `disabled` attribute or `aria-disabled="true"`, CMP-ARIA-03).

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowDown</kbd>, <kbd>ArrowRight</kbd> (LTR) / <kbd>ArrowLeft</kbd> (RTL) | a radio focused, no Ctrl/Alt/Meta | Flag set; highlight and focus move to the next enabled radio, wrapping from last to first; default prevented, propagation stopped; the newly focused radio selects itself. | RDG-KEY-01, RDG-KEY-03 |
| <kbd>ArrowUp</kbd>, <kbd>ArrowLeft</kbd> (LTR) / <kbd>ArrowRight</kbd> (RTL) | same | Previous enabled radio, wrapping from first to last; same side effects. | RDG-KEY-01, RDG-KEY-03 |
| any arrow with <kbd>Shift</kbd> | same | Navigates normally. | RDG-KEY-02 |
| any arrow with <kbd>Ctrl</kbd>, <kbd>Alt</kbd> or <kbd>Meta</kbd> | same | Flag set, but no movement, no `preventDefault`/`stopPropagation`; no selection now or on a later refocus. | RDG-KEY-02, RDG-STA-06 |
| any arrow when the only other radios are disabled | same | Nothing (candidate = current index; CMP-KEY-11). | RDG-KEY-04 |
| <kbd>Home</kbd>, <kbd>End</kbd> | any | Not handled (`enableHomeAndEndKeys: false`). | RDG-KEY-05 |
| <kbd>Space</kbd> | radio focused | Selects the focused radio on keyup (RAD-KEY-01). | — |
| <kbd>Enter</kbd> | radio focused | Never selects (RAD-KEY-02). | — |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Not intercepted; the group is one tab stop (RDG-FOC-01) and leaving it clears the flag and marks the field touched. | RDG-KEY-06, RDG-FOC-05 |
| <kbd>PageUp</kbd>, <kbd>PageDown</kbd>, character keys | any | Not handled. | RDG-KEY-05 |

- **RDG-KEY-01** — MUST. Arrow navigation MUST follow the composite rules for orientation `'both'` (CMP-KEY-03, CMP-ALG-14): the four arrow keys are all handled; forward = <kbd>ArrowDown</kbd> or the horizontal forward key (<kbd>ArrowRight</kbd> in LTR, <kbd>ArrowLeft</kbd> in RTL, CMP-I18N-02), backward = <kbd>ArrowUp</kbd> or the horizontal backward key; the candidate is the adjacent non-disabled radio in document order, wrapping at both ends (`loopFocus` default `true`, CMP-KEY-06/07); on a change the event's default is prevented and its propagation stopped (`stopEventPropagation` default `true`, CMP-KEY-10), the candidate becomes the highlighted item and receives DOM focus one microtask later (CMP-FOC-02). Server: relaxed, see §9.
  Source: `radio-group/RadioGroup.tsx:250-264` (no `orientation`/`loopFocus` passed), `internals/composite/root/useCompositeRoot.ts:76-77,221-226,267-316`. Test: [L4] `RadioGroup.test.tsx` "moves focus with arrow keys and Tab" (ltr and rtl), "does not propagate a click to ancestors when selecting with arrow keys" (`RadioRoot.test.tsx`).
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22, option (i), chosen by the user 2026-10-23; ADR 13 Amendments 2026-10-22): the propagation and focus clauses are asserted through the public group by `radio-group.spec.ts` "port: a handled arrow is default-prevented and stopped, and an arrow with no other enabled radio is neither" and "port: after an arrow key the focus and the tab stop move in the same task, on both modes"; a consumer C# `onkeydown` on the radio and on the group runs once each ("port: a handled arrow runs the radio's and the group's C# onkeydown once each").
- **RDG-KEY-02** — MUST. The group MUST pass `modifierKeys: ['Shift']`, so an arrow key with <kbd>Shift</kbd> held navigates normally while <kbd>Control</kbd>, <kbd>Alt</kbd> or <kbd>Meta</kbd> held blocks navigation (CMP-KEY-02); a blocked arrow MUST NOT prevent default or stop propagation.
  Source: `radio-group/RadioGroup.tsx:25,263`, `internals/composite/root/useCompositeRoot.ts:212-214,367-377`. Test: [L4] `RadioGroup.test.tsx` "when Shift is pressed arrow keys move focus normally", "does not select on refocus after an ignored arrow (%s)" (`metaKey`, `ctrlKey`, `altKey` variants). Docs: CHANGELOG v1.0.0-alpha.7 "Handle modifier keys".
- **RDG-KEY-03** — MUST. Every arrow keydown inside the group (capture phase, before composite handling, any modifiers) MUST set the pending-navigation flag (RDG-STA-06); the radio that receives focus from the resulting composite move then selects itself by clicking its own hidden input (RAD-FOC-05), so one arrow press MUST produce exactly one `onValueChange` with the newly focused radio's value, and the previously checked radio MUST become unchecked in the same change.
  Source: `radio-group/RadioGroup.tsx:241-245`, `radio/root/RadioRoot.tsx:156-166`. Test: [L4] `RadioGroup.test.tsx` "should automatically select radio upon navigation", "does not change state when canceled via arrow key navigation" (focus moves even though the selection is canceled). Docs: CHANGELOG v1.0.0-beta.0 "Fix composite focus of initially selected radio item".
- **RDG-KEY-04** — MUST. Radios whose root element carries `aria-disabled="true"` or `disabled` MUST be skipped by arrow navigation and MUST NOT be min/max (CMP-ALG-02/04 with no `disabledIndices`); if no other enabled radio exists the key changes nothing (CMP-KEY-11).
  Source: `radio-group/RadioGroup.tsx:250-264` (no `disabledIndices`), `floating-ui-react/utils/composite.ts:85-107,520-553`. Test: [L4] `RadioGroup.test.tsx` "does not select on refocus after an ignored arrow (%s)" (`single` variant: only sibling disabled → no move).
- **RDG-KEY-05** — MUST. <kbd>Home</kbd>/<kbd>End</kbd> MUST NOT be handled (`enableHomeAndEndKeys: false`, CMP-KEY-01); no other key is handled by the group.
  Source: `radio-group/RadioGroup.tsx:262`, `internals/composite/root/useCompositeRoot.ts:207-210`. Test: none.
- **RDG-KEY-06** — MUST. <kbd>Tab</kbd> MUST NOT be intercepted: it leaves the group from any radio and re-enters on the radio holding the tab stop (CMP-FOC-01).
  Source: `internals/composite/root/useCompositeRoot.ts:208` (keys handled), `internals/composite/item/useCompositeItem.ts:25`. Test: [L4] `RadioGroup.test.tsx` "moves focus with arrow keys and Tab" (Tab to "after", Shift+Tab back to the last focused radio).

### 6.2 Pointer and touch

The group attaches no pointer handlers; clicking a radio (or its label) selects it through the radio's own click path (RAD-PTR-01..11). Pointer type makes no difference.

- **RDG-PTR-01** — MUST. A pointer click on a radio inside the group MUST select that radio (RAD-PTR-01) and, through the focus it receives, MUST move the tab stop to it (RDG-FOC-02) without setting the pending-navigation flag.
  Source: `radio-group/RadioGroup.tsx:223-246` (no pointer handlers), `radio/root/RadioRoot.tsx:142-155`, `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L4] `RadioGroup.test.tsx` "should call onValueChange when an item is clicked", "should apply data-checked and data-unchecked to radio root and indicator".

### 6.3 Focus management

- **RDG-FOC-01** — MUST. Exactly one radio MUST be in the sequential tab order (`tabindex="0"`, all others `-1`, CMP-DOM-01). On the first publication of the item list the tab stop MUST be the checked radio (it carries the active-item marker `data-composite-item-active`, RAD-DOM-02; CMP-STA-23); if none is checked and the first radio is disabled, the first enabled radio (CMP-STA-24); otherwise the first radio. Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:133`, `internals/composite/root/useCompositeRoot.ts:137-161`. Test: [L4] `RadioGroup.test.tsx` "sets tabIndex=0 to the correct element initially". Docs: CHANGELOG v1.0.0-beta.0 "Fix composite focus of initially selected radio item".
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22: the `composite` module owns the roving `tabindex` from attach; ADR 13 §3): re-tagged from L2, because the tab stop after the composite's first publication is only a browser's to observe; the title runs in `radio-group.spec.ts`. C# renders `-1` on every radio and the active-item marker (RAD-DOM-02), asserted at L2 by `RadioRootTests` "port: inside a group the radio is a composite item whose tabindex the composite writes".
- **RDG-FOC-02** — MUST. Whenever a radio receives focus (keyboard, pointer or programmatic) it MUST become the tab stop (CMP-PTR-02); the group passes no `highlightedIndex`/`onHighlightedIndexChange`, so after the initial publication the tab stop follows focus and keyboard moves, **not** the selected value: a programmatic (consumer-owned) change of `value` MUST NOT move the tab stop by itself (see §16 item 1).
  Source: `radio-group/RadioGroup.tsx:250-264`, `internals/composite/root/useCompositeRoot.ts:90,100-108,115-135`, `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L4] `RadioGroup.test.tsx` "moves focus with arrow keys and Tab" (Shift+Tab returns to `c` after navigating there); programmatic case: none.
- **RDG-FOC-03** — MUST. When the radio holding the tab stop is removed, the tab stop MUST move to the radio that carries the active-item marker (the checked radio) if one is enabled, else to the first enabled radio, else index 0 (CMP-STA-27, CMP-ALG-15); when other radios are added or removed around it, the tab stop MUST stay on the same radio (CMP-STA-26). Server: relaxed, see §9.
  Source: `internals/composite/root/useCompositeRoot.ts:115-135,345-365`. Test: [L4] `RadioGroup.test.tsx` "moves the tab stop to the checked radio when the highlighted radio is removed".
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22: the `composite` module owns the roving `tabindex` from attach; ADR 13 §3): re-tagged from L2, because the tab stop after the composite's first publication is only a browser's to observe; the title runs in `radio-group.spec.ts`. C# renders `-1` on every radio and the active-item marker (RAD-DOM-02), asserted at L2 by `RadioRootTests` "port: inside a group the radio is a composite item whose tabindex the composite writes".
- **RDG-FOC-04** — MUST. Within a `Field.Root`, the field's `focused` flag MUST be set by a radio receiving focus (RAD-FOC-03) and MUST be cleared by the group only when focus leaves the group element (a `blur` whose related target is not inside the group, evaluated across shadow boundaries, UTL-DOM-20); focus moving between radios of the group MUST NOT clear it.
  Source: `radio-group/RadioGroup.tsx:230-234`, `radio/root/RadioRoot.tsx:167-173`, `packages/utils/src/shadowDom.ts:13`. Test: [L4] `RadioGroup.test.tsx` "is kept when a previously focused radio unmounts after focus moves to a sibling", "is reacquired when an arrow key moves focus from a disabled radio to an enabled sibling".
- **RDG-FOC-05** — MUST. On that same leaving `blur` the group MUST clear the pending-navigation flag (so a later refocus does not select), set the field's `touched` to `true`, and, when the field's validation mode is `'onBlur'`, commit a full validation of the current `checkedValue` (FFI-FRM-03).
  Source: `radio-group/RadioGroup.tsx:230-240`. Test: [L4] `RadioGroup.test.tsx` "does not select on refocus after an ignored arrow (%s)", "onBlur validates only when focus leaves the group".
- **RDG-FOC-06** — MUST. Within a `Field.Root`, the `focused` flag MUST also be cleared when the group becomes disabled or unmounts while one of its radios owns focus, and when the focused radio itself unmounts (RAD-FOC-04); it MUST NOT be cleared when a non-owning radio unmounts.
  Source: `radio/root/RadioRoot.tsx:79,87`, `internals/field-root-context/useSetFieldFocused.ts:22-43`; see FFI-STA-09. Test: [L4] `RadioGroup.test.tsx` "is removed when the focused group becomes disabled", "is removed when the focused group unmounts", "is removed when the focused radio unmounts but its group remains", "is kept when a previously focused radio unmounts after focus moves to a sibling".
- **RDG-FOC-07** — MUST. The control the group registers for focus-on-invalid-submit (FFI-FOC-01/04) MUST resolve, at focus time, to the root element of the *representative* radio: the first eligible (FFI-FRM-23) radio whose input is natively invalid, else the first eligible radio in mount order; when no radio is eligible nothing is focused but submission stays blocked (FFI-FOC-02).
  Source: `radio-group/RadioGroup.tsx:94-102,169`, `field/root/useFieldValidation.ts:51-66,118-121`, `form/Form.tsx:41-69`. Test: [L4] `RadioGroup.test.tsx` "focuses the first enabled radio when all radios start disabled", "validates and focuses the first radio after its ancestor fieldset is enabled".

### 6.4 Dismissal

Not applicable — no popup or dismissable surface.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: Value, eventDetails) => void` | A radio's hidden input reports a change (click on the radio, its label or the input; <kbd>Space</kbd>; arrow-key focus), before the state updates | `'none'` | yes (`cancel()`) |
| `onKeyDown`, `onBlur`, `onFocus`, … | standard handlers forwarded to the group element | per DOM; chained before the built-in handlers | — | consumer `preventBaseUIHandler()` suppresses the built-in handler (RND-EVT-03) |

- **RDG-EVT-01** — MUST. `onValueChange(value, details)` MUST be called synchronously inside the selecting radio's hidden-input change handling, before `checkedValue` and the DOM are updated (EVD-EVT-02), with `value` = that radio's `value`.
  Source: `radio-group/RadioGroup.tsx:82-92`, `radio/root/RadioRoot.tsx:206-208`. Test: [L2] `RadioGroup.test.tsx` "should call onValueChange when an item is clicked" (called once with `'a'`).
- **RDG-EVT-02** — MUST. `details.reason` MUST always be `'none'`; pointer, keyboard, label and arrow-navigation selections are not distinguished.
  Source: `radio/root/RadioRoot.tsx:206`, `radio-group/RadioGroup.tsx:332`; see EVD-EVT-28 (reason matrix row `RadioGroup.onValueChange`). Test: none.
- **RDG-EVT-03** — MUST. `details.event` MUST be the native `click` event delivered to the hidden input (the component-dispatched click for root/keyboard activation, carrying the originating modifier state; the browser's own click for label or direct-input activation; a plain click without modifiers for arrow-navigation selection). `details.trigger` is `undefined`.
  Source: `radio/root/RadioRoot.tsx:154,163,206`, `utils/dispatchClickWithModifiers.ts:20-41`, `internals/createBaseUIEventDetails.ts:138-155`. Test: [L4] `RadioGroup.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange" (`event.shiftKey === true`).
- **RDG-EVT-04** — MUST. If the callback calls `details.cancel()`, the selection MUST NOT change (either ownership mode), every hidden input's `checked` property MUST be restored to the current selection, and no field side effects (touched, dirty, filled, validation, error clearing) MUST occur — for root clicks, direct input clicks and arrow-key selection alike (focus still moves in the arrow case). Server: relaxed, see §9.
  Source: `radio-group/RadioGroup.tsx:86-88`, `radio/root/RadioRoot.tsx:210-214`; see EVD-EVT-05/08. Test: [L2] `RadioGroup.test.tsx` "does not change state when canceled via a root click", "does not change state when canceled via a hidden input click", "does not change state when canceled via arrow key navigation" (L4 for the root-click, arrow-key and `checked`-property restore clauses). Docs: CHANGELOG v1.6.0 "Honor canceled value changes".
  Port note 2026-10-23 (ADR 01 relaxation table, the row "C#-origin consumer vetoes of a toggle on a hidden input", 2026-10-22; design point R2 of M2 tracks 7–8, option (c), settled by the orchestrator 2026-10-23: within that ADR 01 row, where about two messages on a refused click are an accepted cost; ADR 16 rule 6 is about per-change reports; no `dom` module edit): on WebAssembly the `toggle` decision's `preventDefault()` makes the browser restore both the clicked input and the selected same-name one; on Server the refused radio raises its restore and asks the group to restore the radios that show the current value, one `dom` update each and no render, because the browser has natively unchecked the selected radio and only the clicked radio's handle hears of it (`RadioGroupTests` "port: a refused selection restores the clicked input and the selected one on Server"; `radio-group.spec.ts` "port: a refused selection restores the input of the selected radio, named or not" and "port: a canceled selection never checks the input on WebAssembly and is restored a round trip later on Server").
- **RDG-EVT-05** — MUST. `onValueChange` MUST NOT be called when `value` changes because the consumer changed the prop, nor on mount (EVD-STA-06).
  Source: `radio-group/RadioGroup.tsx:82-92` (only path is the radio change). Test: [L2] `RadioGroup.test.tsx` "revalidates when the controlled value changes externally" (validation runs; `onValueChange={setValue}` does not loop).
- **RDG-EVT-06** — MUST. `onValueChange` MUST be called exactly once per selection, and MUST NOT be called when the user activates the already-checked radio (the native radio input fires no `change` when it is already checked).
  Source: `radio/root/RadioRoot.tsx:196-215`; see EVD-STA-09. Test: [L2] `RadioGroup.test.tsx` "should call onValueChange when an item is clicked" (`calls.length === 1`) (L4 for the already-checked clause); already-checked case: none.
- **RDG-EVT-07** — MUST. `onValueChange` MUST NOT be called while the radio or group is disabled or read-only, for a radio whose `value` is `undefined`, or for an input click whose default was prevented before the component handled it (RAD-PTR-11).
  Source: `radio/root/RadioRoot.tsx:143,159,198-204`. Test: [L2] `RadioGroup.test.tsx` "should not change its state when clicked" (disabled and readOnly variants) (L4 for the pre-prevented input-click clause).
- **RDG-EVT-08** — MUST. `details.allowPropagation()` has no effect (nothing stops propagation of the originating user event; the internal input click's propagation stop, RAD-PTR-03, is unconditional); `isPropagationAllowed` merely reflects whether it was called.
  Source: `internals/createBaseUIEventDetails.ts:144-152`, `radio/root/RadioRoot.tsx:216-220`. Test: none.

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md) (§6.3 rows `Radio.Root` and `RadioGroup`). This section states what the group contributes; the per-radio hidden input is RAD-FRM-01..04.

**What the group registers with the field** (post-commit, pre-paint): control handle = a getter resolving to the representative radio's root (RDG-FOC-07); registration id = `id` prop or generated id; value = `checkedValue ?? null` (what `validate(value, formValues)` receives and the dirty baseline); form-value getter = RDG-FRM-06; name = the `name` prop (used when `Field.Root` has no `name`); registration is dropped while the group is disabled.

**Timing of field writes** (all post-commit, pre-paint):

1. On every change of `checkedValue` (not on mount): clear the enclosing `Form`'s external error for the effective name; `dirty ← checkedValue !== initialValue`; `filled ← checkedValue != null`; notify validation of the new value (`change(checkedValue)`); then re-point the forwarded input if the value was cleared (RDG-STA-09).
2. On a selection that is not canceled: the radio sets `touched ← true` (RAD-FRM-05).
3. On blur leaving the group: `touched ← true`, `focused ← false`, and in `'onBlur'` mode a full validation commit of `checkedValue` (RDG-FOC-05).

- **RDG-FRM-01** — MUST. Native form submission MUST include exactly one entry `name = serializeValue(value)` for the checked radio when it is enabled (RAD-FRM-02 for the string conversion), and no entry for the name when no radio is checked.
  Source: `radio/root/RadioRoot.tsx:183-194`; see FFI-FRM-28. Test: [L4] `RadioGroup.test.tsx` "should include selected radio value in form data", "should return null in form data when no radio is selected", "should return null when no radio is selected (matching native behavior)", "projects an enabled selected radio, matching native form data".
  Port note 2026-10-23: re-tagged from L2, because the cited titles build `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). They run in `radio-group.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **RDG-FRM-02** — MUST. A checked radio that is disabled (own, group, field or `<fieldset disabled>`) MUST NOT be submitted natively, and re-enabling it before submission MUST restore the entry.
  Source: `radio/root/RadioRoot.tsx:79,192`. Test: [L4] `RadioGroup.test.tsx` "excludes a disabled selected radio from onFormSubmit to match native form data", "includes a selected radio again when it is re-enabled before form submission", "excludes a radio disabled through an ancestor <fieldset disabled> to match native form data", "includes a selected radio after its ancestor fieldset is enabled".
  Port note 2026-10-23: re-tagged from L2, because the cited titles build `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). They run in `radio-group.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **RDG-FRM-03** — MUST. With `form="<id>"`, the radios MUST submit with the `<form>` of that id even when rendered outside it, and MUST be excluded from any other enclosing `Form`'s native data and values.
  Source: `radio/root/RadioRoot.tsx:185`, `field/root/useFieldValidation.ts:32-44`. Test: [L4] `RadioGroup.test.tsx` "submits to an external form when `form` is provided", "omits a radio associated to another form via the `form` prop".
- **RDG-FRM-04** — MUST. Radios rendered outside the `<form>` element without a `form` attribute (portaled) MUST still be validated and projected into `Form` values through the field registration, even though native submission omits them (FFI-FRM-23).
  Source: `radio-group/RadioGroup.tsx:160-164`, `field/root/useFieldValidation.ts:41-43`. Test: [L4] `RadioGroup.test.tsx` "includes a context-portaled radio without native form association in onFormSubmit", "includes a group fully portaled outside the form element in onFormSubmit".
  Port note 2026-10-23 (ADR 13 Amendments 2026-10-22; the CheckboxGroup portal precedent): open to M4: both titles render a portal, which no M2 part offers. The row is listed in the pull request by ID and does not hold the status.
- **RDG-FRM-05** — MUST. Within a `Field.Root`, the group MUST register as the field's control as described above and MUST unregister while disabled and on unmount (FFI-STR-07, FFI-ALG-12).
  Source: `radio-group/RadioGroup.tsx:94-102,169`, `internals/field-register-control/useRegisterFieldControl.ts:21-45`. Test: [L2] `RadioGroup.test.tsx` "onSubmit", "submits null to onFormSubmit when no radio is selected".
- **RDG-FRM-06** — MUST. The value projected into `Form` values (`onFormSubmit`, `formValues` passed to validators) MUST be `checkedValue ?? null` when the group is outside a `<Form>` element; inside one it MUST be `checkedValue ?? null` only if some registered radio input is checked **and** eligible (not `:disabled`, same form or unassociated, FFI-FRM-23), else `null` — so a disabled selected radio projects `null` (FFI-FRM-29).
  Source: `radio-group/RadioGroup.tsx:154-167`. Test: [L4] `RadioGroup.test.tsx` "submits null to onFormSubmit when no radio is selected", "excludes a disabled selected radio from onFormSubmit to match native form data", "excludes an initially disabled selected radio from onFormSubmit to match native form data", "projects an enabled selected radio, matching native form data".
  Port note 2026-10-23 (design point R6 of M2 tracks 7–8, option (a), settled by the orchestrator 2026-10-23; the CheckboxGroup precedent of CKG-ARIA-03/07): radios that share the group's labelable scope claim no control id, and the group claims the scope's control id as `null`, so each radio's input keeps its own id and receives its own submit state (RAD-DOM-10's note). Outside a `<form>` the projected value is the raw value (`RadioGroupFieldTests` "port: outside a form the projected value is the raw value").
- **RDG-FRM-07** — MUST. With `required` and no radio checked, submitting an enclosing `Form` MUST mark the field invalid with native `valueMissing` (`aria-invalid="true"` on the group and on every radio; `Field.Error match="valueMissing"` rendered), MUST block the submit and MUST focus the representative radio (RDG-FOC-07); selecting a radio afterwards MUST clear the error immediately.
  Source: `radio/root/RadioRoot.tsx:194`, `field/root/useFieldValidation.ts:194-223,244-276`, `form/Form.tsx:109-121`. Test: [L4] `RadioGroup.test.tsx` "triggers native HTML validation on submit", "clears required validation when a value is selected"; `RadioGroupFieldTests` "port: the focus target is the first eligible, natively invalid radio's root, else the first eligible one".
  Port note 2026-10-23 (ADR 02 Amendments 2026-10-22 item 1): "validates when inputRef is a function" is re-cited to `RadioGroupFieldTests` "port: the focus target is the first eligible, natively invalid radio's root, else the first eligible one", since `inputRef` is withdrawn.
- **RDG-FRM-08** — MUST. A checked radio that is disabled MUST still satisfy `required` (native radio-group validity), so submission MUST proceed with `null` projected and no `valueMissing` error.
  Source: `field/root/useFieldValidation.ts:51-66,194-223` (representative is the enabled, valid radio). Test: [L4] `RadioGroup.test.tsx` "submits null when the selected radio in a required group is disabled, matching native validity".
- **RDG-FRM-09** — MUST. After every radio of a `required` group unmounts, submission MUST no longer be blocked by `valueMissing` (no registered input remains, FFI-FRM-22) and the projected value is `null`; the field's custom `validate` MUST still run against `checkedValue ?? null` and MAY block submission.
  Source: `radio-group/RadioGroup.tsx:108-111,154-167`, `field/root/useFieldValidation.ts:228-242`. Test: [L2] `RadioGroup.test.tsx` "unblocks submission after every radio in the group unmounts", "runs the custom validator after every radio in the group unmounts".
- **RDG-FRM-10** — MUST. In on-change validation (mode `'onChange'`, or `'onSubmit'` after the first submit, FFI-FRM-01) `validate` MUST be invoked exactly once per change of `checkedValue` with the raw value, including a consumer-owned change that did not come from user interaction.
  Source: `radio-group/RadioGroup.tsx:171-177`, `field/root/useFieldValidation.ts:349-365`. Test: [L2] `RadioGroup.test.tsx` "revalidates when the controlled value changes externally", "onSubmit".
- **RDG-FRM-11** — MUST. In mode `'onBlur'`, moving focus between radios MUST NOT validate; validation MUST run once with `checkedValue` when focus leaves the group.
  Source: `radio-group/RadioGroup.tsx:231,236-238`. Test: [L4] `RadioGroup.test.tsx` "onBlur validates only when focus leaves the group".
- **RDG-FRM-12** — MUST. On every change of `checkedValue`, any external `Form` error registered under the effective name MUST be cleared (FFI-FRM-26).
  Source: `radio-group/RadioGroup.tsx:172`, `form/Form.tsx:143-155`. Test: [L2] `RadioGroup.test.tsx` "clears external errors on change".
- **RDG-FRM-13** — MUST. `Field.Label` association MUST be per radio through `Field.Item` scopes: each native `Field.Label` MUST carry `for` equal to its item's radio hidden-input id (root id when `nativeButton`), and clicking it (wrapping or sibling) MUST select that radio (RAD-FRM-06).
  Source: `radio/root/RadioRoot.tsx:118-119,186`, `field/item/FieldItem.tsx:44-46`; see FFI-STR-03, FFI-ARIA-04. Test: [L2] `RadioGroup.test.tsx` "associates implicitly" and "associates explicitly" (Field.Label). Docs: `components/radio-group/page.mdx` §Form integration; `handbook/forms/page.mdx`.
- **RDG-FRM-14** — MUST. `Field.Description`/`Field.Error` ids in the `Field.Root` scope MUST be appended to the group's `aria-describedby` (RDG-ARIA-06) and to every radio's (RAD-ARIA-07); ids inside a `Field.Item` MUST be appended only to that item's radio.
  Source: `radio-group/RadioGroup.tsx:258`, `radio/root/RadioRoot.tsx:246-249`. Test: [L2] `RadioGroup.test.tsx` "links the group and individual radios", "appends the id attribute of the error to aria-describedby of individual radios", "associates explicitly" (Field.Label, per-item description).
- **RDG-FRM-15** — MUST. A native form reset MUST NOT change the group's selection or field flags (FFI-FRM-30).
  Source: `radio-group/RadioGroup.tsx:1-23` (no reset listener). Test: none.

## 9. Lifecycle, mounting and animation

Not applicable — the group is always mounted; `Radio.Indicator` mounting is specified in [radio](radio.md) §9.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- RDG-FOC-01 — CMP-STA-10 row: the composite's first publication (and so the checked radio's `tabindex="0"`) lands after paint plus a round trip; observable only as latency of the roving `tabindex`.
- RDG-FOC-03 — CMP-STA-10 row: the registry rebuild after the tab-stop radio is removed lands after paint plus a round trip; observable only as latency of the moved `tabindex="0"`.
- RDG-EDGE-06 — CMP-STA-10 row: after hydration the checked radio's `tabindex="0"` arrives with the first publication, after paint plus a round trip.
- RDG-EVT-04, RDG-STA-03 — the row "C#-origin consumer vetoes of a toggle on a hidden input" (added 2026-10-22): after a `cancel()`, or a controlled consumer that does not pass the value back, the clicked input stays checked on Server, and the selected same-name radio unchecked, until the `dom` primitive's restores arrive, about two round trips; WebAssembly reverts both in the same dispatch. The radios' `aria-checked` and `data-*`, which C# renders, never change.
- RDG-API-12, RDG-KEY-01 — the row "SLD-PTR-01 (default-prevented clause), EVD-EVT-26 against composite/button key handling, drag-start vetoes generally" (amended 2026-10-23; CMP-EVT-12): for an arrow key the composite handles, a consumer C# `onkeydown` on the focused radio and on the group runs once each; on WebAssembly inside the `navigate` decision, where `PreventPartHandler()` vetoes the move, on Server a round trip later from the handled-key report, too late to veto. A JavaScript listener's `preventDefault()` is honoured on both modes.
- RDG-STA-05 — FFI registration row: the `filled`/`dirty` writes after a change of `checkedValue` land after paint plus a round trip; observable only as latency of `data-filled`/`data-dirty`.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `CompositeRoot` | [CMP](../architecture/composite-and-list-navigation.md) §6.1 | `tag` default `'div'`; `render`/`className`/`style`; `state` = §3.1 state object; `stateAttributesMapping` = field validity mapping; `props: [defaultProps, elementProps, (p) => validation.getValidationProps(disabled ?? false, p)]`; `refs: [forwardedRef]`; `enableHomeAndEndKeys: false`; `modifierKeys: ['Shift']`; **not passed** (mechanism defaults apply): `orientation` (`'both'`), `loopFocus` (`true`), `stopEventPropagation` (`true`), `highlightedIndex`/`onHighlightedIndexChange` (root-owned), `disabledIndices` (DOM detection), `grid`, `onLoop`, `onMapChange`, `rootRef`, `highlightItemOnHover` (`false`). Direction from the nearest `DirectionProvider` (CMP-I18N-01). |
| `CompositeItem` | CMP §6.4 | Used by `Radio.Root` (RAD §11): `tag: 'span'`, no `metadata`. |
| Active item marker | CMP-DOM-02 | `data-composite-item-active=""` on the checked radio (RAD-DOM-02); read at first publication (CMP-STA-23) and as removal fallback (CMP-STA-27). |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `{ controlled: value, default: defaultValue, name: 'RadioGroup', state: 'value' }` |
| `useValueChanged` | UTL (UTL-STA-36) | watches `checkedValue`: error clearing, dirty, filled, validation change, forwarded-input fallback |
| `useMergedRefs` | UTL | merges the internal forwarded-input handle with the consumer `inputRef` |
| `contains` | UTL-DOM-20 | blur boundary check `contains(groupElement, relatedTarget)` |
| `useFieldRootContext`, `useRegisterFieldControl`, `useFormContext` | [FFI](../architecture/field-and-form-infrastructure.md) | registration `(controlGetter → representative radio root, id ?? generatedId, checkedValue ?? null, getFormValue, enabled = !disabled, nameProp)`; `clearErrors(effectiveName)`; `setDirty`, `setFilled`, `setTouched(true)`, `setFocused(false)`; `validation.change(checkedValue)`; `validation.commit(checkedValue)` on leaving blur in `'onBlur'` mode; `validation.getValidationProps(disabled ?? false, props)` on the group element; `validation.getInputControl()` and `validation.registeredInputs` read; `validation.inputRef` never written |
| `useLabelableContext`, `useFieldsetRootContext` | FFI | `aria-labelledby = labelId ?? fieldsetLegendId` (FFI-ARIA-09) |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | created by `Radio.Root` with reason `'none'` and the native input click; the group only reads `isCanceled` |
| `mergeProps` / `useRenderElement` | [RND](../architecture/rendering-and-composition.md) | via `CompositeRoot`; consumer handlers run before built-in ones |

## 12. Edge cases, invariants and dev warnings

All dev messages are emitted only outside production, through `console.error`, prefixed `Base UI: ` and at most once per message (UTL-EDGE-11/12).

- **RDG-EDGE-01** — SHOULD. Switching `value` between `undefined` and defined after the first render, or changing `defaultValue` of a component-owned group, SHOULD log the `useControlled` dev errors of UTL-EDGE-22/23 with `name: 'RadioGroup'`, `state: 'value'`; the rendered selection is unaffected.
  Source: `radio-group/RadioGroup.tsx:74-79`, `packages/utils/src/useControlled.ts:48-80`. Test: none.
- **RDG-EDGE-02** — MUST. Radios with identical `value`s are all checked when that value is selected (each carries the active marker; the first in document order takes the initial tab stop) and each submits its own entry; the behavior is defined but not a supported configuration.
  Source: `radio/root/RadioRoot.tsx:84,133`, `internals/composite/root/useCompositeRoot.ts:140-143`. Test: [L2] `RadioIndicator.test.tsx` "should remove the indicator when there is no exit animation defined" (two radios with value `"a"`).
- **RDG-EDGE-03** — MUST. A `value`/`defaultValue` of `null` is an ordinary value: a radio whose own `value` is `null` is checked by it, `filled` is `false`, the projected form value is `null`, and the forwarded input falls back to the first enabled radio.
  Source: `radio-group/RadioGroup.tsx:157,175,180`, `radio/root/RadioRoot.tsx:84`. Test: [L2] `RadioRootTests` "port: a radio whose value is null is never selected (allows `null` value)"; `RadioGroupTests` "port: a null value selects no radio".
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22: `null` means nothing selected; ADR 02 Amendments 2026-10-22 item 1): not ported as an ordinary value. A `value` or `DefaultValue` of `null` checks no radio, a radio whose own `Value` is `null` is never checked and never selects (RAD-EVT-03), `filled` is `false` and the projected value is `null`; the forwarded-input clause is withdrawn with `inputRef`. "allows `null` value" is restated as `RadioRootTests` "port: a radio whose value is null is never selected (allows `null` value)", and "keeps inputRef pointing to the first radio when the value is cleared" is re-cited to `RadioGroupTests` "port: a null value selects no radio".
- **RDG-EDGE-04** — MUST. `readOnly` MUST NOT affect focusability, the roving tab stop, arrow-key focus movement or form submission; it only blocks selection (the arrow-driven focus still moves but the focused radio does not select) and adds `aria-readonly`/`data-readonly`.
  Source: `radio-group/RadioGroup.tsx:201,228`, `radio/root/RadioRoot.tsx:143,159,202`. Test: [L2] `RadioGroup.test.tsx` "should not change its state when clicked" (readOnly) (L4 for the arrow-key focus clause); arrow case: none.
- **RDG-EDGE-05** — MUST. The only direction-dependent behavior is the horizontal arrow mapping (RDG-KEY-01); no locale dependence exists.
  Source: `internals/composite/root/CompositeRoot.tsx:43`, `internals/composite/root/useCompositeRoot.ts:221-224`. Test: [L4] `RadioGroup.test.tsx` "moves focus with arrow keys and Tab" (rtl).
- **RDG-EDGE-06** — MUST. Server-rendered markup MUST match the client's initial render: the group element with its ARIA attributes and (when given) `id`; radios render `tabindex="-1"` until the first publication unless their index is `0` (CMP-STA-15; the checked radio's `tabindex="0"` is applied post-hydration), with deterministic generated ids. Server: relaxed, see §9.
  Source: `internals/composite/item/useCompositeItem.ts:19-25`, `internals/composite/list/useCompositeListItem.ts:43-55`. Test: none.
- **RDG-EDGE-07** — MUST. A radio inside the group that is disabled through `<fieldset disabled>` (not through props) is natively `:disabled` (excluded from submission and from the field's eligible inputs, RDG-FRM-02/06) but its `disabled` property is `false`, so it is still eligible as the forwarded input and the composite still treats a non-native radio as enabled (no `aria-disabled`); a `nativeButton` radio's `<button>` matches `:disabled` and is skipped by arrow navigation.
  Source: `radio-group/RadioGroup.tsx:130,136`, `field/root/useFieldValidation.ts:33-35`, `floating-ui-react/utils/composite.ts:546-548`. Test: [L4] `radio-group.spec.ts` "port: a <fieldset disabled> skips a native-button radio in arrow navigation and leaves the other radios enabled"; `RadioGroup.test.tsx` "excludes a radio disabled through an ancestor <fieldset disabled> to match native form data".
  Port note 2026-10-23 (ADR 02 Amendments 2026-10-22 item 1): the forwarded-input clause is withdrawn with `inputRef`. "keeps inputRef available after an ancestor fieldset is enabled" is re-cited to `radio-group.spec.ts` "port: a <fieldset disabled> skips a native-button radio in arrow navigation and leaves the other radios enabled".
- **RDG-EDGE-08** — MUST. Nested groups are not supported: a radio belongs to the nearest enclosing group only (its shared state is the nearest provider's), and the inner group's composite root captures arrow keys first.
  Source: `radio/root/RadioRoot.tsx:54`, `internals/composite/root/useCompositeRoot.ts:303-304`. Test: none.

Inherited dev checks (not re-documented): `nativeButton` mismatch errors on each radio (RAD-EDGE-02), invalid `render` element error and component-looking render callback warning from RND.

## 13. Behaviors evidenced by tests

### `RadioGroup.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; no `testRenderPropWith`, no `button`) | RDG-DOM-07 |
| "can override the built-in attributes" | RDG-DOM-06, RDG-ARIA-01 |
| "is forwarded to the root element" | RDG-API-10, RDG-DOM-01 |
| "should call onValueChange when an item is clicked" | RDG-EVT-01, RDG-EVT-06, RDG-API-03 |
| "should report keyboard modifier event properties when calling onCheckedChange" | RDG-EVT-03, RAD-PTR-01 |
| "should select an item with Space on keyup" | RAD-KEY-01 |
| "should not select an item with Enter" | RAD-KEY-02 |
| "does not change state when canceled via a root click" / "does not change state when canceled via a hidden input click" / "does not change state when canceled via arrow key navigation" | RDG-EVT-04, RDG-STA-05 |
| "should have the `aria-disabled` attribute" | RDG-ARIA-03, RDG-API-04, RAD-ARIA-06, RAD-DOM-09 |
| "should not have the aria attribute when `disabled` is not set" | RDG-ARIA-03 |
| "should not change its state when clicked" (disabled) | RDG-EVT-07, RAD-PTR-08 |
| "should have the `aria-readonly` attribute" / "should not have the aria attribute when `readOnly` is not set" | RDG-ARIA-04 |
| "should not change its state when clicked" (readOnly) | RDG-EVT-07, RAD-PTR-07, RDG-EDGE-04 |
| "should update its state if the underlying input is toggled" | RDG-STA-02, RAD-PTR-04 |
| "should place the style hooks on the root and subcomponents" | RDG-DOM-02, RDG-STA-01, RAD-DOM-03, RAD-DOM-04, RAD-DOM-15 |
| "should set the name attribute on each radio input" | RDG-API-07, RAD-DOM-09, RAD-DOM-11 |
| "points inputRef to the checked radio input when present" | RDG-API-09, RDG-STA-07 |
| "allows reading inputRef.current in an effect" | RDG-STA-10 |
| "supports inputRef as a function" | RDG-API-09, RDG-STA-07 |
| "does not detach a stable inputRef callback on unrelated re-renders" | RDG-STA-10 |
| "transfers the current input when the object inputRef changes" | RDG-API-09, RDG-STA-10 |
| "detaches the previous callback inputRef when it changes (cleanup: %s)" (2 variants: `false`, `true`) | RDG-API-09, RDG-STA-10 |
| "cleans up inputRef bindings when selecting, clearing, and unmounting" | RDG-STA-03, RDG-STA-08, RDG-STA-09 |
| "detaches inputRef when an initially disabled radio is enabled and then unmounted" | RDG-STA-07, RDG-STA-08 |
| "skips disabled radios when assigning inputRef" | RDG-STA-07 |
| "points inputRef to the first radio input when nativeButton wraps a button" | RDG-STA-07, RAD-EDGE-09 |
| "keeps inputRef pointing to the first radio when the value is cleared" | RDG-STA-09, RDG-EDGE-03 |
| "detaches inputRef when its current radio unmounts" / "detaches inputRef when a radio selected after mount unmounts" | RDG-STA-08 |
| "should return null when no radio is selected (matching native behavior)" / "should return null in form data when no radio is selected" | RDG-FRM-01, RDG-STR-01 |
| "should include selected radio value in form data" | RDG-FRM-01 |
| "should automatically select radio upon navigation" | RDG-KEY-03, RDG-STA-06, RDG-STA-05, RAD-FOC-05, RAD-FRM-05 |
| "does not select on refocus after an ignored arrow (%s)" (4 variants: `metaKey`, `ctrlKey`, `altKey`, `single`) | RDG-KEY-02, RDG-KEY-04, RDG-STA-06, RDG-FOC-05 |
| "moves focus with arrow keys and Tab" (2 variants from `describe.each` "direction: $direction": `ltr`, `rtl`) | RDG-KEY-01, RDG-KEY-06, RDG-FOC-02, RDG-EDGE-05 |
| "when Shift is pressed arrow keys move focus normally" (2 variants: `ltr`, `rtl`) | RDG-KEY-02 |
| "moves the tab stop to the checked radio when the highlighted radio is removed" | RDG-FOC-03 |
| "should apply data-checked and data-unchecked to radio root and indicator" | RAD-DOM-03, RAD-DOM-15, RAD-STA-04, RDG-PTR-01 |
| "does not forward `value` prop" | RDG-API-12, RDG-DOM-05 |
| "sets tabIndex=0 to the correct element initially" | RDG-FOC-01, RAD-FOC-01 |
| "associates implicitly" / "associates explicitly" (native `<label>`) | RAD-PTR-05, RAD-PTR-06 |
| "passes the `name` prop to the radio input" | RDG-API-07 |
| "is removed when the focused group becomes disabled" / "is removed when the focused group unmounts" | RDG-FOC-06 |
| "is kept when a previously focused radio unmounts after focus moves to a sibling" | RDG-FOC-04, RDG-FOC-06 |
| "is removed when the focused radio unmounts but its group remains" | RDG-FOC-06, RAD-FOC-04 |
| "is not acquired when focus lands on a disabled radio" / "is not acquired when a radio inherits disabled from Field.Item" | RAD-FOC-03, RAD-API-02 |
| "is not reacquired when a radio disabled while focused is refocused" / "is reacquired when a radio that kept focus while disabled is re-enabled" | RAD-FOC-04 |
| "is reacquired when an arrow key moves focus from a disabled radio to an enabled sibling" | RDG-FOC-04, RAD-FOC-03 |
| "should receive disabled prop from Field.Root" | RDG-API-04, RDG-ARIA-03, RDG-DOM-02 |
| "should receive name prop from Field.Root" | RDG-API-07 |
| "revalidates when the controlled value changes externally" | RDG-FRM-10, RDG-EVT-05, RDG-STA-03 |
| "associates implicitly" / "associates explicitly" (Field.Label) | RDG-FRM-13, RDG-FRM-14, RAD-FRM-06 |
| "links the group and individual radios" | RDG-ARIA-06, RDG-FRM-14, RAD-ARIA-07 |
| "onSubmit" | RDG-FRM-05, RDG-FRM-10, RDG-ARIA-07 |
| "onBlur validates only when focus leaves the group" | RDG-FRM-11, RDG-FOC-05 |
| "keeps inputRef available after an ancestor fieldset is enabled" | RDG-EDGE-07, RDG-STA-07 |
| "labels the radio group from the fieldset legend" | RDG-ARIA-05, RDG-STR-04 |
| "updates label precedence without retaining replaced or unmounted IDs" | RDG-ARIA-05, RDG-DOM-06 |
| "submits to an external form when `form` is provided" | RDG-API-08, RDG-FRM-03 |
| "triggers native HTML validation on submit" | RDG-FRM-07, RDG-API-06 |
| "submits null to onFormSubmit when no radio is selected" | RDG-FRM-05, RDG-FRM-06 |
| "unblocks submission after every radio in the group unmounts" / "runs the custom validator after every radio in the group unmounts" | RDG-FRM-09, RDG-STA-11, RDG-STR-03 |
| "excludes a disabled selected radio from onFormSubmit to match native form data" / "includes a selected radio again when it is re-enabled before form submission" / "excludes an initially disabled selected radio from onFormSubmit to match native form data" | RDG-FRM-02, RDG-FRM-06 |
| "projects an enabled selected radio, matching native form data" | RDG-FRM-01, RDG-FRM-06 |
| "excludes a radio disabled through an ancestor <fieldset disabled> to match native form data" / "includes a selected radio after its ancestor fieldset is enabled" | RDG-FRM-02, RDG-EDGE-07 |
| "omits a radio associated to another form via the `form` prop" | RDG-FRM-03 |
| "includes a context-portaled radio without native form association in onFormSubmit" / "includes a group fully portaled outside the form element in onFormSubmit" | RDG-FRM-04, RDG-STR-02 |
| "submits null when the selected radio in a required group is disabled, matching native validity" | RDG-FRM-08 |
| "clears required validation when a value is selected" | RDG-FRM-07, RAD-ARIA-08 |
| "validates when inputRef is a function" | RDG-FRM-07, RDG-API-09 |
| "focuses the first enabled radio when all radios start disabled" / "validates and focuses the first radio after its ancestor fieldset is enabled" | RDG-FOC-07 |
| "clears external errors on change" | RDG-FRM-12 |
| "appends the id attribute of the error to aria-describedby of individual radios" | RDG-FRM-14, RAD-ARIA-07 |

### Additional requirements introduced here

- **RDG-DOM-07** — MUST. The group MUST pass the shared conformance suite of RND (RND-DOM-36..48) with `refInstanceof: HTMLDivElement`, default substitute element `div`, no `nativeButton` pass.
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] `RadioGroup.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Change detection on the hidden input.** As for Switch, React's `onChange` on a radio input is driven by its `click` event; `event.nativeEvent` is that click and carries modifier keys (RDG-EVT-03). A port without real radio inputs must synthesize an equivalent event and must itself uncheck the previously selected radio. Port: see decisions/04-js-interop-boundary.md §3 (`details.event` is a snapshot record) and decisions/05-state-store-and-event-details.md §3.
- **Controlled-input restoration after `cancel()`.** React re-applies the `checked` prop to every hidden input after a canceled change; a port must re-sync the inputs explicitly (RDG-EVT-04). Port: see decisions/05-state-store-and-event-details.md §2 (`Value`/`ValueChanged`/`DefaultValue` plus `OnValueChange(details)`; cancel stops the request).
- **Layout-effect timing.** Field writes (`useValueChanged`), control registration, radio (re)registration for the forwarded input, `aria-labelledby` resolution and the composite's default-index adjustment all run post-commit, pre-paint. The forwarded-input re-application on `inputRef` identity change is also a layout effect, so a consumer layout effect sees `inputRef.current` populated. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, timing-term mapping) and §Relaxation table (rows CMP-STA-10 and "FFI registration": after paint plus a round trip on Server).
- **DOM-handle merging.** `useMergedRefs` composes the internal and consumer `inputRef` into one callback whose identity changes with the consumer ref; React re-invokes callback refs on identity change, which is what "transfers the current input when the object inputRef changes" observes. The group additionally dedupes re-application (`lastAppliedInputRef`) so unrelated renders do not call the consumer callback. React 19 callback-ref cleanups are honored (`cleanup: true` variant); legacy refs are called with `null`. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24: DOM handles become `ElementReference`).
- **Capture-phase keydown.** `onKeyDownCapture` sets the pending-navigation flag before the composite's bubbling `onKeyDown` and before any radio handler; a port needs an equivalent "runs first" hook. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: roving focus and per-key `preventDefault` are JS; C# receives active-index changes) and §3 (`decide()` for composite key vetoes).
- **`contains(currentTarget, relatedTarget)`** is the shadow-DOM-aware helper; `relatedTarget` is `null` when focus leaves the document, which counts as leaving the group. Port: see decisions/04-js-interop-boundary.md §1 (`dom` module: owner/shadow DOM helpers).
- **Composite registration.** Radios register through callback refs in `CompositeItem`; the index is `-1` until the first publication (so SSR markup has `tabindex="-1"` on every radio except index 0's default highlight), then the checked radio's marker is read once (CMP-STA-23). Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry move detection) and decisions/01-hosting-model-and-timing.md §Relaxation table (row CMP-STA-10).
- **Stable callbacks.** `setCheckedValue`, `registerInputRef`, `setInputRef`, `getFormValue` have fixed identities and read current props at call time; handlers in a port must not capture stale values.
- **`id` for registration vs. element.** `useBaseUiId(idProp)` returns the prop or a generated id (React ≥ 18 `useId`; React 17 assigns after mount), used only for the field registry; the element gets the raw prop. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5, generated ids from a scoped counter).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| RDG-STR-01 | MUST | one host element, no own input | 2 |
| RDG-STR-02 | MUST | composite root for radios at any depth/portal | 2 |
| RDG-STR-03 | MUST | zero radios valid | 2 |
| RDG-STR-04 | MUST | substitutable into Fieldset.Root | 2 |
| RDG-API-01 | MUST | `value` defined on first render → consumer-owned; null is a value | 3.1 |
| RDG-API-02 | MUST | initial = defaultValue; later changes ignored | 3.1 |
| RDG-API-03 | MUST | `onValueChange(value, details)` signature | 3.1 |
| RDG-API-04 | MUST | effective disabled = field OR prop; published; unregisters | 3.1 |
| RDG-API-05 | MUST | readOnly published; blocks selection; aria-readonly | 3.1 |
| RDG-API-06 | MUST | required → inputs `required`; aria-required | 3.1 |
| RDG-API-07 | MUST | effective name = field ?? prop; on inputs only | 3.1 |
| RDG-API-08 | MUST | `form` on inputs only | 3.1 |
| RDG-API-09 | MUST | `inputRef` → forwarded input; object/callback; detach. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 3.1 |
| RDG-API-10 | MUST | `id` verbatim on element; registration id | 3.1 |
| RDG-API-11 | MUST | className/style/render per RND | 3.1 |
| RDG-API-12 | MUST | other props forwarded; `value` not | 3.1 |
| RDG-STA-01 | MUST | state object exactly eight fields | 3.1 |
| RDG-STA-02 | MUST | component-owned selection stores value by identity | 4 |
| RDG-STA-03 | MUST | consumer-owned: DOM follows prop | 4 |
| RDG-STA-04 | MUST | controlled → undefined falls back to default | 4 |
| RDG-STA-05 | MUST | filled/dirty on change; filled on checked mount | 4 |
| RDG-STA-06 | MUST | pending-navigation flag set/cleared | 4 |
| RDG-STA-07 | MUST | forwarded input selection on registration. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 4 |
| RDG-STA-08 | MUST | forwarded input cleared on unmount. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 4 |
| RDG-STA-09 | MUST | value cleared → first enabled input. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 4 |
| RDG-STA-10 | MUST | inputRef (re)application rules. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 4 |
| RDG-STA-11 | MUST | never writes shared validation input handle | 4 |
| RDG-DOM-01 | MUST | `id` verbatim or absent | 5.1 |
| RDG-DOM-02 | MUST | data-disabled/readonly/required | 5.1 |
| RDG-DOM-03 | MUST | field data attributes only within Field | 5.1 |
| RDG-DOM-04 | MUST | data-valid/invalid/neither | 5.1 |
| RDG-DOM-05 | MUST | no tabindex/aria-orientation/name/value/form | 5.1 |
| RDG-DOM-06 | MUST | consumer attributes override; handlers chain | 5.1 |
| RDG-ARIA-01 | MUST | role radiogroup | 5.3 |
| RDG-ARIA-02 | MUST | aria-required | 5.3 |
| RDG-ARIA-03 | MUST | aria-disabled | 5.3 |
| RDG-ARIA-04 | MUST | aria-readonly | 5.3 |
| RDG-ARIA-05 | MUST | aria-labelledby precedence prop → Field.Label → Legend | 5.3 |
| RDG-ARIA-06 | MUST | aria-describedby = consumer + root-scope ids | 5.3 |
| RDG-ARIA-07 | MUST | aria-invalid when field invalid | 5.3 |
| RDG-KEY-01 | MUST | arrows both orientations, RTL, loop, focus after microtask | 6.1 |
| RDG-KEY-02 | MUST | Shift allowed; Ctrl/Alt/Meta block | 6.1 |
| RDG-KEY-03 | MUST | arrow sets flag; focused radio selects; one callback | 6.1 |
| RDG-KEY-04 | MUST | disabled radios skipped | 6.1 |
| RDG-KEY-05 | MUST | Home/End and other keys not handled | 6.1 |
| RDG-KEY-06 | MUST | Tab not intercepted | 6.1 |
| RDG-PTR-01 | MUST | click selects and moves tab stop | 6.2 |
| RDG-FOC-01 | MUST | initial tab stop: checked, else first enabled | 6.3 |
| RDG-FOC-02 | MUST | tab stop follows focus, not value | 6.3 |
| RDG-FOC-03 | MUST | removal fallback to checked radio | 6.3 |
| RDG-FOC-04 | MUST | field focused cleared only on leaving blur | 6.3 |
| RDG-FOC-05 | MUST | leaving blur: flag cleared, touched, onBlur commit | 6.3 |
| RDG-FOC-06 | MUST | focused cleared on disable/unmount of owner | 6.3 |
| RDG-FOC-07 | MUST | invalid-submit focus target = representative radio | 6.3 |
| RDG-EVT-01 | MUST | callback before state update | 7 |
| RDG-EVT-02 | MUST | reason 'none' | 7 |
| RDG-EVT-03 | MUST | event = native input click; trigger undefined | 7 |
| RDG-EVT-04 | MUST | cancel(): no change, inputs restored, no field effects | 7 |
| RDG-EVT-05 | MUST | not for prop changes/mount | 7 |
| RDG-EVT-06 | MUST | once per selection; not for already-checked | 7 |
| RDG-EVT-07 | MUST | not when disabled/readOnly/undefined value/pre-canceled | 7 |
| RDG-EVT-08 | MUST | allowPropagation no effect | 7 |
| RDG-FRM-01 | MUST | FormData entry for checked enabled radio | 8 |
| RDG-FRM-02 | MUST | disabled checked radio not submitted | 8 |
| RDG-FRM-03 | MUST | `form` attribute → external form only | 8 |
| RDG-FRM-04 | MUST | portaled radios still registered | 8 |
| RDG-FRM-05 | MUST | field registration; unregister when disabled | 8 |
| RDG-FRM-06 | MUST | Form value projection (eligible-only) | 8 |
| RDG-FRM-07 | MUST | required → valueMissing on submit; focus; clears on select | 8 |
| RDG-FRM-08 | MUST | disabled checked radio satisfies required | 8 |
| RDG-FRM-09 | MUST | all radios unmounted: no valueMissing; validate still runs | 8 |
| RDG-FRM-10 | MUST | validate once per change in on-change modes | 8 |
| RDG-FRM-11 | MUST | onBlur mode validates on leaving the group | 8 |
| RDG-FRM-12 | MUST | external error cleared on change | 8 |
| RDG-FRM-13 | MUST | Field.Label per Field.Item; click selects | 8 |
| RDG-FRM-14 | MUST | description/error ids on group and radios | 8 |
| RDG-FRM-15 | MUST | form reset ignored | 8 |
| RDG-EDGE-01 | SHOULD | useControlled dev errors | 12 |
| RDG-EDGE-02 | MUST | duplicate values all checked | 12 |
| RDG-EDGE-03 | MUST | null is an ordinary value | 12 |
| RDG-EDGE-04 | MUST | readOnly only blocks selection | 12 |
| RDG-EDGE-05 | MUST | only RTL arrow mapping is direction-dependent | 12 |
| RDG-EDGE-06 | MUST | SSR markup matches; tabindex before publication | 12 |
| RDG-EDGE-07 | MUST | fieldset-disabled radios: native vs property | 12 |
| RDG-EDGE-08 | MUST | nested groups unsupported; nearest wins | 12 |
| RDG-DOM-07 | MUST | passes RND conformance suite | 13 |

## 16. Open questions

1. **CMP open question Q9 is answered differently than it assumes.** `radio-group/RadioGroup.tsx:250-264` passes neither `highlightedIndex` nor `onHighlightedIndexChange`; the group relies on the active-item marker at first publication (CMP-STA-23) and on removal fallback (CMP-STA-27/ALG-15), plus focus-driven highlighting (CMP-PTR-02). Consequently a consumer-owned `value` change made programmatically does not move the tab stop (RDG-FOC-02); no test covers this. The CMP doc's Q9 statement ("RadioGroup keeps the tab stop in sync through `highlightedIndex`") should be corrected to "RadioGroup relies on the marker and focus". Resolved: composite-and-list-navigation.md §10 Q9 now states that RadioGroup passes neither prop and relies on the marker at first publication plus focus-driven highlighting, matching RDG-FOC-02.
2. **Data-attribute outline discrepancy.** `RadioGroupDataAttributes.ts:1-4` and the API outline list only `data-disabled`, but `RadioGroup.tsx:187-192,261` emits `data-readonly`, `data-required`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid`, and the test "should place the style hooks on the root and subcomponents" asserts `data-readonly`/`data-required`. Documented as emitted (RDG-DOM-02..04).
   Recorded 2026-10-23 (M2 track 8): `RadioGroupDataAttributes` lists only `Disabled`, as Base UI's constants do; the state record's other fields render through the generator's projection and the field validity mapping (`RadioGroupTests` "should place the style hooks on the root and subcomponents").
3. **Hinted props `orientation` and `loopFocus` do not exist** on `RadioGroup` in v1.9.0 (`RadioGroup.tsx:282-330`); the composite runs with `'both'` and looping fixed. The CMP doc's Q11 lists `orientation`/`loopFocus` as appearing on the RadioGroup outline entry — they do not (`components/page.mdx:1485-1487`). Resolved: composite-and-list-navigation.md §10 Q11 now states that RadioGroup has no `orientation` or `loopFocus` prop and runs with the mechanism defaults, matching RDG-KEY-01.
4. **Required group without a name.** Each hidden input carries `required` (`RadioRoot.tsx:194`); without an effective `name` every input is its own native radio group, so an unchecked required radio reports `valueMissing` even after another radio was selected, and the representative-input rule (FFI-FRM-24) then marks the field invalid once dirty. All tests supply a name through `Field.Root` or `name`. A port should document or avoid this configuration.
   Resolved 2026-10-23 (design point R5 of M2 tracks 7–8, settled by the orchestrator 2026-10-23, as the brief recommends): document only, in the XML remarks of `Required` and on the sample page; no development warning, which Base UI does not have. The remarks of `RadioGroup<TValue>.Required` say a required group needs a name, from `Name` or `Field.Root`.
5. **First-enabled fallback after unmount.** `RadioGroup.tsx:144-147` clears the recorded first enabled input when it unmounts but picks no successor until another radio re-registers (on its own checked/disabled change or mount), so clearing the value right after unmounting the first radio leaves `inputRef` null. Untested; the spec (RDG-STA-08/09) transcribes it.
   Resolved 2026-10-23 by the `inputRef` withdrawal (ADR 02 Amendments 2026-10-22 item 1): RDG-API-09 and RDG-STA-07..10 are withdrawn.
6. **Group `touched` on selection.** The field's `touched` is set by the radio on every non-canceled selection (`RadioRoot.tsx:214`), not only on blur — unlike Switch/Checkbox. Intentional per the tests ("should automatically select radio upon navigation" asserts `data-touched` without blur); noted for the FFI control matrix (table B says "on change").
   Resolved 2026-10-23 as tested: a non-canceled selection sets `touched` without a blur (RAD-FRM-05; `radio-group.spec.ts` "should automatically select radio upon navigation", `RadioGroupFieldTests` "port: a selection touches the field and fills it").
7. **`aria-label` on the group.** A consumer `aria-label` is forwarded but does not suppress `aria-labelledby` from a Field label/legend (`RadioGroup.tsx:185,229`), unlike the radios (RAD-ARIA-03). Probably intended (labelledby wins in AT); recorded for consistency review.
   Resolved 2026-10-23 as RDG-ARIA-05 states (`RadioGroupTests` "port: a consumer aria-label does not suppress the group's aria-labelledby").
8. **Explicit `aria-labelledby={undefined}`.** Because the consumer's props merge after `defaultProps`, passing an explicit `undefined` clobbers the Field/legend id (RND-DOM-19). Untested; a port that treats `undefined` as "absent" would differ only in this corner. Resolved: decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port relaxes RND-DOM-19 (an explicit `null`/`undefined` attribute emits no frame), so in the port the Field/legend id is kept.
