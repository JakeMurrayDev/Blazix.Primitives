# Switch

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/switch/` |
| Tests | `packages/react/src/switch/**/*.test.tsx` (2 files) |
| Docs page | `docs/src/app/(docs)/react/components/switch/page.mdx` |
| Prefix | `SWT` |
| Status | implemented |
| Revised | 2026-10-23 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Switch is a two-state (on/off) control, the WAI-ARIA `switch` role. The user sees a track (`Switch.Root`) with a movable knob (`Switch.Thumb`); activating the control with a pointer, <kbd>Space</kbd>, <kbd>Enter</kbd>, or an associated `<label>` flips it between checked and unchecked. Unlike Checkbox there is no indeterminate state and no group part; unlike Toggle it participates in forms as a native checkbox would.

The visible root is a `<span>` by default (so it can sit inside a `<label>`), paired with a visually hidden native `<input type="checkbox">` rendered as its next sibling. The hidden input is the single source of form participation, native constraint validation, and label activation: every user-initiated toggle is routed through a click on that input, and the component's `checked` state is updated from the input's resulting `checked` property. Checked state may be owned by the consumer (`checked`) or by the component (`defaultChecked`). Within `Field.Root` the switch registers as the field's control and reports touched/dirty/filled/focused/validity through the field's state attributes.

No component in the library builds on Switch, and Switch builds on no other component.

## 2. Anatomy

```
<Switch.Root>                 required, 1          default element: span  (+ hidden <input type="checkbox"> sibling,
                                                                            + optional hidden <input type="hidden"> sibling)
  <Switch.Thumb>              optional, 0..n       default element: span
```

- **SWT-STR-01** — MUST. `Switch.Root` MUST render, in document order: the root element (default `<span>`), then, only when the switch is unchecked and both an effective `name` and `uncheckedValue` are set, an `<input type="hidden">`, then always an `<input type="checkbox">`. All three are siblings; the inputs are never descendants of the root element.
  Source: `switch/root/SwitchRoot.tsx:230-238`. Test: [L2] `SwitchRoot.test.tsx` "should update its state if the underlying input is toggled" (hidden checkbox exists), "submits uncheckedValue to an external form when off" (L4 for the `FormData` citation).
  Port note 2026-10-22 (ADR 02 Amendments 2026-10-22): the two inputs are rendered after the root element by `PartBase.BuildAfterElement`, so they are its siblings on every render path; the `FormData` citation runs in a browser (ADR 06 §1, ADR 13 §3).
- **SWT-STR-02** — MUST. `Switch.Thumb` MUST be rendered somewhere inside the subtree of a `Switch.Root`; rendering it elsewhere MUST throw the error in SWT-EDGE-01. Nothing limits the number of Thumbs; each one mirrors the root's state.
  Source: `switch/root/SwitchRootContext.ts:9-17`, `switch/thumb/SwitchThumb.tsx:21`. Test: [L2] `SwitchThumb.test.tsx` "throws a descriptive error when rendered outside <Switch.Root>".
- **SWT-STR-03** — MUST. `Switch.Thumb` is structural only: it MUST render an element carrying the root's state attributes and any forwarded attributes, and MUST NOT attach handlers, ARIA attributes or an `id` of its own.
  Source: `switch/thumb/SwitchThumb.tsx:23-28`. Test: [L2] `SwitchRoot.test.tsx` "should place the style hooks on the root and the thumb".

If Thumb is omitted the root still functions fully; Thumb exists only as a styling hook.

## 3. Public API

### 3.1 `Switch.Root`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `checked` | `boolean` | — (uncontrolled) | Whether the switch is on. Supplying it (non-`undefined`) on first render makes the value consumer-owned. | SWT-API-01 |
| `defaultChecked` | `boolean` | `false` | Initial state when the value is component-owned. | SWT-API-02 |
| `onCheckedChange` | `(checked: boolean, eventDetails: Switch.Root.ChangeEventDetails) => void` | — | Called when the user toggles the switch, before the state changes. | SWT-API-03 |
| `disabled` | `boolean` | `false` | Ignore user interaction. Combined (OR) with the enclosing `Field.Root`'s `disabled`. | SWT-API-04 |
| `readOnly` | `boolean` | `false` | User cannot toggle; still focusable, still submits. | SWT-API-05 |
| `required` | `boolean` | `false` | Must be on to pass native constraint validation. | SWT-API-06 |
| `name` | `string` | — | Form field name. Overridden by the enclosing `Field.Root`'s `name` when that is set. | SWT-API-07 |
| `value` | `string` | — (browser submits `"on"`) | Value submitted when on. | SWT-API-08 |
| `uncheckedValue` | `string` | — (nothing submitted) | Value submitted when off. | SWT-API-09 |
| `form` | `string` | — | `id` of the owning `<form>` when rendered outside it. | SWT-API-10 |
| `id` | `string` | generated | `id` of the hidden input; when `nativeButton` is `true`, applied to the root element instead. | SWT-API-11 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the hidden `<input type="checkbox">`. | SWT-API-12 |
| `nativeButton` | `boolean` | `false` | Declare that the substituted root element is a native `<button>`. | SWT-API-13 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object in §3.1 State. | SWT-API-14 |
| `aria-labelledby`, `aria-label` | `string` | — | Consumed to compute the root's accessible name (see §5). `aria-label` is also forwarded to the root. | SWT-ARIA-06 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the root element (not to the hidden input). | SWT-API-15 |

- **SWT-API-01** — MUST. When `checked` is not `undefined` on the first render, the switch's checked state MUST equal `checked` on every render and MUST NOT be changed internally by user interaction.
  Source: `switch/root/SwitchRoot.tsx:87-92`, `packages/utils/src/useControlled.ts:41-46,83-90`. Test: [L2] `SwitchRoot.test.tsx` "should update its state when changed from outside".
- **SWT-API-02** — MUST. When `checked` is `undefined` on the first render, the initial checked state MUST be `Boolean(defaultChecked)` (so an omitted or `undefined` `defaultChecked` yields `false`).
  Source: `switch/root/SwitchRoot.tsx:89`. Test: [L2] `SwitchRoot.test.tsx` "should change its state when clicked" (starts `false`), "should place the style hooks on the root and the thumb" (`defaultChecked` → `data-checked`).
- **SWT-API-03** — MUST. `onCheckedChange` MUST receive the next checked value as its first argument and a change-event-details object (§3.3) as its second. Full firing rules are in §7.
  Source: `switch/root/SwitchRoot.tsx:183-186,302-303`. Test: [L2] `SwitchRoot.test.tsx` "should call onCheckedChange when clicked".
- **SWT-API-04** — MUST. The effective disabled state MUST be `fieldDisabled || disabled`, where `fieldDisabled` is the enclosing `Field.Root`'s disabled state (`false` outside a Field). The default of the prop is `false`.
  Source: `switch/root/SwitchRoot.tsx:51,73`. Test: [L2] `SwitchRoot.test.tsx` "should receive disabled prop from Field.Root".
  Port note 2026-10-23 (M2 track 4 follow-up): the switch shares Checkbox's field behavior (`Field/CheckableFieldBehavior`), which for a checkbox also ORs a `Field.Item`'s disabled state (CHK-API-11); a switch reads the field root's only, as Base UI's `Switch.Root` reads no item context, and still takes the item's labelable scope. Port title "a switch inside a disabled Field.Item is not disabled by it, and claims the item's label" [L2].
- **SWT-API-05** — MUST. `readOnly` defaults to `false`. When `true`, user interaction MUST NOT change the checked state (details in §6), and `aria-readonly="true"` MUST be set (§5).
  Source: `switch/root/SwitchRoot.tsx:49,124,144-146,178-181`. Test: [L2] `SwitchRoot.test.tsx` "prop: readOnly" (all four tests) (L4 for the root-click and label-click citations).
- **SWT-API-06** — MUST. `required` defaults to `false`. When `true`, the hidden input MUST carry the `required` attribute and the root MUST carry `aria-required="true"`.
  Source: `switch/root/SwitchRoot.tsx:50,125,166`. Test: [L2] `SwitchRoot.test.tsx` "should have the `aria-required` attribute", "triggers native HTML validation on submit" (L4 for the native-validation citation).
- **SWT-API-07** — MUST. The effective form name MUST be the enclosing `Field.Root`'s `name` when that is defined, otherwise the `name` prop. The effective name is placed on the hidden input(s) only, never on the root element.
  Source: `switch/root/SwitchRoot.tsx:74,165,234`. Test: [L2] `SwitchRoot.test.tsx` "should receive name prop from Field.Root", "should set the name attribute only on the input".
- **SWT-API-08** — MUST. The `value` prop MUST be placed on the hidden checkbox input only; when the prop is `undefined`, the input MUST have no `value` attribute at all (so the browser submits its default `"on"`).
  Source: `switch/root/SwitchRoot.tsx:202-204`. Test: [L2] `SwitchRoot.test.tsx` "should not set the value attribute by default", "should set the value attribute only on the input".
- **SWT-API-09** — MUST. `uncheckedValue` MUST be submitted (via the hidden `<input type="hidden">`) only while the switch is unchecked and an effective name exists; when `undefined`, an unchecked switch MUST submit nothing.
  Source: `switch/root/SwitchRoot.tsx:233-235,319-323`. Test: [L2] `SwitchRoot.test.tsx` "should submit uncheckedValue when switch is off and uncheckedValue is specified" (L4 for the `FormData` citation). Docs: CHANGELOG v1.0.0-rc.0 "Match native off state in form submission".
- **SWT-API-10** — MUST. `form` MUST be forwarded as the `form` attribute of both hidden inputs and MUST NOT appear on the root element.
  Source: `switch/root/SwitchRoot.tsx:163,234`. Test: [L2] `SwitchRoot.test.tsx` "submits to an external form when `form` is provided" (L4 for the `FormData` citation).
  Port note 2026-10-22: the parameter is `Form : string?` (ADR 08 Amendments 2026-10-22); at L2 the port asserts the attribute on both inputs and not on the root.
- **SWT-API-11** — MUST. When `nativeButton` is `false`, the `id` prop MUST be applied to the hidden checkbox input and the root element MUST receive an auto-generated id. When `nativeButton` is `true`, the `id` prop MUST be applied to the root element and the hidden input MUST have no `id`.
  Source: `switch/root/SwitchRoot.tsx:82-85,121,164`. Test: [L2] `SwitchRoot.test.tsx` "should toggle the switch when a explicitly linked <label> is clicked", "should associate `id` with the native button when `nativeButton=true`" (L4 for the label-click citations).
- **SWT-API-12** — MUST. `inputRef` MUST be attached to the hidden `<input type="checkbox">` element. Port: withdrawn (ADR 02 Amendments 2026-10-22 item 1, 2026-10-22): the hidden input stays internal and no public handle to it exists.
  Source: `switch/root/SwitchRoot.tsx:77,171`. Test: [L2] `SwitchRoot.test.tsx` "should be able to access the native input".
- **SWT-API-13** — MUST. `nativeButton` defaults to `false`. It selects native-button vs. non-native-button behavior from [UBT](../architecture/button-behavior.md) (keyboard activation, `tabindex`, disabled attribute vs `aria-disabled`, `type="button"`) and the `id`/label wiring in SWT-API-11 and §5.
  Source: `switch/root/SwitchRoot.tsx:47,107-110`. Test: [L2] `SwitchRoot.test.tsx` "can render a native button" (L4 for the keyboard-activation clause).
- **SWT-API-14** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md); the state object they receive is the one in "State object" below. None of these three is forwarded as a plain attribute.
  Source: `switch/root/SwitchRoot.tsx:38-57,218-228`. Test: [L2] `SwitchRoot.test.tsx` conformance suite "Base UI component API" (see §13).
- **SWT-API-15** — MUST. Every other prop MUST be forwarded to the root element (merged after the built-in attributes, so a consumer value for the same attribute wins — see SWT-DOM-14). The following are consumed and MUST NOT be forwarded: `checked`, `defaultChecked`, `onCheckedChange`, `disabled`, `readOnly`, `required`, `name`, `value`, `uncheckedValue`, `form`, `id`, `inputRef`, `nativeButton`, `aria-labelledby`, `className`, `style`, `render`. `onChange` is excluded from the accepted parameter type.
  Source: `switch/root/SwitchRoot.tsx:38-57,221-226,260-261`. Test: [L2] `SwitchRoot.test.tsx` "should override the built-in attributes", "should call onClick when clicked", "should set the name attribute only on the input".

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes. It is the enclosing field's state (all `false`/`null` outside a Field) with four switch fields layered on top.

| Field | Type | Meaning |
|---|---|---|
| `checked` | `boolean` | Current checked state. |
| `disabled` | `boolean` | Effective disabled (`Field.Root` disabled OR prop). |
| `readOnly` | `boolean` | The `readOnly` prop. |
| `required` | `boolean` | The `required` prop. |
| `touched` | `boolean` | Field: the control has been blurred at least once. |
| `dirty` | `boolean` | Field: `checked` differs from the value captured when the control first registered. |
| `valid` | `boolean \| null` | Field: validity verdict; `null` until validated or while the field is disabled. |
| `filled` | `boolean` | Field: equals `checked`. |
| `focused` | `boolean` | Field: the root currently owns focus (never `true` while disabled). |

- **SWT-STA-04** — MUST. The state object MUST contain exactly the nine fields above, with `disabled` taken from the effective disabled value (SWT-API-04), not the field's own `disabled`.
  Source: `switch/root/SwitchRoot.tsx:207-216,241-258`. Test: [L2] `SwitchThumb.test.tsx` (test context shape, lines 6-16).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-checked` | `""` while `checked` is `true` | SWT-DOM-02 |
