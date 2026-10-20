# Separator

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/separator/` |
| Tests | `packages/react/src/separator/**/*.test.tsx` (1 file) |
| Docs page | `docs/src/app/(docs)/react/components/separator/page.mdx` |
| Prefix | `SEP` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md) |

## 1. Overview

Separator is a single-part, purely structural component: it renders one element that assistive technology announces as a WAI-ARIA `separator` (a static, non-focusable divider between groups of content). The user sees whatever the consumer's styles draw (typically a 1px line); the component itself contributes no visuals and no interaction. The only public parameter beyond the common rendering parameters is `orientation`, which is reflected both as `aria-orientation` and as `data-orientation` so that one stylesheet can draw horizontal and vertical rules.

Separator differs from the internal `ListboxSeparator` used by `Select.Separator`, `Combobox.Separator` and `Autocomplete.Separator`, which renders `role="presentation"` and no `aria-orientation` (RND-ARIA-02); it differs from `Toolbar.Separator` and `Menu.Separator`, which are separate parts documented in their own component docs.

No component in the library builds on this Separator, and it builds on no other component.

## 2. Anatomy

```
<Separator>            required, 1          default element: div
```

- **SEP-STR-01** — MUST. `Separator` MUST render exactly one host element (default `<div>`) and no other elements; it has no sub-parts, no required ancestor and no ancestor-provided shared state, so it may be placed anywhere.
  Source: `separator/Separator.tsx:12-27`. Test: [L2] `Separator.test.tsx` "renders a div with the `separator` role".

The part is structural only: it emits attributes derived from its parameters and attaches no event handlers.

## 3. Public API

### 3.1 `Separator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Orientation of the separator; drives `aria-orientation`, `data-orientation` and the state object. | SEP-API-01 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) §3.0 | — | Standard element customization; functions receive the state object below. | SEP-API-02 |
| `ref` | DOM handle | — | Receives the host element (`HTMLDivElement` by default). | SEP-API-02 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the host element; same-named attributes override the built-in ones (SEP-DOM-02). | SEP-API-03 |

**Consumed (not forwarded to the element):** `orientation`, `className`, `style`, `render`.

- **SEP-API-01** — MUST. `orientation` accepts `'horizontal'` or `'vertical'`; when it is omitted or `undefined` the effective orientation MUST be `'horizontal'`.
  Source: `separator/Separator.tsx:16,29-35`, `internals/types.ts:97`. Test: [L2] `Separator.test.tsx` "sets aria-orientation="$orientation"" (both values). Docs: CHANGELOG.md v1.0.0-alpha.5 "Support vertical orientation".
- **SEP-API-02** — MUST. `className`, `style`, `render` and `ref` MUST behave per RND-API-01…RND-API-05, with the state object of §3.1 State object passed to the function forms; none of the three customization parameters is forwarded as a plain attribute.
  Source: `separator/Separator.tsx:16,20-24`. Test: [L2] `Separator.test.tsx` conformance suite "Base UI component API".
- **SEP-API-03** — MUST. Every parameter other than those in the consumed list MUST be forwarded to the host element; `orientation` itself MUST NOT appear as an attribute named `orientation`.
  Source: `separator/Separator.tsx:16,23`. Test: [L2] `Separator.test.tsx` conformance suite "Base UI component API" (custom `data-*`/`lang` forwarded).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | The effective orientation (SEP-API-01). |

- **SEP-STA-01** — MUST. The state object exposed to `className`/`style`/`render` MUST contain exactly the field `orientation`, equal to the effective orientation.
  Source: `separator/Separator.tsx:18,37-42`. Test: none.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; `"horizontal"` or `"vertical"` | SEP-DOM-01 |

#### CSS variables

None.

### 3.2 Shared types

- `Separator.Props` (`SeparatorProps`) — the prop table above. Source: `separator/Separator.tsx:29-35`.
- `Separator.State` (`SeparatorState`) — `{ orientation: Orientation }`. Source: `separator/Separator.tsx:37-42`.
- `Orientation` — `'horizontal' | 'vertical'` (shared library type).
- `SeparatorDataAttributes` — exported constant namespace with the single member `orientation = 'data-orientation'`. Source: `separator/SeparatorDataAttributes.ts:1-5`, `separator/index.ts:5`.

