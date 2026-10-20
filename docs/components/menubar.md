# Menubar

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/menubar/` (4 files) plus the Menu branches on `parent.type === 'menubar'` in `menu/root/MenuRoot.tsx`, `menu/trigger/MenuTrigger.tsx`, `menu/positioner/MenuPositioner.tsx`, `menu/popup/MenuPopup.tsx`, `menu/portal/MenuPortal.tsx`, `menu/store/MenuStore.ts`, `menu/filter-root/MenuFilterDropdown.tsx` |
| Tests | `packages/react/src/menubar/Menubar.test.tsx` (1 file); also `menu/positioner/MenuPositioner.test.tsx` "Menubar parent" (2 tests) |
| Docs page | `docs/src/app/(docs)/react/components/menubar/page.mdx` |
| Prefix | `MBR` |
| Status | approved |
| Builds on | [menu](menu.md) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md) |

## 1. Overview

Menubar is a horizontal (or vertical) bar of menu triggers, as in a desktop application's "File / Edit / View" bar. It has exactly one part of its own, `Menubar`, which renders the `role="menubar"` container. Everything inside it is ordinary [Menu](menu.md) parts: each `Menu.Root` placed in the bar (or each `Menu.Trigger` placed in the bar, when the triggers are detached through a `Menu.Handle`) becomes one entry of the bar.

The Menubar turns its triggers into one roving-tabindex group: the bar is a single Tab stop, arrow keys along the bar's axis move between triggers, and Home/End jump to the ends. Once any menu of the bar is open, the bar switches into "menu mode": moving the mouse onto another trigger, or moving keyboard focus to another trigger (arrow keys, including arrow keys pressed inside an open menu), opens that trigger's menu immediately and closes the previous one, without enter/exit animations (`data-instant="group"`). While no menu is open, hovering or focusing a trigger does nothing; a press, Enter/Space, or the cross-axis arrow key (ArrowDown in a horizontal bar) opens a menu.

Menus inside a Menubar behave as Menu documents them, with the overrides listed in this document: they ignore their own `modal` prop (the bar's `modal` decides backdrop and scroll lock), they never close because the pointer leaves them, their triggers have `role="menuitem"` and no focus guards, they position below (horizontal bar) or beside (vertical bar) the trigger aligned to its start, and every popup in the bar shares the bar's id as its root owner id.

This document is a derived document over [menu](menu.md) (conventions §3.1): requirements are adopted from MNU through MBR-STR-05 and only the additions and overrides are specified individually.

## 2. Anatomy

```
<Menubar>                          required, 1                        default element: div (role="menubar")
  <Menu.Root>                      0..n, any depth inside the bar     no element
    <Menu.Trigger>                 0..n per Root; each is a bar item  button (role="menuitem")
    <Menu.Portal> … (full Menu anatomy, MNU §2)
  <Menu.Trigger handle={h}>        0..n detached triggers             button (role="menuitem")
</Menubar>
<Menu.Root handle={h}> …           optional, outside the bar          (detached menu, MNU-API-14)
```

`Menubar` is the only part this document introduces. It is not structural-only: it provides keyboard navigation, the bar-level open state (`hasSubmenuOpen`) and the modality used by its menus.

- **MBR-STR-01** — MUST. `Menubar` renders one `<div>` element by default (element substitution, `className`/`style` functions and attribute forwarding per RND-API-01..04), with the attributes of §5.1, and renders its children inside it.
  Source: `menubar/Menubar.tsx:74-94`; `internals/composite/root/CompositeRoot.tsx:67-72`. Test: [L2] `Menubar.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **MBR-STR-02** — MUST. The Menubar element is a composite root (roving group, CMP §4.5): every `Menu.Trigger` rendered anywhere inside its subtree — the trigger of a `Menu.Root` placed in the bar, several triggers of one `Menu.Root`, or a detached trigger bound to a handle whose Root is elsewhere — is registered as a composite item of the bar in DOM order (CMP-STA-02). Other descendants (plain buttons, separators) are not items and are not reached by the bar's arrow keys. Server: relaxed, see §9.
  Source: `menubar/Menubar.tsx:78-90`; `menu/trigger/MenuTrigger.tsx:272-285`. Test: [L2] `Menubar.test.tsx` "sets role="menuitem" on menu triggers" (3 items for each of the contained, detached and multiple-contained setups), "should navigate between menubar items with arrow keys" (L4 for the navigation citations).
- **MBR-STR-03** — MUST. The Menubar establishes its own floating tree (FLT-STR-07) and registers itself as a node of it. Every top-level menu of the bar registers its popup as a child node of the Menubar node (its parent node id is the Menubar node id); submenus of a bar menu register below that menu as usual (MNU-STR-11). A `Menu.Root` whose parent kind is `'menubar'` does not create a floating tree of its own.
  Source: `menubar/Menubar.tsx:76-77,97-128`; `menu/trigger/MenuTrigger.tsx:86-108`; `menu/root/MenuRoot.tsx:754-757`. Test: [L4] `Menubar.test.tsx` "should open another menu on hover when a nested submenu is open".
