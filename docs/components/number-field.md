# Number Field

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/number-field/` |
| Tests | `packages/react/src/number-field/**/*.test.{ts,tsx}` (13 files; plus 2 type-only `*.spec.tsx`) |
| Docs page | `docs/src/app/(docs)/react/components/number-field/page.mdx` |
| Prefix | `NUM` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Number Field is a numeric text entry control with optional increment/decrement stepper buttons and an optional "scrub area" that changes the value by dragging the pointer. The visible control is a plain `<input type="text">` (role `textbox`, `aria-roledescription="Number field"`), not a native `<input type="number">` and not a `spinbutton`: the component owns parsing of locale-formatted text (group and decimal separators, currency and unit symbols, percent and permille signs, Arabic-Indic, Persian, fullwidth and Han numerals, Unicode plus/minus variants) and formatting of the numeric value through `Intl.NumberFormat`. Typing fires live value changes for every parseable intermediate string and leaves the typed text alone; the text is re-formatted to the canonical representation only when the input is blurred or when a non-typing interaction (step, wheel, scrub, external value change) occurs.

The numeric value is `number | null` (`null` = empty). It may be owned by the consumer (`value`) or by the component (`defaultValue`). Every value change passes through one validation pipeline — optional snapping to the step grid, clamping to `min`/`max`, and floating-point cleanup or `Intl` rounding — and is reported through `onValueChange` (cancelable, with a `reason` and, for step-based changes, a `direction`) and, once an interaction ends, through `onValueCommitted`. Keyboard arrows (with Shift = `largeStep`, Alt = `smallStep`), Home/End, press-and-hold stepper buttons (400 ms then every 60 ms), mouse wheel over the focused input (opt-in) and pointer-lock scrubbing with a virtual cursor are the non-typing interactions.

A visually hidden native `<input type="number">` rendered as a sibling of the root carries `name`, `form`, `min`, `max`, `step`, `required` and the raw numeric value, so form submission and native constraint validation (`stepMismatch`, `rangeOverflow`, `valueMissing`) behave as they would for a native number input. Inside `Field.Root` the visible input registers as the field control and the component publishes `touched`, `dirty`, `filled`, `focused` and validity.

No component in the library builds on Number Field, and Number Field builds on no other component.

## 2. Anatomy

```
<NumberField.Root>                 required, 1          default element: div   (+ hidden <input type="number"> next sibling)
  <NumberField.ScrubArea>          optional, 0..n       default element: span
    <NumberField.ScrubAreaCursor>  optional, 0..n       default element: span  (rendered into <body>, only while scrubbing)
  <NumberField.Group>              optional, 0..n       default element: div   (role="group")
    <NumberField.Decrement>        optional, 0..n       default element: button
    <NumberField.Input>            optional, 0..1       default element: input (type="text")
    <NumberField.Increment>        optional, 0..n       default element: button
```

Nesting inside `Group` is a convention of the docs anatomy, not a requirement: every part other than `ScrubAreaCursor` only needs to be *somewhere* in the `Root` subtree. `ScrubAreaCursor` must be inside a `ScrubArea`.

- **NUM-STR-01** — MUST. `NumberField.Root` MUST render, in document order, the root element (default `<div>`) followed by a visually hidden `<input type="number">` as its next sibling (never a descendant of the root element). Both are always rendered.
  Source: `number-field/root/NumberFieldRoot.tsx:490-541`. Test: [L2] `NumberFieldRoot.test.tsx` "should set the name attribute on the hidden input", "forwards focus to the visible input".
- **NUM-STR-02** — MUST. `Group`, `Input`, `Increment`, `Decrement`, `ScrubArea` and `ScrubAreaCursor` MUST be rendered inside the subtree of a `NumberField.Root`; rendering any of them elsewhere MUST throw the error in NUM-EDGE-01.
  Source: `number-field/root/NumberFieldRootContext.ts:42-51`. Test: [L2] `NumberFieldInput.test.tsx` "throws a descriptive error when rendered outside <NumberField.Root>".
- **NUM-STR-03** — MUST. `ScrubAreaCursor` MUST be rendered inside the subtree of a `NumberField.ScrubArea`; otherwise it MUST throw the error in NUM-EDGE-02.
  Source: `number-field/scrub-area/NumberFieldScrubAreaContext.ts:15-23`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:36-37`. Test: [L2] `NumberFieldScrubAreaCursor.test.tsx` "throws a descriptive error when rendered outside <NumberField.ScrubArea>".
- **NUM-STR-04** — MUST. `NumberField.Group` is structural only: it MUST render an element with `role="group"`, the root state attributes and forwarded attributes, and MUST NOT attach handlers or an `id` of its own.
  Source: `number-field/group/NumberFieldGroup.tsx:17-30`. Test: [L2] `NumberFieldGroup.test.tsx` "has role prop".
- **NUM-STR-05** — MUST. Several `ScrubArea` parts MAY exist under one root; each owns its own scrubbing state and cursor, so while one of them is being scrubbed only *its* `ScrubAreaCursor` is rendered.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:73-75,366-374`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:41-42`. Test: [L2] `NumberFieldScrubAreaCursor.test.tsx` "only renders a cursor for the active scrub area". Docs: CHANGELOG.md v1.0.0 "Fix multiple scrub area support".
- **NUM-STR-06** — MUST. `ScrubAreaCursor` MUST render nothing at all (no element, no DOM handle) unless its scrub area is actively scrubbing with a non-touch pointer, the engine is not WebKit, and the pointer-lock request was not denied; when it renders, the element MUST be appended to the `<body>` of the owning document rather than placed in the component's own subtree.
  Source: `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:41-58`. Test: [L4] `NumberFieldScrubAreaCursor.test.tsx` "renders when using mouse input", "does not render when using touch input", "handles pointer lock denial through requestPointerLock API", "does not render after a quick tap when pointer lock resolves later".

If `Input` is omitted the hidden number input still submits the value and the stepper buttons still work (they fall back to the numeric state), but nothing is focusable for the user and keyboard interaction is unavailable. Rendering more than one `Input` is unsupported: all of them write the same DOM handle, so only the last-mounted one receives programmatic focus and wheel listening (see §16).

## 3. Public API

### 3.1 `NumberField.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `number \| null` | — (uncontrolled) | The raw numeric value. Supplying it (non-`undefined`) on the first render makes the value consumer-owned. | NUM-API-01 |
| `defaultValue` | `number` | `null` | Initial value when the value is component-owned. (`undefined` behaves as `null`.) | NUM-API-02 |
| `onValueChange` | `(value: number \| null, eventDetails: NumberField.Root.ChangeEventDetails) => void` | — | Change request; fires before the value is stored; cancelable. | NUM-API-03 |
| `onValueCommitted` | `(value: number \| null, eventDetails: NumberField.Root.CommitEventDetails) => void` | — | Fired when an interaction ends (blur, pointer release) or synchronously with keyboard/wheel steps. Generic (non-cancelable) details. | NUM-API-04 |
| `min` | `number` | — | Lower bound. Internally `Number.MIN_SAFE_INTEGER` when absent. | NUM-API-05 |
| `max` | `number` | — | Upper bound. Internally `Number.MAX_SAFE_INTEGER` when absent. | NUM-API-05 |
| `step` | `number \| 'any'` | `1` | Base step for keyboard, buttons, wheel and scrub. `'any'` disables native step validation and steps by `1`. | NUM-API-06 |
| `smallStep` | `number` | `0.1` | Step used while <kbd>Alt</kbd> is held. | NUM-API-07 |
| `largeStep` | `number` | `10` | Step used while <kbd>Shift</kbd> is held. | NUM-API-07 |
| `snapOnStep` | `boolean` | `false` | Snap step results to the step grid. | NUM-API-08 |
| `allowOutOfRange` | `boolean` | `false` | Direct text entry may leave the `min`/`max` range (so native range validation can fail); step-based changes still clamp. | NUM-API-09 |
| `allowWheelScrub` | `boolean` | `false` | Mouse wheel over the focused input steps the value. | NUM-API-10 |
| `format` | `Intl.NumberFormatOptions` | — | Formatting options for the displayed text (and parsing). | NUM-API-11 |
| `locale` | `Intl.LocalesArgument` | — (runtime locale) | Locale for formatting and parsing. | NUM-API-12 |
| `disabled` | `boolean` | `false` | Ignore user interaction. OR-ed with the enclosing `Field.Root`'s `disabled`. | NUM-API-13 |
| `readOnly` | `boolean` | `false` | The user cannot change the value; input stays focusable. | NUM-API-14 |
| `required` | `boolean` | `false` | A value must be present to pass native validation. | NUM-API-15 |
| `name` | `string` | — | Form field name (hidden input). Overridden by the enclosing `Field.Root`'s `name`. | NUM-API-16 |
| `form` | `string` | — | `id` of the owning `<form>` (hidden input). | NUM-API-17 |
| `id` | `string` | generated | `id` of the *visible* input. | NUM-API-18 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the *hidden* `<input type="number">`. | NUM-API-19 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | NUM-API-20 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element. `onChange` is excluded from the accepted type. | NUM-API-21 |

**Consumed (not forwarded to the element):** `id`, `min`, `max`, `smallStep`, `step`, `largeStep`, `required`, `disabled`, `readOnly`, `form`, `name`, `defaultValue`, `value`, `onValueChange`, `onValueCommitted`, `allowWheelScrub`, `snapOnStep`, `allowOutOfRange`, `format`, `locale`, `render`, `className`, `inputRef`, `style`.

- **NUM-API-01** — MUST. When `value` is not `undefined` on the first render, the numeric value MUST equal `value` on every render and MUST NOT be changed internally by user interaction (consumer-owned mode, UTL-STA-01/04). `null` is a valid consumer-owned value meaning "empty".
  Source: `number-field/root/NumberFieldRoot.tsx:116-121`, `packages/utils/src/useControlled.ts:41-46,83-90`. Test: [L2] `NumberFieldRoot.test.tsx` "should accept a number value that can change over time", "should accept a `null` value".
- **NUM-API-02** — MUST. When `value` is `undefined` on the first render, the initial numeric value MUST be `defaultValue`, which defaults to `null` (an explicitly passed `undefined` also yields `null`).
  Source: `number-field/root/NumberFieldRoot.tsx:73,116-121`. Test: [L2] `NumberFieldRoot.test.tsx` "should accept a number value" (defaultValue), "should accept an `undefined` value".
- **NUM-API-03** — MUST. `onValueChange` MUST receive the validated next value (`number | null`) as its first argument and a change-event-details object (§3.8) as its second. Firing rules are in §7.
  Source: `number-field/root/NumberFieldRoot.tsx:251-257,649-650`. Test: [L2] `NumberFieldRoot.test.tsx` "should be called when the value changes".
- **NUM-API-04** — MUST. `onValueCommitted` MUST receive the stored (validated) value as its first argument and a generic-event-details object (§3.8; no `cancel`) as its second. Firing rules are in §7.
  Source: `number-field/root/NumberFieldRoot.tsx:135-140,662-663`. Test: [L2] `NumberFieldRoot.test.tsx` "fires on blur with committed numeric value".
- **NUM-API-05** — MUST. `min` and `max` default to `undefined`. For clamping, snapping and boundary detection an absent `min` MUST behave as `Number.MIN_SAFE_INTEGER` and an absent `max` as `Number.MAX_SAFE_INTEGER`; for the snapping base an absent `min` MUST behave as `0`; for <kbd>Home</kbd>/<kbd>End</kbd> and the hidden input's attributes the *raw* props are used (absent = no attribute, key not handled).
  Source: `number-field/root/NumberFieldRoot.tsx:106-108,528-529`, `number-field/input/NumberFieldInput.tsx:354-359`. Test: [L2] `NumberFieldRoot.test.tsx` "prevents the raw value from going below the `min` prop", "prevents the value from going above the `max` prop", "does not prevent native caret movement for Home/End without min/max".
- **NUM-API-06** — MUST. `step` defaults to `1`. The value `'any'` MUST be forwarded verbatim as the hidden input's `step` attribute (disabling native step validation) while interactive stepping MUST use `1` as the base amount; a numeric `step` is used both as the attribute and as the base amount.
  Source: `number-field/root/NumberFieldRoot.tsx:66,102,532`. Test: [L2] `NumberFieldRoot.test.tsx` "defaults to 1", "does not block submission when step=\"any\"", "blocks submission when step mismatch occurs" (L4 for the native step-validation clause).
- **NUM-API-07** — MUST. `smallStep` defaults to `0.1` and `largeStep` to `10`. They replace the base step amount while <kbd>Alt</kbd> respectively <kbd>Shift</kbd> is held during a step interaction (NUM-STA-16).
  Source: `number-field/root/NumberFieldRoot.tsx:65,67,209-217`. Test: [L2] `NumberFieldRoot.test.tsx` "should increment the value by the default `largeStep` prop of 10 while holding the shift key", "should increment the value by the default `smallStep` prop of 0.1 while holding the alt key".
- **NUM-API-08** — MUST. `snapOnStep` defaults to `false`. When `true`, the result of every step-based change MUST be snapped to the step grid per NUM-STA-08; direct text entry is never snapped.
  Source: `number-field/root/NumberFieldRoot.tsx:78,232-242`, `number-field/utils/validate.ts:109-116`. Test: [L2] `NumberFieldRoot.test.tsx` "should snap when incrementing to the nearest multiple of the `step` prop"; `NumberFieldInput.test.tsx` "should not snap number to step on blur".
- **NUM-API-09** — MUST. `allowOutOfRange` defaults to `false`. When `true`, values produced by direct text entry (reasons `input-change`, `input-clear`, `input-blur`, `input-paste`, `none`) MUST NOT be clamped to `min`/`max`, while step-based changes MUST still be clamped; additionally the minus sign becomes typeable regardless of `min` (NUM-KEY-07).
  Source: `number-field/root/NumberFieldRoot.tsx:79,202-204,227-230`. Test: [L2] `NumberFieldRoot.test.tsx` "allows range overflow validation when true", "still clamps step interactions when true", "clamps to range when false", "allows typing a negative value via keyboard when min is 0".
- **NUM-API-10** — MUST. `allowWheelScrub` defaults to `false`; wheel stepping (NUM-PTR-07) MUST be inactive unless it is `true`.
  Source: `number-field/root/NumberFieldRoot.tsx:77,363-367`. Test: [L4] `NumberFieldRoot.test.tsx` "should not allow the user to scrub the input value with the mouse wheel if `allowWheelScrub` is `false`".
- **NUM-API-11** — MUST. `format` (default `undefined`) MUST be passed verbatim to `Intl.NumberFormat` for formatting (NUM-I18N-01) and used to derive the parse rules (NUM-I18N-02), the allowed-character set (NUM-KEY-07) and the rounding applied to stepped/committed values (NUM-STA-10..12). Changing `format` at runtime MUST re-format the displayed text even when the numeric value is unchanged.
  Source: `number-field/root/NumberFieldRoot.tsx:80,131,312-324`. Test: [L2] `NumberFieldRoot.test.tsx` "should format the value using the provided options", "reformats the visible text when the format prop changes at the same value".
