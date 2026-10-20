# OTP Field

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/otp-field/` |
| Tests | `packages/react/src/otp-field/**/*.test.tsx`, `*.test.ts` (5 files) + 2 `*.spec.tsx` type tests |
| Docs page | `docs/src/app/(docs)/react/components/otp-field/page.mdx` |
| Prefix | `OTP` |
| Status | approved |
| Builds on | none (`OTPField.Separator` is a re-export of the standalone [separator](separator.md)) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

OTP Field is a one-time-password / verification-code entry control. The user sees `length` single-character boxes (`OTPField.Input`, "slots") inside a group (`OTPField.Root`); typing fills a slot and advances to the next one, pasting a whole code fills consecutive slots, <kbd>Backspace</kbd> walks backwards, arrow keys move between slots, and the value is exposed to the consumer as a single string. Slots may be wrapped in arbitrary layout elements and separated by `OTPField.Separator` for presentations such as `123-456`.

The input model is **one real `<input>` per slot plus one visually hidden `<input type="text">`** rendered as a sibling of the root. The hidden input carries the whole string, the `name`, native constraints (`required`, `minlength`/`maxlength`, `pattern`) and the `one-time-code` autocomplete hint, so it is the form-participation element, the target of browser/password-manager autofill, and the focus-redirect source for native validation bubbles. The visible slots are never named; only one slot at a time is in the tab order (the first empty slot, or the focused slot while the field has focus). Every character entered by any path (typing, IME composition, paste, autofill, default/controlled values) passes through one normalization pipeline: whitespace is stripped, characters are filtered by `validationType` (`numeric` by default, `alpha`, `alphanumeric`, or `none`), an optional `normalizeValue` function runs, the result is filtered again and clamped to `length`. Rejected characters are reported through `onValueInvalid`; completion is reported through `onValueComplete`, optionally submitting the owning form (`autoSubmit`).

The value may be owned by the consumer (`value`) or by the component (`defaultValue`). Inside `Field.Root` the first slot registers as the field's control, the group carries the field's label/description wiring, and the root reports `touched`/`dirty`/`filled`/`focused`/validity through its state attributes. No component builds on OTP Field.

## 2. Anatomy

```
<OTPField.Root>                 required, 1          default element: div (role="group")
                                                     + hidden <input type="text"> rendered as the root's next sibling
                                                       (only while `length` is a positive integer)
  …any wrapper elements…
  <OTPField.Input>              required, 1..n       default element: input   (exactly `length` instances expected)
  <OTPField.Separator>          optional, 0..n       default element: div (role="separator")
```

- **OTP-STR-01** — MUST. `OTPField.Root` MUST render, in document order, the root element (default `<div>`) followed by a sibling `<input type="text">` ("hidden input"); the hidden input MUST be rendered when, and only when, `length` is a positive integer (`Number.isInteger(length) && length > 0`). The hidden input is never a descendant of the root element.
  Source: `otp-field/root/OTPFieldRoot.tsx:136,395-460`. Test: [L2] `OTPFieldRoot.test.tsx` "renders the hidden validation input with the provided name", "supports grouped layouts without affecting slot counting".
- **OTP-STR-02** — MUST. `OTPField.Input` MUST be rendered inside the subtree of an `OTPField.Root` (at any depth, through any wrapper elements); rendering it elsewhere MUST throw the error in OTP-EDGE-01. Any number of Inputs may be rendered.
  Source: `otp-field/root/OTPFieldRootContext.ts:35-45`, `otp-field/input/OTPFieldInput.tsx:66`. Test: [L2] `OTPFieldInput.test.tsx` "throws a descriptive error when rendered outside <OTPField.Root>", "renders one textbox per slot".
- **OTP-STR-03** — MUST. Each Input's slot index MUST be its position, in document order, among all Inputs registered under the same Root (0-based), regardless of intervening wrapper elements or Separators; the index MUST be correct in the very first render (including server-rendered output) when Inputs are rendered in document order, and corrected post-commit, pre-paint otherwise. There is no `index` prop (OTP-API-24).
  Source: `otp-field/input/OTPFieldInput.tsx:68`, `internals/composite/list/useCompositeListItem.ts:43-55`, `internals/composite/list/CompositeList.tsx:228-273`; see [CMP](../architecture/composite-and-list-navigation.md) CMP-STA-02, CMP-STA-16. Test: [L2] `OTPFieldRoot.test.tsx` "assigns slot indexes from render order when omitted", "supports grouped layouts without affecting slot counting", "renders visible inputs with unique IDs".
- **OTP-STR-04** — MUST. The number of rendered Inputs is expected to equal `length`; a mismatch MUST NOT throw or disable the component (the value is still clamped to `length`, extra slots render empty, focus moves clamp to the rendered slot count per OTP-FOC-05) and MUST produce the development warning OTP-EDGE-02.
  Source: `otp-field/root/OTPFieldRoot.tsx:142,165-170,396-401,649-663`. Test: [L2] `OTPFieldRoot.test.tsx` "warns when length does not match the rendered input count", "is removed when the focused slot unmounts but its field remains".
- **OTP-STR-05** — MUST. `OTPField.Separator` MUST be the standalone `Separator` part (same element, props, state, `role="separator"`, `aria-orientation` and `data-orientation`); it is structural only, reads no OTP state, and MUST NOT affect slot indexing.
  Source: `otp-field/index.parts.ts:3`, `separator/Separator.tsx:12-27`; see [separator](separator.md). Test: [L2] `OTPFieldRoot.test.tsx` "supports grouped layouts without affecting slot counting".

If no Input is rendered the Root still renders the group and the hidden input; the hidden input's value is the (normalized) value and nothing is focusable.

## 3. Public API

### 3.1 `OTPField.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `length` | `number` | — (**required**) | Number of slots. Clamps the value, defines completion, sizes the hidden input's constraints. | OTP-API-01 |
| `value` | `string` | — (uncontrolled) | The OTP value (consumer-owned when not `undefined` on first render). Normalized before use. | OTP-API-02 |
| `defaultValue` | `string` | `''` | Initial value when component-owned. Normalized before use. | OTP-API-03 |
| `onValueChange` | `(value: string, eventDetails: OTPField.Root.ChangeEventDetails) => void` | — | Change request; cancelable. | OTP-API-04 |
| `onValueInvalid` | `(value: string, eventDetails: OTPField.Root.InvalidEventDetails) => void` | — | Entered text had characters rejected by validation/normalization. `value` is the raw entered string. | OTP-API-05 |
| `onValueComplete` | `(value: string, eventDetails: OTPField.Root.CompleteEventDetails) => void` | — | Value became complete (or a complete paste replaced a complete value). | OTP-API-06 |
| `validationType` | `'numeric' \| 'alpha' \| 'alphanumeric' \| 'none'` | `'numeric'` | Character filter, slot/hidden `pattern`, default `inputMode`. | OTP-API-07 |
| `normalizeValue` | `(value: string) => string` | — | Custom normalizer applied after whitespace stripping and `validationType` filtering; output is filtered again and clamped. | OTP-API-08 |
| `inputMode` | `'none' \| 'text' \| 'tel' \| 'url' \| 'email' \| 'numeric' \| 'decimal' \| 'search'` | derived from `validationType` | Virtual-keyboard hint for every slot and the hidden input. | OTP-API-09 |
| `mask` | `boolean` | `false` | Render slots as `type="password"`. | OTP-API-10 |
| `autoComplete` | `string` | `'one-time-code'` | `autocomplete` of the first slot and the hidden input. | OTP-API-11 |
| `autoSubmit` | `boolean` | `false` | Submit the owning form when the value becomes complete. | OTP-API-12 |
| `form` | `string` | — | `id` of the owning `<form>`; applied to the hidden input and every slot; used to resolve the form for `autoSubmit`. | OTP-API-13 |
| `name` | `string` | — | Form field name (hidden input only). Overridden by an enclosing `Field.Root`'s `name`. | OTP-API-14 |
| `disabled` | `boolean` | `false` | OR-ed with the enclosing `Field.Root`'s `disabled`. | OTP-API-15 |
| `readOnly` | `boolean` | `false` | Value cannot be changed by the user; navigation and focus still work. | OTP-API-16 |
| `required` | `boolean` | `false` | Native `required` on the hidden input and every slot. | OTP-API-17 |
| `id` | `string` | generated | Id of the **first slot**; later slots derive `{id}-2`, `{id}-3`, …; also the labelable control id. Not applied to the root element. | OTP-API-18 |
| `aria-labelledby` | `string` | — | Consumed: becomes the group's `aria-labelledby` and suppresses slot inheritance (§5.5). | OTP-ARIA-02 |
| `aria-describedby` | `string` | — | Consumed: merged with Field description ids onto the group (§5.5). | OTP-ARIA-05 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the Root state object. | OTP-API-19 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element (after the built-in attributes, so they win). | OTP-API-20 |

**Consumed (not forwarded to the element):** `aria-describedby`, `aria-labelledby`, `id`, `autoComplete`, `defaultValue`, `value`, `onValueChange`, `onValueComplete`, `form`, `length`, `autoSubmit`, `mask`, `inputMode`, `validationType`, `normalizeValue`, `disabled`, `readOnly`, `required`, `name`, `onValueInvalid`, `render`, `className`, `style`. `onChange` is excluded from the accepted parameter type.

- **OTP-API-01** — MUST. `length` is required (its absence is a type error). It is used verbatim for clamping (OTP-STA-04), completion (OTP-STA-09), slot attributes (`enterkeyhint`, first-slot `maxlength`), hidden-input constraints (OTP-DOM-24/25) and the hidden input's existence (OTP-STR-01); a non-positive-integer value produces OTP-EDGE-03 and the behavior in OTP-EDGE-07.
  Source: `otp-field/root/OTPFieldRoot.tsx:61,136,488,665-675`. Test: [L2] `OTPFieldRoot.spec.tsx` (`// @ts-expect-error - length is required`), `OTPFieldRoot.test.tsx` "warns when length is not a positive integer (%p)".
- **OTP-API-02** — MUST. When `value` is not `undefined` on the first render, the value is consumer-owned: the rendered value MUST be the normalization (OTP-STA-04) of the current `value` prop on every render, and user input MUST NOT change it except through the consumer updating the prop.
  Source: `otp-field/root/OTPFieldRoot.tsx:97-102,138,254`, `packages/utils/src/useControlled.ts:41-46,83-90`; see [UTL](../architecture/shared-utilities.md) UTL-STA-01, UTL-STA-03, UTL-STA-04. Test: [L2] `OTPFieldRoot.test.tsx` "updates the rendered value in controlled mode", "does not move focus later for a stale controlled change" (L4 for the focus-move clause).
- **OTP-API-03** — MUST. When `value` is `undefined` on the first render, the initial stored value MUST be `defaultValue` (default `''`), and the rendered value its normalization; rejected characters in `defaultValue` are dropped silently (no `onValueInvalid`).
  Source: `otp-field/root/OTPFieldRoot.tsx:56,97-102,138`. Test: [L2] `OTPFieldRoot.test.tsx` "splits the default value across inputs", "clamps an overlong default value to the rendered slot count".
- **OTP-API-04** — MUST. `onValueChange` MUST receive the next *normalized* value and a change-event-details object (§3.4) whose `reason` is one of `'input-change'`, `'input-clear'`, `'input-paste'`, `'keyboard'`. Full firing rules are in §7.
  Source: `otp-field/root/OTPFieldRoot.tsx:248,560-561,610-616`. Test: [L4] `OTPFieldRoot.test.tsx` "fires `input-change` when typing".
- **OTP-API-05** — MUST. `onValueInvalid` MUST receive the raw entered string (before any normalization) and a generic-event-details object with `reason` `'input-change'` or `'input-paste'`. Rules in §7 (OTP-EVT-07).
  Source: `otp-field/root/OTPFieldRoot.tsx:268-272,568-569,618-620`. Test: [L4] `OTPFieldRoot.test.tsx` "fires when typing is normalized before the OTP value updates".
- **OTP-API-06** — MUST. `onValueComplete` MUST receive the complete value and a generic-event-details object with `reason` `'input-change'` or `'input-paste'`. Rules in §7 (OTP-EVT-08…12).
  Source: `otp-field/root/OTPFieldRoot.tsx:193-199,579-580,622-625`. Test: [L4] `OTPFieldRoot.test.tsx` "fires when typing completes the OTP".
- **OTP-API-07** — MUST. `validationType` defaults to `'numeric'` and selects the built-in character class of OTP-STA-05.
  Source: `otp-field/root/OTPFieldRoot.tsx:65,132-135`, `otp-field/utils/otp.ts:8-37`. Test: [L2] `OTPFieldRoot.test.tsx` "supports alphabetic values when set to `alpha`", "supports alphanumeric values when set to `alphanumeric`", "omits the hidden validation pattern when set to `none`".
- **OTP-API-08** — MUST. `normalizeValue`, when given, MUST be invoked exactly once per normalization pass with the whitespace-stripped, `validationType`-filtered string, and its return value MUST be filtered by `validationType` again and then clamped (OTP-STA-04). It runs for every normalization (default/controlled values, typing, paste, autofill, replacements). The former name `sanitizeValue` is not accepted.
  Source: `otp-field/utils/otp.ts:62-67`, `otp-field/root/OTPFieldRoot.tsx:66,138,230,416-421`, `otp-field/input/OTPFieldInput.tsx:106-111,135`. Test: [L1] `otp.test.ts` "applies custom normalization after built-in validation", "filters custom normalization output through built-in validation", "clamps values after custom normalization"; `OTPFieldRoot.test.tsx` "supports custom normalization when `validationType` is `none`"; `OTPFieldRoot.spec.tsx` (`sanitizeValue` rejected). Docs: CHANGELOG.md v1.5.0 "Rename `sanitizeValue()` to `normalizeValue()` and allow composing with validation".
- **OTP-API-09** — MUST. The effective `inputMode` MUST be the `inputMode` prop when given, otherwise `'numeric'` for `validationType="numeric"`, `'text'` for `'alpha'`/`'alphanumeric'`, and absent for `'none'`. It is applied to every slot and to the hidden input.
  Source: `otp-field/root/OTPFieldRoot.tsx:135,447`, `otp-field/input/OTPFieldInput.tsx:148`, `otp-field/utils/otp.ts:15,21,27`. Test: [L2] `OTPFieldRoot.test.tsx` "allows a custom inputMode when validation is set to `none`", "allows overriding the built-in inputMode when needed".
- **OTP-API-10** — MUST. `mask` defaults to `false`; when `true` every slot's default `type` is `'password'` (OTP-DOM-09). The hidden input is unaffected.
  Source: `otp-field/root/OTPFieldRoot.tsx:63`, `otp-field/input/OTPFieldInput.tsx:147`. Test: [L2] `OTPFieldRoot.test.tsx` "renders password slot inputs when enabled".
- **OTP-API-11** — MUST. `autoComplete` defaults to `'one-time-code'` and is applied verbatim to the first slot and to the hidden input; every other slot MUST have `autocomplete="off"`.
  Source: `otp-field/root/OTPFieldRoot.tsx:55,446`, `otp-field/input/OTPFieldInput.tsx:149`. Test: [L2] `OTPFieldRoot.test.tsx` "applies the default autocomplete to the first slot only", "allows overriding the autocomplete attribute".
- **OTP-API-12** — MUST. `autoSubmit` defaults to `false`; when `true` the owning form is submitted immediately after `onValueComplete` (OTP-EVT-13).
  Source: `otp-field/root/OTPFieldRoot.tsx:62,196-198`. Test: [L4] `OTPFieldRoot.test.tsx` "does not auto-submit the owning form when the OTP becomes complete by default", "submits the completed named OTP value from the owning form when enabled".
- **OTP-API-13** — MUST. `form` MUST be forwarded as the `form` attribute of the hidden input and of every slot, and MUST NOT appear on the root element.
  Source: `otp-field/root/OTPFieldRoot.tsx:60,443`, `otp-field/input/OTPFieldInput.tsx:157`. Test: [L2] `OTPFieldRoot.test.tsx` "submits an associated external form when used with `autoSubmit`".
- **OTP-API-14** — MUST. The effective name MUST be the enclosing `Field.Root`'s `name` when defined, otherwise the `name` prop; it is placed on the hidden input only (never on slots or the root).
  Source: `otp-field/root/OTPFieldRoot.tsx:95,444`. Test: [L2] `OTPFieldRoot.test.tsx` "renders the hidden validation input with the provided name", "validates the latest value only after focus leaves the OTP field in onBlur mode" (Field name used for form values).
- **OTP-API-15** — MUST. The effective disabled state MUST be `fieldDisabled || disabled` (`fieldDisabled` is the enclosing `Field.Root`'s disabled state, `false` outside a Field); the prop defaults to `false`.
  Source: `otp-field/root/OTPFieldRoot.tsx:67,94`. Test: [L2] `OTPFieldRoot.test.tsx` "disables every slot and prevents value changes".
- **OTP-API-16** — MUST. `readOnly` defaults to `false`; when `true` the `readonly` attribute is set on every slot and the hidden input, and every value-changing path (typing, composition commit, paste, Backspace/Delete, autofill) is ignored while focus, selection and navigation keys keep working (OTP-KEY-06, OTP-FOC-12).
  Source: `otp-field/root/OTPFieldRoot.tsx:68,411,452`, `otp-field/input/OTPFieldInput.tsx:159,203,208,258-260,306`. Test: [L2] `OTPFieldRoot.test.tsx` "marks every slot as readonly and prevents value changes"; `OTPFieldInput.test.tsx` "keeps arrow and home/end navigation working in readonly mode" (L4 for the editing-path clause).
- **OTP-API-17** — MUST. `required` defaults to `false`; when `true` the `required` attribute MUST be set on the hidden input and on every slot.
  Source: `otp-field/root/OTPFieldRoot.tsx:69,453`, `otp-field/input/OTPFieldInput.tsx:160`. Test: [L2] `OTPFieldRoot.test.tsx` "blocks form submission while the code is incomplete".
- **OTP-API-18** — MUST. The resolved control id MUST be: the id resolved by an enclosing labelable scope (Field) when one claims it, else the `id` prop, else a generated `base-ui-…` id. It MUST be applied to the first slot, drive the other slots' ids (OTP-DOM-07), drive the hidden input's fallback id (OTP-DOM-21) and the generated label id (OTP-ARIA-04). The root element itself MUST NOT receive the `id` prop.
  Source: `otp-field/root/OTPFieldRoot.tsx:54,124,300-309,381-393`, `internals/labelable-provider/useLabelableId.ts:17,82` (generated fallback via `useBaseUiId`, prefixed per RND-DOM-27); see [FFI](../architecture/field-and-form-infrastructure.md) FFI-ALG-07. Test: [L2] `OTPFieldRoot.test.tsx` "renders visible inputs with unique IDs", "associates Field.Label with the first slot".
- **OTP-API-19** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md) with the Root state object of this section; none is forwarded as a plain attribute.
  Source: `otp-field/root/OTPFieldRoot.tsx:72-74,381-393`. Test: [L2] `OTPFieldRoot.test.tsx` conformance suite "Base UI component API" (§13).
- **OTP-API-20** — MUST. Every prop not listed as consumed MUST be forwarded to the root element, merged after the built-in `role`/`aria-describedby`/`aria-labelledby` so a consumer attribute of the same name wins (RND-DOM-19); consumer handlers run before built-in ones (RND-EVT-02). A consumer `aria-label` is forwarded and does not alter `aria-labelledby` resolution.
  Source: `otp-field/root/OTPFieldRoot.tsx:75,384-391`. Test: [L2] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root" (`data-testid` forwarded).

#### State object

The Root state is the enclosing field's state (all `false`/`null` outside a Field) with OTP fields layered on top. `filled` and `focused` are component-owned (they work outside a Field); `touched`, `dirty` and `valid` come from the Field.

| Field | Type | Meaning |
|---|---|---|
| `value` | `string` | The normalized value. |
| `length` | `number` | The `length` prop. |
| `complete` | `boolean` | `value.length === length`. |
| `disabled` | `boolean` | Effective disabled (OTP-API-15). |
| `readOnly` | `boolean` | The `readOnly` prop. |
| `required` | `boolean` | The `required` prop. |
| `filled` | `boolean` | `value !== ''`. |
| `focused` | `boolean` | Some slot owns focus (OTP-FOC-07). |
| `touched` | `boolean` | Field: focus has left the root at least once. |
| `dirty` | `boolean` | Field: `value !== initialValue`. |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated or while disabled. |

- **OTP-STA-14** — MUST. The Root state object MUST contain exactly the eleven fields above; `disabled`, `filled` and `focused` MUST be the component's own values (overriding the field's), and `touched`/`dirty`/`valid` MUST be the field's values (`false`/`false`/`null` outside a Field).
  Source: `otp-field/root/OTPFieldRoot.tsx:311-324`, `internals/field-constants/constants.ts:18-32`. Test: [L2] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root", "sets `data-complete` when all slots are filled".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-complete` | `""` while `value.length === length` | OTP-DOM-02 |
