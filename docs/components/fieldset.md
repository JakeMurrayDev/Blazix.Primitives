# Fieldset

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/fieldset/` |
| Tests | `packages/react/src/fieldset/**/*.test.tsx` (2 files) |
| Docs page | `docs/src/app/(docs)/react/components/fieldset/page.mdx` |
| Prefix | `FST` |
| Status | implemented |
| Revised | 2026-10-23 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) |

## 1. Overview

Fieldset groups several related form controls under one shared caption. The user sees a native `<fieldset>` (`Fieldset.Root`) whose accessible name is the text of `Fieldset.Legend`; nothing is interactive. Disabling the fieldset disables everything inside it: native controls through the `<fieldset disabled>` mechanism of the browser, and Base UI fields (`Field.Root`) through the ancestor-provided shared state that Fieldset publishes.

Unlike a native fieldset, the caption is **not** a `<legend>` element: `Fieldset.Legend` renders a `<div>` and the association is made with `aria-labelledby` on the root, which keeps the caption freely stylable and lets the root be substituted by a Base UI group root (`RadioGroup`, `CheckboxGroup`, `Slider.Root`) while the legend keeps naming it. Fieldset has no form value of its own and no callbacks.

No component in the library builds on Fieldset, and Fieldset builds on no other component. Two other components *consume* its shared state: `Field.Root` (disabled propagation, [FFI-STR-06](../architecture/field-and-form-infrastructure.md)) and `RadioGroup` (legend id as fallback label, [FFI-ARIA-09](../architecture/field-and-form-infrastructure.md)).

## 2. Anatomy

```
<Fieldset.Root>               required, 1          default element: fieldset
  <Fieldset.Legend>           optional, 0..1       default element: div
  …any content (Field.Root, controls, nested Fieldset.Root)
```

- **FST-STR-01** — MUST. `Fieldset.Root` MUST render a single element (default `<fieldset>`) and MUST make a *fieldset scope* — the pair (current legend id, effective disabled) — available to every descendant, including descendants of an element substituted through `render` (§3.1) and descendants rendered through a portal.
  Source: `fieldset/root/FieldsetRoot.tsx:34-57`, `fieldset/root/FieldsetRootContext.ts:4-10`. Test: [L2] `FieldsetRoot.test.tsx` "passes disabled to rendered Base UI roots"; `RadioGroup.test.tsx` "labels the radio group from the fieldset legend".
  Port note 2026-10-22 (ADR 13 Amendments 2026-10-22, re-cited): the `FieldsetRoot.test.tsx` title substitutes RadioGroup, CheckboxGroup and Slider, which arrive later. Its port title "passes disabled to a rendered Button root, and the scope reaches the fields inside it" [L2] substitutes `Button`, and a `FieldRoot` inside the substituted root reads the scope. The scope is a fixed cascade around the root's element, so it reaches a substituted component and its descendants (design point DP1 of M2 track 1, settled by the orchestrator 2026-10-22 under ADR 05 §1 and ADR 16 rule 9). The `RadioGroup.test.tsx` clause is open to track 8.
- **FST-STR-02** — MUST. `Fieldset.Legend` MUST be rendered inside the subtree of a `Fieldset.Root` (at any depth); rendering it outside one MUST throw the error in FST-EDGE-01.
  Source: `fieldset/legend/FieldsetLegend.tsx:20`, `fieldset/root/FieldsetRootContext.ts:12-21`. Test: [L2] `FieldsetLegend.test.tsx` "throws a descriptive error when rendered outside <Fieldset.Root>".
- **FST-STR-03** — MUST. `Fieldset.Root` may be nested inside another `Fieldset.Root`; the inner root's effective disabled state MUST be the logical OR of the nearest enclosing root's effective disabled state and its own `disabled` prop (FST-API-01), so a disabled ancestor disables every nested fieldset regardless of their own props.
  Source: `fieldset/root/FieldsetRoot.tsx:27-28`. Test: [L2] `FieldsetRoot.test.tsx` "keeps nested fieldsets disabled when an ancestor fieldset is disabled", "updates nested disabled precedence in both directions".
- **FST-STR-04** — MUST. The legend is structural only: it MUST render an element carrying its `id`, the scope's state attributes and any forwarded attributes, and MUST NOT attach handlers or ARIA attributes of its own.
  Source: `fieldset/legend/FieldsetLegend.tsx:28-32`. Test: [L2] `FieldsetLegend.test.tsx` conformance suite "Base UI component API".

If Legend is omitted the root renders without `aria-labelledby` and has no accessible name unless the consumer supplies one (FST-DOM-04). A single Legend is the supported configuration; see FST-EDGE-03 for several.

## 3. Public API

### 3.1 `Fieldset.Root`

**Default element:** `<fieldset>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables the fieldset: rendered as the native `disabled` attribute and propagated to every nested `Fieldset.Root`, `Field.Root` and Base UI control. Combined (OR) with the enclosing `Fieldset.Root`'s effective disabled state. Not listed in the API outline (see §16). | FST-API-01 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | FST-API-02 |
| `aria-labelledby` | `string` | — (legend id) | Forwarded; when given it replaces the automatic legend association (FST-DOM-04). | FST-API-03 |
| *other `<fieldset>` attributes and handlers* | — | — | Forwarded to the root element. | FST-API-03 |

