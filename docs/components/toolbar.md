# Toolbar

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/toolbar/` |
| Tests | `packages/react/src/toolbar/**/*.test.tsx` (6 files; plus `link/ToolbarLink.spec.tsx`, type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/toolbar/page.mdx` |
| Prefix | `TLB` |
| Status | approved |
| Revised | 2026-10-27 |
| Builds on | none ([separator](separator.md) is reused by `Toolbar.Separator`) |
| Derived by | none; hosts [toggle-group](toggle-group.md) / [toggle](toggle.md) and any popup trigger via `Toolbar.Button` substitution |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md) (indirectly, through hosted parts), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [button-behavior](../architecture/button-behavior.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Toolbar is the WAI-ARIA `toolbar` pattern: a `<div role="toolbar">` that groups a row (or column) of controls and exposes them to the keyboard as **one tab stop**. Exactly one item carries `tabindex="0"`; <kbd>Tab</kbd> enters the toolbar on that item and leaves it on the next press; the arrow keys of the toolbar's orientation (<kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> for `horizontal`, swapped under RTL; <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> for `vertical`) move focus and the tab stop from item to item, wrapping at the ends unless `loopFocus` is `false`. <kbd>Home</kbd>/<kbd>End</kbd> are **not** handled.

Items are `Toolbar.Button` (a button that may also *be* a popup trigger, a Switch, or a Toggle through element substitution), `Toolbar.Link` (an anchor; never disabled), and `Toolbar.Input` (a native text input whose arrow keys move the caret until it reaches the edge of the text, then move focus). `Toolbar.Group` is a `role="group"` wrapper that can disable everything inside it; `Toolbar.Separator` is a `role="separator"` whose orientation is perpendicular to the toolbar's. A [ToggleGroup](toggle-group.md) placed inside the toolbar does not form a nested roving group: its Toggles become items of the toolbar's single roving group.

Disabled items default to **focusable-when-disabled** (`focusableWhenDisabled` is `true` on Button and Input): they stay in the arrow-key order, announce `aria-disabled="true"`, keep hover handlers (so tooltips work), and refuse activation. An item that opts out with `focusableWhenDisabled={false}` is removed from the roving order; the toolbar computes the set of such items from metadata each item publishes (`{ disabled, focusableWhenDisabled }`) and passes it to the composite root as `disabledIndices`, re-validating the initial tab stop when that set changes. `Toolbar.Root` and `Toolbar.Group` `disabled` cascade to Buttons, Inputs and nested ToggleGroups — but not to Links.

Popups opened from a toolbar button cooperate with the toolbar: Dialog/AlertDialog popups always, and Menu/Popover/Select popups when rendered inside a toolbar, stop arrow/Home/End keydowns from reaching the toolbar's handler, so keyboard navigation inside the popup does not move the toolbar's focus. The docs page advises using at most one `Toolbar.Input` in a horizontal toolbar and placing it last, because the horizontal arrow keys are shared between caret movement and navigation.

## 2. Anatomy

```
<Toolbar.Root>                required, 1          default element: div   (role="toolbar", composite root)
  <Toolbar.Button>            optional, 0..n       default element: button  (composite item)
  <Toolbar.Link>              optional, 0..n       default element: a       (composite item)
  <Toolbar.Input>             optional, 0..n       default element: input   (composite item)
  <Toolbar.Separator>         optional, 0..n       default element: div   (role="separator"; structural)
  <Toolbar.Group>             optional, 0..n       default element: div   (role="group"; structural, disables descendants)
    <Toolbar.Button | Link | Input | ToggleGroup …>
  <ToggleGroup>               optional, 0..n       (toggle-group.md) — its Toggles are items of this toolbar
    <Toggle>                  (toggle.md)          composite item
  <Popup.Root>                                     Menu / Select / Dialog / AlertDialog / Popover
    <Toolbar.Button render={<Popup.Trigger />} />  composite item that is also the trigger
  <Tooltip.Root>
    <Tooltip.Trigger render={<Toolbar.Button />} />  (the toolbar part is the inner element here)
```

- **TLB-STR-01** — MUST. `Toolbar.Root` MUST render exactly one element (default `<div>`) that is a composite root ([CMP-STR-01](../architecture/composite-and-list-navigation.md)); every `Toolbar.Button`, `Toolbar.Link`, `Toolbar.Input`, and every `Toggle` inside a `ToggleGroup` anywhere in its subtree (through `Toolbar.Group`, popup roots, tooltip roots or arbitrary wrappers) MUST register as an item of that single roving group, in DOM order ([CMP-STA-02](../architecture/composite-and-list-navigation.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:63-77`; `toolbar/button/ToolbarButton.tsx:54-74`; `toolbar/link/ToolbarLink.tsx:32-43`; `toolbar/input/ToolbarInput.tsx:64-75`; `toggle/Toggle.tsx:123-135`. Test: [L2] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (button, link, grouped buttons, input traversed in order); `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children".
  Port note 2026-10-27 (ADR 13 §3): stays `[L2]` for what C# observes: every `ToolbarButton`, `ToolbarLink` and `ToolbarInput` registers the id it renders with the root, the toggles of a `ToggleGroup` inside the toolbar register with the toolbar's root, and an element that is not a part registers nothing (`ToolbarButtonTests` "port: a toolbar button is an item with a generated id and one button twin in composite mode with its FocusableWhenDisabled"; `ToolbarLinkTests` "port: a link is an item with tabindex -1 and data-orientation only, whatever the toolbar's disabled state"; `ToolbarInputTests` "port: an input is an item with tabindex -1, …"; `ToolbarRootTests` "port: an element that is not a toolbar part is not an item, …"; `ToggleGroupToolbarTests` "port: inside a toolbar the group is no composite root, and its toggles register with the toolbar's"). The traversal in document order is the `composite` module's and runs in `toolbar.spec.ts` ("$direction, orientation: $orientation").
- **TLB-STR-02** — MUST. `Toolbar.Button`, `Toolbar.Link`, `Toolbar.Input`, `Toolbar.Group` and `Toolbar.Separator` rendered outside a `Toolbar.Root` MUST fail with the error `Base UI: ToolbarRootContext is missing. Toolbar parts must be placed within <Toolbar.Root>.`
  Source: `toolbar/root/ToolbarRootContext.ts:12-23`; call sites `toolbar/button/ToolbarButton.tsx:31`, `toolbar/link/ToolbarLink.tsx:26`, `toolbar/input/ToolbarInput.tsx:29`, `toolbar/group/ToolbarGroup.tsx:27`, `toolbar/separator/ToolbarSeparator.tsx:18`. Test: [L2] `ToolbarSeparator.test.tsx` "throws a descriptive error when rendered outside Toolbar.Root"; `ToolbarRoot.test.tsx` "allows optional consumers both outside and inside a toolbar" (the optional read used by ToggleGroup and popups returns nothing outside).
  Port note 2026-10-27 (plan of 2026-10-26 §Settled, chosen by the user; the PRG-EDGE-01 form): the error is the `InvalidOperationException` ``Blazix: The `ToolbarRoot` context is missing. Toolbar parts must be placed within `<ToolbarRoot>`.``, thrown in every build at the part's first render and, for the items, before the composite item's own error (`ToolbarSeparatorTests` "throws a descriptive error when rendered outside Toolbar.Root"; `ToolbarRootTests` "port: a toolbar part outside ToolbarRoot throws the missing-context error", the five parts). The optional read is the internal `ToolbarRootContext` as a nullable cascading parameter (`ToolbarRootTests` "allows optional consumers both outside and inside a toolbar", over a test-only consumer).
- **TLB-STR-03** — MUST. `Toolbar.Group` and `Toolbar.Separator` MUST NOT register as items and MUST NOT take part in keyboard navigation; Group is a structural wrapper that contributes `disabled` to its descendants, Separator is a styled divider.
  Source: `toolbar/group/ToolbarGroup.tsx:43-51`; `toolbar/separator/ToolbarSeparator.tsx:22`. Test: [L4] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (grouped buttons are traversed, the group itself is never focused).
  Port note 2026-10-27, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title is browser-only and asserts the module's traversal; it runs in `toolbar.spec.ts` ("$direction, orientation: $orientation", where the group's two buttons are traversed and the group is never focused). bUnit observes the registration half: a group and a separator register nothing and render no `tabindex` (`ToolbarGroupTests` "port: the group's disabled state is the root's or its own, its orientation the root's, and it is not an item"; `ToolbarSeparatorTests` "port: data-orientation and the state carry the resolved orientation, which follows the toolbar's").
- **TLB-STR-04** — MUST. A `ToggleGroup` inside `Toolbar.Root` MUST NOT become a nested composite root ([TGG-STR-02](toggle-group.md)); its Toggles register with the toolbar, keep the ToggleGroup's value semantics, and are navigated with the toolbar's keys.
  Source: `toggle-group/ToggleGroup.tsx:38,99-109`; `toggle/Toggle.tsx:123-135`. Test: [L4] `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children", "supports multiple selection for direct ToggleGroup > Toggle children"; `ToggleGroup.test.tsx` "preserves selection and roving focus when nested in Toolbar.Group".
  Port note 2026-10-27: both titles run in `toolbar.spec.ts` and the variant in `toggle-group.spec.ts` "preserves selection and roving focus when nested in Toolbar.Group"; the group creates no root inside a toolbar (TGG-STR-02's note).
- **TLB-STR-05** — MUST. A popup trigger (`Menu.Trigger`, `Select.Trigger`, `Dialog.Trigger`, `AlertDialog.Trigger`, `Popover.Trigger`), a `Switch.Root` or a `Toggle` substituted into `Toolbar.Button` via `render` MUST behave as both a toolbar item (roving `tabindex`, toolbar `disabled` cascade, metadata) and as itself (its own role/ARIA, activation, open/close). `Tooltip.Trigger` composes the other way round (`<Tooltip.Trigger render={<Toolbar.Button />} />`).
  Source: `toolbar/button/ToolbarButton.tsx:54-74`; `internals/useRenderElement.tsx:157-173`. Test: [L4] `ToolbarButton.test.tsx` "renders a menu trigger", "renders a select trigger", "renders a dialog trigger", "renders a popover trigger", "renders a switch", "renders toggle and toggle group". Docs: `components/toolbar/page.mdx` §Using with Menu, §Using with Tooltip, §Using with NumberField.
  Port note 2026-10-27 (ADR 12 Amendments 2026-10-26 item 3): the Toggle clause runs in `toolbar.spec.ts` ("renders toggle and toggle group", "port: in the docs' hero shape the substituted toggles are toolbar items, each toggles once, and the arrows reach every item"). The Switch clause runs in `toolbar.spec.ts` ("renders a switch", "handles interactions", "disabled state") and at L2 (`ToolbarButtonTests` "port: a native SwitchRoot substituted into a toolbar button keeps its role and ARIA and is the toolbar's item"). Deviation 2026-10-27 (design point 14.7 of M3 track 4, option (c), built as the orchestrator recommends 2026-10-27 and put to the user): a non-native `SwitchRoot` applies the `id` it receives to its hidden input (SWT-API-11), where the toolbar would register and attach, so the Switch titles run over a native switch, `NativeButton="true"` over a `<button>`, the shape `ToolbarButton`'s remarks name as supported for a checkable root; the non-native shape is open to a follow-up (option (a), a marker attribute the modules resolve by). The popup clauses are open to M4 (Dialog, AlertDialog, Popover) and M5 (Menu, Select).

When `Toolbar.Group` is omitted nothing changes for navigation; when `Toolbar.Separator` is omitted nothing changes. Nested `Toolbar.Root`s are unsupported (§16).

## 3. Public API

### 3.1 `Toolbar.Root`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables every Button and Input (and nested ToggleGroup) in the toolbar; Links are unaffected. | TLB-API-01 |
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Arrow-key axis, `aria-orientation`, `data-orientation`; inherited by every part's state. | TLB-API-02 |
| `loopFocus` | `boolean` | `true` | Whether arrow-key navigation wraps at the ends. | TLB-API-03 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard element customization; functions receive the Root state object. | TLB-API-04 |
| `aria-label`, `aria-labelledby` | `string` | — | Forwarded; the only source of the toolbar's accessible name. | TLB-ARIA-08 |
| *other `<div>` attributes and handlers* | — | — | Forwarded to the element. `dir` is forwarded as an attribute but not read (TLB-EDGE-06). | TLB-API-05 |

**Consumed (not forwarded):** `disabled`, `orientation`, `loopFocus`, `className`, `style`, `render`.

The hinted `cols` prop does not exist in 1.9.0 (removed; `CHANGELOG.md v1.0.0-beta.5 "The \`cols\` prop has been removed"`); see §16.

- **TLB-API-01** — MUST. `disabled` MUST default to `false` and be published to descendants as the toolbar's disabled state, OR-combined into each Button's, Input's, Group's and nested ToggleGroup's effective disabled (TLB-STA-05); it is not written to the root element except as `data-disabled`.
  Source: `toolbar/root/ToolbarRoot.tsx:23,48-56`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links".
- **TLB-API-02** — MUST. `orientation` MUST default to `'horizontal'`, accept only `'horizontal'` or `'vertical'`, be passed to the composite root as its orientation ([CMP-API-02](../architecture/composite-and-list-navigation.md)), be rendered as `aria-orientation` and `data-orientation`, and be published to every part (`data-orientation` on Button, Link, Input, Group; flipped for Separator).
  Source: `toolbar/root/ToolbarRoot.tsx:25,48-61,75,86,99-105`; `internals/types.ts:97`. Test: [L2] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation", "allows optional consumers both outside and inside a toolbar" (`orientation="vertical"` published).
- **TLB-API-03** — MUST. `loopFocus` MUST default to `true` (the composite root's default; Root passes the prop through unchanged, so `undefined` means `true`) and select whether arrow navigation wraps ([CMP-API-01](../architecture/composite-and-list-navigation.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:24,73,107-111`; `internals/composite/root/useCompositeRoot.ts:76`. Test: [L2] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (wraps), "does not wrap focus when loopFocus is false". (L4 for the wrap behaviour)
  Port note 2026-10-27 (ADR 13 §3): stays `[L2]` for the option: `LoopFocus` reaches the composite root as `loopFocus` (`ToolbarRootTests` "port: the toolbar's composite root sends its orientation and loopFocus, with Home and End off and handled keys stopped"); the wrap is the module's and runs in `toolbar.spec.ts` ("$direction, orientation: $orientation", "does not wrap focus when loopFocus is false").
- **TLB-API-04** — MUST. `className`, `style` and `render` MUST behave per [RND](../architecture/rendering-and-composition.md) with the Root state object.
  Source: `toolbar/root/ToolbarRoot.tsx:26-28,65-68`. Test: [L2] `ToolbarRoot.test.tsx` conformance suite "Base UI component API".
- **TLB-API-05** — MUST. Every other prop MUST be forwarded to the root element, merged after the built-in `role` and `aria-orientation` so a consumer value wins ([RND-DOM-19](../architecture/rendering-and-composition.md)); consumer `onKeyDown`/`onFocus` run before the composite root's handlers and may veto them with `preventBaseUIHandler()` ([RND-EVT-02](../architecture/rendering-and-composition.md), [RND-EVT-03](../architecture/rendering-and-composition.md)). Server: relaxed, see §9.
  Source: `toolbar/root/ToolbarRoot.tsx:29,58-61,71`; `internals/composite/root/CompositeRoot.tsx:67-72`. Test: [L2] `ToolbarRoot.test.tsx` conformance suite (prop forwarding); "$direction, orientation: $orientation" (`dir` forwarded). (L4 for the veto clause)
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-22, option (i), chosen by the user 2026-10-23; ADR 01's row "… EVD-EVT-26 against composite/button key handling …"): the root declines the consumer's `onkeydown` and runs it at `navigate` on WebAssembly, where `PreventPartHandler()` vetoes the key, and from the module's handled-key report on Server, a round trip late, where it cannot (`ToolbarRootTests` "port: the root declines its consumer onkeydown, which the composite runs at navigate"; `toolbar.spec.ts` "port: the root's C# onkeydown vetoes a handled arrow on WebAssembly and runs a round trip late on Server"). `dir` is forwarded (`ToolbarRootTests` "port: a consumer role and aria-orientation win over the root's"). Deviation 2026-10-27 (design point 14.6 (a) of M3 track 4, settled by the orchestrator 2026-10-27; the ToggleGroup precedent, TGG-API-09's note): the `onFocus` clause has nothing a C# veto can stop. Base UI's root `onFocus` only selects a native text input's value (`useCompositeRoot.ts:321-327`), which the `composite` module does in its `focusin` listener with no decision point, so the consumer's `onfocus` is forwarded and its `PreventPartHandler()` does not stop the selection on either mode. Grounds of 14.6: the TGG-API-09 precedent of 2026-10-26 and ADR 04 Amendments 2026-10-18 (a dispatch-time veto needs a decision point, and a new point is the user's to name).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The `disabled` prop. |
| `orientation` | `'horizontal' \| 'vertical'` | The `orientation` prop. |

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while `disabled` | TLB-DOM-01 |
| `data-orientation` | always; `"horizontal"` / `"vertical"` | TLB-DOM-01 |

#### CSS variables

None (no part of Toolbar sets CSS variables).

### 3.2 `Toolbar.Button`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables the button. OR-combined with Root and Group disabled. | TLB-API-06 |
| `focusableWhenDisabled` | `boolean` | `true` | Keep the button in the roving order and focusable while disabled. | TLB-API-07 |
| `nativeButton` | `boolean` | `true` | Declares native vs non-native button mode ([UBT](../architecture/button-behavior.md) §3). | TLB-API-08 |
| `className`, `style`, `render` | see RND | — | Standard customization; functions receive the Button state object. When `render` is given, `disabled` is also forwarded to the substituted element. | TLB-API-09 |
| *other `<button>` attributes and handlers* | — | — | Forwarded; `onClick`/`onMouseDown`/`onPointerDown`/`onKeyDown`/`onKeyUp` are wrapped by the button mechanism. | TLB-API-10 |

**Consumed (not forwarded):** `disabled` (forwarded only when `render` is given), `focusableWhenDisabled`, `nativeButton`, `className`, `style`, `render`.

- **TLB-API-06** — MUST. The button's effective disabled state MUST be `toolbar.disabled || group.disabled || disabled` (Group = nearest enclosing `Toolbar.Group`, `false` if none); the prop default is `false`.
  Source: `toolbar/button/ToolbarButton.tsx:23,31-35`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links"; `ToolbarGroup.test.tsx` "disables all toolbar items except links in the group"; `ToolbarButton.test.tsx` "disables the button".
- **TLB-API-07** — MUST. `focusableWhenDisabled` MUST default to `true` and be passed to the button mechanism **and** published in the item's metadata (TLB-STA-03); `false` makes a disabled button natively disabled and removes it from the roving order (TLB-FOC-04).
  Source: `toolbar/button/ToolbarButton.tsx:24,37-46`. Test: [L2] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default", "toolbar items can individually disable focusableWhenDisabled"; `ToolbarButton.test.tsx` "uses the disabled attribute when focusableWhenDisabled is false". Docs: `CHANGELOG.md v1.0.0-beta.1 "Set \`disabled\` attr on toolbar button when \`focusableWhenDisabled={false}\` (#2176)"`. (L4 for the roving-order clause)
  Port note 2026-10-27 (M3 track 1's E3): `FocusableWhenDisabled` reaches the button twin and the item's registration (`ToolbarButtonTests` "port: a toolbar button is an item with a generated id and one button twin in composite mode with its FocusableWhenDisabled"; `ToolbarItemFlagsTests` "port: a button and an input send …"); the roving-order clause runs in `toolbar.spec.ts` "toolbar items can individually disable focusableWhenDisabled".
- **TLB-API-08** — MUST. `nativeButton` MUST default to `true` ([UBT-API-03](../architecture/button-behavior.md)); it is passed to the button mechanism as `native` and never rendered.
  Source: `toolbar/button/ToolbarButton.tsx:26,42-46`; `internals/types.ts:63-71`. Test: [L2] `ToolbarButton.test.tsx` "custom element: dispatches real clicks from Space keyboard activation", "custom element: dispatches real clicks from Enter keyboard activation" (`nativeButton={false}` with a `<span>`); conformance suite "with a non-native button".
  Port note 2026-10-27: `NativeButton` reaches the button twin as `native`, and the writer renders `role="button"` for `false` (`ToolbarButtonTests` "port: type or role, disabled and aria-disabled follow the composite rows of NativeButton, FocusableWhenDisabled and Disabled"; `ToolbarButtonConformanceTests` "with a non-native button"); the two "custom element" titles click through the module and run in `toolbar.spec.ts`.
- **TLB-API-09** — MUST. `className`, `style`, `render` MUST behave per RND with the Button state object. When `render` is supplied, the effective `disabled` boolean MUST additionally be passed to the substituted element as a `disabled` prop (so a substituted Base UI trigger derives its own disabled state); when `render` is absent, no `disabled` prop is forwarded (the default button stays hoverable, TLB-PTR-01).
  Source: `toolbar/button/ToolbarButton.tsx:63-73` (`render ? { disabled } : EMPTY_OBJECT`). Test: [L2] `ToolbarButton.test.tsx` "disables the button" (no `disabled` attribute on the default button), Menu/Dialog/Popover "disabled state" (substituted triggers inert). Docs: `CHANGELOG.md v1.6.0 "Do not forward \`disabled\` to default toolbar button (#4967)"`.
  Port note 2026-10-27 (design point 14.2 (a) of M3 track 4, settled by the orchestrator 2026-10-27): while `Render` or `RenderFunction` is set, the part adds the effective `disabled` to its own attributes, so an element substitute receives the attribute and a component substitute (`Toggle`, `SwitchRoot`) the parameter, `false` included; the default button never receives it (`ToolbarButtonTests` "port: Disabled reaches a substitute only, as an element's attribute and a component's parameter, true and false", "disables the button"). Re-cited (ADR 12 Amendments 2026-10-26 item 3; ADR 13 Amendments 2026-10-22): the substituted-trigger clause is asserted over `SwitchRoot` and `Toggle` (`ToolbarButtonTests` "port: a native SwitchRoot substituted into a toolbar button keeps its role and ARIA and is the toolbar's item"; `toolbar.spec.ts` "port: a disabled Toggle substituted into a focusable-when-disabled button has no disabled attribute and stays focusable, also after a runtime flip"); the Menu, Dialog and Popover "disabled state" titles are M4's and M5's. Grounds: TLB-API-09, TLB-EDGE-04 and ADR 02 decision 1 as built on `main`; no `Rendering/` edit.
- **TLB-API-10** — MUST. Every other prop MUST be forwarded to the element; consumer `onClick`, `onMouseDown`, `onPointerDown`, `onKeyDown`, `onKeyUp` are wrapped by the button mechanism and therefore gated by the disabled state ([UBT-EVT-01](../architecture/button-behavior.md), [UBT-EVT-03](../architecture/button-behavior.md)); consumer `tabIndex`/`role`/`type`/`aria-disabled` override the mechanism ([UBT-DOM-09](../architecture/button-behavior.md)).
  Source: `toolbar/button/ToolbarButton.tsx:28,63-73` (`[elementProps, disabledForRender, getButtonProps]`). Test: [L2] `ToolbarButton.test.tsx` "disables the button" (four consumer handlers gated), conformance suite (prop forwarding).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (TLB-API-06). |
| `orientation` | `'horizontal' \| 'vertical'` | The toolbar's orientation. |
| `focusable` | `boolean` | The `focusableWhenDisabled` prop. |

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effective disabled | TLB-DOM-03 |
| `data-orientation` | always; toolbar orientation | TLB-DOM-03 |
| `data-focusable` | `""` while `focusableWhenDisabled` is `true` (regardless of disabled) | TLB-DOM-03 |

### 3.3 `Toolbar.Link`

**Default element:** `<a>`. **Accepts `render` element substitution:** yes (the callback receives `<a>` props such as `href`). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see RND | — | Standard customization; functions receive the Link state object. | TLB-API-11 |
| *all `<a>` attributes and handlers* (`href`, `target`, …) | — | — | Forwarded unchanged. There is no `disabled` prop. | TLB-API-11 |

- **TLB-API-11** — MUST. `Toolbar.Link` MUST accept no part-specific props: everything except `className`/`style`/`render` is forwarded to the anchor; it MUST NOT accept or derive a disabled state (the outline lists no `disabled`; Root/Group `disabled` do not reach it).
  Source: `toolbar/link/ToolbarLink.tsx:24,32-43,53-57`; `toolbar/link/ToolbarLink.spec.tsx:5-10`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links"; `ToolbarGroup.test.tsx` "disables all toolbar items except links in the group"; `ToolbarLink.test.tsx` "renders an anchor".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | The toolbar's orientation. |

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; toolbar orientation | TLB-DOM-06 |

### 3.4 `Toolbar.Input`

**Default element:** `<input>`. **Accepts `render` element substitution:** yes (e.g. `<NumberField.Input />`). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables the input (ARIA-only; the native `disabled` attribute is never set). OR-combined with Root and Group disabled. | TLB-API-12 |
| `focusableWhenDisabled` | `boolean` | `true` | Keep the input in the roving order while disabled. | TLB-API-13 |
| `defaultValue` | `<input>` `defaultValue` | — | Forwarded (listed explicitly in the type). | TLB-API-14 |
| `className`, `style`, `render` | see RND | — | Standard customization; functions receive the Input state object. | TLB-API-14 |
| *other `<input>` attributes and handlers* (`type`, `value`, `name`, `onChange`, …) | — | — | Forwarded unchanged. | TLB-API-14 |

**Consumed (not forwarded):** `disabled`, `focusableWhenDisabled`, `className`, `style`, `render`.

- **TLB-API-12** — MUST. The input's effective disabled state MUST be `toolbar.disabled || group.disabled || disabled`; the prop default is `false`; the native `disabled` attribute MUST never be emitted (TLB-DOM-07) and `disabled` MUST NOT be forwarded to a substituted element.
  Source: `toolbar/input/ToolbarInput.tsx:24,29-33,40-45`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links", "toolbar items can be focused when disabled by default" (no `disabled` attribute); `ToolbarInput.test.tsx` "disabled state" (NumberField: `not.toHaveAttribute('disabled')`).
  Port note 2026-10-27 (ADR 12 Amendments 2026-10-26 item 3): re-cited. The `ToolbarRoot.test.tsx` clause is asserted by "disables all toolbar items except links" (`ToolbarRootTests`); NumberField's "disabled state" (M5) is re-cited over a plain input and the port's `Input` for its attributes (`ToolbarInputTests` "port: a disabled input renders aria-disabled=\"true\" over the consumer's and never disabled, from any disabled source", "port: a substituted Input receives the toolbar input's attributes and no disabled").
- **TLB-API-13** — MUST. `focusableWhenDisabled` MUST default to `true`, be published in the item's metadata, and gate the keydown suppression of TLB-KEY-13; `false` removes a disabled input from the roving order (TLB-FOC-04).
  Source: `toolbar/input/ToolbarInput.tsx:22,35-45`. Test: [L2] `ToolbarRoot.test.tsx` "skips a disabled Toolbar.Input with focusableWhenDisabled={false}", "toolbar items can individually disable focusableWhenDisabled". (L4 for the keydown-suppression and roving-order clauses)
  Port note 2026-10-27 (ADR 13 §3; M3 track 1's E3): stays `[L2]` for the flags (`ToolbarItemFlagsTests` "port: a button and an input send their disabled state and FocusableWhenDisabled, …"); the keydown-suppression clause is the `button` module's `inputItem` (L3, `js/button.test.ts` "button module: input item") and the roving-order clause runs in `toolbar.spec.ts` "skips a disabled Toolbar.Input with focusableWhenDisabled={false}".
- **TLB-API-14** — MUST. `defaultValue` and every other prop not listed as consumed MUST be forwarded to the input element; consumer `onClick`/`onPointerDown`/`onKeyDown` run **after** the part's own handlers for those events (the part's handlers are merged first and the suppression handler last), and `preventBaseUIHandler()` in a consumer handler does not undo a `preventDefault()` already issued.
  Source: `toolbar/input/ToolbarInput.tsx:26,59-62,73,100`. Test: [L2] `ToolbarInput.test.tsx` conformance suite (prop forwarding), "prevents click default actions while disabled" (`type="checkbox"` forwarded), "renders NumberField.Input" (`render` substitution). (L4 for the handler-order and `preventDefault()` clauses)
  Port note 2026-10-27: `DefaultValue` is a `string?` rendered once, as the `value` attribute of the first render (plan of 2026-10-26 §Settled, chosen by the user; ADR 04 Amendments 2026-10-22, the field half's evidence), so Blazor never writes the element's value afterwards (`ToolbarInputTests` "port: DefaultValue is rendered once as the value attribute"); a consumer `type` is forwarded ("port: an input is an item with tabindex -1, …"). The consumer's `onclick`, `onpointerdown` and `onkeydown` run while disabled, after the `button` module's prevention (design point 14.5 (a) of M3 track 4, settled by the orchestrator 2026-10-27: `ButtonBehavior`'s input item mode declines no handler; the item gate runs the `onkeydown`; `ToolbarInputTests` "port: the consumer's onclick, onpointerdown and onkeydown run while disabled, after the module's prevention"). Re-cited (ADR 12 Amendments 2026-10-26 item 3): "renders NumberField.Input" is asserted over the port's `Input` for the attributes it receives (`ToolbarInputTests` "port: a substituted Input receives the toolbar input's attributes and no disabled"). Deviation 2026-10-27 (design point 14.3 (a), settled by the orchestrator 2026-10-27): a substituted `Input` is supported for its attributes only; its own key gate (the `form` module's decline-and-rerun of `onkeydown`) and the item's would both own one `onkeydown`. Grounds of 14.5: TLB-API-14 and ADR 02 Amendments 2026-10-21 (`ForwardsConsumerHandler` declines only the handlers a part must gate, and the input gates none). Grounds of 14.3: the M3 plan's settled item (the port's `Input` in scope only for the incidental re-citations, two key gates on one element otherwise unsupported), accepted by the user 2026-10-26.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (TLB-API-12). |
| `orientation` | `'horizontal' \| 'vertical'` | The toolbar's orientation. |
| `focusable` | `boolean` | The `focusableWhenDisabled` prop. |

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effective disabled | TLB-DOM-07 |
| `data-orientation` | always; toolbar orientation | TLB-DOM-07 |
| `data-focusable` | `""` while `focusableWhenDisabled` is `true` | TLB-DOM-07 |

### 3.5 `Toolbar.Group`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables every Button, Input and nested ToggleGroup inside the group. OR-combined with Root disabled. | TLB-API-15 |
| `className`, `style`, `render` | see RND | — | Standard customization; functions receive the Group state object. | TLB-API-16 |
| *other `<div>` attributes and handlers* | — | — | Forwarded. | TLB-API-16 |

- **TLB-API-15** — MUST. The group's effective disabled state MUST be `toolbar.disabled || disabled` (default `false`) and MUST be published to its descendants (Buttons, Inputs, ToggleGroups — not Links).
  Source: `toolbar/group/ToolbarGroup.tsx:21,27-36`. Test: [L2] `ToolbarGroup.test.tsx` "disables all toolbar items except links in the group"; `ToolbarRoot.test.tsx` "disables all toolbar items except links" (`data-disabled` on the group); `ToolbarButton.test.tsx` "disables direct ToggleGroup children when Toolbar.Group is disabled".
- **TLB-API-16** — MUST. `className`/`style`/`render` MUST behave per RND; other props MUST be forwarded, merged after the built-in `role="group"` so a consumer `role` wins.
  Source: `toolbar/group/ToolbarGroup.tsx:19-25,43-47`. Test: [L2] `ToolbarGroup.test.tsx` conformance suite "Base UI component API", "renders a group" (`data-testid` forwarded).
  Port note 2026-10-27 (ADR 01 and ADR 04 Amendments 2026-10-26, option (i)'s precision, chosen by the user): a `ToolbarGroup`'s C# `onkeydown` never runs for a key the `composite` module handles, on either mode, where Base UI runs it before the root stops propagation; it runs for a key the module does not handle, and a JavaScript listener on the group sees every key (`toolbar.spec.ts` "port: a ToolbarGroup's C# onkeydown runs for an unhandled key and not for a handled arrow"). A consumer `role` wins (`ToolbarGroupTests` "port: a consumer role wins over the group's").

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled (TLB-API-15). |
| `orientation` | `'horizontal' \| 'vertical'` | The toolbar's orientation. |

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while effective disabled | TLB-DOM-09 |
| `data-orientation` | always; toolbar orientation | TLB-DOM-09 |

### 3.6 `Toolbar.Separator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes. Implemented as the public [Separator](separator.md) with a computed default orientation.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | opposite of the toolbar's orientation | Orientation of the separator; an explicit value overrides the computed default. | TLB-API-17 |
| `className`, `style`, `render` | see RND | — | Standard customization; functions receive `{ orientation }`. | TLB-API-18 |
| *other `<div>` attributes and handlers* | — | — | Forwarded. | TLB-API-18 |

- **TLB-API-17** — MUST. When `orientation` is not supplied, the separator's orientation MUST be `'horizontal'` in a `vertical` toolbar and `'vertical'` in a `horizontal` toolbar; a supplied `orientation` MUST win.
  Source: `toolbar/separator/ToolbarSeparator.tsx:20-22` (`{...props}` spread after the computed value). Test: [L2] `ToolbarSeparator.test.tsx` "uses a %s separator in a %s toolbar" (both), "allows its orientation to be overridden".
  Port note 2026-10-27 (ADR 08 Amendments 2026-10-26, chosen by the user): `ToolbarSeparator.Orientation` is an `Orientation?`; `null`, the default, is perpendicular to the toolbar, and a supplied value wins unvalidated (TLB-EDGE-08; `ToolbarSeparatorTests` "port: data-orientation and the state carry the resolved orientation, which follows the toolbar's").
- **TLB-API-18** — MUST. All other props MUST be forwarded to the underlying Separator part, which renders them per [separator](separator.md) / RND.
  Source: `toolbar/separator/ToolbarSeparator.tsx:22,27-33`; `separator/Separator.tsx:16-24`. Test: [L2] `ToolbarSeparator.test.tsx` conformance suite "Base UI component API".
  Port note 2026-10-27 (plan of 2026-10-26 §Settled, chosen by the user; ADR 16 rule 10; ADR 08 Amendments 2026-10-22): `ToolbarSeparator` is its own part over the internal `SeparatorAttributes` helper `Separator` also uses for `role` and `aria-orientation`; it neither renders nor derives from `Separator`, and its own pipeline forwards every other attribute (`ToolbarSeparatorConformanceTests`).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | The resolved separator orientation (perpendicular to the toolbar by default). |

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-orientation` | always; resolved separator orientation | TLB-DOM-10 |

### 3.7 Shared types

- `Toolbar.Root.Orientation` = `Orientation` = `'horizontal' | 'vertical'` (also exported as `Toolbar.Orientation` from `toolbar/index.parts.ts`). Source: `toolbar/root/ToolbarRoot.tsx:86`; `toolbar/index.parts.ts:8`; `internals/types.ts:97`.
- `Toolbar.Root.ItemMetadata` = `{ disabled: boolean; focusableWhenDisabled: boolean }` — the metadata every item publishes to the toolbar's item registry (TLB-STA-03). Source: `toolbar/root/ToolbarRoot.tsx:81-84,115`.
- `Toolbar.Root.State`, `Toolbar.Button.State`, `Toolbar.Link.State`, `Toolbar.Input.State`, `Toolbar.Group.State`, `Toolbar.Separator.State` — the state objects above (`Button.State`, `Input.State`, `Group.State` extend `Root.State`; `Separator.State` extends `Separator.State` of the Separator component). Source: `toolbar/root/ToolbarRoot.tsx:88-97`, `toolbar/button/ToolbarButton.tsx:78-87`, `toolbar/link/ToolbarLink.tsx:46-51`, `toolbar/input/ToolbarInput.tsx:78-87`, `toolbar/group/ToolbarGroup.tsx:54`, `toolbar/separator/ToolbarSeparator.tsx:25`.
- `Toolbar.*.Props` — the prop tables above. `Toolbar.Link.Props` is `BaseUIComponentProps<'a', State, React.ComponentPropsWithRef<'a'>>` so the render callback is typed with anchor props. Source: `toolbar/link/ToolbarLink.tsx:53-57`.
- Exported data-attribute namespaces: `ToolbarRootDataAttributes` `{ disabled, orientation }`, `ToolbarButtonDataAttributes` `{ disabled, orientation, focusable }`, `ToolbarInputDataAttributes` `{ disabled, orientation, focusable }`, `ToolbarLinkDataAttributes` `{ orientation }`, `ToolbarGroupDataAttributes` `{ disabled, orientation }`, `ToolbarSeparatorDataAttributes` `{ orientation }`. Source: `toolbar/index.ts:11-16` and the six `*DataAttributes.ts` files.
- No change-event reason or details type: Toolbar emits no callbacks (§7).

## 4. State model

Toolbar owns no value. Its state is (a) the composite root's **highlighted index** (which item holds the tab stop), (b) the **item map** published by the item registry, from which the toolbar derives (c) the **disabled indices**, and (d) the propagated **disabled** flags.

**Disabled cascade.** `Root.disabled` → `Group.disabled = root || prop` → `Button.disabled = root || group || prop`, `Input.disabled = root || group || prop`, `ToggleGroup.disabled = root || group || prop` ([TGG-API-04](toggle-group.md)) → `Toggle.disabled = toggle || toggleGroup` ([TGL-API-04](toggle.md)). `Link` never reads any of these.

**Item metadata.** Each item registers `{ disabled, focusableWhenDisabled }`:

| Item | `disabled` | `focusableWhenDisabled` |
|---|---|---|
| `Toolbar.Button` | effective disabled | prop (default `true`) |
| `Toolbar.Input` | effective disabled | prop (default `true`) |
| `Toolbar.Link` | `false` | `true` (constant) |
| `Toggle` as a direct member of a `ToggleGroup` (no `Toolbar.Button`) | toggle's effective disabled | `false` (constant) |
| `Toolbar.Button render={<Toggle />}` (two registrations on one node) | **Toolbar.Button's** | **Toolbar.Button's** |
| `Toolbar.Button render={<Menu.Trigger />}` etc. | Toolbar.Button's | Toolbar.Button's |

**Disabled indices** = indexes of items whose metadata has `disabled && !focusableWhenDisabled`. These are the items that cannot hold the tab stop and are skipped by arrow navigation; natively disabled elements (`:disabled`) are skipped regardless ([CMP-ALG-02](../architecture/composite-and-list-navigation.md)).

**Highlighted index** starts at `0` and is re-validated against the disabled indices once they are known (they are derived from the first item-map publication, one render after the items mount).

- **TLB-STA-01** — MUST. The state objects exposed to `className`/`style`/`render` MUST be exactly: Root `{ disabled, orientation }`; Button `{ disabled, orientation, focusable }`; Input `{ disabled, orientation, focusable }`; Link `{ orientation }`; Group `{ disabled, orientation }`; Separator `{ orientation }` (resolved).
  Source: `toolbar/root/ToolbarRoot.tsx:56`; `toolbar/button/ToolbarButton.tsx:48-52`; `toolbar/input/ToolbarInput.tsx:47-51`; `toolbar/link/ToolbarLink.tsx:28-30`; `toolbar/group/ToolbarGroup.tsx:38-41`; `separator/Separator.tsx:18`. Test: none (shapes pinned by the exported `State` types; data attributes asserted throughout `ToolbarRoot.test.tsx`).
  Port note 2026-10-27: the records are `ToolbarRootState(Disabled, Orientation)`, `ToolbarButtonState(Disabled, Orientation, Focusable)`, `ToolbarInputState(Disabled, Orientation, Focusable)`, `ToolbarLinkState(Orientation)`, `ToolbarGroupState(Disabled, Orientation)` and `ToolbarSeparatorState(Orientation)`, the last resolved; each is asserted by a title of its part's tests (`ToolbarRootTests` "port: the state objects are the root's Disabled and Orientation"; the Button, Input, Link, Group and Separator state titles).
- **TLB-STA-02** — MUST. The toolbar MUST compute, from the item map, the set of indexes whose metadata satisfies `disabled && !focusableWhenDisabled`, and pass exactly that set to the composite root as `disabledIndices`; the set MUST be recomputed whenever the item map is republished (membership, order or metadata identity change, [CMP-STA-09](../architecture/composite-and-list-navigation.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:32-46,72,74`; `internals/composite/list/CompositeList.tsx:69-74`. Test: [L4] `ToolbarRoot.test.tsx` "toolbar items can individually disable focusableWhenDisabled", "skips a disabled Toolbar.Input with focusableWhenDisabled={false}"; `ToolbarButton.test.tsx` "skips disabled direct ToggleGroup > Toggle children", "skips a direct Toggle that becomes disabled at runtime". Docs: `CHANGELOG.md v1.6.0 "Fix disabled state and roving focus bugs (#4968)"`.
  Port note 2026-10-27, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22 and 2026-10-26, M3 track 1's E3: the `composite` module derives the disabled indices from the items' flags; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the derivation is the module's, so the cited titles run in `toolbar.spec.ts` ("toolbar items can individually disable focusableWhenDisabled", "skips a disabled Toolbar.Input with focusableWhenDisabled={false}", "skips disabled direct ToggleGroup > Toggle children", "skips a direct Toggle that becomes disabled at runtime"). bUnit observes the option and the flags: `ToolbarItemFlagsTests` "port: the toolbar's root derives its disabled indices from the items' flags, and a standalone toggle group's does not", "port: a button and an input send their disabled state and FocusableWhenDisabled, …".
- **TLB-STA-03** — MUST. Items MUST publish metadata as in the table above; when two registrations target one element (a `Toolbar.Button` whose substitute is itself a composite item, e.g. a `Toggle` in a `ToggleGroup`), the outer `Toolbar.Button`'s metadata MUST be the one in the item map ([CMP-STR-04](../architecture/composite-and-list-navigation.md)).
  Source: `toolbar/button/ToolbarButton.tsx:37-40,60`; `toolbar/input/ToolbarInput.tsx:35-38,70`; `toolbar/link/ToolbarLink.tsx:8-12,38`; `toggle/Toggle.tsx:116-121,130`; `internals/composite/item/CompositeItem.tsx:30`. Test: [L2] `ToolbarButton.test.tsx` "disabled state" (Toggle and ToggleGroup block: disabled toggles via Toolbar.Button remain focusable — outer metadata `focusableWhenDisabled: true` wins), "skips disabled direct ToggleGroup > Toggle children" (direct toggles: `focusableWhenDisabled: false` → skipped).
  Port note 2026-10-27 (ADR 13 §3): stays `[L2]` for the registrations: each item's flags (`ToolbarItemFlagsTests` "port: a button and an input send their disabled state and FocusableWhenDisabled, and a link is never disabled and focusable"), the direct toggles' `{ disabled, focusableWhenDisabled: false }` (`ToggleGroupToolbarTests` "port: the direct toggles of a toolbar's group send their disabled state and are not focusable when disabled, also after a change"), and the outer registration of a substituted toggle, the first registration per id (M3 track 1's E4; "port: a toggle substituted into a toolbar button registers once, with the button's flags, and changes its group's value"). The navigation they cause runs in `toolbar.spec.ts` "disabled state" (Toggle and ToggleGroup block) and "skips disabled direct ToggleGroup > Toggle children".
- **TLB-STA-04** — MUST. The highlighted index MUST be owned by the composite mechanism (Root passes no controlled index), start at `0`, and — because `disabledIndices` is supplied — be moved to the first non-disabled index post-commit, pre-paint whenever the current highlighted index is in the disabled set, including right after the first item-map publication ([CMP-STA-24](../architecture/composite-and-list-navigation.md), [CMP-STA-29](../architecture/composite-and-list-navigation.md)); if every item is disabled the index is kept. Server: relaxed, see §9.
  Source: `toolbar/root/ToolbarRoot.tsx:63-77` (no `highlightedIndex`); `internals/composite/root/useCompositeRoot.ts:90,151-161,166-193`. Test: [L4] `ToolbarRoot.test.tsx` "moves the initial tab stop off a disabled, non-focusable first item" (`button2` has `tabindex="0"`, `button1` does not). (L4 for the pre-paint timing, relaxed on Server)
  Port note 2026-10-27, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22 and 2026-10-26, M3 track 1's E3: the `composite` module derives the disabled indices from the items' flags; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title reads the module's roving `tabindex` and runs in `toolbar.spec.ts` "moves the initial tab stop off a disabled, non-focusable first item"; the per-mode timing is `toolbar.spec.ts` "port: the first publication puts the tab stop on the first navigable item, a round trip later on Server" (§9).
- **TLB-STA-05** — MUST. The disabled cascade MUST be exactly as described above: Root → Group → Button/Input/ToggleGroup, OR-combined at each level, Links exempt.
  Source: `toolbar/root/ToolbarRoot.tsx:48-54`; `toolbar/group/ToolbarGroup.tsx:29`; `toolbar/button/ToolbarButton.tsx:35`; `toolbar/input/ToolbarInput.tsx:33`; `toggle-group/ToggleGroup.tsx:45-46`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links"; `ToolbarGroup.test.tsx` "disables all toolbar items except links in the group"; `ToolbarButton.test.tsx` "disables direct ToggleGroup children when Toolbar.Group is disabled".
  Port note 2026-10-27 (design point 14.4 (a) of M3 track 4, settled by the orchestrator 2026-10-27; the M2 groups' precedents and ToggleGroup's, design point 11.1): `ToolbarRootContext { Disabled, Orientation }` and `ToolbarGroupContext { Disabled }` are listener lists cascaded fixed; on every change each part recomputes its effective state and renders only when it changed (`ToolbarRootTests` "port: a change of the root's Disabled renders only the parts whose state changed"; `ToolbarButtonTests` "port: the button's disabled state is the toolbar's, its group's or its own, …"; `ToolbarInputTests` "port: a disabled input renders …"; `ToggleGroupToolbarTests` "port: inside a toolbar the group's disabled state is the toolbar's, the toolbar group's or its own"). Grounds: the M3 plan's design-point note (plain listener lists, the three M2 groups' shape) and design point 11.1.
  Port note 2026-10-27: the toggle group's place in the cascade is `ToggleGroupToolbarTests` "port: inside a toolbar the group's disabled state is the toolbar's, the toolbar group's or its own"; "disables direct ToggleGroup children when Toolbar.Group is disabled" runs in `toolbar.spec.ts`.
- **TLB-STA-06** — MUST. An item's metadata MUST be republished when its `disabled` or `focusableWhenDisabled` changes at runtime (new metadata identity → item map republished → disabled indices recomputed → TLB-STA-04 re-validation), so a toggle/button that becomes natively disabled is skipped by the next arrow key and the tab stop leaves it if it held it.
  Source: `toolbar/button/ToolbarButton.tsx:37-40`; `toolbar/input/ToolbarInput.tsx:35-38`; `toggle/Toggle.tsx:118-121`; `internals/composite/list/useCompositeListItem.ts:62-82`. Test: [L4] `ToolbarButton.test.tsx` "skips a direct Toggle that becomes disabled at runtime".
  Port note 2026-10-27, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22 and 2026-10-26, M3 track 1's E3: the `composite` module derives the disabled indices from the items' flags; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): the cited title is browser-only and runs in `toolbar.spec.ts`, with "port: an item that becomes disabled and not focusable while it holds the tab stop gives it to the next item". bUnit observes the republication: `ToolbarItemFlagsTests` "port: a change of an item's disabled state from any source sends its new flags in one update".

## 5. Rendered DOM and ARIA

### 5.1 Root element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"toolbar"` (consumer may override) | TLB-ARIA-01 | C# |
| `aria-orientation` | `"horizontal"` / `"vertical"`, always present | TLB-ARIA-02 | C# |
| `tabindex` | never emitted | TLB-DOM-02 | C# |
| `data-orientation` | always | TLB-DOM-01 | C# |
| `data-disabled` | `""` while `disabled` | TLB-DOM-01 | C# |
| handlers | composite `keydown` and `focus` | TLB-DOM-02 | JS |

- **TLB-DOM-01** — MUST. The root element MUST be a `<div>` by default and carry `data-orientation` (always) and `data-disabled` (while disabled) ([RND-DOM-01](../architecture/rendering-and-composition.md), [RND-DOM-03](../architecture/rendering-and-composition.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:56,65-71`; `toolbar/root/ToolbarRootDataAttributes.ts:4,9`. Test: [L2] `ToolbarRoot.test.tsx` conformance suite (`refInstanceof: HTMLDivElement`); `data-*` assertions: none on Root (the group/items are asserted).
- **TLB-DOM-02** — MUST. The root element MUST carry no `tabindex` and MUST carry the composite root's `keydown` handler (§6.1) and `focus` handler (TLB-FOC-05) ([CMP-DOM-03](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L2] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (<kbd>Tab</kbd> lands on the first item, not the root).
  Port note 2026-10-27 (ADR 13 §3): stays `[L2]`: bUnit observes that the root renders no `tabindex` (`ToolbarRootTests` "port: the root always renders aria-orientation and data-orientation, data-disabled while disabled, and no tabindex"); the handlers are the `composite` module's (§5.1 `JS`, an ownership row), and the cited title's Tab clause runs in `toolbar.spec.ts`.

### 5.2 Button element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `type` | `"button"` (native mode) | TLB-DOM-04 | C# |
| `role` | `"button"` (non-native mode) | TLB-DOM-04 | C# |
| `tabindex` | roving: `"0"` on the tab-stop item, `"-1"` otherwise | TLB-DOM-05 | C# → JS (from attach) |
| `disabled` | only when `focusableWhenDisabled={false}` and disabled (native) | TLB-DOM-04, TLB-DOM-08 | C#; C# → JS (the DOM property a substituted part rendered, cleared by the `button` module while focusable when disabled, design point 14.1, settled by the orchestrator 2026-10-27) |
| `aria-disabled` | per matrix | TLB-ARIA-03 | C# |
| `data-disabled`, `data-orientation`, `data-focusable` | §3.2 | TLB-DOM-03 | C# |

- **TLB-DOM-03** — MUST. The button MUST carry `data-orientation` (always), `data-disabled` (while effectively disabled) and `data-focusable` (while `focusableWhenDisabled` is `true`, independent of disabled).
  Source: `toolbar/button/ToolbarButton.tsx:48-52`; `toolbar/button/ToolbarButtonDataAttributes.ts:4,9,13`. Test: [L2] `ToolbarButton.test.tsx` "disables the button"; `ToolbarRoot.test.tsx` "toolbar items can individually disable focusableWhenDisabled" (`data-focusable` used to select items).
- **TLB-DOM-04** — MUST. The `type`/`role`/`disabled`/`aria-disabled` attributes MUST follow [UBT-ALG-03](../architecture/button-behavior.md) in composite mode with `F = focusableWhenDisabled` (default `true`), i.e. exactly:
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26, chosen by the user: `ToolbarButton` is always in composite mode): the matrix is `ButtonAttributeWriter`'s with `F = FocusableWhenDisabled` and `composite` true (`ToolbarButtonTests` "port: type or role, disabled and aria-disabled follow the composite rows of NativeButton, FocusableWhenDisabled and Disabled", the eight rows). On the default element a non-native row also carries the default element's `type="button"` (RND-STR-05), as Base UI's does.

  | `nativeButton` | `F` | disabled | `type` / `role` | `disabled` attr | `aria-disabled` |
  |---|---|---|---|---|---|
  | `true` | `true` | false | `type="button"` | — | `"false"` |
  | `true` | `true` | true | `type="button"` | — | `"true"` |
  | `true` | `false` | false | `type="button"` | — | — |
  | `true` | `false` | true | `type="button"` | present | — |
  | `false` | `true` | false | `role="button"` | — | — |
  | `false` | `true` | true | `role="button"` | — | `"true"` |
  | `false` | `false` | false | `role="button"` | — | — |
  | `false` | `false` | true | `role="button"` | — | `"true"` |

  Note `aria-disabled="false"` on an enabled native button with the default `focusableWhenDisabled` ([UBT-ARIA-01](../architecture/button-behavior.md)).
  Source: `toolbar/button/ToolbarButton.tsx:42-46`; `utils/useFocusableWhenDisabled.ts:15-16,38-47`; `internals/use-button/useButton.ts:226`. Test: [L2] `ToolbarButton.test.tsx` "disables the button" (row 2), "uses the disabled attribute when focusableWhenDisabled is false" (row 4); `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (no `disabled` attribute), "toolbar items can individually disable focusableWhenDisabled" (`disabled` on the opted-out button).
- **TLB-DOM-05** — MUST. The button mechanism MUST emit no `tabindex` ([UBT-DOM-05](../architecture/button-behavior.md)); the element's `tabindex` MUST be the roving value from the composite item: `"0"` when its index is the highlighted index, `"-1"` otherwise ([CMP-DOM-01](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/item/useCompositeItem.ts:19,25`; `utils/useFocusableWhenDisabled.ts:30`. Test: [L4] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (`tabindex="0"` returns to `button1`), "moves the initial tab stop off a disabled, non-focusable first item".
  Port note 2026-10-27, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's roving `tabindex` from attach; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): both cited titles read the roving value, which only a browser observes; bUnit observes that the writer renders no `tabindex` and the item `"-1"` (`ToolbarButtonTests` "port: type or role, …"). The `Writer` cell of §5.2 reads `C# → JS (from attach)`: an always-item part takes the second half of the settled cell `C# (standalone); C# → JS (in a group/toolbar, from attach)` (plan of 2026-10-26 §Settled, chosen by the user; conventions §3.4). The browser titles that need the derived disabled indices follow M3 track 1's E3.
- **TLB-DOM-08** — MUST. When `render` substitutes a Base UI part that itself renders a `<button>` and sets the DOM `disabled` property from the forwarded `disabled` prop (a disabled `Toggle` in native mode, a disabled trigger), and `focusableWhenDisabled` is `true`, the toolbar button's mechanism MUST reset the element's DOM `disabled` property to `false` after commit and whenever the element or the disabled state changes ([UBT-DOM-10](../architecture/button-behavior.md), [UBT-ALG-04](../architecture/button-behavior.md)), so the rendered element has `aria-disabled="true"` and **no** `disabled` attribute and remains focusable.
  Source: `internals/use-button/useButton.ts:68-89,234-237`; `toolbar/button/ToolbarButton.tsx:62,71`. Test: [L2] `ToolbarButton.test.tsx` "disabled state" (Menu, Dialog, Popover, Select, Toggle and ToggleGroup blocks: `not.toHaveAttribute('disabled')`, `aria-disabled="true"`, focusable by <kbd>Tab</kbd>/arrows).
  Port note 2026-10-27 (design point 14.1 (a) of M3 track 4, settled by the orchestrator 2026-10-27; UBT-ALG-04 on `main` since M1): the outer button's handle clears the DOM `disabled` property a substituted part rendered while the toolbar button is focusable when disabled, after attach and after each update, while Blazor still believes the attribute is there, so a later flip rewrites it and the handle clears it again; §5.2 names the second writer (`C# → JS`, an ownership row). Re-cited (ADR 12 Amendments 2026-10-26 item 3): asserted over a substituted `Toggle` (`toolbar.spec.ts` "port: a disabled Toggle substituted into a focusable-when-disabled button has no disabled attribute and stays focusable, also after a runtime flip"); the Switch block runs over a native switch ("disabled state"; TLB-STR-05's deviation of design point 14.7), and the Menu, Dialog, Popover and Select blocks are M4's and M5's. Grounds: ADR 04 §2's single-writer rule needs a row for every value JavaScript writes (the Switch `checked` row of 2026-10-22 is the precedent for a C#-rendered value JavaScript then owns).

### 5.3 Link element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `tabindex` | roving `"0"` / `"-1"` | TLB-DOM-06 | C# → JS (from attach) |
| `data-orientation` | always; toolbar orientation | TLB-DOM-06 | C# |

- **TLB-DOM-06** — MUST. `Toolbar.Link` MUST render an `<a>` (default) carrying the roving `tabindex` and `data-orientation` only; it MUST never carry `disabled`, `aria-disabled`, `data-disabled`, `data-focusable`, `type` or an added `role` (the native link role applies).
  Source: `toolbar/link/ToolbarLink.tsx:28-43`; `toolbar/link/ToolbarLinkDataAttributes.ts:5`. Test: [L2] `ToolbarLink.test.tsx` "renders an anchor" (`getByRole('link')`); `ToolbarRoot.test.tsx` "disables all toolbar items except links" (no `data-disabled` / `aria-disabled`).
  Port note 2026-10-27: asserted by `ToolbarLinkTests` "renders an anchor" and "port: a link is an item with tabindex -1 and data-orientation only, whatever the toolbar's disabled state". §5.3 gains a table for the link's `tabindex`, `C# → JS (from attach)` as TLB-DOM-05's note gives (ADR 04 Amendments 2026-10-22; the ownership lint reads a cell under a heading naming the part).

### 5.4 Input element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `tabindex` | roving `"0"` / `"-1"` | TLB-DOM-07 | C# → JS (from attach) |
| `aria-disabled` | `"true"` while effectively disabled; absent otherwise | TLB-ARIA-04 | C# |
| `disabled` | never | TLB-DOM-07 | C# |
| `type`, `role` | not added (consumer's `type` forwarded) | TLB-DOM-07 | C# |
| `data-disabled`, `data-orientation`, `data-focusable` | §3.4 | TLB-DOM-07 | C# |
| handlers | `click`/`pointerdown` guards (TLB-PTR-02), keydown suppression (TLB-KEY-13), composite `focus`/`mousemove` | — | JS |

- **TLB-DOM-07** — MUST. `Toolbar.Input` MUST render an `<input>` (default) with the roving `tabindex`, `data-orientation`, `data-disabled` (while disabled), `data-focusable` (while `focusableWhenDisabled`), and `aria-disabled="true"` exactly while disabled; it MUST never carry the native `disabled` attribute ([UBT-DOM-12](../architecture/button-behavior.md): the attribute resolution runs with `composite: true`, `isNativeButton: false`, so neither `tabindex` nor `disabled` is emitted by it).
  Source: `toolbar/input/ToolbarInput.tsx:40-51,64-75`; `toolbar/input/ToolbarInputDataAttributes.ts:4,9,13`; `utils/useFocusableWhenDisabled.ts:30-47`. Test: [L2] `ToolbarInput.test.tsx` conformance suite (`refInstanceof: HTMLInputElement`), "disabled state" (NumberField: no `disabled`, `data-disabled`, `aria-disabled="true"`, `tabindex="0"`); `ToolbarRoot.test.tsx` "disables all toolbar items except links", "toolbar items can be focused when disabled by default".
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26 and 2026-10-27, the `button` row; chosen by the user 2026-10-26): the resolution is `ButtonAttributeWriter.WriteInputItem`, `aria-disabled="true"` while disabled and nothing else, and the behaviour the `button` module's `inputItem` option (UBT-DOM-12; `ToolbarInputTests` "port: an input is an item with tabindex -1, data-orientation and data-focusable, and no type, role, disabled or aria-disabled", "port: the input attaches one button twin with the input item option, not native, in composite mode"). The `tabindex` `Writer` cell of §5.4 reads `C# → JS (from attach)` (TLB-DOM-05's note). Re-cited (ADR 12 Amendments 2026-10-26 item 3): NumberField's "disabled state" over a plain input and the port's `Input` (TLB-API-12's note).

### 5.5 Group element

- **TLB-DOM-09** — MUST. `Toolbar.Group` MUST render a `<div>` (default) with `role="group"`, `data-orientation` (always) and `data-disabled` (while effectively disabled); it has no `tabindex` and no handlers of its own.
  Source: `toolbar/group/ToolbarGroup.tsx:38-47`; `toolbar/group/ToolbarGroupDataAttributes.ts:4,9`. Test: [L2] `ToolbarGroup.test.tsx` "renders a group"; `ToolbarRoot.test.tsx` "disables all toolbar items except links" (`data-disabled` on the group).

### 5.6 Separator element

- **TLB-DOM-10** — MUST. `Toolbar.Separator` MUST render the Separator part: a `<div>` (default) with `role="separator"`, `aria-orientation` equal to the resolved orientation (TLB-API-17) and `data-orientation` with the same value.
  Source: `toolbar/separator/ToolbarSeparator.tsx:20-22`; `separator/Separator.tsx:18-24`; `toolbar/separator/ToolbarSeparatorDataAttributes.ts:5`. Test: [L2] `ToolbarSeparator.test.tsx` "uses a %s separator in a %s toolbar", "allows its orientation to be overridden" (`aria-orientation`); conformance suite (`refInstanceof: HTMLDivElement`).

### 5.7 Attribute precedence

- **TLB-DOM-11** — MUST. On every part a consumer-supplied attribute with the same name as a built-in one MUST win ([RND-DOM-19](../architecture/rendering-and-composition.md)): Root `role`/`aria-orientation`; Group `role`; Button `role`/`type`/`tabIndex`/`aria-disabled`/`disabled` ([UBT-DOM-09](../architecture/button-behavior.md)); Input `tabIndex`/`aria-disabled` — except that on Input the suppression props (`aria-disabled`, `onKeyDown`) are merged **after** consumer props and therefore a consumer `aria-disabled` is overridden while the input is disabled.
  Source: `toolbar/root/ToolbarRoot.tsx:71`; `toolbar/group/ToolbarGroup.tsx:46`; `toolbar/button/ToolbarButton.tsx:63-73`; `toolbar/input/ToolbarInput.tsx:73` (`[defaultProps, elementProps, focusableWhenDisabledProps]`). Test: [L2] conformance suites (prop forwarding) in all six test files; Input override case: none.
  Port note 2026-10-27 (§16 Q5, resolved by mirroring, chosen by the user 2026-10-26; ADR 02 Amendments 2026-10-21, `FinishAttributes`): `ToolbarInput` writes `aria-disabled="true"` over the merged attributes while disabled, so a consumer's loses then and wins otherwise (`ToolbarInputTests` "port: a disabled input renders aria-disabled=\"true\" over the consumer's and never disabled, from any disabled source"; `ButtonAttributeWriterTests` "port: the input item variant writes over an aria-disabled already in the list while disabled"). The other parts' consumer attributes win at the merge (`ToolbarRootTests` "port: a consumer role and aria-orientation win over the root's"; `ToolbarGroupTests` "port: a consumer role wins over the group's").

### 5.8 ARIA

- **TLB-ARIA-01** — MUST. The root element MUST carry `role="toolbar"` by default.
  Source: `toolbar/root/ToolbarRoot.tsx:58-61`. Test: [L2] `ToolbarRoot.test.tsx` "has role="toolbar"".
- **TLB-ARIA-02** — MUST. The root element MUST always carry `aria-orientation` equal to `orientation` (including the default `"horizontal"`).
  Source: `toolbar/root/ToolbarRoot.tsx:58-61`. Test: none directly (`ToolbarSeparator.test.tsx` asserts the separator's; Root's `aria-orientation` is unasserted).
  Port note 2026-10-27: asserted by `ToolbarRootTests` "port: the root always renders aria-orientation and data-orientation, data-disabled while disabled, and no tabindex" (both orientations).
- **TLB-ARIA-03** — MUST. `Toolbar.Button`'s `aria-disabled` MUST follow the TLB-DOM-04 matrix: with the default `focusableWhenDisabled`, a native button carries `aria-disabled="true"` while disabled **and** `aria-disabled="false"` while enabled; with `focusableWhenDisabled={false}` a native button carries no `aria-disabled` (the `disabled` attribute conveys the state, [UBT-ARIA-04](../architecture/button-behavior.md)); a non-native button carries `aria-disabled="true"` exactly while disabled ([UBT-ARIA-02](../architecture/button-behavior.md)).
  Source: `utils/useFocusableWhenDisabled.ts:38-43`. Test: [L2] `ToolbarButton.test.tsx` "disables the button", "uses the disabled attribute when focusableWhenDisabled is false" (`not.toHaveAttribute('aria-disabled')`), Select "disabled state" (non-native `aria-disabled="true"`).
  Port note 2026-10-27 (ADR 12 Amendments 2026-10-26 item 3): re-cited. Select's non-native "disabled state" (M5) is asserted by the non-native rows of `ToolbarButtonTests` "port: type or role, disabled and aria-disabled follow the composite rows of NativeButton, FocusableWhenDisabled and Disabled".
- **TLB-ARIA-04** — MUST. `Toolbar.Input` MUST carry `aria-disabled="true"` exactly while effectively disabled and no `aria-disabled` otherwise, regardless of `focusableWhenDisabled`.
  Source: `toolbar/input/ToolbarInput.tsx:40-45`; `utils/useFocusableWhenDisabled.ts:38-43` (`!isNativeButton && disabled`). Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links" (textboxes), "toolbar items can be focused when disabled by default".
- **TLB-ARIA-05** — MUST. `Toolbar.Link` MUST never carry `aria-disabled`.
  Source: `toolbar/link/ToolbarLink.tsx:32-43`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links"; `ToolbarGroup.test.tsx` "disables all toolbar items except links in the group".
- **TLB-ARIA-06** — MUST. `Toolbar.Group` MUST carry `role="group"` by default; it carries no `aria-disabled` (disabled state is conveyed by its items and `data-disabled`).
  Source: `toolbar/group/ToolbarGroup.tsx:46`. Test: [L2] `ToolbarGroup.test.tsx` "renders a group".
- **TLB-ARIA-07** — MUST. `Toolbar.Separator` MUST carry `role="separator"` and `aria-orientation` equal to its resolved orientation (perpendicular to the toolbar unless overridden).
  Source: `separator/Separator.tsx:23`; `toolbar/separator/ToolbarSeparator.tsx:20`. Test: [L2] `ToolbarSeparator.test.tsx` "uses a %s separator in a %s toolbar", "allows its orientation to be overridden".
- **TLB-ARIA-08** — MUST. Neither Root nor Group generates an accessible name; consumer `aria-label` / `aria-labelledby` are forwarded and are the only naming mechanism.
  Source: `toolbar/root/ToolbarRoot.tsx:71`; `toolbar/group/ToolbarGroup.tsx:46`. Test: none. Docs: `components/toolbar/demos/hero/css-modules/index.tsx` (`aria-label="Numerical format"` on a Group, `aria-label="Alignment"` on a ToggleGroup).
  Port note 2026-10-27: asserted by `ToolbarRootTests` "port: the toolbar names nothing itself, and a consumer aria-label is forwarded".
- **TLB-ARIA-09** — MUST. A substituted part keeps its own role and ARIA (`switch` with `data-checked`/`data-unchecked`; `aria-haspopup="menu"`, `"listbox"` + `role="combobox"`, `"dialog"`; `aria-pressed`), layered with the toolbar button's attributes.
  Source: `toolbar/button/ToolbarButton.tsx:54-74`; `internals/useRenderElement.tsx:171-173`. Test: [L2] `ToolbarButton.test.tsx` "renders a switch", "renders a menu trigger" (`aria-haspopup="menu"`), "renders a select trigger" (`role="combobox"`, `aria-haspopup="listbox"`), "renders a popover trigger" (`aria-haspopup="dialog"`), "renders toggle and toggle group" (`aria-pressed`).
  Port note 2026-10-27: the Switch clause is asserted at L2 by `ToolbarButtonTests` "port: a native SwitchRoot substituted into a toolbar button keeps its role and ARIA and is the toolbar's item" (TLB-STR-05's deviation of design point 14.7) and at L4 by `toolbar.spec.ts` "renders a switch"; the popup clauses (Menu, Select, Popover) are open to M4 and M5 (ADR 12 Amendments 2026-10-26 item 3).

## 6. Interactions

### 6.1 Keyboard

Navigation is the composite root's ([CMP](../architecture/composite-and-list-navigation.md) §4.5) with the options: `orientation` (TLB-API-02), `loopFocus` (TLB-API-03), `disabledIndices` (TLB-STA-02), `stopEventPropagation: true` (composite root part default), `enableHomeAndEndKeys` **not set** (`false`), no grid, no `onLoop`, no `modifierKeys`, no `highlightItemOnHover`, direction from the nearest `DirectionProvider`. Activation of a Button is the button mechanism's in composite mode ([UBT](../architecture/button-behavior.md)).

**Navigation keys (toolbar focused on any item that is not a text input, or a text input at a caret boundary):**

| Key | Orientation, direction | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowRight</kbd> | `horizontal`, LTR | focus next navigable item; wraps to first when `loopFocus` | TLB-KEY-01 |
| <kbd>ArrowLeft</kbd> | `horizontal`, LTR | focus previous navigable item; wraps to last when `loopFocus` | TLB-KEY-01 |
| <kbd>ArrowLeft</kbd> | `horizontal`, RTL | focus **next** (forward) item | TLB-KEY-02 |
| <kbd>ArrowRight</kbd> | `horizontal`, RTL | focus **previous** (backward) item | TLB-KEY-02 |
| <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> | `vertical`, any direction | focus next / previous navigable item; wraps when `loopFocus` | TLB-KEY-01 |
| <kbd>ArrowUp</kbd> / <kbd>ArrowDown</kbd> | `horizontal` | not handled (not consumed; a substituted control such as `NumberField.Input` may use them) | TLB-KEY-03 |
| <kbd>ArrowLeft</kbd> / <kbd>ArrowRight</kbd> | `vertical` | not handled (inside an input: native caret movement) | TLB-KEY-03 |
| <kbd>Home</kbd> / <kbd>End</kbd> | any | **not handled** by the toolbar (inside an input: native caret movement) | TLB-KEY-04 |
| navigation key at an end, `loopFocus={false}` | any | nothing; not consumed | TLB-KEY-05 |
| navigation key with <kbd>Shift</kbd>/<kbd>Ctrl</kbd>/<kbd>Alt</kbd>/<kbd>Meta</kbd> | any | not handled | TLB-KEY-07 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any item, enabled or disabled | leave the toolbar (never prevented) | TLB-KEY-14 |

**Text input rows (`Toolbar.Input` whose element is a native text input — an `<input>` that exposes a selection range, or a `<textarea>` — and is enabled):** let `F` = forward key, `B` = backward key of the toolbar's orientation/direction, `start`/`end` = selection start/end, `len` = value length.

| Condition | Behavior | Req |
|---|---|---|
| `start` unknown (not a text-selection input type), or <kbd>Shift</kbd> held, or `start !== end` (text selected) | native caret/selection behavior; no navigation | TLB-KEY-09 |
| key is `F` and `start < len` | native caret move; no navigation | TLB-KEY-09 |
| key is `F` and `start === len` (caret at end) | navigate forward | TLB-KEY-09 |
| key is `B` and `start > 0` | native caret move; no navigation | TLB-KEY-09 |
| key is `B` and `start === 0` (caret at start) | navigate backward | TLB-KEY-09 |
| cross-axis key, `0 < start < len` or any non-empty value with caret not at both ends | native (same checks: a non-`B` key with `start < len`, or a non-`F` key with `start > 0`, returns to native) | TLB-KEY-09 |
| value is empty (`len === 0`) | `F`/`B` navigate immediately; cross-axis keys do nothing | TLB-KEY-09 |
| focus arrives on the input (keyboard, pointer or programmatic) | the entire value is selected, so the next arrow key first collapses the selection natively | TLB-KEY-10 |
| input is **disabled** (`aria-disabled="true"`) | the text rows do not apply: `F`/`B` always navigate (the key's default is also prevented by TLB-KEY-13) | TLB-KEY-11 |
| `<input type="checkbox">` (no selection range) | treated as a non-text item: `F`/`B` always navigate | TLB-KEY-09 |

**Activation keys (Button, or a substituted button-like part):**

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | native `<button>` | browser click on keydown → `onClick` / trigger opens / toggle flips | TLB-KEY-12 |
| <kbd>Enter</kbd> | `nativeButton={false}` | keydown: default prevented, synthetic click (one `click` seen by capture, substitute, consumer and ancestor listeners) | TLB-KEY-12 |
| <kbd>Space</kbd> | any button (composite mode) | keydown: default prevented, synthetic click; keyup: default prevented on a native `<button>` → exactly one click | TLB-KEY-12 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | disabled Button (`focusableWhenDisabled` true) | default prevented; no click; consumer `onKeyDown` not invoked | TLB-KEY-13 |
| any non-<kbd>Tab</kbd> key | disabled Button/Input with `focusableWhenDisabled` true | default prevented; propagation continues, so arrow keys still navigate | TLB-KEY-13 |
| <kbd>Enter</kbd> | `Toolbar.Link` | native link activation | TLB-KEY-16 |
| <kbd>Space</kbd> | `Toolbar.Link` | not intercepted (no activation; browser default) | TLB-KEY-16 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | `Toggle` in a `ToggleGroup` | toggles ([TGL-KEY-02](toggle.md)); arrows traverse toggles like any item | TLB-KEY-17 |
| <kbd>ArrowDown</kbd> | `Toolbar.Button render={<Select.Trigger />}` (horizontal toolbar) | opens the select (cross-axis key reaches the trigger) | TLB-KEY-15 |
| arrows / Home / End | inside an open popup opened from a toolbar button | consumed by the popup (list navigation) or stopped; the toolbar's focus does not move | TLB-KEY-15 |

- **TLB-KEY-01** — MUST. The toolbar MUST handle the arrow keys of its orientation per [CMP-KEY-01](../architecture/composite-and-list-navigation.md) / [CMP-KEY-03](../architecture/composite-and-list-navigation.md): a forward key moves the tab stop and focus to the next navigable item (skipping disabled indices and natively disabled/invisible elements, [CMP-KEY-08](../architecture/composite-and-list-navigation.md)), a backward key to the previous one, wrapping while `loopFocus` is `true` ([CMP-KEY-06](../architecture/composite-and-list-navigation.md), [CMP-KEY-07](../architecture/composite-and-list-navigation.md)). Items of every kind (Button, Link, Input, grouped items, Toggles) are traversed in DOM order as one sequence.
  Source: `toolbar/root/ToolbarRoot.tsx:63-77`; `internals/composite/root/useCompositeRoot.ts:221-226,267-300`. Test: [L4] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (ltr/horizontal, ltr/vertical).
  Port note 2026-10-27: runs in `toolbar.spec.ts` "$direction, orientation: $orientation" (ltr rows).
- **TLB-KEY-02** — MUST. Under RTL direction (nearest `DirectionProvider`, [CMP-I18N-01](../architecture/composite-and-list-navigation.md)) the horizontal forward key MUST be <kbd>ArrowLeft</kbd> and the backward key <kbd>ArrowRight</kbd> ([CMP-I18N-02](../architecture/composite-and-list-navigation.md)); vertical keys are unchanged.
  Source: `internals/composite/root/CompositeRoot.tsx:43`; `internals/composite/root/useCompositeRoot.ts:221-224`. Test: [L4] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (rtl rows); `ToolbarInput.test.tsx` "respects caret and selection boundaries in horizontal rtl toolbars".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "$direction, orientation: $orientation" (rtl rows) and "respects caret and selection boundaries in horizontal rtl toolbars".
- **TLB-KEY-03** — MUST. Arrow keys of the other axis MUST NOT move focus and MUST NOT be consumed ([CMP-API-02](../architecture/composite-and-list-navigation.md), [CMP-KEY-11](../architecture/composite-and-list-navigation.md)), so a substituted control may use them (e.g. `NumberField.Input` stepping on <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> in a horizontal toolbar, `Select.Trigger` opening on <kbd>ArrowDown</kbd>).
  Source: `internals/composite/root/useCompositeRoot.ts:267-272,282,302`. Test: [L4] `ToolbarInput.test.tsx` "handles interactions" (NumberField, <kbd>ArrowUp</kbd> increments); `ToolbarButton.test.tsx` Select "handles interactions" (<kbd>ArrowDown</kbd> opens).
  Port note 2026-10-27: re-cited (ADR 12 Amendments 2026-10-26 item 3): NumberField's and Select's titles (M5) are asserted by `toolbar.spec.ts` "port: a handled arrow is default-prevented and stopped; the cross-axis arrows, Home, End and a modified arrow are neither".
- **TLB-KEY-04** — MUST. The toolbar MUST NOT handle <kbd>Home</kbd> or <kbd>End</kbd> (the composite option is not enabled): they neither move the tab stop nor are consumed; inside a text input they move the caret natively.
  Source: `toolbar/root/ToolbarRoot.tsx:65-76` (no `enableHomeAndEndKeys`); `internals/composite/root/useCompositeRoot.ts:84,207-210`. Test: none.
  Port note 2026-10-27: asserted by `toolbar.spec.ts` "port: a handled arrow is default-prevented and stopped; the cross-axis arrows, Home, End and a modified arrow are neither".
- **TLB-KEY-05** — MUST. With `loopFocus={false}`, a forward key on the last navigable item and a backward key on the first MUST do nothing (focus stays; event not consumed) ([CMP-KEY-09](../architecture/composite-and-list-navigation.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:73`; `internals/composite/root/useCompositeRoot.ts:282-300,302`. Test: [L4] `ToolbarRoot.test.tsx` "does not wrap focus when loopFocus is false".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "does not wrap focus when loopFocus is false".
- **TLB-KEY-06** — MUST. When a navigation key changes the tab stop, the toolbar MUST call `preventDefault()` and `stopPropagation()` on the keydown ([CMP-KEY-10](../architecture/composite-and-list-navigation.md), [CMP-KEY-15](../architecture/composite-and-list-navigation.md)) and focus the new item one microtask later ([CMP-FOC-02](../architecture/composite-and-list-navigation.md)); the `tabindex` attributes update in the same commit. Server: relaxed, see §9.
  Source: `internals/composite/root/CompositeRoot.tsx:34`; `internals/composite/root/useCompositeRoot.ts:302-316`. Test: [L4] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (`tabindex="0"` and focus asserted after each key). Docs: `CHANGELOG.md v1.0.0-beta.0 "Stop event propagation to allow composite components to be used within popups (#1871)"`.
  Port note 2026-10-27: runs in `toolbar.spec.ts` "toolbar items can be focused when disabled by default", with "port: a handled arrow is default-prevented and stopped; …"; met on both modes (§9, narrowed 2026-10-27).
- **TLB-KEY-07** — MUST. A navigation key pressed with any of <kbd>Shift</kbd>, <kbd>Control</kbd>, <kbd>Alt</kbd>, <kbd>Meta</kbd> held MUST be ignored by the toolbar ([CMP-KEY-02](../architecture/composite-and-list-navigation.md)); for a text input this is also what keeps <kbd>Shift</kbd>+arrow as native selection extension.
  Source: `internals/composite/root/useCompositeRoot.ts:212-214,235`. Test: [L4] `ToolbarInput.test.tsx` "respects caret and selection boundaries in horizontal %s toolbars" (<kbd>Shift</kbd>+forward key keeps focus in the input).
  Port note 2026-10-27: asserted by `toolbar.spec.ts` "port: a handled arrow is default-prevented and stopped; …" (Shift and Control held) and the Shift clause of "respects caret and selection boundaries in horizontal %s toolbars".
- **TLB-KEY-08** — MUST. Navigation MUST treat as *navigable* every registered item that is not in the disabled indices (TLB-STA-02), is visible, and is not natively disabled (`:disabled`); because `disabledIndices` is supplied, an item's `aria-disabled="true"` alone MUST NOT exclude it ([CMP-ALG-02](../architecture/composite-and-list-navigation.md), [CMP-ARIA-03](../architecture/composite-and-list-navigation.md)) — which is how disabled-but-focusable Buttons and Inputs remain arrow-key targets.
  Source: `toolbar/root/ToolbarRoot.tsx:36-46,72`; `floating-ui-react/utils/composite.ts:520-553`. Test: [L4] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (disabled items focused by arrows), "toolbar items can individually disable focusableWhenDisabled" (opted-out item skipped), "keeps an enabled item with focusableWhenDisabled={false} navigable"; `ToolbarInput.test.tsx` "does not block vertical roving focus when disabled".
  Port note 2026-10-27: runs in `toolbar.spec.ts`, the cited titles, over the derived disabled indices (M3 track 1's E3): an `aria-disabled` item is navigable because the indices are supplied.
- **TLB-KEY-09** — MUST. When the keydown's original target is an enabled native text input (an `<input>` whose `selectionStart` is not `null`, or a `<textarea>`; "enabled" = no `disabled` attribute and `aria-disabled` not `"true"`), the toolbar MUST leave the key to the browser (no navigation, not consumed) if any of the following holds: `selectionStart` is `null`; <kbd>Shift</kbd> is held; `selectionStart !== selectionEnd`; the key is not the backward key and `selectionStart < value.length`; the key is not the forward key and `selectionStart > 0`. Otherwise (forward key with the caret at the end of the value, backward key with the caret at the start, or an empty value) the key is handled as a navigation key ([CMP-KEY-04](../architecture/composite-and-list-navigation.md)). The forward/backward keys here are the toolbar's (`horizontal`: <kbd>ArrowRight</kbd>/<kbd>ArrowLeft</kbd>, swapped under RTL; `vertical`: <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd>) — so in a vertical toolbar <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> never navigate and only the caret position (not its visual direction) decides.
  Source: `internals/composite/root/useCompositeRoot.ts:228-246`; `internals/composite/composite.ts:32-42`; `packages/utils/src/isElementDisabled.ts:1-7`. Test: [L4] `ToolbarInput.test.tsx` "respects caret and selection boundaries in horizontal %s toolbars" (ltr, rtl), "orientation: $orientation" (horizontal, vertical). Docs: `components/toolbar/page.mdx` §Usage guidelines.
  Port note 2026-10-27: runs in `toolbar.spec.ts`, the cited titles.
- **TLB-KEY-10** — MUST. When focus arrives anywhere inside the toolbar on a native text input (by arrow key, <kbd>Tab</kbd>, pointer or programmatic focus), the toolbar MUST select the input's entire value (`setSelectionRange(0, value.length)`) ([CMP-KEY-17](../architecture/composite-and-list-navigation.md)); the first arrow key then collapses the selection natively (TLB-KEY-09 "text selected" row) and only the following arrow key at a boundary leaves the input.
  Source: `internals/composite/root/useCompositeRoot.ts:321-328`. Test: [L4] `ToolbarInput.test.tsx` "orientation: $orientation" (`selectionStart === 0`, `selectionEnd === 4` after arrowing in; one extra arrow press before navigation).
  Port note 2026-10-27: runs in `toolbar.spec.ts` "orientation: $orientation".
- **TLB-KEY-11** — MUST. A disabled `Toolbar.Input` (`aria-disabled="true"`) MUST NOT get the text-input exemption of TLB-KEY-09: the toolbar's forward/backward keys always move focus away from it (its default action is additionally prevented by TLB-KEY-13).
  Source: `internals/composite/root/useCompositeRoot.ts:229` (`!isElementDisabled(target)`); `packages/utils/src/isElementDisabled.ts:1-7`. Test: [L4] `ToolbarInput.test.tsx` "does not block vertical roving focus when disabled".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "does not block vertical roving focus when disabled".
- **TLB-KEY-12** — MUST. <kbd>Enter</kbd> and <kbd>Space</kbd> on an enabled `Toolbar.Button` MUST activate it per the button mechanism in composite mode: <kbd>Space</kbd> on **keydown** with the keyup suppressed so exactly one click results ([UBT-KEY-12](../architecture/button-behavior.md), [UBT-KEY-15](../architecture/button-behavior.md)); <kbd>Enter</kbd> natively (native mode, [UBT-KEY-09](../architecture/button-behavior.md)) or as a synthetic click on keydown (non-native, [UBT-KEY-01](../architecture/button-behavior.md)); the synthetic click MUST be a real DOM click observed once by capture-phase, substitute-element, consumer and ancestor listeners ([UBT-EVT-04](../architecture/button-behavior.md)). A substituted trigger or Switch MUST open/toggle from the same key presses ([UBT-KEY-18](../architecture/button-behavior.md): nested mechanisms produce one click).
  Source: `toolbar/button/ToolbarButton.tsx:42-46,72`; `internals/use-button/useButton.ts:116-217`. Test: [L4] `ToolbarButton.test.tsx` "custom element: dispatches real clicks from Space keyboard activation", "custom element: dispatches real clicks from Enter keyboard activation", Switch "handles interactions" (<kbd>Enter</kbd>, <kbd>Space</kbd> toggle once each), Menu/Dialog/AlertDialog/Popover "handles interactions" (<kbd>Enter</kbd> opens), Toggle and ToggleGroup "handles interactions".
  Port note 2026-10-27: re-cited (ADR 12 Amendments 2026-10-26 item 3): runs in `toolbar.spec.ts` "custom element: dispatches real clicks from Space/Enter keyboard activation" and "handles interactions" (Toggle and ToggleGroup block, and the Switch block over a native switch, TLB-STR-05's deviation of design point 14.7); the Menu, Dialog, AlertDialog and Popover clauses are M4's and M5's.
- **TLB-KEY-13** — MUST. On a disabled `Toolbar.Button` or `Toolbar.Input` with `focusableWhenDisabled` `true`, every keydown except <kbd>Tab</kbd> MUST be default-prevented without stopping propagation ([UBT-KEY-20](../architecture/button-behavior.md)); for Button, consumer `onKeyDown`/`onKeyUp` MUST NOT be invoked and no activation occurs ([UBT-KEY-19](../architecture/button-behavior.md)); for Input, a substituted control that honours `defaultPrevented` (e.g. `NumberField.Input`) MUST therefore not act, while the toolbar's own navigation (which does not check `defaultPrevented`) still runs.
  Source: `utils/useFocusableWhenDisabled.ts:23-27`; `toolbar/input/ToolbarInput.tsx:40-45,73`; `internals/use-button/useButton.ts:117-119`; `number-field/input/NumberFieldInput.tsx:272-275`. Test: [L4] `ToolbarButton.test.tsx` "disables the button" (`onKeyDown` uncalled), Menu/Dialog/Popover "disabled state" (<kbd>Enter</kbd>/<kbd>Space</kbd>/arrows do not open); `ToolbarInput.test.tsx` "disabled state" (NumberField value unchanged); `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (arrows still navigate between disabled items).
  Port note 2026-10-27: re-cited (ADR 12 Amendments 2026-10-26 item 3): runs in `toolbar.spec.ts` "disables the button", "disabled state" (Toggle and ToggleGroup block) and "toolbar items can be focused when disabled by default"; the input's prevention is the `button` module's `inputItem` (L3, `js/button.test.ts` "button module: input item"), which NumberField's "disabled state" (M5) relies on; the Menu, Dialog and Popover clauses are M4's and M5's.
- **TLB-KEY-14** — MUST. <kbd>Tab</kbd> and <kbd>Shift</kbd>+<kbd>Tab</kbd> MUST never be prevented by any toolbar part, including on disabled focusable items, so focus can always leave the toolbar; <kbd>Shift</kbd>+<kbd>Tab</kbd> back into the toolbar lands on the item holding the tab stop, even if that item is disabled ([UBT-KEY-21](../architecture/button-behavior.md), [CMP-FOC-01](../architecture/composite-and-list-navigation.md)).
  Source: `utils/useFocusableWhenDisabled.ts:24`; `internals/composite/item/useCompositeItem.ts:25`. Test: [L4] `ToolbarInput.test.tsx` "does not trap keyboard focus when disabled".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "does not trap keyboard focus when disabled", with "port: Tab enters the toolbar on the item holding the tab stop and leaves it in one press".
- **TLB-KEY-15** — MUST. Arrow, <kbd>Home</kbd> and <kbd>End</kbd> keydowns that occur inside a popup opened from a toolbar item MUST NOT reach the toolbar's navigation handler: `Dialog.Popup` / `AlertDialog.Popup` MUST stop propagation of those keys unconditionally, and `Menu.Popup`, `Popover.Popup` and `Select.Popup` MUST stop it when they are rendered within a `Toolbar.Root` subtree; consequently the toolbar's focus and tab stop do not move while the popup is open, and closing the popup returns focus to the toolbar item (TLB-FOC-06). The popup-side counterparts are [DLG-KEY-09](dialog.md) (also adopted by [alert-dialog](alert-dialog.md)), [MNU-KEY-20](menu.md), [POP-KEY-12](popover.md) and [SEL-KEY-14](select.md).
  Source: `dialog/popup/DialogPopup.tsx:82-86`; `menu/popup/MenuPopup.tsx:52,168-173`; `popover/popup/PopoverPopup.tsx:36,95-99`; `select/popup/SelectPopup.tsx:65,446-450`; `internals/composite/composite.ts:22` (`COMPOSITE_KEYS`). Test: [L4] `ToolbarButton.test.tsx` Dialog "prevents composite keydowns from escaping", AlertDialog "prevents composite keydowns from escaping", Menu "handles interactions" (<kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> move within the menu), Select "handles interactions".
  Port note 2026-10-27: open to M4 (Dialog, AlertDialog, Popover) and M5 (Menu, Select) (ADR 12 Amendments 2026-10-26 item 3); it does not hold `implemented`. The popups read `ToolbarRootContext` optionally (DLG-KEY-09, MNU-KEY-20, POP-KEY-12, SEL-KEY-14).
- **TLB-KEY-16** — MUST. `Toolbar.Link` MUST NOT intercept <kbd>Enter</kbd> or <kbd>Space</kbd> (it does not use the button mechanism): <kbd>Enter</kbd> follows the link natively; <kbd>Space</kbd> has the browser's default behavior on an anchor (no activation).
  Source: `toolbar/link/ToolbarLink.tsx:32-43` (composite item only). Test: none.
  Port note 2026-10-27: the link has no button mechanism; its consumer `onkeydown` runs once through the item gate (`ToolbarLinkTests` "port: a link's C# onkeydown runs once through the item gate").
- **TLB-KEY-17** — MUST. Toggles that are items of the toolbar (direct `ToggleGroup` members or substituted into `Toolbar.Button`) MUST toggle on <kbd>Enter</kbd>/<kbd>Space</kbd> ([TGL-KEY-02](toggle.md)) and MUST be traversed by the toolbar's arrow keys like any other item; a disabled direct member (metadata `focusableWhenDisabled: false`) MUST be skipped, while a disabled toggle substituted into a `Toolbar.Button` (default `focusableWhenDisabled: true`) MUST remain an arrow-key target that refuses to toggle.
  Source: `toggle/Toggle.tsx:116-135`; `toolbar/button/ToolbarButton.tsx:37-40`. Test: [L4] `ToolbarButton.test.tsx` "handles interactions", "disabled state", "navigates and selects direct ToggleGroup > Toggle children", "skips disabled direct ToggleGroup > Toggle children", "supports multiple selection for direct ToggleGroup > Toggle children".
  Port note 2026-10-27: runs in `toolbar.spec.ts`, the Toggle and ToggleGroup block and the direct ToggleGroup > Toggle titles.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| click on an enabled Button | any pointer / programmatic | consumer `onClick` once; substituted trigger opens / Switch toggles / Toggle flips | TLB-PTR-01 |
| click / mousedown / pointerdown on a disabled Button | `focusableWhenDisabled` true | click and pointerdown default-prevented; consumer `onClick`/`onMouseDown`/`onPointerDown` not invoked; nothing opens | TLB-PTR-01 |
| hover / mouse move over a disabled Button | any | `onMouseMove` etc. still fire (tooltips work) | TLB-PTR-01 |
| click / pointerdown on a disabled Input | any | default-prevented: the input does not take focus on pointerdown, a checkbox input does not toggle | TLB-PTR-02 |
| click on an enabled Input | any | native: input focuses (value selected, TLB-KEY-10), checkbox toggles | TLB-PTR-02 |
| mouse move over any item | any | no focus / highlight change | TLB-PTR-03 |
| click on any focusable item | any | the item receives focus and becomes the tab stop | TLB-PTR-04 |
| click on a Link | any | native navigation; no toolbar handling | TLB-PTR-05 |

- **TLB-PTR-01** — MUST. `Toolbar.Button` pointer behavior MUST be the button mechanism's: enabled clicks reach the consumer's `onClick` once ([UBT-PTR-05](../architecture/button-behavior.md)); while disabled, `click` and `pointerdown` are default-prevented and `onClick`/`onMouseDown`/`onPointerDown` are not invoked ([UBT-PTR-01](../architecture/button-behavior.md) … [UBT-PTR-03](../architecture/button-behavior.md)); all other handlers keep firing while disabled ([UBT-PTR-04](../architecture/button-behavior.md)), which the default (non-substituted) button guarantees by not carrying a `disabled` attribute (TLB-API-09).
  Source: `toolbar/button/ToolbarButton.tsx:63-73`; `internals/use-button/useButton.ts:104-115,218-224,228`. Test: [L4] `ToolbarButton.test.tsx` "disables the button", "allows hover handlers while blocking activation", Switch "handles interactions" (click toggles; `onClick` once), Switch/Menu/Dialog/Popover "disabled state" (click does nothing).
  Port note 2026-10-27: re-cited (ADR 12 Amendments 2026-10-26 item 3): runs in `toolbar.spec.ts` "disables the button" and "allows hover handlers while blocking activation", and the Switch clause over a native switch ("handles interactions", "disabled state"; TLB-STR-05's deviation of design point 14.7); the popup clauses are M4's and M5's.
- **TLB-PTR-02** — MUST. While disabled, `Toolbar.Input` MUST default-prevent `click` and `pointerdown` (so a pointer press neither focuses it nor performs the input's default action such as toggling a checkbox); when enabled it MUST attach no pointer behavior of its own.
  Source: `toolbar/input/ToolbarInput.tsx:53-62,73`. Test: [L4] `ToolbarInput.test.tsx` "does not steal focus while disabled and becomes pointer-focusable when enabled", "prevents click default actions while disabled".
  Port note 2026-10-27: runs in `toolbar.spec.ts`, the two cited titles (the `button` module's `inputItem`).
- **TLB-PTR-03** — MUST. Moving the pointer over an item MUST NOT focus it or move the tab stop (`highlightItemOnHover` is not enabled, [CMP-PTR-01](../architecture/composite-and-list-navigation.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:65-76`; `internals/composite/root/CompositeRoot.tsx:38`. Test: none.
  Port note 2026-10-27: asserted by `toolbar.spec.ts` "port: moving the pointer over an item neither focuses it nor moves the tab stop".
- **TLB-PTR-04** — MUST. Any item receiving focus (pointer press on a focusable item, programmatic focus) MUST become the tab stop ([CMP-PTR-02](../architecture/composite-and-list-navigation.md)). Server: relaxed, see §9.
  Source: `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L4] `ToolbarInput.test.tsx` "does not steal focus while disabled and becomes pointer-focusable when enabled" (click focuses the enabled input); `ToggleGroup.test.tsx` "preserves selection and roving focus when nested in Toolbar.Group".
  Port note 2026-10-27: asserted by `toolbar.spec.ts` "port: a pointer press on an item makes it the tab stop", met on both modes (§9, narrowed 2026-10-27).
- **TLB-PTR-05** — MUST. `Toolbar.Link` MUST attach no pointer handling; clicks navigate natively.
  Source: `toolbar/link/ToolbarLink.tsx:32-43`. Test: none.

### 6.3 Focus management

- **TLB-FOC-01** — MUST. The toolbar MUST occupy exactly one position in the sequential tab order: the item whose index equals the highlighted index carries `tabindex="0"`, every other item `tabindex="-1"` ([CMP-DOM-01](../architecture/composite-and-list-navigation.md), [CMP-FOC-01](../architecture/composite-and-list-navigation.md)); this includes items inside `Toolbar.Group`, popup roots, tooltip roots and nested `ToggleGroup`s.
  Source: `internals/composite/item/useCompositeItem.ts:19,25`. Test: [L4] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (<kbd>Tab</kbd> lands on `button1`; items traversed thereafter by arrows); `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children".
  Port note 2026-10-27, re-tagged from `[L2]` (ADR 04 Amendments 2026-10-22: the `composite` module owns every item's roving `tabindex` from attach; ADR 13 §3; chosen by the user 2026-10-26, M3 plan §Settled): both cited titles run in `toolbar.spec.ts`, with "port: Tab enters the toolbar on the item holding the tab stop and leaves it in one press".
- **TLB-FOC-02** — MUST. The initial tab stop MUST be the first item (index `0`) unless that item is in the disabled indices or natively disabled, in which case it MUST move to the first navigable item (TLB-STA-04); this re-validation MUST also happen after the disabled indices are first derived from item metadata, so that server/first-render markup with `tabindex="0"` on a disabled-non-focusable first item is corrected before paint. Server: relaxed, see §9.
  Source: `internals/composite/root/useCompositeRoot.ts:137-161,166-193`. Test: [L4] `ToolbarRoot.test.tsx` "moves the initial tab stop off a disabled, non-focusable first item". (L4 for the before-paint timing, relaxed on Server)
  Port note 2026-10-27, re-tagged from `[L2]`: as TLB-STA-04's note.
- **TLB-FOC-03** — MUST. Disabled Buttons and Inputs with `focusableWhenDisabled` `true` MUST remain in the roving order: arrow keys focus them, they may hold the tab stop, and they carry `aria-disabled="true"` and `data-disabled` while focused ([UBT-FOC-03](../architecture/button-behavior.md), [UBT-FOC-05](../architecture/button-behavior.md)).
  Source: `toolbar/root/ToolbarRoot.tsx:39-43`; `utils/useFocusableWhenDisabled.ts:30-47`. Test: [L4] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default"; `ToolbarInput.test.tsx` "does not trap keyboard focus when disabled".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "toolbar items can be focused when disabled by default" and "does not trap keyboard focus when disabled", over the derived disabled indices (M3 track 1's E3).
- **TLB-FOC-04** — MUST. A disabled Button or Input with `focusableWhenDisabled={false}` MUST be excluded from the roving order (never focused by arrows, never the tab stop); an **enabled** item with `focusableWhenDisabled={false}` MUST remain navigable; a Button so excluded is also natively disabled (`disabled` attribute, TLB-DOM-04) while an Input so excluded is only `aria-disabled` (it cannot be natively disabled) and is kept out by `tabindex="-1"` and the disabled indices.
  Source: `toolbar/root/ToolbarRoot.tsx:36-46`; `utils/useFocusableWhenDisabled.ts:45-47`. Test: [L4] `ToolbarRoot.test.tsx` "toolbar items can individually disable focusableWhenDisabled", "keeps an enabled item with focusableWhenDisabled={false} navigable", "skips a disabled Toolbar.Input with focusableWhenDisabled={false}".
  Port note 2026-10-27: runs in `toolbar.spec.ts`, the three cited titles.
- **TLB-FOC-05** — MUST. Focus arriving on a native text input inside the toolbar MUST select its whole value (TLB-KEY-10); focus arriving on any other item MUST NOT change selection.
  Source: `internals/composite/root/useCompositeRoot.ts:321-328`. Test: [L4] `ToolbarInput.test.tsx` "orientation: $orientation".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "orientation: $orientation" (`selectionStart` 0, `selectionEnd` 4 after arrowing in).
- **TLB-FOC-06** — MUST. When a popup is opened from a substituted toolbar trigger by keyboard, focus MUST move into the popup per that popup's own rules (first menu item for Menu, the popup element for Dialog/AlertDialog, the highlighted option for Select) and, on close, MUST return to the toolbar item, which still holds the tab stop; Toolbar adds nothing to this beyond TLB-KEY-15.
  Source: `toolbar/button/ToolbarButton.tsx:54-74` (no focus logic); popup behavior per [popup-store-and-handles](../architecture/popup-store-and-handles.md). Test: [L4] `ToolbarButton.test.tsx` Menu "handles interactions" (`item-1` focused, trigger refocused after <kbd>Escape</kbd>), Dialog/AlertDialog/Popover "handles interactions", Select "handles interactions".
  Port note 2026-10-27: open to M4 (Dialog, AlertDialog, Popover) and M5 (Menu, Select) (ADR 12 Amendments 2026-10-26 item 3); it does not hold `implemented`.
- **TLB-FOC-07** — MUST. When items mount, unmount or move, the tab stop MUST follow the element that held it, falling back to the first navigable item when that element is gone ([CMP-STA-26](../architecture/composite-and-list-navigation.md), [CMP-STA-27](../architecture/composite-and-list-navigation.md)).
  Source: `internals/composite/root/useCompositeRoot.ts:115-134`. Test: none in this slice. Docs: `CHANGELOG.md v1.0.0-beta.4 "Fix \`CompositeList\` not updating item order on reordering (#2675)"`.
- **TLB-FOC-08** — MUST. A disabled focusable `Toolbar.Input` MUST be reachable by arrow keys (TLB-FOC-03) and by <kbd>Shift</kbd>+<kbd>Tab</kbd> from outside when it holds the tab stop, but MUST NOT take focus from a pointer press (TLB-PTR-02).
  Source: `toolbar/input/ToolbarInput.tsx:53-62`; `internals/composite/item/useCompositeItem.ts:25`. Test: [L4] `ToolbarInput.test.tsx` "does not trap keyboard focus when disabled" (<kbd>Shift</kbd>+<kbd>Tab</kbd> lands on the disabled input), "does not steal focus while disabled and becomes pointer-focusable when enabled".
  Port note 2026-10-27: runs in `toolbar.spec.ts` "does not trap keyboard focus when disabled" and "does not steal focus while disabled and becomes pointer-focusable when enabled".
- **TLB-FOC-09** — MUST. No toolbar part MUST move focus on its own initiative (no focus on pointer press, no blur on disable, no auto-focus on mount) ([UBT-FOC-06](../architecture/button-behavior.md)); only arrow-key navigation (TLB-KEY-06) focuses programmatically.
  Source: `toolbar/root/ToolbarRoot.tsx:18-79`, `toolbar/button/ToolbarButton.tsx:17-76`, `toolbar/input/ToolbarInput.tsx:17-76` (no `focus()` calls). Test: none.

### 6.4 Dismissal

Not applicable — Toolbar has no popup of its own; dismissal of substituted triggers' popups is the popup's.

## 7. Events and callbacks

Not applicable — no Toolbar part exposes a callback prop. Consumer DOM handlers on `Toolbar.Button` (`onClick`, `onMouseDown`, `onPointerDown`, `onKeyDown`, `onKeyUp`) are gated by the disabled state per TLB-PTR-01 / TLB-KEY-13; consumer handlers on `Toolbar.Input` run after the part's own handlers (TLB-API-14); handlers on Root, Group, Link and Separator are forwarded unchanged. Callbacks of substituted parts (`onOpenChange`, `onCheckedChange`, `onPressedChange`, `onValueChange`) are those parts' own (see their documents); a disabled toolbar button prevents them from firing (TLB-KEY-13, TLB-PTR-01).

## 8. Form integration

Not applicable — Toolbar adds no form behavior. `Toolbar.Input` is a plain native `<input>` whose `name`, `value`, `defaultValue`, `type` and `form` attributes are forwarded untouched (TLB-API-14), so it participates in forms exactly as the browser dictates; its disabled state is ARIA-only and does **not** remove it from form submission (§16). `Toolbar.Button` is `type="button"` by default ([UBT-FRM-01](../architecture/button-behavior.md)); a consumer `type="submit"` is forwarded.

## 9. Lifecycle, mounting and animation

Not applicable — all parts are always mounted; no transition status or completion callback.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

* TLB-STA-04, TLB-FOC-02, TLB-EDGE-07 — CMP-STA-10 row (post-commit, pre-paint C# state work): after paint plus a round trip; observable as the latency of the first publication of the items, before which every item renders `tabindex="-1"` and the toolbar has no tab stop, and of the items' disabled flags, which arrive in an `update` (also the "`update`-carried value" row), before which the disabled-index re-validation has nothing to read.
* TLB-API-05 — EVD-EVT-26 row against composite key handling, under option (i) (ADR 04 Amendments 2026-10-22, chosen by the user 2026-10-23): the root's and the focused item's consumer C# `onkeydown` run inside `navigate` on WebAssembly, where `PreventPartHandler()` vetoes the key; on Server the key is handled regardless (fallback "run") and the handlers run once from the module's handled-key report, a round trip late. A consumer JavaScript listener calling `preventDefault()` is honoured on both modes (ADR 04 Amendments 2026-10-18).
* TLB-API-16 — the same row's 2026-10-26 sentence (ADR 01 Amendments 2026-10-26, chosen by the user): a C# `onkeydown` on a `Toolbar.Group`, between the item and the root, never runs for a key the module handles, on either mode.

Narrowed 2026-10-27 (M3 track 4; ADR 04 Amendments 2026-10-22): TLB-KEY-06 and TLB-PTR-04 leave the CMP-STA-10 entry. JavaScript writes the roving `tabindex` in the task of the focus move, so both are met on both modes (`toolbar.spec.ts` "port: a pointer press on an item makes it the tab stop", "port: a handled arrow is default-prevented and stopped; …"). TLB-API-05 takes option (i)'s Server text and TLB-API-16 the row's 2026-10-26 sentence.

## 10. Positioning

Not applicable — no floating parts of its own.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `CompositeRoot` (Root) | [CMP](../architecture/composite-and-list-navigation.md) | `orientation` = prop (`'horizontal'` default; never `'both'`); `loopFocus` = prop (`undefined` → `true`); `disabledIndices` = indexes with `disabled && !focusableWhenDisabled` (TLB-STA-02); `onMapChange` = store the item map; `stopEventPropagation: true` (default); `enableHomeAndEndKeys` unset (`false`); `highlightItemOnHover` unset (`false`); no `grid`, `onLoop`, `modifierKeys`, `highlightedIndex`; tag `div`; props `[{ 'aria-orientation', role: 'toolbar' }, consumerProps]`; direction from `DirectionProvider` |
| `CompositeItem` (Button) | CMP | `tag="button"`; `metadata: { disabled, focusableWhenDisabled }`; refs `[consumer, buttonHandle]` after the composite handle; props `[consumerProps, render ? { disabled } : {}, buttonPropsResolver]` |
| `CompositeItem` (Link) | CMP | `tag="a"`; constant `metadata: { disabled: false, focusableWhenDisabled: true }`; props `[consumerProps]` |
| `CompositeItem` (Input) | CMP | `tag="input"`; `metadata: { disabled, focusableWhenDisabled }`; props `[{ onClick, onPointerDown } (prevent when disabled), consumerProps, focusableWhenDisabledProps]` |
| `useButton` (Button) | [UBT](../architecture/button-behavior.md) | `{ disabled: effectiveDisabled, focusableWhenDisabled (default true), native: nativeButton (default true) }`; `composite` inferred (`true`, Root is a composite root); the DOM `disabled` correction (UBT-DOM-10) is what strips `disabled` from substituted triggers |
| `useFocusableWhenDisabled` (Input, direct) | UBT | `{ composite: true, disabled: effectiveDisabled, focusableWhenDisabled (default true), isNativeButton: false }` → `aria-disabled` + key suppression only ([UBT-DOM-12](../architecture/button-behavior.md)) |
| `useRenderElement` (Group) | [RND](../architecture/rendering-and-composition.md) | tag `div`; props `[{ role: 'group' }, consumerProps]`; state `{ disabled, orientation }` → `data-disabled`, `data-orientation` |
| `Separator` (Separator) | [separator](separator.md) | `orientation` = opposite of toolbar unless supplied; everything else forwarded |
| `ToolbarRootContext` (provided by Root) | — | `{ disabled, orientation }`; read (required) by Button, Link, Input, Group, Separator; read optionally by ToggleGroup, `Menu.Popup`, `Popover.Popup`, `Select.Popup` |
| `ToolbarGroupContext` (provided by Group) | — | `{ disabled }`; read by Button, Input, ToggleGroup |
| `COMPOSITE_KEYS` | CMP | the key set (`ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home`, `End`) whose propagation popups stop (TLB-KEY-15) |
| `useDirection` | [UTL](../architecture/shared-utilities.md) | via `CompositeRoot`; [UTL-I18N-01](../architecture/shared-utilities.md) |

## 12. Edge cases, invariants and dev warnings

Dev diagnostics are emitted only in non-production builds through `console.error`, prefixed `Base UI: `, deduplicated per message text ([UTL-EDGE-11](../architecture/shared-utilities.md), [UTL-EDGE-12](../architecture/shared-utilities.md)). Toolbar emits no diagnostic of its own.

- **TLB-EDGE-01** — MUST. Rendering `Toolbar.Button`, `Toolbar.Link`, `Toolbar.Input`, `Toolbar.Group` or `Toolbar.Separator` outside `Toolbar.Root` MUST throw `Base UI: ToolbarRootContext is missing. Toolbar parts must be placed within <Toolbar.Root>.` (TLB-STR-02). (Button/Link/Input would additionally need a composite root; the toolbar check fires first.)
  Source: `toolbar/root/ToolbarRootContext.ts:16-20`. Test: [L2] `ToolbarSeparator.test.tsx` "throws a descriptive error when rendered outside Toolbar.Root".
  Port note 2026-10-27: the port's message and its tests are TLB-STR-02's note.
- **TLB-EDGE-02** — MUST. The button-mechanism mismatch diagnostics MUST be emitted for `Toolbar.Button`: substituting a non-`<button>` (e.g. `<Switch.Root />`, which renders a `<span>`) with the default `nativeButton` logs [UBT-EDGE-01](../architecture/button-behavior.md) exactly once; substituting a real `<button>` with `nativeButton={false}` (e.g. `<Select.Trigger nativeButton={false} />` inside a `Toolbar.Button` that keeps its default) logs [UBT-EDGE-02](../architecture/button-behavior.md). The part still works in both cases.
  Source: `internals/use-button/useButton.ts:36-66`. Test: [L2] `ToolbarButton.test.tsx` Switch "renders a switch" / "handles interactions" / "disabled state" (`console.error` called exactly once with the native-expected message), Select "disabled state" (`toErrorDev` with the non-native-expected message).
  Port note 2026-10-27 (ADR 12 Amendments 2026-10-26 item 3): re-cited. The declared-mode check is the button twin's report, written once per session through `ButtonDevelopmentLog` (`ToolbarButtonTests` "port: a declared-mode mismatch of toolbar buttons is written once per session"); the Switch titles run over a native switch, which trips no check (TLB-STR-05's deviation of design point 14.7: Base UI's span shape logs once), and Select's is M5's.
- **TLB-EDGE-03** — MUST. `Toolbar.Link` MUST ignore every disabled source (Root, Group): it never renders `aria-disabled`, `data-disabled` or `disabled`, stays navigable and activatable.
  Source: `toolbar/link/ToolbarLink.tsx:8-12,26`. Test: [L2] `ToolbarRoot.test.tsx` "disables all toolbar items except links"; `ToolbarGroup.test.tsx` "disables all toolbar items except links in the group". (L4 for the navigable and activatable clause)
- **TLB-EDGE-04** — MUST. The `disabled` prop MUST be forwarded to the substituted element only when `render` is given (TLB-API-09); the default `<button>` therefore never carries `disabled` unless `focusableWhenDisabled={false}` (via the mechanism), keeping it hoverable for tooltips.
  Source: `toolbar/button/ToolbarButton.tsx:63-73`. Test: [L2] `ToolbarButton.test.tsx` "allows hover handlers while blocking activation", "disables the button". Docs: `CHANGELOG.md v1.6.0 "Do not forward \`disabled\` to default toolbar button (#4967)"`.
- **TLB-EDGE-05** — MUST. When every item is in the disabled indices (or natively disabled), the highlighted index MUST be kept (TLB-STA-04 fallback): the toolbar then has no reachable tab stop unless the item at that index is disabled-but-focusable, in which case it remains the single tab stop.
  Source: `internals/composite/root/useCompositeRoot.ts:151-161,181-186`. Test: none.
- **TLB-EDGE-06** — MUST. Direction MUST be read only from the nearest `DirectionProvider` ([UTL-I18N-03](../architecture/shared-utilities.md)); a `dir` attribute on `Toolbar.Root` is forwarded to the element but MUST NOT influence key mapping.
  Source: `internals/composite/root/CompositeRoot.tsx:43`; `toolbar/root/ToolbarRoot.tsx:71`. Test: [L4] `ToolbarRoot.test.tsx` "$direction, orientation: $orientation" (both `DirectionProvider` and `dir` supplied; the provider is what the mechanism reads).
  Port note 2026-10-27: asserted by `toolbar.spec.ts` "$direction, orientation: $orientation", where each toolbar carries the `dir` opposite to its `DirectionProvider`; the composite twin sends the provider's direction (`ToolbarRootTests` "port: a consumer role and aria-orientation win over the root's").
- **TLB-EDGE-07** — MUST. Server-rendered markup MUST contain the root with `role`, `aria-orientation`, `data-orientation`/`data-disabled`, every item with its ARIA/data attributes, and `tabindex="-1"` on every item (item indexes are unresolved before the first item-map publication, [CMP-STA-15](../architecture/composite-and-list-navigation.md)); the tab stop appears on the client after the first publication and TLB-STA-04 re-validation. Server: relaxed, see §9.
  Source: `internals/composite/list/useCompositeListItem.ts:43-55`; `internals/composite/item/useCompositeItem.ts:19,25`; `utils/useFocusableWhenDisabled.ts:20-58`. Test: none.
  Port note 2026-10-27: the markup half is asserted by `ToolbarRootTests` "port: an element that is not a toolbar part is not an item, and every item renders tabindex -1 before the module publishes" (C# renders the same markup in prerender).
- **TLB-EDGE-08** — MUST. `Toolbar.Separator` with an explicit `orientation` equal to the toolbar's (e.g. `horizontal` in a `horizontal` toolbar) MUST render that orientation (no validation).
  Source: `toolbar/separator/ToolbarSeparator.tsx:22`. Test: [L2] `ToolbarSeparator.test.tsx` "allows its orientation to be overridden".
- **TLB-EDGE-09** — MUST. Elements inside `Toolbar.Root` that are not Toolbar parts (plain `<button>`, `<input>`, a bare `Toggle` outside any `ToggleGroup`, [TGL-EDGE-09](toggle.md)) MUST NOT become items: arrow keys never reach them and they keep their own native tab stop, breaking the single-tab-stop invariant for that toolbar.
  Source: `toolbar/root/ToolbarRoot.tsx:63-77` (items are only those registering with the registry). Test: none.
  Port note 2026-10-27: asserted by `ToolbarRootTests` "port: an element that is not a toolbar part is not an item, …" and, for a bare toggle, `ToggleToolbarTests` "port: a bare toggle under ToolbarRoot registers nothing and is in composite button mode".
- **TLB-EDGE-10** — MUST. A disabled `Toolbar.Input` MUST still be a live form control (no `disabled` attribute): it is submitted with the form and can be edited programmatically; only pointer focus/activation (TLB-PTR-02) and key defaults (TLB-KEY-13) are suppressed, and a consumer `onChange` can still fire for programmatic value changes.
  Source: `toolbar/input/ToolbarInput.tsx:40-45,53-62`. Test: none (observed consequence; see §16).
  Port note 2026-10-27 (§16 Q4, resolved as observed, chosen by the user 2026-10-26): a disabled `ToolbarInput` renders no `disabled` attribute (`ToolbarInputTests` "port: a disabled input renders aria-disabled=\"true\" over the consumer's and never disabled, from any disabled source"), so it is submitted and editable.
- **TLB-EDGE-11** — MUST. `Toolbar.Input` with a non-text `type` (e.g. `checkbox`) MUST be navigated like a button (no caret rule, TLB-KEY-09 last row) and MUST honour the disabled click guard (TLB-PTR-02).
  Source: `internals/composite/composite.ts:32-42` (`selectionStart` is `null` for checkbox inputs). Test: [L4] `ToolbarInput.test.tsx` "prevents click default actions while disabled".
  Port note 2026-10-27: asserted by `toolbar.spec.ts` "prevents click default actions while disabled" (a checkbox-type input under the `button` module's `inputItem` option).
- **TLB-EDGE-12** — MUST. A `Toolbar.Button` whose substitute is a `Toggle` inside a `ToggleGroup` MUST be registered once in the toolbar (two registrations on one node; the outer wins, TLB-STA-03), MUST toggle the group value on activation, and when disabled MUST remain focusable with `aria-disabled="true"` and no `disabled` attribute (TLB-DOM-08) — unlike a direct `ToggleGroup > Toggle` member, which is natively disabled and skipped.
  Source: `toolbar/button/ToolbarButton.tsx:37-40,60-71`; `toggle/Toggle.tsx:116-135`; `internals/composite/item/CompositeItem.tsx:30`. Test: [L2] `ToolbarButton.test.tsx` "renders toggle and toggle group", "handles interactions", "disabled state" vs "skips disabled direct ToggleGroup > Toggle children".
  Port note 2026-10-27 (ADR 13 §3): stays `[L2]` for the registration and the click (`ToggleGroupToolbarTests` "port: a toggle substituted into a toolbar button registers once, with the button's flags, and changes its group's value"); the cited titles run in `toolbar.spec.ts`, where the `button` module clears the substituted toggle's `disabled` (design point 14.1).
- **TLB-EDGE-13** — MUST. `Toolbar.Root` MUST behave identically whether or not it contains a `Toolbar.Group`, `Toolbar.Separator` or any item; an empty toolbar renders its element and has no tab stop.
  Source: `toolbar/root/ToolbarRoot.tsx:63-77`; `internals/composite/root/useCompositeRoot.ts:111-113`. Test: [L2] `ToolbarRoot.test.tsx` "has role="toolbar"" (empty root).

Inherited dev checks (not re-documented): invalid `render` element error and uppercase-named render function warning from RND; Switch/Toggle/popup diagnostics from their own documents.

## 13. Behaviors evidenced by tests

### `ToolbarRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`) | TLB-DOM-12, TLB-API-04, TLB-API-05 |
| "has role="toolbar"" | TLB-ARIA-01, TLB-EDGE-13 |
| "allows optional consumers both outside and inside a toolbar" | TLB-STR-02 (optional read), TLB-API-02 |
| "$direction, orientation: $orientation" (ltr/horizontal, ltr/vertical, rtl/horizontal, rtl/vertical; browser only) | TLB-STR-01, TLB-STR-03, TLB-API-02, TLB-API-03, TLB-API-05, TLB-KEY-01, TLB-KEY-02, TLB-FOC-01, TLB-DOM-02, TLB-EDGE-06 |
| "does not wrap focus when loopFocus is false" (browser only) | TLB-API-03, TLB-KEY-05 |
| "disables all toolbar items except links" | TLB-API-01, TLB-API-06, TLB-API-12, TLB-API-11, TLB-API-15, TLB-STA-05, TLB-DOM-06, TLB-DOM-09, TLB-ARIA-03, TLB-ARIA-04, TLB-ARIA-05, TLB-EDGE-03 |
| "toolbar items can be focused when disabled by default" (browser only) | TLB-API-07, TLB-API-13, TLB-DOM-04, TLB-DOM-05, TLB-DOM-07, TLB-KEY-06, TLB-KEY-08, TLB-KEY-13, TLB-FOC-03 |
| "toolbar items can individually disable focusableWhenDisabled" (browser only) | TLB-API-07, TLB-STA-02, TLB-DOM-03, TLB-DOM-04, TLB-KEY-08, TLB-FOC-04 |
| "moves the initial tab stop off a disabled, non-focusable first item" (browser only) | TLB-STA-04, TLB-FOC-02, TLB-DOM-05, TLB-KEY-01 |
| "keeps an enabled item with focusableWhenDisabled={false} navigable" (browser only) | TLB-KEY-08, TLB-FOC-04 |
| "skips a disabled Toolbar.Input with focusableWhenDisabled={false}" (browser only) | TLB-API-13, TLB-STA-02, TLB-FOC-04 |

### `ToolbarButton.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`; rendered inside provided toolbar and composite contexts) | TLB-DOM-12, TLB-API-08, TLB-API-10 |
| "custom element: dispatches real clicks from Space keyboard activation" / "custom element: dispatches real clicks from Enter keyboard activation" | TLB-API-08, TLB-KEY-12, TLB-DOM-04 (`role="button"`) |
| "disables the button" | TLB-API-06, TLB-API-09, TLB-API-10, TLB-DOM-03, TLB-DOM-04, TLB-ARIA-03, TLB-KEY-13, TLB-PTR-01, TLB-EDGE-04 |
| "uses the disabled attribute when focusableWhenDisabled is false" | TLB-API-07, TLB-DOM-04, TLB-ARIA-03 |
| "allows hover handlers while blocking activation" (browser only) | TLB-PTR-01, TLB-EDGE-04 |
| Switch "renders a switch" | TLB-STR-05, TLB-ARIA-09, TLB-EDGE-02 |
| Switch "handles interactions" | TLB-KEY-12, TLB-PTR-01, TLB-DOM-05, TLB-EDGE-02 |
| Switch "disabled state" | TLB-DOM-08, TLB-KEY-13, TLB-PTR-01, TLB-FOC-03, TLB-EDGE-02 |
| Menu "renders a menu trigger" | TLB-STR-05, TLB-ARIA-09 |
| Menu "handles interactions" | TLB-KEY-12, TLB-KEY-15, TLB-FOC-06 |
| Menu "disabled state" | TLB-API-09, TLB-DOM-08, TLB-KEY-13, TLB-FOC-03 |
| Select "renders a select trigger" | TLB-STR-05, TLB-ARIA-09 |
| Select "handles interactions" (jsdom only) | TLB-KEY-03, TLB-KEY-15, TLB-FOC-06 |
| Select "disabled state" | TLB-API-09, TLB-ARIA-03 (non-native), TLB-DOM-08, TLB-KEY-13, TLB-EDGE-02 |
| Dialog "renders a dialog trigger" | TLB-STR-05 |
| Dialog "handles interactions" | TLB-KEY-12, TLB-FOC-06 |
| Dialog "disabled state" | TLB-API-09, TLB-DOM-08, TLB-KEY-13, TLB-FOC-03 |
| Dialog "prevents composite keydowns from escaping" | TLB-KEY-15, TLB-FOC-06, [DLG-KEY-09](dialog.md) |
| AlertDialog "handles interactions" | TLB-KEY-12, TLB-FOC-06 |
| AlertDialog "prevents composite keydowns from escaping" | TLB-KEY-15, TLB-FOC-06, [DLG-KEY-09](dialog.md) (adopted by [alert-dialog](alert-dialog.md)) |
| Popover "renders a popover trigger" | TLB-STR-05, TLB-ARIA-09 |
| Popover "handles interactions" | TLB-KEY-12, TLB-FOC-06 |
| Popover "disabled state" | TLB-API-09, TLB-DOM-08, TLB-KEY-13, TLB-FOC-03 |
| Toggle and ToggleGroup "renders toggle and toggle group" | TLB-STR-05, TLB-ARIA-09, TLB-EDGE-12 |
| Toggle and ToggleGroup "handles interactions" | TLB-KEY-12, TLB-KEY-17, TLB-FOC-01 |
| Toggle and ToggleGroup "disabled state" | TLB-STA-03, TLB-DOM-08, TLB-KEY-13, TLB-KEY-17, TLB-EDGE-12 |
| "navigates and selects direct ToggleGroup > Toggle children" | TLB-STR-01, TLB-STR-04, TLB-KEY-17, TLB-FOC-01 |
| "skips disabled direct ToggleGroup > Toggle children" (browser only) | TLB-STA-02, TLB-STA-03, TLB-KEY-08, TLB-KEY-17, TLB-EDGE-12 |
| "supports multiple selection for direct ToggleGroup > Toggle children" | TLB-STR-04, TLB-KEY-17 |
| "supports a controlled ToggleGroup value" | TLB-STR-04 |
| "disables direct ToggleGroup children when Toolbar.Group is disabled" | TLB-API-15, TLB-STA-05, TLB-STA-02 |
| "skips a direct Toggle that becomes disabled at runtime" (browser only) | TLB-STA-06, TLB-STA-02 |

### `ToolbarInput.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLInputElement`, `testRenderPropWith: 'input'`; rendered inside provided contexts) | TLB-DOM-12, TLB-API-14 |
| "does not steal focus while disabled and becomes pointer-focusable when enabled" | TLB-PTR-02, TLB-PTR-04, TLB-FOC-08 |
| "prevents click default actions while disabled" | TLB-PTR-02, TLB-EDGE-11, TLB-API-14 |
| "respects caret and selection boundaries in horizontal %s toolbars" (ltr, rtl; browser only) | TLB-KEY-09, TLB-KEY-07, TLB-KEY-02 |
| "orientation: $orientation" (horizontal, vertical; browser only) | TLB-KEY-09, TLB-KEY-10, TLB-FOC-05 |
| "does not trap keyboard focus when disabled" (browser only) | TLB-KEY-14, TLB-FOC-03, TLB-FOC-08 |
| "does not block vertical roving focus when disabled" (browser only) | TLB-KEY-11, TLB-KEY-08 |
| "renders NumberField.Input" | TLB-API-14 (`render` substitution) |
| "handles interactions" (NumberField) | TLB-KEY-03, TLB-DOM-07 (`tabindex="0"`) |
| "disabled state" (NumberField) | TLB-API-12, TLB-DOM-07, TLB-ARIA-04, TLB-KEY-13 |

### `ToolbarGroup.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; rendered inside provided contexts) | TLB-DOM-12, TLB-API-16 |
| "renders a group" | TLB-DOM-09, TLB-ARIA-06 |
| "disables all toolbar items except links in the group" | TLB-API-15, TLB-STA-05, TLB-ARIA-03, TLB-ARIA-04, TLB-ARIA-05, TLB-EDGE-03 |

### `ToolbarLink.test.tsx` / `ToolbarLink.spec.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLAnchorElement`, `testRenderPropWith: 'a'`; rendered inside provided contexts) | TLB-DOM-12, TLB-API-11 |
| "renders an anchor" | TLB-DOM-06 |
| `ToolbarLink.spec.tsx`: the `render` callback's props are typed with `href` | §3.7 (`Toolbar.Link.Props` type; no runtime requirement) |

### `ToolbarSeparator.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLDivElement`; rendered inside `Toolbar.Root`) | TLB-DOM-12, TLB-API-18 |
| "uses a %s separator in a %s toolbar" (vertical-in-horizontal, horizontal-in-vertical) | TLB-API-17, TLB-DOM-10, TLB-ARIA-07 |
| "allows its orientation to be overridden" | TLB-API-17, TLB-EDGE-08 |
| "throws a descriptive error when rendered outside Toolbar.Root" | TLB-STR-02, TLB-EDGE-01 |

### Additional requirements introduced here

- **TLB-DOM-12** — MUST. Every Toolbar part MUST pass the shared conformance suite from RND ([RND-DOM-36](../architecture/rendering-and-composition.md) … [RND-DOM-48](../architecture/rendering-and-composition.md)) when rendered with its required ancestors (Root alone; Button/Link/Input/Group inside provided toolbar and composite shared state; Separator inside `Toolbar.Root`): custom attributes forwarded to the default element and to substitutes, `style` merging, DOM handle resolving to `HTMLDivElement` (Root, Group, Separator), `HTMLButtonElement` (Button), `HTMLAnchorElement` (Link), `HTMLInputElement` (Input), wrapped substitutes, `className` merging; Button additionally passes the `div` + `nativeButton={false}` re-run ([RND-DOM-49](../architecture/rendering-and-composition.md)).
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] "Base UI component API" in all six test files.
  Port note 2026-10-27 (incidental under ADR 13 Amendments 2026-10-22): the six suites are `ToolbarRootConformanceTests` (alone), `ToolbarButtonConformanceTests` (`Button`, render prop with a `button`, RND-DOM-49's re-run), `ToolbarLinkConformanceTests` (`a`), `ToolbarInputConformanceTests` (`input`), `ToolbarGroupConformanceTests` and `ToolbarSeparatorConformanceTests`, each part but the root inside a real `ToolbarRoot`.

## 14. React-specific notes and porting considerations

Non-normative.

- **Metadata-driven disabled indices arrive one render late.** `Toolbar.Root` learns which items are non-focusable only when the item registry publishes the map (post-commit, pre-paint) into React state, which triggers a second render that passes the new `disabledIndices` to the composite root. The composite root then re-validates the tab stop in a layout effect ([CMP-STA-29](../architecture/composite-and-list-navigation.md)). A port that can compute metadata synchronously during registration may do this in one pass; the observable contract is TLB-STA-04 / TLB-FOC-02 (correct before paint). Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (CMP-STA-10: after paint plus a round trip on Server) and decisions/04-js-interop-boundary.md §1 (`composite` module; C# receives active-index changes).
- **Metadata identity.** Items memoize their metadata object on `[disabled, focusableWhenDisabled]`; the registry republishes only on identity change ([CMP-STA-09](../architecture/composite-and-list-navigation.md)). A port must republish on value change (TLB-STA-06). Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry).
- **Two registrations on one node.** `Toolbar.Button render={<Toggle />}` inside a `ToggleGroup` registers the same `<button>` twice (Toggle's `CompositeItem` and Toolbar.Button's); the outer one's ref attaches last and wins ([CMP-STR-04](../architecture/composite-and-list-navigation.md)). The port needs a "last registration for an element wins" registry or an explicit outer/inner priority. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry move detection).
- **Props flow through substitution.** With `render={<Menu.Trigger />}`, the toolbar button's resolved props (roving `tabIndex`, `type`, `aria-disabled`, `disabled`, wrapped handlers) are merged onto the trigger element's own props (`mergeProps(props, render.props)`) and become the trigger component's *consumer* props; inside the trigger they are merged before its internal props, so the toolbar's handlers run first ([RND-EVT-02](../architecture/rendering-and-composition.md) applied twice). This is why a disabled `Toolbar.Input` suppresses `NumberField.Input`'s key handling (it checks `defaultPrevented`) and why a nested `useButton` pair yields one click ([UBT-KEY-18](../architecture/button-behavior.md)). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision items 1 and 3 (substitution forms; handler chaining and `PreventBaseUIHandler()` veto) and decisions/04-js-interop-boundary.md §1 (`button` module) and §3 (`decide()` "run" fallback, EVD-EVT-26).
- **DOM `disabled` correction.** The outer `useButton` of a disabled focusable `Toolbar.Button` resets `element.disabled = false` from its callback ref and a layout effect ([UBT-ALG-04](../architecture/button-behavior.md)) because the inner part (Toggle/trigger) sets the attribute from the forwarded `disabled` prop. A port with a single attribute pass can simply not emit `disabled` when the outer item is focusable-when-disabled. Port: see decisions/04-js-interop-boundary.md §1 (`button` module: focusable-when-disabled).
- **Composite key isolation in popups.** Dialog/AlertDialog popups stop propagation of `COMPOSITE_KEYS` unconditionally; Menu/Popover/Select do so only when `useToolbarRootContext(true)` is non-null. React portals propagate synthetic events through the component tree, which is why this is needed at all; a port whose popups are outside the toolbar's DOM subtree and whose key handling follows DOM bubbling gets TLB-KEY-15 for free, but must still ensure the toolbar handler is not attached at document level. Port: see decisions/04-js-interop-boundary.md §5 (portals by DOM move after first render) and §1 (`composite` module owns the toolbar keydown handler).
- **`aria-disabled={false}`** is rendered as `"false"` by React (UBT §8); a port must not drop boolean `false` for `aria-*`.
- **`loopFocus` default.** Root forwards `loopFocus` as `undefined` and the composite root applies `true`; the JSDoc default is `true`.
- **Direction.** The composite root reads `useDirection()`; the `dir` attribute the tests also set on Root is inert for key mapping ([UTL-I18N-03](../architecture/shared-utilities.md)).
- **Focus timing.** Arrow-key focus moves in `queueMicrotask` after the keydown; tests `await` user-event calls, which flush microtasks. Tooltip/popup focus-return happens asynchronously (tests use `waitFor`). Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (microtask is a JS-only term) and decisions/04-js-interop-boundary.md §1 (`composite` module).
- **Hook-order stability.** `ToolbarSeparator` is a thin wrapper around `Separator`; `ToolbarGroup`, `ToolbarRoot` are plain composition. No StrictMode-specific guards exist in this slice beyond the registry's ([CMP-STA-10](../architecture/composite-and-list-navigation.md)).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| TLB-STR-01 | MUST | Root is the composite root; all items anywhere in subtree, DOM order | 2 |
| TLB-STR-02 | MUST | Parts outside Root throw (message) | 2 |
| TLB-STR-03 | MUST | Group/Separator are not items | 2 |
| TLB-STR-04 | MUST | Nested ToggleGroup is not a nested composite; toggles are items | 2 |
| TLB-STR-05 | MUST | Substituted triggers/Switch/Toggle are items and themselves | 2 |
| TLB-API-01 | MUST | Root `disabled` default false; cascades | 3.1 |
| TLB-API-02 | MUST | `orientation` default horizontal; aria/data/published | 3.1 |
| TLB-API-03 | MUST | `loopFocus` default true | 3.1 |
| TLB-API-04 | MUST | Root `className`/`style`/`render` per RND | 3.1 |
| TLB-API-05 | MUST | Root other props forwarded; consumer wins; handler veto | 3.1 |
| TLB-API-06 | MUST | Button disabled = root OR group OR prop | 3.2 |
| TLB-API-07 | MUST | Button `focusableWhenDisabled` default true; mechanism + metadata | 3.2 |
| TLB-API-08 | MUST | Button `nativeButton` default true | 3.2 |
| TLB-API-09 | MUST | `disabled` forwarded to substitute only with `render` | 3.2 |
| TLB-API-10 | MUST | Button other props forwarded; five handlers gated | 3.2 |
| TLB-API-11 | MUST | Link: no own props; never disabled | 3.3 |
| TLB-API-12 | MUST | Input disabled = root OR group OR prop; ARIA-only | 3.4 |
| TLB-API-13 | MUST | Input `focusableWhenDisabled` default true | 3.4 |
| TLB-API-14 | MUST | Input `defaultValue`/others forwarded; consumer handlers after part's | 3.4 |
| TLB-API-15 | MUST | Group disabled = root OR prop; published | 3.5 |
| TLB-API-16 | MUST | Group customization/forwarding | 3.5 |
| TLB-API-17 | MUST | Separator orientation = opposite of toolbar unless supplied | 3.6 |
| TLB-API-18 | MUST | Separator other props → Separator part | 3.6 |
| TLB-STA-01 | MUST | Exact state objects per part | 4 |
| TLB-STA-02 | MUST | `disabledIndices` = disabled && !focusableWhenDisabled; recomputed | 4 |
| TLB-STA-03 | MUST | Metadata per item kind; outer registration wins | 4 |
| TLB-STA-04 | MUST | Highlighted index: mechanism-owned, 0, re-validated | 4 |
| TLB-STA-05 | MUST | Disabled cascade Root → Group → items; Links exempt | 4 |
| TLB-STA-06 | MUST | Metadata republished on runtime change | 4 |
| TLB-DOM-01 | MUST | Root `<div>`, data-orientation/data-disabled | 5.1 |
| TLB-DOM-02 | MUST | Root no tabindex; keydown/focus handlers | 5.1 |
| TLB-DOM-03 | MUST | Button data attributes incl. `data-focusable` | 5.2 |
| TLB-DOM-04 | MUST | Button type/role/disabled/aria-disabled matrix | 5.2 |
| TLB-DOM-05 | MUST | Button roving tabindex | 5.2 |
| TLB-DOM-06 | MUST | Link `<a>`: tabindex + data-orientation only | 5.3 |
| TLB-DOM-07 | MUST | Input attributes; never native `disabled` | 5.4 |
| TLB-DOM-08 | MUST | Substituted `<button>` DOM `disabled` reset to false | 5.2 |
| TLB-DOM-09 | MUST | Group `<div role=group>` + data attrs | 5.5 |
| TLB-DOM-10 | MUST | Separator role/aria-orientation/data-orientation | 5.6 |
| TLB-DOM-11 | MUST | Consumer attribute precedence (Input suppression exception) | 5.7 |
| TLB-DOM-12 | MUST | All six parts pass RND conformance suite | 13 |
| TLB-ARIA-01 | MUST | `role="toolbar"` | 5.8 |
| TLB-ARIA-02 | MUST | `aria-orientation` always on Root | 5.8 |
| TLB-ARIA-03 | MUST | Button `aria-disabled` incl. `"false"` default | 5.8 |
| TLB-ARIA-04 | MUST | Input `aria-disabled="true"` iff disabled | 5.8 |
| TLB-ARIA-05 | MUST | Link never `aria-disabled` | 5.8 |
| TLB-ARIA-06 | MUST | Group `role="group"` | 5.8 |
| TLB-ARIA-07 | MUST | Separator `role="separator"` + orientation | 5.8 |
| TLB-ARIA-08 | MUST | No generated accessible names | 5.8 |
| TLB-ARIA-09 | MUST | Substituted parts keep their ARIA | 5.8 |
| TLB-KEY-01 | MUST | Orientation arrow keys traverse all items; wrap | 6.1 |
| TLB-KEY-02 | MUST | RTL swaps horizontal keys | 6.1 |
| TLB-KEY-03 | MUST | Cross-axis keys not handled | 6.1 |
| TLB-KEY-04 | MUST | Home/End not handled | 6.1 |
| TLB-KEY-05 | MUST | `loopFocus=false` stops at ends | 6.1 |
| TLB-KEY-06 | MUST | Handled key: preventDefault + stopPropagation; focus next microtask | 6.1 |
| TLB-KEY-07 | MUST | Modifier keys ignored | 6.1 |
| TLB-KEY-08 | MUST | Navigable = not in disabledIndices, visible, not `:disabled`; aria-disabled alone does not exclude | 6.1 |
| TLB-KEY-09 | MUST | Text input caret/selection rule (exact) | 6.1 |
| TLB-KEY-10 | MUST | Focus on text input selects all | 6.1 |
| TLB-KEY-11 | MUST | Disabled input: no caret exemption | 6.1 |
| TLB-KEY-12 | MUST | Button activation: Space keydown once; Enter; one real click | 6.1 |
| TLB-KEY-13 | MUST | Disabled focusable: non-Tab keys prevented; Button handlers not invoked | 6.1 |
| TLB-KEY-14 | MUST | Tab never prevented; Shift+Tab lands on tab stop | 6.1 |
| TLB-KEY-15 | MUST | Popups stop composite keys from reaching the toolbar | 6.1 |
| TLB-KEY-16 | MUST | Link: Enter native, Space not intercepted | 6.1 |
| TLB-KEY-17 | MUST | Toggles toggle and are traversed; disabled handling by metadata | 6.1 |
| TLB-PTR-01 | MUST | Button pointer gating; hover passes | 6.2 |
| TLB-PTR-02 | MUST | Disabled Input: click/pointerdown prevented | 6.2 |
| TLB-PTR-03 | MUST | Hover never focuses | 6.2 |
| TLB-PTR-04 | MUST | Focused item becomes tab stop | 6.2 |
| TLB-PTR-05 | MUST | Link: native clicks | 6.2 |
| TLB-FOC-01 | MUST | Single tab stop across all items | 6.3 |
| TLB-FOC-02 | MUST | Initial tab stop first navigable item; corrected before paint | 6.3 |
| TLB-FOC-03 | MUST | Disabled focusable items stay in roving order | 6.3 |
| TLB-FOC-04 | MUST | `focusableWhenDisabled=false` disabled items excluded; enabled stay | 6.3 |
| TLB-FOC-05 | MUST | Focus on text input selects all; others unchanged | 6.3 |
| TLB-FOC-06 | MUST | Popup open/close focus per popup; returns to item | 6.3 |
| TLB-FOC-07 | MUST | Tab stop follows element across list changes | 6.3 |
| TLB-FOC-08 | MUST | Disabled input keyboard-reachable, not pointer-focusable | 6.3 |
| TLB-FOC-09 | MUST | No part moves focus on its own | 6.3 |
| TLB-EDGE-01 | MUST | Missing-context error (verbatim) | 12 |
| TLB-EDGE-02 | MUST | `nativeButton` mismatch diagnostics for substitutes | 12 |
| TLB-EDGE-03 | MUST | Link ignores disabled sources | 12 |
| TLB-EDGE-04 | MUST | `disabled` not forwarded to default button | 12 |
| TLB-EDGE-05 | MUST | All-disabled: index kept | 12 |
| TLB-EDGE-06 | MUST | Direction from provider only; `dir` inert | 12 |
| TLB-EDGE-07 | MUST | SSR markup; `tabindex=-1` pre-publication | 12 |
| TLB-EDGE-08 | MUST | Separator override not validated | 12 |
| TLB-EDGE-09 | MUST | Non-part elements are not items | 12 |
| TLB-EDGE-10 | MUST | Disabled Input remains a live form control | 12 |
| TLB-EDGE-11 | MUST | Non-text Input navigated like a button | 12 |
| TLB-EDGE-12 | MUST | Toggle via Toolbar.Button vs direct member disabled behavior | 12 |
| TLB-EDGE-13 | MUST | Empty toolbar renders; no tab stop | 12 |

## 16. Open questions

1. **`cols` prop.** Hinted by the assignment; removed in `CHANGELOG.md v1.0.0-beta.5` ("This prop was not supposed to be exposed"). `toolbar/root/ToolbarRoot.tsx:99-112` has no `cols` and no grid navigation. Not documented as a prop. Resolved 2026-10-26 (chosen by the user, M3 plan §Settled): `ToolbarRoot` has no `Cols` parameter.
2. **Home/End.** The assignment asked for the Home/End rule; `Toolbar.Root` does not pass `enableHomeAndEndKeys` (`toolbar/root/ToolbarRoot.tsx:65-76`), so unlike ToggleGroup the toolbar never handles them (TLB-KEY-04). No test asserts either way. Confirm the port should not add them (WAI-ARIA APG lists Home/End as optional for toolbars). Resolved 2026-10-26 (chosen by the user, M3 plan §Settled): the port does not add them; `EnableHomeAndEndKeys` is `false` (`ToolbarRootTests` "port: the toolbar's composite root sends its orientation and loopFocus, with Home and End off and handled keys stopped"; `toolbar.spec.ts` "port: a handled arrow is default-prevented and stopped; the cross-axis arrows, Home, End and a modified arrow are neither").
3. **`aria-disabled="false"` on enabled toolbar buttons.** With the default `focusableWhenDisabled={true}`, every enabled native `Toolbar.Button` renders `aria-disabled="false"` (`utils/useFocusableWhenDisabled.ts:38-43`). Only the `"true"` case is asserted in toolbar tests; `ToggleGroup.test.tsx` asserts the `"false"` case for toggles. Documented as MUST (TLB-ARIA-03) per the UBT author's flag. Resolved 2026-10-27 by the writer: `ButtonAttributeWriter` renders `aria-disabled="false"` on an enabled native composite button with `FocusableWhenDisabled` true (`ToolbarButtonTests` "port: type or role, disabled and aria-disabled follow the composite rows of NativeButton, FocusableWhenDisabled and Disabled", the first row).
4. **Disabled `Toolbar.Input` is still submitted.** Because `disabled` is ARIA-only (TLB-DOM-07), a disabled input participates in form submission and remains programmatically editable (TLB-EDGE-10). Likely intentional (focusable-when-disabled cannot coexist with the native attribute) but undocumented; confirm for the port. Resolved 2026-10-26 (chosen by the user, M3 plan §Settled): as observed; TLB-EDGE-10's note.
5. **Input suppression precedence.** `toolbar/input/ToolbarInput.tsx:73` merges the `aria-disabled`/`onKeyDown` suppression props *after* consumer props, so a consumer cannot override `aria-disabled` while disabled and consumer `onKeyDown` sees `defaultPrevented === true` (TLB-DOM-11, TLB-API-14). Toolbar.Button merges the other way round (consumer props first, mechanism last — but the mechanism lets non-handler consumer attributes win, UBT-DOM-09). Decide whether the port should unify. Resolved 2026-10-26 (chosen by the user, M3 plan §Settled): mirrored, not unified; TLB-DOM-11's and TLB-API-14's notes.
6. **Space on `Toolbar.Link`.** The link does not use the button mechanism, so <kbd>Space</kbd> is left to the browser (page scroll, no activation) while `Button` with a substituted `<a>` would activate on Space keydown ([UBT-KEY-12](../architecture/button-behavior.md)). No test; documented as observed (TLB-KEY-16). Proposed 2026-10-27 (M3 track 4), pending the user: mirror Base UI. `ToolbarLink` has no button mechanism and leaves Space to the browser.
7. **Cross-axis keys inside a text input.** TLB-KEY-09 as written in the source (`useCompositeRoot.ts:239-244`) lets a cross-axis key pass to the native handler only when the caret is not at the relevant edge; when the value is empty, a cross-axis key reaches the composite handler and is then ignored (not a forward/backward key). Equivalent outcome, but a port that short-circuits cross-axis keys earlier is observably identical only because the composite handler does nothing with them. Proposed 2026-10-27 (M3 track 4), pending the user: as observed; the `composite` module transcribes the source's order.
8. **Vertical toolbar + text input in RTL.** The test comment (`ToolbarInput.test.tsx:137-139`) notes that real browsers reverse caret movement for RTL text, which the test harness cannot simulate; the source decides by `selectionStart` only (TLB-KEY-09). Behavior in real RTL text (caret index vs visual direction) is untested. Proposed 2026-10-27 (M3 track 4), pending the user: an issue for an L4 probe with real RTL text; `toolbar.spec.ts` runs the source's index rule only.
9. **Nested `Toolbar.Root`** would nest composite roots and re-provide the toolbar context; unsupported and untested. Proposed 2026-10-27 (M3 track 4), pending the user: unsupported and documented in `ToolbarRoot`'s remarks; no check.
10. **Popup key isolation ownership.** TLB-KEY-15 is implemented in four popup components (`DialogPopup.tsx:82-86`, `MenuPopup.tsx:168-173`, `PopoverPopup.tsx:95-99`, `SelectPopup.tsx:446-450`), not in Toolbar. Those component documents should carry the matching requirement; this document states the toolbar-side contract. Resolved: the counterparts exist as [DLG-KEY-09](dialog.md) (adopted by alert-dialog), [MNU-KEY-20](menu.md), [POP-KEY-12](popover.md) and [SEL-KEY-14](select.md); TLB-KEY-15 now cites them.
