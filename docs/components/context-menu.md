# Context Menu

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/context-menu/` (3 own parts: `root/`, `trigger/`, `positioner/`; every other part re-exported from `menu/` or `separator/`) |
| Tests | `packages/react/src/context-menu/**/*.test.tsx` (3 files: `root/ContextMenuRoot.test.tsx`, `root/ContextMenuRoot.non-mac.test.tsx`, `trigger/ContextMenuTrigger.test.tsx`); context-menu cases in `menu/positioner/MenuPositioner.test.tsx` |
| Docs page | `docs/src/app/(docs)/react/components/context-menu/page.mdx` |
| Prefix | `CTX` |
| Status | approved |
| Builds on | [menu](menu.md) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) (inherited through Menu), [button-behavior](../architecture/button-behavior.md) (inherited item behavior), [shared-utilities](../architecture/shared-utilities.md); [separator](separator.md) for `ContextMenu.Separator` |

## 1. Overview

Context Menu is a [Menu](menu.md) that opens at the pointer instead of below a button. The consumer wraps an area of the page in `ContextMenu.Trigger`; a right click (any `contextmenu` event) inside that area, or a 500 ms single-finger long press on a touch screen, opens the menu with its top-left corner just beside the pointer. The browser's own context menu is suppressed over the area and over the menu's backdrops. Once open, the popup behaves like a Menu popup — the same items, groups, radio/checkbox items, submenus, keyboard navigation, typeahead and item activation — with these differences: it is always modal (focus is trapped, outside content is `aria-hidden`, page scroll is locked, an invisible full-viewport backdrop intercepts presses); focus returns to whatever element had focus before the menu opened, because there is no trigger button; on macOS the user may keep the right button held, move onto an item and release to activate it; and on every platform a press-and-hold that is released outside the menu after 500 ms cancels the open.

Only three parts are ContextMenu's own: **Root** (a thin wrapper that creates the pointer anchor and the shared context, then renders a Menu root), **Trigger** (the right-click / long-press area) and **Positioner** (the Menu positioner with context-menu defaults and a narrower public prop set). Every other part is the Menu part of the same name, or the generic [Separator](separator.md). The doc therefore adopts the Menu requirements wholesale (CTX-STR-02) and specifies individually only what ContextMenu adds or overrides. The official docs recommend using a context menu only as an enhancement alongside a visible control that exposes the same actions (`components/context-menu/page.mdx` §Usage guidelines).

Base component: [menu](menu.md). Derived components: none. ContextMenu has no detached triggers, no handle, no `Viewport` and no filtering parts.

## 2. Anatomy

```
<ContextMenu.Root>                       required, 1                 no element
  <ContextMenu.Trigger>                  optional, 0..n              div
  <ContextMenu.Portal>                   required for a popup, 0..1  div (portal node)            = Menu.Portal
    <ContextMenu.Backdrop>               optional, 0..1              div                          = Menu.Backdrop
    <ContextMenu.Positioner>             required inside Portal, 1   div                          (own wrapper of Menu.Positioner)
      <ContextMenu.Popup>                required inside Positioner  div                          = Menu.Popup
        <ContextMenu.Arrow>              optional, 0..1              div                          = Menu.Arrow
        <ContextMenu.Item>               0..n                        div                          = Menu.Item
        <ContextMenu.LinkItem>           0..n                        a                            = Menu.LinkItem
        <ContextMenu.Separator>          0..n                        div                          = Separator (generic)
        <ContextMenu.SubmenuRoot>        0..n                        no element                   = Menu.SubmenuRoot
          <ContextMenu.SubmenuTrigger>   required, 1                 div                          = Menu.SubmenuTrigger
          <ContextMenu.Portal> … <ContextMenu.Positioner> … <ContextMenu.Popup> …   (recursive)
        <ContextMenu.Group>              0..n                        div                          = Menu.Group
          <ContextMenu.GroupLabel>       0..1                        div                          = Menu.GroupLabel
        <ContextMenu.RadioGroup>         0..n                        div                          = Menu.RadioGroup
          <ContextMenu.RadioItem>        0..n                        div                          = Menu.RadioItem
            <ContextMenu.RadioItemIndicator>     0..1                span                         = Menu.RadioItemIndicator
        <ContextMenu.CheckboxItem>       0..n                        div                          = Menu.CheckboxItem
          <ContextMenu.CheckboxItemIndicator>    0..1                span                         = Menu.CheckboxItemIndicator
```

- **CTX-STR-01** — MUST. The `ContextMenu` namespace exposes exactly these parts. Own implementations: `Root`, `Trigger`, `Positioner` (the Menu positioner component itself, with the context-menu prop contract of §3.3). Re-exported unchanged from Menu: `Portal`, `Backdrop`, `Popup`, `Arrow`, `Group`, `GroupLabel`, `Item`, `LinkItem`, `CheckboxItem`, `CheckboxItemIndicator`, `RadioGroup`, `RadioItem`, `RadioItemIndicator`, `SubmenuRoot`, `SubmenuTrigger`. Re-exported from the standalone Separator component: `Separator`. The Menu parts `Trigger`, `Viewport`, `List`, `FilterProvider`, `Input`, `Clear`, `Empty`, `createHandle`/`Handle` and `useFilter` are **not** part of the ContextMenu namespace.
  Source: `context-menu/index.parts.ts:1-21`; `context-menu/positioner/ContextMenuPositioner.tsx:18-20`; `context-menu/index.ts:1-81`. Docs: `components/context-menu/page.mdx` §Anatomy. Test: [L2] `ContextMenuTrigger.test.tsx` describeConformance; `ContextMenuRoot.test.tsx` (all tests use the re-exported parts) (L4 for the gesture-driven citations).
- **CTX-STR-02** — MUST. Every re-exported part, and the Menu machinery the Root wraps, satisfies every MNU requirement whose subject is that part or that machinery, with the substitutions below, **except** the MNU IDs listed under "Not adopted" (they concern parts or features ContextMenu does not have) and under "Overridden" (replaced by the CTX requirement named). The §13 matrix of this document may cite adopted MNU IDs directly.

  Substitutions:

  | Where MNU says | Read |
  |---|---|
  | `Menu.<Part>` for a re-exported part | `ContextMenu.<Part>` |
  | `Menu.Root` / "the Root" (top-level) | `ContextMenu.Root` (the menu root it renders, parent kind `'context-menu'`) |
  | `Menu.Root.ChangeEventDetails` / `…Reason` / `…Actions` | `ContextMenu.Root.ChangeEventDetails` / `…Reason` / `…Actions` (identical shapes) |
  | "the active trigger" as the element focus returns to | the element that was focused before the menu opened (CTX-FOC-03) |
  | "the active trigger" as the positioning anchor of the top-level popup | the pointer virtual anchor (CTX-POS-01) |
  | "top-level menu" / parent kind none | parent kind `'context-menu'` |

  Not adopted (no counterpart in ContextMenu): Menu.Trigger — MNU-STR-02, MNU-STR-03, MNU-STR-16, MNU-API-16…23, MNU-DOM-01…03, MNU-ARIA-01…03, MNU-ARIA-17, MNU-KEY-01…03, MNU-PTR-01…07, MNU-PTR-08 (trigger and Menubar clauses; its `hoverEnabled` gating of submenu popups is adopted with MNU-STA-11), MNU-FOC-06, MNU-FOC-07, MNU-FOC-16, MNU-FOC-17, MNU-EVT-14, MNU-EDGE-01; handles, detached triggers and payloads — MNU-API-13…15, MNU-API-63…66, MNU-ANI-08, MNU-POS-10, MNU-EDGE-07; Viewport — MNU-API-33, MNU-DOM-12; List and filtering — MNU-STR-13…15, MNU-STA-13, MNU-STA-15…25, MNU-API-54…62, MNU-API-67, MNU-DOM-20…24, MNU-DOM-28, MNU-ARIA-05, MNU-ARIA-06, MNU-ARIA-14…16, MNU-KEY-22…31, MNU-KEY-33, MNU-PTR-19…22, MNU-FOC-14, MNU-FOC-15, MNU-EVT-16…19, MNU-EDGE-04, MNU-EDGE-05, MNU-A11Y-02; Menubar/Toolbar-only branches — MNU-KEY-20, MNU-KEY-21 (apply only if the context menu is itself rendered inside a Toolbar/Menubar).

  Overridden: MNU-STR-10 (Separator clause) → CTX-STR-05; MNU-STR-17 → CTX-DOM-04; MNU-STR-19 → CTX-STR-07; MNU-API-08 → CTX-API-04, CTX-STA-05; MNU-API-11 (on the Root) → CTX-API-05; MNU-API-15 → CTX-API-03; MNU-API-26…30 → CTX-API-07…13; MNU-STA-02 (trigger fill-in) → CTX-EVT-03; MNU-STA-06 → CTX-STA-05; MNU-DOM-27, MNU-ARIA-12 (Separator clause) → CTX-DOM-06, CTX-ARIA-03; MNU-ARIA-18 (top-level case) → CTX-ARIA-05; MNU-ARIA-19 → CTX-ARIA-02; MNU-KEY-09 (focus target) → CTX-KEY-04; MNU-KEY-10 → CTX-KEY-06; MNU-KEY-11 (focus target) → CTX-KEY-05; MNU-PTR-03 (release activation) → CTX-PTR-09…13; MNU-PTR-08 (root popup) → CTX-PTR-16; MNU-PTR-18 → §6.2 of this document; MNU-FOC-04 (focus target) → CTX-FOC-03; MNU-FOC-10, MNU-FOC-13 → CTX-FOC-02; MNU-DIS-01 (500 ms clause), MNU-DIS-10 → CTX-DIS-01…03; MNU-DIS-09 → CTX-DIS-05; MNU-POS-01…04 (context-menu clauses) → CTX-POS-01…07; MNU-EDGE-02 → CTX-EDGE-03.
  Source: `context-menu/index.parts.ts:4-21`; `context-menu/root/ContextMenuRoot.tsx:46-52`. Test: see §13.
- **CTX-STR-03** — MUST. `ContextMenu.Root` renders no element. It owns the context-menu shared state (the pointer anchor, DOM handles for the internal backdrop, the consumer Backdrop and the root positioner, the release-arming flag, the initial cursor point, an internal open/close entry point and a generated root id) and renders one menu root with all its props. For that menu root it hides any enclosing menu, so a `ContextMenu.Root` is never treated as a submenu of a menu it happens to be rendered in.
  Source: `context-menu/root/ContextMenuRoot.tsx:16-52`; `context-menu/root/ContextMenuRootContext.ts:5-17`. Test: [L2] `ContextMenuTrigger.test.tsx` "should handle nested context menus correctly" (L4 for the nested-gesture citation).
- **CTX-STR-04** — MUST. `ContextMenu.Trigger` renders a `<div>` and MUST be a descendant of a `ContextMenu.Root`; otherwise it throws CTX-EDGE-01. It may wrap arbitrary content (including other components and nested context menus).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:24-39,198-215`. Test: [L2] `ContextMenuTrigger.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`), "throws when rendered outside ContextMenu.Root".
- **CTX-STR-05** — MUST. `ContextMenu.Separator` is the standalone Separator component (all SEP requirements), not a menu-specific part: it needs no menu ancestor and does not register with the item list.
  Source: `context-menu/index.parts.ts:21`; `separator/Separator.tsx:12-27`. Docs: `components/context-menu/page.mdx` §Separator. Test: none in `context-menu/` (see `separator/Separator.test.tsx`).
- **CTX-STR-06** — MUST. Because the re-exported parts are the Menu parts themselves, `Menu.Item`, `Menu.SubmenuRoot`, `Menu.SubmenuTrigger`, `Menu.Portal`, `Menu.Positioner`, `Menu.Popup` etc. MAY be used interchangeably with their `ContextMenu.*` names inside a context menu, with identical behavior.
  Source: `context-menu/index.parts.ts:4-19`. Docs: `docs/src/app/(private)/experiments/context-menu.tsx:102-139` (Menu submenu parts inside ContextMenu popups). Test: none.
