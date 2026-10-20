# Slider

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/slider/` |
| Tests | `packages/react/src/slider/**/*.test.tsx`, `**/*.test.ts` (11 files, plus the type-level `SliderRoot.spec.tsx`) |
| Docs page | `docs/src/app/(docs)/react/components/slider/page.mdx` |
| Prefix | `SLD` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Slider lets the user pick one number, or an ordered set of numbers (a "range slider"), from a bounded interval by dragging a thumb along a track, pressing the track, or using the keyboard. It is a headless re-implementation of `<input type="range">`: each thumb wraps a real, visually hidden `<input type="range">`, which is the only focusable element, carries the WAI-ARIA `slider` semantics natively, participates in forms, and is what assistive technology (TalkBack's increment/decrement actions, VoiceOver) manipulates. The visible parts (`Root`, `Control`, `Track`, `Indicator`, `Thumb`, `Value`, `Label`) are plain elements positioned from the value.

A single number value makes a single-thumb slider; an array value (even a one-element array) makes a range slider with one thumb per element. Values are clamped to `[min, max]` and kept in ascending order; pointer input is snapped to the `step` grid anchored at `min`. Range thumbs may push each other, swap, or block (`thumbCollisionBehavior`) when dragged into one another; keyboard movement never crosses a neighbour. Changes are reported through `onValueChange` (cancelable, with a `reason` and the index of the thumb that moved) and, once an interaction settles, through `onValueCommitted`. Inside `Field.Root` the slider registers as the field's control, reports touched/dirty/focused/validity, and submits one form entry per thumb.

No component in the library builds on Slider, and Slider builds on no other component.

## 2. Anatomy

```
<Slider.Root>                 required, 1          default element: div   (role="group")
  <Slider.Label>              optional, 0..1       default element: div
  <Slider.Value>              optional, 0..n       default element: output
  <Slider.Control>            optional, 0..1       default element: div   (pointer surface)
    <Slider.Track>            optional, 0..n       default element: div   (structural)
      <Slider.Indicator>      optional, 0..n       default element: div
      <Slider.Thumb>          one per value, 1..n  default element: div   (+ nested <input type="range">,
                                                                           + pre-hydration <script> in the last thumb
                                                                             when thumbAlignment="edge")
```

- **SLD-STR-01** — MUST. `Slider.Root` renders exactly one host element (default `<div>`) and provides the shared slider state to its subtree; every other part MUST be rendered somewhere inside a `Slider.Root` subtree (any depth, portals included), otherwise rendering MUST fail with the error of SLD-EDGE-01.
  Source: `slider/root/SliderRoot.tsx:376-397`, `slider/root/SliderRootContext.ts:110-118`. Test: [L2] `SliderControl.test.tsx` "throws a descriptive error when rendered outside <Slider.Root>".
- **SLD-STR-02** — MUST. `Slider.Thumb` renders its host element with, in order, the consumer's `children`, then a nested `<input type="range">` (§5.6), then — only in `thumbAlignment="edge"` mode, only on the last thumb, and only in server output / during hydration — the pre-hydration `<script>` (SLD-STR-06). The input MUST be a descendant of the thumb element in every element-substitution form (default, substitute element, substitute callback).
  Source: `slider/thumb/SliderThumb.tsx:462-477`. Test: [L2] `SliderThumb.test.tsx` "renders the nested input as a sibling to children", "renders the nested input when using the short form render prop", "renders the nested input when using the long form render prop".
- **SLD-STR-03** — MUST. There is one thumb per value: thumb `i` displays `values[i]` (SLD-STA-03). A thumb whose index has no value MUST render with no `aria-valuenow`, an empty input value, the visually-hidden style of SLD-DOM-11 (non-inset mode), and MUST never produce a value change (keyboard input on it computes `NaN`, which is dropped per SLD-EDGE-06).
  Source: `slider/thumb/SliderThumb.tsx:193-196,288-289,321,454`. Test: [L2] `SliderThumb.test.tsx` "does not commit NaN when more thumbs are rendered than values" (L4 for the keyboard clause).
- **SLD-STR-04** — MUST. `Slider.Track` is structural only: it MUST render an element carrying the root's state attributes, the inline style `position: relative` (overridable by the consumer's `style`), and forwarded attributes, with no handlers, ARIA attributes or id of its own.
  Source: `slider/track/SliderTrack.tsx:23-35`. Test: [L2] `SliderTrack.test.tsx` conformance suite "Base UI component API".
- **SLD-STR-05** — MUST. Pointer interaction (§6.2) is only available for thumbs rendered inside `Slider.Control`'s subtree, because the press surface is the Control element and thumb lookup during a press uses the thumbs registered with the Root; keyboard interaction and native input changes (§6.1, §7) work for a thumb rendered anywhere under the Root.
  Source: `slider/control/SliderControl.tsx:485-558`, `slider/root/SliderRoot.tsx:393`. Test: none.
- **SLD-STR-06** — MUST. In `thumbAlignment="edge"` mode exactly one pre-hydration `<script>` element is emitted per slider, as the last child of the thumb with the highest index (so every preceding thumb is already in the DOM when it runs); it is emitted only into server-rendered markup and kept during the hydration pass, never for client-only mounts, and its element is removed once hydration finishes (TRN-DOM-11, TRN-ANI-37).
  Source: `slider/thumb/SliderThumb.tsx:472-475`, `internals/PrehydrationScript.tsx:32-49`. Test: [L4] `SliderThumb.test.tsx` "renders the inline pre-hydration script for edge-aligned thumbs", "renders a single pre-hydration script with the last thumb".

If `Control` is omitted the slider is keyboard-only. If `Track` is omitted, `Indicator` and `Thumb` may be direct children of `Control` (positioning is relative to whichever ancestor establishes a containing block; the library relies on `Track`'s `position: relative` for that by default). `Indicator`, `Value` and `Label` are optional and may be omitted freely. Several `Label` parts under one Root all receive the same id (SLD-DOM-19); the component does not prevent that.

## 3. Public API

### 3.1 `Slider.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `number \| readonly number[]` | — (component-owned) | Current value. A number makes a single-thumb slider; an array makes a range slider with one thumb per element. | SLD-API-01, SLD-API-02 |
| `defaultValue` | `number \| readonly number[]` | `min` (i.e. `0`) | Initial value when component-owned. | SLD-API-01 |
| `onValueChange` | `(value, eventDetails: Slider.Root.ChangeEventDetails) => void` | — | Change request; `value` has the same shape as the current value. Cancelable. See §7. | SLD-API-03 |
| `onValueCommitted` | `(value, eventDetails: Slider.Root.CommitEventDetails) => void` | — | Fired when an interaction settles on an applied value. Generic (not cancelable). See §7. | SLD-API-04 |
| `disabled` | `boolean` | `false` | Ignore user interaction. OR-ed with the enclosing `Field.Root`'s disabled. | SLD-API-05 |
| `min` | `number` | `0` | Lower bound; also the origin of the `step` grid. | SLD-API-06 |
| `max` | `number` | `100` | Upper bound. Must be greater than `min` (dev warning otherwise). | SLD-API-06 |
| `step` | `number` | `1` | Granularity for pointer snapping and arrow keys. Decimals allowed. | SLD-API-07 |
| `largeStep` | `number` | `10` | Increment for <kbd>PageUp</kbd>/<kbd>PageDown</kbd> and <kbd>Shift</kbd>+arrow. | SLD-API-07 |
| `minStepsBetweenValues` | `number` | `0` | Minimum number of `step`s between adjacent range values. | SLD-API-07 |
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Axis of the slider. | SLD-API-08 |
| `format` | `Intl.NumberFormatOptions` | — | Options used to format values for `Slider.Value` text and thumb `aria-valuetext`. | SLD-API-09 |
| `locale` | `Intl.LocalesArgument` | — (runtime locale) | Locale for that formatting. | SLD-API-09 |
| `name` | `string` | — | Form field name, placed on every thumb input. Overridden by the enclosing `Field.Root`'s `name`. | SLD-API-10 |
| `form` | `string` | — | `id` of the owning `<form>`; forwarded to every thumb input. | SLD-API-10 |
| `thumbAlignment` | `'center' \| 'edge' \| 'edge-client-only'` | `'center'` | How a thumb at `min`/`max` aligns with the Control's edge. | SLD-API-11 |
| `thumbCollisionBehavior` | `'push' \| 'swap' \| 'none'` | `'push'` | What happens when a dragged range thumb reaches a neighbour. Pointer only. | SLD-API-12 |
| `id` | `string` | generated | Root element id; the `Slider.Label` id is derived from it. *Not in the API outline.* | SLD-API-13 |
| `aria-labelledby` | `string` | — | Consumed: becomes the root's and every thumb input's `aria-labelledby` (§5.9). *Not in the API outline.* | SLD-API-14 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | SLD-API-15 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element (then passed through the field's validation-props composition). | SLD-API-15 |

**Consumed (not forwarded to the element):** `value`, `defaultValue`, `onValueChange`, `onValueCommitted`, `disabled`, `min`, `max`, `step`, `largeStep`, `minStepsBetweenValues`, `orientation`, `format`, `locale`, `name`, `form`, `thumbAlignment`, `thumbCollisionBehavior`, `id`, `aria-labelledby`, `className`, `style`, `render`.

- **SLD-API-01** — MUST. `value`/`defaultValue` is the consumer-owned/component-owned pair (UTL-STA-01…04, with `name: 'Slider'`, `state: 'value'`). When `value` is not `undefined` on the first render the raw value MUST equal `value` on every render; otherwise the initial raw value MUST be `defaultValue ?? min` (so a slider with neither prop starts as a single-thumb slider at `min`).
  Source: `slider/root/SliderRoot.tsx:118-122`, `packages/utils/src/useControlled.ts:41-46,83-90`. Test: [L2] `SliderRoot.test.tsx` "should update aria-valuenow" (default), "is called even if the $name did not change" (controlled stays put); `SliderThumb.test.tsx` "positions the thumb when the controlled value changes externally".
- **SLD-API-02** — MUST. The slider is in *range mode* exactly when the raw value is an array (`Array.isArray`), including a one-element array; it is in *single mode* when the raw value is a number. `onValueChange`/`onValueCommitted` MUST receive a value of the same kind: a number in single mode, an array in range mode (a one-element array stays a one-element array).
  Source: `slider/root/SliderRoot.tsx:165,174,206,312`, `slider/control/SliderControl.tsx:204-209`, `slider/root/SliderRoot.spec.tsx:5-41`. Test: [L2] `SliderControl.test.tsx` "preserves single-element array values on track press"; `SliderRoot.test.tsx` "validationMode=onBlur preserves single-element array values" (L4 for the pointer clause).
- **SLD-API-03** — MUST. `onValueChange` MUST be called as `(nextValue, details)` where `details` is a change-event-details object (EVD-API-01) specialised to the reasons of §3.8 and extended with `activeThumbIndex: number`. Firing rules are in §7.
  Source: `slider/root/SliderRoot.tsx:214,551-556,578-594`. Test: [L2] `SliderRoot.test.tsx` "should call handlers", "provides the change reason for input events".
- **SLD-API-04** — MUST. `onValueCommitted` MUST be called as `(value, details)` where `details` is a generic-event-details object (EVD-API-02: `reason` and `event` only). Firing rules are in §7.
  Source: `slider/root/SliderRoot.tsx:243,371-374,570-575,596-602`. Test: [L2] `SliderRoot.test.tsx` "single value" (onValueCommitted), "array value".
- **SLD-API-05** — MUST. The effective disabled state MUST be `fieldDisabled || disabled` (`fieldDisabled` is the enclosing `Field.Root`'s disabled, `false` outside a Field); the prop defaults to `false`. Effects: every thumb input gets the `disabled` attribute, pointer and touch interaction is ignored (§6.2), a focused thumb is blurred (SLD-FOC-05), and `data-disabled` appears on every part.
  Source: `slider/root/SliderRoot.tsx:62,113`, `slider/thumb/SliderThumb.tsx:149,326`. Test: [L2] `SliderRoot.test.tsx` "should render data-disabled on all subcomponents", "should receive disabled prop from Field.Root", "should not respond to drag events if disabled" (L4 for the pointer and blur clauses).
- **SLD-API-06** — MUST. `min` defaults to `0` and `max` to `100`; both are placed verbatim on every thumb input (`min`/`max` attributes) and bound every value (SLD-STA-03). When `min >= max` the development warning SLD-EDGE-02 MUST be emitted; no other behavior is guaranteed in that configuration.
  Source: `slider/root/SliderRoot.tsx:68-69,250-254`, `slider/thumb/SliderThumb.tsx:329-330`. Test: [L2] `SliderRoot.test.tsx` "sets the max attribute on the input", "sets the min attribute on the input", "warns when max is not greater than min".
- **SLD-API-07** — MUST. `step` defaults to `1`, `largeStep` to `10`, `minStepsBetweenValues` to `0`. `step` is placed verbatim on every thumb input's `step` attribute and anchors the snapping grid at `min` (SLD-STA-14); `largeStep` is used only by the keyboard (SLD-KEY-03, SLD-KEY-04); `minStepsBetweenValues × step` is the minimum distance enforced between adjacent range values (SLD-STA-17).
  Source: `slider/root/SliderRoot.tsx:65,70,76`, `slider/thumb/SliderThumb.tsx:444`. Test: [L2] `SliderRoot.test.tsx` "has the correct aria attributes" (`step="1"`), "should enforce a minimum difference between range slider values", "increments the value by largeStep".
- **SLD-API-08** — MUST. `orientation` defaults to `'horizontal'`; `'vertical'` switches the position axis (§5.5, §6.2), the inset measurement axis, and the input's `aria-orientation` and `writing-mode` (SLD-I18N-03); it does NOT change the arrow-key mapping (SLD-KEY-02).
  Source: `slider/root/SliderRoot.tsx:75`, `slider/thumb/SliderThumb.tsx:151,273-274,307-310,320`. Test: [L2] `SliderRoot.test.tsx` "sets the `aria-orientation` attribute", "should report the right position", "keyboard interactions" (vertical cases) (L4 for the pointer-axis and inset clauses).
- **SLD-API-09** — MUST. `format` and `locale` MUST be passed to the shared number formatter (UTL-I18N-05/06) for every formatted value: `Slider.Value`'s default text and render-function argument (SLD-DOM-18, SLD-API-26), the thumb's default `aria-valuetext` and the first argument of `getAriaValueText` (SLD-ARIA-04). Changing either prop at runtime MUST update those outputs.
  Source: `slider/root/SliderRoot.tsx:64,66,308,319`, `slider/value/SliderValue.tsx:36-41`, `slider/thumb/SliderThumb.tsx:57,60,324`. Test: [L2] `SliderRoot.test.tsx` "formats the value", "formats range values", "sets the locale when formatting a single value", "sets the locale when formatting a range value"; `SliderValue.test.tsx` "recomputes the formatted output when the format option changes".
- **SLD-API-10** — MUST. The effective form name MUST be the enclosing `Field.Root`'s `name` when defined, else the `name` prop; it and the `form` prop MUST be placed on every thumb input and MUST NOT appear on the root element. The `name` prop (not the effective name) is what the slider registers with the field as its fallback name (FFI-STA-10).
  Source: `slider/root/SliderRoot.tsx:71-72,114,176,318,323`, `slider/thumb/SliderThumb.tsx:327,331`. Test: [L2] `SliderRoot.test.tsx` "should receive name prop from Field.Root", "submits to an external form when `form` is provided", "should pass "name" and "value" as part of the event.target for onValueChange".
- **SLD-API-11** — MUST. `thumbAlignment` defaults to `'center'`. `'edge'` and `'edge-client-only'` both enable *inset* positioning (thumb kept inside the control, SLD-DOM-10, SLD-ANI-01); only `'edge'` additionally enables server-side pre-positioning (marker attributes SLD-DOM-03/07 and the script of SLD-STR-06).
  Source: `slider/root/SliderRoot.tsx:78,311,330`. Test: [L2] `SliderControl.test.tsx` "accounts for the thumb size when pressing an inset $name control" (`edge-client-only`); `SliderThumb.test.tsx` "recomputes inset positions when the slider becomes visible" (`edge`), "renders the inline pre-hydration script for edge-aligned thumbs" (L4 for the inset-positioning clause).
- **SLD-API-12** — MUST. `thumbCollisionBehavior` defaults to `'push'` and selects the range-collision algorithm used during pointer interaction only (SLD-STA-19, SLD-PTR-10); keyboard and native input changes always clamp to the neighbours (SLD-KEY-13).
  Source: `slider/root/SliderRoot.tsx:77,338`, `slider/control/SliderControl.tsx:212-223`, `slider/root/SliderRoot.tsx:230`. Test: [L4] `SliderThumb.test.tsx` "prop: thumbCollisionBehavior" (all four). Docs: `CHANGELOG.md` v1.0.0-beta.5 "Add `thumbCollisionBehavior` prop".
- **SLD-API-13** — MUST. The root element MUST carry `id` = the `id` prop, or a generated id (RND-DOM-27…30) when absent; `Slider.Label`'s id MUST be `${rootId}-label` (SLD-DOM-19) and MUST follow a runtime change of the root id.
  Source: `slider/root/SliderRoot.tsx:84-85,314,382`, `utils/resolveAriaLabelledBy.ts:3-5`. Test: [L2] `SliderRoot.test.tsx` "updates Slider.Label linkage when root id changes", "does not link Slider.Label before hydration".
- **SLD-API-14** — MUST. The `aria-labelledby` prop MUST be consumed and used as the first choice of SLD-ARIA-07 (root) and SLD-ARIA-06 (thumb inputs); it is not forwarded as an ordinary attribute.
  Source: `slider/root/SliderRoot.tsx:59,112,313,381`. Test: [L2] `SliderRoot.test.tsx` "has the correct aria attributes".
- **SLD-API-15** — MUST. `className`, `style` and `render` MUST behave per RND with the state object of §3.1; every prop not in the consumed list MUST be forwarded to the root element, merged after the built-in `aria-labelledby`/`id`/`role` (so a consumer `role` wins, RND-DOM-19) and then passed through the field's validation-props composition (SLD-ARIA-09).
  Source: `slider/root/SliderRoot.tsx:58-82,376-389`. Test: [L2] `SliderRoot.test.tsx` conformance suite "Base UI component API"; "Field.Description" (consumer `aria-describedby` composed).

#### State object

The object passed to `className`/`style`/`render` functions of **every** part (all parts share the root's state) and used to derive data attributes. It is the enclosing field's state (FFI-STA-01; all `false`/`null` outside a Field) with eight slider fields layered on top.

| Field | Type | Meaning |
|---|---|---|
| `activeThumbIndex` | `number` | Index of the thumb whose input currently owns focus, else `-1` (SLD-STA-06). |
| `disabled` | `boolean` | Effective disabled (SLD-API-05). Overrides the field's own `disabled`. |
| `dragging` | `boolean` | A pointer/touch interaction is in progress (SLD-STA-08). |
| `orientation` | `'horizontal' \| 'vertical'` | The `orientation` prop. |
| `max` | `number` | The `max` prop. |
| `min` | `number` | The `min` prop. |
| `minStepsBetweenValues` | `number` | The `minStepsBetweenValues` prop. |
| `step` | `number` | The `step` prop. |
| `values` | `readonly number[]` | Normalized values (SLD-STA-03): always an array, even in single mode. |
| `touched` | `boolean` | Field: a thumb has been blurred or a value change was attempted (SLD-FRM-05). |
| `dirty` | `boolean` | Field: normalized value differs from the field's initial value (SLD-STA-21). |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated or while disabled. |
| `filled` | `boolean` | Field flag; never written by Slider (SLD-STA-22). |
| `focused` | `boolean` | Field: a thumb input owns focus (SLD-FOC-04). |

- **SLD-STA-05** — MUST. The state object MUST contain exactly the fourteen fields above, with `disabled` taken from the effective disabled value, and MUST be the same object for every part of one slider.
  Source: `slider/root/SliderRoot.tsx:274-299,406-446`, `slider/control/SliderControl.tsx:111,486`, `slider/thumb/SliderThumb.tsx:141,463`, `slider/track/SliderTrack.tsx:21-24`, `slider/indicator/SliderIndicator.tsx:59,77`, `slider/value/SliderValue.tsx:29,44`, `slider/label/SliderLabel.tsx:30,56`. Test: [L2] `SliderRoot.test.tsx` "should render data-disabled on all subcomponents", "sets the data-orientation attribute".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-dragging` | `""` while `dragging` | SLD-DOM-02 |
| `data-orientation` | `"horizontal"` or `"vertical"`, always | SLD-DOM-02 |
| `data-disabled` | `""` while effective disabled | SLD-DOM-02 |
| `data-touched` | `""` while field `touched` (Field only) | SLD-DOM-02 |
| `data-dirty` | `""` while field `dirty` (Field only) | SLD-DOM-02 |
| `data-focused` | `""` while field `focused` (Field only) | SLD-DOM-02 |
| `data-valid` | `""` while field `valid === true` (Field only) | SLD-DOM-02 |
| `data-invalid` | `""` while field `valid === false` (Field only) | SLD-DOM-02 |
| `data-filled` | `""` while field `filled` — never set by Slider itself; not in the outline | SLD-DOM-02, SLD-STA-22 |

`activeThumbIndex`, `max`, `min`, `minStepsBetweenValues`, `step` and `values` are never emitted as data attributes (SLD-DOM-02). The same table applies to every part; `Slider.Thumb` adds `data-index` (§3.6).

#### CSS variables

None on the root.

### 3.2 `Slider.Label`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | — | — | **Ignored.** The label id is always `${rootId}-label`; a consumer `id` is discarded (the prop is excluded from the type). | SLD-API-27 |
| `className`, `style`, `render` | see RND | — | Functions receive the root's state object. | SLD-API-27 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the label element. | SLD-API-27 |

- **SLD-API-27** — MUST. `Slider.Label` MUST discard any `id` prop and render the id `${rootId}-label` (SLD-DOM-19); every other prop except `className`/`style`/`render` MUST be forwarded to its element, merged after the label's own `id`, `onClick` and `onPointerDown` handlers.
  Source: `slider/label/SliderLabel.tsx:25-28,48-59,64-67`. Test: [L2] `SliderLabel.test.tsx` conformance suite "Base UI component API"; `SliderRoot.test.tsx` "updates Slider.Label linkage when root id changes".

#### State object, data attributes, CSS variables

The root's state object and the root's data-attribute table (SLD-DOM-02). The API outline lists no data attributes for Label; the implementation emits the same set as every other part (§16). No CSS variables.

### 3.3 `Slider.Value`

**Default element:** `<output>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `null \| ((formattedValues: readonly string[], values: readonly number[]) => ReactNode)` | — | Render function for custom text; when absent the default text of SLD-DOM-18 is rendered. Non-function children are not accepted by the type. | SLD-API-26 |
| `aria-live` | `string` | `'off'` | Live-region politeness of the `<output>`. *Not in the API outline.* | SLD-API-26 |
| `className`, `style`, `render` | see RND | — | Functions receive the root's state object. | SLD-API-26 |
| *other `<output>` attributes and handlers* | — | — | Forwarded to the output element. | SLD-API-26 |

- **SLD-API-26** — MUST. When `children` is a function it MUST be called with `(formattedValues, values)` — `formattedValues[i]` being `values[i]` formatted per SLD-API-09 — and its return rendered as the element's content; otherwise the default text (SLD-DOM-18) is rendered. `aria-live` MUST default to `"off"` (consumer value wins). Other props MUST be forwarded, merged after the part's `aria-live`, content and `for` attribute.
  Source: `slider/value/SliderValue.tsx:20-58,65-73`. Test: [L2] `SliderValue.test.tsx` "accepts a render function", conformance suite "Base UI component API".

#### State object, data attributes, CSS variables

Root's state object; root's data attributes (SLD-DOM-02). No CSS variables.

### 3.4 `Slider.Control`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive the root's state object. | SLD-API-28 |
| *other `<div>` attributes and handlers* | — | — | Forwarded. A consumer `onPointerDown` runs before the part's own (RND-EVT-02); calling `preventDefault()` in it makes the part ignore the press (SLD-PTR-16). | SLD-API-28 |

- **SLD-API-28** — MUST. `Slider.Control`, `Slider.Track` and `Slider.Indicator` accept no part-specific props; `className`/`style`/`render` behave per RND with the root's state object and every other prop is forwarded to the element, merged after the part's built-in attributes.
  Source: `slider/control/SliderControl.tsx:90,485-558,571`, `slider/track/SliderTrack.tsx:19-42`, `slider/indicator/SliderIndicator.tsx:57,76-95`. Test: [L2] `SliderControl.test.tsx` / `SliderTrack.test.tsx` / `SliderIndicator.test.tsx` conformance suite "Base UI component API".

#### State object, data attributes, CSS variables

Root's state object; root's data attributes (SLD-DOM-02) plus the hydration marker `data-base-ui-slider-control` (SLD-DOM-03). No CSS variables.

### 3.5 `Slider.Track`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes. Props: as SLD-API-28. State object and data attributes: the root's. Inline style `position: relative` (SLD-STR-04). No CSS variables.

### 3.6 `Slider.Indicator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes. Props: as SLD-API-28. State object and data attributes: the root's, plus the hydration marker `data-base-ui-slider-indicator` (SLD-DOM-07).

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--start-position` | Indicator element (inset modes only) | `<start>%`, the inset position of the first thumb | SLD-DOM-06 |
| `--relative-size` | Indicator element (inset modes, range only) | `<end − start>%` | SLD-DOM-06 |

### 3.7 `Slider.Thumb`

**Default element:** `<div>` (plus the nested `<input type="range">`). **Accepts `render` element substitution:** yes (the input is injected into the substituted element's children). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `index` | `number` | `0` (single) / registry index (range) | Which value this thumb represents. Required for server rendering of range sliders. | SLD-API-17 |
| `disabled` | `boolean` | `false` | Disables this thumb only (OR-ed with the root's disabled). | SLD-API-16 |
| `getAriaLabel` | `((index: number) => string) \| null` | — | Computes the input's `aria-label`; takes precedence over `aria-label`. | SLD-API-18 |
| `aria-label` | `string` | — | Input's `aria-label`. *Not in the API outline.* | SLD-API-18 |
| `getAriaValueText` | `((formattedValue: string, value: number, index: number) => string) \| null` | — | Computes the input's `aria-valuetext`; takes precedence over `aria-valuetext`. | SLD-API-19 |
| `aria-valuetext` | `string` | — | Input's `aria-valuetext` when `getAriaValueText` is absent. | SLD-API-19 |
| `aria-labelledby` | `string` | — | Input's `aria-labelledby` (overrides the root-derived one). *Not in the API outline.* | SLD-API-20 |
| `aria-describedby` | `string` | — | Input's `aria-describedby` (composed with Field message ids). *Not in the API outline.* | SLD-API-20 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the nested input. | SLD-API-21 |
| `onFocus`, `onBlur`, `onKeyDown` | handlers for `HTMLInputElement` | — | Attached to the input, not the thumb element. | SLD-API-22 |
| `tabIndex` | `number` | — | Forwarded to the input only. | SLD-API-23 |
| `children` | `ReactNode` | — | Rendered inside the thumb element before the input. *Not in the API outline.* | SLD-API-24 |
| `id` | `string` | generated | Id of the thumb element (not the input). *Not in the API outline.* | SLD-API-25 |
| `className`, `style`, `render` | see RND | — | Functions receive the root's state object (not a thumb-specific one). | SLD-API-25 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the thumb element. | SLD-API-25 |

**Consumed (not forwarded to the thumb element):** `index`, `disabled`, `getAriaLabel`, `aria-label`, `getAriaValueText`, `aria-valuetext`, `aria-labelledby`, `aria-describedby`, `inputRef`, `onFocus`, `onBlur`, `onKeyDown`, `tabIndex`, `children`, `id` (applied explicitly), `className`, `style`, `render`.

- **SLD-API-16** — MUST. A thumb's effective disabled state MUST be `disabledProp || rootEffectiveDisabled`, default `false`. A thumb disabled only through its own prop MUST set the `disabled` attribute on its input, MUST ignore pointer presses on it (SLD-PTR-01), MUST be skipped when choosing the nearest thumb (SLD-PTR-03), and MUST NOT publish the field's focused flag — but its element's `data-disabled` still reflects only the root's disabled state (SLD-EDGE-05).
  Source: `slider/thumb/SliderThumb.tsx:101,149,158,326,481-483`, `slider/control/SliderControl.tsx:159-171,253,409-412,508-511`. Test: [L2] `SliderRoot.test.tsx` "does not drag a thumb disabled via the `disabled` prop", "does not change a single disabled thumb when pressing the track", "is removed when the focused thumb becomes disabled" (L4 for the pointer clauses).
- **SLD-API-17** — MUST. In single mode a thumb's index MUST be `0` regardless of the prop. In range mode the index MUST be the `index` prop when given, else the thumb's position among the Root's registered thumbs in document order (CMP-STA-01/02), which is `-1` until the registry first publishes (post-commit, pre-paint; in server output it is therefore `-1` and the thumb renders as in SLD-STR-03). The `index` prop does not affect the registry order: it MUST equal the thumb's document-order position for pointer handling to address the right thumb (§16).
  Source: `slider/thumb/SliderThumb.tsx:189-196`, `internals/composite/list/useCompositeListItem.ts:32-55`. Test: [L2] `SliderThumb.test.tsx` "sets the thumb index data attribute"; `SliderRoot.test.tsx` "drags the intended thumb when 3 thumbs are present" (L4 for the pointer-handling clause). Docs: `components/slider/page.mdx` §Range slider.
- **SLD-API-18** — MUST. The input's `aria-label` MUST be `getAriaLabel(index)` when `getAriaLabel` is a function, otherwise the `aria-label` prop (absent when neither is given).
  Source: `slider/thumb/SliderThumb.tsx:312-317`. Test: [L2] `SliderRoot.test.tsx` "does not set aria-labelledby when getAriaLabel is provided"; `SliderThumb.test.tsx` "forwards aria-label to the input".
- **SLD-API-19** — MUST. The input's `aria-valuetext` MUST be `getAriaValueText(formatted, value, index)` when `getAriaValueText` is a function (where `formatted` is the value formatted per SLD-API-09), otherwise the `aria-valuetext` prop, otherwise the default of SLD-ARIA-04.
  Source: `slider/thumb/SliderThumb.tsx:322-325`. Test: [L2] `SliderThumb.test.tsx` "prefers getAriaValueText over a direct aria-valuetext prop", "forwards aria-valuetext to the input".
- **SLD-API-20** — MUST. `aria-labelledby` and `aria-describedby` given to a Thumb MUST be placed on its input (not on the thumb element); `aria-describedby` is composed with the field's message ids (SLD-ARIA-08).
  Source: `slider/thumb/SliderThumb.tsx:318-319,456`. Test: [L2] `SliderThumb.test.tsx` "forwards aria-labelledby to the input", "forwards aria-describedby to the input".
- **SLD-API-21** — MUST. `inputRef` MUST receive the nested `<input type="range">`.
  Source: `slider/thumb/SliderThumb.tsx:460,471`. Test: [L2] `SliderThumb.test.tsx` "can focus the input element".
- **SLD-API-22** — MUST. `onFocus`, `onBlur` and `onKeyDown` MUST be attached to the input (so `event.currentTarget` is the input), MUST run before the part's own handler for the same event (RND-EVT-02), MUST be called for handled and unhandled keys alike, and MUST NOT be called for the internal blur/focus pair that restores `:focus-visible` (SLD-KEY-11).
  Source: `slider/thumb/SliderThumb.tsx:107-109,164-176,457,507-510`. Test: [L4] `SliderThumb.test.tsx` "forwards key events that the slider does not handle", "forwards handled $key key events", "forwards focus and blur to the input so `currentTarget` is the input", "does not emit extra blur and focus events when restoring focus-visible".
- **SLD-API-23** — MUST. `tabIndex` MUST be forwarded to the input only; by default neither the thumb element nor the input carries a `tabindex` attribute (the input is natively tabbable, `tabIndex` property `0`). `tabIndex={-1}` MUST remove the input from the tab sequence.
  Source: `slider/thumb/SliderThumb.tsx:110,452`. Test: [L2] `SliderThumb.test.tsx` "does not apply tabIndex to the thumb element by default", "can be removed from the tab sequence".
- **SLD-API-24** — MUST. `children` MUST be rendered inside the thumb element, before the input (SLD-STR-02).
  Source: `slider/thumb/SliderThumb.tsx:95,468-470`. Test: [L2] `SliderThumb.test.tsx` "renders the nested input as a sibling to children".
- **SLD-API-25** — MUST. The thumb element MUST carry `id` = the `id` prop or a generated id; `className`/`style`/`render` behave per RND with the root's state object (the resolved `style` is merged over the positioning style of SLD-DOM-09/10); every prop not in the consumed list MUST be forwarded to the thumb element, merged after the part's `data-index`, `id`, `onPointerDown` and `style`.
  Source: `slider/thumb/SliderThumb.tsx:93-115,462-496`. Test: [L2] `SliderThumb.test.tsx` conformance suite "Base UI component API"; `SliderIndicator.test.tsx` "preserves keyboard and indicator parity…" (consumer `style` width/height merged with positioning) (L4 for the positioning-style merge clause).

#### State object

The root's state object (SLD-STA-05). A thumb has no state fields of its own; `Slider.Thumb.State` extends `Slider.Root.State` with nothing.

#### Data attributes

The root's table (SLD-DOM-02) plus:

| Attribute | Present when / value | Req |
|---|---|---|
| `data-index` | always; the thumb's index as a decimal string (`"0"` in single mode; `"-1"` before a range thumb without `index` is registered) | SLD-DOM-08 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--position` | Thumb element (inset modes only) | `<positionPercent>%` (`0%` until computed) | SLD-DOM-10 |

### 3.8 Shared types

- `Slider.Root.ChangeEventReason` = `'input-change' | 'track-press' | 'drag' | 'keyboard' | 'none'`. `'none'` is declared but no slider code path emits it (§7, §16). Source: `slider/root/SliderRoot.tsx:585-590`, `internals/reason-parts.ts:1,13,17,27,32`.
- `Slider.Root.ChangeEventDetails` = the change-event-details shape of [EVD](../architecture/event-details-and-reasons.md) (EVD-API-01) over those reasons, extended with `activeThumbIndex: number` ("The index of the active thumb at the time of the change"). `event` is `InputEvent | Event` for `input-change`, `KeyboardEvent` for `keyboard`, `PointerEvent | MouseEvent | TouchEvent` for `track-press`, `PointerEvent | TouchEvent` for `drag` (EVD §3.5). Source: `slider/root/SliderRoot.tsx:578-594`, `internals/createBaseUIEventDetails.ts:4-47,126-157`; `slider/root/SliderRoot.spec.tsx:43-66`.
- `Slider.Root.CommitEventReason` = the same five literals. `Slider.Root.CommitEventDetails` = generic-event details (EVD-API-02) over them. Source: `slider/root/SliderRoot.tsx:596-602`, `internals/createBaseUIEventDetails.ts:159-174`; `slider/root/SliderRoot.spec.tsx:68-80`.
- `Slider.Root.State`, and `Slider.Control.State`, `Slider.Track.State`, `Slider.Indicator.State`, `Slider.Thumb.State`, `Slider.Value.State`, `Slider.Label.State` (all identical to the root's) — §3.1 State object. Source: `slider/root/SliderRoot.tsx:406-446`, `slider/control/SliderControl.tsx:569`, `slider/track/SliderTrack.tsx:40`, `slider/indicator/SliderIndicator.tsx:93`, `slider/thumb/SliderThumb.tsx:505`, `slider/value/SliderValue.tsx:63`, `slider/label/SliderLabel.tsx:62`.
- `Slider.Root.Props<Value extends number | readonly number[]>` is generic: `onValueChange`/`onValueCommitted` receive `number` when `Value` is a number, else `Value`. `Slider.<Part>.Props` for the other parts — the prop tables above. Source: `slider/root/SliderRoot.tsx:448-576,604-612`; `slider/root/SliderRoot.spec.tsx`.
- `Orientation` = `'horizontal' | 'vertical'` (`internals/types.ts`).
- Exported constant namespaces `SliderRootDataAttributes`, `SliderValueDataAttributes`, `SliderControlDataAttributes`, `SliderTrackDataAttributes`, `SliderThumbDataAttributes`, `SliderIndicatorDataAttributes` list the attribute names of §3 (all identical: `dragging`, `orientation`, `disabled`, `valid`, `invalid`, `touched`, `dirty`, `focused`; Thumb adds `index`). No `*CssVars.ts` file exists; the CSS variables of §3.6/3.7 are not exported as constants. Source: `slider/index.ts:11-16`, `slider/*/*DataAttributes.ts`.

## 4. State model

The slider owns one piece of value state (the *raw value*, `value`/`defaultValue`) plus interaction state: the active thumb index, the last-used thumb index, the dragging flag, the inset positions, and a record of the reason of the last applied change. Everything displayed is derived from the raw value through the normalization of SLD-STA-03.

**Ownership.** Decided once at first render (UTL-STA-01). In consumer-owned mode the component never stores a candidate value; the DOM reflects only what the consumer passes back.

**Derived values.**

- `range` = `Array.isArray(rawValue)` (SLD-API-02)
- `values` = single: `[clamp(rawValue, min, max)]`; range: `rawValue.map(v => clamp(v, min, max))` sorted ascending (SLD-STA-03)
- `fieldValue` = `range ? values : values[0]` (SLD-STA-12) — what the field registers, validates and compares for `dirty`
- `thumbValue(i)` = `values[i]`; `thumbPercent(i)` = `valueToPercent(values[i], min, max)` (SLD-STA-20)
- effective disabled = `fieldDisabled || disabled`; effective name = `fieldName ?? name`
- `inset` = `thumbAlignment !== 'center'`; `renderBeforeHydration` = `thumbAlignment === 'edge'`

**Invariants.** `values` is ascending and inside `[min, max]`. A candidate produced by any interaction is in `[min, max]`, on the step grid for pointer input, and (range) satisfies the minimum distance of SLD-STA-17 or is rejected whole. The raw value may be unsorted or out of range when the consumer supplies it that way; only the derived values are normalized.

There is no mount/unmount lifecycle for any part; §9 covers only hydration and inset-position timing.

- **SLD-STA-01** — MUST. Ownership of the value MUST be fixed on the first render: consumer-owned if `value !== undefined`, otherwise component-owned; later crossings between `undefined` and a value MUST NOT change the mode (development diagnostic SLD-EDGE-03 instead).
  Source: `slider/root/SliderRoot.tsx:118-122`, `packages/utils/src/useControlled.ts:41,46`. Test: none.
- **SLD-STA-02** — MUST. In component-owned mode the initial raw value MUST be `defaultValue` when defined, else `min`.
  Source: `slider/root/SliderRoot.tsx:120`. Test: [L2] `SliderRoot.test.tsx` "[data-touched]" (no `defaultValue`, input focusable), "[data-dirty]" (`fireEvent.change` to the browser midpoint from an initial `0` marks dirty).
- **SLD-STA-03** — MUST. The normalized `values` array MUST be computed from the raw value as: single mode → `[clamp(raw, min, max)]`; range mode → every element clamped to `[min, max]` and then sorted ascending (`a − b`). All display (thumb position, `aria-valuenow`, input `value`, `Slider.Value` text, form values) MUST use the normalized values, never the raw value.
  Source: `slider/root/SliderRoot.tsx:165-174`, `slider/utils/asc.ts:1-3`, `packages/utils/src/clamp.ts:1-7`. Test: [L1] `SliderRoot.test.tsx` "clamps range values that fall outside the min and max bounds", "submits clamped range slider values to onFormSubmit", "submits a clamped single-thumb slider value to onFormSubmit", "supports non-integer values"; `SliderThumb.test.tsx` "thumb should not go out of bounds when the controlled value goes out of bounds".
- **SLD-STA-04** — MUST. A one-element array raw value MUST keep the slider in range mode: candidates computed from pointer or input interaction MUST be one-element arrays, `fieldValue` MUST be the array, and callbacks MUST receive arrays.
  Source: `slider/root/SliderRoot.tsx:165,174,230`, `slider/control/SliderControl.tsx:96,206`, `slider/thumb/SliderThumb.tsx:364`. Test: [L2] `SliderControl.test.tsx` "preserves single-element array values on track press"; `SliderRoot.test.tsx` "validationMode=onBlur preserves single-element array values" (L4 for the pointer clause).
- **SLD-STA-06** — MUST. `activeThumbIndex` MUST become the index of a thumb when its input receives focus, and `-1` when that input blurs, when a pointer/touch interaction ends (SLD-PTR-11), and when the slider becomes disabled (SLD-FOC-05). It is exposed only through the state object and the thumb stacking order (SLD-DOM-12); there is no `data-active` attribute.
  Source: `slider/root/SliderRoot.tsx:140,151-157,269-271`, `slider/thumb/SliderThumb.tsx:338,351`, `slider/control/SliderControl.tsx:356`. Test: [L4] `SliderThumb.test.tsx` "keeps the most recently active thumb on top after focus moves away".
- **SLD-STA-07** — MUST. The *last-used thumb index* MUST be updated to every non-negative value assigned to `activeThumbIndex` and MUST NOT be reset to `-1`; it is `-1` until a thumb has been active. It is observable only through SLD-DOM-12.
  Source: `slider/root/SliderRoot.tsx:141,151-157`, `slider/thumb/SliderThumb.tsx:201-202`. Test: [L4] `SliderThumb.test.tsx` "keeps the most recently active thumb on top after focus moves away", "relies on DOM order before any thumb is used".
- **SLD-STA-08** — MUST. `dragging` MUST be `true` from a handled `pointerdown` on the Control (SLD-PTR-08), or — for a gesture that began with a `touchstart` not preceded by a `pointerdown` — from the third tracked move event (SLD-PTR-14), until the interaction ends (SLD-PTR-11); otherwise `false`.
  Source: `slider/root/SliderRoot.tsx:142`, `slider/control/SliderControl.tsx:29,343-345,357,535`. Test: [L4] `SliderRoot.test.tsx` "should not apply data-dragging for click modality", "should apply data-dragging for dragging modality"; `SliderControl.test.tsx` "commits and ends a %s drag on pointercancel".
- **SLD-STA-09** — MUST. A candidate value MUST be dropped without any callback when it is `NaN` (single mode) or equal to the *raw* current value — identity for numbers, or same length with `Object.is` element equality for arrays. Because the comparison is against the raw value, a candidate equal to the normalized value but not to an unsorted/out-of-range raw value MUST still be reported.
  Source: `slider/root/SliderRoot.tsx:39-47,195-197`, `packages/utils/src/areArraysEqual.ts:9-27`. Test: [L2] `SliderRoot.test.tsx` "should fire only when the value changes", "is called even if the $name did not change" (`readonly range` / `range`), "does not commit when keyboard interaction leaves the value unchanged" (L4 for the drag and keyboard citations).
- **SLD-STA-10** — MUST. In component-owned mode, a candidate that was reported and not canceled MUST become the raw value exactly as computed (single: a number; range: the resolved array, already sorted and bounded).
  Source: `slider/root/SliderRoot.tsx:214-224`. Test: [L2] `SliderRoot.test.tsx` "should update aria-valuenow", "should round value to step precision" (L4 for the cited keyboard and pointer tests).
- **SLD-STA-11** — MUST. In consumer-owned mode a reported candidate MUST NOT change the displayed value; the display MUST follow every change of the `value` prop, including changes unrelated to user interaction, re-normalizing (clamp/sort) each time.
  Source: `slider/root/SliderRoot.tsx:118-122`, `packages/utils/src/useControlled.ts:46,83-90`. Test: [L2] `SliderThumb.test.tsx` "positions the thumb when the controlled value changes externally" (single / multiple), "thumb should not go out of bounds when the controlled value goes out of bounds"; `SliderRoot.test.tsx` "revalidates when the controlled value changes externally" (L4 for the thumb-position citations).
- **SLD-STA-12** — MUST. `fieldValue` — the value registered with the field (SLD-FRM-02), passed to `validate`, projected into the form's values object and compared for `dirty` — MUST be the normalized array in range mode and `values[0]` in single mode.
  Source: `slider/root/SliderRoot.tsx:174,176,178-191`. Test: [L2] `SliderRoot.test.tsx` "receives an array value for range sliders", "validates once when changed by the user", "submits a clamped single-thumb slider value to onFormSubmit".

### 4.1 Value algorithms

These procedures are pure and MUST be reproduced exactly; every interaction in §6 and §7 is expressed in terms of them. Numbers are IEEE-754 doubles; `clamp(v, lo, hi) = max(lo, min(v, hi))` (UTL-EDGE-14); `toFixed(n)` is the JavaScript `Number.prototype.toFixed` followed by parsing back to a number.

- **SLD-STA-13** — MUST. **Decimal precision.** `getDecimalPrecision(num)`:
  ```
  if num == 0: return 0
  if |num| < 1:
      // exponential form avoids "1e-8" losing its digits
      [mantissa, exp] = num.toExponential().split('e-')   // e.g. "5e-8" → ["5", "8"]; "1.5e-7" → ["1.5", "7"]
      fractionDigits = length of the part of mantissa after '.' (0 if none)
      return fractionDigits + parseInt(exp)
  decimalPart = num.toString().split('.')[1]
  return decimalPart ? decimalPart.length : 0
  ```
  Source: `slider/utils/roundValueToStep.ts:1-16`. Test: [L3] `SliderRoot.test.tsx` "should not fail to round value to step precision when step is very small", "should not fail to round value to step precision when step is very small and negative".
- **SLD-STA-14** — MUST. **Step rounding.** `roundValueToStep(value, step, min)`:
  ```
  nearest = round((value − min) / step) × step + min      // round = JavaScript Math.round (half up)
  return toFixed(nearest, max(getDecimalPrecision(step), getDecimalPrecision(min)))
  ```
  The grid is anchored at `min`; the result is not clamped (callers clamp).
  Source: `slider/utils/roundValueToStep.ts:18-21`. Test: [L3] `roundValueToStep.test.ts` "preserves precision from the step origin" (`0.35, 0.1, 0.25 → 0.35`), "preserves decimal precision for steps greater than one" (`13.2, 1.5, 10.2 → 13.2`); `SliderRoot.test.tsx` "should round value to step precision", "should reach right edge value" (`max` not on the grid is unreachable: `6..108` step `10` → `106`).
- **SLD-STA-15** — MUST. **Keyboard step.** `getNewValue(thumbValue, increment, direction, min, max)` (`direction` is `+1`/`−1`):
  ```
  v = thumbValue + increment × direction
  v = toFixed(v, max(getDecimalPrecision(thumbValue), getDecimalPrecision(increment), getDecimalPrecision(min)))
  return clamp(v, min, max)
  ```
  The caller passes `thumbValue` already rounded by SLD-STA-14; the increment itself is NOT snapped to the `step` grid (a `largeStep` of `5` from `20` with `step` `2` yields `25`).
  Source: `slider/thumb/SliderThumb.tsx:63-81,384,423`. Test: [L3] `SliderRoot.test.tsx` "preserves largeStep increments when step uses a different grid", "rounds fractional values to the configured step", "keypresses should correct invalid values".
- **SLD-STA-16** — MUST. **Neighbour clamp (keyboard / native input).** `getSliderValue(input, index, min, max, range, values)`:
  ```
  clamped = clamp(input, min, max)
  if not range: return clamped
  out = copy of values
  out[index] = clamp(clamped, values[index − 1] ?? −∞, values[index + 1] ?? +∞)
  return out sorted ascending
  ```
  Source: `slider/utils/getSliderValue.ts:4-22`. Test: [L3] `getSliderValue.test.ts` "clamps a single value to the min/max range", "does not let a thumb cross a neighbour whose value is 0", "bounds a thumb between its real neighbours", "leaves the outer edges unbounded by missing neighbours"; `SliderThumb.test.tsx` "does not change the value beyond min and max".
- **SLD-STA-17** — MUST. **Minimum distance.** `validateMinimumDistance(candidate, step, minStepsBetweenValues)` is `true` for a number; for an array it is `true` iff for every adjacent pair `|a[i] − a[i+1]| >= step × minStepsBetweenValues` (a `NaN` element makes it `false`). A candidate that fails MUST be discarded entirely (no partial move, no callback).
  Source: `slider/utils/validateMinimumDistance.ts:1-17`, `slider/root/SliderRoot.tsx:232`, `slider/control/SliderControl.tsx:342`. Test: [L3] `SliderRoot.test.tsx` "should enforce a minimum difference between range slider values"; `SliderControl.test.tsx` "ignores a drag when collision behavior cannot satisfy the minimum distance"; `SliderThumb.test.tsx` "does not commit NaN when more thumbs are rendered than values".
- **SLD-STA-18** — MUST. **Push.** `getPushedThumbValues(values, index, nextValue, min, max, step, minSteps, initialValues?)`:
  ```
  out = copy of values; d = step × minSteps; last = out.length − 1
  base = initialValues ?? values
  out[index] = clamp(nextValue, min + index × d, max − (last − index) × d)
  for i = index + 1 .. last:                       // push later thumbs forward
      minAllowed = out[i − 1] + d
      maxAllowed = max − (last − i) × d
      candidate = max(out[i], minAllowed)
      if base[i] < candidate: candidate = max(base[i], minAllowed)   // let a pushed thumb return toward its baseline
      out[i] = clamp(candidate, minAllowed, maxAllowed)
  for i = index − 1 .. 0:                          // push earlier thumbs backward
      maxAllowed = out[i + 1] − d
      minAllowed = min + i × d
      candidate = min(out[i], maxAllowed)
      if base[i] > candidate: candidate = min(base[i], maxAllowed)
      out[i] = clamp(candidate, minAllowed, maxAllowed)
  for each i: out[i] = toFixed(out[i], 12)
  return out
  ```
  Source: `slider/utils/getPushedThumbValues.ts:10-60`. Test: [L3] `getPushedThumbValues.test.ts` "pushes the next thumb forward when moving past it", "ensures minimum distance between thumbs while pushing forward", "pushes previous thumbs backward when moving before them", "pushes multiple thumbs in sequence", "allows fractional minimum distances", "restores pushed thumbs towards their initial value when space allows"; `SliderThumb.test.tsx` "pushes adjacent thumbs forward when set to "push"".
- **SLD-STA-19** — MUST. **Collision resolution (pointer).** `resolveThumbCollision(behavior, values, currentValues, initialValues, pressedIndex, nextValue, min, max, step, minSteps)` returns `{ value, thumbIndex, didSwap }`:
  ```
  active   = currentValues ?? values          // values as of the last applied change in this gesture
  baseline = initialValues ?? values          // values when the gesture started
  if active.length <= 1: return { value: nextValue, thumbIndex: 0, didSwap: false }
  d = step × minSteps
  if behavior == 'push':
      return { value: getPushedThumbValues(active, pressedIndex, nextValue, min, max, step, minSteps),
               thumbIndex: pressedIndex, didSwap: false }
  // 'swap' and 'none' share the neighbour clamp
  cand = copy of active
  prev = cand[pressedIndex − 1]; next = cand[pressedIndex + 1]
  lower = prev != null ? prev + d : min
  upper = next != null ? next − d : max
  pressedClamped = toFixed(clamp(nextValue, lower, upper), 12)
  cand[pressedIndex] = pressedClamped
  if behavior == 'none' (or unknown): return { value: cand, thumbIndex: pressedIndex, didSwap: false }
  // behavior == 'swap'
  start = active[pressedIndex]; eps = 1e-7
  swapFwd = nextValue > start and next != null and nextValue >= next − eps
  swapBwd = nextValue < start and prev != null and nextValue <= prev + eps
  if not swapFwd and not swapBwd: return { value: cand, thumbIndex: pressedIndex, didSwap: false }
  target = swapFwd ? pressedIndex + 1 : pressedIndex − 1
  basePush = for each i: i == pressedIndex ? pressedClamped : (baseline[i] ?? active[i])
  targetNext = swapFwd ? max(nextValue, cand[target]) : min(nextValue, cand[target])
  adjusted = getPushedThumbValues(cand, target, targetNext, min, max, step, minSteps, basePush)
  neighbour = swapFwd ? target − 1 : target + 1          // the slot the pressed thumb vacated
  lo = adjusted[neighbour − 1] != null ? adjusted[neighbour − 1] + d : min
  lo = max(lo, min + neighbour × d)
  hi = adjusted[neighbour + 1] != null ? adjusted[neighbour + 1] − d : max
  hi = min(hi, max − (adjusted.length − 1 − neighbour) × d)
  adjusted[neighbour] = toFixed(clamp(pressedClamped, lo, hi), 12)
  return { value: adjusted, thumbIndex: target, didSwap: true }
  ```
  Source: `slider/utils/resolveThumbCollision.ts:15-154`. Test: [L3] `resolveThumbCollision.test.ts` (all eleven: "prevents thumbs from passing each other when behavior is "none"", "pushes thumbs forward without cling when behavior is "push"", "keeps pushed thumbs in place when moving backward in push mode", "swaps thumbs when behavior is "swap"", "maintains swap continuity with minimum steps when provided current and initial values", "does not swap before reaching neighbour value with minimum steps", "swaps once reaching the neighbour value with minimum steps", "does not swap backward before reaching neighbour value with minimum steps", "swaps backward once reaching the neighbour value with minimum steps", "does not move the clamped neighbour when swapping across with minimum steps", "uses current values when a controlled range grows during a swap interaction", "returns a scalar when the live values shrink to one item during an interaction"); `SliderThumb.test.tsx` "prevents thumbs from passing each other when set to "none"", "allows thumbs to swap when set to "swap"", "maintains minimum steps between values when swapping".
- **SLD-STA-20** — MUST. **Value → percent.** `valueToPercent(value, min, max) = (value − min) × 100 / (max − min)` (UTL-DOM-31), applied to the normalized (clamped) value; the result is non-finite when `max === min`.
  Source: `slider/thumb/SliderThumb.tsx:196`, `slider/indicator/SliderIndicator.tsx:71-72`, `utils/valueToPercent.ts:1-3`. Test: [L1] `SliderThumb.test.tsx` "positions the thumb when dragged" (single / multiple), "server-side rendering" "single thumb" (L4 for the drag citation).
- **SLD-STA-21** — MUST. Within a `Field.Root`, after every change of `fieldValue` (post-commit, pre-paint; a change is a strict-inequality difference, so a new array identity counts), the field's `dirty` MUST be set to: arrays → `!areArraysEqual(fieldValue, initialValue)`; otherwise `fieldValue !== initialValue`, where `initialValue` is the value captured at first registration (FFI-STA-04). This write happens **after** the validation change notification (SLD-FRM-04), unlike other controls (§16).
  Source: `slider/root/SliderRoot.tsx:178-191`, `internals/useValueChanged.ts:6-17`. Test: [L2] `SliderRoot.test.tsx` "[data-dirty]", "[data-dirty] with a range value".
- **SLD-STA-22** — MUST. Slider MUST NOT write the field's `filled` flag at any time (there is no call to the field's `setFilled`); consequently a `Field.Root` containing only a slider never renders `data-filled`, and the state object's `filled` stays at the field's default `false`.
  Source: `slider/root/SliderRoot.tsx:100-108` (destructures `setTouched`/`setDirty` only; no `setFilled` anywhere under `slider/`). Test: none (no test asserts `data-filled` for Slider).
- **SLD-STA-23** — MUST. The slider MUST remember the `reason` of the most recently *applied* change (initially `'none'`); it is the reason reported by `onValueCommitted` when a pointer/touch interaction ends (SLD-EVT-05).
  Source: `slider/root/SliderRoot.tsx:135,220`, `slider/control/SliderControl.tsx:370-374`. Test: [L4] `SliderRoot.test.tsx` "single value" (`track-press`), "onValueCommitted is called with the same value as the latest onValueChange when pointerUp occurs at a different location than onValueChange" (`drag`).

## 5. Rendered DOM and ARIA

### 5.1 Root element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"group"` (consumer `role` wins) | SLD-DOM-01, SLD-ARIA-10 | C# |
| `id` | prop or generated | SLD-API-13 | C# |
| `aria-labelledby` | per SLD-ARIA-07 | SLD-ARIA-07 | C# |
| `aria-describedby` | consumer ids + Field message ids | SLD-ARIA-09 | C# |
| `aria-invalid` | `"true"` while the field is invalid and not disabled | SLD-ARIA-09 | C# |
| state attributes | §3.1 table | SLD-DOM-02 | C# |