**Consumed (not forwarded to the element):** `disabled` (re-applied as the computed value), `className`, `style`, `render`.

- **FST-API-01** — MUST. `disabled` defaults to `false`. The effective disabled state MUST be `parentDisabled || disabled`, where `parentDisabled` is the effective disabled state of the nearest enclosing `Fieldset.Root` (`false` when there is none).
  Source: `fieldset/root/FieldsetRoot.tsx:21,27-28`. Test: [L2] `FieldsetRoot.test.tsx` "sets the native disabled attribute", "keeps nested fieldsets disabled when an ancestor fieldset is disabled".
- **FST-API-02** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md); the state object they receive is the one in "State object" below. None of the three is forwarded as a plain attribute.
  Source: `fieldset/root/FieldsetRoot.tsx:17-23,34-44`, `internals/types.ts:36-61`. Test: [L2] `FieldsetRoot.test.tsx` conformance suite "Base UI component API".
- **FST-API-03** — MUST. Every prop other than the four consumed ones MUST be forwarded to the root element, merged after the built-in `aria-labelledby` and `disabled` attributes, so a consumer value for the same attribute name wins (RND-DOM-19; `disabled` cannot collide because it is consumed).
  Source: `fieldset/root/FieldsetRoot.tsx:22,37-43`. Test: [L2] `FieldsetRoot.test.tsx` conformance suite "Base UI component API" (`data-testid`, custom attributes).

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes.

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled state (FST-API-01). |

- **FST-STA-01** — MUST. The root's state object MUST contain exactly the one field above, holding the *effective* disabled value, not the raw prop.
  Source: `fieldset/root/FieldsetRoot.tsx:30-32,60-65`. Test: [L2] `FieldsetRoot.test.tsx` "updates nested disabled precedence in both directions" (nested root with `disabled={false}` under a disabled ancestor reports disabled).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effectively disabled | FST-DOM-02 |

The API outline and the generated docs list no data attributes for Root, and no `FieldsetRootDataAttributes` namespace is exported; the attribute nevertheless follows from the state object (§16).

#### CSS variables

None.

### 3.2 `Fieldset.Legend`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated `base-ui-…` | The legend element's id; also what the root's `aria-labelledby` points at. Not listed in the API outline. | FST-API-04 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the state object below. | FST-API-05 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the legend element. | FST-API-05 |

**Consumed (not forwarded to the element):** `id` (re-applied as the resolved id), `className`, `style`, `render`.

- **FST-API-04** — MUST. When `id` is supplied it MUST be used verbatim as the legend element's id and as the value published to the root; when omitted a generated id of the form `base-ui-<token>` (RND-DOM-27…29) MUST be used.
  Source: `fieldset/legend/FieldsetLegend.tsx:18,22`, `utils/useRegisteredLabelId.ts:10`, `internals/useBaseUiId.ts:9-11`. Test: [L2] `FieldsetLegend.test.tsx` "should set aria-labelledby on the fieldset with custom id", "should set aria-labelledby on the fieldset automatically".
- **FST-API-05** — MUST. All Legend props other than `id`, `className`, `style`, `render` MUST be forwarded to the legend element, merged after the built-in `id`; the three customization props MUST behave per RND using the Legend state object.
  Source: `fieldset/legend/FieldsetLegend.tsx:18,28-32`. Test: [L2] `FieldsetLegend.test.tsx` conformance suite "Base UI component API"; "reflects the disabled state" (`data-testid` forwarded).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The enclosing root's effective disabled state. |

- **FST-STA-02** — MUST. The legend's state object MUST contain exactly the one field above, equal at all times to the enclosing `Fieldset.Root`'s effective disabled value (FST-API-01).
  Source: `fieldset/legend/FieldsetLegend.tsx:20,24-26,37-42`. Test: [L2] `FieldsetLegend.test.tsx` "reflects the disabled state".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while the fieldset is effectively disabled | FST-DOM-06 |

#### CSS variables

None.

### 3.3 Shared types

- `Fieldset.Root.State` (`FieldsetRootState`) — `{ disabled: boolean }`, §3.1. Source: `fieldset/root/FieldsetRoot.tsx:60-65,69-72`.
- `Fieldset.Root.Props` (`FieldsetRootProps`) — the `<fieldset>` attributes plus `className`/`style`/`render` typed against `Fieldset.Root.State`. Source: `fieldset/root/FieldsetRoot.tsx:67`.
- `Fieldset.Legend.State` (`FieldsetLegendState`) — `{ disabled: boolean }`, §3.2. Source: `fieldset/legend/FieldsetLegend.tsx:37-42,46-49`.
- `Fieldset.Legend.Props` (`FieldsetLegendProps`) — the `<div>` attributes plus the customization props. Source: `fieldset/legend/FieldsetLegend.tsx:44`.
- Exported constant namespace `FieldsetLegendDataAttributes` = `{ disabled: 'data-disabled' }`. There is no `FieldsetRootDataAttributes` export. Source: `fieldset/legend/FieldsetLegendDataAttributes.ts:1-4`, `fieldset/index.ts:6`.
- Fieldset defines no reason literals and no event-details type (it has no callbacks).