| `data-unchecked` | `""` while `checked` is `false` | SWT-DOM-02 |
| `data-disabled` | `""` while effective disabled | SWT-DOM-03 |
| `data-readonly` | `""` while `readOnly` | SWT-DOM-03 |
| `data-required` | `""` while `required` | SWT-DOM-03 |
| `data-touched` | `""` while field `touched` (Field only) | SWT-DOM-04 |
| `data-dirty` | `""` while field `dirty` (Field only) | SWT-DOM-04 |
| `data-filled` | `""` while `checked` (Field only) | SWT-DOM-04 |
| `data-focused` | `""` while field `focused` (Field only) | SWT-DOM-04 |
| `data-valid` | `""` while field `valid === true` (Field only) | SWT-DOM-05 |
| `data-invalid` | `""` while field `valid === false` (Field only) | SWT-DOM-05 |

#### CSS variables

None.

### 3.2 `Switch.Thumb`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Functions receive the root's state object. | SWT-API-16 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the thumb element. | SWT-API-16 |

- **SWT-API-16** — MUST. All Thumb props other than `className`, `style`, `render` MUST be forwarded to the thumb element; the three customization props MUST behave per RND using the root's state object.
  Source: `switch/thumb/SwitchThumb.tsx:19-28`. Test: [L2] `SwitchThumb.test.tsx` conformance suite "Base UI component API"; `SwitchRoot.test.tsx` "should place the style hooks on the root and the thumb" (`data-testid` forwarded).

#### State object

Identical to the root's state object (same fields, same values; see SWT-STA-08).

#### Data attributes

Identical to the root's (same mapping): `data-checked`, `data-unchecked`, `data-disabled`, `data-readonly`, `data-required`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid`. Req: SWT-DOM-06.

#### CSS variables

None.

### 3.3 Shared types

- `Switch.Root.ChangeEventReason` = `'none'` — the only literal. Source: `switch/root/SwitchRoot.tsx:326`, `internals/reason-parts.ts:1`.
- `Switch.Root.ChangeEventDetails` = the common change-event-details shape from [EVD](../architecture/event-details-and-reasons.md) specialised to reason `'none'`: `{ reason: 'none'; event: Event; cancel(); allowPropagation(); isCanceled; isPropagationAllowed; trigger: Element | undefined }`. Source: `switch/root/SwitchRoot.tsx:327`, `internals/createBaseUIEventDetails.ts:56-85,126-157`.
- `Switch.Root.State`, `Switch.Thumb.State` — the state object in §3.1. `Switch.Thumb.State` extends `Switch.Root.State` with no additional fields. Source: `switch/thumb/SwitchThumb.tsx:33`.
- `Switch.Root.Props`, `Switch.Thumb.Props` — the prop tables above.
- Exported constant namespaces `SwitchRootDataAttributes` and `SwitchThumbDataAttributes` list the attribute names in the tables above (both namespaces are identical). Source: `switch/root/SwitchRootDataAttributes.ts`, `switch/thumb/SwitchThumbDataAttributes.ts`, `switch/index.ts:6-7`.

## 4. State model

The only state owned by the component is `checked`. Everything else in the state object is either a prop echo (`disabled`, `readOnly`, `required`) or field state owned by the enclosing `Field.Root` and written to by the switch at defined moments.

**Ownership.** `checked`/`defaultChecked` is the controlled/uncontrolled pair. Ownership is decided once, on the first render, by whether `checked` is `undefined`. Toggling in the consumer-owned mode only fires `onCheckedChange`; the DOM reflects the new value only when the consumer passes it back.

**Derived values.**

- effective disabled = `fieldDisabled || disabled`
- effective name = `fieldName ?? name`
- `filled` (field) = `checked`, re-published after every change of `checked`
- `dirty` (field) = `checked !== initialValue`, where `initialValue` is the `checked` value captured the first time this field registered a control
- `aria-checked` = `checked`; `data-checked`/`data-unchecked` = `checked`

No value normalization applies (the value is a boolean). There is no mount/unmount lifecycle: all parts are always rendered.

- **SWT-STA-01** — MUST. Ownership of `checked` MUST be fixed on the first render: consumer-owned if `checked !== undefined`, otherwise component-owned. Later changes of `checked` between `undefined` and a boolean MUST NOT change the mode (a dev warning is logged instead, SWT-EDGE-02).
  Source: `packages/utils/src/useControlled.ts:41,46`. Test: none.
- **SWT-STA-02** — MUST. In component-owned mode, a non-canceled user toggle MUST set `checked` to the hidden input's post-click `checked` property (that is, the inverse of the previous state).
  Source: `switch/root/SwitchRoot.tsx:183,192`. Test: [L2] `SwitchRoot.test.tsx` "should change its state when clicked".
- **SWT-STA-03** — MUST. In consumer-owned mode, a user toggle MUST NOT change the rendered state unless the consumer updates `checked`; when the consumer updates `checked` (from any cause, including code unrelated to user interaction) the root's `aria-checked`, data attributes, Thumb attributes and hidden input `checked` property MUST follow. Server: relaxed, see §9.
  Source: `switch/root/SwitchRoot.tsx:87-92,123,161`, `packages/utils/src/useControlled.ts:83-90`. Test: [L2] `SwitchRoot.test.tsx` "should update its state when changed from outside", "does not change state when canceled via a root click" (`input.checked` restored) (L4 for the `input.checked` restore clause).
- **SWT-STA-05** — MUST. When `checked` was consumer-owned at first render and later becomes `undefined`, the rendered state MUST fall back to the initial default (`Boolean(defaultChecked)` as of the first render).
  Source: `packages/utils/src/useControlled.ts:43-46`. Test: none.
- **SWT-STA-06** — MUST. Within a `Field.Root`, the field's `filled` state MUST be set to `checked` immediately after the switch mounts and again immediately after every change of `checked`, synchronously after the DOM update (before paint). Server: relaxed, see §9.
  Source: `switch/root/SwitchRoot.tsx:96-98`. Test: [L2] `SwitchRoot.test.tsx` "adds [data-filled] attribute when checked after being initially unchecked", "removes [data-filled] attribute when unchecked after being initially checked", "clears [data-filled] when a controlled switch remounts unchecked".
- **SWT-STA-07** — MUST. Within a `Field.Root`, after every change of `checked` (but not on mount) the field's `dirty` state MUST be set to `checked !== initialValue`, so that toggling back to the initial value clears `dirty`.
  Source: `switch/root/SwitchRoot.tsx:100-102`, `internals/field-register-control/useFieldControlRegistration.ts:92-103` (initial value capture). Test: [L2] `SwitchRoot.test.tsx` "[data-dirty]".
- **SWT-STA-08** — MUST. `Switch.Thumb` MUST expose exactly the root's state object (same values at the same time); it has no state of its own.
  Source: `switch/thumb/SwitchThumb.tsx:21-24`, `switch/root/SwitchRoot.tsx:231`. Test: [L2] `SwitchRoot.test.tsx` "should place the style hooks on the root and the thumb".

## 5. Rendered DOM and ARIA

### Root element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `id` | generated `base-ui-…` id when `nativeButton=false`; the labelable control id (`id` prop, else Field-provided id, else generated) when `nativeButton=true` | SWT-DOM-01 | C# |
| `role` | `"switch"` (consumer may override) | SWT-ARIA-01 | C# |
| `aria-checked` | `"true"` / `"false"` | SWT-ARIA-02 | C# |
| `aria-readonly` | `"true"` only when `readOnly` | SWT-ARIA-03 | C# |
| `aria-required` | `"true"` only when `required` | SWT-ARIA-04 | C# |
| `aria-disabled` | `"true"` only when disabled and `nativeButton=false` | SWT-ARIA-05 | C# |
| `disabled` | present only when disabled and `nativeButton=true` | SWT-ARIA-05 | C# |
| `tabindex` | `0` in both modes; `-1` when disabled and `nativeButton=false` | SWT-FOC-01, SWT-FOC-02 | C# |
| `type` | `"button"` when `nativeButton=true` | SWT-DOM-16 | C# |
| `aria-labelledby` | see SWT-ARIA-06..09 | | C# |
| `aria-describedby` | consumer value + Field description/error ids | SWT-ARIA-10 | C# |
| `aria-invalid` | `"true"` when the field is invalid and the switch is not disabled | SWT-ARIA-11 | C# |
| `data-*` | §3.1 table | SWT-DOM-02..05 | C# |

- **SWT-DOM-01** — MUST. When `nativeButton` is `false`, the root element MUST always carry an auto-generated `id` (never the `id` prop). When `nativeButton` is `true`, the root MUST carry the labelable control id: the `id` prop if given, else the id supplied by an enclosing `Field.Root`, else a generated id.
  Source: `switch/root/SwitchRoot.tsx:82-85,121`, `internals/labelable-provider/useLabelableId.ts:82`. Test: [L2] `SwitchRoot.test.tsx` "should associate `id` with the native button when `nativeButton=true`".
- **SWT-DOM-02** — MUST. Exactly one of `data-checked` / `data-unchecked` MUST be present on the root at all times, with empty-string value, matching `checked`.
  Source: `switch/stateAttributesMapping.ts:8-14`. Test: [L2] `SwitchRoot.test.tsx` "should place the style hooks on the root and the thumb".
- **SWT-DOM-03** — MUST. `data-disabled`, `data-readonly`, `data-required` MUST be present (value `""`) exactly while the corresponding state field is `true`.
  Source: `internals/getStateAttributesProps.ts:24-28`, `switch/root/SwitchRoot.tsx:207-216`. Test: [L2] `SwitchRoot.test.tsx` "should place the style hooks on the root and the thumb", "should receive disabled prop from Field.Root".
- **SWT-DOM-04** — MUST. `data-touched`, `data-dirty`, `data-filled`, `data-focused` MUST be present (value `""`) exactly while the corresponding field state is `true`; outside a `Field.Root` they MUST never appear.
  Source: `switch/root/SwitchRoot.tsx:209`, `internals/field-constants/constants.ts:18-32`, `internals/field-root-context/FieldRootContext.ts:37-67` (no-op setters outside a Field). Test: [L2] `SwitchRoot.test.tsx` "[data-touched]", "[data-dirty]", "[data-filled]" group, "[data-focused]".
- **SWT-DOM-05** — MUST. `data-valid` MUST be present when field `valid === true`, `data-invalid` when `valid === false`, and neither when `valid === null`.
  Source: `internals/field-constants/constants.ts:34-44`, `switch/stateAttributesMapping.ts:7`. Test: none (asserted indirectly via `aria-invalid` in "prop: validationMode=onSubmit").
- **SWT-DOM-06** — MUST. The Thumb element MUST carry the same data attributes as the root, derived from the same state with the same mapping, and they MUST be committed in the same render batch as the root's.
  Source: `switch/thumb/SwitchThumb.tsx:23-28`, `switch/stateAttributesMapping.ts:6-15`. Test: [L2] `SwitchRoot.test.tsx` "should place the style hooks on the root and the thumb".