- **NUM-API-12** — MUST. `locale` (default `undefined`, meaning the runtime's default locale) MUST be used for formatting and parsing; when `undefined`, parsing MAY infer `'ar'` or `'zh'` from the typed numerals (NUM-I18N-03).
  Source: `number-field/root/NumberFieldRoot.tsx:81,149,271,319`, `number-field/utils/parse.ts:130-138`. Test: [L2] `NumberFieldRoot.test.tsx` "should set the locale of the input", "should use the default locale if no locale is provided".
- **NUM-API-13** — MUST. The effective disabled state MUST be `fieldDisabled || disabled` (the enclosing `Field.Root`'s disabled OR the prop, default `false`).
  Source: `number-field/root/NumberFieldRoot.tsx:69,92,100`. Test: [L2] `NumberFieldRoot.test.tsx` "should disable the input", "disables the input when disabled=true" (Field).
- **NUM-API-14** — MUST. `readOnly` defaults to `false`. When `true`, no interaction (typing handlers, keyboard steps, buttons, wheel, scrub, paste, autofill) MAY change the value, while the input remains focusable and the hidden input carries `readonly`.
  Source: `number-field/root/NumberFieldRoot.tsx:70,366,507,534`, `number-field/input/NumberFieldInput.tsx:147-149,273,412`. Test: [L2] `NumberFieldRoot.test.tsx` "should mark the input as readOnly", "marks the hidden input readOnly when the field is readOnly", "does not scrub on wheel when readOnly"; `useNumberFieldStepperButton.test.tsx` "does not step when readOnly" (L4 for the keyboard, wheel, scrub and paste clauses).
- **NUM-API-15** — MUST. `required` defaults to `false`; when `true` both the visible and the hidden input MUST carry the `required` attribute.
  Source: `number-field/root/NumberFieldRoot.tsx:68,535`, `number-field/input/NumberFieldInput.tsx:116`. Test: [L2] `NumberFieldRoot.test.tsx` "should mark the input as required", "triggers native HTML validation on submit" (L4 for the native-validation citation).
- **NUM-API-16** — MUST. The effective form name MUST be the enclosing `Field.Root`'s `name` when defined, otherwise the `name` prop; it is placed on the hidden input only.
  Source: `number-field/root/NumberFieldRoot.tsx:72,94,101,526`. Test: [L2] `NumberFieldRoot.test.tsx` "should set the name attribute on the hidden input", "should include the input value in the form submission" (Field name) (L4 for the form-submission citation).
- **NUM-API-17** — MUST. `form` MUST be forwarded as the hidden input's `form` attribute and MUST NOT appear on the root element.
  Source: `number-field/root/NumberFieldRoot.tsx:71,525`. Test: [L2] `NumberFieldRoot.test.tsx` "submits to an external form when `form` is provided" (L4 for the external-form submission citation).
- **NUM-API-18** — MUST. `id` MUST be claimed as the labelable control id (FFI-ALG-07) and rendered on the *visible* input; when absent a generated id (`base-ui-…`, RND-DOM-27) is used. The hidden input never carries this id.
  Source: `number-field/root/NumberFieldRoot.tsx:62,114`, `number-field/input/NumberFieldInput.tsx:115`, `internals/labelable-provider/useLabelableId.ts:10-83`. Test: [L2] `NumberFieldRoot.test.tsx` "Field.Label"; `useNumberFieldStepperButton.test.tsx` "exposes aria-controls on the stepper".
- **NUM-API-19** — MUST. `inputRef` MUST be attached to the hidden `<input type="number">` (merged with the field's validation input handle), not to the visible input.
  Source: `number-field/root/NumberFieldRoot.tsx:84,112,523`. Test: none.
- **NUM-API-20** — MUST. `className`, `style` and `render` MUST behave per RND using the state object below; `onChange` MUST be excluded from the accepted prop type.
  Source: `number-field/root/NumberFieldRoot.tsx:490-495,545-548`. Test: [L2] `NumberFieldRoot.test.tsx` conformance suite "Base UI component API".
- **NUM-API-21** — MUST. Every prop not in the consumed list MUST be forwarded to the root element (merged after the state attributes, so a consumer attribute wins).
  Source: `number-field/root/NumberFieldRoot.tsx:86,493`. Test: [L2] `NumberFieldRoot.test.tsx` conformance suite (prop forwarding).

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes. It is the enclosing field's state (all `false`/`null` outside a Field) with six number-field fields layered on top.

| Field | Type | Meaning |
|---|---|---|
| `value` | `number \| null` | Current numeric value. |
| `inputValue` | `string` | Text currently displayed in the input. |
| `required` | `boolean` | The `required` prop. |
| `disabled` | `boolean` | Effective disabled (NUM-API-13). |
| `readOnly` | `boolean` | The `readOnly` prop. |
| `scrubbing` | `boolean` | A `ScrubArea` under this root is being scrubbed. |
| `touched` | `boolean` | Field: input has been blurred at least once. |
| `dirty` | `boolean` | Field: value differs from the initial value captured at registration. |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated. |
| `filled` | `boolean` | Field: `value !== null`. |
| `focused` | `boolean` | Field: the visible input owns focus (never `true` while disabled). |

- **NUM-STA-01** — MUST. The root state object MUST contain exactly the eleven fields above; `value` and `inputValue` MUST NOT produce data attributes.
  Source: `number-field/root/NumberFieldRoot.tsx:426-437,675-700`, `number-field/utils/stateAttributesMapping.ts:5-9`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "passes disabled state to className when root is disabled".
- **NUM-STA-02** — MUST. `Group`, `Input`, `ScrubArea` and `ScrubAreaCursor` MUST expose exactly the root's state object; `Increment` and `Decrement` MUST expose the root's state object with `disabled` replaced by the button's effective disabled value (NUM-STA-20).
  Source: `number-field/group/NumberFieldGroup.tsx:20-26`, `number-field/input/NumberFieldInput.tsx:70,460`, `number-field/scrub-area/NumberFieldScrubArea.tsx:48,361`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:35,47`, `number-field/root/useNumberFieldStepperButton.ts:182-186`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "passes disabled state to className when button is disabled".

#### Data attributes

All seven parts emit the same set (every part uses the root state and the same mapping).

| Attribute | Present when / value | Req |
|---|---|---|
| `data-scrubbing` | `""` while a scrub drag is in progress | NUM-DOM-02 |
| `data-disabled` | `""` while effective disabled (stepper buttons: their own effective disabled) | NUM-DOM-03 |
| `data-readonly` | `""` while `readOnly` | NUM-DOM-03 |
| `data-required` | `""` while `required` | NUM-DOM-03 |
| `data-touched` | `""` while field `touched` (Field only) | NUM-DOM-04 |
| `data-dirty` | `""` while field `dirty` (Field only) | NUM-DOM-04 |
| `data-filled` | `""` while `value !== null` (Field only) | NUM-DOM-04 |
| `data-focused` | `""` while field `focused` (Field only) | NUM-DOM-04 |
| `data-valid` | `""` while field `valid === true` (Field only) | NUM-DOM-05 |
| `data-invalid` | `""` while field `valid === false` (Field only) | NUM-DOM-05 |

#### CSS variables

None.

### 3.2 `NumberField.ScrubArea`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `direction` | `'horizontal' \| 'vertical'` | `'horizontal'` | Axis of pointer movement that changes the value. | NUM-API-24 |
| `pixelSensitivity` | `number` | `2` | Accumulated pixels of movement required before the value changes. | NUM-API-24 |
| `teleportDistance` | `number` | — | Distance (px) the virtual cursor may travel from the scrub area's edges before wrapping; absent = wrap at the visual viewport. | NUM-API-24 |
| `className`, `style`, `render` | see RND | — | Functions receive the root state. | NUM-API-20 |
| *other `<span>` attributes and handlers* | — | — | Forwarded (merged after the built-in `role`/`style`/handlers). | NUM-API-25 |

**Consumed:** `render`, `className`, `direction`, `pixelSensitivity`, `teleportDistance`, `style`.

- **NUM-API-24** — MUST. `direction` defaults to `'horizontal'`, `pixelSensitivity` to `2`, `teleportDistance` to `undefined`; their semantics are NUM-PTR-11 and NUM-PTR-16.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:40-42`. Test: [L2] `NumberFieldScrubArea.test.tsx` "prop: pixelSensitivity", "prop: teleportDistance", "prop: direction".
- **NUM-API-25** — MUST. On `ScrubArea`, `ScrubAreaCursor`, `Group`, `Input`, `Increment` and `Decrement`, every prop other than the part's consumed props MUST be forwarded to the rendered element, merged after the part's built-in attributes so that a consumer attribute wins and consumer handlers run before the built-in handler (RND-EVT-02).
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:359-364`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:44-56`, `number-field/group/NumberFieldGroup.tsx:20-26`, `number-field/input/NumberFieldInput.tsx:458-463`, `number-field/root/useNumberFieldStepperButton.ts:184-189`. Test: [L2] conformance suites of each part (prop forwarding); `NumberFieldScrubArea.test.tsx` "should fire onClick on child elements".

#### State object / Data attributes / CSS variables

Identical to the root's (NUM-STA-02); no CSS variables.

### 3.3 `NumberField.ScrubAreaCursor`

**Default element:** `<span>`, portaled into `<body>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive the root state. | NUM-API-20 |
| *other `<span>` attributes and handlers* | — | — | Forwarded (after built-in `role`/`style`). | NUM-API-25 |

**Consumed:** `render`, `className`, `style`.

#### State object / Data attributes / CSS variables

Identical to the root's; no CSS variables. Its position is driven imperatively through an inline `transform` (NUM-DOM-13), not through a CSS variable.

### 3.4 `NumberField.Group`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive the root state. | NUM-API-20 |
| *other `<div>` attributes and handlers* | — | — | Forwarded (after built-in `role="group"`). | NUM-API-25 |

**Consumed:** `render`, `className`, `style`.

#### State object / Data attributes / CSS variables

Identical to the root's; no CSS variables.

### 3.5 `NumberField.Decrement`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes (declare `nativeButton={false}` when the substitute is not a `<button>`). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables this button on its own; OR-ed with the root's effective disabled and the boundary check. | NUM-API-23 |
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>` (UBT-API-02). Not forwarded. | NUM-API-23 |
| `className`, `style`, `render` | see RND | — | Functions receive the button state (root state with effective `disabled`). | NUM-API-20 |
| *other `<button>` attributes and handlers* | — | — | Forwarded (after built-in attributes; `aria-label`, `tabIndex`, `style` etc. can be overridden). | NUM-API-25 |

**Consumed:** `render`, `className`, `disabled`, `nativeButton`, `style`.

- **NUM-API-23** — MUST. On `Increment` and `Decrement`, `disabled` defaults to `false` and `nativeButton` to `true`; the button's effective disabled is `disabled || rootDisabled || atBoundary` (NUM-STA-20), and the button mechanism is configured with `disabled: effectiveDisabled || readOnly`, `native: nativeButton`, `focusableWhenDisabled: true`.
  Source: `number-field/root/useNumberFieldStepperButton.ts:34-41,62-64,173-180`, `internals/use-button/useButton.ts:17-32`; see [button-behavior](../architecture/button-behavior.md). Test: [L2] `useNumberFieldStepperButton.test.tsx` "does not step when the button is disabled", "does not render aria-readonly on stepper buttons" (`NumberFieldRoot.test.tsx`).

#### State object

The root's state object with `disabled` = the button's effective disabled (NUM-STA-02, NUM-STA-20).

#### Data attributes / CSS variables

Same set as the root (table in §3.1), with `data-disabled` reflecting the button's effective disabled (NUM-DOM-17). No CSS variables.

### 3.6 `NumberField.Input`

**Default element:** `<input type="text">`. **Accepts `render` element substitution:** yes (the substitute callback receives the full native input attribute set, including `disabled` and `readOnly`). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `aria-roledescription` | `string` | `'Number field'` | Role description announced by assistive technology. A consumer value replaces the default. | NUM-API-22 |
| `className`, `style`, `render` | see RND | — | Functions receive the root state. | NUM-API-20 |
| *other `<input>` attributes and handlers* | — | — | Forwarded; consumer attributes override the built-in ones (`id`, `type`, `inputMode`, `autoComplete`, …); consumer handlers (`onFocus`, `onBlur`, `onChange`, `onKeyDown`, `onPaste`) run first and may veto the built-in handler by calling `preventDefault()` (NUM-EVT-14) or `preventBaseUIHandler()`. | NUM-API-25 |

**Consumed:** `render`, `className`, `style`. The outline lists only `aria-roledescription`, `className`, `render`, `style`; the component additionally intercepts `id`, `required`, `disabled`, `readOnly`, `inputMode`, `value`, `type`, `autoComplete`, `autoCorrect`, `spellCheck`, `aria-invalid`, `aria-labelledby`, `aria-describedby` and the five handlers above by *emitting* them (NUM-DOM-08); `name` is never emitted on this element.

- **NUM-API-22** — MUST. `aria-roledescription` MUST default to the string `Number field` and a consumer-supplied value MUST replace it.
  Source: `number-field/input/NumberFieldInput.tsx:125,461,475-480`. Test: none (default asserted indirectly by `screen.getByRole('textbox')` queries).

#### State object / Data attributes / CSS variables

Identical to the root's (NUM-STA-02); no CSS variables.

### 3.7 `NumberField.Increment`

Identical to `NumberField.Decrement` (§3.5) in every respect except the step direction (`+1`), the boundary that disables it (`max`), the default `aria-label` (`Increase`) and the reason it emits (`increment-press`). Source: `number-field/increment/NumberFieldIncrement.tsx:13-18`, `number-field/decrement/NumberFieldDecrement.tsx:13-18`, `number-field/root/useNumberFieldStepperButton.ts:29-33,62-68,122`.

### 3.8 Shared types

- `NumberField.Root.ChangeEventReason` = `'input-change' | 'input-clear' | 'input-blur' | 'input-paste' | 'keyboard' | 'increment-press' | 'decrement-press' | 'wheel' | 'scrub' | 'none'`. Source: `number-field/root/NumberFieldRoot.tsx:702-712`, `internals/reason-parts.ts:1,14-20,27,31-32`.
- `NumberField.Root.ChangeEventDetails` = the change-event-details shape of [EVD](../architecture/event-details-and-reasons.md) §3.1 (`reason`, `event`, `trigger`, `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed`) extended with `direction?: -1 | 1` (EVD §3.7, NUM-EVT-03). `event` is the native event keyed by reason: `WheelEvent` for `wheel`, `PointerEvent` for `scrub`, `KeyboardEvent` for `keyboard`, `PointerEvent | MouseEvent | TouchEvent` for the press reasons, `ClipboardEvent` for `input-paste`, `FocusEvent` for `input-blur`, `InputEvent | FocusEvent | Event` for `input-clear`, `InputEvent | Event` for `input-change`, `Event` for `none`. Source: `number-field/root/NumberFieldRoot.tsx:713-716`, `number-field/utils/types.ts:12-14`, `internals/createBaseUIEventDetails.ts:52-85,126-157`. Test: [L1] `NumberFieldRoot.spec.tsx` (type-level narrowing).
- `NumberField.Root.CommitEventReason` = `'input-blur' | 'input-clear' | 'keyboard' | 'increment-press' | 'decrement-press' | 'wheel' | 'scrub' | 'none'`; `'none'` is declared for consistency but never emitted (§7). Source: `number-field/root/NumberFieldRoot.tsx:718-728`.
- `NumberField.Root.CommitEventDetails` = generic-event details (EVD §3.2): `{ reason, event }` only — no `cancel`, no `trigger`. Source: `number-field/root/NumberFieldRoot.tsx:729-730`, `internals/createBaseUIEventDetails.ts:159-167`.
- `NumberField.Root.State`, and `NumberField.{Group,Input,Increment,Decrement,ScrubArea,ScrubAreaCursor}.State` — all structurally equal to the root state object (§3.1); the part interfaces extend `NumberFieldRootState` with no added fields. Source: `number-field/group/NumberFieldGroup.tsx:32`, `number-field/input/NumberFieldInput.tsx:468`, `number-field/increment/NumberFieldIncrement.tsx:20`, `number-field/decrement/NumberFieldDecrement.tsx:20`, `number-field/scrub-area/NumberFieldScrubArea.tsx:383`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:61`.
- `NumberField.<Part>.Props` — the prop tables above.
- Exported constant namespaces `NumberFieldRootDataAttributes`, `NumberFieldGroupDataAttributes`, `NumberFieldIncrementDataAttributes`, `NumberFieldDecrementDataAttributes`, `NumberFieldInputDataAttributes`, `NumberFieldScrubAreaDataAttributes`, `NumberFieldScrubAreaCursorDataAttributes` — seven identical lists of the ten attribute names in §3.1. Source: `number-field/index.ts:11-17`, `number-field/*/NumberField*DataAttributes.ts:1-40`.
- Internal constants exported for tests: `CHANGE_VALUE_TICK_DELAY = 60`, `START_AUTO_CHANGE_DELAY = 400` (mirroring the press-and-hold defaults UBT-PTR-06). Source: `number-field/utils/constants.ts:1-2`.

## 4. State model

The component owns two pieces of state: the **numeric value** (`number | null`) and the **display text** (`inputValue`, the string shown in the visible input). Everything else in the state object is a prop echo, the field's state, or the `scrubbing` flag published by a `ScrubArea`.

**Ownership.** `value`/`defaultValue` is the controlled/uncontrolled pair; ownership is decided once, on the first render (NUM-STA-03). The display text is always component-owned.

**Display-text sync.** A boolean *sync-allowed* flag (initially `true`) decides whether the display text is re-derived from the numeric value. Typing and pasting clear the flag ("the text is dirty: the user's keystrokes must not be overwritten while they type"); blur, keyboard steps, button presses, wheel and scrub set it back to `true` before applying their change. While the flag is `true`, the display text equals `formatNumber(value, locale, format)` after every render; while it is `false`, external changes to `value` are not reflected in the text until the next sync point (NUM-STA-06). The flag is also what lets a *step* decide whether to start from the numeric value or from the dirty text (NUM-KEY-12, NUM-PTR-05).

**Derived values.**

- effective disabled = `fieldDisabled || disabled`; effective name = `fieldName ?? name`
- `minWithDefault = min ?? MIN_SAFE_INTEGER`, `maxWithDefault = max ?? MAX_SAFE_INTEGER`, `minWithZeroDefault = min ?? 0`
- base step = `step === 'any' ? 1 : step`
- `filled` (field) = `value !== null`; `dirty` (field) = `validatedValue !== initialValue`
- `inputMode` (visible input) = `'numeric'`, or on iOS `'decimal'` / `'text'` (NUM-STA-19)
- stepper button effective disabled = `disabledProp || rootDisabled || atBoundary` (NUM-STA-20)

There is no mount/unmount lifecycle for the value parts; only `ScrubAreaCursor` is conditionally rendered (NUM-STR-06).

- **NUM-STA-03** — MUST. Ownership of the numeric value MUST be fixed on the first render: consumer-owned if `value !== undefined`, otherwise component-owned (UTL-STA-01). Later switching between `undefined` and a value MUST NOT change the mode (dev error NUM-EDGE-04).
  Source: `number-field/root/NumberFieldRoot.tsx:116-121`, `packages/utils/src/useControlled.ts:41,46`. Test: none.
- **NUM-STA-04** — MUST. In consumer-owned mode the numeric value MUST follow the `value` prop on every render (including changes unrelated to user interaction) and the display text MUST be re-formatted from it whenever sync is allowed; in component-owned mode a non-canceled change MUST store the validated value internally.
  Source: `number-field/root/NumberFieldRoot.tsx:116-121,259,312-324`, `packages/utils/src/useControlled.ts:83-90`. Test: [L2] `NumberFieldRoot.test.tsx` "should accept a number value that can change over time", "reflects controlled value changes in the textbox"; `NumberFieldInput.test.tsx` "should update input value after increment/decrement followed by external value change".
- **NUM-STA-05** — MUST. The initial display text MUST be `formatNumber(initialValue, locale, format)` (NUM-I18N-01), computed during the first render (so server-rendered markup already contains the formatted value).
  Source: `number-field/root/NumberFieldRoot.tsx:145-149`. Test: [L2] `NumberFieldRoot.test.tsx` "should accept a number value", "should format the value using the provided options".
- **NUM-STA-06** — MUST. After every update of the component (any state or parameter change) while sync is allowed, the display text MUST be set to `formatNumber(value, locale, format)` if it differs (post-commit, pre-paint). While sync is suspended (after typing or pasting, until blur or a step interaction) the display text MUST NOT be overwritten by changes of `value`, `locale` or `format`.
  Source: `number-field/root/NumberFieldRoot.tsx:142,266-272,312-324`, `number-field/input/NumberFieldInput.tsx:154,237,385,450`. Test: [L2] `NumberFieldRoot.test.tsx` "reformats the visible text when the format prop changes at the same value"; `NumberFieldInput.test.tsx` "commits formatted value only on blur", "should apply default formatting after external change during typing" (external change while typing *is* reflected because `onValueChange` → `setValue` re-formats when sync is allowed — see §16 Q4).
- **NUM-STA-07** — MUST. Every requested value MUST pass through the validation pipeline `toValidatedNumber(value, signedStep, minWithDefault, maxWithDefault, minWithZeroDefault, format, snapOnStep, small, shouldClamp)` before being reported or stored, where: `signedStep` = `stepAmount × direction` for step-based requests (reasons with a `direction`) and `undefined` for direct entry; `small` = the triggering event's `altKey`; `shouldClamp` = `!allowOutOfRange || !isInputReason`; `isInputReason` = reason starts with `input-` or equals `none`. A `null` request MUST pass through unchanged.
  Source: `number-field/root/NumberFieldRoot.tsx:219-242`, `number-field/utils/validate.ts:92-136`. Test: [L1] `validate.test.ts` "returns null when value is null", "skips clamping when clamp is false".
- **NUM-STA-08** — MUST. Snapping (applied only when `snapOnStep` is `true`, `signedStep` is defined and non-zero) MUST use base `B = (small || min is absent) ? minWithZeroDefault : minWithDefault`, tolerance `t = |step| × 1e-10 × sign(step)`, `raw = value − B + t`, and produce: for small steps the *nearest* grid point `B + round(raw / step) × step`; otherwise the *directional* grid point `B + floor(raw / |step|) × |step|` when stepping up or `B + ceil(raw / |step|) × |step|` when stepping down. (Hence incrementing 5.5 by 1 gives 5, decrementing gives 6; 0.15 with small step 0.1 gives 0.2.)
  Source: `number-field/utils/validate.ts:77-90,109-116`. Test: [L1] `validate.test.ts` "snaps 5.5 to 5 when step is 1", "snaps 5.5 to 6 when step is -1", "snaps -0.3 to -1 when step is 1", "rounds to the nearest value when using small step", "handles increment with step 0.1 without getting stuck", "handles fractional step when a minimum is set"; `NumberFieldIncrement.test.tsx` "should increment with respect to the min value"; `NumberFieldDecrement.test.tsx` "should decrement with respect to the min value".
- **NUM-STA-09** — MUST. The pipeline order MUST be: snap (NUM-STA-08) → clamp to `[minWithDefault, maxWithDefault]` if `shouldClamp` → round (NUM-STA-10) → clamp again if `shouldClamp`. Snapping before clamping keeps a non-step-aligned boundary reachable (raw 13, step 3, max 10 → 10); clamping before rounding makes `0.4` with `min 0.6` and an integer format round to `1`.
  Source: `number-field/utils/validate.ts:117-135`. Test: [L1] `validate.test.ts` "clamps to a non-step-aligned max after snapping so the boundary is reachable", "clamps before rounding for non-integer bounds", "clamps the final value after percent rounding crosses max".
- **NUM-STA-10** — MUST. When `signedStep` is `undefined` (direct entry, autofill, external value) and `format` carries none of `maximumFractionDigits`, `minimumFractionDigits`, `maximumSignificantDigits`, `minimumSignificantDigits`, `roundingIncrement`, `roundingMode`, `roundingPriority`, the (clamped) value MUST be returned verbatim — every digit the number type can represent is preserved, including more than 15 significant digits. Otherwise the value MUST be passed through `removeFloatingPointErrors` (NUM-STA-11/12).
  Source: `number-field/utils/validate.ts:19-31,125-135`. Test: [L1] `validate.test.ts` "preserves parsed input beyond 15 significant digits when step is undefined"; `NumberFieldInput.test.tsx` "should preserve values typed with more than 15 significant digits".
- **NUM-STA-11** — MUST. Without rounding options, `removeFloatingPointErrors(v)` MUST return `parseFloat(v.toPrecision(15))` when `|rounded − v| ≤ min(Number.EPSILON × max(1, |v|), 1e-10)`, and `v` unchanged otherwise (non-finite values are returned unchanged). This cleans `0.1 + 0.7 → 0.8` but leaves `1000000.1 + 0.2` and `100000000000000.1 + 0.1` untouched.
  Source: `number-field/utils/validate.ts:6-9,33-57`. Test: [L1] `validate.test.ts` "returns 0.3 for 0.2 + 0.1", "cleans 16-digit noise patterns from arithmetic", "leaves large-magnitude noise uncleaned once one ULP exceeds the absolute cap", "preserves safe integers exactly without a format"; `NumberFieldRoot.test.tsx` "cleans binary floating point noise introduced by stepping", "preserves large fractional values when stepping cleanup would be too coarse".
- **NUM-STA-12** — MUST. With rounding options, `removeFloatingPointErrors(v, format)` MUST format `v` with an `en-US` `Intl.NumberFormat` built from `format` overridden with `signDisplay: 'auto'`, `currencySign: 'standard'`, `useGrouping: false` and `notation` replaced by `'standard'` when it is `'compact'`; parse the result back with `parseNumber(text, 'en-US', format)` (NUM-I18N-02, so percent styles are scaled back); and return the parsed number only if re-formatting it yields the same text (otherwise return `v`). A `null` parse (overflow) returns `v`.
  Source: `number-field/utils/validate.ts:59-74`. Test: [L1] `validate.test.ts` "respects roundingMode when maximumFractionDigits is provided", "rounds percent values at display scale when maximumFractionDigits is provided", "rounds compact notation against its standard equivalent", "preserves non-invertible scientific rounded zero buckets", "keeps the original finite value when percent scaling overflows before Intl rounding", "preserves negative values when signDisplay hides the sign", "rounds negative accounting currency values", "respects roundingIncrement when maximumFractionDigits is provided".
- **NUM-STA-13** — MUST. Within a `Field.Root`, the field's `filled` state MUST be set to `value !== null` post-commit, pre-paint after mount and after every change of `value`.
  Source: `number-field/root/NumberFieldRoot.tsx:125-127`. Test: [L2] `NumberFieldRoot.test.tsx` "adds [data-filled] attribute when filled", "has [data-filled] attribute when already filled".
- **NUM-STA-14** — MUST. Within a `Field.Root`, the field's `dirty` state MUST be set to `validatedValue !== initialValue` synchronously inside each *applied* (non-canceled, reported) change, before the value is stored; canceled changes MUST NOT touch it.
  Source: `number-field/root/NumberFieldRoot.tsx:251-262`. Test: [L2] `NumberFieldRoot.test.tsx` "[data-dirty]".
- **NUM-STA-15** — MUST. The root's `scrubbing` state MUST become `true` when any `ScrubArea` under it starts a scrub (committed synchronously inside the pointerdown handler) and `false` when that scrub ends or the scrub area unmounts mid-scrub.
  Source: `number-field/root/NumberFieldRoot.tsx:104,434`, `number-field/scrub-area/NumberFieldScrubArea.tsx:127-133,277-290,335`. Test: [L2] `NumberFieldScrubArea.test.tsx` "clears the root scrubbing state when the scrub area unmounts mid-scrub", "tracks visual viewport scale and removes scrub listeners after pointer lock exits" (L4 for the pointerdown-timing clause).
- **NUM-STA-16** — MUST. The step amount for an interaction MUST be `smallStep` when the triggering event's `altKey` is set, else `largeStep` when its `shiftKey` is set, else the base step (`step`, or `1` for `'any'`). The modifiers are read from the event that triggered the step (keydown, pointerdown/mouseenter for holds, wheel, pointermove for scrub), not from global keyboard state.
  Source: `number-field/root/NumberFieldRoot.tsx:209-217`, `number-field/input/NumberFieldInput.tsx:375`, `number-field/root/useNumberFieldStepperButton.ts:105,138`, `number-field/root/NumberFieldRoot.tsx:395`, `number-field/scrub-area/NumberFieldScrubArea.tsx:230`. Test: [L2] `NumberFieldRoot.test.tsx` "should use explicit `largeStep` value if provided while holding the shift key", "should not use the `smallStep` prop if no longer holding the alt key", "uses largeStep when shift is held during wheel"; `NumberFieldInput.test.tsx` "uses smallStep and snapOnStep when holding Alt with ArrowUp/ArrowDown".
- **NUM-STA-17** — MUST. A step requested while the numeric value is `null` (and no dirty text supplies a current value) MUST request `0` with *no* `direction` — so it is clamped into range (e.g. `max` for a fully negative range) but never directionally snapped — regardless of the step direction or amount.
  Source: `number-field/root/NumberFieldRoot.tsx:281-291`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "seeds an empty field with 0", "seeds an empty fully-negative range in range on the first step", "seeds an empty field in range without directional snapping".
- **NUM-STA-18** — MUST. A change request MUST be *reported* (invoke `onValueChange`, then store/mark dirty unless canceled) when `validatedValue !== currentValue`, or — only for direct-entry reasons (`input-*`, `none`) — when the *unvalidated* request differs from the current value or sync is currently suspended. Otherwise the request MUST be silently dropped (no callback). In every case (reported or not, canceled or not… see NUM-EVT-04) the "last changed value" used by later commits is set to the validated value and, if sync is allowed, the display text is re-formatted from it.
  Source: `number-field/root/NumberFieldRoot.tsx:244-277`. Test: [L2] `NumberFieldRoot.test.tsx` "prevents the raw value from going below the `min` prop" (typed 4, reported 5 while value stays 5), "does not commit when a wheel step is a no-op at the boundary"; `NumberFieldInput.test.tsx` "reports an unchanged value once when a paste event carries no clipboard data".
- **NUM-STA-19** — MUST. The visible input's `inputMode` MUST be `numeric` initially and on every non-iOS platform. On iOS (per `platform.os.ios`, UTL-EDGE-04) it MUST be recomputed post-commit, pre-paint whenever `minWithDefault` changes: `decimal` when `minWithDefault >= 0`, otherwise `text` (the iOS numeric keyboard has no minus key).
  Source: `number-field/root/NumberFieldRoot.tsx:150,326-345`, `number-field/input/NumberFieldInput.tsx:119`. Test: [L2] `NumberFieldRoot.test.tsx` "should set the inputMode to numeric"; `NumberFieldRoot.iOS.test.tsx` "uses the decimal keyboard when negative values are impossible", "uses the text keyboard when negative values are allowed", "uses the text keyboard when no minimum is set".
- **NUM-STA-20** — MUST. A stepper button's effective disabled MUST be `disabledProp || rootDisabled || atBoundary`, where `atBoundary` is `value !== null && value >= maxWithDefault` for `Increment` and `value !== null && value <= minWithDefault` for `Decrement`. `readOnly` is *not* part of the effective disabled (it is folded into the button mechanism's `disabled` option and the interaction gates instead).
  Source: `number-field/root/useNumberFieldStepperButton.ts:62-64,177`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "passes disabled state to className when root is disabled" / "when button is disabled", "stops calling onValueChange once the boundary is reached"; `NumberFieldRoot.test.tsx` "does not render aria-readonly on stepper buttons". Docs: CHANGELOG.md v1.0.0-beta.2 "Fix button disabled state only including root disabled state".

### 4.1 Formatting and parsing (I18N)

Formatting is a thin wrapper over `Intl.NumberFormat` (shared utility UTL-I18N-05..07). Parsing is the component's own algorithm, reproduced here because the test suite pins its edge cases. Both take the same `(locale, format)` pair the consumer passed.

- **NUM-I18N-01** — MUST. `formatNumber(value, locale, format)` MUST return `""` for `null`/`undefined` and otherwise `new Intl.NumberFormat(locale, format).format(value)`; formatter instances are cached per `(stringified locale, JSON options)` (UTL-I18N-06/07). Server: relaxed, see §9.
  Source: `packages/utils/src/formatNumber.ts:5-28`. Test: [L1] `NumberFieldRoot.test.tsx` "should format the value using the provided options", "should set the locale of the input", "should handle locales using space as the thousands separator".
- **NUM-I18N-02** — MUST. `parseNumber(text, locale?, format?)` MUST return `number | null` by this procedure (pseudo-code; every step is normative):

  ```
  input ← text with all Unicode format-control characters (\p{Cf}) removed, then trimmed
  input ← input with every minus variant [-−－‒–—﹣] → "-" and every plus variant [+＋﹢] → "+"
  negative ← false
  if input ends with /([+-])\s*$/  : remove it; negative ← true if it was "-"
  if input starts with /^\s*([+-])/ : remove it; negative ← true if it was "-"
  loc ← locale; if loc is undefined: loc ← "ar" if input has [٠-٩۰-۹], else "zh" if input has [零〇一二三四五六七八九]
  {group, decimal, currency, exponentSeparator} ← getNumberLocaleDetails(loc, format)        // NUM-I18N-07
  unitRegex ← alternation of every `unit` part of formatToParts(1) for (loc, format), or none
  groupRegex ← if group is a Unicode space separator: /\p{Zs}/g; if group is "'" or "’": /['’]/g; else the literal group
  s ← input
  s ← s with groupRegex removed; with the literal decimal → "."; with [．٫] → "."; with [，٬] removed;
       with the literal currency removed; with unitRegex removed; with [%٪％﹪] removed; with [‰؉] removed;
       with the literal exponentSeparator → "e";
       with each of [٠-٩۰-۹０-９] → String(charCode mod 16); with each Han digit → its index in "零〇一二三四五六七八九" minus 1, floored at 0
  if s contains ".": keep only the LAST "." (remove every earlier one)
  if input matches /^[-+]?Infinity$/i or contains "∞": return null
  n ← parseFloat((negative ? "-" : "") + s)
  if [‰؉] occurred in text: n ← n × 10⁻³ (decimal-exponent shift, see below)
  else if ([%٪％﹪] occurred in text or format.style = "percent") and not (style = "unit" and unit = "percent"): n ← n × 10⁻²
  return isFinite(n) ? n : null
  ```
  The `×10⁻ᵏ` shift MUST be done on the decimal representation (`String(n)` split at `e`, exponent decreased by `k`, re-parsed), not by floating-point division, so `12% → 0.12` exactly and `1e-7% → 1e-9`. Server: relaxed, see §9.
  Source: `number-field/utils/parse.ts:56-59,107-221`. Test: [L1] `parse.test.ts` — every test under "parseNumber" (54 tests, see §13).
- **NUM-I18N-03** — MUST. When `locale` is `undefined`, the parser MUST infer `'ar'` if the text contains Arabic-Indic or Persian digits, else `'zh'` if it contains Han numerals, else use the runtime default locale; an explicit `locale` is never overridden. Server: relaxed, see §9.
  Source: `number-field/utils/parse.ts:130-138`. Test: [L1] `parse.test.ts` "parses a number with Arabic numerals", "parses a number with Han numerals", "parses Persian digits".
- **NUM-I18N-04** — MUST. Digit mapping: Arabic-Indic U+0660–0669, Persian U+06F0–06F9 and fullwidth U+FF10–FF19 map to `charCode mod 16`; Han `零` and `〇` both map to `0`, `一`…`九` to `1`…`9`. All systems MAY be mixed in one string.
  Source: `number-field/utils/parse.ts:3-11,181-183`. Test: [L1] `parse.test.ts` "maps the full Persian digit range", "maps the full fullwidth digit range", "maps the full Arabic-Indic digit range", "maps the full Han digit range", "maps both Han zero forms to 0"; `NumberFieldRoot.test.tsx` "parses Persian digits and separators via change events", "parses fullwidth digits and punctuation".
- **NUM-I18N-05** — MUST. Any of `%`, `٪`, `％`, `﹪` in the *original* text, or `format.style === 'percent'`, MUST divide the parsed number by 100 unless the format is `style: 'unit', unit: 'percent'` (then `12%` is `12`); any of `‰`, `؉` MUST divide by 1000 (taking precedence over percent). Interleaved signs (`1%2`) are stripped before parsing (`→ 12 → 0.12`).
  Source: `number-field/utils/parse.ts:13-14,178-179,205-214`. Test: [L1] `parse.test.ts` "parses percentages by default", "handles percentages with style: \"percent\"", "handles percentages with style: \"unit\" and unit: \"percent\"", "strips an interleaved percent sign (1%2)", "parses permille values", "parses scientific notation percentages", "parses scientific notation permille values", "handles additional percent symbol variants", "parses prefix percentages"; `NumberFieldRoot.test.tsx` "parses percent while typing and commits canonical percent value", "parses percent and permille signs in exotic forms when formatted as percent".
- **NUM-I18N-06** — MUST. `Infinity` (any case, optionally signed, surrounded by spaces), the `∞` symbol, anything whose `parseFloat` is `NaN` (empty string, lone sign, letters, parenthesized negatives), and any result that is not finite (overflow) MUST parse to `null`.
  Source: `number-field/utils/parse.ts:196-199,216-218`. Test: [L1] `parse.test.ts` "returns null for an invalid number", "returns null for Infinity-like inputs", "returns null for Infinity with explicit sign and surrounding spaces", "returns null when parsing overflows to Infinity", "returns null for empty and whitespace-only input", "returns null for just a sign"; `NumberFieldRoot.test.tsx` "treats parentheses negatives as invalid input".
- **NUM-I18N-07** — MUST. `getNumberLocaleDetails(locale, format)` MUST return, for every part type emitted by `formatToParts(11111.1)` under `(locale, format)`, the *last* part value of that type (`group`, `currency`, `unit`, `percentSign`, `exponentSeparator`, `literal`, `minusSign`, …), with `decimal` always overridden by the decimal part of `formatToParts(0.1)` under the plain locale (no options) — so integer formats still know their locale's decimal separator. Server: relaxed, see §9.
  Source: `number-field/utils/parse.ts:66-105`. Test: [L1] `parse.test.ts` "returns the number locale details".
- **NUM-I18N-08** — MUST. Group separators MUST be removed as a *class*: when the locale's group is any Unicode space separator (`\p{Zs}`: NBSP, NNBSP, thin, figure, plain space), every space-separator character is removed; when it is an apostrophe (`'` or `’`), both apostrophes are removed; otherwise the literal separator is removed. Fullwidth comma and Arabic thousands separator are always removed. Server: relaxed, see §9.
  Source: `number-field/utils/parse.ts:152-167,173-174`. Test: [L1] `parse.test.ts` "parses french formatted numbers with narrow no-break space grouping", "removes various Unicode space groupings (fr-FR)", "handles Swiss grouping apostrophe", "parses de-DE formatted numbers", "parses Arabic punctuation with ASCII digits"; `NumberFieldInput.test.tsx` "allows space key when locale uses space-like grouping (pl-PL)". Docs: CHANGELOG.md v1.0.0-rc.0 "Fix parsing numbers with Swiss locale", v1.0.0-alpha.7 "Fix parse of numbers with spaces as thousands separators".
- **NUM-I18N-09** — MUST. After separator replacement, only the last `.` MUST be kept as the decimal point and every earlier `.` removed (`1.234.567.89 → 1234567.89`; `....5 → 0.5`); hence under `en-US` the text `1.234.567,89` parses to `1234.56789` (the `,` group is removed first) while under `fr-FR`/`de-DE` it parses to `1234567.89`.
  Source: `number-field/utils/parse.ts:190-194`. Test: [L1] `parse.test.ts` "collapses extra dots from mixed-locale inputs", "parses number with mixed separators (FR)", "parses number with mixed separators (US)", "collapses multiple consecutive dots keeping only the last as decimal"; `NumberFieldRoot.test.tsx` "collapses extra dots from mixed-locale inputs".
- **NUM-I18N-10** — MUST. A single leading or trailing sign (after whitespace trimming; ASCII or any Unicode minus/plus variant) MUST set the sign; a trailing minus (`1234−`, `1 234,5−` in fr-FR) MUST yield a negative number; a plus sign never changes the value.
  Source: `number-field/utils/parse.ts:51-52,61-64,115-128`. Test: [L1] `parse.test.ts` "handles unicode minus and plus signs", "handles ASCII leading and trailing signs", "supports multiple unicode minus variants", "supports additional unicode plus variants"; `NumberFieldRoot.test.tsx` "parses trailing unicode minus".
- **NUM-I18N-11** — MUST. Unicode format-control characters (`\p{Cf}`: LRM, RLM, ALM, LRE…PDF, etc.) MUST be ignored anywhere in the text, both by the parser and by the change-string validation (NUM-EVT-15), so RTL-locale output (which wraps exponent and currency signs in bidi marks) round-trips.
  Source: `number-field/utils/parse.ts:46-50,113`, `number-field/input/NumberFieldInput.tsx:255-257`. Test: [L1] `parse.test.ts` "strips bidi/control characters", "treats bidi/format controls as ignorable in the middle of input", "parses localized scientific notation for %s", "parses localized percent scientific notation with a negative exponent for %s", "parses localized tiny percent scientific notation for %s"; `NumberFieldRoot.test.tsx` "ignores bidi/format control characters in the value (e.g. RTL exponent signs)".
- **NUM-I18N-12** — MUST. The locale's currency string (symbol, code or name as formatted), every unit string the format renders (multi-part units such as `km/h`, `m/s` included) and the exponent separator MUST be stripped (the exponent separator replaced by `e`, so `1.5E3` and localized scientific notation parse) before numeric parsing; these strings are matched literally (regex-escaped), case-sensitively. Server: relaxed, see §9.
  Source: `number-field/utils/parse.ts:54,145-150,175-180`. Test: [L1] `parse.test.ts` "parses currency when options specify currency style", "parses currency prefix/suffix across locales", "parses units when options specify unit style", "parses units with different formats", "parses scientific notation with currency codes"; `NumberFieldRoot.test.tsx` "accepts currency symbol while typing and parses numeric value", "accepts multi-character unit symbols while typing (e.g. km/h)", "accepts exponent separators while typing (scientific notation)", "allows deleting trailing currency symbols with locale literals". Docs: CHANGELOG.md v1.0.0-rc.0 "Improve symbol replacement logic", v1.0.0-beta.4 "Improve parsing logic".
- **NUM-I18N-13** — MUST. Changing `locale` or `format` at runtime MUST re-format the display text from the current value (when sync is allowed, NUM-STA-06) and MUST change the parse rules, allowed-character set and `inputMode`-independent gating for subsequent input immediately; the formatter cache MUST key on the stringified locale (so `'de-DE'` and `['de-DE']` share an instance and distinct locales never collide). Server: relaxed, see §9.
  Source: `number-field/root/NumberFieldRoot.tsx:152-153,312-324`, `packages/utils/src/formatNumber.ts:5-17`. Test: [L2] `NumberFieldRoot.test.tsx` "reformats the visible text when the format prop changes at the same value". Docs: CHANGELOG.md v1.6.0 "Fix formatter locale cache".

## 5. Rendered DOM and ARIA

### 5.1 Root element

Default `<div>`; no `tabindex`, no `id`, no handlers of its own. Only the state attributes and forwarded consumer attributes.

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-scrubbing` | `""` while scrubbing | NUM-DOM-02 | C# |
| `data-disabled`, `data-readonly`, `data-required` | `""` while the corresponding state is `true` | NUM-DOM-03 | C# |
| `data-touched`, `data-dirty`, `data-filled`, `data-focused` | `""` while the corresponding field state is `true` | NUM-DOM-04 | C# |
| `data-valid` / `data-invalid` | `""` while field `valid` is `true` / `false`; neither while `null` | NUM-DOM-05 | C# |

- **NUM-DOM-01** — MUST. Every part MUST derive its data attributes from its state object with the default projection (RND-DOM-01..05) except that the `value` and `inputValue` fields MUST produce no attribute and the `valid` field MUST be projected per the shared field-validity mapping (FFI-DOM-01: `true` → `data-valid`, `false` → `data-invalid`, `null` → nothing).
  Source: `number-field/utils/stateAttributesMapping.ts:5-9`, `internals/field-constants/constants.ts:34-44`. Test: [L2] `NumberFieldRoot.test.tsx` "onSubmit" (validationMode; asserts `data-invalid`/`data-valid` on root and input).
- **NUM-DOM-02** — MUST. `data-scrubbing=""` MUST be present on every part while the root's `scrubbing` state is `true` and absent otherwise.
  Source: `number-field/root/NumberFieldRoot.tsx:434`, `number-field/root/NumberFieldRootDataAttributes.ts:4`. Test: [L2] `NumberFieldScrubArea.gecko.test.tsx` "delays releasing the pointer lock so soft clicks are not swallowed"; `NumberFieldScrubArea.test.tsx` "ignores non-primary pointer buttons".
- **NUM-DOM-03** — MUST. `data-disabled`, `data-readonly` and `data-required` MUST be present (value `""`) on every part exactly while the effective disabled, `readOnly` and `required` states are `true`; on `Increment`/`Decrement`, `data-disabled` follows the button's effective disabled (NUM-DOM-17).
  Source: `number-field/root/NumberFieldRoot.tsx:429-431`, `number-field/root/NumberFieldRootDataAttributes.ts:8-16`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "passes disabled state to className …" (state), conformance suites.
- **NUM-DOM-04** — MUST. `data-touched`, `data-dirty`, `data-filled` and `data-focused` MUST be present on every part exactly while the enclosing field's corresponding state is `true`; outside a `Field.Root` they are never present.
  Source: `number-field/root/NumberFieldRoot.tsx:428`, `number-field/root/NumberFieldRootDataAttributes.ts:28-40`. Test: [L2] `NumberFieldRoot.test.tsx` "[data-touched]", "[data-dirty]", "adds [data-filled] attribute when filled", "adds [data-focused] attribute on every focus". Docs: CHANGELOG.md v1.2.0 "Fix missing field state data attributes".
- **NUM-DOM-05** — MUST. Exactly one of `data-valid` / `data-invalid` MUST be present on every part while the field's `valid` is `true` / `false`, and neither while it is `null`.
  Source: `number-field/utils/stateAttributesMapping.ts:8`, `internals/field-constants/constants.ts:34-44`. Test: [L2] `NumberFieldRoot.test.tsx` "onSubmit".

### 5.2 Hidden `<input type="number">`

Rendered as the root element's next sibling, always.

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `type` | `number` | NUM-DOM-06 | C# |
| `name` | effective name, or absent | NUM-DOM-06 | C# |
| `form` | the `form` prop, or absent | NUM-DOM-06 | C# |
| `value` | `String(value)` or `""` when `null` (never formatted text) | NUM-DOM-06 | C# |
| `min`, `max` | the raw props (absent when undefined) | NUM-DOM-06 | C# |
| `step` | the raw `step` prop (`1` by default, or `"any"`) | NUM-DOM-06 | C# |
| `disabled`, `readonly`, `required` | mirror effective disabled / `readOnly` / `required` | NUM-DOM-06 | C# |
| `aria-hidden` | `"true"` | NUM-ARIA-10 | C# |
| `tabindex` | `-1` | NUM-DOM-06 | C# |
| `style` | `visuallyHiddenInput` when a name exists, else `visuallyHidden` | NUM-DOM-07 | C# |
| `aria-describedby`, `aria-invalid` | from the field's validation props (FFI-ARIA-01/03) | NUM-ARIA-04 | C# |
| `id` | none | NUM-API-18 | C# |

- **NUM-DOM-06** — MUST. The hidden input MUST carry exactly the attributes in the table: `type="number"`, the effective `name`, `form`, `value` = the numeric value serialized per UTL-DOM-37 (`""` for `null`), raw `min`/`max`/`step`, `disabled`/`readOnly`/`required` mirroring the root, `aria-hidden`, `tabindex="-1"`, and no `id`. Its `value` MUST never contain locale formatting.
  Source: `number-field/root/NumberFieldRoot.tsx:500-540`; see UTL-DOM-37. Test: [L2] `NumberFieldRoot.test.tsx` "should set the name attribute on the hidden input", "marks the hidden input readOnly when the field is readOnly", "should not include formatting in the submitted value", "allows range overflow validation when true" (`hiddenInput.value === '6'`), "blocks submission when step mismatch occurs".
- **NUM-DOM-07** — MUST. The hidden input's inline style MUST be the shared `visuallyHiddenInput` style (UTL-DOM-14, `position: absolute`) when an effective name exists and the shared `visuallyHidden` style (UTL-DOM-13, `position: fixed; top: 0; left: 0`) otherwise.
  Source: `number-field/root/NumberFieldRoot.tsx:538`, `packages/utils/src/visuallyHidden.ts:3-25`. Test: none.

### 5.3 `NumberField.Input` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `type` | `text` (consumer may override; unsupported, see NUM-EDGE-14) | NUM-DOM-08 | C# |
| `id` | the labelable control id (prop `id` or generated) | NUM-DOM-09 | C# |
| `value` | the display text | NUM-DOM-08 | C# |
| `inputmode` | `numeric` / `decimal` / `text` (NUM-STA-19) | NUM-DOM-08 | C# |
| `autocomplete` | `off` | NUM-DOM-08 | C# |
| `autocorrect` | `off` | NUM-DOM-08 | C# |
| `spellcheck` | `false` | NUM-DOM-08 | C# |
| `required`, `disabled`, `readonly` | mirror `required` / effective disabled / `readOnly` | NUM-DOM-08 | C# |
| `name` | never | NUM-DOM-18 | C# |
| `aria-roledescription` | `Number field` (or consumer value) | NUM-ARIA-01 | C# |
| `aria-invalid` | `"true"` while field invalid and not disabled; absent otherwise | NUM-ARIA-02 | C# |
| `aria-labelledby` | the field label id when a `Field.Label` exists; absent otherwise | NUM-ARIA-03 | C# |
| `aria-describedby` | composed field description/error ids merged with a consumer value | NUM-ARIA-04 | C# |
| data attributes | as §5.1 | NUM-DOM-01..05 | C# |

- **NUM-DOM-08** — MUST. The visible input MUST render with `type="text"`, `value` = the display text, `inputmode` per NUM-STA-19, `autocomplete="off"`, `autocorrect="off"`, `spellcheck="false"`, and `required`/`disabled`/`readonly` mirroring the root state. A consumer attribute of the same name MUST override the built-in value (merge order: built-in, then consumer, then the field's validation props).
  Source: `number-field/input/NumberFieldInput.tsx:114-124,458-463`. Test: [L2] `NumberFieldRoot.test.tsx` "should set the inputMode to numeric", "should disable the input", "should mark the input as readOnly", "should mark the input as required"; `NumberFieldInput.test.tsx` "has textbox role", conformance suite.
- **NUM-DOM-09** — MUST. The visible input's `id` MUST be the labelable control id resolved by FFI-ALG-06/07 from the root's `id` prop (generated when absent); a `Field.Label` in the same field MUST therefore render `for` equal to it (FFI-ARIA-04).
  Source: `number-field/root/NumberFieldRoot.tsx:114`, `number-field/input/NumberFieldInput.tsx:115`. Test: [L2] `NumberFieldRoot.test.tsx` "Field.Label".
- **NUM-DOM-18** — MUST. The visible input MUST NOT carry a `name` attribute; only the hidden input participates in form submission.
  Source: `number-field/input/NumberFieldInput.tsx:114-130` (no `name` emitted), `number-field/root/NumberFieldRoot.tsx:526`. Test: [L2] `NumberFieldRoot.test.tsx` "should set the name attribute on the hidden input" (`input[aria-hidden][type=number]` selector).

### 5.4 `NumberField.Increment` / `NumberField.Decrement` elements

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `type` | `button` (native mode, UBT-DOM-01) | NUM-DOM-10 | C# |
| `tabindex` | `-1` | NUM-DOM-10 | C# |
| `disabled` | present while the button's effective disabled (NUM-STA-20) is `true` | NUM-DOM-10 | C# |
| `aria-disabled` | `"true"` while effective disabled **or** `readOnly`; `"false"` otherwise (always present in native mode) | NUM-ARIA-07 | C# |
| `aria-label` | `Increase` / `Decrease` | NUM-ARIA-05 | C# |
| `aria-controls` | the visible input's id | NUM-ARIA-06 | C# |
| `style` | `user-select: none; -webkit-user-select: none` (merged with consumer style) | NUM-DOM-10 | C# |
| data attributes | as §5.1, `data-disabled` from the button's effective disabled | NUM-DOM-17 | C# |

- **NUM-DOM-10** — MUST. Each stepper button MUST render `tabindex="-1"`, an inline `user-select: none` (and `-webkit-user-select: none`) style, and — because the button passes its own `disabled` attribute through the button mechanism (UBT-DOM-09) — the boolean `disabled` attribute exactly while its effective disabled (NUM-STA-20) is `true`, even though the mechanism is configured `focusableWhenDisabled: true`. In native mode `type="button"` is emitted (UBT-DOM-01); in non-native mode `role="button"` (UBT-DOM-02).
  Source: `number-field/root/useNumberFieldStepperButton.ts:18-21,120-128,173-189`, `internals/use-button/useButton.ts:102-229`, `utils/useFocusableWhenDisabled.ts:45-47`; see [button-behavior](../architecture/button-behavior.md) UBT-DOM-06/07/09. Test: [L2] `useNumberFieldStepperButton.test.tsx` "does not step when the button is disabled" (`disabled` attribute); `NumberFieldDecrement.test.tsx` / `NumberFieldIncrement.test.tsx` conformance suites (`button: true`).
- **NUM-DOM-17** — MUST. `data-disabled` on a stepper button MUST reflect the button's effective disabled (own prop OR root OR boundary), so a button at its boundary carries `data-disabled` while the root does not.
  Source: `number-field/root/useNumberFieldStepperButton.ts:182-186`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "passes disabled state to className when button is disabled".

### 5.5 `NumberField.Group` element

Default `<div>` with `role="group"` (NUM-ARIA-09) and the state attributes. No other built-in attributes.

### 5.6 `NumberField.ScrubArea` element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `role` | `presentation` | NUM-ARIA-09 | C# |
| `style` | `touch-action: none; user-select: none; -webkit-user-select: none` | NUM-DOM-12 | C# |
| data attributes | as §5.1 | NUM-DOM-01..05 | C# |

- **NUM-DOM-12** — MUST. The scrub area MUST render `role="presentation"` and an inline style of `touch-action: none`, `user-select: none`, `-webkit-user-select: none` (consumer `style` merged on top), plus the state attributes.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:21-25,311-313`. Test: [L2] `NumberFieldScrubArea.test.tsx` "has presentation role".

### 5.7 `NumberField.ScrubAreaCursor` element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `role` | `presentation` | NUM-ARIA-09 | C# |
| `style` | `position: fixed; top: 0; left: 0; pointer-events: none` plus an imperatively written `transform` | NUM-DOM-13 | C# → JS |
| data attributes | as §5.1 (`data-scrubbing` is always present while it exists) | NUM-DOM-01..05 | C# |

- **NUM-DOM-13** — MUST. While rendered (NUM-STR-06), the cursor element MUST carry `role="presentation"`, the inline style `position: fixed; top: 0; left: 0; pointer-events: none`, and its `transform` style MUST be written directly (outside the render cycle) as `translate3d(<x>px,<y>px,0) scale(<1/s>)` where `(x, y)` is the virtual cursor position (NUM-PTR-16) and `s` is `visualViewport.scale` of the owning window (`1` when unavailable), so the cursor stays the OS-cursor's size under pinch-zoom.
  Source: `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:13-18,48-55`, `number-field/scrub-area/NumberFieldScrubArea.tsx:77-82,124,146`. Test: [L4] `NumberFieldScrubArea.test.tsx` "tracks visual viewport scale and removes scrub listeners after pointer lock exits" (`scale(0.5)`, `scale(0.25)`), "wraps the virtual cursor around the allowed bounds".
- **NUM-DOM-14** — MUST. The cursor element MUST be appended to the owning document's `<body>` (a portal), and MUST be removed from the DOM — not merely hidden — as soon as any of the render conditions of NUM-STR-06 stops holding (scrub ended, touch pointer, WebKit, pointer lock denied).
  Source: `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:39-58`. Test: [L4] `NumberFieldScrubArea.test.tsx` "tracks visual viewport scale …" (`queryByTestId('cursor')` is `null` after pointerup); `NumberFieldScrubAreaCursor.test.tsx` "does not render after a quick tap when pointer lock resolves later".

### 5.8 Attribute precedence and conformance

- **NUM-DOM-15** — MUST. On every part, a consumer-supplied attribute MUST override the built-in attribute of the same name (including `role`, `aria-label`, `tabIndex`, `type`, `inputMode`, `aria-roledescription`), except that on `Input` the field's validation props (`aria-describedby` composition and `aria-invalid`) are applied last and therefore win over a consumer `aria-invalid` and *merge with* a consumer `aria-describedby`.
  Source: `number-field/input/NumberFieldInput.tsx:461`, `number-field/root/useNumberFieldStepperButton.ts:187`, `number-field/scrub-area/NumberFieldScrubArea.tsx:362`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:48-54`, `number-field/group/NumberFieldGroup.tsx:24`. Test: [L2] `NumberFieldRoot.test.tsx` "Field.Description" (`external-description` kept first); conformance suites (custom attributes).
- **NUM-DOM-16** — MUST. All seven parts MUST pass the shared conformance suite (RND-DOM-36..49) with these options: Root — `refInstanceof: HTMLDivElement`, rendered standalone; Group — `HTMLDivElement`, rendered inside `Root`; Input — `HTMLInputElement`, inside `Root`; Increment/Decrement — `HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true` (so the non-native `div` pass of RND-DOM-49 runs), inside `Root`; ScrubArea — `HTMLSpanElement`, inside `Root`; ScrubAreaCursor — `HTMLSpanElement`, inside `Root` + `ScrubArea` with a scrub-area context forced to `isScrubbing: true, isTouchInput: false, isPointerLockDenied: false` (skipped on WebKit).
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] each `*.test.tsx` "Base UI component API".

### 5.9 ARIA

- **NUM-ARIA-01** — MUST. The visible input MUST have the implicit `textbox` role (no explicit `role`; `type="text"`) and `aria-roledescription="Number field"` unless the consumer overrides it. It MUST NOT carry `role="spinbutton"` or any `aria-valuenow`/`aria-valuemin`/`aria-valuemax`/`aria-valuetext` attribute (the hinted spinbutton semantics do not exist in v1.9.0; see §16).
  Source: `number-field/input/NumberFieldInput.tsx:114-130`. Test: [L2] `NumberFieldInput.test.tsx` "has textbox role".
- **NUM-ARIA-02** — MUST. `aria-invalid="true"` MUST be present on the visible input exactly while the enclosing field is invalid (`invalid` state, FFI-STA-02 — which includes external form errors) and the field is not disabled; it MUST be absent otherwise (including while disabled, and outside a Field). Two code paths produce it — the input's own `invalid && !disabled` check and the field's validation props (FFI-ARIA-03) — and they MUST agree.
  Source: `number-field/input/NumberFieldInput.tsx:126,461`, `field/root/useFieldValidation.ts:367-376`. Test: [L2] `NumberFieldRoot.test.tsx` "ignores hidden-input autofill when $lockState inside Field" (readOnly → `"true"`, disabled → `null`), "clears external errors on change", "prop: validate", "onChange" (validationMode).
- **NUM-ARIA-03** — MUST. The visible input's `aria-labelledby` MUST be the id published by a `Field.Label` in the enclosing labelable scope (FFI-ARIA-08) and absent when there is none; a consumer `aria-labelledby` overrides it (NUM-DOM-15). No fallback discovery of a native `<label for>` is performed (unlike Checkbox/Switch).
  Source: `number-field/input/NumberFieldInput.tsx:84,127`. Test: none (label association is tested through `for`, NUM-DOM-09).
- **NUM-ARIA-04** — MUST. `aria-describedby` on both the visible input and the hidden input MUST be the field's composed description/error id list (FFI-ARIA-01), appended after any consumer-supplied `aria-describedby`; `aria-invalid` on the hidden input follows the same rule as NUM-ARIA-02.
  Source: `number-field/input/NumberFieldInput.tsx:461`, `number-field/root/NumberFieldRoot.tsx:501`. Test: [L2] `NumberFieldRoot.test.tsx` "Field.Description".
- **NUM-ARIA-05** — MUST. `Increment` MUST render `aria-label="Increase"` and `Decrement` `aria-label="Decrease"` by default; a consumer `aria-label` (or `aria-labelledby`) replaces it.
  Source: `number-field/root/useNumberFieldStepperButton.ts:122`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "has the Increase label" / "has the Decrease label".
- **NUM-ARIA-06** — MUST. Each stepper button MUST render `aria-controls` equal to the visible input's id (NUM-DOM-09), even when `Input` is not rendered.
  Source: `number-field/root/useNumberFieldStepperButton.ts:123`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "exposes aria-controls on the stepper".
- **NUM-ARIA-07** — MUST. Each stepper button MUST render `aria-disabled` as the stringified boolean of `effectiveDisabled || readOnly` (UBT-ARIA-01: present as `"false"` while enabled in native mode), so a read-only field exposes its steppers as unavailable without the `disabled` attribute.
  Source: `number-field/root/useNumberFieldStepperButton.ts:173-180`, `internals/use-button/useButton.ts:28-32`; see [button-behavior](../architecture/button-behavior.md). Test: [L2] `NumberFieldRoot.test.tsx` "does not render aria-readonly on stepper buttons".
- **NUM-ARIA-08** — MUST. Stepper buttons MUST NOT render `aria-readonly` (invalid on the `button` role) and MUST NOT be `aria-hidden` (touch screen readers must be able to operate them even though they are outside the tab order).
  Source: `number-field/root/useNumberFieldStepperButton.ts:124-127,174-176`. Test: [L2] `NumberFieldRoot.test.tsx` "does not render aria-readonly on stepper buttons".
- **NUM-ARIA-09** — MUST. `Group` MUST render `role="group"`; `ScrubArea` and `ScrubAreaCursor` MUST render `role="presentation"`.
  Source: `number-field/group/NumberFieldGroup.tsx:24`, `number-field/scrub-area/NumberFieldScrubArea.tsx:312`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:50`. Test: [L2] `NumberFieldGroup.test.tsx` "has role prop"; `NumberFieldScrubArea.test.tsx` "has presentation role"; `NumberFieldScrubAreaCursor.test.tsx` "has presentation role".
- **NUM-ARIA-10** — MUST. The hidden number input MUST carry `aria-hidden="true"` and `tabindex="-1"` so it is neither announced nor reachable by keyboard; it remains programmatically focusable (its focus is redirected, NUM-FOC-01).
  Source: `number-field/root/NumberFieldRoot.tsx:536-537`. Test: [L2] `NumberFieldRoot.test.tsx` "forwards focus to the visible input".

## 6. Interactions

### 6.1 Keyboard

All keyboard handling is on the visible `Input`. The stepper buttons are outside the tab order and have no key handlers of their own (a keyboard-generated `click` from assistive technology goes through the click path, NUM-PTR-01). The keydown handler runs *before* the browser inserts the character; it either lets the key through (returns without `preventDefault`), steps the value (`preventDefault` + `stopPropagation`), or blocks the character (`preventDefault`). Characters that get through are then validated again as a whole string by the change handler (NUM-EVT-15).

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowUp</kbd> / <kbd>ArrowDown</kbd> | Input focused, enabled, not read-only | `preventDefault` + `stopPropagation`; step by +/− the step amount (Shift → `largeStep`, Alt → `smallStep`); `onValueChange(reason 'keyboard', direction ±1)` then `onValueCommitted('keyboard')` if applied | NUM-KEY-01, NUM-KEY-12 |
| <kbd>Home</kbd> | `min` prop defined | `preventDefault`; value ← `min` (through the pipeline, no direction); change + commit with reason `keyboard` | NUM-KEY-02 |
| <kbd>End</kbd> | `max` prop defined | same with `max` | NUM-KEY-02 |
| <kbd>Home</kbd> / <kbd>End</kbd> | bound not defined | not intercepted (native caret movement) | NUM-KEY-02 |
| <kbd>Backspace</kbd>, <kbd>Delete</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Tab</kbd>, <kbd>Enter</kbd>, <kbd>Escape</kbd> | any | not intercepted; native behavior (Enter does **not** commit; Tab moves focus and the resulting blur commits) | NUM-KEY-03 |
| <kbd>PageUp</kbd>, <kbd>PageDown</kbd>, <kbd>Insert</kbd>, F-keys, any other multi-character key | any | not intercepted | NUM-KEY-04 |
| any key with <kbd>Ctrl</kbd> or <kbd>Meta</kbd>; any key except ArrowUp/Down with <kbd>Alt</kbd>; IME composition keydown (`which === 229`) | any | not intercepted | NUM-KEY-05 |
| a digit of any supported numeral system (`0-9`, Arabic-Indic, Persian, fullwidth, Han incl. `零`/`〇`) | any | not intercepted | NUM-KEY-06 |
| a character in the allowed non-numeric set (NUM-KEY-07) | subject to NUM-KEY-08/09 | not intercepted | NUM-KEY-07 |
| a sign (`+`/`-` and Unicode variants) when a sign already exists | sign not selected, not all selected | `preventDefault` (blocked) | NUM-KEY-08 |
| the decimal, currency or percent symbol when already present | symbol not selected, not all selected | `preventDefault` (blocked) | NUM-KEY-09 |
| any other single printable character (letters, `%` without a percent format, …) | any | `preventDefault` (blocked) | NUM-KEY-10 |
| anything | Input disabled or read-only, or the keydown's default already prevented by a consumer handler | nothing (not intercepted, no step) | NUM-KEY-11 |

- **NUM-KEY-01** — MUST. On <kbd>ArrowUp</kbd> / <kbd>ArrowDown</kbd> (with any of Alt/Shift, but not Ctrl/Meta) the Input MUST call `preventDefault()` and `stopPropagation()` on the keydown, mark display sync allowed, request `current + amount × direction` (direction `+1` for ArrowUp, `−1` for ArrowDown; amount per NUM-STA-16; `current` per NUM-KEY-12) with reason `keyboard` and that `direction`, and — if the request was applied — immediately call `onValueCommitted(storedValue, { reason: 'keyboard', event })` in the same task.
  Source: `number-field/input/NumberFieldInput.tsx:336,375-400,407-409`. Test: [L4] `NumberFieldInput.test.tsx` "should increment on keydown ArrowUp", "should decrement on keydown ArrowDown"; `NumberFieldRoot.test.tsx` "includes the reason for keyboard increments", "fires on keyboard interactions (ArrowUp/Down/Home/End)", "does not fire on keyboard steps that do not change the value", "does not commit a canceled keyboard change". Docs: CHANGELOG.md v1.0.0-beta.0 "Stop event propagation to allow composite components to be used within popups".
- **NUM-KEY-02** — MUST. On <kbd>Home</kbd> when the `min` prop is defined, or <kbd>End</kbd> when `max` is defined, the Input MUST `preventDefault()`/`stopPropagation()`, mark sync allowed, request that bound with reason `keyboard` and *no* direction (so it is clamped but not snapped), and commit synchronously if applied. When the corresponding bound is undefined the key MUST NOT be intercepted.
  Source: `number-field/input/NumberFieldInput.tsx:353-365,384-386,401-409`. Test: [L4] `NumberFieldInput.test.tsx` "should set the value to min on keydown Home", "should set the value to max on keydown End"; `NumberFieldRoot.test.tsx` "does not prevent native caret movement for Home/End without min/max".
- **NUM-KEY-03** — MUST. <kbd>Backspace</kbd>, <kbd>Delete</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Tab</kbd>, <kbd>Enter</kbd> and <kbd>Escape</kbd> MUST NOT be intercepted (no `preventDefault`, no value change). In particular <kbd>Enter</kbd> performs no commit of its own and a navigation key MUST NOT clear the dirty-text state (NUM-KEY-12).
  Source: `number-field/input/NumberFieldInput.tsx:35-43,279-283,334,348-351`. Test: [L4] `NumberFieldRoot.test.tsx` "should allow navigation keys and not prevent their default behavior"; `NumberFieldInput.test.tsx` "keeps dirty-input authority after a non-mutating key so the next keyboard step uses dirty text", "commits dirty text on blur after a cursor key when the controlled value does not mirror it".
- **NUM-KEY-04** — MUST. Any key whose `key` string is longer than one character and is not ArrowUp/ArrowDown or an applicable Home/End MUST NOT be intercepted (PageUp/PageDown, Insert, function keys, Shift, …).
  Source: `number-field/input/NumberFieldInput.tsx:361-365`. Test: [L4] `NumberFieldRoot.test.tsx` "does not swallow non-printing keys it does not handle".
- **NUM-KEY-05** — MUST. A keydown MUST NOT be intercepted when `ctrlKey` or `metaKey` is set, when `altKey` is set and the key is not ArrowUp/ArrowDown, or when it is an IME composition keydown (`which`/`keyCode` 229). Alt+ArrowUp/Down is a step (small step), not a bypass.
  Source: `number-field/input/NumberFieldInput.tsx:335-351`. Test: [L4] `NumberFieldRoot.test.tsx` "allows composition key events (IME) without preventing default"; `NumberFieldInput.test.tsx` "uses smallStep and snapOnStep when holding Alt with ArrowUp/ArrowDown".
- **NUM-KEY-06** — MUST. A single character that is a digit in any supported numeral system — ASCII `0-9`, Arabic-Indic U+0660–0669, Persian U+06F0–06F9, fullwidth U+FF10–FF19, Han `零〇一二三四五六七八九` — MUST NOT be intercepted.
  Source: `number-field/input/NumberFieldInput.tsx:347`, `number-field/utils/parse.ts:30-35`. Test: [L4] `parse.test.ts` "accepts a digit from every supported numeral system"; `NumberFieldInput.test.tsx` "allows unicode plus/minus, permille and fullwidth digits on keydown when formatted as percent" (`１`); `NumberFieldRoot.test.tsx` "accepts Persian digit keyboard input". Docs: CHANGELOG.md v1.5.0 "Allow Persian digits in keyboard input".
- **NUM-KEY-07** — MUST. The *allowed non-numeric character set* MUST be derived from `locale`/`format` as follows and used both for keydown gating and change-string validation (NUM-EVT-15): start with `.`, `,`, `．` (U+FF0E), `，` (U+FF0C), `٫` (U+066B), `٬` (U+066C); add the locale's decimal separator (from `formatToParts(11111.1)`, falling back to the plain-locale decimal of `0.1` when the format omits fractions); add every character of every `formatToParts(11111.1)` part whose type is not `integer`, `fraction`, `exponentInteger` or `compact` (group separators, currency symbols/codes, unit strings such as `km/h`, exponent separators, literals, percent/permille signs, minus signs), plus a plain space `" "` whenever such a part contains any Unicode space separator; add `%`, `٪`, `％`, `﹪` when `format.style` is `percent` or (`unit` with `unit: 'percent'`); add `‰`, `؉` when `style` is `percent` or (`unit` with `unit: 'permille'`); always add `+`, `＋`, `﹢`; add `-`, `−`, `－`, `‒`, `–`, `—`, `﹣` when `minWithDefault < 0` or `allowOutOfRange` is `true`. The set is recomputed on every keydown/change (so prop changes take effect immediately).
  Source: `number-field/root/NumberFieldRoot.tsx:152-207`, `number-field/utils/parse.ts:13-14,37-52,69-77`. Test: [L4] `NumberFieldInput.test.tsx` "blocks percent and permille symbols on keydown when not formatted as percent", "allows unicode plus/minus, permille and fullwidth digits on keydown when formatted as percent", "allows space key when locale uses space-like grouping (pl-PL)", "applies locale-aware decimal/group gating (de-DE)"; `NumberFieldRoot.test.tsx` "allows typing a negative value via keyboard when min is 0", "accepts multi-character currency symbols while typing (e.g. pt-BR BRL)", "accepts exponent separators while typing (scientific notation)". Docs: CHANGELOG.md v1.7.0 "Fix keyboard editing with multi-character format symbols", v1.0.0-beta.7 "Fix literal space handling with symbols".
- **NUM-KEY-08** — MUST. When the pressed key is a plus or minus character (any variant) that is in the allowed set, it MUST be let through only if the current text contains no sign of either kind, or the whole text is selected, or the selection covers the index of the existing sign of the same kind (replacing it); otherwise it MUST be blocked.
  Source: `number-field/input/NumberFieldInput.tsx:294-323`. Test: [L4] `NumberFieldInput.test.tsx` "blocks a second minus sign when the existing one is not selected", "allows a minus sign that replaces the selected existing sign", "allows a minus sign when the whole value is selected".
- **NUM-KEY-09** — MUST. When the pressed key equals the locale's decimal separator, currency symbol or percent sign (from `getNumberLocaleDetails(locale, format)`), it MUST be let through only if that symbol is not yet in the text, or the whole text is selected, or the selection covers the symbol's first occurrence; otherwise it MUST be blocked. Group separators are not limited (a second `.` in `de-DE` is allowed).
  Source: `number-field/input/NumberFieldInput.tsx:289-292,325-332`. Test: [L4] `NumberFieldInput.test.tsx` "applies locale-aware decimal/group gating (de-DE)"; `NumberFieldRoot.test.tsx` "allows typing a decimal while replacing a selection".
- **NUM-KEY-10** — MUST. Any other single-character key (not a numeral, not in the allowed set after NUM-KEY-08/09, no bypass modifier) MUST be blocked with `preventDefault()` and `stopPropagation()` and MUST NOT change the value.
  Source: `number-field/input/NumberFieldInput.tsx:338-351,377-379` (falls through to `preventDefault` with neither step nor boundary). Test: [L4] `NumberFieldInput.test.tsx` "should not allow non-numeric characters on keydown", "blocks percent and permille symbols on keydown when not formatted as percent".
- **NUM-KEY-11** — MUST. When the Input is disabled or read-only, or a consumer `onKeyDown` already called `preventDefault()`, the keydown handler MUST do nothing (no gating, no step, no commit).
  Source: `number-field/input/NumberFieldInput.tsx:273-275`. Test: [L4] `NumberFieldInput.test.tsx` "does not step when the keydown default is prevented" (disabled/read-only clause: none; the public `readOnly`/`disabled` docs rely on it).
- **NUM-KEY-12** — MUST. A keyboard step MUST start from the parsed display text when the text is dirty (sync suspended) — so stepping from typed `1.5` yields `2.5` even if the consumer never mirrored `1.5` back — and from the numeric value when the text is synced (never from the rounded display text, so `1.23456` steps to `2.23456`). When starting from the numeric value, the "last changed value" MUST first be reset to the current value so that a canceled step cannot later commit a stale value. If the dirty text does not parse, the step starts from the numeric value (via NUM-STA-17 when `null`).
  Source: `number-field/input/NumberFieldInput.tsx:283,367-373,387-400`. Test: [L4] `NumberFieldInput.test.tsx` "steps keyboard from dirty input text when the controlled value lags onValueChange", "increments with keyboard from numeric state, not rounded display text", "decrements with keyboard from numeric state, not rounded display text", "increments with keyboard from numeric state after a no-edit blur cycle", "does not commit a stale value when a synced keyboard step is canceled after an external change". Docs: CHANGELOG.md v1.6.0 "Fix committed values and keyboard stepping".

### 6.2 Pointer and touch

Three pointer surfaces exist: the stepper buttons (click and press-and-hold), the visible input (wheel), and the scrub area (drag with pointer lock). The press-and-hold repeater itself is the shared mechanism of [UBT](../architecture/button-behavior.md) §6.3 (UBT-PTR-06..19, UBT-ALG-07); only its configuration and what the number field does in `tick`/`onStop`/its own handlers is stated here.

| Gesture | Context (part, pointer type, modifiers, state) | Behavior | Req |
|---|---|---|---|
| primary `pointerdown` on Increment/Decrement | mouse/pen/touch; enabled, not read-only, not default-prevented | sync dirty text (NUM-PTR-05); clear the per-hold result slot; focus the input (non-touch-like pointers only); then the repeater: immediate tick (mouse), or tick after the 50 ms touch settle (touch/pen); repeats after 400 ms every 60 ms | NUM-PTR-02, NUM-PTR-03 |
| repeat tick | hold in progress | step by the amount derived from the *originating* event's modifiers; a tick that applies nothing (boundary, canceled) stops the repeat | NUM-PTR-03 |
| window `pointerup` after a started hold | any | stop; `onValueCommitted(lastChanged ?? current, reason 'increment-press'/'decrement-press')` once | NUM-PTR-04 |
| `mouseleave` / `mouseenter` while pressed | mouse | stop / restart repeat (UBT-PTR-13); still exactly one commit on release | NUM-PTR-04 |
| `click` on Increment/Decrement | keyboard/AT click (`detail === 0`) or a touch tap that did not become a hold | sync dirty text; one step; commit immediately if the stored value changed | NUM-PTR-01 |
| `click` after a mouse `pointerdown` or after a touch hold | `detail !== 0` / hold started | ignored (UBT-PTR-17) | NUM-PTR-01 |
| non-primary button `pointerdown` on a stepper | `event.button !== 0` | ignored entirely (not even the dirty-text sync) | NUM-PTR-02 |
| any pointer on a stepper | disabled or read-only | ignored | NUM-PTR-06 |
| `wheel` over the Input | `allowWheelScrub`, enabled, not read-only, input focused, no Ctrl | `preventDefault`; step by the wheel event's modifiers (Shift → large, Alt → small); `deltaY > 0` → −1, `< 0` → +1; change + commit (`wheel`) synchronously | NUM-PTR-07 |
| horizontal `wheel` (|ΔX| > |ΔY|) without Shift, or zero delta | same | ignored, page scrolls | NUM-PTR-08 |
| horizontal `wheel` with Shift | same | treated as vertical: ΔX used as the delta | NUM-PTR-08 |
| `wheel` with Ctrl, or input not focused | any | ignored (pinch-zoom / page scroll) | NUM-PTR-07 |
| primary `pointerdown` on ScrubArea | mouse/pen/touch; enabled, not read-only | start scrub: `scrubbing` ← true (flushed); mouse: `preventDefault` + focus input; non-touch & non-WebKit: request pointer lock on `<body>`; record touch-ness and lock denial | NUM-PTR-10 |
| `pointermove` (window, capture) while scrubbing | pointer still down | `preventDefault`; move virtual cursor (NUM-PTR-16); accumulate the on-axis movement; once |accumulated| ≥ `pixelSensitivity`, step by `|Δ| × amount` in the sign of Δ (vertical: up = +) with reason `scrub` | NUM-PTR-11, NUM-PTR-12 |
| `pointerup` (window, capture) while scrubbing | any | exit pointer lock; `scrubbing` ← false; `onValueCommitted(lastChanged ?? current, 'scrub')`; if no movement crossed the threshold, synthesize one `click` on the pointerdown target after 0 ms unless a native click already reached the scrub area; on Gecko the whole release is delayed 20 ms | NUM-PTR-13, NUM-PTR-14 |
| single-finger `touchstart` on ScrubArea | enabled, not read-only | `preventDefault` (no page scroll); multi-touch left alone | NUM-PTR-15 |
| `pointerdown` on ScrubArea | non-primary button, disabled, read-only, or default-prevented | ignored | NUM-PTR-19 |

#### Stepper buttons

- **NUM-PTR-01** — MUST. On a `click` that is not default-prevented, not disabled/read-only and not skipped by UBT-PTR-17 (so: keyboard/AT-generated clicks and touch taps that did not start a hold), a stepper MUST (1) sync the dirty text (NUM-PTR-05), (2) request `current + amount × direction` with reason `increment-press`/`decrement-press` (amount from the click's modifiers), and (3) call `onValueCommitted(lastChanged ?? current, { reason, event })` only if that committed value differs from the numeric value before the step.
  Source: `number-field/root/useNumberFieldStepperButton.ts:130-152`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "steps once from defaultValue=0", "only calls onValueChange once per step", "does not step twice with pointerdown and click", "always steps on quick touch ($order, before TOUCH_TIMEOUT)", "fires onValueCommitted once on first soft tap (touch)"; `NumberFieldRoot.test.tsx` "fires when using increment/decrement buttons", "includes the correct reason for increment and decrement button presses". Docs: CHANGELOG.md v1.3.0 "Fix increment/decrement press `reason` values in `onValueCommitted`".
- **NUM-PTR-02** — MUST. On a primary-button `pointerdown` that is not default-prevented while enabled and not read-only, a stepper MUST, in order: sync the dirty text (NUM-PTR-05); reset the per-hold result slot ("last changed value") to `null` so a release after a no-op/canceled first tick commits the current value rather than an earlier interaction's; focus the visible input (caret at end, NUM-FOC-02) **only** when the pointer type is neither `touch` nor `pen` (UBT-PTR-20); then hand the event to the press-and-hold repeater (UBT-PTR-06/09). A `pointerdown` with `button !== 0` MUST be ignored entirely.
  Source: `number-field/root/useNumberFieldStepperButton.ts:153-170`, `internals/usePressAndHold.ts:188-223`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "steps when input is dirty but not blurred (pointerdown)", "ignores non-primary pointer buttons", "treats pen pointer as touch-like", "does not commit a stale value when the first hold tick is canceled after dirty input"; `NumberFieldIncrement.test.tsx` "places the caret at the end of the input when a mouse press focuses it", "starts the hold once a touch press settles and ignores the compatibility click", "does not start the hold when the touch moves like a scroll", "treats a touch press with several small moves as a tap". Docs: CHANGELOG.md v1.2.0 "Fix pen pointer handling", v1.0.0-alpha.5 "Correctly handle quick touches".
- **NUM-PTR-03** — MUST. The repeater's `tick` MUST request `current + amount × direction` (amount from the *originating* pointerdown/mouseenter event's modifiers, UBT-EVT-10; reason `increment-press`/`decrement-press`, `direction` set) and return whether the request was applied; the repeater (UBT-PTR-06/07) therefore ticks once immediately, again after 400 ms, then every 60 ms, and stops repeating as soon as a tick applies nothing (value at the boundary, or change canceled) — `onValueChange` is then no longer called.
  Source: `number-field/root/useNumberFieldStepperButton.ts:101-111`, `number-field/utils/constants.ts:1-2`, `internals/usePressAndHold.ts:150-161`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "steps continuously when holding pointerdown", "stops calling onValueChange once the boundary is reached", "stops stepping after mouseleave", "starts stepping again after mouseleave then mouseenter", "does not start stepping again after mouseleave then mouseenter after pointerup"; `NumberFieldRoot.test.tsx` "should increment the value by the default `largeStep` prop of 10 while holding the shift key". Docs: CHANGELOG.md v1.2.0 "Stop repeat change at bounds".
- **NUM-PTR-04** — MUST. When the window `pointerup` that ends a started hold fires (UBT-PTR-12 — once per press, also when the hold auto-stopped at the boundary, also after mouseleave/mouseenter cycles, and also when an ancestor stops `pointerup` propagation, via the local `mouseup` fallback UBT-PTR-14 plus the window listener), the stepper MUST call `onValueCommitted(lastChanged ?? currentValue, { reason: 'increment-press' | 'decrement-press', event: pointerupEvent })` exactly once.
  Source: `number-field/root/useNumberFieldStepperButton.ts:112-117`, `internals/usePressAndHold.ts:134-148`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "commits on release after a hold reaches the boundary", "removes the global release listener when unmounted during a hold"; `NumberFieldRoot.test.tsx` "fires once per press-release even after mouseleave/mouseenter during a hold"; `NumberFieldIncrement.test.tsx` "stops the hold on release when an ancestor stops pointerup propagation".
- **NUM-PTR-05** — MUST. *Dirty-text sync* (run at the start of every stepper `pointerdown` and `click`): mark sync allowed; if the text was already synced, set the "last changed value" to the current numeric value and stop; otherwise parse the display text (NUM-I18N-02) and, if it parses, request it with the button's press reason and *no* direction (so it is clamped but never snapped — no spurious intermediate value), and, if that request was not canceled, use the parsed value as the base for the step that follows. A canceled sync leaves the typed text in place and the following step starts from the stored value.
  Source: `number-field/root/useNumberFieldStepperButton.ts:70-99`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "steps when input is dirty but not blurred (click)", "does not step or commit when the dirty-input sync is canceled", "does not emit a snapped intermediate when committing dirty text before a step", "does not commit a stale value when a synced step is canceled after an external change", "steps from numeric state after typed precision is formatted on blur"; `NumberFieldInput.test.tsx` "keeps dirty-input authority after a non-mutating key so a button step uses dirty text". Docs: CHANGELOG.md v1.5.0 "Sync pasted input during step interactions".
- **NUM-PTR-06** — MUST. While the button's effective disabled (NUM-STA-20) or `readOnly` is `true`, `pointerdown` and `click` on a stepper MUST change nothing and MUST NOT start a hold; a hold already in progress MUST stop as soon as the button becomes disabled, and the compatibility click of a touch press that was interrupted by disabling MUST be ignored (UBT-PTR-16).
  Source: `number-field/root/useNumberFieldStepperButton.ts:101-102,131-134,154-156`, `internals/usePressAndHold.ts:172-179`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "does not step when readOnly", "does not step when root is disabled", "does not step when the button is disabled", "cancels an active mouse press-and-hold interaction when disabled", "cancels the compatibility click from a touch press when disabled". Docs: CHANGELOG.md v1.8.0 "Stop incrementing when disabled during press-and-hold".

#### Wheel

- **NUM-PTR-07** — MUST. When `allowWheelScrub` is `true` and the root is neither disabled nor read-only, a native non-passive `wheel` listener on the visible input MUST, for events where `ctrlKey` is `false` and the input is the active element of its document: call `preventDefault()`, mark sync allowed, and request `current + amount × direction` with reason `wheel`, where `amount` comes from the wheel event's modifiers (NUM-STA-16) and `direction` is `−1` when the effective delta is positive (scrolling down) and `+1` when negative; if applied, `onValueCommitted(storedValue, { reason: 'wheel', event })` MUST follow synchronously. Events with `ctrlKey` or while the input is not focused MUST be ignored (no `preventDefault`).
  Source: `number-field/root/NumberFieldRoot.tsx:361-424`. Test: [L4] `NumberFieldRoot.test.tsx` "should allow the user to scrub the input value with the mouse wheel", "does not scrub on wheel while pinch-zooming (ctrlKey)", "does not scrub on wheel when the input is not focused", "does not scrub on wheel when disabled", "does not scrub on wheel when readOnly", "calls onValueChange and onValueCommitted on wheel", "does not commit when a wheel step is a no-op at the boundary", "syncs the visible input value when using the mouse wheel after pasting", "uses largeStep when shift is held during wheel", "includes the reason for wheel scrubbing".
- **NUM-PTR-08** — MUST. The effective delta MUST be `deltaX` when `shiftKey` is set and `|deltaX| > |deltaY|`, otherwise `deltaY`. The event MUST be ignored (not prevented, no change) when the effective delta is `0`, or when `|deltaX| > |deltaY|` and `shiftKey` is not set — so horizontal gestures (including ones with sub-pixel vertical noise) scroll the page, while Shift+wheel works on browsers that swap the axis.
  Source: `number-field/root/NumberFieldRoot.tsx:379-389`. Test: [L4] `NumberFieldRoot.test.tsx` "does not scrub on a horizontal wheel event, and lets it scroll the page", "scrubs on a vertical wheel event that carries horizontal noise", "uses largeStep when shift is held and the browser swaps the wheel axis". Docs: CHANGELOG.md v1.8.0 "Prevent scrubbing on horizontal wheel events".
- **NUM-PTR-09** — MUST. The wheel listener MUST be attached directly to the input element as a non-passive listener (so `preventDefault` suppresses page scroll) after paint, and detached whenever `allowWheelScrub` becomes `false`, the root becomes disabled or read-only, or the input unmounts.
  Source: `number-field/root/NumberFieldRoot.tsx:361-368,412-423`. Test: [L4] `NumberFieldRoot.test.tsx` "scrubs on a vertical wheel event that carries horizontal noise" (`fireEvent.wheel` returns `false`).

#### Scrub area

- **NUM-PTR-10** — MUST. On a primary-button `pointerdown` on the scrub area that is not default-prevented while enabled and not read-only, the scrub area MUST: record whether the pointer is `touch`; for `mouse` pointers call `preventDefault()` and focus the visible input (NUM-FOC-04); mark itself scrubbing (flag used by the move handler), clear the "moved" flag and the "native click received" flag, cancel any pending synthetic click, remember the event target; set its own and the root's `scrubbing` state to `true` synchronously (flushed before continuing) and position the virtual cursor at the pointer (NUM-PTR-16); then, unless the pointer is touch or the engine is WebKit (`platform.engine.webkit`, UTL-EDGE-01), request pointer lock on the owning document's `<body>`, recording denial (a thrown error or rejected promise) as "pointer lock denied" and success as not denied; after the request settles, if still scrubbing, re-emit the scrubbing state so the cursor reflects the result. Server: relaxed, see §9.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:127-148,317-356`. Test: [L4] `NumberFieldScrubArea.test.tsx` "ignores non-primary pointer buttons", "does not start scrubbing when the field is read-only", "keeps a selection the consumer sets in onFocus when a mouse press focuses the input"; `NumberFieldScrubAreaCursor.test.tsx` "renders when using mouse input", "does not render when using touch input", "handles pointer lock denial through requestPointerLock API", "does not render after a quick tap when pointer lock resolves later". Docs: CHANGELOG.md v1.0.0-alpha.7 "Fix ScrubArea on Safari".
- **NUM-PTR-11** — MUST. While scrubbing, each window `pointermove` MUST add `movementY` (vertical) or `movementX` (horizontal) to an accumulator that starts at `0` when the scrub's listeners are attached; when `|accumulator| ≥ pixelSensitivity` the accumulator MUST reset to `0`, the "moved" flag MUST be set, and with `d = −movementY` (vertical, so upward movement increases) or `movementX` (horizontal) and `raw = d × amount` (amount from the move event's modifiers, NUM-STA-16): if `raw ≠ 0`, mark sync allowed and request `current + |raw| × sign(raw)` with reason `scrub` and `direction = sign(raw)` (`+1` when `raw ≥ 0`). Movement on the other axis MUST never change the value. (With the default `pixelSensitivity` of 2 and `step` 1, a 10 px drag changes the value by 10.)
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:157,209-242`. Test: [L3] `NumberFieldScrubArea.test.tsx` "should increment or decrement the value when scrubbing with the pointer", "should only increment if the pointer movement was greater than or equal to the value", "does not change the value for a zero-distance move when sensitivity is 0", "scrubs on vertical movement when direction is vertical", "should only scrub if the pointer moved in the given direction", "syncs the visible input value when scrubbing after pasting", "calls onValueChange while scrubbing and onValueCommitted on pointerup" (L4 for the end-to-end value assertions).
- **NUM-PTR-12** — MUST. The move handler MUST call `preventDefault()` on every `pointermove` it processes (suppressing text selection), and MUST ignore moves while the scrub area's own "pointer is down" flag is `false` — even if its `scrubbing` state is still `true` because its listeners were torn down and re-attached without a pointer release (e.g. the subtree was hidden and revealed).
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:209-220`. Test: [L3] `NumberFieldScrubArea.test.tsx` "ignores pointer movement once the scrub has ended", "does not resume scrubbing when a hidden scrub area is revealed again".
- **NUM-PTR-13** — MUST. On the window `pointerup` that ends a scrub, the scrub area MUST: exit pointer lock on the owning document (errors ignored); clear its "pointer is down" flag; set its own and the root's `scrubbing` state to `false` (flushed); call `onValueCommitted(lastChanged ?? currentValue, { reason: 'scrub', event: pointerupEvent })` exactly once (even when no value changed); then run the synthetic-click logic (NUM-PTR-14) and clear the "moved" flag and remembered target. On Gecko (`platform.engine.gecko`, UTL-EDGE-02) this whole release handler MUST run 20 ms later instead of immediately (the lock would otherwise not release on a soft click); the delayed timer MUST be cancelled if the listeners are torn down first.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:159-207,250-253`. Test: [L4] `NumberFieldScrubArea.test.tsx` "calls onValueChange while scrubbing and onValueCommitted on pointerup", "tracks visual viewport scale and removes scrub listeners after pointer lock exits"; `NumberFieldScrubArea.gecko.test.tsx` "delays releasing the pointer lock so soft clicks are not swallowed". Docs: CHANGELOG.md v1.0.0-rc.0 "Fix pointer lock release when soft clicking in Firefox".
- **NUM-PTR-14** — MUST. If the "moved" flag is still `false` at release (the pointer never crossed `pixelSensitivity`) and a pointerdown target is remembered, the scrub area MUST start a 0 ms timer that dispatches a synthetic bubbling, cancelable `click` (a `MouseEvent` of the input's window) on the remembered pointerdown target **unless** a native `click` has meanwhile reached the scrub area (tracked by a capture-phase click handler on the scrub area). The net effect MUST be exactly one `click` per tap, whether or not pointer lock redirected the native click; clicks on child elements (e.g. a `<label>`) MUST reach both the child and the scrub area once.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:66,172-197,314-316,332-334`. Test: [L4] `NumberFieldScrubArea.test.tsx` "should fire onClick once when clicked without scrubbing", "should fire onClick on child elements". Docs: CHANGELOG.md v1.9.0 "Fire `<NumberField.ScrubArea>`'s `onClick` once per click", v1.2.0 "Fix click handlers on ScrubArea", v1.0.0-beta.4 "Fix stuck virtual cursor after mouse tap".
- **NUM-PTR-15** — MUST. While enabled and not read-only, the scrub area MUST attach a native `touchstart` listener that calls `preventDefault()` when exactly one touch point is active, and leave multi-touch gestures (pinch-zoom) untouched.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:292-309`. Test: [L3] `NumberFieldScrubArea.test.tsx` "blocks scrolling for a single-touch scrub", "leaves multi-touch gestures such as pinch-zoom to the browser".
- **NUM-PTR-16** — MUST. The virtual cursor position MUST be initialized at scrub start to `(clientX − w/2, clientY − h/2)` where `w`/`h` are the cursor element's `offsetWidth`/`offsetHeight`, and on every processed `pointermove` updated to `(round(x + movementX), round(y + movementY))`, then *wrapped* per axis against the bounds rectangle `R`: if `coord + half < R.low` the coordinate becomes `R.high − half`; if `coord + half > R.high` it becomes `R.low − half`. `R` MUST be the scrub area's bounding client rect expanded by `teleportDistance / 2` on every side when `teleportDistance` is defined; otherwise the visual viewport (`offsetLeft`, `offsetTop`, `offsetLeft + width`, `offsetTop + height`); otherwise (no visual viewport) `(0, 0, documentElement.clientWidth, documentElement.clientHeight)`. The resulting position is written as the cursor's transform (NUM-DOM-13).
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:84-125,139-146`, `number-field/utils/getViewportRect.ts:4-34`. Test: [L3] `NumberFieldScrubArea.test.tsx` "wraps the virtual cursor around the allowed bounds"; `getViewportRect.test.ts` "pads the element rect by half the teleport distance", "collapses to the element rect for a zero teleport distance", "uses the visual viewport when no teleport distance is set", "falls back to the document element without a visual viewport". Docs: CHANGELOG.md v1.0.0-alpha.7 "Correct virtual cursor rendering", "Prevent virtual cursor overlapping native one".
- **NUM-PTR-17** — MUST. If a scrub area is removed from the DOM while its pointer is down, it MUST clear its "pointer is down" flag, set the root's `scrubbing` state to `false` and exit pointer lock (errors ignored), without committing (no pointer release occurred).
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:275-290`. Test: [L4] `NumberFieldScrubArea.test.tsx` "clears the root scrubbing state when the scrub area unmounts mid-scrub".
- **NUM-PTR-18** — MUST. The `pointermove` and `pointerup` listeners MUST be registered on the input's window in the capture phase only while the scrub area's `scrubbing` state is `true` (and the root is enabled and not read-only), and removed as soon as it becomes `false` or the scrub area unmounts; the movement accumulator restarts at `0` on every registration.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:150-157,244-254`. Test: [L3] `NumberFieldScrubArea.test.tsx` "tracks visual viewport scale and removes scrub listeners after pointer lock exits" (`removeEventListener('pointerup', …, true)`).
- **NUM-PTR-19** — MUST. A `pointerdown` on the scrub area MUST be ignored (no scrubbing state, no focus, no pointer lock) when it is default-prevented, when `button !== 0`, or while the root is disabled or read-only; the touch-scroll suppression (NUM-PTR-15) is likewise inactive while disabled or read-only. Server: relaxed, see §9.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:296,318-320`. Test: [L4] `NumberFieldScrubArea.test.tsx` "ignores non-primary pointer buttons", "does not start scrubbing when the field is read-only".
- **NUM-PTR-20** — MUST. Scrub direction MUST NOT depend on the reading direction: in a right-to-left document, moving the pointer right still increases the value.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:224-236` (no direction lookup). Test: none.

### 6.3 Focus management

- **NUM-FOC-01** — MUST. Focusing the hidden number input (e.g. a form's "focus first invalid control", a label click, or script) MUST immediately move focus to the visible input via NUM-FOC-02; the hidden input never keeps focus.
  Source: `number-field/root/NumberFieldRoot.tsx:501-504`. Test: [L4] `NumberFieldRoot.test.tsx` "forwards focus to the visible input", "places the caret at the end of the visible input when forwarding focus", "focuses the input when the field receives an error from Form". Docs: CHANGELOG.md v1.1.0 "Fix hidden input focus on submit".
- **NUM-FOC-02** — MUST. Programmatic focusing by the component (hidden-input focus redirection, stepper pointerdown, scrub-area mouse pointerdown) MUST first set the visible input's selection to a collapsed caret at the end of its text and then call `focus()`, so that every engine restores that caret; a selection the consumer sets in its own `onFocus` handler MUST still win (the handler runs after the caret was stored). Keyboard (Tab) and pointer focus MUST NOT alter the browser's native selection behavior.
  Source: `number-field/root/NumberFieldRoot.tsx:347-359`. Test: [L4] `NumberFieldRoot.test.tsx` "places the caret at the end of the visible input when forwarding focus", "keeps a selection the consumer sets in onFocus when forwarding focus"; `NumberFieldInput.test.tsx` "keeps the selection the browser set when focus moves into the input"; `NumberFieldIncrement.test.tsx` "places the caret at the end of the input when a mouse press focuses it"; `NumberFieldScrubArea.test.tsx` "keeps a selection the consumer sets in onFocus when a mouse press focuses the input". Docs: CHANGELOG.md v1.8.0 "Preserve native and consumer-controlled selection when focusing the input".
- **NUM-FOC-03** — MUST. A primary `pointerdown` on a stepper button MUST focus the visible input (NUM-FOC-02) when the pointer type is `mouse` (or anything other than `touch`/`pen`), and MUST NOT move focus for `touch` or `pen` pointers (no software keyboard on tap). Because the buttons have `tabindex="-1"`, they are never in the Tab order (NUM-FOC-06).
  Source: `number-field/root/useNumberFieldStepperButton.ts:164-167`, `internals/usePressAndHold.ts:19-21`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "treats pen pointer as touch-like"; `NumberFieldIncrement.test.tsx` "places the caret at the end of the input when a mouse press focuses it".
- **NUM-FOC-04** — MUST. A primary `pointerdown` on the scrub area with `pointerType === 'mouse'` MUST call `preventDefault()` (so the scrub area itself never takes focus or starts a selection) and focus the visible input (NUM-FOC-02); touch and pen pointers MUST NOT move focus.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:325-328`. Test: [L4] `NumberFieldScrubArea.test.tsx` "keeps a selection the consumer sets in onFocus when a mouse press focuses the input".
- **NUM-FOC-05** — MUST. Within a `Field.Root`, the field's `focused` state MUST be set to `true` on the visible input's `focus` event unless the event is default-prevented or the root is disabled (read-only inputs do track focus), and to `false` on its `blur` event (unless default-prevented or disabled); the field MUST also release `focused` when the focused input becomes disabled or unmounts without a blur event, and reclaim it on mount if the input is already the active element (FFI-ALG-13).
  Source: `number-field/input/NumberFieldInput.tsx:86,131-145`, `internals/field-root-context/useSetFieldFocused.ts:15-46`. Test: [L4] `NumberFieldRoot.test.tsx` "[data-filled]" (focus/blur), "adds [data-focused] attribute on every focus", "keeps focus state in sync on a readOnly field", "is removed when the focused input becomes disabled", "is removed when the focused input unmounts"; `NumberFieldInput.test.tsx` "does not mark the field focused when the focus default is prevented". Docs: CHANGELOG.md v1.1.0 "Fix Field `data-focused` state".
- **NUM-FOC-06** — MUST. Stepper buttons MUST render `tabindex="-1"` (never Tab-reachable) and the button mechanism MUST be configured `focusableWhenDisabled: true` (UBT-FOC-03); because the part also emits the native `disabled` attribute (NUM-DOM-10), a default `<button>` is nevertheless not focusable while effectively disabled, and only a `nativeButton={false}` substitute remains programmatically focusable while disabled (see §16 Q3).
  Source: `number-field/root/useNumberFieldStepperButton.ts:124-127,179`. Test: none (attribute asserted via conformance; see §16).

### 6.4 Dismissal

Not applicable — the number field has no popup, overlay or dismissible state; blur is covered as a commit boundary in §7 (NUM-EVT-06..08).

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: number \| null, details: ChangeEventDetails) => void` | Synchronously inside the originating input handling, before the value is stored, whenever NUM-STA-18's reporting rule holds | `input-change`, `input-clear`, `input-blur`, `input-paste`, `keyboard`, `increment-press`, `decrement-press`, `wheel`, `scrub`, `none` | yes (`details.cancel()`) |
| `onValueCommitted` | `(value: number \| null, details: CommitEventDetails) => void` | After an applied change, at the interaction's commit point (NUM-EVT-05) | `input-blur`, `input-clear`, `keyboard`, `increment-press`, `decrement-press`, `wheel`, `scrub` (`none` declared, never emitted) | no |

Consumer DOM handlers on every part (`onFocus`, `onBlur`, `onChange`, `onKeyDown`, `onPaste`, `onClick`, `onPointerDown`, …) are merged per RND-EVT-02 and may veto the built-in handler with `preventBaseUIHandler()` (EVD-EVT-26); the Input's built-in handlers additionally honor a plain `preventDefault()` (NUM-EVT-14).

#### `onValueChange`

- **NUM-EVT-01** — MUST. `onValueChange(validatedValue, details)` MUST be invoked synchronously, in the same task as the originating event and before the numeric value or display text is updated, exactly once per reported request (NUM-STA-18), with `validatedValue` already snapped/clamped/rounded (NUM-STA-07) — never the raw typed number.
  Source: `number-field/root/NumberFieldRoot.tsx:232-252`. Test: [L2] `NumberFieldRoot.test.tsx` "should be called when the value changes", "prevents the raw value from going below the `min` prop" (reports `5` for typed `4`), "fires onValueChange for each parseable change while typing". Docs: CHANGELOG.md v1.0.0-beta.1 "Ensure `onValueChange` is called with already-formatted parsed value", v1.0.0-beta.3 "Fix duplicate `onValueChange` calls".
- **NUM-EVT-02** — MUST. `details.reason` MUST be: `input-change` for a change-event whose text parses; `input-clear` for a change-event whose text is empty/whitespace, and for blur of an empty input; `input-blur` for the value committed when blurring non-empty text that differs from the stored value; `input-paste` for a paste that parses; `keyboard` for ArrowUp/ArrowDown/Home/End; `increment-press`/`decrement-press` for every change caused by the respective button (click, hold ticks, and the dirty-text sync that precedes them); `wheel` for wheel steps; `scrub` for scrub-area drags; `none` for browser autofill of the hidden input. `details.event` MUST be the originating native event (EVD-API-05).
  Source: `number-field/input/NumberFieldInput.tsx:157,204,242,269,381-402,452`, `number-field/root/useNumberFieldStepperButton.ts:66-68,90,109,145`, `number-field/root/NumberFieldRoot.tsx:402,514`, `number-field/scrub-area/NumberFieldScrubArea.tsx:238`. Test: [L2] `NumberFieldRoot.test.tsx` "includes the reason for parseable typing", "includes the reason when clearing the value", "includes the reason for keyboard increments", "includes the reason for increment button presses", "includes the reason for decrement button presses", "includes the reason for wheel scrubbing", "pastes %s numerals through the input contract" (`input-paste`); `NumberFieldRoot.spec.tsx` (event types). Docs: CHANGELOG.md v1.0.0-beta.5 "Granular change reasons".
- **NUM-EVT-03** — MUST. For step-based requests (`keyboard` arrows, button steps and hold ticks, `wheel`, `scrub`) `details.direction` MUST be `1` or `-1`; it MUST be absent (`undefined`) for direct entry (`input-*`, `none`), for Home/End, for the dirty-text sync before a button step, and for the "seed an empty field with 0" step (NUM-STA-17).
  Source: `number-field/root/NumberFieldRoot.tsx:281-300`, `number-field/utils/types.ts:12-14`. Test: none (covered indirectly by snapping tests: `useNumberFieldStepperButton.test.tsx` "seeds an empty field in range without directional snapping", "does not emit a snapped intermediate when committing dirty text before a step").
- **NUM-EVT-04** — MUST. When the consumer calls `details.cancel()` inside `onValueChange`: the value MUST NOT be stored (component-owned mode), the field's `dirty` MUST NOT be updated, no "pending commit" MUST be recorded, the display text MUST NOT be re-formatted, the "last changed value" MUST NOT be updated, and the request MUST be reported as *not applied* to its caller — so no `onValueCommitted` follows (NUM-EVT-05), a hold stops repeating (NUM-PTR-03), and a blur leaves the typed text as is (NUM-EVT-07). In consumer-owned mode the consumer must itself skip its state update (EVD-EVT-06).
  Source: `number-field/root/NumberFieldRoot.tsx:251-257`, `number-field/input/NumberFieldInput.tsx:160-162,207-210`, `number-field/root/useNumberFieldStepperButton.ts:93-97`. Test: [L2] `NumberFieldRoot.test.tsx` "does not commit a canceled keyboard change", "does not commit a canceled clear on blur"; `NumberFieldInput.test.tsx` "should not commit a canceled blur change"; `useNumberFieldStepperButton.test.tsx` "does not step or commit when the dirty-input sync is canceled".
- **NUM-EVT-13** — MUST. For direct-entry reasons, `onValueChange` MUST also fire when the validated value equals the current value, provided the unvalidated request differed from it (typed `1000` clamped to a current `10`; typed `4` clamped to `min` 5) or the text was dirty (a paste that splices an empty string re-reports the current value with reason `input-paste`); step-based reasons MUST NOT fire when the validated value is unchanged (boundary no-ops).
  Source: `number-field/root/NumberFieldRoot.tsx:244-251`. Test: [L2] `NumberFieldRoot.test.tsx` "prevents the value from going above the `max` prop", "does not commit when a wheel step is a no-op at the boundary"; `NumberFieldInput.test.tsx` "reports an unchanged value once when a paste event carries no clipboard data"; `useNumberFieldStepperButton.test.tsx` "stops calling onValueChange once the boundary is reached".
- **NUM-EVT-12** — MUST. `onValueChange` MUST NOT be invoked when the consumer changes the `value` prop (EVD-STA-06); such a change only re-formats the display (when sync is allowed) and re-runs field validation (NUM-FRM-08).
  Source: `number-field/root/NumberFieldRoot.tsx:312-324`, `number-field/input/NumberFieldInput.tsx:103-112`. Test: [L2] `NumberFieldInput.test.tsx` "should preserve default formatting on first blur after external value change" (`onValueChange` not called), "should handle multiple blur cycles with default formatting".
- **NUM-EVT-15** — MUST. On every `change` event of the visible input (not default-prevented): mark sync suspended; if the new text is empty or whitespace-only, display it and request `null` with reason `input-clear`; otherwise, if every character is a numeral (NUM-KEY-06), a minus variant, a member of the allowed set (NUM-KEY-07) or a Unicode format-control character (`\p{Cf}`), display the text and — only if it parses (NUM-I18N-02) — request the parsed number with reason `input-change`; if any character is not allowed, neither the display text nor the value MUST change (the input is reverted to the previous display text). Unparseable-but-allowed partials (`-`, `.`, `12.a`? — no: `a` is disallowed; `1,` in `de-DE`) keep the text and fire nothing.
  Source: `number-field/input/NumberFieldInput.tsx:231-271`. Test: [L2] `NumberFieldInput.test.tsx` "should not allow non-numeric characters on change", "should allow numeric characters on change", "ignores a change whose native default was already prevented"; `NumberFieldRoot.test.tsx` "fires onValueChange for each parseable change while typing", "does not fire onValueChange for non-numeric composition/partial input", "handles sign and decimal partials vs. parseable numbers", "accepts grouping while typing and parses progressively", "respects locale decimal separator while typing (de-DE)", "ignores bidi/format control characters in the value (e.g. RTL exponent signs)", "treats parentheses negatives as invalid input", "ignores percent and permille symbols when not formatted as percent", "should be `null` when the input is empty but not trimmed".
- **NUM-EVT-16** — MUST. On a `paste` event of the visible input (not default-prevented, enabled, not read-only): read `text/plain` from the clipboard (`""` when the clipboard object is missing); if reading throws, log NUM-EDGE-03 and let the browser's default paste proceed; otherwise `preventDefault()` (so no `change` event follows), splice the text into the display text at the current selection (replacing the selection), and — only if the spliced text parses — mark sync suspended, request the parsed number with reason `input-paste`, display the spliced text and restore the caret to just after the pasted text (post-commit, pre-paint). If the spliced text does not parse, nothing changes.
  Source: `number-field/input/NumberFieldInput.tsx:88-101,411-455`. Test: [L4] `NumberFieldRoot.test.tsx` "inserts pasted text at the caret instead of replacing the whole value", "replaces the selected range when pasting over a selection", "keeps the caret just after the pasted text", "ignores a paste that does not parse to a number", "does not paste into a readOnly field", "rejects invalid pasted characters without changing the value contract", "pastes %s numerals through the input contract", "should allow pasting a valid number", "should not allow pasting an invalid number"; `NumberFieldInput.test.tsx` "reports an unchanged value once when a paste event carries no clipboard data". Docs: CHANGELOG.md v1.6.0 "Handle unreadable clipboard paste".

#### `onValueCommitted`

- **NUM-EVT-05** — MUST. `onValueCommitted` MUST fire, carrying the stored (validated) value and generic details `{ reason, event }`, at exactly these moments (cf. EVD-EVT-23): synchronously after `onValueChange` for `keyboard` and `wheel`, only when that request was applied (not canceled, value actually changed); immediately after a click step that changed the value (NUM-PTR-01); on the window `pointerup` ending a button hold (`increment-press`/`decrement-press`) on *every* release of a started hold, even when no tick changed the value or the first tick was canceled (NUM-PTR-04); on *every* pointer release ending a scrub (`scrub`), even when no value changed (NUM-PTR-13); and on blur (`input-blur`/`input-clear`) under the conditions of NUM-EVT-06..08, which include unchanged values after manual typing. It MUST NOT fire while typing.
  Source: `number-field/input/NumberFieldInput.tsx:166-171,197-223,381,407-409`, `number-field/root/NumberFieldRoot.tsx:397-409`, `number-field/root/useNumberFieldStepperButton.ts:112-117,148-151`, `number-field/scrub-area/NumberFieldScrubArea.tsx:168-171`. Test: [L2] `NumberFieldRoot.test.tsx` "fires on blur with committed numeric value", "fires on keyboard interactions (ArrowUp/Down/Home/End)", "fires when using increment/decrement buttons", "calls onValueChange and onValueCommitted on wheel", "fires onValueChange for each parseable change while typing" (`onValueCommitted` not called); `NumberFieldScrubArea.test.tsx` "calls onValueChange while scrubbing and onValueCommitted on pointerup". Docs: CHANGELOG.md v1.0.0-beta.4 "Align value changes with `Slider`. An `onValueCommitted` callback has been added."
- **NUM-EVT-06** — MUST. On blur of the visible input (not default-prevented, enabled, not read-only) with non-empty text: mark sync allowed; parse the text; if it does not parse, stop (no change, no commit, text left as typed — NUM-EDGE-05). Otherwise compute `committed` = the current numeric value when the text was *not* manually edited and `format` has no rounding options (so a no-edit focus/blur never re-parses the rounded display and never changes precision); else `removeFloatingPointErrors(parsed, format)` when rounding options exist (NUM-STA-12); else `parsed`. If `committed !== value`, request it with reason `input-blur` (a canceled request stops everything: no commit, no re-format); the committed value becomes the stored (clamped) value. Then, in `validationMode: 'onBlur'`, commit field validation with that value (NUM-FRM-09); then fire `onValueCommitted(committedValue, { reason: 'input-blur', event })` if the text was manually edited, or the value changed, or a programmatic change is pending (NUM-EVT-07); finally replace the display text with `formatNumber(committedValue, locale, format)` if it differs.
  Source: `number-field/input/NumberFieldInput.tsx:139-155,174-230`. Test: [L2] `NumberFieldInput.test.tsx` "commits formatted value only on blur", "should commit validated number on blur (min)", "should commit validated number on blur (max)", "should not snap number to step on blur", "should preserve default formatting on first blur after external value change", "should format to default canonical representation on blur", "should round to explicit maximumFractionDigits on blur", "should respect roundingMode when rounding to explicit maximumFractionDigits on blur in %s", "should commit roundingIncrement values on blur", "should commit significant digit rounded values on blur", "should commit the clamped value when blur rounding crosses a boundary", "should preserve values typed with more than 15 significant digits", "keeps full numeric precision across no-edit focus/blur cycles and subsequent stepping", "commits parsed value on blur and normalizes display for fr-FR", "should not commit values that overflow while parsing on blur"; `NumberFieldRoot.test.tsx` "reports and displays the clamped value on blur, not the raw input", "parses an interleaved percent sign while typing (1%2 -> 12%)". Docs: CHANGELOG.md v1.6.0 "Respect Intl rounding options on blur", "Preserve numeric precision while keeping default display formatting", v1.4.0 "Fix format rounding error on blur".
- **NUM-EVT-07** — MUST. A *pending programmatic change* flag MUST be set whenever a change is applied and cleared whenever `onValueCommitted` fires; blur MUST commit (with `input-blur`, or `input-clear` for empty text) when this flag is set even if the user never typed — so a value changed by a hold whose release was never observed, or by any other uncommitted path, is still committed on blur — and MUST NOT commit again values already committed (keyboard/wheel steps).
  Source: `number-field/root/NumberFieldRoot.tsx:133-140,261`, `number-field/input/NumberFieldInput.tsx:152,168,199`. Test: [L2] `NumberFieldRoot.test.tsx` "calls onValueChange and onValueCommitted on wheel" ("Blur doesn't commit again").
- **NUM-EVT-08** — MUST. On blur with empty/whitespace text: request `null` with reason `input-clear` (stop if canceled); in `'onBlur'` mode commit validation with `null`; then fire `onValueCommitted(null, { reason: 'input-clear', event })` only if the text was manually edited, a programmatic change is pending, or the value was not already `null`. Blurring an untouched empty field MUST NOT commit. Blurring with no edits, no pending change and unchanged value MUST NOT fire `onValueCommitted` either.
  Source: `number-field/input/NumberFieldInput.tsx:156-172`. Test: [L2] `NumberFieldRoot.test.tsx` "fires null on blur when input is cleared", "does not fire when blurring an untouched empty field", "does not commit a canceled clear on blur"; `NumberFieldInput.test.tsx` "keeps full numeric precision across no-edit focus/blur cycles and subsequent stepping" (second blur does not commit).
- **NUM-EVT-09** — MUST. The value passed to `onValueCommitted` MUST always be the stored value after validation (clamped/snapped/rounded), never the pre-validation request: for keyboard, wheel, button and scrub commits it is the "last changed value" recorded by the most recent applied change in that interaction (falling back to the current value when the interaction applied nothing), and for blur it is the value after NUM-EVT-06.
  Source: `number-field/root/NumberFieldRoot.tsx:264`, `number-field/input/NumberFieldInput.tsx:201-211,405-408`, `number-field/root/useNumberFieldStepperButton.ts:115,148`, `number-field/scrub-area/NumberFieldScrubArea.tsx:169`. Test: [L2] `NumberFieldRoot.test.tsx` "reports and displays the min-clamped value on blur, not the raw input", "is validated with clamped value when validationMode=onBlur"; `useNumberFieldStepperButton.test.tsx` "commits on release after a hold reaches the boundary".
- **NUM-EVT-10** — MUST. When the first tick of a hold (or a click step) is canceled or a no-op, the release MUST still commit — with the *current* value, not a value left over from an earlier interaction (the per-hold result slot is cleared at pointerdown, and the synced-path sync refreshes it to the current value); after an external value change, a canceled synced step MUST NOT commit the pre-change value.
  Source: `number-field/root/useNumberFieldStepperButton.ts:74-80,112-117,160-162`. Test: [L2] `useNumberFieldStepperButton.test.tsx` "does not commit a stale value when the first hold tick is canceled after dirty input", "does not commit a stale value when a synced step is canceled after an external change"; `NumberFieldInput.test.tsx` "does not commit a stale value when a synced keyboard step is canceled after an external change".
- **NUM-EVT-11** — MUST. `details.event` on commit details MUST be the native event that ended the interaction: the `KeyboardEvent` (keyboard), `WheelEvent` (wheel), window `PointerEvent` of the release (button hold, scrub), the click `MouseEvent`/`PointerEvent` (button click), or the `FocusEvent` of the blur (`input-blur`, `input-clear`).
  Source: `number-field/input/NumberFieldInput.tsx:157,169,197,381`, `number-field/root/NumberFieldRoot.tsx:407`, `number-field/root/useNumberFieldStepperButton.ts:116,150`, `number-field/scrub-area/NumberFieldScrubArea.tsx:170`. Test: [L2] `NumberFieldRoot.spec.tsx` (commit event narrowing).
- **NUM-EVT-14** — MUST. The Input's built-in `focus`, `blur`, `keydown` and `paste` handlers MUST do nothing when the event's default was already prevented (by a consumer handler that ran first), and the built-in `change` handler MUST do nothing when the native event's default was prevented; blur then neither touches the field, nor re-formats, nor commits.
  Source: `number-field/input/NumberFieldInput.tsx:133,140,233,273,412`. Test: [L4] `NumberFieldInput.test.tsx` "does not mark the field focused when the focus default is prevented", "does not reformat, touch, or commit on blur when the blur default is prevented", "ignores a change whose native default was already prevented", "does not step when the keydown default is prevented".

## 8. Form integration

The mechanism is [FFI](../architecture/field-and-form-infrastructure.md); the Number Field row of its control matrix (FFI §6.3) is: control element = the visible input (registered by `NumberField.Input`); registered value = the numeric `value` (never the text); form participant = the hidden `<input type="number">`, which is also the field's validation input; serialization `name=String(value)` or `""`; native constraints `required`/`min`/`max`/`step` on the hidden input plus `validate`.

- **NUM-FRM-01** — MUST. The hidden number input MUST be the single form-participation and native-validation element: it receives the field's validation input handle (merged with the consumer's `inputRef`), the effective `name`, `form`, `min`, `max`, `step`, `required`, `disabled`, `readOnly` and the raw numeric value, and the field's validation props (NUM-ARIA-04).
  Source: `number-field/root/NumberFieldRoot.tsx:112,500-540`. Test: [L2] `NumberFieldRoot.test.tsx` "should include the input value in the form submission", "triggers native HTML validation on submit", "blocks submission when step mismatch occurs" (L4 for the native-validation citations). Docs: CHANGELOG.md v1.0.0-rc.0 "Ensure hidden input participates in form validation".
- **NUM-FRM-02** — MUST. The submitted form value MUST be the numeric value stringified by the browser from the hidden input's `value` (`"54.5"`, `"0.1"`), and the empty string when the value is `null`; locale formatting, currency symbols, grouping etc. MUST never be submitted.
  Source: `number-field/root/NumberFieldRoot.tsx:527`. Test: [L2] `NumberFieldRoot.test.tsx` "should not include formatting in the submitted value", "should include the input value in the form submission", "submits to an external form when `form` is provided" (L4 for the submitted-value citations). Docs: CHANGELOG.md v1.0.0-beta.0 "Exclude number formatting from form value".
- **NUM-FRM-03** — MUST. Native constraint validation MUST apply through the hidden input: a value not on the step grid anchored at `min` makes `validity.stepMismatch` true and blocks native `<form>` submission (and makes `Field.Error match="stepMismatch"` show in a `Form`); `step="any"` disables step validation; `required` with `null` yields `valueMissing`. Because step validation needs an explicit `min`, the docs tell consumers to pass `min` together with `step` to get step validation on submit.
  Source: `number-field/root/NumberFieldRoot.tsx:528-535,575-577`. Test: [L4] `NumberFieldRoot.test.tsx` "blocks submission when step mismatch occurs", "blocks submission when step mismatch occurs with default step", "does not block submission when step=\"any\"", "prevents form submission when the value does not match the step", "prevents Form/Field submission when the value does not match the step", "triggers native HTML validation on submit", "onSubmit" (validationMode).
- **NUM-FRM-04** — MUST. With `allowOutOfRange`, a typed value outside `min`/`max` MUST reach the hidden input unclamped so that `validity.rangeOverflow`/`rangeUnderflow` become true and native submission is blocked; step-based changes still clamp (NUM-API-09).
  Source: `number-field/root/NumberFieldRoot.tsx:227-241`. Test: [L4] `NumberFieldRoot.test.tsx` "allows range overflow validation when true", "still clamps step interactions when true", "clamps to range when false". Docs: CHANGELOG.md v1.2.0 "Add `allowOutOfRange` prop".
- **NUM-FRM-05** — MUST. A `change` event on the hidden input (browser autofill) MUST — unless its native default was prevented or the root is disabled or read-only — request `valueAsNumber` (or `null` when `NaN`/empty) with reason `none`, then clear the form's external error for this field name, then run the field's change validation with the stored (clamped) value (falling back to the parsed value when nothing was stored, e.g. a canceled change); the visible text follows via NUM-STA-06. While disabled or read-only, autofill MUST be ignored entirely (no change callback, no error clearing, text unchanged).
  Source: `number-field/root/NumberFieldRoot.tsx:505-521`. Test: [L2] `NumberFieldRoot.test.tsx` "should handle browser autofill", "validates the parsed number when handling browser autofill", "ignores hidden-input autofill when $lockState inside Field", "ignores hidden-input autofill when $lockState outside Field", "clears the value when autofill empties the hidden input", "applies an autofilled value to the visible input", "validates the autofilled value even when the change is canceled".
- **NUM-FRM-06** — MUST. `NumberField.Input` MUST register the visible input as the field's control (FFI-ALG-12) with the control id, the numeric value, and the root's own `name` prop (used only for a nameless Field), enabled while not disabled; the registration MUST be updated whenever the value changes so `Form` values and `validate(value, formValues)` see the number.
  Source: `number-field/input/NumberFieldInput.tsx:91`, `internals/field-register-control/useRegisterFieldControl.ts:7-13`. Test: [L2] `NumberFieldRoot.test.tsx` "is validated with latest value when validationMode=onBlur" (`[1, { quantity: 1 }]`), "prevents Form/Field submission when the value does not match the step" (`quantity` is `0.1`).
- **NUM-FRM-07** — MUST. The field's `touched` MUST be set to `true` on the visible input's `blur` (unless default-prevented or disabled; read-only still marks touched), before any commit logic runs.
  Source: `number-field/input/NumberFieldInput.tsx:139-145`. Test: [L2] `NumberFieldRoot.test.tsx` "[data-touched]"; `NumberFieldInput.test.tsx` "does not reformat, touch, or commit on blur when the blur default is prevented".
- **NUM-FRM-08** — MUST. Whenever the numeric value changes (post-commit, pre-paint, by any path including an external `value` change), the component MUST clear the form's external error for the effective name and run the field's change validation with the new value (FFI-FRM-02: commits in `'onChange'` mode or after a failed submit, otherwise revalidates) — except immediately after a blur that itself changed the value, where the blur commit already validated: that one revalidation MUST be skipped unless the field is in an on-change validating state. If a blur's request normalizes back to the current value (no value change), the skip flag MUST be reset so the next external change still revalidates.
  Source: `number-field/input/NumberFieldInput.tsx:88,103-112,205-216`. Test: [L2] `NumberFieldRoot.test.tsx` "clears external errors on change", "revalidates immediately after form submission errors using increment button", "onChange" (validationMode), "revalidates when the controlled value changes externally", "revalidates an external change after a blur that normalizes back to the current value", "onSubmit" (re-invalidation after submit). Docs: CHANGELOG.md v1.0.0-beta.1 "Fix revalidation on change".
- **NUM-FRM-09** — MUST. In `validationMode: 'onBlur'` the blur handler MUST commit field validation (FFI-FRM-03) with the value that will be committed (the stored, clamped value; `null` for an emptied field) after the `input-blur`/`input-clear` change request and before `onValueCommitted`; `validate` therefore receives the number (not the text) and the clamped value.
  Source: `number-field/input/NumberFieldInput.tsx:163-165,218-220`. Test: [L2] `NumberFieldRoot.test.tsx` "prop: validate", "onBlur" (validationMode), "is validated with latest value when validationMode=onBlur", "is validated with clamped value when validationMode=onBlur". Docs: CHANGELOG.md v1.0.0-beta.0 "`validate` with latest value on blur".
- **NUM-FRM-10** — MUST. A `Field.Label` in the enclosing field MUST associate with the visible input (`for` = the input id, NUM-DOM-09) and its id MUST be exposed as the input's `aria-labelledby` (NUM-ARIA-03); the hinted implicit-label (wrapping `<label>`) activation is simply native `<label>` behavior for the visible input and needs no component support. A `Field.Description`/`Field.Error` MUST be referenced from both inputs' `aria-describedby` (NUM-ARIA-04).
  Source: `number-field/input/NumberFieldInput.tsx:84,115,127,461`. Test: [L2] `NumberFieldRoot.test.tsx` "Field.Label", "Field.Description".
- **NUM-FRM-11** — MUST. `Field.Root`'s `disabled` MUST disable the visible input (the `disabled` attribute) and the hidden input, and `Field.Root`'s `name` MUST override the root's `name` on the hidden input.
  Source: `number-field/root/NumberFieldRoot.tsx:89-101,526,533`. Test: [L2] `NumberFieldRoot.test.tsx` "disables the input when disabled=true", "does not disable the input when disabled=false", "should include the input value in the form submission".
- **NUM-FRM-12** — MUST. When a `Form` reports an external error for the field (or submission fails validation), the field becomes invalid: the visible input MUST carry `aria-invalid="true"` (NUM-ARIA-02), the root and every part MUST carry `data-invalid` (NUM-DOM-05), and the form's focus-first-invalid behavior (FFI-FOC-01) MUST land on the *visible* input (the hidden input redirects, NUM-FOC-01).
  Source: `number-field/input/NumberFieldInput.tsx:126`, `number-field/root/NumberFieldRoot.tsx:501-504`. Test: [L2] `NumberFieldRoot.test.tsx` "focuses the input when the field receives an error from Form", "clears external errors on change", "onSubmit" (L4 for the focus-first-invalid clause).
- **NUM-FRM-13** — MUST. No form `reset` handling exists (FFI-FRM-30): after a native reset the hidden input's browser-managed value may diverge from the component's value until the next change; the component does not listen for `reset`.
  Source: `number-field/root/NumberFieldRoot.tsx:500-540` (no reset listener). Test: none.

## 9. Lifecycle, mounting and animation

Not applicable — no part has a transition status or animation hooks; the only conditionally mounted element is `ScrubAreaCursor`, which is added and removed synchronously with the render conditions of NUM-STR-06/NUM-DOM-14 (no `keepMounted`, no `data-starting-style`/`data-ending-style`).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- NUM-I18N-01 — formatting uses .NET globalization instead of `Intl.NumberFormat`; percent spacing and currency patterns may differ (a port relaxation on both render modes, not a timing row: ADR 01 Decision 2 as amended; output equivalent, not byte-identical).
- NUM-I18N-02 — the parse rules take the group, decimal, currency, unit and exponent strings from .NET culture data (port relaxation, both modes; ADR 01 Decision 2).
- NUM-I18N-03 — the "runtime default locale" is the .NET current culture, not the browser's locale (port relaxation, both modes; ADR 01 Decision 2).
- NUM-I18N-07 — locale details are derived from .NET culture data instead of `formatToParts` (port relaxation, both modes; ADR 01 Decision 2).
- NUM-I18N-08 — the locale's group separator character comes from .NET culture data and may differ from the browser's; the class-based removal is unchanged (port relaxation, both modes; ADR 01 Decision 2).
- NUM-I18N-12 — the currency, unit and exponent strings stripped are those .NET formats (port relaxation, both modes; ADR 01 Decision 2).
- NUM-I18N-13 — re-formatting and the formatter cache use .NET formatters keyed on the culture (port relaxation, both modes; ADR 01 Decision 2).
- NUM-PTR-10 — row "drag-start vetoes generally": a C# consumer veto (`PreventBaseUIHandler()`) of the scrub-area pointerdown arrives a round trip late on Server, so the scrub starts regardless; WebAssembly honours it through `decide()`; a consumer JavaScript `preventDefault()` is honoured on both modes.
- NUM-PTR-19 — same row as NUM-PTR-10: a default-prevented pointerdown is ignored on both modes only when the veto comes from a JavaScript listener; a C# veto is late on Server.

## 10. Positioning

Not applicable — the number field has no floating parts. The virtual cursor's placement is pointer-driven (NUM-PTR-16), not anchor positioning.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (all 7 parts) | [RND](../architecture/rendering-and-composition.md) §6.1 | Root `div`; Group `div`; Input `input`; Increment/Decrement `button` with `ref: [forwardedRef, buttonRef]`; ScrubArea `span` with `ref: [forwardedRef, scrubAreaRef]`; ScrubAreaCursor `span` with `enabled: shouldRender` (NUM-STR-06) and `ref: [forwardedRef, cursorRef, setDomElement]`; all with `stateAttributesMapping` = `{ inputValue: null, value: null, ...fieldValidityMapping }` (NUM-DOM-01) |
| `useButton` (steppers) | [UBT](../architecture/button-behavior.md) §6.1 | `disabled: effectiveDisabled \|\| readOnly`, `native: nativeButton` (default `true`), `focusableWhenDisabled: true`; own `disabled`/`tabIndex: -1`/`type` pass through per UBT-DOM-09 (NUM-DOM-10, NUM-ARIA-07) |
| `usePressAndHold` (steppers) | [UBT](../architecture/button-behavior.md) §6.3 | `disabled: effectiveDisabled \|\| readOnly`, `elementRef: inputRef` (the *input's* window receives the listeners), `tick` = step by the originating event's modifiers (NUM-PTR-03), `onStop` = commit (NUM-PTR-04); defaults `startDelay 400`, `tickDelay 60`, `scrollDistance 8`, touch settle 50 ms |
| `isTouchLikePointerType` | UBT §6.3 | `touch` and `pen` skip focusing the input (NUM-FOC-03) |
| `createChangeEventDetails` / `createGenericEventDetails` | [EVD](../architecture/event-details-and-reasons.md) §3.1/3.2 | change details carry the custom `direction` property (NUM-EVT-03); commit details are generic |
| `useControlled` | [UTL](../architecture/shared-utilities.md) §4.1 | `controlled: value`, `default: defaultValue` (`null`), `name: 'NumberField'`, `state: 'value'` (NUM-STA-03, NUM-EDGE-04) |
| `useFieldRootContext` (Root, Input) | [FFI](../architecture/field-and-form-infrastructure.md) §6.1 | Root reads `setDirty`, `validityData.initialValue`, `disabled`, `setFilled`, `name`, `state`, `validation.{inputRef,getValidationProps,change}`; Input reads `validationMode`, `setTouched`, `invalid`, `shouldValidateOnChange`, `validation.{commit,change,getValidationProps}` |
| `useFormContext.clearErrors` | FFI §6.1 | called with the effective name on every value change (NUM-FRM-08) and on autofill (NUM-FRM-05) |
| `useLabelableId` (Root) | FFI §6.2 | `{ id: idProp }` → visible input id (NUM-API-18) |
| `useLabelableContext.labelId` (Input) | FFI §6.2 | → `aria-labelledby` (NUM-ARIA-03) |
| `useRegisterFieldControl` (Input) | FFI §6.3 | `(inputRef, id, value, undefined, !disabled, nameProp)` (NUM-FRM-06) |
| `useSetFieldFocused` (Input) | FFI §5 (FFI-ALG-13) | `(disabled, inputRef)` (NUM-FOC-05) |
| `useValueChanged` (Input) | UTL §4.4 | on `value` change: `clearErrors(name)` then `validation.change(value)` unless blocked (NUM-FRM-08) |
| `formatNumber` / `getFormatter` | UTL §4.15.2 | NUM-I18N-01; also used by `parseNumber` and `removeFloatingPointErrors` |
| `visuallyHidden` / `visuallyHiddenInput` | UTL §4.9 | hidden input style (NUM-DOM-07) |
| `platform.os.ios`, `platform.engine.webkit`, `platform.engine.gecko` | UTL §4.16 | `inputMode` (NUM-STA-19); pointer lock and cursor suppression on WebKit (NUM-PTR-10, NUM-STR-06); 20 ms release delay on Gecko (NUM-PTR-13) |
| `useTimeout` (ScrubArea ×2) | UTL §4.6 | `exitPointerLockTimeout` (20 ms, Gecko), `syntheticClickTimeout` (0 ms) |
| `addEventListener`, `mergeCleanups`, `ownerDocument`, `ownerWindow`, `activeElement`, `getTarget` | UTL §4.7/4.10 | wheel listener on the input; capture-phase window `pointermove`/`pointerup`; element `touchstart`; shadow-DOM-aware active element and event target |
| `warn` (+ `SafeReact.captureOwnerStack`) | UTL §4.17 | NUM-EDGE-03 |
| `describeConformance` | RND §4.9 | options per part in NUM-DOM-16 |

## 12. Edge cases, invariants and dev warnings

- **NUM-EDGE-01** — MUST. Rendering `Group`, `Input`, `Increment`, `Decrement`, `ScrubArea` or `ScrubAreaCursor` outside a `NumberField.Root` MUST throw an `Error` with the exact message `Base UI: NumberFieldRootContext is missing. NumberField parts must be placed within <NumberField.Root>.` (in all builds).
  Source: `number-field/root/NumberFieldRootContext.ts:42-51`. Test: [L2] `NumberFieldInput.test.tsx` "throws a descriptive error when rendered outside <NumberField.Root>".
- **NUM-EDGE-02** — MUST. Rendering `ScrubAreaCursor` outside a `NumberField.ScrubArea` MUST throw an `Error` with the exact message `Base UI: NumberFieldScrubAreaContext is missing. NumberFieldScrubArea parts must be placed within <NumberField.ScrubArea>.`
  Source: `number-field/scrub-area/NumberFieldScrubAreaContext.ts:15-23`. Test: [L2] `NumberFieldScrubAreaCursor.test.tsx` "throws a descriptive error when rendered outside <NumberField.ScrubArea>".
- **NUM-EDGE-03** — MUST. When reading `text/plain` from the clipboard throws during a paste (e.g. a `SecurityError`), the Input MUST (outside production builds) log exactly one console warning whose message starts with `Base UI: <NumberField.Input> could not read clipboard text during paste handling.` followed by a space and the owner stack when the runtime can provide one (an empty string otherwise), MUST NOT call `preventDefault()` (so the browser's default paste proceeds and the change handler validates it), and MUST NOT change the value. The warning is deduplicated per message (UTL-EDGE-11/12). (The field-infrastructure author deferred this diagnostic to this document.)
  Source: `number-field/input/NumberFieldInput.tsx:416-431`. Test: [L3] `NumberFieldInput.test.tsx` "warns in development when clipboard text cannot be read during paste handling", "warns without an owner stack when React cannot provide one".
- **NUM-EDGE-04** — SHOULD. Switching `value` between `undefined` and a defined value after the first render, or changing `defaultValue` while component-owned, SHOULD log the shared controlled-state errors UTL-EDGE-22 / UTL-EDGE-23 with `name: 'NumberField'`, `state: 'value'` (dev builds only); behavior follows NUM-STA-03.
  Source: `number-field/root/NumberFieldRoot.tsx:116-121`, `packages/utils/src/useControlled.ts:48-81`. Test: none.
- **NUM-EDGE-05** — MUST. Blurring with non-empty text that does not parse (`.`, `-`, `12.a` cannot occur but `abc` pasted natively can, overflowing `Infinity` output, …) MUST leave the text exactly as typed, MUST NOT fire `onValueChange` or `onValueCommitted`, and MUST keep the numeric value; the field is still marked touched. The hidden input therefore still carries the last valid value.
  Source: `number-field/input/NumberFieldInput.tsx:175-178`. Test: [L2] `NumberFieldRoot.test.tsx` "does not commit on blur for invalid input"; `NumberFieldInput.test.tsx` "should not commit values that overflow while parsing on blur".
- **NUM-EDGE-06** — MUST. The display text MUST always equal the text the component last accepted: a `change` event whose text fails NUM-EVT-15's character validation MUST leave the DOM value at the previous display text (the browser's edit is reverted), including for native pastes of invalid text.
  Source: `number-field/input/NumberFieldInput.tsx:120,260-262`. Test: [L4] `NumberFieldInput.test.tsx` "should not allow non-numeric characters on change"; `NumberFieldRoot.test.tsx` "should not allow pasting an invalid number".
- **NUM-EDGE-07** — MUST. Server-rendered markup MUST contain the formatted display text (NUM-STA-05) and hydration differences in the visible and hidden inputs' values (server locale ≠ client locale) MUST be tolerated silently; after hydration the client re-formats per NUM-STA-06. Port: withdrawn (ADR 02 Amendments).
  Source: `number-field/root/NumberFieldRoot.tsx:145-149,539`, `number-field/input/NumberFieldInput.tsx:128-130`. Test: none.
- **NUM-EDGE-08** — MUST. Snapping MUST be skipped when the effective step amount is `0` (e.g. `smallStep={0}` with Alt), and a negative `step`/`smallStep`/`largeStep` simply inverts the direction of the arithmetic; the pipeline never throws for any finite inputs. Non-finite `min`/`max` are not guarded (see §16).
  Source: `number-field/utils/validate.ts:109`. Test: none.
- **NUM-EDGE-09** — MUST. Pointer-lock denial (rejected promise or thrown error) MUST NOT abort the scrub: value changes and the commit on release still happen; only the virtual cursor is withheld (NUM-STR-06). The denial flag persists for later scrubs until a successful request clears it.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:338-355`. Test: [L4] `NumberFieldScrubAreaCursor.test.tsx` "handles pointer lock denial through requestPointerLock API".
- **NUM-EDGE-10** — MUST. On WebKit engines (`platform.engine.webkit`) pointer lock MUST never be requested and the virtual cursor MUST never render (the native pointer-lock notification causes layout shift); scrubbing still works with the native cursor, which is not wrapped at the viewport edges.
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:337-338`, `number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx:41-42`. Test: [L4] `NumberFieldScrubAreaCursor.test.tsx` (suite skipped on WebKit); `NumberFieldScrubArea.test.tsx` (pointer tests skipped on WebKit).
- **NUM-EDGE-11** — MUST. While `readOnly`: the visible input is focusable, tracks field focus/touched, accepts no text changes through keydown gating (NUM-KEY-11) — but a `change` event that still arrives (e.g. a native paste or script) is **not** gated by `readOnly` in the change handler (the browser itself blocks typing into a `readonly` input); keyboard steps, button steps, wheel, scrub, paste-handler and autofill MUST all be inert; steppers expose `aria-disabled="true"` without the `disabled` attribute; the hidden input carries `readonly`.
  Source: `number-field/input/NumberFieldInput.tsx:118,147-149,231-271,273,412`, `number-field/root/NumberFieldRoot.tsx:366,507,534`, `number-field/root/useNumberFieldStepperButton.ts:102,131,154,177`, `number-field/scrub-area/NumberFieldScrubArea.tsx:153,296,318`. Test: [L2] `NumberFieldRoot.test.tsx` "prop: readOnly" (3 tests), "does not scrub on wheel when readOnly", "does not paste into a readOnly field", "ignores hidden-input autofill when readOnly …"; `useNumberFieldStepperButton.test.tsx` "does not step when readOnly"; `NumberFieldScrubArea.test.tsx` "does not start scrubbing when the field is read-only" (L4 for the keyboard, wheel, scrub and paste clauses).
- **NUM-EDGE-12** — MUST. While effectively disabled: the visible and hidden inputs carry `disabled`; focus/blur handlers do not touch field state; `aria-invalid` is withheld; keyboard, wheel, scrub, paste, stepper and autofill paths are inert; the wheel and touchstart listeners are not even attached; a hold in progress stops (UBT-PTR-16).
  Source: `number-field/root/NumberFieldRoot.tsx:366,507,533`, `number-field/input/NumberFieldInput.tsx:117,126,133,140,273,412`, `number-field/scrub-area/NumberFieldScrubArea.tsx:153,296,318`. Test: [L2] `NumberFieldRoot.test.tsx` "should disable the input", "does not scrub on wheel when disabled", "is removed when the focused input becomes disabled", "ignores hidden-input autofill when disabled …"; `useNumberFieldStepperButton.test.tsx` "does not step when root is disabled", "cancels an active mouse press-and-hold interaction when disabled" (L4 for the keyboard, wheel, scrub, paste and hold clauses).
- **NUM-EDGE-13** — MUST. A `pen` pointer MUST be treated like `touch` for the steppers (no input focus on press, touch-style hold settle) but NOT for the scrub area, where only `pointerType === 'touch'` suppresses pointer lock/cursor and only `'mouse'` focuses the input (a pen scrub requests pointer lock and shows the cursor, but does not focus the input).
  Source: `internals/usePressAndHold.ts:19-21`, `number-field/root/useNumberFieldStepperButton.ts:164`, `number-field/scrub-area/NumberFieldScrubArea.tsx:322-328,338`. Test: [L4] `useNumberFieldStepperButton.test.tsx` "treats pen pointer as touch-like".
- **NUM-EDGE-14** — MUST. Overriding the Input's `type` with a selection-less type (`number`, `email`) is unsupported: the paste handler and caret restoration read/write `selectionStart`/`selectionEnd`, which throw on such inputs. The component makes no attempt to guard this.
  Source: `number-field/input/NumberFieldInput.tsx:95-101,436-443`. Test: none.
- **NUM-EDGE-15** — MUST. If the scrub area's listeners are torn down and re-attached without the pointer being released (the subtree was hidden and shown again), a later bare pointer move MUST NOT change the value (NUM-PTR-12); and if a scrub is interrupted by unmounting, no commit fires (NUM-PTR-17).
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:209-215,275-290`. Test: [L3] `NumberFieldScrubArea.test.tsx` "does not resume scrubbing when a hidden scrub area is revealed again", "clears the root scrubbing state when the scrub area unmounts mid-scrub" (L4 for the unmount clause).
- **NUM-EDGE-16** — MUST. Parenthesized accounting negatives (`(1,234.5)`) and currency `accounting` sign display are NOT parsed: `(` and `)` are disallowed characters, so typing them is rejected (NUM-EVT-15) and such pasted text parses to `null`. Formatting with `currencySign: 'accounting'` therefore produces text the user cannot re-enter unchanged (see §16).
  Source: `number-field/root/NumberFieldRoot.tsx:171-184` (parentheses are `literal` parts only when the formatter emits them), `number-field/utils/parse.ts:201-203`. Test: [L2] `NumberFieldRoot.test.tsx` "treats parentheses negatives as invalid input".
- **NUM-EDGE-17** — MUST. Percent and permille signs typed without a percent/permille format MUST be rejected as characters (NUM-KEY-07 does not include them), so `12%` cannot be entered and the value stays `12`; conversely under a percent format the typed digits are always divided by 100 whether or not the sign is present.
  Source: `number-field/root/NumberFieldRoot.tsx:186-197`, `number-field/utils/parse.ts:205-214`. Test: [L2] `NumberFieldRoot.test.tsx` "ignores percent and permille symbols when not formatted as percent", "parses percent while typing and commits canonical percent value"; `NumberFieldInput.test.tsx` "blocks percent and permille symbols on keydown when not formatted as percent" (L4 for the keydown clause).
- **NUM-EDGE-18** — MUST. Right-to-left documents need no special handling: formatting follows the locale, bidi marks are ignored when parsing (NUM-I18N-11), the keyboard arrows keep their up/down semantics, and the scrub axis is not mirrored (NUM-PTR-20).
  Source: `number-field/scrub-area/NumberFieldScrubArea.tsx:224-236`, `number-field/utils/parse.ts:113`. Test: [L1] `parse.test.ts` "parses localized scientific notation for %s" (`ar-EG`, `fa-IR`).

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. `it.each` families are listed once with their parameter placeholder. New IDs are introduced only where no earlier requirement captured the assertion (end of this section).

### `NumberFieldRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (`refInstanceof: HTMLDivElement`, standalone) | NUM-DOM-16 |
| "should accept a number value" / "should accept an `undefined` value" (defaultValue) | NUM-API-02, NUM-STA-05 |
| "should accept a number value that can change over time" | NUM-API-01, NUM-STA-04 |
| "should accept an `undefined` value" / "should accept a `null` value" (value) | NUM-API-01, NUM-API-02 |
| "should be `null` when the input is empty but not trimmed" | NUM-EVT-15 |
| "blocks submission when step mismatch occurs" / "blocks submission when step mismatch occurs with default step" / "does not block submission when step="any"" | NUM-API-06, NUM-FRM-03, NUM-DOM-11 |
| "should be called when the value changes" / "should be called with a number when transitioning from `null`" / "should be called with `null` when empty and transitioning from a number" | NUM-API-03, NUM-EVT-01 |
| "includes the reason for parseable typing" / "includes the reason when clearing the value" / "includes the reason for keyboard increments" / "includes the reason for increment button presses" / "includes the reason for decrement button presses" / "includes the reason for wheel scrubbing" | NUM-EVT-02 |
| "fires onValueChange for each parseable change while typing" | NUM-EVT-01, NUM-EVT-05, NUM-EVT-15 |
| "does not fire onValueChange for non-numeric composition/partial input" | NUM-EVT-15, NUM-I18N-04, NUM-EVT-05 |
| "handles sign and decimal partials vs. parseable numbers" | NUM-EVT-15, NUM-EVT-05 |
| "allows typing a decimal while replacing a selection" | NUM-KEY-09 |
| "accepts grouping while typing and parses progressively" / "respects locale decimal separator while typing (de-DE)" | NUM-EVT-15, NUM-KEY-07, NUM-I18N-08 |
| "parses percent while typing and commits canonical percent value" / "parses an interleaved percent sign while typing (1%2 -> 12%)" | NUM-I18N-05, NUM-EVT-06, NUM-EDGE-17 |
| "accepts currency symbol while typing and parses numeric value" / "accepts multi-character currency symbols while typing (e.g. pt-BR BRL)" / "accepts multi-character unit symbols while typing (e.g. km/h)" / "accepts exponent separators while typing (scientific notation)" / "allows deleting trailing currency symbols with locale literals" | NUM-KEY-07, NUM-I18N-12 |
| "ignores bidi/format control characters in the value (e.g. RTL exponent signs)" | NUM-I18N-11, NUM-EVT-15 |
| "allows backspace to remove trailing currency symbol that follows a locale literal" | NUM-KEY-03, NUM-I18N-12 |
| "does not commit on blur for invalid input" | NUM-EDGE-05 |
| "fires on blur with committed numeric value" / "fires null on blur when input is cleared" | NUM-EVT-05, NUM-EVT-06, NUM-EVT-08 |
| "reports and displays the clamped value on blur, not the raw input" / "reports and displays the min-clamped value on blur, not the raw input" | NUM-EVT-09, NUM-EVT-06 |
| "fires on keyboard interactions (ArrowUp/Down/Home/End)" | NUM-KEY-01, NUM-KEY-02, NUM-EVT-05 |
| "fires when using increment/decrement buttons" / "includes the correct reason for increment and decrement button presses" | NUM-PTR-01, NUM-EVT-05 |
| "does not fire when blurring an untouched empty field" | NUM-EVT-08 |
| "does not fire on keyboard steps that do not change the value" | NUM-KEY-01, NUM-EVT-13 |
| "fires once per press-release even after mouseleave/mouseenter during a hold" | NUM-PTR-04 |
| "does not commit a canceled keyboard change" / "does not commit a canceled clear on blur" | NUM-EVT-04 |
| "should disable the input" | NUM-API-13, NUM-DOM-08 |
| "should mark the input as readOnly" / "keeps focus state in sync on a readOnly field" / "does not render aria-readonly on stepper buttons" | NUM-API-14, NUM-FOC-05, NUM-ARIA-07, NUM-ARIA-08, NUM-STA-20 |
| "should mark the input as required" | NUM-API-15, NUM-DOM-08 |
| "should set the name attribute on the hidden input" / "marks the hidden input readOnly when the field is readOnly" | NUM-API-16, NUM-DOM-06, NUM-DOM-18 |
| "prevents the raw value from going below the `min` prop" / "allows the value to go above the `min` prop" / "prevents the value from going above the `max` prop" / "allows the value to go below the `max` prop" | NUM-API-05, NUM-EVT-13 |
| "allows typing a negative value via keyboard when min is 0" | NUM-API-09, NUM-KEY-07 |
| "allows range overflow validation when true" / "still clamps step interactions when true" / "clamps to range when false" | NUM-API-09, NUM-FRM-04, NUM-DOM-11 |
| "defaults to 1" / "should increment the value by the `step` prop" / "should decrement the value by the `step` prop" | NUM-API-06, NUM-PTR-01 |
| "should snap when incrementing to the nearest multiple of the `step` prop" / "should snap when decrementing to the nearest multiple of the `step` prop" | NUM-API-08, NUM-STA-08 |
| "advances by a step finer than 3 fraction digits" | NUM-STA-11, NUM-I18N-01 |
| "cleans binary floating point noise introduced by stepping" / "preserves large fractional values when stepping cleanup would be too coarse" | NUM-STA-11 |
| "should increment the value by the default `largeStep` prop of 10 while holding the shift key" / "should decrement the value by the default `largeStep` prop of 10 while holding the shift key" / "should use explicit `largeStep` value if provided while holding the shift key" / "should not use the `largeStep` prop if no longer holding the shift key" | NUM-API-07, NUM-STA-16, NUM-PTR-03 |
| "should increment the value by the default `smallStep` prop of 0.1 while holding the alt key" / "should decrement the value by the default `smallStep` prop of 0.1 while holding the alt key" / "should use explicit `smallStep` value if provided while holding the alt key" / "should not use the `smallStep` prop if no longer holding the alt key" | NUM-API-07, NUM-STA-16 |
| "reformats the visible text when the format prop changes at the same value" | NUM-API-11, NUM-STA-06, NUM-I18N-13 |
| "should format the value using the provided options" | NUM-API-11, NUM-I18N-01 |
| "reflects controlled value changes in the textbox" | NUM-STA-04 |
| "does not scrub on wheel when disabled" / "does not scrub on wheel when readOnly" / "does not scrub on wheel while pinch-zooming (ctrlKey)" / "does not scrub on wheel when the input is not focused" | NUM-PTR-07, NUM-EDGE-11, NUM-EDGE-12 |
| "should allow the user to scrub the input value with the mouse wheel" / "should not allow the user to scrub the input value with the mouse wheel if `allowWheelScrub` is `false`" | NUM-API-10, NUM-PTR-07 |
| "does not scrub on a horizontal wheel event, and lets it scroll the page" / "scrubs on a vertical wheel event that carries horizontal noise" / "uses largeStep when shift is held and the browser swaps the wheel axis" | NUM-PTR-08, NUM-PTR-09 |
| "uses largeStep when shift is held during wheel" | NUM-STA-16, NUM-PTR-07 |
| "calls onValueChange and onValueCommitted on wheel" | NUM-PTR-07, NUM-EVT-05, NUM-EVT-07 |
| "does not commit when a wheel step is a no-op at the boundary" | NUM-EVT-13, NUM-PTR-07 |
| "syncs the visible input value when using the mouse wheel after pasting" | NUM-PTR-07, NUM-STA-06, NUM-EVT-16 |
| "should include the input value in the form submission" / "should not include formatting in the submitted value" / "submits to an external form when `form` is provided" | NUM-FRM-01, NUM-FRM-02, NUM-API-16, NUM-API-17 |
| "triggers native HTML validation on submit" | NUM-FRM-03, NUM-API-15 |
| "focuses the input when the field receives an error from Form" | NUM-FRM-12, NUM-FOC-01, NUM-ARIA-02 |
| "clears external errors on change" / "revalidates immediately after form submission errors using increment button" | NUM-FRM-08, NUM-ARIA-02 |
| "should handle browser autofill" / "validates the parsed number when handling browser autofill" / "ignores hidden-input autofill when $lockState inside Field" (2 variants) / "ignores hidden-input autofill when $lockState outside Field" (2 variants) | NUM-FRM-05, NUM-ARIA-02 |
| "[data-touched]" | NUM-FRM-07, NUM-DOM-04 |
| "[data-dirty]" | NUM-STA-14, NUM-DOM-04 |
| "adds [data-filled] attribute when filled" / "has [data-filled] attribute when already filled" | NUM-STA-13, NUM-DOM-04 |
| "[data-filled]" (focus/blur) / "adds [data-focused] attribute on every focus" | NUM-FOC-05, NUM-DOM-04 |
| "is removed when the focused input becomes disabled" / "is removed when the focused input unmounts" | NUM-FOC-05 |
| "prop: validate" / "onBlur" (validationMode) | NUM-FRM-09, NUM-ARIA-02 |
| "onSubmit" (validationMode) | NUM-FRM-03, NUM-FRM-08, NUM-DOM-01, NUM-DOM-05 |
| "onChange" (validationMode) / "revalidates when the controlled value changes externally" | NUM-FRM-08, NUM-EVT-12 |
| "prevents form submission when the value does not match the step" / "prevents Form/Field submission when the value does not match the step" | NUM-FRM-03, NUM-FRM-06 |
| "disables the input when disabled=true" / "does not disable the input when disabled=false" | NUM-FRM-11, NUM-API-13 |
| "is validated with latest value when validationMode=onBlur" / "is validated with clamped value when validationMode=onBlur" | NUM-FRM-09, NUM-FRM-06, NUM-EVT-09 |
| "revalidates an external change after a blur that normalizes back to the current value" | NUM-FRM-08 |
| "Field.Label" / "Field.Description" | NUM-DOM-09, NUM-FRM-10, NUM-ARIA-04, NUM-DOM-15 |
| "should set the inputMode to numeric" | NUM-STA-19, NUM-DOM-08 |
| "forwards focus to the visible input" / "places the caret at the end of the visible input when forwarding focus" / "keeps a selection the consumer sets in onFocus when forwarding focus" | NUM-FOC-01, NUM-FOC-02, NUM-ARIA-10 |
| "clears the value when autofill empties the hidden input" / "applies an autofilled value to the visible input" / "validates the autofilled value even when the change is canceled" | NUM-FRM-05 |
| "accepts Persian digit keyboard input" | NUM-KEY-06, NUM-I18N-04 |
| "pastes %s numerals through the input contract" | NUM-EVT-16, NUM-EVT-02, NUM-I18N-04 |
| "rejects invalid pasted characters without changing the value contract" / "ignores a paste that does not parse to a number" | NUM-EVT-16 |
| "parses Persian digits and separators via change events" / "parses Persian digits with Arabic group/decimal separators" / "parses fullwidth digits and punctuation" | NUM-I18N-04, NUM-I18N-08 |
| "parses percent and permille signs in exotic forms when formatted as percent" / "ignores percent and permille symbols when not formatted as percent" | NUM-I18N-05, NUM-EDGE-17 |
| "parses trailing unicode minus" | NUM-I18N-10 |
| "treats parentheses negatives as invalid input" | NUM-I18N-06, NUM-EDGE-16 |
| "collapses extra dots from mixed-locale inputs" | NUM-I18N-09 |
| "allows composition key events (IME) without preventing default" | NUM-KEY-05 |
| "should allow pasting a valid number" / "should not allow pasting an invalid number" | NUM-EVT-16, NUM-EDGE-06 |
| "does not paste into a readOnly field" | NUM-EVT-16, NUM-EDGE-11 |
| "inserts pasted text at the caret instead of replacing the whole value" / "replaces the selected range when pasting over a selection" / "keeps the caret just after the pasted text" | NUM-EVT-16 |
| "should allow navigation keys and not prevent their default behavior" | NUM-KEY-03 |
| "does not prevent native caret movement for Home/End without min/max" | NUM-KEY-02 |
| "does not swallow non-printing keys it does not handle" | NUM-KEY-04 |
| "should set the locale of the input" / "should use the default locale if no locale is provided" / "should handle locales using space as the thousands separator" | NUM-API-12, NUM-I18N-01, NUM-I18N-08 |

### `NumberFieldInput.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (`refInstanceof: HTMLInputElement`, inside `Root`) | NUM-DOM-16 |
| "throws a descriptive error when rendered outside <NumberField.Root>" | NUM-STR-02, NUM-EDGE-01 |
| "has textbox role" | NUM-ARIA-01, NUM-DOM-08 |
| "should not allow non-numeric characters on change" / "should allow numeric characters on change" | NUM-EVT-15, NUM-EDGE-06 |
| "should not allow non-numeric characters on keydown" | NUM-KEY-10 |
| "should increment on keydown ArrowUp" / "should decrement on keydown ArrowDown" | NUM-KEY-01 |
| "should set the value to min on keydown Home" / "should set the value to max on keydown End" | NUM-KEY-02 |
| "uses smallStep and snapOnStep when holding Alt with ArrowUp/ArrowDown" | NUM-STA-16, NUM-STA-08, NUM-KEY-05 |
| "advances by a smallStep finer than 3 fraction digits" | NUM-STA-11, NUM-STA-16 |
| "increments with keyboard from numeric state, not rounded display text" / "decrements with keyboard from numeric state, not rounded display text" / "increments with keyboard from numeric state after a no-edit blur cycle" / "steps keyboard from dirty input text when the controlled value lags onValueChange" | NUM-KEY-12 |
| "does not commit a stale value when a synced keyboard step is canceled after an external change" | NUM-KEY-12, NUM-EVT-10 |
| "keeps dirty-input authority after a non-mutating key so the next keyboard step uses dirty text" / "keeps dirty-input authority after a non-mutating key so a button step uses dirty text" / "commits dirty text on blur after a cursor key when the controlled value does not mirror it" | NUM-KEY-03, NUM-KEY-12, NUM-PTR-05, NUM-EVT-06 |
| "allows unicode plus/minus, permille and fullwidth digits on keydown when formatted as percent" / "blocks percent and permille symbols on keydown when not formatted as percent" | NUM-KEY-06, NUM-KEY-07, NUM-KEY-10, NUM-EDGE-17 |
| "applies locale-aware decimal/group gating (de-DE)" | NUM-KEY-07, NUM-KEY-09 |
| "allows space key when locale uses space-like grouping (pl-PL)" | NUM-KEY-07, NUM-I18N-08 |
| "commits formatted value only on blur" / "should commit validated number on blur (min)" / "should commit validated number on blur (max)" / "should not snap number to step on blur" / "should commit validated number on blur (step and min)" | NUM-EVT-06, NUM-API-08 |
| "should preserve default formatting on first blur after external value change" / "should handle multiple blur cycles with default formatting" | NUM-EVT-06, NUM-EVT-12 |
| "should update input value after increment/decrement followed by external value change" / "should update input value after decrement followed by external value change" | NUM-STA-04, NUM-STA-06 |
| "should allow typing after default formatting on blur" / "should format to default canonical representation on blur" / "should handle edge case where parsed value equals current value but input differs" | NUM-EVT-06, NUM-STA-06 |
| "should apply default formatting after external change during typing" | NUM-STA-06 (see §16 Q4) |
| "should round to explicit maximumFractionDigits on blur" / "should respect roundingMode when rounding to explicit maximumFractionDigits on blur in %s" / "should commit roundingIncrement values on blur" / "should commit significant digit rounded values on blur" / "should preserve tiny percent significant digit rounded values on blur" / "should commit roundingMode values on blur without explicit precision" / "should round %s values on blur" / "should round currency values on blur without percent scaling" / "should preserve exact percent precision boundaries with directional roundingMode on blur" / "should preserve high-precision percent boundaries with directional roundingMode on blur" | NUM-EVT-06, NUM-STA-12 |
| "should not throw on blur when format uses roundingIncrement with fixed fraction digits" / "should format controlled values with rounding options after external value changes" | NUM-STA-12, NUM-STA-06 |
| "should parse locale prefix percent values on blur" | NUM-I18N-05, NUM-EVT-06 |
| "should commit the clamped value when blur rounding crosses a boundary" | NUM-STA-09, NUM-EVT-09 |
| "should not commit values that overflow while parsing on blur" | NUM-I18N-06, NUM-EDGE-05 |
| "should not commit a canceled blur change" | NUM-EVT-04 |
| "should preserve default numeric precision while formatting display on blur" / "should preserve values typed with more than 15 significant digits" / "keeps full numeric precision across no-edit focus/blur cycles and subsequent stepping" | NUM-STA-10, NUM-EVT-06, NUM-EVT-08 |
| "commits parsed value on blur and normalizes display for fr-FR" | NUM-EVT-06, NUM-I18N-08 |
| "warns in development when clipboard text cannot be read during paste handling" / "warns without an owner stack when React cannot provide one" | NUM-EDGE-03 |
| "reports an unchanged value once when a paste event carries no clipboard data" | NUM-EVT-13, NUM-EVT-16 |
| "blocks a second minus sign when the existing one is not selected" / "allows a minus sign that replaces the selected existing sign" / "allows a minus sign when the whole value is selected" | NUM-KEY-08 |
| "keeps the selection the browser set when focus moves into the input" | NUM-FOC-02 |
| "does not mark the field focused when the focus default is prevented" / "does not reformat, touch, or commit on blur when the blur default is prevented" / "ignores a change whose native default was already prevented" / "does not step when the keydown default is prevented" | NUM-EVT-14, NUM-FOC-05, NUM-FRM-07, NUM-KEY-11 |

### `useNumberFieldStepperButton.test.tsx` (each case runs for `Increment` and `Decrement`)

| Test | Requirements |
|---|---|
| "has the ${label} label" (2 variants: "Increase", "Decrease") | NUM-ARIA-05 |
| "exposes aria-controls on the stepper" | NUM-ARIA-06, NUM-API-18 |
| "seeds an empty field with 0" / "seeds an empty fully-negative range in range on the first step" / "seeds an empty field in range without directional snapping" | NUM-STA-17, NUM-EVT-03 |
| "steps once from defaultValue=0" / "only calls onValueChange once per step" | NUM-PTR-01, NUM-EVT-01 |
| "steps from the value set by an external controlled update" / "steps uncontrolled defaultValue from numeric state, not rounded display text" / "steps from numeric state after typed precision is formatted on blur" | NUM-PTR-05, NUM-STA-04 |
| "does not commit a stale value when a synced step is canceled after an external change" | NUM-PTR-05, NUM-EVT-10 |
| "steps when input is dirty but not blurred (click)" / "steps when input is dirty but not blurred (pointerdown)" | NUM-PTR-05, NUM-PTR-02 |
| "ignores non-primary pointer buttons" | NUM-PTR-02 |
| "does not step or commit when the dirty-input sync is canceled" | NUM-PTR-05, NUM-EVT-04 |
| "does not emit a snapped intermediate when committing dirty text before a step" | NUM-PTR-05, NUM-EVT-03 |
| "treats pen pointer as touch-like" | NUM-FOC-03, NUM-EDGE-13 |
| "always steps on quick touch ($order, before TOUCH_TIMEOUT)" / "fires onValueCommitted once on first soft tap (touch)" | NUM-PTR-01, NUM-PTR-02 |
| "steps continuously when holding pointerdown" / "stops stepping after mouseleave" / "starts stepping again after mouseleave then mouseenter" / "does not start stepping again after mouseleave then mouseenter after pointerup" | NUM-PTR-03 |
| "does not step twice with pointerdown and click" | NUM-PTR-01 |
| "stops calling onValueChange once the boundary is reached" | NUM-PTR-03, NUM-STA-20, NUM-EVT-13 |
| "commits on release after a hold reaches the boundary" | NUM-PTR-04, NUM-EVT-09 |
| "does not commit a stale value when the first hold tick is canceled after dirty input" | NUM-EVT-10, NUM-PTR-02 |
| "cancels an active mouse press-and-hold interaction when disabled" / "cancels the compatibility click from a touch press when disabled" | NUM-PTR-06, NUM-EDGE-12 |
| "removes the global release listener when unmounted during a hold" | NUM-PTR-04 |
| "does not step when readOnly" / "does not step when root is disabled" / "does not step when the button is disabled" | NUM-PTR-06, NUM-DOM-10, NUM-EDGE-11, NUM-EDGE-12 |
| "passes disabled state to className when $scenario" | NUM-STA-01, NUM-STA-02, NUM-STA-20, NUM-DOM-17 |

### `NumberFieldIncrement.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`, inside `Root`) | NUM-DOM-16, NUM-DOM-10 |
| "stops the hold on release when an ancestor stops pointerup propagation" | NUM-PTR-04 |
| "starts the hold once a touch press settles and ignores the compatibility click" / "does not start the hold when the touch moves like a scroll" / "treats a touch press with several small moves as a tap" | NUM-PTR-02, NUM-PTR-01 |
| "places the caret at the end of the input when a mouse press focuses it" | NUM-FOC-02, NUM-FOC-03 |
| "should increment by exact step without rounding when snapOnStep is false" / "should snap on increment when snapOnStep is true" / "should increment with respect to the min value" | NUM-API-08, NUM-STA-08, NUM-PTR-05 |

### `NumberFieldDecrement.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (same options as Increment) | NUM-DOM-16, NUM-DOM-10 |
| "should decrement by exact step without rounding when snapOnStep is false" / "should snap on decrement when snapOnStep is true" / "should decrement with respect to the min value" | NUM-API-08, NUM-STA-08, NUM-PTR-05 |

### `NumberFieldGroup.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (`refInstanceof: HTMLDivElement`, inside `Root`) | NUM-DOM-16 |
| "has role prop" | NUM-STR-04, NUM-ARIA-09 |

### `NumberFieldScrubArea.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (`refInstanceof: HTMLSpanElement`, inside `Root`) | NUM-DOM-16 |
| "has presentation role" | NUM-ARIA-09, NUM-DOM-12 |
| "blocks scrolling for a single-touch scrub" / "leaves multi-touch gestures such as pinch-zoom to the browser" | NUM-PTR-15 |
| "ignores non-primary pointer buttons" / "does not start scrubbing when the field is read-only" | NUM-PTR-19, NUM-DOM-02 |
| "keeps a selection the consumer sets in onFocus when a mouse press focuses the input" | NUM-FOC-04, NUM-FOC-02 |
| "should increment or decrement the value when scrubbing with the pointer" | NUM-PTR-11 |
| "tracks visual viewport scale and removes scrub listeners after pointer lock exits" | NUM-DOM-13, NUM-DOM-14, NUM-PTR-13, NUM-PTR-18 |
| "clears the root scrubbing state when the scrub area unmounts mid-scrub" | NUM-PTR-17, NUM-STA-15 |
| "syncs the visible input value when scrubbing after pasting" | NUM-PTR-11, NUM-STA-06 |
| "calls onValueChange while scrubbing and onValueCommitted on pointerup" | NUM-PTR-11, NUM-PTR-13, NUM-EVT-05 |
| "ignores pointer movement once the scrub has ended" / "does not resume scrubbing when a hidden scrub area is revealed again" | NUM-PTR-12, NUM-EDGE-15 |
| "does not change the value for a zero-distance move when sensitivity is 0" / "should only increment if the pointer movement was greater than or equal to the value" | NUM-PTR-11, NUM-API-24 |
| "wraps the virtual cursor around the allowed bounds" | NUM-PTR-16, NUM-API-24 |
| "scrubs on vertical movement when direction is vertical" / "should only scrub if the pointer moved in the given direction" | NUM-PTR-11, NUM-API-24 |
| "should fire onClick once when clicked without scrubbing" / "should fire onClick on child elements" | NUM-PTR-14, NUM-API-25 |

### `NumberFieldScrubArea.gecko.test.tsx`

| Test | Requirements |
|---|---|
| "delays releasing the pointer lock so soft clicks are not swallowed" | NUM-PTR-13, NUM-DOM-02 |

### `NumberFieldScrubAreaCursor.test.tsx` (skipped on WebKit)

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (`refInstanceof: HTMLSpanElement`, forced scrubbing context) | NUM-DOM-16 |
| "has presentation role" | NUM-ARIA-09 |
| "throws a descriptive error when rendered outside <NumberField.ScrubArea>" | NUM-STR-03, NUM-EDGE-02 |
| "renders when using mouse input" / "does not render when using touch input" / "handles pointer lock denial through requestPointerLock API" / "does not render after a quick tap when pointer lock resolves later" | NUM-STR-06, NUM-DOM-14, NUM-PTR-10, NUM-EDGE-09 |
| "only renders a cursor for the active scrub area" | NUM-STR-05 |

### `NumberFieldRoot.iOS.test.tsx`

| Test | Requirements |
|---|---|
| "uses the decimal keyboard when negative values are impossible" / "uses the text keyboard when negative values are allowed" / "uses the text keyboard when no minimum is set" | NUM-STA-19 |

### `parse.test.ts`

| Test | Requirements |
|---|---|
| "returns the number locale details" | NUM-I18N-07 |
| "parses a number" | NUM-I18N-02 |
| "parses percentages by default" / "handles percentages with style: "percent"" / "handles percentages with style: "unit" and unit: "percent"" / "strips an interleaved percent sign (1%2)" / "parses scientific notation percentages" / "parses permille values" / "parses scientific notation permille values" / "handles additional percent symbol variants" / "parses prefix percentages" | NUM-I18N-05 |
| "parses a number with Arabic numerals" / "parses a number with Han numerals" / "parses percentages with Arabic numerals" / "parses percentages with Han numerals" / "parses Persian digits" / "parses Persian digits with Arabic thousands and decimal characters" / "parses fullwidth digits and punctuation" / "maps the full Persian digit range" / "maps the full fullwidth digit range" / "maps the full Arabic-Indic digit range" / "maps the full Han digit range" / "maps both Han zero forms to 0" | NUM-I18N-03, NUM-I18N-04 |
| "returns null for an invalid number" / "returns null for Infinity-like inputs" / "returns null when parsing overflows to Infinity" / "returns null for empty and whitespace-only input" / "returns null for just a sign" / "returns null for Infinity with explicit sign and surrounding spaces" | NUM-I18N-06 |
| "strips bidi/control characters" / "treats bidi/format controls as ignorable in the middle of input" / "parses localized scientific notation for %s" / "parses localized percent scientific notation with a negative exponent for %s" / "parses localized tiny percent scientific notation for %s" | NUM-I18N-11, NUM-I18N-12 |
| "handles unicode minus and plus signs" / "handles ASCII leading and trailing signs" / "supports multiple unicode minus variants" / "supports additional unicode plus variants" | NUM-I18N-10 |
| "parses french formatted numbers with narrow no-break space grouping" / "removes various Unicode space groupings (fr-FR)" / "handles Swiss grouping apostrophe" / "parses de-DE formatted numbers" / "parses Arabic punctuation with ASCII digits" | NUM-I18N-08 |
| "parses currency when options specify currency style" / "parses scientific notation with currency codes" / "parses units when options specify unit style" / "parses currency prefix/suffix across locales" / "parses units with different formats" | NUM-I18N-12 |
| "collapses extra dots from mixed-locale inputs" / "parses number with mixed separators (FR)" / "parses number with mixed separators (US)" / "collapses multiple consecutive dots keeping only the last as decimal" | NUM-I18N-09 |
| "accepts a digit from every supported numeral system" / "rejects non-digit characters" | NUM-KEY-06 |

### `validate.test.ts`

| Test | Requirements |
|---|---|
| "returns 0.3 for 0.2 + 0.1" / "cleans negative floating point noise without a format" / "preserves precision finer than 3 fraction digits without a format" / "preserves safe integers exactly without a format" / "cleans 16-digit noise patterns from arithmetic" / "leaves large-magnitude noise uncleaned once one ULP exceeds the absolute cap" / "returns non-finite values untouched" / "returns 1000 for 1000, ignoring grouping" / "ignores formatting style" | NUM-STA-11 |
| "rounds compact notation against its standard equivalent" / "returns 0.3 for 0.2 + 0.1 with maximumFractionDigits" / "respects roundingMode when maximumFractionDigits is provided" / "respects half-even rounding at ties" / "respects rounding mode differences for negative values" / "rounds percent values at display scale when maximumFractionDigits is provided" / "preserves exact percent precision boundaries with roundingMode: %s" / "rounds percent values at display scale when only minimumFractionDigits is provided" / "rounds values with significant digit precision" / "rounds percent values at display scale when significant digits are provided" / "rounds percent significant digit values without reintroducing binary noise" / "preserves tiny percent significant digit values after scaling back" / "preserves meaningful percent precision above directional rounding boundaries" / "preserves high-precision percent fraction digits" / "preserves high-precision percent boundaries with directional rounding" / "preserves tiny values when Intl supports more than 20 fraction digits" / "uses percent fraction defaults when significant digits use roundingPriority" / "respects roundingPriority when fraction and significant digits are provided" / "does not scale unit percent values when maximumFractionDigits is provided" / "rounds currency values without percent scaling" / "rounds scientific currency code values" / "preserves negative values when signDisplay hides the sign" / "rounds negative accounting currency values" / "respects roundingMode when no precision is provided" / "respects roundingIncrement when maximumFractionDigits is provided" / "keeps the original finite value when percent scaling overflows before Intl rounding" / "rounds scientific notation values at their formatted scale" / "preserves non-invertible scientific rounded zero buckets" / "rounds invertible scientific zero buckets" / "uses resolved maximumFractionDigits when only minimum is provided" | NUM-STA-12 |
| "returns null when value is null" / "skips clamping when clamp is false" / "keeps rounded out-of-range values when clamping is disabled" | NUM-STA-07 |
| "preserves parsed input beyond 15 significant digits when step is undefined" | NUM-STA-10 |
| "cleans arithmetic noise when stepping" / "preserves large fractional values when stepping cleanup would be too coarse" / "preserves high-significance step values" / "removes floating point errors when stepping" | NUM-STA-10, NUM-STA-11 |
| "incrementing" (7 tests): "snaps 5 to 5 when step is 1" / "snaps 5.5 to 5 when step is 1" / "snaps -0.3 to -1 when step is 1" / "be same value when step is undefined and within bounds" / "snaps 9 to 5 when step is 5 and within bounds" / "snaps 12 to 10 when step is 5 and within bounds" / "preserves exact value when snapOnStep is false"; "decrementing" (7 tests): "snaps 5 to 5 when step is -1" / "snaps 5.5 to 6 when step is -1" / "snaps -0.3 to 0 when step is -1" / "be same value when step is undefined and within bounds" / "snaps 9 to 10 when step is -5" / "snaps 12 to 15 when step is -5" / "preserves exact value when snapOnStep is false"; "fractional step with snapOnStep" (6 tests): "handles increment with step 0.1 without getting stuck" / "handles decrement with step -0.1 without getting stuck" / "handles multiple increments with step 0.01" / "handles fractional step when a minimum is set" / "rounds to the nearest value when using small step" / "rounds negative small steps to the nearest value" | NUM-STA-08 |
| "applies roundingMode after step validation" / "clamps the final value after percent rounding crosses max" / "clamps the final value after directional percent rounding crosses min" / "clamps before rounding for non-integer bounds" / "clamps to a non-step-aligned max after snapping so the boundary is reachable" / "clamps to a non-step-aligned min after snapping so the boundary is reachable" | NUM-STA-09 |

### `getViewportRect.test.ts`

| Test | Requirements |
|---|---|
| "pads the element rect by half the teleport distance" / "collapses to the element rect for a zero teleport distance" / "uses the visual viewport when no teleport distance is set" / "falls back to the document element without a visual viewport" | NUM-PTR-16 |

### Type-only specs

`NumberFieldRoot.spec.tsx` (reason → event-type narrowing for change and commit details; `value: number | null`) and `NumberFieldInput.spec.tsx` (the `render` callback receives `disabled`/`readOnly` as native input props) are compile-time checks for §3.6 and §3.8; they map to NUM-EVT-02, NUM-EVT-11 and NUM-API-25.

### Additional requirements introduced here

- **NUM-DOM-11** — MUST. The hidden number input's `value` MUST follow every *applied* numeric change immediately (same render as the change), including live typing before any blur — so native constraint state (`validity.stepMismatch`, `rangeOverflow`) and `form.checkValidity()` reflect the typed value without the field being blurred.
  Source: `number-field/root/NumberFieldRoot.tsx:259,527`. Test: [L2] `NumberFieldRoot.test.tsx` "blocks submission when step mismatch occurs", "allows range overflow validation when true", "clamps to range when false".

## 14. React-specific notes and porting considerations

Non-normative.

- **Two state cells plus a bag of mutable flags.** Besides `value` and `inputValue` (React state), the root keeps several mutable, non-rendering cells: *sync-allowed* (`allowInputSyncRef`), *last changed value* (`lastChangedValueRef`), *pending commit* (`hasPendingCommitRef`), a mirror of the current value (`valueRef`) and of `format` (`formatOptionsRef`). They are read inside event handlers and change synchronously without re-rendering. A port needs equivalent instance fields that handlers can mutate mid-event; the requirements name them as "sync allowed", "last changed value" and "pending programmatic change". Port: see decisions/05-state-store-and-event-details.md §1.
- **Display-text sync is a layout effect without a dependency list** (`syncFormattedInputValueOnValueChange`): it runs after *every* render and compares the formatted value with the current text. That is what makes `format`/`locale` changes re-format the text (NUM-STA-06) and why `setValue` calls `forceRender()` after formatting — a formatting-only change (same number, different text) must still re-render. A port should recompute the display text after every state/prop change while sync is allowed, not only when the number changes. Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping, "post-commit, pre-paint — C# state work").
- **`setFilled`, `inputMode` (iOS) and the paste caret restore are layout effects** (post-commit, pre-paint). The caret restore runs after every render and consumes a one-shot `pendingCaretRef`. Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping; the caret write is DOM-only work).
- **Scrubbing state is flushed synchronously** (`ReactDOM.flushSync` inside `onScrubbingChange`) so the cursor element exists and can be measured (`offsetWidth`) in the same pointerdown handler. The ScrubArea's `onPointerDown` is `async`: it awaits `requestPointerLock()` and re-emits the scrubbing state afterwards only if the pointer is still down (`isScrubbingRef`). The scrub listeners live in a passive effect keyed on `isScrubbing`; React's `<Activity>` can tear that effect down and re-run it without unmounting, which is why `isScrubbingRef` (not the state) gates moves (NUM-PTR-12). Port: see decisions/04-js-interop-boundary.md §1 (the `number-field` module owns the scrub loop) and decisions/01-hosting-model-and-timing.md §Decision ("committed synchronously").
- **Portal.** `ScrubAreaCursor` renders through `ReactDOM.createPortal` into `ownerDocument(domElement).body`; the DOM handle is also stored in state (`setDomElement`) so the owner document can be resolved after first mount. Port: see decisions/04-js-interop-boundary.md §5.
- **Controlled `<input value>` reversion.** React re-applies `value={inputValue}` after each `change` event; a rejected string (NUM-EVT-15) is therefore reverted by React, not by explicit code (NUM-EDGE-06). A port must write the accepted display text back to the DOM after every input event.
- **`event.nativeEvent.defaultPrevented` in `onChange`** works around React issue 9023 (React fires `onChange` for an already-canceled native event). Ports should ignore canceled change events (NUM-EVT-14). Port: see decisions/04-js-interop-boundary.md §3 (event snapshot `DefaultPrevented`).
- **Wheel listener is native and non-passive.** React attaches `onWheel` passively, so `preventDefault` would be ignored; the component adds its own `wheel` listener in an effect (NUM-PTR-09). The effect's dependency list re-attaches it when `allowWheelScrub`/`disabled`/`readOnly` change. Port: see decisions/04-js-interop-boundary.md §1 (`number-field` module).
- **Press-and-hold** uses `useTimeout`/`useInterval` instances (UTL-EVT-01..06); the number field passes the *input's* ref as `elementRef`, so `ownerWindow(input)` receives the global `pointerup`/`contextmenu` listeners. Port: see decisions/04-js-interop-boundary.md §1 (press-and-hold lives in the `button` module).
- **`event.which === 229`** is the composition guard (Safari does not set `isComposing`); React derives `which` from `keyCode`.
- **`suppressHydrationWarning`** on both inputs silences React's hydration diff for server-locale formatting (NUM-EDGE-07).
- **Stable callbacks** (`useStableCallback`) make `setValue`, `incrementValue`, `getStepAmount`, `getAllowedNonNumericKeys`, `focusInput`, `onValueCommitted` identity-stable while always reading the latest props; the root context value is memoized on the remaining fields. Port: see decisions/05-state-store-and-event-details.md §1 (fixed cascaded store; parts subscribe and re-render themselves).
- **`useValueChanged`** (UTL-STA-36) drives the "value changed" side effects in a layout effect keyed on strict inequality; the Input's `blockRevalidationRef` is a one-shot suppression consumed by that effect (NUM-FRM-08). Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping, value-changed side effects).
- **Prop merging order.** `useRenderElement` receives `[builtIn, consumerProps, getter]` lists: attributes merge left-to-right (later wins), handlers chain consumer-first with `preventBaseUIHandler()` veto (RND-EVT-02/03); the stepper additionally wraps everything in `getButtonProps` (UBT-EVT-01..03). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision.
- **Ids.** `useLabelableId` → `useBaseUiId` → `React.useId()` (`base-ui-…`); on React 17 the id is `undefined` for the first render. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (generated ids `base-ui-<n>`).
- **`SafeReact.captureOwnerStack`** (React ≥ 19.1) decorates the clipboard warning; without it the message ends in a trailing space (NUM-EDGE-03).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| NUM-STR-01 | MUST | Root renders div then hidden `<input type="number">` sibling | 2 |
| NUM-STR-02 | MUST | All parts inside Root; else throw | 2 |
| NUM-STR-03 | MUST | ScrubAreaCursor inside ScrubArea; else throw | 2 |
| NUM-STR-04 | MUST | Group is structural only, `role="group"` | 2 |
| NUM-STR-05 | MUST | Multiple ScrubAreas; only the active one renders a cursor | 2 |
| NUM-STR-06 | MUST | Cursor renders only while scrubbing, non-touch, non-WebKit, lock not denied; into body | 2 |
| NUM-API-01 | MUST | `value` defined on first render → consumer-owned | 3.1 |
| NUM-API-02 | MUST | `defaultValue` default `null` | 3.1 |
| NUM-API-03 | MUST | `onValueChange(value, details)` | 3.1 |
| NUM-API-04 | MUST | `onValueCommitted(value, genericDetails)` | 3.1 |
| NUM-API-05 | MUST | `min`/`max` defaults and internal substitutes | 3.1 |
| NUM-API-06 | MUST | `step` default 1; `'any'` forwarded, steps by 1 | 3.1 |
| NUM-API-07 | MUST | `smallStep` 0.1 (Alt), `largeStep` 10 (Shift) | 3.1 |
| NUM-API-08 | MUST | `snapOnStep` default false; steps only | 3.1 |
| NUM-API-09 | MUST | `allowOutOfRange` default false; direct entry unclamped | 3.1 |
| NUM-API-10 | MUST | `allowWheelScrub` default false | 3.1 |
| NUM-API-11 | MUST | `format` passed to Intl; runtime change re-formats | 3.1 |
| NUM-API-12 | MUST | `locale` default runtime; parse heuristic when undefined | 3.1 |
| NUM-API-13 | MUST | effective disabled = field OR prop | 3.1 |
| NUM-API-14 | MUST | `readOnly` default false; blocks every value path | 3.1 |
| NUM-API-15 | MUST | `required` default false; on both inputs | 3.1 |
| NUM-API-16 | MUST | effective name = field ?? prop; hidden input only | 3.1 |
| NUM-API-17 | MUST | `form` on hidden input only | 3.1 |
| NUM-API-18 | MUST | `id` → visible input via labelable claim | 3.1 |
| NUM-API-19 | MUST | `inputRef` → hidden input | 3.1 |
| NUM-API-20 | MUST | `className`/`style`/`render` per RND; `onChange` excluded | 3.1 |
| NUM-API-21 | MUST | other Root props forwarded to root element | 3.1 |
| NUM-API-22 | MUST | Input `aria-roledescription` default `Number field` | 3.6 |
| NUM-API-23 | MUST | Stepper `disabled`/`nativeButton` defaults; button config | 3.5 |
| NUM-API-24 | MUST | ScrubArea `direction`/`pixelSensitivity`/`teleportDistance` defaults | 3.2 |
| NUM-API-25 | MUST | Part props forwarded after built-ins | 3.2 |
| NUM-STA-01 | MUST | Root state object: eleven fields; value/inputValue no attrs | 3.1 |
| NUM-STA-02 | MUST | Parts share root state; steppers override `disabled` | 3.1 |
| NUM-STA-03 | MUST | Ownership fixed on first render | 4 |
| NUM-STA-04 | MUST | Controlled follows prop; uncontrolled stores validated value | 4 |
| NUM-STA-05 | MUST | Initial display text formatted at first render | 4 |
| NUM-STA-06 | MUST | Display sync after every update while allowed; suspended while dirty | 4 |
| NUM-STA-07 | MUST | Validation pipeline inputs (`signedStep`, `small`, `shouldClamp`) | 4 |
| NUM-STA-08 | MUST | Snapping: directional (regular) vs nearest (small); base rule | 4 |
| NUM-STA-09 | MUST | Order: snap → clamp → round → clamp | 4 |
| NUM-STA-10 | MUST | Non-step, no rounding options → verbatim | 4 |
| NUM-STA-11 | MUST | `toPrecision(15)` delta-bounded cleanup | 4 |
| NUM-STA-12 | MUST | Intl round-trip rounding with option overrides | 4 |
| NUM-STA-13 | MUST | field `filled` = value !== null | 4 |
| NUM-STA-14 | MUST | field `dirty` set inside applied change | 4 |
| NUM-STA-15 | MUST | root `scrubbing` follows any ScrubArea | 4 |
| NUM-STA-16 | MUST | step amount: Alt → small, Shift → large, else base | 4 |
| NUM-STA-17 | MUST | empty field step seeds 0, no direction | 4 |
| NUM-STA-18 | MUST | reporting rule (`shouldFireChange`); last-changed/display side effects | 4 |
| NUM-STA-19 | MUST | `inputMode` numeric; iOS decimal/text | 4 |
| NUM-STA-20 | MUST | stepper effective disabled incl. boundary; readOnly separate | 4 |
| NUM-I18N-01 | MUST | `formatNumber` null → "" else Intl | 4.1 |
| NUM-I18N-02 | MUST | `parseNumber` algorithm | 4.1 |
| NUM-I18N-03 | MUST | locale inference ar/zh | 4.1 |
| NUM-I18N-04 | MUST | numeral-system digit mapping | 4.1 |
| NUM-I18N-05 | MUST | percent/permille scaling | 4.1 |
| NUM-I18N-06 | MUST | Infinity/NaN/overflow → null | 4.1 |
| NUM-I18N-07 | MUST | locale details; decimal from plain locale | 4.1 |
| NUM-I18N-08 | MUST | group separator classes | 4.1 |
| NUM-I18N-09 | MUST | last dot is the decimal | 4.1 |
| NUM-I18N-10 | MUST | leading/trailing sign handling | 4.1 |
| NUM-I18N-11 | MUST | format controls ignored | 4.1 |
| NUM-I18N-12 | MUST | currency/unit/exponent stripping | 4.1 |
| NUM-I18N-13 | MUST | locale/format change effects; formatter cache key | 4.1 |
| NUM-DOM-01 | MUST | data attribute projection; value/inputValue none; validity mapping | 5.1 |
| NUM-DOM-02 | MUST | `data-scrubbing` on all parts | 5.1 |
| NUM-DOM-03 | MUST | `data-disabled`/`readonly`/`required` | 5.1 |
| NUM-DOM-04 | MUST | `data-touched`/`dirty`/`filled`/`focused` (Field only) | 5.1 |
| NUM-DOM-05 | MUST | `data-valid`/`data-invalid`/neither | 5.1 |
| NUM-DOM-06 | MUST | hidden input attribute set | 5.2 |
| NUM-DOM-07 | MUST | hidden input visually-hidden style variant | 5.2 |
| NUM-DOM-08 | MUST | visible input built-in attributes; consumer overrides | 5.3 |
| NUM-DOM-09 | MUST | visible input `id` = labelable control id | 5.3 |
| NUM-DOM-10 | MUST | stepper `tabindex=-1`, user-select, `disabled` attribute, type/role | 5.4 |
| NUM-DOM-11 | MUST | hidden input value follows applied changes immediately (typing) | 13 |
| NUM-DOM-12 | MUST | ScrubArea role presentation + touch-action/user-select style | 5.6 |
| NUM-DOM-13 | MUST | cursor fixed style + transform translate3d/scale(1/s) | 5.7 |
| NUM-DOM-14 | MUST | cursor portaled to body; removed when conditions fail | 5.7 |
| NUM-DOM-15 | MUST | consumer attribute precedence; validation props last on Input | 5.8 |
| NUM-DOM-16 | MUST | conformance suite options per part | 5.8 |
| NUM-DOM-17 | MUST | stepper `data-disabled` from effective disabled | 5.4 |
| NUM-DOM-18 | MUST | visible input never has `name` | 5.3 |
| NUM-ARIA-01 | MUST | textbox + `aria-roledescription`; no spinbutton/aria-value* | 5.9 |
| NUM-ARIA-02 | MUST | `aria-invalid` while field invalid and not disabled | 5.9 |
| NUM-ARIA-03 | MUST | `aria-labelledby` from Field.Label id | 5.9 |
| NUM-ARIA-04 | MUST | `aria-describedby` composition on both inputs | 5.9 |
| NUM-ARIA-05 | MUST | stepper `aria-label` Increase/Decrease | 5.9 |
| NUM-ARIA-06 | MUST | stepper `aria-controls` = input id | 5.9 |
| NUM-ARIA-07 | MUST | stepper `aria-disabled` = disabled OR readOnly (stringified) | 5.9 |
| NUM-ARIA-08 | MUST | no `aria-readonly`/`aria-hidden` on steppers | 5.9 |
| NUM-ARIA-09 | MUST | Group `role=group`; ScrubArea/Cursor `role=presentation` | 5.9 |
| NUM-ARIA-10 | MUST | hidden input `aria-hidden`, `tabindex=-1` | 5.9 |
| NUM-KEY-01 | MUST | ArrowUp/Down step + commit | 6.1 |
| NUM-KEY-02 | MUST | Home/End → min/max when defined | 6.1 |
| NUM-KEY-03 | MUST | navigation keys not intercepted; Enter no commit | 6.1 |
| NUM-KEY-04 | MUST | other multi-character keys not intercepted | 6.1 |
| NUM-KEY-05 | MUST | Ctrl/Meta/Alt(non-step)/IME bypass | 6.1 |
| NUM-KEY-06 | MUST | numerals of all systems allowed | 6.1 |
| NUM-KEY-07 | MUST | allowed non-numeric set derivation | 6.1 |
| NUM-KEY-08 | MUST | single sign rule | 6.1 |
| NUM-KEY-09 | MUST | single decimal/currency/percent rule | 6.1 |
| NUM-KEY-10 | MUST | other single characters blocked | 6.1 |
| NUM-KEY-11 | MUST | disabled/readOnly/prevented → no handling | 6.1 |
| NUM-KEY-12 | MUST | step source: dirty text vs numeric state; stale-commit guard | 6.1 |
| NUM-PTR-01 | MUST | stepper click path | 6.2 |
| NUM-PTR-02 | MUST | stepper pointerdown path | 6.2 |
| NUM-PTR-03 | MUST | hold tick semantics; 400/60 ms; stops at no-op | 6.2 |
| NUM-PTR-04 | MUST | commit once on release | 6.2 |
| NUM-PTR-05 | MUST | dirty-text sync before a step | 6.2 |
| NUM-PTR-06 | MUST | steppers inert while disabled/readOnly; hold stops | 6.2 |
| NUM-PTR-07 | MUST | wheel conditions, direction, commit | 6.2 |
| NUM-PTR-08 | MUST | wheel axis selection; horizontal ignored; Shift swap | 6.2 |
| NUM-PTR-09 | MUST | wheel listener non-passive, attach/detach | 6.2 |
| NUM-PTR-10 | MUST | scrub pointerdown: start, focus, pointer lock | 6.2 |
| NUM-PTR-11 | MUST | scrub move accumulation and stepping | 6.2 |
| NUM-PTR-12 | MUST | move preventDefault; gated by pointer-down flag | 6.2 |
| NUM-PTR-13 | MUST | scrub release: unlock, state, commit; Gecko 20 ms | 6.2 |
| NUM-PTR-14 | MUST | synthetic click once per tap | 6.2 |
| NUM-PTR-15 | MUST | single-touch touchstart prevented | 6.2 |
| NUM-PTR-16 | MUST | virtual cursor position and wrapping bounds | 6.2 |
| NUM-PTR-17 | MUST | unmount mid-scrub cleanup, no commit | 6.2 |
| NUM-PTR-18 | MUST | window capture listeners only while scrubbing | 6.2 |
| NUM-PTR-19 | MUST | scrub pointerdown guards | 6.2 |
| NUM-PTR-20 | MUST | scrub axis not mirrored in RTL | 6.2 |
| NUM-FOC-01 | MUST | hidden input focus → visible input | 6.3 |
| NUM-FOC-02 | MUST | programmatic focus places caret at end; consumer selection wins | 6.3 |
| NUM-FOC-03 | MUST | stepper mouse press focuses input; touch/pen do not | 6.3 |
| NUM-FOC-04 | MUST | scrub mouse press prevents default and focuses input | 6.3 |
| NUM-FOC-05 | MUST | field `focused` tracking incl. disable/unmount | 6.3 |
| NUM-FOC-06 | MUST | steppers out of tab order; focusable-when-disabled config | 6.3 |
| NUM-EVT-01 | MUST | `onValueChange` synchronous, pre-store, validated value | 7 |
| NUM-EVT-02 | MUST | reason per user action; native event | 7 |
| NUM-EVT-03 | MUST | `direction` present only for step reasons | 7 |
| NUM-EVT-04 | MUST | cancel semantics | 7 |
| NUM-EVT-05 | MUST | `onValueCommitted` moments per reason | 7 |
| NUM-EVT-06 | MUST | blur commit procedure (non-empty) | 7 |
| NUM-EVT-07 | MUST | pending programmatic change flag → blur commits | 7 |
| NUM-EVT-08 | MUST | blur with empty text; untouched empty never commits | 7 |
| NUM-EVT-09 | MUST | committed value is the stored value | 7 |
| NUM-EVT-10 | MUST | canceled/no-op first tick commits current, not stale | 7 |
| NUM-EVT-11 | MUST | commit `event` types | 7 |
| NUM-EVT-12 | MUST | no `onValueChange` for prop changes | 7 |
| NUM-EVT-13 | MUST | direct entry fires on unchanged validated value; steps do not | 7 |
| NUM-EVT-14 | MUST | consumer `preventDefault` vetoes built-in handlers | 7 |
| NUM-EVT-15 | MUST | change-event procedure (clear / validate chars / parse) | 7 |
| NUM-EVT-16 | MUST | paste procedure (splice, caret, reason) | 7 |
| NUM-FRM-01 | MUST | hidden input is form + validation element | 8 |
| NUM-FRM-02 | MUST | submitted value unformatted / "" | 8 |
| NUM-FRM-03 | MUST | native constraints via hidden input; step="any" | 8 |
| NUM-FRM-04 | MUST | `allowOutOfRange` reaches native range validation | 8 |
| NUM-FRM-05 | MUST | autofill handling; ignored when disabled/readOnly | 8 |
| NUM-FRM-06 | MUST | Input registers as field control with numeric value | 8 |
| NUM-FRM-07 | MUST | touched on blur | 8 |
| NUM-FRM-08 | MUST | value-change side effects; blur revalidation block | 8 |
| NUM-FRM-09 | MUST | onBlur mode commits with clamped value | 8 |
| NUM-FRM-10 | MUST | Field.Label/Description wiring | 8 |
| NUM-FRM-11 | MUST | Field disabled/name overrides | 8 |
| NUM-FRM-12 | MUST | form errors → invalid attrs; focus lands on visible input | 8 |
| NUM-FRM-13 | MUST | no form reset handling | 8 |
| NUM-EDGE-01 | MUST | root-context error message | 12 |
| NUM-EDGE-02 | MUST | scrub-area-context error message | 12 |
| NUM-EDGE-03 | MUST | clipboard warning (verbatim), default paste proceeds | 12 |
| NUM-EDGE-04 | SHOULD | controlled-state dev errors (UTL-EDGE-22/23) | 12 |
| NUM-EDGE-05 | MUST | unparsable text on blur left as is | 12 |
| NUM-EDGE-06 | MUST | rejected change reverts DOM text | 12 |
| NUM-EDGE-07 | MUST | SSR formatted text; hydration mismatch tolerated | 12 |
| NUM-EDGE-08 | MUST | zero/negative step amounts | 12 |
| NUM-EDGE-09 | MUST | pointer lock denial does not abort scrub | 12 |
| NUM-EDGE-10 | MUST | WebKit: no pointer lock, no cursor | 12 |
| NUM-EDGE-11 | MUST | readOnly behavior matrix | 12 |
| NUM-EDGE-12 | MUST | disabled behavior matrix | 12 |
| NUM-EDGE-13 | MUST | pen pointer: touch-like for steppers, not for scrub | 12 |
| NUM-EDGE-14 | MUST | overriding Input `type` unsupported | 12 |
| NUM-EDGE-15 | MUST | hidden/revealed or unmounted scrub area | 12 |
| NUM-EDGE-16 | MUST | parenthesized negatives rejected | 12 |
| NUM-EDGE-17 | MUST | percent signs only under percent formats | 12 |
| NUM-EDGE-18 | MUST | RTL needs no special handling | 12 |

## 16. Open questions

1. **Hinted features that do not exist in v1.9.0.** There is no `role="spinbutton"` and no `aria-valuenow/min/max/text` (`number-field/input/NumberFieldInput.tsx:114-130`); no <kbd>PageUp</kbd>/<kbd>PageDown</kbd> stepping (`NumberFieldInput.tsx:361-365`, test "does not swallow non-printing keys it does not handle"); no <kbd>Enter</kbd> commit (`NumberFieldInput.tsx:35-43`); no `pattern` attribute on the visible input; `autoComplete` is fixed to `off`; `ScrubAreaCursor` has no CSS variables (position is an inline transform). Documented as NUM-ARIA-01, NUM-KEY-03/04, NUM-DOM-08, NUM-DOM-13.
2. **Outline discrepancies.** `components/page.mdx:1232-1236` lists `className, nativeButton, render, style` for Increment/Decrement but omits `disabled`, which the source accepts (`useNumberFieldStepperButton.ts:37`). The outline lists the Input's props as `aria-roledescription, className, render, style`; the component additionally emits/overrides many native attributes (§3.6). Both are documented here from the source.
3. **Stepper `disabled` attribute vs `focusableWhenDisabled`.** The button mechanism is configured `focusableWhenDisabled: true` (`useNumberFieldStepperButton.ts:179`), but the part also passes its own `disabled` attribute (`:121`), which UBT-DOM-09 lets through to the DOM (asserted by "does not step when the button is disabled"). A default native `<button disabled>` is therefore *not* focusable while effectively disabled; only a `nativeButton={false}` substitute keeps programmatic focusability. NUM-FOC-06 states the observable result; whether the native attribute is intended is unclear.
4. **External change while typing.** `NumberFieldInput.test.tsx` "should apply default formatting after external change during typing" passes only because clicking the "set pi" button blurs the input first (blur re-enables sync, then the new value is formatted). A purely programmatic `value` change while the input is focused and dirty is *not* reflected until blur (NUM-STA-06, `NumberFieldRoot.tsx:315-317`). The JSDoc does not describe this; ports should follow the source.
5. **`readOnly` and the change handler.** `onChange` (`NumberFieldInput.tsx:231-271`) is not gated on `readOnly` (keydown, paste, blur-commit and all step paths are). The browser prevents typing into a `readonly` input, so this is only reachable by script/`fireEvent`; NUM-EDGE-11 records it. Autofill via the hidden input *is* gated (`NumberFieldRoot.tsx:507`).
6. **`aria-disabled="false"` on enabled steppers** follows from UBT-ARIA-01 (native + `focusableWhenDisabled`), but no number-field test asserts the `"false"` case; NUM-ARIA-07 relies on the architecture doc.
7. **Non-finite bounds.** `min`/`max` are not validated: `min={-Infinity}` makes the snapping base `-Infinity` (`validate.ts:110-115`) and `snapOnStep` then yields `NaN`; `Number.MIN_SAFE_INTEGER` is only substituted for `undefined`. NUM-EDGE-08 leaves this unspecified.
8. **Multiple `Input` parts.** All share `inputRef` (`NumberFieldInput.tsx:459`), so focus redirection, wheel listening, field-control registration and `useSetFieldFocused` target only the last-mounted input; the anatomy (§2) declares `Input` as `0..1`.
9. **Autofill `reason: 'none'` while typing.** If the hidden input changes while the visible text is dirty (sync suspended), `setValue` reports the change (NUM-STA-18 second clause) but the visible text is not updated until blur (`NumberFieldRoot.tsx:270-272`). Probably unreachable in practice (autofill targets unfocused forms) but untested.
10. **`removeFloatingPointErrors` with `step: 'any'`.** `'any'` becomes `1`, so stepping still cleans noise (`NumberFieldRoot.tsx:102`); typed values keep full precision. Consistent with NUM-STA-10 but not covered by a test.
11. **Clipboard warning ownership.** The field-and-form-infrastructure author deferred the clipboard diagnostic to this document (NUM-EDGE-03); `warn` deduplication (`createLogOnce`) is specified in UTL-EDGE-11/12. `aria-invalid` on the Input is produced by two code paths (NUM-ARIA-02); they agree in v1.9.0 but a port should implement one source of truth.
12. **Pen scrubbing.** `ScrubArea` treats only `pointerType === 'touch'` as touch (`NumberFieldScrubArea.tsx:322`) while the steppers treat `pen` as touch-like (NUM-EDGE-13). A pen scrub therefore requests pointer lock and shows the virtual cursor; no test covers pen on the scrub area.
13. **`teleportDistance` semantics.** The JSDoc says "distance the cursor may move from the center of the scrub area" but the implementation pads the scrub area's *edges* by `teleportDistance / 2` (`getViewportRect.ts:7-15`, test "pads the element rect by half the teleport distance"). NUM-PTR-16 follows the implementation; negative values are unspecified.

### Related architecture-doc discrepancies

14. **EVD-EVT-23 is narrower than the number field.** `architecture/event-details-and-reasons.md` EVD-EVT-23 says `onValueCommitted` fires "only after a change was *applied* (not canceled, value actually changed)". The number field commits on *every* release of a started button hold even when no tick changed the value (`number-field/root/useNumberFieldStepperButton.ts:111-116`, NUM-PTR-04), on every scrub release (`number-field/scrub-area/NumberFieldScrubArea.tsx:168-171`, NUM-PTR-13), and on blur after manual typing even when the value is unchanged (`number-field/input/NumberFieldInput.tsx:199,221-223`). NUM-EVT-05 follows the source.
