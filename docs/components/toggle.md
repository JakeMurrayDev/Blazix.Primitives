# Toggle

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/toggle/` |
| Tests | `packages/react/src/toggle/**/*.test.tsx` (1 file) |
| Docs page | `docs/src/app/(docs)/react/components/toggle/page.mdx` |
| Prefix | `TGL` |
| Status | approved |
| Revised | 2026-10-27 |
| Builds on | none |
| Derived by | [toggle-group](toggle-group.md) (owns a set of Toggles), [toolbar](toolbar.md) (hosts Toggles as roving items) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Toggle is a two-state push button: a `<button>` whose `aria-pressed` attribute reports whether it is currently *pressed*. Activating it with a pointer, <kbd>Enter</kbd> or <kbd>Space</kbd> flips the pressed state and reports the new value through `onPressedChange`. The pressed state may be owned by the consumer (`pressed`) or by the component (`defaultPressed`). Unlike Switch and Checkbox it is not a form control: it renders no hidden input, never submits a value, and the `form` and `type` attributes are deliberately stripped so the element is always a `type="button"` button that cannot submit a form.

A Toggle has two modes of existence. **Standalone**, it is an ordinary button-like part with its own pressed state. **Grouped** — rendered anywhere inside a [ToggleGroup](toggle-group.md) — it surrenders ownership of its pressed state to the group (pressed exactly when its `value` is in the group's value array), becomes a composite item of a roving-tabindex group (its `tabindex` is `0` only when it holds the group's tab stop, <kbd>Space</kbd> activates on keydown), and forwards each activation to the group so that single- or multi-select semantics are applied there. The `value` prop is the toggle's identity within a group; when omitted (or falsy) a generated id is used so that value-less toggles still behave independently.

Toggle participates in [Toolbar](toolbar.md) in two ways: as a direct child of a ToggleGroup placed inside `Toolbar.Root` (the toggle registers with the toolbar's roving group and publishes `{ disabled, focusableWhenDisabled: false }` metadata so that a disabled toggle is skipped), or as the element substituted into `Toolbar.Button` (`<Toolbar.Button render={<Toggle />} />`), in which case the toolbar button's own attributes and focusable-when-disabled behavior are layered on top.

## 2. Anatomy

```
<Toggle>                      required, 1          default element: button
```

Toggle is a single part with no sub-parts. It may be rendered:

```
<Toggle />                                     standalone
<ToggleGroup> … <Toggle value="x" /> … </ToggleGroup>      grouped (anywhere in the group's subtree)
<Toolbar.Root> <ToggleGroup> <Toggle value="x" /> …         grouped inside a toolbar
<Toolbar.Root> <Toolbar.Button render={<Toggle />} value="x" />   substituted into a toolbar button
```

- **TGL-STR-01** — MUST. Toggle MUST render exactly one element (default `<button>`) and no additional DOM nodes (no hidden input, no wrapper).
  Source: `toggle/Toggle.tsx:109-114,123-138`. Test: [L2] `Toggle.test.tsx` conformance suite "Base UI component API" (`refInstanceof: HTMLButtonElement`).
- **TGL-STR-02** — MUST. When an enclosing `ToggleGroup` exists (at any depth), Toggle MUST register itself as a composite item of the nearest enclosing composite root (the ToggleGroup's own roving group, or the Toolbar's when the ToggleGroup is inside `Toolbar.Root`); when no ToggleGroup encloses it, it MUST NOT register as a composite item even if a composite root (such as `Toolbar.Root`) encloses it.
  Source: `toggle/Toggle.tsx:43,110,123-135`; `internals/composite/item/CompositeItem.tsx:25-33`. Test: [L2] `Toggle.test.tsx` "should pass composite props"; `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children". See [CMP-STR-01](../architecture/composite-and-list-navigation.md) (L4 for the navigation clause).
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): stays `[L2]`. "should pass composite props" reads the roving `tabIndex` from the render callback, which the `composite` module writes from attach, so it is restated for what C# observes: `ToggleGroupTests` "port: should pass composite props (the toggle registers its id with the group's root, and the render function receives its state)" asserts the toggle's id in the composite's `attach` items, and `ToggleTests` "port: under a composite root without a group a toggle is in composite button mode and is not an item" the "MUST NOT register" clause. The navigation clause runs in `toggle-group.spec.ts` ("$direction, orientation: $orientation moves focus with arrow keys"). The `ToolbarButton.test.tsx` clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): inside a `ToolbarRoot` a grouped toggle is an item of the toolbar's root, because the toolbar's `ToggleGroup` cascades no registry of its own (`ToggleGroupToolbarTests` "port: inside a toolbar the group is no composite root, and its toggles register with the toolbar's"). Its registration carries the flags `{ disabled: effective, focusableWhenDisabled: false }` (TLB §4; M3 track 1's E3; "port: the direct toggles of a toolbar's group send their disabled state and are not focusable when disabled, also after a change"). The cited `ToolbarButton.test.tsx` title runs in `toolbar.spec.ts`.

## 3. Public API

### 3.1 `Toggle`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `pressed` | `boolean` | — (uncontrolled) | Whether the toggle is pressed. Consumer-owned when defined on first render. Ignored inside a ToggleGroup. | TGL-API-01 |
| `defaultPressed` | `boolean` | `false` | Initial pressed state when component-owned. Ignored inside a ToggleGroup. | TGL-API-02 |
| `onPressedChange` | `(pressed: boolean, eventDetails: Toggle.ChangeEventDetails) => void` | — | Called on every user activation with the next pressed value, before the state changes. | TGL-API-03 |
| `disabled` | `boolean` | `false` | Ignore user interaction. Combined (OR) with the enclosing ToggleGroup's `disabled`. | TGL-API-04 |
| `value` | `Value extends string` | generated id | Identity of the toggle inside a ToggleGroup. A falsy value (`undefined`, `""`) is replaced by a generated `base-ui-…` id. | TGL-API-05 |
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>` (see [UBT](../architecture/button-behavior.md) §3). | TGL-API-06 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | TGL-API-07 |
| `form` | `string` | — | **Consumed and discarded** — never forwarded (the outline omits it; it exists only in the `'button'` attribute type). | TGL-API-08 |
| `type` | `string` | — | **Consumed and discarded** — the element is always `type="button"` in native mode. | TGL-API-08 |
| *other `<button>` attributes and handlers* | — | — | Forwarded to the element. | TGL-API-08 |

**Consumed (not forwarded to the element):** `pressed`, `defaultPressed`, `onPressedChange`, `disabled`, `value`, `nativeButton`, `className`, `style`, `render`, `form`, `type`.

- **TGL-API-01** — MUST. Outside a ToggleGroup, when `pressed` is not `undefined` on the first render, the pressed state MUST equal `pressed` on every render and MUST NOT be changed internally by user interaction (consumer-owned mode, [UTL-STA-01](../architecture/shared-utilities.md), [UTL-STA-04](../architecture/shared-utilities.md)).
  Source: `toggle/Toggle.tsx:61-66`; `packages/utils/src/useControlled.ts:41,46,83-90`. Test: [L2] `Toggle.test.tsx` "controlled".
- **TGL-API-02** — MUST. Outside a ToggleGroup, when `pressed` is `undefined` on the first render, the initial pressed state MUST be `defaultPressed`, whose default is `false`.
  Source: `toggle/Toggle.tsx:28,63`. Test: [L2] `Toggle.test.tsx` "uncontrolled".
- **TGL-API-03** — MUST. `onPressedChange` MUST receive the next pressed value (`!pressed`) as its first argument and a change-event-details object (§3.2) as its second; firing rules are in §7.
  Source: `toggle/Toggle.tsx:83-88,177-178`. Test: [L2] `Toggle.test.tsx` "is called when the pressed state changes".
- **TGL-API-04** — MUST. The effective disabled state MUST be `disabled || group.disabled` where `group.disabled` is the enclosing ToggleGroup's effective disabled state (`false` when there is no group); the prop default is `false`.
  Source: `toggle/Toggle.tsx:29,46`. Test: [L2] `Toggle.test.tsx` "disables the component"; `ToggleGroup.test.tsx` "can disable the whole group", "can disable individual items".
- **TGL-API-05** — MUST. The toggle's identity MUST be the `value` prop when it is a non-empty string; when `value` is `undefined` or `""` the identity MUST be a generated, document-unique id prefixed `base-ui-` ([RND](../architecture/rendering-and-composition.md) `useBaseUiId`), stable for the lifetime of the instance.
  Source: `toggle/Toggle.tsx:41-42`; `internals/useBaseUiId.ts:9-11`; `packages/utils/src/useId.ts:42-47`. Test: [L2] `ToggleGroup.test.tsx` "when Toggles omit value" (uncontrolled and multiple variants). Docs: `CHANGELOG.md v1.0.0 "More permissive towards falsy toggle values (#3477)"`.
  Port note 2026-10-26 (ADR 08 Amendments 2026-10-26, chosen by the user; toggle §16 Q6): the identity is `Value` when it is neither `null` nor `""`, else the part's generated id, drawn once at first render. A toggle draws it only inside a ToggleGroup, where it also renders it as its `id` and registers it with the composite root, or under `RenderFunction`, where the twin attaches by it; a standalone toggle draws none and renders no `id`, as Base UI renders none (design point 12.1 of M3 tracks 2-3, option (a), settled by the orchestrator 2026-10-26: ADR 08 Amendments 2026-10-26, ADR 04 Amendments 2026-10-22 (an item registers by the id its part renders) and the Button and RadioRoot `UsesGeneratedId` precedents; the generator's sequence differs from Base UI's `useId`, which no test observes). Asserted by `ToggleGroupTests` "port: a toggle's identity is its Value, else its generated id, which an empty Value does not replace".
- **TGL-API-06** — MUST. `nativeButton` defaults to `true` and selects native vs non-native button mode per [UBT-API-02](../architecture/button-behavior.md) / [UBT-API-03](../architecture/button-behavior.md); it is never forwarded as an attribute.
  Source: `toggle/Toggle.tsx:36,68-71`; `internals/types.ts:63-71`. Test: [L2] `Toggle.test.tsx` conformance suite "with a non-native button" (re-run with a `div` and `nativeButton={false}`).
- **TGL-API-07** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md) with the state object of §3.1 "State object"; in grouped mode they are passed through the composite item wrapper unchanged.
  Source: `toggle/Toggle.tsx:109-114,125-134`. Test: [L2] `Toggle.test.tsx` conformance suite "Base UI component API"; `docs/src/app/(docs)/react/components/toggle/demos/hero/css-modules/index.tsx` (`render` function receives `state.pressed`).
- **TGL-API-08** — MUST. `form` and `type` MUST be removed from the props and never reach the element; every other prop not listed as consumed MUST be forwarded to the element, merged after the toggle's own attributes so that a consumer value for the same attribute wins (e.g. a consumer `aria-pressed`), except that `onClick`, `onMouseDown`, `onPointerDown`, `onKeyDown`, `onKeyUp` are wrapped by the button mechanism ([UBT-EVT-01](../architecture/button-behavior.md)).
  Source: `toggle/Toggle.tsx:30,34,38,79-107`. Test: [L2] `Toggle.test.tsx` conformance suite "Base UI component API" (prop forwarding); `ToolbarButton.test.tsx` "handles interactions" (Toggle block: consumer `onPressedChange` and forwarded `value`).
  Port note 2026-10-26 (ADR 02 Amendments 2026-10-21, `FinishAttributes`; toggle §16 Q5): the consumer's `form` and `type` arrive in the unmatched attributes and the part removes them after the merge, keeping the writer's `type="button"` in native mode (`ToggleTests` "port: a toggle renders no hidden input, drops the consumer's form and type, and never renders its Value", "port: a consumer attribute of the same name wins, except type"). The port's button mechanism declines the consumer's `onclick` only with the hidden-input bridge, which Toggle does not use, so the consumer's `onclick` runs before the toggle's own in the pipeline's chain (TGL-PTR-03's note). The `ToolbarButton.test.tsx` clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the forwarded `value` reaches the substituted toggle as its `Value` (`ToggleGroupToolbarTests` "port: a toggle substituted into a toolbar button registers once, …"; `toolbar.spec.ts` "handles interactions"). The consumer's `OnPressedChange` on the substitute stays its typed parameter (design point 14.8, settled by the orchestrator 2026-10-27: ADR 02 Amendments 2026-10-27) and is counted per activation in `toolbar.spec.ts` "handles interactions".

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes.

| Field | Type | Meaning |
|---|---|---|
| `pressed` | `boolean` | Current pressed state (group-derived when grouped). |
| `disabled` | `boolean` | Effective disabled (TGL-API-04). |

- **TGL-STA-01** — MUST. The state object exposed to `className`/`style`/`render` MUST contain exactly the two fields above.
  Source: `toggle/Toggle.tsx:73-76,145-154`. Test: [L2] `Toggle.test.tsx` "should pass composite props" (render function invoked in grouped mode; asserts only `tabIndex`, not the state fields); `docs/src/app/(docs)/react/components/toggle/demos/hero/css-modules/index.tsx` (reads `state.pressed`).
  Port note 2026-10-26 (ADR 13 §3): stays `[L2]`; the cited title is restated for what C# observes (TGL-STR-02's note): the render function of a grouped toggle receives `ToggleState(Pressed, Disabled)` (`ToggleGroupTests` "port: should pass composite props (…)"; `ToggleTests` "port: a render function receives the toggle's state, and the toggle draws an id to attach by"). `ToggleState` is a sealed record with exactly these two fields.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-pressed` | `""` while `pressed` is `true` | TGL-DOM-02 |
