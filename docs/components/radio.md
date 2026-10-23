# Radio

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/radio/` |
| Tests | `packages/react/src/radio/**/*.test.tsx` (2 files) |
| Docs page | `docs/src/app/(docs)/react/components/radio-group/page.mdx` (Radio is documented on the Radio Group page) |
| Prefix | `RAD` |
| Status | implemented |
| Revised | 2026-10-23 |
| Builds on | none (items of [radio-group](radio-group.md)) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

`Radio.Root` is one option of a `RadioGroup` (WAI-ARIA `radio`): a visible `<span role="radio">` that the user activates with a pointer, <kbd>Space</kbd>, an associated `<label>`, or by arrow-key focus from a sibling radio, paired with a visually hidden native `<input type="radio">` rendered as its next sibling. The hidden input is the only form participant and the only source of change detection: every activation is routed through a click on that input, the browser checks it, and the radio asks its group to adopt the radio's `value` (any JavaScript value, compared by identity). The radio owns no selection state; `RadioGroup` owns the value, reports changes and configures the composite that gives the radios their roving tab stop ([radio-group](radio-group.md)).

`Radio.Indicator` renders only while its radio is checked (or always with `keepMounted`) and carries the transition-status attributes so enter/exit animations can be styled. A `Radio.Root` rendered outside a group is static: it is checked exactly when its `value` is `""`, and user interaction never changes it (RAD-STA-02). Unlike Switch, the radio emits no `aria-required`/`aria-readonly` of its own and no callbacks; unlike Checkbox there is no indeterminate state and no `uncheckedValue`.

## 2. Anatomy

```
<Radio.Root>                 required, 1          default element: span  (+ hidden <input type="radio"> next sibling)
  <Radio.Indicator>          optional, 0..n       default element: span  (mounted while checked unless keepMounted)
```

- **RAD-STR-01** — MUST. `Radio.Root` MUST render, in document order, the root element (default `<span>`) and then always an `<input type="radio">` as its next sibling; the input is never a descendant of the root element.
  Source: `radio/root/RadioRoot.tsx:260-278`. Test: [L2] `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`" (`nextElementSibling` is the input); `RadioGroup.test.tsx` "should set the name attribute on each radio input".
- **RAD-STR-02** — MUST. `Radio.Indicator` MUST be rendered inside the subtree of a `Radio.Root`; elsewhere it MUST throw the error of RAD-EDGE-01. Any number of Indicators may exist; each mirrors the root's state.
  Source: `radio/root/RadioRootContext.ts:9-17`, `radio/indicator/RadioIndicator.tsx:24`. Test: none (conformance renders it inside a root).
- **RAD-STR-03** — MUST. Inside a `RadioGroup` the root element MUST be a composite item of the group (CMP §6.4: `tag: 'span'`, no metadata); outside a group it MUST be rendered as a plain element with no composite registration.
  Source: `radio/root/RadioRoot.tsx:239,252-275`. Test: [L2] `RadioGroup.test.tsx` "sets tabIndex=0 to the correct element initially"; standalone: `RadioRoot.test.tsx` conformance suite.
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22: the `composite` module owns the roving `tabindex` from attach; ADR 13 §3): the cited title reads the tab stop after the composite's first publication, which only a browser observes, so it runs in `radio-group.spec.ts`; the registration itself is asserted at L2 by `RadioRootTests` "port: inside a group the radio is a composite item whose tabindex the composite writes".
- **RAD-STR-04** — MUST. `Radio.Indicator` is structural: it MUST render an element with the root's state attributes, transition attributes and forwarded attributes, and MUST NOT attach handlers, ARIA attributes or an `id`.
  Source: `radio/indicator/RadioIndicator.tsx:39-44`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents" (L4 for the transition-attribute clause).

If Indicator is omitted the radio functions fully; it exists only as a styling and animation hook.

## 3. Public API

### 3.1 `Radio.Root`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value` (any type; required) | — | Identifies the radio within its group; compared to the group value by identity; serialized onto the hidden input. | RAD-API-01 |
| `disabled` | `boolean` | `false` | OR-ed with `Field.Root`, `Field.Item` and group disabled. | RAD-API-02 |
| `readOnly` | `boolean` | `false` | OR-ed with the group's `readOnly`; user cannot select it. | RAD-API-03 |
| `required` | `boolean` | `false` | OR-ed with the group's `required`; sets `required` on the hidden input. | RAD-API-04 |
| `nativeButton` | `boolean` | `false` | Declare that the substituted root element is a native `<button>`. | RAD-API-05 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the hidden input. | RAD-API-06 |
| `id` | `string` | generated | `id` of the hidden input; applied to the root instead when `nativeButton`. Not in the API outline (native attribute). | RAD-API-07 |
| `aria-labelledby`, `aria-label` | `string` | — | Consumed to compute the root's accessible name (§5.3); `aria-label` is also forwarded. | RAD-ARIA-03 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | RAD-API-08 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the root element (never to the hidden input). | RAD-API-09 |

**Consumed (not forwarded to the element):** `value`, `disabled`, `readOnly`, `required`, `nativeButton`, `inputRef`, `id`, `aria-labelledby`, `className`, `style`, `render`. There are no `name`, `form`, `checked` or change-callback props: `name`/`form` come from the group (RDG-API-07/08).

- **RAD-API-01** — MUST. `value` MUST be accepted as any type including `null`; it MUST be forwarded to the hidden input only, as `serializeValue(value)` (RAD-DOM-11), and MUST NOT appear as an attribute on the root element. A radio whose `value` is `undefined` never selects (RAD-EVT-03).
  Source: `radio/root/RadioRoot.tsx:46,191,202,322-327`, `radio/root/RadioRoot.spec.tsx:5-12`. Test: [L2] `RadioRoot.test.tsx` "does not forward `value` prop", "allows `null` value".
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22, any-typed values; RAD §3.3): `RadioRoot<TValue>` takes `Value` of its group's `TValue`, and `null` stands for Base UI's `undefined` radio (RAD-EVT-03, RAD-DOM-11), so "allows `null` value" is restated as the port title "port: a radio whose value is null is never selected (allows `null` value)". Design point R7 of M2 tracks 7–8 (pending the user): Razor infers a radio's `TValue` from its own `Value` before the group's cascading type parameter, so `<RadioRoot Value="1" />` in a `RadioGroup<int?>` is a `RadioRoot<int>`; such a radio throws ``Blazix: A `RadioRoot` must have the `TValue` of its `RadioGroup`. Set `TValue` on the radio button, or give `Value` the group's type, for example `Value="(int?)1"` in a `RadioGroup` of `int?`.`` in every build instead of rendering as a standalone radio (`RadioGroupTypeTests` "port: a radio whose TValue differs from its group's throws a descriptive error").
- **RAD-API-02** — MUST. The effective disabled state MUST be `fieldDisabled || fieldItemDisabled || groupDisabled || disabled` (each term `false` when the corresponding ancestor is absent; FFI-STA-11); the prop defaults to `false`.
  Source: `radio/root/RadioRoot.tsx:42,79`, `field/item/FieldItem.tsx:31-34`. Test: [L2] `RadioGroup.test.tsx` "should have the `aria-disabled` attribute" (group), "should receive disabled prop from Field.Root", "is not acquired when a radio inherits disabled from Field.Item"; `RadioRoot.test.tsx` "uses aria-disabled instead of HTML disabled" (own).
- **RAD-API-03** — MUST. Effective `readOnly` MUST be `groupReadOnly || readOnly` (default `false`); when `true` user interaction MUST NOT select the radio (RAD-PTR-07, RAD-KEY-06) while focus and form behavior are unchanged; the hidden input MUST carry the `readonly` attribute (RAD-DOM-09). No `aria-readonly` is emitted on the root (RAD-ARIA-09).
  Source: `radio/root/RadioRoot.tsx:43,80,143,159,195,202`. Test: [L2] `RadioGroup.test.tsx` "should not change its state when clicked" (readOnly); `RadioRoot.test.tsx` "acquires the field focused state on focus" (readOnly radio still focusable) (L4 for the interaction clause).
- **RAD-API-04** — MUST. Effective `required` MUST be `groupRequired || required` (default `false`) and MUST be reflected only as the hidden input's `required` attribute (no `aria-required` on the root, RAD-ARIA-09).
  Source: `radio/root/RadioRoot.tsx:44,81,194`. Test: [L2] `RadioGroup.test.tsx` "triggers native HTML validation on submit" (group-level), "should place the style hooks on the root and subcomponents" (`data-required`).
- **RAD-API-05** — MUST. `nativeButton` defaults to `false` and selects the native vs non-native mode of [UBT](../architecture/button-behavior.md) (`type="button"` vs `role="button"` — the latter overridden by `role="radio"` —, `disabled` attribute vs `aria-disabled`, keyboard activation) and the `id`/label wiring of RAD-API-07 and §5.3.
  Source: `radio/root/RadioRoot.tsx:48,119,124,134,176-180`. Test: [L2] `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`", "propagates a single click event to ancestors per user click (nativeButton=%s)" (`true` variant) (L4 for the keyboard-activation clause).
- **RAD-API-06** — MUST. `inputRef` MUST be attached to the hidden `<input type="radio">`. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): the hidden input stays internal and no public handle to it exists.
  Source: `radio/root/RadioRoot.tsx:47,96,184`. Test: none directly (`RadioGroup.test.tsx` inputRef tests observe the same elements through the group).
- **RAD-API-07** — MUST. When `nativeButton` is `false`, the `id` prop MUST become the hidden input's id (the labelable control id; generated when absent) and the root MUST receive a separate generated id. When `nativeButton` is `true`, the `id` prop (or the control id) MUST be applied to the root and the hidden input MUST have no `id`.
  Source: `radio/root/RadioRoot.tsx:117-119,134,186`; see FFI-DOM-04. Test: [L2] `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`", "sets `aria-labelledby` from a sibling label associated with the hidden input"; `RadioGroup.test.tsx` "associates explicitly" (native `<label htmlFor>`).
- **RAD-API-08** — MUST. `className`, `style` and `render` MUST behave per RND with the state object below; none is forwarded as a plain attribute. Inside a group the substituted element is also the composite item element.
  Source: `radio/root/RadioRoot.tsx:40-41,50,252-272`. Test: [L2] `RadioRoot.test.tsx` conformance suite "Base UI component API".
- **RAD-API-09** — MUST. Every other prop MUST be forwarded to the root element (merged after the built-in root attributes, before the button-behavior, description and validation attribute getters), never to the hidden input; consumer handlers run before the built-in handlers for the same event (RND-EVT-02).
  Source: `radio/root/RadioRoot.tsx:51,242-250`. Test: [L2] `RadioRoot.test.tsx` "sets checked and unchecked data attributes" (`data-testid` forwarded), "does not propagate to ancestors when stopPropagation() is called (nativeButton=%s)".

#### State object

The enclosing field's state (all `false`/`null` outside a field) plus four radio fields.

| Field | Type | Meaning |
|---|---|---|
| `checked` | `boolean` | RAD-STA-01. |
| `disabled` | `boolean` | Effective disabled (RAD-API-02). |
| `readOnly` | `boolean` | Effective readOnly (RAD-API-03). |
| `required` | `boolean` | Effective required (RAD-API-04). |
| `touched` | `boolean` | Field: a radio of the field was selected or focus left the group. |
| `dirty` | `boolean` | Field: group value differs from the initial value. |
| `valid` | `boolean \| null` | Field validity; `null` until validated or while disabled. |
| `filled` | `boolean` | Field: a non-null value is selected. |
| `focused` | `boolean` | Field: a radio of the field owns focus. |

