# Meter

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/meter/` |
| Tests | `packages/react/src/meter/**/*.test.tsx` (5 files) |
| Docs page | `docs/src/app/(docs)/react/components/meter/page.mdx` |
| Prefix | `MTR` |
| Status | implemented |
| Revised | 2026-10-21 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) (label-id publication only), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Meter is a read-only graphical display of a scalar measurement within a known range (battery level, disk usage, a score), the WAI-ARIA `meter` role. The consumer supplies a numeric `value` and optionally `min`/`max`; the component computes where that value sits in the range as a percentage, exposes the value to assistive technology through `aria-valuenow`/`aria-valuemin`/`aria-valuemax`/`aria-valuetext`, sizes an indicator element to that percentage, and renders a locale-formatted text version of the value.

Meter is purely presentational: it has no user interaction, no focus behavior, no callbacks with event details, no form participation and no internal state other than the id of its label. The value is always owned by the consumer (there is no `defaultValue`). It differs from Progress ([progress](progress.md)) in three ways: the role is `meter`, there is no indeterminate state (`value` is a required `number`), and no part emits state data attributes (its state object is empty).

No component builds on Meter, and Meter builds on no other component.

## 2. Anatomy

```
<Meter.Root>                 required, 1          default element: div   (+ trailing visually hidden <span>)
  <Meter.Label>              optional, 0..1       default element: span
  <Meter.Track>              optional, 0..n       default element: div
    <Meter.Indicator>        optional, 0..n       default element: div
  <Meter.Value>              optional, 0..n       default element: span
```

The order of Label, Track and Value inside Root is free; the docs anatomy places Label first, then Track (containing Indicator), then Value. Nothing enforces that Indicator is inside Track.

- **MTR-STR-01** — MUST. `Meter.Root` MUST render one host element (default `<div>`) whose children are the consumer's children followed by exactly one additional trailing `<span>` described in MTR-DOM-01.
  Source: `meter/root/MeterRoot.tsx:60-67,80-83`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes" (`getByRole('meter')`).
- **MTR-STR-02** — MUST. `Meter.Indicator`, `Meter.Value` and `Meter.Label` MUST be rendered inside the subtree of a `Meter.Root`; rendering any of them elsewhere MUST throw the error in MTR-EDGE-01.
  Source: `meter/root/MeterRootContext.ts:16-25`, `meter/indicator/MeterIndicator.tsx:20`, `meter/value/MeterValue.tsx:20`, `meter/label/MeterLabel.tsx:21`. Test: [L2] `MeterLabel.test.tsx` "throws a descriptive error when rendered outside <Meter.Root>".
  Port note 2026-10-21 (MTR-EDGE-01): the error is the port's own message, see MTR-EDGE-01.
- **MTR-STR-03** — MUST. `Meter.Track` MUST NOT depend on an enclosing `Meter.Root`: it reads no shared state and renders without error anywhere.
  Source: `meter/track/MeterTrack.tsx:13-23` (no context read). Test: none.
- **MTR-STR-04** — MUST. `Meter.Track` is structural only: it MUST render an element carrying only the consumer's attributes (no built-in attributes, styles, handlers or data attributes).
  Source: `meter/track/MeterTrack.tsx:17-22`. Test: [L2] `MeterTrack.test.tsx` conformance suite "Base UI component API".

All parts other than Root may be omitted; Root alone still exposes the full ARIA value information (MTR-ARIA-01…05). Omitting Label leaves the meter without `aria-labelledby` (the consumer may use `aria-label` instead, as the repo's battery experiment does).

## 3. Public API

### 3.1 `Meter.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state is `{}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `number` | — (required) | The current value. Always consumer-owned. | MTR-API-01 |
| `min` | `number` | `0` | Lower bound of the range. | MTR-API-02 |
| `max` | `number` | `100` | Upper bound of the range. | MTR-API-03 |
| `format` | `Intl.NumberFormatOptions` | — | Number-format options. When given, the *clamped value* is formatted with them; when absent, the *percentage* is formatted as a percent. | MTR-API-04 |
| `locale` | `Intl.LocalesArgument` | — (runtime default locale) | Locale used for all formatting. | MTR-API-05 |
| `getAriaValueText` | `(formattedValue: string, value: number) => string` | — | Produces the `aria-valuetext` string. | MTR-API-06 |
| `aria-valuetext` | `string` | — | Forwarded attribute; overrides the computed `aria-valuetext`. Listed in the API outline. | MTR-API-07 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the empty state object. | MTR-API-08 |
| `children` | node | — | Rendered before the hidden trailing span. | MTR-STR-01 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element. | MTR-API-09 |

**Consumed (not forwarded to the element):** `value`, `min`, `max`, `format`, `locale`, `getAriaValueText`, `className`, `style`, `render`, `children` (re-emitted inside the root, see MTR-STR-01).

- **MTR-API-01** — MUST. `value` is required and is never stored: every render MUST derive all outputs (percentage, clamped value, formatted text, ARIA attributes, indicator width) from the `value`, `min`, `max`, `format` and `locale` passed in that render, so a change of any of them is reflected by every part in the same render.
  Source: `meter/root/MeterRoot.tsx:38-51,70-78`. Test: [L2] `MeterRoot.test.tsx` "refreshes aria-valuenow, aria-valuetext, the value text, and the indicator when value changes", "keeps range attributes, formatted text, and the indicator synchronized on rerender".
- **MTR-API-02** — MUST. `min` MUST default to `0` when omitted or `undefined`.
  Source: `meter/root/MeterRoot.tsx:26`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes" (`aria-valuemin="0"`).
- **MTR-API-03** — MUST. `max` MUST default to `100` when omitted or `undefined`.
  Source: `meter/root/MeterRoot.tsx:25`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes" (`aria-valuemax="100"`).
- **MTR-API-04** — MUST. When `format` is provided (any object, including an empty one), the formatted value MUST be the clamped value (MTR-ALG-02) formatted with `format` and `locale`; when `format` is absent it MUST be the percentage (MTR-ALG-01) divided by 100, formatted with `{ style: 'percent' }` and `locale` (see MTR-I18N-01, MTR-I18N-02).
  Source: `meter/root/MeterRoot.tsx:44-46`. Test: [L2] `MeterRoot.test.tsx` "formats the value" (format), "sets the correct aria attributes" (no format).
- **MTR-API-05** — MUST. `locale` MUST be passed unchanged to the number formatter; when `undefined`, the runtime's default locale applies.
  Source: `meter/root/MeterRoot.tsx:44-46`, `packages/utils/src/formatNumber.ts:19-28`. Test: [L2] `MeterRoot.test.tsx` "sets the locale when formatting the value".
- **MTR-API-06** — MUST. When `getAriaValueText` is provided, the root's computed `aria-valuetext` MUST be its return value, called with `(formattedValue, value)` where `formattedValue` is the string of MTR-API-04 and `value` is the raw, unclamped `value` prop (including `NaN` or out-of-range numbers).
  Source: `meter/root/MeterRoot.tsx:48-51,102`. Test: [L2] `MeterRoot.test.tsx` "uses the returned text and receives the formatted and raw value", "formats the clamped value while clamping range attributes and indicator width".
- **MTR-API-07** — MUST. A consumer-supplied `aria-valuetext` attribute MUST replace the computed `aria-valuetext` (whether default or from `getAriaValueText`); it is forwarded like any other attribute and merged after the built-in attributes.
  Source: `meter/root/MeterRoot.tsx:82,94`, `merge-props/mergeProps.ts:162-184` (RND-DOM-19). Test: none.
- **MTR-API-08** — MUST. `className`, `style` and `render` MUST behave per RND for every Meter part; the state object they receive is the empty object (MTR-STA-01).
  Source: `meter/root/MeterRoot.tsx:28-31,80-83`, `internals/useRenderElement.tsx:45,67` (RND-STA-02). Test: [L2] every `Meter*.test.tsx` conformance suite.
- **MTR-API-09** — MUST. Every Root prop other than the consumed list MUST be forwarded to the root element and merged after the built-in attributes, so a consumer value for the same attribute (`role`, `aria-valuenow`, `aria-valuemin`, `aria-valuemax`, `aria-labelledby`, `aria-valuetext`) wins (RND-DOM-19).
  Source: `meter/root/MeterRoot.tsx:21-33,82`. Test: [L2] `MeterRoot.test.tsx` conformance suite (forwarded `data-*`/`lang`).

#### State object

| Field | Type | Meaning |
|---|---|---|
| — | — | The state object is empty (`{}`). |

- **MTR-STA-01** — MUST. Every Meter part (Root, Track, Indicator, Value, Label) MUST expose the empty object `{}` as its state to `className`/`style`/`render` functions.
  Source: `meter/root/MeterRoot.tsx:88`, `meter/track/MeterTrack.tsx:25`, `meter/indicator/MeterIndicator.tsx:37`, `meter/value/MeterValue.tsx:34`, `meter/label/MeterLabel.tsx:37`, `internals/useRenderElement.tsx:45,67`. Test: none (conformance suites call class functions without asserting state).

#### Data attributes

None. No Meter part emits `data-*` attributes of its own (MTR-DOM-02).

#### CSS variables

None.

### 3.2 `Meter.Track`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive `{}`. | MTR-API-08 |
| *other `<div>` attributes* | — | — | Forwarded. | MTR-API-13 |

State object, data attributes, CSS variables: as §3.1 (empty / none).

### 3.3 `Meter.Indicator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive `{}`. A consumer `style` overrides the built-in sizing keys (MTR-DOM-04). | MTR-API-08 |
| *other `<div>` attributes* | — | — | Forwarded. | MTR-API-13 |

