# Floating and positioning

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/floating-ui-react/` (hooks: `useFloating.ts`, `useFloatingRootContext.ts`, `useSyncedFloatingRootContext.ts`, `useClick.ts`, `useDismiss.ts`, `useFocus.ts`, `useClientPoint.ts`, `useHoverReferenceInteraction.ts`, `useHoverFloatingInteraction.ts`, `useHoverShared.ts`, `useHoverInteractionSharedState.ts`; components: `FloatingRootStore.ts`, `FloatingTree.tsx`, `FloatingTreeStore.ts`, `FloatingDelayGroup.tsx`, `FloatingFocusManager.tsx`, `FloatingPortal.tsx`; `safePolygon.ts`, `middleware/arrow.ts`, `types.ts`, `utils.ts`, `utils/element.ts`, `utils/event.ts`, `utils/tabbable.ts`, `utils/markOthers.ts`, `utils/nodes.ts`, `utils/enqueueFocus.ts`, `utils/createEventEmitter.ts`, `utils/createAttribute.ts`, `utils/constants.ts`, `utils/getEmptyRootContext.ts`), `packages/react/src/internals/useAnchorPositioning.ts`, `packages/react/src/utils/usePositioner.tsx`, `packages/react/src/utils/adaptiveOriginMiddleware.ts`, `packages/react/src/utils/adaptiveOriginConstants.ts`, `packages/react/src/utils/hideMiddleware.ts`, `packages/react/src/utils/CommonPositionerCssVars.ts`, `packages/react/src/utils/FocusGuard.tsx` |
| Tests | `floating-ui-react/hooks/useFloating.test.tsx`, `useClick.test.tsx`, `useDismiss.test.tsx`, `useFocus.test.tsx`, `useClientPoint.test.tsx`, `useHoverReferenceInteraction.test.tsx`, `useHoverFloatingInteraction.test.tsx`, `floating-ui-react/safePolygon.test.ts`, `floating-ui-react/components/FloatingDelayGroup.test.tsx`, `FloatingFocusManager.test.tsx`, `FloatingPortal.test.tsx`, `floating-ui-react/utils/tabbable.test.ts`, `markOthers.test.ts`, `nodes.test.ts`, `event.test.ts`, `internals/useAnchorPositioning.test.tsx`, `utils/hideMiddleware.test.ts`, `packages/react/test/positionerConformanceTests.tsx`, `packages/react/test/waitForPositioned.ts`, `packages/react/test/floating-ui-tests/*` (test shims) |
| Prefix | `FLT` |
| Status | approved |
| Revised | 2026-10-28 |
| Used by | `components/popover.md`, `components/tooltip.md`, `components/preview-card.md`, `components/menu.md`, `components/context-menu.md`, `components/menubar.md`, `components/select.md`, `components/combobox.md`, `components/autocomplete.md`, `components/navigation-menu.md`, `components/dialog.md`, `components/alert-dialog.md`, `components/drawer.md`, `components/toast.md` (positioner root context only); builds on `architecture/popup-store-and-handles.md` (PSH), `architecture/event-details-and-reasons.md` (EVD), `architecture/rendering-and-composition.md` (RND), `architecture/transitions-and-mounting.md` (TRN), `architecture/shared-utilities.md` (UTL) |

## 1. Purpose

Every Base UI popup (popover, tooltip, preview card, menu, select, combobox, navigation menu) is a *floating element*: an element rendered outside its trigger's layout flow and placed next to an *anchor* by measuring both and solving for coordinates. Dialogs and drawers are not anchored, but they reuse the same open/close plumbing, focus trapping and portal. This document specifies the two layers underneath all of them:

1. **Anchored positioning** — how a Positioner part computes where to put itself (`side`, `align`, offsets, collision avoidance, arrow placement, anchor tracking), what it writes to the DOM (`transform`/`top`/`left`, `--anchor-width`, `--available-height`, `--transform-origin`, `data-side`, `data-align`, `data-anchor-hidden`) and how it keeps those values current as the page scrolls, resizes and reflows. Base UI delegates the arithmetic to the external library `@floating-ui/dom` (resolved to `@floating-ui/core@1.8.0`, `@floating-ui/dom@1.8.0`, `@floating-ui/react-dom@2.1.9`, `@floating-ui/utils@0.2.12` by `pnpm-lock.yaml`). Because a port cannot assume that library exists in its target platform, §5 transcribes the parts Base UI configures (placement resolution, `offset`, `flip`, `shift`/`limitShift`, `size`, `arrow`, `hide`, `autoUpdate`) in enough detail to reimplement or to substitute an equivalent engine.
2. **Open/close interaction primitives** — the vendored and modified fork of `@floating-ui/react` under `floating-ui-react/`: the per-popup *root store* and the *floating tree* that lets nested popups find each other; the interaction hooks that turn clicks (`useClick`), hover (`useHoverReferenceInteraction` / `useHoverFloatingInteraction` with `safePolygon`), focus (`useFocus`), pointer coordinates (`useClientPoint`) and dismissal gestures (`useDismiss`: outside press, Escape, reference press) into `setOpen` calls carrying an EVD reason; the shared-delay group used by tooltips (`FloatingDelayGroup`); the focus manager (`FloatingFocusManager`: initial focus, focus trap, focus return, focus restoration, `aria-hidden`/inert marking of the rest of the page); and the portal (`FloatingPortal`: container resolution and the focus guards that keep Tab order sane across a portal boundary).

A port cannot skip this layer: every popup component doc states only *which options it passes* to these mechanisms (see §6) and relies on this document for what those options do, including all timing values, event-ordering subtleties and the exact arithmetic that tests assert on (for example `translate(10px, 36px)` to the pixel).

List navigation and typeahead (`useListNavigation`, `useTypeahead`, `utils/composite.ts`, `gridNavigation.ts`) live in the same directory but are specified in `architecture/composite-and-list-navigation.md` (CMP). The popup store, trigger map, `FloatingPortalLite`, `InternalBackdrop`, `usePopupAutoResize` (which writes `--positioner-width`/`--positioner-height`) and `usePopupViewport` (which supplies the adaptive-origin middleware) are specified in `architecture/popup-store-and-handles.md` (PSH).

## 2. Concepts and terminology

| Base UI / React name | Neutral term used here | Meaning |
|---|---|---|
| reference element, `domReferenceElement` | **anchor element** (DOM anchor) | The real DOM element the popup belongs to; normally the active trigger. Used for hit-testing ("was this press inside the anchor?"), focus return and hover tracking. |
| `positionReference`, virtual element | **position anchor** | What the popup is measured against. Either the anchor element or a *virtual anchor*: any object with `getBoundingClientRect()` (optionally `getClientRects()` and `contextElement`). The position anchor can differ from the anchor element (custom `anchor` prop, cursor-following tooltips, context menus at a click point). |
| floating element, `floatingElement` | **floating element** | The element that is positioned. For anchored components this is the Positioner part; for `treatPopupAsFloatingElement` roots (dialog-like) it is the Popup part. |
| `FloatingRootStore` / `FloatingRootContext` | **root store** | One per popup instance: reactive state (open, elements, id) plus a non-reactive context bag (callbacks, event emitter, last open event, trigger map). All interaction hooks read and write this store. Components usually create it from their popup store via `useSyncedFloatingRootContext` (PSH); a few (`Select`, `Combobox`, `NavigationMenu`, `Toast.Positioner`) create a standalone one with `useFloatingRootContext`. |
| `dataRef` | **context data** | Mutable, non-reactive per-root scratch data: `openEvent` (the DOM event that opened the popup), `floatingContext` (the latest positioning context), `insideReactTree` (a one-tick "the press started inside" marker), `__escapeKeyBubbles` / `__outsidePressBubbles` (dismiss bubbling flags published for parents), `hoverInteractionState` (hover timers shared between trigger and popup hooks). |
| `events` (`FloatingEvents`) | **root event bus** | A tiny synchronous emitter per root store: `emit(name, data)`, `on`, `off`. Base UI emits `openchange` on it (see FLT-EVT-02). Menus additionally emit `setOpen` (documented in `components/menu.md`). |
| `FloatingTree` / `FloatingTreeStore` | **floating tree** | A registry shared by a popup and all popups nested inside it (submenus, nested popovers): an ordered list of nodes `{ id, parentId, context? }` and a second event bus (`floating.closed`, and menu-specific events documented in `components/menu.md`). Lets a parent find its *open* descendants (for dismissal bubbling, safe-polygon abort, focus-out checks) without DOM containment. |
| `FloatingNode` | **tree node boundary** | Marks "everything rendered inside me has node `id` as its parent". |
| `nodeId` / `parentId` | **node id / parent id** | Unique id per popup instance within a tree; `parentId` is `null` for top-level popups. |
| `useFloating` (`useBaseUIFloating`) | **positioning session** | Wraps the engine: given the root store's elements, placement and middleware, produces `x`, `y`, rendered placement, middleware data, `isPositioned`, `floatingStyles`, `update()`. |
| `computePosition`, middleware | **positioning engine**, **middleware pipeline** | External solver (see §5, §8). Middleware run in order and may move the floating element, record data, or ask for a restart with a new placement/rects. |
| `placement` | **placement** | `'<side>'` or `'<side>-<alignment>'` where side ∈ top/right/bottom/left and alignment ∈ start/end. Base UI exposes logical `side` values (`inline-start`/`inline-end`) and `align: 'center'` on top of it. |
| `rects.reference` / `rects.floating` | **anchor rect / floating rect** | Measured boxes in the coordinate space of the floating element's offset parent (absolute strategy) or the viewport (fixed strategy). |
| `strategy` | **position method** | CSS `position` value used: `'absolute'` or `'fixed'` (prop `positionMethod`). |
| `autoUpdate` | **anchor tracking** | Re-running positioning on ancestor scroll/resize, element resize and layout shift while the popup is mounted. Disabled by `disableAnchorTracking`. |
| `ElementProps` (`reference`, `floating`, `item`, `trigger`) | **prop bags** | Each interaction hook returns handler sets keyed by which element they attach to: the active anchor (`reference`), the floating element (`floating`), inactive triggers (`trigger`). Components merge them onto the relevant parts (PSH). |
| `useClick` | **press-to-toggle interaction** | |
| `useDismiss` | **dismissal interaction** | Outside press, Escape, reference press. |
| `useFocus` | **focus-to-open interaction** | |
| `useClientPoint` | **cursor anchoring** | Replaces the position anchor with the pointer coordinates. |
| `useHoverReferenceInteraction` / `useHoverFloatingInteraction` | **hover interaction (trigger side / popup side)** | Split in two so a trigger and a popup rendered by different parts can cooperate through the shared hover state. |
| `safePolygon` | **safe polygon** | A `handleClose` strategy: keeps a hover popup open while the cursor travels from the trigger toward the popup through a computed quadrilateral. |
| `FloatingDelayGroup` / `useDelayGroup` | **delay group** | Shared open/close delay state across sibling tooltips, with an "instant phase" when moving between them. |
| `FloatingFocusManager` | **focus manager** | Initial focus, modal trap, focus-out closing, focus restoration, return focus, outside-content hiding. |
| `FloatingPortal` / `useFloatingPortalNode` | **portal** | Renders the popup subtree into a container element outside the trigger's DOM subtree while keeping it logically nested. |
| `FocusGuard` | **focus guard** | A visually hidden `tabindex="0"` `<span>` whose only job is to receive Tab focus so script can redirect it. |
| `markOthers` | **outside marking** | Sets `aria-hidden="true"` and/or the `data-base-ui-inert` marker on every element outside a set of "inside" elements. |
| `openEvent` click-like | **click-like open** | The popup was opened by `click`, `mousedown`, `keydown` or `keyup` (as opposed to hover or focus). Several hooks change behavior on this (FLT-STA-04). |
| `REASONS.*` | **reason** | EVD reason strings carried in change event details. This slice emits `trigger-press`, `input-press`, `trigger-hover`, `trigger-focus`, `outside-press`, `escape-key`, `focus-out`, `none` (FLT-EVT-01). |
| rAF / "next frame" | **next animation frame** | A deferral until the browser's next rendering opportunity (`requestAnimationFrame`). |
| microtask / "next tick" (`setTimeout(0)`) | **microtask** / **next macrotask** | A microtask runs before the browser returns to the event loop; a 0 ms timeout runs after all pending microtasks and after any remaining DOM events already dispatched in this task. |
| `Timeout` (`useTimeout`) | **restartable timer** | `start(ms, fn)` cancels any pending call on the same timer before scheduling; `clear()` cancels (UTL). |
| `AnimationFrame` (`useAnimationFrame`) | **restartable frame request** | Same, for animation frames (UTL). |
| `useIsoLayoutEffect` | **layout phase** | Work that runs after the DOM is written but before the browser paints; "commit" in React. Used for store synchronization and measurement. §8 explains the porting implications. |

Conventions in this document: "the document" means the owner document of the floating element (or of the anchor, when the floating element does not exist); `contains(a, b)` is composed-tree containment that also crosses shadow roots (`packages/utils/src/shadowDom.ts:13-37`, UTL); `getTarget(event)` is `event.composedPath()[0]` falling back to `event.target` (`shadowDom.ts:72-82`); `activeElement(doc)` descends into open shadow roots (`shadowDom.ts:3-11`). "Mouse-like pointer type" means `pointerType` ∈ {`'mouse'`, `'pen'`} and, when not strict, also `''`/`undefined` (`floating-ui-react/utils/event.ts:50-58`).

## 3. Public surface

The hooks and components in `floating-ui-react/` are marked `@internal` and are not part of the consumer API (`floating-ui-react/index.ts:1-51`). What *is* public is the positioner contract: every `*.Positioner` part spreads `UseAnchorPositioningSharedParameters` into its props (`internals/useAnchorPositioning.ts:665-811`), exposes the same state attributes and CSS variables, and `*.Portal` parts expose `container` from `FloatingPortal`. Those are documented here once; component docs link to this section and list only deviations (different defaults, extra props such as `Select.Positioner.alignItemWithTrigger`).

### 3.1 Shared Positioner props

| Prop | Type | Default | Description |
|---|---|---|---|
| `anchor` | `Element \| null \| VirtualElement \| RefObject<Element \| null> \| (() => Element \| VirtualElement \| null) \| undefined` | `undefined` (the active trigger) | An element to position the popup against. A DOM handle (ref) is dereferenced; a function is called; a virtual element is any object with `getBoundingClientRect(): Rect` (optionally `getClientRects()` and `contextElement`). `null`/`undefined` means "use the root store's anchor element". (`useAnchorPositioning.ts:666-680`) |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | Which CSS `position` property to use. (`:681`) |
| `side` | `'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-end' \| 'inline-start'` | `'bottom'` | Which side of the anchor to place the popup on. May change to avoid collisions. (`:66, :687`) |
| `sideOffset` | `number \| OffsetFunction` | `0` | Distance between anchor and popup in px along the side axis. A function receives `{ side, align, anchor: { width, height }, positioner: { width, height } }` where `side` is the logical side being evaluated. (`:688-712`) |
| `align` | `'start' \| 'center' \| 'end'` | `'center'` | How to align the popup relative to the specified side. (`:67, :717`) |
| `alignOffset` | `number \| OffsetFunction` | `0` | Additional offset along the alignment axis in px; same function contract. (`:718-742`) |
| `collisionBoundary` | `'clipping-ancestors' \| Element \| Element[] \| Rect` | `'clipping-ancestors'` | The area the popup is confined to. (`:68, :747`) |
| `collisionPadding` | `number \| Partial<{ top, right, bottom, left }>` | `5` | Space to keep from the collision boundary's edges. (`:752`) |
| `sticky` | `boolean` | `false` | Keep the popup in the viewport after the anchor scrolls out of view. (`:758`) |
| `arrowPadding` | `number` | `5` | Minimum distance between the Arrow part and the popup's edges. (`:765`) |
| `disableAnchorTracking` | `boolean` | `false` | Stop following layout shifts/scroll/resize of the anchor after the initial position. (`:770`) |
| `collisionAvoidance` | `CollisionAvoidance` | component-specific (see §6.1): `{ fallbackAxisSide: 'end' }` for free popups (`POPUP_COLLISION_AVOIDANCE`, `internals/constants.ts:26-28`) or `{ fallbackAxisSide: 'none' }` for dropdowns (`DROPDOWN_COLLISION_AVOIDANCE`, `:18-20`) | How to handle collisions; see type below. Missing keys default to `side: 'flip'`, `align: 'flip'`, `fallbackAxisSide: 'end'` (FLT-API-10). (`:771-810`) |

`collisionAvoidance` is a union of two shapes (`useAnchorPositioning.ts:76-125`):

* `SideFlipMode`: `side?: 'flip' | 'none'`, `align?: 'flip' | 'shift' | 'none'`, `fallbackAxisSide?: 'start' | 'end' | 'none'`.
* `SideShiftMode`: `side?: 'shift' | 'none'`, `align?: 'shift' | 'none'`, `fallbackAxisSide?: 'start' | 'end' | 'none'`.

Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27; the offset fields and the remaining names settled by the orchestrator 2026-10-27): the twelve props become nullable C# parameters on each family's Positioner, `null` meaning Base UI's default:
* `anchor` → `AnchorValue?`: an `ElementReference`, an element id, or a `VirtualAnchor(X, Y, Width, Height)`; no function case, so a changed anchor is a new value (FLT-API-11's port note).
* `positionMethod` → `PositionMethod?` (`Absolute`, `Fixed`).
* `side` → `Side?` (`Top`, `Bottom`, `Left`, `Right`, `InlineEnd`, `InlineStart`).
* `sideOffset` and `alignOffset` → `OffsetValue?`: a number, or `Offset(Constant, AnchorWidth, AnchorHeight, PositionerWidth, PositionerHeight)` with `Overrides` keyed by the evaluated logical side and alignment, evaluated on every pass (FLT-API-12's port note).
* `align` → `Align?` (`Start`, `Center`, `End`).
* `collisionBoundary` → `CollisionBoundaryValue?` (`ClippingAncestors`, an element, a list of elements, a `Rect`).
* `collisionPadding` → `CollisionPaddingValue?` (a number or `PaddingSides`).
* `sticky`, `arrowPadding`, `disableAnchorTracking` → `bool`, `double?`, `bool`.
* `collisionAvoidance` → `CollisionAvoidance?`, a record of `CollisionAvoidanceMode? Side`, `CollisionAvoidanceMode? Align` (`Flip`, `Shift`, `None`) and `FallbackAxisSide? FallbackAxisSide` (`Start`, `End`, `None`).
The types live in `src/Blazix.Primitives/Floating/`. The element cases cross beside the options as references, which the options name by index (`AnchorRef`, `BoundaryRefs`; ADR 04 Amendments 2026-10-27, element-typed option values; dated 2026-10-28, M4 track 2, once track 1's references array was on `main`).

The docs note (`:805-806`): when `side` is `'shift'`, an explicit `align` only supports `'shift'` or `'none'`; omitted `align` still defaults to `'flip'` at runtime (the shift-before-flip ordering of FLT-POS-03 then makes alignment flipping effectively inert, see FLT-POS-05 note).

### 3.2 Shared Positioner outputs

State fields every Positioner part puts into its state object (and therefore into `data-*` attributes through the RND state-attribute mapping): `open`, `side` (logical rendered side), `align` (rendered alignment), `anchorHidden`, and, for components with a popup store, `instant` (PSH/TRN). Data attributes listed by the API outline for Positioner parts: `data-open`, `data-closed`, `data-side`, `data-align`, `data-anchor-hidden` (`docs/src/app/(docs)/react/components/page.mdx:1327-1329` for Popover; identical for the other Positioners).

CSS variables (`utils/CommonPositionerCssVars.ts:5-37`):

| Variable | Set by | Meaning |
|---|---|---|
| `--available-width` | this document (size middleware, FLT-DOM-05) | Width available between the anchor and the boundary edge on the rendered side, in px. Seeded to `100vw` before the first positioning pass. |
| `--available-height` | this document (FLT-DOM-05) | Same for height; seeded to `100vh`. |
| `--anchor-width` | this document (FLT-DOM-06) | Anchor width in px, snapped to device pixels. |
| `--anchor-height` | this document (FLT-DOM-06) | Anchor height in px, snapped. |
| `--transform-origin` | this document (FLT-ALG-11) | The point the popup should grow from when animated with `transform-origin: var(--transform-origin)`. |
| `--positioner-width` | PSH (`usePopupAutoResize`) | Width of the positioner, for animating size changes between triggers. Not written by positioning. |
| `--positioner-height` | PSH (`usePopupAutoResize`) | Height counterpart. |

Arrow outputs consumed by `*.Arrow` parts: `arrowStyles` (`position: absolute; top: <y>px; left: <x>px`), `arrowUncentered` (→ `data-uncentered`), `side`/`align` (→ `data-side`/`data-align`). See FLT-DOM-09.

### 3.3 Shared Portal prop

| Prop | Type | Default | Description |
|---|---|---|---|
| `container` | `HTMLElement \| ShadowRoot \| null \| RefObject<HTMLElement \| ShadowRoot \| null>` | `undefined` → nearest enclosing portal node, else `document.body` | A parent element to render the portal element into. `null` means "not resolved yet": nothing is rendered until a container is supplied (FLT-API-13). (`FloatingPortal.tsx:300-312`) |
| `portalOwnerRole` | ARIA role string | `undefined` | `@ignore`'d: role of the hidden `aria-owns` owner element (FLT-ARIA-04). Only Menu forwards it. (`:307-311`) |

`FloatingPortal` also accepts `className`, `style`, `render` and arbitrary element props forwarded to the portal `<div>` (RND element substitution), tested by `FloatingPortal.test.tsx` "forwards HTML props to the portal element".

### 3.4 Types exported through component namespaces

`Side`, `Align`, `Boundary`, `OffsetFunction`, `CollisionAvoidance` (`useAnchorPositioning.ts:66-125`) are re-exported by each component's `Positioner.Props`. `Padding` (`number | Partial<SideObject>`) and `VirtualElement` (`{ getBoundingClientRect(): ClientRectObject; getClientRects?(): ClientRectObject[]; contextElement?: Element }`) come from the engine's types (`floating-ui-react/types.ts:27-68`).

## 4. Behavioral requirements

### 4.1 STR — root store and floating tree

- **FLT-STR-01** — MUST. Each popup instance owns one root store whose reactive state has exactly these fields: `open: boolean`, `transitionStatus` (TRN status or `undefined`), `domReferenceElement: Element | null`, `referenceElement: Element | VirtualElement | null`, `floatingElement: HTMLElement | null`, `positionReference: Element | VirtualElement | null`, `floatingId: string | undefined`.
  Source: `floating-ui-react/components/FloatingRootStore.ts:11-22`. Test: [L3] `floating-ui-react/hooks/useFloating.test.tsx` "uses the supplied root store while preserving DOM and position references".
- **FLT-STR-02** — MUST. The root store's *selected* reference element (the one positioning measures) is `positionReference` when non-null, otherwise `referenceElement`; a newly created store initializes `positionReference` and `domReferenceElement` to the initial `referenceElement`.
  Source: `floating-ui-react/components/FloatingRootStore.ts:37, 69-74`. Test: [L3] `useClientPoint.test.tsx` "clears virtual references on unmount without retaining a stale DOM reference".
- **FLT-STR-03** — MUST. The root store carries a non-reactive context with: `onOpenChange` callback (replaced on every render of the part), `dataRef` (context data, FLT-STR-05), `events` (root event bus, FLT-EVT-02), `nested: boolean`, and `triggerElements` (the PSH trigger map).
  Source: `floating-ui-react/components/FloatingRootStore.ts:24-31, 75-82`. Test: [L3] `useFloating.test.tsx` "uses the supplied root store while preserving DOM and position references" (asserts `dataRef`/`events` identity).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the non-reactive context is the `interactions` session keyed by the token C# draws and never renders (`acquireSession`; the token form of `attach`): `dataRef`, `events`, `nested` (from the cascaded parent token) and the trigger map the trigger handles register; `onOpenChange` is the C# store's. L3: `interactions.test.ts` "uses the supplied root store while preserving DOM and position references" (the identity half).
  Port note (2026-10-28, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-28; a fidelity correction of track 3's behaviour, conventions §3.5): a trigger handle joins the session it attached under, so `InteractionsTriggerBehavior` attaches once its trigger has a session token, attaches again under a different token, and keeps the live handle through a batch without one; a detached trigger that first registered with its handle's fallback store no longer reads its first press as an outside press. Test: [L2] `InteractionsBehaviorTests.A_trigger_handle_attaches_once_its_session_has_a_token_again_under_another_and_keeps_it_through_a_missing_one`, `PopoverPartsTests` "port: a detached trigger rendered before its root joins the root's session once the root attaches the handle"; `_qa-notes.md` and `Floating/CONTEXT.md` (the `InteractionsTriggerBehavior.cs` row).
- **FLT-STR-04** — MUST. The root event bus is a synchronous emitter: `on(name, fn)` registers (a set per name, so double registration is a no-op), `off` unregisters, `emit(name, data)` calls every listener registered for `name` in registration order with `data`.
  Source: `floating-ui-react/utils/createEventEmitter.ts:3-19`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `createEventEmitter` in `js/interactions.ts`; port title "port: the bus calls every listener of a name in registration order, a second registration is a no-op, and off removes one".
- **FLT-STR-05** — MUST. Context data (`dataRef.current`) is a mutable record with the optional keys `openEvent` (the DOM `Event` that most recently opened the popup, FLT-STA-03), `floatingContext` (the latest positioning context: `nodeId`, `elements`, `refs`, placement data; written post-commit, pre-paint on every render of the positioning session), `insideReactTree` (FLT-DIS-04, FLT-DIS-12), `__escapeKeyBubbles`/`__outsidePressBubbles` (FLT-DIS-15), and `hoverInteractionState` (shared hover state, §4.7).
  Source: `floating-ui-react/types.ts:115-119`; `floating-ui-react/hooks/useFloating.ts:112-119`; `useDismiss.ts:310-311`; `useHoverInteractionSharedState.ts:99-116`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `dataRef.current` is the session's; `floatingContext` is written by the session from its floating handle (FLT-STR-08) and never cleared once published (§10 Q13, mirrored; design point 9 settled by the orchestrator 2026-10-27); `hoverInteractionState` is the hover hooks' shared state.
- **FLT-STR-06** — MUST. A floating tree is a store with an ordered node list and its own event bus. `addNode(node)` appends; `removeNode(node)` removes that exact node object; nodes are `{ id, parentId, context? }`.
  Source: `floating-ui-react/components/FloatingTreeStore.ts:8-23`. Test: [L3] `floating-ui-react/utils/nodes.test.ts` "getNodeChildren returns an array of children, ignoring closed ones when onlyOpenChildren=true".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the session tree is one forest of all live sessions, each node's parent the enclosing popup's token from C#'s cascade (Blazor's logical tree crosses portals as React's does); `tree.nodesRef.current` and `getNodeChildren`/`getNodeAncestors` work over it. Base UI gives each Popover and PreviewCard Root its own `FloatingTree` and relies on React's portal-crossing propagation for nesting, which the port does not have; the forest stands in for both (design point 3, settled by the orchestrator 2026-10-27).
- **FLT-STR-07** — MUST. A tree is made available to descendants by a `FloatingTree` boundary (which creates one store for its lifetime, or adopts an explicitly supplied external tree), and a popup registers a node `{ id, parentId }` post-commit, pre-paint for as long as it is rendered, where `id` is a generated unique id and `parentId` is the id of the nearest enclosing tree node boundary (`FloatingNode`) or `null`. A hook that is passed an explicit `externalTree` uses it instead of the ancestor-provided one.
  Source: `floating-ui-react/components/FloatingTree.tsx:17-26, 32-50, 62-72, 90-95`. Test: [L3] `useDismiss.test.tsx` "prop: bubbles.outsidePress when false".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): a node is registered while a floating handle with `Node` (default on) is attached to the session; its parent is the session's `ParentToken`.
- **FLT-STR-08** — MUST. On every render of the part that owns a positioning session, the registered tree node with the session's `nodeId` has its `context` set to the session's positioning context (so tree queries can read `context.open`, `context.elements.floating`, `context.elements.domReference`, `context.dataRef`).
  Source: `floating-ui-react/hooks/useFloating.ts:112-119`. Test: [L3] `safePolygon.test.ts` "does not close when a nested child is open".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the node's `context` is a live view the session writes from its floating handle (`open`, `elements`, `dataRef`, `events`, `nodeId`, and the `placement` the `positioning` module publishes with `setPlacement`), never written by `positioning`.
- **FLT-STR-09** — MUST. `nested` is true for a root store created while a tree node boundary is in scope (parent node id not `null`); it is published on `openchange` details (FLT-EVT-02) and read by the focus manager (FLT-FOC-08).
  Source: `floating-ui-react/hooks/useFloatingRootContext.ts:28, 75`; `useSyncedFloatingRootContext.ts:28, 91`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `nested` is true when the session handle's options carry `ParentToken`.
- **FLT-STR-10** — MUST. A root store synced from a popup store (`useSyncedFloatingRootContext`) mirrors, post-commit pre-paint whenever any of them changes: `open`, `floatingId`, `referenceElement` ← the popup store's `activeTriggerElement`, `floatingElement` ← `positionerElement` (or `popupElement` when `treatPopupAsFloatingElement` is true); `domReferenceElement` is updated only when the reference is a DOM element; `positionReference` follows the new reference only when it currently equals the old `referenceElement` (a custom position anchor, e.g. from `useClientPoint` or the `anchor` prop, is not overwritten). It also writes `floatingId` back into the popup store.
  Source: `floating-ui-react/hooks/useSyncedFloatingRootContext.ts:49-91`. Test: none (covered indirectly by every popup component's trigger-switch tests).
- **FLT-STR-11** — MUST. A standalone root store (`useFloatingRootContext({ open, onOpenChange, elements })`) starts closed by default, syncs `open`, `floatingId` and, when supplied, `elements.reference` (also as `domReferenceElement` when it is a DOM element, else `null`) and `elements.floating` post-commit pre-paint, and never runs root-owned side effects itself (`syncOnly: false`, so `setOpen` dispatches `openchange`, FLT-STA-01).
  Source: `floating-ui-react/hooks/useFloatingRootContext.ts:24-77`. Test: [L3] `useClientPoint.test.tsx` "updates position from trigger props" (via the test shim `test/floating-ui-tests/useFloating.ts:18-21`).

### 4.2 STA — open state and open-event tracking

- **FLT-STA-01** — MUST. `setOpen(nextOpen, details)` on a root store MUST, when the store is in *sync-only* mode, only forward to the current `onOpenChange` callback; otherwise it MUST first run `dispatchOpenChange` (FLT-STA-02) and then call `onOpenChange(nextOpen, details)`. Popup-store-backed roots are sync-only (the popup store runs `dispatchOpenChange` itself, PSH).
  Source: `floating-ui-react/components/FloatingRootStore.ts:126-135`; `utils/popups/popupStoreUtils.ts:81`. Test: [L3] `useClick.test.tsx` "opens and closes on repeated clicks".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the M4 popup stores run the consumer's `OnOpenChange` first and relay only a change it did not cancel (FLT-EVT-02's fidelity correction); the module never sets `open`, it requests (`OnRequest`, `OnPress`, the `press` and `escape` points) and the session emits `openchange` when the relay's revision moves. L3: the `useClick` titles in both modes.
- **FLT-STA-02** — MUST. `dispatchOpenChange(nextOpen, details)` MUST first update the recorded open event (FLT-STA-03) and then emit `openchange` on the root event bus with `{ open: nextOpen, reason: details.reason, nativeEvent: details.event, nested, triggerElement: details.trigger }`.
  Source: `floating-ui-react/components/FloatingRootStore.ts:106-118`; `internals/types.ts:9-15`. Test: [L3] `useFocus.test.tsx` "does not reopen when focus is restored after leaving the tab" (relies on the event).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `dispatchOpenChange` is the session's application of the relay in the session handle's `update`: it records the open event (FLT-STA-03), emits `openchange` with `{ open, reason, nativeEvent, nested, triggerElement }`, then applies the committed state and notifies the hooks' post-commit effects. `nativeEvent` is the session's own request event when the committed change is that request, else a `{ type, pointerType }` stand-in built from the relay, or `undefined` without an event. L3: "does not reopen when focus is restored after leaving the tab" and the session's port titles.
- **FLT-STA-03** — MUST. The recorded open event (`dataRef.current.openEvent`) MUST be set to `details.event` when opening and the popup is currently closed, or when opening with a click-like event (FLT-STA-04) regardless of current state; it MUST be set to `undefined` when closing; it MUST be left unchanged when a non-click-like open (hover/focus) arrives while already open. (A pending hover-open never downgrades a click-open; a click upgrades a hover-open.)
  Source: `floating-ui-react/components/FloatingRootStore.ts:91-101`. Test: [L3] `useClick.test.tsx` "stickIfOpen true keeps a hover-opened popup open on first click".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the recorded open event follows the relayed change; for a change C# started (a C# `onclick`, a handle's open) it is the relay's stand-in.
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); gap 16, ADR 04 Amendments 2026-10-29, settled by the orchestrator 2026-10-29): `useClick` writes `dataRef.openEvent` in its click handler, while the port upgraded it only when C# relayed the commit, so on Server a leave within the press's round trip still found a hover open. The `interactions` module now records a press's click-like open event on its request (the rule above, as a hover open awaiting its commit already counts as open), the relay's commit confirms it, and an answer that leaves the request pending (C# canceled it) restores the previous event. Test: [L3] `interactions.test.ts` "port: a press on a hover-opened popup records the click as the open event on its request, before the commit", "port: a press whose request C# cancels rolls the recorded open event back".
- **FLT-STA-04** — MUST. An event is *click-like* if its `type` is `click`, `mousedown`, `keydown` or `keyup`.
  Source: `floating-ui-react/utils/event.ts:60-63`. Test: [L3] `useClick.test.tsx` "stickIfOpen false closes a hover-opened popup on first click".
- **FLT-STA-05** — MUST. An open event is a *hover open* if its type contains `mouse` and is not `mousedown` (`mouseenter`, `mousemove`, `mouseover`).
  Source: `floating-ui-react/hooks/useHoverShared.ts:70-72`. Test: [L3] `FloatingFocusManager.test.tsx` "focus does not return to reference when floating element is triggered by hover".
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the cited title runs at L3 with the hover region's real open and leave (`focus-manager.test.ts` "with the interactions session"): green.
- **FLT-STA-06** — MUST. The hover hooks treat the popup as *click-like open* when the recorded open event is click-like **or** the user has interacted inside the popup with a nested-popup trigger (FLT-PTR-22).
  Source: `floating-ui-react/hooks/useHoverShared.ts:66-68`. Test: none.
- **FLT-STA-07** — MUST. Setting the position anchor through the positioning session (`refs.setPositionReference(node)`) MUST, for a DOM element, store a virtual anchor `{ getBoundingClientRect: () => node.getBoundingClientRect(), getClientRects: () => node.getClientRects(), contextElement: node }`, and for a virtual anchor or `null` store it as-is; the DOM anchor (`domReferenceElement`, `refs.domReference`) MUST be left unchanged.
  Source: `floating-ui-react/hooks/useFloating.ts:56-71, 106-110`. Test: [L3] `useFloating.test.tsx` "uses the supplied root store while preserving DOM and position references".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "positions against the active trigger, or a custom anchor wrapped so that its own rect is measured (FLT-STA-07, FLT-POS-13)" and "positions against a virtual anchor as given, re-sent on change (FLT-STA-07)". The module keeps the active trigger as the DOM reference and hands the engine the wrapped anchor. The session's `refs` belong to the `interactions` session (FLT-STA-08, track 3).
- **FLT-STA-08** — MUST. The positioning session's `context` MUST expose the root store's `dataRef` and `events` by identity, the current `open`, `floatingId`, `nodeId`, `refs` (`reference`, `floating`, `domReference`, `setReference`, `setFloating`, `setPositionReference`), `elements` (`reference`, `floating`, `domReference`) and `onOpenChange = store.setOpen`.
  Source: `floating-ui-react/hooks/useFloating.ts:73-104`. Test: [L3] `useFloating.test.tsx` "uses the supplied root store while preserving DOM and position references".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 04 Amendments 2026-10-27, the session through the `interactions` export): open to M4 track 3. The handle publishes its context to the session (`acquireSession`, `setPlacement`) once the export is on `main`. Until then `PositioningOptions.SessionToken` is sent and inert. It turns green in this pull request after merging `main`, or in a follow-up pull request. 2026-10-28 (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): the export is on `main` with PR 129. The wiring in `positioning.ts` and its L3 title are track 2's follow-up (§10 item 15).
  Port note (2026-10-28, M4 track 2's follow-up, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 04 Amendments 2026-10-27, the session through the `interactions` export, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "takes the session of its token, publishes each committed placement to it, positions against its position reference while one is set, and releases it at dispose (FLT-STA-08)", against the real session. The session is the root store and owns the context (FLT-STR-08): its `dataRef` and `events` are the ones every module of the popup reads, by identity, and the trigger stays its DOM reference. The Positioner's part in the context is the placement (`setPlacement` after each committed pass, and at once to the session of a new token) and the position reference. Base UI keeps two slots: the Positioner's anchor is registered in `useFloating`'s own position reference, which overrides the root store's reference, and the store's reference is its `positionReference` (the client point, the hovered line) or else the trigger (FLT-STR-02). So a virtual anchor or an anchor element is measured first, then the session's `positionReference` while it is set, then the trigger, and the handle re-resolves on `subscribePositionReference`; L3 "an anchor outranks the session's position reference, which outranks the trigger (FLT-STR-02, FLT-STA-08)" and "publishes the placement of the last committed pass to the session of a new token at once (FLT-STA-08)". This restates the cited title over the session. The row is observable at L4 only once a part composes both the `positioning` and the `interactions` floating handle, Popover's Positioner (M4 track 5, with the attach-order title of §10 item 16).
- **FLT-STA-09** — MUST. While a Positioner is not mounted (`mounted === false`), the positioning layer MUST clear the root store's `referenceElement`, `floatingElement`, `domReferenceElement` and `positionReference` to `null` (post-commit, pre-paint), so that a kept-mounted popup does not position against stale elements until it is shown again.
  Source: `internals/useAnchorPositioning.ts:459-470`. Test: none. Docs: `CHANGELOG.md` "Fix `Positioner` not repositioning to a different trigger when reopened with `keepMounted` (#4407)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 7, settled by the orchestrator 2026-10-27): port titles at L3, `src/Blazix.Primitives/js/positioning.test.ts` "tracks the anchor while mounted with the configured options, and a kept-mounted popup only while mounted (FLT-STA-09, FLT-POS-11, FLT-POS-12)" and "starts no positioning pass while the anchor does not resolve, and starts when it does (FLT-STA-09)". Mounted is the absence of the Positioner's `hidden`, read from the applied batch; a Positioner that is not mounted positions against nothing.
  Port note (2026-10-28, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); the portal's move as a task, ADR 04 Amendments 2026-10-27, M4 track 1, settled by the orchestrator 2026-10-27): a Positioner under a Portal attaches only once the portal module has moved the node, which is `display: none` in place until then. Green at L2, `PositionerBehaviorTests` "attaches a positioner under a portal only once the portal has moved its node", and at L4, `tests/browser/positioning.spec.ts` "port: a Positioner under a Portal attaches after the move, so its first pass measures it in its container" (`positioning-lifecycle`, `portaled`), both modes.

### 4.3 API — option normalization

- **FLT-API-01** — MUST. `useDismiss` `bubbles` normalization: a boolean applies to both keys; an object or `undefined` yields `escapeKey = bubbles?.escapeKey ?? false` and `outsidePress = bubbles?.outsidePress ?? true`.
  Source: `floating-ui-react/hooks/useDismiss.ts:35-45`. Test: [L3] `useDismiss.test.tsx` "prop: bubbles › normalizeProp" ("undefined", "when false", "{}", "{ escapeKey: false }", "{ outsidePress: false }"); "prop: capture › normalizeProp" ("undefined", "{}", "when true", "when false", "{ escapeKey: true }", "{ outsidePress: false }") exercises the same function (there is no `capture` option in 1.9.0; see §10).
- **FLT-API-02** — MUST. `outsidePressEvent` (default `'sloppy'`) is resolved at the time of each pointer event: a function is called; a string is used directly; an object is indexed by the current pointer type where `'touch'` selects `touch` and `'mouse'`, `'pen'` or an empty/unknown type selects `mouse`.
  Source: `floating-ui-react/hooks/useDismiss.ts:351-366`. Test: [L3] `useDismiss.test.tsx` "outsidePressEvent: intentional › dragging outside the floating element then clicking outside closes with mouse clicks".
- **FLT-API-03** — MUST. Hover delay resolution `getDelay(value, 'open' | 'close', pointerType)`: if a pointer type is given and is not mouse-like (non-strict), the result is `0`; otherwise a function value is called first; a number applies to both phases; an object yields `value[phase]` (possibly `undefined`, which callers treat as "no delay").
  Source: `floating-ui-react/hooks/useHoverShared.ts:31-57`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "prop: delay › symmetric number", "open", "close".
- **FLT-API-04** — MUST. `restMs` MAY be a number or a function returning a number; it is resolved by calling the function at each use.
  Source: `floating-ui-react/hooks/useHoverShared.ts:59-64`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "restMs".
- **FLT-API-05** — MUST. `FloatingFocusManager` `initialFocus` / `returnFocus` values resolve as: `false` or `undefined` (including a function returning `undefined`) → do nothing; `true` or `null` (including a function returning `null`) → default behavior; a DOM handle or element → that element (an empty handle falls back to default); a function is called with the open/close interaction type (`'mouse' | 'touch' | 'pen' | 'keyboard' | ''`).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:171-200, 694-725, 847-879`. Test: [L3] `FloatingFocusManager.test.tsx` "prop: initialFocus › ref", "prop: returnFocus › ref", "does not insert fallback element when return element is falsy".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27, "Focus targets", and ADR 08 Amendments 2026-10-27, "Unions", chosen by the user 2026-10-27): C# resolves `initialFocus` and `returnFocus` (the value union or the delegate over the interaction type) when the open or close commits, and the module receives the result: `FocusManagerOptions.InitialFocus` / `ReturnFocus` (`false` does nothing; `true` takes the target, else the default), and the target as an element, by the index of a reference passed with the options (`InitialFocusElement`, `ReturnFocusElement`; the popup core's references array), or an element id (`InitialFocusId`, `ReturnFocusId`). A target that resolves to nothing falls back to the default, the empty-handle clause. The delegate's call, once per open with the open type and once per close with the close type, is each family's (PSH-FOC-03, PSH-FOC-05). "prop: initialFocus › ref" and "prop: returnFocus › ref" run at L3 with the element passed as a reference; 2026-10-28, after track 1 merged: "does not insert fallback element when return element is falsy" is green at L3, restated: the port's in-place portal wrapper (FLT-DOM-11's note) is the reference's sibling, and nothing else is inserted beside it; the transport is green at L2 (`FocusManagerBehaviorTests` "port: element targets cross as references, id targets as ids, and false sends no focus": an element the render has not captured yet names nothing, so the module takes the default target). The options, the targets among them, are sent before the batch that applies them (design point 1 of the focus-manager track, chosen by the user 2026-10-27; ADR 01 and ADR 04 Amendments 2026-10-27, the commit path); a popup the opening render creates resolves its element targets at attach.
- **FLT-API-06** — MUST. `useClick` `stickIfOpen` (default `true`) MAY be a getter; it is evaluated when the press is handled, not when configured.
  Source: `floating-ui-react/hooks/useClick.ts:42, 117`. Test: [L3] `useClick.test.tsx` "stickIfOpen true keeps a hover-opened popup open on first click".
- **FLT-API-07** — MUST. `useDismiss` `referencePress` is a getter (default: always `false`) evaluated on each reference `pointerdown`/`click`.
  Source: `floating-ui-react/hooks/useDismiss.ts:31-33, 123, 187-199`. Test: [L3] `useDismiss.test.tsx` "dismisses with reference press".
- **FLT-API-08** — MUST. `collisionPadding` is normalized to `{ top, right, bottom, left }`: a number applies to all four sides; an object's missing sides are `0`.
  Source: `internals/useAnchorPositioning.ts:207-225`. Test: [L4] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 08 Amendments 2026-10-27): L4, the suite's "rests exactly at collisionPadding from the colliding edge" in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`), both modes; also L3 "normalizes the collision padding: a number for every side, missing sides as zero (FLT-API-08)". C#: `CollisionPaddingValue` (a `double` or `PaddingSides(Top, Right, Bottom, Left)`, a missing side 0), sent as `collisionPadding` or `collisionPaddingSides`; the module normalizes.
- **FLT-API-09** — MUST. `collisionBoundary: 'clipping-ancestors'` MUST be passed to the engine as `'clippingAncestors'`; any other value (element, element array, rect) is passed through unchanged.
  Source: `internals/useAnchorPositioning.ts:241-244`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port title at L3, `src/Blazix.Primitives/js/positioning.test.ts` "passes clipping-ancestors to the engine as clippingAncestors and a rect unchanged (FLT-API-09)". C#: `CollisionBoundaryValue` (`ClippingAncestors`, `ElementReference`, a list of elements, `Rect`). The element and element-list cases travel through track 1's references array (`BoundaryRefs`, ADR 04 Amendments 2026-10-27).
  Port note (2026-10-28, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 04 Amendments 2026-10-27, element-typed option values, M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the element cases are green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "passes the boundary elements the references carry to the engine, and rebuilds when an element at the same index changes (FLT-API-09)"; at L2, `PositionerBehaviorTests` "sends the anchor element and the boundary elements as indexes into the references after the options, an update when an element at the same index changes, and no reference for an element not rendered yet"; and at L4, `tests/browser/positioning.spec.ts` "port: positions against an anchor element reference and flips inside a boundary element reference" (`positioning-lifecycle`, `anchor-element`), both modes. One element is sent as a one-index list and reaches the engine as a one-element list, which the engine reads as it reads the element (`getClippingRect` concatenates the boundary into a list). An element reference not captured yet, or a list whose elements are all uncaptured, is sent as `clipping-ancestors`: an uncaptured reference never narrows the boundary (settled by the orchestrator 2026-10-28; L2 `PositionerBehaviorTests` "falls back to the clipping ancestors for a boundary element or a list of boundary elements not rendered yet"). A list drops its uncaptured elements.
- **FLT-API-10** — MUST. `collisionAvoidance` keys default individually: `side ?? 'flip'`, `align ?? 'flip'`, `fallbackAxisSide ?? 'end'`.
  Source: `internals/useAnchorPositioning.ts:167-169`. Test: [L4] `useAnchorPositioning.test.tsx` "flips back to the preferred side without lazy flipping" (`{ fallbackAxisSide: 'none' }` with implicit `side: 'flip'`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): L4 in the page, `tests/browser/positioning.spec.ts` "flips back to the preferred side without lazy flipping" (the shipped module with a stub twin), both modes. C#: `CollisionAvoidance(CollisionAvoidanceMode? Side, CollisionAvoidanceMode? Align, FallbackAxisSide? FallbackAxisSide)`, a `null` key taking its default.
- **FLT-API-11** — MUST. `anchor` resolution, performed post-commit pre-paint while mounted: a function is called; a DOM handle is dereferenced; the result (element, virtual anchor or `null`) becomes the position anchor through `setPositionReference` only when it differs from the previously registered value. Because parent-supplied DOM handles may be filled only after that phase, handle anchors are re-checked after paint and registered then if they changed.
  Source: `internals/useAnchorPositioning.ts:543-579`. Test: [L3] `useAnchorPositioning.test.tsx` "uses the visual viewport for shift by default" (anchor via handle).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "registers a changed anchor only when it differs from the registered one (FLT-API-11)" and the two shift titles (an anchor by id). `AnchorValue` has an `ElementReference` case, an element-id case and a `VirtualAnchor(X, Y, Width, Height)` case, and no function case: a changed anchor is a new value, re-sent in `update`, and a virtual anchor is read when supplied rather than on every pass. The id case is resolved on every `update` and again before a pass when a render replaced the element. The `ElementReference` case travels through track 1's references array (`AnchorRef`).
  Port note (2026-10-28, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 04 Amendments 2026-10-27, element-typed option values, M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the `ElementReference` case is green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "positions against the anchor element the references carry, wrapped so that its own rect is measured, and registers the one a later update carries at the same index (FLT-API-11)"; at L2, `PositionerBehaviorTests` "sends the anchor element and the boundary elements as indexes into the references after the options, an update when an element at the same index changes, and no reference for an element not rendered yet"; and at L4, "port: positions against an anchor element reference and flips inside a boundary element reference", both modes, where the reference is captured after the first render as a consumer's is, and the later `update` registers it. An element reference not captured yet sends nothing, so the popup positions against the trigger, as Base UI does for an empty handle.
  Port note (2026-10-28, M4 track 2's second follow-up, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); settled by the orchestrator 2026-10-28): an anchor that does not resolve, an id that names no element or an element the references carry as `null`, is Base UI's `null` anchor (`unwrappedElement || null`, registered through `setPositionReference(null)`), which leaves the store's reference in force: the handle positions against the session's `positionReference` while one is set, else the trigger, the order of FLT-STA-08's note, and against the anchor once it resolves, at the next update or refresh (a session notification, a change of mount, a reference a render replaced), as Base UI's effects re-check it. Green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "an anchor that does not resolve falls back to the session's position reference while one is set, then the trigger, as a null anchor does (FLT-API-11, FLT-STA-08)". Until then an id that named no element positioned against nothing.
- **FLT-API-12** — MUST. An `OffsetFunction` for `sideOffset`/`alignOffset` receives `{ side, align, anchor: { width, height }, positioner: { width, height } }` where `side` is the *logical* side of the placement currently being evaluated (FLT-I18N-02), `align` is the placement's alignment or `'center'`, and the dimensions come from the engine's measured anchor and floating rects; it is called on every positioning pass (so it observes flipped placements).
  Source: `internals/useAnchorPositioning.ts:55-66, 264-285`. Test: [L4] `test/positionerConformanceTests.tsx` "reads the latest side after a flip inside the function", "reads the latest align after a flip inside the function", "reads the logical side after a flip inside the function", "reads the logical side in RTL mode inside the function" (the four expansions of the template "reads $description inside the function", each run per "prop: $prop" offset case).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27; the fields settled by the orchestrator 2026-10-27, design point 2): L4 on `FixturePositioner`, both modes. The offset function is the declarative `Offset(Constant, AnchorWidth, AnchorHeight, PositionerWidth, PositionerHeight)` with `Overrides` of `OffsetOverride(Side?, Align?, …the same terms)`; the first override whose non-null keys match the logical side and alignment of the placement being evaluated wins. The module evaluates it on every pass. The four titles are restated as "applies the override for the latest side after a flip", "applies the override for the latest align after a flip", "applies the override for the logical side after a flip" and "applies the override for the logical side in RTL mode" (each per "prop: $prop", `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`)), witnessed by the pixel position the override produces and by `data-side`/`data-align`. Also L3: `evaluateOffset` and "feeds the offset middleware the logical side and align of the placement each pass evaluates (FLT-API-12, FLT-POS-06)".
- **FLT-API-13** — MUST. Portal container resolution: `container === null` → no portal node is created (and an existing one is torn down); otherwise the container is the supplied element/shadow root (a DOM handle is dereferenced), else the nearest enclosing portal's node, else `document.body`. A change of the resolved container tears down the portal node and recreates it inside the new container.
  Source: `floating-ui-react/components/FloatingPortal.tsx:93-123`. Test: [L3] `FloatingPortal.test.tsx` "allows custom containers", "allows refs as containers", "allows containers to be initially null", "reattaches the portal when the container changes".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): re-tagged [L4] → [L3] (settled by the orchestrator 2026-10-27, plan §Settled "Layer tags and browser-only titles"): the cited jsdom titles run on the `portal` module (`js/portal.test.ts`), and the L4 port titles on the test-only `FixturePortal` (`tests/browser/portal.spec.ts`: a pending container, a change of container, a nested portal) are green on both render modes. The container crosses as a reference or a selector (ADR 04 Amendments 2026-10-27, decision 5). A selector is matched with `querySelector` in the document of the node's in-place wrapper, the first match, and is resolved again on each update; one that matches nothing keeps the node hidden in place.
- **FLT-API-14** — MUST. `useClientPoint` `axis` (default `'both'`) restricts cursor anchoring to one axis; on the other axis the virtual anchor uses the DOM anchor's coordinate and extent (FLT-ALG-25).
  Source: `floating-ui-react/hooks/useClientPoint.ts:88-94, 34-59`. Test: [L3] `useClientPoint.test.tsx` "axis x", "axis y", "keeps the reference size on the untracked axis when tracking $axis" (2 variants: x, y).
- **FLT-API-15** — MUST. `useFocus` `delay` (default `undefined`) is a number of ms; `0`/`undefined` means open synchronously on focus.
  Source: `floating-ui-react/hooks/useFocus.ts:32-36, 158-183`. Test: [L3] `useFocus.test.tsx` "does not reopen when focus is restored after leaving the tab" (`delay: 100`).
- **FLT-API-16** — MUST. `FloatingDelayGroup` takes `delay` (`number | { open?, close? }`) and `timeoutMs` (default `0`); `useDelayGroup(store, { open })` returns `{ delayRef, activeIdRef, isInstantPhase, hasProvider }` where `hasProvider` is `false` and `delayRef.current` is `0` when no group encloses the caller.
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:25-33, 39-52, 139-157, 269-277`. Test: [L3] `FloatingDelayGroup.test.tsx` "groups delays correctly".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the group's delays arrive in the provider handle's options (`DelayOpen`, `DelayClose`, `Timeout`), resolved by C#.
- **FLT-API-17** — MUST. `useAnchorPositioning` private parameters (not consumer-facing; listed so component docs can cite them): `mounted` (required), `keepMounted` (default `false`), `nodeId`, `externalTree`, `adaptiveOrigin` (a middleware from PSH `usePopupViewport`, or `undefined`), `shift: { crossAxis?: boolean; rootBoundary?: 'layoutViewport' }`, `lazyFlip: boolean | 'placement'` (default `false`), `inline` (a middleware that may replace the measured anchor rect before offsets run; used by Preview Card, see `components/preview-card.md`).
  Source: `internals/useAnchorPositioning.ts:813-838`. Test: [L3] `useAnchorPositioning.test.tsx` "uses the configured shift options".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 12 Amendments 2026-10-27 item 4): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "uses the visual viewport for shift by default" and "uses the configured shift options" (×2), verbatim with the shift spy on `./floating-ui`. `shift` and `lazyFlip` have no M4 consumer. They are branches of the assembly, so they are built and proven, sent as `PositioningOptions.Shift` and `LazyFlip` (`side` for `true`, `placement`).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

* FLT-DOM-08 — mirrored fields `side`, `align`, `anchorHidden`, and the Arrow's `uncentered` (RND-STA-01 row; the Arrow's field added 2026-10-27, M4 track 2, ADR 08 Amendments 2026-10-27): the data attribute is JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.
* FLT-KEY-01 — the consumer's `cancel()` / `allowPropagation()` decision arrives a round trip after the Escape dispatch ends; JS applies the fallback of ADR 04 §3 (prevent and stop whenever the owning popup is the innermost open one); WebAssembly meets the statement through the synchronous `decide()` path.
* FLT-PTR-03, FLT-ALG-16 — the consumer's C# veto of a press (the `press` decision point): on Server the module reports the press and C# runs the consumer's `onclick` and the request a round trip later, the veto still holding because C# makes the request (row "C#-origin consumer vetoes", 2026-10-27, M4 track 3). Server: see ADR 01.
* FLT-EVT-05 — the consumer's `cancel()` / `allowPropagation()` of an Escape close arrives a round trip after the dispatch, as FLT-KEY-01's bullet says; a canceled close re-arms the session when C#'s answer arrives (row "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01", 2026-10-27, M4 track 3). Server: see ADR 01.
* FLT-ALG-23, FLT-ALG-24 — the hover, rest, close and group timers run in JavaScript and report a request when they fire; on Server the open or close commits a round trip later (row "EVD-EVT-18, PSH-ANI-03", 2026-10-27, M4 track 3). Server: see ADR 01.
* FLT-POS-16 — frame-rate loop: met on both modes because C1 places the whole positioning loop in JavaScript; listed so it is not moved back to C#.
* FLT-ALG-13 — frame-rate loop (anchor tracking): met on both modes because C1 places it in JavaScript; listed so it is not moved back to C#.
* FLT-FOC-11 — focus-out close committed in the microtask after the `focusout`: the module decides and reports the request; C# commits it a round trip later on Server and before the next task on WebAssembly (a row of the PSH-KEY-07 kind; settled by the orchestrator 2026-10-27, M4 track 4). Revised 2026-10-29 (M4 track 4's follow-up, [PR 145](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/145); ADR 01 Amendments 2026-10-29; M4 track 5's Gap 12, settled by the orchestrator 2026-10-28): the decision stays in the microtask and the report waits until the focused element is known: at once when the active element is not the body, otherwise at the next capture `focusin` (the browser's own focus move, in the same task) or a 0 ms task when focus lands nowhere, whichever comes first (FLT-FOC-11's port note of that date).

### 4.4 DOM — rendered output of the positioning layer, portal and focus manager

#### Positioner element

- **FLT-DOM-01** — MUST. The shared Positioner element (`usePositioner`) renders a `<div role="presentation">` with: the `hidden` attribute when the component says so (every Positioner passes `hidden: !mounted`); an inline `style` equal to the positioner styles (FLT-DOM-02..04) plus `pointer-events: none` when `inert` is requested (Positioners pass `inert: !open`); the TRN disabled-mount-transition styles for the current transition status; the component's own element props; and `data-*` attributes from its state via the PSH `popupStateMapping` (`data-open` when `open`, `data-closed` otherwise, `data-anchor-hidden` when `anchorHidden`) with the remaining state fields (`side`, `align`, `instant`) emitted by the RND default state-attribute mapping (`data-side="<value>"`, `data-align="<value>"`, `data-instant="<value>"` when defined). Element substitution, `className`/`style` functions and DOM-handle forwarding follow RND.
  Source: `utils/usePositioner.tsx:21-41`; `utils/popupStateMapping.ts:54-64`; `popover/positioner/PopoverPositioner.tsx:109-124`. Test: [L4] `test/positionerConformanceTests.tsx` "reads the latest side after a flip inside the function" (asserts `data-side`/`data-align`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4 through the suite's restated "applies the override for the latest side after a flip" (`data-side`, `data-align`) in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`). The C# half is at L2, `PositionerBehaviorTests` "renders a presentation element with the requested placement and the pre-position style before the first pass": role, `hidden` while not mounted, `pointer-events: none` while closed. `data-open` and the state projection are the families'.
- **FLT-DOM-02** — MUST. Until the current open session has been positioned (`isPositioned === false`), the positioner styles MUST be `position: fixed; top: 0; left: 0; opacity: 0` (coordinates retained from a previous open or from a 0×0 measurement MUST NOT be applied).
  Source: `internals/useAnchorPositioning.ts:509, 511-519, 537-539`. Test: [L4] `test/waitForPositioned.ts:4-11` (helper waits for `opacity !== '0'`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): L4 port title `tests/browser/positioning.spec.ts` "port: the Positioner stays fixed at the viewport origin and transparent until the first pass of each mount" (`positioning-lifecycle`, `kept-mounted`), both modes, witnessed by `MutationObserver` records; also L3 "keeps a kept-mounted Positioner fixed at the origin and transparent until the pass of each new mount". C# renders the four declarations before attach and freezes them; the module owns them from attach.
  Port note (2026-10-28, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); the portal's move as a task, ADR 04 Amendments 2026-10-27, settled by the orchestrator 2026-10-27): for a portaled Positioner, the first pass runs only after the portal's move, so no coordinates from a 0×0 measurement in place are applied: the L4 title "port: a Positioner under a Portal attaches after the move, so its first pass measures it in its container" records the first transform the module writes and where the Positioner was then, both modes; L2 "attaches a positioner under a portal only once the portal has moved its node".
- **FLT-DOM-03** — MUST. Once positioned and when no adaptive-origin middleware is configured, the positioner styles MUST be the engine's floating styles with `position` set to `positionMethod`: `left: 0; top: 0; transform: translate(<x>px, <y>px)` where `x`/`y` are the computed coordinates rounded to the device pixel grid (`round(v * dpr) / dpr`, `dpr = devicePixelRatio || 1`), plus `will-change: transform` when `dpr >= 1.5`.
  Source: `internals/useAnchorPositioning.ts:509, 521-523`; `vendor/@floating-ui_react-dom/dist/floating-ui.react-dom.mjs:62-73, 204-229`. Test: [L4] `test/positionerConformanceTests.tsx` "uses transform positioning without Viewport", "offsets the positioner when a number is specified".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's "uses transform positioning without Viewport" and "offsets the positioner when a number is specified" in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`), and the port title "port: will-change is set at a device pixel ratio of 1.5 or more" (a context with `deviceScaleFactor: 2`), both modes.
- **FLT-DOM-04** — MUST. Once positioned and when an adaptive-origin middleware is configured (a Viewport part is present, PSH), the positioner styles MUST be `position: <positionMethod>` plus `<sideX>: <x>px` and `<sideY>: <y>px` where `sideX ∈ {left, right}`, `sideY ∈ {top, bottom}` and `x`/`y` come from the adaptive-origin middleware data (FLT-ALG-12); no `transform` is set.
  Source: `internals/useAnchorPositioning.ts:505, 519-521`. Test: [L4] `test/positionerConformanceTests.tsx` "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 8, settled by the orchestrator 2026-10-27; ADR 13 Amendments 2026-10-22, the Viewport incidental): L4, the suite's "uses top/left positioning with Viewport" and "updates positioning when Viewport mounts and unmounts" over `FixturePositioner`'s `AdaptiveOrigin` toggle, which stands in for the Viewport part (PSH-POS-02 only supplies the middleware). The families run them again over their Viewport parts.
- **FLT-DOM-05** — MUST. On every positioning pass while mounted, the size middleware MUST write `--available-width: <availableWidth>px` and `--available-height: <availableHeight>px` directly onto the floating element's inline style (values per FLT-ALG-08 with Base UI's collision options); when not mounted it MUST write nothing. Independently, the positioner styles MUST always contain `--available-width: 100vw` and `--available-height: 100vh` as constant seed values so that consumer rules such as `max-height: min(x, var(--available-height))` resolve on the first pass; the imperatively written px values take precedence because they are written later to the same element.
  Source: `internals/useAnchorPositioning.ts:360-369, 524-535`. Test: [L4] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge" (popup styled with `max-height: var(--available-height)`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 04 Amendments 2026-10-27, the layered-style writer): L4, the suite's collision title in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`). C# renders the 100vw/100vh seeds (L2). The measured px values are written in the `positioning-measured` layer of `dom`'s writer, which wins over the consumer's string, as Base UI's `setProperty` does.
- **FLT-DOM-06** — MUST. On every positioning pass while mounted, the size middleware MUST also write `--anchor-width` and `--anchor-height` (px) equal to the anchor rect's extent snapped to device pixels: `width = (round((x + width) * dpr) - round(x * dpr)) / dpr`, likewise for height.
  Source: `internals/useAnchorPositioning.ts:370-378`. Test: none. Docs: `CHANGELOG.md` "Snap `--anchor-width` and `--anchor-height` to device pixel grid (#4082)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port title at L3, `src/Blazix.Primitives/js/positioning.test.ts` "composes the consumer between the positioner styles and the measured variables, and clears its layers on dispose" (`--anchor-width: 72px` from the snapped anchor rect).
- **FLT-DOM-07** — MUST. On every positioning pass the `transformOrigin` middleware MUST write `--transform-origin` on the floating element's inline style, formatted `<x> <y>` (vertical sides: `<crossOrigin> <sideOrigin>`; horizontal sides: `<sideOrigin> <crossOrigin>`) with the values of FLT-ALG-11.
  Source: `internals/useAnchorPositioning.ts:392-452`. Test: [L4] `popover/positioner/PopoverPositioner.test.tsx` "transform origin" (19 tests, lines 295-700; browser only). Docs: `docs/src/app/(docs)/react/handbook/animation/page.mdx:23` (`transform-origin: var(--transform-origin)`), `CHANGELOG.md` "Fix `transform-origin` variable calculation when Positioner `sideOffset` is a function (#2511)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 8, settled by the orchestrator 2026-10-27; ADR 13 Amendments 2026-10-22, Popover incidental): L4, the 19 "transform origin" titles verbatim in `tests/browser/positioning.spec.ts` on `FixturePositioner` (`positioning-origin`), both modes. Popover reaches them again through its Positioner.
- **FLT-DOM-08** — MUST. The positioning result exposed to the Positioner (and through its context to Popup/Arrow parts) MUST include: `side` (logical rendered side, FLT-I18N-02), `align` (rendered alignment, `'center'` when the placement has none), `physicalSide`, `anchorHidden` (FLT-POS-09), `isPositioned`, `positionerStyles`, `arrowStyles`, `arrowRef`, `arrowUncentered`, `refs`, `context`, `update()`. Server: see ADR 01 relaxation table.
  Source: `internals/useAnchorPositioning.ts:588-591, 627-657, 840-853`. Test: [L4] `test/positionerConformanceTests.tsx` "reads the latest align after a flip inside the function".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 3, settled by the orchestrator 2026-10-27): L4, the suite's restated "applies the override for the latest align after a flip", and the port title "port: data-side flips with the positioning pass and the C# mirror follows, a round trip later on Server" (the RND-STA-01 row per mode under `page.routeWebSocket`). `physicalSide`, `isPositioned` and `update()` stay JavaScript's, exported as `readPlacement(element)` and `update(element)` for the `popup` module and PreviewCard. The report `OnPlacementChange` carries `side`, `align`, `anchorHidden` and `uncentered` once per change, mirrored by `PositionerMirror`.
- **FLT-DOM-09** — MUST. `arrowStyles` MUST be `{ position: 'absolute', top: <arrow.y>, left: <arrow.x> }` using the arrow middleware data (either coordinate `undefined` when the arrow did not set that axis), and `arrowUncentered` MUST be `true` iff the arrow data's `centerOffset !== 0` (also `true` when there is no arrow data). Arrow parts emit `data-uncentered` from it (component docs).
  Source: `internals/useAnchorPositioning.ts:616-625`. Test: none. Docs: `docs/src/app/(private)/experiments/anchor-positioning.tsx:126-138` (hides the arrow when uncentered).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 6, settled by the orchestrator 2026-10-27): port titles at L3, `src/Blazix.Primitives/js/positioning.test.ts` "writes the arrow styles and attributes under the arrow consumer style from the update that carries its id (FLT-DOM-09, FLT-DOM-10)", and at L4 "port: the Arrow points at the anchor center, and is marked uncentered when it cannot" (`positioning-lifecycle`), both modes. The Arrow has no handle: its id and style travel in the Positioner's options, and it is re-resolved by id.
- **FLT-DOM-10** — MUST. The `arrowRef` DOM handle is the arrow middleware's element; when no Arrow part has registered, a detached `<div>` created in the floating element's document is used as a stand-in so that `--transform-origin` can still be computed, and the arrow padding passed to the middleware is `0` for the stand-in and `arrowPadding` for a real arrow.
  Source: `internals/useAnchorPositioning.ts:249, 381-390`. Test: [L4] `popover/positioner/PopoverPositioner.test.tsx` "transform origin › does not let the virtual arrow displace the popup on a narrow anchor" (stand-in padding `0`). Docs: `CHANGELOG.md` "Use local document for virtual arrow (#4662)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, "transform origin › does not let the virtual arrow displace the popup on a narrow anchor" in `tests/browser/positioning.spec.ts` on `FixturePositioner` (`positioning-origin`); also L3 "configures the arrow middleware with the Arrow and its padding, or a detached stand-in with none (FLT-POS-08, FLT-DOM-10)". The stand-in serves whenever `ArrowId` is absent or does not resolve.

#### Portal and focus guards

- **FLT-DOM-11** — MUST. A portal MUST render a portal node `<div id="<generated id>" data-base-ui-portal="">` appended to the resolved container (FLT-API-13), with the consumer's element props/`className`/`style`/element substitution merged (RND), and MUST render the popup subtree inside that node; nothing is rendered while the node does not exist.
  Source: `floating-ui-react/components/FloatingPortal.tsx:49, 125-141, 270`. Test: [L3] `FloatingPortal.test.tsx` "allows custom containers", "forwards HTML props to the portal element".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): re-tagged [L4] → [L3] as FLT-API-13 is; the node renders hidden in place inside the pipeline's `display: contents` host wrapper (ADR 02 Amendments 2026-10-27) and the module moves it; the wrapper is one element Base UI's DOM does not have, left in place beside the outside guards while the node is moved, and `display: contents` keeps it out of layout and of the accessibility tree; L4: "port: the portal node moves to the end of the body with the consumer attributes, and its wrapper stays in place" and issue 8's "@verifies ADR-04/3 …" on `portal-basic`, both modes.
- **FLT-DOM-12** — MUST. The portal's `nodeId` reported for `aria-owns` MUST be the id actually rendered on the portal node (an `id` prop or element substitution may override the generated one), so that `aria-owns` never references an absent id.
  Source: `floating-ui-react/components/FloatingPortal.tsx:143-151`. Test: [L2] `FloatingPortal.test.tsx` "uses the rendered portal ID for the aria-owns relationship".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green at L2 on the test-only portal (`PopupPortalTests`, "uses the rendered portal ID for the aria-owns relationship").
- **FLT-DOM-13** — MUST. A focus guard is a `<span>` with `tabindex="0"`, `data-base-ui-focus-guard=""`, a `data-type` of `"inside"` (rendered by the focus manager) or `"outside"` (rendered by the portal), the UTL visually-hidden style (`position: fixed; top: 0; left: 0; clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0`), and `aria-hidden="true"`, except that when the platform is detected as VoiceOver on WebKit it MUST instead get `role="button"` and no `aria-hidden` (post-commit, pre-paint after first render).
  Source: `utils/FocusGuard.tsx:10-42`; `packages/utils/src/visuallyHidden.ts:3-21`. Test: none.
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): port titles: the guards' markup at L2 (`PopupPortalTests`), the VoiceOver-on-WebKit swap at L3 (`portal.test.ts`), including guards rendered after attach.
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 02 and ADR 04 Amendments 2026-10-27): C# renders every focus guard (the trigger guards, the inside guards before and after the Popup element, the portal's outside guards), and the module that owns a guard's behaviour listens on it: this module on the inside guards, found by position (`data-base-ui-focus-guard`, `data-type="inside"`, the Popup element's siblings). The VoiceOver `role="button"` swap (and the removal of `aria-hidden`) is that module's JavaScript write after attach: L3 port title "gives the inside guards role=\"button\" and no aria-hidden under VoiceOver on WebKit".
- **FLT-DOM-14** — MUST. The portal MUST render an *outside* focus guard before and after the portaled subtree (in the trigger's DOM position, not inside the portal node), plus an `aria-owns` owner element between them (FLT-ARIA-04), only while a focus manager inside the portal has published state with `modal === false` and `open === true` and the portal node exists; otherwise none of the three are rendered.
  Source: `floating-ui-react/components/FloatingPortal.tsx:192-193, 252-292`. Test: [L4] `FloatingFocusManager.test.tsx` "non-modal + FloatingPortal › does not mark reference siblings due to outside focus guards".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the render condition (a non-modal, open publication and an existing node; never in the lite portal) is green at L2 (`PopupPortalTests`); the cited title's outside marking is the focus manager's and stays open to M4 track 4.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): the cited title is green at L4 on `focus-manager-portal`, both modes (`tests/browser/focus-manager.spec.ts`), with the manager's publication (FLT-FOC-18) and the portal module's outside guards.
- **FLT-DOM-15** — MUST. The focus manager MUST render an *inside* focus guard immediately before and after its children iff `!disabled && (modal ? !isUntrappedTypeableCombobox : true) && (isInsidePortal || modal)`, where `isUntrappedTypeableCombobox` is "the DOM anchor is a typeable element with `role="combobox"` and `initialFocus === false`".
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:295, 1003-1054`; `floating-ui-react/utils/element.ts:64-69`. Test: [L3] `FloatingFocusManager.test.tsx` "untrapped combobox creates non-modal focus management".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 13 §3, ADR 02 Amendments 2026-10-27): re-tagged [L4] → [L3]: the cited title is jsdom-only and Combobox's, open to M5 (ADR 12 Amendments 2026-10-27 item 4). The render rule is C#'s (the Popup parts render the guards through `BuildBeforeElement` and `BuildAfterElement`, track 1's render path; the helper is `Floating/FocusManagerBehavior`'s, design point 6, settled by the orchestrator 2026-10-27, `!Disabled && (Modal || InsidePortal)` until Combobox), so once a family renders them its pull request moves the row's C# half to [L2]. Proven meanwhile on the test-only host, both modes: `tests/browser/focus-manager.spec.ts` "port: an enabled modal manager has its inside guards and Tab wraps through them", "port: a disabled manager has no inside guards and moves no focus", "port: a non-modal manager outside a portal has no inside guards, and Tab out of it asks for the close". 2026-10-28, after track 1 merged: the render rule is green at L2 on the behavior (`FocusManagerBehaviorTests` "port: the inside guards render before and after the popup iff the manager is enabled and modal or inside a portal") and the portaled clause at L4 ("port: the popup moves into the portal node at the end of the body with its inside guards, and Tab leaves through the outside guards to the last element and asks for the close").
- **FLT-DOM-16** — MUST. While a focus manager is open and enabled, every element outside its inside set (FLT-ARIA-03, marker variant) MUST carry the attribute `data-base-ui-inert=""` (the *inert marker*), applied per FLT-ALG-19 with reference counting so that overlapping popups do not remove each other's markers; the marker MUST be removed when the manager closes/unmounts.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:657-658`; `floating-ui-react/utils/markOthers.ts:13, 132-143, 161-171`. Test: [L3] `FloatingFocusManager.test.tsx` "false - does not apply inert to outside nodes", "false - keeps marker on top-level outside ancestor when reference has siblings".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): as FLT-ARIA-01's: the marker follows the applied batch, before paint on both modes for an attached popup, and is released by the batch that closes it or by the handle's `dispose`.
- **FLT-DOM-17** — MUST. The focus manager MUST manage the floating focus element's `tabindex` per FLT-ALG-27 (only when its `role` contains `dialog`): `tabindex="0"` plus `data-tabindex="0"` when it has no tabbable content, `tabindex="-1"` plus `data-tabindex="-1"` otherwise; a consumer-authored `tabindex` (present without `data-tabindex`) MUST be left untouched. This runs post-commit pre-paint when the manager becomes enabled or the floating element changes, and again after any focus-out of the DOM anchor (the latter only while `closeOnFocusOut` is true, since it runs inside the focus-out listener). Because every Base UI popup part spreads the shared focusable-popup props (`tabindex="-1"` without `data-tabindex`, PSH), the management is inert for all built-in `dialog`-role popups: `Dialog.Popup`/`AlertDialog.Popup`, `Drawer.Popup`, `Popover.Popup` and a Combobox popup with the input inside it always keep `tabindex="-1"` and never receive `data-tabindex` or `tabindex="0"`, even with no tabbable content (DLG-FOC-18, POP-DOM-24). The algorithm applies only to a floating focus element rendered without a `tabindex`.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:114-150, 424-427, 501-503, 992-1001`; exceptions `utils/popups/popupStoreUtils.ts:27-30`, `dialog/popup/DialogPopup.tsx:80`, `drawer/popup/DrawerPopup.tsx:367`, `popover/popup/PopoverPopup.tsx:92`, `combobox/root/AriaCombobox.tsx:1477-1480`, `combobox/popup/ComboboxPopup.tsx:90`. Test: [L3] `FloatingFocusManager.test.tsx` "floating element with no focusable elements and no listbox role gets tabIndex=0 when initialFocus is -1", "floating element with managed tabIndex is downgraded once content becomes tabbable", "floating element with listbox role ignores tabIndex setting", "handles manual tabindex on dialog floating element".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): the module manages the floating focus element's `tabindex` from attach (an ADR 04 §2 page-level write, recorded in `js/CONTEXT.md`); it is inert for every built-in Popup, which renders `tabindex="-1"`. Proven on a popup rendered without it: `tests/browser/focus-manager.spec.ts` "port: a dialog with no tabbable content and no tabindex is managed to tabindex 0 and keeps focus on Tab".
- **FLT-DOM-18** — MUST. Disabling focus inside a portal (`disableFocusInside`) MUST set `tabindex="-1"` on every tabbable element in the portal node and record each element's previous `tabindex` attribute (or `""`) in `data-tabindex`; enabling (`enableFocusInside`) MUST restore the recorded value (removing the attribute when the record is empty) and delete `data-tabindex`.
  Source: `floating-ui-react/utils/tabbable.ts:264-284`. Test: none (exercised by "standard tabbing back and forth of a non-modal floating element").
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): `disableFocusInside` and `enableFocusInside` are this module's exports, called by the `portal` module (track 1, whose row this is) and the `popup` module (track 5). On a composite item the composite's roving `tabindex` takes precedence (ADR 04 Amendments 2026-10-27, chosen by the user); the mechanism is design point 4 of the focus-manager track (settled by the orchestrator 2026-10-27): while an item carries `data-tabindex`, the `composite` module writes its roving value into `data-tabindex`, and `enableFocusInside` restores it (L3: `focus-manager.test.ts` "enableFocusInside restores a composite group with one tab stop after the stop moved while stashed", `composite.test.ts` "writes the roving value into data-tabindex while an item carries the stash"; L4, 2026-10-28: `focus-manager.spec.ts` "port: a composite group in a non-modal portal leaves the tab order with the portal and comes back with one tab stop", both modes). The `portal` module's calls are wired by M4 track 4 (2026-10-28; design point 5).
- **FLT-DOM-19** — MUST. The safe-polygon pointer-events mutation MUST set inline `pointer-events: none` on the *scope element* and `pointer-events: auto` on the anchor element and the floating element; clearing MUST remove those three inline properties, but only if this hover instance is still the registered owner of that scope element (a newer owner's mutation is left intact).
  Source: `floating-ui-react/hooks/useHoverInteractionSharedState.ts:47-97`. Test: none.

### 4.5 ARIA — attributes written by the focus manager and portal

- **FLT-ARIA-01** — MUST. While a focus manager is enabled and open with `modal === true` (or in the untrapped-typeable-combobox case, FLT-DOM-15), every element outside the *control inside set* (FLT-ARIA-03) MUST get `aria-hidden="true"`, except: `<script>` elements; any element with an `aria-live` attribute and all of its ancestors; and elements that already had a non-`false` `aria-hidden`, which are left as-is and not removed on cleanup. Counting is per element so that nested modal popups release the attribute only when the last one closes.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:619-677`; `floating-ui-react/utils/markOthers.ts:102-130, 148-159`. Test: [L3] `FloatingFocusManager.test.tsx` "true - applies aria-hidden to outside nodes", "uses aria-hidden instead of inert on outside nodes if opened with hover and modal=true"; `markOthers.test.ts` "leaves live regions, their ancestors, and scripts exposed", "preserves externally owned aria-hidden during overlapping aria-hidden locks".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27, "The scroll lock, the focus manager's open input and nested dialogs", chosen by the user 2026-10-27): the marking follows the open state of the applied batch, so on an attached popup it is applied and released before the paint of the batch that opens or closes it, on both modes (`tests/browser/focus-manager.spec.ts` "port: the marking lands in the batch that opens an attached popup and is released in the batch that closes it"; with `Disabled` sent from the commit path, before the batch, design point 1 (d), chosen by the user 2026-10-27; measured: sent only from `OnAfterRender`, the marking lands a round trip late on Server). The counters are page-global and sanctioned (`js/CONTEXT.md`). The untrapped-combobox clause is open to M5.
  Port note (2026-10-27, M4 track 4, [PR 128](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/128) review): "uses aria-hidden instead of inert on outside nodes if opened with hover and modal=true" runs at L3 with the `useHover` shim restated as a hover open carried by the relay (`openInteractionType` `mouse`, reason `trigger-hover`).
- **FLT-ARIA-02** — MUST. With `modal === false` (and not the untrapped combobox case) no `aria-hidden` MUST be written anywhere; only the inert marker (FLT-DOM-16) is applied, and the native `inert` attribute is never used.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:652-658`. Test: [L3] `FloatingFocusManager.test.tsx` "false - does not apply inert to outside nodes", "false - comboboxes do not hide all other nodes".
- **FLT-ARIA-03** — MUST. The inside sets are computed when the manager becomes open/enabled: the *marker inside set* is `[floatingElement, ...portalNodesNestedInThisPortal]`; the *control (aria-hidden) inside set* is that plus the manager's own before/after inside guards, the enclosing portal's before/after outside guards, every element returned by `getInsideElements()`, the DOM anchor of the nearest ancestor tree node whose anchor is a typeable combobox, the resolved `previousFocusableElement` and `nextFocusableElement`, and (untrapped combobox case only) the DOM anchor itself. Elements whose parent chain leaves the body through a shadow root are replaced by their shadow host (so marking never descends into another shadow tree).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:626-651`; `floating-ui-react/utils/markOthers.ts:20-43`. Test: [L3] `FloatingFocusManager.test.tsx` "true - keeps supplied inside elements outside the floating node exposed to assistive tech"; `markOthers.test.ts` "uses shadow root host as avoid element when parent chain includes anchor".
  Port note (2026-10-27, M4 track 4, [PR 128](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/128) review; ADR 04 Amendments 2026-10-27): the inside set's elements cross as options: `InsideElements` (reference indices) or `InsideElementIds`, `TriggerFocusGuards` for Popover's trigger guards by position, and `PreviousFocusableElement`/`NextFocusableElement` as reference indices for Menu (M5), each with an L3 title.
- **FLT-ARIA-04** — MUST. When the portal renders its outside guards (FLT-DOM-14) it MUST also render, between the before-outside guard and the portaled subtree, `<span role="<portalOwnerRole>" aria-owns="<portal node id>" style="clip-path: inset(50%); position: fixed; top: 0; left: 0">`, so assistive technology reads the portaled popup at the trigger's position in the tree.
  Source: `floating-ui-react/components/FloatingPortal.tsx:268`; `internals/constants.ts:36-41`. Test: [L2] `FloatingFocusManager.test.tsx` "renders the aria-owns owner without changing regular reference semantics", "supports setting the aria-owns owner role explicitly"; `FloatingPortal.test.tsx` "uses the rendered portal ID for the aria-owns relationship".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): green at L2 (`PopupPortalTests`): the two FloatingFocusManager titles and the FloatingPortal title, the owner role being the portal's internal option (ADR 12 Amendments 2026-10-27 item 4).
- **FLT-ARIA-05** — MUST. Focus guards carry `aria-hidden="true"` (FLT-DOM-13) so they are invisible to assistive technology unless the VoiceOver/WebKit `role="button"` substitution applies.
  Source: `utils/FocusGuard.tsx:33-40`. Test: none.
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): port titles at L2 (the guards carry `aria-hidden="true"`) and L3 (the VoiceOver swap removes it).

### 4.6 KEY — keyboard interaction

Keys handled by this slice. Any key not listed is not intercepted by these mechanisms (list navigation keys are CMP's).

| Key | Where | Effect |
|---|---|---|
| <kbd>Escape</kbd> | document `keydown` (bubble phase) while open; also the anchor's and floating element's `keydown` handlers from the dismiss prop bags | Close with reason `escape-key` (FLT-KEY-01..03). |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | document `keydown` (bubble) while a modal focus manager is enabled | Prevented (`preventDefault` + `stopPropagation`) only when focus is inside the floating focus element and it has no tabbable content and it is not an untrapped typeable combobox (FLT-KEY-04). Otherwise not intercepted: native focus movement reaches a focus guard, which redirects (FLT-KEY-05..07). |
| any key | anchor `keydown` from `useClick` | Clears the remembered pointer type so the following `click` is treated as a keyboard click (FLT-KEY-08). |
| any key | window `keydown` (capture), Mac Safari only, from `useFocus` | Records keyboard modality (FLT-A11Y-06). |
| any key | document `keydown` (capture) from the focus manager | Records `'keyboard'` as the last interaction type used for close-type classification (FLT-FOC-10). |

- **FLT-KEY-01** — MUST. With `escapeKey` enabled (default `true`) and the popup open, a `keydown` whose `key` is `Escape` — observed on the owner document, or delivered to the anchor/floating `onKeyDown` handlers — MUST close the popup with reason `escape-key`; after closing, `preventDefault()` MUST be called on the event unless the change was canceled through the details (`isCanceled`), and `stopPropagation()` MUST be called unless `bubbles.escapeKey` is true or propagation was explicitly allowed through the details (`isPropagationAllowed`). Server: see ADR 01 relaxation table.
  Source: `floating-ui-react/hooks/useDismiss.ts:201-232, 721, 775-782`. Test: [L4] `useDismiss.test.tsx` "dismisses with escape key", "calls preventDefault on escape key dismiss", "does not call preventDefault on escape key if close is canceled".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the module decides "innermost" (FLT-KEY-02) and only then asks the decision point `escape` on the session's twin, fallback `prevent-and-stop`. The C# chain runs `OnOpenChange` with `EventReason.EscapeKey` and answers `PreventAndStop` (accepted), `Prevent` (accepted with `AllowPropagation()`; the verdict `Prevent`), `Stop` (canceled) or `Run` (canceled, propagation allowed); `bubbles.escapeKey` drops the stop. On Server the fallback applies to the innermost popup only and the module reports the close. One close request per keydown: a symbol on the keydown names the sessions that asked `escape` for it, and a session that requested a close is disarmed until the relay brings the next open or C# answers the request (the session's `Answered` counter, design point 10, settled by the orchestrator 2026-10-27), so a canceled close re-arms it on Server too (§10 Q11). L3: the verdict port titles of `interactions.test.ts`, both modes. L4 (2026-10-28, after PR 126): the three titles in `interactions.spec.ts` on track 1's popup host (`popup-conformance`, whose root composes the session on the popup store's token), both modes, with two port titles (a canceled close re-arms the session; `AllowPropagation()` is the verdict `Prevent` on WebAssembly). Server: see ADR 01.
- **FLT-KEY-02** — MUST. When `bubbles.escapeKey` is false (the default) and any *open* descendant tree node exists whose own `__escapeKeyBubbles` flag is false, the Escape press MUST be ignored by this popup (the innermost popup handles it and stops propagation; a later Escape closes the parent).
  Source: `floating-ui-react/hooks/useDismiss.ts:169-178, 216-218`. Test: [L3] `useDismiss.test.tsx` "prop: bubbles.escapeKey › when false", "mixed", "when true"; "without FloatingTree"; "prop: capture.escapeKey › when false" (nested dialogs: first Escape closes only the inner one).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): "innermost" is the blocking-child test over the session forest; every title passes without the open order, because the inner popup's trigger or floating listener stops the key first, as React's propagation does (a finding of the track, reported to the orchestrator 2026-10-27). On a document-level Escape with two nested Popovers the forest blocks the outer one, where Base UI's separate trees close both.
- **FLT-KEY-03** — MUST. Escape MUST be ignored while an IME composition is in progress: either the event reports `isComposing`, or a `compositionstart` was seen on the document and the composition-settled timer has not fired. The timer starts on `compositionend` with 5 ms on WebKit engines and 0 ms elsewhere; closing the popup resets the flag.
  Source: `floating-ui-react/hooks/useDismiss.ts:212-214, 298-305, 316-335`. Test: [L3] `useDismiss.test.tsx` "does not dismiss with escape key if IME is active", "does not dismiss with escape key while a composition spans a reopen", "does not dismiss with the escape key that ends a composition spanning a reopen", "dismisses with escape key after closing during IME composition".
- **FLT-KEY-04** — MUST. With a modal, enabled focus manager, a `Tab` keydown on the document MUST be prevented and stopped when the current active element is inside the floating focus element, the floating focus element has no tabbable descendants, and the untrapped-typeable-combobox case does not apply; focus therefore stays on the floating element.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:342-363`. Test: [L3] `FloatingFocusManager.test.tsx` "fallback to floating element when it has no tabbable content".
- **FLT-KEY-05** — MUST. In modal mode, focusing the before-inside guard (Shift+Tab from the first tabbable) MUST move focus on the next animation frame to the last tabbable element of the floating focus element, and focusing the after-inside guard (Tab from the last) MUST move focus on the next frame to the first tabbable element — so Tab wraps within the popup.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:1013-1017, 1035-1038`; `floating-ui-react/utils/enqueueFocus.ts:12-37`. Test: [L3] `FloatingFocusManager.test.tsx` "prop: modal › when true", "trapped combobox prevents focus moving outside floating element".
- **FLT-KEY-06** — MUST. In non-modal mode inside a portal, focusing the before-inside guard MUST clear the return-focus suppression and then: if the focus came from outside the portal node (the `relatedTarget` is not inside it), focus the next tabbable element after the DOM anchor in document order (falling back to the anchor); otherwise focus `previousFocusableElement` if provided, else the portal's before-outside guard. Focusing the after-inside guard MUST set return-focus suppression when `closeOnFocusOut` is true and then: from outside the portal → focus the previous tabbable before the anchor (fallback: the anchor); otherwise focus `nextFocusableElement` if provided, else the portal's after-outside guard.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:1017-1027, 1038-1051`; `floating-ui-react/utils/tabbable.ts:198-224`. Test: [L3] `FloatingFocusManager.test.tsx` "non-modal + FloatingPortal › shift+tab", "standard tabbing back and forth of a non-modal floating element".
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): green at L3 (the cited titles, with the `portal` module attached as C# renders it) and at L4 ("port: Shift+Tab from the popup reaches the reference through the outside guards and keeps it open; Tab returns into it", both modes). The inside guards listen on `focusin` (React's `onFocus`), so the portal's capture listeners on its node run first (FLT-FOC-17), and fall back to the portal's outside guards, read from the state the portal publishes on its node.
- **FLT-KEY-07** — MUST. Focusing the portal's before-outside guard MUST focus the before-inside guard when focus arrived from outside the portal node, otherwise the previous tabbable element before the DOM anchor. Focusing the after-outside guard MUST focus the after-inside guard when focus arrived from outside the portal, otherwise focus the next tabbable element after the DOM anchor and, when the published focus-manager state has `closeOnFocusOut`, close the popup with reason `focus-out`.
  Source: `floating-ui-react/components/FloatingPortal.tsx:256-265, 273-291`. Test: [L3] `FloatingFocusManager.test.tsx` "non-modal + FloatingPortal › focuses inside element, tabbing out focuses last document element"; "iframe focus navigation › tabs from the popover to the next element in the iframe", "shift+tab from the popover to the previous element in the iframe" (jsdom only; portal container inside an iframe document, guards resolve tabbables in that document).
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); design point 5, settled by the orchestrator 2026-10-27): the `portal` module routes its outside guards from the focus manager's publication through one `focusin` listener on the wrapper's parent, where C# renders them. Green at L3: the cited titles (the "iframe focus navigation" pair moves the rendered app into the iframe, since the port renders in the top document and only a container inside a frame can receive the portal node) and `portal.test.ts`'s port titles; at L4, "port: the popup moves into the portal node at the end of the body with its inside guards, and Tab leaves through the outside guards to the last element and asks for the close", both modes. The close is the manager's focus-out request (FLT-FOC-11).
- **FLT-KEY-08** — MUST. A `keydown` on the anchor (from the `useClick` prop bag) MUST reset the remembered pointer type to "none", so that a keyboard-generated `click` is handled by the click path with no pointer type (not ignored by `ignoreMouse`, not delayed by `touchOpenDelay`).
  Source: `floating-ui-react/hooks/useClick.ts:220-222`. Test: none.
- **FLT-KEY-09** — MUST. Keyboard close classification: when the close event is a `KeyboardEvent` the close type is `'keyboard'`; return focus after a keyboard close MUST request `focusVisible: true` (FLT-FOC-07).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:50-54, 929-933`. Test: [L3] `FloatingFocusManager.test.tsx` "passes focusVisible when returning focus after keyboard close", "omits focusVisible when returning focus after pointer close".

### 4.7 PTR — pointer interaction

#### Press-to-toggle (`useClick`)

The hook returns handlers for the anchor only: `onPointerDown`, `onMouseDown`, `onClick`, `onKeyDown`; nothing when `enabled` is false.

- **FLT-PTR-01** — MUST. On anchor `pointerdown` the pointer type MUST be remembered as `'virtual'` when the event's pointer type is strictly mouse-like (`mouse`/`pen`) **and** the event is a virtual pointer event (FLT-ALG-26); otherwise as the event's `pointerType` (`'mouse' | 'pen' | 'touch'`).
  Source: `floating-ui-react/hooks/useClick.ts:127-137`. Test: [L3] `FloatingFocusManager.test.tsx` "focuses without a frame delay when a screen reader press opens it".
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the cited title runs at L3 (`focus-manager.test.ts` "with the interactions session") and is green, but observes only FLT-A11Y-04's and FOC-06's clause: as Base UI's, it sends no `pointerdown`, so no press is typed `'virtual'`, and it waits the click region's frame; and jsdom's `isVirtualPointerEvent` is `false` by design. No title observes this row's clause, so it stays open to M4 track 5 (Popover's trigger at L4, where a virtual press is a 0×0 `pointerdown` in a real browser; ADR 13 §1).
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): open to M5. Popover's trigger uses the `click` event and no `ignoreMouse`, so the `'virtual'` typing has no effect it can observe; Menu's `mousedown` trigger does (item 17 of §10).
- **FLT-PTR-02** — MUST. On anchor `mousedown`: the handler MUST return without effect when `button !== 0`, when `event` is `'click'` (the default), or when the remembered pointer type is strictly mouse-like and `ignoreMouse` is true. Otherwise it MUST compute the next open value per FLT-ALG-16 treating only `click`/`mousedown` open events as click-like, and then: if the event target is a typeable element or the remembered pointer type is `'virtual'`, apply it synchronously (trigger = the typeable target, or the handler's element for a virtual press); else apply it on the next animation frame (so that focus lands before the popup opens and `:focus-visible` is not shown for pointer input).
  Source: `floating-ui-react/hooks/useClick.ts:138-186`. Test: [L3] `useClick.test.tsx` "opens from the mousedown event path", "closes from the mousedown event path", "delays touch opening from the deferred mousedown event path".
- **FLT-PTR-03** — MUST. On anchor `click`: with `event: 'mousedown-only'` the handler MUST do nothing; with `event: 'mousedown'` and a remembered pointer type, the click MUST be ignored and the pointer type cleared (the mousedown path already handled the press; a keyboard click has no pointer type and goes through); when the remembered pointer type is strictly mouse-like and `ignoreMouse` is true it MUST do nothing. Otherwise it MUST compute the next open value per FLT-ALG-16 treating `click`, `mousedown`, `keydown` and `keyup` open events as click-like and apply it synchronously with the handler's element as trigger.
  Source: `floating-ui-react/hooks/useClick.ts:187-219`. Test: [L3] `useClick.test.tsx` "opens and closes on repeated clicks", "ignores the click event after mousedown when event is mousedown-only", "ignores mouse input when ignoreMouse is true".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the trigger computes the next open value (FLT-ALG-16) and asks the decision point `press` on its own twin, fallback `run`. On WebAssembly it first hands the press over (`OnPressing { open, reason, triggerId, interactionType }`) and C# runs the consumer's declined `onclick` and the request inside the dispatch; on Server it reports `OnPress` with its snapshot and interaction type (`button.ts`'s `classifyClick`, iOS's empty type as touch) and C# runs both a round trip later. A consumer's `PreventPartHandler()` vetoes the press on both modes (L4: `interactions.spec.ts`). The mousedown, `toggle: false`, `ignoreMouse`, `touchOpenDelay` and `input-press` branches have no M4 consumer and are proven at L3 (ADR 12 Amendments 2026-10-27 item 4); a touch-deferred or frame-deferred press asks `press` when it fires. Server: see ADR 01.
- **FLT-PTR-04** — MUST. Applying the next open value with reason `reason` (default `trigger-press`): when `nextOpen` is true, the remembered pointer type is `'touch'` and `touchOpenDelay > 0`, the open MUST be deferred by `touchOpenDelay` ms (restarting any pending deferral); closing, and opening for other pointer types, MUST happen immediately.
  Source: `floating-ui-react/hooks/useClick.ts:77-92`. Test: [L3] `useClick.test.tsx` "delays touch opening when touchOpenDelay is set", "does not delay touch closing", "uses the configured reason".
- **FLT-PTR-05** — MUST. `toggle: false` MUST keep an open popup open on repeated presses (FLT-ALG-16 step 3).
  Source: `floating-ui-react/hooks/useClick.ts:112-115`. Test: [L3] `useClick.test.tsx` "keeps open on repeated clicks when toggle is false".
- **FLT-PTR-06** — MUST. With `stickIfOpen` true (default), a press on the anchor while the popup is open from a non-click-like event (hover or focus) MUST keep it open (and re-record the open event as the click, FLT-STA-03); with `stickIfOpen` false the press MUST close it.
  Source: `floating-ui-react/hooks/useClick.ts:117-123`. Test: [L3] `useClick.test.tsx` "stickIfOpen true keeps a hover-opened popup open on first click", "stickIfOpen false closes a hover-opened popup on first click".

#### Cursor anchoring (`useClientPoint`)

- **FLT-PTR-07** — MUST. The hook's anchor/trigger prop bag MUST contain `onPointerDown`/`onPointerEnter` (record the pointer type) and `onMouseMove`/`onMouseEnter`: while closed, each such event MUST replace the position anchor with a virtual anchor at the event's `clientX`/`clientY` (FLT-ALG-25) built around the handler's element; while open, it MUST do so only when no window `mousemove` listener is currently attached (which re-attaches it).
  Source: `floating-ui-react/hooks/useClientPoint.ts:124-160, 234-250`. Test: [L3] `useClientPoint.test.tsx` "updates position from trigger props", "uses trigger element when dom reference is missing", "reattaches window listener after cursor returns from floating element to reference".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the virtual anchor is the session's `positionReference`, which `positioning` reads through `subscribePositionReference`; the titles assert the session's selected reference (FLT-STR-02) for `useFloating`'s `elements.reference`. Tooltip's `trackCursorAxis` arrives as `ClientPoint.Axis`.
- **FLT-PTR-08** — MUST. While enabled and the "open check" holds — the floating element exists when the pointer type is mouse-like (so the popup keeps following the cursor while animating out), or `open` is true otherwise — a window `mousemove` listener MUST update the virtual anchor to the cursor position for moves whose target is outside the floating element, and MUST detach itself on the first move whose target is inside the floating element. The listener MUST be attached only when there is no recorded open event or the recorded open event is mouse-based (has `clientX`); otherwise the position anchor MUST be reset to the DOM anchor.
  Source: `floating-ui-react/hooks/useClientPoint.ts:162-212`. Test: [L3] `useClientPoint.test.tsx` "renders at mouse event coords", "removes window listener when cursor lands on floating element", "cleans up window listener when closing or disabling".
- **FLT-PTR-09** — MUST. A virtual anchor MUST NOT be installed when the recorded open event is not mouse-based (e.g. focus); the DOM anchor's own rect is used instead.
  Source: `floating-ui-react/hooks/useClientPoint.ts:130-135, 195-199`. Test: [L3] `useClientPoint.test.tsx` "restores the DOM reference when opened by a non-mouse event".
- **FLT-PTR-10** — MUST. Disabling the hook MUST reset the position anchor to the DOM anchor; unmounting it MUST set the position anchor to `null`. While disabled with the popup open, further cursor updates MUST be ignored until the floating element unmounts.
  Source: `floating-ui-react/hooks/useClientPoint.ts:168-171, 214-232`. Test: [L3] `useClientPoint.test.tsx` "clears virtual references on unmount without retaining a stale DOM reference", "cleans up window listener when closing or disabling".

#### Hover — trigger side (`useHoverReferenceInteraction`)

The hook attaches native `mouseenter`/`mouseleave` listeners (and, with `move: true`, a one-shot `mousemove`) to the trigger element — `triggerElementRef` if given, else the DOM anchor when `isActiveTrigger` — and returns a prop bag `{ onPointerDown, onPointerEnter, onMouseMove }` for the trigger; `undefined` when disabled. All timers and flags live in the per-root shared hover state (`HoverInteraction`: `pointerType`, `interactedInside`, `handler`, `blockMouseMove`, pointer-events bookkeeping, `restTimeoutPending`, `openChangeTimeout`, `restTimeout`, `handleCloseOptions`), created once per root store and shared with the popup-side hook.

- **FLT-PTR-11** — MUST. `onPointerDown`/`onPointerEnter` MUST record the event's pointer type in the shared hover state; with `mouseOnly` true, every hover path MUST do nothing when the recorded type is not mouse-like (non-strict: `undefined`/`''` count as mouse).
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:220-222, 419-421, 434-436`. Test: none.
- **FLT-PTR-12** — MUST. On trigger `mouseenter` (and the first `mousemove` when `move` is true), the hook MUST: cancel the pending open/close timer; clear `blockMouseMove`; resolve the trigger node (the handler's element, or — delegated mode — the registered trigger containing the event target, or the active anchor when the handler's element contains it); then decide per FLT-ALG-24: open immediately with reason `trigger-hover` and the trigger node as `trigger` when hovering an *inactive* trigger while open (or during a hover-initiated closing transition), or when re-entering the *same* trigger during a hover-initiated closing transition; else, if only a rest delay is configured (`restMs > 0` and no open delay), defer to `mousemove`; else, with an open delay, start the open timer for that many ms (opening only if still appropriate); else open immediately when closed (or over an inactive trigger). Every open attempt first consults `shouldOpen()` and is vetoed when it returns `false`.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:112-114, 216-300`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "opens on mouseenter", "prop: delay › symmetric number", "open", "restMs + nullish open delay should respect restMs", "reopens immediately for same trigger in delegated wrapper mode during close transition", "reopens immediately when close transition state is provided externally", "does not treat child target as inactive when handlers are on a wrapper".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): every hover, rest, close, intent and group timer runs in the module and reports `OnRequest` (reason `trigger-hover`) when it fires; milliseconds arrive resolved by C# (ADR 08 Amendments 2026-10-27); `isClosing` is the session's `Ending`. A trigger `mouseleave` while the popup is closed cancels the pending open and requests no close (the untested quirk, not replicated, ADR 04 Amendments 2026-10-27; a hover open still awaiting its commit counts as open, so a quick leave still closes it). ADR 01's row "EVD-EVT-18, PSH-ANI-03".
- **FLT-PTR-13** — MUST. "Closing transition" for FLT-PTR-12 is `isClosing()` when the option is given, else `transitionStatus === 'ending'` in the root store; "hover-initiated" means the most recent `openchange(false)` carried reason `trigger-hover`.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:170-181, 259-262`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "reopens immediately when close transition state is provided externally".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.
- **FLT-PTR-14** — MUST. On trigger `mouseleave`: if the popup is click-like open (FLT-STA-06) the hook MUST only clear the safe-polygon pointer-events mutation and return; otherwise it MUST detach any document `mousemove` handler, cancel the rest timer, and return without closing when the `relatedTarget` is inside an *enabled* registered trigger (moving between triggers). Then, if a `handleClose` strategy is configured and a positioning context is available (the session's `floatingContext`, or `getHandleCloseContext()`), it MUST cancel the open timer when closed, create the strategy's `mousemove` handler with `{ ...context, tree, x: clientX, y: clientY, onClose }`, attach it to the document's `mousemove`, and invoke it once immediately with the leave event; `onClose` clears the pointer-events mutation, detaches the handler and closes with delay (FLT-PTR-15) only if still enabled, not click-like open, and the trigger is still the active anchor. Without a strategy it MUST close with delay — for `touch` pointer types only when the `relatedTarget` is not inside the floating element.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:302-360`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "closes on mouseleave", "closes when the pointer moves onto the floating element without safePolygon"; `useHoverFloatingInteraction.test.tsx` "keeps the popup open when the floating element is entered before closeDelay elapses".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.
- **FLT-PTR-15** — MUST. "Close with delay": with a resolved close delay > 0, start the shared open/close timer for that many ms and then close with reason `trigger-hover` and emit `floating.closed` on the tree; with no delay, cancel the timer and do both immediately.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-206`; `useHoverFloatingInteraction.ts:165-178`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "prop: delay › close", "reports the hover reason".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.
- **FLT-PTR-16** — MUST. On trigger `mousemove` (prop bag): after the `mouseOnly` check, when the popup is open, the move is over an inactive trigger, and the configured `handleClose` has `blockPointerEvents`, the hook MUST apply the pointer-events mutation with scope `getScope()` or the document body (FLT-DOM-19). Then, unless (open and over the active trigger) or `restMs` resolves to `0`: if not over an inactive trigger, a rest timer is pending and the move's squared displacement (`movementX² + movementY²`) is `< 2`, the move MUST be ignored (tiny jitter does not restart the rest timer); otherwise the rest timer MUST be cancelled and the rest action scheduled: synchronously (flushed) for `touch`, immediately when over an inactive trigger while open, else after `restMs` ms with `restTimeoutPending` set. The rest action clears `restTimeoutPending`, aborts if the popup became click-like open meanwhile, and opens with reason `trigger-hover` (trigger = handler's element) when `blockMouseMove` is clear, the popup is closed or over an inactive trigger, and `shouldOpen()` allows.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:426-497`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "restMs", "restMs does not reset timer for minor mouse movement".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says. `blockPointerEvents` keeps the mutation's owner in a symbol on the scope element, not a module-level map; no M4 consumer (built and proven at L3, M5's Menu).
- **FLT-PTR-17** — MUST. On `openchange(false)` the trigger-side hook MUST detach the document `mousemove` handler, cancel the open/close and rest timers, set `blockMouseMove`, clear `restTimeoutPending`, and remember whether the close was hover-initiated; on `openchange(true)` it MUST clear that memory.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:165-187`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "prop: delay › open with close 0".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the listener runs when the relay brings the commit, inside the session handle's `update` (on Server a round trip after the commit).
- **FLT-PTR-18** — MUST. With `guardStaleOpen` true, a native `mouseout` on the trigger whose `relatedTarget` is outside the trigger's subtree MUST cancel the open/close timer and the rest timer (backup for a dropped `mouseleave`).
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:362-374`. Test: none. Docs: `CHANGELOG.md` entry for #5152 (submenu stuck open).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says. `GuardStaleOpen` has no M4 consumer (built and proven at L3).
- **FLT-PTR-19** — MUST. `handleClose.__options` (the options the `safePolygon()` factory was called with, always including `blockPointerEvents`) MUST be published to the shared hover state on every render of the active trigger part so the popup-side hook sees the latest value.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:156-159`; `floating-ui-react/safePolygon.ts:444-448`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "updates the handleClose options during render".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.

#### Hover — popup side (`useHoverFloatingInteraction`)

- **FLT-PTR-20** — MUST. Whenever the popup closes, the popup-side hook MUST reset the shared `pointerType` to undefined, clear `restTimeoutPending` and `interactedInside`, and clear the pointer-events mutation (post-commit, pre-paint).
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:81-88`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.
- **FLT-PTR-21** — MUST. While enabled, open, the configured strategy has `blockPointerEvents`, the open event is a hover open (FLT-STA-05) and both DOM anchor and floating element exist, the hook MUST (post-commit, pre-paint) reset the *parent* tree node's floating element's inline `pointer-events` to `''` and apply the pointer-events mutation with scope = `getScope()` ?? the previously cached scope (unless it is this floating element) ?? the parent node's floating element (unless it is this floating element) ?? the closest ancestor of the anchor matching `[data-rootownerid]` ?? the document body; it MUST clear the mutation when any of these conditions stop holding.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:94-154`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says. The floating's layout effects run after the session applied a commit or its anchor attached.
- **FLT-PTR-22** — MUST. A `pointerdown` inside the floating element (capture) MUST set `interactedInside` to "the target is an interactive element (`button`, `a[href]`, `[role=button]`, `select`, `[tabindex]` other than -1, typeable) **and** is inside an element with `aria-haspopup`"; otherwise clear it.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:180-188`; `floating-ui-react/utils/element.ts:55-62`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.
- **FLT-PTR-23** — MUST. Floating `mouseenter` MUST cancel the shared open/close timer and the child-closed timer, unsubscribe from `floating.closed`, and clear the pointer-events mutation.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:190-195`. Test: [L3] `useHoverFloatingInteraction.test.tsx` "keeps the popup open when the floating element is entered before closeDelay elapses".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.
- **FLT-PTR-24** — MUST. Floating `mouseleave`: if this node's *parent* has open child nodes, subscribe to `floating.closed` and return; return without closing when `relatedTarget` is inside an enabled trigger, or when it is inside the floating element of any descendant node (open or closed); if a safe-polygon handler is active, delegate the event to it and return; otherwise clear the pointer-events mutation and, if the popup is hover-open and not click-like open, close with delay using `closeDelay` (FLT-PTR-15).
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:197-233`. Test: [L3] `useHoverFloatingInteraction.test.tsx` "closes after the close delay when the pointer leaves the floating element"; `FloatingFocusManager.test.tsx` "Navigation › closes when unhovering floating element even when focus is inside it".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the Popup-leave close delay is the Trigger's `closeDelay ?? 0` (ADR 04 Amendments 2026-10-18), sent as the floating handle's `Hover.CloseDelay`. Its second title is in `FloatingFocusManager.test.tsx` (track 4's): open until both tracks are on `main`.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the second title, "Navigation › closes when unhovering floating element even when focus is inside it", is green at L3 (`focus-manager.test.ts` "with the interactions session"), on arrival; restated without the safe polygon, whose pointer geometry is the hover region's own titles.
- **FLT-PTR-25** — MUST. When a `floating.closed` tree event arrives while subscribed (FLT-PTR-24), and this node's parent no longer has open children, the hook MUST after 0 ms unsubscribe, close with reason `trigger-hover` and emit `floating.closed` again (letting a `mouseenter` that moved the cursor back into this popup cancel it first).
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:235-245`. Test: none.
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the timers and listeners run in the module, as FLT-PTR-12's note says.

#### Outside-press gesture details (the pointer half of dismissal; the decision logic is in §4.9)

- **FLT-PTR-26** — MUST. Sloppy mode, mouse/pen: an outside `pointerdown` (not `touch`) MUST close the popup immediately per FLT-ALG-17.
  Source: `floating-ui-react/hooks/useDismiss.ts:522-534, 586-614`. Test: [L3] `useDismiss.test.tsx` "dismisses with outside pointer press".
- **FLT-PTR-27** — MUST. Sloppy mode, touch: an outside `touchstart` MUST record the start time and coordinates and arm "dismiss on mousedown"; a subsequent `touchmove` farther than 5 px (Euclidean) MUST arm "dismiss on touchend", and farther than 10 px MUST close immediately and clear the record; `touchend` MUST close iff "dismiss on touchend" is armed and then clear the record; 1000 ms after `touchstart` both arms MUST be disarmed (a long press/scroll does not dismiss). A compatibility `mousedown` arriving while a touch record exists with "dismiss on mousedown" disarmed MUST be ignored.
  Source: `floating-ui-react/hooks/useDismiss.ts:536-563, 599-604, 664-716`. Test: [L3] `useDismiss.test.tsx` "does not dismiss a reopened popup when an old touch moves %s pixels and ends" (2 variants: 6, 12; its final assertion is a fresh 12 px `touchmove` closing immediately).
- **FLT-PTR-28** — MUST. Intentional mode: only outside `click` events close (FLT-ALG-17); a click MUST be *press-backed* — `detail === 0`, or a virtual click (FLT-ALG-26), or a primary-button `pointerdown` was observed while open in this session — otherwise it MUST be ignored. `pointercancel` MUST forget the observed press.
  Source: `floating-ui-react/hooks/useDismiss.ts:489-501, 591-596, 616-621`. Test: [L3] `useDismiss.test.tsx` "mouse click whose press started before open does not close", "keyboard-generated outside click without a prior press closes", "press-less outside click reporting a pointer type closes", "pointerdown-only press while open allows the outside click to close", "non-primary-button press does not count as an outside press", "cancelled press does not count as an outside press", "compatibility events whose pointerdown opened the floating element do not count as a new press", "press seen in a previous open session does not leak into a reopen", "press seen before a same-batch close and reopen does not leak into the new session", "press survives a redundant open dispatch while already open", "press survives listener re-attachment while open".
- **FLT-PTR-29** — MUST. Intentional mode drag handling: a primary-button press that starts inside the floating element (capture `pointerdown`/`mousedown` with target inside it) and ends (`pointerup`/`mouseup`) outside the floating tree MUST suppress exactly the next outside click, unless the `outsidePress` function rejects the release target. If the inside press start was default-prevented (e.g. a scrub gesture), the suppression MUST last only until the next macrotask (0 ms), including after `pointercancel`. A new outside press (non-click event outside own elements) MUST clear any pending suppression.
  Source: `floating-ui-react/hooks/useDismiss.ts:239-275, 337-349, 387-397, 503-508, 616-662`. Test: [L3] `useDismiss.test.tsx` "dragging outside the floating element does not close", "dragging inside the floating element does not close", "dragging outside the floating element then clicking outside closes", "dragging outside the floating element then clicking outside closes with mouse clicks", "inside click then programmatic outside click closes", "inside click after drag does not cause immediate close on first outside click", "drag ending on outsidePress-ignored target does not consume next outside click", "press start prevented inside does not require double outside click", "press start prevented inside suppresses only immediate outside click", "pointercancel after prevented press start suppresses immediate outside click".
- **FLT-PTR-30** — MUST. Reference press: when `referencePress()` returns true, a `pointerdown` or `click` on the anchor (prop bag) MUST close the popup with reason `trigger-press`.
  Source: `floating-ui-react/hooks/useDismiss.ts:187-199, 775-782`. Test: [L3] `useDismiss.test.tsx` "dismisses with reference press", "dismisses with native click", "does not dismiss with reference pointer down".
  Port note 2026-10-28 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); design point 13.9, settled by the orchestrator 2026-10-28; ADR 04 Amendments 2026-10-28, "one close request in flight per handle"): a press reports its close on `pointerdown` and on `click`, as Base UI requests both, but a handle sends no further close request while one is in flight: from its emission until the first of a session update whose relay revision or answered counter moved and the request's own invocation settling (a trigger's or the floating element's twin routes its request to the store directly, and a canceled one moves neither counter). On WebAssembly the first request is answered inside the call, so the `click`'s close is sent as Base UI sends it; on Server it is not sent, where it would reach the store a round trip after the `pointerdown`'s and close a hover begun in between (L3 "port: a handle sends no second close while its close is in flight, and sends again once C# answers it").
  Port note 2026-10-29 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); gap 14 of M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125), settled by the orchestrator 2026-10-29; ADR 04 Amendments 2026-10-29, "the in-flight close rule widens to the session"): the rule above holds per session, not per handle: while a close request from any handle of the session is in flight, a close from another handle of the same session is not sent, with the same window. A module that sends its close through its own twin (the focus manager's focus-out, FLT-FOC-11) claims the session the same way (`claimSessionClose`), so on Server a press outside a non-modal popup with focus inside requests one close, the focus-out or the outside press, where it requested both. A dropped request leaves the dismissal armed (L3 "port: a close request from another handle of the session is dropped while one is in flight"; "port: a press outside a non-modal popup requests one close of the session, the focus-out or the outside press").

### 4.8 FOC — focus management

#### Focus-to-open (`useFocus`)

The hook returns `{ reference, trigger }` (same object) with `onMouseLeave`, `onFocus`, `onBlur`; nothing when disabled.

- **FLT-FOC-01** — MUST. On anchor `focus`: if focus is currently *blocked* for this element (FLT-FOC-02/03) the event MUST be ignored (a block for a different element is cleared instead). The event MUST also be ignored when the focused target is an element that does not match `:focus-visible` (always treated as matching in jsdom or when `matches` throws; FLT-A11Y-08), except on Mac Safari when there is no `relatedTarget`, where it is ignored iff the last modality was pointer and the target is not typeable (FLT-A11Y-06). Otherwise the popup MUST open with reason `trigger-focus` and the handler's element as trigger: immediately when `delay` is falsy or when already open and focus moved from another enabled trigger; else after `delay` ms, unless focus became blocked meanwhile.
  Source: `floating-ui-react/hooks/useFocus.ts:126-184`; `floating-ui-react/utils/element.ts:71-82`. Test: [L3] `useDismiss.test.tsx` "prop: bubbles.escapeKey › without FloatingTree" (focus opens a tooltip).
- **FLT-FOC-02** — MUST. A window `blur` observed while the popup is closed and the DOM anchor is the active element MUST block the next `focus` on that anchor (so returning to the tab does not reopen); `mouseleave` or `blur` on the anchor MUST clear the block.
  Source: `floating-ui-react/hooks/useFocus.ts:56-93, 117-125, 186`. Test: [L3] `useFocus.test.tsx` "does not reopen when focus is restored after leaving the tab".
- **FLT-FOC-03** — MUST. An `openchange` with reason `trigger-press` or `escape-key` MUST block the next `focus` on the current DOM anchor (so closing with Escape or a press does not reopen through the focus that follows).
  Source: `floating-ui-react/hooks/useFocus.ts:95-114`. Test: [L3] `FloatingFocusManager.test.tsx` "Navigation › does not re-open after closing via escape key".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the listener runs when the relay brings the commit, inside the session handle's `update` (on Server a round trip after the commit). Its title is in `FloatingFocusManager.test.tsx` (track 4's): open until both tracks are on `main`.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the title, "Navigation › does not re-open after closing via escape key", is green at L3 (`focus-manager.test.ts` "with the interactions session"), on arrival: the manager returns focus to the reference and the focus region's block keeps it closed.
- **FLT-FOC-04** — MUST. On anchor `blur`, after 0 ms, the popup MUST close with reason `trigger-focus` unless: there is no `relatedTarget` and the anchor is still the active element (focus left the page); the new active element is inside the floating element or the anchor; the `relatedTarget` is a focus guard with `data-type="outside"`; or the next focused element (`relatedTarget`, else the active element) is inside an enabled registered trigger.
  Source: `floating-ui-react/hooks/useFocus.ts:185-233`. Test: none.

#### Focus manager — initial focus

- **FLT-FOC-05** — MUST. The *floating focus element* is the floating element itself when it carries `data-base-ui-focusable`, else its first descendant carrying that attribute, else the floating element.
  Source: `floating-ui-react/utils/element.ts:84-97`; `floating-ui-react/utils/constants.ts:1`. Test: [L3] `FloatingFocusManager.test.tsx` "focus is placed on element with floating props when floating element is a wrapper".
- **FLT-FOC-06** — MUST. When the manager becomes open and enabled with an HTML floating focus element, it MUST reset the recorded close type and last interaction type, capture the active element, and in a microtask: resolve `initialFocus` (FLT-API-05); do nothing for `false`/`undefined`; do nothing if the captured active element is already inside the floating focus element; otherwise pick the element to focus — the resolved element, falling back to the first tabbable descendant of the floating focus element, falling back to the floating focus element — and focus it on the next animation frame (synchronously when the recorded open event is a `mousedown` that is a virtual click, FLT-A11Y-04) with `preventScroll` iff the target is the floating focus element itself. The queued focus MUST be abandoned if the popup has closed meanwhile, or if focus had not been inside before and has since moved to another element inside the floating focus element (e.g. an `autoFocus` control).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:679-771`. Test: [L3] `FloatingFocusManager.test.tsx` "default behavior focuses first tabbable element", "default behavior focuses the checked radio in a named group", "ref", "respects autoFocus", "focuses without a frame delay when a screen reader press opens it", "keeps the frame delay for a real mouse press"; "prop: disabled › true -> false".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the manager becomes open from the applied batch (a `MutationObserver` on the popup element's `OpenSelector`, `[data-open]` by default), and initial focus reads the options current at that point; the options are sent from the commit path, before the batch that applies them (design point 1 (d), chosen by the user 2026-10-27; ADR 01 Amendments 2026-10-27). A composite group whose module attaches after initial focus chose its default gets one re-pick, while focus is still where initial focus put it and nothing has been interacted with (the attach-order rule; `tests/browser/focus-manager.spec.ts` "port: initial focus lands on the composite group's tab stop when js/composite.js attaches last", red before the rule). For a popup the opening render creates, the handle attaches from `OnAfterRender` with the popup already open (the handover at attach). The synchronous focus after a virtual `mousedown` (FLT-A11Y-04) reads the interactions session's recorded open event, so "focuses without a frame delay when a screen reader press opens it" is open until track 3 is on `main`; the other titles of this row are green at L3 on the module.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the synchronous focus after a virtual `mousedown` reads the session's recorded open event; "focuses without a frame delay when a screen reader press opens it" is green at L3 (`focus-manager.test.ts` "with the interactions session", with the click region opening on its frame as Base UI's does).

#### Focus manager — return focus on close

- **FLT-FOC-07** — MUST. When the manager stops being enabled, its floating focus element changes, or it unmounts, it MUST queue a microtask that returns focus: resolve the return element (FLT-ALG-21); take its first tabbable element (itself if tabbable, else its first tabbable descendant, else the element as-is); and call `focus({ preventScroll: true, focusVisible: <closeType === 'keyboard'> })` on it iff: the job was not cancelled by a re-arm in the same commit (FLT-EDGE-07); `returnFocus` is truthy; return focus is not suppressed (FLT-FOC-08); the target is an HTML element; and either return focus is *explicit* (FLT-FOC-21), or the target is already the active element, or the active element is the body, or focus is currently inside the floating tree (the floating element, a `getInsideElements()` element, or any descendant node's floating element). The suppression flag MUST be cleared afterwards in all cases.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:775-952`. Test: [L3] `FloatingFocusManager.test.tsx` "prop: returnFocus › when true", "when false", "ref", "always returns to the reference for nested elements", "return to the first focusable descendent of the reference, if the reference is not focusable", "returns focus to last connected element", "preserves tabbable context next to reference element if removed (modal)", "preserves tabbable context next to reference element if removed (non-modal)".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27): return focus is armed while the manager is enabled and released when it stops, as Base UI's effect cleanup is: `Disabled` becoming true in `update`, the floating focus element or the anchor (`DomReferenceId`) changing, the batch removing the popup element or an ancestor (each ancestor's child list observed while armed, pre-paint, before C# disposes the handle), or the handle's `dispose` (ADR 01 Amendments 2026-10-27, design point 1, chosen by the user). The release reads the close's resolved `ReturnFocus`, its `ExplicitReturnFocus` and the close type C# classified (`CloseInteractionType`, for `focusVisible`). Green at L3: "when true", "when false", "ref", "return to the first focusable descendent of the reference, if the reference is not focusable", "returns focus to last connected element". Green at L3 2026-10-28, after track 1 merged: "preserves tabbable context next to reference element if removed (modal)" and "(non-modal)", with the `portal` module attached. Open: "always returns to the reference for nested elements" (the floating tree, track 3).
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): "always returns to the reference for nested elements" is green at L3 (`focus-manager.test.ts` "with the interactions session"; two sessions, the inner one's portal inside the outer node), on arrival; return focus's "focus inside the floating tree" now includes the tree's descendant nodes, proven by the port title "port: closing a popup while focus is inside its nested popup returns focus to the reference" (red with the branch removed).
- **FLT-FOC-08** — MUST. Return-focus suppression is set when: the popup closes with reason `focus-out` and the details' trigger element is a focus guard; the popup closes with reason `trigger-hover` from a `mouseleave` event; the after-inside guard was focused in non-modal portal mode with `closeOnFocusOut` (FLT-KEY-06); or a non-modal focus-out close occurred (FLT-FOC-11). On an `outside-press` close it is *cleared* when the root is nested or the press was a virtual click/pointer event, and otherwise set iff the platform does not support `focus({ preventScroll })` (feature-detected by focusing a detached `<div>` with a getter on `preventScroll`).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:796-846`. Test: [L3] `FloatingFocusManager.test.tsx` "returns focus to reference on outside press when preventScroll is supported", "Navigation › returns focus to reference when floating element was opened by hover but is closed by esc key", "returns focus to reference when floating element was opened by hover but is closed by an explicit close button", "does not focus reference when hovering it", "focus does not return to reference when floating element is triggered by hover", "does not return focus to reference on outside press when preventScroll is not supported" (jsdom only).
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27, the relay): the internal `openchange` runs once per relay revision carried in `update`. The `trigger-hover` close from `mouseleave` and the `outside-press` `preventScroll` branch are green at L3; the "Navigation" titles' `useHover` shim becomes a hover open and close carried by the relay (restated, same titles). `nested`, the virtual-press verdict of an outside press and whether the trigger was a focus guard come from the interactions session (track 3): the outside-press virtual branch and the focus-guard trigger clause are open until it is on `main`.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the internal `openchange` is the session's, with `nested`, the real event and the trigger. The outside-press virtual branch is green at L3 (port title "port: where preventScroll is unsupported, an outside press returns focus only when a screen reader made it"); "focus does not return to reference when floating element is triggered by hover" also runs with the hover region's real leave. The focus-guard trigger clause stays open to M4 track 5: the session resolves a relayed trigger id among its registered triggers only, so the `popup` module's guard close names the guard through `interactions`' `nameRelayTrigger(session, element)` (ADR 04 Amendments 2026-10-28, track 5's design point 4, settled by the orchestrator; PSH-KEY-07).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): the focus-guard trigger clause is green: `interactions.test.ts` "port: a named element is the trigger element of the next relayed focus-out close without a trigger, and of that close only", `popup.test.ts` "port: on Server, focusing the pre-guard names it, focuses the tabbable before the trigger and reports the guard" (L3) and `PopoverFocusTests` "port: a focused trigger guard closes the popover with focus-out, its event and the guard as the trigger" (L2).
- **FLT-FOC-09** — MUST. A global, weakly held stack of previously focused elements (max 20, body excluded, disconnected entries pruned on each access) MUST record: the active element whenever a manager arms return focus; and, for modal managers with `closeOnFocusOut` true, the inside element that lost focus to nothing (`relatedTarget == null`, e.g. a backdrop press) — the recording runs inside the focus-out listener, which is not installed when `closeOnFocusOut` is false (e.g. Dialog/Drawer with `disablePointerDismissal`, every AlertDialog). Its most recent entry is the last-resort default return element (FLT-ALG-21) and the element that FLT-FOC-11 compares `relatedTarget` against.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:78-100, 424-427, 452-456, 794`; `dialog/popup/DialogPopup.tsx:102`. Test: [L3] `FloatingFocusManager.test.tsx` "returns focus to last connected element".
- **FLT-FOC-10** — MUST. The close interaction type MUST be classified from the close event when `openchange(false)` fires: `KeyboardEvent` → `'keyboard'`; `FocusEvent` → the last recorded pointer interaction type if any, else `'keyboard'`; an event with `pointerType` → that type, or `'keyboard'` for a virtual click, else the last interaction type, else `'mouse'`; a touch event → `'touch'`; other `MouseEvent` → the last interaction type, else `'keyboard'` when `detail === 0`, else `'mouse'`; anything else → `''`. The last interaction type is recorded from document `pointerdown` (the pointer type, or `'keyboard'` when empty) and `keydown` (`'keyboard'`) while open and enabled.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:50-77, 365-421, 796-799`. Test: [L3] `FloatingFocusManager.test.tsx` "resets close modality between keep-mounted open sessions", "preserves keyboard close modality when reopening before focus restoration".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27, "Focus targets", chosen by the user 2026-10-27): the classification is C#'s, from the close request's snapshot and the type this module records (FLT-FOC-10's recording half: document `pointerdown` and `keydown` while open and enabled, reported to the twin once per change as `OnInteractionTypeChange`, reset to `''` on each open, FLT-EDGE-08); the module receives the classified type as `CloseInteractionType`. A close C# starts classifies from its `EventSnapshot`, whose virtual-click branch reads only `Detail` and `PointerType` (no `mozInputSource`, `isTrusted` or platform flag). The two titles are restated at L3 as the recording and its reset; in the port the reopen of "preserves keyboard close modality when reopening before focus restoration" is a later message, after the return job has captured the close type. The classifier is `Floating/CloseTypeClassifier.Classify(EventSnapshot?, InteractionType?)` (design point 6, settled by the orchestrator 2026-10-27), green 2026-10-28 at L1 (`CloseTypeClassifierTests`, one title per branch) and at L2 (`FocusManagerBehaviorTests` "port: the close type is classified when the close commits, from its snapshot and the type the module recorded"). It returns `InteractionType`, not a nullable: Base UI's empty literal is `InteractionType.Unknown`, and an `Unknown` recorded type counts as none.

#### Focus manager — focus-out, restoration, trap bookkeeping

- **FLT-FOC-11** — MUST. With `closeOnFocusOut` true, a `focusout` on the DOM anchor or the floating element MUST, in a microtask, close the popup with reason `focus-out` and set return-focus suppression iff: the manager is non-modal (or the untrapped-typeable-combobox case applies); there is a `relatedTarget`; the `relatedTarget` is *unrelated* (FLT-ALG-22); no click-trigger pointer-down is in progress (FLT-FOC-13); the "press started inside" marker is not set (if it is, it is cleared and nothing happens); and the `relatedTarget` is not the most recent previously focused element — except in the untrapped-combobox case, where that comparison is skipped.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:446-451, 457-500, 544-568, 579-586`. Test: [L4] `FloatingFocusManager.test.tsx` "prop: modal › when false", "closeOnFocusOut: false keeps a non-modal element open when focus leaves", "untrapped typeable combobox closes on second tab sequence (click -> tab -> click -> tab)", "floating element closes upon tabbing out of modal combobox" (L3 for the cited titles).
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): the close is a request: the module reports `OnFocusOutClose` (the `focusout` snapshot and the recorded interaction type) and disarms until the next open, so a focus that keeps moving while C# commits sends no second request (ADR 04 Amendments 2026-10-27). Its commit is C#'s, a round trip later on Server (settled by the orchestrator 2026-10-27: a relaxation row of the PSH-KEY-07 kind). The untrapped-combobox clauses are open to M5 (ADR 12 Amendments 2026-10-27 item 4). Server: see ADR 01.
  Port note (2026-10-27, M4 track 4, [PR 128](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/128) review; conventions §3.4, ADR 01's FLT-FOC-11 row, settled by the orchestrator 2026-10-27): re-tagged [L3] → [L4]: a row of ADR 01's relaxation table runs at L4 on both modes. The cited titles run at L3; the commit timing runs at L4 as "port: the focus-out close commits a round trip after the focusout on Server and before the next frame on WebAssembly" (Server under a 150 ms one-way delay), beside "port: a cancelled focus-out close is asked again": the request is a report, so the module also re-arms when `FocusManagerOptions.Answered` moves, which the behaviour that routed the request raises for every request, closed or canceled, and sends from the commit path outside a render (ADR 04 Amendments 2026-10-27, the verification of PR 128; L3 "asks again for a focus-out close once the answered count moves").
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the registered triggers and the tree are in the focus-out check: port title "port: focus moving from a non-modal popup to another of its registered triggers asks for no close" (L3).
  Port note 2026-10-29 (M4 track 4's follow-up to [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120), [PR 145](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/145); ADR 01 Amendments 2026-10-29; M4 track 5's Gap 12, settled by the orchestrator 2026-10-28): the `focusout` the browser fires for a press or Tab comes with the body as the active element, and the related target is focused after the microtask checkpoint. WebAssembly answers the request inside the call, so a consumer that clears the anchor as it closes (Popover's controlled `TriggerId`) re-armed return focus and released it while the body was active. The re-arm's cancelled job clears the suppression (FLT-FOC-07, EDGE-07), and a release with the body active returns focus, so detachedTriggersConformanceTests' "allows controlling the open state and active trigger programmatically" returned focus to "Open Trigger 1" on WebAssembly. The decision stays in the microtask with the suppression, as Base UI's does; only the report waits until the focused element is known: at once when the active element is not the body (a script's `focus()` has landed by then), otherwise at the next capture `focusin` or a 0 ms task when focus lands nowhere, whichever comes first, cancelled by a newer decision or dispose. Green at L3: port title "port: a focus-out close answered inside the request leaves focus where the browser moved it"; at L4 on WebAssembly only (Server's report arrives a round trip later in any case), "port: a focus-out close that clears the reference leaves focus on the pressed element", red before the change. The family-level proof is Popover's "allows controlling the open state and active trigger programmatically" (PR 136, both modes).
  Port note 2026-10-29 (M4 track 6, [issue 121](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/121); gap 14 of M4 track 5, settled by the orchestrator 2026-10-29; ADR 04 Amendments 2026-10-29): the focus-out request claims the interactions session's one close in flight before its report and releases it when C# answers (the module's answered counter), so the dismiss handle's outside press of the same press is not requested again on Server (FLT-PTR-30's note of 2026-10-29).
- **FLT-FOC-12** — MUST. With `restoreFocus` set and `closeOnFocusOut` true (the restoration runs inside the focus-out listener, which is not installed when `closeOnFocusOut` is false), a `focusout` of the floating element (not the anchor) whose target is no longer visible (removed or hidden) and where the body has become the active element MUST, in the same microtask, focus the floating focus element and then: for `'popup'`, re-focus it again on the next animation frame and stop; for `true`, focus the previously focused tabbable element if it is still among the tabbable descendants, else the last tabbable descendant, else the floating focus element.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:424-427, 439-444, 505-542`. Test: [L4] `FloatingFocusManager.test.tsx` "prop: restoreFocus › true: restores focus to nearest tabbable element if currently focused element is removed", "false: does not restore focus to nearest tabbable element if currently focused element is removed" (browser only), "restores focus to the nearest tabbable element when the focused element becomes hidden".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): `RestoreFocus` is the literal `none`, `tabbable` (Base UI's `true`) or `popup`. The three titles run at L4 on the `focus-manager-restore` fixture, both modes, with the port title "port: 'popup' restores focus to the popup itself when the focused element is removed"; "restores focus to the nearest tabbable element when the focused element becomes hidden" also runs at L3.
- **FLT-FOC-13** — MUST. A document `pointerdown` (capture) on a descendant of an element carrying `data-base-ui-click-trigger`, and any `pointerdown` on the DOM anchor, MUST mark "pointer down in progress" until the next macrotask (0 ms); FLT-FOC-11 does not close while it is set. The same document `pointerdown` records whether the press target is outside the floating element, anchor, portal node and inside elements (cleared on `pointerup`/`pointercancel` and when the manager closes); while an outside press is in progress, the floating element's `focusout` MUST NOT set the "inside" marker.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:365-421, 432-437, 571-579, 606-607`; `internals/constants.ts:7`. Test: [L3] `FloatingFocusManager.test.tsx` "clicking a nested click trigger does not suppress the next focus-out close", "clears outside pointer state between keep-mounted open sessions".
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): "clicking a nested click trigger does not suppress the next focus-out close" is green at L3 on the module, and "clears outside pointer state between keep-mounted open sessions" since track 1 merged (FLT-EDGE-08's note). With a session joined (after M4 track 3 merged) the anchor is the session's active trigger and the inside marker is the session's `dataRef`, shared with the dismiss region.
- **FLT-FOC-14** — MUST. Tabbable and focusable element discovery MUST follow FLT-ALG-18 (composed tree including slotted content and open shadow roots; `inert`, `hidden`, closed `<details>`, CSS visibility and `display: none` ancestors excluded; named radio groups collapsed to the checked radio or the first radio).
  Source: `floating-ui-react/utils/tabbable.ts:8-196`. Test: [L3] `floating-ui-react/utils/tabbable.test.ts` (26 discovery cases) "includes basic tabbable controls and excludes hidden inputs", "includes embedded focusable elements in the tab order", "excludes disabled controls from the tab order", "includes slotted light DOM elements in the tab order", "excludes unslotted light DOM elements from a shadow host tab order", "keeps the summary tabbable but excludes closed details content", "keeps only the first summary tabbable and includes details without a summary", "keeps aria-disabled elements in the tab order", "excludes elements hidden with CSS visibility from the tab order", "keeps descendants of display: contents ancestors in the tab order", "keeps visible descendants of display: contents ancestors in the tab order in Chromium", "excludes descendants of hidden display: contents ancestors from the tab order", "keeps descendants that override ancestor visibility in the tab order", "excludes display: contents candidates when checkVisibility reports them hidden", "excludes display: contents candidates when checkVisibility is unavailable", "excludes descendants of display: none ancestors from the tab order", "excludes descendants of block content-visibility:hidden ancestors from the tab order", "keeps descendants of display: contents content-visibility:hidden ancestors in the tab order", "keeps descendants of inline content-visibility:hidden ancestors in the tab order", "keeps content-visibility:hidden candidates in the tab order", "keeps zero-size elements in the tab order", "keeps visuallyHidden elements in the tab order", "keeps visuallyHiddenInput elements in the tab order", "keeps only the checked radio in a named group", "keeps only the first radio when a named group has no checked item", "treats slotted elements inside inert shadow content as untabbable" (the `content-visibility`, zero-size and Chromium cases are browser only) (L4 for the browser-only cases).
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): the jsdom titles run at L3 (`src/Blazix.Primitives/js/focus-manager.test.ts`); the six browser-only titles run at L4 in `tests/browser/focus-manager.spec.ts`, importing the shipped module in the page (the `dom.spec.ts` precedent), both modes.
- **FLT-FOC-15** — MUST. "Next/previous tabbable after/before the anchor" (used by guards) MUST be computed over the whole body: `getNextTabbable(ref)` is the tabbable element after the current active element in the body's tabbable list (the first when the active element is not in the list), falling back to `ref`; `getPreviousTabbable` mirrors it. `getTabbableNearElement(ref, dir, exclude)` walks the body's focusable candidate list circularly from `ref` in direction `dir`, skipping elements inside `exclude`, returning the first tabbable (and radio-group-eligible) element, else `ref`, else `null` when `ref` is not in the list.
  Source: `floating-ui-react/utils/tabbable.ts:198-256`. Test: [L3] `tabbable.test.ts` "finds adjacent controls around a disabled anchor", "does not choose a destination when the anchor is absent from the composed tree", "finds adjacent controls around a non-tabbable anchor in a %s tree" (2 variants: shadow, slot), "does not let a disabled radio anchor suppress its peer when checked=%s and direction=%s" (4 variants).
- **FLT-FOC-16** — SHOULD. On WebKit engines, when the popup closes while a typeable element inside the floating element is the active element, that element is blurred post-commit pre-paint (prevents Safari scrolling to the page bottom on unmount).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:954-970`. Test: [L3] port title "blurs a focused input inside the popup in the batch that closes it on WebKit" (`src/Blazix.Primitives/js/focus-manager.test.ts`).
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); conventions §3.5, a fidelity correction): the statement read "that element MUST be blurred" under a SHOULD level (M4 fact sheet A, contradiction 9); the level stands and the statement no longer contradicts it. The module blurs in the batch that closes the popup, when `platform.engine.webkit`.
- **FLT-FOC-17** — MUST. In a non-modal portal, when focus leaves the portal node to an element outside it (`focusout` with an outside `relatedTarget`), every tabbable element inside the portal MUST be made untabbable (FLT-DOM-18); when focus re-enters from outside (`focusin` with an outside `relatedTarget`), or when the focus manager inside reports `open === true`, tabbability MUST be restored. Both listeners are capture-phase on the portal node. (This keeps the portaled content out of the document tab sequence except when entered through a guard or pointer.)
  Source: `floating-ui-react/components/FloatingPortal.tsx:196-234`. Test: [L3] `FloatingFocusManager.test.tsx` "non-modal + FloatingPortal › focuses inside element, tabbing out focuses last document element".
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): unless the publication is modal, capture `focusin` and `focusout` on the portal node disable and enable the focus inside, and an open publication enables it (the slot is an accessor on the state the portal publishes). Green at L3 (the cited title; `portal.test.ts` port titles) and at L4 ("port: Shift+Tab from the popup reaches the reference …", both modes).
- **FLT-FOC-18** — MUST. An enabled focus manager inside a portal MUST publish `{ modal, closeOnFocusOut, open, onOpenChange: store.setOpen, domReference }` to the portal post-commit pre-paint, and clear it when disabled/unmounted; the portal uses it for FLT-DOM-14 and FLT-KEY-07.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:972-990`. Test: [L2] `FloatingPortal.test.tsx` "uses the rendered portal ID for the aria-owns relationship".
  Port note 2026-10-27 (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)): the portal's side (the publication slot, `PopupPortalContext.Publish`, and the guards it renders) is green at L2; the publisher is M4 tracks 4, 5 and 8's.
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 13 §3): the tag stays [L2] with track 1's cited title. The publisher's C# half (the Popup's enabled, modal, open and `closeOnFocusOut` state published to the Portal, from which C# renders the outside guards, FLT-DOM-14) is `FocusManagerBehavior`'s; the JavaScript publication on the portal node is design point 3 (settled by the orchestrator 2026-10-27: `{ modal, closeOnFocusOut, open, domReference, the inside guards, close(event) }` on the portal node under `Symbol.for('blazix.portal')`, found with `closest('[data-base-ui-portal]')`; no import from `portal`). Green 2026-10-28: the C# half at L2 (`FocusManagerBehaviorTests` "port: an enabled manager inside a portal publishes modal, open and closeOnFocusOut to it, and withdraws when disabled or removed"), the JavaScript half at L3 ("port: publishes modal, closeOnFocusOut, open, the anchor and the inside guards on the enclosing portal node while enabled, and withdraws them when disabled or disposed"), both at L4 on `focus-manager-portal`.
- **FLT-FOC-19** — MUST. With `disabled` true the manager MUST perform no focus management at all (no initial focus, no trap, no marking, no guards, no return focus), and when it later becomes enabled while open it MUST run initial focus then.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:342-346, 371-373, 426-428, 621-623, 681-683, 777-780, 1003`. Test: [L3] `FloatingFocusManager.test.tsx` "prop: disabled › true -> false", "when false", "supports keepMounted behavior".
- **FLT-FOC-20** — MUST. `openInteractionType === null` (a programmatic open) MUST make the default return element prefer the previously focused element over the DOM anchor; any string (including `''`) prefers the anchor (FLT-ALG-21).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:789-792, 869-871`. Test: [L3] port title "prefers the previously focused element over the reference after a programmatic open" (`src/Blazix.Primitives/js/focus-manager.test.ts`).
- **FLT-FOC-21** — MUST. Return focus is *explicit* iff `explicitReturnFocus` is set (read at close time), else iff `returnFocus` is not a boolean (a handle/element/function counts as explicit); explicit return focus moves focus even when focus has already left the floating tree.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:913-924`. Test: [L3] `FloatingFocusManager.test.tsx` "treats a returnFocus resolver returning true as explicit", "uses the latest explicitReturnFocus value without moving focus while open".
- **FLT-FOC-22** — MUST. `enqueueFocus(el, { preventScroll, sync, shouldFocus })` MUST focus synchronously when `sync`, else on the next animation frame; only one queued focus exists globally (a new call cancels the previous pending frame); `shouldFocus()` is consulted when the frame runs and a `false` result skips the focus.
  Source: `floating-ui-react/utils/enqueueFocus.ts:11-37`. Test: [L3] port titles "focuses on the next frame, and a second call cancels the first", "reads shouldFocus when the frame runs and skips the focus when it returns false", "focuses at once with sync and cancels the pending frame" (`src/Blazix.Primitives/js/focus-manager.test.ts`).

### 4.9 DIS — dismissal (`useDismiss`)

The hook returns `{ reference, floating, trigger }` prop bags (reference and trigger are the same object); nothing when disabled. The reference bag: `onKeyDown` (Escape), `onPointerDown`/`onClick` (reference press). The floating bag: `onKeyDown` (Escape), `onPointerDown`/`onMouseDown` (prevented-press marking), capture-phase `onClickCapture`/`onMouseDownCapture`/`onPointerDownCapture`/`onMouseUpCapture`/`onTouchEndCapture`/`onTouchMoveCapture` (inside marking).

- **FLT-DIS-01** — MUST. While open and enabled, the hook MUST register on the owner document (of the floating element): with `escapeKey`, bubble-phase `keydown`, `compositionstart`, `compositionend`; with `outsidePress !== false`, capture-phase `click`, `pointerdown`, `pointerup`, `pointercancel`, `mousedown`, `mouseup`, and capture-phase **passive** `touchstart`, `touchmove`, `touchend`. All are removed when the popup closes, the hook is disabled, or any option changes.
  Source: `floating-ui-react/hooks/useDismiss.ts:718-746, 748-753`. Test: [L3] `useDismiss.test.tsx` "registers outside press touch listeners as passive", "options set to false › does not dismiss with escape key", "does not dismiss with outside press".
- **FLT-DIS-02** — MUST. The capture-phase press listeners MUST NOT decide anything themselves: each attaches a one-shot listener for the same event type to the event's target and decides there (i.e. after the target's own bubble-phase handlers and the floating element's capture handlers have run), so that an inside press can mark itself first. The capture handler only cancels the touch disarm timer and records the press (FLT-PTR-28) and pointer type.
  Source: `floating-ui-react/hooks/useDismiss.ts:565-614, 693-716`. Test: [L3] `useDismiss.test.tsx` "dragging inside the floating element does not close".
- **FLT-DIS-03** — MUST. Events of the wrong kind for the resolved mode MUST be ignored: in `intentional` mode everything except `click`; in `sloppy` mode `click`. An ignored non-click event outside own elements additionally clears drag-suppression (FLT-PTR-29), and any ignored event clears the inside marker.
  Source: `floating-ui-react/hooks/useDismiss.ts:368-374, 387-397`. Test: [L3] `useDismiss.test.tsx` "outsidePressEvent: intentional › mouse click whose press started before open does not close".
- **FLT-DIS-04** — MUST. An outside-press candidate MUST be ignored when the inside marker is set (the press started or ended inside the floating element's own handlers within the last macrotask); the marker is then cleared.
  Source: `floating-ui-react/hooks/useDismiss.ts:234-237, 399-402`. Test: [L3] `useDismiss.test.tsx` "does not dismiss when clicking portaled children".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): a press inside a nested session's floating element marks the enclosing sessions too (the forest stands in for React's propagation through the portal); the titles place the portaled nodes in `document.body`, no portal module.
- **FLT-DIS-05** — MUST. A press whose target is a registered trigger element, or inside one, MUST NOT close the popup (switching triggers is handled by the trigger's own interaction).
  Source: `floating-ui-react/hooks/useDismiss.ts:414-423`. Test: none (component docs: multiple-trigger tests).
- **FLT-DIS-06** — MUST. A press on an element injected into the page after the popup opened MUST NOT close it: when at least one inert marker (`[data-base-ui-inert]`) exists in the target's root (its shadow root, else the floating element's document), the target is an element other than `html`/`body` that does not contain the floating element, and the target's top-level ancestor (the highest element below the last traversable node) contains none of the markers, the press is ignored.
  Source: `floating-ui-react/hooks/useDismiss.ts:403-412, 425-448`. Test: [L3] `useDismiss.test.tsx` "outsidePress ignored for third party elements", "dismisses when clicking outside a shared shadow root", "dismisses when clicking outside a shared shadow root while focus is managed".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 13 Amendments 2026-10-22): "outsidePress ignored for third party elements" and "dismisses when clicking outside a shared shadow root" are green at L3; "dismisses when clicking outside a shared shadow root while focus is managed" needs track 4's focus manager and is open until both tracks are on `main`.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): "dismisses when clicking outside a shared shadow root while focus is managed" is green at L3 (`focus-manager.test.ts` "with the interactions session"), on arrival; the portal moves the node into the shadow root as a script-held container.
- **FLT-DIS-07** — MUST. A non-touch press on a scrollbar MUST NOT close the popup: for an HTML target, when the target is scrollable on an axis (root nodes always; otherwise `overflow-x`/`overflow-y` matching `auto|scroll`, with `clientWidth/Height > 0` and `scrollWidth/Height > clientWidth/Height`) and the press is in the scrollbar band — vertical: `offsetX > clientWidth` (LTR) or `offsetX <= offsetWidth - clientWidth` (RTL); horizontal: `offsetY > clientHeight`.
  Source: `floating-ui-react/hooks/useDismiss.ts:450-482`. Test: none.
- **FLT-DIS-08** — MUST. A press inside the popup's own elements (floating element or DOM anchor, composed-path containment) or inside the floating element of any *open* descendant tree node MUST NOT close the popup.
  Source: `floating-ui-react/hooks/useDismiss.ts:180-185, 376-385, 484-486`. Test: [L3] `useDismiss.test.tsx` "outsidePress not ignored for nested floating elements", "nested floating elements with different portal containers".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): as FLT-DIS-04's note; the nested popup is a session whose `ParentToken` is the outer one.
- **FLT-DIS-09** — MUST. When `outsidePress` is a function it MUST be called with the native event after all the above checks, and a `false` result MUST veto the close.
  Source: `floating-ui-react/hooks/useDismiss.ts:510-512`. Test: [L3] `useDismiss.test.tsx` "outsidePress function guard" (both describe blocks).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): Base UI's `outsidePress` function is Dialog's four data predicates (`PrimaryButtonOnly`, `SingleTouch`, `OwnBackdropIds`, `Topmost`), evaluated in JavaScript (settled by the orchestrator 2026-10-27).
- **FLT-DIS-10** — MUST. When any *open* descendant tree node has `__outsidePressBubbles === false`, the outside press MUST NOT close this popup (the child closes first; the next press closes the parent).
  Source: `floating-ui-react/hooks/useDismiss.ts:169-178, 514-516`. Test: [L3] `useDismiss.test.tsx` "prop: bubbles.outsidePress › when true", "when false", "mixed"; "prop: capture.outsidePress › when true" (nested dialogs: a press on the outer closes only the inner one).
- **FLT-DIS-11** — MUST. A surviving outside press MUST close the popup with reason `outside-press` and the native event, then clear the inside marker.
  Source: `floating-ui-react/hooks/useDismiss.ts:518-519`. Test: [L3] `useDismiss.test.tsx` "dismisses with outside pointer press".
- **FLT-DIS-12** — MUST. The floating element's capture handlers for `click`, `mousedown`, `pointerdown`, `mouseup`, `touchend`, `touchmove` MUST set the inside marker and schedule its clearing after 0 ms; the `mousedown`/`pointerdown` capture handlers MUST additionally record "press started inside" when `button === 0` and the composed target is inside the floating element. The marker MUST also be cleared when the hook unmounts.
  Source: `floating-ui-react/hooks/useDismiss.ts:234-258, 295, 784-801`. Test: [L3] `useDismiss.test.tsx` "clears the inside marker when the interaction owner unmounts (strict: %s)" (2 variants: false, true).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the Strict Mode title is restated (a repeated `attach` is a no-op, `dispose` is idempotent; ADR 06 Amendments 2026-10-18 rule 2).
- **FLT-DIS-13** — MUST. The floating element's bubble-phase `pointerdown`/`mousedown` handlers MUST record "press start prevented" when the event (or its native event) is default-prevented and a press had started inside.
  Source: `floating-ui-react/hooks/useDismiss.ts:260-275, 787-791`. Test: [L3] `useDismiss.test.tsx` "press start prevented inside does not require double outside click".
- **FLT-DIS-14** — MUST. Session boundaries: an `openchange(false)` MUST forget the observed press (FLT-PTR-28) even if the popup is reopened in the same commit; when the popup renders closed, the composing flag, remembered pointer type and touch record MUST also be reset.
  Source: `floating-ui-react/hooks/useDismiss.ts:277-293, 298-306`. Test: [L3] `useDismiss.test.tsx` "press seen in a previous open session does not leak into a reopen", "press seen before a same-batch close and reopen does not leak into the new session", "does not dismiss a reopened popup when an old touch moves %s pixels and ends" (2 variants; touch record reset).
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the listener runs when the relay brings the commit; a close and a reopen C# commits in one batch are replayed close first from the relay's `coalescedCloseRevision` and `coalescedCloseReason` (design point 11, settled by the orchestrator 2026-10-27), so the boundary is seen. L3: "port: a close and a reopen committed in one batch are replayed, the close with its reason, so a press seen before it does not leak into the reopened session".
- **FLT-DIS-15** — MUST. While open and enabled the hook MUST publish its normalized `bubbles` flags into context data as `__escapeKeyBubbles` and `__outsidePressBubbles` so parents can read them through the tree (FLT-KEY-02, FLT-DIS-10).
  Source: `floating-ui-react/hooks/useDismiss.ts:310-311`. Test: [L3] `useDismiss.test.tsx` "prop: bubbles.escapeKey › mixed".
- **FLT-DIS-16** — MUST. `escapeKey: false` MUST disable all Escape handling (document listener and prop-bag handlers); `outsidePress: false` MUST disable all outside-press listeners; `enabled: false` MUST disable everything and return empty prop bags.
  Source: `floating-ui-react/hooks/useDismiss.ts:136, 201-205, 719-746, 813-815`. Test: [L3] `useDismiss.test.tsx` "options set to false" (all four).

### 4.10 EVT — callbacks, reasons, events and scheduling

- **FLT-EVT-01** — MUST. The reasons this slice emits, and their origin: `trigger-press` (`useClick` default; `useDismiss` reference press), `input-press` (`useClick` with `reason: REASONS.inputPress`, Combobox), `trigger-hover` (all hover opens/closes, including child-closed cascades), `trigger-focus` (`useFocus` open and blur close), `outside-press` (`useDismiss`), `escape-key` (`useDismiss`), `focus-out` (focus manager and portal after-outside guard), `none` (delay group closing the previously active tooltip). No other reason strings originate here.
  Source: `internals/reason-parts.ts:1-24`; `useClick.ts:67`; `useDismiss.ts:193, 220, 518`; `useFocus.ts:162, 231`; `useHoverReferenceInteraction.ts:198, 280`; `useHoverFloatingInteraction.ts:168, 242`; `FloatingFocusManager.tsx:566`; `FloatingPortal.tsx:286`; `FloatingDelayGroup.tsx:237`. Test: [L3] `useDismiss.test.tsx` App helper (`:36-56`, asserts reasons); `useHoverReferenceInteraction.test.tsx` "reports the hover reason".
- **FLT-EVT-02** — MUST. Every open-state change dispatched through a root store (directly, or by the popup store in sync-only mode) MUST emit `openchange` on the root event bus **before** the consumer's `onOpenChange` callback runs, with details `{ open, reason, nativeEvent, nested, triggerElement }` (FLT-STA-02). Listeners in this slice: `useFocus` (FLT-FOC-03), hover trigger hook (FLT-PTR-17), `useDismiss` (FLT-DIS-14), focus manager (FLT-FOC-08/10).
  Source: `floating-ui-react/components/FloatingRootStore.ts:106-135`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "prop: delay › open with close 0".
  Fidelity correction (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); conventions §3.5, no decision record): every M4 popup store calls the consumer's `onOpenChange` first and dispatches only a change it did not cancel (`popover/store/PopoverStore.ts:139-145`, `utils/popups/popupStoreUtils.ts:273-281`); "before the consumer's `onOpenChange`" is the order of the standalone root store alone (FLT-STR-11, M5). Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): C# relays the last committed change in the session handle's `update` (`OpenChangeRelay`: revision, reason, the event's kind and type, the pointer type, the trigger id), and the session emits `openchange` once per relayed revision; a cancelled request moves no revision and emits nothing. Tooltip's press-cancel emits on the bus alone. On Server the listeners run a round trip after the commit (ADR 01 Amendments 2026-10-27). A close and a reopen C# commits in one batch are replayed: the relay's `coalescedCloseRevision` and `coalescedCloseReason` (track 1's members) make the session emit that close's `openchange` first, with its reason, then the open (design point 11, settled by the orchestrator 2026-10-27).
- **FLT-EVT-03** — MUST. Hover closes MUST emit `floating.closed` on the floating tree's event bus with the mouse event, after calling `setOpen(false)`; parents subscribe to it to cascade-close (FLT-PTR-25) and to resume safe-polygon evaluation.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:199, 204`; `useHoverFloatingInteraction.ts:169, 243`. Test: none.
- **FLT-EVT-04** — MUST. All change details MUST be created with the EVD constructor `createChangeEventDetails(reason, nativeEvent, trigger?)`; the `trigger` argument is supplied by `useClick` (the pressed trigger, or the typeable target), `useFocus` (the focused trigger, open only) and the hover trigger hook (the resolved trigger node, open only). Closes carry no trigger.
  Source: `floating-ui-react/hooks/useClick.ts:83`; `useFocus.ts:159-166, 231`; `useHoverReferenceInteraction.ts:280, 292, 297, 482`. Test: [L3] `useClick.test.tsx` "uses the configured reason".
- **FLT-EVT-05** — MUST. Escape cancel semantics: calling `cancel()` on the details inside `onOpenChange` MUST leave the keydown's default action intact; calling `allowPropagation()` MUST let the keydown propagate even though the popup does not bubble Escape (FLT-KEY-01).
  Source: `floating-ui-react/hooks/useDismiss.ts:220-231`. Test: [L4] `useDismiss.test.tsx` "does not call preventDefault on escape key if close is canceled".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the cancel and the allowed propagation reach the module as the `escape` verdicts `Stop`, `Run` and `Prevent` on WebAssembly; on Server under FLT-KEY-01's row. The L4 title runs in `interactions.spec.ts` on track 1's popup host, both modes (2026-10-28, after PR 126). Server: see ADR 01.
- **FLT-EVT-06** — MUST. Timers used by this slice and their values (all restartable, cancelled on close/disable): `useClick` touch-open deferral (`touchOpenDelay` ms) and one next-frame deferral for pointer `mousedown`; `useFocus` open delay (`delay` ms) and blur check (0 ms); `useDismiss` inside-marker clear (0 ms), composition settle (5 ms WebKit / 0 ms), prevented-press suppression (0 ms), touch disarm (1000 ms); hover shared open/close timer (`delay.open`/`delay.close`/`closeDelay` ms), rest timer (`restMs` ms), child-closed cascade (0 ms); safe polygon intent timer (40 ms); delay group reset (`timeoutMs` ms); focus manager blur/inside-marker clear (0 ms), click-trigger pointer-down reset (0 ms), restore-focus re-focus (next frame), initial focus (microtask + next frame), return focus (microtask); `enqueueFocus` (next frame).
  Source: `useClick.ts:73-74, 86, 183`; `useFocus.ts:54, 170, 199`; `useDismiss.ts:159-162, 237, 329-331, 341, 556`; `useHoverInteractionSharedState.ts:21-22`; `useHoverReferenceInteraction.ts:197, 290, 495`; `useHoverFloatingInteraction.ts:67, 173, 240`; `safePolygon.ts:92, 437`; `FloatingDelayGroup.tsx:183`; `FloatingFocusManager.tsx:326-328, 392, 435, 525, 580, 690, 736, 908`; `enqueueFocus.ts:29`. Test: see the per-feature tests cited above.
- **FLT-EVT-07** — MUST. The delay group MUST close the previously active tooltip (through its stored `onOpenChange`) with reason `none` and no event when a different tooltip in the group opens (FLT-ALG-23).
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:234-238`. Test: [L3] `FloatingDelayGroup.test.tsx` "groups delays correctly".

### 4.11 ANI — lifecycle and animation-related behavior

- **FLT-ANI-01** — MUST. Delay-group *instant phase*: when a tooltip opens while another member of the same group is the active one, both the newly opened and the previously active tooltip MUST report `isInstantPhase === true`; when a tooltip opens and no different member was active, both MUST report `false`. Consumers use it to disable open/close transitions when moving between tooltips (PSH `instantType`).
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:216-251`. Test: [L3] `FloatingDelayGroup.test.tsx` "resets the instant phase after Strict Mode replays the lifecycle effects".
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the Strict Mode title is restated (ADR 06 Amendments 2026-10-18 rule 2).
- **FLT-ANI-02** — MUST. A Positioner MUST be invisible (`opacity: 0`, FLT-DOM-02) and positioned `fixed` at the viewport origin until the first positioning pass of the current open completes; `isPositioned` MUST be reset to `false` whenever the session's `open` input becomes `false` (kept-mounted popups pass `mounted` as `open`), so the next open starts hidden again.
  Source: `internals/useAnchorPositioning.ts:494, 509-519`; `vendor/@floating-ui_react-dom/dist/floating-ui.react-dom.mjs:151-176`. Test: [L4] `test/waitForPositioned.ts:4-11`. Docs: `docs/src/app/(private)/experiments/anchor-side-animations.tsx:7-12` (animation must wait for the side to be computed).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4 port title "port: the Positioner stays fixed at the viewport origin and transparent until the first pass of each mount" (a kept-mounted reopen), both modes; `isPositioned` is reset when a kept-mounted Positioner unmounts.
- **FLT-ANI-03** — MUST. Adaptive origin (FLT-ALG-12): when the floating element has a non-zero CSS `transition-duration`, a `left`-side placement MUST be expressed with `right` and a `top`-side placement with `bottom` (distance from the opposite edge of the offset parent/viewport), so that CSS transitions of size grow away from the anchor; with no transition the plain `left`/`top` values are used.
  Source: `utils/adaptiveOriginMiddleware.ts:6-73`. Test: [L4] `test/positionerConformanceTests.tsx` "uses top/left positioning with Viewport". Docs: `CHANGELOG.md` "Fix auto-resize origin for left-anchored popups (#5370)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 8): L4, the suite's "uses top/left positioning with Viewport" over the `AdaptiveOrigin` stand-in; the transition branch at L3, `src/Blazix.Primitives/js/positioning.test.ts` "measures a left or top placement from the opposite edge while the floating element has a transition".
- **FLT-ANI-04** — MUST. Lazy flip: with `lazyFlip` truthy, once the popup is mounted and positioned on a side different from the requested one (or, with `'placement'`, with a different alignment), that rendered placement MUST be locked as the requested placement until the popup unmounts, so content resizes (filtering) do not flip it back; with `lazyFlip` false, each pass recomputes freely. `true` locks the side only; `'placement'` locks side and alignment.
  Source: `internals/useAnchorPositioning.ts:182-205, 593-614`. Test: [L4] `useAnchorPositioning.test.tsx` "locks a flipped alignment after the popup shrinks", "leaves the alignment free when lazy flipping is side-only", "locks a flipped side after the popup shrinks", "flips back to the preferred side without lazy flipping". Docs: `CHANGELOG.md` "Avoid redundant re-renders during lazy flipping (#5372)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): L4 in the page, the four titles of `useAnchorPositioning.test.tsx` verbatim in `tests/browser/positioning.spec.ts` (the shipped module with a stub twin, the `dom.spec.ts` precedent), both modes. `lazyFlip` is sent as `side` for `true` and `placement`.
- **FLT-ANI-05** — MUST. Transition-disabling styles for the Positioner come from TRN (`getDisabledMountTransitionStyles(transitionStatus)`) and are merged after the positioner styles.
  Source: `utils/usePositioner.tsx:37`. Test: none. Docs: `CHANGELOG.md` "Fix mount transitions on `Positioner` in Firefox (#3424)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port titles, L2 `PositionerBehaviorTests` "renders a presentation element with the requested placement and the pre-position style before the first pass" (`transition: none;` while starting, before the consumer's style), and L3 "composes the consumer between the positioner styles and the measured variables…" (`transition: none` in the `transitions` layer of `dom`'s writer, under the consumer).

### 4.12 POS — anchored positioning

- **FLT-POS-01** — MUST. The requested placement is `<physicalSide>` when `align === 'center'`, else `<physicalSide>-<align>`, where `physicalSide` resolves `side` through FLT-I18N-01 (or is the lazily locked side, FLT-ANI-04).
  Source: `internals/useAnchorPositioning.ts:188-205`. Test: [L4] `test/positionerConformanceTests.tsx` "uses transform positioning without Viewport" (bottom/center → `translate(10px, 36px)` for a 72×36 anchor and 52×24 popup).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`); also L3 "requests the physical placement of a logical side with its alignment (FLT-POS-01, FLT-I18N-01)".
- **FLT-POS-02** — MUST. The middleware pipeline MUST be, in order: `inline` (if supplied), `offset`, then either `shift, flip` or `flip, shift` (FLT-POS-03; a disabled one is omitted), `size`, `arrow`, `transformOrigin`, `hide`, `adaptiveOrigin` (if supplied). The engine runs them per FLT-ALG-01.
  Source: `internals/useAnchorPositioning.ts:257-457`. Test: [L3] `useAnchorPositioning.test.tsx` "uses the visual viewport for shift by default".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "assembles offset, shift and flip, size, arrow, transformOrigin and hide in order, adaptiveOrigin last when on (FLT-POS-02)", except the `inline` clause. That clause is open to M4 track 7 (PreviewCard's middleware, a `positioning` edit in its own commit); the slot is left first in the pipeline.
- **FLT-POS-03** — MUST. `shift` MUST run before `flip` when `collisionAvoidance.side === 'shift'`, or `collisionAvoidance.align === 'shift'`, or the alignment being requested is `'center'`; otherwise `flip` runs before `shift`.
  Source: `internals/useAnchorPositioning.ts:348-357`. Test: none. Docs: `CHANGELOG.md` "Fix explicit `collisionAvoidance` with `side: 'flip'` not working (#3877)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port title at L3, `src/Blazix.Primitives/js/positioning.test.ts` "runs flip before shift for an aligned placement unless an axis avoids collisions by shifting (FLT-POS-03)".
- **FLT-POS-04** — MUST. `flip` MUST be omitted when `collisionAvoidance.side === 'none'`; otherwise configured with: boundary/rootBoundary from `collisionBoundary` (FLT-API-09); `padding` = normalized `collisionPadding` + 1 on every side + an extra 1 on the side *opposite* the requested side (top for `'bottom'`, bottom for `'top'`, left for `'right'`, right for `'left'`; none for logical sides) — the preferred-side bias; `mainAxis = !shift.crossAxis && side === 'flip'`; `crossAxis = align === 'flip' ? 'alignment' : false`; `fallbackAxisSideDirection = collisionAvoidance.fallbackAxisSide`.
  Source: `internals/useAnchorPositioning.ts:235-239, 291-307`. Test: [L4] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge" (bias must not affect resting position). Docs: `CHANGELOG.md` "Prevent unwanted flip with capped scrollable content (#5120)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's collision title in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`); also L3 "configures flip with the preferred-side bias and omits it when the side axis avoids nothing (FLT-POS-04)".
- **FLT-POS-05** — MUST. `shift` MUST be omitted when `collisionAvoidance.align === 'none'` and `collisionAvoidance.side !== 'shift'`; otherwise configured with: boundary from `collisionBoundary`; `padding` = normalized `collisionPadding` (no bias); `rootBoundary` = `shift.rootBoundary` (`undefined` → engine default `'viewport'`, or `'layoutViewport'`); `mainAxis` (alignment axis) `= align !== 'none'`; `crossAxis` (side axis) `= sticky || shift.crossAxis || side === 'shift'`; `limiter` = none when `sticky || shift.crossAxis`, else `limitShift` with `offset = arrowSize / 2 + (paddingSum) / 2` where `arrowSize` is the arrow element's width for vertical sides / height for horizontal sides (`{}`, i.e. offset 0, when no Arrow part is registered) and `paddingSum` is `left + right` padding for vertical sides / `top + bottom` for horizontal sides.
  Source: `internals/useAnchorPositioning.ts:287-289, 308-346`. Test: [L3] `useAnchorPositioning.test.tsx` "uses the visual viewport for shift by default", "uses the configured shift options". Docs: `CHANGELOG.md` "Fix `limitShift` offset based on arrow size (#2571)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): green at L3, the two shift titles and `src/Blazix.Primitives/js/positioning.test.ts` "configures shift with the unbiased padding and a limiter, and omits it when no axis shifts (FLT-POS-05, FLT-POS-14)".
- **FLT-POS-06** — MUST. `offset` MUST be configured with a function producing `{ mainAxis: sideOffset, crossAxis: alignOffset, alignmentAxis: alignOffset }` (functions resolved per FLT-API-12 on each pass); consequently positive `alignOffset` moves the popup toward the `end` direction for `start`/`center` alignment and toward `start` for `end` alignment (engine semantics FLT-ALG-04).
  Source: `internals/useAnchorPositioning.ts:264-285`. Test: [L4] `test/positionerConformanceTests.tsx` "prop: sideOffset › offsets the positioner when a number is specified" (`y = 36 + 7`), "prop: alignOffset › offsets the positioner when a number is specified" (`x = 10 + 7`), "offsets the positioner when a function is specified" (both props).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's offset titles in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`); also L3 "feeds the offset middleware the logical side and align of the placement each pass evaluates (FLT-API-12, FLT-POS-06)".
- **FLT-POS-07** — MUST. `size` MUST be configured with the collision boundary and normalized padding and an `apply` that writes the CSS variables of FLT-DOM-05/06 only while mounted (it never changes the element's size itself).
  Source: `internals/useAnchorPositioning.ts:360-379`. Test: [L4] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's collision title in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`); also L3 "normalizes the collision padding…" (the size middleware's padding).
- **FLT-POS-08** — MUST. The `arrow` middleware (Base UI fork, FLT-ALG-09) MUST be configured with the registered arrow element (or the stand-in, FLT-DOM-10) and `padding = arrowPadding` (0 for the stand-in).
  Source: `internals/useAnchorPositioning.ts:381-390`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port title at L3, `src/Blazix.Primitives/js/positioning.test.ts` "configures the arrow middleware with the Arrow and its padding, or a detached stand-in with none (FLT-POS-08, FLT-DOM-10)".
- **FLT-POS-09** — MUST. `anchorHidden` MUST be `true` iff the Base UI `hide` middleware (FLT-ALG-10) reports `referenceHidden`: the anchor is fully clipped on some side by its clipping boundary, or its rect is all zeros (not in layout).
  Source: `internals/useAnchorPositioning.ts:455, 591`; `utils/hideMiddleware.ts:3-25`. Test: [L3] `utils/hideMiddleware.test.ts` "matches Floating UI hide() for reference overflow %#" (5 cases), "treats an all-zero reference rect as hidden". Docs: `CHANGELOG.md` "Make popups' `data-anchor-hidden` state attribute check for anchor presence in layout (#3267)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): green at L3, the `hideMiddleware.test.ts` titles verbatim against the engine's own `hide()` and `src/Blazix.Primitives/js/positioning.test.ts` "reports the rendered side, align, anchorHidden and uncentered once per change, never once per pass (FLT-POS-09, FLT-POS-10)".
- **FLT-POS-10** — MUST. The rendered outputs after each pass MUST be: `physicalSide` = side of the rendered placement; `side` = its logical form per FLT-I18N-02; `align` = its alignment or `'center'`.
  Source: `internals/useAnchorPositioning.ts:588-590`. Test: [L4] `test/positionerConformanceTests.tsx` "reads the latest side after a flip inside the function" (`side: 'left'` at the viewport origin renders `right`), "reads the latest align after a flip inside the function" (`right`/`start` → `end`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's restated "applies the override for the latest side after a flip", "applies the override for the latest align after a flip" and "applies the override for the logical side after a flip" (each per "prop: $prop") in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`), which assert the rendered `data-side`/`data-align`; also L3 "reports the rendered side, align, anchorHidden and uncentered once per change…".
- **FLT-POS-11** — MUST. Anchor tracking options MUST be `ancestorScroll = !disableAnchorTracking`, `elementResize = !disableAnchorTracking && ResizeObserver exists`, `layoutShift = !disableAnchorTracking && IntersectionObserver exists`, with the engine defaults for `ancestorResize` (true) and `animationFrame` (false); tracking MUST run per FLT-ALG-13 for as long as both the position anchor and the floating element exist — through the session's `whileElementsMounted` for normally mounted popups, or, for kept-mounted popups, only while `mounted` is true.
  Source: `internals/useAnchorPositioning.ts:472-478, 496-498, 581-586`. Test: none. Docs: `CHANGELOG.md` "Fix disabled anchor tracking on scroll (#5478)", "**Breaking change:** Replace `trackAnchor` with `disableAnchorTracking`".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port title at L3, `src/Blazix.Primitives/js/positioning.test.ts` "tracks the anchor while mounted with the configured options, and a kept-mounted popup only while mounted (FLT-STA-09, FLT-POS-11, FLT-POS-12)". Every Positioner is tracked only while mounted, as FLT-STA-09 clears the elements for the others.
- **FLT-POS-12** — MUST. The engine strategy MUST equal `positionMethod`; the session's `open` input is `mounted` for kept-mounted popups and unspecified otherwise (FLT-ANI-02).
  Source: `internals/useAnchorPositioning.ts:492-502`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port titles at L3, the tracking title above and "requests the physical placement of a logical side with its alignment…" (the strategy is `positionMethod`).
- **FLT-POS-13** — MUST. A position anchor that is a DOM element MUST be measured through its own `getBoundingClientRect`/`getClientRects` (wrapped virtual anchor, FLT-STA-07) so that an anchor inside a different document position than the floating element is still measured correctly by the engine's offset-parent conversion; with no custom `anchor`, the position anchor is the root store's reference (the active trigger, PSH).
  Source: `floating-ui-react/hooks/useFloating.ts:48-71`; `FloatingRootStore.ts:37`. Test: [L3] `useFloating.test.tsx` "uses the supplied root store while preserving DOM and position references".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "positions against the active trigger, or a custom anchor wrapped so that its own rect is measured (FLT-STA-07, FLT-POS-13)".
- **FLT-POS-14** — MUST. `sticky: true` MUST enable side-axis shifting without a limiter (the popup stays inside the boundary even when the anchor leaves it); `sticky: false` with no `shift.crossAxis` MUST limit alignment-axis shifting so the popup never detaches from the anchor by more than the arrow/padding allowance (FLT-POS-05).
  Source: `internals/useAnchorPositioning.ts:288-289, 316-320`. Test: none. Docs: `docs/src/app/(private)/experiments/anchor-positioning.tsx:307-310`.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port title at L3, `src/Blazix.Primitives/js/positioning.test.ts` "configures shift with the unbiased padding and a limiter, and omits it when no axis shifts (FLT-POS-05, FLT-POS-14)" (`sticky`: side-axis shift without a limiter).
- **FLT-POS-15** — MUST. The resting position after collision handling MUST be exactly `collisionPadding` from the colliding boundary edge (the +1/+2 flip bias affects only the flip decision).
  Source: `internals/useAnchorPositioning.ts:229-239, 294-301, 308-313`. Test: [L4] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge" (top edge at exactly 12 px).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's "rests exactly at collisionPadding from the colliding edge" in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`), both modes.
- **FLT-POS-16** — MUST. The `update()` function returned by the session MUST recompute the position on demand (used by anchor tracking and by component code after size changes); each pass that produces data different from the previous pass MUST be committed synchronously (flushed) so the next paint shows it. Server: see ADR 01 relaxation table.
  Source: `vendor/@floating-ui_react-dom/dist/floating-ui.react-dom.mjs:139-167`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 01 relaxation table, ADR 13 §1): met on both modes, asserted per mode by `tests/browser/positioning.spec.ts` "port: scrolling an ancestor of the anchor re-positions the Positioner in the same frame on both modes" (read inside the scroll event's dispatch); `update()` is exported for PreviewCard; a pass commits only when its data changed (L3 report title).
- **FLT-POS-17** — MUST. Changing `anchor`, `side`, `align`, offsets, collision options, `positionMethod` or the direction MUST re-run positioning with the new configuration on the next pass (the middleware list is rebuilt whenever any of those inputs change).
  Source: `internals/useAnchorPositioning.ts:264-285, 308-346, 486-502`; `vendor/@floating-ui_react-dom/dist/floating-ui.react-dom.mjs:112-115, 139-167`. Test: [L4] `test/positionerConformanceTests.tsx` "updates positioning when Viewport mounts and unmounts".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); design point 8): L4, the suite's "updates positioning when Viewport mounts and unmounts" over the `AdaptiveOrigin` stand-in; also L3 "rebuilds the pipeline only when one of its inputs changes (FLT-ALG-14, FLT-POS-17)" and "rebuilds the pipeline when an offset form changes (FLT-POS-17)", and L2 "sends one update per change of the options and none for an equal re-render". A changed offset form re-runs at once (Base UI reads a function through a ref and waits for the next tracked pass).

### 4.13 I18N — direction

- **FLT-I18N-01** — MUST. `side: 'inline-end'` MUST resolve to `right` in LTR and `left` in RTL; `'inline-start'` to `left` in LTR and `right` in RTL, where the direction comes from the UTL `DirectionProvider` (default LTR). Physical sides are unaffected.
  Source: `internals/useAnchorPositioning.ts:179-180, 190-201`. Test: [L4] `test/positionerConformanceTests.tsx` "reads the logical side in RTL mode inside the function" (`inline-start` in RTL at the viewport origin: physical right, reported `inline-start`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): L4, the suite's restated "applies the override for the logical side in RTL mode" (per "prop: $prop") in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`); also L3 "requests the physical placement of a logical side with its alignment…" and L2 "sends the reading direction of the nearest DirectionProvider and its changes" (`DirectionProviderContext.Read`, the literal from `CompositeLiterals`).
- **FLT-I18N-02** — MUST. The reported `side` (state, `data-side`, offset-function `side`) MUST be logical when the requested side was logical: rendered `right` → `inline-end` (LTR) / `inline-start` (RTL); rendered `left` → `inline-start` (LTR) / `inline-end` (RTL); `top`/`bottom` unchanged; when the requested side was physical, the rendered physical side is reported.
  Source: `internals/useAnchorPositioning.ts:41-53, 589`. Test: [L4] `test/positionerConformanceTests.tsx` "reads the logical side after a flip inside the function" (`inline-start` LTR at the origin → `inline-end`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): L4, the suite's restated "applies the override for the logical side after a flip" and "applies the override for the logical side in RTL mode" (per "prop: $prop") in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`); also L3 "reports $side rendered $rendered (rtl: $rtl) as $expected" (`getLogicalSide`).
- **FLT-I18N-03** — MUST. The engine's RTL flag (`platform.isRTL(floating)` = computed `direction === 'rtl'` of the floating element) MUST affect: alignment placement coordinates (sign of the `start`/`end` adjustment on vertical sides, FLT-ALG-02), `offset` cross-axis sign on vertical sides (FLT-ALG-04), `flip` alignment-side detection and fallback side order (FLT-ALG-05), `size` width side (FLT-ALG-08), and the transform-origin cross origin for aligned arrowless popups (FLT-ALG-11, which reads the platform flag, not the provider direction).
  Source: `vendor/@floating-ui_dom/dist/floating-ui.dom.mjs:495-497`; `internals/useAnchorPositioning.ts:420-425`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03, both direction sources kept): port titles, L3 "grows an aligned arrowless popup from its aligned edge, by the floating element direction" (the platform's `isRTL`, not the provider) and L4 "transform origin › uses the floating element direction for RTL alignment".
- **FLT-I18N-04** — MUST. Scrollbar-press detection (FLT-DIS-07) MUST use the RTL rule when the target's computed `direction` is `rtl`.
  Source: `floating-ui-react/hooks/useDismiss.ts:463-477`. Test: none.

### 4.14 A11Y — screen-reader and focus-visible heuristics

- **FLT-A11Y-01** — MUST. A pointer event is *virtual* (synthesized by assistive technology) iff, outside jsdom: (not Android and `width === 0 && height === 0`); or (`type === 'pointerdown'`, `width === 1`, `height === 1`, `pressure === 0`, `detail === 0`, `pointerType === 'mouse'`, and (Android or `buttons === 0`)); or (`width < 1 && height < 1`, `pressure === 0`, `detail === 0`, `pointerType === 'touch'`) (iOS VoiceOver). In jsdom it is never virtual.
  Source: `floating-ui-react/utils/event.ts:25-48`. Test: [L3] `floating-ui-react/utils/event.test.ts` "recognizes a pressureless 1x1 mouse pointerdown as a screen reader press", "ignores a pressed mouse", "recognizes a TalkBack press with a pressed button on Android", "ignores the same shape on other pointer types", "ignores the same shape on events other than pointerdown", "recognizes an iOS VoiceOver touch".
- **FLT-A11Y-02** — MUST. A mouse/click event is a *virtual click* iff `pointerType === ''` and `isTrusted`; or (Android and a `pointerType` is present and `type === 'click'` and `buttons === 1`); or `detail === 0` with no `pointerType`.
  Source: `floating-ui-react/utils/event.ts:13-23`. Test: [L3] `useDismiss.test.tsx` "press-less outside click reporting a pointer type closes".
- **FLT-A11Y-03** — MUST. `useClick` MUST treat a virtual press as its own pointer type `'virtual'`: it is never dropped by `ignoreMouse`, opens synchronously from `mousedown` (no frame deferral), and reports the handler's element (not the pressed descendant) as trigger (FLT-PTR-01/02).
  Source: `floating-ui-react/hooks/useClick.ts:127-137, 166-175`. Test: [L3] `FloatingFocusManager.test.tsx` "focuses without a frame delay when a screen reader press opens it".
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the cited title runs at L3 (`focus-manager.test.ts` "with the interactions session") and is green, but observes only FLT-A11Y-04's and FOC-06's clause: as Base UI's, it sends no `pointerdown`, so no press is typed `'virtual'`, and it waits the click region's frame; and jsdom's `isVirtualPointerEvent` is `false` by design. No title observes this row's clause, so it stays open to M4 track 5 (Popover's trigger at L4, where a virtual press is a 0×0 `pointerdown` in a real browser; ADR 13 §1).
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): open to M5, as FLT-PTR-01 (item 17 of §10).
- **FLT-A11Y-04** — MUST. The focus manager MUST focus the initial element synchronously (no frame delay) when the recorded open event is a `mousedown` that is a virtual click, so a screen reader's focus re-sync does not pull focus back to the trigger (FLT-FOC-06).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:729-738`. Test: [L3] `FloatingFocusManager.test.tsx` "focuses without a frame delay when a screen reader press opens it", "keeps the frame delay for a real mouse press".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): the recorded open event is the interactions session's (track 3); until it is on `main` no open is a virtual press, so "keeps the frame delay for a real mouse press" is green at L3 and "focuses without a frame delay when a screen reader press opens it" is open until track 3.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): "focuses without a frame delay when a screen reader press opens it" is green at L3 (`focus-manager.test.ts` "with the interactions session").
- **FLT-A11Y-05** — MUST. Intentional-mode outside presses MUST accept press-less clicks (`detail === 0` or virtual click) so keyboard and assistive-technology activations outside the popup dismiss it (FLT-PTR-28).
  Source: `floating-ui-react/hooks/useDismiss.ts:489-501`. Test: [L3] `useDismiss.test.tsx` "keyboard-generated outside click without a prior press closes".
- **FLT-A11Y-06** — MUST. On Mac Safari (`platform.os.mac && platform.engine.webkit`), `useFocus` MUST track modality with capture-phase window `keydown` (keyboard) / `pointerdown` (pointer) listeners, and a focus with no `relatedTarget` (focus entering from outside the document) MUST open only if the last modality was keyboard or the target is typeable — because Safari does not match `:focus-visible` in that case.
  Source: `floating-ui-react/hooks/useFocus.ts:23, 80-91, 139-149`. Test: none.
- **FLT-A11Y-07** — MUST. Outside-content hiding for modal popups MUST use `aria-hidden` (not `inert`) and MUST keep live regions and their ancestors exposed (FLT-ARIA-01); the focus guard MUST take `role="button"` under VoiceOver/WebKit so the virtual cursor can land on it (FLT-DOM-13).
  Source: `floating-ui-react/utils/markOthers.ts:102-112`; `utils/FocusGuard.tsx:16-24`. Test: [L3] `FloatingFocusManager.test.tsx` "true - applies aria-hidden to outside nodes".
- **FLT-A11Y-08** — MUST. The shared focus-visible predicate `matchesFocusVisible(element)` MUST return `true` when `element` is null or the environment is jsdom (which does not match `:focus-visible` on a focused element); otherwise it MUST return `element.matches(':focus-visible')`, and `true` when that call throws (an engine without `:focus-visible` support). It is the predicate behind FLT-FOC-01's "ignore non-focus-visible focus" rule and is shared by component code that needs the same heuristic (Toast re-exports it as `isFocusVisible`; Slider uses it for thumb focus).
  Source: `floating-ui-react/utils/element.ts:71-82`; call sites `floating-ui-react/hooks/useFocus.ts:146`, `toast/utils/focusVisible.ts:1`, `slider/thumb/SliderThumb.tsx:429`. Test: none (jsdom always takes the `true` branch).

### 4.15 EDGE — edge cases and defensive behavior

- **FLT-EDGE-01** — SHOULD. Outside production, creating a standalone root store with `elements.reference` set to a non-DOM (virtual) object MUST log an error with exactly these three arguments: `'Cannot pass a virtual element to the \`elements.reference\` option,'`, `'as it must be a real DOM element. Use \`context.setPositionReference()\`'`, `'instead.'`.
  Source: `floating-ui-react/hooks/useFloatingRootContext.ts:30-39`. Test: none.
- **FLT-EDGE-02** — MUST. The engine MUST honor at most 50 middleware-requested resets per `computePosition` call; further resets are ignored and the loop proceeds to the next middleware.
  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:127, 186-205`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): the engine's (`computePosition`'s reset limit), satisfied by the dependency, neither re-implemented nor re-tested.
- **FLT-EDGE-03** — MUST. Anchor tracking MUST avoid resize feedback loops: when a resize notification is for the anchor, the floating element MUST be temporarily unobserved and re-observed on the next animation frame before `update()` runs.
  Source: `vendor/@floating-ui_dom/dist/floating-ui.dom.mjs:641-655`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): the engine's (`autoUpdate`'s resize-feedback avoidance), satisfied by the dependency, neither re-implemented nor re-tested.
- **FLT-EDGE-04** — MUST. Layout-shift observation (FLT-ALG-13) MUST throttle the clipped-anchor case: when the first observation reports an intersection ratio of 0, the observer is rebuilt only after 1000 ms with a threshold of `1e-7`.
  Source: `vendor/@floating-ui_dom/dist/floating-ui.dom.mjs:569-583`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): the engine's (`autoUpdate`'s layout-shift throttle), satisfied by the dependency, neither re-implemented nor re-tested.
- **FLT-EDGE-05** — MUST. Outside-marking bookkeeping (FLT-ALG-19) MUST be global per page: counters are kept per element across all open popups and reset only when the number of active locks returns to zero; an `aria-hidden` that existed before the first lock on an element MUST be preserved on release, and once all locks release a later lock MUST re-read the element's current `aria-hidden` to decide ownership again.
  Source: `floating-ui-react/utils/markOthers.ts:15-18, 116-128, 147-180`. Test: [L3] `markOthers.test.ts` "multiple calls", "out of order cleanup", "keeps aria-hidden until the last aria-hidden lock releases, alongside marker-only locks", "re-reads externally owned aria-hidden once every lock has released", "re-reads externally owned aria-hidden while an unrelated lock stays held", "does not let mark-only overlap disturb control cleanup bookkeeping".
- **FLT-EDGE-06** — MUST. The previously-focused stack (FLT-FOC-09) MUST hold at most 20 weakly referenced entries, drop disconnected entries whenever it is read or written, and additionally prune disconnected entries in a microtask whenever a focus manager is disabled or its floating focus element changes.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:78-100, 992-1001`. Test: [L3] `FloatingFocusManager.test.tsx` "returns focus to last connected element".
- **FLT-EDGE-07** — MUST. When the return-focus arming is torn down and re-armed in the same commit while the popup stays open (e.g. the DOM anchor changed), the queued return-focus job MUST be cancelled so focus is not moved mid-open, and the suppression flag MUST still be cleared so the real close returns focus normally.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:773-786, 902-937`. Test: [L3] `FloatingFocusManager.test.tsx` "does not return focus while open when the reference changes".
- **FLT-EDGE-08** — MUST. Kept-mounted popups MUST not leak interaction state between open sessions: the recorded close type and last interaction type reset on each open (FLT-FOC-06); the outside-pointer-down flag resets when the manager closes (FLT-FOC-13); the dismiss hook's observed press resets on `openchange(false)` (FLT-DIS-14).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:685-686, 406-407`; `useDismiss.ts:279-285`. Test: [L3] `FloatingFocusManager.test.tsx` "resets close modality between keep-mounted open sessions", "clears outside pointer state between keep-mounted open sessions", "preserves keyboard close modality when reopening before focus restoration".
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): "resets close modality between keep-mounted open sessions" and "preserves keyboard close modality when reopening before focus restoration" are restated at L3 (FLT-FOC-10's note); "clears outside pointer state between keep-mounted open sessions" is green at L3 2026-10-28 (after track 1 merged; it renders `FloatingPortal`): Base UI reads the inside marker, the port observes it through the focus-out close it suppresses (red with the recording cleanup's clear removed). The marker moves to the interactions session with track 3.
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the inside marker is the session's `dataRef` when a session is joined; the title observes it the same way.
- **FLT-EDGE-09** — MUST. `useClientPoint` MUST build the virtual anchor around the handler's element when the root store has no DOM anchor yet, so cursor anchoring works before the trigger registers.
  Source: `floating-ui-react/hooks/useClientPoint.ts:137-146, 150-160`. Test: [L3] `useClientPoint.test.tsx` "uses trigger element when dom reference is missing".
- **FLT-EDGE-10** — MUST. Delegated hover mode (handlers on a wrapper that contains the triggers): the trigger node for the open is the registered trigger containing the event target; an event whose target is inside the active anchor MUST NOT be classified as "over an inactive trigger" even when the handler's element is the wrapper, while a target inside a *different* registered trigger (including a disabled one) MUST be.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:116-140, 228-257`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "does not treat child target as inactive when handlers are on a wrapper", "does not treat a synthetic child target as inactive when the native path differs", "treats disabled child trigger as inactive in wrapper fallback mode".
- **FLT-EDGE-11** — MUST. Delay group membership changes: unmounting a member that is not the active one MUST leave the group's active id, stored callbacks and current delay untouched; unmounting the member that last closed MUST keep the pending `timeoutMs` reset timer running (the group stays "warm"); unmounting the active, still-open member MUST clear the active id, reset the delay and cancel the timer.
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:253-267`. Test: [L3] `FloatingDelayGroup.test.tsx` "keeps the active context when an inactive consumer unmounts", "keeps the timeout active when the last closed consumer unmounts".
- **FLT-EDGE-12** — MUST. Changing a delay group's `delay` prop while a member is active MUST keep the group's open delay at 0 and adopt the new close delay; with no active member it MUST replace the current delay wholesale; the new value always becomes the "initial" delay restored on reset.
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:73-85`. Test: none.
- **FLT-EDGE-13** — MUST. Each invocation of a safe-polygon strategy (`safePolygon()(context)`) MUST start with fresh traversal state (`hasLanded`, last cursor sample); state from a previous `mouseleave` session MUST NOT carry over.
  Source: `floating-ui-react/safePolygon.ts:94-99`. Test: [L3] `safePolygon.test.ts` "resets traversal state for a new handler invocation".
- **FLT-EDGE-14** — MUST. Visually hidden elements (clip-path/1px/overflow hidden technique, FLT-DOM-13 style) MUST remain focusable and tabbable; `visibility: hidden`, `display: none` ancestors, `hidden` attribute and `inert` ancestors exclude an element (FLT-ALG-18).
  Source: `floating-ui-react/utils/tabbable.ts:59-95`; `floating-ui-react/utils/composite.ts:560-573`. Test: [L3] `tabbable.test.ts` "keeps visuallyHidden elements in the tab order", "keeps visuallyHiddenInput elements in the tab order", "excludes elements hidden with CSS visibility from the tab order", "excludes descendants of display: none ancestors from the tab order", "treats slotted elements inside inert shadow content as untabbable".

## 5. Algorithms

Notation: `rects.reference`/`rects.floating` are `{ x, y, width, height }`; a *placement* `p` has `side(p)`, `alignment(p)` (`start`, `end` or none), `sideAxis(p)` (`'y'` for top/bottom, `'x'` for left/right), `alignmentAxis(p)` (the other axis), `axisLength(a)` (`'height'` for `'y'`, `'width'` for `'x'`), `oppositePlacement(p)` (side mirrored, alignment kept), `oppositeAlignmentPlacement(p)` (`start`↔`end`). `overflow` objects are `{ top, right, bottom, left }` in px where a positive value is the amount by which the element exceeds the boundary on that side (negative = slack). `clamp(min, v, max)`.

### 5.1 Positioning engine (external, transcribed from the vendored `@floating-ui` 1.8.0 builds)

Port status: FLT-ALG-01..08 are satisfied by bundling the vendored `@floating-ui/dom` 1.8.0 engine (with `@floating-ui/core` 1.8.0 and `@floating-ui/utils` 0.2.12) into the port's JavaScript `positioning` module; they are not re-implemented or re-tested, and this section is review material for engine upgrades. Base UI's own middleware FLT-ALG-09..12, the anchor tracking FLT-ALG-13 and the pipeline assembly FLT-ALG-14 (§5.2) are ported in JavaScript. Port: see decisions/03-positioning-engine.md §Decision.

- **FLT-ALG-01** — MUST. The implementation reproduces the following procedure exactly (`computePosition`):

  ```
  computePosition(reference, floating, { placement = 'bottom', strategy = 'absolute', middleware = [] }):
    rtl   = platform.isRTL(floating)                       // computed direction === 'rtl'
    rects = platform.getElementRects({ reference, floating, strategy })
          // reference: rect of the (virtual) anchor relative to the floating element's
          //            offset parent (strategy 'absolute') or the viewport (strategy 'fixed'),
          //            scale-corrected; floating: { x: 0, y: 0, width, height } of the floating element
    (x, y) = coordsFromPlacement(rects, placement, rtl)   // FLT-ALG-02
    statefulPlacement = placement; resetCount = 0; middlewareData = {}
    i = 0
    while i < middleware.length:
      m = middleware[i]; if m is null: i += 1; continue
      result = m.fn({ x, y, initialPlacement: placement, placement: statefulPlacement, strategy,
                      middlewareData, rects, platform, elements: { reference, floating } })
      x = result.x ?? x; y = result.y ?? y
      middlewareData[m.name] = merge(middlewareData[m.name], result.data)
      if result.reset and resetCount < 50:
        resetCount += 1
        if result.reset is an object:
          if reset.placement: statefulPlacement = reset.placement
          if reset.rects: rects = (reset.rects === true) ? platform.getElementRects(...) : reset.rects
          (x, y) = coordsFromPlacement(rects, statefulPlacement, rtl)
        i = 0; continue                                    // restart from the first middleware
      i += 1
    return { x, y, placement: statefulPlacement, strategy, middlewareData }
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:128-214`; `vendor/@floating-ui_dom/dist/floating-ui.dom.mjs:480-493, 495-510`. Test: none (exercised by every positioner test).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`.
- **FLT-ALG-02** — MUST. The implementation reproduces the following procedure exactly (`coordsFromPlacement`):

  ```
  coordsFromPlacement(rects, p, rtl):
    r = rects.reference; f = rects.floating
    commonX = r.x + r.width/2 - f.width/2
    commonY = r.y + r.height/2 - f.height/2
    commonAlign = r[axisLength(alignmentAxis(p))]/2 - f[axisLength(alignmentAxis(p))]/2
    switch side(p):
      'top':    c = { x: commonX, y: r.y - f.height }
      'bottom': c = { x: commonX, y: r.y + r.height }
      'right':  c = { x: r.x + r.width, y: commonY }
      'left':   c = { x: r.x - f.width, y: commonY }
    if alignment(p):
      c[alignmentAxis(p)] += commonAlign * (alignment(p) == 'end' ? 1 : -1) * (rtl and sideAxis(p) == 'y' ? -1 : 1)
    return c
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:4-54`. Test: [L3] `test/positionerConformanceTests.tsx` "uses transform positioning without Viewport" (`x = (72 - 52) / 2 = 10`, `y = 36`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`. The cited title still runs, in the L4 suite on `FixturePositioner`.
- **FLT-ALG-03** — MUST. The implementation reproduces the following procedure exactly (`detectOverflow`), including the boundary resolution of the DOM platform:

  ```
  detectOverflow(state, { boundary = 'clippingAncestors', rootBoundary = 'viewport',
                          elementContext = 'floating', altBoundary = false, padding = 0 }):
    pad = paddingObject(padding)                               // number → all four sides
    el  = elements[altBoundary ? other(elementContext) : elementContext]
    clipEl = isElement(el) ? el : (el.contextElement ?? documentElement(floating))
    clip = clientRect(platform.getClippingRect({ element: clipEl, boundary, rootBoundary, strategy }))
    rect = elementContext == 'floating' ? { x: state.x, y: state.y, width: rects.floating.width,
                                            height: rects.floating.height } : rects.reference
    offsetParent = platform.getOffsetParent(floating)
    scale = isElement(offsetParent) ? platform.getScale(offsetParent) : { x: 1, y: 1 }
    elRect = clientRect(platform.convertOffsetParentRelativeRectToViewportRelativeRect(
               { elements, rect, offsetParent, strategy }))
    return { top:    (clip.top - elRect.top + pad.top) / scale.y,
             bottom: (elRect.bottom - clip.bottom + pad.bottom) / scale.y,
             left:   (clip.left - elRect.left + pad.left) / scale.x,
             right:  (elRect.right - clip.right + pad.right) / scale.x }

  getClippingRect({ element, boundary, rootBoundary, strategy }):
    ancestors = boundary == 'clippingAncestors'
                  ? (isTopLayer(element) ? [] : clippingElementAncestors(element))
                  : asArray(boundary)
    ancestors = [...ancestors, rootBoundary]
    acc = clientRectOf(ancestors[0])
    for a in ancestors[1..]: r = clientRectOf(a); acc.top = max(r.top, acc.top); acc.right = min(r.right, acc.right)
                              acc.bottom = min(r.bottom, acc.bottom); acc.left = max(r.left, acc.left)
    return { x: acc.left, y: acc.top, width: acc.right - acc.left, height: acc.bottom - acc.top }
    where clientRectOf(a):
      'viewport' | 'layoutViewport' → viewportRect(element, strategy, a)
      'document'                     → the document's full scrollable rect
      Element                        → its padding box in viewport coordinates
      Rect                           → the rect minus the visual-viewport offsets
    clippingElementAncestors(element): the overflow ancestors of `element` (elements whose
      overflow/overflow-x/overflow-y contains auto|scroll|overlay|hidden|clip and whose display is not
      inline/contents, walking up through shadow hosts and slots; the body is excluded), minus every
      ancestor that cannot clip the element because the chain below it escapes it: walking from the
      element (or its parent when the element is position:fixed) up to html, a non-containing-block
      ancestor is dropped when the last kept position is 'fixed', or when it is 'absolute' and the
      ancestor is position:static. "Containing block" = non-none transform/translate/scale/rotate/
      perspective, (non-WebKit) filter/backdrop-filter, will-change naming one of those, or contain
      paint|layout|strict|content.
    viewportRect(element, strategy, kind): width/height = html.clientWidth/clientHeight, x = y = 0;
      with a visualViewport: for kind 'viewport' use visualViewport.width/height and, when client
      coordinates are layout-relative (not WebKit, or strategy 'fixed'), x/y = visualViewport.offsetLeft/
      offsetTop; for kind 'layoutViewport' keep the layout size and, only when coordinates are
      visual-relative (WebKit with 'absolute'), x/y = -offsetLeft/-offsetTop. Then subtract a reserved
      `scrollbar-gutter` width (≤ 25 px) when the window scrollbar is on the right.
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:64-116`; `vendor/@floating-ui_dom/dist/floating-ui.dom.mjs:218-274, 293-311, 316-349, 353-380`; `vendor/@floating-ui_utils/dist/floating-ui.utils.dom.mjs:45-53, 57-70, 75-81, 118-160`. Test: [L3] `utils/hideMiddleware.test.ts` (mocks `platform.detectOverflow`).
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`. The cited title still runs: the engine's `hide()` is the reference of the L3 `hideMiddleware` titles.
- **FLT-ALG-04** — MUST. The implementation reproduces the following procedure exactly (`offset` middleware; Base UI always passes an object):

  ```
  offset.fn(state, options):
    rtl = platform.isRTL(floating); p = state.placement
    mainAxisMulti  = side(p) in {left, top} ? -1 : 1
    crossAxisMulti = (rtl and sideAxis(p) == 'y') ? -1 : 1
    raw = evaluate(options, state)                    // Base UI: { mainAxis, crossAxis, alignmentAxis }
    mainAxis = raw.mainAxis || 0; crossAxis = raw.crossAxis || 0; alignmentAxis = raw.alignmentAxis
    if alignment(p) and alignmentAxis is a number:
      crossAxis = alignment(p) == 'end' ? -alignmentAxis : alignmentAxis
    diff = sideAxis(p) == 'y' ? { x: crossAxis * crossAxisMulti, y: mainAxis * mainAxisMulti }
                              : { x: mainAxis * mainAxisMulti,  y: crossAxis * crossAxisMulti }
    if p == middlewareData.offset?.placement and middlewareData.arrow?.alignmentOffset: return {}
    return { x: state.x + diff.x, y: state.y + diff.y, data: { ...diff, placement: p } }
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:726, 731-769, 778-810`. Test: [L3] `test/positionerConformanceTests.tsx` "prop: sideOffset › offsets the positioner when a number is specified", "prop: alignOffset › offsets the positioner when a number is specified".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`. The cited titles still run, in the L4 suite on `FixturePositioner`.
- **FLT-ALG-05** — MUST. The implementation reproduces the following procedure exactly (`flip` middleware):

  ```
  flip.fn(state, { mainAxis = true, crossAxis = true, fallbackPlacements?, fallbackStrategy = 'bestFit',
                   fallbackAxisSideDirection = 'none', flipAlignment = true, ...overflowOptions }):
    if middlewareData.arrow?.alignmentOffset: return {}
    p = state.placement; initial = state.initialPlacement
    initialSideAxis = sideAxis(initial); isBase = alignment(initial) is none
    rtl = platform.isRTL(floating)
    fallbacks = fallbackPlacements ?? ((isBase or not flipAlignment) ? [oppositePlacement(initial)]
                                                                     : expandedPlacements(initial))
      // expandedPlacements(p) = [oppositeAlignmentPlacement(p), oppositePlacement(p),
      //                          oppositeAlignmentPlacement(oppositePlacement(p))]
    if fallbackPlacements is absent and fallbackAxisSideDirection != 'none':
      fallbacks += oppositeAxisPlacements(initial, flipAlignment, fallbackAxisSideDirection, rtl)
      // oppositeAxisPlacements(p, flipAlignment, dir, rtl):
      //   list = sideList(side(p), dir == 'start', rtl)
      //     sideList: top/bottom → rtl ? (isStart ? [right,left] : [left,right])
      //                               : (isStart ? [left,right] : [right,left])
      //               left/right → isStart ? [top,bottom] : [bottom,top]
      //   if alignment(p): list = list.map(s → s + '-' + alignment(p));
      //                    if flipAlignment: list += list.map(oppositeAlignmentPlacement)
    placements = [initial, ...fallbacks]
    overflow = detectOverflow(state, overflowOptions)
    overflows = []
    if mainAxis: overflows.push(overflow[side(p)])
    if crossAxis: [s0, s1] = alignmentSides(p, rects, rtl); overflows.push(overflow[s0], overflow[s1])
      // alignmentSides(p, rects, rtl): axis = alignmentAxis(p); len = axisLength(axis)
      //   main = axis == 'x' ? (alignment(p) == (rtl ? 'end' : 'start') ? 'right' : 'left')
      //                      : (alignment(p) == 'start' ? 'bottom' : 'top')
      //   if rects.reference[len] > rects.floating[len]: main = opposite side of main
      //   return [main, opposite side of main]
    overflowsData = [...(middlewareData.flip?.overflows ?? []), { placement: p, overflows }]
    if not every(overflows, v → v <= 0):
      nextIndex = (middlewareData.flip?.index ?? 0) + 1
      next = placements[nextIndex]
      if next:
        ignoreCrossAxisOverflow = (crossAxis == 'alignment') ? initialSideAxis != sideAxis(next) : false
        if not ignoreCrossAxisOverflow
           or every(overflowsData, d → sideAxis(d.placement) == initialSideAxis ? d.overflows[0] > 0 : true):
          return { data: { index: nextIndex, overflows: overflowsData }, reset: { placement: next } }
      resetPlacement = (overflowsData.filter(d → d.overflows[0] <= 0)
                                     .sortBy(d → d.overflows[1]))[0]?.placement
      if not resetPlacement:
        if fallbackStrategy == 'bestFit':
          best = overflowsData
                   .filter(d → fallbackAxisSideDirection != 'none'
                               ? (sideAxis(d.placement) == initialSideAxis or sideAxis(d.placement) == 'y')
                               : true)
                   .map(d → [d.placement, sum(d.overflows.filter(v → v > 0))])
                   .sortBy(pair → pair[1])[0]?.[0]
          if best: resetPlacement = best
        else if fallbackStrategy == 'initialPlacement': resetPlacement = initial
      if p != resetPlacement: return { reset: { placement: resetPlacement } }
    return {}
  ```

  Note that `crossAxis: 'alignment'` (Base UI's setting for `align: 'flip'`) only lets alignment overflow move the search to the *next placement on the same side axis*; a move to the perpendicular axis requires main-axis overflow on every same-axis placement tried so far.
  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:406-530`; `vendor/@floating-ui_utils/dist/floating-ui.utils.mjs:48-98`. Test: [L3] `useAnchorPositioning.test.tsx` "flips back to the preferred side without lazy flipping"; `test/positionerConformanceTests.tsx` "reads the latest side after a flip inside the function", "reads the latest align after a flip inside the function".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`. The cited titles still run, in the L4 suite and in the page ("flips back to the preferred side without lazy flipping").
- **FLT-ALG-06** — MUST. The implementation reproduces the following procedure exactly (`shift` middleware; note that here "main axis" is the *alignment* axis and "cross axis" is the *side* axis):

  ```
  shift.fn(state, { mainAxis = true, crossAxis = false, limiter = identity, ...overflowOptions }):
    overflow = detectOverflow(state, overflowOptions)
    cross = sideAxis(placement); main = other(cross)
    mainCoord = state[main]; crossCoord = state[cross]
    clampCoord(axis, c) = clamp(c + overflow[axis == 'y' ? 'top' : 'left'], c, c - overflow[axis == 'y' ? 'bottom' : 'right'])
    if mainAxis:  mainCoord  = clampCoord(main, mainCoord)
    if crossAxis: crossCoord = clampCoord(cross, crossCoord)
    limited = limiter.fn({ ...state, [main]: mainCoord, [cross]: crossCoord })
    return { ...limited, data: { x: limited.x - state.x, y: limited.y - state.y,
                                 enabled: { [main]: mainAxis, [cross]: crossAxis } } }
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:817-882`. Test: [L3] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`. The cited titles still run, in the L4 suite on `FixturePositioner`.
- **FLT-ALG-07** — MUST. The implementation reproduces the following procedure exactly (`limitShift` limiter):

  ```
  limitShift.fn(state, { offset = 0, mainAxis = true, crossAxis = true }):
    cross = sideAxis(placement); main = other(cross)
    mainCoord = state[main]; crossCoord = state[cross]
    o = evaluate(offset, state); off = o is number ? { mainAxis: o, crossAxis: 0 } : { mainAxis: o.mainAxis ?? 0, crossAxis: o.crossAxis ?? 0 }
    if mainAxis:
      len = main == 'y' ? 'height' : 'width'
      lo = rects.reference[main] - rects.floating[len] + off.mainAxis
      hi = rects.reference[main] + rects.reference[len] - off.mainAxis
      mainCoord = mainCoord < lo ? lo : (mainCoord > hi ? hi : mainCoord)
    if crossAxis:
      len = main == 'y' ? 'width' : 'height'
      isOrigin = side(placement) in {left, top}
      lo = rects.reference[cross] - rects.floating[len] + (isOrigin ? (middlewareData.offset?.[cross] ?? 0) : 0) + (isOrigin ? 0 : off.crossAxis)
      hi = rects.reference[cross] + rects.reference[len] + (isOrigin ? 0 : (middlewareData.offset?.[cross] ?? 0)) - (isOrigin ? off.crossAxis : 0)
      crossCoord = crossCoord < lo ? lo : (crossCoord > hi ? hi : crossCoord)
    return { [main]: mainCoord, [cross]: crossCoord }
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:886-950`. Test: none.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`.
- **FLT-ALG-08** — MUST. The implementation reproduces the following procedure exactly (`size` middleware):

  ```
  size.fn(state, { apply, ...overflowOptions }):
    overflow = detectOverflow(state, overflowOptions)
    p = placement; { width, height } = rects.floating
    if side(p) in {top, bottom}:
      heightSide = side(p)
      widthSide  = alignment(p) == (platform.isRTL(floating) ? 'start' : 'end') ? 'left' : 'right'
    else:
      widthSide  = side(p)
      heightSide = alignment(p) == 'end' ? 'top' : 'bottom'
    maxClipH = height - overflow.top - overflow.bottom
    maxClipW = width  - overflow.left - overflow.right
    availH = min(height - overflow[heightSide], maxClipH)
    availW = min(width  - overflow[widthSide],  maxClipW)
    shiftData = middlewareData.shift
    if shiftData?.enabled.x: availW = maxClipW
    if shiftData?.enabled.y: availH = maxClipH
    if shiftData is absent and alignment(p) is none:
      if sideAxis(p) == 'y': availW = width  - 2 * max(overflow.left, overflow.right)
      else:                  availH = height - 2 * max(overflow.top,  overflow.bottom)
    apply({ ...state, availableWidth: availW, availableHeight: availH })
    next = platform.getDimensions(floating)
    if width != next.width or height != next.height: return { reset: { rects: true } }
    return {}
  ```

  Source: `vendor/@floating-ui_core/dist/floating-ui.core.mjs:964-1036`. Test: [L3] `test/positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): satisfied by the dependency (ADR 03: "FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested"; the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): the bundled `@floating-ui/dom` 1.8.0 runs it inside `computePosition`, behind `js/floating-ui.ts`. The cited title still runs, in the L4 suite on `FixturePositioner`.

### 5.2 Base UI middleware and anchor tracking

- **FLT-ALG-09** — MUST. The implementation reproduces the following procedure exactly (Base UI's `arrow` fork, which always measures against the floating element rather than the arrow's offset parent):

  ```
  arrow.fn(state, { element, padding = 0 }):
    if element is null: return {}
    pad = paddingObject(padding)
    axis = alignmentAxis(placement); len = axisLength(axis); isY = axis == 'y'
    minProp = isY ? 'top' : 'left'; maxProp = isY ? 'bottom' : 'right'
    arrowDim = platform.getDimensions(element)
    endDiff   = rects.reference[len] + rects.reference[axis] - state[axis] - rects.floating[len]
    startDiff = state[axis] - rects.reference[axis]
    clientSize = floating[isY ? 'clientHeight' : 'clientWidth'] || rects.floating[len]
    centerToReference = endDiff/2 - startDiff/2
    largestPossiblePadding = clientSize/2 - arrowDim[len]/2 - 1
    minPadding = min(pad[minProp], largestPossiblePadding)
    maxPadding = min(pad[maxProp], largestPossiblePadding)
    lo = minPadding; hi = clientSize - arrowDim[len] - maxPadding
    center = clientSize/2 - arrowDim[len]/2 + centerToReference
    offset = clamp(lo, center, hi)
    shouldAddOffset = middlewareData.arrow is absent and alignment(placement) is present
                      and center != offset
                      and rects.reference[len]/2 - (center < lo ? minPadding : maxPadding) - arrowDim[len]/2 < 0
    alignmentOffset = shouldAddOffset ? (center < lo ? center - lo : center - hi) : 0
    return { [axis]: state[axis] + alignmentOffset,
             data: { [axis]: offset, centerOffset: center - offset - alignmentOffset,
                     ...(shouldAddOffset ? { alignmentOffset } : {}) },
             reset: shouldAddOffset }
  ```

  `data.x`/`data.y` become the Arrow part's `left`/`top` (FLT-DOM-09); `centerOffset !== 0` means the arrow could not point at the anchor's center (`data-uncentered`); `alignmentOffset` nudges a small-anchor aligned popup so the arrow still points at it and triggers one restart.
  Source: `floating-ui-react/middleware/arrow.ts:29-97, 105-115` (fork of `vendor/@floating-ui_core/dist/floating-ui.core.mjs:221-295`). Test: none. Docs: `CHANGELOG.md` "Remove duplicate options key from arrow middleware (#5606)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118)): port titles at L3, `src/Blazix.Primitives/js/positioning.test.ts` "centers the arrow on the anchor, measured against the floating element", "clamps the arrow to the padding and reports the offset from the center", "nudges an aligned popup once so that the arrow still points at a small anchor" and "returns nothing without an element" (the `.fn(state)` over synthetic states, the `hide` test's technique).
- **FLT-ALG-10** — MUST. The implementation reproduces the following procedure exactly (Base UI's `hide` replacement):

  ```
  hide.fn(state):
    r = rects.reference
    anchorHidden = r.width == 0 and r.height == 0 and r.x == 0 and r.y == 0
    overflow = detectOverflow(state, { elementContext: 'reference' })     // exactly one call, default boundary/padding
    referenceHidden = overflow.top - r.height >= 0 or overflow.right - r.width >= 0
                   or overflow.bottom - r.height >= 0 or overflow.left - r.width >= 0
    return { data: { referenceHidden: referenceHidden or anchorHidden } }
  ```

  Source: `utils/hideMiddleware.ts:3-25` (mirrors `vendor/@floating-ui_core/dist/floating-ui.core.mjs:532-542, 565-578`). Test: [L3] `utils/hideMiddleware.test.ts` "matches Floating UI hide() for reference overflow %#", "treats an all-zero reference rect as hidden".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): green at L3, the `hideMiddleware.test.ts` titles verbatim against the engine's own `hide()`.
- **FLT-ALG-11** — MUST. The implementation reproduces the following procedure exactly (`transformOrigin` middleware; runs after `arrow`, before `hide`):

  ```
  transformOrigin.fn(state):
    renderedSide = side(placement); renderedAlign = alignment(placement)
    isVertical = sideAxis(placement) == 'y'
    arrowEl = registered Arrow element or null
    sideOffsetValue = sideOffset is function ? sideOffset(offsetData(state)) : sideOffset   // FLT-API-12
    // cross axis
    shiftAmount = isVertical ? (middlewareData.shift?.x ?? 0) : (middlewareData.shift?.y ?? 0)
    if arrowEl is null and renderedAlign is present and |shiftAmount| <= 1:
      // aligned, arrowless, not shifted beyond subpixel: grow from the aligned edge
      crossOrigin = ((renderedAlign == 'start') == (isVertical and platform.isRTL(floating) == true)) ? '100%' : '0%'
    else:
      arrowOffset = isVertical ? (middlewareData.arrow?.x ?? 0) : (middlewareData.arrow?.y ?? 0)
      arrowSize   = isVertical ? (arrowEl?.clientWidth ?? 0) : (arrowEl?.clientHeight ?? 0)
      crossOrigin = `${arrowOffset + arrowSize / 2}px`
    // side axis: the anchor-facing edge, or the anchor's center when the popup overlaps it
    sideOrigin = (renderedSide in {top, left}) ? `calc(100% + ${sideOffsetValue}px)` : `${-sideOffsetValue}px`
    if crossAxisShiftEnabled and isVertical and |middlewareData.shift?.y ?? 0| > sideOffsetValue:
      sideOrigin = `${rects.reference.y + rects.reference.height / 2 - state.y}px`
    floating.style['--transform-origin'] = isVertical ? `${crossOrigin} ${sideOrigin}` : `${sideOrigin} ${crossOrigin}`
    return {}
  ```

  where `crossAxisShiftEnabled` is the side-axis shift flag of FLT-POS-05.
  Source: `internals/useAnchorPositioning.ts:391-453`. Test: [L4] `popover/positioner/PopoverPositioner.test.tsx` "transform origin" (19 tests: "points to the anchor center for center alignment", "points to the popup start edge for start alignment", "points to the popup logical start edge for start alignment in RTL", "uses the floating element direction for RTL alignment", "points to the popup end edge for end alignment", "points to the popup logical end edge for end alignment in RTL", "places the side coordinate first for horizontal sides", "points to the popup end edge for horizontal end alignment", "accounts for sideOffset", "accounts for sideOffset on top side", "keeps the popup start edge as the origin with alignOffset", "does not let the virtual arrow displace the popup on a narrow anchor", "points to the flipped edge when the alignment flips", "keeps the popup start edge as the origin for a 1px cross-axis shift", "points to the anchor center when start alignment is shifted on the cross axis", "points to the anchor center when end alignment is shifted on the cross axis", "points to the anchor center when horizontal alignment is shifted on the cross axis", "points to the arrow when present regardless of alignment", "points to the anchor center when the popup is shifted to overlap the anchor"). Docs: `docs/src/app/(docs)/react/handbook/animation/page.mdx:16-35`.
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3)): re-tagged [L3] → [L4], the 19 titles being browser-only in Base UI; they run verbatim in `tests/browser/positioning.spec.ts` on `FixturePositioner` (`positioning-origin`), both modes. The `.fn(state)` also runs over synthetic states at L3 ("points to the arrow, real or stand-in…", "grows an aligned arrowless popup…", "points to the anchor center on the side axis…").
- **FLT-ALG-12** — MUST. The implementation reproduces the following procedure exactly (`adaptiveOrigin` middleware, supplied by PSH `usePopupViewport` and run last):

  ```
  adaptiveOrigin.fn(state):
    styles = computedStyle(floating)
    hasTransition = styles.transitionDuration != '0s' and styles.transitionDuration != ''
    if not hasTransition: return { x: state.x, y: state.y, data: { sideX: 'left', sideY: 'top' } }
    offsetParent = platform.getOffsetParent(floating)
    dims = { width: 0, height: 0 }
    if strategy == 'fixed' and window.visualViewport: dims = visualViewport { width, height }
    else if offsetParent is the window: dims = documentElement { clientWidth, clientHeight }
    else if offsetParent is an element: dims = platform.getDimensions(offsetParent)
    x = state.x; y = state.y
    if side(placement) == 'left': x = dims.width  - (state.x + rects.floating.width)
    if side(placement) == 'top':  y = dims.height - (state.y + rects.floating.height)
    return { x, y, data: { sideX: side == 'left' ? 'right' : 'left', sideY: side == 'top' ? 'bottom' : 'top' } }
  ```

  The returned `x`/`y` are then rendered as `[sideX]: x; [sideY]: y` (FLT-DOM-04).
  Source: `utils/adaptiveOriginMiddleware.ts:6-73`; `utils/adaptiveOriginConstants.ts:1-4`. Test: [L4] `test/positionerConformanceTests.tsx` "uses top/left positioning with Viewport".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); the plan's settled list of 2026-10-27 ("jsdom-only FLT titles to L3, browser-only module titles at L4 in the page, FLT-ALG-01..08 satisfied by the dependency", settled by the orchestrator under ADR 06 §1 and ADR 13 §3); design point 8): re-tagged [L3] → [L4], the suite's Viewport titles being browser-only; they run over the `AdaptiveOrigin` stand-in in `tests/browser/suites/positioner-conformance.ts` on `FixturePositioner` (`positioning-suite`). The `.fn(state)` also runs over synthetic states at L3 ("keeps the plain coordinates while the floating element has no transition", "measures a left or top placement from the opposite edge…").
- **FLT-ALG-13** — MUST. The implementation reproduces the following procedure exactly (anchor tracking, `autoUpdate` + `observeMove`):

  ```
  autoUpdate(reference, floating, update, { ancestorScroll = true, ancestorResize = true,
             elementResize = ResizeObserver exists, layoutShift = IntersectionObserver exists, animationFrame = false }):
    referenceEl = isElement(reference) ? reference : reference.contextElement
    ancestors = (ancestorScroll or ancestorResize)
                ? overflowAncestors(referenceEl) ++ overflowAncestors(floating) : []
      // overflowAncestors(node): nearest overflow ancestor chain up to the body, then the window,
      // the visualViewport (if any), the body itself when it is an overflow element, and the
      // frame element's ancestors when inside an iframe
    for a in ancestors: if ancestorScroll: a.on('scroll', update); if ancestorResize: a.on('resize', update)
    cleanupMove = (referenceEl and layoutShift) ? observeMove(referenceEl, update, ancestorResize) : null
    if elementResize:
      ro = ResizeObserver(entries →
             if entries[0].target == referenceEl and floating:
               ro.unobserve(floating); nextFrame(() → ro.observe(floating))      // FLT-EDGE-03
             update())
      if referenceEl and not animationFrame: ro.observe(referenceEl)
      if floating: ro.observe(floating)
    if animationFrame: loop each frame: if reference's client rect changed since last frame: update()
    update()
    return cleanup (remove listeners, cleanupMove, ro.disconnect, stop frame loop)

  observeMove(element, onMove, ancestorResize):
    refresh(skip = false, threshold = 1):
      disconnect previous observer; cancel pending timer
      rect = element.getBoundingClientRect()
      if not skip: onMove()
      if rect.width == 0 or rect.height == 0: return
      rootMargin = `-${floor(rect.top)}px -${floor(root.clientWidth - (rect.left + rect.width))}px
                    -${floor(root.clientHeight - (rect.top + rect.height))}px -${floor(rect.left)}px`
      threshold = clamp(0, threshold, 1) || 1; isFirstUpdate = true
      observe element with IntersectionObserver(rootMargin, threshold, root = the document) → on entry:
        if element's client rect != rect: refresh()                     // moved since snapshot
        else if entry.intersectionRatio != threshold:
          if not isFirstUpdate: refresh()
          else if ratio == 0: after 1000 ms: refresh(false, 1e-7)           // FLT-EDGE-04
          else: refresh(false, ratio)
        isFirstUpdate = false
    window.on('resize', () → refresh(skip = ancestorResize))
    refresh(skip = true)
    return cleanup
  ```

  Server: see ADR 01 relaxation table.
  Source: `vendor/@floating-ui_dom/dist/floating-ui.dom.mjs:27-29, 512-514, 517-611, 621-690`; `vendor/@floating-ui_utils/dist/floating-ui.utils.dom.mjs:133-160`. Test: none. Docs: `CHANGELOG.md` "Fix disabled anchor tracking on scroll (#5478)".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 01 relaxation table, ADR 13 §1): met on both modes, asserted per mode by `tests/browser/positioning.spec.ts` "port: scrolling an ancestor of the anchor re-positions the Positioner in the same frame on both modes". `autoUpdate` itself is the engine's.
- **FLT-ALG-14** — MUST. The implementation reproduces the following procedure exactly (Base UI's pipeline assembly; it is the normative form of FLT-POS-02..08):

  ```
  assemble(params):
    sideP = params.side ?? 'bottom'; align = params.align ?? 'center'
    cs = params.collisionAvoidance.side ?? 'flip'; ca = params.collisionAvoidance.align ?? 'flip'
    fb = params.collisionAvoidance.fallbackAxisSide ?? 'end'
    shiftCross = params.shift?.crossAxis ?? false; sticky = params.sticky ?? false
    pad = normalizePadding(params.collisionPadding ?? 5)                       // FLT-API-08
    bias = 1; biasTop = sideP == 'bottom' ? 1 : 0; biasBottom = sideP == 'top' ? 1 : 0
    biasLeft = sideP == 'right' ? 1 : 0; biasRight = sideP == 'left' ? 1 : 0
    common = { boundary: params.collisionBoundary == 'clipping-ancestors' ? 'clippingAncestors' : params.collisionBoundary, padding: pad }
    mw = []
    if params.inline: mw.push(params.inline)
    mw.push(offset(state → { mainAxis: resolve(sideOffset, state), crossAxis: resolve(alignOffset, state), alignmentAxis: resolve(alignOffset, state) }))
    shiftDisabled = ca == 'none' and cs != 'shift'
    crossAxisShiftEnabled = not shiftDisabled and (sticky or shiftCross or cs == 'shift')
    flipMw = cs == 'none' ? null : flip({ ...common,
               padding: { top: pad.top + bias + biasTop, right: pad.right + bias + biasRight,
                          bottom: pad.bottom + bias + biasBottom, left: pad.left + bias + biasLeft },
               mainAxis: not shiftCross and cs == 'flip', crossAxis: ca == 'flip' ? 'alignment' : false,
               fallbackAxisSideDirection: fb })
    shiftMw = shiftDisabled ? null : shift({ ...common, rootBoundary: params.shift?.rootBoundary,
               mainAxis: ca != 'none', crossAxis: crossAxisShiftEnabled,
               limiter: (sticky or shiftCross) ? undefined : limitShift(state → arrowEl
                 ? { offset: arrowSize(arrowEl, state.placement) / 2 + padSum(pad, state.placement) / 2 } : {}) })
    if cs == 'shift' or ca == 'shift' or requestedAlign == 'center': mw.push(shiftMw, flipMw) else mw.push(flipMw, shiftMw)
    mw.push(size({ ...common, apply: writeCssVars }), arrow(state → { element: arrowEl ?? standInDiv(state), padding: arrowEl ? arrowPadding : 0 }),
            transformOrigin, hide, params.adaptiveOrigin)
    return mw   // null entries are skipped by the engine
  ```

  Source: `internals/useAnchorPositioning.ts:167-173, 207-239, 241-246, 257-457`. Test: [L3] `useAnchorPositioning.test.tsx` "uses the configured shift options".
  Port note (2026-10-27, M4 track 2, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03): green at L3, `src/Blazix.Primitives/js/positioning.test.ts` "rebuilds the pipeline only when one of its inputs changes (FLT-ALG-14, FLT-POS-17)" and the pipeline titles of FLT-POS-02..05.

### 5.3 Interaction algorithms

- **FLT-ALG-15** — MUST. The implementation reproduces the following procedure exactly (safe polygon; `safePolygon(options)` returns a strategy whose invocation with the close context returns a `mousemove` handler):

  ```
  constants: CURSOR_SPEED_THRESHOLD = 0.1 (px/ms), POLYGON_BUFFER = 0.5 (px), INTENT_TIMEOUT = 40 (ms)

  strategy({ x, y, placement, elements: { domReference, floating }, onClose, nodeId, tree }):
    side = side(placement); hasLanded = false; lastX = lastY = null; lastTime = now()
    isCursorMovingSlowly(nx, ny):
      t = now(); elapsed = t - lastTime
      if lastX is null or elapsed == 0: lastX = nx; lastY = ny; lastTime = t; return false
      d² = (nx - lastX)² + (ny - lastY)²; lastX = nx; lastY = ny; lastTime = t
      return d² < (elapsed * CURSOR_SPEED_THRESHOLD)²
    close(): cancel intent timer; onClose()
    return onMouseMove(event):
      cancel intent timer
      if domReference, floating, side, x or y is missing: return
      (cx, cy) = (event.clientX, event.clientY); target = composedTarget(event); isLeave = event.type == 'mouseleave'
      overFloating = contains(floating, target); overReference = contains(domReference, target)
      if overFloating: hasLanded = true; if not isLeave: return
      if overReference: hasLanded = false; if not isLeave: hasLanded = true; return
      if isLeave and event.relatedTarget is an element inside floating: return
      hasOpenChild() = tree exists and openDescendants(tree, nodeId) is non-empty          // FLT-ALG-20
      closeIfNoOpenChild() = if not hasOpenChild(): close()
      if hasOpenChild(): return
      refRect = domReference.getBoundingClientRect(); rect = floating.getBoundingClientRect()
      leaveFromRight  = x > rect.right  - rect.width  / 2
      leaveFromBottom = y > rect.bottom - rect.height / 2
      wider  = rect.width  > refRect.width;  taller = rect.height > refRect.height
      left = (wider ? refRect : rect).left; right = (wider ? refRect : rect).right
      top  = (taller ? refRect : rect).top; bottom = (taller ? refRect : rect).bottom
      // leaving from the side opposite the popup
      if (side == 'top' and y >= refRect.bottom - 1) or (side == 'bottom' and y <= refRect.top + 1)
         or (side == 'left' and x >= refRect.right - 1) or (side == 'right' and x <= refRect.left + 1):
        closeIfNoOpenChild(); return
      // rectangular trough between the two elements
      inTrough = switch side:
        'top':    inAxisAlignedRect(cx, cy, left, refRect.top + 1, right, rect.bottom - 1)
        'bottom': inAxisAlignedRect(cx, cy, left, rect.top + 1, right, refRect.bottom - 1)
        'left':   inAxisAlignedRect(cx, cy, rect.right - 1, bottom, refRect.left + 1, top)
        'right':  inAxisAlignedRect(cx, cy, refRect.right - 1, bottom, rect.left + 1, top)
        // inAxisAlignedRect(px, py, x1, y1, x2, y2) = min(x1,x2) <= px <= max(x1,x2) and min(y1,y2) <= py <= max(y1,y2)
      if inTrough: return
      if hasLanded and not insideRect(cx, cy, refRect): closeIfNoOpenChild(); return
      if not isLeave and isCursorMovingSlowly(cx, cy): closeIfNoOpenChild(); return
      // the quadrilateral from the leave point to the popup's far edge
      switch side:
        'top' / 'bottom':
          xOff = wider ? POLYGON_BUFFER / 2 : POLYGON_BUFFER * 4
          p1x = wider ? x + xOff : (leaveFromRight ? x + xOff : x - xOff)
          p2x = wider ? x - xOff : (leaveFromRight ? x + xOff : x - xOff)
          py  = side == 'top' ? y + POLYGON_BUFFER + 1 : y - POLYGON_BUFFER
          near = side == 'top' ? rect.bottom - POLYGON_BUFFER : rect.top + POLYGON_BUFFER
          far  = side == 'top' ? rect.top : rect.bottom
          yLeft  = leaveFromRight ? near : (wider ? near : far)
          yRight = leaveFromRight ? (wider ? near : far) : near
          inside = pointInQuad(cx, cy, p1x, py, p2x, py, rect.left, yLeft, rect.right, yRight)
        'left' / 'right':
          yOff = taller ? POLYGON_BUFFER / 2 : POLYGON_BUFFER * 4
          p1y = taller ? y + yOff : (leaveFromBottom ? y + yOff : y - yOff)
          p2y = taller ? y - yOff : (leaveFromBottom ? y + yOff : y - yOff)
          px  = side == 'left' ? x + POLYGON_BUFFER + 1 : x - POLYGON_BUFFER
          near = side == 'left' ? rect.right - POLYGON_BUFFER : rect.left + POLYGON_BUFFER
          far  = side == 'left' ? rect.left : rect.right
          xTop    = leaveFromBottom ? near : (taller ? near : far)
          xBottom = leaveFromBottom ? (taller ? near : far) : near
          inside = side == 'left' ? pointInQuad(cx, cy, xTop, rect.top, xBottom, rect.bottom, px, p1y, px, p2y)
                                  : pointInQuad(cx, cy, px, p1y, px, p2y, xTop, rect.top, xBottom, rect.bottom)
      if not inside: closeIfNoOpenChild()
      else if not hasLanded: start intent timer: after 40 ms closeIfNoOpenChild()

  pointInQuad(px, py, x1,y1, x2,y2, x3,y3, x4,y4): even-odd test over the four edges
    (x1,y1)-(x2,y2), (x2,y2)-(x3,y3), (x3,y3)-(x4,y4), (x4,y4)-(x1,y1), where an edge (xi,yi)-(xj,yj)
    counts when (yi >= py) != (yj >= py) and px <= (xj - xi) * (py - yi) / (yj - yi) + xi
  ```

  The strategy's `__options` is `{ ...options, blockPointerEvents }` with `blockPointerEvents` defaulting to `false` (FLT-PTR-19).
  Source: `floating-ui-react/safePolygon.ts:10-12, 14-81, 90-451`. Test: [L3] `safePolygon.test.ts` "does not close when a nested child is open", "does not close when an open nested child is behind a contextless intermediary node", "closes after intent timeout when no nested child is open", "keeps open while moving through the trough on %s placement" (4 sides), "closes when moving away from corridor on %s placement" (4 sides), "resets traversal state for a new handler invocation".
- **FLT-ALG-16** — MUST. The implementation reproduces the following procedure exactly (`useClick` next-open decision):
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `press` as FLT-PTR-03's note says. With `Click.PatientClick` (Popover), `stickIfOpen` is the patient-click window: a 500 ms module timer armed when the relay brings a hover change (`PopoverStore.ts:161-167`), read at press time, never sent to C# (design point 5, settled by the orchestrator 2026-10-27). Server: see ADR 01.

  ```
  nextOpen(open, pressedElement, isClickLikeOpenEvent):
    if open and store.domReferenceElement != pressedElement: return true     // press on a different trigger always opens it
    if not open: return true
    if not toggle: return true
    openEvent = contextData.openEvent
    if openEvent exists and resolve(stickIfOpen):
      return not isClickLikeOpenEvent(openEvent.type)                        // keep a hover/focus-opened popup; close a click-opened one
    return false
  ```

  Source: `floating-ui-react/hooks/useClick.ts:94-124`. Test: [L3] `useClick.test.tsx` "opens and closes on repeated clicks", "keeps open on repeated clicks when toggle is false", "stickIfOpen true keeps a hover-opened popup open on first click", "stickIfOpen false closes a hover-opened popup on first click".
- **FLT-ALG-17** — MUST. The implementation reproduces the following procedure exactly (`useDismiss` outside-press decision, run on the event target after capture bookkeeping):

  ```
  closeOnPressOutside(event):
    mode = resolveOutsidePressEvent()                                                   // FLT-API-02
    if (mode == 'intentional' and event.type != 'click') or (mode == 'sloppy' and event.type == 'click'):
      if event.type != 'click' and not withinOwnElements(event): cancel prevented-press timer; suppressNextOutsideClick = false
      clearInsideMarker(); return
    if insideMarker: clearInsideMarker(); return
    target = composedTarget(event)
    markers = all `[data-base-ui-inert]` in (target's shadow root if any, else floating element's document)
    if target is a registered trigger or inside one: return
    topAncestor = highest element ancestor of target below html/body/#document
    if markers non-empty and target is an element and target is not html/body
       and not contains(target, floatingElement) and no marker is inside topAncestor: return      // injected after open
    if target is an HTML element and event has no `touches`:
      if pressed on a vertical or horizontal scrollbar of target (FLT-DIS-07): return
    if withinOwnElements(event) or target inside any open descendant node's floating element: return
    if mode == 'intentional':
      if event.detail != 0 and not isVirtualClick(event) and not sawPressWhileOpen: return
      if suppressNextOutsideClick: cancel prevented-press timer; suppressNextOutsideClick = false; return
    if outsidePress is a function and outsidePress(event) == false: return
    if any open descendant node has __outsidePressBubbles == false: return
    store.setOpen(false, details('outside-press', event)); clearInsideMarker()
  ```

  Source: `floating-ui-react/hooks/useDismiss.ts:351-519`. Test: [L3] `useDismiss.test.tsx` "dismisses with outside pointer press", "outsidePress ignored for third party elements", "outsidePress not ignored for nested floating elements", "prop: bubbles.outsidePress › when false", "keyboard-generated outside click without a prior press closes", "inside click after drag does not cause immediate close on first outside click".
- **FLT-ALG-18** — MUST. The implementation reproduces the following procedure exactly (tabbable discovery):

  ```
  CANDIDATE = a[href], button, input, select, textarea, summary, details, iframe, object, embed, [tabindex],
              [contenteditable]:not([contenteditable="false"]), audio[controls], video[controls]
  composedChildren(node): a <slot>'s flattened assigned elements if any; else a shadow host's shadow-root children; else node.children
  candidates(container): depth-first over composedChildren, collecting every element that isFocusableCandidate
  isFocusableCandidate(el): el matches CANDIDATE
      and (el is not <summary> or it is the first <summary> child of a <details> parent)
      and (el is not <details> or it has no <summary> child)
      and (el is not <input type=hidden>)
  isFocusable(el): isFocusableCandidate(el) and el.isConnected and not el.matches(':disabled')
      and for every node n from el upward via assignedSlot → parentElement → shadow host:
            not n.hasAttribute('inert')
            and not (n is an ancestor <details> that is closed and el is not within its open <summary>)
            and not n.hasAttribute('hidden')
            and (n is a <slot> or (n == el ? isElementVisible(el) : computedStyle(n).display != 'none'))
      // isElementVisible: connected, not hidden by styles, and el.checkVisibility() when available,
      //                   else display not in {none, contents}
  tabIndex(el): el.tabIndex, except that a negative value on <details>, <audio>, <video> or a contenteditable
                host counts as 0
  isTabbable(el) = isFocusable(el) and tabIndex(el) >= 0
  focusable(container) = candidates(container).filter(isFocusable)
  tabbable(container) = focusable(container).filter(el → tabIndex(el) >= 0 and radioEligible(el, focusable))
  radioEligible(el, all): if el is not <input type=radio name≠"">: true
      else if some radio in `all` with the same name and form is checked: it is el
      else: el is the first radio in `all` with the same name and form
  ```

  Source: `floating-ui-react/utils/tabbable.ts:8-196`; `floating-ui-react/utils/composite.ts:560-573`. Test: [L3] `tabbable.test.ts` (the 26 discovery titles listed on FLT-FOC-14's `Test:` line, including "keeps only the checked radio in a named group", "keeps only the first radio when a named group has no checked item", "includes slotted light DOM elements in the tab order", "excludes unslotted light DOM elements from a shadow host tab order", "keeps the summary tabbable but excludes closed details content") (L4 for the browser-only cases).
- **FLT-ALG-19** — MUST. The implementation reproduces the following procedure exactly (outside marking, `markOthers(avoid, { ariaHidden = false, mark = true })`):

  ```
  body = ownerDocument(avoid[0]).body
  correct(targets) = targets.map(t → body.contains(t) ? t : nearest shadow host above t that body contains).filter(non-null)
  keepSet(targets) = every target and all of its ancestors (parentNode chain)
  outside(root, keep, stop) = walk children of root depth-first: skip <script>; descend into nodes in `keep`
                              (unless in `stop`); collect every other child (do not descend)
  avoid = correct(avoid)
  markerTargets = mark ? outside(body, keepSet(avoid), set(avoid)) : []
  if ariaHidden:
    live = correct(all [aria-live] in body); control = avoid ++ live
    for node in outside(body, keepSet(control), set(control)):
      already = node.aria-hidden is present and != 'false'
      count[node] += 1; hidden.push(node)
      if count[node] == 1 and already: uncontrolled.add(node)
      if not already: node.aria-hidden = 'true'
  if mark: for node in markerTargets: markCount[node] += 1; marked.push(node); if markCount[node] == 1: node.data-base-ui-inert = ''
  locks += 1
  return undo():
    for node in hidden: count[node] -= 1; if count[node] == 0: if node not in uncontrolled: remove aria-hidden; uncontrolled.delete(node)
    if mark: for node in marked: markCount[node] -= 1; if 0: remove data-base-ui-inert
    locks -= 1; if locks == 0: reset count, uncontrolled, markCount
  ```

  Source: `floating-ui-react/utils/markOthers.ts:13-187`. Test: [L3] `markOthers.test.ts` "single call", "multiple calls", "out of order cleanup", "multiple cleanups with differing controlAttribute", "leaves live regions, their ancestors, and scripts exposed", "does not recurse infinitely with target inside anchor in shadow root".
- **FLT-ALG-20** — MUST. The implementation reproduces the following procedure exactly (tree queries):

  ```
  children(nodes, id, onlyOpen = true):
    direct = nodes.filter(n → n.parentId == id)
    return flatMap(direct, child → (onlyOpen and not child.context?.open ? [] : [child]) ++ children(nodes, child.id, onlyOpen))
    // a closed or context-less intermediate node is excluded itself but its subtree is still searched
  ancestors(nodes, id):
    result = []; parentId = nodes.find(n → n.id == id)?.parentId
    while parentId: node = nodes.find(n → n.id == parentId); parentId = node?.parentId; if node: result.push(node)
    return result            // nearest first
  ```

  Source: `floating-ui-react/utils/nodes.ts:5-32`. Test: [L3] `nodes.test.ts` "getNodeChildren returns an array of children, ignoring closed ones when onlyOpenChildren=true", "getNodeChildren returns an array of children, including closed ones when onlyOpenChildren=false", "getNodeChildren handles deep parent structures correctly (onlyOpenChildren=true)", "getNodeChildren includes open descendants behind contextless intermediary nodes", "getNodeChildren handles deep parent structures correctly (onlyOpenChildren=false)", "getNodeAncestors returns an array of ancestors".
- **FLT-ALG-21** — MUST. The implementation reproduces the following procedure exactly (focus manager return-element resolution, evaluated when the manager tears down):

  ```
  returnElement(closeType):
    v = returnFocus is function ? returnFocus(closeType) : returnFocus
    if v is undefined or v is false: return null
    if v is null: v = true
    referenceReturn = domReference?.isConnected ? domReference : null
    previousReturn  = (elementFocusedBeforeOpen?.isConnected and it is not <body>) ? elementFocusedBeforeOpen : null
    default = preferPreviousFocus ? (previousReturn ?? referenceReturn) : (referenceReturn ?? previousReturn)
    if default is null: default = mostRecentPreviouslyFocusedElement() ?? null
    if v is boolean: return default
    return resolveHandle(v) ?? default ?? null
  ```

  where `elementFocusedBeforeOpen` is the active element captured when the manager armed, and `preferPreviousFocus` is `openInteractionType == null` (FLT-FOC-20).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:786-794, 847-879`. Test: [L3] `FloatingFocusManager.test.tsx` "return to the first focusable descendent of the reference, if the reference is not focusable", "returns focus to last connected element", "prop: returnFocus › ref".
- **FLT-ALG-22** — MUST. The implementation reproduces the following predicate exactly (focus-out "moved to an unrelated node"; `rt` is the `focusout` `relatedTarget`):
  Port note 2026-10-28 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); after M4 track 3 merged, the manager joins the interactions session by `SessionToken`): the tree clause and the registered triggers are in the check. "mixed modality and nesting" is green at L3 (`focus-manager.test.ts` "with the interactions session"), on arrival: the portal's focus-out marker keeps the outer popup open as in Base UI; the port titles "port: focus moving from a non-modal popup into a nested popup asks for no close" (a descendant) and "port: focus moving from a nested non-modal popup to its parent's floating element asks for no close" (an ancestor) prove the tree clause without a portal.

  ```
  related(rt) =
       contains(domReference, rt) or contains(floating, rt) or contains(rt, floating)
    or contains(portalNode, rt)
    or some(getInsideElements(), e → e == rt or contains(e, rt))
    or some registered trigger contains rt
    or (rt has data-base-ui-focus-guard and rt is one of: own before/after inside guards, the portal's
        before/after inside or outside guards, resolved previousFocusableElement, resolved nextFocusableElement)
    or (tree exists and (some open descendant node's floating element or DOM anchor contains rt
                         or some ancestor node's floating element / floating focus element equals rt
                         or some ancestor node's DOM anchor equals rt))
  movedToUnrelatedNode = not related(rt)
  ```

  Source: `floating-ui-react/components/FloatingFocusManager.tsx:457-500`. Test: [L3] `FloatingFocusManager.test.tsx` "mixed modality and nesting", "prop: modal › when false".
- **FLT-ALG-23** — MUST. The implementation reproduces the following procedure exactly (delay group state machine; `G` is the group's shared state `{ delay, initialDelay, currentId, currentContext, timer, timeoutMs }`, and each member calls `useDelayGroup(store, { open })`):
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): one group per provider token, created by the TooltipProvider's elementless handle (sanctioned page-global state, `js/CONTEXT.md`). A member is a trigger whose `Hover.DelayGroupToken` names the provider; its open closes the previous member through that member's Root twin with reason `none`, no event and `instant: true` (FLT-EVT-07); the open request carries the group's instant phase, so C# renders `data-instant="delay"` in the batch of `data-open`; members read the group's delay at `mouseenter`. Provider-less tooltips are not grouped (Base UI's shared default context is not replicated). Server: see ADR 01.

  ```
  on member open becoming true (post-commit, pre-paint):
    prevContext = G.currentContext; prevId = G.currentId
    cancel G.timer
    G.currentContext = { onOpenChange: store.setOpen, setInstantPhase: this member's setter }
    G.currentId = this member's floatingId
    G.delay = { open: 0, close: resolveDelay(G.initialDelay, 'close') }
    if prevId is not null and prevId != this id: this.instant = true; prevContext?.setInstantPhase(true); prevContext?.onOpenChange(false, details('none'))
    else: this.instant = false; prevContext?.setInstantPhase(false)
  on member open becoming false while G.currentId == this id:
    this.instant = false
    if G.timeoutMs: closingId = this id
      start G.timer(G.timeoutMs): if store.open or (G.currentId and G.currentId != closingId): return; reset()
      (if this member reopens or another takes over before the timer fires, the timer is cancelled)
    else: reset()
  reset(): G.currentContext?.setInstantPhase(false); G.currentId = null; G.currentContext = null; G.delay = G.initialDelay; cancel G.timer
  on member unmount while G.currentId == this id: G.currentContext = null; if this member is open: G.currentId = null; G.delay = G.initialDelay; cancel G.timer
  on group `delay` prop change: G.initialDelay = delay; if no current member: G.delay = delay else G.delay = { open: resolveDelay(G.delay, 'open'), close: resolveDelay(delay, 'close') }
  ```

  Members read `G.delay` through `delayRef` when configuring their hover delay (Tooltip passes `delay: () => delayRef.current` semantics, see `components/tooltip.md`), so after one tooltip opened, siblings open with no delay and close with the group's close delay until the group resets.
  Source: `floating-ui-react/components/FloatingDelayGroup.tsx:64-105, 139-278`. Test: [L3] `FloatingDelayGroup.test.tsx` "groups delays correctly" (open 1000 ms, then siblings instant, close 200 ms), "timeoutMs" (group stays warm 500 ms after close), "does not re-render unrelated consumers" (behavioural part: with `timeoutMs` 500 a sibling hovered after the first popup closed opens instantly; its render-count assertions are React-only and have no port equivalent).
- **FLT-ALG-24** — MUST. The implementation reproduces the following procedure exactly (hover trigger `mouseenter` decision; `inst` is the shared hover state):
  Port note (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the hover decision runs in the module, as FLT-PTR-12's note says. Server: see ADR 01.

  ```
  onMouseEnter(event):
    cancel inst.openChangeTimeout; inst.blockMouseMove = false
    if mouseOnly and not isMouseLike(inst.pointerType): return
    restMs = resolve(restMs); openDelay = resolveDelay(delay, 'open', inst.pointerType)
    triggerNode = resolveTriggerNode(event)                                     // FLT-EDGE-10
    overInactive = triggerNode != null and isOverInactiveTrigger(domReference, triggerNode, event target)
    isOpen = store.open
    closing = isClosing ? isClosing() : store.transitionStatus == 'ending'
    hoverCloseTransition = not isOpen and closing and lastCloseWasHover
    reenteringSameTrigger = not overInactive and triggerNode and domReference and contains(domReference, triggerNode) and hoverCloseTransition
    restOnly = restMs > 0 and not openDelay
    openImmediately = (overInactive and (isOpen or hoverCloseTransition)) or reenteringSameTrigger
    shouldOpen = not isOpen or overInactive
    if openImmediately: if shouldOpenProp() != false: setOpen(true, details('trigger-hover', event, triggerNode)); return
    if restOnly: return                                                           // handled by mousemove (FLT-PTR-16)
    if openDelay: start inst.openChangeTimeout(openDelay): if shouldOpen and shouldOpenProp() != false: setOpen(true, ...)
    else if shouldOpen and shouldOpenProp() != false: setOpen(true, ...)
  isOverInactiveTrigger(domRef, currentTarget, target):
    if currentTarget is a registered trigger: return domRef is null or not contains(domRef, currentTarget)
    if target is not an element: return false
    return (some registered trigger contains target) and (domRef is null or not contains(domRef, target))
  ```

  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:116-140, 216-300`. Test: [L3] `useHoverReferenceInteraction.test.tsx` "hover semantics" block and the four wrapper/transition cases.
- **FLT-ALG-25** — MUST. The implementation reproduces the following procedure exactly (cursor-anchoring virtual anchor; created with the DOM element `el`, `axis`, the cursor `(px, py)` and the pointer type):

  ```
  offsetX = offsetY = null; isAutoUpdate = false
  getBoundingClientRect():
    r = el?.getBoundingClientRect() ?? { x: 0, y: 0, width: 0, height: 0 }
    isX = axis in {x, both}; isY = axis in {y, both}
    canTrack = contextData.openEvent?.type in {mouseenter, mousemove} and pointerType != 'touch'
    width  = axis == 'y' ? r.width  : 0
    height = axis == 'x' ? r.height : 0
    x = r.x; y = r.y
    if offsetX is null and px and isX: offsetX = r.x - px
    if offsetY is null and py and isY: offsetY = r.y - py
    x -= offsetX ?? 0; y -= offsetY ?? 0
    if not isAutoUpdate or canTrack:
      x = isX and px != null ? px : x
      y = isY and py != null ? py : y
    isAutoUpdate = true
    return { width, height, x, y, top: y, right: x + width, bottom: y + height, left: x }
  contextElement = el
  ```

  (The first measurement uses the cursor point; later measurements during anchor tracking follow the element's movement by the initial offset unless the popup was opened by a mouse hover, in which case they keep following the cursor.)
  Source: `floating-ui-react/hooks/useClientPoint.ts:10-75`. Test: [L3] `useClientPoint.test.tsx` "renders at mouse event coords", "axis x", "axis y".
- **FLT-ALG-26** — MUST. The implementation reproduces the virtual-input predicates of FLT-A11Y-01 (`isVirtualPointerEvent`) and FLT-A11Y-02 (`isVirtualClick`) exactly, plus `isMouseLikePointerType(type, strict)`: `type ∈ {'mouse','pen'}` or, when not strict, `type ∈ {'', undefined}`.
  Source: `floating-ui-react/utils/event.ts:13-58`. Test: [L3] `event.test.ts` (all six cases).
- **FLT-ALG-27** — MUST. The implementation reproduces the following procedure exactly (`handleTabIndex`, FLT-DOM-17):

  ```
  handleTabIndex(el):
    if el has tabindex and not data-tabindex: return                      // consumer-managed
    if el.role does not contain 'dialog': return
    tabbableContent = focusable(el).filter(c → isTabbable(c) or (c has data-tabindex and not c.data-tabindex.startsWith('-')))
    if tabbableContent is empty:
      if el.tabindex != '0': el.tabindex = '0'; el.data-tabindex = '0'
    else if el.tabindex != '-1' or (el has data-tabindex and el.data-tabindex != '-1'):
      el.tabindex = '-1'; el.data-tabindex = '-1'
  ```

  Source: `floating-ui-react/components/FloatingFocusManager.tsx:114-150`. Test: [L3] `FloatingFocusManager.test.tsx` "floating element with managed tabIndex is downgraded once content becomes tabbable".
- **FLT-ALG-28** — MUST. The implementation reproduces the following helper predicates exactly: `isEventTargetWithin(event, node)` = `node` is in `event.composedPath()` (fallback: `node.contains(event.target)`); `isTargetInsideEnabledTrigger(target, triggers)` = `target` is an element that is (or is inside) a registered trigger, and that trigger does not carry `data-trigger-disabled` (the Tooltip trigger-disabled attribute, see `components/tooltip.md`); `isRootElement(el)` = `el.matches('html,body')`; `isTypeableElement(el)` = HTML element matching `input:not([type='hidden']):not([disabled]), [contenteditable]:not([contenteditable='false']), textarea:not([disabled])`; `isInteractiveElement(el)` = `closest(el, 'button,a[href],[role="button"],select,[tabindex]:not([tabindex="-1"]),<typeable>')` exists; `isTypeableCombobox(el)` = `role="combobox"` and typeable.
  Source: `floating-ui-react/utils/element.ts:10-69`; `floating-ui-react/utils/constants.ts:4-6`. Test: none.

## 6. Configuration surface used by components

Component docs cite these tables in their §11 and state only the values they pass.

### 6.1 `useAnchorPositioning` (every `*.Positioner`)

Public options are in §3.1. Private options:

| Option | Type | Default | Meaning |
|---|---|---|---|
| `mounted` | `boolean` | required | Whether the Positioner is mounted; gates positioning, CSS-variable writes and anchor registration (FLT-STA-09, FLT-DOM-05). |
| `keepMounted` | `boolean` | `false` | Popup stays in the DOM while closed; switches anchor tracking to the explicit `mounted`-gated mode and passes `mounted` as the session's `open` (FLT-POS-11/12, FLT-ANI-02). |
| `floatingRootContext` | root store | required | The root store whose elements are positioned. |
| `nodeId` | `string` | `undefined` | Tree node id of this popup (FLT-STR-08). |
| `externalTree` | tree store | `undefined` | Explicit tree (Menu passes its root tree). |
| `collisionAvoidance` | `CollisionAvoidance` | required (component default) | See §3.1. |
| `shift` | `{ crossAxis?, rootBoundary? }` | `undefined` | Context Menu passes `{ crossAxis: !(side === 'flip'), rootBoundary: 'layoutViewport' }`; Navigation Menu passes `{ rootBoundary: 'layoutViewport' }` (FLT-POS-05). |
| `adaptiveOrigin` | middleware | `undefined` | Supplied by the popup store when a Viewport part is present (PSH); enables FLT-DOM-04/FLT-ALG-12. |
| `lazyFlip` | `boolean \| 'placement'` | `false` | Combobox passes `true`; Menu passes `'placement'` when `virtualFocus` (FLT-ANI-04). |
| `inline` | middleware | `undefined` | Preview Card's inline line-box middleware (FLT-POS-02). |

Component defaults observed in v1.9.0 (details in the component docs): `collisionAvoidance` is `POPUP_COLLISION_AVOIDANCE` for Popover, Tooltip, Preview Card and submenus; `DROPDOWN_COLLISION_AVOIDANCE` for Menu (top-level), Select, Combobox/Autocomplete and top-level Navigation Menu. Menu sets `side: 'inline-end', align: 'start'` for submenus, `side` by orientation for menubar children, `align: 'start'` plus `sideOffset: -5`/`alignOffset: 2` (when no `side`) and `arrowPadding: 0`, `positionMethod: 'fixed'` for context menus (`menu/positioner/MenuPositioner.tsx:40-131`).

### 6.2 `usePositioner`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `styles` | CSS properties | required | `positionerStyles` from §6.1. |
| `transitionStatus` | TRN status | required | Feeds the disabled-mount-transition styles. |
| `props` | element props | `undefined` | Consumer element props merged last. |
| `refs` | DOM handles | `undefined` | Forwarded handle + store setter. |
| `hidden` | `boolean` | `undefined` | `!mounted` in every Positioner. |
| `inert` | `boolean` | `false` | `!open` in every Positioner → `pointer-events: none`. |

### 6.3 `useClick`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `enabled` | `boolean` | `true` | Returns an empty bag when false. | all |
| `event` | `'click' \| 'mousedown' \| 'mousedown-only'` | `'click'` | Which pointer event opens/closes (FLT-PTR-02/03). | Select `'mousedown'`; Submenu trigger `'mousedown'`; Combobox input `'mousedown-only'` |
| `toggle` | `boolean` | `true` | Repeated press closes (FLT-PTR-05). | Combobox `false`; Navigation Menu `isActiveItem` |
| `ignoreMouse` | `boolean` | `false` | Drop strictly mouse-like presses (hover-driven triggers). | none in v1.9.0 |
| `stickIfOpen` | `boolean \| () => boolean` | `true` | Keep hover/focus-opened popup on first press (FLT-PTR-06). | Popover getter from store; Menu `useStickIfOpen`; Navigation Menu |
| `touchOpenDelay` | `number` ms | `0` | Touch-only open deferral (FLT-PTR-04). | Combobox `100` (0 when input is inside the popup) |
| `reason` | `'trigger-press' \| 'input-press'` | `'trigger-press'` | Reason in details. | Combobox `input-press` |

### 6.4 `useDismiss`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `enabled` | `boolean` | `true` | | Tooltip/Menu `!disabled`; Combobox `!disabled && !inline`; Navigation Menu |
| `escapeKey` | `boolean` | `true` | FLT-KEY-01. | — |
| `referencePress` | `() => boolean` | `() => false` | FLT-PTR-30. | Tooltip (`closeOnClick`) |
| `outsidePress` | `boolean \| (event) => boolean` | `true` | FLT-DIS-09. | Menu, Dialog, Combobox, Navigation Menu (functions) |
| `outsidePressEvent` | `'intentional' \| 'sloppy' \| { mouse, touch } \| () => …` | `'sloppy'` | FLT-API-02, FLT-DIS-03. | Popover `{ mouse: modal === 'trap-focus' ? 'sloppy' : 'intentional', touch: 'sloppy' }`; Dialog getter (`'intentional'` with a backdrop, else same object); Combobox `{ mouse: 'sloppy', touch: 'intentional' }`; Navigation Menu `'intentional'` |
| `bubbles` | `boolean \| { escapeKey?, outsidePress? }` | `undefined` → `{ escapeKey: false, outsidePress: true }` | FLT-API-01. | Menu `{ escapeKey: closeParentOnEsc && parent is menu }`; Combobox `true` when inline |
| `externalTree` | tree store | `undefined` | | Menu (nested) |

### 6.5 `useFocus`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `enabled` | `boolean` | `true` | | Tooltip `!disabled`; Menu trigger |
| `delay` | `number` ms | `undefined` | FLT-FOC-01. | Preview Card (`delay`) |

### 6.6 `useClientPoint`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `enabled` | `boolean` | `true` | | Tooltip `!disabled && trackCursorAxis !== 'none'` |
| `axis` | `'x' \| 'y' \| 'both'` | `'both'` | FLT-API-14. | Tooltip `trackCursorAxis` |

### 6.7 `useHoverReferenceInteraction`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `enabled` | `boolean` | `true` | | all (component conditions) |
| `handleClose` | strategy or `null` | `null` | Safe polygon (FLT-ALG-15); `safePolygon({ blockPointerEvents, getScope })`. | Tooltip (unless hoverable popup disabled or `trackCursorAxis === 'both'`), Preview Card, Popover, Menu trigger `{ blockPointerEvents: !isInMenubar }`, Submenu `{ blockPointerEvents: true }`, Navigation Menu (`getScope`) |
| `restMs` | `number \| () => number` | `0` | FLT-PTR-16. | Tooltip open delay getter; Popover `delay`; Menu `delay` (top-level only); Submenu `delay`; Navigation Menu `delay` or 0 when mounted |
| `delay` | `Delay \| () => Delay` | `0` | FLT-API-03. | Tooltip `{ close }` (group-aware); Preview Card `{ open, close }`; Popover/Menu `{ close }`; Submenu `{ open, close }` |
| `move` | `boolean` | `true` | Also open on the first `mousemove`. | `false` everywhere except Submenu (`true`) |
| `mouseOnly` | `boolean` | `false` | FLT-PTR-11. | Tooltip, Preview Card, Popover, Menu, Submenu `true` |
| `externalTree` | tree store | `undefined` | | Menu, Submenu |
| `isActiveTrigger` | `boolean` | `true` | Whether this hook instance owns the active trigger (publishes `handleCloseOptions`). | Tooltip, Preview Card, Popover, Menu |
| `triggerElementRef` | DOM handle | empty | The element to attach native listeners to. | all |
| `getHandleCloseContext` | `() => context \| null` | `undefined` | Context for popups without a positioning session. | Navigation Menu |
| `isClosing` | `() => boolean` | `undefined` → `transitionStatus === 'ending'` | FLT-PTR-13. | Tooltip, Preview Card, Popover, Menu, Submenu |
| `shouldOpen` | `() => boolean` | `undefined` | Veto (FLT-PTR-12). | Tooltip (nested trigger hovered); Submenu (`allowMouseEnter` when `delay > 0`) |
| `guardStaleOpen` | `boolean` | `false` | FLT-PTR-18. | Submenu `true` |

### 6.8 `useHoverFloatingInteraction`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `enabled` | `boolean` | `true` | | Popover `openOnHover && !disabled`; others per component |
| `closeDelay` | `number \| () => number` | `0` | FLT-PTR-24. | Tooltip, Preview Card, Popover, Menu, Navigation Menu |
| `nodeId` | `string` | `undefined` | Node id for popups without a session context. | Navigation Menu |

### 6.9 `FloatingDelayGroup` / `useDelayGroup`

| Option | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `delay` | `Delay` | required | Group delay (FLT-ALG-23). | `Tooltip.Provider` (`delay`/`closeDelay`) |
| `timeoutMs` | `number` | `0` | Warm period after close. | `Tooltip.Provider` `timeout` |
| `useDelayGroup(store, { open })` | | | Returns `delayRef`, `isInstantPhase`, `hasProvider`, `activeIdRef`. | `Tooltip.Trigger` |

### 6.10 `FloatingFocusManager`

| Prop | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `context` | root store | required | | all |
| `openInteractionType` | `'mouse' \| 'touch' \| 'pen' \| 'keyboard' \| '' \| null` | `''` | Open modality (FLT-API-05, FLT-FOC-20). | Dialog, Drawer, Popover, Menu, Select, Combobox (`openMethod`) |
| `disabled` | `boolean` | `false` | FLT-FOC-19. | `!mounted` (Popover also when opened by hover) |
| `initialFocus` | see FLT-API-05 | `true` | FLT-FOC-06. | Dialog/Drawer/Popover/Combobox resolved values; Menu `initialFocus` |
| `returnFocus` | see FLT-API-05 | `true` | FLT-FOC-07. | `finalFocus` (Dialog, Drawer, Popover, Select, Combobox); Menu `finalFocus ?? dynamicReturnFocus ?? returnFocus` |
| `explicitReturnFocus` | `boolean` | `undefined` | FLT-FOC-21. | Menu `false` when using its dynamic default |
| `restoreFocus` | `boolean \| 'popup'` | `false` | FLT-FOC-12. | Dialog, Drawer, Popover `'popup'`; Menu, Select `true` |
| `modal` | `boolean` | `true` | FLT-ARIA-01/02, FLT-KEY-04/05. | Dialog/Drawer `modal !== false`; Menu `isContextMenu`; Select `false`; Popover/Combobox computed |
| `closeOnFocusOut` | `boolean` | `true` | FLT-FOC-11. | Dialog/Drawer `!disablePointerDismissal` |
| `nextFocusableElement` / `previousFocusableElement` | element or handle | `undefined` | FLT-KEY-06, FLT-ARIA-03. | Popover, Menu (trigger focus targets) |
| `beforeContentFocusGuardRef` | handle | `undefined` | Exposes the before-inside guard. | Popover |
| `externalTree` | tree store | `undefined` | | Menu (non-menubar) |
| `getInsideElements` | `() => elements` | `undefined` | Extra inside elements (FLT-ARIA-03, FLT-ALG-22). | Popover, Menu (trigger focus guard), Combobox (dismiss buttons) |

### 6.11 `FloatingPortal` / `useFloatingPortalNode`

| Prop | Type | Default | Meaning | Passed by |
|---|---|---|---|---|
| `container` | see §3.3 | parent portal node / body | FLT-API-13. | every `*.Portal` |
| `portalOwnerRole` | role | `undefined` | FLT-ARIA-04. | Menu |
| element props | | | Forwarded to the portal node (FLT-DOM-11). | every `*.Portal` |

### 6.12 Root store creation

| Entry point | Used by | Notes |
|---|---|---|
| `useSyncedFloatingRootContext({ popupStore, treatPopupAsFloatingElement, floatingRootContext, floatingId, nested, onOpenChange })` | Popover, Tooltip, Preview Card, Menu, Dialog, Drawer (through the PSH popup store) | FLT-STR-10; sync-only store. |
| `useFloatingRootContext({ open, onOpenChange, elements })` | Select, Combobox, Navigation Menu trigger, Toast positioner | FLT-STR-11. |
| `new FloatingRootStore({ … })` | PSH `createPopupStore`, tests, experiments | FLT-STR-01..03. |

## 7. Edge cases and dev warnings

All EDGE requirements are listed in §4.15 (FLT-EDGE-01 … FLT-EDGE-14) so they sit next to the behavior they qualify. Diagnostics emitted by this slice, verbatim:

| Condition | Level | Message | ID |
|---|---|---|---|
| `useFloatingRootContext` called with a virtual `elements.reference` (dev only) | `console.error` (three arguments) | `Cannot pass a virtual element to the \`elements.reference\` option,` / `as it must be a real DOM element. Use \`context.setPositionReference()\`` / `instead.` | FLT-EDGE-01 |

No `throw` statements exist in this slice; the trigger-map duplicate-id error belongs to PSH.

## 8. React-specific notes and porting considerations

Non-normative.

* **Hooks are composition units, not components.** Each interaction hook is a function called during a component's render that returns prop bags and registers side effects. A port needs equivalent objects with explicit lifecycle: *configure* (options may change every render), *attach* (listeners on document/window/elements while open and enabled), *detach*. Every hook here re-runs its attach step whenever an option or element changes, which is why FLT-DIS-14 and FLT-DIS-12 distinguish "popup closed" from "listeners re-attached while open". Port: see decisions/04-js-interop-boundary.md §1 (the hooks become the JavaScript `interactions` module — press-to-toggle, dismissal, focus-to-open, cursor anchoring, hover with safe polygon, delay group, floating tree — and the `focus-manager` module, each with one C# twin) and §3 (`attach(elementOrId, options)` → handle with `update(options)`/`dispose()`; one message to C# per state change, never per frame).
* **Timing phases.** `useIsoLayoutEffect` = post-commit, pre-paint; `React.useEffect` = after paint; `ReactDOM.flushSync` = commit synchronously now. The engine's `update()` commits positions with `flushSync` so the next paint shows them (FLT-POS-16). `queueMicrotask` and `setTimeout(0)` are used as written. The focus manager's initial focus is "microtask then next frame": the microtask lets other post-commit state writes (e.g. managed `tabindex`) land first. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (timing-term mapping; next-frame and microtask work exists only in JavaScript) and §Relaxation table (FLT-POS-16 and FLT-ALG-13 are met on both render modes because the whole positioning loop is JavaScript).
* **Stable callbacks and "value as ref".** `useStableCallback`/`useValueAsRef` make option values readable at event time without re-attaching listeners. The spec expresses this as "resolved when the event is handled" (FLT-API-06/07, FLT-API-12). Ports should read the *latest* option value at event time. Port: see decisions/04-js-interop-boundary.md §3 (C# pushes changed options to the JS handle with `update(options)`; listeners read the latest pushed values at event time).
* **The root store is a reactive store** (`ReactStore` from `@base-ui/utils/store`, UTL): `useState(key)` subscribes a component to one field; `select(key)` reads synchronously; `set`/`update` write. The `context` bag is deliberately non-reactive. A port can model it as an observable record plus a plain mutable side-record. Port: see decisions/05-state-store-and-event-details.md §1 (`Store<TState>`; the popup store and open state are C#) and decisions/04-js-interop-boundary.md §1 (the JS modules hold the non-reactive per-session data and report open-state requests to their twin).
* **Context providers.** `FloatingTree`, `FloatingNode`, `FloatingDelayGroup` and the portal context are React contexts (ancestor-provided shared state). The *scope* semantics matter: a hook finds "the nearest enclosing" tree/node/portal/group. Ports need an equivalent scoping mechanism (cascading parameters in Blazor, an explicit scope object passed down, etc.). `externalTree` exists precisely because React context cannot cross portal/ownership boundaries in Menu's case. Port: see decisions/04-js-interop-boundary.md §1 (the floating tree belongs to the `interactions` module) and decisions/02-render-pipeline-and-attribute-merging.md (ancestor-provided shared state becomes fixed cascading values).
* **Portals.** `ReactDOM.createPortal` keeps the popup *logically* inside the trigger's component tree (so React events bubble to the trigger's ancestors) while rendering it elsewhere in the DOM. The focus guards, `aria-owns` owner and the "inside marker" protocol in `useDismiss` (FLT-DIS-04/12) exist to recreate containment semantics that the DOM no longer provides. A port without such a mechanism must still render popups outside overflow-clipping ancestors and must reproduce the marker protocol with native capture listeners. Port: see decisions/04-js-interop-boundary.md §5 (the `portal` module moves the portal node into the resolved container after first render and restores it before dispose; Blazor's logical tree, cascading values and event bubbling are untouched) and §1 (the `portal` module listens on the outside focus guards; C# renders every guard and the module that owns a guard's behaviour listens on it, ADR 02 and ADR 04 Amendments 2026-10-27; corrected 2026-10-27, M4 track 4).
* **Synthetic vs native events.** Prop-bag handlers (`onClick`, `onPointerDown`, …) are React synthetic handlers attached by the component that spreads the bag; `nativeEvent` is used wherever the spec says "the event". Handlers with `Capture` suffix are capture-phase. Native listeners added with `addEventListener` are called out as "on the document/window/element". Port: see decisions/04-js-interop-boundary.md §3 (`details.event` is a C# snapshot record; Escape close uses `decide(point, eventInfo, fallbackPolicy)`: WebAssembly runs the C# chain synchronously and meets FLT-KEY-01 as written, Server applies the fallback "prevent and stop when the owning popup is the innermost open one") and decisions/01-hosting-model-and-timing.md §Relaxation table (row EVD-EVT-11, EVD-DIS-01, FLT-KEY-01).
* **StrictMode double-invocation.** Several tests run under StrictMode, which mounts, unmounts and remounts effects. Requirements that mention "re-arm in the same commit" (FLT-EDGE-07), "clears the inside marker when the interaction owner unmounts", and the delay-group instant-phase reset (FLT-ANI-01) exist to survive that; a port without StrictMode still needs the stated behavior because real re-renders (reference change while open) exercise the same paths.
* **`deepEqual` on middleware.** `useFloating` keeps the previous middleware array unless a deep comparison (including function `toString()`) finds a change (`vendor/@floating-ui_react-dom/dist/floating-ui.react-dom.mjs:14-60, 112-115`). Base UI passes dependency arrays to each middleware factory (`offset(fn, deps)`, `shift(opts, deps)`, …) so that identity changes only when inputs change. A port should recompute the pipeline whenever an input of FLT-ALG-14 changes and otherwise keep it stable to avoid positioning loops. Port: see decisions/03-positioning-engine.md §Decision (middleware list rebuilt when an input changes, otherwise kept stable; `@floating-ui/react-dom` is not used).
* **Virtual elements.** The engine accepts any object with `getBoundingClientRect()`; `contextElement` lets the DOM platform compute clipping ancestors and scale. FLT-STA-07 wraps DOM anchors into such objects so that a *later* change of the DOM anchor does not silently change the position anchor.
* **`platform.isRTL` vs `DirectionProvider`.** Two direction sources exist: the UTL provider (used for logical-side resolution and reporting) and the engine's computed-style check on the floating element (used inside middleware and for the transform origin). They normally agree; a port must keep both behaviors or document the divergence. Port: see decisions/03-positioning-engine.md §Decision (both are kept; see §10 Q4).
* **Test shims.** `test/floating-ui-tests/useFloating.ts` and `useHover.ts` are React-only adapters that give the ported Floating UI tests the upstream API (`refs.setReference` registering elements in the store). They are not shipped and need no port, but the tests cited here go through them. Port: see decisions/06-testing-harness.md §1 (L3, vitest in jsdom against a fake twin: the `useDismiss`, `useClick`, `useFocus`, hover and `safePolygon` hook tests, `tabbable`, `markOthers` and the `hide` middleware test; L4, Playwright per render mode: the positioner conformance suite and positioning tests with pixel assertions).
* **Vendored engine.** The engine builds in `reference/vendor/` are the authority for §5.1; the pseudo-code here was transcribed from them. `autoPlacement` and `inline` middleware exist in the engine but Base UI does not configure them (Preview Card supplies its own `inline` replacement, see `components/preview-card.md`). Port: see decisions/03-positioning-engine.md §Decision (the vendored engine is bundled, not transcribed; §5.1 status note).
* **Attribute ownership of positioned parts.** Every Positioner's `style` (`transform`/`top`/`left`, `--anchor-*`, `--available-*`, `--transform-origin`), `data-side`, `data-align` and `data-anchor-hidden`, and every Arrow's `style`, `data-side`, `data-align` and `data-uncentered`, are written by JavaScript at frame timing (FLT-DOM-03..10); C# mirrors `side`, `align`, `anchorHidden` and `uncentered` into the state object one round trip late on Server. Rewritten 2026-10-27 (M4 track 2): the earlier disagreement between ADR 03 and ADR 04 §2 on the prerendered markup was settled by ADR 04 Amendments 2026-10-18 (handover at attach). C# renders, in prerender and the first interactive render, everything Base UI renders before the first positioning pass: the requested `side`/`align`, the seeded `--available-width: 100vw` / `--available-height: 100vh`, FLT-DOM-02's `position: fixed; top: 0; left: 0; opacity: 0`, `pointer-events: none` while closed and `transition: none` while starting, then the consumer's style. It freezes them at attach (ADR 04 Amendments 2026-10-27). From attach, JavaScript composes the element's whole `style` through `dom`'s layered-style writer: the consumer's string between the positioner styles and `transition: none` (under it) and the measured variables (over it). Port: see decisions/04-js-interop-boundary.md §2 and Amendments 2026-10-18 and 2026-10-27; `src/Blazix.Primitives/Floating/`.
* **Focus-out gating.** FLT-FOC-09's recording of the element that lost focus and FLT-FOC-12's focus restoration both run inside the focus manager's `focusout` listener, which is installed only while `closeOnFocusOut` is true; that coupling is kept because the listener, the previously-focused stack and the restoration all live in one JavaScript module. Port: see decisions/04-js-interop-boundary.md §1 (`focus-manager`).

## 9. Requirement index

     221 requirements (the count corrected 2026-10-27 by M4 track 2, FLT-A11Y-08 having been added in the second review pass, and by M4 track 3, whose index count agreed). Statements are abbreviated; the section column gives the subsection where the full text, source and test citations appear.

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| FLT-STR-01 | MUST | Each popup instance owns one root store whose reactive state has exactly these fields: `open: boolean`,… | §4.1 |
| FLT-STR-02 | MUST | The root store's *selected* reference element (the one positioning measures) is `positionReference` when non-null,… | §4.1 |
| FLT-STR-03 | MUST | The root store carries a non-reactive context with: `onOpenChange` callback (replaced on every render of the part), `dataRef`… | §4.1 |
| FLT-STR-04 | MUST | The root event bus is a synchronous emitter: `on(name, fn)` registers (a set per name, so double registration is a… | §4.1 |
| FLT-STR-05 | MUST | Context data (`dataRef.current`) is a mutable record with the optional keys `openEvent` (the DOM `Event` that most… | §4.1 |
| FLT-STR-06 | MUST | A floating tree is a store with an ordered node list and its own event bus. `addNode(node)` appends; `removeNode(node)`… | §4.1 |
| FLT-STR-07 | MUST | A tree is made available to descendants by a `FloatingTree` boundary (which creates one store for its lifetime, or… | §4.1 |
| FLT-STR-08 | MUST | On every render of the part that owns a positioning session, the registered tree node with the session's `nodeId` has… | §4.1 |
| FLT-STR-09 | MUST | `nested` is true for a root store created while a tree node boundary is in scope (parent node id not `null`); it is… | §4.1 |
| FLT-STR-10 | MUST | A root store synced from a popup store (`useSyncedFloatingRootContext`) mirrors, post-commit pre-paint whenever any of… | §4.1 |
| FLT-STR-11 | MUST | A standalone root store (`useFloatingRootContext({ open, onOpenChange, elements })`) starts closed by default, syncs… | §4.1 |
| FLT-STA-01 | MUST | `setOpen(nextOpen, details)` on a root store MUST, when the store is in *sync-only* mode, only forward to the current… | §4.2 |
| FLT-STA-02 | MUST | `dispatchOpenChange(nextOpen, details)` MUST first update the recorded open event (FLT-STA-03) and then emit… | §4.2 |
| FLT-STA-03 | MUST | The recorded open event (`dataRef.current.openEvent`) MUST be set to `details.event` when opening and the popup is… | §4.2 |
| FLT-STA-04 | MUST | An event is *click-like* if its `type` is `click`, `mousedown`, `keydown` or `keyup`. | §4.2 |
| FLT-STA-05 | MUST | An open event is a *hover open* if its type contains `mouse` and is not `mousedown` (`mouseenter`, `mousemove`,… | §4.2 |
| FLT-STA-06 | MUST | The hover hooks treat the popup as *click-like open* when the recorded open event is click-like **or** the user has… | §4.2 |
| FLT-STA-07 | MUST | Setting the position anchor through the positioning session (`refs.setPositionReference(node)`) MUST, for a DOM… | §4.2 |
| FLT-STA-08 | MUST | The positioning session's `context` MUST expose the root store's `dataRef` and `events` by identity, the current… | §4.2 |
| FLT-STA-09 | MUST | While a Positioner is not mounted (`mounted === false`), the positioning layer MUST clear the root store's… | §4.2 |
| FLT-API-01 | MUST | `useDismiss` `bubbles` normalization: a boolean applies to both keys; an object or `undefined` yields `escapeKey =… | §4.3 |
| FLT-API-02 | MUST | `outsidePressEvent` (default `'sloppy'`) is resolved at the time of each pointer event: a function is called; a string… | §4.3 |
| FLT-API-03 | MUST | Hover delay resolution `getDelay(value, 'open' \| 'close', pointerType)`: if a pointer type is given and is not… | §4.3 |
| FLT-API-04 | MUST | `restMs` MAY be a number or a function returning a number; it is resolved by calling the function at each use. | §4.3 |
| FLT-API-05 | MUST | `FloatingFocusManager` `initialFocus` / `returnFocus` values resolve as: `false` or `undefined` (including a function… | §4.3 |
| FLT-API-06 | MUST | `useClick` `stickIfOpen` (default `true`) MAY be a getter; it is evaluated when the press is handled, not when… | §4.3 |
| FLT-API-07 | MUST | `useDismiss` `referencePress` is a getter (default: always `false`) evaluated on each reference `pointerdown`/`click`. | §4.3 |
| FLT-API-08 | MUST | `collisionPadding` is normalized to `{ top, right, bottom, left }`: a number applies to all four sides; an object's… | §4.3 |
| FLT-API-09 | MUST | `collisionBoundary: 'clipping-ancestors'` MUST be passed to the engine as `'clippingAncestors'`; any other value… | §4.3 |
| FLT-API-10 | MUST | `collisionAvoidance` keys default individually: `side ?? 'flip'`, `align ?? 'flip'`, `fallbackAxisSide ?? 'end'`. | §4.3 |
| FLT-API-11 | MUST | `anchor` resolution, performed post-commit pre-paint while mounted: a function is called; a DOM handle is dereferenced;… | §4.3 |
| FLT-API-12 | MUST | An `OffsetFunction` for `sideOffset`/`alignOffset` receives `{ side, align, anchor: { width, height }, positioner: {… | §4.3 |
| FLT-API-13 | MUST | Portal container resolution: `container === null` → no portal node is created (and an existing one is torn down);… | §4.3 |
| FLT-API-14 | MUST | `useClientPoint` `axis` (default `'both'`) restricts cursor anchoring to one axis; on the other axis the virtual anchor… | §4.3 |
| FLT-API-15 | MUST | `useFocus` `delay` (default `undefined`) is a number of ms; `0`/`undefined` means open synchronously on focus. | §4.3 |
| FLT-API-16 | MUST | `FloatingDelayGroup` takes `delay` (`number \| { open?, close? }`) and `timeoutMs` (default `0`); `useDelayGroup(store,… | §4.3 |
| FLT-API-17 | MUST | `useAnchorPositioning` private parameters (not consumer-facing; listed so component docs can cite them): `mounted`… | §4.3 |
| FLT-DOM-01 | MUST | The shared Positioner element (`usePositioner`) renders a `<div role="presentation">` with: the `hidden` attribute when… | §4.4 |
| FLT-DOM-02 | MUST | Until the current open session has been positioned (`isPositioned === false`), the positioner styles MUST be `position:… | §4.4 |
| FLT-DOM-03 | MUST | Once positioned and when no adaptive-origin middleware is configured, the positioner styles MUST be the engine's… | §4.4 |
| FLT-DOM-04 | MUST | Once positioned and when an adaptive-origin middleware is configured (a Viewport part is present, PSH), the positioner… | §4.4 |
| FLT-DOM-05 | MUST | On every positioning pass while mounted, the size middleware MUST write `--available-width: <availableWidth>px` and… | §4.4 |
| FLT-DOM-06 | MUST | On every positioning pass while mounted, the size middleware MUST also write `--anchor-width` and `--anchor-height`… | §4.4 |
| FLT-DOM-07 | MUST | On every positioning pass the `transformOrigin` middleware MUST write `--transform-origin` on the floating element's… | §4.4 |
| FLT-DOM-08 | MUST | The positioning result exposed to the Positioner (and through its context to Popup/Arrow parts) MUST include: `side`… | §4.4 |
| FLT-DOM-09 | MUST | `arrowStyles` MUST be `{ position: 'absolute', top: <arrow.y>, left: <arrow.x> }` using the arrow middleware data… | §4.4 |
| FLT-DOM-10 | MUST | The `arrowRef` DOM handle is the arrow middleware's element; when no Arrow part has registered, a detached `<div>`… | §4.4 |
| FLT-DOM-11 | MUST | A portal MUST render a portal node `<div id="<generated id>" data-base-ui-portal="">` appended to the resolved… | §4.4 |
| FLT-DOM-12 | MUST | The portal's `nodeId` reported for `aria-owns` MUST be the id actually rendered on the portal node (an `id` prop or… | §4.4 |
| FLT-DOM-13 | MUST | A focus guard is a `<span>` with `tabindex="0"`, `data-base-ui-focus-guard=""`, a `data-type` of `"inside"` (rendered… | §4.4 |
| FLT-DOM-14 | MUST | The portal MUST render an *outside* focus guard before and after the portaled subtree (in the trigger's DOM position,… | §4.4 |
| FLT-DOM-15 | MUST | The focus manager MUST render an *inside* focus guard immediately before and after its children iff `!disabled &&… | §4.4 |
| FLT-DOM-16 | MUST | While a focus manager is open and enabled, every element outside its inside set (FLT-ARIA-03, marker variant) MUST… | §4.4 |
| FLT-DOM-17 | MUST | The focus manager MUST manage the floating focus element's `tabindex` per FLT-ALG-27 (only when its `role` contains… | §4.4 |
| FLT-DOM-18 | MUST | Disabling focus inside a portal (`disableFocusInside`) MUST set `tabindex="-1"` on every tabbable element in the portal… | §4.4 |
| FLT-DOM-19 | MUST | The safe-polygon pointer-events mutation MUST set inline `pointer-events: none` on the *scope element* and… | §4.4 |
| FLT-ARIA-01 | MUST | While a focus manager is enabled and open with `modal === true` (or in the untrapped-typeable-combobox case,… | §4.5 |
| FLT-ARIA-02 | MUST | With `modal === false` (and not the untrapped combobox case) no `aria-hidden` MUST be written anywhere; only the inert… | §4.5 |
| FLT-ARIA-03 | MUST | The inside sets are computed when the manager becomes open/enabled: the *marker inside set* is `[floatingElement,… | §4.5 |
| FLT-ARIA-04 | MUST | When the portal renders its outside guards (FLT-DOM-14) it MUST also render, between the before-outside guard and the… | §4.5 |
| FLT-ARIA-05 | MUST | Focus guards carry `aria-hidden="true"` (FLT-DOM-13) so they are invisible to assistive technology unless the… | §4.5 |
| FLT-KEY-01 | MUST | With `escapeKey` enabled (default `true`) and the popup open, a `keydown` whose `key` is `Escape` — observed on the… | §4.6 |
| FLT-KEY-02 | MUST | When `bubbles.escapeKey` is false (the default) and any *open* descendant tree node exists whose own… | §4.6 |
| FLT-KEY-03 | MUST | Escape MUST be ignored while an IME composition is in progress: either the event reports `isComposing`, or a… | §4.6 |
| FLT-KEY-04 | MUST | With a modal, enabled focus manager, a `Tab` keydown on the document MUST be prevented and stopped when the current… | §4.6 |
| FLT-KEY-05 | MUST | In modal mode, focusing the before-inside guard (Shift+Tab from the first tabbable) MUST move focus on the next… | §4.6 |
| FLT-KEY-06 | MUST | In non-modal mode inside a portal, focusing the before-inside guard MUST clear the return-focus suppression and then:… | §4.6 |
| FLT-KEY-07 | MUST | Focusing the portal's before-outside guard MUST focus the before-inside guard when focus arrived from outside the… | §4.6 |
| FLT-KEY-08 | MUST | A `keydown` on the anchor (from the `useClick` prop bag) MUST reset the remembered pointer type to "none", so that a… | §4.6 |
| FLT-KEY-09 | MUST | Keyboard close classification: when the close event is a `KeyboardEvent` the close type is `'keyboard'`; return focus… | §4.6 |
| FLT-PTR-01 | MUST | On anchor `pointerdown` the pointer type MUST be remembered as `'virtual'` when the event's pointer type is strictly… | §4.7 |
| FLT-PTR-02 | MUST | On anchor `mousedown`: the handler MUST return without effect when `button !== 0`, when `event` is `'click'` (the… | §4.7 |
| FLT-PTR-03 | MUST | On anchor `click`: with `event: 'mousedown-only'` the handler MUST do nothing; with `event: 'mousedown'` and a… | §4.7 |
| FLT-PTR-04 | MUST | Applying the next open value with reason `reason` (default `trigger-press`): when `nextOpen` is true, the remembered… | §4.7 |
| FLT-PTR-05 | MUST | `toggle: false` MUST keep an open popup open on repeated presses (FLT-ALG-16 step 3). | §4.7 |
| FLT-PTR-06 | MUST | With `stickIfOpen` true (default), a press on the anchor while the popup is open from a non-click-like event (hover or… | §4.7 |
| FLT-PTR-07 | MUST | The hook's anchor/trigger prop bag MUST contain `onPointerDown`/`onPointerEnter` (record the pointer type) and… | §4.7 |
| FLT-PTR-08 | MUST | While enabled and the "open check" holds — the floating element exists when the pointer type is mouse-like (so the… | §4.7 |
| FLT-PTR-09 | MUST | A virtual anchor MUST NOT be installed when the recorded open event is not mouse-based (e.g. focus); the DOM anchor's… | §4.7 |
| FLT-PTR-10 | MUST | Disabling the hook MUST reset the position anchor to the DOM anchor; unmounting it MUST set the position anchor to… | §4.7 |
| FLT-PTR-11 | MUST | `onPointerDown`/`onPointerEnter` MUST record the event's pointer type in the shared hover state; with `mouseOnly` true,… | §4.7 |
| FLT-PTR-12 | MUST | On trigger `mouseenter` (and the first `mousemove` when `move` is true), the hook MUST: cancel the pending open/close… | §4.7 |
| FLT-PTR-13 | MUST | "Closing transition" for FLT-PTR-12 is `isClosing()` when the option is given, else `transitionStatus === 'ending'` in… | §4.7 |
| FLT-PTR-14 | MUST | On trigger `mouseleave`: if the popup is click-like open (FLT-STA-06) the hook MUST only clear the safe-polygon… | §4.7 |
| FLT-PTR-15 | MUST | "Close with delay": with a resolved close delay > 0, start the shared open/close timer for that many ms and then close… | §4.7 |
| FLT-PTR-16 | MUST | On trigger `mousemove` (prop bag): after the `mouseOnly` check, when the popup is open, the move is over an inactive… | §4.7 |
| FLT-PTR-17 | MUST | On `openchange(false)` the trigger-side hook MUST detach the document `mousemove` handler, cancel the open/close and… | §4.7 |
| FLT-PTR-18 | MUST | With `guardStaleOpen` true, a native `mouseout` on the trigger whose `relatedTarget` is outside the trigger's subtree… | §4.7 |
| FLT-PTR-19 | MUST | `handleClose.__options` (the options the `safePolygon()` factory was called with, always including… | §4.7 |
| FLT-PTR-20 | MUST | Whenever the popup closes, the popup-side hook MUST reset the shared `pointerType` to undefined, clear… | §4.7 |
| FLT-PTR-21 | MUST | While enabled, open, the configured strategy has `blockPointerEvents`, the open event is a hover open (FLT-STA-05) and… | §4.7 |
| FLT-PTR-22 | MUST | A `pointerdown` inside the floating element (capture) MUST set `interactedInside` to "the target is an interactive… | §4.7 |
| FLT-PTR-23 | MUST | Floating `mouseenter` MUST cancel the shared open/close timer and the child-closed timer, unsubscribe from… | §4.7 |
| FLT-PTR-24 | MUST | Floating `mouseleave`: if this node's *parent* has open child nodes, subscribe to `floating.closed` and return; return… | §4.7 |
| FLT-PTR-25 | MUST | When a `floating.closed` tree event arrives while subscribed (FLT-PTR-24), and this node's parent no longer has open… | §4.7 |
| FLT-PTR-26 | MUST | Sloppy mode, mouse/pen: an outside `pointerdown` (not `touch`) MUST close the popup immediately per FLT-ALG-17. | §4.7 |
| FLT-PTR-27 | MUST | Sloppy mode, touch: an outside `touchstart` MUST record the start time and coordinates and arm "dismiss on mousedown";… | §4.7 |
| FLT-PTR-28 | MUST | Intentional mode: only outside `click` events close (FLT-ALG-17); a click MUST be *press-backed* — `detail === 0`, or… | §4.7 |
| FLT-PTR-29 | MUST | Intentional mode drag handling: a primary-button press that starts inside the floating element (capture… | §4.7 |
| FLT-PTR-30 | MUST | Reference press: when `referencePress()` returns true, a `pointerdown` or `click` on the anchor (prop bag) MUST close… | §4.7 |
| FLT-FOC-01 | MUST | On anchor `focus`: if focus is currently *blocked* for this element (FLT-FOC-02/03) the event MUST be ignored (a block… | §4.8 |
| FLT-FOC-02 | MUST | A window `blur` observed while the popup is closed and the DOM anchor is the active element MUST block the next `focus`… | §4.8 |
| FLT-FOC-03 | MUST | An `openchange` with reason `trigger-press` or `escape-key` MUST block the next `focus` on the current DOM anchor (so… | §4.8 |
| FLT-FOC-04 | MUST | On anchor `blur`, after 0 ms, the popup MUST close with reason `trigger-focus` unless: there is no `relatedTarget` and… | §4.8 |
| FLT-FOC-05 | MUST | The *floating focus element* is the floating element itself when it carries `data-base-ui-focusable`, else its first… | §4.8 |
| FLT-FOC-06 | MUST | When the manager becomes open and enabled with an HTML floating focus element, it MUST reset the recorded close type… | §4.8 |
| FLT-FOC-07 | MUST | When the manager stops being enabled, its floating focus element changes, or it unmounts, it MUST queue a microtask… | §4.8 |
| FLT-FOC-08 | MUST | Return-focus suppression is set when: the popup closes with reason `focus-out` and the details' trigger element is a… | §4.8 |
| FLT-FOC-09 | MUST | A global, weakly held stack of previously focused elements (max 20, body excluded, disconnected entries pruned on each… | §4.8 |
| FLT-FOC-10 | MUST | The close interaction type MUST be classified from the close event when `openchange(false)` fires: `KeyboardEvent` →… | §4.8 |
| FLT-FOC-11 | MUST | With `closeOnFocusOut` true, a `focusout` on the DOM anchor or the floating element MUST, in a microtask, close the… | §4.8 |
| FLT-FOC-12 | MUST | With `restoreFocus` set and `closeOnFocusOut` true (the restoration runs inside the focus-out listener, which is not… | §4.8 |
| FLT-FOC-13 | MUST | A document `pointerdown` (capture) on a descendant of an element carrying `data-base-ui-click-trigger`, and any… | §4.8 |
| FLT-FOC-14 | MUST | Tabbable and focusable element discovery MUST follow FLT-ALG-18 (composed tree including slotted content and open… | §4.8 |
| FLT-FOC-15 | MUST | "Next/previous tabbable after/before the anchor" (used by guards) MUST be computed over the whole body:… | §4.8 |
| FLT-FOC-16 | SHOULD | On WebKit engines, when the popup closes while a typeable element inside the floating element is the active element,… | §4.8 |
| FLT-FOC-17 | MUST | In a non-modal portal, when focus leaves the portal node to an element outside it (`focusout` with an outside… | §4.8 |
| FLT-FOC-18 | MUST | An enabled focus manager inside a portal MUST publish `{ modal, closeOnFocusOut, open, onOpenChange: store.setOpen,… | §4.8 |
| FLT-FOC-19 | MUST | With `disabled` true the manager MUST perform no focus management at all (no initial focus, no trap, no marking, no… | §4.8 |
| FLT-FOC-20 | MUST | `openInteractionType === null` (a programmatic open) MUST make the default return element prefer the previously focused… | §4.8 |
| FLT-FOC-21 | MUST | Return focus is *explicit* iff `explicitReturnFocus` is set (read at close time), else iff `returnFocus` is not a… | §4.8 |
| FLT-FOC-22 | MUST | `enqueueFocus(el, { preventScroll, sync, shouldFocus })` MUST focus synchronously when `sync`, else on the next… | §4.8 |
| FLT-DIS-01 | MUST | While open and enabled, the hook MUST register on the owner document (of the floating element): with `escapeKey`,… | §4.9 |
| FLT-DIS-02 | MUST | The capture-phase press listeners MUST NOT decide anything themselves: each attaches a one-shot listener for the same… | §4.9 |
| FLT-DIS-03 | MUST | Events of the wrong kind for the resolved mode MUST be ignored: in `intentional` mode everything except `click`; in… | §4.9 |
| FLT-DIS-04 | MUST | An outside-press candidate MUST be ignored when the inside marker is set (the press started or ended inside the… | §4.9 |
| FLT-DIS-05 | MUST | A press whose target is a registered trigger element, or inside one, MUST NOT close the popup (switching triggers is… | §4.9 |
| FLT-DIS-06 | MUST | A press on an element injected into the page after the popup opened MUST NOT close it: when at least one inert marker… | §4.9 |
| FLT-DIS-07 | MUST | A non-touch press on a scrollbar MUST NOT close the popup: for an HTML target, when the target is scrollable on an axis… | §4.9 |
| FLT-DIS-08 | MUST | A press inside the popup's own elements (floating element or DOM anchor, composed-path containment) or inside the… | §4.9 |
| FLT-DIS-09 | MUST | When `outsidePress` is a function it MUST be called with the native event after all the above checks, and a `false`… | §4.9 |
| FLT-DIS-10 | MUST | When any *open* descendant tree node has `__outsidePressBubbles === false`, the outside press MUST NOT close this popup… | §4.9 |
| FLT-DIS-11 | MUST | A surviving outside press MUST close the popup with reason `outside-press` and the native event, then clear the inside… | §4.9 |
| FLT-DIS-12 | MUST | The floating element's capture handlers for `click`, `mousedown`, `pointerdown`, `mouseup`, `touchend`, `touchmove`… | §4.9 |
| FLT-DIS-13 | MUST | The floating element's bubble-phase `pointerdown`/`mousedown` handlers MUST record "press start prevented" when the… | §4.9 |
| FLT-DIS-14 | MUST | Session boundaries: an `openchange(false)` MUST forget the observed press (FLT-PTR-28) even if the popup is reopened in… | §4.9 |
| FLT-DIS-15 | MUST | While open and enabled the hook MUST publish its normalized `bubbles` flags into context data as `__escapeKeyBubbles`… | §4.9 |
| FLT-DIS-16 | MUST | `escapeKey: false` MUST disable all Escape handling (document listener and prop-bag handlers); `outsidePress: false`… | §4.9 |
| FLT-EVT-01 | MUST | The reasons this slice emits, and their origin: `trigger-press` (`useClick` default; `useDismiss` reference press),… | §4.10 |
| FLT-EVT-02 | MUST | Every open-state change dispatched through a root store (directly, or by the popup store in sync-only mode) MUST emit… | §4.10 |
| FLT-EVT-03 | MUST | Hover closes MUST emit `floating.closed` on the floating tree's event bus with the mouse event, after calling… | §4.10 |
| FLT-EVT-04 | MUST | All change details MUST be created with the EVD constructor `createChangeEventDetails(reason, nativeEvent, trigger?)`;… | §4.10 |
| FLT-EVT-05 | MUST | Escape cancel semantics: calling `cancel()` on the details inside `onOpenChange` MUST leave the keydown's default… | §4.10 |
| FLT-EVT-06 | MUST | Timers used by this slice and their values (all restartable, cancelled on close/disable): `useClick` touch-open… | §4.10 |
| FLT-EVT-07 | MUST | The delay group MUST close the previously active tooltip (through its stored `onOpenChange`) with reason `none` and no… | §4.10 |
| FLT-ANI-01 | MUST | Delay-group *instant phase*: when a tooltip opens while another member of the same group is the active one, both the… | §4.11 |
| FLT-ANI-02 | MUST | A Positioner MUST be invisible (`opacity: 0`, FLT-DOM-02) and positioned `fixed` at the viewport origin until the first… | §4.11 |
| FLT-ANI-03 | MUST | Adaptive origin (FLT-ALG-12): when the floating element has a non-zero CSS `transition-duration`, a `left`-side… | §4.11 |
| FLT-ANI-04 | MUST | Lazy flip: with `lazyFlip` truthy, once the popup is mounted and positioned on a side different from the requested one… | §4.11 |
| FLT-ANI-05 | MUST | Transition-disabling styles for the Positioner come from TRN (`getDisabledMountTransitionStyles(transitionStatus)`) and… | §4.11 |
| FLT-POS-01 | MUST | The requested placement is `<physicalSide>` when `align === 'center'`, else `<physicalSide>-<align>`, where… | §4.12 |
| FLT-POS-02 | MUST | The middleware pipeline MUST be, in order: `inline` (if supplied), `offset`, then either `shift, flip` or `flip, shift`… | §4.12 |
| FLT-POS-03 | MUST | `shift` MUST run before `flip` when `collisionAvoidance.side === 'shift'`, or `collisionAvoidance.align === 'shift'`,… | §4.12 |
| FLT-POS-04 | MUST | `flip` MUST be omitted when `collisionAvoidance.side === 'none'`; otherwise configured with: boundary/rootBoundary from… | §4.12 |
| FLT-POS-05 | MUST | `shift` MUST be omitted when `collisionAvoidance.align === 'none'` and `collisionAvoidance.side !== 'shift'`; otherwise… | §4.12 |
| FLT-POS-06 | MUST | `offset` MUST be configured with a function producing `{ mainAxis: sideOffset, crossAxis: alignOffset, alignmentAxis:… | §4.12 |
| FLT-POS-07 | MUST | `size` MUST be configured with the collision boundary and normalized padding and an `apply` that writes the CSS… | §4.12 |
| FLT-POS-08 | MUST | The `arrow` middleware (Base UI fork, FLT-ALG-09) MUST be configured with the registered arrow element (or the… | §4.12 |
| FLT-POS-09 | MUST | `anchorHidden` MUST be `true` iff the Base UI `hide` middleware (FLT-ALG-10) reports `referenceHidden`: the anchor is… | §4.12 |
| FLT-POS-10 | MUST | The rendered outputs after each pass MUST be: `physicalSide` = side of the rendered placement; `side` = its logical… | §4.12 |
| FLT-POS-11 | MUST | Anchor tracking options MUST be `ancestorScroll = !disableAnchorTracking`, `elementResize = !disableAnchorTracking &&… | §4.12 |
| FLT-POS-12 | MUST | The engine strategy MUST equal `positionMethod`; the session's `open` input is `mounted` for kept-mounted popups and… | §4.12 |
| FLT-POS-13 | MUST | A position anchor that is a DOM element MUST be measured through its own `getBoundingClientRect`/`getClientRects`… | §4.12 |
| FLT-POS-14 | MUST | `sticky: true` MUST enable side-axis shifting without a limiter (the popup stays inside the boundary even when the… | §4.12 |
| FLT-POS-15 | MUST | The resting position after collision handling MUST be exactly `collisionPadding` from the colliding boundary edge (the… | §4.12 |
| FLT-POS-16 | MUST | The `update()` function returned by the session MUST recompute the position on demand (used by anchor tracking and by… | §4.12 |
| FLT-POS-17 | MUST | Changing `anchor`, `side`, `align`, offsets, collision options, `positionMethod` or the direction MUST re-run… | §4.12 |
| FLT-I18N-01 | MUST | `side: 'inline-end'` MUST resolve to `right` in LTR and `left` in RTL; `'inline-start'` to `left` in LTR and `right` in… | §4.13 |
| FLT-I18N-02 | MUST | The reported `side` (state, `data-side`, offset-function `side`) MUST be logical when the requested side was logical:… | §4.13 |
| FLT-I18N-03 | MUST | The engine's RTL flag (`platform.isRTL(floating)` = computed `direction === 'rtl'` of the floating element) MUST… | §4.13 |
| FLT-I18N-04 | MUST | Scrollbar-press detection (FLT-DIS-07) MUST use the RTL rule when the target's computed `direction` is `rtl`. | §4.13 |
| FLT-A11Y-01 | MUST | A pointer event is *virtual* (synthesized by assistive technology) iff, outside jsdom: (not Android and `width === 0 &&… | §4.14 |
| FLT-A11Y-02 | MUST | A mouse/click event is a *virtual click* iff `pointerType === ''` and `isTrusted`; or (Android and a `pointerType` is… | §4.14 |
| FLT-A11Y-03 | MUST | `useClick` MUST treat a virtual press as its own pointer type `'virtual'`: it is never dropped by `ignoreMouse`, opens… | §4.14 |
| FLT-A11Y-04 | MUST | The focus manager MUST focus the initial element synchronously (no frame delay) when the recorded open event is a… | §4.14 |
| FLT-A11Y-05 | MUST | Intentional-mode outside presses MUST accept press-less clicks (`detail === 0` or virtual click) so keyboard and… | §4.14 |
| FLT-A11Y-06 | MUST | On Mac Safari (`platform.os.mac && platform.engine.webkit`), `useFocus` MUST track modality with capture-phase window… | §4.14 |
| FLT-A11Y-07 | MUST | Outside-content hiding for modal popups MUST use `aria-hidden` (not `inert`) and MUST keep live regions and their… | §4.14 |
| FLT-A11Y-08 | MUST | `matchesFocusVisible(element)`: `true` for null/jsdom/throwing `matches`, else `element.matches(':focus-visible')`… | §4.14 |
| FLT-EDGE-01 | SHOULD | Outside production, creating a standalone root store with `elements.reference` set to a non-DOM (virtual) object MUST… | §4.15 |
| FLT-EDGE-02 | MUST | The engine MUST honor at most 50 middleware-requested resets per `computePosition` call; further resets are ignored and… | §4.15 |
| FLT-EDGE-03 | MUST | Anchor tracking MUST avoid resize feedback loops: when a resize notification is for the anchor, the floating element… | §4.15 |
| FLT-EDGE-04 | MUST | Layout-shift observation (FLT-ALG-13) MUST throttle the clipped-anchor case: when the first observation reports an… | §4.15 |
| FLT-EDGE-05 | MUST | Outside-marking bookkeeping (FLT-ALG-19) MUST be global per page: counters are kept per element across all open popups… | §4.15 |
| FLT-EDGE-06 | MUST | The previously-focused stack (FLT-FOC-09) MUST hold at most 20 weakly referenced entries, drop disconnected entries… | §4.15 |
| FLT-EDGE-07 | MUST | When the return-focus arming is torn down and re-armed in the same commit while the popup stays open (e.g. the DOM… | §4.15 |
| FLT-EDGE-08 | MUST | Kept-mounted popups MUST not leak interaction state between open sessions: the recorded close type and last interaction… | §4.15 |
| FLT-EDGE-09 | MUST | `useClientPoint` MUST build the virtual anchor around the handler's element when the root store has no DOM anchor yet,… | §4.15 |
| FLT-EDGE-10 | MUST | Delegated hover mode (handlers on a wrapper that contains the triggers): the trigger node for the open is the… | §4.15 |
| FLT-EDGE-11 | MUST | Delay group membership changes: unmounting a member that is not the active one MUST leave the group's active id, stored… | §4.15 |
| FLT-EDGE-12 | MUST | Changing a delay group's `delay` prop while a member is active MUST keep the group's open delay at 0 and adopt the new… | §4.15 |
| FLT-EDGE-13 | MUST | Each invocation of a safe-polygon strategy (`safePolygon()(context)`) MUST start with fresh traversal state… | §4.15 |
| FLT-EDGE-14 | MUST | Visually hidden elements (clip-path/1px/overflow hidden technique, FLT-DOM-13 style) MUST remain focusable and… | §4.15 |
| FLT-ALG-01 | MUST | The implementation reproduces the following procedure exactly (`computePosition`): | §5.1 |
| FLT-ALG-02 | MUST | The implementation reproduces the following procedure exactly (`coordsFromPlacement`): | §5.1 |
| FLT-ALG-03 | MUST | The implementation reproduces the following procedure exactly (`detectOverflow`), including the boundary resolution of… | §5.1 |
| FLT-ALG-04 | MUST | The implementation reproduces the following procedure exactly (`offset` middleware; Base UI always passes an object): | §5.1 |
| FLT-ALG-05 | MUST | The implementation reproduces the following procedure exactly (`flip` middleware): | §5.1 |
| FLT-ALG-06 | MUST | The implementation reproduces the following procedure exactly (`shift` middleware; note that here "main axis" is the… | §5.1 |
| FLT-ALG-07 | MUST | The implementation reproduces the following procedure exactly (`limitShift` limiter): | §5.1 |
| FLT-ALG-08 | MUST | The implementation reproduces the following procedure exactly (`size` middleware): | §5.1 |
| FLT-ALG-09 | MUST | The implementation reproduces the following procedure exactly (Base UI's `arrow` fork, which always measures against… | §5.2 |
| FLT-ALG-10 | MUST | The implementation reproduces the following procedure exactly (Base UI's `hide` replacement): | §5.2 |
| FLT-ALG-11 | MUST | The implementation reproduces the following procedure exactly (`transformOrigin` middleware; runs after `arrow`, before… | §5.2 |
| FLT-ALG-12 | MUST | The implementation reproduces the following procedure exactly (`adaptiveOrigin` middleware, supplied by PSH… | §5.2 |
| FLT-ALG-13 | MUST | The implementation reproduces the following procedure exactly (anchor tracking, `autoUpdate` + `observeMove`): | §5.2 |
| FLT-ALG-14 | MUST | The implementation reproduces the following procedure exactly (Base UI's pipeline assembly; it is the normative form of… | §5.2 |
| FLT-ALG-15 | MUST | The implementation reproduces the following procedure exactly (safe polygon; `safePolygon(options)` returns a strategy… | §5.3 |
| FLT-ALG-16 | MUST | The implementation reproduces the following procedure exactly (`useClick` next-open decision): | §5.3 |
| FLT-ALG-17 | MUST | The implementation reproduces the following procedure exactly (`useDismiss` outside-press decision, run on the event… | §5.3 |
| FLT-ALG-18 | MUST | The implementation reproduces the following procedure exactly (tabbable discovery): | §5.3 |
| FLT-ALG-19 | MUST | The implementation reproduces the following procedure exactly (outside marking, `markOthers(avoid, { ariaHidden =… | §5.3 |
| FLT-ALG-20 | MUST | The implementation reproduces the following procedure exactly (tree queries): | §5.3 |
| FLT-ALG-21 | MUST | The implementation reproduces the following procedure exactly (focus manager return-element resolution, evaluated when… | §5.3 |
| FLT-ALG-22 | MUST | The implementation reproduces the following predicate exactly (focus-out "moved to an unrelated node"; `rt` is the… | §5.3 |
| FLT-ALG-23 | MUST | The implementation reproduces the following procedure exactly (delay group state machine; `G` is the group's shared… | §5.3 |
| FLT-ALG-24 | MUST | The implementation reproduces the following procedure exactly (hover trigger `mouseenter` decision; `inst` is the… | §5.3 |
| FLT-ALG-25 | MUST | The implementation reproduces the following procedure exactly (cursor-anchoring virtual anchor; created with the DOM… | §5.3 |
| FLT-ALG-26 | MUST | The implementation reproduces the virtual-input predicates of FLT-A11Y-01 (`isVirtualPointerEvent`) and FLT-A11Y-02… | §5.3 |
| FLT-ALG-27 | MUST | The implementation reproduces the following procedure exactly (`handleTabIndex`, FLT-DOM-17): | §5.3 |
| FLT-ALG-28 | MUST | The implementation reproduces the following helper predicates exactly: `isEventTargetWithin(event, node)` = `node` is… | §5.3 |

## 10. Open questions

1. **`layoutViewport` root boundary vs `rootBoundary` default.** `useAnchorPositioning.ts:171, 308-314` passes `rootBoundary: shiftRootBoundary` (undefined unless a component sets `'layoutViewport'`). The engine resolves `undefined` to `'viewport'` (visual viewport); only `shift` receives the override — `flip`, `size` and `hide` always use the visual viewport. Whether the visual/layout mismatch between `shift` and the other middleware is intended (Context Menu, Navigation Menu) or an oversight is not stated in the source; FLT-POS-05 documents the behavior as-is.
2. **`collisionAvoidance.side: 'shift'` with the default `align: 'flip'`.** The type (`useAnchorPositioning.ts:100-112`, shift-mode `align?: 'shift' | 'none'`) says `align` only supports `'shift'`/`'none'` in shift mode but the runtime defaults it to `'flip'` (`:168`), which configures `flip({ mainAxis: false, crossAxis: 'alignment' })` after `shift`. Because `shift` already removed the alignment-axis overflow, the alignment flip rarely triggers but can when `limitShift` constrains the shift. Treated as an implementation detail here (FLT-POS-03/04); a port that wants identical pixel results must keep the ordering.
   Resolved: decisions/03-positioning-engine.md "Decision" — the pipeline assembly (FLT-ALG-14) is ported in JavaScript as written over the bundled 1.8.0 engine, so the ordering and the runtime `'flip'` default are kept.
3. **`hide` middleware boundary.** Base UI's `hide` calls `detectOverflow` with only `elementContext: 'reference'` (`utils/hideMiddleware.ts:10-12`), i.e. the engine defaults (`clippingAncestors` + `viewport`, padding 0) rather than the Positioner's `collisionBoundary`/`collisionPadding`. The test only compares against the engine's `hide()`, so this is deliberate parity, but `data-anchor-hidden` therefore ignores a custom `collisionBoundary`.
4. **Transform-origin RTL source.** FLT-ALG-11 reads `platform.isRTL(floating)` (computed `direction` of the positioner) for the aligned-arrowless case while every other logical computation uses the `DirectionProvider` value (`useAnchorPositioning.ts:420-425` comment: "must match what Floating UI placed with"). If a consumer sets `DirectionProvider` without a matching CSS `direction`, `data-side` and `--transform-origin` can disagree; not covered by tests.
   Resolved: decisions/03-positioning-engine.md "Decision" keeps both sources — logical-side resolution reads the C#-supplied `DirectionProvider` value and FLT-ALG-11 uses the engine's computed-style check — so the port reproduces Base UI including this divergence.
5. **`useFocus` blur close has no test**, and its "focus left the page" branch (`useFocus.ts:204-206`) relies on `activeElement === domReference` after a 0 ms timer; jsdom cannot model window focus loss, so FLT-FOC-04 is transcribed from source only.
6. **Safe polygon `tree`-less close context.** `HandleCloseContext.tree` is optional; when a trigger has no tree (`useFloatingTree` returns `null`), `hasOpenChildNode()` is always false. The Navigation Menu does **not** pass the tree explicitly: its `getHandleCloseContext` (`navigation-menu/trigger/NavigationMenuTrigger.tsx:503-509, 832-845`) returns `{ placement, elements, nodeId }`, and the hover hook adds `tree` from the ancestor `FloatingTree` (`useHoverReferenceInteraction.ts:96, 328-331`) exactly as for every other consumer. What Navigation Menu supplies explicitly is the node id, because inline nested menus have no positioning session to publish their node.
   Resolved: navigation-menu.md §16 Q10 — a port needs a tree per top-level Root, a node per Root, and the node id threaded into the hover close context; no tree-less path is required.
7. **Intentional-mode `detail` semantics across browsers.** FLT-PTR-28 keys on `MouseEvent.detail === 0` for press-less clicks. The test file notes Android assistive technology reports `pointerType: 'mouse'` with `detail: 0`; engines that report `detail: 1` for synthesized clicks would be classified as needing an observed press. Documented as-is.
8. **`FloatingPortalLite`** (`utils/FloatingPortalLite.tsx`) uses `useFloatingPortalNode` from this slice but is assigned to PSH by conventions §3.2; its test lives in `FloatingPortal.test.tsx` ("FloatingPortalLite forwards HTML props to the portal element") and is not mapped to an FLT requirement.
   Resolved: mapped to PSH-STR-12 (lite portal forwards props to the portal node element) in popup-store-and-handles.md.
9. **Source files possibly belonging elsewhere.** `floating-ui-react/utils/composite.ts` (incl. `isElementVisible`, used by FLT-ALG-18) and `gridNavigation.ts`, `useListNavigation.ts`, `useTypeahead.ts` are CMP; `utils/FocusGuard.tsx` is listed under PSH in conventions but its rendered shape is required here (FLT-DOM-13) — PSH should cite FLT-DOM-13 rather than restate it.
   Resolved: PSH-STR-08, PSH-DOM-13 and PSH-ARIA-05 now cite FLT-DOM-13 as the owner of the rendered shape.
10. **Escape from an inactive trigger inside another popup is listener-order dependent** (dialog.md §16 Q11). With an inactive `Dialog.Trigger` rendered inside an open Popover's popup, the Dialog's trigger-level `keydown` handler from its dismiss reference bag (FLT-KEY-01) runs first because it is attached to the focused element, and its `stopPropagation()` is what keeps the enclosing Popover's floating-element handler and document listener from also closing the Popover (`floating-ui-react/hooks/useDismiss.ts:201-232, 721, 777`; `dialog/root/DialogRoot.detached-triggers.test.tsx:114-153`). The test asserts the Dialog closes on every reopen while the Popover stays open; no requirement in this document guarantees that outcome independently of handler order, so a port that attaches the handlers differently (e.g. capture-phase document listeners first) would close both. Recorded, not normative.
   Recorded as observed (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): the port's dismissal listeners are element listeners, so the inactive trigger's `keydown` listener runs first on the focused element and stops the key, as in Base UI; port title "port: an Escape on an inactive trigger of an open popup, inside another open popup, closes that popup alone" in `interactions.test.ts`.
11. **Possible double `onOpenChange` on Escape with `allowPropagation()`** (popover.md §16 Q7). When the consumer calls `allowPropagation()` for an Escape close handled by the floating element's `keydown` handler, `stopPropagation()` is skipped (FLT-KEY-01), so the same keydown also reaches the document listener registered by the same `useDismiss` instance (`useDismiss.ts:721`). That listener's `open` guard (`useDismiss.ts:203`) reads the value of the last committed render; if the close has not been committed before the event reaches the document (e.g. a script-dispatched event, where no microtask checkpoint occurs between listeners), the close is requested a second time and `onOpenChange(false, …)` fires twice. No test covers it (`tooltip/root/TooltipRoot.test.tsx` "allowPropagation() prevents stopPropagation on Escape while still closing" does not count callbacks). A port SHOULD guard so that one keydown produces at most one close request; recorded here and in EVD §10 Q11 as a suspected defect, not a requirement.
   Resolved (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, the verdict `Prevent`, chosen by the user 2026-10-27): one close request per keydown. A symbol on the keydown names the sessions that asked `escape` for it, so the same session's document listener skips a keydown its floating or reference listener handled, and a session that requested a close is disarmed until the relay brings the next open, so a script-dispatched second Escape before the commit requests nothing. POP §16 Q7 is resolved by the same note.
12. **`closeWithDelay` requests a close on a `mouseleave` while already closed** (tooltip.md §16 Q14, preview-card.md §16 Q5, popover.md §16 Q12). The trigger `mouseleave` handler (`floating-ui-react/hooks/useHoverReferenceInteraction.ts:302-360`) does not check `store.select('open')` before its non-safe-polygon branch: when no close handler applies (no `handleClose`, or no close context yet, `:315,321`), it calls `closeWithDelay(event)` (`:352-359`), which runs `store.setOpen(false, trigger-hover)` immediately for a zero close delay or after the delay otherwise (`:194-206`). On a popup that is closed (e.g. the pointer left before the open delay elapsed), this replaces the pending open timer and issues an `onOpenChange(false, …)` for a popup that never opened (the shared `applyPopupOpenChange` has no equality guard, `utils/popups/popupStoreUtils.ts:273`). Intended (cancel-the-open) or a spurious callback? Not specified as a requirement; the open cancellation is.
   Resolved (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27 "untested quirks", chosen by the user 2026-10-27): a trigger `mouseleave` on a closed popup cancels the pending open only and requests no close; a hover open still awaiting its commit counts as open. Port title "port: a trigger mouseleave while the popup is closed cancels the pending open and requests no close". The per-family notes are tracks 5–7's.
13. **`dataRef.current.floatingContext` is never cleared.** It is assigned in a layout effect of `useFloating` once a Positioner renders (`floating-ui-react/hooks/useFloating.ts:112-113`); the root store creates `dataRef` as `{ current: {} }` (`floating-ui-react/components/FloatingRootStore.ts:77`) and no code resets the field when the Positioner unmounts. Readers (`useHoverReferenceInteraction.ts:315`, `useDismiss.ts:171,377`, `useFocus.ts:216`, `useHoverFloatingInteraction.ts:209`, `components/FloatingFocusManager.tsx:288`) therefore see the last positioning session's context — stale `refs.floating` and `nodeId` — after the popup has unmounted. Its presence or absence also selects the hover-leave branch of Q12, so the same leave behaves differently before and after the first render of a Positioner. Unclear whether the stale value is relied upon; not specified.
   Resolved (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); design point 9, settled by the orchestrator 2026-10-27 as recommended, under ADR 04 Amendments 2026-10-18 (an untested quirk without a requirement against it is mirrored)): mirrored. The session publishes its context when the floating handle attaches and never clears it.
14. **`useDismiss` tests for a `capture` option that does not exist.** `useDismiss.test.tsx:1009-1180` ("prop: capture") runs `normalizeProp` cases and two nested-dialog cases, but `UseDismissProps` (`floating-ui-react/hooks/useDismiss.ts:46-109`) has no `capture` option in 1.9.0: the outside-press listeners are always capture phase and the Escape listener always bubble phase (`useDismiss.ts:718-746`). The `normalizeProp` cases exercise the same function as `bubbles` (FLT-API-01) and the nested-dialog cases pass only because of the default `bubbles` behaviour (FLT-KEY-02, FLT-DIS-10), which is where they are cited. No `capture` requirement is issued.
   Kept (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119)): the `capture` titles run verbatim in `interactions.test.ts` under the default `bubbles`.
15. **Implementation revision of 2026-10-27 (M4 track 2, positioning, [issue 118](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/118); ADR 03, ADR 04 Amendments 2026-10-27; ADR 13 Amendments 2026-10-22).** Dated 2026-10-27. The `positioning` mechanism module (`src/Blazix.Primitives/js/positioning.ts`) runs over the bundled engine through the wrapper that carries its licence banner (`js/floating-ui.ts`). It is joined by `PositioningTwin`, floating positioning's C# half in `src/Blazix.Primitives/Floating/` (the public types of §3.1, the internal Positioner and Arrow behaviours) and `dom`'s layered-style writer. They ship the 60 IDs of the `positioning` bucket on test-only hosts: `TestPositioner`/`TestPositionerArrow` at L2, and `FixturePositioner`/`FixturePositionerArrow` at L4 with the fixtures `positioning-suite`, `-origin`, `-lifecycle` and the prerendered `-prerender`. No public part ships. Design points 1–8 of the track were settled by the orchestrator 2026-10-27 (ADR 04): the writer's bands, the `Offset` record, `readPlacement`/`update`, one report per change, the Arrow's handover, the titles on `FixturePositioner`, the open input from the applied batch and the references array; the shared token field is `SessionToken`. Amendments under conventions §3.5, each with its dated port note in the bullet:
   - green at L3 on the module: FLT-STA-07, FLT-API-11, FLT-API-17, FLT-POS-02 (but its `inline` clause), FLT-POS-05, FLT-POS-09, FLT-POS-13, FLT-ALG-10, FLT-ALG-14;
   - satisfied by the dependency (ADR 03), each with its note: FLT-ALG-01..08 and FLT-EDGE-02..04. The cited titles of FLT-ALG-02, 04, 05, 06 and 08 still run in the L4 suite or in the page, and FLT-ALG-03's in the L3 `hide` titles;
   - re-tagged [L3] → [L4], the titles being browser-only: FLT-ALG-11 (the 19 transform-origin titles) and FLT-ALG-12 (the suite's Viewport titles);
   - green at L4 on `FixturePositioner`, both modes:
     - the suite's non-Viewport rows FLT-API-08, FLT-API-12, FLT-DOM-01, FLT-DOM-03, FLT-DOM-05, FLT-DOM-08, FLT-POS-01, FLT-POS-04, FLT-POS-06, FLT-POS-07, FLT-POS-10, FLT-POS-15, FLT-I18N-01, FLT-I18N-02;
     - over the `AdaptiveOrigin` stand-in for the Viewport: FLT-DOM-04, FLT-ANI-03, FLT-POS-17;
     - through the 19 transform-origin titles: FLT-DOM-07, FLT-DOM-10;
     - in the page: FLT-ANI-04 and FLT-API-10;
     - under port titles: FLT-DOM-02, FLT-ANI-02;
   - the `none` rows with a port title: FLT-STA-09, FLT-API-09, FLT-DOM-06, FLT-DOM-09, FLT-ANI-05, FLT-POS-03, FLT-POS-08, FLT-POS-11, FLT-POS-12, FLT-POS-14, FLT-I18N-03, FLT-ALG-09;
   - met on both modes, asserted per mode (ADR 13 §1): FLT-POS-16, FLT-ALG-13; FLT-DOM-08's relaxation row is asserted per mode under `page.routeWebSocket`;
   - restated titles: the suite's four "reads $description inside the function" titles, as "applies the override for $description" per prop (FLT-API-12's note); port notes on §3.1, FLT-API-11's function clause and FLT-API-12 (ADR 08 Amendments 2026-10-27, chosen by the user);
   - open by ID:
     - FLT-POS-02's `inline` clause waits for M4 track 7 (PreviewCard).
   - each Positioner family proves again, through its own parts: the suite's 16 titles (Popover, Tooltip, PreviewCard, each with its Viewport), and Popover the 19 transform-origin titles if it runs them;
   - stale text corrected: §8's "Attribute ownership of positioned parts" bullet (settled by ADR 04 Amendments 2026-10-18 and 2026-10-27) and §9's "220 requirements" (221).
   - dated 2026-10-28, once track 1 ([issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117), PR 126) was on `main`: the `ElementReference` cases of FLT-API-11 and FLT-API-09 (`AnchorRef`, `BoundaryRefs`, indexes into the references after the options) and the first attach of a Positioner under a Portal, which awaits the portal's move (FLT-DOM-02, FLT-STA-09; the Toolbar first-attach precedent, settled by the orchestrator 2026-10-27), are green at L3, L2 and L4 on both modes, each with its dated note;
   - dated 2026-10-28, M4 track 2's follow-up pull request after M4 track 3 ([issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119), PR 129) was on `main`: FLT-STA-08 is green at L3; the handle takes the session of `SessionToken`, publishes each committed placement and follows the session's position reference, which an anchor outranks (its dated note). Its L4 proof comes with Popover's Positioner (track 5);
   - dated 2026-10-28, M4 track 2's second follow-up pull request (PR 138): an anchor that does not resolve falls back as Base UI's `null` anchor does, to the session's position reference while set, then the trigger (FLT-API-11's dated note);
   - No new Server relaxation beyond the RND-STA-01 row (the mirrored side, align, anchorHidden and uncentered) and the `update`-carried values row. The document stays `approved`.
16. **Implementation revision (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27; ADR 13 Amendments 2026-10-22).** Dated 2026-10-27; numbered 16 at the merge after track 2 (item 15). The `interactions` module (`src/Blazix.Primitives/js/interactions.ts`), `InteractionsTwin`, the `Floating/` interactions behaviours and the token form of `attach` ship the track's rows, proven on test-only hosts. Each amended row carries its dated note.
   - Green at L3 (`interactions.test.ts`, 224 titles with the port titles): STR-02, 03, 06, 07, 08; STA-01..04; API-01..04, 06, 07, 14..16; KEY-02, 03; PTR-02..10, 12..17, 19, 23, 24 (first title), 26..30; FOC-01, 02; DIS-01..04, 08..16; EVT-01, 02, 04, 07; ANI-01; A11Y-01, 02, 05; EDGE-09, 10, 11, 13; ALG-15, 16, 17, 20, 23..26. The `none` rows are built and most have port titles (STR-04, STR-05, STR-09, STA-06, DOM-19, KEY-08, PTR-11, PTR-18, PTR-20..22, PTR-25, FOC-04, DIS-05, EVT-03, A11Y-06, EDGE-12, ALG-28); DIS-07 (the scrollbar press) and I18N-04 need layout and stay without a title; A11Y-08's `matches` branch needs a real engine. Also PSH-PTR-08 and PSH-PTR-29.
   - Green at L4 on the press host, both modes (`interactions.spec.ts`): the press veto and the attach order of a trigger's `button` and `interactions` handles and of its session.
   - Green at L4 on track 1's popup host, both modes (2026-10-28, after PR 126; `interactions.spec.ts`): KEY-01 and EVT-05, and PSH-DIS-01's Escape titles in `popup.spec.ts`.
   - Open, waiting for track 4: STA-05, PTR-01, PTR-24's second title, FOC-03, A11Y-03 (their titles are in `FloatingFocusManager.test.tsx`) and DIS-06 (two of its three titles are green; "dismisses when clicking outside a shared shadow root while focus is managed" needs the focus manager). Open to track 5 (2026-10-28, on this track's recommendation, track 2 being on `main`): the attach-order title of the Positioner's two handles. Popover's Positioner is the first part that composes both `PositionerBehavior` and `InteractionsFloatingBehavior`. Open to M5: STR-11 and EDGE-01 (the standalone root store).
   - Green 2026-10-28, with M4 track 4 ([issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120)): STA-05, PTR-24's second title (restated without the safe polygon), FOC-03 and DIS-06's third title, at L3 in `focus-manager.test.ts` "with the interactions session" (this module's harness and the focus manager in one page; each row has its dated note). PTR-01 and A11Y-03 stay open, to M4 track 5: their cited title observes neither row's clause (no `pointerdown` is sent, so no press is typed `'virtual'`, and jsdom's `isVirtualPointerEvent` is `false`); Popover's trigger at L4 is the first place a virtual press can be dispatched.
   - Built early, no M4 consumer, proven at L3 (ADR 12 Amendments 2026-10-27 item 4): `useClick`'s mousedown paths, `toggle: false`, `ignoreMouse`, `touchOpenDelay`, `input-press`; the hover's `blockPointerEvents`, `move: true`, `guardStaleOpen`. M5's: Menu's `blockPointerEvents` and the collision with a trigger's C#-rendered `style`, NavigationMenu's `getHandleCloseContext` and `getScope`, Combobox's `input-press`.
   - Issue 26: the `[JSImport]` transport measured (`harness.spec.ts`, `@verifies ADR-16/2`); the finding is in ADR 16.
17. **Implementation revision of 2026-10-27 (M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 and ADR 12 Amendments 2026-10-27; ADR 13 Amendments 2026-10-22).** Dated 2026-10-27; revised 2026-10-28, when track 1 was on `main`; numbered 17 at the merge after tracks 2 and 3 (items 15 and 16); revised 2026-10-29 by the follow-up PR 145. The `focus-manager` module (`src/Blazix.Primitives/js/focus-manager.ts`) and `FocusManagerTwin` (`Interop/`) ship the 40 IDs fact sheet A allocates to the module, proven on the test-only host `FixtureFocusManaged` (the `focus-manager-*` fixtures, both modes) and at L3 with the test playing C#. Amendments under conventions §3.5, each with its dated port note in the bullet:
   - green at their layers: FLT-ALG-18, ALG-19, ALG-21, ALG-27, ARIA-01 (but its combobox clause), ARIA-02, ARIA-03 (but its combobox clause), DOM-16, DOM-17, KEY-04, KEY-05, KEY-09, FOC-05, FOC-09, FOC-12 [L4], FOC-14, FOC-15, FOC-16, FOC-19, FOC-20, FOC-21, FOC-22, A11Y-07, EDGE-05, EDGE-06, EDGE-07, EDGE-14; FOC-10 restated (the recording at L3, the classification C#'s, `CloseTypeClassifier` at L1 and L2 from 2026-10-28);
   - re-tagged: FLT-FOC-11 [L3] → [L4] (its ADR 01 row; the review of PR 128), FLT-DOM-15 [L4] → [L3] (a jsdom-only title; its C# render rule is at [L2] on `FocusManagerBehavior` from 2026-10-28, and each family proves it again); FLT-FOC-18 keeps [L2] with track 1's cited title, green on `main`; FLT-FOC-16, FOC-20 and FOC-22 had no test and carry port titles at L3; FLT-FOC-16's statement no longer contradicts its SHOULD level (fact sheet A contradiction 9, `_qa-notes.md`);
   - green from 2026-10-28, when track 1 was on `main` (aed2631, merged into this branch): the options' `Relay` member (`OpenChangeRelay`; a coalesced close needs nothing of the focus manager, since Base UI's re-arm in the same commit cancels the return job and clears its suppression, FLT-EDGE-07); the element-index options' C# transport through `TwinReferences` (`InitialFocusElement`, `ReturnFocusElement`, `PreviousFocusableElement`, `NextFocusableElement`, `InsideElements`; L2, each green at L3); the twin's first attach inside a portal awaiting `PopupPortalContext.WhenMoved(context)` (L2; settled by the orchestrator 2026-10-27, the Toolbar first-attach precedent); FLT-FOC-18 (`PopupPortalContext.Publish(PortalFocusPublication(Modal, Open, CloseOnFocusOut))` at L2, the publication on the portal node at L3, both at L4); KEY-06; API-05's "does not insert fallback element when return element is falsy" (restated) and the element-target transport; FOC-07's "preserves tabbable context next to reference element if removed (modal)" and "(non-modal)"; EDGE-08's and FOC-13's "clears outside pointer state between keep-mounted open sessions"; DOM-15's portaled clause and its C# render rule at L2; the C# half in `Floating/` (`FocusManagerBehavior`, `FocusManagerInputs`, `CloseTypeClassifier`: the classifier's L1 titles, the transport titles and ADR 16 rule 7's "equal inputs allocate nothing" at L2). Track 1's rows wired here (design point 5): KEY-07, FOC-17, DOM-14's cited title [L4], DOM-18's L4 title (the stash on composite items, design point 4);
   - green from 2026-10-28, when track 3 was on `main` (1a76ac0, merged into this branch; the manager joins the interactions session by `SessionToken`, and reads its `openchange`, `dataRef`, anchor, registered triggers and tree): the options' `SessionToken` member (L2); FOC-07's "always returns to the reference for nested elements"; ALG-22's "mixed modality and nesting" and its tree clause (port title); A11Y-04's and FOC-06's "focuses without a frame delay when a screen reader press opens it"; FOC-08's outside-press virtual branch (port title); FOC-11's registered-trigger clause (port title); FOC-13's anchor and shared inside marker. Track 3's rows that waited for this one, in §10 item 16: STA-05, PTR-24's second title, FOC-03 and DIS-06's third title, each at L3 in `focus-manager.test.ts` "with the interactions session"; the tree clauses of FOC-07 (return focus) and ALG-22 (the ancestor) under port titles;
   - open to M4 track 5: FOC-08's focus-guard trigger clause (the session resolves a relayed trigger id among its registered triggers only, so the `popup` module's guard close names the guard through `nameRelayTrigger`, ADR 04 Amendments 2026-10-28; PSH-KEY-07); track 3's PTR-01 and A11Y-03 (the title both cite observes only A11Y-04's clause; a virtual press needs a real browser's 0×0 `pointerdown`, at L4 on Popover's trigger). Revised 2026-10-29 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): FOC-08's clause is green, through `interactions.test.ts` "port: a named element is the trigger element of the next relayed focus-out close without a trigger, and of that close only" and `popup.test.ts` "port: on Server, focusing the pre-guard names it, focuses the tabbable before the trigger and reports the guard" (L3), and `PopoverFocusTests` "port: a focused trigger guard closes the popover with focus-out, its event and the guard as the trigger" (L2). PTR-01 and A11Y-03 move to M5: Popover's trigger uses the `click` event and no `ignoreMouse`, so a press typed `'virtual'` changes nothing it does, and the virtual-click title (`popover.spec.ts` "dismisses a keyboard-reopened popup on a virtual outside click after a touch session") dispatches no `pointerdown` on an anchor; Menu's trigger (`mousedown`) observes both;
   - open to M5 (ADR 12 Amendments 2026-10-27 item 4): the untrapped-typeable-combobox clauses of DOM-15, FOC-11, KEY-04, ARIA-01 and ARIA-03, with "floating element closes upon tabbing out of modal combobox", "untrapped typeable combobox closes on second tab sequence (click -> tab -> click -> tab)" and "untrapped combobox creates non-modal focus management";
   - the design points of the focus-manager track: 1 chosen by the user 2026-10-27 (the options from the commit path; return focus also on the element's removal; the composite re-pick, proven by the attach-order title); 3, 4, 6 and 8 and FLT-FOC-11's relaxation row settled by the orchestrator 2026-10-27 (ADR 04 Amendments 2026-10-27). Built: 1, 3 (the publication, and `portal.ts`'s routing with design point 5, 2026-10-28), 4 (the `composite.ts` edit, CMP-DOM-01's note), 6 (`Floating/`, 2026-10-28) and 8;
   - to prove through the families: Popover (track 5): POP-FOC-*, POP-KEY-05..11, POP-ARIA-09; Dialog (track 8): DLG-FOC-*, DLG-KEY-06..08, DLG-ARIA-11; AlertDialog (track 9) through Dialog's behaviours;
   - no new decision point: the module's prevents are unconditional and its closes are requests; the page-level writes are recorded in `js/CONTEXT.md` (ADR 04 §2's note of 2026-10-27).
   - revised 2026-10-29 (M4 track 4's follow-up, [PR 145](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/145); ADR 01 Amendments 2026-10-29; M4 track 5's Gap 12): FOC-11's focus-out report waits until the focused element is known (its port note of that date, and its Server-relaxation bullet), with an L3 port title and a WebAssembly-only L4 port title on `focus-manager-non-modal`'s clearing switch; the family-level proof is Popover's "allows controlling the open state and active trigger programmatically" (PR 136, both modes);
   - The document stays `approved` (ADR 13 Amendments 2026-10-22); its README row is revised 2026-10-29.