- **SLD-DOM-01** — MUST. The root element MUST carry `role="group"`, its `id` (SLD-API-13) and `aria-labelledby` (SLD-ARIA-07), followed by the consumer's forwarded attributes (a consumer `role` replaces `"group"`), followed by the field validation attributes (SLD-ARIA-09). It MUST have no `tabindex`, no handlers of its own, and no CSS variables.
  Source: `slider/root/SliderRoot.tsx:376-389`. Test: [L2] `SliderRoot.test.tsx` "sets the data-orientation attribute" (`getByRole('group')`), "has the correct aria attributes", "Field.Description".
- **SLD-DOM-02** — MUST. Every part (Root, Label, Value, Control, Track, Indicator, Thumb element) MUST render, from the shared state object: `data-dragging=""` while `dragging`; `data-orientation="horizontal"|"vertical"` always; `data-disabled=""` while effective disabled; `data-touched`, `data-dirty`, `data-focused`, `data-filled` (`""`) while the corresponding field flag is `true`; `data-valid=""` when field `valid === true`; `data-invalid=""` when `valid === false`; and MUST NOT render any attribute for `activeThumbIndex`, `max`, `min`, `minStepsBetweenValues`, `step` or `values`.
  Source: `slider/root/stateAttributesMapping.ts:5-15`, `internals/field-constants/constants.ts:34-44`, `internals/getStateAttributesProps.ts:5-32`; applied at `slider/root/SliderRoot.tsx:388`, `slider/label/SliderLabel.tsx:58`, `slider/value/SliderValue.tsx:57`, `slider/control/SliderControl.tsx:557`, `slider/track/SliderTrack.tsx:34`, `slider/indicator/SliderIndicator.tsx:87`, `slider/thumb/SliderThumb.tsx:495`. Test: [L2] `SliderRoot.test.tsx` "should render data-disabled on all subcomponents", "sets the data-orientation attribute", "[data-touched]", "[data-dirty]", "[data-focused]", "validationMode=onSubmit" (`data-invalid`/`data-valid` on root and thumb); `SliderControl.test.tsx` "commits and ends a %s drag on pointercancel" (`data-dragging`) (L4 for the `data-dragging` citation).

