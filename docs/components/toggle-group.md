# Toggle Group

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/toggle-group/` |
| Tests | `packages/react/src/toggle-group/**/*.test.tsx` (1 file; plus `ToggleGroup.spec.tsx`, type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/toggle-group/page.mdx` |
| Prefix | `TGG` |
| Status | implemented |
| Revised | 2026-10-27 |
| Builds on | [toggle](toggle.md) (the group's items) |
| Derived by | none; hosted by [toolbar](toolbar.md) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [button-behavior](../architecture/button-behavior.md) (through Toggle), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

ToggleGroup gives a set of [Toggle](toggle.md) buttons one shared value: an array of the identities (`value` props) of the toggles that are currently pressed. By default the group is **single-select** — pressing a toggle makes it the only pressed one, and pressing the pressed toggle again empties the selection (nothing forces one toggle to stay pressed; the experiments page shows the consumer enforcing that in `onValueChange`). With `multiple` the group is **multi-select** and each activation adds or removes one identity. The value may be owned by the consumer (`value`) or by the component (`defaultValue`).

The group renders a `<div role="group">` with no accessible name of its own and no `aria-orientation` (that attribute is invalid on `group`; `data-orientation` is provided for styling instead). **Standalone**, the group is a roving-tabindex composite: exactly one toggle is in the tab order, arrow keys matching `orientation` (swapped under RTL) move focus between toggles, <kbd>Home</kbd>/<kbd>End</kbd> jump to the ends, and focus wraps unless `loopFocus` is `false`. **Inside `Toolbar.Root`**, the group renders the same element but is *not* a composite root: its toggles register with the toolbar's roving group so the whole toolbar stays a single tab stop, and the group's `orientation`/`loopFocus` have no keyboard effect. The group's `disabled` combines with the enclosing `Toolbar.Root` / `Toolbar.Group` disabled state and is pushed down to every toggle.

Every value change originates in a toggle: the toggle's `onPressedChange` fires first, then the group's `onValueChange` with the same details object, and either may cancel.

## 2. Anatomy

```
<ToggleGroup>                 required, 1          default element: div  (role="group")
  <Toggle value="…">          optional, 0..n       default element: button  (see toggle.md; anywhere in the subtree)
```

- **TGG-STR-01** — MUST. ToggleGroup MUST render exactly one element (default `<div>`); Toggles are not required to be direct children — any Toggle in the subtree joins the group, and nothing else is treated as a member.
  Source: `toggle-group/ToggleGroup.tsx:99-124`; `toggle/Toggle.tsx:43`. Test: [L2] `ToggleGroup.test.tsx` conformance suite "Base UI component API"; "renders a `group`" (empty group).
- **TGG-STR-02** — MUST. When no `Toolbar.Root` encloses it, the group element MUST be a composite root ([CMP-STR-01](../architecture/composite-and-list-navigation.md)) that owns the roving tab stop of its toggles; when a `Toolbar.Root` encloses it (at any depth, including through `Toolbar.Group`), the group MUST render the same element **without** composite-root behavior, and its toggles MUST register with the toolbar's composite root instead.
  Source: `toggle-group/ToggleGroup.tsx:38,99-104,106-123`. Test: [L2] `ToggleGroup.test.tsx` "preserves selection and roving focus when %s" (standalone vs "nested in Toolbar.Group"); `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children" (L4 for the roving-navigation clause).
  Port note 2026-10-26: standalone, the group composes the composite root (`ToggleGroupTests` "port: the group's composite root sends its orientation, loopFocus and Home and End, and no disabled indices"); the navigation clause runs in `toggle-group.spec.ts` ("preserves selection and roving focus when standalone"). The in-toolbar path and the `ToolbarButton.test.tsx` and "nested in Toolbar.Group" clauses are open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): inside a `ToolbarRoot`, read at the first parameter set, the group creates no `CompositeRootBehavior` and cascades no registry, so its toggles are the toolbar's items (`ToggleGroupToolbarTests` "port: inside a toolbar the group is no composite root, and its toggles register with the toolbar's"); the "nested in Toolbar.Group" variant runs in `toggle-group.spec.ts`, and the `ToolbarButton.test.tsx` title in `toolbar.spec.ts`.
- **TGG-STR-03** — MUST. A group with zero toggles MUST render normally (role, data attributes) and MUST have no tab stop.
  Source: `toggle-group/ToggleGroup.tsx:99-124`; `internals/composite/root/useCompositeRoot.ts:111-113`. Test: [L2] `ToggleGroup.test.tsx` "renders a `group`".

Nested ToggleGroups are not a supported configuration (an inner group would re-provide the shared state and, standalone, create a nested composite root); see §16.

## 3. Public API

### 3.1 `ToggleGroup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `readonly Value[]` | — (uncontrolled) | Identities of the pressed toggles. Consumer-owned when defined on first render. | TGG-API-01 |
| `defaultValue` | `readonly Value[]` | `[]` | Initial pressed identities when component-owned. | TGG-API-02 |
| `onValueChange` | `(groupValue: Value[], eventDetails: ToggleGroup.ChangeEventDetails) => void` | — | Called when a toggle activation would change the value, before the change is applied. | TGG-API-03 |
| `disabled` | `boolean` | `false` | Disables every toggle in the group. OR-combined with enclosing `Toolbar.Root` / `Toolbar.Group` disabled. | TGG-API-04 |
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Axis of arrow-key navigation (standalone only) and value of `data-orientation`. | TGG-API-05 |
| `loopFocus` | `boolean` | `true` | Whether arrow-key navigation wraps at the ends (standalone only). | TGG-API-06 |
| `multiple` | `boolean` | `false` | `false`: at most one toggle pressed; `true`: any number. | TGG-API-07 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the state object below. | TGG-API-08 |
| `aria-label`, `aria-labelledby` | `string` | — | Forwarded; the only source of the group's accessible name. | TGG-ARIA-03 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the element. | TGG-API-09 |

**Consumed (not forwarded to the element):** `value`, `defaultValue`, `onValueChange`, `disabled`, `orientation`, `loopFocus`, `multiple`, `className`, `style`, `render`.

- **TGG-API-01** — MUST. When `value` is not `undefined` on the first render, the group's value MUST equal `value` on every render and MUST NOT be changed internally by user interaction ([UTL-STA-01](../architecture/shared-utilities.md), [UTL-STA-04](../architecture/shared-utilities.md)); the consumer's array is used as-is (never copied or mutated).
  Source: `toggle-group/ToggleGroup.tsx:48-53`; `packages/utils/src/useControlled.ts:41,46,83-90`. Test: [L2] `ToggleGroup.test.tsx` "pressed state" (controlled), "prop: value"; `ToolbarButton.test.tsx` "supports a controlled ToggleGroup value".
  Port note 2026-10-26 (ADR 08 Amendments 2026-10-22 and 2026-10-26, chosen by the user): `Value`, `DefaultValue` and `ValueChanged` are `IReadOnlyList<string>`, through `Controlled<IReadOnlyList<string>>`; the consumer's list is read as given (`ToggleGroupContext` tests membership against it). The `ToolbarButton.test.tsx` clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` title runs in `toolbar.spec.ts` "supports a controlled ToggleGroup value".
- **TGG-API-02** — MUST. When `value` is `undefined` on the first render, the initial value MUST be `defaultValue`, or an empty array when `defaultValue` is also `undefined`.
  Source: `toggle-group/ToggleGroup.tsx:41,50`; `packages/utils/src/empty.ts` (`EMPTY_ARRAY`). Test: [L2] `ToggleGroup.test.tsx` "prop: defaultValue", "pressed state" (uncontrolled, starts empty).
- **TGG-API-03** — MUST. `onValueChange` MUST receive the complete next value array (a fresh array, §7) as its first argument and the change-event-details object shared with the originating toggle as its second.
  Source: `toggle-group/ToggleGroup.tsx:55-73,168-169`. Test: [L2] `ToggleGroup.test.tsx` "fires when an Item is clicked" (`['one']`, then `['two']`).
- **TGG-API-04** — MUST. The effective disabled state MUST be `toolbar.disabled || toolbarGroup.disabled || disabled`, where the first two are the enclosing `Toolbar.Root` and `Toolbar.Group` effective disabled states (`false` when absent); the prop default is `false`. The effective value is published to every toggle ([TGL-API-04](toggle.md)).
  Source: `toggle-group/ToggleGroup.tsx:26,38-39,45-46,85-93`. Test: [L2] `ToggleGroup.test.tsx` "can disable the whole group"; `ToolbarButton.test.tsx` "disables direct ToggleGroup children when Toolbar.Group is disabled".
  Port note 2026-10-26: the group's own `Disabled` is the effective state in this pull request; the `Toolbar.Root` and `Toolbar.Group` terms and the `ToolbarButton.test.tsx` clause are open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the toolbar's and the toolbar group's disabled states join the group's own, read from their listener-list contexts (design point 14.4 (a) of M3 track 4, settled by the orchestrator 2026-10-27; `ToggleGroupToolbarTests` "port: inside a toolbar the group's disabled state is the toolbar's, the toolbar group's or its own"); the `ToolbarButton.test.tsx` title runs in `toolbar.spec.ts`.
- **TGG-API-05** — MUST. `orientation` MUST default to `'horizontal'` and accept only `'horizontal'` or `'vertical'` (the composite `'both'` value is not exposed); it selects the arrow-key axis standalone (TGG-KEY-01) and is always reflected in `data-orientation` (TGG-DOM-02).
  Source: `toggle-group/ToggleGroup.tsx:29,83,120,146,175-178`; `internals/types.ts:97`. Test: [L2] `ToggleGroup.test.tsx` "vertical", "$direction, orientation: $orientation moves focus with arrow keys" (L4 for the arrow-key axis clause).
  Port note 2026-10-26 (ADR 08 Amendments 2026-10-26, chosen by the user): `Orientation` is the shared non-nullable enum with the default `Horizontal`, passed to the composite as `CompositeLiterals.ToLiteral(Orientation)`; the arrow-key axis clause runs in `toggle-group.spec.ts`.
- **TGG-API-06** — MUST. `loopFocus` MUST default to `true` and, standalone, be passed to the composite root as its looping option ([CMP-API-01](../architecture/composite-and-list-navigation.md)); inside a Toolbar it MUST have no effect.
  Source: `toggle-group/ToggleGroup.tsx:27,118`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" ("loop to the beginning" step); `loopFocus={false}`: none (see [CMP-KEY-09](../architecture/composite-and-list-navigation.md)) (L4 for the looping behaviour).
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title is browser-only and asserts the module's wrap; it runs in `toggle-group.spec.ts`, beside "port: with LoopFocus false the arrows stop at either end". The option C# sends is asserted at L2 by `ToggleGroupTests` "port: the group's composite root sends its orientation, loopFocus and Home and End, and no disabled indices". The in-toolbar clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): inside a toolbar the group sends nothing, so `LoopFocus` has no effect (`ToggleGroupToolbarTests` "port: inside a toolbar the group's Orientation and LoopFocus send nothing, and data-orientation is its own").
- **TGG-API-07** — MUST. `multiple` MUST default to `false`; it selects the value-update rule (TGG-STA-03 vs TGG-STA-04) and is reflected in `data-multiple` (TGG-DOM-04). The former name `toggleMultiple` does not exist in 1.9.0.
  Source: `toggle-group/ToggleGroup.tsx:30,62-70,142,186-191`. Test: [L2] `ToggleGroup.test.tsx` "multiple items can be pressed when true", "only one item can be pressed when false". Docs: `CHANGELOG.md v1.0.0-beta.4 "Rename \`toggleMultiple\` prop to \`multiple\` (#2764)"`.
- **TGG-API-08** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md) with the state object below, in both the standalone (composite root) and in-toolbar (plain element) render paths.
  Source: `toggle-group/ToggleGroup.tsx:32-34,99-104,111-117`. Test: [L2] `ToggleGroup.test.tsx` conformance suite "Base UI component API".