State object, data attributes: as §3.1. Inline style: MTR-DOM-03. CSS variables: none.

### 3.4 `Meter.Value`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `null \| (formattedValue: string, value: number) => node` | — | Optional function producing the element's content. | MTR-API-10, MTR-API-11 |
| `className`, `style`, `render` | see RND | — | Functions receive `{}`. | MTR-API-08 |
| *other `<span>` attributes* | — | — | Forwarded. | MTR-API-13 |

- **MTR-API-10** — MUST. When `children` is a function, the Value element's content MUST be the function's return value, called with `(formattedValue, value)`: `formattedValue` is the string of MTR-API-04 (the same string the root uses for its default `aria-valuetext`) and `value` is the raw, unclamped `value` prop.
  Source: `meter/value/MeterValue.tsx:20,27`, `meter/root/MeterRoot.tsx:70-76`. Test: [L2] `MeterValue.test.tsx` "accepts a render function", "passes updated arguments to the render function when value changes".
- **MTR-API-11** — MUST. When `children` is not a function (`undefined`, `null`, or any other node), the Value element's content MUST be the formatted value string; non-function children are discarded.
  Source: `meter/value/MeterValue.tsx:18,27`. Test: [L2] `MeterValue.test.tsx` "renders the value when children is not provided", "renders a formatted value when a format is provided".

State object, data attributes, CSS variables: as §3.1.

### 3.5 `Meter.Label`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated `base-ui-…` | Id of the label element; published to Root for `aria-labelledby`. Not listed in the API outline (native attribute). | MTR-API-12 |
| `className`, `style`, `render` | see RND | — | Functions receive `{}`. | MTR-API-08 |
| *other `<span>` attributes* | — | — | Forwarded. | MTR-API-13 |

- **MTR-API-12** — MUST. The Label element's `id` MUST be the consumer's `id` when supplied (used verbatim, RND-DOM-28), otherwise a generated id of the form `base-ui-<token>` (RND-DOM-27); that id MUST be the one published to Root (MTR-ARIA-06).
  Source: `meter/label/MeterLabel.tsx:19,23,29`, `utils/useRegisteredLabelId.ts:10`. Test: [L2] `MeterLabel.test.tsx` "updates and clears the meter label association"; `MeterRoot.test.tsx` "sets the correct aria attributes" (generated id).
- **MTR-API-13** — MUST. For Track, Indicator, Value and Label, every prop other than `className`, `style`, `render` (and, for Value, `children`; for Label, `id`, which is re-emitted per MTR-API-12) MUST be forwarded to the part's element, merged after the built-in attributes.
  Source: `meter/track/MeterTrack.tsx:17-21`, `meter/indicator/MeterIndicator.tsx:18,24-33`, `meter/value/MeterValue.tsx:18,24-30`, `meter/label/MeterLabel.tsx:19,27-33`. Test: [L2] conformance suites of all four parts; `MeterIndicator.test.tsx` `data-testid` queries.

State object, data attributes, CSS variables: as §3.1.

### 3.6 Shared types

- `Meter.Root.State`, `Meter.Track.State`, `Meter.Indicator.State`, `Meter.Value.State`, `Meter.Label.State` — all the empty object type; the four part states extend `Meter.Root.State` with no fields. Source: `meter/root/MeterRoot.tsx:88`, `meter/*/Meter*.tsx` `*State` interfaces.
- `Meter.Root.Props` … `Meter.Label.Props` — the prop tables above. Flat aliases `MeterRootState`, `MeterRootProps`, etc. are also exported. Source: `meter/index.ts:1-7`.
- No `*DataAttributes` namespaces and no `ChangeEventDetails`/reason types exist for Meter.