| `data-filled` | `""` while `value !== ''` | OTP-DOM-04 |
| `data-focused` | `""` while a slot owns focus | OTP-DOM-04 |
| `data-disabled` | `""` while effective disabled | OTP-DOM-03 |
| `data-readonly` | `""` while `readOnly` | OTP-DOM-03 |
| `data-required` | `""` while `required` | OTP-DOM-03 |
| `data-touched` | `""` while field `touched` (Field only) | OTP-DOM-05 |
| `data-dirty` | `""` while field `dirty` (Field only) | OTP-DOM-05 |
| `data-valid` | `""` while field `valid === true` (Field only) | OTP-DOM-05 |
| `data-invalid` | `""` while field `valid === false` (Field only) | OTP-DOM-05 |

`value` and `length` are never projected as attributes (OTP-DOM-02).

#### CSS variables

None.

### 3.2 `OTPField.Input`

**Default element:** `<input>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `aria-label` | `string` | — | Consumed. Applied only to slots with index ≥ 1; ignored on the first slot (with a dev warning when the first slot has no associated `<label>`). | OTP-API-21 |
| `aria-labelledby` | `string` | — | Consumed. Used for this slot instead of the inherited group label. | OTP-API-22 |
| `type` and every other native `<input>` attribute/handler | — | — | Forwarded after the built-in attributes (so `type`, `placeholder`, `inputMode`, … override); handlers run before the built-in ones. | OTP-API-23 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the Input state object. | OTP-API-23 |
| `index` | — | — | **Not accepted** (type error); order is inferred from render order. | OTP-API-24 |

**Consumed (not forwarded to the element):** `aria-label`, `aria-labelledby`, `render`, `className`, `style`.

- **OTP-API-21** — MUST. A consumer `aria-label` MUST be applied to the slot element only when the slot index is ≥ 1; on the first slot it MUST be dropped (the first slot keeps the shared label, OTP-ARIA-07).
  Source: `otp-field/input/OTPFieldInput.tsx:33,81-83,163`. Test: [L2] `OTPFieldInput.test.tsx` "keeps the shared label on the first slot even if an aria-label is provided".
- **OTP-API-22** — MUST. A consumer `aria-labelledby` on a slot MUST take precedence over the label inherited from the Root (OTP-ARIA-06) for that slot.
  Source: `otp-field/input/OTPFieldInput.tsx:34,82,161`. Test: none.
- **OTP-API-23** — MUST. All other props MUST be forwarded to the slot element, merged after the built-in attributes (a consumer `type`, `inputMode`, `placeholder`, `tabIndex`, … replaces the built-in value, RND-DOM-19) with consumer handlers running first (RND-EVT-02); `className`/`style`/`render` behave per RND with the Input state object, and the attribute set handed to a substitute callback includes the native input attributes (`disabled`, `readOnly`, `value`, …).
  Source: `otp-field/input/OTPFieldInput.tsx:35-39,332-337,357-361`. Test: [L2] `OTPFieldRoot.test.tsx` "allows overriding the input type on individual slots"; `OTPFieldInput.spec.tsx` (render callback receives `disabled`/`readOnly`); `OTPFieldInput.test.tsx` conformance suite "Base UI component API".
- **OTP-API-24** — MUST. `OTPField.Input` MUST NOT accept an `index` prop; slot order is derived from render/document order (OTP-STR-03).
  Source: `otp-field/input/OTPFieldInput.tsx:68,357-361`. Test: [L1] `OTPFieldInput.spec.tsx` (`// @ts-expect-error - slot order is inferred from render order`).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `value` | `string` | The character in this slot (`''` when empty). |
| `index` | `number` | The slot index. |
| `filled` | `boolean` | `value !== ''` for this slot. |
| `complete`, `disabled`, `length`, `readOnly`, `required`, `focused`, `touched`, `dirty`, `valid` | as Root | Copied from the Root state (`focused` is the root-level flag, not per slot). |

- **OTP-STA-11** — MUST. The Input state MUST be the Root state with `value` replaced by `value[index] ?? ''` (indexing by UTF-16 code unit), `filled` replaced by `slotValue !== ''`, and `index` added — twelve fields in total.
  Source: `otp-field/root/OTPFieldRootContext.ts:47-58`, `otp-field/input/OTPFieldInput.tsx:79-80,342-355`. Test: [L2] `OTPFieldRoot.test.tsx` "splits the default value across inputs"; `OTPFieldInput.test.tsx` "marks each input as complete when all slots are filled".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-filled` | `""` while this slot holds a character | OTP-DOM-17 |
| `data-complete`, `data-disabled`, `data-readonly`, `data-required`, `data-focused`, `data-touched`, `data-dirty`, `data-valid`, `data-invalid` | as on Root (same conditions) | OTP-DOM-17 |
| `data-length` | `String(length)` while `length` is truthy — **undocumented** (see §16) | OTP-DOM-18 |

`value` and `index` are never projected.

#### CSS variables

None.

### 3.3 `OTPField.Separator`

Identical to the standalone Separator (OTP-STR-05): **default element** `<div>`; props `orientation` (`'horizontal' | 'vertical'`, default `'horizontal'`), `className`/`style`/`render`, other `<div>` attributes forwarded; state `{ orientation }`; attributes `role="separator"`, `aria-orientation={orientation}`, `data-orientation={orientation}`. Requirements live in [separator](separator.md).

- **OTP-API-25** — MUST. `OTPField.Separator` MUST expose exactly the Separator's props, state and attributes; it receives nothing from the OTP Root (its state object has only `orientation`).
  Source: `separator/Separator.tsx:16-24,29-42`, `otp-field/index.ts:8`. Test: none in OTP (see separator).

### 3.4 Shared types

- `OTPField.Root.ValidationType` = `'numeric' | 'alpha' | 'alphanumeric' | 'none'`. Source: `otp-field/utils/otp.ts:8`, `otp-field/root/OTPFieldRoot.tsx:630`.
- `OTPField.Root.ChangeEventReason` = `'input-change' | 'input-clear' | 'input-paste' | 'keyboard'`. Source: `otp-field/root/OTPFieldRoot.tsx:610-614`, `internals/reason-parts.ts:17,18,20,27`.
- `OTPField.Root.ChangeEventDetails` = the change-event-details shape of [EVD](../architecture/event-details-and-reasons.md) (EVD-API-01) specialised to the reasons above; `event` is `InputEvent | Event` for `input-change`, `InputEvent | FocusEvent | Event` for `input-clear`, `ClipboardEvent` for `input-paste`, `KeyboardEvent` for `keyboard`; `trigger` is always `undefined`. Source: `otp-field/root/OTPFieldRoot.tsx:615-616`, `internals/createBaseUIEventDetails.ts:21-24,31,126-157`. Test: [L1] `OTPFieldRoot.spec.tsx` (narrowing).
- `OTPField.Root.InvalidEventReason` = `'input-change' | 'input-paste'`; `OTPField.Root.InvalidEventDetails` = generic-event details (`reason`, `event` only; EVD-API-02). Source: `otp-field/root/OTPFieldRoot.tsx:618-620`, `internals/createBaseUIEventDetails.ts:159-174`.
- `OTPField.Root.CompleteEventReason` = `'input-change' | 'input-paste'`; `OTPField.Root.CompleteEventDetails` = generic-event details. Source: `otp-field/root/OTPFieldRoot.tsx:622-625`.
- `OTPField.Root.State`, `OTPField.Root.Props`, `OTPField.Input.State`, `OTPField.Input.Props`, `OTPField.Separator.State`, `OTPField.Separator.Props` — the tables above. Aliases `OTPFieldRootState`, `OTPFieldRootProps`, `OTPFieldRootChangeEventReason`, `OTPFieldRootChangeEventDetails`, `OTPFieldRootInvalidEventReason`, `OTPFieldRootInvalidEventDetails`, `OTPFieldRootCompleteEventReason`, `OTPFieldRootCompleteEventDetails`, `OTPFieldInputState`, `OTPFieldInputProps` are exported from the package root. Source: `otp-field/index.ts:3-4`, `otp-field/root/OTPFieldRoot.tsx:627-637`, `otp-field/input/OTPFieldInput.tsx:363-366`.
- Exported constant namespaces `OTPFieldRootDataAttributes` (`complete`, `disabled`, `readonly`, `required`, `valid`, `invalid`, `touched`, `dirty`, `filled`, `focused`), `OTPFieldInputDataAttributes` (same ten names) and `OTPFieldSeparatorDataAttributes` (`orientation`). Source: `otp-field/root/OTPFieldRootDataAttributes.ts:1-40`, `otp-field/input/OTPFieldInputDataAttributes.ts:1-40`, `separator/SeparatorDataAttributes.ts:5`, `otp-field/index.ts:6-8`.

## 4. State model

The component owns one piece of state, the value string, plus two focus-related scalars (the index of the last focused slot, and whether a slot currently owns focus). Everything else is derived.

**Ownership.** `value`/`defaultValue` is the consumer-owned / component-owned pair (OTP-API-02/03). In consumer-owned mode a user edit only fires `onValueChange`; the DOM follows the prop.

- **OTP-STA-01** — MUST. Ownership of the value MUST be fixed on the first render (consumer-owned iff `value !== undefined`) and never change; later flips between `undefined` and a string MUST NOT change the mode (dev diagnostic OTP-EDGE-06).
  Source: `packages/utils/src/useControlled.ts:41,46`; UTL-STA-01. Test: none.
- **OTP-STA-02** — MUST. In component-owned mode a non-canceled change MUST store the *normalized* requested value (so stored and rendered values are identical from then on); `defaultValue` is read once.
  Source: `otp-field/root/OTPFieldRoot.tsx:230,254`; UTL-STA-02, UTL-STA-05. Test: [L2] `OTPFieldRoot.test.tsx` "fills consecutive slots when typing multiple characters into the first input".
- **OTP-STA-03** — MUST. In consumer-owned mode the rendered value (slots, hidden input, state object, registered field value) MUST follow the `value` prop (normalized) on every render, from any cause; if `value` later becomes `undefined` the rendered value MUST fall back to the normalization of the first-render `defaultValue`.
  Source: `otp-field/root/OTPFieldRoot.tsx:97-102,138`; UTL-STA-03. Test: [L2] `OTPFieldRoot.test.tsx` "updates the rendered value in controlled mode", "does not fire again when a controlled value changes from one complete value to another".

**Normalization** (`normalizeOTPValueWithDetails`). Every string that enters the component goes through this procedure; it returns the normalized string and a boolean "rejected characters".

- **OTP-STA-04** — MUST. Normalizing `(input, length, validationType, normalizeValue)` MUST proceed exactly as follows: (1) `stripped` = `input ?? ''` with every character matching `\s` removed (whitespace removal is **not** counted as rejection); (2) `filtered` = `stripped` with every character outside the `validationType` class removed (`'none'`: unchanged); `rejected` = `stripped.length > filtered.length`; (3) if `normalizeValue` is given: `custom = normalizeValue(filtered)`; `rejected ||= filtered.length > custom.length`; `filtered = custom` filtered again by the class; `rejected ||= custom.length > filtered.length`; (4) split `filtered` into Unicode code points, keep the first `max` where `max = length < 0 ? 0 : length` (JavaScript `slice(0, max)` semantics: `NaN` → none, non-integer → truncated, `Infinity` → all), `rejected ||= codePointCount > max`; the result is the kept code points joined. Length comparisons are in UTF-16 code units; the clamp is in code points.
  Source: `otp-field/utils/otp.ts:39-45,51-77`. Test: [L1] `otp.test.ts` "removes whitespace from pasted values", "returns an empty string when stripping nullish values", "normalizes, filters, and clamps numeric values", "returns an empty string for nullish input values", "uses custom normalization when validationType is none", "applies custom normalization after built-in validation", "filters custom normalization output through built-in validation", "clamps values after custom normalization", "returns an empty string for negative lengths"; `OTPFieldRoot.test.tsx` "fires when built-in validation removes characters before custom normalization expands the value", "fires when custom normalization removes all characters after built-in validation".
- **OTP-STA-05** — MUST. The built-in classes MUST be: `numeric` — characters matching `\d` (removal regexp `/[^\d]/g`), slot pattern `\d{1}`, hidden pattern `\d{N}`, default `inputMode` `numeric`; `alpha` — `[a-zA-Z]`, patterns `[a-zA-Z]{1}` / `[a-zA-Z]{N}`, `inputMode` `text`; `alphanumeric` — `[a-zA-Z0-9]`, patterns `[a-zA-Z0-9]{1}` / `[a-zA-Z0-9]{N}`, `inputMode` `text`; `none` — no filtering, no patterns, no default `inputMode`. `N` is `length` interpolated verbatim.
  Source: `otp-field/utils/otp.ts:10-37`. Test: [L1] `otp.test.ts` "normalizes alphabetic values", "normalizes alphanumeric values"; `OTPFieldRoot.test.tsx` "applies single-character validation to each visible slot", "renders the hidden validation input with an alphanumeric pattern during SSR", "renders a hidden validation input with the provided length" (L2 for the slot and hidden-input `pattern`/`inputMode` clause).
- **OTP-STA-06** — MUST. The rendered value MUST always be a normalized string: `defaultValue`, the `value` prop, and every stored value are normalized at render time with the current `length`, `validationType` and `normalizeValue` (so changing any of these re-normalizes an existing value, e.g. shrinking `length` truncates it).
  Source: `otp-field/root/OTPFieldRoot.tsx:138`. Test: [L2] `OTPFieldRoot.test.tsx` "splits the default value across inputs", "supports alphabetic values when set to `alpha`".
- **OTP-STA-07** — MUST. *Replacement from a slot* (`replaceOTPValue(current, index, text)`) MUST be: `n` = normalize(`text`); result = normalize(`current.slice(0, index)` + `n` + `current.slice(index + n.length)`), i.e. the characters at positions `index … index + n.length − 1` are overwritten, the rest preserved, and the whole re-clamped to `length` (overflow caused by the insertion position is dropped silently, without rejection reporting). Slicing is by UTF-16 code unit.
  Source: `otp-field/utils/otp.ts:92-110`. Test: [L1] `otp.test.ts` "replaces values from the middle of the OTP", "replaces values from the first slot", "replaces values at the last slot", "applies custom normalization when replacing OTP values", "preserves suffix characters when custom normalization removes part of a middle replacement"; `OTPFieldRoot.test.tsx` "replaces consecutive slots when typing multiple characters into a later input".
- **OTP-STA-08** — MUST. *Character removal* (`removeOTPCharacter(current, index)`) MUST return `current` unchanged when `index < 0 || index >= current.length`, otherwise `current` with the code unit at `index` removed (later characters shift left).
  Source: `otp-field/utils/otp.ts:112-118`. Test: [L1] `otp.test.ts` "removes a character from the first slot", "removes a character from the last slot", "leaves the value unchanged when removing an out-of-bounds index".

**Derived values.**

- **OTP-STA-09** — MUST. `complete` MUST be `value.length === length` (code units vs. the prop).
  Source: `otp-field/root/OTPFieldRoot.tsx:314`. Test: [L2] `OTPFieldRoot.test.tsx` "sets `data-complete` when all slots are filled".
- **OTP-STA-10** — MUST. `filled` MUST be `value !== ''`, computed from the component's value (works outside a Field).
  Source: `otp-field/root/OTPFieldRoot.tsx:140,316`. Test: [L2] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root".
- **OTP-STA-12** — MUST. The *end target index* used by keyboard navigation MUST be `min(value.length, max(length − 1, 0))`: the first empty slot, or the last slot when the value is complete.
  Source: `otp-field/input/OTPFieldInput.tsx:227-228`. Test: [L3] `OTPFieldInput.test.tsx` "moves focus to the empty end slot with End", "keeps focus on the final slot with ArrowDown when the value is complete".
- **OTP-STA-13** — MUST. The *last focused index* MUST be initialized to `min(value.length, length − 1)` at first render and set to a slot's index whenever that slot receives focus without being redirected (OTP-FOC-03/04).
  Source: `otp-field/root/OTPFieldRoot.tsx:143,281`. Test: [L4] `OTPFieldInput.test.tsx` "allows tabbing out of the field from the active slot".

**Invariants.** The rendered value never exceeds `length` code points and contains only characters accepted by the class and `normalizeValue`. `complete` implies `filled` unless `length` is `0`. There is no mount/unmount lifecycle for any part; the hidden input's presence depends only on `length` validity.

Field-derived state: `touched`, `dirty` and `valid` have the semantics of FFI-STA-07/05/02; when the component writes them is in §8 (OTP-FRM-07…10); `focused` tracking is in §6.3.

## 5. Rendered DOM and ARIA

### 5.1 Root (group) element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"group"` (consumer may override) | OTP-ARIA-01 | C# |
| `aria-labelledby` | resolved label id(s), see §5.5 | OTP-ARIA-02…04 | C# |
| `aria-describedby` | consumer ids + Field description/error ids, deduplicated | OTP-ARIA-05 | C# |
| `id` | **never** from the `id` prop (that goes to the first slot); only via a substitute element | OTP-DOM-01 | C# |
| `data-*` | §3.1 table | OTP-DOM-02…05 | C# |