- **TGG-API-09** — MUST. Every prop not listed as consumed MUST be forwarded to the group element, merged after the built-in `role` so that a consumer `role` wins ([RND-DOM-19](../architecture/rendering-and-composition.md)); standalone, consumer `onKeyDown`/`onFocus` handlers run before the composite root's own handlers and may veto them with `preventBaseUIHandler()` ([RND-EVT-02](../architecture/rendering-and-composition.md), [RND-EVT-03](../architecture/rendering-and-composition.md)). Server: relaxed, see §9.
  Source: `toggle-group/ToggleGroup.tsx:35,95-97,103,117`; `internals/composite/root/CompositeRoot.tsx:67-72` (`[defaultProps, ...props, elementProps]`). Test: [L2] `ToggleGroup.test.tsx` "renders a `group`" (`aria-label` forwarded), conformance suite (prop forwarding) (L4 for the veto clause, relaxed on Server).
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-22, option (i), chosen by the user 2026-10-23; ADR 02 Amendments 2026-10-21): the group declines the consumer's `onkeydown` and the composite runs it at `navigate` (`ToggleGroupTests` "port: the group declines its consumer onkeydown, which the root runs at navigate"; `toggle-group.spec.ts` "port: the group's C# onkeydown vetoes a handled arrow on WebAssembly and runs a round trip late on Server"). The composite root's own `onFocus` only selects the value of a native text input that receives focus (`useCompositeRoot.ts:321-327`); no toggle is one, so a consumer `onfocus` has nothing to veto and is forwarded unchanged.

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes.

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (TGG-API-04). |
| `multiple` | `boolean` | The `multiple` prop. |
| `orientation` | `'horizontal' \| 'vertical'` | The `orientation` prop. |

- **TGG-STA-01** — MUST. The state object MUST contain exactly the three fields above.
  Source: `toggle-group/ToggleGroup.tsx:83,131-147`. Test: none (shape pinned by `ToggleGroup.State` type).
  Port note 2026-10-26: `ToggleGroupState(bool Disabled, bool Multiple, Orientation Orientation)`, a sealed record (`ToggleGroupTests` "port: the state is the effective disabled state, Multiple and Orientation").

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; `"horizontal"` or `"vertical"` | TGG-DOM-02 |
| `data-disabled` | `""` while effective disabled | TGG-DOM-03 |
| `data-multiple` | `""` while `multiple` is `true` | TGG-DOM-04 |

#### CSS variables

None.

### 3.2 Shared types

- `ToggleGroup.ChangeEventReason` = `'none'` — the only literal. Source: `toggle-group/ToggleGroup.tsx:194`, `internals/reason-parts.ts:1`.
- `ToggleGroup.ChangeEventDetails` = the common change-event-details shape from [EVD-API-01](../architecture/event-details-and-reasons.md) specialised to reason `'none'`; it is the *same object* as the originating toggle's `Toggle.ChangeEventDetails`. Source: `toggle-group/ToggleGroup.tsx:196`; `toggle-group/ToggleGroupContext.ts:8-12`.
- `ToggleGroup.State` — the state object in §3.1. `ToggleGroup.Props<Value extends string = string>` — the prop table; `value`/`defaultValue` are typed `readonly Value[]` (a `const` tuple is accepted), while `onValueChange` receives a mutable `Value[]`. Source: `toggle-group/ToggleGroup.tsx:158-169,198-203`; `toggle-group/ToggleGroup.spec.tsx:4-27`.
  Port note 2026-10-26 (ADR 08 Amendments 2026-10-22 and 2026-10-26, chosen by the user; TGG §14 "mirror … or document the deviation"): `ToggleGroup` is not generic, and `Value`, `DefaultValue`, `ValueChanged` and `OnValueChange` carry `IReadOnlyList<string>` both ways, so the read-only input / mutable output split and the `const` tuple narrowing are not expressed. Each commit is a new list (TGG-EVT-05) and list equality is by reference; there is no `ValueExpression`, since ToggleGroup has no form integration (§8).
- Exported constant namespace `ToggleGroupDataAttributes` = `{ disabled: 'data-disabled', orientation: 'data-orientation', multiple: 'data-multiple' }`. Source: `toggle-group/ToggleGroupDataAttributes.ts:4,9,13`, `toggle-group/index.ts:5`.
- `Orientation` = `'horizontal' | 'vertical'`. Source: `internals/types.ts:97`.

## 4. State model

The group owns one piece of state, the value array; the roving tab stop (standalone) is owned by the composite root mechanism; `disabled`, `multiple` and `orientation` are prop echoes.

**Ownership.** `value`/`defaultValue` is the controlled/uncontrolled pair, decided once on the first render. A toggle activation in consumer-owned mode only fires the callbacks; toggles re-derive their pressed state from whatever `value` the consumer renders next.

**Identity.** Each toggle contributes a string identity ([TGL-API-05](toggle.md)); membership is tested with string equality (`indexOf`). Duplicated identities are not prevented (§12).

**Value update rules.** For an activation of a toggle with identity `v` and next pressed state `next`:

| `multiple` | `next` | next value |
|---|---|---|
| `false` | `true` | `[v]` (every other toggle becomes unpressed) |
| `false` | `false` | `[]` (the last pressed toggle may be deselected) |
| `true` | `true` | `[...current, v]` (appended at the end) |
| `true` | `false` | `current` with the **first** occurrence of `v` removed |

**Derived values.**

- effective disabled = `toolbar.disabled || toolbarGroup.disabled || disabled` (TGG-API-04)
- `isValueInitialized` = `value !== undefined || defaultValue !== undefined` (raw props, evaluated every render) — consumed only by the Toggle warning ([TGL-EDGE-01](toggle.md))
- each toggle's `pressed` = `value.includes(identity)` ([TGL-STA-04](toggle.md))

- **TGG-STA-02** — MUST. Ownership of the value MUST be fixed on the first render: consumer-owned if `value !== undefined`, otherwise component-owned; later crossings MUST NOT change the mode (dev diagnostic TGG-EDGE-02), and a consumer-owned value that later becomes `undefined` MUST fall back to the first render's default ([UTL-STA-03](../architecture/shared-utilities.md)).
  Source: `toggle-group/ToggleGroup.tsx:48-53`; `packages/utils/src/useControlled.ts:41-46`. Test: none.
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: ownership is fixed at the first parameter set, and a removed Value falls back to the initial default".
- **TGG-STA-03** — MUST. With `multiple` `false`, a non-canceled activation that presses a toggle MUST make the next value exactly `[identity]`, and one that unpresses the pressed toggle MUST make it `[]`; deselecting the last pressed toggle MUST NOT be prevented by the component.
  Source: `toggle-group/ToggleGroup.tsx:69-71`. Test: [L1] `ToggleGroup.test.tsx` "only one item can be pressed when false", "pressed state" (uncontrolled), "when Toggles omit value" (single); deselection to `[]`: none (`docs/src/app/(private)/experiments/toggle-group.tsx:16-21` shows the consumer enforcing "one must remain pressed" by ignoring an empty next value).
  Port note 2026-10-26 (toggle-group §16 Q1, chosen by the user 2026-10-26: mirror the source): stays `[L1]`; the rule is the internal pure function `ToggleGroupValue.Next` (`ToggleGroupValueTests`, with the cited titles and "port: pressing the pressed toggle again empties a single-select value"). The rendering half of each title is `ToggleGroupTests` (L2).
- **TGG-STA-04** — MUST. With `multiple` `true`, a non-canceled activation that presses a toggle MUST append its identity to the end of a copy of the current array, and one that unpresses MUST remove the first occurrence of its identity (by `indexOf`) from a copy; the current array MUST NOT be mutated.
  Source: `toggle-group/ToggleGroup.tsx:62-68`. Test: [L1] `ToggleGroup.test.tsx` "multiple items can be pressed when true", "when Toggles omit value" (multiple: press, press, unpress); `ToolbarButton.test.tsx` "supports multiple selection for direct ToggleGroup > Toggle children" (`['one', 'two']`).
  Port note 2026-10-26: stays `[L1]` (`ToggleGroupValueTests`, with the cited titles and the port titles for a new list per call, no mutation and ordinal membership); the rendering half is `ToggleGroupTests` (L2). The `ToolbarButton.test.tsx` clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the `ToolbarButton.test.tsx` title runs in `toolbar.spec.ts` "supports multiple selection for direct ToggleGroup > Toggle children" (`value:one,two`).
- **TGG-STA-05** — MUST. Changing `multiple` at runtime MUST NOT alter the current value (no normalization): a multi-element array survives switching to `multiple={false}` until the next activation applies TGG-STA-03.
  Source: `toggle-group/ToggleGroup.tsx:30,48-53` (no effect on `multiple`). Test: [L2] `ToggleGroup.test.tsx` "preserves selection and roving focus when %s" (single → multiple → single with the selection carried across).
  Port note 2026-10-26 (ADR 13 §3): stays `[L2]`: bUnit clicks show the value and `data-multiple` across the `Multiple` flips (`ToggleGroupTests` "port: changing Multiple keeps the value, and the next activation follows the new rule"); the roving half of the cited title is TGG-FOC-03's, in `toggle-group.spec.ts`. The "nested in Toolbar.Group" variant is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the "nested in Toolbar.Group" variant runs in `toggle-group.spec.ts`.