- **MBR-STR-04** — MUST. A `Menu.Root` rendered inside a Menubar, and not as a `Menu.SubmenuRoot` of another menu, has parent kind `'menubar'` (MNU-STR-19; the `'menu'` kind of a submenu takes precedence). A `Menu.Root` rendered outside the bar whose triggers are detached triggers inside the bar takes on parent kind `'menubar'` (and the bar's floating tree, parent node and keyboard relay) as soon as one of those triggers is the trigger that mounted its popup; the parent kind of its Portal's ownership element is still decided by the Root's own position (MBR-ARIA-04).
  Source: `menu/root/MenuRoot.tsx:94-122`; `menu/trigger/MenuTrigger.tsx:95-108,394-411`; `utils/popups/popupStoreUtils.ts:323-394`; `menu/portal/MenuPortal.tsx:29-32`. Test: [L2] `Menubar.test.tsx` every test in the "when using detached triggers" variant; "renders detached top-level portal ownership without an accessibility role" (L4 for the interaction tests of the detached-trigger variant).
- **MBR-STR-05** — MUST. Every Menu part rendered under a Menubar-parented menu (parent kind `'menubar'`, and the submenus below it) MUST satisfy every MNU requirement, with these substitutions and overrides:

  | MNU requirement(s) | Under a Menubar |
  |---|---|
  | MNU-STR-16, MNU-FOC-06, MNU-FOC-07 (trigger focus guards) | No guards are rendered; the trigger is a bar item (MBR-DOM-07, MBR-KEY-11). |
  | MNU-STR-17, MNU-DOM-25 (internal backdrop condition and cutout) | Replaced by MBR-DOM-08. |
  | MNU-STR-18, MNU-ARIA-18 (portal owner role) | `group` when the Root is inside the bar (MBR-ARIA-04). |
  | MNU-API-08, MNU-STA-06, MNU-DIS-09 (modality, scroll lock) | The Root's `modal` is ignored; the bar's `modal` decides (MBR-API-07, MBR-DIS-05). |
  | MNU-API-10, MNU-API-16, MNU-STA-07 (disabled) | OR-ed with the bar's `disabled` (MBR-API-08). |
  | MNU-API-19, MNU-API-20, MNU-API-21, MNU-PTR-05 … MNU-PTR-08, MNU-DIS-11 (trigger hover) | Replaced by MBR-API-09, MBR-API-10, MBR-PTR-03 … MBR-PTR-05. MNU-PTR-08's `hoverEnabled` gating still governs the popups of submenus inside bar menus (MNU-STA-11). |
  | MNU-API-26, MNU-POS-02 (placement defaults) | MBR-POS-01. |
  | MNU-KEY-01, MNU-KEY-02 (arrow keys on the trigger) | Replaced by MBR-KEY-01 … MBR-KEY-05. |
  | MNU-KEY-03 (Enter/Space on the trigger) | Unchanged except that there is no 500 ms keep-open after a hover open (MBR-KEY-06, MBR-PTR-02). |
  | MNU-KEY-10 (Tab) | MBR-KEY-11. |
  | MNU-KEY-21 (keyboard relay) | Specified from the bar side by MBR-KEY-08, MBR-KEY-13. |
  | MNU-KEY-28 (filter trigger key relay) | Disabled (MBR-KEY-14). |
  | MNU-PTR-01, MNU-PTR-06 (trigger press, stick-if-open) | MBR-PTR-01, MBR-PTR-02. |
  | MNU-FOC-04 (return focus) | MBR-FOC-05. |
  | MNU-FOC-16 (focus-open interaction of `Menu.Trigger`, enabled only inside a bar while another bar menu is open) | Specified from the bar side by MBR-FOC-03 (focus opens with `trigger-focus`) and MBR-EVT-03 (trigger blur closes with `trigger-focus`); this is the only `trigger-focus` emitter for Menu. |
  | MNU-STA-04, MNU-ANI-07 (instant type) | MBR-ANI-01. |
  | MNU-DOM-07 (`data-rootownerid`) | Value is the Menubar id (MBR-DOM-09). |
  | MNU-STA-12 / MNU-PTR-02 … MNU-PTR-04 (drag-release arming) | The arming flag is shared by all menus of the bar (MBR-STA-07). |

  All other MNU requirements (items, groups, checkbox/radio items, submenus, filtering, list navigation inside the popup, typeahead inside the popup, transitions, events) apply unchanged.
  Source: the branches cited by each MBR ID named in the table. Test: [L4] `Menubar.test.tsx` (whole file).
- **MBR-STR-06** — MUST. A `Menu.Root` with no trigger MAY be placed inside a Menubar (e.g. opened through `open`); it participates as a bar menu (parent kind `'menubar'`) but contributes no bar item.
  Source: `menu/root/MenuRoot.tsx:102-107`. Test: [L4] `Menubar.test.tsx` "keeps focus on an outside target when a triggerless menubar menu closes".

## 3. Public API

### 3.1 `Menubar`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (`Menubar.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` (`MenuRoot.Orientation`) | `'horizontal'` | Axis of the bar's arrow-key navigation, and of the default menu placement. | MBR-API-01 |
| `loopFocus` | `boolean` | `true` | Arrow keys wrap from the last trigger to the first and back. | MBR-API-02 |
| `modal` | `boolean` | `true` | Open bar menus render the internal backdrop and lock page scroll. | MBR-API-03 |
| `disabled` | `boolean` | `false` | Disables every menu of the bar. | MBR-API-04 |
| `id` | `string` | generated | Element id; also the root owner id of every popup in the bar. Not in the outline. | MBR-API-05 |
| `className`, `style`, `render` | per RND | — | | RND-API-01..03 |

**Consumed (not forwarded):** `orientation`, `loopFocus`, `modal`, `disabled`, `id` (re-emitted as the resolved id). All other attributes are forwarded (MBR-API-06).

- **MBR-API-01** — MUST. `orientation` defaults to `'horizontal'`; it is the bar's composite `orientation` (CMP-API-02), the parent orientation used by each bar menu's trigger and popup key handling (MBR-KEY-04, MBR-KEY-07), the `aria-orientation`/`data-orientation` value (MBR-ARIA-02, MBR-DOM-01), and selects the default side of the bar's menus (MBR-POS-01).
  Source: `menubar/Menubar.tsx:35,52,67,85-86`; `menu/root/MenuRoot.tsx:522`; `menu/positioner/MenuPositioner.tsx:98-101`. Test: [L2] `Menubar.test.tsx` "sets aria-orientation on the root element", "moves between triggers with the main-axis arrow keys when vertical" (L4 for the arrow-key clause).
- **MBR-API-02** — MUST. `loopFocus` defaults to `true` and is the bar's composite `loopFocus` (CMP-API-01, CMP-KEY-06/07/09): with `true` the forward key on the last trigger moves to the first and the backward key on the first moves to the last; with `false` focus stays on the end trigger. It does not affect navigation inside the menus (each Menu.Root has its own `loopFocus`, MNU-API-06).
  Source: `menubar/Menubar.tsx:36,87`. Test: [L4] `Menubar.test.tsx` "prop: loopFocus" (4 tests).
- **MBR-API-03** — MUST. `modal` defaults to `true`; it controls the internal backdrop of the bar's menus (MBR-DOM-08) and page scroll lock (MBR-DIS-05), and is reflected as `data-modal` (MBR-DOM-02). It never installs a focus trap (MBR-FOC-06).
  Source: `menubar/Menubar.tsx:39,53,65`; `menu/positioner/MenuPositioner.tsx:245-268`. Test: [L2] `Menubar.test.tsx` "scroll locking" (3 tests) (L4 for the scroll-lock and focus-trap clauses).
- **MBR-API-04** — MUST. `disabled` defaults to `false`; when `true` every bar menu is effectively disabled (MBR-API-08). The Menubar element itself carries no disabled attribute.
  Source: `menubar/Menubar.tsx:40,66`. Test: [L2] `Menubar.test.tsx` "disables child menus when menubar is disabled", "disables menu items while the menubar is disabled".
- **MBR-API-05** — MUST. The element id is the `id` prop or a generated id (RND-DOM-27/28); it is rendered on the element (MBR-DOM-04) and is the root owner id of every menu in the bar (MBR-STA-06).
  Source: `menubar/Menubar.tsx:41,49,69,85`. Test: none.
- **MBR-API-06** — MUST. Attributes not consumed are forwarded to the element and are merged after the part's own `role` and `aria-orientation`, so a consumer-supplied `role` or `aria-orientation` replaces the part's value (RND precedence).
  Source: `menubar/Menubar.tsx:43,85`. Test: [L2] `Menubar.test.tsx` describeConformance (prop forwarding).

#### Menu parameters whose meaning changes under a Menubar

| Part / prop | Under a Menubar | Req |
|---|---|---|
| `Menu.Root` `modal` | Ignored (with a development warning). | MBR-API-07 |
| `Menu.Root` `disabled`, `Menu.Trigger` `disabled` | OR-ed with `Menubar` `disabled`. | MBR-API-08 |
| `Menu.Trigger` `openOnHover` | Default resolves to "a bar menu is open"; hover-open is only possible while a bar menu is open. | MBR-API-09 |
| `Menu.Trigger` `delay` | Ignored (hover-open is immediate). | MBR-API-10 |
| `Menu.Trigger` `closeDelay` | No observable effect (bar menus never hover-close). | MBR-PTR-05 |
| `Menu.Root` `orientation` | Still governs the popup's own list; independent of the bar's orientation. | MBR-API-11 |
| `Menu.Positioner` `side` / `align` | Defaults `bottom`/`start` (horizontal bar) or `inline-end`/`start` (vertical bar). | MBR-POS-01 |

- **MBR-API-07** — MUST. A `Menu.Root` with parent kind `'menubar'` ignores its own `modal` prop (its effective menu modality is `false`, MNU-STA-06) and, in development, logs the nested-menu warning of MNU-EDGE-02 when the prop is supplied explicitly.
  Source: `menu/root/MenuRoot.tsx:166,207-213,220`; `menu/store/MenuStore.ts:82-84`. Test: none in `menubar/` (`MenuRoot.test.tsx` "warns that nested menus ignore the modal prop" covers the message).
- **MBR-API-08** — MUST. Under a Menubar, a menu's effective disabled state is `Menu.Root disabled || Menubar disabled` (MNU-STA-07), and each trigger's is `Menu.Trigger disabled || Menu.Root disabled || Menubar disabled` (MNU-API-16). A disabled bar menu cannot be opened by press, keyboard, hover or focus, and its items are disabled (`aria-disabled="true"`, clicks do not run the item's `onClick`), even while it is held open by a consumer-owned `open`.
  Source: `menu/store/MenuStore.ts:78-81`; `menu/trigger/MenuTrigger.tsx:114`. Docs: `CHANGELOG.md v1.0.0-beta.4 "Fix Menubar not disabling child Menus"`. Test: [L2] `Menubar.test.tsx` "disables child menus when menubar is disabled", "disables menu items while the menubar is disabled" (L4 for the press, keyboard, hover and focus opening clause).
- **MBR-API-09** — MUST. A bar trigger's `openOnHover` defaults to `undefined`, which resolves to the bar's `hasSubmenuOpen` (MBR-STA-02). Hover-opening is enabled only when the resolved value is `true`, the trigger is not disabled, `hasSubmenuOpen` is `true`, and the trigger is not the trigger that mounted its menu's current popup. Consequently an explicit `openOnHover={true}` does not make a bar trigger open on hover while no bar menu is open, and `openOnHover={false}` excludes that trigger from hover switching.
  Source: `menu/trigger/MenuTrigger.tsx:173-180`. Test: [L4] `Menubar.test.tsx` "should not open submenus on hover when no submenu is already open", "should open submenus on hover when another submenu is already open".
- **MBR-API-10** — MUST. A bar trigger's `delay` prop has no effect: its hover interaction is configured with no rest time and no open delay, so the menu opens on pointer entry (MBR-PTR-04).
  Source: `menu/trigger/MenuTrigger.tsx:184-185`; FLT-PTR-12. Test: [L4] `Menubar.test.tsx` "should open submenus on hover when another submenu is already open".
- **MBR-API-11** — MUST. A bar menu's `Menu.Root` `orientation` (default `'vertical'`) keeps governing navigation inside its popup (MNU-API-09) and the popup's `aria-orientation` (MNU-ARIA-04); the bar's orientation only adds the parent-orientation behavior of MBR-KEY-04 … MBR-KEY-08.
  Source: `menu/root/MenuRoot.tsx:516-532`; `menu/popup/MenuPopup.tsx:159-166`. Test: [L4] `Menubar.test.tsx` "should navigate within the menu using arrow keys".

#### State object (`Menubar.State`)

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | The `orientation` prop. |
| `modal` | `boolean` | The `modal` prop. |
| `hasSubmenuOpen` | `boolean` | A top-level menu of the bar is open (MBR-STA-02/03). |

- **MBR-STA-01** — MUST. The state object exposed to `className`/`style`/`render` contains exactly `orientation`, `modal` and `hasSubmenuOpen`.
  Source: `menubar/Menubar.tsx:51-55,130-143`. Test: [L2] `Menubar.test.tsx` describeConformance.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; `horizontal` \| `vertical` | MBR-DOM-01 |
| `data-modal` | `modal === true`; empty | MBR-DOM-02 |
| `data-has-submenu-open` | `hasSubmenuOpen === true`; empty | MBR-DOM-03 |

#### CSS variables

None.

### 3.2 Shared types

- `Menubar.Props`, `Menubar.State` (§3.1).
- `Orientation` = `'horizontal' | 'vertical'` (the Menu type `MenuRoot.Orientation`).
- `MenubarDataAttributes`: exported namespace of constants `modal = 'data-modal'`, `orientation = 'data-orientation'`, `hasSubmenuOpen = 'data-has-submenu-open'` (`menubar/MenubarDataAttributes.ts:1-13`, `menubar/index.ts:5`).
- The Menubar has no callbacks; the change-event details and reasons are those of `Menu.Root.ChangeEventDetails` (MNU §3.28, EVD-API-01).

## 4. State model

The Menubar owns two pieces of state: the bar-level flag `hasSubmenuOpen` and the composite highlighted index (which trigger is the Tab stop). Open state lives in each Menu.Root (MNU §4); the bar coordinates the menus through its floating tree's `menuopenchange` notifications (emitted by each menu's Positioner, MNU §4.1/§9).

- **MBR-STA-02** — MUST. `hasSubmenuOpen` starts `false` and becomes `true` when a menu whose parent node is the Menubar node reports that it is open.
  Source: `menubar/Menubar.tsx:47,102-111`; `menu/positioner/MenuPositioner.tsx:218-227`. Test: [L2] `Menubar.test.tsx` "should open submenus on hover when another submenu is already open" (`data-has-submenu-open` after the click) (L4 for the click-open citation).
- **MBR-STA-03** — MUST. `hasSubmenuOpen` becomes `false` when such a menu reports that it closed with a reason other than `sibling-open` or `list-navigation` (including `trigger-press`, `escape-key`, `outside-press`, `item-press`, `focus-out`, `trigger-focus`, `cancel-open`, `imperative-action`, `none` and an absent reason). A close with `sibling-open` (another bar menu took over) or `list-navigation` (the user arrowed out of the menu) leaves it `true`.
  Source: `menubar/Menubar.tsx:112-117`. Test: [L2] `Menubar.test.tsx` "correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)" (click close clears the attribute) (L4 for the click and hover citations).
- **MBR-STA-04** — MUST. The open/close reports are produced by each menu's `Menu.Positioner` after paint, both when the Positioner mounts and whenever the menu's open state changes, carrying the menu's last open-change reason; `hasSubmenuOpen` is updated in response (so it lags the menu's own open state by at least one paint). Reports from submenus (whose parent node is a menu, not the bar) and from menus without a mounted Positioner do not affect it.
  Source: `menu/positioner/MenuPositioner.tsx:218-227`; `menubar/Menubar.tsx:102-125`. Test: [L2] `Menubar.test.tsx` "should open nested submenus on hover when parent menu is open" (waits for the attribute after opening) (L4 for the hover citation).
- **MBR-STA-05** — MUST. The bar's composite highlighted index (the Tab stop) is owned by the Menubar (not consumer-controllable): initially `0`, moved on first publication to the first trigger that is not disabled (CMP-STA-24), kept on its element when triggers are added/removed (CMP-STA-26/27), and updated whenever a trigger receives focus (CMP-PTR-02) or the bar handles an arrow/Home/End key (CMP-KEY-10). Server: relaxed, see §9.
  Source: `menubar/Menubar.tsx:78-90`; `internals/composite/root/useCompositeRoot.ts:90-193`. Test: [L4] `Menubar.test.tsx` "keeps the menubar reachable when the first trigger is disabled".
- **MBR-STA-06** — MUST. The root id of every menu in the bar (top-level menus and all their submenus) is the Menubar's id (MBR-API-05), not the Menu.Root's own generated id.
  Source: `menu/store/MenuStore.ts:91-97`; `menubar/Menubar.tsx:69`. Test: none.
- **MBR-STA-07** — MUST. All menus of one bar share a single drag-release arming flag (MNU-PTR-02/03): a press on any bar trigger arms it after 200 ms and any document `mouseup` handled by the arming trigger disarms it; unlike a standalone menu, the flag is not reset by a trigger whose menu is not open.
  Source: `menu/store/MenuStore.ts:175-181`; `menubar/Menubar.tsx:57,68`; `menu/trigger/MenuTrigger.tsx:121-125,130-136,248-251`. Test: none.

State diagram of the bar flag (each arrow is a report from a top-level bar menu):

```
hasSubmenuOpen=false ──(menu reports open)──► true
true ──(menu reports closed, reason ∉ {sibling-open, list-navigation})──► false
true ──(menu reports closed, reason ∈ {sibling-open, list-navigation})──► true (unchanged)
```

Invariant: at most one top-level bar menu is open once the sibling close of MBR-DIS-03 has committed (a consumer-owned `open` may veto it).

## 5. Rendered DOM and ARIA

### 5.1 `Menubar` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `menubar` | MBR-ARIA-01 | C# |
| `id` | resolved id | MBR-DOM-04 | C# |
| `aria-orientation` | `orientation` | MBR-ARIA-02 | C# |
| `data-orientation`, `data-modal`, `data-has-submenu-open` | §3.1 | MBR-DOM-01..03 | C# |

- **MBR-DOM-01** — MUST. The element carries `data-orientation="horizontal"` or `data-orientation="vertical"` (generic projection of the `orientation` field, RND-DOM-03).
  Source: `menubar/Menubar.tsx:51-55,82-83`; `menubar/MenubarDataAttributes.ts:5-9`. Test: none.
- **MBR-DOM-02** — MUST. The element carries `data-modal=""` iff `modal` is `true` (RND-DOM-01/02).
  Source: `menubar/Menubar.tsx:51-55`; `menubar/MenubarDataAttributes.ts:1-4`. Test: none.