| `data-disabled` | `""` while effective disabled | TGL-DOM-03 |

#### CSS variables

None.

### 3.2 Shared types

- `Toggle.ChangeEventReason` = `'none'` — the only literal. Source: `toggle/Toggle.tsx:186`, `internals/reason-parts.ts:1`.
- `Toggle.ChangeEventDetails` = the common change-event-details shape from [EVD-API-01](../architecture/event-details-and-reasons.md) specialised to reason `'none'`: `{ reason: 'none'; event: Event; trigger: Element | undefined; cancel(); allowPropagation(); isCanceled; isPropagationAllowed }`. Source: `toggle/Toggle.tsx:188`, `internals/createBaseUIEventDetails.ts:56-85,126-157`.
- `Toggle.State` — the state object in §3.1. `Toggle.Props<Value extends string = string>` — the prop table (generic over the `value` type). Source: `toggle/Toggle.tsx:190-195`.
  Port note 2026-10-26 (ADR 08 Amendments 2026-10-26, chosen by the user): `Toggle` is not generic. `Value extends string` is a literal-type device of TypeScript with no C# counterpart; `Value` is a `string?`, and the group's value an `IReadOnlyList<string>`.
- Exported constant namespace `ToggleDataAttributes` = `{ pressed: 'data-pressed', disabled: 'data-disabled' }`. Source: `toggle/ToggleDataAttributes.ts:4,8`, `toggle/index.ts:5`.
- `NativeButtonProps` (the `nativeButton` prop with default `true`) from [UBT](../architecture/button-behavior.md) §3.

## 4. State model

The only state the component owns is `pressed`, and it owns it only when standalone. Everything else in the state object is a prop echo.

**Ownership (standalone).** `pressed`/`defaultPressed` is the controlled/uncontrolled pair. Ownership is decided once, on the first render, by whether `pressed` is `undefined` (TGL-API-01/02). A user activation in consumer-owned mode only fires `onPressedChange`; the DOM reflects the new value only when the consumer passes it back.

**Ownership (grouped).** Inside a ToggleGroup the pressed state is always consumer-owned from the toggle's point of view: on every render it equals "the group's current value array contains this toggle's identity". The `pressed` and `defaultPressed` props are ignored, and the toggle's internal setter is a no-op; the group decides what the activation means (single-select replacement, multi-select add/remove, see [TGG-STA-03](toggle-group.md), [TGG-STA-04](toggle-group.md)).

**Derived values.**

- identity = `value` if truthy, else generated id (TGL-API-05)
- effective disabled = `disabled || group.disabled` (TGL-API-04)
- `aria-pressed` = `pressed`; `data-pressed` present iff `pressed`

There is no value normalization (the value is a boolean) and no mount/unmount lifecycle.

- **TGL-STA-02** — MUST. Standalone ownership of `pressed` MUST be fixed on the first render: consumer-owned if `pressed !== undefined`, otherwise component-owned; later crossings between `undefined` and a boolean MUST NOT change the mode (a dev diagnostic is logged instead, TGL-EDGE-02), and a consumer-owned value that later becomes `undefined` MUST fall back to the first render's `defaultPressed`.
  Source: `toggle/Toggle.tsx:61-66`; `packages/utils/src/useControlled.ts:41-46`. Test: none. See [UTL-STA-01](../architecture/shared-utilities.md), [UTL-STA-03](../architecture/shared-utilities.md).
  Port note 2026-10-26: asserted by `ToggleTests` "port: ownership is fixed at the first parameter set, and a removed Pressed falls back to the initial default" (`Controlled<bool>`, ADR 05 Amendments 2026-10-18).
- **TGL-STA-03** — MUST. In standalone component-owned mode, a non-canceled activation MUST set `pressed` to `!pressed` (the value previously passed to `onPressedChange`).
  Source: `toggle/Toggle.tsx:83,102`. Test: [L2] `Toggle.test.tsx` "uncontrolled".
