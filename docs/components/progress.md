# Progress

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/progress/` |
| Tests | `packages/react/src/progress/**/*.test.tsx` (5 files) |
| Docs page | `docs/src/app/(docs)/react/components/progress/page.mdx` |
| Prefix | `PRG` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) (label-id publication only), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Progress displays the completion status of a long-running task (upload, export, install), the WAI-ARIA `progressbar` role. The consumer supplies `value` (a number, or `null` when the amount of work is unknown) and optionally `min`/`max`. From these the component derives a *status* — `indeterminate`, `progressing` or `complete` — which every part exposes as a `data-*` attribute, computes the value's position in the range as a percentage that sizes an indicator element, and renders a locale-formatted text version of the value. Assistive technology receives the value through `aria-valuenow`/`aria-valuemin`/`aria-valuemax`/`aria-valuetext`; in the indeterminate status `aria-valuenow` is omitted.

Progress has no user interaction, focus behavior, change callbacks or form participation; the value is always owned by the consumer. It is the sibling of Meter ([meter](meter.md)): same parts and value computations, but with the `progressbar` role, the indeterminate status, and status data attributes on every part.

No component builds on Progress, and Progress builds on no other component.

## 2. Anatomy

```
<Progress.Root>                 required, 1          default element: div   (+ trailing visually hidden <span>)
  <Progress.Label>              optional, 0..1       default element: span
  <Progress.Track>              optional, 0..n       default element: div
    <Progress.Indicator>        optional, 0..n       default element: div
  <Progress.Value>              optional, 0..n       default element: span
```

The order of Label, Track and Value inside Root is free (the docs anatomy places Label, then Track with Indicator, then Value). Nothing enforces that Indicator is inside Track.

- **PRG-STR-01** — MUST. `Progress.Root` MUST render one host element (default `<div>`) whose children are the consumer's children followed by exactly one additional trailing `<span>` described in PRG-DOM-01.
  Source: `progress/root/ProgressRoot.tsx:73-80,94-99`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes" (`getByRole('progressbar')`).
- **PRG-STR-02** — MUST. `Progress.Track`, `Progress.Indicator`, `Progress.Value` and `Progress.Label` MUST be rendered inside the subtree of a `Progress.Root`; rendering any of them elsewhere MUST throw the error in PRG-EDGE-01.
  Source: `progress/root/ProgressRootContext.tsx:28-37`, `progress/track/ProgressTrack.tsx:21`, `progress/indicator/ProgressIndicator.tsx:21`, `progress/value/ProgressValue.tsx:20`, `progress/label/ProgressLabel.tsx:22`. Test: [L2] `ProgressLabel.test.tsx` "throws a descriptive error when rendered outside <Progress.Root>".
- **PRG-STR-03** — MUST. `Progress.Track` is structural: it MUST render an element carrying the root's status data attributes (PRG-DOM-02) and the consumer's attributes, and nothing else (no built-in styles, handlers or ARIA).
  Source: `progress/track/ProgressTrack.tsx:19-28`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle" (track attributes).

All parts other than Root may be omitted; Root alone exposes the full ARIA value information. Without a Label the progress bar has no `aria-labelledby` (the consumer may supply `aria-label`).

## 3. Public API

### 3.1 `Progress.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `number \| null` | — (required) | Current value; `null` (or any non-finite number) means indeterminate. Always consumer-owned. | PRG-API-01 |
| `min` | `number` | `0` | Lower bound of the range. | PRG-API-02 |
| `max` | `number` | `100` | Upper bound of the range. | PRG-API-03 |
| `format` | `Intl.NumberFormatOptions` | — | Number-format options. When given, the *clamped value* is formatted with them; when absent, the *percentage* is formatted as a percent. | PRG-API-04 |
| `locale` | `Intl.LocalesArgument` | — (runtime default locale) | Locale used for all formatting. | PRG-API-05 |
| `getAriaValueText` | `(formattedValue: string, value: number \| null) => string` | — | Produces the `aria-valuetext` string in every status. | PRG-API-06 |
| `aria-valuetext` | `string` | — | Forwarded attribute; overrides the computed `aria-valuetext`. Listed in the API outline. | PRG-API-07 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | PRG-API-08 |
| `children` | node | — | Rendered before the hidden trailing span. | PRG-STR-01 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the root element. | PRG-API-09 |

**Consumed (not forwarded to the element):** `value`, `min`, `max`, `format`, `locale`, `getAriaValueText`, `className`, `style`, `render`, `children` (re-emitted inside the root, see PRG-STR-01).

- **PRG-API-01** — MUST. `value` is required and never stored: every render MUST derive status, percentage, clamped value, formatted text, ARIA attributes, data attributes and indicator style from the `value`, `min`, `max`, `format` and `locale` of that render, so a change of any of them (including `format` alone) is reflected by every part in the same render.
  Source: `progress/root/ProgressRoot.tsx:42-62,83-92`. Test: [L2] `ProgressRoot.test.tsx` "should update aria-valuenow when value changes", "keeps every composed part synchronized through the status cycle", "reflects format changes without lagging a commit".
- **PRG-API-02** — MUST. `min` MUST default to `0` when omitted or `undefined`.
  Source: `progress/root/ProgressRoot.tsx:27`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes" (`aria-valuemin="0"`).
- **PRG-API-03** — MUST. `max` MUST default to `100` when omitted or `undefined`.
  Source: `progress/root/ProgressRoot.tsx:26`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes" (`aria-valuemax="100"`).
- **PRG-API-04** — MUST. While determinate, when `format` is provided (any object, including an empty one), the formatted value MUST be the clamped value (PRG-ALG-02) formatted with `format` and `locale`; when `format` is absent it MUST be the percentage (PRG-ALG-01) divided by 100, formatted with `{ style: 'percent' }` and `locale`. While indeterminate the formatted value MUST be the empty string `''`.
  Source: `progress/root/ProgressRoot.tsx:45,56-58`. Test: [L2] `ProgressRoot.test.tsx` "formats the value", "sets the correct aria attributes", "receives the formatted and raw values for determinate and indeterminate states" (`''`).
- **PRG-API-05** — MUST. `locale` MUST be passed unchanged to the number formatter; when `undefined`, the runtime's default locale applies.
  Source: `progress/root/ProgressRoot.tsx:56-58`, `packages/utils/src/formatNumber.ts:19-28`. Test: [L2] `ProgressRoot.test.tsx` "sets the locale when formatting the value".