## 4. State model

Fieldset owns two pieces of state, both held by `Fieldset.Root`:

1. **Effective disabled** — derived, never stored: `parentDisabled || disabledProp` (FST-API-01). It is recomputed on every render, so changing either input updates the root element, the legend, nested fieldsets and nested fields within the same update.
2. **Current legend id** — the id most recently published by a mounted `Fieldset.Legend`, or absent. It is written by the legend post-commit, pre-paint (FST-DOM-07) and read by the root (`aria-labelledby`) and by `RadioGroup` (FST-FRM-03).

There is no controlled/uncontrolled pair, no value normalization and no mount/unmount lifecycle: both parts are always rendered.

- **FST-STA-03** — MUST. The effective disabled state MUST follow every later change of the `disabled` prop of the root itself or of any enclosing root, in both directions: a nested root that becomes enabled while an ancestor is disabled stays disabled, and it becomes enabled as soon as the last disabled ancestor is enabled.
  Source: `fieldset/root/FieldsetRoot.tsx:27-28`. Test: [L2] `FieldsetRoot.test.tsx` "updates nested disabled precedence in both directions".

## 5. Rendered DOM and ARIA

### 5.1 `Fieldset.Root` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `disabled` | present (empty) while effectively disabled; absent otherwise | FST-DOM-01 | C# |
| `data-disabled` | `""` while effectively disabled | FST-DOM-02 | C# |
| `aria-labelledby` | the current legend id; absent while no legend is registered; consumer value wins | FST-DOM-03, FST-DOM-04 | C# |
| `role` | none set explicitly (`<fieldset>` has the implicit `group` role) | FST-ARIA-01 | C# |
| *consumer attributes* | forwarded | FST-API-03 | C# |

- **FST-DOM-01** — MUST. The root element MUST carry the native `disabled` attribute exactly while the effective disabled state is `true`. When the rendered element is a `<fieldset>`, this makes the browser disable every native form control inside it (FST-FRM-02).
  Source: `fieldset/root/FieldsetRoot.tsx:40`. Test: [L2] `FieldsetRoot.test.tsx` "sets the native disabled attribute".
- **FST-DOM-02** — MUST. The root element MUST carry `data-disabled=""` exactly while the effective disabled state is `true` (default state projection, RND-DOM-01/02).
  Source: `fieldset/root/FieldsetRoot.tsx:30-32,36`, `internals/getStateAttributesProps.ts:24-26`. Test: none (the attribute is only asserted on `Field.Root` and controls in the suite).
  Port note 2026-10-22: the port's L2 translation of "sets the native disabled attribute" also asserts `data-disabled`.
- **FST-DOM-03** — MUST. While a `Fieldset.Legend` is mounted inside the root, the root element MUST carry `aria-labelledby` equal to that legend's id (FST-API-04); while none is mounted the attribute MUST be absent.
  Source: `fieldset/root/FieldsetRoot.tsx:25,39`. Test: [L2] `FieldsetLegend.test.tsx` "should set aria-labelledby on the fieldset automatically", "should set aria-labelledby on the fieldset with custom id", "updates and clears the legend association".
- **FST-DOM-04** — MUST. A consumer-supplied `aria-labelledby` on `Fieldset.Root` MUST replace the legend-derived value (it is a later source, RND-DOM-19); the legend still registers its id and still names a substituted `RadioGroup` through the scope (FST-FRM-03).
  Source: `fieldset/root/FieldsetRoot.tsx:37-43`. Test: [L2] port title "a consumer aria-labelledby replaces the legend's id" (added 2026-10-22).

### 5.2 `Fieldset.Legend` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | the `id` prop, else generated | FST-DOM-05 | C# |
| `data-disabled` | `""` while the fieldset is effectively disabled | FST-DOM-06 | C# |
| *consumer attributes* | forwarded | FST-API-05 | C# |

- **FST-DOM-05** — MUST. The legend element MUST always carry an `id` attribute: the `id` prop when given, otherwise the generated id. The generated id MUST be identical in server-rendered markup and on the client (RND-DOM-29).
  Source: `fieldset/legend/FieldsetLegend.tsx:22,31`, `packages/utils/src/useId.ts:42-47`. Test: [L2] `FieldsetLegend.test.tsx` "sets `aria-labelledby` after hydration without a custom legend id" (`legend.id` non-empty in server markup). (L4 for the server-markup-equals-client clause)
- **FST-DOM-06** — MUST. The legend element MUST carry `data-disabled=""` exactly while the fieldset is effectively disabled.
  Source: `fieldset/legend/FieldsetLegend.tsx:24-26,29`, `fieldset/legend/FieldsetLegendDataAttributes.ts:4`. Test: [L2] `FieldsetLegend.test.tsx` "reflects the disabled state".
