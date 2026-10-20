# Menu

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/menu/` (28 part directories) |
| Tests | `packages/react/src/menu/**/*.test.tsx` + `*.test.ts` (38 files) + 3 `*.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/menu/page.mdx` |
| Prefix | `MNU` |
| Status | approved |
| Builds on | none |
| Derived by | [context-menu](context-menu.md), [menubar](menubar.md) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Menu is a dropdown list of actions anchored to a trigger button. A **Root** owns the open state and the item highlight; a **Trigger** (contained in the Root, or *detached* and connected through a handle created by `Menu.createHandle()`) opens it by press, keyboard or, optionally, hover; a **Portal** → **Positioner** → **Popup** chain renders the floating list; items come in four flavours (**Item**, **LinkItem**, **CheckboxItem**, **RadioItem** inside a **RadioGroup**), can be grouped (**Group** + **GroupLabel**), separated (**Separator**) and nested to any depth with **SubmenuRoot** + **SubmenuTrigger**. Optional parts add an **Arrow**, a styled **Backdrop**, a **Viewport** for animating content when one popup is shared by several triggers, and — behind `Menu.FilterProvider` (documented as *Preview* in 1.9.0) — a type-to-filter mode built from **Input**, **Clear**, **Empty** and **List**.

What the user experiences: pressing the trigger (mousedown) opens the menu; the first item is highlighted and focused when the menu was opened from the keyboard, while a pointer open leaves the popup (or its `List`) focused with nothing highlighted. Arrow keys move a roving highlight (`data-highlighted`, `tabindex="0"`) through the items, including disabled ones; typing letters jumps to the matching item; Enter/Space activate the highlighted item, which closes the menu unless the item opts out; Escape closes the current level and returns focus to its trigger; Tab/Shift+Tab leave the menu and close it; clicking outside closes the whole tree. Pressing the trigger, dragging onto an item and releasing activates that item. Hover-opened menus close when the pointer leaves, with safe-polygon intent tracking between the trigger and the popup, and treat an "impatient" click (within 500 ms of opening) as keep-open. Submenus open on hover (after a delay) or with the cross-axis arrow key (ArrowRight in LTR, flipped in RTL, ArrowDown in a horizontal parent) and close with the opposite key, Escape, or when a sibling item is hovered.

Context Menu and Menubar reuse `MenuRootInternal`, `Menu.Positioner`, `Menu.Popup` and every item part; their deviations are documented in their own documents and only the branches they switch on (`parent.type === 'context-menu' | 'menubar'`) are named here.

## 2. Anatomy

```
<Menu.FilterProvider>           optional, 0..1 directly above a Root/SubmenuRoot   no element
  <Menu.Root>                   required, 1                                        no element
    <Menu.Trigger>              optional, 0..n (contained or detached via handle)  button
    <Menu.Portal>               required for a popup, 0..1                         div (portal node)
      <Menu.Backdrop>           optional, 0..1                                     div
      <Menu.Positioner>         required inside Portal, 1                          div
        <Menu.Popup>            required inside Positioner, 1                      div
          <Menu.Arrow>          optional, 0..1                                     div
          <Menu.Input>          filter only, 0..1                                  input
          <Menu.Clear>          filter only, 0..1                                  button
          <Menu.Empty>          filter only, 0..1                                  div
          <Menu.List>           optional (required with filtering), 0..1           div
            <Menu.Item>         0..n                                               div
            <Menu.LinkItem>     0..n                                               a
            <Menu.Separator>    0..n                                               div
            <Menu.SubmenuRoot>  0..n                                               no element
              <Menu.SubmenuTrigger>  required inside SubmenuRoot, 1               div
              <Menu.Portal> … <Menu.Positioner> … <Menu.Popup> …                  (recursive)
            <Menu.Group>        0..n                                               div
              <Menu.GroupLabel> 0..1                                               div
            <Menu.RadioGroup>   0..n                                               div
              <Menu.GroupLabel> 0..1                                               div
              <Menu.RadioItem>  0..n                                               div
                <Menu.RadioItemIndicator>  0..1                                    span
            <Menu.CheckboxItem> 0..n                                               div
              <Menu.CheckboxItemIndicator> 0..1                                    span
          <Menu.Viewport>       optional, 0..1 (wraps the content)                 div
```

Parts locate their Root through ancestor-provided shared state, not DOM adjacency; items may sit at any depth below the Popup (inside Groups, Lists, Viewports, or arbitrary wrappers). Structural-only parts (state attributes, no behavior): `Arrow`, `Backdrop`, `Group`, `GroupLabel`, `Separator`, `Viewport`, `Empty`.

- **MNU-STR-01** — MUST. `Menu.Root` renders no element of its own; it owns one popup store (PSH-STR-01) for its whole mounted lifetime and provides it to every descendant part. When it has no enclosing menu it also establishes a new floating tree that every nested submenu joins.
  Source: `menu/root/MenuRoot.tsx:156-176,747-759`. Test: [L4] `MenuRoot.test.tsx` popupConformanceTests.
- **MNU-STR-02** — MUST. `Menu.Trigger` renders a `<button>` by default and MUST resolve a store either from an ancestor `Menu.Root` or from its `handle` prop (PSH-STR-02); otherwise it throws the error in MNU-EDGE-01. Any number of triggers (contained, detached, or mixed) MAY target one Root; at most one is the active trigger (PSH-STR-06).
  Source: `menu/trigger/MenuTrigger.tsx:66-73,264-270`. Test: [L2] `MenuTrigger.test.tsx` "throws without Menu.Root or a handle"; `MenuRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests. (L4 for the detached-triggers suite clause)
- **MNU-STR-03** — MUST. Detached triggers are supported only for top-level menus: a `Menu.SubmenuTrigger` MUST be rendered inside its `Menu.SubmenuRoot` (it takes no `handle`), and a `Menu.Trigger` rendered inside a `Menu.SubmenuRoot` behaves as a trigger of the submenu's own store, not of the parent.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:47-50,68`; `menu/trigger/MenuTrigger.tsx:66-68`. Docs: `components/menu/page.mdx` §Detached triggers. Test: [L2] `MenuSubmenuTrigger.test.tsx` "throws when rendered outside Menu.SubmenuRoot".
- **MNU-STR-04** — MUST. `Menu.Portal` MUST enclose `Menu.Positioner`; a Positioner rendered without a Portal throws `Base UI: <Menu.Portal> is missing.` The Portal renders its subtree into a portal node (default container `<body>`) only while the popup is mounted or `keepMounted` is set.
  Source: `menu/portal/MenuPortalContext.ts:6-12`; `menu/portal/MenuPortal.tsx:19-39`. Test: [L2] `MenuPositioner.test.tsx` "throws when rendered outside Menu.Portal".
- **MNU-STR-05** — MUST. `Menu.Positioner` renders a `<div>` and MUST enclose `Menu.Popup`; a Popup (or Arrow) rendered outside a Positioner throws `Base UI: MenuPositionerContext is missing. MenuPositioner parts must be placed within <Menu.Positioner>.`
  Source: `menu/positioner/MenuPositionerContext.ts:14-24`; `menu/popup/MenuPopup.tsx:51`; `menu/arrow/MenuArrow.tsx:23`. Test: [L2] `MenuPopup.test.tsx` "throws when rendered outside Menu.Positioner".
- **MNU-STR-06** — MUST. `Menu.Popup` renders a `<div>` carrying the `menu` role (or `presentation` when a `Menu.List` is rendered, MNU-ARIA-05) and is the element whose animations drive mount/unmount and `onOpenChangeComplete` (§9).
  Source: `menu/popup/MenuPopup.tsx:151-181`. Test: [L2] `MenuPopup.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **MNU-STR-07** — MUST. Every part other than Root, Trigger (with a handle), Portal, Backdrop and FilterProvider MUST be a descendant of a `Menu.Root`; rendering one outside throws `Base UI: MenuRootContext is missing. Menu parts must be placed within <Menu.Root>.`
  Source: `menu/root/MenuRootContext.ts:24-35`. Test: [L2] `MenuItem.test.tsx` "throws when rendered outside Menu.Root"; `MenuSubmenuTrigger.test.tsx` "throws when Menu.SubmenuRoot is rendered outside a menu".
- **MNU-STR-08** — MUST. `Menu.Item`, `Menu.CheckboxItem`, `Menu.RadioItem` and `Menu.SubmenuTrigger` render a `<div>`; `Menu.LinkItem` renders an `<a>`. All of them register in the Root's composite item list in DOM order (CMP-STA-02), which defines keyboard navigation order and typeahead labels.
  Source: `menu/item/MenuItem.tsx:29,55`; `menu/link-item/MenuLinkItem.tsx:32,63`; `menu/checkbox-item/MenuCheckboxItem.tsx:38,76`; `menu/radio-item/MenuRadioItem.tsx:41,80`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:58,223`. Test: [L2] `MenuItem.test.tsx` / `MenuLinkItem.test.tsx` / `MenuCheckboxItem.test.tsx` / `MenuRadioItem.test.tsx` / `MenuSubmenuTrigger.test.tsx` describeConformance.
- **MNU-STR-09** — MUST. `Menu.RadioItem` MUST be inside a `Menu.RadioGroup` (throws `Base UI: MenuRadioGroupContext is missing. MenuRadioGroup parts must be placed within <Menu.RadioGroup>.`); `Menu.CheckboxItemIndicator` MUST be inside a `Menu.CheckboxItem` (`Base UI: MenuCheckboxItemContext is missing. MenuCheckboxItem parts must be placed within <Menu.CheckboxItem>.`); `Menu.RadioItemIndicator` MUST be inside a `Menu.RadioItem` (`Base UI: MenuRadioItemContext is missing. MenuRadioItem parts must be placed within <Menu.RadioItem>.`); `Menu.GroupLabel` MUST be inside a `Menu.Group` or `Menu.RadioGroup` (`Base UI: MenuGroupContext is missing. Menu group parts must be used within <Menu.Group> or <Menu.RadioGroup>.`).
  Source: `menu/radio-group/MenuRadioGroupContext.ts:15-24`; `menu/checkbox-item/MenuCheckboxItemContext.ts:14-23`; `menu/radio-item/MenuRadioItemContext.ts:14-23`; `menu/group/MenuGroupContext.ts:8-17`. Test: [L2] `MenuRadioItem.test.tsx` "throws when rendered outside Menu.RadioGroup"; `MenuCheckboxItemIndicator.test.tsx` "throws when rendered outside Menu.CheckboxItem"; `MenuRadioItemIndicator.test.tsx` "throws when rendered outside Menu.RadioItem"; `MenuGroupLabel.test.tsx` "throws when rendered outside Menu.Group or Menu.RadioGroup".
- **MNU-STR-10** — MUST. `Menu.Group` and `Menu.RadioGroup` render a `<div role="group">`; `Menu.GroupLabel` renders a `<div>`; `Menu.CheckboxItemIndicator` and `Menu.RadioItemIndicator` render a `<span>`; `Menu.Separator` is the standalone `Separator` component (`<div role="separator" aria-orientation>`, SEP-STR-01, SEP-ARIA-01) — not the listbox separator; `Menu.Backdrop`, `Menu.Arrow`, `Menu.Viewport`, `Menu.List`, `Menu.Empty` render a `<div>`; `Menu.Input` renders an `<input>`; `Menu.Clear` renders a `<button>`.
  Source: `menu/group/MenuGroup.tsx:19-26`; `menu/radio-group/MenuRadioGroup.tsx:53-62`; `menu/group-label/MenuGroupLabel.tsx:32-40`; `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:48-57`; `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:48-57`; `menu/index.parts.ts:19`; `separator/Separator.tsx:16-26`; `menu/backdrop/MenuBackdrop.tsx:36`; `menu/arrow/MenuArrow.tsx:33`; `menu/viewport/MenuViewport.tsx:40`; `menu/list/MenuList.tsx:37`; `filter-dropdown/empty/FilterDropdownEmpty.tsx:36`; `filter-dropdown/input/FilterDropdownInput.tsx:59`; `filter-dropdown/clear/FilterDropdownClear.tsx:43`. Test: [L2] the describeConformance suites of `MenuGroup.test.tsx`, `MenuRadioGroup.test.tsx`, `MenuGroupLabel.test.tsx`, `MenuCheckboxItemIndicator.test.tsx`, `MenuRadioItemIndicator.test.tsx`, `MenuBackdrop.test.tsx`, `MenuArrow.test.tsx`, `MenuViewport.test.tsx`, `MenuList.test.tsx`, `MenuFilterParts.test.tsx`.
- **MNU-STR-11** — MUST. `Menu.SubmenuRoot` renders no element and MUST be a descendant of a menu (its Root or a parent SubmenuRoot); its store records the enclosing menu's store as `parent.type === 'menu'`, joins the enclosing floating tree as a child node of the parent popup, and shares the parent's `rootId`, floating tree and keyboard relay.
  Source: `menu/submenu-root/MenuSubmenuRoot.tsx:19-39`; `menu/root/MenuRoot.tsx:94-100,183-185`; `menu/store/MenuStore.ts:91-97,125-131,190-218`. Test: [L2] `MenuRoot.test.tsx` "submenu parent store subscription" (3 tests); `MenuRoot.triggerless.test.tsx` "preserves real trigger registration, initially present=%s".
- **MNU-STR-12** — MUST. Submenus nest to any depth; each level is a separate menu with its own open state, popup, positioner and item list, and each submenu trigger is simultaneously an item of the parent list and the (only) trigger of its own submenu.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:58,68,147-148`. Test: [L4] `MenuRoot.test.tsx` "returns focus to submenu triggers when closing nested menus", "closes the entire tree when clicking outside the deepest submenu".
- **MNU-STR-13** — MUST. `Menu.List` is optional. When rendered it takes the `menu` role and the accessible label from the Popup, which becomes presentational (MNU-ARIA-05), and the Trigger's `aria-controls` points at the List instead of the Popup (MNU-ARIA-03). It is the required container of items in a filterable menu (the filter root publishes its item list there).
  Source: `menu/list/MenuList.tsx:17-51`; `menu/store/MenuStore.ts:100-104`. Test: [L2] `MenuList.test.tsx` "takes the menu role and label from the popup".
- **MNU-STR-14** — MUST. `Menu.FilterProvider` renders no element and makes exactly the `Menu.Root` or `Menu.SubmenuRoot` rendered directly below it filterable; the provider is consumed by that root, so a plain `Menu.SubmenuRoot` inside a filterable menu stays plain, and each searchable submenu needs its own provider.
  Source: `menu/filter-provider/MenuFilterProvider.tsx:17-41`; `menu/root/MenuRoot.tsx:768-783`; `menu/submenu-root/MenuSubmenuRoot.tsx:22-32`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "makes the menu filterable", "does not extend to a plain submenu inside the filterable menu", "filters a submenu that uses FilterSubmenuRoot", "filters a submenu of a plain menu".
- **MNU-STR-15** — MUST. `Menu.Input`, `Menu.Clear` and `Menu.Empty` MUST be rendered inside a filterable menu; otherwise they throw MNU-EDGE-04. `Menu.List`, `Menu.Group`, `Menu.RadioGroup`, `Menu.Popup` and all item parts work in both plain and filterable menus and switch implementation according to the menu they belong to.
  Source: `menu/filter-root/MenuFilterContext.ts:71-96`; `menu/input/MenuInput.tsx:28`; `menu/clear/MenuClear.tsx:21`; `menu/empty/MenuEmpty.tsx:21`; `menu/popup/MenuPopup.tsx:236-242`; `menu/list/MenuList.tsx:60-66`; `menu/group/MenuGroup.tsx:37-43`; `menu/radio-group/MenuRadioGroup.tsx:86-94`. Test: [L2] `MenuFilterParts.test.tsx` "throws when <Menu.%s> is rendered without a filter provider"; `MenuFilterRoot.scoping.test.tsx` "throws when a filter part is rendered in a plain menu", "throws when a filter part is rendered in a plain submenu of a filterable menu".
- **MNU-STR-16** — MUST. While a contained top-level trigger is the active trigger of an open menu, one focus guard `<span>` is rendered immediately before and one immediately after the trigger element (PSH-STR-10); the trigger element itself keeps its DOM identity across this change. In a Menubar the trigger renders as a composite item and no guards are rendered.
  Source: `menu/trigger/MenuTrigger.tsx:272-308`. Test: [L2] `MenuRoot.test.tsx` "focus guards" (2 tests); `MenuTrigger.test.tsx` "tabbing backward from the open trigger". (L4 for the Shift+Tab-from-trigger clause)
- **MNU-STR-17** — MUST. When the popup is modal (MNU-STA-06) and was not opened by hover, an internal backdrop `<div role="presentation">` is rendered as the sibling immediately before the Positioner (PSH-STR-09) with a cutout for the trigger element, and it is `inert` while the menu is not open. Submenus never render one.
  Source: `menu/positioner/MenuPositioner.tsx:264-290`. Test: [L2] `MenuRoot.test.tsx` "should render an internal backdrop when `true`", "should not render an internal backdrop when `false`", "treats hover-opened menus as modal after a click".
- **MNU-STR-18** — MUST. The portal node's hidden `aria-owns` owner `<span>` (rendered in the Root's own tree) carries `role="group"` when the Portal belongs to a submenu or to a menu inside a Menubar, and no role otherwise, so that the owned subtree stays a valid child of the enclosing `menu`.
  Source: `menu/portal/MenuPortal.tsx:29-37`; `floating-ui-react/components/FloatingPortal.tsx:268`. Test: [L2] `MenuRoot.test.tsx` "renders root menu portal ownership without an accessibility role", "renders submenu portal ownership as an allowed menu child".
- **MNU-STR-19** — MUST. A Root determines its *parent kind* once from its ancestors: `'menu'` when it is a SubmenuRoot inside a menu; else `'menubar'` when inside a Menubar; else `'context-menu'` when inside a ContextMenu root with no enclosing menu; else none. The parent kind selects positioning defaults (§10), modality (MNU-STA-09), dismissal (§6.4), focus return (§6.3) and the instant-transition classification (§9).
  Source: `menu/root/MenuRoot.tsx:94-122`. Test: [L4] `MenuPositioner.test.tsx` "Menubar parent" (2 tests), "layout viewport" (4 tests).

## 3. Public API

Every part accepts `className`, `style` and `render` per [rendering-and-composition](../architecture/rendering-and-composition.md) (RND-API-01..03) unless stated otherwise; other native attributes are forwarded to the rendered element (RND-API-04). Consumer event handlers run before the part's own handler for the same event and may suppress it with `preventBaseUIHandler()` (RND-EVT-02, EVD-EVT-26).

### 3.1 `Menu.Root`

**Default element:** none (renders children only). **Accepts `render` element substitution:** no. **Accepts `className`/`style`:** no.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `defaultOpen` | `boolean` | `false` | Initial open state when `open` is not supplied. | MNU-API-01 |
| `open` | `boolean \| undefined` | `undefined` | Consumer-owned open state. | MNU-API-02 |
| `onOpenChange` | `(open, eventDetails: Menu.Root.ChangeEventDetails) => void` | — | Called when the menu wants to open or close (§7). | MNU-API-03 |
| `onOpenChangeComplete` | `(open: boolean) => void` | — | Called after the open/close animation finishes (§9). | MNU-API-04 |
| `onItemHighlighted` | `(item: HTMLElement \| undefined, eventDetails: Menu.Root.HighlightEventDetails) => void` | — | Called when the highlighted item changes (§7). | MNU-API-05 |
| `loopFocus` | `boolean` | `true` | Arrow keys wrap at the list ends. | MNU-API-06 |
| `highlightItemOnHover` | `boolean` | `true` | Moving the pointer over an item highlights it. | MNU-API-07 |
| `modal` | `boolean` | `true` | Scroll lock + outside pointer blocking while open (not for hover opens, ignored on nested menus). | MNU-API-08 |
| `orientation` | `'vertical' \| 'horizontal'` | `'vertical'` | Which arrow-key axis moves the highlight. | MNU-API-09 |
| `disabled` | `boolean` | `false` | Disables every interaction of the menu and its parts. | MNU-API-10 |
| `closeParentOnEsc` | `boolean` | `false` | On a submenu root (also accepted by `Menu.Root`, where it has no effect): Escape also closes the parent. | MNU-API-11 |
| `actionsRef` | `RefObject<Menu.Root.Actions \| null>` | — | Receives `{ unmount(), close() }`. | MNU-API-12 |
| `triggerId` / `defaultTriggerId` | `string \| null \| undefined` | `undefined` / `null` | Consumer-owned / initial active trigger id (PSH-API-06). | MNU-API-13 |
| `handle` | `Menu.Handle<Payload>` | — | Connects detached triggers and the imperative API. | MNU-API-14 |
| `children` | `ReactNode \| ({ payload }) => ReactNode` | — | Content, or a function receiving the active trigger's payload. | MNU-API-15 |

The outline lists no `className`/`style`/`render` for Root, which matches the source. Internal-only parameters (`isSubmenu`, `virtualFocus`, `virtualFocusRef`, `allowEscape`, `resetOnPointerLeave`) exist on `MenuRootInternal` and are set by `Menu.SubmenuRoot` and the filter roots; they are not public.

**Consumed (not forwarded):** every prop (there is no element).

- **MNU-API-01** — MUST. `defaultOpen` defaults to `false` and is the initial value of the component-owned open state; it is read once (EVD-STA-02).
  Source: `menu/root/MenuRoot.tsx:67,159`. Test: [L2] `MenuRoot.test.tsx` popupConformanceTests; `MenuPopupTransitionState.test.tsx` "starts each opening once (controlled=$controlled, keepMounted=$keepMounted, defaultOpen=$defaultOpen)" (defaultOpen=true cases). (L4 for the popup conformance suite clause)
- **MNU-API-02** — MUST. `open`, when not `undefined`, is the consumer-owned open state (EVD-STA-01/04): user interactions request a change through `onOpenChange` and nothing changes until the consumer renders the new value. A consumer that declines a close keeps the menu open with its trigger association intact (EVD-STA-10).
  Source: `menu/root/MenuRoot.tsx:63,160,178`; `utils/popups/store.ts` (PSH-STA-02). Test: [L2] `MenuTrigger.test.tsx` "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed"; `MenuRoot.test.tsx` "returns focus on Escape after a controlled root ignores a Shift+Tab close".
- **MNU-API-03** — MUST. `onOpenChange` is optional and cancelable (§7, MNU-EVT-01..03).
  Source: `menu/root/MenuRoot.tsx:372-376`. Test: [L2] `MenuRoot.test.tsx` "keeps focus in the menu when a Shift+Tab close is canceled".
- **MNU-API-04** — MUST. `onOpenChangeComplete` is optional; see MNU-ANI-06.
  Source: `menu/root/MenuRoot.tsx:181`; `menu/popup/MenuPopup.tsx:98-107`. Test: [L4] `MenuPopupTransitionState.test.tsx`; `MenuFilterRoot.test.tsx` "calls onOpenChangeComplete when the menu opens and closes".
- **MNU-API-05** — MUST. `onItemHighlighted` is optional; when absent no highlight reporting work is observable, and adding it later reports subsequent changes (MNU-EVT-10..13).
  Source: `menu/root/MenuRoot.tsx:570-618`. Test: [L2] `MenuRoot.test.tsx` "prop: onItemHighlighted" (7 tests).
- **MNU-API-06** — MUST. `loopFocus` defaults to `true` and is passed as the list-navigation `loopFocus` option (CMP-API-01); with `false`, arrow keys stop at the first/last item.
  Source: `menu/root/MenuRoot.tsx:70,523`. Test: [L2] `MenuRoot.test.tsx` popupListConformanceTests (`listEnd: 'wrap'`); `MenuList.test.tsx` "enters the list from the end with ArrowUp after a pointer open without looping"; `MenuFilterRoot.test.tsx` "stops parent navigation at the boundary when loopFocus is disabled". (L4 for the arrow-key stopping clause)
- **MNU-API-07** — MUST. `highlightItemOnHover` defaults to `true`; with `false`, pointer movement over items and submenu triggers neither highlights nor focuses them (MNU-PTR-12).
  Source: `menu/root/MenuRoot.tsx:77,165,219,544`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:154-156`. Test: [L4] `MenuRoot.test.tsx` "prop: highlightItemOnHover" (3 tests).
- **MNU-API-08** — MUST. `modal` defaults to `true`. It is honored only on a top-level menu (parent kind none or `context-menu`); a nested menu ignores it (and warns, MNU-EDGE-02). Its effects are the internal backdrop (MNU-STR-17) and scroll lock (MNU-DIS-09); the modal focus management of a context menu does not depend on it (MNU-FOC-13).
  Source: `menu/root/MenuRoot.tsx:69,166,207-213,220`; `menu/store/MenuStore.ts:82-84`. Test: [L2] `MenuRoot.test.tsx` "prop: modal" (2 tests), "warns that nested menus ignore the modal prop". (L4 for the scroll-lock clause)
- **MNU-API-09** — MUST. `orientation` defaults to `'vertical'`; it selects the main navigation axis (CMP-API-02), the cross-axis submenu open/close keys (§6.1), and the `aria-orientation` attribute (MNU-ARIA-04).
  Source: `menu/root/MenuRoot.tsx:71,529-532,723`. Test: [L2] `MenuRoot.test.tsx` "sets aria-orientation on a horizontal popup", "opens a nested menu of a $orientation $direction menu with $openKey key and closes it with $closeKey". (L4 for the cross-axis submenu key clause)
- **MNU-API-10** — MUST. `disabled` defaults to `false`. When `true`: the trigger is disabled (MNU-API-16), outside/Escape dismissal, list navigation and typeahead are disabled, every item (Item, CheckboxItem, RadioItem, SubmenuTrigger, LinkItem excepted) is disabled and carries `data-disabled`, item clicks neither activate nor close, and filter controls are disabled. In a submenu the effective value is the submenu's own `disabled` OR its parent's (through the submenu trigger).
  Source: `menu/root/MenuRoot.tsx:68,164,218,502,517,557`; `menu/item/MenuItem.tsx:32,37`; `menu/checkbox-item/MenuCheckboxItem.tsx:41,46`; `menu/radio-item/MenuRadioItem.tsx:47,49`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:129-132`; `menu/trigger/MenuTrigger.tsx:112-114`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:108`. Test: [L4] `MenuRoot.test.tsx` "prop: disabled" (4 tests); `MenuFilterRoot.test.tsx` "disables filter controls when the root is disabled".
- **MNU-API-11** — MUST. `closeParentOnEsc` defaults to `false`. It takes effect only when the parent kind is `'menu'` (i.e. on a submenu): with `true`, the Escape close bubbles so the parent menu closes too (MNU-DIS-03); with `false`, only the submenu closes and focus returns to its trigger.
  Source: `menu/root/MenuRoot.tsx:73,503`. Test: [L4] `MenuRoot.test.tsx` "prop: closeParentOnEsc" (2 tests).
- **MNU-API-12** — MUST. `actionsRef.current` is `{ unmount, close }`: `close()` requests a close with reason `imperative-action` and no trigger; `unmount()` ends the closing phase after `preventUnmountOnClose()` was used (PSH-API-09, TRN-EVT-08).
  Source: `menu/root/MenuRoot.tsx:478-486`. Test: [L2] `MenuRoot.test.tsx` "unmounts the menu when the `unmount` method is called", "does not leave a tabbable item after tabbing out of a keepMounted menu before close".
- **MNU-API-13** — MUST. `triggerId` (consumer-owned) and `defaultTriggerId` (initial, default `null`) select the active trigger among several `Menu.Trigger`s by their `id` (PSH-API-06, PSH-STA-03); `onOpenChange` details carry the trigger element so a controlled consumer can keep `triggerId` in sync.
  Source: `menu/root/MenuRoot.tsx:75-76,161-162,179`. Docs: `components/menu/page.mdx` §Controlled mode with multiple triggers. Test: [L2] `MenuRoot.test.tsx` "returns focus to the opener when a menu is opened programmatically"; `MenuRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant when a controlled close commits late".
- **MNU-API-14** — MUST. `handle` attaches the Root's store to a `Menu.Handle` (PSH-STR-03/04) so detached `Menu.Trigger`s with the same handle and the imperative `open`/`close`/`isOpen` members work; a Root remounted with the same handle starts closed (PSH-STA-40).
  Source: `menu/root/MenuRoot.tsx:74,749`; `menu/store/MenuHandle.ts:13-48`. Docs: `CHANGELOG.md v1.7.0 "Prevent <Menu.Root> from reopening after remounting with a reused handle"`. Test: [L4] `MenuRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests (`openInteractions: ['click']`, `ariaExpanded: true`, `throwOnMissingTrigger: true`).
- **MNU-API-15** — MUST. When `children` is a function it is called during every render with `{ payload }`, where `payload` is the active trigger's `payload` (or `undefined`) (PSH-API-07).
  Source: `menu/root/MenuRoot.tsx:196,745`. Test: [L2] `MenuViewport.test.tsx` viewportConformanceTests; `MenuRoot.detached-triggers.test.tsx` "multiple detached triggers". (L4 for the viewport suite clause)

#### State object (`Menu.Root.State`)

Empty (`{}`); the Root renders nothing. Source: `menu/root/MenuRoot.tsx:785`.

#### Data attributes / CSS variables

None.

### 3.2 `Menu.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Ignore interaction. | MNU-API-16 |
| `nativeButton` | `boolean` | `true` | Whether the rendered element is a native `<button>` (UBT-API-02). | MNU-API-17 |
| `id` | `string` | generated | Element id and trigger id (PSH-API-08). | MNU-API-18 |
| `openOnHover` | `boolean` | `undefined` (→ `false`) | Also open on hover. | MNU-API-19 |
| `delay` | `number` | `100` | Pointer rest time (ms) before a hover open. | MNU-API-20 |
| `closeDelay` | `number` | `0` | Delay (ms) before a hover-opened menu closes on leave. | MNU-API-21 |
| `handle` | `Menu.Handle<Payload>` | — | Store to attach to when rendered outside a Root. | MNU-API-22 |
| `payload` | `Payload` | — | Value exposed to the Root's function child while this trigger is active. | MNU-API-23 |
| `children` | `ReactNode` | — | | — |

**Consumed (not forwarded):** `disabled`, `nativeButton`, `id` (re-emitted as the resolved id), `openOnHover`, `delay`, `closeDelay`, `handle`, `payload`.

- **MNU-API-16** — MUST. The effective disabled state is `disabled || root.disabled || (menubar.disabled when inside a Menubar)`. A disabled trigger renders the native `disabled` attribute (via UBT-DOM-06), receives no hover/click/focus open interactions, and does not open the menu when clicked.
  Source: `menu/trigger/MenuTrigger.tsx:55,112-119,177-206`. Test: [L2] `MenuTrigger.test.tsx` "prop: disabled" (2 tests). (L4 for the hover and focus open clauses)
- **MNU-API-17** — MUST. `nativeButton` defaults to `true` and is the button mechanism's `native` option; with `false` the element gets `role="button"` and keyboard activation emulation (UBT-DOM-02, UBT-KEY-01/02).
  Source: `menu/trigger/MenuTrigger.tsx:56,116-119`. Test: [L2] `MenuTrigger.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`), "opens the menu when pressing $key on a $buttonType button", "has a role prop inside a Popover when not a native button". (L4 for the keyboard-activation emulation clause)
- **MNU-API-18** — MUST. The trigger id is the `id` prop or a generated id (RND-DOM-27); it is rendered as the element `id`, registered in the store's trigger registry (PSH-STA-26) and compared with `triggerId`/`defaultTriggerId`. The rendered element's own `id` (from a `render` element) wins for labelling (MNU-ARIA-19).
  Source: `menu/trigger/MenuTrigger.tsx:57,75,95-108,242`. Test: [L2] `MenuRoot.test.tsx` "keeps the root menu open when a submenu opens and the trigger `render` element has a custom id"; `MenuFilterRoot.test.tsx` "uses the id from a custom trigger render element for popup labelling".
- **MNU-API-19** — MUST. `openOnHover` defaults to `undefined`, which resolves to `false` outside a Menubar and to "the menubar has a submenu open" inside one. When effectively `true` (and not disabled), the hover interaction of MNU-PTR-05..08 is enabled.
  Source: `menu/trigger/MenuTrigger.tsx:58,173-190`. Docs: `components/menu/page.mdx` §Open on hover. Test: [L4] `MenuRoot.test.tsx` "prop: openOnHover" (8 tests); `MenuTrigger.test.tsx` "impatient clicks with `openOnHover=true`" (5 tests).
- **MNU-API-20** — MUST. `delay` defaults to `100` and is the hover *rest* time (MNU-PTR-05): the menu opens once the pointer has stayed over the trigger, without moving more than ~1 px, for `delay` ms. `delay={0}` opens on pointer enter. Applies only to top-level menus (`restMs` is `undefined` inside a Menubar).
  Source: `menu/trigger/MenuTrigger.tsx:59,184`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:224-300,454-497`. Test: [L4] `MenuTrigger.test.tsx` "sticks when clicked before the hover delay completes", "should keep the menu open when re-hovered and clicked within the patient threshold"; `MenuRoot.test.tsx` "reopens on hover after an impatient click closes via item press".
- **MNU-API-21** — MUST. `closeDelay` defaults to `0` and is the hover close delay (MNU-PTR-07): leaving a hover-opened menu (trigger and popup) closes after `closeDelay` ms; `0` closes immediately.
  Source: `menu/trigger/MenuTrigger.tsx:60,185`; `menu/popup/MenuPopup.tsx:123-127`. Test: [L4] `MenuRoot.test.tsx` "should close after delay".
- **MNU-API-22** — MUST. `handle`, when given, selects the store exposed by the handle (PSH-STR-02); the trigger then works whether it is rendered before, after, inside or outside the Root (PSH-STR-05).
  Source: `menu/trigger/MenuTrigger.tsx:61,66-68`. Test: [L4] `MenuRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests; `MenuRoot.test.tsx` every "when using detached triggers" test.
- **MNU-API-23** — MUST. `payload` is forwarded to the store when this trigger becomes/is the active trigger and is read by the Root's function child; it persists after close until another trigger activates (PSH-STA-30/32).
  Source: `menu/trigger/MenuTrigger.tsx:62,95-108`. Test: [L2] `MenuRoot.detached-triggers.test.tsx` "multiple detached triggers" (3 tests); `MenuViewport.test.tsx` viewportConformanceTests. (L4 for the viewport suite clause)

#### State object (`Menu.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | The menu is open **and** this trigger is its active trigger (`isOpenedByTrigger`, PSH-STA-08). |
| `disabled` | `boolean` | Effective disabled state (MNU-API-16). |

Source: `menu/trigger/MenuTrigger.tsx:229-232,354-363`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open` | `open === true`; empty | MNU-DOM-01 | C# |
| `data-pressed` | `open === true`; empty | MNU-DOM-01 | C# |
| `data-disabled` | `disabled === true`; empty | MNU-DOM-02 | C# |

#### CSS variables

None.

### 3.3 `Menu.Portal`

**Default element:** `<div>` (the portal node). **Accepts `render` element substitution:** yes (through the shared portal). **Accepts `className`/`style`:** yes (string/object; the state is `{}`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the subtree in the DOM while closed. | MNU-API-24 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject \| null` | `document.body` | Where the portal node is appended. | MNU-API-25 |

- **MNU-API-24** — MUST. `keepMounted` defaults to `false`; the Portal (and everything below it) renders iff `mounted || keepMounted` (TRN-ANI-01). The value is also published to the Positioner so positioning can account for a kept-mounted closed popup.
  Source: `menu/portal/MenuPortal.tsx:19-27,35`; `menu/positioner/MenuPositioner.tsx:58,120`. Test: [L2] `MenuPositioner.test.tsx` "prop: keepMounted" (2 tests); `MenuPositioner.spec.tsx` (`keepMounted` is not a Positioner prop).
- **MNU-API-25** — MUST. `container` selects the parent of the portal node (PSH-STR-11); a shadow root is accepted, and submenus portaled into shadow roots keep their focus-guard and keyboard behavior.
  Source: `menu/portal/MenuPortal.tsx:50-54`. Test: [L4] `MenuSubmenuTrigger.test.tsx` "closes a submenu when its focus guard is in a shadow-root portal"; `MenuFilterSubmenuRoot.test.tsx` "moves a filterable parent on when a submenu portaled into a shadow root closes".

State object: `{}`. Data attributes: none (the portal node carries the shared `data-base-ui-portal` marker, MNU-DOM-26).

### 3.4 `Menu.Backdrop`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Backdrop.State`).

No own props beyond `className`/`style`/`render` and forwarded attributes.

#### State object (`Menu.Backdrop.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `transitionStatus` | `TransitionStatus` | Root popup transition status (§9). |

Source: `menu/backdrop/MenuBackdrop.tsx:31-34,57-66`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | MNU-DOM-10 | C# |
| `data-starting-style` / `data-ending-style` | `transitionStatus` is `'starting'` / `'ending'` | MNU-DOM-10 | JS |

### 3.5 `Menu.Positioner`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Positioner.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `side` | `Side` | `'bottom'` (submenu / vertical menubar: `'inline-end'`) | Preferred side. | MNU-API-26 |
| `align` | `Align` | `'center'` (submenu, menubar, context menu: `'start'`) | Alignment along the side. | MNU-API-26 |
| `sideOffset` | `number \| fn` | `0` (context menu: `-5` when no `side` and `align !== 'center'`) | Gap from the anchor. | MNU-API-27 |
| `alignOffset` | `number \| fn` | `0` (context menu: `2` under the same condition) | Shift along the side. | MNU-API-27 |
| `anchor` | `Element \| VirtualElement \| RefObject \| () => … \| null` | active trigger element (context menu: the pointer position) | Positioning reference. | MNU-API-30 |
| `collisionBoundary` | `Boundary` | `'clipping-ancestors'` | | MNU-API-28 |
| `collisionPadding` | `Padding` | `5` | | MNU-API-28 |
| `arrowPadding` | `number` | `5` (context menu: `0`) | | MNU-API-28 |
| `sticky` | `boolean` | `false` | | MNU-API-28 |
| `disableAnchorTracking` | `boolean` | `false` | | MNU-API-28 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` (context menu: `'fixed'`) | | MNU-API-28 |
| `collisionAvoidance` | `CollisionAvoidance` | `{ fallbackAxisSide: 'none' }` (submenu: `{ fallbackAxisSide: 'end' }`) | | MNU-API-29 |

**Consumed (not forwarded):** all of the above.

- **MNU-API-26** — MUST. `side` defaults to `'bottom'` and `align` to `'center'` for a top-level menu; a submenu defaults to `side: 'inline-end'`, `align: 'start'`; a menu inside a horizontal Menubar to `'bottom'`/`'start'`, inside a vertical Menubar to `'inline-end'`/`'start'`; a context menu to `align: 'start'`. Explicit props always win.
  Source: `menu/positioner/MenuPositioner.tsx:43-44,83-102`; `internals/useAnchorPositioning.ts:145-147`. Test: [L2] `MenuPositioner.test.tsx` "Menubar parent" (2 tests), "preserves explicit context-menu placement and offsets". (L4 for the resolved-placement tests)
- **MNU-API-27** — MUST. `sideOffset` and `alignOffset` default to `0`; under a context menu with no `side` prop and an align other than `'center'`, the defaults become `sideOffset: -5` and `alignOffset: 2` (explicit values still win).
  Source: `menu/positioner/MenuPositioner.tsx:45-46,83-90`. Test: [L2] `MenuPositioner.test.tsx` "preserves explicit context-menu placement and offsets". (L4 for the resolved-placement tests)
- **MNU-API-28** — MUST. `collisionBoundary` defaults to `'clipping-ancestors'`, `collisionPadding` to `5`, `arrowPadding` to `5` (forced to `0` under a context menu), `sticky` to `false`, `disableAnchorTracking` to `false`, `positionMethod` to `'absolute'` (forced to `'fixed'` under a context menu); all are passed through to the shared anchor positioning (FLT).
  Source: `menu/positioner/MenuPositioner.tsx:40,47-51,106-132`. Test: [L2] `MenuPositioner.test.tsx` positionerConformanceTests (`viewport: true`). (L4 for the positioner suite clause)
- **MNU-API-29** — MUST. `collisionAvoidance` defaults to `{ fallbackAxisSide: 'none' }` (dropdown behavior: never flips to the other axis) for a top-level menu and to `{ fallbackAxisSide: 'end' }` for a submenu; an explicit value wins in both cases.
  Source: `menu/positioner/MenuPositioner.tsx:52,94-97`; `internals/constants.ts:18-28`. Docs: `CHANGELOG.md v1.2.0 "Allow side axis fallback for submenus by default"`. Test: none.
- **MNU-API-30** — MUST. `anchor` overrides the positioning reference (element, virtual element with `getBoundingClientRect`, DOM handle, or a function returning one of these, re-read every render); when absent the active trigger element is the anchor, and a menu with no trigger at all MAY supply an external anchor (e.g. `document.body`) and still support submenus and focus return. Under a context menu the context's pointer anchor is used when none is given.
  Source: `menu/positioner/MenuPositioner.tsx:39,78-84,106-108`. Test: [L4] `MenuPositioner.test.tsx` "prop: anchor" (6 tests); `MenuRoot.triggerless.test.tsx` "supports an external anchor across reopen, keepMounted=%s".

#### State object (`Menu.Positioner.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Resolved side after collision handling. |
| `align` | `Align` | Resolved alignment. |
| `anchorHidden` | `boolean` | Anchor is clipped/hidden. |
| `nested` | `boolean` | Parent kind is `'menu'`. |
| `instant` | `string \| undefined` | Current instant type (§9). |

Source: `menu/positioner/MenuPositioner.tsx:236-243,304-329`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | MNU-DOM-04 | C# |
| `data-anchor-hidden` | `anchorHidden` | MNU-DOM-04 | JS |
| `data-side` | resolved side (`top`/`bottom`/`left`/`right`/`inline-start`/`inline-end`) | MNU-DOM-04 | C# → JS |
| `data-align` | `start`/`center`/`end` | MNU-DOM-04 | C# → JS |
| `data-nested` | `nested === true` (not in outline, §16 Q3) | MNU-DOM-04 | C# |
| `data-instant` | `instant` value (not in outline, §16 Q3) | MNU-DOM-04 | C# |
| `hidden` | not mounted | MNU-DOM-05 | C# |
| `inert` | not open | MNU-DOM-05 | C# |

#### CSS variables

| Variable | Set on | Value | Req | Writer |
|---|---|---|---|---|
| `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`, `--positioner-width`, `--positioner-height` | Positioner inline style | per FLT (`CommonPositionerCssVars`) | MNU-POS-08 | C# → JS |

### 3.6 `Menu.Popup`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Popup.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `finalFocus` | `boolean \| RefObject \| (closeType: InteractionType) => boolean \| HTMLElement \| null \| void` | internal default (§6.3) | Element to focus on close. | MNU-API-31 |
| `id` | `string` | generated | Popup id (`@ignore` in the outline). | MNU-API-32 |
| `children` | `ReactNode` | — | | — |

- **MNU-API-31** — MUST. `finalFocus` overrides the close focus target (MNU-FOC-09): `false` leaves focus alone, `true` uses the default, a DOM handle focuses that element, and a function is called with the interaction type that closed the menu (`'mouse'`, `'touch'`, `'pen'` or `'keyboard'`) and may return an element, `true`, or a falsy value to do nothing.
  Source: `menu/popup/MenuPopup.tsx:43,199-201,250-263`. Test: [L4] `MenuPopup.test.tsx` popupFocusPropsTests (`initialFocus: false`), "prop: finalFocus" (2 tests).
- **MNU-API-32** — MUST. The popup id is the `id` prop, else the `render` element's `id`, else a generated id (RND-DOM-32). An explicitly empty id (`""`) is rendered as-is and then nothing points at the popup (`aria-controls` falls back to nothing, PSH-STA-04). Removing a custom id falls back to the generated id without a gap.
  Source: `menu/popup/MenuPopup.tsx:72-77,158`; `menu/root/MenuRoot.tsx:127-133`. Test: [L2] `MenuFilterRoot.test.tsx` "releases a popup id override when the prop is removed".

#### State object (`Menu.Popup.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `transitionStatus` | `TransitionStatus` | `'starting'`, `'ending'`, `'idle'`, `undefined` (TRN-STA-14). |
| `side` | `Side` | From the Positioner. |
| `align` | `Align` | From the Positioner. |
| `nested` | `boolean` | Parent kind is `'menu'`. |
| `instant` | `'dismiss' \| 'click' \| 'group' \| 'trigger-change' \| undefined` | Instant type (§9). |

Source: `menu/popup/MenuPopup.tsx:141-148,265-290`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | MNU-DOM-06 | C# |
| `data-starting-style` / `data-ending-style` | transition status | MNU-DOM-06 | JS |
| `data-side`, `data-align` | resolved side / align | MNU-DOM-06 | C# |
| `data-instant` | instant type | MNU-DOM-06 | C# |
| `data-nested` | nested (not in outline, §16 Q3) | MNU-DOM-06 | C# |
| `data-rootownerid` | always: the Root's generated id | MNU-DOM-07 | C# |
| `data-base-ui-focusable` | always | MNU-DOM-08 | C# |

CSS variables: none.

### 3.7 `Menu.Viewport`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Viewport.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | — | Content rendered inside the transition container. | MNU-API-33 |

- **MNU-API-33** — MUST. `Menu.Viewport` wraps its children in the shared popup viewport (PSH-STR-07): a `<div data-current>` child, plus a `<div data-previous>` snapshot of the outgoing content (carrying `--popup-width`/`--popup-height`) while a trigger switch transition runs. It is optional and intended for menus with several triggers and differing content.
  Source: `menu/viewport/MenuViewport.tsx:28-45`; `utils/usePopupViewport.tsx`. Docs: `components/menu/page.mdx` §Viewport. Test: [L4] `MenuViewport.test.tsx` viewportConformanceTests (`openOn: 'click'`); `MenuFilterRoot.test.tsx` "keeps real focus on the input while the cursor moves through the list" (with a Viewport).

#### State object (`Menu.Viewport.State`)

| Field | Type | Meaning |
|---|---|---|
| `activationDirection` | `string \| undefined` | `"<left\|right\|''> <down\|up\|''>"` tokens of the last trigger switch. |
| `transitioning` | `boolean` | A content transition is running. |
| `instant` | instant type | As Popup. |

Source: `menu/viewport/MenuViewport.tsx:34-38,48-61`.

#### Data attributes / CSS variables

| Attribute / variable | Where | Req | Writer |
|---|---|---|---|
| `data-activation-direction`, `data-transitioning`, `data-instant` | Viewport element | MNU-DOM-12 | C# |
| `data-current`, `data-previous` | child containers | MNU-DOM-12 | C# |
| `--popup-width`, `--popup-height` | `data-previous` container | MNU-DOM-12 | C# |

### 3.8 `Menu.Arrow`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Arrow.State`). No own props.

#### State object (`Menu.Arrow.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | |
| `side` | `Side` | |
| `align` | `Align` | |
| `uncentered` | `boolean` | The arrow could not be centered on the anchor. |

Source: `menu/arrow/MenuArrow.tsx:26-31,45-62`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | | MNU-DOM-11 | C# |
| `data-side`, `data-align` | | MNU-DOM-11 | C# → JS |
| `data-uncentered` | `uncentered === true` | MNU-DOM-11 | JS |

### 3.9 `Menu.Item`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Item.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `closeOnClick` | `boolean` | `true` | Close the menu (whole tree) when activated. | MNU-API-34 |
| `disabled` | `boolean` | `false` | Ignore interaction (still focusable). | MNU-API-35 |
| `label` | `string` | rendered text | Text for typeahead and filtering. | MNU-API-36 |
| `nativeButton` | `boolean` | `false` | Rendered element is a native `<button>`. | MNU-API-37 |
| `id` | `string` | generated | Element id (`@ignore` in the outline). | MNU-API-38 |
| `onClick` | `MouseEventHandler` | — | Activation handler. | MNU-API-39 |

**Consumed (not forwarded):** `closeOnClick`, `disabled`, `label`, `nativeButton`, `id` (re-emitted).

- **MNU-API-34** — MUST. `closeOnClick` defaults to `true` on `Menu.Item` and `false` on `Menu.LinkItem`, `Menu.CheckboxItem` and `Menu.RadioItem`. When `true`, a click on the item (including the synthetic click of keyboard activation and of drag-release, MNU-PTR-03) requests a close of the menu *and every ancestor menu* with reason `item-press` and the click's native event (MNU-DIS-06). When `false`, the click leaves the menu open.
  Source: `menu/item/MenuItem.tsx:22`; `menu/link-item/MenuLinkItem.tsx:23`; `menu/checkbox-item/MenuCheckboxItem.tsx:29`; `menu/radio-item/MenuRadioItem.tsx:28`; `menu/item/useMenuItemCommonProps.ts:102-106`. Test: [L2] `MenuItem.test.tsx` "prop: closeOnClick" (2 tests); `MenuCheckboxItem.test.tsx` / `MenuRadioItem.test.tsx` "prop: closeOnClick" (2 tests each); `MenuRoot.detached-triggers.test.tsx` "opens a submenu on click when hover is disabled" (nested item click closes the whole tree).
- **MNU-API-35** — MUST. `disabled` defaults to `false`; the effective value is `disabled || root.disabled` (RadioItem: also `|| radioGroup.disabled`). A disabled item stays focusable and reachable with the arrow keys (`focusableWhenDisabled`, UBT-FOC-05), renders `aria-disabled="true"` and `data-disabled`, and ignores clicks, Enter and Space (its consumer `onClick`/`onKeyDown`/`onKeyUp` are not called, UBT-KEY-19, UBT-PTR-01). `Menu.LinkItem` has no `disabled` prop.
  Source: `menu/item/MenuItem.tsx:21,37`; `menu/item/useMenuItem.ts:29-34`; `menu/radio-item/MenuRadioItem.tsx:49`. Test: [L2] `MenuItem.test.tsx` "can be focused but not interacted with when disabled"; `MenuRoot.test.tsx` "includes disabled items during keyboard navigation"; `MenuRadioItem.test.tsx` "can be focused but not interacted with when individual items are disabled". (L4 for the arrow-key reachability clause)
- **MNU-API-36** — MUST. `label`, when given, is the string used for typeahead matching (CMP-STA-06) and filter matching (MNU-STA-17) instead of the element's text content.
  Source: `menu/item/MenuItem.tsx:19,29`; `menu/link-item/MenuLinkItem.tsx:32`; `menu/checkbox-item/MenuCheckboxItem.tsx:38`; `menu/radio-item/MenuRadioItem.tsx:41`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:58`; `filter-dropdown/item/useFilterDropdownItem.ts:101-104`. Test: [L2] `MenuRoot.test.tsx` "changes the highlighted item using text navigation on label prop"; `MenuSubmenuTrigger.test.tsx` "uses the label prop for text navigation"; `MenuFilterRoot.test.tsx` "prefers the label prop over rendered text for matching", "matches a submenu trigger on its label". (L4 for the typeahead clause)
- **MNU-API-37** — MUST. `nativeButton` defaults to `false` on every item part (Item, CheckboxItem, RadioItem, SubmenuTrigger; UBT-API-03); `Menu.LinkItem` always uses non-native mode (UBT-API-04). The button mechanism runs in composite mode (`composite: true`), so no `tabindex` comes from it (UBT-DOM-05) and Space activates on keydown (UBT-KEY-12).
  Source: `menu/item/MenuItem.tsx:20`; `menu/item/useMenuItem.ts:29-34`; `menu/link-item/MenuLinkItem.tsx:41-44`. Test: [L2] `MenuItem.test.tsx` describeConformance (`button: true`); `MenuRoot.test.tsx` "skips natively disabled items in text navigation" (`nativeButton` + `render={<button disabled/>}`).
- **MNU-API-38** — MUST. The item id is the `id` prop or a generated id, rendered as the element `id`; it is what `aria-activedescendant` references in filterable menus (MNU-ARIA-14).
  Source: `menu/item/MenuItem.tsx:30`; `menu/item/useMenuItemCommonProps.ts:79`. Test: [L2] `MenuList.test.tsx` "reads an item id when navigating to it".
- **MNU-API-39** — MUST. `onClick` is called once per activation (pointer click, Enter/Space synthetic click, drag-release synthetic click, filter-input Enter) before the item's own close handling; calling `preventBaseUIHandler()` in it suppresses the close.
  Source: `menu/item/MenuItem.tsx:57`; `menu/item/useMenuItemCommonProps.ts:102-106`. Test: [L2] `MenuItem.test.tsx` "calls the onClick handler when clicked", "does not close the menu when onClick prevents Base UI handler", "allows onMouseDown to call preventBaseUIHandler". (L4 for the Enter/Space and drag-release activation clauses)

#### State object (`Menu.Item.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled state. |
| `highlighted` | `boolean` | This item is at the Root's active index. |

Source: `menu/item/MenuItem.tsx:50-53,81-90`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-highlighted` | highlighted | MNU-DOM-13 | C# |
| `data-disabled` | disabled | MNU-DOM-13 | C# |
| `tabindex` | `0` when open, highlighted and not virtual focus; otherwise `-1` | MNU-DOM-14 | C# |

### 3.10 `Menu.LinkItem`

**Default element:** `<a>`. **Accepts `render`:** yes (the callback receives the native `<a>` props, e.g. `href`). **Accepts `className`/`style` as function of state:** yes (`Menu.LinkItem.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `closeOnClick` | `boolean` | `false` | | MNU-API-34 |
| `label` | `string` | rendered text | | MNU-API-36 |
| `id` | `string` | generated | | MNU-API-38 |
| native `<a>` attributes (`href`, …) | | | Forwarded. | MNU-API-40 |

- **MNU-API-40** — MUST. `Menu.LinkItem` exposes no `disabled` or `nativeButton` prop; it is always non-native-button mode on an anchor, so Enter triggers the browser's own link activation (UBT-KEY-10) and Space dispatches a synthetic click (UBT-KEY-11/12) — both navigate — except while a typeahead session is in progress (MNU-KEY-13).
  Source: `menu/link-item/MenuLinkItem.tsx:41-44,97-116`. Test: [L4] `MenuLinkItem.test.tsx` "react-router <Link> activates with Enter and Space", "does not navigate when Space is pressed during an active typeahead session"; `MenuLinkItem.spec.tsx`.

State object (`Menu.LinkItem.State`): `{ highlighted: boolean }`. Source: `menu/link-item/MenuLinkItem.tsx:61,90-95`. Data attributes: `data-highlighted` (MNU-DOM-15); `tabindex` per MNU-DOM-14.

### 3.11 `Menu.CheckboxItem`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.CheckboxItem.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `checked` | `boolean \| undefined` | `undefined` | Consumer-owned checked state. | MNU-API-41 |
| `defaultChecked` | `boolean` | `false` | Initial component-owned state. | MNU-API-41 |
| `onCheckedChange` | `(checked: boolean, eventDetails: Menu.CheckboxItem.ChangeEventDetails) => void` | — | Cancelable. | MNU-API-42 |
| `closeOnClick` | `boolean` | `false` | | MNU-API-34 |
| `disabled` | `boolean` | `false` | | MNU-API-35 |
| `label` | `string` | rendered text | | MNU-API-36 |
| `nativeButton` | `boolean` | `false` | | MNU-API-37 |
| `id` | `string` | generated | | MNU-API-38 |
| `onClick` | `MouseEventHandler` | — | | MNU-API-39 |

- **MNU-API-41** — MUST. `checked`/`defaultChecked` form a controlled/uncontrolled pair (UTL-STA-01..05) owned *above* the rendered element, so an uncontrolled item keeps its state while it is hidden by filtering or while the popup is closed and kept mounted.
  Source: `menu/checkbox-item/MenuCheckboxItem.tsx:107-116,129-141`. Test: [L2] `MenuCheckboxItem.test.tsx` "adds the state and ARIA attributes when checked=$checked", "keeps the state when closed and reopened"; `MenuFilterRoot.test.tsx` "preserves uncontrolled checkbox state while the item is filtered out", "tracks a controlled checkbox item".
- **MNU-API-42** — MUST. Each activation calls `onCheckedChange(!checked, details)` with `reason: 'item-press'` and the click's native event; `details.cancel()` keeps the current state. The details object also exposes a no-op `preventUnmountOnClose()` (the type is `Menu.Root.ChangeEventDetails`).
  Source: `menu/checkbox-item/MenuCheckboxItem.tsx:68-74,119-127`. Test: [L2] `MenuCheckboxItem.test.tsx` "calls `onCheckedChange` when the item is clicked", "does not toggle when `onCheckedChange` cancels the event"; `MenuFilterRoot.test.tsx` "leaves a checkbox item unchecked when onCheckedChange is canceled".

#### State object (`Menu.CheckboxItem.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled state. |
| `highlighted` | `boolean` | |
| `checked` | `boolean` | Current checked state. |

Source: `menu/checkbox-item/MenuCheckboxItem.tsx:59-67,143-156`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-checked` / `data-unchecked` | checked / not checked (exactly one is present) | MNU-DOM-16 | C# |
| `data-disabled`, `data-highlighted` | | MNU-DOM-16 | C# |

### 3.12 `Menu.CheckboxItemIndicator` and 3.13 `Menu.RadioItemIndicator`

**Default element:** `<span>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while not checked. | MNU-API-43 |

- **MNU-API-43** — MUST. `keepMounted` defaults to `false`; the indicator element is rendered iff `keepMounted || mounted`, where `mounted` follows the parent item's `checked` through the shared transition-status machine (MNU-ANI-13).
  Source: `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:21,27-39,56`; `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:21,27-39,56`. Test: [L2] `MenuCheckboxItemIndicator.test.tsx` / `MenuRadioItemIndicator.test.tsx` (3 tests each + describeConformance with `keepMounted`). (L4 for the exit-transition clause)

#### State object (`Menu.CheckboxItemIndicator.State` / `Menu.RadioItemIndicator.State`)

| Field | Type | Meaning |
|---|---|---|
| `checked` | `boolean` | Parent item checked. |
| `disabled` | `boolean` | Parent item disabled. |
| `highlighted` | `boolean` | Parent item highlighted. |
| `transitionStatus` | `TransitionStatus` | Indicator's own status. |

Source: `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:41-46`; `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:41-46`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-checked` / `data-unchecked` | | MNU-DOM-17 | C# |
| `data-disabled`, `data-highlighted` | | MNU-DOM-17 | C# |
| `data-starting-style` / `data-ending-style` | | MNU-DOM-17 | JS |

### 3.14 `Menu.RadioGroup`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.RadioGroup.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` | `undefined` | Consumer-owned selected value. | MNU-API-44 |
| `defaultValue` | `any` | `undefined` | Initial value. | MNU-API-44 |
| `onValueChange` | `(value: any, eventDetails: Menu.RadioGroup.ChangeEventDetails) => void` | — | Cancelable. | MNU-API-45 |
| `disabled` | `boolean` | `false` | Disables every radio item in the group. | MNU-API-46 |
| `children` | `ReactNode` | — | | — |

- **MNU-API-44** — MUST. `value`/`defaultValue` form a controlled/uncontrolled pair (UTL-STA-01..05, diagnostic name `MenuRadioGroup`); a radio item is checked iff `group.value === item.value` (strict equality).
  Source: `menu/radio-group/MenuRadioGroup.tsx:33-37`; `menu/radio-item/MenuRadioItem.tsx:50`. Test: [L2] `MenuRadioItem.test.tsx` "adds the state and ARIA attributes when selected", "keeps the state when closed and reopened".
- **MNU-API-45** — MUST. Activating a radio item calls `onValueChange(item.value, details)` with `reason: 'item-press'`; `details.cancel()` keeps the current value. It is called even when the activated item is already selected (EVD-STA-09).
  Source: `menu/radio-group/MenuRadioGroup.tsx:39-49`; `menu/radio-item/MenuRadioItem.tsx:72-78`. Test: [L2] `MenuRadioItem.test.tsx` "calls `onValueChange` when the item is clicked", "does not select when `onValueChange` cancels the event".
- **MNU-API-46** — MUST. `disabled` defaults to `false`; when `true` every `Menu.RadioItem` in the group is disabled (MNU-API-35) and the group renders `aria-disabled="true"`.
  Source: `menu/radio-group/MenuRadioGroup.tsx:25,59`; `menu/radio-item/MenuRadioItem.tsx:38,49`. Test: [L2] `MenuRadioItem.test.tsx` "can be focused but not interacted with when a radio group is disabled".

State object (`Menu.RadioGroup.State`): `{ disabled: boolean }` (default projection → `data-disabled`; not in outline, §16 Q3). Source: `menu/radio-group/MenuRadioGroup.tsx:51,126-131`.

### 3.15 `Menu.RadioItem`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.RadioItem.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` (required) | — | Value set on the group when selected. | MNU-API-47 |
| `closeOnClick` | `boolean` | `false` | | MNU-API-34 |
| `disabled` | `boolean` | `false` | | MNU-API-35 |
| `label`, `nativeButton`, `id`, `onClick` | | | As `Menu.Item`. | MNU-API-36..39 |

- **MNU-API-47** — MUST. `value` is required and is passed verbatim to the group's `onValueChange` / stored as the group value on activation.
  Source: `menu/radio-item/MenuRadioItem.tsx:29,72-78,135-139`. Test: [L2] `MenuRadioItem.test.tsx` "calls `onValueChange` when the item is clicked" (`value={1}` → `1`).

State object (`Menu.RadioItem.State`): `{ disabled, highlighted, checked }` as CheckboxItem. Source: `menu/radio-item/MenuRadioItem.tsx:63-70,118-131`. Data attributes: as CheckboxItem (MNU-DOM-16).

### 3.16 `Menu.Group` and 3.17 `Menu.GroupLabel`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style`:** yes (state `{}`).

| Part | Prop | Type | Default | Description | Req |
|---|---|---|---|---|---|
| Group | `children` | `ReactNode` | — | | — |
| GroupLabel | `id` | `string` | generated | Label id referenced by the group. | MNU-API-48 |

- **MNU-API-48** — MUST. The label id is the `id` prop or a generated id; the enclosing Group/RadioGroup renders it as `aria-labelledby` (MNU-ARIA-10) while the label is mounted. The outline does not list `id` for GroupLabel (§16 Q3).
  Source: `menu/group-label/MenuGroupLabel.tsx:19-30,35`. Test: [L2] `MenuGroupLabel.test.tsx` "should reference the generated id in Group's `aria-labelledby`", "should reference the provided id in Group's `aria-labelledby`".

### 3.18 `Menu.Separator`

The standalone `Separator` component (separator.md), not the listbox separator: `<div role="separator">` with `aria-orientation` (SEP-ARIA-01, SEP-ARIA-02), prop `orientation` (default `'horizontal'`, SEP-API-01), `data-orientation` (SEP-DOM-01). Exported attribute namespace `MenuSeparatorDataAttributes` (= `SeparatorDataAttributes`). Source: `menu/index.parts.ts:19`; `menu/index.ts:46`; `separator/Separator.tsx:16-26`. Test: [L2] `MenuFilterParts.test.tsx` describeConformance(`<Menu.Separator />`).

### 3.19 `Menu.SubmenuRoot`

**Default element:** none. No `className`/`style`/`render`.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `defaultOpen`, `open`, `onOpenChange`, `onOpenChangeComplete`, `onItemHighlighted`, `loopFocus`, `highlightItemOnHover`, `orientation`, `disabled`, `actionsRef` | as `Menu.Root` | as `Menu.Root` | | MNU-API-49 |
| `closeParentOnEsc` | `boolean` | `false` | | MNU-API-11 |
| `children` | `ReactNode` | — | | — |

- **MNU-API-49** — MUST. `Menu.SubmenuRoot` accepts the Root props except `modal`, `handle`, `triggerId`, `defaultTriggerId` (and the function-child form); `onOpenChange` receives `Menu.SubmenuRoot.ChangeEventDetails` (identical to the Root's). A `modal` value passed anyway is ignored with the warning MNU-EDGE-02. Under a `Menu.FilterProvider` it additionally accepts the provider's filter props (applied by the provider, not by the consumer).
  Source: `menu/submenu-root/MenuSubmenuRoot.tsx:41-66`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:311`. Test: [L2] `MenuRoot.test.tsx` "warns that nested menus ignore the modal prop"; `MenuPositioner.test.tsx` "closes an open submenu with a sibling reason when its controlled parent closes".

State object (`Menu.SubmenuRoot.State`): `{}`. Source: `menu/submenu-root/MenuSubmenuRoot.tsx:68`.

### 3.20 `Menu.SubmenuTrigger`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.SubmenuTrigger.State`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `openOnHover` | `boolean` | `true` | Open the submenu on hover. | MNU-API-50 |
| `delay` | `number` | `100` | Hover open delay (ms). | MNU-API-51 |
| `closeDelay` | `number` | `0` | Hover/sibling close delay (ms). | MNU-API-51 |
| `disabled` | `boolean` | `false` | | MNU-API-52 |
| `label` | `string` | rendered text | | MNU-API-36 |
| `nativeButton` | `boolean` | `false` | | MNU-API-37 |
| `id` | `string` | generated | Element id and trigger id. | MNU-API-53 |
| `onClick` | `MouseEventHandler` | — | | MNU-API-53 |

- **MNU-API-50** — MUST. `openOnHover` defaults to `true`. With `true`, hovering opens the submenu (MNU-PTR-09), *mouse* presses are ignored by the click interaction (`ignoreMouse`) and a first activation after a hover open keeps it open (`stickIfOpen`); keyboard and touch/screen-reader activations toggle it. With `false`, a mouse press (mousedown) toggles the submenu.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:40,176-204`. Docs: `CHANGELOG.md v1.9.0 "Toggle submenus on repeated trigger activation"`. Test: [L4] `MenuRoot.test.tsx` "opens submenu on click when openOnHover is false"; `MenuSubmenuTrigger.test.tsx` "keeps a hover-opened submenu open when pressing %s", "closes a touch-opened submenu on a second tap"; `MenuSubmenuTrigger.screenReaderPress.test.tsx` (5 tests).
- **MNU-API-51** — MUST. `delay` defaults to `100` and is both the hover open delay and the rest time of the submenu trigger (MNU-PTR-09); `closeDelay` defaults to `0`, is the hover close delay, and is published to the submenu store so a sibling-hover close in the parent waits `closeDelay` ms (MNU-PTR-11).
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:41-42,127,181-183`. Docs: `CHANGELOG.md v1.4.0 "Fix closeDelay not being applied to Menu.SubmenuTrigger"`. Test: [L4] `MenuRoot.test.tsx` "opens the submenu on hover with zero delay", "should close submenu after delay when hovering a sibling item", "should not restart closeDelay on repeated mousemove over sibling items", "opens a submenu after a plain delay without waiting for the pointer to rest".
- **MNU-API-52** — MUST. The effective disabled state is `disabled || submenu.disabled || parentMenu.disabled`; a disabled submenu trigger renders `data-disabled` and `aria-disabled="true"`, stays focusable, and neither hover nor press opens the submenu.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:43,129-132,177,193`. Test: [L2] `MenuSubmenuTrigger.test.tsx` "prop: disabled" (2 behavioral tests); `MenuRoot.test.tsx` "disables submenu triggers when controlled open". (L4 for the hover-open clause)
- **MNU-API-53** — MUST. The trigger id (prop or generated) is the element id, the submenu store's trigger-registry key (re-registered when it changes) and the target of the submenu popup's `aria-labelledby`; `onClick` is forwarded through the item/button chain (consumer first).
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:38,59,68-106,262`. Test: [L2] `MenuSubmenuTrigger.test.tsx` "follows a submenu trigger id change".

#### State object (`Menu.SubmenuTrigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled state. |
| `highlighted` | `boolean` | Highlighted in the *parent* list. |
| `open` | `boolean` | The submenu is open. |

Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:210,291-304`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open` | submenu open | MNU-DOM-18 | C# |
| `data-highlighted`, `data-disabled` | | MNU-DOM-18 | C# |
| `tabindex` | `0` when (open or highlighted) and the parent is not virtual-focus; otherwise `-1` | MNU-DOM-18 | C# |

### 3.21 `Menu.FilterProvider` (Preview)

**Default element:** none. No `className`/`style`/`render`. The provider forwards its filter props to the `Menu.Root`/`Menu.SubmenuRoot` rendered directly inside it (MNU-STR-14). Its change-event types reuse the internal filter-dropdown reasons (`input-change`, `input-clear`, `clear-press`, `popup-close`); the filter-dropdown module itself is not exported and is documented here only as consumed by Menu.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `filter` | `((text: string, query: string) => boolean) \| null` | locale-aware "contains" | Match predicate; `null` disables built-in filtering. | MNU-API-54 |
| `autoHighlight` | `boolean \| 'always'` | `false` | Highlight the first match automatically. | MNU-API-55 |
| `locale` | `Intl.LocalesArgument` | runtime locale | Locale of the default filter. | MNU-API-56 |
| `value` / `defaultValue` | `string` | `undefined` / `''` | Consumer-owned / initial query. | MNU-API-57 |
| `onValueChange` | `(value: string, eventDetails: Menu.FilterProvider.ChangeEventDetails) => void` | — | Cancelable. | MNU-API-58 |
| `children` | `ReactNode` | — | The root to make filterable. | — |

- **MNU-API-54** — MUST. `filter` defaults to the shared locale filter's `contains` (UTL-I18N-08/09: case-, accent- and punctuation-insensitive substring match built with `Intl.Collator`, `usage: 'search'`, `sensitivity: 'base'`, `ignorePunctuation: true`). A custom function is called with exactly two arguments `(itemText, trimmedQuery)` for every registered item and keeps the item when it returns `true`; a changed function re-runs matching. `null` turns built-in matching off (every item stays visible; the consumer filters its own rendering) while query ownership, `autoHighlight` and the empty state keep working.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:83-89,162-170`; `menu/filter-provider/MenuFilterProviderOptions.ts:5-11`. Docs: `components/menu/page.mdx` §Matching items. Test: [L2] `MenuFilterRoot.test.tsx` "applies a custom filter to item text", "keeps every item visible when filtering is turned off with null", "uses an updated custom filter function", "uses the configured locale for default matching".
- **MNU-API-55** — MUST. `autoHighlight` defaults to `false`. `true`: whenever the trimmed query is non-empty and changes, the first visible item is highlighted (MNU-STA-19); `'always'`: the first visible item is highlighted even with an empty query, including on open and when the query is cleared. With either value the arrow keys wrap within the list instead of escaping to the input (`allowEscape` off), and with `'always'` a pointer highlight is kept when the pointer leaves (`resetOnPointerLeave` off).
  Source: `menu/filter-root/useMenuFilterRoot.ts:13,26-27`; `filter-dropdown/root/FilterDropdownRoot.tsx:89-91,144-150,175-183`. Test: [L2] `MenuFilterRoot.test.tsx` "prop: autoHighlight" (17 tests), "clears a pointer highlight on pointer leave by default".
- **MNU-API-56** — MUST. `locale` is passed to the default filter's collator (UTL-I18N-08); it has no effect on a custom `filter`.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:84`. Test: [L2] `MenuFilterRoot.test.tsx` "uses the configured locale for default matching" (`locale="tr"`, `ı` matches `Istanbul`).
- **MNU-API-57** — MUST. `value`/`defaultValue` form a controlled/uncontrolled pair for the query (UTL-STA-01..05, diagnostic name `MenuFilterProvider`, state `value`); `defaultValue` defaults to `''`. A controlled query renders exactly the given value in the input and filters by it; typing only requests a change.
  Source: `menu/filter-root/MenuFilterDropdown.tsx:36,47-52`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "supports a controlled query"; `MenuFilterRoot.test.tsx` "supports a controlled query" (autoHighlight); `MenuFilterSubmenuRoot.test.tsx` "renders the controlled query and reports changes".
- **MNU-API-58** — MUST. `onValueChange(value, details)` is called for every query change with `details.reason` ∈ {`input-change`, `input-clear`, `clear-press`, `popup-close`} (MNU-EVT-16..18); `details.cancel()` keeps the current (uncontrolled) query and the current highlight.
  Source: `menu/filter-root/MenuFilterDropdown.tsx:54-61`; `filter-dropdown/root/FilterDropdownRoot.tsx:98-107`. Test: [L2] `MenuFilterRoot.test.tsx` "input value change reasons" (4 tests), "leaves the uncontrolled query and visible items unchanged when a change is canceled", "keeps the query when the popup-close reset is canceled".

State object (`Menu.FilterProvider.State`): `{}`.

### 3.22 `Menu.Input`

**Default element:** `<input>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Input.State`). MUST be inside a filterable menu (MNU-STR-15).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated | Element id (so a `<label htmlFor>` can target it). | MNU-API-59 |
| `disabled` | `boolean` | — | OR-ed with the root's `disabled`. | MNU-API-59 |
| native `<input>` attributes, `onKeyDown`, `onFocus`, … | | | Forwarded; consumer handlers run first. | MNU-API-59 |

- **MNU-API-59** — MUST. The input id is the `id` prop or a generated id; `disabled` is `root.disabled || disabled`; consumer `onKeyDown`/`onFocus`/`onBlur` handlers run before the part's own ones, exactly once per event, and `preventBaseUIHandler()` in `onKeyDown` opts that key out of list navigation and activation.
  Source: `menu/input/MenuInput.tsx:32-56`; `filter-dropdown/input/FilterDropdownInput.tsx:35,67`. Test: [L2] `MenuFilterRoot.test.tsx` "uses the id passed to Menu.Input", "disables filter controls when the root is disabled", "runs a consumer handler once per interaction on each composed part", "lets a consumer key handler on the input opt out of list navigation and activation". (L4 for the `onKeyDown` opt-out clause)

State object (`Menu.Input.State`): `{ highlighted: boolean }` — the input "owns" the visible focus ring: `true` while it is focus-visible and either the last modality was pointer or no item is highlighted (MNU-STA-22). Source: `filter-dropdown/input/FilterDropdownInput.tsx:55-57,141-147`. Data attribute: `data-highlighted` (MNU-DOM-21).

### 3.23 `Menu.Clear`

**Default element:** `<button>`. **Accepts `render`:** yes. **Accepts `className`/`style` as function of state:** yes (`Menu.Clear.State`). MUST be inside a filterable menu.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | OR-ed with the root's `disabled`. | MNU-API-60 |
| `nativeButton` | `boolean` | `true` | | MNU-API-60 |

- **MNU-API-60** — MUST. `Menu.Clear` renders only while the query is non-empty (MNU-DOM-22); pressing it reports `''` with reason `clear-press` and refocuses the input (MNU-PTR-21). `disabled` (default `false`, OR-ed with the root's) disables it; `nativeButton` defaults to `true` (UBT-API-02).
  Source: `filter-dropdown/clear/FilterDropdownClear.tsx:24-33,43-64`. Test: [L2] `MenuFilterParts.test.tsx` describeConformance(`<Menu.Clear />`, `button: true`); `MenuFilterRoot.test.tsx` "reports clear-press when the clear button is used", "disables filter controls when the root is disabled".

State object (`Menu.Clear.State`): `{ disabled: boolean }` → `data-disabled`.

### 3.24 `Menu.Empty`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style`:** yes (state `{}`). MUST be inside a filterable menu. No own props.

- **MNU-API-61** — MUST. `Menu.Empty` renders its element (and children) only when the menu has no visible item (MNU-STA-23) and never before the items of the same commit have registered; it is absent from server-rendered markup.
  Source: `filter-dropdown/empty/FilterDropdownEmpty.tsx:20-47`. Test: [L2] `MenuFilterRoot.test.tsx` "never mounts the Empty children while items match", "renders Empty when no items are registered at all", "removes Empty once the query matches again", "shows the empty state after hydration when no items are rendered".

### 3.25 `Menu.List`

**Default element:** `<div>`. **Accepts `render`:** yes. **Accepts `className`/`style`:** yes (state `{}`).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated (filter: `<rootId>-list`) | Element id; what triggers/input point at. | MNU-API-62 |

- **MNU-API-62** — MUST. The list id is the `id` prop, else the `render` element's id, else a generated id; the active trigger's `aria-controls` (plain menu) and the input's `aria-controls` (filterable menu) follow id changes while open, and an explicitly empty id removes those references.
  Source: `menu/list/MenuList.tsx:23-33`; `filter-dropdown/list/FilterDropdownList.tsx:23-27`. Test: [L2] `MenuList.test.tsx` "updates the trigger when the open list changes its id"; `MenuFilterRoot.test.tsx` "omits aria-controls when the list renders with an explicitly empty id", "points aria-controls at the id of a server-created render element".

### 3.26 `Menu.createHandle()` / `Menu.Handle`

- **MNU-API-63** — MUST. `Menu.createHandle<Payload>()` returns a new independent `Menu.Handle` (PSH-API-01); `new Menu.Handle()` is equivalent.
  Source: `menu/store/MenuHandle.ts:13-19,53-55`. Test: [L1] `MenuRoot.test.tsx` `DetachedTriggerMenu` fixture (`new Menu.Handle()`); `MenuRoot.detached-triggers.test.tsx`.
- **MNU-API-64** — MUST. `handle.open(triggerId)` opens the attached menu associated with the `Menu.Trigger` whose id is `triggerId` (reason `imperative-action`, that trigger as `details.trigger`); it accepts only a string for Menu (PSH-API-02, PSH-STA-38). Without an attached Root it is ignored and warns in development (PSH, `MenuHandle.open() was called while no root using this handle is mounted…`).
  Source: `menu/store/MenuHandle.ts:29-31`; `utils/popups/popupHandle.ts:213-238`. Test: [L4] `MenuRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests.
- **MNU-API-65** — MUST. `handle.close()` requests a close with reason `imperative-action` on the attached Root, or is ignored (with a development warning) when none is attached (PSH-API-04).
  Source: `menu/store/MenuHandle.ts:38-40`; `utils/popups/popupHandle.ts:271-285`. Test: [L4] `MenuRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests.
- **MNU-API-66** — MUST. `handle.isOpen` is the attached Root's effective open state, `false` while no Root is attached (PSH-API-05).
  Source: `menu/store/MenuHandle.ts:45-47`. Test: [L2] `MenuTrigger.test.tsx` "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed".

### 3.27 `Menu.useFilter`

- **MNU-API-67** — MUST. `Menu.useFilter(options?)` is the shared `getFilter` (UTL-I18N-08..13): it returns `{ contains, startsWith, endsWith }` locale-aware predicates for consumer-side filtering, taking `{ locale, ...collatorOptions }`.
  Source: `menu/index.parts.ts:28`. Docs: `components/menu/page.mdx` §useFilter. Test: none in `menu/`.

### 3.28 Shared types

- `Menu.Root.ChangeEventReason` = `'trigger-hover' | 'trigger-focus' | 'trigger-press' | 'outside-press' | 'focus-out' | 'list-navigation' | 'escape-key' | 'item-press' | 'close-press' | 'sibling-open' | 'cancel-open' | 'imperative-action' | 'none'`. `close-press` is declared only, never emitted by any Menu part (§16 Q1). Source: `menu/root/MenuRoot.tsx:924-937`.
- `Menu.Root.ChangeEventDetails` = the common change-event details (EVD-API-01) specialized to those reasons plus `preventUnmountOnClose()` (EVD-API-09, TRN-EVT-07). Source: `menu/root/MenuRoot.tsx:939-942`.
- `Menu.Root.HighlightEventReason` = `'keyboard' | 'pointer' | 'none'`; `Menu.Root.HighlightEventDetails` = the generic (non-cancelable) details `{ reason, event, label }` where `label` is the item's `label` prop or text content, and `event` is a `KeyboardEvent` for `'keyboard'`, a `MouseEvent` or `PointerEvent` for `'pointer'`, any `Event` for `'none'` (EVD-API-02/08, EVD-EVT-24). Source: `menu/root/MenuRoot.tsx:944-955`. Test: [L1] `MenuRoot.spec.tsx`.
- `Menu.Root.Actions` = `{ unmount(): void; close(): void }`. `Menu.Root.Orientation` = `'horizontal' | 'vertical'`. Source: `menu/root/MenuRoot.tsx:919-922,957`.
- `Menu.SubmenuRoot.ChangeEventReason/Details`, `Menu.CheckboxItem.ChangeEventReason/Details`, `Menu.RadioGroup.ChangeEventReason/Details` are aliases of the Root's types. Source: `menu/submenu-root/MenuSubmenuRoot.tsx:70-71`; `menu/checkbox-item/MenuCheckboxItem.tsx:203-204`; `menu/radio-group/MenuRadioGroup.tsx:133-134`.
- `Menu.FilterProvider.ChangeEventReason` = `'input-change' | 'input-clear' | 'clear-press' | 'popup-close'`; `Menu.FilterProvider.ChangeEventDetails` = cancelable details with those reasons. Source: `menu/filter-provider/MenuFilterProvider.tsx:52-53`; `filter-dropdown/root/FilterDropdownRootContext.ts:107-114`.
- `MenuFilter` / `MenuFilterOptions` = the shared filter's return type / parameters. Source: `menu/index.ts:26-29`.
- `Menu.Handle<Payload>`; `Menu.Trigger.Props<Payload>`; `Menu.Root.Props<Payload>`. Every part exposes `Props` and `State`. Attribute-name namespaces exported: `MenuArrowDataAttributes`, `MenuBackdropDataAttributes`, `MenuCheckboxItemDataAttributes`, `MenuCheckboxItemIndicatorDataAttributes`, `MenuItemDataAttributes`, `MenuLinkItemDataAttributes`, `MenuPopupDataAttributes`, `MenuPositionerCssVariables`, `MenuPositionerDataAttributes`, `MenuRadioItemDataAttributes`, `MenuRadioItemIndicatorDataAttributes`, `MenuTriggerDataAttributes`, `MenuViewportCssVariables`, `MenuViewportDataAttributes`, `MenuSeparatorDataAttributes`, `MenuSubmenuTriggerDataAttributes`, `MenuInputDataAttributes`, `MenuClearDataAttributes`. Source: `menu/index.ts:31-49`.
- `TransitionStatus`, `Side`, `Align`, `InteractionType` (`'mouse' | 'touch' | 'pen' | 'keyboard' | ''`) are shared (TRN, FLT, UBT-ALG-06).

## 4. State model

### 4.1 Open state and the open-change sequence

The Root's store is the shared popup store (PSH §4.3) extended with menu fields: `disabled`, `modal`, `openMethod`, `keyboardOpen`, `allowMouseEnter`, `highlightItemOnHover`, `parent`, `rootId`, `activeIndex`, `listElement`, `filterTriggerProps`, `inputProps`, `inputFocused`, `highlightedItem`, `hoverEnabled`, `instantType`, `openChangeReason`, `floatingTreeRoot`, `floatingNodeId`, `floatingParentNodeId`, `itemProps`, `closeDelay`, `keyboardEventRelay`, `adaptiveOrigin` (initial values in `menu/store/MenuStore.ts:291-327`).

- **MNU-STA-01** — MUST. The effective open state is the consumer-owned `open` when defined, otherwise the component-owned value seeded from `defaultOpen` (PSH-STA-02). Every open-change request (trigger press, hover, keyboard, dismissal, handle, actions, tree events) goes through one *open-change sequence*: it is ignored when it requests a close while already closed, and when it requests the current state from the current active trigger with the same reason as the last change; otherwise `onOpenChange` is called first and, if not canceled, the store commits the new state (EVD-STA-08, EVD-EVT-07).
  Source: `menu/root/MenuRoot.tsx:341-445`. Test: [L2] `MenuRoot.test.tsx` popupConformanceTests, "calls onOpenChange with false exactly once per menu when a submenu item is clicked"; `MenuFilterRoot.test.tsx` "stays closed when onOpenChange is canceled". (L4 for the popup conformance suite clause)
- **MNU-STA-02** — MUST. Before `onOpenChange` is called for a close whose details carry no `trigger`, `details.trigger` is set to the current active trigger element (EVD-EVT-16); the active trigger id/element are *not* cleared by the close itself (they are cleared when the popup unmounts, PSH-STA-45) so exit transitions and focus return still know the trigger.
  Source: `menu/root/MenuRoot.tsx:366-370,408-417`; `utils/popups/popupStoreUtils.ts:189-227`. Test: [L2] `MenuRoot.test.tsx` "focuses the trigger after the menu is closed but not unmounted"; `MenuRoot.triggerless.test.tsx` "supports an external anchor across reopen, keepMounted=%s".
- **MNU-STA-03** — MUST. A committed change records `openChangeReason` (readable as `lastOpenChangeReason`) and `keyboardOpen`, which is `true` iff the change opens the menu with reason `list-navigation`, or with reason `trigger-press`/`item-press` whose native event is a click with `detail === 0` (keyboard or assistive-technology activation). `keyboardOpen` is reset to `false` whenever a filterable menu is closed.
  Source: `menu/root/MenuRoot.tsx:419-420`; `menu/utils/isKeyboardOpen.ts:8-21`; `menu/filter-root/MenuFilterDropdown.tsx:86-90`. Test: [L1] `MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation", "moves focus to the first item after a screen reader $name"; `MenuFilterRoot.initialHighlight.test.tsx` "does not highlight an item when a controlled open follows a keyboard open".
- **MNU-STA-04** — MUST. The committed change also sets `instantType`: `'group'` when the parent kind is `'menubar'` and the reason is one of `trigger-focus`, `focus-out`, `trigger-hover`, `list-navigation`, `sibling-open`; else `'click'` when the reason is `trigger-press`/`item-press` with a `detail === 0` click; else `'dismiss'` when closing with reason `escape-key` or no reason; else `undefined`. (`'trigger-change'` is written by the trigger-switch mechanism, TRN-ANI-28, and is reported as `undefined` once the popup is closed.)
  Source: `menu/root/MenuRoot.tsx:404,422-437`; `menu/store/MenuStore.ts:115-123`. Test: [L1] `MenuPopupEnterTransition.test.tsx` "marks an initially open submenu as instant when its parent opened instantly"; `MenuRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers".
- **MNU-STA-05** — MUST. A touch-originated close is suppressed when it is a `click` event closing the menu for a reason other than `item-press` within 300 ms of an open with reason `trigger-focus` (delayed-click guard for mobile browsers that fire focus before click); any other open re-arms touch closes immediately.
  Source: `menu/root/MenuRoot.tsx:380-402`. Test: none.
- **MNU-STA-06** — MUST. `modal` as stored is `true` by default and is only written for a top-level (non-nested) Root; the effective modality is `true` iff the parent kind is none or `'context-menu'` and the stored value is not `false`. A menu whose last open reason is `trigger-hover` behaves as non-modal (no backdrop, no scroll lock) until a later change (e.g. an impatient click) rewrites the reason.
  Source: `menu/store/MenuStore.ts:82-84,300`; `menu/root/MenuRoot.tsx:166,220`; `menu/positioner/MenuPositioner.tsx:245-253,264-268`. Test: [L1] `MenuRoot.test.tsx` "treats hover-opened menus as modal after a click", "prop: modal".
- **MNU-STA-07** — MUST. The effective `disabled` is the Root's `disabled`, OR-ed with the Menubar's `disabled` when the parent kind is `'menubar'`.
  Source: `menu/store/MenuStore.ts:78-81`. Test: [L1] `MenuRoot.test.tsx` "prop: disabled".
- **MNU-STA-08** — MUST. `rootId` is a generated id owned by the top-level Root and read through the parent chain by every submenu; `floatingTreeRoot` and `keyboardEventRelay` are likewise inherited from the parent store, and a submenu's subscribers are notified when any of the three change in the parent. The subscription is released when the submenu unmounts.
  Source: `menu/store/MenuStore.ts:91-97,125-148,190-218`; `menu/root/MenuRoot.tsx:476`. Docs: `CHANGELOG.md v1.9.0 "Release parent menu subscriptions when a submenu unmounts"`. Test: [L2] `MenuRoot.test.tsx` "submenu parent store subscription" (3 tests).

### 4.2 Highlight (active index)

- **MNU-STA-09** — MUST. The highlight is a single `activeIndex` (`number | null`) into the Root's DOM-ordered item list (CMP-STA-31). It is `null` when nothing is highlighted, is set by keyboard navigation, typeahead, pointer movement over an item (when `highlightItemOnHover`), the automatic highlight of filtering, and the initial open highlight; it is reset to `null` when the popup closes or its element unmounts (CMP-STA-34), when the pointer leaves an item without entering another (MNU-PTR-13), when a trigger of an open menu receives focus (plain menus), and when a submenu trigger blurs while highlighted.
  Source: `menu/root/MenuRoot.tsx:194,224-240,535-537`; `menu/store/MenuStore.ts:224-251`; `floating-ui-react/hooks/useListNavigation.ts:391-411,709-743,915-924`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:254-258`. Test: [L4] `MenuRoot.test.tsx` "changes the highlighted item using the arrow keys", "passes native mouse and pointer events for pointer highlights"; `MenuSubmenuTrigger.test.tsx` "opens the submenu with $openKey and highlights a single item in $direction direction".
- **MNU-STA-10** — MUST. Each highlight write is tagged with the reason that produced it (`keyboard` for key events, `pointer` for mouse/pointer events, `none` otherwise); a write that targets the item already reported to `onItemHighlighted` (a write-back) is tagged `none` so a later re-emission caused by list changes does not repeat a stale reason.
  Source: `menu/store/MenuStore.ts:224-240`; `utils/getHighlightReason.ts:8-21`. Test: [L1] `MenuRoot.test.tsx` "does not report a stale highlight reason for a later list change".
- **MNU-STA-11** — MUST. `hoverEnabled` starts `true`, becomes `false` when a child submenu opens (so the parent's own hover-close logic yields to the child), when the pointer moves inside a submenu's popup, or when the popup is clicked, and is reset to `true` whenever the menu is closed.
  Source: `menu/positioner/MenuPositioner.tsx:135-140`; `menu/root/MenuRoot.tsx:335-339,681-691`. Test: [L4] `MenuRoot.test.tsx` "should not close when submenu is hovered after root menu is hovered", "keeps the parent submenu open after a third-level submenu closes due to sibling hover".
- **MNU-STA-12** — MUST. `allowMouseEnter` starts `false`, is set `true` by the first pointer movement over the active trigger or over the popup, and reset to `false` when the popup unmounts. While `false`, a submenu trigger with `delay > 0` does not open on hover (MNU-PTR-09).
  Source: `menu/root/MenuRoot.tsx:229-231,646-650,681-683`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:184`. Test: [L4] `MenuRoot.test.tsx` "opens a submenu after a plain delay without waiting for the pointer to rest" (programmatic open, `delay: 100`, opens without resting).
- **MNU-STA-13** — MUST. `listElement` is the mounted `Menu.List` element (or `null`); it switches the popup role (MNU-ARIA-05), the trigger's `aria-controls` target (MNU-ARIA-03) and the pointer-open focus target (MNU-FOC-02).
  Source: `menu/list/MenuList.tsx:21,38`; `menu/store/MenuStore.ts:99-104`. Test: [L2] `MenuList.test.tsx` "<Menu.List /> in a plain menu" (8 tests).

### 4.3 Checked state (CheckboxItem / RadioItem)

- **MNU-STA-14** — MUST. A `Menu.CheckboxItem`'s `checked` is its own controlled/uncontrolled boolean (MNU-API-41); a `Menu.RadioItem` is checked iff the group's value strictly equals its `value` (MNU-API-44). Both render `aria-checked` and `data-checked`/`data-unchecked` from that value only.
  Source: `menu/checkbox-item/MenuCheckboxItem.tsx:30,83`; `menu/radio-item/MenuRadioItem.tsx:50,87`. Test: [L2] `MenuCheckboxItem.test.tsx` "state management"; `MenuRadioItem.test.tsx` "state management".

### 4.4 Filter state (filterable menus)

A filterable root runs the list in *virtual focus* mode: real focus stays on the `Menu.Input` (or wherever the user put it) and the highlight is exposed through `aria-activedescendant`. The Root records `virtualFocus: true`, `allowEscape = !autoHighlight`, `resetOnPointerLeave = autoHighlight !== 'always'`, `inputProps` (the navigation key handler and `aria-activedescendant` for the input), `inputFocused`, and `highlightedItem` (the element at `activeIndex`, published only in this mode).

- **MNU-STA-15** — MUST. The *filter query* is the trimmed query string; matching is skipped (every registered item visible) when the trimmed query is empty or `filter` is `null`. Otherwise the set of visible items is recomputed, post-commit pre-paint, whenever the query, the filter function, or the registered item set (ids or texts) changes while the menu is open; items not in the set render nothing (MNU-DOM-28).
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:86-89,131-196`; `filter-dropdown/store.ts:15-16`; `filter-dropdown/item/useFilterDropdownItem.ts:87,163`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "filters items by text and shows the empty state"; `MenuFilterRoot.test.tsx` "keeps a highlight when typing does not change the trimmed query with autoHighlight=%s".
- **MNU-STA-16** — MUST. The first visible-set computation waits until at least one item has registered (a popup's content registers one commit after the root opens), so items are never hidden and remounted on open; an item that does not match renders exactly once (to capture its text) and matching items are never remounted by the filter.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:155-159`; `filter-dropdown/item/useFilterDropdownItem.ts:89-92,142-148`. Test: [L2] `MenuFilterRoot.test.tsx` "keeps the matching items mounted".
- **MNU-STA-17** — MUST. An item's filter text is resolved as: the `label` prop when given; else the element's current `textContent` while mounted (re-read on every match so descendant text changes count); else, while hidden, the cached last text unless the item's children (or `render` element) changed, in which case the children's string content (strings/numbers concatenated, elements recursed) is used. A text change re-registers the item so the active query runs again.
  Source: `filter-dropdown/item/useFilterDropdownItem.ts:12-25,99-162`. Test: [L4] `useMenuFilterItem.test.tsx` "item text resolution" (7 tests); `MenuFilterRoot.test.tsx` "keeps component-rendered item text after the item is filtered out", "re-filters an item whose rendered text changes while it is hidden".
- **MNU-STA-18** — MUST. When the query changes (not canceled) and `autoHighlight` is `false`, the highlight is cleared (`activeIndex = null`) because indexes are positional. When the visible set changes identity (new matches, or a smaller set), the highlight is set to the first visible item if `autoHighlight` is enabled for the current query, otherwise cleared (except for the very first computation when the query did not change). A registry update that does not change the visible set keeps the highlight, unless `autoHighlight` is enabled and the query changed, in which case the first item is highlighted.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:98-107,135-184`. Test: [L2] `MenuFilterRoot.test.tsx` "resets the highlight when the query changes and rebuilds it from the first match", "clears a positional highlight when a controlled query replaces the visible item", "updates the active descendant when filtering replaces a custom-id item", "keeps the highlight when an item's text changes with autoHighlight=\"always\"", "clears the highlight when no items match".
- **MNU-STA-19** — MUST. "autoHighlight enabled for the current query" means: open and (`autoHighlight === 'always'`, or `autoHighlight === true` and the trimmed query is non-empty). With `filter === null` or an empty query, enabling it highlights index 0 whenever the query changed or nothing is highlighted; clearing a typed query with `autoHighlight: true` clears the highlight, with `'always'` it returns to the first item.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:89-91,140-151`. Test: [L2] `MenuFilterRoot.test.tsx` "automatically highlights the first match after the user types", "always highlights the first item when autoHighlight is \"always\"", "clears the automatic highlight when a typed query is cleared", "restores the first item when an always-highlighted query is cleared", "always highlights the first item when mounted inside an opened dialog".
- **MNU-STA-20** — MUST. When the DOM-ordered item list changes while open (items inserted, removed, reordered, or filtered), the highlight is kept only if the element at `activeIndex` is the same element as before; otherwise it becomes the first item when autoHighlight is enabled, else `null`. An item appended after the highlighted one (including one the query hides) therefore keeps the highlight; an item inserted before it clears it.
  Source: `menu/filter-root/MenuFilterList.tsx:101-123`; `filter-dropdown/root/FilterDropdownRoot.tsx:109-125`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "does not report the item that takes the highlighted index when an item is inserted", "keeps the highlight when an item the query filters out is appended", "keeps the highlight when an item is appended after reopening from a filtered session".
- **MNU-STA-21** — MUST. When the menu closes while the query is non-empty, `onValueChange('', { reason: 'popup-close' })` is issued once, post-commit pre-paint of the closing commit; the input and the filtered item set keep showing the closing query until the popup unmounts (or reopens), and reopening starts from the (possibly canceled → preserved) committed value.
  Source: `filter-dropdown/root/useFilterDropdownCloseQuery.ts:19-38`; `menu/filter-root/MenuFilterDropdown.tsx:68-73`. Test: [L2] `MenuFilterRoot.test.tsx` "resets the input value once when the popup closes", "keeps the query when the popup-close reset is canceled", "keeps submenu filtering updated during the exit transition", "prop: keepMounted" "resets the query and highlight across a close and reopen", "ignores text typed in the input while the popup animates out with $name".
- **MNU-STA-22** — MUST. The input's `highlighted` state is `inputFocusVisible && (!keyboardModality || activeItemId == null)`: `inputFocusVisible` is `true` on focus and `false` on blur, and both it and `keyboardModality` are seeded from `keyboardOpen` on every open; `keyboardModality` becomes `true` on any keydown in the input and `false` on pointer down / pointer move in the list or mouse enter / pointer down on the input.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:49-58`; `filter-dropdown/input/FilterDropdownInput.tsx:55-57,104-116,117-134`; `filter-dropdown/list/FilterDropdownList.tsx:45-50`. Test: [L2] `MenuFilterRoot.test.tsx` "highlights the first item while keeping input focus when opened with the keyboard", "marks the input focus-visible when the menu is opened with a pointer", "focuses the input when a filterable submenu opens with the keyboard"; `MenuFilterRoot.initialHighlight.test.tsx` "highlights the first enabled action and keeps the input in the arrow loop".
- **MNU-STA-23** — MUST. The menu is *empty* when the visible set is `null` and no item is registered, or when the visible set is empty; `Menu.Empty` renders exactly then (MNU-API-61). The registered-item count is updated synchronously as items register/unregister.
  Source: `filter-dropdown/store.ts:9-17`; `filter-dropdown/root/FilterDropdownRoot.tsx:69-78`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "filters items by text and shows the empty state"; `MenuFilterRoot.test.tsx` "renders Empty when no items are registered at all".
- **MNU-STA-24** — MUST. A `Menu.Group`/`Menu.RadioGroup` in a filterable menu is *hidden* iff a visible set exists, the group has registered members, and none of them is visible or retained; nested groups and kept-mounted filterable submenus register with their enclosing group as one member whose visibility is their own hidden state, and a `Menu.List` of a nested filter root does not register its items with the enclosing group.
  Source: `filter-dropdown/group/useFilterDropdownGroup.ts:12-23,42-64`; `filter-dropdown/list/FilterDropdownList.tsx:58-66`. Test: [L2] `MenuFilterGroup.test.tsx` (2 tests); `MenuFilterRoot.test.tsx` "hides a group, label included, when the query filters out all of its items", "keeps a group with no registered items visible while filtering".
- **MNU-STA-25** — MUST. A `Menu.SubmenuTrigger` under a filterable parent registers as an item of the *parent's* filter (it belongs to the parent list even though it renders inside its own SubmenuRoot); it stays rendered while its submenu is mounted even when the query hides it (`visible || mounted`), and when the query hides it while the submenu is open, the submenu is closed with reason `none`. A consumer that cancels that close keeps the trigger and submenu mounted.
  Source: `menu/filter-root/useMenuFilterSubmenuTrigger.ts:18-45,80-85`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:276-289`. Test: [L2] `MenuFilterRoot.test.tsx` "closes the submenu when the query filters out its trigger", "keeps an open submenu trigger mounted when its filter close is canceled", "filters a non-filterable submenu trigger from a filterable parent", "filters a Menu.SubmenuTrigger used inside a plain submenu root".

### 4.5 Open/close state diagram

```
closed ──(open request, not canceled)──► open, mounted, transitionStatus 'starting' (next frame → undefined)
open ──(close request, not canceled)──► open=false, still mounted, transitionStatus 'ending'
  ──(popup animations finished, or actionsRef.unmount())──► unmounted: activeTriggerId/Element=null, activeIndex=null,
       allowMouseEnter=false, hoverEnabled=true, openMethod=null, onOpenChangeComplete(false)
'ending' ──(open request)──► open again without re-entering 'starting' (TRN-STA-09)
```

The transition statuses, the kept-mounted variant and the completion callbacks are specified in §9.

### 4.6 Invariants

- At most one item is highlighted per menu level; a highlighted submenu trigger and a highlighted item inside its open submenu may coexist (each level has its own index).
- Exactly one of `data-checked`/`data-unchecked` is present on a checkbox/radio item.
- In a filterable menu real focus never rests on an item as a result of the menu's own behavior (items prevent `mousedown` default, MNU-PTR-20); a screen reader may still focus one, which is handed back (MNU-KEY-27).

## 5. Rendered DOM and ARIA

State attributes follow the generic projection (RND-DOM-01..10) unless a mapping is named: `pressableTriggerOpenStateMapping` (Trigger), `triggerOpenStateMapping` (SubmenuTrigger), `popupStateMapping` (Positioner, Arrow), `popupTransitionStateMapping` (Popup, Backdrop), `itemMapping` (checkbox/radio items and indicators), `popupViewportStateMapping` (Viewport). Fields not covered by a mapping are projected generically, which is why `data-nested` and `data-instant` appear on parts whose outline entry does not list them (§16 Q3).

### 5.1 `Menu.Trigger` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `type="button"` / `role="button"` | native / non-native mode (UBT-DOM-01/02) | MNU-API-17 | C# |
| `id` | trigger id | MNU-DOM-03 | C# |
| `disabled` | effective disabled (native mode) | MNU-API-16 | C# |
| `data-popup-open`, `data-pressed` | open by this trigger | MNU-DOM-01 | C# |
| `data-disabled` | effective disabled | MNU-DOM-02 | C# |
| `aria-haspopup`, `aria-expanded`, `aria-controls` | see §5.9 | MNU-ARIA-01..03 | C# |
| `role="menuitem"` | inside a Menubar | MNU-ARIA-17 | C# |

- **MNU-DOM-01** — MUST. `data-popup-open=""` and `data-pressed=""` are both present iff the menu is open *and* this trigger is the active trigger; neither is present otherwise (a second trigger of the same menu stays unmarked). A vetoed controlled close keeps them.
  Source: `menu/trigger/MenuTrigger.tsx:229-232,266`; `utils/popupStateMapping.ts:13-16,39-46`. Test: [L2] `MenuTrigger.test.tsx` "should have the data-popup-open and data-pressed attributes when open", "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed"; `MenuRoot.test.tsx` "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen".
- **MNU-DOM-02** — MUST. `data-disabled=""` is present iff the effective disabled state is `true`.
  Source: `menu/trigger/MenuTrigger.tsx:229-232`; `menu/trigger/MenuTriggerDataAttributes.ts:14`. Test: none (covered by generic projection, RND-DOM-01).
- **MNU-DOM-03** — MUST. The element's `id` is the trigger id (MNU-API-18); when the `render` element supplies its own `id`, that id is rendered and is what the popup label references.
  Source: `menu/trigger/MenuTrigger.tsx:242`; `internals/resolvePopupLabel.ts:22-27`. Test: [L2] `MenuRoot.test.tsx` "keeps the root menu open when a submenu opens and the trigger `render` element has a custom id".

### 5.2 `Menu.Positioner` element

- **MNU-DOM-04** — MUST. The Positioner carries `data-open`/`data-closed` (exactly one), `data-anchor-hidden` when the anchor is hidden, `data-side` and `data-align` with the *resolved* placement, `data-nested=""` for a submenu, and `data-instant="<type>"` while an instant type is set; it also carries the positioning inline styles and CSS variables of FLT (`--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`, `--positioner-width`, `--positioner-height`; MNU-POS-08).
  Source: `menu/positioner/MenuPositioner.tsx:236-243,255-262`; `utils/usePositioner.tsx:36-40`; `utils/popupStateMapping.ts:48-61`; `menu/positioner/MenuPositionerCssVars.ts`. Test: [L4] `MenuPositioner.test.tsx` positionerConformanceTests, "prop: anchor".
- **MNU-DOM-05** — MUST. The Positioner renders `role="presentation"`, the boolean `hidden` attribute while the popup is not mounted (kept-mounted closed state, TRN-DOM-06) and `inert` while the menu is not open (so a closing menu cannot be interacted with); while `transitionStatus === 'starting'` it carries `style="transition: none"` (TRN-DOM-05).
  Source: `menu/positioner/MenuPositioner.tsx:255-262`; `utils/usePositioner.tsx:24-40`. Test: [L2] `MenuPositioner.test.tsx` "when keepMounted=true, should keep the content mounted when closed" (inaccessible while closed).

### 5.3 `Menu.Popup` element

- **MNU-DOM-06** — MUST. The Popup carries `data-open`/`data-closed`, `data-starting-style`/`data-ending-style` per its transition status (TRN-DOM-01..03), `data-side`/`data-align` (resolved placement), `data-instant="<type>"` while an instant type is set, and `data-nested=""` for a submenu. Server: relaxed, see §9.
  Source: `menu/popup/MenuPopup.tsx:141-154`; `menu/popup/MenuPopupDataAttributes.ts`. Test: [L4] `MenuPopupEnterTransition.test.tsx` (all tests observe `data-starting-style`/`data-instant`); `MenuRoot.test.tsx` "does not leave a tabbable item after tabbing out of a keepMounted menu before close" (`data-open`).
- **MNU-DOM-07** — MUST. The Popup renders its resolved `id` (MNU-API-32) and `data-rootownerid="<rootId>"`, the Root's generated id shared by every popup of the same menu tree; a consumer-supplied `data-rootownerid` is overridden.
  Source: `menu/popup/MenuPopup.tsx:155-180`. Test: [L2] `MenuTrigger.test.tsx` "keeps a hover-opened menu open when mouseup lands outside the trigger DOM but within its bounds" (indirect, see MNU-PTR-04). 
- **MNU-DOM-08** — MUST. The Popup renders `tabindex="-1"` and the focusable-popup marker `data-base-ui-focusable=""` (CMP-DOM-05), so it can receive focus on a pointer open and be identified as the list's focus element.
  Source: `menu/root/MenuRoot.tsx:678-679`; `utils/popups/popupStoreUtils.ts:27-30`. Test: [L2] `MenuRoot.test.tsx` "closes the entire tree when clicking outside the deepest submenu" (`expect(menu).toHaveFocus()`).
- **MNU-DOM-09** — MUST. While `transitionStatus === 'starting'` the Popup carries `style="transition: none"` (TRN-DOM-05), so the starting-style frame is not itself animated.
  Source: `menu/popup/MenuPopup.tsx:177`. Test: [L4] `MenuPopupTransitionState.test.tsx` "starts each opening once (controlled=$controlled, keepMounted=$keepMounted, defaultOpen=$defaultOpen)".

### 5.4 `Menu.Backdrop` element

- **MNU-DOM-10** — MUST. The Backdrop renders `role="presentation"`, the boolean `hidden` attribute while the popup is not mounted, inline `user-select: none` (with the `-webkit-` prefix), inline `pointer-events: none` iff the last open-change reason is `trigger-hover` (so a hover-opened menu does not block the page), and the `data-open`/`data-closed`/`data-starting-style`/`data-ending-style` attributes of the Root's transition status. When rendered under a ContextMenu it also registers itself with the context menu's backdrop handle.
  Source: `menu/backdrop/MenuBackdrop.tsx:23-54`. Test: [L2] `MenuBackdrop.test.tsx` "sets `pointer-events: none` style on backdrop if opened by hover", "does not set `pointer-events: none` style on backdrop if opened by click", "does not start an entry phase when initially open (controlled=%s)".

### 5.5 `Menu.Arrow` element

- **MNU-DOM-11** — MUST. The Arrow renders `aria-hidden="true"`, the arrow positioning inline styles from the Positioner (FLT), `data-open`/`data-closed`, `data-side`, `data-align` and `data-uncentered=""` when the arrow could not be centered on the anchor.
  Source: `menu/arrow/MenuArrow.tsx:22-42`. Test: [L2] `MenuArrow.test.tsx` describeConformance. (L4 for the Positioner-owned inline style and `data-side`/`data-align`/`data-uncentered` clause)

### 5.6 `Menu.Viewport` element

- **MNU-DOM-12** — MUST. The Viewport renders `data-activation-direction="<horizontal> <vertical>"` (tokens `left`/`right` and `up`/`down`, possibly empty) after a trigger switch, `data-transitioning=""` while a content transition runs, and `data-instant` like the Popup; its content is wrapped in `<div data-current>` and, during a transition, a preceding `<div data-previous>` carrying `--popup-width`/`--popup-height` (PSH-STR-07).
  Source: `menu/viewport/MenuViewport.tsx:26-45`; `menu/viewport/MenuViewportDataAttributes.ts`; `menu/viewport/MenuViewportCssVars.ts`. Test: [L4] `MenuViewport.test.tsx` viewportConformanceTests.

### 5.7 Item elements

- **MNU-DOM-13** — MUST. `Menu.Item` renders `data-highlighted=""` iff it is the highlighted item and `data-disabled=""` iff its effective disabled state is `true`.
  Source: `menu/item/MenuItem.tsx:50-58`; `menu/item/MenuItemDataAttributes.ts`. Test: [L2] `MenuRoot.test.tsx` "skips natively disabled items in text navigation" (`not.toHaveAttribute('data-highlighted')`), "marks items as disabled when controlled open".
- **MNU-DOM-14** — MUST. Every item part (Item, LinkItem, CheckboxItem, RadioItem) renders `tabindex="0"` iff the menu is open, the item is highlighted and the menu is not in virtual-focus mode; otherwise `tabindex="-1"` (never omitted, so links and native buttons leave the tab order). The highlighted item is therefore the only Tab stop inside a plain open menu; after the menu closes (even kept mounted) every item is `-1`.
  Source: `menu/item/useMenuItemCommonProps.ts:74-81`. Test: [L2] `MenuRoot.test.tsx` "focuses the first item after the menu is opened by keyboard", "does not leave a tabbable item after tabbing out of a keepMounted menu before close"; `MenuFilterRoot.test.tsx` "keeps plain menu items and submenu triggers out of the tab order in a filterable list".
- **MNU-DOM-15** — MUST. `Menu.LinkItem` renders `data-highlighted=""` when highlighted and no `data-disabled`.
  Source: `menu/link-item/MenuLinkItem.tsx:61`; `menu/link-item/MenuLinkItemDataAttributes.ts`. Test: [L2] `MenuLinkItem.test.tsx` describeConformance.
- **MNU-DOM-16** — MUST. `Menu.CheckboxItem` and `Menu.RadioItem` render exactly one of `data-checked=""` / `data-unchecked=""`, plus `data-highlighted` and `data-disabled` as for `Menu.Item`.
  Source: `menu/utils/stateAttributesMapping.ts:5-17`; `menu/checkbox-item/MenuCheckboxItem.tsx:76-79`; `menu/radio-item/MenuRadioItem.tsx:80-82`. Test: [L2] `MenuCheckboxItem.test.tsx` "adds the state and ARIA attributes when checked=$checked"; `MenuRadioItem.test.tsx` "adds the state and ARIA attributes when selected".
- **MNU-DOM-17** — MUST. The indicators render `data-checked`/`data-unchecked`, `data-disabled`, `data-highlighted` from the parent item and `data-starting-style`/`data-ending-style` from their own transition status (TRN-DOM-07); the element exists iff `keepMounted || mounted` (MNU-ANI-13).
  Source: `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:41-57`; `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:41-57`. Test: [L4] `MenuCheckboxItemIndicator.test.tsx` "keeps the indicator mounted to play its exit animation when unchecked without keepMounted"; `MenuRadioItemIndicator.test.tsx` same.
- **MNU-DOM-18** — MUST. `Menu.SubmenuTrigger` renders `data-popup-open=""` iff its submenu is open, `data-highlighted=""` iff highlighted in the parent list, `data-disabled=""` iff disabled, and `tabindex="0"` iff (the submenu is open or the trigger is highlighted) and the parent is not in virtual-focus mode, else `tabindex="-1"`.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:210,225,244-245`; `menu/submenu-trigger/MenuSubmenuTriggerDataAttributes.ts`. Test: [L2] `MenuSubmenuTrigger.test.tsx` "sets tabIndex to 0 on the submenu trigger after opening the submenu with a keydown event", "should render with disabled attributes when disabled prop is set"; `useMenuFilterSubmenuTrigger.test.tsx` "keeps real focus on the parent input until the submenu opens".

### 5.8 Group, label, list, filter and auxiliary elements

- **MNU-DOM-19** — MUST. `Menu.GroupLabel` renders its resolved `id` (MNU-API-48).
  Source: `menu/group-label/MenuGroupLabel.tsx:35`. Test: [L2] `MenuGroupLabel.test.tsx` "should reference the provided id in Group's `aria-labelledby`".
- **MNU-DOM-20** — MUST. In a filterable menu, `Menu.Group` and `Menu.RadioGroup` render the boolean `hidden` attribute while hidden (MNU-STA-24) and stay mounted; a consumer `hidden` prop is OR-ed in. Plain menus never set it.
  Source: `menu/filter-root/MenuFilterGroup.tsx:16-49`. Docs: `components/menu/page.mdx` §Matching items. Test: [L2] `MenuFilterGroup.test.tsx` "hides a radio group, label included, when the query filters out all of its items".
- **MNU-DOM-21** — MUST. `Menu.Input` renders `<input type="text" role="searchbox" inputmode="search" autocomplete="off" spellcheck="false" autocorrect="off" autocapitalize="none">` with its resolved `id`, `disabled` when disabled, `value` equal to the committed query (or the in-progress IME composition text), `aria-controls` = the List id, `aria-activedescendant` = the highlighted item's id (absent when none), no `aria-autocomplete`, and `data-highlighted=""` while its `highlighted` state is `true` (MNU-STA-22).
  Source: `filter-dropdown/input/FilterDropdownInput.tsx:44-53`; `menu/input/MenuInput.tsx:32-65`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "makes the menu filterable"; `MenuFilterRoot.react17.test.tsx` "omits partial ids during SSR and wires relationships after hydration"; `MenuList.test.tsx` "keeps virtual focus on the input without making the list tabbable".
- **MNU-DOM-22** — MUST. `Menu.Clear` renders only while the query is non-empty; when rendered it is a `<button type="button">` (native mode) with `tabindex="-1"`, `aria-hidden="true"`, `data-disabled` when disabled, and it is excluded from the tab order and the accessibility tree.
  Source: `filter-dropdown/clear/FilterDropdownClear.tsx:33,43-64`. Test: [L2] `MenuFilterParts.test.tsx` "excludes Clear from the tab order and accessibility tree"; `MenuFilterRoot.scoping.test.tsx` "skips Clear when tabbing out of the popup".
- **MNU-DOM-23** — MUST. `Menu.Empty` renders `<div role="status" aria-live="polite" aria-atomic="true">` only while the menu is empty (MNU-STA-23), and runs the initial live-region text mutation of UTL-A11Y-01..04 when it appears.
  Source: `filter-dropdown/empty/FilterDropdownEmpty.tsx:27-47`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "filters items by text and shows the empty state" (`getByRole('status')`).
- **MNU-DOM-24** — MUST. `Menu.List` renders its resolved `id`, `tabindex="-1"` (so a pointer open can focus it and scrollable lists are not Tab stops), and in a filterable menu resets the enclosing group registration for its children.
  Source: `menu/list/MenuList.tsx:40-44`; `filter-dropdown/list/FilterDropdownList.tsx:29-66`. Test: [L2] `MenuList.test.tsx` "keeps virtual focus on the input without making the list tabbable", "focuses the list rather than the presentational popup on a pointer open".
- **MNU-DOM-25** — MUST. The internal backdrop (MNU-STR-17) is `<div role="presentation" data-base-ui-inert="">` with a fixed full-viewport inline style, a `clip-path` cutout matching the trigger's (or menubar content's) bounding box so the trigger stays clickable, and `inert` while the menu is not open.
  Source: `menu/positioner/MenuPositioner.tsx:270-290`; `utils/InternalBackdrop.tsx:10-44`. Test: [L2] `MenuRoot.test.tsx` "should render an internal backdrop when `true`" (`previousElementSibling` has `role="presentation"`). (L4 for the `clip-path` cutout clause)
- **MNU-DOM-26** — MUST. The portal node carries the shared `data-base-ui-portal` marker and an id, and the Root's tree contains a visually hidden `<span aria-owns="<portal id>">` (role per MNU-STR-18) so the portaled popup is announced as owned by the trigger's context.
  Source: `floating-ui-react/components/FloatingPortal.tsx:49,268`; `menu/portal/MenuPortal.tsx:32-37`. Test: [L2] `MenuRoot.test.tsx` "renders root menu portal ownership without an accessibility role", "renders submenu portal ownership as an allowed menu child".
- **MNU-DOM-27** — MUST. `Menu.Separator` renders `data-orientation="horizontal|vertical"` (SEP-DOM-01).
  Source: `menu/index.parts.ts:19`; `separator/Separator.tsx:16-26`. Test: [L2] `MenuFilterParts.test.tsx` describeConformance(`<Menu.Separator />`).
- **MNU-DOM-28** — MUST. An item (Item, LinkItem, CheckboxItem, RadioItem, SubmenuTrigger) that the filter hides renders no element at all; it is not in the DOM, not in the composite list, and not reachable by navigation.
  Source: `menu/item/MenuItem.tsx:72-78`; `menu/link-item/MenuLinkItem.tsx:81-87`; `menu/checkbox-item/MenuCheckboxItem.tsx:117-131`; `menu/radio-item/MenuRadioItem.tsx:109-115`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:280-284`. Test: [L2] `MenuFilterRoot.test.tsx` "filters each menu item variant without changing its role".
- **MNU-DOM-29** — MUST. `Menu.RadioGroup` renders `data-disabled=""` when its `disabled` prop is `true` (generic projection of its state; not listed in the outline, §16 Q3).
  Source: `menu/radio-group/MenuRadioGroup.tsx:51-55`. Test: none.

### 5.9 ARIA

- **MNU-ARIA-01** — MUST. Every `Menu.Trigger` renders `aria-haspopup="menu"`; a trigger of a filterable menu renders `aria-haspopup="dialog"` instead (the filter popup is a dialog, MNU-ARIA-06). A consumer-supplied `aria-haspopup` wins in both cases.
  Source: `menu/trigger/MenuTrigger.tsx:240,257,260`; `menu/root/MenuRoot.tsx:654,663`; `menu/filter-root/MenuFilterDropdown.tsx:129`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "makes the menu filterable", "leaves a plain menu untouched", "announces a dialog from a detached trigger attached to a filterable root"; `useMenuFilterSubmenuTrigger.test.tsx` "lets a consumer override aria-haspopup on the root trigger".
- **MNU-ARIA-02** — MUST. The active trigger renders `aria-expanded` equal to the effective open state; every inactive trigger renders `aria-expanded="false"`. A vetoed controlled close keeps `"true"`.
  Source: `menu/root/MenuRoot.tsx:655,664`. Test: [L2] `MenuRoot.test.tsx` "returns focus on Escape after a controlled root ignores a Shift+Tab close"; `MenuRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests (`ariaExpanded: true`). (L4 for the detached-triggers suite clause)
- **MNU-ARIA-03** — MUST. A trigger renders `aria-controls` only while it owns the open popup (or is the lone registered trigger of an open popup, PSH-STA-10); its value is the `Menu.List` id when a List is mounted (updating when the List's id changes), else the Popup id. An inactive trigger and a closed menu render no `aria-controls`; an explicitly empty popup/list id yields none.
  Source: `menu/trigger/MenuTrigger.tsx:80,241`; `menu/store/MenuStore.ts:100-104`; `menu/list/MenuList.tsx:25-33`. Test: [L2] `MenuList.test.tsx` "takes the menu role and label from the popup", "updates the trigger when the open list changes its id", "does not point an inactive trigger at the list"; `MenuFilterRoot.react17.test.tsx` "omits partial ids during SSR and wires relationships after hydration".
- **MNU-ARIA-04** — MUST. Without a `Menu.List`, the Popup renders `role="menu"`, `aria-orientation="horizontal"` only when `orientation === 'horizontal'` (never `"vertical"`, the implicit default), and `aria-labelledby` per MNU-ARIA-19.
  Source: `menu/popup/MenuPopup.tsx:159-168`. Test: [L2] `MenuRoot.test.tsx` "sets aria-orientation on a horizontal popup", "does not render aria-orientation on a vertical popup"; `MenuPopup.test.tsx` "uses an aria-label instead of the trigger label", "uses aria-labelledby from a render element".
- **MNU-ARIA-05** — MUST. With a `Menu.List` mounted (plain menu), the Popup renders `role="presentation"` and no `aria-labelledby`/`aria-orientation`, and the List renders `role="menu"`, the conditional `aria-orientation` and the `aria-labelledby` of MNU-ARIA-19.
  Source: `menu/popup/MenuPopup.tsx:159-168`; `menu/list/MenuList.tsx:40-47`. Test: [L2] `MenuList.test.tsx` "takes the menu role and label from the popup", "renders the orientation on the list".
- **MNU-ARIA-06** — MUST. In a filterable menu the Popup renders `role="dialog"` (with `aria-labelledby` per MNU-ARIA-19 and no `aria-activedescendant`/`aria-orientation`), the List renders `role="menu"` with `aria-labelledby` resolved against the trigger id and the conditional `aria-orientation`, and the Input is a `searchbox` inside the dialog.
  Source: `menu/filter-root/MenuFilterPopup.tsx:52-56`; `menu/filter-root/useMenuFilterPopup.ts:97-101`; `menu/filter-root/MenuFilterList.tsx:124-135`. Docs: `components/menu/page.mdx` §Focus and keyboard. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "makes the menu filterable"; `MenuList.test.tsx` "exposes horizontal navigation on the menu instead of the dialog"; `MenuFilterRoot.test.tsx` "uses dialog semantics for a filterable submenu", "accessible names" (6 tests).
- **MNU-ARIA-07** — MUST. `Menu.Item` and `Menu.LinkItem` render `role="menuitem"`; disabled items render `aria-disabled="true"` (UBT-ARIA-01/02) and no `disabled` attribute unless the rendered element is a native button.
  Source: `menu/item/useMenuItemCommonProps.ts:80`; `menu/item/useMenuItem.ts:29-34`. Test: [L2] `MenuRoot.test.tsx` "includes disabled items during keyboard navigation" (`aria-disabled="true"`); `MenuItem.test.tsx` describeConformance.
- **MNU-ARIA-08** — MUST. `Menu.CheckboxItem` renders `role="menuitemcheckbox"` and `aria-checked="true"|"false"`.
  Source: `menu/checkbox-item/MenuCheckboxItem.tsx:82-83`. Test: [L2] `MenuCheckboxItem.test.tsx` "adds the state and ARIA attributes when checked=$checked".
- **MNU-ARIA-09** — MUST. `Menu.RadioItem` renders `role="menuitemradio"` and `aria-checked="true"|"false"`.
  Source: `menu/radio-item/MenuRadioItem.tsx:86-87`. Test: [L2] `MenuRadioItem.test.tsx` "adds the state and ARIA attributes when selected".
- **MNU-ARIA-10** — MUST. `Menu.Group` and `Menu.RadioGroup` render `role="group"` and `aria-labelledby` = the id of their mounted `Menu.GroupLabel` (a consumer `aria-labelledby` wins on RadioGroup); when the label unmounts the reference is removed only if it still points at that label (a newer label's id is kept). `Menu.RadioGroup` additionally renders `aria-disabled="true"` when disabled. A `Menu.Group` substituted with a `Menu.RadioGroup` (`render={<Menu.RadioGroup/>}`) still labels correctly.
  Source: `menu/group/MenuGroup.tsx:17-28`; `menu/radio-group/MenuRadioGroup.tsx:27-31,56-60,73-77`; `menu/group-label/MenuGroupLabel.tsx:25-30`. Test: [L2] `MenuGroup.test.tsx` "renders a div with the `group` role"; `MenuRadioGroup.test.tsx` same; `MenuGroupLabel.test.tsx` "a11y attributes" (8 tests).
- **MNU-ARIA-11** — MUST. `Menu.GroupLabel` renders `aria-hidden="true"` by default (the group's accessible name comes from `aria-labelledby`); a consumer may override it (`aria-hidden={undefined}` removes it).
  Source: `menu/group-label/MenuGroupLabel.tsx:36-37`. Test: [L2] `MenuGroupLabel.test.tsx` "is hidden from the accessibility tree by default", "allows overriding aria-hidden".
- **MNU-ARIA-12** — MUST. `Menu.Arrow`, `Menu.CheckboxItemIndicator` and `Menu.RadioItemIndicator` render `aria-hidden="true"`; `Menu.Backdrop` and the internal backdrop render `role="presentation"`; `Menu.Separator` renders `role="separator"` and `aria-orientation` equal to its `orientation` (default `"horizontal"`) (SEP-ARIA-01, SEP-ARIA-02).
  Source: `menu/arrow/MenuArrow.tsx:39`; `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:53`; `menu/radio-item-indicator/MenuRadioItemIndicator.tsx:53`; `menu/backdrop/MenuBackdrop.tsx:44`; `menu/index.parts.ts:19`; `separator/Separator.tsx:20-24`. Test: [L2] describeConformance suites.
- **MNU-ARIA-13** — MUST. `Menu.SubmenuTrigger` renders `role="menuitem"`, `aria-haspopup="menu"` (`"dialog"` when its own submenu is filterable; a consumer value wins), `aria-expanded` = submenu open state, and `aria-controls` = the submenu popup id while the submenu is open (or the trigger is the lone trigger per PSH-STA-10). Under VoiceOver (`platform.screenReader.voiceOver`, i.e. Apple platforms) `aria-expanded` is omitted while the submenu is open and was opened from the keyboard (`list-navigation` reason or keyboard open method), so the announcement of the newly focused submenu item is not overridden; pointer opens keep `aria-expanded="true"`.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:212-219,239-242`; `menu/filter-root/useMenuFilterSubmenuTrigger.ts:59-62`. Docs: `CHANGELOG.md v1.7.0 "Fix VoiceOver announcement when opening a submenu"`. Test: [L2] `MenuSubmenuTrigger.voiceOver.test.tsx` (3 tests); `MenuFilterRoot.scoping.test.tsx` "filters a submenu that uses FilterSubmenuRoot" (`dialog`); `useMenuFilterSubmenuTrigger.test.tsx` "accessible semantics of a plain submenu" (3 tests); `MenuFilterRoot.test.tsx` "uses dialog semantics for a filterable submenu". (L4 for the VoiceOver `aria-expanded` omission clause)
- **MNU-ARIA-14** — MUST. `Menu.Input` renders `role="searchbox"`, `aria-controls` = the List id, and `aria-activedescendant` = the highlighted item's rendered id while an item is highlighted (reflecting item id changes), absent otherwise; the dialog itself never carries `aria-activedescendant`.
  Source: `filter-dropdown/input/FilterDropdownInput.tsx:68-79`; `menu/input/MenuInput.tsx:33,61`; `menu/store/MenuStore.ts:112`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "navigates the list from the input with aria-activedescendant"; `MenuList.test.tsx` "reads an item id when navigating to it"; `MenuFilterRoot.test.tsx` "filters items and selects the active item while focus remains on the input".
- **MNU-ARIA-15** — MUST. On WebKit engines only, while the menu's `Menu.Input` has focus, the highlighted item (Item, LinkItem, CheckboxItem, RadioItem, SubmenuTrigger — the latter judged against the *parent* input) renders `aria-selected="true"`; every other item, every item while the input is blurred or unmounted, and every item of a plain menu render no `aria-selected`.
  Source: `menu/store/MenuStore.ts:107-111`; `menu/item/useMenuItemCommonProps.ts:63-68,82`; `menu/input/MenuInput.tsx:36-53`. Test: [L4] `MenuFilterRoot.webkit.test.tsx` (9 tests); `MenuFilterRoot.test.tsx` "does not set aria-selected on highlighted items outside WebKit".
- **MNU-ARIA-16** — MUST. `Menu.Clear` renders `aria-hidden="true"`; `Menu.Empty` renders `role="status" aria-live="polite" aria-atomic="true"`.
  Source: `filter-dropdown/clear/FilterDropdownClear.tsx:49-50`; `filter-dropdown/empty/FilterDropdownEmpty.tsx:40-43`. Test: [L2] `MenuFilterParts.test.tsx` "excludes Clear from the tab order and accessibility tree"; `MenuFilterRoot.scoping.test.tsx` "filters items by text and shows the empty state".
- **MNU-ARIA-17** — MUST. Inside a Menubar, `Menu.Trigger` renders `role="menuitem"` (as a composite item of the bar); outside one it has no role in native mode and `role="button"` in non-native mode (UBT-DOM-02), including when rendered inside another popup such as a Popover.
  Source: `menu/trigger/MenuTrigger.tsx:258,272-285`. Test: [L2] `MenuTrigger.test.tsx` "does not have role prop inside a Popover", "has a role prop inside a Popover when not a native button".
- **MNU-ARIA-18** — MUST. The hidden `aria-owns` owner span (MNU-DOM-26) carries `role="group"` for submenus and Menubar menus and no role for a top-level menu.
  Source: `menu/portal/MenuPortal.tsx:32`. Test: [L2] `MenuRoot.test.tsx` "renders submenu portal ownership as an allowed menu child".
- **MNU-ARIA-19** — MUST. The accessible label of the Popup (plain menu, `role="menu"`), of the filter dialog, and of the List is resolved as: an explicit `aria-label`/`aria-labelledby` prop or one on the part's `render` element takes precedence (then no `aria-labelledby` is generated); otherwise `aria-labelledby` is the *rendered* id of the active trigger element, falling back to the active trigger id while the element is unknown; a trigger rendered with an explicitly empty id, or a menu with no trigger, yields no `aria-labelledby` (UTL-ARIA-01).
  Source: `internals/resolvePopupLabel.ts:10-30`; `menu/popup/MenuPopup.tsx:78,167`; `menu/list/MenuList.tsx:35,47`; `menu/filter-root/MenuFilterList.tsx:124-130`. Test: [L2] `MenuPopup.test.tsx` "uses an aria-label instead of the trigger label", "uses aria-labelledby from a render element"; `MenuFilterRoot.test.tsx` "accessible names" (6 tests), "uses the id from a custom trigger render element for popup labelling", "uses an explicit popup label instead of the trigger label", "uses an explicit label from the popup render element".

## 6. Interactions

Vocabulary: *main axis* = `ArrowUp`/`ArrowDown` for `orientation: 'vertical'`, `ArrowLeft`/`ArrowRight` for `'horizontal'`; *end key* = `ArrowDown` (vertical) / `ArrowRight` in LTR, `ArrowLeft` in RTL (horizontal); *start key* = the opposite; *submenu open key* = `ArrowRight` (LTR) / `ArrowLeft` (RTL) for a vertical parent, `ArrowDown` for a horizontal parent; *submenu close key* = `ArrowLeft` (LTR) / `ArrowRight` (RTL) for a vertical submenu, `ArrowUp` for a horizontal submenu (predicates MAIN/TOEND/XOPEN/XCLOSE of CMP-ALG-20, RTL per CMP-I18N-04). Source: `floating-ui-react/hooks/useListNavigation.ts:56-99`.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| main-axis arrow | closed Trigger (focused), no Menubar | Opens (`list-navigation`), highlights first item (end key / Enter / Space) or last item (start key); `preventDefault` | MNU-KEY-01, MNU-KEY-02 |
| ArrowDown (horizontal bar) / ArrowRight LTR, ArrowLeft RTL (vertical bar) | closed Trigger inside a Menubar | Opens (`list-navigation`), highlights the first item | MNU-KEY-01, MNU-KEY-02 |
| ArrowUp (horizontal bar) / the opposite cross-axis arrow (vertical bar) | Trigger inside a Menubar | Not handled: does not open the menu (MBR-KEY-05) | MNU-KEY-01 |
| any arrow | closed Trigger under a ContextMenu | Not handled (`openOnArrowKeyDown: false`) | MNU-KEY-01 |
| Enter, Space | Trigger | Opens via click (`trigger-press`, keyboard click `detail: 0`) and highlights the first item; toggles closed when open | MNU-KEY-03 |
| main-axis arrow | open Popup, item focused | Moves highlight/focus to adjacent item, wrapping per `loopFocus`; `preventDefault`+`stopPropagation` | MNU-KEY-04 |
| Home / End | open Popup | First / last item, including `disabled`-prop items (`disabledIndices: []`) | MNU-KEY-05, MNU-KEY-34 |
| main-axis arrow | open Popup, focus on non-item content (List, custom button, link) | Enters the list at the boundary item | MNU-KEY-06 |
| cross-axis arrow | open Popup, no submenu trigger highlighted | Not handled | MNU-KEY-04 |
| Enter, Space | highlighted item | Activates (synthetic click), closes if `closeOnClick` | MNU-KEY-08 |
| Space | item, during a typeahead session | Continues the typeahead; does not activate | MNU-KEY-13 |
| printable characters | open Popup / Trigger | Typeahead | MNU-KEY-12 |
| Escape | open Popup (or anywhere in the document) | Closes the innermost open menu, focus to its trigger | MNU-KEY-09, MNU-KEY-19 |
| Tab | open Popup | Not intercepted; focus leaves through the guard, menu closes (`focus-out`) | MNU-KEY-10 |
| Shift+Tab | open Popup (also under a ContextMenu) | Closes (`focus-out`), focus to trigger; `preventDefault` | MNU-KEY-11, MNU-FOC-13 |
| Shift+Tab | focused open Trigger | Closes, focus to previous tabbable (via the pre-trigger guard) | MNU-FOC-06 |
| submenu open key | SubmenuTrigger | Opens submenu (`list-navigation`), first item focused; re-enters when already open | MNU-KEY-15 |
| submenu close key | inside submenu Popup | Closes submenu (`list-navigation`), focus to SubmenuTrigger | MNU-KEY-16 |
| Enter, Space | SubmenuTrigger | Toggles the submenu (keeps a hover-opened one open) | MNU-KEY-17 |
| other keys (Home, End, letters) | SubmenuTrigger | Handled by the parent list | MNU-KEY-18 |
| Arrow keys, Home, End | Popup inside a Toolbar (only there) | Propagation stopped so the toolbar does not move | MNU-KEY-20 |
| any key | Popup of a menu with a detached trigger inside a Menubar | Relayed to the menubar's composite root | MNU-KEY-21 |
| see §6.1.1 | `Menu.Input` (filterable menu) | | MNU-KEY-22..33 |

- **MNU-KEY-01** — MUST. The Root passes these list-navigation options for the trigger: `openOnArrowKeyDown` = `false` under a ContextMenu parent (and for a filterable submenu, whose keyboard opening is orchestrated by its navigation wrapper, MNU-KEY-29), else `true`; `triggerOrientation` = the Root `orientation` (always `'vertical'` for a filterable menu); `rtl` from the reading direction; and `nested` = `true` under a Menubar (with `parentOrientation` = the bar's orientation) and for every plain menu that has a parent kind, else `false`. The observable trigger key handling is per CMP-KEY-19, CMP-KEY-21, CMP-KEY-22 (a non-nested trigger opens on a main-axis arrow of `triggerOrientation` and consumes the key) and CMP-KEY-23 (a nested trigger opens only on the parent's cross-axis open key), with the open details per CMP-EVT-06 (reason `list-navigation`, `event` = the keydown, `trigger` = the trigger element). Consequences for Menu: under a ContextMenu arrow keys never open; a trigger in a horizontal Menubar opens only with ArrowDown — ArrowUp is not handled and does nothing — and one in a vertical Menubar only with ArrowRight (LTR) / ArrowLeft (RTL) (MBR-KEY-05).
  Source: `menu/root/MenuRoot.tsx:516-546`; `floating-ui-react/hooks/useListNavigation.ts:815-914`; see CMP-KEY-19, CMP-KEY-21, CMP-KEY-22, CMP-KEY-23, CMP-EVT-06. Test: [L2] `MenuTrigger.test.tsx` "opens the menu when pressing $key on a $buttonType button"; `MenuRoot.test.tsx` "focuses the first item when down arrow key opens the menu", "focuses the last item when up arrow key opens the menu". (L4 for the observable trigger key handling)
- **MNU-KEY-02** — MUST. After a keyboard open (arrow, Enter, Space, or any `detail === 0` click) the initial highlight is set by the list-navigation initial sync on open (CMP-STA-37; the items are looked for immediately, after a microtask and on the next frame, CMP-ALG-24, CMP-FOC-09): the last navigable item when the opening key was the start key of `triggerOrientation` (ArrowUp for a vertical menu, ArrowLeft in LTR for a horizontal one), otherwise the first navigable item; a submenu (nested list) always starts at its first item. Because the initial sync ignores `disabledIndices`, "navigable" here skips items carrying a `disabled` attribute or `aria-disabled="true"` and items hidden with CSS, even though arrow keys and Home/End later reach attribute-disabled items (MNU-KEY-34).
  Source: `floating-ui-react/hooks/useListNavigation.ts:440-477`; `menu/root/MenuRoot.tsx:521,527`; see CMP-STA-37, CMP-ALG-24, CMP-KEY-45. Test: [L4] `MenuRoot.test.tsx` "focuses the first item after the menu is opened by keyboard", "highlights the first item when the items render a microtask after a keyboard opening", "moves focus to the first item after a screen reader $name"; `MenuFilterRoot.initialHighlight.test.tsx` "highlights the first enabled action and keeps the input in the arrow loop".
- **MNU-KEY-03** — MUST. Enter or Space on a focused Trigger activates it like a click (native button: browser activation; non-native: UBT-KEY-01/02): the click handler opens with reason `trigger-press` and a `detail === 0` click, so `keyboardOpen` is `true`, the first item is highlighted and focused, and the popup is marked `data-instant="click"`. When the menu is already open the same keys close it (toggle), except within 500 ms of a hover open (MNU-PTR-06).
  Source: `menu/trigger/MenuTrigger.tsx:197-203`; `floating-ui-react/hooks/useClick.ts:187-219`; `menu/root/MenuRoot.tsx:420,431-432`. Test: [L4] `MenuRoot.test.tsx` "changes the highlighted item using the arrow keys" (opens with Enter), "closes with a `detail === 0` click event on keyboard item activation"; `MenuPopupEnterTransition.test.tsx` "marks an initially open submenu as instant when its parent opened instantly".
- **MNU-KEY-04** — MUST. The Root passes `orientation` (MNU-API-09), `loopFocus` (MNU-API-06), `rtl`, `disabledIndices: []` (MNU-KEY-34) and, for a plain menu, `virtual: false` to list navigation, so in an open plain menu a main-axis arrow key on the popup moves the highlight and real focus to the adjacent navigable item per CMP-KEY-30 and CMP-KEY-32 (wrapping with `loopFocus`, staying at the end otherwise; the key is always default-prevented and propagation-stopped). Menu-specific results: each move is reported through `onItemHighlighted` with reason `keyboard` (MNU-EVT-11); cross-axis arrows are not handled by the list of a menu without a parent kind (they act only on a submenu trigger, MNU-KEY-15, or inside a submenu, MNU-KEY-16); IME keydowns (`which === 229`) and keys arriving while the popup animates out are ignored (CMP-KEY-25, CMP-KEY-26).
  Source: `menu/root/MenuRoot.tsx:516-546`; `floating-ui-react/hooks/useListNavigation.ts:551-685`; see CMP-KEY-25, CMP-KEY-26, CMP-KEY-30, CMP-KEY-32. Test: [L2] `MenuRoot.test.tsx` "changes the highlighted item using the arrow keys", "navigates across grouped items with arrow keys and text", "skips null items when navigating" (loops back to first); `MenuRoot.test.tsx` popupListConformanceTests. (L4 for the arrow-key movement and focus clauses)
- **MNU-KEY-05** — MUST. Home and End on the open popup move the highlight to the first / last navigable item regardless of orientation and are consumed, per CMP-KEY-28 computed with Menu's `disabledIndices: []`, so they reach an end item that is disabled through its `disabled` prop (MNU-KEY-34, CMP-KEY-52).
  Source: `floating-ui-react/hooks/useListNavigation.ts:600-615`; `menu/root/MenuRoot.tsx:534`; see CMP-KEY-28, CMP-KEY-52. Test: [L4] `MenuRoot.test.tsx` "changes the highlighted item using the Home and End keys"; `MenuFilterRoot.test.tsx` "uses Home and End for list navigation after an item is highlighted".
- **MNU-KEY-06** — MUST. Per CMP-KEY-31 (the popup uses DOM focus): when a main-axis key is pressed while focus is inside the popup but on an element that is not an item (the List, a custom button, a link in the popup), the highlight jumps to the boundary item — first for the end key, last for the start key — and focus is moved there even when that item was already highlighted.
  Source: `floating-ui-react/hooks/useListNavigation.ts:644-667`; see CMP-KEY-31. Test: [L4] `MenuPopup.test.tsx` "enters the items at the list boundary when an arrow key leaves custom popup content", "moves focus into the items when the boundary item is already highlighted", "moves focus into the items when the last item is already highlighted"; `MenuList.test.tsx` "enters the list from the end with ArrowUp after a pointer open without looping".
- **MNU-KEY-07** — MUST. Arrow navigation (CMP-KEY-32 with `disabledIndices: []`, CMP-ALG-02) includes items disabled through the `disabled` prop (they render `aria-disabled="true"` and are focusable-when-disabled, CMP-KEY-51) but skips items that are hidden (`display: none`, `visibility: hidden`, or otherwise not rendered) and items whose rendered element is natively `:disabled`.
  Source: `menu/root/MenuRoot.tsx:534`; `floating-ui-react/utils/composite.ts:520-553`; see CMP-ALG-02, CMP-KEY-51. Test: [L4] `MenuRoot.test.tsx` "includes disabled items during keyboard navigation", "skips items hidden with CSS during keyboard navigation"; `MenuItem.test.tsx` "skips a natively disabled item during keyboard navigation".
- **MNU-KEY-08** — MUST. Enter (keydown) and Space (keydown, composite mode; UBT-KEY-01/12) on the focused highlighted item dispatch a synthetic click (`detail: 0`) on it: the consumer `onClick` runs, a checkbox/radio item toggles/selects, and when `closeOnClick` is `true` the whole menu tree closes with reason `item-press` and that click as `event` (MNU-DIS-06). On a disabled item nothing happens.
  Source: `menu/item/useMenuItem.ts:29-34`; `menu/item/useMenuItemCommonProps.ts:102-106`. Test: [L4] `MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation"; `MenuCheckboxItem.test.tsx` "toggles the checked state when Space is pressed", "toggles the checked state when Enter is pressed"; `MenuRadioItem.test.tsx` "selects the item when $key is pressed".
- **MNU-KEY-09** — MUST. The Root passes `escapeKey` (default `true`), `enabled: !disabled` and `bubbles: { escapeKey: closeParentOnEsc && parent kind === 'menu' }` to the dismiss mechanism; Escape handling is per FLT-KEY-01 (a document-level keydown, so it works wherever focus is), FLT-KEY-02 (a menu with an open child that does not bubble Escape does not close itself, EVD-DIS-02) and FLT-KEY-03 (ignored while an IME composition is in progress). Menu-specific results: the innermost open menu closes with reason `escape-key` and the keydown as `event`; the keydown is default-prevented when the close is not canceled and its propagation is stopped (EVD-EVT-11, EVD-DIS-01, FLT-EVT-05). Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:501-512`; `floating-ui-react/hooks/useDismiss.ts:201-232,719-724`; see FLT-KEY-01, FLT-KEY-02, FLT-KEY-03, FLT-EVT-05. Test: [L4] `MenuRoot.test.tsx` "does not close the parent menu when the Escape key is pressed by default"; `MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s".
- **MNU-KEY-10** — MUST. Tab from inside an open top-level menu is not intercepted: sequential focus moves to the focus guard after the trigger, which closes the menu with reason `focus-out` (`trigger` = the guard) and moves focus to the next tabbable element after the trigger (resolved after the close commits, excluding the positioner; PSH focus guards). The same happens from a filterable popup, and `Menu.Clear` is never a Tab stop.
  Source: `menu/trigger/MenuTrigger.tsx:224-227,299-303`; `menu/popup/MenuPopup.tsx:210-213`; `utils/popups/useTriggerFocusGuards.ts:38-69`; see PSH-KEY-01, PSH-KEY-02, PSH-DIS-04. Test: [L4] `MenuRoot.test.tsx` "closes the menu and moves focus to the next element when tabbing forward from the open menu"; `MenuFilterRoot.scoping.test.tsx` "lets Tab leave a modal filterable popup and closes it", "skips Clear when tabbing out of the popup", "lets Tab leave the popup with modal={false}"; `MenuFilterRoot.test.tsx` "moves focus past the menu when tabbing from the input".
- **MNU-KEY-11** — MUST. With focus inside an open plain popup (DOM focus, not virtual), Shift+Tab is handled by list navigation per CMP-KEY-24 and CMP-EVT-08: it is consumed and closes the menu with reason `focus-out`; when not canceled, focus moves to the return element, which Menu sets through `nestedReturnFocusRef` to the parent's input for a plain submenu under a filterable parent and otherwise leaves as the trigger (a submenu's own submenu trigger). When canceled, focus stays in the popup and the trigger keeps `aria-expanded="true"`. If the keydown target is outside the popup's focus element (e.g. inside a nested Dialog opened from an item), nothing happens. This applies under a ContextMenu as well (MNU-FOC-13).
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`; `menu/root/MenuRoot.tsx:543`; see CMP-KEY-24, CMP-EVT-08. Test: [L4] `MenuRoot.test.tsx` "closes the menu and moves focus to the trigger when shift-tabbing from the open menu", "keeps focus in the menu when a Shift+Tab close is canceled", "closes submenus when focus is lost by shift-tabbing from a nested menu", "keeps the menu and dialog open when pressing Shift+Tab inside a nested dialog"; `MenuFilterRoot.scoping.test.tsx` "returns to its own trigger on Shift+Tab".
- **MNU-KEY-12** — MUST. The Root passes the item labels and elements, `activeIndex`, `resetMs: 500` (instead of the 750 ms default) and `enabled: !disabled && !virtualFocus` to typeahead, whose matching is per CMP-KEY-35 … CMP-KEY-43 and CMP-STA-42 … CMP-STA-45: printable characters (single-character keys without Ctrl/Meta/Alt) typed while the menu is open — on the trigger or in the popup — accumulate into a buffer that resets 500 ms after the last character; the next item after the current highlight (cycling) whose label starts with the buffer (case-insensitive, exact code-unit comparison so diacritics match only themselves) is matched. Items hidden with CSS, natively disabled items and items without text are skipped; repeated same first letter cycles through matches; a buffer that no longer matches is cleared. Labels come from `label` or text content (CMP-STA-06). Menu-specific results: a match while open highlights the item with reason `keyboard` and the keydown as event, and focuses it; keydowns that start a session are default-prevented and propagation-stopped when the menu is open.
  Source: `menu/root/MenuRoot.tsx:555-568`; `internals/constants.ts:3`; `floating-ui-react/hooks/useTypeahead.ts:93-204`; see CMP-KEY-35, CMP-KEY-40, CMP-KEY-42, CMP-STA-43. Test: [L2] `MenuRoot.test.tsx` "text navigation" (12 tests); `MenuSubmenuTrigger.test.tsx` "uses the label prop for text navigation". (L4 for the typeahead matching clauses)
- **MNU-KEY-13** — MUST. While a typeahead session is in progress (`typingRef`), Space continues the session and MUST NOT activate the focused item: the keydown is default-prevented on every item part (Item, LinkItem, CheckboxItem, RadioItem, SubmenuTrigger), so no click, toggle, navigation or submenu open occurs. Once the session has ended (500 ms, or a non-matching key), Space activates again.
  Source: `menu/item/useMenuItemCommonProps.ts:85-89`; `menu/root/MenuRoot.tsx:548-553,567`. Test: [L4] `MenuRoot.test.tsx` "does not trigger the onClick event when Space is pressed during text navigation", "does not open a submenu when pressing Space during a typeahead session", "opens a focused submenu trigger with Space when not typing"; `MenuLinkItem.test.tsx` "does not navigate when Space is pressed during an active typeahead session"; `MenuCheckboxItem.test.tsx` "does not toggle when Space is pressed during an active typeahead session", "toggles with Space after closing during typeahead and reopening"; `MenuRadioItem.test.tsx` "does not select when Space is pressed during an active typeahead session".
- **MNU-KEY-14** — MUST. Typeahead is disabled while the Root is `disabled` (a typed letter neither highlights nor focuses) and in filterable menus (the input owns typing); its buffer is cleared when the menu closes or when focus leaves both trigger and popup.
  Source: `menu/root/MenuRoot.tsx:557`; `floating-ui-react/hooks/useTypeahead.ts:206-237`; see CMP-STA-44, CMP-STA-45. Test: [L4] `MenuRoot.test.tsx` "does not highlight items with text navigation when controlled open"; `MenuFilterRoot.test.tsx` "does not move the highlight while typing into the input".
- **MNU-KEY-15** — MUST. A `Menu.SubmenuTrigger` is the trigger of its submenu's nested list (the submenu Root passes `nested: true`; the parent's orientation is read from the parent's published list-navigation data, CMP-STA-41), so its keydown is handled per CMP-KEY-23: the submenu open key on a focused/highlighted submenu trigger of a closed submenu opens it with reason `list-navigation` (`trigger` = the submenu trigger, CMP-EVT-06) and stops the event; the submenu's first navigable item is highlighted and focused once rendered (MNU-KEY-02), and the submenu trigger becomes the Tab stop (`tabindex="0"`) of the parent. When the submenu is already open, the same key re-enters it at the first index computed with `disabledIndices: []`, without reopening — unlike the initial open, this does not skip a first item disabled through its `disabled` prop (MNU-KEY-34). RTL flips the horizontal keys; a horizontal parent uses ArrowDown.
  Source: `floating-ui-react/hooks/useListNavigation.ts:842-893`; `menu/root/MenuRoot.tsx:516-546`; see CMP-KEY-23, CMP-STA-41, CMP-EVT-06. Test: [L4] `MenuRoot.test.tsx` "opens a nested menu of a $orientation $direction menu with $openKey key and closes it with $closeKey"; `MenuSubmenuTrigger.test.tsx` "opens the submenu with $openKey and highlights a single item in $direction direction", "sets tabIndex to 0 on the submenu trigger after opening the submenu with a keydown event"; `MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local".
- **MNU-KEY-16** — MUST. Inside an open submenu the submenu close key is handled per CMP-KEY-27, CMP-DIS-01 and CMP-EVT-07: it closes the submenu with reason `list-navigation` and moves focus to the return element — the submenu trigger (which stays highlighted), or the parent's input for a plain submenu under a filterable parent (`nestedReturnFocusRef`). The keydown is default-prevented and propagation-stopped unless the key is also a main-axis key of the parent (e.g. ArrowUp closing a horizontal-parent submenu's vertical child), in which case it propagates so the parent navigates as well.
  Source: `floating-ui-react/hooks/useListNavigation.ts:570-585`; `menu/root/MenuRoot.tsx:543`; see CMP-KEY-27, CMP-DIS-01, CMP-EVT-07. Test: [L4] `MenuRoot.test.tsx` "opens a nested menu of a $orientation $direction menu with $openKey key and closes it with $closeKey", "returns focus to submenu triggers when closing nested menus"; `MenuFilterSubmenuRoot.test.tsx` "moves a horizontal parent on when a vertical submenu closes on its main-axis key (filterable parent: $parentFilterable, filterable submenu: $submenuFilterable)".
- **MNU-KEY-17** — MUST. Enter and Space on a focused `Menu.SubmenuTrigger` activate it as a click (`detail: 0`): a closed submenu opens with reason `trigger-press` (first item focused), an open submenu closes (toggle), except that the first activation after a *hover* open keeps it open (`stickIfOpen`).
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:193-204`. Test: [L4] `MenuSubmenuTrigger.test.tsx` "keeps a hover-opened submenu open when pressing %s"; `MenuSubmenuTrigger.voiceOver.test.tsx` "omits the expanded state when the submenu is opened with Enter"; `MenuRoot.test.tsx` "opens a focused submenu trigger with Space when not typing".
- **MNU-KEY-18** — MUST. Keys on a submenu trigger that are neither the submenu open key nor an activation key are not consumed by the submenu; the parent list handles them (main-axis arrows move in the parent, Home/End jump, letters run the parent's typeahead).
  Source: `floating-ui-react/hooks/useListNavigation.ts:877-893`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:240-260`. Test: [L4] `MenuRoot.test.tsx` "matches a submenu trigger label after a space + numeric suffix"; `MenuFilterSubmenuRoot.test.tsx` "leaves a key that is neither an axis nor an activation key to the parent", "moves along the parent list with the main-axis keys".
- **MNU-KEY-19** — MUST. Escape inside a submenu closes only that submenu (reason `escape-key`) and focuses its trigger, leaving the parent open; with `closeParentOnEsc` on the submenu root, the keydown is not stopped at the submenu and the parent (and its ancestors with the same setting) close too.
  Source: `menu/root/MenuRoot.tsx:503`. Test: [L4] `MenuRoot.test.tsx` "does not close the parent menu when the Escape key is pressed by default", "closes the parent menu when the Escape key is pressed  if `closeParentOnEsc=true`".
- **MNU-KEY-20** — MUST. When the menu is rendered inside a `Toolbar`, keydowns of `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home`, `End` on the popup have their propagation stopped so the toolbar's composite navigation does not run; other keys propagate normally. Outside a Toolbar `Menu.Popup` adds no such stopping (this is the Menu side of TLB-KEY-15).
  Source: `menu/popup/MenuPopup.tsx:52,169-173`; `internals/composite/composite.ts:22`. Test: [L4] `MenuPopup.test.tsx` "stops toolbar navigation keys without blocking ordinary key events".
- **MNU-KEY-21** — MUST. A popup keydown whose propagation was not stopped is relayed to the keyboard relay published by a Menubar's composite root through the trigger (so detached triggers inside a Menubar still let the bar move between menus); the relay is inherited by submenus.
  Source: `menu/root/MenuRoot.tsx:692-700`; `menu/trigger/MenuTrigger.tsx:106`; `menu/store/MenuStore.ts:138-148`. Test: none in `menu/` (Menubar tests).
- **MNU-KEY-34** — MUST. The Root passes `disabledIndices: []` (an empty array, never a predicate and never omitted) to list navigation. Observable consequence: because a provided `disabledIndices` turns off attribute-based disabled detection (CMP-ARIA-03, CMP-ALG-02), arrow keys (MNU-KEY-07), Home/End (MNU-KEY-05) and re-entering an open submenu with its open key (MNU-KEY-15) reach items that are disabled through their `disabled` prop (`aria-disabled="true"`), while the initial highlight of a keyboard open, which list navigation computes without `disabledIndices` (CMP-STA-37), skips them (MNU-KEY-02). With item 0 disabled, ArrowDown on the trigger therefore opens with item 1 highlighted, after which Home highlights item 0. Hidden and natively `:disabled` items are skipped in every case.
  Source: `menu/root/MenuRoot.tsx:534`; `floating-ui-react/hooks/useListNavigation.ts:460-470,547-549,600-601`; `floating-ui-react/utils/composite.ts:520-553`; see CMP-STA-37, CMP-KEY-28, CMP-KEY-45, CMP-KEY-51, CMP-KEY-52. Test: [L2] `MenuRoot.test.tsx` popupListConformanceTests "stops on a disabled item while navigating" and the disabled-first-item / Home-End cases (CMP-KEY-45, CMP-KEY-52); `MenuRoot.test.tsx` "includes disabled items during keyboard navigation". (L4 for the observable consequence)

#### 6.1.1 Keys in a filterable menu

While the popup is open the `Menu.Input` holds real focus and every key it receives is classified by the following table (`getMenuFilterKeyAction`), evaluated in order. *highlighted* = an item is highlighted; *submenu trigger* = the highlighted item has `aria-haspopup`; *has text* = the input value is non-empty.

| Condition (first match wins) | Action | Req |
|---|---|---|
| `which === 229` (IME composition) | `ignore` | MNU-KEY-31 |
| Tab | `ignore` (Shift+Tab → `close`) | MNU-KEY-26 |
| Enter | `activate` if highlighted, else `navigate` | MNU-KEY-25 |
| any modifier (Shift/Ctrl/Alt/Meta) held | `edit` for arrows, Home/End and single characters; otherwise `navigate` | MNU-KEY-30 |
| single character (incl. Space) | `edit` | MNU-KEY-22 |
| Home / End | `navigate` if highlighted and no text, else `edit` | MNU-KEY-23 |
| non-arrow key (Escape, …) | `navigate` | MNU-KEY-26 |
| main-axis arrow | `edit` when horizontal, nothing highlighted and has text; else `navigate` | MNU-KEY-23 |
| cross-axis arrow that is the submenu open or close key, submenu trigger highlighted | `submenu` | MNU-KEY-29 |
| cross-axis arrow, horizontal menu, nothing highlighted | `enter-list` | MNU-KEY-24 |
| cross-axis arrow otherwise | `edit` if has text, else `navigate` | MNU-KEY-30 |

- **MNU-KEY-22** — MUST. `edit` keys are left to the input's native editing (propagation stopped so parent popups/typeahead never see them); a printable `edit` key additionally ends pointer modality so a result change that moves the highlighted item out from under a resting pointer does not clear the highlight.
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:52-109,137-145`. Test: [L4] `useMenuFilterKeyDown.test.ts` (every row); `MenuFilterRoot.test.tsx` "keeps the automatic highlight when typing moves it out from under the pointer", "leaves modified editing keys to the input while an item is highlighted".
- **MNU-KEY-23** — MUST. `navigate` runs the menu's list-navigation key handler on the input: the main-axis keys move the virtual highlight (`aria-activedescendant`), and the key's propagation is stopped so the popup's own navigation does not move it a second time. Without `autoHighlight`, stepping past the last (or before the first) item releases the highlight to the input (`aria-activedescendant` removed, the input shows `data-highlighted`), and the next step re-enters at the first/last item; with `autoHighlight` the highlight wraps within the list. Home/End move to the first/last item once an item is highlighted and the input is empty, otherwise they move the caret.
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:146-154`; `menu/root/MenuRoot.tsx:523-528,636-639`. Test: [L4] `MenuFilterRoot.initialHighlight.test.tsx` "highlights the first enabled action and keeps the input in the arrow loop"; `MenuFilterRoot.test.tsx` "walks the list with the arrow keys and releases the highlight at the end", "does not allow arrow navigation to escape when autoHighlight is %s", "keeps Home and End as caret keys until an item is highlighted", "keeps Home and End as caret keys while the input is empty", "keeps the caret keys while a query is highlighted automatically", "keeps the caret keys after navigating to an item while the query is not empty", "uses Home and End for list navigation after an item is highlighted"; `MenuList.test.tsx` "horizontal list without autoHighlight".
- **MNU-KEY-24** — MUST. In a horizontal filterable menu with nothing highlighted, ArrowDown highlights the first navigable item and ArrowUp the last (`enter-list`, reason `keyboard`, `preventDefault`), after which ArrowLeft/ArrowRight move along the list.
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:155-167`. Test: [L4] `MenuList.test.tsx` "enters the results with the vertical arrows after typing", "enters the results from the end with ArrowUp", "highlights the first item when opened from the trigger with ArrowDown".
- **MNU-KEY-25** — MUST. Enter with a highlighted item (`activate`) default-prevents the keydown and dispatches a synthetic click on the item that carries the keydown's modifier keys (UBT-KEY-22, UBT-EVT-05): the item's `onClick` runs (receiving e.g. `ctrlKey`), a link navigates, a checkbox/radio toggles, and `closeOnClick` applies. A disabled highlighted item is not activated. Enter during IME composition is ignored (MNU-KEY-31).
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:65-67,177-180`; `utils/dispatchClickWithModifiers.ts:20-41`. Test: [L4] `MenuFilterRoot.scoping.test.tsx` "navigates the list from the input with aria-activedescendant"; `MenuFilterRoot.test.tsx` "preserves modifiers when the input activates a link item", "does not activate the highlighted item when Enter commits an IME composition"; `useMenuFilterItem.test.tsx` "does not activate a highlighted disabled item with Enter".
- **MNU-KEY-26** — MUST. Shift+Tab in the input (`close`) stops the event, closes with reason `focus-out`, and (if not canceled) focuses the return element: the active trigger, or for a filterable submenu the element the submenu was entered from (the parent's input when the parent is filterable). A canceled close leaves focus in the input with `aria-expanded="true"` and typing still works. Escape is classified `navigate` and is handled by the document Escape dismissal (MNU-KEY-09): it closes outright without clearing the query first. Plain Tab is `ignore`: the browser moves focus out through the guard, which closes the menu (MNU-KEY-10).
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:62-64,181-195`. Test: [L4] `MenuFilterRoot.test.tsx` "closes on Escape even with a query typed", "closes and returns focus to the trigger on Shift+Tab from the input", "keeps input focus when a Shift+Tab close is canceled"; `MenuFilterRoot.scoping.test.tsx` "closes the popup with Escape and returns focus to the trigger".
- **MNU-KEY-27** — MUST. A key that lands on the `Menu.List` or on an item that holds real focus (a scrollbar press focused the list, or a screen reader following `aria-activedescendant` focused the item) is handled as if it had been typed in the input: focus is handed back to the input and the same classification runs (ArrowDown/Up navigate without scrolling the list, Enter activates the highlighted item, Escape still closes, a printable key's default action proceeds into the refocused input). Keys an item already acted on (default-prevented, e.g. Enter/Space activating it) are left alone. When the list's keydown comes from a *nested* popup bubbling through, main-axis keys are stopped from reaching this list's navigation, and if the nested popup just closed on that key, the parent highlight moves one step from the nested popup's trigger.
  Source: `menu/filter-root/MenuFilterList.tsx:43-99`; `filter-dropdown/list/FilterDropdownList.tsx:33-44`. Test: [L4] `MenuList.test.tsx` "replays keys from an item that holds real focus and activates on Enter", "keys on a focused list" (4 tests); `MenuFilterPopup.test.tsx` "returns focus to the input when the list takes it from the input", "leaves focus on the list when the input did not have it".
- **MNU-KEY-28** — MUST. While a filterable menu is open but its trigger still has focus (hover open), keys on the trigger are relayed: ArrowUp/ArrowDown focus the input, run the input key handler (highlighting the first/last item), and are default-prevented; a typeahead character (single character, not Space, no Ctrl/Meta/Alt) focuses the input so the character is typed there; ArrowLeft/ArrowRight are suppressed (so they do not open submenus); modified keys and other keys leave focus on the trigger. A consumer `onKeyDown` on the trigger calling `preventBaseUIHandler()` cancels the relay. The relay is disabled inside a Menubar.
  Source: `menu/filter-root/MenuFilterDropdown.tsx:116-160`. Test: [L4] `MenuFilterRoot.scoping.test.tsx` "trigger key relay" (4 tests).
- **MNU-KEY-29** — MUST. Filterable submenu keyboard entry/exit: when the highlighted item of a filterable parent is a submenu trigger, the submenu open key (`submenu` action) re-dispatches the keydown on the trigger, which opens the submenu with reason `list-navigation`; the parent's highlight is cleared (reported with reason `keyboard` and the ArrowRight/ArrowLeft event) and, for a filterable submenu, its input receives focus with the first item highlighted; for a plain submenu its first item receives real focus. Enter on the highlighted submenu trigger opens it as a click (keyboard open) and the parent highlight clears with reason `none`. The submenu close key pressed in the submenu's input/popup (or on the focused submenu trigger) closes it with reason `list-navigation`, focuses the parent's input (or the element the submenu was entered from) and re-highlights the trigger (reason `keyboard`, the close keydown as event); the next main-axis key continues from the trigger. Escape in a filterable submenu likewise returns focus to the parent input, highlights the trigger, and lets the parent resume filtering. A closed submenu is never reopened by a stale cross-axis key after the highlight moved elsewhere.
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:168-176`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:44-128,204-293`; `menu/filter-root/useVirtualFocusParentHandoff.ts:25-44`. Test: [L4] `MenuFilterRoot.test.tsx` "opens a virtually focused submenu with the keyboard", "opens a virtually focused submenu with Enter from the input", "returns virtual focus to the parent input on Escape and resumes filtering", "uses RTL cross-axis keys to enter and leave a filterable submenu", "does not reopen a stale submenu with a cross-axis key", "opens a submenu from a filterable submenu input", "passes the keydown events when entering and leaving a filterable submenu", "reports no keyboard reason when Enter opens a filterable submenu", "restores input focus after opening with %s and closing with %s"; `MenuFilterRoot.scoping.test.tsx` "enters and leaves a plain submenu with the cross-axis keys from the input", "highlights the trigger as soon as %s closes the submenu"; `MenuFilterSubmenuRoot.test.tsx` "inside a plain parent menu" (9 tests).
- **MNU-KEY-30** — MUST. Modified arrows, Home/End and characters are text-editing commands (`edit`) and never navigate, activate or open a submenu (a Meta/Ctrl/Alt/Shift + cross-axis key does not open a highlighted submenu trigger); an unmodified cross-axis key edits the caret while the input has text and no submenu trigger is highlighted (so editing a submenu query does not leave the submenu), and is treated as navigation only on an empty input (where it can still close a submenu).
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:72-80,103-108`. Test: [L4] `MenuFilterRoot.test.tsx` "keeps cross-axis keys as caret keys while the submenu query is not empty (autoHighlight: %s)", "does not open a highlighted submenu with a modified cross-axis key from the input", "leaves modified editing keys to the input while an item is highlighted"; `useMenuFilterKeyDown.test.ts`.
- **MNU-KEY-31** — MUST. Keydowns with `which === 229` (IME composition) are ignored by the filter key router, by the submenu close-key handler of a filterable submenu popup, and by list navigation; the composed text is committed to the query only on `compositionend` (MNU-EVT-19).
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:59-61`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:279-283`; `floating-ui-react/hooks/useListNavigation.ts:559-561`. Test: [L4] `MenuFilterRoot.test.tsx` "does not activate the highlighted item when Enter commits an IME composition", "does not forward submenu navigation keys while composing text", "filters on the committed text, not the composition in progress"; `MenuFilterSubmenuRoot.test.tsx` "ignores a close key that composes text in the submenu input".
- **MNU-KEY-32** — MUST. When a submenu closes on a key that is also a main-axis key of its parent (vertical submenu under a horizontal parent), the parent's highlight moves one step from the submenu trigger in that key's direction using the parent's own stepping rules (loop, `aria-disabled` items reachable); for a filterable parent without `autoHighlight` the step from the first/last item escapes nothing and keeps the trigger highlighted. In a plain parent the new item also receives real focus.
  Source: `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:192-238`; `menu/filter-root/moveHighlightFrom.ts:18-43`; `menu/filter-root/MenuFilterList.tsx:51-73`. Test: [L4] `MenuFilterSubmenuRoot.test.tsx` "moves a horizontal parent on when a vertical submenu closes on its main-axis key (filterable parent: $parentFilterable, filterable submenu: $submenuFilterable)", "does not wrap a filterable parent when closing from its first item (filterable submenu: %s)", "moves a filterable parent on when a submenu portaled into a shadow root closes", "prevents native scrolling when moving along the parent menu"; `MenuFilterRoot.test.tsx` "keeps disabled parent items reachable during main-axis navigation", "wraps parent navigation when loopFocus is enabled".
- **MNU-KEY-33** — MUST. A cross-axis *close* key pressed on a non-typeable focusable element inside a filterable popup (a custom button) moves focus to the input and stops propagation; after a filterable submenu opens from a plain, roving-focus parent, the submenu's own close key and Escape return focus to the highlighted submenu trigger.
  Source: `menu/filter-root/useMenuFilterPopup.ts:145-156`. Test: [L4] `MenuFilterPopup.test.tsx` "returns focus with the %s %s close key"; `MenuFilterRoot.test.tsx` "returns focus to the input with the RTL submenu close key"; `MenuFilterSubmenuRoot.test.tsx` "closes with Escape and returns focus to the highlighted trigger", "closes with the cross-axis close key and returns focus to the trigger".

### 6.2 Pointer and touch

| Gesture | Target | Behavior | Req |
|---|---|---|---|
| mousedown (primary) | Trigger | Toggles open/closed (`trigger-press`); 200 ms later arms drag-release | MNU-PTR-01, MNU-PTR-02 |
| mouseup on an item after press-and-drag | Item, CheckboxItem, RadioItem, LinkItem (≥200 ms after trigger mousedown; not SubmenuTrigger) | Activates the item via a dispatched click | MNU-PTR-03 |
| mouseup outside the popup after press-and-drag | document | Closes (`cancel-open`) | MNU-PTR-04 |
| hover (rest `delay`, default 100 ms) | Trigger with `openOnHover` | Opens (`trigger-hover`); leave closes after `closeDelay` with safe polygon | MNU-PTR-05..08 |
| hover (`delay` default 100 ms) | SubmenuTrigger (`openOnHover` default true) | Opens submenu (`trigger-hover`) | MNU-PTR-09 |
| pointer leaves submenu popup | submenu Popup | Closes after `closeDelay` unless re-entered or guarded | MNU-PTR-10 |
| hover another item of the parent | sibling SubmenuTrigger / Item | Closes open sibling submenu after `closeDelay` (`sibling-open`) | MNU-PTR-11 |
| pointer move over an item | Item | Highlights (reason `pointer`), subject to `highlightItemOnHover` | MNU-PTR-12, MNU-PTR-14 |
| pointer leaves the popup / list | Popup | Clears the highlight | MNU-PTR-13 |
| click | Item | Activates, closes tree if `closeOnClick` (`item-press`) | MNU-PTR-15 |
| touch tap / screen-reader press | Item, SubmenuTrigger | Activates / toggles | MNU-PTR-16 |
| click | SubmenuTrigger | Opens (`trigger-press`); closes only when `openOnHover` is false | MNU-PTR-17 |
| right-click / long-press | ContextMenu.Trigger | Opens (ContextMenu owns it); 500 ms outside-press grace unless the open came from a `contextmenu` event | MNU-PTR-18, MNU-DIS-10 |
| mouse click / touch / pen / virtual | Trigger of a filterable menu | Opens; focus rules per modality | MNU-PTR-19 |
| mousedown | filterable Item / List | Default-prevented so the input keeps focus | MNU-PTR-20 |
| press | `Menu.Clear` | Clears the query (`clear-press`), keeps/restores input focus | MNU-PTR-21 |
| pointer enters popup / input | filterable Popup | Refocuses the input when focus wandered | MNU-PTR-22 |

- **MNU-PTR-01** — MUST. A primary-button `mousedown` on an enabled `Menu.Trigger` toggles the menu with reason `trigger-press` (`useClick` with `event: 'mousedown'`, `toggle: true`): a closed menu opens, an open menu closes, except that a click within 500 ms (`PATIENT_CLICK_THRESHOLD`) of a hover open keeps it open (MNU-PTR-06). Non-primary buttons and clicks with modifier keys are ignored. The Trigger passes `ignoreMouse: false`, so the `mousedown` toggles for every pointer type that produces a primary-button `mousedown` (mouse, pen, touch compatibility events); a press recognized as a screen-reader (virtual) press applies synchronously instead of after a frame (`isVirtualPointerEvent`). The `click` that follows a recorded `pointerdown` is ignored (the recorded pointer type is reset), so one press toggles once; a `click` with no preceding `pointerdown` (keyboard Enter/Space activation, `element.click()`) toggles. Inside a Menubar the trigger of the currently open bar menu uses `event: 'click'` instead (MBR doc). When the trigger is `disabled`, nothing happens.
  Source: `menu/trigger/MenuTrigger.tsx:197-203,243-255`; `floating-ui-react/hooks/useClick.ts:126-221`; `internals/constants.ts:4`. Test: [L4] `MenuRoot.test.tsx` popup conformance suite "opens on trigger click and sets `aria-expanded` on the trigger"; popup-list suite "closes when the trigger is clicked again", "ignores right clicks on the trigger and on items"; `MenuTrigger.test.tsx` "prevents opening the menu with a mouse when `preventBaseUIHandler` is called in onMouseDown", "should not open the menu when clicked"; `MenuRoot.test.tsx` "moves focus to the first item after a screen reader $name".
- **MNU-PTR-02** — MUST. Immediately after the `mousedown` that opens the menu, the Trigger records the open and starts a 200 ms timer; once it elapses the trigger is in the "drag-release armed" state (`allowMouseUpTriggerRef`). A `mouseup` on the trigger itself before/after this window never activates an item. The timer is cleared and the armed flag reset when the menu closes (so a reopen starts a fresh window).
  Source: `menu/trigger/MenuTrigger.tsx:243-255,267-282`. Test: [L4] `MenuRoot.test.tsx` "triggers a menu item and closes the menu on click, drag, release"; `MenuTrigger.test.tsx` "removes the hover mouseup listener when unmounted before mouseup".
- **MNU-PTR-03** — MUST. When the pointer is pressed on the trigger, dragged into the popup and released on an item (`mouseup` arriving ≥200 ms after the opening `mousedown`, armed state), an item whose registration type is `regular-item` — `Menu.Item`, `Menu.LinkItem`, `Menu.CheckboxItem` and `Menu.RadioItem` all register as `REGULAR_ITEM` — receives a dispatched native `click` (`detail: 1`, `pointerType: 'mouse'`, bubbles) so its handler runs exactly as for a normal click: a plain item closes the menu with reason `item-press` and a checkbox/radio item toggles (and closes per its `closeOnClick`). A `Menu.SubmenuTrigger` registers as `submenu-trigger` and receives no dispatched click, so a drag-release onto it does not open its submenu. The armed flag is cleared by the first such release. `mouseup` on a disabled item does nothing. (The inline code comment says this "overrides" `closeOnClick`; the dispatched click honors `closeOnClick` like any other click — see §16 Q5.)
  Source: `menu/item/useMenuItemCommonProps.ts:107-145`; `menu/item/MenuItem.tsx:47`; `menu/link-item/MenuLinkItem.tsx:54`; `menu/checkbox-item/MenuCheckboxItem.tsx:56`; `menu/radio-item/MenuRadioItem.tsx:60`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:150-153`; `menu/trigger/MenuTrigger.tsx:130-157,243-255`. Test: [L4] `MenuRoot.test.tsx` "triggers a menu item and closes the menu on click, drag, release", "keeps the dialog open after a press-drag-release activation"; `MenuRoot.detached-triggers.test.tsx` "selects nested items with click, drag, release"; `MenuPopup.test.tsx` "receives the mouse interaction type after a drag-release item selection".
- **MNU-PTR-04** — MUST. A document `mouseup` outside the popup (and outside the trigger) while the trigger is in the armed state closes the menu with reason `cancel-open` and the `mouseup` as `event` ("the user pressed on the trigger, dragged away and released"). The listener is attached on the opening `mousedown` and removed on close or on the first qualifying mouseup; releasing on the trigger itself leaves the menu open and merely re-arms nothing.
  Source: `menu/trigger/MenuTrigger.tsx:130-157`. Test: [L4] `MenuRoot.test.tsx` "closes the menu on click, drag outside, release"; `MenuTrigger.test.tsx` "keeps a hover-opened menu open when mouseup lands outside the trigger DOM but within its bounds".
- **MNU-PTR-05** — MUST. With `openOnHover`, a mouse pointer (not touch/pen) resting on the Trigger for `delay` ms (default 100; the timer restarts on every `pointermove`, `restMs` semantics) opens the menu with reason `trigger-hover` and the last pointer event as `event`; `delay: 0` opens on the first movement. The hover interaction is disabled while the trigger is `disabled`; inside a Menubar it is enabled only while another menu of the bar is open (`hasSubmenuOpen`) and this trigger's menu is not the one currently mounted (MNU-API-19) — there is no further check for a sibling bar menu whose hover open is pending. Re-entering the trigger during a hover-initiated close transition reopens immediately (FLT-PTR-12, FLT-PTR-13). The popup opened this way is non-modal regardless of `modal` (MNU-DIS-09, MNU-FOC-10); a filterable popup opened this way keeps focus on the trigger (MNU-FOC-02, MNU-PTR-19).
  Source: `menu/trigger/MenuTrigger.tsx:173-190`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:95-131,214-259`; see FLT-PTR-11, FLT-PTR-12, FLT-PTR-13, FLT-API-04, FLT-ALG-24. Test: [L4] `MenuRoot.test.tsx` "opens when the trigger is hovered and closes when it is no longer hovered", "stays open while the pointer crosses the gap from the trigger to the popup"; `MenuFilterRoot.initialHighlight.test.tsx` "does not focus the input when opened on hover"; `MenuBackdrop.test.tsx` "sets `pointer-events: none` style on backdrop if opened by hover".
- **MNU-PTR-06** — MUST. For a top-level menu with no parent kind, the Trigger passes `stickIfOpen` = "opened by hover within the last 500 ms" to the press interaction (FLT-PTR-06, FLT-API-06): a `mousedown`/click on the Trigger within 500 ms (`PATIENT_CLICK_THRESHOLD`) of a hover open does not close the menu (`stickIfOpen`): the user who hovers then immediately clicks keeps it open, and the open is re-attributed to the click (`instantType` becomes `'click'`, popup gains `data-instant="click"`, focus moves to the first item as for a keyboard open only when the click is a keyboard click). After the threshold a click toggles normally. Under any parent kind — in particular inside a Menubar — the Trigger passes `stickIfOpen: false`, so the first click on a hover-opened bar menu closes it.
  Source: `menu/trigger/MenuTrigger.tsx:195-203,373-392`; `internals/constants.ts:4`; see FLT-PTR-06, FLT-API-06. Test: [L2] `MenuTrigger.test.tsx` "does not close the menu if the user clicks too quickly", "closes the menu if the user clicks patiently", "sticks if the user clicks impatiently", "sticks when clicked before the hover delay completes", "should keep the menu open when re-hovered and clicked within the patient threshold"; `MenuRoot.test.tsx` "treats hover-opened menus as modal after a click", "reopens on hover after an impatient click closes via item press"; `menubar/Menubar.test.tsx` "correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)". (L4 for the patient-click behaviour)
- **MNU-PTR-07** — MUST. After a hover open, moving the pointer off the trigger closes the menu with reason `trigger-hover` after `closeDelay` ms (default 0) unless the pointer travels inside the safe polygon toward the popup or enters the popup; the safe polygon (`safePolygon()`, FLT) keeps the menu open while the pointer moves along the triangle between its leave point and the popup's near edge, and the floating-side hover interaction (`useHoverFloatingInteraction`) cancels the close while the pointer is over the popup and restarts the `closeDelay` on leaving it. The close is not scheduled when the menu was opened by a non-hover interaction (MNU-DIS-11) or while an item of the popup is pressed. Hover closing never happens for touch pointers.
  Source: `menu/trigger/MenuTrigger.tsx:176-190`; `menu/popup/MenuPopup.tsx:124-127`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:133-212`; `floating-ui-react/safePolygon.ts:1-40`; see FLT-PTR-14, FLT-PTR-15, FLT-PTR-23, FLT-PTR-24, FLT-ALG-15. Test: [L4] `MenuRoot.test.tsx` "should close after delay", "stays open while the pointer crosses the gap from the trigger to the popup", "should not close when submenu is hovered after root menu is hovered", "scopes submenu safePolygon pointer events to the parent menu with keepMounted portal", "does not clear body pointer-events styles when closing a scoped submenu".
- **MNU-PTR-08** — MUST. Pointer-leave closing applies only to a hover-opened menu: a menu whose recorded open event is click-like (click, mousedown, keydown, keyup; FLT-STA-04, FLT-STA-06) stays open when the pointer leaves. In addition the popup-side hover interaction runs only while the `hoverEnabled` store flag is `true`; it becomes `false` when the user clicks inside the popup, when the pointer moves within a submenu popup, and when a child submenu of this menu opens, and it is reset to `true` on every close. Within a Menubar/sibling group, moving between triggers switches menus without delay (`instantType: 'group'`, MNU-ANI-07).
  Source: `menu/root/MenuRoot.tsx:335-339,681-691`; `menu/positioner/MenuPositioner.tsx:134-139`; `menu/popup/MenuPopup.tsx:123-126`; `menu/trigger/MenuTrigger.tsx:179-186`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:69-75,230`; see FLT-STA-04, FLT-STA-06. Test: [L4] `MenuRoot.test.tsx` "does not close after hovering out of a popup opened without trigger hover", "does not close after hovering out of a popup opened externally", "closes after hovering out of a popup opened by its trigger"; `MenuRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers".
- **MNU-PTR-09** — MUST. A `Menu.SubmenuTrigger` with `openOnHover` (default `true`) opens its submenu with reason `trigger-hover` after `delay` ms (default 100) of `mousemove`/`mouseenter` over it (`mouseOnly: true` — touch/pen never hover-open; `move: true` — plain movement over it counts), provided pointer interaction is allowed on the parent (`allowMouseEnter`: the parent must have been hovered after it opened, so a trigger that happens to sit under a stationary pointer when the parent opens by keyboard does not open immediately — the first real `mousemove` arms it). `guardStaleOpen` suppresses a hover open scheduled before the trigger's own submenu was closed by a sibling. A hover-opened submenu is non-modal and does not move focus (the trigger stays highlighted and focused). `delay: 0` opens on the first move. When a sibling submenu is open, the hover open is emitted as a tree `itemhover` event first (MNU-PTR-11) so the sibling closes.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:176-191`; `menu/positioner/MenuPositioner.tsx:187-227`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:95-131`; see FLT-PTR-11, FLT-PTR-12, FLT-PTR-18. Test: [L4] `MenuRoot.test.tsx` "opens the submenu on hover with zero delay", "opens a submenu after a plain delay without waiting for the pointer to rest", "cancels a pending submenu hover-open when the pointer leaves via mouseout", "keeps a pending submenu hover-open when mouseout stays within the trigger"; `MenuSubmenuTrigger.test.tsx` "does not open on hover when disabled"; `MenuSubmenuTrigger.screenReaderPress.test.tsx` "ignores an ordinary mouse press with the default `openOnHover`"; `MenuFilterRoot.test.tsx` "opens a hovered submenu without moving focus until the pointer enters it".
- **MNU-PTR-10** — MUST. When the pointer leaves a hover-opened submenu popup (and is not inside its trigger or the safe polygon), the submenu closes after `closeDelay` ms (default 0) with reason `trigger-hover`; while the pointer moves within the popup, or re-enters it before the delay elapses, the close is cancelled. Leaving the parent popup entirely closes the whole hover-opened chain. A submenu opened by click or keyboard is not closed by pointer leave; if a nested Dialog opened from an item holds focus, pointer leave does not disturb it (MNU-FOC-11).
  Source: `menu/popup/MenuPopup.tsx:124-127`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:40-118`; see FLT-PTR-24, FLT-PTR-25. Test: [L4] `MenuFilterRoot.test.tsx` "closes when the pointer sweeps off the trigger", "closes when the pointer leaves the submenu popup", "closes and resets after a query was typed into the hover-opened submenu"; `MenuRoot.test.tsx` "keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item", "keeps pending focus in a nested dialog when the pointer leaves the triggering menu item".
- **MNU-PTR-11** — MUST. Hovering a *different* item of the parent while a hover-opened submenu is open emits a tree `itemhover` event; the open submenu's Positioner reacts by scheduling a close with reason `sibling-open` after its own `closeDelay` (a timer already running is not restarted by further hover events, so repeated moves over siblings do not keep the submenu open indefinitely). Hovering back onto the submenu's own trigger or its popup before the delay cancels the pending close. Non-hover-opened submenus (keyboard/click) close on sibling hover immediately with `sibling-open` as well, so that only one submenu per level is open.
  Source: `menu/positioner/MenuPositioner.tsx:187-216`; `menu/item/useMenuItemCommonProps.ts:127-144`. Test: [L4] `MenuRoot.test.tsx` "should close submenu after delay when hovering a sibling item", "should not restart closeDelay on repeated mousemove over sibling items", "keeps the parent submenu open after a third-level submenu closes due to sibling hover"; `MenuRoot.triggerless.test.tsx` "closes a sibling submenu without closing the root on %s".
- **MNU-PTR-12** — MUST. With `highlightItemOnHover: false` on the Root, pointer movement over items neither highlights them nor emits `onItemHighlighted`, and the previously keyboard-highlighted item keeps its highlight; items still activate on click and submenu triggers still open on hover. Hover highlighting remains active for submenus unless each submenu root also sets it.
  Source: `menu/root/MenuRoot.tsx:536`; `floating-ui-react/hooks/useListNavigation.ts:705-721`. Test: [L4] `MenuRoot.test.tsx` "highlights an item on mouse move by default", "does not highlight items from mouse movement when disabled", "does not highlight submenu triggers from mouse enter when disabled".
- **MNU-PTR-13** — MUST. The Root passes `focusItemOnHover` = `highlightItemOnHover` and `resetOnPointerLeave` (default `true`; the filter root sets it per `autoHighlight`) to list navigation, so when a mouse pointer leaves an item for a target that is not another item, the highlight is cleared and, in a plain menu, real focus returns to the popup element per CMP-PTR-05. Menu-specific results: `activeIndex` becomes `null` and `onItemHighlighted(undefined, details)` is reported with reason `pointer` and the `pointerleave` event (MNU-EVT-10, CMP-EVT-09); in a filterable menu with `autoHighlight: 'always'` the highlight is kept, and in other filterable modes the highlight resets while focus stays on the input. The reset does not run for touch pointers, while the input modality is keyboard, or after the popup closed.
  Source: `floating-ui-react/hooks/useListNavigation.ts:707-740`; `menu/root/MenuRoot.tsx:544-545`; `menu/filter-root/useMenuFilterRoot.ts:27`; see CMP-PTR-05, CMP-EVT-09. Test: [L2] popup-list suite "clears the highlight when the pointer leaves the list without leaving focus on an item"; `MenuFilterRoot.test.tsx` "clears a pointer highlight on pointer leave by default", "preserves a pointer highlight when autoHighlight is \"always\"". (L4 for the pointer-leave reset)
- **MNU-PTR-14** — MUST. With `focusItemOnHover` = `highlightItemOnHover` (MNU-PTR-12), a `mousemove` over an item highlights it per CMP-PTR-04 — once per item (a further move over the already-highlighted item is a no-op) — and, under DOM focus, focuses it; under virtual focus it sets `aria-activedescendant` instead (CMP-FOC-05, CMP-ARIA-01). On WebKit a move with zero movement (emitted when content scrolls under a stationary pointer) is ignored (CMP-PTR-08), so scrolling the list with the keyboard does not steal the highlight. Menu-specific results: the highlight is reported with reason `pointer` and the native mouse/pointer event (CMP-EVT-09); items with the `disabled` prop are highlighted (but not activated) while hidden or natively disabled items are not; `pointerenter`/`pointerdown` alone do not highlight.
  Source: `floating-ui-react/hooks/useListNavigation.ts:687-706`; `menu/item/useMenuItemCommonProps.ts:90-101`; see CMP-PTR-04, CMP-PTR-08, CMP-EVT-09. Test: [L4] popup-list suite "highlights and focuses the hovered item", "lets the pointer take the highlight back after keyboard navigation", "keeps the highlight when a touch scroll starts on the highlighted item"; `MenuRoot.test.tsx` "passes native mouse and pointer events for pointer highlights", "passes the mouse event when a submenu trigger is hovered"; `MenuFilterRoot.test.tsx` "keeps the automatic highlight when typing moves it out from under the pointer", "keeps real focus on the input while the cursor moves through the list".
- **MNU-PTR-15** — MUST. A click on an enabled `Menu.Item` (any pointer type, or the dispatched click of MNU-KEY-08/MNU-PTR-03) runs the consumer `onClick` first (UBT ordering); if it did not call `preventBaseUIHandler()`, `closeOnClick` (default `true`) closes the whole menu tree with reason `item-press` and that click as `event` (MNU-DIS-06). With `closeOnClick: false` the menu stays open. A click on a `disabled` item is swallowed (no `onClick`, no close). `Menu.LinkItem` follows the same rule but the native anchor navigation is not prevented, and `Menu.CheckboxItem`/`Menu.RadioItem` apply their toggle before closing (default `closeOnClick: false` for those).
  Source: `menu/item/useMenuItem.ts:24-38`; `menu/item/useMenuItemCommonProps.ts:92-106`; `menu/checkbox-item/MenuCheckboxItem.tsx:70-84`; `menu/radio-item/MenuRadioItem.tsx:62-77`. Test: [L2] `MenuItem.test.tsx` "calls the onClick handler when clicked", "does not close the menu when onClick prevents Base UI handler", "closes the menu when the item is clicked by default", "when `closeOnClick=false` does not close the menu when the item is clicked", "can be focused but not interacted with when disabled"; `MenuCheckboxItem.test.tsx` and `MenuRadioItem.test.tsx` "does not close the menu when the item is clicked by default", "when `closeOnClick=true`, closes the menu when the item is clicked"; `MenuLinkItem.test.tsx` "react-router <Link> activates with Enter and Space"; popup-list suite "does not activate a disabled item". (L4 for the keyboard and drag-release clicks)
- **MNU-PTR-16** — MUST. Touch taps and screen-reader presses (a `click` with `detail === 0` not preceded by keydown, or a `pointerdown` with `pointerType === ''`/zero dimensions — `isVirtualClick`/`isVirtualPointerEvent`) activate items like mouse clicks. On a `Menu.SubmenuTrigger` a screen-reader press is distinguished from a keyboard Enter by the absence of a preceding keydown and toggles the submenu with reason `trigger-press`, after which the first submenu item is highlighted so VoiceOver/NVDA users land inside (MNU-A11Y-03). A touch tap on a hover-enabled submenu trigger opens it on `click` (never on hover).
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:192-204`; `floating-ui-react/utils/event.ts:13-47`. Test: [L4] `MenuSubmenuTrigger.screenReaderPress.test.tsx` "toggles the submenu on repeated screen reader activation with the default `openOnHover`", "does not close the submenu on the trailing click of a screen reader press with `openOnHover={false}`", "opens on a screen reader press without waiting a frame", "reports the trigger, not a pressed descendant, as the opener"; `MenuRoot.test.tsx` "moves focus to the first item after a screen reader $name"; `MenuSubmenuTrigger.test.tsx` "closes a touch-opened submenu on a second tap"; `MenuFilterRoot.initialHighlight.test.tsx` "focuses the input when a screen reader press opens the menu".
- **MNU-PTR-17** — MUST. `Menu.SubmenuTrigger` installs `useClick` with `event: 'mousedown'`, `toggle: true`, `ignoreMouse: openOnHover`, `stickIfOpen: openOnHover`. With `openOnHover` (default `true`) **mouse** presses are ignored entirely — hovering is what opens the submenu, and a mouse click on the open trigger neither closes nor reopens it — while touch taps, keyboard activation and screen-reader presses toggle it (so TalkBack/VoiceOver users can close a submenu from its trigger), except that the first such activation after a hover open keeps it open (`stickIfOpen`). With `openOnHover: false` a mouse press (`mousedown`) toggles the submenu with reason `trigger-press`, and the trailing `click` of a screen-reader press does not close it again. A click open does not move focus into the submenu (the trigger keeps focus/highlight; the first submenu item is highlighted only for keyboard clicks), a disabled trigger ignores presses, and a press on the submenu trigger never closes the parent (its `closeOnClick` is forced `false`).
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:192-204,223-266`; `floating-ui-react/hooks/useClick.ts:98-219`. Test: [L2] `MenuRoot.test.tsx` "opens submenu on click when openOnHover is false"; `MenuRoot.detached-triggers.test.tsx` "opens a submenu on click when hover is disabled"; `MenuSubmenuTrigger.screenReaderPress.test.tsx` "ignores an ordinary mouse press with the default `openOnHover`", "does not close the submenu on the trailing click of a screen reader press with `openOnHover={false}`"; `MenuSubmenuTrigger.test.tsx` "closes a touch-opened submenu on a second tap"; `MenuFilterRoot.test.tsx` "retains the parent highlight when a submenu opens from a pointer". (L4 for the press behaviour)
- **MNU-PTR-18** — MUST. Under a `ContextMenu` parent the Trigger behaviors above are replaced by the context-menu trigger (right-click / long-press handled by `ContextMenu.Trigger`, out of scope here): Menu only contributes that the positioner anchors to the pointer coordinates (MNU-POS-04), arrow-key opening is disabled (MNU-KEY-01), the popup is modal with focus management (MNU-FOC-13), and that an outside press within 500 ms of an open that was not caused by a `contextmenu` event (e.g. a touch long press) is ignored, while an open from a `contextmenu` event gets no grace (MNU-DIS-10).
  Source: `menu/root/MenuRoot.tsx:94-122,312-333`; `menu/positioner/MenuPositioner.tsx:58-75`. Test: [L4] `MenuPositioner.test.tsx` "uses the layout viewport for a root context menu", "preserves explicit context-menu placement and offsets", "uses the visual viewport for a context menu submenu" (the trigger gesture itself is covered by the ContextMenu tests).
- **MNU-PTR-19** — MUST. In a filterable menu the Trigger opens on the same gestures as a plain menu, and the focus target depends on modality: a mouse click or a virtual (screen-reader) click focuses `Menu.Input` once the popup mounts (the input is the `initialFocus`); a touch or pen tap opens without focusing the input (so the on-screen keyboard does not appear) and leaves focus on the trigger until the user taps the input; a hover open leaves focus on the trigger (the trigger key relay MNU-KEY-28 then forwards keys). When the popup opens by keyboard the input receives focus with the first item highlighted when `autoHighlight` is set.
  Source: `menu/filter-root/MenuFilterPopup.tsx:27-55`; `menu/filter-root/useMenuFilterPopup.ts:52-98`; `utils/useOpenInteractionType.ts:13-63`. Test: [L4] `MenuFilterRoot.initialHighlight.test.tsx` "handles opening with %s", "focuses the input when a screen reader press opens the menu", "does not focus the input when an item is tapped after a touch open", "does not focus the input when opened on hover"; `MenuFilterRoot.test.tsx` "marks the input focus-visible when the menu is opened with a pointer"; `MenuFilterRoot.scoping.test.tsx` "does not trap a menu opened by touch", "does not trap a popup opened by an assistive technology click".
- **MNU-PTR-20** — MUST. In a filterable menu every item part (Item, LinkItem, CheckboxItem, RadioItem, SubmenuTrigger) and the List default-prevent `mousedown` so that pointer interaction with the list never takes focus away from `Menu.Input`; the scrollbar press on the list is the one exception the browser still focuses (handled by MNU-KEY-27/MNU-PTR-22). `Menu.Clear` likewise default-prevents `mousedown`.
  Source: `filter-dropdown/item/useFilterDropdownItem.ts:46-61`; `menu/filter-root/MenuFilterList.tsx:101-108`; `filter-dropdown/clear/FilterDropdownClear.tsx:44-50`. Test: [L4] `useMenuFilterItem.test.tsx` "keeps focus on the input when items are pressed"; `MenuFilterRoot.test.tsx` "preserves input focus on item presses", "filters items and selects the active item while focus remains on the input".
- **MNU-PTR-21** — MUST. Pressing `Menu.Clear` (click, Enter/Space via `useButton`) clears the query: `onValueChange('', { reason: 'clear-press', event })`, the input's value becomes `''`, the highlight is reset per `autoHighlight`, and focus stays on (or returns to) the input. Clear is not rendered/active while the query is empty (MNU-DOM-28) and is disabled when the Root is disabled.
  Source: `menu/clear/MenuClear.tsx:17-23`; `filter-dropdown/clear/FilterDropdownClear.tsx:30-62`. Test: [L2] `MenuFilterRoot.test.tsx` "reports clear-press when the clear button is used", "clears a navigated highlight when Clear is pressed", "restores the first item when an always-highlighted query is cleared", "disables filter controls when the root is disabled"; `MenuFilterParts.test.tsx` "excludes Clear from the tab order and accessibility tree". (L4 for the Enter/Space activation and focus clauses)
- **MNU-PTR-22** — MUST. In a filterable menu, when real focus has wandered to the list (scrollbar press), to an item (screen reader), or to the submenu trigger of a closed submenu, `pointermove`/`pointerenter` over the popup or the input refocuses `Menu.Input` (`refocusOwner`), unless focus is on a nested popup or on a typeable element inside the popup; `mouseover` on the Trigger while the popup is open and the trigger has focus leaves focus there. Nested-popup focus is tracked so that a parent does not steal focus from a filterable submenu's input.
  Source: `menu/filter-root/useMenuFilterPopup.ts:100-143`; `filter-dropdown/utils/refocusOwner.ts:10-17`. Test: [L4] `MenuFilterRoot.test.tsx` "leaves focus on another control in the popup when the pointer moves", "returns focus to the parent input when the pointer moves back over the parent popup", "returns focus to the parent input when a mouse enters the trigger after a tap", "restores the parent input highlight when the pointer returns from a plain submenu", "moves focus back to the parent input while the pointer moves between submenu triggers"; `MenuFilterSubmenuRoot.test.tsx` "keeps focus in the submenu input when the pointer crosses the trigger again".

### 6.3 Focus management

- **MNU-FOC-01** — MUST. Opening by keyboard (arrow, Enter/Space, `detail === 0` click, or `openByTrigger` from a keyboard event) moves focus into the popup: a plain menu focuses the initial highlighted item (first or last per MNU-KEY-02) via the FloatingFocusManager `initialFocus` resolving to that item (falling back to the popup element when no item exists); a filterable menu focuses `Menu.Input`. A submenu opened by keyboard focuses its first item (plain) or its input (filterable). The focus move happens after the popup mounts, and before any entry animation completes (`data-starting-style` phase).
  Source: `menu/popup/MenuPopup.tsx:85-97,194-218`; `menu/root/MenuRoot.tsx:420-432`; `menu/filter-root/MenuFilterPopup.tsx:27-55`; see FLT-FOC-06, FLT-API-05. Test: [L4] `MenuRoot.test.tsx` "focuses the first item after the menu is opened by keyboard", "focuses the first item when down arrow key opens the menu", "focuses the last item when up arrow key opens the menu"; popup-list suite "opens with %s without activating an item"; `MenuFilterRoot.test.tsx` "highlights the first item while keeping input focus when opened with the keyboard", "focuses the input when a filterable submenu opens with the keyboard".
- **MNU-FOC-02** — MUST. Opening by mouse click or hover does not highlight an item: a click-opened plain menu moves focus to the popup element (`initialFocus` = popup; `focusItemOnOpen` is `false` for non-keyboard opens) so that arrow keys enter the list at the boundary (MNU-KEY-06) and Escape/typeahead work; a hover-opened plain menu resolves `initialFocus` the same way (the popup element; §16 Q10 records that no test asserts this), while a hover-opened filterable menu keeps focus on the trigger (the input's `initialFocus` resolves to `false` when the last open-change reason is `trigger-hover`). In a filterable menu a click-opened popup focuses the input (MNU-PTR-19) and no item is highlighted unless `autoHighlight`.
  Source: `menu/popup/MenuPopup.tsx:84-97`; `menu/root/MenuRoot.tsx:420,527`; `menu/filter-root/MenuFilterPopup.tsx:27-48`; see FLT-FOC-06. Test: [L4] popup-list suite "starts without a highlight after a pointer opening"; `MenuList.test.tsx` "focuses the list rather than the presentational popup on a pointer open"; `MenuFilterRoot.initialHighlight.test.tsx` "does not focus the input when opened on hover"; `MenuRoot.test.tsx` "does not close after hovering out of a popup opened externally".
- **MNU-FOC-03** — MUST. Items use a roving `tabindex`: the highlighted (active) item has `tabindex="0"`, every other item `tabindex="-1"` (CMP-DOM-01 semantics); when nothing is highlighted the popup element itself carries `tabindex="-1"` and is the only focusable stop. In a filterable (virtual-focus) menu all items keep `tabindex="-1"` and the input is the single Tab stop; `Menu.Clear` has `tabindex="-1"`. Submenu triggers follow the same rule in their parent list and additionally receive `tabindex="0"` while their submenu is open by keyboard so Shift+Tab/close returns to them.
  Source: `menu/item/useMenuItemCommonProps.ts:74-75`; `floating-ui-react/hooks/useListNavigation.ts:687-700`; `menu/popup/MenuPopup.tsx:155`. Test: [L4] popup-list suite "moves focus along with the highlight"; `MenuSubmenuTrigger.test.tsx` "sets tabIndex to 0 on the submenu trigger after opening the submenu with a keydown event"; `MenuList.test.tsx` "keeps virtual focus on the input without making the list tabbable"; `MenuFilterRoot.test.tsx` "keeps plain menu items and submenu triggers out of the tab order in a filterable list"; `MenuRoot.test.tsx` "does not leave a tabbable item after tabbing out of a keepMounted menu before close".
- **MNU-FOC-04** — MUST. When the menu closes, focus returns to the element that opened it: the active trigger for `escape-key`, `item-press`, `list-navigation`, `imperative-action` and `none` closes; the element that had focus before the open when the menu was opened programmatically without a trigger (`previouslyFocusedElement`); nothing (focus is left where the user put it) for `outside-press` (focus is on what was clicked), for `focus-out` through the focus guards (the guard already moved focus, MNU-KEY-10), and for hover closes where focus never entered the popup. Return focus is performed after the exit transition finishes when the popup animates (`returnFocus` deferred until unmount) and immediately otherwise; the FloatingFocusManager `returnFocus` is `false` while the trigger is the active element so it is not re-focused needlessly.
  Source: `menu/popup/MenuPopup.tsx:99-107,194-218`; `menu/root/MenuRoot.tsx:400-412`; `floating-ui-react/components/FloatingFocusManager.tsx:154-257`; see FLT-FOC-07, FLT-FOC-21, FLT-ALG-21. Test: [L4] `MenuRoot.test.tsx` "focuses the trigger after the menu is closed", "focuses the trigger after Escape when the closing menu receives mouseleave", "returns focus to the opener when a menu is opened programmatically", "returns focus on Escape after a controlled root ignores a Shift+Tab close"; popup-list suite "closes on Escape without committing, and keeps focus on the trigger", "closes on an outside press without committing"; popupFocusPropsTests "focuses the trigger by default".
- **MNU-FOC-05** — MUST. With `keepMounted` on the Portal the return-focus behavior is identical (the popup is hidden, not unmounted); `restoreFocus` ensures that if the previously highlighted item was removed while open, closing still returns focus to the trigger rather than `body`. Items that unmount while focused (dynamic lists) hand focus back to the popup element so that keyboard navigation keeps working.
  Source: `menu/popup/MenuPopup.tsx:201-207`; `floating-ui-react/components/FloatingFocusManager.tsx:225-233`. Test: [L4] `MenuRoot.test.tsx` "focuses the trigger after the menu is closed but not unmounted", "does not leave a tabbable item after tabbing out of a keepMounted menu before close"; `MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s".
- **MNU-FOC-06** — MUST. Focus guards: while the menu is open, a visually hidden focusable guard is rendered immediately before the trigger (`beforeContentFocusGuard`, rendered only for the active trigger) and the FloatingFocusManager renders guards around the popup; Shift+Tab from an open focused Trigger lands on the pre-trigger guard, which closes the menu (`focus-out`) and moves focus to the tabbable element before the trigger; Tab from the trigger lands on the popup's first guard which moves focus into the popup (plain menu: highlighted item or popup element; filterable: input). The trigger guards are rendered exactly while this trigger owns the open popup (`isOpenedByTrigger`), so a trigger that is not the active one renders none; a trigger inside a Menubar returns early as a composite item and never renders them (the bar manages Tab); the source makes no exception for hover-opened menus.
  Source: `menu/trigger/MenuTrigger.tsx:79,224-227,272-308`; `utils/popups/store.ts:200-201`; `utils/popups/useTriggerFocusGuards.ts:34-72`; `menu/popup/MenuPopup.tsx:210-216`. Test: [L4] `MenuRoot.test.tsx` "closes the menu and moves focus to the next element when tabbing forward from the open menu", "closes the menu and moves focus to the trigger when shift-tabbing from the open menu"; `MenuTrigger.test.tsx` "focuses the preceding element when modal=%s"; `MenuSubmenuTrigger.test.tsx` "keeps a submenu open on direct trigger focus but closes it on guard return", "closes a submenu when its focus guard is in a shadow-root portal".
- **MNU-FOC-07** — MUST. Shift+Tab on the focused Trigger of an open menu (modal or not) closes the menu with reason `focus-out` and moves focus to the previous tabbable element in the document (resolved via `getTabbableNearElement`, skipping the guards and the portaled positioner), rather than into the popup; Tab from the trigger of an open modal menu enters the popup (MNU-FOC-06). For a filterable menu whose trigger kept focus after a hover open, Shift+Tab behaves the same.
  Source: `utils/popups/useTriggerFocusGuards.ts:38-69`; `floating-ui-react/utils/tabbable.ts:226-256`. Test: [L4] `MenuTrigger.test.tsx` "focuses the preceding element when modal=%s"; `MenuFilterRoot.scoping.test.tsx` "returns to its own trigger on Shift+Tab".
- **MNU-FOC-08** — MUST. In a submenu, focus leaving the popup through its focus guards (Tab or Shift+Tab out of a keyboard-opened submenu) closes the submenu with reason `focus-out` and returns focus to the submenu trigger via the `nestedReturnFocusRef` (the parent list keeps its highlight on that trigger); Tab from the last item of a top-level popup leaves the whole tree. A submenu that is closed because its parent closes does not fight the parent for focus: only the outermost closing menu performs return focus.
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`; `menu/popup/MenuPopup.tsx:109-122,196-209`. Test: [L4] `MenuRoot.test.tsx` "returns focus to submenu triggers when closing nested menus", "closes submenus when focus is lost by shift-tabbing from a nested menu"; `MenuSubmenuTrigger.test.tsx` "keeps a submenu open on direct trigger focus but closes it on guard return"; `MenuFilterRoot.test.tsx` "closes a filterable submenu and moves focus to the next element when tabbing forward", "closes a pointer-opened filterable submenu and moves focus forward when tabbing".
- **MNU-FOC-09** — MUST. `Menu.Popup`'s `finalFocus` prop overrides the return-focus target: `false` disables return focus entirely; a DOM handle to an element focuses that element; a function `(closeType: 'escape-key' | 'outside-press' | 'focus-out' | 'item-press' | 'list-navigation' | 'none') => HTMLElement | boolean | undefined | null` is called once per close with the reason category (all other reasons map to `'none'`) and its return value is used (`undefined`/`null`/`true` fall back to the default target, `false` disables). `finalFocus` is also applied when the menu is closed by `actionsRef.unmount()` or by an item press.
  Source: `menu/popup/MenuPopup.tsx:99-107,199-205`; `floating-ui-react/components/FloatingFocusManager.tsx:189-199`. Test: [L4] `MenuPopup.test.tsx` "receives the interaction type of the item press that closed the menu", "receives the mouse interaction type after a drag-release item selection"; popupFocusPropsTests (`initialFocus: false`) "focuses the trigger by default", "focuses the element of a ref", "does not move focus when `false`", "passes the close type to a function".
- **MNU-FOC-10** — MUST. Non-modal menus (`modal: false`, hover-opened menus, submenus, and all menus not under a ContextMenu): outside content is not `aria-hidden`/inert, Tab moves focus out of the popup (closing it, MNU-KEY-10), and the FloatingFocusManager runs with `modal: false` so no focus trap is installed. A modal top-level menu (`modal: true`, default) under a plain Root behaves identically regarding Tab (it closes rather than trapping) — the `modal` prop affects only scroll locking and the internal backdrop (MNU-DIS-09, MNU-DOM-11), not focus trapping.
  Source: `menu/popup/MenuPopup.tsx:194-218`; `menu/root/MenuRoot.tsx:676-690`; see FLT-ARIA-02, FLT-FOC-11. Test: [L4] `MenuRoot.test.tsx` "should not render an internal backdrop when `false`"; `MenuFilterRoot.scoping.test.tsx` "lets Tab leave a modal filterable popup and closes it", "lets Tab leave the popup with modal={false}", "does not trap a hover-opened popup".
- **MNU-FOC-11** — MUST. When a nested Dialog (or any popup) opened from an item holds focus, pointer leave of the menu and the menu's own focus-out detection do not close the menu or steal focus: focus-out is only acted upon when the newly focused element is outside the menu tree *and* outside any floating element registered in the tree (`getInsideElements`), and Shift+Tab inside that nested dialog is left to the dialog (MNU-KEY-11).
  Source: `menu/popup/MenuPopup.tsx:208-216`; `floating-ui-react/components/FloatingFocusManager.tsx:234-257`. Test: [L4] `MenuRoot.test.tsx` "keeps the menu and dialog open when pressing Shift+Tab inside a nested dialog", "keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item", "keeps pending focus in a nested dialog when the pointer leaves the triggering menu item", "keeps the dialog open after a press-drag-release activation".
- **MNU-FOC-12** — MUST. `restoreFocus`: if the element that holds focus inside the popup is removed from the DOM while the menu is open (filtering removes the highlighted item, a dynamic list shrinks), focus is restored to the nearest tabbable element inside the floating tree (`restoreFocus`; for a plain menu that is the popup element) or stays on the input (filterable) so that keyboard interaction continues, and on close the normal return target is used.
  Source: `menu/popup/MenuPopup.tsx:202`; `floating-ui-react/components/FloatingFocusManager.tsx:212-218,505-530`; see FLT-FOC-12. Test: [L4] `MenuFilterRoot.test.tsx` "clears a positional highlight when a controlled query replaces the visible item", "updates the active descendant when filtering replaces a custom-id item"; `MenuRoot.test.tsx` "skips null items when navigating".
- **MNU-FOC-13** — MUST. `Menu.Popup` passes `modal: true` to the focus manager iff the Root's parent kind is `'context-menu'` — independently of the `modal` prop and of how the menu was opened — and `modal: false` for every other Menu, including `modal: true` top-level menus (MNU-FOC-10). Under a ContextMenu the observable result is per FLT-ARIA-01 (outside content `aria-hidden`), FLT-DOM-15 and FLT-KEY-04/05 (Tab does not leave the popup). Shift+Tab does not cycle: list navigation intercepts it before the focus trap and closes the menu with reason `focus-out`, returning focus to the trigger surface (MNU-KEY-11, CMP-KEY-24). Escape and outside press remain dismissal paths.
  Source: `menu/popup/MenuPopup.tsx:82,194-199`; `menu/root/MenuRoot.tsx:94-122`; `floating-ui-react/hooks/useListNavigation.ts:771-792`; see FLT-ARIA-01, FLT-DOM-15, FLT-KEY-04, FLT-KEY-05, CMP-KEY-24. Test: [L2] `context-menu/root/ContextMenuRoot.test.tsx` "returns focus to the focused surface when closing with Shift+Tab"; `MenuRoot.test.tsx` "should render an internal backdrop when `true`" (backdrop without trap); `MenuFilterRoot.scoping.test.tsx` "lets Tab leave a modal filterable popup and closes it". (L4 for the ContextMenu focus-trap result)
- **MNU-FOC-14** — MUST. Filterable menu focus ownership: `Menu.Input` is the owner of real focus while the popup is open. Focus arriving on the popup element (the FloatingFocusManager fallback) or on the list is redirected to the input; focus arriving on an item from the input (screen reader following `aria-activedescendant`) is tolerated and the next key or pointer move hands it back (MNU-KEY-27, MNU-PTR-22); focus leaving the popup to an element outside the menu tree closes it (`focus-out`). The input's `onBlur` to an element within the popup is ignored. When the input unmounts while focused (conditional rendering), focus falls back to the popup element and the menu stays open.
  Source: `menu/filter-root/useMenuFilterPopup.ts:52-143`; `menu/filter-root/MenuFilterPopup.tsx:27-55`; `menu/input/MenuInput.tsx:20-48`. Test: [L4] `MenuFilterPopup.test.tsx` "returns focus to the input when the list takes it from the input", "leaves focus on the list when the input did not have it"; `MenuList.test.tsx` "clears the highlight when focus returns to the input from an item", "keeps the highlight when focus returns to the input with autoHighlight=\"always\""; `MenuFilterRoot.webkit.test.tsx` "clears selection when the focused input unmounts while the menu stays open"; `MenuFilterRoot.test.tsx` "does not reclaim focus when a controlled submenu closes after focus moves outside".
- **MNU-FOC-15** — MUST. Filterable submenu handoff (`useVirtualFocusParentHandoff`): when a filterable submenu opens by keyboard or click, its input receives focus (and the parent input's `aria-activedescendant` is cleared); when it opens by hover, focus stays on the parent's input until the pointer enters the submenu popup, at which point the submenu input takes focus; when a plain (non-filterable) submenu opens from a filterable parent by keyboard its first item receives real focus. On close (Escape, close key, outside press into the parent, item press that does not close the tree) focus returns to the parent input and the parent re-highlights the submenu trigger; if the submenu unmounts while its input is focused, focus goes to the parent input. A filterable submenu under a plain parent returns focus to the highlighted submenu trigger instead.
  Source: `menu/filter-root/useVirtualFocusParentHandoff.ts:16-66`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:44-128`. Test: [L4] `MenuFilterRoot.test.tsx` "focuses the input when a filterable submenu opens with the keyboard", "moves focus into the submenu input once the pointer enters it", "closes a hover-opened submenu from a virtually focused parent", "focuses the first item when entering a hover-opened submenu from a filterable menu", "returns focus to the parent input once a pointer-opened submenu unmounts", "restores input focus after opening with %s and closing with %s"; `MenuFilterSubmenuRoot.pointerFocus.test.tsx` "handles a %s press without hover opening", "does not focus the input after a touch press with hover opening enabled", "does not focus a hover-opened input on a %s press", "lets keyboard activation enter a hover-opened submenu"; `MenuFilterSubmenuRoot.test.tsx` "opens with the cross-axis key and focuses the submenu input", "keeps the automatic highlight when the keyboard enters a hover-opened submenu"; `useMenuFilterSubmenuTrigger.test.tsx` "keeps real focus on the parent input until the submenu opens"; `MenuSubmenuTrigger.screenReaderPress.test.tsx` "opens a filterable submenu from a filterable parent and focuses its input", "keeps focus in a kept-mounted filterable submenu opened by a screen reader press".
- **MNU-FOC-16** — MUST. Focusing the Trigger (programmatically or by click) while the menu is open does not seed a highlight: in a plain menu it clears the highlight (`activeIndex` → `null`, so no item claims `tabindex="0"`) and in a filterable (virtual-focus) menu it leaves the highlight and `aria-activedescendant` untouched. Outside a Menubar a `focus` on the trigger never opens the menu. Inside a Menubar, `Menu.Trigger` enables a focus-open interaction (FLT-FOC-01 … FLT-FOC-04) only while another menu of the bar is open: focusing the trigger then opens with reason `trigger-focus` and a later blur of the trigger closes with `trigger-focus`. ContextMenu never emits `trigger-focus`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:915-945`; `menu/root/MenuRoot.tsx:623-667`; `menu/trigger/MenuTrigger.tsx:205-207`; `floating-ui-react/hooks/useFocus.ts:150-233`; see CMP-FOC-06, FLT-FOC-01, FLT-FOC-04. Test: [L4] `MenuFilterRoot.test.tsx` "does not seed a highlight when the trigger regains focus while open", "does not highlight an item when another detached trigger receives focus".
- **MNU-FOC-17** — MUST. With detached triggers (`Menu.createHandle()`), return focus targets the trigger that opened the menu (`activeTriggerElement`), even when several triggers share the handle; `handle.open(id)`/`openByTrigger` from a keyboard event records that trigger as active and seeds the keyboard highlight; `handle.open()` with no trigger (triggerless) records the previously focused element as the return target; switching triggers while open (MNU-POS-10) updates the return target without moving focus until close.
  Source: `menu/root/MenuRoot.tsx:400-412`; `utils/popups/popupHandle.ts:213-238`; `utils/popups/popupStoreUtils.ts:139-177`. Test: [L4] detached-triggers suite "opens by trigger id with its payload and closes"; `MenuRoot.detached-triggers.test.tsx` "supports keyboard navigation regardless of which trigger opened the menu"; `MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s", "preserves real trigger registration, initially present=%s"; `MenuRoot.test.tsx` "returns focus to the opener when a menu is opened programmatically".

### 6.4 Dismissal

- **MNU-DIS-01** — MUST. The Root passes `enabled: !disabled`, an `outsidePress` predicate (MNU-DIS-10) and, for nested menus, the enclosing floating tree (`externalTree`) to the dismiss mechanism, leaving `outsidePressEvent` at its default `'sloppy'`. Outside-press handling is per FLT-DIS-01 … FLT-DIS-11 and FLT-PTR-26/27: a mouse/pen `pointerdown` outside the popup closes at once; touch presses follow the touch rules of FLT-PTR-27; presses on any trigger registered with the menu (including a *different* trigger bound to the same handle, whose own press then switches the popup to it, MNU-POS-10), inside the floating element of an open descendant tree node (submenus, nested dialogs) or on a scrollbar do not close. Menu-specific results: the close carries reason `outside-press` and the press event as `event`; the internal backdrop (modal) counts as outside; a press on another menu's trigger inside the same Menubar is handled by the bar; under a ContextMenu, presses within the grace period of MNU-DIS-10 are vetoed.
  Source: `menu/root/MenuRoot.tsx:501-512`; `floating-ui-react/hooks/useDismiss.ts:240-420`; see FLT-DIS-01, FLT-DIS-05, FLT-DIS-07, FLT-DIS-08, FLT-DIS-09, FLT-DIS-11, FLT-PTR-26, FLT-PTR-27, FLT-ALG-17. Test: [L2] popup-list suite "closes on an outside press without committing", "closes when the trigger is clicked again"; `MenuRoot.test.tsx` "closes the entire tree when clicking outside the deepest submenu"; `MenuRoot.detached-triggers.test.tsx` "closes every level when clicking outside the deepest submenu". (L4 for the outside-press handling)
- **MNU-DIS-02** — MUST. Escape closes the innermost menu (MNU-KEY-09) with reason `escape-key`; Escape is listened for on the document while open (`escapeKey: true`, `bubbles: { escapeKey: closeParentOnEsc }`), so it works when focus is on the trigger, inside the popup, or on the body (hover-opened).
  Source: `menu/root/MenuRoot.tsx:501-512`; `floating-ui-react/hooks/useDismiss.ts:201-232`; see FLT-KEY-01, FLT-API-01. Test: [L4] popup-list suite "closes on Escape without committing, and keeps focus on the trigger", "closes only itself on Escape inside a dialog"; popup conformance suite "is ignored when `open={true}` is passed, so Escape cannot close"; `MenuFilterRoot.test.tsx` "closes on Escape even with a query typed".
- **MNU-DIS-03** — MUST. `closeParentOnEsc` (SubmenuRoot, default `false`): when `true`, an Escape in the submenu is allowed to bubble to the parent menu's dismiss listener, so the parent closes too (each level decides for itself; the top-level menu always closes on Escape); when `false` the Escape is stopped at the submenu, which closes alone and returns focus to its trigger (MNU-KEY-19). The prop is accepted on a top-level Root but has no effect (§16 Q2).
  Source: `menu/root/MenuRoot.tsx:503`; `menu/submenu-root/MenuSubmenuRoot.tsx:28-40`. Test: [L4] `MenuRoot.test.tsx` "does not close the parent menu when the Escape key is pressed by default", "closes the parent menu when the Escape key is pressed  if `closeParentOnEsc=true`".
- **MNU-DIS-04** — MUST. Closing a menu closes every open descendant submenu: the parent's `setOpen(false)` emits a tree `close` event, each child whose `parentNodeId` matches closes with the parent's reason (a child closed because its parent closed by `escape-key`/`outside-press`/`item-press`/`imperative-action` reports that same reason; when the parent close carries no reason the child reports `sibling-open`). The children's `onOpenChange` fire after the parent's. Unmounting the parent while a child is open closes the child with reason `none`.
  Source: `menu/popup/MenuPopup.tsx:109-122`; `menu/root/MenuRoot.tsx:341-445`; see FLT-STR-06, FLT-STR-07. Test: [L4] `MenuRoot.test.tsx` "calls onOpenChange with false exactly once per menu when a submenu item is clicked", "closes the entire tree when clicking outside the deepest submenu"; `MenuPositioner.test.tsx` "closes an open submenu with a sibling reason when its controlled parent closes"; `MenuRoot.triggerless.test.tsx` "keeps an initially open submenu attached to the root".
- **MNU-DIS-05** — MUST. `sibling-open`: when a submenu trigger of the same parent is hovered or opened while another submenu is open, the open submenu closes with reason `sibling-open` (MNU-PTR-11), and the same reason is used inside a Menubar when another bar menu opens; the close event is the pointer/keyboard event that opened the sibling, or `undefined` when triggered via the tree event without an originating event.
  Source: `menu/positioner/MenuPositioner.tsx:134-216`; `menu/root/MenuRoot.tsx:368-379`. Test: [L4] `MenuRoot.test.tsx` "should close submenu after delay when hovering a sibling item", "keeps the parent submenu open after a third-level submenu closes due to sibling hover"; `MenuRoot.triggerless.test.tsx` "closes a sibling submenu without closing the root on %s"; `MenuPositioner.test.tsx` "closes an open submenu with a sibling reason when its controlled parent closes".
- **MNU-DIS-06** — MUST. `item-press`: activating an item with `closeOnClick` closes the **entire** tree — the item's menu emits a tree `close` event with the click, every ancestor menu up to the root closes with reason `item-press` and the same `event`, and each ancestor's `onOpenChange(false, …)` fires innermost-first. Return focus goes to the root trigger. A `Menu.SubmenuTrigger` press never emits `item-press`. In a Menubar the whole bar menu closes.
  Source: `menu/item/useMenuItem.ts:24-38`; `menu/popup/MenuPopup.tsx:109-122`; `menu/root/MenuRoot.tsx:341-445`. Test: [L4] `MenuRoot.test.tsx` "calls onOpenChange with false exactly once per menu when a submenu item is clicked", "closes with a `detail === 0` click event on keyboard item activation", "reopens on hover after an impatient click closes via item press"; `MenuItem.test.tsx` "closes the menu when the item is clicked by default"; `MenuFilterRoot.scoping.test.tsx` "keeps focus off the input while an item press closes the whole menu".
- **MNU-DIS-07** — MUST. `cancel-open`: a press-and-drag that is released outside closes with reason `cancel-open` (MNU-PTR-04); the same reason is used when, after a `trigger-hover` open by this trigger, a document `mouseup` lands outside the trigger, its bounds and the popup (the one-shot `mouseup` listener installed for hover opens). Menu has no Menubar-specific `cancel-open` path: inside a bar the reason comes only from these two `mouseup` listeners. `cancel-open` closes do not run return focus (focus never left the trigger).
  Source: `menu/trigger/MenuTrigger.tsx:130-171,243-255`. Test: [L4] `MenuRoot.test.tsx` "closes the menu on click, drag outside, release"; `MenuTrigger.test.tsx` "keeps a hover-opened menu open when mouseup lands outside the trigger DOM but within its bounds".
- **MNU-DIS-08** — MUST. `focus-out`: focus leaving the popup and the trigger (Tab through the guard, Shift+Tab, programmatic focus to an element outside the tree) closes with reason `focus-out` and the originating focus/keyboard event as `event`; focus moving into a nested floating element or to another trigger of the same handle does not count. Canceling a `focus-out` close (`eventDetails.cancel()`) keeps the popup open and focus where it was.
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`; `utils/popups/useTriggerFocusGuards.ts:38-69`; `menu/popup/MenuPopup.tsx:208-216`. Test: [L4] `MenuRoot.test.tsx` "closes the menu and moves focus to the next element when tabbing forward from the open menu", "keeps focus in the menu when a Shift+Tab close is canceled", "returns focus on Escape after a controlled root ignores a Shift+Tab close"; popup-list suite "closes on Tab without committing, and moves focus past the trigger", "closes on Shift+Tab without committing, and moves focus to the element before the popup"; `MenuFilterRoot.test.tsx` "moves focus past the menu when tabbing from the input".
- **MNU-DIS-09** — MUST. Scroll lock: while a top-level menu is open with `modal: true` (default) and the open was not initiated by hover, document scrolling is locked (`useAnchoredPopupScrollLock`, PSH/UTL semantics: body overflow hidden with scrollbar compensation, restored on close); for touch pointers the lock is applied only when the positioner is at least as wide as the document viewport (within a small tolerance), so a narrow menu does not block page scrolling on mobile. Submenus and `modal: false` menus never lock. The internal backdrop (`InternalBackdrop`) is rendered under the same condition and intercepts outside presses (MNU-DOM-11).
  Source: `menu/positioner/MenuPositioner.tsx:245-253,264-290`; `utils/useAnchoredPopupScrollLock.ts`. Test: [L4] `MenuRoot.test.tsx` "should not apply scroll lock when opened via touch", "should apply scroll lock when opened via mouse", "should apply scroll lock when a touch-opened popup covers the viewport width", "should not apply scroll lock when a touch-opened popup is narrower than the viewport", "should render an internal backdrop when `true`", "should not render an internal backdrop when `false`"; `MenuBackdrop.test.tsx` "sets `pointer-events: none` style on backdrop if opened by hover", "does not set `pointer-events: none` style on backdrop if opened by click".
- **MNU-DIS-10** — MUST. Under a ContextMenu parent the Root's `outsidePress` predicate (FLT-DIS-09) vetoes outside presses during the first 500 ms after an open — a grace timer started when the menu opens and cleared on close — so the trailing `mousedown`/`pointerdown` of a long-press or similar opening gesture does not close the menu immediately. The grace is skipped when the current open was caused by a `contextmenu` event (the recorded open event's `type` is `contextmenu`): outside presses then close at once. Outside a ContextMenu the predicate always allows the close.
  Source: `menu/root/MenuRoot.tsx:312-333,406,501-512`; `context-menu/trigger/ContextMenuTrigger.tsx:69,82,141-142`; see FLT-DIS-09. Test: none in `menu/` (covered by the ContextMenu tests, CTX-DIS-01, CTX-DIS-02).
- **MNU-DIS-11** — MUST. Hover-close (pointer leaving trigger/popup) applies only to a menu whose current open was initiated by hover (`hoverEnabled` true and `openReason` hover-type); after a click or keyboard open the pointer may leave freely, and a hover-opened menu that is subsequently clicked (MNU-PTR-06) converts to a sticky open that no longer closes on leave. Stale hover timers scheduled before a non-hover open are discarded.
  Source: `menu/root/MenuRoot.tsx:335-339,371-379`; `menu/trigger/MenuTrigger.tsx:182-190`. Test: [L4] `MenuRoot.test.tsx` "does not close after hovering out of a popup opened without trigger hover", "does not close after hovering out of a popup opened externally", "closes after hovering out of a popup opened by its trigger", "treats hover-opened menus as modal after a click"; `MenuRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant after a hover close has started".
- **MNU-DIS-12** — MUST. When the Root is `disabled`, the menu cannot be opened by any user interaction (trigger press/hover/keyboard; `handle.open()` and a controlled `open` still work) and, for a menu held open by the `open` prop, the dismissal interactions are disabled too (`useDismiss` enabled = `!disabled`: neither Escape nor outside press requests a close), item activation and typeahead are disabled (MNU-KEY-14), and the Trigger renders `disabled`/`aria-disabled` per `nativeButton` (UBT-DOM-06, UBT-ARIA-02, UBT-ARIA-03).
  Source: `menu/root/MenuRoot.tsx:157,505`; `menu/trigger/MenuTrigger.tsx:214-222`. Test: [L4] `MenuRoot.test.tsx` "marks items as disabled when controlled open", "does not highlight items with text navigation when controlled open", "does not close or activate items when controlled open", "disables submenu triggers when controlled open"; `MenuTrigger.test.tsx` "should render a disabled button", "should not open the menu when clicked"; `MenuFilterRoot.test.tsx` "disables filter controls when the root is disabled".
- **MNU-DIS-13** — MUST. Reason `none` closes: the menu closes with reason `none` and no `event` when the controlling `open` prop flips to `false`, when the active trigger is unmounted or filtered out of a Menubar, when the Root unmounts while open (children), and when `actionsRef.current.unmount()` forces an unmount. These closes skip animations only for the unmount case and run return focus only when the trigger still exists.
  Source: `menu/root/MenuRoot.tsx:241-281,478-486`; `utils/popups/popupStoreUtils.ts:139-177`. Test: [L2] `MenuTrigger.test.tsx` "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed"; `MenuRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant when a controlled close commits late"; `MenuRoot.test.tsx` "unmounts the menu when the `unmount` method is called"; `MenuFilterRoot.test.tsx` "closes the submenu when the query filters out its trigger", "keeps an open submenu trigger mounted when its filter close is canceled". (L4 for the return-focus clause)

### 6.5 Reading direction

- **MNU-I18N-01** — MUST. The reading direction (`DirectionProvider`, default `ltr`) flips the cross-axis submenu keys: in RTL `ArrowLeft` opens a submenu from a vertical parent and `ArrowRight` closes it (MNU-KEY-15/16), the filterable key router classifies `ArrowLeft` as the submenu open key and `ArrowRight` as the close key (MNU-KEY-29/30/33), and the close-key handler of a filterable submenu popup uses the same mapping. Direction is read at render from the nearest `DirectionProvider`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:56-99`; `menu/filter-root/useMenuFilterKeyDown.ts:9,118-135`; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:157,195,249,287`; `menu/filter-root/useMenuFilterPopup.ts:22,151`. Test: [L4] `MenuRoot.test.tsx` "opens a nested menu of a $orientation $direction menu with $openKey key and closes it with $closeKey" (rtl rows); `MenuSubmenuTrigger.test.tsx` "opens the submenu with $openKey and highlights a single item in $direction direction"; `MenuFilterRoot.test.tsx` "uses RTL cross-axis keys to enter and leave a filterable submenu", "returns focus to the input with the RTL submenu close key"; `MenuFilterPopup.test.tsx` "returns focus with the %s %s close key"; `MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s" (ltr and rtl).
- **MNU-I18N-02** — MUST. In a `horizontal` menu the main axis follows the reading direction: in RTL `ArrowLeft` moves to the next item and `ArrowRight` to the previous (end/start keys swap, MNU-KEY-04), Home/End are unaffected, and the submenu open key of a horizontal parent remains `ArrowDown` (close `ArrowUp`) in both directions. Positioning sides `inline-start`/`inline-end` (submenu and vertical-menubar defaults) resolve to physical left/right per direction (FLT).
  Source: `floating-ui-react/hooks/useListNavigation.ts:56-99`; `menu/filter-root/moveHighlightFrom.ts:24-30`; `menu/positioner/MenuPositioner.tsx:86-96`. Test: [L4] `MenuRoot.test.tsx` "opens a nested menu of a $orientation $direction menu with $openKey key and closes it with $closeKey" (horizontal rtl row); `MenuFilterPopup.test.tsx` "returns focus with the %s %s close key" (horizontal rtl row).
- **MNU-I18N-03** — MUST. `Menu.FilterProvider`'s `locale` (an `Intl.LocalesArgument`, default: the runtime's default locale) selects the collator used by the default `contains` matcher (`getFilter({ locale })`, UTL filter semantics: case-, accent- and width-insensitive per locale); it does not affect typeahead (which is used only in plain menus and compares lower-cased code units).
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:33,83,265`; `menu/filter-provider/MenuFilterProvider.tsx:18-33`. Test: [L2] `MenuFilterRoot.test.tsx` "uses the configured locale for default matching".

## 7. Events and callbacks

| Callback | Part | Signature | Reasons | Req |
|---|---|---|---|---|
| `onOpenChange` | Root, SubmenuRoot | `(open, details: ChangeEventDetails)` | trigger-hover, trigger-focus, trigger-press, outside-press, focus-out, list-navigation, escape-key, item-press, sibling-open, cancel-open, imperative-action, none (`close-press` is declared only, never emitted) | MNU-EVT-01..07 |
| `onOpenChangeComplete` | Root, SubmenuRoot | `(open)` | — | MNU-EVT-06 |
| `onItemHighlighted` | Root, SubmenuRoot | `(item: HTMLElement \| undefined, details: HighlightEventDetails)` | keyboard, pointer, none | MNU-EVT-10..13 |
| `onCheckedChange` | CheckboxItem | `(checked, details)` | item-press | MNU-EVT-08 |
| `onValueChange` | RadioGroup | `(value, details)` | item-press | MNU-EVT-09 |
| `onClick` and other DOM handlers | Trigger, items, SubmenuTrigger, Input | React handlers | — | MNU-EVT-14, MNU-EVT-15 |
| `onValueChange` | FilterProvider | `(value: string, details)` | input-change, input-clear, clear-press, popup-close | MNU-EVT-16..19 |

### 7.1 Reason to action mapping

| Reason | Open or close | Produced by | Event type |
|---|---|---|---|
| `trigger-press` | both | Trigger / SubmenuTrigger mousedown, click (touch, virtual, keyboard) | `MouseEvent` / `PointerEvent` / `KeyboardEvent`-originated click |
| `trigger-hover` | both | Trigger with `openOnHover`, SubmenuTrigger hover, pointer leaving | `MouseEvent` / `PointerEvent` |
| `trigger-focus` | both | `Menu.Trigger` inside a Menubar while another bar menu is open: focusing the trigger opens, a later trigger blur closes; never emitted outside a Menubar, and never by ContextMenu | `FocusEvent` |
| `list-navigation` | both | arrow keys on trigger / submenu trigger / submenu popup | `KeyboardEvent` |
| `escape-key` | close | document Escape | `KeyboardEvent` |
| `outside-press` | close | press outside the tree | `PointerEvent` / `MouseEvent` |
| `focus-out` | close | focus guards, Shift+Tab, focus leaving the tree | `FocusEvent` / `KeyboardEvent` |
| `item-press` | close | item activation with `closeOnClick` (whole tree) | `MouseEvent` (click; `detail` 0 for keyboard) |
| `sibling-open` | close | another submenu/bar menu opens, or a parent closed without a reason | pointer event or `undefined` |
| `cancel-open` | close | press-and-drag released outside | `MouseEvent` (`mouseup`) |
| `imperative-action` | both | `handle.open()`/`handle.close()`, `actionsRef.current.close()` | `undefined` |
| `none` | both | controlled `open` change, trigger removal, parent unmount, `unmount()` | `undefined` |
| `close-press` | — | declared only (in `ChangeEventReason`), never emitted by any Menu part (§16 Q1) | — |

- **MNU-EVT-01** — MUST. `onOpenChange(nextOpen, details)` is called synchronously inside the interaction that requests the change, before any state is written, for every request where `nextOpen` differs from the current state or the reason/trigger differ (a repeated identical request, e.g. a relayed close after the state already closed, is dropped silently). `details` is a `BaseUIChangeEventDetails<ChangeEventReason>` (EVD-API-01, EVD-API-09): `reason`, `event` (the originating native event or `undefined`), `trigger` (the trigger element; on close it is filled with the active trigger when the request carried none), `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed`, plus `preventUnmountOnClose()` (MNU-EVT-03).
  Source: `menu/root/MenuRoot.tsx:341-376,939-942`. Test: [L2] popup conformance suite "is called with the new open state and the change details"; `MenuRoot.test.tsx` "calls onOpenChange with false exactly once per menu when a submenu item is clicked"; `MenuTrigger.test.tsx` "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed". (L4 for the popup conformance suite clause)
- **MNU-EVT-02** — MUST. Calling `details.cancel()` inside `onOpenChange` aborts the change: the store is not updated, no `floatingRootContext` open-change is dispatched, no transition starts, `aria-expanded`/`data-popup-open` keep their values, focus stays where it is, and tree children are not closed. A canceled *open* from a hover keeps the hover timers idle until the pointer re-enters.
  Source: `menu/root/MenuRoot.tsx:372-378`. Test: [L2] popup conformance suite "keeps the popup closed when the open change is canceled"; `MenuRoot.test.tsx` "keeps focus in the menu when a Shift+Tab close is canceled"; `MenuTrigger.test.tsx` "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed"; `MenuFilterRoot.test.tsx` "stays closed when onOpenChange is canceled", "keeps input focus when a Shift+Tab close is canceled". (L4 for the popup conformance suite and focus clauses)
- **MNU-EVT-03** — MUST. `details.preventUnmountOnClose()` (available only when `nextOpen` is `false`; on open it is a no-op) keeps the popup subtree mounted after the close commits (`preventUnmountingOnClose` store flag) until `actionsRef.current.unmount()` is called; a later normal close without the call unmounts as usual, and `unmount()` ends the closing phase immediately, running `onOpenChangeComplete(false)` once (TRN-ANI-18 semantics via `useOpenStateTransitions`).
  Source: `menu/root/MenuRoot.tsx:362-369,478-486`; `utils/popups/popupStoreUtils.ts:573-620`. Test: [L2] `MenuRoot.test.tsx` "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen", "unmounts the menu when the `unmount` method is called".
- **MNU-EVT-04** — MUST. Each reason is produced only by the interactions listed in §7.1, with the listed event type, and `details.trigger` is the element that initiated the open (the active trigger for closes). In particular `trigger-focus` is produced only by `Menu.Trigger` inside a Menubar while another bar menu is open (focus opens, trigger blur closes; MNU-FOC-16) — never outside a Menubar and never by ContextMenu — and `close-press` is declared only, never emitted by any Menu part. Ports MUST keep `close-press` in the reason union for type compatibility but need not emit it.
  Source: `internals/reason-parts.ts`; `menu/root/MenuRoot.tsx:924-937`; `menu/trigger/MenuTrigger.tsx:176-207`; `floating-ui-react/hooks/useFocus.ts:150-233`. Test: [L4] `MenuRoot.test.tsx` "calls onOpenChange with false exactly once per menu when a submenu item is clicked" (`item-press`), "closes the menu on click, drag outside, release" (`cancel-open`); `MenuPositioner.test.tsx` "closes an open submenu with a sibling reason when its controlled parent closes".
- **MNU-EVT-05** — MUST. `item-press` closes carry the item's native `click` event: `detail === 1` for a pointer click or a drag-release activation, `detail === 0` for Enter/Space activation (synthetic click) and for screen-reader presses. The same `event` object is passed to every ancestor's `onOpenChange` and to `finalFocus` type resolution (MNU-FOC-09).
  Source: `menu/item/useMenuItem.ts:24-38`; `menu/item/useMenuItemCommonProps.ts:102-125`. Test: [L4] `MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation"; `MenuPopup.test.tsx` "receives the interaction type of the item press that closed the menu", "receives the mouse interaction type after a drag-release item selection".
- **MNU-EVT-06** — MUST. `onOpenChangeComplete(open)` is called once per completed transition: `true` after the popup is mounted and its enter animations (if any) have finished, `false` after the exit animations have finished and the popup unmounted (or hidden under `keepMounted`), and never on initial mount when closed. With no animations both calls happen in the same tick as the state change (after commit). See MNU-ANI-06 for interaction with `preventUnmountOnClose`. Server: relaxed, see §9.
  Source: `menu/popup/MenuPopup.tsx:99-107`; `utils/popups/popupStoreUtils.ts:590-600`. Test: [L4] popup conformance suite "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes"; `MenuFilterRoot.test.tsx` "calls onOpenChangeComplete when the menu opens and closes".
- **MNU-EVT-07** — MUST. `Menu.SubmenuRoot`'s `onOpenChange` receives the same details shape with the submenu's reasons: `trigger-hover`/`trigger-press`/`list-navigation` on open, `escape-key`, `list-navigation`, `sibling-open`, `item-press`, `outside-press`, `focus-out`, `none` on close; when its parent closes, the submenu reports the parent's reason, or `sibling-open` when the parent closed without one (MNU-DIS-04). Each submenu reports exactly one `false` per close even when several dismissal paths fire together.
  Source: `menu/popup/MenuPopup.tsx:109-122`; `menu/positioner/MenuPositioner.tsx:134-227`. Test: [L4] `MenuRoot.test.tsx` "calls onOpenChange with false exactly once per menu when a submenu item is clicked"; `MenuPositioner.test.tsx` "closes an open submenu with a sibling reason when its controlled parent closes"; `MenuFilterRoot.test.tsx` "does not reclaim focus when a controlled submenu closes after focus moves outside".
- **MNU-EVT-08** — MUST. `Menu.CheckboxItem` calls `onCheckedChange(!checked, details)` on every activation (click, Enter/Space synthetic click, drag-release) with `details.reason === 'item-press'`, `details.event` = the native click, and a no-op `preventUnmountOnClose`; `details.cancel()` keeps the uncontrolled state unchanged. In controlled mode (`checked` prop) the item renders the prop value and the consumer decides. The callback fires before the menu closes when `closeOnClick` is set.
  Source: `menu/checkbox-item/MenuCheckboxItem.tsx:68-74,107-127`. Test: [L2] `MenuCheckboxItem.test.tsx` "calls `onCheckedChange` when the item is clicked", "does not toggle when `onCheckedChange` cancels the event", "keeps the state when closed and reopened"; `MenuFilterRoot.test.tsx` "leaves a checkbox item unchecked when onCheckedChange is canceled", "tracks a controlled checkbox item".
- **MNU-EVT-09** — MUST. `Menu.RadioGroup` calls `onValueChange(value, details)` when a `Menu.RadioItem` of the group is activated, with `details.reason === 'item-press'` and the native click; the callback is invoked even when the item is already selected; `details.cancel()` prevents the uncontrolled value update. The `value` prop makes the group controlled.
  Source: `menu/radio-group/MenuRadioGroup.tsx:33-48`; `menu/radio-item/MenuRadioItem.tsx:72-78`. Test: [L2] `MenuRadioItem.test.tsx` "calls `onValueChange` when the item is clicked", "does not select when `onValueChange` cancels the event", "keeps the state when closed and reopened".
- **MNU-EVT-10** — MUST. `onItemHighlighted(item, details)` reports the highlighted item element (`undefined` when the highlight is cleared) with `details.reason` ∈ {`keyboard`, `pointer`, `none`}, `details.event` (the keydown for keyboard highlights including typeahead; the `mousemove`/`pointermove`/`mouseenter` native event for pointer highlights; `undefined` otherwise) and `details.label` (the item's `label` prop or its text content, `undefined` when clearing). The details expose no positional index. It is invoked post-commit, pre-paint after `activeIndex` changes and again when the item registry settles so that an index pointing at a new element is re-reported.
  Source: `menu/root/MenuRoot.tsx:570-618,947-955`; `menu/root/MenuRoot.spec.tsx`. Test: [L4] `MenuRoot.test.tsx` "passes the keydown event for keyboard highlights", "passes the keydown event for typeahead highlights", "passes native mouse and pointer events for pointer highlights", "passes the mouse event when a submenu trigger is hovered"; `MenuFilterRoot.test.tsx` "passes the keydown event to onItemHighlighted in a %s menu"; `MenuFilterRoot.scoping.test.tsx` "reports keyboard highlights while the input keeps focus".
- **MNU-EVT-11** — MUST. The reason is the one attached to the store write that produced the committed `activeIndex`: `keyboard` for arrow/Home/End/typeahead/enter-list/submenu-return highlights, `pointer` for hover highlights, and `none` for automatic highlights (`autoHighlight`, initial keyboard-open highlight of a filterable menu, programmatic changes, highlight loss caused by list changes, Enter opening a filterable submenu). The reason tag is consumed once; a later unrelated list change never re-reports a stale reason.
  Source: `menu/root/MenuRoot.tsx:594-598`; `menu/store/MenuStore.ts:224-251`; `utils/getHighlightReason.ts`. Test: [L4] `MenuRoot.test.tsx` "does not report a stale highlight reason for a later list change"; `MenuFilterRoot.scoping.test.tsx` "reports the auto-highlighted item as a programmatic change"; `MenuFilterRoot.test.tsx` "reports no keyboard reason when Enter opens a filterable submenu".
- **MNU-EVT-12** — MUST. A clear (`item === undefined`) is reported when the pointer leaves the popup, when the menu closes with an item highlighted, when the trigger regains focus in a plain menu, when the query change drops the highlight, and when a parent's highlight is cleared on submenu entry; consecutive identical reports are suppressed (same element and same index).
  Source: `menu/root/MenuRoot.tsx:586-592`. Test: [L4] popup-list suite "clears the highlight when the pointer leaves the list without leaving focus on an item"; `MenuRoot.test.tsx` "reports clearing a highlight made before onItemHighlighted was added"; `MenuFilterRoot.test.tsx` "passes the keydown events when entering and leaving a filterable submenu".
- **MNU-EVT-13** — MUST. The callback is read through a stable reference: removing it and adding it back resumes reporting with the next change; when it is absent no reporting work is performed; a highlight that is kept across an item insertion/removal (same element) is not re-reported, while an index that now resolves to a different element is reported once the registry settles (reason `none`).
  Source: `menu/root/MenuRoot.tsx:570-618`. Test: [L2] `MenuRoot.test.tsx` "reports highlights correctly after onItemHighlighted is removed and added back"; `MenuFilterRoot.scoping.test.tsx` "does not report the item that takes the highlighted index when an item is inserted", "keeps the highlight when an item the query filters out is appended", "keeps the highlight when an item is appended after reopening from a filtered session".
- **MNU-EVT-14** — MUST. Consumer DOM handlers on `Menu.Trigger` run before the internal ones (RND-EVT-01/02): `onMouseDown` with `preventBaseUIHandler()` prevents the mouse open, `onClick` with `preventBaseUIHandler()` prevents the keyboard/virtual-click open, `onKeyDown` with `preventBaseUIHandler()` prevents arrow-key opening and the filter key relay; the handlers are still called exactly once per interaction.
  Source: `menu/trigger/MenuTrigger.tsx:243-255,336-350`; `menu/filter-root/MenuFilterDropdown.tsx:121-153`. Test: [L4] `MenuTrigger.test.tsx` "prevents opening the menu with a mouse when `preventBaseUIHandler` is called in onMouseDown", "prevents opening the menu with keyboard when `preventBaseUIHandler` is called in onClick"; `MenuFilterRoot.scoping.test.tsx` "lets a consumer onKeyDown cancel the relay"; `MenuFilterRoot.test.tsx` "runs a consumer handler once per interaction on each composed part".
- **MNU-EVT-15** — MUST. On items, the consumer `onClick` runs before the close/toggle logic and `preventBaseUIHandler()` in it prevents the close (the item still receives the click); `onMouseDown` with `preventBaseUIHandler()` prevents the internal mousedown handling (focus/arming); `onMouseEnter`/`onMouseMove` consumer handlers run before the highlight logic; on `Menu.Input` a consumer `onKeyDown` calling `preventBaseUIHandler()` opts that key out of list navigation and activation. Internal handlers on the Trigger and the items are composed so each consumer handler fires exactly once per interaction.
  Source: `menu/item/useMenuItem.ts:24-38`; `menu/item/useMenuItemCommonProps.ts:92-125`; `menu/filter-root/useMenuFilterKeyDown.ts:118-135`. Test: [L2] `MenuItem.test.tsx` "calls the onClick handler when clicked", "does not close the menu when onClick prevents Base UI handler", "allows onMouseDown to call preventBaseUIHandler"; `MenuFilterRoot.test.tsx` "lets a consumer key handler on the input opt out of list navigation and activation", "runs a consumer handler once per interaction on each composed part". (L4 for the `onMouseDown`, `onMouseEnter`/`onMouseMove` and `Menu.Input` `onKeyDown` clauses)
- **MNU-EVT-16** — MUST. `Menu.FilterProvider`'s `onValueChange(value, details)` is called with `reason: 'input-change'` and the native `input` event for every keystroke/paste that changes the input's value, and with `reason: 'input-clear'` when an edit empties the field (Backspace to empty, select-all + delete); `details.cancel()` leaves the uncontrolled query and the visible items unchanged while the native input value is reverted on the next render. In controlled mode (`value` prop) the visible items follow the prop.
  Source: `filter-dropdown/root/FilterDropdownRoot.tsx:98-107`; `filter-dropdown/input/FilterDropdownInput.tsx:44-53`. Test: [L2] `MenuFilterRoot.test.tsx` "reports input-change while typing", "reports input-clear when the field is emptied", "leaves the uncontrolled query and visible items unchanged when a change is canceled", "supports a controlled query"; `MenuFilterRoot.scoping.test.tsx` "supports a controlled query"; `MenuFilterSubmenuRoot.test.tsx` "renders the controlled query and reports changes", "keeps the uncontrolled query when the change is canceled".
- **MNU-EVT-17** — MUST. Pressing `Menu.Clear` calls `onValueChange('', details)` with `reason: 'clear-press'` and the click event (MNU-PTR-21).
  Source: `filter-dropdown/clear/FilterDropdownClear.tsx:30-62`. Test: [L2] `MenuFilterRoot.test.tsx` "reports clear-press when the clear button is used".
- **MNU-EVT-18** — MUST. When the popup closes (any reason) with a non-empty query, `onValueChange('', details)` is called once with `reason: 'popup-close'` and no event, after the close commits; the query and the highlight are reset for the next open unless `details.cancel()` is called, in which case the query (and the filtered list) persist across the close and reopen. With `keepMounted`, the reset still happens on close so a reopen starts from an empty query and the initial highlight is re-applied. The reset is skipped for an empty query.
  Source: `filter-dropdown/root/useFilterDropdownCloseQuery.ts:19-38`; `menu/filter-root/MenuFilterDropdown.tsx:68-73`. Test: [L2] `MenuFilterRoot.test.tsx` "reports popup-close when a controlled close discards the query", "resets the input value once when the popup closes", "keeps the query when the popup-close reset is canceled", "resets the query and highlight across a close and reopen", "closes and resets after a query was typed into the hover-opened submenu".
- **MNU-EVT-19** — MUST. During an IME composition (`compositionstart` … `compositionend`) the in-progress text is **not** reported or filtered: the matching runs and `input-change` fires on the committed text at `compositionend` (and for every non-composing `input` event). Keydowns with `which === 229` are ignored throughout (MNU-KEY-31).
  Source: `filter-dropdown/input/FilterDropdownInput.tsx:59-110`; `menu/filter-root/useMenuFilterKeyDown.ts:59-61`. Test: [L4] `MenuFilterRoot.test.tsx` "filters on the committed text, not the composition in progress", "does not activate the highlighted item when Enter commits an IME composition", "does not forward submenu navigation keys while composing text".

## 8. Form integration

Not applicable — Menu has no form value, renders no hidden input, does not participate in `Field`/`Form` validation, and exposes no `name` prop. (`Menu.CheckboxItem`/`Menu.RadioGroup` hold UI state only; consumers wire it to forms themselves.) No MNU-FRM requirements are issued.

## 9. Lifecycle, mounting and animation

- **MNU-ANI-01** — MUST. The Positioner/Popup subtree is mounted while `mounted` is `true`: from the open request until the exit transition completes (`useOpenStateTransitions` → `useUnmountAfterClose`, TRN-ANI-12..18); with `Menu.Portal keepMounted` the subtree stays mounted and is `hidden` while closed (Positioner `hidden` attribute, PSH-STA-30; the Backdrop likewise receives `hidden` while not mounted). `Menu.Backdrop` is rendered iff mounted or `keepMounted`. Server: relaxed, see §9.
  Source: `menu/root/MenuRoot.tsx:226-233`; `menu/portal/MenuPortal.tsx:29-54`; `utils/usePositioner.tsx:24-40`; `menu/backdrop/MenuBackdrop.tsx:24-45`. Test: [L4] `MenuPositioner.test.tsx` "when keepMounted=true, should keep the content mounted when closed", "when keepMounted=false, should unmount the content when closed"; popup conformance suite "removes the popup when there is no exit animation defined".
- **MNU-ANI-02** — MUST. `transitionStatus` follows TRN-STA-01: `'starting'` on the first frame(s) after mount while open, `undefined` while open and settled, `'ending'` after a close request until animations finish (then unmount or hide), and `undefined` while closed-and-mounted (`keepMounted`). Positioner, Popup and Backdrop emit `data-starting-style`/`data-ending-style` from it (TRN-DOM-07..09; MNU-DOM-04/05/11), and the state object exposed to `className`/`style`/`render` functions carries `transitionStatus`.
  Source: `menu/root/MenuRoot.tsx:226-233`; `utils/popupStateMapping.ts`; `menu/popup/MenuPopup.tsx:141-153`. Test: [L4] `MenuPopupTransitionState.test.tsx` "starts each opening once (controlled=$controlled, keepMounted=$keepMounted, defaultOpen=$defaultOpen)"; popup conformance suite "is called on open once the enter animation finishes".
- **MNU-ANI-03** — MUST. A menu that is open on its first render (`defaultOpen`, controlled `open: true` at mount) does not go through `'starting'` and plays no enter transition; the same holds for a submenu rendered open inside such a parent and for a submenu whose parent popup is kept mounted (`keepMounted`), because the parent's status is `undefined` at the child's mount. A submenu that is open when its parent opens *interactively* (parent status `'starting'`) **does** animate in with its parent (`animateInitialOpen`), including a controlled-open submenu.
  Source: `menu/root/MenuRoot.tsx:135-147,226-233`. Test: [L4] `MenuPopupEnterTransition.test.tsx` "does not play the enter transition for a menu that is open on the first render", "does not play the enter transition for a submenu inside a menu that is open on the first render", "does not play the enter transition for a submenu when the parent popup is kept mounted", "plays the enter transition for a submenu that is open when its parent opens", "plays the enter transition for a controlled-open submenu when its parent opens", "plays the enter transition for a submenu opened by the user".
- **MNU-ANI-04** — MUST. `instantType` seeding: a submenu that mounts open while its parent opened instantly (parent `instantType` set, e.g. keyboard click) copies the parent's `instantType` into its own store at first render so `[data-instant]` suppresses the enter transition on both popups or on neither; the seeded value is cleared once the enter phase settles (after `useAnimationsFinished` on the popup), immediately when a controlled close interrupts the entry, and immediately when there is no popup element to animate; a submenu that is closed at mount and opened later programmatically is never marked instant.
  Source: `menu/root/MenuRoot.tsx:148-176,241-281`. Test: [L2] `MenuPopupEnterTransition.test.tsx` "marks an initially open submenu as instant when its parent opened instantly", "plays the enter transition for a submenu whose parent opened instantly", "clears the inherited instant type after the initial open", "clears the inherited instant type when a controlled close interrupts the entry", "clears the inherited instant type when the popup is not initially rendered", "does not mark a closed submenu as instant when it is later opened programmatically".
- **MNU-ANI-05** — SHOULD. Known limitation: a submenu whose parent popup is kept mounted (`keepMounted` on the parent's Portal) is treated as page-load content on the parent's first open and does not play its enter transition (see MNU-ANI-03 and §16 Q4). Ports MAY improve on this but MUST NOT animate the page-load case.
  Source: `menu/root/MenuRoot.tsx:139-147`. Test: [L4] `MenuPopupEnterTransition.test.tsx` "does not play the enter transition for a submenu when the parent popup is kept mounted".
- **MNU-ANI-06** — MUST. `onOpenChangeComplete(true)` fires after the popup's enter animations finish (`useOpenChangeComplete` on the popup element) and `onOpenChangeComplete(false)` fires when the exit completes and the subtree unmounts/hides; when `preventUnmountOnClose()` was called, the `false` call is deferred until `actionsRef.current.unmount()` and is made exactly once. The callback is read through the store context so a changed prop is honored without re-subscribing. Server: relaxed, see §9.
  Source: `menu/popup/MenuPopup.tsx:99-107`; `menu/root/MenuRoot.tsx:181`; `utils/popups/popupStoreUtils.ts:573-620`. Test: [L4] popup conformance suite "is called on close once the exit animation finishes"; `MenuRoot.test.tsx` "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen"; `MenuFilterRoot.test.tsx` "calls onOpenChangeComplete when the menu opens and closes".
- **MNU-ANI-07** — MUST. `instantType` (projected as `data-instant="<type>"` on Positioner and Popup, MNU-DOM-04/05) is set on every open-state write: `'group'` inside a Menubar for `trigger-focus`, `focus-out`, `trigger-hover`, `list-navigation` and `sibling-open` changes (moving between bar menus must not animate); `'click'` for keyboard clicks (`detail === 0` click, Enter/Space) on open; `'dismiss'` for closes with reason `escape-key` or no reason; `'trigger-change'` while switching triggers (MNU-ANI-08); `undefined` otherwise (pointer opens and closes animate). A hover open followed by a patient click re-attributes to `'click'`.
  Source: `menu/root/MenuRoot.tsx:400-445`; `utils/popups/popupStoreUtils.ts`. Test: [L2] `MenuPopupEnterTransition.test.tsx` "marks an initially open submenu as instant when its parent opened instantly"; `MenuRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers". (L4 for the hover and keyboard open reasons)
- **MNU-ANI-08** — MUST. Trigger switching (detached triggers, PSH-STA-39..42 / TRN-ANI-28..31): while the popup is open and another trigger of the same handle is pressed, the popup stays mounted, `instantType` becomes `'trigger-change'` for the re-anchoring frame and is cleared afterwards; it is not applied to a hover close that starts after the switch and is not restored when a controlled close commits late. The Viewport morphs between the old and new content (`viewportConformanceTests`).
  Source: `menu/root/MenuRoot.tsx:285-310`; `internals/useTriggerSwitchTransition.ts`. Test: [L4] detached-triggers suite "reuses the popup DOM nodes when switching triggers", "does not leave an inline scale style on the popup after switching triggers"; `MenuRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers", "does not restore the trigger-change instant when a controlled close commits late", "does not restore the trigger-change instant after a hover close has started".
- **MNU-ANI-09** — MUST. Re-opening during the exit phase (`'ending'`) cancels the exit: the subtree stays mounted, `transitionStatus` returns to `undefined`, and no `onOpenChangeComplete(false)` fires for the interrupted close; keys and text typed into a filterable popup while it animates out are ignored, and submenu filtering keeps updating during its parent's exit transition.
  Source: `utils/popups/popupStoreUtils.ts:573-620`; `menu/filter-root/MenuFilterDropdown.tsx`. Test: [L4] `MenuPopupTransitionState.test.tsx` "starts each opening once (controlled=$controlled, keepMounted=$keepMounted, defaultOpen=$defaultOpen)"; `MenuFilterRoot.test.tsx` "ignores %s in the input while the popup animates out", "ignores text typed in the input while the popup animates out with $name", "keeps submenu filtering updated during the exit transition", "does not highlight the first item while closing from the trigger with a pointer".
- **MNU-ANI-10** — MUST. `actionsRef.current.unmount()` forces the subtree to unmount immediately (skipping any running exit animation and the `preventUnmountOnClose` hold), resets `preventUnmountingOnClose`, sets `mounted: false`, and runs `onOpenChangeComplete(false)` once; calling it while open is a no-op beyond the above. `actionsRef.current.close()` requests a close with reason `imperative-action`.
  Source: `menu/root/MenuRoot.tsx:478-486`; `utils/popups/popupStoreUtils.ts:573-620`. Test: [L2] `MenuRoot.test.tsx` "unmounts the menu when the `unmount` method is called", "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen".
- **MNU-ANI-11** — MUST. `Menu.Backdrop` shares the Root's `transitionStatus` and does not start an entry phase when the menu is initially open (controlled or `defaultOpen`); while the menu was opened by hover it carries `pointer-events: none` so hovering across it does not interfere with the safe polygon, and `user-select: none` always.
  Source: `menu/backdrop/MenuBackdrop.tsx:24-50`. Test: [L4] `MenuBackdrop.test.tsx` "does not start an entry phase when initially open (controlled=%s)", "sets `pointer-events: none` style on backdrop if opened by hover", "does not set `pointer-events: none` style on backdrop if opened by click".
- **MNU-ANI-12** — MUST. While `transitionStatus === 'starting'` the Popup carries the disabled-mount-transition inline style (`transition: none` for the first frame, TRN-ANI-05 via `getDisabledMountTransitionStyles`) so `data-starting-style` styles apply without transitioning from the unmounted state.
  Source: `menu/popup/MenuPopup.tsx:176`. Test: [L4] `MenuPopupEnterTransition.test.tsx` "plays the enter transition for a submenu opened by the user" (asserts the transition runs after the starting frame).
- **MNU-ANI-13** — MUST. `Menu.CheckboxItemIndicator` and `Menu.RadioItemIndicator` run their own transition machine on the parent item's `checked`: the element mounts when checked, gains `data-starting-style` on the first frame, and on uncheck gains `data-ending-style` and stays mounted until its exit animation finishes (then unmounts unless `keepMounted`); with no exit animation it unmounts immediately. `useOpenChangeComplete` watches the indicator element for the animations. Server: relaxed, see §9.
  Source: `menu/checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:21-60`; `menu/radio-item-indicator/MenuRadioItemIndicator.tsx`. Test: [L4] `MenuCheckboxItemIndicator.test.tsx` and `MenuRadioItemIndicator.test.tsx` "should remove the indicator when there is no exit animation defined", "should remove the indicator when the animation finishes", "keeps the indicator mounted to play its exit animation when unchecked without keepMounted".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- MNU-ANI-01 — TRN-ANI-03/09/10 row: after the exit animation the unmount (or `hidden`) lands in the first render batch after the completion message, about two round trips later; JS sets `hidden` at animation end (optimistic convergent write, ADR 04 §2) so the popup is not visible meanwhile.
- MNU-ANI-06 — TRN-ANI-03/09/10 row: `onOpenChangeComplete(false)` and the unmount commit run in that first render batch after the completion message, not in the microtask the animation ends.
- MNU-ANI-13 — TRN-ANI-03/09/10 row: the indicator's removal after its exit animation lands about two round trips later; JS hides it at animation end in the meantime.
- MNU-EVT-06 — TRN-ANI-03/09/10 row: as MNU-ANI-06; with no animations the completion call still follows the completion message rather than the state change's own tick.
- MNU-KEY-09 — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01 row: the consumer's `cancel()` / `allowPropagation()` decision arrives a round trip after the Escape dispatch; JS applies the ADR 04 §3 fallback (prevent and stop whenever this menu is the innermost open popup). WebAssembly meets the spec through the synchronous `decide()` path.
- MNU-DOM-06 — RND-STA-01 row (mirrored fields): the Popup's `data-side`/`data-align` come from the mirrored `side`/`align` state, which updates one round trip after the Positioner's JS-written attributes.
- MNU-POS-06 — RND-STA-01 row (mirrored fields): the Positioner's `data-side`/`data-align`/`data-anchor-hidden` are JS-written at frame timing; the `side`/`align` state fields (and so the Popup's attributes and `ClassValue`/`StyleValue`) follow one round trip later.

## 10. Positioning

Menu delegates to the shared anchored-positioning engine (FLT: `useAnchorPositioning`, `usePositioner`). This section lists only what Menu configures.

- **MNU-POS-01** — MUST. The Positioner passes its `anchor` prop to anchored positioning (under a ContextMenu parent: the prop, else the context menu's pointer-position virtual element); with no anchor the popup is positioned against the active trigger element (`floatingRootContext.domReferenceElement`, PSH-STA-27). Anchor resolution is per FLT-API-11 and FLT-POS-13: an element, a DOM handle, a function returning an element (which may be non-memoized), or a virtual element with `getBoundingClientRect`; an `anchor` that changes from a DOM handle to `undefined` and back while open re-anchors the popup.
  Source: `menu/positioner/MenuPositioner.tsx:76-83,106-110`; see FLT-API-11, FLT-POS-13, FLT-POS-17. Test: [L4] `MenuPositioner.test.tsx` "should be placed near the specified element when a ref is passed", "should be placed near the specified element when an element is passed", "should be placed near the specified element when a function returning an element is passed", "should be placed at the specified position", "should accept a non-memoized function as an anchor", "should react to the anchor changing from a ref to undefined and back"; `MenuRoot.triggerless.test.tsx` "supports an external anchor across reopen, keepMounted=%s".
- **MNU-POS-02** — MUST. The Positioner passes these defaults by parent kind to anchored positioning (placement, offsets, padding and collision behavior then per FLT-POS-01, FLT-POS-06, FLT-API-08, FLT-API-12, FLT-POS-14, FLT-POS-15, FLT-I18N-01): top-level menu `side: 'bottom'`, `align: 'center'` (engine defaults), `sideOffset: 0`, `alignOffset: 0`, `collisionBoundary: 'clipping-ancestors'`, `collisionPadding: 5`, `arrowPadding: 5`, `sticky: false`, `positionMethod: 'absolute'`, `disableAnchorTracking: false`; submenu (parent `'menu'`) `side: 'inline-end'`, `align: 'start'`; menubar child `side: 'bottom'` for a horizontal bar / `'inline-end'` for a vertical bar, `align: 'start'`; context menu `align: 'start'` and, when no `side` is given and `align !== 'center'`, `alignOffset: 2` and `sideOffset: -5` (explicit props win), `arrowPadding: 0`; and `positionMethod: 'fixed'` for every menu rendered inside a `ContextMenu.Root` (MNU-POS-04). `sideOffset`/`alignOffset` accept numbers or functions of the positioning data.
  Source: `menu/positioner/MenuPositioner.tsx:39-55,76-100,106-132`; see FLT-POS-01, FLT-POS-06, FLT-API-08, FLT-API-12, FLT-POS-15. Test: [L2] `MenuPositioner.test.tsx` "uses bottom as the default side when the menubar is horizontal", "uses inline-end as the default side when the menubar is vertical", "preserves explicit context-menu placement and offsets"; positioner conformance suite (`viewport: true`) "offsets the positioner when a number is specified", "offsets the positioner when a function is specified", "rests exactly at collisionPadding from the colliding edge". (L4 for the resulting placement)
- **MNU-POS-03** — MUST. Collision avoidance defaults passed to anchored positioning (resolved per FLT-API-10, applied per FLT-POS-03 … FLT-POS-05): top-level and menubar menus use `DROPDOWN_COLLISION_AVOIDANCE` (`{ side: 'flip', align: 'shift', fallbackAxisSide: 'none' }`), submenus use `POPUP_COLLISION_AVOIDANCE` (`fallbackAxisSide: 'end'`) so a submenu that cannot fit on either inline side falls back to the block axis; the Positioner `collisionAvoidance` prop overrides either.
  Source: `menu/positioner/MenuPositioner.tsx:52,92-94`; `internals/useAnchorPositioning.ts:167-169`; see FLT-API-10, FLT-POS-03, FLT-POS-04, FLT-POS-05. Test: [L2] `MenuPositioner.test.tsx` positioner conformance suite "rests exactly at collisionPadding from the colliding edge"; (submenu fallback: none). (L4 for the resulting placement)
- **MNU-POS-04** — MUST. Under a ContextMenu parent the Positioner passes `shift: { crossAxis: !(collisionAvoidance.side === 'flip'), rootBoundary: 'layoutViewport' }` (FLT-POS-05), so the positioner shifts within the **layout** viewport with cross-axis shifting enabled unless side collision avoidance is `flip`; a submenu of a context menu uses the visual viewport like any submenu. `positionMethod: 'fixed'` (FLT-POS-12) is passed for every menu rendered inside a `ContextMenu.Root`, whatever its parent kind and `positionMethod` prop (MNU-EDGE-15).
  Source: `menu/positioner/MenuPositioner.tsx:59,104-132`; see FLT-POS-05, FLT-POS-12. Test: [L2] `MenuPositioner.test.tsx` "uses the layout viewport for a root context menu", "disables cross-axis shifting when side collision avoidance is flip", "uses the visual viewport for a context menu submenu". (L4 for the resulting placement)
- **MNU-POS-05** — MUST. The Positioner passes `lazyFlip: 'placement'` for a filterable (virtual-focus) menu and `lazyFlip: false` otherwise (FLT-ANI-04): once placed, the popup does not flip sides while the user types and the result list grows/shrinks (its placement is recomputed only on anchor/scroll changes), so the input stays put.
  Source: `menu/positioner/MenuPositioner.tsx:131`; see FLT-ANI-04. Test: [L2] `MenuPositioner.test.tsx` "enables lazy flipping for a filter menu", "leaves lazy flipping off for a plain menu". (L4 for the lazy-flip placement result)
- **MNU-POS-06** — MUST. Per FLT-DOM-08, FLT-POS-10 and FLT-I18N-02, the resolved placement is exposed as `data-side` (`top`/`bottom`/`left`/`right`/`inline-start`/`inline-end`) and `data-align` (`start`/`center`/`end`) on both Positioner and Popup (after collision handling, so a flipped menu reports the flipped side), and in the `side`/`align` state fields; `data-anchor-hidden` appears when the anchor is scrolled out of its clipping ancestors (FLT-POS-09). Server: relaxed, see §9.
  Source: `menu/positioner/MenuPositioner.tsx:229-243`; `menu/popup/MenuPopup.tsx:141-148`; `utils/popupStateMapping.ts`; see FLT-DOM-08, FLT-POS-09, FLT-POS-10, FLT-I18N-02. Test: [L4] `MenuPositioner.test.tsx` positioner conformance suite; `MenuPopupEnterTransition.test.tsx` (reads `data-side`); `MenuRoot.triggerless.test.tsx` "supports an external anchor across reopen, keepMounted=%s".
- **MNU-POS-07** — MUST. `Menu.Arrow` reads `arrowRef`, `arrowStyles`, `side`, `align`, `arrowUncentered` from the Positioner context, applies the computed inline position styles (FLT-DOM-09, FLT-POS-08), and exposes `data-side`, `data-align` and `data-uncentered` (when the arrow had to be offset from the popup's center to stay within `arrowPadding`); it throws when rendered outside `Menu.Positioner`.
  Source: `menu/arrow/MenuArrow.tsx:23-45`; `menu/positioner/MenuPositionerContext.ts:17-24`; see FLT-DOM-09, FLT-POS-08. Test: [L4] `MenuArrow.test.tsx` describeConformance.
- **MNU-POS-08** — MUST. The Positioner's inline style carries the engine's CSS variables `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`, `--positioner-width`, `--positioner-height` (FLT-DOM-05 … FLT-DOM-07 / `CommonPositionerCssVars`), positioned with `transform` by default (FLT-DOM-03) and with `top`/`left` when a `Menu.Viewport` is rendered (adaptive origin, FLT-DOM-04) (so the viewport's morphing containers are not scaled), updating when the Viewport mounts or unmounts.
  Source: `utils/usePositioner.tsx`; `menu/positioner/MenuPositioner.tsx:255-262`; see FLT-DOM-03, FLT-DOM-04, FLT-DOM-05, FLT-DOM-06, FLT-DOM-07. Test: [L4] positioner conformance suite "uses transform positioning without Viewport", "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
- **MNU-POS-09** — MUST. The Positioner passes `disableAnchorTracking` (default `false`), `sticky` (default `false`) and `collisionBoundary` to anchored positioning: anchor tracking keeps the popup attached to the trigger while ancestors scroll or resize (FLT-POS-11); `sticky: true` keeps it inside the boundary when the anchor scrolls out (FLT-POS-14); `collisionBoundary` accepts the engine's boundary values (FLT-API-09). The positioner element is `role="presentation"` and `hidden` while not mounted (`keepMounted`) per FLT-DOM-01.
  Source: `menu/positioner/MenuPositioner.tsx:48-51,106-132`; `utils/usePositioner.tsx:24-40`; see FLT-POS-11, FLT-POS-14, FLT-API-09, FLT-DOM-01. Test: [L2] `MenuPositioner.test.tsx` "when keepMounted=true, should keep the content mounted when closed"; positioner conformance suite. (L4 for the anchor-tracking and sticky results)
- **MNU-POS-10** — MUST. When the active trigger changes while open (detached triggers), the popup re-anchors to the new trigger with the trigger-switch transition (`useTriggerSwitchTransition`, PSH-STA-41): the positioner is re-measured, `--anchor-*` variables update, and an inline scale style used for the morph is removed afterwards; the Viewport's `data-activation-direction` reflects the direction from the previous to the new trigger.
  Source: `menu/positioner/MenuPositioner.tsx:106-132`; `menu/root/MenuRoot.tsx:285-310`; `menu/viewport/MenuViewport.tsx:17-40`. Test: [L4] detached-triggers suite "reuses the popup DOM nodes when switching triggers", "does not leave an inline scale style on the popup after switching triggers"; viewport conformance suite "sets `data-activation-direction` from the previous to the new trigger", "remounts the `current` container when the active trigger changes".

## 11. Cross-cutting dependencies

| Mechanism | Doc / ID | What Menu configures |
|---|---|---|
| `useRenderElement`, state attribute mapping, `render`/`className`/`style` functions | RND (rendering-and-composition) | all 28 parts; mappings `pressableTriggerOpenStateMapping` (Trigger), `triggerOpenStateMapping` (SubmenuTrigger), `popupStateMapping` (Positioner), `popupTransitionStateMapping` (Popup, Backdrop), `itemMapping` (items, indicators), `popupViewportStateMapping` (Viewport) |
| `useButton` | UBT | Trigger (`nativeButton` default `true`, `focusableWhenDisabled` false), SubmenuTrigger and every item (`focusableWhenDisabled: true`, `native: false` unless `nativeButton`), `Menu.Clear` |
| Popup store, handles, detached triggers, focus guards, open-state transitions | PSH | `MenuStore extends ReactStore<PopupStoreState>`; `createMenuHandle`; `useTriggerRegistration`, `useTriggerDataForwarding`, `useImplicitActiveTrigger`, `useTriggerFocusGuards`, `useOpenStateTransitions`, `createPopupOpenState`, `attachPreventUnmountOnClose` |
| Change / highlight event details and reasons | EVD | reason unions in §7; `createChangeEventDetails`, `createGenericEventDetails` |
| Transitions, `useTransitionStatus`, `useOpenChangeComplete`, `useAnimationsFinished` | TRN | §9; indicators run their own `useTransitionStatus` |
| List navigation, typeahead, composite list | CMP (CMP-KEY-19 … CMP-KEY-34, CMP-KEY-35 … CMP-KEY-43, CMP-STA-31 … CMP-STA-45, CMP-PTR-04/05) | `useListNavigation` options in §4/§6: `loop`, `orientation`, `nested`, `virtual`, `allowEscape`, `focusItemOnOpen`, `openOnArrowKeyDown`, `resetOnPointerLeave`, `disabledIndices: []`, `nestedReturnFocusRef`, `triggerOrientation`; `useTypeahead` with `resetMs: 500`, disabled for filterable menus |
| Click, hover, dismiss, focus interactions | FLT (FLT-PTR-01 … FLT-PTR-06 click, FLT-PTR-11 … FLT-PTR-25 hover/safe polygon, FLT-DIS-01 … FLT-DIS-16 and FLT-KEY-01 … FLT-KEY-03 dismiss, FLT-FOC-01 … FLT-FOC-04 focus) | `useClick({ event: 'mousedown', toggle, stickIfOpen })` (Menubar: `useFocus` while a bar menu is open); hover `{ restMs/delay, closeDelay, safePolygon, mouseOnly, move }`; `useDismiss({ escapeKey, bubbles: { escapeKey: closeParentOnEsc }, outsidePress fn, externalTree })` |
| Focus management | FLT (FLT-FOC-05 … FLT-FOC-22, FLT-ARIA-01/02) | `modal` only under ContextMenu, `initialFocus`, `returnFocus`/`explicitReturnFocus`, `restoreFocus`, `getInsideElements`, `disabled` while closed |
| Floating tree events | FLT (FLT-STR-06 … FLT-STR-08, FLT-EVT-03) | `menuopenchange`, `itemhover`, `close`, `setOpen`, `floating.closed` |
| Anchored positioning, positioner element, scroll lock, viewport | FLT (§4.12, §6.1) / `internals/useAnchorPositioning.ts`, `utils/usePositioner.tsx`, `utils/useAnchoredPopupScrollLock.ts`, `utils/usePopupViewport.tsx`, `utils/InternalBackdrop.tsx` | §10; scroll lock enabled per MNU-DIS-09; Viewport via `usePopupViewport` |
| Shared utilities | UTL | `useControlled` (open, checked, radio value, query), `useBaseUiId`/`useRenderedId`, `useStableCallback`, `useTimeout`, `useAnimationFrame`, `ownerDocument`, `mergeProps`, `getFilter`/`useFilter` (locale-aware matching), `resolvePopupLabel`, `dispatchClickWithModifiers`, `isElementDisabled`, `warn` |
| Filter dropdown engine (consumed, not owned) | `filter-dropdown/*` | `FilterDropdownRoot` matching/highlight state machine, `FilterDropdownInput`, `FilterDropdownList`, `useFilterDropdownItem`, `useFilterDropdownGroup`, `FilterDropdownClear`, `FilterDropdownEmpty`, `useFilterDropdownCloseQuery`, `refocusOwner` |
| Composite parents | Menubar, ContextMenu, Toolbar, Popover, Dialog docs | parent kind detection (`MenuParent`), keyboard relay, `instantType: 'group'`, context-menu anchoring/modality, toolbar key stopping, Popover role suppression |

## 12. Edge cases, invariants and dev warnings

- **MNU-EDGE-01** — MUST. `Menu.Trigger` rendered with neither an ancestor `Menu.Root` nor a `handle` throws `Error("Base UI: <Menu.Trigger> must be either used within a <Menu.Root> component or provided with a handle.")`.
  Source: `menu/trigger/MenuTrigger.tsx:68-73`. Test: [L2] `MenuTrigger.test.tsx` "throws without Menu.Root or a handle"; `MenuFilterParts.test.tsx` "throws when Trigger is rendered without a root or handle".
- **MNU-EDGE-02** — MUST. In development, a nested menu (any parent kind) that receives an explicit `modal` prop logs `console.warn("Base UI: The `modal` prop is not supported on nested menus. It will be ignored.")` on every render; nothing is logged in production and the prop is ignored either way.
  Source: `menu/root/MenuRoot.tsx:207-213`. Test: [L2] `MenuRoot.test.tsx` "warns that nested menus ignore the modal prop", "does not emit the nested modal warning in production".
- **MNU-EDGE-03** — MUST. Context errors (verbatim): `Menu.SubmenuTrigger` outside `Menu.SubmenuRoot` throws `"Base UI: <Menu.SubmenuTrigger> must be placed in <Menu.SubmenuRoot>."`; `Menu.SubmenuRoot` outside a menu throws the Root context error `"Base UI: MenuRootContext is missing. Menu parts must be placed within <Menu.Root>."` (also thrown by Item, LinkItem, CheckboxItem, RadioGroup, List, Popup, Backdrop and any part reading the root context outside a Root); Positioner parts outside `Menu.Positioner` throw `"Base UI: MenuPositionerContext is missing. MenuPositioner parts must be placed within <Menu.Positioner>."` (Popup, Arrow); `Menu.Positioner` outside `Menu.Portal` throws `"Base UI: <Menu.Portal> is missing."`; `Menu.GroupLabel` outside a group throws `"Base UI: MenuGroupContext is missing. Menu group parts must be used within <Menu.Group> or <Menu.RadioGroup>."`; `Menu.RadioItem` outside `Menu.RadioGroup` throws `"Base UI: MenuRadioGroupContext is missing. MenuRadioGroup parts must be placed within <Menu.RadioGroup>."`; `Menu.CheckboxItemIndicator` outside `Menu.CheckboxItem` throws `"Base UI: MenuCheckboxItemContext is missing. MenuCheckboxItem parts must be placed within <Menu.CheckboxItem>."`; `Menu.RadioItemIndicator` outside `Menu.RadioItem` throws `"Base UI: MenuRadioItemContext is missing. MenuRadioItem parts must be placed within <Menu.RadioItem>."`.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:47-50`; `menu/root/MenuRootContext.ts:27-34`; `menu/positioner/MenuPositionerContext.ts:17-24`; `menu/portal/MenuPortalContext.ts:9`; `menu/group/MenuGroupContext.ts:9-16`; `menu/radio-group/MenuRadioGroupContext.ts:16-23`; `menu/checkbox-item/MenuCheckboxItemContext.ts:15-22`; `menu/radio-item/MenuRadioItemContext.ts:15-22`. Test: [L2] `MenuSubmenuTrigger.test.tsx` "throws when rendered outside Menu.SubmenuRoot", "throws when Menu.SubmenuRoot is rendered outside a menu"; `MenuItem.test.tsx` "throws when rendered outside Menu.Root"; `MenuPopup.test.tsx` "throws when rendered outside Menu.Positioner"; `MenuPositioner.test.tsx` "throws when rendered outside Menu.Portal"; `MenuGroupLabel.test.tsx` "throws when rendered outside Menu.Group or Menu.RadioGroup"; `MenuRadioItem.test.tsx` "throws when rendered outside Menu.RadioGroup"; `MenuCheckboxItemIndicator.test.tsx` "throws when rendered outside Menu.CheckboxItem"; `MenuRadioItemIndicator.test.tsx` "throws when rendered outside Menu.RadioItem".
- **MNU-EDGE-04** — MUST. `Menu.Input`, `Menu.Clear` and `Menu.Empty` rendered in a menu that is not wrapped in `Menu.FilterProvider` (including a plain submenu inside a filterable menu) throw `` Error(`Base UI: <Menu.${part}> must be placed in a menu wrapped in <Menu.FilterProvider>. It reads the filter query and the matching items from the provider, which a plain menu does not have. Wrap the <Menu.Root> or <Menu.SubmenuRoot> it belongs to in <Menu.FilterProvider>. See https://base-ui.com/react/components/menu#filtering`) `` where `part` is `Input`, `Clear` or `Empty`.
  Source: `menu/filter-root/MenuFilterContext.ts:85-96`. Test: [L2] `MenuFilterParts.test.tsx` "throws when <Menu.%s> is rendered without a filter provider"; `MenuFilterRoot.scoping.test.tsx` "throws when a filter part is rendered in a plain menu", "throws when a filter part is rendered in a plain submenu of a filterable menu".
- **MNU-EDGE-05** — MUST. In development, a filterable menu whose popup opens without a mounted `Menu.Input` (no focus owner registered) logs `warn("A filterable menu opened without a <Menu.Input>. Render the input inside <Menu.Popup>, or drop <Menu.FilterProvider> for a menu that does not filter.")` once per open; a filterable menu with an input and a detached trigger does not warn.
  Source: `menu/filter-root/useMenuFilterPopup.ts:38-48`. Test: [L2] `MenuFilterRoot.scoping.test.tsx` "warns when a filterable menu opens without an input", "does not warn for a filterable menu with an input and a detached trigger".
- **MNU-EDGE-06** — MUST. In development, a `Menu.SubmenuTrigger` whose rendered element is natively disabled (`disabled` attribute / `aria-disabled` on the `render` element) while the component's `disabled` prop is false logs `` warn(`A disabled element was detected on <Menu.SubmenuTrigger>. To properly disable the trigger, use the \`disabled\` prop on the component instead of setting it on the rendered element.${ownerStackMessage}`) `` after every commit, where `ownerStackMessage` is React's `captureOwnerStack()` output when available (empty string otherwise); the check does not run in production.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:134-145`. Test: [L2] `MenuSubmenuTrigger.test.tsx` "should warn when a disabled element is detected via render prop with JSX element", "warns without an owner stack when React cannot provide one", "does not inspect rendered disabled elements in production".
- **MNU-EDGE-07** — MUST. Handle misuse (PSH-API-08/09, messages verbatim with `componentName` = `Menu`): `handle.open()`/`handle.close()` while no Root using the handle is mounted logs `console.warn("Base UI: MenuHandle.open() was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.")` / `"Base UI: MenuHandle.close() was called while no root using this handle is mounted. The call was ignored."` and ignores the call; `handle.open(id)` with an unregistered trigger id throws (Menu sets `throwOnMissingTrigger`) `` Error(`Base UI: MenuHandle.open() was called with the trigger id "${triggerId}", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted Menu.Trigger that has this handle set on its "handle" prop.`) ``. A detached trigger declared after the Root, and a trigger still registered to a previous Root during a transient overlap (route transition), are resolved.
  Source: `utils/popups/popupHandle.ts:213-285`; `menu/store/MenuHandle.ts`. Test: [L2] detached-triggers suite "throws when opened with an unregistered trigger id", "ignores imperative calls while no root is attached and follows the root across remounts", "registers a detached trigger declared after the root", "resolves a trigger still registered to the previous root during a transient overlap". (L4 for the detached-triggers suite clause)
- **MNU-EDGE-08** — MUST. React 17 id fallback / SSR: when `React.useId` is unavailable, ids are generated after hydration; during SSR the partial ids are omitted (no `aria-controls`/`aria-labelledby` pointing at a not-yet-known id) and the relationships are wired after hydration, submenu triggers register once fallback ids resolve, and the empty state renders after hydration when no items exist. A list rendered with an explicitly empty `id` omits `aria-controls`; a trigger rendered with an explicitly empty `id` omits the list label; a popup/list never points at a trigger that did not render.
  Source: `menu/filter-root/MenuFilterDropdown.tsx`; `menu/popup/MenuPopup.tsx:72-77`; `internals/resolvePopupLabel.ts`. Test: [L4] `MenuFilterRoot.react17.test.tsx` "omits partial ids during SSR and wires relationships after hydration", "registers a submenu trigger after fallback ids resolve"; `MenuFilterRoot.test.tsx` "shows the empty state after hydration when no items are rendered", "does not point the popup or list at a trigger that never rendered", "omits aria-controls when the list renders with an explicitly empty id", "omits the list label when the trigger renders with an explicitly empty id", "points aria-controls at the id of a server-created render element".
- **MNU-EDGE-09** — MUST. Dynamic items: items may mount, unmount, change text or change ids while the menu is open. Navigation skips `null`/removed entries, the highlight is kept when its element survives (MNU-STA-20), items that mount a microtask after a keyboard open still receive the initial highlight, a submenu trigger whose `id` changes updates the registration and `aria-controls`, a `Menu.List` that changes its `id` while open updates the trigger's `aria-controls`, and a popup `id` override is released when the prop is removed. Item text that changes while hidden by the filter is re-matched.
  Source: `menu/root/MenuRoot.tsx:570-618`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:68-106`; `menu/list/MenuList.tsx:23-33`; `filter-dropdown/root/FilterDropdownRoot.tsx:109-125`. Test: [L4] `MenuRoot.test.tsx` "skips null items when navigating", "highlights the first item when the items render a microtask after a keyboard opening"; `MenuSubmenuTrigger.test.tsx` "follows a submenu trigger id change"; `MenuList.test.tsx` "updates the trigger when the open list changes its id"; `MenuFilterRoot.test.tsx` "releases a popup id override when the prop is removed", "re-filters an item whose rendered text changes while it is hidden", "keeps component-rendered item text after the item is filtered out"; `useMenuFilterItem.test.tsx` "item text resolution" (7 tests).
- **MNU-EDGE-10** — MUST. Shadow DOM: a submenu portaled into a shadow root still closes when focus leaves through its guard, and a filterable parent still moves its highlight when such a submenu closes on the parent's main-axis key; a `Menu.Portal container` inside a shadow root is supported.
  Source: `menu/portal/MenuPortal.tsx:50-54`; `floating-ui-react/utils/tabbable.ts:226-256`. Test: [L4] `MenuSubmenuTrigger.test.tsx` "closes a submenu when its focus guard is in a shadow-root portal"; `MenuFilterSubmenuRoot.test.tsx` "moves a filterable parent on when a submenu portaled into a shadow root closes".
- **MNU-EDGE-11** — Withdrawn: React-specific; moved to §14.
- **MNU-EDGE-12** — MUST. Stale timers and relays never reopen or re-close: a hover open timer scheduled before a sibling closed the submenu is discarded (`guardStaleOpen`); a relayed tree close arriving after the store already closed is dropped (MNU-EVT-01); a pending hover mouseup listener is removed when the trigger unmounts before mouseup; a hover close scheduled before a keyboard/click open does not fire; and a stale cross-axis key does not reopen a submenu after the highlight moved.
  Source: `menu/root/MenuRoot.tsx:348-352`; `menu/trigger/MenuTrigger.tsx:130-157,267-282`; `menu/positioner/MenuPositioner.tsx:187-216`. Test: [L4] `MenuTrigger.test.tsx` "removes the hover mouseup listener when unmounted before mouseup"; `MenuRoot.test.tsx` "cancels a pending submenu hover-open when the pointer leaves via mouseout", "focuses the trigger after Escape when the closing menu receives mouseleave"; `MenuFilterRoot.test.tsx` "does not reopen a stale submenu with a cross-axis key".
- **MNU-EDGE-13** — MUST. Nested popups opened from an item (Dialog, AlertDialog, Popover) are part of the floating tree: the menu stays open and does not steal focus while they are open (MNU-FOC-11), a press-drag-release activation that opens a dialog keeps the dialog open, Escape inside the dialog closes only the dialog, and a filterable submenu stays accessible (not `aria-hidden`) inside its parent. A `Menu.Trigger` inside a Popover with a non-native `render` element receives an explicit `role="button"`; a native button inside a Popover gets none.
  Source: `menu/popup/MenuPopup.tsx:208-216`; `menu/trigger/MenuTrigger.tsx:272-285`. Test: [L4] `MenuRoot.test.tsx` "keeps the menu and dialog open when pressing Shift+Tab inside a nested dialog", "keeps the dialog open after a press-drag-release activation"; popup-list suite "closes only itself on Escape inside a dialog"; `MenuFilterRoot.scoping.test.tsx` "leaves a filterable submenu accessible inside its parent", "announces a dialog from a detached trigger attached to a filterable root"; `MenuTrigger.test.tsx` "does not have role prop inside a Popover", "has a role prop inside a Popover when not a native button".
- **MNU-EDGE-14** — MUST. Invariants: at most one submenu per level is open at a time (MNU-DIS-05); a submenu is never open while its parent is closed (MNU-DIS-04); exactly one element in an open plain popup has `tabindex="0"` (the highlighted item) or, with no highlight, only the popup/list is focusable; `aria-expanded` on the active trigger always equals the store's `open`; `data-popup-open` and `handle.isOpen` remain `true` when a controlled close is vetoed; `activeIndex` is `null` or a valid index into the current item list; a filterable menu's query is `''` whenever the popup is closed unless the `popup-close` reset was canceled.
  Source: `menu/store/MenuStore.ts:76-149,224-251`; `menu/root/MenuRoot.tsx:341-445`. Test: [L2] `MenuTrigger.test.tsx` "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed"; `MenuRoot.test.tsx` "does not leave a tabbable item after tabbing out of a keepMounted menu before close"; `MenuFilterRoot.test.tsx` "keeps the highlight index in step with the highlighted item after the input is refocused", "starts navigation from the top again after closing and reopening". (L4 for the tree-close invariants)
- **MNU-EDGE-15** — MUST. Some context-menu branches test only for the presence of the ContextMenu shared state, not for the menu's parent kind, so a plain `Menu.Root` rendered anywhere inside a `ContextMenu.Root` — in the Trigger or in the context menu's popup, where its parent kind is none (MNU-STR-19) — inherits them: its Positioner always uses `positionMethod: 'fixed'`, ignoring the prop (CTX-API-12); its items apply the context-menu release rules (CTX-PTR-09 … CTX-PTR-12), so a `mouseup` activates an item only as a right-button release on macOS and never on other platforms, which disables primary-button press-drag-release activation (MNU-PTR-03); and its `Menu.Backdrop` is registered as the context menu's backdrop. The parent-kind-based behaviors (pointer anchoring, modal focus, outside-press grace, no arrow-key opening) are not inherited.
  Source: `menu/positioner/MenuPositioner.tsx:59,109`; `menu/item/useMenuItemCommonProps.ts:59,73,108-130`; `menu/backdrop/MenuBackdrop.tsx:29-38`; `menu/root/MenuRoot.tsx:108-117`. Test: none (CTX-EDGE-05; context-menu.md §16 Q9).
- **MNU-EDGE-16** — MUST. `Menu.Popup` and the filterable `Menu.List` resolve the id they publish to their owner — for the trigger's `aria-controls` (MNU-ARIA-03) and the other owner-computed IDREFs to the popup/list — per RND-DOM-32 and RND-DOM-33: an element-form substitute's own `id` key wins (`undefined` there means "no id"), else the `id` prop, else the generated id. A callback-form element substitution is opaque: the owner keeps referencing the id handed to the callback, so a callback that does not apply that `id` to the element it returns leaves those IDREFs dangling. Ports MUST hand the resolved id to the callback and MUST NOT try to infer the rendered id from the callback's output. (The plain `Menu.List` is not affected: the trigger reads `aria-controls` from the mounted list element's `id`.)
  Source: `internals/resolveRenderedId.ts:10-28`; `menu/popup/MenuPopup.tsx:72-76`; `filter-dropdown/list/FilterDropdownList.tsx:18-27`; `menu/store/MenuStore.ts:100-103`; see RND-DOM-32, RND-DOM-33. Test: [L2] `MenuFilterRoot.test.tsx` "points aria-controls at the id of a server-created render element" (element form); callback form: none.



## 13. Behaviors evidenced by tests

Coverage of each of the 38 test files by the requirements above (titles are the `it` titles; `it.each` titles keep their placeholders; rows with identical mappings are joined with `/`), plus the requirements that exist only because a test asserts them (MNU-A11Y-01..03).

### `arrow/MenuArrow.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Arrow />`, `refInstanceof: HTMLDivElement`) | MNU-STR-05, MNU-DOM-11, MNU-POS-07, RND-DOM-36..49 |

### `backdrop/MenuBackdrop.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Backdrop />`, `refInstanceof: HTMLDivElement`, inside `Menu.Root open`) | MNU-STR-07, MNU-DOM-10, RND-DOM-36..49 |
| "does not start an entry phase when initially open (controlled=%s)" | MNU-DOM-10, MNU-ANI-11 |
| "sets `pointer-events: none` style on backdrop if opened by hover" | MNU-DOM-10, MNU-PTR-05, MNU-DIS-09, MNU-ANI-11 |
| "does not set `pointer-events: none` style on backdrop if opened by click" | MNU-DOM-10, MNU-DIS-09, MNU-ANI-11 |

### `checkbox-item-indicator/MenuCheckboxItemIndicator.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.CheckboxItemIndicator keepMounted />`, `refInstanceof: HTMLSpanElement`) | MNU-STR-10, MNU-API-43, RND-DOM-36..49 |
| "throws when rendered outside Menu.CheckboxItem" | MNU-STR-09, MNU-EDGE-03 |
| "should remove the indicator when there is no exit animation defined" / "should remove the indicator when the animation finishes" | MNU-ANI-13 |
| "keeps the indicator mounted to play its exit animation when unchecked without keepMounted" | MNU-DOM-17, MNU-ANI-13 |

### `checkbox-item/MenuCheckboxItem.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.CheckboxItem />`, `refInstanceof: HTMLDivElement`, inside `Menu.Root open`) | MNU-STR-08, RND-DOM-36..49 |
| "perf: does not rerender menu items unnecessarily" | §14 (MNU-EDGE-11 withdrawn) |
| "adds the state and ARIA attributes when checked=$checked" | MNU-API-41, MNU-DOM-16, MNU-ARIA-08 |
| "toggles the checked state when clicked" | MNU-API-41, MNU-STA-14, MNU-EVT-08 |
| "toggles the checked state when Space is pressed" | MNU-KEY-08 |
| "toggles with Space after closing during typeahead and reopening" / "does not toggle when Space is pressed during an active typeahead session" | MNU-KEY-13 |
| "toggles the checked state when Enter is pressed" | MNU-KEY-08 |
| "calls `onCheckedChange` when the item is clicked" / "does not toggle when `onCheckedChange` cancels the event" | MNU-API-42, MNU-EVT-08 |
| "keeps the state when closed and reopened" | MNU-API-41, MNU-EVT-08 |
| "when `closeOnClick=true`, closes the menu when the item is clicked" / "does not close the menu when the item is clicked by default" / "can be focused but not interacted with when disabled" | MNU-PTR-15 |

### `filter-root/MenuFilterGroup.test.tsx`

| Test | Requirements |
|---|---|
| "hides a radio group, label included, when the query filters out all of its items" | MNU-DOM-20 |
| "hides a group whose kept-mounted filterable submenu has its own groups" | MNU-STA-24, MNU-DOM-20 |

### `filter-root/MenuFilterParts.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance, inside a filterable popup, for `<Menu.Trigger />` (`HTMLButtonElement`, `button: true`), `<Menu.Input />` (`HTMLInputElement`), `<Menu.Clear />` (`HTMLButtonElement`, `button: true`, `defaultValue: 'query'`), `<Menu.Empty />`, `<Menu.List />`, `<Menu.Item />`, `<Menu.LinkItem href="#" />`, `<Menu.CheckboxItem />`, `<Menu.RadioItem value="value" />`, `<Menu.Group />`, `<Menu.Arrow />`, `<Menu.Backdrop />`, `<Menu.Separator />`, `<Menu.SubmenuTrigger />` | MNU-STR-14, MNU-STR-15, MNU-DOM-21, MNU-DOM-22, MNU-DOM-23, MNU-DOM-24, MNU-DOM-27, RND-DOM-36..49 |
| "excludes Clear from the tab order and accessibility tree" | MNU-DOM-22, MNU-ARIA-16, MNU-PTR-21 |
| "throws when Trigger is rendered without a root or handle" | MNU-EDGE-01 |
| "throws when <Menu.%s> is rendered without a filter provider" | MNU-STR-15, MNU-EDGE-04 |

### `filter-root/MenuFilterPopup.test.tsx`

| Test | Requirements |
|---|---|
| "returns focus to the input when the list takes it from the input" / "leaves focus on the list when the input did not have it" | MNU-KEY-27, MNU-FOC-14 |
| "returns focus with the %s %s close key" | MNU-KEY-33, MNU-I18N-01, MNU-I18N-02 |

### `filter-root/MenuFilterRoot.initialHighlight.test.tsx`

| Test | Requirements |
|---|---|
| "highlights the first enabled action and keeps the input in the arrow loop" | MNU-STA-22, MNU-KEY-02, MNU-KEY-23 |
| "clears the initial highlight when typing and permits an empty result" | MNU-STA-19, MNU-STA-23 |
| "handles opening with %s" | MNU-PTR-19 |
| "focuses the input when a screen reader press opens the menu" | MNU-PTR-16, MNU-PTR-19 |
| "focuses a submenu input when a screen reader press opens the submenu" | MNU-PTR-16, MNU-FOC-15, MNU-A11Y-03 |
| "does not focus the input when an item is tapped after a touch open" | MNU-PTR-19 |
| "does not focus the input when opened on hover" | MNU-PTR-05, MNU-PTR-19, MNU-FOC-02 |
| "highlights the first action again when reopening" | MNU-STA-19, MNU-EVT-18 |
| "does not highlight an item when a controlled open follows a keyboard open" | MNU-STA-03 |

### `filter-root/MenuFilterRoot.react17.test.tsx`

| Test | Requirements |
|---|---|
| "omits partial ids during SSR and wires relationships after hydration" | MNU-DOM-21, MNU-ARIA-03, MNU-EDGE-08 |
| "registers a submenu trigger after fallback ids resolve" | MNU-EDGE-08 |

### `filter-root/MenuFilterRoot.scoping.test.tsx`

| Test | Requirements |
|---|---|
| "makes the menu filterable" | MNU-STR-14, MNU-DOM-21, MNU-ARIA-01, MNU-ARIA-06 |
| "filters items by text and shows the empty state" | MNU-STA-15, MNU-STA-23, MNU-DOM-23, MNU-ARIA-16 |
| "navigates the list from the input with aria-activedescendant" | MNU-ARIA-14, MNU-KEY-25 |
| "supports a controlled query" | MNU-API-57, MNU-EVT-16 |
| "leaves a plain menu untouched" | MNU-ARIA-01 |
| "throws when a filter part is rendered in a plain menu" / "throws when a filter part is rendered in a plain submenu of a filterable menu" | MNU-STR-15, MNU-EDGE-04 |
| "does not extend to a plain submenu inside the filterable menu" | MNU-STR-14 |
| "filters a submenu that uses FilterSubmenuRoot" | MNU-STR-14, MNU-ARIA-13 |
| "filters a submenu of a plain menu" | MNU-STR-14 |
| "announces a dialog from a detached trigger attached to a filterable root" | MNU-ARIA-01, MNU-EDGE-13 |
| "enters and leaves a plain submenu with the cross-axis keys from the input" / "highlights the trigger as soon as %s closes the submenu" | MNU-KEY-29 |
| "keeps focus off the input while an item press closes the whole menu" | MNU-DIS-06 |
| "returns focus to the input as soon as the pointer closes the submenu" | MNU-PTR-22, MNU-FOC-15 |
| "moves ArrowDown from the trigger into the input and highlights the first item" / "moves a typed character from the trigger into the input" / "keeps focus on the trigger for modified keys and cross-axis arrows" | MNU-KEY-28 |
| "lets a consumer onKeyDown cancel the relay" | MNU-EVT-14 |
| "warns when a filterable menu opens without an input" / "does not warn for a filterable menu with an input and a detached trigger" | MNU-EDGE-05 |
| "reports keyboard highlights while the input keeps focus" | MNU-EVT-10 |
| "reports the auto-highlighted item as a programmatic change" | MNU-EVT-11 |
| "does not report the item that takes the highlighted index when an item is inserted" / "keeps the highlight when an item the query filters out is appended" / "keeps the highlight when an item is appended after reopening from a filtered session" | MNU-STA-20, MNU-EVT-13 |
| "returns to its own trigger on Shift+Tab" | MNU-KEY-11, MNU-FOC-07 |
| "does not trap a hover-opened popup" | MNU-FOC-10 |
| "does not trap a popup opened by an assistive technology click" | MNU-PTR-19 |
| "lets Tab leave a modal filterable popup and closes it" | MNU-KEY-10, MNU-FOC-10, MNU-FOC-13 |
| "skips Clear when tabbing out of the popup" | MNU-DOM-22, MNU-KEY-10 |
| "closes the popup with Escape and returns focus to the trigger" | MNU-KEY-26 |
| "lets Tab leave the popup with modal={false}" | MNU-KEY-10, MNU-FOC-10 |
| "does not trap a menu opened by touch" | MNU-PTR-19 |
| "leaves a filterable submenu accessible inside its parent" | MNU-EDGE-13 |

### `filter-root/MenuFilterRoot.test.tsx`

| Test | Requirements |
|---|---|
| "keeps the matching items mounted" | MNU-STA-16 |
| "ignores %s in the input while the popup animates out" | MNU-ANI-09 |
| "ignores text typed in the input while the popup animates out with $name" | MNU-STA-21, MNU-ANI-09 |
| "highlights the first item while keeping input focus when opened with the keyboard" | MNU-STA-22, MNU-FOC-01 |
| "marks the input focus-visible when the menu is opened with a pointer" | MNU-STA-22, MNU-PTR-19 |
| "automatically highlights the first match after the user types" | MNU-STA-19 |
| "updates the active descendant when filtering replaces a custom-id item" | MNU-STA-18, MNU-FOC-12 |
| "always highlights the first item when autoHighlight is "always"" | MNU-STA-19 |
| "keeps a highlight when typing does not change the trimmed query with autoHighlight=%s" | MNU-STA-15 |
| "does not allow arrow navigation to escape when autoHighlight is %s" | MNU-KEY-23 |
| "always highlights the first item when mounted inside an opened dialog" / "clears the automatic highlight when a typed query is cleared" | MNU-STA-19 |
| "clears a navigated highlight when Clear is pressed" | MNU-PTR-21 |
| "restores the first item when an always-highlighted query is cleared" | MNU-STA-19, MNU-PTR-21 |
| "clears the highlight when no items match" | MNU-STA-18 |
| "continues keyboard navigation from the automatic highlight and activates it" | MNU-STA-19, MNU-KEY-23, MNU-KEY-25 |
| "preserves navigation with an inline filter and a highlighted-item preview" | MNU-STA-19, MNU-KEY-23 |
| "preserves a pointer highlight when autoHighlight is "always"" | MNU-PTR-13 |
| "keeps the highlight when an item's text changes with autoHighlight="always"" | MNU-STA-18 |
| "keeps the automatic highlight when typing moves it out from under the pointer" | MNU-KEY-22, MNU-PTR-14 |
| "supports a controlled query" | MNU-API-57, MNU-EVT-16 |
| "supports persistent always highlighting inside a filterable submenu" | MNU-STR-14, MNU-STA-19 |
| "does not highlight the first item while closing from the trigger with a pointer" | MNU-ANI-09 |
| "moves focus past the menu when tabbing from the input" | MNU-KEY-10, MNU-DIS-08 |
| "uses the configured locale for default matching" | MNU-API-54, MNU-API-56, MNU-I18N-03 |
| "keeps Home and End as caret keys until an item is highlighted" / "keeps Home and End as caret keys while the input is empty" / "keeps the caret keys while a query is highlighted automatically" / "keeps the caret keys after navigating to an item while the query is not empty" | MNU-KEY-23 |
| "uses Home and End for list navigation after an item is highlighted" | MNU-KEY-05, MNU-KEY-23 |
| "hides a group, label included, when the query filters out all of its items" | MNU-STA-24 |
| "closes a filterable submenu and moves focus to the next element when tabbing forward" / "closes a pointer-opened filterable submenu and moves focus forward when tabbing" | MNU-FOC-08 |
| "focuses a click-opened filterable submenu input so typing filters the submenu" | MNU-PTR-19, MNU-FOC-15 |
| "uses dialog semantics for a filterable submenu" | MNU-ARIA-06, MNU-ARIA-13 |
| "lets the ordinary parent menu own its main-axis navigation keys" | MNU-KEY-18, MNU-KEY-32 |
| "keeps disabled parent items reachable during main-axis navigation" / "wraps parent navigation when loopFocus is enabled" | MNU-KEY-32 |
| "stops parent navigation at the boundary when loopFocus is disabled" | MNU-API-06 |
| "resets the input value once when the popup closes" | MNU-STA-21, MNU-EVT-18 |
| "keeps submenu filtering updated during the exit transition" | MNU-STA-21, MNU-ANI-09 |
| "leaves the uncontrolled query and visible items unchanged when a change is canceled" | MNU-API-58, MNU-EVT-16 |
| "clears a positional highlight when a controlled query replaces the visible item" | MNU-STA-18, MNU-FOC-12 |
| "supports a detached trigger in a filterable menu" | MNU-API-14, MNU-FOC-17 |
| "uses the id from a custom trigger render element for popup labelling" | MNU-API-18, MNU-ARIA-19 |
| "uses an explicit popup label instead of the trigger label" / "uses an explicit label from the popup render element" | MNU-ARIA-19 |
| "releases a popup id override when the prop is removed" | MNU-API-32, MNU-EDGE-09 |
| "focuses the input when a filterable submenu opens with the keyboard" | MNU-STA-22, MNU-FOC-01, MNU-FOC-15 |
| "opens a virtually focused submenu with the keyboard" | MNU-KEY-29 |
| "keeps cross-axis keys as caret keys while the submenu query is not empty (autoHighlight: %s)" | MNU-KEY-30 |
| "uses RTL cross-axis keys to enter and leave a filterable submenu" | MNU-KEY-29, MNU-I18N-01 |
| "returns virtual focus to the parent input on Escape and resumes filtering" | MNU-KEY-29 |
| "does not reclaim focus when a controlled submenu closes after focus moves outside" | MNU-FOC-14, MNU-EVT-07 |
| "opens a submenu from a filterable submenu input" / "opens a virtually focused submenu with Enter from the input" | MNU-KEY-29 |
| "does not reopen a stale submenu with a cross-axis key" | MNU-KEY-29, MNU-EDGE-12 |
| "moves focus into the submenu input once the pointer enters it" / "closes a hover-opened submenu from a virtually focused parent" / "focuses the first item when entering a hover-opened submenu from a filterable menu" | MNU-FOC-15 |
| "filters items and selects the active item while focus remains on the input" | MNU-ARIA-14, MNU-PTR-20 |
| "does not activate the highlighted item when Enter commits an IME composition" | MNU-KEY-25, MNU-KEY-31, MNU-EVT-19 |
| "does not forward submenu navigation keys while composing text" | MNU-KEY-31, MNU-EVT-19 |
| "disables filter controls when the root is disabled" | MNU-API-10, MNU-API-59, MNU-API-60, MNU-PTR-21, MNU-DIS-12 |
| "uses an updated custom filter function" | MNU-API-54 |
| "filters a non-filterable submenu trigger from a filterable parent" | MNU-STA-25 |
| "keeps a submenu trigger visible when the query matches its label" | MNU-API-36, MNU-STA-17 |
| "matches a submenu trigger on its label" | MNU-API-36 |
| "filters a Menu.SubmenuTrigger used inside a plain submenu root" | MNU-STA-25 |
| "keeps plain menu items and submenu triggers out of the tab order in a filterable list" | MNU-DOM-14, MNU-FOC-03 |
| "retains the parent highlight when a submenu opens from a pointer" | MNU-PTR-17 |
| "closes the submenu when the query filters out its trigger" / "keeps an open submenu trigger mounted when its filter close is canceled" | MNU-STA-25, MNU-DIS-13 |
| "filters each menu item variant without changing its role" | MNU-DOM-28 |
| "preserves uncontrolled checkbox state while the item is filtered out" | MNU-API-41 |
| "does not rerender unaffected items when the highlight moves" | §14 (MNU-EDGE-11 withdrawn) |
| "walks the list with the arrow keys and releases the highlight at the end" | MNU-KEY-23 |
| "starts navigation from the top again after closing and reopening" / "keeps the highlight index in step with the highlighted item after the input is refocused" | MNU-EDGE-14 |
| "resets the highlight when the query changes and rebuilds it from the first match" | MNU-STA-18 |
| "does not move the highlight while typing into the input" | MNU-KEY-14 |
| "keeps component-rendered item text after the item is filtered out" | MNU-STA-17, MNU-EDGE-09 |
| "preserves input focus on item presses" | MNU-PTR-20 |
| "returns focus to the input with the RTL submenu close key" | MNU-KEY-33, MNU-I18N-01 |
| "releases the highlight when the query is cleared" | MNU-STA-19 |
| "keeps the query when the popup-close reset is canceled" | MNU-API-58, MNU-STA-21, MNU-EVT-18 |
| "renders Empty when no items are registered at all" | MNU-API-61, MNU-STA-23 |
| "keeps a group with no registered items visible while filtering" | MNU-STA-24 |
| "re-filters an item whose rendered text changes while it is hidden" | MNU-STA-17, MNU-EDGE-09 |
| "preserves modifiers when the input activates a link item" | MNU-KEY-25 |
| "leaves modified editing keys to the input while an item is highlighted" | MNU-KEY-22, MNU-KEY-30 |
| "does not open a highlighted submenu with a modified cross-axis key from the input" | MNU-KEY-30 |
| "calls onOpenChangeComplete when the menu opens and closes" | MNU-API-04, MNU-EVT-06, MNU-ANI-06 |
| "does not seed a highlight when the trigger regains focus while open" | MNU-FOC-16 |
| "renders the trigger as often as a plain menu on mount" | §14 (MNU-EDGE-11 withdrawn) |
| "uses the id passed to Menu.Input" | MNU-API-59 |
| "prefers the label prop over rendered text for matching" | MNU-API-36 |
| "removes Empty once the query matches again" | MNU-API-61 |
| "shows the empty state after hydration when no items are rendered" | MNU-API-61, MNU-EDGE-08 |
| "does not set aria-selected on highlighted items outside WebKit" | MNU-ARIA-15 |
| "closes when the pointer sweeps off the trigger" / "closes when the pointer leaves the submenu popup" | MNU-PTR-10 |
| "closes and resets after a query was typed into the hover-opened submenu" | MNU-PTR-10, MNU-EVT-18 |
| "applies a custom filter to item text" / "keeps every item visible when filtering is turned off with null" | MNU-API-54 |
| "stays closed when onOpenChange is canceled" | MNU-STA-01, MNU-EVT-02 |
| "leaves a checkbox item unchecked when onCheckedChange is canceled" | MNU-API-42, MNU-EVT-08 |
| "tracks a controlled checkbox item" | MNU-API-41, MNU-EVT-08 |
| "never mounts the Empty children while items match" | MNU-API-61 |
| "filters on the committed text, not the composition in progress" | MNU-KEY-31, MNU-EVT-19 |
| "reports input-change while typing" / "reports input-clear when the field is emptied" | MNU-EVT-16 |
| "reports clear-press when the clear button is used" | MNU-API-60, MNU-PTR-21, MNU-EVT-17 |
| "reports popup-close when a controlled close discards the query" | MNU-EVT-18 |
| "does not point the popup or list at a trigger that never rendered" | MNU-EDGE-08 |
| "labels the list with the trigger when no label is given" / "keeps a list label supplied through a render element" | MNU-ARIA-04, MNU-ARIA-19 |
| "omits aria-controls when the list renders with an explicitly empty id" / "points aria-controls at the id of a server-created render element" | MNU-API-62, MNU-EDGE-08 |
| "omits the list label when the trigger renders with an explicitly empty id" | MNU-EDGE-08 |
| "opens from a trigger rendered outside the root" | MNU-API-14, MNU-API-63 |
| "does not highlight an item when another detached trigger receives focus" | MNU-FOC-16 |
| "runs a consumer handler once per interaction on each composed part" | MNU-API-59, MNU-EVT-14, MNU-EVT-15 |
| "lets a consumer key handler on the input opt out of list navigation and activation" | MNU-API-59, MNU-EVT-15 |
| "leaves focus on another control in the popup when the pointer moves" / "returns focus to the parent input when the pointer moves back over the parent popup" / "returns focus to the parent input when a mouse enters the trigger after a tap" / "restores the parent input highlight when the pointer returns from a plain submenu" | MNU-PTR-22 |
| "returns focus to the parent input once a pointer-opened submenu unmounts" | MNU-FOC-15 |
| "opens a hovered submenu without moving focus until the pointer enters it" | MNU-PTR-09 |
| "moves focus back to the parent input while the pointer moves between submenu triggers" | MNU-PTR-22 |
| "closes on Escape even with a query typed" | MNU-KEY-26, MNU-DIS-02 |
| "closes and returns focus to the trigger on Shift+Tab from the input" | MNU-KEY-26 |
| "keeps input focus when a Shift+Tab close is canceled" | MNU-KEY-26, MNU-EVT-02 |
| "resets the query and highlight across a close and reopen" | MNU-STA-21, MNU-EVT-18 |
| "keeps real focus on the input while the cursor moves through the list" | MNU-API-33, MNU-PTR-14 |
| "clears a pointer highlight on pointer leave by default" | MNU-API-55, MNU-PTR-13 |
| "passes the keydown event to onItemHighlighted in a %s menu" | MNU-EVT-10 |
| "passes the keydown events when entering and leaving a filterable submenu" | MNU-KEY-29, MNU-EVT-12 |
| "reports no keyboard reason when Enter opens a filterable submenu" | MNU-KEY-29, MNU-EVT-11 |
| "restores input focus after opening with %s and closing with %s" | MNU-KEY-29, MNU-FOC-15 |
| "does not repeat matching when navigating past hidden component labels" | MNU-STA-17, MNU-KEY-23 |

### `filter-root/MenuFilterRoot.webkit.test.tsx`

| Test | Requirements |
|---|---|
| "marks only the highlighted item while its input is focused in WebKit" / "keeps selection state for an auto-focused input in Strict Mode" / "keeps selection state when a focused input is replaced" / "keeps selection in sync when a focused input is replaced without autofocus" | MNU-ARIA-15, MNU-A11Y-02 |
| "clears selection when the focused input unmounts while the menu stays open" | MNU-ARIA-15, MNU-FOC-14, MNU-A11Y-02 |
| "does not restore selection after a focused input unmounts and the menu reopens on hover" | MNU-ARIA-15, MNU-A11Y-02 |
| "preserves checked state on checkbox and radio items in WebKit" | MNU-ARIA-08, MNU-ARIA-15, MNU-A11Y-02 |
| "marks highlighted links and submenu triggers while their input is focused in WebKit" / "marks items inside an opened submenu while its input is focused" / "leaves plain menu items alone, which navigate with real focus" | MNU-ARIA-15, MNU-A11Y-02 |

- **MNU-A11Y-02** — MUST. The WebKit `aria-selected` marker (MNU-ARIA-15) is derived from the store selector `webkitAriaSelected(highlighted)` = `platform.engine.webkit && inputFocused && highlighted`, where `inputFocused` is written by `Menu.Input` from its own focus/blur events and from a mount-time check of `document.activeElement`. Consequently: an `autoFocus` input marks the highlighted item (also under StrictMode double effects); when the input element is replaced, the marker follows the replacement only if the replacement actually holds focus; it is removed when the focused input unmounts while the menu stays open and is not restored when the menu reopens on hover with an unfocused input; it coexists with `aria-checked` on checkbox/radio items; it is applied to LinkItems and SubmenuTriggers of the input's own list and to items of an open filterable submenu judged against that submenu's input; plain menus, which navigate with real focus, never render it.
  Source: `menu/store/MenuStore.ts:39,107-111,313`; `menu/input/MenuInput.tsx:32-55`; `menu/item/useMenuItemCommonProps.ts:68,82`. Test: [L4] `MenuFilterRoot.webkit.test.tsx` (all 10 tests).

### `filter-root/useMenuFilterItem.test.tsx`

| Test | Requirements |
|---|---|
| "keeps focus on the input when items are pressed" | MNU-PTR-20 |
| "matches text that a descendant rendered after the item registered" / "stops matching text that a descendant no longer renders" / "matches an item whose children are an array while it is filtered out" / "keeps the rendered text of a hidden item whose children render different text" / "matches the new children of an item that changed while it was filtered out" / "matches the updated text of an item rendered through its render element" / "matches a render element whose text changed while the item was filtered out" | MNU-STA-17, MNU-EDGE-09 |
| "keeps a disabled item reachable with the arrow keys" | MNU-KEY-07, MNU-KEY-32 |
| "does not activate a highlighted disabled item with Enter" | MNU-KEY-25 |

### `filter-root/useMenuFilterKeyDown.test.ts`

| Test | Requirements |
|---|---|

### `filter-root/useMenuFilterSubmenuTrigger.test.tsx`

| Test | Requirements |
|---|---|
| "keeps real focus on the parent input until the submenu opens" | MNU-DOM-18, MNU-FOC-15 |
| "reports a menu popup on a trigger whose submenu has no filter" | MNU-ARIA-13 |
| "lets a consumer override aria-haspopup" / "lets a consumer override aria-haspopup on the root trigger" | MNU-ARIA-01 |

### `filter-submenu-root/MenuFilterSubmenuRoot.pointerFocus.test.tsx`

| Test | Requirements |
|---|---|
| "handles a %s press without hover opening" / "does not focus the input after a touch press with hover opening enabled" / "does not focus a hover-opened input on a %s press" / "lets keyboard activation enter a hover-opened submenu" | MNU-FOC-15 |

### `filter-submenu-root/MenuFilterSubmenuRoot.test.tsx`

| Test | Requirements |
|---|---|
| "renders the controlled query and reports changes" | MNU-API-57, MNU-EVT-16 |
| "keeps the uncontrolled query when the change is canceled" | MNU-EVT-16 |
| "closes with Escape and returns focus to the highlighted trigger" | MNU-KEY-33 |
| "opens with the cross-axis key and focuses the submenu input" | MNU-FOC-15 |
| "closes with the cross-axis close key and returns focus to the trigger" | MNU-KEY-33 |
| "hands the cursor back to the submenu input when the open key repeats" | MNU-KEY-15, MNU-KEY-29 |
| "keeps the automatic highlight when the keyboard enters a hover-opened submenu" | MNU-FOC-15 |
| "moves along the parent list with the main-axis keys" | MNU-KEY-18 |
| "closes when the close key arrives while the trigger holds focus" | MNU-KEY-29, MNU-KEY-33 |
| "keeps focus in the submenu input when the pointer crosses the trigger again" | MNU-PTR-22 |
| "leaves a key that is neither an axis nor an activation key to the parent" | MNU-KEY-18 |
| "prevents native scrolling when moving along the parent menu" | MNU-KEY-32 |
| "ignores a close key that composes text in the submenu input" | MNU-KEY-31 |
| "moves a horizontal parent on when a vertical submenu closes on its main-axis key (filterable parent: $parentFilterable, filterable submenu: $submenuFilterable)" | MNU-KEY-16, MNU-KEY-32 |
| "does not wrap a filterable parent when closing from its first item (filterable submenu: %s)" | MNU-KEY-32 |
| "moves a filterable parent on when a submenu portaled into a shadow root closes" | MNU-API-25, MNU-KEY-32, MNU-EDGE-10 |

### `group-label/MenuGroupLabel.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.GroupLabel />`, `refInstanceof: HTMLDivElement`, inside `Menu.Group`) | MNU-STR-10, RND-DOM-36..49 |
| "throws when rendered outside Menu.Group or Menu.RadioGroup" | MNU-STR-09, MNU-EDGE-03 |
| "is hidden from the accessibility tree by default" / "allows overriding aria-hidden" | MNU-ARIA-11 |
| "should reference the generated id in Group's `aria-labelledby`" | MNU-API-48 |
| "should reference the provided id in Group's `aria-labelledby`" | MNU-API-48, MNU-DOM-19 |
| "should reference the generated id in RadioGroup's `aria-labelledby`" / "should reference the provided id in RadioGroup's `aria-labelledby`" / "should support GroupLabel when RadioGroup is rendered as Group" | MNU-API-48, MNU-ARIA-11 |
| "does not let an older label cleanup clear a newer label" | MNU-API-48, MNU-EDGE-09 |

### `group/MenuGroup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Group />`, `refInstanceof: HTMLDivElement`) | MNU-STR-10, RND-DOM-36..49 |
| "renders a div with the `group` role" | MNU-ARIA-10 |

### `item/MenuItem.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Item />`, `refInstanceof: HTMLDivElement`, `button: true`, inside `Menu.Root open`) | MNU-STR-08, RND-DOM-36..49, UBT, conformance |
| "throws when rendered outside Menu.Root" | MNU-STR-07, MNU-EDGE-03 |
| "calls the onClick handler when clicked" / "does not close the menu when onClick prevents Base UI handler" | MNU-API-39, MNU-PTR-15, MNU-EVT-15 |
| "allows onMouseDown to call preventBaseUIHandler" | MNU-API-39, MNU-EVT-15 |
| "perf: does not rerender menu items unnecessarily" | §14 (MNU-EDGE-11 withdrawn) |
| "closes the menu when the item is clicked by default" | MNU-PTR-15, MNU-DIS-06 |
| "when `closeOnClick=false` does not close the menu when the item is clicked" | MNU-PTR-15 |
| "can be focused but not interacted with when disabled" | MNU-API-35, MNU-PTR-15 |
| "skips a natively disabled item during keyboard navigation" | MNU-KEY-07 |

### `link-item/MenuLinkItem.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.LinkItem />`, `refInstanceof: HTMLAnchorElement`, inside `Menu.Root open`) | MNU-STR-08, MNU-API-40, RND-DOM-36..49 |
| "react-router <Link> activates with Enter and Space" | MNU-API-40, MNU-PTR-15 |
| "does not navigate when Space is pressed during an active typeahead session" | MNU-API-40, MNU-KEY-13 |

### `list/MenuList.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.List />`, `refInstanceof: HTMLDivElement`, inside a filterable popup) | MNU-STR-13, RND-DOM-36..49 |
| "replays keys from an item that holds real focus and activates on Enter" | MNU-KEY-27 |
| "clears the highlight when focus returns to the input from an item" / "keeps the highlight when focus returns to the input with autoHighlight="always"" | MNU-FOC-14 |
| "keeps virtual focus on the input without making the list tabbable" | MNU-DOM-21, MNU-DOM-24, MNU-FOC-03 |
| "replays navigation keys on the input instead of scrolling" / "commits the highlighted item when Enter lands on the list" | MNU-KEY-27 |
| "still closes the menu with Escape" | MNU-KEY-27, MNU-DIS-02 |
| "hands typing keys back to the input" | MNU-KEY-27 |
| "reads an item id when navigating to it" | MNU-API-38, MNU-ARIA-14 |
| "exposes horizontal navigation on the menu instead of the dialog" | MNU-ARIA-06 |
| "enters the results with the vertical arrows after typing" / "enters the results from the end with ArrowUp" / "highlights the first item when opened from the trigger with ArrowDown" | MNU-KEY-24 |
| "takes the menu role and label from the popup" | MNU-STR-13, MNU-ARIA-03, MNU-ARIA-05 |
| "updates the trigger when the open list changes its id" | MNU-API-62, MNU-ARIA-03, MNU-EDGE-09 |
| "renders the orientation on the list" | MNU-ARIA-05 |
| "navigates the items with the keyboard" | MNU-STR-13, MNU-KEY-04 |
| "focuses the list rather than the presentational popup on a pointer open" | MNU-DOM-24, MNU-FOC-02 |
| "does not point an inactive trigger at the list" | MNU-ARIA-03 |
| "does not re-render an inactive trigger when the list mounts" | §14 (MNU-EDGE-11 withdrawn) |
| "enters the list from the end with ArrowUp after a pointer open without looping" | MNU-API-06, MNU-KEY-06 |

### `popup/MenuPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Popup />`, `refInstanceof: HTMLDivElement`) | MNU-STR-06, RND-DOM-36..49 |
| popupFocusPropsTests (`initialFocus: false`) | MNU-API-31, MNU-FOC-01, MNU-FOC-04, MNU-FOC-09 |
| "throws when rendered outside Menu.Positioner" | MNU-STR-05, MNU-EDGE-03 |
| "uses an aria-label instead of the trigger label" / "uses aria-labelledby from a render element" | MNU-ARIA-04, MNU-ARIA-19 |
| "stops toolbar navigation keys without blocking ordinary key events" | MNU-KEY-20 |
| "enters the items at the list boundary when an arrow key leaves custom popup content" / "moves focus into the items when the boundary item is already highlighted" / "moves focus into the items when the last item is already highlighted" | MNU-KEY-06 |
| "receives the interaction type of the item press that closed the menu" | MNU-FOC-09, MNU-EVT-05 |
| "receives the mouse interaction type after a drag-release item selection" | MNU-PTR-03, MNU-FOC-09, MNU-EVT-05 |

### `popup/MenuPopupEnterTransition.test.tsx`

| Test | Requirements |
|---|---|
| "plays the enter transition for a submenu that is open when its parent opens" / "plays the enter transition for a controlled-open submenu when its parent opens" / "does not play the enter transition for a menu that is open on the first render" / "does not play the enter transition for a submenu inside a menu that is open on the first render" | MNU-ANI-03 |
| "does not play the enter transition for a submenu when the parent popup is kept mounted" | MNU-ANI-03, MNU-ANI-05 |
| "plays the enter transition for a submenu opened by the user" | MNU-ANI-03, MNU-ANI-12 |
| "plays the enter transition for a submenu whose parent opened instantly" | MNU-ANI-04 |
| "marks an initially open submenu as instant when its parent opened instantly" | MNU-STA-04, MNU-KEY-03, MNU-ANI-04, MNU-ANI-07 |
| "clears the inherited instant type after the initial open" / "clears the inherited instant type when a controlled close interrupts the entry" / "clears the inherited instant type when the popup is not initially rendered" / "does not mark a closed submenu as instant when it is later opened programmatically" | MNU-ANI-04 |

### `popup/MenuPopupTransitionState.test.tsx`

| Test | Requirements |
|---|---|
| "starts each opening once (controlled=$controlled, keepMounted=$keepMounted, defaultOpen=$defaultOpen)" | MNU-API-01, MNU-DOM-09, MNU-ANI-02, MNU-ANI-09 |

### `portal/MenuPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Portal keepMounted />`, `refInstanceof: HTMLDivElement`, inside `Menu.Root`) | MNU-STR-04, MNU-API-24, RND-DOM-36..49 |

### `positioner/MenuPositioner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Positioner />`, `refInstanceof: HTMLDivElement`) | MNU-STR-05, RND-DOM-36..49 |
| positionerConformanceTests (`viewport: true`) | MNU-API-26, MNU-API-27, MNU-API-28, MNU-POS-02, MNU-POS-03, MNU-POS-06, MNU-POS-08, MNU-POS-09 |
| "throws when rendered outside Menu.Portal" | MNU-STR-04, MNU-EDGE-03 |
| "enables lazy flipping for a filter menu" / "leaves lazy flipping off for a plain menu" | MNU-POS-05 |
| "uses the layout viewport for a root context menu" | MNU-PTR-18, MNU-POS-04 |
| "disables cross-axis shifting when side collision avoidance is flip" | MNU-POS-04 |
| "preserves explicit context-menu placement and offsets" | MNU-API-26, MNU-API-27, MNU-PTR-18, MNU-POS-02 |
| "uses the visual viewport for a context menu submenu" | MNU-PTR-18, MNU-POS-04 |
| "closes an open submenu with a sibling reason when its controlled parent closes" | MNU-API-49, MNU-DIS-04, MNU-DIS-05, MNU-EVT-04, MNU-EVT-07 |
| "should be placed near the specified element when a ref is passed" / "should be placed near the specified element when an element is passed" / "should be placed near the specified element when a function returning an element is passed" / "should be placed at the specified position" / "should accept a non-memoized function as an anchor" / "should react to the anchor changing from a ref to undefined and back" | MNU-POS-01 |
| "when keepMounted=true, should keep the content mounted when closed" | MNU-DOM-05, MNU-ANI-01, MNU-POS-09 |
| "when keepMounted=false, should unmount the content when closed" | MNU-ANI-01 |
| "uses bottom as the default side when the menubar is horizontal" / "uses inline-end as the default side when the menubar is vertical" | MNU-POS-02 |

### `radio-group/MenuRadioGroup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.RadioGroup />`, `refInstanceof: HTMLDivElement`) | MNU-STR-10, RND-DOM-36..49 |
| "renders a div with the `group` role" | MNU-ARIA-10 |

### `radio-item-indicator/MenuRadioItemIndicator.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.RadioItemIndicator keepMounted />`, `refInstanceof: HTMLSpanElement`) | MNU-STR-10, MNU-API-43, RND-DOM-36..49 |
| "throws when rendered outside Menu.RadioItem" | MNU-STR-09, MNU-EDGE-03 |
| "should remove the indicator when there is no exit animation defined" / "should remove the indicator when the animation finishes" | MNU-ANI-13 |
| "keeps the indicator mounted to play its exit animation when unchecked without keepMounted" | MNU-DOM-17, MNU-ANI-13 |

### `radio-item/MenuRadioItem.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.RadioItem value="0" />`, `refInstanceof: HTMLDivElement`) | MNU-STR-08, MNU-STR-09, RND-DOM-36..49 |
| "throws when rendered outside Menu.RadioGroup" | MNU-STR-09, MNU-EDGE-03 |
| "perf: does not rerender menu items unnecessarily" | §14 (MNU-EDGE-11 withdrawn) |
| "adds the state and ARIA attributes when selected" | MNU-API-44, MNU-DOM-16, MNU-ARIA-09 |
| "selects the item when $key is pressed" | MNU-KEY-08 |
| "does not select when Space is pressed during an active typeahead session" | MNU-KEY-13 |
| "calls `onValueChange` when the item is clicked" | MNU-API-45, MNU-API-47, MNU-EVT-09 |
| "does not select when `onValueChange` cancels the event" | MNU-API-45, MNU-EVT-09 |
| "keeps the state when closed and reopened" | MNU-API-44, MNU-EVT-09 |
| "when `closeOnClick=true`, closes the menu when the item is clicked" / "does not close the menu when the item is clicked by default" | MNU-PTR-15 |
| "can be focused but not interacted with when a radio group is disabled" | MNU-API-46 |
| "can be focused but not interacted with when individual items are disabled" | MNU-API-35 |

### `root/MenuRoot.detached-triggers.test.tsx`

| Test | Requirements |
|---|---|
| detachedTriggersConformanceTests (`openInteractions: ['click']`, `ariaExpanded: true`, `throwOnMissingTrigger: true`) | MNU-API-13, MNU-API-14, MNU-API-15, MNU-API-63, MNU-API-64, MNU-API-65, MNU-API-66, MNU-FOC-17, MNU-ANI-08, MNU-POS-10, MNU-EDGE-07 |
| "does not apply the trigger-change instant to a hover close after switching triggers" | MNU-STA-04, MNU-PTR-08, MNU-ANI-07, MNU-ANI-08 |
| "does not restore the trigger-change instant when a controlled close commits late" | MNU-API-13, MNU-DIS-13, MNU-ANI-08 |
| "does not restore the trigger-change instant after a hover close has started" | MNU-DIS-11, MNU-ANI-08 |
| "supports keyboard navigation regardless of which trigger opened the menu" | MNU-FOC-17 |
| "opens a submenu on click when hover is disabled" | MNU-API-34, MNU-PTR-17 |
| "closes every level when clicking outside the deepest submenu" | MNU-DIS-01 |
| "selects nested items with click, drag, release" | MNU-PTR-03 |

### `root/MenuRoot.test.tsx`

| Test | Requirements |
|---|---|
| popupConformanceTests (`triggerMouseAction: 'click'`, `expectedPopupRole: 'menu'`) | MNU-STR-01, MNU-API-01, MNU-API-02, MNU-API-03, MNU-API-04, MNU-API-32, MNU-ARIA-01, MNU-ARIA-02, MNU-ARIA-03, MNU-EVT-01, MNU-EVT-02, MNU-EVT-06, MNU-ANI-01, MNU-PTR-01, MNU-DIS-02 |
| popupListConformanceTests (`itemRole: 'menuitem'`, `listEnd: 'wrap'`) | MNU-KEY-02, MNU-KEY-03, MNU-KEY-04, MNU-KEY-05, MNU-KEY-07, MNU-KEY-08, MNU-KEY-10, MNU-KEY-11, MNU-KEY-12, MNU-PTR-01, MNU-PTR-13, MNU-PTR-14, MNU-PTR-15, MNU-FOC-01, MNU-FOC-02, MNU-FOC-03, MNU-FOC-04, MNU-DIS-01, MNU-DIS-02, MNU-DIS-08, MNU-EDGE-13 |
| "returns focus on Escape after a controlled root ignores a Shift+Tab close" | MNU-API-02, MNU-ARIA-02, MNU-FOC-04, MNU-DIS-08 |
| "keeps focus in the menu when a Shift+Tab close is canceled" | MNU-API-03, MNU-KEY-11, MNU-DIS-08, MNU-EVT-02 |
| "stays open while the pointer crosses the gap from the trigger to the popup" | MNU-PTR-05, MNU-PTR-07 |
| "highlights the first item when the items render a microtask after a keyboard opening" | MNU-KEY-02, MNU-EDGE-09 |
| "moves focus to the first item after a screen reader $name" | MNU-STA-03, MNU-KEY-02, MNU-PTR-01, MNU-PTR-16 |
| "warns that nested menus ignore the modal prop" | MNU-API-08, MNU-API-49, MNU-EDGE-02 |
| "does not emit the nested modal warning in production" | MNU-EDGE-02 |
| "sets aria-orientation on a horizontal popup" | MNU-API-09, MNU-ARIA-04 |
| "does not render aria-orientation on a vertical popup" | MNU-ARIA-04 |
| "changes the highlighted item using the arrow keys" | MNU-STA-09, MNU-KEY-03, MNU-KEY-04 |
| "navigates across grouped items with arrow keys and text" | MNU-KEY-04 |
| "closes with a `detail === 0` click event on keyboard item activation" | MNU-STA-03, MNU-KEY-03, MNU-KEY-08, MNU-DIS-06, MNU-EVT-05 |
| "changes the highlighted item using the Home and End keys" | MNU-KEY-05 |
| "includes disabled items during keyboard navigation" | MNU-API-35, MNU-ARIA-07, MNU-KEY-07 |
| "skips items hidden with CSS during keyboard navigation" | MNU-KEY-07 |
| "changes the highlighted item" / "skips items hidden with CSS in text navigation" | MNU-KEY-12 |
| "skips natively disabled items in text navigation" | MNU-API-37, MNU-DOM-13 |
| "changes the highlighted item using text navigation on label prop" | MNU-API-36 |
| "skips the non-stringifiable items" / "navigate to options with diacritic characters" / "navigate to next options that begin with diacritic characters" | MNU-KEY-12 |
| "does not trigger the onClick event when Space is pressed during text navigation" / "does not open a submenu when pressing Space during a typeahead session" | MNU-KEY-13 |
| "opens a focused submenu trigger with Space when not typing" | MNU-KEY-13, MNU-KEY-17 |
| "matches "Item 2" after "Item " currently matches "Item 1"" | MNU-KEY-12 |
| "matches a submenu trigger label after a space + numeric suffix" | MNU-KEY-18 |
| "opens a nested menu of a $orientation $direction menu with $openKey key and closes it with $closeKey" | MNU-API-09, MNU-KEY-15, MNU-KEY-16, MNU-I18N-01, MNU-I18N-02 |
| "opens submenu on click when openOnHover is false" | MNU-API-50, MNU-PTR-17 |
| "renders root menu portal ownership without an accessibility role" | MNU-STR-18, MNU-DOM-26 |
| "renders submenu portal ownership as an allowed menu child" | MNU-STR-18, MNU-DOM-26, MNU-ARIA-18 |
| "keeps the root menu open when a submenu opens and the trigger `render` element has a custom id" | MNU-API-18, MNU-DOM-03 |
| "closes submenus when focus is lost by shift-tabbing from a nested menu" | MNU-KEY-11, MNU-FOC-08 |
| "closes the entire tree when clicking outside the deepest submenu" | MNU-STR-12, MNU-DOM-08, MNU-DIS-01, MNU-DIS-04 |
| "calls onOpenChange with false exactly once per menu when a submenu item is clicked" | MNU-STA-01, MNU-DIS-04, MNU-DIS-06, MNU-EVT-01, MNU-EVT-04, MNU-EVT-07 |
| "returns focus to submenu triggers when closing nested menus" | MNU-STR-12, MNU-KEY-16, MNU-FOC-08 |
| "keeps the menu and dialog open when pressing Shift+Tab inside a nested dialog" | MNU-KEY-11, MNU-FOC-11, MNU-EDGE-13 |
| "keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item" / "keeps pending focus in a nested dialog when the pointer leaves the triggering menu item" | MNU-PTR-10, MNU-FOC-11 |
| "focuses the first item after the menu is opened by keyboard" | MNU-DOM-14, MNU-KEY-02, MNU-FOC-01 |
| "focuses the first item when down arrow key opens the menu" / "focuses the last item when up arrow key opens the menu" | MNU-KEY-01, MNU-FOC-01 |
| "focuses the trigger after the menu is closed" | MNU-FOC-04 |
| "focuses the trigger after Escape when the closing menu receives mouseleave" | MNU-FOC-04, MNU-EDGE-12 |
| "focuses the trigger after the menu is closed but not unmounted" | MNU-STA-02, MNU-FOC-05 |
| "closes the menu and moves focus to the next element when tabbing forward from the open menu" | MNU-KEY-10, MNU-FOC-06, MNU-DIS-08 |
| "closes the menu and moves focus to the trigger when shift-tabbing from the open menu" | MNU-KEY-11, MNU-FOC-06 |
| "does not close the parent menu when the Escape key is pressed by default" | MNU-KEY-09, MNU-KEY-19, MNU-DIS-03 |
| "closes the parent menu when the Escape key is pressed if `closeParentOnEsc=true`" | MNU-KEY-19, MNU-DIS-03 |
| "should render an internal backdrop when `true`" | MNU-STR-17, MNU-DOM-25, MNU-FOC-13, MNU-DIS-09 |
| "should not render an internal backdrop when `false`" | MNU-STR-17, MNU-FOC-10, MNU-DIS-09 |
| "does not close after hovering out of a popup opened without trigger hover" | MNU-PTR-08, MNU-DIS-11 |
| "returns focus to the opener when a menu is opened programmatically" | MNU-API-13, MNU-FOC-04, MNU-FOC-17 |
| "does not close after hovering out of a popup opened externally" | MNU-PTR-08, MNU-FOC-02, MNU-DIS-11 |
| "closes after hovering out of a popup opened by its trigger" | MNU-PTR-08, MNU-DIS-11 |
| "should not apply scroll lock when opened via touch" / "should apply scroll lock when opened via mouse" / "should apply scroll lock when a touch-opened popup covers the viewport width" / "should not apply scroll lock when a touch-opened popup is narrower than the viewport" | MNU-DIS-09 |
| "unmounts the menu when the `unmount` method is called" | MNU-API-12, MNU-DIS-13, MNU-EVT-03, MNU-ANI-10 |
| "opens when the trigger is hovered and closes when it is no longer hovered" | MNU-PTR-05 |
| "opens the submenu on hover with zero delay" | MNU-API-51, MNU-PTR-09 |
| "does not clear body pointer-events styles when closing a scoped submenu" / "scopes submenu safePolygon pointer events to the parent menu with keepMounted portal" | MNU-PTR-07 |
| "should not close when submenu is hovered after root menu is hovered" | MNU-STA-11, MNU-PTR-07 |
| "keeps the parent submenu open after a third-level submenu closes due to sibling hover" | MNU-STA-11, MNU-PTR-11, MNU-DIS-05 |
| "reopens on hover after an impatient click closes via item press" | MNU-API-20, MNU-PTR-06, MNU-DIS-06 |
| "treats hover-opened menus as modal after a click" | MNU-STR-17, MNU-STA-06, MNU-PTR-06, MNU-DIS-11 |
| "should close after delay" | MNU-API-21, MNU-PTR-07 |
| "should close submenu after delay when hovering a sibling item" | MNU-API-51, MNU-PTR-11, MNU-DIS-05 |
| "should not restart closeDelay on repeated mousemove over sibling items" | MNU-API-51, MNU-PTR-11 |
| "opens a submenu after a plain delay without waiting for the pointer to rest" | MNU-API-51, MNU-STA-12, MNU-PTR-09 |
| "cancels a pending submenu hover-open when the pointer leaves via mouseout" | MNU-PTR-09, MNU-EDGE-12 |
| "keeps a pending submenu hover-open when mouseout stays within the trigger" | MNU-PTR-09 |
| "triggers a menu item and closes the menu on click, drag, release" | MNU-PTR-02, MNU-PTR-03 |
| "closes the menu on click, drag outside, release" | MNU-PTR-04, MNU-DIS-07, MNU-EVT-04 |
| "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen" | MNU-DOM-01, MNU-EVT-03, MNU-ANI-06, MNU-ANI-10 |
| "does not leave a tabbable item after tabbing out of a keepMounted menu before close" | MNU-API-12, MNU-DOM-06, MNU-DOM-14, MNU-FOC-03, MNU-FOC-05, MNU-EDGE-14 |
| "keeps the dialog open after a press-drag-release activation" | MNU-PTR-03, MNU-FOC-11, MNU-EDGE-13 |
| "highlights an item on mouse move by default" / "does not highlight items from mouse movement when disabled" / "does not highlight submenu triggers from mouse enter when disabled" | MNU-PTR-12 |
| "marks items as disabled when controlled open" | MNU-DOM-13, MNU-DIS-12 |
| "does not highlight items with text navigation when controlled open" | MNU-KEY-14, MNU-DIS-12 |
| "does not close or activate items when controlled open" | MNU-DIS-12 |
| "disables submenu triggers when controlled open" | MNU-API-52, MNU-DIS-12 |
| "skips null items when navigating" | MNU-KEY-04, MNU-FOC-12, MNU-EDGE-09 |
| "does not re-render the trigger while navigating the list" | §14 (MNU-EDGE-11 withdrawn) |
| "passes the keydown event for keyboard highlights" / "passes the keydown event for typeahead highlights" | MNU-EVT-10 |
| "passes native mouse and pointer events for pointer highlights" | MNU-STA-09, MNU-PTR-14, MNU-EVT-10 |
| "passes the mouse event when a submenu trigger is hovered" | MNU-PTR-14, MNU-EVT-10 |
| "does not report a stale highlight reason for a later list change" | MNU-STA-10, MNU-EVT-11 |
| "reports highlights correctly after onItemHighlighted is removed and added back" | MNU-EVT-13 |
| "reports clearing a highlight made before onItemHighlighted was added" | MNU-EVT-12 |
| "releases the subscriptions a submenu adds to its parent store when it unmounts" / "releases them under StrictMode" / "notifies submenu subscribers when shared parent state changes under StrictMode" | §14 (MNU-EDGE-11 withdrawn) |

### `root/MenuRoot.triggerless.test.tsx`

| Test | Requirements |
|---|---|
| "keeps nested Escape and focus local, keepMounted=%s" | MNU-KEY-09, MNU-FOC-05, MNU-FOC-17, MNU-I18N-01 |
| "supports an external anchor across reopen, keepMounted=%s" | MNU-API-30, MNU-STA-02, MNU-POS-01, MNU-POS-06 |
| "closes a sibling submenu without closing the root on %s" | MNU-PTR-11, MNU-DIS-05 |
| "keeps an initially open submenu attached to the root" | MNU-DIS-04 |
| "preserves real trigger registration, initially present=%s" | MNU-STR-11, MNU-FOC-17 |

### `submenu-trigger/MenuSubmenuTrigger.screenReaderPress.test.tsx`

| Test | Requirements |
|---|---|
| "toggles the submenu on repeated screen reader activation with the default `openOnHover`" | MNU-PTR-16, MNU-A11Y-03 |
| "does not close the submenu on the trailing click of a screen reader press with `openOnHover={false}`" | MNU-PTR-16, MNU-PTR-17, MNU-A11Y-03 |
| "opens on a screen reader press without waiting a frame" / "reports the trigger, not a pressed descendant, as the opener" | MNU-PTR-16, MNU-A11Y-03 |
| "ignores an ordinary mouse press with the default `openOnHover`" | MNU-PTR-09, MNU-PTR-17, MNU-A11Y-03 |
| "opens a filterable submenu from a filterable parent and focuses its input" / "keeps focus in a kept-mounted filterable submenu opened by a screen reader press" | MNU-FOC-15, MNU-A11Y-03 |

- **MNU-A11Y-03** — MUST. Screen-reader (virtual) presses on `Menu.SubmenuTrigger` are detected by `useClick`: a `click` with `detail === 0` that was not preceded by a keydown, or a `pointerdown` that `isVirtualPointerEvent` classifies as virtual (empty `pointerType`, zero-size press, Chrome's 1x1 pressureless press). Such a press toggles the submenu with reason `trigger-press` synchronously (no frame delay), repeated presses keep toggling with the default `openOnHover`, the trailing `click` of a press under `openOnHover={false}` does not close the submenu again, `details.trigger` is the trigger element even when a descendant was pressed, an ordinary mouse press under the default `openOnHover` is ignored (MNU-PTR-17), and a filterable submenu opened this way focuses its input (also when kept mounted).
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:192-204`; `floating-ui-react/hooks/useClick.ts:98-219`; `floating-ui-react/utils/event.ts:13-47`. Test: [L4] `MenuSubmenuTrigger.screenReaderPress.test.tsx` (all 7 tests).

### `submenu-trigger/MenuSubmenuTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.SubmenuTrigger />`, `refInstanceof: HTMLDivElement`, `button: true`) | MNU-STR-08, MNU-STR-03, RND-DOM-36..49, UBT, conformance |
| "keeps a hover-opened submenu open when pressing %s" | MNU-API-50, MNU-KEY-17 |
| "closes a touch-opened submenu on a second tap" | MNU-API-50, MNU-PTR-16, MNU-PTR-17 |
| "keeps a submenu open on direct trigger focus but closes it on guard return" | MNU-FOC-06, MNU-FOC-08 |
| "closes a submenu when its focus guard is in a shadow-root portal" | MNU-API-25, MNU-FOC-06, MNU-EDGE-10 |
| "follows a submenu trigger id change" | MNU-API-53, MNU-EDGE-09 |
| "throws when rendered outside Menu.SubmenuRoot" | MNU-STR-03, MNU-EDGE-03 |
| "throws when Menu.SubmenuRoot is rendered outside a menu" | MNU-STR-07, MNU-EDGE-03 |
| "opens the submenu with $openKey and highlights a single item in $direction direction" | MNU-STA-09, MNU-KEY-15, MNU-I18N-01 |
| "sets tabIndex to 0 on the submenu trigger after opening the submenu with a keydown event" | MNU-DOM-18, MNU-KEY-15, MNU-FOC-03 |
| "uses the label prop for text navigation" | MNU-API-36, MNU-KEY-12 |
| "should render with disabled attributes when disabled prop is set" | MNU-DOM-18 |
| "does not open on hover when disabled" | MNU-PTR-09 |
| "should warn when a disabled element is detected via render prop with JSX element" / "warns without an owner stack when React cannot provide one" / "does not inspect rendered disabled elements in production" | MNU-EDGE-06 |

### `submenu-trigger/MenuSubmenuTrigger.voiceOver.test.tsx`

| Test | Requirements |
|---|---|
| "omits the expanded state when the submenu is opened with ArrowRight" / "omits the expanded state when the submenu is opened with Enter" / "keeps the expanded state when the submenu is opened with a pointer" | MNU-ARIA-13, MNU-A11Y-01 |

- **MNU-A11Y-01** — MUST. The VoiceOver `aria-expanded` omission (MNU-ARIA-13) is keyed on a platform screen-reader hint (`platform.screenReader.voiceOver`, Apple platforms) and on how the submenu was opened: `openedByKeyboard` = (last open-change reason is `list-navigation`) OR (`openMethod === 'keyboard'`, i.e. Enter/Space click), so the attribute is omitted while a keyboard-opened submenu is open, kept as `"true"` for a pointer-opened one, and always `"false"` while closed. Ports MUST expose an equivalent platform hint and apply the omission only under it.
  Source: `menu/submenu-trigger/MenuSubmenuTrigger.tsx:212-219`. Test: [L4] `MenuSubmenuTrigger.voiceOver.test.tsx` "omits the expanded state when the submenu is opened with ArrowRight", "omits the expanded state when the submenu is opened with Enter", "keeps the expanded state when the submenu is opened with a pointer".

### `trigger/MenuTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Trigger />`, `refInstanceof: HTMLButtonElement`, `button: true`) | MNU-STR-02, MNU-API-16, RND-DOM-36..49, UBT, conformance |
| "throws without Menu.Root or a handle" | MNU-STR-02, MNU-EDGE-01 |
| "focuses the preceding element when modal=%s" | MNU-FOC-06, MNU-FOC-07 |
| "should render a disabled button" | MNU-DIS-12 |
| "should not open the menu when clicked" | MNU-PTR-01, MNU-DIS-12 |
| "removes the hover mouseup listener when unmounted before mouseup" | MNU-PTR-02, MNU-EDGE-12 |
| "opens the menu when pressing $key on a $buttonType button" | MNU-API-17, MNU-KEY-01 |
| "should have the data-popup-open and data-pressed attributes when open" | MNU-DOM-01 |
| "keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed" | MNU-API-02, MNU-API-66, MNU-DOM-01, MNU-DIS-13, MNU-EVT-01, MNU-EVT-02, MNU-EDGE-14 |
| "does not close the menu if the user clicks too quickly" / "closes the menu if the user clicks patiently" / "sticks if the user clicks impatiently" | MNU-PTR-06 |
| "sticks when clicked before the hover delay completes" / "should keep the menu open when re-hovered and clicked within the patient threshold" | MNU-API-20, MNU-PTR-06 |
| "keeps a hover-opened menu open when mouseup lands outside the trigger DOM but within its bounds" | MNU-DOM-07, MNU-PTR-04 |
| "prevents opening the menu with a mouse when `preventBaseUIHandler` is called in onMouseDown" | MNU-PTR-01, MNU-EVT-14 |
| "prevents opening the menu with keyboard when `preventBaseUIHandler` is called in onClick" | MNU-EVT-14 |
| "does not have role prop inside a Popover" | MNU-ARIA-17, MNU-EDGE-13 |
| "has a role prop inside a Popover when not a native button" | MNU-API-17, MNU-ARIA-17, MNU-EDGE-13 |

### `viewport/MenuViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Menu.Viewport />`, `refInstanceof: HTMLDivElement`) | MNU-API-33, RND-DOM-36..49 |
| viewportConformanceTests (`openOn: 'click'`) | MNU-API-33, MNU-DOM-12, MNU-POS-10, MNU-ANI-08 |

### Type-level specs

| File | Requirements |
|---|---|
| `root/MenuRoot.spec.tsx` (`onItemHighlighted` detail types: `reason`, `label`, `event` narrowing by reason, no `index`) | MNU-EVT-10, MNU-EVT-11 |
| `link-item/MenuLinkItem.spec.tsx` (`render` callback receives native `<a>` props such as `href`) | MNU-API-40, MNU-STR-08 |
| `positioner/MenuPositioner.spec.tsx` (`keepMounted` is not a Positioner prop) | MNU-API-24 |

### Tests in other files that exercise this component

`context-menu/**/*.test.tsx`, `menubar/**/*.test.tsx`, `toolbar/**/*.test.tsx` and `popover/**/*.test.tsx` exercise Menu as a child of those components (context-menu anchoring, modality and the 500 ms outside-press grace: MNU-PTR-18, MNU-DIS-10, MNU-FOC-13, MNU-POS-04; menubar key relay and `instantType: 'group'`: MNU-KEY-21, MNU-ANI-07; toolbar key stopping: MNU-KEY-20; trigger role inside Popover: MNU-EDGE-13). They are owned by those components' docs and are not enumerated here.

## 14. React-specific notes and porting considerations

- **Store and selectors.** `MenuStore` is a `ReactStore` whose selectors (`isActive(index)`, `webkitAriaSelected`, `itemProps`, `triggerProps`, `isFilterItemVisible`, …) let each item subscribe to exactly the slice it renders; a port must offer per-item change notification with the same granularity or it will fail the perf tests listed below. Submenu stores subscribe to their parent store (`subscribeToParentMenu`) for `disabled`, `rootId`, keyboard relay and `instantType`, and must release those subscriptions on unmount. Port: see decisions/05-state-store-and-event-details.md §1.
- **Rendering cost (formerly MNU-EDGE-11, withdrawn as React-specific).** The perf tests assert re-render counts, which have no observable DOM effect: moving the highlight re-renders only the items whose highlighted state changed (items subscribe to `isActive(index)` selectors, not the whole store); the Trigger is not re-rendered while the list is navigated or when the list mounts, and inactive triggers are not re-rendered either; a filterable menu renders its trigger as often as a plain menu on mount; and the subscriptions a submenu adds to its parent store are released on unmount (also under development double-invocation) while shared parent state still notifies subscribers. Sources: `menu/store/MenuStore.ts:76-149`; `menu/item/useMenuItem.ts`; `menu/root/MenuRoot.tsx:476`. Tests: `MenuItem.test.tsx`, `MenuCheckboxItem.test.tsx`, `MenuRadioItem.test.tsx` "perf: does not rerender menu items unnecessarily"; `MenuRoot.test.tsx` "does not re-render the trigger while navigating the list", "releases the subscriptions a submenu adds to its parent store when it unmounts", "releases them under StrictMode", "notifies submenu subscribers when shared parent state changes under StrictMode"; `MenuList.test.tsx` "does not re-render an inactive trigger when the list mounts"; `MenuFilterRoot.test.tsx` "does not rerender unaffected items when the highlight moves", "renders the trigger as often as a plain menu on mount". A port should match the granularity where its rendering model has an equivalent cost. Port: see decisions/05-state-store-and-event-details.md §1.
- **Effects and timing.** Highlight reporting runs in a layout effect after `activeIndex` commits and again when the composite list settles; the initial keyboard highlight is retried on a microtask and on the next frame (MNU-KEY-02). `instantType` must be written in the same update that mounts the popup (React 17 legacy mode flushes synchronously, MNU-ANI-04). Ports with a different scheduling model must preserve the ordering: `onOpenChange` → state write → mount → focus move → `onItemHighlighted` → animations → `onOpenChangeComplete`. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4).
- **Composition of handlers.** Every DOM handler is merged with `mergeProps` (RND), consumer handlers first, with `preventBaseUIHandler()` short-circuiting the internal one; ports need an equivalent per-event veto mechanism (MNU-EVT-14/15). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision.
- **Render functions and the `State` objects.** Each part exposes the `State` listed in §3 to `className`/`style`/`render`; state objects are memoized per part and ports should keep them cheap (items re-render on highlight changes only). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision.
- **Floating tree.** Menu relies on `FloatingTree`/`FloatingNode` ids (`floatingNodeId`, `floatingParentNodeId`) for outside-press containment, nested dismissal, `itemhover`/`menuopenchange`/`close`/`setOpen` events and the keyboard relay; a port needs an equivalent tree with event bus and parent/child lookup (`getNodeChildren`). Hover opening, the safe polygon, close delays and the tree event bus live in the interactions module. Port: see decisions/04-js-interop-boundary.md §1.
- **Platform sniffing.** Three behaviors depend on runtime platform detection: VoiceOver `aria-expanded` omission (MNU-A11Y-01), WebKit `aria-selected` (MNU-A11Y-02) and the Chrome 1x1 pressureless virtual press (MNU-A11Y-03). Ports must expose the same hints or document the deviation. Port: see decisions/04-js-interop-boundary.md §4.
- **Filter mode is a composition, not a flag.** `Menu.FilterProvider` swaps the Root/SubmenuRoot implementation (`MenuFilterRoot`, `MenuFilterSubmenuRoot`) and the Popup/List/item hooks through `MENU_FILTER_IMPL`; the filter engine itself (`filter-dropdown/*`) is shared with other components and should be ported once (its state machine is restated in §4.4 only as consumed by Menu). The filterable input, query state and filter engine are C#. Port: see decisions/04-js-interop-boundary.md §1.
- **`useFilter`/`getFilter`** depend on `Intl.Collator`; `locale` must map to the port platform's collation facility (MNU-I18N-03). Port: see decisions/04-js-interop-boundary.md §1.
- **Ids.** `useBaseUiId`/`useRenderedId` and the React 17 fallback (MNU-EDGE-08) decide when `aria-controls`/`aria-labelledby`/`aria-activedescendant` can be rendered; ports should render those relationships only once both ids are known. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision.
- **List navigation, typeahead and roving focus.** Arrow/Home/End navigation, typeahead matching, roving `tabIndex` and per-key `preventDefault` (MNU-KEY-01…08, MNU-KEY-11…13) run in the composite module; C# receives active-index/highlight changes and raises `onItemHighlighted` from them. Port: see decisions/04-js-interop-boundary.md §1.
- **Escape bubbling and the consumer cancel.** Escape inside a submenu closes only the innermost menu unless `closeParentOnEsc` (MNU-KEY-09, FLT-KEY-01), and `onOpenChange` may cancel the close; under Server the cancel cannot run before the key's default handling, so the fallback policy applies. Port: see decisions/04-js-interop-boundary.md §3 and decisions/01-hosting-model-and-timing.md §Relaxation table (EVD-EVT-11, EVD-DIS-01, FLT-KEY-01).

## 15. Requirement index

| ID | Level | Section | Summary |
|---|---|---|---|
| MNU-STR-01 | MUST | §2 | `Menu.Root` renders no element of its own |
| MNU-STR-02 | MUST | §2 | `Menu.Trigger` renders a `<button>` by default and MUST resolve a store either from an ancestor… |
| MNU-STR-03 | MUST | §2 | Detached triggers are supported only for top-level menus |
| MNU-STR-04 | MUST | §2 | `Menu.Portal` MUST enclose `Menu.Positioner` |
| MNU-STR-05 | MUST | §2 | `Menu.Positioner` renders a `<div>` and MUST enclose `Menu.Popup` |
| MNU-STR-06 | MUST | §2 | `Menu.Popup` renders a `<div>` carrying the `menu` role (or `presentation` when a `Menu.List` is rendered,… |
| MNU-STR-07 | MUST | §2 | Every part other than Root, Trigger (with a handle), Portal, Backdrop and FilterProvider MUST be a… |
| MNU-STR-08 | MUST | §2 | `Menu.Item`, `Menu.CheckboxItem`, `Menu.RadioItem` and `Menu.SubmenuTrigger` render a `<div>` |
| MNU-STR-09 | MUST | §2 | `Menu.RadioItem` MUST be inside a `Menu.RadioGroup` (throws `Base UI: MenuRadioGroupContext is missing |
| MNU-STR-10 | MUST | §2 | Default elements of group, label, indicator, separator (standalone `Separator`) and auxiliary parts |
| MNU-STR-11 | MUST | §2 | `Menu.SubmenuRoot` renders no element and MUST be a descendant of a menu (its Root or a parent SubmenuRoot) |
| MNU-STR-12 | MUST | §2 | Submenus nest to any depth |
| MNU-STR-13 | MUST | §2 | `Menu.List` is optional |
| MNU-STR-14 | MUST | §2 | `Menu.FilterProvider` renders no element and makes exactly the `Menu.Root` or `Menu.SubmenuRoot` rendered… |
| MNU-STR-15 | MUST | §2 | `Menu.Input`, `Menu.Clear` and `Menu.Empty` MUST be rendered inside a filterable menu |
| MNU-STR-16 | MUST | §2 | While a contained top-level trigger is the active trigger of an open menu, one focus guard `<span>` is… |
| MNU-STR-17 | MUST | §2 | When the popup is modal (MNU-STA-06) and was not opened by hover, an internal backdrop `<div… |
| MNU-STR-18 | MUST | §2 | The portal node's hidden `aria-owns` owner `<span>` (rendered in the Root's own tree) carries… |
| MNU-STR-19 | MUST | §2 | A Root determines its *parent kind* once from its ancestors |
| MNU-API-01 | MUST | §3.1 | `defaultOpen` defaults to `false` and is the initial value of the component-owned open state |
| MNU-API-02 | MUST | §3.1 | `open`, when not `undefined`, is the consumer-owned open state (EVD-STA-01/04) |
| MNU-API-03 | MUST | §3.1 | `onOpenChange` is optional and cancelable (§7, MNU-EVT-01..03) |
| MNU-API-04 | MUST | §3.1 | `onOpenChangeComplete` is optional |
| MNU-API-05 | MUST | §3.1 | `onItemHighlighted` is optional |
| MNU-API-06 | MUST | §3.1 | `loopFocus` defaults to `true` and is passed as the list-navigation `loopFocus` option (CMP-API-01) |
| MNU-API-07 | MUST | §3.1 | `highlightItemOnHover` defaults to `true` |
| MNU-API-08 | MUST | §3.1 | `modal` defaults to `true` |
| MNU-API-09 | MUST | §3.1 | `orientation` defaults to `'vertical'` |
| MNU-API-10 | MUST | §3.1 | `disabled` defaults to `false` |
| MNU-API-11 | MUST | §3.1 | `closeParentOnEsc` defaults to `false` |
| MNU-API-12 | MUST | §3.1 | `actionsRef.current` is `{ unmount, close }` |
| MNU-API-13 | MUST | §3.1 | `triggerId` (consumer-owned) and `defaultTriggerId` (initial, default `null`) select the active trigger… |
| MNU-API-14 | MUST | §3.1 | `handle` attaches the Root's store to a `Menu.Handle` (PSH-STR-03/04) so detached `Menu.Trigger`s with the… |
| MNU-API-15 | MUST | §3.1 | When `children` is a function it is called during every render with `{ payload }`, where `payload` is the… |
| MNU-API-16 | MUST | §3.2 | The effective disabled state is `disabled \|\| root.disabled \|\| (menubar.disabled when inside a Menubar)` |
| MNU-API-17 | MUST | §3.2 | `nativeButton` defaults to `true` and is the button mechanism's `native` option |
| MNU-API-18 | MUST | §3.2 | The trigger id is the `id` prop or a generated id (RND-DOM-27) |
| MNU-API-19 | MUST | §3.2 | `openOnHover` defaults to `undefined`, which resolves to `false` outside a Menubar and to "the menubar has… |
| MNU-API-20 | MUST | §3.2 | `delay` defaults to `100` and is the hover *rest* time (MNU-PTR-05) |
| MNU-API-21 | MUST | §3.2 | `closeDelay` defaults to `0` and is the hover close delay (MNU-PTR-07) |
| MNU-API-22 | MUST | §3.2 | `handle`, when given, selects the store exposed by the handle (PSH-STR-02) |
| MNU-API-23 | MUST | §3.2 | `payload` is forwarded to the store when this trigger becomes/is the active trigger and is read by the… |
| MNU-API-24 | MUST | §3.3 | `keepMounted` defaults to `false` |
| MNU-API-25 | MUST | §3.3 | `container` selects the parent of the portal node (PSH-STR-11) |
| MNU-API-26 | MUST | §3.5 | `side` defaults to `'bottom'` and `align` to `'center'` for a top-level menu |
| MNU-API-27 | MUST | §3.5 | `sideOffset` and `alignOffset` default to `0` |
| MNU-API-28 | MUST | §3.5 | `collisionBoundary` defaults to `'clipping-ancestors'`, `collisionPadding` to `5`, `arrowPadding` to `5`… |
| MNU-API-29 | MUST | §3.5 | `collisionAvoidance` defaults to `{ fallbackAxisSide |
| MNU-API-30 | MUST | §3.5 | `anchor` overrides the positioning reference (element, virtual element with `getBoundingClientRect`, DOM… |
| MNU-API-31 | MUST | §3.6 | `finalFocus` overrides the close focus target (MNU-FOC-09) |
| MNU-API-32 | MUST | §3.6 | The popup id is the `id` prop, else the `render` element's `id`, else a generated id (RND-DOM-32) |
| MNU-API-33 | MUST | §3.7 | `Menu.Viewport` wraps its children in the shared popup viewport (PSH-STR-07) |
| MNU-API-34 | MUST | §3.9 | `closeOnClick` defaults to `true` on `Menu.Item` and `false` on `Menu.LinkItem`, `Menu.CheckboxItem` and… |
| MNU-API-35 | MUST | §3.9 | `disabled` defaults to `false` |
| MNU-API-36 | MUST | §3.9 | `label`, when given, is the string used for typeahead matching (CMP-STA-06) and filter matching… |
| MNU-API-37 | MUST | §3.9 | `nativeButton` defaults to `false` on every item part (Item, CheckboxItem, RadioItem, SubmenuTrigger |
| MNU-API-38 | MUST | §3.9 | The item id is the `id` prop or a generated id, rendered as the element `id` |
| MNU-API-39 | MUST | §3.9 | `onClick` is called once per activation (pointer click, Enter/Space synthetic click, drag-release… |
| MNU-API-40 | MUST | §3.10 | `Menu.LinkItem` exposes no `disabled` or `nativeButton` prop |
| MNU-API-41 | MUST | §3.11 | `checked`/`defaultChecked` form a controlled/uncontrolled pair (UTL-STA-01..05) owned *above* the rendered… |
| MNU-API-42 | MUST | §3.11 | Each activation calls `onCheckedChange(!checked, details)` with `reason |
| MNU-API-43 | MUST | §3.12 | `keepMounted` defaults to `false` |
| MNU-API-44 | MUST | §3.14 | `value`/`defaultValue` form a controlled/uncontrolled pair (UTL-STA-01..05, diagnostic name `MenuRadioGroup`) |
| MNU-API-45 | MUST | §3.14 | Activating a radio item calls `onValueChange(item.value, details)` with `reason |
| MNU-API-46 | MUST | §3.14 | `disabled` defaults to `false` |
| MNU-API-47 | MUST | §3.15 | `value` is required and is passed verbatim to the group's `onValueChange` / stored as the group value on… |
| MNU-API-48 | MUST | §3.16 | The label id is the `id` prop or a generated id |
| MNU-API-49 | MUST | §3.19 | `Menu.SubmenuRoot` accepts the Root props except `modal`, `handle`, `triggerId`, `defaultTriggerId` (and… |
| MNU-API-50 | MUST | §3.20 | `openOnHover` defaults to `true` |
| MNU-API-51 | MUST | §3.20 | `delay` defaults to `100` and is both the hover open delay and the rest time of the submenu trigger… |
| MNU-API-52 | MUST | §3.20 | The effective disabled state is `disabled \|\| submenu.disabled \|\| parentMenu.disabled` |
| MNU-API-53 | MUST | §3.20 | The trigger id (prop or generated) is the element id, the submenu store's trigger-registry key… |
| MNU-API-54 | MUST | §3.21 | `filter` defaults to the shared locale filter's `contains` (UTL-I18N-08/09 |
| MNU-API-55 | MUST | §3.21 | `autoHighlight` defaults to `false` |
| MNU-API-56 | MUST | §3.21 | `locale` is passed to the default filter's collator (UTL-I18N-08) |
| MNU-API-57 | MUST | §3.21 | `value`/`defaultValue` form a controlled/uncontrolled pair for the query (UTL-STA-01..05, diagnostic name… |
| MNU-API-58 | MUST | §3.21 | `onValueChange(value, details)` is called for every query change with `details.reason` ∈ {`input-change`,… |
| MNU-API-59 | MUST | §3.22 | The input id is the `id` prop or a generated id |
| MNU-API-60 | MUST | §3.23 | `Menu.Clear` renders only while the query is non-empty (MNU-DOM-22) |
| MNU-API-61 | MUST | §3.24 | `Menu.Empty` renders its element (and children) only when the menu has no visible item (MNU-STA-23) and… |
| MNU-API-62 | MUST | §3.25 | The list id is the `id` prop, else the `render` element's id, else a generated id |
| MNU-API-63 | MUST | §3.26 | `Menu.createHandle<Payload>()` returns a new independent `Menu.Handle` (PSH-API-01) |
| MNU-API-64 | MUST | §3.26 | `handle.open(triggerId)` opens the attached menu associated with the `Menu.Trigger` whose id is… |
| MNU-API-65 | MUST | §3.26 | `handle.close()` requests a close with reason `imperative-action` on the attached Root, or is ignored… |
| MNU-API-66 | MUST | §3.26 | `handle.isOpen` is the attached Root's effective open state, `false` while no Root is attached (PSH-API-05) |
| MNU-API-67 | MUST | §3.27 | `Menu.useFilter(options?)` is the shared `getFilter` (UTL-I18N-08..13) |
| MNU-STA-01 | MUST | §4.1 | The effective open state is the consumer-owned `open` when defined, otherwise the component-owned value… |
| MNU-STA-02 | MUST | §4.1 | Before `onOpenChange` is called for a close whose details carry no `trigger`, `details.trigger` is set to… |
| MNU-STA-03 | MUST | §4.1 | A committed change records `openChangeReason` (readable as `lastOpenChangeReason`) and `keyboardOpen`,… |
| MNU-STA-04 | MUST | §4.1 | The committed change also sets `instantType` |
| MNU-STA-05 | MUST | §4.1 | A touch-originated close is suppressed when it is a `click` event closing the menu for a reason other than… |
| MNU-STA-06 | MUST | §4.1 | `modal` as stored is `true` by default and is only written for a top-level (non-nested) Root |
| MNU-STA-07 | MUST | §4.1 | The effective `disabled` is the Root's `disabled`, OR-ed with the Menubar's `disabled` when the parent… |
| MNU-STA-08 | MUST | §4.1 | `rootId` is a generated id owned by the top-level Root and read through the parent chain by every submenu |
| MNU-STA-09 | MUST | §4.2 | The highlight is a single `activeIndex` (`number \| null`) into the Root's DOM-ordered item list (CMP-STA-31) |
| MNU-STA-10 | MUST | §4.2 | Each highlight write is tagged with the reason that produced it (`keyboard` for key events, `pointer` for… |
| MNU-STA-11 | MUST | §4.2 | `hoverEnabled` starts `true`, becomes `false` when a child submenu opens (so the parent's own hover-close… |
| MNU-STA-12 | MUST | §4.2 | `allowMouseEnter` starts `false`, is set `true` by the first pointer movement over the active trigger or… |
| MNU-STA-13 | MUST | §4.2 | `listElement` is the mounted `Menu.List` element (or `null`) |
| MNU-STA-14 | MUST | §4.3 | A `Menu.CheckboxItem`'s `checked` is its own controlled/uncontrolled boolean (MNU-API-41) |
| MNU-STA-15 | MUST | §4.4 | The *filter query* is the trimmed query string |
| MNU-STA-16 | MUST | §4.4 | The first visible-set computation waits until at least one item has registered (a popup's content… |
| MNU-STA-17 | MUST | §4.4 | An item's filter text is resolved as |
| MNU-STA-18 | MUST | §4.4 | When the query changes (not canceled) and `autoHighlight` is `false`, the highlight is cleared… |
| MNU-STA-19 | MUST | §4.4 | "autoHighlight enabled for the current query" means |
| MNU-STA-20 | MUST | §4.4 | When the DOM-ordered item list changes while open (items inserted, removed, reordered, or filtered), the… |
| MNU-STA-21 | MUST | §4.4 | When the menu closes while the query is non-empty, `onValueChange('', { reason |
| MNU-STA-22 | MUST | §4.4 | The input's `highlighted` state is `inputFocusVisible && (!keyboardModality \|\| activeItemId == null)` |
| MNU-STA-23 | MUST | §4.4 | The menu is *empty* when the visible set is `null` and no item is registered, or when the visible set is empty |
| MNU-STA-24 | MUST | §4.4 | A `Menu.Group`/`Menu.RadioGroup` in a filterable menu is *hidden* iff a visible set exists, the group has… |
| MNU-STA-25 | MUST | §4.4 | A `Menu.SubmenuTrigger` under a filterable parent registers as an item of the *parent's* filter (it… |
| MNU-DOM-01 | MUST | §5.1 | `data-popup-open=""` and `data-pressed=""` are both present iff the menu is open *and* this trigger is the… |
| MNU-DOM-02 | MUST | §5.1 | `data-disabled=""` is present iff the effective disabled state is `true` |
| MNU-DOM-03 | MUST | §5.1 | The element's `id` is the trigger id (MNU-API-18) |
| MNU-DOM-04 | MUST | §5.2 | The Positioner carries `data-open`/`data-closed` (exactly one), `data-anchor-hidden` when the anchor is… |
| MNU-DOM-05 | MUST | §5.2 | The Positioner renders `role="presentation"`, the boolean `hidden` attribute while the popup is not… |
| MNU-DOM-06 | MUST | §5.3 | The Popup carries `data-open`/`data-closed`, `data-starting-style`/`data-ending-style` per its transition… |
| MNU-DOM-07 | MUST | §5.3 | The Popup renders its resolved `id` (MNU-API-32) and `data-rootownerid="<rootId>"`, the Root's generated… |
| MNU-DOM-08 | MUST | §5.3 | The Popup renders `tabindex="-1"` and the focusable-popup marker `data-base-ui-focusable=""` (CMP-DOM-05),… |
| MNU-DOM-09 | MUST | §5.3 | While `transitionStatus === 'starting'` the Popup carries `style="transition |
| MNU-DOM-10 | MUST | §5.4 | The Backdrop renders `role="presentation"`, the boolean `hidden` attribute while the popup is not mounted,… |
| MNU-DOM-11 | MUST | §5.5 | The Arrow renders `aria-hidden="true"`, the arrow positioning inline styles from the Positioner (FLT),… |
| MNU-DOM-12 | MUST | §5.6 | The Viewport renders `data-activation-direction="<horizontal> <vertical>"` (tokens `left`/`right` and… |
| MNU-DOM-13 | MUST | §5.7 | `Menu.Item` renders `data-highlighted=""` iff it is the highlighted item and `data-disabled=""` iff its… |
| MNU-DOM-14 | MUST | §5.7 | Every item part (Item, LinkItem, CheckboxItem, RadioItem) renders `tabindex="0"` iff the menu is open, the… |
| MNU-DOM-15 | MUST | §5.7 | `Menu.LinkItem` renders `data-highlighted=""` when highlighted and no `data-disabled` |
| MNU-DOM-16 | MUST | §5.7 | `Menu.CheckboxItem` and `Menu.RadioItem` render exactly one of `data-checked=""` / `data-unchecked=""`,… |
| MNU-DOM-17 | MUST | §5.7 | The indicators render `data-checked`/`data-unchecked`, `data-disabled`, `data-highlighted` from the parent… |
| MNU-DOM-18 | MUST | §5.7 | `Menu.SubmenuTrigger` renders `data-popup-open=""` iff its submenu is open, `data-highlighted=""` iff… |
| MNU-DOM-19 | MUST | §5.8 | `Menu.GroupLabel` renders its resolved `id` (MNU-API-48) |
| MNU-DOM-20 | MUST | §5.8 | In a filterable menu, `Menu.Group` and `Menu.RadioGroup` render the boolean `hidden` attribute while… |
| MNU-DOM-21 | MUST | §5.8 | `Menu.Input` renders `<input type="text" role="searchbox" inputmode="search" autocomplete="off"… |
| MNU-DOM-22 | MUST | §5.8 | `Menu.Clear` renders only while the query is non-empty |
| MNU-DOM-23 | MUST | §5.8 | `Menu.Empty` renders `<div role="status" aria-live="polite" aria-atomic="true">` only while the menu is… |
| MNU-DOM-24 | MUST | §5.8 | `Menu.List` renders its resolved `id`, `tabindex="-1"` (so a pointer open can focus it and scrollable… |
| MNU-DOM-25 | MUST | §5.8 | The internal backdrop (MNU-STR-17) is `<div role="presentation" data-base-ui-inert="">` with a fixed… |
| MNU-DOM-26 | MUST | §5.8 | The portal node carries the shared `data-base-ui-portal` marker and an id, and the Root's tree contains a… |
| MNU-DOM-27 | MUST | §5.8 | `Menu.Separator` renders `data-orientation="horizontal\|vertical"` (SEP-DOM-01) |
| MNU-DOM-28 | MUST | §5.8 | An item (Item, LinkItem, CheckboxItem, RadioItem, SubmenuTrigger) that the filter hides renders no element… |
| MNU-DOM-29 | MUST | §5.8 | `Menu.RadioGroup` renders `data-disabled=""` when its `disabled` prop is `true` (generic projection of its… |
| MNU-ARIA-01 | MUST | §5.9 | Every `Menu.Trigger` renders `aria-haspopup="menu"` |
| MNU-ARIA-02 | MUST | §5.9 | The active trigger renders `aria-expanded` equal to the effective open state |
| MNU-ARIA-03 | MUST | §5.9 | A trigger renders `aria-controls` only while it owns the open popup (or is the lone registered trigger of… |
| MNU-ARIA-04 | MUST | §5.9 | Without a `Menu.List`, the Popup renders `role="menu"`, `aria-orientation="horizontal"` only when… |
| MNU-ARIA-05 | MUST | §5.9 | With a `Menu.List` mounted (plain menu), the Popup renders `role="presentation"` and no… |
| MNU-ARIA-06 | MUST | §5.9 | In a filterable menu the Popup renders `role="dialog"` (with `aria-labelledby` per MNU-ARIA-19 and no… |
| MNU-ARIA-07 | MUST | §5.9 | `Menu.Item` and `Menu.LinkItem` render `role="menuitem"` |
| MNU-ARIA-08 | MUST | §5.9 | `Menu.CheckboxItem` renders `role="menuitemcheckbox"` and `aria-checked="true"\|"false"` |
| MNU-ARIA-09 | MUST | §5.9 | `Menu.RadioItem` renders `role="menuitemradio"` and `aria-checked="true"\|"false"` |
| MNU-ARIA-10 | MUST | §5.9 | `Menu.Group` and `Menu.RadioGroup` render `role="group"` and `aria-labelledby` = the id of their mounted… |
| MNU-ARIA-11 | MUST | §5.9 | `Menu.GroupLabel` renders `aria-hidden="true"` by default (the group's accessible name comes from… |
| MNU-ARIA-12 | MUST | §5.9 | `Menu.Arrow`, `Menu.CheckboxItemIndicator` and `Menu.RadioItemIndicator` render `aria-hidden="true"`; `Menu.Separator` renders `role="separator"` |
| MNU-ARIA-13 | MUST | §5.9 | `Menu.SubmenuTrigger` renders `role="menuitem"`, `aria-haspopup="menu"` (`"dialog"` when its own submenu… |
| MNU-ARIA-14 | MUST | §5.9 | `Menu.Input` renders `role="searchbox"`, `aria-controls` = the List id, and `aria-activedescendant` = the… |
| MNU-ARIA-15 | MUST | §5.9 | On WebKit engines only, while the menu's `Menu.Input` has focus, the highlighted item (Item, LinkItem,… |
| MNU-ARIA-16 | MUST | §5.9 | `Menu.Clear` renders `aria-hidden="true"` |
| MNU-ARIA-17 | MUST | §5.9 | Inside a Menubar, `Menu.Trigger` renders `role="menuitem"` (as a composite item of the bar) |
| MNU-ARIA-18 | MUST | §5.9 | The hidden `aria-owns` owner span (MNU-DOM-26) carries `role="group"` for submenus and Menubar menus and… |
| MNU-ARIA-19 | MUST | §5.9 | The accessible label of the Popup (plain menu, `role="menu"`), of the filter dialog, and of the List is… |
| MNU-KEY-01 | MUST | §6.1 | Trigger list-navigation options (`openOnArrowKeyDown`, `triggerOrientation`, `nested`); arrow-key opening per CMP-KEY-19/21–23; bar triggers open only on the bar open key (ArrowUp does nothing in a horizontal bar) |
| MNU-KEY-02 | MUST | §6.1 | Initial keyboard-open highlight per CMP-STA-37 (first, or last for the start key; skips attribute-disabled items) |
| MNU-KEY-03 | MUST | §6.1 | Enter or Space on a focused Trigger activates it like a click (native button |
| MNU-KEY-04 | MUST | §6.1 | List-navigation options for the open popup; main-axis arrows move highlight and focus per CMP-KEY-30/32 |
| MNU-KEY-05 | MUST | §6.1 | Home/End per CMP-KEY-28 with `disabledIndices: []` (reach disabled end items) |
| MNU-KEY-06 | MUST | §6.1 | Main-axis key with focus on non-item popup content enters the list at the boundary item (CMP-KEY-31) |
| MNU-KEY-07 | MUST | §6.1 | Arrow navigation includes `disabled`-prop items, skips hidden and natively `:disabled` items (CMP-ALG-02, CMP-KEY-51) |
| MNU-KEY-08 | MUST | §6.1 | Enter (keydown) and Space (keydown, composite mode |
| MNU-KEY-09 | MUST | §6.1 | Escape via the dismiss mechanism (FLT-KEY-01..03) closes the innermost open menu with `escape-key` |
| MNU-KEY-10 | MUST | §6.1 | Tab from inside an open top-level menu is not intercepted |
| MNU-KEY-11 | MUST | §6.1 | Shift+Tab in a plain popup closes with `focus-out` and focuses the trigger per CMP-KEY-24 |
| MNU-KEY-12 | MUST | §6.1 | Typeahead options (`resetMs: 500`, disabled under virtual focus); matching per CMP-KEY-35..43 |
| MNU-KEY-13 | MUST | §6.1 | While a typeahead session is in progress (`typingRef`), Space continues the session and MUST NOT activate… |
| MNU-KEY-14 | MUST | §6.1 | Typeahead is disabled while the Root is `disabled` (a typed letter neither highlights nor focuses) and in… |
| MNU-KEY-15 | MUST | §6.1 | Submenu open key on a SubmenuTrigger opens/re-enters the submenu per CMP-KEY-23 |
| MNU-KEY-16 | MUST | §6.1 | Submenu close key closes the submenu and focuses its trigger per CMP-KEY-27 |
| MNU-KEY-17 | MUST | §6.1 | Enter and Space on a focused `Menu.SubmenuTrigger` activate it as a click (`detail |
| MNU-KEY-18 | MUST | §6.1 | Keys on a submenu trigger that are neither the submenu open key nor an activation key are not consumed by… |
| MNU-KEY-19 | MUST | §6.1 | Escape inside a submenu closes only that submenu (reason `escape-key`) and focuses its trigger, leaving… |
| MNU-KEY-20 | MUST | §6.1 | Inside a `Toolbar` only, the popup stops propagation of arrow/Home/End keydowns (TLB-KEY-15 counterpart) |
| MNU-KEY-21 | MUST | §6.1 | A popup keydown whose propagation was not stopped is relayed to the keyboard relay published by a… |
| MNU-KEY-22 | MUST | §6.1.1 | `edit` keys are left to the input's native editing (propagation stopped so parent popups/typeahead never… |
| MNU-KEY-23 | MUST | §6.1.1 | `navigate` runs the menu's list-navigation key handler on the input |
| MNU-KEY-24 | MUST | §6.1.1 | In a horizontal filterable menu with nothing highlighted, ArrowDown highlights the first navigable item… |
| MNU-KEY-25 | MUST | §6.1.1 | Enter with a highlighted item (`activate`) default-prevents the keydown and dispatches a synthetic click… |
| MNU-KEY-26 | MUST | §6.1.1 | Shift+Tab in the input (`close`) stops the event, closes with reason `focus-out`, and (if not canceled)… |
| MNU-KEY-27 | MUST | §6.1.1 | A key that lands on the `Menu.List` or on an item that holds real focus (a scrollbar press focused the… |
| MNU-KEY-28 | MUST | §6.1.1 | While a filterable menu is open but its trigger still has focus (hover open), keys on the trigger are relayed |
| MNU-KEY-29 | MUST | §6.1.1 | Filterable submenu keyboard entry/exit |
| MNU-KEY-30 | MUST | §6.1.1 | Modified arrows, Home/End and characters are text-editing commands (`edit`) and never navigate, activate… |
| MNU-KEY-31 | MUST | §6.1.1 | Keydowns with `which === 229` (IME composition) are ignored by the filter key router, by the submenu… |
| MNU-KEY-32 | MUST | §6.1.1 | When a submenu closes on a key that is also a main-axis key of its parent (vertical submenu under a… |
| MNU-KEY-33 | MUST | §6.1.1 | A cross-axis *close* key pressed on a non-typeable focusable element inside a filterable popup (a custom… |
| MNU-KEY-34 | MUST | §6.1 | Root passes `disabledIndices: []`; arrows/Home/End reach `disabled`-prop items, the keyboard-open initial highlight skips them |
| MNU-PTR-01 | MUST | §6.2 | A primary-button `mousedown` on an enabled `Menu.Trigger` toggles the menu with reason `trigger-press`… |
| MNU-PTR-02 | MUST | §6.2 | Immediately after the `mousedown` that opens the menu, the Trigger records the open and starts a 200 ms timer |
| MNU-PTR-03 | MUST | §6.2 | When the pointer is pressed on the trigger, dragged into the popup and released on an item (`mouseup`… |
| MNU-PTR-04 | MUST | §6.2 | A document `mouseup` outside the popup (and outside the trigger) while the trigger is in the armed state… |
| MNU-PTR-05 | MUST | §6.2 | With `openOnHover`, resting the mouse on the Trigger for `delay` ms opens (`trigger-hover`); in a Menubar only while another bar menu is open |
| MNU-PTR-06 | MUST | §6.2 | A click within 500 ms of a hover open keeps a top-level menu open (`stickIfOpen`); `stickIfOpen` is `false` under any parent kind (Menubar) |
| MNU-PTR-07 | MUST | §6.2 | After a hover open, moving the pointer off the trigger closes the menu with reason `trigger-hover` after… |
| MNU-PTR-08 | MUST | §6.2 | Pointer-leave closing applies only to a hover-opened menu; `hoverEnabled` gating |
| MNU-PTR-09 | MUST | §6.2 | A `Menu.SubmenuTrigger` with `openOnHover` (default `true`) opens its submenu with reason `trigger-hover`… |
| MNU-PTR-10 | MUST | §6.2 | When the pointer leaves a hover-opened submenu popup (and is not inside its trigger or the safe polygon),… |
| MNU-PTR-11 | MUST | §6.2 | Hovering a *different* item of the parent while a hover-opened submenu is open emits a tree `itemhover` event |
| MNU-PTR-12 | MUST | §6.2 | With `highlightItemOnHover |
| MNU-PTR-13 | MUST | §6.2 | Pointer leaving an item for a non-item clears the highlight per CMP-PTR-05 (`resetOnPointerLeave`) |
| MNU-PTR-14 | MUST | §6.2 | `mousemove` over an item highlights and focuses it per CMP-PTR-04 (WebKit stationary moves ignored) |
| MNU-PTR-15 | MUST | §6.2 | A click on an enabled `Menu.Item` (any pointer type, or the dispatched click of MNU-KEY-08/MNU-PTR-03)… |
| MNU-PTR-16 | MUST | §6.2 | Touch taps and screen-reader presses (a `click` with `detail === 0` not preceded by keydown, or a… |
| MNU-PTR-17 | MUST | §6.2 | `Menu.SubmenuTrigger` installs `useClick` with `event |
| MNU-PTR-18 | MUST | §6.2 | Under a `ContextMenu` parent the Trigger behaviors are replaced by the context-menu trigger; Menu contributes anchoring, no arrow opening, modal focus, outside-press grace (non-`contextmenu` opens) |
| MNU-PTR-19 | MUST | §6.2 | In a filterable menu the Trigger opens on the same gestures as a plain menu, and the focus target depends… |
| MNU-PTR-20 | MUST | §6.2 | In a filterable menu every item part (Item, LinkItem, CheckboxItem, RadioItem, SubmenuTrigger) and the… |
| MNU-PTR-21 | MUST | §6.2 | Pressing `Menu.Clear` (click, Enter/Space via `useButton`) clears the query |
| MNU-PTR-22 | MUST | §6.2 | In a filterable menu, when real focus has wandered to the list (scrollbar press), to an item (screen… |
| MNU-FOC-01 | MUST | §6.3 | Opening by keyboard (arrow, Enter/Space, `detail === 0` click, or `openByTrigger` from a keyboard event)… |
| MNU-FOC-02 | MUST | §6.3 | Opening by mouse click or hover does not highlight an item |
| MNU-FOC-03 | MUST | §6.3 | Items use a roving `tabindex` |
| MNU-FOC-04 | MUST | §6.3 | When the menu closes, focus returns to the element that opened it |
| MNU-FOC-05 | MUST | §6.3 | With `keepMounted` on the Portal the return-focus behavior is identical (the popup is hidden, not unmounted) |
| MNU-FOC-06 | MUST | §6.3 | Focus guards |
| MNU-FOC-07 | MUST | §6.3 | Shift+Tab on the focused Trigger of an open menu (modal or not) closes the menu with reason `focus-out`… |
| MNU-FOC-08 | MUST | §6.3 | In a submenu, focus leaving the popup through its focus guards (Tab or Shift+Tab out of a keyboard-opened… |
| MNU-FOC-09 | MUST | §6.3 | `Menu.Popup`'s `finalFocus` prop overrides the return-focus target |
| MNU-FOC-10 | MUST | §6.3 | Non-modal menus (`modal |
| MNU-FOC-11 | MUST | §6.3 | When a nested Dialog (or any popup) opened from an item holds focus, pointer leave of the menu and the… |
| MNU-FOC-12 | MUST | §6.3 | `restoreFocus` |
| MNU-FOC-13 | MUST | §6.3 | Focus manager `modal: true` only under a ContextMenu parent; there Tab is trapped but Shift+Tab closes (`focus-out`) |
| MNU-FOC-14 | MUST | §6.3 | Filterable menu focus ownership |
| MNU-FOC-15 | MUST | §6.3 | Filterable submenu handoff (`useVirtualFocusParentHandoff`) |
| MNU-FOC-16 | MUST | §6.3 | Focusing the Trigger (programmatically or by click) while the menu is open does not seed a highlight |
| MNU-FOC-17 | MUST | §6.3 | With detached triggers (`Menu.createHandle()`), return focus targets the trigger that opened the menu… |
| MNU-DIS-01 | MUST | §6.4 | Outside press via the dismiss mechanism (FLT-DIS-01..11) closes with `outside-press` |
| MNU-DIS-02 | MUST | §6.4 | Escape closes the innermost menu (MNU-KEY-09) with reason `escape-key` |
| MNU-DIS-03 | MUST | §6.4 | `closeParentOnEsc` (SubmenuRoot, default `false`) |
| MNU-DIS-04 | MUST | §6.4 | Closing a menu closes every open descendant submenu |
| MNU-DIS-05 | MUST | §6.4 | `sibling-open` |
| MNU-DIS-06 | MUST | §6.4 | `item-press` |
| MNU-DIS-07 | MUST | §6.4 | `cancel-open` from the drag-release and post-hover `mouseup` listeners only (no Menubar-specific path) |
| MNU-DIS-08 | MUST | §6.4 | `focus-out` |
| MNU-DIS-09 | MUST | §6.4 | Scroll lock |
| MNU-DIS-10 | MUST | §6.4 | Under a ContextMenu, 500 ms outside-press grace after open, skipped when the open came from a `contextmenu` event |
| MNU-DIS-11 | MUST | §6.4 | Hover-close (pointer leaving trigger/popup) applies only to a menu whose current open was initiated by… |
| MNU-DIS-12 | MUST | §6.4 | When the Root is `disabled`, the menu cannot be opened by any user interaction (trigger press/hover/keyboard |
| MNU-DIS-13 | MUST | §6.4 | Reason `none` closes |
| MNU-EVT-01 | MUST | §7.1 | `onOpenChange(nextOpen, details)` is called synchronously inside the interaction that requests the change,… |
| MNU-EVT-02 | MUST | §7.1 | Calling `details.cancel()` inside `onOpenChange` aborts the change |
| MNU-EVT-03 | MUST | §7.1 | `details.preventUnmountOnClose()` (available only when `nextOpen` is `false` |
| MNU-EVT-04 | MUST | §7.1 | Each reason is produced only by the interactions listed in §7.1, with the listed event type, and… |
| MNU-EVT-05 | MUST | §7.1 | `item-press` closes carry the item's native `click` event |
| MNU-EVT-06 | MUST | §7.1 | `onOpenChangeComplete(open)` is called once per completed transition |
| MNU-EVT-07 | MUST | §7.1 | `Menu.SubmenuRoot`'s `onOpenChange` receives the same details shape with the submenu's reasons |
| MNU-EVT-08 | MUST | §7.1 | `Menu.CheckboxItem` calls `onCheckedChange(!checked, details)` on every activation (click, Enter/Space… |
| MNU-EVT-09 | MUST | §7.1 | `Menu.RadioGroup` calls `onValueChange(value, details)` when a `Menu.RadioItem` of the group is activated,… |
| MNU-EVT-10 | MUST | §7.1 | `onItemHighlighted(item, details)` reports the highlighted item element (`undefined` when the highlight is… |
| MNU-EVT-11 | MUST | §7.1 | The reason is the one attached to the store write that produced the committed `activeIndex` |
| MNU-EVT-12 | MUST | §7.1 | A clear (`item === undefined`) is reported when the pointer leaves the popup, when the menu closes with an… |
| MNU-EVT-13 | MUST | §7.1 | The callback is read through a stable reference |
| MNU-EVT-14 | MUST | §7.1 | Consumer DOM handlers on `Menu.Trigger` run before the internal ones (RND-EVT-01/02) |
| MNU-EVT-15 | MUST | §7.1 | On items, the consumer `onClick` runs before the close/toggle logic and `preventBaseUIHandler()` in it… |
| MNU-EVT-16 | MUST | §7.1 | `Menu.FilterProvider`'s `onValueChange(value, details)` is called with `reason |
| MNU-EVT-17 | MUST | §7.1 | Pressing `Menu.Clear` calls `onValueChange('', details)` with `reason |
| MNU-EVT-18 | MUST | §7.1 | When the popup closes (any reason) with a non-empty query, `onValueChange('', details)` is called once… |
| MNU-EVT-19 | MUST | §7.1 | During an IME composition (`compositionstart` … `compositionend`) the in-progress text is **not** reported… |
| — | — | §8 | Not applicable (no MNU-FRM requirements) |
| MNU-ANI-01 | MUST | §9 | The Positioner/Popup subtree is mounted while `mounted` is `true` |
| MNU-ANI-02 | MUST | §9 | `transitionStatus` follows TRN-STA-01 |
| MNU-ANI-03 | MUST | §9 | A menu that is open on its first render (`defaultOpen`, controlled `open |
| MNU-ANI-04 | MUST | §9 | `instantType` seeding |
| MNU-ANI-05 | SHOULD | §9 | Known limitation |
| MNU-ANI-06 | MUST | §9 | `onOpenChangeComplete(true)` fires after the popup's enter animations finish (`useOpenChangeComplete` on… |
| MNU-ANI-07 | MUST | §9 | `instantType` (projected as `data-instant="<type>"` on Positioner and Popup, MNU-DOM-04/05) is set on… |
| MNU-ANI-08 | MUST | §9 | Trigger switching (detached triggers, PSH-STA-39..42 / TRN-ANI-28..31) |
| MNU-ANI-09 | MUST | §9 | Re-opening during the exit phase (`'ending'`) cancels the exit |
| MNU-ANI-10 | MUST | §9 | `actionsRef.current.unmount()` forces the subtree to unmount immediately (skipping any running exit… |
| MNU-ANI-11 | MUST | §9 | `Menu.Backdrop` shares the Root's `transitionStatus` and does not start an entry phase when the menu is… |
| MNU-ANI-12 | MUST | §9 | While `transitionStatus === 'starting'` the Popup carries the disabled-mount-transition inline style… |
| MNU-ANI-13 | MUST | §9 | `Menu.CheckboxItemIndicator` and `Menu.RadioItemIndicator` run their own transition machine on the parent… |
| MNU-POS-01 | MUST | §10 | Positioner `anchor` (default: active trigger; ContextMenu: pointer anchor) resolved per FLT-API-11 |
| MNU-POS-02 | MUST | §10 | Positioner defaults passed to anchored positioning, by parent kind |
| MNU-POS-03 | MUST | §10 | Collision avoidance defaults (dropdown vs submenu) per FLT-API-10, FLT-POS-03..05 |
| MNU-POS-04 | MUST | §10 | ContextMenu: `shift` with layout-viewport root boundary; `fixed` position method inside any `ContextMenu.Root` |
| MNU-POS-05 | MUST | §10 | `lazyFlip: 'placement'` for a filterable menu, `false` otherwise (FLT-ANI-04) |
| MNU-POS-06 | MUST | §10 | Resolved placement as `data-side`/`data-align` on Positioner and Popup (FLT-DOM-08, FLT-POS-10) |
| MNU-POS-07 | MUST | §10 | `Menu.Arrow` reads `arrowRef`, `arrowStyles`, `side`, `align`, `arrowUncentered` from the Positioner… |
| MNU-POS-08 | MUST | §10 | The Positioner's inline style carries the engine's CSS variables `--anchor-width`, `--anchor-height`,… |
| MNU-POS-09 | MUST | §10 | Anchor tracking, `sticky`, `collisionBoundary` and the positioner element per FLT-POS-11/14, FLT-API-09, FLT-DOM-01 |
| MNU-POS-10 | MUST | §10 | When the active trigger changes while open (detached triggers), the popup re-anchors to the new trigger… |
| MNU-A11Y-01 | MUST | §13 | The VoiceOver `aria-expanded` omission (MNU-ARIA-13) is keyed on a platform screen-reader hint… |
| MNU-A11Y-02 | MUST | §13 | The WebKit `aria-selected` marker (MNU-ARIA-15) is derived from the store selector… |
| MNU-A11Y-03 | MUST | §13 | Screen-reader (virtual) presses on `Menu.SubmenuTrigger` are detected by `useClick` |
| MNU-I18N-01 | MUST | §6.5 | The reading direction (`DirectionProvider`, default `ltr`) flips the cross-axis submenu keys |
| MNU-I18N-02 | MUST | §6.5 | In a `horizontal` menu the main axis follows the reading direction |
| MNU-I18N-03 | MUST | §6.5 | `Menu.FilterProvider`'s `locale` (an `Intl.LocalesArgument`, default |
| MNU-EDGE-01 | MUST | §12 | `Menu.Trigger` rendered with neither an ancestor `Menu.Root` nor a `handle` throws `Error("Base UI:… |
| MNU-EDGE-02 | MUST | §12 | In development, a nested menu (any parent kind) that receives an explicit `modal` prop logs… |
| MNU-EDGE-03 | MUST | §12 | Context errors (verbatim) |
| MNU-EDGE-04 | MUST | §12 | `Menu.Input`, `Menu.Clear` and `Menu.Empty` rendered in a menu that is not wrapped in… |
| MNU-EDGE-05 | MUST | §12 | In development, a filterable menu whose popup opens without a mounted `Menu.Input` (no focus owner… |
| MNU-EDGE-06 | MUST | §12 | In development, a `Menu.SubmenuTrigger` whose rendered element is natively disabled (`disabled` attribute… |
| MNU-EDGE-07 | MUST | §12 | Handle misuse (PSH-API-08/09, messages verbatim with `componentName` = `Menu`) |
| MNU-EDGE-08 | MUST | §12 | React 17 id fallback / SSR: when `React.useId` is unavailable, ids are generated after hydration |
| MNU-EDGE-09 | MUST | §12 | Dynamic items |
| MNU-EDGE-10 | MUST | §12 | Shadow DOM: a submenu portaled into a shadow root still closes when focus leaves through its guard, and a… |
| MNU-EDGE-11 | — | §12 | Withdrawn: React-specific; moved to §14 |
| MNU-EDGE-12 | MUST | §12 | Stale timers and relays never reopen or re-close |
| MNU-EDGE-13 | MUST | §12 | Nested popups opened from an item (Dialog, AlertDialog, Popover) are part of the floating tree |
| MNU-EDGE-14 | MUST | §12 | Invariants |
| MNU-EDGE-15 | MUST | §12 | Plain `Menu.Root` inside a `ContextMenu.Root` inherits context-menu quirks (fixed positioning, macOS right-button release activation, backdrop registration) |
| MNU-EDGE-16 | MUST | §12 | Callback element substitution on Popup/filterable List must apply the handed `id`, or owner IDREFs (`aria-controls`) dangle |

Totals: STR 19, API 67, STA 25, DOM 29, ARIA 19, KEY 33, PTR 22, FOC 17, DIS 13, EVT 19, FRM 0, ANI 13, POS 10, A11Y 3, I18N 3, EDGE 14 — 306 requirements.

## 16. Open questions

- **Q1 — `close-press` is declared but never emitted.** `MenuRoot.ChangeEventReason` includes `close-press` (`menu/root/MenuRoot.tsx:933`), yet no Menu part dispatches it (Dialog/Popover/Combobox close buttons do). Should a port keep it in the union for API parity (as MNU-EVT-04 requires) or drop it? Confirmed by the event-details author; recommended: keep for compatibility, flag as reserved. Resolved (QA 2026-10-14): re-confirmed in source — `close-press` appears only in the union (`menu/root/MenuRoot.tsx:933`) and no file under `menu/`, `context-menu/`, `menubar/` or `filter-dropdown/` emits it; the doc says "declared only, never emitted" consistently (§3.28, §7, §7.1, MNU-EVT-04), and MNU-EVT-04 keeps it in the union for type compatibility.
- **Q2 — Doc comments vs behavior.** (a) `Menu.Trigger`'s `delay` is documented as "how long to wait before the menu may be opened on hover" but is implemented as a *rest* delay (`restMs`) for top-level triggers; `Menu.SubmenuTrigger`'s `delay` is a plain hover delay (MNU-PTR-05/09). (b) `closeParentOnEsc` is accepted on `Menu.Root` but only takes effect on a `SubmenuRoot` (MNU-API-11, MNU-DIS-03). (c) `Menu.SubmenuRoot` accepts `disabled`, `closeParentOnEsc`, `orientation` etc. per the shared Root props, but `modal` warns and is ignored. Should the port narrow the Root/SubmenuRoot prop types? Sources (second pass 2026-10-18): (a) `menu/trigger/MenuTrigger.tsx:184`; (b) `menu/root/MenuRoot.tsx:503`; (c) the `SubmenuRoot` props type omits `modal` (`menu/submenu-root/MenuSubmenuRoot.tsx:41-43`), and the dev warning "The `modal` prop is not supported on nested menus" fires for any Root with a parent kind (`menu/root/MenuRoot.tsx:207-213`).
- **Q3 — Outline (`docs/.../components/page.mdx` "## Menu") omissions.** The generic state projection emits `data-nested` and `data-instant` on both `Menu.Positioner` and `Menu.Popup` (MNU-DOM-04/06) and `data-disabled` on `Menu.RadioGroup` (MNU-DOM-29); `Menu.GroupLabel` accepts `id`; `Menu.Popup`/`Menu.Item`/`Menu.List` `id` props are marked `@ignore` but observable; the `nested` and `instant` fields of `Menu.Popup.State` are undocumented. The doc treats these as MUST because tests or the shared mapping produce them — confirm whether the port should document them as public. Source: `docs/src/app/(docs)/react/components/page.mdx:923` ("## Menu").
- **Q4 — `keepMounted` parent suppresses the submenu's enter transition.** Marked SHOULD (MNU-ANI-05). The source comments call it a known limitation; decide whether the port replicates it or fixes it (fixing changes `MenuPopupEnterTransition.test.tsx` "does not play the enter transition for a submenu when the parent popup is kept mounted"). Source: `menu/root/MenuRoot.tsx:139-147`.
- **Q5 — Drag-release comment vs behavior.** `useMenuItemCommonProps.ts` says a drag-release "overrides `closeOnClick`", but the dispatched click runs the normal click path, so `closeOnClick: false` still keeps the menu open (MNU-PTR-03 documents the observed behavior). Confirm the intended semantics. Source: `menu/item/useMenuItemCommonProps.ts:133-142`.
- **Q6 — Unreachable `useListNavigation` dev warnings.** `useListNavigation` warns in development when `virtual` is used without `aria-activedescendant` support or when `nested` is set without `parentOrientation`; Menu always satisfies both, so the warnings are unreachable through the public API and are not listed in §12. Confirm they need no port equivalent. Correction (second pass 2026-10-18): the dev warnings `useListNavigation` actually emits are "looping must be enabled to allow escaping" (`allowEscape` without `loopFocus`), "must be virtual to allow escaping" (`allowEscape` without `virtual`) and the grid-orientation warning (`floating-ui-react/hooks/useListNavigation.ts:270-287`); Menu passes `allowEscape: virtualFocus && loopFocus && allowEscape` and never grid mode (`menu/root/MenuRoot.tsx:528`), so all three remain unreachable and the conclusion stands.
- **Q7 — Lone-trigger `aria-controls`.** PSH-STA-10 makes a single contained trigger expose `aria-controls` even while closed; with several detached triggers only the active one does (MNU-ARIA-02). The docs page does not state this; confirm whether the port must match exactly or may always omit `aria-controls` while closed. Source: `menu/trigger/MenuTrigger.tsx:241`; see PSH-STA-10.
- **Q8 — FLT and CMP architecture docs.** `floating-and-positioning.md` (FLT) does not exist yet and `composite-and-list-navigation.md` (CMP) stops at §4.6, so §6.1 and §10 describe `useListNavigation`/`useAnchorPositioning` behavior inline with source citations. When those docs land, the inline descriptions in MNU-KEY-01..11, MNU-KEY-15/16, MNU-PTR-13/14 and MNU-POS-01..09 should be replaced by references to their IDs. Resolved (QA 2026-10-14): MNU-KEY-01/02/04/05/06/07/09/11/12/15/16, MNU-PTR-13/14, MNU-POS-01..09 and MNU-DIS-01 are now configuration requirements citing CMP/FLT IDs; hover, focus-manager and tree requirements gained FLT citations; MNU-KEY-34 states the `disabledIndices` value (CMP §10 Q10).
- **Q9 — Cross-axis close key under a ContextMenu.** The Root passes `nested: true` to list navigation for every plain menu with a parent kind (`menu/root/MenuRoot.tsx:521`), including a top-level menu under a ContextMenu, so the vertical close key (ArrowLeft in LTR, ArrowRight in RTL) on the context menu's popup would close it with reason `list-navigation` (`floating-ui-react/hooks/useListNavigation.ts:570-583`). No test covers this; confirm whether it is intended before porting. Mirrored (second pass 2026-10-18): context-menu.md CTX-KEY-07 specifies the behavior at SHOULD and its §16 Q13 carries the same question; CTX-EVT-01 no longer claims `list-navigation` is never emitted for the root.
- **Q10 — Focus target of a hover-opened plain menu.** A top-level plain popup passes `initialFocus` = `true` (or the `Menu.List` resolver) regardless of how it opened (`menu/popup/MenuPopup.tsx:84-97`), and the focus manager has no hover exception (`floating-ui-react/components/FloatingFocusManager.tsx:693-757`), so a hover open moves focus off the trigger into the popup; only the filterable popup returns `false` for a `trigger-hover` open (`menu/filter-root/MenuFilterPopup.tsx:27-48`). No test asserts the plain-menu focus target after a hover open (MNU-FOC-02 states the source behavior; the earlier text claimed focus stayed on the trigger). Confirm before porting. Decided (2026-10-18): replicated by the port (ADR 04 Amendments).