- **PRG-API-06** — MUST. When `getAriaValueText` is provided, the root's computed `aria-valuetext` MUST be its return value in every status, called with `(formattedValue, value)` where `formattedValue` is the string of PRG-API-04 (`''` when indeterminate) and `value` is the raw `value` prop (unclamped; `null`, `NaN` or `±∞` passed through as given).
  Source: `progress/root/ProgressRoot.tsx:69-71,127`. Test: [L2] `ProgressRoot.test.tsx` "receives the formatted and raw values for determinate and indeterminate states", "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range".
- **PRG-API-07** — MUST. A consumer-supplied `aria-valuetext` attribute MUST replace the computed `aria-valuetext` (default or from `getAriaValueText`); it is merged after the built-in attributes.
  Source: `progress/root/ProgressRoot.tsx:97,119`, `merge-props/mergeProps.ts:162-184` (RND-DOM-19). Test: none.
- **PRG-API-08** — MUST. `className`, `style` and `render` MUST behave per RND for every Progress part; the state object they receive is the one below, identical across parts.
  Source: `progress/root/ProgressRoot.tsx:29-32,94-99`. Test: [L2] every `Progress*.test.tsx` conformance suite.
- **PRG-API-09** — MUST. Every Root prop other than the consumed list MUST be forwarded to the root element and merged after the built-in attributes, so a consumer value for the same attribute (`role`, `aria-valuenow`, `aria-valuemin`, `aria-valuemax`, `aria-labelledby`, `aria-valuetext`, a status `data-*`) wins (RND-DOM-10, RND-DOM-19).
  Source: `progress/root/ProgressRoot.tsx:22-34,97`. Test: [L2] `ProgressRoot.test.tsx` conformance suite.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `status` | `'indeterminate' \| 'progressing' \| 'complete'` | Derived status (PRG-STA-02, PRG-STA-03). |

- **PRG-STA-01** — MUST. The state object of Root, Track, Indicator, Value and Label MUST be exactly `{ status }`, the same value for all parts in the same render.
  Source: `progress/root/ProgressRoot.tsx:62,108-113`, `progress/track/ProgressTrack.tsx:21-25`, `progress/indicator/ProgressIndicator.tsx:21,33`, `progress/value/ProgressValue.tsx:20,29`, `progress/label/ProgressLabel.tsx:22,27`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-indeterminate` | `""` while `status === 'indeterminate'` | PRG-DOM-02 |
| `data-progressing` | `""` while `status === 'progressing'` | PRG-DOM-02 |
| `data-complete` | `""` while `status === 'complete'` | PRG-DOM-02 |

No `data-status` attribute is emitted.

#### CSS variables

None.

### 3.2 `Progress.Track`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive `{ status }`. | PRG-API-08 |
| *other `<div>` attributes* | — | — | Forwarded. | PRG-API-13 |

State object, data attributes: as §3.1. CSS variables: none.

### 3.3 `Progress.Indicator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Functions receive `{ status }`. A consumer `style` overrides the built-in sizing keys (PRG-DOM-05). | PRG-API-08 |
| *other `<div>` attributes* | — | — | Forwarded. | PRG-API-13 |

State object, data attributes: as §3.1. Inline style: PRG-DOM-03, PRG-DOM-04. CSS variables: none.

### 3.4 `Progress.Value`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `null \| (formattedValue: string \| null, value: number \| null) => node` | — | Optional function producing the element's content. | PRG-API-10, PRG-API-11 |
| `className`, `style`, `render` | see RND | — | Functions receive `{ status }`. | PRG-API-08 |
| *other `<span>` attributes* | — | — | Forwarded. | PRG-API-13 |

- **PRG-API-10** — MUST. When `children` is a function, the Value element's content MUST be its return value, called with `(formattedArg, value)`: `formattedArg` is the formatted value of PRG-API-04 while determinate and the literal string `'indeterminate'` while indeterminate; `value` is the raw `value` prop (`null`, `NaN`, `±∞` passed through as given).
  Source: `progress/value/ProgressValue.tsx:20-25,34-37`. Test: [L2] `ProgressValue.test.tsx` "numerical value", "indeterminate value %s" (`null`, `NaN`).