- **RAD-STA-03** — MUST. The state object MUST contain exactly the nine fields above, with the four radio fields being the *effective* values (not the raw props).
  Source: `radio/root/RadioRoot.tsx:226-235,283-320`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-checked` / `data-unchecked` | `""`, exactly one, matching `checked` | RAD-DOM-03 |
| `data-disabled`, `data-readonly`, `data-required` | `""` while the effective flag is `true` | RAD-DOM-04 |
| `data-touched`, `data-dirty`, `data-filled`, `data-focused` | `""` while the field flag is `true` (Field only) | RAD-DOM-05 |
| `data-valid` / `data-invalid` | `""` while field `valid` is `true` / `false` | RAD-DOM-06 |
| `data-composite-item-active` | `""` while `checked` (composite marker, CMP-DOM-02) | RAD-DOM-02 |

`data-composite-item-active` is not listed in `RadioRootDataAttributes.ts` or the outline (see §16 item 3).

#### CSS variables

None.

### 3.2 `Radio.Indicator`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while the radio is unchecked. | RAD-API-10 |
| `className`, `style`, `render` | see RND | — | Functions receive the Indicator state object. | RAD-API-11 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the indicator element. | RAD-API-11 |

- **RAD-API-10** — MUST. `keepMounted` defaults to `false`; its mounting semantics are RAD-ANI-01/04.
  Source: `radio/indicator/RadioIndicator.tsx:22,37,58-60`. Test: [L2] `RadioGroup.test.tsx` "should apply data-checked and data-unchecked to radio root and indicator" (`keepMounted` indicators present while unchecked).
- **RAD-API-11** — MUST. All Indicator props other than `keepMounted`, `className`, `style`, `render` MUST be forwarded to the indicator element; the three customization props behave per RND with the Indicator state object.
  Source: `radio/indicator/RadioIndicator.tsx:22,39-44`. Test: [L2] `RadioIndicator.test.tsx` conformance suite; "should remove the indicator when the animation finishes" (`onAnimationEnd` forwarded).

#### State object

The root's state object (RAD-STA-04) plus:

| Field | Type | Meaning |
|---|---|---|
| `transitionStatus` | `'starting' \| 'ending' \| undefined` | TRN-STA-01 (no idle state). |

#### Data attributes

Those of the root (same mapping, RAD-DOM-15) plus `data-starting-style` / `data-ending-style` (RAD-ANI-02). Never `data-composite-item-active` (the Indicator state has no such field — the marker is a root attribute, not a state field).

#### CSS variables

None.

### 3.3 Shared types

- `Radio.Root.Props<Value = any>`, `Radio.Root.State`, `Radio.Indicator.Props`, `Radio.Indicator.State` — the tables above. `Radio.Indicator.State` extends `Radio.Root.State` with `transitionStatus` (`radio/indicator/RadioIndicator.tsx:73-78`). The `Value` type parameter MUST accept an explicit argument that constrains `value` (`radio/root/RadioRoot.spec.tsx:9-12`).
- Reason/details types belong to the group: `RadioGroup.ChangeEventReason` = `'none'`, `RadioGroup.ChangeEventDetails` (RDG §3.2). Radio exports none.
- Exported constant namespaces `RadioRootDataAttributes` (`checked`, `unchecked`, `disabled`, `readonly`, `required`, `valid`, `invalid`, `touched`, `dirty`, `filled`, `focused`) and `RadioIndicatorDataAttributes` (the same plus `startingStyle`, `endingStyle`). Source: `radio/root/RadioRootDataAttributes.ts`, `radio/indicator/RadioIndicatorDataAttributes.ts`, `radio/index.ts:6-7`.

## 4. State model

The radio owns no selection state. Its state object is derived from the group value, the effective props and the field.

- effective disabled / readOnly / required per RAD-API-02..04; `form`, `name` from the group.
- `checked` per RAD-STA-01; `aria-checked`, `data-checked`/`data-unchecked`, the active marker and the hidden input's `checked` property are all equal to it.
- Indicator `mounted` = `checked`, kept `true` through the exit animation (§9).

- **RAD-STA-01** — MUST. Inside a group, `checked` MUST be `groupValue === value` (strict identity: objects by reference, `NaN` never checked, `null === null` checked). Outside a group, `checked` MUST be `value === ''`.
  Source: `radio/root/RadioRoot.tsx:84`. Test: [L2] `RadioRoot.test.tsx` "sets checked and unchecked data attributes", "allows `null` value"; standalone: `RadioIndicator.test.tsx` conformance suite (`<Radio.Root value="">` renders a checked radio whose Indicator mounts).
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22: `null` means nothing selected): the `null === null` clause is not ported. A group value of `null` checks no radio and a radio whose value is `null` is never checked (RDG-EDGE-03's note); otherwise identity holds as stated (`Equality.StrictEquals<TValue>`: references by identity, strings ordinal, `NaN` never, `±0` equal). The clause is asserted by `RadioGroupTests` "port: identity decides which radio is checked: references by identity, strings by value, NaN never" and "port: a null value selects no radio"; the standalone `''` rule is implemented as written (§16 Q1).
- **RAD-STA-02** — MUST. Outside a group, user interaction MUST NOT change `checked` (the group setter is a no-op and the hidden input is re-synchronized to `checked`), no callback exists, and the only side effect of a non-canceled activation is the field's `touched` flag (RAD-FRM-05). Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:65-67,193,208,214`. Test: none (standalone tests only cover focus).
  Port note 2026-10-23: asserted by `RadioRootTests` "port: a standalone radio is checked when its value is the empty string, and a click never changes it", "port: a standalone radio's activation only touches its field" and `radio.spec.ts` "port: a standalone radio whose value is the empty string is checked, and a click keeps it checked". The input's re-synchronization is the `dom` primitive's restore (RAD-DOM-09's note), so the row joins the hidden-input veto row of ADR 01's relaxation table (2026-10-22), §9.
- **RAD-STA-04** — MUST. `Radio.Indicator` MUST expose exactly the root's state object plus `transitionStatus`, with the same values in the same render. Server: relaxed, see §9.
  Source: `radio/indicator/RadioIndicator.tsx:24-33`, `radio/root/RadioRoot.tsx:237,261`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents", "should apply data-checked and data-unchecked to radio root and indicator" (L4 for the `transitionStatus` clause).
- **RAD-STA-05** — MUST. Within a `Field.Root`, when a radio mounts with its hidden input checked it MUST set the field's `filled` to `true` once, post-commit, pre-paint; a radio never sets `filled` to `false` (the group does, RDG-STA-05). Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:98-102`. Test: none directly (`RadioGroup.test.tsx` "does not change state when canceled via a root click" asserts the negative).

## 5. Rendered DOM and ARIA

### 5.1 Root element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | generated `base-ui-…` when `nativeButton=false`; control id (`id` prop, else `Field.Item`/`Field.Root` scope id, else generated) when `true` | RAD-DOM-01 | C# |
| `role` | `"radio"` (consumer may override) | RAD-ARIA-01 | C# |
| `aria-checked` | `"true"` / `"false"` | RAD-ARIA-02 | C# |
| `aria-labelledby` | §5.3 | RAD-ARIA-03..05 | C# |
| `aria-disabled` | `"true"` only when disabled and `nativeButton=false` | RAD-ARIA-06 | C# |
| `disabled` | present only when disabled and `nativeButton=true` | RAD-ARIA-06 | C# |
| `aria-describedby` | consumer tokens + field message ids | RAD-ARIA-07 | C# |
| `aria-invalid` | `"true"` while the field is invalid, inside a group only | RAD-ARIA-08 | C# |
| `aria-required`, `aria-readonly` | never | RAD-ARIA-09 | C# |
| `tabindex` | in a group: roving `0`/`-1`; standalone non-native: `0` (`-1` when disabled); native: absent | RAD-FOC-01 | C# (standalone); C# → JS (in a group, from attach) |
| `type` | `"button"` when `nativeButton=true` | RAD-DOM-07 | C# |
| `data-composite-item-active` | `""` while checked | RAD-DOM-02 | C# |
| `data-*` | §3.1 table | RAD-DOM-03..06 | C# |

- **RAD-DOM-01** — MUST. With `nativeButton=false` the root MUST carry an auto-generated id (never the `id` prop); with `nativeButton=true` it MUST carry the labelable control id (the `id` prop, else the id supplied by the enclosing `Field.Item`/`Field.Root` scope, else generated).
  Source: `radio/root/RadioRoot.tsx:117-118,134`, `internals/labelable-provider/useLabelableId.ts:82`. Test: [L2] `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`".
- **RAD-DOM-02** — MUST. The root MUST carry `data-composite-item-active=""` exactly while `checked`, inside and outside a group; this is the marker the composite reads for the initial tab stop (CMP-STA-23) and the removal fallback (CMP-STA-27).
  Source: `radio/root/RadioRoot.tsx:133`, `internals/composite/constants.ts:1`. Test: [L2] `RadioGroup.test.tsx` "sets tabIndex=0 to the correct element initially", "moves the tab stop to the checked radio when the highlighted radio is removed".
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22; ADR 13 §3): the marker is C#'s and is asserted at L2 by `RadioRootTests` "port: inside a group the radio is a composite item whose tabindex the composite writes" and "port: a standalone radio is checked when its value is the empty string, and a click never changes it"; the cited titles read the tab stop the composite derives from it, which only a browser observes, and run in `radio-group.spec.ts`.
- **RAD-DOM-03** — MUST. Exactly one of `data-checked` / `data-unchecked` MUST be present (value `""`) at all times, matching `checked`, updated in the same render as a selection change.
  Source: `radio/utils/stateAttributesMapping.ts:8-13`. Test: [L2] `RadioRoot.test.tsx` "sets checked and unchecked data attributes"; `RadioGroup.test.tsx` "should apply data-checked and data-unchecked to radio root and indicator".
- **RAD-DOM-04** — MUST. `data-disabled`, `data-readonly`, `data-required` MUST be present (value `""`) exactly while the effective flag is `true` (including flags inherited from the group, field or item).
  Source: `radio/root/RadioRoot.tsx:79-81,226-235`, `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents", "should have the `aria-disabled` attribute" (`data-disabled` from group).
- **RAD-DOM-05** — MUST. `data-touched`, `data-dirty`, `data-filled`, `data-focused` MUST be present exactly while the corresponding field flag is `true`; outside a `Field.Root` never.
  Source: `radio/root/RadioRoot.tsx:228`, `internals/field-root-context/FieldRootContext.ts:37-67`; see FFI-DOM-01. Test: none on the radio element (field-level assertions in `RadioGroup.test.tsx` "[data-focused] without a blur event").
- **RAD-DOM-06** — MUST. `data-valid` / `data-invalid` MUST follow field `valid` (`true` / `false`; neither when `null`).
  Source: `radio/utils/stateAttributesMapping.ts:15`, `internals/field-constants/constants.ts:34-44`. Test: none.
- **RAD-DOM-07** — MUST. When `nativeButton` is `true` the root MUST receive `type="button"`; when `false` no `type` and no `role="button"` survive (the button-behavior `role` is replaced by `role="radio"`, which is merged later).
  Source: `internals/use-button/useButton.ts:226-228`, `radio/root/RadioRoot.tsx:130,242-250` (`rootProps` are part of the external props the button getter re-merges last). Test: [L2] `RadioRoot.test.tsx` every `getByRole('radio')` query with `nativeButton`.
- **RAD-DOM-16** — MUST. Consumer attributes on `Radio.Root` MUST take precedence over the built-in root attributes of the same name (including `tabindex` inside a group), except `className`/`style` (merged per RND), `aria-describedby` (composed, RAD-ARIA-07) and handlers (chained consumer-first).
  Source: `radio/root/RadioRoot.tsx:242-250`, `internals/use-button/useButton.ts:102-229`. Test: none (group-level equivalent: `RadioGroup.test.tsx` "can override the built-in attributes").

### 5.2 Hidden `<input>`

Rendered always, as the root's next sibling.

| Attribute / property | Value | Req | Writer |
|---|---|---|---|
| `type` | `"radio"` | RAD-DOM-08 | C# |
| `aria-hidden` | `"true"` | RAD-DOM-08 | C# |
| `tabindex` | `-1` | RAD-DOM-08 | C# |
| `checked` (property) | `checked` | RAD-DOM-09 | C# → JS |
| `disabled` | effective disabled | RAD-DOM-09 | C# |
| `required` | effective required | RAD-DOM-09 | C# |
| `readonly` | effective readOnly | RAD-DOM-09 | C# |
| `name` | group effective name (absent outside a group / when none) | RAD-DOM-09 | C# |
| `form` | group `form` prop | RAD-DOM-09 | C# |
| `id` | control id when `nativeButton=false`; absent when `true` | RAD-DOM-10 | C# |
| `value` | `serializeValue(value)`; attribute absent when `value` is `undefined` | RAD-DOM-11 | C# |
| `style` | visually-hidden declarations (two variants) | RAD-DOM-12 | C# |
| `aria-describedby`, `aria-invalid` | never (unlike Switch) | RAD-DOM-13 | C# |

- **RAD-DOM-08** — MUST. The hidden input MUST be `<input type="radio">` with `aria-hidden="true"` and `tabindex="-1"`.
  Source: `radio/root/RadioRoot.tsx:183,188,190`. Test: [L2] `RadioGroup.test.tsx` `document.querySelector('input[type="radio"]')` queries.
- **RAD-DOM-09** — MUST. The hidden input's `checked` property MUST always equal `checked`, and its `disabled`, `required`, `readonly`, `name` and `form` attributes MUST mirror the effective disabled, effective required, effective readOnly, the group's effective name and the group's `form`.
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22, the ownership row of the hidden input's `checked`; ADR 13 §3): the `Writer` cell of `checked` is `C# → JS`, as SWT-DOM-08's. C# renders the initial `checked` once, in prerender and the first interactive render; from attach the `dom` primitive owns the property, sets it on every `update` and restores it when C# raises `DomOptions.CheckedRestores` after a refused request (a `Cancel()`, a controlled consumer that does not pass the value back, a standalone radio, a `null` value). It is re-asserted one task after the owning form's `reset` (§16 Q4).
  Source: `radio/root/RadioRoot.tsx:185,187,192-195`. Test: [L2] `RadioGroup.test.tsx` "should have the `aria-disabled` attribute" (`disabled` on input), "should set the name attribute on each radio input", "does not change state when canceled via a root click" (`input.checked` restored), "triggers native HTML validation on submit" (`required`).