- **TGL-STA-04** — MUST. Inside a ToggleGroup, `pressed` MUST be `true` exactly when the group's current value array contains the toggle's identity (string equality), re-evaluated on every render; the `pressed` and `defaultPressed` props MUST have no effect, and the toggle MUST NOT store a pressed value of its own.
  Source: `toggle/Toggle.tsx:44,62`; `packages/utils/src/useControlled.ts:46,83-90`. Test: [L2] `ToggleGroup.test.tsx` "pressed state" (controlled), "prop: value", "prop: defaultValue".
  Port note 2026-10-26 (design point 11.1 of M3 tracks 2-3, option (a), settled by the orchestrator 2026-10-26; the M2 groups' precedents R3 and G1): the group shares its value through a listener list cascaded fixed; on every change each toggle recomputes its membership (ordinal) and renders only when its pressed or disabled state changed, and the group does not render for a selection (`ToggleGroupTests` "port: a selection renders only the toggles whose pressed state changed, and not the group").
- **TGL-STA-05** — MUST. In standalone consumer-owned mode, a change of the `pressed` prop from any cause MUST update `aria-pressed` and `data-pressed` without invoking `onPressedChange`.
  Source: `toggle/Toggle.tsx:61-66,81`. Test: [L2] `Toggle.test.tsx` "controlled".

## 5. Rendered DOM and ARIA

### 5.1 Toggle element

Attributes on the single rendered element. "Composite" below means the toggle is inside a ToggleGroup (TGL-STR-02) **or** inside any other composite root such as `Toolbar.Root` — the button mechanism infers composite mode from the nearest composite root ancestor ([UBT-STA-01](../architecture/button-behavior.md)), independently of whether the toggle registered as an item.

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `aria-pressed` | `"true"` / `"false"`, always present | TGL-ARIA-01 | C# |
| `type` | `"button"` when `nativeButton` is `true` (consumer `type` cannot override) | TGL-DOM-04 | C# |
| `role` | `"button"` when `nativeButton` is `false`; none otherwise | TGL-DOM-04 | C# |
| `tabindex` | standalone: `"0"` (`"-1"` when disabled and non-native); composite: `"0"` for the item holding the tab stop, `"-1"` otherwise | TGL-DOM-05 | C# (standalone); C# → JS (in a group/toolbar, from attach) |
| `disabled` | present per TGL-ARIA-02 matrix | TGL-ARIA-02 | C# |
| `aria-disabled` | `"true"` / `"false"` / absent per TGL-ARIA-02 matrix | TGL-ARIA-02 | C# |
| `data-pressed` | `""` while pressed | TGL-DOM-02 | C# |
| `data-disabled` | `""` while disabled | TGL-DOM-03 | C# |
| *consumer attributes* | forwarded; override the above except `type` | TGL-DOM-06 | C# |

- **TGL-DOM-01** — MUST. The default element MUST be `<button>`; with `render` the substituted element receives all attributes and handlers below.
  Source: `toggle/Toggle.tsx:109,126`. Test: [L2] `Toggle.test.tsx` conformance suite "Base UI component API".
- **TGL-DOM-02** — MUST. `data-pressed=""` MUST be present exactly while `pressed` is `true` and absent otherwise ([RND-DOM-01](../architecture/rendering-and-composition.md), [RND-DOM-02](../architecture/rendering-and-composition.md)).
  Source: `toggle/Toggle.tsx:73-76`; `toggle/ToggleDataAttributes.ts:4`. Test: [L2] `ToggleGroup.test.tsx` "pressed state" (uncontrolled), "prop: defaultValue", "prop: value".
- **TGL-DOM-03** — MUST. `data-disabled=""` MUST be present exactly while the effective disabled state is `true`.
  Source: `toggle/Toggle.tsx:46,74`; `toggle/ToggleDataAttributes.ts:8`. Test: [L2] `Toggle.test.tsx` "disables the component"; `ToggleGroup.test.tsx` "can disable individual items" (absent on the enabled toggle).
- **TGL-DOM-04** — MUST. In native mode the element MUST carry `type="button"` ([UBT-DOM-01](../architecture/button-behavior.md)); because the `type` prop is discarded (TGL-API-08) a consumer cannot change it. In non-native mode the element MUST carry `role="button"` and no `type` ([UBT-DOM-02](../architecture/button-behavior.md)).
  Source: `toggle/Toggle.tsx:34,68-71`; `internals/use-button/useButton.ts:226`. Test: [L2] `Toggle.test.tsx` conformance suite "with a non-native button".
- **TGL-DOM-05** — MUST. Standalone (no composite root ancestor), the element MUST carry `tabindex="0"`, or `tabindex="-1"` when disabled in non-native mode ([UBT-DOM-03](../architecture/button-behavior.md), [UBT-DOM-04](../architecture/button-behavior.md)). Inside a ToggleGroup the element MUST carry the roving value: `"0"` when the toggle's index equals the group's highlighted index, `"-1"` otherwise ([CMP-DOM-01](../architecture/composite-and-list-navigation.md)); the button mechanism emits no `tabindex` in composite mode ([UBT-DOM-05](../architecture/button-behavior.md)). Server: relaxed, see §9.
  Source: `toggle/Toggle.tsx:68-71,123-135`; `utils/useFocusableWhenDisabled.ts:30-36`; `internals/composite/item/useCompositeItem.ts:19,25`. Test: [L4] `Toggle.test.tsx` "should pass composite props" (`tabIndex: 0` on the only item); `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (`tabindex="0"` follows focus).
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's roving `tabindex` from attach; the cell text chosen by the user 2026-10-26, M3 plan §Settled; conventions §3.4, §3.5): the `Writer` cell of §5.1 reads `C# (standalone); C# → JS (in a group/toolbar, from attach)`, the `radio.md` §5.1 precedent. C# renders the standalone values and `-1` on a grouped toggle (CMP-STA-15), unchanged afterwards; the module writes the roving value from attach. The ownership registry has the row (toggle, Toggle, `tabindex`, C# → JS).
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): both cited titles read the roving value the `composite` module writes from attach, which only a browser observes; the port's `RenderProps<ToggleState>.Attributes` carry C#'s `-1`. "should pass composite props" is restated on the DOM attribute as `toggle.spec.ts` "port: should pass composite props (the toggle a render function renders holds the tab stop)"; the second title runs in `toggle-group.spec.ts`. The standalone clause, which C# renders, is asserted at L2 by `ToggleTests` "port: a standalone toggle is a tab stop; disabled, a native one is disabled and a non-native one has tabindex -1", and the grouped `-1` by `ToggleGroupTests` "port: grouped toggles render tabindex -1, which the composite owns from attach, and the group renders none". The first publication is the CMP-STA-10 row (§9).
- **TGL-DOM-06** — MUST. A consumer-supplied attribute with the same name as a built-in one (`aria-pressed`, `role`, `tabIndex`, `disabled`, `aria-disabled`, `data-*`) MUST win ([RND-DOM-19](../architecture/rendering-and-composition.md), [UBT-DOM-09](../architecture/button-behavior.md)); `type` is the single exception (TGL-DOM-04).
  Source: `toggle/Toggle.tsx:79-107` (`elementProps` merged after the built-in attributes and passed through the button props resolver). Test: [L2] `Toggle.test.tsx` conformance suite "Base UI component API" (prop forwarding).

### 5.2 ARIA

- **TGL-ARIA-01** — MUST. The element MUST always carry `aria-pressed` with the string value `"true"` when pressed and `"false"` when not pressed (never absent, never a mixed value).
  Source: `toggle/Toggle.tsx:81`. Test: [L2] `Toggle.test.tsx` "controlled", "uncontrolled"; `ToolbarButton.test.tsx` "renders toggle and toggle group".
- **TGL-ARIA-02** — MUST. The `disabled` attribute and `aria-disabled` MUST follow [UBT-ALG-03](../architecture/button-behavior.md) with `focusableWhenDisabled` unspecified, i.e. exactly this matrix (D = effective disabled):
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` clauses run in `toolbar.spec.ts` (Toggle and ToggleGroup block): a substituted toggle carries the toolbar button's composite rows, `aria-disabled="true"` and no `disabled` once the `button` module clears it (design point 14.1).

  | `nativeButton` | composite root ancestor | D | `disabled` attr | `aria-disabled` |
  |---|---|---|---|---|
  | `true` | no | false | — | — |
  | `true` | no | true | present | — |
  | `true` | yes | false | — | `"false"` |
  | `true` | yes | true | present | `"true"` |
  | `false` | no | false | — | — |
  | `false` | no | true | — | `"true"` |
  | `false` | yes | false | — | — |
  | `false` | yes | true | — | `"true"` |

  The `"false"` row is normative: an enabled native Toggle inside a ToggleGroup (or any composite root) renders `aria-disabled="false"`. A disabled native Toggle inside a ToggleGroup carries **both** `disabled` and `aria-disabled="true"` and is therefore not focusable ([UBT-ARIA-05](../architecture/button-behavior.md), [UBT-FOC-05](../architecture/button-behavior.md)); the JSDoc claim that composite items are focusable when disabled by default does not hold for native toggles. When the toggle is substituted into `Toolbar.Button` the outer mechanism's correction removes the `disabled` attribute again ([UBT-DOM-10](../architecture/button-behavior.md); see [TLB-DOM-08](toolbar.md)).
  Source: `toggle/Toggle.tsx:68-71`; `utils/useFocusableWhenDisabled.ts:15-16,38-47`; `internals/use-button/useButton.ts:25-34`. Test: [L2] `Toggle.test.tsx` "disables the component" (`disabled` attribute, standalone native); `ToggleGroup.test.tsx` "can disable individual items" (`aria-disabled="false"` / `"true"`), "can disable the whole group"; `ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children" (`toBeDisabled()`), "disables direct ToggleGroup children when Toolbar.Group is disabled".
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-26, chosen by the user: composite mode inferred, UBT-STA-01): the matrix is `ButtonAttributeWriter`'s with `focusableWhenDisabled` `null` and `composite` true under any composite root (toggle §16 Q2, Q3). The composite rows are asserted by `ToggleGroupTests` "can disable individual items", "can disable the whole group" and `ToggleTests` "port: under a composite root without a group a toggle is in composite button mode and is not an item"; the standalone rows by "disables the component" and "port: a standalone toggle is a tab stop; …". The `ToolbarButton.test.tsx` clauses are open to M3 track 4 (Toolbar).
- **TGL-ARIA-03** — MUST. Toggle MUST NOT add an accessible name of its own; `aria-label` / `aria-labelledby` / text content supplied by the consumer are forwarded unchanged.
  Source: `toggle/Toggle.tsx:79-107` (no label logic). Test: none. Docs: `components/toggle/demos/hero/css-modules/index.tsx` (`aria-label="Favorite"`).

## 6. Interactions

### 6.1 Keyboard