- **OTP-DOM-01** — MUST. The root element MUST NOT carry the `id` prop; it carries no built-in `id` at all (a substitute element's own `id` applies per RND-DOM-32).
  Source: `otp-field/root/OTPFieldRoot.tsx:54,381-393`. Test: [L2] `OTPFieldRoot.test.tsx` "renders visible inputs with unique IDs" (ids found on inputs only).
- **OTP-DOM-02** — MUST. `data-complete=""` MUST be present on the root exactly while `value.length === length`; `value` and `length` MUST NOT be projected as attributes on the root.
  Source: `otp-field/utils/stateAttributesMapping.ts:6-10`, `otp-field/root/OTPFieldRoot.tsx:314`. Test: [L2] `OTPFieldRoot.test.tsx` "sets `data-complete` when all slots are filled".
- **OTP-DOM-03** — MUST. `data-disabled`, `data-readonly` and `data-required` (empty-string values) MUST be present on the root exactly while the effective disabled state, `readOnly` and `required` are `true` respectively.
  Source: `otp-field/root/OTPFieldRoot.tsx:315,319-320`, `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `OTPFieldRoot.test.tsx` "disables every slot and prevents value changes", "marks every slot as readonly and prevents value changes".
- **OTP-DOM-04** — MUST. `data-filled=""` MUST be present exactly while `value !== ''` and `data-focused=""` exactly while a slot owns focus (OTP-FOC-07), inside or outside a Field.
  Source: `otp-field/root/OTPFieldRoot.tsx:140,144,316-317`. Test: [L2] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root", "tracks focus state when a readonly slot receives focus" (L4 for the `data-focused` clause).
- **OTP-DOM-05** — MUST. `data-touched`, `data-dirty` MUST reflect the field's `touched`/`dirty`; exactly one of `data-valid`/`data-invalid` MUST be present while the field's `valid` is `true`/`false` and neither while it is `null`. Outside a Field none of these four is ever present.
  Source: `otp-field/utils/stateAttributesMapping.ts:9`, `internals/field-constants/constants.ts:34-44`; see FFI-DOM-01. Test: [L2] `OTPFieldRoot.test.tsx` "is removed when the focused field becomes disabled" (Field-wrapped attributes).
- **OTP-DOM-06** — MUST. Consumer attributes on the root MUST override the built-in `role`, data attributes and (through a substitute element) `id` (RND-DOM-10, RND-DOM-19); the consumer cannot override the computed `aria-labelledby`/`aria-describedby` through plain props because those props are consumed.
  Source: `otp-field/root/OTPFieldRoot.tsx:384-391`. Test: none.

### 5.2 Slot `<input>` element (`OTPField.Input`)

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | `controlId` for index 0, `` `${controlId}-${index+1}` `` otherwise; absent when no control id exists yet (React 17 SSR) | OTP-DOM-07 | C# |
| `value` | this slot's character; the composition buffer while composing | OTP-DOM-08 | C# → JS |
| `type` | `"password"` when `mask`, else `"text"`; consumer `type` wins | OTP-DOM-09 | C# |
| `inputmode` | effective `inputMode` (OTP-API-09) | OTP-DOM-10 | C# |
| `autocomplete` | `autoComplete` on slot 0, `"off"` on every other slot | OTP-DOM-11 | C# |
| `autocorrect`, `spellcheck` | `"off"`, `"false"` | OTP-DOM-12 | C# |
| `enterkeyhint` | `"done"` on the last slot (`index === length − 1`), `"next"` otherwise | OTP-DOM-13 | C# |
| `maxlength` | `length` on slot 0 only; absent elsewhere | OTP-DOM-14 | C# |
| `disabled`, `readonly`, `required`, `form` | mirror effective disabled / `readOnly` / `required` / `form` | OTP-DOM-15 | C# |
| `pattern` | slot pattern of OTP-STA-05; absent for `'none'` | OTP-DOM-16 | C# |
| `tabindex` | `0` on the active slot, `-1` on every other | OTP-FOC-01 | C# |
| `aria-labelledby`, `aria-label`, `aria-invalid` | see §5.5 | OTP-ARIA-06…09 | C# |
| `name` | never | OTP-FRM-02 | C# |
| `data-*` | §3.2 table (incl. `data-length`) | OTP-DOM-17, OTP-DOM-18 | C# |

- **OTP-DOM-07** — MUST. Slot ids MUST be derived from the control id (OTP-API-18): slot 0 gets the control id itself, slot `i ≥ 1` gets `` `${controlId}-${i + 1}` `` (`verification-code`, `verification-code-2`, …). When no control id exists (legacy id fallback before client assignment) slots MUST have no `id`.
  Source: `otp-field/root/OTPFieldRoot.tsx:300-309`, `otp-field/input/OTPFieldInput.tsx:145`, `packages/utils/src/useId.ts:9-31,42-52`. Test: [L2] `OTPFieldRoot.test.tsx` "renders visible inputs with unique IDs"; `OTPFieldRoot.react17.test.tsx` "omits generated slot ids during SSR until the client fallback is assigned".
- **OTP-DOM-08** — MUST. The slot's `value` property MUST be the slot character (OTP-STA-11), except while an IME composition is buffered in that slot, when it MUST be the buffered composition text (OTP-KEY-21).
  Source: `otp-field/input/OTPFieldInput.tsx:77,146`. Test: [L2] `OTPFieldRoot.test.tsx` "splits the default value across inputs"; `OTPFieldInput.test.tsx` "commits an IME composition once on compositionend instead of per intermediate change" (L4 for the IME-composition clause).
- **OTP-DOM-09** — MUST. The slot's default `type` MUST be `"password"` when `mask` is `true` and `"text"` otherwise; a consumer `type` on the slot replaces it.
  Source: `otp-field/input/OTPFieldInput.tsx:147,335`. Test: [L2] `OTPFieldRoot.test.tsx` "renders password slot inputs when enabled", "allows overriding the input type on individual slots".
- **OTP-DOM-10** — MUST. Every slot MUST carry the effective `inputMode` (OTP-API-09) and none when it is absent.
  Source: `otp-field/input/OTPFieldInput.tsx:148`. Test: [L2] `OTPFieldRoot.test.tsx` "allows a custom inputMode when validation is set to `none`".
- **OTP-DOM-11** — MUST. Slot 0 MUST carry `autocomplete` equal to the `autoComplete` prop; every other slot MUST carry `autocomplete="off"`.
  Source: `otp-field/input/OTPFieldInput.tsx:149`. Test: [L2] `OTPFieldRoot.test.tsx` "applies the default autocomplete to the first slot only". Docs: CHANGELOG.md v1.6.0 "Avoid password manager bubbles after first input".
- **OTP-DOM-12** — MUST. Every slot MUST carry `autocorrect="off"` and `spellcheck="false"`.
  Source: `otp-field/input/OTPFieldInput.tsx:150-151`. Test: none.
- **OTP-DOM-13** — MUST. Every slot MUST carry `enterkeyhint="next"`, except the slot whose index is `length − 1`, which MUST carry `enterkeyhint="done"`.
  Source: `otp-field/input/OTPFieldInput.tsx:152`. Test: none.
- **OTP-DOM-14** — MUST. Slot 0 MUST carry `maxlength` equal to `length`; no other slot MUST carry `maxlength`.
  Source: `otp-field/input/OTPFieldInput.tsx:153-154`. Test: [L2] `OTPFieldRoot.test.tsx` "clamps an overlong default value to the rendered slot count".
- **OTP-DOM-15** — MUST. Every slot MUST mirror the effective disabled state as the `disabled` attribute, `readOnly` as `readonly`, `required` as `required`, and the `form` prop as `form`.
  Source: `otp-field/input/OTPFieldInput.tsx:156-160`. Test: [L2] `OTPFieldRoot.test.tsx` "disables every slot and prevents value changes", "marks every slot as readonly and prevents value changes".
- **OTP-DOM-16** — MUST. Every slot MUST carry the single-character `pattern` of its `validationType` (OTP-STA-05) and none for `'none'`.
  Source: `otp-field/input/OTPFieldInput.tsx:158`, `otp-field/root/OTPFieldRoot.tsx:133`. Test: [L2] `OTPFieldRoot.test.tsx` "applies single-character validation to each visible slot".
- **OTP-DOM-17** — MUST. Every slot MUST carry `data-filled=""` exactly while its own character is non-empty, and `data-complete`, `data-disabled`, `data-readonly`, `data-required`, `data-focused`, `data-touched`, `data-dirty`, `data-valid`/`data-invalid` under the same conditions as the root (OTP-DOM-02…05); `value` and `index` MUST NOT be projected.
  Source: `otp-field/utils/stateAttributesMapping.ts:12-16`, `otp-field/root/OTPFieldRootContext.ts:47-58`. Test: [L2] `OTPFieldInput.test.tsx` "marks each input as complete when all slots are filled", "adds disabled and readonly state attributes to each slot".
- **OTP-DOM-18** — MUST. Every slot MUST carry `data-length` with value `String(length)` while `length` is truthy (the `length` state field is not suppressed by the slot mapping; it is suppressed on the root). This attribute is absent from the official outline and data-attribute namespace (§16).
  Source: `otp-field/utils/stateAttributesMapping.ts:12-16` (no `length` entry), `otp-field/input/OTPFieldInput.tsx:342`, `internals/getStateAttributesProps.ts:26-27`. Test: none.
- **OTP-DOM-19** — MUST. Consumer attributes on a slot MUST override the built-in ones of the same name (RND-DOM-19); consumer handlers run before the built-in handler and may veto it with `preventBaseUIHandler()` or, where stated in §6, `preventDefault()`.
  Source: `otp-field/input/OTPFieldInput.tsx:335`. Test: [L2] `OTPFieldRoot.test.tsx` "allows overriding the input type on individual slots"; `OTPFieldInput.test.tsx` "allows a composed mousedown handler to prevent focus" (L4 for the handler-veto clause).

### 5.3 Hidden `<input type="text">` (form-participation input)

Rendered as the root element's next sibling while `length` is a positive integer (OTP-STR-01). It is the field's shared validation input (FFI §6 table A).

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `type` | `"text"` | OTP-DOM-20 | C# |
| `id` | `` `${controlId}-hidden-input` `` only when a control id exists **and** the effective name is `null`/`undefined`; otherwise none | OTP-DOM-21 | C# |
| `name`, `value`, `form` | effective name; the normalized value; `form` prop | OTP-DOM-22 | C# |
| `autocomplete`, `inputmode` | `autoComplete` prop; effective `inputMode` | OTP-DOM-23 | C# |
| `pattern` | whole-code pattern of OTP-STA-05 (`\d{6}`); none for `'none'` | OTP-DOM-24 | C# |
| `minlength`, `maxlength` | both `length` | OTP-DOM-25 | C# |
| `disabled`, `readonly`, `required` | mirror effective disabled / `readOnly` / `required` | OTP-DOM-22 | C# |
| `aria-hidden`, `tabindex` | `"true"`, `-1` | OTP-DOM-20 | C# |
| `style` | `visuallyHiddenInput` when named, `visuallyHidden` when nameless | OTP-DOM-26 | C# |
| `aria-describedby`, `aria-invalid` | Field description ids; `"true"` when the field's computed validity is `false` and nothing is disabled | OTP-ARIA-11 | C# |

- **OTP-DOM-20** — MUST. The hidden input MUST be `type="text"` with `aria-hidden` and `tabindex="-1"`, so it is never a tab stop and never exposed to assistive technology.
  Source: `otp-field/root/OTPFieldRoot.tsx:441,454-455`; FFI-DOM-02. Test: [L2] `OTPFieldRoot.test.tsx` "renders the hidden validation input with the provided name" (`querySelector('input[name="otp"]')`, not a textbox).
- **OTP-DOM-21** — MUST. The hidden input MUST have `id` `` `${controlId}-hidden-input` `` when, and only when, the effective name is `null`/`undefined` and a control id exists; a named hidden input MUST have no `id`.
  Source: `otp-field/root/OTPFieldRoot.tsx:442`; FFI-DOM-03. Test: [L2] `OTPFieldRoot.test.tsx` "renders a fallback hidden input id when name is not provided".
- **OTP-DOM-22** — MUST. The hidden input MUST carry the effective name as `name` (absent when none), the normalized value as its `value`, the `form` prop as `form`, and `disabled`/`readonly`/`required` mirroring the effective disabled state, `readOnly` and `required`.
  Source: `otp-field/root/OTPFieldRoot.tsx:443-445,451-453`. Test: [L2] `OTPFieldRoot.test.tsx` "clamps an overlong default value to the rendered slot count" (`toHaveValue('123456')`), "submits the completed named OTP value from the owning form when enabled".
- **OTP-DOM-23** — MUST. The hidden input MUST carry `autocomplete` equal to the `autoComplete` prop and `inputmode` equal to the effective `inputMode` (none when absent).
  Source: `otp-field/root/OTPFieldRoot.tsx:446-447`. Test: [L2] `OTPFieldRoot.test.tsx` "allows overriding the autocomplete attribute", "allows a custom inputMode when validation is set to `none`".
- **OTP-DOM-24** — MUST. The hidden input MUST carry the whole-code `pattern` of OTP-STA-05 with `length` interpolated (`\d{6}`, `[a-zA-Z0-9]{6}`, …) and no `pattern` for `validationType="none"`; it MUST be present in server-rendered output.
  Source: `otp-field/root/OTPFieldRoot.tsx:134,450`, `otp-field/utils/otp.ts:13,19,25`. Test: [L2] `OTPFieldRoot.test.tsx` "renders the hidden validation input with an alphanumeric pattern during SSR", "omits the hidden validation pattern when set to `none`", "renders a hidden validation input with the provided length".
- **OTP-DOM-25** — MUST. The hidden input MUST carry `minlength` and `maxlength` both equal to `length`.
  Source: `otp-field/root/OTPFieldRoot.tsx:448-449`. Test: [L2] `OTPFieldRoot.test.tsx` "renders a hidden validation input with the provided length".
- **OTP-DOM-26** — MUST. The hidden input's inline style MUST be `visuallyHiddenInput` (absolute positioning, so native validation bubbles anchor near the control) when an effective name exists and `visuallyHidden` (fixed at the viewport origin) otherwise.
  Source: `otp-field/root/OTPFieldRoot.tsx:456`, `packages/utils/src/visuallyHidden.ts:14-25`; UTL-DOM-13, UTL-DOM-14, FFI-DOM-02. Test: none.

### 5.4 Separator element

- **OTP-DOM-27** — MUST. An `OTPField.Separator` rendered inside the Root MUST render exactly what the standalone Separator renders (`<div role="separator" aria-orientation="…" data-orientation="…">`) and MUST NOT receive any OTP data attribute.
  Source: `separator/Separator.tsx:18-24`, `otp-field/index.parts.ts:3`. Test: [L2] `OTPFieldRoot.test.tsx` "supports grouped layouts without affecting slot counting" (`getByText('-')` visible).

### 5.5 ARIA

Label resolution has two layers: the **group** (`aria-labelledby` on the root) and the **slots** (each slot is a separate `textbox` and needs its own name).

- **OTP-ARIA-01** — MUST. The root MUST carry `role="group"` unless the consumer overrides `role`.
  Source: `otp-field/root/OTPFieldRoot.tsx:386`. Test: [L2] `OTPFieldRoot.test.tsx` "supports grouped layouts without affecting slot counting" (`getByRole('group')`).
- **OTP-ARIA-02** — MUST. When the consumer passes `aria-labelledby` on the Root, the root MUST carry exactly that value and slots MUST NOT inherit it (OTP-ARIA-06 yields nothing).
  Source: `otp-field/root/OTPFieldRoot.tsx:53,125-126,388`, `internals/labelable-provider/useAriaLabelledBy.ts:20`. Test: [L2] `OTPFieldRoot.test.tsx` "forwards root `aria-labelledby` to the group only".
- **OTP-ARIA-03** — MUST. Otherwise, inside a `Field.Root` whose `Field.Label` has published an id, the root's `aria-labelledby` MUST be that label id.
  Source: `otp-field/root/OTPFieldRoot.tsx:92,125`, `internals/labelable-provider/useAriaLabelledBy.ts:19-20`; FFI-ARIA-05. Test: [L2] `OTPFieldRoot.test.tsx` "applies the Field description to the group".
- **OTP-ARIA-04** — MUST. Otherwise the root's `aria-labelledby` MUST be the id of the native `<label>` associated with the **first slot** — its parent `<label>`, else its next element sibling `<label>` whose `for` equals the first slot's id, else the first entry of the slot's `labels` list — assigning that label the id `` `${controlId}-label` `` when it has none; re-evaluated post-commit, pre-paint after every update; absent when no such label exists. The consumer's root `aria-label` does not suppress this resolution.
  Source: `otp-field/root/OTPFieldRoot.tsx:114-122,125`, `internals/labelable-provider/useAriaLabelledBy.ts:14-35,40-74`; FFI-ALG-08, FFI-ARIA-06. Test: [L4] `OTPFieldInput.test.tsx` "applies a native label to every slot" (slots inherit the generated label id).
- **OTP-ARIA-05** — MUST. The root's `aria-describedby` MUST be the consumer's `aria-describedby` ids followed by the Field's description/error ids, split on whitespace, de-duplicated, space-joined; absent when the list is empty. Slots MUST NOT receive these ids.
  Source: `otp-field/root/OTPFieldRoot.tsx:127-131,387,639-642`; FFI-ARIA-01. Test: [L2] `OTPFieldRoot.test.tsx` "applies the Field description to the group", "forwards root `aria-describedby` to the group"; `OTPFieldInput.test.tsx` "applies the Field label to every slot" (no `aria-describedby` on slots).
- **OTP-ARIA-06** — MUST. A slot's `aria-labelledby` MUST be: the slot's own `aria-labelledby` prop when given; otherwise the root's resolved `aria-labelledby` (OTP-ARIA-03/04) **only when the Root received no explicit `aria-labelledby` prop**; otherwise absent. It MUST be omitted entirely on any slot that renders an `aria-label` (OTP-ARIA-07).
  Source: `otp-field/root/OTPFieldRoot.tsx:126`, `otp-field/input/OTPFieldInput.tsx:82-83,161`. Test: [L2] `OTPFieldInput.test.tsx` "applies the Field label to every slot", "applies a native label to every slot"; `OTPFieldRoot.test.tsx` "forwards root `aria-labelledby` to the group only" (L4 for the native-label clause).
- **OTP-ARIA-07** — MUST. Slot 0 MUST never carry `aria-label` (a consumer value is dropped, OTP-API-21, with the dev warning OTP-EDGE-04 when the slot has no associated native label); slots with index ≥ 1 MUST carry the consumer's `aria-label` when given.
  Source: `otp-field/input/OTPFieldInput.tsx:83,163`. Test: [L2] `OTPFieldInput.test.tsx` "keeps the shared label on the first slot even if an aria-label is provided".
- **OTP-ARIA-08** — MUST. On a slot with `aria-label`, `aria-labelledby` MUST be absent (the slot's name is the `aria-label` alone).
  Source: `otp-field/input/OTPFieldInput.tsx:161`. Test: [L2] `OTPFieldInput.test.tsx` "keeps the shared label on the first slot even if an aria-label is provided" (slot 1 has `aria-label`).
- **OTP-ARIA-09** — MUST. Every slot MUST carry `aria-invalid="true"` exactly while the enclosing Field's app-controlled invalid flag (`invalid` prop, or a `Form` error for the effective name) is `true` and the effective disabled state is `false`; a computed validation failure (native constraint or `validate`) does NOT by itself set `aria-invalid` on slots; outside a Field the attribute is never present.
  Source: `otp-field/input/OTPFieldInput.tsx:162`, `field/root/FieldRoot.tsx:97`; see FFI-STA-02 and FFI §10 note 8. Test: [L2] `OTPFieldRoot.test.tsx` "ignores hidden-input autofill when readOnly inside Field" (`aria-invalid="true"`), "… when disabled inside Field" (absent), "… outside Field" (absent).
- **OTP-ARIA-10** — MUST. Slots MUST NOT carry `aria-describedby` from the Field (only the root and the hidden input do).
  Source: `otp-field/input/OTPFieldInput.tsx:144-163` (no described-by). Test: [L2] `OTPFieldInput.test.tsx` "applies the Field label to every slot".
- **OTP-ARIA-11** — MUST. The hidden input MUST carry the Field's described-by composition as `aria-describedby` and `aria-invalid="true"` when the field's computed `valid === false` and neither the field nor the component is disabled (FFI-ARIA-03); it carries no label.
  Source: `otp-field/root/OTPFieldRoot.tsx:406`, `field/root/useFieldValidation.ts:367-376`. Test: none in OTP (mechanism tested in FFI).

## 6. Interactions

Terminology: *slot i* is the Input with index `i`; `last` = `max(length − 1, 0)`; *end target* = `min(value.length, last)` (OTP-STA-12); *boundary modifier* = <kbd>Ctrl</kbd> or <kbd>Meta</kbd> held **and** <kbd>Alt</kbd> not held; "focus slot k" means OTP-FOC-05 (focus + select content, subject to the redirect OTP-FOC-03).

### 6.1 Keyboard

All handling is on `keydown` of a slot. <kbd>Shift</kbd> is ignored everywhere (a shifted arrow behaves like the plain arrow). Handled keys call `preventDefault()` **and** `stopPropagation()`.

| Key | Context (slot focused, modifiers, direction) | Behavior | Req |
|---|---|---|---|
| any | event default-prevented by a consumer handler, effective disabled, or an IME composition is buffered in this slot | not handled; native behavior and bubbling | OTP-KEY-01 |
| <kbd>ArrowLeft</kbd> (LTR) / <kbd>ArrowRight</kbd> (RTL), no boundary modifier | any | focus slot `max(0, i − 1)` | OTP-KEY-02 |
| <kbd>ArrowRight</kbd> (LTR) / <kbd>ArrowLeft</kbd> (RTL), no boundary modifier | any | focus slot `min(last, i + 1)` (redirected to the end target if beyond it) | OTP-KEY-02 |
| <kbd>Ctrl</kbd>/<kbd>Meta</kbd> + previous-arrow | any (Alt not held) | focus slot 0 | OTP-KEY-03 |
| <kbd>Ctrl</kbd>/<kbd>Meta</kbd> + next-arrow | any (Alt not held) | focus the end target | OTP-KEY-03 |
| <kbd>Home</kbd>, <kbd>ArrowUp</kbd> | any modifiers | focus slot 0 | OTP-KEY-04 |
| <kbd>End</kbd>, <kbd>ArrowDown</kbd> | any modifiers | focus the end target | OTP-KEY-05 |
| every key below | `readOnly` | not handled | OTP-KEY-06 |
| <kbd>Ctrl</kbd>/<kbd>Meta</kbd> + <kbd>Backspace</kbd> | Alt not held | value → `''` (`reason: 'keyboard'`); focus slot 0 after commit | OTP-KEY-07 |
| <kbd>Delete</kbd> | any | remove character `i` (OTP-STA-08), `reason: 'keyboard'`; focus stays on slot `i` (re-selected after commit) | OTP-KEY-08 |
| printable key (`key.length === 1`) equal to this slot's character while the slot content is fully selected | any modifiers | no value change, no callbacks; focus slot `i + 1` unless `i` is the last slot | OTP-KEY-09 |
| <kbd>Backspace</kbd> (also with Alt or Shift) | slot `i` filled | remove character `i`; focus slot `max(0, i − 1)` after commit | OTP-KEY-10 |
| <kbd>Backspace</kbd> | slot `i` empty, `i > 0` | remove character `i − 1`; focus slot `i − 1` after commit | OTP-KEY-10 |
| <kbd>Backspace</kbd> | slot 0 empty | removal is a no-op → no callback, focus unchanged | OTP-KEY-10 |
| <kbd>Tab</kbd>, <kbd>Shift</kbd>+<kbd>Tab</kbd>, <kbd>Enter</kbd>, <kbd>Escape</kbd>, <kbd>Ctrl</kbd>/<kbd>Meta</kbd>+<kbd>A</kbd>/<kbd>C</kbd>/<kbd>X</kbd>/<kbd>V</kbd>, every other key | any | not intercepted (native: Tab leaves the field, Ctrl/Meta+V raises the paste path of OTP-KEY-18) | OTP-KEY-12 |

- **OTP-KEY-01** — MUST. A keydown MUST be left entirely to the browser (no focus move, no value change, not default-prevented, propagation untouched) when the event is already default-prevented, the effective disabled state is `true`, or an IME composition is currently buffered in the slot (OTP-KEY-21). Server: relaxed, see §9.
  Source: `otp-field/input/OTPFieldInput.tsx:222-224`. Test: [L4] `OTPFieldInput.test.tsx` "leaves vertical arrow navigation unhandled in disabled mode", "ignores keyboard commands while a composition is buffered".
- **OTP-KEY-02** — MUST. The "previous" arrow (<kbd>ArrowLeft</kbd> in LTR, <kbd>ArrowRight</kbd> in RTL) MUST stop the event and focus slot `max(0, i − 1)`; the "next" arrow MUST stop the event and focus slot `min(last, i + 1)`, which the redirect of OTP-FOC-03 may turn into the end target. The value never changes.
  Source: `otp-field/input/OTPFieldInput.tsx:229-244`. Test: [L4] `OTPFieldInput.test.tsx` "moves focus with arrow keys", "moves focus with arrow keys in RTL".
- **OTP-KEY-03** — MUST. With a boundary modifier (<kbd>Ctrl</kbd> or <kbd>Meta</kbd>, and not <kbd>Alt</kbd>), the previous arrow MUST focus slot 0 and the next arrow MUST focus the end target, in both directions' key mapping.
  Source: `otp-field/input/OTPFieldInput.tsx:229,236,242`. Test: [L4] `OTPFieldInput.test.tsx` "moves focus to the field boundaries with Ctrl/Cmd + arrow keys", "… in RTL". Docs: CHANGELOG.md v1.5.0 "Support <kbd>Ctrl</kbd> and <kbd>Cmd</kbd> shortcuts".
- **OTP-KEY-04** — MUST. <kbd>Home</kbd> and <kbd>ArrowUp</kbd> MUST stop the event and focus slot 0 regardless of modifiers.
  Source: `otp-field/input/OTPFieldInput.tsx:246-250`. Test: [L4] `OTPFieldInput.test.tsx` "moves focus to the first slot with Home", "moves focus to the field boundary with ArrowUp".
- **OTP-KEY-05** — MUST. <kbd>End</kbd> and <kbd>ArrowDown</kbd> MUST stop the event (default prevented, propagation stopped — ancestor keydown listeners do not see it) and focus the end target; when the focused slot already is the end target, focus MUST stay there.
  Source: `otp-field/input/OTPFieldInput.tsx:252-256`. Test: [L4] `OTPFieldInput.test.tsx` "moves focus to the empty end slot with End", "moves focus to the field boundary with ArrowDown", "stops propagation when ArrowDown moves focus to the empty end slot", "keeps focus on the empty end slot with ArrowDown", "keeps focus on the final slot with ArrowDown when the value is complete". Docs: CHANGELOG.md v1.5.0 "Fix vertical arrow slot navigation".
- **OTP-KEY-06** — MUST. While `readOnly`, the navigation keys of OTP-KEY-02…05 MUST keep working, and every editing key (<kbd>Backspace</kbd> with or without modifiers, <kbd>Delete</kbd>, printable keys) MUST be left to the browser (which, on a read-only input, changes nothing); no callback fires and focus does not move.
  Source: `otp-field/input/OTPFieldInput.tsx:258-260`. Test: [L4] `OTPFieldInput.test.tsx` "keeps arrow and home/end navigation working in readonly mode", "blocks Delete and Backspace from changing the value in readonly mode".
- **OTP-KEY-07** — MUST. <kbd>Ctrl</kbd>/<kbd>Meta</kbd>+<kbd>Backspace</kbd> (Alt not held) MUST stop the event and request the value `''` with `reason: 'keyboard'`; when the request commits, slot 0 MUST be focused and selected after the value is applied (OTP-FOC-10).
  Source: `otp-field/input/OTPFieldInput.tsx:262-277`. Test: [L4] `OTPFieldInput.test.tsx` "clears all slots with Ctrl/Cmd + Backspace".
- **OTP-KEY-08** — MUST. <kbd>Delete</kbd> MUST stop the event and request `removeOTPCharacter(value, i)` with `reason: 'keyboard'`; after the commit slot `i` MUST be re-focused and its (new) content selected. On an empty slot at or beyond the value's end the request equals the current value and no callback fires.
  Source: `otp-field/input/OTPFieldInput.tsx:279-283`. Test: [L4] `OTPFieldInput.test.tsx` "deletes the current character with Delete without moving focus"; `OTPFieldRoot.test.tsx` "does not fire `onValueChange` for Delete on an empty slot".
- **OTP-KEY-09** — MUST. When a single-character key is pressed while the slot's content is fully selected (`selectionStart === 0 && selectionEnd === value.length` on the DOM input) and the key equals the slot's character, the event MUST be stopped (so no `input` event follows) and focus MUST move to slot `i + 1` when `i < length − 1`; on the last slot nothing happens (no re-selection, no callback).
  Source: `otp-field/input/OTPFieldInput.tsx:285-296`. Test: [L4] `OTPFieldInput.test.tsx` "moves focus to the next slot when typing the same character into a filled slot", "does not reselect the final slot when typing the same character".
- **OTP-KEY-10** — MUST. Plain <kbd>Backspace</kbd> (no boundary modifier; <kbd>Alt</kbd>/<kbd>Shift</kbd> do not matter) MUST stop the event, compute `target = max(0, i − 1)` and `deleteIndex = (slot i empty) ? target : i`, and request `removeOTPCharacter(value, deleteIndex)` with `reason: 'keyboard'`; after the commit slot `target` MUST be focused and selected. When the removal leaves the value unchanged (empty first slot) no callback fires and focus stays.
  Source: `otp-field/input/OTPFieldInput.tsx:298-303`. Test: [L4] `OTPFieldInput.test.tsx` "deletes the current character and moves focus to the previous slot on backspace", "deletes the previous filled slot when backspacing on an empty non-first slot", "selects the previous slot value after backspacing into the first slot"; `OTPFieldRoot.test.tsx` "removes a character and moves focus to the previous slot with backspace", "does not fire `onValueChange` for Backspace on an already-empty first slot".
- **OTP-KEY-11** — MUST. The focus move that follows a keyboard edit (OTP-KEY-07/08/10) MUST happen only if the request was committed (not canceled, value actually changed), and only once the applied value equals the requested value (OTP-FOC-10).
  Source: `otp-field/input/OTPFieldInput.tsx:262-271`. Test: [L4] `OTPFieldInput.test.tsx` "keeps focus in place when backspace is canceled".
- **OTP-KEY-12** — MUST. No other key is intercepted: <kbd>Tab</kbd>/<kbd>Shift</kbd>+<kbd>Tab</kbd> move focus natively out of the field (only one slot is tabbable, OTP-FOC-01), <kbd>Enter</kbd> follows native implicit-submission rules, <kbd>Escape</kbd> does nothing, and clipboard shortcuts reach the browser (paste then arrives as a `paste` event, OTP-KEY-18).
  Source: `otp-field/input/OTPFieldInput.tsx:219-304` (no other branches). Test: [L4] `OTPFieldInput.test.tsx` "allows tabbing out of the field from the active slot".

#### 6.1.1 Text entry

Typing is observed through the slot's `input`/`change` event, not through keydown: the browser writes into the slot (whose content is selected on focus, so a keystroke replaces it) and the component reads the slot's DOM value.

- **OTP-KEY-13** — MUST. A slot change event MUST be ignored (no normalization, no callbacks, no value change) when it is default-prevented by a consumer handler, when the effective disabled state is `true`, or when `readOnly` is `true`. Server: relaxed, see §9.
  Source: `otp-field/input/OTPFieldInput.tsx:208-210`. Test: [L4] `OTPFieldRoot.test.tsx` "disables every slot and prevents value changes", "marks every slot as readonly and prevents value changes".
- **OTP-KEY-14** — MUST. Otherwise the slot's DOM value (`raw`) MUST be normalized (OTP-STA-04); when characters were rejected, `onValueInvalid(raw, { reason: 'input-change', event })` MUST fire first (OTP-EVT-07).
  Source: `otp-field/input/OTPFieldInput.tsx:106-115,217`. Test: [L4] `OTPFieldRoot.test.tsx` "fires when typing is normalized before the OTP value updates", "supports typing alphabetic values when set to `alpha`".
- **OTP-KEY-15** — MUST. When the normalized text `n` is non-empty, the requested value MUST be the replacement of OTP-STA-07 at slot `i` with `reason: 'input-change'`; if it commits, slot `min(i + n.length, length − 1)` MUST be focused and selected after the value is applied. Typing several characters at once (e.g. a multi-character `input` event) therefore fills consecutive slots.
  Source: `otp-field/input/OTPFieldInput.tsx:134-141`. Test: [L4] `OTPFieldRoot.test.tsx` "fills consecutive slots when typing multiple characters into the first input", "replaces consecutive slots when typing multiple characters into a later input", "composes built-in validation and custom normalization from a non-first slot"; `OTPFieldInput.test.tsx` "moves focus to the next slot after typing", "selects the last slot after typing into it for the first time".
- **OTP-KEY-16** — MUST. When the slot's DOM value becomes `''` (the user deleted the slot content through text input, e.g. a mobile keyboard), the requested value MUST be `removeOTPCharacter(value, i)` with `reason: 'input-clear'`; no focus move is queued.
  Source: `otp-field/input/OTPFieldInput.tsx:117-125`. Test: [L4] `OTPFieldRoot.test.tsx` "fires `input-clear` when clearing a slot by input".
- **OTP-KEY-17** — MUST. When the raw text is non-empty but normalizes to `''` (every character rejected or whitespace): if the slot holds a character, the slot's DOM value MUST be restored to that character and its content re-selected; if the slot is empty, the slot MUST end up displaying `''`; in both cases no value change and no `onValueChange` occur (only `onValueInvalid` when characters were rejected).
  Source: `otp-field/input/OTPFieldInput.tsx:117-131`. Test: [L4] `OTPFieldInput.test.tsx` "keeps the filled slot selected when typing an invalid character"; `OTPFieldRoot.test.tsx` "fires when custom normalization removes all characters after built-in validation".
- **OTP-KEY-18** — MUST. On a slot `paste` event that is not default-prevented, disabled or read-only, the component MUST read the clipboard's `text/plain` data (`''` when the event has no clipboard data), call `preventDefault()` (the browser never inserts the text itself), then process the text exactly like typed text (OTP-KEY-14/16) with `reason: 'input-paste'`: rejected characters → `onValueInvalid(raw, { reason: 'input-paste' })`; non-empty normalized text → replacement at slot `i`, focus slot `min(i + n.length, length − 1)` after commit; empty or fully rejected text → no value change, no `onValueChange`, no focus move, slot content untouched. Propagation is not stopped.
  Source: `otp-field/input/OTPFieldInput.tsx:305-329,101-141`. Test: [L4] `OTPFieldRoot.test.tsx` "fills consecutive slots when pasting a code", "composes built-in validation and custom normalization for pasted values", "fires `input-paste` when pasted text is normalized before the OTP value updates"; `OTPFieldInput.test.tsx` "replaces values from the middle when pasting into a later slot", "ignores a paste event when clipboard text is unavailable", "keeps focus in place when paste is canceled".
- **OTP-KEY-19** — MUST. A `paste` event MUST be left to the browser (no `preventDefault`, no processing) when it is default-prevented by a consumer handler, the effective disabled state is `true`, or `readOnly` is `true`. Server: relaxed, see §9.
  Source: `otp-field/input/OTPFieldInput.tsx:306-308`. Test: [L4] `OTPFieldInput.test.tsx` "blocks paste from changing the value in readonly mode".
- **OTP-KEY-20** — MUST. If reading the clipboard data throws, the component MUST log the development warning OTP-EDGE-05, MUST NOT call `preventDefault()`, and MUST leave the value unchanged.
  Source: `otp-field/input/OTPFieldInput.tsx:312-325`. Test: [L4] `OTPFieldInput.test.tsx` "warns in development when clipboard text cannot be read during paste handling".
- **OTP-KEY-21** — MUST. IME composition (non-Android platforms): on `compositionstart` the slot MUST start a buffer initialized to the slot's current character; while buffered, each slot change event MUST only update the displayed buffer (the slot shows the in-progress text, no normalization, no callbacks, no value change) and keydowns are not handled (OTP-KEY-01); on `compositionend` the buffer MUST be discarded (the slot shows its character again) and, unless disabled or read-only, the slot's DOM value at that moment MUST be processed once as typed text (OTP-KEY-14/16/17/18, `reason: 'input-change'`). A composition ending with `''` therefore requests `removeOTPCharacter(value, i)` (a no-op on an empty slot).
  Source: `otp-field/input/OTPFieldInput.tsx:73-77,146,189-218`. Test: [L4] `OTPFieldInput.test.tsx` "commits an IME composition once on compositionend instead of per intermediate change", "reports characters rejected from a committed IME composition", "ignores keyboard commands while a composition is buffered", "does not commit a composition that ends after the field becomes disabled/readOnly". Docs: CHANGELOG.md v1.9.0 "Fix Safari IME composition over-filling slots".
- **OTP-KEY-22** — MUST. On Android (`platform.os.android`, UTL-EDGE-05) `compositionstart` MUST NOT start a buffer: every change event commits immediately as in OTP-KEY-14/16, and `compositionend` does nothing.
  Source: `otp-field/input/OTPFieldInput.tsx:190-195,197-199`. Test: [L4] `OTPFieldInput.android.test.tsx` "commits each change during an IME composition".
- **OTP-KEY-23** — MUST. A `compositionend` without a buffered composition in that slot (no preceding `compositionstart`, or Android) MUST be ignored.
  Source: `otp-field/input/OTPFieldInput.tsx:197-199`. Test: [L4] `OTPFieldInput.android.test.tsx` "commits each change during an IME composition" (`compositionEnd` changes nothing).


### 6.2 Pointer and touch

Only `mousedown` is handled (touch and pen generate compatibility `mousedown` events). There is no click, pointer-move, hover, long-press, drag or context-menu handling.

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| mouse/touch/pen press on a slot | enabled, not default-prevented | `preventDefault()` (no native caret placement), then focus + select slot `i` (redirected per OTP-FOC-03) | OTP-PTR-01 |
| press on a slot | consumer `onMouseDown` called `preventDefault()` | nothing; the browser does not focus the slot either | OTP-PTR-02 |
| press on a slot | effective disabled | nothing (native disabled behavior) | OTP-PTR-03 |
| press on a slot beyond the first empty one | enabled | focus lands on the end target (OTP-FOC-03) | OTP-PTR-04 |
| right-click, double-click, drag-select, touch long-press | any | not intercepted beyond the `mousedown` above (selection drag is suppressed by the prevented mousedown) | OTP-PTR-01 |

- **OTP-PTR-01** — MUST. On `mousedown` of an enabled slot whose event is not already default-prevented, the component MUST call `preventDefault()` and then focus and select slot `i` (OTP-FOC-05), so the slot content is selected rather than a caret placed.
  Source: `otp-field/input/OTPFieldInput.tsx:164-171`. Test: [L4] `OTPFieldInput.test.tsx` "selects the slot value on mousedown".
- **OTP-PTR-02** — MUST. When a consumer `onMouseDown` handler calls `preventDefault()`, the built-in handler MUST do nothing, and (because the default is prevented) the slot MUST NOT receive focus. Server: relaxed, see §9.
  Source: `otp-field/input/OTPFieldInput.tsx:165-167`. Test: [L4] `OTPFieldInput.test.tsx` "allows a composed mousedown handler to prevent focus".
- **OTP-PTR-03** — MUST. While the effective disabled state is `true`, `mousedown` MUST NOT be default-prevented and MUST NOT move focus.
  Source: `otp-field/input/OTPFieldInput.tsx:165-167`. Test: none.
- **OTP-PTR-04** — MUST. A press on a slot whose index exceeds the value's length MUST result in focus on the end target (via the focus redirect, OTP-FOC-03), never on the pressed slot.
  Source: `otp-field/input/OTPFieldInput.tsx:170`, `otp-field/root/OTPFieldRoot.tsx:276-279`. Test: [L4] `OTPFieldInput.test.tsx` "redirects focus to the first empty slot when a later empty slot is focused" (same path, programmatic focus).

### 6.3 Focus management

The field is a single tab stop. One slot — the *active slot* — has `tabindex="0"`; all others have `tabindex="-1"`. Slot content is always selected on focus, so the next keystroke overwrites.

- **OTP-FOC-01** — MUST. Exactly one slot MUST have `tabindex="0"` (the active slot, OTP-FOC-02); every other slot MUST have `tabindex="-1"`. Consequently <kbd>Tab</kbd> from any slot leaves the field.
  Source: `otp-field/input/OTPFieldInput.tsx:155`. Test: [L2] `OTPFieldInput.test.tsx` "allows tabbing out of the field from the active slot" (L4 for the Tab-out clause).
- **OTP-FOC-02** — MUST. The active slot index MUST be `min(lastFocusedIndex, max(length − 1, 0))` while a slot owns focus (OTP-FOC-07), and `min(value.length, length − 1)` (the first empty slot, or the last slot when complete) otherwise.
  Source: `otp-field/root/OTPFieldRoot.tsx:143-148`. Test: [L2] `OTPFieldInput.test.tsx` "allows tabbing out of the field from the active slot" (focus lands on slot 1 after typing, then Tab leaves).
- **OTP-FOC-03** — MUST. When a slot with index `i > value.length` receives focus (by any means: click, programmatic `focus()`, arrow key), the component MUST immediately move focus to slot `min(value.length, length − 1)` instead (selecting its content) and MUST NOT record `i` as the last focused index.
  Source: `otp-field/root/OTPFieldRoot.tsx:274-279`. Test: [L4] `OTPFieldInput.test.tsx` "redirects focus to the first empty slot when a later empty slot is focused".
- **OTP-FOC-04** — MUST. When a slot with index `i ≤ value.length` receives focus (event not default-prevented, not disabled), the component MUST record `i` as the last focused index, select the slot's content, and mark the field focused (OTP-FOC-07).
  Source: `otp-field/root/OTPFieldRoot.tsx:281-282`, `otp-field/input/OTPFieldInput.tsx:172-179`. Test: [L4] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root"; `OTPFieldInput.test.tsx` "selects the slot value on mousedown".
- **OTP-FOC-05** — MUST. "Focus slot k" MUST clamp `k` to `[0, renderedSlotCount − 1]` (rendered, connected slots — not `length`), then call `focus()` and `select()` on that slot element; it is a no-op when no slot is rendered.
  Source: `otp-field/root/OTPFieldRoot.tsx:165-170`. Test: [L4] `OTPFieldInput.test.tsx` "selects the previous slot value after backspacing into the first slot", "deletes the current character with Delete without moving focus" (selection asserted).
- **OTP-FOC-06** — MUST. Focus arriving on the hidden input (browser validation bubble, autofill UI, programmatic focus) MUST be redirected to slot 0 (focused and selected).
  Source: `otp-field/root/OTPFieldRoot.tsx:407-409`; FFI-FOC-05. Test: [L4] `OTPFieldRoot.test.tsx` "redirects hidden validation input focus to the first visible slot".
- **OTP-FOC-07** — MUST. The root-level `focused` flag (state field, `data-focused`, and the Field's `focused`) MUST become `true` when an enabled slot receives focus and MUST become `false` on a slot `blur` whose `relatedTarget` is outside the root element (or `null`); a blur whose `relatedTarget` is inside the root (slot-to-slot moves, including redirects) MUST NOT change it. The hidden input is outside the root for this purpose.
  Source: `otp-field/root/OTPFieldRoot.tsx:144,286-294`, `otp-field/input/OTPFieldInput.tsx:70,172-188`, `packages/utils/src/shadowDom.ts` (`contains`, UTL-DOM-20). Test: [L4] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root", "validates the latest value only after focus leaves the OTP field in onBlur mode".
- **OTP-FOC-08** — MUST. A consumer `onFocus` handler that calls `preventDefault()` MUST prevent the focused flag from being set (focus itself still lands); a consumer `onBlur` handler that calls `preventDefault()` MUST prevent the flag from being cleared (and prevents the touched/commit side effects of OTP-FRM-09/10). Server: relaxed, see §9.
  Source: `otp-field/input/OTPFieldInput.tsx:173-175,181-183`. Test: [L4] `OTPFieldInput.test.tsx` "allows a composed focus handler to prevent internal focus state", "allows a composed blur handler to preserve internal focus state".
- **OTP-FOC-09** — MUST. Focus ownership follows FFI-ALG-13 per slot: the slot that most recently set the flag is its owner; when the owner slot becomes disabled or is removed from the DOM (no blur fires), the root and Field `focused` flags MUST be cleared post-commit, pre-paint; removing a slot that is *not* the owner MUST leave the flags untouched; a slot that is already the active element when it mounts or becomes enabled MUST acquire the flag post-commit, pre-paint.
  Source: `otp-field/input/OTPFieldInput.tsx:70`, `internals/field-root-context/useSetFieldFocused.ts:22-43`. Test: [L4] `OTPFieldRoot.test.tsx` "is removed when the focused field becomes disabled", "is removed when the focused field unmounts", "is kept when a previously focused slot unmounts after focus moves to a sibling", "is removed when the focused slot unmounts but its field remains".
- **OTP-FOC-10** — MUST. Focus moves that follow a value request (OTP-KEY-07/08/10/15/18, OTP-FOC-13) MUST be *queued* with the requested value and performed post-commit, pre-paint of the render in which the rendered value changes, only if the new rendered value equals the queued value; any value change clears the queue. Hence: a canceled request moves nothing; in consumer-owned mode the move happens when (and only when) the consumer applies exactly the requested value, even asynchronously; a different value applied later moves nothing.
  Source: `otp-field/root/OTPFieldRoot.tsx:106-109,172-174,207-215`, `internals/useValueChanged.ts:10-16` (UTL-STA-36). Test: [L4] `OTPFieldRoot.test.tsx` "does not move focus later for a stale controlled change", "moves focus after an asynchronously accepted controlled change", "composes with built-in validation and advances focus"; `OTPFieldInput.test.tsx` "keeps focus in place when typing is canceled".
- **OTP-FOC-11** — MUST. Programmatic focus on a slot (e.g. the Form focusing the first invalid field's control, which is slot 0) behaves like user focus: redirect, selection and flag per OTP-FOC-03/04.
  Source: `otp-field/root/OTPFieldRoot.tsx:163,274-284`, `form/Form.tsx:41-69`. Test: [L4] `OTPFieldRoot.test.tsx` "keeps focus on the first invalid field when auto-submit is blocked".
- **OTP-FOC-12** — MUST. `readOnly` MUST NOT affect focusability, selection on focus, the focused flag or navigation.
  Source: `otp-field/input/OTPFieldInput.tsx:172-179,258`. Test: [L4] `OTPFieldRoot.test.tsx` "tracks focus state when a readonly slot receives focus".
- **OTP-FOC-13** — MUST. After a committed autofill through the hidden input (OTP-FRM-13) with a non-empty value `v`, slot `v.length − 1` MUST be focused and selected (queued per OTP-FOC-10); an autofill that commits `''` MUST NOT move focus.
  Source: `otp-field/root/OTPFieldRoot.tsx:435-437`. Test: [L4] `OTPFieldRoot.test.tsx` "accepts valid hidden-input autofill and preserves focus when autofill is cleared", "handles password manager autofill through the hidden input".

### 6.4 Dismissal

Not applicable — there is no popup; nothing is dismissed. Blur handling is specified in OTP-FOC-07 and §8.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: string, details: ChangeEventDetails)` | a user action requests a value that differs from the current normalized value, synchronously inside the originating event, before the value is applied | `input-change`, `input-clear`, `input-paste`, `keyboard` | yes (`details.cancel()`) |
| `onValueInvalid` | `(value: string, details: InvalidEventDetails)` | typed, composed, pasted or autofilled text had characters rejected; before `onValueChange` of the same input | `input-change`, `input-paste` | no (generic details) |
| `onValueComplete` | `(value: string, details: CompleteEventDetails)` | the applied value reaches `length` through typing/paste/autofill, or a complete paste replaces a complete value | `input-change`, `input-paste` | no (generic details) |

- **OTP-EVT-01** — MUST. `onValueChange` MUST be invoked synchronously inside the handling of the originating DOM event, before any state update, with the normalized requested value and a change-event-details object whose `event` is the native originating event (`input`/`change` event, `ClipboardEvent`, `KeyboardEvent`, or the hidden input's change event) and whose `trigger` is `undefined`.
  Source: `otp-field/root/OTPFieldRoot.tsx:248`, `otp-field/input/OTPFieldInput.tsx:124,136,265`, `otp-field/root/OTPFieldRoot.tsx:432`; EVD-EVT-01, EVD-EVT-02. Test: [L4] `OTPFieldRoot.test.tsx` "fires `input-change` when typing", "fires `input-clear` when clearing a slot by input".
- **OTP-EVT-02** — MUST. The `reason` MUST be: `'input-change'` for slot text entry (OTP-KEY-15), a committed IME composition (OTP-KEY-21) and hidden-input autofill (OTP-FRM-13); `'input-clear'` for a slot emptied by text input (OTP-KEY-16); `'input-paste'` for paste (OTP-KEY-18); `'keyboard'` for <kbd>Backspace</kbd>, <kbd>Delete</kbd> and <kbd>Ctrl</kbd>/<kbd>Meta</kbd>+<kbd>Backspace</kbd> (OTP-KEY-07/08/10). No other reason is ever emitted.
  Source: `otp-field/input/OTPFieldInput.tsx:124,136,204,217,265,328`, `otp-field/root/OTPFieldRoot.tsx:432,610-614`; EVD-EVT-28 (OTPField rows). Test: [L4] `OTPFieldRoot.test.tsx` "fires `input-change` when typing", "fires `input-clear` when clearing a slot by input", "fills consecutive slots when pasting a code", "removes a character and moves focus to the previous slot with backspace", "handles password manager autofill through the hidden input".
- **OTP-EVT-03** — MUST. `onValueChange` MUST NOT fire when the normalized requested value equals the current normalized value (Delete/Backspace on empty positions, re-typing the same character, pasting the current value, autofill of the same value); such a request has no effect except the completion case of OTP-EVT-09.
  Source: `otp-field/root/OTPFieldRoot.tsx:240-246`; EVD-STA-09. Test: [L4] `OTPFieldRoot.test.tsx` "does not fire `onValueChange` for Delete on an empty slot", "does not fire `onValueChange` for Backspace on an already-empty first slot", "fires `input-paste` when a complete paste replaces a complete OTP" (third paste).
- **OTP-EVT-04** — MUST. Calling `details.cancel()` inside `onValueChange` MUST prevent the commit: in component-owned mode the value, slots, hidden input and state attributes stay unchanged; no focus move is queued; no completion is recorded (`onValueComplete` does not fire, `autoSubmit` does not run); none of the field side effects of OTP-FRM-07 run. In consumer-owned mode `cancel()` only suppresses the queued focus move and completion (the component never commits anyway, EVD-EVT-06).
  Source: `otp-field/root/OTPFieldRoot.tsx:250-252`, `otp-field/input/OTPFieldInput.tsx:139,268`; EVD-EVT-05, EVD-EVT-08. Test: [L4] `OTPFieldRoot.test.tsx` "does not fire when a completion-making paste is canceled"; `OTPFieldInput.test.tsx` "keeps focus in place when typing is canceled", "keeps focus in place when backspace is canceled", "keeps focus in place when paste is canceled".
- **OTP-EVT-05** — MUST. `onValueChange` MUST NOT fire on mount, for `defaultValue`, for changes of the `value` prop, or for re-normalization caused by `length`/`validationType`/`normalizeValue` changes.
  Source: `otp-field/root/OTPFieldRoot.tsx:228-266` (only call site is user-initiated `setValue`); EVD-STA-06. Test: [L2] `OTPFieldRoot.test.tsx` "does not fire again when a controlled value changes from one complete value to another".
- **OTP-EVT-06** — MUST. `details.allowPropagation()` has no effect (the component stops propagation only of handled keydowns, before the callback). `isCanceled`/`isPropagationAllowed` are live accessors per EVD-API-06.
  Source: `internals/createBaseUIEventDetails.ts:141-152`. Test: [L1] `OTPFieldRoot.spec.tsx` (type narrowing of `details.event` per reason).
- **OTP-EVT-07** — MUST. `onValueInvalid(raw, { reason, event })` MUST fire exactly once per processed input (slot change, composition commit, paste, hidden-input autofill) whose normalization reported rejected characters (OTP-STA-04: filtered by the class, shortened by `normalizeValue`, re-filtered, or clamped — whitespace removal excluded), with `raw` the unprocessed entered string, **before** `onValueChange`, and regardless of whether the value then changes (it fires even when every character was rejected or the result equals the current value). It MUST NOT fire for `defaultValue`/`value` normalization, nor when the input is ignored (disabled, read-only, default-prevented).
  Source: `otp-field/input/OTPFieldInput.tsx:106-115`, `otp-field/root/OTPFieldRoot.tsx:268-272,416-428`; EVD-EVT-25. Test: [L4] `OTPFieldRoot.test.tsx` "fires when typing is normalized before the OTP value updates", "fires when custom normalization removes characters", "fires when custom normalization removes characters after built-in validation", "fires when built-in validation removes characters before custom normalization expands the value", "fires when custom normalization removes all characters after built-in validation", "fires `input-paste` when pasted text is normalized before the OTP value updates", "fires `input-paste` when custom normalization removes characters after built-in validation", "handles password manager autofill through the hidden input"; `OTPFieldInput.test.tsx` "reports characters rejected from a committed IME composition".
- **OTP-EVT-08** — MUST. A request is *completion-making* iff its reason is `'input-change'` or `'input-paste'`, the normalized requested value has length `length`, and either the current value's length differs from `length` or the reason is `'input-paste'`. Keyboard requests, and text entry into an already complete value, are never completion-making.
  Source: `otp-field/root/OTPFieldRoot.tsx:231-238`. Test: [L4] `OTPFieldRoot.test.tsx` "fires when typing completes the OTP", "fires `input-paste` when pasting completes the OTP", "fires `input-paste` when pasting into a middle slot completes the OTP", "does not fire before the OTP becomes complete".
- **OTP-EVT-09** — MUST. For a completion-making request whose value equals the current value (a complete paste of the same code), `onValueComplete(value, { reason, event })` MUST fire immediately and synchronously, without `onValueChange`.
  Source: `otp-field/root/OTPFieldRoot.tsx:240-246`. Test: [L4] `OTPFieldRoot.test.tsx` "fires `input-paste` when a complete paste replaces a complete OTP".
- **OTP-EVT-10** — MUST. For a completion-making request that is not canceled and changes the value, the completion MUST be recorded as pending and `onValueComplete` MUST fire post-commit, pre-paint of the render in which the rendered value becomes exactly the pending value — after the field side effects of OTP-FRM-07 and after the queued focus move. Any rendered-value change clears the pending completion (so a different value applied by the consumer never completes); a non-complete committed value also clears it.
  Source: `otp-field/root/OTPFieldRoot.tsx:110-113,217-225,255-262`; EVD-EVT-25. Test: [L4] `OTPFieldRoot.test.tsx` "fires after an asynchronously accepted controlled completion", "does not fire later for a stale controlled completion attempt", "does not fire again when a controlled value changes from one complete value to another".
- **OTP-EVT-11** — MUST. `onValueComplete` MUST NOT fire when the completion-making request was canceled (OTP-EVT-04), nor for a `value` prop that is complete on mount or changes between complete values without a user request.
  Source: `otp-field/root/OTPFieldRoot.tsx:250-252`. Test: [L4] `OTPFieldRoot.test.tsx` "does not fire when a completion-making paste is canceled", "does not fire again when a controlled value changes from one complete value to another".
- **OTP-EVT-12** — MUST. Replacing characters of an already complete value by typing (reason `'input-change'`, result still complete) MUST fire `onValueChange` but MUST NOT fire `onValueComplete`.
  Source: `otp-field/root/OTPFieldRoot.tsx:236`. Test: none.
- **OTP-EVT-13** — MUST. When `autoSubmit` is `true`, immediately after each `onValueComplete` call (same synchronous sequence) the component MUST resolve the owning form as: the element with id `form` (looked up in the root's owner document) **if it is a `<form>`**; otherwise the hidden input's form owner, else slot 0's form owner, else none — and call `requestSubmit()` on it when the method exists. No form, a non-form element for the `form` id with no other owner, or a form without `requestSubmit` → no submission (the value and the completion callback are unaffected).
  Source: `otp-field/root/OTPFieldRoot.tsx:176-199`. Test: [L4] `OTPFieldRoot.test.tsx` "submits the completed named OTP value from the owning form when enabled", "does not submit the owning form before the OTP becomes complete when enabled", "keeps the completed value when the owning form has no requestSubmit method", "submits an associated external form when used with `autoSubmit`", "does not submit an ancestor form when the explicit form id is not a form".
- **OTP-EVT-14** — MUST. Because submission is requested through `requestSubmit()`, the owning form's own submit pipeline runs synchronously: a Base UI `<Form>` validates every registered field, blocks submission and focuses the first invalid control (FFI-EVT-01, FFI-FOC-01) — which may be another field — and a plain `<form>` runs native interactive constraint validation unless `novalidate`. The OTP value and `onValueComplete` are unaffected by a blocked submission.
  Source: `otp-field/root/OTPFieldRoot.tsx:188-190`, `form/Form.tsx:41-69,109-121`. Test: [L4] `OTPFieldRoot.test.tsx` "does not call flushSync inside a layout effect when auto-submitting a Base UI Form", "blocks invalid Base UI Form auto-submit without calling flushSync inside a layout effect", "keeps focus on the first invalid field when auto-submit is blocked". Docs: CHANGELOG.md v1.7.0 "Keep focus on the invalid field when `autoSubmit` is blocked".
- **OTP-EVT-15** — MUST. For a single user input the order MUST be: `onValueInvalid` (if any) → `onValueChange` → (commit) → post-commit, pre-paint: Form error clearing, `dirty`, field validation change (OTP-FRM-07) → queued focus move → `onValueComplete` → `requestSubmit()` (when `autoSubmit`). `filled` is published post-commit, pre-paint of the same render, independently.
  Source: `otp-field/root/OTPFieldRoot.tsx:150-152,201-226,423-437`, `otp-field/input/OTPFieldInput.tsx:113-141`. Test: [L4] `OTPFieldRoot.test.tsx` "handles password manager autofill through the hidden input" (invalid, change and complete all observed in order).
- **OTP-EVT-16** — MUST. Consumer DOM handlers on a slot (`onMouseDown`, `onFocus`, `onBlur`, `onChange`, `onKeyDown`, `onPaste`, `onCompositionStart`, `onCompositionEnd`) MUST run before the built-in handler of the same event (RND-EVT-02); `event.preventDefault()` in the consumer handler vetoes the built-in `mousedown`, `focus`, `blur`, `change`, `keydown` and `paste` handling (OTP-PTR-02, OTP-FOC-08, OTP-KEY-01/13/19) and `preventBaseUIHandler()` vetoes any of them (RND-EVT-03). Server: relaxed, see §9.
  Source: `otp-field/input/OTPFieldInput.tsx:165,173,181,208,222,306,335`. Test: [L4] `OTPFieldInput.test.tsx` "allows a composed mousedown handler to prevent focus", "allows a composed focus handler to prevent internal focus state", "allows a composed blur handler to preserve internal focus state".

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md) (§6 tables A–C, OTPField rows). The hidden input (§5.3) is the form-participation and shared validation input; slot 0 is the field's registered control.

- **OTP-FRM-01** — MUST. Native form submission MUST include exactly one entry `name=value` (the normalized value, possibly `''`) when an effective name exists and the hidden input is rendered and not disabled; slots never contribute entries.
  Source: `otp-field/root/OTPFieldRoot.tsx:444-445,451`; FFI-FRM-28. Test: [L4] `OTPFieldRoot.test.tsx` "submits the completed named OTP value from the owning form when enabled", "submits an associated external form when used with `autoSubmit`".
- **OTP-FRM-02** — MUST. Slots MUST never carry `name`; without a valid `length` there is no hidden input and the field submits nothing.
  Source: `otp-field/input/OTPFieldInput.tsx:144-163`, `otp-field/root/OTPFieldRoot.tsx:404`. Test: [L2] `OTPFieldRoot.test.tsx` "renders the hidden validation input with the provided name" (single named input).
- **OTP-FRM-03** — MUST. The `name` prop MUST be offered to the enclosing Field as the control-registered name (so a `Field.Root` without `name` adopts it), while the hidden input carries the effective name (OTP-API-14).
  Source: `otp-field/root/OTPFieldRoot.tsx:163`; FFI-STA-10. Test: none.
- **OTP-FRM-04** — MUST. Native constraint validation of the hidden input MUST apply: `required` → `valueMissing` when the value is `''`; `pattern` (OTP-DOM-24) → `patternMismatch` for any value whose length differs from `length` or that contains characters outside the class; `minlength`/`maxlength` → `tooShort`/`tooLong` under the browser's user-edit rules. Hence a `required`, incomplete numeric/alpha/alphanumeric code blocks `checkValidity()`/submission and a complete one passes; for `validationType="none"` only `required` and the length constraints apply.
  Source: `otp-field/root/OTPFieldRoot.tsx:448-453`. Test: [L4] `OTPFieldRoot.test.tsx` "blocks form submission while the code is incomplete", "allows form submission when the code is complete".
- **OTP-FRM-05** — MUST. With a `form` prop, the hidden input and slots are owned by that form (FFI-DOM-08); `autoSubmit` resolves the same form (OTP-EVT-13).
  Source: `otp-field/root/OTPFieldRoot.tsx:181-186,443`, `otp-field/input/OTPFieldInput.tsx:157`. Test: [L4] `OTPFieldRoot.test.tsx` "submits an associated external form when used with `autoSubmit`".
- **OTP-FRM-06** — MUST. Inside a `Field.Root`, the component MUST register as the field's control with: control handle = slot 0's element, id = the control id, value = the normalized value (re-registered on every value change), name = the `name` prop; registration MUST be withdrawn while the effective disabled state is `true` and on unmount. Server: relaxed, see §9.
  Source: `otp-field/root/OTPFieldRoot.tsx:114-122,163`, `internals/field-register-control/useRegisterFieldControl.ts:21-45`; FFI-ALG-12, FFI-STA-04 (initial value captured from the first registration). Test: [L4] `OTPFieldRoot.test.tsx` "validates the latest value only after focus leaves the OTP field in onBlur mode" (`validate(['1', { otp: '1' }])`), "keeps focus on the first invalid field when auto-submit is blocked".
- **OTP-FRM-07** — MUST. Post-commit, pre-paint of every render in which the rendered value differs from the previous render's (strict inequality — user commits, `value` prop changes, re-normalization), the component MUST run, in order: `clearErrors(effectiveName)` on the enclosing Form; `setDirty(value !== initialValue)` on the Field; `validation.change(value)` (FFI-FRM-02). This sequence MUST NOT run for a canceled request (nothing changed). Server: relaxed, see §9.
  Source: `otp-field/root/OTPFieldRoot.tsx:201-205`; FFI-EVT-02, UTL-STA-36. Test: [L4] `OTPFieldRoot.test.tsx` "blocks invalid Base UI Form auto-submit without calling flushSync inside a layout effect" (validation observed), "ignores hidden-input autofill when readOnly inside Field" (error kept when nothing changes).
- **OTP-FRM-08** — MUST. The Field's `filled` MUST be set to `value !== ''` post-commit, pre-paint on mount and whenever that predicate changes. Server: relaxed, see §9.
  Source: `otp-field/root/OTPFieldRoot.tsx:150-152`; FFI-STA-08. Test: [L4] `OTPFieldRoot.test.tsx` "updates standalone filled and focused state on the root" (root attribute; Field write is the same flag).
- **OTP-FRM-09** — MUST. The Field's `touched` MUST be set to `true` when a slot blurs with `relatedTarget` outside the root (same boundary as OTP-FOC-07); slot-to-slot moves MUST NOT set it; a consumer `onBlur` that prevents default suppresses it.
  Source: `otp-field/root/OTPFieldRoot.tsx:286-291`; FFI-STA-07. Test: none directly (boundary tested by "validates the latest value only after focus leaves the OTP field in onBlur mode").
- **OTP-FRM-10** — MUST. In `validationMode="onBlur"`, when focus leaves the root (OTP-FOC-07 boundary) the component MUST commit the current normalized value to field validation (`validation.commit(value)`); in other modes leaving the root only marks touched and clears `focused`.
  Source: `otp-field/root/OTPFieldRoot.tsx:295-297`; FFI-FRM-03. Test: [L4] `OTPFieldRoot.test.tsx` "validates the latest value only after focus leaves the OTP field in onBlur mode".
- **OTP-FRM-11** — MUST. A native `Field.Label` MUST get `for` equal to slot 0's id (the control id), so clicking it focuses slot 0 natively; the group's `aria-labelledby` is the label id (OTP-ARIA-03) and slots inherit it (OTP-ARIA-06).
  Source: `otp-field/root/OTPFieldRoot.tsx:124,145`; FFI-ARIA-04. Test: [L2] `OTPFieldRoot.test.tsx` "associates Field.Label with the first slot"; `OTPFieldInput.test.tsx` "applies the Field label to every slot".
- **OTP-FRM-12** — MUST. `Field.Description`/`Field.Error` ids MUST appear in the root's `aria-describedby` (merged with the consumer's) and in the hidden input's, and MUST NOT appear on slots (OTP-ARIA-05/10/11).
  Source: `otp-field/root/OTPFieldRoot.tsx:127-131,387,406`. Test: [L2] `OTPFieldRoot.test.tsx` "applies the Field description to the group".
- **OTP-FRM-13** — MUST. A `change`/`input` event on the hidden input (browser or password-manager autofill) MUST be processed as: ignored when default-prevented, disabled or read-only (no callbacks, no value change, Form errors untouched); otherwise the input's value is normalized (OTP-STA-04), `onValueInvalid(raw, { reason: 'input-change' })` fires when characters were rejected, and the normalized value is requested with `reason: 'input-change'` (replacing the whole value, not inserting at a slot); on commit of a non-empty value, focus moves to slot `v.length − 1` (OTP-FOC-13). Completion applies per OTP-EVT-08.
  Source: `otp-field/root/OTPFieldRoot.tsx:410-438`; FFI-FRM-31. Test: [L4] `OTPFieldRoot.test.tsx` "accepts valid hidden-input autofill and preserves focus when autofill is cleared", "handles password manager autofill through the hidden input", "composes validation and custom normalization during hidden input autofill", "ignores hidden-input autofill when $lockState inside Field", "ignores hidden-input autofill when $lockState outside Field". Docs: CHANGELOG.md v1.5.0 "Ignore hidden input changes while readonly or disabled", "Prevent locked hidden autofill validation".
- **OTP-FRM-14** — MUST. The component MUST NOT listen for the form `reset` event; a native reset does not change the component's value, flags or attributes (the browser resets the hidden input's DOM value, which the next render re-synchronizes to the component value — see §14).
  Source: `otp-field/root/OTPFieldRoot.tsx:404-458` (no reset handling); FFI-FRM-30. Test: none.
