# Navigation Menu

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/navigation-menu/` |
| Tests | `packages/react/src/navigation-menu/**/*.test.tsx` + `root/NavigationMenuRootContext.test.ts` + `root/NavigationMenuRoot.webkit.test.tsx` (15 files) + `root/NavigationMenuRoot.spec.tsx`, `link/NavigationMenuLink.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/navigation-menu/page.mdx` |
| Prefix | `NAV` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Navigation Menu is a site-navigation widget: a horizontal (or vertical) **List** of **Items**, each of which is either a plain **Link** or a **Trigger** that opens a panel of **Content**. Unlike Menu or Popover, there is a single shared popup for the whole menu: one **Portal → Positioner → Popup → Viewport** chain. Opening an item moves that item's Content element *into* the Viewport, re-anchors the Positioner to the item's Trigger, and resizes the Popup from the previous Content's size to the new one. Switching between Triggers while open is therefore a cross-fade: the old Content plays its exit transition in place (absolutely positioned at the Viewport's origin) while the new Content enters, and both carry a `data-activation-direction` attribute telling CSS which way the pointer moved.

The open state is the Root's `value`: the value of the Item whose content is shown, or `null` when closed. Items open on hover (after a 50 ms rest delay, with a 50 ms close delay and a safe-polygon pointer path to the popup) and on click or keyboard; a click within 500 ms of a hover-open does not close ("patient click"). Focus never moves into the popup automatically; Tab from the Trigger enters the popup through focus guards, and Tab out of the popup closes it. Nested menus are supported two ways: a nested Root with its own Portal/Positioner/Popup opens a second floating popup; a nested Root with only a List and a Viewport ("inline nesting") switches content inside the parent's panel.

The user experiences: `aria-expanded`/`aria-controls` on the active Trigger, `aria-current="page"` on the active Link, a Popup that stays mounted through its exit animation, `--popup-width/height` and `--positioner-width/height` CSS variables that drive size transitions, and a `data-instant` marker on the Positioner that suppresses its position transition for the first tick of an initially-open menu and for 100 ms after a window resize.

## 2. Anatomy

```
<NavigationMenu.Root>                 required, 1            default element: nav (div when nested)
  <NavigationMenu.List>               optional, 0..1 (1 expected)  default element: ul
    <NavigationMenu.Item>             optional, 0..n         default element: li
      <NavigationMenu.Trigger>        optional, 0..1 per Item  default element: button
        <NavigationMenu.Icon>         optional, 0..n         default element: span
      <NavigationMenu.Content>        optional, 0..n per Item  default element: div (moved into Viewport)
        <NavigationMenu.Link>         optional, 0..n         default element: a
        <NavigationMenu.Root>         optional (nested menu)
      <NavigationMenu.Link>           optional (top-level link item)
  <NavigationMenu.Portal>             optional, 0..1         default element: div (appended to <body>)
    <NavigationMenu.Backdrop>         optional, 0..1         default element: div
    <NavigationMenu.Positioner>       optional, 0..1         default element: div
      <NavigationMenu.Popup>          optional, 0..1         default element: nav
        <NavigationMenu.Arrow>        optional, 0..1         default element: div
        <NavigationMenu.Viewport>     optional, 0..1         default element: div
  <NavigationMenu.Viewport>           inline-nested form only: Viewport directly under Root, no Portal
```

Parts locate each other through ancestor-provided shared state (Root state, Item state, Portal state, Positioner state), not DOM adjacency. The Content part is *declared* inside its Item but *rendered* inside the Viewport (it is portaled there while active); the Item's `<li>` contains only the Trigger (and the Content only during server rendering with `keepMounted`, §9.6).

- **NAV-STR-01** — MUST. `NavigationMenu.Root` renders a `<nav>` when it is a top-level menu and a `<div>` when it is nested (NAV-STA-04). It owns the value/open state and provides the shared state for every other part; it attaches no event handlers of its own.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:80,310-323,356-360`. Test: [L2] `NavigationMenuRoot.test.tsx` describeConformance (`refInstanceof: HTMLElement`); "does not apply aria-orientation to nested lists or root elements" (nested root is a `div` carrying `data-testid`).
- **NAV-STR-02** — MUST. `NavigationMenu.List` renders a `<ul>` and MUST be a descendant of a Root (otherwise NAV-EDGE-01). At the top level it is a roving-tabindex composite root (§6.1); when nested it is a plain element and its Triggers participate in the enclosing Content's composite group instead.
  Source: `navigation-menu/list/NavigationMenuList.tsx:29-40,96-125`. Test: [L2] `NavigationMenuList.test.tsx` describeConformance (`refInstanceof: HTMLUListElement`), "throws a descriptive error when rendered outside <NavigationMenu.Root>".
- **NAV-STR-03** — MUST. `NavigationMenu.Item` renders an `<li>` and provides its `value` (explicit or generated, NAV-STA-10) to its descendants. It does not require a Root ancestor itself; Trigger, Icon and Content require an Item ancestor (NAV-EDGE-02).
  Source: `navigation-menu/item/NavigationMenuItem.tsx:19-35`; `navigation-menu/item/NavigationMenuItemContext.ts:12-19`. Test: [L2] `NavigationMenuItem.test.tsx` describeConformance (`refInstanceof: HTMLLIElement`); `NavigationMenuIcon.test.tsx` "throws a descriptive error when rendered outside <NavigationMenu.Item>".
- **NAV-STR-04** — MUST. `NavigationMenu.Trigger` renders a `<button>` and MUST be inside an Item inside a Root. While it is the active trigger it additionally renders, as following siblings in this order: a focus guard, a visually hidden `<span aria-owns>` owner element, and a second focus guard (NAV-DOM-10).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:719-784`. Test: [L2] `NavigationMenuTrigger.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`, `testRenderPropWith: 'button'`); `NavigationMenuRoot.test.tsx` "returns focus to the trigger when no viewport focus guard is rendered" (two `[data-base-ui-focus-guard]` siblings).
- **NAV-STR-05** — MUST. `NavigationMenu.Icon` renders a `<span>` whose default children are the text `▼`; it MUST be inside an Item inside a Root. It is structural only (state attributes, no handlers).
  Source: `navigation-menu/icon/NavigationMenuIcon.tsx:29-34`. Test: [L2] `NavigationMenuIcon.test.tsx` describeConformance (`refInstanceof: HTMLSpanElement`).
- **NAV-STR-06** — MUST. `NavigationMenu.Content` renders a `<div>` that is a composite root for the links and nested triggers inside it. It MUST be inside an Item inside a Root. Its element is rendered inside the Viewport's target container (never inside the `<li>`) except in the server-rendered `keepMounted` case (NAV-ANI-16); when no Viewport exists and the content is not mounted, nothing is rendered.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:135-174`. Test: [L4] `NavigationMenuContent.test.tsx` "moves content into the popup and keeps it there when switching triggers"; describeConformance is skipped for this part (§16 Q1).
- **NAV-STR-07** — MUST. `NavigationMenu.Link` renders an `<a>` and MUST be inside a Root; it is a composite item and therefore MUST also be inside a composite root (a top-level List or a Content), otherwise CMP-STR-02 applies.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:33,66-76`; `internals/composite/item/CompositeItem.tsx:25`. Test: [L2] `NavigationMenuLink.test.tsx` describeConformance (`refInstanceof: HTMLAnchorElement`, rendered inside `Root > List`).
- **NAV-STR-08** — MUST. `NavigationMenu.Portal` renders a `<div>` portal container appended to `<body>` (or to `container`) and MUST be inside a Root. It renders nothing while the popup is not mounted unless `keepMounted` (NAV-ANI-01). Positioner MUST be inside a Portal (NAV-EDGE-03).
  Source: `navigation-menu/portal/NavigationMenuPortal.tsx:19-32`; `floating-ui-react/components/FloatingPortal.tsx:104-141`. Test: [L2] `NavigationMenuPortal.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`, Root `value="item"`) (L4 for the `<body>`/`container` placement clause).
- **NAV-STR-09** — MUST. `NavigationMenu.Backdrop` renders a `<div>` and requires only a Root ancestor (it may be placed inside or outside the Portal).
  Source: `navigation-menu/backdrop/NavigationMenuBackdrop.tsx:21-43`. Test: [L2] `NavigationMenuBackdrop.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`, rendered directly under Root).
- **NAV-STR-10** — MUST. `NavigationMenu.Positioner` renders a `<div>` and MUST be inside a Portal inside a Root; it provides positioning state to Popup, Arrow and Viewport.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:72,170-183`. Test: [L2] `NavigationMenuPositioner.test.tsx` describeConformance, "throws a descriptive error when rendered outside <NavigationMenu.Portal>".
- **NAV-STR-11** — MUST. `NavigationMenu.Popup` renders a `<nav>` and MUST be inside a Positioner (NAV-EDGE-04).
  Source: `navigation-menu/popup/NavigationMenuPopup.tsx:27,49`. Test: [L2] `NavigationMenuPopup.test.tsx` describeConformance (`refInstanceof: HTMLElement`).
- **NAV-STR-12** — MUST. `NavigationMenu.Viewport` renders a `<div>` and MUST be inside a Root. A Positioner ancestor is optional: with one, the Viewport is the clipping container inside the Popup; without one (inline nested menu) the Viewport renders an extra inner `<div>` target wrapper around its children and is itself the floating element for hover and sizing purposes.
  Source: `navigation-menu/viewport/NavigationMenuViewport.tsx:89-91,99-131`. Test: [L2] `NavigationMenuViewport.test.tsx` describeConformance (rendered directly under Root); `NavigationMenuRoot.test.tsx` "renders viewport content correctly for inline nested menu".
- **NAV-STR-13** — MUST. `NavigationMenu.Arrow` renders a `<div>` and MUST be inside a Positioner (NAV-EDGE-04).
  Source: `navigation-menu/arrow/NavigationMenuArrow.tsx:24-25,34-43`. Test: [L2] `NavigationMenuArrow.test.tsx` describeConformance, "throws a descriptive error when rendered outside <NavigationMenu.Positioner>".
- **NAV-STR-14** — MUST. A Root rendered inside another Root's Content is a *nested* menu. Two forms are supported: (a) **portaled** — the nested Root has its own Portal/Positioner/Popup/Viewport and opens a second floating popup anchored to the nested Trigger; (b) **inline** — the nested Root has only a List and a Viewport (no Portal), and its Content switches inside the parent's panel. Nesting depth is unbounded (three levels are tested).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:80-81,318-321`; `navigation-menu/viewport/NavigationMenuViewport.tsx:119-125`. Test: [L4] `NavigationMenuRoot.test.tsx` "opens nested menu on hover and stays open when hovering over nested popup" (portaled), "handles inline nested menu without positioner/popup correctly" (inline), "allows arrow key navigation with 3+ levels of nesting". Docs: `components/navigation-menu/page.mdx` §Nested submenus, §Nested inline submenus.

## 3. Public API

### 3.1 `NavigationMenu.Root`

**Default element:** `<nav>` (`<div>` when nested). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Root.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value \| null \| undefined` | `undefined` (uncontrolled) | Consumer-owned value of the open item; non-nullish = open, `null` = closed. | NAV-API-01 |
| `defaultValue` | `Value \| null` | `null` | Initial component-owned value. | NAV-API-02 |
| `onValueChange` | `(value: Value \| null, eventDetails: NavigationMenu.Root.ChangeEventDetails) => void` | `undefined` | Change request callback (§7). | NAV-API-03 |
| `actionsRef` | `RefObject<NavigationMenu.Root.Actions \| null>` | `undefined` | Receives `{ unmount(), close() }` (§7, §9). | NAV-API-04 |
| `onOpenChangeComplete` | `(open: boolean) => void` | `undefined` | Called with `false` after the closing animation completes; never called with `true` (NAV-EVT-07). | NAV-API-05 |
| `delay` | `number` | `50` | Hover rest delay in ms before opening from closed. | NAV-API-06 |
| `closeDelay` | `number` | `50` | Delay in ms before closing after the pointer leaves. | NAV-API-07 |
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Arrow-key axis of the List and the open key of Triggers; also selects the activation-direction axis. | NAV-API-08 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<nav>` attribute | | — | Forwarded to the rendered element. | — |

**Consumed (not forwarded to the element):** `value`, `defaultValue`, `onValueChange`, `actionsRef`, `delay`, `closeDelay`, `orientation`, `onOpenChangeComplete`.
Source: `navigation-menu/root/NavigationMenuRoot.tsx:333-346`.

- **NAV-API-01** — MUST. `value` is the consumer-owned value. When it is not `undefined` on the first render the component never changes the value itself; it only reports requests through `onValueChange` (UTL-STA-01, UTL-STA-04). A `null` value means closed; any other value (including `0`, `''`, `false`) means open with that item (NAV-STA-03).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:83-91,397-403`; see [shared-utilities](../architecture/shared-utilities.md) UTL-STA-01…04. Test: [L2] `NavigationMenuRoot.test.tsx` "should be controlled by value prop".
- **NAV-API-02** — MUST. `defaultValue` defaults to `null` and is the initial value of the component-owned state; later changes are ignored (UTL-STA-05).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:70,83-88,404-410`. Test: [L2] `NavigationMenuRoot.test.tsx` "should respect defaultValue".
- **NAV-API-03** — MUST. `onValueChange` is optional; when absent, interactions still change the component-owned value (uncontrolled) or do nothing (controlled).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:214-216`. Test: [L2] `NavigationMenuRoot.test.tsx` "opens on click with mouse input" (no callback).
- **NAV-API-04** — MUST. `actionsRef`, when supplied, receives an object `{ unmount(): void; close(): void }` whose members are specified by NAV-EVT-08 and NAV-EVT-09.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:245-252,384-391,434-437`. Test: [L2] `NavigationMenuRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once" (`unmount` is a function).
- **NAV-API-05** — MUST. `onOpenChangeComplete` is optional and is invoked only as `onOpenChangeComplete(false)` (NAV-EVT-07).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:151,392-395`. Test: [L2] `NavigationMenuRoot.test.tsx` "closes on the short exit path after switching content" (`calls` equal `[[false]]`).
- **NAV-API-06** — MUST. `delay` defaults to `50` and is the rest delay (NAV-PTR-01) used when the popup is not yet mounted or there is no Positioner.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:74,417-421`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:526`; `navigation-menu/utils/constants.ts:1`. Test: [L4] `NavigationMenuRoot.test.tsx` "respects custom delay value".
- **NAV-API-07** — MUST. `closeDelay` defaults to `50` and is the hover close delay of both the Trigger and the popup (NAV-PTR-08).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:75,422-426`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:527`; `navigation-menu/list/NavigationMenuList.tsx:50`; `navigation-menu/utils/constants.ts:2`. Test: [L4] `NavigationMenuRoot.test.tsx` "respects custom closeDelay value".
- **NAV-API-08** — MUST. `orientation` defaults to `'horizontal'`. It is passed as the top-level List's composite orientation (NAV-KEY-05), selects the Trigger's open key (NAV-KEY-01/02), the List's propagation guard (NAV-KEY-06) and the activation-direction axis (NAV-STA-06). It never produces an `aria-orientation` attribute (NAV-ARIA-07).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:76,427-431`; `navigation-menu/list/NavigationMenuList.tsx:121`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:564-568,685-686`. Test: [L4] `NavigationMenuRoot.test.tsx` "reports %s activation direction when switching in either direction"; `NavigationMenuList.test.tsx` "stops vertical navigation keys from escaping the list".

#### State object (`NavigationMenu.Root.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | `value != null` (NAV-STA-02). |
| `nested` | `boolean` | Whether this Root is nested inside another Root's floating tree (NAV-STA-04). |

Source: `navigation-menu/root/NavigationMenuRoot.tsx:351-354,369-378`.

#### Data attributes

None are defined for the Root (the state fields `open` and `nested` are exposed to `className`/`style`/`render` only through the default mapping, NAV-DOM-01).

#### CSS variables

None.

### 3.2 `NavigationMenu.List`

**Default element:** `<ul>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.List.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | | — |
| any other `<ul>` attribute / handler | | — | Forwarded; `onKeyDown` is chained after the composite handler. | — |

#### State object (`NavigationMenu.List.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root `open`. |

Source: `navigation-menu/list/NavigationMenuList.tsx:69-71,128-133`.

#### Data attributes

None defined (`open` is mapped only by the default rule, NAV-DOM-04).

#### CSS variables

None.

### 3.3 `NavigationMenu.Item`

**Default element:** `<li>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `{}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` | generated id | Identifies this item; the Root's `value` equals it while the item is open. | NAV-API-11 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<li>` attribute | | — | Forwarded. | — |

**Consumed:** `value`.

- **NAV-API-11** — MUST. `value` defaults to a generated document-unique id of the form `base-ui-<token>` (RND-DOM-27) computed once per Item instance; `null`/`undefined` falls back to the generated id, any other value (including falsy ones) is used as-is.
  Source: `navigation-menu/item/NavigationMenuItem.tsx:21-22`; `internals/useBaseUiId.ts:9-11`. Test: [L2] `NavigationMenuRoot.test.tsx` "treats a falsy item value (%s) as a valid open value"; `NavigationMenuTrigger.test.tsx` "handles focus and positioner height" (items without `value`).

#### State object (`NavigationMenu.Item.State`)

Empty object `{}`.
Source: `navigation-menu/item/NavigationMenuItem.tsx:24-27,38`.

#### Data attributes

None.

#### CSS variables

None.

### 3.4 `NavigationMenu.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>`. | NAV-API-09 |
| `disabled` | `boolean` | `false` | Ignores every opening interaction; the element stays focusable. | NAV-API-10 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; handlers are chained after the component's own handlers (RND handler merging). | — |

**Consumed:** `nativeButton`, `disabled`.

- **NAV-API-09** — MUST. `nativeButton` defaults to `true` and is passed as the `native` option of the shared button mechanism (§11): `true` → `type="button"`, `false` → `role="button"` with keyboard activation emulation.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:77,711-715`; see [button-behavior](../architecture/button-behavior.md) UBT-API-02, UBT-DOM-01/02. Test: [L2] `NavigationMenuTrigger.test.tsx` describeConformance (`button: true`, non-native re-run).
- **NAV-API-10** — MUST. `disabled` defaults to `false`. While `true`: hover, click, touch and keyboard opening are all suppressed and `onValueChange` is never called (NAV-EDGE-06); the trigger remains in the tab order (`focusableWhenDisabled: true`, no `disabled` attribute, `aria-disabled="true"`, NAV-ARIA-03) and renders `data-disabled` (NAV-DOM-08).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:78,128-130,438-440,620-622,711-715`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not open on hover/click/touch/via keyboard when the trigger is disabled" (four tests); `NavigationMenuTrigger.test.tsx` "applies the data-disabled style hook only when disabled".

#### State object (`NavigationMenu.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | `true` only while the Root is open **and** this trigger's Item is the active value (NAV-STA-11). |
| `disabled` | `boolean` | The `disabled` prop. |

Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:126,642-645,787-796`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open` | `open === true`; empty value | NAV-DOM-07 | C# |
| `data-pressed` | `open === true`; empty value (same condition as `data-popup-open`) | NAV-DOM-07 | C# |
| `data-disabled` | `disabled === true` | NAV-DOM-08 | C# |
| `data-base-ui-navigation-menu-trigger` | always; empty value (internal marker, not in the outline, §16 Q2) | NAV-DOM-09 | C# |

#### CSS variables

None.

### 3.5 `NavigationMenu.Icon`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Icon.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | | — |
| `children` | node | `'▼'` | Replaces the default glyph. | NAV-DOM-12 |
| any other `<span>` attribute | | — | Forwarded; a consumer `aria-hidden` overrides the default. | — |

#### State object (`NavigationMenu.Icon.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root open **and** this Item is the active value. |

Source: `navigation-menu/icon/NavigationMenuIcon.tsx:20-27,39-44`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open` | `open === true`; empty value | NAV-DOM-13 | C# |

#### CSS variables

None.

### 3.6 `NavigationMenu.Content`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes (through the composite root, CMP-STR-05). **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Content.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the content element in the DOM (hidden) while it is not the active content; ensures presence in server-rendered markup. | NAV-API-12 |
| `className`, `style`, `render` | per RND | — | `style` is merged after the component's exiting-position styles (NAV-DOM-17). | — |
| any other `<div>` attribute / handler | | — | Forwarded; `hidden` from the consumer overrides the component's. | — |

**Consumed:** `keepMounted`.

- **NAV-API-12** — MUST. `keepMounted` defaults to `false`. When `false`, the content element exists only while its own transition state is mounted (open, or exiting) and only inside the Viewport. When `true`, the element is always present: inside the Viewport once a Viewport target exists, otherwise inline in the Item with `hidden` (NAV-ANI-16, NAV-DOM-16).
  Source: `navigation-menu/content/NavigationMenuContent.tsx:49,135-159,196-201`. Test: [L4] `NavigationMenuContent.test.tsx` "keeps the content mounted (hidden) in the DOM when keepMounted is true", "does not keep the content mounted in the DOM when keepMounted is false", "keeps the content mounted (hidden) post-hydration when keepMounted is true", "does not keep the content mounted post-hydration when keepMounted is false". Docs: CHANGELOG.md v1.0.0-rc.0 "Add `keepMounted` prop to `Content` part (#3794)".

#### State object (`NavigationMenu.Content.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Popup mounted **and** this Item is the active value (NAV-STA-11). |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | The Content's own transition status (NAV-STA-12); `'idle'` never occurs. |
| `activationDirection` | `'left' \| 'right' \| 'up' \| 'down' \| null` | Direction from the previously active trigger to the current one while open, else `null` (NAV-STA-06). |

Source: `navigation-menu/content/NavigationMenuContent.tsx:97-101,177-190`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` | `open === true` | NAV-DOM-14 | C# |
| `data-closed` | `open === false` | NAV-DOM-14 | C# |
| `data-starting-style` | `transitionStatus === 'starting'` | NAV-DOM-15 | JS |
| `data-ending-style` | `transitionStatus === 'ending'` | NAV-DOM-15 | JS |
| `data-activation-direction` | `activationDirection` non-null; value is the direction string | NAV-DOM-18 | C# |
| `hidden` | `keepMounted && !mounted` (boolean attribute) | NAV-DOM-16 | C# |

#### CSS variables

None.

### 3.7 `NavigationMenu.Link`

**Default element:** `<a>`. **Accepts `render` element substitution:** yes (the render callback receives the native `<a>` attributes, e.g. `href`). **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Link.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `active` | `boolean` | `false` | Marks the link as the current page. | NAV-API-13 |
| `closeOnClick` | `boolean` | `false` | Close the menu when the link is clicked. | NAV-API-14 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<a>` attribute / handler (`href`, …) | | — | Forwarded; `onClick`/`onBlur` chained. | — |

**Consumed:** `active`, `closeOnClick`.

- **NAV-API-13** — MUST. `active` defaults to `false`; `true` renders `aria-current="page"` (NAV-ARIA-04) and `data-active` (NAV-DOM-20). It has no behavioral effect.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:27,37-42,91-95`. Test: [L2] `NavigationMenuLink.test.tsx` "when `true`, renders with aria-current=\"page\"", "when `false`, does not render with aria-current=\"page\"".
- **NAV-API-14** — MUST. `closeOnClick` defaults to `false`; when `true` a click on the link requests a close with reason `link-press` (NAV-EVT-02, NAV-DIS-09).
  Source: `navigation-menu/link/NavigationMenuLink.tsx:28,44-48,96-100`. Test: [L2] `NavigationMenuLink.test.tsx` "closes the menu when clicking a link when true", "does not close the menu when clicking a link when false". Docs: CHANGELOG.md v1.0.0-beta.3 "Make link close on click configurable (#2740)".

#### State object (`NavigationMenu.Link.State`)

| Field | Type | Meaning |
|---|---|---|
| `active` | `boolean` | The `active` prop. |

Source: `navigation-menu/link/NavigationMenuLink.tsx:37-39,79-84`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-active` | `active === true`; empty value | NAV-DOM-20 | C# |

#### CSS variables

None.

### 3.8 `NavigationMenu.Portal`

**Default element:** `<div>` (the portal container). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `{}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the portal (and everything in it) in the DOM while the popup is not mounted. | NAV-API-15 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject<…> \| null` | `<body>` of the current document | Parent to render the portal container into. | NAV-API-16 |
| `className`, `style`, `render` | per RND | — | Applied to the portal container element. | — |
| any other `<div>` attribute | | — | Forwarded to the portal container. | — |

**Consumed:** `keepMounted`, `container`.

- **NAV-API-15** — MUST. `keepMounted` defaults to `false`. The Portal (and its subtree) is rendered iff `mounted || keepMounted` (NAV-ANI-01). The value is also provided to the Positioner, which uses it as the `keepMounted` option of anchor positioning (NAV-POS-06).
  Source: `navigation-menu/portal/NavigationMenuPortal.tsx:19-32`; `navigation-menu/positioner/NavigationMenuPositioner.tsx:72,123`. Test: [L2] `NavigationMenuContent.test.tsx` "keeps content mounted inside the popup when closed if the portal is kept mounted"; `NavigationMenuRoot.test.tsx` "preserves popup size when controlled value closes externally with keepMounted portal".
- **NAV-API-16** — MUST. The Portal passes `container` (default `undefined`) and its element props to the shared portal mechanism; the observable result is per FLT-API-13 (omitted/`undefined` → enclosing portal node or `<body>`; `null` → nothing rendered until a container is supplied) and FLT-DOM-11 (portal container `<div>` with a generated `id` and `data-base-ui-portal=""`).
  Source: `navigation-menu/portal/NavigationMenuPortal.tsx:19-32`; `floating-ui-react/components/FloatingPortal.tsx:93-134,163-179`; see FLT-API-13, FLT-DOM-11. Test: none (NavigationMenu-specific); see FLT-DOM-11.

#### State object

Empty object `{}`. Source: `navigation-menu/portal/NavigationMenuPortal.tsx:35`.

#### Data attributes / CSS variables

None of its own (NAV-DOM-22 for the portal container marker).

### 3.9 `NavigationMenu.Backdrop`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Backdrop.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | `style` is merged after the component's `user-select: none` (NAV-DOM-24). | — |
| any other `<div>` attribute | | — | Forwarded; a consumer `role`/`hidden` overrides the component's. | — |

#### State object (`NavigationMenu.Backdrop.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root open. |
| `transitionStatus` | `TransitionStatus` | Root (popup) transition status (NAV-STA-05). |

Source: `navigation-menu/backdrop/NavigationMenuBackdrop.tsx:21-26,48-57`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | NAV-DOM-23 | C# |
| `data-starting-style` / `data-ending-style` | Root status `'starting'` / `'ending'` | NAV-DOM-23 | JS |
| `hidden` | `!mounted` | NAV-DOM-24 | C# |

#### CSS variables

None.

