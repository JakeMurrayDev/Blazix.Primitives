# <Component Name>

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/<dir>/` |
| Tests | `packages/react/src/<dir>/**/*.test.tsx` (<n> files) |
| Docs page | `docs/src/app/(docs)/react/components/<dir>/page.mdx` |
| Prefix | `XXX` |
| Status | draft |
| Builds on | <none \| link to base component doc, e.g. [dialog](dialog.md)> |
| Derived by | <none \| component docs that build on this one, e.g. [accordion](accordion.md)> |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md) (if the component has change callbacks), <add each other architecture doc actually used: button-behavior, field-and-form-infrastructure, composite-and-list-navigation, floating-and-positioning, popup-store-and-handles, transitions-and-mounting, shared-utilities> |

<!--
Authoring notes (delete this comment block in the finished doc):
- Follow docs/conventions.md. Keep every heading below, in this order, with these numbers.
- Write "Not applicable — <one sentence>" under any section that genuinely does not apply.
- Every normative statement is a requirement bullet in the fixed format with Source and Test lines.
- Explanatory prose is welcome but carries no ID.
- Tables in §3 *reference* IDs in their Req column; the normative bullets for data attributes and ARIA live in §5 only. Never state the same requirement twice.
- The orchestrator's assignment may list props/behaviors as hints; the source is authoritative. If a hinted prop does not exist, say so in §16.
-->

## 1. Overview

One or two paragraphs: what the component is for, what the user experiences, how it differs from similar components in the library. List derived components (those that build on this one) and the base component (if this one is derived).

Paired components (Checkbox/CheckboxGroup, Radio/RadioGroup, Toggle/ToggleGroup, Menu/Menubar) state the **composition contract**: which document owns the member-side rules and which owns the group-side rules, so each stands alone and cross-cites the other.

## 2. Anatomy

Part tree in the order parts are nested. For each part: required/optional, cardinality, allowed parents, default rendered element.

```
<Root>                 required, 1          default element: div
  <Trigger>            optional, 0..n       default element: button
  <Portal>             …
    <Positioner>
      <Popup>
```

- **XXX-STR-01** — MUST. …

Derived docs add a **Re-exported parts** sub-section in §3 (one table: part, base section, changed defaults) and **2.1 Adoption from base**: one short adopting requirement plus two ID-less tables, *Not adopted* and *Overridden* (base ID → replacing ID), and a substitution table for names that differ.

A structural-only part with no props of its own may replace its §3 sub-section body with "Props: none beyond the common props. State/attributes: as §3.1."

Also state: which parts are "structural only" (render an element with state attributes but no behavior), which parts may be omitted and what happens when they are (e.g. "if Header is omitted, Trigger must be a direct child of Item").

## 3. Public API

One subsection per part, in anatomy order. Then a subsection for shared types.

### 3.1 `<Component>.<Part>`

**Default element:** `<tag>`. **Accepts `render` element substitution:** yes/no. **Accepts `className`/`style` as function of state:** yes/no. An element-less part (render-function-only, e.g. `Field.Validity`) states "Renders no element" and omits the data-attribute/CSS-variable tables. A part whose state is the root's plus a few fields may say "Inherits §3.1 state with: …".

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value[]` | `[]` (uncontrolled) | … | XXX-API-01 |

Include `className`, `style`, `render` only once (reference RND doc) unless the part treats them specially. Include every other prop, including ones inherited from a base component, and native attributes the part intercepts or treats specially (e.g. `id`, `disabled`, `hidden`, `name`); note when the API outline omits them.

**Consumed (not forwarded to the element):** `list of props`.

- **XXX-API-01** — MUST. …

#### State object

The object passed to `className`/`style`/`render` functions and used to derive data attributes.

| Field | Type | Meaning |
|---|---|---|