- **CTX-STR-07** — MUST. Parent-kind classification (extends MNU-STR-19): the menu root rendered by `ContextMenu.Root` has parent kind `'context-menu'`; a `SubmenuRoot` inside its popup has parent kind `'menu'` and inherits the context menu's root id, floating tree and release-arming flag; a plain `Menu.Root` rendered anywhere inside the context menu (inside the Trigger or inside the popup, not as a SubmenuRoot) has parent kind none and is an ordinary top-level menu; a `ContextMenu.Root` rendered anywhere inside another menu or context menu is an independent context-menu root (CTX-STR-03).
  Source: `menu/root/MenuRoot.tsx:94-122,283-302`; `menu/store/MenuStore.ts:91-97,175-181`; `context-menu/root/ContextMenuRoot.tsx:48`. Docs: `CHANGELOG.md v1.0.0-beta.3 "Fix menu not opening when inside context menu trigger"`. Test: [L4] `MenuPositioner.test.tsx` "uses the visual viewport for a context menu submenu"; `ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item".
- **CTX-STR-08** — MUST. `ContextMenu.Trigger` renders no sibling elements (no focus guards before or after it, unlike MNU-STR-16) and nothing else besides its own element and children.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:198-217`. Docs: `CHANGELOG.md v1.1.0 "Avoid creating sibling elements next to trigger"`. Test: [L2] `ContextMenuTrigger.test.tsx` describeConformance.
- **CTX-STR-09** — MUST. Nested context menus: a `ContextMenu.Root` (with its own Trigger) rendered inside another `ContextMenu.Trigger` is fully independent — its own open state, anchor, root id and outside-press handling; a right click on the inner trigger opens only the inner menu (CTX-PTR-15).
  Source: `context-menu/root/ContextMenuRoot.tsx:29,46-52`; `context-menu/trigger/ContextMenuTrigger.tsx:81`. Test: [L4] `ContextMenuTrigger.test.tsx` "should handle nested context menus correctly".

Structural-only parts are the same as Menu's (Arrow, Backdrop, Group, GroupLabel, Separator). If the Trigger is omitted the menu can still be opened through the `open`/`defaultOpen` props and is then anchored at the initial anchor (CTX-STA-04).

## 3. Public API

Every part accepts `className`, `style` and `render` per [rendering-and-composition](../architecture/rendering-and-composition.md) (RND-API-01..03); other attributes are forwarded to the rendered element (RND-API-04). Consumer handlers run before the part's own and may call `preventBaseUIHandler()` (RND-EVT-02, EVD-EVT-26). Re-exported parts: see [menu](menu.md) §3 (CTX-STR-02). Only the three own parts are specified here.

### 3.1 `ContextMenu.Root`

**Default element:** none. **Accepts `render` element substitution:** no. **Accepts `className`/`style`:** no.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `defaultOpen` | `boolean` | `false` | Initial open state (MNU-API-01). | CTX-API-01 |
| `open` | `boolean \| undefined` | `undefined` | Consumer-owned open state (MNU-API-02). | CTX-API-01 |
| `onOpenChange` | `(open, eventDetails: ContextMenu.Root.ChangeEventDetails) => void` | — | Open-change request (§7). | CTX-API-02 |
| `onOpenChangeComplete` | `(open: boolean) => void` | — | MNU-API-04. | CTX-API-01 |
| `onItemHighlighted` | `(item, eventDetails) => void` | — | MNU-API-05. | CTX-API-01 |
| `loopFocus` | `boolean` | `true` | MNU-API-06. | CTX-API-01 |
| `highlightItemOnHover` | `boolean` | `true` | MNU-API-07. | CTX-API-01 |
| `orientation` | `'vertical' \| 'horizontal'` | `'vertical'` | MNU-API-09. | CTX-API-01 |
| `disabled` | `boolean` | `false` | MNU-API-10, plus Trigger gestures (CTX-PTR-02). | CTX-API-01 |
| `actionsRef` | `RefObject<{ unmount(), close() }>` | — | MNU-API-12. | CTX-API-01 |
| `children` | `ReactNode` | — | Plain content only. | CTX-API-03 |
| `closeParentOnEsc` | `boolean` | `false` | Deprecated, `@ignore`d, no effect (not in outline). | CTX-API-05 |

Not accepted (removed from the public type): `handle`, `triggerId`, `defaultTriggerId`, `modal`. (`openOnHover`, `delay`, `closeDelay` are also listed as removed in the source but are not `Menu.Root` props in the first place, §16 Q7.)

**Consumed (not forwarded):** every prop (there is no element).

- **CTX-API-01** — MUST. `defaultOpen`, `open`, `onOpenChangeComplete`, `onItemHighlighted`, `loopFocus`, `highlightItemOnHover`, `orientation`, `disabled` and `actionsRef` behave exactly as on `Menu.Root` (MNU-API-01, -02, -04, -05, -06, -07, -09, -10, -12), with the same defaults.
  Source: `context-menu/root/ContextMenuRoot.tsx:49,57-85`. Test: [L2] `ContextMenuTrigger.test.tsx` "adds open state attributes" (`defaultOpen`); `ContextMenuRoot.test.tsx` "does not open when disabled", "flips to the opposite side when side: flip is set and there is no space" (`open`) (L4 for the right-click and placement citations).
- **CTX-API-02** — MUST. `onOpenChange(open, details)` is the Menu open-change callback (MNU-API-03, MNU-EVT-01..03) typed with `ContextMenu.Root.ChangeEventDetails`, whose reason union is identical to `Menu.Root.ChangeEventReason`; the reasons actually emitted are listed in §7.
  Source: `context-menu/root/ContextMenuRoot.tsx:74-78,87-90`. Test: [L4] `ContextMenuTrigger.test.tsx` "should call onOpenChange when menu is opened via right click".
- **CTX-API-03** — MUST. `children` is plain content; the function-of-payload form of MNU-API-15 is not part of the contract (there is no trigger payload).
  Source: `context-menu/root/ContextMenuRoot.tsx:70-72,84`. Test: none.
- **CTX-API-04** — MUST. `handle`, `triggerId`, `defaultTriggerId` and `modal` are not part of the public contract: a context menu has no detached triggers and is always modal (CTX-STA-05). A `modal` value passed anyway is ignored (CTX-EDGE-03).
  Source: `context-menu/root/ContextMenuRoot.tsx:57-68`; `menu/root/MenuRoot.tsx:166,220`. Test: none.
- **CTX-API-05** — MUST. `closeParentOnEsc` is accepted on `ContextMenu.Root` for type compatibility but has no effect (it only acts on a submenu root, MNU-API-11); on `ContextMenu.SubmenuRoot` it behaves as MNU-API-11.
  Source: `context-menu/root/ContextMenuRoot.tsx:79-83`; `menu/root/MenuRoot.tsx:503`. Test: none.

#### State object (`ContextMenu.Root.State`)

Empty (`{}`). Source: `context-menu/root/ContextMenuRoot.tsx:55`.

#### Data attributes / CSS variables

None.

### 3.2 `ContextMenu.Trigger`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (`ContextMenu.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | | RND-API-01..03 |
| any other attribute | — | — | Forwarded to the `<div>`; consumer `onContextMenu`/`onTouch*` run before the part's handlers. | CTX-API-06 |