- **TGG-STA-06** — MUST. In component-owned mode, a non-canceled activation MUST commit the next array computed by TGG-STA-03/04; in consumer-owned mode the group MUST render whatever `value` the consumer supplies and MUST NOT store the request ([EVD-STA-04](../architecture/event-details-and-reasons.md)).
  Source: `toggle-group/ToggleGroup.tsx:75-79`; `packages/utils/src/useControlled.ts:83-90`. Test: [L2] `ToggleGroup.test.tsx` "pressed state" (uncontrolled), "pressed state" (controlled, `setProps`).
- **TGG-STA-07** — MUST. The group MUST expose to its toggles whether its value was initialized by the consumer: `true` iff `value` or `defaultValue` (raw props) is not `undefined` on the current render; this flag gates [TGL-EDGE-01](toggle.md) and has no other effect.
  Source: `toggle-group/ToggleGroup.tsx:42-43,90`; `toggle-group/ToggleGroupContext.ts:14-18`. Test: [L2] `ToggleGroup.test.tsx` "should warn if Toggle value is not set and ToggleGroup value is defined" (`defaultValue` set → warning); "when Toggles omit value" (nothing set → no warning asserted, `console.error` not mocked).
- **TGG-STA-08** — MUST. Standalone, the group's roving tab stop MUST be the composite root's highlighted index, owned by the mechanism, initially `0` ([CMP-STA-22](../architecture/composite-and-list-navigation.md)); ToggleGroup sets no active item marker, so the initially pressed toggle does **not** receive the initial tab stop.
  Source: `toggle-group/ToggleGroup.tsx:111-121` (no `highlightedIndex`, no marker); `internals/composite/root/useCompositeRoot.ts:90`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (first <kbd>Tab</kbd> lands on `button1` with `tabindex="0"`); pressed-but-not-first case: none.
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled); toggle-group §16 Q2, chosen by the user 2026-10-26: the cited title is browser-only and reads the tab stop the `composite` module writes after its first publication; it runs in `toggle-group.spec.ts`. The group sets no active-item marker, so the first toggle takes the initial tab stop whatever is pressed.

## 5. Rendered DOM and ARIA

### 5.1 Group element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"group"` (consumer may override) | TGG-ARIA-01 | C# |
| `aria-orientation` | never emitted | TGG-ARIA-02 | C# |
| `aria-label` / `aria-labelledby` | consumer-supplied only | TGG-ARIA-03 | C# |
| `tabindex` | never emitted on the group element | TGG-DOM-05 | C# |
| `data-orientation` | `"horizontal"` / `"vertical"` | TGG-DOM-02 | C# |
| `data-disabled` | `""` while disabled | TGG-DOM-03 | C# |
| `data-multiple` | `""` while `multiple` | TGG-DOM-04 | C# |
| handlers | standalone: composite `keydown` + `focus` handlers; in toolbar: none of its own | TGG-DOM-06 | JS |

- **TGG-DOM-01** — MUST. The default element MUST be `<div>`; with `render` the substituted element receives the attributes and handlers of this section.
  Source: `toggle-group/ToggleGroup.tsx:99,111`. Test: [L2] `ToggleGroup.test.tsx` conformance suite "Base UI component API" (`refInstanceof: HTMLDivElement`).
- **TGG-DOM-02** — MUST. `data-orientation` MUST always be present with the string value of `orientation` ([RND-DOM-03](../architecture/rendering-and-composition.md)), in both render paths.
  Source: `toggle-group/ToggleGroup.tsx:83`; `toggle-group/ToggleGroupDataAttributes.ts:9`. Test: [L2] `ToggleGroup.test.tsx` "vertical".
- **TGG-DOM-03** — MUST. `data-disabled=""` MUST be present exactly while the effective disabled state is `true`.
  Source: `toggle-group/ToggleGroup.tsx:45-46,83`; `toggle-group/ToggleGroupDataAttributes.ts:4`. Test: none directly on the group element (`ToggleGroup.test.tsx` "can disable the whole group" asserts the toggles; `ToolbarRoot.test.tsx` "disables all toolbar items except links" asserts the analogous attribute on `Toolbar.Group`).
  Port note 2026-10-26: asserted on the group element by `ToggleGroupTests` "port: the state is the effective disabled state, Multiple and Orientation" and "port: the data attribute constants are Base UI's".
- **TGG-DOM-04** — MUST. `data-multiple=""` MUST be present exactly while `multiple` is `true`, and MUST be added/removed when the prop changes at runtime.
  Source: `toggle-group/ToggleGroup.tsx:83`; `toggle-group/ToggleGroupDataAttributes.ts:13`. Test: [L2] `ToggleGroup.test.tsx` "sets data-multiple only when true", "preserves selection and roving focus when %s".