- **SWT-DOM-14** — MUST. Attributes supplied by the consumer on `Switch.Root` MUST take precedence over the built-in root attributes with the same name (e.g. `role="checkbox"` replaces `role="switch"`), except `className`/`style` which merge per RND and event handlers which chain per RND (consumer handler first).
  Source: `switch/root/SwitchRoot.tsx:221-226` (merge order), `merge-props/mergeProps.ts:14-31,150-188`. Test: [L2] `SwitchRoot.test.tsx` "should override the built-in attributes".
- **SWT-DOM-16** — MUST. When `nativeButton` is `true`, the root MUST receive `type="button"` (so it never submits a form); when `false`, no `type` attribute is added.
  Source: `internals/use-button/useButton.ts:226`. Test: none.

- **SWT-ARIA-01** — MUST. The root MUST have `role="switch"` unless the consumer supplies a `role`.
  Source: `switch/root/SwitchRoot.tsx:122`. Test: [L2] `SwitchRoot.test.tsx` every `getByRole('switch')` query; "should override the built-in attributes".
- **SWT-ARIA-02** — MUST. The root MUST have `aria-checked` equal to the string form of `checked` (`"true"`/`"false"`), committed in the same render batch as the state change.
  Source: `switch/root/SwitchRoot.tsx:123`. Test: [L2] `SwitchRoot.test.tsx` "should change its state when clicked".
- **SWT-ARIA-03** — MUST. `aria-readonly="true"` MUST be present when `readOnly` is `true` and absent otherwise (never `"false"`).
  Source: `switch/root/SwitchRoot.tsx:124`. Test: [L2] `SwitchRoot.test.tsx` "should have the `aria-readonly` attribute", "should not have the aria attribute when `readOnly` is not set".
- **SWT-ARIA-04** — MUST. `aria-required="true"` MUST be present when `required` is `true` and absent otherwise.
  Source: `switch/root/SwitchRoot.tsx:125`. Test: [L2] `SwitchRoot.test.tsx` "should have the `aria-required` attribute", "should not have the aria attribute when `required` is not set".
- **SWT-ARIA-05** — MUST. When disabled and `nativeButton` is `false`, the root MUST carry `aria-disabled="true"` and MUST NOT carry a `disabled` attribute. When disabled and `nativeButton` is `true`, the root MUST carry the `disabled` attribute and no `aria-disabled`. When not disabled, neither is present.
  Source: `utils/useFocusableWhenDisabled.ts:38-47`, `switch/root/SwitchRoot.tsx:107-110`. Test: [L2] `SwitchRoot.test.tsx` "uses aria-disabled instead of HTML disabled", "should not have the `disabled` attribute when `disabled` is not set".
- **SWT-ARIA-06** — MUST. The root's `aria-labelledby` MUST be resolved in this precedence: (1) the consumer's `aria-labelledby` prop; else (2) if the consumer's `aria-label` is a non-blank string (after trimming), no `aria-labelledby` at all — even when a `Field.Label` is present; else (3) the id of the enclosing field's `Field.Label` (when one is mounted); else (4) the fallback in SWT-ARIA-07; else absent.
  Source: `switch/root/SwitchRoot.tsx:111-118,126`, `internals/labelable-provider/useAriaLabelledBy.ts:16-20`. Test: [L2] `SwitchRoot.test.tsx` "prefers `aria-label` over an associated label", "sets `for` on the label" (Field.Label id), "sets `aria-labelledby` from a sibling label associated with the hidden input" (L4 for the native-label fallback clause). Docs: CHANGELOG v1.9.0 "Prefer `aria-label` over associated labels".
- **SWT-ARIA-07** — MUST. Fallback (`nativeButton=false` only): the component MUST look for a native `<label>` associated with the hidden checkbox input, in this order: the input's parent element if it is a `<label>`; else, if the input has an `id`, its next element sibling if that is a `<label>` whose `htmlFor` equals the input's id; else the first entry of the input's `labels` list. If a label is found and has no `id`, the component MUST assign it the id `"<hidden input id>-label"`; the label's id (existing or assigned) becomes the root's `aria-labelledby`.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:16,40-74`. Test: [L4] `SwitchRoot.test.tsx` "sets `aria-labelledby` from a sibling label associated with the hidden input", "should toggle the switch when a wrapping <label> is clicked" (name derived; not asserted). Docs: CHANGELOG v1.2.0 "Add automatic `aria-labelledby` support".
  Port note 2026-10-23: the lookup is the `form` module's on the hidden input (ADR 04 §1 `form` row, "native label lookup"; the Checkbox track's design point C4): it reports the label's id, assigning `"<hidden input id>-label"` when the label has none (FFI-ARIA-06), and C# renders it as the root's `aria-labelledby` while nothing earlier in SWT-ARIA-06's precedence names the switch. The wrapping-label citation is asserted at L4 as the port title "a wrapping native label names the switch".
- **SWT-ARIA-08** — MUST. The fallback in SWT-ARIA-07 MUST be re-evaluated on every render of the part (the root), not only when props change, so that a change of the hidden input's `id`, or a label mounting/unmounting, updates `aria-labelledby` without any prop change. Server: relaxed, see §9.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:22-35`. Test: [L4] `SwitchRoot.test.tsx` "updates fallback `aria-labelledby` when the hidden input id changes".
- **SWT-ARIA-09** — MUST. When `nativeButton` is `true`, the fallback in SWT-ARIA-07 MUST be disabled (steps 1-3 of SWT-ARIA-06 still apply); a native `<label htmlFor>` then names the `<button>` through native label semantics.
  Source: `switch/root/SwitchRoot.tsx:115`, `internals/labelable-provider/useAriaLabelledBy.ts:28-30`. Test: [L2] `SwitchRoot.test.tsx` "should associate `id` with the native button when `nativeButton=true`" (no `aria-labelledby` asserted; implied).
- **SWT-ARIA-10** — MUST. Within a `Field.Root`, the root's `aria-describedby` MUST be the consumer's `aria-describedby` tokens followed by the ids of the field's mounted `Field.Description`/`Field.Error` elements (deduplicated, space-separated); absent when the result is empty.
  Source: `switch/root/SwitchRoot.tsx:225`, `field/root/useFieldValidation.ts:367-376`, `internals/labelable-provider/LabelableProvider.tsx:68-81`. Test: [L2] `SwitchRoot.test.tsx` "Field.Description", "preserves Field validation props through canceled changes, submit, and reset".
- **SWT-ARIA-11** — MUST. Within a `Field.Root`, the root MUST carry `aria-invalid="true"` exactly while the field's `valid` is `false` and neither the field nor the switch is disabled; otherwise `aria-invalid` is absent.
  Source: `field/root/useFieldValidation.ts:370-373`, `switch/root/SwitchRoot.tsx:225`. Test: [L2] `SwitchRoot.test.tsx` "prop: validationMode=onSubmit", "clears external errors on change".

### Hidden checkbox input

Rendered always, as the root's next sibling (after the optional `<input type="hidden">`).

| Attribute / property | Value | Req | Writer |
|---|---|---|---|
| `type` | `"checkbox"` | SWT-DOM-07 | C# |
| `aria-hidden` | `"true"` | SWT-DOM-07 | C# |
| `tabindex` | `-1` | SWT-DOM-07 | C# |
| `checked` (property) | `checked` state | SWT-DOM-08 | C# → JS |
| `disabled` | effective disabled | SWT-DOM-08 | C# |
| `required` | `required` | SWT-DOM-08 | C# |
| `name` | effective name (absent when none) | SWT-DOM-08 | C# |
| `form` | `form` prop | SWT-DOM-08 | C# |
| `id` | labelable control id when `nativeButton=false`; absent when `true` | SWT-DOM-10 | C# |
| `value` | `value` prop; attribute absent when `undefined` | SWT-DOM-11 | C# |
| `style` | visually-hidden declarations (two variants) | SWT-DOM-09 | C# |
| `aria-describedby` | Field description/error ids | SWT-DOM-15 | C# |
| `aria-invalid` | `"true"` when field invalid and not disabled | SWT-DOM-15 | C# |

- **SWT-DOM-07** — MUST. The hidden input MUST be `<input type="checkbox">` with `aria-hidden="true"` and `tabindex="-1"`.
  Source: `switch/root/SwitchRoot.tsx:168-170`. Test: [L2] `SwitchRoot.test.tsx` `getByRole('checkbox', { hidden: true })` queries.