- **RAD-DOM-10** — MUST. With `nativeButton=false` the hidden input MUST carry the labelable control id (`id` prop; else the enclosing `Field.Item`/`Field.Root` scope's control id; else generated) — never absent; with `nativeButton=true` it MUST have no `id`.
  Source: `radio/root/RadioRoot.tsx:118-119,186`; see FFI-DOM-04, FFI-FRM-36. Test: [L2] `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`", "sets `aria-labelledby` from a sibling label associated with the hidden input"; `RadioGroup.test.tsx` "associates explicitly" (Field.Label `for` equals the input id).
  Port note 2026-10-23 (design point R6 of M2 tracks 7–8, pending the user; the CheckboxGroup precedent of CKG-ARIA-03/07): a radio that shares its group's labelable scope (placed directly in a `Field.Root`, no `Field.Item` of its own) claims no control id and takes no label from the scope; its input gets the radio's generated id, and the group claims the scope's control id as `null`. Base UI's radios would all render the field's control id, and the port reaches a validation input by its element id (RDG-FRM-06..08, RDG-FOC-07). A radio in its own `Field.Item` is unchanged (`RadioGroupFieldTests` "port: the focus target is the first eligible, natively invalid radio's root, else the first eligible one", "clears required validation when a value is selected").
- **RAD-DOM-11** — MUST. The hidden input MUST carry `value = serializeValue(value)` (RAD-FRM-02) whenever the `value` prop is not `undefined`, and no `value` attribute otherwise.
  Source: `radio/root/RadioRoot.tsx:191`, `internals/serializeValue.ts:1-13`. Test: [L2] `RadioGroup.test.tsx` "should set the name attribute on each radio input" (`value="a"`); `RadioRootTests` "port: a radio whose value is null is never selected (allows `null` value)" (no `value` attribute).
  Port note 2026-10-23 (ADR 02 Amendments 2026-10-22 item 1; conventions §3.5): "transfers the current input when the object inputRef changes" is re-cited, since `inputRef` is withdrawn (RAD-API-06); a `null` value renders no attribute (ADR 08 Amendments 2026-10-22).
- **RAD-DOM-12** — MUST. The hidden input MUST be visually hidden by inline style: with a group name, `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute`; without a name, the same with `position: fixed; top: 0; left: 0; margin: 0` (FFI-DOM-02, UTL-DOM-13/14).
  Source: `radio/root/RadioRoot.tsx:189`, `packages/utils/src/visuallyHidden.ts:3-25`. Test: none.
- **RAD-DOM-13** — MUST. The hidden input MUST NOT receive `aria-describedby` or `aria-invalid`; validation attributes are applied to the root only.
  Source: `radio/root/RadioRoot.tsx:182-224` (no validation props on the input). Test: none.
- **RAD-DOM-14** — MUST. `name`, `value`, `form`, `required`, `readOnly` and `disabled` MUST NOT appear as attributes on the root element from the component (the root reflects `disabled` only through `aria-disabled`/`disabled` per RAD-ARIA-06).
  Source: `radio/root/RadioRoot.tsx:39-52,129-134`. Test: [L2] `RadioRoot.test.tsx` "does not forward `value` prop", "uses aria-disabled instead of HTML disabled".

### 5.3 Indicator element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-checked` / `data-unchecked`, `data-*` | the root's (§3.1 table) | RAD-DOM-15 | C# |
| `data-starting-style` | `transitionStatus === 'starting'` | RAD-ANI-02 | JS |
| `data-ending-style` | `transitionStatus === 'ending'` | RAD-ANI-02 | JS |

Table and `Writer` cells added 2026-10-23 (conventions §3.4, ADR 04 §2 and Amendments 2026-10-18 and 2026-10-21, ADR 13 §3): RAD §3.2 states the two transition attributes in prose only, and the ownership lint needs a row for the mirrored field. The `transitions` module writes both from attach; C# mirrors the status into `RadioIndicatorState.TransitionStatus` (RAD-STA-04) and renders `data-starting-style` once on an element it creates checked (the handover, ADR 04 Amendments 2026-10-18).

- **RAD-DOM-15** — MUST. The Indicator element MUST carry the root's data attributes (RAD-DOM-03..06, same mapping and values, same render) plus the transition attributes of RAD-ANI-02; it MUST NOT receive `hidden`, `inert` or inline styles when kept mounted while unchecked (TRN-DOM-07).
  Source: `radio/indicator/RadioIndicator.tsx:30-33,39-44`, `radio/utils/stateAttributesMapping.ts:7-20`. Test: [L2] `RadioGroup.test.tsx` "should place the style hooks on the root and subcomponents", "should apply data-checked and data-unchecked to radio root and indicator" (L4 for the transition-attribute clause). Docs: CHANGELOG v1.0.0-alpha.5 "Avoid applying `hidden` attr when `keepMounted=true` for indicators".

### 5.4 ARIA

- **RAD-ARIA-01** — MUST. The root MUST have `role="radio"` unless the consumer supplies `role`.
  Source: `radio/root/RadioRoot.tsx:130`. Test: [L2] `RadioRoot.test.tsx` every `getByRole('radio')` query.
- **RAD-ARIA-02** — MUST. The root MUST have `aria-checked` equal to the string form of `checked`, updated in the same render as the group value.
  Source: `radio/root/RadioRoot.tsx:131`. Test: [L2] `RadioRoot.test.tsx` "allows `null` value"; `RadioGroup.test.tsx` "should update its state if the underlying input is toggled".
- **RAD-ARIA-03** — MUST. `aria-labelledby` MUST resolve in this precedence (FFI-ARIA-05): (1) the consumer's `aria-labelledby`; else (2) if the consumer's `aria-label` is non-blank after trimming, no `aria-labelledby` at all, even with a `Field.Label`; else (3) the id of the `Field.Label` in the nearest labelable scope (`Field.Item` if present, else `Field.Root`); else (4) the fallback of RAD-ARIA-04; else absent.
  Source: `radio/root/RadioRoot.tsx:45,77,120-127,132`, `internals/labelable-provider/useAriaLabelledBy.ts:14-20`. Test: [L2] `RadioRoot.test.tsx` "prefers `aria-label` over an associated label", "sets `aria-labelledby` from a sibling label associated with the hidden input" (L4 for the native-label fallback clause). Docs: CHANGELOG v1.9.0 "Prefer `aria-label` over associated labels".
- **RAD-ARIA-04** — MUST. Fallback (`nativeButton=false` only): a native `<label>` associated with the hidden input is looked up as the input's parent if it is a `<label>`, else (when the input has an id) its next element sibling if that is a `<label>` whose `htmlFor` equals the input id, else the first entry of the input's `labels`; a found label without an id MUST be assigned `"<hidden input id>-label"` (FFI-ARIA-06), and its id becomes the root's `aria-labelledby`. The fallback MUST be re-evaluated after every update of the radio so that id changes and label mount/unmount are reflected.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:16,26-35,40-74`. Test: [L4] `RadioRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input", "updates fallback `aria-labelledby` when the hidden input id changes". Docs: CHANGELOG v1.3.0 "Add automatic `aria-labelledby` support".
- **RAD-ARIA-05** — MUST. When `nativeButton` is `true` the fallback of RAD-ARIA-04 MUST be disabled (steps 1–3 still apply); a `<label htmlFor>` then names the `<button>` natively.
  Source: `radio/root/RadioRoot.tsx:124`, `internals/labelable-provider/useAriaLabelledBy.ts:28-30`. Test: [L2] `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`" (implied).
- **RAD-ARIA-06** — MUST. When disabled and `nativeButton=false` the root MUST carry `aria-disabled="true"` and no `disabled` attribute; when disabled and `nativeButton=true` it MUST carry the `disabled` attribute and no `aria-disabled`; when enabled, neither (UBT-ARIA-02, UBT-DOM-06/08; `composite: false` is passed so the composite-mode attribute rules do not apply).
  Source: `radio/root/RadioRoot.tsx:176-180`, `utils/useFocusableWhenDisabled.ts:38-47`. Test: [L2] `RadioRoot.test.tsx` "uses aria-disabled instead of HTML disabled"; `RadioGroup.test.tsx` "should have the `aria-disabled` attribute". Docs: CHANGELOG v1.7.0 "Remove unnecessary ARIA attributes from `<Radio.Root>`".
- **RAD-ARIA-07** — MUST. The root's `aria-describedby` MUST be the consumer's tokens, then the ids of `Field.Description`/`Field.Error` in the `Field.Root` scope, then those in the enclosing `Field.Item` scope, de-duplicated and space-joined; absent when empty (FFI-ARIA-01, FFI-STR-03). This applies inside and outside a group.
  Source: `radio/root/RadioRoot.tsx:77,246-249`, `internals/labelable-provider/LabelableProvider.tsx:68-81`, `field/root/useFieldValidation.ts:367-376`. Test: [L2] `RadioGroup.test.tsx` "links the group and individual radios" (`radio-description <groupDescriptionId>`), "associates explicitly" (Field.Label; per-item description), "appends the id attribute of the error to aria-describedby of individual radios".
- **RAD-ARIA-08** — MUST. Inside a group within a `Field.Root`, the root MUST carry `aria-invalid="true"` exactly while the field's `valid` is `false` and neither the field nor this radio is disabled (FFI-ARIA-03); a standalone radio never carries `aria-invalid` (it has no access to the group's validation).
  Source: `radio/root/RadioRoot.tsx:63,247-249`. Test: [L2] `RadioGroup.test.tsx` "clears required validation when a value is selected" (both radios).
- **RAD-ARIA-09** — MUST. The root MUST NOT carry `aria-required` or `aria-readonly`; the group carries them (RDG-ARIA-02/04).
  Source: `radio/root/RadioRoot.tsx:129-134`. Test: none. Docs: CHANGELOG v1.7.0 "Remove unnecessary ARIA attributes from `<Radio.Root>`".

## 6. Interactions

All selections funnel through one path: a `click` is delivered to the hidden input, the browser checks it, the input's change handler asks the group to adopt `value` (§7), and the group's state update re-renders every radio. The root's click handler is the bridge from the visible element to the input; the focus handler is the bridge from arrow-key navigation to the input.

### 6.1 Keyboard