No callback types exist; Separator has no event-details callbacks (see [EVD](../architecture/event-details-and-reasons.md) §3.6).

## 4. State model

Separator owns no state. The state object is a pure function of the `orientation` parameter (SEP-STA-01); there is no consumer-owned/component-owned pair, no derived value beyond the default, no normalization (SEP-EDGE-01), and no mount/unmount lifecycle.

## 5. Rendered DOM and ARIA

### 5.1 Host element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"separator"` (consumer may override) | SEP-ARIA-01 | C# |
| `aria-orientation` | always; equals effective `orientation` | SEP-ARIA-02 | C# |
| `data-orientation` | always; equals effective `orientation` | SEP-DOM-01 | C# |
| `tabindex` | never emitted by the component | SEP-FOC-01 | C# |

- **SEP-DOM-01** — MUST. The host element MUST carry `data-orientation` whose value is the effective orientation string (`"horizontal"` by default, `"vertical"` when `orientation="vertical"`), produced by the default state-attribute projection (RND-DOM-03, RND-DOM-05).
  Source: `separator/Separator.tsx:18,20-22` (no custom mapping), `internals/getStateAttributesProps.ts:24-28`. Test: none. Docs: `components/separator/types.md` "Separator Data Attributes".
- **SEP-DOM-02** — MUST. Attributes supplied by the consumer MUST take precedence over the built-in `role`, `aria-orientation` and `data-orientation` (the built-in attributes are merged first and the consumer's attributes last, per RND-DOM-10 and RND-DOM-19).
  Source: `separator/Separator.tsx:23`. Test: none.

### 5.2 ARIA

- **SEP-ARIA-01** — MUST. The host element MUST carry `role="separator"` unless the consumer supplies a `role`.
  Source: `separator/Separator.tsx:23`. Test: [L2] `Separator.test.tsx` "renders a div with the `separator` role".
- **SEP-ARIA-02** — MUST. The host element MUST always carry `aria-orientation` equal to the effective orientation — `"horizontal"` included, even though that is the ARIA default for the `separator` role; the attribute is never omitted.
  Source: `separator/Separator.tsx:16,23`. Test: [L2] `Separator.test.tsx` "sets aria-orientation="$orientation"" (`horizontal` and `vertical` cases).

No `aria-valuenow`/`aria-valuemin`/`aria-valuemax` are emitted: the separator is the static (non-focusable) variant of the role, not a focusable splitter. No `id` is generated.

## 6. Interactions

### 6.1 Keyboard

Not applicable — the component attaches no keyboard handlers and is not focusable (SEP-FOC-01).

### 6.2 Pointer and touch

Not applicable — the component attaches no pointer handlers.

### 6.3 Focus management

- **SEP-FOC-01** — MUST. The component MUST NOT emit `tabindex` or any other attribute that makes the host element focusable; it is skipped by sequential focus navigation unless the consumer adds a `tabindex`.
  Source: `separator/Separator.tsx:23` (only `role` and `aria-orientation` are part-supplied). Test: none.

### 6.4 Dismissal

Not applicable — no dismissable surface.

## 7. Events and callbacks

Not applicable — Separator has no callback parameters; consumer DOM handlers are forwarded unchanged (SEP-API-03).

## 8. Form integration

Not applicable — Separator is not a form control.

## 9. Lifecycle, mounting and animation

Not applicable — the element is always rendered; there is no transition status.

**Server relaxations:** none.

## 10. Positioning

Not applicable — not a floating part.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Rendering pipeline (`useRenderElement`) | [RND](../architecture/rendering-and-composition.md) §6.1 | default tag `div`; state `{ orientation }`; no custom attribute mapping (default projection emits `data-orientation`); part-supplied attributes `{ role: 'separator', 'aria-orientation': orientation }` merged first, consumer attributes last; DOM handle = consumer handle only |

## 12. Edge cases, invariants and dev warnings

Separator emits no warnings and throws no errors of its own. Inherited dev checks (invalid `render` element error, uppercase-named render function warning) are specified in RND-EDGE-01…RND-EDGE-03.

- **SEP-EDGE-01** — MUST. The `orientation` value MUST NOT be validated or normalized: whatever truthy value is supplied is reflected verbatim in `aria-orientation`, `data-orientation` and the state object. (Only `'horizontal'` and `'vertical'` are part of the public type.)
  Source: `separator/Separator.tsx:16-23`. Test: none.
- **SEP-EDGE-02** — MUST. No behavior depends on writing direction (RTL) or locale; a vertical separator in a right-to-left layout carries the same attributes.
  Source: `separator/Separator.tsx:1-27` (no direction or locale input). Test: none.
- **SEP-EDGE-03** — MUST. Server-rendered markup MUST be identical to the client's first render (all attributes are pure functions of the parameters; nothing is applied after mount).
  Source: `separator/Separator.tsx:16-26`. Test: none.

## 13. Behaviors evidenced by tests

### `Separator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; `testRenderPropWith` default `div`; `button` not set) | SEP-DOM-03, SEP-API-02, SEP-API-03 |
| "renders a div with the `separator` role" | SEP-STR-01, SEP-ARIA-01 |
| "sets aria-orientation="$orientation"" (parameterised: `horizontal`, `vertical`) | SEP-ARIA-02, SEP-API-01 |

### Additional requirements introduced here

- **SEP-DOM-03** — MUST. Separator MUST pass the shared conformance suite: RND-DOM-36…RND-DOM-48 with a `div` substitute element and an `HTMLDivElement` DOM handle (RND-DOM-49 does not apply; Separator has no `nativeButton`).
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] `Separator.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- The component is a stateless function of its parameters. A port can render it as a single element with three computed attributes and attribute pass-through. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 4 (and §Amendments: source-generated `data-*` projection of `orientation`).
- `aria-orientation` is passed explicitly for both values; a port whose attribute layer drops attributes equal to the ARIA default must not do so here (SEP-ARIA-02 is asserted for `horizontal`). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 4 (part-supplied attributes merged before the unmatched bag, per SEP-DOM-02).
- The DOM handle type in the source is `HTMLDivElement`; when the consumer substitutes another element (`render`), the handle receives that element. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24, RND-DOM-41).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| SEP-STR-01 | MUST | single host element, default `div`, no sub-parts | 2 |
| SEP-API-01 | MUST | `orientation` default `'horizontal'` | 3.1 |
| SEP-API-02 | MUST | `className`/`style`/`render`/`ref` per RND with state object | 3.1 |
| SEP-API-03 | MUST | other params forwarded; `orientation` consumed | 3.1 |
| SEP-STA-01 | MUST | state object is exactly `{ orientation }` | 3.1 |
| SEP-DOM-01 | MUST | `data-orientation` always equals orientation | 5.1 |
| SEP-DOM-02 | MUST | consumer attributes override built-in ones | 5.1 |
| SEP-DOM-03 | MUST | passes RND conformance suite (DOM-36…48) | 13 |
| SEP-ARIA-01 | MUST | `role="separator"` | 5.2 |
| SEP-ARIA-02 | MUST | `aria-orientation` always present, both values | 5.2 |
| SEP-FOC-01 | MUST | never focusable by itself | 6.3 |
| SEP-EDGE-01 | MUST | orientation not validated | 12 |
| SEP-EDGE-02 | MUST | no RTL/locale dependence | 12 |
| SEP-EDGE-03 | MUST | SSR markup equals first client render | 12 |

## 16. Open questions

1. **`aria-orientation="horizontal"` is redundant.** WAI-ARIA defines `horizontal` as the implicit value for `separator`; Base UI nevertheless always emits it (`separator/Separator.tsx:23`) and the test asserts it. Documented as required (SEP-ARIA-02).
2. **Assignment hint "aria-orientation only when vertical?"** — resolved: it is emitted for both orientations.
3. **No `<hr>` default.** The default element is a `<div>` with `role="separator"` rather than a native `<hr>` (`separator/Separator.tsx:20`); a port should not substitute `<hr>` by default because the conformance suite expects an `HTMLDivElement`.