- **SWT-DOM-08** — MUST. The hidden input's `checked` property MUST always equal the switch's `checked` state; its `disabled`, `required`, `name` and `form` attributes MUST mirror the effective disabled, `required`, effective name and `form` values.
  Source: `switch/root/SwitchRoot.tsx:161-166`. Test: [L2] `SwitchRoot.test.tsx` "should receive name prop from Field.Root", "does not submit uncheckedValue when disabled", "does not change state when canceled via a hidden input click" (`input.checked`) (L4 for the `checked`-property restore and `FormData` citations).
  Port note 2026-10-22 (ADR 04 Amendments 2026-10-22, the ownership row of the hidden input's `checked`): the `Writer` cell is `C# → JS`. C# renders the initial `checked` once, in prerender and the first interactive render; from attach the `dom` primitive owns the property, sets it on every `update` and is asked to restore it (`DomOptions.CheckedRestores`) when a request leaves the switch's value different from the box. It is not re-asserted inside a native click, which would undo an accepted toggle on Server before C# commits it (ADR 04 Amendments 2026-10-22 item 2 as refined by M2 track 4); it is re-asserted after the owning form's `reset` (§16 Q1).
- **SWT-DOM-09** — MUST. The hidden input MUST be visually hidden by inline style. With no effective name the declarations are: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0`. With an effective name they are: `clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute` (kept in flow near the root so native validation UI appears beside the switch).
  Source: `switch/root/SwitchRoot.tsx:167`, `packages/utils/src/visuallyHidden.ts:3-25`. Test: none.
- **SWT-DOM-10** — MUST. When `nativeButton` is `false`, the hidden input MUST carry the labelable control id (the `id` prop; else the enclosing `Field.Root`'s control id; else a generated id) — it is never absent. When `nativeButton` is `true`, the hidden input MUST have no `id`.
  Source: `switch/root/SwitchRoot.tsx:84-85,164`, `internals/labelable-provider/useLabelableId.ts:82`. Test: [L2] `SwitchRoot.test.tsx` "should toggle the switch when a explicitly linked <label> is clicked", "should associate `id` with the native button when `nativeButton=true`", "sets `for` on the label".
- **SWT-DOM-11** — MUST. The hidden input MUST have a `value` attribute only when the `value` prop is defined.
  Source: `switch/root/SwitchRoot.tsx:202-204`. Test: [L2] `SwitchRoot.test.tsx` "should not set the value attribute by default", "should set the value attribute only on the input".
- **SWT-DOM-12** — MUST. `name` and `value` MUST NOT appear on the root element.
  Source: `switch/root/SwitchRoot.tsx:38-57` (consumed before forwarding). Test: [L2] `SwitchRoot.test.tsx` "should set the name attribute only on the input", "should set the value attribute only on the input".
- **SWT-DOM-15** — MUST. Within a `Field.Root`, the hidden input MUST also receive `aria-describedby` (the field's description/error ids only, without the consumer's tokens) and `aria-invalid="true"` under the same condition as SWT-ARIA-11.
  Source: `switch/root/SwitchRoot.tsx:160`, `field/root/useFieldValidation.ts:367-376`. Test: [L2] `SwitchRoot.test.tsx` "Field.Description" (`internalInput` `aria-describedby`).

### Hidden `type="hidden"` input (unchecked value)

- **SWT-DOM-13** — MUST. An `<input type="hidden">` with `name` = effective name, `value` = `uncheckedValue`, `form` = `form` prop and `disabled` = effective disabled MUST be rendered between the root and the checkbox input exactly while `checked` is `false` AND an effective name is non-empty AND `uncheckedValue !== undefined`; otherwise it MUST NOT exist.
  Source: `switch/root/SwitchRoot.tsx:233-235`. Test: [L2] `SwitchRoot.test.tsx` "submits uncheckedValue to an external form when off", "submits custom value and uncheckedValue across an off/on/off cycle", "does not submit uncheckedValue when disabled" (L4 for the `FormData` citations).

## 6. Interactions

All toggles funnel through one path: a `click` is delivered to the hidden checkbox input, the browser flips the input's `checked`, and the component reads the input's new `checked` to produce the next state (see §7 for the callback and cancel step). The root's own click handler is the bridge from the visible element to the input.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | root focused, `nativeButton=false`, not disabled | On keydown: default prevented, a click carrying the key event's modifier state is dispatched on the root → toggle. | SWT-KEY-01 |
| <kbd>Space</kbd> | root focused, `nativeButton=false`, not disabled | On keydown: default prevented (no page scroll), no toggle. On keyup (if its default was not prevented): a click with modifier state is dispatched on the root → toggle. | SWT-KEY-02 |
| <kbd>Enter</kbd>, <kbd>Space</kbd> | root focused, `nativeButton=true` | Not intercepted; the browser's native button activation produces the click → toggle. | SWT-KEY-03 |
| any key | event target is a descendant of the root, not the root itself | Not handled. | SWT-KEY-04 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Not intercepted; native focus movement. | SWT-KEY-05 |
| any key | disabled | Not handled (no preventDefault, no toggle). | SWT-KEY-06 |
| <kbd>Enter</kbd>, <kbd>Space</kbd> | `readOnly` | Click is still dispatched on the root; the root click handler ignores it, so no toggle. | SWT-KEY-09 |
| Modifier combinations | any of the above | Modifiers never change behavior; their state is carried into the resulting click/change event. | SWT-KEY-07 |

There are no orientation or RTL variants.

- **SWT-KEY-01** — MUST. With `nativeButton=false`, a <kbd>Enter</kbd> keydown whose target is the root MUST have its default prevented and MUST toggle the switch (by dispatching a click on the root).
  Source: `internals/use-button/useButton.ts:127-131,155-175`, `switch/root/SwitchRoot.tsx:107-110,143-156`. Test: [L4] `SwitchRoot.test.tsx` "can be activated with Enter key".
- **SWT-KEY-02** — MUST. With `nativeButton=false`, a <kbd>Space</kbd> keydown whose target is the root MUST have its default prevented without toggling; the subsequent <kbd>Space</kbd> keyup on the root MUST toggle the switch unless the keyup's default was prevented.
  Source: `internals/use-button/useButton.ts:170,207-216`. Test: [L4] `SwitchRoot.test.tsx` "can be activated with Space key".
- **SWT-KEY-03** — MUST. With `nativeButton=true`, <kbd>Enter</kbd> and <kbd>Space</kbd> MUST NOT be intercepted; the browser's native click MUST toggle the switch.
  Source: `internals/use-button/useButton.ts:131,155-163`. Test: [L4] `SwitchRoot.test.tsx` "can render a native button".
- **SWT-KEY-04** — MUST. Key events whose target is not the root element itself MUST NOT toggle the switch.
  Source: `internals/use-button/useButton.ts:127,131,208`. Test: none.
- **SWT-KEY-05** — MUST. <kbd>Tab</kbd> MUST NOT be intercepted.
  Source: `internals/use-button/useButton.ts:116-176` (no Tab branch). Test: [L4] `SwitchRoot.test.tsx` "can be activated with Enter key" (`[Tab]` focuses root).
- **SWT-KEY-06** — MUST. While disabled, keydown/keyup on the root MUST have no effect (no toggle, no `preventDefault`).
  Source: `internals/use-button/useButton.ts:117-119,178-180`. Test: none.
- **SWT-KEY-07** — MUST. When a toggle originates from the keyboard, the `event` in the change details (§7) MUST report the keyboard event's `shiftKey`, `ctrlKey`, `altKey`, `metaKey`.
  Source: `utils/dispatchClickWithModifiers.ts:20-41`, `internals/use-button/useButton.ts:174,215`, `switch/root/SwitchRoot.tsx:155`. Test: none for keyboard (`SwitchRoot.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange" covers a pointer click with <kbd>Shift</kbd> held). Docs: CHANGELOG v1.1.0 "Preserve modifier key properties in the change event".
- **SWT-KEY-08** — MUST. With `nativeButton=false`, a consumer `onKeyDown` handler that calls `preventDefault()` MUST cancel <kbd>Enter</kbd> activation; a consumer `onKeyUp` handler that calls `preventDefault()` MUST cancel <kbd>Space</kbd> activation. (A prevented <kbd>Space</kbd> *keydown* does not cancel the keyup activation; see [UBT](../architecture/button-behavior.md).) Server: relaxed, see §9.
  Source: `internals/use-button/useButton.ts:166-168,204-213`. Test: none.
- **SWT-KEY-09** — MUST. When `readOnly`, keyboard activation MUST NOT change the state.
  Source: `switch/root/SwitchRoot.tsx:144-146`. Test: none (pointer equivalent: "should not change its state when clicked" under `prop: readOnly`).

### 6.2 Pointer and touch

Pointer type (mouse/touch/pen) makes no difference. There is no hover, drag, long-press or right-click behavior.

| Gesture | Behavior | Req |
|---|---|---|
| Click on root (any pointer, or synthetic `click()`) | Root click default prevented; a click carrying modifier state is dispatched on the hidden input → toggle. | SWT-PTR-01, SWT-PTR-02 |
| Click on root while disabled | Default prevented, nothing else (`pointerdown` default is also prevented). | SWT-PTR-08 |
| Click on root while `readOnly` | Ignored entirely (default not prevented). | SWT-PTR-07 |
| Click on the hidden input directly | Toggle (unless `readOnly`/disabled). | SWT-PTR-04 |
| Click on a wrapping native `<label>` | Browser activates the hidden input → toggle. | SWT-PTR-05 |
| Click on a native `<label htmlFor=…>` | Browser activates the element with that id (hidden input or native-button root) → toggle. | SWT-PTR-06 |
| Click on a non-native `Field.Label` (`nativeLabel={false}`) | Focuses the switch; no toggle. | SWT-PTR-10 |

- **SWT-PTR-01** — MUST. A click on the root (not disabled, not read-only) MUST toggle the switch by dispatching a `click` on the hidden checkbox input; that click MUST be a bubbling, cancelable, composed pointer-type click with `detail` 0, empty `pointerType`, and the root click's `shiftKey`/`ctrlKey`/`altKey`/`metaKey`.
  Source: `switch/root/SwitchRoot.tsx:143-156`, `utils/dispatchClickWithModifiers.ts:20-41`. Test: [L4] `SwitchRoot.test.tsx` "should change its state when clicked", "should report keyboard modifier event properties when calling onCheckedChange".
- **SWT-PTR-02** — MUST. The root's click handler MUST call `preventDefault()` on the root click before dispatching to the input (so that an enclosing `<label>`'s activation behavior does not deliver a second click to the input). This happens only when not disabled and not read-only; the hidden input MUST be dispatched to only if it is mounted.
  Source: `switch/root/SwitchRoot.tsx:148-155`. Test: [L4] `SwitchRoot.test.tsx` "should toggle the switch when a wrapping <label> is clicked" (single toggle).
- **SWT-PTR-03** — MUST. The click dispatched on the hidden input MUST NOT propagate to ancestors: for one user click on the root, ancestor click listeners MUST observe exactly one click event (the original), with both `nativeButton` values.
  Source: `switch/root/SwitchRoot.tsx:194-198`. Test: [L4] `SwitchRoot.test.tsx` "propagates a single click event to ancestors per user click (nativeButton=false)" and "(nativeButton=true)". Docs: CHANGELOG v1.7.0 "Stop internal input clicks from reaching ancestors".
- **SWT-PTR-04** — MUST. A click delivered directly to the hidden input (e.g. by a label or script) MUST toggle the switch exactly as a root click does.
  Source: `switch/root/SwitchRoot.tsx:172-193`. Test: [L2] `SwitchRoot.test.tsx` "should update its state if the underlying input is toggled".
- **SWT-PTR-05** — MUST. With `nativeButton=false`, clicking anywhere on a native `<label>` that wraps the switch MUST toggle it once.
  Source: `switch/root/SwitchRoot.tsx:148,236` (input is a labelable descendant of the label). Test: [L4] `SwitchRoot.test.tsx` "should toggle the switch when a wrapping <label> is clicked". Docs: `components/switch/page.mdx` §Labeling a switch.
- **SWT-PTR-06** — MUST. Clicking a native `<label htmlFor={id}>` MUST toggle the switch when `id` equals the switch's `id` prop, for both `nativeButton=false` (label targets the hidden input) and `nativeButton=true` (label targets the root `<button>`).
  Source: `switch/root/SwitchRoot.tsx:84-85,121,164`. Test: [L4] `SwitchRoot.test.tsx` "should toggle the switch when a explicitly linked <label> is clicked", "should associate `id` with the native button when `nativeButton=true`". Docs: `components/switch/page.mdx` §Rendering as a native button.
- **SWT-PTR-07** — MUST. When `readOnly`, a click on the root MUST be ignored (no `preventDefault`, no dispatch), and a click reaching the hidden input (from a label or directly) MUST NOT change the state: the input's change is reverted and `onCheckedChange` is not called.
  Source: `switch/root/SwitchRoot.tsx:144-146,178-181`. Test: [L4] `SwitchRoot.test.tsx` "should not change its state when clicked" (readOnly), "should not change its state when its label is clicked". Docs: CHANGELOG v1.4.0 "Prevent input state changes in `readOnly` mode".
- **SWT-PTR-08** — MUST. When disabled, a click on the root MUST have its default prevented and MUST NOT toggle; `pointerdown` on the root MUST also have its default prevented; consumer `onClick`/`onMouseDown`/`onPointerDown` handlers MUST NOT be invoked.
  Source: `internals/use-button/useButton.ts:104-115,218-224`, `switch/root/SwitchRoot.tsx:144-146`. Test: [L4] `SwitchRoot.test.tsx` "should not change its state when clicked" (disabled).
- **SWT-PTR-09** — MUST. A consumer `onClick` on the root MUST run before the internal toggle for the same click, exactly once per click. Calling `stopPropagation()` in it MUST stop the click reaching ancestors but MUST NOT prevent the toggle. Calling `preventBaseUIHandler()` on the event MUST prevent the toggle. Server: relaxed, see §9.
  Source: `merge-props/mergeProps.ts:17-19,210-239`, `switch/root/SwitchRoot.tsx:221-224`. Test: [L4] `SwitchRoot.test.tsx` "should call onClick when clicked", "does not propagate to ancestors when stopPropagation() is called (nativeButton=false/true)".
- **SWT-PTR-10** — MUST. Clicking a `Field.Label` rendered as a non-native label (`nativeLabel={false}`) MUST focus the switch's root and MUST NOT toggle it.
  Source: `internals/labelable-provider/useLabel.ts:46-62,70-76`, `switch/root/SwitchRoot.tsx:199-201`. Test: [L4] `SwitchRoot.test.tsx` "when rendering a non-native label".
- **SWT-PTR-11** — MUST. A click event delivered to the hidden input whose default was already prevented before dispatch MUST be ignored: `onCheckedChange` MUST NOT be called and the state MUST NOT change.
  Source: `switch/root/SwitchRoot.tsx:173-176`. Test: [L4] `SwitchRoot.test.tsx` "ignores a hidden input click canceled before React handles it".

### 6.3 Focus management

- **SWT-FOC-01** — MUST. The root MUST carry `tabindex="0"` in both modes (with `nativeButton=true` too, including while disabled, where the `disabled` attribute still removes it from the tab order), except per SWT-FOC-02, making it the only tab stop of the component; the hidden input (`tabindex="-1"`) MUST never be a tab stop. A consumer `tabIndex` prop overrides the built-in value.
  Source: `utils/useFocusableWhenDisabled.ts:11,30-36`, `internals/use-button/useButton.ts:18,28-32`, `switch/root/SwitchRoot.tsx:168`; see [UBT-DOM-03](../architecture/button-behavior.md). Test: [L2] `SwitchRoot.test.tsx` "can be activated with Enter key" (`[Tab]` focuses the root), "can render a native button" (L4 for the tab-order clause).
- **SWT-FOC-02** — MUST. When disabled and `nativeButton=false`, the root MUST have `tabindex="-1"` (removed from the tab order but still focusable programmatically). When disabled and `nativeButton=true`, the `disabled` attribute makes it unfocusable.
  Source: `utils/useFocusableWhenDisabled.ts:33-35,45-47`. Test: [L2] `SwitchRoot.test.tsx` "does not set [data-focused] when disabled" (programmatic focus event on a disabled root).
- **SWT-FOC-03** — MUST. If the hidden input receives focus (e.g. from a label click or a focus-first-invalid routine), focus MUST be moved immediately to the root element.
  Source: `switch/root/SwitchRoot.tsx:199-201`. Test: none.
  Port note 2026-10-23: the redirect is the `form` module's on the hidden input (FFI-FOC-05; its options name the root as the focus target). Port title "focus on the hidden input moves to the switch" [L4], both render modes.