The radio passes `composite: false` to button-behavior, so inside a group it still uses the *non-composite* activation rules (Space on keyup) while the composite root handles arrow keys (RDG §6.1).

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Space</kbd> | root focused, `nativeButton=false`, enabled | keydown: default prevented (no scroll), nothing else; keyup (if its default was not prevented): a click with the key's modifier state is dispatched on the root → selection. | RAD-KEY-01 |
| <kbd>Space</kbd> | root focused, `nativeButton=true` | Not intercepted; the browser's native button click on keyup → selection. | RAD-KEY-03 |
| <kbd>Enter</kbd> | root focused, either mode | keydown default prevented; no click, no selection. | RAD-KEY-02 |
| <kbd>ArrowUp/Down/Left/Right</kbd> | in a group | Not handled by the radio; the group moves focus and the newly focused radio selects itself (RDG-KEY-01/03, RAD-FOC-05). Standalone: nothing. | RAD-KEY-04 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Not intercepted. | RAD-KEY-05 |
| any key | disabled | No effect (no `preventDefault`, no activation, consumer key handlers not invoked). | RAD-KEY-06 |
| <kbd>Space</kbd> | `readOnly` | Click dispatched on the root; the root click handler ignores it. | RAD-KEY-06 |
| any key | target is a descendant of the root, not the root | Not handled. | RAD-KEY-08 |
| modifiers | any of the above | Never change behavior; carried into the resulting click/change event. | RAD-KEY-07 |

- **RAD-KEY-01** — MUST. With `nativeButton=false`, a <kbd>Space</kbd> keydown whose target is the root MUST have its default prevented without selecting; the subsequent <kbd>Space</kbd> keyup on the root MUST select the radio (by dispatching a modifier-carrying click on the root) unless the keyup's default was prevented (UBT-KEY-02/04). Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:176-180`, `internals/use-button/useButton.ts:138,155-170,207-216`. Test: [L4] `RadioGroup.test.tsx` "should select an item with Space on keyup". Docs: CHANGELOG v1.6.0 "Fix selection on space key press".
- **RAD-KEY-02** — MUST. An <kbd>Enter</kbd> keydown on the root MUST have its default prevented and MUST NOT select the radio, in both modes (non-native: the prevented keydown cancels the button-behavior click, UBT-KEY-03; native: the prevented keydown cancels native button activation).
  Source: `radio/root/RadioRoot.tsx:135-141`, `internals/use-button/useButton.ts:165-168`. Test: [L4] `RadioGroup.test.tsx` "should not select an item with Enter".
- **RAD-KEY-03** — MUST. With `nativeButton=true`, <kbd>Space</kbd> MUST NOT be intercepted (UBT-KEY-09); the browser's native click selects the radio through RAD-PTR-01.
  Source: `internals/use-button/useButton.ts:155-163,187-196` (composite `false`, native → no branch). Test: [L4] `RadioRoot.test.tsx` "propagates a single click event to ancestors per user click (nativeButton=%s)" (`true` variant; click path only).
- **RAD-KEY-04** — MUST. The radio MUST NOT handle arrow keys itself; outside a group arrow keys have no effect.
  Source: `radio/root/RadioRoot.tsx:135-141` (only Enter handled). Test: none.
- **RAD-KEY-05** — MUST. <kbd>Tab</kbd> MUST NOT be intercepted (UBT-KEY-21).
  Source: `internals/use-button/useButton.ts:116-176`. Test: [L4] `RadioGroup.test.tsx` "moves focus with arrow keys and Tab".
- **RAD-KEY-06** — MUST. While disabled, keydown/keyup on the root MUST have no effect and consumer `onKeyDown`/`onKeyUp` MUST NOT be invoked (UBT-KEY-19); while `readOnly`, the <kbd>Space</kbd> keyup still dispatches a root click but the root click handler MUST ignore it (RAD-PTR-07).
  Source: `internals/use-button/useButton.ts:117-119,178-180`, `radio/root/RadioRoot.tsx:143`. Test: none.
- **RAD-KEY-07** — MUST. When a selection originates from the keyboard, the change details' `event` MUST report the key event's `shiftKey`/`ctrlKey`/`altKey`/`metaKey` (the modifier state is carried from the key event to the root click and from the root click to the input click).
  Source: `utils/dispatchClickWithModifiers.ts:20-41`, `internals/use-button/useButton.ts:215`, `radio/root/RadioRoot.tsx:154`. Test: none for keyboard (pointer equivalent: `RadioGroup.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange"). Docs: CHANGELOG v1.2.0 "Preserve modifier key properties in the change event".
- **RAD-KEY-08** — MUST. Key events whose target is not the root element itself MUST NOT activate the radio (UBT-KEY-08).
  Source: `internals/use-button/useButton.ts:127,208`. Test: none.
- **RAD-KEY-09** — MUST. A consumer `onKeyUp` that calls `preventDefault()` MUST cancel <kbd>Space</kbd> selection; a prevented <kbd>Space</kbd> *keydown* does not (UBT-KEY-05); `preventBaseUIHandler()` in either handler MUST prevent activation (UBT-KEY-06). Server: relaxed, see §9.
  Source: `internals/use-button/useButton.ts:121-124,184-186,198-216`. Test: none.

### 6.2 Pointer and touch

Pointer type makes no difference; there is no hover, drag, long-press or right-click behavior.

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| Click on root (any pointer or `click()`) | enabled, not readOnly | Root click default prevented; a click carrying modifier state is dispatched on the hidden input → selection. | RAD-PTR-01, RAD-PTR-02 |
| Click on root | disabled | Default prevented, nothing else; `pointerdown` default also prevented; consumer click/mousedown/pointerdown handlers not invoked. | RAD-PTR-08 |
| Click on root | readOnly | Ignored entirely (default not prevented). | RAD-PTR-07 |
| Click on the hidden input directly | enabled | Selection; the click does not propagate to ancestors. | RAD-PTR-03, RAD-PTR-04 |
| Click on a wrapping native `<label>` | `nativeButton=false` | Browser activates the hidden input → selection. | RAD-PTR-05 |
| Click on `<label htmlFor=…>` | either mode | Browser activates the element with that id (hidden input, or the native-button root) → selection. | RAD-PTR-06 |
| Click on a non-native `Field.Label` | any | Focuses the radio; no selection. | RAD-PTR-10 |
| Click on the already-checked radio | any | Click delivered to the input; no change event, no callback. | RDG-EVT-06 |

- **RAD-PTR-01** — MUST. A click on the root (enabled, not read-only, default not already prevented) MUST select the radio by dispatching a `click` on the hidden input that is bubbling, cancelable, composed, pointer-typed with `detail` 0, empty `pointerType`, and the root click's `shiftKey`/`ctrlKey`/`altKey`/`metaKey` (UBT-EVT-09, UBT-ALG-05).
  Source: `radio/root/RadioRoot.tsx:142-155`, `utils/dispatchClickWithModifiers.ts:20-41`. Test: [L4] `RadioGroup.test.tsx` "should call onValueChange when an item is clicked", "should report keyboard modifier event properties when calling onCheckedChange".
- **RAD-PTR-02** — MUST. The root click handler MUST call `preventDefault()` on the root click before dispatching (so an enclosing `<label>`'s activation does not deliver a second click to the input) and MUST dispatch only when the hidden input is mounted; a root click whose default was already prevented (by a consumer handler) MUST be ignored. Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:143-154`. Test: [L4] `RadioGroup.test.tsx` "associates implicitly" (native `<label>`, one change per click).
- **RAD-PTR-03** — MUST. Any click delivered to the hidden input MUST NOT propagate to ancestors: for one user click on the root, ancestor click listeners observe exactly one click (the original) in both modes, and an arrow-key selection produces no ancestor click at all.
  Source: `radio/root/RadioRoot.tsx:216-220`. Test: [L4] `RadioRoot.test.tsx` "propagates a single click event to ancestors per user click (nativeButton=%s)" (2 variants), "does not propagate a click to ancestors when selecting with arrow keys". Docs: CHANGELOG v1.7.0 "Stop internal input clicks from reaching ancestors".
- **RAD-PTR-04** — MUST. A click delivered directly to the hidden input (label or script) MUST select the radio exactly as a root click does.
  Source: `radio/root/RadioRoot.tsx:196-215`. Test: [L2] `RadioGroup.test.tsx` "should update its state if the underlying input is toggled", "does not change state when canceled via a hidden input click".
- **RAD-PTR-05** — MUST. With `nativeButton=false`, clicking anywhere on a native `<label>` that wraps the radio MUST select it once.
  Source: `radio/root/RadioRoot.tsx:147,276` (the input is a labelable descendant of the label). Test: [L4] `RadioGroup.test.tsx` "associates implicitly" (native `<label>`), "associates implicitly" (Field.Label). Docs: `components/radio-group/page.mdx` §Labeling a radio group.
- **RAD-PTR-06** — MUST. Clicking a native `<label htmlFor={id}>` MUST select the radio when `id` equals the radio's `id` prop, for both `nativeButton=false` (label targets the hidden input) and `nativeButton=true` (label targets the root `<button>`).
  Source: `radio/root/RadioRoot.tsx:118-119,134,186`. Test: [L4] `RadioGroup.test.tsx` "associates explicitly" (native `<label>`); `RadioRoot.test.tsx` "associates `id` with the native button when `nativeButton=true`". Docs: `components/radio-group/page.mdx` §Rendering as a native button.
- **RAD-PTR-07** — MUST. When read-only, a root click MUST be ignored (no `preventDefault`, no dispatch) and a click reaching the hidden input (label or direct) MUST NOT change the selection: the input's `checked` is reverted and no callback fires.
  Source: `radio/root/RadioRoot.tsx:143,202-204`. Test: [L4] `RadioGroup.test.tsx` "should not change its state when clicked" (readOnly).
- **RAD-PTR-08** — MUST. When disabled, a root click MUST have its default prevented and MUST NOT select; `pointerdown` MUST also be default-prevented; consumer `onClick`/`onMouseDown`/`onPointerDown` MUST NOT be invoked (UBT-PTR-01..03); a click reaching the hidden input MUST be ignored.
  Source: `internals/use-button/useButton.ts:104-115,218-224`, `radio/root/RadioRoot.tsx:143,202`. Test: [L4] `RadioGroup.test.tsx` "should not change its state when clicked" (disabled).
- **RAD-PTR-09** — MUST. A consumer `onClick` on the root MUST run before the internal selection, once per click; `stopPropagation()` in it MUST stop the click reaching ancestors without preventing the selection; `preventDefault()` or `preventBaseUIHandler()` in it MUST prevent the selection (RAD-PTR-02, RND-EVT-03). Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:143,242-250`, `internals/use-button/useButton.ts:104-110`. Test: [L4] `RadioRoot.test.tsx` "does not propagate to ancestors when stopPropagation() is called (nativeButton=%s)" (2 variants).
- **RAD-PTR-10** — MUST. Clicking a `Field.Label` rendered as a non-native label (`nativeLabel={false}`) MUST focus the radio root (via the hidden input's focus redirect, RAD-FOC-02) and MUST NOT select it (FFI-PTR-01).
  Source: `internals/labelable-provider/useLabel.ts:46-62,70-76`, `radio/root/RadioRoot.tsx:221-223`. Test: none.
- **RAD-PTR-11** — MUST. A click event delivered to the hidden input whose default was already prevented before dispatch MUST be ignored: no group change request, no callback, no field side effect.
  Source: `radio/root/RadioRoot.tsx:197-200`. Test: none.

### 6.3 Focus management

- **RAD-FOC-01** — MUST. Inside a group the root's `tabindex` MUST be the composite roving value (`0` when it holds the group's highlight, else `-1`, CMP-DOM-01) regardless of its disabled state in non-native mode (UBT-FOC-05); a disabled `nativeButton` radio carries `disabled` and is not focusable. Standalone with `nativeButton=false` the root MUST have `tabindex="0"` (`-1` when disabled, UBT-DOM-03/04); standalone native: no `tabindex` added. The hidden input (`tabindex="-1"`) is never a tab stop. A consumer `tabIndex` overrides the built-in value.
  Source: `radio/root/RadioRoot.tsx:176-180,242-250,263-272`, `internals/composite/item/useCompositeItem.ts:25`, `utils/useFocusableWhenDisabled.ts:30-36`, `internals/use-button/useButton.ts:227-228` (external props merged last). Test: [L4] `RadioGroup.test.tsx` "sets tabIndex=0 to the correct element initially", "moves the tab stop to the checked radio when the highlighted radio is removed"; `RadioRoot.test.tsx` "does not acquire the field focused state when focused programmatically" (standalone disabled radio is programmatically focusable).
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-22: the `composite` module owns the roving `tabindex` from attach; ADR 13 §3): re-tagged from L2, because the group clause is the composite's after its first publication, which only a browser observes; the titles run in `radio-group.spec.ts` and `radio.spec.ts`. The standalone clauses, which C# renders, are asserted at L2 by the conformance suite and `RadioRootTests` "uses aria-disabled instead of HTML disabled".
- **RAD-FOC-02** — MUST. If the hidden input receives focus (from a label click, a browser validation bubble or script), focus MUST be moved immediately to the root element (FFI-FOC-05).
  Source: `radio/root/RadioRoot.tsx:221-223`. Test: none.
- **RAD-FOC-03** — MUST. Within a `Field.Root`, the root receiving focus MUST set the field's `focused` flag to `true` unless the radio is disabled (a disabled focused radio never publishes the flag); on blur the radio MUST clear the flag only when it is standalone — inside a group the group clears it on leaving blur (RDG-FOC-04).
  Source: `radio/root/RadioRoot.tsx:87,157,167-173`, `internals/field-root-context/useSetFieldFocused.ts:22-30`. Test: [L4] `RadioRoot.test.tsx` "acquires the field focused state on focus", "does not acquire the field focused state when focused programmatically"; `RadioGroup.test.tsx` "is not acquired when focus lands on a disabled radio", "is not acquired when a radio inherits disabled from Field.Item".
- **RAD-FOC-04** — MUST. Within a `Field.Root`, a radio that owns the focused flag MUST release it when it becomes disabled or unmounts without a blur; a radio that is the document's active element when it mounts or becomes enabled MUST re-acquire it post-commit, pre-paint; refocusing a radio while disabled MUST NOT acquire it; a radio that never owned the flag MUST NOT clear it when it unmounts (FFI-STA-09, FFI-ALG-13). Server: relaxed, see §9.
  Source: `internals/field-root-context/useSetFieldFocused.ts:22-43`. Test: [L4] `RadioGroup.test.tsx` "is removed when the focused radio unmounts but its group remains", "is not reacquired when a radio disabled while focused is refocused", "is reacquired when a radio that kept focus while disabled is re-enabled", "is kept when a previously focused radio unmounts after focus moves to a sibling".
- **RAD-FOC-05** — MUST. *Focus-driven selection.* When the root receives focus while the group's pending-navigation flag is set (RDG-STA-06) and the radio is enabled and not read-only (and the focus event is not default-prevented), the radio MUST click its own hidden input (a plain, modifier-less click → selection) and then clear the flag; when the flag is not set, focus never selects. Field `focused` is set before this check (RAD-FOC-03). Server: relaxed, see §9.
  Source: `radio/root/RadioRoot.tsx:156-166`. Test: [L4] `RadioGroup.test.tsx` "should automatically select radio upon navigation", "does not select on refocus after an ignored arrow (%s)", "is reacquired when an arrow key moves focus from a disabled radio to an enabled sibling".
  Port note 2026-10-23 (ADR 04 Amendments 2026-10-20: the decision point `select`, fallback **run**; §16 Q6): the click is the `composite` module's, after its pending navigation; a JavaScript `preventDefault()` on the focus is honoured, and a C# consumer `onfocus` that calls `PreventPartHandler()` vetoes it through the decision point (`radio-group.spec.ts` "port: a C# onfocus veto stops the focus-driven selection on WebAssembly and is too late on Server"; `RadioRootTests` "port: a consumer onfocus that prevents the part handler vetoes the focus-driven selection").
- **RAD-FOC-06** — MUST. Inside a group, the root receiving focus MUST make it the group's highlighted item (tab stop) without scrolling (CMP-PTR-02).
  Source: `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L4] `RadioGroup.test.tsx` "moves focus with arrow keys and Tab" (Shift+Tab returns to the last focused radio).