## 4. State model

Meter holds no value state. The only component-held state is the id of the currently mounted Label (`labelId`, initially "none"). All other quantities are pure functions of the props of the current render:

```
rawPercentage   = ((value − min) × 100) / (max − min)                       (MTR-ALG-01)
percentageValue = clamp(isNaN(rawPercentage) ? 0 : rawPercentage, 0, 100)
clampedValue    = clamp(isNaN(value) ? min : value, min, max)               (MTR-ALG-02)
formattedValue  = format ? fmt(clampedValue, locale, format)
                         : fmt(percentageValue / 100, locale, {style:'percent'})
ariaValueText   = getAriaValueText ? getAriaValueText(formattedValue, value) : formattedValue
```

`clamp(x, lo, hi) = max(lo, min(x, hi))` (UTL-EDGE-14); `fmt` is the shared cached number formatter (UTL-I18N-05, UTL-I18N-06).

- **MTR-ALG-01** — MUST. The percentage MUST be computed as `((value − min) × 100) / (max − min)` (UTL-DOM-31) in IEEE-754 double arithmetic; if the result is `NaN` it MUST be replaced by `0`; the result MUST then be clamped to `[0, 100]` (so `+∞` → `100`, `−∞` → `0`).
  Source: `meter/root/MeterRoot.tsx:37-39`, `utils/valueToPercent.ts:1-3`, `packages/utils/src/clamp.ts:1-7`. Test: [L1] `MeterRoot.test.tsx` "formats the value as its position within a custom range and keeps the indicator in sync", "formats the value relative to a non-zero min"; `MeterIndicator.test.tsx` "clamps the width to 100% when the value exceeds max", "clamps the width to 0% when the value is below min", "produces a finite width when min equals max".
- **MTR-ALG-02** — MUST. The clamped value MUST be `clamp(value, min, max)`, except that a `NaN` value MUST be replaced by `min` before clamping.
  Source: `meter/root/MeterRoot.tsx:40`. Test: [L1] `MeterRoot.test.tsx` "normalizes aria attributes when $label" (all four rows), "formats the clamped value while clamping range attributes and indicator width".
- **MTR-ALG-03** — MUST. Numbers written into attributes and inline styles (`aria-valuenow`, `aria-valuemin`, `aria-valuemax`, the Indicator `width` percentage) are written unformatted, so their serialization is per UTL-DOM-37 (e.g. `100/3`-style results keep full precision such as `"33.333333333333336"`). No rounding is applied to these values; only the formatted text is rounded (by the number formatter).
  Source: `meter/root/MeterRoot.tsx:55-57`, `meter/indicator/MeterIndicator.tsx:29`; see UTL-DOM-37. Test: [L2] `MeterRoot.test.tsx` "formats the value as its position within a custom range and keeps the indicator in sync" (`aria-valuenow="0.5"`).

- **MTR-STA-02** — MUST. Root, Indicator and Value MUST observe the same derived values within one render: the Indicator width, the Value text, the default `aria-valuetext` and `aria-valuenow` MUST never disagree with each other for the current props.
  Source: `meter/root/MeterRoot.tsx:42-46,70-78`. Test: [L2] `MeterRoot.test.tsx` "defaults aria-valuetext to the localized formatted value, matching Meter.Value", "rounds the default aria-valuetext like the displayed value", "refreshes aria-valuenow, aria-valuetext, the value text, and the indicator when value changes". Docs: CHANGELOG v1.6.0 "Sync value text with indicator"; v1.7.0 "Format clamped values".
- **MTR-STA-03** — MUST. Root MUST track at most one label id: the id most recently published by a mounted `Meter.Label`. When a Label unmounts or changes id, the previously published id MUST be withdrawn only if it is still the tracked id (FFI-ARIA-08); the tracked id then becomes "none".
  Source: `meter/root/MeterRoot.tsx:35`, `utils/useRegisteredLabelId.ts:12-17`. Test: [L2] `MeterLabel.test.tsx` "updates and clears the meter label association".

There is no controlled/uncontrolled pair, no normalization of `min`/`max` (they are not reordered or validated), and no mount/unmount lifecycle.

## 5. Rendered DOM and ARIA

### 5.1 Root element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `role` | `"meter"` | MTR-ARIA-01 | C# |
| `aria-valuenow` | clamped value | MTR-ARIA-02 | C# |
| `aria-valuemin` | `min` | MTR-ARIA-03 | C# |
| `aria-valuemax` | `max` | MTR-ARIA-03 | C# |
| `aria-valuetext` | formatted value, or `getAriaValueText(...)` result | MTR-ARIA-04, MTR-ARIA-05 | C# |
| `aria-labelledby` | id of the mounted Label; absent when none | MTR-ARIA-06 | C# |
| `tabindex` | not set | MTR-FOC-01 | C# |
| trailing child | visually hidden `<span role="presentation">x</span>` | MTR-DOM-01 | C# |

- **MTR-DOM-01** — MUST. After the consumer's children, Root MUST render one `<span>` with `role="presentation"`, the inline style `visuallyHidden` (UTL-DOM-13: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0`) and the single text character `x`. The span exists so that the NVDA screen reader announces the label (Base UI issue 4184).
  Source: `meter/root/MeterRoot.tsx:60-67`, `packages/utils/src/visuallyHidden.ts:3-20`. Test: none. Docs: CHANGELOG v1.3.0 "Fix label announcements in NVDA".
- **MTR-DOM-02** — MUST. No Meter part MUST emit any `data-*` attribute other than those the consumer passes.
  Source: `meter/root/MeterRoot.tsx:80-83` (no `state`/mapping passed), `meter/*/Meter*.tsx` likewise. Test: none.