- **MBR-DOM-03** — MUST. The element carries `data-has-submenu-open=""` iff `hasSubmenuOpen` is `true`; no attribute otherwise.
  Source: `menubar/Menubar.tsx:19-23`. Test: [L2] `Menubar.test.tsx` "should open submenus on hover when another submenu is already open", "correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)" (L4 for the hover and click citations).
- **MBR-DOM-04** — MUST. The element renders the resolved id (MBR-API-05) as its `id`.
  Source: `menubar/Menubar.tsx:85`. Test: none.

### 5.2 Trigger elements inside the bar

- **MBR-DOM-05** — MUST. Each bar trigger renders the composite roving `tabindex`: `tabindex="0"` on the trigger at the bar's highlighted index and `tabindex="-1"` on every other trigger (CMP-DOM-01); the button mechanism emits no `tabindex` of its own (UBT-DOM-05).
  Source: `menu/trigger/MenuTrigger.tsx:272-285`; `internals/composite/item/useCompositeItem.ts:19,25`. Test: [L2] `Menubar.test.tsx` "keeps the menubar reachable when the first trigger is disabled" (L4 for the composite highlight move it projects).
- **MBR-DOM-06** — MUST. Bar triggers use the button mechanism in composite mode: a disabled native trigger renders both `disabled` and `aria-disabled="true"` (UBT-ARIA-05) and is therefore not focusable; an enabled native trigger renders `aria-disabled="false"` (UBT-ARIA-01).
  Source: `menu/trigger/MenuTrigger.tsx:116-119`; `internals/use-button/useButton.ts` (UBT-STA-01). Test: [L2] `Menubar.test.tsx` "disables child menus when menubar is disabled", "keeps the menubar reachable when the first trigger is disabled" (`disabled` attribute).
- **MBR-DOM-07** — MUST. No focus guard elements are rendered around a bar trigger, whatever its open state (contrast MNU-STR-16); the trigger element is rendered directly as the composite item.
  Source: `menu/trigger/MenuTrigger.tsx:272-285`; PSH-FOC-07; see MNU-FOC-06 (bar-trigger clause). Test: none (`MenuRoot.test.tsx` "focus guards" covers the non-bar case).
- **MBR-DOM-08** — MUST. While a top-level bar menu is mounted and the Menubar's `modal` is `true`, the internal backdrop (PSH-DOM-14) is rendered before that menu's Positioner, with the Menubar element as its cutout (so every bar trigger stays pointer-interactive), regardless of the open reason (hover-opened bar menus also get it) and regardless of the Menu.Root's `modal`; it is `inert` while the menu is not open. With Menubar `modal` `false` no internal backdrop is rendered. Submenus never render one.
  Source: `menu/positioner/MenuPositioner.tsx:264-290`. Test: none directly (the cutout is exercised by `Menubar.test.tsx` "should open submenus on hover when another submenu is already open" in a real browser).
- **MBR-DOM-09** — MUST. Every popup of the bar (top-level menus and submenus) renders `data-rootownerid` equal to the Menubar's id (MBR-STA-06).
  Source: `menu/popup/MenuPopup.tsx:62,178`; `menu/store/MenuStore.ts:91-97`. Test: none.

### 5.3 ARIA

- **MBR-ARIA-01** — MUST. The Menubar element renders `role="menubar"`.
  Source: `menubar/Menubar.tsx:85`. Test: [L2] `Menubar.test.tsx` "sets role="menubar" on the root element".
- **MBR-ARIA-02** — MUST. The Menubar element renders `aria-orientation` equal to `orientation` for both values (`"horizontal"` is rendered explicitly, unlike a Menu popup, MNU-ARIA-04).
  Source: `menubar/Menubar.tsx:85`. Test: [L2] `Menubar.test.tsx` "sets aria-orientation on the root element".
- **MBR-ARIA-03** — MUST. Every bar trigger renders `role="menuitem"` (MNU-ARIA-17) together with the Menu trigger ARIA of MNU-ARIA-01 … MNU-ARIA-03 (`aria-haspopup="menu"`, or `"dialog"` for a filterable menu; `aria-expanded`; `aria-controls` while it owns the open popup).
  Source: `menu/trigger/MenuTrigger.tsx:239-258`; `menu/root/MenuRoot.tsx:654-665`. Docs: `CHANGELOG.md v1.0.0-beta.2 "Fix triggers role"`. Test: [L2] `Menubar.test.tsx` "sets role="menuitem" on menu triggers".
- **MBR-ARIA-04** — MUST. When a bar menu's `Menu.Root` (and so its `Menu.Portal`) is rendered inside the Menubar, the hidden `aria-owns` owner `<span>` of its portal is rendered inside the Menubar element with `role="group"`, no `aria-hidden`, and `aria-owns` = the portal node id, so the owned popup is a permitted child of the menubar. When the Root is rendered outside the bar (detached triggers), the owner span is outside the Menubar element and has no role.
  Source: `menu/portal/MenuPortal.tsx:29-37`; `floating-ui-react/components/FloatingPortal.tsx:268`. Docs: `CHANGELOG.md v1.8.0 "Fix menubar accessibility tree to satisfy aria-required-children"`. Test: [L2] `Menubar.test.tsx` "renders contained top-level portal ownership as an allowed menubar child", "renders detached top-level portal ownership without an accessibility role".
- **MBR-ARIA-05** — MUST. The popup of a bar menu keeps `role="menu"` and its own `aria-orientation`/`aria-labelledby` rules (MNU-ARIA-04, MNU-ARIA-19); the Menubar adds no ARIA attributes to popups or items.
  Source: `menu/popup/MenuPopup.tsx:159-167`. Test: [L2] `Menubar.test.tsx` "applies scroll lock when a touch-opened submenu covers the viewport width" (`findByRole('menu')`) (L4 for the touch-open citation).

## 6. Interactions