### 6.4 Dismissal

Not applicable — no popup or dismissable surface.

## 7. Events and callbacks

Radio parts expose no callback props; all selections are reported through `RadioGroup.onValueChange` (RDG-EVT-01..08). This section states what the radio contributes to that pipeline.

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| (none on Radio) | — | — | — | — |
| `RadioGroup.onValueChange` | see RDG §7 | hidden input change (RAD-EVT-01) | `'none'` | yes |
| `onClick`, `onFocus`, `onKeyDown`, … | DOM handlers forwarded to the root | per DOM, consumer-first | — | see RAD-PTR-09, RAD-KEY-09 |

- **RAD-EVT-01** — MUST. On a hidden-input change that is not pre-canceled, while enabled, not read-only and with a defined `value`, the radio MUST create change-event details with reason `'none'` and `event` = the native event (the input click), request the group to adopt `value` with those details (which invokes `onValueChange` and, unless canceled, commits the value), and, only if not canceled, set the field's `touched` flag to `true` (EVD-ALG-03, EVD-EVT-08).
  Source: `radio/root/RadioRoot.tsx:196-215`, `internals/createBaseUIEventDetails.ts:126-157`. Test: [L2] `RadioGroup.test.tsx` "should call onValueChange when an item is clicked", "should automatically select radio upon navigation" (`data-touched`), "does not change state when canceled via a root click" (no `data-touched`).
- **RAD-EVT-02** — MUST. Outside a group the change request is a no-op (no callback exists) but the touched write of RAD-EVT-01 still happens.
  Source: `radio/root/RadioRoot.tsx:65,208,214`. Test: none.
- **RAD-EVT-03** — MUST. A radio whose `value` is `undefined` MUST ignore input changes entirely (no request, no touched write); the input is re-synchronized to `checked`.
  Source: `radio/root/RadioRoot.tsx:202-204`. Test: none.

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md) (§6.3 row `Radio.Root`). The radio itself never registers as a field control; it contributes a native input and a validation-registry entry to the group's registration (RDG §8).

- **RAD-FRM-01** — MUST. The hidden input participates natively in its owning form (the ancestor `<form>`, or the `<form>` named by the group's `form`): when checked and enabled it contributes `name = serializeValue(value)`; when unchecked, disabled, `:disabled` through a `<fieldset disabled>`, or nameless it contributes nothing (FFI-FRM-28).
  Source: `radio/root/RadioRoot.tsx:185,187,191-193`. Test: [L4] `RadioGroup.test.tsx` "should include selected radio value in form data", "excludes a radio disabled through an ancestor <fieldset disabled> to match native form data", "submits to an external form when `form` is provided".
  Port note 2026-10-23: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3). The titles run in `radio-group.spec.ts`; `Form` and `Field.Root`, which Base UI renders around some of them, take no part in what they assert.
- **RAD-FRM-02** — MUST. `serializeValue(value)` MUST be: `""` for `null`/`undefined`; the string itself for strings; otherwise `JSON.stringify(value)` (so `1` → `"1"`, `{a:1}` → `"{\"a\":1}"`), falling back to `String(value)` when JSON serialization throws (FFI-ALG-11, FFI-EDGE-05).
  Source: `internals/serializeValue.ts:1-13`. Test: [L2] `RadioGroup.test.tsx` "should set the name attribute on each radio input" (string); non-string: none.
  Port note 2026-10-23 (ADR 08 Amendments 2026-10-22; FFI-ALG-11's note; ADR 16 rule 1): a `string` is submitted as is and `null` renders no `value` attribute (`""` in a form value); any other `TValue` is submitted as its text under `CultureInfo.InvariantCulture`, built by an interpolated `string.Create`, which takes the value unboxed. `true` becomes `True`, an enum its name and a record its `ToString()`, where `JSON.stringify` gives `true`, a number and an object literal: a reflection-based JSON serializer fails ADR 16 rule 1. The serializer is internal to `Radio/` until Select (M5) moves it to `Field/` (`RadioValuesTests`, L1, with `de-DE` as the current culture).
- **RAD-FRM-03** — MUST. Inside a group within a `Field.Root`, the hidden input MUST be registered in the field's validation registry when it mounts (with the root element as its control) and removed when it unmounts, so native validity (`required` → `valueMissing`) is read from the representative radio and the focus-on-invalid target is a radio root (FFI-FRM-24, FFI-FOC-04).
  Source: `radio/root/RadioRoot.tsx:90-96`, `field/root/useFieldValidation.ts:108-121`. Test: [L2] `RadioGroup.test.tsx` "triggers native HTML validation on submit", "focuses the first enabled radio when all radios start disabled", "unblocks submission after every radio in the group unmounts" (L4 for the native-validity and focus-on-invalid clauses).
- **RAD-FRM-04** — MUST. The hidden input MUST be registered with the group for `inputRef` forwarding when it mounts and re-registered post-commit, pre-paint whenever the radio's `checked` or effective disabled changes, except when it is both disabled and checked (RDG-STA-07); its unmount MUST detach it (RDG-STA-08). Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-23): with `inputRef` withdrawn (RAD-API-06, RDG-API-09) there is no forwarding to register for; the §9 entry is removed.
  Source: `radio/root/RadioRoot.tsx:96,104-115`. Test: [L2] `RadioGroup.test.tsx` "points inputRef to the checked radio input when present", "detaches inputRef when an initially disabled radio is enabled and then unmounted".
- **RAD-FRM-05** — MUST. Within a `Field.Root`, every non-canceled selection MUST set the field's `touched` flag to `true` (in addition to the group's blur-based touched, RDG-FOC-05).
  Source: `radio/root/RadioRoot.tsx:214`. Test: [L2] `RadioGroup.test.tsx` "should automatically select radio upon navigation".
- **RAD-FRM-06** — MUST. A native `Field.Label` in the radio's labelable scope (its `Field.Item`, else the `Field.Root`) MUST carry `for` = the hidden input's id (`nativeButton=false`) or the root's id (`nativeButton=true`), and the root's `aria-labelledby` MUST be that label's id (subject to RAD-ARIA-03); clicking the label (wrapping or sibling) MUST select the radio.
  Source: `radio/root/RadioRoot.tsx:118-119,132,134,186`, `internals/labelable-provider/useLabel.ts:64-69`. Test: [L2] `RadioGroup.test.tsx` "associates implicitly" and "associates explicitly" (Field.Label). Docs: CHANGELOG v1.0.0-beta.1 "Support implicit `Field.Label`"; `handbook/forms/page.mdx`.
- **RAD-FRM-07** — MUST. A native form reset MUST NOT change the radio's `checked` (it follows the group value, which has no reset handling, FFI-FRM-30).
  Source: `radio/root/RadioRoot.tsx:84,193,276`. Test: none (see §16 item 4).

## 9. Lifecycle, mounting and animation

Vocabulary: "post-commit, pre-paint", "next frame", "on animations finished" per conventions §4; mechanism in [TRN](../architecture/transitions-and-mounting.md). Only `Radio.Indicator` has a lifecycle; `Radio.Root` and its hidden input are always mounted.

State diagram (Indicator, `keepMounted=false`):

```
unchecked, unmounted ──(checked)──► mounted, 'starting' ──(next frame)──► mounted, no status
        ▲                                                                        │
        │ (on animations finished)                                           (unchecked)
        └──────────────── mounted, 'ending' ◄────────────────────────────────────┘
```

- **RAD-ANI-01** — MUST. The Indicator element MUST be rendered iff `keepMounted || mounted`, where `mounted` becomes `true` in the same commit the radio becomes checked (TRN-STA-03) and becomes `false` only through RAD-ANI-03.
  Source: `radio/indicator/RadioIndicator.tsx:26-28,37,58-62`. Test: [L4] `RadioIndicator.test.tsx` "triggers enter animation via data-starting-style when mounting" (absent before selection), "should remove the indicator when there is no exit animation defined".
- **RAD-ANI-02** — MUST. `transitionStatus` MUST be `'starting'` (`data-starting-style=""`) in the commit where the Indicator mounts because the radio became checked, cleared on the next frame (TRN-STA-04); `'ending'` (`data-ending-style=""`) in the commit where the radio becomes unchecked while mounted (TRN-STA-06); and unset once unmounted (TRN-STA-08). An Indicator whose radio is checked at first render MUST NOT go through `'starting'` (TRN-STA-13). Server: relaxed, see §9.
  Source: `radio/indicator/RadioIndicator.tsx:28-33`, `internals/useTransitionStatus.ts:23-42,58-73`, `internals/stateAttributesMapping.ts:10-20`. Test: [L4] `RadioIndicator.test.tsx` "triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount".