- **MTR-DOM-08** — MUST. Consumer attributes MUST take precedence over built-in attributes of the same name on every part (e.g. `role`, `aria-*`, Value's `aria-hidden`); `style` merges per RND-DOM-16 and `className` per RND-DOM-14. (On Label the consumer `id` is already the built-in id, MTR-API-12.)
  Source: `meter/root/MeterRoot.tsx:82`, `meter/indicator/MeterIndicator.tsx:24-33`, `meter/value/MeterValue.tsx:24-30`, `meter/label/MeterLabel.tsx:27-33`. Test: none.

### 5.2 Indicator element

| Inline style declaration | Value | Req | Writer |
|---|---|---|---|
| `inset-inline-start` | `0` | MTR-DOM-03 | C# |
| `height` | `inherit` | MTR-DOM-03 | C# |
| `width` | `<percentage>%` | MTR-DOM-03 | C# |

- **MTR-DOM-03** — MUST. The Indicator element MUST carry the inline style declarations `inset-inline-start: 0`, `height: inherit` and `width: <P>%`, where `<P>` is the percentage of MTR-ALG-01 serialized per MTR-ALG-03. The declarations MUST be present for every value (there is no state in which they are omitted).
  Source: `meter/indicator/MeterIndicator.tsx:20-30`. Test: [L2] `MeterIndicator.test.tsx` "clamps the width to 100% when the value exceeds max", "clamps the width to 0% when the value is below min", "produces a finite width when min equals max", "sets positioning styles" (browser: `left: 0px`, `width: 33px` in a 100 px root), "sets zero width when value is 0" (browser: `inset-inline-start: 0px`, `width: 0px`); `MeterRoot.test.tsx` "refreshes aria-valuenow, aria-valuetext, the value text, and the indicator when value changes". (L4 for the computed-pixel browser citations)
- **MTR-DOM-04** — MUST. A consumer `style` on the Indicator MUST override the built-in declarations key by key (e.g. a consumer `width` replaces the computed width), per RND-DOM-16.
  Source: `meter/indicator/MeterIndicator.tsx:18,26-30`, `internals/useRenderElement.tsx:115-117`. Test: none.

The Indicator is sized only by `width`; it is not positioned by the component (no `position` declaration), so `inset-inline-start` has an effect only if the consumer positions the element. The width percentage resolves against the Indicator's containing block (normally the Track).

### 5.3 Value element

- **MTR-DOM-05** — MUST. The Value element's text content MUST be the formatted value (MTR-API-04) unless a children function is supplied (MTR-API-10), and MUST equal the root's default `aria-valuetext` when no `getAriaValueText` is given; `getAriaValueText` MUST NOT affect the Value text.
  Source: `meter/value/MeterValue.tsx:27`, `meter/root/MeterRoot.tsx:48-51`. Test: [L2] `MeterRoot.test.tsx` "defaults aria-valuetext to the localized formatted value, matching Meter.Value", "uses the returned text and receives the formatted and raw value".
- **MTR-DOM-06** — MUST. The Value element MUST carry `aria-hidden="true"` (the value is announced through the root's `aria-valuetext` instead) unless the consumer overrides it.
  Source: `meter/value/MeterValue.tsx:26`. Test: none.

### 5.4 Label element

- **MTR-DOM-07** — MUST. The Label element MUST carry the id of MTR-API-12 and `role="presentation"`.
  Source: `meter/label/MeterLabel.tsx:27-31`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes" (label has an id).

### 5.5 ARIA

- **MTR-ARIA-01** — MUST. The root element MUST have `role="meter"` unless the consumer supplies a `role`.
  Source: `meter/root/MeterRoot.tsx:59`. Test: [L2] `MeterRoot.test.tsx` every `getByRole('meter')` query.
- **MTR-ARIA-02** — MUST. `aria-valuenow` MUST be the clamped value (MTR-ALG-02), always present.
  Source: `meter/root/MeterRoot.tsx:57`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes", "normalizes aria attributes when $label", "formats the clamped value while clamping range attributes and indicator width".
- **MTR-ARIA-03** — MUST. `aria-valuemin` MUST be the effective `min` and `aria-valuemax` the effective `max` (defaults applied, no validation or reordering), always present.
  Source: `meter/root/MeterRoot.tsx:55-56`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes", "keeps range attributes, formatted text, and the indicator synchronized on rerender".
- **MTR-ARIA-04** — MUST. Without `getAriaValueText`, `aria-valuetext` MUST be the formatted value (MTR-API-04); with the default formatting this is the locale-formatted, rounded percentage (e.g. value `30` in `de-DE` → `"30 %"` with a no-break space before `%`; `33.333` → `"33%"` in `en-US`).
  Source: `meter/root/MeterRoot.tsx:48,58`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes", "defaults aria-valuetext to the localized formatted value, matching Meter.Value", "rounds the default aria-valuetext like the displayed value".
- **MTR-ARIA-05** — MUST. With `getAriaValueText`, `aria-valuetext` MUST be its return value (MTR-API-06).
  Source: `meter/root/MeterRoot.tsx:49-51,58`. Test: [L2] `MeterRoot.test.tsx` "uses the returned text and receives the formatted and raw value".
- **MTR-ARIA-06** — MUST. `aria-labelledby` MUST equal the id tracked per MTR-STA-03 and MUST be absent while no Label is mounted. A consumer `aria-labelledby` replaces it (MTR-API-09).
  Source: `meter/root/MeterRoot.tsx:35,54`, `meter/label/MeterLabel.tsx:21-23`. Test: [L2] `MeterRoot.test.tsx` "sets the correct aria attributes"; `MeterLabel.test.tsx` "updates and clears the meter label association".
- **MTR-ARIA-07** — MUST. When a Label mounts, changes `id`, or unmounts, the root's `aria-labelledby` MUST be updated post-commit, pre-paint (one extra update of the root after the Label's DOM change, before the browser paints). Server: relaxed, see §9.
  Source: `utils/useRegisteredLabelId.ts:12-17`. Test: [L2] `MeterLabel.test.tsx` "updates and clears the meter label association".
  Port note 2026-10-21 (§14 allowance; ADR 01 relaxation table, "FFI registration" row): the label publishes its id while its parameters are set, which is inside the render batch that creates, re-ids or removes it, and the root renders again in that batch. On Server the root's `aria-labelledby` therefore lands with the label itself, not after paint plus a round trip; WebAssembly is the same. Tested on both render modes at L4 (`meter.spec.ts` "updates and clears the meter label association"). Removal and id changes follow MTR-STA-03.

Meter has no Field integration: it does not read `Field.Label`, `Field.Description` or field validity, and emits no `aria-describedby`/`aria-invalid`.

## 6. Interactions

### 6.1 Keyboard

Not applicable — no part attaches keyboard handlers; all keys behave natively (Tab skips the meter, MTR-FOC-01).

### 6.2 Pointer and touch

Not applicable — no part attaches pointer handlers.

### 6.3 Focus management

- **MTR-FOC-01** — MUST. No Meter part MUST add a `tabindex` or otherwise make itself focusable; the meter is not a tab stop.
  Source: `meter/root/MeterRoot.tsx:53-68` (no `tabIndex`). Test: none.

### 6.4 Dismissal

Not applicable — no dismissable surface.

### 6.5 Reading direction

- **MTR-I18N-04** — MUST. Meter MUST NOT read the reading direction; direction affects only the Indicator's logical `inset-inline-start` declaration, which the browser resolves (to the left edge in LTR, the right edge in RTL, when the Indicator is positioned). `width` is a physical property and is not mirrored or swapped for vertical writing modes.
  Source: `meter/indicator/MeterIndicator.tsx:26-30` (no direction import anywhere in `meter/`). Test: none.

## 7. Events and callbacks

Meter has no change callbacks and no event details. Two consumer functions are invoked during rendering:

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `getAriaValueText` (Root) | `(formattedValue: string, value: number) => string` | every render of Root | — | no |
| `children` function (Value) | `(formattedValue: string, value: number) => node` | every render of Value | — | no |

- **MTR-EVT-01** — MUST. `getAriaValueText` MUST be called synchronously on every render of the Root part (no memoization; call count checked against the part's render count observed through bUnit, L2), with the arguments of MTR-API-06 for that render; its result is used only for `aria-valuetext`.
  Source: `meter/root/MeterRoot.tsx:48-51`. Test: [L2] `MeterRoot.test.tsx` "uses the returned text and receives the formatted and raw value" (`toHaveBeenCalledWith`), "formats the clamped value while clamping range attributes and indicator width" (`toHaveBeenLastCalledWith`).
- **MTR-EVT-02** — MUST. A Value `children` function MUST be called synchronously on every render of the Value part (render count observed through bUnit, L2) with the arguments of MTR-API-10 for the current props; after the root's `value` changes, the most recent call MUST have the new arguments.
  Source: `meter/value/MeterValue.tsx:27`. Test: [L2] `MeterValue.test.tsx` "passes updated arguments to the render function when value changes".

## 8. Form integration

Not applicable — Meter renders no input, registers with no Field and submits nothing.

## 9. Lifecycle, mounting and animation

Not applicable — all parts are always mounted; there is no transition status. Animating the Indicator (the docs demo uses `transition: width 500ms`) is the consumer's concern.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- MTR-I18N-01 — the percent formatting uses .NET globalization instead of `Intl.NumberFormat`; percent spacing may differ (a port relaxation on both render modes, not a timing row: ADR 01 Decision 2 as amended; output equivalent, not byte-identical).
- MTR-I18N-02 — `format` options are applied by a .NET formatter; currency patterns may differ from `Intl.NumberFormat`'s (port relaxation, both modes; ADR 01 Decision 2).
- MTR-I18N-03 — `locale` selects a .NET culture; with no `locale` the .NET current culture applies, and the formatter cache is keyed on the culture and options (port relaxation, both modes; ADR 01 Decision 2).
- MTR-ARIA-07 — FFI registration row (post-commit, pre-paint C# state work): on Server the root's `aria-labelledby` update after a Label mounts, changes id or unmounts lands after paint plus a round trip.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (all parts) | [RND](../architecture/rendering-and-composition.md) | Root `div`, Track `div`, Indicator `div`, Value `span`, Label `span`; no `state` or attribute mapping passed (state `{}`, RND-STA-02); built-in props merged first, consumer props second |
| `formatNumber` | [UTL](../architecture/shared-utilities.md) UTL-I18N-05…07 | `(clampedValue, locale, format)` or `(percentageValue / 100, locale, { style: 'percent' })` |
| `valueToPercent` | UTL UTL-DOM-31 | `(value, min, max)` |
| `clamp` | UTL UTL-EDGE-14 | percentage to `[0, 100]`; value to `[min, max]` |
| `visuallyHidden` | UTL UTL-DOM-13 | style of the trailing NVDA span |
| `useRegisteredLabelId` / `useBaseUiId` | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ARIA-08, RND-DOM-27 | Label publishes its id to Root's label-id slot |

## 12. Edge cases, invariants and dev warnings

Meter emits no warnings and no dev-only diagnostics of its own.

- **MTR-EDGE-01** — MUST. Rendering `Meter.Indicator`, `Meter.Value` or `Meter.Label` outside a `Meter.Root` MUST throw an error with the message `Base UI: MeterRootContext is missing. Meter parts must be placed within <Meter.Root>.` (thrown in all builds).
  Source: `meter/root/MeterRootContext.ts:16-25`. Test: [L2] `MeterLabel.test.tsx` "throws a descriptive error when rendered outside <Meter.Root>".
  Port note 2026-10-21: the message is ``Blazix: The `MeterRoot` context is missing. Meter parts must be placed within `<MeterRoot>`.`` (the port's words under ADR 08 Amendments 2026-10-20: it starts `Blazix: ` and quotes code in backticks); the error is an `InvalidOperationException` thrown in every build.
- **MTR-EDGE-02** — MUST. With `value = NaN`: `aria-valuenow` MUST be `min`, the percentage `0`, the Indicator width `0%`, and the default formatted value the formatted `0` percent (or `min` formatted with `format`).
  Source: `meter/root/MeterRoot.tsx:38-40`. Test: [L2] `MeterRoot.test.tsx` "normalizes aria attributes when value is NaN".
- **MTR-EDGE-03** — MUST. With `min === max`: when `value === min` the percentage MUST be `0` (the `0/0` NaN case) and `aria-valuenow` MUST be `min`; when `value` differs, the percentage follows MTR-ALG-01 (`±∞` clamped to `100`/`0`) and `aria-valuenow` is `min` (= `max`).
  Source: `meter/root/MeterRoot.tsx:37-40`. Test: [L2] `MeterRoot.test.tsx` "normalizes aria attributes when min equals max"; `MeterIndicator.test.tsx` "produces a finite width when min equals max".
- **MTR-EDGE-04** — MUST. With `value > max` the percentage MUST be `100`, `aria-valuenow` `max`, and the formatted value derived from the clamped value; with `value < min` the percentage MUST be `0` and `aria-valuenow` `min`. `getAriaValueText` and a Value children function still receive the raw value.
  Source: `meter/root/MeterRoot.tsx:39-40,50`. Test: [L2] `MeterRoot.test.tsx` "normalizes aria attributes when value exceeds max", "normalizes aria attributes when value is below min", "formats the clamped value while clamping range attributes and indicator width".
- **MTR-EDGE-05** — MUST. `min > max` is not validated: outputs MUST follow MTR-ALG-01/02 literally (e.g. `min=100, max=0, value=30` gives percentage `70` and `aria-valuenow="100"`, because `clamp` returns its lower bound when bounds are inverted).
  Source: `meter/root/MeterRoot.tsx:38-40`, `packages/utils/src/clamp.ts:6`. Test: none.
- **MTR-EDGE-06** — MUST. Server-rendered markup MUST NOT contain the root's `aria-labelledby` (the label id is published only after the client mounts, MTR-ARIA-07); every other attribute, including the Label's generated id, MUST match the client's first render.
  Source: `utils/useRegisteredLabelId.ts:12` (post-commit publication), `meter/root/MeterRoot.tsx:35,54`. Test: none.
  Port note 2026-10-21 (§14 allowance, see MTR-ARIA-07): the label registers during the render that creates it, so the server-rendered markup of a prerendered page already carries the root's `aria-labelledby`, and the Label's id matches the first interactive render. This is the "acceptable improvement" of §14; tested at L4 (`meter.spec.ts` "carries aria-labelledby in the server-rendered markup", both render modes) and at L2 ("renders aria-labelledby in the first markup of the root").

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND.

## 13. Behaviors evidenced by tests

### `MeterRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; `testRenderPropWith` default `div`; `button` not set) | MTR-DOM-09, MTR-API-08, MTR-API-09 |
| "sets the correct aria attributes" | MTR-ARIA-01, MTR-ARIA-02, MTR-ARIA-03, MTR-ARIA-04, MTR-ARIA-06, MTR-API-02, MTR-API-03, MTR-DOM-07 |
| "defaults aria-valuetext to the localized formatted value, matching Meter.Value" | MTR-I18N-01, MTR-ARIA-04, MTR-DOM-05, MTR-STA-02 |
| "rounds the default aria-valuetext like the displayed value" | MTR-I18N-01, MTR-ARIA-04, MTR-STA-02 |
| "refreshes aria-valuenow, aria-valuetext, the value text, and the indicator when value changes" | MTR-API-01, MTR-STA-02, MTR-DOM-03 |
| "uses the returned text and receives the formatted and raw value" | MTR-API-06, MTR-ARIA-05, MTR-EVT-01, MTR-DOM-05 |
| "formats the value as its position within a custom range and keeps the indicator in sync" | MTR-ALG-01, MTR-ALG-03, MTR-ARIA-02 |
| "formats the value relative to a non-zero min" | MTR-ALG-01, MTR-I18N-01 |
| "keeps range attributes, formatted text, and the indicator synchronized on rerender" | MTR-API-01, MTR-ARIA-03 |
| "normalizes aria attributes when $label" (4 variants: value exceeds max / value is below min / min equals max / value is NaN) | MTR-ALG-02, MTR-EDGE-02, MTR-EDGE-03, MTR-EDGE-04 |
| "formats the value" (prop: format) | MTR-API-04, MTR-I18N-02 |
| "formats the clamped value while clamping range attributes and indicator width" | MTR-I18N-02, MTR-ALG-02, MTR-API-06, MTR-EVT-01, MTR-EDGE-04 |
| "sets the locale when formatting the value" | MTR-API-05, MTR-I18N-03 |

### `MeterIndicator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Meter.Root value={30}>`) | MTR-DOM-09 |
| "clamps the width to 100% when the value exceeds max" / "clamps the width to 0% when the value is below min" | MTR-ALG-01, MTR-DOM-03 |
| "produces a finite width when min equals max" | MTR-ALG-01, MTR-EDGE-03 |
| "sets positioning styles" / "sets zero width when value is 0" (browser only) | MTR-DOM-03 |

### `MeterLabel.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, rendered inside `<Meter.Root value={50}>`) | MTR-DOM-09 |
| "updates and clears the meter label association" | MTR-API-12, MTR-STA-03, MTR-ARIA-06, MTR-ARIA-07 |
| "throws a descriptive error when rendered outside <Meter.Root>" | MTR-EDGE-01, MTR-STR-02 |

### `MeterTrack.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Meter.Root value={30}>`) | MTR-DOM-09, MTR-STR-04 |

### `MeterValue.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, rendered inside `<Meter.Root value={30}>`) | MTR-DOM-09 |
| "renders the value when children is not provided" | MTR-API-11, MTR-I18N-01 |
| "renders a formatted value when a format is provided" | MTR-API-11, MTR-I18N-02 |
| "accepts a render function" | MTR-API-10 |
| "passes updated arguments to the render function when value changes" | MTR-API-10, MTR-EVT-02 |

### Additional requirements introduced here

- **MTR-DOM-09** — MUST. All five parts MUST pass the shared conformance suite: RND-DOM-36…RND-DOM-48 with a `div` substitute element, the DOM handle being an `HTMLDivElement` (Root, Track, Indicator) or `HTMLSpanElement` (Value, Label). RND-DOM-49 does not apply (no `nativeButton`).
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] all five files, "Base UI component API".
- **MTR-I18N-01** — MUST. Without `format`, the formatted value MUST be `percentageValue / 100` formatted as a percent (`{ style: 'percent' }`, i.e. the formatter's default 0 fraction digits, rounding half-expand) in `locale`; the result is position-in-range, not the raw value (value `0.5` in `[0, 1]` → `"50%"`; value `30` in `[20, 40]` → `"50%"`). Server: relaxed, see §9.
  Source: `meter/root/MeterRoot.tsx:44-46`; see UTL-I18N-05. Test: [L2] `MeterRoot.test.tsx` "defaults aria-valuetext to the localized formatted value, matching Meter.Value", "rounds the default aria-valuetext like the displayed value", "formats the value relative to a non-zero min"; `MeterValue.test.tsx` "renders the value when children is not provided".
- **MTR-I18N-02** — MUST. With `format`, the formatted value MUST be the clamped value (not the percentage) formatted with exactly those options in `locale` (value `150` with `{style:'currency',currency:'USD'}` and default range → `"$100.00"` in `en-US`). Server: relaxed, see §9.
  Source: `meter/root/MeterRoot.tsx:44-45`. Test: [L2] `MeterRoot.test.tsx` "formats the value", "formats the clamped value while clamping range attributes and indicator width"; `MeterValue.test.tsx` "renders a formatted value when a format is provided".
- **MTR-I18N-03** — MUST. The `locale` prop MUST select the locale for both the Value text and `aria-valuetext` (value `86.49`, `de-DE`, two fraction digits → `"86,49"`); equal `(locale, format)` pairs SHOULD reuse one cached formatter (UTL-I18N-06). Server: relaxed, see §9.
  Source: `meter/root/MeterRoot.tsx:44-46`, `packages/utils/src/formatNumber.ts:3-17`. Test: [L2] `MeterRoot.test.tsx` "sets the locale when formatting the value", "defaults aria-valuetext to the localized formatted value, matching Meter.Value".

## 14. React-specific notes and porting considerations

Non-normative.

- **Ancestor-provided shared state.** Root publishes `{ formattedValue, percentageValue, value, setLabelId }` to its descendants; Indicator, Value and Label read it, Track does not. A port can pass these values to children in any way, provided all parts render from the same computation (MTR-STA-02).
- **Label id round trip.** `useRegisteredLabelId` publishes the Label's id from a layout effect into Root's own state, so Root renders a second time (before paint) with `aria-labelledby`. On the server layout effects do not run, hence MTR-EDGE-06. A port that knows the Label's id at Root render time (e.g. by pre-registering children) may emit `aria-labelledby` in the first render; that is an acceptable improvement as long as removal and id changes still follow MTR-STA-03. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table ("FFI registration" row: on Server the publication lands after paint plus a round trip) and decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 (generated `base-ui-<n>` ids).
- **Render-time callbacks.** `getAriaValueText` and the Value children function run during React's render phase and may run more than once per commit (StrictMode); consumers are expected to keep them pure. Ports should call them once per render pass and not rely on call counts.
- **Numeric attributes.** React stringifies numeric attribute values with `String(n)` and writes a numeric `0` style value without a unit (`inset-inline-start: 0`). A port must use the ECMAScript number serialization (MTR-ALG-03); C# `double.ToString()` and Rust `f64` `Display` differ for exponents and some fractions.
- **`Intl.NumberFormat`.** The percent/currency/decimal strings come from the host ICU data. A .NET port must reproduce `Intl.NumberFormat` output (including the no-break space in `de-DE` percent) to pass translated tests; see [UTL](../architecture/shared-utilities.md) §4.15.2. Port: see decisions/01-hosting-model-and-timing.md §Decision item 2 (formatting is render-time state work, so C#); whether .NET formatting can match `Intl.NumberFormat` is not settled by any ADR (§16 item 10).
- **NVDA span.** The trailing `x` span (MTR-DOM-01) is a screen-reader workaround and part of the observable DOM; the root's `textContent` therefore ends with `x`.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| MTR-STR-01 | MUST | Root renders one element: children + trailing hidden span | 2 |
| MTR-STR-02 | MUST | Indicator/Value/Label must be inside Root; throw otherwise | 2 |
| MTR-STR-03 | MUST | Track has no Root dependency | 2 |
| MTR-STR-04 | MUST | Track is structural only | 2 |
| MTR-API-01 | MUST | `value` required, never stored; all outputs derived per render | 3.1 |
| MTR-API-02 | MUST | `min` default 0 | 3.1 |
| MTR-API-03 | MUST | `max` default 100 | 3.1 |
| MTR-API-04 | MUST | `format` given → format clamped value; else percent of percentage | 3.1 |
| MTR-API-05 | MUST | `locale` passed to formatter; undefined → runtime default | 3.1 |
| MTR-API-06 | MUST | `getAriaValueText(formattedValue, rawValue)` → aria-valuetext | 3.1 |
| MTR-API-07 | MUST | consumer `aria-valuetext` overrides computed | 3.1 |
| MTR-API-08 | MUST | className/style/render per RND with state `{}` | 3.1 |
| MTR-API-09 | MUST | other Root props forwarded, consumer wins | 3.1 |
| MTR-API-10 | MUST | Value children function `(formattedValue, rawValue)` | 3.4 |
| MTR-API-11 | MUST | non-function Value children ignored; formatted value shown | 3.4 |
| MTR-API-12 | MUST | Label id = consumer id or generated; published to Root | 3.5 |
| MTR-API-13 | MUST | Track/Indicator/Value/Label forward other props | 3.5 |
| MTR-STA-01 | MUST | every part's state is `{}` | 3.1 |
| MTR-STA-02 | MUST | width, Value text, aria-valuetext, aria-valuenow agree per render | 4 |
| MTR-STA-03 | MUST | Root tracks one label id; guarded withdrawal | 4 |
| MTR-DOM-01 | MUST | trailing `<span role=presentation>` `x`, visually hidden | 5.1 |
| MTR-DOM-02 | MUST | no built-in data attributes | 5.1 |
| MTR-DOM-03 | MUST | Indicator style inset-inline-start 0, height inherit, width P% | 5.2 |
| MTR-DOM-04 | MUST | consumer Indicator style overrides built-in keys | 5.2 |
| MTR-DOM-05 | MUST | Value text = formatted value = default aria-valuetext | 5.3 |
| MTR-DOM-06 | MUST | Value `aria-hidden="true"` | 5.3 |
| MTR-DOM-07 | MUST | Label id and `role="presentation"` | 5.4 |
| MTR-DOM-08 | MUST | consumer attributes override built-in | 5.1 |
| MTR-DOM-09 | MUST | all parts pass RND conformance suite | 13 |
| MTR-ARIA-01 | MUST | `role="meter"` | 5.5 |
| MTR-ARIA-02 | MUST | `aria-valuenow` = clamped value | 5.5 |
| MTR-ARIA-03 | MUST | `aria-valuemin`/`max` = effective min/max | 5.5 |
| MTR-ARIA-04 | MUST | default `aria-valuetext` = formatted value | 5.5 |
| MTR-ARIA-05 | MUST | `aria-valuetext` from getAriaValueText | 5.5 |
| MTR-ARIA-06 | MUST | `aria-labelledby` = tracked label id; absent without Label | 5.5 |
| MTR-ARIA-07 | MUST | labelledby updated post-commit, pre-paint | 5.5 |
| MTR-FOC-01 | MUST | not focusable; no tabindex | 6.3 |
| MTR-EVT-01 | MUST | getAriaValueText called on every render of the Root part | 7 |
| MTR-EVT-02 | MUST | Value children function called on every render of the Value part with current args | 7 |
| MTR-I18N-01 | MUST | default formatting: percent of position-in-range | 13 |
| MTR-I18N-02 | MUST | `format`: clamped value with given options | 13 |
| MTR-I18N-03 | MUST | `locale` applies to Value and aria-valuetext; cached formatter | 13 |
| MTR-I18N-04 | MUST | no direction read; logical inset, physical width | 6.5 |
| MTR-EDGE-01 | MUST | missing Root context error (message) | 12 |
| MTR-EDGE-02 | MUST | NaN value → min / 0% | 12 |
| MTR-EDGE-03 | MUST | min === max handling | 12 |
| MTR-EDGE-04 | MUST | out-of-range clamping; raw value to callbacks | 12 |
| MTR-EDGE-05 | MUST | inverted range not validated | 12 |
| MTR-EDGE-06 | MUST | SSR markup lacks aria-labelledby | 12 |
| MTR-ALG-01 | MUST | percentage formula, NaN → 0, clamp [0,100] | 4 |
| MTR-ALG-02 | MUST | clamped value; NaN → min | 4 |
| MTR-ALG-03 | MUST | Unformatted numbers in attributes/styles, serialized per UTL-DOM-37 | 4 |

## 16. Open questions

1. **Hint: `data-*` attributes.** The assignment asked for Meter `data-*`; v1.9.0 Meter has none. `MeterRootState` is `{}` (`meter/root/MeterRoot.tsx:88`) and no part passes a state or mapping to the renderer, unlike Progress. Documented as MTR-DOM-02.
2. **Hint: "width/inline-size".** The Indicator uses the physical `width` together with the logical `inset-inline-start` (`meter/indicator/MeterIndicator.tsx:26-30`); there is no `inline-size`. In a vertical writing mode the two disagree. Also, `inset-inline-start` has no effect unless the consumer positions the Indicator (the docs demo does not). MTR-I18N-04 states the literal behavior.
3. **Track does not require Root.** `MeterTrack.tsx` never reads the Root context, so it renders outside Root without error, whereas `Progress.Track` throws (`progress/track/ProgressTrack.tsx:21`). MTR-STR-03 states the Meter behavior; a port may choose to align with Progress.
4. **Inverted range.** `min > max` is not validated (`meter/root/MeterRoot.tsx:38-40`); `aria-valuenow` then always equals `min`. MTR-EDGE-05 specifies literal behavior; a port may prefer to warn.
5. **`format` with `style: 'percent'`.** Because `format` applies to the clamped value, `format={{ style: 'percent' }}` with value `30` renders `"3,000%"` (`meter/root/MeterRoot.tsx:44-45`). Consistent with the docs ("Options to format the value") but easy to misuse; no warning.
6. **`value` omitted at runtime.** `value` is typed as required. If `undefined` is passed, `Number.isNaN(undefined)` is `false`, so `clamp(undefined, …)` yields `NaN` and `aria-valuenow="NaN"` is rendered while the percentage is `0` (`meter/root/MeterRoot.tsx:38-40`). Not specified; a typed port cannot reach this state.
7. **Consumer `aria-valuetext` vs `getAriaValueText`.** When both are given, `getAriaValueText` is still called but its result is discarded (`meter/root/MeterRoot.tsx:49-51,82`). MTR-API-07 / MTR-EVT-01 describe this.
8. **Empty-string Label id.** `<Meter.Label id="">` is used verbatim (RND-DOM-28) and published, giving `aria-labelledby=""` on Root (`utils/useRegisteredLabelId.ts:10-13`). Untested.
9. **Multiple Labels.** Only one id is tracked; with two Labels the most recently registered wins and unmounting the older one does not clear it (FFI-ARIA-08). Multiple labels are not a documented configuration.
10. **Locale formatting parity.** The formatted value is `Intl.NumberFormat` output from the host's ICU data (`packages/utils/src/formatNumber.ts:13,27`; `meter/root/MeterRoot.tsx:44-46`) and the tests compare against `Intl.NumberFormat` in the same runtime (`meter/root/MeterRoot.test.tsx:43,255,281`). ADR 01 Decision item 2 places render-time work in C#, but no ADR decides whether C# number formatting (which differs from ECMA-402 in percent spacing, currency patterns and rounding options) must match `Intl.NumberFormat` byte for byte, or whether the port may call the browser's `Intl` (impossible synchronously during a Server render). Needs a decision before MTR-I18N-01..03 and MTR-ARIA-04 can be tested. Decided (2026-10-18): .NET globalization; output equivalent, not byte-identical (ADR 01 Decision 2, amended).
11. **Port notes (2026-10-21, M1 track 2, issue 59).** The Blazor surface of the document's rows, in one place; each bullet cites the decision it follows.
    - **`format` and `locale` (MTR-API-04, MTR-API-05, MTR-I18N-01..03; ADR 08 Amendments 2026-10-21, ADR 01 decision 2).** `Format : string?` is a .NET numeric format string applied as `double.ToString(format, culture)` to the clamped value; any non-null value counts as "provided", an empty string included (MTR-API-04's "including an empty one"), and formats with the general format. `Culture : CultureInfo?` is passed unchanged, `null` meaning `CultureInfo.CurrentCulture` (read on every change of the inputs, so a changed current culture is picked up). Without `Format`, the percentage divided by 100 is formatted with `"P0"`. The output is equivalent, not byte-identical, to `Intl.NumberFormat`; the accepted divergences are: percent text in `de-DE` is `30 %` with an ASCII space where `Intl` writes U+00A0 (and `fr-FR` U+202F), the currency symbol of `C` follows the culture where `Intl` formats the `currency` code the options name, and the nine rounding modes and the other `Intl` options have no equivalent (NumberField, M6). The samples of MTR-ARIA-04 hold: `33.333` in `en-US` gives `33%`.
    - **Value template (MTR-API-10, MTR-API-11; ADR 08 Amendments 2026-10-20).** Base UI's `children` function is the template parameter `MeterValue.ValueContent : RenderFragment<MeterValueArguments>?`, used with `Context`; `MeterValueArguments` is `readonly record struct MeterValueArguments(string FormattedValue, double Value)`. A plain `ChildContent` is discarded; with no template the part renders the formatted value.
    - **Parts and state (MTR-STA-01, MTR-STR-03; ADR 02 Amendments 2026-10-20).** The five parts are `MeterRoot`, `MeterLabel`, `MeterTrack`, `MeterIndicator` and `MeterValue` over `PartBase<EmptyState>`. `MeterTrack` reads no context and renders anywhere (§16 item 3 stays a decision for Progress).
    - **Label and `Enabled` (MTR-STA-03).** A `MeterLabel` with `Enabled="false"` renders no element and publishes no id; a root whose consumer supplies its own `aria-labelledby` ignores the published id (MTR-ARIA-06).
    - **Sharing (MTR-STA-02; ADR 16 rule 9).** Root, Indicator and Value read one computation in an internal fixed-cascade context; a change reaches the Indicator and the Value through the context's listeners, never through the cascade.
    - **Helpers (MTR-ALG-01..03, MTR-DOM-01; UTL-EDGE-14, UTL-DOM-31, UTL-DOM-37, UTL-DOM-13, FFI-ARIA-08).** Percentage, clamp, number serialization, the `visuallyHidden` declarations, the formatting and the label-id slot are in `src/Blazix.Primitives/Utilities/` (folder contract `CONTEXT.md`), reused by Progress; the NVDA span is rendered inline by the root.
    - **Tests.** MTR-ALG-01 and MTR-ALG-02 are tested at L1 on those helpers, the rest at L2 (bUnit) with the browser clause of MTR-DOM-03 at L4 on both render modes; the steady-state render of the root is an allocation budget (ADR 16 rule 7).