Vocabulary (CMP §4.6 predicates, with *b* = the Menubar `orientation`, *m* = the bar menu's own `orientation`, direction from the nearest DirectionProvider):
*bar main keys* = MAIN(*b*): ArrowLeft/ArrowRight (horizontal) or ArrowUp/ArrowDown (vertical); *bar forward/backward* per CMP-KEY-03 (horizontal forward = ArrowRight in LTR, ArrowLeft in RTL);
*bar open key* = XOPEN(*b*): ArrowDown (horizontal bar, both directions) or ArrowRight in LTR / ArrowLeft in RTL (vertical bar);
*menu close key* = XCLOSE(*m*): ArrowLeft in LTR / ArrowRight in RTL for a vertical menu, ArrowUp for a horizontal menu.

### 6.1 Keyboard

| Key | Context | Behavior | Req | Delegated |
|---|---|---|---|---|
| bar main keys | focus on a trigger | Focus moves to the next/previous enabled trigger (wraps per `loopFocus`); `preventDefault` + `stopPropagation` when focus moves | MBR-KEY-01 | CMP-KEY-03..11 |
| Home / End | focus on a trigger | First / last enabled trigger | MBR-KEY-02 | CMP-KEY-05 |
| arrows, Home, End with Shift/Ctrl/Alt/Meta | focus on a trigger | Not handled by the bar | MBR-KEY-03 | CMP-KEY-02 |
| bar open key | trigger, its menu closed | Opens (`list-navigation`), first item highlighted and focused | MBR-KEY-04 | CMP-KEY-23 |
| bar open key | trigger, its menu open | Highlights and focuses the first item (no reopen) | MBR-KEY-04 | CMP-KEY-23 |
| non-main, non-open arrow (e.g. ArrowUp in a horizontal bar) | trigger | Nothing | MBR-KEY-05 | — |
| Enter, Space | trigger | Toggles its menu (`trigger-press`, keyboard click); on open, first item highlighted and focused | MBR-KEY-06 | MNU-KEY-03, UBT-KEY-09/12 |
| menu close key | inside an open bar menu (not in a submenu) | Closes the menu (`list-navigation`), focuses its trigger; if the key is also a bar main key, the bar then moves to the adjacent trigger | MBR-KEY-07 | CMP-KEY-27 |
| bar main key not used by the menu | inside an open bar menu or its submenu, on a non-submenu-trigger item | Relayed to the bar: focus moves to the adjacent trigger, whose menu opens; the previous menu tree closes | MBR-KEY-08 | CMP-KEY-14 |
| submenu open key | submenu trigger inside a bar menu | Opens the submenu (does not move the bar) | MBR-KEY-09 | MNU-KEY-15 |
| Escape | anywhere while a bar menu is open | Closes the innermost menu (`escape-key`), focus to its trigger; bar leaves menu mode when a top-level menu closes | MBR-KEY-10 | MNU-KEY-09, MNU-KEY-19 |
| Tab / Shift+Tab | trigger | Not intercepted; leaves the bar (single Tab stop) | MBR-KEY-11 | CMP-FOC-01 |
| Tab | inside an open bar menu | Not intercepted by the bar or the menu; no trigger focus guards | MBR-KEY-11 | — |
| Shift+Tab | inside an open bar menu | Closes (`focus-out`), focus to the trigger | MBR-KEY-12 | MNU-KEY-11 |
| main keys of *m*, Home/End, letters | inside an open bar menu | Menu list navigation / typeahead, unchanged | MBR-STR-05 | MNU-KEY-04..13 |
| printable characters | trigger | No bar-level typeahead across triggers | MBR-KEY-15 | — |

- **MBR-KEY-01** — MUST. A bar main key pressed while focus is on a bar trigger (no menu of that trigger intercepting it) moves the bar's highlighted index to the next (forward key) or previous (backward key) trigger, skipping triggers carrying `disabled` or `aria-disabled="true"`, wrapping only when `loopFocus` is `true`, and focuses that trigger one microtask later (CMP-KEY-03 … CMP-KEY-11, CMP-FOC-02). When focus moves the keydown is default-prevented and its propagation stopped; at a non-looping end nothing happens. Moving focus does not open a menu while no bar menu is open (MBR-FOC-02); it does while one is open (MBR-FOC-03).
  Source: `menubar/Menubar.tsx:78-89`; `internals/composite/root/useCompositeRoot.ts:206-317`. Test: [L4] `Menubar.test.tsx` "should navigate between menubar items with arrow keys", "moves between triggers with the main-axis arrow keys when vertical", "prop: loopFocus" (4 tests).
- **MBR-KEY-02** — MUST. Home and End on a bar trigger move focus to the first and last enabled trigger (the bar enables Home/End keys, CMP-KEY-01/05), regardless of orientation and direction.
  Source: `menubar/Menubar.tsx:88`. Test: [L4] `Menubar.test.tsx` "moves focus to the first and last triggers with Home and End".
- **MBR-KEY-03** — MUST. The bar passes no allowed modifier keys and no disabled indices to its composite root, so arrow/Home/End keys with Shift, Control, Alt or Meta held are ignored by the bar (CMP-KEY-02) and disabled triggers are detected from their attributes (CMP-ALG-02).
  Source: `menubar/Menubar.tsx:78-90`. Test: none.
- **MBR-KEY-04** — MUST. The bar open key on a focused, enabled bar trigger is consumed (default prevented, propagation stopped). When the trigger's menu is closed it opens with reason `list-navigation` (event = the keydown, trigger = the trigger element) and, once items are rendered, the first navigable item is highlighted and focused (a nested list always starts at its first item, CMP-STA-37). When the menu is already open the first enabled item is highlighted and focused without reopening. In a filterable menu the input receives focus and the first item is highlighted (MBR-KEY-14).
  Source: `menu/root/MenuRoot.tsx:521-522`; `floating-ui-react/hooks/useListNavigation.ts:77-85,847-893,465-470`. Docs: `CHANGELOG.md v1.6.0 "Fix vertical menu focus behavior"`. Test: [L4] `Menubar.test.tsx` "opens a menu of a vertical $direction menubar with $openKey" (2 cases); "filterable menubar menus" "preserves %s trigger navigation" (2 cases).
- **MBR-KEY-05** — MUST. Arrow keys on a bar trigger that are neither a bar main key nor the bar open key (ArrowUp in a horizontal bar; ArrowLeft in LTR / ArrowRight in RTL in a vertical bar) neither open the menu nor move the bar.
  Source: `floating-ui-react/hooks/useListNavigation.ts:877-893`; `internals/composite/root/useCompositeRoot.ts:267-302`. Test: [L4] `Menubar.test.tsx` "opens a menu of a vertical $direction menubar with $openKey" (the opposite key leaves `menu` absent).
- **MBR-KEY-06** — MUST. Enter and Space on a focused bar trigger activate it as a click (Enter: native button activation; Space: composite-mode keydown click, UBT-KEY-12/15, one click per press) and toggle its menu: closed → open with reason `trigger-press` and a `detail === 0` click (keyboard open: first item highlighted and focused, `data-instant="click"`, MNU-KEY-03); open (opened by this trigger) → close with reason `trigger-press`. There is no 500 ms keep-open after a hover open inside a bar.
  Source: `menu/trigger/MenuTrigger.tsx:197-203`; `floating-ui-react/hooks/useClick.ts:187-219`. Test: [L4] `Menubar.test.tsx` "should open the menu with Space key", "should navigate within the menu using arrow keys" (opens with Enter, first item focused), "focuses a menubar item without immediately opening the menu" (Enter after Tab).
- **MBR-KEY-07** — MUST. Inside an open top-level bar menu, the menu close key closes that menu with reason `list-navigation` (event = the keydown) and focuses its trigger (CMP-KEY-27). The keydown's propagation is stopped unless the key is also a bar main key; in that case (e.g. ArrowLeft in an LTR horizontal bar with a vertical menu) the key additionally reaches the bar, which moves focus to the previous trigger and, the bar still being in menu mode (MBR-STA-03), opens that trigger's menu (MBR-FOC-03). In a vertical bar the close key (ArrowLeft in LTR) is not a bar main key, so the menu closes and focus stays on its trigger.
  Source: `floating-ui-react/hooks/useListNavigation.ts:570-585`; `menu/root/MenuRoot.tsx:521-522,692-700`. Test: none directly (the submenu variant: `Menubar.test.tsx` "should close submenu with left arrow key and return focus to submenu trigger").
- **MBR-KEY-08** — MUST. A keydown inside the popup of a bar menu (or of any of its submenus) that the menu's own handlers did not stop is relayed to the bar's key handling (MNU-KEY-21, CMP-KEY-14). Hence a bar main key that the menu does not use — e.g. ArrowRight (LTR) on a plain item of a vertical menu in a horizontal bar, including from inside an open submenu — moves the bar to the adjacent trigger, which opens its menu with reason `trigger-focus`; the previously open bar menu closes (MBR-DIS-03) together with all its open submenus (MNU-DIS-04). The relay runs whether the Root is inside the bar or detached.
  Source: `menu/root/MenuRoot.tsx:692-700`; `menu/trigger/MenuTrigger.tsx:106`; `menu/store/MenuStore.ts:138-148`; `internals/composite/root/useCompositeRoot.ts:338`. Test: [L4] `Menubar.test.tsx` "should navigate between menus using left/right arrow keys when menus are open", "should allow clicking a menu trigger then navigating to another menu with keyboard", "closes open submenus when navigating to the next menubar item with ArrowRight".
- **MBR-KEY-09** — MUST. The submenu open key on a highlighted `Menu.SubmenuTrigger` inside a bar menu opens the submenu and is consumed (MNU-KEY-15), so it never moves the bar; the submenu close key inside the submenu closes only the submenu and returns focus to its submenu trigger (MNU-KEY-16), without moving the bar.
  Source: `floating-ui-react/hooks/useListNavigation.ts:570-585,877-893`. Test: [L4] `Menubar.test.tsx` "should open the submenu with right arrow key", "should close submenu with left arrow key and return focus to submenu trigger".
- **MBR-KEY-10** — MUST. Escape closes the innermost open menu with reason `escape-key` and returns focus to its trigger (MNU-KEY-09, MNU-KEY-19, MNU-FOC-04); closing a top-level bar menu this way clears `hasSubmenuOpen` (MBR-STA-03), and the focus returning to the trigger does not reopen it (the `escape-key` close blocks the next focus-open on that trigger, FLT-FOC-03). Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:501-512`; `menubar/Menubar.tsx:112-117`; `floating-ui-react/hooks/useFocus.ts:100-108`. Test: [L4] `Menubar.test.tsx` "should close the menu with Escape key".
- **MBR-KEY-11** — MUST. Tab and Shift+Tab are not intercepted by the bar: from a trigger they leave the bar, which is a single Tab stop (CMP-FOC-01). Inside an open bar menu, Tab is not intercepted by the menu either, and no trigger focus guards exist (MBR-DOM-07); where focus lands is decided by the portal focus guards of the non-modal popup (FLT-DOM-14).
  Source: `menu/trigger/MenuTrigger.tsx:272-285`; `menu/popup/MenuPopup.tsx:203-212`. Test: [L4] `Menubar.test.tsx` "focuses a menubar item without immediately opening the menu" (Tab into the bar), "disables child menus when menubar is disabled" (Tab skips disabled triggers).
- **MBR-KEY-12** — MUST. Shift+Tab with focus inside an open bar menu closes it with reason `focus-out` and moves focus to its trigger (MNU-KEY-11); this close clears `hasSubmenuOpen`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`; `menubar/Menubar.tsx:112-117`. Test: none in `menubar/`.
- **MBR-KEY-13** — MUST. A key that the bar handles from a relayed popup keydown has its propagation stopped by the bar (the bar's composite stops propagation for handled keys, CMP-KEY-10/15), so the same keydown, continuing to bubble through the component hierarchy towards the bar, is not handled a second time.
  Source: `internals/composite/root/CompositeRoot.tsx:34`; `internals/composite/root/useCompositeRoot.ts:302-305`; `menu/root/MenuRoot.tsx:696-699`. Test: [L4] `Menubar.test.tsx` "should navigate between menus using left/right arrow keys when menus are open" (moves exactly one trigger).
- **MBR-KEY-14** — MUST. For a filterable bar menu (`Menu.FilterProvider`), the trigger-to-input key relay of MNU-KEY-28 is disabled; the bar main keys move between triggers without opening the menu, and the bar open key opens it, focuses `Menu.Input` and highlights the first item.
  Source: `menu/filter-root/MenuFilterDropdown.tsx:121-153`; `menu/root/MenuRoot.tsx:516-532`. Test: [L4] `Menubar.test.tsx` "filterable menubar menus" "preserves %s trigger navigation" (`horizontal`, `vertical`).
- **MBR-KEY-15** — MUST. The bar has no typeahead of its own: printable characters on a trigger whose menu is closed do nothing; on the active trigger of an open menu they run that menu's typeahead (MNU-KEY-12).
  Source: `menubar/Menubar.tsx:78-90` (no typeahead configured); `menu/root/MenuRoot.tsx:555-568`. Test: none.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| primary `mousedown` | closed trigger | Opens (`trigger-press`); the click of the same press is ignored | MBR-PTR-01 |
| click | trigger whose menu it opened | Closes (`trigger-press`), even right after a hover open | MBR-PTR-02 |
| mouse enter | trigger, no bar menu open | Nothing | MBR-PTR-03 |
| mouse enter | another trigger while a bar menu is open | Opens its menu immediately (`trigger-hover`); previous menu closes (`sibling-open`) | MBR-PTR-04 |
| pointer leaves trigger / popup | hover-opened bar menu | Nothing (no hover close) | MBR-PTR-05 |
| `mousemove` | enabled trigger while a bar menu is open | Focuses it (bar highlight) | MBR-PTR-06 |
| hover / press | submenu trigger in a bar menu | As Menu (MNU-PTR-09, MNU-PTR-17) | MBR-PTR-07 |
| touch tap | trigger | Opens / closes; no hover switching | MBR-PTR-08 |
| any press | Menubar area while a modal bar menu is open | Not blocked by the backdrop | MBR-PTR-09 |

- **MBR-PTR-01** — MUST. A primary-button `mousedown` on an enabled bar trigger whose menu is closed opens the menu with reason `trigger-press` (pointer opens are applied on the next frame, FLT-PTR-02), arms the drag-release behavior (MNU-PTR-02 … MNU-PTR-04, MBR-STA-07), and arms a one-shot suppression so that the `click` ending the same press does not close the menu again (PSH-PTR-04 with `mouseDownAction: 'open'`, enabled only inside a bar).
  Source: `menu/trigger/MenuTrigger.tsx:197-213,243-255`; `utils/useMixedToggleClickHandler.ts:12-43`. Test: [L4] `Menubar.test.tsx` "should open the menu after clicking on its trigger and close it when clicking again".
- **MBR-PTR-02** — MUST. While a bar trigger is the trigger of its open menu, its press interaction switches from `mousedown` to `click`: a `mousedown` alone does nothing and the following `click` closes the menu with reason `trigger-press` (toggle). The keep-open-after-hover rule (`stickIfOpen`, MNU-PTR-06) is disabled inside a bar, so the first click on a hover-opened bar menu's trigger closes it.
  Source: `menu/trigger/MenuTrigger.tsx:197-203`; FLT-PTR-03, FLT-PTR-06. Test: [L4] `Menubar.test.tsx` "should open the menu after clicking on its trigger and close it when clicking again", "correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)".
- **MBR-PTR-03** — MUST. While no bar menu is open (`hasSubmenuOpen` `false`), moving the pointer onto or over a bar trigger opens nothing and does not move the bar's highlight.
  Source: `menu/trigger/MenuTrigger.tsx:173-180`; `menubar/Menubar.tsx:89`. Test: [L4] `Menubar.test.tsx` "should not open submenus on hover when no submenu is already open".
- **MBR-PTR-04** — MUST. While a bar menu is open (`hasSubmenuOpen` `true`), a mouse pointer entering another enabled bar trigger (one whose hover is enabled per MBR-API-09) opens that trigger's menu immediately on `mouseenter` — no rest time and no delay — with reason `trigger-hover` and that trigger as `trigger`; the previously open bar menu then closes with reason `sibling-open` (MBR-DIS-03). With several triggers on one Root (contained or detached), entering an inactive trigger switches the open popup to it (FLT-PTR-12). Only mouse-like pointers hover-open (`mouseOnly`, FLT-PTR-11); touch and pen do not. The switch can be repeated across any number of triggers.
  Source: `menu/trigger/MenuTrigger.tsx:173-190`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:216-300`; `menu/positioner/MenuPositioner.tsx:134-154`. Test: [L4] `Menubar.test.tsx` "should open submenus on hover when another submenu is already open", "correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)".
- **MBR-PTR-05** — MUST. A bar menu never closes because the pointer leaves its trigger or its popup, whether it was opened by hover or otherwise: the trigger's hover interaction is disabled for the trigger that mounted the popup, and the popup-side hover interaction is disabled for bar menus. Consequently a bar trigger's `closeDelay` has no observable effect, and the safe-polygon strategy never blocks pointer events on the page (`blockPointerEvents: false`).
  Source: `menu/trigger/MenuTrigger.tsx:176-185`; `menu/popup/MenuPopup.tsx:123-126`. Test: [L4] `Menubar.test.tsx` "should open nested submenus on hover when parent menu is open" (the hover-opened file menu stays open while the pointer moves into it).
- **MBR-PTR-06** — MUST. While `hasSubmenuOpen` is `true`, the bar's composite highlights on hover: a `mousemove` over an enabled bar trigger that is not the bar's highlighted trigger focuses it (CMP-PTR-01), which makes it the Tab stop; while `false`, pointer movement has no effect on the bar's highlight.
  Source: `menubar/Menubar.tsx:89`; `internals/composite/item/useCompositeItem.ts:29-39`. Test: none.
- **MBR-PTR-07** — MUST. Submenus inside bar menus behave as in Menu: a submenu trigger opens on hover after its `delay` (MNU-PTR-09), clicking items honors their `closeOnClick` (radio and checkbox items keep their menus open by default, MNU-API-34), whether the bar menu was opened by click or by hover switching; hovering another bar trigger while a submenu is open closes the whole open branch (MNU-DIS-04) and opens the new menu.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:176-204`; `menu/positioner/MenuPositioner.tsx:156-175`. Docs: `CHANGELOG.md v1.0.0-beta.1 "Fix closeOnClick: false not working in nested menus"`. Test: [L4] `Menubar.test.tsx` "should open nested submenus on hover when parent menu is open", "should open another menu on hover when a nested submenu is open", "should respect closeOnClick on nested items when the menu was opened on click", "should respect closeOnClick on nested items when the menu was opened on hover".
- **MBR-PTR-08** — MUST. Touch taps on bar triggers open and close menus as press interactions (no hover switching); a single outside touch press closes the open bar menu together with its open submenu (MNU-DIS-01, MNU-DIS-04); a touch `click` that would close a bar menu within 300 ms of that menu being opened by focus (`trigger-focus`) is ignored, except an item activation (MNU-STA-05).
  Source: `menu/root/MenuRoot.tsx:380-402`; `menu/trigger/MenuTrigger.tsx:176-206`. Docs: `CHANGELOG.md v1.1.0 "Fix submenu outside-press dismiss on touch"`. Test: [L4] `Menubar.test.tsx` "closes the entire tree on a single outside press after opening a submenu", "ignores a delayed touch click immediately after focus opens another menu", "closes on a touch item click immediately after focus opens another menu".
- **MBR-PTR-09** — MUST. While a modal bar menu is open, presses and hovers over any part of the Menubar element reach the bar (the backdrop's cutout is the Menubar element, MBR-DOM-08, PSH-PTR-05), so other triggers can be pressed or hovered; presses elsewhere hit the backdrop and count as outside presses.
  Source: `menu/positioner/MenuPositioner.tsx:270-289`; `utils/InternalBackdrop.tsx`. Test: none directly (exercised by every hover-switch test in a real browser).

### 6.3 Focus management

- **MBR-FOC-01** — MUST. The bar is one Tab stop: Tab into the bar focuses the trigger at the highlighted index, initially the first trigger that is not disabled; a disabled first trigger (native `disabled`) does not take the Tab stop. With every trigger disabled (e.g. Menubar `disabled`), no trigger is focusable and Tab passes the bar.
  Source: `internals/composite/root/useCompositeRoot.ts:151-161`; `menu/trigger/MenuTrigger.tsx:114-119`. Test: [L4] `Menubar.test.tsx` "keeps the menubar reachable when the first trigger is disabled", "disables child menus when menubar is disabled".
- **MBR-FOC-02** — MUST. Focusing a bar trigger (Tab, arrow keys, programmatic focus) while no bar menu is open does not open its menu.
  Source: `menu/trigger/MenuTrigger.tsx:205-207`. Test: [L4] `Menubar.test.tsx` "focuses a menubar item without immediately opening the menu", "moves between triggers with the main-axis arrow keys when vertical".
- **MBR-FOC-03** — MUST. While `hasSubmenuOpen` is `true`, a bar trigger whose menu is closed opens it when it receives focus, with reason `trigger-focus`, the focus event as `event` and the trigger as `trigger`, synchronously (no delay), subject to the focus-open rules of FLT-FOC-01 … FLT-FOC-03 (`:focus-visible` required; a trigger whose menu was just closed by `trigger-press` or `escape-key` is blocked from reopening on the following focus). A menu opened this way is not a keyboard open (MNU-STA-03).
  Source: `menu/trigger/MenuTrigger.tsx:205-207,215-218`; `floating-ui-react/hooks/useFocus.ts:100-184`. Test: [L4] `Menubar.test.tsx` "should navigate between menus using left/right arrow keys when menus are open", "ignores a delayed touch click immediately after focus opens another menu" (programmatic focus opens `edit-menu`).
- **MBR-FOC-04** — MUST. Initial focus on open follows Menu: keyboard opens (bar open key, Enter/Space) focus the first item (MNU-FOC-01); press, hover and focus opens do not highlight an item (MNU-FOC-02) and, because a bar menu resolves `initialFocus` the same way for every kind of open, move focus from the trigger to the popup element of a plain bar menu (§16 Q3); a later main-axis key of the menu enters the list at its boundary (MNU-KEY-06). A filterable bar menu focuses its input on press and focus opens and keeps focus on the trigger after a hover open (MNU-FOC-02).
  Source: `menu/popup/MenuPopup.tsx:84-96`; `menu/filter-root/MenuFilterPopup.tsx:27-48`; `floating-ui-react/hooks/useListNavigation.ts:440-477`. Test: [L4] `Menubar.test.tsx` "should navigate within the menu using arrow keys", "should allow keyboard navigation after opening a menu with mouse click".
- **MBR-FOC-05** — MUST. When a bar menu closes, focus is returned (to its trigger, per MNU-FOC-04) whenever the menu has an active trigger, and also for a triggerless bar menu unless the close reason is `outside-press`; a triggerless bar menu closed by an outside press leaves focus on the pressed element. `Menu.Popup` `finalFocus` overrides this as in Menu (MNU-API-31).
  Source: `menu/popup/MenuPopup.tsx:182-199`. Test: [L4] `Menubar.test.tsx` "keeps focus on an outside target when a triggerless menubar menu closes", "should close submenu with left arrow key and return focus to submenu trigger".
- **MBR-FOC-06** — MUST. Bar menus always run their focus manager in non-modal mode: no focus trap and no `aria-hidden`/inert marking of outside content, even with Menubar `modal` `true` (MNU-FOC-10, MNU-FOC-13).
  Source: `menu/popup/MenuPopup.tsx:194-199`. Test: none.

### 6.4 Dismissal

- **MBR-DIS-01** — MUST. Outside presses close an open bar menu and its open submenus with reason `outside-press` (MNU-DIS-01); a press on the Menubar element passes through the backdrop (MBR-PTR-09) and is handled by the pressed trigger (a press on another trigger opens that trigger's menu, which then closes the previous one, MBR-DIS-03).
  Source: `menu/root/MenuRoot.tsx:501-512`; `floating-ui-react/hooks/useDismiss.ts:168-190`. Test: [L4] `Menubar.test.tsx` "closes the entire tree on a single outside press after opening a submenu", "keeps focus on an outside target when a triggerless menubar menu closes".
- **MBR-DIS-02** — MUST. Escape dismissal is that of Menu (MNU-DIS-02/03, MBR-KEY-10); the Menubar adds no Escape handling of its own. Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:501-503`. Test: [L4] `Menubar.test.tsx` "should close the menu with Escape key".
- **MBR-DIS-03** — MUST. When a top-level bar menu reports that it opened (MBR-STA-04), every other top-level bar menu that is open requests a close with reason `sibling-open` and no `event` (it shares the Menubar as parent node); a consumer-owned `open` may veto it. Each closed menu's open submenus close with it (MNU-DIS-04).
  Source: `menu/positioner/MenuPositioner.tsx:134-154`. Test: [L4] `Menubar.test.tsx` "should open submenus on hover when another submenu is already open", "closes open submenus when navigating to the next menubar item with ArrowRight".
- **MBR-DIS-04** — MUST. Activating an item with `closeOnClick` closes the bar menu's whole tree with reason `item-press` (MNU-DIS-06) and takes the bar out of menu mode (MBR-STA-03).
  Source: `menu/item/useMenuItem.ts:24-38`; `menubar/Menubar.tsx:112-117`. Test: [L4] `Menubar.test.tsx` "closes on a touch item click immediately after focus opens another menu".
- **MBR-DIS-05** — MUST. While a top-level bar menu is open and the Menubar's `modal` is `true`, page scroll is locked by the anchored scroll lock (PSH-PTR-07) regardless of the open reason (hover-opened bar menus included) and regardless of the Menu.Root's `modal`; for touch opens the lock applies only when the menu's Positioner is at least as wide as the viewport. Switching from a touch-opened menu that locks to one that does not releases the lock once the first closes. With Menubar `modal` `false` no bar menu locks scrolling.
  Source: `menu/positioner/MenuPositioner.tsx:245-253`; `utils/popups/useAnchoredPopupScrollLock.ts`. Test: [L4] `Menubar.test.tsx` "applies scroll lock when a touch-opened submenu covers the viewport width", "does not apply scroll lock when a touch-opened submenu is narrower than the viewport", "updates scroll lock when handing off between top-level touch-opened menus".
- **MBR-DIS-06** — MUST. A bar menu opened by hover or focus is dismissed only by the paths above (press, Escape, outside press, sibling open, item press, focus leaving, `list-navigation`) and never by pointer leave (MBR-PTR-05).
  Source: `menu/popup/MenuPopup.tsx:123-126`; `menu/trigger/MenuTrigger.tsx:176-181`. Test: [L4] `Menubar.test.tsx` "should open another menu on hover when a nested submenu is open".

### 6.5 Reading direction

- **MBR-I18N-01** — MUST. The bar reads its direction from the nearest DirectionProvider (CMP-I18N-01). In a horizontal bar under RTL, ArrowLeft is the forward key and ArrowRight the backward key between triggers (CMP-I18N-02); Home/End, ArrowUp/ArrowDown and the bar open key (ArrowDown) are unaffected.
  Source: `internals/composite/root/CompositeRoot.tsx:43`; `internals/composite/root/useCompositeRoot.ts:221-226`. Test: none in `menubar/` (`CompositeRoot.test.tsx` rtl "horizontal orientation").
- **MBR-I18N-02** — MUST. In a vertical bar, the bar open key is ArrowRight in LTR and ArrowLeft in RTL; the opposite horizontal key does nothing on a trigger.
  Source: `floating-ui-react/hooks/useListNavigation.ts:77-85`; `menu/root/MenuRoot.tsx:533`. Test: [L4] `Menubar.test.tsx` "opens a menu of a vertical $direction menubar with $openKey" (`ltr`/`ArrowRight`, `rtl`/`ArrowLeft`).
- **MBR-I18N-03** — MUST. Inside a vertical bar menu of a horizontal RTL bar, ArrowRight is the menu close key: it closes the menu and, being also a bar main key, reaches the bar, which moves to the previous trigger (backward in RTL); ArrowLeft on a plain item is relayed and moves to the next trigger (MBR-KEY-07, MBR-KEY-08).
  Source: `floating-ui-react/hooks/useListNavigation.ts:87-99,570-585`; `internals/composite/root/useCompositeRoot.ts:221-226`. Test: none.

## 7. Events and callbacks

The Menubar part has no callbacks. Its menus report through `Menu.Root.onOpenChange` / `Menu.SubmenuRoot.onOpenChange` (MNU §7) and `Menu.Root.onItemHighlighted`. The table lists the reasons and the bar interactions that produce them; reasons not listed behave exactly as in MNU §7.1.

| Reason | Open/close | Produced in a bar by | Event | Req |
|---|---|---|---|---|
| `trigger-press` | open | `mousedown` on a closed trigger; Enter/Space click | `MouseEvent` / keyboard click | MBR-PTR-01, MBR-KEY-06 |
| `trigger-press` | close | `click` on the trigger of the open menu; Enter/Space | `MouseEvent` / keyboard click | MBR-PTR-02, MBR-KEY-06 |
| `trigger-hover` | open | mouse entering another trigger while a bar menu is open | `MouseEvent` | MBR-PTR-04 |
| `trigger-focus` | open | a trigger receiving focus while a bar menu is open | `FocusEvent` | MBR-FOC-03 |
| `trigger-focus` | close | focus leaving a focus-enabled trigger to an element outside its menu (FLT-FOC-04) | `FocusEvent` | MBR-EVT-03 |
| `list-navigation` | open | bar open key on a trigger | `KeyboardEvent` | MBR-KEY-04 |
| `list-navigation` | close | menu close key inside the menu | `KeyboardEvent` | MBR-KEY-07 |
| `sibling-open` | close | another top-level bar menu opened | none | MBR-DIS-03 |
| `escape-key`, `outside-press`, `item-press`, `focus-out`, `cancel-open`, `imperative-action`, `none` | close | as MNU §7.1 | as MNU | MBR-DIS-01..04, MBR-KEY-12 |

- **MBR-EVT-01** — MUST. The Menubar exposes no callback props; all open-state changes of bar menus are reported through the Menu callbacks with the details shape of EVD-API-01 and are cancelable as in Menu (MNU-EVT-01/02). A canceled `sibling-open` close leaves two bar menus open.
  Source: `menubar/Menubar.tsx:145-167`; `menu/root/MenuRoot.tsx:341-378`. Test: none.
- **MBR-EVT-02** — MUST. Every reason in the table above is produced only by the listed interaction; in particular `trigger-focus` opens occur only for bar triggers while `hasSubmenuOpen` is `true` (Menu triggers outside a bar never produce it, MNU-FOC-16), and bar triggers never produce `trigger-hover` closes.
  Source: `menu/trigger/MenuTrigger.tsx:173-207`; `menu/popup/MenuPopup.tsx:123-126`. Test: [L4] `Menubar.test.tsx` "should open submenus on hover when another submenu is already open", "ignores a delayed touch click immediately after focus opens another menu".
- **MBR-EVT-03** — MUST. While a bar trigger's focus-open interaction is enabled (`hasSubmenuOpen` `true`), blurring the trigger to an element that is neither inside its menu's popup nor one of that menu's own registered triggers closes the menu with reason `trigger-focus` after 0 ms (FLT-FOC-04).
  Source: `menu/trigger/MenuTrigger.tsx:205-207`; `floating-ui-react/hooks/useFocus.ts:185-233`. Test: none.
- **MBR-EVT-04** — MUST. When the bar moves from an open menu with an open submenu to the next trigger by keyboard (MBR-KEY-08), the final `onOpenChange` call of the submenu is `false`, that of the previous top-level menu is `false`, and that of the newly focused trigger's menu is `true`.
  Source: `menu/positioner/MenuPositioner.tsx:134-175`; `menu/root/MenuRoot.tsx:692-700`. Test: [L4] `Menubar.test.tsx` "closes open submenus when navigating to the next menubar item with ArrowRight".

## 8. Form integration

Not applicable — Menubar and its menus submit no form values.

## 9. Lifecycle, mounting and animation

Mounting, transition status, `keepMounted` and `onOpenChangeComplete` of bar menus are those of Menu (MNU §9, TRN). The bar adds only the instant classification below. "After paint" is used per conventions §4.

- **MBR-ANI-01** — MUST. A top-level bar menu whose open state changes with reason `trigger-focus`, `focus-out`, `trigger-hover`, `list-navigation` or `sibling-open` records instant type `'group'` (MNU-STA-04), so its Positioner and Popup carry `data-instant="group"` and the switch between bar menus is not animated by styles keyed on `[data-instant]`; other reasons classify as in Menu (`'click'` for keyboard clicks, `'dismiss'` for `escape-key`/absent reason, else none). Submenus inside bar menus (parent kind `'menu'`) never get `'group'`.
  Source: `menu/root/MenuRoot.tsx:422-437`. Test: none in `menubar/`.
- **MBR-ANI-02** — MUST. With animations, a switch between bar menus runs the outgoing menu's closing phase and the incoming menu's opening phase independently (each menu has its own popup and transition status, MNU §9); the bar flag `hasSubmenuOpen` is not cleared by the `sibling-open` close (MBR-STA-03). Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:226-233`; `menubar/Menubar.tsx:112-117`. Test: none (tests run with animations disabled).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- MBR-STR-02 — EVD-STA-07, CMP-STA-10, FFI registration row (post-commit, pre-paint C# state work): the bar's composite registration rebuild and publication land after paint plus a round trip; observable only as latency of dependent attribute updates.
- MBR-STA-05 — same row (CMP-STA-10): the highlighted-index adjustment on publication (CMP-STA-24/26/27) lands after paint plus a round trip; observable only as latency of the bar triggers' roving `tabindex` (MBR-DOM-05).
- MBR-KEY-10 — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01 row (adopted through MNU-KEY-09): the consumer's `cancel()` / `allowPropagation()` decision arrives a round trip after the Escape dispatch; JS applies the ADR 04 §3 fallback (prevent and stop whenever this menu is the innermost open popup). WebAssembly meets the spec through the synchronous `decide()` path.
- MBR-DIS-02 — same row, adopted through MNU-DIS-02: as MBR-KEY-10.
- MBR-ANI-02 — TRN-ANI-03/09/10 row (adopted through MNU-ANI-01, MNU-ANI-06): the outgoing bar menu's unmount (or `hidden`) and `onOpenChangeComplete(false)` land in the first render batch after the completion message, about two round trips after its exit animation ends; JS sets `hidden` at animation end (optimistic convergent write, ADR 04 §2).
- MBR-POS-01 — RND-STA-01 row (mirrored fields, adopted through MNU-POS-06): the Positioner's `data-side` is JS-written at frame timing; the `side` state field (and so the Popup's attributes and `ClassValue`/`StyleValue`) follows one round trip later.

## 10. Positioning

- **MBR-POS-01** — MUST. A top-level bar menu's Positioner defaults to `side: 'bottom'` in a horizontal bar and `side: 'inline-end'` in a vertical bar, and to `align: 'start'` in both; explicit `side`/`align` props win. The resolved side is reported to function-form offsets and as `data-side`. Server: relaxed, see §9.
  Source: `menu/positioner/MenuPositioner.tsx:98-102`. Test: [L2] `MenuPositioner.test.tsx` "uses bottom as the default side when the menubar is horizontal", "uses inline-end as the default side when the menubar is vertical" (L4 for the resolved side and resulting placement).
- **MBR-POS-02** — MUST. All other positioning defaults of a top-level bar menu are those of a top-level menu (MNU-POS-02/03: `sideOffset` 0, `alignOffset` 0, `collisionPadding` 5, `arrowPadding` 5, `positionMethod` `'absolute'`, dropdown collision avoidance without axis fallback); submenus inside bar menus use the submenu defaults (MNU-API-26, MNU-API-29).
  Source: `menu/positioner/MenuPositioner.tsx:39-55,92-132`. Test: none in `menubar/`.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Composite root part (`CompositeRoot`) | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) | `orientation` = Menubar `orientation`; `loopFocus` = Menubar `loopFocus`; `enableHomeAndEndKeys: true`; `highlightItemOnHover` = `hasSubmenuOpen`; `stopEventPropagation` default `true`; no `grid`, `disabledIndices`, `modifierKeys`, `onLoop`, `highlightedIndex`; `stateAttributesMapping` = `{ hasSubmenuOpen → data-has-submenu-open }`; extra props `{ role: 'menubar', id, 'aria-orientation': orientation }` before consumer props. |
| Composite item part (`CompositeItem`) | same | Used by `Menu.Trigger` inside a bar: `tag: 'button'`, state/mapping of the Menu trigger (`pressableTriggerOpenStateMapping`). |
| Keyboard relay | same (CMP-KEY-14) | The bar's relay is forwarded by every bar trigger to its menu store (`keyboardEventRelay`), inherited by submenus; popup keydowns not stopped are relayed. |
| List navigation (`useListNavigation`) | same | For bar menus: `nested: true`, `parentOrientation` = Menubar `orientation` (other options as MNU §11). |
| Floating tree | [floating-and-positioning](../architecture/floating-and-positioning.md) | Menubar owns a `FloatingTree` and a `FloatingNode`; bar menus are child nodes; listens to `menuopenchange` for `hasSubmenuOpen`. |
| `useClick` (trigger) | same | `event: 'click'` while the trigger owns the open menu, else `'mousedown'`; `toggle: true`; `stickIfOpen: false`. |
| `useFocus` (trigger) | same | `enabled: !disabled && hasSubmenuOpen`, no delay. |
| Hover trigger interaction | same | `enabled: openOnHover && !disabled && hasSubmenuOpen && !isMountedByThisTrigger`; `mouseOnly: true`; `move: false`; `restMs: undefined`; `delay: { close: closeDelay }`; `handleClose: safePolygon({ blockPointerEvents: false })`. Popup-side hover interaction disabled. |
| Mixed toggle guard | [popup-store-and-handles](../architecture/popup-store-and-handles.md) | `enabled: true` inside a bar, `mouseDownAction: 'open'`, `open` = trigger owns the open menu (PSH-PTR-04). |
| Internal backdrop / scroll lock | same | Backdrop when `Menubar.modal` (any open reason), cutout = Menubar element; scroll lock `open && Menubar.modal` (PSH-PTR-05/07). |
| Focus manager | floating-and-positioning | `modal: false`; `returnFocus` = trigger present or reason ≠ `outside-press`; context tree (not the menu's external tree); no inside elements, no next focusable element. |
| Button mechanism | [button-behavior](../architecture/button-behavior.md) | Menu trigger's `useButton({ disabled, native })`, inferred composite mode (UBT-STA-01). |

## 12. Edge cases, invariants and dev warnings

- **MBR-EDGE-01** — MUST. Reading the Menubar's ancestor-provided state outside a Menubar in the required mode throws `Error("Base UI: MenubarContext is missing. Menubar parts must be placed within <Menubar>.")`. (Menu parts read it in optional mode and never throw; the message is reachable only through internal consumers.)
  Source: `menubar/MenubarContext.ts:19-30`. Test: [L2] `Menubar.test.tsx` "throws when the menubar context is missing".
- **MBR-EDGE-02** — SHOULD. In development, a `Menu.Root` inside a Menubar given an explicit `modal` prop logs the warning of MNU-EDGE-02 while the prop is supplied (the number of repetitions is not specified; parameters: none; the message is fixed).
  Source: `menu/root/MenuRoot.tsx:207-213`. Test: none in `menubar/`.
- **MBR-EDGE-03** — MUST. After a top-level bar menu closes with reason `list-navigation` and the bar does not open another menu (e.g. the close key of a vertical bar, or a backward key at the first trigger with `loopFocus` `false`), `hasSubmenuOpen` stays `true`: hovering or focusing another trigger still opens its menu, and `data-has-submenu-open` stays present, until a later bar menu close with another reason clears it.
  Source: `menubar/Menubar.tsx:112-117`. Test: none.
- **MBR-EDGE-04** — MUST. A Menubar with no triggers renders an empty `role="menubar"` element with no Tab stop; non-trigger focusable content inside the bar keeps its own tab order and is skipped by the bar's arrow keys.
  Source: `internals/composite/root/useCompositeRoot.ts:111-113`; `menubar/Menubar.tsx:78-90`. Test: [L2] `Menubar.test.tsx` "sets aria-orientation on the root element" (empty bar renders).
- **MBR-EDGE-05** — MUST. Only top-level menus of the bar interact with `hasSubmenuOpen`, sibling closing and the `'group'` instant type; a `Menu.SubmenuRoot` inside a bar menu, even several levels deep, is a plain submenu (parent kind `'menu'`), inherits the bar's root id, floating tree and keyboard relay through its parent menu (MNU-STA-08), and shares the bar's drag-release flag.
  Source: `menu/root/MenuRoot.tsx:94-100`; `menu/store/MenuStore.ts:91-97,125-148,175-181`. Test: [L4] `Menubar.test.tsx` "closes open submenus when navigating to the next menubar item with ArrowRight".
- **MBR-EDGE-06** — MUST. A disabled bar trigger is skipped by arrow-key navigation and Home/End (it carries `disabled`), cannot be focused, and does not open on press, focus or hover; other triggers remain fully functional.
  Source: `menu/trigger/MenuTrigger.tsx:114-119,176-207`; CMP-ALG-02. Test: [L4] `Menubar.test.tsx` "keeps the menubar reachable when the first trigger is disabled".

## 13. Behaviors evidenced by tests

All `when using $name` tests run three times, once per trigger setup: **contained** (one Menu.Root per trigger inside the bar), **detached** (three `Menu.Trigger handle` in the bar, one Root with a function child outside the bar), **multiple contained** (one Root inside the bar with three triggers and a function child). Tests marked (browser) are skipped in jsdom; tests marked (jsdom) run only in jsdom.

### `menubar/Menubar.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`) | RND-DOM-36..49; MBR-STR-01, MBR-STA-01, MBR-API-06 |
| "throws when the menubar context is missing" | MBR-EDGE-01 |
| "ignores a delayed touch click immediately after focus opens another menu" | MBR-FOC-03, MBR-PTR-08, MBR-EVT-02, MNU-STA-05 |
| "closes on a touch item click immediately after focus opens another menu" | MBR-PTR-08, MBR-DIS-04, MNU-STA-05 |
| "keeps focus on an outside target when a triggerless menubar menu closes" | MBR-STR-06, MBR-FOC-05, MBR-DIS-01 |
| click interactions: "should open the menu after clicking on its trigger and close it when clicking again" (browser, ×3) | MBR-PTR-01, MBR-PTR-02 |
| hover: "should not open submenus on hover when no submenu is already open" (browser, ×3) | MBR-API-09, MBR-PTR-03 |
| hover: "should open submenus on hover when another submenu is already open" (browser, ×3) | MBR-STA-02, MBR-DOM-03, MBR-API-09, MBR-API-10, MBR-PTR-04, MBR-DIS-03, MBR-EVT-02 |
| hover: "should open nested submenus on hover when parent menu is open" (browser, ×3) | MBR-STA-04, MBR-PTR-05, MBR-PTR-07 |
| hover: "should open another menu on hover when a nested submenu is open" (browser, ×3) | MBR-STR-03, MBR-PTR-07, MBR-DIS-06 |
| focus: "focuses a menubar item without immediately opening the menu" (×3) | MBR-FOC-01, MBR-FOC-02, MBR-KEY-06, MBR-KEY-11 |
| "should respect closeOnClick on nested items when the menu was opened on click" (browser, ×3) | MBR-PTR-07 |
| "should respect closeOnClick on nested items when the menu was opened on hover" (browser, ×3) | MBR-PTR-04, MBR-PTR-07 |
| keyboard: "should navigate between menubar items with arrow keys" (×3) | MBR-STR-02, MBR-KEY-01 |
| keyboard: "moves focus to the first and last triggers with Home and End" (×3) | MBR-KEY-02 |
| keyboard: "should open the menu with Space key" (×3) | MBR-KEY-06 |
| keyboard: "should navigate within the menu using arrow keys" (×3) | MBR-KEY-06, MBR-FOC-04, MBR-API-11 |
| keyboard: "should open the submenu with right arrow key" (×3) | MBR-KEY-09 |
| keyboard: "should close the menu with Escape key" (browser, ×3) | MBR-KEY-10, MBR-DIS-02 |
| keyboard: "should close submenu with left arrow key and return focus to submenu trigger" (×3) | MBR-KEY-09, MBR-KEY-07, MBR-FOC-05 |
| keyboard: "closes open submenus when navigating to the next menubar item with ArrowRight" (jsdom, ×3) | MBR-KEY-08, MBR-DIS-03, MBR-EVT-04, MBR-EDGE-05 |
| keyboard: "should navigate between menus using left/right arrow keys when menus are open" (jsdom, ×3) | MBR-KEY-08, MBR-KEY-13, MBR-FOC-03 |
| mixed: "should allow keyboard navigation after opening a menu with mouse click" (jsdom, ×3) | MBR-FOC-04 |
| mixed: "should allow clicking a menu trigger then navigating to another menu with keyboard" (jsdom, ×3) | MBR-KEY-08 |
| touch: "closes the entire tree on a single outside press after opening a submenu" (jsdom, ×3) | MBR-PTR-08, MBR-DIS-01 |
| scroll locking: "applies scroll lock when a touch-opened submenu covers the viewport width" (browser, ×3) | MBR-DIS-05, MBR-ARIA-05 |
| scroll locking: "does not apply scroll lock when a touch-opened submenu is narrower than the viewport" (browser, ×3) | MBR-DIS-05 |
| scroll locking: "updates scroll lock when handing off between top-level touch-opened menus" (browser, ×3) | MBR-DIS-05, MBR-DIS-03 |
| loopFocus true: "should loop around to the first item after the last one" / "should loop around to the last item after the first one" (jsdom, ×3) | MBR-API-02, MBR-KEY-01 |
| loopFocus false: "should stay on the last item when navigating beyond it" / "should stay on the first item when navigating before it" (jsdom, ×3) | MBR-API-02, MBR-KEY-01 |
| "disables child menus when menubar is disabled" (×3) | MBR-API-04, MBR-API-08, MBR-DOM-06, MBR-FOC-01, MBR-KEY-11 |
| "correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)" (browser, ×3) | MBR-STA-03, MBR-DOM-03, MBR-PTR-02, MBR-PTR-04 |
| role: "sets role="menubar" on the root element" (×3) | MBR-ARIA-01 |
| role: "sets aria-orientation on the root element" (×3) | MBR-ARIA-02, MBR-API-01, MBR-EDGE-04 |
| role: "moves between triggers with the main-axis arrow keys when vertical" (×3) | MBR-API-01, MBR-KEY-01, MBR-FOC-02 |
| role: "opens a menu of a vertical $direction menubar with $openKey" (`ltr`/`rtl`, ×3) | MBR-KEY-04, MBR-KEY-05, MBR-I18N-02 |
| role: "sets role="menuitem" on menu triggers" (×3) | MBR-STR-02, MBR-ARIA-03 |
| "renders contained top-level portal ownership as an allowed menubar child" | MBR-ARIA-04 |
| "renders detached top-level portal ownership without an accessibility role" | MBR-ARIA-04, MBR-STR-04 |
| disabled state: "keeps the menubar reachable when the first trigger is disabled" | MBR-STA-05, MBR-DOM-05, MBR-DOM-06, MBR-FOC-01, MBR-EDGE-06 |
| disabled state: "disables menu items while the menubar is disabled" | MBR-API-04, MBR-API-08 |
| filterable menubar menus: "preserves %s trigger navigation" (`horizontal`, `vertical`) | MBR-KEY-04, MBR-KEY-14 |

### Tests in other files that exercise this component

| File / test | Requirements |
|---|---|
| `menu/positioner/MenuPositioner.test.tsx` "uses bottom as the default side when the menubar is horizontal" (browser) | MBR-POS-01 |
| `menu/positioner/MenuPositioner.test.tsx` "uses inline-end as the default side when the menubar is vertical" (browser) | MBR-POS-01 |

No requirement exists only because of a test beyond those already placed in §§3–12.

## 14. React-specific notes and porting considerations

Non-normative.

- **Ancestor-provided state.** `MenubarContext` carries `{ modal, disabled, orientation, hasSubmenuOpen, setHasSubmenuOpen, contentElement, setContentElement, allowMouseUpTriggerRef, rootId }`. Menu parts read it in optional mode; a port typically passes a nullable "enclosing menubar" reference down the component hierarchy. `contentElement` is the Menubar element (used only as the backdrop cutout). Port: see decisions/05-state-store-and-event-details.md §1 (shared state cascaded as a fixed value; parts subscribe through selectors).
- **Store `parent` overwrite.** The Menu store's `parent` is first computed from the Root's position, then overwritten by whichever trigger mounts the popup (trigger data forwarding). That is how a detached Root outside the bar gets menubar behavior, while `Menu.Portal` deliberately keeps the Root-position parent for the `aria-owns` role. A port should model "parent kind" as a property of the active trigger with a Root-position fallback, and keep the portal-owner decision separate. On the very first open from a detached trigger the Root-position parent may still be in effect for that one open-change (instant type, see §16 Q6). Port: see decisions/05-state-store-and-event-details.md §1 (handles as `PopupHandle<TPayload>` with the attached-store stack, PSH-STA-34..40).
- **Event bubbling through portals.** React synthetic events bubble along the component tree, so a keydown in a portaled bar menu bubbles into the Menubar element's handler even though the DOM popup is elsewhere. Base UI additionally relays popup keydowns to the bar explicitly (needed for detached Roots) and relies on the bar's `stopPropagation()` to avoid double handling. A port whose events follow the DOM tree should rely on the explicit relay alone (MBR-KEY-08/13). Port: see decisions/04-js-interop-boundary.md §1 (`composite` module owns bar roving focus and the relay; C# receives highlight changes) and §5 (portals by DOM move).
- **`hasSubmenuOpen` timing.** The flag is React state set from a passive effect listening to `menuopenchange` events, themselves emitted from a passive effect in each Positioner. It therefore lags the menus' open state by a render; behaviors gated on it (focus-open, hover-open, hover highlight) read the last committed value. Ports should either reproduce the lag or update the flag synchronously; the observable difference is limited to the same-task window after an open/close. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, "after paint" row) and decisions/05-state-store-and-event-details.md §1.
- **Focus after composite moves** is applied in a microtask so that a closing popup's return-focus work runs first (CMP-KEY-10). Port: see decisions/04-js-interop-boundary.md §1 (`composite` module; the microtask stays in JavaScript, decisions/01-hosting-model-and-timing.md constraint C2).
- **`allowMouseUpTriggerRef`** is a mutable reference shared by all menus of the bar; it is not render state.
- **Interop placement.** Trigger press-to-toggle, hover switching (`mouseenter` with no delay), focus-to-open and the bar's floating tree belong to the `interactions` module; Escape dismissal is a JS-owned decision point. Port: see decisions/04-js-interop-boundary.md §1 (`interactions`), §3 (`decide()`, Escape fallback prevent-and-stop when innermost); decisions/01-hosting-model-and-timing.md §Relaxation table (EVD-EVT-11, EVD-DIS-01).
- **Fragments and keys.** Outside a bar `Menu.Trigger` wraps itself in keyed fragments to keep DOM identity while guards appear; inside a bar it renders a composite item directly, so no identity concern arises. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 1: frame rewriting preserves `@key`).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| MBR-STR-01 | MUST | Menubar renders one `<div>` with RND rendering support | 2 |
| MBR-STR-02 | MUST | Menubar is a composite root; every descendant Menu.Trigger is an item in DOM order | 2 |
| MBR-STR-03 | MUST | Menubar owns a floating tree node; bar menus are its child nodes | 2 |
| MBR-STR-04 | MUST | Root inside the bar has parent kind `menubar`; detached Root adopts it via its trigger | 2 |
| MBR-STR-05 | MUST | Menus under a bar satisfy MNU with the substitution table | 2 |
| MBR-STR-06 | MUST | Triggerless Menu.Root allowed inside a bar | 2 |
| MBR-API-01 | MUST | `orientation` default `horizontal`; drives navigation, ARIA, placement | 3.1 |
| MBR-API-02 | MUST | `loopFocus` default `true`; composite wrapping between triggers | 3.1 |
| MBR-API-03 | MUST | `modal` default `true`; backdrop + scroll lock, no focus trap | 3.1 |
| MBR-API-04 | MUST | `disabled` default `false`; disables every bar menu | 3.1 |
| MBR-API-05 | MUST | `id` prop or generated; rendered; root owner id of bar menus | 3.1 |
| MBR-API-06 | MUST | Forwarded attributes override `role`/`aria-orientation` | 3.1 |
| MBR-API-07 | MUST | Menu.Root `modal` ignored under a bar (warns) | 3.1 |
| MBR-API-08 | MUST | Menu/trigger disabled OR-ed with Menubar `disabled` | 3.1 |
| MBR-API-09 | MUST | Trigger `openOnHover` default = `hasSubmenuOpen`; hover needs an open bar menu | 3.1 |
| MBR-API-10 | MUST | Trigger `delay` ignored; hover-open immediate | 3.1 |
| MBR-API-11 | MUST | Menu.Root `orientation` still governs the popup | 3.1 |
| MBR-STA-01 | MUST | State = `orientation`, `modal`, `hasSubmenuOpen` | 3.1 |
| MBR-STA-02 | MUST | `hasSubmenuOpen` set when a top-level bar menu opens | 4 |
| MBR-STA-03 | MUST | `hasSubmenuOpen` cleared on close unless `sibling-open`/`list-navigation` | 4 |
| MBR-STA-04 | MUST | Reports come from Positioners after paint; submenus ignored | 4 |
| MBR-STA-05 | MUST | Bar owns the composite highlighted index (Tab stop) | 4 |
| MBR-STA-06 | MUST | Root id of every bar menu is the Menubar id | 4 |
| MBR-STA-07 | MUST | Drag-release arming flag shared across the bar | 4 |
| MBR-DOM-01 | MUST | `data-orientation` always | 5.1 |
| MBR-DOM-02 | MUST | `data-modal` iff modal | 5.1 |
| MBR-DOM-03 | MUST | `data-has-submenu-open` iff `hasSubmenuOpen` | 5.1 |
| MBR-DOM-04 | MUST | Renders resolved `id` | 5.1 |
| MBR-DOM-05 | MUST | Triggers carry roving `tabindex` | 5.2 |
| MBR-DOM-06 | MUST | Triggers in composite button mode (`disabled` + `aria-disabled`) | 5.2 |
| MBR-DOM-07 | MUST | No focus guards around bar triggers | 5.2 |
| MBR-DOM-08 | MUST | Internal backdrop iff Menubar `modal`, cutout = Menubar | 5.2 |
| MBR-DOM-09 | MUST | Popups' `data-rootownerid` = Menubar id | 5.2 |
| MBR-ARIA-01 | MUST | `role="menubar"` | 5.3 |
| MBR-ARIA-02 | MUST | `aria-orientation` always rendered | 5.3 |
| MBR-ARIA-03 | MUST | Triggers `role="menuitem"` plus Menu trigger ARIA | 5.3 |
| MBR-ARIA-04 | MUST | Portal owner span `role="group"` inside bar; none when detached | 5.3 |
| MBR-ARIA-05 | MUST | Popups keep Menu ARIA | 5.3 |
| MBR-KEY-01 | MUST | Bar main keys move between enabled triggers | 6.1 |
| MBR-KEY-02 | MUST | Home/End to first/last trigger | 6.1 |
| MBR-KEY-03 | MUST | Modified keys ignored; no disabled indices | 6.1 |
| MBR-KEY-04 | MUST | Bar open key opens with `list-navigation`, first item | 6.1 |
| MBR-KEY-05 | MUST | Other cross-axis arrow on a trigger does nothing | 6.1 |
| MBR-KEY-06 | MUST | Enter/Space toggle; no hover stick | 6.1 |
| MBR-KEY-07 | MUST | Menu close key closes; propagates to bar if a bar main key | 6.1 |
| MBR-KEY-08 | MUST | Unhandled popup keys relayed to bar; switch menus | 6.1 |
| MBR-KEY-09 | MUST | Submenu open/close keys do not move the bar | 6.1 |
| MBR-KEY-10 | MUST | Escape closes, returns focus, clears bar flag, no reopen | 6.1 |
| MBR-KEY-11 | MUST | Tab not intercepted; bar is one Tab stop; no guards | 6.1 |
| MBR-KEY-12 | MUST | Shift+Tab inside menu closes `focus-out`, focus trigger | 6.1 |
| MBR-KEY-13 | MUST | Handled relayed keys stopped; no double handling | 6.1 |
| MBR-KEY-14 | MUST | Filterable bar menus: no trigger key relay | 6.1 |
| MBR-KEY-15 | MUST | No bar-level typeahead | 6.1 |
| MBR-PTR-01 | MUST | `mousedown` opens; same-press click ignored | 6.2 |
| MBR-PTR-02 | MUST | Click closes owning trigger's menu; no stickIfOpen | 6.2 |
| MBR-PTR-03 | MUST | No hover effect while no bar menu open | 6.2 |
| MBR-PTR-04 | MUST | Hover switch: immediate `trigger-hover` open, mouse only | 6.2 |
| MBR-PTR-05 | MUST | Bar menus never hover-close; `closeDelay` inert | 6.2 |
| MBR-PTR-06 | MUST | Hover moves bar focus while a menu is open | 6.2 |
| MBR-PTR-07 | MUST | Submenus in bar menus behave as Menu | 6.2 |
| MBR-PTR-08 | MUST | Touch: press only; outside press closes tree; 300 ms guard | 6.2 |
| MBR-PTR-09 | MUST | Backdrop does not block the Menubar area | 6.2 |
| MBR-FOC-01 | MUST | Single Tab stop on first enabled trigger | 6.3 |
| MBR-FOC-02 | MUST | Focus alone does not open while no bar menu open | 6.3 |
| MBR-FOC-03 | MUST | Focus opens (`trigger-focus`) while a bar menu is open | 6.3 |
| MBR-FOC-04 | MUST | Initial focus on open follows Menu | 6.3 |
| MBR-FOC-05 | MUST | Return focus unless triggerless + outside press | 6.3 |
| MBR-FOC-06 | MUST | Bar menus' focus manager is non-modal | 6.3 |
| MBR-DIS-01 | MUST | Outside press closes; bar area handled by triggers | 6.4 |
| MBR-DIS-02 | MUST | Escape as Menu | 6.4 |
| MBR-DIS-03 | MUST | Other top-level bar menus close with `sibling-open` | 6.4 |
| MBR-DIS-04 | MUST | Item press closes tree, clears bar flag | 6.4 |
| MBR-DIS-05 | MUST | Scroll lock iff Menubar `modal` (touch width rule) | 6.4 |
| MBR-DIS-06 | MUST | No pointer-leave dismissal | 6.4 |
| MBR-I18N-01 | MUST | RTL swaps horizontal bar keys | 6.5 |
| MBR-I18N-02 | MUST | Vertical bar open key ArrowRight/ArrowLeft by direction | 6.5 |
| MBR-I18N-03 | MUST | RTL keys inside bar menus | 6.5 |
| MBR-EVT-01 | MUST | No Menubar callbacks; Menu callbacks cancelable | 7 |
| MBR-EVT-02 | MUST | Reasons produced only by listed bar interactions | 7 |
| MBR-EVT-03 | MUST | Trigger blur closes with `trigger-focus` while focus-open enabled | 7 |
| MBR-EVT-04 | MUST | Keyboard switch: submenu false, previous false, next true | 7 |
| MBR-ANI-01 | MUST | `'group'` instant type for bar switch reasons | 9 |
| MBR-ANI-02 | MUST | Outgoing/incoming menus animate independently | 9 |
| MBR-POS-01 | MUST | Default side bottom / inline-end, align start | 10 |
| MBR-POS-02 | MUST | Other positioning defaults as top-level menu | 10 |
| MBR-EDGE-01 | MUST | Context-missing error message | 12 |
| MBR-EDGE-02 | SHOULD | `modal` on a bar Menu.Root warns in development | 12 |
| MBR-EDGE-03 | MUST | `list-navigation` close leaves bar in menu mode | 12 |
| MBR-EDGE-04 | MUST | Empty bar / non-trigger content | 12 |
| MBR-EDGE-05 | MUST | Submenus are plain submenus inheriting bar ids/tree/relay | 12 |
| MBR-EDGE-06 | MUST | Disabled trigger skipped and inert | 12 |

## 16. Open questions

1. **ArrowUp on a horizontal-bar trigger does nothing** (`floating-ui-react/hooks/useListNavigation.ts:877-893`). The WAI-ARIA menubar pattern opens the menu and focuses the last item on ArrowUp; Base UI 1.9.0 only opens with the bar open key (ArrowDown). The assignment hint "ArrowDown/Up to open a menu from its trigger" is therefore only half true; documented as MBR-KEY-05.
2. **Reason of the outgoing menu during a switch.** When focus moves to another trigger (MBR-KEY-08, MBR-FOC-03), the previous menu can be closed by three paths: the bar's `sibling-open` notification (after paint, `menu/positioner/MenuPositioner.tsx:134-147`), the trigger blur check of `useFocus` (`trigger-focus`, 0 ms, `floating-ui-react/hooks/useFocus.ts:185-233`), or the focus manager's focus-out handling. Which one wins depends on timing; the `trigger-focus`/`focus-out` close would clear `hasSubmenuOpen` (`menubar/Menubar.tsx:112-117`) while the new menu's open report sets it again. No test asserts the outgoing reason; MBR-DIS-03 and MBR-EVT-03 describe each path but not their order.
3. **Focus target after a focus- or hover-switch.** A `trigger-focus` open is not a keyboard open, so by MNU-FOC-02 the popup element (not an item) should receive focus (`menu/popup/MenuPopup.tsx:84-96`, FLT-FOC-06). No Menubar test asserts where focus is after `ArrowRight` switches menus; MBR-FOC-04 cites the Menu rule. Mirror of menu.md §16 Q10 (second pass 2026-10-18): a bar menu passes `initialFocus` = `true` for every open kind (`menu/popup/MenuPopup.tsx:84-85`, parent kind `'menubar'` is not `'menu'`), so a hover switch also moves focus off the newly entered trigger into its popup; only a filterable bar menu keeps focus on the trigger after a hover open (`menu/filter-root/MenuFilterPopup.tsx:27-48`). Untested; confirm before porting. Decided (2026-10-18): replicated by the port (ADR 04 Amendments; menu.md §16 Q10).
4. **Reopen after `list-navigation` close.** In MBR-KEY-07 the menu close key focuses the trigger synchronously while `hasSubmenuOpen` is still `true` (`floating-ui-react/hooks/useListNavigation.ts:577-582`); the trigger's focus-open (`menu/trigger/MenuTrigger.tsx:205-207`) is not blocked for `list-navigation` closes (only `trigger-press`/`escape-key`, `useFocus.ts:100-108`), so in a vertical bar the menu may immediately reopen with `trigger-focus` when the focus is `:focus-visible`. Untested; MBR-EDGE-03 records only the flag behavior.
5. **MNU base-doc mismatches.** MNU-PTR-06 says `stickIfOpen` "is also forced while another menu in a Menubar is hover-open"; the source sets `stickIfOpen: false` for every trigger with a parent (`menu/trigger/MenuTrigger.tsx:202`), and the #2222 test confirms the first click closes a hover-opened bar menu. MNU-PTR-05's "The open is skipped while another menu of the same Menubar group is already being opened via hover" has no matching branch in `MenuTrigger.tsx:176-190`. MNU-DIS-07's "when the Menubar cancels a pending open of a sibling" has no Menubar-specific code; `cancel-open` in a bar comes only from the drag-release/hover `mouseup` listener (`menu/trigger/MenuTrigger.tsx:130-171`). Suggest the MNU QA pass revisit these three sentences.
   Resolved (QA 2026-10-14): menu.md corrected — MNU-PTR-06 (`stickIfOpen: false` under any parent kind), MNU-PTR-05 (no sibling-pending check), MNU-DIS-07 (no Menubar-specific `cancel-open`).
6. **First open from a detached trigger.** The store `parent` switches to `'menubar'` only after the detached trigger has mounted the popup (`utils/popups/popupStoreUtils.ts:382-391`), so the first open-change of a detached Root computes its instant type with the Root-position parent (`menu/root/MenuRoot.tsx:422-430`); a focus/hover first open from a detached bar trigger would not get `'group'`. Untested.
7. **ContextMenu inside a Menubar.** Parent-kind detection checks the Menubar before the ContextMenu (`menu/root/MenuRoot.tsx:102-117`), so a ContextMenu rendered inside a Menubar would be treated as a bar menu (losing context-menu anchoring/modality). Probably unintended; untested.
8. **Outline gaps.** The outline lists no `id` prop although `Menubar` consumes and re-emits it (`menubar/Menubar.tsx:41,49`) and uses it as the root owner id of all bar popups (MBR-STA-06); documented in §3.1. The docs page has no behavioral prose beyond the anatomy and API table. `data-modal` and `data-orientation` come from the generic state projection, not from the explicit mapping (`menubar/Menubar.tsx:19-23`); the outline lists them correctly. Hint "`modal`?" — the prop exists (default `true`).
9. **Unused context field.** `MenubarContext.allowMouseUpTriggerRef` is created per bar and shared into every bar menu store (`menu/store/MenuStore.ts:175-181`), but `MenuTrigger`'s reset of the flag when its menu is not open runs only for menus without a parent (`menu/trigger/MenuTrigger.tsx:121-125`), so a stale `true` could let a later `mouseup` on an item of another bar menu activate it within the same bar session. Untested (MBR-STA-07).