- **RAD-ANI-03** — MUST. After the radio becomes unchecked, the Indicator MUST stay mounted until all finite animations/transitions on its element have finished (TRN-ANI-03..07; first inspection on the next frame, TRN-ANI-11), then `mounted` MUST become `false`; with no animation defined (or `getAnimations` unavailable) it is removed at that first inspection. The completion watcher is armed only while unchecked and MUST be aborted if the radio becomes checked again before completion (TRN-ANI-08, TRN-STA-09). Completions of several Indicators in the same microtask MAY be batched into one commit (`batch: true`, TRN-ANI-10). Server: relaxed, see §9.
  Source: `radio/indicator/RadioIndicator.tsx:46-56`, `internals/useOpenChangeComplete.tsx:9-28`. Test: [L4] `RadioIndicator.test.tsx` "should remove the indicator when there is no exit animation defined", "should remove the indicator when the animation finishes", "applies data-ending-style before unmount". Docs: CHANGELOG v1.2.0 "Allow exit animations on `<Radio.Indicator>` when `keepMounted={false}`".
- **RAD-ANI-04** — MUST. With `keepMounted`, the element MUST remain in the DOM while unchecked with `data-unchecked` and no `hidden`/`inert`/inline style; `data-ending-style` MUST still be applied on uncheck and removed on animations finished (status unset, TRN-STA-08), and `data-starting-style` applied on re-check.
  Source: `radio/indicator/RadioIndicator.tsx:37,46-56`. Test: [L4] `RadioIndicator.test.tsx` "should remove the indicator when the animation finishes" (`keepMounted`, `onAnimationEnd` fires); `RadioGroup.test.tsx` "should apply data-checked and data-unchecked to radio root and indicator".
- **RAD-ANI-05** — MUST. There is no `onOpenChangeComplete`, `actionsRef` or `hidden="until-found"` on the Indicator; the `BASE_UI_ANIMATIONS_DISABLED` flag (TRN-EDGE-01) makes removal immediate.
  Source: `radio/indicator/RadioIndicator.tsx:65-71`. Test: [L4] `RadioIndicator.test.tsx` `beforeEach` sets the flag.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- RAD-KEY-01 — UBT-KEY-03/04 row (consumer veto of the <kbd>Space</kbd> keyup): met on both modes when a consumer JavaScript listener calls `preventDefault()` (the `button` module checks `defaultPrevented`); a C# consumer's `PreventBaseUIHandler()` is synchronous on WebAssembly through `decide()` and a round trip late on Server (fallback "run": the radio is selected). The library's own Enter suppression (RAD-KEY-02) is a `button` module option and is not relaxed.
- RAD-KEY-09 — UBT-KEY-03/04 and EVD-EVT-26 rows: a consumer `onKeyUp` `preventDefault()` from a JavaScript listener is honoured on both modes; a C# consumer's `preventDefault()`/`PreventBaseUIHandler()` arrives after dispatch, so on Server the <kbd>Space</kbd> keyup is handled regardless; WebAssembly honours it through `decide()`.
- RAD-PTR-02 — EVD-EVT-26 row: a root click already prevented by a C# consumer handler is seen by the `button` module only a round trip later on Server, so the hidden-input click is dispatched regardless; a JavaScript listener's `preventDefault()` and WebAssembly's `decide()` meet the spec.
- RAD-PTR-09 — EVD-EVT-26 row: `preventDefault()`/`PreventBaseUIHandler()` in a C# consumer `onClick` arrives after dispatch on Server and does not prevent the selection; WebAssembly honours it through `decide()`.
- RAD-STA-04 — RND-STA-01 row (mirrored fields): the Indicator's `transitionStatus` reaches the C# state (and so `ClassValue`/`StyleValue`) one round trip after JS writes `data-starting-style`/`data-ending-style`.
- RAD-ANI-02 — TRN-STA-04/05/07/09 row, met on both modes: the next-frame `data-starting-style`/`data-ending-style` flip is JS-owned with C# mirroring; listed so it is not moved back to C#.
- RAD-ANI-03 — TRN-ANI-03/09/10 row: the Indicator's removal after its exit animations lands in the first render batch after the completion message, about two round trips later; JS hides it at animation end (optimistic convergent write).
- RAD-STA-02 — the row "C#-origin consumer vetoes of a toggle on a hidden input" (added 2026-10-22): a click on a standalone radio leaves its input checked as the click left it on Server until the `dom` primitive's restore, about two round trips; on WebAssembly the `toggle` decision reverts it in the same dispatch. The root's `aria-checked` and `data-*` never change.
- RAD-FOC-05 — the row "C#-origin consumer vetoes": a JavaScript `preventDefault()` on the focus is honoured on both modes; a C# consumer `onfocus` that calls `PreventPartHandler()` is answered at the decision point `select` on WebAssembly and arrives after the selection on Server (fallback **run**, ADR 04 Amendments 2026-10-20).
- RAD-STA-05 — FFI registration row: the mount-time `filled` write lands after paint plus a round trip; observable only as latency of `data-filled`.
- RAD-FOC-04 — FFI registration row: re-acquiring the field's `focused` flag on mount or re-enable lands after paint plus a round trip; observable only as latency of `data-focused`.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `CompositeItem` (in a group) | [CMP](../architecture/composite-and-list-navigation.md) §6.4 | `tag: 'span'`; `render`/`className`/`style`; `state`; `refs: [forwardedRef, rootHandle, buttonHandle]`; `props: [rootProps, elementProps, getButtonProps, getDescriptionProps, validationGetter]`; `stateAttributesMapping` = §3.1 mapping; no `metadata`; the registration handle attaches first (CMP-STR-04) |
| `useRenderElement` (standalone) | [RND](../architecture/rendering-and-composition.md) | tag `span`, same `props`/`refs`/mapping; `enabled: !inGroup` |
| `useRenderElement` (Indicator) | RND | tag `span`; consumer props only; mapping = checked/unchecked + transition status + field validity |
| `getStateAttributesProps` | RND | booleans → `data-<lowercase key>=""`; custom mapping for `checked`, `transitionStatus`, `valid` |
| `useButton` | [UBT](../architecture/button-behavior.md) | `{ disabled: effectiveDisabled, native: nativeButton, composite: false }`; `focusableWhenDisabled` unset; `tabIndex` default `0` (overridden by the composite item's `tabindex` inside a group); its `role: 'button'` is overridden by `role: 'radio'` |
| `useFocusableWhenDisabled` (via `useButton`) | UBT | non-composite rules: non-native `tabindex` `0`/`-1` + `aria-disabled`; native `disabled` attribute |
| `dispatchClickWithModifiers` | UBT-ALG-05 | root click → hidden input (`detail: 0`, `pointerType: ''`); keyboard keyup → root |
| Active item marker | CMP-DOM-02 | `data-composite-item-active=""` while checked |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | reason `'none'`, event = native input click, no trigger, no custom fields |
| `useTransitionStatus` | [TRN](../architecture/transitions-and-mounting.md) | `useTransitionStatus(checked)` — no idle state, no deferred ending, no animate-initial-open |
| `useOpenChangeComplete` | TRN | `{ batch: true, enabled: !checked, open: checked, ref: indicatorHandle, onComplete: unmount if still unchecked }` |
| `useBaseUiId` / `useLabelableId` | RND / [FFI](../architecture/field-and-form-infrastructure.md) | generated root id (non-native); `useLabelableId({ id: idProp })` claims the scope's control id (FFI-ALG-06/07) |
| `useAriaLabelledBy` | FFI-ALG-08 | `(ariaLabelledByProp, labelId, hiddenInputHandle, enableFallback = !nativeButton, hiddenInputId, ariaLabel)` |
| `useFieldRootContext`, `useFieldItemContext`, `useSetFieldFocused`, `useLabelableContext` | FFI | `disabled` chain (FFI-STA-11); `setFieldFocused(true)` on focus, `(false)` on blur when standalone; `setFilled(true)` on checked mount; `setTouched(true)` on selection; `getDescriptionProps` from the nearest scope |
| Group shared state | [RDG](radio-group.md) | reads `checkedValue`, `disabled`, `readOnly`, `required`, `name`, `form`, `validation`, pending-navigation flag; calls `setCheckedValue(value, details)`, `setTouched(false)`, `registerInputRef(input)`; `validation.registerInput(input, { controlRef: rootHandle, value: undefined })`; `validation.getValidationProps(disabled, props)` on the root |
| `serializeValue` | FFI-ALG-11 | hidden input `value` |
| `visuallyHidden` / `visuallyHiddenInput` | UTL-DOM-13/14 | hidden input style by presence of a group name |
| `useMergedRefs` | UTL | hidden input: consumer `inputRef` + internal handle + group registration + field registration |
| `mergeProps` | RND | consumer handlers before built-in; `preventBaseUIHandler()` opt-out |

## 12. Edge cases, invariants and dev warnings

All dev messages are emitted only outside production, through `console.error`, prefixed `Base UI: `, at most once per message.

- **RAD-EDGE-01** — MUST. Rendering `Radio.Indicator` outside a `Radio.Root` MUST throw an error with the message `Base UI: RadioRootContext is missing. Radio parts must be placed within <Radio.Root>.`
  Source: `radio/root/RadioRootContext.ts:9-17`. Test: none.
  Port note 2026-10-23 (the Collapsible and Checkbox precedent, collapsible §16 item 17): the message is the port's, in every build: ``Blazix: The `RadioRoot` context is missing. Radio parts must be placed within `<RadioRoot>`.`` (`RadioIndicatorTests` "port: rendering an indicator outside a radio throws a descriptive error").
- **RAD-EDGE-02** — SHOULD. A `nativeButton`/element mismatch SHOULD log the dev errors of UBT-EDGE-01/02 (verbatim there).
  Source: `internals/use-button/useButton.ts:36-66`. Test: none.
- **RAD-EDGE-03** — MUST. Outside a group the radio is static (RAD-STA-01/02): checked iff `value === ''`, never changed by interaction, no `aria-invalid`, no composite registration; it still reports field `focused`/`touched` and participates in native submission only when the group's name is absent (so never submits a named value).
  Source: `radio/root/RadioRoot.tsx:54-68,84,187,239`. Test: [L2] `RadioRoot.test.tsx` "acquires the field focused state on focus", "does not acquire the field focused state when focused programmatically" (L4 for the `focused` clause).
- **RAD-EDGE-04** — MUST. `readOnly` MUST NOT affect focusability, the roving tab stop, form submission, `data-disabled` or `aria-disabled`; it blocks selection only (click, Space, focus-driven selection) and adds `data-readonly` and the input's `readonly` attribute.
  Source: `radio/root/RadioRoot.tsx:143,159,195,202`. Test: [L2] `RadioRoot.test.tsx` "acquires the field focused state on focus" (readOnly radio focusable) (L4 for the Space and focus-driven selection clauses).
- **RAD-EDGE-05** — MUST. A consumer `aria-label` consisting only of whitespace MUST be treated as absent for RAD-ARIA-03 while still being forwarded.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:18`. Test: none.
- **RAD-EDGE-06** — MUST. No behavior of the radio depends on writing direction or locale (arrow-key direction handling belongs to the group, RDG-KEY-01).
  Source: `radio/root/RadioRoot.tsx:1-27` (no direction import). Test: none.
- **RAD-EDGE-07** — MUST. Server-rendered markup MUST contain the same root/hidden-input structure and attributes as the client's initial render with deterministic ids; inside a group the root's `tabindex` is `-1` before the first composite publication unless its index is `0` (CMP-STA-15), so the checked radio's `tabindex="0"` appears post-hydration.
  Source: `radio/root/RadioRoot.tsx:276` (`suppressHydrationWarning` on the input only), `internals/composite/item/useCompositeItem.ts:19-25`. Test: none.
- **RAD-EDGE-08** — MUST. A root click, focus or Space activation before the hidden input is attached MUST NOT throw and MUST leave the selection unchanged.
  Source: `radio/root/RadioRoot.tsx:149-152,163`. Test: none.
- **RAD-EDGE-09** — MUST. A `nativeButton` radio substituted through a render callback that wraps the `<button>` in a `<label>` (docs pattern) MUST keep the hidden input outside the label (it stays the root's sibling); clicking the label activates the `<button>` natively, which selects the radio through RAD-PTR-01.
  Source: `radio/root/RadioRoot.tsx:260-278`. Test: [L2] `RadioRootTests` "port: a native-button radio wrapped in a label by a render callback renders its hidden input after the label" (L4 for the label-click clause). Docs: `components/radio-group/page.mdx` §Rendering as a native button.
  Port note 2026-10-23 (ADR 02 Amendments 2026-10-22 item 1; conventions §3.5): "points inputRef to the first radio input when nativeButton wraps a button" is re-cited to the port title above, which asserts the structure. The label-click clause is open: the `button` module finds the hidden input as the element's next sibling (ADR 04 Amendments 2026-10-22, the bridge row), and here the input follows the callback's `<label>`, so neither a click on the label nor one on the button selects the radio. `radio.spec.ts` "port: a native-button radio wrapped in a label by a render callback is selected by a click on the label" is a Playwright expected failure until design point R9 of M2 tracks 7–8 is answered (raised to the orchestrator 2026-10-23).
- **RAD-EDGE-10** — MUST. Several radios with the same `value` are all checked when that value is selected (RDG-EDGE-02); each Indicator mounts and unmounts independently.
  Source: `radio/root/RadioRoot.tsx:84`. Test: [L4] `RadioIndicator.test.tsx` "should remove the indicator when there is no exit animation defined".
- **RAD-EDGE-11** — MUST. Inside a `Field.Item` whose `disabled` is `true`, the radio is disabled (RAD-API-02) even when the group and field are not.
  Source: `radio/root/RadioRoot.tsx:76,79`, `field/item/FieldItem.tsx:31-34`. Test: [L2] `RadioGroup.test.tsx` "is not acquired when a radio inherits disabled from Field.Item".

Inherited dev checks (not re-documented): invalid `render` element error and component-looking render callback warning from RND; `useControlled` errors belong to the group (RDG-EDGE-01).

## 13. Behaviors evidenced by tests

### `RadioRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, `testRenderPropWith: 'span'`, `button: true`; rendered standalone with `value=""`) | RAD-DOM-17 |
| "sets checked and unchecked data attributes" | RAD-DOM-03, RAD-STA-01, RAD-API-09 |
| "does not forward `value` prop" | RAD-API-01, RAD-DOM-14 |
| "allows `null` value" | RAD-STA-01, RAD-ARIA-02, RAD-API-01 |
| "associates `id` with the native button when `nativeButton=true`" | RAD-API-07, RAD-DOM-01, RAD-DOM-10, RAD-PTR-06, RAD-STR-01 |
| "sets `aria-labelledby` from a sibling label associated with the hidden input" | RAD-ARIA-03, RAD-ARIA-04, RAD-DOM-10 |
| "updates fallback `aria-labelledby` when the hidden input id changes" | RAD-ARIA-04, RAD-DOM-18 |
| "prefers `aria-label` over an associated label" | RAD-ARIA-03 |
| "propagates a single click event to ancestors per user click (nativeButton=%s)" (2 variants: `false`, `true`) | RAD-PTR-03, RAD-API-05, RAD-KEY-03 |
| "does not propagate to ancestors when stopPropagation() is called (nativeButton=%s)" (2 variants: `false`, `true`) | RAD-PTR-09, RAD-API-09 |
| "does not propagate a click to ancestors when selecting with arrow keys" | RAD-PTR-03, RAD-FOC-05 |
| "uses aria-disabled instead of HTML disabled" | RAD-ARIA-06, RAD-DOM-14, RAD-API-02 |
| "does not acquire the field focused state when focused programmatically" | RAD-FOC-03, RAD-FOC-01, RAD-EDGE-03 |
| "acquires the field focused state on focus" | RAD-FOC-03, RAD-API-03, RAD-EDGE-04 |