- **CTX-API-06** — MUST. The Trigger has no props of its own: in particular no `disabled` (disabled state comes only from `ContextMenu.Root`'s `disabled`), no `nativeButton`, no `id` handling, no `handle`/`payload`, no hover props. Every attribute other than `className`/`style`/`render` is forwarded to the element; consumer `onContextMenu`, `onTouchStart`, `onTouchMove`, `onTouchEnd`, `onTouchCancel` handlers run before the Trigger's own and can suppress them with `preventBaseUIHandler()`.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:28,198-215,227-230`. Test: [L2] `ContextMenuTrigger.test.tsx` describeConformance, "blocks the native context menu when onContextMenu skips the Base UI handler" (L4 for the native-context-menu clause).

**Consumed (not forwarded):** `className`, `style` (merged), `render`.

#### State object (`ContextMenu.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | The context menu's effective open state. |

- **CTX-STA-01** — MUST. The Trigger's state object contains exactly `open`, equal to the Root's effective open state (not "opened by this trigger": there is no active-trigger concept).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:42,194-196,220-225`. Test: [L2] `ContextMenuTrigger.test.tsx` "adds open state attributes".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | `open === true`; empty | CTX-DOM-01 |
| `data-pressed` | `open === true`; empty | CTX-DOM-01 |

#### CSS variables

None.

### 3.3 `ContextMenu.Positioner`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`ContextMenu.Positioner.State`, identical to `Menu.Positioner.State`).

The component is `Menu.Positioner`; the table gives the defaults that apply under a context menu. "Root" = the positioner of the context menu's top-level popup; "submenu" = a positioner inside a `SubmenuRoot`.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `side` | `Side` | root: `undefined` → `'bottom'`; submenu: `'inline-end'` | Preferred side. | CTX-API-07 |
| `align` | `Align` | root: `'start'`; submenu: `'start'` | Alignment. | CTX-API-08 |
| `sideOffset` | `number \| OffsetFunction` | root: `-5` when `side` is not given and `align !== 'center'`, else `0`; submenu: `0` | | CTX-API-09 |
| `alignOffset` | `number \| OffsetFunction` | root: `2` under the same condition, else `0`; submenu: `0` | | CTX-API-09 |
| `anchor` | `Element \| VirtualElement \| RefObject \| () => … \| null` | root: pointer virtual anchor; submenu: its SubmenuTrigger | | CTX-API-10 |
| `arrowPadding` | `number` | root: always `0` (prop ignored); submenu: `5` | | CTX-API-11 |
| `positionMethod` | `'absolute' \| 'fixed'` | always `'fixed'` (prop ignored; deprecated, `@ignore`d, not in outline) | | CTX-API-12 |
| `collisionBoundary` | `Boundary` | `'clipping-ancestors'` | | CTX-API-13 |
| `collisionPadding` | `Padding` | `5` | | CTX-API-13 |
| `collisionAvoidance` | `CollisionAvoidance` | root: `{ fallbackAxisSide: 'none' }`; submenu: `{ fallbackAxisSide: 'end' }` | | CTX-API-13 |
| `sticky` | `boolean` | `false` | | CTX-API-13 |
| `disableAnchorTracking` | `boolean` | `false` | | CTX-API-13 |

**Consumed (not forwarded):** all of the above.

- **CTX-API-07** — MUST. `side` has no explicit default on the root positioner: when omitted, the placement side is the engine default `'bottom'` (and the offset defaults of CTX-API-09 apply); a submenu positioner defaults to `'inline-end'` (MNU-API-26).
  Source: `context-menu/positioner/ContextMenuPositioner.tsx:47-54`; `menu/positioner/MenuPositioner.tsx:43,83-96`. Test: [L2] `MenuPositioner.test.tsx` "preserves explicit context-menu placement and offsets" (L4 for the resulting placement).
- **CTX-API-08** — MUST. `align` defaults to `'start'` on the root positioner (and on submenus, MNU-API-26); an explicit value wins.
  Source: `menu/positioner/MenuPositioner.tsx:83-85,94-96`; `context-menu/positioner/ContextMenuPositioner.tsx:79-83`. Test: [L2] `MenuPositioner.test.tsx` "preserves explicit context-menu placement and offsets" (L4 for the resulting placement).
- **CTX-API-09** — MUST. On the root positioner, when `side` is not given and the effective `align` is not `'center'`, `sideOffset` defaults to `-5` and `alignOffset` to `2`; in every other case (an explicit `side`, `align="center"`, or a submenu) both default to `0`. An explicit `sideOffset`/`alignOffset` always wins, including `0` and negative values. Both accept numbers or offset functions (FLT-API-12).
  Source: `menu/positioner/MenuPositioner.tsx:45-46,86-89`; `context-menu/positioner/ContextMenuPositioner.tsx:55-107`. Docs: `CHANGELOG.md v1.0.0-beta.4 "Fix default offsets when align="center" or side differs"`. Test: [L2] `MenuPositioner.test.tsx` "preserves explicit context-menu placement and offsets"; `ContextMenuRoot.test.tsx` "ignores mouseup directly under the cursor when the context menu spawns there" (`alignOffset={0}`), "ignores mouseup directly under the cursor when alignOffset is negative" (`alignOffset={-5}`) (L4 for the resulting placement and the mouseup citation).
- **CTX-API-10** — MUST. `anchor`, when given, replaces the pointer anchor on the root positioner (element, DOM handle, function, or virtual element with `getBoundingClientRect`, resolved per FLT-API-11); when omitted the root positioner uses the context menu's pointer virtual anchor (CTX-POS-01) and a submenu positioner uses its SubmenuTrigger element.
  Source: `menu/positioner/MenuPositioner.tsx:78,84`; `context-menu/positioner/ContextMenuPositioner.tsx:36-41`. Docs: `CHANGELOG.md v1.0.0-beta.5 "Fix ignored anchor prop on <ContextMenu.Positioner>"`. Test: [L4] `ContextMenuRoot.test.tsx` "flips to the opposite side when side: flip is set and there is no space" (explicit virtual `anchor`).
- **CTX-API-11** — MUST. The root positioner always passes `arrowPadding = 0` to positioning, ignoring the prop; a submenu positioner uses the prop (default `5`).
  Source: `menu/positioner/MenuPositioner.tsx:49,104,115`; `context-menu/positioner/ContextMenuPositioner.tsx:108-113`. Test: none.
- **CTX-API-12** — MUST. Every menu positioner rendered inside a `ContextMenu.Root` (the root positioner, every submenu positioner, and also the positioner of a plain `Menu.Root` placed anywhere inside the context menu's subtree) uses `positionMethod: 'fixed'`, ignoring the `positionMethod` prop.
  Source: `menu/positioner/MenuPositioner.tsx:59,109`; `context-menu/positioner/ContextMenuPositioner.tsx:42-46`. Test: none.
- **CTX-API-13** — MUST. `collisionBoundary` (`'clipping-ancestors'`), `collisionPadding` (`5`), `sticky` (`false`), `disableAnchorTracking` (`false`) and `collisionAvoidance` (root `{ fallbackAxisSide: 'none' }`, submenu `{ fallbackAxisSide: 'end' }`) have the Menu defaults (MNU-API-28, MNU-API-29) and are passed to the shared positioning unchanged; the root additionally gets the shift configuration of CTX-POS-03.
  Source: `menu/positioner/MenuPositioner.tsx:47-52,97,116-122`. Test: [L2] `ContextMenuRoot.test.tsx` "flips to the opposite side when side: flip is set and there is no space" (L4 for the flip result).

#### State object, data attributes, CSS variables

As `Menu.Positioner` (MNU §3.5): state `{ open, side, align, anchorHidden, nested, instant }`.

- **CTX-STA-02** — MUST. The Positioner state object contains exactly the `Menu.Positioner.State` fields; `nested` is `false` on the root positioner and `true` on submenu positioners. The Popup, Backdrop and Arrow state objects are the Menu ones.
  Source: `context-menu/positioner/ContextMenuPositioner.tsx:10`; `menu/positioner/MenuPositioner.tsx:236-243`. Test: [L2] `ContextMenuRoot.test.tsx` "flips to the opposite side when side: flip is set and there is no space" (`data-side`) (L4 for the `data-side` citation).

| Attribute / variable | Req |
|---|---|
| `data-open`/`data-closed`, `data-side`, `data-align`, `data-anchor-hidden`, `data-nested`, `data-instant` | CTX-DOM-05 (MNU-DOM-04) |
| `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`, `--positioner-width`, `--positioner-height` | MNU-POS-08 (adopted) |

### 3.4 Re-exported parts

`Portal`, `Backdrop`, `Popup`, `Arrow`, `Item`, `LinkItem`, `Group`, `GroupLabel`, `RadioGroup`, `RadioItem`, `RadioItemIndicator`, `CheckboxItem`, `CheckboxItemIndicator`, `SubmenuRoot`, `SubmenuTrigger`: props, state objects, data attributes and requirements as [menu](menu.md) §3.3, §3.4, §3.6, §3.8–§3.20 (CTX-STR-02). `Separator`: as [separator](separator.md) §3 (CTX-STR-05). The `ContextMenu.*DataAttributes` / `ContextMenuPositionerCssVariables` enum exports are the Menu (and Separator) enums under new names, except `ContextMenuTriggerDataAttributes` = `{ popupOpen, pressed }` (`context-menu/index.ts:68-81`; `context-menu/trigger/ContextMenuTriggerDataAttributes.ts:1-10`).

### 3.5 Shared types

- `ContextMenu.Root.ChangeEventReason` = `Menu.Root.ChangeEventReason` (`'trigger-hover' | 'trigger-focus' | 'trigger-press' | 'outside-press' | 'focus-out' | 'list-navigation' | 'escape-key' | 'item-press' | 'close-press' | 'sibling-open' | 'cancel-open' | 'imperative-action' | 'none'`); `ContextMenu.Root.ChangeEventDetails` = cancelable change details with that reason (EVD); `ContextMenu.Root.Actions` = `Menu.Root.Actions` (`{ unmount(): void; close(): void }`). Source: `context-menu/root/ContextMenuRoot.tsx:87-98`.
- `ContextMenu.Positioner.State` = `Menu.Positioner.State`; `ContextMenu.Trigger.State` = `{ open: boolean }`; `ContextMenu.Root.State` = `{}`.
- All other `ContextMenu.<Part>.Props/State/ChangeEventDetails/ChangeEventReason` types are the Menu (or Separator) types (`context-menu/index.ts:7-66`).

## 4. State model

The Root's open state, highlight, checked state and the open-change sequence are the Menu ones (MNU §4.1–4.3, adopted). The context menu adds a small amount of shared state held by `ContextMenu.Root`:

| Field | Initial value | Written by | Read by |
|---|---|---|---|
| pointer anchor | 0 × 0 rectangle at (0, 0) | each open gesture | root positioner |
| release-arming flag | `true` | `contextmenu` (→ `true`), first document `mouseup` after it (→ `false`), `touchstart` (→ `false`) | item `mouseup` (CTX-PTR-09) |
| initial cursor point | none | each open gesture | first item `mouseup` after open (consumed) |
| outside-press grace flag | `false` | open (→ `true` after 500 ms), close (→ `false`) | outside-press dismissal |
| root id | generated | — | `data-rootownerid` on every popup of the tree |

- **CTX-STA-03** — MUST. The open state follows MNU-STA-01, -03, -04, -05 (consumer-owned `open` vs component-owned `defaultOpen`, one cancelable open-change sequence for every request). A request to open while already open is **not** deduplicated when it comes from a gesture (the request carries no trigger element while the stored active trigger is `null`), so `onOpenChange(true, …)` is called again (CTX-PTR-14).
  Source: `menu/root/MenuRoot.tsx:341-445` (dedupe at 354-360); `utils/popups/store.ts:116`. Test: [L4] `ContextMenuTrigger.test.tsx` "should call onOpenChange when menu is opened via right click".
- **CTX-STA-04** — MUST. The pointer anchor is a virtual element whose rectangle is fixed when it is created. Before any gesture it is a 0 × 0 rectangle at viewport (0, 0), so a menu opened through `defaultOpen`/`open` without a prior gesture is anchored at the top-left corner of the viewport. Each open gesture replaces it (CTX-POS-01); it is not reset on close, so a later programmatic open reuses the last gesture position.
  Source: `context-menu/root/ContextMenuRoot.tsx:17-21`; `context-menu/trigger/ContextMenuTrigger.tsx:57-66`. Test: [L4] `ContextMenuTrigger.test.tsx` "adds open state attributes" (opens with `defaultOpen`).
- **CTX-STA-05** — MUST. The context menu is always modal: the stored `modal` value is never written for parent kind `'context-menu'`, so the effective modality (MNU-STA-06 formula) is `true`. Consequences: internal backdrop (CTX-DOM-04), scroll lock (CTX-DIS-05), modal focus management (CTX-FOC-02).
  Source: `menu/root/MenuRoot.tsx:166,220`; `menu/store/MenuStore.ts:82-84`. Test: [L2] `ContextMenuTrigger.test.tsx` "blocks native context menus on both internal and external backdrops" (internal backdrop present) (L4 for the native-context-menu citation).
- **CTX-STA-06** — MUST. The release-arming flag is one value shared by the context menu and all its submenus. It starts `true`, is set `true` by every `contextmenu` gesture on the Trigger, is set `false` by the first document `mouseup` after that gesture (whatever the target) and by every `touchstart` on the Trigger. Items consult it on `mouseup` (CTX-PTR-09).
  Source: `context-menu/root/ContextMenuRoot.tsx:27`; `context-menu/trigger/ContextMenuTrigger.tsx:80,93,131`; `menu/store/MenuStore.ts:175-181`; `menu/item/useMenuItemCommonProps.ts:127-130`. Test: [L4] `ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item", "allows mouseup after leaving the initial cursor point".
- **CTX-STA-07** — MUST. The initial cursor point is recorded (viewport `clientX`/`clientY`) by every open gesture and cleared by the first `mouseup` on any item of the context menu tree after it (whether or not that `mouseup` activates the item).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:55`; `menu/item/useMenuItemCommonProps.ts:108-110`. Test: [L4] `ContextMenuRoot.test.tsx` "ignores mouseup directly under the cursor when the context menu spawns there".
- **CTX-STA-08** — MUST. The outside-press grace flag is `false` while closed; each time the menu becomes open a 500 ms timer starts, after which it is `true`; closing clears the timer and resets it to `false`.
  Source: `menu/root/MenuRoot.tsx:200-201,312-333`. Test: [L4] `ContextMenuTrigger.test.tsx` "delays outside-press dismissal after opening from a long press".
- **CTX-STA-09** — MUST. The open interaction type (`openMethod`, MNU's mouse/touch/pen/keyboard classification of the opening press) is never set for a context menu, because the Trigger does not carry the press-classification handlers; it stays unset while open.
  Source: `menu/root/MenuRoot.tsx:215,641-658`; `utils/useOpenInteractionType.ts:45-62`; `context-menu/trigger/ContextMenuTrigger.tsx:198-215`. Test: none.
- **CTX-STA-10** — MUST. The tree's root id is the `ContextMenu.Root`'s generated id: the context menu's own menu root and every submenu report it as `rootId`.
  Source: `context-menu/root/ContextMenuRoot.tsx:29,41`; `menu/store/MenuStore.ts:91-97`. Test: [L2] `ContextMenuTrigger.test.tsx` "keeps the root menu open when the context-menu gesture ends in a portaled submenu" (L4 for the gesture citation).

State diagram (top-level context menu):

```
closed ──(contextmenu on Trigger | 500 ms single-touch hold | open/defaultOpen)──► open (anchor = gesture point)
open ──(right-button release on item, macOS)──► item activated → closed ('item-press')
open ──(document mouseup ≥ 500 ms after a contextmenu open, outside the tree)──► closed ('cancel-open')
open ──(outside press: immediately after a contextmenu open; after 500 ms otherwise)──► closed ('outside-press')
open ──(Escape | Shift+Tab | item click | actionsRef.close())──► closed (Menu reasons)
open ──(contextmenu on Trigger again)──► open, re-anchored (onOpenChange(true) again)
```

Mounting and transition statuses follow MNU §4.5 / §9.

## 5. Rendered DOM and ARIA

### 5.1 `ContextMenu.Trigger` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-popup-open`, `data-pressed` | menu open | CTX-DOM-01 | C# |
| `style` | `-webkit-touch-callout: none` merged before consumer `style` | CTX-DOM-02 | C# |
| `role`, `tabindex`, `id`, `aria-*`, `data-disabled` | never emitted | CTX-DOM-02, CTX-ARIA-01 | C# |

- **CTX-DOM-01** — MUST. `data-popup-open=""` and `data-pressed=""` are both present on the Trigger iff the context menu is open (pressable trigger mapping, PSH-DOM-02); both are removed when it closes (e.g. by Escape).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:194-196,214`; `utils/popupStateMapping.ts:39-46`. Test: [L2] `ContextMenuTrigger.test.tsx` "adds open state attributes" (L4 for the Escape-close clause).
- **CTX-DOM-02** — MUST. The Trigger's only built-in attribute is the inline style `-webkit-touch-callout: none` (suppresses the iOS long-press callout); a consumer `style` is merged over it. It emits no `role`, `tabindex`, `id`, `data-disabled` or ARIA attribute, and sets no `user-select` or `touch-action` style.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:198-215`. Test: [L2] `ContextMenuTrigger.test.tsx` describeConformance.

### 5.2 Positioner, Popup, Backdrop, Arrow, items

- **CTX-DOM-03** — MUST. Every Popup of the context menu tree (the root popup and all submenu popups) renders `data-rootownerid="<id>"` where `<id>` is the `ContextMenu.Root`'s generated id (CTX-STA-10). A nested independent context menu's popups carry their own root's id.
  Source: `menu/popup/MenuPopup.tsx:178`; `menu/store/MenuStore.ts:91-97`; `context-menu/root/ContextMenuRoot.tsx:29`. Test: [L2] `ContextMenuTrigger.test.tsx` "keeps the root menu open when the context-menu gesture ends in a portaled submenu" (L4 for the gesture citation).
- **CTX-DOM-04** — MUST. While the root popup is mounted, an internal backdrop `<div role="presentation" data-base-ui-inert="">` (PSH-DOM-14) is rendered as the sibling immediately before the root Positioner, with **no cutout** (it covers the Trigger too), `inert` while the menu is not open, and its DOM handle registered with the context menu so that native context menus on it are suppressed (CTX-PTR-03). Submenus render none.
  Source: `menu/positioner/MenuPositioner.tsx:264-290`. Test: [L2] `ContextMenuTrigger.test.tsx` "blocks native context menus on both internal and external backdrops" (L4 for the native-context-menu suppression clause).
- **CTX-DOM-05** — MUST. Positioner, Popup, Backdrop, Arrow and item elements render the Menu attributes (MNU-DOM-04…11, MNU-DOM-13…18, MNU-DOM-25, MNU-DOM-26, MNU-DOM-29); `data-nested` is absent on the root Positioner/Popup and present on submenu ones; `data-side`/`data-align` reflect the resolved placement (e.g. `top` after a flip). Server: relaxed, see §9.
  Source: `menu/positioner/MenuPositioner.tsx:236-243`; `menu/popup/MenuPopup.tsx:141-148`. Test: [L2] `ContextMenuRoot.test.tsx` "flips to the opposite side when side: flip is set and there is no space" (L4 for the resolved-placement `data-side`/`data-align` clause).
- **CTX-DOM-06** — MUST. `ContextMenu.Separator` renders the Separator attributes: `data-orientation` (SEP-DOM-01), consumer precedence (SEP-DOM-02).
  Source: `separator/Separator.tsx:16-24`. Test: none in `context-menu/`.

The consumer `ContextMenu.Backdrop` (MNU-DOM-10) additionally registers its element with the context menu (used by CTX-PTR-03). Source: `menu/backdrop/MenuBackdrop.tsx:29-39`.

### 5.3 ARIA

- **CTX-ARIA-01** — MUST. The Trigger carries no ARIA: no `aria-haspopup`, `aria-expanded`, `aria-controls` and no role (it is a plain `<div>` unless the consumer substitutes an element). Discoverability is the consumer's responsibility (the docs require an alternative visible control).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:198-215` (the menu's trigger props of `menu/root/MenuRoot.tsx:641-668` are never applied to it). Docs: `components/context-menu/page.mdx` §Usage guidelines. Test: none.
- **CTX-ARIA-02** — MUST. The Popup renders `role="menu"` and the conditional `aria-orientation` (MNU-ARIA-04), but no generated `aria-labelledby` — there is no trigger whose id could label it; a consumer `aria-label`/`aria-labelledby` (on the Popup or its substitute element) is rendered as given.
  Source: `menu/popup/MenuPopup.tsx:78,159-168`; `internals/resolvePopupLabel.ts:10-30`. Test: none.
