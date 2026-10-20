# Scroll Area

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/scroll-area/` |
| Tests | `packages/react/src/scroll-area/**/*.test.tsx` (6 files) |
| Docs page | `docs/src/app/(docs)/react/components/scroll-area/page.mdx` |
| Prefix | `SCR` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [shared-utilities](../architecture/shared-utilities.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md) (finite-animation filter only). [event-details-and-reasons](../architecture/event-details-and-reasons.md) is not used: the component has no change callbacks and emits no `reason` values. |

## 1. Overview

Scroll Area is a native scroll container (**Viewport**, an element with `overflow: scroll` whose native scrollbars are hidden by a stylesheet) with consumer-styled scrollbars drawn on top of it. Each **Scrollbar** is an absolutely positioned track for one axis containing a **Thumb** whose length and offset the component computes from the Viewport's `scrollHeight`/`clientHeight` (or width) ratio and scroll offset. A **Corner** fills the square where a vertical and a horizontal scrollbar meet. **Content** is a wrapper inside the Viewport that grows to its content's intrinsic width (so horizontal overflow is measurable) and whose resizes trigger re-measurement. The **Root** groups everything, owns all shared state, and exposes it to every part as `data-*` attributes (`data-scrolling`, `data-hovering`, `data-has-overflow-x/y`, `data-overflow-{x,y}-{start,end}`) and CSS variables (`--scroll-area-thumb-height/width`, `--scroll-area-corner-height/width`, `--scroll-area-overflow-{x,y}-{start,end}`).

What the user experiences: scrolling the Viewport by wheel, keyboard, touch or trackpad is entirely native; the custom thumb follows. The Scrollbar/Thumb typically fade in while the pointer hovers the scroll area (`data-hovering`) or for 500 ms after the last scroll movement (`data-scrolling`), both driven by consumer CSS. Dragging the Thumb scrolls the Viewport proportionally with pointer capture; pressing the track jumps the thumb under the pointer and starts a drag from there; wheeling over a scrollbar scrolls its axis. The Viewport is keyboard-focusable only when it actually overflows. On WebKit, rubber-band overscroll shrinks the thumb against the pinned edge. Scroll Area has no open/close state, no callbacks, no positioning and no form integration; its complexity lies entirely in measurement, scroll attribution and the pointer-drag mapping.

## 2. Anatomy

```
<ScrollArea.Root>              required, exactly 1          default element: div
  <ScrollArea.Viewport>        optional, 0..1               default element: div   (the scroll container)
    <ScrollArea.Content>       optional, 0..1               default element: div   (must be inside Viewport)
  <ScrollArea.Scrollbar>       optional, 0..1 per orientation   default element: div
    <ScrollArea.Thumb>         optional, 0..1 per Scrollbar     default element: div   (must be inside Scrollbar)
  <ScrollArea.Corner>          optional, 0..1               default element: div
</ScrollArea.Root>
```

Parts locate each other through ancestor-provided shared state, not DOM adjacency: Viewport, Scrollbar and Corner bind to the nearest ancestor Root; Content binds to the nearest ancestor Viewport (and Root); Thumb binds to the nearest ancestor Scrollbar (for its orientation) and Root. A Root holds exactly one DOM handle per role (viewport, vertical scrollbar, horizontal scrollbar, vertical thumb, horizontal thumb, corner), so a second instance of the same role within one Root overwrites the first's registration; nested Roots are independent.

- **SCR-STR-01** — MUST. `ScrollArea.Root` renders a `<div>` by default and is the only part that owns state; it may be rendered with any subset of the other parts (including none).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:34-44,280-285,343-348`. Test: [L2] `ScrollAreaRoot.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **SCR-STR-02** — MUST. `ScrollArea.Viewport` renders a `<div>` by default and MUST be a descendant of a `ScrollArea.Root`; rendering it outside one throws the error of SCR-EDGE-01. A Root is designed for at most one Viewport (single viewport handle).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:77-103,409-414`; `scroll-area/root/ScrollAreaRootContext.ts:52-60`. Test: [L2] `ScrollAreaViewport.test.tsx` describeConformance; "throws a descriptive error when rendered outside <ScrollArea.Root>".
- **SCR-STR-03** — MUST. `ScrollArea.Content` renders a `<div>` by default and MUST be a descendant of a `ScrollArea.Viewport` (and therefore of a Root); rendering it inside a Root but outside a Viewport throws the error of SCR-EDGE-02.
  Source: `scroll-area/content/ScrollAreaContent.tsx:17-24,60-73`; `scroll-area/viewport/ScrollAreaViewportContext.ts:12-20`. Test: [L2] `ScrollAreaContent.test.tsx` describeConformance; "throws a descriptive error when rendered outside <ScrollArea.Viewport>".
- **SCR-STR-04** — MUST. `ScrollArea.Scrollbar` renders a `<div>` by default and MUST be a descendant of a `ScrollArea.Root` (SCR-EDGE-01 otherwise). A Root supports one vertical and one horizontal Scrollbar; a second Scrollbar of the same `orientation` overwrites the first's handle registration.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:22-53,221-226`. Test: [L2] `ScrollAreaScrollbar.test.tsx` describeConformance (`<ScrollArea.Scrollbar keepMounted />`, `refInstanceof: HTMLDivElement`).
- **SCR-STR-05** — MUST. `ScrollArea.Thumb` renders a `<div>` by default and MUST be a descendant of a `ScrollArea.Scrollbar`, from which it takes its orientation; rendering it inside a Root but outside a Scrollbar throws the error of SCR-EDGE-03. One Thumb per Scrollbar is supported (one thumb handle per orientation).
  Source: `scroll-area/thumb/ScrollAreaThumb.tsx:14-40`; `scroll-area/scrollbar/ScrollAreaScrollbarContext.ts:10-18`. Test: [L2] `ScrollAreaThumb.test.tsx` describeConformance; "throws a descriptive error when rendered outside <ScrollArea.Scrollbar>".
- **SCR-STR-06** — MUST. `ScrollArea.Corner` renders a `<div>` by default and MUST be a descendant of a `ScrollArea.Root` (SCR-EDGE-01 otherwise); one Corner per Root is supported.
  Source: `scroll-area/corner/ScrollAreaCorner.tsx:13-36`. Test: [L2] `ScrollAreaCorner.test.tsx` describeConformance (rendered inside a Root whose viewport overflows on both axes so that the Corner is present, see SCR-DOM-26).
- **SCR-STR-07** — MUST. Every part other than Root is optional, and every behavior that needs an absent part degrades to a no-op rather than an error: without a Viewport, scrollbar/thumb presses and wheel events do nothing (SCR-PTR-12, SCR-PTR-15, SCR-PTR-21); without a Thumb, track presses do nothing (SCR-PTR-15); without a Scrollbar for an axis, the thumb size for that axis is still computed from the viewport alone (§4.5); without a Corner, the corner size stays `0 × 0` (SCR-STA-18).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:145-154,199-209`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:137-146`; `scroll-area/viewport/ScrollAreaViewport.tsx:199-204,261-269`. Test: [L4] `ScrollAreaThumb.test.tsx` "handles a thumb gesture when no viewport is mounted"; `ScrollAreaScrollbar.test.tsx` "handles a track press when no viewport is mounted", "does not start a track gesture without a thumb".

Content is "structural" in the sense that it attaches no handlers, but it is not inert: it carries the Root's state attributes, forces `min-width: fit-content` so that content wider than the viewport is reflected in the viewport's `scrollWidth` (the docs call it required for horizontally scrollable content, `CHANGELOG.md v1.0.0-alpha.8 "Add Content part"`), and observes its own resizes (SCR-ANI-05). Corner is structural: an empty absolutely positioned box sized from the scrollbars.

## 3. Public API

All six parts accept `className`, `style` and `render` per [rendering-and-composition](../architecture/rendering-and-composition.md) (RND-API-01…03) and forward every other native `<div>` attribute (RND-API-04). Consumer attributes are merged after the part's own attributes, so a consumer value for the same attribute name replaces the part's (RND-DOM-19), consumer inline styles override the part's inline styles key by key (RND-DOM-16), and consumer handlers run before the part's handler for the same event and may veto it with `preventBaseUIHandler()` (RND-EVT-02…04).

### 3.1 `ScrollArea.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `ScrollArea.Root.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `overflowEdgeThreshold` | `number \| Partial<{ xStart: number; xEnd: number; yStart: number; yEnd: number }> \| undefined` | `0` | Distance in pixels the viewport must be scrolled away from an edge before the corresponding `data-overflow-*` edge attribute is applied. A number applies to all four edges; an object configures edges individually. | SCR-API-01, SCR-API-02, SCR-API-03, SCR-API-04 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). The part's own inline `style` (SCR-DOM-01, SCR-DOM-02) is merged under the consumer's. | — |
| any other `<div>` attribute / handler | | — | Forwarded. `role` (SCR-ARIA-01) and the pointer handlers of SCR-PTR-27…31 are part-supplied and can be overridden/chained. | — |

**Consumed (not forwarded to the element):** `overflowEdgeThreshold`.

- **SCR-API-01** — MUST. `overflowEdgeThreshold` defaults to `0` (every edge attribute appears as soon as the normalized scroll distance from that edge is greater than `0`, see SCR-STA-09).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:46,386-401,408-422`. Test: [L2] `ScrollAreaRoot.test.tsx` "applies data attributes on root, viewport and scrollbars based on overflow and edges".
- **SCR-API-02** — MUST. A numeric `overflowEdgeThreshold` applies the same threshold to `xStart`, `xEnd`, `yStart` and `yEnd`; an object form sets each named edge independently, and an edge missing from the object uses `0`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:411-421`. Test: [L2] `ScrollAreaRoot.test.tsx` "applies numeric overflowEdgeThreshold to every edge", "respects overflowEdgeThreshold and exposes scroll metrics" (`{ xStart: 20, yStart: 5 }`).
- **SCR-API-03** — MUST. Each threshold is normalized to `max(0, value || 0)`: negative, `NaN`, `undefined` and `null` all become `0`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:416-421`. Test: none.
- **SCR-API-04** — MUST. Changing any threshold after mount re-evaluates the edge attributes against the current scroll offset without requiring a new scroll event (timing per SCR-ANI-02).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:287-299`. Test: [L4] `ScrollAreaRoot.test.tsx` "recomputes overflow edges when overflowEdgeThreshold changes".

#### State object (`ScrollArea.Root.State`)

| Field | Type | Meaning |
|---|---|---|
| `scrolling` | `boolean` | A user-driven scroll movement on either axis occurred within the last 500 ms (SCR-STA-11, SCR-STA-12). |
| `hasOverflowX` | `boolean` | Content is wider than the viewport (SCR-STA-06). |
| `hasOverflowY` | `boolean` | Content is taller than the viewport (SCR-STA-06). |
| `overflowXStart` | `boolean` | Scrolled away from the inline-start edge by more than the `xStart` threshold (SCR-STA-09). |
| `overflowXEnd` | `boolean` | Scrolled away from the inline-end edge by more than the `xEnd` threshold. |
| `overflowYStart` | `boolean` | Scrolled away from the top edge by more than the `yStart` threshold. |
| `overflowYEnd` | `boolean` | Scrolled away from the bottom edge by more than the `yEnd` threshold. |
| `cornerHidden` | `boolean` | Not both axes overflow (SCR-STA-07). Present in the state object but never emitted as an attribute (SCR-DOM-04). |

Source: `scroll-area/root/ScrollAreaRoot.tsx:251-263,351-384`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-scrolling` | `scrolling === true`; empty value | SCR-DOM-03 |
| `data-has-overflow-x` | `hasOverflowX === true`; empty value | SCR-DOM-03 |
| `data-has-overflow-y` | `hasOverflowY === true`; empty value | SCR-DOM-03 |
| `data-overflow-x-start` | `overflowXStart === true`; empty value | SCR-DOM-03 |
| `data-overflow-x-end` | `overflowXEnd === true`; empty value | SCR-DOM-03 |
| `data-overflow-y-start` | `overflowYStart === true`; empty value | SCR-DOM-03 |
| `data-overflow-y-end` | `overflowYEnd === true`; empty value | SCR-DOM-03 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--scroll-area-corner-height` | Root inline style | corner height in px (`0px` until both scrollbars are visible and a Corner is mounted) | SCR-DOM-02 |
| `--scroll-area-corner-width` | Root inline style | corner width in px | SCR-DOM-02 |

### 3.2 `ScrollArea.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `ScrollArea.Viewport.State`, identical to the Root's state).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `tabIndex` | `number` | computed: `0` when either axis overflows, `-1` otherwise | Native attribute the part supplies itself (SCR-DOM-08); a consumer value replaces the computed one. Not listed in the outline. | SCR-API-05 |
| `className`, `style`, `render` | per RND | — | The part's own class `base-ui-disable-scrollbar` and inline `overflow: scroll` are merged with the consumer's (SCR-DOM-06, SCR-DOM-07). | — |
| `onScroll`, `onWheel`, `onPointerMove`, `onPointerEnter`, `onKeyDown` | handlers | — | Part-supplied handlers (SCR-PTR-32, SCR-PTR-33) chained with the consumer's per RND-EVT-02. | — |
| any other `<div>` attribute | | — | Forwarded. | — |

**Consumed (not forwarded to the element):** none.