- **XXX-STA-NN** — MUST. The state object exposed to `className`/`style`/`render` contains exactly the fields above. (This is the one STA bullet that lives in §3. When several parts share one state object, later parts may say "State object, data attributes and CSS variables: as §3.1". Multi-part components may consolidate the per-part state-object requirements into one bullet listing every part's fields.)
  Source: `…`. Test: `…`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` | present while open | XXX-DOM-01 |

When several parts share one attribute mapping, define it once (one DOM ID) and reference that ID from each part's table.

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|

### 3.N-2 Imperative API and non-part exports (if any)

Managers, handles, hooks and plain functions the component exports (`useToastManager`, `createToastManager`, `Dialog.createHandle`, `Combobox.useFilter`): signature, return shape, lifecycle, and the requirements that govern them. Cite PSH for shared handle semantics.

### 3.N-1 Base-component parameters overridden by this component (derived docs only)

| Base part / prop | Base default or meaning | Under this component |
|---|---|---|

### 3.N Shared types and functions

`Value`, `ChangeEventReason` (list every literal), `ChangeEventDetails`, `Orientation`, etc. Reference EVD doc for the common shape.

## 4. State model

- Controlled/uncontrolled pairs (`open`/`defaultOpen` …): ownership rules, what happens when the consumer rejects a change (controlled prop not updated), initial value precedence.
- Derived state and how it is computed.
- Value normalization (e.g. `multiple` → array; clamping; step rounding).
- State diagram (text) for open/close with transition statuses if the part mounts/unmounts. The diagram may reference §9 IDs; do not duplicate §9's requirements here.
- Invariants (e.g. "at most one item open unless `multiple`").
- Component-owned value algorithms (parsing, formatting, step rounding, clamping, value↔position mapping) go in a `### 4.1 Value algorithms` sub-section here, using STA IDs, or I18N IDs for locale-dependent formatting/parsing. Component-owned geometry procedures (indicator sizing, aligned positioning) may use ALG IDs per conventions §3.3.
- Field-derived state (`dirty`, `touched`, `filled`, `focused`, `valid`): define the *value semantics* here (STA); the attributes go in §5 (DOM); *when the component writes to the Field* goes in §8 (FRM); `focused` tracking goes in §6.3 (FOC).

- **XXX-STA-01** — MUST. …

## 5. Rendered DOM and ARIA

Both the DOM and ARIA requirement sequences live in this section. Use one sub-heading per rendered element, then one cross-part ARIA sub-heading:

### 5.1 `<Part>` element

Element, `tabindex`, `hidden`/`inert`, every data attribute (normative bullets here; §3 tables only reference them), attribute precedence when the consumer passes the same attribute.

| Attribute | Value / present when | Req |
|---|---|---|

- **XXX-DOM-01** — MUST. …

### 5.2 Hidden `<input>` (if any)

One sub-section per additional element the part renders (hidden inputs, wrappers, portals, live regions): type, name, value, when rendered, which attributes are mirrored onto it.

### 5.N ARIA

`role`, every `aria-*` attribute and what it is derived from, `id` generation and cross-references (`aria-controls`, `aria-labelledby`, `aria-describedby`, `aria-activedescendant`), label precedence (`aria-label` vs `aria-labelledby` vs Field label).

- **XXX-ARIA-01** — MUST. …

## 6. Interactions

### 6.1 Keyboard

| Key | Context (which part focused, modifiers, orientation/RTL) | Behavior | Req |
|---|---|---|---|

When the behavior is entirely delegated to a shared mechanism, add a `Delegated` column naming the architecture ID (e.g. `UBT-KEY-03`) and write component bullets only for the component's configuration.

Include unhandled keys that matter (Tab). Escape and outside-press rows reference the DIS IDs defined in §6.4 rather than defining duplicates; tables may reference IDs from any subsection. State `preventDefault`/propagation behavior where relevant (e.g. "Arrow keys call preventDefault to suppress page scroll").

- **XXX-KEY-01** — MUST. …

### 6.2 Pointer and touch

Press vs click semantics, pointer type differences (mouse/touch/pen), hover open/close timing, drag/swipe, long-press, right-click, text-selection suppression. A gesture table in the same shape as the keyboard table is recommended:

| Gesture | Context (part, pointer type, modifiers, state) | Behavior | Req |
|---|---|---|---|

- **XXX-PTR-01** — MUST. …

### 6.3 Focus management

Initial focus on open, focus return on close, roving tabindex, focus trap/modality, `tabindex` values, behavior when focused element is removed.

- **XXX-FOC-01** — MUST. …

### 6.4 Dismissal

Outside press, Escape, scroll, window blur, ancestor close, `disablePointerDismissal`-style options, nested-popup precedence.

- **XXX-DIS-01** — MUST. …

### 6.N Gesture and keyboard algorithms (optional)

Component-owned interaction procedures (snap-point release, gesture arbitration, keyboard-viewport detection) as language-neutral pseudo-code with ALG IDs. A large provider-like sub-mechanism (e.g. Drawer's VirtualKeyboardProvider) may get its own nested sub-section here.

### 6.5 Reading direction (optional)

Also the home for A11Y-area requirements (focus-visible heuristics, screen-reader-specific branches) when the component has any; use a `### 6.6 Accessibility heuristics` sub-heading.

RTL/LTR behavior not already covered by the keyboard table: which keys flip, which measurements mirror, how direction is resolved. Uses I18N IDs.

## 7. Events and callbacks

For every callback prop: signature; exact moment fired relative to state update and DOM update; complete list of `reason` values with the user action that produces each; cancel semantics (`eventDetails.cancel()`), `allowPropagation()`; whether it fires for programmatic/controlled changes; whether it fires when the value is unchanged.

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|

- **XXX-EVT-01** — MUST. …

## 8. Form integration

`name`, `value` serialization (hidden input or native control), `form` attribute, `required`, `disabled`, `readOnly`, validation (`validate`, `validationMode`, native constraint validation), Field state attributes (`data-dirty`, `data-touched`, `data-filled`, `data-invalid`, `data-valid`), form reset handling, label association, `aria-describedby` from Field.Description/Error. Reference FFI doc for the mechanism.

- **XXX-FRM-01** — MUST. …

## 9. Lifecycle, mounting and animation

Define any timing/phase vocabulary used below before the first requirement, using the canonical terms from conventions §4 ("post-commit, pre-paint", "next frame", …).

Mount/unmount timing, `keepMounted`, transition status data attributes (`data-starting-style`, `data-ending-style`), how animation completion is detected, `onOpenChangeComplete`, `hidden="until-found"` behavior, `actionsRef`/imperative handles. Reference TRN doc for the mechanism.

- **XXX-ANI-01** — MUST. …

## 10. Positioning

Floating parts only. Positioner props (`side`, `align`, `sideOffset`, `alignOffset`, `anchor`, `collisionBoundary`, `collisionPadding`, `collisionAvoidance`, `sticky`, `arrowPadding`, `positionMethod`, `trackAnchor`, …), CSS variables (`--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`, …), data attributes (`data-side`, `data-align`, `data-anchor-hidden`), arrow part behavior. Reference FLT doc; state only component-specific defaults and overrides.

- **XXX-POS-01** — MUST. …

## 11. Cross-cutting dependencies

Table of shared mechanisms used and the configuration this component passes.

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useButton` | [button-behavior](../architecture/button-behavior.md) | `native: props.nativeButton`, `disabled: …`, `focusableWhenDisabled: true` |

State also the mechanism options deliberately *not* passed (defaults relied on), either inline in the configuration cell or in a `Defaults relied on` column.

## 12. Edge cases, invariants and dev warnings

Every `warn(...)`, `console.error`, thrown `Error`, and dev-only check that is *specific to this component* (quote the message verbatim). Messages that come from a shared mechanism (`useControlled`, `useButton`, …) are quoted once in the owning architecture doc; here, cite that doc's ID and give only the substituted parameters (e.g. `name: 'Switch', state: 'checked'`). Dev-only diagnostics are SHOULD unless a test asserts them. Behavior under `disabled`/`readOnly`. Nested instances. RTL. Server rendering / pre-hydration script. Behavior with zero items, duplicate values, missing required parts.

- **XXX-EDGE-01** — MUST. …

## 13. Behaviors evidenced by tests

Two parts, per test file:

1. **Coverage matrix (mandatory).** Parameterized blocks (`describe.for`, `describe.each`, `it.each`) are listed once with the variant count, e.g. `"opens on click" (×3: contained, detached, multiple)`. The `describeConformance` suite row may cite `RND-DOM-36..49` directly without a local ID. A table mapping every test title (and every shared conformance suite invoked, with its options) to the IDs it exercises. Use `/` to group tests with identical mapping. This is the QA artifact that proves brief step 5 was done. The per-ID `Test:` line is authoritative; the matrix is a derived view — if they disagree, fix the `Test:` line.
2. **Requirements that exist only because of a test.** New IDs use the existing area codes (DOM, KEY, ANI, …) and continue that area's sequence. Do not invent a TST area.

### `<File>.test.tsx`

| Test | Requirements |
|---|---|

### Tests in other files that exercise this component (optional)

| File / test | Requirements |
|---|---|

- **XXX-KEY-07** — MUST. … Source: … Test: `…test.tsx` "…".

## 14. React-specific notes and porting considerations

Non-normative. Things that exist only because of React (StrictMode double-invocation guards, hydration, ref timing, effect ordering, `React.memo` boundaries, store subscriptions) and what a port should do instead. Note any behavior that depends on React 17 vs 18/19.

## 15. Requirement index

Every ID defined in this document, grouped by AREA in the order of conventions §3.3 and numeric within each area (this makes gaps auditable). The Section column uses section numbers (`5.2`, `9`).

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|

## 16. Open questions

Ambiguities, suspected bugs, undocumented behaviors, and places where the source and docs disagree. Each with a file:line.

Derived docs add a sub-heading **Base-doc discrepancies — to be fixed in `<base>.md`** listing each base requirement ID the source contradicts, so the orchestrator can route the fix.