### 3.10 `NavigationMenu.Positioner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Positioner.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `anchor` | `Element \| VirtualElement \| RefObject \| (() => …) \| null` | the active Trigger element | Element to position against. | NAV-POS-02 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | CSS position used. | NAV-POS-01 |
| `side` | `Side` | `'bottom'` | Preferred side. | NAV-POS-01 |
| `align` | `'start' \| 'center' \| 'end'` | `'center'` | Alignment. | NAV-POS-01 |
| `sideOffset` | `number \| OffsetFunction` | `0` | | NAV-POS-01 |
| `alignOffset` | `number \| OffsetFunction` | `0` | | NAV-POS-01 |
| `collisionBoundary` | `Boundary` | `'clipping-ancestors'` | | NAV-POS-01 |
| `collisionPadding` | `Padding` | `5` | | NAV-POS-01 |
| `collisionAvoidance` | `CollisionAvoidance` | `{ fallbackAxisSide: 'none' }` top-level; `{ fallbackAxisSide: 'end' }` nested | | NAV-POS-05 |
| `arrowPadding` | `number` | `5` | | NAV-POS-01 |
| `sticky` | `boolean` | `false` | | NAV-POS-01 |
| `disableAnchorTracking` | `boolean` | `false` | | NAV-POS-01 |
| `className`, `style`, `render` | per RND | — | `style` is merged after the computed position styles. | — |
| any other `<div>` attribute | | — | Forwarded; a consumer `role`/`hidden` overrides the component's. | — |

**Consumed:** all positioning props above.
Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:53-70`.

#### State object (`NavigationMenu.Positioner.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root open. |
| `side` | `Side` | Resolved side after collision handling. |
| `align` | `Align` | Resolved alignment. |
| `anchorHidden` | `boolean` | Anchor scrolled out of view. |
| `instant` | `boolean` | Position transitions are suppressed (NAV-ANI-13/14). |

Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:133-139,186-207`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | NAV-DOM-27 | C# |
| `data-side` | resolved side (`top`, `bottom`, `left`, `right`, `inline-start`, `inline-end`) | NAV-DOM-27 | C# → JS |
| `data-align` | resolved align (`start`, `center`, `end`) | NAV-DOM-27 | C# → JS |
| `data-anchor-hidden` | `anchorHidden === true` | NAV-DOM-27 | JS |
| `data-instant` | `instant === true`; empty value | NAV-DOM-28 | C# |
| `hidden` | `!mounted` | NAV-DOM-26 | C# |

#### CSS variables

| Variable | Set on | Value | Req | Writer |
|---|---|---|---|---|
| `--anchor-width`, `--anchor-height` | Positioner inline style | anchor dimensions in px | NAV-POS-07 | C# → JS |
| `--available-width`, `--available-height` | Positioner inline style | space to the viewport edge in px | NAV-POS-07 | C# → JS |
| `--transform-origin` | Positioner inline style | origin string | NAV-POS-07 | C# → JS |
| `--positioner-width`, `--positioner-height` | Positioner inline style (written imperatively by the active Trigger / Root) | `<n>px` final content size; removed while re-measuring | NAV-DOM-29 | JS |

### 3.11 `NavigationMenu.Popup`

**Default element:** `<nav>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Popup.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated `base-ui-<token>` | Element id; referenced by the active Trigger's `aria-controls`. | NAV-API-17 |
| `className`, `style`, `render` | per RND | — | `style` merged after the component's origin-side styles. | — |
| any other `<nav>` attribute | | — | Forwarded; a consumer `tabIndex` overrides `-1`. | — |

**Consumed:** `id` (re-emitted, possibly generated).

- **NAV-API-17** — MUST. `id` defaults to a generated `base-ui-<token>` (RND-DOM-27); a consumer id is used verbatim. The rendered id is what the active Trigger's `aria-controls` references (NAV-ARIA-02).
  Source: `navigation-menu/popup/NavigationMenuPopup.tsx:24,30,54`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:663`. Test: none directly (conformance renders the part; `aria-controls` is not asserted, §16 Q3).

#### State object (`NavigationMenu.Popup.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root open. |
| `transitionStatus` | `TransitionStatus` | Root (popup) status. |
| `side` | `Side` | From the Positioner. |
| `align` | `Align` | From the Positioner. |
| `anchorHidden` | `boolean` | From the Positioner. |

Source: `navigation-menu/popup/NavigationMenuPopup.tsx:32-38,73-94`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | NAV-DOM-32 | C# |
| `data-starting-style` / `data-ending-style` | status `'starting'` / `'ending'` | NAV-DOM-32 | JS |
| `data-side`, `data-align` | resolved placement | NAV-DOM-32 | C# |
| `data-anchor-hidden` | `anchorHidden` | NAV-DOM-32 | C# |

#### CSS variables

| Variable | Set on | Value | Req | Writer |
|---|---|---|---|---|
| `--popup-width`, `--popup-height` | Popup inline style (imperative) | `<n>px` during a size transition, `auto` once settled, removed while re-measuring | NAV-DOM-33 | JS |

### 3.12 `NavigationMenu.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `{}`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated (unprefixed) | Element id; referenced by the active Trigger's `aria-owns` owner element. | NAV-API-18 |
| `className`, `style`, `render` | per RND | — | | — |
| `children` | node | — | Normally empty: active Content elements are portaled into the Viewport (or into its inner target wrapper when there is no Positioner). | NAV-STR-12 |
| any other `<div>` attribute / handler | | — | Forwarded; `onBlur` chained. | — |

- **NAV-API-18** — MUST. The Viewport's `id` defaults to a generated document-unique id (without the `base-ui-` prefix) and is the target of the active Trigger's `aria-owns` (NAV-ARIA-06).
  Source: `navigation-menu/viewport/NavigationMenuViewport.tsx:76-78,103`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:750`. Test: none.

#### State object

Empty object `{}`. Source: `navigation-menu/viewport/NavigationMenuViewport.tsx:134`.

#### Data attributes / CSS variables

None of its own; `inert` per NAV-DOM-35.

### 3.13 `NavigationMenu.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `NavigationMenu.Arrow.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | `style` merged after the computed arrow position styles. | — |
| any other `<div>` attribute | | — | Forwarded; consumer `aria-hidden` overrides. | — |

#### State object (`NavigationMenu.Arrow.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Root open. |
| `side` | `Side` | From the Positioner. |
| `align` | `Align` | From the Positioner. |
| `uncentered` | `boolean` | The arrow could not be centered on the anchor. |

Source: `navigation-menu/arrow/NavigationMenuArrow.tsx:23-32,48-65`.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | NAV-DOM-37 | C# |
| `data-side`, `data-align` | resolved placement | NAV-DOM-37 | C# → JS |
| `data-uncentered` | `uncentered === true` | NAV-DOM-37 | JS |

#### CSS variables

None.

### 3.14 Shared types