- **SCR-API-05** — MUST. A consumer-supplied `tabIndex` on the Viewport replaces the computed value of SCR-DOM-08 (the consumer's attribute is merged last).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:369,409-414`; `internals/useRenderElement.tsx` (props list order, RND-DOM-19). Test: [L2] `ScrollAreaViewport.test.tsx` "does not use a presentational role on the focusable viewport" (`tabIndex={0}` rendered as `tabindex="0"`). Docs: experiments `scroll-area/inside-menu.tsx:21` and `inside-select.tsx:23` pass `tabIndex={-1}`.

#### State object (`ScrollArea.Viewport.State`)

Same fields and values as `ScrollArea.Root.State`; the Viewport receives the Root's state object unchanged (SCR-STA-02).
Source: `scroll-area/viewport/ScrollAreaViewport.tsx:102,411,435`.

#### Data attributes

Same seven attributes as the Root (SCR-DOM-10): `data-scrolling`, `data-has-overflow-x`, `data-has-overflow-y`, `data-overflow-x-start`, `data-overflow-x-end`, `data-overflow-y-start`, `data-overflow-y-end`. Additionally `data-id` (SCR-DOM-09; not in the outline).

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--scroll-area-overflow-x-start` | Viewport inline style | normalized distance in px scrolled from the inline-start edge (`0px` when no horizontal overflow) | SCR-DOM-11 |
| `--scroll-area-overflow-x-end` | Viewport inline style | distance in px to the inline-end edge | SCR-DOM-11 |
| `--scroll-area-overflow-y-start` | Viewport inline style | distance in px scrolled from the top edge | SCR-DOM-11 |
| `--scroll-area-overflow-y-end` | Viewport inline style | distance in px to the bottom edge | SCR-DOM-11 |

These four variables are registered as non-inheriting custom properties (SCR-DOM-12); descendants that want them must opt in with `--scroll-area-overflow-y-start: inherit` (Docs: `components/scroll-area/page.mdx` §Gradient scroll fade).

### 3.3 `ScrollArea.Content`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `ScrollArea.Content.State`, identical to the Root's state).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | The part's inline `min-width: fit-content` is merged under the consumer's style (SCR-DOM-13). | — |
| any other `<div>` attribute | | — | Forwarded. `role` (SCR-ARIA-02) is part-supplied and may be overridden. | — |

**Consumed:** none.

#### State object (`ScrollArea.Content.State`)

Same as `ScrollArea.Root.State` (SCR-STA-02). Source: `scroll-area/content/ScrollAreaContent.tsx:24,62,78`.

#### Data attributes

Same seven attributes as the Root (SCR-DOM-14).

#### CSS variables

None.

### 3.4 `ScrollArea.Scrollbar`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `ScrollArea.Scrollbar.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `orientation` | `'vertical' \| 'horizontal' \| undefined` | `'vertical'` | Which axis the scrollbar controls. | SCR-API-06 |
| `keepMounted` | `boolean \| undefined` | `false` | Keep the element in the DOM when the viewport does not overflow on this axis. | SCR-API-07 |
| `className`, `style`, `render` | per RND | — | The part's inline positioning/visibility styles (SCR-DOM-17…20) are merged under the consumer's. | — |
| `aria-hidden` | | `true` | Part-supplied (SCR-ARIA-04); consumer may override, including with `undefined` to remove it. | — |
| `onPointerDown`, `onMouseDown`, `onPointerUp`, `onPointerCancel` | handlers | — | Part-supplied (SCR-PTR-14…20); chained with the consumer's. The `wheel` listener (SCR-PTR-26) is a native listener, not a merged handler. | — |
| any other `<div>` attribute | | — | Forwarded. | — |

**Consumed (not forwarded to the element):** `orientation`, `keepMounted`.

- **SCR-API-06** — MUST. `orientation` defaults to `'vertical'`; it selects which viewport axis the Scrollbar (and its Thumb) measure and control, which handle registration the element receives, and the value of `data-orientation`.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:29,55,222,254-262`. Test: [L2] `ScrollAreaScrollbar.test.tsx` "sets the orientation data attribute".
- **SCR-API-07** — MUST. `keepMounted` defaults to `false`. With `false`, the Scrollbar renders no element while its axis has no overflow (SCR-DOM-15); with `true`, the element is always rendered and its track is never hidden by the pre-measurement visibility rule (SCR-DOM-19). `keepMounted` has no transition or animation semantics (SCR-ANI-10).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:30,65-67,228-230,263-267`. Test: [L2] `ScrollAreaRoot.test.tsx` "shows keepMounted scrollbar track and thumb after mount compute", "does not add state attributes when content does not overflow" (both scrollbars `keepMounted` while nothing overflows).

#### State object (`ScrollArea.Scrollbar.State`)

| Field | Type | Meaning |
|---|---|---|
| all fields of `ScrollArea.Root.State` | | Copied from the Root's state, except `scrolling` which is replaced below (SCR-STA-03). |
| `hovering` | `boolean` | A non-touch pointer is over the scroll area (SCR-STA-15). |
| `scrolling` | `boolean` | A user-driven scroll movement on **this scrollbar's axis** occurred within the last 500 ms (SCR-STA-11). |
| `orientation` | `'vertical' \| 'horizontal'` | The `orientation` prop. |

Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:57-62,239-252`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; `"vertical"` or `"horizontal"` | SCR-DOM-16 |
| `data-hovering` | `hovering === true`; empty value | SCR-DOM-16 |
| `data-scrolling` | axis `scrolling === true`; empty value | SCR-DOM-16 |
| `data-has-overflow-x`, `data-has-overflow-y`, `data-overflow-x-start`, `data-overflow-x-end`, `data-overflow-y-start`, `data-overflow-y-end` | as on the Root (both axes, regardless of this scrollbar's orientation) | SCR-DOM-16 |
| `data-id` | always (when an id was generated); `<root id>-scrollbar`. Not in the outline. | SCR-DOM-21 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--scroll-area-thumb-height` | vertical Scrollbar inline style | computed thumb height in px (`0px` before the first measurement) | SCR-DOM-20 |
| `--scroll-area-thumb-width` | horizontal Scrollbar inline style | computed thumb width in px | SCR-DOM-20 |

### 3.5 `ScrollArea.Thumb`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `ScrollArea.Thumb.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | The part's inline `visibility` and `height`/`width` (SCR-DOM-23) are merged under the consumer's style; the inline `transform` and the overscroll size variable are written directly to the element after render (SCR-DOM-24, SCR-DOM-25) and are not part of the merge. | — |
| `onPointerDown`, `onPointerMove`, `onPointerUp`, `onPointerCancel` | handlers | — | Part-supplied drag handlers (§6.2); chained with the consumer's. | — |
| any other `<div>` attribute | | — | Forwarded. | — |

**Consumed:** none.

#### State object (`ScrollArea.Thumb.State`)

| Field | Type | Meaning |
|---|---|---|
| `scrolling` | `boolean` | Axis-specific scrolling flag of the enclosing Scrollbar's orientation (SCR-STA-04). |
| `orientation` | `'horizontal' \| 'vertical'` | Orientation of the enclosing Scrollbar. |

Source: `scroll-area/thumb/ScrollAreaThumb.tsx:31-37,62-71`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; `"vertical"` or `"horizontal"` | SCR-DOM-22 |
| `data-scrolling` | axis `scrolling === true`; empty value | SCR-DOM-22 |

#### CSS variables

None set by the Thumb. It consumes `--scroll-area-thumb-height`/`--scroll-area-thumb-width` from its Scrollbar (SCR-DOM-23) and temporarily overrides that variable on itself while overscrolling (SCR-DOM-25).

### 3.6 `ScrollArea.Corner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `ScrollArea.Corner.State`, an empty object).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | The part's inline position and size (SCR-DOM-27) are merged under the consumer's style. | — |
| `aria-hidden` | | `true` | Part-supplied (SCR-ARIA-05); consumer may override. | — |
| any other `<div>` attribute | | — | Forwarded. | — |

**Consumed:** none.

#### State object (`ScrollArea.Corner.State`)

Empty (`{}`); class/style functions receive an empty object and no state attributes are emitted (SCR-STA-05, RND-STA-02).
Source: `scroll-area/corner/ScrollAreaCorner.tsx:21-36,45`.

#### Data attributes

None (SCR-DOM-28).

#### CSS variables

None of its own; its size is also published on the Root as `--scroll-area-corner-width/height` (SCR-DOM-02).

### 3.7 Shared types

- `ScrollArea.Root.Props` / `.State`, `ScrollArea.Viewport.Props` / `.State`, `ScrollArea.Content.Props` / `.State`, `ScrollArea.Scrollbar.Props` / `.State`, `ScrollArea.Thumb.Props` / `.State`, `ScrollArea.Corner.Props` / `.State` — the twelve types the outline lists; each `Props` extends the common part parameters for `'div'` with the state type above. Flat aliases `ScrollAreaRootProps`, `ScrollAreaRootState`, `ScrollAreaViewportProps`, … are also exported (`scroll-area/index.ts:3-8`).
- Attribute/variable name constants are exported as namespaces: `ScrollAreaRootCssVariables`, `ScrollAreaRootDataAttributes`, `ScrollAreaViewportCssVariables`, `ScrollAreaViewportDataAttributes`, `ScrollAreaScrollbarCssVariables`, `ScrollAreaScrollbarDataAttributes`, `ScrollAreaContentDataAttributes`, `ScrollAreaThumbDataAttributes` (`scroll-area/index.ts:10-17`). There is no `ScrollAreaCornerDataAttributes` export (the Corner has none).
- `Orientation` literal union: `'vertical' | 'horizontal'`.
- No `ChangeEventReason`/`ChangeEventDetails` types exist; the component has no change callbacks (§7).

## 4. State model

Scroll Area has no consumer-owned (controlled) state and no `default*` props. Everything is derived from DOM measurement of the Viewport and from pointer/scroll input, and all of it is owned by the Root and shared with the other parts. There is no open/close state and no transition status.

### 4.1 State objects per part

- **SCR-STA-01** — MUST. The state object exposed to the Root's `className`/`style`/`render` functions contains exactly the fields `scrolling`, `hasOverflowX`, `hasOverflowY`, `overflowXStart`, `overflowXEnd`, `overflowYStart`, `overflowYEnd` and `cornerHidden` (all booleans), with the values defined in §4.2–4.3. Server: relaxed, see §9.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:251-263`. Test: none directly (the data attributes derived from it are tested throughout `ScrollAreaRoot.test.tsx`).
- **SCR-STA-02** — MUST. The Viewport and the Content expose the Root's state object unchanged (same fields, same values at the same time).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:102,411`; `scroll-area/content/ScrollAreaContent.tsx:24,62`. Test: [L2] `ScrollAreaRoot.test.tsx` "applies data attributes on root, viewport and scrollbars based on overflow and edges" (root, viewport and content flags compared for equality).
- **SCR-STA-03** — MUST. The Scrollbar's state object contains every field of the Root's state plus `hovering` and `orientation`, with `scrolling` replaced by the per-axis flag of the Scrollbar's own orientation (SCR-STA-11); `cornerHidden` is included but never emitted (SCR-DOM-04). Server: relaxed, see §9.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:57-62`. Test: [L2] `ScrollAreaScrollbar.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled in the correct direction".
- **SCR-STA-04** — MUST. The Thumb's state object contains exactly `scrolling` (the per-axis flag of the enclosing Scrollbar's orientation) and `orientation`. Server: relaxed, see §9.
  Source: `scroll-area/thumb/ScrollAreaThumb.tsx:34-37`. Test: [L2] `ScrollAreaThumb.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled in the correct direction".
- **SCR-STA-05** — MUST. The Corner's state object is empty.
  Source: `scroll-area/corner/ScrollAreaCorner.tsx:21-36,45`. Test: none.

### 4.2 Measured state (overflow, edges, thumb and corner size)

All measured state is produced by one procedure, the *measurement* (`computeThumbPosition`, transcribed in §4.6), which reads the Viewport's `scrollHeight`, `scrollWidth`, `clientHeight`, `clientWidth`, `scrollTop`, `scrollLeft` and the scrollbar/thumb boxes, and runs at the moments listed in §9.

- **SCR-STA-06** — MUST. `hasOverflowY` is `true` iff `viewport.scrollHeight > viewport.clientHeight`; `hasOverflowX` is `true` iff `viewport.scrollWidth > viewport.clientWidth` (equality means no overflow). These are evaluated at every measurement; a measurement that exits early (SCR-ANI-08) leaves them unchanged.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:152-154,271,442-451`. Test: [L3] `ScrollAreaRoot.test.tsx` "does not add state attributes when content does not overflow", "clears corner, overflow attributes, and metrics when content stops overflowing"; `ScrollAreaContent.test.tsx` "recomputes overflow when observed content resizes".
- **SCR-STA-07** — MUST. `cornerHidden` is `true` iff at least one axis has no overflow (`!hasOverflowX || !hasOverflowY`).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:449`; `scroll-area/root/ScrollAreaRoot.tsx:260`. Test: [L3] `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing" (corner absent at half size, present at full size).
- **SCR-STA-08** — MUST. Before the first measurement, the state is: no overflow on either axis (`hasOverflowX = hasOverflowY = false`, `cornerHidden = true`), all four edge flags `false`, `scrolling = false`, `hovering = false`, thumb size `0 × 0`, corner size `0 × 0`. This is also the server-rendered state (SCR-EDGE-13).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:18-21,55-63`. Test: [L2] `ScrollAreaRoot.test.tsx` "measures content mounted after the viewport initial measurement" (empty viewport: no `data-has-overflow-y`, `tabindex="-1"`).
- **SCR-STA-09** — MUST. The edge flags are, with `fromStart`/`fromEnd` the normalized distances of SCR-STA-10 and `thr` the normalized thresholds of SCR-API-01…03: `overflowXStart = hasOverflowX && fromStartX > thr.xStart`; `overflowXEnd = hasOverflowX && fromEndX > thr.xEnd`; `overflowYStart = hasOverflowY && fromStartY > thr.yStart`; `overflowYEnd = hasOverflowY && fromEndY > thr.yEnd`. The comparison is strict, so with the default threshold `0` an edge flag is set as soon as the normalized distance is positive.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:273-280`. Test: [L3] `ScrollAreaRoot.test.tsx` "applies data attributes on root, viewport and scrollbars based on overflow and edges" (start: `x-end`/`y-end` only; middle: all four; end: `x-start`/`y-start` only), "applies numeric overflowEdgeThreshold to every edge" (15 px with threshold 20 → not set; `max − 15` → start set, end not set), "respects overflowEdgeThreshold and exposes scroll metrics" (`scrollLeft` 15 < 20 → no `x-start`; 35 → set; `scrollTop` 7 > 5 → `y-start`).
- **SCR-STA-10** — MUST. For an axis with overflow, `fromStart` is `normalizeScrollOffset(offset, max)` where `offset` is `scrollTop` (vertical) or, for the horizontal axis, `scrollLeft` in LTR and `−scrollLeft` in RTL (SCR-I18N-03), and `max` is `max(0, scrollSize − clientSize)`; `fromEnd = max − fromStart`. Normalization clamps to `[0, max]` and snaps to `0`/`max` when within 1 px of that edge (UTL-DOM-30 / UTL-ALG-12 in [shared-utilities](../architecture/shared-utilities.md)). For an axis without overflow both distances are `0`.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:157-174`; `utils/scrollEdges.ts:9-33`. Test: [L3] `ScrollAreaRoot.test.tsx` "treats near-edge scroll offsets as fully scrolled" (`max − 0.5` → end flags cleared), "correctly handles RTL".
- **SCR-STA-17** — MUST. The thumb size per axis is the value `thumbW`/`thumbH` of the measurement in §4.6: the usable track length (scrollbar box minus the scrollbar's padding on that axis minus the thumb's margin on that axis, additionally minus the not-yet-applied corner size, and never more than the viewport's client size minus those offsets) multiplied by `clientSize / scrollSize`, floored at `MIN_THUMB_SIZE = 16` px. The size is stored only when it differs from the previous value.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:155-156,175-176,185-211`; `scroll-area/constants.ts:2`. Test: [L3] `ScrollAreaRoot.test.tsx` "should correctly set thumb height and width based on scrollable content" (`(200/1000)×200 = 40px`), "accounts for scrollbar padding" (`(200−16)×0.2`), "accounts for scrollbar margin" (`(390−22)×0.39`), "accounts for thumb margin" (`(200−16)×0.2`).
- **SCR-STA-18** — MUST. The corner size is `{ width: verticalScrollbar.offsetWidth, height: horizontalScrollbar.offsetHeight }` when both axes overflow (a missing scrollbar element contributes `0`), and `0 × 0` when either axis does not overflow; it is updated only by a measurement that runs while a Corner element is mounted, and only when the value differs from the previous one.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:178-183,261-269`. Test: [L3] `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing" (`11px × 13px`), "clears corner, overflow attributes, and metrics when content stops overflowing"; `ScrollAreaCorner.test.tsx` "should apply correct corner size when both scrollbars are present".
- **SCR-STA-19** — MUST. A *measured* flag becomes `true` at the first measurement attempt (even one that exits early because the viewport has zero size) and never reverts; it gates the pre-measurement `visibility: hidden` of the Thumb and of a non-`keepMounted` Scrollbar (SCR-DOM-19, SCR-DOM-23) and whether a later-mounted Content's first resize notification is honoured (SCR-ANI-05).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:136-146`; `scroll-area/root/ScrollAreaRoot.tsx:59`. Test: [L4] `ScrollAreaRoot.test.tsx` "shows scrollbars after mount compute before the first ResizeObserver measurement", "shows keepMounted scrollbar track and thumb after mount compute".
- **SCR-STA-20** — MUST. A measurement whose results (thumb size, corner size, overflow presence, edge flags) are all identical to the stored values MUST NOT change the shared state; only the Viewport's inline variables and the Thumb's inline transform are rewritten (so a scroll that changes no derived state notifies no part of a state change and rewrites no part's attributes).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:209-211,266-269,271,280,457-465`. Test: [L4] `ScrollAreaRoot.test.tsx` "does not re-render parts on scroll when the corner size is unchanged". Docs: `CHANGELOG.md v1.7.0 "Fix thumb-drag divide-by-zero and per-scroll re-render"`.

### 4.3 Scrolling flags and scroll attribution

- **SCR-STA-11** — MUST. There is one *scrolling* flag per axis. A user-driven scroll movement (SCR-STA-13) whose position differs from the previously recorded position on an axis sets that axis's flag to `true` and (re)starts a 500 ms timer (`SCROLL_TIMEOUT`) that resets the flag to `false`; every further movement on the axis restarts the timer, so the flag clears exactly 500 ms after the last movement on that axis. Movement on one axis never sets the other axis's flag. A thumb drag move on an axis also sets that axis's flag (SCR-PTR-05); a drag release clears it immediately (SCR-PTR-09).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:82-105`; `scroll-area/constants.ts:1`; `packages/utils/src/useTimeout.ts:19-25` (UTL-EVT-01). Test: [L4] `ScrollAreaScrollbar.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled in the correct direction"; `ScrollAreaThumb.test.tsx` same title; `ScrollAreaViewport.test.tsx` "removes [data-scrolling] after timeout" (present at 499 ms, absent at 500 ms).
- **SCR-STA-12** — MUST. The Root's (and hence Viewport's and Content's) `scrolling` is `true` iff either axis flag is `true`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:253`. Test: [L3] `ScrollAreaRoot.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled"; `ScrollAreaViewport.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled".
- **SCR-STA-13** — MUST. A `scroll` event on the Viewport counts as *user-driven* iff, at the time of the event, the scroll area is in touch modality (SCR-STA-16) **or** a `wheel`, `pointermove`, `pointerenter` or `keydown` event has been delivered to the Viewport element since the *programmatic* marker was last restored (SCR-STA-14). Only user-driven scrolls feed the scrolling flags; a scroll with no preceding interaction (e.g. `scrollTo()`, scroll restoration) does not set `data-scrolling`. Scroll position changes made by the component itself through a track press, a wheel over a scrollbar or a thumb drag are reported to the scrolling flags directly by those handlers (SCR-PTR-05, SCR-PTR-19, SCR-PTR-25).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:107,361-363,386-391,403-406`. Test: [L4] `ScrollAreaViewport.test.tsx` "ignores data-scrolling during programmatic scroll", "keeps ignoring programmatic scrolls in mouse modality", "adds [data-scrolling] in touch modality even when the gesture delivers no events", "restores programmatic scroll suppression after modality flips back to mouse". Docs: `CHANGELOG.md v1.0.0-beta.1 "Ignore data-scrolling during programmatic scroll"`, `CHANGELOG.md v1.7.0 "Fix scrollbar visibility during touch scrolling on iOS"`.
- **SCR-STA-14** — MUST. The *programmatic* marker starts `true`, is set to `false` by any of the four Viewport interaction events of SCR-STA-13, and is restored to `true` by a 100 ms timer that every `scroll` event on the Viewport restarts (so it is restored 100 ms after the last scroll event, i.e. once momentum scrolling has come to rest).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:107,361-363,393-401`. Test: [L4] `ScrollAreaViewport.test.tsx` "keeps ignoring programmatic scrolls in mouse modality" (pointerdown does not count as interaction; scroll 200 ms later is programmatic).

### 4.4 Hover and input modality

- **SCR-STA-15** — MUST. `hovering` is `true` from a non-touch `pointerenter`/`pointermove` delivered to the Root whose event target is the Root element or a DOM descendant of it, until a `pointerleave` on the Root; it is also `true` from mount when the Viewport already matches `:hover` (SCR-PTR-27…30).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:55,242-249,266-272`; `scroll-area/viewport/ScrollAreaViewport.tsx:301-307`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "detects a viewport that is already hovered on mount", "does not enter hover state for touch pointers", "adds [data-hovering] when the synthetic pointer target differs from the native path".
- **SCR-STA-16** — MUST. *Touch modality* is `true` iff the most recent `pointerenter`, `pointermove` or `pointerdown` delivered to the Root had `pointerType === 'touch'`; a later event of any other pointer type clears it.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:58,238-249,269`. Test: [L4] `ScrollAreaViewport.test.tsx` "adds [data-scrolling] in touch modality even when the gesture delivers no events", "restores programmatic scroll suppression after modality flips back to mouse".

### 4.5 Drag state

- **SCR-STA-21** — MUST. The Root keeps one drag record shared by both scrollbars: the active pointer id (or none), the pointer's client X/Y at drag start, the viewport's `scrollTop`/`scrollLeft` at drag start, the drag orientation (`'vertical'`/`'horizontal'`), and the Viewport's saved inline `scroll-snap-type` (or none). At most one drag is active at a time (SCR-PTR-11).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:73-80,138-150`. Test: [L4] `ScrollAreaThumb.test.tsx` "ignores a second pointer while a drag is active".

### 4.6 Measurement algorithm (transcribed)

The following pseudo-code is the exact procedure for thumb size, thumb position, overflow variables and edge flags; SCR-STA-06…10, SCR-STA-17, SCR-STA-18, SCR-DOM-11, SCR-DOM-24 and SCR-DOM-25 are its observable outputs. `rtl` is the reading direction of SCR-I18N-01; `scrollbarX/Y`, `thumbX/Y`, `corner` are the registered elements or `null`.

```
getOffset(element, prop ∈ {padding, margin}, axis ∈ {x, y}):      // scroll-area/utils/getOffset.ts:1-21
  if element is null: return 0
  key := prop + (axis = x ? "Inline" : "Block")                    // e.g. padding-inline-*, margin-block-*
  start := parseFloat(computedStyle(element)[key + "Start"])
  if axis = x and prop = margin:
    return start × 2                                               // SCR-I18N-05 (Safari misreports margin-inline-end in RTL)
  return start + parseFloat(computedStyle(element)[key + "End"])

measure():                                                         // ScrollAreaViewport.tsx:118-281
  viewport := Viewport element; if null: return
  contentH := viewport.scrollHeight; contentW := viewport.scrollWidth
  viewH := viewport.clientHeight;    viewW := viewport.clientWidth
  scrollTop := viewport.scrollTop;   scrollLeft := viewport.scrollLeft
  first := (lastMetrics is unset)
  lastMetrics := [viewH, contentH, viewW, contentW]
  if first: measured := true                                       // SCR-STA-19
  if contentH = 0 or contentW = 0: return                          // SCR-ANI-08

  hidden.y := viewH ≥ contentH ; hidden.x := viewW ≥ contentW ; hidden.corner := hidden.y or hidden.x
  ratioX := viewW / contentW ; ratioY := viewH / contentH
  maxScrollLeft := max(0, contentW − viewW) ; maxScrollTop := max(0, contentH − viewH)

  fromStartX := 0 ; fromEndX := 0
  if not hidden.x:
    fromStartX := normalizeScrollOffset(rtl ? −scrollLeft : scrollLeft, maxScrollLeft)   // UTL-DOM-30
    fromEndX := maxScrollLeft − fromStartX
  fromStartY := hidden.y ? 0 : normalizeScrollOffset(scrollTop, maxScrollTop)
  fromEndY   := hidden.y ? 0 : maxScrollTop − fromStartY

  trackW := hidden.x ? 0 : viewW ; trackH := hidden.y ? 0 : viewH

  cornerW := 0 ; cornerH := 0
  if not hidden.x and not hidden.y:
    cornerW := scrollbarY ? (scrollbarY.offsetWidth or 0) : 0
    cornerH := scrollbarX ? (scrollbarX.offsetHeight or 0) : 0
  cornerUnsized := (cornerSize.width = 0 and cornerSize.height = 0)
  cornerWOffset := cornerUnsized ? cornerW : 0                     // scrollbar not yet shortened by the corner
  cornerHOffset := cornerUnsized ? cornerH : 0

  padX := getOffset(scrollbarX, padding, x) ; padY := getOffset(scrollbarY, padding, y)
  marginX := getOffset(thumbX, margin, x)   ; marginY := getOffset(thumbY, margin, y)
  idealW := trackW − padX − marginX ; idealH := trackH − padY − marginY
  maxW := scrollbarX ? min(scrollbarX.offsetWidth − cornerWOffset, idealW) : idealW
  maxH := scrollbarY ? min(scrollbarY.offsetHeight − cornerHOffset, idealH) : idealH
  thumbW := max(16, maxW × ratioX) ; thumbH := max(16, maxH × ratioY)
  if {thumbW, thumbH} ≠ thumbSize: thumbSize := {thumbW, thumbH}  // SCR-STA-17, SCR-DOM-20

  if scrollbarY and thumbY:
    maxThumbOffsetY := scrollbarY.offsetHeight − thumbH − padY − marginY
    offsetY := overscrollThumb(thumbY, "--scroll-area-thumb-height", scrollTop, maxScrollTop, contentH, thumbH, maxThumbOffsetY)
    thumbY.style.transform := "translate3d(0," + offsetY + "px,0)"                    // SCR-DOM-24
  if scrollbarX and thumbX:
    maxThumbOffsetX := scrollbarX.offsetWidth − thumbW − padX − marginX
    offsetX := overscrollThumb(thumbX, "--scroll-area-thumb-width", rtl ? −scrollLeft : scrollLeft,
                               maxScrollLeft, contentW, thumbW, maxThumbOffsetX)
    thumbX.style.transform := "translate3d(" + (rtl ? −offsetX : offsetX) + "px,0,0)" // SCR-DOM-24, SCR-I18N-02

  viewport inline: --scroll-area-overflow-x-start := fromStartX + "px"; …-x-end := fromEndX; …-y-start := fromStartY; …-y-end := fromEndY   // SCR-DOM-11
  if corner element mounted and {cornerW, cornerH} ≠ cornerSize: cornerSize := {cornerW, cornerH}   // SCR-STA-18
  if hidden ≠ hiddenState: hiddenState := hidden                   // SCR-STA-06/07
  edges := { xStart: !hidden.x ∧ fromStartX > thr.xStart, xEnd: !hidden.x ∧ fromEndX > thr.xEnd,
             yStart: !hidden.y ∧ fromStartY > thr.yStart, yEnd: !hidden.y ∧ fromEndY > thr.yEnd }
  if edges ≠ overflowEdges: overflowEdges := edges                 // SCR-STA-09

overscrollThumb(thumb, sizeVar, scrollFromStart, maxScroll, content, size, maxThumbOffset):   // ScrollAreaViewport.tsx:472-492
  clamped := clamp(scrollFromStart, 0, maxScroll)
  overscroll := scrollFromStart − clamped                          // < 0 past the start edge, > 0 past the end edge
  nextSize := max(16, size × content / (content + |overscroll|))
  thumb inline sizeVar := (overscroll ≠ 0) ? nextSize + "px" : removed   // SCR-DOM-25
  offset := (maxScroll ≠ 0) ? (clamped / maxScroll) × maxThumbOffset : 0
  return offset + (overscroll > 0 ? size − nextSize : 0)          // end-edge overscroll keeps the thumb pinned to the end
```

Only Safari reports an out-of-range `scrollTop`/`scrollLeft` during rubber-banding; on other engines `overscroll` is always `0` and the branch is inert (`CHANGELOG.md v1.7.0 "Add WebKit overscroll feedback to <ScrollArea.Thumb>"`).

### 4.7 Invariants

- Thumb length is never below 16 px, even when the proportional length would be smaller or the track is shorter than 16 px (the thumb then overflows the track and `maxThumbOffset ≤ 0`, see SCR-PTR-06 and SCR-PTR-15).
- Edge flags are `false` whenever the axis has no overflow, regardless of thresholds.
- `cornerHidden` is `true` whenever either `hasOverflow*` is `false`; the Corner element exists iff `cornerHidden` is `false` (SCR-DOM-26).
- At most one drag is active; the per-axis scrolling flag of the dragged axis is `true` throughout a drag that moves the viewport and `false` immediately after release.

## 5. Rendered DOM and ARIA

State data attributes are projected per [rendering-and-composition](../architecture/rendering-and-composition.md): `true` → attribute with empty value, `false` → absent (RND-DOM-01, RND-DOM-02), name = `data-` + lowercased field (RND-DOM-05), with the custom mapping `scrollAreaStateAttributesMapping` (`scroll-area/root/stateAttributes.ts:5-15`) used by Root, Viewport, Content and Scrollbar: it maps the six overflow fields to the hyphenated names below and maps `cornerHidden` to nothing (RND-DOM-06, RND-DOM-07). `scrolling`, `hovering` and `orientation` are not in the mapping and use the default projection.

### 5.1 Root element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"presentation"` (overridable) | SCR-ARIA-01 | C# |
| `style.position` | `relative` | SCR-DOM-01 | C# |
| `style.--scroll-area-corner-height` / `--scroll-area-corner-width` | `<n>px` | SCR-DOM-02 | C# → JS |
| `data-scrolling` | `scrolling` | SCR-DOM-03 | JS |
| `data-has-overflow-x` / `data-has-overflow-y` | `hasOverflowX` / `hasOverflowY` | SCR-DOM-03 | C# |
| `data-overflow-x-start` / `-x-end` / `-y-start` / `-y-end` | corresponding edge flag | SCR-DOM-03 | C# |
| (no `data-cornerhidden`) | never | SCR-DOM-04 | C# |

- **SCR-DOM-01** — MUST. The Root element carries inline `position: relative` (so that the absolutely positioned Scrollbars and Corner are placed relative to it); a consumer `style.position` overrides it.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:273-277`. Test: none.
- **SCR-DOM-02** — MUST. The Root element carries the inline custom properties `--scroll-area-corner-height` and `--scroll-area-corner-width`, each equal to the corner size of SCR-STA-18 formatted as `<number>px` (number serialized per UTL-DOM-37) (`0px` initially and whenever the corner is hidden).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:275-276`; `scroll-area/root/ScrollAreaRootCssVars.ts:5,10`; see UTL-DOM-37. Test: [L4] `ScrollAreaCorner.test.tsx` "should apply correct corner size when both scrollbars are present" (`10px`/`10px` read from the Corner's computed style, inherited from the Root).
- **SCR-DOM-03** — MUST. The Root element carries `data-scrolling`, `data-has-overflow-x`, `data-has-overflow-y`, `data-overflow-x-start`, `data-overflow-x-end`, `data-overflow-y-start` and `data-overflow-y-end`, each present with an empty value exactly while the corresponding state field is `true`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:280-285`; `scroll-area/root/stateAttributes.ts:7-15`; `scroll-area/root/ScrollAreaRootDataAttributes.ts:4-28`. Test: [L2] `ScrollAreaRoot.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled", "applies data attributes on root, viewport and scrollbars based on overflow and edges", "does not add state attributes when content does not overflow" (L4 for the `data-scrolling` clause).
- **SCR-DOM-04** — MUST NOT. No part emits an attribute for the `cornerHidden` state field (the mapping suppresses it).
  Source: `scroll-area/root/stateAttributes.ts:14`. Test: none.

### 5.2 Scrollbar-hiding style element

- **SCR-DOM-05** — MUST. The Root renders, immediately before its own element, the shared scrollbar-hiding `<style>` element whose text is exactly `.base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}` (RND-DOM-35, UTL-DOM-32: de-duplicated per document, carries the CSP `nonce` when one is configured), unless the nearest `CSPProvider` sets `disableStyleElements` (UTL-DOM-33, UTL-DOM-35), in which case no style element is rendered and the consumer must supply the rule for the `base-ui-disable-scrollbar` class.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:53,345`; `utils/styles.tsx:1-12`; `internals/csp-context/CSPContext.tsx:11-17`. Test: none in this component (RND/UTL own the style-element tests).

### 5.3 Viewport element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `class` | contains `base-ui-disable-scrollbar` | SCR-DOM-06 | C# |
| `style.overflow` | `scroll` | SCR-DOM-07 | C# |
| `tabindex` | `0` when `hasOverflowX \|\| hasOverflowY`, else `-1`; consumer override (SCR-API-05) | SCR-DOM-08 | C# |
| `data-id` | `<root id>-viewport` | SCR-DOM-09 | C# |
| `role` | none | SCR-ARIA-03 | C# |
| seven state data attributes | as on the Root | SCR-DOM-10 | C# (overflow); JS (`data-scrolling`) |
| `style.--scroll-area-overflow-{x,y}-{start,end}` | `<n>px`, written directly | SCR-DOM-11 | JS |

- **SCR-DOM-06** — MUST. The Viewport element's class list contains `base-ui-disable-scrollbar` (merged with the consumer's class per RND-DOM-14), which with SCR-DOM-05 hides the native scrollbars (`scrollbar-width: none`; `::-webkit-scrollbar { display: none }`).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:370`; `utils/styles.tsx:1-4`. Test: none.
- **SCR-DOM-07** — MUST. The Viewport element carries inline `overflow: scroll` (both axes always scrollable containers; a consumer `style.overflow` overrides it).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:371-373`. Test: none.
- **SCR-DOM-08** — MUST. The Viewport element carries `tabindex="0"` when at least one axis overflows and `tabindex="-1"` when neither does (including before the first measurement), so a non-scrollable viewport is not in the tab order; the attribute updates with the overflow state.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:367-369`. Test: [L2] `ScrollAreaRoot.test.tsx` "measures content mounted after the viewport initial measurement" (`-1` while empty, `0` after content mounts). Docs: `CHANGELOG.md v1.3.0 "Fix focus trapping with a non-scrollable viewport"`.
- **SCR-DOM-09** — MUST. The Viewport element carries `data-id` equal to the Root's generated id (RND-DOM-27, `base-ui-<token>`) followed by `-viewport`; when no id is available (legacy fallback of RND-EDGE-09) the attribute is absent.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:366`; `scroll-area/root/ScrollAreaRoot.tsx:48`; `internals/useBaseUiId.ts:9-11`. Test: none.
- **SCR-DOM-10** — MUST. The Viewport element carries the same seven state data attributes as the Root (SCR-DOM-03), with identical values at all times.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:409-414`; `scroll-area/viewport/ScrollAreaViewportDataAttributes.ts:4-28`. Test: [L2] `ScrollAreaViewport.test.tsx` "applies data attributes on viewport", "adds [data-scrolling] attribute when viewport is scrolled"; `ScrollAreaRoot.test.tsx` "applies data attributes on root, viewport and scrollbars based on overflow and edges" (L4 for the `data-scrolling` clause).
- **SCR-DOM-11** — MUST. Every measurement writes the inline custom properties `--scroll-area-overflow-x-start`, `--scroll-area-overflow-x-end`, `--scroll-area-overflow-y-start` and `--scroll-area-overflow-y-end` on the Viewport element, formatted `<number>px` (number serialized per UTL-DOM-37), equal to `fromStartX`, `fromEndX`, `fromStartY`, `fromEndY` of SCR-STA-10 (all `0px` for an axis without overflow). They are written directly to the element during the measurement, not through the render pipeline, so they are absent from server-rendered markup until the first client measurement.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:24-29,250-259`; `scroll-area/viewport/ScrollAreaViewportCssVars.ts:5-20`; see UTL-DOM-37. Test: [L4] `ScrollAreaRoot.test.tsx` "respects overflowEdgeThreshold and exposes scroll metrics" (`--scroll-area-overflow-x-start` is `35px`), "clears corner, overflow attributes, and metrics when content stops overflowing" (all four `0px`). Docs: `components/scroll-area/page.mdx` §Gradient scroll fade; `CHANGELOG.md v1.0.0-beta.5 "Improve CSS vars performance"`.
- **SCR-DOM-12** — MUST. On the first Viewport mount in a page (post-commit, pre-paint), the four overflow custom properties are registered with the CSS Properties and Values API as `syntax: '<length>'`, `inherits: false`, `initialValue: '0px'` so that descendants do not inherit them; registration happens at most once per page, is skipped entirely on WebKit (`platform.engine.webkit`, UTL-EDGE-01, where `inherit` on children would not work), is skipped when `CSS.registerProperty` is unavailable, and registration errors (already registered) are ignored.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:31-69,283-285`; `packages/utils/src/platform/engine.ts:7-8`. Test: none. Docs: `components/scroll-area/page.mdx` §Gradient scroll fade ("inheritance to children is disabled").

### 5.4 Content element

| Attribute / style | Value | Req | Writer |
|---|---|---|---|
| `role` | `"presentation"` (overridable) | SCR-ARIA-02 | C# |
| `style.min-width` | `fit-content` | SCR-DOM-13 | C# |
| seven state data attributes | as on the Root | SCR-DOM-14 | C# (overflow); JS (`data-scrolling`) |

- **SCR-DOM-13** — MUST. The Content element carries inline `min-width: fit-content`, so that content wider than the viewport widens the Content box and is therefore reflected in the Viewport's `scrollWidth`.
  Source: `scroll-area/content/ScrollAreaContent.tsx:67-69`. Test: none directly (horizontal overflow tests in `ScrollAreaRoot.test.tsx` rely on it: "applies data attributes on root, viewport and scrollbars based on overflow and edges"). Docs: `CHANGELOG.md v1.0.0-alpha.8 "Add Content part"`.
- **SCR-DOM-14** — MUST. The Content element carries the same seven state data attributes as the Root (SCR-DOM-03).
  Source: `scroll-area/content/ScrollAreaContent.tsx:60-63`; `scroll-area/content/ScrollAreaContentDataAttributes.ts:4-28`. Test: [L2] `ScrollAreaRoot.test.tsx` "applies data attributes on root, viewport and scrollbars based on overflow and edges" (content flags) (L4 for the `data-scrolling` clause).
- **SCR-DOM-29** — MUST NOT. No part writes padding or margin onto the Viewport or its children to reserve space for the custom scrollbars; the scrollbars overlay the content (SCR-DOM-17).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:365-407`; `scroll-area/content/ScrollAreaContent.tsx:64-71`. Test: [L4] `ScrollAreaRoot.test.tsx` "should not add padding for overlay scrollbars".

### 5.5 Scrollbar element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| element | rendered iff `keepMounted \|\| hasOverflow<axis>` | SCR-DOM-15 | C# |
| `aria-hidden` | `"true"` (overridable) | SCR-ARIA-04 | C# |
| `data-orientation` | `"vertical"` / `"horizontal"` | SCR-DOM-16 | C# |
| `data-hovering` | `hovering` | SCR-DOM-16 | JS |
| `data-scrolling` | axis `scrolling` | SCR-DOM-16 | JS |
| six overflow attributes | as on the Root | SCR-DOM-16 | C# |
| `data-id` | `<root id>-scrollbar` | SCR-DOM-21 | C# |
| `style.position` / `touch-action` / `user-select` / `-webkit-user-select` | `absolute` / `none` / `none` / `none` | SCR-DOM-17 | C# |
| `style.top`, `bottom`, `inset-inline-end` (vertical) | `0`, `var(--scroll-area-corner-height)`, `0` | SCR-DOM-18 | C# |
| `style.inset-inline-start`, `inset-inline-end`, `bottom` (horizontal) | `0`, `var(--scroll-area-corner-width)`, `0` | SCR-DOM-18 | C# |
| `style.visibility` | `hidden` until measured, non-`keepMounted` only | SCR-DOM-19 | C# |
| `style.--scroll-area-thumb-height` (vertical) / `--scroll-area-thumb-width` (horizontal) | `<n>px` | SCR-DOM-20 | C# → JS |

- **SCR-DOM-15** — MUST. A Scrollbar with `keepMounted = false` renders no element at all (no DOM node, DOM handles `null`) while its axis has no overflow — in particular before the first measurement — and renders its element as soon as a measurement reports overflow on its axis; it is removed again when overflow disappears. With `keepMounted = true` the element is always rendered.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:65-67,228-230`. Test: [L2] `ScrollAreaScrollbar.test.tsx` "registers after the horizontal scrollbar becomes visible"; `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing".
- **SCR-DOM-16** — MUST. The Scrollbar element carries `data-orientation` with the value of its `orientation` prop, `data-hovering` while `hovering` is `true`, `data-scrolling` while the scrolling flag of **its own axis** is `true`, and the six overflow attributes of SCR-DOM-03 for **both** axes with the Root's values.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:57-62,221-226`; `scroll-area/scrollbar/ScrollAreaScrollbarDataAttributes.ts:5-37`. Test: [L2] `ScrollAreaScrollbar.test.tsx` "sets the orientation data attribute", "adds [data-scrolling] attribute when viewport is scrolled in the correct direction", "applies data attributes on vertical and horizontal scrollbars", "detects a viewport that is already hovered on mount" (L4 for the `data-hovering` and `data-scrolling` clauses).
- **SCR-DOM-17** — MUST. The Scrollbar element carries inline `position: absolute`, `touch-action: none` (touch contacts on the track and thumb are delivered as pointer events rather than panning the page), `user-select: none` and `-webkit-user-select: none` (no text selection starts from a scrollbar press).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:199-203`. Test: none. Docs: `CHANGELOG.md v1.0.0-beta.2 "Disable user-select on scrollbar and non-main button interactions"`.
- **SCR-DOM-18** — MUST. A vertical Scrollbar carries inline `top: 0`, `bottom: var(--scroll-area-corner-height)`, `inset-inline-end: 0`; a horizontal Scrollbar carries `inset-inline-start: 0`, `inset-inline-end: var(--scroll-area-corner-width)`, `bottom: 0`. The logical inset properties follow the element's CSS `direction` (SCR-I18N-04), and the corner variables shorten each track by the other scrollbar's thickness once the Corner is sized (SCR-DOM-02).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:205-217`. Test: none directly (`ScrollAreaViewport.test.tsx` overscroll tests assert thumb pinning against the track's bounding rect). Docs: `CHANGELOG.md v1.0.0-alpha.5 "Read DirectionProvider and use logical positioning CSS props"`.
- **SCR-DOM-19** — MUST. A Scrollbar with `keepMounted = false` carries inline `visibility: hidden` while the measured flag (SCR-STA-19) is `false`; a `keepMounted` Scrollbar never carries it. (Because a non-`keepMounted` Scrollbar is only rendered after a measurement, this rule is observable only through the absence of `visibility: hidden` once rendered; see §16 Q3.)
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:65,204`. Test: [L2] `ScrollAreaRoot.test.tsx` "shows scrollbars after mount compute before the first ResizeObserver measurement", "shows keepMounted scrollbar track and thumb after mount compute" (computed `visibility` is `visible`).
- **SCR-DOM-20** — MUST. A vertical Scrollbar carries the inline custom property `--scroll-area-thumb-height`, a horizontal one `--scroll-area-thumb-width`, equal to the thumb size of SCR-STA-17 for its axis formatted `<number>px` (number serialized per UTL-DOM-37) (`0px` before the first measurement). The Thumb consumes it (SCR-DOM-23).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:210,216`; `scroll-area/scrollbar/ScrollAreaScrollbarCssVars.ts:5,10`; see UTL-DOM-37. Test: [L4] `ScrollAreaRoot.test.tsx` "should correctly set thumb height and width based on scrollable content" (`40px` read from the Thumb's computed style), "recomputes thumb size when becoming visible without requiring scroll" (not `0px`).
- **SCR-DOM-21** — MUST. The Scrollbar element carries `data-id` equal to the Root's generated id followed by `-scrollbar` (the same value on a vertical and a horizontal Scrollbar of one Root; see §16 Q1); absent when no id is available.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:121`. Test: none.

### 5.6 Thumb element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `data-orientation` | `"vertical"` / `"horizontal"` | SCR-DOM-22 | C# |
| `data-scrolling` | axis `scrolling` | SCR-DOM-22 | JS |
| `style.visibility` | `hidden` until measured | SCR-DOM-23 | C# |
| `style.height` (vertical) / `style.width` (horizontal) | `var(--scroll-area-thumb-height)` / `var(--scroll-area-thumb-width)` | SCR-DOM-23 | C# |
| `style.transform` | `translate3d(…)`, written directly | SCR-DOM-24 | JS |
| `style.--scroll-area-thumb-height` / `-width` | `<n>px` only while overscrolling, written directly | SCR-DOM-25 | JS |

- **SCR-DOM-22** — MUST. The Thumb element carries `data-orientation` with the enclosing Scrollbar's orientation and `data-scrolling` while the scrolling flag of that axis is `true`; it carries no overflow or hovering attributes.
  Source: `scroll-area/thumb/ScrollAreaThumb.tsx:34-41`; `scroll-area/thumb/ScrollAreaThumbDataAttributes.ts:5,9`. Test: [L2] `ScrollAreaThumb.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled in the correct direction" (L4 for the `data-scrolling` clause). Docs: `CHANGELOG.md v1.6.0 "Add scrolling state to Thumb"`.
- **SCR-DOM-23** — MUST. The Thumb element carries inline `visibility: hidden` while the measured flag is `false` (regardless of `keepMounted`), and inline `height: var(--scroll-area-thumb-height)` (vertical) or `width: var(--scroll-area-thumb-width)` (horizontal) so that its length is the computed thumb size.
  Source: `scroll-area/thumb/ScrollAreaThumb.tsx:48-53`. Test: [L2] `ScrollAreaRoot.test.tsx` "shows keepMounted scrollbar track and thumb after mount compute". Docs: `CHANGELOG.md v1.0.0-beta.4 "Fix thumb size flicker"`.
- **SCR-DOM-24** — MUST. Every measurement that finds both the Scrollbar and the Thumb of an axis writes the Thumb's inline `transform`: `translate3d(0,<offsetY>px,0)` for the vertical thumb and `translate3d(<offsetX>px,0,0)` for the horizontal thumb, where the offset is `(clampedScroll / maxScroll) × maxThumbOffset` (plus the overscroll correction of §4.6) and `maxThumbOffset = trackLength − thumbLength − scrollbarPadding − thumbMargin`; in RTL the horizontal offset is negated (SCR-I18N-02). A Thumb whose Scrollbar is absent, or a drag with no Viewport, leaves the transform untouched (empty). Numeric values in these styles are serialized per UTL-DOM-37.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:214-248,490`; see UTL-DOM-37. Test: [L4] `ScrollAreaViewport.test.tsx` overscroll tests (thumb top/bottom/left/right pinned to the track rect); `ScrollAreaThumb.test.tsx` "handles a thumb gesture when no viewport is mounted" (`style.transform` stays `''`).
- **SCR-DOM-25** — MUST. While the viewport's scroll offset on an axis is outside `[0, max]` (WebKit rubber-banding), the Thumb element carries the inline custom property `--scroll-area-thumb-height` (vertical) / `--scroll-area-thumb-width` (horizontal) set to `max(16, size × content / (content + |overscroll|))` px, overriding the Scrollbar's resting value; when the offset is back in range the inline property is removed (set to the empty string) so the Scrollbar's value applies again. The thumb stays pinned to the overscrolled edge (offset `0` at the start edge; `offset + size − shrunkSize` at the end edge).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:472-492`. Test: [L4] `ScrollAreaViewport.test.tsx` "shrinks and pins the thumb to the start edge while overscrolling past the top", "shrinks and pins the thumb to the end edge while overscrolling past the bottom", "restores the resting thumb size once the viewport settles back into range", and the four "shrinks and pins the horizontal thumb to the inline start/end while overscrolling (LTR/RTL)" tests. Docs: `CHANGELOG.md v1.7.0 "Add WebKit overscroll feedback to <ScrollArea.Thumb>"`.

### 5.7 Corner element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| element | rendered iff `cornerHidden === false` | SCR-DOM-26 | C# |
| `aria-hidden` | `"true"` (overridable) | SCR-ARIA-05 | C# |
| `style.position` / `bottom` / `inset-inline-end` | `absolute` / `0` / `0` | SCR-DOM-27 | C# |
| `style.width` / `style.height` | corner size in px | SCR-DOM-27 | C# → JS |
| data attributes | none | SCR-DOM-28 | C# |

- **SCR-DOM-26** — MUST. The Corner renders no element while `cornerHidden` is `true` (before the first measurement and whenever either axis lacks overflow), and renders its element once both axes overflow.
  Source: `scroll-area/corner/ScrollAreaCorner.tsx:38-42`. Test: [L2] `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing", "clears corner, overflow attributes, and metrics when content stops overflowing".
- **SCR-DOM-27** — MUST. The Corner element carries inline `position: absolute`, `bottom: 0`, `inset-inline-end: 0`, and `width`/`height` equal to the corner size of SCR-STA-18 in px (`0px` on the render in which it first appears, then the measured size).
  Source: `scroll-area/corner/ScrollAreaCorner.tsx:26-32`. Test: [L2] `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing" (`style.width === '11px'`, `style.height === '13px'`) (L4 for the measured `width`/`height` clause).
- **SCR-DOM-28** — MUST NOT. The Corner element carries no state data attributes (its state is empty).
  Source: `scroll-area/corner/ScrollAreaCorner.tsx:21-36`. Test: none.

### 5.8 ARIA

Scroll Area exposes no ARIA roles or properties beyond `aria-hidden` on the decorative parts; there is no `role="scrollbar"`, no `aria-orientation`, `aria-valuenow`, `aria-valuemin`/`aria-valuemax` or `aria-controls` anywhere, and no ids are generated for ARIA cross-references. The Viewport is an ordinary focusable scroll container (SCR-DOM-08).

- **SCR-ARIA-01** — MUST. The Root element carries `role="presentation"` unless the consumer supplies a `role`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:266`. Test: none.
- **SCR-ARIA-02** — MUST. The Content element carries `role="presentation"` unless the consumer supplies a `role`.
  Source: `scroll-area/content/ScrollAreaContent.tsx:66`. Test: none.
- **SCR-ARIA-03** — MUST NOT. The Viewport element carries no `role` (in particular not `presentation`, which would conflict with it being focusable).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:365-407`. Test: [L2] `ScrollAreaViewport.test.tsx` "does not use a presentational role on the focusable viewport". Docs: `CHANGELOG.md v1.9.0 "Remove presentational viewport role"`.
- **SCR-ARIA-04** — MUST. The Scrollbar element carries `aria-hidden="true"`; a consumer-supplied `aria-hidden` (including an explicit `undefined`, which removes the attribute) replaces it.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:122`; RND-DOM-19. Test: [L2] `ScrollAreaScrollbar.test.tsx` "is hidden from the accessibility tree by default", "allows overriding aria-hidden". Docs: `CHANGELOG.md v1.8.0 "Hide group labels and scrollbars from the accessibility tree"`.
- **SCR-ARIA-05** — MUST. The Corner element carries `aria-hidden="true"`; a consumer-supplied `aria-hidden` (including `undefined`) replaces it.
  Source: `scroll-area/corner/ScrollAreaCorner.tsx:25`. Test: [L2] `ScrollAreaCorner.test.tsx` "is hidden from the accessibility tree by default", "allows overriding aria-hidden".
- **SCR-ARIA-06** — MUST NOT. No part emits any other `aria-*` attribute or role; the Thumb and the Viewport carry neither `role` nor `aria-*`.
  Source: `scroll-area/thumb/ScrollAreaThumb.tsx:42-56`; `scroll-area/viewport/ScrollAreaViewport.tsx:365-407`. Test: none.

## 6. Interactions

### 6.1 Keyboard

Scroll Area intercepts no key. Keyboard scrolling of a focused Viewport (arrows, Page Up/Down, Home/End, Space) is the browser's native behavior for an `overflow: scroll` element and produces ordinary `scroll` events that the component measures (SCR-PTR-33).

| Key | Context | Behavior | Req |
|---|---|---|---|
| any key | Viewport focused | Not intercepted; no `preventDefault`. The `keydown` only marks the scroll area as user-interacted so that the resulting scroll is attributed to the user (`data-scrolling`). | SCR-KEY-01, SCR-KEY-02 |
| <kbd>Tab</kbd> | anywhere | Native focus movement; the Viewport is a tab stop only while it overflows (SCR-DOM-08); Scrollbar, Thumb, Corner and Content are never tab stops (SCR-FOC-01). | SCR-KEY-01 |

- **SCR-KEY-01** — MUST NOT. No part attaches a key handler that calls `preventDefault`, `stopPropagation` or changes the scroll position; all keyboard scrolling is native.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:406` (the only key handler), `scroll-area/root/ScrollAreaRoot.tsx:265-278`, `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:120-219`. Test: none.
- **SCR-KEY-02** — MUST. A `keydown` delivered to the Viewport element clears the *programmatic* marker (SCR-STA-14), so a scroll that follows is user-driven.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:361-363,406`. Test: none (the equivalent `pointerenter` path is tested by `ScrollAreaViewport.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled").

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `pointerdown`, button 0, on Thumb | any pointer type | Start drag: record pointer id, client X/Y, viewport `scrollTop`/`scrollLeft`, orientation; disable scroll snap; capture the pointer on the Thumb. | SCR-PTR-01, SCR-PTR-03, SCR-PTR-04 |
| `pointerdown`, button ≠ 0, on Thumb or track | | Ignored entirely (no capture, no snap change, no scroll). | SCR-PTR-02 |
| `pointerdown` while a drag holds capture | second pointer | Ignored; if capture was silently lost, the new pointer takes over the drag. | SCR-PTR-11 |
| `pointermove` on Thumb, active pointer, primary button held | | Map delta → scroll offset (§6.2.1); `preventDefault`; mark axis scrolling. | SCR-PTR-05, SCR-PTR-06, SCR-PTR-13 |
| `pointermove`, active pointer, primary button **not** held | release went missing | Treated as a release (SCR-PTR-09). | SCR-PTR-08 |
| `pointermove`, other pointer id | | Ignored. | SCR-PTR-07 |
| `pointerup` / `pointercancel`, active pointer, on Thumb or Scrollbar | | End drag: clear id, clear axis scrolling immediately, restore scroll snap, release capture if held. | SCR-PTR-09 |
| `pointerup` / `pointercancel`, other pointer id | | Ignored. | SCR-PTR-10 |
| any of the above with no Viewport mounted | | Drag bookkeeping runs but nothing scrolls, no `data-scrolling`, no transform. | SCR-PTR-12 |
| `pointerdown`, button 0, on Scrollbar track (not over the Thumb) | | Jump: scroll so the thumb centers under the pointer (§6.2.2), disable snap first, mark scrolling, then start a drag from there. | SCR-PTR-14, SCR-PTR-16…19 |
| track press with no Viewport / no Thumb / thumb longer than track | | Ignored (no scroll, no snap change, no drag). | SCR-PTR-15 |
| `mousedown`, any button, on Scrollbar or Thumb | | `preventDefault` so focus does not move. | SCR-PTR-20 |
| `wheel` over Scrollbar | no `ctrlKey`, non-zero delta on the scrollbar's axis, not at the edge in the delta's direction | Scroll the viewport by the delta (clamped), `preventDefault`, mark axis scrolling. | SCR-PTR-21, SCR-PTR-22, SCR-PTR-25 |
| `wheel` over Scrollbar | `ctrlKey`, zero delta, or at the edge | Not consumed: no `preventDefault`, no scroll, no `data-scrolling`; the event chains to ancestors. | SCR-PTR-22, SCR-PTR-23, SCR-PTR-24 |
| `pointerenter` / `pointermove` on Root subtree | non-touch pointer, target inside Root | `hovering = true`. | SCR-PTR-27 |
| `pointerleave` on Root | any pointer | `hovering = false`. | SCR-PTR-28 |
| `pointerenter` / `pointermove` / `pointerdown` on Root subtree | `pointerType === 'touch'` | touch modality on; hover unchanged (never set by touch). | SCR-PTR-29, SCR-PTR-31 |
| `wheel` / `pointermove` / `pointerenter` on Viewport | | Mark user interaction (programmatic marker off). | SCR-PTR-32 |
| `scroll` on Viewport | | Measure; attribute to user per SCR-STA-13; restart 100 ms restore timer. | SCR-PTR-33 |

#### 6.2.1 Thumb drag

- **SCR-PTR-01** — MUST. A `pointerdown` with `button === 0` on the Thumb starts a drag: the pointer id, the pointer's `clientX`/`clientY`, the Viewport's current `scrollTop`/`scrollLeft` and the drag orientation are recorded (SCR-STA-21), and pointer capture for that pointer id is set on the Thumb of that orientation (when a Thumb element exists).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:121-155`; `scroll-area/thumb/ScrollAreaThumb.tsx:44`. Test: [L4] `ScrollAreaThumb.test.tsx` "updates LTR scroll position and pointer capture state" (`setPointerCapture` called once).
- **SCR-PTR-02** — MUST. A `pointerdown` whose `button` is not `0` (middle, secondary, pen barrel, …) on the Thumb or the Scrollbar track is ignored: no drag state is recorded, no pointer capture is set, the Viewport's scroll snap and scroll position are unchanged.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:122-124`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:124-126`. Test: [L4] `ScrollAreaThumb.test.tsx` "ignores non-primary pointer presses"; `ScrollAreaScrollbar.test.tsx` "ignores non-primary pointer presses".
- **SCR-PTR-03** — MUST. When a drag starts (thumb press or track press) and a Viewport is mounted, the Viewport's inline `scroll-snap-type` is saved and replaced with `none`, so that CSS scroll snapping does not quantize the positions assigned during the drag. The save is guarded: if a value is already saved (a drag is in progress), nothing is overwritten.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:107-119,145-150`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:172-176`. Test: [L4] `ScrollAreaThumb.test.tsx` "disables viewport scroll snap while dragging and restores it on release", "ignores a second pointer while a drag is active"; `ScrollAreaScrollbar.test.tsx` "does not snap the initial jump-to-click position". Docs: `CHANGELOG.md v1.7.0 "Prevent scroll snapping while dragging the thumb"`; experiment `scroll-area/scroll-area-snap.tsx`.
- **SCR-PTR-04** — MUST. The drag orientation is read from the `data-orientation` attribute of the element on which the handler is registered (the Thumb for a thumb press, the Scrollbar for a track press); the matching Thumb receives the pointer capture and the matching Scrollbar/Thumb boxes are used by the move mapping.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:141-143,152-154`. Test: [L4] `ScrollAreaThumb.test.tsx` "clears horizontal scrolling state on pointer cancel" (horizontal thumb drives `scrollLeft`); `ScrollAreaScrollbar.test.tsx` "clears track drag state on pointer cancel".
- **SCR-PTR-05** — MUST. A `pointermove` on the Thumb from the active pointer with the primary button held (`buttons & 1`) sets the Viewport's scroll offset on the drag axis to `startScroll + (delta / maxThumbOffset) × (scrollSize − clientSize)`, where `delta` is the pointer's client displacement since drag start on that axis and `maxThumbOffset = trackLength − thumbLength − scrollbarPadding − thumbMargin` (with `getOffset` of §4.6, `offsetHeight/offsetWidth` for the lengths); the event's default action is cancelled; and the axis scrolling flag is set with its 500 ms timer (SCR-STA-11). The browser clamps the assigned offset to the scrollable range. Server: relaxed, see §9.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:184-236`. Test: [L4] `ScrollAreaThumb.test.tsx` "updates LTR scroll position and pointer capture state", "ends a drag whose release was missed instead of scrolling on hover" (`scrollTop` increases with each move), "clears scrolling state on pointer cancel without releasing stale capture" (`data-scrolling` after a move).
- **SCR-PTR-06** — MUST. When `maxThumbOffset ≤ 0` (the 16 px minimum thumb fills or exceeds the track), the drag ratio is `0`: moves leave the scroll offset exactly at its drag-start value instead of jumping to an extreme.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:216-221`. Test: [L3] `ScrollAreaScrollbar.test.tsx` "does not jump the scroll when dragging a thumb that fills the track", "does not jump the scroll when dragging a thumb taller than the track". Docs: `CHANGELOG.md v1.7.0 "Fix thumb-drag divide-by-zero and per-scroll re-render"`.
- **SCR-PTR-07** — MUST. A `pointermove` whose `pointerId` is not the active drag's pointer id is ignored (it neither scrolls nor ends the drag).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:185-187`. Test: [L4] `ScrollAreaThumb.test.tsx` "ends a drag whose release was missed instead of scrolling on hover" (a second pointer with `buttons: 0` hovering the thumb changes nothing).
- **SCR-PTR-08** — MUST. A `pointermove` from the active pointer whose `buttons` value has bit 1 clear (primary button no longer held) is treated as the missed release: the drag ends exactly as in SCR-PTR-09 and the move does not scroll.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:189-197`. Test: [L4] `ScrollAreaThumb.test.tsx` "ends a drag whose release was missed instead of scrolling on hover". Docs: `CHANGELOG.md v1.7.0 "End thumb drag when the primary button is no longer held"`.
- **SCR-PTR-09** — MUST. A `pointerup` or `pointercancel` from the active pointer (on the Thumb or the Scrollbar), or the missed-release move of SCR-PTR-08, ends the drag: the active pointer id is cleared; the scrolling flag of the drag axis is set to `false` immediately (without waiting for the 500 ms timer); the Viewport's inline `scroll-snap-type` is restored to the saved value (when a Viewport is still mounted) and the saved value is discarded; and pointer capture is released only if the Thumb still holds capture for that pointer id (so a `pointercancel`, which releases capture implicitly, does not throw).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:157-182`; `scroll-area/thumb/ScrollAreaThumb.tsx:46-47`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:195-198`. Test: [L4] `ScrollAreaThumb.test.tsx` "clears scrolling state on pointer cancel without releasing stale capture", "clears horizontal scrolling state on pointer cancel", "restores viewport scroll snap on pointer cancel", "uses the negative RTL range and clears scrolling on pointer cancel"; `ScrollAreaScrollbar.test.tsx` "clears track drag state on pointer cancel", "does not snap the initial jump-to-click position" (release re-snaps).
- **SCR-PTR-10** — MUST. A `pointerup`/`pointercancel` whose `pointerId` differs from the active pointer id is ignored (the drag continues, snap stays disabled).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:158-160`. Test: [L4] `ScrollAreaThumb.test.tsx` "ignores a second pointer while a drag is active" (`pointerup` of pointer 2 leaves `scroll-snap-type: none`).
- **SCR-PTR-11** — MUST. A `pointerdown` (button 0) while a drag is active is ignored when the active Thumb still holds pointer capture for the active pointer id; when it does not (the browser dropped capture without a `pointerup`/`pointercancel`), the new pointer takes over as the active drag (new id, start coordinates and start offsets recorded; snap stays disabled because the saved value is kept).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:126-136`. Test: [L4] `ScrollAreaThumb.test.tsx` "ignores a second pointer while a drag is active", "lets a new pointer take over when capture was silently dropped".
- **SCR-PTR-12** — MUST. With no Viewport mounted, a thumb drag still records and clears its bookkeeping but moves scroll nothing: no scrolling flag is set and the Thumb's inline transform is not written.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:145-150,199-202`. Test: [L4] `ScrollAreaThumb.test.tsx` "handles a thumb gesture when no viewport is mounted".
- **SCR-PTR-13** — MUST. The drag mapping of SCR-PTR-05 applies the client-space delta directly in both reading directions; in RTL the horizontal scroll range is `[−max, 0]`, so dragging the horizontal thumb toward the left (negative delta) yields a negative `scrollLeft`, and the thumb (whose offset is mirrored by SCR-I18N-02) follows the pointer.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:220-232`. Test: [L3] `ScrollAreaThumb.test.tsx` "uses the negative RTL range and clears scrolling on pointer cancel".

Transcribed drag mapping:

```
dragMove(event):                                                 // ScrollAreaRoot.tsx:184-236
  if event.pointerId ≠ activePointerId: return                   // SCR-PTR-07
  if (event.buttons mod 2) = 0: release(event); return           // SCR-PTR-08
  viewport := Viewport element; if null: return                  // SCR-PTR-12
  vertical := activeOrientation = "vertical"
  thumb := vertical ? thumbY : thumbX ; scrollbar := vertical ? scrollbarY : scrollbarX
  if thumb is null or scrollbar is null: return
  axis := vertical ? y : x
  pad := getOffset(scrollbar, padding, axis) ; margin := getOffset(thumb, margin, axis)
  thumbLen := vertical ? thumb.offsetHeight : thumb.offsetWidth
  trackLen := vertical ? scrollbar.offsetHeight : scrollbar.offsetWidth
  maxThumbOffset := trackLen − thumbLen − pad − margin
  delta := vertical ? event.clientY − startY : event.clientX − startX
  ratio := (maxThumbOffset ≤ 0) ? 0 : delta / maxThumbOffset     // SCR-PTR-06
  contentLen := vertical ? viewport.scrollHeight : viewport.scrollWidth
  viewLen := vertical ? viewport.clientHeight : viewport.clientWidth
  start := vertical ? startScrollTop : startScrollLeft
  next := start + ratio × (contentLen − viewLen)
  if vertical: viewport.scrollTop := next else viewport.scrollLeft := next   // browser clamps
  event.preventDefault()
  markScrolling(axis)                                            // SCR-STA-11
```

#### 6.2.2 Track press

- **SCR-PTR-14** — MUST. A `pointerdown` with `button === 0` on the Scrollbar whose target is not inside the Thumb scrolls the Viewport so that the thumb's center lands under the pointer: with `click = pointerClient − trackRectStart − thumbLength/2 − scrollbarPadding + thumbMargin/2` and `maxThumbOffset` as in SCR-PTR-05, the offset becomes `(click / maxThumbOffset) × (scrollSize − clientSize)` (vertical, or horizontal LTR); the value is not clamped by the component (the browser clamps the assignment).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:123-189`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "scrolls down when clicking below the thumb on a vertical track", "scrolls right when clicking the end of a horizontal LTR track", "marks the scroll area as scrolling when pressing the track" (`scrollTop` becomes non-zero), "does not snap the initial jump-to-click position" (lands within 1 px of the aimed offset).
- **SCR-PTR-15** — MUST. The track press does nothing (no scroll, no snap change, no drag, no scrolling flag) when the button is not primary (SCR-PTR-02), when the press target is inside the Thumb, when no Viewport is mounted, when no Thumb or Scrollbar element of that axis exists, or when `maxThumbOffset ≤ 0`.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:124-146,161-167`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "handles a track press when no viewport is mounted", "does not start a track gesture without a thumb", "does not jump the scroll when clicking a track whose thumb fills the track", "does not jump the scroll when clicking a track whose thumb is taller than the track".
- **SCR-PTR-16** — MUST. "Inside the Thumb" is decided on the event's composed-path target (`composedPath()[0]`, falling back to `target`; UTL-DOM-22) with a shadow-DOM-aware containment check (UTL-DOM-20), so a press on a Thumb inside a shadow root that is retargeted to the track is still treated as a thumb press.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:128-135`; `packages/utils/src/shadowDom.ts:13,72`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "ignores thumb clicks when the native path differs from the synthetic target".
- **SCR-PTR-17** — MUST. The track press disables scroll snap (SCR-PTR-03) **before** assigning the jump offset, so the assigned position is not quantized to a snap point; releasing the pointer restores snapping, which then re-snaps.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:172-184`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "does not snap the initial jump-to-click position".
- **SCR-PTR-18** — MUST. For a horizontal Scrollbar in RTL, the jump assigns `scrollLeft = −(1 − ratio) × (scrollWidth − clientWidth)` (ratio measured from the track's left edge), so a press near the left (inline-end) edge scrolls into the negative range toward the end.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:178-184`. Test: [L3] `ScrollAreaScrollbar.test.tsx` "scrolls into the negative RTL range when clicking a horizontal RTL track".
- **SCR-PTR-19** — MUST. After the jump, the new position is reported to the scrolling flags (SCR-STA-11; the axis whose offset changed gets `data-scrolling`) and a drag is started as in SCR-PTR-01 with the press as its origin (so the subsequent `pointermove`s continue from the jumped position; pointer capture is set on the Thumb).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:186-188`; `scroll-area/root/ScrollAreaRoot.tsx:121-155`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "marks the scroll area as scrolling when pressing the track", "clears track drag state on pointer cancel" (a move after cancel no longer scrolls).
- **SCR-PTR-20** — MUST. A `mousedown` on the Scrollbar, with any button, including one bubbling from the Thumb, has its default action cancelled, so pressing a scrollbar never moves focus away from the active element (native scrollbars behave the same).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:190-194`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "cancels a $name press on the track so focus stays on the active element" (3 variants: `primary`, `middle`, `secondary`), "cancels the press on the thumb so focus stays on the active element". Docs: `CHANGELOG.md v1.8.0 "Prevent scrollbars from stealing focus"`.

Transcribed track press:

```
trackPress(event):                                               // ScrollAreaScrollbar.tsx:123-189
  if event.button ≠ 0: return
  target := composedPath()[0] or event.target
  thumb := axis Thumb
  if thumb and contains(thumb, target): return                   // SCR-PTR-16
  viewport := Viewport element; if null: return
  scrollbar := axis Scrollbar; if thumb is null or scrollbar is null: return
  margin := getOffset(thumb, margin, axis) ; pad := getOffset(scrollbar, padding, axis)
  thumbLen := vertical ? thumb.offsetHeight : thumb.offsetWidth
  rect := scrollbar.getBoundingClientRect()
  click := (vertical ? event.clientY − rect.top : event.clientX − rect.left) − thumbLen/2 − pad + margin/2
  contentLen := vertical ? viewport.scrollHeight : viewport.scrollWidth
  viewLen := vertical ? viewport.clientHeight : viewport.clientWidth
  trackLen := vertical ? scrollbar.offsetHeight : scrollbar.offsetWidth
  maxThumbOffset := trackLen − thumbLen − pad − margin
  if maxThumbOffset ≤ 0: return                                  // SCR-PTR-15
  ratio := click / maxThumbOffset
  maxScroll := contentLen − viewLen
  disableSnap()                                                  // SCR-PTR-17
  if vertical: viewport.scrollTop := ratio × maxScroll
  else if rtl:  viewport.scrollLeft := −(1 − ratio) × maxScroll  // SCR-PTR-18
  else:         viewport.scrollLeft := ratio × maxScroll
  reportScroll({ x: viewport.scrollLeft, y: viewport.scrollTop })   // SCR-PTR-19 → SCR-STA-11
  dragStart(event)                                               // orientation read from the Scrollbar
```

#### 6.2.3 Wheel over a scrollbar

- **SCR-PTR-21** — MUST. A `wheel` event on a rendered Scrollbar scrolls the Viewport along the Scrollbar's axis by the event's `deltaY` (vertical) or `deltaX` (horizontal), clamped to the axis's scroll range; the other delta component is ignored. Without a Viewport nothing happens.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:81-115`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "clamps vertical wheel scrolling at both edges" (790 + 50 → 800), "clamps horizontal LTR wheel scrolling at both edges".
- **SCR-PTR-22** — MUST. The wheel event's default action is cancelled only when the component actually moves the viewport; when the viewport is already at the edge in the delta's direction (`offset ≤ min && delta < 0` or `offset ≥ max && delta > 0`), the event is left uncancelled so it chains to the ancestor scroller/page, and no scrolling flag is set.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:100-107`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "preventDefaults only when it consumes the scroll, allowing chaining at edges", "does not mark the scroll area as scrolling when chaining at an edge".
- **SCR-PTR-23** — MUST. A wheel event with `ctrlKey` set (browser zoom gesture) or with a zero delta on the scrollbar's axis is ignored: not cancelled, no scroll, no scrolling flag.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:82-91`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "does not intercept browser zoom gestures", "ignores zero-delta wheel events".
- **SCR-PTR-24** — MUST. For a horizontal Scrollbar in RTL the clamping range is `[−max, 0]` (`max = scrollWidth − clientWidth`): a negative delta scrolls from `0` toward `−max`, and the edge test of SCR-PTR-22 uses these bounds.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:93-98,109-112`. Test: [L3] `ScrollAreaScrollbar.test.tsx` "allows horizontal scrolling away from the RTL start edge", "clamps horizontal RTL wheel scrolling at both edges".
- **SCR-PTR-25** — MUST. A consumed wheel scroll reports the new position to the scrolling flags (SCR-STA-11), so the Scrollbar's `data-scrolling` appears without any `scroll` event or Viewport interaction.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:114`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "marks the scroll area as scrolling when wheeling over the scrollbar", "marks the scroll area as scrolling when wheeling over the horizontal scrollbar".
- **SCR-PTR-26** — MUST. The wheel handler is a native, non-passive (`passive: false`) `wheel` listener registered on the Scrollbar element **after paint** of the render in which the Scrollbar element exists, re-registered when the reading direction changes, and removed when the element unmounts; it is therefore not a merged handler (a consumer `onWheel` runs independently and cannot veto it with `preventBaseUIHandler()`).
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:69-118`; `packages/utils/src/addEventListener.ts` (UTL-EVT-13). Test: [L4] `ScrollAreaScrollbar.test.tsx` "registers after the horizontal scrollbar becomes visible".

Transcribed wheel handler:

```
wheel(event):                                                    // ScrollAreaScrollbar.tsx:81-115
  if viewport is null or event.ctrlKey: return
  horizontal := not vertical
  prop := horizontal ? scrollLeft : scrollTop
  delta := horizontal ? event.deltaX : event.deltaY
  if delta = 0: return
  maxScroll := horizontal ? scrollWidth − clientWidth : scrollHeight − clientHeight
  minV := (horizontal and rtl) ? −maxScroll : 0
  maxV := (horizontal and rtl) ? 0 : maxScroll
  cur := viewport[prop]
  if (cur ≤ minV and delta < 0) or (cur ≥ maxV and delta > 0): return   // chain to ancestors
  event.preventDefault()
  viewport[prop] := min(maxV, max(minV, cur + delta))
  reportScroll({ x: viewport.scrollLeft, y: viewport.scrollTop })
```

#### 6.2.4 Hover, modality and viewport interaction

- **SCR-PTR-27** — MUST. A `pointerenter` or `pointermove` delivered to the Root (from any descendant) with `pointerType !== 'touch'` sets `hovering` to whether the event's target element is the Root element or a DOM descendant of it (events from a portal mounted logically inside the Root but outside its DOM subtree therefore clear hovering).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:242-249,267-268`; `packages/utils/src/shadowDom.ts:13` (UTL-DOM-20). Test: [L4] `ScrollAreaScrollbar.test.tsx` "adds [data-hovering] when the synthetic pointer target differs from the native path". Docs: `CHANGELOG.md v1.0.0-beta.3 "Prevent pointer events from sibling portals triggering hover"`.
- **SCR-PTR-28** — MUST. A `pointerleave` on the Root sets `hovering` to `false` regardless of pointer type.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:270-272`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "adds [data-hovering] when the synthetic pointer target differs from the native path" (cleared after `pointerleave`).
- **SCR-PTR-29** — MUST NOT. A touch pointer (`pointerType === 'touch'`) never sets `hovering`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:245`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "does not enter hover state for touch pointers".
- **SCR-PTR-30** — MUST. Post-commit, pre-paint after the Viewport mounts, if the Viewport element matches `:hover`, `hovering` is set to `true` (no enter event fires for an element mounted under a stationary pointer).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:301-307`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "detects a viewport that is already hovered on mount".
- **SCR-PTR-31** — MUST. Every `pointerenter`, `pointermove` and `pointerdown` delivered to the Root updates touch modality to `pointerType === 'touch'` (SCR-STA-16).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:238-243,267-269`. Test: [L4] `ScrollAreaViewport.test.tsx` "adds [data-scrolling] in touch modality even when the gesture delivers no events", "restores programmatic scroll suppression after modality flips back to mouse".
- **SCR-PTR-32** — MUST. A `wheel`, `pointermove` or `pointerenter` delivered to the Viewport element (together with `keydown`, SCR-KEY-02) clears the *programmatic* marker (SCR-STA-14). `pointerdown` on the Viewport is **not** such a marker.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:361-363,403-406`. Test: [L4] `ScrollAreaViewport.test.tsx` "adds [data-scrolling] attribute when viewport is scrolled" (after `pointerenter`), "keeps ignoring programmatic scrolls in mouse modality" (`pointerdown` alone does not count).
- **SCR-PTR-33** — MUST. On every `scroll` event of the Viewport (while a Viewport element exists): first the measurement of §4.6 runs synchronously (Viewport variables and Thumb transform are written during the event); then, if the scroll is user-driven (SCR-STA-13), the position is reported to the scrolling flags; then the 100 ms programmatic-restore timer is restarted (SCR-STA-14). With no Viewport element the handler does nothing.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:374-402`. Test: [L4] `ScrollAreaRoot.test.tsx` "applies data attributes on root, viewport and scrollbars based on overflow and edges" (attributes after `fireEvent.scroll` + microtasks); `ScrollAreaViewport.test.tsx` "handles a user scroll callback unmounting the viewport".

### 6.3 Focus management

Only the Viewport is focusable, through its computed `tabindex` (SCR-DOM-08, overridable per SCR-API-05). There is no focus trap, no initial focus and no focus return.

- **SCR-FOC-01** — MUST NOT. Root, Content, Scrollbar, Thumb and Corner carry no `tabindex` and never receive focus from the component; pressing a Scrollbar or Thumb does not move focus (SCR-PTR-20).
  Source: `scroll-area/root/ScrollAreaRoot.tsx:265-278`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:120-219`; `scroll-area/thumb/ScrollAreaThumb.tsx:42-56`; `scroll-area/corner/ScrollAreaCorner.tsx:23-34`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "cancels a $name press on the track so focus stays on the active element" (3 variants), "cancels the press on the thumb so focus stays on the active element".

### 6.4 Dismissal

Not applicable — Scroll Area has no open/closed state and nothing to dismiss.

## 7. Events and callbacks

Not applicable — no part defines a change callback, no `reason` values are emitted, and the [event-details-and-reasons](../architecture/event-details-and-reasons.md) contract is not used. Consumer DOM handlers (`onScroll`, `onWheel`, `onPointerDown`, …) are chained with the part's own handlers per RND-EVT-01…06 (consumer first; `preventBaseUIHandler()` vetoes the part's handler for that event), except the native wheel listener of SCR-PTR-26, which is independent of the merged `onWheel`.

## 8. Form integration

Not applicable — Scroll Area renders no form control, has no `name`/`value`, and does not register with Field or Form.

## 9. Lifecycle, mounting and animation

Vocabulary (conventions §4): **post-commit, pre-paint** = synchronously after the DOM has been updated and before paint; **after paint** = after the browser has painted; **microtask** = before the next macrotask; **N ms** = macrotask timer. Scroll Area has no transition statuses, no `data-starting-style`/`data-ending-style`, and no animation of its own; this section specifies *when the measurement of §4.6 runs* and when conditional parts appear.

- **SCR-ANI-01** — MUST. The first measurement runs one microtask after the post-commit, pre-paint phase of the render that mounts the Viewport. Its results (measured flag, overflow state, thumb size, hidden state) are applied in a subsequent synchronous commit, so by the time any macrotask runs, a keepMounted Scrollbar's track and Thumb are visible (SCR-DOM-19, SCR-DOM-23) and non-keepMounted Scrollbars whose axis overflows are being rendered.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:287-299`. Test: [L4] `ScrollAreaRoot.test.tsx` "shows scrollbars after mount compute before the first ResizeObserver measurement", "shows keepMounted scrollbar track and thumb after mount compute" (both with a mocked ResizeObserver that never fires on its own).
- **SCR-ANI-02** — MUST. A measurement is scheduled (one microtask after post-commit, pre-paint) whenever the hidden state changes (so newly rendered Scrollbar/Thumb/Corner elements are measured, see SCR-ANI-03), whenever the reading direction changes (SCR-I18N-01), and whenever any normalized threshold value changes (SCR-API-04).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:287-299`. Test: [L4] `ScrollAreaRoot.test.tsx` "recomputes horizontal overflow edges when direction changes", "recomputes overflow edges when overflowEdgeThreshold changes".
- **SCR-ANI-03** — MUST. Because a non-keepMounted Scrollbar and the Corner are only rendered after a measurement reports overflow, the geometry settles in two passes: the first measurement (without those elements) sets the hidden state and a provisional thumb size from the viewport alone; the elements then mount; the hidden-state change schedules a second measurement (SCR-ANI-02) that reads their boxes and produces the final thumb size, thumb transform and corner size. No scroll or resize is required for the second pass.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:199-204,261-269,287-299`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:228-230`; `scroll-area/corner/ScrollAreaCorner.tsx:38-40`. Test: [L4] `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing" (corner sized `11px × 13px` after a single resize notification), "should correctly set thumb height and width based on scrollable content".
- **SCR-ANI-04** — MUST. Post-commit, pre-paint after the Viewport mounts, a `ResizeObserver` observes the Viewport element (when the API exists, SCR-EDGE-08). Its first notification is ignored if the Viewport's `clientHeight`, `scrollHeight`, `clientWidth` and `scrollWidth` all equal the values recorded by the most recent measurement (it would duplicate SCR-ANI-01); otherwise, and for every later notification, a measurement runs synchronously in the notification.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:309-336`. Test: [L4] `ScrollAreaRoot.test.tsx` "recomputes thumb size when becoming visible without requiring scroll" (zero-size at mount, then shown), "recomputes corner size when content starts overflowing", "clears corner, overflow attributes, and metrics when content stops overflowing".
- **SCR-ANI-05** — MUST. Post-commit, pre-paint after the Content mounts, a `ResizeObserver` observes the Content element. Its first notification is ignored unless the measured flag (SCR-STA-19) was already `true` when the Content first rendered (Content mounted after the Viewport's initial measurement, in which case the first notification is what brings the overflow state up to date); every later notification runs a measurement synchronously.
  Source: `scroll-area/content/ScrollAreaContent.tsx:26-58`. Test: [L4] `ScrollAreaContent.test.tsx` "recomputes overflow when observed content resizes"; `ScrollAreaRoot.test.tsx` "measures content mounted after the viewport initial measurement".
- **SCR-ANI-06** — MUST. 0 ms (next macrotask) after the Viewport mounts, the finite animations running on the Viewport **and its subtree** (`getAnimations({ subtree: true })` filtered by TRN-ALG-02: `duration ≠ Infinity` and `iterations ≠ Infinity`, see [transitions-and-mounting](../architecture/transitions-and-mounting.md) TRN-ANI-05) are collected; if there are none nothing further happens; otherwise, once every one of them has settled (finished or been cancelled), a measurement runs (in the microtask following the last settlement). Infinite animations never block this; the wait holds no reference to the settled animations' objects (SCR-EDGE-07), and a settlement after the Viewport has unmounted runs nothing and raises no error (SCR-EDGE-06).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:338-353`; `utils/getFiniteAnimations.ts:4-9`. Test: [L4] `ScrollAreaViewport.test.tsx` "recomputes overflow after a subtree animation finishes with %s" (3 variants: `no infinite animation`, `infinite iterations`, `infinite duration`; overflow attribute appears only after the finite animation resolves).
- **SCR-ANI-07** — MUST. When the Viewport unmounts, its resize observation is disconnected and the pending 0 ms animation-collection timer is cleared; when the Content unmounts, its resize observation is disconnected; when the Root unmounts, the pending 500 ms scrolling timers are cleared; when the Viewport unmounts, the 100 ms programmatic-restore timer is cleared (UTL-EVT-03).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:115-116,355-358`; `scroll-area/content/ScrollAreaContent.tsx:55-57`; `scroll-area/root/ScrollAreaRoot.tsx:50-51`. Test: [L3] `ScrollAreaViewport.test.tsx` "ignores an animation finishing after its viewport unmounts".
- **SCR-ANI-08** — MUST. A measurement that finds `scrollHeight === 0` or `scrollWidth === 0` (the viewport or an ancestor is `display: none`, or nothing is laid out yet) records the metrics and sets the measured flag but changes no other state and writes no variables or transforms; the scroll area is measured correctly later by the resize notification that fires when it gains size (SCR-ANI-04).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:136-150`. Test: [L3] `ScrollAreaRoot.test.tsx` "recomputes thumb size when becoming visible without requiring scroll". Docs: `CHANGELOG.md v1.0.0-alpha.8 "Handle visibility change and nesting"`.
- **SCR-ANI-09** — MUST. The corner size is populated by the first measurement that runs while the Corner element is mounted (the second pass of SCR-ANI-03); on the render in which the Corner first appears its inline size is `0px × 0px` and the Root's corner variables are `0px`.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:261-269`; `scroll-area/corner/ScrollAreaCorner.tsx:26-32`. Test: [L4] `ScrollAreaRoot.test.tsx` "recomputes corner size when content starts overflowing"; `ScrollAreaCorner.test.tsx` "should apply correct corner size when both scrollbars are present".
- **SCR-ANI-10** — MUST NOT. `keepMounted` on the Scrollbar (SCR-API-07) and the Corner's conditional rendering (SCR-DOM-26) involve no transition status, no `data-starting-style`/`data-ending-style`, no animation wait and no deferred unmount: the elements are added or removed in the commit that changes the overflow state.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:65-67,228-230`; `scroll-area/corner/ScrollAreaCorner.tsx:38-40`. Test: [L2] `ScrollAreaRoot.test.tsx` "clears corner, overflow attributes, and metrics when content stops overflowing".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- SCR-PTR-05 — met on both modes because constraint C1 places the whole thumb-drag loop in JavaScript; listed so nobody moves it back to C#.
- RND-STA-01 for the mirrored fields `scrolling` and `hovering` (SCR-STA-01, SCR-STA-03, SCR-STA-04) — `data-scrolling` and `data-hovering` are JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.

## 10. Positioning

Not applicable — Scroll Area has no floating parts; the Scrollbars and Corner are absolutely positioned within the Root by the inline styles of SCR-DOM-18 and SCR-DOM-27.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (rendering pipeline) | [rendering-and-composition](../architecture/rendering-and-composition.md) §6.1 | Every part: `element: 'div'`, consumer attributes last. Root: `state` = Root state, `ref` = [consumer, root handle], mapping `scrollAreaStateAttributesMapping`. Viewport: `state` = Root state, `ref` = [consumer, viewport handle], same mapping. Content: `state` = Root state, `ref` = [consumer, content handle], same mapping. Scrollbar: `state` = Scrollbar state, `ref` = [consumer, vertical-or-horizontal scrollbar handle], same mapping; the element is discarded (`null` returned) when SCR-DOM-15 says not rendered. Thumb: `state` = Thumb state, `ref` = [consumer, vertical-or-horizontal thumb handle], default projection. Corner: no state, `ref` = [consumer, corner handle]; element discarded when `cornerHidden`. Sources: `ScrollAreaRoot.tsx:280-285`, `ScrollAreaViewport.tsx:409-414`, `ScrollAreaContent.tsx:60-73`, `ScrollAreaScrollbar.tsx:221-230`, `ScrollAreaThumb.tsx:39-57`, `ScrollAreaCorner.tsx:21-42`. |
| State → data-attribute projection, custom mapping | RND-DOM-01…10 | `scrollAreaStateAttributesMapping` (`scroll-area/root/stateAttributes.ts:5-15`): `hasOverflowX → data-has-overflow-x`, `hasOverflowY → data-has-overflow-y`, `overflowXStart → data-overflow-x-start`, `overflowXEnd → data-overflow-x-end`, `overflowYStart → data-overflow-y-start`, `overflowYEnd → data-overflow-y-end` (each `""` when `true`, nothing when `false`); `cornerHidden → nothing`. |
| Generated id (`useBaseUiId`) | RND-DOM-27, RND-EDGE-09 | Root generates one id (`ScrollAreaRoot.tsx:48`) used only for the `data-id` suffixes `-viewport` and `-scrollbar` (SCR-DOM-09, SCR-DOM-21); no consumer override, not rendered on the Root itself. |
| Scrollbar-hiding style element and CSP | RND-DOM-35; UTL-DOM-32, UTL-DOM-33, UTL-DOM-35 | Root renders `styleDisableScrollbar.getElement(nonce)` unless `disableStyleElements` (`ScrollAreaRoot.tsx:53,345`); Viewport applies `styleDisableScrollbar.className` (`ScrollAreaViewport.tsx:370`). |
| Reading direction (`useDirection`) | UTL-I18N-01…04 | Read by Viewport (`:105`) and Scrollbar (`:64`); `'ltr'` without a provider. |
| `Timeout` / `useTimeout` | UTL-EVT-01…03 | Root: two instances (`scrollYTimeout`, `scrollXTimeout`), 500 ms (`SCROLL_TIMEOUT`). Viewport: `scrollEndTimeout` 100 ms, `waitForAnimationsTimeout` 0 ms. |
| `addEventListener` | UTL-EVT-13 | Scrollbar: native `wheel` listener with `{ passive: false }` (`ScrollAreaScrollbar.tsx:117`). |
| `contains`, `getTarget` (shadow-DOM helpers) | UTL-DOM-20, UTL-DOM-22, UTL-ALG-17 | Root hover check `contains(rootEl, event.target)` (`:246`); Scrollbar thumb-press check `contains(thumbEl, getTarget(nativeEvent))` (`:128-133`). |
| `normalizeScrollOffset` | UTL-DOM-30, UTL-ALG-12 | Viewport measurement (`:164-173`), 1 px edge tolerance. |
| `clamp` | UTL-EDGE-14 | `applyOverscrollThumb` (`ScrollAreaViewport.tsx:481`). |
| `getFiniteAnimations` | TRN-ALG-02, TRN-ANI-05 | Viewport: `getFiniteAnimations(viewport, { subtree: true })` (`:341`) — the only caller in 1.9.0 that passes `subtree: true` (see §16 Q2). |
| `platform.engine.webkit` | UTL-EDGE-01 | Skips custom-property registration (`ScrollAreaViewport.tsx:48`). |
| Stable callbacks (`useStableCallback`) | UTL-EDGE-27 | `handleScroll`, `disableViewportSnap`, `handlePointerDown/Up/Move`, `computeThumbPosition` are stable trampolines; no component-specific configuration. |

## 12. Edge cases, invariants and dev warnings

Scroll Area has no development-only warnings; its only diagnostics are the three thrown structural errors below, which apply in every build.

- **SCR-EDGE-01** — MUST. Rendering `ScrollArea.Viewport`, `ScrollArea.Scrollbar`, `ScrollArea.Thumb`, `ScrollArea.Corner` or `ScrollArea.Content` outside a `ScrollArea.Root` throws `Error` with the message `Base UI: ScrollAreaRootContext is missing. ScrollArea parts must be placed within <ScrollArea.Root>.`
  Source: `scroll-area/root/ScrollAreaRootContext.ts:52-60`. Test: [L2] `ScrollAreaViewport.test.tsx` "throws a descriptive error when rendered outside <ScrollArea.Root>".
- **SCR-EDGE-02** — MUST. Rendering `ScrollArea.Content` outside a `ScrollArea.Viewport` (even inside a Root) throws `Error` with the message `Base UI: ScrollAreaViewportContext missing. ScrollAreaViewport parts must be placed within <ScrollArea.Viewport>.`
  Source: `scroll-area/viewport/ScrollAreaViewportContext.ts:12-20`. Test: [L2] `ScrollAreaContent.test.tsx` "throws a descriptive error when rendered outside <ScrollArea.Viewport>".
- **SCR-EDGE-03** — MUST. Rendering `ScrollArea.Thumb` outside a `ScrollArea.Scrollbar` (even inside a Root) throws `Error` with the message `Base UI: ScrollAreaScrollbarContext is missing. ScrollAreaScrollbar parts must be placed within <ScrollArea.Scrollbar>.`
  Source: `scroll-area/scrollbar/ScrollAreaScrollbarContext.ts:10-18`. Test: [L2] `ScrollAreaThumb.test.tsx` "throws a descriptive error when rendered outside <ScrollArea.Scrollbar>".
- **SCR-EDGE-04** — MUST. If a consumer `onScroll` handler on the Viewport removes the Viewport synchronously during the event, the component's own scroll handling (which runs after the consumer's, RND-EVT-02) detects the missing element and does nothing; no error is thrown.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:375-377`. Test: [L4] `ScrollAreaViewport.test.tsx` "handles a user scroll callback unmounting the viewport".
- **SCR-EDGE-05** — MUST. If a consumer handler removes the Scrollbar/Thumb during a drag `pointermove`, or removes the Viewport during a drag `pointerup`, the component's handlers complete without throwing: the move scrolls nothing (thumb/scrollbar handles are null) and the release clears the drag record, discards the saved snap value without restoring it, and skips the capture release.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:168-181,204-209`. Test: [L4] `ScrollAreaThumb.test.tsx` "handles the scrollbar unmounting from a user pointer-move callback", "handles the viewport unmounting from a user pointer-up callback".
- **SCR-EDGE-06** — MUST. A subtree animation (SCR-ANI-06) that settles after the Viewport has unmounted triggers no measurement and produces no unhandled promise rejection.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:345-352` (`.catch(() => {})`, null viewport check in the measurement). Test: [L3] `ScrollAreaViewport.test.tsx` "ignores an animation finishing after its viewport unmounts".
- **SCR-EDGE-08** — MUST. When `ResizeObserver` is not defined (server, legacy environments), neither the Viewport nor the Content observes resizes; measurements then run only at mount (SCR-ANI-01), on scroll, on dependency changes (SCR-ANI-02) and after subtree animations (SCR-ANI-06).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:310-313`; `scroll-area/content/ScrollAreaContent.tsx:30-32`. Test: none (the Root tests mock `ResizeObserver` with a controllable stub rather than removing it).
- **SCR-EDGE-09** — MUST NOT. No part logs a development warning or error; there are no `process.env.NODE_ENV` branches in the component.
  Source: `scroll-area/**` (no `warn(`/`console.error` calls). Test: none.
- **SCR-EDGE-10** — MUST. A part whose substitute element does not forward the DOM handle (so the part's internal handle stays `null`) still renders its element with all merged attributes; behaviors that need the handle (wheel listener registration, resize observation, drag geometry) are simply inert for that part.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:74-79`; `scroll-area/content/ScrollAreaContent.tsx:51-53`. Test: [L2] `ScrollAreaScrollbar.test.tsx` "supports a custom scrollbar renderer that does not forward its ref"; `ScrollAreaContent.test.tsx` "supports a custom content renderer that does not forward its ref" (L4 for the inert-behaviour clause).
- **SCR-EDGE-11** — MUST. Nested scroll areas are independent: every part binds to its nearest ancestor Root/Viewport/Scrollbar, and pointer or scroll events bubbling from an inner scroll area are processed by the outer Root's hover/modality handlers only (an inner Viewport's `scroll` event does not bubble; an inner pointer event updates the outer `hovering` because its target is inside the outer Root).
  Source: `scroll-area/root/ScrollAreaRootContext.ts:48-60`; `scroll-area/root/ScrollAreaRoot.tsx:242-249`. Test: none. Docs: `CHANGELOG.md v1.0.0-alpha.8 "Handle visibility change and nesting"`.
- **SCR-EDGE-12** — MUST. A scroll area mounted inside a hidden subtree (`display: none`) reports no overflow and a zero thumb size until it becomes visible; when it does, the resize notification (SCR-ANI-04, not deduplicated because the metrics changed) measures it and the thumb-size variables become non-zero without any scroll or other interaction.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:148-150,316-334`. Test: [L4] `ScrollAreaRoot.test.tsx` "recomputes thumb size when becoming visible without requiring scroll".
- **SCR-EDGE-13** — MUST. Server-rendered markup reflects the pre-measurement state (SCR-STA-08): Root with `role="presentation"`, `position: relative` and `0px` corner variables; Viewport with `tabindex="-1"`, `overflow: scroll`, the scrollbar-hiding class, no overflow attributes and no overflow variables; non-keepMounted Scrollbars and the Corner absent; a keepMounted Scrollbar present with `--scroll-area-thumb-*: 0px` and its Thumb with `visibility: hidden`; the `<style>` element of SCR-DOM-05 present. The first client measurement (SCR-ANI-01) then updates everything. The docs recommend a fallback in `var(--scroll-area-overflow-*-end, <px>)` for masks that must look right before hydration.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:18-21,265-278`; `scroll-area/viewport/ScrollAreaViewport.tsx:365-373`. Test: none. Docs: `components/scroll-area/page.mdx` §Gradient scroll fade ("SSR fallback").

### 12.1 Reading direction and RTL

- **SCR-I18N-01** — MUST. The reading direction used by every RTL rule below is the value of the nearest enclosing `DirectionProvider` (`'ltr'` when there is none; UTL-I18N-01…03 in [shared-utilities](../architecture/shared-utilities.md)); the component never reads the CSS `direction` or the `dir` attribute. Because the inline positioning of SCR-DOM-18/27 uses logical properties that *do* follow CSS `direction`, a consumer rendering RTL must set both (the docs' tests and experiments set `style={{ direction: 'rtl' }}` on the Root inside a `DirectionProvider direction="rtl"`).
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:105`; `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:64`; `internals/direction-context/DirectionContext.tsx:12-16`. Test: [L2] `ScrollAreaRoot.test.tsx` "correctly handles RTL", "recomputes horizontal overflow edges when direction changes". Docs: `CHANGELOG.md v1.5.0 "Fix RTL component behavior"`; experiment `scroll-area/scroll-area-inset.tsx`.
- **SCR-I18N-02** — MUST. In RTL the horizontal Thumb's inline transform is `translate3d(−offsetX px,0,0)`, i.e. the offset computed from the inline-start distance is applied leftward from the inline-start (right) edge, so the thumb sits at the right edge when `scrollLeft === 0` and moves left as the viewport scrolls toward `−max`.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:234-247`. Test: [L3] `ScrollAreaViewport.test.tsx` "shrinks and pins the horizontal thumb to the inline start while overscrolling (RTL)" (right edge), "shrinks and pins the horizontal thumb to the inline end while overscrolling (RTL)" (left edge).
- **SCR-I18N-03** — MUST. In RTL the horizontal scroll distance from the inline start is `−scrollLeft` (the browser's RTL range is `0 … −max`); `--scroll-area-overflow-x-start`/`-end`, `data-overflow-x-start`/`-end` and the thumb offset are all computed from that value, so `scrollLeft === 0` yields `x-end` only and `scrollLeft === −max` yields `x-start` only, exactly as in LTR for `0` and `max`.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:162-169,236`. Test: [L3] `ScrollAreaRoot.test.tsx` "correctly handles RTL", "recomputes horizontal overflow edges when direction changes".
- **SCR-I18N-04** — MUST. The Scrollbar and Corner are positioned with logical inset properties (`inset-inline-start`, `inset-inline-end`, SCR-DOM-18, SCR-DOM-27), so under CSS `direction: rtl` the vertical scrollbar and the corner sit at the left edge and the horizontal scrollbar's corner gap is on the left.
  Source: `scroll-area/scrollbar/ScrollAreaScrollbar.tsx:209,213-214`; `scroll-area/corner/ScrollAreaCorner.tsx:29`. Test: none. Docs: `CHANGELOG.md v1.0.0-alpha.5 "Read DirectionProvider and use logical positioning CSS props"`, `CHANGELOG.md v1.0.0-beta.4 "Fix RTL horizontal scrollbar on Safari"`.
- **SCR-I18N-05** — MUST. The horizontal *margin* offset used for thumb sizing, thumb positioning, drag mapping and track presses is `2 × margin-inline-start` of the Thumb (the inline-end margin is assumed symmetrical and not read, because Safari misreports `margin-inline-end` in RTL); horizontal *padding* of the Scrollbar and both vertical offsets are `start + end` of the computed logical values.
  Source: `scroll-area/utils/getOffset.ts:10-20`. Test: [L3] `ScrollAreaRoot.test.tsx` "accounts for thumb margin" (symmetric `marginInline: 8` → offset 16), "accounts for scrollbar padding" (`paddingInline: 8` → 16).

## 13. Behaviors evidenced by tests

Coverage of every test by the requirements above (part 1), followed by the requirements that exist only because a test asserts them (part 2). Tests marked `skipIf(isJSDOM)` in the source run only in a real browser (Chromium); the others run under jsdom with mocked element metrics (`Object.defineProperties` on `clientHeight`, `scrollHeight`, `offsetHeight`, `getBoundingClientRect`, `setPointerCapture`, …) and, where noted, fake timers.

### `root/ScrollAreaRoot.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<ScrollArea.Root />`, `refInstanceof: HTMLDivElement`, rendered standalone) | SCR-STR-01; RND-DOM-36…48 |
| "adds [data-scrolling] attribute when viewport is scrolled" (fake timers) | SCR-DOM-03, SCR-STA-11, SCR-STA-12, SCR-PTR-32, SCR-PTR-33 |
| "recomputes thumb size when becoming visible without requiring scroll" | SCR-EDGE-12, SCR-ANI-04, SCR-ANI-08, SCR-DOM-20 |
| "shows scrollbars after mount compute before the first ResizeObserver measurement" | SCR-ANI-01, SCR-DOM-19, SCR-STA-19 |
| "shows keepMounted scrollbar track and thumb after mount compute" | SCR-ANI-01, SCR-API-07, SCR-DOM-19, SCR-DOM-23, SCR-STA-19 |
| "recomputes corner size when content starts overflowing" | SCR-STA-07, SCR-STA-18, SCR-DOM-15, SCR-DOM-26, SCR-DOM-27, SCR-ANI-03, SCR-ANI-04, SCR-ANI-09 |
| "clears corner, overflow attributes, and metrics when content stops overflowing" | SCR-STA-06, SCR-STA-18, SCR-DOM-11, SCR-DOM-26, SCR-ANI-04, SCR-ANI-10 |
| "should correctly set thumb height and width based on scrollable content" | SCR-STA-17, SCR-DOM-20, SCR-ANI-03 |
| "should not add padding for overlay scrollbars" | SCR-DOM-29 |
| "accounts for scrollbar padding" / "accounts for scrollbar margin" / "accounts for thumb margin" | SCR-STA-17, SCR-DOM-20 (`getOffset` of §4.6) |
| "recomputes horizontal overflow edges when direction changes" | SCR-ANI-02, SCR-I18N-01, SCR-I18N-03, SCR-STA-09 |
| "measures content mounted after the viewport initial measurement" | SCR-STA-08, SCR-DOM-08, SCR-ANI-05 |
| "applies data attributes on root, viewport and scrollbars based on overflow and edges" | SCR-API-01, SCR-STA-02, SCR-STA-09, SCR-DOM-03, SCR-DOM-10, SCR-DOM-13, SCR-DOM-14, SCR-DOM-16, SCR-PTR-33 |
| "treats near-edge scroll offsets as fully scrolled" | SCR-STA-10 |
| "respects overflowEdgeThreshold and exposes scroll metrics" | SCR-API-02, SCR-STA-09, SCR-DOM-11 |
| "applies numeric overflowEdgeThreshold to every edge" | SCR-API-02, SCR-STA-09 |
| "recomputes overflow edges when overflowEdgeThreshold changes" | SCR-API-04, SCR-ANI-02 |
| "does not add state attributes when content does not overflow" | SCR-STA-06, SCR-API-07, SCR-DOM-03 |
| "correctly handles RTL" | SCR-STA-10, SCR-I18N-03, SCR-STA-09 |
| "does not re-render parts on scroll when the corner size is unchanged" | SCR-STA-20 |

### `viewport/ScrollAreaViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<ScrollArea.Viewport />` inside Root, `refInstanceof: HTMLDivElement`) | SCR-STR-02; RND-DOM-36…48 |
| "does not use a presentational role on the focusable viewport" | SCR-ARIA-03, SCR-API-05 |
| "handles a user scroll callback unmounting the viewport" | SCR-EDGE-04, SCR-PTR-33 |
| "releases a completed animation target while another animation is paused" (Blink only) | SCR-EDGE-07, SCR-ANI-06 |
| "recomputes overflow after a subtree animation finishes with %s" (it.each, 3 variants: `no infinite animation`, `infinite iterations`, `infinite duration`) | SCR-ANI-06, TRN-ANI-05 |
| "ignores an animation finishing after its viewport unmounts" | SCR-EDGE-06, SCR-ANI-07 |
| "adds [data-scrolling] attribute when viewport is scrolled" (fake timers) | SCR-DOM-10, SCR-STA-11, SCR-STA-12, SCR-PTR-32 |
| "ignores data-scrolling during programmatic scroll" | SCR-STA-13, SCR-STA-14 |
| "adds [data-scrolling] in touch modality even when the gesture delivers no events" | SCR-STA-13, SCR-STA-16, SCR-PTR-31 |
| "keeps ignoring programmatic scrolls in mouse modality" | SCR-STA-13, SCR-STA-14, SCR-PTR-32 |
| "restores programmatic scroll suppression after modality flips back to mouse" | SCR-STA-13, SCR-STA-16, SCR-PTR-31 |
| "removes [data-scrolling] after timeout" | SCR-STA-11 (499 ms present, 500 ms absent) |
| "applies data attributes on viewport" | SCR-DOM-10, SCR-STA-09 |
| "shrinks and pins the thumb to the start edge while overscrolling past the top" / "shrinks and pins the thumb to the end edge while overscrolling past the bottom" / "restores the resting thumb size once the viewport settles back into range" | SCR-DOM-25, SCR-DOM-24 |
| "shrinks and pins the horizontal thumb to the inline start while overscrolling (LTR)" / "shrinks and pins the horizontal thumb to the inline end while overscrolling (LTR)" / "shrinks and pins the horizontal thumb to the inline start while overscrolling (RTL)" / "shrinks and pins the horizontal thumb to the inline end while overscrolling (RTL)" | SCR-DOM-25, SCR-DOM-24, SCR-I18N-02, SCR-I18N-03 |
| "throws a descriptive error when rendered outside <ScrollArea.Root>" | SCR-EDGE-01, SCR-STR-02 |

### `scrollbar/ScrollAreaScrollbar.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<ScrollArea.Scrollbar keepMounted />` inside Root, `refInstanceof: HTMLDivElement`) | SCR-STR-04, SCR-API-07; RND-DOM-36…48 |
| "is hidden from the accessibility tree by default" / "allows overriding aria-hidden" | SCR-ARIA-04 |
| "sets the orientation data attribute" | SCR-API-06, SCR-DOM-16 |
| "supports a custom scrollbar renderer that does not forward its ref" | SCR-EDGE-10 |
| "adds [data-scrolling] attribute when viewport is scrolled in the correct direction" (fake timers) | SCR-STA-03, SCR-STA-11, SCR-DOM-16 |
| "detects a viewport that is already hovered on mount" | SCR-PTR-30, SCR-STA-15, SCR-DOM-16 |
| "does not enter hover state for touch pointers" | SCR-PTR-29 |
| "adds [data-hovering] when the synthetic pointer target differs from the native path" | SCR-PTR-27, SCR-PTR-28, SCR-PTR-34 |
| "ignores non-primary pointer presses" (track) | SCR-PTR-02, SCR-PTR-15 |
| "handles a track press when no viewport is mounted" | SCR-PTR-15, SCR-STR-07 |
| "does not start a track gesture without a thumb" | SCR-PTR-15, SCR-STR-07 |
| "ignores thumb clicks when the native path differs from the synthetic target" | SCR-PTR-16 |
| "marks the scroll area as scrolling when pressing the track" | SCR-PTR-14, SCR-PTR-19 |
| "clears track drag state on pointer cancel" | SCR-PTR-09, SCR-PTR-19, SCR-PTR-04 |
| "cancels a $name press on the track so focus stays on the active element" (it.each, 3 variants: `primary`, `middle`, `secondary`) / "cancels the press on the thumb so focus stays on the active element" | SCR-PTR-20, SCR-FOC-01 |
| "scrolls down when clicking below the thumb on a vertical track" / "scrolls right when clicking the end of a horizontal LTR track" | SCR-PTR-14 |
| "scrolls into the negative RTL range when clicking a horizontal RTL track" | SCR-PTR-18 |
| "does not snap the initial jump-to-click position" | SCR-PTR-17, SCR-PTR-14, SCR-PTR-09, SCR-PTR-03 |
| "does not jump the scroll when dragging a thumb that fills the track" / "does not jump the scroll when dragging a thumb taller than the track" | SCR-PTR-06 |
| "does not jump the scroll when clicking a track whose thumb fills the track" / "does not jump the scroll when clicking a track whose thumb is taller than the track" | SCR-PTR-15 |
| "allows horizontal scrolling away from the RTL start edge" / "clamps horizontal RTL wheel scrolling at both edges" | SCR-PTR-24, SCR-PTR-21 |
| "clamps horizontal LTR wheel scrolling at both edges" / "clamps vertical wheel scrolling at both edges" | SCR-PTR-21 |
| "preventDefaults only when it consumes the scroll, allowing chaining at edges" | SCR-PTR-22 |
| "ignores zero-delta wheel events" / "does not intercept browser zoom gestures" | SCR-PTR-23 |
| "marks the scroll area as scrolling when wheeling over the scrollbar" / "marks the scroll area as scrolling when wheeling over the horizontal scrollbar" | SCR-PTR-25 |
| "does not mark the scroll area as scrolling when chaining at an edge" | SCR-PTR-22 |
| "registers after the horizontal scrollbar becomes visible" | SCR-PTR-26, SCR-DOM-15 |
| "applies data attributes on vertical and horizontal scrollbars" | SCR-DOM-16, SCR-STA-09 |

### `thumb/ScrollAreaThumb.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<ScrollArea.Thumb />` inside Root › Scrollbar `keepMounted`, `refInstanceof: HTMLDivElement`) | SCR-STR-05; RND-DOM-36…48 |
| "throws a descriptive error when rendered outside <ScrollArea.Scrollbar>" | SCR-EDGE-03, SCR-STR-05 |
| "handles a thumb gesture when no viewport is mounted" | SCR-PTR-12, SCR-DOM-24, SCR-STR-07 |
| "handles the scrollbar unmounting from a user pointer-move callback" / "handles the viewport unmounting from a user pointer-up callback" | SCR-EDGE-05 |
| "updates LTR scroll position and pointer capture state" | SCR-PTR-01, SCR-PTR-05, SCR-PTR-09 |
| "uses the negative RTL range and clears scrolling on pointer cancel" | SCR-PTR-13, SCR-PTR-09 |
| "clears scrolling state on pointer cancel without releasing stale capture" / "clears horizontal scrolling state on pointer cancel" | SCR-PTR-09, SCR-PTR-05, SCR-PTR-04 |
| "ends a drag whose release was missed instead of scrolling on hover" | SCR-PTR-08, SCR-PTR-07, SCR-PTR-05, SCR-PTR-03, SCR-PTR-09 |
| "disables viewport scroll snap while dragging and restores it on release" / "restores viewport scroll snap on pointer cancel" | SCR-PTR-03, SCR-PTR-09 |
| "ignores a second pointer while a drag is active" | SCR-PTR-11, SCR-PTR-10, SCR-STA-21 |
| "lets a new pointer take over when capture was silently dropped" | SCR-PTR-11 |
| "ignores non-primary pointer presses" (thumb) | SCR-PTR-02 |
| "adds [data-scrolling] attribute when viewport is scrolled in the correct direction" (fake timers) | SCR-STA-04, SCR-STA-11, SCR-DOM-22 |

### `corner/ScrollAreaCorner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<ScrollArea.Corner />` inside Root with mocked overflowing Viewport and both Scrollbars `keepMounted`, so the Corner is rendered) | SCR-STR-06, SCR-DOM-26; RND-DOM-36…48 |
| "is hidden from the accessibility tree by default" / "allows overriding aria-hidden" | SCR-ARIA-05 |
| "should apply correct corner size when both scrollbars are present" | SCR-STA-18, SCR-DOM-02, SCR-ANI-09 |

### `content/ScrollAreaContent.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<ScrollArea.Content />` inside Root › Viewport, `refInstanceof: HTMLDivElement`) | SCR-STR-03; RND-DOM-36…48 |
| "throws a descriptive error when rendered outside <ScrollArea.Viewport>" | SCR-EDGE-02, SCR-STR-03 |
| "recomputes overflow when observed content resizes" | SCR-ANI-05, SCR-STA-06 |
| "supports a custom content renderer that does not forward its ref" | SCR-EDGE-10 |

### Requirements that exist only because of a test

- **SCR-PTR-34** — MUST. The hover containment check of SCR-PTR-27 uses the event's `target` (the element the event was dispatched on, as seen by the Root's handler), not the first entry of the event's composed path; a `pointerover`/`pointerenter` whose composed path starts outside the Root but whose target is inside it sets `hovering`.
  Source: `scroll-area/root/ScrollAreaRoot.tsx:246`. Test: [L4] `ScrollAreaScrollbar.test.tsx` "adds [data-hovering] when the synthetic pointer target differs from the native path".
- **SCR-EDGE-07** — MUST. The subtree-animation wait of SCR-ANI-06 retains no reference to a finished animation or its target once that animation has settled, even while other (paused or running) animations are still being awaited; a removed element whose animation finished becomes collectable.
  Source: `scroll-area/viewport/ScrollAreaViewport.tsx:349-350` (`animation.finished.then(NOOP)` discards the resolved `Animation`). Test: [L4] `ScrollAreaViewport.test.tsx` "releases a completed animation target while another animation is paused".

## 14. React-specific notes and porting considerations

Non-normative.

- **Timing phases.** The initial measurement is `queueMicrotask(computeThumbPosition)` from a layout effect (`ScrollAreaViewport.tsx:287-299`), i.e. post-commit, pre-paint plus one microtask; the hover sync (`:301-307`), the custom-property registration (`:283-285`) and the `ResizeObserver` setup (`:309-359`) are layout effects; the Scrollbar's wheel listener is a passive effect (`ScrollAreaScrollbar.tsx:69-118`, after paint). State setters called from the measurement (`setThumbSize`, `setCornerSize`, `setHiddenState`, `setOverflowEdges`, `setHasMeasuredScrollbar`) are batched into one re-render; the Thumb transform and the Viewport variables are written imperatively inside the measurement and are therefore visible before that re-render. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (timing-term mapping) and §Relaxation table (SCR-PTR-05 is met on both modes because constraint C1 puts the whole measurement/drag loop, SCR-STA-06..18 and SCR-PTR-05, in JavaScript); see decisions/04-js-interop-boundary.md §1 (`scroll-area` component module) and §2 (the thumb `transform`, `--scroll-area-*` variables, `data-scrolling`, `data-hovering` and corner size variables are JS-owned and mirrored into C# state, so consumer `ClassValue`/`StyleValue` functions see `scrolling`/`hovering` one round trip late on Server, the RND-STA-01 mirrored-field relaxation).
- **Bail-out semantics.** `pickState` returns the previous object when nothing changed so React's `setState` bails out (`ScrollAreaViewport.tsx:457-465`); the Root context value is memoized on those objects (`ScrollAreaRoot.tsx:287-341`). SCR-STA-20 is the framework-neutral statement; a port should compare by value before notifying subscribers. Port: see decisions/04-js-interop-boundary.md §2 (the twin callback fires only when a mirrored value changes) and decisions/05-state-store-and-event-details.md §1 (`Is<T>` equality before notifying).
- **Stable callbacks.** `handleScroll`, `handlePointerDown/Move/Up`, `disableViewportSnap` and `computeThumbPosition` are `useStableCallback` trampolines so the latest closure runs without re-subscribing listeners; calling them during render would throw in development (UTL-EDGE-27). Mutable drag state lives in refs (`ScrollAreaRoot.tsx:73-80`) read and written only inside handlers — a port models them as ordinary instance fields. Port: see decisions/04-js-interop-boundary.md §1 (the drag record and handlers live in the `scroll-area` component module, not in C#).
- **Synthetic event details.** `handlePointerDown` reads `event.currentTarget.getAttribute('data-orientation')` (`ScrollAreaRoot.tsx:141-143`): in React the synthetic `currentTarget` is the element the handler was attached to (Thumb or Scrollbar). The track press reads the real target through `event.nativeEvent` and `composedPath()` (`ScrollAreaScrollbar.tsx:128`) because React retargets synthetic events across shadow boundaries (SCR-PTR-16), whereas the hover check uses the synthetic `event.target` (SCR-PTR-34). `onPointerEnter`/`onPointerLeave` are synthesized by React from `pointerover`/`pointerout` and do not bubble natively; a port using native listeners must register `pointerenter`/`pointerleave` on the Root (they fire for the Root's subtree) and `pointermove` with bubbling. Port: see decisions/04-js-interop-boundary.md §1 (pointer, wheel and hover listeners are native listeners in the `scroll-area` module) and §3 (C# receives an event snapshot record, not the native event).
- **Consumer-handler ordering and `flushSync`.** Three tests remove parts from inside consumer handlers using `ReactDOM.flushSync`; the part's handlers run afterwards (RND-EVT-02) and must null-check every DOM handle (SCR-EDGE-04, SCR-EDGE-05). A port whose handler chain runs the component's handler first does not need the checks for that reason but still needs them for `keepMounted = false` Scrollbars (handles are `null` while not rendered). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 3 (substitute → consumer → part chain) and decisions/01-hosting-model-and-timing.md §Decision item 4 ("committed synchronously" is not met on Server).
- **Style element hoisting.** `styleDisableScrollbar.getElement(nonce)` is a React 19 hoistable `<style href precedence>` element, de-duplicated by `href` (`utils/styles.tsx:7`); it is rendered as a sibling before the Root element inside the provider. A port needs one shared stylesheet per document with the exact rule text of SCR-DOM-05 and the CSP nonce.
- **Generated ids.** `useBaseUiId()` is `base-ui-` + `React.useId()` on React ≥ 18 (stable across SSR); on React 17 it is assigned after the first commit, so `data-id` is absent on the first render (RND-EDGE-09). Only the `data-id` suffixes depend on it. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (scoped `base-ui-<n>` generator; no after-commit fallback).
- **Element removal of conditional parts.** `ScrollAreaScrollbar` and `ScrollAreaCorner` call `useRenderElement` unconditionally and then return `null` (`ScrollAreaScrollbar.tsx:228-230`, `ScrollAreaCorner.tsx:38-40`); when they return `null` React detaches their DOM handles (set to `null`), which is what SCR-DOM-15/26 and the null checks in the measurement rely on. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn (RND-DOM-20..24: the part exposes `Element`, `null` while not rendered).
- **`ResizeObserver` first-delivery deduplication.** The browser fires one notification immediately after `observe()`; the component compares the viewport metrics against `lastMeasuredViewportMetricsRef` (`ScrollAreaViewport.tsx:320-331`) to skip the duplicate of the mount measurement, and the Content skips its first delivery unless it mounted late (`ScrollAreaContent.tsx:27,43-45`). A port must reproduce both skips or it will measure twice at mount (harmless for correctness, relevant for SCR-STA-20). Port: see decisions/04-js-interop-boundary.md §1 (`ResizeObserver` subscriptions belong to the `dom` module, composed by the `scroll-area` module).
- **Module-level registration flag.** `scrollAreaOverflowVarsRegistered` (`ScrollAreaViewport.tsx:33`) is per JavaScript module instance, i.e. per page; `CSS.registerProperty` throwing for an already-registered name is swallowed. Port: see decisions/04-js-interop-boundary.md §1 and §4 (one lazily loaded ESM module per page holds the flag).
- **React version.** Nothing depends on React 17 vs 18/19 except the id fallback above; `useIsoLayoutEffect` is `useLayoutEffect` on the client and `useEffect` on the server (no measurement on the server, SCR-EDGE-13). Port: see decisions/04-js-interop-boundary.md §2 (prerender emits no JS-owned attribute, matching SCR-EDGE-13).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| SCR-STR-01 | MUST | Root renders div, owns all state, usable alone | 2 |
| SCR-STR-02 | MUST | Viewport renders div, inside Root, one per Root | 2 |
| SCR-STR-03 | MUST | Content renders div, inside Viewport | 2 |
| SCR-STR-04 | MUST | Scrollbar renders div, inside Root, one per orientation | 2 |
| SCR-STR-05 | MUST | Thumb renders div, inside Scrollbar, one per Scrollbar | 2 |
| SCR-STR-06 | MUST | Corner renders div, inside Root, one per Root | 2 |
| SCR-STR-07 | MUST | All non-Root parts optional; absent parts degrade to no-ops | 2 |
| SCR-API-01 | MUST | `overflowEdgeThreshold` default 0 | 3.1 |
| SCR-API-02 | MUST | Number → all edges; object → per edge, missing → 0 | 3.1 |
| SCR-API-03 | MUST | Threshold normalized `max(0, v || 0)` | 3.1 |
| SCR-API-04 | MUST | Threshold change re-evaluates edges without a scroll | 3.1 |
| SCR-API-05 | MUST | Consumer `tabIndex` replaces computed Viewport tabindex | 3.2 |
| SCR-API-06 | MUST | Scrollbar `orientation` default vertical; selects axis/handle/attribute | 3.4 |
| SCR-API-07 | MUST | `keepMounted` default false; element kept, no transition semantics | 3.4 |
| SCR-STA-01 | MUST | Root state fields exactly | 4.1 |
| SCR-STA-02 | MUST | Viewport/Content state = Root state | 4.1 |
| SCR-STA-03 | MUST | Scrollbar state = Root state + hovering, axis scrolling, orientation | 4.1 |
| SCR-STA-04 | MUST | Thumb state = { scrolling (axis), orientation } | 4.1 |
| SCR-STA-05 | MUST | Corner state empty | 4.1 |
| SCR-STA-06 | MUST | hasOverflow = scrollSize > clientSize per axis | 4.2 |
| SCR-STA-07 | MUST | cornerHidden = not both axes overflow | 4.2 |
| SCR-STA-08 | MUST | Pre-measurement/initial state values | 4.2 |
| SCR-STA-09 | MUST | Edge flags = hasOverflow ∧ normalized distance > threshold (strict) | 4.2 |
| SCR-STA-10 | MUST | Normalized from-start/from-end distances (1 px tolerance; RTL negation) | 4.2 |
| SCR-STA-11 | MUST | Per-axis scrolling flag, 500 ms timer restarted per movement | 4.3 |
| SCR-STA-12 | MUST | Root `scrolling` = either axis | 4.3 |
| SCR-STA-13 | MUST | User-driven scroll = touch modality or prior viewport interaction | 4.3 |
| SCR-STA-14 | MUST | Programmatic marker restored 100 ms after last scroll event | 4.3 |
| SCR-STA-15 | MUST | `hovering` semantics | 4.4 |
| SCR-STA-16 | MUST | Touch modality = last pointer event was touch | 4.4 |
| SCR-STA-17 | MUST | Thumb size formula with 16 px floor; stored only when changed | 4.2 |
| SCR-STA-18 | MUST | Corner size = scrollbar thicknesses when both overflow; needs mounted Corner | 4.2 |
| SCR-STA-19 | MUST | Measured flag set at first measurement attempt, never reverts | 4.2 |
| SCR-STA-20 | MUST | Unchanged measurement does not update shared state | 4.2 |
| SCR-STA-21 | MUST | Single shared drag record | 4.5 |
| SCR-DOM-01 | MUST | Root inline `position: relative` | 5.1 |
| SCR-DOM-02 | MUST | Root `--scroll-area-corner-height/width` in px | 5.1 |
| SCR-DOM-03 | MUST | Root seven state data attributes | 5.1 |
| SCR-DOM-04 | MUST NOT | `cornerHidden` never emitted | 5.1 |
| SCR-DOM-05 | MUST | Scrollbar-hiding `<style>` before Root unless `disableStyleElements` | 5.2 |
| SCR-DOM-06 | MUST | Viewport class `base-ui-disable-scrollbar` | 5.3 |
| SCR-DOM-07 | MUST | Viewport inline `overflow: scroll` | 5.3 |
| SCR-DOM-08 | MUST | Viewport tabindex 0 iff overflow, else −1 | 5.3 |
| SCR-DOM-09 | MUST | Viewport `data-id` = `<root id>-viewport` | 5.3 |
| SCR-DOM-10 | MUST | Viewport seven state data attributes | 5.3 |
| SCR-DOM-11 | MUST | Viewport `--scroll-area-overflow-*` inline px, written per measurement | 5.3 |
| SCR-DOM-12 | MUST | Overflow vars registered non-inheriting once per page, not on WebKit | 5.3 |
| SCR-DOM-13 | MUST | Content inline `min-width: fit-content` | 5.4 |
| SCR-DOM-14 | MUST | Content seven state data attributes | 5.4 |
| SCR-DOM-15 | MUST | Non-keepMounted Scrollbar rendered iff axis overflows | 5.5 |
| SCR-DOM-16 | MUST | Scrollbar data-orientation/hovering/axis scrolling + six overflow attrs | 5.5 |
| SCR-DOM-17 | MUST | Scrollbar inline absolute, touch-action none, user-select none | 5.5 |
| SCR-DOM-18 | MUST | Scrollbar logical insets per orientation with corner vars | 5.5 |
| SCR-DOM-19 | MUST | Non-keepMounted Scrollbar `visibility: hidden` until measured | 5.5 |
| SCR-DOM-20 | MUST | Scrollbar `--scroll-area-thumb-height/width` in px | 5.5 |
| SCR-DOM-21 | MUST | Scrollbar `data-id` = `<root id>-scrollbar` | 5.5 |
| SCR-DOM-22 | MUST | Thumb data-orientation, data-scrolling | 5.6 |
| SCR-DOM-23 | MUST | Thumb visibility until measured; height/width from var | 5.6 |
| SCR-DOM-24 | MUST | Thumb inline translate3d transform per measurement | 5.6 |
| SCR-DOM-25 | MUST | Thumb overscroll size override and edge pinning | 5.6 |
| SCR-DOM-26 | MUST | Corner rendered iff both axes overflow | 5.7 |
| SCR-DOM-27 | MUST | Corner inline absolute bottom/inset-inline-end 0, size px | 5.7 |
| SCR-DOM-28 | MUST NOT | Corner has no data attributes | 5.7 |
| SCR-DOM-29 | MUST NOT | No padding reserved for overlay scrollbars | 5.4 |
| SCR-ARIA-01 | MUST | Root `role="presentation"` (overridable) | 5.8 |
| SCR-ARIA-02 | MUST | Content `role="presentation"` (overridable) | 5.8 |
| SCR-ARIA-03 | MUST NOT | Viewport has no role | 5.8 |
| SCR-ARIA-04 | MUST | Scrollbar `aria-hidden="true"` (overridable incl. undefined) | 5.8 |
| SCR-ARIA-05 | MUST | Corner `aria-hidden="true"` (overridable) | 5.8 |
| SCR-ARIA-06 | MUST NOT | No other aria-*/roles anywhere | 5.8 |
| SCR-KEY-01 | MUST NOT | No key intercepted; native keyboard scrolling | 6.1 |
| SCR-KEY-02 | MUST | Viewport keydown clears programmatic marker | 6.1 |
| SCR-PTR-01 | MUST | Thumb pointerdown (button 0) starts drag, captures pointer | 6.2.1 |
| SCR-PTR-02 | MUST | Non-primary button ignored on Thumb and track | 6.2.1 |
| SCR-PTR-03 | MUST | Drag start saves and disables `scroll-snap-type` (guarded) | 6.2.1 |
| SCR-PTR-04 | MUST | Drag orientation from pressed element's `data-orientation` | 6.2.1 |
| SCR-PTR-05 | MUST | Drag move mapping, preventDefault, axis scrolling | 6.2.1 |
| SCR-PTR-06 | MUST | `maxThumbOffset ≤ 0` → ratio 0 | 6.2.1 |
| SCR-PTR-07 | MUST | Foreign pointer move ignored | 6.2.1 |
| SCR-PTR-08 | MUST | Buttonless move = missed release | 6.2.1 |
| SCR-PTR-09 | MUST | Release: clear id, clear axis scrolling now, restore snap, release capture if held | 6.2.1 |
| SCR-PTR-10 | MUST | Foreign pointer up/cancel ignored | 6.2.1 |
| SCR-PTR-11 | MUST | Second pointerdown ignored while captured; takeover when capture dropped | 6.2.1 |
| SCR-PTR-12 | MUST | Drag without Viewport scrolls nothing | 6.2.1 |
| SCR-PTR-13 | MUST | RTL drag adds delta directly (negative range) | 6.2.1 |
| SCR-PTR-14 | MUST | Track press jumps thumb center under pointer | 6.2.2 |
| SCR-PTR-15 | MUST | Track press no-op conditions | 6.2.2 |
| SCR-PTR-16 | MUST | Thumb detection via composed-path target + shadow-aware contains | 6.2.2 |
| SCR-PTR-17 | MUST | Snap disabled before the jump | 6.2.2 |
| SCR-PTR-18 | MUST | RTL track press mapping `−(1 − ratio) × max` | 6.2.2 |
| SCR-PTR-19 | MUST | Track press marks scrolling and starts a drag | 6.2.2 |
| SCR-PTR-20 | MUST | mousedown (any button) on Scrollbar/Thumb prevented | 6.2.2 |
| SCR-PTR-21 | MUST | Wheel over Scrollbar scrolls its axis, clamped | 6.2.3 |
| SCR-PTR-22 | MUST | Wheel cancelled only when consumed; chains at edges | 6.2.3 |
| SCR-PTR-23 | MUST | Wheel with ctrlKey or zero delta ignored | 6.2.3 |
| SCR-PTR-24 | MUST | RTL wheel range `[−max, 0]` | 6.2.3 |
| SCR-PTR-25 | MUST | Consumed wheel marks axis scrolling | 6.2.3 |
| SCR-PTR-26 | MUST | Native non-passive wheel listener, after paint, re-registered on direction change | 6.2.3 |
| SCR-PTR-27 | MUST | Non-touch pointerenter/move sets hovering iff target inside Root | 6.2.4 |
| SCR-PTR-28 | MUST | pointerleave clears hovering | 6.2.4 |
| SCR-PTR-29 | MUST NOT | Touch never sets hovering | 6.2.4 |
| SCR-PTR-30 | MUST | `:hover` at mount sets hovering | 6.2.4 |
| SCR-PTR-31 | MUST | pointerenter/move/down update touch modality | 6.2.4 |
| SCR-PTR-32 | MUST | Viewport wheel/pointermove/pointerenter clear programmatic marker; pointerdown does not | 6.2.4 |
| SCR-PTR-33 | MUST | Scroll event: measure, attribute, restart 100 ms timer | 6.2.4 |
| SCR-PTR-34 | MUST | Hover check uses event target, not composed path | 13 |
| SCR-FOC-01 | MUST NOT | Only Viewport focusable; scrollbar presses do not move focus | 6.3 |
| SCR-ANI-01 | MUST | First measurement one microtask after post-commit pre-paint | 9 |
| SCR-ANI-02 | MUST | Re-measure on hidden-state, direction, threshold change | 9 |
| SCR-ANI-03 | MUST | Two-pass settling for conditionally rendered parts | 9 |
| SCR-ANI-04 | MUST | Viewport ResizeObserver; first delivery deduplicated | 9 |
| SCR-ANI-05 | MUST | Content ResizeObserver; first delivery skipped unless late mount | 9 |
| SCR-ANI-06 | MUST | 0 ms subtree finite-animation wait, then measure | 9 |
| SCR-ANI-07 | MUST | Unmount cleanup of observers and timers | 9 |
| SCR-ANI-08 | MUST | Zero-size measurement exits early, sets measured flag | 9 |
| SCR-ANI-09 | MUST | Corner sized by first measurement after it mounts | 9 |
| SCR-ANI-10 | MUST NOT | No transition semantics for keepMounted/Corner | 9 |
| SCR-I18N-01 | MUST | Direction from DirectionProvider only | 12.1 |
| SCR-I18N-02 | MUST | RTL horizontal thumb offset negated | 12.1 |
| SCR-I18N-03 | MUST | RTL from-start = −scrollLeft | 12.1 |
| SCR-I18N-04 | MUST | Logical inset positioning follows CSS direction | 12.1 |
| SCR-I18N-05 | MUST | Horizontal thumb margin offset = 2 × margin-inline-start | 12.1 |
| SCR-EDGE-01 | MUST | Root context error message | 12 |
| SCR-EDGE-02 | MUST | Viewport context error message | 12 |
| SCR-EDGE-03 | MUST | Scrollbar context error message | 12 |
| SCR-EDGE-04 | MUST | Scroll handler tolerates Viewport removal | 12 |
| SCR-EDGE-05 | MUST | Drag handlers tolerate part removal | 12 |
| SCR-EDGE-06 | MUST | Animation settling after unmount ignored | 12 |
| SCR-EDGE-07 | MUST | No retention of settled animations | 13 |
| SCR-EDGE-08 | MUST | No ResizeObserver → no resize observation | 12 |
| SCR-EDGE-09 | MUST NOT | No dev warnings | 12 |
| SCR-EDGE-10 | MUST | Substitute without handle still renders | 12 |
| SCR-EDGE-11 | MUST | Nested scroll areas independent | 12 |
| SCR-EDGE-12 | MUST | Hidden-subtree mount measured when shown | 12 |
| SCR-EDGE-13 | MUST | Server-rendered markup = pre-measurement state | 12 |

## 16. Open questions

1. **`data-id` is undocumented and duplicated.** `ScrollAreaViewport.tsx:366` and `ScrollAreaScrollbar.tsx:121` render `data-id="<root id>-viewport"` / `"<root id>-scrollbar"`; neither the outline nor the docs page mention them, nothing in the component reads them, and a vertical and a horizontal Scrollbar of one Root get the same value. SCR-DOM-09/21 specify them as rendered; a port could treat them as optional if the reviewers agree they are vestigial.
2. **Discrepancy with the transitions doc.** `transitions-and-mounting.md` TRN-ANI-04 and §10 Q3 state that no caller passes `{ subtree: true }` to `getFiniteAnimations` in 1.9.0; `ScrollAreaViewport.tsx:341` does (`getFiniteAnimations(viewport, { subtree: true })`). The TRN statement should be corrected to exclude Scroll Area; SCR-ANI-06 documents the subtree behavior. Resolved: TRN-ANI-04 now names `ScrollArea.Viewport` as the one `{ subtree: true }` caller (geometry re-measure only, not open/close completion), and transitions-and-mounting.md §10 Q3 carries the correction.
3. **Unreachable `visibility: hidden` branch on the track.** `ScrollAreaScrollbar.tsx:65,204` hides a non-`keepMounted` track until measured, but such a Scrollbar is only rendered after a measurement (SCR-DOM-15), and the measured flag is set in the same commit as the hidden state (`ScrollAreaViewport.tsx:144-146,271`), so the style is never observable in 1.9.0. SCR-DOM-19 states it as written; a port may omit it without observable difference.
4. **Stale scroll position after programmatic scrolls.** `handleScroll` (`ScrollAreaRoot.tsx:92-105`) is only called for user-driven scrolls, so the recorded last position is not updated by programmatic scrolls (`ScrollAreaViewport.tsx:386-391`). A programmatic scroll on Y followed by a user scroll on X therefore sets `data-scrolling` for both axes. Documented behavior (SCR-STA-11 "previously recorded position") but possibly unintended.
5. **Asymmetric horizontal margins.** `getOffset.ts:14-18` doubles `margin-inline-start` for the horizontal thumb margin as a Safari workaround; asymmetric inline margins on the Thumb are mis-measured on every engine. SCR-I18N-05 preserves this.
6. **Drag orientation depends on the rendered attribute.** `ScrollAreaRoot.tsx:141-143` reads `data-orientation` from the pressed element; a consumer override of that attribute (permitted by RND-DOM-10/19) would redirect the drag to the other axis. SCR-PTR-04 states the mechanism; whether a port should instead carry the orientation internally is a design choice to confirm.
7. **Measured flag never resets.** `hasMeasuredScrollbar` lives on the Root (`ScrollAreaRoot.tsx:59`); after a Viewport unmounts and remounts, the Thumb is never hidden pre-measurement and a remounted Content treats its first resize notification as a late mount (SCR-ANI-05). Behavior is specified as-is.
8. **Track-press ratio is not clamped.** `ScrollAreaScrollbar.tsx:169-184` can produce a ratio outside `[0, 1]` (press within half a thumb of an edge) and, in RTL, a positive `scrollLeft`; correctness relies on the browser clamping the assignment. The transcription in §6.2.2 preserves this.
9. **Drag vs. track asymmetry at `maxThumbOffset ≤ 0`.** A drag move still cancels the event and sets `data-scrolling` with ratio `0` (`ScrollAreaRoot.tsx:221-235`), whereas a track press returns before doing anything (`ScrollAreaScrollbar.tsx:165-167`). Both are specified (SCR-PTR-06, SCR-PTR-15).
10. **Hinted items that do not exist in 1.9.0.** There is no `role="scrollbar"`, `aria-orientation`, `aria-valuenow`/`aria-valuemin`/`aria-valuemax` or `aria-controls` on any part (SCR-ARIA-06); the track press is a jump-to-position followed by a drag, not a page jump (SCR-PTR-14); no dev warnings exist (SCR-EDGE-09).
11. **Outline omissions.** The outline (`components/page.mdx` "## Scroll Area") does not list: `data-id` on Viewport/Scrollbar; the computed `tabIndex` of the Viewport; `role="presentation"` on Root and Content; `aria-hidden` on Scrollbar and Corner; the `cornerHidden` state field (present in `ScrollArea.Root.State` but never an attribute). Conversely every prop, attribute, variable and type the outline lists is covered here.
12. **Template fit.** The pseudo-code requested by the assignment lives in §4.6 and §6.2 under STA/PTR IDs because the component template has no ALG area; see the completion report for a suggested template change. Resolved: conventions §3.3 now permits ALG in component docs for component-local measurement (thumb geometry); the existing STA/PTR IDs are kept because IDs are never renumbered, and the transcriptions in §4.6 and §6.2 are the content of the `scroll-area` component module of decisions/04-js-interop-boundary.md §1.
13. **ADR 04 §2 ownership list does not match the parts that carry the values.** `decisions/04-js-interop-boundary.md` §2 lists `ScrollArea.Thumb`, `ScrollArea.Viewport` and `ScrollArea.Root` as owning "thumb `transform`, `--scroll-area-*` variables, `data-scrolling`, `data-hovering`, corner size variables". In 1.9.0 `data-hovering` is emitted only on the Scrollbar (`ScrollAreaScrollbar.tsx:57-62`, SCR-DOM-16; the Root state has no `hovering`, `ScrollAreaRoot.tsx:251-263`); `--scroll-area-thumb-height/width` sit on the Scrollbar (`ScrollAreaScrollbar.tsx:210,216`, SCR-DOM-20) as well as on the Thumb during overscroll (SCR-DOM-25); `data-scrolling` is also on the Scrollbar, Thumb and Content (SCR-DOM-14/16/22); and the Corner's inline `width`/`height` carry the corner size (`ScrollAreaCorner.tsx:30-31`, SCR-DOM-27). Whether `ScrollArea.Scrollbar`, `ScrollArea.Content` and `ScrollArea.Corner` join the list (and whether the overflow `data-*` attributes, which the same JS loop produces, stay C#-projected) needs a decision. Decided (2026-10-18): Scrollbar, Content and Corner rows are added to the ownership list; the overflow data-* attributes stay C#-projected from mirrored state (ADR 04 §2 as amended).