- **SWT-FOC-04** — MUST. Within a `Field.Root`, the field's `focused` state MUST become `true` when the root receives focus (unless disabled) and `false` when it loses focus, provided this switch is the control that last set it.
  Source: `switch/root/SwitchRoot.tsx:80,127-137`, `internals/field-root-context/useSetFieldFocused.ts:22-30`. Test: [L2] `SwitchRoot.test.tsx` "[data-focused]", "does not set [data-focused] when disabled".
- **SWT-FOC-05** — MUST. Within a `Field.Root`, if the root is focused and then becomes disabled or unmounts without a blur event, the field's `focused` state MUST be cleared.
  Source: `internals/field-root-context/useSetFieldFocused.ts:36-43`. Test: [L4] `SwitchRoot.test.tsx` "is removed when the focused switch becomes disabled", "is removed when the focused switch unmounts".

### 6.4 Dismissal

Not applicable — Switch has no popup or dismissable surface.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onCheckedChange` | `(checked: boolean, eventDetails) => void` | A click reaches the hidden input and is not ignored (not read-only, not disabled, not pre-canceled), before the state updates | `'none'` | yes (`cancel()`) |
| `onClick`, `onFocus`, `onBlur`, `onKeyDown`, … | standard DOM handler props forwarded to the root | per DOM | — | see SWT-PTR-09 / SWT-KEY-08 |

- **SWT-EVT-01** — MUST. `onCheckedChange(nextChecked, details)` MUST be called synchronously inside the hidden input's change handling, with `nextChecked` = the input's post-click `checked` property, before the component's state is updated.
  Source: `switch/root/SwitchRoot.tsx:183-186`. Test: [L2] `SwitchRoot.test.tsx` "should call onCheckedChange when clicked" (called once with `true`).
- **SWT-EVT-02** — MUST. `details.reason` MUST always be `'none'`; keyboard, pointer and label activations are not distinguished.
  Source: `switch/root/SwitchRoot.tsx:184,326`. Test: none.
- **SWT-EVT-03** — MUST. `details.event` MUST be the native `click` event received by the hidden input (the component-dispatched click for root/keyboard activation; the browser's own click for label or direct-input activation), so that it carries the originating modifier-key state. `details.trigger` is `undefined`.
  Source: `switch/root/SwitchRoot.tsx:184`, `internals/createBaseUIEventDetails.ts:138-155`. Test: [L4] `SwitchRoot.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange".
- **SWT-EVT-04** — MUST. If the callback calls `details.cancel()`, the state MUST NOT change (in either ownership mode), the hidden input's `checked` property MUST be restored to the current state, and no field side effects (dirty, filled, validation, error clearing) MUST occur. Server: relaxed, see §9.
  Source: `switch/root/SwitchRoot.tsx:188-192`. Test: [L2] `SwitchRoot.test.tsx` "does not change state when canceled via a root click", "does not change state when canceled via a hidden input click", "preserves Field validation props through canceled changes, submit, and reset" (L4 for the `checked`-property restore clause).
- **SWT-EVT-05** — MUST. `onCheckedChange` MUST NOT be called when `checked` changes because the consumer changed the prop, nor on mount.
  Source: `switch/root/SwitchRoot.tsx:172-193` (only path to the callback is the input change). Test: [L2] `SwitchRoot.test.tsx` "revalidates when a controlled value changes externally" (validation runs; callback not asserted — implied by `onCheckedChange={setChecked}` not looping).
- **SWT-EVT-06** — MUST. `onCheckedChange` MUST be called exactly once per user toggle, regardless of whether the toggle came from the root, keyboard, label or direct input click.
  Source: `switch/root/SwitchRoot.tsx:148,186,197`. Test: [L2] `SwitchRoot.test.tsx` "should call onCheckedChange when clicked" (`calls.length === 1`) (L4 for the root, keyboard and label clauses).
- **SWT-EVT-07** — MUST. `onCheckedChange` MUST NOT be called while `readOnly` or disabled, nor for a pre-canceled input click (SWT-PTR-11).
  Source: `switch/root/SwitchRoot.tsx:144-146,174-181`. Test: [L4] `SwitchRoot.test.tsx` "ignores a hidden input click canceled before React handles it".
- **SWT-EVT-08** — MUST. `details.allowPropagation()` has no effect for this component (nothing stops propagation of the original user event); `isPropagationAllowed` merely reflects whether it was called.
  Source: `internals/createBaseUIEventDetails.ts:144-152` (no consumer in Switch). Test: none.

## 8. Form integration

Mechanism: [FFI](../architecture/field-and-form-infrastructure.md). This section states what Switch contributes.

**What Switch registers with the field.** Control element = the root (focused by `Form` when the field is first-invalid on submit); registration id = the root's generated id; value = `checked` (a boolean — this is what `validate(value, formValues)` receives and what `Form`'s `onFormSubmit` values contain under the field name); name = the `name` prop (used only when `Field.Root` has no `name`); registration is dropped while disabled. The field's validation input handle is attached to the hidden checkbox input, so native constraint validation (`required` → `valueMissing`) is read from it.

**Timing of field writes** (all run synchronously after the DOM commit that reflects the new `checked`, before paint):

1. `filled ← checked` (on mount and on every change).
2. On every change of `checked` (not on mount): clear the enclosing `Form`'s external error for the effective name; `dirty ← checked !== initialValue`; notify validation of the new value (`validation.change(checked)`).
3. On blur (only if the hidden input exists and the switch is not disabled): `touched ← true`; `focused ← false`; if the field's validation mode is `onBlur`, commit a full validation of the hidden input's current `checked`.

- **SWT-FRM-01** — MUST. When checked and an effective name exists, form submission MUST include one entry `name = value` where `value` is the `value` prop or `"on"` when undefined.
  Source: `switch/root/SwitchRoot.tsx:161,165,204`. Test: [L4] `SwitchRoot.test.tsx` "should include the switch value in form submission, matching native checkbox behavior", "matches native checkbox form submission behavior", "submits custom value and uncheckedValue across an off/on/off cycle".
  Port note 2026-10-22: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3).
- **SWT-FRM-02** — MUST. When unchecked and `uncheckedValue` is undefined, form submission MUST include no entry for the name.
  Source: `switch/root/SwitchRoot.tsx:233`. Test: [L4] `SwitchRoot.test.tsx` "should include the switch value in form submission, matching native checkbox behavior" (`null`), "matches native checkbox form submission behavior" (`getAll` → `[]`).
  Port note 2026-10-22: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3).
- **SWT-FRM-03** — MUST. When unchecked and `uncheckedValue` is defined, form submission MUST include exactly one entry `name = uncheckedValue`.
  Source: `switch/root/SwitchRoot.tsx:233-235`. Test: [L4] `SwitchRoot.test.tsx` "should submit uncheckedValue when switch is off and uncheckedValue is specified", "should submit custom uncheckedValue when switch is off", "submits uncheckedValue to an external form when off".
  Port note 2026-10-22: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3).
- **SWT-FRM-04** — MUST. When disabled, form submission MUST include no entry for the name in either state.
  Source: `switch/root/SwitchRoot.tsx:162,234`. Test: [L4] `SwitchRoot.test.tsx` "does not submit uncheckedValue when disabled".
  Port note 2026-10-22: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3).
- **SWT-FRM-05** — MUST. With `form="<id>"`, both hidden inputs MUST be submitted with the `<form>` of that id even when rendered outside it.
  Source: `switch/root/SwitchRoot.tsx:163,234`. Test: [L4] `SwitchRoot.test.tsx` "submits to an external form when `form` is provided", "submits uncheckedValue to an external form when off".
  Port note 2026-10-22: re-tagged from L2, because every cited title builds `FormData` in a browser (`it.skipIf(isJSDOM)`; ADR 06 §1, ADR 13 §3).
- **SWT-FRM-06** — MUST. Within a `Field.Root`, the switch MUST register as the field's control with value = `checked`, control element = root, and name = `name` prop; it MUST unregister while disabled and on unmount. Server: relaxed, see §9.
  Source: `switch/root/SwitchRoot.tsx:94`, `internals/field-register-control/useRegisterFieldControl.ts:21-45`. Test: [L2] `SwitchRoot.test.tsx` "prop: validationMode=onSubmit" (field validated on submit), "validates once when changed by the user" (`validate` receives `true`).
- **SWT-FRM-07** — MUST. The field's dirty baseline (`initialValue`) MUST be the `checked` value at the switch's first registration within that field and MUST NOT be re-captured on later changes.
  Source: `internals/field-register-control/useFieldControlRegistration.ts:86-103`. Test: [L2] `SwitchRoot.test.tsx` "[data-dirty]".
- **SWT-FRM-08** — MUST. With `required` and unchecked, submitting an enclosing `Form` MUST mark the field invalid with native `valueMissing` (`aria-invalid="true"` on the root; `Field.Error match="valueMissing"` rendered), MUST prevent the submit, and MUST focus the root.
  Source: `switch/root/SwitchRoot.tsx:166`, `field/root/useFieldValidation.ts:194-223`, `form/Form.tsx:41-69,109-121`, `internals/field-register-control/useFieldControlRegistration.ts:53-63`. Test: [L4] `SwitchRoot.test.tsx` "triggers native HTML validation on submit", "prop: validationMode=onSubmit", "preserves Field validation props through canceled changes, submit, and reset".
  Port note 2026-10-23: the field reads the hidden input's native validity from the `form` module's reports, and reads it once when the input's handle attaches as well as after every change, so a required, unchecked switch is `valueMissing` at its first submit (the module reports each enrolled element at submit by its id, and the switch registers under its root's). Port title "a required switch is invalid at its first submit, before any toggle" [L2].
- **SWT-FRM-09** — MUST. The field's `validate` function MUST be invoked with the boolean `checked` value exactly once per user toggle when on-change validation applies (mode `onChange`, or mode `onSubmit` after the first submit).
  Source: `switch/root/SwitchRoot.tsx:100-105`, `field/root/useFieldValidation.ts:349-365`, `field/root/FieldRoot.tsx:88-92`. Test: [L2] `SwitchRoot.test.tsx` "validates once when changed by the user", "prop: validationMode=onChange". Docs: CHANGELOG v1.5.0 "Fix extra `validate` fn calls".
- **SWT-FRM-10** — MUST. A change of a consumer-owned `checked` that did not come from user interaction MUST trigger the same on-change validation as a user toggle (one additional `validate` call with the new value).
  Source: `switch/root/SwitchRoot.tsx:100-105`, `internals/useValueChanged.ts:10-16`. Test: [L2] `SwitchRoot.test.tsx` "revalidates when a controlled value changes externally".
- **SWT-FRM-11** — MUST. In validation mode `onBlur`, blurring the root MUST run a full validation of the hidden input's current `checked` (native constraints then `validate`); changing the value alone MUST NOT mark the field invalid.
  Source: `switch/root/SwitchRoot.tsx:139-141`. Test: [L2] `SwitchRoot.test.tsx` "prop: validationMode=onBlur". Docs: CHANGELOG v1.8.0 "Fix controlled blur validation and stale filled state".
- **SWT-FRM-12** — MUST. In validation mode `onSubmit`, after the first submit every toggle MUST re-validate (so a required switch flips between invalid and valid as it is toggled off and on).
  Source: `field/root/FieldRoot.tsx:88-92`, `field/root/useFieldValidation.ts:356-364`. Test: [L2] `SwitchRoot.test.tsx` "prop: validationMode=onSubmit".
- **SWT-FRM-13** — MUST. On every change of `checked`, any external `Form` error registered under the effective name MUST be cleared (removing `aria-invalid` and the `Field.Error` content).
  Source: `switch/root/SwitchRoot.tsx:101`, `form/Form.tsx:143-155`. Test: [L2] `SwitchRoot.test.tsx` "clears external errors on change".
- **SWT-FRM-14** — MUST. The field's `touched` state MUST become `true` on the root's blur, except when the switch is disabled or the hidden input is not mounted.
  Source: `switch/root/SwitchRoot.tsx:130-137`. Test: [L2] `SwitchRoot.test.tsx` "[data-touched]".
- **SWT-FRM-15** — MUST. A native form reset MUST NOT change the switch's checked state, its `aria-invalid`, or its `aria-describedby`.
  Source: `switch/root/SwitchRoot.tsx:87-92,236` (state is component-held, not read back from the input). Test: [L4] `SwitchRoot.test.tsx` "preserves Field validation props through canceled changes, submit, and reset".