- **FST-DOM-07** — MUST. The legend MUST publish its id to the root post-commit, pre-paint when it mounts and whenever its id changes, and MUST withdraw it when it unmounts; withdrawal only clears the root's value if that value is still the withdrawing legend's id (FFI-ARIA-08). Consequently a change of the `id` prop updates the root's `aria-labelledby` within the same update, and removing the legend removes the attribute. Server: relaxed, see §9.
  Source: `utils/useRegisteredLabelId.ts:12-17`, `fieldset/root/FieldsetRoot.tsx:25,49`; see [FFI-ARIA-08](../architecture/field-and-form-infrastructure.md). Test: [L2] `FieldsetLegend.test.tsx` "updates and clears the legend association".
  Port note 2026-10-22 (ADR 01 Amendments 2026-10-22, "label and legend ids publish in the creating batch"): the legend publishes its id in `OnParametersSet` and withdraws it when disposed, inside the render batch that creates, re-ids or removes it, and the root renders again in that batch. The row is therefore met on both modes, not relaxed; tested at L4 on both (`fieldset.spec.ts` "updates and clears the legend association").
- **FST-DOM-08** — MUST. Server-rendered markup MUST NOT contain `aria-labelledby` on the root (the legend has not registered yet), even when a legend is present; after hydration the association MUST appear (post-commit, pre-paint of the hydrating update) pointing at the legend's id, which is unchanged by hydration. Server: relaxed, see §9.
  Source: see [FFI-FRM-34](../architecture/field-and-form-infrastructure.md); `utils/useRegisteredLabelId.ts:12-13`. Test: [L2] `FieldsetLegend.test.tsx` "does not set `aria-labelledby` during SSR when legend is absent", "sets `aria-labelledby` after hydration without a custom legend id". (L4 for the after-hydration clause)
  Port note 2026-10-22 (ADR 01 Amendments 2026-10-22): the legend publishes while it initializes, so the server-rendered markup of a prerendered page already carries the root's `aria-labelledby`, pointing at the same generated id the interactive render keeps. This is the improvement the amendment chose. The clause "MUST NOT contain `aria-labelledby` ... even when a legend is present" does not hold for the port, and Base UI's tests assert absence only where no legend is rendered. Tested at L4 on both modes (`fieldset.spec.ts`, the two SSR titles over the prerendered `fieldset-prerender` page) and at L2.
- **FST-DOM-09** — MUST. When `Fieldset.Root` is substituted (`render`) with a component, that component MUST receive the computed `disabled` (boolean), the current `aria-labelledby`, the state data attribute and the forwarded attributes as its parameters, and it is rendered inside the fieldset scope (FST-STR-01). A Base UI group root used this way therefore reflects the fieldset's disabled state through its own mechanism (`RadioGroup` → `aria-disabled="true"`; `CheckboxGroup` → member checkboxes `data-disabled`; `Slider.Root` → `data-disabled` on its parts). A substituted plain element that is not a `<fieldset>` receives a `disabled` attribute with no native effect; nested `Field.Root`s are still disabled through the scope (FST-FRM-01).
  Source: `fieldset/root/FieldsetRoot.tsx:34-44,55-57`; see [RND-DOM-38](../architecture/rendering-and-composition.md), [RND-DOM-43](../architecture/rendering-and-composition.md). Test: [L2] `FieldsetRoot.test.tsx` "passes disabled to rendered Base UI roots".
  Port note 2026-10-22 (ADR 13 Amendments 2026-10-22, re-cited): "passes disabled to rendered Base UI roots" renders RadioGroup, CheckboxGroup and Slider. The port title "passes disabled to a rendered Button root, and the scope reaches the fields inside it" [L2] substitutes `Button`: the spread `disabled` binds to `Button.Disabled`, since Blazor matches component parameters by name case-insensitively. The RadioGroup and CheckboxGroup clauses are open to tracks 8 and 6, the Slider clause to M5.

### 5.3 ARIA

- **FST-ARIA-01** — MUST. `Fieldset.Root` MUST NOT set an explicit `role`; the default `<fieldset>` element exposes the implicit `group` role, which is what the association via `aria-labelledby` names. When the root is substituted with another element the implementation adds no role (a substituted `RadioGroup` keeps its own `radiogroup` role).
  Source: `fieldset/root/FieldsetRoot.tsx:34-44` (no `role`). Test: [L2] `FieldsetLegend.test.tsx` "should set aria-labelledby on the fieldset automatically" (queried by role `group`).
- **FST-ARIA-02** — MUST. `Fieldset.Legend` MUST NOT set any `role` or `aria-*` attribute; it names the group solely by being the target of the root's `aria-labelledby` (FST-DOM-03), and it is not a native `<legend>` (so the browser's legend heuristics do not apply).
  Source: `fieldset/legend/FieldsetLegend.tsx:28-32`. Test: none.

## 6. Interactions

### 6.1 Keyboard

Not applicable — neither part attaches keyboard handlers; the browser's native handling of a `<fieldset disabled>` subtree (descendant controls not focusable) is the only keyboard effect and follows from FST-DOM-01.

### 6.2 Pointer and touch

Not applicable — no pointer handlers.

### 6.3 Focus management