### `RadioIndicator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`; rendered inside `<Radio.Root value="">`, i.e. checked) | RAD-DOM-17, RAD-STA-01 |
| "should remove the indicator when there is no exit animation defined" | RAD-ANI-01, RAD-ANI-03, RAD-EDGE-10 |
| "should remove the indicator when the animation finishes" | RAD-ANI-03, RAD-ANI-04, RAD-API-11 |
| "triggers enter animation via data-starting-style when mounting" | RAD-ANI-01, RAD-ANI-02 |
| "applies data-ending-style before unmount" | RAD-ANI-02, RAD-ANI-03 |

### `RadioGroup.test.tsx` (tests whose assertions are Radio-owned)

The full matrix is in [radio-group](radio-group.md) §13; these rows are repeated here for traceability.

| Test | Requirements |
|---|---|
| "should select an item with Space on keyup" | RAD-KEY-01 |
| "should not select an item with Enter" | RAD-KEY-02 |
| "should report keyboard modifier event properties when calling onCheckedChange" | RAD-PTR-01 |
| "should update its state if the underlying input is toggled" | RAD-PTR-04 |
| "should not change its state when clicked" (disabled / readOnly) | RAD-PTR-08 / RAD-PTR-07 |
| "should place the style hooks on the root and subcomponents" / "should apply data-checked and data-unchecked to radio root and indicator" | RAD-DOM-03, RAD-DOM-04, RAD-DOM-15, RAD-STA-03, RAD-STA-04, RAD-API-10 |
| "should set the name attribute on each radio input" | RAD-DOM-09, RAD-DOM-11, RAD-FRM-02 |
| "sets tabIndex=0 to the correct element initially" / "moves the tab stop to the checked radio when the highlighted radio is removed" | RAD-DOM-02, RAD-FOC-01 |
| "should automatically select radio upon navigation" / "does not select on refocus after an ignored arrow (%s)" | RAD-FOC-05, RAD-EVT-01, RAD-FRM-05 |
| "associates implicitly" / "associates explicitly" (native label and Field.Label) | RAD-PTR-05, RAD-PTR-06, RAD-FRM-06 |
| "[data-focused] without a blur event" group | RAD-FOC-03, RAD-FOC-04, RAD-EDGE-11 |
| "links the group and individual radios" / "appends the id attribute of the error to aria-describedby of individual radios" | RAD-ARIA-07 |
| "clears required validation when a value is selected" | RAD-ARIA-08 |
| "triggers native HTML validation on submit" / "unblocks submission after every radio in the group unmounts" | RAD-FRM-03 |
| "points inputRef to the checked radio input when present" / "detaches inputRef when an initially disabled radio is enabled and then unmounted" | RAD-FRM-04 |
| "points inputRef to the first radio input when nativeButton wraps a button" | RAD-EDGE-09 |

### Additional requirements introduced here

- **RAD-DOM-17** — MUST. Both parts MUST pass the shared conformance suite of RND (RND-DOM-36..48): for Root with `testRenderPropWith: 'span'` and `button: true` (so the extra non-native-button pass of RND-DOM-49 does not run, and `nativeButton={false}` is passed to the substitute), DOM handle an `HTMLSpanElement`; for Indicator rendered inside a checked standalone root, DOM handle an `HTMLSpanElement`.
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] `RadioRoot.test.tsx` / `RadioIndicator.test.tsx` "Base UI component API".
- **RAD-DOM-18** — MUST. When the `id` prop changes at runtime (`nativeButton=false`), the hidden input's id MUST change accordingly and the root's fallback `aria-labelledby` MUST switch to the `<label htmlFor>` now associated with the new id, assigning that label an id if it has none; the previously assigned label id is left in place.
  Source: `radio/root/RadioRoot.tsx:118-119,186`, `internals/labelable-provider/useAriaLabelledBy.ts:26-35,46-48`, `internals/labelable-provider/useLabelableId.ts:38-61`. Test: [L4] `RadioRoot.test.tsx` "updates fallback `aria-labelledby` when the hidden input id changes".

## 14. React-specific notes and porting considerations

Non-normative.