- **SWT-FRM-16** — MUST. Within a `Field.Root`, `Field.Label` MUST be associated with the switch: its `for` MUST equal the hidden input's id when `nativeButton=false`, or the root's id when `nativeButton=true`, and the root's `aria-labelledby` MUST equal the label's id (subject to SWT-ARIA-06). Clicking a native `Field.Label` MUST toggle the switch (wrapping or sibling).
  Source: `switch/root/SwitchRoot.tsx:84,111-118,121,164`, `internals/labelable-provider/useLabel.ts:64-69`. Test: [L2] `SwitchRoot.test.tsx` "sets `for` on the label", "when the label is sibling to the switch", "when rendering a non-native button" (L4 for the label-click clause). Docs: `components/switch/page.mdx` §Form integration; `handbook/forms/page.mdx` §Naming form controls; CHANGELOG v1.0.0-beta.0 "Support implicit `Field.Label`".
- **SWT-FRM-17** — MUST. `Field.Description` and `Field.Error` ids MUST be appended to the root's `aria-describedby` (SWT-ARIA-10) and set on the hidden input (SWT-DOM-15); these MUST survive canceled changes, submit and reset.
  Source: `switch/root/SwitchRoot.tsx:160,225`. Test: [L2] `SwitchRoot.test.tsx` "Field.Description", "preserves Field validation props through canceled changes, submit, and reset" (L4 for the submit and reset clause).
- **SWT-FRM-18** — MUST. When `Field.Root` has no `name`, the switch's `name` prop MUST serve as the field's name for `Form` error lookup and submitted values.
  Source: `switch/root/SwitchRoot.tsx:94` (name passed to registration), `field/root/FieldRoot.tsx:61-62`. Test: none.

## 9. Lifecycle, mounting and animation