- **CTX-ARIA-03** — MUST. `ContextMenu.Separator` renders `role="separator"` and `aria-orientation` equal to its orientation (SEP-ARIA-01, SEP-ARIA-02).
  Source: `separator/Separator.tsx:23`. Test: none in `context-menu/`.
- **CTX-ARIA-04** — MUST. While the root popup is open, content outside the context menu tree receives `aria-hidden="true"` per the modal focus manager (FLT-ARIA-01); it is removed on close.
  Source: `menu/popup/MenuPopup.tsx:194-197`; see FLT-ARIA-01. Test: none.
- **CTX-ARIA-05** — MUST. The hidden `aria-owns` owner span of the root Portal carries no role (top-level case of MNU-ARIA-18); submenu Portals' owner spans carry `role="group"`.
  Source: `menu/portal/MenuPortal.tsx:32`. Test: none.

Items, groups, indicators, submenu triggers: MNU-ARIA-07…13 (adopted).

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| any key | Trigger | Not handled (no arrow/Enter/Space opening) | CTX-KEY-01 |
| Shift+F10, ContextMenu key | focused element inside the Trigger | Handled only through the `contextmenu` event the platform dispatches (same path as a right click) | CTX-KEY-02 |
| arrows, Home/End, typeahead, Enter/Space, submenu keys | open popup | As Menu | CTX-KEY-03 (MNU-KEY-04…08, 12…19, 32) |
| ArrowLeft (LTR) / ArrowRight (RTL) | focus inside the root popup (vertical orientation) | Closes the top-level context menu (`list-navigation`); untested, probably unintended | CTX-KEY-07 |
| Escape | anywhere while open | Closes innermost menu (`escape-key`); root close returns focus to the pre-open focused element | CTX-KEY-04 |
| Shift+Tab | focus inside the root popup | Closes (`focus-out`), focus to the pre-open focused element | CTX-KEY-05 |
| Tab | focus inside the root popup | Does not leave the popup (modal trap) | CTX-KEY-06 |