All key handling comes from the button mechanism ([UBT](../architecture/button-behavior.md) §4 KEY); Toggle attaches no keyboard handler of its own. "Composite" = a composite root ancestor exists (ToggleGroup or Toolbar). "click" = the activation sequence of TGL-PTR-01.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | native, any context | not intercepted; the browser's keydown click activates → click | TGL-KEY-01 |
| <kbd>Enter</kbd> | non-native, any context | keydown: default prevented, synthetic click → click | TGL-KEY-01 |
| <kbd>Space</kbd> | native, standalone | not intercepted; the browser's keyup click → click | TGL-KEY-01 |
| <kbd>Space</kbd> | non-native, standalone | keydown: default prevented (no scroll); keyup: synthetic click → click | TGL-KEY-01 |
| <kbd>Space</kbd> | composite (native `<button>` or non-native) | keydown: default prevented, synthetic click → click; keyup: default prevented (native) / nothing (non-native); exactly one click per press | TGL-KEY-02 |
| <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd>/<kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>, <kbd>Home</kbd>, <kbd>End</kbd> | any | not handled by Toggle; propagate to the enclosing composite root (ToggleGroup / Toolbar) which may move focus | TGL-KEY-03 |
| <kbd>Tab</kbd>, <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | not intercepted; native focus movement | TGL-KEY-04 |
| any key with <kbd>Shift</kbd>/<kbd>Ctrl</kbd>/<kbd>Alt</kbd>/<kbd>Meta</kbd> | any | same as the unmodified key; modifier state is copied onto the synthetic click and therefore onto `details.event` | TGL-KEY-01 |
| any key | disabled | consumer `onKeyDown`/`onKeyUp` not invoked; no activation | TGL-KEY-05 |

- **TGL-KEY-01** — MUST. Standalone, <kbd>Enter</kbd> and <kbd>Space</kbd> MUST activate the toggle per the button mechanism: in native mode the browser's own activation produces the click ([UBT-KEY-09](../architecture/button-behavior.md)); in non-native mode <kbd>Enter</kbd> activates on keydown ([UBT-KEY-01](../architecture/button-behavior.md)) and <kbd>Space</kbd> on keyup ([UBT-KEY-02](../architecture/button-behavior.md)), with modifier keys preserved on the resulting click ([UBT-EVT-04](../architecture/button-behavior.md)).
  Source: `toggle/Toggle.tsx:68-71,106`; `internals/use-button/useButton.ts:116-217`. Test: none in `Toggle.test.tsx` (standalone tests use programmatic `click()`); `ToolbarButton.test.tsx` "handles interactions" (Toggle block: <kbd>Enter</kbd> presses a native Toggle substituted into `Toolbar.Button`; that toggle is in composite mode, where <kbd>Enter</kbd> behaves as here per TGL-KEY-02).
  Port note 2026-10-26: the standalone clause is asserted at L4 by `toggle.spec.ts` "port: Enter and Space activate a standalone toggle once per press, native and non-native"; the `ToolbarButton.test.tsx` parenthetical is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` parenthetical runs in `toolbar.spec.ts` "handles interactions" (Toggle and ToggleGroup block).
- **TGL-KEY-02** — MUST. Inside a composite root, <kbd>Space</kbd> MUST activate on **keydown** (default prevented, synthetic click, internal-handler suppression set) and the keyup MUST NOT produce a second click (native `<button>`: keyup default-prevented) ([UBT-KEY-12](../architecture/button-behavior.md), [UBT-KEY-15](../architecture/button-behavior.md)); <kbd>Enter</kbd> is unchanged.
  Source: `internals/use-button/useButton.ts:25-26,138-152,182-196`. Test: [L4] `ToggleGroup.test.tsx` "key: $key toggles the pressed state" (Enter, Space), "fires when the $key is pressed"; `ToolbarButton.test.tsx` "handles interactions" (Toggle block, <kbd>Space</kbd> on the grouped toggle).
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` clause runs in `toolbar.spec.ts` "handles interactions" (Space on the grouped toggle).
- **TGL-KEY-03** — MUST. Toggle MUST NOT intercept arrow, <kbd>Home</kbd> or <kbd>End</kbd> keys; they bubble to the enclosing composite root, which moves focus per [TGG-KEY-01](toggle-group.md) / [TLB-KEY-01](toolbar.md). Standalone, these keys have no effect.
  Source: `internals/use-button/useButton.ts:155-163` ([UBT-KEY-07](../architecture/button-behavior.md)). Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys".
  Port note 2026-10-26 (design point 12.3 of M3 tracks 2-3, option (a), settled by the orchestrator 2026-10-26; ADR 01 and ADR 04 Amendments 2026-10-26, the one keydown gate; ADR 04 Amendments 2026-10-22, the one-shot marker): Toggle intercepts none of these keys. Both its button mechanism and its composite item decline the consumer's `onkeydown`, and the part's one handler runs it through one gate: in a group, the composite's navigation keys (the arrows, <kbd>Home</kbd> and <kbd>End</kbd>) through the item's, every other key through the button's; standalone, every key through the button's. Each key meets at most one decision point and the handler runs once (`ToggleGroupTests` "port: in a group, arrows, Home and End run the toggle's consumer onkeydown through the item gate, once"; `toggle.spec.ts` "port: a vetoed Home runs the toggle's C# onkeydown once"). A key the root does not handle leaves no marker in either gate, so a root without <kbd>Home</kbd>/<kbd>End</kbd> (Toolbar) and one with them (ToggleGroup, Tabs) follow the same rule.
- **TGL-KEY-04** — MUST. <kbd>Tab</kbd> MUST never be default-prevented ([UBT-KEY-21](../architecture/button-behavior.md)).
  Source: `utils/useFocusableWhenDisabled.ts:24`. Test: none.
- **TGL-KEY-05** — MUST. While disabled, no key MUST activate the toggle and the consumer's `onKeyDown` / `onKeyUp` MUST NOT be invoked ([UBT-KEY-19](../architecture/button-behavior.md)).
  Source: `internals/use-button/useButton.ts:117-119,178-180`. Test: [L4] `ToolbarButton.test.tsx` "disabled state" (Toggle block: <kbd>Enter</kbd>/<kbd>Space</kbd> on disabled toggles leave `onPressedChange` uncalled).
  Port note 2026-10-27 (M3 track 4): runs in `toolbar.spec.ts` "disabled state" (Toggle and ToggleGroup block). Each substituted toggle's `OnPressedChange` stays uncalled (design point 14.8, settled by the orchestrator 2026-10-27, keeps it typed).

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| click (mouse, touch, pen, programmatic `click()`, keyboard-synthesized) | enabled | activation sequence: compute next, notify, group, commit | TGL-PTR-01 |
| click | disabled | default prevented; consumer `onClick` not invoked; no change | TGL-PTR-02 |
| click | enabled, consumer `onClick` present | consumer handler runs first; `preventBaseUIHandler()` suppresses the toggle | TGL-PTR-03 |
| `pointerdown` / `mousedown` | disabled | `pointerdown` default-prevented, consumer handlers not invoked; `mousedown` consumer handler not invoked | TGL-PTR-04 |
| hover / move / enter / leave | any | passed through unchanged, even while disabled | TGL-PTR-04 |

- **TGL-PTR-01** — MUST. On an enabled `click` the toggle MUST, synchronously and in this order: (1) compute `next = !pressed`; (2) create change-event details with reason `'none'` and the native click event; (3) invoke `onPressedChange(next, details)`; (4) if `details.isCanceled`, stop; (5) if inside a ToggleGroup, ask the group to apply `(identity, next, details)` ([TGG-EVT-01](toggle-group.md)); (6) if `details.isCanceled`, stop; (7) set the component-owned pressed state to `next` (no-op when consumer-owned or grouped).
  Source: `toggle/Toggle.tsx:82-103`. Test: [L2] `Toggle.test.tsx` "uncontrolled", "is called when the pressed state changes", "canceling in a grouped Toggle prevents the group value from changing"; `ToggleGroup.test.tsx` "pressed state" (uncontrolled). See [EVD-ALG-03](../architecture/event-details-and-reasons.md).