- **PRG-API-11** — MUST. When `children` is not a function (`undefined`, `null`, or any other node), the Value element's content MUST be the formatted value while determinate and MUST be empty (no child nodes) while indeterminate; non-function children are discarded.
  Source: `progress/value/ProgressValue.tsx:18,26,34-37`. Test: [L2] `ProgressValue.test.tsx` "renders the value when children is not provided", "renders a formatted value when a format is provided"; `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle" (`toBeEmptyDOMElement`).

State object, data attributes: as §3.1. CSS variables: none.

### 3.5 `Progress.Label`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated `base-ui-…` | Id of the label element; published to Root for `aria-labelledby`. Not listed in the API outline (native attribute). | PRG-API-12 |
| `className`, `style`, `render` | see RND | — | Functions receive `{ status }`. | PRG-API-08 |
| *other `<span>` attributes* | — | — | Forwarded. | PRG-API-13 |

- **PRG-API-12** — MUST. The Label element's `id` MUST be the consumer's `id` when supplied (used verbatim, RND-DOM-28), otherwise a generated id `base-ui-<token>` (RND-DOM-27); that id MUST be the one published to Root (PRG-ARIA-06).
  Source: `progress/label/ProgressLabel.tsx:20,24,31`, `utils/useRegisteredLabelId.ts:10`. Test: [L2] `ProgressLabel.test.tsx` "updates and clears the progress bar label association"; `ProgressRoot.test.tsx` "sets the correct aria attributes" (generated id).
- **PRG-API-13** — MUST. For Track, Indicator, Value and Label, every prop other than `className`, `style`, `render` (and, for Value, `children`; for Label, `id`, re-emitted per PRG-API-12) MUST be forwarded to the part's element, merged after the built-in attributes.
  Source: `progress/track/ProgressTrack.tsx:19,26`, `progress/indicator/ProgressIndicator.tsx:19,35-40`, `progress/value/ProgressValue.tsx:18,31-40`, `progress/label/ProgressLabel.tsx:20,28-35`. Test: [L2] conformance suites of all four parts; `ProgressRoot.test.tsx` `data-testid` queries.

State object, data attributes: as §3.1. CSS variables: none.

### 3.6 Shared types

- `Progress.Status` (alias `ProgressStatus`) = `'indeterminate' | 'progressing' | 'complete'`. Source: `progress/root/ProgressRoot.tsx:106`, `progress/index.parts.ts:7`.
- `Progress.Root.State` = `{ status: Progress.Status }`; `Progress.Track.State`, `Progress.Indicator.State`, `Progress.Value.State`, `Progress.Label.State` extend it with no fields. Source: `progress/root/ProgressRoot.tsx:108-113`, part `*State` interfaces.
- `Progress.Root.Props` … `Progress.Label.Props` — the prop tables above; flat aliases (`ProgressRootProps`, …) are also exported. Source: `progress/index.ts:1-7`.
- Exported constant namespaces `ProgressRootDataAttributes`, `ProgressTrackDataAttributes`, `ProgressIndicatorDataAttributes`, `ProgressValueDataAttributes`, `ProgressLabelDataAttributes`, each with `complete = 'data-complete'`, `indeterminate = 'data-indeterminate'`, `progressing = 'data-progressing'` (all five identical). Source: `progress/index.ts:9-13`, `progress/*/Progress*DataAttributes.ts:1-12`.
- No `ChangeEventDetails`/reason types exist for Progress.

## 4. State model

Progress holds no value state. The only component-held state is the id of the currently mounted Label. Everything else is a pure function of the current props:

```
if value is not null/undefined and is a finite number:          (determinate)
    rawPercentage   = ((value − min) × 100) / (max − min)                      (PRG-ALG-01)
    percentageValue = clamp(isNaN(rawPercentage) ? 0 : rawPercentage, 0, 100)
    clampedValue    = clamp(value, min, max)                                   (PRG-ALG-02)
    status          = clampedValue === max ? 'complete' : 'progressing'
    formattedValue  = format ? fmt(clampedValue, locale, format)
                             : fmt(percentageValue / 100, locale, {style:'percent'})
    defaultText     = formattedValue
else:                                                            (indeterminate)
    status = 'indeterminate'; percentageValue = clampedValue = none
    formattedValue = ''; defaultText = 'indeterminate progress'
ariaValueText = getAriaValueText ? getAriaValueText(formattedValue, value) : defaultText
```

Status cycle (driven only by prop changes; any status can follow any other):

```
indeterminate ──value finite──► progressing ──clamped value reaches max──► complete
      ▲                              │  ▲                                     │
      └──────value null / non-finite─┘  └──────clamped value < max────────────┘
```

- **PRG-STA-02** — MUST. Progress MUST be determinate exactly when `value` is neither `null` nor `undefined` and is a finite number; `null`, `undefined`, `NaN`, `+∞` and `−∞` MUST all yield status `'indeterminate'`.
  Source: `progress/root/ProgressRoot.tsx:42,49`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle", "keeps non-finite value %s indeterminate" (`NaN`, `+∞`, `−∞`).
- **PRG-STA-03** — MUST. While determinate, status MUST be `'complete'` when the clamped value equals `max` (i.e. `value ≥ max` for a well-formed range, and also when `min === max === value`) and `'progressing'` otherwise.
  Source: `progress/root/ProgressRoot.tsx:52-53`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle" (`50` → progressing, `100` → complete), "reports complete when the value reaches or exceeds max".
- **PRG-STA-04** — MUST. Root, Track, Indicator, Value and Label MUST observe the same derived values within one render: status attributes, Indicator style, Value text, `aria-valuenow` and the default `aria-valuetext` MUST never disagree for the current props.
  Source: `progress/root/ProgressRoot.tsx:38-41,83-92`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle", "normalizes the formatted value, aria-valuetext, and indicator within a custom range". Docs: CHANGELOG v1.7.0 "Fix custom `min`/`max` semantics to match the indicator".
- **PRG-STA-05** — MUST. Root MUST track at most one label id: the id most recently published by a mounted `Progress.Label`. When a Label unmounts or changes id, the previously published id MUST be withdrawn only if it is still the tracked id (FFI-ARIA-08).
  Source: `progress/root/ProgressRoot.tsx:36`, `utils/useRegisteredLabelId.ts:12-17`. Test: [L2] `ProgressLabel.test.tsx` "updates and clears the progress bar label association".

- **PRG-ALG-01** — MUST. While determinate, the percentage MUST be `((value − min) × 100) / (max − min)` (UTL-DOM-31) in IEEE-754 double arithmetic, with a `NaN` result replaced by `0`, then clamped to `[0, 100]`.
  Source: `progress/root/ProgressRoot.tsx:50-51`, `utils/valueToPercent.ts:1-3`. Test: [L1] `ProgressRoot.test.tsx` "normalizes the formatted value, aria-valuetext, and indicator within a custom range", "clamps aria-valuenow, the value text, and the indicator when the value overshoots max", "… undershoots min", "normalizes aria attributes when min equals max".
- **PRG-ALG-02** — MUST. While determinate, the clamped value MUST be `clamp(value, min, max) = max(min, min(value, max))` (UTL-EDGE-14).
  Source: `progress/root/ProgressRoot.tsx:52`, `packages/utils/src/clamp.ts:6`. Test: [L1] `ProgressRoot.test.tsx` "clamps aria-valuenow, the value text, and the indicator when the value overshoots max", "… undershoots min", "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range".
- **PRG-ALG-03** — MUST. Numbers written into attributes and inline styles (`aria-valuenow`, `aria-valuemin`, `aria-valuemax`, the Indicator `width` percentage) are written unformatted, so their serialization is per UTL-DOM-37; no rounding is applied to them.
  Source: `progress/root/ProgressRoot.tsx:66-68`, `progress/indicator/ProgressIndicator.tsx:29`; see UTL-DOM-37. Test: [L2] `ProgressRoot.test.tsx` "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range" (`String(expectedValue)`).

There is no controlled/uncontrolled pair and no normalization of `min`/`max`.

## 5. Rendered DOM and ARIA

### 5.1 Root element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `role` | `"progressbar"` | PRG-ARIA-01 | C# |
| `aria-valuenow` | clamped value; absent while indeterminate | PRG-ARIA-02 | C# |
| `aria-valuemin` / `aria-valuemax` | `min` / `max` (all statuses) | PRG-ARIA-03 | C# |
| `aria-valuetext` | formatted value / `"indeterminate progress"` / `getAriaValueText(...)` | PRG-ARIA-04, PRG-ARIA-05 | C# |
| `aria-labelledby` | id of the mounted Label; absent when none | PRG-ARIA-06 | C# |
| `data-indeterminate` / `data-progressing` / `data-complete` | one of them, per status | PRG-DOM-02 | C# |
| `tabindex` | not set | PRG-FOC-01 | C# |
| trailing child | visually hidden `<span role="presentation">x</span>` | PRG-DOM-01 | C# |

- **PRG-DOM-01** — MUST. After the consumer's children, Root MUST render one `<span>` with `role="presentation"`, the inline style `visuallyHidden` (UTL-DOM-13: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0`) and the single text character `x`, in every status. The span exists so that the NVDA screen reader announces the label (Base UI issue 4184).
  Source: `progress/root/ProgressRoot.tsx:73-80`, `packages/utils/src/visuallyHidden.ts:3-20`. Test: none. Docs: CHANGELOG v1.3.0 "Fix label announcements in NVDA".
- **PRG-DOM-02** — MUST. Every part (Root, Track, Indicator, Value, Label) MUST carry exactly one of `data-indeterminate`, `data-progressing`, `data-complete` (value `""`), matching `status`, and none of the other two; the attributes MUST switch in the same render as the status.
  Source: `progress/root/stateAttributesMapping.ts:5-18`, `progress/root/ProgressRoot.tsx:98`, `progress/track/ProgressTrack.tsx:27`, `progress/indicator/ProgressIndicator.tsx:41`, `progress/value/ProgressValue.tsx:41`, `progress/label/ProgressLabel.tsx:36`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle", "reports complete when the value reaches or exceeds max", "keeps non-finite value %s indeterminate".
- **PRG-DOM-09** — MUST. Consumer attributes MUST take precedence over built-in attributes of the same name on every part (e.g. `role`, `aria-*`, Value's `aria-hidden`, a status `data-*`); `style` merges per RND-DOM-16 and `className` per RND-DOM-14. (On Label the consumer `id` is already the built-in id, PRG-API-12.)
  Source: `progress/root/ProgressRoot.tsx:97`, `progress/indicator/ProgressIndicator.tsx:35-40`, `progress/value/ProgressValue.tsx:31-40`, `progress/label/ProgressLabel.tsx:28-35`. Test: none.

### 5.2 Indicator element

| Status | Inline style declarations | Req | Writer |
|---|---|---|---|
| `progressing`, `complete` | `inset-inline-start: 0; height: inherit; width: <P>%` | PRG-DOM-03 | C# |
| `indeterminate` | none | PRG-DOM-04 | C# |

- **PRG-DOM-03** — MUST. While determinate, the Indicator element MUST carry the inline style declarations `inset-inline-start: 0`, `height: inherit` and `width: <P>%`, where `<P>` is the percentage of PRG-ALG-01 serialized per PRG-ALG-03.
  Source: `progress/indicator/ProgressIndicator.tsx:21-30`. Test: [L2] `ProgressIndicator.test.tsx` "determinate" (browser: `inset-inline-start: 0px`, `width: 33%` with `render={<span />}`), "sets zero width when value is 0" (browser: `width: 0px`); `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle" (`50%`, `100%`), "normalizes the formatted value, aria-valuetext, and indicator within a custom range", "clamps aria-valuenow, …" (both), "normalizes aria attributes when min equals max" (`0%`). (L4 for the computed-style browser citations)
- **PRG-DOM-04** — MUST. While indeterminate, the Indicator MUST receive no built-in inline style declarations at all (its `style` contains only what the consumer supplies; with no consumer style, `style.width` is empty).
  Source: `progress/indicator/ProgressIndicator.tsx:23-25`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle" (`style.width === ''`), "keeps non-finite value %s indeterminate"; `ProgressIndicator.test.tsx` "indeterminate" (browser).
- **PRG-DOM-05** — MUST. A consumer `style` on the Indicator MUST override the built-in declarations key by key (RND-DOM-16).
  Source: `progress/indicator/ProgressIndicator.tsx:19,35-40`, `internals/useRenderElement.tsx:115-117`. Test: none.

The Indicator is not positioned by the component (no `position` declaration), so `inset-inline-start` takes effect only if the consumer positions it. Animating an indeterminate indicator (e.g. a sliding bar keyed on `[data-indeterminate]`) is the consumer's concern.

### 5.3 Value element

- **PRG-DOM-06** — MUST. The Value element's text content MUST equal the root's default `aria-valuetext` while determinate and no children function is given (PRG-API-11); while indeterminate it MUST be empty even though the default `aria-valuetext` is `"indeterminate progress"`. `getAriaValueText` MUST NOT affect the Value text.
  Source: `progress/value/ProgressValue.tsx:24-26,34-37`, `progress/root/ProgressRoot.tsx:59,69-71`. Test: [L2] `ProgressRoot.test.tsx` "normalizes the formatted value, aria-valuetext, and indicator within a custom range", "receives the formatted and raw values for determinate and indeterminate states".
- **PRG-DOM-07** — MUST. The Value element MUST carry `aria-hidden="true"` in every status unless the consumer overrides it.
  Source: `progress/value/ProgressValue.tsx:33`. Test: none.

### 5.4 Label element

- **PRG-DOM-08** — MUST. The Label element MUST carry the id of PRG-API-12 and `role="presentation"`.
  Source: `progress/label/ProgressLabel.tsx:29-33`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes" (label has an id).

### 5.5 ARIA

- **PRG-ARIA-01** — MUST. The root element MUST have `role="progressbar"` unless the consumer supplies a `role`.
  Source: `progress/root/ProgressRoot.tsx:72`. Test: [L2] `ProgressRoot.test.tsx` every `getByRole('progressbar')` query.
- **PRG-ARIA-02** — MUST. While determinate, `aria-valuenow` MUST be the clamped value (PRG-ALG-02); while indeterminate the attribute MUST be absent.
  Source: `progress/root/ProgressRoot.tsx:68`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes", "should update aria-valuenow when value changes", "keeps every composed part synchronized through the status cycle", "clamps aria-valuenow, …" (both), "keeps non-finite value %s indeterminate".
- **PRG-ARIA-03** — MUST. `aria-valuemin` MUST be the effective `min` and `aria-valuemax` the effective `max` in every status, including indeterminate (no validation or reordering).
  Source: `progress/root/ProgressRoot.tsx:66-67`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes", "clamps aria-valuenow, the value text, and the indicator when the value overshoots max" (`aria-valuemax="40"`), "… undershoots min" (`aria-valuemin="20"`).
- **PRG-ARIA-04** — MUST. Without `getAriaValueText`, `aria-valuetext` MUST be the formatted value (PRG-API-04) while determinate and the literal English string `"indeterminate progress"` while indeterminate.
  Source: `progress/root/ProgressRoot.tsx:47,59,69-71`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes", "keeps every composed part synchronized through the status cycle", "keeps non-finite value %s indeterminate".
- **PRG-ARIA-05** — MUST. With `getAriaValueText`, `aria-valuetext` MUST be its return value in every status (PRG-API-06); the `"indeterminate progress"` default is then not used.
  Source: `progress/root/ProgressRoot.tsx:69-71`. Test: [L2] `ProgressRoot.test.tsx` "receives the formatted and raw values for determinate and indeterminate states" (`"Waiting to start"`).
- **PRG-ARIA-06** — MUST. `aria-labelledby` MUST equal the id tracked per PRG-STA-05 and MUST be absent while no Label is mounted. A consumer `aria-labelledby` replaces it (PRG-API-09).
  Source: `progress/root/ProgressRoot.tsx:36,65`, `progress/label/ProgressLabel.tsx:22-24`. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes"; `ProgressLabel.test.tsx` "updates and clears the progress bar label association".
- **PRG-ARIA-07** — MUST. When a Label mounts, changes `id` or unmounts, the root's `aria-labelledby` MUST be updated post-commit, pre-paint. Server: relaxed, see §9.
  Source: `utils/useRegisteredLabelId.ts:12-17`. Test: [L2] `ProgressLabel.test.tsx` "updates and clears the progress bar label association".

Progress has no Field integration (no `Field.Label`, `aria-describedby` or `aria-invalid` wiring) and sets no `aria-busy`.

## 6. Interactions

### 6.1 Keyboard

Not applicable — no part attaches keyboard handlers; Tab skips the progress bar (PRG-FOC-01).

### 6.2 Pointer and touch

Not applicable — no part attaches pointer handlers.

### 6.3 Focus management

- **PRG-FOC-01** — MUST. No Progress part MUST add a `tabindex` or otherwise make itself focusable.
  Source: `progress/root/ProgressRoot.tsx:64-81` (no `tabIndex`). Test: none.

### 6.4 Dismissal

Not applicable — no dismissable surface.

### 6.5 Reading direction

- **PRG-I18N-04** — MUST. Progress MUST NOT read the reading direction; direction affects only the Indicator's logical `inset-inline-start` declaration, which the browser resolves (left edge in LTR, right edge in RTL, when positioned). `width` is physical and is not swapped for vertical writing modes.
  Source: `progress/indicator/ProgressIndicator.tsx:26-30` (no direction import anywhere in `progress/`). Test: none.

## 7. Events and callbacks

Progress has no change callbacks and no event details. Two consumer functions run during rendering:

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `getAriaValueText` (Root) | `(formattedValue: string, value: number \| null) => string` | every render of Root, every status | — | no |
| `children` function (Value) | `(formattedValue: string \| null, value: number \| null) => node` | every render of Value, every status | — | no |

- **PRG-EVT-01** — MUST. `getAriaValueText` MUST be called synchronously on every render of the Root part (determinate and indeterminate), with the arguments of PRG-API-06 for that render; the call count is checked against the part's render count observed through bUnit (L2).
  Source: `progress/root/ProgressRoot.tsx:69-71`. Test: [L2] `ProgressRoot.test.tsx` "receives the formatted and raw values for determinate and indeterminate states" (`toHaveBeenLastCalledWith(…, 30)` then `('', null)`).
- **PRG-EVT-02** — MUST. A Value `children` function MUST be called synchronously on every render of the Value part with the arguments of PRG-API-10 for the current props (render count observed through bUnit, L2). Although the declared type allows `null` for the first argument, it is never `null` in practice (a string or `'indeterminate'`).
  Source: `progress/value/ProgressValue.tsx:25,34-36,54`. Test: [L2] `ProgressValue.test.tsx` "numerical value", "indeterminate value %s".

## 8. Form integration

Not applicable — Progress renders no input, registers with no Field and submits nothing.

## 9. Lifecycle, mounting and animation

Not applicable — all parts are always mounted; there is no transition status. The docs demo animates the Indicator with `transition: width 500ms`; that is consumer CSS.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- PRG-I18N-01 — the percent formatting uses .NET globalization instead of `Intl.NumberFormat`; percent spacing may differ (a port relaxation on both render modes, not a timing row: ADR 01 Decision 2 as amended; output equivalent, not byte-identical).
- PRG-I18N-02 — `format` options are applied by a .NET formatter; currency patterns may differ from `Intl.NumberFormat`'s (port relaxation, both modes; ADR 01 Decision 2).
- PRG-I18N-03 — `locale` selects a .NET culture; with no `locale` the .NET current culture applies, and the formatter cache is keyed on the culture and options (port relaxation, both modes; ADR 01 Decision 2).
- PRG-ARIA-07 — FFI registration row (post-commit, pre-paint C# state work): on Server the root's `aria-labelledby` update after a Label mounts, changes id or unmounts lands after paint plus a round trip.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (all parts) | [RND](../architecture/rendering-and-composition.md) | Root `div`, Track `div`, Indicator `div`, Value `span`, Label `span`; `state = { status }` and `progressStateAttributesMapping` passed by every part; built-in props first, consumer props second |
| `getStateAttributesProps` | RND RND-DOM-06, RND-DOM-07 | custom mapping for `status` (§3.1 table); no default projection |
| `formatNumber` | [UTL](../architecture/shared-utilities.md) UTL-I18N-05…07 | determinate only: `(clampedValue, locale, format)` or `(percentageValue / 100, locale, { style: 'percent' })` |
| `valueToPercent` | UTL UTL-DOM-31 | `(value, min, max)` |
| `clamp` | UTL UTL-EDGE-14 | percentage to `[0, 100]`; value to `[min, max]` |
| `visuallyHidden` | UTL UTL-DOM-13 | style of the trailing NVDA span |
| `useRegisteredLabelId` / `useBaseUiId` | [FFI](../architecture/field-and-form-infrastructure.md) FFI-ARIA-08, RND-DOM-27 | Label publishes its id to Root's label-id slot |

## 12. Edge cases, invariants and dev warnings

Progress emits no warnings and no dev-only diagnostics of its own.

- **PRG-EDGE-01** — MUST. Rendering `Progress.Track`, `Progress.Indicator`, `Progress.Value` or `Progress.Label` outside a `Progress.Root` MUST throw an error with the message `Base UI: ProgressRootContext is missing. Progress parts must be placed within <Progress.Root>.` (thrown in all builds).
  Source: `progress/root/ProgressRootContext.tsx:28-37`. Test: [L2] `ProgressLabel.test.tsx` "throws a descriptive error when rendered outside <Progress.Root>".
- **PRG-EDGE-02** — MUST. With `min === max === value`, the percentage MUST be `0` (the `0/0` NaN case), `aria-valuenow` MUST be `min`, the Indicator width `0%`, the default formatted value the formatted `0` percent, and status `'complete'`.
  Source: `progress/root/ProgressRoot.tsx:50-53`. Test: [L2] `ProgressRoot.test.tsx` "normalizes aria attributes when min equals max" (status not asserted).
- **PRG-EDGE-03** — MUST. With a finite `value > max`, the percentage MUST be `100`, `aria-valuenow` `max`, the formatted value derived from the clamped value, and status `'complete'`; with `value < min`, the percentage MUST be `0` and `aria-valuenow` `min`. `getAriaValueText` and a Value children function still receive the raw value.
  Source: `progress/root/ProgressRoot.tsx:50-58,70`. Test: [L2] `ProgressRoot.test.tsx` "clamps aria-valuenow, the value text, and the indicator when the value overshoots max", "… undershoots min", "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range" (`50` → `$40.00`, `10` → `$20.00`), "reports complete when the value reaches or exceeds max".
- **PRG-EDGE-04** — MUST. A non-finite numeric `value` (`NaN`, `+∞`, `−∞`) MUST be treated exactly like `null` for every output (status, `aria-valuenow`, default `aria-valuetext`, Value text, Indicator style) except that callbacks receive the raw value.
  Source: `progress/root/ProgressRoot.tsx:38-41,49`. Test: [L2] `ProgressRoot.test.tsx` "keeps non-finite value %s indeterminate"; `ProgressValue.test.tsx` "indeterminate value %s" (`NaN` passed through).
- **PRG-EDGE-05** — MUST. `min > max` is not validated: outputs MUST follow PRG-ALG-01/02 and PRG-STA-03 literally (e.g. `min=100, max=0, value=30` gives percentage `70`, `aria-valuenow="100"` and status `'progressing'`, because `clamp` returns its lower bound when bounds are inverted).
  Source: `progress/root/ProgressRoot.tsx:50-53`, `packages/utils/src/clamp.ts:6`. Test: none.
- **PRG-EDGE-06** — MUST. Server-rendered markup MUST NOT contain the root's `aria-labelledby` (the label id is published only after the client mounts, PRG-ARIA-07); every other attribute, including the status data attributes and the Label's generated id, MUST match the client's first render.
  Source: `utils/useRegisteredLabelId.ts:12`, `progress/root/ProgressRoot.tsx:36,65`. Test: none.

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND.

## 13. Behaviors evidenced by tests

### `ProgressRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; `testRenderPropWith` default `div`; `button` not set) | PRG-DOM-10, PRG-API-08, PRG-API-09 |
| "sets the correct aria attributes" | PRG-ARIA-01, PRG-ARIA-02, PRG-ARIA-03, PRG-ARIA-04, PRG-ARIA-06, PRG-API-02, PRG-API-03, PRG-DOM-08, PRG-I18N-01 |
| "should update aria-valuenow when value changes" | PRG-API-01, PRG-ARIA-02 |
| "keeps every composed part synchronized through the status cycle" | PRG-STA-01, PRG-STA-02, PRG-STA-03, PRG-STA-04, PRG-DOM-02, PRG-DOM-03, PRG-DOM-04, PRG-API-11, PRG-ARIA-02, PRG-ARIA-04, PRG-STR-03, PRG-I18N-05 |
| "normalizes the formatted value, aria-valuetext, and indicator within a custom range" | PRG-ALG-01, PRG-STA-04, PRG-DOM-06, PRG-I18N-01 |
| "clamps aria-valuenow, the value text, and the indicator when the value overshoots max" / "clamps aria-valuenow, the value text, and the indicator when the value undershoots min" | PRG-ALG-01, PRG-ALG-02, PRG-ARIA-02, PRG-ARIA-03, PRG-EDGE-03 |
| "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range" (2 variants: `50`→`40`, `10`→`20`) | PRG-I18N-02, PRG-ALG-02, PRG-ALG-03, PRG-API-06, PRG-EDGE-03 |
| "reports complete when the value reaches or exceeds max" | PRG-STA-03, PRG-DOM-02, PRG-EDGE-03 |
| "normalizes aria attributes when min equals max" | PRG-EDGE-02, PRG-ALG-01 |
| "keeps non-finite value %s indeterminate" (3 variants: `NaN`, `+∞`, `−∞`) | PRG-STA-02, PRG-EDGE-04, PRG-ARIA-02, PRG-ARIA-04, PRG-DOM-04 |
| "receives the formatted and raw values for determinate and indeterminate states" | PRG-API-04, PRG-API-06, PRG-ARIA-05, PRG-EVT-01, PRG-DOM-06 |
| "formats the value" (prop: format) | PRG-API-04, PRG-I18N-02 |
| "reflects format changes without lagging a commit" | PRG-API-01, PRG-I18N-02 |
| "sets the locale when formatting the value" | PRG-API-05, PRG-I18N-03 |

### `ProgressIndicator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Progress.Root value={40}>`) | PRG-DOM-10 |
| "determinate" / "sets zero width when value is 0" (browser only) | PRG-DOM-03 |
| "indeterminate" (browser only; asserts an empty computed-style expectation) | PRG-DOM-04 |

### `ProgressLabel.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, rendered inside `<Progress.Root value={40}>`) | PRG-DOM-10 |
| "updates and clears the progress bar label association" | PRG-API-12, PRG-STA-05, PRG-ARIA-06, PRG-ARIA-07 |
| "throws a descriptive error when rendered outside <Progress.Root>" | PRG-EDGE-01, PRG-STR-02 |

### `ProgressTrack.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, rendered inside `<Progress.Root value={40}>`) | PRG-DOM-10 |

### `ProgressValue.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, rendered inside `<Progress.Root value={40}>`) | PRG-DOM-10 |
| "renders the value when children is not provided" | PRG-API-11, PRG-I18N-01 |
| "renders a formatted value when a format is provided" | PRG-API-11, PRG-I18N-02 |
| "numerical value" (it accepts a render function) | PRG-API-10, PRG-EVT-02 |
| "indeterminate value %s" (2 variants: `null`, `NaN`) | PRG-API-10, PRG-EVT-02, PRG-EDGE-04 |

### Additional requirements introduced here

- **PRG-DOM-10** — MUST. All five parts MUST pass the shared conformance suite: RND-DOM-36…RND-DOM-48 with a `div` substitute element, the DOM handle being an `HTMLDivElement` (Root, Track, Indicator) or `HTMLSpanElement` (Value, Label). RND-DOM-49 does not apply (no `nativeButton`).
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] all five files, "Base UI component API".
- **PRG-I18N-01** — MUST. While determinate and without `format`, the formatted value MUST be `percentageValue / 100` formatted as a percent (`{ style: 'percent' }`: default 0 fraction digits, half-expand rounding) in `locale`; it expresses position in range, not the raw value (value `30` in `[20, 40]` → `"50%"`). Server: relaxed, see §9.
  Source: `progress/root/ProgressRoot.tsx:56-58`; see UTL-I18N-05. Test: [L2] `ProgressRoot.test.tsx` "sets the correct aria attributes", "normalizes the formatted value, aria-valuetext, and indicator within a custom range"; `ProgressValue.test.tsx` "renders the value when children is not provided".
- **PRG-I18N-02** — MUST. While determinate and with `format`, the formatted value MUST be the clamped value formatted with exactly those options in `locale` (value `50`, range `[20, 40]`, USD currency → `"$40.00"` in `en-US`); changing `format` MUST change the text in the same render. Server: relaxed, see §9.
  Source: `progress/root/ProgressRoot.tsx:56-57`. Test: [L2] `ProgressRoot.test.tsx` "formats the value", "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range", "reflects format changes without lagging a commit"; `ProgressValue.test.tsx` "renders a formatted value when a format is provided".
- **PRG-I18N-03** — MUST. The `locale` prop MUST select the locale for both the Value text and `aria-valuetext` (value `70.51`, `de-DE`, two fraction digits → `"70,51"`); equal `(locale, format)` pairs SHOULD reuse one cached formatter (UTL-I18N-06). The indeterminate default text is outside `locale`'s reach (PRG-I18N-05). Server: relaxed, see §9.
  Source: `progress/root/ProgressRoot.tsx:56-58`, `packages/utils/src/formatNumber.ts:3-17`. Test: [L2] `ProgressRoot.test.tsx` "sets the locale when formatting the value".
- **PRG-I18N-05** — MUST. The indeterminate default `aria-valuetext` (PRG-ARIA-04) MUST be the fixed, non-localized English literal `"indeterminate progress"` regardless of `locale`, `format` or document language; it is not passed through the number formatter or any translation hook, and `getAriaValueText` (PRG-ARIA-05) is the only way to replace it. (Known i18n gap, kept for parity — §16 Q2.)
  Source: `progress/root/ProgressRoot.tsx:47,49-59`. Test: [L2] `ProgressRoot.test.tsx` "keeps every composed part synchronized through the status cycle" (literal only; no test combines it with `locale`).

## 14. React-specific notes and porting considerations

Non-normative.

- **Ancestor-provided shared state.** Root publishes `{ formattedValue, percentageValue, value, setLabelId, state }` to all four child parts (all of which require it). The memoized `state` object changes identity only when `status` changes; this is an optimization with no observable effect beyond PRG-STA-01.
- **Status data attributes.** `status` is projected through a custom mapping (one presence attribute per status, PRG-DOM-02), not the default projection. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 4 (custom mappings are hand-written; the default projection is amended by ADR 05).
- **Label id round trip.** `useRegisteredLabelId` publishes the Label's id from a layout effect into Root state, causing a second Root render before paint; on the server this never happens (PRG-EDGE-06). A port that knows the Label id during Root's render may emit `aria-labelledby` immediately, as long as id changes and removal follow PRG-STA-05. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table ("FFI registration" row: on Server the publication lands after paint plus a round trip) and decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 (generated `base-ui-<n>` ids).
- **Render-time callbacks.** `getAriaValueText` and the Value children function run in React's render phase and may run more than once per commit under StrictMode. Ports should call them once per render and not depend on call counts.
- **Numeric serialization.** React writes `aria-valuenow={40}` as `String(40)` and a numeric `0` style value without a unit. Ports must use ECMAScript number-to-string semantics (PRG-ALG-03); .NET and Rust default float formatting differ for exponents.
- **`Intl.NumberFormat`.** Percent and currency strings come from ICU data; see [UTL](../architecture/shared-utilities.md) §4.15.2. A .NET port must match `Intl.NumberFormat` output to pass translated tests. Port: see decisions/01-hosting-model-and-timing.md §Decision item 2 (formatting is render-time state work, so C#); whether .NET formatting can match `Intl.NumberFormat` is not settled by any ADR (§16 item 10).
- **Indeterminate style.** React renders an empty style object as no `style` attribute; when the status flips from determinate to indeterminate, React removes the three declarations it previously set (and leaves consumer declarations). A port must likewise remove the built-in declarations on that transition (PRG-DOM-04).
- **NVDA span.** The trailing `x` span (PRG-DOM-01) is part of the observable DOM in all statuses.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| PRG-STR-01 | MUST | Root renders one element: children + trailing hidden span | 2 |
| PRG-STR-02 | MUST | Track/Indicator/Value/Label must be inside Root; throw otherwise | 2 |
| PRG-STR-03 | MUST | Track structural: status attributes + consumer attributes only | 2 |
| PRG-API-01 | MUST | `value` required, never stored; all outputs derived per render | 3.1 |
| PRG-API-02 | MUST | `min` default 0 | 3.1 |
| PRG-API-03 | MUST | `max` default 100 | 3.1 |
| PRG-API-04 | MUST | formatted value: clamped+format / percent / `''` indeterminate | 3.1 |
| PRG-API-05 | MUST | `locale` passed to formatter; undefined → runtime default | 3.1 |
| PRG-API-06 | MUST | `getAriaValueText(formattedValue, rawValue)` in every status | 3.1 |
| PRG-API-07 | MUST | consumer `aria-valuetext` overrides computed | 3.1 |
| PRG-API-08 | MUST | className/style/render per RND with `{ status }` | 3.1 |
| PRG-API-09 | MUST | other Root props forwarded, consumer wins | 3.1 |
| PRG-API-10 | MUST | Value children function `(formatted \| 'indeterminate', rawValue)` | 3.4 |
| PRG-API-11 | MUST | non-function Value children ignored; text or empty | 3.4 |
| PRG-API-12 | MUST | Label id = consumer id or generated; published to Root | 3.5 |
| PRG-API-13 | MUST | Track/Indicator/Value/Label forward other props | 3.5 |
| PRG-STA-01 | MUST | all parts share state `{ status }` | 3.1 |
| PRG-STA-02 | MUST | determinate iff value non-null and finite | 4 |
| PRG-STA-03 | MUST | complete iff clamped value === max, else progressing | 4 |
| PRG-STA-04 | MUST | all derived outputs agree within a render | 4 |
| PRG-STA-05 | MUST | Root tracks one label id; guarded withdrawal | 4 |
| PRG-DOM-01 | MUST | trailing `<span role=presentation>` `x`, visually hidden | 5.1 |
| PRG-DOM-02 | MUST | exactly one status data attribute on every part | 5.1 |
| PRG-DOM-03 | MUST | determinate Indicator style inset-inline-start/height/width | 5.2 |
| PRG-DOM-04 | MUST | indeterminate Indicator: no built-in style | 5.2 |
| PRG-DOM-05 | MUST | consumer Indicator style overrides built-in keys | 5.2 |
| PRG-DOM-06 | MUST | Value text = formatted value; empty when indeterminate | 5.3 |
| PRG-DOM-07 | MUST | Value `aria-hidden="true"` | 5.3 |
| PRG-DOM-08 | MUST | Label id and `role="presentation"` | 5.4 |
| PRG-DOM-09 | MUST | consumer attributes override built-in | 5.1 |
| PRG-DOM-10 | MUST | all parts pass RND conformance suite | 13 |
| PRG-ARIA-01 | MUST | `role="progressbar"` | 5.5 |
| PRG-ARIA-02 | MUST | `aria-valuenow` = clamped value; absent when indeterminate | 5.5 |
| PRG-ARIA-03 | MUST | `aria-valuemin`/`max` always present | 5.5 |
| PRG-ARIA-04 | MUST | default `aria-valuetext`: formatted / "indeterminate progress" | 5.5 |
| PRG-ARIA-05 | MUST | `aria-valuetext` from getAriaValueText in every status | 5.5 |
| PRG-ARIA-06 | MUST | `aria-labelledby` = tracked label id; absent without Label | 5.5 |
| PRG-ARIA-07 | MUST | labelledby updated post-commit, pre-paint | 5.5 |
| PRG-FOC-01 | MUST | not focusable; no tabindex | 6.3 |
| PRG-EVT-01 | MUST | getAriaValueText called on every render of the Root part | 7 |
| PRG-EVT-02 | MUST | Value children function called on every render of the Value part with current args | 7 |
| PRG-I18N-01 | MUST | default formatting: percent of position-in-range | 13 |
| PRG-I18N-02 | MUST | `format`: clamped value with given options; no lag | 13 |
| PRG-I18N-03 | MUST | `locale` applies to Value text and `aria-valuetext`; cached formatter | 13 |
| PRG-I18N-04 | MUST | no direction read; logical inset, physical width | 6.5 |
| PRG-I18N-05 | MUST | indeterminate default `aria-valuetext` is fixed English, not localized | 13 |
| PRG-EDGE-01 | MUST | missing Root context error (message) | 12 |
| PRG-EDGE-02 | MUST | min === max === value → 0%, complete | 12 |
| PRG-EDGE-03 | MUST | out-of-range clamping; ≥ max complete; raw value to callbacks | 12 |
| PRG-EDGE-04 | MUST | non-finite value ≡ null | 12 |
| PRG-EDGE-05 | MUST | inverted range not validated | 12 |
| PRG-EDGE-06 | MUST | SSR markup lacks aria-labelledby | 12 |
| PRG-ALG-01 | MUST | percentage formula, NaN → 0, clamp [0,100] | 4 |
| PRG-ALG-02 | MUST | clamped value | 4 |
| PRG-ALG-03 | MUST | Unformatted numbers in attributes/styles, serialized per UTL-DOM-37 | 4 |

## 16. Open questions

1. **Hint: Indicator "inline-size".** The Indicator uses physical `width` with logical `inset-inline-start` (`progress/indicator/ProgressIndicator.tsx:26-30`); there is no `inline-size`, and `inset-inline-start` is inert unless the consumer positions the element. PRG-I18N-04 records the literal behavior.
2. **`"indeterminate progress"` is hard-coded English** (`progress/root/ProgressRoot.tsx:47`) and is not affected by `locale`. Consumers must use `getAriaValueText` to localize it. PRG-I18N-05 states this; confirm the port should keep the literal.
3. **`Value` children first argument.** The declared type is `string | null` (`progress/value/ProgressValue.tsx:54`) but the implementation never passes `null`: it passes the literal `'indeterminate'` (`:25`), while `getAriaValueText` receives `''` for the same status (`ProgressRoot.tsx:45,70`). The two sentinels differ; documented as implemented (PRG-API-10, PRG-API-06).
4. **Complete when `min === max`.** With `min === max === value` the status is `'complete'` but the indicator width is `0%` (`ProgressRoot.tsx:51-53`); the test "normalizes aria attributes when min equals max" asserts the width but not the status. PRG-EDGE-02 states the implemented combination.
5. **Inverted range.** `min > max` is not validated (`ProgressRoot.tsx:50-53`); `complete` is then unreachable. A port may prefer to warn.
6. **`format` with `style: 'percent'`.** `format` is applied to the clamped value, so `{ style: 'percent' }` with value `30` renders `"3,000%"` (`ProgressRoot.tsx:56-57`). No warning.
7. **Consumer `aria-valuetext` vs `getAriaValueText`.** When both are supplied, `getAriaValueText` is still invoked but its result is overridden (`ProgressRoot.tsx:69-71,97`).
8. **Empty-string Label id / multiple Labels.** As in Meter: `id=""` is published verbatim, producing `aria-labelledby=""`; with two Labels the most recently registered wins (FFI-ARIA-08). Neither is tested.
9. **Outline vs source.** The API outline lists Root data attributes for every part and matches the source; it omits Label's `id` (a native attribute that Label consumes and re-publishes). No other discrepancy found.
10. **Locale formatting parity.** The determinate formatted value is `Intl.NumberFormat` output from the host's ICU data (`packages/utils/src/formatNumber.ts:13,27`; `progress/root/ProgressRoot.tsx:56-58`) and the tests compare against `Intl.NumberFormat`/`toLocaleString` in the same runtime (`progress/root/ProgressRoot.test.tsx:123,190,336`). ADR 01 Decision item 2 places render-time work in C#, but no ADR decides whether C# number formatting (which differs from ECMA-402 in percent spacing, currency patterns and rounding options) must match `Intl.NumberFormat` byte for byte, or whether the port may call the browser's `Intl` (impossible synchronously during a Server render). Needs a decision before PRG-I18N-01..03 and PRG-ARIA-04 can be tested. Decided (2026-10-18): .NET globalization; output equivalent, not byte-identical (ADR 01 Decision 2, amended).