- **Change detection.** React's `onChange` on a radio input fires from the input's `click`; `event.nativeEvent` is that click (so `shiftKey` etc. are available). A port must compute the selection itself and build an equivalent event for the group's details. The `nativeEvent.defaultPrevented` guard (RAD-PTR-11) works around React issue 9023. Port: see decisions/04-js-interop-boundary.md §3 (`details.event` is a snapshot record) and decisions/05-state-store-and-event-details.md §3.
- **Controlled `checked` restoration.** After a canceled or read-only change the browser has already checked the input; React re-applies `checked={checked}` on re-render. A port must re-sync every radio's input explicitly. The same applies to the previously selected radio: the browser unchecks it natively only when both inputs share a `name` and form; without a name a port must uncheck it itself. Port: see decisions/05-state-store-and-event-details.md §2 (the group value is the controlled pair; the inputs' `checked` is derived from it on every render).
- **Explicit `composite: false`.** Button-behavior infers composite mode from the composite root ancestor; the radio opts out so <kbd>Space</kbd> activates on keyup and the non-composite attribute rules apply, while the composite item's `tabindex` (merged into the external props that the button getter re-merges last) still wins. Ports composing these two mechanisms must preserve that precedence (RAD-FOC-01). Port: see decisions/04-js-interop-boundary.md §1 (`button` module: keydown-to-click synthesis; `composite` module: roving focus, C# receives active-index changes) and §3 (`decide()` for composite/button key vetoes).
- **Prop merging.** `props: [rootProps, elementProps, getButtonProps, getDescriptionProps, validationGetter]` (plus `compositeProps` first inside a group). Objects merge left-to-right; handlers chain so the consumer's runs before the built-in one; the three getters receive the merged props and wrap/augment them. `getButtonProps` extracts `onClick`/`onKeyDown`/… and calls them from inside its own handlers after the disabled gate, which is why consumer handlers are not invoked while disabled. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 3, handler chaining and the `PreventBaseUIHandler` veto).
- **Layout effects.** `setFilled(true)` on checked mount, group re-registration on `checked`/`disabled` change, `useLabelableId` claiming, `useAriaLabelledBy`'s per-render fallback, and `useSetFieldFocused`'s ownership effect are all post-commit, pre-paint. The group re-registration effect ignores the cleanup returned by `registerInputRef`; only the mount-time callback-ref cleanup detaches (hence the comment at `RadioGroup.tsx:141-143`). Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, timing-term mapping) and §Relaxation table (row "FFI registration").
- **Callback-ref merging.** `useMergedRefs(inputRefProp, inputRef, registerInputRef, registerInput)`: React calls the merged callback with the element on mount and runs its cleanup on unmount (React 19 cleanup functions, or `null` calls for legacy refs). The field registry's `registerInput` returns an unregister function used as that cleanup. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24: DOM handles become `ElementReference`).
- **Indicator lifecycle.** `useTransitionStatus` sets state during render for the mount/starting transition; `useOpenChangeComplete` arms in a passive effect and aborts on re-check. A port should follow TRN-ALG-01/04 directly. Port: see decisions/04-js-interop-boundary.md §1 (`transitions` module) and §2 (`data-starting-style`/`data-ending-style` are JS-owned); decisions/01-hosting-model-and-timing.md §Relaxation table (row TRN-STA-04/05/07/09).
- **Ids.** `useBaseUiId()` → `base-ui-` + `React.useId()` (React 17: assigned after mount). The control id claimed through the labelable scope may differ from the `id` prop during the first server render (FFI-FRM-34). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5, generated ids from a scoped counter).
- **`suppressHydrationWarning`** on the hidden input silences browser-managed `checked` differences; server markup must still match (RAD-EDGE-07). Port: see decisions/01-hosting-model-and-timing.md §Decision (item 1, both render modes) and decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5, RND-DOM-29).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| RAD-STR-01 | MUST | root element + hidden radio input next sibling | 2 |
| RAD-STR-02 | MUST | Indicator inside Root; throws otherwise | 2 |
| RAD-STR-03 | MUST | composite item in a group; plain element standalone | 2 |
| RAD-STR-04 | MUST | Indicator structural only | 2 |
| RAD-API-01 | MUST | `value` any type; hidden input only; undefined never selects | 3.1 |
| RAD-API-02 | MUST | effective disabled chain | 3.1 |
| RAD-API-03 | MUST | effective readOnly; blocks selection; input `readonly` | 3.1 |
| RAD-API-04 | MUST | effective required → input `required` only | 3.1 |
| RAD-API-05 | MUST | `nativeButton` default false; UBT mode | 3.1 |
| RAD-API-06 | MUST | `inputRef` → hidden input. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 3.1 |
| RAD-API-07 | MUST | `id` → hidden input (non-native) / root (native) | 3.1 |
| RAD-API-08 | MUST | className/style/render per RND | 3.1 |
| RAD-API-09 | MUST | other props forwarded to root; consumer handlers first | 3.1 |
| RAD-STA-03 | MUST | root state object exactly nine fields | 3.1 |
| RAD-API-10 | MUST | Indicator `keepMounted` default false | 3.2 |
| RAD-API-11 | MUST | Indicator props forwarded | 3.2 |
| RAD-STA-01 | MUST | checked = identity match in group; `''` standalone | 4 |
| RAD-STA-02 | MUST | standalone interaction never changes checked | 4 |
| RAD-STA-04 | MUST | Indicator state = root state + transitionStatus | 4 |
| RAD-STA-05 | MUST | filled true on checked mount | 4 |
| RAD-DOM-01 | MUST | root id: generated / control id | 5.1 |
| RAD-DOM-02 | MUST | active marker while checked | 5.1 |
| RAD-DOM-03 | MUST | exactly one of data-checked/unchecked | 5.1 |
| RAD-DOM-04 | MUST | data-disabled/readonly/required | 5.1 |
| RAD-DOM-05 | MUST | field data attributes only within Field | 5.1 |
| RAD-DOM-06 | MUST | data-valid/invalid/neither | 5.1 |
| RAD-DOM-07 | MUST | `type="button"` native; role radio wins | 5.1 |
| RAD-DOM-16 | MUST | consumer attributes override built-in | 5.1 |
| RAD-DOM-08 | MUST | hidden input type/aria-hidden/tabindex | 5.2 |
| RAD-DOM-09 | MUST | hidden input mirrors checked/disabled/required/readonly/name/form | 5.2 |
| RAD-DOM-10 | MUST | hidden input id = control id / none | 5.2 |
| RAD-DOM-11 | MUST | `value` attribute = serializeValue; absent when undefined | 5.2 |
| RAD-DOM-12 | MUST | visually-hidden styles (two variants) | 5.2 |
| RAD-DOM-13 | MUST | no describedby/invalid on hidden input | 5.2 |
| RAD-DOM-14 | MUST | no form attributes on root | 5.2 |
| RAD-DOM-15 | MUST | Indicator carries root data attributes + transition; no hidden | 5.3 |
| RAD-ARIA-01 | MUST | role radio | 5.4 |
| RAD-ARIA-02 | MUST | aria-checked | 5.4 |
| RAD-ARIA-03 | MUST | aria-labelledby precedence; aria-label suppresses | 5.4 |
| RAD-ARIA-04 | MUST | native label fallback discovery; re-evaluated | 5.4 |
| RAD-ARIA-05 | MUST | fallback disabled when native | 5.4 |
| RAD-ARIA-06 | MUST | aria-disabled (non-native) vs `disabled` (native) | 5.4 |
| RAD-ARIA-07 | MUST | aria-describedby = consumer + root scope + item scope | 5.4 |
| RAD-ARIA-08 | MUST | aria-invalid in group within Field only | 5.4 |
| RAD-ARIA-09 | MUST | no aria-required/aria-readonly | 5.4 |
| RAD-KEY-01 | MUST | Space: keydown prevents, keyup selects (non-native) | 6.1 |
| RAD-KEY-02 | MUST | Enter prevented, never selects | 6.1 |
| RAD-KEY-03 | MUST | native: Space not intercepted | 6.1 |
| RAD-KEY-04 | MUST | arrow keys not handled by radio | 6.1 |
| RAD-KEY-05 | MUST | Tab not intercepted | 6.1 |
| RAD-KEY-06 | MUST | disabled keys inert; readOnly ignores Space click | 6.1 |
| RAD-KEY-07 | MUST | modifiers carried to change event | 6.1 |
| RAD-KEY-08 | MUST | descendant key events ignored | 6.1 |
| RAD-KEY-09 | MUST | consumer keyup preventDefault cancels Space | 6.1 |
| RAD-PTR-01 | MUST | root click → dispatched input click with modifiers | 6.2 |
| RAD-PTR-02 | MUST | root click preventDefault; only if input mounted; ignore pre-prevented | 6.2 |
| RAD-PTR-03 | MUST | input click does not propagate | 6.2 |
| RAD-PTR-04 | MUST | direct input click selects | 6.2 |
| RAD-PTR-05 | MUST | wrapping native label selects once | 6.2 |
| RAD-PTR-06 | MUST | `htmlFor` label selects (both modes) | 6.2 |
| RAD-PTR-07 | MUST | readOnly: click ignored; input change reverted | 6.2 |
| RAD-PTR-08 | MUST | disabled: click/pointerdown prevented; no consumer handlers | 6.2 |
| RAD-PTR-09 | MUST | consumer onClick first; stopPropagation keeps selection; preventDefault cancels | 6.2 |
| RAD-PTR-10 | MUST | non-native Field.Label click focuses only | 6.2 |
| RAD-PTR-11 | MUST | pre-canceled input click ignored | 6.2 |
| RAD-FOC-01 | MUST | tabindex: roving in group; 0/-1 standalone; native none | 6.3 |
| RAD-FOC-02 | MUST | hidden input focus → root | 6.3 |
| RAD-FOC-03 | MUST | field focused on focus unless disabled; blur clears standalone only | 6.3 |
| RAD-FOC-04 | MUST | focused ownership on disable/unmount/re-enable | 6.3 |
| RAD-FOC-05 | MUST | focus-driven selection when navigation flag set | 6.3 |
| RAD-FOC-06 | MUST | focus makes it the highlighted item | 6.3 |
| RAD-EVT-01 | MUST | input change → details 'none' → group request → touched | 7 |
| RAD-EVT-02 | MUST | standalone: no-op request; touched still set | 7 |
| RAD-EVT-03 | MUST | undefined value ignored | 7 |
| RAD-FRM-01 | MUST | native participation rules | 8 |
| RAD-FRM-02 | MUST | serializeValue rules | 8 |
| RAD-FRM-03 | MUST | validation registry entry with root control | 8 |
| RAD-FRM-04 | MUST | group registration for inputRef; re-register on change. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 8 |
| RAD-FRM-05 | MUST | touched on selection | 8 |
| RAD-FRM-06 | MUST | Field.Label `for`/aria-labelledby; click selects | 8 |
| RAD-FRM-07 | MUST | form reset ignored | 8 |
| RAD-ANI-01 | MUST | Indicator rendered iff keepMounted or mounted | 9 |
| RAD-ANI-02 | MUST | starting/ending status and attributes | 9 |
| RAD-ANI-03 | MUST | unmount on animations finished; abort on re-check; batch | 9 |
| RAD-ANI-04 | MUST | keepMounted: stays, no hidden, attributes still cycle | 9 |
| RAD-ANI-05 | MUST | no completion callback/actions; animations-disabled flag | 9 |
| RAD-EDGE-01 | MUST | Indicator outside Root throws (message) | 12 |
| RAD-EDGE-02 | SHOULD | nativeButton mismatch dev errors | 12 |
| RAD-EDGE-03 | MUST | standalone radio static | 12 |
| RAD-EDGE-04 | MUST | readOnly only blocks selection | 12 |
| RAD-EDGE-05 | MUST | blank aria-label treated as absent | 12 |
| RAD-EDGE-06 | MUST | no RTL/locale dependence | 12 |
| RAD-EDGE-07 | MUST | SSR markup matches; tabindex before publication | 12 |
| RAD-EDGE-08 | MUST | interaction before input mounts tolerated | 12 |
| RAD-EDGE-09 | MUST | native button wrapped in label via render callback | 12 |
| RAD-EDGE-10 | MUST | duplicate values all checked | 12 |
| RAD-EDGE-11 | MUST | Field.Item disabled propagates | 12 |
| RAD-DOM-17 | MUST | both parts pass RND conformance suite | 13 |
| RAD-DOM-18 | MUST | runtime `id` change re-targets input and fallback label | 13 |

## 16. Open questions

1. **Standalone semantics.** `RadioRoot.tsx:84` makes an ungrouped radio checked iff `value === ''` and `RadioRoot.tsx:65-67` make all group operations no-ops, so a standalone radio is a static control that still writes the field's `touched`/`focused`. The docs page never shows a standalone radio; only the conformance suites and two focus tests render one. Confirm whether a port must reproduce the `''` rule (RAD-STA-01) or may treat ungrouped radios as always unchecked.
   Resolved 2026-10-23 (M2 track 7): RAD-STA-01 is implemented as written; the Indicator's conformance run renders `<RadioRoot Value="">`, which is checked (`RadioIndicatorConformanceTests`; `RadioRootTests` "port: a standalone radio is checked when its value is the empty string, and a click never changes it").
2. **Disabled-and-checked re-registration.** `RadioRoot.tsx:109-112` skips group re-registration when a radio is both disabled and checked, with the comment-free call `registerInputRef(null)` being a no-op in the group (`RadioGroup.tsx:126-128`). The intent (do not make a disabled checked radio the `inputRef` target) is reproduced by RDG-STA-07, but a radio that becomes disabled while already forwarded stays forwarded until a sibling registers. Untested.
   Resolved 2026-10-23 by the `inputRef` withdrawal (ADR 02 Amendments 2026-10-22 item 1): RAD-API-06, RAD-FRM-04 and RDG-API-09, RDG-STA-07..10 are withdrawn, so there is no forwarding to keep.
3. **`data-composite-item-active` is a public attribute in practice.** It is emitted on every checked radio (`RadioRoot.tsx:133`) but absent from `RadioRootDataAttributes.ts` and the API outline. Documented as RAD-DOM-02 because the composite reads it; the outline cross-check in brief step 9 flags the omission.
   Recorded 2026-10-23 (M2 track 7): the marker stays out of the constants; `RadioRootDataAttributes` lists Base UI's eleven attributes in its order (`RadioTypeTests` "port: the data attribute constants are Base UI's, in its order"), and the root renders the marker as RAD-DOM-02 states.
4. **Native form reset.** As for Switch (SWT §16 item 1), `RadioRoot.tsx:276` relies on React's controlled-input machinery to re-sync the hidden inputs after a `reset`; React 18 and 19 differ. RAD-FRM-07 requires only that `checked` is stable; a port should also re-sync the inputs.
   Resolved 2026-10-23 (as switch §16 Q1 (b), chosen by the user 2026-10-22; FFI-FRM-30's dated note): the `dom` primitive re-asserts each hidden input's `checked` one task after the owning form's `reset` (`radio-group.spec.ts` "port: a native form reset keeps the selection and re-syncs the hidden inputs, so the next submit matches it").
5. **`readonly` attribute on a radio input** (`RadioRoot.tsx:195`) has no native effect for `type="radio"`; it is emitted anyway. Kept in RAD-DOM-09 as observable output.
6. **Focus event `defaultPrevented` guard** (`RadioRoot.tsx:159`): focus events are not cancelable, so this branch is unreachable in browsers; RAD-FOC-05 mentions it only for fidelity.
   Resolved 2026-10-23 by ADR 04 Amendments 2026-10-20: the `composite` module keeps the `defaultPrevented` check for fidelity, and a C# consumer's veto of the focus-driven selection is the decision point `select` (RAD-FOC-05's note).
7. **Enter on a native-button radio.** RAD-KEY-02 states that the prevented keydown cancels native Enter activation (`RadioRoot.tsx:135-141`); the only test ("should not select an item with Enter") uses the non-native mode. Verify in a real browser before relying on it for `nativeButton` ports.
   Answered 2026-10-23 (M2 track 7, measured in Chromium on both render modes): the `button` module's Enter suppression prevents the keydown's default, which cancels the native button's activation, so Enter does not select a native-button radio and Space does (`radio.spec.ts` "port: Enter on a native-button radio does not select it, and Space does"). RAD-KEY-02 holds as written.
8. **Hinted `form`/`name` props.** The assignment hinted `inputRef?` only; `Radio.Root` indeed has `inputRef` but no `name`/`form`/`checked` props (`RadioRoot.tsx:322-344`); the outline matches the source.