- `NavigationMenu.Root.Value<TValue = any>` = `TValue | null`. The Root is generic in `Value`; without an explicit type argument the value type is `any`. Source: `navigation-menu/root/NavigationMenuRoot.tsx:65-66,324-326,459`. Test: [L1] `NavigationMenuRoot.spec.tsx` (type assertions: explicit generic narrows `onValueChange`'s parameter to `'a' | 'b' | null`; mismatched `value` is a type error).
- `NavigationMenu.Root.ChangeEventReason` = `'trigger-press' | 'trigger-hover' | 'outside-press' | 'list-navigation' | 'focus-out' | 'escape-key' | 'link-press' | 'imperative-action' | 'none'`. Source: `navigation-menu/root/NavigationMenuRoot.tsx:439-448`; `internals/reason-parts.ts:1,3,4,7,10,23,24,26,40`.
- `NavigationMenu.Root.ChangeEventDetails` = the common change-event details ([event-details-and-reasons](../architecture/event-details-and-reasons.md) EVD-API-01) specialized to those reasons, plus `preventUnmountOnClose(): void` (EVD-API-09). Source: `navigation-menu/root/NavigationMenuRoot.tsx:450-454`.
- `NavigationMenu.Root.Actions` = `{ unmount: () => void; close: () => void }`. Source: `navigation-menu/root/NavigationMenuRoot.tsx:434-437`.
- `NavigationMenu.Root.Props<TValue>`, `NavigationMenu.Root.State`, and `Props`/`State` for List, Item, Trigger, Icon, Content, Link, Portal, Backdrop, Positioner, Popup, Viewport, Arrow, as above. Flat aliases (`NavigationMenuRootProps`, …) are also exported, along with the attribute-name namespaces `NavigationMenuContentDataAttributes`, `NavigationMenuTriggerDataAttributes`, `NavigationMenuPositionerCssVariables`, `NavigationMenuPositionerDataAttributes`, `NavigationMenuBackdropDataAttributes`, `NavigationMenuPopupCssVariables`, `NavigationMenuPopupDataAttributes`, `NavigationMenuArrowDataAttributes`, `NavigationMenuLinkDataAttributes`, `NavigationMenuIconDataAttributes`. Source: `navigation-menu/index.ts:1-26`. Test: [L2] `NavigationMenuLink.spec.tsx` (render callback receives `href`).
- `TransitionStatus` = `'starting' | 'ending' | 'idle' | undefined`. Source: `internals/useTransitionStatus.ts:6`.
- `Side` = `'top' | 'bottom' | 'left' | 'right' | 'inline-start' | 'inline-end'`; `Align` = `'start' | 'center' | 'end'`. See [floating-and-positioning](../architecture/floating-and-positioning.md).

## 4. State model

### 4.1 Value and open state

- **NAV-STA-01** — MUST. The value is consumer-owned iff `value !== undefined` on the first render, otherwise component-owned starting from `defaultValue`; the mode never changes (UTL-STA-01…05 with `name: 'NavigationMenu'`, `state: 'value'`).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:83-88`; `packages/utils/src/useControlled.ts:41-46`. Test: [L2] `NavigationMenuRoot.test.tsx` "should be controlled by value prop", "should respect defaultValue".
- **NAV-STA-02** — MUST. `open` is derived: `open = value != null`. There is no separate open flag; closing is setting the value to `null`.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:91`. Test: [L2] `NavigationMenuRoot.test.tsx` "should call onValueChange when value changes" (`aria-expanded` follows the value).
- **NAV-STA-03** — MUST. Falsy non-nullish values (`0`, `''`, `false`) are valid open values: an Item with such a value opens, its Trigger reports `aria-expanded="true"`, and `onValueChange` receives the falsy value. Equality between the Root value and an Item value is strict identity (`===`).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:91,207,222`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:126`; `navigation-menu/content/NavigationMenuContent.tsx:62`. Test: [L2] `NavigationMenuRoot.test.tsx` "treats a falsy item value (%s) as a valid open value", "treats a falsy value (%s) as open for inline nested trigger interactions", "propagates a nested close with a falsy open value to the parent root". Docs: CHANGELOG.md v1.6.0 "Fix interaction, value, and styling-hook bugs (#4942)".
- **NAV-STA-04** — MUST. A Root is *nested* iff it is rendered inside the floating node of another Root (i.e. inside another Root's Content, including inline-nested Roots at any depth). A top-level Root creates the floating tree shared by all nested Roots and nested popups (Dialog, Popover, …) rendered inside its Content.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:80,318-321,363-365`; `floating-ui-react/components/FloatingTree.tsx:17-18,32-50,62-72,90-95`. Test: [L2] `NavigationMenuRoot.test.tsx` "does not apply aria-orientation to nested lists or root elements" (nested root renders `div`).

### 4.2 Mounted flag and transition status

The Root derives `mounted` and `transitionStatus` from `open` with the shared unmount-after-close mechanism ([transitions-and-mounting](../architecture/transitions-and-mounting.md) §6.4) configured with idle state **disabled**, ending state **not deferred**, and no entrance transition for an initially open menu; the awaited element is the Popup element, and a second completion watcher awaits the Viewport's inner target wrapper (inline nested menus have no Popup).

- **NAV-STA-05** — MUST. `mounted` becomes `true` in the same commit as `open` becomes `true` (status `'starting'`, cleared next frame), becomes `false` when a close completes (status `'ending'` from the closing commit until completion, then unset). A close completes on animations finished of the Popup element **or** of the Viewport target wrapper, whichever watcher fires (NAV-ANI-02), unless `preventUnmountOnClose()` was recorded (NAV-EVT-05), in which case it completes only on the `unmount` action. Server: relaxed, see §9.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:122-170`; `internals/useUnmountAfterClose.ts:59-64,118-127`; TRN-STA-03/04/06, TRN-ANI-18. Test: [L4] `NavigationMenuRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once", "keeps the popup mounted until the unmount action completes closing", "preserves popup size when controlled value closes externally" (`data-ending-style` on the popup).

### 4.3 Activation direction, active trigger hand-off and element registry

- **NAV-STA-06** — MUST. `activationDirection` is `null` while closed. When a Trigger is activated (hover switch, click, keyboard open) while the popup is mounted and a previous active trigger element is known: with `orientation='horizontal'`, if the new trigger's bounding-box `left` differs from the previous one's, the direction becomes `'right'` when it is greater and `'left'` otherwise; with `'vertical'`, if `top` differs, `'down'` when greater and `'up'` otherwise; when the coordinate is equal the direction is unchanged. It is reset to `null` when the value becomes `null` and when the close completes. The value exposed to Content is `null` whenever `open` is `false`, even during the exit transition.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:555-569`; `navigation-menu/root/NavigationMenuRoot.tsx:152,228,254`. Test: [L4] `NavigationMenuRoot.test.tsx` "reports %s activation direction when switching in either direction", "clears activation direction when controlled value closes externally after switching triggers".
- **NAV-STA-07** — MUST. Each Trigger owns a floating root context whose reference element is the Trigger and whose floating element is the Positioner element (or the Viewport element when there is no Positioner). Post-commit, pre-paint after a Trigger becomes the active trigger, its context becomes the Root's shared floating root context and its element becomes the *previous trigger element* used for focus return (NAV-FOC-05) and for the activation-direction computation. The shared context is cleared when the value becomes `null` and when the close completes.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:476-483,547-552`; `navigation-menu/root/NavigationMenuRoot.tsx:153,229`; `navigation-menu/viewport/NavigationMenuViewport.tsx:93-97`. Test: [L4] `NavigationMenuTrigger.test.tsx` "repositions the positioner when switching triggers via hover".
- **NAV-STA-08** — MUST. The Positioner, Popup, Viewport and Viewport target wrapper register their elements with the Root when they attach and unregister (`null`) when they detach; the List's dismiss and hover interactions are enabled only while a Positioner (or, for hover, a Viewport) element is registered or the menu is closed (NAV-DIS-08).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:96-101`; `navigation-menu/positioner/NavigationMenuPositioner.tsx:174`; `navigation-menu/popup/NavigationMenuPopup.tsx:51`; `navigation-menu/viewport/NavigationMenuViewport.tsx:100,123`; `navigation-menu/list/NavigationMenuList.tsx:44-46`. Test: [L2] `NavigationMenuRoot.test.tsx` "handles inline nested menu without positioner/popup correctly".
- **NAV-STA-09** — MUST. `viewportInert` is a Root flag, initially `false`. It becomes `true` when focus leaves the Viewport to an element that is neither inside the Viewport nor the active trigger (NAV-FOC-07); it becomes `false` whenever the value changes, when the active Trigger receives focus, and when the trigger's trailing focus guard is reached from outside the popup. It affects the DOM only for a Viewport without a Positioner (NAV-DOM-35).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:107,198-200`; `navigation-menu/viewport/NavigationMenuViewport.tsx:104-118`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:665-670,755-757`. Test: none.
- **NAV-STA-10** — MUST. An Item's effective value is its `value` prop when not nullish, otherwise a generated id stable for the Item's lifetime (NAV-API-11); the Trigger, Icon and Content of that Item compare the Root value against this effective value.
  Source: `navigation-menu/item/NavigationMenuItem.tsx:21-22,29`. Test: [L2] `NavigationMenuTrigger.test.tsx` "handles focus and positioner height".
- **NAV-STA-11** — MUST. "Active item" is `open && value === itemValue`. Trigger `State.open` and Icon `State.open` use this predicate; Content `State.open` uses `popupMounted && value === itemValue` (so an item stays "open" for Content purposes only while the popup is mounted).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:126`; `navigation-menu/icon/NavigationMenuIcon.tsx:23`; `navigation-menu/content/NavigationMenuContent.tsx:62`. Test: [L2] `NavigationMenuRoot.test.tsx` "adds popup-open data attributes to the active icon".
- **NAV-STA-12** — MUST. Each Content has its own transition status derived from its `open` with the default status machine (TRN-STA-03/04/06, no idle state): `'starting'` in the commit it opens (cleared next frame), `'ending'` in the commit it closes, unset once its exit animations finish (NAV-ANI-18). Its `mounted` flag is forced to `false` whenever the popup is no longer mounted, so a Content whose exit was cut short by the popup unmounting re-enters through `'starting'` next time.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:69-85`; `internals/useTransitionStatus.ts:17-42`. Test: [L4] `NavigationMenuRoot.test.tsx` "clears activation direction when controlled value closes externally after switching triggers" (`data-ending-style` on content), "seeds the popup width from the exiting panel when reopening after hovering a top-level link".

### 4.4 Per-trigger interaction state

- **NAV-STA-13** — MUST. Each Trigger keeps a *patient-click* flag `stickIfOpen`, initially `true`. Every hover-driven open or close request sets it to `true` and (re)starts a 500 ms timer that sets it to `false`; the timer is cleared when the menu closes.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:119,296-298,450-459`; `internals/constants.ts:4`. Test: [L4] `NavigationMenuRoot.test.tsx` "stays open if hovered then clicked within the patient threshold", "closes if hovered then clicked after the patient threshold".
- **NAV-STA-14** — MUST. Each Trigger records the `pointerType` of the last `pointerenter`/`pointerdown` it received (`'mouse'`, `'touch'`, `'pen'` or `''`); it is reset to `''` when the menu closes and when the trigger requests a close.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:120,296-304,465,647-654,660-661`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not open on hover with touch input", "restores hover open after a touch click closes outside".
- **NAV-STA-15** — MUST. The Root records the reason of the most recent close request (`closeReason`) when a close is requested, and clears it when the close completes; it gates focus return (NAV-FOC-06).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:93,207-209,156`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not restore focus to the trigger when closed via hover".

### 4.5 State objects summary

- **NAV-STA-16** — MUST. The state objects exposed to `className`/`style`/`render` contain exactly the fields tabulated in §3 for each part: Root `{open, nested}`, List `{open}`, Item `{}`, Trigger `{open, disabled}`, Icon `{open}`, Content `{open, transitionStatus, activationDirection}`, Link `{active}`, Portal `{}`, Backdrop `{open, transitionStatus}`, Positioner `{open, side, align, anchorHidden, instant}`, Popup `{open, transitionStatus, side, align, anchorHidden}`, Viewport `{}`, Arrow `{open, side, align, uncentered}`. Server: relaxed, see §9.
  Source: per-part `State` interfaces cited in §3. Test: [L2] describeConformance of every part (RND-DOM-47 class function receives the state) (L4 for the JS-mirrored `side`/`align`/`anchorHidden`/`transitionStatus` fields).

Invariants: at most one Item is active (the value is a single scalar); `data-popup-open` is present on at most one Trigger per Root; while `open` is `false`, `activationDirection` is `null` for every Content; the Portal subtree exists iff `mounted || keepMounted`.

Text state diagram (Root `open` / `mounted` / popup status):

```
CLOSED      value=null  mounted=false status=unset   (Portal absent unless keepMounted; Positioner hidden)
  | setValue(item)
OPEN        value=item  mounted=true  status='starting' → unset next frame  (Content 'starting' likewise)
  | setValue(other)                                | setValue(null)
OPEN (switch): old Content 'ending' + absolute,   CLOSING  value=null mounted=true status='ending'
  new Content 'starting', popup resizes           | animations finished on Popup or Viewport target
  (no Root status change)                         | (or unmount action when opted out)
                                                  CLOSED  (focus returned per NAV-FOC-05/06; onOpenChangeComplete(false))
```

## 5. Rendered DOM and ARIA

### 5.1 Root element

- **NAV-DOM-01** — MUST. The Root element is a `<nav>` (top-level) or `<div>` (nested) with no data attributes of its own: the state fields `open` and `nested` are booleans mapped by the default rule (RND-DOM-01/02) — `data-open=""` when open, `data-nested=""` when nested — and no attribute otherwise.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:351-360` (no custom mapping); `internals/getStateAttributesProps.ts:24-28`. Test: none (the outline lists no Root data attributes, §16 Q2).
- **NAV-DOM-02** — MUST. The Root element carries no `aria-orientation` attribute (NAV-ARIA-07) and no `role`.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:356-360`. Test: [L2] `NavigationMenuRoot.test.tsx` "does not apply aria-orientation to the top-level list or root element".
- **NAV-DOM-03** — MUST. The consumer DOM handle on the Root receives the `<nav>`/`<div>` element; the same element is the boundary used by the focus-out test (NAV-FOC-10).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:94,358`. Test: [L2] `NavigationMenuRoot.test.tsx` describeConformance (`refInstanceof: HTMLElement`).

### 5.2 List element

- **NAV-DOM-04** — MUST. The List element is a `<ul>` with `data-open=""` while the Root is open (default mapping of `open`) and no other component attributes; at the top level it carries the composite root's `keydown`/`focus` handlers and the dismiss handlers for the floating element (Escape, inside-press bookkeeping); it never carries `aria-orientation` or `role`.
  Source: `navigation-menu/list/NavigationMenuList.tsx:54-67,91,111-125`; `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L2] `NavigationMenuRoot.test.tsx` "does not apply aria-orientation to the top-level list or root element"; `NavigationMenuList.test.tsx` describeConformance.

### 5.3 Item element

- **NAV-DOM-05** — MUST. The Item element is an `<li>` with no component attributes; the Content is not a DOM child of it except in the server-rendered `keepMounted` case (NAV-ANI-16).
  Source: `navigation-menu/item/NavigationMenuItem.tsx:24-27`. Test: [L4] `NavigationMenuContent.test.tsx` "moves content into the popup and keeps it there when switching triggers" (`list.contains(content1) === false`).

### 5.4 Trigger element and its siblings

- **NAV-DOM-06** — MUST. The Trigger element always has `tabindex="0"` (every Trigger is in the sequential tab order; the composite roving `tabindex` is overridden), `type="button"` (native) or `role="button"` (non-native), and no `disabled` attribute even when disabled (NAV-ARIA-03).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:657,711-715,729-735`; `merge-props/mergeProps.ts:177-183`; `utils/useFocusableWhenDisabled.ts:30-47`; UBT-DOM-01/02/07. Test: [L2] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly" (Tab reaches the second trigger directly), "does not open via keyboard when the trigger is disabled" (trigger receives focus).
- **NAV-DOM-07** — MUST. While the Trigger is the active trigger it has `data-popup-open=""` and `data-pressed=""`; both are absent otherwise (including for inactive triggers while another item is open).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:727`; `utils/popupStateMapping.ts:13-16,39-46`. Test: [L2] `NavigationMenuRoot.test.tsx` "adds popup-open data attributes to the active icon" (Icon equivalent); `NavigationMenuTrigger.test.tsx` describeConformance.
- **NAV-DOM-08** — MUST. The Trigger has `data-disabled=""` iff `disabled` is `true`; it is removed when the prop becomes `false`.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:644`; `internals/getStateAttributesProps.ts:24-28`. Test: [L2] `NavigationMenuTrigger.test.tsx` "applies the data-disabled style hook only when disabled". Docs: CHANGELOG.md v1.9.0 "Add disabled data attribute to `<NavigationMenu.Trigger>` (#5521)".
- **NAV-DOM-09** — MUST. Every Trigger carries the marker attribute `data-base-ui-navigation-menu-trigger=""`; the outside-press dismissal ignores presses whose target is inside any element carrying it (NAV-PTR-17).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:664`; `navigation-menu/utils/constants.ts:3`; `navigation-menu/list/NavigationMenuList.tsx:57-64`. Test: [L2] `NavigationMenuRoot.test.tsx` "does not close menu when clicking a different trigger with mouse".
- **NAV-DOM-10** — MUST. While the Trigger is the active trigger, three siblings follow it in order: a focus guard (`<span tabindex="0" data-base-ui-focus-guard="" aria-hidden="true">`, visually hidden, PSH-STR-08), a `<span aria-owns="<viewport id>">` with the owner-visually-hidden style (`clip-path: inset(50%); position: fixed; top: 0; left: 0`, UTL-DOM-15), and a second focus guard. None of them exist for inactive triggers.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:737-782`; `utils/FocusGuard.tsx:33-41`; `internals/constants.ts:36-41`. Test: [L2] `NavigationMenuRoot.test.tsx` "returns focus to the trigger when no viewport focus guard is rendered" (`guards[1]` is the trailing guard).
- **NAV-DOM-11** — MUST. The Trigger's `aria-expanded`/`aria-controls` and the focus-guard siblings update in the same commit as the value change; `data-popup-open` is present as soon as `aria-expanded="true"`.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:662-663,737`. Test: [L2] `NavigationMenuRoot.test.tsx` "opens on click with mouse input" (`aria-expanded` true immediately after the click).

### 5.5 Icon element

- **NAV-DOM-12** — MUST. The Icon is a `<span aria-hidden="true">` whose children default to the single character `▼`; consumer children replace it.
  Source: `navigation-menu/icon/NavigationMenuIcon.tsx:29-34`. Test: [L2] `NavigationMenuIcon.test.tsx` describeConformance.
- **NAV-DOM-13** — MUST. The Icon has `data-popup-open=""` iff the Root is open and its Item is the active value; it never has `data-pressed`.
  Source: `navigation-menu/icon/NavigationMenuIcon.tsx:23-33`; `utils/popupStateMapping.ts:9-11,30-37`. Test: [L2] `NavigationMenuRoot.test.tsx` "adds popup-open data attributes to the active icon".

### 5.6 Content element

- **NAV-DOM-14** — MUST. The Content element has `data-open=""` while its state `open` is `true` and `data-closed=""` otherwise (exactly one of the two).
  Source: `navigation-menu/content/NavigationMenuContent.tsx:25-27,97-98`; `utils/popupStateMapping.ts:48-54`. Test: [L2] `NavigationMenuRoot.test.tsx` "preserves popup size when controlled value closes externally" (`data-open` on content).
- **NAV-DOM-15** — MUST. The Content element has `data-starting-style=""` while its own status is `'starting'` and `data-ending-style=""` while `'ending'` (NAV-STA-12).
  Source: `navigation-menu/content/NavigationMenuContent.tsx:27,99`; `internals/stateAttributesMapping.ts:10-20`. Test: [L4] `NavigationMenuRoot.test.tsx` "clears activation direction when controlled value closes externally after switching triggers".
- **NAV-DOM-16** — MUST. With `keepMounted`, the Content element has the boolean `hidden` attribute whenever its own `mounted` flag is `false` (closed and not exiting). Without `keepMounted` the element is absent in that state. A `hidden` attribute toggling on a kept Content is observed by the active trigger's size tracking (NAV-ANI-07).
  Source: `navigation-menu/content/NavigationMenuContent.tsx:136,151,169`. Test: [L2] `NavigationMenuContent.test.tsx` "keeps content mounted inside the popup when closed if the portal is kept mounted"; `NavigationMenuRoot.test.tsx` "updates popup sizing when switching kept inline nested content" (`hidden` moves from one content to the other).
- **NAV-DOM-17** — MUST. While a Content is exiting (`!open && mounted`), its element receives inline `position: absolute; top: 0; left: 0` (consumer `style` merged after) and the `inert` attribute unless focus is inside it (NAV-FOC-09). Neither is present while open or while hidden.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:126-133`; `packages/utils/src/inertValue.ts:3-9`. Test: none directly (`data-ending-style` tests exercise the state; TRN-DOM-06 row for Content).
- **NAV-DOM-18** — MUST. The Content element has `data-activation-direction="<left|right|up|down>"` while the Root is open and a direction is known (NAV-STA-06), on every Content (entering and exiting) in that Root; the attribute is absent while closed, including on an exiting Content.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:28-35,100`; `navigation-menu/content/NavigationMenuContentDataAttributes.ts:23`. Test: [L2] `NavigationMenuRoot.test.tsx` "reports %s activation direction when switching in either direction", "clears activation direction when controlled value closes externally after switching triggers".
- **NAV-DOM-19** — MUST. The Content element is a DOM descendant of the Viewport element (of the Viewport's inner target wrapper when the Viewport has no Positioner) whenever it exists and a Viewport has mounted; it carries the composite root's `keydown`/`focus` handlers and the component's own `focus`/`blur` tracking handlers.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:111-124,135,161-174`. Test: [L4] `NavigationMenuContent.test.tsx` "moves content into the popup and keeps it there when switching triggers" (`viewport.contains(content)`).

### 5.7 Link element

- **NAV-DOM-20** — MUST. The Link element has `data-active=""` iff `active` is `true`.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:37-39`; `navigation-menu/link/NavigationMenuLinkDataAttributes.ts:4`. Test: [L2] `NavigationMenuLink.test.tsx` describeConformance (class function receives `{active}`).
- **NAV-DOM-21** — MUST. The Link element renders no `tabindex` attribute (the composite roving `tabindex` is explicitly overridden with `undefined`), so a Link is tabbable by native `<a href>` rules and participates in arrow-key navigation through its composite registration only.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:43,66-76`; `merge-props/mergeProps.ts:177-183`. Test: [L2] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly" (Tab visits Link 1 then Link 2).

### 5.8 Portal container

- **NAV-DOM-22** — MUST. The Portal renders a `<div id="<generated>" data-base-ui-portal="">` appended to the resolved container; the Portal's own `className`/`style`/`render` and other attributes apply to this element; no focus guards or owner element are rendered by the Portal itself (NavigationMenu renders its own, NAV-DOM-10, NAV-DOM-34).
  Source: `navigation-menu/portal/NavigationMenuPortal.tsx:28-32`; `floating-ui-react/components/FloatingPortal.tsx:125-134,192-193,252-292` (guards require a focus-manager state, which NavigationMenu never sets). Test: [L2] `NavigationMenuPortal.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`) (L4 for the container placement clause).

### 5.9 Backdrop element

- **NAV-DOM-23** — MUST. The Backdrop is a `<div role="presentation">` with `data-open`/`data-closed` and `data-starting-style`/`data-ending-style` derived from the Root's `open` and status (NAV-STA-05).
  Source: `navigation-menu/backdrop/NavigationMenuBackdrop.tsx:23-26,33,42`; `utils/popupStateMapping.ts:63-70`. Test: [L2] `NavigationMenuBackdrop.test.tsx` describeConformance (L4 for the `data-starting-style`/`data-ending-style` clause).
- **NAV-DOM-24** — MUST. The Backdrop has the boolean `hidden` attribute while the popup is not mounted and inline `user-select: none; -webkit-user-select: none` always (consumer `style` merged after).
  Source: `navigation-menu/backdrop/NavigationMenuBackdrop.tsx:34-38`. Test: none.

### 5.10 Positioner element

- **NAV-DOM-25** — MUST. The Positioner is a `<div role="presentation">` whose inline style is the computed position (`position`, `top`, `left`, transforms per FLT-DOM-02…04) merged with the consumer `style`.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:170-177`; `utils/usePositioner.tsx:26-41`; see FLT-DOM-01. Test: [L4] `NavigationMenuPositioner.test.tsx` describeConformance.
- **NAV-DOM-26** — MUST. The Positioner has the boolean `hidden` attribute while the popup is not mounted and inline `pointer-events: none` while the Root is not open (including during the exit transition).
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:175-176`; `utils/usePositioner.tsx:28-36`; TRN-DOM-06. Test: none directly.
- **NAV-DOM-27** — MUST. The Positioner has `data-open`/`data-closed` from the Root's `open`, `data-side` and `data-align` with the resolved placement, and `data-anchor-hidden=""` when the anchor is hidden; it has no `data-starting-style`/`data-ending-style` (its state has no `transitionStatus`).
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:133-139`; `utils/popupStateMapping.ts:48-61`. Test: [L4] `NavigationMenuRoot.test.tsx` "pins the popup to the right edge when side is %s so it grows leftward" (`data-side` on the Popup, same source value).
- **NAV-DOM-28** — MUST. The Positioner has `data-instant=""` while its `instant` state is `true` (NAV-ANI-13/14) and no attribute otherwise. This boolean marker is NavigationMenu-specific and independent of the shared mount-transition suppression (NAV-ANI-15), which also applies to this element.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:80,138`; `navigation-menu/positioner/NavigationMenuPositionerDataAttributes.ts:28`; TRN-DOM-10 (default boolean rule). Test: [L2] `NavigationMenuRoot.test.tsx` "suppresses the positioner transition for the first frame while initially open", "updates popup sizing when the window is resized while the popup is open".
- **NAV-DOM-29** — MUST. The Positioner's inline style carries `--positioner-width` and `--positioner-height` (values `<n>px`) whenever the active Trigger or the Root has written them (§9.3–9.5); the properties are removed (not set to `auto`) while a measurement is in progress and are absent before the first open.
  Source: `navigation-menu/utils/setSharedFixedSize.ts:4-20`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:164-169,202-209,273-275`; `navigation-menu/positioner/NavigationMenuPositionerCssVars.ts:30,35`. Test: [L4] `NavigationMenuTrigger.test.tsx` "handles focus and positioner height", "handles positioner width correctly".

### 5.11 Popup element

- **NAV-DOM-30** — MUST. The Popup is a `<nav>` with the id of NAV-API-17 and `tabindex="-1"` (consumer-overridable).
  Source: `navigation-menu/popup/NavigationMenuPopup.tsx:49-55`. Test: [L2] `NavigationMenuPopup.test.tsx` describeConformance.
- **NAV-DOM-31** — MUST. When the resolved side is `top`, or is physically left (`left`; `inline-start` in LTR; `inline-end` in RTL), the Popup receives inline `position: absolute` plus `bottom: 0` (side `top`) or `top: 0` (otherwise), and `right: 0` (physically left) or `left: 0` (otherwise), so that size transitions grow away from the anchor; for other sides no such styles are set. Consumer `style` is merged after.
  Source: `navigation-menu/popup/NavigationMenuPopup.tsx:40-47,56-62`. Test: [L4] `NavigationMenuRoot.test.tsx` "pins the popup to the right edge when side is %s so it grows leftward" (`position: absolute; top: 0px; right: 0px` for `left`/LTR, `inline-start`/LTR, `inline-end`/RTL).
- **NAV-DOM-32** — MUST. The Popup has `data-open`/`data-closed`, `data-starting-style`/`data-ending-style` (Root status), `data-side`, `data-align` and `data-anchor-hidden` per the popup transition mapping.
  Source: `navigation-menu/popup/NavigationMenuPopup.tsx:32-38,67`; `utils/popupStateMapping.ts:63-70`. Test: [L4] `NavigationMenuRoot.test.tsx` "preserves popup size when controlled value closes externally" (`data-ending-style`), "pins the popup to the right edge…" (`data-side`).
- **NAV-DOM-33** — MUST. The Popup's inline style carries `--popup-width` and `--popup-height`: a `<n>px` value during a size transition, `auto` once the transition has settled, and no property while a measurement is in progress or before the first open (§9.3–9.5).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:159-169,199-200`; `navigation-menu/popup/NavigationMenuPopupCssVars.ts:5,10`. Test: [L4] `NavigationMenuTrigger.test.tsx` "does not let a previously hovered trigger reapply popup sizes after a later switch" (`700px` → `500px` → `auto`).

### 5.12 Viewport element

- **NAV-DOM-34** — MUST. With a Positioner, the Viewport is a `<div id>` and is wrapped by two focus guards (one before, one after, as siblings inside the Popup). Without a Positioner, the Viewport element itself contains, in order: a focus guard, an inner `<div>` target wrapper holding the children, and a second focus guard — but only when the Root has an active floating context; while no item has ever been active, the inner wrapper is rendered without guards.
  Source: `navigation-menu/viewport/NavigationMenuViewport.tsx:21-64,119-131`. Test: [L2] `NavigationMenuRoot.test.tsx` "renders viewport content correctly for inline nested menu".
- **NAV-DOM-35** — MUST. A Viewport without a Positioner has the `inert` attribute while `viewportInert` is `true` (NAV-STA-09); a Viewport with a Positioner never renders `inert`.
  Source: `navigation-menu/viewport/NavigationMenuViewport.tsx:118`. Test: none.

### 5.13 Arrow element

- **NAV-DOM-36** — MUST. The Arrow is a `<div aria-hidden="true">` whose inline style is the computed arrow position (`arrowStyles`, FLT-DOM-09) merged with the consumer `style`, plus `transition: none` while the Root status is `'starting'` (NAV-ANI-15).
  Source: `navigation-menu/arrow/NavigationMenuArrow.tsx:34-43`. Test: [L4] `NavigationMenuRoot.test.tsx` "disables popup and arrow transitions while a kept portal opens".
- **NAV-DOM-37** — MUST. The Arrow has `data-open`/`data-closed`, `data-side`, `data-align`, and `data-uncentered=""` when it cannot be centered on the anchor; it has no `data-anchor-hidden` (not in its state).
  Source: `navigation-menu/arrow/NavigationMenuArrow.tsx:27-32,42`; `utils/popupStateMapping.ts:48-61`; `navigation-menu/arrow/NavigationMenuArrowDataAttributes.ts:24`. Test: [L4] `NavigationMenuArrow.test.tsx` describeConformance.

### 5.14 ARIA

- **NAV-ARIA-01** — MUST. Every Trigger has `aria-expanded="true"` while it is the active trigger and `aria-expanded="false"` otherwise (including while closed and for inactive triggers of an open menu).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:662`. Test: [L2] `NavigationMenuRoot.test.tsx` "opens on hover with mouse input", "does not close menu when clicking a different trigger with mouse" (`false` on the previous trigger).
- **NAV-ARIA-02** — MUST. The active Trigger has `aria-controls` equal to the Popup element's id when a Popup element is registered; inactive triggers, and the active trigger of a menu without a Popup (inline nested), have no `aria-controls`.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:663`. Test: none (§16 Q3).
- **NAV-ARIA-03** — MUST. The Trigger has `aria-disabled="true"` while disabled and `aria-disabled="false"` while enabled (native mode with `focusableWhenDisabled: true`, UBT-ARIA-01); it never has the `disabled` attribute.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:711-715`; `utils/useFocusableWhenDisabled.ts:38-47`. Test: none directly (`data-disabled` test asserts the hook; UBT-ARIA-01).
- **NAV-ARIA-04** — MUST. The Link has `aria-current="page"` iff `active` is `true`; the attribute is absent otherwise (consumer `aria-current` overrides).
  Source: `navigation-menu/link/NavigationMenuLink.tsx:42`. Test: [L2] `NavigationMenuLink.test.tsx` "when `true`, renders with aria-current=\"page\"", "when `false`, does not render with aria-current=\"page\"".
- **NAV-ARIA-05** — MUST. Icon and Arrow have `aria-hidden="true"`; the focus guards have `aria-hidden="true"` except under VoiceOver on WebKit where they get `role="button"` instead (PSH focus-guard contract).
  Source: `navigation-menu/icon/NavigationMenuIcon.tsx:32`; `navigation-menu/arrow/NavigationMenuArrow.tsx:38`; `utils/FocusGuard.tsx:16-37`. Test: none.
- **NAV-ARIA-06** — MUST. While a Trigger is active, the owner `<span>` after it has `aria-owns` equal to the Viewport element's id (absent when no Viewport is registered), so assistive technology reads the portaled popup as following the trigger.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:750`; `navigation-menu/viewport/NavigationMenuViewport.tsx:103`. Test: none.
- **NAV-ARIA-07** — MUST. Neither the Root, the List (top-level or nested) nor the Content element renders `aria-orientation`, regardless of `orientation`.
  Source: `navigation-menu/list/NavigationMenuList.tsx:111-125`; CMP-DOM-03. Test: [L2] `NavigationMenuRoot.test.tsx` "does not apply aria-orientation to the top-level list or root element", "does not apply aria-orientation to nested lists or root elements". Docs: CHANGELOG.md v1.4.0 "Remove invalid `aria-orientation` attribute (#4355)".
- **NAV-ARIA-08** — MUST. Positioner and Backdrop have `role="presentation"`; the Popup and Content have no `role` (the Popup is a `<nav>` landmark by element semantics).
  Source: `utils/usePositioner.tsx:36`; `navigation-menu/backdrop/NavigationMenuBackdrop.tsx:33`; `navigation-menu/popup/NavigationMenuPopup.tsx:49-63`. Test: none.
- **NAV-ARIA-09** — MUST. No part renders `aria-haspopup`, `aria-labelledby` or `aria-describedby`; label association is left to the consumer.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:656-709` (absence). Test: none.

## 6. Interactions

### 6.1 Keyboard

The top-level List is a roving-tabindex composite group ([composite-and-list-navigation](../architecture/composite-and-list-navigation.md)) configured with `orientation` = Root `orientation`, `loopFocus: false`, Home/End disabled, propagation stopped on handled keys; its items are the Triggers and Links placed directly in the List. Every Content is a second composite group (orientation `'both'`, `loopFocus: true`, Home/End disabled) whose items are the Links and nested Triggers inside it (nested Lists are not composite roots). Triggers always carry `tabindex="0"` (NAV-DOM-06), so sequential Tab visits every Trigger; the roving index only drives arrow keys.

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowDown</kbd> | top-level Trigger, `orientation='horizontal'` | Opens this item with reason `list-navigation`; default prevented, propagation stopped; focus stays on the trigger. | NAV-KEY-01 |
| <kbd>ArrowRight</kbd> (LTR) / <kbd>ArrowLeft</kbd> (RTL) | top-level Trigger, `orientation='vertical'` | Same as above. | NAV-KEY-02 |
| <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> (horizontal) or <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> (vertical) | any item of the top-level List | Moves focus to the previous/next List item (RTL swaps left/right); no wrap at the ends; propagation stopped even at the ends. Does not open or close anything. | NAV-KEY-05, NAV-KEY-06 |
| <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> (horizontal List) / <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> (vertical List) | top-level Trigger | Not a list-navigation key: the open-key case above applies when it is the open key; otherwise not intercepted (bubbles). | NAV-KEY-01/02 |
| any Arrow key | item inside a Content (Link or nested Trigger) | Moves focus to the previous/next item of that Content, wrapping at the ends; nested Triggers never intercept arrows themselves. | NAV-KEY-08, NAV-KEY-09 |
| <kbd>Home</kbd>/<kbd>End</kbd> | anywhere | Not handled (no Home/End navigation). | NAV-KEY-07 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | Trigger | Native button activation → click → toggles per NAV-PTR-04/05 with reason `trigger-press`; focus stays on the trigger. | NAV-KEY-03 |
| <kbd>Escape</kbd> | Trigger or anywhere in the document while open (with a Positioner) | Closes with reason `escape-key`; default prevented unless canceled. | NAV-KEY-04 |
| <kbd>Tab</kbd> | active Trigger | Enters the popup: focus moves to the first tabbable element inside the Viewport (through the guards). | NAV-KEY-10 |
| <kbd>Tab</kbd> | last tabbable inside the popup | Leaves the popup to the next tabbable after the trigger in document order; closes with `focus-out` when that element is outside the Root. | NAV-KEY-11 |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | first tabbable inside the popup | Returns to the active Trigger. | NAV-KEY-12 |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | the element that follows the trigger in document order | Returns into the popup onto its last tabbable element. | NAV-KEY-12 |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | active Trigger | Native focus moves backwards; closes with `focus-out` when the destination is outside the Root and popup. | NAV-KEY-13 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | inside an inline nested Content | Sequential; moves between nested triggers and the links of the active nested panel without opening inactive panels; from the last link of the last nested panel Tab reaches the next top-level trigger. | NAV-KEY-15, NAV-KEY-16 |
| any key except <kbd>Tab</kbd> | disabled Trigger | Default prevented; nothing opens. | NAV-KEY-14 |
| Modifier + Arrow | any | Ignored by the composite groups (CMP-KEY-02). | NAV-KEY-05 |

- **NAV-KEY-01** — MUST. On a top-level Trigger with `orientation='horizontal'`, <kbd>ArrowDown</kbd> requests `value = itemValue` with reason `list-navigation` (once, even if the trigger is already active), runs the activation path (NAV-PTR-18 direction computation and §9.3 sizing), and calls `preventDefault()` and `stopPropagation()`. The popup's size tracking starts so that `--positioner-height` reflects the content height.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:676-693,584,619-640`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not emit a duplicate onValueChange when switching items via keyboard"; `NavigationMenuTrigger.test.tsx` "handles focus and positioner height", "keeps focus on the trigger when opened with ArrowDown". Docs: CHANGELOG.md v1.0.0-beta.1 "Fix positioner height when opening menu using the keyboard arrows (#2060)".
- **NAV-KEY-02** — MUST. With `orientation='vertical'` the open key is <kbd>ArrowRight</kbd> in LTR and <kbd>ArrowLeft</kbd> in RTL (reading direction from the nearest direction provider, default LTR); <kbd>ArrowDown</kbd> is then a list-navigation key, not an open key.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:684-688`; `internals/direction-context/DirectionContext.ts` (UTL `useDirection`). Test: [L4] `NavigationMenuTrigger.test.tsx` "opens a vertical menu with the mirrored arrow key in RTL mode". Docs: CHANGELOG.md v1.6.0 "Fix RTL component behavior (#4840)".
- **NAV-KEY-03** — MUST. <kbd>Enter</kbd> and <kbd>Space</kbd> on a Trigger activate it through the button mechanism (a `click`, UBT-KEY rules); the resulting open/close follows the click rules (NAV-PTR-04/05) with reason `trigger-press`, and focus remains on the trigger after the popup opens.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:537-541,659,711-715`. Test: [L4] `NavigationMenuTrigger.test.tsx` "keeps focus on the trigger when opened with Enter" / "…with Space". Docs: CHANGELOG.md v1.9.0 "Keep focus on trigger when opening (#5479)".
- **NAV-KEY-04** — MUST. While open and a Positioner is registered, <kbd>Escape</kbd> pressed anywhere in the document (document-level listener) or on a Trigger/List (element handlers) requests a close with reason `escape-key`; `preventDefault()` is called unless the request is canceled; propagation is stopped unless `allowPropagation()` was called. When an open nested floating node (nested portaled menu, Dialog, Popover) exists, the parent's Escape handler yields to it (FLT-KEY-02). Server: relaxed, see §9.
  Source: `navigation-menu/list/NavigationMenuList.tsx:54-67,91`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:731`; `floating-ui-react/hooks/useDismiss.ts:201-232,719-724,775-786`. Test: [L4] `NavigationMenuRoot.test.tsx` "returns focus to trigger when closing menu" (Escape from a link), "closes on the short exit path after switching content" (Escape on the trigger); `NavigationMenuContent.test.tsx` "keeps content mounted inside the popup when closed if the portal is kept mounted".
- **NAV-KEY-05** — MUST. The top-level List moves focus between its items with the arrow keys of its orientation (horizontal: <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd>, swapped in RTL; vertical: <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>), without wrapping (`loopFocus: false`), skipping nothing (no disabled indices are supplied; a disabled Trigger is still focusable), and keeping the roving index on the focused item so that removing an earlier item does not change which item the next arrow key moves from.
  Source: `navigation-menu/list/NavigationMenuList.tsx:113-123`; CMP-API-01/02, CMP-KEY-03/08/09, CMP-STA-26. Test: [L4] `NavigationMenuRoot.test.tsx` "does not emit a duplicate onValueChange when switching items via keyboard" (ArrowRight moves focus without opening); `NavigationMenuTrigger.test.tsx` "handles focus and positioner height" (ArrowRight/ArrowLeft between triggers); `NavigationMenuList.test.tsx` "navigates from the focused trigger after an earlier item is removed".
- **NAV-KEY-06** — MUST. A top-level List stops propagation of its orientation's arrow keys (<kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> for horizontal, <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> for vertical) even when the composite group did not move focus (end of list), so ancestors never observe them; other keys (e.g. <kbd>PageDown</kbd>) propagate. A nested List has no such guard.
  Source: `navigation-menu/list/NavigationMenuList.tsx:73-89`. Test: [L4] `NavigationMenuList.test.tsx` "stops vertical navigation keys from escaping the list".
- **NAV-KEY-07** — MUST. <kbd>Home</kbd> and <kbd>End</kbd> are not handled by any NavigationMenu composite group (`enableHomeAndEndKeys` left at its `false` default) and are not intercepted.
  Source: `navigation-menu/list/NavigationMenuList.tsx:113-123`; `navigation-menu/content/NavigationMenuContent.tsx:145-153,163-171`; CMP-KEY-01. Test: none.
- **NAV-KEY-08** — MUST. A nested Trigger (one whose Root is nested) never intercepts arrow keys: it has no open key, and its arrow keys are handled by the enclosing Content's composite group. Nested items therefore open only by hover, click, <kbd>Enter</kbd>/<kbd>Space</kbd>.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:676-682`. Test: [L4] `NavigationMenuRoot.test.tsx` "allows arrow key navigation to submenu triggers" (ArrowDown moves from Link 1 to nested triggers without opening).
- **NAV-KEY-09** — MUST. Inside a Content, all four arrow keys move focus among the Content's composite items (links and nested triggers, in DOM order), wrapping from last to first and first to last; propagation of handled keys is stopped. Items of a *nested* Content form their own group (three levels tested).
  Source: `navigation-menu/content/NavigationMenuContent.tsx:145-153,163-171` (defaults `orientation='both'`, `loopFocus=true`); `internals/composite/root/useCompositeRoot.ts:76-77,267-316`. Test: [L4] `NavigationMenuRoot.test.tsx` "allows arrow key navigation to submenu triggers", "allows arrow key navigation with 3+ levels of nesting".
- **NAV-KEY-10** — MUST. <kbd>Tab</kbd> from the active Trigger focuses the first tabbable element inside the popup: the trailing focus guard after the trigger is reached first; because focus arrived from outside the floating element it forwards focus to the guard before the Viewport, which (again arriving from outside) focuses the next tabbable element after itself in document order, i.e. the first tabbable inside the Viewport.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:739-748`; `navigation-menu/viewport/NavigationMenuViewport.tsx:41-50`; `floating-ui-react/utils/tabbable.ts:198-224`. Test: [L4] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly", "closes after tabbing out of arbitrary tabbable content" (Tab reaches a plain `<button>` inside Content); `NavigationMenuTrigger.test.tsx` "handles focus and positioner height".
- **NAV-KEY-11** — MUST. <kbd>Tab</kbd> from the last tabbable element inside the popup reaches the guard after the Viewport, which (focus arriving from inside) focuses the guard after the trigger, which (focus arriving from inside the floating element) focuses the next tabbable element after itself in document order — the next List item or the first tabbable after the menu — and, when that element is not inside the Root element, requests a close with reason `focus-out`. For an inline nested menu without a Positioner the close is not requested by the guard; instead, when the next tabbable would land inside the nested Viewport, the next tabbable after the inner guard is used (NAV-KEY-15).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:751-780`; `navigation-menu/viewport/NavigationMenuViewport.tsx:52-61`. Test: [L4] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly" (Tab from Link 2 lands on trigger 2, menu stays open), "closes the menu when tabbing forward out", "closes after tabbing out of arbitrary tabbable content".
- **NAV-KEY-12** — MUST. <kbd>Shift</kbd>+<kbd>Tab</kbd> from the first tabbable inside the popup reaches the guard before the Viewport, which (focus arriving from inside the floating element) focuses the *leading* outside guard after the trigger, which (focus arriving from inside) focuses the previous tabbable before itself: the active Trigger. <kbd>Shift</kbd>+<kbd>Tab</kbd> from the element after the menu reaches the trailing outside guard, which (focus from outside the floating element) clears `viewportInert` and focuses the guard after the Viewport, which focuses the previous tabbable before itself: the last tabbable inside the popup.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:739-748,751-760`; `navigation-menu/viewport/NavigationMenuViewport.tsx:41-61`. Test: [L4] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly" (three Shift+Tabs return to trigger 1), "returns to the last submenu item when shift+tabbing after tabbing out of it", "returns to the last inline submenu item when shift+tabbing after leaving it". Docs: CHANGELOG.md v1.4.0 "Fix reverse Shift+Tab submenu re-entry (#4464)".
- **NAV-KEY-13** — MUST. <kbd>Shift</kbd>+<kbd>Tab</kbd> from the active Trigger is not intercepted; focus moves to the previous tabbable element natively and the trigger's blur requests a close with reason `focus-out` when the destination is outside the Root element and the popup (NAV-FOC-10).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:694-708`. Test: [L4] `NavigationMenuRoot.test.tsx` "closes the menu when tabbing back out".
- **NAV-KEY-14** — MUST. On a disabled Trigger every keydown except <kbd>Tab</kbd> is default-prevented (UBT-KEY-20) and no open is requested; <kbd>Tab</kbd> moves focus normally.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:711-715`; `utils/useFocusableWhenDisabled.ts:23-27`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not open via keyboard when the trigger is disabled".
- **NAV-KEY-15** — MUST. In an inline nested menu, <kbd>Tab</kbd> from the last link of the active nested panel focuses the next tabbable after the nested trigger's trailing guard that is *not* inside the nested Viewport (the next top-level trigger when the nested panel is the last content); the nested panel stays open.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:761-773`; `floating-ui-react/utils/tabbable.ts:226-256`. Test: [L4] `NavigationMenuRoot.test.tsx` "tabs from the last link of the last nested panel to the next top-level trigger".
- **NAV-KEY-16** — MUST. Sequential focus movement never opens an inactive nested panel: tabbing onto an inactive nested Trigger leaves the active nested value unchanged.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:665-670` (focus handler only clears `viewportInert`); `navigation-menu/content/NavigationMenuContent.tsx:111-124`. Test: [L4] `NavigationMenuRoot.test.tsx` "tabs between nested triggers and links without opening inactive panels".
- **NAV-KEY-17** — MUST. Opening with the keyboard (open key, <kbd>Enter</kbd>, <kbd>Space</kbd>) never moves focus into the popup; the trigger keeps focus until the user tabs.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:656-709` (no focus call on open); `navigation-menu/root/NavigationMenuRoot.tsx:122-158` (no initial-focus logic). Test: [L4] `NavigationMenuTrigger.test.tsx` "keeps focus on the trigger when opened with %s"; `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly" (trigger focused after click).

### 6.2 Pointer and touch

Hover opening is the shared hover-reference interaction ([floating-and-positioning](../architecture/floating-and-positioning.md) §6.7, FLT-PTR-11…19, FLT-ALG-24) configured per Trigger with `move: false`, `restMs` = `delay` when the popup is not mounted or there is no Positioner and `0` otherwise, `delay: { close: closeDelay }`, and a safe-polygon close strategy (FLT-ALG-15) with `blockPointerEvents` enabled except for touch (pointer-events mutation FLT-DOM-19). The List runs the shared hover-floating interaction (§6.8, FLT-PTR-20…25) with `closeDelay` and the Root's `nodeId` on the Positioner (or Viewport) element, and the shared dismiss interaction (§6.4, FLT-ALG-17) with `outsidePressEvent: 'intentional'` (FLT-PTR-28/29). Clicks are the shared click interaction (§6.3, FLT-ALG-16) with `toggle` enabled only on the active trigger (FLT-PTR-05) and `stickIfOpen` bound to the patient-click flag (NAV-STA-13, FLT-PTR-06).

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| pointer enters Trigger (mouse/pen) | closed | Open after `delay` ms of rest (no `mousemove` beyond ~1px); reason `trigger-hover`. | NAV-PTR-01 |
| pointer enters another Trigger (mouse/pen) | open | Switch immediately (no delay), reason `trigger-hover`; activation direction recorded. | NAV-PTR-02, NAV-PTR-07 |
| pointer enters Trigger | touch | No hover open; nothing happens until click. | NAV-PTR-03 |
| click / tap Trigger | closed | Open, reason `trigger-press`. | NAV-PTR-04 |
| click active Trigger | hover-opened < 500 ms ago | Stays open (patient click). | NAV-PTR-05 |
| click active Trigger | click-opened, or hover-opened ≥ 500 ms ago | Close, reason `trigger-press`. | NAV-PTR-05 |
| click another Trigger | open | Switch, reason `trigger-press`; never closes. | NAV-PTR-06 |
| pointer leaves Trigger toward the popup | hover-opened | Stays open while the pointer is inside the safe polygon between trigger and popup, then closes after `closeDelay` if it leaves the polygon. | NAV-PTR-08, NAV-PTR-13 |
| pointer leaves Trigger elsewhere | hover-opened | Close after `closeDelay` ms, reason `trigger-hover`. | NAV-PTR-08 |
| pointer leaves Trigger | click-opened | No close; pointer-events lock released. | NAV-PTR-08 |
| pointer enters the popup (Positioner/Viewport) | open | Cancels pending close; releases the pointer-events lock. | NAV-PTR-09, NAV-PTR-11 |
| pointer leaves the popup | hover-opened | Close after `closeDelay` (unless moving to a trigger or into a nested floating element). | NAV-PTR-09 |
| pointerdown inside the popup on an element with `aria-haspopup` | hover-opened | Subsequent leaves no longer close (treated as click-like). | NAV-PTR-14 |
| pointerdown on a Trigger | any | Releases the pointer-events lock; records pointer type. | NAV-PTR-11, NAV-PTR-15 |
| press-backed click outside popup and triggers | open (with Positioner) | Close, reason `outside-press`. | NAV-PTR-16 |
| click on any NavigationMenu Trigger (same or other Root) | open | Not an outside press. | NAV-PTR-17 |
| window resize | open | Re-measure popup size without animation. | NAV-ANI-10 |

- **NAV-PTR-01** — MUST. With the menu closed and the popup not mounted (or no Positioner registered), moving a mouse-like pointer onto a Trigger starts a rest timer of `delay` ms on each `mousemove` whose movement is ≥ √2 px (movement below that while the timer is pending is ignored); when it elapses without the pointer having left, the menu opens with `value = itemValue` and reason `trigger-hover`. A `mouseenter` alone (no `mousemove`) does not start the timer. Leaving the trigger before the timer elapses cancels it.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:519-530` (`move: false`, `restMs`); `floating-ui-react/hooks/useHoverReferenceInteraction.ts:269-287,426-497`; see FLT-PTR-12, FLT-PTR-16. Test: [L4] `NavigationMenuRoot.test.tsx` "opens on hover with mouse input", "respects custom delay value" (not open at `delay − 25` ms, open at `delay + 25` ms); `NavigationMenuTrigger.test.tsx` "releases the pointer-events lock when the pointer sweeps across a trigger without the menu opening".
- **NAV-PTR-02** — MUST. While the popup is mounted and a Positioner is registered, the rest delay is `0`: moving onto an inactive Trigger (`mouseenter` or `mousemove`) switches to it immediately with reason `trigger-hover` (the trigger's own `mouseenter` handler requests the switch synchronously; the hover interaction's request is then a no-op).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:526,574-592,658`. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps top-level safePolygon pointer events active when switching directly to a different trigger" (no clock tick between `mouseenter` on trigger 2 and the assertion), "should be controlled by value prop".
- **NAV-PTR-03** — MUST. When the Trigger's recorded pointer type is `'touch'`, hover never opens or switches: hover-reason open requests are ignored, the trigger's `mouseenter` activation is skipped, and no pointer-events lock is applied. A touch tap (click) opens normally.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:442-444,486,578-580,596`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not open on hover with touch input", "opens on click with touch input", "does not close menu when clicking a different trigger on touch".
- **NAV-PTR-04** — MUST. A `click` on a Trigger while the menu is closed opens it with reason `trigger-press` (mouse, touch, pen, keyboard-generated clicks alike; mousedown does not open).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:537-541,659`; `floating-ui-react/hooks/useClick.ts:94-124,187-219`; see FLT-PTR-03, FLT-ALG-16. Test: [L4] `NavigationMenuRoot.test.tsx` "opens on click with mouse input", "opens on click with touch input".
- **NAV-PTR-05** — MUST. A `click` on the *active* Trigger closes the menu with reason `trigger-press` when the open event was click-like (click, mousedown, keydown, keyup) or when more than 500 ms (`PATIENT_CLICK_THRESHOLD`) have elapsed since the last hover-driven open/close request (NAV-STA-13); otherwise (hover-opened within 500 ms) the menu stays open and no `onValueChange` is emitted.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:450-459,537-541`; `floating-ui-react/hooks/useClick.ts:112-123`; `internals/constants.ts:4`; see FLT-PTR-06, FLT-STA-04. Test: [L4] `NavigationMenuRoot.test.tsx` "stays open if hovered then clicked within the patient threshold", "closes if hovered then clicked after the patient threshold".
- **NAV-PTR-06** — MUST. A `click` on an inactive Trigger while another item is open switches the value to that item with reason `trigger-press` and never closes the menu; the previous trigger's `aria-expanded` becomes `"false"` and its Content leaves.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:446-448,584-592`; `floating-ui-react/hooks/useClick.ts:112-115`; see FLT-PTR-05. Test: [L4] `NavigationMenuRoot.test.tsx` "does not close menu when clicking a different trigger with mouse", "does not close menu when clicking a different trigger on touch", "should call onValueChange when value changes" (exactly two calls).
- **NAV-PTR-07** — MUST. A hover switch resets the recorded open event, so that a menu opened by click, switched by hover to another trigger and hovered back is treated as hover-opened (leaving the trigger then closes it); a later hover-open of a different trigger after a quick click-then-switch sequence still works.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:571-576`. Test: [L4] `NavigationMenuRoot.test.tsx` "restores hover open after a quick click then trigger switch".
- **NAV-PTR-08** — MUST. The Trigger passes `handleClose: safePolygon(…)` and `delay: { close: closeDelay }` to the hover-reference interaction; the observable result on leaving the active Trigger (not into another trigger) is per FLT-PTR-14/15 and FLT-ALG-15: for a hover-opened menu, safe-polygon tracking starts and, when the strategy decides to close (pointer outside the polygon, or not landed on the popup within the 40 ms intent timeout), a close with reason `trigger-hover` is requested after `closeDelay` ms; for a click-like-opened menu (FLT-STA-06), leaving the trigger only releases the pointer-events lock. NavigationMenu-specific: a close requested by a non-active trigger is ignored.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:432-474,519-530`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-206,302-360`; `floating-ui-react/safePolygon.ts:90-451`; see FLT-PTR-14, FLT-PTR-15, FLT-ALG-15. Test: [L4] `NavigationMenuRoot.test.tsx` "respects custom closeDelay value", "does not restore focus to the trigger when closed via hover", "closes after pointerdown on a link in a hover-open popup when pointer leaves".
- **NAV-PTR-09** — MUST. The List passes `closeDelay` and the Root's tree `nodeId` to the hover-floating interaction on the floating element (Positioner, or Viewport when there is none), enabled while a shared floating context exists and (a Positioner or Viewport is registered, or the menu is closed); the observable result is per FLT-PTR-23…25: entering the floating element cancels any pending hover close and releases the pointer-events lock; leaving it while hover-opened requests a close after `closeDelay` with reason `trigger-hover`, except when moving onto an enabled Trigger of the same Root, into a descendant floating node, or while a nested node is open (the parent then closes 0 ms after the nested node's `floating.closed`).
  Source: `navigation-menu/list/NavigationMenuList.tsx:44-52`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:165-255`; see FLT-PTR-23, FLT-PTR-24, FLT-PTR-25, FLT-EVT-03. Test: [L4] `NavigationMenuRoot.test.tsx` "opens nested menu on hover and stays open when hovering over nested popup", "closes the parent menu after a nested submenu closes on delayed hover-out", "maintains inline viewport state when hovering between triggers and content".
- **NAV-PTR-10** — MUST. *Pointer-events lock.* When a mouse-like pointer enters a Trigger whose floating element exists (Positioner registered, or Viewport without Positioner) and the Root is top-level or inline-nested, inline `pointer-events: none` is set on the **scope element** — the nearest `<ul>` ancestor of the trigger, or `document.body` when there is none (custom List element) — and `pointer-events: auto` on the trigger and on the floating element. The same lock is (re)applied post-commit, pre-paint whenever the menu is open by hover and the lock strategy has `blockPointerEvents`. For a nested Root with its own Positioner the trigger's `mouseenter` applies no lock; the shared hover-floating pass applies it with the *parent's* floating element as scope.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:486,511-517,594-615`; `floating-ui-react/hooks/useHoverInteractionSharedState.ts:72-97`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:94-141`; see FLT-DOM-19, FLT-PTR-16, FLT-PTR-21. Test: [L4] `NavigationMenuRoot.test.tsx` "blocks pointer events on the list while traversing from a top-level trigger to the popup" (`ul` gets `pointer-events: none`, body does not; sibling trigger computed `none`), "scopes safePolygon pointer events to the document for a custom list element" (body `none`, custom list unchanged), "blocks pointer events on sibling top-level triggers when opened through real hover"; `NavigationMenuRoot.webkit.test.tsx` "blocks top-level pointer events for hover-open menus". Docs: CHANGELOG.md v1.4.0 "Fix top-level trigger pointer events blocking (#4362)".
- **NAV-PTR-11** — MUST. The lock is released (inline `pointer-events` removed from scope, trigger and floating element) when: the pointer enters the floating element; `pointerdown` occurs on any Trigger; the pointer leaves a Trigger while the menu is closed; the pointer leaves the trigger of a click-like-opened menu; the menu closes; the active Trigger unmounts; or the safe-polygon tracking decides to close. Only the lock owner for a scope element may release it; a new owner taking the same scope releases the previous one first.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:488-501,651-654,671-675`; `floating-ui-react/hooks/useHoverInteractionSharedState.ts:52-70,82-86`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:81-92,190-195`; see FLT-DOM-19, FLT-PTR-20, FLT-PTR-23. Test: [L4] `NavigationMenuRoot.test.tsx` "clears top-level safePolygon pointer events on trigger pointerdown", "blocks pointer events on the list while traversing…" (released on positioner `mouseenter`); `NavigationMenuTrigger.test.tsx` "releases the pointer-events lock on the list when the open trigger unmounts", "releases the pointer-events lock when the pointer sweeps across a trigger without the menu opening". Docs: CHANGELOG.md v1.9.0 "Fix pointer-events lock when sweeping quickly across a trigger (#5454)"; v1.7.0 "Fix frozen menu when the open trigger unmounts (#5240)".
- **NAV-PTR-12** — MUST. When the pointer enters an inactive Trigger while another item is open (a hover switch), the lock for the new trigger is applied in a **microtask** after the switch (so the switch's synchronously committed DOM update lands first); when entering the active trigger or a trigger of a closed menu it is applied synchronously. After returning from the popup to a trigger and switching again, the lock is re-applied with the new trigger as the `auto` element.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:600-615`. Test: [L4] `NavigationMenuRoot.test.tsx` "reapplies top-level safePolygon pointer events after returning from the popup and switching triggers", "keeps top-level safePolygon pointer events active when switching directly to a different trigger" (previous trigger computed `none`, new trigger expanded).
- **NAV-PTR-13** — MUST. For an inline nested menu (nested Root with a Viewport and no Positioner), the safe-polygon close context is built by the Trigger itself: the placement is the dominant direction from the trigger's bounding-box center to the Viewport's center (`right`/`left` when |Δx| ≥ |Δy|, else `bottom`/`top`), the elements are the trigger and the Viewport, and the node id is the nested Root's tree node; the enclosing tree is the top-level Root's. A pointer leaving the nested trigger toward the Viewport on that side keeps the nested item open (through `closeDelay`) and keeps the nested List locked; leaving away from the path releases the lock and still keeps the item open until the close delay logic decides; returning to the original trigger and traversing again keeps the lock.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:503-509,812-845`. Test: [L4] `NavigationMenuRoot.test.tsx` "scopes inline safePolygon pointer events to the submenu list while traversing to the viewport", "keeps an inline submenu open while traversing toward a viewport on the %s" (right/left/bottom/top), "clears inline safePolygon pointer events when the pointer leaves the traversal path", "keeps inline safePolygon pointer events when returning to the original trigger before traversing again"; `NavigationMenuRoot.webkit.test.tsx` "keeps nested safePolygon pointer events scoped on WebKit". Docs: CHANGELOG.md v1.4.0 "Fix nested inline submenu hover handoff (#4413)".
- **NAV-PTR-14** — MUST. A `pointerdown` inside the floating element on an interactive element that is inside an `[aria-haspopup]` element marks the session as "interacted inside": from then on the popup counts as click-like-opened, so leaving the trigger or the popup does not close it (FLT-STA-06). A `pointerdown` on a plain link does not set the mark, and a subsequent pointer leave still closes.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:180-188,251`; `navigation-menu/list/NavigationMenuList.tsx:48-52`; see FLT-PTR-22, FLT-STA-06. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps a hover-open menu open when pointerdown happens on a nested dialog trigger", "closes after pointerdown on a link in a hover-open popup when pointer leaves".
- **NAV-PTR-15** — MUST. The Trigger records `pointerType` on `pointerenter` and `pointerdown` (NAV-STA-14); a `touch` type disables the pointer-events lock (`blockPointerEvents: false`) and hover opening (NAV-PTR-03). After a touch-opened menu is closed by an outside press, the pointer type resets so that a later mouse hover opens normally.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:296-304,486,647-661`. Test: [L4] `NavigationMenuRoot.test.tsx` "restores hover open after a touch click closes outside", "restores hover open after touching a nested submenu trigger and closing outside".
- **NAV-PTR-16** — MUST. While open with a Positioner registered, a press-backed `click` (one whose `pointerdown`/`mousedown` happened while open, or a press-less virtual click) whose target is outside the floating element, outside every trigger of this Root, and not inside an open descendant floating node, requests a close with reason `outside-press` (intentional mode: `pointerdown` alone never closes; a press that starts inside and ends outside is ignored once).
  Source: `navigation-menu/list/NavigationMenuList.tsx:54-67`; `floating-ui-react/hooks/useDismiss.ts:351-374,484-520,725-746`; see FLT-PTR-28, FLT-PTR-29, FLT-DIS-08, FLT-DIS-10, FLT-ALG-17. Test: [L4] `NavigationMenuRoot.test.tsx` "respects focus outside when clicking menu", "restores hover open after a touch click closes outside", "keeps the menu open when interacting with a nested dialog" / "…nested popover" (clicks inside child nodes are not outside presses).
- **NAV-PTR-17** — MUST. A click whose target is inside any element carrying `data-base-ui-navigation-menu-trigger` (a Trigger of this or any other NavigationMenu, including nested ones) is never treated as an outside press.
  Source: `navigation-menu/list/NavigationMenuList.tsx:57-64`; `navigation-menu/utils/constants.ts:3`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not close menu when clicking a different trigger with mouse", "handles click interactions on inline nested menu triggers".
- **NAV-PTR-18** — MUST. Every activation (trigger `mouseenter`, `click`, or open-key `keydown`) runs synchronously, before any other handler of the same event continues: it computes the activation direction (NAV-STA-06), resets the recorded open event for non-click switches, ignores touch `mouseenter`, requests `value = itemValue` when the menu is already open and the event is not a `keydown` (reason `trigger-hover` for `mouseenter`, otherwise `trigger-press`), and applies the pointer-events lock (NAV-PTR-10/12). The resulting state change is flushed synchronously so that the size measurement that follows (§9.3) sees the new content.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:554-617,619-640`. Test: [L4] `NavigationMenuRoot.test.tsx` "reports %s activation direction when switching in either direction"; `NavigationMenuTrigger.test.tsx` "repositions the positioner when switching triggers via hover".
- **NAV-PTR-19** — MUST. A nested Root with its own Positioner opens on hover of its Trigger after `delay` ms like a top-level menu, stays open while the pointer is over its popup, and when both the nested and the parent popup are left, the nested menu closes first and the parent closes after `closeDelay` (the parent waits for the nested close).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:519-530`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:197-245`. Test: [L4] `NavigationMenuRoot.test.tsx` "opens nested menu on hover and stays open when hovering over nested popup", "closes the parent menu after a nested submenu closes on delayed hover-out". Docs: CHANGELOG.md v1.3.0 "Fix close propagation in nested hover menus (#4285)".
- **NAV-PTR-20** — MUST. Hovering a Trigger of an inline nested Root whose value is `null` (no `defaultValue`) opens that nested item after `delay` ms without closing the parent; hovering between inline nested triggers switches the nested content, and the parent stays open throughout.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:128-130,519-530`; `navigation-menu/list/NavigationMenuList.tsx:44-52`. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps parent menu open when hovering inline nested triggers without defaultValue", "switches content in viewport when hovering different nested triggers". Docs: CHANGELOG.md v1.3.0 "Fix support for nested inline menus (#4198)".

### 6.3 Focus management

- **NAV-FOC-01** — MUST. Opening never moves focus: after a hover, click or keyboard open the previously focused element (normally the Trigger) keeps focus; there is no focus trap and no initial focus inside the popup.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:432-474,554-640` (no focus calls). Test: [L4] `NavigationMenuRoot.test.tsx` "respects focus outside when clicking menu" (trigger focused after click); `NavigationMenuTrigger.test.tsx` "keeps focus on the trigger when opened with %s".
- **NAV-FOC-02** — MUST. Every Trigger is a sequential tab stop (`tabindex="0"`, NAV-DOM-06), including disabled triggers and inactive triggers of an open menu; the composite group's roving index is used only for arrow keys.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:657`. Test: [L4] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly".
- **NAV-FOC-03** — MUST. Links are tab stops by their native semantics (an `<a href>`; no `tabindex` is rendered, NAV-DOM-21); a substituted element without native focusability is not made focusable.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:43`. Test: [L4] `NavigationMenuRoot.test.tsx` "moves focus through the menu correctly".
- **NAV-FOC-04** — MUST. The two focus guards after the active Trigger and the two around the Viewport (NAV-DOM-10, NAV-DOM-34) implement the Tab flow of NAV-KEY-10…12: the trigger's guards decide by whether focus arrived from inside the floating element (Positioner, or Viewport without one); the Viewport's guards decide by whether focus arrived from inside the same element. "Next/previous tabbable" is computed over the whole document body from the currently focused guard (FLT-FOC-15).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:717,739-780`; `navigation-menu/viewport/NavigationMenuViewport.tsx:21-64`; `floating-ui-react/utils/tabbable.ts:198-262`. Test: [L4] `NavigationMenuRoot.test.tsx` tabbing block (six tests).
- **NAV-FOC-05** — MUST. When a close completes (NAV-STA-05) and the close reason is not one of `trigger-hover`, `outside-press`, `focus-out`, and the previously active trigger element still exists, and the document's active element is the body or is inside the Popup element, and a Popup element exists, focus is moved to that trigger element with `preventScroll`. The remembered trigger is then forgotten (so a second completion cannot refocus it).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:33-37,133-149`. Test: [L4] `NavigationMenuRoot.test.tsx` "returns focus to trigger when closing menu" (Escape from a focused link). Docs: CHANGELOG.md v1.0.0-beta.3 "Fix focus returning to trigger without animations (#2779)".
- **NAV-FOC-06** — MUST. No focus return happens when the close reason is `trigger-hover`, `outside-press` or `focus-out`, when focus is already on an element outside the popup (other than the body), or when there is no Popup element (inline nested menus never return focus).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:33-37,137-146`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not restore focus to the trigger when closed via hover", "does not restore focus to the trigger when focus moves outside", "respects focus outside when clicking menu".
- **NAV-FOC-07** — MUST. When focus leaves the Viewport element (`blur` with a non-null related target) to an element that is neither inside the Viewport nor the active trigger element, `viewportInert` becomes `true` (NAV-STA-09); a Viewport without a Positioner then renders `inert` (NAV-DOM-35) until the flag is cleared by a value change, by the active Trigger receiving focus, or by the trailing outside guard being reached from outside the popup.
  Source: `navigation-menu/viewport/NavigationMenuViewport.tsx:104-118`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:665-670,753-759`; `navigation-menu/root/NavigationMenuRoot.tsx:198-200`. Test: none.
- **NAV-FOC-08** — MUST. The Positioner listens (capture phase) for `focusin`/`focusout` on itself: when focus enters it from outside, every element inside that was made non-tabbable is restored; when focus leaves it to outside, every tabbable element inside gets `tabindex="-1"` (its previous value remembered in `data-tabindex`). Because the restore runs before any guard's own focus handler, guard-driven re-entry (NAV-KEY-12) always sees the original tab order.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:83-106`; `floating-ui-react/utils/tabbable.ts:264-284`; FLT-DOM-18. Test: [L4] `NavigationMenuRoot.test.tsx` "returns to the last submenu item when shift+tabbing after tabbing out of it".
- **NAV-FOC-09** — MUST. A Content tracks whether focus is inside it (`focus` from any descendant that is not a focus guard sets the flag; `blur` to an element outside the Content clears it). While the Content is exiting, it is `inert` unless focus is inside it (NAV-DOM-17), so an exiting panel cannot receive focus but does not eject focus it already holds.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:67,111-133`. Test: none.
- **NAV-FOC-10** — MUST. When a Trigger loses focus while a Positioner and a Popup element are registered, and the related target is outside the Root element, outside the Popup element, and not inside any open descendant floating node, a close with reason `focus-out` is requested. A `null` related target counts as outside.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:694-708`; `navigation-menu/utils/isOutsideMenuEvent.ts:18-35`; `floating-ui-react/utils/element.ts` (`contains(x, null)` is false). Test: [L4] `NavigationMenuRoot.test.tsx` "closes the menu when tabbing back out", "keeps the opt-out when focus leaves the trigger during a manual exit", "does not collapse auto-sized popup to zero on close if measurement temporarily returns 0" (`blur` with `relatedTarget: document.body`).
- **NAV-FOC-11** — MUST. When a Link loses focus while a Positioner and a Popup element are registered, the same test as NAV-FOC-10 applies *only if the Link is not inside the Popup element* (a top-level Link in the List): then a related target outside Root, Popup and open child nodes requests `focus-out`. A Link inside the Popup never requests a close on blur.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:49-63`; `navigation-menu/utils/isOutsideMenuEvent.ts:29-34` (`!contains(popupElement, currentTarget)` is false for links inside the popup). Test: [L4] `NavigationMenuLink.test.tsx` "keeps the menu open when a link loses focus without a related target".
- **NAV-FOC-12** — MUST. Focus moving between the Trigger, the popup contents and nested popups (Dialog/Popover rendered inside Content) never closes the menu: those elements are inside the Root, inside the Popup, or inside an open child floating node.
  Source: `navigation-menu/utils/isOutsideMenuEvent.ts:21-34`; `floating-ui-react/utils/nodes.ts:5-16`. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps the menu open when interacting with a nested dialog", "keeps the menu open when interacting with a nested popover".

### 6.4 Dismissal

- **NAV-DIS-01** — MUST. <kbd>Escape</kbd> closes (reason `escape-key`) per NAV-KEY-04; the close is a request through `onValueChange` and can be canceled.
  Source: `navigation-menu/list/NavigationMenuList.tsx:54-67`. Test: [L4] `NavigationMenuRoot.test.tsx` "returns focus to trigger when closing menu".
- **NAV-DIS-02** — MUST. An outside press closes (reason `outside-press`) per NAV-PTR-16/17.
  Source: `navigation-menu/list/NavigationMenuList.tsx:54-67`. Test: [L4] `NavigationMenuRoot.test.tsx` "respects focus outside when clicking menu".
- **NAV-DIS-03** — MUST. Focus leaving the menu closes (reason `focus-out`) per NAV-KEY-11/13 and NAV-FOC-10/11.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:694-708,775-777`. Test: [L4] `NavigationMenuRoot.test.tsx` "closes the menu when tabbing forward out", "closes the menu when tabbing back out".
- **NAV-DIS-04** — MUST. The pointer leaving a hover-opened menu closes it after `closeDelay` (reason `trigger-hover`) per NAV-PTR-08/09.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:519-530`; `navigation-menu/list/NavigationMenuList.tsx:48-52`. Test: [L4] `NavigationMenuRoot.test.tsx` "respects custom closeDelay value".
- **NAV-DIS-05** — MUST. There is no scroll-based or window-blur dismissal; scrolling re-positions (anchor tracking) and a window `resize` re-measures (NAV-ANI-10).
  Source: `navigation-menu/list/NavigationMenuList.tsx:54-65` (only `outsidePress`/`escapeKey`); `navigation-menu/trigger/NavigationMenuTrigger.tsx:333-352`. Test: none.
- **NAV-DIS-06** — MUST. When a parent menu closes, every nested menu inside its Content disappears with it (the nested Content elements are inside the parent's popup); a nested Root's own value is not changed by the parent's close, so an inline nested Root with `defaultValue` shows the same panel when the parent reopens.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:157-174` (nested Root lives inside the parent's Content). Test: [L4] `NavigationMenuRoot.test.tsx` "closes inline nested viewport when parent menu closes".
- **NAV-DIS-07** — MUST. Interacting with a nested popup (Dialog, Popover) rendered inside Content — opening it, clicking inside it — never closes the menu: child floating nodes are excluded from outside-press, focus-out and hover-leave decisions, and a parent's Escape yields to an open child node.
  Source: `floating-ui-react/hooks/useDismiss.ts:169-178,376-386,484-486`; `navigation-menu/utils/isOutsideMenuEvent.ts:21-26`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:209-221`. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps the menu open when interacting with a nested dialog", "keeps the menu open when interacting with a nested popover", "keeps a hover-open menu open when pointerdown happens on a nested dialog trigger". Docs: CHANGELOG.md v1.0.0-beta.5 "Fix nested popup dismiss actions (#2978)".
- **NAV-DIS-08** — MUST. An inline nested Root (no Positioner) has no Escape or outside-press dismissal of its own while open (its dismiss interaction is enabled only while a Positioner is registered or it is closed); it closes only through its parent, through hover-out, through `closeOnClick` links, or programmatically.
  Source: `navigation-menu/list/NavigationMenuList.tsx:44,54-56`. Test: none (§16 Q4).
- **NAV-DIS-09** — MUST. A click on a Link with `closeOnClick` requests a close with reason `link-press` on the Link's Root; when that Root is nested and the request is not canceled, the parent Root receives the same close request (same details object) after the nested one commits, and so on up to the top level, so that every level closes and each level's `onValueChange` observes `null` with reason `link-press`.
  Source: `navigation-menu/link/NavigationMenuLink.tsx:44-48`; `navigation-menu/root/NavigationMenuRoot.tsx:234-241`. Test: [L4] `NavigationMenuLink.test.tsx` "closes the menu when clicking a link when true"; `NavigationMenuRoot.test.tsx` "closes the parent menu when a nested link with closeOnClick is clicked", "calls onValueChange on the parent root when nested closeOnClick link is clicked", "closes all levels when a deeply nested link with closeOnClick is clicked", "propagates a nested close with a falsy open value to the parent root". Docs: CHANGELOG.md v1.3.0 "Close parent menus when nested link with `closeOnClick` is clicked (#4276)".

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: Value \| null, details: ChangeEventDetails) => void` | A trigger, link, dismissal, keyboard or imperative action requests a different value (NAV-EVT-06) | `trigger-press`, `trigger-hover`, `list-navigation`, `link-press`, `escape-key`, `outside-press`, `focus-out`, `imperative-action` (`none` declared, never emitted) | yes (`cancel()`); also `preventUnmountOnClose()` on closes |
| `onOpenChangeComplete` | `(open: boolean) => void` | `false` only: when the close completes (animations finished or `unmount` action) | — | no |

- **NAV-EVT-01** — MUST. `onValueChange(nextValue, details)` is invoked synchronously inside the originating input's handling, before the value changes, with `details` a change-event details object (EVD-API-01) extended with `preventUnmountOnClose()` (EVD-API-09); `details.event` is the native event (`MouseEvent`/`PointerEvent`/`KeyboardEvent`/`FocusEvent`) or a synthetic `base-ui` event for `imperative-action`; `details.trigger` is the Trigger element for `trigger-press`/`trigger-hover` requests issued by the shared click/hover interactions and `undefined` for requests issued directly by the component.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:202-216`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:585-591,689`; `internals/createBaseUIEventDetails.ts`. Test: [L2] `NavigationMenuRoot.test.tsx` "should call onValueChange when value changes", "closes through the `close` action so `onValueChange` can opt out" (`reason === 'imperative-action'`).
- **NAV-EVT-02** — MUST. The reasons and the user actions producing them are exactly: `trigger-press` — Trigger click/tap/Enter/Space (open, toggle-close, or switch); `trigger-hover` — hover open after the rest delay, hover switch, hover-out close; `list-navigation` — the open key on a top-level Trigger; `link-press` — click on a Link with `closeOnClick` (close), including the propagated parent close; `escape-key` — Escape; `outside-press` — press-backed click outside; `focus-out` — Tab past the popup, trigger/top-level-link blur to outside; `imperative-action` — `actionsRef.current.close()`. `none` is part of the type but no code path emits it.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:439-448`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:585-591,689,706,776`; `navigation-menu/link/NavigationMenuLink.tsx:46,61`; `navigation-menu/list/NavigationMenuList.tsx:54`; `navigation-menu/root/NavigationMenuRoot.tsx:249`; `floating-ui-react/hooks/useClick.ts:67,83`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:198-203,280-297,480-484`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:168,242`; `floating-ui-react/hooks/useDismiss.ts:220,518`. Test: [L4] `NavigationMenuRoot.test.tsx` "closes through the `close` action so `onValueChange` can opt out"; other reasons are exercised without reason assertions (§16 Q5).
- **NAV-EVT-03** — MUST. A hover-reason request (`trigger-hover`) is committed synchronously (flushed before the handler returns); all other requests commit through the normal update path. Activation-path requests (NAV-PTR-18) are also flushed synchronously. Server: relaxed, see §9.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:469-473,555`; EVD-EVT-18. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps top-level safePolygon pointer events active when switching directly to a different trigger" (`aria-expanded` updated without a tick).
- **NAV-EVT-04** — MUST. Calling `details.cancel()` inside `onValueChange` prevents the change: in uncontrolled mode the value is unchanged and nothing opens/closes; the close bookkeeping (close reason, activation-direction reset, floating-context reset, parent propagation) is skipped as well. In controlled mode the consumer must itself ignore the request.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:218-220`; EVD-EVT-05/06. Test: [L2] `NavigationMenuRoot.test.tsx` "does not open when onValueChange cancels the interaction".
- **NAV-EVT-05** — MUST. `details.preventUnmountOnClose()` called during a non-canceled `onValueChange(null, …)` while the value is currently non-null records an opt-out: the popup then stays mounted in its closing phase (`data-ending-style`, `mounted` true) until `actionsRef.current.unmount()` is called, at which point `onOpenChangeComplete(false)` fires once. A redundant close request (value already `null`, e.g. the trigger's blur after an imperative close) does not invoke `onValueChange` and does not discard a recorded opt-out. Opening clears the opt-out.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:211-212,222-227`; `utils/popups/popupStoreUtils.ts:228-236`; `internals/useUnmountAfterClose.ts:66-76`; EVD-EVT-21/22, TRN-ANI-23/24. Test: [L2] `NavigationMenuRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing", "keeps the opt-out when the `close` action is called again while closed", "keeps the opt-out when focus leaves the trigger during a manual exit" (L4 for the `data-ending-style` clause and the focus-leave test). Docs: `components/navigation-menu/page.mdx` §Closing animations; CHANGELOG.md v1.9.0 "Decouple automatic unmounting from `actionsRef`…".
- **NAV-EVT-06** — MUST. `onValueChange` is not invoked when the requested value is identical (`===`) to the current value (a second `click` activation on an already switched-to trigger, a redundant close); the internal bookkeeping of a redundant close still runs (NAV-STA-15 reason record, direction reset).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:214-216,222-230`. Test: [L2] `NavigationMenuRoot.test.tsx` "should call onValueChange when value changes" (two calls for two clicks although each click issues two requests), "does not emit a duplicate onValueChange when switching items via keyboard".
- **NAV-EVT-07** — MUST. `onOpenChangeComplete` is invoked only with `false`, exactly once per completed close, from the unmount completion (after the Popup's/Viewport target's exit animations finish, or on the `unmount` action); it is never invoked with `true` after an open, and never on mount, on a redundant `unmount` call, or while the opt-out of NAV-EVT-05 is pending. Server: relaxed, see §9.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:133-158,161-170` (no open-completion watcher). Test: [L4] `NavigationMenuRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once", "ignores `unmount` while the popup is open", "closes on the short exit path after switching content" (`[[false]]`).
- **NAV-EVT-08** — MUST. `actionsRef.current.close()` requests `value = null` with reason `imperative-action` through `onValueChange` (so the consumer may cancel or opt out); it is a no-op for the value when already closed but still runs the redundant-close bookkeeping.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:249`. Test: [L2] `NavigationMenuRoot.test.tsx` "closes through the `close` action so `onValueChange` can opt out", "keeps the opt-out when the `close` action is called again while closed".
- **NAV-EVT-09** — MUST. `actionsRef.current.unmount()` ends the closing phase immediately when the popup is mounted and closed (exit animations are abandoned); it is ignored when already unmounted; when called while open it is deferred until the Root's next render batch is committed and applied only if the menu closed in the same render batch, otherwise dropped (TRN-ANI-19…22).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:248`; `internals/useUnmountAfterClose.ts:86-116`. Test: [L2] `NavigationMenuRoot.test.tsx` "ignores `unmount` while the popup is open", "still unmounts on a later close after `unmount` was called while open", "unmounts immediately when called while the popup is closing".
- **NAV-EVT-10** — MUST. For a nested Root, a non-canceled `link-press` close is propagated to the parent Root as `parent.setValue(null, sameDetails)` after the nested value is set; the parent's `onValueChange` then runs with the same details object (so a parent `cancel()` affects only the parent).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:234-241`. Test: [L2] `NavigationMenuRoot.test.tsx` "calls onValueChange on the parent root when nested closeOnClick link is clicked".
- **NAV-EVT-11** — MUST. Changing the controlled `value` prop from the consumer never invokes `onValueChange` and never records a close reason or opt-out; a controlled close therefore plays its exit animation and completes automatically unless the consumer previously called `close()` and opted out.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:83-88,202-243`; EVD-STA-06. Test: [L2] `NavigationMenuRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing" (comment: "The controlled close never runs `onValueChange`"), "preserves popup size when controlled value closes externally" (L4 for the exit-animation clause).

## 8. Form integration

Not applicable — NavigationMenu renders no form control, hidden input or Field integration (no `name`, `value` serialization, validation or Field state attributes exist in the source).

## 9. Lifecycle, mounting and animation

Timing vocabulary follows conventions §4: *post-commit, pre-paint*, *after paint*, *next frame*, *two frames*, *microtask*, *N ms*, *on animations finished* (TRN-ALG-03). "Popup size variables" means `--popup-width`/`--popup-height` on the Popup element; "positioner size variables" means `--positioner-width`/`--positioner-height` on the Positioner element. "Measure the popup" means reading the Popup element's CSS dimensions (`getCssDimensions`, UTL-DOM-23: computed width/height, falling back to `offsetWidth`/`offsetHeight` when they disagree after rounding). "Remembered size" is the last size recorded by a resize observer on the Popup (NAV-ANI-11).

### 9.1 Mounting

- **NAV-ANI-01** — MUST. The Portal subtree (Portal container, Backdrop, Positioner, Popup, Viewport, Arrow) is rendered iff `mounted || keepMounted`; with `keepMounted` the Positioner and Backdrop carry `hidden` while not mounted (NAV-DOM-24/26). A Content element without `keepMounted` exists only while its own `mounted` flag is true and a Viewport target exists.
  Source: `navigation-menu/portal/NavigationMenuPortal.tsx:21-26`; `navigation-menu/content/NavigationMenuContent.tsx:157-159`; TRN-ANI-01. Test: [L2] `NavigationMenuRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once" (`popup-root` removed), "disables popup and arrow transitions while a kept portal opens"; `NavigationMenuContent.test.tsx` "does not keep the content mounted post-hydration when keepMounted is false".
- **NAV-ANI-02** — MUST. A close completes (NAV-STA-05) on animations finished of the Popup element, with a second watcher armed on the Viewport's inner target wrapper (present only for a Viewport without a Positioner); whichever completes first unmounts. The watchers are armed when `mounted && !open && !optedOut` and aborted when the menu reopens. With no `getAnimations` support or with animations disabled (TRN-EDGE-01) completion is synchronous. Server: relaxed, see §9.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:122-170`; `internals/useUnmountAfterClose.ts:118-127`; `internals/useOpenChangeComplete.tsx`; TRN-ANI-17/18. Test: [L4] `NavigationMenuRoot.test.tsx` "preserves popup size when controlled value closes externally" (mocked `getAnimations` keeps `data-ending-style` until finished), "closes on the short exit path after switching content" (completion within 325 ms after a 150 ms exit transition; the old Content's 350 ms transition does not delay it).

### 9.2 Close-transition size freeze

- **NAV-ANI-03** — MUST. Post-commit, pre-paint in the commit where `open` becomes `false` (and whenever the Popup/Positioner elements change while closed), if the positioner size variables currently hold positive widths and heights, those values are written to both the popup size variables and the positioner size variables (`<n>px`), so the closing transition has a fixed size even if the Popup now measures `0`. Nothing is written when either stored value is missing or non-positive.
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:39-57,172-196`; `navigation-menu/utils/setSharedFixedSize.ts:4-20`. Test: [L4] `NavigationMenuRoot.test.tsx` "preserves popup size when controlled value closes externally", "…with keepMounted portal" (`675px`/`220px` on both elements while `data-ending-style`), "does not collapse auto-sized popup to zero on close if measurement temporarily returns 0". Docs: CHANGELOG.md v1.6.0 "Preserve exit transition when controlled (#4855)".

### 9.3 Opening and switching: popup size transition

The active Trigger drives the size of the shared Popup. Let *current* be the Popup's size at the moment of activation and *measured* its size after the new Content is in place.

- **NAV-ANI-04** — MUST. On activation of a Trigger (hover switch, click, open key) while Popup and Positioner elements exist: (1) *current* is measured **before** the activation commits; (2) the activation commits synchronously (NAV-PTR-18); (3) the **size transition** runs: any pending auto-size reset is cancelled, all four size variables are removed, the popup is measured (each axis falling back to the remembered size when `0`), and if *current* has a zero axis it is replaced by *measured*; the popup size variables are set to *current* (px) and the positioner size variables to *measured* (px); (4) **next frame**, if this Trigger is still the active one, the popup size variables are set to *measured* (px) and an auto-size reset is scheduled (NAV-ANI-05). Steps 3–4 also run post-commit, pre-paint whenever the active Trigger's `open`, Popup, Positioner or transition status changes (e.g. the initial open), measuring *current* at that time, unless the skip of NAV-ANI-06 applies.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:185-222,411-430,619-640`. Test: [L4] `NavigationMenuTrigger.test.tsx` "handles focus and positioner height" (`--positioner-height` ≈ content height after keyboard open and after switching), "handles positioner width correctly", "does not let a previously hovered trigger reapply popup sizes after a later switch" (`--popup-width` goes `700px` → `500px` → `auto`); `NavigationMenuRoot.test.tsx` "updates popup sizing immediately when switching to a keepMounted trigger" (positioner variables hold the new size right after the click). Docs: CHANGELOG.md v1.4.0 "Fix initial trigger switch size snap (#4327)"; v1.6.0 "Fix `keepMounted` content sizing (#4817)".
- **NAV-ANI-05** — MUST. *Auto-size reset.* On animations finished of the Popup element (TRN-ALG-03; abortable), the popup size variables are set to `auto` (the positioner variables keep their px values). The reset is owned by the Trigger that scheduled it: a newer activation by any Trigger aborts the pending reset before scheduling its own, so a stale reset or a stale size from a previous trigger is never applied after a later switch.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:115-120,139-147,171-183`; `internals/useAnimationsFinished.ts:46-163`. Test: [L4] `NavigationMenuTrigger.test.tsx` "does not let a previously hovered trigger reapply popup sizes after a later switch", "does not let interrupted mutation resizing reapply popup sizes after a later switch"; `NavigationMenuRoot.test.tsx` "ignores the initial open size reset once a trigger switch has started" (the first open's animation finishing after a switch does not reset to `auto`; the switch's does). Docs: CHANGELOG.md v1.4.1 "Fix stale popup size on rapid trigger hover (#4646)".
- **NAV-ANI-06** — MUST. When the activation is a switch from another open item (value non-null and different) by `click`, or by any non-touch pointer, the post-commit size pass of NAV-ANI-04 for that commit is skipped (the activation handler already ran the transition with the correct *current* size); the skip flag is one-shot and cleared when the menu closes.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:124,296-304,411-417,630-637`. Test: [L4] `NavigationMenuRoot.test.tsx` "updates popup sizing immediately when switching to a keepMounted trigger"; `NavigationMenuTrigger.test.tsx` "handles positioner width correctly".

### 9.4 Content mutations while open

- **NAV-ANI-07** — MUST. While a Trigger is active and Popup/Positioner elements exist, the active Content element is observed for child-list, subtree, character-data changes and `hidden` attribute changes. On a mutation: if the Root status is `'starting'` or the Popup has `data-starting-style`, the size is synced without animation (NAV-ANI-09); otherwise, if the popup size variables are currently fixed px values (a size transition is in flight), the *interrupted-resize* path runs (NAV-ANI-08) with the Popup's current offset size (falling back to the remembered size); otherwise the size transition of NAV-ANI-04 steps 3–4 runs with *current* = remembered size.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:277-294,354-409`. Test: [L4] `NavigationMenuRoot.test.tsx` "updates popup sizing when inline nested content is inserted while active" (`--positioner-height` becomes the new height, popup variables `auto` after animations), "updates popup sizing when a kept nested content hidden attribute changes", "updates popup sizing when switching kept inline nested content".
- **NAV-ANI-08** — MUST. *Interrupted resize.* Pending frames and the auto-size reset are cancelled; if the current size has a zero axis nothing else happens (the in-flight transition keeps its targets); otherwise both element sets are fixed to the current size, then **two frames** later all size variables are removed, the popup is measured (falling back to current), both sets are fixed to current again, and on the **next frame** (if still active) both sets are set to *measured* and an auto-size reset is scheduled.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:224-257`. Test: [L4] `NavigationMenuRoot.test.tsx` "keeps inline mutation resize interruptible when content updates again mid-transition" (`190px` written, final `260px`), "preserves the current size when an interrupted mutation temporarily measures zero"; `NavigationMenuTrigger.test.tsx` "does not let interrupted mutation resizing reapply popup sizes after a later switch".
- **NAV-ANI-09** — MUST. *Sync without animation.* Pending size frame and auto-size reset are cancelled, all size variables removed, the popup measured; if either axis is `0` nothing more happens; otherwise the remembered size is updated, the popup size variables are set to `auto` and the positioner size variables to the measured px size. This path runs for mutations during the entering phase (so content that mounts during opening — a nested `defaultValue` panel, kept content first moving into the portal — does not animate) and for window resizes.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:259-275,367-374`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not animate popup sizing when nested default content mounts during opening", "does not animate popup sizing when kept nested default content first moves into the portal" (every fixed `--popup-height` written is the final `220px`).
- **NAV-ANI-10** — MUST. While open and active, a window `resize` event runs the sync of NAV-ANI-09 on the **next frame** (one pending request at a time).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:333-352`. Test: [L4] `NavigationMenuRoot.test.tsx` "updates popup sizing when the window is resized while the popup is open". Docs: CHANGELOG.md v1.0.0-beta.1 "Handle layout resize while open (#2070)".
- **NAV-ANI-11** — MUST. The remembered size is updated from the Popup's `offsetWidth`/`offsetHeight` by a resize observer on the Popup element (when the platform provides one), reset to `0×0` whenever the popup unmounts, and used as the fallback whenever a measurement yields `0`.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:60,123,308-331`. Test: [L4] `NavigationMenuRoot.test.tsx` "seeds the popup width from the exiting panel when reopening after hovering a top-level link" (the exiting `675px` width is written before the reopening `500px`). Docs: CHANGELOG.md v1.4.0 "Fix transition width when reopening (#4587)".
- **NAV-ANI-12** — MUST. When a Trigger stops being the active trigger, its pending mutation and size frames are cancelled and its auto-size reset is aborted (only if it owns the pending reset); when the menu closes, every Trigger cancels its frames, aborts the reset, clears the patient-click timer, clears the skip flag and resets its pointer type.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:149-157,296-306`. Test: [L4] `NavigationMenuTrigger.test.tsx` "does not let a previously hovered trigger reapply popup sizes after a later switch".

### 9.5 Positioner instant marker and mount-transition suppression

Two independent mechanisms suppress transitions on the Positioner. The shared one (TRN-DOM-05, inline `transition: none` while the Root status is `'starting'`) is applied to the Positioner, the Popup and the Arrow. The NavigationMenu-specific one is the boolean `instant` state rendered as `data-instant` (NAV-DOM-28) for CSS to opt into; it is not a `'delay'`/`'dismiss'` string marker and TRN §10 Q5 is confirmed: it is a separate mechanism.

- **NAV-ANI-13** — MUST. A Positioner that mounts while the menu is already open (initially open menu, or a Positioner added while open) starts with `instant = true`; **0 ms** (next macrotask) after it is open it becomes `false` unless a resize suppression (NAV-ANI-14) is pending. A Positioner that mounts closed starts with `instant = false`.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:75-81,141-154`. Test: [L2] `NavigationMenuRoot.test.tsx` "suppresses the positioner transition for the first frame while initially open".
- **NAV-ANI-14** — MUST. While open, a window `resize` event synchronously sets `instant = true` and starts a **100 ms** timer that sets it back to `false` (restarted on every resize).
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:156-168`. Test: [L4] `NavigationMenuRoot.test.tsx` "updates popup sizing when the window is resized while the popup is open" (`data-instant` present at 0 ms, absent after 100 ms).
- **NAV-ANI-15** — MUST. While the Root status is `'starting'`, the Positioner, the Popup and the Arrow carry inline `transition: none`; it is removed when the status clears (next frame). This applies to every open, including an open of a kept-mounted Portal.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:170-177`; `utils/usePositioner.tsx:37`; `navigation-menu/popup/NavigationMenuPopup.tsx:64`; `navigation-menu/arrow/NavigationMenuArrow.tsx:39`; TRN-DOM-05. Test: [L4] `NavigationMenuRoot.test.tsx` "disables popup and arrow transitions while a kept portal opens". Docs: CHANGELOG.md v1.0.0 "Fix mount transitions on `Positioner` in Firefox (#3424)".

### 9.6 Content lifecycle

- **NAV-ANI-16** — MUST. A Content with `keepMounted` whose Root has no Viewport target yet (server rendering, or the Portal not yet rendered) is rendered inline in its Item with the `hidden` attribute. Once a Viewport target exists it is rendered inside the Viewport and never returns inline, even if the target later disappears (it then renders nothing until a target reappears). Without `keepMounted` the Content renders nothing on the server.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:66,135-159`. Test: [L4] `NavigationMenuContent.test.tsx` "keeps the content mounted (hidden) in the DOM when keepMounted is true" (server), "does not keep the content mounted in the DOM when keepMounted is false" (server), "keeps the content mounted (hidden) post-hydration when keepMounted is true".
- **NAV-ANI-17** — MUST. Content elements already moved into the Viewport stay there when another item becomes active (a kept Content is hidden in place; an exiting Content stays until its exit finishes) and when the menu closes with a kept Portal.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:136,161-174`. Test: [L4] `NavigationMenuContent.test.tsx` "moves content into the popup and keeps it there when switching triggers", "keeps content mounted inside the popup when closed if the portal is kept mounted".
- **NAV-ANI-18** — MUST. When a Content stops being active it enters `'ending'` (NAV-DOM-15), is positioned absolutely at the Viewport origin and made inert (NAV-DOM-17), and its own completion watcher waits on animations finished of the Content element; then its `mounted` flag becomes `false` (element removed, or `hidden` with `keepMounted`). An entering Content goes through `'starting'` for one commit, so entering and exiting panels overlap for a cross-fade. Server: relaxed, see §9.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:69-85,126-133`; TRN-STA-03/06. Test: [L4] `NavigationMenuRoot.test.tsx` "clears activation direction when controlled value closes externally after switching triggers" (`data-ending-style` on the exiting content), "closes on the short exit path after switching content".
- **NAV-ANI-19** — MUST. The active Content element is the one the size tracking observes (NAV-ANI-07): the registration happens when the Content element attaches while open, and is refreshed post-commit, pre-paint whenever a Content becomes open again while its element is unchanged (re-entering before its exit finished). It is cleared when the close completes.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:87-95,103-109`; `navigation-menu/root/NavigationMenuRoot.tsx:155`. Test: [L4] `NavigationMenuRoot.test.tsx` "updates popup sizing when switching kept inline nested content".
- **NAV-ANI-20** — MUST. A Content whose exit is interrupted by the popup unmounting (the menu closed and completed before the Content's own animations finished) has its `mounted` flag forced to `false` in the next render, so its next open starts from `'starting'`.
  Source: `navigation-menu/content/NavigationMenuContent.tsx:71-75`. Test: none.
- **NAV-ANI-21** — MUST. The size-freeze of NAV-ANI-03 and the fallbacks of NAV-ANI-04/08/09/11 together guarantee that a `0` measurement (an exiting popup, a detached element, a temporarily empty content) never writes a `0px` size variable; the previous positive size is kept instead.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:191-198,230-232,265-268`; `navigation-menu/root/NavigationMenuRoot.tsx:52-54`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not collapse auto-sized popup to zero on close if measurement temporarily returns 0", "preserves the current size when an interrupted mutation temporarily measures zero".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- NAV-STA-05 — row TRN-ANI-03/09/10: `mounted` is cleared in the first render batch after the completion message (about two round trips after the exit animation ends); JS sets `hidden` at animation end (optimistic convergent write) so the element is not visible meanwhile.
- NAV-ANI-02 — row TRN-ANI-03/09/10: the unmount on animations finished lands in the first render batch after the completion message, about two round trips later; optimistic convergent write as above.
- NAV-ANI-18 — row TRN-ANI-03/09/10: the exiting Content's `mounted` flag is cleared (element removed or `hidden`) in the first render batch after its completion message; optimistic convergent write as above.
- NAV-EVT-07 — row TRN-ANI-03/09/10: `onOpenChangeComplete(false)` is committed in the first render batch after the completion message, about two round trips after the exit animation ends.
- NAV-EVT-03 — row EVD-EVT-18/PSH-ANI-03: the `trigger-hover` (and activation-path) commit lands a round trip later; same-task animation queries do not see it.
- NAV-KEY-04 — row EVD-EVT-11/EVD-DIS-01/FLT-KEY-01: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after dispatch; JS prevents and stops whenever the owning popup is the innermost open one (ADR 04 §3 fallback). WebAssembly meets the spec through `decide()`.
- NAV-STA-16 — row RND-STA-01 for mirrored fields: `side`, `align`, `anchorHidden` and `transitionStatus` data attributes are JS-written at frame timing; the C# state fields (and so `ClassValue`/`StyleValue`) update one round trip later.

## 10. Positioning

The Positioner uses the shared anchor-positioning mechanism ([floating-and-positioning](../architecture/floating-and-positioning.md)) through a NavigationMenu-specific entry point that tolerates the floating root context arriving *after* the Positioner has rendered (the context is supplied by whichever Trigger becomes active): when no shared context exists yet, a private fallback root store is used.

- **NAV-POS-01** — MUST. Defaults: `positionMethod='absolute'`, `side='bottom'`, `align='center'`, `sideOffset=0`, `alignOffset=0`, `collisionBoundary='clipping-ancestors'`, `collisionPadding=5`, `arrowPadding=5`, `sticky=false`, `disableAnchorTracking=false`.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:53-70`. Test: [L2] `NavigationMenuPositioner.test.tsx` describeConformance (renders with defaults).
- **NAV-POS-02** — MUST. The anchor is the `anchor` prop when given; otherwise the active Trigger's element (the DOM reference of the shared floating root context). When the active trigger changes, the Positioner re-anchors to the new trigger and its position updates (the popup moves along the list).
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:108-111`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:476-483,547-552`. Test: [L4] `NavigationMenuTrigger.test.tsx` "repositions the positioner when switching triggers via hover" (`left` differs by more than 20 px).
- **NAV-POS-03** — MUST. The shift middleware uses the **layout viewport** as its root boundary (`shift: { rootBoundary: 'layoutViewport' }`), so pinch-zoom of the visual viewport does not shift the popup.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:126`; `navigation-menu/utils/useNavigationMenuAnchorPositioning.ts:11-24`. Test: [L2] `NavigationMenuPositioner.test.tsx` "uses the layout viewport". Docs: CHANGELOG.md v1.7.0 "Ignore pinch-zoom shifting (#4485)".
- **NAV-POS-04** — MUST. The adaptive-origin middleware is always configured, so the Positioner is positioned from the edge nearest the anchor (`top`/`bottom`, `left`/`right` chosen per side) rather than always from top/left; combined with NAV-DOM-31 this keeps the popup anchored while its size and position transition together for `side='top'`/`'left'`.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:128-130`; `utils/adaptiveOriginMiddleware.ts`; FLT-DOM-04. Test: none directly.
- **NAV-POS-05** — MUST. `collisionAvoidance` defaults to `{ fallbackAxisSide: 'none' }` for a top-level Root (dropdown behavior: never fall back to the perpendicular axis) and to `{ fallbackAxisSide: 'end' }` for a nested Root (popup behavior); a consumer value replaces the default entirely.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:64`; `internals/constants.ts:18-28`. Test: none.
- **NAV-POS-06** — MUST. The Portal's `keepMounted` is passed to anchor positioning as `keepMounted`, together with `mounted`, the shared floating root context and the Root's tree node id; while not mounted no positioning runs and the shared context's reference/floating elements are cleared (FLT-STA-09), so a kept-mounted Positioner does not track a stale anchor.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:72,112-127`; `internals/useAnchorPositioning.ts:460-470,494-501`. Test: [L2] `NavigationMenuRoot.test.tsx` "preserves popup size when controlled value closes externally with keepMounted portal".
- **NAV-POS-07** — MUST. The Positioner's inline style carries `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height` (px values) and `--transform-origin` as written by the shared positioning pass (FLT-DOM-05/06/07); consumers use `--available-height` to cap Popup/Content height (docs §Large menus).
  Source: `navigation-menu/positioner/NavigationMenuPositionerCssVars.ts:5-25`; see FLT-DOM-05, FLT-DOM-06, FLT-DOM-07. Test: none (NavigationMenu-specific). Docs: `components/navigation-menu/page.mdx` §Large menus.
- **NAV-POS-08** — MUST. The resolved `side` and `align` (after collision handling) are exposed as `data-side`/`data-align` on Positioner, Popup and Arrow, and `anchorHidden` as `data-anchor-hidden` on Positioner and Popup; logical sides (`inline-start`/`inline-end`) are reported as given, not converted to physical sides.
  Source: `navigation-menu/positioner/NavigationMenuPositioner.tsx:133-139`; `navigation-menu/popup/NavigationMenuPopup.tsx:32-38`; `navigation-menu/arrow/NavigationMenuArrow.tsx:27-32`. Test: [L4] `NavigationMenuRoot.test.tsx` "pins the popup to the right edge when side is %s so it grows leftward" (`data-side="inline-start"`).
- **NAV-POS-09** — MUST. The Arrow receives the computed arrow offset styles and the `uncentered` flag from the Positioner's positioning result (arrow padding `arrowPadding`), and its element is the arrow element registered with the positioning pass.
  Source: `navigation-menu/arrow/NavigationMenuArrow.tsx:24-38`; `internals/useAnchorPositioning.ts:625-650`. Test: [L4] `NavigationMenuArrow.test.tsx` describeConformance.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` / `CompositeRoot` / `CompositeItem` rendering | [rendering-and-composition](../architecture/rendering-and-composition.md) | Root: tag `nav`/`div`; List: `enabled: nested` for the plain path, `tag="ul"` for the composite path; Item `li`; Trigger `CompositeItem tag="button"` with `pressableTriggerOpenStateMapping`; Icon `span` with `triggerOpenStateMapping`; Content `CompositeRoot` with `popupStateMapping + transitionStatusMapping + activationDirection`; Link `CompositeItem tag="a"`; Backdrop/Popup `popupTransitionStateMapping`; Positioner via `usePositioner` (`popupStateMapping`); Arrow `popupStateMapping`. |
| `useControlled` | [shared-utilities](../architecture/shared-utilities.md) UTL-STA-01…06 | `{ controlled: value, default: defaultValue (null), name: 'NavigationMenu', state: 'value' }` (`NavigationMenuRoot.tsx:83-88`). |
| `Timeout` / `AnimationFrame` schedulers | [shared-utilities](../architecture/shared-utilities.md) UTL-EVT-01…05 | Trigger: patient-click `Timeout` (500 ms), `mutationFrame`, `resizeFrame`, `sizeFrame`; Positioner: `initialInstantTimeout` (0 ms), `resizeTimeout` (100 ms); List hover: `childClosedTimeout` (0 ms). |
| `useBaseUiId` / `useId` | [rendering-and-composition](../architecture/rendering-and-composition.md) RND-DOM-27…29 | Item fallback value (`base-ui-` prefix), Popup id (`base-ui-` prefix), Viewport id (no prefix), Portal container id (no prefix). |
| `inertValue`, `ownerVisuallyHidden`, `visuallyHidden`, `getCssDimensions`, `platform.engine.webkit` | [shared-utilities](../architecture/shared-utilities.md) UTL-DOM-13/15/16/23, UTL-EDGE-01 | Content `inert` while exiting; Viewport `inert`; owner span style; focus-guard style; popup measurement; `FocusGuard` VoiceOver role. |
| Change-event details, reasons, `preventUnmountOnClose` | [event-details-and-reasons](../architecture/event-details-and-reasons.md) EVD-API-01/09, EVD-EVT-21/22 | `createChangeEventDetails(reason, nativeEvent)` for `trigger-hover`/`trigger-press`/`list-navigation`/`focus-out`/`link-press`; `imperative-action` without event; `attachPreventUnmountOnClose` on every `setValue`. |
| `useButton` | [button-behavior](../architecture/button-behavior.md) §6.2 row `NavigationMenu.Trigger` | `native: nativeButton`, `disabled`, `focusableWhenDisabled: true`, composite inferred (List or Content is a composite root). |
| `CompositeRoot` (List) | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) CMP-API-01/02, CMP-KEY-01…11, CMP-DOM-01 | `orientation` = Root `orientation`, `loopFocus: false`, `stopEventPropagation` default `true`, Home/End off, no `disabledIndices`; omitted entirely when nested. |
| `CompositeRoot` (Content) | same | defaults: `orientation='both'`, `loopFocus=true`, `stopEventPropagation=true`, Home/End off. |
| `useFloatingRootContext` | [floating-and-positioning](../architecture/floating-and-positioning.md) §6.12, FLT-STR-11 | One per Trigger: `{ open, onOpenChange: handleOpenChange, elements: { reference: triggerElement, floating: positionerElement ?? viewportElement } }`; the active trigger's store becomes the Root's shared store (NAV-STA-07). |
| `useHoverReferenceInteraction` (Trigger) | FLT §6.7, FLT-PTR-11…19, FLT-ALG-24 | `enabled: (floating exists \|\| closed) && !disabled`, `move: false`, `handleClose: safePolygon({ blockPointerEvents: pointerType !== 'touch', getScope })`, `restMs: mounted && positioner ? 0 : delay`, `delay: { close: closeDelay }`, `triggerElementRef`, `getHandleCloseContext` (inline nested only, NAV-PTR-13). The floating **tree** is *not* passed explicitly: the hook takes the ancestor tree created by the top-level Root (FLT §10 Q6 answered: NavigationMenu passes the `nodeId`, not the tree). |
| `useHoverFloatingInteraction` (List) | FLT §6.8, FLT-PTR-20…25 | `enabled: sharedContext && (positioner \|\| viewport \|\| closed)`, `closeDelay`, `nodeId` = the Root's tree node id (explicit, because inline nested menus have no positioning session to publish it). |
| `useClick` (Trigger) | FLT §6.3, FLT-PTR-01…06, FLT-ALG-16 | `enabled: (positioner \|\| closed) && !disabled`, `stickIfOpen` = patient flag, `toggle: isActiveItem`; default `event: 'click'`. |
| `useDismiss` (List) | FLT §6.4, FLT-DIS-01…16, FLT-PTR-28/29, FLT-ALG-17 | `enabled: positioner \|\| closed`, `outsidePressEvent: 'intentional'`, `outsidePress(event)`: false when the target is inside `[data-base-ui-navigation-menu-trigger]`; `bubbles` default (Escape does not bubble past an open child node). Floating props on the List element, reference props on every Trigger. |
| `FloatingTree` / `FloatingNode` / `useFloatingNodeId` | FLT-STR-06…08, FLT-ALG-20 | Top-level Root creates the tree; every Root registers a node; Content wraps its subtree in the Root's node so nested popups become children of the menu's node. |
| `FloatingPortal` | FLT §6.11, FLT-API-13, FLT-DOM-11 | Default container `<body>`; no focus-manager state is ever set, so the portal renders no guards/owner of its own. |
| `useAnchorPositioning` (via `useNavigationMenuAnchorPositioning`) + `usePositioner` | FLT §6.1/6.2, FLT-DOM-01…08, FLT-POS-01…17, FLT-STA-09 | Parameters per §10; `floatingRootContext` may be `undefined` → private fallback store; `hidden: !mounted`, `inert: !open` → `pointer-events: none`. |
| `safePolygon` | FLT-ALG-15, FLT-DOM-19, FLT-PTR-19, FLT-PTR-21 | `blockPointerEvents` per pointer type; `getScope` → nearest `ul` of the trigger, or `null` for a nested Root with a Positioner (then the parent floating element or body per FLT-PTR-21). |
| `FocusGuard` | [popup-store-and-handles](../architecture/popup-store-and-handles.md) PSH-STR-08 | Four guards per active menu (two after the trigger, two around the Viewport). |
| `useTransitionStatus`, `useUnmountAfterClose`, `useOpenChangeComplete`, `useAnimationsFinished`, `getDisabledMountTransitionStyles` | [transitions-and-mounting](../architecture/transitions-and-mounting.md) §6.1–6.4, §6.6 | Root: `useUnmountAfterClose({ open, ref: popupElement, preventUnmountOnClose, onUnmount })` + second watcher on the Viewport target; Content: `useTransitionStatus(open)` defaults + completion watcher on the content element; Trigger: `useAnimationsFinished(popupElement)` for the auto-size reset (abort signal per activation); mount-transition suppression on Positioner, Popup, Arrow. |
| `DirectionProvider` / `useDirection` | [shared-utilities](../architecture/shared-utilities.md) §3.1 | Trigger open key (NAV-KEY-02); Popup physical-left detection (NAV-DOM-31); composite arrow mapping (CMP-KEY-03). |

## 12. Edge cases, invariants and dev warnings

- **NAV-EDGE-01** — MUST. Rendering List, Trigger, Icon, Content, Link, Portal, Backdrop, Positioner, Popup, Viewport or Arrow outside a Root fails with the error `Base UI: NavigationMenuRootContext is missing. Navigation Menu parts must be placed within <NavigationMenu.Root>.` (Item does not read the Root state and does not fail.)
  Source: `navigation-menu/root/NavigationMenuRootContext.ts:65-75`. Test: [L2] `NavigationMenuList.test.tsx` "throws a descriptive error when rendered outside <NavigationMenu.Root>".
- **NAV-EDGE-02** — MUST. Rendering Trigger, Icon or Content outside an Item fails with `Base UI: NavigationMenuItem parts must be used within a <NavigationMenu.Item>.`
  Source: `navigation-menu/item/NavigationMenuItemContext.ts:12-19`. Test: [L2] `NavigationMenuIcon.test.tsx` "throws a descriptive error when rendered outside <NavigationMenu.Item>".
- **NAV-EDGE-03** — MUST. Rendering a Positioner outside a Portal fails with `Base UI: <NavigationMenu.Portal> is missing.`
  Source: `navigation-menu/portal/NavigationMenuPortalContext.ts:6-12`. Test: [L2] `NavigationMenuPositioner.test.tsx` "throws a descriptive error when rendered outside <NavigationMenu.Portal>".
- **NAV-EDGE-04** — MUST. Rendering a Popup or an Arrow outside a Positioner fails with `Base UI: NavigationMenuPositionerContext is missing. NavigationMenuPositioner parts must be placed within <NavigationMenu.Positioner>.` (A Viewport outside a Positioner is allowed, NAV-STR-12.)
  Source: `navigation-menu/positioner/NavigationMenuPositionerContext.ts:17-24`. Test: [L2] `NavigationMenuArrow.test.tsx` "throws a descriptive error when rendered outside <NavigationMenu.Positioner>".
- **NAV-EDGE-05** — SHOULD. Outside production builds the Root's shared-state object carries the debugging name `NavigationMenuRootContext`; in production it carries none.
  Source: `navigation-menu/root/NavigationMenuRootContext.ts:55-57`. Test: [L2] `NavigationMenuRootContext.test.ts` "sets a development display name", "omits the display name in production".
- **NAV-EDGE-06** — MUST. A disabled Trigger ignores hover (no rest timer, no lock), click, touch tap and the open key, never calls `onValueChange`, keeps `aria-expanded="false"`, remains focusable, and does not take part in hover/click interactions even while another item is open (hovering it does not switch).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:128-130,438-440,620-622`. Test: [L4] `NavigationMenuRoot.test.tsx` "does not open on hover/click/touch/via keyboard when the trigger is disabled".
- **NAV-EDGE-07** — MUST. Items with identical `value`s are all considered active when that value is open (every such Trigger reports expanded and every such Content opens); the behavior is otherwise unspecified (§16 Q6). An Item whose value never matches any Trigger (a value-less Item wrapping a plain Link) is a valid list item.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:126`; `navigation-menu/content/NavigationMenuContent.tsx:62`. Test: none; `NavigationMenuRoot.test.tsx` `TestNavigationMenuWithTopLevelLink` fixture (Item without value holding a Link).
- **NAV-EDGE-08** — MUST. When the platform lacks `ResizeObserver`, the remembered size (NAV-ANI-11) is updated only by the sync path (NAV-ANI-09); when it lacks `MutationObserver`, content mutations do not trigger resizing. Neither absence is an error.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:314-317,357-365`. Test: [L3] `NavigationMenuRoot.test.tsx` "updates popup sizing when inline nested content is inserted while active" (`ResizeObserver` removed), "preserves the current size when an interrupted mutation temporarily measures zero".
- **NAV-EDGE-09** — SHOULD. Switching `value` between `undefined` and a defined value after mount, or changing `defaultValue` after mount, logs the shared controlled-state diagnostics with `name: 'NavigationMenu'`, `state: 'value'` (UTL-EDGE-22…24).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:83-88`; `packages/utils/src/useControlled.ts:48-80`. Test: none.
- **NAV-EDGE-10** — MUST. The active Trigger unmounting while the menu is open (e.g. the consumer replaces it with a plain link in the same update that closes the menu) releases the pointer-events lock it applied (NAV-PTR-11); the Root keeps the last known trigger element for focus return only while an unmount completion is pending.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:498-500`; `navigation-menu/root/NavigationMenuRoot.tsx:143-148`. Test: [L4] `NavigationMenuTrigger.test.tsx` "releases the pointer-events lock on the list when the open trigger unmounts".
- **NAV-EDGE-11** — MUST. Under a WebKit engine the behavior is identical: the pointer-events lock scope is still the nearest `ul` (never the body unless there is no `ul`), and the inline nested lock is still scoped to the nested List. No NavigationMenu code branches on the engine; only the shared focus-guard role (NAV-ARIA-05) and dismiss timing do.
  Source: `navigation-menu/` (no `platform` usage); `utils/FocusGuard.tsx:21`; `floating-ui-react/hooks/useDismiss.ts:325-330`. Test: [L4] `NavigationMenuRoot.webkit.test.tsx` "blocks top-level pointer events for hover-open menus", "keeps nested safePolygon pointer events scoped on WebKit".

Invariants: the four size variables are either all absent, or the positioner pair holds px values with the popup pair holding px or `auto`; `data-popup-open` and `aria-expanded="true"` always coincide on a Trigger; a Content is never a DOM child of its `<li>` after a Viewport target has existed; focus guards exist only for the active trigger.

### 12.1 Reading direction (I18N)

- **NAV-I18N-01** — MUST. In RTL reading direction the vertical open key is <kbd>ArrowLeft</kbd> instead of <kbd>ArrowRight</kbd> (NAV-KEY-02); horizontal menus are unaffected (<kbd>ArrowDown</kbd> opens in both directions).
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:684`. Test: [L4] `NavigationMenuTrigger.test.tsx` "opens a vertical menu with the mirrored arrow key in RTL mode".
- **NAV-I18N-02** — MUST. In RTL the top-level List's horizontal arrow mapping is mirrored (<kbd>ArrowLeft</kbd> moves forward, <kbd>ArrowRight</kbd> backward) per CMP-KEY-03; the List's propagation guard (NAV-KEY-06) still stops both horizontal keys.
  Source: `navigation-menu/list/NavigationMenuList.tsx:80-83,113-123`; `internals/composite/root/useCompositeRoot.ts:221-226`. Test: none.
- **NAV-I18N-03** — MUST. The Popup's physical-left detection (NAV-DOM-31) maps `inline-start` to physical left in LTR and `inline-end` to physical left in RTL; `left` is physical left in both.
  Source: `navigation-menu/popup/NavigationMenuPopup.tsx:41-47`. Test: [L4] `NavigationMenuRoot.test.tsx` "pins the popup to the right edge when side is %s so it grows leftward".

## 13. Behaviors evidenced by tests

Coverage of every test by the requirements above, plus requirements that exist only because a test asserts them. `describeConformance` rows cite the shared obligations RND-DOM-36…49 and record only the options passed.

### `root/NavigationMenuRoot.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Root />`, `refInstanceof: HTMLElement`) | NAV-STR-01, NAV-DOM-03; RND-DOM-36…48 |
| "does not apply aria-orientation to the top-level list or root element" | NAV-ARIA-07, NAV-DOM-02, NAV-DOM-04 |
| "does not apply aria-orientation to nested lists or root elements" | NAV-ARIA-07, NAV-STR-01, NAV-STA-04 |
| "opens on hover with mouse input" | NAV-PTR-01, NAV-ARIA-01 |
| "blocks pointer events on the list while traversing from a top-level trigger to the popup" | NAV-PTR-10, NAV-PTR-11 |
| "reapplies top-level safePolygon pointer events after returning from the popup and switching triggers" | NAV-PTR-12, NAV-PTR-11, NAV-PTR-02 |
| "keeps top-level safePolygon pointer events active when switching directly to a different trigger" | NAV-PTR-02, NAV-PTR-12, NAV-EVT-03, NAV-ARIA-01 |
| "clears top-level safePolygon pointer events on trigger pointerdown" | NAV-PTR-11 |
| "scopes safePolygon pointer events to the document for a custom list element" | NAV-PTR-10, NAV-PTR-11 |
| "blocks pointer events on sibling top-level triggers when opened through real hover" | NAV-PTR-10 |
| "opens on click with mouse input" | NAV-PTR-04, NAV-DOM-11, NAV-API-03 |
| "does not open on hover with touch input" | NAV-PTR-03, NAV-STA-14 |
| "reports %s activation direction when switching in either direction" (horizontal / vertical) | NAV-STA-06, NAV-DOM-18, NAV-API-08, NAV-PTR-18 |
| "opens on click with touch input" | NAV-PTR-04, NAV-PTR-03 |
| "restores hover open after a touch click closes outside" | NAV-PTR-15, NAV-PTR-16, NAV-STA-14 |
| "restores hover open after touching a nested submenu trigger and closing outside" | NAV-PTR-15, NAV-PTR-16 |
| "restores hover open after a quick click then trigger switch" | NAV-PTR-07, NAV-PTR-08 |
| "closes after pointerdown on a link in a hover-open popup when pointer leaves" | NAV-PTR-14, NAV-PTR-08 |
| "does not close menu when clicking a different trigger with mouse" | NAV-PTR-06, NAV-PTR-17, NAV-DOM-09, NAV-ARIA-01 |
| "does not close menu when clicking a different trigger on touch" | NAV-PTR-06, NAV-PTR-03 |
| "returns focus to trigger when closing menu" | NAV-FOC-05, NAV-KEY-04, NAV-DIS-01 |
| "respects focus outside when clicking menu" | NAV-PTR-16, NAV-FOC-06, NAV-FOC-01, NAV-DIS-02 |
| "does not restore focus to the trigger when closed via hover" | NAV-FOC-06, NAV-STA-15, NAV-PTR-08 |
| "does not restore focus to the trigger when focus moves outside" | NAV-FOC-06, NAV-KEY-11 |
| "stays open if hovered then clicked within the patient threshold" | NAV-PTR-05, NAV-STA-13 |
| "closes if hovered then clicked after the patient threshold" | NAV-PTR-05, NAV-STA-13 |
| "should respect defaultValue" | NAV-API-02, NAV-STA-01 |
| "disables popup and arrow transitions while a kept portal opens" | NAV-ANI-15, NAV-DOM-36, NAV-ANI-01 |
| "should call onValueChange when value changes" | NAV-EVT-01, NAV-EVT-06, NAV-PTR-06, NAV-STA-02 |
| "does not open when onValueChange cancels the interaction" | NAV-EVT-04 |
| "does not emit a duplicate onValueChange when switching items via keyboard" | NAV-KEY-01, NAV-KEY-05, NAV-EVT-06 |
| "treats a falsy item value (%s) as a valid open value" (`0`, `''`, `false`) | NAV-STA-03, NAV-API-11 |
| "should be controlled by value prop" | NAV-API-01, NAV-STA-01, NAV-PTR-02 |
| "preserves popup size when controlled value closes externally" / "preserves popup size when controlled value closes externally with keepMounted portal" | NAV-ANI-03, NAV-ANI-02, NAV-STA-05, NAV-DOM-14, NAV-DOM-32, NAV-EVT-11, NAV-API-15 |
| "clears activation direction when controlled value closes externally after switching triggers" | NAV-STA-06, NAV-DOM-18, NAV-DOM-15, NAV-STA-12, NAV-ANI-18 |
| "respects custom delay value" | NAV-API-06, NAV-PTR-01 |
| "respects custom closeDelay value" | NAV-API-07, NAV-PTR-08, NAV-DIS-04 |
| "does not open on hover when the trigger is disabled" | NAV-API-10, NAV-EDGE-06 |
| "does not open on click when the trigger is disabled" | NAV-API-10, NAV-EDGE-06 |
| "does not open on touch when the trigger is disabled" | NAV-API-10, NAV-EDGE-06 |
| "does not open via keyboard when the trigger is disabled" | NAV-API-10, NAV-EDGE-06, NAV-KEY-14 |
| "automatically unmounts with an actions ref and completes closing once" | NAV-API-04, NAV-EVT-07, NAV-EVT-09, NAV-STA-05, NAV-ANI-01 |
| "keeps the popup mounted until the unmount action completes closing" | NAV-EVT-05, NAV-EVT-11, NAV-EVT-07 |
| "closes through the `close` action so `onValueChange` can opt out" | NAV-EVT-08, NAV-EVT-01, NAV-EVT-02 |
| "keeps the opt-out when the `close` action is called again while closed" | NAV-EVT-05, NAV-EVT-08 |
| "keeps the opt-out when focus leaves the trigger during a manual exit" | NAV-EVT-05, NAV-FOC-10 |
| "ignores `unmount` while the popup is open" | NAV-EVT-09, NAV-EVT-07 |
| "still unmounts on a later close after `unmount` was called while open" | NAV-EVT-09 |
| "unmounts immediately when called while the popup is closing" | NAV-EVT-09 |
| "pins the popup to the right edge when side is %s so it grows leftward" (left/LTR, inline-start/LTR, inline-end/RTL) | NAV-DOM-31, NAV-DOM-32, NAV-POS-08, NAV-I18N-03 |
| "moves focus through the menu correctly" | NAV-KEY-10, NAV-KEY-11, NAV-KEY-12, NAV-FOC-02, NAV-FOC-03, NAV-FOC-04, NAV-DOM-06, NAV-DOM-21, NAV-KEY-17 |
| "closes the menu when tabbing forward out" | NAV-KEY-11, NAV-DIS-03 |
| "closes after tabbing out of arbitrary tabbable content" | NAV-KEY-10, NAV-KEY-11 |
| "returns focus to the trigger when no viewport focus guard is rendered" | NAV-FOC-13, NAV-DOM-10, NAV-STR-04 |
| "returns to the last submenu item when shift+tabbing after tabbing out of it" | NAV-KEY-12, NAV-FOC-08 |
| "closes the menu when tabbing back out" | NAV-KEY-13, NAV-FOC-10, NAV-DIS-03 |
| "keeps a hover-open menu open when pointerdown happens on a nested dialog trigger" | NAV-PTR-14, NAV-DIS-07 |
| "keeps the menu open when interacting with a nested dialog" / "keeps the menu open when interacting with a nested popover" | NAV-DIS-07, NAV-FOC-12, NAV-PTR-16 |
| "opens nested menu on hover and stays open when hovering over nested popup" | NAV-STR-14, NAV-PTR-19, NAV-PTR-09 |
| "handles inline nested menu without positioner/popup correctly" | NAV-STR-14, NAV-STA-08, NAV-PTR-20 |
| "keeps parent menu open when hovering inline nested triggers without defaultValue" | NAV-PTR-20 |
| "closes the parent menu after a nested submenu closes on delayed hover-out" | NAV-PTR-19, NAV-PTR-09 |
| "closes the parent menu when a nested link with closeOnClick is clicked" | NAV-DIS-09 |
| "calls onValueChange on the parent root when nested closeOnClick link is clicked" | NAV-EVT-10, NAV-DIS-09 |
| "closes all levels when a deeply nested link with closeOnClick is clicked" | NAV-DIS-09 |
| "renders viewport content correctly for inline nested menu" | NAV-STR-12, NAV-DOM-34 |
| "switches content in viewport when hovering different nested triggers" | NAV-PTR-20 |
| "scopes inline safePolygon pointer events to the submenu list while traversing to the viewport" | NAV-PTR-13, NAV-PTR-10, NAV-PTR-11 |
| "keeps an inline submenu open while traversing toward a viewport on the %s" (right/left/bottom/top) | NAV-PTR-13 |
| "clears inline safePolygon pointer events when the pointer leaves the traversal path" | NAV-PTR-13 |
| "keeps inline safePolygon pointer events when returning to the original trigger before traversing again" | NAV-PTR-13 |
| "closes inline nested viewport when parent menu closes" | NAV-DIS-06 |
| "maintains inline viewport state when hovering between triggers and content" | NAV-PTR-09 |
| "handles click interactions on inline nested menu triggers" | NAV-PTR-17, NAV-PTR-06 |
| "adds popup-open data attributes to the active icon" | NAV-DOM-13, NAV-STA-11 |
| "treats a falsy value (%s) as open for inline nested trigger interactions" | NAV-STA-03 |
| "propagates a nested close with a falsy open value to the parent root" | NAV-STA-03, NAV-DIS-09 |
| "allows arrow key navigation to submenu triggers" | NAV-KEY-08, NAV-KEY-09 |
| "allows arrow key navigation with 3+ levels of nesting" | NAV-KEY-09, NAV-STR-14 |
| "updates popup sizing when inline nested content is inserted while active" | NAV-ANI-07, NAV-ANI-05, NAV-EDGE-08 |
| "does not animate popup sizing when nested default content mounts during opening" | NAV-ANI-09 |
| "does not animate popup sizing when kept nested default content first moves into the portal" | NAV-ANI-09 |
| "updates popup sizing when switching kept inline nested content" | NAV-ANI-07, NAV-DOM-16, NAV-ANI-19 |
| "updates popup sizing when a kept nested content hidden attribute changes" | NAV-ANI-07, NAV-DOM-16 |
| "keeps inline mutation resize interruptible when content updates again mid-transition" | NAV-ANI-08 |
| "preserves the current size when an interrupted mutation temporarily measures zero" | NAV-ANI-08, NAV-ANI-21, NAV-EDGE-08 |
| "updates popup sizing when the window is resized while the popup is open" | NAV-ANI-10, NAV-ANI-14, NAV-DOM-28 |
| "updates popup sizing immediately when switching to a keepMounted trigger" | NAV-ANI-04, NAV-ANI-06 |
| "ignores the initial open size reset once a trigger switch has started" | NAV-ANI-05 |
| "seeds the popup width from the exiting panel when reopening after hovering a top-level link" | NAV-ANI-11, NAV-STA-12 |
| "closes on the short exit path after switching content" | NAV-ANI-02, NAV-EVT-07, NAV-API-05, NAV-KEY-04 |
| "does not collapse auto-sized popup to zero on close if measurement temporarily returns 0" | NAV-ANI-03, NAV-ANI-21, NAV-FOC-10 |
| "tabs from the last link of the last nested panel to the next top-level trigger" | NAV-KEY-15 |
| "tabs between nested triggers and links without opening inactive panels" | NAV-KEY-16 |
| "returns to the last inline submenu item when shift+tabbing after leaving it" | NAV-KEY-12 |
| "suppresses the positioner transition for the first frame while initially open" | NAV-ANI-13, NAV-DOM-28 |

- **NAV-FOC-13** — MUST. When the trailing outside guard after the active Trigger receives focus from outside the floating element and no Viewport guard exists (Popup without a Viewport), focus is placed back on the Trigger and the menu stays open.
  Source: `navigation-menu/trigger/NavigationMenuTrigger.tsx:753-759`. Test: [L4] `NavigationMenuRoot.test.tsx` "returns focus to the trigger when no viewport focus guard is rendered".

### `root/NavigationMenuRoot.webkit.test.tsx`

| Test | Requirements |
|---|---|
| "blocks top-level pointer events for hover-open menus" | NAV-EDGE-11, NAV-PTR-10 |
| "keeps nested safePolygon pointer events scoped on WebKit" | NAV-EDGE-11, NAV-PTR-13 |

### `root/NavigationMenuRootContext.test.ts`

| Test | Requirements |
|---|---|
| "sets a development display name" / "omits the display name in production" | NAV-EDGE-05 |

### `root/NavigationMenuRoot.spec.tsx`

Type-level only: generic `Value` narrowing of `value`/`defaultValue`/`onValueChange` (§3.14).

### `trigger/NavigationMenuTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`, `testRenderPropWith: 'button'`) | NAV-STR-04, NAV-API-09, NAV-DOM-07; RND-DOM-36…49 |
| "applies the data-disabled style hook only when disabled" | NAV-DOM-08, NAV-API-10 |
| "opens a vertical menu with the mirrored arrow key in RTL mode" | NAV-KEY-02, NAV-I18N-01 |
| "keeps focus on the trigger when opened with %s" (ArrowDown / Enter / Space) | NAV-KEY-17, NAV-KEY-01, NAV-KEY-03, NAV-FOC-01 |
| "handles focus and positioner height" | NAV-ANI-04, NAV-DOM-29, NAV-KEY-01, NAV-KEY-05, NAV-KEY-10, NAV-KEY-12, NAV-STA-10 |
| "handles positioner width correctly" | NAV-ANI-04, NAV-ANI-06, NAV-DOM-29 |
| "repositions the positioner when switching triggers via hover" | NAV-POS-02, NAV-STA-07, NAV-PTR-18 |
| "does not let a previously hovered trigger reapply popup sizes after a later switch" | NAV-ANI-05, NAV-ANI-12, NAV-DOM-33 |
| "does not let interrupted mutation resizing reapply popup sizes after a later switch" | NAV-ANI-05, NAV-ANI-08 |
| "releases the pointer-events lock on the list when the open trigger unmounts" | NAV-PTR-11, NAV-EDGE-10 |
| "releases the pointer-events lock when the pointer sweeps across a trigger without the menu opening" | NAV-PTR-11, NAV-PTR-01 |

### `content/NavigationMenuContent.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance — **skipped** (`describeConformance.skip`) | NAV-STR-06 (§16 Q1) |
| "keeps the content mounted (hidden) in the DOM when keepMounted is true" (server) | NAV-ANI-16, NAV-API-12 |
| "does not keep the content mounted in the DOM when keepMounted is false" (server) | NAV-ANI-16, NAV-API-12 |
| "keeps the content mounted (hidden) post-hydration when keepMounted is true" | NAV-API-12, NAV-ANI-16, NAV-DOM-16 |
| "does not keep the content mounted post-hydration when keepMounted is false" | NAV-API-12, NAV-ANI-01 |
| "moves content into the popup and keeps it there when switching triggers" | NAV-STR-06, NAV-DOM-05, NAV-DOM-19, NAV-ANI-17 |
| "keeps content mounted inside the popup when closed if the portal is kept mounted" | NAV-API-15, NAV-DOM-16, NAV-ANI-17, NAV-KEY-04 |

### `list/NavigationMenuList.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLUListElement`) | NAV-STR-02, NAV-DOM-04; RND-DOM-36…48 |
| "throws a descriptive error when rendered outside <NavigationMenu.Root>" | NAV-EDGE-01 |
| "stops vertical navigation keys from escaping the list" | NAV-KEY-06, NAV-API-08 |
| "navigates from the focused trigger after an earlier item is removed" | NAV-KEY-05 |

### `link/NavigationMenuLink.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLAnchorElement`, inside `Root > List`) | NAV-STR-07, NAV-DOM-20; RND-DOM-36…48 |
| "closes the menu when clicking a link when true" | NAV-API-14, NAV-DIS-09 |
| "does not close the menu when clicking a link when false" | NAV-API-14 |
| "when `true`, renders with aria-current=\"page\"" / "when `false`, does not render with aria-current=\"page\"" | NAV-ARIA-04, NAV-API-13 |
| "keeps the menu open when a link loses focus without a related target" | NAV-FOC-11 |

### `link/NavigationMenuLink.spec.tsx`

Type-level only: the render callback receives native `<a>` props (§3.14).

### `positioner/NavigationMenuPositioner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root value="test" > Portal`) | NAV-STR-10, NAV-DOM-25, NAV-POS-01; RND-DOM-36…48 |
| "uses the layout viewport" | NAV-POS-03 |
| "throws a descriptive error when rendered outside <NavigationMenu.Portal>" | NAV-EDGE-03 |

### `popup/NavigationMenuPopup.test.tsx`, `portal/NavigationMenuPortal.test.tsx`, `backdrop/NavigationMenuBackdrop.test.tsx`, `viewport/NavigationMenuViewport.test.tsx`, `item/NavigationMenuItem.test.tsx`

| Test | Requirements |
|---|---|
| Popup describeConformance (`refInstanceof: HTMLElement`, inside `Root value="test" > Portal > Positioner`) | NAV-STR-11, NAV-DOM-30; RND-DOM-36…48 |
| Portal describeConformance (`refInstanceof: HTMLDivElement`, inside `Root value="item"`) | NAV-STR-08, NAV-DOM-22; RND-DOM-36…48 |
| Backdrop describeConformance (`refInstanceof: HTMLDivElement`, inside `Root`) | NAV-STR-09, NAV-DOM-23; RND-DOM-36…48 |
| Viewport describeConformance (`refInstanceof: HTMLDivElement`, inside `Root`) | NAV-STR-12, NAV-DOM-34; RND-DOM-36…48 |
| Item describeConformance (`refInstanceof: HTMLLIElement`, inside `Root`) | NAV-STR-03, NAV-DOM-05; RND-DOM-36…48 |

### `arrow/NavigationMenuArrow.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root value="test" > Portal > Positioner`) | NAV-STR-13, NAV-DOM-36, NAV-DOM-37; RND-DOM-36…48 |
| "throws a descriptive error when rendered outside <NavigationMenu.Positioner>" | NAV-EDGE-04 |

### `icon/NavigationMenuIcon.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLSpanElement`, inside `Root > Item`) | NAV-STR-05, NAV-DOM-12; RND-DOM-36…48 |
| "throws a descriptive error when rendered outside <NavigationMenu.Item>" | NAV-EDGE-02, NAV-STR-03 |

## 14. React-specific notes and porting considerations

Non-normative.

- **Synchronous flushes.** Four paths force a synchronous re-render inside an event handler: the activation path (`ReactDOM.flushSync` in `handleActivation`, `NavigationMenuTrigger.tsx:555`), hover-reason open changes (`:470`), the trailing outside focus guard's `viewportInert` reset before it focuses the inner guard (`:755`), and the resize `instant` flag (`NavigationMenuPositioner.tsx:157`). They exist so that DOM measurements taken immediately afterwards (the popup size transition, the lock application) see the new content. A port with batched rendering must either flush equivalently or restructure the size pass so that *current* is measured before the content swap and *measured* after it has been laid out. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4, "committed synchronously") and §Relaxation table (EVD-EVT-18 row).
- **Pre-paint vs after-paint work.** Pre-paint (layout effects): active-trigger hand-off (`:547-552`), the active-item size pass (`:411-430`), the resize observer install (`:314-331`), the close-size freeze (`NavigationMenuRoot.tsx:172-196`), Content's current-element refresh (`NavigationMenuContent.tsx:91-95`), the Positioner focus-in/out listeners and the Viewport's previous-trigger record. After paint (passive effects): the mutation observer and window-resize listeners, the `instant` timers, the close cleanup of timers/frames, and `viewportInert` reset. Only the ordering stated in §9 is normative. Port: see decisions/01-hosting-model-and-timing.md §Decision (item 4); the size pass, `ResizeObserver`/`MutationObserver` measurement and the `--popup-*`/`--positioner-*` writes fall under decisions/04-js-interop-boundary.md §1 (`dom` and `transitions` modules) and §2 (ownership list; see §16 note on whether these variables are JS-owned).
- **Mutable cells read during handlers.** `prevSizeRef`, `skipAutoSizeSyncRef`, `triggerElementRef`, `isActiveItemRef`, `popupAutoSizeResetRef` (shared across triggers through the Root), `currentContentRef`, `prevTriggerElementRef`, `closeReasonRef`, the four guard refs and `needsInitialInstantResetRef` are instance fields, not render state; a port models them as plain fields on the trigger/root objects. The shared `popupAutoSizeResetRef` carries an `AbortController` and an *owner* (item value) so that any trigger can cancel another's pending reset (NAV-ANI-05).
- **Per-trigger floating root stores.** Each Trigger creates its own root store (`useFloatingRootContext`) whose trigger map is empty; the "inactive trigger" fast paths of the shared hover hook therefore never fire, and all trigger-switching logic lives in `handleActivation`. The Root merely republishes the active trigger's store so the List, Positioner and Viewport read the active anchor from it. A port may keep one store per trigger or one per root with an explicit "active trigger" field, as long as NAV-STA-07 holds. Port: see decisions/04-js-interop-boundary.md §1 (`interactions` module: hover with safe polygon, floating tree) and decisions/05-state-store-and-event-details.md §1.
- **Fallback root store.** List, Positioner and Viewport read from `getEmptyRootContext()` (a never-opening store) while no trigger has been active, so hooks have a stable store identity before the first open; `useNavigationMenuAnchorPositioning` likewise substitutes a private store so the positioning session can exist before the active store arrives. These are React hook-ordering devices; the observable contract is that positioning starts once a Positioner is mounted and an anchor exists. Port: see decisions/05-state-store-and-event-details.md §1 (NullStore fallback) and decisions/03-positioning-engine.md §Decision.
- **Portaling Content.** `ReactDOM.createPortal` keeps the Content in the Item's component tree (so it reads Item/Root state) while rendering into the Viewport. The `hasMountedInPortal` latch (NAV-ANI-16) prevents the element from moving back inline, which would destroy and re-create it. A port that moves DOM nodes imperatively must preserve element identity across trigger switches (the mutation observer and the size tracking depend on it). Port: see decisions/04-js-interop-boundary.md §5 (DOM move after first render, restored before dispose).
- **Stable callbacks.** `handleValueChange`, `handleInterruptedMutationResize`, `syncCurrentSize`, `getMutationBaseline`, `cancelAutoSizeReset`, `handleOpenEvent`, `handleCurrentContentRef` and the Root's `setValue` are stable-identity wrappers reading the latest render values; `setValue`'s `value` comparison (NAV-EVT-06) therefore sees the value committed by a preceding synchronous flush in the same event.
- **`inert` attribute.** Written through `inertValue` for React 17/18 compatibility (UTL-DOM-16); the DOM result is the boolean attribute.
- **React 17.** Generated ids (`base-ui-…`) appear only after the first commit on React 17 (RND-EDGE-09): the Item's fallback value, the Popup id and the Viewport id are `undefined` in the first render there; nothing else differs. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5) and §Requirements relaxed or withdrawn for the port (RND-DOM-27 row: the Viewport's unprefixed id, NAV-API-18, becomes `base-ui-<n>`).
- **Development-only.** `NavigationMenuRootContext.displayName` (NAV-EDGE-05) and the `useControlled` diagnostics (NAV-EDGE-09) are the only development-mode branches; no NavigationMenu-specific `warn`/`console.error` exists.
- **Test harness.** The size tests stub `offsetWidth`/`offsetHeight`, `getAnimations`, `ResizeObserver` and `getBoundingClientRect`; a port's test translation needs equivalent hooks for layout measurement and animation completion (TRN-EDGE-01 flag). Port: see decisions/06-testing-harness.md §1 and §2.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| NAV-STR-01 | MUST | NavigationMenu.Root renders a <nav> when it is a top-level menu and a <div> when it is nested (NAV-STA-04). It… | 2 |
| NAV-STR-02 | MUST | NavigationMenu.List renders a <ul> and MUST be a descendant of a Root (otherwise NAV-EDGE-01). At the top leve… | 2 |
| NAV-STR-03 | MUST | NavigationMenu.Item renders an <li> and provides its value (explicit or generated, NAV-STA-10) to its descenda… | 2 |
| NAV-STR-04 | MUST | NavigationMenu.Trigger renders a <button> and MUST be inside an Item inside a Root. While it is the active tri… | 2 |
| NAV-STR-05 | MUST | NavigationMenu.Icon renders a <span> whose default children are the text ▼; it MUST be inside an Item inside a… | 2 |
| NAV-STR-06 | MUST | NavigationMenu.Content renders a <div> that is a composite root for the links and nested triggers inside it. I… | 2 |
| NAV-STR-07 | MUST | NavigationMenu.Link renders an <a> and MUST be inside a Root; it is a composite item and therefore MUST also b… | 2 |
| NAV-STR-08 | MUST | NavigationMenu.Portal renders a <div> portal container appended to <body> (or to container) and MUST be inside… | 2 |
| NAV-STR-09 | MUST | NavigationMenu.Backdrop renders a <div> and requires only a Root ancestor (it may be placed inside or outside … | 2 |
| NAV-STR-10 | MUST | NavigationMenu.Positioner renders a <div> and MUST be inside a Portal inside a Root; it provides positioning s… | 2 |
| NAV-STR-11 | MUST | NavigationMenu.Popup renders a <nav> and MUST be inside a Positioner (NAV-EDGE-04). | 2 |
| NAV-STR-12 | MUST | NavigationMenu.Viewport renders a <div> and MUST be inside a Root. A Positioner ancestor is optional: with one… | 2 |
| NAV-STR-13 | MUST | NavigationMenu.Arrow renders a <div> and MUST be inside a Positioner (NAV-EDGE-04). | 2 |
| NAV-STR-14 | MUST | A Root rendered inside another Root's Content is a nested menu. Two forms are supported: (a) portaled — the ne… | 2 |
| NAV-API-01 | MUST | value is the consumer-owned value. When it is not undefined on the first render the component never changes th… | 3.1 |
| NAV-API-02 | MUST | defaultValue defaults to null and is the initial value of the component-owned state; later changes are ignored… | 3.1 |
| NAV-API-03 | MUST | onValueChange is optional; when absent, interactions still change the component-owned value (uncontrolled) or … | 3.1 |
| NAV-API-04 | MUST | actionsRef, when supplied, receives an object { unmount(): void; close(): void } whose members are specified b… | 3.1 |
| NAV-API-05 | MUST | onOpenChangeComplete is optional and is invoked only as onOpenChangeComplete(false) (NAV-EVT-07). | 3.1 |
| NAV-API-06 | MUST | delay defaults to 50 and is the rest delay (NAV-PTR-01) used when the popup is not yet mounted or there is no … | 3.1 |
| NAV-API-07 | MUST | closeDelay defaults to 50 and is the hover close delay of both the Trigger and the popup (NAV-PTR-08). | 3.1 |
| NAV-API-08 | MUST | orientation defaults to 'horizontal'. It is passed as the top-level List's composite orientation (NAV-KEY-05),… | 3.1 |
| NAV-API-09 | MUST | nativeButton defaults to true and is passed as the native option of the shared button mechanism (§11): true → … | 3.4 |
| NAV-API-10 | MUST | disabled defaults to false. While true: hover, click, touch and keyboard opening are all suppressed and onValu… | 3.4 |
| NAV-API-11 | MUST | value defaults to a generated document-unique id of the form base-ui-<token> (RND-DOM-27) computed once per It… | 3.3 |
| NAV-API-12 | MUST | keepMounted defaults to false. When false, the content element exists only while its own transition state is m… | 3.6 |
| NAV-API-13 | MUST | active defaults to false; true renders aria-current="page" (NAV-ARIA-04) and data-active (NAV-DOM-20). It has … | 3.7 |
| NAV-API-14 | MUST | closeOnClick defaults to false; when true a click on the link requests a close with reason link-press (NAV-EVT… | 3.7 |
| NAV-API-15 | MUST | keepMounted defaults to false. The Portal (and its subtree) is rendered iff mounted // keepMounted (NAV-ANI-01… | 3.8 |
| NAV-API-16 | MUST | The Portal passes container (default undefined) and its element props to the shared portal mechanism; the obse… | 3.8 |
| NAV-API-17 | MUST | id defaults to a generated base-ui-<token> (RND-DOM-27); a consumer id is used verbatim. The rendered id is wh… | 3.11 |
| NAV-API-18 | MUST | The Viewport's id defaults to a generated document-unique id (without the base-ui- prefix) and is the target o… | 3.12 |
| NAV-STA-01 | MUST | The value is consumer-owned iff value !== undefined on the first render, otherwise component-owned starting fr… | 4.1 |
| NAV-STA-02 | MUST | open is derived: open = value != null. There is no separate open flag; closing is setting the value to null. | 4.1 |
| NAV-STA-03 | MUST | Falsy non-nullish values (0, '', false) are valid open values: an Item with such a value opens, its Trigger re… | 4.1 |
| NAV-STA-04 | MUST | A Root is nested iff it is rendered inside the floating node of another Root (i.e. inside another Root's Conte… | 4.1 |
| NAV-STA-05 | MUST | mounted becomes true in the same commit as open becomes true (status 'starting', cleared next frame), becomes … | 4.2 |
| NAV-STA-06 | MUST | activationDirection is null while closed. When a Trigger is activated (hover switch, click, keyboard open) whi… | 4.3 |
| NAV-STA-07 | MUST | Each Trigger owns a floating root context whose reference element is the Trigger and whose floating element is… | 4.3 |
| NAV-STA-08 | MUST | The Positioner, Popup, Viewport and Viewport target wrapper register their elements with the Root when they at… | 4.3 |
| NAV-STA-09 | MUST | viewportInert is a Root flag, initially false. It becomes true when focus leaves the Viewport to an element th… | 4.3 |
| NAV-STA-10 | MUST | An Item's effective value is its value prop when not nullish, otherwise a generated id stable for the Item's l… | 4.3 |
| NAV-STA-11 | MUST | "Active item" is open && value === itemValue. Trigger State.open and Icon State.open use this predicate; Conte… | 4.3 |
| NAV-STA-12 | MUST | Each Content has its own transition status derived from its open with the default status machine (TRN-STA-03/0… | 4.3 |
| NAV-STA-13 | MUST | Each Trigger keeps a patient-click flag stickIfOpen, initially true. Every hover-driven open or close request … | 4.4 |
| NAV-STA-14 | MUST | Each Trigger records the pointerType of the last pointerenter/pointerdown it received ('mouse', 'touch', 'pen'… | 4.4 |
| NAV-STA-15 | MUST | The Root records the reason of the most recent close request (closeReason) when a close is requested, and clea… | 4.4 |
| NAV-STA-16 | MUST | The state objects exposed to className/style/render contain exactly the fields tabulated in §3 for each part: … | 4.5 |
| NAV-DOM-01 | MUST | The Root element is a <nav> (top-level) or <div> (nested) with no data attributes of its own: the state fields… | 5.1 |
| NAV-DOM-02 | MUST | The Root element carries no aria-orientation attribute (NAV-ARIA-07) and no role. | 5.1 |
| NAV-DOM-03 | MUST | The consumer DOM handle on the Root receives the <nav>/<div> element; the same element is the boundary used by… | 5.1 |
| NAV-DOM-04 | MUST | The List element is a <ul> with data-open="" while the Root is open (default mapping of open) and no other com… | 5.2 |
| NAV-DOM-05 | MUST | The Item element is an <li> with no component attributes; the Content is not a DOM child of it except in the s… | 5.3 |
| NAV-DOM-06 | MUST | The Trigger element always has tabindex="0" (every Trigger is in the sequential tab order; the composite rovin… | 5.4 |
| NAV-DOM-07 | MUST | While the Trigger is the active trigger it has data-popup-open="" and data-pressed=""; both are absent otherwi… | 5.4 |
| NAV-DOM-08 | MUST | The Trigger has data-disabled="" iff disabled is true; it is removed when the prop becomes false. | 5.4 |
| NAV-DOM-09 | MUST | Every Trigger carries the marker attribute data-base-ui-navigation-menu-trigger=""; the outside-press dismissa… | 5.4 |
| NAV-DOM-10 | MUST | While the Trigger is the active trigger, three siblings follow it in order: a focus guard (<span tabindex="0" … | 5.4 |
| NAV-DOM-11 | MUST | The Trigger's aria-expanded/aria-controls and the focus-guard siblings update in the same commit as the value … | 5.4 |
| NAV-DOM-12 | MUST | The Icon is a <span aria-hidden="true"> whose children default to the single character ▼; consumer children re… | 5.5 |
| NAV-DOM-13 | MUST | The Icon has data-popup-open="" iff the Root is open and its Item is the active value; it never has data-press… | 5.5 |
| NAV-DOM-14 | MUST | The Content element has data-open="" while its state open is true and data-closed="" otherwise (exactly one of… | 5.6 |
| NAV-DOM-15 | MUST | The Content element has data-starting-style="" while its own status is 'starting' and data-ending-style="" whi… | 5.6 |
| NAV-DOM-16 | MUST | With keepMounted, the Content element has the boolean hidden attribute whenever its own mounted flag is false … | 5.6 |
| NAV-DOM-17 | MUST | While a Content is exiting (!open && mounted), its element receives inline position: absolute; top: 0; left: 0… | 5.6 |
| NAV-DOM-18 | MUST | The Content element has data-activation-direction="<left/right/up/down>" while the Root is open and a directio… | 5.6 |
| NAV-DOM-19 | MUST | The Content element is a DOM descendant of the Viewport element (of the Viewport's inner target wrapper when t… | 5.6 |
| NAV-DOM-20 | MUST | The Link element has data-active="" iff active is true. | 5.7 |
| NAV-DOM-21 | MUST | The Link element renders no tabindex attribute (the composite roving tabindex is explicitly overridden with un… | 5.7 |
| NAV-DOM-22 | MUST | The Portal renders a <div id="<generated>" data-base-ui-portal=""> appended to the resolved container; the Por… | 5.8 |
| NAV-DOM-23 | MUST | The Backdrop is a <div role="presentation"> with data-open/data-closed and data-starting-style/data-ending-sty… | 5.9 |
| NAV-DOM-24 | MUST | The Backdrop has the boolean hidden attribute while the popup is not mounted and inline user-select: none; -we… | 5.9 |
| NAV-DOM-25 | MUST | The Positioner is a <div role="presentation"> whose inline style is the computed position (position, top, left… | 5.10 |
| NAV-DOM-26 | MUST | The Positioner has the boolean hidden attribute while the popup is not mounted and inline pointer-events: none… | 5.10 |
| NAV-DOM-27 | MUST | The Positioner has data-open/data-closed from the Root's open, data-side and data-align with the resolved plac… | 5.10 |
| NAV-DOM-28 | MUST | The Positioner has data-instant="" while its instant state is true (NAV-ANI-13/14) and no attribute otherwise.… | 5.10 |
| NAV-DOM-29 | MUST | The Positioner's inline style carries --positioner-width and --positioner-height (values <n>px) whenever the a… | 5.10 |
| NAV-DOM-30 | MUST | The Popup is a <nav> with the id of NAV-API-17 and tabindex="-1" (consumer-overridable). | 5.11 |
| NAV-DOM-31 | MUST | When the resolved side is top, or is physically left (left; inline-start in LTR; inline-end in RTL), the Popup… | 5.11 |
| NAV-DOM-32 | MUST | The Popup has data-open/data-closed, data-starting-style/data-ending-style (Root status), data-side, data-alig… | 5.11 |
| NAV-DOM-33 | MUST | The Popup's inline style carries --popup-width and --popup-height: a <n>px value during a size transition, aut… | 5.11 |
| NAV-DOM-34 | MUST | With a Positioner, the Viewport is a <div id> and is wrapped by two focus guards (one before, one after, as si… | 5.12 |
| NAV-DOM-35 | MUST | A Viewport without a Positioner has the inert attribute while viewportInert is true (NAV-STA-09); a Viewport w… | 5.12 |
| NAV-DOM-36 | MUST | The Arrow is a <div aria-hidden="true"> whose inline style is the computed arrow position (arrowStyles, FLT-D… | 5.13 |
| NAV-DOM-37 | MUST | The Arrow has data-open/data-closed, data-side, data-align, and data-uncentered="" when it cannot be centered … | 5.13 |
| NAV-ARIA-01 | MUST | Every Trigger has aria-expanded="true" while it is the active trigger and aria-expanded="false" otherwise (inc… | 5.14 |
| NAV-ARIA-02 | MUST | The active Trigger has aria-controls equal to the Popup element's id when a Popup element is registered; inact… | 5.14 |
| NAV-ARIA-03 | MUST | The Trigger has aria-disabled="true" while disabled and aria-disabled="false" while enabled (native mode with … | 5.14 |
| NAV-ARIA-04 | MUST | The Link has aria-current="page" iff active is true; the attribute is absent otherwise (consumer aria-current … | 5.14 |
| NAV-ARIA-05 | MUST | Icon and Arrow have aria-hidden="true"; the focus guards have aria-hidden="true" except under VoiceOver on Web… | 5.14 |
| NAV-ARIA-06 | MUST | While a Trigger is active, the owner <span> after it has aria-owns equal to the Viewport element's id (absent … | 5.14 |
| NAV-ARIA-07 | MUST | Neither the Root, the List (top-level or nested) nor the Content element renders aria-orientation, regardless … | 5.14 |
| NAV-ARIA-08 | MUST | Positioner and Backdrop have role="presentation"; the Popup and Content have no role (the Popup is a <nav> lan… | 5.14 |
| NAV-ARIA-09 | MUST | No part renders aria-haspopup, aria-labelledby or aria-describedby; label association is left to the consumer. | 5.14 |
| NAV-KEY-01 | MUST | On a top-level Trigger with orientation='horizontal', ArrowDown requests value = itemValue with reason list-na… | 6.1 |
| NAV-KEY-02 | MUST | With orientation='vertical' the open key is ArrowRight in LTR and ArrowLeft in RTL (reading direction from the… | 6.1 |
| NAV-KEY-03 | MUST | Enter and Space on a Trigger activate it through the button mechanism (a click, UBT-KEY rules); the resulting … | 6.1 |
| NAV-KEY-04 | MUST | While open and a Positioner is registered, Escape pressed anywhere in the document (document-level listener) o… | 6.1 |
| NAV-KEY-05 | MUST | The top-level List moves focus between its items with the arrow keys of its orientation (horizontal: ArrowLeft… | 6.1 |
| NAV-KEY-06 | MUST | A top-level List stops propagation of its orientation's arrow keys (ArrowLeft/ArrowRight for horizontal, Arrow… | 6.1 |
| NAV-KEY-07 | MUST | Home and End are not handled by any NavigationMenu composite group (enableHomeAndEndKeys left at its false def… | 6.1 |
| NAV-KEY-08 | MUST | A nested Trigger (one whose Root is nested) never intercepts arrow keys: it has no open key, and its arrow key… | 6.1 |
| NAV-KEY-09 | MUST | Inside a Content, all four arrow keys move focus among the Content's composite items (links and nested trigger… | 6.1 |
| NAV-KEY-10 | MUST | Tab from the active Trigger focuses the first tabbable element inside the popup: the trailing focus guard afte… | 6.1 |
| NAV-KEY-11 | MUST | Tab from the last tabbable element inside the popup reaches the guard after the Viewport, which (focus arrivin… | 6.1 |
| NAV-KEY-12 | MUST | Shift+Tab from the first tabbable inside the popup reaches the guard before the Viewport, which (focus arrivin… | 6.1 |
| NAV-KEY-13 | MUST | Shift+Tab from the active Trigger is not intercepted; focus moves to the previous tabbable element natively an… | 6.1 |
| NAV-KEY-14 | MUST | On a disabled Trigger every keydown except Tab is default-prevented (UBT-KEY-20) and no open is requested; Tab… | 6.1 |
| NAV-KEY-15 | MUST | In an inline nested menu, Tab from the last link of the active nested panel focuses the next tabbable after th… | 6.1 |
| NAV-KEY-16 | MUST | Sequential focus movement never opens an inactive nested panel: tabbing onto an inactive nested Trigger leaves… | 6.1 |
| NAV-KEY-17 | MUST | Opening with the keyboard (open key, Enter, Space) never moves focus into the popup; the trigger keeps focus u… | 6.1 |
| NAV-PTR-01 | MUST | With the menu closed and the popup not mounted (or no Positioner registered), moving a mouse-like pointer onto… | 6.2 |
| NAV-PTR-02 | MUST | While the popup is mounted and a Positioner is registered, the rest delay is 0: moving onto an inactive Trigge… | 6.2 |
| NAV-PTR-03 | MUST | When the Trigger's recorded pointer type is 'touch', hover never opens or switches: hover-reason open requests… | 6.2 |
| NAV-PTR-04 | MUST | A click on a Trigger while the menu is closed opens it with reason trigger-press (mouse, touch, pen, keyboard-… | 6.2 |
| NAV-PTR-05 | MUST | A click on the active Trigger closes the menu with reason trigger-press when the open event was click-like (cl… | 6.2 |
| NAV-PTR-06 | MUST | A click on an inactive Trigger while another item is open switches the value to that item with reason trigger-… | 6.2 |
| NAV-PTR-07 | MUST | A hover switch resets the recorded open event, so that a menu opened by click, switched by hover to another tr… | 6.2 |
| NAV-PTR-08 | MUST | The Trigger passes handleClose: safePolygon(…) and delay: { close: closeDelay } to the hover-reference interac… | 6.2 |
| NAV-PTR-09 | MUST | The List passes closeDelay and the Root's tree nodeId to the hover-floating interaction on the floating elemen… | 6.2 |
| NAV-PTR-10 | MUST | Pointer-events lock. When a mouse-like pointer enters a Trigger whose floating element exists (Positioner regi… | 6.2 |
| NAV-PTR-11 | MUST | The lock is released (inline pointer-events removed from scope, trigger and floating element) when: the pointe… | 6.2 |
| NAV-PTR-12 | MUST | When the pointer enters an inactive Trigger while another item is open (a hover switch), the lock for the new … | 6.2 |
| NAV-PTR-13 | MUST | For an inline nested menu (nested Root with a Viewport and no Positioner), the safe-polygon close context is b… | 6.2 |
| NAV-PTR-14 | MUST | A pointerdown inside the floating element on an interactive element that is inside an [aria-haspopup] element … | 6.2 |
| NAV-PTR-15 | MUST | The Trigger records pointerType on pointerenter and pointerdown (NAV-STA-14); a touch type disables the pointe… | 6.2 |
| NAV-PTR-16 | MUST | While open with a Positioner registered, a press-backed click (one whose pointerdown/mousedown happened while … | 6.2 |
| NAV-PTR-17 | MUST | A click whose target is inside any element carrying data-base-ui-navigation-menu-trigger (a Trigger of this or… | 6.2 |
| NAV-PTR-18 | MUST | Every activation (trigger mouseenter, click, or open-key keydown) runs synchronously, before any other handler… | 6.2 |
| NAV-PTR-19 | MUST | A nested Root with its own Positioner opens on hover of its Trigger after delay ms like a top-level menu, stay… | 6.2 |
| NAV-PTR-20 | MUST | Hovering a Trigger of an inline nested Root whose value is null (no defaultValue) opens that nested item after… | 6.2 |
| NAV-FOC-01 | MUST | Opening never moves focus: after a hover, click or keyboard open the previously focused element (normally the … | 6.3 |
| NAV-FOC-02 | MUST | Every Trigger is a sequential tab stop (tabindex="0", NAV-DOM-06), including disabled triggers and inactive tr… | 6.3 |
| NAV-FOC-03 | MUST | Links are tab stops by their native semantics (an <a href>; no tabindex is rendered, NAV-DOM-21); a substitute… | 6.3 |
| NAV-FOC-04 | MUST | The two focus guards after the active Trigger and the two around the Viewport (NAV-DOM-10, NAV-DOM-34) impleme… | 6.3 |
| NAV-FOC-05 | MUST | When a close completes (NAV-STA-05) and the close reason is not one of trigger-hover, outside-press, focus-out… | 6.3 |
| NAV-FOC-06 | MUST | No focus return happens when the close reason is trigger-hover, outside-press or focus-out, when focus is alre… | 6.3 |
| NAV-FOC-07 | MUST | When focus leaves the Viewport element (blur with a non-null related target) to an element that is neither ins… | 6.3 |
| NAV-FOC-08 | MUST | The Positioner listens (capture phase) for focusin/focusout on itself: when focus enters it from outside, ever… | 6.3 |
| NAV-FOC-09 | MUST | A Content tracks whether focus is inside it (focus from any descendant that is not a focus guard sets the flag… | 6.3 |
| NAV-FOC-10 | MUST | When a Trigger loses focus while a Positioner and a Popup element are registered, and the related target is ou… | 6.3 |
| NAV-FOC-11 | MUST | When a Link loses focus while a Positioner and a Popup element are registered, the same test as NAV-FOC-10 app… | 6.3 |
| NAV-FOC-12 | MUST | Focus moving between the Trigger, the popup contents and nested popups (Dialog/Popover rendered inside Content… | 6.3 |
| NAV-FOC-13 | MUST | When the trailing outside guard after the active Trigger receives focus from outside the floating element and … | 13 |
| NAV-DIS-01 | MUST | Escape closes (reason escape-key) per NAV-KEY-04; the close is a request through onValueChange and can be canc… | 6.4 |
| NAV-DIS-02 | MUST | An outside press closes (reason outside-press) per NAV-PTR-16/17. | 6.4 |
| NAV-DIS-03 | MUST | Focus leaving the menu closes (reason focus-out) per NAV-KEY-11/13 and NAV-FOC-10/11. | 6.4 |
| NAV-DIS-04 | MUST | The pointer leaving a hover-opened menu closes it after closeDelay (reason trigger-hover) per NAV-PTR-08/09. | 6.4 |
| NAV-DIS-05 | MUST | There is no scroll-based or window-blur dismissal; scrolling re-positions (anchor tracking) and a window resiz… | 6.4 |
| NAV-DIS-06 | MUST | When a parent menu closes, every nested menu inside its Content disappears with it (the nested Content element… | 6.4 |
| NAV-DIS-07 | MUST | Interacting with a nested popup (Dialog, Popover) rendered inside Content — opening it, clicking inside it — n… | 6.4 |
| NAV-DIS-08 | MUST | An inline nested Root (no Positioner) has no Escape or outside-press dismissal of its own while open (its dism… | 6.4 |
| NAV-DIS-09 | MUST | A click on a Link with closeOnClick requests a close with reason link-press on the Link's Root; when that Root… | 6.4 |
| NAV-EVT-01 | MUST | onValueChange(nextValue, details) is invoked synchronously inside the originating input's handling, before the… | 7 |
| NAV-EVT-02 | MUST | The reasons and the user actions producing them are exactly: trigger-press — Trigger click/tap/Enter/Space (op… | 7 |
| NAV-EVT-03 | MUST | A hover-reason request (trigger-hover) is committed synchronously (flushed before the handler returns); all ot… | 7 |
| NAV-EVT-04 | MUST | Calling details.cancel() inside onValueChange prevents the change: in uncontrolled mode the value is unchanged… | 7 |
| NAV-EVT-05 | MUST | details.preventUnmountOnClose() called during a non-canceled onValueChange(null, …) while the value is current… | 7 |
| NAV-EVT-06 | MUST | onValueChange is not invoked when the requested value is identical (===) to the current value (a second click … | 7 |
| NAV-EVT-07 | MUST | onOpenChangeComplete is invoked only with false, exactly once per completed close, from the unmount completion… | 7 |
| NAV-EVT-08 | MUST | actionsRef.current.close() requests value = null with reason imperative-action through onValueChange (so the c… | 7 |
| NAV-EVT-09 | MUST | actionsRef.current.unmount() ends the closing phase immediately when the popup is mounted and closed (exit ani… | 7 |
| NAV-EVT-10 | MUST | For a nested Root, a non-canceled link-press close is propagated to the parent Root as parent.setValue(null, s… | 7 |
| NAV-EVT-11 | MUST | Changing the controlled value prop from the consumer never invokes onValueChange and never records a close rea… | 7 |
| NAV-ANI-01 | MUST | The Portal subtree (Portal container, Backdrop, Positioner, Popup, Viewport, Arrow) is rendered iff mounted //… | 9.1 |
| NAV-ANI-02 | MUST | A close completes (NAV-STA-05) on animations finished of the Popup element, with a second watcher armed on the… | 9.1 |
| NAV-ANI-03 | MUST | Post-commit, pre-paint in the commit where open becomes false (and whenever the Popup/Positioner elements chan… | 9.2 |
| NAV-ANI-04 | MUST | On activation of a Trigger (hover switch, click, open key) while Popup and Positioner elements exist: (1) curr… | 9.3 |
| NAV-ANI-05 | MUST | Auto-size reset. On animations finished of the Popup element (TRN-ALG-03; abortable), the popup size variables… | 9.3 |
| NAV-ANI-06 | MUST | When the activation is a switch from another open item (value non-null and different) by click, or by any non-… | 9.3 |
| NAV-ANI-07 | MUST | While a Trigger is active and Popup/Positioner elements exist, the active Content element is observed for chil… | 9.4 |
| NAV-ANI-08 | MUST | Interrupted resize. Pending frames and the auto-size reset are cancelled; if the current size has a zero axis … | 9.4 |
| NAV-ANI-09 | MUST | Sync without animation. Pending size frame and auto-size reset are cancelled, all size variables removed, the … | 9.4 |
| NAV-ANI-10 | MUST | While open and active, a window resize event runs the sync of NAV-ANI-09 on the next frame (one pending reques… | 9.4 |
| NAV-ANI-11 | MUST | The remembered size is updated from the Popup's offsetWidth/offsetHeight by a resize observer on the Popup ele… | 9.4 |
| NAV-ANI-12 | MUST | When a Trigger stops being the active trigger, its pending mutation and size frames are cancelled and its auto… | 9.4 |
| NAV-ANI-13 | MUST | A Positioner that mounts while the menu is already open (initially open menu, or a Positioner added while open… | 9.5 |
| NAV-ANI-14 | MUST | While open, a window resize event synchronously sets instant = true and starts a 100 ms timer that sets it bac… | 9.5 |
| NAV-ANI-15 | MUST | While the Root status is 'starting', the Positioner, the Popup and the Arrow carry inline transition: none; it… | 9.5 |
| NAV-ANI-16 | MUST | A Content with keepMounted whose Root has no Viewport target yet (server rendering, or the Portal not yet rend… | 9.6 |
| NAV-ANI-17 | MUST | Content elements already moved into the Viewport stay there when another item becomes active (a kept Content i… | 9.6 |
| NAV-ANI-18 | MUST | When a Content stops being active it enters 'ending' (NAV-DOM-15), is positioned absolutely at the Viewport or… | 9.6 |
| NAV-ANI-19 | MUST | The active Content element is the one the size tracking observes (NAV-ANI-07): the registration happens when t… | 9.6 |
| NAV-ANI-20 | MUST | A Content whose exit is interrupted by the popup unmounting (the menu closed and completed before the Content'… | 9.6 |
| NAV-ANI-21 | MUST | The size-freeze of NAV-ANI-03 and the fallbacks of NAV-ANI-04/08/09/11 together guarantee that a 0 measurement… | 9.6 |
| NAV-POS-01 | MUST | Defaults: positionMethod='absolute', side='bottom', align='center', sideOffset=0, alignOffset=0, collisionBoun… | 10 |
| NAV-POS-02 | MUST | The anchor is the anchor prop when given; otherwise the active Trigger's element (the DOM reference of the sha… | 10 |
| NAV-POS-03 | MUST | The shift middleware uses the layout viewport as its root boundary (shift: { rootBoundary: 'layoutViewport' })… | 10 |
| NAV-POS-04 | MUST | The adaptive-origin middleware is always configured, so the Positioner is positioned from the edge nearest the… | 10 |
| NAV-POS-05 | MUST | collisionAvoidance defaults to { fallbackAxisSide: 'none' } for a top-level Root (dropdown behavior: never fal… | 10 |
| NAV-POS-06 | MUST | The Portal's keepMounted is passed to anchor positioning as keepMounted, together with mounted, the shared flo… | 10 |
| NAV-POS-07 | MUST | The Positioner's inline style carries --anchor-width, --anchor-height, --available-width, --available-height (… | 10 |
| NAV-POS-08 | MUST | The resolved side and align (after collision handling) are exposed as data-side/data-align on Positioner, Popu… | 10 |
| NAV-POS-09 | MUST | The Arrow receives the computed arrow offset styles and the uncentered flag from the Positioner's positioning … | 10 |
| NAV-I18N-01 | MUST | In RTL reading direction the vertical open key is ArrowLeft instead of ArrowRight (NAV-KEY-02); horizontal men… | 12.1 |
| NAV-I18N-02 | MUST | In RTL the top-level List's horizontal arrow mapping is mirrored (ArrowLeft moves forward, ArrowRight backward… | 12.1 |
| NAV-I18N-03 | MUST | The Popup's physical-left detection (NAV-DOM-31) maps inline-start to physical left in LTR and inline-end to p… | 12.1 |
| NAV-EDGE-01 | MUST | Rendering List, Trigger, Icon, Content, Link, Portal, Backdrop, Positioner, Popup, Viewport or Arrow outside a… | 12 |
| NAV-EDGE-02 | MUST | Rendering Trigger, Icon or Content outside an Item fails with Base UI: NavigationMenuItem parts must be used w… | 12 |
| NAV-EDGE-03 | MUST | Rendering a Positioner outside a Portal fails with Base UI: <NavigationMenu.Portal> is missing. | 12 |
| NAV-EDGE-04 | MUST | Rendering a Popup or an Arrow outside a Positioner fails with Base UI: NavigationMenuPositionerContext is miss… | 12 |
| NAV-EDGE-05 | SHOULD | Outside production builds the Root's shared-state object carries the debugging name NavigationMenuRootContext;… | 12 |
| NAV-EDGE-06 | MUST | A disabled Trigger ignores hover (no rest timer, no lock), click, touch tap and the open key, never calls onVa… | 12 |
| NAV-EDGE-07 | MUST | Items with identical values are all considered active when that value is open (every such Trigger reports expa… | 12 |
| NAV-EDGE-08 | MUST | When the platform lacks ResizeObserver, the remembered size (NAV-ANI-11) is updated only by the sync path (NAV… | 12 |
| NAV-EDGE-09 | SHOULD | Switching value between undefined and a defined value after mount, or changing defaultValue after mount, logs … | 12 |
| NAV-EDGE-10 | MUST | The active Trigger unmounting while the menu is open (e.g. the consumer replaces it with a plain link in the s… | 12 |
| NAV-EDGE-11 | MUST | Under a WebKit engine the behavior is identical: the pointer-events lock scope is still the nearest ul (never … | 12 |

## 16. Open questions

1. **Content conformance suite is skipped.** `content/NavigationMenuContent.test.tsx:9` runs `describeConformance.skip(<NavigationMenu.Content />)`; the fixture renders Content inside `Root value="test" > Item` with a full Portal chain, which would portal the element into the Viewport (RND conformance expects the element at the render location). Whether Content must satisfy RND-DOM-36…48 at all (and with which fixture) is undecided; NAV-STR-06 documents the rendering location only.
2. **Outline omissions.** `components/page.mdx` lists no data attributes for Root, List or Item although the default mapping renders `data-open`/`data-nested` on Root and `data-open` on List (NAV-DOM-01/04), and omits the internal marker `data-base-ui-navigation-menu-trigger` (NAV-DOM-09) and the Positioner's `inert`-derived `pointer-events: none`. The outline also lists `className`/`render`/`style` for Portal (applied to the portal container). Documented from source; outline is incomplete.
3. **`aria-controls` is never asserted.** `NavigationMenuTrigger.tsx:663` references the Popup id only while active; no test checks it, and for an inline nested menu (no Popup) the active trigger has no `aria-controls` and no element announces the Viewport other than through `aria-owns`. Confirm whether a port should also reference the Viewport id when no Popup exists.
4. **Inline nested menus have no dismissal of their own.** `NavigationMenuList.tsx:44,54-56` enables Escape/outside-press only while a Positioner is registered or the value is `null`, so an inline nested Root that is open (e.g. with `defaultValue`) ignores Escape (the parent handles it) and never closes on outside press. Likely intentional (the nested panel is part of the parent's popup) but untested.
5. **Reason literals are mostly untested.** Only `imperative-action` is asserted (`NavigationMenuRoot.test.tsx:2145`). `trigger-press` vs `trigger-hover` on a *switch* depends on the event type reaching `handleActivation` (`NavigationMenuTrigger.tsx:588`): a `mouseenter` switch reports `trigger-hover`, a `click` switch `trigger-press`; the shared click hook's later request for the same value is dropped by NAV-EVT-06. `none` is declared in the union (`NavigationMenuRoot.tsx:448`) but never emitted.
6. **Duplicate item values.** Nothing prevents two Items from sharing a `value`; both Triggers would be active and both Contents would be portaled and sized (`NavigationMenuContent.tsx:62`, `NavigationMenuTrigger.tsx:126`). The size tracking observes only the last-registered Content. Undefined behavior; NAV-EDGE-07 records the observable part only.
7. **Trigger blur with a `null` related target closes the menu.** `isOutsideMenuEvent` treats `relatedTarget: null` as outside for a Trigger (`NavigationMenuTrigger.tsx:694-708`, `isOutsideMenuEvent.ts:29-34`), so a `blur` without a related target (clicking a non-focusable area, window blur in some browsers) requests `focus-out`. For a Link inside the popup the same helper always returns `false` because the link is inside the popup element (NAV-FOC-11); the Link's blur handler is effectively dead code for in-popup links. Confirm the intended semantics before porting the Link handler verbatim.
8. **`onOpenChangeComplete(true)` is never called.** The Root wires only the close completion (`NavigationMenuRoot.tsx:151`); the prop's JSDoc says "when the navigation menu is closed". TRN-EVT-01 (open completion) therefore does not apply to NavigationMenu; the port must not emit `true`.
9. **`data-instant` (TRN §10 Q5) confirmed.** The Positioner's boolean `instant` (`NavigationMenuPositioner.tsx:78-81,141-168`) is a NavigationMenu-owned marker for the first 0 ms tick of an initially open menu and for 100 ms after a window resize; it is independent of the shared `transition: none` suppression during `'starting'`, which the Positioner *also* receives through `usePositioner`. Documented as NAV-ANI-13/14/15 and NAV-DOM-28.
10. **Floating tree for safe polygon (FLT §10 Q6).** NavigationMenu does **not** pass the tree explicitly. `getHandleCloseContext` (`NavigationMenuTrigger.tsx:503-509,831-846`) returns `{ placement, elements, nodeId }`; the `tree` in the close context is added by the hover hook from the ancestor tree (`useHoverReferenceInteraction.ts:96,328-331`), which the top-level Root always provides. What NavigationMenu supplies explicitly is the `nodeId` (here and to the List's hover-floating interaction), because inline nested menus have no positioning session to publish their node. A port needs: a tree created by every top-level Root, a node per Root, and the node id threaded into the hover close context and the floating hover hook. Resolved: the floating tree, hover with safe polygon and its node threading live in the JavaScript `interactions` module; see decisions/04-js-interop-boundary.md §1.
11. **Lock scope for a nested portaled menu.** For a nested Root with its own Positioner, `getScope()` returns `null` and the trigger applies no lock (`NavigationMenuTrigger.tsx:512-517,597`); the shared hover-floating pass then picks the parent's floating element as scope (`useHoverFloatingInteraction.ts:110-130`). This is stated in NAV-PTR-10 from reading FLT; no NavigationMenu test asserts which element is locked in that configuration.
12. **`restMs` vs `delay` after the first open.** Once the popup is mounted and a Positioner exists, `restMs` is `0` (`NavigationMenuTrigger.tsx:526`), so re-hovering *any* trigger — including after a hover-close whose exit animation is still running — opens immediately with no rest delay. The docs describe `delay` as "how long to wait before opening"; the actual contract is "before the first open of a mount". Flagged for the docs reviewer.
13. **Zero-size popup during a kept-mounted close.** NAV-ANI-03 writes the last positioner size to both element sets while closed; with a kept Portal the inline variables persist until the next open's size pass removes them (`NavigationMenuTrigger.tsx:159-169`). A consumer stylesheet that reads `--popup-width` while closed therefore sees the last size, not `auto`. Intentional per the source comment at `NavigationMenuRoot.tsx:187-189`, but worth stating in the docs.
14. **Ownership of the size variables in the port (ADR 04 §2).** The size machinery writes `--positioner-width/height` on the Positioner and `--popup-width/height` on the Popup from frame-timed loops: three animation frames (`NavigationMenuTrigger.tsx:114-116`), a `ResizeObserver` on the Popup (`:319-326`), a `MutationObserver` on the active Content (`:367-386`) and the close-size freeze (`NavigationMenuRoot.tsx:172-196`). ADR 04 §2's ownership list makes every Positioner's `style` JS-owned (so the Positioner variables are JS-written by construction) but does not name `NavigationMenu.Popup` or its `--popup-*` variables, and §1 lists no `navigation-menu` component module for the NAV-ANI-03…12 size pass. Undecided: whether the Popup's `--popup-*` variables join the ownership list and whether the size pass becomes a component module (C1). Separately, ADR 04 §5 covers moving a portal node into its container; moving each Content element into the Viewport (NAV-ANI-16/17, element identity preserved across switches) is a second DOM move that §5 does not name. Decided (2026-10-18): a `navigation-menu` component module owns the size pass and the Content move; `--popup-*` and the viewport variables are JS-owned after attach (ADR 04 Amendments).