### 5.2 Control element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-base-ui-slider-control` | `""` only when `thumbAlignment="edge"` | SLD-DOM-03 | C# |
| `tabindex`, `role` | none | SLD-DOM-03 | C# |
| state attributes | §3.1 table | SLD-DOM-02 | C# |

- **SLD-DOM-03** — MUST. The Control element MUST carry no `tabindex` and no `role`; it MUST carry `data-base-ui-slider-control=""` exactly when `thumbAlignment` is `'edge'` (the hook the pre-hydration script uses to find the control), and it MUST attach the pointer handling of §6.2 (a `pointerdown` handler merged before the consumer's, plus a native passive `touchstart` listener).
  Source: `slider/control/SliderControl.tsx:467-469,489-491`. Test: [L2] `SliderControl.test.tsx` "does not apply a tabIndex by default"; `SliderThumb.test.tsx` "renders the inline pre-hydration script for edge-aligned thumbs" (script resolves the control through the marker) (L4 for the pointer-handling clause).

### 5.3 Track element

- **SLD-DOM-04** — MUST. The Track element MUST render the inline style `position: relative` as its own contribution, overridable key-by-key by the consumer's `style` (RND-DOM-16), and the state attributes of SLD-DOM-02; nothing else.
  Source: `slider/track/SliderTrack.tsx:23-35`. Test: [L2] `SliderTrack.test.tsx` conformance suite.

### 5.4 Indicator element

Let `start` = the first thumb's percent and `end` = the last thumb's percent: in `'center'` mode these are `valueToPercent(values[0])` and `valueToPercent(values[last])`; in the inset modes they are the inset positions computed by the first/last thumb (SLD-ANI-01), `undefined` until computed. `range` here means `values.length > 1`.

| Style / attribute | Value | Req | Writer |
|---|---|---|---|
| `position` | `relative` (horizontal) / `absolute` (vertical) | SLD-DOM-05 | C# → JS |
| `height` (horizontal) / `width` (vertical) | `inherit` | SLD-DOM-05 | C# → JS |
| `inset-inline-start` (horizontal) / `bottom` (vertical) | range: `<start>%` (`var(--start-position)` inset); single: `0` | SLD-DOM-05/06 | C# → JS |
| `width` (horizontal) / `height` (vertical) | range: `<end − start>%` (`var(--relative-size)` inset); single: `<start>%` (`var(--start-position)` inset) | SLD-DOM-05/06 | C# → JS |
| `--start-position` | inset only: `<start ?? 0>%` | SLD-DOM-06 | C# → JS |
| `--relative-size` | inset + range only: `<(end ?? 0) − (start ?? 0)>%` | SLD-DOM-06 | C# → JS |
| `visibility` | `hidden` while inset positions are unknown or (edge mode) during hydration | SLD-DOM-06 | C# → JS |
| `data-base-ui-slider-indicator` | `""` only when `thumbAlignment="edge"` | SLD-DOM-07 | C# |

- **SLD-DOM-05** — MUST. In `'center'` mode the Indicator MUST render inline styles: `position: relative` for horizontal / `absolute` for vertical; the cross-axis size (`height` horizontal, `width` vertical) `inherit`; the start inset (`insetInlineStart` horizontal, `bottom` vertical) `<start>%` in range mode and `0` in single mode; the main-axis size (`width` horizontal, `height` vertical) `<end − start>%` in range mode and `<start>%` in single mode; no `visibility` and no custom properties. The consumer's `style` merges over these. Numeric values in these styles are serialized per UTL-DOM-37.
  Source: `slider/indicator/SliderIndicator.tsx:11-45,67-74`; see UTL-DOM-37. Test: [L4] `SliderIndicator.test.tsx` "preserves keyboard and indicator parity for $axis center $valueShape sliders"; `SliderRoot.test.tsx` "should handle RTL". Docs: `CHANGELOG.md` v1.0.0-alpha.7 "Fix incorrect CSS position on vertical slider indicator".
- **SLD-DOM-06** — MUST. In the inset modes (`'edge'`, `'edge-client-only'`) the Indicator MUST additionally set `--start-position: <start ?? 0>%` and, in range mode, `--relative-size: <(end ?? 0) − (start ?? 0)>%`, and MUST express the start inset as `var(--start-position)` (range) and the main-axis size as `var(--relative-size)` (range) / `var(--start-position)` (single); it MUST render `visibility: hidden` while `start` is `undefined`, while range `end` is `undefined`, or (`'edge'` only) during the hydration pass, and no `visibility` otherwise. Numeric values in these styles are serialized per UTL-DOM-37.
  Source: `slider/indicator/SliderIndicator.tsx:19-43,59-74`; see UTL-DOM-37. Test: [L4] `SliderIndicator.test.tsx` "preserves keyboard and indicator parity for $axis edge $valueShape sliders"; `SliderThumb.test.tsx` "recomputes inset positions when the slider becomes visible", "recomputes range inset positions when the slider becomes visible".
- **SLD-DOM-07** — MUST. The Indicator MUST carry `data-base-ui-slider-indicator=""` exactly when `thumbAlignment` is `'edge'`.
  Source: `slider/indicator/SliderIndicator.tsx:81`. Test: none (used by the script of SLD-ANI-07).

### 5.5 Thumb element

| Attribute / style | Value | Req | Writer |
|---|---|---|---|
| `id` | prop or generated | SLD-API-25 | C# |
| `data-index` | the thumb's index | SLD-DOM-08 | C# |
| `position` | `absolute` | SLD-DOM-09 | C# → JS |
| `inset-inline-start` (horizontal) / `bottom` (vertical) | `<percent>%`, or `var(--position)` in inset modes | SLD-DOM-09/10 | C# → JS |
| `top` (horizontal) / `left` (vertical) | `50%` | SLD-DOM-09 | C# → JS |
| `translate` | horizontal LTR `-50% -50%`; horizontal RTL `50% -50%`; vertical `-50% 50%` | SLD-DOM-09 | C# → JS |
| `z-index` | `2`/`1` per SLD-DOM-12, else none | SLD-DOM-12 | C# → JS |
| `--position` | inset only: `<positionPercent ?? 0>%` | SLD-DOM-10 | C# → JS |
| `visibility` | inset: `hidden` until computed / during hydration (edge) | SLD-DOM-10 | C# → JS |
| (non-inset, non-finite percent) | visually-hidden style instead of the above | SLD-DOM-11 | C# → JS |
| state attributes | §3.1 table | SLD-DOM-02 | C# |

- **SLD-DOM-08** — MUST. The thumb element MUST carry `data-index` whose value is the thumb's index (SLD-API-17) as a decimal string.
  Source: `slider/thumb/SliderThumb.tsx:467`, `slider/thumb/SliderThumbDataAttributes.ts:4`. Test: [L2] `SliderThumb.test.tsx` "sets the thumb index data attribute".
- **SLD-DOM-09** — MUST. In `'center'` mode, when `thumbPercent` is finite, the thumb element MUST render inline styles `position: absolute`; `insetInlineStart` (horizontal) or `bottom` (vertical) = `<thumbPercent>%`; `top` (horizontal) or `left` (vertical) = `50%`; `translate` = `-50% -50%` for horizontal LTR, `50% -50%` for horizontal RTL, `-50% 50%` for vertical (either direction); and `zIndex` per SLD-DOM-12. The consumer's `style` merges over these. The thumb's position therefore follows the normalized value, not the pointer. Numeric values in these styles are serialized per UTL-DOM-37.
  Source: `slider/thumb/SliderThumb.tsx:273-274,287-305`; see UTL-DOM-37. Test: [L4] `SliderThumb.test.tsx` "positions the thumb when dragged" (single / multiple), "positions the thumb when the controlled value changes externally" (single / multiple), "thumb should not go out of bounds when the controlled value goes out of bounds"; `SliderRoot.test.tsx` "should handle RTL" (`insetInlineStart: 30%`). Docs: `CHANGELOG.md` v1.0.0-beta.0 "Position thumb based on value instead of pointer location when dragging".
- **SLD-DOM-10** — MUST. In the inset modes the start inset MUST be `var(--position)` and the thumb MUST set `--position: <positionPercent ?? 0>%` where `positionPercent` is computed per SLD-ANI-01; it MUST render `visibility: hidden` while `positionPercent` is unknown and, in `'edge'` mode, during the hydration pass; all other styles are as in SLD-DOM-09. Numeric values in these styles are serialized per UTL-DOM-37.
  Source: `slider/thumb/SliderThumb.tsx:291-304`; see UTL-DOM-37. Test: [L4] `SliderThumb.test.tsx` "recomputes inset positions when the slider becomes visible" (`--position` `0%`→`32%`, `visibility` `hidden`→`''`), "recomputes range inset positions when the slider becomes visible".
- **SLD-DOM-11** — MUST. In `'center'` mode, when `thumbPercent` is not finite (no value for this index, or `max === min`), the thumb element MUST render the shared visually-hidden style (UTL-DOM-13) instead of the positioning styles.
  Source: `slider/thumb/SliderThumb.tsx:288-289`, `packages/utils/src/visuallyHidden.ts:14-20`. Test: none.
- **SLD-DOM-12** — MUST. Stacking: in range mode the thumb whose index equals `activeThumbIndex` MUST render `z-index: 2`, else the thumb whose index equals the last-used index (SLD-STA-07, only when that index is within the current value count) MUST render `z-index: 1`; in single mode the active thumb renders `z-index: 1`; every other thumb renders no `z-index` of its own.
  Source: `slider/thumb/SliderThumb.tsx:201-202,276-285`. Test: [L4] `SliderThumb.test.tsx` "relies on DOM order before any thumb is used", "keeps the most recently active thumb on top after focus moves away".
- **SLD-DOM-13** — MUST. The thumb element MUST attach a `pointerdown` handler (SLD-PTR-02) merged before the consumer's, MUST carry no `role`, `tabindex` or ARIA attributes of its own (all ARIA lives on the input), and in `'edge'` mode its server-rendered inline style may differ from the client's first render without being treated as a mismatch (§14).
  Source: `slider/thumb/SliderThumb.tsx:479-491`. Test: [L2] `SliderThumb.test.tsx` "does not apply tabIndex to the thumb element by default" (L4 for the pointer-handling clause).

### 5.6 Nested `<input type="range">` (one per thumb)

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `type` | `"range"` | SLD-DOM-14 | C# |
| `id` | single mode: the labelable control id; range mode: a generated id per thumb | SLD-DOM-15 | C# |
| `name` | effective name (SLD-API-10) or absent | SLD-DOM-14 | C# |
| `form` | the `form` prop or absent | SLD-DOM-14 | C# |
| `min`, `max`, `step` | the props, verbatim | SLD-DOM-14 | C# |
| `value` | `String(values[index])`, `""` when there is no value | SLD-DOM-16 | C# |
| `disabled` | present while the thumb's effective disabled is `true` | SLD-API-16 | C# |
| `tabindex` | the Thumb's `tabIndex` prop or absent | SLD-API-23 | C# |
| `aria-*` | §5.9 | SLD-ARIA-01…06, 08 | C# |
| inline style | visually hidden, 100% × 100%, `writing-mode` for vertical | SLD-DOM-14, SLD-I18N-03 | C# |

- **SLD-DOM-14** — MUST. Each thumb MUST render exactly one `<input type="range">` carrying `min`, `max`, `step` (verbatim props), `name` and `form` (SLD-API-10), `disabled` (SLD-API-16), `tabindex` (SLD-API-23), the ARIA attributes of §5.9, the handlers of §6 (`change`, `focus`, `blur`, `keydown`), and the inline style `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; margin: 0; position: fixed; top: 0; left: 0; width: 100%; height: 100%` plus `writing-mode` per SLD-I18N-03 (the 100% box lets VoiceOver's focus ring match the thumb). It MUST NOT have `aria-hidden` or `role`: its native `slider` role is the accessible slider.
  Source: `slider/thumb/SliderThumb.tsx:315-458,471`, `packages/utils/src/visuallyHidden.ts:3-20`. Test: [L2] `SliderRoot.test.tsx` "has the correct aria attributes" (`tagName INPUT`, `step`), "sets the max attribute on the input", "sets the min attribute on the input"; `SliderThumb.test.tsx` "single thumb" (focus and blur: `type="range"`), "handles non-integer values" (`step="1e-8"`). Docs: `CHANGELOG.md` v1.0.0-beta.2 "Instead of the thumb div, the `input type="range"` element receives focus".
- **SLD-DOM-15** — MUST. In single mode the input's `id` MUST be the id of the enclosing labelable scope's control (the `Field.Root`'s control id, so a `Field.Label` `for` targets it; a generated id outside a Field); in range mode each input MUST have its own generated id, distinct and non-empty, and none of them claims the labelable scope's control id.
  Source: `slider/thumb/SliderThumb.tsx:178-180,328`, `internals/labelable-provider/useLabelableId.ts:10-83`, `internals/labelable-provider/LabelableProvider.tsx:12-20`. Test: [L2] `SliderValue.test.tsx` "associates the output with every thumb input" (distinct, non-empty); `SliderLabel.test.tsx` "focuses the registered thumb when composed within a Field"; `SliderRoot.test.tsx` "Field.Label".
- **SLD-DOM-16** — MUST. The input's `value` MUST track `values[index]` on every render (the number serialized per UTL-DOM-37; `""` when the index has no value), so the browser's `valueAsNumber` matches the displayed value; the input is never left in an uncontrolled state.
  Source: `slider/thumb/SliderThumb.tsx:454`; see UTL-DOM-37. Test: [L2] `SliderRoot.test.tsx` "should update aria-valuenow"; `SliderThumb.test.tsx` "handles change events".

### 5.7 Value `<output>` element

- **SLD-DOM-17** — MUST. `Slider.Value` MUST render an `<output>` whose `for` attribute is the space-joined list of every registered thumb's input id in index order (absent while no thumb is registered, i.e. in server output and before the first registry publication), and `aria-live` per SLD-API-26.
  Source: `slider/value/SliderValue.tsx:29-34,50-53`, `slider/thumb/SliderThumb.tsx:182-191,501-503`. Test: [L2] `SliderValue.test.tsx` "associates the output with every thumb input".
- **SLD-DOM-18** — MUST. When `children` is not a function, the output's text MUST be every normalized value formatted per SLD-API-09, joined with `" – "` (space, EN DASH U+2013, space); a single value renders without separator.
  Source: `slider/value/SliderValue.tsx:36-41,51-52`. Test: [L2] `SliderValue.test.tsx` "renders a single value", "renders a range", "renders all thumb values"; `SliderRoot.test.tsx` "formats the value", "formats range values".

### 5.8 Label element

- **SLD-DOM-19** — MUST. `Slider.Label` MUST render its element with `id` = `${rootId}-label`, a `click` handler and a `pointerdown` handler (SLD-FOC-06, FFI-PTR-01), and MUST publish that id (post-commit, pre-paint; withdrawn on unmount only if still current, FFI-ARIA-08) both to the Root (for SLD-ARIA-06/07) and to the enclosing labelable scope. It renders no `for` attribute (it is a non-native label).
  Source: `slider/label/SliderLabel.tsx:30,48-59`, `internals/labelable-provider/useLabel.ts:10-77`, `utils/useRegisteredLabelId.ts:6-20`. Test: [L2] `SliderRoot.test.tsx` "Slider.Label", "updates Slider.Label linkage when root id changes", "does not link Slider.Label before hydration".

### 5.9 Pre-hydration `<script>` (`thumbAlignment="edge"` only)

- **SLD-DOM-20** — MUST. The script element of SLD-STR-06 MUST contain the inline program of SLD-ANI-07 as its text, carry the CSP provider's `nonce` when one is configured (UTL-DOM-34), and exist only in server output and during the hydration pass.
  Source: `slider/thumb/SliderThumb.tsx:473-475`, `slider/thumb/prehydrationScript.min.ts:5`, `internals/PrehydrationScript.tsx:32-49`. Test: [L4] `SliderThumb.test.tsx` "renders the inline pre-hydration script for edge-aligned thumbs", "renders a single pre-hydration script with the last thumb".
- **SLD-DOM-21** — MUST. Attribute precedence on every part: the part's own attributes first, the consumer's forwarded attributes replacing them key-by-key (RND-DOM-19), except the consumed props of §3; on the Root the field validation composition runs last (SLD-ARIA-09); on the thumb input, the consumer's `onFocus`/`onBlur`/`onKeyDown` are merged last and therefore run first (RND-EVT-02).
  Source: `slider/root/SliderRoot.tsx:379-387`, `slider/thumb/SliderThumb.tsx:315-458,465-494`, `slider/control/SliderControl.tsx:488-556`. Test: [L2] `SliderRoot.test.tsx` conformance suite; `SliderThumb.test.tsx` "allows preventing the internal key handling"; `SliderRoot.test.tsx` "handles a touch press normally after a cancelled %s gesture" (consumer `onPointerDown` runs first) (L4 for the thumb-input handler-order clause).

### 5.10 ARIA

The accessible slider is the native `<input type="range">` in each thumb; no part carries an explicit `role="slider"`.

| Element | Attribute | Value | Req | Writer |
|---|---|---|---|---|
| input | (implicit role) | `slider` — native | SLD-ARIA-01 | C# |
| input | `aria-valuenow` | `values[index]` (absent when no value) | SLD-ARIA-02 | C# |
| input | `aria-orientation` | `orientation` | SLD-ARIA-03 | C# |
| input | `aria-valuetext` | `getAriaValueText(...)` → `aria-valuetext` prop → default text | SLD-ARIA-04 | C# |
| input | `aria-label` | `getAriaLabel(index)` → `aria-label` prop | SLD-ARIA-05 | C# |
| input | `aria-labelledby` | Thumb prop → (no `aria-label`) root's resolved label id → absent | SLD-ARIA-06 | C# |
| root | `aria-labelledby` | prop → Field label id → Slider.Label id → absent | SLD-ARIA-07 | C# |
| input | `aria-describedby`, `aria-invalid` | field composition | SLD-ARIA-08 | C# |
| root | `aria-describedby`, `aria-invalid` | field composition | SLD-ARIA-09 | C# |
| root | `role` | `group` | SLD-ARIA-10 | C# |
| input | `disabled` (no `aria-disabled`) | native | SLD-ARIA-11 | C# |

- **SLD-ARIA-01** — MUST. Each thumb's input MUST expose the native `slider` role (an `<input type="range">` without a `role` attribute); the thumb element and the Control MUST NOT carry `role="slider"`, so exactly one accessible slider exists per value.
  Source: `slider/thumb/SliderThumb.tsx:453,471`. Test: [L2] `SliderRoot.test.tsx` "has the correct aria attributes" (`getByRole('slider').tagName === 'INPUT'`); `SliderThumb.test.tsx` "single thumb" / "multiple thumbs" (focus and blur).
- **SLD-ARIA-02** — MUST. The input's `aria-valuenow` MUST equal the normalized value of its index (serialized per UTL-DOM-37, e.g. `5e-8`), updated on every change; it MUST be absent when the index has no value.
  Source: `slider/thumb/SliderThumb.tsx:195,321`; see UTL-DOM-37. Test: [L2] `SliderRoot.test.tsx` "should update aria-valuenow", "supports non-integer values"; `SliderThumb.test.tsx` "handles non-integer values".
- **SLD-ARIA-03** — MUST. The input's `aria-orientation` MUST equal the `orientation` prop.
  Source: `slider/thumb/SliderThumb.tsx:320`. Test: [L2] `SliderRoot.test.tsx` "sets the `aria-orientation` attribute", "has the correct aria attributes".
- **SLD-ARIA-04** — MUST. The input's `aria-valuetext` MUST be resolved as in SLD-API-19; the **default** text is: for exactly two values, `"<formatted> start range"` for index `0` and `"<formatted> end range"` for index `1`; otherwise `<formatted>` when a `format` prop is given, else absent (`index < 0` → absent). `<formatted>` is the value formatted per SLD-API-09 and MUST update when `format`/`locale` change.
  Source: `slider/thumb/SliderThumb.tsx:46-61,322-325`. Test: [L2] `SliderRoot.test.tsx` "should set default aria-valuetext on range slider thumbs", "formats the value", "recomputes the thumb aria text when the format option changes", "formats range values"; `SliderThumb.test.tsx` "prefers getAriaValueText over a direct aria-valuetext prop".
- **SLD-ARIA-05** — MUST. The input's `aria-label` MUST be resolved as in SLD-API-18 and, when present, MUST suppress the root-derived `aria-labelledby` fallback on that input (SLD-ARIA-06).
  Source: `slider/thumb/SliderThumb.tsx:312-318`. Test: [L2] `SliderRoot.test.tsx` "does not set aria-labelledby when getAriaLabel is provided", "does not set fallback aria-labelledby when no label is rendered".
- **SLD-ARIA-06** — MUST. The input's `aria-labelledby` MUST be the Thumb's `aria-labelledby` prop when given; otherwise, when the input has no `aria-label` (SLD-ARIA-05), the root's resolved label id (SLD-ARIA-07); otherwise absent.
  Source: `slider/thumb/SliderThumb.tsx:318`. Test: [L2] `SliderRoot.test.tsx` "has the correct aria attributes", "Field.Label", "Slider.Label", "does not set aria-labelledby when getAriaLabel is provided"; `SliderThumb.test.tsx` "forwards aria-labelledby to the input".
- **SLD-ARIA-07** — MUST. The root's resolved label id MUST be: the root's `aria-labelledby` prop when given; else the enclosing labelable scope's (Field) label id; else the `Slider.Label` id once it has been published (FFI-ARIA-07); else absent. It is rendered as the root's `aria-labelledby` and reused by SLD-ARIA-06; it MUST follow runtime changes of the root id (SLD-API-13).
  Source: `slider/root/SliderRoot.tsx:109-112,313,381`, `utils/resolveAriaLabelledBy.ts:7-12`. Test: [L2] `SliderRoot.test.tsx` "has the correct aria attributes", "Field.Label", "Slider.Label", "updates Slider.Label linkage when root id changes".
- **SLD-ARIA-08** — MUST. The input's `aria-describedby` MUST be the Thumb's `aria-describedby` prop composed with the field's message ids (FFI-ARIA-01, FFI-ARIA-03) and the input MUST carry `aria-invalid="true"` while the field is invalid and neither the field nor the thumb is disabled; `aria-invalid` MUST be removed as soon as the field becomes valid or an external error is cleared (SLD-FRM-14).
  Source: `slider/thumb/SliderThumb.tsx:319,456`, `field/root/useFieldValidation.ts:367-376`. Test: [L2] `SliderRoot.test.tsx` "clears external errors on change", "validationMode=onSubmit", "validationMode=onBlur", "validationMode=onChange"; `SliderThumb.test.tsx` "forwards aria-describedby to the input".
- **SLD-ARIA-09** — MUST. The root element MUST also pass through the field's validation composition: its `aria-describedby` is the consumer's root `aria-describedby` followed by the field's message ids (de-duplicated, space-joined), and it carries `aria-invalid="true"` under the same condition as SLD-ARIA-08 (with the root's disabled state).
  Source: `slider/root/SliderRoot.tsx:386`. Test: [L2] `SliderRoot.test.tsx` "Field.Description" (`"external-description <descriptionId>"`), "validationMode=onSubmit" (`data-invalid` on root).
- **SLD-ARIA-10** — MUST. The root MUST carry `role="group"` (unless the consumer supplies a `role`), grouping the thumb sliders under the shared label.
  Source: `slider/root/SliderRoot.tsx:383`. Test: [L2] `SliderRoot.test.tsx` "sets the data-orientation attribute".
- **SLD-ARIA-11** — MUST. Disabled state is conveyed by the native `disabled` attribute on each input only; no part renders `aria-disabled`, `aria-readonly` or `aria-required` (the slider has no `readOnly`/`required` parameters).
  Source: `slider/thumb/SliderThumb.tsx:326`. Test: [L2] `SliderRoot.test.tsx` "does not drag a thumb disabled via the `disabled` prop" (`toBeDisabled`).
- **SLD-ARIA-12** — MUST. Server-rendered markup MUST contain the Label id and the Root id but MUST NOT contain the Slider.Label-derived `aria-labelledby` on the root or the inputs (the label publishes its id only after it is in the DOM); the association appears after hydration (FFI-FRM-34). An explicit `aria-labelledby` prop or a Field label id present at render time is rendered on the server.
  Source: `slider/root/SliderRoot.tsx:110-112`, `utils/useRegisteredLabelId.ts:12-17`. Test: [L4] `SliderRoot.test.tsx` "does not link Slider.Label before hydration".

## 6. Interactions

### 6.1 Keyboard

All keyboard handling is on the thumb's `<input>` (`keydown`). `step`/`largeStep` are the props; "neighbour" means the adjacent value in range mode. The handled set is <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Home</kbd>, <kbd>End</kbd>, <kbd>PageUp</kbd>, <kbd>PageDown</kbd>.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowUp</kbd> | thumb input focused; any orientation/direction | +`step` (`largeStep` with <kbd>Shift</kbd>) | SLD-KEY-02, SLD-KEY-03 |
| <kbd>ArrowDown</kbd> | any orientation/direction | −`step` (`largeStep` with <kbd>Shift</kbd>) | SLD-KEY-02, SLD-KEY-03 |
| <kbd>ArrowRight</kbd> | LTR (any orientation) | +`step` / +`largeStep` with Shift | SLD-KEY-02, SLD-KEY-03 |
| <kbd>ArrowRight</kbd> | RTL (any orientation) | −`step` / −`largeStep` with Shift | SLD-KEY-02, SLD-KEY-03 |
| <kbd>ArrowLeft</kbd> | LTR | −`step` / −`largeStep` with Shift | SLD-KEY-02, SLD-KEY-03 |
| <kbd>ArrowLeft</kbd> | RTL | +`step` / +`largeStep` with Shift | SLD-KEY-02, SLD-KEY-03 |
| <kbd>PageUp</kbd> | any | +`largeStep` (Shift has no extra effect) | SLD-KEY-04 |
| <kbd>PageDown</kbd> | any | −`largeStep` | SLD-KEY-04 |
| <kbd>Home</kbd> | single | value = `min` | SLD-KEY-05 |
| <kbd>Home</kbd> | range | value = `values[index−1] + step × minStepsBetweenValues`, or `min` for the first thumb | SLD-KEY-05 |
| <kbd>End</kbd> | single | value = `max` | SLD-KEY-05 |
| <kbd>End</kbd> | range | value = `values[index+1] − step × minStepsBetweenValues`, or `max` for the last thumb | SLD-KEY-05 |
| any handled key, `defaultPrevented` by the consumer's `onKeyDown` | — | ignored | SLD-KEY-01 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | — | not intercepted; native focus movement between thumb inputs and beyond | SLD-KEY-09, SLD-KEY-10 |
| any other key (<kbd>Enter</kbd>, <kbd>Space</kbd>, digits, …) | — | not intercepted; consumer `onKeyDown` still called | SLD-KEY-09 |

- **SLD-KEY-01** — MUST. A `keydown` on a thumb input MUST be ignored by the slider when its default has already been prevented (the consumer's `onKeyDown` runs first, SLD-API-22) or when the key is not one of the eight handled keys. Server: relaxed, see §9.
  Source: `slider/thumb/SliderThumb.tsx:44,368-375`. Test: [L4] `SliderThumb.test.tsx` "allows preventing the internal key handling", "forwards key events that the slider does not handle".
- **SLD-KEY-02** — MUST. Direction: <kbd>ArrowUp</kbd> → `+1`, <kbd>ArrowDown</kbd> → `−1`, <kbd>ArrowRight</kbd> → `+1` in LTR / `−1` in RTL, <kbd>ArrowLeft</kbd> → `−1` in LTR / `+1` in RTL, where direction comes from the nearest `DirectionProvider` (UTL-I18N-01, default LTR). `orientation` MUST NOT alter this mapping (a vertical slider still accepts Left/Right).
  Source: `slider/thumb/SliderThumb.tsx:147,152,385-397`. Test: [L4] `SliderRoot.test.tsx` "keyboard interactions" → "$direction, orientation: $orientation" → "decrements the value" / "increments the value" (all four cases × keys); `SliderIndicator.test.tsx` "preserves keyboard and indicator parity…".
- **SLD-KEY-03** — MUST. With <kbd>Shift</kbd> held, an arrow key MUST use `largeStep` as the increment instead of `step`.
  Source: `slider/thumb/SliderThumb.tsx:383`. Test: [L4] `SliderRoot.test.tsx` "decrements the value by largeStep when Shift is pressed", "increments the value by largeStep when Shift is pressed", "clears external errors on change" (`Shift+ArrowRight`).
- **SLD-KEY-04** — MUST. <kbd>PageUp</kbd> MUST use `largeStep` with direction `+1` and <kbd>PageDown</kbd> `largeStep` with direction `−1`, regardless of <kbd>Shift</kbd>, orientation or direction.
  Source: `slider/thumb/SliderThumb.tsx:398-405`. Test: [L4] `SliderRoot.test.tsx` "key: PageUp" → "increments the value by largeStep", "key: PageDown" → "decrements the value by largeStep".
- **SLD-KEY-05** — MUST. <kbd>End</kbd> MUST request `max`, except in range mode when a next neighbour exists, where it requests `values[index+1] − step × minStepsBetweenValues`; <kbd>Home</kbd> MUST request `min`, except in range mode when a previous neighbour exists, where it requests `values[index−1] + step × minStepsBetweenValues`. These targets are not step-rounded.
  Source: `slider/thumb/SliderThumb.tsx:406-417`. Test: [L4] `SliderRoot.test.tsx` "key: End" → "sets value to max in a single value slider", "sets value to the maximum possible value in a range slider"; "key: Home" → "sets value to min in a single value slider", "sets value to the minimum possible value in a range slider".
- **SLD-KEY-06** — MUST. For arrow and Page keys the candidate MUST be `getNewValue(roundValueToStep(values[index], step, min), increment, direction, min, max)` (SLD-STA-14, SLD-STA-15): the current value is first snapped to the step grid, the increment is added at the combined decimal precision, and the result is clamped to `[min, max]` (so a key press at a bound leaves the value unchanged and reports nothing).
  Source: `slider/thumb/SliderThumb.tsx:384,422-424`. Test: [L4] `SliderRoot.test.tsx` "should not go more than the max", "should not go less than the min", "stops at min when decrementing while Shift is pressed", "stops at max when incrementing while Shift is pressed", "rounds fractional values to the configured step", "preserves largeStep increments when step uses a different grid", "does not exceed max", "does not go below min", "keypresses should correct invalid values".
- **SLD-KEY-07** — MUST. The keyboard candidate MUST then pass through SLD-STA-16 (neighbour clamp and sort in range mode) and SLD-STA-17 (minimum distance); if the distance check fails the key press MUST produce no change and no callback (but still marks the field touched only when the check passed — see SLD-FRM-05).
  Source: `slider/root/SliderRoot.tsx:228-247`, `slider/thumb/SliderThumb.tsx:440`. Test: [L4] `SliderRoot.test.tsx` "should enforce a minimum difference between range slider values", "does not commit when keyboard interaction leaves a range value unchanged".
- **SLD-KEY-08** — MUST. For every handled key (whether or not the value changes) the slider MUST call `preventDefault()` on the keydown (suppressing the native range input's own stepping and page scroll), and for the arrow keys, <kbd>Home</kbd> and <kbd>End</kbd> — but NOT <kbd>PageUp</kbd>/<kbd>PageDown</kbd> — MUST also call `stopPropagation()` so enclosing composite widgets and popups do not react.
  Source: `slider/thumb/SliderThumb.tsx:377-379,441`, `internals/composite/composite.ts:22`. Test: none (propagation not asserted). Docs: `CHANGELOG.md` v1.0.0-beta.0 "Stop event propagation to allow composite components to be used within popups".
- **SLD-KEY-09** — MUST. Keys outside the handled set MUST NOT be intercepted (no `preventDefault`, no `stopPropagation`); <kbd>Tab</kbd> performs native focus movement.
  Source: `slider/thumb/SliderThumb.tsx:373-375`. Test: [L4] `SliderThumb.test.tsx` "forwards key events that the slider does not handle"; "single thumb" / "multiple thumbs" (focus and blur: Tab leaves the slider).
- **SLD-KEY-10** — MUST. <kbd>Tab</kbd> MUST move focus through the thumb inputs in document order, then out of the slider; <kbd>Shift</kbd>+<kbd>Tab</kbd> the reverse (native behavior; the thumb elements and Control are not tab stops, SLD-FOC-01).
  Source: `slider/thumb/SliderThumb.tsx:452-453`. Test: [L4] `SliderThumb.test.tsx` "multiple thumbs" (focus and blur); `SliderRoot.test.tsx` "sets value to the minimum possible value in a range slider" (`Shift+Tab`).
- **SLD-KEY-11** — MUST. When a handled key produces a candidate and the input does not currently match `:focus-visible` (e.g. it was focused by a pointer press), the slider MUST blur and immediately re-focus the input with `{ preventScroll: true, focusVisible: true }` before applying the change, so that `:focus-visible` styling appears; this synthetic blur/focus pair MUST NOT invoke the consumer's `onBlur`/`onFocus`, MUST NOT change `activeThumbIndex`, the field's touched/focused flags, or trigger a blur-mode validation commit, and MUST NOT propagate to ancestors.
  Source: `slider/thumb/SliderThumb.tsx:159-176,335-350,426-438`, `floating-ui-react/utils/element.ts:71-82`. Test: [L4] `SliderRoot.test.tsx` "shows :focus-visible after keyboard interaction following a pointer press"; `SliderThumb.test.tsx` "does not emit extra blur and focus events when restoring focus-visible". Docs: `CHANGELOG.md` v1.0.0-beta.2 "Fix thumb `:focus-visible` with mixed keyboard and pointer modality".
- **SLD-KEY-12** — MUST. A keyboard change MUST be reported with `reason: 'keyboard'`, `activeThumbIndex: index`, and `event` = the native `KeyboardEvent` (cloned per SLD-EVT-04); when applied, `onValueCommitted` MUST fire synchronously right after, with the same value and reason (SLD-EVT-05).
  Source: `slider/root/SliderRoot.tsx:233-244`, `slider/thumb/SliderThumb.tsx:440`. Test: [L4] `SliderRoot.test.tsx` "provides the change reason for keyboard interactions", "does not commit a canceled change", "validates once when changed by the user".
- **SLD-KEY-13** — MUST. Keyboard movement MUST ignore `thumbCollisionBehavior`: a thumb can never move past (or closer than the minimum distance to) a neighbour by keyboard; the candidate is clamped to the neighbours by SLD-STA-16.
  Source: `slider/root/SliderRoot.tsx:230`, `slider/utils/getSliderValue.ts:18-21`. Test: [L4] `SliderRoot.test.tsx` "sets value to the maximum possible value in a range slider" (`[20,50]` + End on thumb 0 → `[50,50]`). Docs: `CHANGELOG.md` v1.0.0-beta.5 "Keyboard interactions always use `none` behavior".
- **SLD-KEY-14** — MUST. A disabled thumb input (SLD-API-16) receives no keyboard events because it is not focusable (native `disabled`); the slider adds no further keyboard gating. There is no read-only mode.
  Source: `slider/thumb/SliderThumb.tsx:326`. Test: none.

### 6.2 Pointer and touch

Pointer handling lives on `Slider.Control`. A gesture starts with `pointerdown` on the Control (any pointer type), or with a native `touchstart` on the Control when no `pointerdown` started it (synthetic/compatibility paths), tracks movement through document-level listeners, and ends on release/cancel. The *pressed thumb* is the thumb whose `pointerdown` fired first (thumbs are descendants, so a thumb press reaches the thumb before the Control), otherwise the thumb nearest to the press point. The thumb's position is always derived from the value (SLD-DOM-09), never from the raw pointer.

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| primary-button `pointerdown` on the track (not on a thumb) | enabled | nearest enabled thumb becomes pressed; value at the press point applied with reason `track-press`; dragging starts; thumb input focused next frame | SLD-PTR-01, 03, 04, 05, 07, 08 |
| primary-button `pointerdown` on a thumb | enabled thumb | that thumb becomes pressed (grab offset recorded); **no** value change; dragging starts; input focused next frame unless already focused | SLD-PTR-02, 05, 07, 08 |
| `pointerdown` on a disabled thumb / non-primary button / `defaultPrevented` / non-element target / disabled slider | — | ignored | SLD-PTR-01, 16, 17 |
| `pointermove` (document) while pressed | `buttons !== 0` | value at pointer applied with reason `drag` (collision rules apply) | SLD-PTR-09, 10 |
| `pointermove` with `buttons === 0` | pressed | treated as release (dropped `pointerup` hedge) | SLD-PTR-09 |
| `pointerup` / `pointercancel` (document) | pressed, no touch tracked | interaction ends; `onValueCommitted` if anything was applied | SLD-PTR-11 |
| `pointercancel` while a touch is tracked | touch gesture | ignored; touch listeners keep tracking | SLD-PTR-13 |
| `touchstart` on the Control (native, passive) after `pointerdown` | touch/pen | adds touch tracking to the running gesture | SLD-PTR-12 |
| `touchstart` on the Control without a preceding `pointerdown` | — | starts a gesture: nearest thumb, immediate focus, `track-press` value | SLD-PTR-12 |
| `touchmove` / `touchend` / `touchcancel` (document) | same touch identifier only | as move / end | SLD-PTR-12, 13 |
| tap on a thumb (touch/pen, no movement) | — | no value change, no commit | SLD-PTR-15 |
| `mousedown` alone (no pointer event) | — | nothing | SLD-PTR-21 |

- **SLD-PTR-01** — MUST. A `pointerdown` on the Control MUST be handled only when: the Control element exists, the slider is not disabled, the event is not default-prevented (the consumer's `onPointerDown` runs first), the target is an element, and `event.button === 0`. If the target lies inside a disabled thumb (its input has `disabled`), the press MUST be ignored and any pressed-thumb state cleared. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:159-171,491-511`. Test: [L4] `SliderRoot.test.tsx` "should not react to right clicks", "does not drag a thumb disabled via the `disabled` prop", "does not change a single disabled thumb when pressing the track", "handles a touch press normally after a cancelled %s gesture" (prevented pointerdown ignored by the pointer path).
- **SLD-PTR-02** — MUST. A `pointerdown` on an enabled thumb element MUST record that thumb's index as the pressed index and the *grab offset* = pointer coordinate along the slider axis (`clientY` vertical, `clientX` horizontal) minus the midpoint of the thumb element's bounding rectangle on that axis; a disabled thumb records nothing. The offset is subtracted from every later pointer coordinate of the gesture (SLD-PTR-04) so the thumb does not jump under the pointer.
  Source: `slider/thumb/SliderThumb.tsx:479-489`, `slider/utils/getMidpoint.ts:1-4`, `slider/control/SliderControl.tsx:190-192`. Test: [L4] `SliderThumb.test.tsx` "preserves the grab offset when dragging a vertical thumb"; `SliderRoot.test.tsx` "drags the intended thumb when 3 thumbs are present".
- **SLD-PTR-03** — MUST. When the gesture starts, the pressed thumb MUST be resolved as: (a) if a thumb index was recorded by SLD-PTR-02 and is within the value count: that index, except that when its value equals `max` the pressed index becomes the lowest index of the run of thumbs stacked at `max` ending at it (so stacked thumbs at the top can be pulled apart); (b) otherwise (track press) the enabled thumb (input not disabled) whose element midpoint on the slider axis is closest to the press coordinate, later indexes winning ties; `-1` when there is no enabled thumb, in which case nothing happens. When the resolved index differs from the recorded one, the recorded grab offset is discarded if no element exists for the new index.
  Source: `slider/control/SliderControl.tsx:147-152,226-267`. Test: [L4] `SliderControl.test.tsx` "moves the first of several thumbs stacked at the maximum"; `SliderRoot.test.tsx` "does not select a disabled thumb when pressing the track", "drags the intended thumb when 3 thumbs are present"; `SliderControl.test.tsx` "ignores touch interactions when no thumbs are composed".
- **SLD-PTR-04** — MUST. **Position → value.** For a pointer at client coordinates `(x, y)` the candidate for the pressed thumb MUST be computed as:
  ```
  rect = Control bounding client rect
  offset = { start: border + padding at inline-start (horizontal) / top (vertical),
             end:   border + padding at inline-end   (horizontal) / bottom (vertical) }   // from the Control's computed style
  insetOffset = inset ? (pressed thumb's rect width (horizontal) or height (vertical)) / 2 : 0   // measured at gesture start
  controlSize = (vertical ? rect.height : rect.width) − offset.start − offset.end − 2 × insetOffset
  px = x − grabOffset; py = y − grabOffset                      // SLD-PTR-02 (0 for a track press)
  valueSize = vertical ? rect.bottom − py − offset.end
                       : (rtl ? rect.right − px : px − rect.left) − offset.start
  ratio = clamp((valueSize − insetOffset) / controlSize, 0, 1)
  v = (max − min) × ratio + min
  v = clamp(roundValueToStep(v, step, min), min, max)           // SLD-STA-14
  single mode: candidate = range-mode-array ? [v] : v
  range mode:  candidate = resolveThumbCollision(behavior, values, latestApplied, valuesAtPressStart, pressedIndex, v, …)   // SLD-STA-19
  ```
  Computation MUST return "no candidate" when the Control element is missing or the pressed index is outside the current value count. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:31-51,173-224,269-276`, `slider/utils/getMidpoint.ts:1-4`. Test: [L4] `SliderRoot.test.tsx` "should handle RTL", "should report the right position", "should round value to step precision", "should reach right edge value", "should not break when initial value is out of range"; `SliderControl.test.tsx` "accounts for the thumb size when pressing an inset $name control"; `SliderThumb.test.tsx` "preserves the grab offset when dragging a vertical thumb". Docs: `CHANGELOG.md` v1.0.0-alpha.7 "Correct thumb positioning when control has padding".
- **SLD-PTR-05** — MUST. A press that did not land on a thumb (no grab offset recorded) MUST immediately apply the candidate of SLD-PTR-04 with `reason: 'track-press'`; a press on a thumb MUST NOT change the value by itself (even if the press point is off the thumb's centre), so a plain click/tap on a thumb reports and commits nothing. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:537-540`. Test: [L4] `SliderRoot.test.tsx` "is called when clicking on the control", "is not called when clicking on the thumb", "does not commit when thumb press leaves the value unchanged", "provides the change reason for track presses"; `SliderControl.test.tsx` "preserves single-element array values on track press".
- **SLD-PTR-06** — MUST. After a handled `pointerdown` with a non-zero `pointerId`, the Control MUST capture the pointer (`setPointerCapture`); when the gesture ends with a pointer event and the Control still holds the capture, it MUST release it. (A `pointerId` of `0` is never captured — §16.) Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:377-382,542-544`. Test: [L4] `SliderControl.test.tsx` "releases pointer capture when the interaction ends". Docs: `CHANGELOG.md` v1.0.0-beta.1 "Use pointer capture when dragging".
- **SLD-PTR-07** — MUST. On a handled `pointerdown`: if the pressed thumb's input (or any descendant of that thumb) is already the active element, the slider MUST call `preventDefault()` on the pointerdown (so the browser does not move focus away); otherwise it MUST focus the pressed thumb's input on the next frame with `{ preventScroll: true, focusVisible: false }`. A pending focus frame MUST be cancelled if the Control unmounts. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:279-291,459,473,522-533`. Test: [L4] `SliderRoot.test.tsx` "should focus the slider when dragging", "shows :focus-visible after keyboard interaction following a pointer press" (not focus-visible after pointer focus).
- **SLD-PTR-08** — MUST. A handled `pointerdown` MUST set `dragging` to `true` immediately (before applying any value), and MUST add document listeners for `pointermove` (passive), `pointerup` (once) and `pointercancel` (once); the move counter is reset to `0`. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:535,546-552`. Test: [L4] `SliderControl.test.tsx` "commits and ends a %s drag on pointercancel" (`data-dragging` after pointerdown + move).
- **SLD-PTR-09** — MUST. On each tracked move (`pointermove`, or `touchmove` of the tracked finger) the slider MUST: increment the move counter; if the event is a `pointermove` with `buttons === 0`, end the gesture as a release (SLD-PTR-11) and stop; otherwise compute the candidate (SLD-PTR-04), discard it unless SLD-STA-17 holds, and apply it with `reason: 'drag'`. Equal candidates are dropped by SLD-STA-09, so repeated moves at one position report once. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:320-349`. Test: [L4] `SliderRoot.test.tsx` "should hedge against a dropped mouseup event", "should fire only when the value changes", "should support touch events"; `SliderControl.test.tsx` "ignores a drag when collision behavior cannot satisfy the minimum distance".
- **SLD-PTR-10** — MUST. In range mode the drag candidate is resolved by SLD-STA-19 with `currentValues` = the last applied value of this gesture (initially the values at press) and `initialValues` = the values at press. When the result has `didSwap` and the change is applied (not canceled), the pressed index MUST switch to the returned `thumbIndex` (clearing the grab offset when no thumb element exists for it) and that thumb's input MUST be focused immediately; `activeThumbIndex` in the details is the returned `thumbIndex`. A canceled swap MUST leave the pressed index unchanged so later moves continue to address the originally pressed thumb. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:212-223,293-318`. Test: [L4] `SliderRoot.test.tsx` "keeps focus and the active index on the logical thumb after a swap from a %s", "keeps the original pressed thumb after a canceled swap and commits the next accepted move"; `SliderControl.test.tsx` "clears the grabbed offset when swapping to a thumb that is not rendered"; `SliderThumb.test.tsx` "prevents thumbs from passing each other when set to "none"", "pushes adjacent thumbs forward when set to "push"", "allows thumbs to swap when set to "swap"", "maintains minimum steps between values when swapping".
- **SLD-PTR-11** — MUST. **End of gesture** (`pointerup`; `pointercancel` when no touch is tracked; `touchend`/`touchcancel` of the tracked finger): the slider MUST set `activeThumbIndex` to `-1` and `dragging` to `false`, clear the grab offset, discard the cached interaction value if it is an array whose length differs from the current value count, then — if an interaction value remains — call `onValueCommitted(value, { reason: <last applied reason>, event })` exactly once with the value of the last applied `onValueChange`; then release pointer capture (SLD-PTR-06), reset the pressed index, and remove every document listener it added (pointer and touch), so later events anywhere on the page are ignored. A release with no applied change commits nothing. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:351-387,445-457`. Test: [L4] `SliderRoot.test.tsx` "single value", "array value", "onValueCommitted is called with the same value as the latest onValueChange when pointerUp occurs at a different location than onValueChange", "does not commit when thumb press leaves the value unchanged", "does not call onValueCommitted on outside taps after a touch interaction", "removes the document touchend listener after a touch interaction"; `SliderControl.test.tsx` "commits and ends a %s drag on pointercancel", "releases pointer capture when the interaction ends". Docs: `CHANGELOG.md` v1.9.0 "End drags on `touchcancel` and `pointercancel`", v1.7.0 "Fix touchend listener accumulation leak".
- **SLD-PTR-12** — MUST. **Touch path.** The Control MUST listen natively (passive) for `touchstart`. On `touchstart`, unless the slider is disabled (then nothing happens) or the target is inside a disabled thumb (then the pressed state is cleared and nothing happens): the first changed touch's `identifier` becomes the tracked finger. If the gesture was already started by a `pointerdown` of a touch-like pointer (`touch` or `pen`) and no `touchstart` has consumed that flag yet, only the document listeners `touchmove`/`touchend`/`touchcancel` (passive) are added. Otherwise the gesture starts here: pressed thumb per SLD-PTR-03 (using the touch's coordinates), no candidate → stop; else the pressed thumb's input is focused **immediately** (not next frame) and the candidate is applied with `reason: 'track-press'`, the move counter reset, and the touch listeners added. Touch events whose `changedTouches` do not include the tracked identifier MUST be ignored (another finger); events on a text node target MUST work.
  Source: `slider/control/SliderControl.tsx:53-78,399-443,461-477,548`, `internals/usePressAndHold.ts:19-21`. Test: [L4] `SliderRoot.test.tsx` "commits a %s track tap", "commits a real touch track tap", "should support touch events", "should only listen to changes from the same touchpoint", "should focus the slider when touching", "does not drag a disabled thumb with touch events"; `SliderControl.test.tsx` "handles touch interactions that originate on a text node", "ignores touch interactions when no thumbs are composed", "ignores %s from another finger during a touch drag". Docs: `CHANGELOG.md` v1.9.0 "Fix touch track taps not firing `onValueCommitted`".
- **SLD-PTR-13** — MUST. While a touch identifier is tracked, a `pointercancel` MUST be ignored (the browser cancels the pointer when it starts panning; the touch listeners keep tracking the finger) and the gesture ends only on `touchend`/`touchcancel` of that finger (or `pointerup`); a `pointercancel` with no tracked touch ends the gesture per SLD-PTR-11. A `touchend`/`touchcancel` for a different finger MUST NOT end it.
  Source: `slider/control/SliderControl.tsx:389-397`. Test: [L4] `SliderRoot.test.tsx` "keeps tracking a touch drag with touch listeners after the browser cancels the pointer", "handles a touch press normally after a cancelled %s gesture"; `SliderControl.test.tsx` "keeps a touch drag going after pointercancel and commits and ends it on touchcancel", "ignores %s from another finger during a touch drag".
- **SLD-PTR-14** — MUST. In a gesture started by `touchstart` (no `pointerdown`), `dragging` MUST become `true` only once more than two move events have been tracked (i.e. on the third `touchmove`), so a tap with incidental jitter is not styled as a drag; a `pointerdown`-started gesture is dragging from the start (SLD-PTR-08).
  Source: `slider/control/SliderControl.tsx:29,343-345`. Test: [L4] `SliderRoot.test.tsx` "should not apply data-dragging for click modality", "should apply data-dragging for dragging modality".
- **SLD-PTR-15** — MUST. A touch or pen tap on a thumb (pointerdown on the thumb, compatibility touchstart, pointerup, touchend) MUST change nothing and commit nothing.
  Source: `slider/control/SliderControl.tsx:402-403,421-437,537-540`. Test: [L4] `SliderRoot.test.tsx` "does not change the value on a %s tap on the thumb".
- **SLD-PTR-16** — MUST. A `pointerdown` with `button !== 0` (right/middle), or whose default was prevented before the Control's handler ran, MUST be ignored entirely (no focus, no value, no listeners). The stale "started by pointer" flag of a previous cancelled gesture MUST be cleared at every `pointerdown` and consumed by the next `touchstart`, so a prevented pointerdown followed by a touchstart is handled by the touch path as a fresh gesture. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:402-403,493-506`. Test: [L4] `SliderRoot.test.tsx` "should not react to right clicks", "handles a touch press normally after a cancelled %s gesture".
- **SLD-PTR-17** — MUST. While the slider is disabled, `pointerdown` and `touchstart` MUST be ignored; when the slider becomes disabled during a gesture, every document listener MUST be removed and the gesture dropped without `onValueCommitted` (the interaction value is discarded).
  Source: `slider/control/SliderControl.tsx:405-407,479-483,498-499`. Test: [L4] `SliderRoot.test.tsx` "should not respond to drag events if disabled", "should not respond to drag events after becoming disabled"; `SliderControl.test.tsx` "ends a cancelled %s drag after disabling and re-enabling during a touch drag".
- **SLD-PTR-18** — MUST. Pressing a thumb whose value equals `max` while lower-indexed thumbs share that value MUST drag the lowest of the stacked thumbs (SLD-PTR-03a), reported with that lower `activeThumbIndex`.
  Source: `slider/control/SliderControl.tsx:234-243`. Test: [L4] `SliderControl.test.tsx` "moves the first of several thumbs stacked at the maximum". Docs: `CHANGELOG.md` v1.0.0-alpha.7 "Fix overlapping slider thumbs stuck at min or max".
- **SLD-PTR-19** — MUST. If the value count changes during a gesture (consumer-owned array grows or shrinks): when the pressed index is no longer within the count, further moves MUST produce no candidate and the cached interaction value MUST be dropped; a cached array whose length no longer matches MUST NOT be committed on release; the collision algorithm MUST use the live values (SLD-STA-19 `currentValues`) so a grown array is handled consistently.
  Source: `slider/control/SliderControl.tsx:177-182,361-367`. Test: [L4] `SliderControl.test.tsx` "does not resurrect a removed thumb value when the range shrinks mid-drag", "does not commit a stale value when the range shrinks mid-drag and the pressed thumb stays valid", "clears cached interaction state when the controlled range grows mid-drag"; `resolveThumbCollision.test.ts` "uses current values when a controlled range grows during a swap interaction", "returns a scalar when the live values shrink to one item during an interaction".
- **SLD-PTR-20** — MUST. The slider MUST NOT mutate the native pointer/touch/mouse events it receives (their `target` stays the element the user pressed); the only `target` override is on the cloned event passed in the change details (SLD-EVT-04).
  Source: `slider/root/SliderRoot.tsx:199-212`. Test: [L4] `SliderRoot.test.tsx` "should not override the event.target on touch events", "should not override the event.target on mouse events".
- **SLD-PTR-21** — MUST. `mousedown`/`mouseup`/`click` alone (without pointer or touch events) MUST do nothing.
  Source: `slider/control/SliderControl.tsx:488-556` (only `pointerdown` and `touchstart` are handled). Test: [L4] `SliderRoot.test.tsx` "should not override the event.target on mouse events" (`mouseDown` → no change).
- **SLD-PTR-22** — SHOULD. The component does not set `touch-action` or `user-select` on any element; consumers SHOULD style the Control with `touch-action: none` (and `user-select: none`) so the browser does not cancel the pointer to pan. The touch path (SLD-PTR-12/13) exists so dragging still works when they do not.
  Source: `slider/control/SliderControl.tsx:460-462` (comment), `docs/src/app/(docs)/react/components/slider/demos/hero/css-modules/index.module.css:1-10`. Test: none.
- **SLD-PTR-23** — MUST. The press-time snapshot (values at press start, "last applied" values, move counter, tracked touch id, grab offset, pressed index) MUST be reset at the start of each gesture and cleared when it ends (SLD-PTR-11), so no state leaks from one gesture into the next.
  Source: `slider/control/SliderControl.tsx:226-230,384,445-457,546`. Test: [L4] `SliderControl.test.tsx` "commits and ends a %s drag on pointercancel" (a later press elsewhere changes nothing), "ends a cancelled %s drag after disabling and re-enabling during a touch drag".
- **SLD-PTR-24** — MUST. In the inset modes the half-size of the pressed thumb (width horizontal / height vertical, measured from its bounding rectangle when the gesture starts) MUST be used as `insetOffset` in SLD-PTR-04, so pressing at the Control's edge yields `min`/`max` exactly.
  Source: `slider/control/SliderControl.tsx:134-136,186-198,269-276`. Test: [L4] `SliderControl.test.tsx` "accounts for the thumb size when pressing an inset $name control".

### 6.3 Focus management

- **SLD-FOC-01** — MUST. The thumb inputs MUST be the only focusable elements of the slider: the root, Control, Track, Indicator, Value, Label and thumb elements carry no `tabindex`, and each input is natively tabbable (`tabIndex` property `0`) unless the Thumb's `tabIndex` prop says otherwise.
  Source: `slider/thumb/SliderThumb.tsx:452-453`, `slider/control/SliderControl.tsx:488-556`. Test: [L2] `SliderThumb.test.tsx` "single thumb" (focus and blur), "does not apply tabIndex to the thumb element by default"; `SliderControl.test.tsx` "does not apply a tabIndex by default" (L4 for the focus clause).
- **SLD-FOC-02** — MUST. Tab order MUST be the document order of the thumb inputs (no roving tabindex: every enabled thumb is a tab stop); `tabIndex={-1}` on a Thumb MUST remove that input from the sequence.
  Source: `slider/thumb/SliderThumb.tsx:452`. Test: [L4] `SliderThumb.test.tsx` "multiple thumbs" (focus and blur), "can be removed from the tab sequence".
- **SLD-FOC-03** — MUST. When a thumb input gains focus (by any means) `activeThumbIndex` MUST become its index and the last-used index MUST be updated (SLD-STA-06/07); when it loses focus `activeThumbIndex` MUST become `-1`. The synthetic pair of SLD-KEY-11 is excluded.
  Source: `slider/thumb/SliderThumb.tsx:335-351`. Test: [L4] `SliderThumb.test.tsx` "keeps the most recently active thumb on top after focus moves away".
- **SLD-FOC-04** — MUST. Field `focused` follows focus ownership (FFI-STA-09, FFI-ALG-13) with each thumb input as a focus target: focusing an enabled thumb input acquires the flag; blurring to a target *inside any thumb of the same slider* MUST neither release the flag nor mark the field touched nor commit blur-mode validation (the sibling thumb acquires it on its own focus); blurring anywhere else releases it; a thumb that becomes disabled or unmounts while it is the owner releases it, while a thumb that no longer owns it (focus moved to a sibling) does not disturb it.
  Source: `slider/thumb/SliderThumb.tsx:158,339,351-360`, `internals/field-root-context/useSetFieldFocused.ts:15-46`. Test: [L4] `SliderRoot.test.tsx` "[data-focused]", "is removed when the focused thumb becomes disabled", "is removed when the focused thumb unmounts", "is kept when focus moved to a sibling before the first thumb is disabled", "is kept when a sibling thumb is disabled or unmounted"; `SliderThumb.test.tsx` "does not commit field validation when moving focus between range thumbs", "commits field validation when focus moves from a thumb to an arbitrary Control child".
- **SLD-FOC-05** — MUST. When the slider becomes disabled while the active element is inside the root, that element MUST be blurred explicitly (post-commit, pre-paint) and `activeThumbIndex` reset to `-1` (some browsers keep focus on a newly disabled control).
  Source: `slider/root/SliderRoot.tsx:256-272`. Test: [L4] `SliderRoot.test.tsx` "explicitly blurs the focused thumb when disabled", "should not respond to drag events after becoming disabled".
- **SLD-FOC-06** — MUST. Clicking `Slider.Label` (FFI-PTR-01, FFI-PTR-05) MUST focus, with `{ focusVisible: true }`: the element whose id is the enclosing labelable scope's control id when such an id resolves to an element (looked up through enclosing shadow roots, then the document); otherwise the single `input[type="range"]` inside `Slider.Control` when exactly one exists; otherwise nothing (a range slider outside a Field is not focused by its label). Clicks whose target is a `button`, `input`, `select` or `textarea` are ignored; `pointerdown` on the label is default-prevented.
  Source: `slider/label/SliderLabel.tsx:32-52`, `internals/labelable-provider/useLabel.ts:30-77,103-121`. Test: [L4] `SliderRoot.test.tsx` "Slider.Label focuses slider on click", "Slider.Label does not focus a thumb on click for range sliders"; `SliderLabel.test.tsx` "focuses the registered thumb when composed within a Field", "focuses the thumb in its shadow root when the outer document has a matching ID", "focuses a thumb in the light DOM from a label in a shadow root", "does nothing when a Field slider has no thumb to focus", "does not throw when the label is clicked in a detached React root".
- **SLD-FOC-07** — MUST. The control handle the slider registers with the field is the shared validation input (the last-mounted thumb's `<input>`), so a blocked form submission focuses a range input rather than the root (FFI-FOC-06).
  Source: `slider/root/SliderRoot.tsx:176`, `slider/thumb/SliderThumb.tsx:460`. Test: [L4] `SliderRoot.test.tsx` "moves focus to the range input on invalid submit".
- **SLD-FOC-08** — MUST. Pointer-initiated focus (SLD-PTR-07, SLD-PTR-12) requests `focusVisible: false`; keyboard interaction afterwards restores `:focus-visible` (SLD-KEY-11); label-initiated focus requests `focusVisible: true` (SLD-FOC-06).
  Source: `slider/control/SliderControl.tsx:285-290`, `slider/thumb/SliderThumb.tsx:432-437`, `internals/labelable-provider/useLabel.ts:115-121`. Test: [L4] `SliderRoot.test.tsx` "shows :focus-visible after keyboard interaction following a pointer press".

### 6.4 Dismissal

Not applicable — the slider has no popup; nothing is dismissed by outside press, Escape, scroll or blur.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: number \| number[], details: ChangeEventDetails)` | before every candidate is applied (not for equal/NaN candidates, not for prop changes) | `input-change`, `keyboard`, `track-press`, `drag` (`none` declared only) | yes (`details.cancel()`) |
| `onValueCommitted` | `(value, details: CommitEventDetails)` | synchronously after an applied keyboard/input change; at the end of a pointer/touch gesture with the last applied value | same literals; pointer end reports the last applied reason | no |

| Reason | User action | Event type | Commit timing |
|---|---|---|---|
| `input-change` | the native range input's value changed by something other than the slider's own keyboard handler (assistive technology increment/decrement, form libraries, scripts dispatching `change`) | `Event` / `InputEvent` | immediately after the change |
| `keyboard` | a handled key on a thumb input (§6.1) | `KeyboardEvent` | immediately after the change |
| `track-press` | `pointerdown`/`touchstart` on the Control not on a thumb (§6.2) | `PointerEvent` / `TouchEvent` | on release, if no later drag change was applied |
| `drag` | a tracked move while pressed | `PointerEvent` / `TouchEvent` | on release |
| `none` | — (declared in the type; never emitted by the slider) | — | — |

- **SLD-EVT-01** — MUST. Every candidate that passes SLD-STA-09 MUST be reported through `onValueChange(candidate, details)` **before** any state is updated; `details` MUST expose `reason`, `event` (SLD-EVT-04), `activeThumbIndex`, `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed` and `trigger` (`undefined`) per EVD-API-01.
  Source: `slider/root/SliderRoot.tsx:193-226,236-238`, `slider/control/SliderControl.tsx:298-303`. Test: [L2] `SliderRoot.test.tsx` "should call handlers", "provides the change reason for input events".
- **SLD-EVT-02** — MUST. `reason` MUST be `'input-change'` for a native change of the range input, `'keyboard'` for §6.1, `'track-press'` for the initial press of §6.2, and `'drag'` for tracked moves; no slider code path emits `'none'`.
  Source: `slider/root/SliderRoot.tsx:233`, `slider/control/SliderControl.tsx:347,434,539`. Test: [L2] `SliderRoot.test.tsx` "provides the change reason for input events", "provides the change reason for keyboard interactions", "provides the change reason for track presses", "should update aria-valuenow"; `SliderThumb.test.tsx` "handles change events" (L4 for the keyboard, track-press and drag clauses).
- **SLD-EVT-03** — MUST. Calling `details.cancel()` inside `onValueChange` MUST prevent the commit (EVD-EVT-05): the raw value, every attribute and `onValueCommitted` stay untouched for that candidate; a drag continues from the previously applied value, a canceled swap keeps the originally pressed thumb (SLD-PTR-10), and the gesture's commit (if any) uses the last non-canceled value (SLD-EVT-12).
  Source: `slider/root/SliderRoot.tsx:216-218`, `slider/control/SliderControl.tsx:305-315`. Test: [L4] `SliderRoot.test.tsx` "does not commit a canceled change", "does not commit a canceled track press", "does not commit a canceled drag", "keeps the original pressed thumb after a canceled swap and commits the next accepted move".
- **SLD-EVT-04** — MUST. `details.event` MUST be a *clone* of the originating native event — constructed with the original's constructor, `type` and init dictionary (not `window.event`) — whose `target` property is redefined (writable) to the object `{ value: candidate, name: effectiveName }`; the original event MUST NOT be modified. (This lets form libraries read `event.target.value`/`event.target.name`.)
  Source: `slider/root/SliderRoot.tsx:199-212`. Test: [L2] `SliderRoot.test.tsx` "should pass "name" and "value" as part of the event.target for onValueChange", "should not rely on the global event when cloning change events", "should not override the event.target on mouse events". Docs: `CHANGELOG.md` v1.1.0 "Fix change event cloning".
- **SLD-EVT-05** — MUST. `onValueCommitted` MUST fire: (a) synchronously after an applied (`onValueChange` called and not canceled) `keyboard` or `input-change` change, with that value and reason; (b) once per pointer/touch gesture, at its end (SLD-PTR-11), with the value of the gesture's last applied change and the reason of that change (`track-press` or `drag`) — never with a value the consumer did not already receive from `onValueChange`; its `details` is a generic-event object with `event` = the native keyboard/change event (a) or the release event (b). It MUST NOT fire when nothing was applied (EVD-EVT-23).
  Source: `slider/root/SliderRoot.tsx:242-244`, `slider/control/SliderControl.tsx:369-375`. Test: [L2] `SliderRoot.test.tsx` "single value", "array value", "should call handlers", "onValueCommitted is called with the same value as the latest onValueChange when pointerUp occurs at a different location than onValueChange", "commits a %s track tap", "does not commit when thumb press leaves the value unchanged" (L4 for the keyboard part of (a) and for (b)). Docs: `CHANGELOG.md` v1.0.0-alpha.4 "Ensure `onValueCommitted` is called with the same value as latest `onValueChange`".
- **SLD-EVT-06** — MUST. Neither callback fires when the candidate equals the raw current value (SLD-STA-09): pressing an arrow at a bound, moving the pointer to the same grid position, or a native change to the same value is silent.
  Source: `slider/root/SliderRoot.tsx:195-197`. Test: [L2] `SliderRoot.test.tsx` "does not commit when keyboard interaction leaves the value unchanged", "should fire only when the value changes"; `SliderThumb.test.tsx` "does not change the value beyond min and max" (second change to the same clamped value is silent) (L4 for the arrow-key and pointer clauses).
- **SLD-EVT-07** — MUST. Neither callback fires for changes of the `value` prop or of `min`/`max` (re-normalization); only user-initiated candidates are reported.
  Source: `slider/root/SliderRoot.tsx:167-172,193-226`. Test: [L2] `SliderThumb.test.tsx` "positions the thumb when the controlled value changes externally" (no callback asserted on external set); `SliderRoot.test.tsx` "revalidates when the controlled value changes externally" (validation runs, no `onValueChange`).
- **SLD-EVT-08** — MUST. The event carried by the details MUST be the native event of the interaction: `KeyboardEvent` for `keyboard`, the input's change event for `input-change`, `PointerEvent`/`TouchEvent` (whichever started or moved the gesture) for `track-press`/`drag`; the commit details at gesture end carry the `pointerup`/`pointercancel`/`touchend`/`touchcancel` event.
  Source: `slider/root/SliderRoot.tsx:236,243`, `slider/control/SliderControl.tsx:296-303,373`. Test: [L2] `SliderRoot.spec.tsx` (type-level narrowing of `details.event` by reason) (L4 for the keyboard and pointer clauses).
- **SLD-EVT-09** — MUST. Consumer DOM handlers on the parts MUST run before the part's own handler for the same event and MAY suppress the part's handling through the event's default-prevention (`pointerdown` on Control, `keydown` on the thumb input) or `preventBaseUIHandler()` (EVD-API-11); consumer `onFocus`/`onBlur` on the Thumb are informational. Server: relaxed, see §9.
  Source: `slider/control/SliderControl.tsx:500,555`, `slider/thumb/SliderThumb.tsx:369,457`. Test: [L4] `SliderThumb.test.tsx` "allows preventing the internal key handling"; `SliderRoot.test.tsx` "handles a touch press normally after a cancelled %s gesture".
- **SLD-EVT-10** — MUST. `onValueChange` MUST fire for a candidate that differs from the raw value even when it equals the normalized display value (an unsorted or out-of-range controlled array), with the candidate being a new, sorted array (never the consumer's array instance).
  Source: `slider/root/SliderRoot.tsx:39-47,195`. Test: [L2] `SliderRoot.test.tsx` "is called even if the $name did not change".
- **SLD-EVT-11** — MUST. `details.activeThumbIndex` MUST be the index of the thumb whose value the candidate changes: the input's index for `input-change`/`keyboard`, the pressed index for `track-press`/`drag`, and the post-swap index when a swap occurred (SLD-PTR-10).
  Source: `slider/root/SliderRoot.tsx:236-238`, `slider/control/SliderControl.tsx:300-302`. Test: [L2] `SliderRoot.test.tsx` "should call handlers" (`0`), "provides the change reason for input events", "drags the intended thumb when 3 thumbs are present" (`2`), "keeps focus and the active index on the logical thumb after a swap from a %s" (`1`); `SliderControl.test.tsx` "moves the first of several thumbs stacked at the maximum" (`0`) (L4 for the `track-press`/`drag` and swap clauses).
- **SLD-EVT-12** — MUST. When a later move of a gesture is canceled, the gesture's commit MUST carry the last applied (non-canceled) value, and the display MUST remain at that value.
  Source: `slider/control/SliderControl.tsx:305-307,369-375`. Test: [L4] `SliderRoot.test.tsx` "commits the last applied value when a later move is canceled", "commits the last applied range value when a later move is canceled".

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md). The slider's row in FFI §6 tables A–C applies; this section states what the slider contributes.

- **SLD-FRM-01** — MUST. Native form participation is through the thumb inputs: with an effective name, a submitted `FormData` MUST contain one entry per thumb under that name, each the string form of the thumb's normalized value, in thumb order (so a range slider submits `getAll(name) === ['25', '50']`); without a name nothing is submitted. `format`/`locale` never affect the submitted strings.
  Source: `slider/thumb/SliderThumb.tsx:327,331,454`. Test: [L2] `SliderRoot.test.tsx` "should include the slider value", "should include range slider value".
- **SLD-FRM-02** — MUST. Within a `Field.Root` the slider MUST register as the field's control (post-commit, pre-paint, re-registering on every change) with: control handle = the shared validation input (SLD-FOC-07), id = the root id, registered value = `fieldValue` (SLD-STA-12), no separate `getValue` projection, fallback name = the `name` prop; registration is disabled while the slider is disabled (FFI-FRM-17/23).
  Source: `slider/root/SliderRoot.tsx:176`, `internals/field-register-control/useRegisterFieldControl.ts:7-46`. Test: [L2] `SliderRoot.test.tsx` "moves focus to the range input on invalid submit", "submits clamped range slider values to onFormSubmit".
- **SLD-FRM-03** — MUST. The form's values object (`Form.onFormSubmit`, `validate`'s second argument) MUST hold `fieldValue` under the field's effective name: a clamped number in single mode, a clamped sorted number array in range mode.
  Source: `slider/root/SliderRoot.tsx:174,176`, FFI-API-07. Test: [L2] `SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit", "submits a clamped single-thumb slider value to onFormSubmit".
- **SLD-FRM-04** — MUST. After every change of `fieldValue` (post-commit, pre-paint, strict inequality — including controlled/programmatic changes and `min`/`max` re-normalization) the slider MUST, in this order: `clearErrors(effectiveName)` (FFI-EVT-03), then `validation.change(fieldValue)` (FFI-FRM-02: validates in `'onChange'` mode and in `'onSubmit'` mode after a submission, once per change), then write `dirty` (SLD-STA-21). Note the ordering: the dirty write comes **after** the validation notification, the reverse of other controls (§16).
  Source: `slider/root/SliderRoot.tsx:178-191`. Test: [L2] `SliderRoot.test.tsx` "validationMode=onChange", "validates once when changed by the user", "validates once with an array value for range sliders when changed by the user", "revalidates when the controlled value changes externally", "does not call validate on change when validationMode is omitted", "validationMode=onSubmit" (post-submit revalidation on change).
- **SLD-FRM-05** — MUST. The field MUST be marked `touched` (a) whenever a keyboard or native-input candidate passes the minimum-distance check — even if it is then dropped as equal or canceled — and (b) when a thumb input blurs to a target outside every thumb of the slider. Pointer interaction alone does not mark touched (the pointer-focused input's later blur does).
  Source: `slider/root/SliderRoot.tsx:240`, `slider/thumb/SliderThumb.tsx:355-359`. Test: [L2] `SliderRoot.test.tsx` "[data-touched]"; `SliderThumb.test.tsx` "commits field validation when focus moves from a thumb to an arbitrary Control child" (`data-touched`) (L4 for the keyboard and blur clauses).
- **SLD-FRM-06** — MUST. In `validationMode="onBlur"` the slider MUST commit validation when a thumb input blurs to a target outside every thumb, with the value `getSliderValue(values[index], index, min, max, isArray, values)` — i.e. the current normalized value in the raw value's shape (a one-element array stays an array); blurring to a sibling thumb MUST NOT commit.
  Source: `slider/thumb/SliderThumb.tsx:354-366`. Test: [L4] `SliderRoot.test.tsx` "validationMode=onBlur", "validationMode=onBlur preserves single-element array values"; `SliderThumb.test.tsx` "does not commit field validation when moving focus between range thumbs", "commits field validation when focus moves from a thumb to an arbitrary Control child".
- **SLD-FRM-07** — MUST. Field validity attributes: `aria-invalid`/`aria-describedby` on every thumb input and on the root (SLD-ARIA-08/09); `data-valid`/`data-invalid` on every part (SLD-DOM-02); a `Field.Error` renders the message. All of these MUST switch in the same update as the validity verdict.
  Source: `slider/root/SliderRoot.tsx:386`, `slider/thumb/SliderThumb.tsx:456`. Test: [L2] `SliderRoot.test.tsx` "validationMode=onSubmit".
- **SLD-FRM-08** — MUST. `Field.Root`'s `disabled` and `name` MUST propagate as SLD-API-05 and SLD-API-10 describe (`disabled` OR-ed, `name` overriding the prop).
  Source: `slider/root/SliderRoot.tsx:113-114`. Test: [L2] `SliderRoot.test.tsx` "should receive disabled prop from Field.Root", "should receive name prop from Field.Root".
- **SLD-FRM-09** — MUST. The slider has no native constraints: there is no `required` or `readOnly` parameter, the range inputs carry no constraint attributes, and native validation never reports an error for them; only the consumer's `validate` function (FFI-FRM-07) decides validity.
  Source: `slider/thumb/SliderThumb.tsx:315-458` (no `required`), `slider/root/SliderRoot.tsx:448-576`. Test: [L2] `SliderRoot.test.tsx` "does not call validate on change when validationMode is omitted".
- **SLD-FRM-10** — MUST. With a `form` prop, every thumb input carries `form="<id>"` so a slider rendered outside the `<form>` element still submits with it (FFI-DOM-08).
  Source: `slider/thumb/SliderThumb.tsx:327`. Test: [L2] `SliderRoot.test.tsx` "submits to an external form when `form` is provided".
- **SLD-FRM-11** — MUST. The slider does not listen for form `reset`: a native reset does not change the slider's state (FFI-FRM-30); the browser may reset the inputs' own values, which the next render overwrites (SLD-DOM-16).
  Source: `slider/thumb/SliderThumb.tsx:454`. Test: none.
- **SLD-FRM-12** — MUST. Label association inside a Field: in single mode `Field.Label`'s `for` targets the thumb input (SLD-DOM-15) and clicking it focuses the input natively; in range mode the inputs use their own ids, `Field.Label` targets nothing, and each thumb should carry its own `aria-label` (the docs recommend `Fieldset.Legend` for the group label). A `Field.Label` id always wins over a `Slider.Label` id for `aria-labelledby` (FFI-ARIA-07).
  Source: `slider/thumb/SliderThumb.tsx:178-180`, `slider/root/SliderRoot.tsx:112`. Test: [L2] `SliderRoot.test.tsx` "Field.Label"; `SliderLabel.test.tsx` "focuses the registered thumb when composed within a Field". Docs: `components/slider/page.mdx` §Labeling a slider, §Form integration.
- **SLD-FRM-13** — MUST. `validate` MUST receive `fieldValue` (number or number array, clamped), both on submit and on change/blur commits.
  Source: `slider/root/SliderRoot.tsx:174,181`, `slider/thumb/SliderThumb.tsx:363-365`. Test: [L2] `SliderRoot.test.tsx` "receives an array value for range sliders", "validates once with an array value for range sliders when changed by the user".
- **SLD-FRM-14** — MUST. An external error from `Form.errors` for the slider's effective name MUST render as invalid (`aria-invalid` on the inputs, `Field.Error` text) and MUST be cleared by the first value change (SLD-FRM-04), removing `aria-invalid` and the error element.
  Source: `slider/root/SliderRoot.tsx:179`. Test: [L2] `SliderRoot.test.tsx` "clears external errors on change".
- **SLD-FRM-15** — MUST. The field's `touched`/`dirty`/`focused`/`valid` flags MUST be reflected on every slider part as data attributes (SLD-DOM-02), including the thumb elements and the Value output.
  Source: `slider/root/stateAttributesMapping.ts:7-15`. Test: [L2] `SliderRoot.test.tsx` "[data-touched]", "[data-dirty]", "[data-focused]", "validationMode=onSubmit" (thumb `data-invalid`/`data-valid`).
- **SLD-FRM-16** — MUST. A canceled change MUST NOT run the value-change pipeline of SLD-FRM-04 (nothing changed), but the touched write of SLD-FRM-05(a) still happens for keyboard/input candidates.
  Source: `slider/root/SliderRoot.tsx:216-218,240`. Test: [L2] `SliderRoot.test.tsx` "does not commit a canceled change" (value unchanged; no revalidation asserted — see EVD-EVT-08 for the general rule).

## 9. Lifecycle, mounting and animation

Timing vocabulary per conventions §4: **post-commit, pre-paint**, **after paint**, **next frame**, **microtask**. There is no mount/unmount or transition lifecycle (no `keepMounted`, no transition attributes, no `actionsRef`). This section covers the inset-position computation and server rendering/hydration.

- **SLD-ANI-01** — MUST. **Inset position** (inset modes only). For each thumb, with `side` = `width` (horizontal) / `height` (vertical):
  ```
  controlSize = controlRect[side] − thumbRect[side]
  offsetFromEdge = thumbRect[side] / 2 + controlSize × thumbPercent / 100
  positionPercent = offsetFromEdge / controlRect[side] × 100      // undefined when not finite
  ```
  It MUST be (re)computed: post-commit, pre-paint whenever `thumbPercent` changes or inset mode is entered; additionally one **microtask** after the thumb mounts (or inset mode is entered); and whenever a `ResizeObserver` reports a size change of the Control or the thumb element (so a slider revealed from `display: none` positions itself). The result sets the thumb's `--position` (SLD-DOM-10) and, for index `0` / the last index, the Indicator's start / end position (SLD-DOM-06). When no `ResizeObserver` exists, the resize recomputation is skipped.
  Source: `slider/thumb/SliderThumb.tsx:204-271`. Test: [L4] `SliderThumb.test.tsx` "recomputes inset positions when the slider becomes visible", "recomputes range inset positions when the slider becomes visible" (`30 → 32%`, `70 → 68%`, relative size `36%` with a 10 px thumb in a 100 px control); `SliderIndicator.test.tsx` "…edge…". Docs: `CHANGELOG.md` v1.4.0 "Fix edge thumb alignment after reveal".
- **SLD-ANI-02** — MUST. Until the inset position is known, inset-mode thumbs and the Indicator MUST be rendered with `visibility: hidden` (SLD-DOM-10, SLD-DOM-06) and `--position`/`--start-position` of `0%`; they become visible in the same update that supplies the first finite position.
  Source: `slider/thumb/SliderThumb.tsx:297-303`, `slider/indicator/SliderIndicator.tsx:20-23`. Test: [L4] `SliderThumb.test.tsx` "recomputes inset positions when the slider becomes visible" (hidden, `0%` before reveal).
- **SLD-ANI-03** — MUST. In `'edge'` mode the server output MUST contain the thumbs and Indicator with `visibility: hidden` and the pre-hydration script (SLD-STR-06, SLD-DOM-20); the script MUST run once, from the server HTML before the client takes over, computing every thumb's `--position` and the Indicator's variables from the server-rendered DOM (SLD-ANI-07) and removing the inline `visibility`; the client's first render keeps the already-executed script and then takes over positioning per SLD-ANI-01 (its first render still marks the elements hidden until its own computation lands — the server-set inline styles are not overwritten before that).
  Source: `slider/thumb/SliderThumb.tsx:300-301,473-475,491`, `slider/indicator/SliderIndicator.tsx:73,83`, `internals/PrehydrationScript.tsx:6-30,37-48`. Test: [L4] `SliderThumb.test.tsx` "renders the inline pre-hydration script for edge-aligned thumbs", "renders a single pre-hydration script with the last thumb".
- **SLD-ANI-04** — MUST. In `'edge-client-only'` mode no script is emitted and no marker attributes are rendered; server output and the client's first render have the thumbs/Indicator hidden until SLD-ANI-01 runs on the client.
  Source: `slider/root/SliderRoot.tsx:311,330`, `slider/thumb/SliderThumb.tsx:473`. Test: [L4] `SliderControl.test.tsx` "accounts for the thumb size when pressing an inset $name control" (`edge-client-only` renders and interacts). Docs: `CHANGELOG.md` v1.4.0 "Skip client-only prehydration scripts".
- **SLD-ANI-05** — MUST. In `'center'` mode server output MUST position thumbs and the Indicator by percentage immediately (SLD-DOM-05/09), with no hidden phase, provided range thumbs carry an `index` prop (SLD-API-17).
  Source: `slider/thumb/SliderThumb.tsx:193-196,291-296`. Test: [L2] `SliderThumb.test.tsx` "server-side rendering" → "single thumb", "multiple thumbs".
- **SLD-ANI-06** — MUST. A server-rendered `Slider.Value` has no `for` attribute (SLD-DOM-17) and a server-rendered range thumb without `index` has `data-index="-1"` and is hidden (SLD-STR-03); both resolve after hydration when the thumb registry publishes.
  Source: `slider/value/SliderValue.tsx:31-34`, `slider/thumb/SliderThumb.tsx:189-196`. Test: none.
- **SLD-ANI-07** — MUST. **Pre-hydration script.** Run from inside the last thumb (`document.currentScript.parentElement`):
  ```
  control = thumb.closest('[data-base-ui-slider-control]'); if none: return
  indicator = control.querySelector('[data-base-ui-slider-indicator]')      // may be null
  controlRect = control.getBoundingClientRect()
  side = control's data-orientation == 'vertical' ? 'height' : 'width'
  inputs = control.querySelectorAll('input[type="range"]'); range = inputs.length > 1; last = inputs.length − 1
  startPosition = null
  for i, input in inputs:
      value = parseFloat(input value attribute); if NaN: return
      thumb = input.parentElement; if none: return
      max = parseFloat(input max attribute ?? '100'); min = parseFloat(input min attribute ?? '0')
      thumbRect = thumb.getBoundingClientRect()
      controlSize = controlRect[side] − thumbRect[side]
      percent = value→percent per SLD-STA-20, then offset per SLD-ANI-01 → positionPercent
      thumb.style '--position' = `${percent}%`
      if percent is finite:
          thumb.style remove 'visibility'
          if indicator:
              if i == 0: startPosition = percent; indicator '--start-position' = `${percent}%`;
                         if not range: indicator remove 'visibility'
              else if i == last: indicator '--end-position' = `${percent}%`;        // note: --end-position is set here only
                         indicator '--relative-size' = `${percent − (startPosition ?? 0)}%`; indicator remove 'visibility'
  ```
  Source: `slider/thumb/prehydrationScript.template.js:1-69`, `slider/thumb/prehydrationScript.min.ts:5`. Test: none (script body not executed under test).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- SLD-PTR-01 — (default-prevented clause) a consumer `pointerdown` veto in the same dispatch stops the JS-owned drag start: on Server a C# consumer's `PreventBaseUIHandler()` arrives a round trip late and the drag starts regardless; WebAssembly honours it synchronously through `decide()` (ADR 04 §3); a consumer JavaScript listener's `preventDefault()` is honoured on both modes (UBT-KEY-03/04 row; ADR 04 Amendments 2026-10-18).
- SLD-PTR-04 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-05 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-06 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-07 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-08 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-09 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-10 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-11 — met on both modes because constraint C1 places the whole drag loop in the `slider` module (ADR 04 §1); listed so nobody moves it back to C#.
- SLD-PTR-16 — (default-prevented clause; "drag-start vetoes generally") as SLD-PTR-01.
- SLD-KEY-01 — (default-prevented clause; UBT-KEY-03/04 row as it applies to the thumb `keydown` owned by the `slider` module) met on both modes when the veto is a consumer JavaScript listener's `preventDefault()` (the module checks `defaultPrevented`); a C# consumer's `PreventBaseUIHandler()` is synchronous on WebAssembly through `decide()` and a round trip late on Server, where the key is handled (fallback "run"); Blazor event directives are rejected on parts.
- SLD-EVT-09 — (default-prevention and `preventBaseUIHandler()` clauses) as SLD-PTR-01 for the Control `pointerdown` and as SLD-KEY-01 for the thumb-input `keydown`.
- RND-STA-01 for mirrored fields (adopted; no SLD bullet) — the Thumb and Indicator `style` is JS-written at frame timing during a drag or a key press; on Server the C# state (`values`, `dragging`), the attributes rendered from it (`aria-valuenow`, the input `value`, `data-dragging`, `Slider.Value` text) and the `ClassValue`/`StyleValue` arguments follow one round trip later.

## 10. Positioning

Not applicable — the slider has no floating parts; thumb/indicator placement is covered by §5 and §9.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` / `mergeProps` / state attributes | [RND](../architecture/rendering-and-composition.md) | Every part renders a `div` (Value: `output`) with the root's state object and the slider mapping (`activeThumbIndex`, `max`, `min`, `minStepsBetweenValues`, `step`, `values` → nothing; `valid` → `data-valid`/`data-invalid`). Thumb input attributes merged with `mergeProps(internal, validationProps, consumerHandlers)`. |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `controlled: value`, `default: defaultValue ?? min`, `name: 'Slider'` (state `'value'`). |
| `createChangeEventDetails` / `createGenericEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | Reasons `input-change`, `keyboard`, `track-press`, `drag`; custom `activeThumbIndex`; the event is cloned with a synthetic `target` before the callback (SLD-EVT-04). |
| `useValueChanged` | [UTL](../architecture/shared-utilities.md) | On `fieldValue` (strict inequality) → `clearErrors` → `validation.change` → `setDirty`. |
| Field registration / validation / focus ownership | [FFI](../architecture/field-and-form-infrastructure.md) | `useRegisterFieldControl(validation.inputRef, rootId, fieldValue, undefined, !disabled, nameProp)`; `getValidationProps` on root and every input; `useSetFieldFocused(thumbDisabled, inputRef)` per thumb; `setTouched` on input/keyboard attempts and outside blur; `validation.commit` on blur in `'onBlur'` mode; `setFilled` never called. |
| Labelable scope (`useLabelableId`, `useLabel`, `resolveAriaLabelledBy`) | [FFI](../architecture/field-and-form-infrastructure.md) | Single-mode input claims the scope's control id; `Slider.Label` = `useLabel({ id: \`${rootId}-label\`, setLabelId, focusControl })` (non-native, custom focus target FFI-PTR-05); root `aria-labelledby = prop ?? fieldLabelId ?? sliderLabelId`. |
| `CompositeList` / `useCompositeListItem` | [CMP](../architecture/composite-and-list-navigation.md) | Root hosts the thumb registry (`elementsRef` = thumb elements by index, `onMapChange` → thumb map with `{ inputId }` metadata); thumbs register without an explicit index. Only the registry is used — no composite keyboard navigation. |
| `useDirection` | [UTL](../architecture/shared-utilities.md) | RTL flips the horizontal pointer axis, Left/Right keys, and the thumb `translate`; vertical `writing-mode` becomes `vertical-rl`. |
| `formatNumber` | [UTL](../architecture/shared-utilities.md) | `formatNumber(value, locale, format)` for Value text and `aria-valuetext`. |
| `valueToPercent`, `clamp`, `areArraysEqual`, `visuallyHidden`, `ownerDocument`/`activeElement`/`contains`/`getTarget` | [UTL](../architecture/shared-utilities.md) | As cited in §4–§6. |
| `useAnimationFrame` | [UTL](../architecture/shared-utilities.md) | One frame instance on Control for the deferred pointer focus (SLD-PTR-07); cancelled on unmount. |
| `isTouchLikePointerType` | [UBT](../architecture/button-behavior.md) | `pointerType === 'touch' || 'pen'` marks a gesture as expecting a compatibility `touchstart`. |
| `matchesFocusVisible` | [floating-and-positioning](../architecture/floating-and-positioning.md) FLT-A11Y-08 | `element.matches(':focus-visible')`, `true` under JSDOM / on error — gates SLD-KEY-11. |
| `PrehydrationScript` / `useIsHydrating` | [TRN](../architecture/transitions-and-mounting.md), [UTL](../architecture/shared-utilities.md) | `'edge'` mode only; script body `slider/thumb/prehydrationScript.min.ts`. |
| `warn` | [UTL](../architecture/shared-utilities.md) | SLD-EDGE-02. |

## 12. Edge cases, invariants and dev warnings

- **SLD-EDGE-01** — MUST. Rendering any part other than `Slider.Root` outside a `Slider.Root` subtree MUST throw `Error` with the message `Base UI: SliderRootContext is missing. Slider parts must be placed within <Slider.Root>.`
  Source: `slider/root/SliderRootContext.ts:110-118`. Test: [L2] `SliderControl.test.tsx` "throws a descriptive error when rendered outside <Slider.Root>".
- **SLD-EDGE-02** — MUST. In development, when `min >= max`, the warning `Base UI: Slider \`max\` must be greater than \`min\`.` MUST be emitted (once per message, UTL-EDGE-12) during render of the root.
  Source: `slider/root/SliderRoot.tsx:249-254`, `packages/utils/src/warn.ts`. Test: [L2] `SliderRoot.test.tsx` "warns when max is not greater than min". Docs: `CHANGELOG.md` v1.0.0-alpha.6 "Warn when `min` is not less than `max`".
- **SLD-EDGE-03** — SHOULD. The shared controlled-state diagnostics UTL-EDGE-22 (mode switch) and UTL-EDGE-23 (changed `defaultValue`) apply with `name: 'Slider'`, `state: 'value'`.
  Source: `slider/root/SliderRoot.tsx:118-122`, `packages/utils/src/useControlled.ts:48-81`. Test: none.
- **SLD-EDGE-04** — MUST. A raw value outside `[min, max]` (or an unsorted array) MUST be displayed, announced, submitted and validated in its normalized form (SLD-STA-03) while the raw value is left as supplied; interaction from such a state produces in-range candidates (and reports them even if they equal the display, SLD-EVT-10).
  Source: `slider/root/SliderRoot.tsx:167-172,195`. Test: [L2] `SliderRoot.test.tsx` "should not break when initial value is out of range", "clamps range values that fall outside the min and max bounds"; `SliderThumb.test.tsx` "thumb should not go out of bounds when the controlled value goes out of bounds" (L4 for the interaction clause). Docs: `CHANGELOG.md` v1.0.0-alpha.6 "Fix thumb positioning when controlled value violates min/max/step".
- **SLD-EDGE-05** — MUST. A thumb disabled only by its own `disabled` prop: its input is `disabled`, it is skipped by nearest-thumb selection, presses on it are ignored, it cannot own the field's focused flag — but its element and the other parts keep the root's `data-disabled` state (absent), because all parts render the root's state object.
  Source: `slider/thumb/SliderThumb.tsx:149,463`, `slider/control/SliderControl.tsx:159-171,253`. Test: [L4] `SliderRoot.test.tsx` "does not drag a thumb disabled via the `disabled` prop", "does not select a disabled thumb when pressing the track", "is removed when the focused thumb becomes disabled".
- **SLD-EDGE-06** — MUST. A `NaN` candidate MUST never be applied or reported: single mode drops it at the equality guard; in range mode it fails the minimum-distance check (`NaN` comparisons are false).
  Source: `slider/root/SliderRoot.tsx:195`, `slider/utils/validateMinimumDistance.ts:12-14`. Test: [L4] `SliderThumb.test.tsx` "does not commit NaN when more thumbs are rendered than values".
- **SLD-EDGE-07** — MUST. A substituted thumb element whose element reference (the part's `Element`, ADR 02 relaxation of RND-DOM-20…24) changes on every render of the part MUST NOT cause an update loop: the thumb registry settles, and the part's render count, observed through bUnit (L2), stops growing once the registry has published.
  Source: `slider/thumb/SliderThumb.tsx:189-191,464`, `internals/composite/list/useCompositeListItem.ts:62-82`. Test: [L2] `SliderThumb.test.tsx` "settles when the rendered component recreates its merged ref". Docs: `CHANGELOG.md` v1.8.0 "Prevent update loops from unstable refs".
- **SLD-EDGE-08** — MUST. When a substitute Control element drops the part's DOM handle (the part cannot see its element), pointer presses MUST be ignored without throwing, and unmounting MUST be clean.
  Source: `slider/control/SliderControl.tsx:461-465,494-506`. Test: [L4] `SliderControl.test.tsx` "degrades safely when a custom render function drops the control ref".
- **SLD-EDGE-09** — MUST. Event cloning (SLD-EVT-04) MUST use the originating event's own constructor and MUST NOT consult the global `window.event`.
  Source: `slider/root/SliderRoot.tsx:203-205`. Test: [L2] `SliderRoot.test.tsx` "should not rely on the global event when cloning change events".
- **SLD-EDGE-10** — MUST. The slider MUST work inside shadow roots and across shadow boundaries: keyboard changes on an input in a shadow root are handled; `Slider.Label` finds its control through enclosing shadow roots (a light-DOM element with the same id does not shadow it) and from a label portaled into a shadow root; clicking a label in a detached root does not throw.
  Source: `internals/labelable-provider/useLabel.ts:103-113`, `slider/label/SliderLabel.tsx:32-46`. Test: [L4] `SliderRoot.test.tsx` "should handle keyboard changes inside a shadow root"; `SliderLabel.test.tsx` "focuses the thumb in its shadow root when the outer document has a matching ID", "focuses a thumb in the light DOM from a label in a shadow root", "does not throw when the label is clicked in a detached React root". Docs: `CHANGELOG.md` v1.9.0 "Fix label focus in Shadow DOM".
- **SLD-EDGE-11** — MUST. More thumbs than values: extra thumbs render per SLD-STR-03 and are inert; fewer thumbs than values: the missing values have no thumb (they still exist in `values`, the Indicator spans to the last value, and `Slider.Value` lists them).
  Source: `slider/thumb/SliderThumb.tsx:193-196`, `slider/indicator/SliderIndicator.tsx:72`. Test: [L2] `SliderThumb.test.tsx` "does not commit NaN when more thumbs are rendered than values"; `SliderControl.test.tsx` "clears the grabbed offset when swapping to a thumb that is not rendered" (L4 for the swap citation).
- **SLD-EDGE-12** — MUST. Changing the value count mid-gesture is handled per SLD-PTR-19; the registry re-indexes thumbs post-commit, pre-paint.
  Source: `slider/control/SliderControl.tsx:177-182,361-367`. Test: [L4] `SliderControl.test.tsx` (the three "mid-drag" tests).
- **SLD-EDGE-13** — MUST. Direction comes only from `DirectionProvider` (UTL-I18N-03): a `dir="rtl"` attribute alone MUST NOT flip the slider.
  Source: `slider/control/SliderControl.tsx:118`, `slider/thumb/SliderThumb.tsx:147`. Test: [L2] `SliderRoot.test.tsx` "should handle RTL" (wraps in `DirectionProvider`) (L4 for the pointer citation).
- **SLD-EDGE-14** — SHOULD. The `index` prop of a range thumb SHOULD equal its document-order position among the slider's thumbs; the Control addresses thumb elements by registry (document-order) index while values are addressed by the `index` prop, so a mismatch makes presses move the wrong value (§16).
  Source: `slider/thumb/SliderThumb.tsx:189-193`, `slider/control/SliderControl.tsx:149,251-262,280`. Test: none.
- **SLD-EDGE-15** — MUST. Disabling the slider during a gesture MUST drop the gesture without a commit (SLD-PTR-17); re-enabling MUST allow a fresh gesture to start and commit normally.
  Source: `slider/control/SliderControl.tsx:479-483`. Test: [L4] `SliderControl.test.tsx` "ends a cancelled %s drag after disabling and re-enabling during a touch drag".
- **SLD-EDGE-16** — MUST. The vertical slider MUST NOT set `-webkit-appearance: slider-vertical` (or `appearance`) on the input; vertical orientation is conveyed through `aria-orientation` and `writing-mode` only, because the appearance value changes WebKit's horizontal arrow-key handling.
  Source: `slider/thumb/SliderThumb.tsx:307-310,445-451`. Test: [L2] `SliderRoot.test.tsx` "does not set the orientation via appearance for WebKit browsers".
- **SLD-EDGE-17** — MUST. `Slider.Value` text and `aria-valuetext` MUST re-format when `format` or `locale` change at runtime (formatters are cached per locale/options key, UTL-I18N-06, so equal options reuse the same formatter).
  Source: `slider/value/SliderValue.tsx:36-39`, `slider/thumb/SliderThumb.tsx:322-325`. Test: [L2] `SliderValue.test.tsx` "recomputes the formatted output when the format option changes"; `SliderRoot.test.tsx` "recomputes the thumb aria text when the format option changes".

### I18N

- **SLD-I18N-01** — MUST. Under an RTL `DirectionProvider`, a horizontal slider MUST map pointer position from the Control's right edge (SLD-PTR-04), swap the Left/Right arrow directions (SLD-KEY-02), and translate thumbs by `+50%` on the inline axis (SLD-DOM-09); the Indicator and thumb insets use logical `inset-inline-start`, so CSS direction handles their mirroring. Vertical sliders are unaffected except for `writing-mode` (SLD-I18N-03).
  Source: `slider/control/SliderControl.tsx:118,196`, `slider/thumb/SliderThumb.tsx:152,273,295,390,396`. Test: [L4] `SliderRoot.test.tsx` "should handle RTL", "keyboard interactions" (rtl cases); `SliderIndicator.test.tsx` "…horizontal-rtl…".
- **SLD-I18N-02** — MUST. Number formatting (SLD-API-09) MUST use `Intl.NumberFormat(locale, format)` semantics for the given `locale` (runtime default when absent), e.g. `de-DE` renders `70,51`.
  Source: `packages/utils/src/formatNumber.ts:5-28`. Test: [L2] `SliderRoot.test.tsx` "sets the locale when formatting a single value", "sets the locale when formatting a range value", "formats the value".
- **SLD-I18N-03** — MUST. For `orientation="vertical"` the input's inline style MUST include `writing-mode: vertical-lr` (LTR) or `vertical-rl` (RTL); horizontal sliders set no `writing-mode`.
  Source: `slider/thumb/SliderThumb.tsx:307-310,450`. Test: none (asserted indirectly by "does not set the orientation via appearance for WebKit browsers").

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. `/` groups tests with identical mapping; parametrized titles are given with their `%s`/`$name` placeholders as in the source. No requirement exists only because of a test — every assertion mapped below is captured by an ID defined in §2–§12 (see "Additional requirements introduced here").

### `SliderRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, element `<Slider.Root defaultValue={50} />`, default `testRenderPropWith: 'div'`, `button: false`) | RND-DOM-36…48, SLD-API-15 |
| "warns when max is not greater than min" | SLD-EDGE-02, SLD-API-06 |
| server-side rendering › "does not link Slider.Label before hydration" | SLD-ARIA-12, SLD-DOM-19, SLD-API-13 |
| "should not break when initial value is out of range" | SLD-EDGE-04, SLD-STA-03, SLD-PTR-04, SLD-PTR-12 |
| ARIA attributes › "has the correct aria attributes" | SLD-ARIA-01, SLD-ARIA-02, SLD-ARIA-03, SLD-ARIA-06, SLD-ARIA-07, SLD-API-14, SLD-DOM-14 |
| "should update aria-valuenow" | SLD-ARIA-02, SLD-DOM-16, SLD-EVT-02, SLD-KEY-02 |
| "should set default aria-valuetext on range slider thumbs" | SLD-ARIA-04 |
| rtl › "should handle RTL" | SLD-I18N-01, SLD-EDGE-13, SLD-DOM-09, SLD-PTR-04, SLD-PTR-12 |
| prop: disabled › "should render data-disabled on all subcomponents" | SLD-DOM-02, SLD-API-05, SLD-STA-05 |
| "explicitly blurs the focused thumb when disabled" | SLD-FOC-05 |
| "should not respond to drag events after becoming disabled" | SLD-FOC-05, SLD-PTR-17 |
| "should not respond to drag events if disabled" | SLD-PTR-17, SLD-API-05 |
| "does not drag a thumb disabled via the `disabled` prop" | SLD-API-16, SLD-EDGE-05, SLD-PTR-01, SLD-ARIA-11 |
| "does not drag a disabled thumb with touch events" | SLD-API-16, SLD-EDGE-05, SLD-PTR-12 |
| "does not change a single disabled thumb when pressing the track" | SLD-API-16, SLD-EDGE-05, SLD-PTR-03 |
| "does not select a disabled thumb when pressing the track" | SLD-PTR-03, SLD-EDGE-05 |
| prop: orientation › "sets the `aria-orientation` attribute" | SLD-ARIA-03, SLD-API-08 |
| "sets the data-orientation attribute" | SLD-DOM-02, SLD-DOM-01, SLD-ARIA-10 |
| "does not set the orientation via appearance for WebKit browsers" | SLD-EDGE-16, SLD-I18N-03 |
| "should report the right position" | SLD-PTR-04, SLD-API-08 |
| prop: step › "supports non-integer values" | SLD-ARIA-02, SLD-STA-03 |
| "should round value to step precision" | SLD-PTR-04, SLD-STA-14, SLD-STA-10 |
| "should not fail to round value to step precision when step is very small" / "should not fail to round value to step precision when step is very small and negative" | SLD-STA-13, SLD-STA-14, SLD-PTR-04 |
| prop: max › "sets the max attribute on the input" | SLD-DOM-14, SLD-API-06 |
| "should not go more than the max" | SLD-KEY-06 |
| "should reach right edge value" | SLD-PTR-04, SLD-STA-14 |
| prop: min › "sets the min attribute on the input" | SLD-DOM-14, SLD-API-06 |
| "should use min as the step origin" | SLD-STA-03 |
| "should not go less than the min" | SLD-KEY-06 |
| "clamps range values that fall outside the min and max bounds" | SLD-STA-03, SLD-EDGE-04 |
| prop: minStepsBetweenValues › "should enforce a minimum difference between range slider values" | SLD-KEY-07, SLD-STA-17, SLD-API-07 |
| prop: onValueCommitted › "single value" | SLD-EVT-05, SLD-STA-23, SLD-PTR-05, SLD-PTR-11, SLD-API-04 |
| "commits a %s track tap" (`touch`, `pen`) | SLD-PTR-12, SLD-PTR-11, SLD-EVT-05 |
| "commits a real touch track tap" | SLD-PTR-12, SLD-EVT-05 |
| "keeps tracking a touch drag with touch listeners after the browser cancels the pointer" | SLD-PTR-13 |
| "handles a touch press normally after a cancelled %s gesture" (`mouse`, `pen`, `touch`) | SLD-PTR-13, SLD-PTR-16, SLD-PTR-12, SLD-EVT-09 |
| "does not change the value on a %s tap on the thumb" (`touch`, `pen`) | SLD-PTR-15, SLD-PTR-05 |
| "array value" | SLD-EVT-05, SLD-PTR-11, SLD-API-04 |
| "does not commit a canceled change" | SLD-EVT-03, SLD-KEY-12 |
| "does not commit when keyboard interaction leaves the value unchanged" | SLD-EVT-06, SLD-STA-09, SLD-KEY-06 |
| "does not commit when keyboard interaction leaves a range value unchanged" | SLD-EVT-06, SLD-KEY-07 |
| "does not commit when thumb press leaves the value unchanged" | SLD-PTR-05, SLD-PTR-11 |
| "does not commit a canceled track press" / "does not commit a canceled drag" | SLD-EVT-03 |
| "commits the last applied value when a later move is canceled" / "commits the last applied range value when a later move is canceled" | SLD-EVT-12, SLD-EVT-03 |
| events › "should call handlers" | SLD-EVT-01, SLD-EVT-05, SLD-EVT-11, SLD-API-03 |
| "should support touch events" | SLD-PTR-12, SLD-PTR-09 |
| "should only listen to changes from the same touchpoint" | SLD-PTR-12 |
| "does not call onValueCommitted on outside taps after a touch interaction" / "removes the document touchend listener after a touch interaction" | SLD-PTR-11 |
| "should hedge against a dropped mouseup event" | SLD-PTR-09 |
| "should focus the slider when touching" | SLD-PTR-12 |
| "should focus the slider when dragging" | SLD-PTR-07 |
| "should not override the event.target on touch events" / "should not override the event.target on mouse events" | SLD-PTR-20, SLD-PTR-21, SLD-EVT-04 |
| dragging state › "should not apply data-dragging for click modality" / "should apply data-dragging for dragging modality" | SLD-PTR-14, SLD-STA-08, SLD-DOM-02 |
| prop: onValueChange › "is called when clicking on the control" / "is not called when clicking on the thumb" | SLD-PTR-05 |
| "should not react to right clicks" | SLD-PTR-16, SLD-PTR-01 |
| "provides the change reason for input events" | SLD-EVT-02, SLD-EVT-11, SLD-API-03 |
| "provides the change reason for keyboard interactions" | SLD-EVT-02, SLD-KEY-12 |
| "shows :focus-visible after keyboard interaction following a pointer press" | SLD-KEY-11, SLD-FOC-08, SLD-PTR-07 |
| "provides the change reason for track presses" | SLD-EVT-02, SLD-PTR-05 |
| "drags the intended thumb when 3 thumbs are present" | SLD-PTR-02, SLD-PTR-03, SLD-EVT-11, SLD-API-17 |
| "keeps focus and the active index on the logical thumb after a swap from a %s" (`thumb drag`, `track press`) | SLD-PTR-10, SLD-EVT-11, SLD-STA-19 |
| "keeps the original pressed thumb after a canceled swap and commits the next accepted move" | SLD-EVT-03, SLD-PTR-10 |
| "should fire only when the value changes" | SLD-EVT-06, SLD-STA-09, SLD-PTR-09 |
| "is called even if the $name did not change" (`readonly range`, `range`) | SLD-STA-09, SLD-EVT-10, SLD-API-01 |
| "should pass "name" and "value" as part of the event.target for onValueChange" | SLD-EVT-04, SLD-API-10 |
| "should not rely on the global event when cloning change events" | SLD-EDGE-09, SLD-EVT-04 |
| "should handle keyboard changes inside a shadow root" | SLD-EDGE-10 |
| "onValueCommitted is called with the same value as the latest onValueChange when pointerUp occurs at a different location than onValueChange" | SLD-EVT-05, SLD-STA-23, SLD-PTR-11 |
| keyboard interactions › "$direction, orientation: $orientation" › key: $key › "decrements the value" / "increments the value" (4 cases × keys) | SLD-KEY-02, SLD-API-08 |
| "decrements the value by largeStep when Shift is pressed" / "increments the value by largeStep when Shift is pressed" | SLD-KEY-03 |
| "stops at min when decrementing while Shift is pressed" / "stops at max when incrementing while Shift is pressed" | SLD-KEY-06, SLD-KEY-03 |
| "rounds fractional values to the configured step" | SLD-KEY-06, SLD-STA-15 |
| key: End › "sets value to max in a single value slider" / "sets value to the maximum possible value in a range slider" | SLD-KEY-05, SLD-KEY-13 |
| key: Home › "sets value to min in a single value slider" / "sets value to the minimum possible value in a range slider" | SLD-KEY-05, SLD-KEY-10 |
| key: PageUp › "increments the value by largeStep" | SLD-KEY-04 |
| "preserves largeStep increments when step uses a different grid" | SLD-KEY-06, SLD-STA-15 |
| "does not exceed max" | SLD-KEY-06 |
| key: PageDown › "decrements the value by largeStep" | SLD-KEY-04 |
| "does not go below min" | SLD-KEY-06 |
| "keypresses should correct invalid values" | SLD-KEY-06, SLD-STA-14 |
| prop: format › "formats the value" | SLD-API-09, SLD-I18N-02, SLD-DOM-18, SLD-ARIA-04 |
| "recomputes the thumb aria text when the format option changes" | SLD-ARIA-04, SLD-EDGE-17 |
| "formats range values" | SLD-ARIA-04, SLD-DOM-18 |
| prop: locale › "sets the locale when formatting a single value" / "sets the locale when formatting a range value" | SLD-I18N-02, SLD-API-09 |
| Form › "clears external errors on change" | SLD-FRM-14, SLD-ARIA-08, SLD-KEY-03 |
| form submission › "should include the slider value" / "should include range slider value" | SLD-FRM-01 |
| "submits clamped range slider values to onFormSubmit" / "submits a clamped single-thumb slider value to onFormSubmit" | SLD-FRM-03, SLD-FRM-02, SLD-STA-03 |
| "submits to an external form when `form` is provided" | SLD-FRM-10, SLD-API-10 |
| Field › "should receive disabled prop from Field.Root" | SLD-API-05, SLD-FRM-08 |
| "should receive name prop from Field.Root" | SLD-API-10, SLD-FRM-08 |
| "[data-touched]" | SLD-FRM-05, SLD-FRM-15 |
| "[data-dirty]" | SLD-STA-21, SLD-FRM-04, SLD-STA-02 |
| "[data-dirty] with a range value" | SLD-STA-21 |
| "[data-focused]" | SLD-FOC-04, SLD-FRM-15 |
| [data-focused] without a blur event › "is removed when the focused thumb becomes disabled" / "is removed when the focused thumb unmounts" / "is kept when focus moved to a sibling before the first thumb is disabled" / "is kept when a sibling thumb is disabled or unmounted" | SLD-FOC-04, SLD-API-16 |
| prop: validate › "validationMode=onSubmit" | SLD-FRM-07, SLD-FRM-04, SLD-FRM-15, SLD-ARIA-08, SLD-ARIA-09 |
| "moves focus to the range input on invalid submit" | SLD-FOC-07, SLD-FRM-02 |
| "validationMode=onBlur" | SLD-FRM-06 |
| "validationMode=onBlur preserves single-element array values" | SLD-FRM-06, SLD-STA-04 |
| "validationMode=onChange" / "validates once when changed by the user" / "revalidates when the controlled value changes externally" / "does not call validate on change when validationMode is omitted" | SLD-FRM-04 |
| "validates once with an array value for range sliders when changed by the user" / "receives an array value for range sliders" | SLD-FRM-04, SLD-FRM-13 |
| "Field.Label" | SLD-ARIA-06, SLD-ARIA-07, SLD-FRM-12 |
| "Slider.Label" | SLD-ARIA-06, SLD-ARIA-07, SLD-DOM-19 |
| "Slider.Label focuses slider on click" / "Slider.Label does not focus a thumb on click for range sliders" | SLD-FOC-06 |
| "does not set aria-labelledby when getAriaLabel is provided" | SLD-API-18, SLD-ARIA-05, SLD-ARIA-06 |
| "does not set fallback aria-labelledby when no label is rendered" | SLD-ARIA-05, SLD-ARIA-06 |
| "updates Slider.Label linkage when root id changes" | SLD-API-13, SLD-DOM-19, SLD-ARIA-07 |
| "Field.Description" | SLD-ARIA-09, SLD-DOM-01 |

### `SliderRoot.spec.tsx` (type-level)

| Test | Requirements |
|---|---|
| value / defaultValue narrowing for `onValueChange` and `onValueCommitted`; explicit generic; `details.event` narrowing by `reason` | SLD-API-02, SLD-API-03, SLD-API-04, SLD-EVT-08 |

### `SliderControl.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Slider.Root>`) | RND-DOM-36…48, SLD-API-28 |
| "does not apply a tabIndex by default" | SLD-DOM-03, SLD-FOC-01 |
| "throws a descriptive error when rendered outside <Slider.Root>" | SLD-EDGE-01, SLD-STR-01 |
| "moves the first of several thumbs stacked at the maximum" | SLD-PTR-18, SLD-PTR-03, SLD-EVT-11, SLD-STA-18 |
| "clears the grabbed offset when swapping to a thumb that is not rendered" | SLD-PTR-10, SLD-EDGE-11, SLD-STA-19 |
| "ignores a drag when collision behavior cannot satisfy the minimum distance" | SLD-PTR-09, SLD-STA-17, SLD-STA-19 |
| "accounts for the thumb size when pressing an inset $name control" (`horizontal`, `vertical`) | SLD-PTR-24, SLD-PTR-04, SLD-API-11, SLD-ANI-04 |
| "preserves single-element array values on track press" | SLD-STA-04, SLD-API-02, SLD-PTR-05 |
| "releases pointer capture when the interaction ends" | SLD-PTR-06, SLD-PTR-11 |
| "degrades safely when a custom render function drops the control ref" | SLD-EDGE-08 |
| "handles touch interactions that originate on a text node" | SLD-PTR-12 |
| "ignores touch interactions when no thumbs are composed" | SLD-PTR-12, SLD-PTR-03 |
| cancelled gestures › "ignores %s from another finger during a touch drag" (`touchEnd`, `touchCancel`) | SLD-PTR-12, SLD-PTR-13 |
| "ends a cancelled %s drag after disabling and re-enabling during a touch drag" (`mouse`, `pen`) | SLD-EDGE-15, SLD-PTR-17, SLD-PTR-23, SLD-PTR-11 |
| "commits and ends a %s drag on pointercancel" (`mouse`, `pen`) | SLD-PTR-11, SLD-PTR-08, SLD-STA-08, SLD-PTR-23 |
| "keeps a touch drag going after pointercancel and commits and ends it on touchcancel" | SLD-PTR-13, SLD-PTR-11 |
| "does not resurrect a removed thumb value when the range shrinks mid-drag" / "does not commit a stale value when the range shrinks mid-drag and the pressed thumb stays valid" / "clears cached interaction state when the controlled range grows mid-drag" | SLD-PTR-19, SLD-EDGE-12 |

### `SliderThumb.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Slider.Root>`) | RND-DOM-36…48, SLD-API-25 |
| "sets the thumb index data attribute" | SLD-DOM-08, SLD-API-17 |
| "settles when the rendered component recreates its merged ref" | SLD-EDGE-07 |
| ARIA attributes › "forwards $attr to the input" (`aria-label`, `aria-labelledby`, `aria-describedby`, `aria-valuetext`) | SLD-API-18, SLD-API-19, SLD-API-20, SLD-ARIA-04, SLD-ARIA-05, SLD-ARIA-06, SLD-ARIA-08 |
| "prefers getAriaValueText over a direct aria-valuetext prop" | SLD-API-19, SLD-ARIA-04 |
| prop: onKeyDown › "forwards key events that the slider does not handle" | SLD-API-22, SLD-KEY-01, SLD-KEY-09 |
| "forwards handled $key key events" (`ArrowRight`, `PageUp`) | SLD-API-22 |
| "allows preventing the internal key handling" | SLD-KEY-01, SLD-EVT-09, SLD-DOM-21 |
| "does not commit NaN when more thumbs are rendered than values" | SLD-STR-03, SLD-EDGE-06, SLD-EDGE-11, SLD-STA-17 |
| events › focus and blur › "single thumb" | SLD-FOC-01, SLD-ARIA-01, SLD-DOM-14, SLD-API-22, SLD-KEY-09 |
| "multiple thumbs" | SLD-FOC-02, SLD-KEY-10, SLD-ARIA-01 |
| "does not emit extra blur and focus events when restoring focus-visible" | SLD-KEY-11, SLD-API-22 |
| "forwards focus and blur to the input so `currentTarget` is the input" | SLD-API-22 |
| "does not commit field validation when moving focus between range thumbs" | SLD-FOC-04, SLD-FRM-06 |
| "commits field validation when focus moves from a thumb to an arbitrary Control child" | SLD-FRM-06, SLD-FRM-05, SLD-FOC-04 |
| change › "handles change events" | SLD-EVT-02, SLD-DOM-16 |
| "does not change the value beyond min and max" | SLD-STA-16, SLD-EVT-06 |
| "handles non-integer values" | SLD-DOM-14, SLD-ARIA-02 |
| prop: tabIndex › "does not apply tabIndex to the thumb element by default" | SLD-API-23, SLD-FOC-01, SLD-DOM-13 |
| "can be removed from the tab sequence" | SLD-API-23, SLD-FOC-02 |
| prop: children › "renders the nested input as a sibling to children" / "renders the nested input when using the short form render prop" / "renders the nested input when using the long form render prop" | SLD-STR-02, SLD-API-24 |
| prop: inputRef › "can focus the input element" | SLD-API-21 |
| "preserves the grab offset when dragging a vertical thumb" | SLD-PTR-02, SLD-PTR-04 |
| stacking order › "relies on DOM order before any thumb is used" | SLD-DOM-12, SLD-STA-07 |
| "keeps the most recently active thumb on top after focus moves away" | SLD-DOM-12, SLD-STA-06, SLD-STA-07, SLD-FOC-03 |
| prop: thumbAlignment › "recomputes inset positions when the slider becomes visible" / "recomputes range inset positions when the slider becomes visible" | SLD-ANI-01, SLD-ANI-02, SLD-DOM-10, SLD-DOM-06, SLD-API-11 |
| positioning styles › positions the thumb when dragged › "single thumb" / "multiple thumbs" | SLD-DOM-09, SLD-STA-20 |
| prop: thumbCollisionBehavior › "prevents thumbs from passing each other when set to "none"" / "pushes adjacent thumbs forward when set to "push"" / "allows thumbs to swap when set to "swap"" / "maintains minimum steps between values when swapping" | SLD-API-12, SLD-PTR-10, SLD-STA-18, SLD-STA-19 |
| positions the thumb when the controlled value changes externally › "single thumb" / "multiple thumbs" | SLD-DOM-09, SLD-STA-11, SLD-EVT-07 |
| "thumb should not go out of bounds when the controlled value goes out of bounds" | SLD-EDGE-04, SLD-STA-03, SLD-DOM-09 |
| server-side rendering › "single thumb" / "multiple thumbs" | SLD-ANI-05, SLD-DOM-09 |
| "renders the inline pre-hydration script for edge-aligned thumbs" / "renders a single pre-hydration script with the last thumb" | SLD-STR-06, SLD-DOM-20, SLD-ANI-03 |

### `SliderIndicator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Slider.Root>`) | RND-DOM-36…48, SLD-API-28 |
| "preserves keyboard and indicator parity for $axis $alignment $valueShape sliders" (12 cases: horizontal-ltr / horizontal-rtl / vertical-ltr × center / edge × single / range) | SLD-DOM-05, SLD-DOM-06, SLD-ANI-01, SLD-API-25, SLD-KEY-02, SLD-KEY-04, SLD-KEY-05, SLD-I18N-01 |

### `SliderLabel.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside a `<Slider.Root defaultValue={50}>` with a Control/Thumb) | RND-DOM-36…48, SLD-API-27 |
| "focuses the registered thumb when composed within a Field" | SLD-FOC-06, SLD-DOM-15, SLD-FRM-12 |
| "focuses the thumb in its shadow root when the outer document has a matching ID" / "focuses a thumb in the light DOM from a label in a shadow root" | SLD-FOC-06, SLD-EDGE-10 |
| "does not throw when the label is clicked in a detached React root" | SLD-EDGE-10, SLD-FOC-06 |
| "does nothing when a Field slider has no thumb to focus" | SLD-FOC-06 |

### `SliderTrack.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Slider.Root>`) | RND-DOM-36…48, SLD-STR-04, SLD-API-28 |

### `SliderValue.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLOutputElement`, rendered inside `<Slider.Root>`) | RND-DOM-36…48, SLD-API-26 |
| "renders a single value" / "renders a range" / "renders all thumb values" | SLD-DOM-18 |
| "associates the output with every thumb input" | SLD-DOM-17, SLD-DOM-15 |
| "recomputes the formatted output when the format option changes" | SLD-EDGE-17, SLD-API-09 |
| prop: children › "accepts a render function" | SLD-API-26 |

### `utils/getPushedThumbValues.test.ts`

| Test | Requirements |
|---|---|
| "pushes the next thumb forward when moving past it" / "ensures minimum distance between thumbs while pushing forward" / "pushes previous thumbs backward when moving before them" / "pushes multiple thumbs in sequence" / "allows fractional minimum distances" / "restores pushed thumbs towards their initial value when space allows" | SLD-STA-18 |

### `utils/getSliderValue.test.ts`

| Test | Requirements |
|---|---|
| "clamps a single value to the min/max range" / range neighbours › "does not let a thumb cross a neighbour whose value is 0" / "bounds a thumb between its real neighbours" / "leaves the outer edges unbounded by missing neighbours" | SLD-STA-16 |

### `utils/resolveThumbCollision.test.ts`

| Test | Requirements |
|---|---|
| "prevents thumbs from passing each other when behavior is "none"" / "pushes thumbs forward without cling when behavior is "push"" / "keeps pushed thumbs in place when moving backward in push mode" / "swaps thumbs when behavior is "swap"" / "maintains swap continuity with minimum steps when provided current and initial values" / "does not swap before reaching neighbour value with minimum steps" / "swaps once reaching the neighbour value with minimum steps" / "does not swap backward before reaching neighbour value with minimum steps" / "swaps backward once reaching the neighbour value with minimum steps" / "does not move the clamped neighbour when swapping across with minimum steps" | SLD-STA-19 |
| "uses current values when a controlled range grows during a swap interaction" / "returns a scalar when the live values shrink to one item during an interaction" | SLD-STA-19, SLD-PTR-19 |

### `utils/roundValueToStep.test.ts`

| Test | Requirements |
|---|---|
| "preserves precision from the step origin" / "preserves decimal precision for steps greater than one" | SLD-STA-14, SLD-STA-13 |

### Additional requirements introduced here

None. Every test-only observation (NaN guard, unstable ref settling, dropped control handle, global-event independence, stacked-at-max selection, mid-drag count changes, focus-visible restoration, touch identifier filtering, listener cleanup) was given its ID in the owning area above (SLD-EDGE-06…09, SLD-PTR-18/19, SLD-KEY-11, SLD-PTR-12, SLD-PTR-11).

## 14. React-specific notes and porting considerations

Non-normative.

- **Change events.** The input's `onChange` is React's synthetic change, which fires on the native `input` event of `<input type="range">`; a port should treat the range input's `input` event (and `change`) as the `input-change` source and read `valueAsNumber`. Tests dispatch `change` with `target.value` strings; the browser sanitizes an unparsable string to the input's default (midpoint), which is why `"[data-dirty]"` passes. Port: see decisions/05-state-store-and-event-details.md §2 (value/`ValueChanged`/`OnValueChange`) and §3 (`SliderChangeEventDetails.ActiveThumbIndex`); decisions/04-js-interop-boundary.md §3 (`details.event` is a snapshot record, so the cloned-event `target` override of SLD-EVT-04 becomes snapshot fields).
- **Handler ordering** follows `mergeProps` (RND-EVT-02): consumer handlers passed as props run before the part's internal handler for the same event, and the Thumb's own `pointerdown` runs before the Control's because of DOM bubbling, not framework ordering. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (handler chaining, `PreventBaseUIHandler` veto); for the consumer `onPointerDown` default-prevented clause of SLD-PTR-01, which gates a JS-owned drag start, see decisions/01-hosting-model-and-timing.md §Relaxation table and decisions/04-js-interop-boundary.md §3 (`decide()`, fallback "start" on Server).
- **Timing.** `useIsoLayoutEffect` = post-commit, pre-paint (inset position on value change, the inset-mode `ResizeObserver` attachment, disabled blur, field registration, label id publication, `useValueChanged` pipeline). `queueMicrotask` (inset position once after mount). `useAnimationFrame` = next frame (pointer focus). The native `touchstart` listener and the disable-time listener cleanup (SLD-PTR-17) run in passive effects (after paint); a touch that starts before the first paint is not captured. The `touchstart` listener is attached with `{ passive: true }`, as are the document `touchmove`/`touchend`/`touchcancel` and `pointermove` listeners. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (timing-term mapping) and §Relaxation table (the drag loop SLD-PTR-04..11 is met on both modes because the whole loop is JavaScript); decisions/04-js-interop-boundary.md §1 (`slider` component module).
- **`useStableCallback` trampolines** make `setValue`, `handleInputChange`, `onValueChange`, `onValueCommitted` and the gesture handlers identity-stable; the port only needs the semantics (always the latest callback is invoked). Invoking them during render is an error (UTL-EDGE-27).
- **Memoized `values`.** `values` is recomputed only when `min`, `max`, range-ness or the raw value identity change; a consumer passing a new but equal array causes a new `values` identity and therefore re-runs the `fieldValue` pipeline (SLD-FRM-04) — `clearErrors`, `validation.change` (which revalidates in change-validation modes) and `setDirty` — even though nothing observable changed. A port that compares by value instead would skip that redundant revalidation; no test depends on it. Port: see decisions/05-state-store-and-event-details.md §1 (`Is<T>` equality) and §2.
- **Hydration.** `suppressHydrationWarning` is set on every thumb input and, in `'edge'` mode, on the thumb and Indicator elements, because the pre-hydration script mutates their inline styles before React hydrates (SLD-ANI-03). `useIsHydrating` distinguishes the hydration pass from client mounts (React 18+: `useSyncExternalStore` server snapshot; React 17: one extra "not hydrated" render for every mount — TRN-ANI-36). The `#prehydration/slider/thumb` import resolves to an empty string in client bundles, so the script text exists only in server output. Port: see decisions/01-hosting-model-and-timing.md §Consequences (prerender is in scope for markup only) and decisions/06-testing-harness.md §Decision (SLD-ANI-03 gets a dedicated prerendering fixture); whether the port keeps the executing pre-hydration script is not settled by either ADR (§16 Q14).
- **Thumb registry.** `CompositeList`/`useCompositeListItem` provide the thumb index (document order) and the `thumbMap` used by `Slider.Value` for `for`; the index is `-1` until the first publication (post-commit, pre-paint), which is why `index` is required for SSR of range sliders. The registration callback is identity-sensitive to tolerate substitute elements that recreate their DOM handle (SLD-EDGE-07).
- **Refs captured once.** The Control captures its computed style object on first attach (`getComputedStyle` returns a live declaration, so padding/border changes are still seen); the field's shared validation input handle is whichever thumb input attached last. Port: see decisions/04-js-interop-boundary.md §1 (computed-style and rect reads belong to the `slider` module).
- **StrictMode / double invocation.** No slider-specific guards; the registry and field registration tolerate register → unregister → register (CMP-STA-20, FFI).
- **`forwardRef` generics.** `Slider.Root` is a generic component over the value type; the port's API should preserve that `onValueChange` receives a number for single sliders and an array for range sliders. Port: see decisions/05-state-store-and-event-details.md §2.
- **`event.pointerId` truthiness.** Pointer capture is skipped when `pointerId` is `0` (falsy) — see §16. Port: see decisions/04-js-interop-boundary.md §1 (pointer capture is part of the JS drag loop).
- **Inline position styles.** The thumb's and Indicator's inline positioning (`--position`, `--start-position`, `--end-position`, `--relative-size`, inline `visibility`) are computed from layout reads (SLD-ANI-01) and change on every drag frame. Port: see decisions/04-js-interop-boundary.md §2 (JavaScript is the single writer of these properties; C# mirrors them into the state object and renders no `style` on those elements). The centre-mode offsets (`insetInlineStart`/`bottom` percentage, `translate`, `z-index`, SLD-DOM-05/09/12) and the consumer's own `style` on Thumb/Indicator are not in that ADR's table — see §16 Q16. Superseded 2026-10-18: C# renders the initial inline values once and JavaScript owns `style` from attach (ADR 04 Amendments, handover at attach).
- **Algorithm placement (constraint C1).** `roundValueToStep` (SLD-STA-14) is used by both the keyboard path (SLD-KEY-06, a `keydown` on the thumb input) and the pointer path (SLD-PTR-04, the JS drag loop); `getSliderValue`, `validateMinimumDistance` and `resolveThumbCollision` are likewise shared between the two paths. Port: see decisions/01-hosting-model-and-timing.md §Decision item 2 and decisions/04-js-interop-boundary.md §1 (every SLD ALG section lives in the `slider` module, implemented once); this forces the keyboard step to run in that module too, or the shared helpers to be called from C# through it — see §16 Q15.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| SLD-STR-01 | MUST | Root renders one `div`; every other part must be inside a Root (else error) | 2 |
| SLD-STR-02 | MUST | Thumb renders children, then nested `input[type=range]`, then (edge/last) script | 2 |
| SLD-STR-03 | MUST | One thumb per value; valueless thumb is inert and hidden | 2 |
| SLD-STR-04 | MUST | Track is structural only (`position: relative`) | 2 |
| SLD-STR-05 | MUST | Pointer interaction only for thumbs inside Control | 2 |
| SLD-STR-06 | MUST | Single pre-hydration script in the last thumb, edge mode, server/hydration only | 2 |
| SLD-API-01 | MUST | `value`/`defaultValue` ownership; default `defaultValue ?? min` | 3.1 |
| SLD-API-02 | MUST | Array raw value → range mode (one-element arrays included); callbacks keep shape | 3.1 |
| SLD-API-03 | MUST | `onValueChange(value, details)` with `activeThumbIndex` | 3.1 |
| SLD-API-04 | MUST | `onValueCommitted(value, genericDetails)` | 3.1 |
| SLD-API-05 | MUST | effective disabled = field OR prop; default false; effects | 3.1 |
| SLD-API-06 | MUST | `min` 0 / `max` 100 defaults; on inputs; warn when `min >= max` | 3.1 |
| SLD-API-07 | MUST | `step` 1, `largeStep` 10, `minStepsBetweenValues` 0 | 3.1 |
| SLD-API-08 | MUST | `orientation` default horizontal; vertical axis effects; no key remap | 3.1 |
| SLD-API-09 | MUST | `format`/`locale` feed every formatted value; live updates | 3.1 |
| SLD-API-10 | MUST | effective name = field ?? prop; `name`/`form` on inputs only | 3.1 |
| SLD-API-11 | MUST | `thumbAlignment` center/edge/edge-client-only semantics | 3.1 |
| SLD-API-12 | MUST | `thumbCollisionBehavior` default push; pointer only | 3.1 |
| SLD-API-13 | MUST | root `id` prop/generated; label id `${id}-label` follows changes | 3.1 |
| SLD-API-14 | MUST | root `aria-labelledby` consumed for root and inputs | 3.1 |
| SLD-API-15 | MUST | RND customization; other props forwarded to root; consumed list | 3.1 |
| SLD-API-16 | MUST | Thumb `disabled` = prop OR root; input disabled; press/selection ignored | 3.7 |
| SLD-API-17 | MUST | Thumb `index`: 0 single; prop ?? registry (document order, −1 until published) | 3.7 |
| SLD-API-18 | MUST | input `aria-label` = `getAriaLabel(index)` → prop | 3.7 |
| SLD-API-19 | MUST | input `aria-valuetext` = `getAriaValueText` → prop → default | 3.7 |
| SLD-API-20 | MUST | Thumb `aria-labelledby`/`aria-describedby` go to the input | 3.7 |
| SLD-API-21 | MUST | `inputRef` receives the input | 3.7 |
| SLD-API-22 | MUST | `onFocus`/`onBlur`/`onKeyDown` on the input; run first; no synthetic pair | 3.7 |
| SLD-API-23 | MUST | `tabIndex` to input only; no default `tabindex`; −1 removes from tab order | 3.7 |
| SLD-API-24 | MUST | `children` rendered before the input | 3.7 |
| SLD-API-25 | MUST | thumb element `id`; RND customization; forwarding | 3.7 |
| SLD-API-26 | MUST | Value `children` function `(formattedValues, values)`; `aria-live` default off | 3.3 |
| SLD-API-27 | MUST | Label ignores `id`; renders `${rootId}-label`; forwards others | 3.2 |
| SLD-API-28 | MUST | Control/Track/Indicator: no part props; forwarding | 3.4 |
| SLD-STA-01 | MUST | ownership fixed at first render | 4 |
| SLD-STA-02 | MUST | initial raw value `defaultValue ?? min` | 4 |
| SLD-STA-03 | MUST | normalized `values`: clamp, sort ascending; all display uses it | 4 |
| SLD-STA-04 | MUST | one-element arrays stay arrays everywhere | 4 |
| SLD-STA-05 | MUST | state object = field state + 8 slider fields, shared by all parts | 3.1 |
| SLD-STA-06 | MUST | `activeThumbIndex` = focused input's index; −1 on blur/release/disable | 4 |
| SLD-STA-07 | MUST | last-used thumb index latches non-negative actives | 4 |
| SLD-STA-08 | MUST | `dragging` from pointerdown (or 3rd touch move) until release | 4 |
| SLD-STA-09 | MUST | equality guard against the raw value; NaN dropped | 4 |
| SLD-STA-10 | MUST | uncontrolled: applied candidate becomes raw value as computed | 4 |
| SLD-STA-11 | MUST | controlled: display follows `value` prop only, re-normalized | 4 |
| SLD-STA-12 | MUST | `fieldValue` = array (range) / `values[0]` (single) | 4 |
| SLD-STA-13 | MUST | `getDecimalPrecision` algorithm | 4.1 |
| SLD-STA-14 | MUST | `roundValueToStep` algorithm (grid anchored at `min`) | 4.1 |
| SLD-STA-15 | MUST | `getNewValue` keyboard step algorithm (increment not snapped) | 4.1 |
| SLD-STA-16 | MUST | `getSliderValue` neighbour clamp + sort | 4.1 |
| SLD-STA-17 | MUST | `validateMinimumDistance`; failing candidate discarded whole | 4.1 |
| SLD-STA-18 | MUST | `getPushedThumbValues` push algorithm | 4.1 |
| SLD-STA-19 | MUST | `resolveThumbCollision` push/swap/none algorithm | 4.1 |
| SLD-STA-20 | MUST | `valueToPercent` on normalized value | 4.1 |
| SLD-STA-21 | MUST | field `dirty` after each `fieldValue` change; written after `validation.change` | 4.1 |
| SLD-STA-22 | MUST | Slider never sets field `filled` | 4.1 |
| SLD-STA-23 | MUST | last applied reason recorded (initially `none`) for pointer commits | 4.1 |
| SLD-DOM-01 | MUST | root: `role=group`, id, `aria-labelledby`, consumer attrs, validation props; no tabindex | 5.1 |
| SLD-DOM-02 | MUST | state attributes on every part; numeric/index fields never emitted | 5.1 |
| SLD-DOM-03 | MUST | Control: no tabindex/role; edge marker attr; pointer handling | 5.2 |
| SLD-DOM-04 | MUST | Track: `position: relative` + state attrs | 5.3 |
| SLD-DOM-05 | MUST | Indicator center-mode inline styles | 5.4 |
| SLD-DOM-06 | MUST | Indicator inset-mode vars (`--start-position`, `--relative-size`) and hidden phase | 5.4 |
| SLD-DOM-07 | MUST | Indicator edge marker attr | 5.4 |
| SLD-DOM-08 | MUST | thumb `data-index` | 5.5 |
| SLD-DOM-09 | MUST | thumb center-mode positioning styles (percent, 50%, translate, z-index) | 5.5 |
| SLD-DOM-10 | MUST | thumb inset-mode `--position`/`var(--position)` and hidden phase | 5.5 |
| SLD-DOM-11 | MUST | thumb visually hidden when percent non-finite (center mode) | 5.5 |
| SLD-DOM-12 | MUST | thumb z-index stacking rules | 5.5 |
| SLD-DOM-13 | MUST | thumb element: pointerdown handler; no role/tabindex/ARIA | 5.5 |
| SLD-DOM-14 | MUST | input attributes, handlers and visually-hidden 100% style; native role | 5.6 |
| SLD-DOM-15 | MUST | input id: labelable control id (single) / generated per thumb (range) | 5.6 |
| SLD-DOM-16 | MUST | input `value` tracks `values[index]` (`""` when none) | 5.6 |
| SLD-DOM-17 | MUST | Value `<output for="…">` = thumb input ids in index order | 5.7 |
| SLD-DOM-18 | MUST | Value default text = formatted values joined by `" – "` | 5.7 |
| SLD-DOM-19 | MUST | Label id `${rootId}-label`, click/pointerdown handlers, id published | 5.8 |
| SLD-DOM-20 | MUST | pre-hydration script element content/nonce/lifetime | 5.9 |
| SLD-DOM-21 | MUST | attribute precedence per part | 5.9 |
| SLD-ARIA-01 | MUST | native `slider` role on each input; nowhere else | 5.10 |
| SLD-ARIA-02 | MUST | input `aria-valuenow` = normalized value; absent when none | 5.10 |
| SLD-ARIA-03 | MUST | input `aria-orientation` = orientation | 5.10 |
| SLD-ARIA-04 | MUST | `aria-valuetext` precedence; default "start/end range" text | 5.10 |
| SLD-ARIA-05 | MUST | `aria-label` precedence; suppresses labelledby fallback | 5.10 |
| SLD-ARIA-06 | MUST | input `aria-labelledby` = prop → root label id (no aria-label) → absent | 5.10 |
| SLD-ARIA-07 | MUST | root label id = prop → Field label → Slider.Label → absent | 5.10 |
| SLD-ARIA-08 | MUST | input `aria-describedby` composition and `aria-invalid` | 5.10 |
| SLD-ARIA-09 | MUST | root `aria-describedby` composition and `aria-invalid` | 5.10 |
| SLD-ARIA-10 | MUST | root `role="group"` | 5.10 |
| SLD-ARIA-11 | MUST | disabled via native attribute only; no aria-disabled/readonly/required | 5.10 |
| SLD-ARIA-12 | MUST | server markup has no Slider.Label-derived `aria-labelledby` | 5.10 |
| SLD-KEY-01 | MUST | ignore prevented keydown and unhandled keys | 6.1 |
| SLD-KEY-02 | MUST | arrow direction mapping with RTL; orientation irrelevant | 6.1 |
| SLD-KEY-03 | MUST | Shift+arrow uses `largeStep` | 6.1 |
| SLD-KEY-04 | MUST | PageUp/PageDown use `largeStep` | 6.1 |
| SLD-KEY-05 | MUST | Home/End targets (neighbour ∓ min distance in range) | 6.1 |
| SLD-KEY-06 | MUST | candidate = `getNewValue(roundValueToStep(current))`, clamped | 6.1 |
| SLD-KEY-07 | MUST | neighbour clamp + minimum-distance check on keyboard candidates | 6.1 |
| SLD-KEY-08 | MUST | preventDefault on all handled keys; stopPropagation except Page keys | 6.1 |
| SLD-KEY-09 | MUST | unhandled keys not intercepted | 6.1 |
| SLD-KEY-10 | MUST | Tab order = document order of inputs | 6.1 |
| SLD-KEY-11 | MUST | focus-visible restoration (blur/focus) without side effects | 6.1 |
| SLD-KEY-12 | MUST | keyboard reason/activeThumbIndex/event; synchronous commit | 6.1 |
| SLD-KEY-13 | MUST | keyboard ignores collision behavior (never crosses neighbour) | 6.1 |
| SLD-KEY-14 | MUST | disabled input gets no keys; no read-only mode | 6.1 |
| SLD-PTR-01 | MUST | pointerdown preconditions; disabled thumb target ignored | 6.2 |
| SLD-PTR-02 | MUST | thumb pointerdown records index and grab offset | 6.2 |
| SLD-PTR-03 | MUST | pressed-thumb resolution (stacked at max; nearest enabled thumb) | 6.2 |
| SLD-PTR-04 | MUST | position → value algorithm | 6.2 |
| SLD-PTR-05 | MUST | track press applies `track-press`; thumb press applies nothing | 6.2 |
| SLD-PTR-06 | MUST | pointer capture set/released (non-zero pointerId) | 6.2 |
| SLD-PTR-07 | MUST | focus handling on press (preventDefault if focused; else next-frame focus) | 6.2 |
| SLD-PTR-08 | MUST | dragging true at pointerdown; document listeners added | 6.2 |
| SLD-PTR-09 | MUST | move handling; `buttons === 0` hedge; `drag` reason | 6.2 |
| SLD-PTR-10 | MUST | collision resolution during drag; swap switches pressed index/focus when applied | 6.2 |
| SLD-PTR-11 | MUST | end of gesture: reset, commit last applied, release capture, cleanup | 6.2 |
| SLD-PTR-12 | MUST | touch path (touchstart, identifier tracking, immediate focus) | 6.2 |
| SLD-PTR-13 | MUST | pointercancel ignored while touch tracked; touch end ends | 6.2 |
| SLD-PTR-14 | MUST | touch-only gesture: dragging after third move | 6.2 |
| SLD-PTR-15 | MUST | touch/pen tap on thumb changes nothing | 6.2 |
| SLD-PTR-16 | MUST | non-primary / prevented pointerdown ignored; gesture flag reset | 6.2 |
| SLD-PTR-17 | MUST | disabled: presses ignored; gesture dropped on disable | 6.2 |
| SLD-PTR-18 | MUST | stacked-at-max press drags lowest stacked thumb | 6.2 |
| SLD-PTR-19 | MUST | value-count change mid-gesture handling | 6.2 |
| SLD-PTR-20 | MUST | native events never mutated | 6.2 |
| SLD-PTR-21 | MUST | mouse events alone do nothing | 6.2 |
| SLD-PTR-22 | SHOULD | consumer sets `touch-action: none` on Control | 6.2 |
| SLD-PTR-23 | MUST | per-gesture state reset/cleared | 6.2 |
| SLD-PTR-24 | MUST | inset half-thumb offset in position → value | 6.2 |
| SLD-FOC-01 | MUST | only inputs focusable | 6.3 |
| SLD-FOC-02 | MUST | tab order document order; `tabIndex=-1` removes | 6.3 |
| SLD-FOC-03 | MUST | focus/blur update active index | 6.3 |
| SLD-FOC-04 | MUST | field focused ownership; sibling-thumb blur is neutral | 6.3 |
| SLD-FOC-05 | MUST | disabling blurs focused descendant, resets active | 6.3 |
| SLD-FOC-06 | MUST | Label click focus target resolution | 6.3 |
| SLD-FOC-07 | MUST | invalid submit focuses the registered range input | 6.3 |
| SLD-FOC-08 | MUST | focusVisible flags per focus source | 6.3 |
| SLD-EVT-01 | MUST | `onValueChange` before state update with full details | 7 |
| SLD-EVT-02 | MUST | reason literals per action; `none` never emitted | 7 |
| SLD-EVT-03 | MUST | `cancel()` semantics | 7 |
| SLD-EVT-04 | MUST | cloned event with `target = { value, name }` | 7 |
| SLD-EVT-05 | MUST | `onValueCommitted` timing/value/reason | 7 |
| SLD-EVT-06 | MUST | no callbacks for unchanged candidates | 7 |
| SLD-EVT-07 | MUST | no callbacks for prop changes | 7 |
| SLD-EVT-08 | MUST | `details.event` native event types | 7 |
| SLD-EVT-09 | MUST | consumer handlers run first and may suppress | 7 |
| SLD-EVT-10 | MUST | fires when candidate differs from raw but equals display | 7 |
| SLD-EVT-11 | MUST | `activeThumbIndex` semantics (post-swap index) | 7 |
| SLD-EVT-12 | MUST | canceled later move → commit last applied | 7 |
| SLD-FRM-01 | MUST | FormData: one entry per thumb under the name | 8 |
| SLD-FRM-02 | MUST | field registration contents | 8 |
| SLD-FRM-03 | MUST | form values projection = `fieldValue` | 8 |
| SLD-FRM-04 | MUST | per-change pipeline: clearErrors → validation.change → setDirty | 8 |
| SLD-FRM-05 | MUST | touched on input/keyboard attempts and outside blur | 8 |
| SLD-FRM-06 | MUST | onBlur commit with thumb value; not to sibling | 8 |
| SLD-FRM-07 | MUST | validity attributes on inputs, root, all parts | 8 |
| SLD-FRM-08 | MUST | Field disabled/name propagation | 8 |
| SLD-FRM-09 | MUST | no native constraints; `validate` only | 8 |
| SLD-FRM-10 | MUST | `form` attribute on every input | 8 |
| SLD-FRM-11 | MUST | no reset handling | 8 |
| SLD-FRM-12 | MUST | Field.Label association single vs range | 8 |
| SLD-FRM-13 | MUST | `validate` receives `fieldValue` | 8 |
| SLD-FRM-14 | MUST | external errors shown and cleared on change | 8 |
| SLD-FRM-15 | MUST | field flags on every part | 8 |
| SLD-FRM-16 | MUST | canceled change skips pipeline; touched still set | 8 |
| SLD-ANI-01 | MUST | inset position algorithm and timing (layout, microtask, resize) | 9 |
| SLD-ANI-02 | MUST | hidden until inset position known | 9 |
| SLD-ANI-03 | MUST | edge mode server rendering + script execution | 9 |
| SLD-ANI-04 | MUST | edge-client-only: no script, hidden until client computes | 9 |
| SLD-ANI-05 | MUST | center mode SSR positions immediately | 9 |
| SLD-ANI-06 | MUST | SSR `for`/`data-index` resolve after hydration | 9 |
| SLD-ANI-07 | MUST | pre-hydration script algorithm | 9 |
| SLD-EDGE-01 | MUST | missing-root error message | 12 |
| SLD-EDGE-02 | MUST | `min >= max` warning | 12 |
| SLD-EDGE-03 | SHOULD | shared controlled-state diagnostics | 12 |
| SLD-EDGE-04 | MUST | out-of-range raw value normalized for display | 12 |
| SLD-EDGE-05 | MUST | thumb-only disabled: no `data-disabled` on parts | 12 |
| SLD-EDGE-06 | MUST | NaN never applied | 12 |
| SLD-EDGE-07 | MUST | unstable substitute element reference settles (no update loop) | 12 |
| SLD-EDGE-08 | MUST | dropped Control handle degrades safely | 12 |
| SLD-EDGE-09 | MUST | cloning never uses `window.event` | 12 |
| SLD-EDGE-10 | MUST | shadow DOM support | 12 |
| SLD-EDGE-11 | MUST | thumb/value count mismatch | 12 |
| SLD-EDGE-12 | MUST | count change mid-gesture | 12 |
| SLD-EDGE-13 | MUST | direction only from DirectionProvider | 12 |
| SLD-EDGE-14 | SHOULD | `index` must match document order | 12 |
| SLD-EDGE-15 | MUST | disable mid-gesture drops; re-enable works | 12 |
| SLD-EDGE-16 | MUST | no `-webkit-appearance: slider-vertical` | 12 |
| SLD-EDGE-17 | MUST | re-format on `format`/`locale` change | 12 |
| SLD-I18N-01 | MUST | RTL effects (pointer axis, keys, translate) | 12 |
| SLD-I18N-02 | MUST | `Intl.NumberFormat` semantics | 12 |
| SLD-I18N-03 | MUST | vertical `writing-mode` on the input | 12 |

## 16. Open questions

1. **Dirty written after validation (flagged by the FFI author; verified).** `slider/root/SliderRoot.tsx:178-191` runs `clearErrors` → `validation.change(fieldValue)` → `setDirty(...)`, whereas other controls set dirty before notifying validation (FFI-EVT-02). In `'onChange'` mode the synchronous part of the first commit therefore sees a `false` dirty latch. Range inputs carry no native constraints, so no observable difference is known and no test covers it; SLD-FRM-04/SLD-STA-21 document the actual order. A port may normalize to the common order if FFI decides to mandate it. Resolved (second pass 2026-10-18): FFI-EVT-02 now names the Slider deviation (clear errors → validate → dirty) and allows either order for Slider because range inputs never report a missing value; not a Base UI inconsistency to port around.
2. **`filled` never written (flagged by the FFI author; verified).** No `setFilled` call exists under `slider/`, so `data-filled` never appears for a slider-only field although the mapping would emit it (SLD-STA-22, SLD-DOM-02). The API outline omits `data-filled` for every slider part, consistent with the implementation. Decide whether the port should mirror this or define `filled` (e.g. "value differs from `min`").
3. **`'none'` reason declared but never emitted.** `SliderRoot.tsx:549,585-590,596-601` and the JSDoc describe `'none'` for both callbacks; no slider emitter uses it (EVD §10 Q7). SLD-EVT-02 states it is never emitted. The `lastChangeReason` record starts as `'none'` but a commit cannot happen before an applied change, so it is unobservable.
4. **`pointerId === 0` is never captured.** `SliderControl.tsx:542-544` guards `setPointerCapture` with a truthiness test on `pointerId`; Firefox reports `0` for the mouse pointer, so mouse drags there run without capture (the document listeners still track the drag). Documented as implemented in SLD-PTR-06; a port should probably capture whenever `pointerId` is defined.
5. **`dragging` asymmetry between pointer and touch paths.** A `pointerdown`-started gesture is `dragging` immediately (`SliderControl.tsx:535`), so a plain mouse click shows `data-dragging` between press and release, while a `touchstart`-only gesture waits for the third move (`:343-345`). Tests cover only the touch-only path for the "click modality" case ("should not apply data-dragging for click modality" uses touch events). Documented in SLD-STA-08/PTR-08/PTR-14; confirm the intended semantics.
6. **Stale grab offset after a gesture dropped by disabling.** `stopListening` (`SliderControl.tsx:445-457`) clears the touch id, press snapshot and interaction value but not `pressedThumbIndexRef`/`pressedThumbCenterOffsetRef` (those are cleared only in `handleTouchEnd`/`resetPressedThumb`). If a thumb was pressed when the slider was disabled, the next Control press after re-enabling reuses the stale offset and skips the `track-press` value because `pressedOnAnyThumb` reads true (`:537-540`). Not covered by tests (the existing re-enable test presses the track, not a thumb). Likely a bug; the port should clear both on cleanup.
7. **`index` prop vs registry index.** `SliderThumb.tsx:189-193` registers thumbs without passing `index`, so the Control's element list is document-ordered while values are addressed by the prop. A consumer that renders `<Thumb index={1}/>` before `<Thumb index={0}/>` gets mismatched presses. Recorded as SLD-EDGE-14 (SHOULD); decide whether the port should register with the explicit index instead.
8. **Thumb-only `disabled` is invisible in state attributes.** Because every part renders the root's state (`SliderThumb.tsx:463`), a thumb disabled through its own prop has no `data-disabled` (SLD-EDGE-05). Possibly unintended; the docs page says nothing about per-thumb styling of disabled state.
9. **API outline discrepancies** (`docs/src/app/(docs)/react/components/page.mdx:1693-1713`): Root omits `id` and `aria-labelledby` (both consumed, SLD-API-13/14); Thumb omits `aria-label`, `aria-labelledby`, `aria-describedby`, `children`, `id` (all handled specially, SLD-API-18/20/24/25); Value omits `aria-live` (SLD-API-26); Label lists no data attributes although it renders the full set (SLD-DOM-02); no part lists `data-filled` (consistent with Q2); no CSS variables are listed anywhere although `--position`, `--start-position`, `--relative-size` are set in the inset modes (SLD-DOM-06/10) and `--end-position` is set only by the pre-hydration script (SLD-ANI-07) and never by the component.
10. **Orchestrator hints that do not exist in v1.9.0:** `readOnly` (no such prop; no `aria-readonly`), `inputRef` on Root (it lives on Thumb since v1.0.0-beta.4), `data-active` (no such attribute; the active thumb is only visible through `z-index`), `--slider-*` CSS variables (the variables are unprefixed, see Q9), and `role="slider"` on the thumb element (the role is the input's native one).
11. **Duplicate Label ids.** Every `Slider.Label` under one Root receives the same id `${rootId}-label` (`SliderLabel.tsx:26-28,48-50`); two Labels would produce duplicate ids. Not guarded; documented in §2 prose only.
12. **Inset mount microtask.** `SliderThumb.tsx:234-238` schedules an extra `queueMicrotask(getInsetPosition)` on mount in addition to the synchronous layout computation at `:240-244`; the reason (presumably the Control's handle not yet attached when a thumb's layout effect runs first in some trees) is not stated. SLD-ANI-01 requires both; a port that computes once after all handles attach would be observably equivalent. Partly resolved: decisions/04-js-interop-boundary.md §2 makes `--position` and the Indicator variables JavaScript-owned, and decisions/01-hosting-model-and-timing.md §Decision item 3 (C2) allows microtasks only in JavaScript, so the `slider` module can schedule the microtask as written; the reason for the extra pass remains unstated.
13. **`--end-position` on the Indicator.** The pre-hydration script sets `--end-position` (`prehydrationScript.template.js:62`) but the component never sets or reads it; the client's indicator styles use `--start-position`/`--relative-size` only. Harmless leftover; not required by SLD-DOM-06.
14. **Pre-hydration script under the port's prerender model.** SLD-STR-06, SLD-DOM-20, SLD-ANI-03 and SLD-ANI-07 require an inline `<script>` (`slider/thumb/SliderThumb.tsx:473-475`, `slider/thumb/prehydrationScript.template.js:1-69`) that executes from the server HTML before the client takes over. decisions/01-hosting-model-and-timing.md §Consequences puts prerender "out of scope for behaviour but in scope for markup", while decisions/06-testing-harness.md §Decision schedules a dedicated prerendering fixture for SLD-ANI-03. Neither ADR says whether the port emits and executes this script (behaviour inside prerendered markup) or withdraws it and treats `'edge'` as `'edge-client-only'`. Needs a decision. Decided (2026-10-18): prerender renders the initial inline values once (ADR 04 Amendments, handover at attach); the inline pre-hydration script is replaced by that initial render plus the `slider` module on attach.
15. **One implementation of the step arithmetic (C1).** `roundValueToStep` is called by the keyboard path (`slider/thumb/SliderThumb.tsx:384`) and by the drag path (`slider/control/SliderControl.tsx:201`); `getSliderValue`/`validateMinimumDistance` by the keyboard and native-input path (`slider/root/SliderRoot.tsx:230-232`) and `validateMinimumDistance` by the drag path (`slider/control/SliderControl.tsx:342`). decisions/01-hosting-model-and-timing.md §Decision item 2 forbids two implementations (it names the old port's duplicated `roundValueToStep`), and decisions/04-js-interop-boundary.md §1 assigns all SLD ALG sections to the `slider` module. Not yet decided: whether the thumb input's `keydown` (SLD-KEY-01..13) is handled in that JavaScript module, or C# handles the key and obtains the candidate from the module (an interop call per key press). Decided (2026-10-18): thumb keydown moves into the JS `slider` module next to the drag loop; one implementation (ADR 04 Amendments, module placement rule).
16. **Ownership of centre-mode position styles.** decisions/04-js-interop-boundary.md §2 lists `--position`, `--start-position`, `--end-position`, `--relative-size` and inline `visibility` as JavaScript-owned on `Slider.Thumb`/`Slider.Indicator`, and says C# renders no `style` on an element with JS-owned style properties. In centre mode the same elements carry `insetInlineStart`/`bottom` percentages, `translate`, `z-index`, `position` and the Indicator's `width`/`height` (`slider/thumb/SliderThumb.tsx:287-305`, `slider/indicator/SliderIndicator.tsx:11-45`), plus the consumer's `style`, none of which is in the table. Decide whether those are JS-owned too (the whole `style` attribute of Thumb and Indicator) so a single writer holds. Decided (2026-10-18): the whole `style` of Thumb and Indicator is JS-owned after attach, C# renders the initial value (ADR 04 Amendments).