- **TGG-DOM-05** — MUST. The group element MUST NOT carry a `tabindex` of its own (the tab stop lives on a toggle, [CMP-DOM-03](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (<kbd>Tab</kbd> lands on a toggle).
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title is browser-only (where <kbd>Tab</kbd> lands); it runs in `toggle-group.spec.ts`. That C# renders no `tabindex` on the group is asserted at L2 by `ToggleGroupTests` "port: grouped toggles render tabindex -1, which the composite owns from attach, and the group renders none".
- **TGG-DOM-06** — MUST. Standalone, the group element MUST carry the composite root's `keydown` and `focus` handlers (§6); inside a Toolbar it MUST carry only forwarded consumer handlers.
  Source: `toggle-group/ToggleGroup.tsx:99-104,106-123`; `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L4] `ToggleGroup.test.tsx` "preserves selection and roving focus when %s" (both paths navigate; in the toolbar path the toolbar's handler does the work).
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title is browser-only and asserts the module's handlers. ADR 04 Amendments 2026-10-26 (option (i)'s precision, chosen by the user; ADR 01's row "… EVD-EVT-26 against composite/button key handling …" as amended 2026-10-26): the standalone group's keydown handling is the `composite` module's listener, and a key the module handles never reaches Blazor's dispatcher, so a C# `onkeydown` on an element between a toggle and the group (a consumer wrapper; in a toolbar, the group itself) never runs for it, where Base UI runs it before the root stops propagation; for a key the module does not handle it runs as usual (`toggle-group.spec.ts` "port: a C# onkeydown on a wrapper between the toggles and the group runs for an unhandled key and not for a handled arrow"). The in-toolbar clause is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): inside a toolbar the group renders no handler of its own and forwards the consumer's `onkeydown` (`ToggleGroupToolbarTests` "port: inside a toolbar the group forwards its consumer onkeydown and renders no handler of its own"); for a key the toolbar's module handles, that C# handler never runs, as for a `ToolbarGroup` (TLB-API-16).
- **TGG-DOM-07** — MUST. A consumer attribute with the same name as a built-in one (`role`, `data-*`) MUST win; the `data-*` state attributes are emitted before consumer props in the merge order.
  Source: `toggle-group/ToggleGroup.tsx:95-97,103,117`; `internals/composite/root/CompositeRoot.tsx:70`. Test: [L2] `ToggleGroup.test.tsx` conformance suite (prop forwarding).

### 5.2 ARIA

- **TGG-ARIA-01** — MUST. The group element MUST carry `role="group"` by default.
  Source: `toggle-group/ToggleGroup.tsx:95-97`. Test: [L2] `ToggleGroup.test.tsx` "renders a `group`".
- **TGG-ARIA-02** — MUST. The group element MUST NOT carry `aria-orientation` for either orientation (the attribute is not valid on `role="group"`; styling uses `data-orientation`).
  Source: `toggle-group/ToggleGroup.tsx:95-97` (no `aria-orientation`); `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L2] `ToggleGroup.test.tsx` "does not render aria-orientation on role="group"". Docs: `CHANGELOG.md v1.6.0 "Remove invalid \`aria-orientation\` from \`role="group"\` element (#4628)"`.
- **TGG-ARIA-03** — MUST. The group MUST NOT generate an accessible name; `aria-label` / `aria-labelledby` supplied by the consumer are forwarded unchanged and are the only naming mechanism.
  Source: `toggle-group/ToggleGroup.tsx:35,103,117`. Test: [L2] `ToggleGroup.test.tsx` "renders a `group`" (`getByRole('group', { name: 'My Toggle Group' })`). Docs: `components/toggle-group/demos/hero/css-modules/index.tsx` (`aria-label="Text alignment"`).
- **TGG-ARIA-04** — MUST. Each toggle's `aria-pressed` MUST reflect membership in the group value ([TGL-ARIA-01](toggle.md), [TGL-STA-04](toggle.md)); the group adds no `aria-checked`, `aria-selected` or `aria-multiselectable`.
  Source: `toggle-group/ToggleGroup.tsx:95-97`; `toggle/Toggle.tsx:62,81`. Test: [L2] `ToggleGroup.test.tsx` "pressed state" (controlled).
- **TGG-ARIA-05** — MUST. Every member Toggle is a composite item (standalone: under the group's composite root; inside a Toolbar: under the toolbar's, [UBT-STA-01](../architecture/button-behavior.md)) and passes `disabled` (effective, TGG-API-04) and `native` to the button mechanism with `focusableWhenDisabled` **unspecified** — the item metadata `focusableWhenDisabled: false` is read only by `Toolbar.Root` for `disabledIndices` and is never passed to the mechanism, so the "explicitly `false`" rows of [UBT-ALG-03](../architecture/button-behavior.md) ([UBT-ARIA-04](../architecture/button-behavior.md)) do not apply. The observable result is per [UBT-ALG-03](../architecture/button-behavior.md), [UBT-ARIA-01](../architecture/button-behavior.md), [UBT-ARIA-02](../architecture/button-behavior.md), [UBT-ARIA-05](../architecture/button-behavior.md), [UBT-DOM-05](../architecture/button-behavior.md), [UBT-DOM-06](../architecture/button-behavior.md) and [UBT-FOC-05](../architecture/button-behavior.md), i.e. for grouped toggles exactly:
  Port note 2026-10-27 (M3 track 4): inside a toolbar the toggles are items of the toolbar's root, with the toggle's flags (TGL-STR-02's note).

  | `nativeButton` | effective disabled | `disabled` attr | `aria-disabled` | focusable / in arrow order |
  |---|---|---|---|---|
  | `true` (default) | false | — | `"false"` | yes |
  | `true` (default) | true | present | `"true"` | no (natively disabled; skipped, TGG-KEY-08) |
  | `false` | false | — | — | yes |
  | `false` | true | — | `"true"` | keeps roving `tabindex` (Tab stop if highlighted), skipped by arrow keys (TGG-KEY-08) |

  An enabled native grouped toggle therefore always renders `aria-disabled="false"`; a disabled native grouped toggle is **not** focusable-when-disabled despite being a composite item (the composite DOM-`disabled` correction of [UBT-ALG-04](../architecture/button-behavior.md) does not fire because the mechanism itself emitted `disabled`), and the non-`Tab` keydown suppression of [UBT-KEY-20](../architecture/button-behavior.md) does not apply. The per-toggle statement is [TGL-ARIA-02](toggle.md).
  Source: `toggle/Toggle.tsx:46,68-71,118-121,124-135`; `internals/use-button/useButton.ts:25-34,79-87`; `utils/useFocusableWhenDisabled.ts:15-16,24,38-47`; see [button-behavior](../architecture/button-behavior.md). Test: [L2] `ToggleGroup.test.tsx` "can disable individual items" (`aria-disabled="false"` on the enabled toggle, `"true"` on the disabled one), "can disable the whole group" (`aria-disabled="true"` on both); non-native rows: none.

## 6. Interactions

### 6.1 Keyboard

Standalone, keyboard navigation is the composite root's ([CMP](../architecture/composite-and-list-navigation.md) §4.5) with the options `orientation` (TGG-API-05), `loopFocus` (TGG-API-06), `enableHomeAndEndKeys: true`, `stopEventPropagation: true` (composite root part default), no `disabledIndices` (disabled toggles are detected from the DOM), no grid, no `highlightItemOnHover`, no `modifierKeys`. Toggling itself is the toggle's business ([TGL-KEY-01](toggle.md), [TGL-KEY-02](toggle.md)). Inside a Toolbar none of this applies — see [TLB-KEY-01](toolbar.md).

| Key | Context (focused toggle, standalone group) | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowRight</kbd> | `horizontal`, LTR | focus next enabled toggle; wraps to first when `loopFocus` | TGG-KEY-01 |
| <kbd>ArrowLeft</kbd> | `horizontal`, LTR | focus previous enabled toggle; wraps to last when `loopFocus` | TGG-KEY-01 |
| <kbd>ArrowLeft</kbd> | `horizontal`, RTL | focus **next** (forward) toggle | TGG-KEY-02 |
| <kbd>ArrowRight</kbd> | `horizontal`, RTL | focus **previous** (backward) toggle | TGG-KEY-02 |
| <kbd>ArrowDown</kbd> | `vertical`, any direction | focus next enabled toggle; wraps when `loopFocus` | TGG-KEY-01 |
| <kbd>ArrowUp</kbd> | `vertical`, any direction | focus previous enabled toggle; wraps when `loopFocus` | TGG-KEY-01 |
| <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> | `horizontal` | not handled: no focus change, not consumed | TGG-KEY-03 |
| <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> | `vertical` | not handled: no focus change, not consumed | TGG-KEY-03 |
| <kbd>Home</kbd> | any | focus first enabled toggle | TGG-KEY-04 |
| <kbd>End</kbd> | any | focus last enabled toggle | TGG-KEY-04 |
| any of the above with <kbd>Shift</kbd>/<kbd>Ctrl</kbd>/<kbd>Alt</kbd>/<kbd>Meta</kbd> | any | not handled | TGG-KEY-07 |
| arrow/Home/End at an end with `loopFocus={false}` | any | no focus change, not consumed | TGG-KEY-05 |
| <kbd>Enter</kbd>, <kbd>Space</kbd> | any | toggle the focused item (Space on keydown) — [TGL-KEY-02](toggle.md) | TGG-KEY-09 |
| <kbd>Tab</kbd>, <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | leave the group (single tab stop) | TGG-FOC-01 |
| any key | effective disabled group | arrows still navigate between *focusable* toggles (non-native) or do nothing (native toggles are unfocusable); no toggling | TGG-KEY-08 |

- **TGG-KEY-01** — MUST. Standalone, the group MUST handle the arrow keys of its orientation per [CMP-KEY-01](../architecture/composite-and-list-navigation.md) / [CMP-KEY-03](../architecture/composite-and-list-navigation.md): `'horizontal'` → <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd>, `'vertical'` → <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>; a forward key moves the tab stop and focus to the next non-disabled toggle, a backward key to the previous one ([CMP-KEY-08](../architecture/composite-and-list-navigation.md)), wrapping at the ends while `loopFocus` is `true` ([CMP-KEY-06](../architecture/composite-and-list-navigation.md), [CMP-KEY-07](../architecture/composite-and-list-navigation.md)).
  Source: `toggle-group/ToggleGroup.tsx:118-120`; `internals/composite/root/useCompositeRoot.ts:221-226,267-300`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (ltr/horizontal, ltr/vertical rows).
- **TGG-KEY-02** — MUST. Under RTL direction (from the nearest `DirectionProvider`, [CMP-I18N-01](../architecture/composite-and-list-navigation.md)), the horizontal forward key MUST be <kbd>ArrowLeft</kbd> and the backward key <kbd>ArrowRight</kbd> ([CMP-I18N-02](../architecture/composite-and-list-navigation.md)); vertical keys are unaffected ([CMP-I18N-03](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/CompositeRoot.tsx:43`; `internals/composite/root/useCompositeRoot.ts:221-224`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (rtl/horizontal, rtl/vertical rows).
- **TGG-KEY-03** — MUST. Arrow keys of the other axis MUST NOT move focus and MUST NOT be consumed (no `preventDefault`, no `stopPropagation`) ([CMP-API-02](../architecture/composite-and-list-navigation.md), [CMP-KEY-11](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/useCompositeRoot.ts:267-272,282,302`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" ("keys from the other axis should not move focus"). Docs: `CHANGELOG.md v1.4.0 "Fix incorrect keyboard nav on cross axis (#4488)"`.
- **TGG-KEY-04** — MUST. <kbd>Home</kbd> MUST move the tab stop and focus to the first non-disabled toggle and <kbd>End</kbd> to the last, regardless of orientation and direction ([CMP-KEY-05](../architecture/composite-and-list-navigation.md)); the group enables these keys explicitly.
  Source: `toggle-group/ToggleGroup.tsx:119`; `internals/composite/root/useCompositeRoot.ts:207-210,274-280`. Test: [L4] `ToggleGroup.test.tsx` "Home key moves focus to the first item", "End key moves focus to the last item". Docs: `CHANGELOG.md v1.2.0 "Enable \`Home\`/\`End\` key navigation (#3971)"`.
- **TGG-KEY-05** — MUST. With `loopFocus={false}`, a forward key on the last enabled toggle and a backward key on the first MUST do nothing (focus stays, event not consumed) ([CMP-KEY-09](../architecture/composite-and-list-navigation.md), [CMP-KEY-11](../architecture/composite-and-list-navigation.md)).
  Source: `toggle-group/ToggleGroup.tsx:118`; `internals/composite/root/useCompositeRoot.ts:282-300,302`. Test: none in this slice (`CompositeRoot.test.tsx` "does not loop or call onLoop when loopFocus is disabled"; `ToolbarRoot.test.tsx` "does not wrap focus when loopFocus is false" for the sibling component).
  Port note 2026-10-26: asserted by `toggle-group.spec.ts` "port: with LoopFocus false the arrows stop at either end" (focus stays and the key is not default-prevented).
- **TGG-KEY-06** — MUST. When a handled key changes the tab stop, the group MUST call `preventDefault()` and `stopPropagation()` on the keydown ([CMP-KEY-10](../architecture/composite-and-list-navigation.md), [CMP-KEY-15](../architecture/composite-and-list-navigation.md)), so that a ToggleGroup rendered inside a popup (menu, popover) does not also drive the popup's list navigation; focus moves to the new toggle one microtask later ([CMP-FOC-02](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/CompositeRoot.tsx:34`; `internals/composite/root/useCompositeRoot.ts:302-316`. Test: none. Docs: `CHANGELOG.md v1.0.0-beta.0 "Stop event propagation to allow composite components to be used within popups (#1871)"`.
  Port note 2026-10-26: asserted by `toggle-group.spec.ts` "port: a handled arrow is default-prevented and stopped, and Enter is neither" (`StopEventPropagation` stays `true`).
- **TGG-KEY-07** — MUST. A navigation key pressed with any of <kbd>Shift</kbd>, <kbd>Control</kbd>, <kbd>Alt</kbd>, <kbd>Meta</kbd> held MUST be ignored by the group ([CMP-KEY-02](../architecture/composite-and-list-navigation.md); no `modifierKeys` allowance is configured).
  Source: `toggle-group/ToggleGroup.tsx:111-121` (no `modifierKeys`); `internals/composite/root/useCompositeRoot.ts:212-214`. Test: none.
  Port note 2026-10-26: the group sends no modifier keys (`ToggleGroupTests` "port: the group's composite root sends its orientation, loopFocus and Home and End, and no disabled indices"); the module's rule is CMP-KEY-02's, green at L3.
- **TGG-KEY-08** — MUST. Because the group passes no `disabledIndices`, navigation MUST skip toggles that are disabled in the DOM: elements matching `:disabled`, carrying the `disabled` attribute, or `aria-disabled="true"`, and elements that are not visible ([CMP-ARIA-03](../architecture/composite-and-list-navigation.md), [CMP-ALG-02](../architecture/composite-and-list-navigation.md)). A disabled native toggle is therefore both unfocusable and skipped; a disabled non-native toggle is skipped by its `aria-disabled`. When every toggle is disabled, keys do nothing.
  Source: `toggle-group/ToggleGroup.tsx:111-121` (no `disabledIndices`); `floating-ui-react/utils/composite.ts:520-553`; `toggle/Toggle.tsx:68-71`. Test: none in this slice (`ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children" covers the toolbar-hosted variant, where the toolbar's `disabledIndices` and the DOM check agree).
  Port note 2026-10-26 (toggle-group §16 Q3, settled by the orchestrator 2026-10-26): the standalone group sends no `disabledIndices`, so the module reads each toggle's disabled state from the document (`ToggleGroupTests` "port: the group's composite root sends …"; `toggle-group.spec.ts` "port: the arrow keys skip a disabled toggle"). The toolbar variant is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the toolbar's root derives its disabled indices from the items' flags and a standalone group's does not, so §16 Q3's option is per root (`ToolbarItemFlagsTests` "port: the toolbar's root derives its disabled indices from the items' flags, and a standalone toggle group's does not"); the cited `ToolbarButton.test.tsx` title runs in `toolbar.spec.ts`.
- **TGG-KEY-09** — MUST. <kbd>Enter</kbd> and <kbd>Space</kbd> MUST toggle the focused toggle (Space activating on keydown because the toggle is a composite item, [TGL-KEY-02](toggle.md)); the group does not intercept them.
  Source: `internals/composite/composite.ts:22` (`COMPOSITE_KEYS` excludes Enter/Space); `internals/use-button/useButton.ts:138-152`. Test: [L4] `ToggleGroup.test.tsx` "key: $key toggles the pressed state", "fires when the $key is pressed".
- **TGG-KEY-10** — MUST. Inside a `Toolbar.Root`, the group MUST NOT handle any key; arrow navigation among its toggles is the toolbar's ([TLB-KEY-01](toolbar.md)), uses the **toolbar's** orientation, direction and `loopFocus`, and does not handle <kbd>Home</kbd>/<kbd>End</kbd>; the group's own `orientation` and `loopFocus` are inert.
  Source: `toggle-group/ToggleGroup.tsx:99-104,108-109`. Test: [L4] `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children"; `ToggleGroup.test.tsx` "preserves selection and roving focus when nested in Toolbar.Group".
  Port note 2026-10-27 (M3 track 4): both titles run, "preserves selection and roving focus when nested in Toolbar.Group" in `toggle-group.spec.ts` and "navigates and selects direct ToggleGroup > Toggle children" in `toolbar.spec.ts`; the group's options send nothing (`ToggleGroupToolbarTests` "port: inside a toolbar the group's Orientation and LoopFocus send nothing, …").

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| click on a toggle | enabled | value updated per TGG-STA-03/04 through the toggle's activation ([TGL-PTR-01](toggle.md)); the clicked toggle becomes the tab stop | TGG-PTR-01 |
| click on a toggle | disabled (group or toggle) | nothing ([TGL-PTR-02](toggle.md)) | TGG-PTR-02 |
| mouse move over a toggle | any | no focus or highlight change | TGG-PTR-03 |
| click on the group element itself | any | nothing (no handler) | — |

- **TGG-PTR-01** — MUST. A pointer click on an enabled toggle MUST run the toggle's activation sequence, which updates the group value per TGG-STA-03/04 and fires `onValueChange` (§7); the clicked toggle MUST become the group's tab stop by virtue of receiving focus ([CMP-PTR-02](../architecture/composite-and-list-navigation.md)).
  Source: `toggle/Toggle.tsx:82-103`; `toggle-group/ToggleGroup.tsx:55-81`; `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L2] `ToggleGroup.test.tsx` "pressed state" (uncontrolled), "prop: defaultValue", "fires when an Item is clicked", "preserves selection and roving focus when %s" (<kbd>ArrowLeft</kbd> after clicking `button2` moves to `button1`) (L4 for the tab-stop-on-focus clause).
  Port note 2026-10-26: the tab-stop-on-focus clause runs in `toggle-group.spec.ts` ("preserves selection and roving focus when standalone"); the "nested in Toolbar.Group" variant is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the "nested in Toolbar.Group" variant runs in `toggle-group.spec.ts`.
- **TGG-PTR-02** — MUST. While the group is disabled, clicks on its toggles MUST NOT change the value or fire callbacks (every toggle is disabled by TGG-API-04).
  Source: `toggle-group/ToggleGroup.tsx:45-46,87`; `internals/use-button/useButton.ts:104-110`. Test: none (`ToggleGroup.test.tsx` "can disable the whole group" asserts attributes only).
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: a disabled group's toggles ignore clicks".
- **TGG-PTR-03** — MUST. Moving the pointer over a toggle MUST NOT focus it or move the tab stop (`highlightItemOnHover` is not enabled, [CMP-PTR-01](../architecture/composite-and-list-navigation.md)).
  Source: `toggle-group/ToggleGroup.tsx:111-121`; `internals/composite/root/CompositeRoot.tsx:38`. Test: none.
  Port note 2026-10-26: asserted by `toggle-group.spec.ts` "port: moving the pointer over a toggle neither focuses it nor moves the tab stop".

### 6.3 Focus management

- **TGG-FOC-01** — MUST. Standalone, the group MUST occupy exactly one position in the sequential tab order: the toggle whose index equals the highlighted index carries `tabindex="0"`, all others `tabindex="-1"` ([CMP-DOM-01](../architecture/composite-and-list-navigation.md), [CMP-FOC-01](../architecture/composite-and-list-navigation.md)); <kbd>Tab</kbd> enters on that toggle and <kbd>Tab</kbd> again leaves the group.
  Source: `internals/composite/item/useCompositeItem.ts:19,25`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys", "Home key moves focus to the first item" (L4 for the Tab-order clause).
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): both cited titles are browser-only and read the roving `tabindex` the `composite` module writes; they run in `toggle-group.spec.ts`, beside "port: Tab enters the group on the toggle holding the tab stop and leaves it in one press".
- **TGG-FOC-02** — MUST. The initial tab stop MUST be the first toggle (index `0`), or — when that toggle is disabled in the DOM at the time the item list is first published — the first non-disabled toggle ([CMP-STA-24](../architecture/composite-and-list-navigation.md)); pressed state plays no role (TGG-STA-08). Server: relaxed, see §9.
  Source: `internals/composite/root/useCompositeRoot.ts:90,137-161`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (`button1` first); disabled-first case: none in this slice (L4 for the disabled-first re-validation, relaxed on Server).
  Port note 2026-10-26, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title is browser-only and reads the initial tab stop of the first publication; it runs in `toggle-group.spec.ts`, and the disabled-first case in "port: with the first toggle disabled, the first enabled toggle takes the initial tab stop".
- **TGG-FOC-03** — MUST. Focusing any toggle (keyboard, pointer or programmatic) MUST make it the tab stop; a key move MUST update `tabindex` on both toggles and focus the new one one microtask after the keydown ([CMP-PTR-02](../architecture/composite-and-list-navigation.md), [CMP-KEY-16](../architecture/composite-and-list-navigation.md), [CMP-FOC-02](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/useCompositeRoot.ts:310-315`; `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (`tabindex="0"` and focus asserted together), "key: $key toggles the pressed state" (programmatic `focus()` then key).
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-22): the key-move clause is met on both modes, because the module writes `tabindex` in the task of the focus move (`toggle-group.spec.ts` "port: after an arrow key the focus and the tab stop move in the same task, on both modes"); the row leaves §9's CMP-STA-10 entry and its trailing "Server: relaxed" sentence is removed. The "nested in Toolbar.Group" variant is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the "nested in Toolbar.Group" variant runs in `toggle-group.spec.ts`; inside a toolbar the tab stop is the toolbar root's.
- **TGG-FOC-04** — MUST. When toggles are added, removed or reordered, the tab stop MUST follow the toggle that held it ([CMP-STA-26](../architecture/composite-and-list-navigation.md), [CMP-STA-27](../architecture/composite-and-list-navigation.md)). Server: relaxed, see §9.
  Source: `internals/composite/root/useCompositeRoot.ts:115-134`. Test: none in this slice.
  Port note 2026-10-26: asserted per mode by `toggle-group.spec.ts` "port: the tab stop follows its toggle when the toggles are reordered" (the CMP-STA-10 row, §9).
- **TGG-FOC-05** — MUST. Inside a `Toolbar.Root` the group MUST NOT introduce a tab stop of its own: its toggles are items of the toolbar's single roving group and the arrow keys traverse toolbar items and toggles alike ([TLB-FOC-01](toolbar.md)).
  Source: `toggle-group/ToggleGroup.tsx:99-109`; `toggle/Toggle.tsx:123-135`. Test: [L4] `ToggleGroup.test.tsx` "preserves selection and roving focus when nested in Toolbar.Group"; `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children".
  Port note 2026-10-27 (M3 track 4): as TGG-KEY-10's note.

### 6.4 Dismissal

Not applicable — no popup.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(groupValue: Value[], details: ToggleGroup.ChangeEventDetails) => void` | A toggle in the group is activated (pointer, keyboard, programmatic click) and the toggle's own `onPressedChange` did not cancel | `'none'` | yes — `details.cancel()` |

- **TGG-EVT-01** — MUST. `onValueChange` MUST be invoked synchronously, inside the originating toggle's click handling, after that toggle's `onPressedChange` and before any state is committed ([EVD-EVT-02](../architecture/event-details-and-reasons.md), [EVD-EVT-04](../architecture/event-details-and-reasons.md)), with the next array computed by TGG-STA-03/04.
  Source: `toggle/Toggle.tsx:88-96`; `toggle-group/ToggleGroup.tsx:55-73`. Test: [L2] `ToggleGroup.test.tsx` "fires when an Item is clicked"; `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children" (`['three']` replaces `['one']`).
- **TGG-EVT-02** — MUST. `details` MUST be the very object the toggle created: `reason` `'none'`, `event` the native click event, `trigger` `undefined` ([TGL-EVT-02](toggle.md), [TGL-EVT-03](toggle.md)).
  Source: `toggle/Toggle.tsx:84,95`; `toggle-group/ToggleGroupContext.ts:8-12`. Test: none.
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: the group's request carries the toggle's details instance, with the reason none and the click" (`Assert.Same`).
- **TGG-EVT-03** — MUST. Calling `details.cancel()` inside `onValueChange` MUST leave the group value unchanged (component-owned mode) and MUST leave the originating toggle's `aria-pressed` unchanged; the callback still counts as having fired once ([EVD-EVT-05](../architecture/event-details-and-reasons.md)).
  Source: `toggle-group/ToggleGroup.tsx:75-77`; `toggle/Toggle.tsx:98-100`. Test: [L2] `ToggleGroup.test.tsx` "does not change the value when the event is canceled" (`calls.length === 1`, `aria-pressed="false"`).
- **TGG-EVT-04** — MUST. When the originating toggle's `onPressedChange` cancels, `onValueChange` MUST NOT be invoked at all ([EVD-EVT-09](../architecture/event-details-and-reasons.md)).
  Source: `toggle/Toggle.tsx:90-92`. Test: [L2] `Toggle.test.tsx` "canceling in a grouped Toggle prevents the group value from changing".
- **TGG-EVT-05** — MUST. The array passed to `onValueChange` MUST be a new array on every call (never the current value array, never later mutated by the component), so the consumer may store it directly.
  Source: `toggle-group/ToggleGroup.tsx:61-70` (`slice()` / new literal). Test: [L2] `ToolbarButton.test.tsx` "supports a controlled ToggleGroup value" (array stored via `setValue`); identity assertion: none.
  Port note 2026-10-26: the new list per call is asserted at L1 by `ToggleGroupValueTests` "port: every call returns a new list and leaves the current one unchanged"; the cited `ToolbarButton.test.tsx` title is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the cited title runs in `toolbar.spec.ts` "supports a controlled ToggleGroup value" (`@bind-Value`).
- **TGG-EVT-06** — MUST. `onValueChange` MUST fire exactly once per toggle activation, for pointer and keyboard activations alike ([EVD-EVT-03](../architecture/event-details-and-reasons.md)).
  Source: `toggle-group/ToggleGroup.tsx:73`; `toggle/Toggle.tsx:94-96`. Test: [L2] `ToggleGroup.test.tsx` "fires when an Item is clicked" (1 then 2), "fires when the $key is pressed" (Enter, Space) (L4 for the keyboard activation clause).
  Port note 2026-10-26: the keyboard clause runs in `toggle-group.spec.ts` ("fires when the $key is pressed").
- **TGG-EVT-07** — MUST. `onValueChange` MUST NOT fire on mount, when the `value` prop changes, when `multiple`/`disabled` change, or when toggles mount/unmount ([EVD-STA-06](../architecture/event-details-and-reasons.md)).
  Source: `toggle-group/ToggleGroup.tsx:55-81` (only call site is the toggle-driven setter). Test: [L2] `ToggleGroup.test.tsx` "fires when an Item is clicked" (`0` before any click); prop-change case: none.
  Port note 2026-10-26: the prop-change case is asserted by `ToggleGroupTests` "port: a consumer-owned group's click raises OnValueChange and ValueChanged and shows the consumer's next value".
- **TGG-EVT-08** — MUST. `onValueChange` MUST NOT fire for a disabled toggle or while the group is disabled (the click never reaches the toggle's handler).
  Source: `internals/use-button/useButton.ts:104-110`. Test: [L2] `ToolbarButton.test.tsx` "disabled state" (Toggle and ToggleGroup block; `onPressedChange` uncalled implies the group was not reached).
  Port note 2026-10-26: the standalone case is asserted by `ToggleGroupTests` "port: a disabled group's toggles ignore clicks"; the cited `ToolbarButton.test.tsx` title is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the cited title runs in `toolbar.spec.ts` "disabled state" (Toggle and ToggleGroup block: the group's `OnValueChange` is not reached) and, at L2, in `ToggleGroupToolbarTests` "port: a toggle substituted into a toolbar button registers once, with the button's flags, and changes its group's value".
- **TGG-EVT-09** — MUST. `details.allowPropagation()` MUST have no effect (the group never stops the click's propagation).
  Source: `toggle-group/ToggleGroup.tsx:55-81`. Test: none.
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: the group's request carries the toggle's details instance, …" (`AllowPropagation()` called, the value committed as usual).
- **TGG-EVT-10** — MUST. In consumer-owned mode, the group MUST NOT change its rendered value after `onValueChange`; the consumer's next `value` is what the toggles reflect ([EVD-STA-04](../architecture/event-details-and-reasons.md), [EVD-EVT-06](../architecture/event-details-and-reasons.md)).
  Source: `toggle-group/ToggleGroup.tsx:79`; `packages/utils/src/useControlled.ts:83-90`. Test: [L2] `ToolbarButton.test.tsx` "supports a controlled ToggleGroup value".
  Port note 2026-10-26: the standalone case is asserted by `ToggleGroupTests` "port: a consumer-owned group's click raises OnValueChange and ValueChanged and shows the consumer's next value"; the cited `ToolbarButton.test.tsx` title is open to M3 track 4 (Toolbar).
  Port note 2026-10-27 (M3 track 4): the cited title runs in `toolbar.spec.ts` "supports a controlled ToggleGroup value".

## 8. Form integration

Not applicable — ToggleGroup has no `name` prop, renders no hidden input, never serializes its value, and does not register with `Field.Root` or `Form` (its toggles are `type="button"` elements, [TGL-FRM-01](toggle.md)).

## 9. Lifecycle, mounting and animation

Not applicable — the element is always mounted; no transition status or completion callback.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

* TGG-FOC-02, TGG-FOC-04, TGG-EDGE-09 — CMP-STA-10 row (post-commit, pre-paint C# state work): after paint plus a round trip; observable only as latency of the item-map publication, which writes the first tab stop (and the disabled-first re-validation) and makes the tab stop follow a reordered toggle. Narrowed 2026-10-26 (ADR 04 Amendments 2026-10-22: the `composite` module writes every item's `tabindex` from attach; chosen by the user 2026-10-26, M3 plan §Settled): TGG-FOC-03's key-move clause left this entry, because the module writes `tabindex` in the task of the focus move, on both modes (`toggle-group.spec.ts` "port: after an arrow key the focus and the tab stop move in the same task, on both modes").
* TGG-API-09 — the row "SLD-PTR-01 (default-prevented clause), EVD-EVT-26 against composite/button key handling, …" with option (i) (ADR 01 Amendments 2026-10-23; ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-23): a C# consumer veto (`PreventPartHandler()`) in the group's `onkeydown` is synchronous on WebAssembly through the `navigate` decision point; on Server the key is handled regardless (fallback "run") and the group's C# `onkeydown`, with the focused toggle's, runs a round trip later from the module's handled-key report and cannot veto; a consumer JavaScript listener calling `preventDefault()` is honoured on both modes (ADR 04 Amendments 2026-10-18) (`toggle-group.spec.ts` "port: the group's C# onkeydown vetoes a handled arrow on WebAssembly and runs a round trip late on Server").

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `CompositeRoot` (standalone only) | [CMP](../architecture/composite-and-list-navigation.md) | `orientation` = prop (`'horizontal'` default; never `'both'`), `loopFocus` = prop (`true` default), `enableHomeAndEndKeys: true`, `stopEventPropagation: true` (part default), `highlightItemOnHover: false` (default), no `disabledIndices` (DOM detection), no `grid`, no `onLoop`, no `modifierKeys`, no controlled `highlightedIndex`, no `onMapChange`; element tag `div`; props `[{ role: 'group' }, consumerProps]`; direction from `DirectionProvider` |
| `useRenderElement` (in-toolbar only) | [RND](../architecture/rendering-and-composition.md) | default tag `div`; `enabled: Boolean(toolbarContext)`; props `[{ role: 'group' }, consumerProps]`; default state → data-attribute projection (`disabled` → `data-disabled`, `multiple` → `data-multiple`, `orientation` → `data-orientation`) |
| `useControlled` | [UTL](../architecture/shared-utilities.md) | `{ controlled: value, default: defaultValue ?? [], name: 'ToggleGroup', state: 'value' }` |
| `createChangeEventDetails` | [EVD](../architecture/event-details-and-reasons.md) | not created here — the toggle's details object is received and may be canceled |
| `useToolbarRootContext(optional)`, `useToolbarGroupContext` | [TLB](toolbar.md) | read `disabled` for TGG-API-04 and presence for TGG-STR-02 |
| `ToggleGroupContext` (provided) | [TGL](toggle.md) | `{ value, setGroupValue(identity, nextPressed, details), disabled, isValueInitialized }` |
| `useDirection` | UTL | via `CompositeRoot`; [UTL-I18N-01](../architecture/shared-utilities.md) |

## 12. Edge cases, invariants and dev warnings

All messages are emitted only in non-production builds through `console.error`, prefixed `Base UI: `, deduplicated per message text ([UTL-EDGE-11](../architecture/shared-utilities.md), [UTL-EDGE-12](../architecture/shared-utilities.md)).

- **TGG-EDGE-01** — MUST. When the group's value is initialized (TGG-STA-07) and a member Toggle has no `value` prop, the Toggle's diagnostic [TGL-EDGE-01](toggle.md) MUST be logged (verbatim text there); the group itself logs nothing.
  Source: `toggle-group/ToggleGroup.tsx:42-43,90`; `toggle/Toggle.tsx:48-59`. Test: [L2] `ToggleGroup.test.tsx` "should warn if Toggle value is not set and ToggleGroup value is defined".
- **TGG-EDGE-02** — SHOULD. If `value` crosses between `undefined` and an array after the first render, [UTL-EDGE-22](../architecture/shared-utilities.md) SHOULD be logged with `name: 'ToggleGroup'`, `state: 'value'`.
  Source: `toggle-group/ToggleGroup.tsx:48-53`; `packages/utils/src/useControlled.ts:48-64`. Test: none.
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: ownership is fixed at the first parameter set, and a removed Value falls back to the initial default".
- **TGG-EDGE-03** — SHOULD. In component-owned mode, if `defaultValue` later serializes differently (a new array with *different contents*; a new array with the same contents does not count, [UTL-EDGE-24](../architecture/shared-utilities.md)), [UTL-EDGE-23](../architecture/shared-utilities.md) SHOULD be logged with `name: 'ToggleGroup'`, `state: 'value'`; the value is unaffected.
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: none.
  Port note 2026-10-26: asserted by `ToggleGroupTests` "port: a DefaultValue whose entries change after the first parameter set is logged, and a new list with the same entries is not" (`Controlled<T>` compares lists by content, UTL-EDGE-24).
- **TGG-EDGE-04** — MUST. Duplicate identities are not rejected: two toggles sharing a `value` MUST both render pressed while that identity is in the array; unpressing either removes one occurrence (TGG-STA-04), so with a single occurrence both become unpressed, and with `multiple={false}` the array becomes `[]`.
  Source: `toggle-group/ToggleGroup.tsx:62-70`; `toggle/Toggle.tsx:62`. Test: none.
  Port note 2026-10-26: asserted at L1 by `ToggleGroupValueTests` "port: unpressing a duplicated identity removes its first occurrence only".
- **TGG-EDGE-05** — MUST. Identities present in `value`/`defaultValue` that match no toggle MUST be preserved verbatim through TGG-STA-04 updates (multi-select appends/removes around them) and MUST be dropped by the first single-select activation (TGG-STA-03 replaces the whole array).
  Source: `toggle-group/ToggleGroup.tsx:62-70`. Test: none.
  Port note 2026-10-26: asserted at L1 by `ToggleGroupValueTests` "port: a multi-select update keeps unknown identities, and a single-select one drops them".
- **TGG-EDGE-06** — MUST. Inside a `Toolbar.Root`, `orientation` and `loopFocus` MUST have no keyboard effect (TGG-KEY-10) while `data-orientation` still reflects the group's own `orientation` prop (which may differ from the toolbar's).
  Source: `toggle-group/ToggleGroup.tsx:83,99-109`. Test: none directly (`ToggleGroup.test.tsx` "preserves selection and roving focus when nested in Toolbar.Group" exercises navigation by the toolbar).
  Port note 2026-10-27 (M3 track 4): asserted by `ToggleGroupToolbarTests` "port: inside a toolbar the group's Orientation and LoopFocus send nothing, and data-orientation is its own".
- **TGG-EDGE-07** — MUST. A group inside a disabled `Toolbar.Group` or `Toolbar.Root` MUST render all its toggles disabled (native toggles carry the `disabled` attribute, are unfocusable, and are excluded from the toolbar's roving order via their metadata, [TLB-STA-02](toolbar.md)).
  Source: `toggle-group/ToggleGroup.tsx:45-46`; `toggle/Toggle.tsx:46,118-121`. Test: [L2] `ToolbarButton.test.tsx` "disables direct ToggleGroup children when Toolbar.Group is disabled" (L4 for the unfocusable and roving-order clauses).
  Port note 2026-10-27 (M3 track 4): the `[L2]` half is asserted by `ToggleGroupToolbarTests` "port: the direct toggles of a toolbar's group send their disabled state and are not focusable when disabled, also after a change" (natively disabled, sent disabled and not focusable); the unfocusable and roving-order clauses run in `toolbar.spec.ts` "disables direct ToggleGroup children when Toolbar.Group is disabled".
- **TGG-EDGE-08** — MUST. Direction MUST be taken only from the nearest `DirectionProvider` ([UTL-I18N-03](../architecture/shared-utilities.md)); a `dir` attribute on the group or an ancestor MUST NOT affect key mapping.
  Source: `internals/composite/root/CompositeRoot.tsx:43`. Test: [L4] `ToggleGroup.test.tsx` "$direction, orientation: $orientation moves focus with arrow keys" (direction supplied via `DirectionProvider` only).
- **TGG-EDGE-09** — MUST. Server-rendered markup MUST contain the group element with `role`, `data-orientation`, `data-disabled`/`data-multiple` as on the client; toggles render `tabindex="-1"` on the server and the tab stop appears on the client after the first item-map publication ([CMP-STA-15](../architecture/composite-and-list-navigation.md), [TGL-EDGE-08](toggle.md)). Server: relaxed, see §9.
  Source: `internals/composite/list/useCompositeListItem.ts:43-55`; `internals/composite/item/useCompositeItem.ts:19,25`. Test: none.
  Port note 2026-10-26: C#'s `-1` and the group's attributes are asserted at L2 by `ToggleGroupTests` "port: grouped toggles render tabindex -1, which the composite owns from attach, and the group renders none"; the first publication per mode by `toggle-group.spec.ts` "port: grouped toggles render tabindex -1 until the first publication, a round trip later on Server".
- **TGG-EDGE-10** — MUST. Standalone, when every toggle is disabled, the highlighted index MUST be kept (no tab stop is reachable by <kbd>Tab</kbd> for native toggles; non-native disabled toggles keep `tabindex` and remain reachable) ([CMP-STA-24](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/useCompositeRoot.ts:151-161`. Test: none.

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND.

## 13. Behaviors evidenced by tests

### `ToggleGroup.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; default `testRenderPropWith: 'div'`; no `button`) | TGG-DOM-08, TGG-API-08, TGG-API-09 |
| "renders a `group`" | TGG-STR-01, TGG-STR-03, TGG-ARIA-01, TGG-ARIA-03 |
| "pressed state" (uncontrolled; browser only) | TGG-API-02, TGG-STA-03, TGG-STA-06, TGG-PTR-01, [TGL-DOM-02](toggle.md) |
| "prop: defaultValue" | TGG-API-02, TGG-STA-03, TGG-PTR-01 |
| "when Toggles omit value" (uncontrolled) | TGG-STA-03, [TGL-API-05](toggle.md), [TGL-EDGE-05](toggle.md) |
| "should warn if Toggle value is not set and ToggleGroup value is defined" | TGG-STA-07, TGG-EDGE-01, [TGL-EDGE-01](toggle.md) |
| "pressed state" (controlled) | TGG-API-01, TGG-STA-06, TGG-ARIA-04 |
| "prop: value" | TGG-API-01, TGG-STA-06 |
| "can disable the whole group" | TGG-API-04, TGG-ARIA-05, [TGL-API-04](toggle.md), [TGL-ARIA-02](toggle.md) |
| "can disable individual items" | TGG-ARIA-05, [TGL-ARIA-02](toggle.md) (`aria-disabled="false"` on the enabled toggle), [TGL-DOM-03](toggle.md) |
| "vertical" | TGG-API-05, TGG-DOM-02 |
| "does not render aria-orientation on role="group"" | TGG-ARIA-02 |
| "sets data-multiple only when true" | TGG-DOM-04 |
| "multiple items can be pressed when true" | TGG-API-07, TGG-STA-04 |
| "only one item can be pressed when false" | TGG-API-07, TGG-STA-03 |
| "when Toggles omit value" (multiple) | TGG-STA-04, [TGL-API-05](toggle.md) |
| "preserves selection and roving focus when %s" (template, 2 variants: "standalone", "nested in Toolbar.Group"; browser only) | TGG-STR-02, TGG-STA-05, TGG-DOM-04, TGG-DOM-06, TGG-PTR-01, TGG-FOC-03, TGG-FOC-05, TGG-KEY-10 |
| "$direction, orientation: $orientation moves focus with arrow keys" (ltr/horizontal, ltr/vertical, rtl/horizontal, rtl/vertical; browser only) | TGG-API-05, TGG-API-06, TGG-KEY-01, TGG-KEY-02, TGG-KEY-03, TGG-DOM-05, TGG-FOC-01, TGG-FOC-02, TGG-FOC-03, TGG-EDGE-08, TGG-STA-08 |
| "Home key moves focus to the first item" / "End key moves focus to the last item" (browser only) | TGG-KEY-04, TGG-FOC-01 |
| "key: $key toggles the pressed state" (Enter, Space; browser only) | TGG-KEY-09, TGG-FOC-03, [TGL-KEY-02](toggle.md) |
| "fires when an Item is clicked" | TGG-API-03, TGG-EVT-01, TGG-EVT-06, TGG-EVT-07 |
| "does not change the value when the event is canceled" | TGG-EVT-03, [TGL-EVT-05](toggle.md) |
| "fires when the $key is pressed" (Enter, Space; browser only) | TGG-EVT-06, TGG-KEY-09 |

### `ToggleGroup.spec.tsx` (type-level)

| Check | Requirements |
|---|---|
| `defaultValue` and `value` accept `readonly string[]`; `onValueChange` receives `string[]`; a `const` tuple narrows `Value` | §3.2 shared types (no runtime requirement) |

### Tests in other files that exercise ToggleGroup

| Test | Requirements |
|---|---|
| `Toggle.test.tsx` "canceling in a grouped Toggle prevents the group value from changing" | TGG-EVT-04 |
| `Toggle.test.tsx` "should pass composite props" | TGG-STR-02, TGG-FOC-01 |
| `ToolbarButton.test.tsx` "renders toggle and toggle group", "handles interactions", "disabled state" (Toggle and ToggleGroup block) | TGG-STR-02, TGG-EVT-08 |
| `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children" | TGG-STR-02, TGG-STA-03, TGG-EVT-01, TGG-KEY-10, TGG-FOC-05 |
| `ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children", "skips a direct Toggle that becomes disabled at runtime" | TGG-KEY-08 (toolbar variant), [TLB-STA-02](toolbar.md) |
| `ToolbarButton.test.tsx` "supports multiple selection for direct ToggleGroup > Toggle children" | TGG-STA-04 |
| `ToolbarButton.test.tsx` "supports a controlled ToggleGroup value" | TGG-API-01, TGG-EVT-05, TGG-EVT-10 |
| `ToolbarButton.test.tsx` "disables direct ToggleGroup children when Toolbar.Group is disabled" | TGG-API-04, TGG-EDGE-07 |

### Additional requirements introduced here

- **TGG-DOM-08** — MUST. ToggleGroup MUST pass the shared conformance suite from RND ([RND-DOM-36](../architecture/rendering-and-composition.md) … [RND-DOM-48](../architecture/rendering-and-composition.md)) standalone: custom attributes forwarded to the default `<div>` and to substituted elements (function and element forms), `style` merging, DOM handle resolving to an `HTMLDivElement`, wrapped substitutes, `className` string/function merging. The `button` option is not set, so no non-native re-run applies.
  Source: `test/describeConformance.tsx:55-63`. Test: [L2] `ToggleGroup.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Two render paths.** Standalone the group *is* a `CompositeRoot` (which renders the element through the generic pipeline and wraps it in the item-registry and composite-root shared state); inside a toolbar the generic pipeline is used directly with `enabled: Boolean(toolbarContext)` — both are always called to keep hook order stable, and the unused one renders nothing ([RND-DOM-25](../architecture/rendering-and-composition.md)). A port needs one element plus a flag "am I inside a toolbar" that decides whether to instantiate the roving-group behavior. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 4 (`Enabled = false` renders nothing) and decisions/04-js-interop-boundary.md §1 (`composite` module; C# receives active-index changes).
- **Shared state object.** The context value `{ disabled, setGroupValue, value, isValueInitialized }` is memoized on those four; toggles re-render when any changes. `setGroupValue` is a stable callback that reads the *latest* `groupValue`, `multiple` and `onValueChange` at call time, so a toggle clicked right after a `multiple` flip uses the new rule. Port: see decisions/05-state-store-and-event-details.md §1 (simple components use `Controlled<T>`; ancestor state cascaded as a fixed value).
- **`EMPTY_ARRAY` default.** `defaultValue ?? EMPTY_ARRAY` uses a shared frozen-by-convention empty array so the default-change diagnostic (TGG-EDGE-03) does not fire spuriously for an omitted `defaultValue`; a consumer passing a fresh `[]` literal each render is also safe because comparison is by serialization ([UTL-ALG-08](../architecture/shared-utilities.md)). Port: see decisions/05-state-store-and-event-details.md §2 (`Value`/`ValueChanged`/`DefaultValue`; controlled iff supplied at the first parameter set).
- **Controlled arrays are not copied.** `groupValue` is the consumer's array object; `indexOf` is evaluated against it on every toggle render. A port should likewise avoid defensive copies on the read path and copy only when computing the next value (TGG-EVT-05). Port: see decisions/05-state-store-and-event-details.md §2.
- **Value typing.** `value`/`defaultValue` are `readonly Value[]` so `as const` tuples type-check; the callback hands back a mutable `Value[]` (`ToggleGroup.spec.tsx`). Ports with nominal array types should mirror the read-only input / mutable output split or document the deviation.
- **Direction.** `CompositeRoot` reads `useDirection()`; ToggleGroup forwards nothing direction-related. Under React the `DirectionProvider` value also reaches portaled subtrees ([UTL-I18N-04](../architecture/shared-utilities.md)). Port: see decisions/04-js-interop-boundary.md §5 (portals move the DOM after first render; the component tree, and so the cascaded direction, is unchanged).
- **Key handling timing.** The composite root's keydown handler runs on the group element (bubbling from the toggle) and focuses the target in a `queueMicrotask` ([CMP-KEY-10](../architecture/composite-and-list-navigation.md)); `tabindex` updates in the same commit. Tests assert both after `await user.keyboard(...)`, which flushes microtasks. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: roving focus, arrow-key navigation and per-key `preventDefault` in JS; C# receives active-index changes) and decisions/01-hosting-model-and-timing.md §Decision item 4 (microtask is a JS-only term) and §Relaxation table (CMP-STA-10).
- **Toolbar coupling.** ToggleGroup imports the toolbar's root and group contexts directly (`ToggleGroup.tsx:9-10`); there is no abstraction — the port must give ToggleGroup read access to the toolbar's `disabled` and presence.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TGG-STR-01 | MUST | One `<div>`; any Toggle in subtree is a member | 2 |
| TGG-STR-02 | MUST | Composite root standalone; plain element inside Toolbar | 2 |
| TGG-STR-03 | MUST | Empty group renders, no tab stop | 2 |
| TGG-API-01 | MUST | `value` defined first render → consumer-owned, array used as-is | 3.1 |
| TGG-API-02 | MUST | initial value `defaultValue` or `[]` | 3.1 |
| TGG-API-03 | MUST | `onValueChange(nextArray, sharedDetails)` | 3.1 |
| TGG-API-04 | MUST | disabled = toolbar OR toolbar group OR prop; pushed to toggles | 3.1 |
| TGG-API-05 | MUST | `orientation` default `'horizontal'`, two values | 3.1 |
| TGG-API-06 | MUST | `loopFocus` default `true`; inert in toolbar | 3.1 |
| TGG-API-07 | MUST | `multiple` default `false`; `toggleMultiple` gone | 3.1 |
| TGG-API-08 | MUST | `className`/`style`/`render` per RND, both paths | 3.1 |
| TGG-API-09 | MUST | other props forwarded; consumer `role` wins; handler veto | 3.1 |
| TGG-STA-01 | MUST | state object exactly `disabled`, `multiple`, `orientation` | 3.1 |
| TGG-STA-02 | MUST | ownership fixed on first render; fallback to default | 4 |
| TGG-STA-03 | MUST | single-select: `[v]` / `[]`, last may be deselected | 4 |
| TGG-STA-04 | MUST | multi-select: append / remove first occurrence, no mutation | 4 |
| TGG-STA-05 | MUST | `multiple` change does not normalize value | 4 |
| TGG-STA-06 | MUST | commit only when component-owned | 4 |
| TGG-STA-07 | MUST | `isValueInitialized` flag for Toggle warning | 4 |
| TGG-STA-08 | MUST | tab stop = highlighted index, initial 0, no active marker | 4 |
| TGG-DOM-01 | MUST | default `<div>`; substitute gets everything | 5.1 |
| TGG-DOM-02 | MUST | `data-orientation` always | 5.1 |
| TGG-DOM-03 | MUST | `data-disabled` iff disabled | 5.1 |
| TGG-DOM-04 | MUST | `data-multiple` iff multiple, live | 5.1 |
| TGG-DOM-05 | MUST | no `tabindex` on group element | 5.1 |
| TGG-DOM-06 | MUST | composite handlers only standalone | 5.1 |
| TGG-DOM-07 | MUST | consumer attributes override built-ins | 5.1 |
| TGG-DOM-08 | MUST | passes RND conformance suite | 13 |
| TGG-ARIA-01 | MUST | `role="group"` | 5.2 |
| TGG-ARIA-02 | MUST | no `aria-orientation` | 5.2 |
| TGG-ARIA-03 | MUST | no generated accessible name | 5.2 |
| TGG-ARIA-04 | MUST | toggles' `aria-pressed` reflect membership; nothing else added | 5.2 |
| TGG-ARIA-05 | MUST | grouped toggles: `focusableWhenDisabled` unspecified → native `aria-disabled="false"`/`disabled`+`"true"` matrix per UBT | 5.2 |
| TGG-KEY-01 | MUST | orientation arrow keys move tab stop, wrap with loopFocus | 6.1 |
| TGG-KEY-02 | MUST | RTL swaps horizontal keys | 6.1 |
| TGG-KEY-03 | MUST | cross-axis keys not handled, not consumed | 6.1 |
| TGG-KEY-04 | MUST | Home/End to first/last enabled | 6.1 |
| TGG-KEY-05 | MUST | `loopFocus=false` stops at ends | 6.1 |
| TGG-KEY-06 | MUST | handled key: preventDefault + stopPropagation; focus next microtask | 6.1 |
| TGG-KEY-07 | MUST | modifier keys ignored | 6.1 |
| TGG-KEY-08 | MUST | DOM-disabled toggles skipped | 6.1 |
| TGG-KEY-09 | MUST | Enter/Space toggle via Toggle (Space keydown) | 6.1 |
| TGG-KEY-10 | MUST | inside Toolbar: no key handling of its own | 6.1 |
| TGG-PTR-01 | MUST | click updates value and tab stop | 6.2 |
| TGG-PTR-02 | MUST | disabled group ignores clicks | 6.2 |
| TGG-PTR-03 | MUST | hover never focuses | 6.2 |
| TGG-FOC-01 | MUST | single tab stop, roving tabindex | 6.3 |
| TGG-FOC-02 | MUST | initial tab stop first enabled toggle | 6.3 |
| TGG-FOC-03 | MUST | focus moves tab stop; key move focuses after microtask | 6.3 |
| TGG-FOC-04 | MUST | tab stop follows element across list changes | 6.3 |
| TGG-FOC-05 | MUST | inside Toolbar: no own tab stop | 6.3 |
| TGG-EVT-01 | MUST | callback after toggle's, before commit, with next array | 7 |
| TGG-EVT-02 | MUST | details object shared with toggle; reason `'none'` | 7 |
| TGG-EVT-03 | MUST | `cancel()` → no change | 7 |
| TGG-EVT-04 | MUST | toggle cancel → group callback not invoked | 7 |
| TGG-EVT-05 | MUST | fresh array each call | 7 |
| TGG-EVT-06 | MUST | exactly once per activation | 7 |
| TGG-EVT-07 | MUST | not fired on mount / prop changes | 7 |
| TGG-EVT-08 | MUST | not fired when disabled | 7 |
| TGG-EVT-09 | MUST | `allowPropagation()` no effect | 7 |
| TGG-EVT-10 | MUST | consumer-owned: render consumer's next value | 7 |
| TGG-EDGE-01 | MUST | Toggle missing-value diagnostic gated by group | 12 |
| TGG-EDGE-02 | SHOULD | controlled/uncontrolled switch diagnostic | 12 |
| TGG-EDGE-03 | SHOULD | `defaultValue` change diagnostic | 12 |
| TGG-EDGE-04 | MUST | duplicate identities behavior | 12 |
| TGG-EDGE-05 | MUST | unknown identities preserved (multi) / dropped (single) | 12 |
| TGG-EDGE-06 | MUST | in toolbar: `orientation`/`loopFocus` inert, data attr kept | 12 |
| TGG-EDGE-07 | MUST | disabled Toolbar.Group/Root disables toggles | 12 |
| TGG-EDGE-08 | MUST | direction from provider only | 12 |
| TGG-EDGE-09 | MUST | SSR markup; `tabindex=-1` pre-publication | 12 |
| TGG-EDGE-10 | MUST | all-disabled: highlighted index kept | 12 |

## 16. Open questions

1. **Deselecting the last toggle in single-select mode.** `toggle-group/ToggleGroup.tsx:70` yields `[]` when the pressed toggle is pressed again; no test pins this and the experiment (`experiments/toggle-group.tsx:16-21`) works around it in `onValueChange`. The assignment asked whether the last one can be deselected: yes. Confirm the port should keep this (radio-like "always one" is a consumer policy).
   Resolved 2026-10-26 (chosen by the user 2026-10-26, M3 plan §Settled: mirror the pinned source): kept; `ToggleGroupValue.Next` gives `[]` (TGG-STA-03's note).
2. **Initial tab stop vs pressed toggle.** Standalone, the first toggle gets the tab stop even when another toggle is initially pressed (TGG-STA-08) because ToggleGroup does not set `data-composite-item-active` (compare Tabs/RadioGroup, [CMP-DOM-02](../architecture/composite-and-list-navigation.md)). No test covers a pressed non-first toggle. Decide whether the port should match or prefer the pressed toggle.
   Resolved 2026-10-26 (chosen by the user 2026-10-26, M3 plan §Settled: mirror the pinned source): matched; the group sets no active-item marker (TGG-STA-08's note).
3. **No `disabledIndices` standalone.** The standalone group relies on DOM detection (`floating-ui-react/utils/composite.ts:550-553`), while the same group inside a toolbar relies on metadata-derived `disabledIndices` from `Toolbar.Root` (which also still honours `:disabled`). The two agree for native toggles; for a non-native disabled toggle (`aria-disabled="true"`, no `disabled` attribute) the standalone group skips it, and the toolbar also skips it (metadata `focusableWhenDisabled: false`). No test covers non-native toggles in groups.
   Resolved 2026-10-26 (settled by the orchestrator 2026-10-26 under the M3 plan's rule "§16 port choices mirror the pinned source", accepted by the user 2026-10-26): mirrored; the standalone group sends no `disabledIndices` and the module reads the document (TGG-KEY-08's note). The toolbar's metadata path is M3 track 4's.
4. **Nested ToggleGroups** (`toggle-group/ToggleGroup.tsx:107`): an inner group re-provides the shared state and, standalone, nests a second composite root. Behavior is undefined by tests; treat as unsupported.
   Resolved 2026-10-26 (settled by the orchestrator 2026-10-26 under the same rule; the RDG-EDGE-08 precedent): unsupported and documented, with no guard: an inner group re-cascades its own context and composite root, which then captures the arrow keys first.
5. **`data-orientation` inside a toolbar** reflects the group's own prop (default `'horizontal'`) even though navigation uses the toolbar's orientation (TGG-EDGE-06). Possibly misleading for styling a vertical toolbar; documented as observed.
   Resolved 2026-10-26 (settled by the orchestrator 2026-10-26 under the same rule): kept as observed; `data-orientation` reflects the group's own `Orientation` in a toolbar too (TGG-EDGE-06), built and tested by M3 track 4 (Toolbar).
6. **`toggleMultiple` hint.** Not present in 1.9.0 (renamed to `multiple`, CHANGELOG v1.0.0-beta.4). Documented in TGG-API-07.
7. **`aria-disabled="false"` on enabled grouped toggles** — see [toggle](toggle.md) §16 Q2; asserted by `ToggleGroup.test.tsx` "can disable individual items". Resolved (QA 2026-10-14): the full grouped-toggle `focusableWhenDisabled` matrix (native/non-native × enabled/disabled) is now TGG-ARIA-05, citing button-behavior UBT Q2/Q4.