- **OTP-FRM-15** — MUST. A Form error registered for the effective name MUST make the field invalid (`data-invalid` on root/slots via the field's `valid === false`, `aria-invalid` on slots via OTP-ARIA-09, `Field.Error` text) and MUST be cleared on the next rendered-value change (OTP-FRM-07); when nothing changes (locked autofill) the error persists.
  Source: `otp-field/root/OTPFieldRoot.tsx:202`, `field/root/FieldRoot.tsx:97`; FFI-FRM-25, FFI-FRM-26. Test: [L2] `OTPFieldRoot.test.tsx` "ignores hidden-input autofill when $lockState inside Field".
- **OTP-FRM-16** — MUST. While the effective disabled state is `true`: the hidden input is disabled (not submitted, not validated — FFI-FRM-23), the control registration is withdrawn (the Form neither validates nor focuses it), slots carry `disabled`, and `aria-invalid` is suppressed on slots and hidden input.
  Source: `otp-field/root/OTPFieldRoot.tsx:163,406,451`, `otp-field/input/OTPFieldInput.tsx:156,162`. Test: [L2] `OTPFieldRoot.test.tsx` "ignores hidden-input autofill when disabled inside Field", "is removed when the focused field becomes disabled".

## 9. Lifecycle, mounting and animation

Not applicable — every part is always mounted while rendered; there is no transition status, `keepMounted`, completion callback or imperative handle. The only conditional element is the hidden input, whose presence depends solely on `length` validity (OTP-STR-01). The development warnings of §12 are evaluated after paint.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):
- OTP-KEY-01 — consumer veto: met on both modes from a consumer JavaScript listener (the `otp-field` module checks `defaultPrevented`); a C# veto is `PreventBaseUIHandler()`, synchronous on WebAssembly via `decide()`, a round trip late on Server (fallback "run"); Blazor event directives rejected on parts
- OTP-KEY-13 — consumer veto: met on both modes from a consumer JavaScript listener (the `otp-field` module checks `defaultPrevented`); a C# veto is `PreventBaseUIHandler()`, synchronous on WebAssembly via `decide()`, a round trip late on Server (fallback "run"); Blazor event directives rejected on parts
- OTP-KEY-19 — consumer veto: met on both modes from a consumer JavaScript listener (the `otp-field` module checks `defaultPrevented`); a C# veto is `PreventBaseUIHandler()`, synchronous on WebAssembly via `decide()`, a round trip late on Server (fallback "run"); Blazor event directives rejected on parts
- OTP-PTR-02 — consumer veto: met on both modes from a consumer JavaScript listener (the `otp-field` module checks `defaultPrevented`); a C# veto is `PreventBaseUIHandler()`, synchronous on WebAssembly via `decide()`, a round trip late on Server (fallback "run"); Blazor event directives rejected on parts
- OTP-FOC-08 — consumer veto: met on both modes from a consumer JavaScript listener (the `otp-field` module checks `defaultPrevented`); a C# veto is `PreventBaseUIHandler()`, synchronous on WebAssembly via `decide()`, a round trip late on Server (fallback "run"); Blazor event directives rejected on parts
- OTP-EVT-16 — consumer veto: met on both modes from a consumer JavaScript listener (the `otp-field` module checks `defaultPrevented`); a C# veto is `PreventBaseUIHandler()`, synchronous on WebAssembly via `decide()`, a round trip late on Server (fallback "run"); Blazor event directives rejected on parts
- OTP-FRM-06 — FFI registration / value-changed side effects (post-commit, pre-paint C# state work): after paint plus a round trip; observable only as latency of dependent attribute updates
- OTP-FRM-07 — FFI registration / value-changed side effects (post-commit, pre-paint C# state work): after paint plus a round trip; observable only as latency of dependent attribute updates
- OTP-FRM-08 — FFI registration / value-changed side effects (post-commit, pre-paint C# state work): after paint plus a round trip; observable only as latency of dependent attribute updates

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (Root) | [RND](../architecture/rendering-and-composition.md) | default tag `div`; attribute sources in order: `{ role: 'group', 'aria-describedby', 'aria-labelledby' }` → consumer props; DOM handles: consumer + internal root handle; state = Root state; mapping: `value → null`, `length → null`, `valid → data-valid/data-invalid` |
| `useRenderElement` (Input) | RND | default tag `input`; sources: built-in slot attributes/handlers (§5.2, §6) → consumer props; handles: consumer + composite-list item handle + internal slot handle; state = Input state; mapping: `value → null`, `index → null`, `valid → …` (note: `length` not suppressed, OTP-DOM-18) |
| `getStateAttributesProps` | RND | default projection for all other fields (booleans → `data-<lowercase>=""`, numbers → string) |
| `mergeProps` handler chaining | RND-EVT-02/03 | consumer handlers first; `preventDefault()` checks in built-in handlers (`event.defaultPrevented`) give consumers a second veto besides `preventBaseUIHandler()` |
| `describeConformance` | RND §4.9 | Root: `refInstanceof: HTMLDivElement`, minimal element `<OTPField.Root length={1} />`; Input: `refInstanceof: HTMLInputElement`, rendered inside `<OTPField.Root length={1}>`; defaults otherwise (`testRenderPropWith: 'div'`, `button: false`, `wrappingAllowed: true`) |
| `useControlled` | [UTL](../architecture/shared-utilities.md) §4.1 | `{ controlled: value, default: defaultValue ('' default), name: 'OTPField', state: 'value' }` |
| `useValueChanged` | UTL-STA-36 | watches the normalized value; runs OTP-FRM-07, the queued focus move (OTP-FOC-10) and pending completion (OTP-EVT-10) |
| `CompositeList` / `useCompositeListItem` | [CMP](../architecture/composite-and-list-navigation.md) | Root hosts the item registry (`elementsRef` = slot elements by index; `onMapChange` → rendered-slot count for OTP-EDGE-02); each Input registers with `{ guess: true }`, no explicit index, no label/metadata; no list navigation, typeahead or roving-tabindex mechanism is used (tabindex is computed by the Root, OTP-FOC-01/02) |
| `useLabelableId` | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ALG-07 | `{ id: idProp }`; the resolved id is slot 0's id (OTP-API-18) |
| `useAriaLabelledBy` | FFI-ALG-08 | `(ariaLabelledByProp, fieldLabelId, slot0Handle, enableFallback = true, controlId, ariaLabel = undefined)` → group `aria-labelledby`; the generated fallback label id is `${controlId}-label` |
| `getDescriptionProps` | FFI-ARIA-01 | `getDescriptionProps({})['aria-describedby']` merged with the consumer's ids onto the group (OTP-ARIA-05); also applied to the hidden input through `getValidationProps` |
| `useRegisterFieldControl` | FFI-ALG-12 | `(slot0Handle, controlId, value, undefined, enabled = !disabled, nameProp)` |
| `useSetFieldFocused` | FFI-ALG-13 | per slot: `(disabled, slotHandle, onFocusedChange = root setFocused)`; the Root's blur handler additionally clears the Field's `focused` directly |
| `validation.getValidationProps` / `inputRef` / `change` / `commit` | FFI §5 | `getValidationProps(disabled, { onFocus, onChange })` on the hidden input (which is `validation.inputRef`); `change(value)` after every value change; `commit(value)` on blur leaving the root in `onBlur` mode |
| `useFormContext().clearErrors` | FFI-EVT-03 | `clearErrors(effectiveName)` on every value change |
| `createChangeEventDetails` / `createGenericEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | change details for `onValueChange` (no trigger, no custom fields); generic details for `onValueInvalid` and `onValueComplete` |
| `visuallyHidden` / `visuallyHiddenInput` | UTL-DOM-13/14 | hidden input style chosen by presence of an effective name |
| `warn` (`createLogOnce('warn', 'Base UI')`) | UTL-EDGE-11/12 | all four OTP dev warnings (§12); each message logged once per page lifetime, prefixed `Base UI: ` |
| `platform.os.android` | UTL-EDGE-05 | disables IME buffering (OTP-KEY-22) |
| `useDirection` | UTL-I18N-01 | swaps the previous/next arrow keys (OTP-I18N-01) |
| `contains` (shadow-DOM aware) | UTL-DOM-20 | `contains(rootElement, blur.relatedTarget)` decides the focus boundary (OTP-FOC-07) |
| `stopEvent` | `floating-ui-react/utils/event.ts:3-6` | `preventDefault()` + `stopPropagation()` on handled keydowns |
| `ownerDocument` | UTL-DOM-18 | `getElementById(form)` lookup for `autoSubmit` (OTP-EVT-13) |
| `Separator` | [separator](separator.md) | re-exported unchanged as `OTPField.Separator` |

## 12. Edge cases, invariants and dev warnings

All warnings below are development-only, written through the shared once-per-message logger (UTL-EDGE-11/12): each appears as `Base UI: <message>`, possibly followed by a space and an owner-stack string, and is emitted at most once per unique message text per page lifetime.

- **OTP-EDGE-01** — MUST. Rendering `OTPField.Input` outside an `OTPField.Root` MUST throw an error whose message is `Base UI: OTPFieldRootContext is missing. OTPField parts must be placed within <OTPField.Root>.`
  Source: `otp-field/root/OTPFieldRootContext.ts:38-42`. Test: [L2] `OTPFieldInput.test.tsx` "throws a descriptive error when rendered outside <OTPField.Root>".
- **OTP-EDGE-02** — MUST. After paint, whenever `length` is a positive integer, at least one Input is rendered, and the rendered Input count `n` differs from `length`, the Root MUST log the warning `<OTPField.Root> \`length\` must match the number of rendered <OTPField.Input /> parts. Received \`length={<length>}\` but rendered <n> input<s>.` where `<s>` is empty when `n === 1` and `s` otherwise (e.g. `Received \`length={6}\` but rendered 5 inputs.`, `Received \`length={2}\` but rendered 1 input.`). Re-evaluated whenever `length` or the rendered count changes; not logged while no Input is rendered.
  Source: `otp-field/root/OTPFieldRoot.tsx:142,154-161,396-401,649-663`. Test: [L2] `OTPFieldRoot.test.tsx` "warns when length does not match the rendered input count", "warns with singular input wording when one input is rendered".
- **OTP-EDGE-03** — MUST. After paint, whenever `length` is not a positive integer (`0`, negative, non-integer, `NaN`, `±Infinity`), the Root MUST log `<OTPField.Root> \`length\` must be a positive integer. Received \`length={<String(length)>}\`.` (e.g. `length={NaN}`, `length={Infinity}`, `length={3.7}`).
  Source: `otp-field/root/OTPFieldRoot.tsx:665-675`. Test: [L2] `OTPFieldRoot.test.tsx` "warns when length is not a positive integer (%p)".
- **OTP-EDGE-04** — MUST. After paint, when slot 0 received a consumer `aria-label` and the slot element has no associated native labels (`labels` list empty — no `<label for>`, wrapping `<label>` or `Field.Label` association), the Input MUST log `<OTPField.Input> ignores \`aria-label\` on the first input. Use a \`<label>\` or \`<Field.Label>\` to label the OTP field.`; no warning when a label is associated.
  Source: `otp-field/input/OTPFieldInput.tsx:85-99`. Test: [L4] `OTPFieldInput.test.tsx` "warns when aria-label is provided on the first slot without an associated label", "does not warn for a first-slot aria-label when a native label is associated", "does not warn for a first-slot aria-label when Field.Label is associated".
- **OTP-EDGE-05** — MUST. When reading `text/plain` from a paste event's clipboard data throws, the Input MUST log `<OTPField.Input> could not read clipboard text during paste handling.` and abandon the paste (OTP-KEY-20).
  Source: `otp-field/input/OTPFieldInput.tsx:312-325`. Test: [L4] `OTPFieldInput.test.tsx` "warns in development when clipboard text cannot be read during paste handling".
- **OTP-EDGE-06** — SHOULD. The controlled/uncontrolled mode-switch and changed-default diagnostics of UTL-EDGE-22/23 apply with `name: 'OTPField'`, `state: 'value'`.
  Source: `otp-field/root/OTPFieldRoot.tsx:97-102`, `packages/utils/src/useControlled.ts:48-80`. Test: none in OTP.
- **OTP-EDGE-07** — MUST. With an invalid `length` the component MUST still render without throwing: no hidden input (OTP-STR-01); the value normalizes per OTP-STA-04 (`NaN` or negative → `''`; `Infinity` → unclamped; non-integer → truncated count); slot attributes use `length` verbatim (`enterkeyhint` compares against `length − 1`, `maxlength` on slot 0 receives the raw value); keyboard `last` is `max(length − 1, 0)`.
  Source: `otp-field/root/OTPFieldRoot.tsx:136,404`, `otp-field/utils/otp.ts:70-76`, `otp-field/input/OTPFieldInput.tsx:152,154,227`. Test: [L2] `otp.test.ts` "returns an empty string for negative lengths"; `OTPFieldRoot.test.tsx` "warns when length is not a positive integer (%p)" (renders without error).
- **OTP-EDGE-08** — MUST. While effectively disabled: every slot and the hidden input carry `disabled`; `mousedown`, `focus`, `keydown`, `change`, `paste` and composition commits are inert; `aria-invalid` is suppressed; `data-disabled` is on root and slots; the field registration is withdrawn; a focused slot that becomes disabled releases the focused flag (OTP-FOC-09).
  Source: `otp-field/root/OTPFieldRoot.tsx:94,163,406,411,451`, `otp-field/input/OTPFieldInput.tsx:156,162,165,173,203,208,222,306`. Test: [L2] `OTPFieldRoot.test.tsx` "disables every slot and prevents value changes", "is removed when the focused field becomes disabled"; `OTPFieldInput.test.tsx` "leaves vertical arrow navigation unhandled in disabled mode" (L4 for the inert-event clause).
- **OTP-EDGE-09** — MUST. While `readOnly`: slots and hidden input carry `readonly`; focus, selection, navigation keys, the focused/touched flags and blur-commit keep working; typing, composition commits, paste, Backspace/Delete and autofill change nothing and fire no callbacks; form submission still includes the value; `aria-invalid` is unaffected.
  Source: `otp-field/root/OTPFieldRoot.tsx:68,411,452`, `otp-field/input/OTPFieldInput.tsx:159,203,208,258,306`. Test: [L2] `OTPFieldRoot.test.tsx` "marks every slot as readonly and prevents value changes", "tracks focus state when a readonly slot receives focus", "ignores hidden-input autofill when readOnly inside Field"; `OTPFieldInput.test.tsx` "keeps arrow and home/end navigation working in readonly mode", "blocks Delete and Backspace from changing the value in readonly mode", "blocks paste from changing the value in readonly mode" (L4 for the focus, navigation and blur clause).
- **OTP-EDGE-10** — MUST. Server-rendered markup MUST contain the group, every slot with its final attributes (ids per OTP-DOM-07 when a deterministic id generator exists, `value`, `tabindex`, `maxlength`, `pattern`, `autocomplete`) and the hidden input with its constraints, identical to the client's first render; slot indexes are assigned from render order so no corrective update is needed for in-order lists.
  Source: `otp-field/input/OTPFieldInput.tsx:68`, `otp-field/root/OTPFieldRoot.tsx:404-458`; CMP-STA-16, RND-EDGE-09. Test: [L2] `OTPFieldRoot.test.tsx` "renders visible inputs with unique IDs", "renders a hidden validation input with the provided length", "renders the hidden validation input with an alphanumeric pattern during SSR"; `OTPFieldRoot.react17.test.tsx` "omits generated slot ids during SSR until the client fallback is assigned".
- **OTP-EDGE-11** — MUST. Removing or adding Inputs at runtime MUST re-index the remaining slots by document order post-commit, pre-paint (CMP-STA-18); focus ownership follows OTP-FOC-09; the value is unaffected (it is clamped by `length`, not by the slot count).
  Source: `otp-field/input/OTPFieldInput.tsx:68`, `internals/composite/list/CompositeList.tsx:143-171`. Test: [L2] `OTPFieldRoot.test.tsx` "is kept when a previously focused slot unmounts after focus moves to a sibling", "is removed when the focused slot unmounts but its field remains" (L4 for the focus-ownership clause).
- **OTP-EDGE-12** — MUST. Characters outside the Basic Multilingual Plane are clamped as single code points (OTP-STA-04) but indexed into slots, measured for `complete`, and removed/replaced by UTF-16 code unit (OTP-STA-07/08/09/11); with `validationType="none"` such a character therefore occupies two slots as lone surrogates. A port MUST reproduce the code-unit semantics (see §16).
  Source: `otp-field/utils/otp.ts:69-74,101-102,117`, `otp-field/input/OTPFieldInput.tsx:79`, `otp-field/root/OTPFieldRoot.tsx:314`. Test: none.
- **OTP-EDGE-13** — MUST. Duplicate ids in the described-by composition MUST be emitted once (first occurrence wins); whitespace-only consumer values yield no attribute.
  Source: `otp-field/root/OTPFieldRoot.tsx:639-642`. Test: none.
- **OTP-EDGE-14** — MUST. Changing `length` at runtime MUST re-normalize the value (truncating when shrinking), re-evaluate completion, hidden-input constraints and presence, slot `enterkeyhint`/`maxlength`, and the active slot; it MUST NOT fire `onValueChange` or `onValueComplete`.
  Source: `otp-field/root/OTPFieldRoot.tsx:136,138,146-148,314,448-450`. Test: [L2] `OTPFieldRoot.test.tsx` "is kept when a previously focused slot unmounts after focus moves to a sibling" (`length` 3 → 2 with `defaultValue="1"`).
- **OTP-I18N-01** — MUST. Only the previous/next arrow mapping depends on the reading direction from the nearest `DirectionProvider` (`'ltr'` default): in RTL, <kbd>ArrowRight</kbd> is "previous" and <kbd>ArrowLeft</kbd> is "next" (also with the boundary modifier). Slot order, value order, <kbd>Home</kbd>/<kbd>End</kbd>/<kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>, <kbd>Backspace</kbd>/<kbd>Delete</kbd> and focus redirects are direction-independent; no locale affects character handling.
  Source: `otp-field/input/OTPFieldInput.tsx:71,230-232`, `internals/direction-context/DirectionContext.ts:12-16`; UTL-I18N-01/03. Test: [L4] `OTPFieldInput.test.tsx` "moves focus with arrow keys in RTL", "moves focus to the field boundaries with Ctrl/Cmd + arrow keys in RTL". Docs: CHANGELOG.md v1.5.0 "Fix RTL arrow navigation".

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render callback warning (RND-EDGE-01/03).

## 13. Behaviors evidenced by tests

Every test maps to existing IDs; no requirement exists only because of a test. Conformance rows cite RND §4.9 per conventions §4.

### `root/OTPFieldRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; minimal element `<OTPField.Root length={1} />`; defaults `testRenderPropWith: 'div'`, `button: false`, `wrappingAllowed: true`) | RND-DOM-36…48, OTP-API-19, OTP-API-20 |
| "splits the default value across inputs" | OTP-API-03, OTP-STA-04, OTP-STA-06, OTP-STA-11, OTP-DOM-08 |
| "clamps an overlong default value to the rendered slot count" | OTP-API-03, OTP-STA-04, OTP-DOM-14, OTP-DOM-22 |
| "assigns slot indexes from render order when omitted" | OTP-STR-03 |
| "supports grouped layouts without affecting slot counting" | OTP-STR-01, OTP-STR-03, OTP-STR-05, OTP-DOM-27, OTP-ARIA-01 |
| "updates the rendered value in controlled mode" | OTP-API-02, OTP-STA-03 |
| "supports alphabetic values when set to `alpha`" / "supports alphanumeric values when set to `alphanumeric`" | OTP-API-07, OTP-STA-05, OTP-STA-06 |
| "supports typing alphabetic values when set to `alpha`" | OTP-STA-05, OTP-KEY-14 |
| "applies single-character validation to each visible slot" | OTP-DOM-16, OTP-STA-05 |
| "renders the hidden validation input with an alphanumeric pattern during SSR" | OTP-DOM-24, OTP-EDGE-10 |
| "omits the hidden validation pattern when set to `none`" | OTP-API-07, OTP-DOM-24 |
| "allows a custom inputMode when validation is set to `none`" / "allows overriding the built-in inputMode when needed" | OTP-API-09, OTP-DOM-10, OTP-DOM-23 |
| "supports custom normalization when `validationType` is `none`" | OTP-API-08, OTP-STA-04 |
| "composes with built-in validation and advances focus" | OTP-API-08, OTP-STA-04, OTP-FOC-10 |
| "composes built-in validation and custom normalization for pasted values" | OTP-API-08, OTP-KEY-18 |
| "composes built-in validation and custom normalization from a non-first slot" | OTP-STA-07, OTP-KEY-15, OTP-FOC-10 |
| "fires `input-change` when typing" | OTP-API-04, OTP-EVT-01, OTP-EVT-02 |
| "fires `input-clear` when clearing a slot by input" | OTP-KEY-16, OTP-EVT-01, OTP-EVT-02 |
| "fires when typing is normalized before the OTP value updates" / "fires when custom normalization removes characters" / "fires when custom normalization removes characters after built-in validation" / "fires when built-in validation removes characters before custom normalization expands the value" / "fires when custom normalization removes all characters after built-in validation" | OTP-API-05, OTP-STA-04, OTP-KEY-14, OTP-KEY-17, OTP-EVT-07 |
| "fires `input-paste` when pasted text is normalized before the OTP value updates" / "fires `input-paste` when custom normalization removes characters after built-in validation" | OTP-KEY-18, OTP-EVT-07 |
| "fires when typing completes the OTP" / "fires `input-paste` when pasting completes the OTP" / "fires `input-paste` when pasting into a middle slot completes the OTP" | OTP-API-06, OTP-EVT-08, OTP-EVT-10 |
| "fires `input-paste` when a complete paste replaces a complete OTP" | OTP-EVT-03, OTP-EVT-08, OTP-EVT-09 |
| "does not fire when a completion-making paste is canceled" | OTP-EVT-04, OTP-EVT-11 |
| "does not fire before the OTP becomes complete" | OTP-EVT-08 |
| "does not fire later for a stale controlled completion attempt" / "fires after an asynchronously accepted controlled completion" / "does not fire again when a controlled value changes from one complete value to another" | OTP-STA-03, OTP-EVT-05, OTP-EVT-10, OTP-EVT-11 |
| "associates Field.Label with the first slot" | OTP-API-18, OTP-FRM-11 |
| "applies the Field description to the group" | OTP-ARIA-03, OTP-ARIA-05, OTP-FRM-12 |
| "validates the latest value only after focus leaves the OTP field in onBlur mode" | OTP-API-14, OTP-FOC-07, OTP-FRM-06, OTP-FRM-10 |
| "forwards root `aria-describedby` to the group" | OTP-ARIA-05 |
| "forwards root `aria-labelledby` to the group only" | OTP-ARIA-02, OTP-ARIA-06 |
| "applies the default autocomplete to the first slot only" / "allows overriding the autocomplete attribute" | OTP-API-11, OTP-DOM-11, OTP-DOM-23 |
| "disables every slot and prevents value changes" | OTP-API-15, OTP-DOM-03, OTP-DOM-15, OTP-KEY-13, OTP-EDGE-08 |
| "marks every slot as readonly and prevents value changes" | OTP-API-16, OTP-DOM-03, OTP-DOM-15, OTP-KEY-13, OTP-EDGE-09 |
| "tracks focus state when a readonly slot receives focus" | OTP-DOM-04, OTP-FOC-12 |
| "renders password slot inputs when enabled" | OTP-API-10, OTP-DOM-09 |
| "allows overriding the input type on individual slots" | OTP-API-23, OTP-DOM-09, OTP-DOM-19 |
| "fills consecutive slots when typing multiple characters into the first input" | OTP-STA-02, OTP-KEY-15 |
| "replaces consecutive slots when typing multiple characters into a later input" | OTP-STA-07, OTP-KEY-15 |
| "fills consecutive slots when pasting a code" | OTP-KEY-18, OTP-EVT-02 |
| "removes a character and moves focus to the previous slot with backspace" | OTP-KEY-10, OTP-EVT-02 |
| "does not fire `onValueChange` for Delete on an empty slot" | OTP-KEY-08, OTP-EVT-03 |
| "does not fire `onValueChange` for Backspace on an already-empty first slot" | OTP-KEY-10, OTP-EVT-03 |
| "does not move focus later for a stale controlled change" / "moves focus after an asynchronously accepted controlled change" | OTP-API-02, OTP-FOC-10 |
| "blocks form submission while the code is incomplete" / "allows form submission when the code is complete" | OTP-API-17, OTP-FRM-04 |
| "renders the hidden validation input with the provided name" | OTP-STR-01, OTP-API-14, OTP-DOM-20, OTP-FRM-02 |
| "redirects hidden validation input focus to the first visible slot" | OTP-FOC-06 |
| "accepts valid hidden-input autofill and preserves focus when autofill is cleared" | OTP-FRM-13, OTP-FOC-13 |
| "handles password manager autofill through the hidden input" | OTP-FRM-13, OTP-EVT-02, OTP-EVT-07, OTP-EVT-15 |
| "composes validation and custom normalization during hidden input autofill" | OTP-FRM-13 |
| "ignores hidden-input autofill when $lockState inside Field" (readOnly, disabled) | OTP-FRM-13, OTP-FRM-15, OTP-FRM-16, OTP-ARIA-09, OTP-FRM-07 |
| "ignores hidden-input autofill when $lockState outside Field" (readOnly, disabled) | OTP-FRM-13, OTP-ARIA-09 |
| "does not auto-submit the owning form when the OTP becomes complete by default" | OTP-API-12 |
| "submits the completed named OTP value from the owning form when enabled" | OTP-API-12, OTP-EVT-13, OTP-FRM-01 |
| "keeps the completed value when the owning form has no requestSubmit method" | OTP-EVT-13 |
| "does not call flushSync inside a layout effect when auto-submitting a Base UI Form" / "blocks invalid Base UI Form auto-submit without calling flushSync inside a layout effect" | OTP-EVT-14, OTP-FRM-07 |
| "keeps focus on the first invalid field when auto-submit is blocked" | OTP-EVT-14, OTP-FOC-11, OTP-FRM-06 |
| "does not submit the owning form before the OTP becomes complete when enabled" | OTP-EVT-08, OTP-EVT-13 |
| "submits an associated external form when used with `autoSubmit`" | OTP-API-13, OTP-EVT-13, OTP-FRM-01, OTP-FRM-05 |
| "does not submit an ancestor form when the explicit form id is not a form" | OTP-EVT-13 |
| "renders visible inputs with unique IDs" | OTP-STR-03, OTP-API-18, OTP-DOM-01, OTP-DOM-07, OTP-EDGE-10 |
| "renders a hidden validation input with the provided length" | OTP-STA-05, OTP-DOM-24, OTP-DOM-25, OTP-EDGE-10 |
| "is removed when the focused field becomes disabled" | OTP-DOM-05, OTP-FOC-09, OTP-EDGE-08 |
| "is removed when the focused field unmounts" | OTP-FOC-09 |
| "is kept when a previously focused slot unmounts after focus moves to a sibling" | OTP-FOC-09, OTP-EDGE-11, OTP-EDGE-14 |
| "is removed when the focused slot unmounts but its field remains" | OTP-STR-04, OTP-FOC-09, OTP-EDGE-11 |
| "updates standalone filled and focused state on the root" | OTP-API-20, OTP-STA-10, OTP-STA-14, OTP-DOM-04, OTP-FOC-04, OTP-FOC-07, OTP-FRM-08 |
| "sets `data-complete` when all slots are filled" | OTP-STA-09, OTP-STA-14, OTP-DOM-02 |
| "renders a fallback hidden input id when name is not provided" | OTP-DOM-21 |
| "warns when length does not match the rendered input count" / "warns with singular input wording when one input is rendered" | OTP-STR-04, OTP-EDGE-02 |
| "warns when length is not a positive integer (%p)" (`0`, `-1`, `3.7`, `NaN`, `Infinity`) | OTP-API-01, OTP-EDGE-03, OTP-EDGE-07 |

### `input/OTPFieldInput.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLInputElement`; `render` wraps the node in `<OTPField.Root length={1}>`; defaults otherwise) | RND-DOM-36…48, OTP-API-23 |
| "renders one textbox per slot" | OTP-STR-02 |
| "moves focus with arrow keys" | OTP-KEY-02 |
| "moves focus with arrow keys in RTL" | OTP-KEY-02, OTP-I18N-01 |
| "redirects focus to the first empty slot when a later empty slot is focused" | OTP-FOC-03, OTP-PTR-04 |
| "moves focus to the next slot after typing" | OTP-KEY-15, OTP-FOC-10 |
| "selects the last slot after typing into it for the first time" | OTP-KEY-15, OTP-FOC-05, OTP-FOC-10 |
| "keeps focus in place when typing is canceled" | OTP-EVT-04, OTP-FOC-10 |
| "keeps the filled slot selected when typing an invalid character" | OTP-KEY-17 |
| "commits an IME composition once on compositionend instead of per intermediate change" | OTP-DOM-08, OTP-KEY-21 |
| "reports characters rejected from a committed IME composition" | OTP-KEY-21, OTP-EVT-07 |
| "ignores keyboard commands while a composition is buffered" | OTP-KEY-01, OTP-KEY-21 |
| "does not commit a composition that ends after the field becomes $prop" (disabled, readOnly) | OTP-KEY-21 |
| "selects the slot value on mousedown" | OTP-PTR-01, OTP-FOC-04 |
| "allows a composed mousedown handler to prevent focus" | OTP-PTR-02, OTP-DOM-19, OTP-EVT-16 |
| "allows a composed focus handler to prevent internal focus state" / "allows a composed blur handler to preserve internal focus state" | OTP-FOC-08, OTP-EVT-16 |
| "moves focus to the next slot when typing the same character into a filled slot" / "does not reselect the final slot when typing the same character" | OTP-KEY-09 |
| "moves focus to the field boundary with %s" (ArrowUp, ArrowDown) | OTP-KEY-04, OTP-KEY-05 |
| "stops propagation when ArrowDown moves focus to the empty end slot" / "keeps focus on the empty end slot with ArrowDown" / "keeps focus on the final slot with ArrowDown when the value is complete" | OTP-KEY-05, OTP-STA-12 |
| "moves focus to the first slot with Home" | OTP-KEY-04 |
| "moves focus to the empty end slot with End" | OTP-KEY-05, OTP-STA-12 |
| "moves focus to the field boundaries with %s + arrow keys" (Ctrl, Cmd) | OTP-KEY-03 |
| "moves focus to the field boundaries with %s + arrow keys in RTL" (Ctrl, Cmd) | OTP-KEY-03, OTP-I18N-01 |
| "keeps arrow and home/end navigation working in readonly mode" | OTP-API-16, OTP-KEY-06, OTP-EDGE-09 |
| "leaves vertical arrow navigation unhandled in disabled mode" | OTP-KEY-01, OTP-EDGE-08 |
| "blocks Delete and Backspace from changing the value in readonly mode" | OTP-KEY-06, OTP-EDGE-09 |
| "blocks paste from changing the value in readonly mode" | OTP-KEY-19, OTP-EDGE-09 |
| "warns in development when clipboard text cannot be read during paste handling" | OTP-KEY-20, OTP-EDGE-05 |
| "ignores a paste event when clipboard text is unavailable" | OTP-KEY-18 |
| "allows tabbing out of the field from the active slot" | OTP-STA-13, OTP-KEY-12, OTP-FOC-01, OTP-FOC-02 |
| "deletes the current character and moves focus to the previous slot on backspace" / "deletes the previous filled slot when backspacing on an empty non-first slot" | OTP-KEY-10 |
| "clears all slots with %s + Backspace" (Ctrl, Cmd) | OTP-KEY-07 |
| "keeps focus in place when backspace is canceled" | OTP-KEY-11, OTP-EVT-04 |
| "deletes the current character with Delete without moving focus" | OTP-KEY-08, OTP-FOC-05 |
| "selects the previous slot value after backspacing into the first slot" | OTP-KEY-10, OTP-FOC-05 |
| "keeps focus in place when paste is canceled" | OTP-KEY-18, OTP-EVT-04 |
| "replaces values from the middle when pasting into a later slot" | OTP-STA-07, OTP-KEY-18 |
| "marks each input as complete when all slots are filled" | OTP-STA-11, OTP-DOM-17 |
| "adds disabled and readonly state attributes to each slot" | OTP-DOM-17 |
| "applies the Field label to every slot" | OTP-ARIA-06, OTP-ARIA-10, OTP-FRM-11 |
| "applies a native label to every slot" | OTP-ARIA-04, OTP-ARIA-06 |
| "keeps the shared label on the first slot even if an aria-label is provided" | OTP-API-21, OTP-ARIA-07, OTP-ARIA-08 |
| "warns when aria-label is provided on the first slot without an associated label" / "does not warn for a first-slot aria-label when a native label is associated" / "does not warn for a first-slot aria-label when Field.Label is associated" | OTP-EDGE-04 |
| "throws a descriptive error when rendered outside <OTPField.Root>" | OTP-STR-02, OTP-EDGE-01 |

### `input/OTPFieldInput.android.test.tsx`

| Test | Requirements |
|---|---|
| "commits each change during an IME composition" (`platform.os.android` mocked `true`) | OTP-KEY-22, OTP-KEY-23 |

### `root/OTPFieldRoot.react17.test.tsx`

| Test | Requirements |
|---|---|
| "omits generated slot ids during SSR until the client fallback is assigned" (`SafeReact.useId` and `captureOwnerStack` mocked `undefined`) | OTP-DOM-07, OTP-EDGE-10 |

### `utils/otp.test.ts`

| Test | Requirements |
|---|---|
| "removes whitespace from pasted values" / "returns an empty string when stripping nullish values" | OTP-STA-04 (step 1) |
| "normalizes, filters, and clamps numeric values" / "normalizes alphabetic values" / "normalizes alphanumeric values" / "returns an empty string for nullish input values" | OTP-STA-04, OTP-STA-05 |
| "uses custom normalization when validationType is none" / "applies custom normalization after built-in validation" / "filters custom normalization output through built-in validation" / "clamps values after custom normalization" | OTP-API-08, OTP-STA-04 (step 3–4) |
| "returns an empty string for negative lengths" | OTP-STA-04, OTP-EDGE-07 |
| "replaces values from the middle of the OTP" / "replaces values from the first slot" / "replaces values at the last slot" / "applies custom normalization when replacing OTP values" / "preserves suffix characters when custom normalization removes part of a middle replacement" | OTP-STA-07 |
| "removes a character from the first slot" / "removes a character from the last slot" / "leaves the value unchanged when removing an out-of-bounds index" | OTP-STA-08 |

### `root/OTPFieldRoot.spec.tsx` and `input/OTPFieldInput.spec.tsx` (type-level)

| Assertion | Requirements |
|---|---|
| `details.event` narrows to `ClipboardEvent` / `KeyboardEvent` / `InputEvent \| Event` / `InputEvent \| FocusEvent \| Event` by reason; `KeyboardEvent` rejected for `input-clear`; `onValueInvalid`/`onValueComplete` narrow to `ClipboardEvent` / `InputEvent \| Event` | §3.4 types, OTP-EVT-06 |
| `<OTPField.Root />` without `length` is a type error | OTP-API-01 |
| `inputMode="numeric"` with `validationType="none"`; `inputMode="tel"` with `"numeric"` | OTP-API-09 |
| `normalizeValue` accepted; `sanitizeValue` rejected | OTP-API-08 |
| `<OTPField.Input index={0} />` is a type error | OTP-API-24 |
| `render` callback receives `disabled`/`readOnly` typed as `boolean \| undefined` | OTP-API-23 |

### Additional requirements introduced here

None.

## 14. React-specific notes and porting considerations

Non-normative.

- **Controlled-input restoration.** Every slot and the hidden input are React controlled inputs. After a change event in which the component does not update state (all typed characters rejected into an empty slot, OTP-KEY-17; a canceled request; a locked hidden-input autofill), React itself rewrites the DOM `value` back to the controlled value. The source only restores explicitly for the *filled*-slot case (`OTPFieldInput.tsx:126-129`). A port that drives real inputs must re-sync the DOM value to the slot character after every ignored or canceled change, and re-sync the hidden input after a native form reset (OTP-FRM-14). Blazor's diff does not rewrite an unchanged `value` attribute, so this is an explicit JS write (and `select()`); see §16 items 6 and 9. Port: see decisions/01-hosting-model-and-timing.md §Decision item 2.
- **Timing phases.** `setFilled`, the value-change sequence (`clearErrors` → `setDirty` → `validation.change` → queued focus → pending completion → `autoSubmit`), field-control registration, label-id claim, the `aria-labelledby` fallback discovery, composite index publication and focus-ownership checks all run in layout effects: **post-commit, pre-paint**. The three `length`/`aria-label` warnings run in passive effects: **after paint**. The fallback label discovery runs after *every* commit (no dependency list), so a port needs a per-render DOM query or mutation-driven recomputation. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (C# state work → `OnAfterRender`; Server: after paint plus one round trip).
- **`requestSubmit()` inside a layout effect.** `autoSubmit` dispatches a native `submit` synchronously from the post-commit hook; Base UI's `<Form>` deliberately avoids `flushSync` in its submit path so this does not raise React's "flushSync was called from inside a lifecycle method" error (two tests assert the absence of that error). A port must ensure that synchronously submitting from its post-commit hook cannot re-enter rendering. Port: see decisions/01-hosting-model-and-timing.md §Decision items 2 and 4 (`requestSubmit()` is a JS primitive called from `OnAfterRender`).
- **Index guessing.** `useCompositeListItem({ guess: true })` hands each Input a render-order index during its first render so SSR markup and the first client render carry correct `value`/`tabindex`/`id` without a corrective re-render; the registry rebuild post-commit corrects out-of-order cases. Ports with deterministic child enumeration can compute indexes directly. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry move detection).
- **Composition buffer as state.** `composingValue` is component state, so each intermediate IME change re-renders the slot with the buffer as its `value`; a port can keep the buffer in the DOM and only suppress commits. No ADR covers composition events (Blazor has no built-in `compositionstart`/`compositionend` event arguments).
- **`event.nativeEvent`.** Callback `details.event` is always the native DOM event (EVD-API-05); keydown handlers read React's synthetic `event.key`, `ctrlKey`, `metaKey`, `altKey`, `currentTarget.selectionStart/End`. Port: see decisions/04-js-interop-boundary.md §3 (`details.event` is a snapshot record; `Kind` replaces `instanceof`).
- **StrictMode / remount guards.** `useSetFieldFocused` re-runs on mount and on `disabled` changes to reclaim or release focus that no event reported; `CompositeList` marks its map dirty on effect teardown so a replayed mount rebuilds indexes; `useRegisterFieldControl` re-registers (not delete + add) on every value change so the Form's field order is stable.
- **Ids.** With React ≥ 18 the control id is `base-ui-` + `useId()` when no `id` prop is given; under React 17 the id is `undefined` during SSR and the first client render, so slots have no `id` until a post-mount re-render (`OTPFieldRoot.react17.test.tsx`). Ports should generate stable SSR-safe ids eagerly. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (scoped `base-ui-<n>` generator; the React 17 fallback of RND-EDGE-09 does not arise).
- **Owner stack in warnings.** In React 19 development builds `SafeReact.captureOwnerStack()` appends a component stack to each warning as a second argument joined by a space; tests use substring matching. Ports may omit the stack.
- **Stable callbacks.** `focusInput`, `queueFocusInput`, `setValue`, `reportValueInvalid`, `handleInputFocus/Blur` are identity-stable wrappers reading the latest props; the Input receives them through ancestor-provided shared state. No port-observable behavior. Port: see decisions/05-state-store-and-event-details.md §1 (store cascaded fixed).
- **Prop merging.** Both parts pass `[builtInProps, consumerProps]` to the rendering pipeline: consumer attributes override (RND-DOM-19), consumer handlers run first (RND-EVT-02), and built-in handlers additionally check `event.defaultPrevented` so a plain `preventDefault()` in a consumer handler vetoes them (OTP-EVT-16). That veto relies on React's synthetic event setting `defaultPrevented` even for non-cancelable events (`focus`, `blur`, and the `input` event behind `onChange`); a native DOM `preventDefault()` on those events leaves `defaultPrevented` `false`, and Blazor handlers cannot call `preventDefault()` at all, so in the port only `PreventBaseUIHandler()` vetoes OTP-FOC-08/OTP-KEY-13, and the `mousedown`/`keydown`/`paste` vetoes of OTP-PTR-02/OTP-KEY-01/OTP-KEY-19 need a decision point. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 3 and decisions/04-js-interop-boundary.md §3.
- **Value ownership and details.** `value`/`defaultValue`/`onValueChange` follow the generic controlled-value pattern; `onValueInvalid`/`onValueComplete` carry generic details. Port: see decisions/05-state-store-and-event-details.md §2 and §3.
- **Interop split.** Value state, normalization (OTP-STA-04/07/08), Field registration (OTP-FRM-06/07), the hidden input and its attributes are C#. The slot handlers, however, need browser APIs at event time: conditional `preventDefault()`/`stopPropagation()` per key (OTP-KEY-02…10), on `paste` (OTP-KEY-18) and on `mousedown` (OTP-PTR-01); `clipboardData.getData('text/plain')` (OTP-KEY-18/20); `selectionStart`/`selectionEnd` reads (OTP-KEY-09); `focus()` + `select()` (OTP-FOC-03…06); the DOM `value` restore (OTP-KEY-17); composition events (OTP-KEY-21); `form.requestSubmit()` (OTP-EVT-13). Port: see decisions/01-hosting-model-and-timing.md §Decision item 2 and decisions/04-js-interop-boundary.md §1 (no listed module owns selection, clipboard or composition handling).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| OTP-STR-01 | MUST | Root element + sibling hidden input (only when `length` valid) | 2 |
| OTP-STR-02 | MUST | Input must be inside Root subtree; throws otherwise | 2 |
| OTP-STR-03 | MUST | slot index = document order among Inputs; correct on first render | 2 |
| OTP-STR-04 | MUST | Input count ≠ `length` tolerated, warned | 2 |
| OTP-STR-05 | MUST | Separator is the standalone Separator; structural only | 2 |
| OTP-API-01 | MUST | `length` required; uses listed | 3.1 |
| OTP-API-02 | MUST | `value` defined on first render → consumer-owned, normalized | 3.1 |
| OTP-API-03 | MUST | `defaultValue` (`''`) initial, normalized silently | 3.1 |
| OTP-API-04 | MUST | `onValueChange(value, details)` with four reasons | 3.1 |
| OTP-API-05 | MUST | `onValueInvalid(raw, generic details)` | 3.1 |
| OTP-API-06 | MUST | `onValueComplete(value, generic details)` | 3.1 |
| OTP-API-07 | MUST | `validationType` default `'numeric'` | 3.1 |
| OTP-API-08 | MUST | `normalizeValue` runs once per pass, re-filtered, clamped; no `sanitizeValue` | 3.1 |
| OTP-API-09 | MUST | effective `inputMode` = prop ?? class default | 3.1 |
| OTP-API-10 | MUST | `mask` → slot `type="password"` | 3.1 |
| OTP-API-11 | MUST | `autoComplete` default `'one-time-code'`; slot 0 + hidden; others `off` | 3.1 |
| OTP-API-12 | MUST | `autoSubmit` default `false` | 3.1 |
| OTP-API-13 | MUST | `form` on hidden input and slots, not root | 3.1 |
| OTP-API-14 | MUST | effective name = field name ?? prop; hidden input only | 3.1 |
| OTP-API-15 | MUST | effective disabled = field OR prop | 3.1 |
| OTP-API-16 | MUST | `readOnly` blocks edits, keeps navigation | 3.1 |
| OTP-API-17 | MUST | `required` on hidden input and slots | 3.1 |
| OTP-API-18 | MUST | control id resolution; applied to slot 0, not root | 3.1 |
| OTP-API-19 | MUST | Root `className`/`style`/`render` per RND | 3.1 |
| OTP-API-20 | MUST | other Root props forwarded; consumed list | 3.1 |
| OTP-API-21 | MUST | slot `aria-label` only on index ≥ 1 | 3.2 |
| OTP-API-22 | MUST | slot `aria-labelledby` overrides inherited | 3.2 |
| OTP-API-23 | MUST | other Input props forwarded/override; RND customization | 3.2 |
| OTP-API-24 | MUST | no `index` prop | 3.2 |
| OTP-API-25 | MUST | Separator API identical to standalone | 3.3 |
| OTP-STA-01 | MUST | ownership fixed on first render | 4 |
| OTP-STA-02 | MUST | component-owned commit stores normalized value | 4 |
| OTP-STA-03 | MUST | consumer-owned: rendered value follows prop; fallback on `undefined` | 4 |
| OTP-STA-04 | MUST | normalization procedure (strip, filter, custom, re-filter, clamp, rejected flag) | 4 |
| OTP-STA-05 | MUST | built-in classes, patterns, inputModes | 4 |
| OTP-STA-06 | MUST | rendered value always normalized with current props | 4 |
| OTP-STA-07 | MUST | replacement-from-slot algorithm | 4 |
| OTP-STA-08 | MUST | character-removal algorithm | 4 |
| OTP-STA-09 | MUST | `complete = value.length === length` | 4 |
| OTP-STA-10 | MUST | `filled = value !== ''` | 4 |
| OTP-STA-11 | MUST | Input state = Root state + slot `value`/`filled`/`index` | 3.2 |
| OTP-STA-12 | MUST | end target index = `min(value.length, last)` | 4 |
| OTP-STA-13 | MUST | last focused index initial value and updates | 4 |
| OTP-STA-14 | MUST | Root state has exactly eleven fields; own `disabled`/`filled`/`focused` | 3.1 |
| OTP-DOM-01 | MUST | root never carries the `id` prop | 5.1 |
| OTP-DOM-02 | MUST | `data-complete`; `value`/`length` not projected on root | 5.1 |
| OTP-DOM-03 | MUST | `data-disabled`/`data-readonly`/`data-required` on root | 5.1 |
| OTP-DOM-04 | MUST | `data-filled`/`data-focused` on root (also outside Field) | 5.1 |
| OTP-DOM-05 | MUST | `data-touched`/`data-dirty`/`data-valid`/`data-invalid` (Field only) | 5.1 |
| OTP-DOM-06 | MUST | consumer attributes override built-ins on root | 5.1 |
| OTP-DOM-07 | MUST | slot ids `controlId`, `controlId-2`, …; none without control id | 5.2 |
| OTP-DOM-08 | MUST | slot `value` = character or composition buffer | 5.2 |
| OTP-DOM-09 | MUST | slot `type` password/text; consumer wins | 5.2 |
| OTP-DOM-10 | MUST | slot `inputmode` | 5.2 |
| OTP-DOM-11 | MUST | slot `autocomplete` (slot 0 prop, others `off`) | 5.2 |
| OTP-DOM-12 | MUST | slot `autocorrect="off"`, `spellcheck="false"` | 5.2 |
| OTP-DOM-13 | MUST | slot `enterkeyhint` next/done | 5.2 |
| OTP-DOM-14 | MUST | `maxlength` on slot 0 only | 5.2 |
| OTP-DOM-15 | MUST | slot `disabled`/`readonly`/`required`/`form` | 5.2 |
| OTP-DOM-16 | MUST | slot single-character `pattern` | 5.2 |
| OTP-DOM-17 | MUST | slot data attributes (own `data-filled`) | 5.2 |
| OTP-DOM-18 | MUST | slot `data-length` (undocumented) | 5.2 |
| OTP-DOM-19 | MUST | consumer slot attributes/handlers override/veto | 5.2 |
| OTP-DOM-20 | MUST | hidden input `type=text`, `aria-hidden`, `tabindex=-1` | 5.3 |
| OTP-DOM-21 | MUST | hidden input id `…-hidden-input` only when nameless | 5.3 |
| OTP-DOM-22 | MUST | hidden input name/value/form/disabled/readonly/required | 5.3 |
| OTP-DOM-23 | MUST | hidden input `autocomplete`/`inputmode` | 5.3 |
| OTP-DOM-24 | MUST | hidden input whole-code `pattern`; none for `none`; SSR | 5.3 |
| OTP-DOM-25 | MUST | hidden input `minlength`/`maxlength` = `length` | 5.3 |
| OTP-DOM-26 | MUST | hidden input visually-hidden style variants | 5.3 |
| OTP-DOM-27 | MUST | Separator renders as standalone; no OTP attributes | 5.4 |
| OTP-ARIA-01 | MUST | root `role="group"` | 5.5 |
| OTP-ARIA-02 | MUST | explicit root `aria-labelledby`; slots do not inherit | 5.5 |
| OTP-ARIA-03 | MUST | Field label id on group | 5.5 |
| OTP-ARIA-04 | MUST | native-label fallback via slot 0; generated `…-label` id | 5.5 |
| OTP-ARIA-05 | MUST | group `aria-describedby` composition; not on slots | 5.5 |
| OTP-ARIA-06 | MUST | slot `aria-labelledby` resolution/inheritance | 5.5 |
| OTP-ARIA-07 | MUST | slot 0 never `aria-label`; others carry consumer value | 5.5 |
| OTP-ARIA-08 | MUST | `aria-labelledby` absent on slots with `aria-label` | 5.5 |
| OTP-ARIA-09 | MUST | slot `aria-invalid` from app-controlled invalid, not disabled | 5.5 |
| OTP-ARIA-10 | MUST | slots carry no Field `aria-describedby` | 5.5 |
| OTP-ARIA-11 | MUST | hidden input described-by + computed `aria-invalid` | 5.5 |
| OTP-KEY-01 | MUST | keydown unhandled when prevented/disabled/composing | 6.1 |
| OTP-KEY-02 | MUST | previous/next arrow step | 6.1 |
| OTP-KEY-03 | MUST | Ctrl/Meta + arrow → boundaries | 6.1 |
| OTP-KEY-04 | MUST | Home/ArrowUp → slot 0 | 6.1 |
| OTP-KEY-05 | MUST | End/ArrowDown → end target; stops event | 6.1 |
| OTP-KEY-06 | MUST | readOnly: navigation works, editing keys unhandled | 6.1 |
| OTP-KEY-07 | MUST | Ctrl/Meta+Backspace clears all; focus slot 0 | 6.1 |
| OTP-KEY-08 | MUST | Delete removes char at i; focus stays | 6.1 |
| OTP-KEY-09 | MUST | same character on selected slot → advance, no change | 6.1 |
| OTP-KEY-10 | MUST | Backspace semantics (filled / empty / first) | 6.1 |
| OTP-KEY-11 | MUST | keyboard-edit focus move only on commit | 6.1 |
| OTP-KEY-12 | MUST | Tab/Enter/Escape/clipboard shortcuts not intercepted | 6.1 |
| OTP-KEY-13 | MUST | change event ignored when prevented/disabled/readOnly | 6.1.1 |
| OTP-KEY-14 | MUST | typed text normalized; `onValueInvalid` first | 6.1.1 |
| OTP-KEY-15 | MUST | non-empty text → replacement at slot; focus advance | 6.1.1 |
| OTP-KEY-16 | MUST | slot emptied by input → `input-clear` | 6.1.1 |
| OTP-KEY-17 | MUST | fully rejected text: restore+select (filled) / empty stays | 6.1.1 |
| OTP-KEY-18 | MUST | paste processing (`input-paste`), preventDefault | 6.1.1 |
| OTP-KEY-19 | MUST | paste left to browser when prevented/disabled/readOnly | 6.1.1 |
| OTP-KEY-20 | MUST | clipboard read failure: warn, no preventDefault, no change | 6.1.1 |
| OTP-KEY-21 | MUST | IME buffering and single commit on compositionend | 6.1.1 |
| OTP-KEY-22 | MUST | Android: no buffering | 6.1.1 |
| OTP-KEY-23 | MUST | compositionend without buffer ignored | 6.1.1 |
| OTP-PTR-01 | MUST | mousedown: preventDefault, focus + select slot | 6.2 |
| OTP-PTR-02 | MUST | consumer-prevented mousedown: nothing, no focus | 6.2 |
| OTP-PTR-03 | MUST | disabled mousedown inert | 6.2 |
| OTP-PTR-04 | MUST | press beyond first empty → end target | 6.2 |
| OTP-FOC-01 | MUST | exactly one slot `tabindex=0` | 6.3 |
| OTP-FOC-02 | MUST | active slot index definition | 6.3 |
| OTP-FOC-03 | MUST | focus redirect when `i > value.length` | 6.3 |
| OTP-FOC-04 | MUST | focus: record index, select, set flag | 6.3 |
| OTP-FOC-05 | MUST | focus-slot clamps to rendered count; focus + select | 6.3 |
| OTP-FOC-06 | MUST | hidden input focus → slot 0 | 6.3 |
| OTP-FOC-07 | MUST | focused flag set on focus, cleared on blur leaving root | 6.3 |
| OTP-FOC-08 | MUST | consumer onFocus/onBlur preventDefault vetoes flag | 6.3 |
| OTP-FOC-09 | MUST | focus ownership on disable/unmount | 6.3 |
| OTP-FOC-10 | MUST | queued focus move only when applied value matches | 6.3 |
| OTP-FOC-11 | MUST | programmatic focus behaves like user focus | 6.3 |
| OTP-FOC-12 | MUST | readOnly does not affect focus | 6.3 |
| OTP-FOC-13 | MUST | autofill focuses last filled slot; cleared → none | 6.3 |
| OTP-EVT-01 | MUST | `onValueChange` sync, before state, native event, no trigger | 7 |
| OTP-EVT-02 | MUST | reason per action | 7 |
| OTP-EVT-03 | MUST | not fired when value unchanged | 7 |
| OTP-EVT-04 | MUST | `cancel()` semantics | 7 |
| OTP-EVT-05 | MUST | not fired for mount/prop changes/re-normalization | 7 |
| OTP-EVT-06 | MUST | `allowPropagation` no effect; live accessors | 7 |
| OTP-EVT-07 | MUST | `onValueInvalid` conditions, raw value, before change | 7 |
| OTP-EVT-08 | MUST | completion-making request definition | 7 |
| OTP-EVT-09 | MUST | same complete paste → immediate `onValueComplete` | 7 |
| OTP-EVT-10 | MUST | pending completion fires post-commit when value matches | 7 |
| OTP-EVT-11 | MUST | no completion when canceled / controlled-only changes | 7 |
| OTP-EVT-12 | MUST | typing into complete value → no completion | 7 |
| OTP-EVT-13 | MUST | `autoSubmit` form resolution and `requestSubmit()` | 7 |
| OTP-EVT-14 | MUST | form submit pipeline runs; blocked submission harmless | 7 |
| OTP-EVT-15 | MUST | callback/side-effect ordering | 7 |
| OTP-EVT-16 | MUST | consumer DOM handlers first; preventDefault/preventBaseUIHandler vetoes | 7 |
| OTP-FRM-01 | MUST | `name=value` entry from hidden input | 8 |
| OTP-FRM-02 | MUST | slots never named; no hidden input → nothing submitted | 8 |
| OTP-FRM-03 | MUST | `name` prop offered to Field registration | 8 |
| OTP-FRM-04 | MUST | native constraints on hidden input | 8 |
| OTP-FRM-05 | MUST | `form` prop ownership | 8 |
| OTP-FRM-06 | MUST | field registration (slot 0, id, value, name; disabled → withdrawn) | 8 |
| OTP-FRM-07 | MUST | value-change sequence post-commit, pre-paint | 8 |
| OTP-FRM-08 | MUST | `filled` published | 8 |
| OTP-FRM-09 | MUST | `touched` on blur leaving root | 8 |
| OTP-FRM-10 | MUST | onBlur-mode commit on leaving root | 8 |
| OTP-FRM-11 | MUST | `Field.Label` `for` ↔ slot 0 | 8 |
| OTP-FRM-12 | MUST | description ids on root + hidden input, not slots | 8 |
| OTP-FRM-13 | MUST | hidden-input autofill path; locked → ignored | 8 |
| OTP-FRM-14 | MUST | no reset listener | 8 |
| OTP-FRM-15 | MUST | Form error → invalid; cleared on value change | 8 |
| OTP-FRM-16 | MUST | disabled: no submission/validation/registration | 8 |
| OTP-I18N-01 | MUST | RTL swaps previous/next arrows only | 12 |
| OTP-EDGE-01 | MUST | missing-root error message | 12 |
| OTP-EDGE-02 | MUST | `length` mismatch warning (message, timing) | 12 |
| OTP-EDGE-03 | MUST | non-positive-integer `length` warning | 12 |
| OTP-EDGE-04 | MUST | first-slot `aria-label` warning | 12 |
| OTP-EDGE-05 | MUST | clipboard read failure warning | 12 |
| OTP-EDGE-06 | SHOULD | controlled/default diagnostics (`OTPField`, `value`) | 12 |
| OTP-EDGE-07 | MUST | invalid `length` behavior | 12 |
| OTP-EDGE-08 | MUST | disabled behavior summary | 12 |
| OTP-EDGE-09 | MUST | readOnly behavior summary | 12 |
| OTP-EDGE-10 | MUST | SSR markup identical to first client render | 12 |
| OTP-EDGE-11 | MUST | runtime slot add/remove re-indexes | 12 |
| OTP-EDGE-12 | MUST | code-point clamp vs code-unit indexing | 12 |
| OTP-EDGE-13 | MUST | described-by de-duplication | 12 |
| OTP-EDGE-14 | MUST | runtime `length` change re-normalizes, no callbacks | 12 |

## 16. Open questions

1. **`data-length` on slots** (OTP-DOM-18). `otp-field/utils/stateAttributesMapping.ts:12-16` suppresses `value` and `index` but not `length`, so every slot renders `data-length="6"`; neither the outline nor `OTPFieldInputDataAttributes.ts` lists it. Likely an oversight; the port should decide whether to reproduce it (documented as MUST because it is observable).
2. **Code units vs code points** (OTP-EDGE-12). `otp.ts:69-74` clamps by code point while `OTPFieldInput.tsx:79` (`value[index]`), `otp.ts:101-102,117` and `OTPFieldRoot.tsx:314` use code-unit indexing; with `validationType="none"` an astral character occupies two slots as lone surrogates and counts twice toward completion. Confirm whether the port should mirror this or treat it as a bug.
3. **Slot `aria-invalid` ignores computed validity** (OTP-ARIA-09). `OTPFieldInput.tsx:162` reads the field's app-controlled `invalid` flag (`FieldRoot.tsx:97`), so a failed `validate()` or native constraint marks only the `aria-hidden` hidden input (`OTPFieldRoot.tsx:406`) — assistive technology never hears computed invalidity. Documented as implemented; flagged by the FFI author as a second code path.
4. **Incomplete-code blocking with `validationType="none"`** (OTP-FRM-04). Without a `pattern`, an incomplete value only fails `tooShort`, which browsers apply solely to user-edited values; a script-set partial value with `required` passes native validation. Tests cover numeric only (`OTPFieldRoot.test.tsx:974-994`).
5. **Modifier + same character** (OTP-KEY-09). `OTPFieldInput.tsx:290` does not check modifiers, so e.g. <kbd>Ctrl</kbd>+<kbd>A</kbd> on a selected slot holding `a` (alpha mode) advances focus instead of the native select-all. Decide whether to mirror.
6. **Empty-slot rejected input relies on framework restoration** (OTP-KEY-17, §14). `OTPFieldInput.tsx:126-129` restores the DOM value only when the slot was filled; the empty case depends on React's controlled-input reset. Stated as a requirement; a port must implement it explicitly.
7. **Root `aria-label` does not suppress `aria-labelledby`** (OTP-ARIA-04). `OTPFieldRoot.tsx:125` omits the `ariaLabel` argument that Switch/Checkbox pass, so a group with `aria-label` and an associated native label gets both. Possibly intentional (slots, not the group, are the labelled controls); confirm.
8. **Insertion overflow is silent** (OTP-STA-07). Pasting `k` characters at slot `i` with `i + k > length` drops the overflow in `replaceOTPValue` (`otp.ts:104-109`) without `onValueInvalid`, whereas overflow of the pasted text itself (clamp in `normalizeOTPValueWithDetails`) is reported. Inconsistent; documented as implemented.
9. **Form reset** (OTP-FRM-14). The browser resets the hidden input's DOM value; re-synchronization to the component value depends on React's controlled-input handling (same question as switch.md §16 item 1). A port should explicitly re-sync after `reset`.
10. **Outline discrepancy.** The API outline lists only `className`, `render`, `style` for `OTPField.Input`, while `aria-label`, `aria-labelledby` and `type` receive special handling (OTP-API-21/22, OTP-DOM-09); and the outline's Input data attributes omit `data-length` (item 1).
11. **`length` mismatch and focus clamping** (OTP-STR-04). `focusInput` clamps to the rendered slot count (`OTPFieldRoot.tsx:166`) but the redirect and keyboard bounds use `length` (`:277`, `OTPFieldInput.tsx:227`); with fewer slots than `length` the two disagree only in which slot ends up focused at the tail. Not tested; documented as implemented.