Not applicable — Fieldset manages no focus; disabled native descendants are removed from the tab order by the browser (FST-DOM-01).

### 6.4 Dismissal

Not applicable — no popup.

## 7. Events and callbacks

Not applicable — Fieldset has no callback props and emits no event details (there are no reason literals; see §3.3).

## 8. Form integration

Fieldset has no `name`, no value and no validation of its own. Its form role is to *disable* a region and to *name* it. Mechanism: [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md).

- **FST-FRM-01** — MUST. Every `Field.Root` nested (at any depth) inside an effectively disabled `Fieldset.Root` MUST be disabled (FFI-STR-06, FFI-STA-11): its control renders disabled (e.g. the native `disabled` attribute on `Field.Control`), its registration with the surrounding `Form` is dropped so it is neither validated on submit nor present in the values object (FFI-FRM-17), its computed validity is neutralized (`aria-invalid` and `Field.Error` removed, FFI-STA-02), and all of this reverses when the fieldset is enabled again.
  Source: `field/root/FieldRoot.tsx:44,48`; see [FFI-STR-06](../architecture/field-and-form-infrastructure.md), [FFI-FRM-17](../architecture/field-and-form-infrastructure.md). Test: [L2] `FieldsetRoot.test.tsx` "keeps nested fieldsets disabled when an ancestor fieldset is disabled", "updates nested disabled precedence in both directions"; `Form.test.tsx` "excludes disabled fieldset fields from validation and onFormSubmit values", "clears invalid UI when a fieldset field becomes disabled".
- **FST-FRM-02** — MUST. When the root renders a native `<fieldset>`, plain native form controls inside it (no `Field.Root`) MUST be disabled by the browser while the fieldset is disabled (`:disabled` matches, they are excluded from form submission and constraint validation, FFI-FRM-23); this is a consequence of FST-DOM-01 and requires no additional attributes on the controls.
  Source: `fieldset/root/FieldsetRoot.tsx:34,40`. Test: [L4] `FieldsetRoot.test.tsx` "sets the native disabled attribute"; `CheckboxGroup.test.tsx` "omits checkboxes disabled by a fieldset", "skips a checkbox disabled by a fieldset when focusing Form errors".
- **FST-FRM-03** — MUST. The current legend id MUST be exposed through the fieldset scope so that a `RadioGroup` inside the fieldset (including one substituted for the root) uses it as its `aria-labelledby` when no `Field.Label` id is available (FFI-ARIA-09: `fieldLabelId ?? legendId`); replacing or unmounting the legend updates the group's attribute accordingly.
  Source: `fieldset/root/FieldsetRoot.tsx:46-53`, `radio-group/RadioGroup.tsx:68,185,229`; see [FFI-ARIA-09](../architecture/field-and-form-infrastructure.md). Test: [L2] `RadioGroup.test.tsx` "labels the radio group from the fieldset legend", "updates label precedence without retaining replaced or unmounted IDs".
- **FST-FRM-04** — MUST. Fieldset MUST NOT add anything to native `FormData` or to the `Form` values object, MUST NOT register with the form scope, and MUST NOT alter the `name` of nested controls.
  Source: `fieldset/root/FieldsetRoot.tsx:1-58` (no form-scope or field-scope interaction). Test: [L2] `Form.test.tsx` "excludes disabled fieldset fields from validation and onFormSubmit values" (values contain only the enabled field).

## 9. Lifecycle, mounting and animation