Not applicable — all parts are always mounted; there is no transition status, `keepMounted`, or completion callback. CSS transitions on `data-checked` are the consumer's concern (the docs demo animates `translate` on `[data-checked]`).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- SWT-PTR-09 — row "EVD-EVT-26 against composite/button key handling": a C# consumer's `PreventBaseUIHandler()` on the root click is synchronous on WebAssembly through `decide()`; on Server the press is handled regardless (fallback "run"); a consumer JavaScript listener's `preventDefault()` is honoured on both modes. Order (2026-10-22, M2 track 4): on WebAssembly the consumer's C# `onClick` runs inside the `activate` decision, before the toggle; on Server the `button` module's toggle report reaches C# before Blazor dispatches the same click (ADR-04/1), so the consumer's C# `onClick` runs after the toggle request.
- SWT-EVT-04, SWT-STA-03 (the restore of the hidden input's `checked`) — row "C#-origin consumer vetoes of a toggle on a hidden input" (added 2026-10-22): met on WebAssembly, where the `toggle` decision reverts the box inside the click; on Server the visually hidden input stays as the click left it for about two round trips, measured 2026-10-22 (about 615 ms with 150 ms added each way, five clicks, `page.routeWebSocket`): the toggle report goes up, the render batch comes down, the batch's acknowledgement goes up (on Server `OnAfterRender` runs only after it, ADR 01 Context), and the `dom` primitive's `update` comes down, until the `dom` primitive restores it, while the root's `aria-checked` and data attributes never change.
- SWT-KEY-08 — rows "EVD-EVT-26 against composite/button key handling" and "UBT-KEY-03/04": met on both modes when the veto is a consumer JavaScript listener's `preventDefault()` (the `button` module checks `defaultPrevented`); a C# consumer's `PreventBaseUIHandler()` is synchronous on WebAssembly through `decide()` and a round trip late on Server, where the key is handled (fallback "run"); Blazor event directives are rejected on parts.
- SWT-STA-06 — row "FFI registration" (post-commit, pre-paint C# state work): the field's `filled` write lands after paint plus a round trip; observable only as latency of `data-filled`.
- SWT-ARIA-08 — row "FFI registration": the native-label fallback re-evaluation runs after the render (`OnAfterRender`), on Server after paint plus a round trip; observable only as latency of `aria-labelledby`.
- SWT-FRM-06 — row "FFI registration": field-control registration runs after paint plus a round trip; observable only as latency of dependent attribute updates.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (Root) | [RND](../architecture/rendering-and-composition.md) | default tag `span`; props merged in order: built-in root props → consumer props → button-behavior props (as a getter receiving the merged props) → field validation props (as a getter); three DOM handles merged (consumer, internal root handle, button handle); state attribute mapping = field validity mapping + `checked` → `data-checked`/`data-unchecked` |
| `useRenderElement` (Thumb) | RND | default tag `span`; same state object and mapping as Root; consumer props only |
| `getStateAttributesProps` | RND | booleans → `data-<lowercase key>=""`; custom mapping for `checked` and `valid` |
| `useButton` | [UBT](../architecture/button-behavior.md) | `{ disabled: effectiveDisabled, native: nativeButton }`; `focusableWhenDisabled` unset (→ `false`), `tabIndex` default `0`, `composite` inferred (Switch is never a composite item in its own tests) |
| `useFocusableWhenDisabled` (via `useButton`) | UBT | non-native: `tabindex` `0`/`-1`, `aria-disabled`; native: `tabindex` `0`, `disabled` attribute |
| `dispatchClickWithModifiers` | UBT | called with the hidden input and the root click (default `detail: 0`, `pointerType: ''`) |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | reason `'none'`, event = native input click, no trigger, no custom fields |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `{ controlled: checked, default: Boolean(defaultChecked), name: 'Switch', state: 'checked' }` |
| `useBaseUiId` / `useId` | UTL / RND | one generated id for the root (non-native) and field registration; `"<input id>-label"` for an auto-labelled native `<label>` |
| `visuallyHidden` / `visuallyHiddenInput` | UTL | hidden input style chosen by presence of effective name |
| `useValueChanged` | UTL | watches `checked`; runs error clearing, dirty, validation change |
| `useFieldRootContext`, `useSetFieldFocused`, `useRegisterFieldControl`, `useFormContext` | [FFI](../architecture/field-and-form-infrastructure.md) | see §8: registration `(rootHandle, generatedId, checked, undefined, !disabled, nameProp)`; focus target = root; `clearErrors(effectiveName)`; `setFilled(checked)`; `setDirty(checked !== initialValue)`; `validation.change(checked)`; `validation.commit(input.checked)` on blur in `onBlur` mode; `validation.getValidationProps(disabled, props)` on root and `validation.getValidationProps(disabled)` on hidden input; `validation.inputRef` attached to hidden input |
| `useLabelableContext`, `useLabelableId`, `useAriaLabelledBy` | FFI | `useLabelableId({ id: idProp })` registers the control id; `useAriaLabelledBy(ariaLabelledByProp, labelId, hiddenInputHandle, enableFallback = !nativeButton, hiddenInputId, ariaLabel)` |
| `mergeProps` | RND | consumer handlers run before built-in ones; `preventBaseUIHandler()` suppresses the built-in handler |

## 12. Edge cases, invariants and dev warnings

All messages below are emitted only in non-production builds, through `console.error`, prefixed `Base UI: `, and each distinct message is logged at most once per page lifetime.

- **SWT-EDGE-01** — MUST. Rendering `Switch.Thumb` outside a `Switch.Root` MUST throw an error with the message `Base UI: SwitchRootContext is missing. Switch parts must be placed within <Switch.Root>.`
  Source: `switch/root/SwitchRootContext.ts:11-15`. Test: [L2] `SwitchThumb.test.tsx` "throws a descriptive error when rendered outside <Switch.Root>".
  Port note 2026-10-22: the message is ``Blazix: The `SwitchRoot` context is missing. Switch parts must be placed within `<SwitchRoot>`.`` (the port's words under ADR 08 Amendments 2026-10-20; the PRG-EDGE-01 precedent); the error is an `InvalidOperationException` thrown in every build.
- **SWT-EDGE-02** — SHOULD. If `checked` switches between `undefined` and a boolean after the first render, a dev error SHOULD be logged: `A component is changing the uncontrolled checked state of Switch to be controlled.` (or `…the controlled checked state of Switch to be uncontrolled.`) followed by `Elements should not switch from uncontrolled to controlled (or vice versa).`, `Decide between using a controlled or uncontrolled Switch element for the lifetime of the component.`, `The nature of the state is determined during the first render. It's considered controlled if the value is not `undefined`.`, `More info: https://fb.me/react-controlled-components` (newline-separated).
  Source: `packages/utils/src/useControlled.ts:48-64`. Test: none.
- **SWT-EDGE-03** — SHOULD. If `defaultChecked` changes after the first render of a component-owned switch, a dev error SHOULD be logged: `A component is changing the default checked state of an uncontrolled Switch after being initialized. To suppress this warning opt to use a controlled Switch.` The rendered state is unaffected.
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: none.
- **SWT-EDGE-04** — SHOULD. If `nativeButton` is `true` but the rendered root is not a `<button>`, a dev error SHOULD be logged: `A component that acts as a button expected a native <button> because the `nativeButton` prop is true. Rendering a non-<button> removes native button semantics, which can impact forms and accessibility. Use a real <button> in the `render` prop, or set `nativeButton` to `false`.` If `nativeButton` is `false` but the rendered root is a `<button>`: `A component that acts as a button expected a non-<button> because the `nativeButton` prop is false. Rendering a <button> keeps native behavior while Base UI applies non-native attributes and handlers, which can add unintended extra attributes (such as `role` or `aria-disabled`). Use a non-<button> in the `render` prop, or set `nativeButton` to `true`.`
  Source: `internals/use-button/useButton.ts:36-66`. Test: none in Switch (see UBT).
- **SWT-EDGE-05** — MUST. Calling `focus()`, `blur()` and `click()` on the root element before the hidden input is attached (e.g. from a DOM-handle callback during mount) MUST NOT throw and MUST leave the switch unchecked.
  Source: `switch/root/SwitchRoot.tsx:131-134,150-153`. Test: [L4] `SwitchRoot.test.tsx` "tolerates imperative interaction in its ref callback before the hidden input mounts".
- **SWT-EDGE-06** — MUST. `readOnly` MUST NOT affect form submission, focusability, `tabindex`, or `data-disabled`; it only blocks state changes from user input and adds `aria-readonly`/`data-readonly`.
  Source: `switch/root/SwitchRoot.tsx:124,144,178`. Test: none.
- **SWT-EDGE-07** — MUST. A consumer `aria-label` consisting only of whitespace MUST be treated as absent for the purpose of SWT-ARIA-06 (the label association is kept), while still being forwarded as an attribute.
  Source: `internals/labelable-provider/useAriaLabelledBy.ts:18`. Test: none.
- **SWT-EDGE-08** — MUST. Multiple switches inside one `Field.Root` are not a supported configuration; the last registered control owns the field's value, and only the focus-owning switch clears `focused`. A port MUST at least reproduce the single-switch behavior; no guarantee is required for multiple.
  Source: `internals/field-root-context/useSetFieldFocused.ts:7-14`, `internals/field-register-control/useFieldControlRegistration.ts:147-168`. Test: none.
- **SWT-EDGE-09** — MUST. No behavior depends on writing direction (RTL) or locale.
  Source: `switch/root/SwitchRoot.tsx:1-27` (no direction or locale import). Test: none.
- **SWT-EDGE-10** — MUST. Server-rendered markup MUST contain the same root/hidden-input structure and attributes as the client render for the initial state, with deterministic ids, so that no attribute changes on hydration except the hidden input's browser-managed properties.
  Source: `switch/root/SwitchRoot.tsx:236` (`suppressHydrationWarning` on the input only). Test: none.

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND.

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. New IDs are introduced only where no earlier requirement captured the assertion.

### `SwitchRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, `testRenderPropWith: 'span'`, `button: true`) | SWT-DOM-17 |
| "tolerates imperative interaction in its ref callback before the hidden input mounts" | SWT-EDGE-05 |
| "should change its state when clicked" | SWT-PTR-01, SWT-STA-02, SWT-ARIA-02 |
| "should update its state when changed from outside" | SWT-STA-03 |
| "should update its state if the underlying input is toggled" | SWT-PTR-04 |
| "ignores a hidden input click canceled before React handles it" | SWT-PTR-11, SWT-EVT-07 |
| "can be activated with $key key" (it.each, 2 variants: Enter, Space) | SWT-KEY-01, SWT-KEY-02, SWT-FOC-01 |
| "should override the built-in attributes" | SWT-DOM-14 |
| "sets `aria-labelledby` from a sibling label associated with the hidden input" | SWT-ARIA-07 |
| "updates fallback `aria-labelledby` when the hidden input id changes" | SWT-ARIA-08, SWT-DOM-18 |
| "prefers `aria-label` over an associated label" | SWT-ARIA-06 |
| "should call onCheckedChange when clicked" | SWT-EVT-01, SWT-EVT-06 |
| "should report keyboard modifier event properties when calling onCheckedChange" | SWT-EVT-03, SWT-PTR-01 |
| "does not change state when canceled via a root click" / "does not change state when canceled via a hidden input click" | SWT-EVT-04 |
| "should call onClick when clicked" | SWT-PTR-09 |
| "propagates a single click event to ancestors per user click (nativeButton=%s)" (it.each, 2 variants: false, true) | SWT-PTR-03 |
| "does not propagate to ancestors when stopPropagation() is called (nativeButton=%s)" (it.each, 2 variants: false, true) | SWT-PTR-09 |
| "uses aria-disabled instead of HTML disabled" / "should not have the `disabled` attribute when `disabled` is not set" | SWT-ARIA-05 |
| "should not change its state when clicked" (disabled) | SWT-PTR-08 |
| "should have the `aria-readonly` attribute" / "should not have the aria attribute when `readOnly` is not set" | SWT-ARIA-03 |
| "should not change its state when clicked" (readOnly) / "should not change its state when its label is clicked" | SWT-PTR-07 |
| "should have the `aria-required` attribute" / "should not have the aria attribute when `required` is not set" | SWT-ARIA-04 |
| "should be able to access the native input" | SWT-API-12 |
| "should place the style hooks on the root and the thumb" | SWT-DOM-02, SWT-DOM-03, SWT-DOM-06, SWT-STA-08 |
| "should set the name attribute only on the input" | SWT-API-07, SWT-DOM-12 |
| "should not set the value attribute by default" / "should set the value attribute only on the input" | SWT-DOM-11, SWT-DOM-12 |
| "should toggle the switch when a wrapping <label> is clicked" | SWT-PTR-05 |
| "should toggle the switch when a explicitly linked <label> is clicked" | SWT-PTR-06, SWT-DOM-10 |
| "should associate `id` with the native button when `nativeButton=true`" | SWT-DOM-01, SWT-DOM-10, SWT-PTR-06 |
| "preserves Field validation props through canceled changes, submit, and reset" | SWT-EVT-04, SWT-FRM-08, SWT-FRM-15, SWT-FRM-17 |
| "should include the switch value in form submission, matching native checkbox behavior" | SWT-FRM-01, SWT-FRM-02 |
| "submits to an external form when `form` is provided" | SWT-FRM-05 |
| "submits uncheckedValue to an external form when off" | SWT-FRM-03, SWT-FRM-05 |
| "does not submit uncheckedValue when disabled" | SWT-FRM-04 |
| "matches native checkbox form submission behavior" | SWT-FRM-01, SWT-FRM-02 |
| "should submit uncheckedValue when switch is off and uncheckedValue is specified" / "should submit custom uncheckedValue when switch is off" / "submits custom value and uncheckedValue across an off/on/off cycle" | SWT-FRM-01, SWT-FRM-03 |
| "triggers native HTML validation on submit" | SWT-FRM-08 |
| "clears external errors on change" | SWT-FRM-13 |
| "should receive disabled prop from Field.Root" | SWT-API-04 |
| "should receive name prop from Field.Root" | SWT-API-07 |
| "[data-touched]" | SWT-FRM-14, SWT-DOM-04 |
| "[data-dirty]" | SWT-STA-07 |
| "adds [data-filled] attribute when checked after being initially unchecked" / "removes [data-filled] attribute when unchecked after being initially checked" / "clears [data-filled] when a controlled switch remounts unchecked" | SWT-STA-06 |
| "[data-focused]" / "does not set [data-focused] when disabled" | SWT-FOC-04 |
| "is removed when the focused switch becomes disabled" / "is removed when the focused switch unmounts" | SWT-FOC-05 |
| "prop: validationMode=onSubmit" | SWT-FRM-08, SWT-FRM-12, SWT-ARIA-11 |
| "prop: validationMode=onChange" | SWT-FRM-09 |
| "validates once when changed by the user" | SWT-FRM-09 |
| "revalidates when a controlled value changes externally" | SWT-FRM-10 |
| "prop: validationMode=onBlur" | SWT-FRM-11 |
| "sets `for` on the label" (implicit Field.Label) | SWT-FRM-16 |
| "when the label is sibling to the switch" / "when rendering a non-native button" | SWT-FRM-16 |
| "when rendering a non-native label" | SWT-PTR-10 |
| "Field.Description" | SWT-ARIA-10, SWT-DOM-15 |
| "can render a native button" | SWT-KEY-03, SWT-DOM-01 |

### `SwitchThumb.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`, rendered inside a provided root state) | SWT-DOM-17 |
| "throws a descriptive error when rendered outside <Switch.Root>" | SWT-EDGE-01 |

### Additional requirements introduced here

- **SWT-DOM-17** — MUST. Both parts MUST pass the shared conformance suite from RND: custom attributes forwarded to the default element and to a substituted element (function and element forms, with `nativeButton={false}` passed for Root); `style` from the component and from the substituted element both applied; DOM handle resolves to an `HTMLSpanElement`; substituted element may be wrapped; component and substituted-element DOM handles both receive the element; `className` string and function merge with the substituted element's class; string `className` applied. For Root, `testRenderPropWith` is `span`, so the suite's extra "non-native button" pass does not run.
  Source: `test/describeConformance.tsx:55-76`, `test/conformanceTests/propForwarding.tsx:22-101`, `test/conformanceTests/renderProp.tsx:40-160`, `test/conformanceTests/refForwarding.tsx:13-26`, `test/conformanceTests/className.tsx:14-25`. Test: [L2] `SwitchRoot.test.tsx` / `SwitchThumb.test.tsx` "Base UI component API".
- **SWT-DOM-18** — MUST. When the `id` prop changes at runtime (`nativeButton=false`), the hidden input's `id` MUST change accordingly and the root's fallback `aria-labelledby` MUST switch to the label now associated with the new id, assigning that label an id if it has none; the previously assigned label id is left in place.
  Source: `switch/root/SwitchRoot.tsx:84-85,164`, `internals/labelable-provider/useAriaLabelledBy.ts:26-35,46-48`. Test: [L2] `SwitchRoot.test.tsx` "updates fallback `aria-labelledby` when the hidden input id changes" (L4 for the native-label fallback clause).

## 14. React-specific notes and porting considerations

Non-normative.

- **Change detection via the native input.** React's `onChange` for a checkbox fires from the input's `click` event; Base UI reads `event.currentTarget.checked` after the browser has flipped it. A port that does not drive a real `<input type="checkbox">` must compute `next = !checked` itself and construct an equivalent event object (carrying modifier keys) for `details.event`. Port: see decisions/05-state-store-and-event-details.md §2 (C#-rendered hidden input, request sequence); decisions/04-js-interop-boundary.md §3 (`details.event` snapshot).
- **Pre-canceled clicks.** The `nativeEvent.defaultPrevented` guard works around React issue 9023 (React fires `onChange` even for a click whose default was prevented, in which case the browser reverts the checkbox). A port should simply ignore canceled clicks (SWT-PTR-11). Port: see decisions/05-state-store-and-event-details.md §2.
- **Controlled-input restoration.** After `cancel()`, React restores the hidden input's `checked` to the prop value as part of its controlled-input machinery. A port must explicitly re-sync the hidden input (SWT-EVT-04). The same machinery is what keeps the input in sync after a native form reset; see §16 item 1. Port: see decisions/05-state-store-and-event-details.md §2 (`Controlled<T>`).
- **`value` only when defined.** React < 19 writes an empty `value=""` attribute when `undefined` is passed explicitly, so the source spreads `value` conditionally. Ports should just omit the attribute when undefined (SWT-DOM-11). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (null attribute values emit no frame).
- **`suppressHydrationWarning` on the hidden input** silences React's hydration diff for browser-managed input state; not observable, but server markup must match (SWT-EDGE-10).
- **Layout-effect timing.** `setFilled`, the value-changed reaction (clear errors, dirty, validation), field-control registration, label-id registration and the `aria-labelledby` fallback all run in layout effects: synchronously after the DOM update, before paint. The fallback runs after *every* commit with no dependency list. A port should run these as "after render, before paint" hooks; for the fallback, a per-render DOM query (or a mutation-driven recomputation) is required. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 and §Relaxation table row "FFI registration".
- **StrictMode / remount guards.** `useSetFieldFocused` re-runs on mount to reclaim focus that no focus event reported (StrictMode double-invocation, pre-hydration focus). `useFieldControlRegistration` captures the dirty baseline once per field, not per control instance, so a keyed remount of the switch does not reset the baseline — but a keyed remount of `Field.Root` does.
- **Prop merging order.** `useRenderElement` receives `[rootProps, elementProps, getButtonProps, validationPropsGetter]`. Objects merge left-to-right (later attribute wins), but event handlers chain right-to-left (consumer handler before built-in) with `preventBaseUIHandler()` as the opt-out. The two getters receive the merged props so far and wrap/augment them. A port needs an equivalent "consumer handler first, can veto internal handler" composition. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3; decisions/04-js-interop-boundary.md §1 (button module) and §3 (`decide()`, EVD-EVT-26).
- **Ids.** With React ≥ 18, generated ids are `base-ui-` + `React.useId()` output (e.g. `base-ui-«r1»`); React 17 falls back to a global counter assigned after mount (ids are `undefined` for one render). Ports should generate stable, SSR-safe ids eagerly. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 5.
- **Context identity.** The root publishes its state object (memoized) to Thumb; Thumb re-renders only when that object changes. No port-relevant behavior beyond SWT-STA-08. Port: see decisions/05-state-store-and-event-details.md §1; decisions/02-render-pipeline-and-attribute-merging.md §Decision 4 (`data-*` projection).
- **`error()` logging** is dev-only and deduplicated per message string (`createLogOnce`); porting it as a once-per-message console error is sufficient.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| SWT-STR-01 | MUST | Root renders root element, optional hidden-value input, hidden checkbox as siblings | 2 |
| SWT-STR-02 | MUST | Thumb must be inside Root; throws otherwise | 2 |
| SWT-STR-03 | MUST | Thumb is structural only | 2 |
| SWT-API-01 | MUST | `checked` defined on first render → consumer-owned | 3.1 |
| SWT-API-02 | MUST | initial state `Boolean(defaultChecked)` | 3.1 |
| SWT-API-03 | MUST | `onCheckedChange(checked, details)` signature | 3.1 |
| SWT-API-04 | MUST | effective disabled = field OR prop; default false | 3.1 |
| SWT-API-05 | MUST | `readOnly` default false; blocks user toggles; `aria-readonly` | 3.1 |
| SWT-API-06 | MUST | `required` default false; input `required`; `aria-required` | 3.1 |
| SWT-API-07 | MUST | effective name = field name ?? prop; on inputs only | 3.1 |
| SWT-API-08 | MUST | `value` on input only; absent when undefined | 3.1 |
| SWT-API-09 | MUST | `uncheckedValue` submitted only when off and named | 3.1 |
| SWT-API-10 | MUST | `form` on both inputs, not root | 3.1 |
| SWT-API-11 | MUST | `id` → hidden input (non-native) or root (native) | 3.1 |
| SWT-API-12 | MUST | `inputRef` → hidden checkbox. Port: withdrawn (ADR 02 Amendments 2026-10-22) | 3.1 |
| SWT-API-13 | MUST | `nativeButton` default false; selects UBT mode | 3.1 |
| SWT-API-14 | MUST | `className`/`style`/`render` per RND with state object | 3.1 |
| SWT-API-15 | MUST | other props forwarded to root; consumed list; `onChange` excluded | 3.1 |
| SWT-API-16 | MUST | Thumb props forwarded; customization per RND | 3.2 |
| SWT-STA-01 | MUST | ownership fixed on first render | 4 |
| SWT-STA-02 | MUST | component-owned toggle sets state from input | 4 |
| SWT-STA-03 | MUST | consumer-owned: DOM follows prop only | 4 |
| SWT-STA-04 | MUST | state object has exactly nine fields | 3.1 |
| SWT-STA-05 | MUST | controlled → undefined falls back to initial default | 4 |
| SWT-STA-06 | MUST | field `filled` = checked, on mount and change, before paint | 4 |
| SWT-STA-07 | MUST | field `dirty` = checked !== initialValue on change | 4 |
| SWT-STA-08 | MUST | Thumb state identical to Root state | 4 |
| SWT-DOM-01 | MUST | root id: generated (non-native) / control id (native) | 5 |
| SWT-DOM-02 | MUST | exactly one of data-checked/data-unchecked | 5 |
| SWT-DOM-03 | MUST | data-disabled/readonly/required | 5 |
| SWT-DOM-04 | MUST | data-touched/dirty/filled/focused only within Field | 5 |
| SWT-DOM-05 | MUST | data-valid/data-invalid/neither | 5 |
| SWT-DOM-06 | MUST | Thumb carries same data attributes | 5 |
| SWT-DOM-07 | MUST | hidden input type/aria-hidden/tabindex | 5 |
| SWT-DOM-08 | MUST | hidden input checked/disabled/required/name/form mirror | 5 |
| SWT-DOM-09 | MUST | hidden input visually-hidden styles (two variants) | 5 |
| SWT-DOM-10 | MUST | hidden input id = control id (non-native) / none (native) | 5 |
| SWT-DOM-11 | MUST | `value` attribute only when defined | 5 |
| SWT-DOM-12 | MUST | no `name`/`value` on root | 5 |
| SWT-DOM-13 | MUST | hidden-value input rendering conditions | 5 |
| SWT-DOM-14 | MUST | consumer attributes override built-in | 5 |
| SWT-DOM-15 | MUST | hidden input gets field describedby/invalid | 5 |
| SWT-DOM-16 | MUST | `type="button"` when native | 5 |
| SWT-DOM-17 | MUST | both parts pass RND conformance suite | 13 |
| SWT-DOM-18 | MUST | runtime `id` change re-targets hidden input and fallback label | 13 |
| SWT-ARIA-01 | MUST | `role="switch"` | 5 |
| SWT-ARIA-02 | MUST | `aria-checked` | 5 |
| SWT-ARIA-03 | MUST | `aria-readonly` only when readOnly | 5 |
| SWT-ARIA-04 | MUST | `aria-required` only when required | 5 |
| SWT-ARIA-05 | MUST | `aria-disabled` (non-native) vs `disabled` (native) | 5 |
| SWT-ARIA-06 | MUST | `aria-labelledby` precedence; aria-label suppresses | 5 |
| SWT-ARIA-07 | MUST | native label fallback discovery and assigned id | 5 |
| SWT-ARIA-08 | MUST | fallback re-evaluated on every render of the part | 5 |
| SWT-ARIA-09 | MUST | fallback disabled when native | 5 |
| SWT-ARIA-10 | MUST | `aria-describedby` = consumer + field message ids | 5 |
| SWT-ARIA-11 | MUST | `aria-invalid` when field invalid and not disabled | 5 |
| SWT-KEY-01 | MUST | non-native Enter keydown toggles | 6.1 |
| SWT-KEY-02 | MUST | non-native Space: keydown prevents, keyup toggles | 6.1 |
| SWT-KEY-03 | MUST | native: Enter/Space not intercepted | 6.1 |
| SWT-KEY-04 | MUST | descendant key events ignored | 6.1 |
| SWT-KEY-05 | MUST | Tab not intercepted | 6.1 |
| SWT-KEY-06 | MUST | disabled: keys inert | 6.1 |
| SWT-KEY-07 | MUST | modifier state carried to change event | 6.1 |
| SWT-KEY-08 | MUST | consumer preventDefault cancels Enter (keydown)/Space (keyup) | 6.1 |
| SWT-KEY-09 | MUST | readOnly: keyboard no-op | 6.1 |
| SWT-PTR-01 | MUST | root click → dispatched click on input with modifiers | 6.2 |
| SWT-PTR-02 | MUST | root click preventDefault; only if input mounted | 6.2 |
| SWT-PTR-03 | MUST | internal input click does not propagate; one click per user click | 6.2 |
| SWT-PTR-04 | MUST | direct input click toggles | 6.2 |
| SWT-PTR-05 | MUST | wrapping native label toggles once | 6.2 |
| SWT-PTR-06 | MUST | `htmlFor` label toggles (both modes) | 6.2 |
| SWT-PTR-07 | MUST | readOnly: root click ignored; input change reverted | 6.2 |
| SWT-PTR-08 | MUST | disabled: click/pointerdown prevented; no consumer handlers | 6.2 |
| SWT-PTR-09 | MUST | consumer onClick first; stopPropagation keeps toggle; preventBaseUIHandler cancels | 6.2 |
| SWT-PTR-10 | MUST | non-native Field.Label click focuses, no toggle | 6.2 |
| SWT-PTR-11 | MUST | pre-canceled input click ignored | 6.2 |
| SWT-FOC-01 | MUST | root tabindex 0 (both modes); input never a tab stop | 6.3 |
| SWT-FOC-02 | MUST | disabled: tabindex -1 (non-native) / unfocusable (native) | 6.3 |
| SWT-FOC-03 | MUST | focus on hidden input redirected to root | 6.3 |
| SWT-FOC-04 | MUST | field focused set/cleared on focus/blur | 6.3 |
| SWT-FOC-05 | MUST | field focused cleared on disable/unmount without blur | 6.3 |
| SWT-EVT-01 | MUST | callback before state update with input's checked | 7 |
| SWT-EVT-02 | MUST | reason always 'none' | 7 |
| SWT-EVT-03 | MUST | event = native input click; trigger undefined | 7 |
| SWT-EVT-04 | MUST | cancel(): no state change, input restored, no field effects | 7 |
| SWT-EVT-05 | MUST | not called for prop changes or mount | 7 |
| SWT-EVT-06 | MUST | exactly once per toggle | 7 |
| SWT-EVT-07 | MUST | not called when readOnly/disabled/pre-canceled | 7 |
| SWT-EVT-08 | MUST | allowPropagation no effect | 7 |
| SWT-FRM-01 | MUST | checked → name=value ?? "on" | 8 |
| SWT-FRM-02 | MUST | unchecked, no uncheckedValue → no entry | 8 |
| SWT-FRM-03 | MUST | unchecked + uncheckedValue → one entry | 8 |
| SWT-FRM-04 | MUST | disabled → no entries | 8 |
| SWT-FRM-05 | MUST | `form` attribute → external form | 8 |
| SWT-FRM-06 | MUST | registers with Field (value, control, name); unregisters when disabled | 8 |
| SWT-FRM-07 | MUST | dirty baseline captured once | 8 |
| SWT-FRM-08 | MUST | required + unchecked → valueMissing on submit, blocks, focuses | 8 |
| SWT-FRM-09 | MUST | validate(checked) once per toggle in on-change modes | 8 |
| SWT-FRM-10 | MUST | external controlled change revalidates | 8 |
| SWT-FRM-11 | MUST | onBlur mode: full validation on blur | 8 |
| SWT-FRM-12 | MUST | onSubmit mode: revalidate on change after first submit | 8 |
| SWT-FRM-13 | MUST | external Form error cleared on change | 8 |
| SWT-FRM-14 | MUST | touched on blur unless disabled/unmounted input | 8 |
| SWT-FRM-15 | MUST | form reset does not change state/aria-invalid/describedby | 8 |
| SWT-FRM-16 | MUST | Field.Label `for`/`aria-labelledby` association; click toggles | 8 |
| SWT-FRM-17 | MUST | description/error ids on root and input; survive cancel/submit/reset | 8 |
| SWT-FRM-18 | MUST | switch `name` serves as field name when Field has none | 8 |
| SWT-EDGE-01 | MUST | Thumb outside Root throws (message) | 12 |
| SWT-EDGE-02 | SHOULD | controlled/uncontrolled switch dev error (message) | 12 |
| SWT-EDGE-03 | SHOULD | defaultChecked change dev error (message) | 12 |
| SWT-EDGE-04 | SHOULD | nativeButton/element mismatch dev errors (messages) | 12 |
| SWT-EDGE-05 | MUST | imperative focus/blur/click before input mounts tolerated | 12 |
| SWT-EDGE-06 | MUST | readOnly affects only user toggles + aria/data | 12 |
| SWT-EDGE-07 | MUST | blank aria-label treated as absent for labelling | 12 |
| SWT-EDGE-08 | MUST | multiple switches per Field unsupported; single-switch behavior required | 12 |
| SWT-EDGE-09 | MUST | no RTL/locale dependence | 12 |
| SWT-EDGE-10 | MUST | SSR markup matches client initial render | 12 |

## 16. Open questions

1. **Form reset vs. hidden input.** `SwitchRoot.test.tsx:584-591` asserts only that the root's `aria-checked` survives a native reset. Whether the hidden input's `checked` property is re-synchronized after the browser resets it depends on React's controlled-input handling (React 19 keeps `defaultChecked` in sync with `checked`; React 18 does not), i.e. `switch/root/SwitchRoot.tsx:236` relies on framework behavior. SWT-FRM-15 requires only the state to be stable; a port should additionally re-sync the hidden input after `reset` so a subsequent submit matches the state. Confirm desired behavior.
   Resolved 2026-10-22 (M2 track 4, option (b), chosen by the user 2026-10-22): the `dom` primitive re-asserts the hidden input's `checked` one task after the owning form's `reset` (FFI-FRM-30's dated note), so a submit after a reset matches the state; the alternative (a), mirroring Base UI, would have sent the `checked` attribute rendered at first. Tested by `js/dom.test.ts` ("SwitchRoot hidden input checked after a form reset") and `switch.spec.ts` ("port: a native form reset keeps the state and re-syncs the hidden input, so the next submit matches it").
2. **`data-filled` wording.** `switch/root/SwitchRootDataAttributes.ts:37-40` documents `data-filled` as "Present when the switch is active"; the implementation (`SwitchRoot.tsx:96-98`) makes it exactly `checked`. Documented here as "equals `checked`".
3. **`aria-describedby`/`aria-invalid` on an `aria-hidden` input.** `SwitchRoot.tsx:160` spreads the field validation props onto the hidden input (asserted by "Field.Description", line 1301). Harmless but redundant for assistive technology; kept as SWT-DOM-15 because a test asserts it.
4. **Prevented Space keydown.** `internals/use-button/useButton.ts:204-206` documents that a prevented *keydown* cannot cancel non-native Space activation (keyup fires the click). This diverges from native `<button>` semantics; SWT-KEY-08 states the actual behavior. Decide whether the port should mirror the limitation or fix it.
   Resolved 2026-10-22 (M2 track 4, settled by the orchestrator 2026-10-22 under UBT-KEY-05 and the `button` module's existing behavior): the port mirrors the limitation, as the `button` module already does (no state is kept from the keydown); SWT-KEY-08 stands as written.
5. **Field registration name.** `SwitchRoot.tsx:94` passes `nameProp` (not the effective name) to field registration while `clearErrors` (`:101`) uses the effective name. Both resolve identically because `Field.Root` prefers its own `name`; noted in case the FFI document chooses a single term.
   Resolved 2026-10-23 (M2 track 4, with track 1's term): the switch registers its **own name** (`FieldControlRegistration.Name`: the `Name` parameter, else the `CheckedExpression` identifier's field name, ADR 05 Amendments 2026-10-22), and the field's **effective name** is FFI-STA-10's `Field.Root.name ?? <name registered by the active control>`, which errors are looked up and cleared by and the hidden input carries. SWT-API-07 and SWT-FRM-18 use these two terms.
6. **Fallback label id format.** The assigned id `"<hidden input id>-label"` (`useAriaLabelledBy.ts:16`) is only tested for non-emptiness (`SwitchRoot.test.tsx:137,162`). SWT-ARIA-07 states the exact format as implemented; a port may treat the format as SHOULD if the FFI document relaxes it. Resolved: FFI-ARIA-06 (`docs/architecture/field-and-form-infrastructure.md`) states the `${controlId}-label` format as MUST, so SWT-ARIA-07 stays MUST.
7. **Docs vs. source on root element.** `components/switch/page.mdx` §Rendering as a native button says a native `<button>` is preferred for sibling labels; the source does not warn when a sibling `<label htmlFor>` targets a non-native switch (it works via the hidden input's id, tested at line 491). No discrepancy in behavior; the guidance is advisory.