- **CTX-KEY-01** — MUST. `ContextMenu.Trigger` attaches no keyboard handlers: arrow keys, Enter and Space on it never open the menu (the menu's arrow-key opening is also disabled for parent kind `'context-menu'`).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:198-213`; `menu/root/MenuRoot.tsx:541`. Test: none.
- **CTX-KEY-02** — MUST. A `contextmenu` event dispatched for a keyboard request (Shift+F10, the ContextMenu key) on an element inside the Trigger is handled exactly like a pointer one (CTX-PTR-01), using the event's `clientX`/`clientY` as reported. No key code is inspected. The Trigger is not focusable by default; keyboard access requires the consumer to make it (or content inside it) focusable.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:76-82`. Test: [L4] `ContextMenuRoot.test.tsx` "returns focus to the focused surface when closing with Shift+Tab" (Trigger substituted with a focusable `<button>`).
- **CTX-KEY-03** — MUST. Inside the open popup all Menu keys apply unchanged (MNU-KEY-04…08, MNU-KEY-12…19, MNU-KEY-32, MNU-I18N-01/02). Because a gesture open leaves the popup focused with nothing highlighted, the first main-axis arrow enters the list at the boundary item (MNU-KEY-06).
  Source: `menu/root/MenuRoot.tsx:516-546`. Test: none in `context-menu/`.
- **CTX-KEY-04** — MUST. Escape closes the innermost open menu of the tree with reason `escape-key` (MNU-KEY-09, MNU-KEY-19); when the root closes, focus returns per CTX-FOC-03. The Trigger's `data-popup-open` is removed. Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:501-512`. Test: [L4] `ContextMenuTrigger.test.tsx` "adds open state attributes".
- **CTX-KEY-05** — MUST. Shift+Tab with focus inside the root popup is default-prevented and closes the menu with reason `focus-out` (MNU-KEY-11); focus then goes to the element that was focused before the menu opened (CTX-FOC-03).
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`; `menu/popup/MenuPopup.tsx:182-199`. Test: [L4] `ContextMenuRoot.test.tsx` "returns focus to the focused surface when closing with Shift+Tab".
- **CTX-KEY-06** — MUST. Tab with focus inside the root popup does not move focus out of the context menu: the modal focus manager wraps focus within the popup (FLT-KEY-05) or keeps it on the popup when it has no tabbable content (FLT-KEY-04). No `focus-out` close occurs (replaces MNU-KEY-10, which relies on trigger focus guards).
  Source: `menu/popup/MenuPopup.tsx:194-197`; see FLT-KEY-04, FLT-KEY-05. Test: none.
- **CTX-KEY-07** — SHOULD. The top-level context menu's list navigation is configured as *nested* (the root passes `nested: true` for every non-virtual-focus menu that has a parent kind, and the ContextMenu root is parent kind `'context-menu'`), so the cross-axis close key of CMP-KEY-27 — ArrowLeft in LTR, ArrowRight in RTL for the default `vertical` orientation; ArrowUp for `horizontal` — pressed with focus inside the root popup closes the top-level context menu with reason `list-navigation`. No list-level return element exists (the context menu registers no reference element and has no parent menu store), so the keydown is not consumed (no parent orientation is known, CMP-KEY-27's main-axis exception) and focus returns per CTX-FOC-03. Not tested and probably unintended (§16 Q13, menu.md §16 Q9); a port that deliberately omits it must still not emit `list-navigation` for any other root close. Port: not replicated (ADR 04 Amendments).
  Source: `menu/root/MenuRoot.tsx:521,543`; `floating-ui-react/hooks/useListNavigation.ts:539-545,570-585`; see CMP-KEY-27. Test: none.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| `contextmenu` (right click, any origin) | Trigger, enabled | `preventDefault` + `stopPropagation`; anchor 0×0 at pointer; open (`trigger-press`); arm release | CTX-PTR-01 |
| `contextmenu` | Trigger, Root `disabled` | Ignored; native menu shown | CTX-PTR-02 |
| `contextmenu` | inside Trigger subtree / internal backdrop / Backdrop | Native menu suppressed (document listener) | CTX-PTR-03 |
| document `mouseup` | < 500 ms after a `contextmenu` open | Ignored (disarms release) | CTX-PTR-04, CTX-PTR-05 |
| document `mouseup` | ≥ 500 ms after, outside the tree | Close (`cancel-open`) | CTX-PTR-05 |
| single-touch hold 500 ms | Trigger, enabled | Anchor 10×10 at touch point; open (`trigger-press`) | CTX-PTR-06 |
| move > 10 px, touchend, touchcancel, second finger | pending long press | Cancels | CTX-PTR-07 |
| right-button `mouseup` on an item | macOS, armed, ≥ 1 px from open point | Activates item (dispatched click) | CTX-PTR-09, CTX-PTR-10 |
| right-button `mouseup` on an item | non-macOS | Ignored | CTX-PTR-11 |
| left-button `mouseup` on an item | any | No release activation (the `click` still activates) | CTX-PTR-12 |
| right-button `mouseup` on a SubmenuTrigger | any | Nothing | CTX-PTR-13 |
| `contextmenu` on the Trigger while open | | Re-anchors, `onOpenChange(true)` again | CTX-PTR-14 |
| `contextmenu` on a nested inner Trigger | | Only the inner menu opens | CTX-PTR-15 |
| pointer leaving the root popup | | Does not close | CTX-PTR-16 |

- **CTX-PTR-01** — MUST. A `contextmenu` event on the Trigger (or a descendant) while the Root is not disabled MUST, synchronously in the event: set the release-arming flag to `true`; call `preventDefault()` and `stopPropagation()` on the event; record the event's `clientX`/`clientY` as the initial cursor point; replace the pointer anchor with a 0 × 0 rectangle at (`clientX`, `clientY`) (CTX-POS-01); request open with reason `trigger-press` and the native `contextmenu` event as `event` (no `trigger` element); start a 500 ms timer that, on expiry, allows the document `mouseup` of CTX-PTR-05 to act; and install the document `mouseup` listener (CTX-PTR-04).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:52-119`. Test: [L4] `ContextMenuTrigger.test.tsx` "should open menu on right click (context menu event)", "should call onOpenChange when menu is opened via right click"; `ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item".
- **CTX-PTR-02** — MUST. While the Root is `disabled`, a `contextmenu` event on the Trigger does nothing: the menu does not open, `onOpenChange` is not called, and the event's default is **not** prevented (the native context menu appears).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:77-79,175-177`. Docs: `CHANGELOG.md v1.2.0 "Fix disabled prop not working"`. Test: [L4] `ContextMenuTrigger.test.tsx` "does not open on right-click when disabled", "does not block the native context menu when disabled"; `ContextMenuRoot.test.tsx` "does not open when disabled".
- **CTX-PTR-03** — MUST. While the Trigger is mounted and the Root is not disabled, a listener on the Trigger's owner document calls `preventDefault()` on every `contextmenu` event whose target is DOM-contained in the Trigger element (including a portal container placed inside it), in the internal backdrop (CTX-DOM-04), or in the consumer `ContextMenu.Backdrop`. Other `contextmenu` events (e.g. on the page outside, or on a popup portaled elsewhere) are left untouched. This holds even when a consumer `onContextMenu` calls `preventBaseUIHandler()` (then the menu does not open, but the native menu is still suppressed).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:173-192`. Test: [L4] `ContextMenuTrigger.test.tsx` "blocks native context menus on both internal and external backdrops", "blocks native context menus in a portal mounted inside the trigger DOM subtree", "blocks the native context menu when onContextMenu skips the Base UI handler", "does not block the native context menu when disabled".
- **CTX-PTR-04** — MUST. Each gesture of CTX-PTR-01 installs exactly one one-shot `mouseup` listener on the Trigger's owner document, first removing any listener left by a previous gesture that never saw its `mouseup`. The listener is removed when it fires once, and also when the Trigger unmounts.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:85-118,165-171`. Test: [L3] `ContextMenuTrigger.test.tsx` "aborts the pending document mouseup listener when the trigger unmounts".
- **CTX-PTR-05** — MUST. When that document `mouseup` fires: the release-arming flag is set `false`; if fewer than 500 ms have passed since the gesture, nothing else happens; otherwise the 500 ms timer is cleared and the menu is closed with reason `cancel-open` and the `mouseup` event, **unless** the `mouseup` target is inside the root positioner element, or the nearest ancestor-or-self of the target (walking up through parent nodes and shadow hosts until the document root) that carries `data-rootownerid` has the value of this context menu's root id (i.e. the release landed in any popup of this tree, including portaled submenus).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:90-117`; `menu/utils/findRootOwnerId.ts:3-13`; `menu/root/MenuRoot.tsx:493-497`. Test: [L4] `ContextMenuTrigger.test.tsx` "does not cancel opening menu on mouseup after mousedown outside before 500ms", "cancels opening menu on mouseup after mousedown outside after 500ms", "keeps the menu open when the context-menu gesture ends inside its positioner", "keeps the root menu open when the context-menu gesture ends in a portaled submenu".
- **CTX-PTR-06** — MUST. A `touchstart` on the Trigger with exactly one touch, while the Root is not disabled, MUST stop the event's propagation, record the touch's `clientX`/`clientY`, and start a 500 ms long-press timer. When it expires the menu opens as in CTX-PTR-01 except: the anchor is a 10 × 10 rectangle whose top-left corner is the recorded point (CTX-POS-01), the open event is the `touchstart` event, no document `mouseup` listener is installed, and `preventDefault` is not called. No haptic feedback, `touch-action` or `user-select` change is applied.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:16,52-74,126-144`. Test: [L4] `ContextMenuTrigger.test.tsx` "should open menu on long press on touchscreen devices".
- **CTX-PTR-07** — MUST. A pending long press is cancelled (timer cleared, recorded point dropped) by: a `touchmove` whose single touch is more than 10 px away from the recorded point on the x **or** y axis (≤ 10 px on both keeps it pending); any `touchmove` or `touchstart` with a touch count other than 1; `touchend`; `touchcancel`; and a `touchstart` while the Root is disabled.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:121-163,205-206`. Test: [L3] `ContextMenuTrigger.test.tsx` "should cancel long press when touch moves beyond threshold", "keeps a pending long press when touch movement stays within the threshold", "cancels a pending long press when the touch ends", "cancels a pending long press when the gesture becomes multi-touch", "does not open on long press when disabled".
- **CTX-PTR-08** — MUST. Every `touchstart` on the Trigger (enabled, any touch count) sets the release-arming flag to `false`.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:126-131`. Test: none.
- **CTX-PTR-09** — MUST. Release activation: a `mouseup` on an Item, LinkItem, CheckboxItem or RadioItem anywhere in the context menu tree dispatches a synthetic `click` (`detail: 1`, `pointerType: 'mouse'`, carrying the `mouseup`'s modifier keys) on that item iff all of: the platform is macOS; `event.button === 2`; the release-arming flag is `true`; and CTX-PTR-10 does not suppress it. The click then behaves as an ordinary click (MNU-PTR-15): a plain item closes the whole tree with reason `item-press` (every open submenu and the root report `item-press`). Release activation happens immediately (no 200 ms arming delay as in MNU-PTR-02).
  Source: `menu/item/useMenuItemCommonProps.ts:107-143`. Test: [L4] `ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item", "allows mouseup after leaving the initial cursor point".
- **CTX-PTR-10** — MUST. The first item `mouseup` after an open gesture is ignored (no activation) when its `clientX` and `clientY` are each within 1 px (inclusive) of the initial cursor point — the popup may have opened under the cursor; the initial point is consumed by that `mouseup` either way.
  Source: `menu/item/useMenuItemCommonProps.ts:108-118`. Docs: `CHANGELOG.md v1.0.0-beta.7 "Block mouseup at initial cursor point"`. Test: [L4] `ContextMenuRoot.test.tsx` "ignores mouseup directly under the cursor when the context menu spawns there", "ignores mouseup directly under the cursor when alignOffset is negative".
- **CTX-PTR-11** — MUST. On platforms other than macOS a right-button (`button === 2`) `mouseup` on an item never activates it (on those platforms the `contextmenu` event follows the release, so the `mouseup` belongs to the opening gesture).
  Source: `menu/item/useMenuItemCommonProps.ts:120-124`. Docs: `CHANGELOG.md v1.3.0 "Ignore mouseup on non-Mac platforms"`. Test: [L4] `ContextMenuRoot.non-mac.test.tsx` "ignores context menu mouseup on non-Mac platforms".
- **CTX-PTR-12** — MUST. A `mouseup` with any button other than 2 on a context-menu item never triggers release activation; ordinary `click` activation (MNU-PTR-15) is unaffected.
  Source: `menu/item/useMenuItemCommonProps.ts:127-131`. Test: none.
- **CTX-PTR-13** — MUST. A right-button `mouseup` on a `SubmenuTrigger` never activates it (no synthetic click): its submenu does not open and the submenu's `onOpenChange` is not called.
  Source: `menu/item/useMenuItemCommonProps.ts:134-141`. Test: [L4] `ContextMenuRoot.test.tsx` "does not activate a submenu trigger when releasing the context menu pointer over it".
- **CTX-PTR-14** — MUST. A `contextmenu` event that reaches the Trigger while the menu is already open runs CTX-PTR-01 again: the anchor moves to the new point (the popup repositions), the initial cursor point and arming state are reset, and `onOpenChange(true, { reason: 'trigger-press' })` is called again (CTX-STA-03).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:52-82`; `menu/root/MenuRoot.tsx:354-360`. Test: none.
- **CTX-PTR-15** — MUST. When Triggers are nested (an inner `ContextMenu.Root`/`Trigger` inside an outer `ContextMenu.Trigger`), a `contextmenu` event on the inner Trigger opens only the inner menu (its handler stops propagation before the outer Trigger's handler runs); the outer opens only for events whose target is outside the inner Trigger. The inner menu then closes on an outside press like any context menu.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:81`. Test: [L4] `ContextMenuTrigger.test.tsx` "should handle nested context menus correctly".
- **CTX-PTR-16** — MUST. The root popup has no hover-close behavior (the popup hover interaction is disabled for parent kind `'context-menu'`); pointer movement in and out of the root popup never closes it. Submenus keep MNU-PTR-09…11.
  Source: `menu/popup/MenuPopup.tsx:123-126`. Test: none.

### 6.3 Focus management

- **CTX-FOC-01** — MUST. On a gesture open the root popup element itself receives focus (initial focus `true`, MNU-FOC-02 pointer path); no item is highlighted.
  Source: `menu/popup/MenuPopup.tsx:84-85`. Test: [L4] `ContextMenuRoot.test.tsx` "returns focus to the focused surface when closing with Shift+Tab" (`menu` role has focus after the right click).
- **CTX-FOC-02** — MUST. The root popup's focus manager runs in modal mode: focus is trapped within the context menu tree (CTX-KEY-06), outside content is `aria-hidden` (CTX-ARIA-04). This is the only Menu configuration that uses modal focus management (MNU-FOC-13); submenus run inside the same tree.
  Source: `menu/popup/MenuPopup.tsx:82,194-197`. Test: [L4] `ContextMenuRoot.test.tsx` "returns focus to the focused surface when closing with Shift+Tab".
- **CTX-FOC-03** — MUST. When the root closes, return focus is enabled and targets the element that was focused before the menu opened (the focus manager's previously-focused element, FLT-ALG-21), since no trigger element is associated; the consumer `finalFocus` on the Popup overrides this (MNU-API-31, MNU-FOC-09). Outside-press closes follow the focus manager's outside-press rule (FLT-FOC-08).
  Source: `menu/popup/MenuPopup.tsx:182-201`; see FLT-FOC-07, FLT-FOC-09, FLT-ALG-21. Test: [L4] `ContextMenuRoot.test.tsx` "returns focus to the focused surface when closing with Shift+Tab".

### 6.4 Dismissal

- **CTX-DIS-01** — MUST. When the current open was caused by a `contextmenu` event, outside presses (MNU-DIS-01 semantics) close the menu immediately, with no grace period.
  Source: `menu/root/MenuRoot.tsx:199,406,504-510`. Test: [L4] `ContextMenuTrigger.test.tsx` "should handle nested context menus correctly" (pointerdown outside closes the inner menu right after opening).
- **CTX-DIS-02** — MUST. When the current open was caused by anything else (a long press, `defaultOpen`, controlled `open`), outside presses during the first 500 ms after opening are ignored; after 500 ms they close the menu with reason `outside-press` (CTX-STA-08).
  Source: `menu/root/MenuRoot.tsx:312-333,504-510`. Test: [L4] `ContextMenuTrigger.test.tsx` "delays outside-press dismissal after opening from a long press".
- **CTX-DIS-03** — MUST. Because the internal backdrop covers the whole viewport without a cutout (CTX-DOM-04), a press on the Trigger area while the menu is open counts as an outside press; the right-click's `contextmenu` on the backdrop has its default prevented (CTX-PTR-03).
  Source: `menu/positioner/MenuPositioner.tsx:270-290`; `context-menu/trigger/ContextMenuTrigger.tsx:181-187`. Test: [L4] `ContextMenuTrigger.test.tsx` "blocks native context menus on both internal and external backdrops".
- **CTX-DIS-04** — MUST. Escape (MNU-DIS-02), item activation (`item-press`, MNU-DIS-06), focus-out via Shift+Tab, tree-wide closing of submenus (MNU-DIS-04), sibling-open (MNU-DIS-05), `actionsRef.current.close()` (`imperative-action`) and reason-`none` closes (MNU-DIS-13) behave as in Menu. The `cancel-open` close is CTX-PTR-05. Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:478-512`. Test: [L4] `ContextMenuTrigger.test.tsx` "adds open state attributes"; `ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item".
- **CTX-DIS-05** — MUST. Document scrolling is locked while the context menu is open (UTL scroll lock, MNU-DIS-09 mechanism) for every open, including long-press opens: the touch exemption of MNU-DIS-09 never applies because the open interaction type is never `touch` (CTX-STA-09).
  Source: `menu/positioner/MenuPositioner.tsx:245-253`; `utils/useAnchoredPopupScrollLock.ts:18-43`. Test: none.
- **CTX-DIS-06** — MUST. While the Root is `disabled`, a menu held open by `open` cannot be dismissed by Escape or outside press (MNU-DIS-12).
  Source: `menu/root/MenuRoot.tsx:501-502`. Test: none in `context-menu/`.

### 6.5 Reading direction

MNU-I18N-01/02 apply inside the popup (adopted). The `align: 'start'` default and the positive `alignOffset` default are logical: in RTL the popup's start edge is its right edge, so the default places the popup's right edge 2 px left of the pointer (FLT-POS-06, FLT-I18N rules). No ContextMenu-specific I18N requirement.

## 7. Events and callbacks

| Callback | Part | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|---|
| `onOpenChange` | Root | `(open, details: ContextMenu.Root.ChangeEventDetails)` | Every open-change request of the top-level menu | `trigger-press`, `cancel-open`, `outside-press`, `escape-key`, `focus-out`, `item-press`, `imperative-action`, `none` | yes |
| `onOpenChange` | SubmenuRoot | Menu submenu signature | MNU-EVT-07 | Menu submenu reasons | yes |
| `onOpenChangeComplete`, `onItemHighlighted`, `onCheckedChange`, `onValueChange` | as Menu | | MNU-EVT-06, MNU-EVT-08…13 | | |

Reason → action for the root:

| Reason | Open/close | Produced by | `event` |
|---|---|---|---|
| `trigger-press` | open | `contextmenu` on the Trigger (CTX-PTR-01); long press (CTX-PTR-06) | `MouseEvent`/`PointerEvent` (`contextmenu`) / `TouchEvent` (`touchstart`) |
| `cancel-open` | close | document `mouseup` ≥ 500 ms after a `contextmenu` open, outside the tree (CTX-PTR-05) | `MouseEvent` (`mouseup`) |
| `outside-press` | close | press outside (CTX-DIS-01/02) | `PointerEvent`/`MouseEvent` |
| `escape-key` | close | Escape (CTX-KEY-04) | `KeyboardEvent` |
| `focus-out` | close | Shift+Tab in the popup (CTX-KEY-05) | `KeyboardEvent` |
| `list-navigation` | close | cross-axis close key in the root popup (CTX-KEY-07; untested) | `KeyboardEvent` |
| `item-press` | close | item activation, including release activation (CTX-PTR-09) | `MouseEvent` (`click`) |
| `imperative-action` | close | `actionsRef.current.close()` | `undefined` |
| `none` | both | controlled `open` change, unmount (MNU-DIS-13) | `undefined` |

- **CTX-EVT-01** — MUST. The root `onOpenChange` is called only with the reasons in the table above. `trigger-hover`, `trigger-focus`, `sibling-open` and `close-press` are never emitted for the top-level context menu, and `list-navigation` only by the cross-axis close key of CTX-KEY-07 (they may be emitted for its submenus per MNU-EVT-07, except `trigger-focus` and `close-press`).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:69,112-115`; `menu/root/MenuRoot.tsx:478-512,521,541`; `menu/popup/MenuPopup.tsx:108-126`; see CTX-KEY-07. Test: [L4] `ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item" (`item-press`); `ContextMenuTrigger.test.tsx` "cancels opening menu on mouseup after mousedown outside after 500ms" (close after open).
- **CTX-EVT-02** — MUST. A gesture open calls `onOpenChange(true, details)` synchronously inside the `contextmenu` handler (or the long-press timer callback) with `details.reason === 'trigger-press'` and `details.event` the native `contextmenu` (resp. `touchstart`) event; a `cancel-open` close passes the document `mouseup` event.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:69,112-115,141-143`. Test: [L4] `ContextMenuTrigger.test.tsx` "should call onOpenChange when menu is opened via right click", "cancels opening menu on mouseup after mousedown outside after 500ms".
- **CTX-EVT-03** — MUST. `details.trigger` is `undefined` for every root open-change (opens carry no trigger element, and the close fill-in of MNU-STA-02 finds no active trigger).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:69,112-115`; `menu/root/MenuRoot.tsx:368-370`. Test: none.
- **CTX-EVT-04** — MUST. The pointer anchor and the initial cursor point are updated **before** `onOpenChange` is called; if the consumer cancels the open (`details.cancel()`) or a controlled consumer ignores it, the menu stays closed but the anchor keeps the new position, and the 500 ms release timer and document `mouseup` listener are still installed (a later `mouseup` ≥ 500 ms requests a `cancel-open` close, which is ignored because the menu is closed, MNU-STA-01).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:52-74,85-118`; `menu/root/MenuRoot.tsx:350-352,372-376`. Test: none.
- **CTX-EVT-05** — MUST. Each `cancel-open` close and each gesture open is a single request: `onOpenChange` is called exactly once per gesture open and once per qualifying `mouseup` (a `mouseup` inside the tree, or before 500 ms, causes no call).
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:90-117`. Test: [L4] `ContextMenuTrigger.test.tsx` "does not cancel opening menu on mouseup after mousedown outside before 500ms", "keeps the menu open when the context-menu gesture ends inside its positioner", "keeps the root menu open when the context-menu gesture ends in a portaled submenu", "aborts the pending document mouseup listener when the trigger unmounts"; `ContextMenuRoot.test.tsx` "ignores mouseup directly under the cursor when the context menu spawns there".

## 8. Form integration

Not applicable — ContextMenu has no form value; checkbox/radio items hold menu state only (MNU §8).

## 9. Lifecycle, mounting and animation

Timing terms follow conventions §4. Mount/unmount, transition statuses, `keepMounted` on the Portal, `onOpenChangeComplete`, `actionsRef.unmount()` and indicator transitions are the Menu ones.

- **CTX-ANI-01** — MUST. The context menu satisfies MNU-ANI-01…07 and MNU-ANI-09…13 (MNU-ANI-08 — trigger switching — does not apply). In particular a `defaultOpen`/initially-`open` context menu plays no enter transition (MNU-ANI-03), and Backdrop shares the root transition status (MNU-ANI-11). Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:226-235`; `menu/popup/MenuPopup.tsx:98-107`. Test: [L4] `ContextMenuTrigger.test.tsx` "adds open state attributes" (initially open).
- **CTX-ANI-02** — MUST. The instant type written on a gesture open follows MNU-STA-04: a `trigger-press` open whose event has `detail === 0` is classified as a keyboard click and sets `instantType = 'click'` (so Positioner and Popup carry `data-instant="click"`); otherwise it is unset. An Escape close sets `'dismiss'`.
  Source: `menu/root/MenuRoot.tsx:404,419-437`; `menu/utils/isKeyboardOpen.ts:8-21`. Test: none.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- CTX-KEY-04 — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01 row (adopted through MNU-KEY-09): the consumer's `cancel()` / `allowPropagation()` decision arrives a round trip after the Escape dispatch; JS applies the ADR 04 §3 fallback (prevent and stop whenever this menu is the innermost open popup). WebAssembly meets the spec through the synchronous `decide()` path.
- CTX-DIS-04 — (Escape clause) same row, adopted through MNU-DIS-02: as CTX-KEY-04.
- CTX-ANI-01 — TRN-ANI-03/09/10 row (adopted through MNU-ANI-01, MNU-ANI-06, MNU-ANI-13): after the exit animation the unmount (or `hidden`) and `onOpenChangeComplete(false)` land in the first render batch after the completion message, about two round trips later; JS sets `hidden` at animation end (optimistic convergent write, ADR 04 §2) so the popup is not visible meanwhile.
- CTX-DOM-05 — RND-STA-01 row (mirrored fields, adopted through MNU-DOM-06): the Popup's `data-side`/`data-align` come from the mirrored `side`/`align` state, which updates one round trip after the Positioner's JS-written attributes.
- CTX-POS-07 — RND-STA-01 row (mirrored fields, adopted through MNU-POS-06): the Positioner's `data-side`/`data-align` are JS-written at frame timing; the `side`/`align` state fields (and so the Popup's attributes and `ClassValue`/`StyleValue`) follow one round trip later.

## 10. Positioning

- **CTX-POS-01** — MUST. The pointer virtual anchor created by a gesture reports, from `getBoundingClientRect()`, a rectangle with `x = clientX`, `y = clientY` of the gesture (viewport coordinates) and width × height `0 × 0` for a `contextmenu` gesture or `10 × 10` for a touch long press (the touch point is the rectangle's top-left corner). The rectangle is constant: it does not follow scrolling.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:52-66`. Test: [L4] `ContextMenuTrigger.test.tsx` "should open menu on right click (context menu event)", "should open menu on long press on touchscreen devices".
- **CTX-POS-02** — MUST. With all defaults (side `bottom`, align `start`, `sideOffset -5`, `alignOffset 2`, positionMethod `fixed`), the popup is placed below the anchor with its start edge aligned to the anchor's start edge, then shifted 5 px up along the side axis and 2 px toward `end` along the alignment axis (FLT-POS-06) — for a mouse gesture in LTR its top-left corner lands at (`clientX + 2`, `clientY − 5`) before collision handling; for a long press at (`x + 2`, `y + 5`).
  Source: `menu/positioner/MenuPositioner.tsx:83-90,106-122`; see FLT-POS-01, FLT-POS-06. Test: none (offset values asserted in `MenuPositioner.test.tsx` only for explicit props).
- **CTX-POS-03** — MUST. The root positioner passes the shift configuration `{ crossAxis: !(collisionAvoidance.side === 'flip'), rootBoundary: 'layoutViewport' }` (FLT-POS-05): side-axis shifting within the **layout** viewport is on by default and off when the consumer's `collisionAvoidance` has `side: 'flip'`. Submenu positioners pass no shift configuration (visual viewport, like any submenu).
  Source: `menu/positioner/MenuPositioner.tsx:123-128`. Test: [L2] `MenuPositioner.test.tsx` "uses the layout viewport for a root context menu", "disables cross-axis shifting when side collision avoidance is flip", "uses the visual viewport for a context menu submenu" (L4 for the layout/visual-viewport shift results).
- **CTX-POS-04** — MUST. Consequently, by default the root popup does not flip to the opposite side when it lacks room below the pointer (the flip's main-axis check is disabled while side-axis shifting is on, FLT-POS-04) and is instead shifted to stay inside the layout viewport; with `collisionAvoidance={{ side: 'flip' }}` it flips (e.g. to `data-side="top"` when there is no room below).
  Source: `menu/positioner/MenuPositioner.tsx:123-128`; `internals/useAnchorPositioning.ts:288-318`. Docs: `CHANGELOG.md v1.2.0 "Fix explicit collisionAvoidance with side: 'flip' not working"`. Test: [L4] `ContextMenuRoot.test.tsx` "flips to the opposite side when side: flip is set and there is no space".
- **CTX-POS-05** — MUST. Explicit `side`, `align`, `sideOffset` and `alignOffset` props are passed to positioning unchanged (e.g. `side="right" align="center" sideOffset={11} alignOffset={13}` positions exactly so).
  Source: `menu/positioner/MenuPositioner.tsx:83-90,106-114`. Test: [L2] `MenuPositioner.test.tsx` "preserves explicit context-menu placement and offsets" (L4 for the resulting placement).
- **CTX-POS-06** — MUST. Submenu positioners inside a context menu anchor to their SubmenuTrigger with the Menu submenu defaults (MNU-POS-02 submenu clause, MNU-POS-03), except that `positionMethod` is `fixed` (CTX-API-12).
  Source: `menu/positioner/MenuPositioner.tsx:94-97,109`. Test: [L2] `MenuPositioner.test.tsx` "uses the visual viewport for a context menu submenu" (L4 for the visual-viewport result).
- **CTX-POS-07** — MUST. The root popup re-positions whenever the anchor changes (each gesture, CTX-PTR-14) per FLT-POS-17, and its resolved placement is exposed as `data-side`/`data-align` on Positioner, Popup and Arrow (MNU-POS-06); the Arrow is positioned with `arrowPadding = 0` on the root (CTX-API-11). Server: relaxed, see §9.
  Source: `menu/positioner/MenuPositioner.tsx:106-132`; see FLT-POS-17. Test: [L4] `ContextMenuRoot.test.tsx` "flips to the opposite side when side: flip is set and there is no space".

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Menu root / store | [menu](menu.md) | Rendered with all Root props; parent kind `'context-menu'`; stored `modal` never written (always modal); `rootId` from the ContextMenu root; enclosing menu hidden |
| `useDismiss` | FLT | `outsidePress()` returns `true` when the open event was `contextmenu`, else the 500 ms grace flag |
| `useListNavigation` | CMP/FLT | `openOnArrowKeyDown: false` |
| `FloatingFocusManager` | FLT | `modal: true`, `returnFocus: true` (no trigger), `initialFocus: true`, `openInteractionType: null` |
| Hover floating interaction | FLT | disabled for the root popup |
| `useAnchorPositioning` | FLT | `anchor` = pointer virtual element, `align: 'start'`, `sideOffset: -5`/`alignOffset: 2` conditional defaults, `positionMethod: 'fixed'`, `arrowPadding: 0`, `shift: { crossAxis, rootBoundary: 'layoutViewport' }` |
| Internal backdrop | PSH | always (modal), no cutout, handle registered with the context menu |
| Scroll lock | UTL | always while open (no touch exemption) |
| State attributes | RND/PSH | Trigger: pressable trigger mapping |
| Timers | UTL | 500 ms long press; 500 ms release window; 500 ms outside-press grace |
| Separator | [separator](separator.md) | used as-is |

`useClientPoint` (FLT §6.6) is **not** used: the context menu builds its own static virtual anchor (§16 Q1).

## 12. Edge cases, invariants and dev warnings

- **CTX-EDGE-01** — MUST. `ContextMenu.Trigger` rendered outside a `ContextMenu.Root` throws `Error("Base UI: ContextMenuRootContext is missing. ContextMenu parts must be placed within <ContextMenu.Root>.")`.
  Source: `context-menu/root/ContextMenuRootContext.ts:25-32`; `context-menu/trigger/ContextMenuTrigger.tsx:39`. Test: [L2] `ContextMenuTrigger.test.tsx` "throws when rendered outside ContextMenu.Root".
- **CTX-EDGE-02** — MUST. The re-exported parts throw the Menu context errors verbatim (MNU-EDGE-03, Menu part names unchanged in the messages) when misplaced; `ContextMenu.Positioner` outside a Portal throws `Base UI: <Menu.Portal> is missing.`.
  Source: `menu/root/MenuRootContext.ts:24-35`; `menu/portal/MenuPortalContext.ts:6-12`. Test: none in `context-menu/`.
- **CTX-EDGE-03** — SHOULD. A `modal` value passed to `ContextMenu.Root` at runtime (outside the typed contract) is ignored and, in development, logs `console.warn` with the nested-menu message of MNU-EDGE-02 (`Base UI: The \`modal\` prop is not supported on nested menus. It will be ignored.`), because the root's parent kind is `'context-menu'`.
  Source: `menu/root/MenuRoot.tsx:207-213`. Test: none.
- **CTX-EDGE-04** — MUST. If the Trigger unmounts between the gesture and the `mouseup`, the pending document listener is removed: a later `mouseup` does not close the menu, which stays open.
  Source: `context-menu/trigger/ContextMenuTrigger.tsx:165-171`. Test: [L3] `ContextMenuTrigger.test.tsx` "aborts the pending document mouseup listener when the trigger unmounts".
- **CTX-EDGE-05** — MUST. Every part rendered anywhere inside a `ContextMenu.Root` sees the context-menu shared state, including a plain `Menu.Root` inside the Trigger or popup: its positioner is forced to `positionMethod: 'fixed'` (CTX-API-12), its `Menu.Backdrop` registers as the context menu's backdrop, and its items apply the context-menu release rules (only right-button `mouseup` on macOS, CTX-PTR-09…12), so press-drag-release activation with the primary button (MNU-PTR-03) does not work in such a nested menu.
  Source: `menu/positioner/MenuPositioner.tsx:59,109`; `menu/backdrop/MenuBackdrop.tsx:29-39`; `menu/item/useMenuItemCommonProps.ts:59,73,107-131`. Test: none.
- **CTX-EDGE-06** — MUST. Before any gesture the release-arming flag is `true` (CTX-STA-06), so in a menu opened by `defaultOpen`/`open` on macOS the first right-button `mouseup` on an item activates it.
  Source: `context-menu/root/ContextMenuRoot.tsx:27`; `menu/item/useMenuItemCommonProps.ts:127-141`. Test: none.

Invariants: at most one top-level popup per `ContextMenu.Root`; the Trigger's `data-popup-open` always equals the Root's open state; every popup of the tree carries the same `data-rootownerid`. Shadow DOM: the `data-rootownerid` walk crosses shadow hosts (`getParentNode`), and event targets are resolved with composed-path awareness (`getTarget`). Server rendering: nothing is computed at render time from the DOM; the anchor is created only by client gestures.

## 13. Behaviors evidenced by tests

### `root/ContextMenuRoot.test.tsx`

Platform mocked as macOS (`platform.os.mac = true`). Animations disabled.

| Test | Requirements |
|---|---|
| "closes nested submenus when releasing the context menu pointer over an item" | CTX-PTR-01, CTX-PTR-09, CTX-STA-06, CTX-STR-07, CTX-EVT-01, CTX-DIS-04, MNU-DIS-04, MNU-DIS-06, MNU-PTR-09 |
| "does not activate a submenu trigger when releasing the context menu pointer over it" | CTX-PTR-13 |
| "ignores mouseup directly under the cursor when the context menu spawns there" | CTX-PTR-10, CTX-STA-07, CTX-API-09, CTX-EVT-05 |
| "ignores mouseup directly under the cursor when alignOffset is negative" | CTX-PTR-10, CTX-API-09 |
| "allows mouseup after leaving the initial cursor point" | CTX-PTR-09, CTX-STA-06 |
| "does not open when disabled" | CTX-PTR-02, CTX-API-01 |
| "returns focus to the focused surface when closing with Shift+Tab" | CTX-KEY-02, CTX-KEY-05, CTX-FOC-01, CTX-FOC-02, CTX-FOC-03 |
| "prop: collisionAvoidance" › "flips to the opposite side when side: flip is set and there is no space" (browser only) | CTX-POS-04, CTX-API-10, CTX-API-13, CTX-STA-02, CTX-DOM-05, CTX-POS-07 |

### `root/ContextMenuRoot.non-mac.test.tsx`

Platform mocked as non-macOS.

| Test | Requirements |
|---|---|
| "ignores context menu mouseup on non-Mac platforms" | CTX-PTR-11 |

### `trigger/ContextMenuTrigger.test.tsx`

| Test | Requirements |
|---|---|
| conformance suite (`refInstanceof: HTMLDivElement`, rendered inside `ContextMenu.Root`) | RND-DOM-36…48; CTX-STR-01, CTX-STR-04, CTX-STR-08, CTX-API-06, CTX-DOM-02 |
| "throws when rendered outside ContextMenu.Root" | CTX-EDGE-01, CTX-STR-04 |
| "should open menu on right click (context menu event)" | CTX-PTR-01, CTX-POS-01 |
| "adds open state attributes" | CTX-DOM-01, CTX-STA-01, CTX-KEY-04, CTX-STA-04, CTX-ANI-01, CTX-API-01 |
| "should call onOpenChange when menu is opened via right click" | CTX-PTR-01, CTX-EVT-02, CTX-API-02, CTX-STA-03 |
| "does not cancel opening menu on mouseup after mousedown outside before 500ms" | CTX-PTR-05, CTX-EVT-05 |
| "cancels opening menu on mouseup after mousedown outside after 500ms" | CTX-PTR-05, CTX-EVT-01, CTX-EVT-02 |
| "keeps the menu open when the context-menu gesture ends inside its positioner" | CTX-PTR-05, CTX-EVT-05 |
| "keeps the root menu open when the context-menu gesture ends in a portaled submenu" | CTX-PTR-05, CTX-DOM-03, CTX-STA-10, CTX-EVT-05 |
| "aborts the pending document mouseup listener when the trigger unmounts" | CTX-PTR-04, CTX-EDGE-04, CTX-EVT-05 |
| "prop: disabled" › "does not open on right-click when disabled" | CTX-PTR-02 |
| "prop: disabled" › "does not block the native context menu when disabled" | CTX-PTR-02, CTX-PTR-03 |
| "blocks native context menus on both internal and external backdrops" | CTX-PTR-03, CTX-DOM-04, CTX-STA-05, CTX-DIS-03 |
| "blocks native context menus in a portal mounted inside the trigger DOM subtree" | CTX-PTR-03 |
| "blocks the native context menu when onContextMenu skips the Base UI handler" | CTX-PTR-03, CTX-API-06 |
| "long press" › "should open menu on long press on touchscreen devices" (browser only) | CTX-PTR-06, CTX-POS-01 |
| "long press" › "should cancel long press when touch moves beyond threshold" | CTX-PTR-07 |
| "long press" › "keeps a pending long press when touch movement stays within the threshold" | CTX-PTR-07 |
| "long press" › "cancels a pending long press when the touch ends" | CTX-PTR-07 |
| "long press" › "cancels a pending long press when the gesture becomes multi-touch" | CTX-PTR-07 |
| "long press" › "delays outside-press dismissal after opening from a long press" | CTX-DIS-02, CTX-STA-08 |
| "long press" › "does not open on long press when disabled" | CTX-PTR-07, CTX-PTR-02 |
| "should handle nested context menus correctly" | CTX-STR-03, CTX-STR-09, CTX-PTR-15, CTX-DIS-01 |

### Tests in other files that exercise this component

| File / test | Requirements |
|---|---|
| `menu/positioner/MenuPositioner.test.tsx` "uses the layout viewport for a root context menu" | CTX-POS-03, MNU-POS-04 |
| `menu/positioner/MenuPositioner.test.tsx` "disables cross-axis shifting when side collision avoidance is flip" | CTX-POS-03 |
| `menu/positioner/MenuPositioner.test.tsx` "preserves explicit context-menu placement and offsets" | CTX-POS-05, CTX-API-07, CTX-API-08, CTX-API-09 |
| `menu/positioner/MenuPositioner.test.tsx` "uses the visual viewport for a context menu submenu" | CTX-POS-03, CTX-POS-06, CTX-STR-07 |

No requirement exists only because of a test beyond those already placed in §3–§12.

## 14. React-specific notes and porting considerations

- **Context reset.** `ContextMenu.Root` renders `<MenuRootContext.Provider value={undefined}>` around its `Menu.Root` so that the menu root does not see an enclosing menu. A port needs an equivalent "this is a new top-level menu tree" boundary. Port: see decisions/05-state-store-and-event-details.md §1 (the menu store travels as a fixed cascaded value, so the boundary is a re-provided absent store).
- **Event-handler ordering.** Release activation (CTX-PTR-09) relies on the item's `mouseup` handler (React delegates at the app root container) running **before** the native `mouseup` listener on `document` that disarms the flag (CTX-PTR-05). A port that attaches the item handler directly to the item gets the same order by bubbling; one that handles both at the document level must preserve item-first ordering. Port: see decisions/04-js-interop-boundary.md §1 (no module is named for the context-menu document `mouseup` listener; §16 Q14).
- **`stopEvent` on `contextmenu`.** `preventDefault()` + React `stopPropagation()` (which also stops the native event at the root container). Because the document-level `contextmenu` listener (CTX-PTR-03) runs after that, it may not see the event when the Trigger handled it — it exists for the cases where the Trigger's handler did not run (backdrops, `preventBaseUIHandler`, disabled-check order). A port should keep both paths. Port: see decisions/04-js-interop-boundary.md §3 (`preventDefault` inside a handler is a JS-side decision; the owning module is open, §16 Q14).
- **Imperative bridge.** The Trigger calls the menu's internal `setOpen` through a mutable handle (`actionsRef` in the ContextMenu context, filled with `useImperativeHandle` by `Menu.Root`); the root positioner element is published the same way. These are plumbing, not public API (`ContextMenu.Root`'s public `actionsRef` is the Menu one). Port: see decisions/05-state-store-and-event-details.md §1.
- **Parent sync effect.** The context-menu parent and floating-tree node ids are re-written into the store in a layout effect on every relevant change (`MenuRoot.tsx:283-302`), because ContextMenu "doesn't support detached triggers yet". A port can set them once at construction. Port: see decisions/05-state-store-and-event-details.md §1 (prop-to-store sync in `OnParametersSet`).
- **Timers.** `useTimeout` instances are cleared on unmount; the long-press timer, the 500 ms release window and the 500 ms grace timer must all be cancelled when the owning part goes away. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, macrotask row: C# timers when the result is state); which module owns the touch long-press timer is open (§16 Q14).
- **`AbortController`** scopes the one-shot `mouseup` listener; any equivalent "remove previous listener / remove on unmount" scheme suffices. Port: see decisions/04-js-interop-boundary.md §3 (`attach` → handle with `dispose()`).
- **Escape close.** Escape (CTX-KEY-04) is a JS-owned dismissal whose consumer veto is a decision point. Port: see decisions/04-js-interop-boundary.md §3 (`decide()`, fallback prevent-and-stop when innermost); decisions/01-hosting-model-and-timing.md §Relaxation table (EVD-EVT-11, EVD-DIS-01).
- **Platform detection** (`platform.os.mac`) is a module-level constant in the source (mocked in tests). A port should resolve it once from the user agent / platform API. Port: see decisions/04-js-interop-boundary.md §4 (the JS initializer computes platform flags, UTL-ALG-11).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| CTX-STR-01 | MUST | Namespace parts: 3 own, 15 re-exported Menu parts, generic Separator; no Menu.Trigger/Viewport/List/filter/handle | 2 |
| CTX-STR-02 | MUST | Adopts MNU requirements with substitutions, minus not-adopted and overridden IDs | 2 |
| CTX-STR-03 | MUST | Root renders no element, owns context-menu state, hides enclosing menu | 2 |
| CTX-STR-04 | MUST | Trigger renders a div and must be inside Root | 2 |
| CTX-STR-05 | MUST | Separator is the standalone Separator | 2 |
| CTX-STR-06 | MUST | Menu.* parts interchangeable with ContextMenu.* names | 2 |
| CTX-STR-07 | MUST | Parent-kind classification under a ContextMenu | 2 |
| CTX-STR-08 | MUST | Trigger renders no sibling elements / focus guards | 2 |
| CTX-STR-09 | MUST | Nested ContextMenu roots are independent | 2 |
| CTX-API-01 | MUST | Inherited Root props behave as Menu | 3.1 |
| CTX-API-02 | MUST | onOpenChange typed with ContextMenu details, Menu reason union | 3.1 |
| CTX-API-03 | MUST | children is plain content (no payload function) | 3.1 |
| CTX-API-04 | MUST | handle/triggerId/defaultTriggerId/modal not in contract; always modal | 3.1 |
| CTX-API-05 | MUST | closeParentOnEsc on Root has no effect | 3.1 |
| CTX-API-06 | MUST | Trigger has no own props; attributes forwarded; consumer handlers first | 3.2 |
| CTX-API-07 | MUST | Positioner side: root engine default bottom; submenu inline-end | 3.3 |
| CTX-API-08 | MUST | Positioner align defaults to start | 3.3 |
| CTX-API-09 | MUST | Root offsets -5/2 when no side and align ≠ center; else 0 | 3.3 |
| CTX-API-10 | MUST | anchor prop overrides pointer anchor; submenu anchors to its trigger | 3.3 |
| CTX-API-11 | MUST | Root arrowPadding forced 0; submenu default 5 | 3.3 |
| CTX-API-12 | MUST | positionMethod forced fixed for every menu inside ContextMenu.Root | 3.3 |
| CTX-API-13 | MUST | Other positioner props have Menu defaults | 3.3 |
| CTX-STA-01 | MUST | Trigger state is exactly { open } = root open | 3.2 |
| CTX-STA-02 | MUST | Positioner/Popup/Backdrop/Arrow state objects are Menu ones | 3.3 |
| CTX-STA-03 | MUST | Open state per Menu; gesture re-open not deduplicated | 4 |
| CTX-STA-04 | MUST | Pointer anchor: initial 0×0 at (0,0), replaced per gesture, kept after close | 4 |
| CTX-STA-05 | MUST | Always modal | 4 |
| CTX-STA-06 | MUST | Release-arming flag lifecycle (shared with submenus) | 4 |
| CTX-STA-07 | MUST | Initial cursor point recorded per gesture, consumed by first item mouseup | 4 |
| CTX-STA-08 | MUST | Outside-press grace flag: true 500 ms after open, reset on close | 4 |
| CTX-STA-09 | MUST | Open interaction type never set | 4 |
| CTX-STA-10 | MUST | Tree root id is the ContextMenu root's id | 4 |
| CTX-DOM-01 | MUST | Trigger data-popup-open/data-pressed iff open | 5.1 |
| CTX-DOM-02 | MUST | Trigger only emits -webkit-touch-callout: none | 5.1 |
| CTX-DOM-03 | MUST | Every popup carries data-rootownerid = context root id | 5.2 |
| CTX-DOM-04 | MUST | Internal backdrop always, no cutout, registered | 5.2 |
| CTX-DOM-05 | MUST | Positioner/Popup/etc. render Menu attributes; data-nested only on submenus | 5.2 |
| CTX-DOM-06 | MUST | Separator attributes per SEP | 5.2 |
| CTX-ARIA-01 | MUST | Trigger has no ARIA attributes or role | 5.3 |
| CTX-ARIA-02 | MUST | Popup role menu without generated aria-labelledby | 5.3 |
| CTX-ARIA-03 | MUST | Separator role separator + aria-orientation | 5.3 |
| CTX-ARIA-04 | MUST | Outside content aria-hidden while open | 5.3 |
| CTX-ARIA-05 | MUST | Root portal owner span has no role | 5.3 |
| CTX-KEY-01 | MUST | Trigger has no keyboard handling | 6.1 |
| CTX-KEY-02 | MUST | Keyboard-originated contextmenu events handled like pointer ones | 6.1 |
| CTX-KEY-03 | MUST | Menu keys apply inside the popup | 6.1 |
| CTX-KEY-04 | MUST | Escape closes innermost; root focus per CTX-FOC-03 | 6.1 |
| CTX-KEY-05 | MUST | Shift+Tab closes (focus-out), focus to pre-open element | 6.1 |
| CTX-KEY-06 | MUST | Tab trapped in popup | 6.1 |
| CTX-KEY-07 | SHOULD | Cross-axis close key closes the root with `list-navigation` (untested) | 6.1 |
| CTX-PTR-01 | MUST | contextmenu on Trigger: prevent, record, anchor, open, arm, listen | 6.2 |
| CTX-PTR-02 | MUST | Disabled Root: contextmenu ignored, native menu not blocked | 6.2 |
| CTX-PTR-03 | MUST | Document listener suppresses native menu on trigger/backdrops | 6.2 |
| CTX-PTR-04 | MUST | One one-shot document mouseup listener per gesture; removed on unmount | 6.2 |
| CTX-PTR-05 | MUST | mouseup ≥ 500 ms outside tree closes with cancel-open | 6.2 |
| CTX-PTR-06 | MUST | Single-touch 500 ms long press opens with 10×10 anchor | 6.2 |
| CTX-PTR-07 | MUST | Long press cancelled by >10 px move, end, cancel, multi-touch, disabled | 6.2 |
| CTX-PTR-08 | MUST | touchstart disarms release activation | 6.2 |
| CTX-PTR-09 | MUST | macOS right-button release on item activates via synthetic click | 6.2 |
| CTX-PTR-10 | MUST | First item mouseup within 1 px of open point ignored | 6.2 |
| CTX-PTR-11 | MUST | Non-macOS right-button item mouseup never activates | 6.2 |
| CTX-PTR-12 | MUST | Non-right-button mouseup never release-activates | 6.2 |
| CTX-PTR-13 | MUST | Release over SubmenuTrigger does nothing | 6.2 |
| CTX-PTR-14 | MUST | contextmenu while open re-anchors and re-requests open | 6.2 |
| CTX-PTR-15 | MUST | Nested triggers: innermost opens only | 6.2 |
| CTX-PTR-16 | MUST | Root popup has no hover-close | 6.2 |
| CTX-FOC-01 | MUST | Gesture open focuses the popup, no highlight | 6.3 |
| CTX-FOC-02 | MUST | Modal focus management | 6.3 |
| CTX-FOC-03 | MUST | Return focus to element focused before open; finalFocus overrides | 6.3 |
| CTX-DIS-01 | MUST | contextmenu-opened menu: outside press closes immediately | 6.4 |
| CTX-DIS-02 | MUST | Other opens: outside presses ignored for 500 ms | 6.4 |
| CTX-DIS-03 | MUST | Press on Trigger area while open is outside press | 6.4 |
| CTX-DIS-04 | MUST | Menu dismissals apply | 6.4 |
| CTX-DIS-05 | MUST | Scroll lock on every open, no touch exemption | 6.4 |
| CTX-DIS-06 | MUST | Disabled Root disables dismissal | 6.4 |
| CTX-EVT-01 | MUST | Root reasons limited to the listed set | 7 |
| CTX-EVT-02 | MUST | Gesture open/cancel-open event objects | 7 |
| CTX-EVT-03 | MUST | details.trigger always undefined at root | 7 |
| CTX-EVT-04 | MUST | Anchor updated before onOpenChange; persists if canceled | 7 |
| CTX-EVT-05 | MUST | One onOpenChange per gesture / qualifying mouseup | 7 |
| CTX-ANI-01 | MUST | Menu lifecycle requirements apply (except trigger switching) | 9 |
| CTX-ANI-02 | MUST | detail===0 gesture event → instantType 'click' | 9 |
| CTX-POS-01 | MUST | Anchor rect: 0×0 (mouse) / 10×10 (touch) at client point, static | 10 |
| CTX-POS-02 | MUST | Default placement math (x+2, y−5) | 10 |
| CTX-POS-03 | MUST | Root shift config: layoutViewport, crossAxis unless side flip | 10 |
| CTX-POS-04 | MUST | Default shifts instead of flipping; side: 'flip' flips | 10 |
| CTX-POS-05 | MUST | Explicit placement props honored | 10 |
| CTX-POS-06 | MUST | Submenus use Menu submenu defaults, fixed | 10 |
| CTX-POS-07 | MUST | Re-positions on anchor change; data-side/data-align | 10 |
| CTX-EDGE-01 | MUST | Trigger outside Root throws (verbatim) | 12 |
| CTX-EDGE-02 | MUST | Re-exported parts throw Menu context errors | 12 |
| CTX-EDGE-03 | SHOULD | Runtime modal prop ignored with nested-menu warning | 12 |
| CTX-EDGE-04 | MUST | Trigger unmount removes pending mouseup listener | 12 |
| CTX-EDGE-05 | MUST | Plain Menu inside ContextMenu subtree inherits context-menu quirks | 12 |
| CTX-EDGE-06 | MUST | Release flag initially armed for programmatic opens | 12 |

## 16. Open questions

1. **`useClientPoint` not used.** The assignment pointed at FLT's `useClientPoint` for virtual-anchor positioning, but ContextMenu builds its own static virtual element (`context-menu/trigger/ContextMenuTrigger.tsx:57-66`) passed as the Positioner `anchor` (`menu/positioner/MenuPositioner.tsx:84`); `useClientPoint` is used only by Tooltip (`tooltip/root/TooltipRoot.tsx`). The anchor therefore does not follow the cursor and does not track page scroll.
2. **Menu doc discrepancies to reconcile.** (a) MNU-DIS-10 / MNU-PTR-18 describe a 500 ms outside-press grace for every context-menu open; the source skips the grace when the open event was `contextmenu` (`menu/root/MenuRoot.tsx:505`) — CTX-DIS-01/02 follow the source. (b) MNU-FOC-13 says Tab/Shift+Tab "cycle within the popup"; Shift+Tab is intercepted by list navigation and closes the menu (test "returns focus to the focused surface when closing with Shift+Tab"). (c) MNU §7.1 says `trigger-focus` is emitted by "ContextMenu integrations"; no ContextMenu code emits it. (d) MNU-STR-10/MNU-ARIA-12 describe `Menu.Separator` as the listbox separator with `role="presentation"`; both `menu/index.parts.ts:19` and `context-menu/index.parts.ts:21` export the standalone `Separator` (`role="separator"`, `aria-orientation`).
   Resolved (QA 2026-10-14): menu.md corrected — (a) MNU-DIS-10, MNU-PTR-18; (b) MNU-FOC-13; (c) §7.1, MNU-FOC-16, MNU-EVT-04 (`trigger-focus` comes only from `Menu.Trigger` inside a Menubar while another bar menu is open; ContextMenu never emits it); (d) MNU-STR-10, MNU-ARIA-12, §3.18.
3. **`nested-context-menu` parent kind** is declared in `MenuParent` (`menu/root/MenuRoot.tsx:972-976`) and checked in `menu/positioner/MenuPositioner.tsx:283`, but nothing ever assigns it in v1.9.0 — dead branch, not specified.
4. **Keyboard opening.** No Shift+F10 / ContextMenu-key handling exists in source; keyboard opening depends entirely on the browser dispatching `contextmenu` (CTX-KEY-02). The coordinates browsers report for keyboard `contextmenu` events vary (often the focused element's position or 0,0); untested.
5. **`detail === 0` classification (CTX-ANI-02).** `isKeyboardClick` treats any `trigger-press` whose event has `detail === 0` as a keyboard click. `contextmenu` events created by `fireEvent` and touch events typically have `detail === 0`, so a pointer gesture may mark the popup `data-instant="click"` and `keyboardOpen = true` (`menu/root/MenuRoot.tsx:419-432`). Likely unintended; behavior in real browsers depends on their `contextmenu.detail`.
6. **Second right-click while open.** With the default modal internal backdrop covering the viewport (no cutout), a second right click normally lands on the backdrop rather than the Trigger, so it dismisses as an outside press (CTX-DIS-03) rather than re-anchoring (CTX-PTR-14); which one the user sees depends on stacking (z-index) of the consumer's Trigger. Untested.
7. **Omitted prop names.** `ContextMenuRootProps` omits `openOnHover`, `delay`, `closeDelay` (`context-menu/root/ContextMenuRoot.tsx:65-67`), which are not `Menu.Root` props in v1.9.0 (they are `Menu.Trigger` props) — harmless no-ops in the type.
8. **Runtime pass-through.** `ContextMenu.Root` spreads all props onto `Menu.Root` (`context-menu/root/ContextMenuRoot.tsx:49`); untyped `handle`/`triggerId`/`defaultTriggerId` passed at runtime would reach the menu root and are not guarded. Not specified (CTX-API-04 excludes them from the contract).
9. **Plain Menu inside a ContextMenu (CTX-EDGE-05).** `useMenuItemCommonProps` decides "context menu item" by the presence of the ContextMenu context, not by the menu's parent kind (`menu/item/useMenuItemCommonProps.ts:59,73`), so a `Menu.Root` inside a `ContextMenu.Trigger` loses primary-button drag-release activation and its positioner is forced to `fixed`. Probably unintended; no test.
10. **Long press on platforms that also fire `contextmenu`.** On browsers that dispatch `contextmenu` for a touch long press, both CTX-PTR-06 (timer) and CTX-PTR-01 (event) may run for one gesture, giving two open requests (the second re-anchors to a 0 × 0 rect and installs the `mouseup` listener). The source does not deduplicate; untested.
11. **Multiple Triggers per Root.** Nothing prevents several `ContextMenu.Trigger`s under one Root (each installs its own document listeners and shares the anchor and arming state); untested, specified only implicitly by cardinality 0..n in §2.
12. **Outline gaps.** The outline omits: `ContextMenu.Root` `closeParentOnEsc` (deprecated, `@ignore`), `ContextMenu.Positioner` `positionMethod` (deprecated, `@ignore`), `data-nested` on Positioner/Popup (as in MNU §16 Q3), `data-instant` on the Positioner, `data-disabled` on RadioGroup, and the `ContextMenu.Popup` `id` prop (`@ignore` in Menu). It lists everything else found in source.
13. **Cross-axis close key closes the root (CTX-KEY-07).** `menu/root/MenuRoot.tsx:521` passes `nested: true` for the top-level menu under a ContextMenu, so `floating-ui-react/hooks/useListNavigation.ts:570-583` closes it on ArrowLeft (LTR) / ArrowRight (RTL) with reason `list-navigation`; there is no test and nothing in the docs page mentions it (mirror of menu.md §16 Q9). Needs the user's decision: replicate (as specified, SHOULD) or treat as a Base UI bug and omit. Decided (2026-10-18): not replicated by the port (ADR 04 Amendments); CTX-KEY-07 stays as the Base UI description.
14. **Interop module for the Trigger gestures.** ADR 04 §1 names no `context-menu` module. The Trigger must call `preventDefault()`/`stopPropagation()` synchronously on `contextmenu` (CTX-PTR-01), keep a document `contextmenu` suppressor (CTX-PTR-03), run the 500 ms touch long-press timer with its 10 px move threshold (CTX-PTR-06/07) and own the one-shot document `mouseup` listener with its 500 ms window and `data-rootownerid` walk (CTX-PTR-04/05) (`context-menu/trigger/ContextMenuTrigger.tsx:76-192`). The `button` module's press-and-hold timers cover buttons only, and the `interactions` module's cursor anchoring is `useClientPoint`, which ContextMenu does not use (Q1). Needs the user's decision: extend `interactions` (or `button`) with these gestures, or add a `context-menu` component module (amends ADR 04 §1). Decided (2026-10-18): a `context-menu` component module (ADR 04 Amendments, module placement rule).
15. **ContextMenu inside a Menubar.** Parent-kind detection checks for an enclosing Menubar before the ContextMenu (`menu/root/MenuRoot.tsx:102-117`), and `ContextMenu.Root` clears only the enclosing menu, not the Menubar (`context-menu/root/ContextMenuRoot.tsx:47-49`), so a context menu rendered inside a `Menubar` gets parent kind `'menubar'` and loses the context-menu anchoring, modality and outside-press rules (CTX-STR-07 does not cover this case). Untested; mirror of menubar.md §16 Q7.