Not applicable — both parts are always mounted while rendered; no transition statuses, `keepMounted` or `hidden="until-found"` behavior exists. The only timed behavior is the legend registration (post-commit, pre-paint; FST-DOM-07).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- FST-DOM-07 — FFI registration row (post-commit, pre-paint C# state work): the legend's id publication and withdrawal run in `OnAfterRender`; on Server the root's `aria-labelledby` update lands after paint plus a round trip, observable only as latency of that attribute.
- FST-DOM-08 — FFI registration row: after hydration (the first interactive render) the association appears after paint plus a round trip on Server.
- 2026-10-22 (ADR 01 Amendments 2026-10-22): both rows are met on both modes in the port. The legend publishes in `OnParametersSet`, inside the creating batch, and the association is already in prerendered markup; see the port notes on FST-DOM-07 and FST-DOM-08.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` | [rendering-and-composition](../architecture/rendering-and-composition.md) | Root: tag `fieldset`, state `{ disabled }`, props `[ { 'aria-labelledby': legendId, disabled }, consumerProps ]`, default state projection. Legend: tag `div`, state `{ disabled }`, props `[ { id }, consumerProps ]`. (`FieldsetRoot.tsx:34-44`, `FieldsetLegend.tsx:28-32`) |
| `useRegisteredLabelId` (label id publication) | [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) §6.2, FFI-ARIA-08 | Legend passes `(idProp, setLegendId)`; the root's `legendId` state is the publication target. (`FieldsetLegend.tsx:22`) |
| `useBaseUiId` / `useId` | [rendering-and-composition](../architecture/rendering-and-composition.md) RND-DOM-27…30 | Legend id generation with the `base-ui` prefix. (`utils/useRegisteredLabelId.ts:10`) |
| Fieldset scope consumers | [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) FFI-STR-06, FFI-STA-11, FFI-ARIA-09 | `Field.Root` reads `disabled` (`field/root/FieldRoot.tsx:44`); `RadioGroup` reads `legendId` (`radio-group/RadioGroup.tsx:68,185`). Both use the optional lookup and never throw. |

## 12. Edge cases, invariants and dev warnings

- **FST-EDGE-01** — MUST. Rendering `Fieldset.Legend` outside a `Fieldset.Root` MUST throw an `Error` whose message is exactly `Base UI: FieldsetRootContext is missing. Fieldset parts must be placed within <Fieldset.Root>.` This is thrown in every build (it is not a dev-only diagnostic) and is the only diagnostic Fieldset emits.
  Source: `fieldset/root/FieldsetRootContext.ts:14-20`, `fieldset/legend/FieldsetLegend.tsx:20`. Test: [L2] `FieldsetLegend.test.tsx` "throws a descriptive error when rendered outside <Fieldset.Root>".
  Port note 2026-10-22 (PRG-EDGE-01 precedent, ADR 08 Amendments 2026-10-20): the message is ``Blazix: The `FieldsetRoot` context is missing. Fieldset parts must be placed within `<FieldsetRoot>`.``, an `InvalidOperationException` thrown in every build.
- **FST-EDGE-02** — MUST. `Fieldset.Root` itself MUST NOT throw when it has no enclosing `Fieldset.Root` (it performs the optional lookup); its effective disabled state is then simply its own `disabled` prop.
  Source: `fieldset/root/FieldsetRoot.tsx:27`, `fieldset/root/FieldsetRootContext.ts:12,16`. Test: [L2] `FieldsetRoot.test.tsx` (every test renders a top-level root).
- **FST-EDGE-03** — MUST. If several `Fieldset.Legend`s are mounted under one root, the root's `aria-labelledby` MUST be the id of the legend whose publication ran last (for legends mounted in the same update: the last in document order), and unmounting a legend whose id is not the current one MUST NOT clear the attribute (FFI-ARIA-08).
  Source: `utils/useRegisteredLabelId.ts:12-17`. Test: [L2] `useRegisteredLabelId.test.tsx` "does not let an older label cleanup clear a newer label", translated over two legends (added 2026-10-22); [L4] the same title in `fieldset.spec.ts`.
- **FST-EDGE-04** — MUST. A legend `id` of the empty string is used verbatim (RND-DOM-28): the legend renders `id=""` and the root renders `aria-labelledby=""`. No normalization is applied.
  Source: `utils/useRegisteredLabelId.ts:10`, `packages/utils/src/useId.ts:46`. Test: [L2] port title "an empty legend id is used verbatim" (added 2026-10-22).

**Behavior under disabled.** Nothing in Fieldset reacts to interaction, so `disabled` has no interaction-related effect of its own beyond FST-DOM-01/02 and the propagation in §8. There is no `readOnly`.

**RTL / server rendering.** Direction-independent. Server rendering differs only in the absence of `aria-labelledby` before hydration (FST-DOM-08).

**Missing parts.** A root without a legend is valid (FST-DOM-03). A root with no children renders an empty `<fieldset>`.

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. One ID is introduced here for the shared conformance suite.

### `FieldsetRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLFieldSetElement`, default `render`, default `testRenderPropWith: 'div'`) | FST-DOM-10, FST-API-02, FST-API-03 |
| "sets the native disabled attribute" | FST-API-01, FST-DOM-01, FST-FRM-02 |
| "keeps nested fieldsets disabled when an ancestor fieldset is disabled" | FST-STR-03, FST-API-01, FST-FRM-01 |
| "updates nested disabled precedence in both directions" | FST-STR-03, FST-STA-01, FST-STA-03, FST-FRM-01 |
| "passes disabled to rendered Base UI roots" | FST-STR-01, FST-DOM-09 |

### `FieldsetLegend.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`, `render` wraps the node in `<Fieldset.Root>`) | FST-DOM-10, FST-STR-04, FST-API-05 |
| "should set aria-labelledby on the fieldset automatically" | FST-API-04, FST-DOM-03, FST-ARIA-01 |
| "should set aria-labelledby on the fieldset with custom id" | FST-API-04, FST-DOM-03 |
| "reflects the disabled state" | FST-STA-02, FST-DOM-06 |
| "updates and clears the legend association" | FST-DOM-03, FST-DOM-07 |
| "throws a descriptive error when rendered outside <Fieldset.Root>" | FST-STR-02, FST-EDGE-01 |
| "does not set `aria-labelledby` during SSR when legend is absent" (browser only) | FST-DOM-08 |
| "sets `aria-labelledby` after hydration without a custom legend id" (browser only) | FST-DOM-05, FST-DOM-08 |

### Tests in other suites that exercise Fieldset

| Test | Requirements |
|---|---|
| `Form.test.tsx` "excludes disabled fieldset fields from validation and onFormSubmit values" / "clears invalid UI when a fieldset field becomes disabled" | FST-FRM-01, FST-FRM-04 |
| `RadioGroup.test.tsx` "labels the radio group from the fieldset legend" / "updates label precedence without retaining replaced or unmounted IDs" | FST-STR-01, FST-FRM-03 |
| `CheckboxGroup.test.tsx` "omits checkboxes disabled by a fieldset" / "skips a checkbox disabled by a fieldset when focusing Form errors" | FST-FRM-02 |

- **FST-DOM-10** — MUST. Both parts MUST pass the shared conformance suite of [RND §4.9](../architecture/rendering-and-composition.md) (RND-DOM-36…48): custom attributes forwarded to the default element and to a substituted `div` (element and callback forms), `style` from the part and from the substitute both applied, the DOM handle resolving to an `HTMLFieldSetElement` (Root) / `HTMLDivElement` (Legend), wrapper substitution allowed, both DOM handles receiving the element, and `className` string/function merged with the substitute's class. Neither part accepts `nativeButton`, so RND-DOM-49 does not apply.
  Source: `test/describeConformance.tsx:55-78`. Test: [L2] `FieldsetRoot.test.tsx` / `FieldsetLegend.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **The scope is a React context** (`FieldsetRootContext`, created with `undefined` as default). "Missing" is detected as the context value being `undefined`; the optional lookup (`useFieldsetRootContext(true)`) used by Root, `Field.Root` and `RadioGroup` returns `undefined` instead of throwing. A port needs an ancestor lookup that follows the *logical* tree (portals included) and distinguishes "no fieldset" from "fieldset with no legend". Port: see decisions/04-js-interop-boundary.md §1 (field and form infrastructure is C#) and §5 (portals are DOM moves after first render, so the C# ancestor lookup follows the logical tree).
- **Legend registration runs in a layout effect** (`useIsoLayoutEffect` in `useRegisteredLabelId`), so the root re-renders synchronously before paint with the new `aria-labelledby`; the cleanup uses a functional state update (`currentId === id ? undefined : currentId`) to avoid clearing a newer legend's id. Ports must run the registration after the DOM update and before paint, and must run an unmounting legend's withdrawal *before* a replacement legend's registration when both happen in one update (React runs cleanups first), which is what the RadioGroup precedence test relies on. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table ("FFI registration" row: on Server the association appears after paint plus a round trip).
- **Server rendering.** The legend's id is generated with `React.useId` (prefixed `base-ui-`), so it is identical on server and client; the root's `aria-labelledby` only appears after the registration effect runs on the client. There is no hydration mismatch because the server markup never carries the attribute. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 (scoped `base-ui-<n>` generator; prerender and interactive passes agree when they render the same tree in the same order).
- **`disabled` on a substituted component.** `useRenderElement` clones the `render` element with the merged props, so a `RadioGroup`/`CheckboxGroup`/`Slider.Root` substitute receives `disabled` as a *prop* (and reads the legend id from context). A port that substitutes elements by composition must forward the computed boolean to the substitute's own disabled parameter, not just set an attribute. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 1 (a component-frame root receives the merged attributes as its parameters).
- **React 17 id fallback.** On React 17 the generated legend id is `undefined` on the first render and assigned after mount (`packages/utils/src/useId.ts:9-31`); `useRegisteredLabelId` then publishes `undefined` first and the real id on the next effect run. Ports with synchronous id generation can ignore this. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5 (ids are drawn at first render, so there is no undefined-first phase).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| FST-STR-01 | MUST | Root renders one element and provides the fieldset scope to all descendants incl. substitutes/portals | 2 |
| FST-STR-02 | MUST | Legend must be inside a Root; otherwise throws FST-EDGE-01 | 2 |
| FST-STR-03 | MUST | Roots nest; effective disabled = ancestor OR own | 2 |
| FST-STR-04 | MUST | Legend is structural only | 2 |
| FST-API-01 | MUST | `disabled` default `false`; effective = parent OR prop | 3.1 |
| FST-API-02 | MUST | Root `className`/`style`/`render` per RND with Root state | 3.1 |
| FST-API-03 | MUST | Other Root props forwarded; consumer attribute wins | 3.1 |
| FST-API-04 | MUST | Legend `id` verbatim, else generated `base-ui-…` | 3.2 |
| FST-API-05 | MUST | Other Legend props forwarded; customization per RND | 3.2 |
| FST-STA-01 | MUST | Root state is exactly `{ disabled }` (effective) | 3.1 |
| FST-STA-02 | MUST | Legend state is exactly `{ disabled }` = root's effective | 3.2 |
| FST-STA-03 | MUST | Effective disabled tracks later prop/ancestor changes both ways | 4 |
| FST-DOM-01 | MUST | Native `disabled` attribute on root iff effectively disabled | 5.1 |
| FST-DOM-02 | MUST | `data-disabled` on root iff effectively disabled | 5.1 |
| FST-DOM-03 | MUST | Root `aria-labelledby` = mounted legend's id; absent otherwise | 5.1 |
| FST-DOM-04 | MUST | Consumer `aria-labelledby` replaces legend value | 5.1 |
| FST-DOM-05 | MUST | Legend always has an `id`; generated id stable across SSR/hydration | 5.2 |
| FST-DOM-06 | MUST | `data-disabled` on legend iff fieldset effectively disabled | 5.2 |
| FST-DOM-07 | MUST | Legend publishes id post-commit pre-paint; withdraws only if current | 5.2 |
| FST-DOM-08 | MUST | No `aria-labelledby` in server markup; appears after hydration | 5.2 |
| FST-DOM-09 | MUST | Substitute receives `disabled`, `aria-labelledby`, data attrs; rendered in scope | 5.2 |
| FST-DOM-10 | MUST | Both parts pass the RND conformance suite | 13 |
| FST-ARIA-01 | MUST | No explicit role; `<fieldset>` implicit `group` | 5.3 |
| FST-ARIA-02 | MUST | Legend sets no role/aria attributes; not a `<legend>` | 5.3 |
| FST-FRM-01 | MUST | Nested `Field.Root`s disabled: control disabled, unregistered, validity neutralized; reversible | 8 |
| FST-FRM-02 | MUST | Native descendants disabled by the browser via `<fieldset disabled>` | 8 |
| FST-FRM-03 | MUST | Legend id is `RadioGroup`'s fallback `aria-labelledby` | 8 |
| FST-FRM-04 | MUST | Contributes nothing to FormData/values; no registration; no name change | 8 |
| FST-EDGE-01 | MUST | Missing-root error text (verbatim) | 12 |
| FST-EDGE-02 | MUST | Root never throws without an enclosing Root | 12 |
| FST-EDGE-03 | MUST | Several legends: last registered wins; stale withdrawal ignored | 12 |
| FST-EDGE-04 | MUST | Empty-string legend id used verbatim | 12 |

## 16. Open questions

1. **`disabled` and `data-disabled` missing from the API outline.** `docs/src/app/(docs)/react/components/page.mdx:864-865` lists only `className, render, style` for `Fieldset.Root` and no data attributes, and `fieldset/types.md:14-18` likewise; yet `disabled` is a documented-by-JSDoc state field (`FieldsetRoot.tsx:60-65`) consumed as a prop (`:21`), and `data-disabled` is emitted by the default projection (`:30-36`). There is also no `FieldsetRootDataAttributes.ts` to mirror `FieldsetLegendDataAttributes.ts`. Documented as FST-API-01 / FST-DOM-02 (Test: none for the attribute); the outline omission looks like a generator limitation (native attributes are not listed), not a behavioral difference.
   Resolved 2026-10-22 (M2 track 1): the port exposes `FieldsetRoot.Disabled` and renders `data-disabled`, and the L2 translation of "sets the native disabled attribute" asserts both; no `FieldsetRootDataAttributes` class, as Base UI exports none (ADR 08).
2. **Legend `id` prop not in the outline** for the same reason (`FieldsetLegend.tsx:18` intercepts it). Documented as FST-API-04.
   Resolved 2026-10-22: `FieldsetLegend` takes `id` from the forwarded attributes verbatim (FST-API-04), as every part takes `id`.
3. **Multiple legends** (FST-EDGE-03) and **empty-string id** (FST-EDGE-04) follow from `utils/useRegisteredLabelId.ts:10-17` but are untested; a port may reasonably treat both as unsupported configurations.
   Resolved 2026-10-22: the port follows the source for both. The last publication names the root, and an older legend's withdrawal leaves a newer id in place (`LabelIdSlot`); an empty id is used verbatim. Both are now tested by L2 titles (FST-EDGE-03, FST-EDGE-04).
4. **Consumer `aria-labelledby` override** (FST-DOM-04) follows from the prop order at `FieldsetRoot.tsx:37-43` and RND-DOM-19 but is untested; note that per RND-DOM-19 an explicit `aria-labelledby={undefined}` would also suppress the legend association. Resolved (explicit-`undefined` clause only): not expressible in the port, see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-19); the untested override itself stays open.
   Resolved 2026-10-22: the consumer's `aria-labelledby` replaces the legend's id (the pipeline merges the consumer last); tested by an L2 port title (FST-DOM-04). The root then skips re-rendering for legend id changes nothing would show.
5. **Non-`<fieldset>` substitution.** With `render={<div />}` the root renders `<div disabled="">` — an attribute with no native meaning — and no `group` role (`FieldsetRoot.tsx:34-44`); nested `Field.Root`s are still disabled through the scope. Whether a port should add `role="group"` in that case is a product decision; Base UI does not.
   Resolved 2026-10-22: the port follows Base UI and adds no role to a non-`<fieldset>` substitute.
6. **Changelog intent.** `CHANGELOG.md` v1.0.0-beta.3 "Fix disabled fieldset form bugs (#4890)" introduced the registration drop and validity neutralization tested by `Form.test.tsx` "excludes disabled fieldset fields…" / "clears invalid UI when a fieldset field becomes disabled"; the behavior itself lives in `Field.Root` (FFI-STR-06), so it is cited rather than restated here.
   Resolved 2026-10-22: informational; the behaviour is `Field.Root`'s (FFI-STR-06), and its `Form.test.tsx` titles land with track 2 and PR 1c.