- **TGL-PTR-02** — MUST. While disabled, a `click` (including programmatic `element.click()`) MUST be default-prevented, MUST NOT invoke `onPressedChange` or the consumer's `onClick`, and MUST NOT change the pressed state ([UBT-PTR-01](../architecture/button-behavior.md)).
  Source: `internals/use-button/useButton.ts:104-110`. Test: [L2] `Toggle.test.tsx` "disables the component" (L4 for the default-prevention clause).
  Port note 2026-10-26: the default-prevention clause is asserted by `toggle.spec.ts` "port: a disabled toggle's click is default-prevented and runs neither the toggle nor the consumer's handlers" (a script's `click()` on a non-native toggle; a disabled native button receives no click).
- **TGL-PTR-03** — MUST. A consumer `onClick` MUST run before the toggle's own click handler, and calling `event.preventBaseUIHandler()` in it MUST prevent the activation sequence (no `onPressedChange`, no change) ([RND-EVT-02](../architecture/rendering-and-composition.md), [RND-EVT-03](../architecture/rendering-and-composition.md)). `stopPropagation()` alone MUST NOT prevent it. Server: relaxed for the keyboard path, see §9.
  Source: `toggle/Toggle.tsx:79-107` (`[ownProps, elementProps, getButtonProps]`); `merge-props/mergeProps.ts` per RND. Test: [L2] `ToolbarButton.test.tsx` "handles interactions" (Toggle block: forwarded consumer handler and toggle both run); `preventBaseUIHandler` case: none (L4 for the `preventBaseUIHandler()` veto, relaxed on Server).
  Port note 2026-10-26 (design point 12.2 of M3 tracks 2-3, option (a), settled by the orchestrator 2026-10-26; ADR 02 Amendments 2026-10-21, `ForwardsConsumerHandler`: without the hidden-input bridge the button mechanism declines no `onclick`; ADR 01's row "C#-origin consumer vetoes"): the consumer's C# `onclick` is not declined, so it runs before the toggle's own handler in the same Blazor dispatch and its `PreventPartHandler()` keeps the toggle unpressed on both modes, for a pointer click and for a keyboard activation's click alike (`ToggleTests` "port: a consumer onclick runs before the toggle's, and its PreventPartHandler() keeps the toggle unpressed"; `toggle.spec.ts` "port: a C# onclick that calls PreventPartHandler() keeps the toggle unpressed, on both modes"). What Server relaxes is a C# `onkeydown`'s veto of keyboard activation, the `activate` decision point (`toggle.spec.ts` "port: a C# onkeydown that calls PreventPartHandler() vetoes keyboard activation on WebAssembly and is too late on Server"). The `ToolbarButton.test.tsx` clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` clause runs in `toolbar.spec.ts` "handles interactions": the substituted toggle's typed `OnPressedChange` reaches it (design point 14.8, settled by the orchestrator 2026-10-27; ADR 02 Amendments 2026-10-27).
- **TGL-PTR-04** — MUST. Toggle MUST attach no pointer handler of its own besides `click`; while disabled, `pointerdown` is default-prevented and consumer `onPointerDown`/`onMouseDown` are not invoked ([UBT-PTR-02](../architecture/button-behavior.md), [UBT-PTR-03](../architecture/button-behavior.md)), while all other handlers (`onMouseMove`, `onMouseEnter`, …) keep firing ([UBT-PTR-04](../architecture/button-behavior.md)).
  Source: `toggle/Toggle.tsx:80-104`; `internals/use-button/useButton.ts:111-115,218-224,228`. Test: none in this slice (`ToolbarButton.test.tsx` "allows hover handlers while blocking activation" covers the mechanism through Toolbar.Button).
  Port note 2026-10-26: the pointerdown clause is asserted on a disabled non-native toggle by `toggle.spec.ts` "port: a disabled toggle's click is default-prevented and runs neither the toggle nor the consumer's handlers"; the hover clause (`ToolbarButton.test.tsx` "allows hover handlers while blocking activation") is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the hover clause runs on a `ToolbarButton` in `toolbar.spec.ts` "allows hover handlers while blocking activation".

### 6.3 Focus management

- **TGL-FOC-01** — MUST. A standalone enabled Toggle MUST be a sequential tab stop (`tabindex="0"`); a standalone disabled native Toggle MUST NOT be focusable (`disabled` attribute, [UBT-FOC-01](../architecture/button-behavior.md)); a standalone disabled non-native Toggle MUST be out of the tab order but programmatically focusable (`tabindex="-1"`, [UBT-FOC-02](../architecture/button-behavior.md)).
  Source: `utils/useFocusableWhenDisabled.ts:30-36,45-47`. Test: none directly (`Toggle.test.tsx` "disables the component" asserts the `disabled` attribute).
  Port note 2026-10-26: asserted at L2 by `ToggleTests` "port: a standalone toggle is a tab stop; disabled, a native one is disabled and a non-native one has tabindex -1" (the attributes C# renders).
- **TGL-FOC-02** — MUST. Inside a ToggleGroup, the Toggle MUST take part in the group's roving tabindex ([CMP-FOC-01](../architecture/composite-and-list-navigation.md)): receiving focus (from any source) makes it the group's tab stop ([CMP-PTR-02](../architecture/composite-and-list-navigation.md)); it has no tab stop of its own.
  Source: `internals/composite/item/useCompositeItem.ts:25-28`. Test: [L4] `ToggleGroup.test.tsx` "preserves selection and roving focus when %s" (click then <kbd>ArrowLeft</kbd> moves from the clicked toggle).
- **TGL-FOC-03** — MUST. A disabled native Toggle inside a ToggleGroup MUST be unfocusable (consequence of TGL-ARIA-02) and MUST be skipped by the group's arrow-key navigation ([TGG-KEY-08](toggle-group.md), [TLB-FOC-04](toolbar.md)); a disabled non-native Toggle inside a ToggleGroup keeps `tabindex` and is skipped by navigation because of its `aria-disabled="true"` ([CMP-ARIA-03](../architecture/composite-and-list-navigation.md)).
  Source: `utils/useFocusableWhenDisabled.ts:38-47`; `floating-ui-react/utils/composite.ts:520-553`; `toggle/Toggle.tsx:118-121` (toolbar metadata `focusableWhenDisabled: false`). Test: [L4] `ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children", "skips a direct Toggle that becomes disabled at runtime".
  Port note 2026-10-27 (M3 track 4): both titles run in `toolbar.spec.ts`, over the disabled indices the toolbar's root derives from the toggles' flags (M3 track 1's E3).
- **TGL-FOC-04** — MUST. The toggle MUST never move focus itself (no focus on press, no blur on disable) ([UBT-FOC-06](../architecture/button-behavior.md)); a mouse-move over a grouped toggle MUST NOT focus it (the ToggleGroup and Toolbar composite roots do not enable hover highlighting).
  Source: `internals/composite/item/useCompositeItem.ts:29-39`; `internals/composite/root/CompositeRoot.tsx:38`. Test: none.
  Port note 2026-10-26: the hover clause is asserted by `toggle-group.spec.ts` "port: moving the pointer over a toggle neither focuses it nor moves the tab stop".

### 6.4 Dismissal

Not applicable — Toggle has no popup and no dismissal behavior.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onPressedChange` | `(pressed: boolean, details: Toggle.ChangeEventDetails) => void` | On every enabled activation (pointer click, keyboard activation, programmatic `click()`), before any state change | `'none'` | yes — `details.cancel()` |

- **TGL-EVT-01** — MUST. `onPressedChange` MUST be invoked synchronously inside the handling of the click event, with `pressed` = `!currentPressed`, before the toggle's state (and, when grouped, the group's value) changes ([EVD-EVT-02](../architecture/event-details-and-reasons.md)).
  Source: `toggle/Toggle.tsx:83-88`. Test: [L2] `Toggle.test.tsx` "is called when the pressed state changes" (`calls[0][0] === true`).
- **TGL-EVT-02** — MUST. `details.reason` MUST always be `'none'`.
  Source: `toggle/Toggle.tsx:84,186`; `internals/reason-parts.ts:1`. Test: none (type-level: `Toggle.ChangeEventReason`).
  Port note 2026-10-26: asserted by `ToggleTests` "port: the details carry the reason none and the click, no trigger, and AllowPropagation changes nothing".
- **TGL-EVT-03** — MUST. `details.event` MUST be the native click event (a `MouseEvent`/`PointerEvent`; for keyboard activation the synthetic click carrying the key press's modifier flags), and `details.trigger` MUST be `undefined` ([EVD-API-05](../architecture/event-details-and-reasons.md), [EVD-API-07](../architecture/event-details-and-reasons.md)).
  Source: `toggle/Toggle.tsx:84`; `internals/createBaseUIEventDetails.ts:130-140,153`. Test: none.
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-21: `EventSnapshot.FromEventArgs`): `details.Event` is the snapshot of the click Blazor dispatched to the part's own `onclick`, with its modifier keys; `Trigger` is `null` (`ToggleTests` "port: the details carry the reason none and the click, …"; `toggle.spec.ts` reads `type=click detail=0` for a keyboard activation).
- **TGL-EVT-04** — MUST. Calling `details.cancel()` inside `onPressedChange` MUST leave the pressed state, `aria-pressed` and data attributes unchanged (component-owned mode), and MUST prevent the group from being notified when grouped ([EVD-EVT-05](../architecture/event-details-and-reasons.md), [EVD-EVT-09](../architecture/event-details-and-reasons.md)).
  Source: `toggle/Toggle.tsx:90-96`. Test: [L2] `Toggle.test.tsx` "does not change the pressed state when the event is canceled", "canceling in a grouped Toggle prevents the group value from changing" (`onValueChange` not called). Docs: `CHANGELOG.md v1.6.0 "Fix grouped cancelation and JSDoc (#4940)"`.
- **TGL-EVT-05** — MUST. When grouped, the **same** details object MUST be passed on to the ToggleGroup's `onValueChange` (child-then-parent order, [EVD-EVT-04](../architecture/event-details-and-reasons.md)); a `cancel()` issued by the group's callback MUST likewise leave the toggle unchanged.
  Source: `toggle/Toggle.tsx:94-100`; `toggle-group/ToggleGroup.tsx:55-81`. Test: [L2] `ToggleGroup.test.tsx` "does not change the value when the event is canceled" (`aria-pressed` stays `"false"`).
- **TGL-EVT-06** — MUST. `onPressedChange` MUST fire exactly once per activation, whether the activation came from a pointer, from <kbd>Enter</kbd>/<kbd>Space</kbd>, or from `element.click()` ([EVD-EVT-03](../architecture/event-details-and-reasons.md)).
  Source: `toggle/Toggle.tsx:82-103`; `internals/use-button/useButton.ts:138-152,182-196` (one click per Space press in composites). Test: [L2] `Toggle.test.tsx` "is called when the pressed state changes" (`length === 1`); `ToolbarButton.test.tsx` "handles interactions" (Toggle block: counts 1, 2, 3 across Enter/Space activations) (L4 for the keyboard activation clause).
  Port note 2026-10-26: the keyboard clause is asserted at L4 by `toggle.spec.ts` "port: Enter and Space activate a standalone toggle once per press, native and non-native" and, grouped, by `toggle-group.spec.ts` "fires when the $key is pressed". The `ToolbarButton.test.tsx` counts are open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` counts run in `toolbar.spec.ts` "handles interactions", one `OnPressedChange` per activation (design point 14.8, settled by the orchestrator 2026-10-27, keeps a substitute's typed callback typed).
- **TGL-EVT-07** — MUST. `onPressedChange` MUST NOT fire on mount, when the `pressed` prop changes, or when the enclosing group's value changes for any reason other than this toggle's own activation ([EVD-STA-06](../architecture/event-details-and-reasons.md)).
  Source: `toggle/Toggle.tsx:82-103` (only call site is the click handler). Test: [L2] `Toggle.test.tsx` "controlled" (external changes; no callback supplied) — asserts attribute only; direct assertion: none.
- **TGL-EVT-08** — MUST. `onPressedChange` MUST NOT fire while the toggle is disabled (the click is gated before the handler runs).
  Source: `internals/use-button/useButton.ts:104-110`. Test: [L2] `Toggle.test.tsx` "disables the component" (`calls.length === 0`).
- **TGL-EVT-09** — MUST. `details.allowPropagation()` MUST have no effect on Toggle (the click's propagation is never stopped by the component).
  Source: `toggle/Toggle.tsx:82-103` (no `stopPropagation`). Test: none.
  Port note 2026-10-26: asserted by `ToggleTests` "port: the details carry the reason none and the click, no trigger, and AllowPropagation changes nothing".

## 8. Form integration

Toggle does not participate in forms.

- **TGL-FRM-01** — MUST. Toggle MUST NOT render a hidden input, MUST NOT accept `name`/`value`-as-submission semantics (its `value` prop is an identity only, TGL-API-05, and is never written to the DOM), and MUST strip `form` and `type` so that, in native mode, the element is always `type="button"` and never submits or resets an enclosing form ([UBT-FRM-01](../architecture/button-behavior.md)).
  Source: `toggle/Toggle.tsx:30,34,42` (`value` not in any props object). Test: none. Docs: `components/page.mdx` §Toggle (outline lists no `name`/`form`).
  Port note 2026-10-26: asserted by `ToggleTests` "port: a toggle renders no hidden input, drops the consumer's form and type, and never renders its Value".

No Field integration exists: Toggle does not read `Field.Root` state and does not register as a field control.

## 9. Lifecycle, mounting and animation

Not applicable — the element is always mounted; there is no transition status, `keepMounted`, or completion callback. CSS transitions on `[data-pressed]` are the consumer's concern.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- TGL-PTR-03 — the row "C#-origin consumer vetoes" (UBT-KEY-03/04): a C# consumer's `PreventPartHandler()` in `onkeydown` vetoes keyboard activation synchronously on WebAssembly through the `activate` decision point; on Server the toggle is activated regardless (fallback "run") and the handler runs a round trip later; a consumer JavaScript listener's `preventDefault()` is honoured on both modes (ADR 04 Amendments 2026-10-18). Narrowed 2026-10-26 (design point 12.2 of M3 tracks 2-3, option (a), settled by the orchestrator 2026-10-26; ADR 02 Amendments 2026-10-21, `ForwardsConsumerHandler`; conventions §3.5, ADR 13 §3): a C# `onclick`'s veto is met on both modes, because the port does not decline the consumer's `onclick` and runs it before the toggle's own handler in the same Blazor dispatch (`toggle.spec.ts` "port: a C# onclick that calls PreventPartHandler() keeps the toggle unpressed, on both modes"; "port: a C# onkeydown that calls PreventPartHandler() vetoes keyboard activation on WebAssembly and is too late on Server").
- TGL-DOM-05 (grouped clause), TGL-EDGE-08 — the row "EVD-STA-07, CMP-STA-10, FFI registration" (added 2026-10-26, ADR 04 Amendments 2026-10-22; the RDG-FOC-01/03, RDG-EDGE-06 precedent): the first toggle's `tabindex="0"` arrives with the composite's first publication, after paint plus a round trip on Server; until then every grouped toggle shows C#'s `-1` (`toggle-group.spec.ts` "port: grouped toggles render tabindex -1 until the first publication, a round trip later on Server"). Later moves of the tab stop are the module's, in the task of the key or the focus, on both modes.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (standalone) | [RND](../architecture/rendering-and-composition.md) | default tag `button`; `enabled: !groupContext`; props merged in order: `{ 'aria-pressed', onClick }` → consumer props → button props resolver; DOM handles: button handle, consumer handle; default state → data-attribute projection (`pressed` → `data-pressed`, `disabled` → `data-disabled`), no custom mapping |
| `CompositeItem` (grouped) | [CMP](../architecture/composite-and-list-navigation.md) | `tag="button"`; `metadata: { disabled, focusableWhenDisabled: false }` (read by Toolbar.Root to build `disabledIndices`, [TLB-STA-02](toolbar.md)); composite handle attached first, then button handle, then consumer handle; composite props (`tabIndex`, `onFocus`, `onMouseMove`) merged before the toggle's own props; same state object |
| `useButton` | [UBT](../architecture/button-behavior.md) | `{ disabled: effectiveDisabled, native: nativeButton }`; `focusableWhenDisabled` unset; `tabIndex` default `0`; `composite` inferred from the nearest composite root (ToggleGroup, Toolbar) |
| `useFocusableWhenDisabled` (via `useButton`) | UBT | matrix TGL-ARIA-02 |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | reason `'none'`, event = native click, no trigger, no custom fields; one object shared with the group |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `{ controlled: group ? (value in group.value) : pressed, default: defaultPressed, name: 'Toggle', state: 'pressed' }` |
| `useBaseUiId` | RND | identity fallback `useBaseUiId(value || undefined)` |
| `useToggleGroupContext` | [TGG](toggle-group.md) | reads `{ value, setGroupValue, disabled, isValueInitialized }`; absent → standalone |
| `error` (dev logger) | UTL | TGL-EDGE-01 |

## 12. Edge cases, invariants and dev warnings

All messages are emitted only in non-production builds, through `console.error`, prefixed `Base UI: `, and each distinct message is logged at most once per page lifetime ([UTL-EDGE-11](../architecture/shared-utilities.md), [UTL-EDGE-12](../architecture/shared-utilities.md)).

- **TGL-EDGE-01** — MUST. When a Toggle is rendered inside a ToggleGroup whose value has been initialized (`value` or `defaultValue` supplied, [TGG-STA-07](toggle-group.md)) and the toggle's `value` prop is `undefined`, exactly this error MUST be logged (once, even when several toggles qualify): `Base UI: A `<Toggle>` component rendered in a `<ToggleGroup>` has no explicit `value` prop. This will cause issues between the Toggle Group and Toggle values. Provide the `<Toggle>` with a `value` prop matching the `<ToggleGroup>` values prop type.` The check runs after paint, on mount and whenever the group, the `value` prop, or the group's initialized flag changes. A `value` of `""` does not trigger it (only `undefined` does).
  Source: `toggle/Toggle.tsx:48-59`; `toggle-group/ToggleGroupContext.ts:14-18`. Test: [L2] `ToggleGroup.test.tsx` "should warn if Toggle value is not set and ToggleGroup value is defined" (`toHaveBeenCalledExactlyOnceWith`).
  Port note 2026-10-26 (chosen by the user 2026-10-26, M3 plan §Settled; the PRG-EDGE-01 precedent for the prefix): the message is the port's, written once per session (a scoped `ToggleDevelopmentLog`, the `ButtonDevelopmentLog` precedent) to the toggle's logger while the development warnings switch is on: ``Blazix: A `<Toggle>` component rendered in a `<ToggleGroup>` has no explicit `Value` parameter. This will cause issues between the Toggle Group and Toggle values. Provide the `<Toggle>` with a `Value` parameter matching the `<ToggleGroup>` values parameter type.`` The check runs after the toggle's render (ADR 01 decision 4); a toggle whose group's initialized flag turns on renders once more so that the check runs (`ToggleGroupTests` "port: the missing-value message is written once a later parameter set initializes the value", "port: no missing-value message without an initialized value, or for an empty Value").
- **TGL-EDGE-02** — SHOULD. Standalone, if `pressed` crosses between `undefined` and a boolean after the first render, the shared controlled/uncontrolled diagnostic [UTL-EDGE-22](../architecture/shared-utilities.md) SHOULD be logged with `name: 'Toggle'`, `state: 'pressed'`.
  Source: `toggle/Toggle.tsx:64-65`; `packages/utils/src/useControlled.ts:48-64`. Test: none.
  Port note 2026-10-26: asserted by `ToggleTests` "port: ownership is fixed at the first parameter set, and a removed Pressed falls back to the initial default" (the message is `Controlled<T>`'s, written to the toggle's logger).
- **TGL-EDGE-03** — SHOULD. Standalone in component-owned mode, if `defaultPressed` changes after the first render, [UTL-EDGE-23](../architecture/shared-utilities.md) SHOULD be logged with `name: 'Toggle'`, `state: 'pressed'`; the rendered state is unaffected.
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: none.
  Port note 2026-10-26: asserted by `ToggleTests` "port: a DefaultPressed changed after the first parameter set is logged and ignored".
- **TGL-EDGE-04** — SHOULD. If `nativeButton` is `true` but the rendered element is not a `<button>`, or `false` but it is, the shared diagnostics [UBT-EDGE-01](../architecture/button-behavior.md) / [UBT-EDGE-02](../architecture/button-behavior.md) SHOULD be logged.
  Source: `internals/use-button/useButton.ts:36-66`. Test: none for Toggle.
  Port note 2026-10-26: the `button` module's declared-mode check reports through the twin, and `ButtonDevelopmentLog` writes each message once per session (`ToggleTests` "port: a declared-mode mismatch of toggles is written once per session").
- **TGL-EDGE-05** — MUST. Toggles inside a ToggleGroup that omit `value` or pass `""` MUST each behave as an independent member of the group: pressing one presses only that one, and the group's value array then contains that toggle's generated identity.
  Source: `toggle/Toggle.tsx:41-42,94-96`. Test: [L2] `ToggleGroup.test.tsx` "when Toggles omit value" (single and multiple).
- **TGL-EDGE-06** — MUST. Inside a ToggleGroup, a Toggle whose `pressed` or `defaultPressed` prop is supplied MUST ignore it (TGL-STA-04); no diagnostic is emitted for this.
  Source: `toggle/Toggle.tsx:62-63`. Test: none.
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: a grouped toggle ignores Pressed and DefaultPressed and logs nothing"; a grouped toggle does not feed `Controlled<bool>`, so neither of its diagnostics can fire.
- **TGL-EDGE-07** — MUST. No behavior of Toggle depends on writing direction or locale; direction affects only the enclosing group's arrow-key mapping.
  Source: `toggle/Toggle.tsx:1-14` (no direction import). Test: none.
- **TGL-EDGE-08** — MUST. Server-rendered markup MUST equal the client's initial render: `aria-pressed`, `type`/`role`, data attributes and the `disabled`/`aria-disabled` resolution are computed synchronously ([UBT-DOM-11](../architecture/button-behavior.md)). Inside a ToggleGroup the server markup carries `tabindex="-1"` on every toggle (item index unresolved, [CMP-STA-15](../architecture/composite-and-list-navigation.md)) and the first toggle becomes `tabindex="0"` after the first item-map publication. Server: relaxed, see §9.
  Source: `internals/composite/item/useCompositeItem.ts:19,25`; `internals/composite/list/useCompositeListItem.ts:43-55`. Test: none.
  Port note 2026-10-26: the grouped `-1` is asserted at L2 by `ToggleGroupTests` "port: grouped toggles render tabindex -1, which the composite owns from attach, and the group renders none", and the first publication per mode by `toggle-group.spec.ts` "port: grouped toggles render tabindex -1 until the first publication, a round trip later on Server" (the CMP-STA-10 row, §9).
- **TGL-EDGE-09** — MUST. A Toggle placed inside `Toolbar.Root` **without** a ToggleGroup and **without** `Toolbar.Button` MUST behave as a standalone toggle that is in composite button mode (Space on keydown, no `tabindex` emitted, `aria-disabled` per the composite rows of TGL-ARIA-02) but is **not** a toolbar item: it does not register with the toolbar, arrow keys do not reach it, and a native `<button>` stays sequentially focusable by its own default. (Not a supported layout; see §16.)
  Source: `toggle/Toggle.tsx:110,123-138`; `internals/use-button/useButton.ts:25-26`. Test: none.
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-26, chosen by the user; toggle §16 Q1): implemented as stated; Toggle reads the optional composite root cascade for its button mode. Asserted on the test-only composite root by `ToggleTests` "port: under a composite root without a group a toggle is in composite button mode and is not an item" and `toggle.spec.ts` "port: a bare toggle under a composite root activates Space on keydown and is skipped by the root's arrows".
  Port note 2026-10-27 (M3 track 4): asserted under a `ToolbarRoot` too, by `ToggleToolbarTests` "port: a bare toggle under ToolbarRoot registers nothing and is in composite button mode".

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND.

## 13. Behaviors evidenced by tests

### `Toggle.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`; includes the "with a non-native button" re-run) | TGL-DOM-07, TGL-API-06, TGL-API-07 |
| "controlled" | TGL-API-01, TGL-STA-05, TGL-ARIA-01 |
| "uncontrolled" | TGL-API-02, TGL-STA-03, TGL-PTR-01, TGL-ARIA-01 |
| "is called when the pressed state changes" | TGL-API-03, TGL-EVT-01, TGL-EVT-06 |
| "does not change the pressed state when the event is canceled" | TGL-EVT-04 |
| "canceling in a grouped Toggle prevents the group value from changing" | TGL-EVT-04, TGL-PTR-01, [TGG-EVT-04](toggle-group.md) |
| "disables the component" | TGL-API-04, TGL-DOM-03, TGL-ARIA-02, TGL-PTR-02, TGL-EVT-08 |
| "should pass composite props" | TGL-STR-02, TGL-DOM-05, TGL-STA-01 |

### Tests in other files that exercise Toggle

| Test | Requirements |
|---|---|
| `ToggleGroup.test.tsx` "pressed state" (uncontrolled), "prop: defaultValue", "prop: value", "pressed state" (controlled) | TGL-STA-04, TGL-DOM-02, TGL-ARIA-01 |
| `ToggleGroup.test.tsx` "when Toggles omit value" (both) | TGL-API-05, TGL-EDGE-05 |
| `ToggleGroup.test.tsx` "should warn if Toggle value is not set and ToggleGroup value is defined" | TGL-EDGE-01 |
| `ToggleGroup.test.tsx` "can disable the whole group", "can disable individual items" | TGL-API-04, TGL-DOM-03, TGL-ARIA-02 |
| `ToggleGroup.test.tsx` "key: $key toggles the pressed state", "fires when the $key is pressed" | TGL-KEY-02 |
| `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" | TGL-KEY-03, TGL-DOM-05 |
| `ToggleGroup.test.tsx` "preserves selection and roving focus when %s" | TGL-FOC-02 |
| `ToggleGroup.test.tsx` "does not change the value when the event is canceled" | TGL-EVT-05 |
| `ToolbarButton.test.tsx` "renders toggle and toggle group", "handles interactions", "disabled state" (Toggle and ToggleGroup block) | TGL-ARIA-01, TGL-KEY-01, TGL-KEY-02, TGL-KEY-05, TGL-EVT-06, TGL-API-08, TGL-PTR-03 |
| `ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children", "skips a direct Toggle that becomes disabled at runtime", "disables direct ToggleGroup children when Toolbar.Group is disabled" | TGL-ARIA-02, TGL-FOC-03 |

### Additional requirements introduced here

- **TGL-DOM-07** — MUST. Toggle MUST pass the shared conformance suite from RND ([RND-DOM-36](../architecture/rendering-and-composition.md) … [RND-DOM-49](../architecture/rendering-and-composition.md)): custom attributes forwarded to the default `<button>` and to a substituted element (function and element forms), `style` merging, DOM handle resolving to an `HTMLButtonElement`, wrapped substitutes, `className` string/function merging; and, because `button: true` with `testRenderPropWith: 'button'`, the prop-forwarding and substitution tests pass again with a `div` substitute and `nativeButton={false}` ([RND-DOM-49](../architecture/rendering-and-composition.md)).
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] `Toggle.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Two render paths, one component.** Standalone, Toggle renders through the generic element pipeline with `enabled: !groupContext`; grouped, that pipeline is still executed (to keep React hook order stable) but returns nothing ([RND-DOM-25](../architecture/rendering-and-composition.md)), and a `CompositeItem` renders the element instead with the same `state`, `refs` and `props` list. A port needs only one path with an "is a composite item" switch that (a) registers the element with the nearest item registry, (b) prepends the roving `tabindex`/`focus`/`mousemove` attributes, and (c) publishes `{ disabled, focusableWhenDisabled: false }` metadata. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 4 (`Enabled = false` renders nothing) and decisions/04-js-interop-boundary.md §1 (`composite` module; C# receives active-index changes).
- **Grouped mode is "always controlled".** `useControlled` is called with `controlled: value in group.value` (a boolean, never `undefined`) when grouped, so the component-owned branch is dead there; `setPressedState` is a no-op. The second `isCanceled` check after the group call therefore only matters for standalone toggles whose group is absent — it is defensive. A port can read `pressed` straight from the group. Port: see decisions/05-state-store-and-event-details.md §1 (`Controlled<T>`) and §2 (controlled iff supplied at the first parameter set).
- **`value || undefined` and `useId`.** With React ≥ 18 the generated identity is `base-ui-` + `React.useId()` (e.g. `base-ui-«r1»`) and is available on the first render; with React 17 it is `undefined` for the first render and assigned after mount (`useId.ts:9-31`), which is why `if (value)` guards the group call. Ports should generate the identity eagerly and treat the guard as always true. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (scoped `base-ui-<n>` generator drawn at first render).
- **Dev-only warning timing.** TGL-EDGE-01 runs in a passive effect (after paint) keyed on `[groupContext, valueProp, groupContext?.isValueInitialized]`; the group context object identity changes whenever the group's value/disabled/callback change, so the check re-runs often but the logger deduplicates. A port may check once per mount and once per `value`/initialized change. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (timing-term mapping: after paint).
- **Handler composition.** The props list `[{ 'aria-pressed', onClick }, elementProps, getButtonProps]` means: consumer `onClick` runs before the toggle's `onClick` (both inside the mechanism's disabled gate), `preventBaseUIHandler()` from the consumer vetoes the toggle, and `type`/`role`/`tabindex`/`disabled`/`aria-disabled` from the consumer override the mechanism ([UBT-EVT-03](../architecture/button-behavior.md), [UBT-DOM-09](../architecture/button-behavior.md)). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 3 (handler chaining and `PreventBaseUIHandler()` veto) and decisions/04-js-interop-boundary.md §1 (`button` module) and §3 (`decide()` "run" fallback, EVD-EVT-26).
- **`aria-pressed={pressed}`** is a boolean prop that React stringifies (`"true"`/`"false"`); likewise `aria-disabled={false}` renders as `"false"` (UBT §8). A port's attribute layer must not drop boolean `false` for `aria-*`.
- **Composite mode inference.** `useButton` reads the composite root context *optionally*; the context exists for any descendant of `Toolbar.Root` or a standalone `ToggleGroup`. This is what makes TGL-EDGE-09 possible (composite key semantics without item registration). Port: see decisions/04-js-interop-boundary.md §1 (`button` module: keydown-to-click synthesis, focusable-when-disabled).
- **Metadata stability.** `itemMetadata` is memoized on `disabled` so that the item re-registers (and Toolbar.Root recomputes `disabledIndices`) only when `disabled` changes — a port must republish metadata on `disabled` change for [TLB-STA-02](toolbar.md) to react ("skips a direct Toggle that becomes disabled at runtime"). Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry) and decisions/01-hosting-model-and-timing.md §Relaxation table (CMP-STA-10: item-map publication after paint plus a round trip on Server).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TGL-STR-01 | MUST | Single `<button>` element, no extra DOM | 2 |
| TGL-STR-02 | MUST | Registers as composite item iff inside a ToggleGroup | 2 |
| TGL-API-01 | MUST | `pressed` defined on first render → consumer-owned (standalone) | 3.1 |
| TGL-API-02 | MUST | initial state `defaultPressed` (default `false`) | 3.1 |
| TGL-API-03 | MUST | `onPressedChange(next, details)` signature | 3.1 |
| TGL-API-04 | MUST | effective disabled = prop OR group; default `false` | 3.1 |
| TGL-API-05 | MUST | identity = truthy `value` else generated `base-ui-` id | 3.1 |
| TGL-API-06 | MUST | `nativeButton` default `true`; selects UBT mode | 3.1 |
| TGL-API-07 | MUST | `className`/`style`/`render` per RND with state object | 3.1 |
| TGL-API-08 | MUST | `form`/`type` discarded; other props forwarded, consumer wins | 3.1 |
| TGL-STA-01 | MUST | state object has exactly `pressed`, `disabled` | 3.1 |
| TGL-STA-02 | MUST | standalone ownership fixed on first render; fallback to default | 4 |
| TGL-STA-03 | MUST | component-owned activation sets `!pressed` | 4 |
| TGL-STA-04 | MUST | grouped: pressed iff identity in group value; props ignored | 4 |
| TGL-STA-05 | MUST | controlled prop change updates DOM without callback | 4 |
| TGL-DOM-01 | MUST | default element `<button>`; substitute gets everything | 5.1 |
| TGL-DOM-02 | MUST | `data-pressed` iff pressed | 5.1 |
| TGL-DOM-03 | MUST | `data-disabled` iff disabled | 5.1 |
| TGL-DOM-04 | MUST | `type="button"` (native, not overridable) / `role="button"` (non-native) | 5.1 |
| TGL-DOM-05 | MUST | `tabindex`: 0 / -1 standalone; roving in group | 5.1 |
| TGL-DOM-06 | MUST | consumer attributes override built-ins except `type` | 5.1 |
| TGL-DOM-07 | MUST | passes RND conformance suite incl. non-native re-run | 13 |
| TGL-ARIA-01 | MUST | `aria-pressed` always `"true"`/`"false"` | 5.2 |
| TGL-ARIA-02 | MUST | `disabled`/`aria-disabled` matrix incl. `"false"` in composites | 5.2 |
| TGL-ARIA-03 | MUST | no accessible name added | 5.2 |
| TGL-KEY-01 | MUST | standalone Enter/Space activation per UBT | 6.1 |
| TGL-KEY-02 | MUST | composite: Space on keydown, one click per press | 6.1 |
| TGL-KEY-03 | MUST | arrows/Home/End not intercepted; bubble to group | 6.1 |
| TGL-KEY-04 | MUST | Tab never prevented | 6.1 |
| TGL-KEY-05 | MUST | disabled: no activation, consumer key handlers not invoked | 6.1 |
| TGL-PTR-01 | MUST | activation sequence (compute, notify, group, commit) | 6.2 |
| TGL-PTR-02 | MUST | disabled click prevented, no callback, no change | 6.2 |
| TGL-PTR-03 | MUST | consumer `onClick` first; `preventBaseUIHandler` vetoes | 6.2 |
| TGL-PTR-04 | MUST | no own pointer handlers; disabled pointerdown gating; hover passes | 6.2 |
| TGL-FOC-01 | MUST | standalone focusability by mode | 6.3 |
| TGL-FOC-02 | MUST | grouped: roving tab stop follows focus | 6.3 |
| TGL-FOC-03 | MUST | disabled grouped toggle unfocusable / skipped | 6.3 |
| TGL-FOC-04 | MUST | never moves focus; no hover focus | 6.3 |
| TGL-EVT-01 | MUST | callback synchronous, before state change, with `!pressed` | 7 |
| TGL-EVT-02 | MUST | reason always `'none'` | 7 |
| TGL-EVT-03 | MUST | event = native click; trigger undefined | 7 |
| TGL-EVT-04 | MUST | `cancel()` → no change, group not notified | 7 |
| TGL-EVT-05 | MUST | same details object to group; group cancel also blocks | 7 |
| TGL-EVT-06 | MUST | exactly once per activation | 7 |
| TGL-EVT-07 | MUST | not fired on mount / prop change / external group change | 7 |
| TGL-EVT-08 | MUST | not fired while disabled | 7 |
| TGL-EVT-09 | MUST | `allowPropagation()` no effect | 7 |
| TGL-FRM-01 | MUST | no form participation; `form`/`type` stripped | 8 |
| TGL-EDGE-01 | MUST | missing-`value`-in-group dev error (verbatim, once) | 12 |
| TGL-EDGE-02 | SHOULD | controlled/uncontrolled switch diagnostic | 12 |
| TGL-EDGE-03 | SHOULD | `defaultPressed` change diagnostic | 12 |
| TGL-EDGE-04 | SHOULD | `nativeButton` mismatch diagnostics | 12 |
| TGL-EDGE-05 | MUST | value-less toggles independent in a group | 12 |
| TGL-EDGE-06 | MUST | `pressed`/`defaultPressed` ignored in group, silently | 12 |
| TGL-EDGE-07 | MUST | no RTL/locale dependence | 12 |
| TGL-EDGE-08 | MUST | SSR markup equals client initial render; `tabindex=-1` pre-publication | 12 |
| TGL-EDGE-09 | MUST | bare Toggle in Toolbar: composite button mode, not an item | 12 |

## 16. Open questions

1. **Bare Toggle inside `Toolbar.Root`.** `toggle/Toggle.tsx:110,123` registers the toggle as a composite item only when a ToggleGroup encloses it, while `internals/use-button/useButton.ts:25-26` switches to composite key semantics for any composite root ancestor. A `<Toggle>` placed directly in `Toolbar.Root` (not via `Toolbar.Button`) therefore gets Space-on-keydown and `aria-disabled="false"` but is invisible to arrow navigation and keeps its native tab stop (TGL-EDGE-09). The docs only show `<Toolbar.Button render={<Toggle />} />`. Decide whether the port should mirror this or reject/auto-wrap the layout.
   Resolved 2026-10-26 (ADR 04 Amendments 2026-10-26, chosen by the user): mirrored; TGL-EDGE-09 holds as stated, and `Toggle` infers its button mode from the optional composite root cascade (TGL-EDGE-09's note).
2. **`aria-disabled="false"` on enabled grouped toggles.** `utils/useFocusableWhenDisabled.ts:38-43` emits the stringified boolean for native composite items; `ToggleGroup.test.tsx` "can disable individual items" asserts it. Flagged by the UBT author (UBT §10 Q4): if the port's attribute layer drops `false`, that test translation fails. Documented as MUST in TGL-ARIA-02.
   Resolved 2026-10-26: `ButtonAttributeWriter` writes `aria-disabled="false"` on an enabled native composite item (`ToggleGroupTests` "can disable individual items").
3. **"Composite items are focusable when disabled by default" (JSDoc, `utils/useFocusableWhenDisabled.ts:71-75`).** Verified false for native toggles: a disabled native Toggle in a ToggleGroup gets the `disabled` attribute (TGL-ARIA-02 row 4, asserted by `ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children" via `toBeDisabled()`). Only non-native toggles stay focusable. The matrix in TGL-ARIA-02 is the real behavior.
   Resolved 2026-10-26: `ButtonAttributeWriter` gives a disabled native composite toggle both `disabled` and `aria-disabled="true"` (`ToggleGroupTests` "can disable individual items", "can disable the whole group"; `toggle-group.spec.ts` "port: with the first toggle disabled, the first enabled toggle takes the initial tab stop").
4. **Second cancel check.** `toggle/Toggle.tsx:98-100` re-checks `details.isCanceled` after the group call, but in grouped mode the local setter is a no-op anyway (§14). Harmless; a port can keep a single check after the group call.
   Resolved 2026-10-26 (issue 101, M3 tracks 2-3): a grouped toggle commits nothing of its own, so the port keeps only the check before the group's request (TGL-EVT-04); the group's cancel stops the group's commit (TGL-EVT-05).
5. **`form` prop in the type.** `Toggle.Props` extends the `'button'` attribute type, so `form` is accepted by the type system but silently dropped (`toggle/Toggle.tsx:30`). The API outline does not list it. Confirm the port should accept-and-ignore rather than reject.
   Resolved 2026-10-26 (issue 101, M3 tracks 2-3; ADR 02 Amendments 2026-10-21): accepted and ignored; `FinishAttributes` removes the consumer's `form` and `type` (TGL-API-08's note).
6. **Pre-render identity on React 17.** `if (value)` (`toggle/Toggle.tsx:94`) can be false during the first render on React 17 (no `useId`), so an activation in that window would skip the group. Not reachable in practice (no click before mount); ignored by this spec. Resolved: the port draws the identity at first render (decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5), so the guard is always true.
